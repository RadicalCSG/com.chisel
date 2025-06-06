﻿using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Unity.Collections;

namespace Chisel.Core
{
    public static class CSGTreeExtensions
    {
        /// <summary>Destroys all the children of this <see cref="Chisel.Core.CSGTreeBranch"/>.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void DestroyChildren(in this CSGTreeBranch branch)
        {
            CompactHierarchyManager.DestroyChildNodes(branch);
        }

        /// <summary>Destroys all the children of this <see cref="Chisel.Core.CSGTree"/>.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void DestroyChildren(in this CSGTree tree)
        {
            CompactHierarchyManager.DestroyChildNodes(tree);
        }

        /// <summary>Destroys all the children of this <see cref="Chisel.Core.CSGTreeNode"/>.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void DestroyChildren(in this CSGTreeNode treeNode)
        {
            CompactHierarchyManager.DestroyChildNodes(treeNode);
        }

        /// <summary>Sets all the children of this <see cref="Chisel.Core.CSGTree"/> to the give array of <see cref="Chisel.Core.CSGTreeNode"/>s at the specified index.</summary>
        /// <param name="array">The array whose <see cref="Chisel.Core.CSGTreeNode"/>s should be inserted into the <see cref="Chisel.Core.CSGTree"/>. The array itself cannot be null.</param>
        /// <returns><b>true</b> on success, <b>false</b> on failure</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool SetChildren(in this CSGTree tree, params CSGTreeNode[] array) 
        { 
            if (array == null) 
                throw new ArgumentNullException(nameof(array));

            tree.Clear();
            return tree.AddRange(array);
        }

        /// <summary>Sets all the children of this <see cref="Chisel.Core.CSGTree"/> to the give array of <see cref="Chisel.Core.CSGTreeNode"/>s at the specified index.</summary>
        /// <param name="list">The list whose <see cref="Chisel.Core.CSGTreeNode"/>s should be inserted into the <see cref="Chisel.Core.CSGTree"/>. The list itself cannot be null.</param>
        /// <returns><b>true</b> on success, <b>false</b> on failure</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool SetChildren(in this CSGTreeNode treeNode, List<CSGTreeNode> list) 
        {
            if (treeNode.Type == CSGNodeType.Brush)
                return false;

            if (list == null) 
                throw new ArgumentNullException(nameof(list));

            NativeArray<CSGTreeNode> childNodes;
			using var _childNodes = childNodes = new NativeArray<CSGTreeNode>(list.Count, Allocator.Temp);
            for (int i = 0; i < list.Count; i++)
                childNodes[i] = list[i];
            return CompactHierarchyManager.SetChildNodes(treeNode.nodeID, childNodes);
        }

        /// <summary>Sets all the children of this <see cref="Chisel.Core.CSGTreeBranch"/> to the give array of <see cref="Chisel.Core.CSGTreeNode"/>s at the specified index.</summary>
        /// <param name="array">The array whose <see cref="Chisel.Core.CSGTreeNode"/>s should be inserted into the <see cref="Chisel.Core.CSGTreeBranch"/>. The array itself cannot be null.</param>
        /// <returns><b>true</b> on success, <b>false</b> on failure</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool SetChildren(in this CSGTreeBranch branch, params CSGTreeNode[] array)
        {
            if (array == null)
                throw new ArgumentNullException(nameof(array));

            branch.Clear();
            return branch.AddRange(array);
        }

        /// <summary>Sets all the children of this <see cref="Chisel.Core.CSGTreeNode"/> to the give array of <see cref="Chisel.Core.CSGTreeNode"/>s at the specified index.</summary>
        /// <param name="array">The array whose <see cref="Chisel.Core.CSGTreeNode"/>s should be inserted into the <see cref="Chisel.Core.CSGTreeNode"/>. The array itself cannot be null.</param>
        /// <returns><b>true</b> on success, <b>false</b> on failure</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool SetChildren(in this CSGTreeNode node, params CSGTreeNode[] array)
        {
            if (node.Type == CSGNodeType.Branch)
                return SetChildren((CSGTreeBranch)node, array);
            else
            if (node.Type == CSGNodeType.Tree)
                return SetChildren((CSGTree)node, array);
            return false;
        }

        /// <summary>Inserts an array of <see cref="Chisel.Core.CSGTreeNode"/>s into the <see cref="Chisel.Core.CSGTree"/> at the specified index.</summary>
        /// <param name="index">The zero-based index at which the new <see cref="Chisel.Core.CSGTreeNode"/>s should be inserted.</param>
        /// <param name="array">The array whose <see cref="Chisel.Core.CSGTreeNode"/>s should be inserted into the <see cref="Chisel.Core.CSGTree"/>. The array itself cannot be null.</param>
        /// <returns><b>true</b> on success, <b>false</b> on failure</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool InsertRange(in this Chisel.Core.CSGTree tree, int index, params Chisel.Core.CSGTreeNode[] array)
        {
            if (array == null) throw new ArgumentNullException(nameof(array));
            var length = array.Length;
            if (length == 0) return true;
			using var nativeArray = array.ToNativeArray(Allocator.Temp);
			return tree.InsertRange(index, array);
		}


        /// <summary>Inserts the <see cref="Chisel.Core.CSGTreeNode"/>s of an array into the <see cref="Chisel.Core.CSGTreeBranch"/> at the specified index.</summary>
        /// <param name="index">The zero-based index at which the new <see cref="Chisel.Core.CSGTreeNode"/>s should be inserted.</param>
        /// <param name="array">The array whose <see cref="Chisel.Core.CSGTreeNode"/>s should be inserted into the <see cref="Chisel.Core.CSGTreeBranch"/>. The array itself cannot be null.</param>
        /// <returns><b>true</b> on success, <b>false</b> on failure</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool InsertRange(in this Chisel.Core.CSGTreeBranch branch, int index, params Chisel.Core.CSGTreeNode[] array)
        {
            if (array == null) throw new ArgumentNullException(nameof(array));
            var length = array.Length;
            if (length == 0) return true;

			using var nativeArray = array.ToNativeArray(Allocator.Temp);
			return branch.InsertRange(index, nativeArray);
		}


        /// <summary>Adds the <see cref="Chisel.Core.CSGTreeNode"/>s of the specified array to the end of the <see cref="Chisel.Core.CSGTree"/>.</summary>
        /// <param name="array">The array whose <see cref="Chisel.Core.CSGTreeNode"/>s should be added to the end of the <see cref="Chisel.Core.CSGTree"/>. The array itself cannot be null.</param>
        /// <returns><b>true</b> on success, <b>false</b> on failure</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool AddRange(in this Chisel.Core.CSGTree tree, params Chisel.Core.CSGTreeNode[] array)
        {
            return InsertRange(tree, tree.Count, array);
        }


        /// <summary>Adds the <see cref="Chisel.Core.CSGTreeNode"/>s of the specified array to the end of the  <see cref="Chisel.Core.CSGTreeBranch"/>.</summary>
        /// <param name="array">The array whose <see cref="Chisel.Core.CSGTreeNode"/>s should be added to the end of the <see cref="Chisel.Core.CSGTreeBranch"/>. The array itself cannot be null.</param>
        /// <returns><b>true</b> on success, <b>false</b> on failure</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool AddRange(in this Chisel.Core.CSGTreeBranch branch, params Chisel.Core.CSGTreeNode[] array)
        {
            return InsertRange(branch, branch.Count, array);
        }
    }
}
