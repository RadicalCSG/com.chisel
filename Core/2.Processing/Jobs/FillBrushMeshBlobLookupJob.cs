using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Debug = UnityEngine.Debug;
using ReadOnlyAttribute = Unity.Collections.ReadOnlyAttribute;
using WriteOnlyAttribute = Unity.Collections.WriteOnlyAttribute;

namespace Chisel.Core
{
    // A brush's mesh as an update looked it up: its id, its blob and how many polygons it has
    struct BrushMeshLookupEntry
    {
        public BlobAssetReference<BrushMeshBlob>    blob;
        public int                                  meshID;
        public int                                  polygonCount;
    }

    // Each brush's mesh, by node order. Kept from the last update (cache) where it is still the same: a brush whose mesh
    // changes is flagged (ShapeModified, so it is in rebuildTreeBrushIndexOrders), and while a brush holds its mesh id the id
    // stays registered with the same blob. Looked up again for those brushes, a brush whose id differs or that had none,
    // and every brush when the brush list changed (a brush at an order may be another one).
    [BurstCompile(CompileSynchronously = true)]
    struct FillBrushMeshBlobLookupJob : IJob
    {
        // Read
        [NoAlias, ReadOnly] public NativeParallelHashMap<int, RefCountedBrushMeshBlob> brushMeshBlobs;
        [NoAlias, ReadOnly] public NativeList<IndexOrder> allTreeBrushIndexOrders;
        [NoAlias, ReadOnly] public NativeArray<int>       allBrushMeshIDs;
        [NoAlias, ReadOnly] public NativeList<IndexOrder> rebuildTreeBrushIndexOrders;
        [NoAlias, ReadOnly] public bool                   brushListChanged;

        // Read / Write
        [NoAlias] public NativeList<BrushMeshLookupEntry> cache;
        [NoAlias] public NativeArray<int>                 stats;

        // Write
        [NoAlias, WriteOnly] public NativeArray<BlobAssetReference<BrushMeshBlob>> brushMeshLookup;
        [NoAlias, WriteOnly] public NativeReference<int> surfaceCountRef;

        public void Execute()
        {
            int brushCount = allBrushMeshIDs.Length;
            if (cache.Length < brushCount)
                cache.Resize(brushCount, NativeArrayOptions.ClearMemory);

            // the brushes that changed are looked up below
            if (!brushListChanged)
            {
                for (int r = 0; r < rebuildTreeBrushIndexOrders.Length; r++)
                {
                    int nodeOrder = rebuildTreeBrushIndexOrders[r].nodeOrder;
                    if (nodeOrder < brushCount)
                        cache[nodeOrder] = default;
                }
            }

            int lookedUp = 0;
            int surfaceCount = 0;
            for (int nodeOrder = 0; nodeOrder < brushCount; nodeOrder++)
            {
                var entry = cache[nodeOrder];
                if (brushListChanged || entry.meshID == 0 || entry.meshID != allBrushMeshIDs[nodeOrder] || !entry.blob.IsCreated)
                {
                    entry = LookUp(nodeOrder);
                    lookedUp++;
                }
                surfaceCount += entry.polygonCount;
                brushMeshLookup[nodeOrder] = entry.blob;
            }
            surfaceCountRef.Value = surfaceCount * 2;
            stats[(int)ExactCSGStat.MeshesLookedUp] += lookedUp;
        }

        BrushMeshLookupEntry LookUp(int nodeOrder)
        {
            int brushMeshHash = allBrushMeshIDs[nodeOrder];
            var entry = new BrushMeshLookupEntry { meshID = brushMeshHash };
            if (brushMeshHash != 0 &&
                brushMeshBlobs.TryGetValue(brushMeshHash, out var item))
            {
                entry.blob         = item.brushMeshBlob;
                entry.polygonCount = item.brushMeshBlob.Value.polygons.Length;
            } else
            if (brushMeshHash == 0)
            {
                // The brushMeshID is invalid: a Generator created/didn't update a TreeBrush correctly
                Debug.LogError($"The brushMeshID is invalid: a Generator created/didn't update a TreeBrush correctly.");
            } else
            {
                // *should* never happen
                var nodeID = allTreeBrushIndexOrders[nodeOrder].compactNodeID;
                Debug.LogError($"Brush with ID {nodeID} has its brushMeshID set to {brushMeshHash}, which is not initialized.");
            }
            cache[nodeOrder] = entry;
            return entry;
        }
    }
}
