using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using Debug = UnityEngine.Debug;
using ReadOnlyAttribute = Unity.Collections.ReadOnlyAttribute;
using Unity.Entities;
using andywiecko.BurstTriangulator.LowLevel.Unsafe;
using andywiecko.BurstTriangulator;

namespace Chisel.Core
{
	//[BurstCompile(CompileSynchronously = true)] // FIXME: If enabled, it causes more missing triangles in the mesh
	struct GenerateSurfaceTrianglesJob : IJobParallelForDefer
	{
		// Read
		// 'Required' for scheduling with index count
		[NoAlias, ReadOnly] public NativeList<IndexOrder> allUpdateBrushIndexOrders;

		// Gate the vertex re-add and the T-junction insert on plane incidence; set from CSGManager's kUseIncidenceWeld.
		[NoAlias, ReadOnly] public bool useIncidenceWeld;
		// Canonical vertices (see CanonicalVertices): from Everywhere on, only the same vertex is merged here.
		[NoAlias, ReadOnly] public CanonicalVertexStage canonicalVertexStage;
		[NoAlias, ReadOnly] public NativeList<BlobAssetReference<BrushTreeSpacePlanes>> brushTreeSpacePlaneCache;
		[NoAlias, ReadOnly] public NativeList<BlobAssetReference<BasePolygonsBlob>> basePolygonCache;
		[NoAlias, ReadOnly] public NativeList<NodeTransformations> transformationCache;

		[NativeDisableParallelForRestriction]
		[NoAlias, ReadOnly] public NativeArray<UnsafeList<float3>> loopVerticesLookup;
		[NoAlias, ReadOnly] public NativeList<BlobAssetReference<BrushesTouchedByBrush>> brushesTouchedByBrushCache;

		[NoAlias, ReadOnly] public NativeStream.Reader input;
		[NoAlias, ReadOnly] public NativeArray<MeshQuery> meshQueries;
		[NoAlias, ReadOnly] public CompactHierarchyManagerInstance.ReadOnlyEntityIDLookup entityIDLookup;
		[NoAlias, ReadOnly] public bool subtractiveWorkflow;
		[NoAlias, ReadOnly] public float normalSmoothingAngle;
		// Set when the input comes from ExactCSGJob (triangles already made) instead of PerformCSGJob (loops).
		[NoAlias, ReadOnly] public bool exactInput;
		// Decals (Documentation~/Design/Decals.md), lowest first. Empty when the tree has none.
		[NoAlias, ReadOnly] public NativeArray<DecalVolume> decalVolumes;
		// The surfaces decals are limited to (DecalVolume.targetStart/targetCount)
		[NoAlias, ReadOnly] public NativeArray<ChiselDecalTarget> decalTargets;

		// Write
		[NativeDisableParallelForRestriction]
		[NoAlias] public NativeList<BlobAssetReference<ChiselBrushRenderBuffer>> brushRenderBufferCache;

		[BurstDiscard]
		public static void InvalidFinalCategory(CategoryIndex _interiorCategory)
		{
			Debug.Assert(false, $"Invalid final category {_interiorCategory}");
		}


#if UNITY_EDITOR
		static readonly bool kLogTriangulationIssues = false;

#endif

		static readonly bool kCleanupNonSimpleLoops = true;

		static readonly bool kCloseOpenChains2D = true;

		static readonly bool kInsertTJunctions = true;

		const float kTJunctionPlaneBand = 0.05f;

		static readonly bool kLogTJunctionInsertions = false;

		// Logs this brush's touch-graph neighbours (entityID:IntersectionType), so a brush whose vertex
		// lands on this brush's edge can be checked against the touch graph for presence.
		[BurstDiscard]
		static void LogTouchedNeighbours(ulong entityID, ref BlobArray<BrushIntersection> touched,
										 CompactHierarchyManagerInstance.ReadOnlyEntityIDLookup lookup)
		{
#if UNITY_EDITOR
			if (!kLogTJunctionInsertions)
				return;
			var sb = new System.Text.StringBuilder(160);
			sb.Append($"TJTOUCH b{entityID}:");
			for (int i = 0; i < touched.Length; i++)
			{
				var id = lookup.SafeGetNodeEntityID(touched[i].nodeIndexOrder.compactNodeID);
				sb.Append($" {id}:{touched[i].type}");
			}
			Debug.LogWarning(sb.ToString());
#endif
		}

		static void LogClosestApproach(in UnsafeList<Edge> edges, in NativeList<float3> candidates,
									   HashedVertices vertices, ref double minPerp)
		{
			for (int c = 0; c < candidates.Length; c++)
			{
				double3 w = candidates[c];
				for (int e = 0; e < edges.Length; e++)
				{
					double3 a = vertices[edges[e].index1];
					double3 ab = (double3)vertices[edges[e].index2] - a, aw = w - a;
					double L2 = math.dot(ab, ab);
					if (L2 <= 1e-12)
						continue;
					double t = math.dot(aw, ab) / L2;
					if (t <= 0.0 || t >= 1.0)
						continue;
					double pp = math.length(math.cross(aw, ab)) / math.sqrt(L2);
					if (pp < minPerp) minPerp = pp;
				}
			}
		}

