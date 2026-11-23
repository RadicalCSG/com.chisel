﻿using System;
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
    // A tree's compact tree as its last build made it, and what it was built from: the hierarchy's structure version
    // (CompactHierarchy.StructureVersion) and the number of contents
    struct KeptCompactTree : IDisposable
    {
        public BlobAssetReference<CompactTree> tree;
        public ulong structureVersion;
        public int   contentsCount;
        public bool  built;

        public void Dispose()
        {
            if (tree.IsCreated)
                tree.Dispose();
            this = default;
        }
    }

    // The compact tree, kept from the last update while the hierarchy's structure and the number of contents are the same
    [BurstCompile(CompileSynchronously = true)]
    unsafe struct BuildCompactTreeJob : IJob
    {
        // Read
        public CompactNodeID treeCompactNodeID;
        public int           contentsCount;
        public bool          keepTree;
        public bool          verifyKeptTree;
        [NoAlias, ReadOnly] public NativeList<CompactNodeID> brushes;
        [NoAlias, ReadOnly] public NativeList<CompactNodeID> nodes;
        [NoAlias, ReadOnly] public CompactHierarchy.ReadOnly compactHierarchy;

        // Read / Write
        [NoAlias] public NativeReference<KeptCompactTree>    keptTree;
        // Only its own counters, which no other job writes, and atomically: the jobs before the CSG that write other
        // counters run beside it
        [NativeDisableContainerSafetyRestriction]
        [NoAlias] public NativeArray<int>                    stats;

        // Write
        [NoAlias, WriteOnly] public NativeReference<BlobAssetReference<CompactTree>> compactTreeRef;

        public void Execute()
        {
            var kept    = keptTree.Value;
            var version = compactHierarchy.StructureVersion;
            if (keepTree && kept.built && kept.structureVersion == version && kept.contentsCount == contentsCount)
            {
                Count(ExactCSGStat.CompactTreesKept);
                if (verifyKeptTree)
                {
                    var check = CompactTreeBuilder.Create(compactHierarchy, nodes.AsArray(), brushes.AsArray(), treeCompactNodeID, contentsCount);
                    if (!Same(kept.tree, check))
                        Count(ExactCSGStat.CompactTreeMismatch);
                    if (check.IsCreated)
                        check.Dispose();
                }
                compactTreeRef.Value = kept.tree;
                return;
            }

            kept.Dispose();
            kept = new KeptCompactTree
            {
                tree             = CompactTreeBuilder.Create(compactHierarchy, nodes.AsArray(), brushes.AsArray(), treeCompactNodeID, contentsCount),
                structureVersion = version,
                contentsCount    = contentsCount,
                built            = true
            };
            keptTree.Value       = kept;
            compactTreeRef.Value = kept.tree;
        }

        void Count(ExactCSGStat stat)
        {
            if (stats.IsCreated)
                System.Threading.Interlocked.Increment(ref ((int*)stats.GetUnsafePtr())[(int)stat]);
        }

        static bool Same(BlobAssetReference<CompactTree> aRef, BlobAssetReference<CompactTree> bRef)
        {
            if (aRef.IsCreated != bRef.IsCreated)
                return false;
            if (!aRef.IsCreated)
                return true;
            ref var a = ref aRef.Value;
            ref var b = ref bRef.Value;
            if (a.minBrushIDValue != b.minBrushIDValue || a.minNodeIDValue != b.minNodeIDValue || a.maxNodeIDValue != b.maxNodeIDValue ||
                a.compactHierarchy.Length != b.compactHierarchy.Length || a.brushAncestorLegend.Length != b.brushAncestorLegend.Length ||
                a.brushAncestors.Length != b.brushAncestors.Length || a.brushIDValueToAncestorLegend.Length != b.brushIDValueToAncestorLegend.Length ||
                a.brushIDValueToContents.Length != b.brushIDValueToContents.Length)
                return false;
            for (int i = 0; i < a.compactHierarchy.Length; i++)
            {
                var x = a.compactHierarchy[i];
                var y = b.compactHierarchy[i];
                if (x.Type != y.Type || x.Operation != y.Operation || x.CompactNodeID != y.CompactNodeID ||
                    x.childCount != y.childCount || x.childOffset != y.childOffset)
                    return false;
            }
            for (int i = 0; i < a.brushAncestorLegend.Length; i++)
                if (a.brushAncestorLegend[i].ancestorStartIDValue != b.brushAncestorLegend[i].ancestorStartIDValue ||
                    a.brushAncestorLegend[i].ancestorEndIDValue != b.brushAncestorLegend[i].ancestorEndIDValue)
                    return false;
            for (int i = 0; i < a.brushAncestors.Length; i++)
                if (a.brushAncestors[i] != b.brushAncestors[i])
                    return false;
            for (int i = 0; i < a.brushIDValueToAncestorLegend.Length; i++)
                if (a.brushIDValueToAncestorLegend[i] != b.brushIDValueToAncestorLegend[i])
                    return false;
            for (int i = 0; i < a.brushIDValueToContents.Length; i++)
            {
                var x = a.brushIDValueToContents[i];
                var y = b.brushIDValueToContents[i];
                if (UnsafeUtility.As<BrushContentsInfo, byte>(ref x) != UnsafeUtility.As<BrushContentsInfo, byte>(ref y))
                    return false;
            }
            return true;
        }
    }
}
