using System.Runtime.CompilerServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using ReadOnlyAttribute = Unity.Collections.ReadOnlyAttribute;
using WriteOnlyAttribute = Unity.Collections.WriteOnlyAttribute;

namespace Chisel.Core
{
    struct BrushBoundsSweepEntry
    {
        public MinMaxAABB bounds;
        public float      sortMin;    // bounds.Min[axis]; the sort key
        public float      prefixMax;  // max(bounds.Max[axis]) over entries[0..this]; non-decreasing
        public int        nodeOrder;
        public int        axis;       // the sweep axis; identical in every entry
    }

    static class BrushBoundsSweep
    {
        // Total order (ties broken on nodeOrder) so the result never depends on the sort algorithm.
        public struct SortBySortMin : System.Collections.Generic.IComparer<BrushBoundsSweepEntry>
        {
            public int Compare(BrushBoundsSweepEntry x, BrushBoundsSweepEntry y)
            {
                var diff = x.sortMin.CompareTo(y.sortMin);
                if (diff != 0)
                    return diff;
                return x.nodeOrder.CompareTo(y.nodeOrder);
            }
        }

        // Sweep along the axis with the largest overall extent: that is where the fewest brushes share an interval, so the
        // candidate ranges are the narrowest.
        public static int Axis(NativeList<IndexOrder> brushes, NativeList<MinMaxAABB> bounds)
        {
            var totalMin = new float3(float.PositiveInfinity);
            var totalMax = new float3(float.NegativeInfinity);
            for (int i = 0; i < brushes.Length; i++)
            {
                var brushBounds = bounds[brushes[i].nodeOrder];
                if (!math.all(math.isfinite(brushBounds.Min)) || !math.all(math.isfinite(brushBounds.Max)))
                    continue;
                totalMin = math.min(totalMin, brushBounds.Min);
                totalMax = math.max(totalMax, brushBounds.Max);
            }
            var extent = totalMax - totalMin;
            var axis = 0;
            if (extent.y > extent[axis]) axis = 1;
            if (extent.z > extent[axis]) axis = 2;
            return axis;
        }

        // A brush's entry; false for a brush without (finite) bounds, which is not in the sweep
        public static bool TryEntry(int nodeOrder, in MinMaxAABB bounds, int axis, out BrushBoundsSweepEntry entry)
        {
            entry = new BrushBoundsSweepEntry { bounds = bounds, sortMin = bounds.Min[axis], nodeOrder = nodeOrder, axis = axis };
            return math.all(math.isfinite(bounds.Min)) && math.all(math.isfinite(bounds.Max));
        }

        // Every brush's entry, sorted, each with its prefixMax
        public static void Build(NativeList<IndexOrder> brushes, NativeList<MinMaxAABB> bounds, NativeList<BrushBoundsSweepEntry> entries)
        {
            Build(brushes, bounds, entries, Axis(brushes, bounds));
        }

