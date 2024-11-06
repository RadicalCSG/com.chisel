using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Pool;

using Debug = UnityEngine.Debug;
using ReadOnlyAttribute = Unity.Collections.ReadOnlyAttribute;
using WriteOnlyAttribute = Unity.Collections.WriteOnlyAttribute;

namespace Chisel.Core
{
    [GenerateTestsForBurstCompatibility]
    public readonly struct NodeID : IComparable<NodeID>, IEquatable<NodeID>
    {
        public static readonly NodeID Invalid = default;

        public readonly SlotIndex slotIndex;
        internal NodeID(SlotIndex slotIndex) { this.slotIndex = slotIndex; }

        #region Overhead
        [EditorBrowsable(EditorBrowsableState.Never), BurstDiscard] public readonly override string ToString() { return $"NodeID = {slotIndex}"; }
        [EditorBrowsable(EditorBrowsableState.Never), MethodImpl(MethodImplOptions.AggressiveInlining)] public static bool operator ==(NodeID left, NodeID right) { return left.slotIndex == right.slotIndex; }
        [EditorBrowsable(EditorBrowsableState.Never), MethodImpl(MethodImplOptions.AggressiveInlining)] public static bool operator !=(NodeID left, NodeID right) { return left.slotIndex != right.slotIndex; }
        [EditorBrowsable(EditorBrowsableState.Never), MethodImpl(MethodImplOptions.AggressiveInlining)] public readonly override bool Equals(object obj) { if (obj is NodeID id) return this == id; return false; }
        [EditorBrowsable(EditorBrowsableState.Never), MethodImpl(MethodImplOptions.AggressiveInlining)] public readonly override int GetHashCode() { return slotIndex.GetHashCode(); }
        [EditorBrowsable(EditorBrowsableState.Never), MethodImpl(MethodImplOptions.AggressiveInlining)] public readonly int CompareTo(NodeID other) { return slotIndex.CompareTo(other.slotIndex); }
        [EditorBrowsable(EditorBrowsableState.Never), MethodImpl(MethodImplOptions.AggressiveInlining)] public readonly bool Equals(NodeID other) { return slotIndex == other.slotIndex; }
        #endregion
    }

    // TODO: unify/clean up error messages
    // TODO: use struct, make this non static somehow?
    // TODO: rename
    [GenerateTestsForBurstCompatibility]
    internal struct CompactHierarchyManagerInstance : IDisposable
    {
        public SlotIndexMap                 hierarchyIDLookup;
        public SlotIndexMap                 nodeIDLookup;
        public NativeList<CompactHierarchy> hierarchies;
        public NativeList<CompactNodeID>    nodes;
        public NativeList<CSGTree>          allTrees;
        public NativeList<CSGTree>          updatedTrees;
        public CompactHierarchyID           defaultHierarchyID;
		public BrushWireframeManager        brushOutlineManager; // TODO: move somewhere else

		public void Initialize()
        {
            Dispose();
            Allocator allocator = Allocator.Persistent;
			hierarchyIDLookup   = SlotIndexMap.Create(allocator);
            nodeIDLookup        = SlotIndexMap.Create(allocator);
            hierarchies         = new NativeList<CompactHierarchy>(allocator);
            nodes               = new NativeList<CompactNodeID>(allocator);
			allTrees            = new NativeList<CSGTree>(allocator);
            updatedTrees        = new NativeList<CSGTree>(allocator);
			brushOutlineManager = BrushWireframeManager.Create(allocator);
			defaultHierarchyID  = CreateHierarchy().HierarchyID;
        }
        
        public void Dispose()
        {
            Clear();
            if (hierarchyIDLookup.IsCreated) hierarchyIDLookup.Dispose(); hierarchyIDLookup = default;
            if (nodeIDLookup     .IsCreated) nodeIDLookup     .Dispose(); nodeIDLookup = default;
            if (hierarchies      .IsCreated) hierarchies      .Dispose(); hierarchies = default;
            if (nodes            .IsCreated) nodes            .Dispose(); nodes = default;
			if (allTrees         .IsCreated) allTrees         .Dispose(); allTrees = default;
            if (updatedTrees     .IsCreated) updatedTrees     .Dispose(); updatedTrees = default;
            if (brushOutlineManager.IsCreated) brushOutlineManager.Dispose(); brushOutlineManager = default;
			defaultHierarchyID = CompactHierarchyID.Invalid;
		}


		// BrushOutlineManager
		//{{

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void FreeOutline(CompactNodeID compactNodeID)
        {
            brushOutlineManager.FreeOutline(compactNodeID);
		}

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly BlobAssetReference<NativeWireframeBlob> GetOutline(CompactNodeID compactNodeID)
        {
            return brushOutlineManager.GetOutline(compactNodeID);
        }

		//}}


		internal void Clear()
        {
            if (hierarchies.IsCreated)
            {
                var tempHierarchyList = ListPool< CompactHierarchy>.Get();
                for (int i = 0; i < hierarchies.Length; i++)
                    tempHierarchyList.Add(hierarchies[i]);

                try
                {
                    for (int i = 0; i < tempHierarchyList.Count; i++)
                    {
                        if (tempHierarchyList[i].IsCreated)
                        { 
                            try
                            {
                                // Note: calling dispose will remove it from hierarchies, 
                                // which is why we need to copy the list to dispose them all efficiently
                                tempHierarchyList[i].Dispose();
                                tempHierarchyList[i] = default;
                            } 
                            catch (Exception ex)
                            {
                                Debug.LogException(ex);
                            }
                        }
                    }
                }
                finally
                {
					ListPool<CompactHierarchy>.Release(tempHierarchyList);
				}
            }
            defaultHierarchyID = CompactHierarchyID.Invalid; 

            if (hierarchies.IsCreated) hierarchies.Clear();
            if (hierarchyIDLookup.IsCreated) hierarchyIDLookup.Clear();

            if (nodes.IsCreated) nodes.Clear();
			if (nodeIDLookup.IsCreated) nodeIDLookup.Clear();
        }

        #region CreateHierarchy
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ref CompactHierarchy CreateHierarchy(Int32 instanceID = 0)
        {
            var rootNodeID = CreateNodeID(out var rootNodeIndex);
            var hierarchyID = CreateHierarchyID(out var hierarchyIndex);
            var hierarchy = CompactHierarchy.CreateHierarchy(hierarchyID, rootNodeID, instanceID, Allocator.Persistent);
            if (hierarchies[hierarchyIndex].IsCreated)
            {
                hierarchies[hierarchyIndex].Dispose();
                hierarchies[hierarchyIndex] = default;
            }
            hierarchies[hierarchyIndex] = hierarchy;
            nodes[rootNodeIndex] = hierarchy.RootID;
			unsafe
            {
                return ref UnsafeUtility.AsRef<CompactHierarchy>(hierarchies.GetUnsafePtr() + hierarchyIndex);
            }
		}
        #endregion
        
