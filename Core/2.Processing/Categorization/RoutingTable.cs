using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;

namespace Chisel.Core
{
    struct RoutingTable
    {
        public BlobArray<CategoryRoutingRow> routingRows;
        public BlobArray<RoutingLookup>      routingLookups;
        public BlobArray<int>	             nodeIDToTableIndex;
        public int	                         nodeIDOffset;

        // What the table was built from (CreateRoutingTableJob): the tree's structure, which has every node's id, and every
        // node the brush touches with how it touches it, as (node id value << 2) | IntersectionType, in id order. A table
        // built from the same is the same.
        public ulong                         structureGeneration;
        public BlobArray<uint>               touched;
    }

    // The tree's structure as CreateRoutingTableJob reads it (CompactTree: every node's type, operation, id and children,
    // and every brush's contents), and a generation that goes up whenever it changes (TrackTreeStructureJob)
    struct TreeStructureMemory
    {
        public UnsafeList<CompactHierarchyNode> hierarchy;
        public UnsafeList<BrushContentsInfo>    contents;
        public int                              minBrushIDValue;
        public ulong                            generation;

        public static TreeStructureMemory Create(Allocator allocator)
        {
            return new TreeStructureMemory
            {
                hierarchy = new UnsafeList<CompactHierarchyNode>(64, allocator),
                contents  = new UnsafeList<BrushContentsInfo>(64, allocator)
            };
        }

        public void Dispose()
        {
            if (hierarchy.IsCreated) hierarchy.Dispose();
            if (contents.IsCreated)  contents.Dispose();
            this = default;
        }

        // Takes the tree's structure; a new generation when it is not the one taken last
        public void Track(ref CompactTree tree)
        {
            if (!Same(ref tree))
            {
                hierarchy.Clear();
                contents.Clear();
                for (int i = 0; i < tree.compactHierarchy.Length; i++)
                    hierarchy.Add(tree.compactHierarchy[i]);
                for (int i = 0; i < tree.brushIDValueToContents.Length; i++)
                    contents.Add(tree.brushIDValueToContents[i]);
                minBrushIDValue = tree.minBrushIDValue;
                generation++;
            }
        }

        bool Same(ref CompactTree tree)
        {
            if (generation == 0 || minBrushIDValue != tree.minBrushIDValue ||
                hierarchy.Length != tree.compactHierarchy.Length || contents.Length != tree.brushIDValueToContents.Length)
                return false;
            for (int i = 0; i < hierarchy.Length; i++)
            {
                var a = hierarchy[i];
                var b = tree.compactHierarchy[i];
                if (a.Type != b.Type || a.Operation != b.Operation || a.CompactNodeID != b.CompactNodeID ||
                    a.childCount != b.childCount || a.childOffset != b.childOffset)
                    return false;
            }
            for (int i = 0; i < contents.Length; i++)
            {
                var a = contents[i];
                var b = tree.brushIDValueToContents[i];
                if (UnsafeUtility.As<BrushContentsInfo, byte>(ref a) != UnsafeUtility.As<BrushContentsInfo, byte>(ref b))
                    return false;
            }
            return true;
        }
    }
}
