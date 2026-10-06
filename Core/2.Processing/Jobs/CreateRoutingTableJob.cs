#define HAVE_SELF_CATEGORIES
#define USE_OPTIMIZATIONS
//#define SHOW_DEBUG_MESSAGES 
using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using Debug = UnityEngine.Debug;
using ReadOnlyAttribute = Unity.Collections.ReadOnlyAttribute;
using WriteOnlyAttribute = Unity.Collections.WriteOnlyAttribute;
using Unity.Entities;

namespace Chisel.Core
{
    [BurstCompile(CompileSynchronously = true)]
    struct CreateRoutingTableJob : IJobParallelForDefer
    {
        // Read
        [NoAlias, ReadOnly] public NativeList<IndexOrder>                                   allUpdateBrushIndexOrders;
        [NoAlias, ReadOnly] public NativeList<BlobAssetReference<BrushesTouchedByBrush>>    brushesTouchedByBrushes;
        [NoAlias, ReadOnly] public NativeReference<BlobAssetReference<CompactTree>>         compactTreeRef;

        // Write
        [NativeDisableParallelForRestriction]
        [NoAlias, WriteOnly] public NativeList<BlobAssetReference<RoutingTable>>            routingTableLookup;

        public int rowsPerNodeCopyLimit;

        internal const int kMaxRowsPerNodeCopy = ushort.MaxValue / CategoryRoutingRow.Length;

        // Per thread scratch memory
        [NativeDisableContainerSafetyRestriction, NoAlias] NativeArray<QueuedEvent>         queuedEvents;
        [NativeDisableContainerSafetyRestriction, NoAlias] NativeArray<CategoryStackNode>   tempStackArray;
        [NativeDisableContainerSafetyRestriction, NoAlias] NativeBitArray                   combineUsedIndices;
#if USE_OPTIMIZATIONS
        [NativeDisableContainerSafetyRestriction, NoAlias] NativeArray<ushort>              combineIndexRemap;
#endif
        [NativeDisableContainerSafetyRestriction, NoAlias] NativeArray<int>                 routingSteps;
        [NativeDisableContainerSafetyRestriction, NoAlias] NativeArray<CategoryStackNode>   routingTable;

        struct CategoryStackNode
        {
            int nodeIndex;
            ushort input;
            public CategoryRoutingRow routingRow;

            public ushort Input { get => input; set => input = value; }
            public int NodeIDValue { get => nodeIndex; set => nodeIndex = value; }
        }


        static int MaxQueuedEvents([NoAlias] ref BrushesTouchedByBrush brushesTouchedByBrush,
                                   [NoAlias] ref BlobArray<CompactHierarchyNode> compactHierarchy)
        {
            var nodeCount = compactHierarchy.Length;
            if (nodeCount <= 0)
                return 1;

            // depth[v]: the deepest the stack gets while the event for node v is processed and everything it spawns,
            // counted from v's own event being the only thing on it.
            var depth = new NativeArray<int>(nodeCount, Allocator.Temp);
            using var _depth = depth;

            for (int v = nodeCount - 1; v >= 0; v--)
            {
                ref var node = ref compactHierarchy[v];
                depth[v] = 1;   // its own event, popped without pushing anything

                if (node.Type == CSGNodeType.Brush || node.childCount == 0)
                    continue;

                // The same first-child skip GetStackNodes does: leading children that neither add nor copy never
                // produce geometry, so they are not walked.
                var firstIndex = node.childOffset;
                var lastIndex = firstIndex + node.childCount;
                if (firstIndex < 0 || lastIndex > nodeCount)
                    continue;
                while (firstIndex < lastIndex && (compactHierarchy[firstIndex].Operation != CSGOperationType.Additive &&
                                                  compactHierarchy[firstIndex].Operation != CSGOperationType.Copy))
                    firstIndex++;
                if ((lastIndex - firstIndex) <= 0)
                    continue;

                if (firstIndex <= v)
                    return (nodeCount * 2) + 2;   // children before their parent: fall back rather than guess

                // Pushed in the same order and under the same conditions as GetStackNodes, then popped LIFO: the
                // GetStackNode for the first child runs first, then the ListItems in increasing index order.
                var pushed = 0;
                for (int i = lastIndex - 1; i >= firstIndex + 1; i--)
                {
                    var childIntersectionType = brushesTouchedByBrush.Get(compactHierarchy[i].CompactNodeID);
                    if ((childIntersectionType != IntersectionType.NoIntersection &&
                         childIntersectionType != IntersectionType.InvalidValue) ||
                        compactHierarchy[i].Operation == CSGOperationType.Intersecting)
                        pushed++;
                }
                var firstType = brushesTouchedByBrush.Get(compactHierarchy[firstIndex].CompactNodeID);
                var pushesFirst = firstType != IntersectionType.NoIntersection &&
                                  firstType != IntersectionType.InvalidValue;
                if (pushesFirst)
                    pushed++;
                if (pushed == 0)
                    continue;

                // Everything sitting on the stack the instant the branch finishes pushing.
                var deepest = pushed;

                // Pop position 0 is the first child's GetStackNode, which spawns its subtree with the remaining
                // events still under it.
                var remaining = pushed - 1;
                if (pushesFirst)
                {
                    var reached = remaining + depth[firstIndex];
                    if (reached > deepest) deepest = reached;
                    remaining--;
                }

                // Then each ListItem in turn. A ListItem replaces itself with a Combine plus the child's
                // GetStackNode, and the Combine stays under that child for the whole of its subtree.
                for (int i = firstIndex + 1; i < lastIndex; i++)
                {
                    var childIntersectionType = brushesTouchedByBrush.Get(compactHierarchy[i].CompactNodeID);
                    var touches = childIntersectionType != IntersectionType.NoIntersection &&
                                  childIntersectionType != IntersectionType.InvalidValue;
                    if (!touches && compactHierarchy[i].Operation != CSGOperationType.Intersecting)
                        continue;
                    // A ListItem queued for a non-touching intersecting child spawns a GetStackNode that stops
                    // immediately, so its subtree costs one event rather than depth[i].
                    var childDepth = touches ? depth[i] : 1;
                    var reached = remaining + 1 + childDepth;
                    if (reached > deepest) deepest = reached;
                    remaining--;
                }

                depth[v] = deepest;
            }

            // The walk starts with the root's event already on the stack.
            return math.max(1, depth[0]);
        }

