using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using Debug = UnityEngine.Debug;
using ReadOnlyAttribute = Unity.Collections.ReadOnlyAttribute;
using WriteOnlyAttribute = Unity.Collections.WriteOnlyAttribute;

namespace Chisel.Core
{
    [BurstCompile(CompileSynchronously = true)]
    struct GatherBrushIntersectionPairsJob : IJob
    {
        // Read
        [NoAlias, ReadOnly] public NativeArray<UnsafeList<BrushIntersectWith>> brushBrushIntersections;

        // Write
        [NoAlias, WriteOnly] public NativeArray<int2>       brushIntersectionsWithRange;

        // Read / Write
        [NoAlias] public NativeList<BrushIntersectWith>     brushIntersectionsWith;

        // Per thread scratch memory
        [NativeDisableContainerSafetyRestriction] NativeList<BrushPair> intersections;


        struct BrusPairListComparer : System.Collections.Generic.IComparer<BrushPair>
        {
            public readonly int Compare(BrushPair x, BrushPair y)
            {
                var orderX = x.brushNodeOrder0;
                var orderY = y.brushNodeOrder0;
                var diff = orderX.CompareTo(orderY);
                if (diff != 0)
                    return diff;

                orderX = x.brushNodeOrder1;
                orderY = y.brushNodeOrder1;
                return orderX.CompareTo(orderY);
            }
        }


        public void Execute()
        {
            var minCount = brushBrushIntersections.Length * 16;

            NativeList<BrushPair> intersections;
			using var _intersections = intersections = new NativeList<BrushPair> (minCount, Allocator.Temp);
			//NativeCollectionHelpers.EnsureCapacityAndClear(ref intersections, minCount);
            for (int i = 0; i < brushBrushIntersections.Length; i++)
            {
                if (!brushBrushIntersections[i].IsCreated)
                    continue;
                var subArray = brushBrushIntersections[i];
                for (int j = 0; j < subArray.Length; j++)
                {
                    var intersectWith = subArray[j];
                    var pair = new BrushPair
                    {
                        brushNodeOrder0 = i,
                        brushNodeOrder1 = intersectWith.brushNodeOrder1,
                        type = intersectWith.type
                    };
                    intersections.Add(pair);
                    pair.Flip();
                    intersections.Add(pair);
                }
            }
            brushIntersectionsWith.Clear();
            if (intersections.Length == 0)
                return;

            if (brushIntersectionsWith.Capacity < intersections.Length)
                brushIntersectionsWith.Capacity = intersections.Length;

            intersections.Sort(new BrusPairListComparer());           

            var currentPair = intersections[0];
            int previousOrder = currentPair.brushNodeOrder0;
            brushIntersectionsWith.AddNoResize(new BrushIntersectWith
            {
                brushNodeOrder1 = currentPair.brushNodeOrder1,
                type            = currentPair.type,
            });

            int2 range = new(0, 1);
            for (int i = 1; i < intersections.Length; i++)
            {
                currentPair = intersections[i];
                int currentOrder = currentPair.brushNodeOrder0;
                brushIntersectionsWith.AddNoResize(new BrushIntersectWith
                {
                    brushNodeOrder1 = currentPair.brushNodeOrder1,
                    type            = currentPair.type,
                });
                if (currentOrder != previousOrder)
                {
                    //Debug.Log($"{previousOrder} {range}");
                    brushIntersectionsWithRange[previousOrder] = range;
                    previousOrder = currentOrder;
                    range.x = i;
                    range.y = 1;
                } else
                    range.y++;
            }
            brushIntersectionsWithRange[previousOrder] = range;
        }
    }
}
