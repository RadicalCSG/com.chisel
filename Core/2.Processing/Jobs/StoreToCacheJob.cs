﻿using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;

using Debug = UnityEngine.Debug;
using ReadOnlyAttribute = Unity.Collections.ReadOnlyAttribute;
using WriteOnlyAttribute = Unity.Collections.WriteOnlyAttribute;

namespace Chisel.Core
{
    // TODO: move somewhere else
    [BurstCompile(CompileSynchronously = true)]
    struct StoreToCacheJob : IJob
    {
        // Read
        [NoAlias, ReadOnly] public NativeList<IndexOrder> allTreeBrushIndexOrders;
        [NoAlias, ReadOnly] public NativeList<MinMaxAABB> brushTreeSpaceBoundCache;
        [NoAlias, ReadOnly] public NativeList<BlobAssetReference<ChiselBrushRenderBuffer>> brushRenderBufferCache;

        // Read / Write
        [NoAlias] public NativeParallelHashMap<CompactNodeID, BlobAssetReference<ChiselBrushRenderBuffer>> brushRenderBufferLookup;

        public void Execute()
        {
            brushRenderBufferLookup.Clear();
            for (int i = 0; i < allTreeBrushIndexOrders.Length; i++)
            {
                var nodeID = allTreeBrushIndexOrders[i].compactNodeID;
				brushRenderBufferLookup[nodeID] = brushRenderBufferCache[i];
            }
        }
    }
}