        public void Execute(int index)
        {
            if (index >= allUpdateBrushIndexOrders.Length)
                return;

            var processedIndexOrder = allUpdateBrushIndexOrders[index];
            var processedNodeID     = processedIndexOrder.compactNodeID;
            int processedNodeOrder  = processedIndexOrder.nodeOrder;

            var brushesTouchedByBrush = brushesTouchedByBrushes[processedNodeOrder];
            if (brushesTouchedByBrush == BlobAssetReference<BrushesTouchedByBrush>.Null)
                return;

            ref var compactTree                 = ref compactTreeRef.Value.Value;
            ref var topDownNodes                = ref compactTree.compactHierarchy;
            ref var brushesTouchedByBrushValue  = ref brushesTouchedByBrush.Value;
            // Sized from the tree rather than guessed: see MaxQueuedEvents.
            var maxQueuedEvents = MaxQueuedEvents(ref brushesTouchedByBrushValue, ref compactTree.compactHierarchy);
            NativeArray<QueuedEvent> queuedEvents;
            using var _queuedEvents = queuedEvents = new NativeArray<QueuedEvent>(maxQueuedEvents, Allocator.Temp);

            var touchingBrushCount = math.max(1, brushesTouchedByBrushValue.brushIntersections.Length);
            var routingTable       = new NativeArray<CategoryStackNode>(touchingBrushCount, Allocator.Temp);   // at least a row per brush it routes through
            var tempStackArray     = new NativeArray<CategoryStackNode>(touchingBrushCount, Allocator.Temp);   // holds one right stack at a time
            var routingSteps       = new NativeArray<int>(touchingBrushCount, Allocator.Temp);                 // one entry per node in a right stack
            var combineUsedIndices = new NativeBitArray(CategoryRoutingRow.Length, Allocator.Temp);           // Combine's vIndex spans at least one row per category
#if USE_OPTIMIZATIONS
            var combineIndexRemap  = new NativeArray<ushort>(CategoryRoutingRow.Length, Allocator.Temp);
#endif
            var mergeClassOfRow    = new NativeArray<int>(touchingBrushCount, Allocator.Temp);                 // MergeEquivalentRows: the class of each row
            var mergeClassRows     = new NativeArray<int>(CategoryRoutingRow.Length, Allocator.Temp);          // MergeEquivalentRows: the first row of each class of one node
            var rowLimit           = (rowsPerNodeCopyLimit > 0) ? math.min(rowsPerNodeCopyLimit, kMaxRowsPerNodeCopy) : kMaxRowsPerNodeCopy;
            try
            {
    			var categoryStackNodeCount = GetStackNodes(processedNodeID, ref brushesTouchedByBrushValue,
                                                            ref routingTable,
                                                            ref compactTree.compactHierarchy,
                                                            compactTree.GetBrushContents(processedNodeID),
                                                            ref compactTree.brushIDValueToContents,
                                                            compactTree.minBrushIDValue,
                                                            ref queuedEvents,
                                                            ref tempStackArray,
                                                            ref combineUsedIndices,
    #if USE_OPTIMIZATIONS
                                                            ref combineIndexRemap,
    #endif
                                                            ref mergeClassOfRow,
                                                            ref mergeClassRows,
                                                            rowLimit,
                                                            ref routingSteps);
                if (categoryStackNodeCount < 0)
                {
                    Debug.LogError($"Chisel: brush {processedNodeID.slotIndex.index} is nested too deeply to route: a brush it touches needs more than {rowLimit} rows in its routing table. The brush's surfaces are left out.");
                    return;
                }

                var totalInputsSize = 16 + (categoryStackNodeCount * UnsafeUtility.SizeOf<ushort>());
                var totalRoutingRowsSize = 16 + (categoryStackNodeCount * UnsafeUtility.SizeOf<CategoryRoutingRow>());
                var totalLookupsSize = 16 + (categoryStackNodeCount * UnsafeUtility.SizeOf<RoutingLookup>());
                var totalNodesSize = 16 + (categoryStackNodeCount * UnsafeUtility.SizeOf<int>());
                var totalSize = totalInputsSize + totalRoutingRowsSize + totalLookupsSize + totalNodesSize;

    			using var builder = new BlobBuilder(Allocator.Temp, totalSize);
                ref var root = ref builder.ConstructRoot<RoutingTable>();
                var routingRows = builder.Allocate(ref root.routingRows, categoryStackNodeCount);

                // TODO: clean up
                int nodeCounter = 1;
                routingRows[0] = routingTable[0].routingRow;
                var prevNodeID = routingTable[0].NodeIDValue;
                for (int i = 1; i < categoryStackNodeCount; i++)
                {
                    routingRows[i] = routingTable[i].routingRow;
                    var curNodeID = routingTable[i].NodeIDValue;
                    if (prevNodeID != curNodeID)
                        nodeCounter++;
                    prevNodeID = curNodeID;
                }

                var routingLookups = builder.Allocate(ref root.routingLookups, nodeCounter);

                {
                    // TODO: clean up
                    nodeCounter = 0;
                    for (int i = 0; i < categoryStackNodeCount;)
                    {
                        var cuttingNodeID = routingTable[i].NodeIDValue;
                        int startIndex = i;
                        i++;
                        while (i < categoryStackNodeCount && routingTable[i].NodeIDValue == cuttingNodeID)
                            i++;
                        int endIndex = i;

                        routingLookups[nodeCounter] = new RoutingLookup { startIndex = startIndex, endIndex = endIndex };
                        nodeCounter++;
                    }

                    int maxNodeID = 0;
                    int minNodeID = 0;
                    for (int i = 0; i < nodeCounter; i++)
                    {
                        var NodeID = routingTable[routingLookups[i].startIndex].NodeIDValue;
                        minNodeID = math.min(minNodeID, NodeID);
                        maxNodeID = math.max(maxNodeID, NodeID);
                    }
                    root.nodeIDOffset = minNodeID;

                    var indexToTableIndexCount = (maxNodeID + 1) - minNodeID;
                    var nodeIDToTableIndex = builder.Allocate(ref root.nodeIDToTableIndex, indexToTableIndexCount);
                    for (int i = 0; i < indexToTableIndexCount; i++)
                        nodeIDToTableIndex[i] = -1;
                    for (int i = 0; i < nodeCounter; i++)
                        nodeIDToTableIndex[routingTable[routingLookups[i].startIndex].NodeIDValue - minNodeID] = i;

                    var routingTableBlob = builder.CreateBlobAssetReference<RoutingTable>(Allocator.Persistent); // Confirmed to be disposed
    				routingTableLookup[processedNodeOrder] = routingTableBlob;
                }
            }
            finally
            {
                routingTable.Dispose();
                tempStackArray.Dispose();
                routingSteps.Dispose();
                combineUsedIndices.Dispose();
#if USE_OPTIMIZATIONS
                combineIndexRemap.Dispose();
#endif
                mergeClassOfRow.Dispose();
                mergeClassRows.Dispose();
            }
        }


