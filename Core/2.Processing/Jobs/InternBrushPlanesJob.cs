using System.Runtime.CompilerServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using ReadOnlyAttribute = Unity.Collections.ReadOnlyAttribute;

namespace Chisel.Core
{
    [BurstCompile(CompileSynchronously = true)]
    struct InternBrushPlanesJob : IJob
    {
        // Read
        [NoAlias, ReadOnly] public NativeList<IndexOrder>                          allUpdateBrushIndexOrders;
        [NoAlias, ReadOnly] public NativeArray<BlobAssetReference<BrushMeshBlob>>  brushMeshLookup;
        [NoAlias, ReadOnly] public NativeList<NodeTransformations>                 transformationCache;

        // Write
        [NoAlias] public InternedPlanes      internedPlanes;
        [NoAlias] public NativeList<int>     brushPlaneIds;
        [NoAlias] public NativeArray<int2>   brushPlaneIdRange;   // by nodeOrder: (offset, count)

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static int Encode(int id, bool flipped) { return flipped ? -(id + 1) : (id + 1); }

        public void Execute()
        {
            internedPlanes.Clear();
            brushPlaneIds.Clear();

            var facePoints = new NativeList<float3>(64, Allocator.Temp);
            try
            {
                for (int index = 0; index < allUpdateBrushIndexOrders.Length; index++)
                {
                    var brushIndexOrder = allUpdateBrushIndexOrders[index];
                    int brushNodeOrder  = brushIndexOrder.nodeOrder;

                    var brushMeshBlob = brushMeshLookup[brushNodeOrder];
                    if (!brushMeshBlob.IsCreated)
                    {
                        brushPlaneIdRange[brushNodeOrder] = new int2(brushPlaneIds.Length, 0);
                        continue;
                    }

                    ref var mesh           = ref brushMeshBlob.Value;
                    ref var polygons       = ref mesh.polygons;
                    ref var halfEdges      = ref mesh.halfEdges;
                    ref var localVertices  = ref mesh.localVertices;
                    ref var localPlanes    = ref mesh.localPlanes;
                    var nodeToTree         = transformationCache[brushNodeOrder].nodeToTree;
                    var nodeToTreeInverseTransposed = math.transpose(math.inverse(nodeToTree));

                    var offset = brushPlaneIds.Length;
                    for (int p = 0; p < polygons.Length; p++)
                    {
                        if (p >= localPlanes.Length)
                        {
                            brushPlaneIds.Add(0);
                            continue;
                        }

                        ref var polygon = ref polygons[p];
                        facePoints.Clear();
                        for (int e = 0; e < polygon.edgeCount; e++)
                        {
                            var vertexIndex = halfEdges[polygon.firstEdge + e].vertexIndex;
                            if (vertexIndex < 0 || vertexIndex >= localVertices.Length)
                                continue;
                            facePoints.Add(math.mul(nodeToTree, new float4(localVertices[vertexIndex], 1)).xyz);
                        }
                        if (facePoints.Length == 0)
                        {
                            brushPlaneIds.Add(0);
                            continue;
                        }

                        var treePlane = math.mul(nodeToTreeInverseTransposed, localPlanes[p]);
                        var length = math.length(treePlane.xyz);
                        if (length <= (float)CSGConstants.kDivideMinimumEpsilon)
                        {
                            brushPlaneIds.Add(0);
                            continue;
                        }
                        treePlane /= length;

                        var id = internedPlanes.Add(treePlane, facePoints.AsArray(), out bool flipped);
                        brushPlaneIds.Add(Encode(id, flipped));
                    }
                    brushPlaneIdRange[brushNodeOrder] = new int2(offset, brushPlaneIds.Length - offset);
                }
            }
            finally { facePoints.Dispose(); }
        }
    }
}
