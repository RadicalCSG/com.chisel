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
    // Each brush's mesh id, in node order
    // Each brush's mesh id, in node order
    [BurstCompile(CompileSynchronously = true)]
    struct UpdateBrushMeshIDsJob : IJob
    {
        // Read
        [NoAlias, ReadOnly] public CompactHierarchy.ReadOnly compactHierarchy;
        [NoAlias, ReadOnly] public int                       brushCount;
        [NoAlias, ReadOnly] public NativeList<CompactNodeID> brushes;

        // Write
        [NoAlias, WriteOnly] public NativeArray<int>         allBrushMeshIDs;

        public void Execute()
        {
            for (int nodeOrder = 0; nodeOrder < brushCount; nodeOrder++)
            {
                var brushCompactNodeID = brushes[nodeOrder];
                int brushMeshHash = -1;
                if (!compactHierarchy.IsValidCompactNodeID(brushCompactNodeID) ||
                    // NOTE: Assignment is intended, this is not supposed to be a comparison
                    (brushMeshHash = compactHierarchy.GetBrushMeshID(brushCompactNodeID)) == 0)
                {
                    // The brushMeshID is invalid: a Generator created/didn't update a TreeBrush correctly, or the input values didn't produce a valid mesh (for example; 0 height cube)
                    Debug.LogError($"Brush with ID ({brushCompactNodeID}) has its brushMeshID set to ({brushMeshHash}), which is invalid.");
                    allBrushMeshIDs[nodeOrder] = 0;
                } else
                    allBrushMeshIDs[nodeOrder] = brushMeshHash;
            }
        }
    }
}
