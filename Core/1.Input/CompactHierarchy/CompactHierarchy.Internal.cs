using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using Debug = UnityEngine.Debug;
using ReadOnlyAttribute = Unity.Collections.ReadOnlyAttribute;
using WriteOnlyAttribute = Unity.Collections.WriteOnlyAttribute;

namespace Chisel.Core
{
    // TODO: Should be its own container with its own array pointers (fewer indirections)
    [GenerateTestsForBurstCompatibility]
    public partial struct CompactHierarchy : IDisposable
    {
        [NoAlias] UnsafeParallelHashMap<int, CompactNodeID> brushMeshToBrush;

        [NoAlias] UnsafeList<CompactChildNode> compactNodes;
        [NoAlias] SlotIndexMap                 slotIndexMap;
        bool isCreated;



		public struct ReadOnly
		{
			[NoAlias, ReadOnly] NativeList<CompactHierarchy> hierarchies;
			int hierarchyIndex;
			public ReadOnly(NativeList<CompactHierarchy> hierarchies, int hierarchyIndex)
            {
                this.hierarchies = hierarchies;
                this.hierarchyIndex = hierarchyIndex;
            }

			public readonly CompactNodeID RootID { [MethodImpl(MethodImplOptions.AggressiveInlining)] get { return hierarchies[hierarchyIndex].RootID; } }
			public readonly CompactHierarchyID HierarchyID { [MethodImpl(MethodImplOptions.AggressiveInlining)] get { return hierarchies[hierarchyIndex].HierarchyID; } }
			public readonly bool IsCreated { [MethodImpl(MethodImplOptions.AggressiveInlining)] get { return hierarchies.IsCreated && hierarchies[hierarchyIndex].isCreated; } }

            [return: MarshalAs(UnmanagedType.U1)]
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public readonly bool IsValidCompactNodeID(CompactNodeID compactNodeID) { return hierarchies[hierarchyIndex].IsValidCompactNodeID(compactNodeID); }
			
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
			internal readonly bool IsAnyStatusFlagSet(CompactNodeID compactNodeID) { return hierarchies[hierarchyIndex].IsAnyStatusFlagSet(compactNodeID); }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            internal void ClearStatusFlag(CompactNodeID compactNodeID, NodeStatusFlags flag) { hierarchies[hierarchyIndex].ClearStatusFlag(compactNodeID, flag); }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            internal void SetStatusFlag(CompactNodeID compactNodeID, NodeStatusFlags flag) { hierarchies[hierarchyIndex].SetStatusFlag(compactNodeID, flag); }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            internal readonly bool IsStatusFlagSet(CompactNodeID compactNodeID, NodeStatusFlags flag) { return hierarchies[hierarchyIndex].IsStatusFlagSet(compactNodeID, flag); }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            internal readonly Int32 GetBrushMeshID(CompactNodeID compactNodeID) { return hierarchies[hierarchyIndex].GetBrushMeshID(compactNodeID); }

			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public CompactNodeID ParentOf(CompactNodeID compactNodeID)
			{
				Debug.Assert(IsCreated);
				if (compactNodeID == CompactNodeID.Invalid)
					throw new ArgumentException(nameof(compactNodeID), $"{nameof(compactNodeID)} is an invalid node");

				var nodeIndex = hierarchies[hierarchyIndex].HierarchyIndexOfInternal(compactNodeID);
				if (nodeIndex == -1)
					return CompactNodeID.Invalid;

				return hierarchies[hierarchyIndex].compactNodes[nodeIndex].parentID;
			}

			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public readonly float4x4 GetChildTransformation(CompactNodeID compactNodeID) { return hierarchies[hierarchyIndex].UnsafeGetChildRefAtInternal(compactNodeID).transformation; }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public readonly int ChildCount(CompactNodeID compactNodeID) { return hierarchies[hierarchyIndex].ChildCount(compactNodeID); }

			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public readonly CompactNodeID GetChildCompactNodeIDAt(CompactNodeID compactNodeID, int index) { return hierarchies[hierarchyIndex].GetChildCompactNodeIDAt(compactNodeID, index); }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            internal CSGOperationType GetOperation(CompactNodeID compactNodeID) { return hierarchies[hierarchyIndex].GetOperation(compactNodeID); }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            internal CSGNodeType GetTypeOfNode(CompactNodeID compactNodeID) { return hierarchies[hierarchyIndex].GetTypeOfNode(compactNodeID); }
		}

		public CompactNodeID        RootID      { [MethodImpl(MethodImplOptions.AggressiveInlining)] readonly get; [MethodImpl(MethodImplOptions.AggressiveInlining)] internal set; }
        public CompactHierarchyID   HierarchyID { [MethodImpl(MethodImplOptions.AggressiveInlining)] readonly get; [MethodImpl(MethodImplOptions.AggressiveInlining)] private set; }


        public readonly bool IsCreated
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get { return isCreated; }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly bool CheckConsistency()
        {
            return CheckConsistency(ref CompactHierarchyManager.HierarchyIDLookup, CompactHierarchyManager.HierarchyList, ref CompactHierarchyManager.NodeIDLookup, CompactHierarchyManager.Nodes);
        }

