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