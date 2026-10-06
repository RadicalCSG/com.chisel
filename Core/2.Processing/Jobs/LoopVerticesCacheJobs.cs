using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using ReadOnlyAttribute = Unity.Collections.ReadOnlyAttribute;
using WriteOnlyAttribute = Unity.Collections.WriteOnlyAttribute;

namespace Chisel.Core
{

    [BurstCompile(CompileSynchronously = true)]
    struct SeedLoopVerticesFromCacheJob : IJob
    {
        // Read
        [NoAlias, ReadOnly] public NativeList<IndexOrder>           allTreeBrushIndexOrders;
        [NoAlias, ReadOnly] public NativeList<IndexOrder>           allUpdateBrushIndexOrders;
        [NoAlias, ReadOnly] public NativeList<UnsafeList<float3>>   loopVerticesCache;
        public Allocator allocator;

        // Write (only the entries of brushes outside the update; the update's own entries are created
        // by FindLoopOverlapIntersectionsJob)
        [NoAlias] public NativeArray<UnsafeList<float3>>            loopVerticesLookup;

        public void Execute()
        {
            var brushCount = loopVerticesLookup.Length;
            if (brushCount == 0)
                return;

            using var updated = new NativeBitArray(brushCount, Allocator.Temp, NativeArrayOptions.ClearMemory);
            for (int i = 0; i < allUpdateBrushIndexOrders.Length; i++)
            {
                var nodeOrder = allUpdateBrushIndexOrders[i].nodeOrder;
                if (nodeOrder >= 0 && nodeOrder < brushCount)
                    updated.Set(nodeOrder, true);
            }

            for (int i = 0; i < allTreeBrushIndexOrders.Length; i++)
            {
                var nodeOrder = allTreeBrushIndexOrders[i].nodeOrder;
                if (nodeOrder < 0 || nodeOrder >= brushCount || nodeOrder >= loopVerticesCache.Length ||
                    updated.IsSet(nodeOrder))
                    continue;

                var cached = loopVerticesCache[nodeOrder];
                if (!cached.IsCreated || cached.Length == 0)
                    continue;

                var copy = new UnsafeList<float3>(cached.Length, allocator);
                copy.AddRange(cached);
                loopVerticesLookup[nodeOrder] = copy;
            }
        }
    }

    [BurstCompile(CompileSynchronously = true)]
    struct StoreLoopVerticesJob : IJob
    {
        // Read
        [NoAlias, ReadOnly] public NativeList<IndexOrder>                                   allUpdateBrushIndexOrders;
        [NoAlias, ReadOnly] public NativeList<IndexOrder>                                   allTreeBrushIndexOrders;
        [NoAlias, ReadOnly] public NativeArray<UnsafeList<float3>>                          loopVerticesLookup;
        [NoAlias, ReadOnly] public NativeList<BlobAssetReference<BrushesTouchedByBrush>>    brushesTouchedByBrushCache;
        [NoAlias, ReadOnly] public NativeList<MinMaxAABB>                                   brushTreeSpaceBounds;
        [NoAlias, ReadOnly] public NativeArray<int>                                         mergeBrushState;
        [NoAlias, ReadOnly] public int                                                      lastMergeIteration;

        // Read / Write
        [NoAlias] public NativeList<UnsafeList<float3>>                                     loopVerticesCache;

        // Write
        [NoAlias] public NativeList<CompactNodeID>                                          staleBrushes;
        [NoAlias] public NativeArray<int>                                                   stats;
        public static int kStatsCount(int mergeIterations) { return mergeIterations + 4; }

        static bool SameVertices(in UnsafeList<float3> a, in UnsafeList<float3> b)
        {
            var lengthA = a.IsCreated ? a.Length : 0;
            var lengthB = b.IsCreated ? b.Length : 0;
            if (lengthA != lengthB)
                return false;
            for (int i = 0; i < lengthA; i++)
            {
                if (!math.all(a[i] == b[i]))
                    return false;
            }
            return true;
        }

        // Vertices in `from` that are not in `to` (exact positions; order and duplicates are irrelevant
        // to the neighbours, which consume these lists as sets).
        static void AddMissing(in UnsafeList<float3> from, in UnsafeList<float3> to, ref NativeList<float3> result)
        {
            var fromLength = from.IsCreated ? from.Length : 0;
            if (fromLength == 0)
                return;
            var toLength = to.IsCreated ? to.Length : 0;
            using var lookup = new NativeHashSet<float3>(math.max(1, toLength), Allocator.Temp);
            for (int i = 0; i < toLength; i++)
                lookup.Add(to[i]);
            for (int i = 0; i < fromLength; i++)
            {
                var vertex = from[i];
                if (!lookup.Contains(vertex))
                    result.Add(vertex);
            }
        }