        [return: MarshalAs(UnmanagedType.U1)]
        public bool CheckConsistency()
        {
            for (int i = 0; i < hierarchies.Length; i++)
            {
                if (!hierarchies[i].CheckConsistency())
                    return false;
            }
            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal CompactHierarchyID CreateHierarchyID(out int index)
        {
            index = hierarchyIDLookup.CreateSlotIndex(out var slotIndex);
            while (index >= hierarchies.Length)
                hierarchies.Add(default);
            return new CompactHierarchyID(slotIndex);
        }


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void FreeHierarchyID(CompactHierarchyID hierarchyID)
        {
            if (hierarchyID == CompactHierarchyID.Invalid)
                return;

            if (!hierarchyIDLookup.IsCreated)
                return;

            var index = hierarchyIDLookup.FreeSlotIndex(hierarchyID.slotIndex);
            if (index < 0 || index >= hierarchies.Length)
                return;

            // This method is called from Dispose, so shouldn't call dispose (would cause circular loop)
            //hierarchies[index].Dispose();
            hierarchies[index] = default;
        }


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal NodeID CreateNodeID(out int index)
        {
            index = nodeIDLookup.CreateSlotIndex(out var slotIndex);
            //Debug.Log($"CreateNodeID index:{index} id:{id} generation:{generation}");
            if (index >= nodes.Length)
                nodes.Resize(index + 1, NativeArrayOptions.ClearMemory);
            return new NodeID(slotIndex);
        }


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void FreeNodeID(NodeID nodeID)
        {
            if (nodeID == NodeID.Invalid)
                throw new ArgumentException($"The {nameof(NodeID)} {nameof(nodeID)} ({nodeID}) is invalid", nameof(nodeID));

            var index = nodeIDLookup.FreeSlotIndex(nodeID.slotIndex);
            if (index < 0 || index >= nodes.Length)
                throw new ArgumentException($"The {nameof(NodeID)} {nameof(nodeID)} ({nodeID}) is invalid", nameof(nodeID));

			FreeOutline(nodes[index]);
			nodes[index] = CompactNodeID.Invalid;
        }


        [return: MarshalAs(UnmanagedType.U1)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal readonly bool IsValidNodeID(NodeID nodeID, out int index)
        {
            index = -1;
            if (nodeID == NodeID.Invalid)
                return false;

            if (!nodeIDLookup.IsValidSlotIndex(nodeID.slotIndex, out index))
                return false;

            if (index < 0 || index >= nodes.Length)
            {
                index = -1;
                return false;
            }

            return true;
        }

        [return: MarshalAs(UnmanagedType.U1)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsValidNodeID(CSGTreeNode treeNode)
        {
            var nodeID = treeNode.nodeID;
            if (nodeID == NodeID.Invalid)
                return false;

            if (!nodeIDLookup.IsCreated ||
                !nodeIDLookup.IsValidSlotIndex(nodeID.slotIndex, out var index))
                return false;

            if (index < 0 || index >= nodes.Length)
                return false;

            var compactNodeID = nodes[index];
            if (!IsValidCompactNodeID(compactNodeID))
                return false;

            return true;
        }

        [return: MarshalAs(UnmanagedType.U1)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsValidNodeID(NodeID nodeID)
        {
            return IsValidNodeID(ref nodeIDLookup, ref hierarchyIDLookup, hierarchies, nodes, nodeID);
        }

        [return: MarshalAs(UnmanagedType.U1)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static bool IsValidNodeID(ref SlotIndexMap nodeIDLookup, ref SlotIndexMap hierarchyIDLookup, NativeList<CompactHierarchy> hierarchies, NativeList<CompactNodeID> nodes, NodeID nodeID)
        {
            if (nodeID == NodeID.Invalid)
                return false;

            if (!nodeIDLookup.IsCreated ||
                !nodeIDLookup.IsValidSlotIndex(nodeID.slotIndex, out var index))
                return false;

            if (index < 0 || index >= nodes.Length)
                return false;

            var compactNodeID = nodes[index];
            if (!IsValidCompactNodeID(ref hierarchyIDLookup, hierarchies, compactNodeID))
                return false;

            return true;
        }


        [return: MarshalAs(UnmanagedType.U1)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsValidCompactNodeID(CompactNodeID compactNodeID)
        {
            return IsValidCompactNodeID(ref hierarchyIDLookup, hierarchies, compactNodeID);
        }

        [return: MarshalAs(UnmanagedType.U1)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static bool IsValidCompactNodeID(ref SlotIndexMap hierarchyIDLookup, NativeList<CompactHierarchy> hierarchies, CompactNodeID compactNodeID)
        {
            if (compactNodeID == CompactNodeID.Invalid)
                return false;

            var hierarchyID = compactNodeID.hierarchyID;
            if (hierarchyID == CompactHierarchyID.Invalid)
                return false;
            
            if (!hierarchyIDLookup.IsValidSlotIndex(compactNodeID.hierarchyID.slotIndex, out var index))
                return false;

            if (index < 0 || index >= hierarchies.Length)
                return false;
             
            return hierarchies[index].IsValidCompactNodeID(compactNodeID);
        }
        
        public void GetAllTreeNodes(NativeList<CSGTreeNode> allNodes)
        {
            if (!allNodes.IsCreated)
                return;
            allNodes.Clear();

            var hierarchyCount = hierarchies.Length;
            if (hierarchyCount == 0)
                return;

            if (allNodes.Capacity < hierarchyCount)
                allNodes.Capacity = hierarchyCount;

            using (var tempNodes = new NativeList<CSGTreeNode>(Allocator.Temp))
            {
                for (int i = 0; i < hierarchyCount; i++)
                {
                    if (!hierarchies[i].IsCreated)
                        continue;
                    tempNodes.Clear();
                    hierarchies[i].GetAllNodes(tempNodes);
                    allNodes.AddRange(tempNodes.AsArray());
                }
            }
        }

        public void GetAllTrees(NativeList<CSGTree> allTrees)
        {
            if (!allTrees.IsCreated)
                return;
            allTrees.Clear();

            var hierarchyCount = hierarchies.Length;
            if (hierarchyCount == 0)
                return;

            if (allTrees.Capacity < hierarchyCount)
                allTrees.Capacity = hierarchyCount;

            for (int i = 0; i < hierarchyCount; i++)
            {
                if (!hierarchies[i].IsCreated)
                    continue;
                var tree = CSGTree.Find(hierarchies[i].RootID);
                if (!tree.Valid)
                    continue;
                allTrees.Add(tree);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public BlobAssetReference<NativeWireframeBlob> GetOutline(NodeID nodeID)
        {
            if (!IsValidNodeID(nodeID))
                throw new ArgumentException($"The {nameof(NodeID)} {nameof(nodeID)} ({nodeID}) is invalid", nameof(nodeID));

            var compactNodeID = GetCompactNodeID(nodeID);
            ref var hierarchy = ref GetHierarchy(compactNodeID);
			if (!hierarchy.IsValidCompactNodeID(compactNodeID))
				throw new ArgumentException($"The {nameof(CompactNodeID)} {nameof(compactNodeID)} ({compactNodeID}) is invalid", nameof(compactNodeID));

			return GetOutline(compactNodeID);
        }

		#region GetHierarchy
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public readonly CompactHierarchy.ReadOnly GetReadOnlyHierarchy(CompactNodeID compactNodeID)
		{
			if (compactNodeID == default)
				throw new ArgumentException($"{nameof(CompactNodeID)} ({compactNodeID}) is invalid.", nameof(compactNodeID));

			return GetReadOnlyHierarchy(compactNodeID.hierarchyID);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public readonly CompactHierarchy.ReadOnly GetReadOnlyHierarchy(CompactHierarchyID hierarchyID)
		{
			if (hierarchyID == CompactHierarchyID.Invalid)
				throw new ArgumentException($"{nameof(CompactHierarchyID)} ({hierarchyID}) is invalid.", nameof(hierarchyID));

			var hierarchyIndex = hierarchyIDLookup.GetIndex(hierarchyID.slotIndex);
			if (hierarchyIndex < 0 || hierarchyIndex >= hierarchies.Length)
				throw new ArgumentException($"{nameof(CompactHierarchyID)} ({hierarchyID}) with index {hierarchyIndex} has an invalid hierarchy (out of bounds [0...{hierarchies.Length}]), are you using an old reference?", nameof(hierarchyID));

            return new CompactHierarchy.ReadOnly(hierarchies, hierarchyIndex);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly ref CompactHierarchy GetHierarchy(CompactNodeID compactNodeID)
        {
            if (compactNodeID == default)
                throw new ArgumentException($"{nameof(CompactNodeID)} ({compactNodeID}) is invalid.", nameof(compactNodeID));

            return ref GetHierarchy(compactNodeID.hierarchyID);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly ref CompactHierarchy GetHierarchy(CompactHierarchyID hierarchyID)
		{
			if (hierarchyID == CompactHierarchyID.Invalid)
				throw new ArgumentException($"{nameof(CompactHierarchyID)} ({hierarchyID}) is invalid.", nameof(hierarchyID));

			var hierarchyIndex = hierarchyIDLookup.GetIndex(hierarchyID.slotIndex);
			if (hierarchyIndex < 0 || hierarchyIndex >= hierarchies.Length)
				throw new ArgumentException($"{nameof(CompactHierarchyID)} ({hierarchyID}) with index {hierarchyIndex} has an invalid hierarchy (out of bounds [0...{hierarchies.Length}]), are you using an old reference?", nameof(hierarchyID));
			unsafe
			{
				return ref UnsafeUtility.AsRef<CompactHierarchy>(hierarchies.GetUnsafePtr() + hierarchyIndex);
			}
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal readonly ref CompactHierarchy GetHierarchy(CompactHierarchyID hierarchyID, out int hierarchyIndex)
        {
            if (hierarchyID == CompactHierarchyID.Invalid)
                throw new ArgumentException($"{nameof(CompactHierarchyID)} ({hierarchyID}) is invalid.", nameof(hierarchyID));

            hierarchyIndex = hierarchyIDLookup.GetIndex(hierarchyID.slotIndex);
            if (hierarchyIndex < 0 || hierarchyIndex >= hierarchies.Length)
                throw new ArgumentException($"{nameof(CompactHierarchyID)} ({hierarchyID}) with index {hierarchyIndex} has an invalid hierarchy (out of bounds [0...{hierarchies.Length}]), are you using an old reference?", nameof(hierarchyID));
			unsafe
            {
                return ref UnsafeUtility.AsRef<CompactHierarchy>(hierarchies.GetUnsafePtr() + hierarchyIndex);
            }
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		internal readonly ref CompactHierarchy GetHierarchy(CompactNodeID compactNodeID, out int hierarchyIndex)
		{
			if (compactNodeID == default)
				throw new ArgumentException($"{nameof(CompactNodeID)} ({compactNodeID}) is invalid.", nameof(compactNodeID));

			return ref GetHierarchy(compactNodeID.hierarchyID, out hierarchyIndex);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		internal ref CompactHierarchy GetHierarchy(NodeID nodeID)
		{
			if (nodeID == NodeID.Invalid)
				throw new ArgumentException($"{nameof(NodeID)} is invalid.", nameof(nodeID));

			if (!IsValidNodeID(nodeID, out int index))
				throw new ArgumentException($"{nameof(NodeID)} ({nodeID}) is invalid, are you using an old reference?", nameof(nodeID));

			return ref GetHierarchy(nodes[index].hierarchyID);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ref CompactHierarchy GetHierarchy(CSGTree tree)
        {
            return ref GetHierarchy(tree.nodeID);
        }
        #endregion

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal int GetHierarchyIndex(NodeID nodeID)
        {
            if (nodeID == NodeID.Invalid)
                throw new ArgumentException($"{nameof(NodeID)} is invalid.", nameof(nodeID));

            if (!IsValidNodeID(nodeID, out int index))
                throw new ArgumentException($"{nameof(NodeID)} ({nodeID}) is invalid, are you using an old reference?", nameof(nodeID));

            var compactNodeID = nodes[index];
            if (!IsValidCompactNodeID(compactNodeID))
                throw new ArgumentException($"{nameof(CompactNodeID)} ({compactNodeID}) is invalid, are you using an old reference?", nameof(compactNodeID));

            var hierarchyID = compactNodeID.hierarchyID;
            if (hierarchyID == CompactHierarchyID.Invalid)
                throw new ArgumentException($"{nameof(CompactHierarchyID)} ({hierarchyID}) is invalid.", nameof(hierarchyID));

            var hierarchyIndex = hierarchyIDLookup.GetIndex(hierarchyID.slotIndex);
            if (hierarchyIndex < 0 || hierarchyIndex >= hierarchies.Length)
                throw new ArgumentException($"{nameof(CompactHierarchyID)} ({hierarchyID}) with index {hierarchyIndex} has an invalid hierarchy (out of bounds [0...{hierarchies.Length}]), are you using an old reference?", nameof(hierarchyID));

            return hierarchyIndex;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static int GetHierarchyIndexUnsafe(ref SlotIndexMap hierarchyIDLookup, CompactNodeID compactNodeID)
        {
            var hierarchyID = compactNodeID.hierarchyID;
            var hierarchyIndex = hierarchyIDLookup.GetIndex(hierarchyID.slotIndex);
            return hierarchyIndex;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal int GetHierarchyIndex(CompactNodeID compactNodeID)
        {
            if (!IsValidCompactNodeID(compactNodeID))
                throw new ArgumentException($"{nameof(CompactNodeID)} ({compactNodeID}) is invalid, are you using an old reference?", nameof(compactNodeID));

            var hierarchyID = compactNodeID.hierarchyID;
            if (hierarchyID == CompactHierarchyID.Invalid)
                throw new ArgumentException($"{nameof(CompactHierarchyID)} ({hierarchyID}) is invalid.", nameof(hierarchyID));

            var hierarchyIndex = hierarchyIDLookup.GetIndex(hierarchyID.slotIndex);
            if (hierarchyIndex < 0 || hierarchyIndex >= hierarchies.Length)
                throw new ArgumentException($"{nameof(CompactHierarchyID)} ({hierarchyID}) with index {hierarchyIndex} has an invalid hierarchy (out of bounds [0...{hierarchies.Length}]), are you using an old reference?", nameof(hierarchyID));

            return hierarchyIndex;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public CompactHierarchyID GetHierarchyID(NodeID nodeID)
        {
            if (nodeID == NodeID.Invalid)
                throw new ArgumentException($"{nameof(NodeID)} is invalid.", nameof(nodeID));

            if (!IsValidNodeID(nodeID, out int index))
                throw new ArgumentException($"{nameof(NodeID)} ({nodeID}) is invalid, are you using an old reference?", nameof(nodeID));

            var compactNodeID = nodes[index];
            if (!IsValidCompactNodeID(compactNodeID))
                throw new ArgumentException($"{nameof(CompactNodeID)} ({compactNodeID}) is invalid, are you using an old reference?", nameof(compactNodeID));

            return compactNodeID.hierarchyID;
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void MoveNodeID(NodeID nodeID, CompactNodeID compactNodeID)
        {
            MoveNodeID(ref nodeIDLookup, nodes, nodeID, compactNodeID);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void MoveNodeID(ref SlotIndexMap nodeIDLookup, NativeList<CompactNodeID> nodes, NodeID nodeID, CompactNodeID compactNodeID)
        {
            if (nodeID == NodeID.Invalid)
                throw new ArgumentException($"The {nameof(NodeID)} {nameof(nodeID)} ({nodeID}) is invalid", nameof(nodeID));

            if (!nodeIDLookup.IsValidSlotIndex(nodeID.slotIndex, out var index))
                throw new ArgumentException($"The {nameof(NodeID)} {nameof(nodeID)} ({nodeID}) is invalid", nameof(nodeID));

            nodes[index] = compactNodeID;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void MoveNodeIDs(ref SlotIndexMap nodeIDLookup, NativeList<CompactNodeID> nodes, UnsafeList<CompactChildNode> compactNodes, int offset, int count)
        {
            for (int i = offset, lastIndex = (offset + count); i < lastIndex; i++)
            {
                var nodeID          = compactNodes[i].nodeID;
                if (nodeID == NodeID.Invalid)
					//throw new ArgumentException($"The {nameof(NodeID)} {nameof(nodeID)} ({nodeID}) is invalid", nameof(nodeID));
					continue;

                if (!nodeIDLookup.IsValidSlotIndexUnsafe(nodeID.slotIndex, out var index))
					//throw new ArgumentException($"The {nameof(NodeID)} {nameof(nodeID)} ({nodeID}) is invalid", nameof(nodeID));
					continue;

                var compactNodeID = compactNodes[i].compactNodeID;
                nodes[index] = compactNodeID;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static CompactNodeID GetCompactNodeIDNoError(ref SlotIndexMap nodeIDLookup, NativeList<CompactNodeID> nodes, NodeID nodeID, out int index)
        {
            index = -1;
            if (nodeID == NodeID.Invalid)
                return CompactNodeID.Invalid;

            index = nodeIDLookup.GetIndex(nodeID.slotIndex);
            if (index < 0 || index >= nodes.Length)
                return CompactNodeID.Invalid;

            return nodes[index];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static CompactNodeID GetCompactNodeIDNoError(ref SlotIndexMap nodeIDLookup, NativeList<CompactNodeID> nodes, NodeID nodeID)
        {
            return GetCompactNodeIDNoError(ref nodeIDLookup, nodes, nodeID, out _);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public CompactNodeID GetCompactNodeIDNoError(NodeID nodeID)
        {
            return GetCompactNodeIDNoError(ref nodeIDLookup, nodes, nodeID, out _);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal CompactNodeID GetCompactNodeID(NodeID nodeID, out int index)
        {
            if (nodeID == NodeID.Invalid)
                throw new ArgumentException($"{nameof(NodeID)} is invalid.", nameof(nodeID));

            index = nodeIDLookup.GetIndex(nodeID.slotIndex);
            if (index < 0 || index >= nodes.Length)
                throw new ArgumentException($"{nameof(NodeID)} ({nodeID}) points to an invalid index, are you using an old reference?", nameof(nodeID));

            return nodes[index];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public CompactNodeID GetCompactNodeID(NodeID nodeID)
        {
            return GetCompactNodeID(nodeID, out _);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public CompactNodeID GetCompactNodeID(CSGTreeNode treeNode)
        {
            return GetCompactNodeID(treeNode.nodeID, out _);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal readonly IEnumerable<CompactNodeID> GetAllChildren(CompactHierarchy hierarchy, CompactNodeID compactNodeID)
        {
            yield return compactNodeID;
            var childCount = hierarchy.ChildCount(compactNodeID);
            if (childCount == 0)
                yield break;

            for (int i = 0; i < childCount; i++)
            {
                var childCompactNodeID = hierarchy.GetChildCompactNodeIDAt(compactNodeID, i);
                foreach (var item in GetAllChildren(hierarchy, childCompactNodeID))
                    yield return item;
            }
        }

        // TODO: Optimize
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal IEnumerable<CompactNodeID> GetAllChildren(CompactNodeID compactNodeID)
        {
            if (!IsValidCompactNodeID(compactNodeID))
                yield break;

            foreach (var item in GetAllChildren(GetHierarchy(compactNodeID), compactNodeID))
                yield return item;
        }


        [return: MarshalAs(UnmanagedType.U1)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal readonly bool IsValidHierarchyID(CompactHierarchyID hierarchyID, out int index)
        {
            index = -1;
            if (hierarchyID == CompactHierarchyID.Invalid)
                return false;

            if (!hierarchyIDLookup.IsValidSlotIndex(hierarchyID.slotIndex, out index))
                return false;

            if (index < 0 || index >= nodes.Length)
            {
                index = -1;
                return false;
            }

            return true;
        }

        [return: MarshalAs(UnmanagedType.U1)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly bool IsValidHierarchyID(CompactHierarchyID hierarchyID)
        {
            return IsValidHierarchyID(hierarchyID, out _);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public NodeID CreateTree(Int32 instanceID = 0)
        {
            if (defaultHierarchyID == CompactHierarchyID.Invalid)
                Initialize();
            ref var newHierarchy = ref CreateHierarchy(instanceID);
            return newHierarchy.GetNodeID(newHierarchy.RootID);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public NodeID CreateBranch(Int32 instanceID = 0) { return CreateBranch(CSGOperationType.Additive, instanceID); }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public NodeID CreateBranch(CSGOperationType operation, Int32 instanceID = 0)
        {
            if (defaultHierarchyID == CompactHierarchyID.Invalid)
                Initialize();
            var generatedBranchNodeID = CreateNodeID(out var index);
            nodes[index] = GetHierarchy(defaultHierarchyID).CreateBranch(generatedBranchNodeID, operation, instanceID);
            return generatedBranchNodeID;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public NodeID CreateBrush(BrushMeshInstance brushMesh, float4x4 localTransformation, CSGOperationType operation, Int32 instanceID = 0)
        {
            if (defaultHierarchyID == CompactHierarchyID.Invalid)
                Initialize();
            var generatedBrushNodeID = CreateNodeID(out var index);
            nodes[index] = GetHierarchy(defaultHierarchyID).CreateBrush(generatedBrushNodeID, brushMesh.brushMeshHash, localTransformation, operation, instanceID);
            return generatedBrushNodeID;
        }


        #region Dirty
        [return: MarshalAs(UnmanagedType.U1)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal bool IsNodeDirty(CompactNodeID compactNodeID)
        {
            if (!IsValidCompactNodeID(compactNodeID))
                return false;

            ref var hierarchy = ref GetHierarchy(compactNodeID);
            return hierarchy.IsNodeDirty(compactNodeID);
        }

        [return: MarshalAs(UnmanagedType.U1)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal bool IsNodeDirty(NodeID nodeID)
        {
            if (!IsValidNodeID(nodeID, out var index))
                return false;

            return IsNodeDirty(nodes[index]);
        }

        [return: MarshalAs(UnmanagedType.U1)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal bool SetChildrenDirty(CompactNodeID compactNodeID)
        {
            if (!IsValidCompactNodeID(compactNodeID))
                return false;

            ref var hierarchy = ref GetHierarchy(compactNodeID);
            return hierarchy.SetChildrenDirty(compactNodeID);
        }

        [return: MarshalAs(UnmanagedType.U1)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal bool SetDirty(CompactNodeID compactNodeID)
        {
            if (!IsValidCompactNodeID(compactNodeID))
                return false;

            ref var hierarchy = ref GetHierarchy(compactNodeID);
            return hierarchy.SetDirty(compactNodeID);
        }

        [return: MarshalAs(UnmanagedType.U1)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal bool SetDirty(NodeID nodeID)
        {
            if (!IsValidNodeID(nodeID, out var index))
                return false;

            return SetDirty(nodes[index]);
        }

        // Do not use. This method might be removed/renamed in the future
        [EditorBrowsable(EditorBrowsableState.Never)]
        [return: MarshalAs(UnmanagedType.U1)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool ClearDirty(CompactNodeID compactNodeID)
        {
            if (!IsValidCompactNodeID(compactNodeID))
                return false;

            ref var hierarchy = ref GetHierarchy(compactNodeID);
            return hierarchy.ClearDirty(compactNodeID);
        }

        // Do not use. This method might be removed/renamed in the future
        [EditorBrowsable(EditorBrowsableState.Never)]
        [return: MarshalAs(UnmanagedType.U1)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool ClearDirty(CSGTreeNode treeNode)
        {
            if (!IsValidNodeID(treeNode.nodeID, out var index))
                return false;

            return ClearDirty(nodes[index]);
        }
        #endregion

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal CSGNodeType GetTypeOfNode(NodeID nodeID)
        {
            if (!IsValidNodeID(nodeID, out var index))
                return CSGNodeType.None;

            var compactNodeID = nodes[index];
            if (!IsValidCompactNodeID(compactNodeID))
                return CSGNodeType.None;

            ref var hierarchy = ref GetHierarchy(compactNodeID);
            return hierarchy.GetTypeOfNode(compactNodeID);
        }

        public struct ReadOnlyInstanceIDLookup
		{
            [ReadOnly] SlotIndexMap                    hierarchyIDLookup;
			[ReadOnly] NativeList<CompactHierarchy> hierarchies;

			public ReadOnlyInstanceIDLookup(SlotIndexMap hierarchyIDLookup, NativeList<CompactHierarchy> hierarchies)
            {
                this.hierarchyIDLookup = hierarchyIDLookup;
                this.hierarchies = hierarchies;
			}

			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public readonly int GetHierarchyIndex(CompactNodeID compactNodeID)
			{
				if (compactNodeID == CompactNodeID.Invalid)
					return -1;

				var hierarchyID = compactNodeID.hierarchyID;
				if (hierarchyID == CompactHierarchyID.Invalid)
					return -1;

				var hierarchyIndex = hierarchyIDLookup.GetIndex(compactNodeID.hierarchyID.slotIndex);
				if (hierarchyIndex < 0 || hierarchyIndex >= hierarchies.Length)
					return -1;
				return hierarchyIndex;
			}

			[return: MarshalAs(UnmanagedType.U1)]
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public bool TryGetCompactNodeID(CompactNodeID compactNodeID, out int hierarchyIndex)
			{
                hierarchyIndex = GetHierarchyIndex(compactNodeID);
				if (hierarchyIndex == -1)
					return false;

				return hierarchies[hierarchyIndex].IsValidCompactNodeID(compactNodeID);
			}

			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public int SafeGetNodeInstanceID(CompactNodeID compactNodeID)
			{
				if (!TryGetCompactNodeID(compactNodeID, out int hierarchyIndex))
					return 0;

				return hierarchies[hierarchyIndex].GetNodeInstanceID(compactNodeID);
			}
		}

        public readonly ReadOnlyInstanceIDLookup GetReadOnlyInstanceIDLookup()
        {
            if (!hierarchyIDLookup.IsCreated) throw new NullReferenceException("hierarchyIDLookup");
			if (!hierarchies.IsCreated) throw new NullReferenceException("hierarchies");
			return new ReadOnlyInstanceIDLookup(hierarchyIDLookup, hierarchies);
		}



		public struct ReadWrite
		{
			SlotIndexMap hierarchyIDLookup;
			NativeList<CompactHierarchy> hierarchies;

			public ReadWrite(SlotIndexMap hierarchyIDLookup, NativeList<CompactHierarchy> hierarchies)
			{
				this.hierarchyIDLookup = hierarchyIDLookup;
				this.hierarchies = hierarchies;
			}

			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public readonly int GetHierarchyIndex(CompactNodeID compactNodeID)
			{
				if (compactNodeID == CompactNodeID.Invalid)
					return -1;

				var hierarchyID = compactNodeID.hierarchyID;
				if (hierarchyID == CompactHierarchyID.Invalid)
					return -1;

				var hierarchyIndex = hierarchyIDLookup.GetIndex(compactNodeID.hierarchyID.slotIndex);
				if (hierarchyIndex < 0 || hierarchyIndex >= hierarchies.Length)
					return -1;
				return hierarchyIndex;
			}

			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public readonly ref CompactHierarchy GetHierarchyByIndex(int hierarchyIndex)
			{
				unsafe
				{
					return ref UnsafeUtility.AsRef<CompactHierarchy>(hierarchies.GetUnsafePtr() + hierarchyIndex);
				}
			}
		}

		public readonly ReadWrite AsReadWrite()
		{
			if (!hierarchyIDLookup.IsCreated) throw new NullReferenceException("hierarchyIDLookup");
			if (!hierarchies.IsCreated) throw new NullReferenceException("hierarchies");
			return new ReadWrite(hierarchyIDLookup, hierarchies);
		}



		[MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int GetNodeInstanceID(NodeID nodeID)
        {
            if (!IsValidNodeID(nodeID, out var index))
                return 0;

            var compactNodeID = nodes[index];
            if (!IsValidCompactNodeID(compactNodeID))
                return 0;

            return GetHierarchy(compactNodeID).GetNodeInstanceID(compactNodeID);
        }

        #region Transformations
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal float4x4 GetNodeLocalTransformation(NodeID nodeID)
        {
            if (!IsValidNodeID(nodeID, out var index))
                throw new ArgumentException($"The {nameof(NodeID)} {nameof(nodeID)} ({nodeID}) is invalid", nameof(nodeID));

            var compactNodeID = nodes[index];
            if (!IsValidCompactNodeID(compactNodeID))
                throw new ArgumentException($"The {nameof(NodeID)} {nameof(nodeID)} ({nodeID}) is invalid", nameof(nodeID));

            return GetHierarchy(compactNodeID).GetChildRef(compactNodeID).transformation;
        }

        static readonly NodeTransformations kIdentityNodeTransformation = new NodeTransformations
        {
            nodeToTree = float4x4.identity,
            treeToNode = float4x4.identity
        };


        // TODO: Optimize this, doing A LOT of redundant work here
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static NodeTransformations GetNodeTransformation(in CompactHierarchy.ReadOnly hierarchy, CompactNodeID compactNodeID)
        {
            if (!hierarchy.IsValidCompactNodeID(compactNodeID))
                throw new ArgumentException($"The {nameof(CompactNodeID)} {nameof(compactNodeID)} ({compactNodeID}) is invalid", nameof(compactNodeID));

            if (compactNodeID == hierarchy.RootID)
                return kIdentityNodeTransformation;

            var localNodeToTree = hierarchy.GetChildTransformation(compactNodeID);
            var localTreeToNode = math.inverse(localNodeToTree);

            var parentCompactNodeID = hierarchy.ParentOf(compactNodeID);
            if (parentCompactNodeID == CompactNodeID.Invalid)
            {
                return new NodeTransformations
                {
                    nodeToTree = localNodeToTree,
                    treeToNode = localTreeToNode
                };
            }

            var parentTransformation = GetNodeTransformation(in hierarchy, parentCompactNodeID);
            return new NodeTransformations
            {
                nodeToTree = math.mul(parentTransformation.nodeToTree, localNodeToTree),
                treeToNode = math.mul(localTreeToNode, parentTransformation.treeToNode)
            };
        }

        // TODO: Optimize
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void FlagTransformationChangedDeep(ref CompactHierarchy hierarchy, CompactNodeID compactNodeID)
        {
            var childCount = hierarchy.ChildCount(compactNodeID);
            if (childCount == 0)
                return;

            for (int i = 0; i < childCount; i++)
            {
                var childCompactNodeID = hierarchy.GetChildCompactNodeIDAt(compactNodeID, i);
                ref var childNodeRef = ref hierarchy.GetChildRefAt(compactNodeID, i);
                if ((childNodeRef.flags & NodeStatusFlags.TransformationModified) == NodeStatusFlags.TransformationModified)
                    continue;
                
                childNodeRef.flags |= NodeStatusFlags.TransformationModified;
                FlagTransformationChangedDeep(ref hierarchy, childCompactNodeID);
            }
        }

        [return: MarshalAs(UnmanagedType.U1)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal bool SetNodeLocalTransformation(NodeID nodeID, in float4x4 transformation)
        {   
            if (!IsValidNodeID(nodeID, out var index))
                return false;

            var compactNodeID = nodes[index];
            if (!IsValidCompactNodeID(compactNodeID))
                return false;

            ref var hierarchy = ref GetHierarchy(compactNodeID);
            ref var nodeRef = ref hierarchy.GetChildRef(compactNodeID);
            if (math.any(nodeRef.transformation.c0 != transformation.c0) ||
                math.any(nodeRef.transformation.c1 != transformation.c1) ||
                math.any(nodeRef.transformation.c2 != transformation.c2) ||
                math.any(nodeRef.transformation.c3 != transformation.c3))
            {
                nodeRef.transformation = transformation;
                //nodeRef.bounds = BrushMeshManager.CalculateBounds(nodeRef.brushMeshHash, in nodeRef.transformation);

                // TODO: not sure why this test doesn't work?
                //if ((nodeRef.flags & NodeStatusFlags.TransformationModified) != NodeStatusFlags.TransformationModified)
                {
                    nodeRef.flags |= NodeStatusFlags.TransformationModified;
                    FlagTransformationChangedDeep(ref hierarchy, compactNodeID);
                }

                ref var rootNode = ref hierarchy.GetChildRef(hierarchy.RootID);
                rootNode.flags |= NodeStatusFlags.TreeNeedsUpdate;
            }
            return true;
        }

        [return: MarshalAs(UnmanagedType.U1)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal bool GetTreeToNodeSpaceMatrix(NodeID nodeID, out float4x4 result)
        {
            result = float4x4.identity;
            if (!IsValidNodeID(nodeID, out var index))
                return false;

            var compactNodeID = nodes[index];
            if (!IsValidCompactNodeID(compactNodeID))
                return false;

            // TODO: Optimize
            var hierarchy = GetReadOnlyHierarchy(compactNodeID);
            var nodeTransformation = GetNodeTransformation(in hierarchy, compactNodeID);
            result = nodeTransformation.treeToNode;
            return true;
        }

        [return: MarshalAs(UnmanagedType.U1)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal bool GetNodeToTreeSpaceMatrix(NodeID nodeID, out float4x4 result)
        {
            result = float4x4.identity;
            if (!IsValidNodeID(nodeID, out var index))
                return false;

            var compactNodeID = nodes[index];
            if (!IsValidCompactNodeID(compactNodeID))
                return false;

            // TODO: Optimize
            var hierarchy = GetReadOnlyHierarchy(compactNodeID);
            var nodeTransformation = GetNodeTransformation(in hierarchy, compactNodeID);
            result = nodeTransformation.nodeToTree;
            return true;
        }
        #endregion

        #region BrushMeshID
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal Int32 GetBrushMeshID(NodeID nodeID)
        {
            if (!IsValidNodeID(nodeID, out var index))
                throw new ArgumentException($"The {nameof(NodeID)} {nameof(nodeID)} ({nodeID}) is invalid", nameof(nodeID));

            var compactNodeID = nodes[index];
            if (!IsValidCompactNodeID(compactNodeID))
                throw new ArgumentException($"The {nameof(NodeID)} {nameof(nodeID)} ({nodeID}) is invalid", nameof(nodeID));

            return GetHierarchy(compactNodeID).GetChildRef(compactNodeID).brushMeshHash;
        }

        [return: MarshalAs(UnmanagedType.U1)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal bool SetBrushMeshID(NodeID nodeID, Int32 brushMeshID)
        {
            if (!IsValidNodeID(nodeID, out var index))
                throw new ArgumentException($"The {nameof(NodeID)} {nameof(nodeID)} ({nodeID}) is invalid", nameof(nodeID));

            var compactNodeID = nodes[index];
            if (!IsValidCompactNodeID(compactNodeID))
                throw new ArgumentException($"The {nameof(NodeID)} {nameof(nodeID)} ({nodeID}) is invalid", nameof(nodeID));

            ref var hierarchy = ref GetHierarchy(compactNodeID);
            var result = hierarchy.SetBrushMeshID(compactNodeID, brushMeshID);
            if (result)
                hierarchy.SetTreeDirty();
            return result;
        }
        #endregion

        #region Operation
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal CSGOperationType GetNodeOperationType(NodeID nodeID)
        {
            if (!IsValidNodeID(nodeID, out var index))
                throw new ArgumentException($"{nameof(CompactNodeID)} ({nodeID}) is invalid, are you using an old reference?", nameof(nodeID));
            
            var compactNodeID = nodes[index];
            if (!IsValidCompactNodeID(compactNodeID))
                throw new ArgumentException($"{nameof(CompactNodeID)} ({compactNodeID}) is invalid, are you using an old reference?", nameof(nodeID));

            ref var nodeRef = ref GetHierarchy(compactNodeID).GetChildRef(compactNodeID);
            return nodeRef.operation;
        }

        [return: MarshalAs(UnmanagedType.U1)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal bool SetNodeOperationType(NodeID nodeID, CSGOperationType operation)
        {
            if (!IsValidNodeID(nodeID, out var index))
                throw new ArgumentException($"The {nameof(NodeID)} {nameof(nodeID)} ({nodeID}) is invalid", nameof(nodeID));

            var compactNodeID = nodes[index];
            if (!IsValidCompactNodeID(compactNodeID))
                throw new ArgumentException($"The {nameof(NodeID)} {nameof(nodeID)} ({nodeID}) is invalid", nameof(nodeID));

            ref var hierarchy = ref GetHierarchy(compactNodeID);
            ref var nodeRef = ref hierarchy.GetChildRef(compactNodeID);
            if (nodeRef.operation == operation)
                return false;

            nodeRef.operation = operation;
            if (nodeRef.brushMeshHash == Int32.MaxValue)
                nodeRef.flags |= NodeStatusFlags.BranchNeedsUpdate;
            else
                nodeRef.flags |= NodeStatusFlags.NeedFullUpdate;

            ref var rootNode = ref hierarchy.GetChildRef(hierarchy.RootID);
            rootNode.flags |= NodeStatusFlags.TreeNeedsUpdate;
            return true;
        }
        #endregion

        [return: MarshalAs(UnmanagedType.U1)]
        public bool DestroyNode(NodeID nodeID)
        {
            if (!IsValidNodeID(nodeID, out var index))
            {
                Debug.LogError($"The {nameof(NodeID)} {nameof(nodeID)} ({nodeID}) is invalid");
                return false;
            }

            var compactNodeID = nodes[index];
            if (!IsValidCompactNodeID(compactNodeID))
            {
                Debug.LogError($"The {nameof(NodeID)} {nameof(nodeID)} ({nodeID}) is invalid");
                return false;
            }

            ref var currHierarchy = ref GetHierarchy(compactNodeID, out int hierarchyIndex);
            if (currHierarchy.RootID == compactNodeID)
            {
                if (defaultHierarchyID == CompactHierarchyID.Invalid)
                    Initialize();

                // move children to default hierarchy
                ref var defaultHierarchy = ref GetHierarchy(defaultHierarchyID, out int defaultHierarchyIndex);

                Debug.Assert(hierarchyIndex != defaultHierarchyIndex, "hierarchyIndex != defaultHierarchyIndex");
                for (int c = 0, childCount = currHierarchy.ChildCount(compactNodeID); c < childCount; c++)
                {
                    var child = currHierarchy.GetChildCompactNodeIDAtInternal(compactNodeID, c);
                    MoveChildNode(child, ref currHierarchy, ref defaultHierarchy, true); 
                }

                hierarchies[hierarchyIndex] = default;
                currHierarchy.Dispose();
                currHierarchy = default;
                FreeNodeID(nodeID);
                return true;
            }

            var oldParent = GetParentOfNode(nodeID);
            if (oldParent != NodeID.Invalid)
                SetDirty(oldParent);
            SetDirty(currHierarchy.RootID);

            FreeNodeID(nodeID);

            return currHierarchy.Delete(compactNodeID);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public NodeID GetParentOfNode(NodeID nodeID)
        {
            if (!IsValidNodeID(nodeID, out var index))
                throw new ArgumentException($"The {nameof(NodeID)} {nameof(nodeID)} ({nodeID}) is invalid", nameof(nodeID));

            var compactNodeID = nodes[index];
            if (!IsValidCompactNodeID(compactNodeID))
                throw new ArgumentException($"The {nameof(NodeID)} {nameof(nodeID)} ({nodeID}) is invalid", nameof(nodeID));

            ref var hierarchy = ref GetHierarchy(compactNodeID);
            var parentCompactNodeID = hierarchy.ParentOf(compactNodeID);
            if (parentCompactNodeID == CompactNodeID.Invalid)
                return NodeID.Invalid;
            return hierarchy.GetNodeID(parentCompactNodeID);
        }

        internal CompactNodeID DeepMove(int nodeIndex, CompactNodeID destinationParentCompactNodeID, ref CompactHierarchy destinationHierarchy, ref CompactHierarchy sourceHierarchy, ref CompactChildNode sourceNode)
        {
            Debug.Assert(destinationHierarchy.IsCreated, "Hierarchy has not been initialized");
            var srcCompactNodeID    = sourceNode.compactNodeID;
            var nodeID              = sourceNode.nodeID;
			var newCompactNodeID    = destinationHierarchy.CreateNode(sourceNode.nodeID, ref nodeIndex, in sourceNode.nodeInformation, destinationParentCompactNodeID);
            var childCount          = sourceHierarchy.ChildCount(srcCompactNodeID);
            if (childCount > 0)
            {
                var offset = destinationHierarchy.AllocateChildCount(nodeIndex, childCount);
                for (int i = 0; i < childCount; i++)
                {
                    var srcChildID = sourceHierarchy.GetChildCompactNodeIDAt(srcCompactNodeID, i);
                    ref var srcChild = ref sourceHierarchy.GetNodeRef(srcChildID);
                    var childNodeID = srcChild.nodeID;
                    var newChildCompactID = DeepMove(offset + i, newCompactNodeID, ref destinationHierarchy, ref sourceHierarchy, ref srcChild);
                    if (childNodeID != NodeID.Invalid)
                    {
                        var childNodeindex = nodeIDLookup.GetIndex(childNodeID.slotIndex);
                        nodes[childNodeindex] = newChildCompactID;
					}
                }
            }

            if (nodeID != NodeID.Invalid)
            {
                var nodeindex = nodeIDLookup.GetIndex(nodeID.slotIndex);
                nodes[nodeindex] = newCompactNodeID;
            }
            return newCompactNodeID;
        }

        // unchecked
        internal CompactNodeID MoveChildNode(CompactNodeID compactNodeID, ref CompactHierarchy sourceHierarchy, ref CompactHierarchy destinationHierarchy, [MarshalAs(UnmanagedType.U1)] bool recursive = true)
        {
            Debug.Assert(sourceHierarchy.IsCreated, "Source hierarchy has not been initialized");
            Debug.Assert(destinationHierarchy.IsCreated, "Destination hierarchy has not been initialized");

            ref var sourceNode = ref sourceHierarchy.GetNodeRef(compactNodeID);
            if (recursive)
            {
                var result = DeepMove(Int32.MaxValue, CompactNodeID.Invalid, ref destinationHierarchy, ref sourceHierarchy, ref sourceNode);
                return result;
            }

            var result2 = destinationHierarchy.CreateNode(sourceNode.nodeID, sourceNode.nodeInformation);
            return result2;
        }

        // Move nodes from one hierarchy to another
        public CompactNodeID MoveChildNode(NodeID nodeID, CompactHierarchyID destinationParentID, [MarshalAs(UnmanagedType.U1)] bool recursive = true)
        {
            if (!IsValidNodeID(nodeID, out var index))
                throw new ArgumentException($"The {nameof(NodeID)} {nameof(nodeID)} ({nodeID}) is invalid", nameof(nodeID));

            var compactNodeID = nodes[index];
            if (!IsValidCompactNodeID(compactNodeID))
                throw new ArgumentException($"The {nameof(NodeID)} {nameof(nodeID)} ({nodeID}) is invalid", nameof(nodeID));

            if (destinationParentID == CompactHierarchyID.Invalid)
                throw new ArgumentException(nameof(destinationParentID));

            if (compactNodeID == CompactNodeID.Invalid)
                throw new ArgumentException($"The {nameof(NodeID)} {nameof(nodeID)} ({nodeID}) is invalid", nameof(nodeID));

            var itemHierarchyID = compactNodeID.hierarchyID;
            if (itemHierarchyID == CompactHierarchyID.Invalid)
                throw new ArgumentException($"The {nameof(NodeID)} {nameof(nodeID)} ({nodeID}) is invalid", nameof(nodeID));

            // nothing to do
            if (itemHierarchyID == destinationParentID)
                return compactNodeID;

            ref var newParentHierarchy = ref GetHierarchy(destinationParentID);
            ref var oldParentHierarchy = ref GetHierarchy(itemHierarchyID);

            // Create new copy of item in new hierarchy
            var newCompactNodeID = MoveChildNode(compactNodeID, ref oldParentHierarchy, ref newParentHierarchy, recursive);

            nodes[index] = newCompactNodeID;

			// Delete item in old hierarchy
			oldParentHierarchy.DeleteRecursive(compactNodeID);
            return newCompactNodeID;
        }

        [return: MarshalAs(UnmanagedType.U1)]
        public bool AddChildNode(NodeID parent, NodeID childNode)
        {
            if (parent == NodeID.Invalid)
            {
                Debug.LogError("Cannot add a child to a parent with an invalid node");
                return false;
            }
            if (childNode == NodeID.Invalid)
            {
                Debug.LogError("Cannot add invalid node as child");
                return false;
            }

            if (parent == childNode)
            {
                Debug.LogError("Cannot add self as child");
                return false;
            }

            if (!IsValidNodeID(parent, out var parentIndex))
            {
                Debug.LogError("!IsValidNodeID(parent, out var index)");
                return false;
            }

            var newParentCompactNodeID = nodes[parentIndex];
            if (!IsValidCompactNodeID(newParentCompactNodeID))
            {
                Debug.LogError("!IsValidCompactNodeID(newParentCompactNodeID)");
                return false;
            }

            if (!IsValidNodeID(childNode, out var childIndex))
            {
                Debug.LogError("!IsValidNodeID(childNode)");
                return false;
            }

            var childCompactNodeID = nodes[childIndex];
            if (!IsValidCompactNodeID(childCompactNodeID))
            {
                Debug.LogError("!IsValidCompactNodeID(childNode)");
                return false;
            }

            var newParentHierarchyID = newParentCompactNodeID.hierarchyID;
            if (newParentHierarchyID == CompactHierarchyID.Invalid)
            {
                Debug.LogError("newParentHierarchyID == CompactHierarchyID.Invalid");
                return false;
            }
             
            var currParentHierarchyID = childCompactNodeID.hierarchyID;
            if (currParentHierarchyID == CompactHierarchyID.Invalid)
            {
                Debug.LogError("currParentHierarchyID == CompactHierarchyID.Invalid");
                return false;
            }

            ref var oldParentHierarchy = ref GetHierarchy(currParentHierarchyID);
            ref var newParentHierarchy = ref GetHierarchy(newParentHierarchyID);
            if (childCompactNodeID == oldParentHierarchy.RootID ||
                childCompactNodeID == newParentHierarchy.RootID)
            {
                Debug.LogError("Cannot add a tree as a child");
                return false;
            }

            var oldParentNodeID = oldParentHierarchy.ParentOf(childCompactNodeID);
            if (currParentHierarchyID != newParentHierarchyID)
            {
                // Create new copy of item in new hierarchy
                var newCompactNodeID = MoveChildNode(childCompactNodeID, ref oldParentHierarchy, ref newParentHierarchy, true);

                nodes[childIndex] = newCompactNodeID;

                // Delete item in old hierarchy
                oldParentHierarchy.DeleteRecursive(childCompactNodeID);
                childCompactNodeID = newCompactNodeID;
                SetDirty(oldParentHierarchy.RootID);
            } else
            {
                if (oldParentNodeID == newParentCompactNodeID)
                {
                    if (oldParentHierarchy.SiblingIndexOf(childCompactNodeID) == oldParentHierarchy.ChildCount(newParentCompactNodeID) - 1)
                        return false;
                }
                
                if (oldParentHierarchy.GetTypeOfNode(childCompactNodeID) != CSGNodeType.Brush)
                {
                    // We cannot add a child to its own descendant (would create a loop)
                    if (oldParentHierarchy.IsDescendant(childCompactNodeID, newParentCompactNodeID))
                        return false;
                }
            }

            if (oldParentNodeID != CompactNodeID.Invalid)
                oldParentHierarchy.SetDirty(oldParentNodeID);

            newParentHierarchy.AttachToParent(ref hierarchyIDLookup, hierarchies, ref nodeIDLookup, nodes, newParentCompactNodeID, childCompactNodeID, ignoreBrushMeshHashes: true);

            SetDirty(parent);
            SetDirty(childNode);
            SetDirty(newParentHierarchy.RootID);
            return true;
        }

        [return: MarshalAs(UnmanagedType.U1)]
        internal bool InsertChildNodeRange(NodeID parent, int index, [ReadOnly] NativeArray<CSGTreeNode> array)
        {
            if (!IsValidNodeID(parent, out var parentNodeIndex))
                throw new ArgumentException($"The {nameof(NodeID)} {nameof(parent)} ({parent}) is invalid", nameof(parent));

            var parentCompactNodeID = nodes[parentNodeIndex];
            if (!IsValidCompactNodeID(parentCompactNodeID))
                throw new ArgumentException($"The {nameof(CompactNodeID)} {nameof(parent)} ({parentCompactNodeID}) is invalid", nameof(parent));

            var newParentHierarchyID = parentCompactNodeID.hierarchyID;
            if (newParentHierarchyID == CompactHierarchyID.Invalid)
            {
                Debug.LogError("newParentHierarchyID == CompactHierarchyID.Invalid");
                return false;
            }

            ref var newParentHierarchy = ref GetHierarchy(newParentHierarchyID);
            var count = newParentHierarchy.ChildCount(parentCompactNodeID);
            if (index < 0 || index > count)
            {
                if (count == 0)
                    Debug.LogError($"{nameof(index)} is invalid, its value '{index}' can only be 0 because since are no other children");
                else
                    Debug.LogError($"{nameof(index)} is invalid, its value '{index}' must be between 0 .. {count}");
                return false;
            }
            for (int i = 0, lastNodex = array.Length; i < lastNodex; i++)
            {
                var childNode = array[i].nodeID;

                if (!IsValidNodeID(childNode, out var childIndex))
                {
                    Debug.LogError("Cannot add an invalid child to a parent");
                    return false;
                }

                var childCompactNodeID = nodes[childIndex];
                if (!IsValidCompactNodeID(childCompactNodeID))
                {
                    Debug.LogError("!IsValidCompactNodeID(childNode)");
                    return false;
                }

                var currParentHierarchyID = childCompactNodeID.hierarchyID;
                if (currParentHierarchyID == CompactHierarchyID.Invalid)
                {
                    Debug.LogError("currParentHierarchyID == CompactHierarchyID.Invalid");
                    return false;
                }

                ref var oldParentHierarchy = ref GetHierarchy(currParentHierarchyID);
                if (childCompactNodeID == oldParentHierarchy.RootID ||
                    childCompactNodeID == newParentHierarchy.RootID)
                {
                    Debug.LogError("Cannot add a tree as a child");
                    return false;
                }

                if (childCompactNodeID == parentCompactNodeID)
                {
                    Debug.LogError("A node cannot be its own child");
                    return false;
                }

                if (currParentHierarchyID == newParentHierarchyID &&
                    oldParentHierarchy.GetTypeOfNode(childCompactNodeID) != CSGNodeType.Brush)
                {
                    // We cannot add a child to its own descendant (would create a loop)
                    if (oldParentHierarchy.IsDescendant(childCompactNodeID, parentCompactNodeID))
                    {
                        Debug.LogError("Cannot add a descendant as a child");
                        return false;
                    }
                }
            }
            
            for (int i = 0, lastNodex = array.Length; i < lastNodex; i++)
            {
                var childNode               = array[i].nodeID;
                var childCompactNodeID      = GetCompactNodeID(childNode, out var childIndex);
                var currParentHierarchyID   = childCompactNodeID.hierarchyID;
                ref var oldParentHierarchy  = ref GetHierarchy(currParentHierarchyID);

                var oldParentNodeID = GetParentOfNode(childNode);
                if (currParentHierarchyID != newParentHierarchyID)
                {
                    // Create new copy of item in new hierarchy
                    var newCompactNodeID = MoveChildNode(childCompactNodeID, ref oldParentHierarchy, ref newParentHierarchy, true);

                    nodes[childIndex] = newCompactNodeID;

                    // Delete item in old hierarchy
                    oldParentHierarchy.DeleteRecursive(childCompactNodeID);
                    childCompactNodeID = newCompactNodeID;
                    SetDirty(oldParentHierarchy.RootID);
                } else
                {
                    var oldParentCompactNodeID = GetCompactNodeIDNoError(oldParentNodeID);
                    if (oldParentCompactNodeID == parentCompactNodeID)
                    {
                        if (oldParentHierarchy.SiblingIndexOf(childCompactNodeID) == oldParentHierarchy.ChildCount(parentCompactNodeID) - 1)
                            continue;
                    }
                }


                if (oldParentNodeID != NodeID.Invalid)
                    SetDirty(oldParentNodeID);

                newParentHierarchy.AttachToParentAt(ref hierarchyIDLookup, hierarchies, ref nodeIDLookup, nodes, parentCompactNodeID, index + i, childCompactNodeID);
                SetDirty(childCompactNodeID);
            }
            SetDirty(parent);
            return true;
        }

        [return: MarshalAs(UnmanagedType.U1)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal bool InsertChildNode(NodeID parent, int index, NodeID item)
        {
            var treeNode = CSGTreeNode.Find(item);
            var nodeArray = new NativeArray<CSGTreeNode>(1, Allocator.Temp);
            nodeArray[0] = treeNode;
            using (nodeArray)
            {
                return InsertChildNodeRange(parent, index, nodeArray);
            }
        }

        [return: MarshalAs(UnmanagedType.U1)]
        internal bool SetChildNodes(NodeID parent, [ReadOnly] NativeArray<CSGTreeNode> array)
        {
            if (!IsValidNodeID(parent, out var newParentNodeIndex))
                throw new ArgumentException($"The {nameof(NodeID)} {nameof(parent)} ({parent}) is invalid", nameof(parent));

            var newParentCompactNodeID = nodes[newParentNodeIndex];
            if (!IsValidCompactNodeID(newParentCompactNodeID))
                throw new ArgumentException($"The {nameof(CompactNodeID)} {nameof(parent)} ({newParentCompactNodeID}) is invalid", nameof(parent));

            var newParentHierarchyID = newParentCompactNodeID.hierarchyID;
            if (newParentHierarchyID == CompactHierarchyID.Invalid)
                throw new ArgumentException($"The {nameof(CompactNodeID)} {nameof(parent)} ({newParentCompactNodeID}) is invalid", nameof(parent));

            ref var hierarchy = ref GetHierarchy(newParentCompactNodeID);
            hierarchy.DetachAllChildrenFromParent(newParentCompactNodeID);

            if (array.Length == 0)
                return true;

            ref var newParentHierarchy = ref GetHierarchy(newParentHierarchyID);

            using (var newChildren = new NativeList<CompactNodeID>(array.Length, Allocator.Temp))
            {
                newParentHierarchy.ReserveChildren(array.Length);

                using (var usedTreeNodes = new NativeParallelHashSet<CSGTreeNode>(array.Length, Allocator.Temp))
                {
                    for (int i = 0; i < array.Length; i++)
                    {
                        var treeNode = array[i];

                        if (!usedTreeNodes.Add(treeNode))
                        {
                            Debug.LogError("Have duplicate child");
                            return false;
                        }

                        var childNode = treeNode.nodeID;
                        if (childNode == NodeID.Invalid)
                        {
                            Debug.LogError("Cannot add invalid node as child");
                            return false;
                        }

                        if (parent == childNode)
                        {
                            Debug.LogError("Cannot add self as child");
                            return false;
                        }

                        if (!IsValidNodeID(childNode, out var childIndex))
                        {
                            Debug.LogError("!IsValidNodeID(childNode)");
                            return false;
                        }

                        var childCompactNodeID = nodes[childIndex];
                        if (!IsValidCompactNodeID(childCompactNodeID))
                        {
                            Debug.LogError("!IsValidCompactNodeID(childNode)");
                            return false;
                        }

                        var currParentHierarchyID = childCompactNodeID.hierarchyID;
                        if (currParentHierarchyID == CompactHierarchyID.Invalid)
                        {
                            Debug.LogError("currParentHierarchyID == CompactHierarchyID.Invalid");
                            return false;
                        }

                        ref var oldParentHierarchy = ref GetHierarchy(currParentHierarchyID);
                        if (childCompactNodeID == oldParentHierarchy.RootID ||
                            childCompactNodeID == newParentHierarchy.RootID)
                        {
                            Debug.LogError("Cannot add a tree as a child");
                            return false;
                        }

                        var oldParentNodeID = oldParentHierarchy.ParentOf(childCompactNodeID);
                        if (currParentHierarchyID != newParentHierarchyID)
                        {
                            // Create new copy of item in new hierarchy
                            var newCompactNodeID = MoveChildNode(childCompactNodeID, ref oldParentHierarchy, ref newParentHierarchy, true);

                            nodes[childIndex] = newCompactNodeID;

                            // Delete item in old hierarchy
                            oldParentHierarchy.DeleteRecursive(childCompactNodeID);
                            childCompactNodeID = newCompactNodeID;

                            SetDirty(oldParentHierarchy.RootID);
                        } else
                        {
                            if (oldParentHierarchy.GetTypeOfNode(childCompactNodeID) != CSGNodeType.Brush)
                            {
                                // We cannot add a child to its own descendant (would create a loop)
                                if (oldParentHierarchy.IsDescendant(childCompactNodeID, newParentCompactNodeID))
                                {
                                    Debug.LogError("Cannot add child to one of its ancestors (would create infinite loop)");
                                    return false;
                                }
                            }
                        }

                        if (oldParentNodeID != CompactNodeID.Invalid)
                        {
                            oldParentHierarchy.SetDirty(oldParentNodeID);
                        }

                        newChildren.AddNoResize(childCompactNodeID);

                        SetDirty(childNode);
                    }
                }

                if (newChildren.Length == 0)
                    return true;

                newParentHierarchy.SetChildrenUnchecked(ref hierarchyIDLookup, hierarchies, ref nodeIDLookup, nodes, newParentCompactNodeID, newChildren, ignoreBrushMeshHashes: true);
            }

            SetDirty(newParentHierarchy.RootID);
            SetDirty(parent);
            return true;
        }

        [return: MarshalAs(UnmanagedType.U1)]
        public bool RemoveChildNode(NodeID parent, NodeID item)
        {
            if (!IsValidNodeID(parent, out var parentNodeIndex))
            {
                Debug.LogError($"The {nameof(NodeID)} {nameof(parent)} ({parent}) is invalid");
                return false;
            }

            var parentCompactNodeID = nodes[parentNodeIndex];
            if (!IsValidCompactNodeID(parentCompactNodeID))
            {
                Debug.LogError($"The {nameof(CompactNodeID)} {nameof(parent)} ({parentCompactNodeID}) is invalid");
                return false;
            }

            if (!IsValidNodeID(item, out var itemNodeIndex))
            {
                Debug.LogError($"The {nameof(NodeID)} {nameof(item)} ({item}) is invalid");
                return false;
            }

            var itemCompactNodeID = nodes[itemNodeIndex];
            if (!IsValidCompactNodeID(itemCompactNodeID))
            {
                Debug.LogError($"The {nameof(CompactNodeID)} {nameof(item)} ({itemCompactNodeID}) is invalid");
                return false;
            }

            ref var hierarchy = ref GetHierarchy(parentCompactNodeID);
            var parentOfItem = hierarchy.ParentOf(itemCompactNodeID);
            if (parentOfItem != parentCompactNodeID)
                return false;

            if (parentOfItem == CompactNodeID.Invalid)
                return false;

            var result = hierarchy.Detach(itemCompactNodeID);
            if (result)
            {
                SetDirty(parent);
                SetDirty(itemCompactNodeID);
            }
            return result;
        }

        [return: MarshalAs(UnmanagedType.U1)]
        public bool RemoveChildNodeAt(NodeID parent, int index)
        {
            if (!IsValidNodeID(parent, out var parentNodeIndex))
                throw new ArgumentException($"The {nameof(NodeID)} {nameof(parent)} ({parent}) is invalid", nameof(parent));

            var parentCompactNodeID = nodes[parentNodeIndex];
            if (!IsValidCompactNodeID(parentCompactNodeID))
                throw new ArgumentException($"The {nameof(CompactNodeID)} {nameof(parent)} ({parentCompactNodeID}) is invalid", nameof(parent));

            ref var hierarchy = ref GetHierarchy(parentCompactNodeID);
            var count = hierarchy.ChildCount(parentCompactNodeID);
            if (index < 0 || index >= count)
            {
                Debug.LogError($"{nameof(index)} is invalid, its value '{index}' must be between 0 .. {count}");
                return false;
            }

            var childCompactNodeID  = hierarchy.GetChildCompactNodeIDAt(parentCompactNodeID, index);
            var result              = hierarchy.DetachChildFromParentAt(parentCompactNodeID, index);
            if (result)
            {
                SetDirty(parent);
                SetDirty(childCompactNodeID);
            }
            return result;
        }

        [return: MarshalAs(UnmanagedType.U1)]
        internal bool RemoveChildNodeRange(NodeID parent, int index, int range)
        {
            if (index < 0)
            {
                Debug.LogError($"{nameof(index)} must be positive");
                return false;
            }

            if (range < 0)
            {
                Debug.LogError($"{nameof(range)} must be positive");
                return false;
            }

            if (range == 0)
                return true;

            if (!IsValidNodeID(parent, out var parentNodeIndex))
                throw new ArgumentException($"The {nameof(NodeID)} {nameof(parent)} ({parent}) is invalid", nameof(parent));

            var parentCompactNodeID = nodes[parentNodeIndex];
            if (!IsValidCompactNodeID(parentCompactNodeID))
                throw new ArgumentException($"The {nameof(CompactNodeID)} {nameof(parent)} ({parentCompactNodeID}) is invalid", nameof(parent));

            ref var hierarchy = ref GetHierarchy(parentCompactNodeID);
            var count = hierarchy.ChildCount(parentCompactNodeID);
            if (index + range > count)
            {
                Debug.LogError($"{nameof(index)} + {nameof(range)} must be below or equal to {count}");
                return false;
            }

            var list = new NativeList<CompactNodeID>(count, Allocator.Temp);
            try
            {
                for (int i = index; i < index + range; i++)
                {
                    var childID = hierarchy.GetChildCompactNodeIDAtInternal(parentCompactNodeID, i);
                    list.Add(childID);
                }

                var result = hierarchy.DetachChildrenFromParentAt(parentCompactNodeID, index, range);
                if (result)
                {
                    SetDirty(parent);
                    for (int i = 0; i < list.Length; i++)
                        SetDirty(list[i]);
                }
                return result;
            }
            finally
            {
                list.Dispose();
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void ClearChildNodes(NodeID parent)
        {
            if (!IsValidNodeID(parent, out var parentNodeIndex))
                throw new ArgumentException($"The {nameof(NodeID)} {nameof(parent)} ({parent}) is invalid", nameof(parent));

            var parentCompactNodeID = nodes[parentNodeIndex];
            ref var hierarchy = ref GetHierarchy(parentCompactNodeID);
            if (hierarchy.ChildCount(parentCompactNodeID) == 0)
                return;

            SetChildrenDirty(parentCompactNodeID);
            hierarchy.DetachAllChildrenFromParent(parentCompactNodeID);
            SetDirty(parentCompactNodeID);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void DestroyChildNodes(CSGTreeNode treeNode)
        {
            var parent = treeNode.nodeID;
            if (!IsValidNodeID(parent, out var parentNodeIndex))
                throw new ArgumentException($"The {nameof(NodeID)} {nameof(parent)} ({parent}) is invalid", nameof(parent));

            var parentCompactNodeID = nodes[parentNodeIndex];
            ref var hierarchy = ref GetHierarchy(parentCompactNodeID);
            if (hierarchy.ChildCount(parentCompactNodeID) == 0)
                return;

            SetChildrenDirty(parentCompactNodeID);
            hierarchy.DestroyAllChildrenFromParent(parentCompactNodeID);
            SetDirty(parentCompactNodeID);
        }
    }

    // TODO: create "ReadOnlyCompact...Manager" wrapper can be used in jobs
    public partial class CompactHierarchyManager : IDisposable
    {
        static CompactHierarchyManagerInstance  instance;
        
        // Burst forces us to write unmaintable code
        internal static NativeList<CompactHierarchy> HierarchyList { get { return instance.hierarchies; } }
        internal static ref SlotIndexMap HierarchyIDLookup { get { return ref instance.hierarchyIDLookup; } }

		internal static ref BrushWireframeManager BrushOutlineManager { get { return ref instance.brushOutlineManager; } }

		// Burst forces us to write unmaintable code
		internal static NativeList<CompactNodeID> Nodes { get { return instance.nodes; } }
        internal static ref SlotIndexMap NodeIDLookup { get { return ref instance.nodeIDLookup; } }

		internal static CompactHierarchyManagerInstance.ReadOnlyInstanceIDLookup GetReadOnlyInstanceIDLookup()
		{
			return instance.GetReadOnlyInstanceIDLookup();
		}

		internal static CompactHierarchyManagerInstance.ReadWrite AsReadWrite()
		{
			return instance.AsReadWrite();
		}

		[UnityEditor.InitializeOnLoadMethod]
        [RuntimeInitializeOnLoadMethod]
        static void StaticInitialize()
        {
#if UNITY_EDITOR
            UnityEditor.AssemblyReloadEvents.beforeAssemblyReload -= OnBeforeAssemblyReload;
            UnityEditor.AssemblyReloadEvents.beforeAssemblyReload += OnBeforeAssemblyReload;
            UnityEditor.AssemblyReloadEvents.afterAssemblyReload -= OnAfterAssemblyReload;
            UnityEditor.AssemblyReloadEvents.afterAssemblyReload += OnAfterAssemblyReload;
#endif
        }
         
#if UNITY_EDITOR
        static void OnBeforeAssemblyReload() { instance.Dispose(); instance = default; }

        // TODO: need a runtime equivalent
        static void OnAfterAssemblyReload() { instance.Initialize(); }
#endif

        [return: MarshalAs(UnmanagedType.U1)]
        public static bool CheckConsistency() { return instance.CheckConsistency(); }

        public static void Clear() 
        { 
            instance.Clear();
            brushSelectableState.Clear();
        }

        public void Dispose()
        {
            var prevHierarchies = instance.hierarchies;
            instance.hierarchies = default;
            if (prevHierarchies.IsCreated)
			{
				for (int i = 0; i < prevHierarchies.Length; i++)
                {
                    var prevHierarchy = prevHierarchies[i];
					prevHierarchies[i] = default;
					prevHierarchy.Dispose();
				}
                prevHierarchies.Clear();
				prevHierarchies.Dispose();
			}
		}

		public static void Destroy()
		{
            instance.Dispose();
		}


		/// <summary>Updates all pending changes to all <see cref="Chisel.Core.CSGTree"/>s.</summary>
		/// <returns>True if any <see cref="Chisel.Core.CSGTree"/>s have been updated, false if no changes have been found.</returns>
		[return: MarshalAs(UnmanagedType.U1)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool Flush(FinishMeshUpdate finishMeshUpdates) 
        { 
            if (!UpdateAllTreeMeshes(finishMeshUpdates, out JobHandle handle)) 
                return false; 
            handle.Complete(); 
            return true; 
        }


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void GetAllTreeNodes(NativeList<CSGTreeNode> allNodes) { instance.GetAllTreeNodes(allNodes); }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void GetAllTrees(NativeList<CSGTree> allTrees) { instance.GetAllTrees(allTrees); }

        
        #region Inspector State
#if UNITY_EDITOR
        enum BrushVisibilityState
        {
            None            = 0,
            Visible         = 1,
            PickingEnabled  = 2,
            Selectable      = Visible | PickingEnabled
        }
        static readonly Dictionary<int, BrushVisibilityState> brushSelectableState = new();

        [EditorBrowsable(EditorBrowsableState.Never)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SetVisibility(NodeID nodeID, [MarshalAs(UnmanagedType.U1)] bool visible)
        {
            if (!IsValidNodeID(nodeID, out var index))
                return;

            var compactNodeID = instance.nodes[index];
            if (!IsValidCompactNodeID(compactNodeID))
                return;

            var instanceID = GetHierarchy(compactNodeID).GetChildRef(compactNodeID).instanceID;

            var state = (visible ? BrushVisibilityState.Visible : BrushVisibilityState.None);
            if (brushSelectableState.TryGetValue(instanceID, out var result))
                state |= (result & BrushVisibilityState.PickingEnabled);
            brushSelectableState[instanceID] = state;
        }

        [EditorBrowsable(EditorBrowsableState.Never)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SetPickingEnabled(NodeID nodeID, [MarshalAs(UnmanagedType.U1)] bool pickingEnabled)
        {
            if (!IsValidNodeID(nodeID, out var index))
                return;

            var compactNodeID = instance.nodes[index];
            if (!IsValidCompactNodeID(compactNodeID))
                return;

            var instanceID = GetHierarchy(compactNodeID).GetChildRef(compactNodeID).instanceID;

            var state = (pickingEnabled ? BrushVisibilityState.PickingEnabled : BrushVisibilityState.None);
            if (brushSelectableState.TryGetValue(instanceID, out var result))
                state |= (result & BrushVisibilityState.Visible);
            brushSelectableState[instanceID] = state;
        }

        [EditorBrowsable(EditorBrowsableState.Never)]
        [return: MarshalAs(UnmanagedType.U1)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsBrushVisible(NodeID nodeID)
        {
            if (!IsValidNodeID(nodeID, out var index))
                return false;

            var compactNodeID = instance.nodes[index];
            if (!IsValidCompactNodeID(compactNodeID))
                return false;

            var instanceID = GetHierarchy(compactNodeID).GetChildRef(compactNodeID).instanceID;
            if (!brushSelectableState.TryGetValue(instanceID, out var result))
                return false;

            return (result & BrushVisibilityState.Visible) == BrushVisibilityState.Visible;
        }

        [EditorBrowsable(EditorBrowsableState.Never)]
        [return: MarshalAs(UnmanagedType.U1)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsBrushPickingEnabled(NodeID nodeID)
        {
            if (!IsValidNodeID(nodeID, out var index))
                return false;

            var compactNodeID = instance.nodes[index];
            if (!IsValidCompactNodeID(compactNodeID))
                return false;

            var instanceID = GetHierarchy(compactNodeID).GetChildRef(compactNodeID).instanceID;
            if (!brushSelectableState.TryGetValue(instanceID, out var result))
                return false;

            return (result & BrushVisibilityState.PickingEnabled) != BrushVisibilityState.None;
        }

        [EditorBrowsable(EditorBrowsableState.Never)]
        [return: MarshalAs(UnmanagedType.U1)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsBrushSelectable(NodeID nodeID)
        {
            if (!IsValidNodeID(nodeID, out var index))
                return false;

            var compactNodeID = instance.nodes[index];
            if (!IsValidCompactNodeID(compactNodeID))
                return false;

            var instanceID = GetHierarchy(compactNodeID).GetChildRef(compactNodeID).instanceID;
            if (!brushSelectableState.TryGetValue(instanceID, out var result))
                return false;

            return (result & BrushVisibilityState.Selectable) != BrushVisibilityState.None;
        }
#endif
        #endregion

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static BlobAssetReference<NativeWireframeBlob> GetOutline(NodeID nodeID) { return instance.GetOutline(nodeID); }

        #region GetHierarchy
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ref CompactHierarchy GetHierarchy(CompactHierarchyID hierarchyID) { return ref instance.GetHierarchy(hierarchyID); }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ref CompactHierarchy GetHierarchy(CSGTree tree) { return ref instance.GetHierarchy(tree); }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ref CompactHierarchy GetHierarchy(CompactNodeID compactNodeID) { return ref instance.GetHierarchy(compactNodeID); }

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static CompactHierarchy.ReadOnly GetReadOnlyHierarchy(CompactNodeID compactNodeID) { return instance.GetReadOnlyHierarchy(compactNodeID); }

		#endregion


		[MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static int GetHierarchyIndexUnsafe(ref SlotIndexMap hierarchyIDLookup, CompactNodeID compactNodeID) { return CompactHierarchyManagerInstance.GetHierarchyIndexUnsafe(ref hierarchyIDLookup, compactNodeID); }


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void FreeHierarchyID(CompactHierarchyID hierarchyID) { instance.FreeHierarchyID(hierarchyID); }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void MoveNodeID(ref SlotIndexMap nodeIDLookup, NativeList<CompactNodeID> nodes, NodeID nodeID, CompactNodeID compactNodeID) { CompactHierarchyManagerInstance.MoveNodeID(ref nodeIDLookup, nodes, nodeID, compactNodeID); }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void MoveNodeIDs(ref SlotIndexMap nodeIDLookup, NativeList<CompactNodeID> nodes, UnsafeList<CompactChildNode> compactNodes, int offset, int count) { CompactHierarchyManagerInstance.MoveNodeIDs(ref nodeIDLookup, nodes, compactNodes, offset, count); }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static CompactNodeID GetCompactNodeIDNoError(ref SlotIndexMap nodeIDLookup, NativeList<CompactNodeID> nodes, NodeID nodeID) { return CompactHierarchyManagerInstance.GetCompactNodeIDNoError(ref nodeIDLookup, nodes, nodeID); }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static CompactNodeID GetCompactNodeID(NodeID nodeID) { return instance.GetCompactNodeID(nodeID); }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static CompactNodeID GetCompactNodeID(CSGTreeNode treeNode) { return instance.GetCompactNodeID(treeNode); }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static IEnumerable<CompactNodeID> GetAllChildren(CompactNodeID compactNodeID) { return instance.GetAllChildren(compactNodeID); }

        [return: MarshalAs(UnmanagedType.U1)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static bool IsValidHierarchyID(CompactHierarchyID hierarchyID, out int index) { return instance.IsValidHierarchyID(hierarchyID, out index); }

        [return: MarshalAs(UnmanagedType.U1)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsValidHierarchyID(CompactHierarchyID hierarchyID) { return instance.IsValidHierarchyID(hierarchyID); }

        [return: MarshalAs(UnmanagedType.U1)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static bool IsValidNodeID(NodeID nodeID, out int index) { return instance.IsValidNodeID(nodeID, out index); }

        [return: MarshalAs(UnmanagedType.U1)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsValidNodeID(CSGTreeNode treeNode) { return instance.IsValidNodeID(treeNode); }

        [return: MarshalAs(UnmanagedType.U1)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsValidNodeID(NodeID nodeID) { return instance.IsValidNodeID(nodeID); }

        [return: MarshalAs(UnmanagedType.U1)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static bool IsValidNodeID(ref SlotIndexMap nodeIDLookup, ref SlotIndexMap hierarchyIDLookup, NativeList<CompactHierarchy> hierarchies, NativeList<CompactNodeID> nodes, NodeID nodeID) { return CompactHierarchyManagerInstance.IsValidNodeID(ref nodeIDLookup, ref hierarchyIDLookup, hierarchies, nodes, nodeID); }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static NodeID CreateTree(Int32 instanceID = 0) { return instance.CreateTree(instanceID); }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static NodeID CreateBranch(Int32 instanceID = 0) { return instance.CreateBranch(instanceID); }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static NodeID CreateBranch(CSGOperationType operation, Int32 instanceID = 0) { return instance.CreateBranch(operation, instanceID); }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static NodeID CreateBrush(BrushMeshInstance brushMesh, float4x4 localTransformation, CSGOperationType operation, Int32 instanceID = 0) { return instance.CreateBrush(brushMesh, localTransformation, operation, instanceID); }

        #region Dirty
        [return: MarshalAs(UnmanagedType.U1)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static bool IsNodeDirty(CompactNodeID compactNodeID) { return instance.IsNodeDirty(compactNodeID); }

        [return: MarshalAs(UnmanagedType.U1)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static bool IsNodeDirty(NodeID nodeID) { return instance.IsNodeDirty(nodeID); }

        [return: MarshalAs(UnmanagedType.U1)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static bool SetChildrenDirty(CompactNodeID compactNodeID) { return instance.SetChildrenDirty(compactNodeID); }

        [return: MarshalAs(UnmanagedType.U1)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static bool SetDirty(CompactNodeID compactNodeID) { return instance.SetDirty(compactNodeID); }

        [return: MarshalAs(UnmanagedType.U1)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static bool SetDirty(NodeID nodeID) { return instance.SetDirty(nodeID); }

        // Do not use. This method might be removed/renamed in the future
        [EditorBrowsable(EditorBrowsableState.Never)]
        [return: MarshalAs(UnmanagedType.U1)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool ClearDirty(CompactNodeID compactNodeID) { return instance.ClearDirty(compactNodeID); }

        // Do not use. This method might be removed/renamed in the future
        [EditorBrowsable(EditorBrowsableState.Never)]
        [return: MarshalAs(UnmanagedType.U1)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool ClearDirty(CSGTreeNode treeNode) { return instance.ClearDirty(treeNode); }
        #endregion

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static CSGNodeType GetTypeOfNode(NodeID nodeID) { return instance.GetTypeOfNode(nodeID); }
        
        [return: MarshalAs(UnmanagedType.U1)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsValidCompactNodeID(CompactNodeID compactNodeID) { return instance.IsValidCompactNodeID(compactNodeID); }

        [return: MarshalAs(UnmanagedType.U1)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static bool IsValidCompactNodeID(ref SlotIndexMap hierarchyIDLookup, NativeList<CompactHierarchy> hierarchies, CompactNodeID compactNodeID) { return CompactHierarchyManagerInstance.IsValidCompactNodeID(ref hierarchyIDLookup, hierarchies, compactNodeID); }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int GetNodeInstanceID(NodeID nodeID) { return instance.GetNodeInstanceID(nodeID); }

        #region Transformations
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static float4x4 GetNodeLocalTransformation(NodeID nodeID) { return instance.GetNodeLocalTransformation(nodeID); }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static NodeTransformations GetNodeTransformation(in CompactHierarchy.ReadOnly hierarchy, CompactNodeID compactNodeID) { return CompactHierarchyManagerInstance.GetNodeTransformation(in hierarchy, compactNodeID); }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void FlagTransformationChangedDeep(ref CompactHierarchy hierarchy, CompactNodeID compactNodeID) { CompactHierarchyManagerInstance.FlagTransformationChangedDeep(ref hierarchy, compactNodeID); }

        [return: MarshalAs(UnmanagedType.U1)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static bool SetNodeLocalTransformation(NodeID nodeID, in float4x4 result) { return instance.SetNodeLocalTransformation(nodeID, in result); }

        [return: MarshalAs(UnmanagedType.U1)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static bool GetTreeToNodeSpaceMatrix(NodeID nodeID, out float4x4 result) { return instance.GetTreeToNodeSpaceMatrix(nodeID, out result); }

        [return: MarshalAs(UnmanagedType.U1)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static bool GetNodeToTreeSpaceMatrix(NodeID nodeID, out float4x4 result) { return instance.GetNodeToTreeSpaceMatrix(nodeID, out result); }
        #endregion

        #region BrushMeshID
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static Int32 GetBrushMeshID(NodeID nodeID) { return instance.GetBrushMeshID(nodeID); }

        [return: MarshalAs(UnmanagedType.U1)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static bool SetBrushMeshID(NodeID nodeID, Int32 brushMeshID) { return instance.SetBrushMeshID(nodeID, brushMeshID); }
        #endregion

        #region Operation
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static CSGOperationType GetNodeOperationType(NodeID nodeID) { return instance.GetNodeOperationType(nodeID); }

        [return: MarshalAs(UnmanagedType.U1)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static bool SetNodeOperationType(NodeID nodeID, CSGOperationType operation) { return instance.SetNodeOperationType(nodeID, operation); }
        #endregion

        [return: MarshalAs(UnmanagedType.U1)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool DestroyNode(NodeID nodeID) { return instance.DestroyNode(nodeID); }

        // Move nodes from one hierarchy to another
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static CompactNodeID MoveChildNode(NodeID nodeID, CompactHierarchyID destinationParentID, [MarshalAs(UnmanagedType.U1)] bool recursive = true) { return instance.MoveChildNode(nodeID, destinationParentID, recursive); }

        [return: MarshalAs(UnmanagedType.U1)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool AddChildNode(NodeID parent, NodeID childNode) { return instance.AddChildNode(parent, childNode); }

        [return: MarshalAs(UnmanagedType.U1)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static bool InsertChildNodeRange(NodeID parent, int index, [ReadOnly] NativeArray<CSGTreeNode> array) { return instance.InsertChildNodeRange(parent, index, array); }

        [return: MarshalAs(UnmanagedType.U1)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static bool InsertChildNode(NodeID parent, int index, NodeID item) { return instance.InsertChildNode(parent, index, item); }

        [return: MarshalAs(UnmanagedType.U1)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static bool SetChildNodes(NodeID parent, [ReadOnly] NativeArray<CSGTreeNode> array) { return instance.SetChildNodes(parent, array); }

        [return: MarshalAs(UnmanagedType.U1)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool RemoveChildNode(NodeID parent, NodeID item) { return instance.RemoveChildNode(parent, item); }

        [return: MarshalAs(UnmanagedType.U1)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool RemoveChildNodeAt(NodeID parent, int index) { return instance.RemoveChildNodeAt(parent, index); }

        [return: MarshalAs(UnmanagedType.U1)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static bool RemoveChildNodeRange(NodeID parent, int index, int range) { return instance.RemoveChildNodeRange(parent, index, range); }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void ClearChildNodes(NodeID parent) { instance.ClearChildNodes(parent); }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void DestroyChildNodes(CSGTreeNode treeNode) { instance.DestroyChildNodes(treeNode); }
    }
}