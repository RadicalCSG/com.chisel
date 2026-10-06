using System;

using Unity.Entities;

namespace Chisel.Core
{
    struct CompactHierarchyNode
    {
        // TODO: combine bits
        public CSGNodeType      Type;
        public CSGOperationType Operation;
        public CompactNodeID    CompactNodeID;
        public int              childCount;
        public int              childOffset;

        public override string ToString() { return $"({nameof(Type)}: {Type}, {nameof(childCount)}: {childCount}, {nameof(childOffset)}: {childOffset}, {nameof(Operation)}: {Operation}, {nameof(CompactNodeID)}: {CompactNodeID})"; }
    }
    
    struct BrushAncestorLegend
    {
        public int  ancestorStartIDValue;
        public int  ancestorEndIDValue;

        public override string ToString() { return $"({nameof(ancestorStartIDValue)}: {ancestorStartIDValue}, {nameof(ancestorEndIDValue)}: {ancestorEndIDValue})"; }
    }

    readonly struct BrushContentsInfo
    {
        const byte kCarving      = 0x80;
        const byte kIntersecting = 0x40;
        const byte kContentsMask = 0x3F;
        readonly byte value;

        public BrushContentsInfo(byte contents, bool carving, bool intersecting = false)
        {
            value = (byte)((contents & kContentsMask) | (carving ? kCarving : 0) | (intersecting ? kIntersecting : 0));
        }

        public byte Contents     { get { return (byte)(value & kContentsMask); } }
        public bool Carving      { get { return (value & kCarving) != 0; } }
        public bool Intersecting { get { return (value & kIntersecting) != 0; } }

        public override string ToString() { return $"({nameof(Contents)}: {Contents}, {nameof(Carving)}: {Carving}, {nameof(Intersecting)}: {Intersecting})"; }
    }

    struct CompactTree
    {
        public BlobArray<CompactHierarchyNode> compactHierarchy;
        public BlobArray<BrushAncestorLegend>  brushAncestorLegend;
        public BlobArray<int>                  brushAncestors;

        public int                             minBrushIDValue;
        public BlobArray<int>                  brushIDValueToAncestorLegend;
        // Indexed like brushIDValueToAncestorLegend
        public BlobArray<BrushContentsInfo>    brushIDValueToContents;
        public int                             minNodeIDValue;
        public int                             maxNodeIDValue;

        // A brush that isn't in this tree reads as Solid and not carving
        public BrushContentsInfo GetBrushContents(CompactNodeID brushCompactNodeID)
        {
            var index = brushCompactNodeID.slotIndex.index - minBrushIDValue;
            if (index < 0 || index >= brushIDValueToContents.Length)
                return default;
            return brushIDValueToContents[index];
        }
    }
}
