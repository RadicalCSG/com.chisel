using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using Debug = UnityEngine.Debug;

namespace Chisel.Core
{
    // Temporary workaround until we can switch to hashes
    public enum NodeStatusFlags : UInt16
    {
        None                        = 0,
        //NeedChildUpdate		    = 1,
        NeedPreviousSiblingsUpdate  = 2,

        BranchNeedsUpdate           = 4,
            
        TreeIsDisabled              = 1024,
        TreeNeedsUpdate             = 8,
        TreeMeshNeedsUpdate         = 16,

            
        ShapeModified               = 32,
        TransformationModified      = 64,
        HierarchyModified           = 128,
        OutlineModified             = 256,
        NeedAllTouchingUpdated      = 512,	// all brushes that touch this brush need to be updated,
        NeedFullUpdate              = ShapeModified | TransformationModified | OutlineModified | HierarchyModified,
        NeedCSGUpdate               = ShapeModified | TransformationModified | HierarchyModified,
        NeedUpdateDirectOnly        = TransformationModified | OutlineModified
    };
    
    [GenerateTestsForBurstCompatibility]
    public readonly struct CompactHierarchyID : IComparable<CompactHierarchyID>, IEquatable<CompactHierarchyID>
    {
        readonly static CompactHierarchyID kInvalid = default;
		public static CompactHierarchyID Invalid { [MethodImpl(MethodImplOptions.AggressiveInlining)] get { return kInvalid; } }

		public readonly SlotIndex slotIndex;
		internal CompactHierarchyID(SlotIndex slotIndex) { this.slotIndex = slotIndex; }

        [EditorBrowsable(EditorBrowsableState.Never), BurstDiscard]
        public override string ToString() { return $"(HierarchyID = {slotIndex.index}, Generation = {slotIndex.generation})"; }

        #region Comparison
        [EditorBrowsable(EditorBrowsableState.Never), MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator ==(CompactHierarchyID left, CompactHierarchyID right) { return left.slotIndex == right.slotIndex; }
        [EditorBrowsable(EditorBrowsableState.Never), MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator !=(CompactHierarchyID left, CompactHierarchyID right) { return left.slotIndex != right.slotIndex; }
        [EditorBrowsable(EditorBrowsableState.Never), MethodImpl(MethodImplOptions.AggressiveInlining)]
		public readonly override bool Equals(object obj) { if (obj is CompactHierarchyID id) return this == id; return false; }
        [EditorBrowsable(EditorBrowsableState.Never), MethodImpl(MethodImplOptions.AggressiveInlining)]
		public readonly override int GetHashCode() { return (int)Hash(); }
		[EditorBrowsable(EditorBrowsableState.Never), MethodImpl(MethodImplOptions.AggressiveInlining)]
		public readonly uint Hash() { return slotIndex.Hash(); }

		[EditorBrowsable(EditorBrowsableState.Never), MethodImpl(MethodImplOptions.AggressiveInlining)]
		public readonly int CompareTo(CompactHierarchyID other) { return slotIndex.CompareTo(other.slotIndex); }

        [EditorBrowsable(EditorBrowsableState.Never), MethodImpl(MethodImplOptions.AggressiveInlining)]
		public readonly bool Equals(CompactHierarchyID other) { return slotIndex == other.slotIndex; }
        #endregion
    }
    
    [GenerateTestsForBurstCompatibility]
    public readonly struct CompactNodeID : IComparable<CompactNodeID>, IEquatable<CompactNodeID>
    {
        readonly static CompactNodeID kInvalid = default;
		public static CompactNodeID Invalid { [MethodImpl(MethodImplOptions.AggressiveInlining)] get { return kInvalid; } }

		public readonly SlotIndex slotIndex;

		public readonly CompactHierarchyID hierarchyID;

        internal CompactNodeID(CompactHierarchyID hierarchyID, SlotIndex slotIndex) { this.hierarchyID = hierarchyID; this.slotIndex = slotIndex; }

		[EditorBrowsable(EditorBrowsableState.Never), BurstDiscard]
        public readonly override string ToString() { return $"{nameof(slotIndex)} = {slotIndex}, {nameof(hierarchyID)} = {hierarchyID}"; }

        #region Comparison
        [EditorBrowsable(EditorBrowsableState.Never), MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator ==(CompactNodeID left, CompactNodeID right) { return left.slotIndex == right.slotIndex && left.hierarchyID == right.hierarchyID; }
        [EditorBrowsable(EditorBrowsableState.Never), MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator !=(CompactNodeID left, CompactNodeID right) { return left.slotIndex != right.slotIndex || left.hierarchyID != right.hierarchyID; }
        [EditorBrowsable(EditorBrowsableState.Never), MethodImpl(MethodImplOptions.AggressiveInlining)]
		public readonly override bool Equals(object obj) { if (obj is CompactNodeID id) return this == id; return false; }

        [EditorBrowsable(EditorBrowsableState.Never), MethodImpl(MethodImplOptions.AggressiveInlining)]
		public readonly override int GetHashCode() { return (int)Hash(); }
		[EditorBrowsable(EditorBrowsableState.Never), MethodImpl(MethodImplOptions.AggressiveInlining)]
		public uint Hash() { unchecked { return (uint)math.hash(new uint2(slotIndex.Hash(), hierarchyID.Hash())); } }

		[EditorBrowsable(EditorBrowsableState.Never), MethodImpl(MethodImplOptions.AggressiveInlining)]
		public readonly int CompareTo(CompactNodeID other)
        {
            var diff = hierarchyID.CompareTo(other.hierarchyID);
            if (diff != 0) return diff;
            return slotIndex.CompareTo(other.slotIndex);
        }
        [EditorBrowsable(EditorBrowsableState.Never), MethodImpl(MethodImplOptions.AggressiveInlining)]
		public readonly bool Equals(CompactNodeID other) { return slotIndex == other.slotIndex && hierarchyID == other.hierarchyID; }
        #endregion
    }

    [GenerateTestsForBurstCompatibility]
	public struct CompactNode
    {
        public Int32                instanceID;

        public CSGOperationType     operation;
        public float4x4             transformation; // local (may include non-ChiselNode parent transformations)
        public NodeStatusFlags      flags;          // TODO: replace with using hashes to compare changes        
        
        public Int32                brushMeshHash;  // TODO: use hash of mesh as "ID"
        public MinMaxAABB           bounds;         // TODO: move this somewhere else, 1:1 relationship with brushMeshID

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int GetHashcode() { unchecked { return (int)this.Hash(); } }

        public override readonly string ToString() { return $"{nameof(brushMeshHash)} = {brushMeshHash}, {nameof(operation)} = {operation}, {nameof(instanceID)} = {instanceID}, {nameof(transformation)} = {transformation}"; }
    }

    [GenerateTestsForBurstCompatibility]
	internal struct CompactChildNode // TODO: rename
    {
		// TODO: probably need to split this up into multiple pieces, figure out how this will actually be used in practice first

		internal CompactNode    nodeInformation;
        public NodeID           nodeID;         // TODO: figure out how to get rid of this
        public CompactNodeID    compactNodeID;     
        public CompactNodeID    parentID;       // TODO: rewrite updating code to use index here instead of ID (removes indirection)
		public Int32            childCount;
        public Int32            childOffset;

        readonly static CompactChildNode kInvalid = default;
		public static CompactChildNode Invalid { [MethodImpl(MethodImplOptions.AggressiveInlining)] get { return kInvalid; } }

		public override readonly string ToString() { return $"{nameof(compactNodeID)} = {compactNodeID}, {nameof(parentID)} = {parentID}, {nameof(nodeInformation.instanceID)} = {nodeInformation.instanceID}, {nameof(childCount)} = {childCount}, {nameof(childOffset)} = {childOffset}, {nameof(nodeInformation.brushMeshHash)} = {nodeInformation.brushMeshHash}, {nameof(nodeInformation.operation)} = {nodeInformation.operation}, {nameof(nodeInformation.transformation)} = {nodeInformation.transformation}"; }
    }

    // TODO: make sure everything is covered in tests
    [GenerateTestsForBurstCompatibility]
	partial struct CompactHierarchy //: IDisposable
    {
        #region CreateHierarchy
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static CompactHierarchy CreateHierarchy(NodeID nodeID, Int32 instanceID, Allocator allocator)
        {
            return CreateHierarchy(CompactHierarchyID.Invalid, nodeID, instanceID, allocator);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static CompactHierarchy CreateHierarchy(NodeID nodeID, Allocator allocator)
        {
            return CreateHierarchy(CompactHierarchyID.Invalid, nodeID, 0, allocator);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static CompactHierarchy CreateHierarchy(CompactHierarchyID hierarchyID, NodeID nodeID, Int32 instanceID, Allocator allocator)
        {
            var compactHierarchy = new CompactHierarchy
            {
                brushMeshToBrush = new UnsafeParallelHashMap<int, CompactNodeID>(16384, allocator),
                compactNodes     = new UnsafeList<CompactChildNode>(1024, allocator),
				slotIndexMap     = SlotIndexMap.Create(allocator),
                HierarchyID      = hierarchyID,
                isCreated        = true
            };
            compactHierarchy.RootID = compactHierarchy.CreateNode(nodeID, new CompactNode
            {
                instanceID     = instanceID,
                operation      = CSGOperationType.Additive,
                transformation = float4x4.identity,
                brushMeshHash  = Int32.MaxValue
            });
            return compactHierarchy;
        }
        #endregion

        #region CreateBranch
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public CompactNodeID CreateBranch(NodeID nodeID, CSGOperationType operation = CSGOperationType.Additive, Int32 instanceID = 0) { return CreateBranch(nodeID, float4x4.identity, operation, instanceID); }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public CompactNodeID CreateBranch(NodeID nodeID, float4x4 transformation, CSGOperationType operation = CSGOperationType.Additive, Int32 instanceID = 0)
        {
            return CreateNode(nodeID, new CompactNode
            {
                instanceID          = instanceID,
                operation       = operation,
                transformation  = transformation,
                brushMeshHash   = Int32.MaxValue
            });
        }
        #endregion

        #region CreateBrush
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public CompactNodeID CreateBrush(NodeID nodeID, Int32 brushMeshID, CSGOperationType operation = CSGOperationType.Additive, Int32 instanceID = 0) { return CreateBrush(nodeID, brushMeshID, float4x4.identity, operation, instanceID); }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public CompactNodeID CreateBrush(NodeID nodeID, Int32 brushMeshID, float4x4 transformation, CSGOperationType operation = CSGOperationType.Additive, Int32 instanceID = 0)
        {
            return CreateNode(nodeID, new CompactNode
            {
                instanceID      = instanceID,
                operation       = operation,
                transformation  = transformation,
                brushMeshHash   = brushMeshID, 
                //bounds        = BrushMeshManager.CalculateBounds(brushMeshID, in transformation)
            });
        }
        #endregion

        [return: MarshalAs(UnmanagedType.U1)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly bool IsValidCompactNodeID(CompactNodeID compactNodeID)
        {
            if (!IsCreated)
				return false;
			if (!slotIndexMap.IsValidSlotIndex(compactNodeID.slotIndex, out var index))
                return false;
            if (compactNodes[index].compactNodeID != compactNodeID)
                return false;
            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
		public readonly int SiblingIndexOf(CompactNodeID compactNodeID)
        {
            Debug.Assert(IsCreated);
            var nodeIndex = HierarchyIndexOfInternal(compactNodeID);
            if (nodeIndex == -1)
                throw new ArgumentException(nameof(compactNodeID), $"{nameof(compactNodeID)} is an invalid node");

            var parentID = compactNodes[nodeIndex].parentID;
            if (parentID == CompactNodeID.Invalid)
                return -1;

            var parentIndex = HierarchyIndexOfInternal(parentID);
            Debug.Assert(parentIndex != -1);
            return SiblingIndexOfInternal(parentIndex, nodeIndex);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
		public readonly int SiblingIndexOf(CompactNodeID parent, CompactNodeID child)
        {
            Debug.Assert(IsCreated);
            if (parent == CompactNodeID.Invalid)
                return -1;

            var nodeIndex = HierarchyIndexOfInternal(child);
            if (nodeIndex == -1)
                return -1;

            var childParent = compactNodes[nodeIndex].parentID;
            if (childParent == CompactNodeID.Invalid ||
                childParent != parent)
                return -1;

            var parentIndex = HierarchyIndexOfInternal(childParent);
            Debug.Assert(parentIndex != -1);
            return SiblingIndexOfInternal(parentIndex, nodeIndex);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
		public readonly NodeID GetNodeID(CompactNodeID compactNodeID)
        {
            int nodeIndex;
            Debug.Assert(IsCreated);
            if (compactNodeID == CompactNodeID.Invalid)
            {
                Debug.LogError($"{nameof(compactNodeID)} is an invalid node");
                return NodeID.Invalid;
            }

            nodeIndex = HierarchyIndexOfInternal(compactNodeID);
            if (nodeIndex == -1)
                return NodeID.Invalid;

            if (nodeIndex < 0 || nodeIndex >= compactNodes.Length)
            {
                Debug.LogError($"{nameof(compactNodeID)} nodeIndex is out of range");
                return NodeID.Invalid;
            }

            return compactNodes[nodeIndex].nodeID;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly NodeID GetNodeIDNoErrors(CompactNodeID compactNodeID)
        {
            int nodeIndex;
            Debug.Assert(IsCreated);
            if (compactNodeID == CompactNodeID.Invalid)
                return NodeID.Invalid;

            nodeIndex = HierarchyIndexOfInternalNoErrors(compactNodeID);
            if (nodeIndex == -1)
                return NodeID.Invalid;

            if (nodeIndex < 0 || nodeIndex >= compactNodes.Length)
                return NodeID.Invalid;

            return compactNodes[nodeIndex].nodeID;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly CompactNodeID ParentOf(CompactNodeID compactNodeID)
        {
            Debug.Assert(IsCreated);
            if (compactNodeID == CompactNodeID.Invalid)
                throw new ArgumentException(nameof(compactNodeID), $"{nameof(compactNodeID)} is an invalid node");

            var nodeIndex = HierarchyIndexOfInternal(compactNodeID);
            if (nodeIndex == -1)
                return CompactNodeID.Invalid;

            return compactNodes[nodeIndex].parentID;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly int ChildCount(CompactNodeID compactNodeID)
        {
            Debug.Assert(IsCreated);
            var nodeIndex = HierarchyIndexOfInternal(compactNodeID);
            if (nodeIndex == -1)
                throw new ArgumentException(nameof(compactNodeID), $"{nameof(compactNodeID)} is invalid");

            return compactNodes[nodeIndex].childCount;
        }

        /// <summary>
        /// WARNING: The returned reference will become invalid after modifying the hierarchy!
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal ref CompactChildNode GetNodeRef(CompactNodeID compactNodeID) { return ref UnsafeGetNodeRefAtInternal(compactNodeID); }


        /// <summary>
        /// WARNING: The returned reference will become invalid after modifying the hierarchy!
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly ref CompactNode GetChildRef(CompactNodeID compactNodeID) { return ref UnsafeGetChildRefAtInternal(compactNodeID); }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public CompactNode GetChild(CompactNodeID compactNodeID) { return UnsafeGetChildRefAtInternal(compactNodeID); }

        /// <summary>
        /// WARNING: The returned reference will become invalid after modifying the hierarchy!
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ref CompactNode GetChildRefAt(CompactNodeID compactNodeID, int index) { return ref SafeGetChildRefAtInternal(compactNodeID, index); }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public CompactNode GetChildAt(CompactNodeID compactNodeID, int index) { return SafeGetChildRefAtInternal(compactNodeID, index); }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly CompactNodeID GetChildCompactNodeIDAt(CompactNodeID compactNodeID, int index) { return GetChildCompactNodeIDAtInternal(compactNodeID, index); }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [return: MarshalAs(UnmanagedType.U1)]
        public bool Compact()
        {
            Debug.Assert(IsCreated);
            return CompactInternal();
        }

        [return: MarshalAs(UnmanagedType.U1)]
        public bool Delete(CompactNodeID compactNodeID)
        {
            Debug.Assert(IsCreated);
            if (compactNodeID == RootID) // Cannot remove root
                return false;

            var nodeIndex = HierarchyIndexOfInternal(compactNodeID);
            if (nodeIndex == -1)
                throw new ArgumentException(nameof(compactNodeID), $"{nameof(compactNodeID)} is invalid");

            var parentID = compactNodes[nodeIndex].parentID;
            var parentIndex = HierarchyIndexOfInternal(parentID);
            if (parentIndex == -1)
            {
                // node doesn't have a parent, so it cannot be removed from its parent
                var childCount = ChildCount(compactNodeID);
                if (childCount > 0) DetachRangeInternal(nodeIndex, 0, childCount);
                FreeIndexRange(nodeIndex, 1);
                return true;
            }

            var index = SiblingIndexOfInternal(parentIndex, nodeIndex);
            return DeleteRangeInternal(parentIndex, index, range: 1, deleteChildren: false);
        }

        [return: MarshalAs(UnmanagedType.U1)]
        public bool DeleteRecursive(CompactNodeID compactNodeID)
        {
            if (compactNodeID == RootID) // Cannot remove root
                return false;

            var nodeIndex = HierarchyIndexOfInternal(compactNodeID);
            if (nodeIndex == -1)
                throw new ArgumentException(nameof(compactNodeID), $"{nameof(compactNodeID)} is invalid");

            var parentID    = compactNodes[nodeIndex].parentID;
            var parentIndex = HierarchyIndexOfInternal(parentID);
            if (parentIndex == -1)
            {
                // node doesn't have a parent, so it cannot be removed from its parent
                var childCount = ChildCount(compactNodeID);
                if (childCount > 0) DeleteRangeInternal(nodeIndex, 0, childCount, deleteChildren: true);
                FreeIndexRange(nodeIndex, 1);
                return true; 
            }

            var index = SiblingIndexOfInternal(parentIndex, nodeIndex);
            var result = DeleteRangeInternal(parentIndex, index, range: 1, deleteChildren: true);
            return result;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [return: MarshalAs(UnmanagedType.U1)]
        public bool DeleteChildFromParentAt(CompactNodeID parentID, int index)
        {
            Debug.Assert(IsCreated);
            var parentIndex = HierarchyIndexOfInternal(parentID);
            if (parentIndex < 0)
                throw new ArgumentException(nameof(parentID), $"{nameof(parentID)} is invalid");
            return DeleteRangeInternal(parentIndex, index, range: 1, deleteChildren: false);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [return: MarshalAs(UnmanagedType.U1)]
        public bool DeleteChildFromParentRecursiveAt(CompactNodeID parentID, int index)
        {
            Debug.Assert(IsCreated);
            var parentIndex = HierarchyIndexOfInternal(parentID);
            if (parentIndex < 0)
                throw new ArgumentException(nameof(parentID), $"{nameof(parentID)} is invalid");
            return DeleteRangeInternal(parentIndex, index, range: 1, deleteChildren: true);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [return: MarshalAs(UnmanagedType.U1)]
        public bool DeleteChildrenFromParentAt(CompactNodeID parentID, int index, int range)
        {
            Debug.Assert(IsCreated);
            var parentIndex = HierarchyIndexOfInternal(parentID);
            if (parentIndex < 0)
                throw new ArgumentException(nameof(parentID), $"{nameof(parentID)} is invalid");
            return DeleteRangeInternal(parentIndex, index, range, deleteChildren: false);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [return: MarshalAs(UnmanagedType.U1)]
        public bool DeleteChildrenFromParentRecursiveAt(CompactNodeID parentID, int index, int range)
        {
            Debug.Assert(IsCreated);
            var parentIndex = HierarchyIndexOfInternal(parentID);
            if (parentIndex < 0)
                throw new ArgumentException(nameof(parentID), $"{nameof(parentID)} is invalid");
            return DeleteRangeInternal(parentIndex, index, range, deleteChildren: true);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [return: MarshalAs(UnmanagedType.U1)]
        public bool Detach(CompactNodeID compactNodeID)
        {
            Debug.Assert(IsCreated);
            if (compactNodeID == RootID) // Cannot remove root
                return false;

            var nodeIndex = HierarchyIndexOfInternal(compactNodeID);
            if (nodeIndex == -1)
                throw new ArgumentException(nameof(compactNodeID), $"{nameof(compactNodeID)} is invalid");

            var parentID    = compactNodes[nodeIndex].parentID;
            var parentIndex = HierarchyIndexOfInternal(parentID);
            if (parentIndex == -1)
                return false; // node doesn't have a parent, so it cannot be removed from its parent

            var index       = SiblingIndexOfInternal(parentIndex, nodeIndex);
            return DetachRangeInternal(parentIndex, index, range: 1);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [return: MarshalAs(UnmanagedType.U1)]
        public bool DetachChildFromParentAt(CompactNodeID parentID, int index)
        {
            Debug.Assert(IsCreated);
            var parentIndex = HierarchyIndexOfInternal(parentID);
            if (parentIndex < 0)
                throw new ArgumentException(nameof(parentID), $"{nameof(parentID)} is invalid");
            return DetachRangeInternal(parentIndex, index, range: 1);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [return: MarshalAs(UnmanagedType.U1)]
        public bool DetachChildrenFromParentAt(CompactNodeID parentID, int index, int range)
        {
            Debug.Assert(IsCreated);
            var parentIndex = HierarchyIndexOfInternal(parentID);
            if (parentIndex < 0)
                throw new ArgumentException(nameof(parentID), $"{nameof(parentID)} is invalid");
            return DetachRangeInternal(parentIndex, index, range);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [return: MarshalAs(UnmanagedType.U1)]
        public bool DetachAllChildrenFromParent(CompactNodeID parentID)
        {
            Debug.Assert(IsCreated);
            var parentIndex = HierarchyIndexOfInternal(parentID);
            if (parentIndex < 0)
                throw new ArgumentException(nameof(parentID), $"{nameof(parentID)} is invalid");
            return DetachAllChildrenInternal(parentIndex);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [return: MarshalAs(UnmanagedType.U1)]
        public bool DestroyAllChildrenFromParent(CompactNodeID parentID)
        {
            Debug.Assert(IsCreated);
            var parentIndex = HierarchyIndexOfInternal(parentID);
            if (parentIndex < 0)
                throw new ArgumentException(nameof(parentID), $"{nameof(parentID)} is invalid");
            return DeleteAllChildrenInternal(parentIndex);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AttachToParent(CompactNodeID parentID, CompactNodeID compactNodeID)
        {
            AttachToParent(ref CompactHierarchyManager.HierarchyIDLookup, CompactHierarchyManager.HierarchyList, ref CompactHierarchyManager.NodeIDLookup, CompactHierarchyManager.Nodes, parentID, compactNodeID, ignoreBrushMeshHashes: true);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void AttachToParent(ref SlotIndexMap hierarchyIDLookup, NativeList<CompactHierarchy> hierarchies, ref SlotIndexMap nodeIDLookup, NativeList<CompactNodeID> nodes, CompactNodeID parentID, CompactNodeID compactNodeID, bool ignoreBrushMeshHashes = false)
        {
            Debug.Assert(IsCreated);
            var parentIndex = HierarchyIndexOfInternal(parentID);
            if (parentIndex == -1)
                throw new ArgumentException(nameof(parentID), $"{nameof(parentID)} is invalid");

            if (!IsValidCompactNodeID(compactNodeID))
                return;

            var parentHierarchy = compactNodes[parentIndex];
            var parentChildCount = parentHierarchy.childCount;
            AttachInternal(ref hierarchyIDLookup, hierarchies, ref nodeIDLookup, nodes, parentID, parentIndex, parentChildCount, compactNodeID, ignoreBrushMeshHashes);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [return: MarshalAs(UnmanagedType.U1)]
        public bool AttachToParentAt(CompactNodeID parentID, int index, CompactNodeID compactNodeID)
        {
            return AttachToParentAt(ref CompactHierarchyManager.HierarchyIDLookup, CompactHierarchyManager.HierarchyList, ref CompactHierarchyManager.NodeIDLookup, CompactHierarchyManager.Nodes, parentID, index, compactNodeID);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [return: MarshalAs(UnmanagedType.U1)]
        internal bool AttachToParentAt(ref SlotIndexMap hierarchyIDLookup, NativeList<CompactHierarchy> hierarchies, ref SlotIndexMap nodeIDLookup, NativeList<CompactNodeID> nodes, CompactNodeID parentID, int index, CompactNodeID compactNodeID)
        {
            Debug.Assert(IsCreated);
            if (index < 0)
            {
                Debug.LogError("The index must be positive");
                return false;
            }
            var parentIndex = HierarchyIndexOfInternal(parentID);
            if (parentIndex == -1)
                throw new ArgumentException(nameof(parentID), $"{nameof(parentID)} is invalid");

            if (!IsValidCompactNodeID(compactNodeID))
                return false;

            return AttachInternal(ref hierarchyIDLookup, hierarchies, ref nodeIDLookup, nodes, parentID, parentIndex, index, compactNodeID);
        }
    }
}
