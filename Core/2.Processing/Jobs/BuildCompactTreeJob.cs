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
    unsafe struct BuildCompactTreeJob : IJob
    {
        // Read
        public CompactNodeID treeCompactNodeID;
        [NoAlias, ReadOnly] public NativeList<CompactNodeID> brushes;
        [NoAlias, ReadOnly] public NativeList<CompactNodeID> nodes;
        [NoAlias, ReadOnly] public CompactHierarchy.ReadOnly compactHierarchy;


        // Write
        [NoAlias, WriteOnly] public NativeReference<BlobAssetReference<CompactTree>> compactTreeRef;

        public void Execute()
        {
            compactTreeRef.Value = CompactTreeBuilder.Create(compactHierarchy, nodes.AsArray(), brushes.AsArray(), treeCompactNodeID);
        }
    }
}
