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

namespace Chisel.Core
{
    [BurstCompile(CompileSynchronously = true)]
    struct GenerateSurfaceTrianglesJob : IJobParallelForDefer
    {
        // Read
        // 'Required' for scheduling with index count
        [NoAlias, ReadOnly] public NativeList<IndexOrder>                           allUpdateBrushIndexOrders;
        
        [NoAlias, ReadOnly] public NativeList<BlobAssetReference<BasePolygonsBlob>> basePolygonCache;
        [NoAlias, ReadOnly] public NativeList<NodeTransformations>                  transformationCache;
        [NoAlias, ReadOnly] public NativeStream.Reader                              input;        
        [NoAlias, ReadOnly] public NativeArray<MeshQuery>                           meshQueries;

        // Write
        [NativeDisableParallelForRestriction]
        [NoAlias] public NativeList<BlobAssetReference<ChiselBrushRenderBuffer>>    brushRenderBufferCache;

        // Per thread scratch memory
        [NativeDisableContainerSafetyRestriction] NativeArray<float3>               surfaceColliderVertices;
        [NativeDisableContainerSafetyRestriction] NativeArray<RenderVertex>         surfaceRenderVertices;
        [NativeDisableContainerSafetyRestriction] NativeArray<int>                  indexRemap;
        [NativeDisableContainerSafetyRestriction] NativeList<int>                   loops;
        [NativeDisableContainerSafetyRestriction] NativeList<ChiselQuerySurface>    querySurfaceList;
        [NativeDisableContainerSafetyRestriction] HashedVertices                    brushVertices;
        [NativeDisableContainerSafetyRestriction] NativeList<UnsafeList<int>>       surfaceLoopIndices;
        [NativeDisableContainerSafetyRestriction] NativeArray<SurfaceInfo>          surfaceLoopAllInfos;
        [NativeDisableContainerSafetyRestriction] NativeList<UnsafeList<Edge>>      surfaceLoopAllEdges;
        [NativeDisableContainerSafetyRestriction] NativeList<int>                   surfaceIndexList;
        
        [BurstDiscard]
        public static void InvalidFinalCategory(CategoryIndex _interiorCategory)
        {
            Debug.Assert(false, $"Invalid final category {_interiorCategory}");
        }

        struct CompareSortByBasePlaneIndex : System.Collections.Generic.IComparer<ChiselQuerySurface>
        {
            public int Compare(ChiselQuerySurface x, ChiselQuerySurface y)
            {
                var diff = x.surfaceParameter - y.surfaceParameter;
                if (diff != 0)
                    return diff;
                return x.surfaceIndex - y.surfaceIndex;
            }
        }

        static readonly CompareSortByBasePlaneIndex compareSortByBasePlaneIndex = new CompareSortByBasePlaneIndex();

