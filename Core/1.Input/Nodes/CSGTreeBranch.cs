﻿using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;
using Unity.Mathematics;
using Unity.Burst;
using Unity.Collections;
using UnityEngine;

namespace Chisel.Core
{
    /// <summary>A branch in a CSG tree, used to encapsulate other <see cref="Chisel.Core.CSGTreeBranch"/>es and <see cref="Chisel.Core.CSGTreeBrush"/>es and perform operations with them as a whole.</summary>
    /// <remarks>A branch can be used to combine multiple branches and/or brushes, each with different <see cref="Chisel.Core.CSGOperationType"/>s, 
    /// and perform a CSG operation with the shape that's defined by all those branches and brushes on other parts of the CSG tree.
    /// <note>The internal ID contained in this struct is generated at runtime and is not persistent.</note>
    /// <note>This struct can be converted into a <see cref="Chisel.Core.CSGTreeNode"/> and back again.</note>
    /// <note>Be careful when keeping track of <see cref="Chisel.Core.CSGTreeBranch"/>s because <see cref="Chisel.Core.BrushMeshInstance.BrushMeshID"/>s can be recycled after being Destroyed.</note>
    /// See the [CSG Trees](~/documentation/CSGTrees.md) article for more information.</remarks>
    /// <seealso cref="Chisel.Core.CSGTreeNode"/>
    /// <seealso cref="Chisel.Core.CSGTree"/>
    /// <seealso cref="Chisel.Core.CSGTreeBrush"/>
    [StructLayout(LayoutKind.Sequential), GenerateTestsForBurstCompatibility, Serializable]
    [System.Diagnostics.DebuggerDisplay("Branch ({nodeID})")]
    public struct CSGTreeBranch : IEquatable<CSGTreeBranch>
    {
        #region Create
        /// <summary>Generates a branch and returns a <see cref="Chisel.Core.CSGTreeBranch"/> struct that contains a reference to it.</summary>
        /// <param name="instanceID">A unique id to help identify this particular branch. For instance, this could be an InstanceID to a [UnityEngine.Object](https://docs.unity3d.com/ScriptReference/Object.html)</param>
        /// <param name="children">The child nodes that are children of this branch. A branch may not have duplicate children, contain itself or contain a <see cref="Chisel.Core.CSGTree"/>.</param>
        /// <returns>A new <see cref="Chisel.Core.CSGTreeBranch"/>. May be an invalid node if it failed to create it.</returns>
        [BurstDiscard, MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static CSGTreeBranch Create(Int32 instanceID = 0, CSGOperationType operation = CSGOperationType.Additive, params CSGTreeNode[] children)
        {
            var branchNodeID = CompactHierarchyManager.CreateBranch(operation, instanceID);
            Debug.Assert(CompactHierarchyManager.IsValidNodeID(branchNodeID));
            if (children != null && children.Length > 0)
            {
				using var childrenNativeArray = children.ToNativeArray(Allocator.Temp);
				if (!CompactHierarchyManager.SetChildNodes(branchNodeID, childrenNativeArray))
				{
					CompactHierarchyManager.DestroyNode(branchNodeID);
					return CSGTreeBranch.Invalid;
				}
			}
            CompactHierarchyManager.SetDirty(branchNodeID);
            return CSGTreeBranch.Find(branchNodeID);
        }

        /// <summary>Generates a branch and returns a <see cref="Chisel.Core.CSGTreeBranch"/> struct that contains a reference to it.</summary>
        /// <param name="instanceID">A unique id to help identify this particular branch. For instance, this could be an InstanceID to a [UnityEngine.Object](https://docs.unity3d.com/ScriptReference/Object.html)</param>
        /// <param name="children">The child nodes that are children of this branch. A branch may not have duplicate children, contain itself or contain a <see cref="Chisel.Core.CSGTree"/>.</param>
        /// <returns>A new <see cref="Chisel.Core.CSGTreeBranch"/>. May be an invalid node if it failed to create it.</returns>
        [BurstDiscard, MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static CSGTreeBranch Create(Int32 instanceID, params CSGTreeNode[] children) { return Create(instanceID: instanceID, CSGOperationType.Additive, children); }

        /// <summary>Generates a branch and returns a <see cref="Chisel.Core.CSGTreeBranch"/> struct that contains a reference to it.</summary>
        /// <param name="children">The child nodes that are children of this branch. A branch may not have duplicate children, contain itself or contain a <see cref="Chisel.Core.CSGTree"/>.</param>
        /// <returns>A new <see cref="Chisel.Core.CSGTreeBranch"/>. May be an invalid node if it failed to create it.</returns>
        [BurstDiscard, MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static CSGTreeBranch Create(params CSGTreeNode[] children) { return Create(0, children); }
        #endregion



        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static CSGTreeBranch Find(NodeID nodeID)
        {
            if (nodeID == NodeID.Invalid)
                return CSGTreeBranch.Invalid;
            var compactNodeID = CompactHierarchyManager.GetCompactNodeID(nodeID);
            if (compactNodeID == CompactNodeID.Invalid)
                return CSGTreeBranch.Invalid;
            //var compactHierarchyID = CompactHierarchyManager.GetHierarchyID(nodeID);
            return Encapsulate(nodeID);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static CSGTreeBranch Find(CompactNodeID compactNodeID)
        {
            if (compactNodeID == CompactNodeID.Invalid)
                return CSGTreeBranch.Invalid;
            ref var hierarchy = ref CompactHierarchyManager.GetHierarchy(compactNodeID);
            var nodeID = hierarchy.GetNodeID(compactNodeID);
            if (nodeID == NodeID.Invalid)
                return CSGTreeBranch.Invalid;
            //var compactHierarchyID = hierarchy.HierarchyID;
            return Encapsulate(nodeID);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static CSGTreeBranch FindNoErrors(CompactNodeID compactNodeID)
        {
            if (compactNodeID == CompactNodeID.Invalid)
                return CSGTreeBranch.Invalid;
            ref var hierarchy = ref CompactHierarchyManager.GetHierarchy(compactNodeID);
            var nodeID = hierarchy.GetNodeIDNoErrors(compactNodeID);
            if (nodeID == NodeID.Invalid)
                return CSGTreeBranch.Invalid;
            //var compactHierarchyID = hierarchy.HierarchyID;
            return Encapsulate(nodeID);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static CSGTreeBranch Encapsulate(NodeID nodeID)
        {
            return new CSGTreeBranch() { nodeID = nodeID };
        }

        #region Node
        /// <value>Returns if the current <see cref="Chisel.Core.CSGTreeBranch"/> is valid or not.</value>
        /// <remarks><note>If <paramref name="Valid"/> is <b>false</b> that could mean that this node has been destroyed.</note></remarks>
        public bool				Valid			{ get { return nodeID != NodeID.Invalid && CompactHierarchyManager.IsValidNodeID(nodeID); } }

        /// <value>Gets the <see cref="Chisel.Core.CSGTreeBranch.NodeID"/> of the <see cref="Chisel.Core.CSGTreeBranch"/>, which is a unique ID of this node.</value>
        /// <remarks><note>NodeIDs are eventually recycled, so be careful holding on to Nodes that have been destroyed.</note></remarks>
        public NodeID           NodeID			{ get { return nodeID; } }
        
        /// <value>Gets the <see cref="Chisel.Core.CSGTreeBranch.InstanceID"/> set to the <see cref="Chisel.Core.CSGTreeBranch"/> at creation time.</value>
        public Int32			InstanceID			{ get { return CompactHierarchyManager.GetNodeInstanceID(nodeID); } }
        
        /// <value>Returns the dirty flag of the <see cref="Chisel.Core.CSGTreeBranch"/>. When the it's dirty, then it means (some of) its generated meshes have been modified.</value>
        public bool				Dirty			{ get { return CompactHierarchyManager.IsNodeDirty(nodeID); } }

        /// <summary>Force set the dirty flag of the <see cref="Chisel.Core.CSGTreeNode"/>.</summary>
        public void SetDirty	()				{ CompactHierarchyManager.SetDirty(nodeID); }

        /// <summary>Destroy this <see cref="Chisel.Core.CSGTreeNode"/>. Sets the state to invalid.</summary>
        /// <returns><b>true</b> on success, <b>false</b> on failure</returns>
        public bool Destroy		()				{ var prevBranchNodeID = nodeID; this = CSGTreeBranch.Invalid; return CompactHierarchyManager.DestroyNode(prevBranchNodeID); }

        /// <summary>Sets the state of this struct to invalid.</summary>
        public void SetInvalid	()				{ this = CSGTreeBranch.Invalid; }
        #endregion

        #region ChildNode
        /// <value>Returns the parent <see cref="Chisel.Core.CSGTreeBranch"/> this <see cref="Chisel.Core.CSGTreeBranch"/> is a child of. Returns an invalid node if it's not a child of any <see cref="Chisel.Core.CSGTreeBranch"/>.</value>
        public CSGTreeBranch	Parent			{ get { return CSGTreeBranch.Find(Hierarchy.ParentOf(CompactNodeID)); } }

        /// <value>Returns tree this <see cref="Chisel.Core.CSGTreeBranch"/> belongs to.</value>
        public CSGTree			Tree			{ get { return CSGTree.Find(Hierarchy.GetRootOfNode(CompactNodeID)); } }

        /// <value>The CSG operation that this <see cref="Chisel.Core.CSGTreeBranch"/> will use.</value>
        public CSGOperationType Operation		{ get { return (CSGOperationType)CompactHierarchyManager.GetNodeOperationType(nodeID); } set { CompactHierarchyManager.SetNodeOperationType(nodeID, value); } }
        #endregion

        #region ChildNodeContainer
        /// <value>Gets the number of elements contained in the <see cref="Chisel.Core.CSGTreeBranch"/>.</value>
        public readonly Int32 Count { get { return Hierarchy.ChildCount(CompactNodeID); } }

        /// <summary>Gets child at the specified index.</summary>
        /// <param name="index">The zero-based index of the child to get.</param>
        /// <returns>The element at the specified index.</returns>
        public readonly CSGTreeNode this[int index] { get { return Find(Hierarchy.GetChildCompactNodeIDAt(CompactNodeID, index)); } }


        /// <summary>Adds a <see cref="Chisel.Core.CSGTreeNode"/> to the end of the <see cref="Chisel.Core.CSGTreeBranch"/>.</summary>
        /// <param name="item">The <see cref="Chisel.Core.CSGTreeNode"/> to be added to the end of the <see cref="Chisel.Core.CSGTreeBranch"/>.</param>
        /// <returns><b>true</b> on success, <b>false</b> on failure</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Add			(CSGTreeNode item)	{ return CompactHierarchyManager.AddChildNode(nodeID, item.nodeID); }

        /// <summary>Adds the <see cref="Chisel.Core.CSGTreeNode"/>s of the specified array to the end of the  <see cref="Chisel.Core.CSGTreeBranch"/>.</summary>
        /// <param name="arrayPtr">The pointer to the array whose <see cref="Chisel.Core.CSGTreeNode"/>s should be added to the end of the <see cref="Chisel.Core.CSGTreeBranch"/>. The array itself cannot be null.</param>
        /// <param name="length">The length of the array whose <see cref="Chisel.Core.CSGTreeNode"/>s should be added to the end of the <see cref="Chisel.Core.CSGTreeBranch"/>. </param>
        /// <returns><b>true</b> on success, <b>false</b> on failure</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool AddRange(NativeArray<CSGTreeNode> array)
        {
            if (array.Length == 0)
                return true;
            return CompactHierarchyManager.InsertChildNodeRange(nodeID, Count, array);
        }

        /// <summary>Inserts an element into the <see cref="Chisel.Core.CSGTreeNode"/> at the specified index.</summary>
        /// <param name="index">The zero-based index at which <paramref name="item"/> should be inserted.</param>
        /// <param name="item">The <see cref="Chisel.Core.CSGTreeNode"/> to insert.</param>
        /// <returns><b>true</b> on success, <b>false</b> on failure</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Insert(int index, CSGTreeNode item) { return CompactHierarchyManager.InsertChildNode(nodeID, index, item.nodeID); }

        /// <summary>Inserts an array of <see cref="Chisel.Core.CSGTreeNode"/>s into the <see cref="Chisel.Core.CSGTreeBranch"/> at the specified index.</summary>
        /// <param name="index">The zero-based index at which the new <see cref="Chisel.Core.CSGTreeNode"/>s should be inserted.</param>
        /// <param name="arrayPtr">The pointer to the array whose <see cref="Chisel.Core.CSGTreeNode"/>s should be inserted into the <see cref="Chisel.Core.CSGTreeBranch"/>. The array itself cannot be null.</param>
        /// <param name="length">The length of the array whose <see cref="Chisel.Core.CSGTreeNode"/>s should be inserted into the <see cref="Chisel.Core.CSGTreeBranch"/>. </param>
        /// <returns><b>true</b> on success, <b>false</b> on failure</returns>
        [EditorBrowsable(EditorBrowsableState.Never)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool InsertRange(int index, NativeArray<CSGTreeNode> array)
        {
            if (array.Length == 0)
                return true;
            return CompactHierarchyManager.InsertChildNodeRange(nodeID, index, array);
        }

        /// <summary>Removes a specific <see cref="Chisel.Core.CSGTreeNode"/> from the <see cref="Chisel.Core.CSGTreeBranch"/>.</summary>
        /// <param name="item">The <see cref="Chisel.Core.CSGTreeNode"/> to remove from the <see cref="Chisel.Core.CSGTreeBranch"/>.</param>
        /// <returns><b>true</b> on success, <b>false</b> on failure</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Remove		(CSGTreeNode item)				{ return CompactHierarchyManager.RemoveChildNode(nodeID, item.nodeID); }

        /// <summary>Removes the child at the specified index of the <see cref="Chisel.Core.CSGTreeBranch"/>.</summary>
        /// <param name="index">The zero-based index of the child to remove.</param>
        /// <returns><b>true</b> on success, <b>false</b> on failure</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool RemoveAt	(int index)						{ return CompactHierarchyManager.RemoveChildNodeAt(nodeID, index); }

        /// <summary>Removes a range of children from the <see cref="Chisel.Core.CSGTreeBranch"/>.</summary>
        /// <param name="index">The zero-based starting index of the range of children to remove.</param>
        /// <param name="count">The number of children to remove.</param>
        /// <returns><b>true</b> on success, <b>false</b> on failure</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool RemoveRange	(int index, int count)			{ return CompactHierarchyManager.RemoveChildNodeRange(nodeID, index, count); }

        /// <summary>Removes all children from the <see cref="Chisel.Core.CSGTreeBranch"/>.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Clear		()								{ CompactHierarchyManager.ClearChildNodes(nodeID); }

        /// <summary>Determines the index of a specific child in the <see cref="Chisel.Core.CSGTreeBranch"/>.</summary>
        /// <param name="item">The <see cref="Chisel.Core.CSGTreeNode"/> to locate in the <see cref="Chisel.Core.CSGTreeBranch"/>.</param>
        /// <returns>The index of <paramref name="item"/> if found in the <see cref="Chisel.Core.CSGTreeBranch"/>; otherwise, –1.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int  IndexOf		(CSGTreeNode item)              { return Hierarchy.SiblingIndexOf(CompactNodeID, item.CompactNodeID); }

        /// <summary>Determines whether the <see cref="Chisel.Core.CSGTreeBranch"/> contains a specific value.</summary>
        /// <param name="item">The Object to locate in the <see cref="Chisel.Core.CSGTreeBranch"/>.</param>
        /// <returns><b>true</b> if item is found in the <see cref="Chisel.Core.CSGTreeBranch"/>; otherwise, <b>false</b>.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Contains	(CSGTreeNode item)				{ return IndexOf(item) != -1; }
        #endregion
        
#if UNITY_EDITOR
        #region Inspector State

        public bool Visible         { readonly get { return CompactHierarchyManager.IsBrushVisible(nodeID); } set { CompactHierarchyManager.SetVisibility(nodeID, value); } }
        public bool PickingEnabled  { readonly get { return CompactHierarchyManager.IsBrushPickingEnabled(nodeID); } set { CompactHierarchyManager.SetPickingEnabled(nodeID, value); } }
        public readonly bool IsSelectable { get { return CompactHierarchyManager.IsBrushSelectable(nodeID); } }

        #endregion
#endif

        #region Transformation
        internal float4x4 LocalTransformation { readonly get { return CompactHierarchyManager.GetNodeLocalTransformation(nodeID); } set { CompactHierarchyManager.SetNodeLocalTransformation(nodeID, in value); } }
        #endregion
                
        #region Comparison
        [EditorBrowsable(EditorBrowsableState.Never)]
        public static bool operator == (CSGTreeBranch left, CSGTreeBranch right) { return left.nodeID == right.nodeID; }
        [EditorBrowsable(EditorBrowsableState.Never)]
        public static bool operator != (CSGTreeBranch left, CSGTreeBranch right) { return left.nodeID != right.nodeID; }
		[EditorBrowsable(EditorBrowsableState.Never)]
        public static bool operator ==(CSGTreeBranch left, CSGTreeNode right) { return left.nodeID == right.nodeID; }
		[EditorBrowsable(EditorBrowsableState.Never)]
        public static bool operator !=(CSGTreeBranch left, CSGTreeNode right) { return left.nodeID != right.nodeID; }
		[EditorBrowsable(EditorBrowsableState.Never)]
        public static bool operator ==(CSGTreeNode left, CSGTreeBranch right) { return left.nodeID == right.nodeID; }
		[EditorBrowsable(EditorBrowsableState.Never)]
        public static bool operator !=(CSGTreeNode left, CSGTreeBranch right) { return left.nodeID != right.nodeID; }

        [EditorBrowsable(EditorBrowsableState.Never)]
        public override readonly bool Equals(object obj)
        {
            if (obj is CSGTreeBranch branch) return this == branch;
            if (obj is CSGTreeNode node) return this == node;
			return false;
        }
        [EditorBrowsable(EditorBrowsableState.Never)]
        public readonly bool Equals(CSGTreeBranch other) { return this == other; }
        [EditorBrowsable(EditorBrowsableState.Never)]
        public override readonly int GetHashCode() { return nodeID.GetHashCode(); }
        #endregion


        /// <value>An invalid node</value>
        readonly static CSGTreeBranch kInvalid = new() { nodeID = NodeID.Invalid };
		public static ref readonly CSGTreeBranch Invalid => ref kInvalid;


		// Temporary workaround until we can switch to hashes
		internal readonly bool IsAnyStatusFlagSet()                  { return Hierarchy.IsAnyStatusFlagSet(CompactNodeID); }
        internal readonly bool IsStatusFlagSet(NodeStatusFlags flag) { return Hierarchy.IsStatusFlagSet(CompactNodeID, flag); }
        internal void SetStatusFlag(NodeStatusFlags flag)   { Hierarchy.SetStatusFlag(CompactNodeID, flag); }
        internal void ClearStatusFlag(NodeStatusFlags flag) { Hierarchy.ClearStatusFlag(CompactNodeID, flag); }
        internal void ClearAllStatusFlags()                 { Hierarchy.ClearAllStatusFlags(CompactNodeID); }
        

        [SerializeField] internal NodeID nodeID;


        internal readonly CompactNodeID      CompactNodeID       { get { return CompactHierarchyManager.GetCompactNodeID(nodeID); } }
        internal readonly CompactHierarchyID CompactHierarchyID  { get { return CompactNodeID.hierarchyID; } }

		readonly ref CompactHierarchy Hierarchy
        {
			get
            {
                var hierarchyID = CompactHierarchyID;
                if (hierarchyID == CompactHierarchyID.Invalid)
                    throw new InvalidOperationException($"Invalid NodeID");
                return ref CompactHierarchyManager.GetHierarchy(hierarchyID);
            }
        }

        public override readonly string ToString() => $"{((CSGTreeNode)this).Type} ({nodeID})";
    }
}