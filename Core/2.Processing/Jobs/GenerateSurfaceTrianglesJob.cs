using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using Debug = UnityEngine.Debug;
using ReadOnlyAttribute = Unity.Collections.ReadOnlyAttribute;
using WriteOnlyAttribute = Unity.Collections.WriteOnlyAttribute;
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

		[NoAlias, ReadOnly] public NativeList<BlobAssetReference<BasePolygonsBlob>> basePolygonCache;
		[NoAlias, ReadOnly] public NativeList<NodeTransformations> transformationCache;
		[NoAlias, ReadOnly] public NativeStream.Reader input;
		[NoAlias, ReadOnly] public NativeArray<MeshQuery> meshQueries;
		[NoAlias, ReadOnly] public CompactHierarchyManagerInstance.ReadOnlyInstanceIDLookup instanceIDLookup;

		// Write
		[NativeDisableParallelForRestriction]
		[NoAlias] public NativeList<BlobAssetReference<ChiselBrushRenderBuffer>> brushRenderBufferCache;

		[BurstDiscard]
		public static void InvalidFinalCategory(CategoryIndex _interiorCategory)
		{
			Debug.Assert(false, $"Invalid final category {_interiorCategory}");
		}

		struct CompareSortByBasePlaneIndex : System.Collections.Generic.IComparer<ChiselQuerySurface>
		{
			public readonly int Compare(ChiselQuerySurface x, ChiselQuerySurface y)
			{
				var diff = x.surfaceParameter - y.surfaceParameter;
				if (diff != 0)
					return diff;
				return x.surfaceIndex - y.surfaceIndex;
			}
		}
		readonly static CompareSortByBasePlaneIndex kCompareSortByBasePlaneIndex = new();


		public unsafe void Execute(int index)
		{
			var count = input.BeginForEachIndex(index);
			if (count == 0)
				return;

			// Read brush data
			var brushIndexOrder = input.Read<IndexOrder>();
			var brushNodeOrder = brushIndexOrder.nodeOrder;
			var vertexCount = input.Read<int>();

			// Pre-check: need at least 3 vertices
			if (vertexCount < 3)
			{
				input.EndForEachIndex();
				return;
			}

			HashedVertices brushVertices;
			using var _brushVertices = brushVertices = new HashedVertices(vertexCount, Allocator.Temp);
			for (int v = 0; v < vertexCount; v++)
			{
				var vertex = input.Read<float3>();
				brushVertices.AddNoResize(vertex);
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

					int instanceID = instanceIDLookup.SafeGetNodeInstanceID(brushIndexOrder.compactNodeID);

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

					// Scratch allocators
					Vertex2DRemapper vertex2DRemapper;
					using var _vertex2DRemapper = vertex2DRemapper = new()
					{
						lookup = new NativeList<int>(64, Allocator.Temp),
						positions2D = new NativeList<double2>(64, Allocator.Temp),
						edgeIndices = new NativeList<int>(128, Allocator.Temp)
					};

					UniqueVertexMapper uniqueVertexMapper;
					using var _uniqueVertexMapper = uniqueVertexMapper = new()
					{
						indexRemap = new NativeArray<int>(brushVertices.Length, Allocator.Temp),
						surfaceColliderVertices = new NativeList<float3>(brushVertices.Length, Allocator.Temp),
						surfaceSelectVertices = new NativeList<SelectVertex>(brushVertices.Length, Allocator.Temp),
						surfaceRenderVertices = new NativeList<RenderVertex>(brushVertices.Length, Allocator.Temp)
					};

					Args settings = new(
							autoHolesAndBoundary: true,
							concentricShellsParameter: 0.001f,
							preprocessor: Preprocessor.None,
							refineMesh: false,
							restoreBoundary: true,
							sloanMaxIters: 1_000_000,
							validateInput: true,
							verbose: true,
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

					var output = new andywiecko.BurstTriangulator.LowLevel.Unsafe.OutputData<double2>()
					{
						Triangles = triangles,
						Status = status
					};

					using var builder = new BlobBuilder(Allocator.Temp, 4096);

					ref var root = ref builder.ConstructRoot<ChiselBrushRenderBuffer>();
					var surfaceBuffers = builder.Allocate(ref root.surfaces, surfaceLoopIndices.Length);

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
						if (loops.Length == 0) continue;

						// We need to convert our UV matrix from tree-space, to brush local-space, to plane-space
						// since the vertices of the polygons, at this point, are in tree-space.
						var plane = baseSurfaces[surf].localPlane;
						var localToPlane = MathExtensions.GenerateLocalToPlaneSpaceMatrix(plane);
						var treeToPlane = math.mul(localToPlane, treeToNode);
						var planeNormalMap = math.mul(nodeToTreeInvTrans, plane);
						var map3DTo2D = new Map3DTo2D(planeNormalMap.xyz);

						surfaceIndexList.Clear();
						for (int li = 0; li < loops.Length; li++)
						{
							var loopIdx = loops[li];
							var edges = surfaceLoopAllEdges[loopIdx];
							var info = surfaceLoopAllInfos[loopIdx];
							Debug.Assert(surf == info.basePlaneIndex, "surfaceIndex != loopInfo.basePlaneIndex");

							// Convert 3D -> 2D
							vertex2DRemapper.ConvertToPlaneSpace(*brushVertices.m_Vertices, edges, map3DTo2D);
							vertex2DRemapper.RemoveDuplicates();

							if (vertex2DRemapper.CheckForSelfIntersections())
							{
#if UNITY_EDITOR && DEBUG
								Debug.LogWarning($"Self-intersection detected in surface {surf}, loop index {loopIdx}.");
#endif
								vertex2DRemapper.RemoveSelfIntersectingEdges();
							}

							var roVerts = vertex2DRemapper.AsReadOnly();

							// Pre-check: need enough points and edges
							if (roVerts.positions2D.Length < 3 || roVerts.edgeIndices.Length < 3)
								continue;

							// Check for degenerate edges
							if (IsDegenerate(roVerts.positions2D))
								continue;

							try
							{
								output.Triangles.Clear();
								triangulator.Triangulate(
									new andywiecko.BurstTriangulator.LowLevel.Unsafe.InputData<double2>
									{
										Positions = roVerts.positions2D,
										ConstraintEdges = roVerts.edgeIndices
									},
									output,
									settings,
									Allocator.Temp);
#if UNITY_EDITOR && DEBUG
								// Inside the loop after calling Triangulate:
								if (output.Status.Value != Status.OK)
								{
									// Log the specific status enum value/name for more detail!
									Debug.LogError($"Triangulator failed for surface {surf}, loop index {loopIdx} with status {output.Status.Value.ToString()} ({output.Status.Value})");
								}
								else if (output.Triangles.Length == 0)
								{
									Debug.LogError($"Triangulator returned zero triangles for surface {surf}, loop index {loopIdx}.");
								}
#endif
								if (output.Status.Value != Status.OK || output.Triangles.Length == 0)
									continue;

								// Map triangles back
								var prevCount = surfaceIndexList.Length;
								var interiorCat = (CategoryIndex)info.interiorCategory;
								roVerts.RemapTriangles(interiorCat, output.Triangles, surfaceIndexList);
								uniqueVertexMapper.RegisterVertices(
									surfaceIndexList,
									prevCount,
									*brushVertices.m_Vertices,
									map3DTo2D.normal,
									instanceID,
									interiorCat);
							}
							catch (System.Exception ex) { Debug.LogException(ex); }
						}

						if (surfaceIndexList.Length == 0) continue;

						var flags = baseSurfaces[surf].destinationFlags;
						var parms = baseSurfaces[surf].destinationParameters;
						var UV0 = baseSurfaces[surf].UV0;
						var uvMat = math.mul(UV0.ToFloat4x4(), treeToPlane);
						MeshAlgorithms.ComputeUVs(uniqueVertexMapper.surfaceRenderVertices, uvMat);
						MeshAlgorithms.ComputeTangents(surfaceIndexList, uniqueVertexMapper.surfaceRenderVertices);

						ref var buf = ref surfaceBuffers[surf];
						buf.Construct(builder, surfaceIndexList,
									  uniqueVertexMapper.surfaceColliderVertices,
									  uniqueVertexMapper.surfaceSelectVertices,
									  uniqueVertexMapper.surfaceRenderVertices,
									  surf, flags, parms);
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
							ref var rb = ref surfaceBuffers[s];
							var destinationFlags = rb.destinationFlags;
							if ((destinationFlags & layerQueryMask) != layerQuery)
								continue;

							queryList.AddNoResize(new ChiselQuerySurface
							{
								surfaceIndex = rb.surfaceIndex,
								surfaceParameter = surfaceParameterIndex < 0 ? 0 : rb.destinationParameters.parameters[surfaceParameterIndex],
								vertexCount = rb.vertexCount,
								indexCount = rb.indexCount,
								surfaceHash = rb.surfaceHash,
								geometryHash = rb.geometryHash
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
				finally
				{
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
