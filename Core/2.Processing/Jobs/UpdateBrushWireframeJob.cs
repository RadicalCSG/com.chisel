using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Debug = UnityEngine.Debug;
using ReadOnlyAttribute = Unity.Collections.ReadOnlyAttribute;
using WriteOnlyAttribute = Unity.Collections.WriteOnlyAttribute;

namespace Chisel.Core
{
	[BurstCompile(CompileSynchronously = true)]
    unsafe struct UpdateBrushWireframeJob : IJob
	{
		// Read
        [NoAlias, ReadOnly] public CompactHierarchy.ReadOnly compactHierarchy;
        [NoAlias, ReadOnly] public NativeArray<IndexOrder>   allUpdateBrushIndexOrders;
        [NoAlias, ReadOnly] public NativeParallelHashMap<int, RefCountedBrushMeshBlob> brushMeshBlobs;

		// Read/Write
        [NoAlias] public BrushWireframeManager brushWireframeManager;

		public void Execute()
        {
            for (int index = 0; index < allUpdateBrushIndexOrders.Length; index++)
            {
                var brushIndexOrder = allUpdateBrushIndexOrders[index];
                var brushNodeID     = brushIndexOrder.compactNodeID;
                var brushMeshHash   = compactHierarchy.GetBrushMeshID(brushNodeID);
                if (brushMeshBlobs.TryGetValue(brushMeshHash, out var item) && item.brushMeshBlob.IsCreated)
                {
					var wireframeBlob = NativeWireframeBlob.Create(ref item.brushMeshBlob.Value);
					brushWireframeManager.SetWireframe(brushNodeID, wireframeBlob);
                }
            }
        }
    }
}
