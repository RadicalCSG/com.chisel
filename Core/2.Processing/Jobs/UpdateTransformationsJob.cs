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
    internal struct NodeOrderNodeID { public int nodeOrder; public CompactNodeID compactNodeID; }

    [BurstCompile(CompileSynchronously = true)]
    unsafe struct UpdateTransformationsJob: IJobParallelForDefer
    {      
        // Read
        [NoAlias, ReadOnly] public CompactHierarchy.ReadOnly            compactHierarchy;
        [NoAlias, ReadOnly] public NativeList<NodeOrderNodeID>          transformTreeBrushIndicesList;

        // Write
        [NativeDisableParallelForRestriction]
        [NoAlias, WriteOnly] public NativeList<NodeTransformations>    transformationCache;

        public void Execute(int index)
        {
            // TODO: optimize, only do this when necessary
            var lookup = transformTreeBrushIndicesList[index];
            transformationCache[lookup.nodeOrder] = CompactHierarchyManager.GetNodeTransformation(in compactHierarchy, lookup.compactNodeID);
        }
    }
}
