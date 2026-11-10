using System;
using System.Runtime.CompilerServices;
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
    struct CacheRemappingJob : IJob
    {
        // A cache's entries moved from their old order (x) to their new one (y), those without one (-1) disposed; an order
        // nothing moves to is left empty, for the brush made there
        static void Remap<T>(NativeList<BlobAssetReference<T>> cache, NativeArray<int2> remap) where T : unmanaged
        {
            var old = new NativeArray<BlobAssetReference<T>>(remap.Length, Allocator.Temp);
            for (int n = 0; n < remap.Length; n++)
            {
                old[n] = cache[n];
                cache[n] = default;
            }
            for (int n = 0; n < remap.Length; n++)
            {
                var destination = remap[n].y;
                if (destination >= 0)
                    cache[destination] = old[n];
                else if (old[n].IsCreated)
                    old[n].Dispose();
            }
            old.Dispose();
        }

        static void RemapValues<T>(NativeList<T> cache, NativeArray<int2> remap) where T : unmanaged
        {
            var old = new NativeArray<T>(remap.Length, Allocator.Temp);
            for (int n = 0; n < remap.Length; n++)
            {
                old[n] = cache[n];
                cache[n] = default;
            }
            for (int n = 0; n < remap.Length; n++)
            {
                var destination = remap[n].y;
                if (destination >= 0)
                    cache[destination] = old[n];
            }
            old.Dispose();
        }

		[NoAlias, ReadOnly] public CompactHierarchy.ReadOnly    compactHierarchy;
		[NoAlias, ReadOnly] public NativeList<int>              nodeIDValueToNodeOrder;
        [NoAlias, ReadOnly] public NativeReference<int>         nodeIDValueToNodeOrderOffsetRef;
        [NoAlias, ReadOnly] public NativeList<CompactNodeID>    brushes;
        [NoAlias, ReadOnly] public int                          brushCount;
        [NoAlias, ReadOnly] public NativeList<IndexOrder>       allTreeBrushIndexOrders;
        [NoAlias, ReadOnly] public NativeList<CompactNodeID>    brushIDValues;

        // Read/Write
        [NoAlias] public NativeList<BlobAssetReference<BasePolygonsBlob>>           basePolygonCache;
        [NoAlias] public NativeList<BlobAssetReference<RoutingTable>>               routingTableCache;
        [NoAlias] public NativeList<NodeTransformations>                            transformationCache;
        [NoAlias] public NativeList<BlobAssetReference<ChiselBrushRenderBuffer>>    brushRenderBufferCache;
        [NoAlias] public NativeList<BlobAssetReference<BrushTreeSpaceVerticesBlob>> treeSpaceVerticesCache;
        [NoAlias] public NativeList<MinMaxAABB>                                     brushTreeSpaceBoundCache;
        [NoAlias] public NativeList<BlobAssetReference<BrushesTouchedByBrush>>      brushesTouchedByBrushCache;
        [NoAlias] public NativeList<BlobAssetReference<ExactBrush>>                 exactBrushCache;

        // Write
        [NoAlias, WriteOnly] public NativeParallelHashSet<IndexOrder>   brushesThatNeedIndirectUpdateHashMap;
        [NoAlias, WriteOnly] public NativeReference<bool>               needRemappingRef;

        public void Execute()
        {
            var needRemapping = false;
            var nodeIDValueToNodeOrderOffset = nodeIDValueToNodeOrderOffsetRef.Value;

            // Remaps all cached data from previous brush order in tree, to new brush order
            var previousBrushIDValuesLength = brushIDValues.Length;
            if (previousBrushIDValuesLength > 0)
            {
				NativeArray<int> indexLookup;
				using var _indexLookup = indexLookup = new NativeArray<int>(nodeIDValueToNodeOrder.Length, Allocator.Temp);
				
				NativeArray<int2> remapOldOrderToNewOrder;
				using var _remapOldOrderToNewOrder = remapOldOrderToNewOrder = new NativeArray<int2>(previousBrushIDValuesLength, Allocator.Temp);

                for (int n = 0; n < brushCount; n++)
                {
                    var compactNodeID = allTreeBrushIndexOrders[n].compactNodeID;
                    var offsetIDValue = compactNodeID.slotIndex.index - nodeIDValueToNodeOrderOffset;
                    indexLookup[offsetIDValue] = (n + 1);
                }

				using var removedBrushes = new NativeList<IndexOrder>(previousBrushIDValuesLength, Allocator.Temp);
				var maxCount = math.max(brushCount, previousBrushIDValuesLength) + 1;
				for (int n = 0; n < previousBrushIDValuesLength; n++)
				{
					var sourceID = brushIDValues[n];
					var sourceIDValue = sourceID.slotIndex.index;
					var sourceOffset = sourceIDValue - nodeIDValueToNodeOrderOffset;
					var destination = (sourceOffset < 0 || sourceOffset >= nodeIDValueToNodeOrder.Length) ? -1 : indexLookup[sourceOffset] - 1;
					if (destination == -1)
					{
						removedBrushes.Add(new IndexOrder { compactNodeID = sourceID, nodeOrder = n });
						destination = -1;
						needRemapping = true;
					}
					else
						maxCount = math.max(maxCount, destination + 1);
					remapOldOrderToNewOrder[n] = new int2(n, destination);
					needRemapping = needRemapping || (n != destination);
				}

				if (needRemapping)
				{
					using var inTree = new NativeParallelHashSet<CompactNodeID>(math.max(16, brushes.Length), Allocator.Temp);
					for (int b = 0; b < brushes.Length; b++)
						inTree.Add(brushes[b]);
					for (int b = 0; b < removedBrushes.Length; b++)
					{
						var indexOrder = removedBrushes[b];
						//int nodeIndex = indexOrder.nodeIndex;
						int nodeOrder = indexOrder.nodeOrder;

						var brushTouchedByBrush = brushesTouchedByBrushCache[nodeOrder];
						if (!brushTouchedByBrush.IsCreated ||
							brushTouchedByBrush == BlobAssetReference<BrushesTouchedByBrush>.Null)
							continue;

						ref var brushIntersections = ref brushTouchedByBrush.Value.brushIntersections;
						for (int i = 0; i < brushIntersections.Length; i++)
						{
							var otherBrushID = brushIntersections[i].nodeIndexOrder.compactNodeID;

							if (!compactHierarchy.IsValidCompactNodeID(otherBrushID))
								continue;

							if (!inTree.Contains(otherBrushID))
								continue;

							var otherBrushIDValue = otherBrushID.slotIndex.index;
							var otherBrushOrder = nodeIDValueToNodeOrder[otherBrushIDValue - nodeIDValueToNodeOrderOffset];
							var otherIndexOrder = new IndexOrder { compactNodeID = otherBrushID, nodeOrder = otherBrushOrder };
							brushesThatNeedIndirectUpdateHashMap.Add(otherIndexOrder);
						}
					}

					if (basePolygonCache.Length < maxCount)
						basePolygonCache.Resize(maxCount, NativeArrayOptions.ClearMemory);
					if (routingTableCache.Length < maxCount)
						routingTableCache.Resize(maxCount, NativeArrayOptions.ClearMemory);
					if (transformationCache.Length < maxCount)
						transformationCache.Resize(maxCount, NativeArrayOptions.ClearMemory);
					if (treeSpaceVerticesCache.Length < maxCount)
						treeSpaceVerticesCache.Resize(maxCount, NativeArrayOptions.ClearMemory);
					if (brushRenderBufferCache.Length < maxCount)
						brushRenderBufferCache.Resize(maxCount, NativeArrayOptions.ClearMemory);
					if (brushTreeSpaceBoundCache.Length < maxCount)
						brushTreeSpaceBoundCache.Resize(maxCount, NativeArrayOptions.ClearMemory);
					if (brushesTouchedByBrushCache.Length < maxCount)
						brushesTouchedByBrushCache.Resize(maxCount, NativeArrayOptions.ClearMemory);
					if (exactBrushCache.Length < maxCount)
						exactBrushCache.Resize(maxCount, NativeArrayOptions.ClearMemory);

					// every entry to its new order, those of removed brushes disposed
					Remap(basePolygonCache, remapOldOrderToNewOrder);
					Remap(routingTableCache, remapOldOrderToNewOrder);
					RemapValues(transformationCache, remapOldOrderToNewOrder);
					Remap(brushRenderBufferCache, remapOldOrderToNewOrder);
					Remap(treeSpaceVerticesCache, remapOldOrderToNewOrder);
					RemapValues(brushTreeSpaceBoundCache, remapOldOrderToNewOrder);
					Remap(brushesTouchedByBrushCache, remapOldOrderToNewOrder);
					Remap(exactBrushCache, remapOldOrderToNewOrder);
				}
            }

            needRemappingRef.Value = needRemapping;
        }
    }
}
