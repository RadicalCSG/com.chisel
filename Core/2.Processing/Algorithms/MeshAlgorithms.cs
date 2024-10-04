using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using Debug = UnityEngine.Debug;
using Vector3 = UnityEngine.Vector3;
using Quaternion = UnityEngine.Quaternion;
using ReadOnlyAttribute = Unity.Collections.ReadOnlyAttribute;
using WriteOnlyAttribute = Unity.Collections.WriteOnlyAttribute;
using Unity.Entities;
using andywiecko.BurstTriangulator;
using System.Runtime.CompilerServices;
using System.ComponentModel;

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


		public void ConvertToPlaneSpace(UnsafeList<float3> vertices, UnsafeList<Edge> edges, Map3DTo2D map3DTo2D)
		{
			lookup.Clear();
			positions2D.Clear();
            edgeIndices.Clear();

			if (lookup.Capacity < vertices.Length) lookup.Capacity = vertices.Length;
			if (positions2D.Capacity < vertices.Length) positions2D.Capacity = vertices.Length;
			if (edgeIndices.Capacity < edges.Length * 2) edgeIndices.Capacity = edges.Length * 2;

			var usedIndices = new UnsafeList<int>(vertices.Length, Allocator.Temp);
			usedIndices.Resize(vertices.Length, NativeArrayOptions.ClearMemory);
			lookup.Resize(vertices.Length, NativeArrayOptions.ClearMemory);
			edgeIndices.Resize(edges.Length * 2, NativeArrayOptions.UninitializedMemory);
			for (int i = 0, j = 0; j < edges.Length; i += 2, j++)
			{
				var index1 = edges[j].index1;
				if (usedIndices[index1] == 0)
				{
					lookup[positions2D.Length] = index1;
					positions2D.Add(map3DTo2D.Convert(vertices[index1]));
					usedIndices[index1] = positions2D.Length;
				}
				index1 = (ushort)(usedIndices[index1] - 1);

				var index2 = edges[j].index2;
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
		}

		public void Dispose()
        {
		    lookup.Dispose();
		    positions2D.Dispose();
		    edgeIndices.Dispose();
        }
    }
    

    internal struct UniqueVertexMapper : IDisposable
    {
		public NativeArray<int>			indexRemap;
		public NativeList<float3>		surfaceColliderVertices;
		public NativeList<RenderVertex> surfaceRenderVertices;


		public void Reset()
		{
			indexRemap.ClearValues();
			surfaceColliderVertices.Clear();
			surfaceRenderVertices.Clear();
		}

		public void RegisterVertices(NativeList<int> triangles, int startIndex, [ReadOnly] UnsafeList<float3> sourceVertices, float3 normal, CategoryIndex categoryIndex)
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
					indexRemap[vertexIndexSrc] = vertexIndexDst + 1;
				} else
					vertexIndexDst--;
				triangles[i] = vertexIndexDst;
			}
		}

		public void Dispose()
        {
		    indexRemap.Dispose();
			surfaceColliderVertices.Dispose();
			surfaceRenderVertices.Dispose();
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

		public static void ComputeTangents([ReadOnly] NativeList<int> indices, NativeList<RenderVertex> vertices)
		{
            var triTangents     = new NativeArray<double3>(vertices.Length, Allocator.Temp);
            var triBinormals    = new NativeArray<double3>(vertices.Length, Allocator.Temp);

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

                triTangents[index0] = weight0 * tangent;
                triTangents[index1] = weight1 * tangent;
                triTangents[index2] = weight2 * tangent;

                triBinormals[index0] = weight0 * binormal;
                triBinormals[index1] = weight1 * binormal;
                triBinormals[index2] = weight2 * binormal;
            }

            for (int v = 0; v < vertices.Length; ++v)
            {
                var originalTangent = triTangents[v];
                var originalBinormal = triBinormals[v];
                var vertex = vertices[v];
                var normal = (double3)vertex.normal;

                var dotTangent = math.dot(normal, originalTangent);
                var newTangent = new double3(originalTangent.x - dotTangent * normal.x,
                                                originalTangent.y - dotTangent * normal.y,
                                                originalTangent.z - dotTangent * normal.z);
                var tangentMagnitude = math.length(newTangent);
                newTangent /= tangentMagnitude;

                var dotBinormal = math.dot(normal, originalBinormal);
                dotTangent = math.dot(newTangent, originalBinormal) * tangentMagnitude;
                var newBinormal = new double3(originalBinormal.x - dotBinormal * normal.x - dotTangent * newTangent.x,
                                                originalBinormal.y - dotBinormal * normal.y - dotTangent * newTangent.y,
                                                originalBinormal.z - dotBinormal * normal.z - dotTangent * newTangent.z);
                var binormalMagnitude = math.length(newBinormal);
                newBinormal /= binormalMagnitude;

                const double kNormalizeEpsilon = 1e-6;
                if (tangentMagnitude <= kNormalizeEpsilon || binormalMagnitude <= kNormalizeEpsilon)
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

                    newTangent = axis1 - math.dot(normal, axis1) * normal;
                    newBinormal = axis2 - math.dot(normal, axis2) * normal - math.dot(newTangent, axis2) * math.normalizesafe(newTangent);

                    newTangent = math.normalizesafe(newTangent);
                    newBinormal = math.normalizesafe(newBinormal);
                }

                var dp = math.dot(math.cross(normal, newTangent), newBinormal);
                var tangent = new float4((float3)newTangent.xyz, (dp > 0) ? 1 : -1);

                vertex.tangent = tangent;
                vertices[v] = vertex;
            }

			triTangents.Dispose();
			triBinormals.Dispose();
		}
	}
}