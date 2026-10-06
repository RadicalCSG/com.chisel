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

    [BurstCompile(CompileSynchronously = true)]
    struct BuildBrushBoundsSweepJob : IJob
    {
        // Read
        [NoAlias, ReadOnly] public NativeList<IndexOrder>   allTreeBrushIndexOrders;
        [NoAlias, ReadOnly] public NativeList<MinMaxAABB>   brushTreeSpaceBounds;

        // Write
        [NoAlias] public NativeList<BrushBoundsSweepEntry>  sweepEntries;

        struct SortBySortMin : System.Collections.Generic.IComparer<BrushBoundsSweepEntry>
        {
            // Total order (ties broken on nodeOrder) so the result never depends on the sort algorithm.
            public int Compare(BrushBoundsSweepEntry x, BrushBoundsSweepEntry y)
            {
                var diff = x.sortMin.CompareTo(y.sortMin);
                if (diff != 0)
                    return diff;
                return x.nodeOrder.CompareTo(y.nodeOrder);
            }
        }

        public void Execute()
        {
            sweepEntries.Clear();
            var count = allTreeBrushIndexOrders.Length;
            if (count == 0)
                return;
            if (sweepEntries.Capacity < count)
                sweepEntries.Capacity = count;

            // Sweep along the axis with the largest overall extent: that is where the fewest brushes
            // share an interval, so the candidate ranges are the narrowest.
            var totalMin = new float3(float.PositiveInfinity);
            var totalMax = new float3(float.NegativeInfinity);
            for (int i = 0; i < count; i++)
            {
                var bounds = brushTreeSpaceBounds[allTreeBrushIndexOrders[i].nodeOrder];
                if (!math.all(math.isfinite(bounds.Min)) || !math.all(math.isfinite(bounds.Max)))
                    continue;
                totalMin = math.min(totalMin, bounds.Min);
                totalMax = math.max(totalMax, bounds.Max);
            }
            var extent = totalMax - totalMin;
            var axis = 0;
            if (extent.y > extent[axis]) axis = 1;
            if (extent.z > extent[axis]) axis = 2;

            for (int i = 0; i < count; i++)
            {
                var nodeOrder = allTreeBrushIndexOrders[i].nodeOrder;
                var bounds    = brushTreeSpaceBounds[nodeOrder];
                if (!math.all(math.isfinite(bounds.Min)) || !math.all(math.isfinite(bounds.Max)))
                    continue;
                sweepEntries.AddNoResize(new BrushBoundsSweepEntry
                {
                    bounds    = bounds,
                    sortMin   = bounds.Min[axis],
                    nodeOrder = nodeOrder,
                    axis      = axis
                });
            }

            sweepEntries.Sort(new SortBySortMin());

            var runningMax = float.NegativeInfinity;
            for (int i = 0; i < sweepEntries.Length; i++)
            {
                var entry = sweepEntries[i];
                runningMax = math.max(runningMax, entry.bounds.Max[axis]);
                entry.prefixMax = runningMax;
                sweepEntries[i] = entry;
            }
        }
    }
}