		[BurstDiscard]
		static void LogTJunctionInsertions(ulong entityID, int touched, int withVerts, int planeCand, int inserted, double selfPlaneMax, double minPerp)
		{
#if UNITY_EDITOR
			if (!kLogTJunctionInsertions)
				return;
			var ci = System.Globalization.CultureInfo.InvariantCulture;
			Debug.LogWarning($"TJINS b{entityID}: touched={touched} withVerts={withVerts} planeCand={planeCand} inserted={inserted} selfPlaneMax={selfPlaneMax.ToString("0.######", ci)} minPerp={minPerp.ToString("0.######", ci)}");
#endif
		}

		static readonly bool kLogBoundaryEdges = false;

		[BurstDiscard]
		static unsafe void LogBoundaryEdges(ulong entityID, int surfaceIndex, int loopIndex, in UnsafeList<Edge> edges, float3* verts)
		{
#if UNITY_EDITOR
			if (!kLogBoundaryEdges)
				return;
			var ci = System.Globalization.CultureInfo.InvariantCulture;
			var sb = new System.Text.StringBuilder(256);
			sb.Append($"BEDGE b{entityID} s{surfaceIndex} l{loopIndex}:");
			for (int e = 0; e < edges.Length; e++)
			{
				var a = verts[edges[e].index1];
				var b = verts[edges[e].index2];
				sb.Append($" {a.x.ToString("0.####", ci)},{a.y.ToString("0.####", ci)},{a.z.ToString("0.####", ci)}|{b.x.ToString("0.####", ci)},{b.y.ToString("0.####", ci)},{b.z.ToString("0.####", ci)}");
			}
			sb.Append(" | I:");
			for (int e = 0; e < edges.Length; e++)
				sb.Append($" {edges[e].index1}-{edges[e].index2}");
			Debug.LogWarning(sb.ToString());
#endif
		}

		enum SurfaceDropReason : byte
		{
			SurfaceHasNoLoops,        // all loops on this surface were filtered out before triangulation
			TooFewPointsAfterDedup,   // < 3 unique points / edges left once duplicates were removed
			DegenerateInput,          // the 2D input collapsed to a line / point
			TriangulatorError,        // BurstTriangulator returned a non-OK Status (see decoded flags)
			ZeroTrianglesReturned,    // Status was OK but the triangulator produced no triangles
		}

		[BurstDiscard]
		static void LogSurfaceDrop(ulong entityID, int surfaceIndex, int loopIndex,
								   int pointCount, int edgeCount, SurfaceDropReason reason, Status status)
		{
#if UNITY_EDITOR && DEBUG
			if (!kLogTriangulationIssues)
				return;
			var detail = reason == SurfaceDropReason.TriangulatorError ? $", status={status} (0x{(int)status:X})" : "";
			// NOTE: edgeCount is roVerts.edgeIndices.Length, which is FLATTENED (2 ints per edge), so the
			// real edge count is edgeCount/2.
			Debug.LogWarning($"[Chisel.Triangulation] No geometry for brush {entityID}, surface {surfaceIndex}, " +
							 $"loop {loopIndex}: {reason}{detail} (points={pointCount}, edges={edgeCount}).");
#endif
		}

#if UNITY_EDITOR
		static readonly bool kLogDropDetail = false;
#endif

		[BurstDiscard]
		static void LogDropDetail(ulong entityID, int surfaceIndex, int loopIndex, SurfaceDropReason reason,
								  NativeArray<double2> positions2D, NativeArray<int> edgeIndices)
		{
#if UNITY_EDITOR
			if (!kLogDropDetail)
				return;
			// InvariantCulture so decimals use '.' (not the OS locale's ',') and stay parseable - the
			// only separators inside the line are then the ',' between x and y and spaces between points.
			var ci = System.Globalization.CultureInfo.InvariantCulture;
			var sb = new System.Text.StringBuilder(256);
			sb.Append($"DROPDET b{entityID} s{surfaceIndex} l{loopIndex} {reason} pts={positions2D.Length} edges={edgeIndices.Length / 2} | P:");
			for (int i = 0; i < positions2D.Length; i++)
				sb.Append($" {i}=({positions2D[i].x.ToString("0.####", ci)},{positions2D[i].y.ToString("0.####", ci)})");
			sb.Append(" | E:");
			for (int i = 0; i + 1 < edgeIndices.Length; i += 2)
				sb.Append($" {edgeIndices[i]}-{edgeIndices[i + 1]}");
			Debug.LogWarning(sb.ToString());
#endif
		}

		[BurstDiscard]
		static void SelfIntersectionDetected(int surf, int loopIdx)
		{
#if UNITY_EDITOR && DEBUG
			if (!kLogTriangulationIssues)
				return;
			Debug.LogWarning($"Self-intersection detected in surface {surf}, loop index {loopIdx}.");
#endif
		}

		struct CompareSortByBasePlaneIndex : System.Collections.Generic.IComparer<ChiselQuerySurface>
		{
			public readonly int Compare(ChiselQuerySurface x, ChiselQuerySurface y)
			{
				var diff = x.surfaceParameter.CompareTo(y.surfaceParameter);
				if (diff != 0)
					return diff;
				return x.surfaceIndex - y.surfaceIndex;
			}
		}
		readonly static CompareSortByBasePlaneIndex kCompareSortByBasePlaneIndex = new();


		public unsafe void Execute(int index)
		{
			if (exactInput)
			{
				ExecuteExact(index);
				return;
			}

			var count = input.BeginForEachIndex(index);
			if (count == 0)
				return;

			// Read brush data
			var brushIndexOrder = input.Read<IndexOrder>();
			var brushNodeOrder = brushIndexOrder.nodeOrder;
			var vertexCount = input.Read<int>();

			if (vertexCount < 3)
				return;

			// PerformCSGJob already welded this list; re-adding it may only merge what this brush's own faces allow.
			var weldFilter = WeldIncidenceFilter.Disabled;
			if (canonicalVertexStage >= CanonicalVertexStage.Everywhere)
				weldFilter = WeldIncidenceFilter.SameVertexOnly;   // canonical vertices: only the same vertex is merged
			else
			if (useIncidenceWeld && brushNodeOrder < brushTreeSpacePlaneCache.Length &&
			    brushTreeSpacePlaneCache[brushNodeOrder].IsCreated && basePolygonCache[brushNodeOrder].IsCreated)
			    weldFilter = WeldIncidenceFilter.Create(ref brushTreeSpacePlaneCache[brushNodeOrder].Value.treeSpacePlanes,
			                                            basePolygonCache[brushNodeOrder].Value.polygons.Length);

			HashedVertices brushVertices;
			using var _brushVertices = brushVertices = new HashedVertices(vertexCount, Allocator.Temp);
			for (int v = 0; v < vertexCount; v++)
			{
				var vertex = input.Read<float3>();
				brushVertices.AddNoResize(vertex, in weldFilter);
			}

			// Read surface loops
			var surfaceOuterCount = input.Read<int>();
			NativeList<UnsafeList<int>> surfaceLoopIndices;
			using var _surfaceLoopIndices = surfaceLoopIndices = new NativeList<UnsafeList<int>>(surfaceOuterCount, Allocator.Temp);
			surfaceLoopIndices.Resize(surfaceOuterCount, NativeArrayOptions.ClearMemory);
			try
			{
				for (int o = 0; o < surfaceOuterCount; o++)
				{
					var countInner = input.Read<int>();
					if (countInner > 0)
					{
						var inner = new UnsafeList<int>(countInner, Allocator.Temp);
						for (int i = 0; i < countInner; i++)
						{
							inner.AddNoResize(input.Read<int>());
						}
						surfaceLoopIndices[o] = inner;
					}
					else
						surfaceLoopIndices[o] = default;
				}

				// Read loop infos and edges
				var loopCount = input.Read<int>();
				NativeArray<SurfaceInfo> surfaceLoopAllInfos;
				using var _surfaceLoopAllInfos = surfaceLoopAllInfos = new NativeArray<SurfaceInfo>(loopCount, Allocator.Temp);

				NativeList<UnsafeList<Edge>> surfaceLoopAllEdges;
				using var _surfaceLoopAllEdges = surfaceLoopAllEdges = new NativeList<UnsafeList<Edge>>(loopCount, Allocator.Temp);
				surfaceLoopAllEdges.Resize(loopCount, NativeArrayOptions.ClearMemory);

				// Declared before the try so the finally can dispose it; populated by the cross-brush
				// T-junction pre-pass below (default/uncreated when that pass is disabled or no-op).
				NativeArray<bool> protectedVertices = default;
				// Created only for a brush that decals may reach. Disposed explicitly, never through a copy: its lists
				// reallocate as they grow.
				DecalSurfaceBuilder decalBuilder = default;
				try
				{
					for (int l = 0; l < loopCount; l++)
					{
						surfaceLoopAllInfos[l] = input.Read<SurfaceInfo>();
						var edgeCount = input.Read<int>();
						if (edgeCount > 0)
						{
							var edges = new UnsafeList<Edge>(edgeCount, Allocator.Temp);
							for (int e = 0; e < edgeCount; e++)
							{
								edges.AddNoResize(input.Read<Edge>());
							}
							surfaceLoopAllEdges[l] = edges;
						}
					}
					input.EndForEachIndex();

					if (!basePolygonCache[brushNodeOrder].IsCreated)
						return;

					ulong entityID = entityIDLookup.SafeGetNodeEntityID(brushIndexOrder.compactNodeID);

					// Compute maximum sizes
					int maxLoops = 0, maxIndices = 0;
					for (int s = 0; s < surfaceLoopIndices.Length; s++)
					{
						if (!surfaceLoopIndices[s].IsCreated)
							continue;
						var length = surfaceLoopIndices[s].Length;
						maxIndices += length;
						maxLoops = math.max(maxLoops, length);
					}

					ref var baseSurfaces = ref basePolygonCache[brushNodeOrder].Value.surfaces;
					var transform = transformationCache[brushNodeOrder];
					var treeToNode = transform.treeToNode;
					var nodeToTreeInvTrans = math.transpose(treeToNode);

					protectedVertices = default;
					if (kInsertTJunctions &&
						brushNodeOrder < brushesTouchedByBrushCache.Length &&
						brushesTouchedByBrushCache[brushNodeOrder].IsCreated)
					{
						var tjProtected     = new NativeList<int>(16, Allocator.Temp);
						var planeCandidates = new NativeList<float3>(64, Allocator.Temp);
						var candIdx         = new NativeList<ushort>(64, Allocator.Temp);
						int dbgWithVerts = 0, dbgPlaneCand = 0, dbgInserted = 0;
						double dbgSelfPlaneMax = 0, dbgMinPerp = double.PositiveInfinity;

						ref var touched = ref brushesTouchedByBrushCache[brushNodeOrder].Value.brushIntersections;
						for (int i = 0; i < touched.Length; i++)
						{
							var oo = touched[i].nodeIndexOrder.nodeOrder;
							if (oo != brushNodeOrder && oo >= 0 && oo < loopVerticesLookup.Length &&
								loopVerticesLookup[oo].IsCreated && loopVerticesLookup[oo].Length > 0)
								dbgWithVerts++;
						}
						if (kLogTJunctionInsertions)
							LogTouchedNeighbours(entityID, ref touched, entityIDLookup);

						var neighbourCandidates = new NativeList<float3>(64, Allocator.Temp);
						{
							float3 selfMin = brushVertices[0], selfMax = brushVertices[0];
							for (int v = 1; v < brushVertices.Length; v++)
							{
								var p = brushVertices[v];
								selfMin = math.min(selfMin, p);
								selfMax = math.max(selfMax, p);
							}
							selfMin -= CSGConstants.kEdgeIntersectionEpsilon;
							selfMax += CSGConstants.kEdgeIntersectionEpsilon;

							for (int i = 0; i < touched.Length; i++)
							{
								var otherOrder = touched[i].nodeIndexOrder.nodeOrder;
								if (otherOrder == brushNodeOrder ||
									otherOrder < 0 || otherOrder >= loopVerticesLookup.Length)
									continue;
								var ov = loopVerticesLookup[otherOrder];
								if (!ov.IsCreated) continue;
								for (int v = 0; v < ov.Length; v++)
								{
									var w = ov[v];
									if (math.any(w < selfMin) || math.any(w > selfMax))
										continue;
									neighbourCandidates.Add(w);
								}
							}
						}

						for (int surf = 0; surf < surfaceLoopIndices.Length && neighbourCandidates.Length > 0; surf++)
						{
							if (!surfaceLoopIndices[surf].IsCreated) continue;
							var loopIndices = surfaceLoopIndices[surf];

							var planeTree = math.mul(nodeToTreeInvTrans, baseSurfaces[surf].localPlane);
							var planeLen  = math.length(planeTree.xyz);
							if (planeLen > 1e-9f) planeTree /= planeLen;

							planeCandidates.Clear();
							for (int c = 0; c < neighbourCandidates.Length; c++)
							{
								var w = neighbourCandidates[c];
								var d = CSGMath.SignedDistance(planeTree, w);
								if (d < -kTJunctionPlaneBand || d > kTJunctionPlaneBand)
									continue;
								planeCandidates.Add(w);
							}
							dbgPlaneCand += planeCandidates.Length;
							if (planeCandidates.Length == 0) continue;

							for (int l = 0; l < loopIndices.Length; l++)
							{
								var loopIdx = loopIndices[l];
								var edges = surfaceLoopAllEdges[loopIdx];
								if (!edges.IsCreated || edges.Length < 3) continue;

								if (kLogTJunctionInsertions)
								{
									var sa = brushVertices[edges[0].index1];
									var sd = math.abs(CSGMath.SignedDistance(planeTree, sa));
									if (sd > dbgSelfPlaneMax) dbgSelfPlaneMax = sd;
									LogClosestApproach(in edges, in planeCandidates, brushVertices, ref dbgMinPerp);
								}

								float3 loopMin = brushVertices[edges[0].index1], loopMax = loopMin;
								for (int e = 0; e < edges.Length; e++)
								{
									var a = brushVertices[edges[e].index1];
									var b = brushVertices[edges[e].index2];
									loopMin = math.min(loopMin, math.min(a, b));
									loopMax = math.max(loopMax, math.max(a, b));
								}
								loopMin -= CSGConstants.kEdgeIntersectionEpsilon;
								loopMax += CSGConstants.kEdgeIntersectionEpsilon;

								bool reserved = false;
								candIdx.Clear();
								for (int c = 0; c < planeCandidates.Length; c++)
								{
									var w = planeCandidates[c];
									if (math.any(w < loopMin) || math.any(w > loopMax))
										continue;
									bool onAnEdge = false;
									for (int e = 0; e < edges.Length; e++)
									{
										var a = brushVertices[edges[e].index1];
										var b = brushVertices[edges[e].index2];
										if (MathExtensions.IsPointOnLineSegmentButNotOnVertex(w, a, b, CSGConstants.kSqrEdgeDistanceEpsilon))
										{ onAnEdge = true; break; }
									}
									if (!onAnEdge) continue;
									if (!reserved)
									{
										// Only reserve once we know this loop actually inserts something -
										// most loops insert nothing and would otherwise pay for the growth.
										brushVertices.ReserveAdditionalVertices(planeCandidates.Length - c);
										reserved = true;
									}
									var idx = brushVertices.AddNoResize(w, in weldFilter);
									candIdx.Add(idx);
									tjProtected.Add(idx);
								}
								if (candIdx.Length == 0) continue;
								dbgInserted += candIdx.Length;

								var posArr  = brushVertices.AsArray();
								var candArr = candIdx.AsArray();
								LoopEdgeSplitter.SplitEdgesAtVertices(ref edges, in posArr, in candArr, candArr.Length, CSGConstants.kSqrEdgeDistanceEpsilon);
								surfaceLoopAllEdges[loopIdx] = edges;
							}
						}

						if (tjProtected.Length > 0)
						{
							protectedVertices = new NativeArray<bool>(brushVertices.Length, Allocator.Temp);
							for (int i = 0; i < tjProtected.Length; i++)
							{
								var pi = tjProtected[i];
								if (pi >= 0 && pi < protectedVertices.Length)
									protectedVertices[pi] = true;
							}
						}

						if (kLogTJunctionInsertions)
							LogTJunctionInsertions(entityID, touched.Length, dbgWithVerts, dbgPlaneCand, dbgInserted,
												   dbgSelfPlaneMax, double.IsInfinity(dbgMinPerp) ? -1.0 : dbgMinPerp);

						tjProtected.Dispose();
						planeCandidates.Dispose();
						neighbourCandidates.Dispose();
						candIdx.Dispose();
					}

					// Decals: the ones that may reach each surface. Each gets a slot after the brush's own surfaces,
					// which stays empty when the decal draws nothing there.
					var baseSurfaceCount = surfaceLoopIndices.Length;
					NativeList<int> decalCandidates;
					using var _decalCandidates = decalCandidates = new NativeList<int>(16, Allocator.Temp);
					NativeList<byte> decalTargeted;
					using var _decalTargeted = decalTargeted = new NativeList<byte>(16, Allocator.Temp);
					NativeArray<int> decalCandidateStart;
					using var _decalCandidateStart = decalCandidateStart = new NativeArray<int>(baseSurfaceCount + 1, Allocator.Temp);
					BoundsOf(brushVertices.AsArray(), out var brushMin, out var brushMax);
					FindDecalCandidates(ref baseSurfaces, nodeToTreeInvTrans, brushVertices.Length > 0, brushMin, brushMax, entityID, decalCandidates, decalTargeted, decalCandidateStart);
					if (decalCandidates.Length > 0)
						decalBuilder = DecalSurfaceBuilder.Create(Allocator.Temp);

					// Scratch allocators
					Vertex2DRemapper vertex2DRemapper;
					using var _vertex2DRemapper = vertex2DRemapper = new()
					{
						lookup = new NativeList<int>(64, Allocator.Temp),
						positions2D = new NativeList<double2>(64, Allocator.Temp),
						edgeIndices = new NativeList<int>(128, Allocator.Temp),
						collapseDegree     = Vertex2DRemapper.AllocateCollapseScratch(brushVertices.Length),
						collapseNeighbourA = Vertex2DRemapper.AllocateCollapseScratch(brushVertices.Length),
						collapseNeighbourB = Vertex2DRemapper.AllocateCollapseScratch(brushVertices.Length)
					};

					UniqueVertexMapper uniqueVertexMapper;
					using var _uniqueVertexMapper = uniqueVertexMapper = new()
					{
						indexRemap = new NativeArray<int>(brushVertices.Length, Allocator.Temp),
						surfaceColliderVertices = new NativeList<float3>(brushVertices.Length, Allocator.Temp),
						surfaceSelectVertices = new NativeList<SelectVertex>(brushVertices.Length, Allocator.Temp),
						surfaceRenderVertices = new NativeList<RenderVertex>(brushVertices.Length, Allocator.Temp)
					};

					Args settings = Args.Default(
							autoHolesAndBoundary: true,
							concentricShellsParameter: 0.001f,
							preprocessor: Preprocessor.None,
							refineMesh: false,
							restoreBoundary: true,
							sloanMaxIters: 1_000_000,
							validateInput: true,
							verbose: false,			// we report failures ourselves via LogSurfaceDrop; verbose:true double-logs every failure as a red error
							refinementThresholdAngle: math.radians(5),
							refinementThresholdArea: 1f
						);

					NativeList<int> surfaceIndexList;
					using var _surfaceIndexList = surfaceIndexList = new NativeList<int>(maxIndices, Allocator.Temp);

					NativeList<int> loops;
					using var _loops = loops = new NativeList<int>(maxLoops, Allocator.Temp);

					NativeList<int> triangles;
					using var _triangles = triangles = new NativeList<int>(64, Allocator.Temp);

					NativeReference<Status> status;
					using var _status = status = new NativeReference<Status>(Allocator.Temp);

					var output = new andywiecko.BurstTriangulator.LowLevel.Unsafe.NativeOutputData<double2>()
					{
						Triangles = triangles,
						Status = status
					};

					using var builder = new BlobBuilder(Allocator.Temp, 4096);

					ref var root = ref builder.ConstructRoot<ChiselBrushRenderBuffer>();
					var surfaceBuffers = builder.Allocate(ref root.surfaces, baseSurfaceCount + decalCandidates.Length);

					var triangulator = new UnsafeTriangulator<double2>();
					for (int surf = 0; surf < surfaceLoopIndices.Length; surf++)
					{
						if (!surfaceLoopIndices[surf].IsCreated) continue;
						loops.Clear(); uniqueVertexMapper.Reset();

						// Collect valid loops
						var loopIndices = surfaceLoopIndices[surf];
						for (int l = 0; l < loopIndices.Length; l++)
						{
							var loopIdx = loopIndices[l];
							var edges = surfaceLoopAllEdges[loopIdx];
							if (edges.Length < 3) continue;
							loops.AddNoResize(loopIdx);
						}
						if (loops.Length == 0)
						{
							LogSurfaceDrop(entityID, surf, -1, 0, 0, SurfaceDropReason.SurfaceHasNoLoops, Status.OK);
							continue;
						}

						// We need to convert our UV matrix from tree-space, to brush local-space, to plane-space
						// since the vertices of the polygons, at this point, are in tree-space.
						var plane = baseSurfaces[surf].localPlane;
						var localToPlane = MathExtensions.GenerateLocalToPlaneSpaceMatrix(plane);
						var treeToPlane = math.mul(localToPlane, treeToNode);
						var planeNormalMap = math.mul(nodeToTreeInvTrans, plane);
						var map3DTo2D = new Map3DTo2D(planeNormalMap.xyz);

						// Normal flip logic preparation
						float3 finalFaceNormal = map3DTo2D.normal;
						if (subtractiveWorkflow)
						{
							// Flip the normal direction so lighting is correct for the "inside"
							finalFaceNormal = -finalFaceNormal;
						}

						surfaceIndexList.Clear();
						for (int li = 0; li < loops.Length; li++)
						{
							var loopIdx = loops[li];
							var edges = surfaceLoopAllEdges[loopIdx];
							var info = surfaceLoopAllInfos[loopIdx];
							Debug.Assert(surf == info.basePlaneIndex, "surfaceIndex != loopInfo.basePlaneIndex");

							if (kLogBoundaryEdges)
								LogBoundaryEdges(entityID, surf, loopIdx, in edges, brushVertices.m_Vertices->Ptr);

							vertex2DRemapper.ConvertToPlaneSpace(*brushVertices.m_Vertices, edges, map3DTo2D, protectedVertices);

							if (kCloseOpenChains2D)
								vertex2DRemapper.RepairBoundary();

							if (vertex2DRemapper.CheckForSelfIntersections())
							{
#if UNITY_EDITOR && DEBUG
								if (kLogTriangulationIssues)
								{
									SelfIntersectionDetected(surf, loopIdx);
								}
#endif
								vertex2DRemapper.RemoveSelfIntersectingEdges();
							}

							var roVerts = vertex2DRemapper.AsReadOnly();

							// Pre-check: need enough points and edges
							if (roVerts.positions2D.Length < 3 || roVerts.edgeIndices.Length < 3)
							{
								LogSurfaceDrop(entityID, surf, loopIdx, roVerts.positions2D.Length, roVerts.edgeIndices.Length, SurfaceDropReason.TooFewPointsAfterDedup, Status.OK);
								continue;
							}

							// Check for degenerate edges
							if (IsDegenerate(roVerts.positions2D))
							{
								LogSurfaceDrop(entityID, surf, loopIdx, roVerts.positions2D.Length, roVerts.edgeIndices.Length, SurfaceDropReason.DegenerateInput, Status.OK);
								continue;
							}

							try
							{
								output.Triangles.Clear();
								triangulator.Triangulate(
									new andywiecko.BurstTriangulator.LowLevel.Unsafe.NativeInputData<double2>
									{
										Positions = roVerts.positions2D,
										ConstraintEdges = roVerts.edgeIndices
									},
									output,
									settings,
									Allocator.Temp);

								// Report (editor, while Burst is off) exactly why a surface produced no geometry.
								if (output.Status.Value != Status.OK)
								{
									LogSurfaceDrop(entityID, surf, loopIdx, roVerts.positions2D.Length, roVerts.edgeIndices.Length, SurfaceDropReason.TriangulatorError, output.Status.Value);
										LogDropDetail(entityID, surf, loopIdx, SurfaceDropReason.TriangulatorError, roVerts.positions2D, roVerts.edgeIndices);
									continue;
								}
								if (output.Triangles.Length == 0)
								{
									bool recovered = false;
									if (kCleanupNonSimpleLoops)
									{
										var cleaned = new UnsafeList<Edge>(edges.Length, Allocator.Temp);
										cleaned.AddRange(edges);
										LoopEdgeSplitter.RemoveAntiparallelEdgePairs(ref cleaned);
										if (cleaned.Length != edges.Length && cleaned.Length >= 3)
										{
											vertex2DRemapper.ConvertToPlaneSpace(*brushVertices.m_Vertices, cleaned, map3DTo2D, protectedVertices);
											if (kCloseOpenChains2D)
												vertex2DRemapper.RepairBoundary();
											if (vertex2DRemapper.CheckForSelfIntersections())
												vertex2DRemapper.RemoveSelfIntersectingEdges();
											roVerts = vertex2DRemapper.AsReadOnly();
											if (roVerts.positions2D.Length >= 3 && roVerts.edgeIndices.Length >= 3 && !IsDegenerate(roVerts.positions2D))
											{
												output.Triangles.Clear();
												triangulator.Triangulate(
													new andywiecko.BurstTriangulator.LowLevel.Unsafe.NativeInputData<double2>
													{
														Positions = roVerts.positions2D,
														ConstraintEdges = roVerts.edgeIndices
													},
													output, settings, Allocator.Temp);
												recovered = output.Status.Value == Status.OK && output.Triangles.Length > 0;
											}
										}
										cleaned.Dispose();
									}
									if (!recovered)
									{
										LogSurfaceDrop(entityID, surf, loopIdx, roVerts.positions2D.Length, roVerts.edgeIndices.Length, SurfaceDropReason.ZeroTrianglesReturned, Status.OK);
											LogDropDetail(entityID, surf, loopIdx, SurfaceDropReason.ZeroTrianglesReturned, roVerts.positions2D, roVerts.edgeIndices);
										continue;
									}
								}

								// Winding order flip for subtractive
								if (subtractiveWorkflow)
								{
									// Flip winding order (0,1,2 -> 0,2,1) to face inwards
									for (int ti = 0; ti < output.Triangles.Length; ti += 3)
									{
										(output.Triangles[ti + 1], output.Triangles[ti + 2]) = 
											(output.Triangles[ti + 2], output.Triangles[ti + 1]);
									}
								}

								// Map triangles back
								var prevCount = surfaceIndexList.Length;
								var interiorCat = (CategoryIndex)info.interiorCategory;
								roVerts.RemapTriangles(interiorCat, output.Triangles, surfaceIndexList);

								// Register vertices (Pass calculated/flipped normal)
								uniqueVertexMapper.RegisterVertices(
									surfaceIndexList,
									prevCount,
									*brushVertices.m_Vertices,
									finalFaceNormal, 
									entityID,
									interiorCat);
							}
							catch (System.Exception ex) { Debug.LogException(ex); }
						}

						if (surfaceIndexList.Length == 0) continue;
						FinishSurface(surf, brushNodeOrder, ref baseSurfaces, treeToPlane, finalFaceNormal, surfaceIndexList, ref uniqueVertexMapper,
						              builder, surfaceBuffers, ref decalBuilder, decalCandidates, decalTargeted, decalCandidateStart, baseSurfaceCount);
					}

					FinishBrush(brushIndexOrder, brushNodeOrder, builder, ref root, surfaceBuffers, decalCandidateStart, baseSurfaceCount);
				}
				finally
				{
					if (protectedVertices.IsCreated)
						protectedVertices.Dispose();
					decalBuilder.Dispose();

					if (surfaceLoopAllEdges.IsCreated)
					{
						for (int i = 0; i < surfaceLoopAllEdges.Length; i++)
						{
							if (surfaceLoopAllEdges[i].IsCreated)
								surfaceLoopAllEdges[i].Dispose();
							surfaceLoopAllEdges[i] = default;
						}
					}
				}
			}
			finally
			{
				if (surfaceLoopIndices.IsCreated)
				{
					for (int i = 0; i < surfaceLoopIndices.Length; i++)
					{
						if (surfaceLoopIndices[i].IsCreated)
							surfaceLoopIndices[i].Dispose();
						surfaceLoopIndices[i] = default;
					}
				}
			}
		}
		// After a surface's triangles have been registered: normal smoothing, UVs, tangents, lightmap coordinates, the decals on
		// it and its render buffer. Shared by both inputs.
		unsafe void FinishSurface(int surf, int brushNodeOrder, ref BlobArray<BaseSurface> baseSurfaces, float4x4 treeToPlane, float3 finalFaceNormal,
						   NativeList<int> surfaceIndexList, ref UniqueVertexMapper uniqueVertexMapper,
						   BlobBuilder builder, BlobBuilderArray<ChiselSurfaceRenderBuffer> surfaceBuffers,
						   ref DecalSurfaceBuilder decalBuilder, NativeList<int> decalCandidates, NativeList<byte> decalTargeted,
						   NativeArray<int> decalCandidateStart, int baseSurfaceCount)
		{
			if (normalSmoothingAngle > 0.0001f)
			{
				var renderVertices = uniqueVertexMapper.surfaceRenderVertices;
				var positions      = uniqueVertexMapper.surfaceColliderVertices;
				float smoothingCos = math.cos(math.radians(normalSmoothingAngle));

				var alignedPlanes = new NativeList<float4>(64, Allocator.Temp);
				for (int otherBrushIdx = 0; otherBrushIdx < basePolygonCache.Length; otherBrushIdx++)
				{
					if (!basePolygonCache[otherBrushIdx].IsCreated) continue;
					ref var otherSurfaces = ref basePolygonCache[otherBrushIdx].Value.surfaces;
					// Hoisted out of the vertex loop - this only varies per brush.
					var otherNodeToTreeInvTrans = math.transpose(transformationCache[otherBrushIdx].treeToNode);

					for (int otherSurf = 0; otherSurf < otherSurfaces.Length; otherSurf++)
					{
						// Skip ourselves (same brush, same surface)
						if (otherBrushIdx == brushNodeOrder && otherSurf == surf) continue;

						float4 otherPlaneTree = math.mul(otherNodeToTreeInvTrans, otherSurfaces[otherSurf].localPlane);

						// If subtractive workflow, we must flip the neighbour normal effectively
						// to compare "Inwards vs Inwards" rather than "Inwards vs Outwards"
						float3 comparisonNormal = subtractiveWorkflow ? -otherPlaneTree.xyz : otherPlaneTree.xyz;
						if (math.dot(finalFaceNormal, comparisonNormal) < smoothingCos)
							continue;

						// Kept RAW (normal + D as cached): the per-vertex test below needs the
						// raw plane for the geometric distance, and negates it when accumulating.
						alignedPlanes.Add(otherPlaneTree);
					}
				}

				for (int v = 0; v < renderVertices.Length; v++)
				{
					float3 vertPos        = positions[v];
					float3 smoothedNormal = finalFaceNormal;

					for (int a = 0; a < alignedPlanes.Length; a++)
					{
						var otherPlaneTree = alignedPlanes[a];
						// distance = dot(N_raw, P) + D_raw
						float dist = math.dot(otherPlaneTree.xyz, vertPos) + otherPlaneTree.w;
						if (math.abs(dist) < 0.005f)
							smoothedNormal += subtractiveWorkflow ? -otherPlaneTree.xyz : otherPlaneTree.xyz;
					}

					var rv = renderVertices[v];
					rv.normal = math.normalizesafe(smoothedNormal, finalFaceNormal);
					renderVertices[v] = rv;
				}

				alignedPlanes.Dispose();
			}

			var flags = baseSurfaces[surf].destinationFlags;
			var outputFlags = baseSurfaces[surf].outputFlags;
			var parms = baseSurfaces[surf].destinationParameters;
			var UV0 = baseSurfaces[surf].UV0;
			var uvMat = math.mul(UV0.ToFloat4x4(), treeToPlane);
			
			MeshAlgorithms.ComputeUVs(uniqueVertexMapper.surfaceRenderVertices, uvMat);
			MeshAlgorithms.ComputeTangents(uniqueVertexMapper.surfaceRenderVertices, finalFaceNormal, uvMat);
			MeshAlgorithms.ComputeLightmapCoordinates(uniqueVertexMapper.surfaceRenderVertices, finalFaceNormal);

			// Decals on this surface, each in its own slot
			var candidateStart = decalCandidateStart[surf];
			var candidateCount = decalCandidateStart[surf + 1] - candidateStart;
			var decalCut = false;
			if (candidateCount > 0)
			{
				var candidates = new UnsafeList<int>(decalCandidates.GetUnsafeReadOnlyPtr() + candidateStart, candidateCount);
				var targeted   = new UnsafeList<byte>(decalTargeted.GetUnsafeReadOnlyPtr() + candidateStart, candidateCount);
				decalCut = decalBuilder.Build(surfaceIndexList, uniqueVertexMapper.surfaceRenderVertices,
											  uniqueVertexMapper.surfaceSelectVertices, decalVolumes, candidates, targeted, flags);
				for (int k = 0; k < candidateCount; k++)
				{
					var decalOutput = decalBuilder.decals[k];
					if (decalOutput.IsEmpty)
						continue;
					var volume     = decalVolumes[decalCandidates[candidateStart + k]];
					var decalParms = parms;
					decalParms.parameter1 = volume.renderMaterial;
					if (volume.IsTransparent)
						decalParms.parameter2 = 0;
					MeshAlgorithms.ComputeTangents(decalOutput.indices, decalOutput.renderVertices);
					MeshAlgorithms.ComputeLightmapCoordinates(decalOutput.renderVertices, finalFaceNormal);
					var slot = baseSurfaceCount + candidateStart + k;
					surfaceBuffers[slot].Construct(builder, decalOutput.indices, decalOutput.colliderVertices,
												   decalOutput.selectVertices, decalOutput.renderVertices,
												   slot, surf, volume.GetDestinationFlags(flags), decalParms, outputFlags);
					surfaceBuffers[slot].decalEntityID = volume.entityID;
				}
			}

			ref var buf = ref surfaceBuffers[surf];
			if (decalCut)
			{
				// An opaque decal took part of this surface: the rest of it is drawn instead
				var remaining = decalBuilder.surface;
				MeshAlgorithms.ComputeUVs(remaining.renderVertices, uvMat);
				MeshAlgorithms.ComputeTangents(remaining.renderVertices, finalFaceNormal, uvMat);
				MeshAlgorithms.ComputeLightmapCoordinates(remaining.renderVertices, finalFaceNormal);
				buf.Construct(builder, remaining.indices, remaining.colliderVertices,
							  remaining.selectVertices, remaining.renderVertices,
							  surf, surf, flags, parms, outputFlags);
			} else
			buf.Construct(builder, surfaceIndexList,
						uniqueVertexMapper.surfaceColliderVertices,
						uniqueVertexMapper.surfaceSelectVertices,
						uniqueVertexMapper.surfaceRenderVertices,
						surf, surf, flags, parms, outputFlags);
		}

		// After every surface: the empty decal slots, the mesh queries, and the brush's render buffer in the cache.
		void FinishBrush(IndexOrder brushIndexOrder, int brushNodeOrder, BlobBuilder builder, ref ChiselBrushRenderBuffer root,
						 BlobBuilderArray<ChiselSurfaceRenderBuffer> surfaceBuffers, NativeArray<int> decalCandidateStart, int baseSurfaceCount)
		{
			// Decal slots nothing was drawn in still say which surface they belong to
			for (int s = 0; s < baseSurfaceCount; s++)
			{
				for (int c = decalCandidateStart[s]; c < decalCandidateStart[s + 1]; c++)
				{
					ref var emptySlot = ref surfaceBuffers[baseSurfaceCount + c];
					if (emptySlot.vertexCount != 0)
						continue;
					emptySlot.surfaceIndex     = baseSurfaceCount + c;
					emptySlot.baseSurfaceIndex = s;
				}
			}

			using var queryList = new NativeList<ChiselQuerySurface>(surfaceBuffers.Length, Allocator.Temp);
			var queryArr = builder.Allocate(ref root.querySurfaces, meshQueries.Length);
			for (int t = 0; t < meshQueries.Length; t++)
			{
				var meshQuery = meshQueries[t];
				var layerQueryMask = meshQuery.LayerQueryMask;
				var layerQuery = meshQuery.LayerQuery;
				var surfaceParameterIndex = (meshQuery.LayerParameterIndex >= SurfaceParameterIndex.Parameter1 &&
												meshQuery.LayerParameterIndex <= SurfaceParameterIndex.MaxParameterIndex) ?
												(int)meshQuery.LayerParameterIndex - 1 : -1;

				queryList.Clear();
				for (int s = 0; s < surfaceBuffers.Length; s++)
				{
					ref var buffer = ref surfaceBuffers[s];
					// A slot that has no triangles is left out: a surface that was skipped above was never
					// constructed, so its surfaceIndex is 0 and would make its entry read surface 0.
					if (buffer.vertexCount == 0 || buffer.indexCount == 0)
						continue;
					var destinationFlags = buffer.destinationFlags;
					if ((destinationFlags & layerQueryMask) != layerQuery)
						continue;

					queryList.AddNoResize(new ChiselQuerySurface
					{
						surfaceIndex      = s,
						surfaceParameter  = surfaceParameterIndex < 0 ? 0 : buffer.destinationParameters.parameters[surfaceParameterIndex],
						vertexCount       = buffer.vertexCount,
						indexCount        = buffer.indexCount,
						surfaceHashValue  = buffer.surfaceHashValue,
						geometryHashValue = buffer.geometryHashValue
					});
				}
				queryList.Sort(kCompareSortByBasePlaneIndex);

				builder.Construct(ref queryArr[t].surfaces, queryList);
				queryArr[t].brushNodeID = brushIndexOrder.compactNodeID;
			}


			root.surfaceOffset = 0;
			root.surfaceCount = surfaceBuffers.Length;

			var brushRenderBuffer = builder.CreateBlobAssetReference<ChiselBrushRenderBuffer>(Allocator.Persistent);
			if (brushRenderBufferCache[brushNodeOrder].IsCreated)
			{
				brushRenderBufferCache[brushNodeOrder].Dispose();
				brushRenderBufferCache[brushNodeOrder] = default;
			}
			brushRenderBufferCache[brushNodeOrder] = brushRenderBuffer;
		}

		// The frame a surface is textured in, and the normal its vertices get.
		void SurfaceFrame(ref BlobArray<BaseSurface> baseSurfaces, int surf, float4x4 treeToNode, float4x4 nodeToTreeInvTrans,
						  out float4x4 treeToPlane, out float3 finalFaceNormal)
		{
			var plane = baseSurfaces[surf].localPlane;
			var localToPlane = MathExtensions.GenerateLocalToPlaneSpaceMatrix(plane);
			treeToPlane = math.mul(localToPlane, treeToNode);
			var planeNormalMap = math.mul(nodeToTreeInvTrans, plane);
			var map3DTo2D = new Map3DTo2D(planeNormalMap.xyz);
			finalFaceNormal = map3DTo2D.normal;
			if (subtractiveWorkflow)
				finalFaceNormal = -finalFaceNormal;   // lighting for the "inside"
		}

		unsafe void ExecuteExact(int index)
		{
			var count = input.BeginForEachIndex(index);
			if (count == 0)
				return;
			var brushIndexOrder = input.Read<IndexOrder>();
			var brushNodeOrder  = brushIndexOrder.nodeOrder;
			var surfaceCount    = input.Read<int>();

			// All parts of all surfaces: vertices concatenated, indices already offset into them.
			var vertices = new UnsafeList<float3>(64, Allocator.Temp);
			NativeList<int> partIndices;
			using var _partIndices = partIndices = new NativeList<int>(256, Allocator.Temp);
			NativeList<int4> parts;     // (surface, category, first index, index count)
			using var _parts = parts = new NativeList<int4>(surfaceCount * 2, Allocator.Temp);
			DecalSurfaceBuilder decalBuilder = default;
			try
			{
				for (int s = 0; s < surfaceCount; s++)
				{
					var partCount = input.Read<int>();
					for (int p = 0; p < partCount; p++)
					{
						var category     = input.Read<int>();
						var vertexCount  = input.Read<int>();
						var vertexOffset = vertices.Length;
						for (int v = 0; v < vertexCount; v++)
							vertices.Add(input.Read<float3>());
						var indexCount = input.Read<int>();
						var first      = partIndices.Length;
						for (int i = 0; i < indexCount; i++)
							partIndices.Add(vertexOffset + input.Read<int>());
						parts.Add(new int4(s, category, first, indexCount));
					}
				}
				input.EndForEachIndex();

				if (!basePolygonCache[brushNodeOrder].IsCreated)
					return;

				ulong entityID = entityIDLookup.SafeGetNodeEntityID(brushIndexOrder.compactNodeID);
				ref var baseSurfaces = ref basePolygonCache[brushNodeOrder].Value.surfaces;
				var transform = transformationCache[brushNodeOrder];
				var treeToNode = transform.treeToNode;
				var nodeToTreeInvTrans = math.transpose(treeToNode);
				var baseSurfaceCount = math.min(surfaceCount, baseSurfaces.Length);

				NativeList<int> decalCandidates;
				using var _decalCandidates = decalCandidates = new NativeList<int>(16, Allocator.Temp);
				NativeList<byte> decalTargeted;
				using var _decalTargeted = decalTargeted = new NativeList<byte>(16, Allocator.Temp);
				NativeArray<int> decalCandidateStart;
				using var _decalCandidateStart = decalCandidateStart = new NativeArray<int>(baseSurfaceCount + 1, Allocator.Temp);
				float3 brushMin = float3.zero, brushMax = float3.zero;
				if (vertices.Length > 0)
				{
					brushMin = brushMax = vertices[0];
					for (int v = 1; v < vertices.Length; v++)
					{
						brushMin = math.min(brushMin, vertices[v]);
						brushMax = math.max(brushMax, vertices[v]);
					}
				}
				FindDecalCandidates(ref baseSurfaces, nodeToTreeInvTrans, vertices.Length > 0, brushMin, brushMax, entityID, decalCandidates, decalTargeted, decalCandidateStart);
				if (decalCandidates.Length > 0)
					decalBuilder = DecalSurfaceBuilder.Create(Allocator.Temp);

				UniqueVertexMapper uniqueVertexMapper;
				using var _uniqueVertexMapper = uniqueVertexMapper = new()
				{
					indexRemap = new NativeArray<int>(math.max(1, vertices.Length), Allocator.Temp),
					surfaceColliderVertices = new NativeList<float3>(vertices.Length, Allocator.Temp),
					surfaceSelectVertices = new NativeList<SelectVertex>(vertices.Length, Allocator.Temp),
					surfaceRenderVertices = new NativeList<RenderVertex>(vertices.Length, Allocator.Temp)
				};

				NativeList<int> surfaceIndexList;
				using var _surfaceIndexList = surfaceIndexList = new NativeList<int>(partIndices.Length, Allocator.Temp);

				using var builder = new BlobBuilder(Allocator.Temp, 4096);
				ref var root = ref builder.ConstructRoot<ChiselBrushRenderBuffer>();
				var surfaceBuffers = builder.Allocate(ref root.surfaces, baseSurfaceCount + decalCandidates.Length);

				for (int surf = 0; surf < baseSurfaceCount; surf++)
				{
					uniqueVertexMapper.Reset();
					surfaceIndexList.Clear();
					SurfaceFrame(ref baseSurfaces, surf, treeToNode, nodeToTreeInvTrans, out var treeToPlane, out var finalFaceNormal);
					for (int p = 0; p < parts.Length; p++)
					{
						var part = parts[p];
						if (part.x != surf || part.w < 3)
							continue;
						var interiorCat = (CategoryIndex)part.y;
						bool flip = (interiorCat == CategoryIndex.ValidAligned) == subtractiveWorkflow;
						var prevCount = surfaceIndexList.Length;
						for (int t = part.z; t + 2 < part.z + part.w; t += 3)
						{
							surfaceIndexList.Add(partIndices[t]);
							surfaceIndexList.Add(flip ? partIndices[t + 2] : partIndices[t + 1]);
							surfaceIndexList.Add(flip ? partIndices[t + 1] : partIndices[t + 2]);
						}
						uniqueVertexMapper.RegisterVertices(surfaceIndexList, prevCount, vertices, finalFaceNormal, entityID, interiorCat);
					}
					if (surfaceIndexList.Length == 0)
						continue;
					FinishSurface(surf, brushNodeOrder, ref baseSurfaces, treeToPlane, finalFaceNormal, surfaceIndexList, ref uniqueVertexMapper,
								  builder, surfaceBuffers, ref decalBuilder, decalCandidates, decalTargeted, decalCandidateStart, baseSurfaceCount);
				}
				FinishBrush(brushIndexOrder, brushNodeOrder, builder, ref root, surfaceBuffers, decalCandidateStart, baseSurfaceCount);
			}
			finally
			{
				decalBuilder.Dispose();
				vertices.Dispose();
			}
		}

		static void BoundsOf(NativeArray<float3> vertices, out float3 min, out float3 max)
		{
			min = max = float3.zero;
			if (vertices.Length == 0)
				return;
			min = max = vertices[0];
			for (int v = 1; v < vertices.Length; v++)
			{
				min = math.min(min, vertices[v]);
				max = math.max(max, vertices[v]);
			}
		}

		unsafe void FindDecalCandidates(ref BlobArray<BaseSurface> baseSurfaces, float4x4 nodeToTreeInvTrans, bool hasVertices, float3 brushMin, float3 brushMax,
										ulong brushEntityID, NativeList<int> candidates, NativeList<byte> targeted, NativeArray<int> start)
		{
			candidates.Clear();
			targeted.Clear();
			for (int s = 0; s < start.Length; s++)
				start[s] = 0;
			if (!decalVolumes.IsCreated || decalVolumes.Length == 0 || !hasVertices)
				return;

			var volumes = (DecalVolume*)decalVolumes.GetUnsafeReadOnlyPtr();

			// The decals near the brush, once for all of its surfaces (a map has thousands of decals)
			using var nearBrush = new NativeList<int>(math.min(decalVolumes.Length, 64), Allocator.Temp);
			for (int d = 0; d < decalVolumes.Length; d++)
			{
				if (volumes[d].Overlaps(brushMin, brushMax))
					nearBrush.Add(d);
			}
			if (nearBrush.Length == 0)
				return;

			var surfaceCount = start.Length - 1;
			for (int s = 0; s < surfaceCount; s++)
			{
				start[s] = candidates.Length;
				if (s >= baseSurfaces.Length ||
					(baseSurfaces[s].destinationFlags & SurfaceDestinationFlags.Renderable) == 0)
					continue;
				var plane  = (double4)math.mul(nodeToTreeInvTrans, baseSurfaces[s].localPlane);
				var length = math.length(plane.xyz);
				if (!(length > 0))
					continue;
				plane /= length;
				for (int i = 0; i < nearBrush.Length; i++)
				{
					var d = nearBrush[i];
					ref var volume = ref volumes[d];
					// Does the surface's plane pass through the decal's bounds?
					var center   = ((double3)volume.boundsMin + (double3)volume.boundsMax) * 0.5;
					var extents  = ((double3)volume.boundsMax - (double3)volume.boundsMin) * 0.5;
					var distance = math.dot(plane.xyz, center) + plane.w;
					var reach    = math.dot(math.abs(plane.xyz), extents);
					if (math.abs(distance) > reach + DecalVolume.kPlaneEpsilon)
						continue;
					var isTargeted = volume.Targets(decalTargets, brushEntityID, baseSurfaces[s].descriptionIndex);
					if (!isTargeted && volume.IsTransparent)
						continue;
					candidates.Add(d);
					targeted.Add(isTargeted ? (byte)1 : (byte)0);
				}
			}
			start[surfaceCount] = candidates.Length;
		}

		static bool IsDegenerate(NativeArray<double2> verts)
		{
			if (verts.Length < 3)
				return true;

			double2 min = verts[0];
			double2 max = verts[0];

			for (int i = 1; i < verts.Length; i++)
			{
				var v = verts[i];
				min = math.min(min, v);
				max = math.max(max, v);
			}

			// A zero-area axis-aligned box means every point sits on a line
			return math.abs(max.x - min.x) <= double.Epsilon ||
				   math.abs(max.y - min.y) <= double.Epsilon;
		}
	}
}
