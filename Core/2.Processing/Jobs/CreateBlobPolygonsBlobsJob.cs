using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Debug = UnityEngine.Debug;
using ReadOnlyAttribute = Unity.Collections.ReadOnlyAttribute;
using WriteOnlyAttribute = Unity.Collections.WriteOnlyAttribute;

namespace Chisel.Core
{
    // Each brush's polygons in tree space, with the vertex indices of its mesh, and what its surfaces are drawn with
    [BurstCompile(CompileSynchronously = true)]
    struct CreateBlobPolygonsBlobsJob : IJobParallelForDefer
    {
        // Read
        [NoAlias, ReadOnly] public NativeList<IndexOrder>                                         allUpdateBrushIndexOrders;
        [NoAlias, ReadOnly] public NativeArray<BlobAssetReference<BrushMeshBlob>>                 brushMeshLookup;
        [NoAlias, ReadOnly] public NativeList<BlobAssetReference<BrushTreeSpaceVerticesBlob>>     treeSpaceVerticesCache;

        // Write
        [NativeDisableParallelForRestriction]
        [NoAlias, WriteOnly] public NativeList<BlobAssetReference<BasePolygonsBlob>>              basePolygonCache;

        public void Execute(int b)
        {
            var indexOrder = allUpdateBrushIndexOrders[b];
            int nodeOrder  = indexOrder.nodeOrder;

            if (treeSpaceVerticesCache[nodeOrder] == BlobAssetReference<BrushTreeSpaceVerticesBlob>.Null)
                return;

            var mesh                    = brushMeshLookup[nodeOrder];
            ref var treeSpaceVertices   = ref treeSpaceVerticesCache[nodeOrder].Value.treeSpaceVertices;
            ref var halfEdges           = ref mesh.Value.halfEdges;
            ref var localPlanes         = ref mesh.Value.localPlanes;
            ref var polygons            = ref mesh.Value.polygons;
            ref var halfEdgeUVs         = ref mesh.Value.halfEdgeUVs;
            var hasCornerUVs            = halfEdgeUVs.Length == halfEdges.Length && halfEdgeUVs.Length > 0;

            Debug.Assert(localPlanes.Length > 0);

            // a polygon without an area, or without a plane, has no edges
            var totalEdgeCount = 0;
            for (int p = 0; p < polygons.Length; p++)
            {
                if (polygons[p].edgeCount >= 3 && p < localPlanes.Length)
                    totalEdgeCount += polygons[p].edgeCount;
            }

            var totalEdgeSize       = 16 + (totalEdgeCount           * UnsafeUtility.SizeOf<Edge>());
            var totalPolygonSize    = 16 + (polygons.Length          * UnsafeUtility.SizeOf<BasePolygon>());
            var totalSurfaceSize    = 16 + (polygons.Length          * UnsafeUtility.SizeOf<BaseSurface>());
            var totalVertexSize     = 16 + (treeSpaceVertices.Length * UnsafeUtility.SizeOf<float3>());
            var totalUVSize         = 16 + ((hasCornerUVs ? totalEdgeCount : 0) * UnsafeUtility.SizeOf<float2>());
            var totalSize           = totalEdgeSize + totalPolygonSize + totalSurfaceSize + totalVertexSize + totalUVSize;

            using var builder = new BlobBuilder(Allocator.Temp, totalSize);
            ref var root = ref builder.ConstructRoot<BasePolygonsBlob>();
            var polygonArray = builder.Allocate(ref root.polygons, polygons.Length);
            var edgeArray    = builder.Allocate(ref root.edges, totalEdgeCount);
            builder.Construct(ref root.vertices, ref treeSpaceVertices);
            var surfaceArray = builder.Allocate(ref root.surfaces, polygons.Length);
            var edgeUVArray  = builder.Allocate(ref root.edgeUVs, hasCornerUVs ? totalEdgeCount : 0);
            var edgeIndex = 0;
            for (int p = 0; p < polygons.Length; p++)
            {
                ref var polygon = ref polygons[p];
                var startEdgeIndex = edgeIndex;
                if (polygon.edgeCount >= 3 && p < localPlanes.Length)
                {
                    var firstEdge = polygon.firstEdge;
                    var lastEdge  = firstEdge + polygon.edgeCount;
                    for (int e = firstEdge; e < lastEdge; e++)
                    {
                        var next = (e + 1 < lastEdge) ? e + 1 : firstEdge;
                        edgeArray[edgeIndex] = new Edge
                        {
                            index1 = (ushort)halfEdges[e].vertexIndex,
                            index2 = (ushort)halfEdges[next].vertexIndex
                        };
                        if (hasCornerUVs)
                            edgeUVArray[edgeIndex] = halfEdgeUVs[e];
                        edgeIndex++;
                    }
                }
                polygonArray[p] = new BasePolygon()
                {
                    nodeIndexOrder  = indexOrder,
                    surfaceInfo     = new SurfaceInfo
                    {
                        basePlaneIndex      = (ushort)p,
                        interiorCategory    = (byte)CategoryIndex.ValidAligned,
                    },
                    startEdgeIndex  = startEdgeIndex,
                    endEdgeIndex    = edgeIndex
                };
                surfaceArray[p] = new BaseSurface
                {
                    destinationFlags      = polygon.surface.destinationFlags,
                    destinationParameters = polygon.surface.parameters,
                    outputFlags           = polygon.surface.outputFlags,
                    UV0                   = polygon.surface.details.UV0,
                    localPlane            = p < localPlanes.Length ? localPlanes[p] : default,
                    descriptionIndex      = polygon.descriptionIndex,
                    smoothingGroup        = polygon.surface.details.smoothingGroup,
                    hotspotLayout         = polygon.surface.hotspotLayout,
                    hotspotPinned         = polygon.surface.details.hotspot.pinnedTile,
                    hotspotPinnedSheet    = polygon.surface.details.hotspot.pinnedSheet,
                    hotspotSeed           = polygon.surface.details.hotspot.seed,
                    // mirrored top to bottom is mirrored left to right and turned half way round
                    hotspotRotation       = polygon.surface.details.hotspot.rotation + (polygon.surface.details.hotspot.mirrorY ? 180 : 0),
                    hotspotDensity        = polygon.surface.details.hotspot.Density,
                    hotspotMirror         = polygon.surface.details.hotspot.mirror != polygon.surface.details.hotspot.mirrorY,
                    hotspotOffset         = polygon.surface.details.hotspot.offset,
                    textureInWorld        = (polygon.surface.details.detailFlags & SurfaceDetailFlags.TextureIsInWorldSpace) != 0,
                    cornerUVs             = hasCornerUVs && polygon.edgeCount >= 3 && p < localPlanes.Length &&
                                            (polygon.surface.details.detailFlags & SurfaceDetailFlags.TextureIsInWorldSpace) == 0
                };
            }
            var basePolygonsBlob = builder.CreateBlobAssetReference<BasePolygonsBlob>(Allocator.Persistent); // Confirmed to be disposed
            basePolygonCache[nodeOrder] = basePolygonsBlob;
        }
    }
}