        internal readonly bool CheckConsistency( ref SlotIndexMap hierarchyIDLookup, NativeList<CompactHierarchy> hierarchies, ref SlotIndexMap nodeIDLookup, NativeList<CompactNodeID> nodes, bool ignoreBrushMeshHashes = false)
        {
#if false
            if (HierarchyID == default)
            {
                return true;
            }

            if (!IsValidCompactNodeID(RootID))
            {
                Debug.LogError("!IsValidCompactNodeID(RootID)");
                return false;
            }

            if (!slotIndexMap.CheckConsistency())
                return false;

            var brushMeshKeyValueArrays = brushMeshToBrush.GetKeyValueArrays(Allocator.Temp);
            var brushMeshKeys           = brushMeshKeyValueArrays.Keys;
            var brushMeshValues         = brushMeshKeyValueArrays.Values;
            try
            {
                for (int i = 0; i < compactNodes.Length; i++)
                {
                    // If the node is not used, all its values should be set to default
                    if (compactNodes[i].compactNodeID == default)
                    {
                        if (compactNodes[i].nodeID != default ||
                            compactNodes[i].parentID != default ||
                            compactNodes[i].childCount != 0 ||
                            compactNodes[i].childOffset != 0 ||
                            (!ignoreBrushMeshHashes && compactNodes[i].nodeInformation.brushMeshHash != 0) ||
                            compactNodes[i].nodeInformation.instanceID != 0)
                        {
                            Debug.LogError($"{compactNodes[i].nodeID} != default ||\n{compactNodes[i].parentID} != default ||\n{compactNodes[i].childCount} != 0 ||\n{compactNodes[i].childOffset} != 0 ||\n{compactNodes[i].nodeInformation.brushMeshHash != 0} ||\n{compactNodes[i].nodeInformation.instanceID} != 0");
                            return false;
                        }
                        continue;
                    }

                    if (!IsValidCompactNodeID(compactNodes[i].compactNodeID))
                    {
                        Debug.LogError($"!IsValidCompactNodeID(compactNodes[{i}].compactNodeID)");
                        return false;
                    }

                    if (!CompactHierarchyManager.IsValidNodeID(ref nodeIDLookup, ref hierarchyIDLookup, hierarchies, nodes, compactNodes[i].nodeID))
                    {
                        Debug.LogError($"!CompactHierarchyManager.IsValidNodeID({compactNodes[i].nodeID})");
                        return false;
                    }

                    var foundCompactNodeID = CompactHierarchyManager.GetCompactNodeIDNoError(ref nodeIDLookup, nodes, compactNodes[i].nodeID);
                    if (foundCompactNodeID != compactNodes[i].compactNodeID)
                    {
                        Debug.LogError($"{foundCompactNodeID} != compactNodes[{i}].compactNodeID");
                        return false;
                    }

                    ref var hierarchy = ref CompactHierarchyManager.GetHierarchy(ref hierarchyIDLookup, hierarchies, compactNodes[i].compactNodeID.hierarchyID);
                    var foundNodeID = hierarchy.GetNodeID(compactNodes[i].compactNodeID);
                    if (foundNodeID != compactNodes[i].nodeID)
                    {
                        Debug.LogError($"{foundNodeID} != compactNodes[{i}].nodeID");
                        return false;
                    }

                    for (int c = 0; c < compactNodes[i].childCount; c++)
                    {
                        var childIndex = compactNodes[i].childOffset + c;
                        if (childIndex < 0 || childIndex >= compactNodes.Length)
                        {
                            Debug.LogError($"{childIndex} < 0 || {childIndex} >= {compactNodes.Length}");
                            return false;
                        }

                        if (!IsValidCompactNodeID(compactNodes[childIndex].compactNodeID))
                        {
                            Debug.LogError($"!IsValidCompactNodeID(compactNodes[{childIndex}].compactNodeID)");
                            return false;
                        }
                        if (!IsValidCompactNodeID(compactNodes[childIndex].parentID)) 
                        {
                            Debug.LogError($"!IsValidCompactNodeID(compactNodes[{childIndex}].parentID)");
                            return false;
                        }
                        if (compactNodes[childIndex].parentID != compactNodes[i].compactNodeID)
                        {
                            Debug.LogError($"compactNodes[{childIndex}].parentID != compactNodes[{i}].compactNodeID");
                            return false;
                        }
                    }

                    if (!ignoreBrushMeshHashes  // Workaround due to hierarchy being (partially) set up before brushMeshes are set by generators
                        && compactNodes[i].nodeInformation.brushMeshHash != Int32.MaxValue)
                    {
                        if (compactNodes[i].nodeInformation.brushMeshHash == BrushMeshInstance.InvalidInstance.BrushMeshID)
                        {
                            Debug.LogError($"compactNodes[{i}] does not have a valid brushMesh set");
                            return false;
                        } else
                        if (!brushMeshKeys.Contains(compactNodes[i].nodeInformation.brushMeshHash))
                        {
                            Debug.LogError($"!brushMeshKeys.Contains(compactNodes[{i}].nodeInformation.brushMeshID) {compactNodes[i].nodeInformation.brushMeshHash}");
                            return false;
                        }

                        if (!brushMeshValues.Contains(foundCompactNodeID))
                        {
                            Debug.LogError($"!brushMeshValues.Contains(compactNodeID) {foundCompactNodeID}");
                            return false;
                        }
                    }
                }

                for (int index = 0; index < slotIndexMap.IndexCount; index++)
                {
                    if (!slotIndexMap.IsValidIndex(index, out var value, out var generation))
                    {
                        if (compactNodes[index].compactNodeID != default)
                        {
                            Debug.LogError($"!slotIndexMap.IsValidIndex({index}, out var {value}, out var {generation}) && compactNodes[{index}].compactNodeID != default");
                            return false;
                        }
                        continue;
                    }

                    if (compactNodes[index].compactNodeID.value != value ||
                        compactNodes[index].compactNodeID.generation != generation)
                    {
                        Debug.LogError($"compactNodes[{index}].compactNodeID.value ({compactNodes[index].compactNodeID.value}) != {value} || compactNodes[{index}].compactNodeID.generation != {generation}  ({compactNodes[index].compactNodeID.generation})");
                        return false;
                    }

                    if (!slotIndexMap.IsValidID(value, generation, out var foundIndex))
                    {
                        Debug.LogError($"!slotIndexMap.IsValidID({value}, {generation}, out var {foundIndex})");
                        return false;
                    }

                    if (foundIndex != index)
                    {
                        Debug.LogError($"{foundIndex} != {index}");
                        return false;
                    }
                }

                for (int i = 0; i < brushMeshValues.Length; i++)
                {
                    var compactNodeID = brushMeshValues[i];
                    if (!slotIndexMap.IsValidID(compactNodeID.value, compactNodeID.generation, out var foundIndex))
                    {
                        Debug.LogError($"!slotIndexMap.IsValidID({compactNodeID.value}, {compactNodeID.generation}, out var {foundIndex})");
                        return false;
                    }
                }
            }
            finally
            {
                brushMeshKeyValueArrays.Dispose();
            }
#endif
			return true;
        }


