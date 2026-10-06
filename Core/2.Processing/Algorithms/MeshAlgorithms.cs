using System;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using ReadOnlyAttribute = Unity.Collections.ReadOnlyAttribute;
using WriteOnlyAttribute = Unity.Collections.WriteOnlyAttribute;
using System.Runtime.CompilerServices;
using UnityEditor;
using UnityEngine;

namespace Chisel.Core
{
    public readonly struct Map3DTo2D
	{
		public readonly float3 normal;
		public readonly double3 axi1;
		public readonly double3 axi2;

        public Map3DTo2D(float3 normal)
        {
			// Find 2 axi perpendicular to the normal
			double3 xAxis = new(1, 0, 0), yAxis = new(0, 1, 0), zAxis = new(0, 0, 1);
			double3 tmp = (math.abs(math.dot(normal, yAxis)) < math.abs(math.dot(normal, zAxis))) ? yAxis : zAxis;
			this.normal = normal;
			this.axi1 = math.cross(normal, (math.abs(math.dot(normal, tmp)) < math.abs(math.dot(normal, xAxis))) ? tmp : xAxis);
			this.axi2 = math.cross(normal, this.axi1);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public readonly double2 Convert(double3 vertex)
		{
			return new double2(math.dot(vertex, axi1), math.dot(vertex, axi2));
		}
	}

    internal struct Vertex2DRemapper : IDisposable
    {
		public NativeList<int>      lookup;
		public NativeList<double2>  positions2D;
		public NativeList<int>      edgeIndices;

		public NativeArray<int>     collapseDegree;
		public NativeArray<int>     collapseNeighbourA;
		public NativeArray<int>     collapseNeighbourB;

		public static NativeArray<int> AllocateCollapseScratch(int vertexCount)
		{
			return new NativeArray<int>(math.max(vertexCount, 1), Allocator.Temp, NativeArrayOptions.ClearMemory);
		}

		void EnsureCollapseScratch(int vertexCount)
		{
			if (collapseDegree.IsCreated && collapseDegree.Length >= vertexCount)
				return;
			if (collapseDegree.IsCreated)     collapseDegree.Dispose();
			if (collapseNeighbourA.IsCreated) collapseNeighbourA.Dispose();
			if (collapseNeighbourB.IsCreated) collapseNeighbourB.Dispose();
			collapseDegree     = AllocateCollapseScratch(vertexCount);
			collapseNeighbourA = AllocateCollapseScratch(vertexCount);
			collapseNeighbourB = AllocateCollapseScratch(vertexCount);
		}

        public struct ReadOnly
		{
            [ReadOnly] public NativeArray<int>     lookup;
			[ReadOnly] public NativeArray<double2> positions2D;
			[ReadOnly] public NativeArray<int>     edgeIndices;

			public void RemapTriangles(CategoryIndex interiorCategory,
                                       [ReadOnly] NativeList<int> triangles, 
                                       [WriteOnly] NativeList<int> surfaceIndexList)
			{
                if (triangles.Length < 3)
                    return;
				
				if (interiorCategory == CategoryIndex.ValidReverseAligned ||
					interiorCategory == CategoryIndex.ReverseAligned)
				{
					for (int i = 0; i < triangles.Length; i++)
					{
						surfaceIndexList.Add(lookup[triangles[i]]);
					}
				}
				else
				{
					for (int i = 0, j = triangles.Length - 1; i < triangles.Length; i++, j--)
					{
						surfaceIndexList.Add(lookup[triangles[j]]);
					}
				}
			}
		};

        public ReadOnly AsReadOnly()
        {
            return new ReadOnly()
            {
                lookup      = lookup.AsArray(),
				positions2D = positions2D.AsArray(),
				edgeIndices = edgeIndices.AsArray()
			};
        }

        public bool RepairBoundary()
        {
            int edgeCount = edgeIndices.Length / 2;
            if (edgeCount < 2)
                return false;

            var tmp = new UnsafeList<Edge>(edgeCount, Allocator.Temp);
            for (int e = 0; e < edgeCount; e++)
                tmp.Add(new Edge { index1 = (ushort)edgeIndices[e * 2], index2 = (ushort)edgeIndices[e * 2 + 1] });

            var changed = LoopEdgeSplitter.RemoveTinyComponents(ref tmp);
            changed |= LoopEdgeSplitter.CloseSingleOpenChain(ref tmp);
            if (changed)
            {
                edgeIndices.Clear();
                for (int i = 0; i < tmp.Length; i++)
                {
                    edgeIndices.Add(tmp[i].index1);
                    edgeIndices.Add(tmp[i].index2);
                }
            }
            tmp.Dispose();
            return changed;
        }

        public void Clear()
        {
	        lookup.Clear();
	        positions2D.Clear();
			edgeIndices.Clear();
        }

		public void ConvertToPlaneSpace(UnsafeList<float3> vertices, UnsafeList<Edge> edges, Map3DTo2D map3DTo2D)
		{
			ConvertToPlaneSpace(vertices, edges, map3DTo2D, default);
		}

		public void ConvertToPlaneSpace(UnsafeList<float3> vertices, UnsafeList<Edge> edges, Map3DTo2D map3DTo2D, NativeArray<bool> protectedVertices)
		{
			lookup.Clear();
			positions2D.Clear();
            edgeIndices.Clear();

			var cleanEdges = new NativeList<int2>(edges.Length, Allocator.Temp);
			for (int e = 0; e < edges.Length; e++)
			{
				int a = edges[e].index1;
				int b = edges[e].index2;
				if (a == b)                 // drop degenerate (zero-length) edges
					continue;
				int lo = math.min(a, b), hi = math.max(a, b);
				bool duplicate = false;
				for (int k = 0; k < cleanEdges.Length; k++)
				{
					var ck = cleanEdges[k];
					if (math.min(ck.x, ck.y) == lo && math.max(ck.x, ck.y) == hi) { duplicate = true; break; }
				}
				if (!duplicate)
					cleanEdges.Add(new int2(a, b));
			}

			if (cleanEdges.Length > 0)
			{
				const float kSqrEps = CSGConstants.kSqrEdgeDistanceEpsilon;
				EnsureCollapseScratch(vertices.Length);
				var deg  = collapseDegree;
				var nbrA = collapseNeighbourA;
				var nbrB = collapseNeighbourB;
				bool changed = true;
				while (changed && cleanEdges.Length > 0)
				{
					changed = false;

					// Rebuild degree + (up to two) neighbours for every vertex still referenced.
					for (int e = 0; e < cleanEdges.Length; e++)
					{
						deg[cleanEdges[e].x] = 0;
						deg[cleanEdges[e].y] = 0;
					}
					for (int e = 0; e < cleanEdges.Length; e++)
					{
						int x = cleanEdges[e].x, y = cleanEdges[e].y;
						if (deg[x] == 0) nbrA[x] = y; else if (deg[x] == 1) nbrB[x] = y;
						deg[x]++;
						if (deg[y] == 0) nbrA[y] = x; else if (deg[y] == 1) nbrB[y] = x;
						deg[y]++;
					}

					// Find one collapsible vertex (restart the pass after each collapse so the
					// adjacency stays valid - loops are tiny so this is cheap).
					int collapse = -1, pA = -1, pB = -1;
					for (int e = 0; e < cleanEdges.Length && collapse < 0; e++)
					{
						for (int s = 0; s < 2; s++)
						{
							int v = (s == 0) ? cleanEdges[e].x : cleanEdges[e].y;
							if (deg[v] != 2) continue;
							if (protectedVertices.IsCreated && v < protectedVertices.Length && protectedVertices[v]) continue;
							int a = nbrA[v], b = nbrB[v];
							if (a == b) continue;
							double3 pv = vertices[v], p = vertices[a], q = vertices[b];
							double3 d = q - p;
							double dlen2 = math.dot(d, d);
							if (dlen2 < 1e-12) continue;                 // neighbours coincide
							double t = math.dot(pv - p, d) / dlen2;
							if (t < 0.0 || t > 1.0) continue;            // only true mid-points
							double3 diff = pv - (p + t * d);
							if (math.dot(diff, diff) > kSqrEps) continue;
							collapse = v; pA = a; pB = b; break;
						}
					}

					if (collapse >= 0)
					{
						for (int e = cleanEdges.Length - 1; e >= 0; e--)
						{
							if (cleanEdges[e].x == collapse || cleanEdges[e].y == collapse)
								cleanEdges.RemoveAt(e);
						}
						if (pA != pB)   // wire the two neighbours together (dedup against existing)
						{
							int lo = math.min(pA, pB), hi = math.max(pA, pB);
							bool present = false;
							for (int k = 0; k < cleanEdges.Length; k++)
							{
								var ck = cleanEdges[k];
								if (math.min(ck.x, ck.y) == lo && math.max(ck.x, ck.y) == hi) { present = true; break; }
							}
							if (!present) cleanEdges.Add(new int2(pA, pB));
						}
						changed = true;
					}
				}
			}

			if (lookup.Capacity < vertices.Length) lookup.Capacity = vertices.Length;
			if (positions2D.Capacity < vertices.Length) positions2D.Capacity = vertices.Length;
			if (edgeIndices.Capacity < cleanEdges.Length * 2) edgeIndices.Capacity = cleanEdges.Length * 2;
						
			UnsafeList<int> usedIndices;			
			using var _usedIndices = usedIndices = new UnsafeList<int>(vertices.Length, Allocator.Temp);

			usedIndices.Resize(vertices.Length, NativeArrayOptions.ClearMemory);
			lookup.Resize(vertices.Length, NativeArrayOptions.ClearMemory);
			edgeIndices.Resize(cleanEdges.Length * 2,
				NativeArrayOptions.ClearMemory);
				//NativeArrayOptions.UninitializedMemory);
			for (int i = 0, j = 0; j < cleanEdges.Length; i += 2, j++)
			{
				var index1 = cleanEdges[j].x;
				if (usedIndices[index1] == 0)
				{
					lookup[positions2D.Length] = index1;
					positions2D.Add(map3DTo2D.Convert(vertices[index1]));
					usedIndices[index1] = positions2D.Length;
				}
				index1 = (ushort)(usedIndices[index1] - 1);

				var index2 = cleanEdges[j].y;
				if (usedIndices[index2] == 0)
				{
					lookup[positions2D.Length] = index2;
					positions2D.Add(map3DTo2D.Convert(vertices[index2]));
					usedIndices[index2] = positions2D.Length;
				}
				index2 = (ushort)(usedIndices[index2] - 1);

				edgeIndices[i + 0] = index1;
				edgeIndices[i + 1] = index2;
			}
			
            usedIndices.Dispose();
			cleanEdges.Dispose();
		}
		
		public void RemoveDuplicates()
		{
			var cleaned = new NativeList<int>(positions2D.Length, Allocator.Temp);
			var used = new NativeArray<bool>(positions2D.Length, Allocator.Temp);
			for (int i = 0; i < positions2D.Length; i++)
			{
				if (!used[i])
				{
					cleaned.Add(lookup[i]);
					used[i] = true;
				}
			}

			lookup.Clear();
			lookup.AddRange(cleaned.ToArray(Allocator.Temp));
			cleaned.Dispose();
			used.Dispose();
		}

		// Helper function to check if point q lies on segment pr (assuming p, q, r are collinear)
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private static bool OnSegment(double2 p, double2 q, double2 r)
		{
			return (q.x <= math.max(p.x, r.x) && q.x >= math.min(p.x, r.x) &&
			        q.y <= math.max(p.y, r.y) && q.y >= math.min(p.y, r.y));
		}

		// Helper function to find orientation of ordered triplet (p, q, r)
		// Returns:
		// 0 --> p, q and r are collinear
		// 1 --> Clockwise
		// 2 --> Counterclockwise
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private static int Orientation(double2 p, double2 q, double2 r)
		{
			// Signed area via the centralized predicate.
			double val = CSGMath.Orient2D(p, q, r);

			if (math.abs(val) < 1e-12) return 0; // Collinear (use a small epsilon for float comparison)
			return (val > 0) ? 1 : 2; // Clockwise or Counterclockwise
		}
		
		// Helper function to check if line segment 'p1q1' and 'p2q2' intersect.
		private static bool SegmentsIntersect(double2 p1, double2 q1, double2 p2, double2 q2)
		{
			// Find the four orientations needed for general and special cases
			int o1 = Orientation(p1, q1, p2);
			int o2 = Orientation(p1, q1, q2);
			int o3 = Orientation(p2, q2, p1);
			int o4 = Orientation(p2, q2, q1);

			// General case: Orientations are different
			if (o1 != o2 && o3 != o4)
				return true;

			// Special Cases (Collinear points)
			// p1, q1 and p2 are collinear and p2 lies on segment p1q1
			if (o1 == 0 && OnSegment(p1, p2, q1)) return true;

			// p1, q1 and q2 are collinear and q2 lies on segment p1q1
			if (o2 == 0 && OnSegment(p1, q2, q1)) return true;

			// p2, q2 and p1 are collinear and p1 lies on segment p2q2
			if (o3 == 0 && OnSegment(p2, p1, q2)) return true;

			// p2, q2 and q1 are collinear and q1 lies on segment p2q2
			if (o4 == 0 && OnSegment(p2, q1, q2)) return true;

			// Doesn't fall into any intersecting case
			return false;
		}

		/// <summary>
		/// Checks if any non-adjacent edges in the 2D polygon representation intersect.
		/// Assumes ConvertToPlaneSpace has been called.
		/// </summary>
		/// <returns>True if self-intersections are found, false otherwise.</returns>
		public bool CheckForSelfIntersections()
		{
			var n_edges = edgeIndices.Length / 2;
			if (n_edges < 2) // Need at least 2 edges to intersect
				return false;

			// Iterate through all pairs of edges
			for (int i = 0; i < n_edges; ++i)
			{
				int idx_p1 = edgeIndices[i * 2 + 0];
				int idx_q1 = edgeIndices[i * 2 + 1];
				double2 p1 = positions2D[idx_p1];
				double2 q1 = positions2D[idx_q1];

				// Compare with subsequent edges to avoid redundant checks
				for (int j = i + 1; j < n_edges; ++j)
				{
					int idx_p2 = edgeIndices[j * 2 + 0];
					int idx_q2 = edgeIndices[j * 2 + 1];

					// Skip check if edges share a vertex (they are adjacent)
					if (idx_p1 == idx_p2 || idx_p1 == idx_q2 || idx_q1 == idx_p2 || idx_q1 == idx_q2)
					{
						continue;
					}

					double2 p2 = positions2D[idx_p2];
					double2 q2 = positions2D[idx_q2];

					if (math.any(math.min(p1, q1) > math.max(p2, q2)) ||
						math.any(math.max(p1, q1) < math.min(p2, q2)))
						continue;

					// Check if the segments intersect
					if (SegmentsIntersect(p1, q1, p2, q2))
					{
						// For debugging:
						// Debug.LogWarning($"Self-intersection detected between edge {i} ({idx_p1}-{idx_q1}) and edge {j} ({idx_p2}-{idx_q2})");
						return true; // Found an intersection
					}
				}
			}

			return false; // No intersections found
		}
		
		public void RemoveSelfIntersectingEdges()
		{
			int edgeCount = edgeIndices.Length / 2;
			if (edgeCount < 2)
				return;

			// Track which edges to drop
			var removeFlags = new NativeArray<bool>(edgeCount, Allocator.Temp);
			for (int i = 0; i < edgeCount; i++)
				removeFlags[i] = false;

			// Mark any pair of non‐adjacent edges that intersect
			for (int i = 0; i < edgeCount; ++i)
			{
				int p1i = edgeIndices[i * 2 + 0];
				int q1i = edgeIndices[i * 2 + 1];
				double2 p1 = positions2D[p1i];
				double2 q1 = positions2D[q1i];

				for (int j = i + 1; j < edgeCount; ++j)
				{
					int p2i = edgeIndices[j * 2 + 0];
					int q2i = edgeIndices[j * 2 + 1];

					// skip adjacent edges
					if (p1i == p2i || p1i == q2i || q1i == p2i || q1i == q2i)
						continue;

					double2 p2 = positions2D[p2i];
					double2 q2 = positions2D[q2i];

					// Same strictly-disjoint bounding-box reject as CheckForSelfIntersections.
					if (math.any(math.min(p1, q1) > math.max(p2, q2)) ||
						math.any(math.max(p1, q1) < math.min(p2, q2)))
						continue;

					if (SegmentsIntersect(p1, q1, p2, q2))
					{
						removeFlags[i] = true;
						removeFlags[j] = true;
					}
				}
			}

			// Rebuild edgeIndices skipping any marked edges
			var cleaned = new NativeList<int>(edgeIndices.Capacity, Allocator.Temp);
			for (int i = 0; i < edgeCount; i++)
			{
				if (!removeFlags[i])
				{
					cleaned.Add(edgeIndices[i * 2 + 0]);
					cleaned.Add(edgeIndices[i * 2 + 1]);
				}
			}

			// Swap back
			removeFlags.Dispose();
			edgeIndices.Clear();
			edgeIndices.AddRange(cleaned.AsArray());
			cleaned.Dispose();
		}

		public void Dispose()
        {
		    lookup.Dispose();
		    positions2D.Dispose();
		    edgeIndices.Dispose();
		    if (collapseDegree.IsCreated)     collapseDegree.Dispose();
		    if (collapseNeighbourA.IsCreated) collapseNeighbourA.Dispose();
		    if (collapseNeighbourB.IsCreated) collapseNeighbourB.Dispose();
        }
    }
    

    internal struct UniqueVertexMapper : IDisposable
    {
		public NativeArray<int>			indexRemap;
		public NativeList<float3>		surfaceColliderVertices;
		public NativeList<SelectVertex> surfaceSelectVertices;
		public NativeList<RenderVertex> surfaceRenderVertices;


		public void Reset()
		{
			indexRemap.ClearValues();
			surfaceColliderVertices.Clear();
			surfaceSelectVertices.Clear();
			surfaceRenderVertices.Clear();
		}

		public void RegisterVertices(NativeList<int> triangles, int startIndex, [ReadOnly] UnsafeList<float3> sourceVertices, float3 normal, ulong entityID, CategoryIndex categoryIndex)
		{
			var surfaceNormal = normal;
			if (categoryIndex == CategoryIndex.ValidReverseAligned || categoryIndex == CategoryIndex.ReverseAligned)
				surfaceNormal = -surfaceNormal;
			for (int i = startIndex; i < triangles.Length; i++)
			{
				var vertexIndexSrc = triangles[i];
				var vertexIndexDst = indexRemap[vertexIndexSrc];
				if (vertexIndexDst == 0)
				{
					vertexIndexDst = surfaceColliderVertices.Length;
					var position = sourceVertices[vertexIndexSrc];
					surfaceColliderVertices.Add(position);
					surfaceRenderVertices.Add(new RenderVertex
					{
						position = position,
						normal = surfaceNormal
					});
					surfaceSelectVertices.Add(new SelectVertex
					{
						position = position,
						entityID = new Vector4((int)(byte)(entityID & 0xFF), (int)(byte)((entityID >> 8) & 0xFF), (int)(byte)((entityID >> 16) & 0xFF), (int)(byte)((entityID >> 24) & 0xFF)) / 255f
				});
					indexRemap[vertexIndexSrc] = vertexIndexDst + 1;
				} else
					vertexIndexDst--;
				triangles[i] = vertexIndexDst;
			}
		}

		public void Dispose()
		{
			if (indexRemap.IsCreated) indexRemap.Dispose(); indexRemap = default;
			if (surfaceColliderVertices.IsCreated) surfaceColliderVertices.Dispose(); surfaceColliderVertices = default;
			if (surfaceSelectVertices.IsCreated) surfaceSelectVertices.Dispose(); surfaceSelectVertices = default;
			if (surfaceRenderVertices.IsCreated) surfaceRenderVertices.Dispose(); surfaceRenderVertices = default;
		}
    }

	public static class MeshAlgorithms
	{
		public static void ComputeUVs(NativeList<RenderVertex> vertices, float4x4 uv0Matrix)// array might be larger than number of vertices
		{
            for (int i = 0; i < vertices.Length; i ++)
			{
                var vertex = vertices[i];
				var uv0 = math.mul(uv0Matrix, new float4(vertex.position, 1)).xy;
				vertex.uv0 = uv0;
				vertices[i] = vertex;
			}
		}

		/// <summary>
		/// The axes a surface's lightmap chart lies along in its plane. They follow the world: a floor's run along x and z, a
		/// wall's along the ground and up, so the texels of a surface in an axis' plane line up with its edges, and surfaces in
		/// one plane share one grid.
		/// </summary>
		public static void LightmapAxes(float3 planeNormal, out float3 u, out float3 v)
		{
			var normal = math.normalizesafe(planeNormal, new float3(0, 1, 0));
			var size   = math.abs(normal);
			// Along x, unless the plane faces mostly along x
			var along  = (size.x > size.y && size.x >= size.z) ? new float3(0, 0, 1) : new float3(1, 0, 0);
			u = math.normalize(along - (normal * math.dot(normal, along)));
			v = math.cross(normal, u);
		}

		/// <summary>
		/// Gives the vertices of a surface their lightmap coordinates: where they lie in its plane, along <see cref="LightmapAxes"/>,
		/// in the units of the tree. Each output mesh lays its surfaces' charts out in its own lightmap (LightmapUVLayout).
		/// </summary>
		public static void ComputeLightmapCoordinates(NativeList<RenderVertex> vertices, float3 planeNormal)
		{
			LightmapAxes(planeNormal, out var u, out var v);
			for (int i = 0; i < vertices.Length; i++)
			{
				var vertex = vertices[i];
				vertex.uv1 = new float2(math.dot(vertex.position, u), math.dot(vertex.position, v));
				vertices[i] = vertex;
			}
		}

		/// <summary>
		/// Gives the vertices of a planar surface their tangents from its plane and its texture mapping, the way their normals
		/// come from its plane: the tangent is the direction in the plane along which u grows while v stays put, the
		/// bitangent the same for v, for the affine mapping uv = uvMatrix * position. Every triangle of the surface gives
		/// that same answer - except one as thin as a float step, whose corners round to one point, which gives NaN. The
		/// exact CSG draws such triangles where brushes meet a hair out of line, and a NaN tangent lights its surface NaN.
		/// Each vertex's tangent is made perpendicular to its own normal, which normal smoothing may have turned.
		/// </summary>
		public static void ComputeTangents(NativeList<RenderVertex> vertices, float3 planeNormal, float4x4 uvMatrix)
		{
			// u = dot(a, position) + ..., v = dot(b, position) + ...: rows 0 and 1 of the mapping
			var n = math.normalizesafe((double3)planeNormal);
			var a = new double3(uvMatrix.c0.x, uvMatrix.c1.x, uvMatrix.c2.x);
			var b = new double3(uvMatrix.c0.y, uvMatrix.c1.y, uvMatrix.c2.y);
			// In the plane: a.T = 1, b.T = 0 and a.B = 0, b.B = 1, solved by T = (b x n) / det, B = (n x a) / det
			var determinant = math.dot(n, math.cross(a, b));
			var mapped = determinant != 0 && math.isfinite(determinant);
			var tangent   = mapped ? math.cross(b, n) / determinant : double3.zero;
			var bitangent = mapped ? math.cross(n, a) / determinant : double3.zero;
			for (int i = 0; i < vertices.Length; i++)
			{
				var vertex = vertices[i];
				var normal = math.normalizesafe((double3)vertex.normal, n);
				vertex.tangent = Tangent(normal, tangent, bitangent);
				vertices[i] = vertex;
			}
		}

		static float4 Tangent(double3 normal, double3 tangent, double3 bitangent)
		{
			var perpendicular = tangent - normal * math.dot(normal, tangent);
			var length = math.length(perpendicular);
			if (!(length > 0) || !math.isfinite(length))
				return AxisTangent(normal);
			perpendicular /= length;
			var side = math.dot(math.cross(normal, perpendicular), bitangent);
			return new float4((float3)perpendicular, side < 0 ? -1 : 1);
		}

		static float4 AxisTangent(double3 normal)
		{
			var dpXN = math.abs(math.dot(new double3(1, 0, 0), normal));
			var dpYN = math.abs(math.dot(new double3(0, 1, 0), normal));
			var dpZN = math.abs(math.dot(new double3(0, 0, 1), normal));

			double3 axis1, axis2;
			if (dpXN <= dpYN && dpXN <= dpZN)
			{
				axis1 = new double3(1, 0, 0);
				axis2 = (dpYN <= dpZN) ? new double3(0, 1, 0) : new double3(0, 0, 1);
			}
			else if (dpYN <= dpXN && dpYN <= dpZN)
			{
				axis1 = new double3(0, 1, 0);
				axis2 = (dpXN <= dpZN) ? new double3(1, 0, 0) : new double3(0, 0, 1);
			}
			else
			{
				axis1 = new double3(0, 0, 1);
				axis2 = (dpXN <= dpYN) ? new double3(1, 0, 0) : new double3(0, 1, 0);
			}

			var tangent   = math.normalizesafe(axis1 - math.dot(normal, axis1) * normal, axis1);
			var bitangent = math.normalizesafe(axis2 - math.dot(normal, axis2) * normal - math.dot(tangent, axis2) * tangent, axis2);
			var side = math.dot(math.cross(normal, tangent), bitangent);
			return new float4((float3)tangent, side < 0 ? -1 : 1);
		}

		/// <summary>
		/// Tangents from the triangles, for texture coordinates that are not one affine mapping of the plane (a perspective
		/// decal's). A triangle that gives no tangent - its corners rounded to one point, or its texture coordinates to one
		/// line - is left out instead of writing NaN over what its neighbours gave.
		/// </summary>
		public static void ComputeTangents([ReadOnly] NativeList<int> indices, NativeList<RenderVertex> vertices)
		{
			NativeArray<double3> triTangents;
			using var _triTangents = triTangents = new NativeArray<double3>(vertices.Length, Allocator.Temp);
			NativeArray<double3> triBinormals;
			using var _triBinormals = triBinormals = new NativeArray<double3>(vertices.Length, Allocator.Temp);

            for (int i = 0; i < indices.Length; i += 3)
            {
                var index0 = indices[i + 0];
                var index1 = indices[i + 1];
                var index2 = indices[i + 2];

                var vertex0 = vertices[index0];
                var vertex1 = vertices[index1];
                var vertex2 = vertices[index2];
                var position0 = vertex0.position;
                var position1 = vertex1.position;
                var position2 = vertex2.position;
                var uv0 = vertex0.uv0;
                var uv1 = vertex1.uv0;
                var uv2 = vertex2.uv0;

                var p = new double3(position1.x - position0.x, position1.y - position0.y, position1.z - position0.z);
                var q = new double3(position2.x - position0.x, position2.y - position0.y, position2.z - position0.z);
                var s = new double2(uv1.x - uv0.x, uv2.x - uv0.x);
                var t = new double2(uv1.y - uv0.y, uv2.y - uv0.y);

                var scale = s.x * t.y - s.y * t.x;
                var absScale = math.abs(scale);
                p *= scale; q *= scale;

                var tangent = math.normalize(t.y * p - t.x * q) * absScale;
                var binormal = math.normalize(s.x * q - s.y * p) * absScale;

                var edge20 = math.normalize(position2 - position0);
                var edge01 = math.normalize(position0 - position1);
                var edge12 = math.normalize(position1 - position2);

                var angle0 = math.dot(edge20, -edge01);
                var angle1 = math.dot(edge01, -edge12);
                var angle2 = math.dot(edge12, -edge20);
                var weight0 = math.acos(math.clamp(angle0, -1.0, 1.0));
                var weight1 = math.acos(math.clamp(angle1, -1.0, 1.0));
                var weight2 = math.acos(math.clamp(angle2, -1.0, 1.0));

                // a triangle with no area in space or in its texture coordinates has no tangent to give
                if (!math.all(math.isfinite(tangent)) || !math.all(math.isfinite(binormal)) ||
                    !math.isfinite(weight0) || !math.isfinite(weight1) || !math.isfinite(weight2))
                    continue;

                triTangents[index0] = weight0 * tangent;
                triTangents[index1] = weight1 * tangent;
                triTangents[index2] = weight2 * tangent;

                triBinormals[index0] = weight0 * binormal;
                triBinormals[index1] = weight1 * binormal;
                triBinormals[index2] = weight2 * binormal;
            }

            for (int v = 0; v < vertices.Length; ++v)
            {
                var vertex = vertices[v];
                var normal = math.normalizesafe((double3)vertex.normal, new double3(0, 1, 0));
                vertex.tangent = Tangent(normal, triTangents[v], triBinormals[v]);
                vertices[v] = vertex;
            }
		}
	}
}