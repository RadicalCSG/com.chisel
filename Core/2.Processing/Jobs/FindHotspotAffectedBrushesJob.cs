using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using ReadOnlyAttribute = Unity.Collections.ReadOnlyAttribute;

namespace Chisel.Core
{
    // The brushes with a surface drawn on a hotspot layout edited since the last update (changedLayouts, by id), added to
    // the ones rebuilt: only they are mapped again
    [BurstCompile(CompileSynchronously = true)]
    struct FindHotspotAffectedBrushesJob : IJob
    {
        // Read
        [NoAlias, ReadOnly] public NativeArray<bool>                                 changedLayouts;
        [NoAlias, ReadOnly] public NativeList<BlobAssetReference<BasePolygonsBlob>>  basePolygonCache;
        [NoAlias, ReadOnly] public NativeList<IndexOrder>                            allTreeBrushIndexOrders;
        [NoAlias, ReadOnly] public int                                               brushCount;

        // Read/Write
        [NoAlias] public NativeList<IndexOrder>                                      rebuildTreeBrushIndexOrders;

        public void Execute()
        {
            var any = false;
            for (int i = 1; i < changedLayouts.Length && !any; i++)
                any = changedLayouts[i];
            if (!any || rebuildTreeBrushIndexOrders.Length >= brushCount)
                return;

            using var rebuilt = new NativeBitArray(brushCount, Allocator.Temp, NativeArrayOptions.ClearMemory);
            for (int i = 0; i < rebuildTreeBrushIndexOrders.Length; i++)
            {
                var nodeOrder = rebuildTreeBrushIndexOrders[i].nodeOrder;
                if (nodeOrder >= 0 && nodeOrder < brushCount)
                    rebuilt.Set(nodeOrder, true);
            }

            if (rebuildTreeBrushIndexOrders.Capacity < brushCount)
                rebuildTreeBrushIndexOrders.Capacity = brushCount;

            var added = false;
            var count = System.Math.Min(brushCount, basePolygonCache.Length);
            for (int nodeOrder = 0; nodeOrder < count; nodeOrder++)
            {
                // a brush without polygons yet is new, and rebuilt anyway
                if (rebuilt.IsSet(nodeOrder) || !basePolygonCache[nodeOrder].IsCreated)
                    continue;
                ref var surfaces = ref basePolygonCache[nodeOrder].Value.surfaces;
                for (int s = 0; s < surfaces.Length; s++)
                {
                    var layout = surfaces[s].hotspotLayout;
                    if (layout <= 0 || layout >= changedLayouts.Length || !changedLayouts[layout])
                        continue;
                    rebuildTreeBrushIndexOrders.AddNoResize(allTreeBrushIndexOrders[nodeOrder]);
                    added = true;
                    break;
                }
            }

            // FindModifiedBrushesJob lists the brushes in tree order; keep it that way
            if (added)
                rebuildTreeBrushIndexOrders.Sort(new IntersectionUtility.IndexOrderComparer());
        }
    }
}
