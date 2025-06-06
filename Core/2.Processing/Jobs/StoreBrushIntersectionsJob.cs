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
    [BurstCompile(CompileSynchronously = true)]
    struct StoreBrushIntersectionsJob : IJobParallelForDefer
    {
        // Read
        [NoAlias, ReadOnly] public CompactNodeID                                    treeCompactNodeID;
        [NoAlias, ReadOnly] public NativeReference<BlobAssetReference<CompactTree>> compactTreeRef;
        [NoAlias, ReadOnly] public NativeList<IndexOrder>                           allTreeBrushIndexOrders;
        [NoAlias, ReadOnly] public NativeList<IndexOrder>                           allUpdateBrushIndexOrders;
        [NoAlias, ReadOnly] public NativeList<BrushIntersectWith>                   brushIntersectionsWith;
        [NoAlias, ReadOnly] public NativeArray<int2>                                brushIntersectionsWithRange;

        // Write
        [NativeDisableParallelForRestriction]
        [NoAlias, WriteOnly] public NativeList<BlobAssetReference<BrushesTouchedByBrush>> brushesTouchedByBrushCache;

        // Per thread scratch memory
        //[NativeDisableContainerSafetyRestriction] NativeList<BrushIntersection> scratch_brushIntersection;

        static void SetUsedNodesBits([NoAlias, ReadOnly] ref CompactTree compactTree, [NoAlias, ReadOnly] in NativeList<BrushIntersection> brushIntersections, CompactNodeID brushNodeID, CompactNodeID rootNodeID, [NoAlias] ref BrushIntersectionLookup bitset)
        {
            var brushNodeIDValue = brushNodeID.slotIndex.index;
            var rootNodeIDValue  = rootNodeID.slotIndex.index;

            bitset.Clear();
            bitset.Set(brushNodeIDValue, IntersectionType.Intersection);
            bitset.Set(rootNodeIDValue, IntersectionType.Intersection);

            var minBrushIDValue                     = compactTree.minBrushIDValue;
            ref var brushAncestors                  = ref compactTree.brushAncestors;
            ref var brushAncestorLegend             = ref compactTree.brushAncestorLegend;
            ref var brushIDValueToAncestorLegend    = ref compactTree.brushIDValueToAncestorLegend;

            if (brushNodeIDValue < minBrushIDValue || (brushNodeIDValue - minBrushIDValue) >= brushIDValueToAncestorLegend.Length)
            {
                Debug.Log($"nodeIndex is out of bounds {brushNodeIDValue} - {minBrushIDValue} < {brushIDValueToAncestorLegend.Length}");
                return;
            }

            var intersectionIDValue = brushIDValueToAncestorLegend[brushNodeIDValue - minBrushIDValue];
            var intersectionInfo    = brushAncestorLegend[intersectionIDValue];
            for (int b = intersectionInfo.ancestorStartIDValue; b < intersectionInfo.ancestorEndIDValue; b++)
                bitset.Set(brushAncestors[b], IntersectionType.Intersection);

            for (int i = 0; i < brushIntersections.Length; i++)
            {
                var otherIntersectionInfo = brushIntersections[i];
                var otherNodeID           = otherIntersectionInfo.nodeIndexOrder.compactNodeID;
                var otherNodeIDValue      = otherNodeID.slotIndex.index;
                bitset.Set(otherNodeIDValue, otherIntersectionInfo.type);
                for (int b = otherIntersectionInfo.bottomUpStart; b < otherIntersectionInfo.bottomUpEnd; b++)
                    bitset.Set(brushAncestors[b], IntersectionType.Intersection);
            }
        }

        static BlobAssetReference<BrushesTouchedByBrush> GenerateBrushesTouchedByBrush([NoAlias, ReadOnly] ref CompactTree                 compactTree, 
                                                                                       [NoAlias, ReadOnly] NativeArray<IndexOrder>         allTreeBrushIndexOrders, 
                                                                                       IndexOrder brushIndexOrder, CompactNodeID           rootNodeID,
                                                                                       [NoAlias, ReadOnly] NativeArray<BrushIntersectWith> brushIntersectionsWith, int intersectionOffset, int intersectionCount,
                                                                                       //[NoAlias] ref NativeList<BrushIntersection>         scratch_brushIntersection,
                                                                                       Allocator allocator = Allocator.Persistent)// Indirect
		{
            var brushNodeID     = brushIndexOrder.compactNodeID;            
            var minBrushIDValue = compactTree.minBrushIDValue;
            var minNodeIDValue  = compactTree.minNodeIDValue;
            var maxNodeIDValue  = compactTree.maxNodeIDValue;
            ref var brushAncestorLegend             = ref compactTree.brushAncestorLegend;
            ref var brushIDValueToAncestorLegend    = ref compactTree.brushIDValueToAncestorLegend;

            // Intersections
            NativeList<BrushIntersection> scratch_brushIntersection;
			using var _scratch_brushIntersection = scratch_brushIntersection = new NativeList<BrushIntersection> (intersectionCount, Allocator.Temp);
            //NativeCollectionHelpers.EnsureCapacityAndClear(ref scratch_brushIntersection, intersectionCount);

            {
                for (int i = 0; i < intersectionCount; i++)
                {
                    var touchingBrush = brushIntersectionsWith[intersectionOffset + i];
                    //Debug.Assert(touchingBrush.brushIndexOrder0.nodeOrder == brushNodeOrder);

                    var otherIndexOrder     = touchingBrush.brushNodeOrder1;
                    var otherBrushID        = allTreeBrushIndexOrders[otherIndexOrder].compactNodeID;
                    var otherBrushIDValue   = otherBrushID.slotIndex.index;
                    if ((otherBrushIDValue < minBrushIDValue || (otherBrushIDValue - minBrushIDValue) >= brushIDValueToAncestorLegend.Length))
                        continue;
                    
                    var otherBottomUpIDValue    = brushIDValueToAncestorLegend[otherBrushIDValue - minBrushIDValue];
                    scratch_brushIntersection.AddNoResize(new BrushIntersection
                    {
                        nodeIndexOrder  = new IndexOrder { compactNodeID = otherBrushID, nodeOrder = otherIndexOrder },
                        type            = touchingBrush.type,
                        bottomUpStart   = brushAncestorLegend[otherBottomUpIDValue].ancestorStartIDValue, 
                        bottomUpEnd     = brushAncestorLegend[otherBottomUpIDValue].ancestorEndIDValue
                    });
                }
                for (int b0 = 0; b0 < scratch_brushIntersection.Length; b0++)
                {
                    var brushIntersection0 = scratch_brushIntersection[b0];
                    ref var nodeIndexOrder0 = ref brushIntersection0.nodeIndexOrder;
                    for (int b1 = b0 + 1; b1 < scratch_brushIntersection.Length; b1++)
                    {
                        var brushIntersection1 = scratch_brushIntersection[b1];
                        ref var nodeIndexOrder1 = ref brushIntersection1.nodeIndexOrder;
                        if (nodeIndexOrder0.nodeOrder > nodeIndexOrder1.nodeOrder)
                        {
                            var t = nodeIndexOrder0;
                            nodeIndexOrder0 = nodeIndexOrder1;
                            nodeIndexOrder1 = t;
                        }
                        scratch_brushIntersection[b1] = brushIntersection1;
                    }
                    scratch_brushIntersection[b0] = brushIntersection0;
                }
            }

            BrushIntersectionLookup bitset;
			using var _bitset = bitset = new BrushIntersectionLookup(minNodeIDValue, (maxNodeIDValue - minNodeIDValue) + 1, Allocator.Temp);
            SetUsedNodesBits(ref compactTree, in scratch_brushIntersection, brushNodeID, rootNodeID, ref bitset);
            
            var totalBrushIntersectionsSize = 16 + (scratch_brushIntersection.Length * UnsafeUtility.SizeOf<BrushIntersection>());
            var totalIntersectionBitsSize   = 16 + (bitset.twoBits.Length * UnsafeUtility.SizeOf<uint>());
            var totalSize                   = totalBrushIntersectionsSize + totalIntersectionBitsSize;

            using var builder = new BlobBuilder(Allocator.Temp, totalSize);
            ref var root = ref builder.ConstructRoot<BrushesTouchedByBrush>();

            builder.Construct(ref root.brushIntersections, scratch_brushIntersection);
            builder.Construct(ref root.intersectionBits, bitset.twoBits);
            root.BitCount = bitset.Length;
            root.BitOffset = bitset.Offset;
            return builder.CreateBlobAssetReference<BrushesTouchedByBrush>(allocator);// Allocator.Persistent / Confirmed to be disposed
		}

        public void Execute(int index)
        {
            var brushIndexOrder     = allUpdateBrushIndexOrders[index];
            int brushNodeOrder      = brushIndexOrder.nodeOrder;
            {
                if (compactTreeRef.IsCreated &&
                    compactTreeRef.Value.IsCreated)
                { 
                    ref var compactTree = ref compactTreeRef.Value.Value;
                    var result = GenerateBrushesTouchedByBrush(ref compactTree, 
                                                               allTreeBrushIndexOrders.AsArray(), brushIndexOrder, treeCompactNodeID,
                                                               brushIntersectionsWith.AsArray(), 
                                                               intersectionOffset: brushIntersectionsWithRange[brushNodeOrder].x, 
                                                               intersectionCount:  brushIntersectionsWithRange[brushNodeOrder].y);
                                                               //ref scratch_brushIntersection);
                    if (result.IsCreated)
                        brushesTouchedByBrushCache[brushNodeOrder] = result;
                } // else brushesTouchedByBrushCache[brushNodeOrder] = BlobAssetReference<BrushesTouchedByBrush>.Null;  ??
            }
        }
    }
}