        public unsafe void Execute(int index)
        {
            var count = input.BeginForEachIndex(index);
            if (count == 0)
                return;

            var brushIndexOrder = input.Read<IndexOrder>();
            var brushNodeOrder = brushIndexOrder.nodeOrder;
            var vertexCount = input.Read<int>();
            NativeCollectionHelpers.EnsureCapacityAndClear(ref brushVertices, vertexCount);
            for (int v = 0; v < vertexCount; v++)
            {
                var vertex = input.Read<float3>();
                brushVertices.AddNoResize(vertex);
            }


            var surfaceOuterCount = input.Read<int>();
            NativeCollectionHelpers.EnsureSizeAndClear(ref surfaceLoopIndices, surfaceOuterCount);
            for (int o = 0; o < surfaceOuterCount; o++)
            {
                UnsafeList<int> inner = default;
                var surfaceInnerCount = input.Read<int>();
                if (surfaceInnerCount > 0)
                {
                    inner = new UnsafeList<int>(surfaceInnerCount, Allocator.Temp);
                    for (int i = 0; i < surfaceInnerCount; i++)
                    {
                        inner.AddNoResize(input.Read<int>());
                    }
                }
                surfaceLoopIndices[o] = inner;
            }

            var surfaceLoopCount = input.Read<int>();
            NativeCollectionHelpers.EnsureMinimumSizeAndClear(ref surfaceLoopAllInfos, surfaceLoopCount);
            NativeCollectionHelpers.EnsureSizeAndClear(ref surfaceLoopAllEdges, surfaceLoopCount);
            for (int l = 0; l < surfaceLoopCount; l++)
            {
                surfaceLoopAllInfos[l] = input.Read<SurfaceInfo>();
                var edgeCount   = input.Read<int>();
                if (edgeCount > 0)
                { 
                    var edgesInner  = new UnsafeList<Edge>(edgeCount, Allocator.Temp);
                    for (int e = 0; e < edgeCount; e++)
                    {
                        edgesInner.AddNoResize(input.Read<Edge>());
                    }
                    surfaceLoopAllEdges[l] = edgesInner;
                }
            }
            input.EndForEachIndex();



            if (!basePolygonCache[brushNodeOrder].IsCreated)
                return;

            var maxLoops = 0;
            var maxIndices = 0;
            for (int s = 0; s < surfaceLoopIndices.Length; s++)
            {
                if (!surfaceLoopIndices[s].IsCreated)
                    continue;
                var length = surfaceLoopIndices[s].Length;
                maxIndices += length;
                maxLoops = math.max(maxLoops, length);
            }


            ref var baseSurfaces            = ref basePolygonCache[brushNodeOrder].Value.surfaces;
            var brushTransformations        = transformationCache[brushNodeOrder];
            var treeToNode                  = brushTransformations.treeToNode;
            var nodeToTreeInverseTransposed = math.transpose(treeToNode);                

            var pointCount                  = brushVertices.Length + 2;

            NativeCollectionHelpers.EnsureCapacityAndClear(ref loops, maxLoops);
            NativeCollectionHelpers.EnsureCapacityAndClear(ref surfaceIndexList, maxIndices);

            var builder = new BlobBuilder(Allocator.Temp, 4096);
            ref var root = ref builder.ConstructRoot<ChiselBrushRenderBuffer>();
            var surfaceRenderBuffers = builder.Allocate(ref root.surfaces, surfaceLoopIndices.Length);

            for (int s = 0; s < surfaceLoopIndices.Length; s++)
            {
                ref var surfaceRenderBuffer = ref surfaceRenderBuffers[s];
                var surfaceIndex = s;
                surfaceRenderBuffer.surfaceIndex = surfaceIndex;

                if (!surfaceLoopIndices[s].IsCreated)
                    continue;

                loops.Clear();

                var loopIndices = surfaceLoopIndices[s];
                for (int l = 0; l < loopIndices.Length; l++)
                {
                    var surfaceLoopIndex    = loopIndices[l];
                    var surfaceLoopEdges    = surfaceLoopAllEdges[surfaceLoopIndex];

                    // TODO: verify that this never happens, check should be in previous job
                    Debug.Assert(surfaceLoopEdges.Length >= 3);
                    if (surfaceLoopEdges.Length < 3)
                        continue;

                    loops.Add(surfaceLoopIndex);
                }
                
                var destinationFlags      = baseSurfaces[surfaceIndex].destinationFlags;
                var destinationParameters = baseSurfaces[surfaceIndex].destinationParameters;
                var UV0                   = baseSurfaces[surfaceIndex].UV0;
                var localSpacePlane       = baseSurfaces[surfaceIndex].localPlane;

				// We need to convert our UV matrix from tree-space, to brush local-space, to plane-space
                // since the vertices of the polygons, at this point, are in tree-space.
				var localSpaceToPlaneSpace  = MathExtensions.GenerateLocalToPlaneSpaceMatrix(localSpacePlane);
                var treeSpaceToPlaneSpace   = math.mul(localSpaceToPlaneSpace, treeToNode);
                var uv0Matrix               = math.mul(UV0.ToFloat4x4(), treeSpaceToPlaneSpace);
                var surfaceTreeSpacePlane   = math.mul(nodeToTreeInverseTransposed, localSpacePlane);

                // Find 2 axi perpendicular to the normal
                float3 normal = surfaceTreeSpacePlane.xyz;
                float3 xAxis = new(1, 0, 0), yAxis = new(0, 1, 0), zAxis = new(0, 0, 1);
				double3 tmp = (math.abs(math.dot(normal, yAxis)) < math.abs(math.dot(normal, zAxis))) ? yAxis : zAxis;
				double3 axi1 = math.cross(normal, (math.abs(math.dot(normal, tmp)) < math.abs(math.dot(normal, xAxis))) ? tmp : xAxis);
				double3 axi2 = math.cross(normal, axi1);
				

                surfaceIndexList.Clear();

                CategoryIndex interiorCategory = CategoryIndex.ValidAligned;

                for (int l = 0; l < loops.Length; l++)
                {
                    var loopIndex   = loops[l];
                    var loopEdges   = surfaceLoopAllEdges[loopIndex];
                    var loopInfo    = surfaceLoopAllInfos[loopIndex];
                    interiorCategory = (CategoryIndex)loopInfo.interiorCategory;

                    Debug.Assert(surfaceIndex == loopInfo.basePlaneIndex, "surfaceIndex != loopInfo.basePlaneIndex");

					ref var vertices = ref *brushVertices.m_Vertices;
					var usedIndices = new NativeList<int>(vertices.Length, Allocator.Persistent);
					var positions = new NativeList<double2>(vertices.Length, Allocator.Persistent);
					usedIndices.Resize(vertices.Length, NativeArrayOptions.ClearMemory);

					var lookup = new NativeList<int>(vertices.Length, Allocator.Persistent);
					lookup.Resize(vertices.Length, NativeArrayOptions.ClearMemory);

					var constraints = new NativeList<int>(loopEdges.Length * 2, Allocator.Persistent);
					constraints.Resize(loopEdges.Length * 2, NativeArrayOptions.UninitializedMemory);
					for (int i = 0, j = 0; j < loopEdges.Length; i += 2, j++)
					{
						var index1 = loopEdges[j].index1;
						if (usedIndices[index1] == 0)
						{
							var vertex = (double3)vertices[index1];
							lookup[positions.Length] = index1;

							positions.Add(new double2(math.dot(vertex, axi1), math.dot(vertex, axi2)));

							usedIndices[index1] = positions.Length;
						}
						index1 = (ushort)(usedIndices[index1] - 1);

						var index2 = loopEdges[j].index2;
						if (usedIndices[index2] == 0)
						{
							var vertex = (double3)vertices[index2];
							lookup[positions.Length] = index2;

							positions.Add(new double2(math.dot(vertex, axi1), math.dot(vertex, axi2)));

							usedIndices[index2] = positions.Length;
						}
						index2 = (ushort)(usedIndices[index2] - 1);

						constraints[i + 0] = index1;
						constraints[i + 1] = index2;
					}

                    

					var holes = new NativeList<double2>(64, Allocator.Persistent);
					var triangulator = new Triangulator<double2>(64, Allocator.Temp)
                    {

						Input = new()
                        {
                            Positions = positions.AsArray(),
                            ConstraintEdges = constraints.AsArray(),
                            HoleSeeds = holes.AsArray()
                        },
                        Settings = new()
                        {
                            ValidateInput = true,
                            Verbose = true,
                            Preprocessor = Preprocessor.None,
                            AutoHolesAndBoundary = true,
                            RestoreBoundary = true,
                            SloanMaxIters = 1_000_000,
                            RefineMesh = false,
                            RefinementThresholds = new RefinementThresholds
                            {
                                Area = 1f,
                                Angle = math.radians(5)
                            },
                            ConcentricShellsParameter = 0.001f
                        }
                    };


					triangulator.Execute();
					
					var status = triangulator.Output.Status.Value;
					if (status != Status.OK)
					{
						Debug.LogError($"{status}");
					} else
                    { 
					    var triangles = triangulator.Output.Triangles;
					    if (triangles.Length >= 3)
					    {
                            if (interiorCategory == CategoryIndex.ValidReverseAligned ||
                                interiorCategory == CategoryIndex.ReverseAligned)
                            {
                                for (int i = 0; i < triangles.Length; i++)
                                {
								    surfaceIndexList.Add(lookup[triangles[i]]);
                                }
                            } else
					        {
						        for (int i = 0, j = triangles.Length - 1; i < triangles.Length; i++, j--)
						        {
								    surfaceIndexList.Add(lookup[triangles[j]]);
						        }
						    }
					    }
                    }

					triangulator.Dispose();
					holes.Dispose();
					positions.Dispose();
					constraints.Dispose();
					usedIndices.Dispose();
                }

                if (surfaceIndexList.Length == 0)
                    continue;

                var surfaceIndicesCount = surfaceIndexList.Length;
                NativeCollectionHelpers.EnsureMinimumSize(ref surfaceColliderVertices, brushVertices.Length);
                NativeCollectionHelpers.EnsureMinimumSize(ref surfaceRenderVertices, brushVertices.Length);                
                NativeCollectionHelpers.EnsureMinimumSizeAndClear(ref indexRemap, brushVertices.Length);


                if (interiorCategory == CategoryIndex.ValidReverseAligned || interiorCategory == CategoryIndex.ReverseAligned)
                    normal = -normal;

                // Only use the vertices that we've found in the indices
                var surfaceVerticesCount = 0;
                for (int i = 0; i < surfaceIndicesCount; i++)
                {
                    var vertexIndexSrc = surfaceIndexList[i];
                    var vertexIndexDst = indexRemap[vertexIndexSrc];
                    if (vertexIndexDst == 0)
                    {
                        vertexIndexDst = surfaceVerticesCount;
                        var position = brushVertices[vertexIndexSrc];
                        surfaceColliderVertices[surfaceVerticesCount] = position;

                        var uv0 = math.mul(uv0Matrix, new float4(position, 1)).xy;
                        surfaceRenderVertices[surfaceVerticesCount] = new RenderVertex
                        {
                            position    = position,
                            normal      = normal,
                            uv0         = uv0
                        };
                        surfaceVerticesCount++;
                        indexRemap[vertexIndexSrc] = vertexIndexDst + 1;
                    } else
                        vertexIndexDst--;
                    surfaceIndexList[i] = vertexIndexDst;
                }


                

                ComputeTangents(surfaceIndexList.AsArray(),
                                surfaceRenderVertices,
                                surfaceIndicesCount,
                                surfaceVerticesCount);


				var vertexHash = surfaceColliderVertices.Hash(surfaceVerticesCount);
				var indicesHash = surfaceIndexList.Hash(surfaceIndicesCount);
				var geometryHash = math.hash(new uint2(vertexHash, indicesHash));

				var min = new float3(float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity);
                var max = new float3(float.NegativeInfinity, float.NegativeInfinity, float.NegativeInfinity);
                for (int i = 0; i < surfaceVerticesCount; i++)
                {
                    min = math.min(min, surfaceColliderVertices[i]);
                    max = math.max(max, surfaceColliderVertices[i]);
                }

				surfaceRenderBuffer.destinationFlags      = destinationFlags;
				surfaceRenderBuffer.destinationParameters = destinationParameters;

                surfaceRenderBuffer.vertexCount = surfaceVerticesCount;
                surfaceRenderBuffer.indexCount = surfaceIndexList.Length;

                // TODO: properly compute hash again, AND USE IT
                surfaceRenderBuffer.surfaceHash = 0;// math.hash(new uint3(normalHash, tangentHash, uv0Hash));
                surfaceRenderBuffer.geometryHash = geometryHash;

                surfaceRenderBuffer.min = min;
                surfaceRenderBuffer.max = max;

                var outputIndices = builder.Construct(ref surfaceRenderBuffer.indices, surfaceIndexList, surfaceIndicesCount);
                var outputVertices = builder.Construct(ref surfaceRenderBuffer.colliderVertices, surfaceColliderVertices, surfaceVerticesCount);
                builder.Construct(ref surfaceRenderBuffer.renderVertices, surfaceRenderVertices, surfaceVerticesCount);


                Debug.Assert(outputVertices.Length == surfaceRenderBuffer.vertexCount);
                Debug.Assert(outputIndices.Length == surfaceRenderBuffer.indexCount);
            }

            NativeCollectionHelpers.EnsureCapacityAndClear(ref querySurfaceList, surfaceRenderBuffers.Length);

            var querySurfaces = builder.Allocate(ref root.querySurfaces, meshQueries.Length);
            for (int t = 0; t < meshQueries.Length; t++)
            {
                var meshQuery       = meshQueries[t];
                var layerQueryMask  = meshQuery.LayerQueryMask;
                var layerQuery      = meshQuery.LayerQuery;
                var surfaceParameterIndex = (meshQuery.LayerParameterIndex >= SurfaceParameterIndex.Parameter1 && 
                                             meshQuery.LayerParameterIndex <= SurfaceParameterIndex.MaxParameterIndex) ?
                                             (int)meshQuery.LayerParameterIndex - 1 : -1;

                querySurfaceList.Clear();

                for (int s = 0; s < surfaceRenderBuffers.Length; s++)
                {
					ref var renderBuffer = ref surfaceRenderBuffers[s];
					var destinationFlags  = renderBuffer.destinationFlags;
                    if ((destinationFlags & layerQueryMask) != layerQuery)
                        continue;

					querySurfaceList.AddNoResize(new ChiselQuerySurface
                    {
                        surfaceIndex        = renderBuffer.surfaceIndex,
                        surfaceParameter    = surfaceParameterIndex < 0 ? 0 : renderBuffer.destinationParameters.parameters[surfaceParameterIndex],
                        vertexCount         = renderBuffer.vertexCount,
                        indexCount          = renderBuffer.indexCount,
                        surfaceHash         = renderBuffer.surfaceHash,
                        geometryHash        = renderBuffer.geometryHash
                    });
                }
                querySurfaceList.Sort(compareSortByBasePlaneIndex);

                builder.Construct(ref querySurfaces[t].surfaces, querySurfaceList);
                querySurfaces[t].brushNodeID = brushIndexOrder.compactNodeID;
            }

            root.surfaceOffset = 0;
            root.surfaceCount = surfaceRenderBuffers.Length;

            var brushRenderBuffer = builder.CreateBlobAssetReference<ChiselBrushRenderBuffer>(Allocator.Persistent);
            if (brushRenderBufferCache[brushNodeOrder].IsCreated)
            {
                brushRenderBufferCache[brushNodeOrder].Dispose();
                brushRenderBufferCache[brushNodeOrder] = default;
            }
            brushRenderBufferCache[brushNodeOrder] = brushRenderBuffer;
        }

        
        static void ComputeTangents(NativeArray<int>            indices,
                                    NativeArray<RenderVertex>   vertices,
                                    int totalIndices,
                                    int totalVertices) 
        {

            var triTangents     = new NativeArray<double3>(totalVertices, Allocator.Temp);
            var triBinormals    = new NativeArray<double3>(totalVertices, Allocator.Temp);

            for (int i = 0; i < totalIndices; i += 3)
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

                var p = new double3(position1.x - position0.x, position1.y - position0.y, position1.z - position0.z );
                var q = new double3(position2.x - position0.x, position2.y - position0.y, position2.z - position0.z );
                var s = new double2(uv1.x - uv0.x, uv2.x - uv0.x);
                var t = new double2(uv1.y - uv0.y, uv2.y - uv0.y);

                var scale       = s.x * t.y - s.y * t.x;
                var absScale    = math.abs(scale);
                p *= scale; q *= scale;

                var tangent  = math.normalize(t.y * p - t.x * q) * absScale;
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

            for (int v = 0; v < totalVertices; ++v)
            {
                var originalTangent  = triTangents[v];
                var originalBinormal = triBinormals[v];
                var vertex           = vertices[v];
                var normal           = (double3)vertex.normal;

                var dotTangent = math.dot(normal, originalTangent);
                var newTangent = new double3(originalTangent.x - dotTangent * normal.x, 
                                                originalTangent.y - dotTangent * normal.y, 
                                                originalTangent.z - dotTangent * normal.z);
                var tangentMagnitude = math.length(newTangent);
                newTangent /= tangentMagnitude;

                var dotBinormal = math.dot(normal, originalBinormal);
                dotTangent      = math.dot(newTangent, originalBinormal) * tangentMagnitude;
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
                        axis1 = new double3(1,0,0);
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

                    newTangent  = axis1 - math.dot(normal, axis1) * normal;
                    newBinormal = axis2 - math.dot(normal, axis2) * normal - math.dot(newTangent, axis2) * math.normalizesafe(newTangent);

                    newTangent  = math.normalizesafe(newTangent);
                    newBinormal = math.normalizesafe(newBinormal);
                }

                var dp = math.dot(math.cross(normal, newTangent), newBinormal);
                var tangent = new float4((float3)newTangent.xyz, (dp > 0) ? 1 : -1);
                
                vertex.tangent = tangent;
                vertices[v] = vertex;
            }
        }
    }
}