        public void Execute()
        {
            staleBrushes.Clear();
            for (int i = 0; i < stats.Length; i++)
                stats[i] = 0;
            var brushCount = loopVerticesLookup.Length;
            if (brushCount == 0)
                return;
            for (int p = 0; p <= lastMergeIteration; p++)
            {
                var changedInPass = 0;
                for (int i = 0; i < allUpdateBrushIndexOrders.Length; i++)
                {
                    var nodeOrder = allUpdateBrushIndexOrders[i].nodeOrder;
                    if (nodeOrder >= 0 && nodeOrder < brushCount &&
                        (mergeBrushState[(p * brushCount) + nodeOrder] & MergeTouchingBrushVerticesIndirectJob.kChanged) != 0)
                        changedInPass++;
                }
                stats[p] = changedInPass;
            }
            var statChangedBrushes = lastMergeIteration + 1;
            var statChangedVertices = lastMergeIteration + 2;
            var statMarksByChange = lastMergeIteration + 3;
            var statMarksByMoving = lastMergeIteration + 4;

            var influence = 2.0f * math.max(HashedVertices.kCellSize, CSGConstants.kEdgeIntersectionEpsilon);

            using var updated = new NativeBitArray(brushCount, Allocator.Temp, NativeArrayOptions.ClearMemory);
            using var marked  = new NativeBitArray(brushCount, Allocator.Temp, NativeArrayOptions.ClearMemory);
            for (int i = 0; i < allUpdateBrushIndexOrders.Length; i++)
            {
                var nodeOrder = allUpdateBrushIndexOrders[i].nodeOrder;
                if (nodeOrder >= 0 && nodeOrder < brushCount)
                    updated.Set(nodeOrder, true);
            }

            var changedVertices = new NativeList<float3>(64, Allocator.Temp);
            for (int i = 0; i < allUpdateBrushIndexOrders.Length; i++)
            {
                var nodeOrder = allUpdateBrushIndexOrders[i].nodeOrder;
                if (nodeOrder < 0 || nodeOrder >= brushCount || nodeOrder >= loopVerticesCache.Length)
                    continue;

                var current = loopVerticesLookup[nodeOrder];
                var cached  = loopVerticesCache[nodeOrder];
                var stillMoving = (mergeBrushState[(lastMergeIteration * brushCount) + nodeOrder] &
                                   MergeTouchingBrushVerticesIndirectJob.kChanged) != 0;

                changedVertices.Clear();
                if (!SameVertices(current, cached))
                {
                    AddMissing(current, cached, ref changedVertices);
                    AddMissing(cached, current, ref changedVertices);
                    stats[statChangedBrushes]++;
                    stats[statChangedVertices] += changedVertices.Length;

                    if (cached.IsCreated)
                        cached.Dispose();
                    var stored = default(UnsafeList<float3>);
                    if (current.IsCreated && current.Length > 0)
                    {
                        stored = new UnsafeList<float3>(current.Length, Allocator.Persistent);
                        stored.AddRange(current);
                    }
                    loopVerticesCache[nodeOrder] = stored;
                }
                if (changedVertices.Length == 0 && !stillMoving)
                    continue;

                var touched = brushesTouchedByBrushCache[nodeOrder];
                if (!touched.IsCreated)
                    continue;

                ref var brushIntersections = ref touched.Value.brushIntersections;
                for (int t = 0; t < brushIntersections.Length; t++)
                {
                    var otherOrder = brushIntersections[t].nodeIndexOrder.nodeOrder;
                    if (otherOrder < 0 || otherOrder >= brushCount || otherOrder == nodeOrder)
                        continue;
                    // A neighbour inside this update already welded against / split at this brush's
                    // vertices this round - unless those vertices were still moving in the last pass.
                    if (updated.IsSet(otherOrder) && !stillMoving)
                        continue;
                    if (marked.IsSet(otherOrder))
                        continue;

                    if (!stillMoving)
                    {
                        // Only a neighbour with a changed vertex within reach of its own vertices can be
                        // affected; its loop vertices lie inside its tree-space bounds.
                        var bounds = brushTreeSpaceBounds[otherOrder];
                        var min = bounds.Min - influence;
                        var max = bounds.Max + influence;
                        var affected = false;
                        for (int c = 0; c < changedVertices.Length && !affected; c++)
                        {
                            var vertex = changedVertices[c];
                            affected = !math.any(vertex < min) && !math.any(vertex > max);
                        }
                        if (!affected)
                            continue;
                    }

                    marked.Set(otherOrder, true);
                    staleBrushes.Add(allTreeBrushIndexOrders[otherOrder].compactNodeID);
                    stats[stillMoving ? statMarksByMoving : statMarksByChange]++;
                }
            }
            changedVertices.Dispose();
        }
    }
}