        public void Dispose()
		{
            // Confirmed to be called
			isCreated = false;
            if (brushMeshToBrush.IsCreated) brushMeshToBrush.Dispose(); brushMeshToBrush = default;
            if (compactNodes.IsCreated) compactNodes.Dispose(); compactNodes = default;

            try
            {
                CompactHierarchyManager.FreeHierarchyID(HierarchyID);
            }
            finally
            {
                HierarchyID = CompactHierarchyID.Invalid;
                if (slotIndexMap.IsCreated) slotIndexMap.Dispose(); slotIndexMap = default;
            } 
        }

        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal CompactNodeID CreateNode(NodeID nodeID, CompactNode nodeInformation)
        {
            Debug.Assert(IsCreated, "Hierarchy has not been initialized");
            int index = Int32.MaxValue;
            return CreateNode(nodeID, ref index, in nodeInformation, CompactNodeID.Invalid);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal CompactNodeID CreateNode(NodeID nodeID, in CompactNode nodeInformation, CompactNodeID parentID)
        {
            Debug.Assert(IsCreated, "Hierarchy has not been initialized");
            int index = Int32.MaxValue;
            return CreateNode(nodeID, ref index, in nodeInformation, parentID);
        }

        internal CompactNodeID CreateNode(NodeID nodeID, ref int index, in CompactNode nodeInformation, CompactNodeID parentID)
        {
            Debug.Assert(IsCreated, "Hierarchy has not been initialized");
            SlotIndex slotIndex;
            if (index == Int32.MaxValue)
            {
                index = slotIndexMap.CreateSlotIndex(out slotIndex);
            } else
            {
                slotIndexMap.GetSlotIndex(index, out slotIndex);
                Debug.Assert(index >= compactNodes.Length || compactNodes[index].compactNodeID == default);
            }
            var compactNodeID = new CompactNodeID(hierarchyID: HierarchyID, slotIndex: slotIndex);
            if (index >= compactNodes.Length)
            {
                compactNodes .Resize(index + 1,
				    NativeArrayOptions.ClearMemory); 
                    //NativeArrayOptions.UninitializedMemory);
            }
            compactNodes[index] = new CompactChildNode
            {
                nodeInformation = nodeInformation,
                nodeID          = nodeID,
                compactNodeID   = compactNodeID,
				parentID        = parentID,
                childOffset     = 0,
                childCount      = 0
            };
            if (nodeInformation.brushMeshHash != 0 &&
                nodeInformation.brushMeshHash != Int32.MaxValue)
                brushMeshToBrush.Add(nodeInformation.brushMeshHash, compactNodeID);
            Debug.Assert(IsValidCompactNodeID(compactNodeID), "newly created ID is invalid");
            Debug.Assert(GetChildRef(compactNodeID).instanceID == nodeInformation.instanceID, "newly created ID is invalid");
            return compactNodeID;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void UpdateHash(CompactNodeID compactNodeID, int newBrushMeshHash)
        {
            UpdateHash(ref ChiselMeshLookup.Value.brushMeshBlobCache, compactNodeID, newBrushMeshHash);
        }

        internal bool UpdateHash([NoAlias] ref NativeParallelHashMap<int, RefCountedBrushMeshBlob> brushMeshBlobCache, CompactNodeID compactNodeID, int newBrushMeshHash)
        {
            var nodeIndex = HierarchyIndexOfInternal(compactNodeID);
            if (nodeIndex == -1)
                return false;

            var compactNode         = compactNodes[nodeIndex];
            var prevBrushMeshHash   = compactNode.nodeInformation.brushMeshHash;
            if (prevBrushMeshHash == newBrushMeshHash)
                return false;

            BrushMeshManager.RegisterBrushMeshHash(ref brushMeshBlobCache, newBrushMeshHash, prevBrushMeshHash);
            if (newBrushMeshHash != 0 && 
                newBrushMeshHash != Int32.MaxValue)
                brushMeshToBrush.Add(newBrushMeshHash, compactNodeID);

            if (compactNode.nodeInformation.brushMeshHash != newBrushMeshHash)
                compactNode.nodeInformation.brushMeshHash = newBrushMeshHash;
            compactNodes[nodeIndex] = compactNode;
            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal readonly Int32 GetBrushMeshID(CompactNodeID compactNodeID)
        {
            Debug.Assert(IsCreated);
            var index = HierarchyIndexOfInternal(compactNodeID);
            if (index < 0 || index >= compactNodes.Length)
                throw new ArgumentException($"The {nameof(CompactNodeID)} {nameof(compactNodeID)} ({compactNodeID}) is invalid", nameof(compactNodeID));

            return compactNodes[index].nodeInformation.brushMeshHash;
        }

        #region ID / Memory Management
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        void RemoveMeshReference(CompactNodeID compactNodeID, int brushMeshID)
        {
            if (brushMeshID == Int32.MaxValue)
                return;

            brushMeshToBrush.Remove(brushMeshID);//, compactNodeID.value);
            /*
            var value = compactNodeID.value;

            bool found = true;
            while (found)
            {
                found = false;
                if (brushMeshToBrush.TryGetFirstValue(brushMeshID, out var item, out var iterator))
                {
                    do
                    {
                        if (item.value == value)
                        {
                            found = true;
                            brushMeshToBrush.Remove(iterator);
                            break;
                        }
                    } while (brushMeshToBrush.TryGetNextValue(out item, ref iterator));
                }
            }*/
        }

        void FreeIndexRange(int index, int range)
        {
            if (index < 0 || index + range > compactNodes.Length)
                throw new ArgumentOutOfRangeException();

            for (int i = index, lastNode = index + range; i < lastNode; i++)
            {
                var compactNodeID   = compactNodes[i].compactNodeID;
                var brushMeshID     = compactNodes[i].nodeInformation.brushMeshHash;
                
                RemoveMeshReference(compactNodeID, brushMeshID);
                compactNodes[i] = default;
            }

            slotIndexMap.FreeIndexRange(index, range);
        }

        void RemoveIndexRange(int parentChildOffset, int parentChildCount, int index, int range)
        {
            if (index < 0 || index + range > compactNodes.Length)
                throw new ArgumentOutOfRangeException();

            for (int i = index, lastNode = index + range; i < lastNode; i++)
            {
                var compactNodeID   = compactNodes[i].compactNodeID;
                var brushMeshID     = compactNodes[i].nodeInformation.brushMeshHash;

                RemoveMeshReference(compactNodeID, brushMeshID);
                compactNodes[i] = default;
            }

            slotIndexMap.RemoveIndexRange(parentChildOffset, parentChildCount, index, range);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal int AllocateChildCount(int parentNodeIndex, int length)
        {
            if (length < 0)
                throw new ArgumentException(nameof(length));

            if (parentNodeIndex < 0 || parentNodeIndex >= compactNodes.Length)
                throw new ArgumentException($"{nameof(parentNodeIndex)} ({parentNodeIndex}) must be between 0 and {compactNodes.Length}", nameof(parentNodeIndex));

            var parentNode = compactNodes[parentNodeIndex];
            if (parentNode.childCount > 0)
                throw new ArgumentException($"{nameof(parentNodeIndex)} already has children", nameof(parentNodeIndex));

            parentNode.childOffset = slotIndexMap.AllocateIndexRange(length);
            parentNode.childCount = length;
            compactNodes[parentNodeIndex] = parentNode;
            return parentNode.childOffset;
        }
        #endregion


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
		readonly int HierarchyIndexOfInternal(CompactNodeID compactNodeID)
        {
            Debug.Assert(IsCreated);
            if (compactNodeID == CompactNodeID.Invalid)
                return -1;
            if (compactNodeID.hierarchyID != HierarchyID)
                return -1;
            return slotIndexMap.GetIndex(compactNodeID.slotIndex);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
		readonly int HierarchyIndexOfInternalNoErrors(CompactNodeID compactNodeID)
        {
            Debug.Assert(IsCreated);
            if (compactNodeID == CompactNodeID.Invalid)
                return -1;
            if (compactNodeID.hierarchyID != HierarchyID)
                return -1;
            return slotIndexMap.GetIndexNoErrors(compactNodeID.slotIndex);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
		readonly int UnsafeHierarchyIndexOfInternal(CompactNodeID compactNodeID)
        {
            return slotIndexMap.GetIndex(compactNodeID.slotIndex);
        }

        // WARNING: The returned reference will become invalid after modifying the hierarchy!
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
		readonly ref CompactChildNode UnsafeGetNodeRefAtInternal(CompactNodeID compactNodeID)
        {
            Debug.Assert(IsCreated, "Hierarchy has not been initialized");
            var nodeIndex       = UnsafeHierarchyIndexOfInternal(compactNodeID);
			unsafe
            {
                var compactNodesPtr = compactNodes.Ptr;
                return ref compactNodesPtr[nodeIndex];
            }
        }

        // WARNING: The returned reference will become invalid after modifying the hierarchy!
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        readonly ref CompactNode UnsafeGetChildRefAtInternal(CompactNodeID compactNodeID)
        {
            Debug.Assert(IsCreated, "Hierarchy has not been initialized");
            var nodeIndex       = UnsafeHierarchyIndexOfInternal(compactNodeID);
			unsafe
            {
                var compactNodesPtr = compactNodes.Ptr;
                return ref compactNodesPtr[nodeIndex].nodeInformation;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal readonly CompactNodeID GetChildIDAtInternalNoError(CompactNodeID parentD, int index)
        {
            Debug.Assert(IsCreated, "Hierarchy has not been initialized");
            var parentIndex         = UnsafeHierarchyIndexOfInternal(parentD);
            var parentHierarchy     = compactNodes[parentIndex];
            var parentChildOffset   = parentHierarchy.childOffset;
            var parentChildCount    = parentHierarchy.childCount;

            if (index < 0 || index >= parentChildCount)
                return CompactNodeID.Invalid;

            var nodeIndex = parentChildOffset + index;
            if (nodeIndex < 0 || nodeIndex >= compactNodes.Length)
                return CompactNodeID.Invalid;

			unsafe
            {
                var compactNodesPtr = compactNodes.Ptr;
                return compactNodesPtr[nodeIndex].compactNodeID;
            }
        }

        // WARNING: The returned reference will become invalid after modifying the hierarchy!
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly CompactNodeID GetChildCompactNodeIDAtInternal(CompactNodeID parentD, int index)
        {
            Debug.Assert(IsCreated, "Hierarchy has not been initialized");
            var parentIndex         = UnsafeHierarchyIndexOfInternal(parentD);
            var parentHierarchy     = compactNodes[parentIndex];
            var parentChildOffset   = parentHierarchy.childOffset;
            var parentChildCount    = parentHierarchy.childCount;

            if (index < 0 || index >= parentChildCount) throw new ArgumentOutOfRangeException(nameof(index));

            var nodeIndex = parentChildOffset + index;
            if (nodeIndex < 0 || nodeIndex >= compactNodes.Length) throw new ArgumentOutOfRangeException(nameof(nodeIndex));

			unsafe
            {
                var compactNodesPtr = compactNodes.Ptr;
                return compactNodesPtr[nodeIndex].compactNodeID;
            }
        }

        // WARNING: The returned reference will become invalid after modifying the hierarchy!
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal readonly CompactNodeID GetChildCompactNodeIDAtNoError(CompactNodeID parentD, int index)
        {
            Debug.Assert(IsCreated, "Hierarchy has not been initialized");
            var parentIndex = UnsafeHierarchyIndexOfInternal(parentD);
            var parentHierarchy = compactNodes[parentIndex];
            var parentChildOffset = parentHierarchy.childOffset;
            var parentChildCount = parentHierarchy.childCount;

            if (index < 0 || index >= parentChildCount)
                return CompactNodeID.Invalid;

            var nodeIndex = parentChildOffset + index;
            if (nodeIndex < 0 || nodeIndex >= compactNodes.Length)
                return CompactNodeID.Invalid;

			unsafe
            {
                var compactNodesPtr = compactNodes.Ptr;
                return compactNodesPtr[nodeIndex].compactNodeID;
            }
        }

        // WARNING: The returned reference will become invalid after modifying the hierarchy!
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        ref CompactNode SafeGetChildRefAtInternal(CompactNodeID parentD, int index)
        {
            Debug.Assert(IsCreated, "Hierarchy has not been initialized");
            var parentIndex         = UnsafeHierarchyIndexOfInternal(parentD);
            var parentHierarchy     = compactNodes[parentIndex];
            var parentChildOffset   = parentHierarchy.childOffset;
            var parentChildCount    = parentHierarchy.childCount;

            if (index < 0 || index >= parentChildCount) throw new ArgumentOutOfRangeException(nameof(index));

            var nodeIndex = parentChildOffset + index;
            if (nodeIndex < 0 || nodeIndex >= compactNodes.Length) throw new ArgumentOutOfRangeException(nameof(nodeIndex));

			unsafe
            {
                var compactNodesPtr = compactNodes.Ptr;
                return ref compactNodesPtr[nodeIndex].nodeInformation;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
		readonly int SiblingIndexOfInternal(int parentIndex, int nodeIndex)
        {
            var parentHierarchy     = compactNodes[parentIndex];
            var parentChildOffset   = parentHierarchy.childOffset;
            var parentChildCount    = parentHierarchy.childCount;

            var index = nodeIndex - parentChildOffset;
            Debug.Assert(index >= 0 && index < parentChildCount);

            return index;
        }

        bool DeleteRangeInternal(int parentIndex, int siblingIndex, int range, bool deleteChildren)
        {
            if (range == 0)
                return false;
            
            Debug.Assert(parentIndex >= 0 && parentIndex < compactNodes.Length);
            var parentHierarchy     = compactNodes[parentIndex];
            var parentChildOffset   = parentHierarchy.childOffset;
            var parentChildCount    = parentHierarchy.childCount;
            if (siblingIndex < 0 || siblingIndex + range > parentChildCount)
                throw new ArgumentOutOfRangeException(nameof(siblingIndex));

            if (deleteChildren)
            {
                // Delete all children of the nodes we're going to delete
                for (int i = parentChildOffset + siblingIndex, lastIndex = (parentChildOffset + siblingIndex + range); i < lastIndex; i++)
                {
                    var childHierarchy  = compactNodes[i];
                    var childCount      = childHierarchy.childCount;
                    DeleteRangeInternal(i, 0, childCount, deleteChildren: true);
                }
            } else
            {
                // Detach all children of the nodes we're going to delete
                for (int i = parentChildOffset + siblingIndex, lastIndex = (parentChildOffset + siblingIndex + range); i < lastIndex; i++)
                {
                    var childHierarchy  = compactNodes[i];
                    var childCount      = childHierarchy.childCount;
                    DetachRangeInternal(i, 0, childCount);
                }
            }
            

            var nodeIndex = siblingIndex + parentChildOffset;


            // Check if we're deleting from the front of the list of children
            if (siblingIndex == 0)
            {
                // Clear the ids of the children we're deleting
                FreeIndexRange(nodeIndex, range);

                for (int i = nodeIndex; i < nodeIndex + range; i++)
				{
					compactNodes[i]  = default;
                }

                // If the range is identical to the number of children, we're deleting all the children
                if (parentChildCount == range)
                {
                    parentHierarchy.childCount = 0;
                    parentHierarchy.childOffset = 0;
                    compactNodes[parentIndex] = parentHierarchy;
                } else
                // Otherwise, we can just move the start offset of the parents' children forward
                {
                    Debug.Assert(parentChildCount > range);
                    parentHierarchy.childCount -= range;
                    parentHierarchy.childOffset += range;
                    compactNodes[parentIndex] = parentHierarchy;
                }
                return true;
            } else
            // Check if we're deleting from the back of the list of children
            if (siblingIndex == parentChildCount - range)
            {
                // Clear the ids of the children we're deleting
                FreeIndexRange(nodeIndex, range);

                for (int i = nodeIndex; i < nodeIndex + range; i++)
				{
					compactNodes[i]  = default;
                }

                // In that case, we can just decrease the number of children in the list
                parentHierarchy.childCount -= range;
                compactNodes[parentIndex] = parentHierarchy;
                return true;
            }

            // If we get here, it means we're deleting children in the center of the list of children

            // Clear the ids of the children we're deleting
            RemoveIndexRange(parentChildOffset, parentChildCount, nodeIndex, range);

            // Move nodes behind our node on top of the node we're removing
            var count = parentChildCount - (siblingIndex + range);
            compactNodes.MemMove(nodeIndex, nodeIndex + range, count);
            
            for (int i = nodeIndex + count; i < parentChildOffset + parentChildCount; i++)
			{
				compactNodes[i] = default;
            }

            parentHierarchy.childCount -= range;
            compactNodes[parentIndex] = parentHierarchy;
            return true;
        }

        // "optimize" - remove holes, reorder them in hierarchy order
        bool CompactInternal()
        {
            // TODO: implement
            //       could just create a new hierarchy and insert everything in it in order, and replace the hierarchy with this one
            throw new NotImplementedException();
        }

        bool DetachAllChildrenInternal(int parentIndex)
        {
            Debug.Assert(parentIndex >= 0 && parentIndex < compactNodes.Length);
            var parentHierarchy     = compactNodes[parentIndex];
            var parentChildCount    = parentHierarchy.childCount;
            return DetachRangeInternal(parentIndex, 0, parentChildCount);
        }

        bool DeleteAllChildrenInternal(int parentIndex)
        {
            Debug.Assert(parentIndex >= 0 && parentIndex < compactNodes.Length);
            var parentHierarchy     = compactNodes[parentIndex];
            var parentChildCount    = parentHierarchy.childCount;
            return DeleteRangeInternal(parentIndex, 0, parentChildCount, true);
        }

        bool DetachRangeInternal(int parentIndex, int siblingIndex, int range)
        {
            if (range == 0)
                return false;

            Debug.Assert(parentIndex >= 0 && parentIndex < compactNodes.Length);
            var parentHierarchy     = compactNodes[parentIndex];
            var parentChildOffset   = parentHierarchy.childOffset;
            var parentChildCount    = parentHierarchy.childCount;
            var lastNodeIndex       = parentChildCount + parentChildOffset - 1;
            if (siblingIndex < 0 || siblingIndex + range > parentChildCount)
                throw new ArgumentOutOfRangeException(nameof(siblingIndex));

            var nodeIndex = siblingIndex + parentChildOffset;

            // Set the parents of our detached nodes to invalid
            unsafe
            {
                var compactNodesPtr = compactNodes.Ptr;
                for (int i = nodeIndex; i < nodeIndex + range; i++)
                {
                    compactNodesPtr[i].parentID = CompactNodeID.Invalid;
                }
            }

            // Check if we're detaching from the front of the list of children
            if (siblingIndex == 0)
            {
                // If the range is identical to the number of children, we're detaching all the children
                if (parentChildCount == range)
                {
                    parentHierarchy.childCount = 0;
                    parentHierarchy.childOffset = 0;
                    compactNodes[parentIndex] = parentHierarchy;
                } else
                // Otherwise, we can just move the start offset of the parents' children forward
                {
                    parentHierarchy.childCount -= range;
                    parentHierarchy.childOffset += range;
                    compactNodes[parentIndex] = parentHierarchy;
                }
                return true;
            } else
            // Check if we're detaching from the back of the list of children
            if (siblingIndex == parentChildCount - range)
            {
                // In that case, we can just decrease the number of children in the list
                parentHierarchy.childCount -= range;
                compactNodes[parentIndex] = parentHierarchy;
                return true;
            }
            
            // If we get here, it means we're detaching children in the center of the list of children

            var prevLength = compactNodes.Length;
            // Resize compactNodes to have space for the nodes we're detaching (compactNodes will probably have capacity for this already)
            compactNodes.Resize(prevLength + range,
				NativeArrayOptions.ClearMemory); 
                //NativeArrayOptions.UninitializedMemory);
            
            // Copy the original nodes to behind all our other nodes
            compactNodes.MemMove(prevLength, nodeIndex, range);
            
            // Move nodes behind our node on top of the node we're removing
            var count = parentChildCount - (siblingIndex + range);
            compactNodes.MemMove(nodeIndex, nodeIndex + range, count);
            
            // Copy original nodes to behind the new parent child list
            compactNodes.MemMove(lastNodeIndex, prevLength, range);

            // Set the compactNodes length to its original size
            compactNodes.Resize(prevLength,
                NativeArrayOptions.ClearMemory);
                //NativeArrayOptions.UninitializedMemory);
            
            slotIndexMap.SwapIndexRangeToBack(parentChildOffset, parentChildCount, siblingIndex, range);

            parentHierarchy.childCount -= range;
            compactNodes[parentIndex] = parentHierarchy;
            return true;
        }

        // Note: assumes the given compactNodeID and their respective nodeIDs are consecutive 
        internal int SetChildrenUncheckedSlowPath(int parentChildOffset, int parentChildCount, int insertStartIndex, CompactNodeID parentID, NativeList<CompactNodeID> compactNodeIDs, int startCompactNodeIndex, int count)
        {
            var compactNodeID       = compactNodeIDs[startCompactNodeIndex];
            var nodeIndex           = HierarchyIndexOfInternal(compactNodeID);
            var prevChildOffset     = parentChildOffset;
            var prevChildCount      = parentChildCount;
            var insertIndex         = insertStartIndex;

            Debug.Assert(!slotIndexMap.IsAnyIndexFree(nodeIndex, count));

            // Find (or create) a span of enough elements that we can use to copy our children into
            parentChildOffset = slotIndexMap.InsertIndexRange(prevChildOffset, prevChildCount, srcIndex: nodeIndex, dstIndex: insertIndex, count);
            Debug.Assert(parentChildOffset >= 0 && parentChildOffset < compactNodes.Length + prevChildCount + count);
            //Debug.Log($"{parentChildCount} {count} | {parentChildOffset} {prevChildOffset} | {prevChildCount - insertIndex} | {parentChildOffset} {insertIndex} | {nodeIndex} {parentChildOffset + insertIndex}");

            if (compactNodes.Length < parentChildOffset + prevChildCount + count)
            {
                compactNodes.Resize(parentChildOffset + prevChildCount + count, NativeArrayOptions.ClearMemory);
            }

            // We first move the last nodes to the correct new offset ..
            var items = prevChildCount - insertIndex;
            if (items != 0)
            {
                compactNodes.MemMove(parentChildOffset + insertIndex + count, prevChildOffset + insertIndex, items);
            }

            // If our offset is different then the front section will not be in the right location, so we might need to copy this
            // We'd also need to reset the old nodes to invalid, if we don't we'd create new dangling nodes
            if (prevChildOffset != parentChildOffset && insertIndex != 0)
            {
                // Then we move the first part (if necesary)
                compactNodes.MemMove(parentChildOffset, prevChildOffset, insertIndex);
            }

            compactNodes .ClearValues(parentChildOffset + insertIndex, count);

            for (int c = 0; c < count; c++)
            {
                // Then we copy our node to the new location
                var oldNodeIndex        = nodeIndex + c;
                var newNodeIndex        = parentChildOffset + insertIndex + c;
                var nodeItem            = compactNodes[oldNodeIndex];
                nodeItem.parentID = parentID;
                compactNodes[newNodeIndex] = nodeItem;

                // Then we set the old indices to 0
                if (oldNodeIndex < parentChildOffset || oldNodeIndex > (parentChildOffset + parentChildCount + count))
				{
					compactNodes[oldNodeIndex] = default;
                }
            }

            if (prevChildOffset != parentChildOffset)
            {
                // Set the old indices to 0
                for (int c = prevChildOffset; c < prevChildOffset + prevChildCount; c++)
                {
                    if (c < parentChildOffset || c > (parentChildOffset + parentChildCount + count))
					{
						compactNodes[c] = default;
                    }
                }
            }
			return parentChildOffset;
        }

        internal bool SetChildrenUnchecked(ref SlotIndexMap hierarchyIDLookup, NativeList<CompactHierarchy> hierarchies, ref SlotIndexMap nodeIDLookup, NativeList<CompactNodeID> nodes, CompactNodeID parentID, NativeList<CompactNodeID> compactNodeIDs, bool ignoreBrushMeshHashes = false)
        {
            Debug.Assert(IsCreated);
            var parentIndex = HierarchyIndexOfInternal(parentID);
            if (parentIndex == -1)
                throw new ArgumentException(nameof(parentID), $"{nameof(parentID)} is invalid");

            Debug.Assert(parentID != CompactNodeID.Invalid);
            var parentHierarchy = compactNodes[parentIndex];

            bool noChange = true;
            for (int c = compactNodeIDs.Length - 1; c >= 0; c--)
            {
                var compactNodeID = compactNodeIDs[c];
                var nodeIndex = HierarchyIndexOfInternal(compactNodeID);
                if (nodeIndex == -1)
                    throw new ArgumentException($"{nameof(CompactNodeID)} is invalid", nameof(compactNodeID));

                var parentChildOffset   = parentHierarchy.childOffset;
                var desiredIndex        = parentChildOffset + c;
                if (desiredIndex != nodeIndex)
                    noChange = false;
            }

            if (noChange &&
                parentHierarchy.childCount == compactNodeIDs.Length)
                return true;

            DetachAllChildrenInternal(parentIndex);

			//Debug.Log($"Start {compactNodeIDs.Length}");
			bool needIdFixup = false;
            int insertIndex = 0;
            for (int c = 0; c < compactNodeIDs.Length; c++)
            {
                var parentChildCount    = parentHierarchy.childCount;
                Debug.Assert(insertIndex >= 0 && insertIndex <= parentChildCount);

                var compactNodeID       = compactNodeIDs[c];
                var nodeIndex           = HierarchyIndexOfInternal(compactNodeID);

                var parentChildOffset   = parentHierarchy.childOffset;
                var desiredIndex        = parentChildOffset + insertIndex;

                // If our new parent doesn't have any child nodes yet, we don't need to move our node and just set 
                // our node as the location for our children
                if (parentChildCount == 0)
                {
                    // Make a temporary copy of our node in case we need to move it
                    var nodeItem = compactNodes[nodeIndex];

                    parentHierarchy.childOffset = nodeIndex;
                    parentHierarchy.childCount = 1;
                    compactNodes[parentIndex] = parentHierarchy;
                
                    nodeItem.parentID = parentID;
                    compactNodes[nodeIndex] = nodeItem;
                    insertIndex++;
                    continue;
                }

                // If the desired index of our node is already at the index we want it to be, things are simple
                if (desiredIndex == nodeIndex)
                {
                    // Make a temporary copy of our node in case we need to move it
                    var nodeItem = compactNodes[nodeIndex];

                    parentHierarchy.childCount ++;
                    compactNodes[parentIndex] = parentHierarchy;

                    nodeItem.parentID = parentID;
                    compactNodes[nodeIndex] = nodeItem;
                    insertIndex++;
                    continue;
                }

                // If, however, the desired index of our node is NOT at the index we want it to be, 
                // we need to move nodes around

                // find all nodes that are consecutive so we can move them together
                var prevNodeIndex = nodeIndex;
                var lastCompactNodeIndex = c;
                do
                {
                    lastCompactNodeIndex++;
                    if (lastCompactNodeIndex >= compactNodeIDs.Length)
                        break;
                    var nextNodeIndex = HierarchyIndexOfInternal(compactNodeIDs[lastCompactNodeIndex]);
                    if (nextNodeIndex - prevNodeIndex != 1)
                        break;
                    prevNodeIndex = nextNodeIndex;
                } while (true);

                //Debug.Log($"{lastCompactNodeIndex - c} | {c} {lastCompactNodeIndex} {compactNodeIDs.Length} | {nodeIndex}");

                needIdFixup = true;
                UnityEngine.Profiling.Profiler.BeginSample("Slow path");
                var count = (lastCompactNodeIndex - c);
                var insertStartIndex = insertIndex;
                parentChildOffset = SetChildrenUncheckedSlowPath(parentChildOffset, parentChildCount, insertStartIndex, parentID, compactNodeIDs, c, count);
                parentHierarchy.childOffset = parentChildOffset; // We make sure we set the parent child offset correctly
                parentHierarchy.childCount += count;             // And we increase the childCount of our parent
                insertIndex = insertStartIndex + count;
                compactNodes[parentIndex] = parentHierarchy;
                UnityEngine.Profiling.Profiler.EndSample();

                c = lastCompactNodeIndex - 1;
            }

            if (needIdFixup)
            {
                UnityEngine.Profiling.Profiler.BeginSample("MoveNodeID");
                {
                    // And fixup the id to index lookup
                    CompactHierarchyManager.MoveNodeIDs(ref nodeIDLookup, nodes, compactNodes, parentHierarchy.childOffset, parentHierarchy.childCount);
                }
                UnityEngine.Profiling.Profiler.EndSample();
            }

            // TODO: comment this out after adding enough tests 
            UnityEngine.Profiling.Profiler.BeginSample("CheckConsistency");
            Debug.Assert(CheckConsistency(ref hierarchyIDLookup, hierarchies, ref nodeIDLookup, nodes, ignoreBrushMeshHashes));
            UnityEngine.Profiling.Profiler.EndSample();
            return true;
        }


        bool AttachInternal(ref SlotIndexMap hierarchyIDLookup, NativeList<CompactHierarchy> hierarchies, ref SlotIndexMap nodeIDLookup, NativeList<CompactNodeID> nodes, CompactNodeID parentID, int parentIndex, int insertIndex, CompactNodeID compactNodeID, bool ignoreBrushMeshHashes = false)
        {
            Debug.Assert(parentID != CompactNodeID.Invalid);

            var parentHierarchy   = compactNodes[parentIndex];
            var parentChildCount  = parentHierarchy.childCount;
            if (insertIndex < 0 || insertIndex > parentChildCount)
            {
                Debug.LogError($"Index ({insertIndex}) must be between 0 .. {parentChildCount}");
                return false;
            }

            var nodeIndex = HierarchyIndexOfInternal(compactNodeID);
            if (nodeIndex == -1)
                throw new ArgumentException($"{nameof(CompactNodeID)} is invalid", nameof(compactNodeID));

            // Make a temporary copy of our node in case we need to move it
            var nodeItem            = compactNodes[nodeIndex];
            var oldParentID         = nodeItem.parentID; 
            var parentChildOffset   = parentHierarchy.childOffset;
            var desiredIndex        = parentChildOffset + insertIndex;

            // If the node is already a child of a parent, then we need to remove it from that parent
            if (oldParentID != CompactNodeID.Invalid)
            {
                // Check if our node is already a child of the right parent and at the correct position
                if (oldParentID == parentID && desiredIndex == nodeIndex)
                    return true;

                // inline & optimize this
                Detach(compactNodeID);
                if (desiredIndex > nodeIndex)
                {
                    desiredIndex--;
                    insertIndex--;
                }
                nodeIndex           = HierarchyIndexOfInternal(compactNodeID);
                nodeItem            = compactNodes[nodeIndex];
                parentHierarchy     = compactNodes[parentIndex];
                parentChildCount    = parentHierarchy.childCount;
                parentChildOffset   = parentHierarchy.childOffset;
                desiredIndex        = parentChildOffset + insertIndex;
            }

            // If our new parent doesn't have any child nodes yet, we don't need to move our node and just set 
            // our node as the location for our children
            if (parentChildCount == 0)
            {
                parentHierarchy.childOffset = nodeIndex;
                parentHierarchy.childCount = 1;
                compactNodes[parentIndex] = parentHierarchy;
                
                nodeItem.parentID = parentID;
                compactNodes[nodeIndex] = nodeItem;
                return true;
            }
            

            // If the desired index of our node is already at the index we want it to be, things are simple
            if (desiredIndex == nodeIndex)
            {
                parentHierarchy.childCount ++;
                compactNodes[parentIndex] = parentHierarchy;

                nodeItem.parentID = parentID;
                compactNodes[nodeIndex] = nodeItem;
                return true;
            }

            // If, however, the desired index of our node is NOT at the index we want it to be, 
            // we need to move nodes around


            // If it's a different parent then we need to change the size of our child list
            {
                var originalOffset = parentChildOffset;
                var originalCount  = parentChildCount;

                Debug.Assert(!slotIndexMap.IsIndexFree(nodeIndex));

                // Find (or create) a span of enough elements that we can use to copy our children into
                parentChildCount++;
                parentChildOffset = slotIndexMap.InsertIndexRange(originalOffset, originalCount, srcIndex: nodeIndex, dstIndex: insertIndex, 1);
                Debug.Assert(parentChildOffset >= 0 && parentChildOffset < compactNodes.Length + parentChildCount);

                if (compactNodes.Length < parentChildOffset + parentChildCount)
                {
                    compactNodes.Resize(parentChildOffset + parentChildCount, NativeArrayOptions.ClearMemory);
                }

                // We first move the last nodes to the correct new offset ..
                var items = originalCount - insertIndex;
                compactNodes.MemMove(parentChildOffset + insertIndex + 1, originalOffset + insertIndex, items);

                // If our offset is different then the front section will not be in the right location, so we might need to copy this
                // We'd also need to reset the old nodes to invalid, if we don't we'd create new dangling nodes
                if (originalOffset != parentChildOffset)
                {
                    // Then we move the first part (if necesary)
                    items = insertIndex;
                    compactNodes.MemMove(parentChildOffset, originalOffset, items);
                }

                // Then we copy our node to the new location
                var newNodeIndex = parentChildOffset + insertIndex;
                nodeItem.parentID = parentID;
                compactNodes[newNodeIndex] = nodeItem;

                // Then we set the old indices to 0
                var newCount = originalCount + 1;
                if (nodeIndex < parentChildOffset || nodeIndex >= parentChildOffset + newCount)
                {
                    //if (brushOutlines[nodeIndex].IsCreated) brushOutlines[nodeIndex].Dispose(); 
                    compactNodes[nodeIndex] = default;
                }
                for (int i = originalOffset, lastIndex = (originalOffset + originalCount); i < lastIndex; i++)
                {
                    if (i >= parentChildOffset && i < parentChildOffset + newCount)
                        continue;

                    //if (brushOutlines[i].IsCreated) brushOutlines[i].Dispose(); 
                    compactNodes[i] = default;
                }

                // And fixup the id to index lookup
                for (int i = parentChildOffset, lastIndex = (parentChildOffset + newCount); i < lastIndex; i++)
                {
                    if (compactNodes[i].nodeID != default)
                        CompactHierarchyManager.MoveNodeID(ref nodeIDLookup, nodes, compactNodes[i].nodeID, compactNodes[i].compactNodeID);
                }

                parentHierarchy.childOffset = parentChildOffset; // We make sure we set the parent child offset correctly
                parentHierarchy.childCount++;                    // And we increase the childCount of our parent
                compactNodes[parentIndex] = parentHierarchy;
            }

            // TODO: comment this out after adding enough tests 
            Debug.Assert(CheckConsistency(ref hierarchyIDLookup, hierarchies, ref nodeIDLookup, nodes, ignoreBrushMeshHashes)); 
            return true;
        }

        internal void ReserveChildren(int arrayLength)
        {
            if (arrayLength <= 0)
                return;
            if (compactNodes.Capacity < compactNodes.Length + arrayLength)
                compactNodes.SetCapacity((int)((compactNodes.Length + arrayLength) * 1.5f));
        }

        public readonly void GetTreeNodes(NativeList<CompactNodeID> nodes, NativeList<CompactNodeID> brushes)
        {
            if (nodes.IsCreated) nodes.Clear();
            if (brushes.IsCreated) brushes.Clear();
            if (!nodes.IsCreated && !brushes.IsCreated)
                return;

            var rootIndex = UnsafeHierarchyIndexOfInternal(RootID);
            if (rootIndex < 0 || rootIndex >= compactNodes.Length)
                return;


			unsafe
            {
                if (nodes.IsCreated) nodes.Add(RootID);
                var compactNodesPtr = compactNodes.Ptr;
                using var nodeStack = new NativeList<int>(math.max(1, compactNodes.Length), Allocator.Temp);
                
                nodeStack.Add(rootIndex);
                while (nodeStack.Length > 0)
                {
                    var lastNodeStackIndex = nodeStack.Length - 1;
                    var nodeIndex = nodeStack[lastNodeStackIndex];
                    nodeStack.RemoveAt(lastNodeStackIndex);
                    ref var node = ref compactNodesPtr[nodeIndex];

                    if (!IsValidCompactNodeID(node.compactNodeID))
                        continue;

                    if (nodes.IsCreated &&
                        node.compactNodeID != RootID)
                    {
                        nodes.Add(node.compactNodeID);
                    }
                    if (node.childCount > 0)
                    {
                        for (int i = 0, childIndex = node.childOffset + node.childCount - 1, childCount = node.childCount; i < childCount; i++, childIndex--)
                            nodeStack.Add(childIndex);
                    }
                    else
                    if (node.nodeInformation.brushMeshHash != Int32.MaxValue && brushes.IsCreated)
                    {
                        brushes.Add(node.compactNodeID);
                    }
                }
            }
        }

        internal readonly void GetAllNodes(NativeList<CSGTreeNode> nodes)
        {
            if (!nodes.IsCreated)
                return;
            
            nodes.Clear();

			unsafe
            {
                var compactNodesPtr = compactNodes.Ptr;
                for (int i = 0, count = this.compactNodes.Length; i < count; i++)
                {
                    ref var node = ref compactNodesPtr[i];
                    if (node.nodeID == NodeID.Invalid)
                        continue;

                    var treeNode = CSGTreeNode.Find(node.nodeID);
                    if (!treeNode.Valid)
                        continue;
                    nodes.Add(treeNode);
                }
            }
        }

        
        // Temporary workaround until we can switch to hashes
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal readonly bool IsAnyStatusFlagSet(CompactNodeID compactNodeID)
        {
            Debug.Assert(IsCreated, "Hierarchy has not been initialized");
            if (compactNodeID == CompactNodeID.Invalid)
                return false;

            ref var node = ref GetChildRef(compactNodeID);
            return node.flags != NodeStatusFlags.None;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal readonly bool IsStatusFlagSet(CompactNodeID compactNodeID, NodeStatusFlags flag)
        {
            Debug.Assert(IsCreated, "Hierarchy has not been initialized");
            if (compactNodeID == CompactNodeID.Invalid)
                return false;

            ref var node = ref GetChildRef(compactNodeID);
            return (node.flags & flag) == flag;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void SetStatusFlag(CompactNodeID compactNodeID, NodeStatusFlags flag)
        {
            Debug.Assert(IsCreated, "Hierarchy has not been initialized");
            if (compactNodeID == CompactNodeID.Invalid)
                return;

            ref var node = ref GetChildRef(compactNodeID);
            node.flags |= flag;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void ClearAllStatusFlags(CompactNodeID compactNodeID)
        {
            Debug.Assert(IsCreated, "Hierarchy has not been initialized");
            if (compactNodeID == CompactNodeID.Invalid)
                return;

            ref var node = ref GetChildRef(compactNodeID);
            node.flags = NodeStatusFlags.None;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void ClearStatusFlag(CompactNodeID compactNodeID, NodeStatusFlags flag)
        {
            Debug.Assert(IsCreated, "Hierarchy has not been initialized");
            if (compactNodeID == CompactNodeID.Invalid)
                return;

            ref var node = ref GetChildRef(compactNodeID);
            node.flags &= ~flag;
        }
        
        // This method might be removed/renamed in the future
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal readonly bool IsNodeDirty(CompactNodeID compactNodeID)
        {
            if (!IsValidCompactNodeID(compactNodeID))
                return false;

            ref var node = ref GetChildRef(compactNodeID);
            CSGNodeType nodeType;
            if (RootID != compactNodeID)
                nodeType = (node.brushMeshHash == Int32.MaxValue) ? CSGNodeType.Branch : CSGNodeType.Brush;
            else
                nodeType = CSGNodeType.Tree;

            switch (nodeType)
            {
                case CSGNodeType.Brush:  return (node.flags & (NodeStatusFlags.NeedCSGUpdate)) != NodeStatusFlags.None;
                case CSGNodeType.Branch: return (node.flags & (NodeStatusFlags.BranchNeedsUpdate | NodeStatusFlags.NeedPreviousSiblingsUpdate)) != NodeStatusFlags.None;
                case CSGNodeType.Tree:   return (node.flags & (NodeStatusFlags.TreeNeedsUpdate | NodeStatusFlags.TreeMeshNeedsUpdate)) != NodeStatusFlags.None;
            }
            return false;
        }

        // This method might be removed/renamed in the future
        internal bool SetChildrenDirty(CompactNodeID compactNodeID)
        {
            if (!IsValidCompactNodeID(compactNodeID))
                return false;

            ref var node = ref GetChildRef(compactNodeID);
            if (node.brushMeshHash != Int32.MaxValue)
                return false;

            var result = true;
            var count = ChildCount(compactNodeID);
            for (int i = 0; i < count; i++)
            {
                var childID = GetChildCompactNodeIDAtInternal(compactNodeID, i);
                result = SetDirty(childID) && result;
            }
            return result;
        }

        // This method might be removed/renamed in the future
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal bool SetDirty(CompactNodeID compactNodeID)
        {
            if (!IsValidCompactNodeID(compactNodeID))
                return false;

            ref var node = ref GetChildRef(compactNodeID);
            CSGNodeType nodeType;
            if (RootID != compactNodeID)
                nodeType = (node.brushMeshHash == Int32.MaxValue) ? CSGNodeType.Branch : CSGNodeType.Brush;
            else
                nodeType = CSGNodeType.Tree;

            switch (nodeType)
            {
                case CSGNodeType.Brush:
                {
                    node.flags |= NodeStatusFlags.NeedFullUpdate;
                    ref var rootNode = ref GetChildRef(RootID);
                    rootNode.flags |= NodeStatusFlags.TreeNeedsUpdate;
                    //Debug.Assert(IsNodeDirty(compactNodeID));
                    return true; 
                }
                case CSGNodeType.Branch:
                {
                    node.flags |= NodeStatusFlags.BranchNeedsUpdate;
                    ref var rootNode = ref GetChildRef(RootID);
                    rootNode.flags |= NodeStatusFlags.TreeNeedsUpdate;
                    //Debug.Assert(IsNodeDirty(compactNodeID));
                    return true; 
                }
                case CSGNodeType.Tree:
                {
                    node.flags |= NodeStatusFlags.TreeNeedsUpdate;
                    //Debug.Assert(IsNodeDirty(compactNodeID));
                    return true;
                }
                default:
                {
                    Debug.LogError("Unknown node type");
                    return false;
                }
            }
        }

        // Do not use. This method might be removed/renamed in the future
        [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly bool ClearDirty(CompactNodeID compactNodeID)
        {
            if (!IsValidCompactNodeID(compactNodeID))
                return false;

            GetChildRef(compactNodeID).flags = NodeStatusFlags.None;
            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal readonly CSGNodeType GetTypeOfNode(CompactNodeID compactNodeID)
        {
            if (!IsValidCompactNodeID(compactNodeID))
                return CSGNodeType.None;

            if (RootID == compactNodeID)
                return CSGNodeType.Tree;

            return (GetChildRef(compactNodeID).brushMeshHash == Int32.MaxValue) ? CSGNodeType.Branch : CSGNodeType.Brush;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal readonly int GetNodeInstanceID(CompactNodeID compactNodeID)
        {
            if (!IsValidCompactNodeID(compactNodeID))
                return 0;

            return GetChildRef(compactNodeID).instanceID;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal readonly CSGOperationType GetOperation(CompactNodeID compactNodeID)
        {
            if (!IsValidCompactNodeID(compactNodeID))
                return CSGOperationType.Invalid;                        
            return GetChildRef(compactNodeID).operation;
        }


        internal readonly bool IsDescendant(CompactNodeID parentCompactNodeID, CompactNodeID childCompactNodeID)
        {
            if (parentCompactNodeID.hierarchyID != HierarchyID)
                throw new ArgumentException($"{nameof(parentCompactNodeID)} is not part of this hierarchy", nameof(parentCompactNodeID));

            if (childCompactNodeID.hierarchyID != HierarchyID)
                throw new ArgumentException($"{nameof(childCompactNodeID)} is not part of this hierarchy", nameof(childCompactNodeID));

            var iterator = childCompactNodeID;
            while (iterator != parentCompactNodeID)
            {
                if (iterator == CompactNodeID.Invalid)
                    return false;
                iterator = ParentOf(iterator);
            }
            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal readonly void SetTreeDirty()
        {
            ref var rootNode = ref GetChildRef(RootID);
            rootNode.flags |= NodeStatusFlags.TreeNeedsUpdate;
        }

        [return: MarshalAs(UnmanagedType.U1)]
        internal bool SetState(CompactNodeID compactNodeID, [NoAlias, ReadOnly] NativeParallelHashMap<int, RefCountedBrushMeshBlob> brushMeshBlobCache, Int32 brushMeshHash, CSGOperationType operation, float4x4 transformation)
        {
            if (!IsValidCompactNodeID(compactNodeID))
                throw new ArgumentException($"The {nameof(CompactNodeID)} {nameof(compactNodeID)} ({compactNodeID}) is invalid", nameof(compactNodeID));


            var modifiedFlags = NodeStatusFlags.None;
            ref var nodeRef = ref UnsafeGetChildRefAtInternal(compactNodeID);
            if (nodeRef.operation != operation)
            {
                modifiedFlags |= NodeStatusFlags.NeedAllTouchingUpdated;
                nodeRef.operation = operation;
            }

            if (math.any(nodeRef.transformation.c0 = transformation.c0) ||
                math.any(nodeRef.transformation.c1 = transformation.c1) ||
                math.any(nodeRef.transformation.c2 = transformation.c2) ||
                math.any(nodeRef.transformation.c3 = transformation.c3))
            {
                modifiedFlags |= NodeStatusFlags.NeedAllTouchingUpdated | NodeStatusFlags.TransformationModified;
                nodeRef.transformation = transformation;
            }

            if (UpdateHash(ref brushMeshBlobCache, compactNodeID, brushMeshHash) ||
                nodeRef.brushMeshHash != brushMeshHash)
            {
                modifiedFlags |= NodeStatusFlags.ShapeModified | NodeStatusFlags.NeedAllTouchingUpdated;
                nodeRef.brushMeshHash = brushMeshHash;
            }

            nodeRef.flags |= modifiedFlags;
            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal readonly void UpdateBounds(CompactNodeID compactNodeID, MinMaxAABB bounds)
        {
            ref var nodeRef = ref UnsafeGetChildRefAtInternal(compactNodeID);
            nodeRef.bounds = bounds;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal readonly float4x4 GetLocalTransformation(CompactNodeID compactNodeID)
        {
            if (!IsValidCompactNodeID(compactNodeID))
                throw new ArgumentException($"The {nameof(CompactNodeID)} {nameof(compactNodeID)} ({compactNodeID}) is invalid", nameof(compactNodeID));

            ref var nodeRef = ref GetChildRef(compactNodeID);
            return nodeRef.transformation;
        }


        [return: MarshalAs(UnmanagedType.U1)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal readonly bool SetBrushMeshID(CompactNodeID compactNodeID, Int32 brushMeshID)
        {
            if (!IsValidCompactNodeID(compactNodeID))
                throw new ArgumentException($"The {nameof(CompactNodeID)} {nameof(compactNodeID)} ({compactNodeID}) is invalid", nameof(compactNodeID));

            ref var nodeRef = ref GetChildRef(compactNodeID);
            if (nodeRef.brushMeshHash != brushMeshID)
            {
                nodeRef.brushMeshHash = brushMeshID;
                nodeRef.flags |= NodeStatusFlags.ShapeModified | NodeStatusFlags.NeedAllTouchingUpdated;
            }
            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal readonly MinMaxAABB GetBrushBounds(CompactNodeID compactNodeID)
        {
            if (!IsValidCompactNodeID(compactNodeID))
                throw new ArgumentException($"The {nameof(CompactNodeID)} {nameof(compactNodeID)} ({compactNodeID}) is invalid", nameof(compactNodeID));

            ref var nodeRef = ref GetChildRef(compactNodeID);
            if (nodeRef.brushMeshHash == Int32.MaxValue)
                throw new ArgumentException($"The {nameof(CompactNodeID)} {nameof(compactNodeID)} ({compactNodeID}) is invalid", nameof(compactNodeID));

            return nodeRef.bounds;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal readonly MinMaxAABB GetBrushBounds(CompactNodeID compactNodeID, float4x4 transformation)
        {
            if (!IsValidCompactNodeID(compactNodeID))
                throw new ArgumentException($"The {nameof(CompactNodeID)} {nameof(compactNodeID)} ({compactNodeID}) is invalid", nameof(compactNodeID));

            ref var nodeRef = ref GetChildRef(compactNodeID);
            if (nodeRef.brushMeshHash == Int32.MaxValue)
                throw new ArgumentException($"The {nameof(CompactNodeID)} {nameof(compactNodeID)} ({compactNodeID}) is invalid", nameof(compactNodeID));

            return BrushMeshManager.CalculateBounds(nodeRef.brushMeshHash, in transformation);
        }

        public readonly CompactNodeID GetRootOfNode(CompactNodeID compactNodeID)
        {
            var rootCompactNodeID = RootID;
            if (compactNodeID == rootCompactNodeID)
                return CompactNodeID.Invalid;
            var iterator = compactNodeID;
            while (iterator != CompactNodeID.Invalid)
            {
                if (iterator == rootCompactNodeID)
                    return rootCompactNodeID;
                iterator = ParentOf(iterator);
            }
            return CompactNodeID.Invalid;
        }
    }
}
