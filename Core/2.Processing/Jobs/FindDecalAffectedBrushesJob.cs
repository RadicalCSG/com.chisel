using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using ReadOnlyAttribute = Unity.Collections.ReadOnlyAttribute;

namespace Chisel.Core
{
    [BurstCompile(CompileSynchronously = true)]
    struct FindDecalAffectedBrushesJob : IJob
    {
        // Read
        [NoAlias, ReadOnly] public NativeArray<MinMaxAABB>  changedDecalBounds;
        [NoAlias, ReadOnly] public NativeList<MinMaxAABB>   brushTreeSpaceBoundCache;
        [NoAlias, ReadOnly] public NativeList<IndexOrder>   allTreeBrushIndexOrders;
        [NoAlias, ReadOnly] public int                      brushCount;

        // Read/Write
        [NoAlias] public NativeList<IndexOrder>             rebuildTreeBrushIndexOrders;

        public void Execute()
        {
            if (changedDecalBounds.Length == 0 ||
                rebuildTreeBrushIndexOrders.Length >= brushCount)
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
            var boundsCount = math.min(brushCount, brushTreeSpaceBoundCache.Length);
            for (int nodeOrder = 0; nodeOrder < boundsCount; nodeOrder++)
            {
                if (rebuilt.IsSet(nodeOrder))
                    continue;

                // A brush that has no bounds yet is new, and rebuilt anyway
                var brushBounds = brushTreeSpaceBoundCache[nodeOrder];
                if (math.any(brushBounds.Min > brushBounds.Max))
                    continue;

                for (int d = 0; d < changedDecalBounds.Length; d++)
                {
                    var decalBounds = changedDecalBounds[d];
                    if (math.any(brushBounds.Max < decalBounds.Min) ||
                        math.any(brushBounds.Min > decalBounds.Max))
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
