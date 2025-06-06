﻿using Unity.Mathematics;
using Unity.Collections;
using Unity.Entities;

namespace Chisel.Core
{
    class CompactTreeBuilder
    {
        struct CompactTopDownBuilderNode
        {
            public CompactNodeID  compactNodeID;
            public int            compactHierarchyindex;
        }


        public static BlobAssetReference<CompactTree> Create(CompactHierarchy.ReadOnly  compactHierarchy, 
                                                             NativeArray<CompactNodeID> nodes, 
                                                             NativeArray<CompactNodeID> brushes, 
                                                             CompactNodeID              treeCompactNodeID,
															 Allocator					allocator = Allocator.Persistent)// Indirect
		{
            if (brushes.Length == 0)
                return BlobAssetReference<CompactTree>.Null;

            var minNodeIDValue = int.MaxValue;
            var maxNodeIDValue = 0;
            for (int b = 0; b < nodes.Length; b++)
            {
                var nodeID = nodes[b];
                if (nodeID == CompactNodeID.Invalid)
                    continue;

                var nodeIDValue = nodeID.slotIndex.index;
                minNodeIDValue = math.min(nodeIDValue, minNodeIDValue);
                maxNodeIDValue = math.max(nodeIDValue, maxNodeIDValue);
            }

            if (minNodeIDValue == int.MaxValue)
                minNodeIDValue = 0;

            var minBrushIDValue = int.MaxValue;
            var maxBrushIDValue = 0;
            for (int b = 0; b < brushes.Length; b++)
            {
                var brushCompactNodeID = brushes[b];
                if (brushCompactNodeID == CompactNodeID.Invalid)
                    continue;

                var brushCompactNodeIDValue = brushCompactNodeID.slotIndex.index;
                minBrushIDValue = math.min(brushCompactNodeIDValue, minBrushIDValue);
                maxBrushIDValue = math.max(brushCompactNodeIDValue, maxBrushIDValue);
            }

            if (minBrushIDValue == int.MaxValue)
                minBrushIDValue = 0;

            var desiredBrushIDValueToBottomUpLength = (maxBrushIDValue + 1) - minBrushIDValue;

			NativeArray<int> brushIDValueToAncestorLegend;
			using var _brushIDValueToAncestorLegend = brushIDValueToAncestorLegend = new NativeArray<int>(desiredBrushIDValueToBottomUpLength, Allocator.Temp);
			
			NativeArray<int> brushIDValueToOrder;
			using var _brushIDValueToOrder = brushIDValueToOrder = new NativeArray<int>(desiredBrushIDValueToBottomUpLength, Allocator.Temp);

			using var brushAncestorLegend = new NativeList<BrushAncestorLegend>(brushes.Length, Allocator.Temp);
			using var brushAncestorsIDValues = new NativeList<int>(brushes.Length, Allocator.Temp);

			// Bottom-up -> per brush list of all ancestors to root
			for (int b = 0; b < brushes.Length; b++)
			{
				var brushCompactNodeID = brushes[b];
				if (!compactHierarchy.IsValidCompactNodeID(brushCompactNodeID))
					continue;

				var parentStart = brushAncestorsIDValues.Length;

				var parentCompactNodeID = compactHierarchy.ParentOf(brushCompactNodeID);
				while (compactHierarchy.IsValidCompactNodeID(parentCompactNodeID) && parentCompactNodeID != treeCompactNodeID)
				{
					var parentCompactNodeIDValue = parentCompactNodeID.slotIndex.index;
					brushAncestorsIDValues.Add(parentCompactNodeIDValue);
					parentCompactNodeID = compactHierarchy.ParentOf(parentCompactNodeID);
				}

				var brushCompactNodeIDValue = brushCompactNodeID.slotIndex.index;
				brushIDValueToAncestorLegend[brushCompactNodeIDValue - minBrushIDValue] = brushAncestorLegend.Length;
				brushIDValueToOrder[brushCompactNodeIDValue - minBrushIDValue] = b;
				brushAncestorLegend.Add(new BrushAncestorLegend()
				{
					ancestorEndIDValue = brushAncestorsIDValues.Length,
					ancestorStartIDValue = parentStart
				});
			}

			NativeList<CompactTopDownBuilderNode> nodeQueue;
			using var _nodeQueue = nodeQueue = new NativeList<CompactTopDownBuilderNode>(brushes.Length, Allocator.Temp);
			
			NativeList<CompactHierarchyNode> hierarchyNodes;
			using var _hierarchyNodes = hierarchyNodes = new NativeList<CompactHierarchyNode>(brushes.Length, Allocator.Temp);
			{
				if (brushAncestorLegend.Length == 0)
					return BlobAssetReference<CompactTree>.Null;

				// Top-down                    
				nodeQueue.Add(new CompactTopDownBuilderNode { compactNodeID = treeCompactNodeID, compactHierarchyindex = 0 });
				hierarchyNodes.Add(new CompactHierarchyNode
				{
					Type = CSGNodeType.Tree,
					Operation = CSGOperationType.Additive,
					CompactNodeID = treeCompactNodeID
				});

				while (nodeQueue.Length > 0)
				{
					var parentItem = nodeQueue[0];
					var parentCompactNodeID = parentItem.compactNodeID;
					nodeQueue.RemoveAt(0);
					var nodeCount = compactHierarchy.ChildCount(parentCompactNodeID);
					if (nodeCount == 0)
					{
						var item = hierarchyNodes[parentItem.compactHierarchyindex];
						item.childOffset = -1;
						item.childCount = 0;
						hierarchyNodes[parentItem.compactHierarchyindex] = item;
						continue;
					}

					int firstCompactTreeIndex = 0;
					// Skip all nodes that are not additive at the start of the branch since they will never produce any geometry
					while (firstCompactTreeIndex < nodeCount)
					{
						var childCompactNodeID = compactHierarchy.GetChildCompactNodeIDAt(parentItem.compactNodeID, firstCompactTreeIndex);
						if (!compactHierarchy.IsValidCompactNodeID(childCompactNodeID))
							break;
						var operation = compactHierarchy.GetOperation(childCompactNodeID);
						if (operation == CSGOperationType.Additive ||
							operation == CSGOperationType.Copy)
							break;
						firstCompactTreeIndex++;
					}

					var firstChildIndex = hierarchyNodes.Length;
					for (int i = firstCompactTreeIndex; i < nodeCount; i++)
					{
						var childCompactNodeID = compactHierarchy.GetChildCompactNodeIDAt(parentItem.compactNodeID, i);
						// skip invalid nodes (they don't contribute to the mesh)
						if (!compactHierarchy.IsValidCompactNodeID(childCompactNodeID))
							continue;

						var childType = compactHierarchy.GetTypeOfNode(childCompactNodeID);
						if (childType != CSGNodeType.Brush)
							nodeQueue.Add(new CompactTopDownBuilderNode
							{
								compactNodeID = childCompactNodeID,
								compactHierarchyindex = hierarchyNodes.Length
							});
						hierarchyNodes.Add(new CompactHierarchyNode
						{
							Type = childType,
							Operation = compactHierarchy.GetOperation(childCompactNodeID),
							CompactNodeID = childCompactNodeID
						});
					}

					{
						var item = hierarchyNodes[parentItem.compactHierarchyindex];
						item.childOffset = firstChildIndex;
						item.childCount = hierarchyNodes.Length - firstChildIndex;
						hierarchyNodes[parentItem.compactHierarchyindex] = item;
					}
				}

				using var builder = new BlobBuilder(Allocator.Temp);
				ref var root = ref builder.ConstructRoot<CompactTree>();
				builder.Construct(ref root.compactHierarchy, hierarchyNodes);
				builder.Construct(ref root.brushAncestorLegend, brushAncestorLegend);
				builder.Construct(ref root.brushAncestors, brushAncestorsIDValues);
				root.minBrushIDValue = minBrushIDValue;
				root.minNodeIDValue = minNodeIDValue;
				root.maxNodeIDValue = maxNodeIDValue;
				builder.Construct(ref root.brushIDValueToAncestorLegend, brushIDValueToAncestorLegend, desiredBrushIDValueToBottomUpLength);
				return builder.CreateBlobAssetReference<CompactTree>(allocator); // Allocator.Persistent / Confirmed to be disposed
			}
		}

    }
}