        public static void Build(NativeList<IndexOrder> brushes, NativeList<MinMaxAABB> bounds, NativeList<BrushBoundsSweepEntry> entries, int axis)
        {
            entries.Clear();
            if (brushes.Length == 0)
                return;
            if (entries.Capacity < brushes.Length)
                entries.Capacity = brushes.Length;
            for (int i = 0; i < brushes.Length; i++)
            {
                var nodeOrder = brushes[i].nodeOrder;
                if (TryEntry(nodeOrder, bounds[nodeOrder], axis, out var entry))
                    entries.AddNoResize(entry);
            }
            entries.Sort(new SortBySortMin());
            var runningMax = float.NegativeInfinity;
            for (int i = 0; i < entries.Length; i++)
            {
                var entry = entries[i];
                runningMax = math.max(runningMax, entry.bounds.Max[axis]);
                entry.prefixMax = runningMax;
                entries[i] = entry;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool FindRange([NoAlias, ReadOnly] ref NativeList<BrushBoundsSweepEntry> entries, in MinMaxAABB bounds, double epsilon, out int first, out int last)
        {
            first = 0;
            last  = -1;
            var count = entries.Length;
            if (count == 0)
                return false;

            var axis    = entries[0].axis;
            var padding = 2.0f * (float)epsilon;
            var qMin    = bounds.Min[axis] - padding;
            var qMax    = bounds.Max[axis] + padding;

            // last = last entry whose sortMin <= qMax (sortMin is ascending)
            int lo = 0, hi = count;
            while (lo < hi)
            {
                var mid = (lo + hi) >> 1;
                if (entries[mid].sortMin <= qMax) lo = mid + 1; else hi = mid;
            }
            last = lo - 1;
            if (last < 0)
                return false;

            // first = first entry whose prefixMax >= qMin (prefixMax is non-decreasing). Every entry
            // whose own Max >= qMin has prefixMax >= qMin too, so nothing that overlaps is skipped.
            lo = 0; hi = last + 1;
            while (lo < hi)
            {
                var mid = (lo + hi) >> 1;
                if (entries[mid].prefixMax >= qMin) hi = mid; else lo = mid + 1;
            }
            first = lo;
            return first <= last;
        }
    }

    // The bounds sweep, kept with the tree: built from every brush when the brush list changed or the axis did, otherwise
    // the kept entries without the brushes built again, merged with those brushes' new entries. Either way it is the sweep
    // Build gives for the bounds the brushes have now.
    [BurstCompile(CompileSynchronously = true)]
    struct BuildBrushBoundsSweepJob : IJob
    {
        // Read
        [NoAlias, ReadOnly] public NativeList<IndexOrder>   allTreeBrushIndexOrders;
        [NoAlias, ReadOnly] public NativeList<MinMaxAABB>   brushTreeSpaceBounds;
        // the brushes whose bounds can have changed since the last update: ExactBoundsJob took theirs again
        [NoAlias, ReadOnly] public NativeList<IndexOrder>   rebuildTreeBrushIndexOrders;
        // a brush was added, removed or moved to another order since the last update (CacheRemappingJob)
        [NoAlias, ReadOnly] public bool                     brushListChanged;

        // Read / Write
        [NoAlias] public NativeList<BrushBoundsSweepEntry>  sweepEntries;
        [NoAlias] public NativeArray<int>                   stats;

        public void Execute()
        {
            int count = allTreeBrushIndexOrders.Length;
            int axis  = BrushBoundsSweep.Axis(allTreeBrushIndexOrders, brushTreeSpaceBounds);
            if (brushListChanged || count == 0 || sweepEntries.Length == 0 || sweepEntries[0].axis != axis)
            {
                BrushBoundsSweep.Build(allTreeBrushIndexOrders, brushTreeSpaceBounds, sweepEntries, axis);
                stats[(int)ExactCSGStat.SweepEntriesSorted] += sweepEntries.Length;
                return;
            }

            var changed = new NativeBitArray(math.max(1, count), Allocator.Temp);
            var moved   = new NativeList<BrushBoundsSweepEntry>(math.max(1, rebuildTreeBrushIndexOrders.Length), Allocator.Temp);
            var kept    = new NativeArray<BrushBoundsSweepEntry>(sweepEntries.AsArray(), Allocator.Temp);
            try
            {
                for (int r = 0; r < rebuildTreeBrushIndexOrders.Length; r++)
                {
                    int nodeOrder = rebuildTreeBrushIndexOrders[r].nodeOrder;
                    if (nodeOrder >= count || changed.IsSet(nodeOrder))
                        continue;
                    changed.Set(nodeOrder, true);
                    if (BrushBoundsSweep.TryEntry(nodeOrder, brushTreeSpaceBounds[nodeOrder], axis, out var entry))
                        moved.Add(entry);
                }
                var order = new BrushBoundsSweep.SortBySortMin();
                moved.Sort(order);
                stats[(int)ExactCSGStat.SweepEntriesSorted] += moved.Length;

                // both sorted the same way: merged, with the prefixMax of the entries as they are now
                sweepEntries.Clear();
                if (sweepEntries.Capacity < kept.Length + moved.Length)
                    sweepEntries.Capacity = kept.Length + moved.Length;
                int k = 0, m = 0;
                var runningMax = float.NegativeInfinity;
                while (true)
                {
                    while (k < kept.Length && changed.IsSet(kept[k].nodeOrder))
                        k++;
                    BrushBoundsSweepEntry next;
                    if (k < kept.Length && (m >= moved.Length || order.Compare(kept[k], moved[m]) < 0))
                        next = kept[k++];
                    else if (m < moved.Length)
                        next = moved[m++];
                    else
                        break;
                    runningMax = math.max(runningMax, next.bounds.Max[axis]);
                    next.prefixMax = runningMax;
                    sweepEntries.AddNoResize(next);
                }
            }
            finally
            {
                changed.Dispose();
                moved.Dispose();
                kept.Dispose();
            }
        }
    }
}