        enum EventType : int { GetStackNode, Combine, ListItem }
        [StructLayout(LayoutKind.Explicit)]
        struct QueuedEvent
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static QueuedEvent GetStackNode(int currIndex, int outputStartIndex, IntersectionType intersectionType)
            {
                return new QueuedEvent
                {
                    type                = EventType.GetStackNode,
                    currIndex           = currIndex,
                    intersectionType    = intersectionType,
                    outputStartIndex    = outputStartIndex
                };
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static QueuedEvent ListItem(int currIndex, int leftStackStartIndex, IntersectionType intersectionType)
            {
                return new QueuedEvent 
                {
                    type                = EventType.ListItem,
                    currIndex           = currIndex,
                    intersectionType    = intersectionType,
                    leftStackStartIndex = leftStackStartIndex
                };
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static QueuedEvent Combine(int currIndex, int leftHaveGoneBeyondSelf, int leftStackStartIndex, int rightStackStartIndex)
            {
                return new QueuedEvent
                {
                    type                    = EventType.Combine,
                    currIndex               = currIndex,
                    leftHaveGoneBeyondSelf  = leftHaveGoneBeyondSelf,
                    leftStackStartIndex     = leftStackStartIndex,
                    rightStackStartIndex    = rightStackStartIndex,
                };
            }

            [FieldOffset(0)] public EventType type;
            [FieldOffset(4)] public int currIndex;
            [FieldOffset(8)] public int leftHaveGoneBeyondSelf;
            [FieldOffset(8)] public IntersectionType intersectionType;
            [FieldOffset(12)] public int outputStartIndex;
            [FieldOffset(12)] public int leftStackStartIndex;
            [FieldOffset(16)] public int rightStackStartIndex;
        }

        // Returns the number of rows written to output, or -1 when a Combine refused because a node would need more
        // rows than a routing row can address.
        static int GetStackNodes(CompactNodeID processedNodeID,
                                 [NoAlias] ref BrushesTouchedByBrush            brushesTouchedByBrush,
                                 [NoAlias] ref NativeArray<CategoryStackNode>   output,
                                 [NoAlias] ref BlobArray<CompactHierarchyNode>  compactHierarchy,
                                 BrushContentsInfo                              processedContents,
                                 [NoAlias] ref BlobArray<BrushContentsInfo>     brushIDValueToContents,
                                 int                                            minBrushIDValue,
                                 [NoAlias] ref NativeArray<QueuedEvent>         queuedEvents,
                                 [NoAlias] ref NativeArray<CategoryStackNode>   tempStackArray,
                                 [NoAlias] ref NativeBitArray                   combineUsedIndices,
#if USE_OPTIMIZATIONS
                                 [NoAlias] ref NativeArray<ushort>              combineIndexRemap,
#endif
                                 [NoAlias] ref NativeArray<int>                 mergeClassOfRow,
                                 [NoAlias] ref NativeArray<int>                 mergeClassRows,
                                 int                                            rowLimit,
                                 [NoAlias] ref NativeArray<int>                 routingSteps)
        {
            int haveGoneBeyondSelf = 0;
            int outputLength = 0;
            int queuedEventCount = 0;
            queuedEvents[0] = QueuedEvent.GetStackNode(0, 0, brushesTouchedByBrush.Get(compactHierarchy[0].CompactNodeID));
            queuedEventCount++;
            //ref var compactHierarchy = ref compactTree.Value.compactHierarchy;
            while (queuedEventCount > 0)
            {
                var currEvent = queuedEvents[queuedEventCount - 1];
                queuedEventCount--;
              
                switch (currEvent.type)
                {
                    case EventType.GetStackNode:
                    {
                        var intersectionType = currEvent.intersectionType;
                        if (intersectionType == IntersectionType.NoIntersection ||
                            intersectionType == IntersectionType.InvalidValue)
                            break;

                        ref var currentNode = ref compactHierarchy[currEvent.currIndex];
                        var currentNodeID = currentNode.CompactNodeID;
                        if (currentNode.Type == CSGNodeType.Brush)
                        {
                            if (intersectionType == IntersectionType.AInsideB)
                            {
                                // processedNode lies wholly inside this brush. A brush of another type that doesn't
                                // remove its faces is looked through, as if it weren't there.
                                var contentsIndex = currentNodeID.slotIndex.index - minBrushIDValue;
                                var contents      = (contentsIndex >= 0 && contentsIndex < brushIDValueToContents.Length) ? brushIDValueToContents[contentsIndex] : default;
                                var routingRow    = ContentsRules.RemovesWhenInside(processedContents.Contents, contents.Contents, contents.Carving)
                                                  ? CategoryRoutingRow.AllInside : CategoryRoutingRow.AllOutside;
                                NativeCollectionHelpers.GrowToFit(ref output, outputLength + 1, keepContents: true);
                                output[outputLength] = new CategoryStackNode { NodeIDValue = currentNodeID.slotIndex.index, routingRow = routingRow };
                                outputLength++;
                                break;
                            }
                            if (intersectionType == IntersectionType.BInsideA) 
                            { 
                                NativeCollectionHelpers.GrowToFit(ref output, outputLength + 1, keepContents: true);
                                output[outputLength] = new CategoryStackNode { NodeIDValue = currentNodeID.slotIndex.index, routingRow = CategoryRoutingRow.AllOutside };
                                outputLength++;
                                break; 
                            }

                            // All surfaces of processedNode are aligned with it's own surfaces, so all categories are Aligned
                            if (processedNodeID == currentNode.CompactNodeID)
                            {
                                haveGoneBeyondSelf = 1; // We're currently "ON" our brush
                                NativeCollectionHelpers.GrowToFit(ref output, outputLength + 1, keepContents: true);
                                output[outputLength] = new CategoryStackNode { NodeIDValue = currentNodeID.slotIndex.index, routingRow = CategoryRoutingRow.AllSelfAligned };
                                outputLength++;
                                break;
                            }  

                            if (haveGoneBeyondSelf > 0)
                                haveGoneBeyondSelf = 2; // We're now definitely beyond our brush

                            // Otherwise return identity categories (input == output)
                            NativeCollectionHelpers.GrowToFit(ref output, outputLength + 1, keepContents: true);
                            output[outputLength] = new CategoryStackNode { NodeIDValue = currentNodeID.slotIndex.index, routingRow = CategoryRoutingRow.Identity };
                            outputLength++;
                            break;
                        }

                        var nodeCount = currentNode.childCount;
                        if (nodeCount == 0)
                            break;

                        // Skip all nodes that are not additive at the start of the branch since they will never produce any geometry
                        var firstIndex = currentNode.childOffset;
                        var lastIndex  = firstIndex + nodeCount;
                        while (firstIndex < lastIndex && (compactHierarchy[firstIndex].Operation != CSGOperationType.Additive &&
                                                          compactHierarchy[firstIndex].Operation != CSGOperationType.Copy))
                            firstIndex++;

                        if ((lastIndex - firstIndex) <= 0) // no nodes left to process, nothing is visible
                            break;


                        // Note: Events are executed in reverse order, so the last one added is run first
                        var leftStackStartIndex = currEvent.outputStartIndex;
                        outputLength = leftStackStartIndex;

                        for (int i = lastIndex - 1; i >= firstIndex + 1; i--)
                        {
                            // This needs to be it's own event since we need to use intermediate data to create the next event

                            // 2. Combine the left stack (previous output stack) with the right stack
                            ref var childNode   = ref compactHierarchy[i];
                            var childNodeID     = childNode.CompactNodeID;
                            var childIntersectionType = brushesTouchedByBrush.Get(childNodeID);
                            if (childIntersectionType != IntersectionType.NoIntersection &&
                                childIntersectionType != IntersectionType.InvalidValue)
                            {
                                queuedEvents[queuedEventCount] = QueuedEvent.ListItem(i, leftStackStartIndex, childIntersectionType);
                                queuedEventCount++;
                            } else
                            if (childNode.Operation == CSGOperationType.Intersecting)
                            {
                                queuedEvents[queuedEventCount] = QueuedEvent.ListItem(i, leftStackStartIndex, IntersectionType.NoIntersection);
                                queuedEventCount++;
                            }
                        }

                        // 1. Get the first stack, which gets stored in output
                        ref var firstChildNode = ref compactHierarchy[firstIndex];
                        var firstChildNodeID = firstChildNode.CompactNodeID;
                        var firstChildIntersectionType = brushesTouchedByBrush.Get(firstChildNodeID);
                        if (firstChildIntersectionType != IntersectionType.NoIntersection &&
                            firstChildIntersectionType != IntersectionType.InvalidValue)
                        {
                            queuedEvents[queuedEventCount] = QueuedEvent.GetStackNode(firstIndex, leftStackStartIndex, firstChildIntersectionType);
                            queuedEventCount++;
                        }
                        break;
                    }

                    case EventType.ListItem:
                    {
                        var leftHaveGoneBeyondSelf = haveGoneBeyondSelf;
                        var rightStackStartIndex = outputLength;
                        // Note: Events are executed in reverse order, so the last one added is run first

                        // 2. Combine the left stack (previous output stack) with the right stack
                        queuedEvents[queuedEventCount] = QueuedEvent.Combine(currEvent.currIndex, leftHaveGoneBeyondSelf, currEvent.leftStackStartIndex, rightStackStartIndex);
                        queuedEventCount++;

                        // 1. Add the right stack to the output stack
                        queuedEvents[queuedEventCount] = QueuedEvent.GetStackNode(currEvent.currIndex, rightStackStartIndex, currEvent.intersectionType);
                        queuedEventCount++;
                        break;
                    }


                    // Combine two stacks together, currently stored behind each other in output
                    //        [left stack              ][right stack               ]  
                    // [..... leftStackStartIndex ..... rightStackStartIndex ..... ] output
                    case EventType.Combine:
                    {
                        var operation = compactHierarchy[currEvent.currIndex].Operation;
                        if (operation == CSGOperationType.Invalid)
                            operation = CSGOperationType.Additive;

                        var leftCount   = currEvent.rightStackStartIndex - currEvent.leftStackStartIndex;
                        var rightCount  = outputLength - currEvent.rightStackStartIndex;
                                
                        if (leftCount == 0) // left node has a branch without children or children are not intersecting with processedNode
                        {
                            if (rightCount == 0) // right node has a branch without children or children are not intersecting with processedNode
                            {
                                // Nothing to do, both stacks are empty
                                outputLength = currEvent.leftStackStartIndex;
                                continue;
                            }
                            switch (operation)
                            {
                                case CSGOperationType.Additive:
                                case CSGOperationType.Copy:
                                {
                                    // Output stack already contains only the right stack, which is what we want
                                    continue;
                                }
                                default:
                                {
                                    // Remove both the left and rightStack, which is stored after the leftStack
                                    outputLength = currEvent.rightStackStartIndex;
                                    continue;
                                }
                            }
                        } else
                        if (rightCount == 0) // right node has a branch without children or children are not intersecting with processedNode
                        {
                            switch (operation)
                            {
                                case CSGOperationType.Additive:
                                case CSGOperationType.Copy:
                                case CSGOperationType.Subtractive:
                                {
                                    // Remove the rightStack, which is stored after the leftStack
                                    outputLength = currEvent.rightStackStartIndex;
                                    continue;
                                }
                                default:
                                {
                                    // Remove both the left and rightStack, which is stored after the leftStack
                                    outputLength = currEvent.leftStackStartIndex;
                                    continue;
                                }
                            }
                        }

                        // We have both a left and a right stack at this point, but we need to write in the left stack.
                        // So we move the rightStack to it's own NativeArray 
                        var rightStackLength = outputLength - currEvent.rightStackStartIndex;
                        // Overwritten whole by the copy below, so it grows without keeping anything. The handle is taken AFTER
                        // growing, because growing replaces the array.
                        NativeCollectionHelpers.GrowToFit(ref tempStackArray, rightStackLength, keepContents: false);
                        var rightStack = tempStackArray;
                        rightStack.CopyFrom(output, currEvent.rightStackStartIndex, rightStackLength);
                        // ... and remove it from the leftStack
                        outputLength = currEvent.rightStackStartIndex;

                        if (!Combine(ref output,     currEvent.leftHaveGoneBeyondSelf, currEvent.leftStackStartIndex, ref outputLength,
                                     ref rightStack, haveGoneBeyondSelf, rightStackLength,
                                     operation,
                                     ref compactHierarchy,
                                     ref combineUsedIndices,
#if USE_OPTIMIZATIONS
                                     ref combineIndexRemap,
#endif
                                     ref mergeClassOfRow,
                                     ref mergeClassRows,
                                     rowLimit,
                                     ref routingSteps))
                            return -1;
                        break;
                    }
                }
            }

            if (outputLength == 0)
            {
                NativeCollectionHelpers.GrowToFit(ref output, outputLength + 1, keepContents: true);
                output[outputLength] = new CategoryStackNode { NodeIDValue = processedNodeID.slotIndex.index, routingRow = CategoryRoutingRow.AllOutside };
                outputLength++;
            }
#if SHOW_DEBUG_MESSAGES
            Dump(processedNodeID, output, outputLength);
#endif
            return outputLength;
        }



        static bool Combine([NoAlias] ref NativeArray<CategoryStackNode> leftStack, int leftHaveGoneBeyondSelf, int leftStackStart, ref int leftStackEnd,
                            [NoAlias] ref NativeArray<CategoryStackNode> rightStack, int rightHaveGoneBeyondSelf, int rightStackLength,
                            CSGOperationType operation,
                            [NoAlias] ref BlobArray<CompactHierarchyNode>   compactHierarchy,
                            [NoAlias] ref NativeBitArray                    combineUsedIndices,
#if USE_OPTIMIZATIONS
                            [NoAlias] ref NativeArray<ushort>               combineIndexRemap,
#endif
                            [NoAlias] ref NativeArray<int>                  mergeClassOfRow,
                            [NoAlias] ref NativeArray<int>                  mergeClassRows,
                            int                                             rowLimit,
                            [NoAlias] ref NativeArray<int>                  routingSteps)
        {
            //Debug.Assert(rightStackLength > 0);

            var leftStackCount  = leftStackEnd - leftStackStart;
            var firstNodeID     = rightStack[0].NodeIDValue;

            int routingStepsLength = 0;
            {
                // Count the number of rows for unique node
                var rightNodeID = firstNodeID;
                int counter     = 1;
                for (int r = 1; r < rightStackLength; r++)
                {
                    if (rightNodeID != rightStack[r].NodeIDValue)
                    {
                        NativeCollectionHelpers.GrowToFit(ref routingSteps, routingStepsLength + 1, keepContents: true);
                        routingSteps[routingStepsLength] = counter;
                        routingStepsLength++;
                        counter = 0;
                        rightNodeID = rightStack[r].NodeIDValue;
                    }
                    counter++;
                }
                NativeCollectionHelpers.GrowToFit(ref routingSteps, routingStepsLength + 1, keepContents: true);
                routingSteps[routingStepsLength] = counter;
                routingStepsLength++;

                for (int s = 0; s < routingStepsLength; s++)
                {
                    if (routingSteps[s] > rowLimit)
                        return false;
                }


                int startSearchRowIndex = leftStackStart + leftStackCount;
                int prevNodeIndex       = startSearchRowIndex - 1;
                int rightPartStart      = startSearchRowIndex;

                combineUsedIndices.Clear();
                if (leftStackCount == 0)
				{
					for (int t = 0; t < CategoryRoutingRow.Length; t++)
						SetUsed(ref combineUsedIndices, t);
					//combineUsedIndices.Set(0, true);
                    //combineUsedIndices.Set(1, true);
                    //combineUsedIndices.Set(2, true);
                    //combineUsedIndices.Set(3, true);
                } else
                {
                    while (prevNodeIndex > leftStackStart)
                    {
                        if (leftStack[prevNodeIndex - 1].NodeIDValue != leftStack[prevNodeIndex].NodeIDValue)
                            break;
                        prevNodeIndex--;
                    }

                    for (int p = prevNodeIndex; p < startSearchRowIndex; p++)
                    {
                        for (int t = 0; t < CategoryRoutingRow.Length; t++)
                            SetUsed(ref combineUsedIndices, (int)leftStack[p].routingRow[t]);
						//combineUsedIndices.Set((int)leftStack[p].routingRow.inside, true);
                        //combineUsedIndices.Set((int)leftStack[p].routingRow.aligned, true);
                        //combineUsedIndices.Set((int)leftStack[p].routingRow.reverseAligned, true);
                        //combineUsedIndices.Set((int)leftStack[p].routingRow.outside, true);
                    }
                }


                var outputStackStart    = leftStackStart;

#if HAVE_SELF_CATEGORIES
                var operationTableOffset = (int)operation;
#else
                var operationTableOffset = //(leftHaveGoneBeyondSelf >= 1 ?//&& rightStackLength == 1 ?
                                            //CategoryRoutingRow.RemoveOverlappingOffset : 0) +
                                            (leftHaveGoneBeyondSelf * CategoryRoutingRow.OperationCount) +
                                            (int)operation;
#endif

                int startRightStackRowIndex = 0;
                int routingLength           = routingSteps[0];
                for (int stackIndex = 1; stackIndex < routingStepsLength; stackIndex++)
                {
                    int routingStep             = routingSteps[stackIndex];
                    int endRightStackRowIndex   = startRightStackRowIndex + routingLength;

                    // duplicate route multiple times
                    for (int t = 0, vIndex = 0, inputRowIndex = 0, routingOffset = 0; t < CategoryRoutingRow.Length; t++, routingOffset += routingStep) // TODO: left table might not output every one of these?
                    {
                        for (var rightStackRowIndex = startRightStackRowIndex; rightStackRowIndex < endRightStackRowIndex; rightStackRowIndex++, vIndex++)
                        {
                            var routingRow = rightStack[rightStackRowIndex].routingRow + routingOffset; // Fix up routing to include offset b/c duplication
                            bool skip = !IsUsed(ref combineUsedIndices, vIndex);
#if USE_OPTIMIZATIONS
                            NativeCollectionHelpers.GrowToFit(ref combineIndexRemap, vIndex + 1, keepContents: true);
                            combineIndexRemap[vIndex] = skip ? (ushort)0 : 
#endif
                                AddRowToOutput(ref leftStack, ref leftStackEnd, startSearchRowIndex,
                                               ref inputRowIndex, in routingRow, rightStack[rightStackRowIndex].NodeIDValue);
                        }
                    }

#if USE_OPTIMIZATIONS
                    if (prevNodeIndex >= outputStackStart)
                    {
                        RemapIndices(leftStack, combineIndexRemap, prevNodeIndex, startSearchRowIndex);
                    }

                    combineIndexRemap.ClearValues();
                    combineUsedIndices.Clear();
                    for (int p = startSearchRowIndex; p < leftStackEnd; p++)
					{
#if HAVE_SELF_CATEGORIES
						SetUsed(ref combineUsedIndices, (int)leftStack[p].routingRow.inside);
                        SetUsed(ref combineUsedIndices, (int)leftStack[p].routingRow.aligned);
						SetUsed(ref combineUsedIndices, (int)leftStack[p].routingRow.selfAligned);
						SetUsed(ref combineUsedIndices, (int)leftStack[p].routingRow.selfReverseAligned);
						SetUsed(ref combineUsedIndices, (int)leftStack[p].routingRow.reverseAligned);
                        SetUsed(ref combineUsedIndices, (int)leftStack[p].routingRow.outside);
#else
						SetUsed(ref combineUsedIndices, (int)leftStack[p].routingRow.inside);
                        SetUsed(ref combineUsedIndices, (int)leftStack[p].routingRow.aligned);
						SetUsed(ref combineUsedIndices, (int)leftStack[p].routingRow.reverseAligned);
                        SetUsed(ref combineUsedIndices, (int)leftStack[p].routingRow.outside);
#endif
					}
#endif

					prevNodeIndex   = startSearchRowIndex;
                    startSearchRowIndex = leftStackEnd;
                    startRightStackRowIndex += routingLength;
                    routingLength = routingStep;
                }

                {
                    int endRightStackRowIndex = startRightStackRowIndex + routingLength;

                    // Duplicate route multiple times, bake operation into table for last node
                    for (int t = 0, vIndex = 0, inputRowIndex = 0; t < CategoryRoutingRow.Length; t++) // TODO: left table might not output every one of these?
                    {
                        var leftCategoryIndex = (CategoryIndex)t;
                        for (var rightStackRowIndex = startRightStackRowIndex; rightStackRowIndex < endRightStackRowIndex; rightStackRowIndex++, vIndex++)
                        {
                            // Fix up output of last node to include operation between last left and last right.
                            // We don't add a routingOffset here since this is last node & we don't have a destination beyond this point
							var routingRow = new CategoryRoutingRow(operationTableOffset, leftCategoryIndex, rightStack[rightStackRowIndex].routingRow); // applies operation
                            var skip = !IsUsed(ref combineUsedIndices, vIndex);
#if USE_OPTIMIZATIONS
                            NativeCollectionHelpers.GrowToFit(ref combineIndexRemap, vIndex + 1, keepContents: true);
                            combineIndexRemap[vIndex] = skip ? (ushort)0 : 
#endif
                                AddRowToOutput(ref leftStack, ref leftStackEnd, startSearchRowIndex, 
                                               ref inputRowIndex, in routingRow, rightStack[rightStackRowIndex].NodeIDValue);
                        }
                    }
                }

#if USE_OPTIMIZATIONS
                if (prevNodeIndex >= outputStackStart)
                {
                    RemapIndices(leftStack, combineIndexRemap, prevNodeIndex, startSearchRowIndex);

                    bool allEqual = true;
                    combineIndexRemap.ClearValues();
                    for (int i = startSearchRowIndex; i < leftStackEnd; i++)
                    {
                        if (!leftStack[i].routingRow.AreAllTheSame())
                        {
                            allEqual = false;
                            break;
                        }
                        NativeCollectionHelpers.GrowToFit(ref combineIndexRemap, (int)leftStack[i].Input + 1, keepContents: true);
                        combineIndexRemap[(int)leftStack[i].Input] = (ushort)(((int)leftStack[i].routingRow.inside) + 1);
                    }
                    if (allEqual)
                    {
                        leftStackEnd = startSearchRowIndex;
                        RemapIndices(leftStack, combineIndexRemap, prevNodeIndex, startSearchRowIndex);
                    }
                }
#endif

                MergeEquivalentRows(ref leftStack, outputStackStart, ref leftStackEnd, rightPartStart,
                                    ref mergeClassOfRow, ref mergeClassRows);

#if USE_OPTIMIZATIONS
                // When all the paths for the first node lead to the same destination, just remove it
                int lastRemoveCount = outputStackStart;
                while (lastRemoveCount < leftStackEnd - 1 &&
                        leftStack[lastRemoveCount].NodeIDValue != leftStack[lastRemoveCount + 1].NodeIDValue &&
                        leftStack[lastRemoveCount].routingRow.AreAllValue(0))
                    lastRemoveCount++;
                if (lastRemoveCount > outputStackStart)
                {
                    // Unfortunately there's a Collections version out there that adds RemoveRange to NativeList,
                    // but used (begin, end) instead of (begin, count), which is inconsistent with List<>
                    var removeCount = lastRemoveCount - outputStackStart;
                    leftStack.RemoveRange(outputStackStart, removeCount, ref leftStackEnd);
                }
#endif
            }
            return true;
        }


        static void MergeEquivalentRows([NoAlias] ref NativeArray<CategoryStackNode> stack, int stackStart, ref int stackEnd, int rightPartStart,
                                        [NoAlias] ref NativeArray<int> classOfRow, [NoAlias] ref NativeArray<int> classRows)
        {
            // classOfRow[i]: the new index of row i in its node when row i is kept, the complement (~) of the index of
            // the row it was merged into when it is not.
            NativeCollectionHelpers.GrowToFit(ref classOfRow, stackEnd, keepContents: false);

            var regionStart         = stackEnd;     // the first row of the first node the walk looked at
            var nextNodeStart       = stackEnd;
            var nextNodeRowCount    = 0;
            var nextNodeChanged     = false;        // did the node after this one merge or renumber rows?
            var nodeEnd             = stackEnd;
            while (nodeEnd > stackStart)
            {
                var nodeID    = stack[nodeEnd - 1].NodeIDValue;
                var nodeStart = nodeEnd - 1;
                while (nodeStart > stackStart && stack[nodeStart - 1].NodeIDValue == nodeID)
                    nodeStart--;

                if (nodeStart < rightPartStart && nodeEnd != rightPartStart && !nextNodeChanged)
                    break;

                var classCount = 0;
                var changed    = false;
                for (int i = nodeStart; i < nodeEnd; i++)
                {
                    if (nextNodeChanged)
                    {
                        var row = stack[i];
                        var routingRow = row.routingRow;
                        row.routingRow = new CategoryRoutingRow(
                            RemapDestination(routingRow.inside,             ref classOfRow, nextNodeStart, nextNodeRowCount),
                            RemapDestination(routingRow.aligned,            ref classOfRow, nextNodeStart, nextNodeRowCount),
                            RemapDestination(routingRow.selfAligned,        ref classOfRow, nextNodeStart, nextNodeRowCount),
                            RemapDestination(routingRow.selfReverseAligned, ref classOfRow, nextNodeStart, nextNodeRowCount),
                            RemapDestination(routingRow.reverseAligned,     ref classOfRow, nextNodeStart, nextNodeRowCount),
                            RemapDestination(routingRow.outside,            ref classOfRow, nextNodeStart, nextNodeRowCount));
                        stack[i] = row;
                    }

                    var sameAs = -1;
                    for (int c = 0; c < classCount; c++)
                    {
                        if (stack[classRows[c]].routingRow.Equals(stack[i].routingRow))
                        {
                            sameAs = c;
                            break;
                        }
                    }
                    if (sameAs >= 0)
                    {
                        classOfRow[i] = ~sameAs;
                        changed = true;
                        continue;
                    }
                    NativeCollectionHelpers.GrowToFit(ref classRows, classCount + 1, keepContents: true);
                    classRows[classCount] = i;
                    classOfRow[i] = classCount;
                    classCount++;
                }

                regionStart      = nodeStart;
                nextNodeStart    = nodeStart;
                nextNodeRowCount = nodeEnd - nodeStart;
                nextNodeChanged  = changed;
                nodeEnd          = nodeStart;
            }

            // Drop the merged rows and give the kept ones their new index in their node
            var write = regionStart;
            for (int i = regionStart; i < stackEnd; i++)
            {
                var index = classOfRow[i];
                if (index < 0)
                    continue;
                var row = stack[i];
                row.Input = (ushort)index;
                stack[write] = row;
                write++;
            }
            stackEnd = write;
        }

        // The row of the next node a destination now means. A destination that is not a row of the next node - only a
        // Copy operation writes one, as the Invalid sentinel - is left as it is.
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static ushort RemapDestination(ushort destination, [NoAlias] ref NativeArray<int> classOfRow, int nextNodeStart, int nextNodeRowCount)
        {
            if (destination >= nextNodeRowCount)
                return destination;
            var index = classOfRow[nextNodeStart + destination];
            return (ushort)(index >= 0 ? index : ~index);
        }


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static void SetUsed([NoAlias] ref NativeBitArray used, int index)
        {
            NativeCollectionHelpers.GrowToFit(ref used, index + 1);
            used.Set(index, true);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static bool IsUsed([NoAlias] ref NativeBitArray used, int index)
        {
            return index < used.Length && used.IsSet(index);
        }

		[BurstDiscard]
        static void FailureMessage()
        {
            Debug.LogError("Unity Burst Compiler is broken");
        }

        static ushort AddRowToOutput([NoAlias] ref NativeArray<CategoryStackNode> outputStack, ref int outputLength, int startSearchRowIndex,
                                  ref int input, [NoAlias] in CategoryRoutingRow routingRow, int nodeID)
        {
#if USE_OPTIMIZATIONS
            for (int n = startSearchRowIndex; n < outputLength; n++)
            {
                //Debug.Assert(nodeIndex == outputStack[n].nodeIndex);
                
                // We don't want to add identical rows, so if we find one, return it's input index
                if (outputStack[n].routingRow.Equals(routingRow))
                    return (ushort)((int)outputStack[n].Input + 1); 
            }
#endif
            NativeCollectionHelpers.GrowToFit(ref outputStack, outputLength + 1, keepContents: true);
            outputStack[outputLength] = new CategoryStackNode
            {
                Input       = (ushort)input,
                routingRow  = routingRow,
                NodeIDValue = nodeID
            };
            outputLength++;
            input++;
            // NOTE: we return the input row index + 1 so 0 (uninitialized value) is invalid
            return (ushort)input;
        }

        // Remap indices to new destinations, used when destination rows have been merged
#if USE_OPTIMIZATIONS
        static void RemapIndices([NoAlias] NativeArray<CategoryStackNode> stack, [NoAlias] NativeArray<ushort> remap, int start, int last)
        {
            for (int i = start; i < last; i++)
            {
                var categoryRow = stack[i];
                var routingRow = categoryRow.routingRow;

                {
                    var key = (int)routingRow.inside;
                    if (key >= remap.Length || remap[key] == 0) { FailureMessage(); return; }
                }

                {
                    var key = (int)routingRow.aligned;
                    if (key >= remap.Length || remap[key] == 0) { FailureMessage(); return; }
				}

#if HAVE_SELF_CATEGORIES
				{
					var key = (int)routingRow.selfAligned;
					if (key >= remap.Length || remap[key] == 0) { FailureMessage(); return; }
				}

				{
					var key = (int)routingRow.selfReverseAligned;
					if (key >= remap.Length || remap[key] == 0) { FailureMessage(); return; }
				}
#endif

				{
                    var key = (int)routingRow.reverseAligned;
                    if (key >= remap.Length || remap[key] == 0) { FailureMessage(); return; }
                }

                {
                    var key = (int)routingRow.outside;
                    if (key >= remap.Length || remap[key] == 0) { FailureMessage(); return; }
                }

#if HAVE_SELF_CATEGORIES
				categoryRow.routingRow = new CategoryRoutingRow(
                        (ushort)(remap[(int)routingRow.inside            ] - 1),
                        (ushort)(remap[(int)routingRow.aligned           ] - 1),
                        (ushort)(remap[(int)routingRow.selfAligned       ] - 1),
                        (ushort)(remap[(int)routingRow.selfReverseAligned] - 1),
                        (ushort)(remap[(int)routingRow.reverseAligned    ] - 1),
                        (ushort)(remap[(int)routingRow.outside           ] - 1)
                    );
#else
				categoryRow.routingRow = new CategoryRoutingRow(
                        (ushort)(remap[(int)routingRow.inside        ] - 1),
                        (ushort)(remap[(int)routingRow.aligned       ] - 1),
                        (ushort)(remap[(int)routingRow.reverseAligned] - 1),
                        (ushort)(remap[(int)routingRow.outside       ] - 1)
                    );
#endif
                stack[i] = categoryRow;
            }
        }
#endif

#if SHOW_DEBUG_MESSAGES
        static void Dump(int processedNodeID, NativeArray<CategoryStackNode> stack, int stackLength, int depth = 0)
        {
            var space = new String(' ', depth*4);
            if (stackLength == 0)
            {
                Debug.Log($"{space}processedNode: {processedNodeID} stack.Count == 0");
                return;
            }
            var stringBuilder = new System.Text.StringBuilder();
            for (int i = 0; i < stackLength; i++)
            {
                if (i == 0 || stack[i - 1].nodeIndex != stack[i].nodeIndex)
                    stringBuilder.AppendLine($"  --- nodeIndex: {stack[i].nodeIndex}");
                stringBuilder.AppendLine($"\t{i,-3} -\t[{(int)stack[i].input,-3}]: {stack[i].routingRow.ToString(false)}");
            }
            Debug.LogWarning($"{space}processedNode: {processedNodeID}\n{stringBuilder.ToString()}");
        }
#endif
            }
		}
