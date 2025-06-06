using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using Chisel.Core;
using UnityEngine;
using UnityEngine.Pool;
using UnityEngine.Profiling;
using UnityEngine.SceneManagement;


/*
    goals:
    - fast detection of changes in transform hierarchy
        - parent chain
        - sibling index
            - also at root
        - keep in mind 
            - make sure it works when unity starts up / loading scene
            - adding/removing/enabling/disabling components, incl. in between nodes
            - adding/removing/activating/deactivating gameObjects
            - composite passthrough mode
            - undo/redo
            - mind prefabs
            - multiple scenes
                - also keep in mind never dirtying non modified scenes
            - default TreeNode (model) per scene
*/
namespace Chisel.Components
{
    // TODO: rewrite this, shouldn't be static & have more control over lifetime
    public static class ChiselNodeHierarchyManager
	{
		[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
		static void ResetState()
		{
		    sceneHierarchies.Clear();

            nonNodeChildren.Clear();
            registeredNodes.Clear();
            nodeToinstanceIDLookup.Clear();

            componentLookup.Clear();
            hierarchyItemLookup.Clear();
            treeNodeLookup.Clear();

            registerQueueLookup.Clear();
            registerQueue.Clear();
            unregisterQueueLookup.Clear();
            unregisterQueue.Clear();

            findChildrenQueue.Clear();

            destroyNodesList.Clear();

            addToHierarchyLookup.Clear();
            addToHierarchyQueue.Clear();

            rebuildTreeNodes.Clear();

            rebuildBrushMeshes.Clear();
            rebuildTreeBrushes.Clear();
            rebuildTreeBrushOutlines.Clear();
            rebuildSurfaceDefinitions.Clear();

            updateTransformationNodes.Clear();

            updateChildrenQueue.Clear();
            updateChildrenQueueList.Clear();
            sortChildrenQueue.Clear();

            hierarchyUpdateQueue.Clear();
            siblingIndexUpdateQueue.Clear();
            siblingIndexUpdateQueueSkip.Clear();
            parentUpdateQueue.Clear();
            onHierarchyChangeCalled.Clear();

            ignoreNextChildrenChanged = false;
            firstStart = false;
            prefabInstanceUpdatedEvent = false;

			prevPlaying = false;
	    }

	    public readonly static Dictionary<int, ChiselSceneHierarchy> sceneHierarchies = new();

		readonly static HashSet<ChiselNodeComponent> nonNodeChildren = new();
		readonly static HashSet<ChiselNodeComponent> registeredNodes = new();
		readonly static Dictionary<ChiselNodeComponent, int> nodeToinstanceIDLookup = new();

		// Note: keep in mind that these work even when components have already been destroyed
		readonly static Dictionary<Transform, ChiselNodeComponent> componentLookup = new();
		readonly static Dictionary<ChiselNodeComponent, ChiselHierarchyItem> hierarchyItemLookup = new();
		readonly static Dictionary<ChiselNodeComponent, CSGTreeNode> treeNodeLookup = new();

		readonly static HashSet<ChiselNodeComponent> registerQueueLookup = new();
		readonly static List<ChiselNodeComponent> registerQueue = new();
		readonly static HashSet<ChiselNodeComponent> unregisterQueueLookup = new();
		readonly static List<ChiselNodeComponent> unregisterQueue = new();

		readonly static List<ChiselHierarchyItem> findChildrenQueue = new();

		readonly static HashSet<CSGTreeNode> destroyNodesList = new();

		readonly static Dictionary<ChiselNodeComponent, ChiselHierarchyItem> addToHierarchyLookup = new();
		readonly static List<ChiselHierarchyItem> addToHierarchyQueue = new(5000);

		readonly static HashSet<ChiselNodeComponent> rebuildTreeNodes = new();

		readonly static Dictionary<ChiselBrushComponent, int> rebuildBrushMeshes = new();
		readonly static List<CSGTreeBrush> rebuildTreeBrushes = new();
		readonly static List<BrushMesh> rebuildTreeBrushOutlines = new();
		readonly static List<ChiselSurfaceArray> rebuildSurfaceDefinitions = new();

		readonly static HashSet<ChiselNodeComponent> updateTransformationNodes = new();

		readonly static HashSet<ChiselHierarchyItem> updateChildrenQueue = new();
		readonly static List<ChiselHierarchyItem> updateChildrenQueueList = new();
		readonly static HashSet<List<ChiselHierarchyItem>> sortChildrenQueue = new();

		readonly static HashSet<ChiselNodeComponent> hierarchyUpdateQueue = new();
		readonly static HashSet<ChiselHierarchyItem> siblingIndexUpdateQueue = new();
		readonly static HashSet<ChiselHierarchyItem> siblingIndexUpdateQueueSkip = new();
		readonly static HashSet<ChiselHierarchyItem> parentUpdateQueue = new();
		readonly static HashSet<ChiselNodeComponent> onHierarchyChangeCalled = new();

        public static bool ignoreNextChildrenChanged = false;
        public static bool firstStart = false;
        public static bool prefabInstanceUpdatedEvent = false;

        public static event Action NodeHierarchyModified;
        public static event Action NodeHierarchyReset;
        public static event Action TransformationChanged;

		readonly static Dictionary<Transform, ChiselNodeComponent> s_TransformNodeLookup = new();
		internal static bool prevPlaying = false;

#if UNITY_EDITOR
		[UnityEditor.InitializeOnLoadMethod]
#endif
        [RuntimeInitializeOnLoadMethod]
        public static void Initialize()
        {
            ChiselNodeHierarchyManager.firstStart = false;
        }

        // TODO: Clean up API
        public static void Rebuild()
        {
            double endTime, startTime;




            var log = new System.Text.StringBuilder();
            double resetTime = 0;
            double updateModelsTime = 0;
            double updateVisibilityTime = 0;
            double fullTime = 0;
            try
            {
                var fullStartTime = Time.realtimeSinceStartupAsDouble;

                startTime = fullStartTime;
                Profiler.BeginSample("CSGManager.Clear");
                Chisel.Core.CompactHierarchyManager.Clear();
                ChiselNodeHierarchyManager.FindAndReregisterAllNodes();
                ChiselNodeHierarchyManager.UpdateAllTransformations();
                ChiselNodeHierarchyManager.Update();
                Profiler.EndSample();
                endTime = Time.realtimeSinceStartupAsDouble;
                resetTime = (endTime - startTime) * 1000;

                startTime = endTime;
                Profiler.BeginSample("UpdateModels");
				ChiselModelManager.Instance.UpdateModels();
                Profiler.EndSample();
                endTime = Time.realtimeSinceStartupAsDouble;
                updateModelsTime = (endTime - startTime) * 1000;

                startTime = endTime;
                Profiler.BeginSample("UpdateVisibility");
				ChiselUnityVisibilityManager.UpdateVisibility(force: true);
                Profiler.EndSample();
                endTime = Time.realtimeSinceStartupAsDouble;
                updateVisibilityTime = (endTime - startTime) * 1000;

                var fullEndTime = endTime;
                fullTime = (fullEndTime - fullStartTime) * 1000;
            }
            finally
            {

                log.AppendFormat("Full CSG rebuild: {0:0.00} ms", fullTime);
                log.AppendFormat("  Reinitialize: {0:0.00} ms", resetTime);
                log.AppendFormat("  Build meshes: {0:0.00} ms", updateModelsTime);
                log.AppendFormat("  Cleanup: {0:0.00} ms", updateVisibilityTime);
                Debug.LogFormat(LogType.Log, LogOption.NoStacktrace, null, log.ToString());
            }
        }

        internal static void Reset()
        {
            sceneHierarchies.Clear();


            foreach (var item in registeredNodes)
                item.ResetTreeNodes();

            registeredNodes.Clear();
            nonNodeChildren.Clear();
            nodeToinstanceIDLookup.Clear();

            componentLookup.Clear();
            hierarchyItemLookup.Clear();
            treeNodeLookup.Clear();

            foreach (var item in destroyNodesList)
                item.Destroy();

            ClearQueues();

			ChiselModelManager.Instance.Reset();

			NodeHierarchyReset?.Invoke();
		}

        static void ClearQueues()
        {
            registerQueueLookup.Clear();
            registerQueue.Clear();
            unregisterQueueLookup.Clear();
            unregisterQueue.Clear();

            findChildrenQueue.Clear();

            destroyNodesList.Clear();

            addToHierarchyLookup.Clear();
            addToHierarchyQueue.Clear();

            rebuildTreeNodes.Clear();

            updateTransformationNodes.Clear();

            updateChildrenQueue.Clear();
            updateChildrenQueueList.Clear();
            sortChildrenQueue.Clear();
            hierarchyUpdateQueue.Clear();

            rebuildBrushMeshes.Clear();
        }

        // *Workaround*
        // Unfortunately prefabs do not always send out all the necessary events
        // so we need to go through all the nodes and assume they've changed
        public static void OnPrefabInstanceUpdated(GameObject instance)
        {
            var nodes = instance.GetComponentsInChildren<ChiselNodeComponent>();
            for (int i = 0; i < nodes.Length; i++)
            {
                var node = nodes[i];
                if (registeredNodes.Contains(node))
                    OnTransformParentChanged(node);
                else
                    Register(node);
            }

            // Figure out if a components have been removed or created
            prefabInstanceUpdatedEvent = true;
        }

        public static void Register(ChiselNodeComponent component)
        {
            if (!component)
                return;

            // NOTE: this method is called from constructor and cannot use Debug.Log, get Transforms etc.

            if (unregisterQueueLookup.Remove(component))
                unregisterQueue.Remove(component);

            if (registerQueueLookup.Add(component))
            {
                if (addToHierarchyLookup.Remove(component))
                    addToHierarchyQueue.Remove(component.hierarchyItem);
                registerQueue.Add(component);
            }

            component.hierarchyItem.Registered = true;
        }

        public static void Unregister(ChiselNodeComponent component)
        {
            // NOTE: this method is called from destructor and cannot use Debug.Log, get Transforms etc.

            if (registerQueueLookup.Remove(component))
            {
                registerQueue.Remove(component);
            }

            if (unregisterQueueLookup.Add(component))
            {
                ChiselHierarchyItem hierarchyItem;
                // we can't get the hierarchyItem from our component since it might've already been destroyed
                if (addToHierarchyLookup.TryGetValue(component, out hierarchyItem))
                {
                    addToHierarchyLookup.Remove(component);
                    addToHierarchyQueue.Remove(component.hierarchyItem);
                }

                unregisterQueue.Add(component);
            }

            if (ReferenceEquals(component, null))
                return;

            var children = component.hierarchyItem.Children;
            for (int n = 0; n < children.Count; n++)
            {
                if (!children[n].Component || !children[n].Component.IsActive)
                    continue;
                hierarchyUpdateQueue.Add(children[n].Component);
                updateTransformationNodes.Add(children[n].Component);
            }

            component.hierarchyItem.Registered = false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void UpdateAvailability(ChiselNodeComponent node)
        {
            if (!node.IsActive)
            {
                ChiselNodeHierarchyManager.Unregister(node);
            } else
            {
                ChiselNodeHierarchyManager.Register(node);
            }
        }

        public static void OnTransformParentChanged(ChiselNodeComponent node)
        {
            if (!node ||
                !node.hierarchyItem.Registered ||
                !node.IsActive)
            {
                nonNodeChildren.Remove(node);
                return;
            }

            if (!(node is ChiselModelComponent) &&
                // If our parent is not a ChiselNode, add it to the nonNodeChildren.
                // Regulare ChiselNode parents capture an event that significies that the order
                // of child nodes may have changed. But for parent gameobjects that are not 
                // ChiselNodes we cannot do this. So we put all those in a different list to handle 
                // them in a different (slower) way
                (node.hierarchyItem.parentComponent == null ||
                 node.hierarchyItem.Transform.parent != node.hierarchyItem.Parent.Transform))
                nonNodeChildren.Add(node);
            hierarchyUpdateQueue.Add(node);
        }


        // Let the hierarchy manager know that this/these node(s) has/have moved, so we can regenerate meshes
        [MethodImpl(MethodImplOptions.AggressiveInlining)] 
        public static void RebuildTreeNodes(ChiselNodeComponent node) { if (node) rebuildTreeNodes.Add(node); }
        [MethodImpl(MethodImplOptions.AggressiveInlining)] 
        public static void UpdateTreeNodeTransformation(ChiselNodeComponent node) { if (node) updateTransformationNodes.Add(node); }
        [MethodImpl(MethodImplOptions.AggressiveInlining)] 
        public static void NotifyTransformationChanged(HashSet<ChiselNodeComponent> nodes) { foreach (var node in nodes) if (node) updateTransformationNodes.Add(node); }
        [MethodImpl(MethodImplOptions.AggressiveInlining)] 
        public static void UpdateAllTransformations() { foreach (var node in registeredNodes) if (node) updateTransformationNodes.Add(node); }


        // Let the hierarchy manager know that the contents of this node has been modified
        //	so we can rebuild/update sub-trees and regenerate meshes
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void NotifyContentsModified(ChiselNodeComponent node)
        {
            node.hierarchyItem.SetBoundsDirty();
            node.TopTreeNode.SetDirty();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static void UpdateGeneratedBrushes(ChiselNodeComponent node)
        {
            node.UpdateBrushMeshInstances();
        }

        public static void OnBrushMeshUpdate(ChiselBrushComponent component, ref CSGTreeNode node)
        {
            var brush = (CSGTreeBrush)node;
            if (!brush.Valid)
                return;

            if (rebuildBrushMeshes.TryGetValue(component, out int index))
            {
                rebuildTreeBrushes[index] = brush;
                rebuildTreeBrushOutlines[index] = component.definition.BrushOutline;
                rebuildSurfaceDefinitions[index] = component.surfaceArray;
            } else
            {
                rebuildBrushMeshes[component] = rebuildTreeBrushOutlines.Count;
                rebuildTreeBrushes.Add(brush);
                rebuildTreeBrushOutlines.Add(component.definition.BrushOutline);
                rebuildSurfaceDefinitions.Add(component.surfaceArray);
            }
            rebuildTreeNodes.Add(component);
        }

        static void UpdateBrushMeshes()
        {
            BrushMeshManager.ConvertBrushMeshesToBrushMeshInstances(rebuildTreeBrushes, rebuildTreeBrushOutlines, rebuildSurfaceDefinitions);

            rebuildBrushMeshes.Clear();
            rebuildTreeBrushes.Clear();
            rebuildTreeBrushOutlines.Clear();
            rebuildSurfaceDefinitions.Clear();
        }


        public static void OnTransformChildrenChanged(ChiselNodeComponent component)
        {
            if (ignoreNextChildrenChanged)
            {
                ignoreNextChildrenChanged = false;
                return;
            }

            if (component.hierarchyItem != null &&
                component.hierarchyItem.SiblingIndices != null)
            {
                foreach (var child in component.hierarchyItem.Children)
                {
                    if (child.UpdateSiblingIndices(ignoreWhenParentIsChiselNode: false))
                        hierarchyUpdateQueue.Add(child.Component);
                }
            }

            if (onHierarchyChangeCalled.Contains(component))
                return;

            onHierarchyChangeCalled.Add(component);

            if (!component ||
                !component.hierarchyItem.Registered ||
                !component.IsActive)
                return;

            var children = component.hierarchyItem.Children;
            for (int i = 0; i < children.Count; i++)
            {
                var childComponent = children[i].Component;
                if (!childComponent || !childComponent.IsActive)
                {
                    continue;
                }
                hierarchyUpdateQueue.Add(childComponent);
                onHierarchyChangeCalled.Add(component);
            }
        }

        // Find first parent chiselNode & find siblingIndices up to the model + keep track how many siblingIndices we have until we reach the chiselNode 
        // Note that we also keep track of the siblingIndices of transforms between the first parent ChiselNode and our own Component, since the Component
        // might have several gameobjects as parents without any chiselNode, but we still need these siblingIndices to sort properly.
        static ChiselNodeComponent UpdateSiblingIndices(ChiselHierarchyItem hierarchyItem)
        {
            var transform = hierarchyItem.Transform;
            if (!transform)
                return null;

            var parent = transform.parent;

            hierarchyItem.SiblingIndices.Clear();
            hierarchyItem.SiblingIndices.Add(transform.GetSiblingIndex());
            hierarchyItem.siblingIndicesUntilNode = 1;

            if (ReferenceEquals(parent, null))
            {
                //Debug.Log($"{hierarchyItem.Component.name} null {hierarchyItem.SiblingIndices.Count}");
                return null;
            }

            // Find siblingIndices up to the model
            ChiselNodeComponent firstParentComponent = null;
            do
            {
                // Store the index of our parent
                hierarchyItem.SiblingIndices.Insert(0, parent.GetSiblingIndex());

                // If we haven't found a node before, increase our counter to determine how many siblingIndices we have until the next ChiselNode
                if (firstParentComponent == null)
                    hierarchyItem.siblingIndicesUntilNode++;

                // See if our parent is a ChiselNode
                if (componentLookup.TryGetValue(parent, out var parentComponent))
                {
                    // If we haven't found a node before and our node is not a Composite PassthTough node, store it
                    if (firstParentComponent == null)
                    {
                        var composite = parentComponent as ChiselCompositeComponent;
                        if (composite == null || !composite.PassThrough)
                            firstParentComponent = parentComponent;
                    }

                    // If we found the model, quit
                    if (parentComponent is ChiselModelComponent)
                        break;
                }
                // Find the parent of our last parent
                parent = parent.parent;

                // If this is our last parent, it means we don't have a model and can't go any further
            } while (!ReferenceEquals(parent, null));

            // Return the first ChiselNode parent
            return firstParentComponent;
        }

        static void UpdateChildren(Transform rootTransform)
		{
			var __transforms = QueuePool<Transform>.Get();
			try
            {
                if (rootTransform.parent == null &&
				    ChiselModelManager.Instance.IsDefaultModel(rootTransform))
                {
                    // The default model is special in the sense that, unlike all other models, it doesn't
                    // simply contain all the nodes that are its childrens. Instead, it contains all the nodes 
                    // that do not have a model as a parent. So we go through the hierarchy and find the top
                    // level nodes that are not a model
                    var scene = rootTransform.gameObject.scene;
                    var rootGameObjects = ListPool<GameObject>.Get();
                    scene.GetRootGameObjects(rootGameObjects);
                    for (int i = 0; i < rootGameObjects.Count; i++)
                    {
                        var childTransform = rootGameObjects[i].transform;
                        var childNode = childTransform.GetComponentInChildren<ChiselNodeComponent>();
                        if (!childNode)
                            continue;
                        if (childNode is ChiselModelComponent)
                            continue;
                        __transforms.Enqueue(childTransform);
                        hierarchyUpdateQueue.Add(childNode);
                    }
                    ListPool<GameObject>.Release(rootGameObjects);
                } else
                    __transforms.Enqueue(rootTransform);

                while (__transforms.Count > 0)
                {
                    var transform = __transforms.Dequeue();
                    for (int i = 0; i < transform.childCount; i++)
                    {
                        var childTransform = transform.GetChild(i);
                        var childNode = childTransform.GetComponent<ChiselNodeComponent>();
                        if (!childNode || !childNode.IsActive)
                        {
                            __transforms.Enqueue(childTransform);
                            continue;
                        }
                        hierarchyUpdateQueue.Add(childNode);
                    }
                }
            }
            finally
			{
				QueuePool<Transform>.Release(__transforms);
			}
        }

        static void SetChildScenes(ChiselHierarchyItem hierarchyItem, Scene scene)
        {
            // Note: we're only setting the scene here.
            //		 we're not updating the hierarchy, that's done in hierarchyUpdateQueue/addToHierarchyQueue
            var __hierarchyQueueLists = QueuePool<List<ChiselHierarchyItem>>.Get();
            try
            {
                __hierarchyQueueLists.Enqueue(hierarchyItem.Children);
                while (__hierarchyQueueLists.Count > 0)
                {
                    var children = __hierarchyQueueLists.Dequeue();
                    for (int i = 0; i < children.Count; i++)
                    {
                        var childItem = children[i];
                        var childNode = childItem.Component;
                        if (!childNode || !childNode.IsActive)
                        {
                            __hierarchyQueueLists.Enqueue(childItem.Children);
                            continue;
                        }
                        if (childItem.Scene != scene)
                        {
                            childItem.Scene = scene;
                            hierarchyUpdateQueue.Add(childNode);
                        }
                    }
                }
            }
            finally
            {
				QueuePool<List<ChiselHierarchyItem>>.Release(__hierarchyQueueLists);
            }
        }

        static void AddChildNodesToHashSet(HashSet<ChiselNodeComponent> allFoundChildren)
		{
			var __hierarchyQueueLists = QueuePool<List<ChiselHierarchyItem>>.Get();
			try
            {
                foreach (var node in allFoundChildren)
                    __hierarchyQueueLists.Enqueue(node.hierarchyItem.Children);
                while (__hierarchyQueueLists.Count > 0)
                {
                    var children = __hierarchyQueueLists.Dequeue();
                    for (int i = 0; i < children.Count; i++)
                    {
                        var childItem = children[i];
                        var childNode = childItem.Component;
                        if (!allFoundChildren.Add(childNode))
                            continue;
                        __hierarchyQueueLists.Enqueue(childItem.Children);
                    }
                }
            }
            finally
			{
				QueuePool<List<ChiselHierarchyItem>>.Release(__hierarchyQueueLists);
			}
        }

        static void TransformNodeLookupClear()
        {
            s_TransformNodeLookup.Clear();
        }

        static void RegisterTransformLookup(ChiselNodeComponent component)
        {
            var transform = component.hierarchyItem.Transform ? component.hierarchyItem.Transform : component.transform;
            if (transform == null)
                return;

            s_TransformNodeLookup[transform] = component;
        }

        static void RegisterInternal(ChiselNodeComponent component)
        {
            if (!component)
                return;

            int index = addToHierarchyQueue.Count;
            var parent = component.hierarchyItem.Transform ? component.hierarchyItem.Transform : component.transform;
            s_TransformNodeLookup[parent] = component;

            var parentComponent = component;
            do
            {
                if (parentComponent &&
                    parentComponent.IsActive)
                {
                    if (registeredNodes.Add(parentComponent))
                    {
                        var instanceID = parentComponent.GetInstanceID();
                        nodeToinstanceIDLookup[parentComponent] = instanceID;
                        var parentHierarchyItem = parentComponent.hierarchyItem;

                        addToHierarchyLookup[parentComponent] = parentHierarchyItem;
                        addToHierarchyQueue.Insert(index, parentHierarchyItem);

                        UpdateGeneratedBrushes(parentComponent);
                    }
                }

                parent = parent.parent;
                if (parent == null)
                    break;

                if (!s_TransformNodeLookup.TryGetValue(parent, out parentComponent))
                {
                    parent.TryGetComponent<ChiselNodeComponent>(out parentComponent);
                    s_TransformNodeLookup[parent] = parentComponent;
                }
            } while (true);
        }

        static bool RemoveFromHierarchy(List<ChiselHierarchyItem> rootItems, ChiselNodeComponent component)
		{
			var __hierarchyQueueLists = QueuePool<List<ChiselHierarchyItem>>.Get();
			try
			{
				__hierarchyQueueLists.Enqueue(rootItems);
				while (__hierarchyQueueLists.Count > 0)
                {
                    var children = __hierarchyQueueLists.Dequeue();
                    for (int i = 0; i < children.Count; i++)
                    {
                        if (children[i].Component == component)
                        {
                            children.RemoveAt(i);
                            return true;
                        }
                    }
                    for (int i = 0; i < children.Count; i++)
                    {
                        if (children[i].Children == null)
                            continue;

                        children[i].SetChildBoundsDirty();
                        __hierarchyQueueLists.Enqueue(children[i].Children);
                    }
                }
            }
            finally
			{
				QueuePool<List<ChiselHierarchyItem>>.Release(__hierarchyQueueLists);
			}
            return false;
        }

        static void UnregisterInternal(ChiselNodeComponent component)
        {
            if (!registeredNodes.Remove(component))
                return;

            var instanceID = nodeToinstanceIDLookup[component];
            nodeToinstanceIDLookup.Remove(component);

            var sceneHierarchy = component.hierarchyItem.sceneHierarchy;
            if (sceneHierarchy == null)
                return;

            var rootItems = sceneHierarchy.RootItems;
            RemoveFromHierarchy(rootItems, component);

            if (rootItems.Count == 0)
            {
                sceneHierarchies.Remove(sceneHierarchy.Scene.handle);
            }
        }

        static void FindAndReregisterAllNodes()
        {
            Reset();
            var children = ListPool<ChiselNodeComponent>.Get();
            var rootObjects = ListPool<GameObject>.Get();
            for (int s = 0; s < SceneManager.sceneCount; s++)
            {
                var scene = SceneManager.GetSceneAt(s);
                if (!scene.isLoaded)
                    continue;
                rootObjects.Clear();
                scene.GetRootGameObjects(rootObjects);
                for (int r = 0; r < rootObjects.Count; r++)
                {
                    var rootObject = rootObjects[r];
                    children.Clear();
                    rootObject.GetComponentsInChildren<ChiselNodeComponent>(includeInactive: false, children);
                    for (int n = 0; n < children.Count; n++)
                    {
                        var node = children[n];
                        if (node.isActiveAndEnabled)
                            Register(node);
                    }
                }
            }
            ListPool<GameObject>.Release(rootObjects);
            ListPool<ChiselNodeComponent>.Release(children);
        }

        public static void Update()
        {
            try
            {
                Profiler.BeginSample("UpdateTrampoline");
                UpdateTrampoline();
                Profiler.EndSample();
            }
            // If we get an exception we don't want to end up infinitely spawning this exception ..
            finally
            {
                ClearQueues();
            }
        }

        public static ChiselSceneHierarchy GetSceneHierarchyForScene(Scene scene)
        {
            var sceneHandle = scene.handle;
            if (sceneHierarchies.TryGetValue(sceneHandle, out ChiselSceneHierarchy sceneHierarchy))
                return sceneHierarchy;

            return sceneHierarchies[sceneHandle] = new ChiselSceneHierarchy { Scene = scene };
        }

		readonly static Comparison<ChiselHierarchyItem> s_CompareChiselHierarchyGlobalOrder = CompareChiselHierarchyGlobalOrder;
        static int CompareChiselHierarchyGlobalOrder(ChiselHierarchyItem x, ChiselHierarchyItem y)
        {
            var xIndices = x.SiblingIndices;
            var yIndices = y.SiblingIndices;
            if (xIndices.Count != yIndices.Count)
                return xIndices.Count - yIndices.Count;
            var count = xIndices.Count;
            for (int i = 0; i < count; i++)
            {
                var difference = xIndices[i].CompareTo(yIndices[i]);
                if (difference != 0)
                    return difference;
            }
            return 0;
        }

		readonly static Comparison<ChiselHierarchyItem> s_CompareChiselHierarchyParentOrder = CompareChiselHierarchyParentOrder;
        static int CompareChiselHierarchyParentOrder(ChiselHierarchyItem x, ChiselHierarchyItem y)
        {
            var xIndices = x.SiblingIndices;
            var yIndices = y.SiblingIndices;
            var xEnd = xIndices.Count;
            var yEnd = yIndices.Count;
            var xCount = x.siblingIndicesUntilNode;
            var yCount = y.siblingIndicesUntilNode;
            var xStart = xEnd - xCount;
            var yStart = yEnd - yCount;
            var count = Mathf.Min(xCount, yCount);
            for (int i = 0; i < count; i++)
            {
                var difference = xIndices[i + xStart].CompareTo(yIndices[i + yStart]);
                if (difference != 0)
                    return difference;
            }
            return 0;
        }

        public static void CheckOrderOfChildNodesModifiedOfNonNodeGameObject()
        {
            Profiler.BeginSample("CheckNodeOrderModified");
            // Check if order of nodes has changed
            foreach (var node in nonNodeChildren)
            {
                var hierarchyItem = node.hierarchyItem;
                if (hierarchyItem != null &&
                    hierarchyItem.SiblingIndices != null)
                {
                    if (hierarchyItem.UpdateSiblingIndices(ignoreWhenParentIsChiselNode: true))
                        hierarchyUpdateQueue.Add(node);
                }
            }
            Profiler.EndSample();
        }

        internal static void UpdateTrampoline()
        {
            Profiler.BeginSample("UpdateTrampoline.Setup");
#if UNITY_EDITOR
            // *Workaround*
            // Events are not properly called, and can be even duplicated, on entering and exiting playmode
            var currentPlaying = UnityEditor.EditorApplication.isPlaying;
            if (currentPlaying != prevPlaying)
            {
                prevPlaying = currentPlaying;
                return;
            }
#endif
            // *Workaround*
            // It's possible that some events aren't properly called at UnityEditor startup (possibly runtime as well)
            if (!firstStart)
            {
                firstStart = true;
                Chisel.Core.CompactHierarchyManager.Clear();

                // Prefabs can fire events that look like objects have been loaded/created ..
                // Also, starting up in the editor can swallow up events and cause some nodes to not be registered properly
                // So to ensure that the first state is correct, we get it explicitly
                FindAndReregisterAllNodes();
				ChiselUnityVisibilityManager.SetDirty();
            }

            // *Workaround*
            // Unity has no event to tell us if an object has moved between scenes
            // fortunately, when you change the parent of a gameobject, we get that event.
            // We still need to check the highest level objects though.
            foreach (var pair in sceneHierarchies)
            {
                var defaultModel = pair.Value.DefaultModel;
                if (defaultModel)
                {
                    var defaultChildren = defaultModel.hierarchyItem.Children;
                    var expectedScene = pair.Key;
                    for (int n = defaultChildren.Count - 1; n >= 0; n--)
                    {
                        if (!defaultChildren[n].GameObject)
                        {
                            var rootComponent = defaultChildren[n].Component;
                            defaultChildren.RemoveAt(n); // prevent potential infinite loops
                            Unregister(rootComponent);   // .. try to clean this up
                            continue;
                        }

                        if (defaultChildren[n].GameObject.scene.handle == expectedScene &&
                            defaultChildren[n].Scene.handle == expectedScene)
                            continue;

                        var component = defaultChildren[n].Component;
                        updateChildrenQueue.Add(defaultModel.hierarchyItem);
                        OnTransformParentChanged(component);
                    }
                }
                var rootItems = pair.Value.RootItems;
                if (rootItems.Count > 0)
                {
                    var expectedScene = pair.Key;
                    for (int n = rootItems.Count - 1; n >= 0; n--)
                    {
                        if (!rootItems[n].GameObject)
                        {
                            var rootComponent = rootItems[n].Component;
                            rootItems.RemoveAt(n);     // prevent potential infinite loops
                            Unregister(rootComponent); // .. try to clean this up
                            continue;
                        }

                        if (rootItems[n].GameObject.scene.handle == expectedScene &&
                            rootItems[n].Scene.handle == expectedScene)
                            continue;

                        var component = rootItems[n].Component;
                        OnTransformParentChanged(component);
                    }
                }
            }

            // *Workaround*
            // Prefabs can fire events that look like objects have been loaded/created ..
            // So we're forced to filter them out
            if (prefabInstanceUpdatedEvent)
            {
                prefabInstanceUpdatedEvent = false;
                for (int i = registerQueue.Count - 1; i >= 0; i--)
                {
                    var component = registerQueue[i];
                    if (component)
                    {
#if UNITY_EDITOR

                        if (UnityEditor.PrefabUtility.GetPrefabAssetType(component) != UnityEditor.PrefabAssetType.Regular)

#endif
                            continue;
                    }
                    registerQueue.RemoveAt(i);
                }

                var knownNodes = registeredNodes.ToArray();
                for (int i = 0; i < knownNodes.Length; i++)
                {
                    var knownNode = knownNodes[i];
                    if (knownNode)
                        continue;

                    Unregister(knownNode);
                }
            }

            if (registerQueue.Count != 0)
            {
                for (int i = registerQueue.Count - 1; i >= 0; i--)
                {
                    if (!registerQueue[i] ||
                        !registerQueue[i].isActiveAndEnabled)
                        registerQueue.RemoveAt(i);
                }
            }
            Profiler.EndSample();

#if !UNITY_EDITOR
            // In the chisel editor package we call this on the UnityEditor.EditorApplication.hierarchyChanged event
            CheckOrderOfChildNodesModifiedOfNonNodeGameObject();
#endif

            if (registerQueue.Count == 0 &&
                unregisterQueue.Count == 0 &&
                sortChildrenQueue.Count == 0 &&
                findChildrenQueue.Count == 0 &&
                hierarchyUpdateQueue.Count == 0 &&
                updateChildrenQueue.Count == 0 &&
                addToHierarchyQueue.Count == 0 &&
                updateTransformationNodes.Count == 0 &&
                destroyNodesList.Count == 0 &&
                rebuildTreeNodes.Count == 0 &&
                rebuildBrushMeshes.Count == 0)
                return;

ForceRerun:
            bool forceRerun = false;
            TransformNodeLookupClear();
            
            var registerNodes = HashSetPool<ChiselNodeComponent>.Get();
            var unregisterNodes = HashSetPool<ChiselNodeComponent>.Get();
            try
            { 

                if (rebuildTreeNodes.Count > 0)
                {
                    foreach (var component in rebuildTreeNodes)
                    {
                        if (!unregisterQueueLookup.Contains(component))
                        {
                            unregisterQueue.Add(component);
                        }
                        if (!registerQueueLookup.Contains(component))
                        {
                            registerQueue.Add(component);
                        }
                    }
                }

                Profiler.BeginSample("UpdateTrampoline.unregisterQueue");
                if (unregisterQueue.Count > 0)
                {
                    for (int i = 0; i < unregisterQueue.Count; i++)
                    {
                        var node = unregisterQueue[i];

                        // Remove any treeNodes that are part of the components we're trying to unregister 
                        // (including components that may have been already destroyed)
                        CSGTreeNode createdTreeNode;
                        if (treeNodeLookup.TryGetValue(node, out createdTreeNode))
                        {
                            if (createdTreeNode.Valid)
                                destroyNodesList.Add(createdTreeNode);
                            treeNodeLookup.Remove(node);
                        }
                    }

                    for (int i = 0; i < unregisterQueue.Count; i++)
                    {
                        var node = unregisterQueue[i];
                        ChiselHierarchyItem hierarchyItem;
                        if (hierarchyItemLookup.TryGetValue(node, out hierarchyItem))
                        {
                            unregisterNodes.Add(node);

                            hierarchyItemLookup.Remove(node);
                            if (hierarchyItem.Transform != null)
                                componentLookup.Remove(hierarchyItem.Transform);

                            var parentHierarchyItem	= hierarchyItem.Parent;
                            if (parentHierarchyItem != null)
                            {
                                if (parentHierarchyItem.Parent == null &&
									ChiselModelManager.Instance.IsDefaultModel(parentHierarchyItem.Component))
                                {
                                    var hierarchy = hierarchyItem.sceneHierarchy;
                                    if (hierarchy != null)
                                        hierarchy.RootItems.Remove(hierarchyItem);
                                }

                                parentHierarchyItem.SetChildBoundsDirty();
                                parentHierarchyItem.Children.Remove(hierarchyItem);
                                if (parentHierarchyItem.Component &&
                                    parentHierarchyItem.Component.IsActive)
                                {
                                    updateChildrenQueue.Add(parentHierarchyItem);
                                    if (parentHierarchyItem.Children.Count > 0)
                                    {
                                        sortChildrenQueue.Add(parentHierarchyItem.Children);
                                    }
                                }
                            } else
                            {
                                var hierarchy = hierarchyItem.sceneHierarchy;
                                if (hierarchy != null)
                                {
                                    hierarchy.RootItems.Remove(hierarchyItem);
                                    sortChildrenQueue.Add(hierarchy.RootItems);
                                }
                            }
                        }
                    }

                    for (int i = 0; i < unregisterQueue.Count; i++)
                    {
                        var node = unregisterQueue[i];
                        if (!node)
                            continue;

                        node.hierarchyItem.Scene = default;
                        node.ResetTreeNodes();
                        UnregisterInternal(node);
                    }
                
                    unregisterQueue.Clear();
                    unregisterQueueLookup.Clear();
                }
                Profiler.EndSample();
            
                Profiler.BeginSample("UpdateTrampoline.registerQueue");
                if (registerQueue.Count > 0)
                {
                    Profiler.BeginSample("UpdateTrampoline.registerQueue.A");
                    for (int i = 0; i < registerQueue.Count; i++)
                    {
                        var node = registerQueue[i];

                        // Remove any treeNodes that are part of the components we're trying to register 
                        // (including components that may have been already destroyed)
                        CSGTreeNode createdTreeNode;
                        if (treeNodeLookup.TryGetValue(node, out createdTreeNode))
                        {
                            if (createdTreeNode.Valid)
                                destroyNodesList.Add(createdTreeNode);
                            treeNodeLookup.Remove(node);
                        }
                    }
                    Profiler.EndSample();

                    // Initialize the components
                    Profiler.BeginSample("UpdateTrampoline.registerQueue.B");
                    for (int i = registerQueue.Count - 1; i >= 0; i--) // reversed direction because we're also potentially removing items
                    {
                        var node = registerQueue[i];
                        if (!node ||		// component might've been destroyed after adding it to the registerQueue
                            !node.IsActive)	// component might be active/enabled etc. after adding it to the registerQueue
                        {
                            registerQueue.RemoveAt(i);
                            continue;
                        }

                        {
                            var hierarchyItem	= node.hierarchyItem;
                            var transform		= hierarchyItem.Transform;
                            if (!hierarchyItem.Scene.IsValid())
                            {
                                hierarchyItem.Scene					= hierarchyItem.GameObject.scene;
                                hierarchyItem.LocalToWorldMatrix	= hierarchyItem.Transform.localToWorldMatrix;
                                hierarchyItem.WorldToLocalMatrix	= hierarchyItem.Transform.worldToLocalMatrix;
                                findChildrenQueue.Add(hierarchyItem);
                            }
                            updateTransformationNodes.Add(node);
                            componentLookup[transform]	= node;
                            hierarchyItemLookup[node]	= hierarchyItem;
                            if (unregisterNodes.Contains(node))
                            {
                                // we removed it before we added it, so nothing has actually changed
                                unregisterNodes.Remove(node);
                            } else
                                registerNodes.Add(node); 
                        }
                    }
                    Profiler.EndSample();

                    // Separate loop to ensure all parent components are already initialized
                    // this is because the order of the registerQueue is essentially random
                    Profiler.BeginSample("UpdateTrampoline.registerQueue.C");
                    foreach (var node in registeredNodes)
                    {
                        if (node)
                            RegisterTransformLookup(node);
                    }
                    for (int i = 0; i < registerQueue.Count; i++)
                    {
                        var node = registerQueue[i];
                        RegisterInternal(node);
                    }
                    Profiler.EndSample();

                    Profiler.BeginSample("UpdateTrampoline.registerQueue.D");
                    for (int i = 0; i < registerQueue.Count; i++)
                    {
                        var node = registerQueue[i];
                        if (!addToHierarchyLookup.ContainsKey(node))
                        {
                            addToHierarchyLookup.Add(node, node.hierarchyItem);
                            addToHierarchyQueue.Add(node.hierarchyItem);
                        }
                    }
                    Profiler.EndSample();

                    registerQueue.Clear();
                    registerQueueLookup.Clear();
                }
                Profiler.EndSample();

                Profiler.BeginSample("UpdateTrampoline.findChildrenQueue");
                if (findChildrenQueue.Count > 0)
                {
                    for (int i = 0; i < findChildrenQueue.Count; i++)
                    {
                        var hierarchyItem = findChildrenQueue[i];
                        UpdateChildren(hierarchyItem.Transform);
                    }
                    findChildrenQueue.Clear();
                }
                Profiler.EndSample();
        
                Profiler.BeginSample("UpdateTrampoline.hierarchyUpdateQueue");
                siblingIndexUpdateQueueSkip.Clear();
                parentUpdateQueue.Clear();
                if (addToHierarchyQueue.Count > 0)
                {
                    for (int i = addToHierarchyQueue.Count - 1; i >= 0; i--)
                    {
                        var hierarchyItem = addToHierarchyQueue[i];
                        if (!hierarchyItem.Component ||
                            !hierarchyItem.Component.IsActive)
                        {
                            addToHierarchyQueue.RemoveAt(i);
                            continue;
                        }

                        hierarchyItem.Component.ResetTreeNodes();
                    
                        var sceneHierarchy = GetSceneHierarchyForScene(hierarchyItem.Scene);
                        hierarchyItem.sceneHierarchy = sceneHierarchy;
                    
                        hierarchyItem.parentComponent = UpdateSiblingIndices(hierarchyItem);
                        if (hierarchyItem.parentComponent != null &&
                            hierarchyItem.parentComponent.hierarchyItem != null)
                        {
                            parentUpdateQueue.Add(hierarchyItem.parentComponent.hierarchyItem);
                        }
                        siblingIndexUpdateQueueSkip.Add(hierarchyItem);
                    }
                    foreach(var parent in parentUpdateQueue)
                    {
                        var children = parent.Children;
                        siblingIndexUpdateQueue.EnsureCapacity(siblingIndexUpdateQueue.Count + children.Count);
                        for (int c = 0; c < children.Count; c++)
                            siblingIndexUpdateQueue.Add(children[c]);
                    }
                    foreach (var node in addToHierarchyQueue)
                        hierarchyUpdateQueue.Add(node.Component);
                }
                if (hierarchyUpdateQueue.Count > 0)
                {
                    var prevUpdateQueue = ListPool<ChiselNodeComponent>.Get();
                    prevUpdateQueue.AddRange(hierarchyUpdateQueue);
                    for (int i = 0; i < prevUpdateQueue.Count; i++)
                    {
                        var component = prevUpdateQueue[i];
                        if (!component ||
                            !component.IsActive)
                            continue;

                        var hierarchyItem	= component.hierarchyItem;
                        if (!hierarchyItem.Scene.IsValid())
                        {
                            hierarchyItem.Scene				 = hierarchyItem.GameObject.scene;
                            hierarchyItem.LocalToWorldMatrix = hierarchyItem.Transform.localToWorldMatrix;
                            hierarchyItem.WorldToLocalMatrix = hierarchyItem.Transform.worldToLocalMatrix;
                            UpdateChildren(hierarchyItem.Transform);
                        } else
                        {
                            // Determine if our node has been moved to another scene
                            var currentScene = hierarchyItem.GameObject.scene;
                            if (currentScene != hierarchyItem.Scene)
                            {
                                hierarchyItem.Scene = currentScene;
                                SetChildScenes(hierarchyItem, currentScene);
                            }
                        }
                        updateTransformationNodes.Add(component);
                    }
                    ListPool<ChiselNodeComponent>.Release(prevUpdateQueue);

                    foreach (var component in hierarchyUpdateQueue)
                    {
                        //Debug.Log($"hierarchyUpdateQueue {component}", component);
                        if (!component)
                            continue;

                        // make sure we update our old parent
                        var hierarchyItem		= component.hierarchyItem;
                        var parentHierarchyItem	= hierarchyItem.Parent;
                        if (parentHierarchyItem != null)
                        {
                            if (parentHierarchyItem.Parent == null &&
								ChiselModelManager.Instance.IsDefaultModel(parentHierarchyItem.Component))
                            {
                                var hierarchy = hierarchyItem.sceneHierarchy;
                                if (hierarchy != null)
                                    hierarchy.RootItems.Remove(hierarchyItem);
                            }

                            parentHierarchyItem.SetChildBoundsDirty();
                            parentHierarchyItem.Children.Remove(hierarchyItem);
                            if (parentHierarchyItem.Component &&
                                parentHierarchyItem.Component.IsActive)
                            {
                                updateChildrenQueue.Add(parentHierarchyItem);
                                if (parentHierarchyItem.Children.Count > 0)
                                {
                                    sortChildrenQueue.Add(parentHierarchyItem.Children);
                                }
                            }
                        } else
                        {
                            var hierarchy = hierarchyItem.sceneHierarchy;
                            if (hierarchy != null)
                            {
                                hierarchy.RootItems.Remove(hierarchyItem);
                                sortChildrenQueue.Add(hierarchy.RootItems);
                            }
                        }

                        if (!addToHierarchyLookup.ContainsKey(hierarchyItem.Component))
                        {
                            addToHierarchyLookup.Add(hierarchyItem.Component, hierarchyItem);
                            addToHierarchyQueue.Add(hierarchyItem);

                            if (hierarchyItem.Component &&
                                hierarchyItem.Component.IsActive)
                            {
                                hierarchyItem.Component.ResetTreeNodes();

                                var sceneHierarchy = GetSceneHierarchyForScene(hierarchyItem.Scene);
                                hierarchyItem.sceneHierarchy = sceneHierarchy;

                                hierarchyItem.parentComponent = UpdateSiblingIndices(hierarchyItem);
                                if (hierarchyItem.parentComponent != null &&
                                    hierarchyItem.parentComponent.hierarchyItem != null)
                                {
                                    foreach (var child in hierarchyItem.parentComponent.hierarchyItem.Children)
                                        siblingIndexUpdateQueue.Add(child);
                                }
                                siblingIndexUpdateQueueSkip.Add(hierarchyItem);
                            }
                        }
                    }
                    hierarchyUpdateQueue.Clear();
                }
                if (siblingIndexUpdateQueueSkip.Count > 0)
                {
                    foreach (var item in siblingIndexUpdateQueueSkip)
                        siblingIndexUpdateQueue.Remove(item);
                    siblingIndexUpdateQueueSkip.Clear();
                }
                if (siblingIndexUpdateQueue.Count > 0)
                {
                    siblingIndexUpdateQueue.Clear();
                    foreach (var hierarchyItem in siblingIndexUpdateQueue)
                    {
                        hierarchyItem.parentComponent = UpdateSiblingIndices(hierarchyItem);
                        if (ReferenceEquals(hierarchyItem.parentComponent, null))
                        {
                            if (!(hierarchyItem.Component is ChiselModelComponent))
                            {
                                hierarchyItem.parentComponent = hierarchyItem.sceneHierarchy.GetOrCreateDefaultModel(out var created);
                                if (created) forceRerun = true;
                            }
                        }
                    }
                    siblingIndexUpdateQueue.Clear();
                }
                Profiler.EndSample();

                Profiler.BeginSample("UpdateTrampoline.addToHierarchyQueue(1)");
                for (int i = addToHierarchyQueue.Count - 1; i >= 0; i--)
                {
                    var hierarchyItem = addToHierarchyQueue[i];
                    if (!hierarchyItem.Component ||
                        !hierarchyItem.Component.IsActive)
                    {
                        addToHierarchyQueue.RemoveAt(i);
                        continue;
                    }
                }

                for (int i = 0; i < addToHierarchyQueue.Count; i++)
                {
                    var hierarchyItem = addToHierarchyQueue[i];
                    var sceneHierarchy = hierarchyItem.sceneHierarchy;
                    hierarchyItem.parentComponent = UpdateSiblingIndices(hierarchyItem);
                    if (ReferenceEquals(hierarchyItem.parentComponent, null))
                    {
                        if (!(hierarchyItem.Component is ChiselModelComponent))
                        {
                            hierarchyItem.parentComponent = sceneHierarchy.GetOrCreateDefaultModel(out var created);
                            if (created) forceRerun = true;
                        }
                    }

                    if (hierarchyItem.parentComponent == sceneHierarchy.DefaultModel)
                    {
                        if (sceneHierarchy.RootItems.Contains(hierarchyItem))
                            sceneHierarchy.RootItems.Remove(hierarchyItem);
                    }
                }
                Profiler.EndSample();

                Profiler.BeginSample("UpdateTrampoline.addToHierarchyQueue(2)");
                if (addToHierarchyQueue.Count > 0)
                {
                    for (int i = addToHierarchyQueue.Count - 1; i >= 0; i--)
                    {
                        var hierarchyItem = addToHierarchyQueue[i];
                        var sceneHierarchy = hierarchyItem.sceneHierarchy;
                        if (!ReferenceEquals(hierarchyItem.parentComponent, null))
                        {
                            var parentHierarchyItem = hierarchyItem.parentComponent.hierarchyItem;
                            hierarchyItem.Parent = parentHierarchyItem;

                            if (!parentHierarchyItem.Children.Contains(hierarchyItem))
                            {
                                parentHierarchyItem.SetChildBoundsDirty();
                                parentHierarchyItem.Children.Add(hierarchyItem);
                            }
                        
                            var iterator = parentHierarchyItem;
                            do
                            {
                                if (iterator.Children.Count > 0)
                                {
                                    sortChildrenQueue.Add(iterator.Children);
                                }
                                if (iterator.Component.IsContainer) 
                                {
                                    updateChildrenQueue.Add(iterator);
                                    break;
                                }
                                iterator = iterator.Parent;
                            } while (iterator != null);
                        } else
                        { 
                            hierarchyItem.Parent = null;
                            if (!sceneHierarchy.RootItems.Contains(hierarchyItem))
                            {
                                sceneHierarchy.RootItems.Add(hierarchyItem);
                            }
                        
                            sortChildrenQueue.Add(sceneHierarchy.RootItems);
                        }
                    }
                    addToHierarchyQueue.Clear();
                    addToHierarchyLookup.Clear();
                }
                Profiler.EndSample();
             
                Profiler.BeginSample("UpdateTrampoline.sortChildrenQueue");
                if (sortChildrenQueue.Count > 0)
                {
                    foreach (var items in sortChildrenQueue)
                    {
                        //Debug.Log($"sortChildrenQueue {items.Count}");
                        if (items.Count > 1)
                            continue;

                        items.Sort(s_CompareChiselHierarchyParentOrder);
                    }
                    sortChildrenQueue.Clear();
                }
                Profiler.EndSample();

                Profiler.BeginSample("UpdateTrampoline.updateChildrenQueue");
                if (updateChildrenQueue.Count > 0)
                {
                    foreach (var item in updateChildrenQueue)
                    {
                        //Debug.Log($"updateChildrenQueue {item.Component}", item.Component);

                        if (!item.Component)
                            continue;
                    
                        if (!item.Component.IsContainer)
                            continue;
                    
                        // TODO: create a virtual updateChildrenQueue list for the default model instead?
                        if (item.Children.Count == 0 &&
							ChiselModelManager.Instance.IsDefaultModel(item.Component))
                        {
                            var itemModel = item.Component as ChiselModelComponent;

                            // If the default model is empty, we'll destroy it to remove clutter
                            var sceneHandle = item.Scene.handle;
                            ChiselSceneHierarchy sceneHierarchy;
                            if (sceneHierarchies.TryGetValue(sceneHandle, out sceneHierarchy))
                            {
                                if (sceneHierarchy.DefaultModel == itemModel)
                                {
                                    sceneHierarchy.DefaultModel = null;
                                }
                                sceneHierarchy.RootItems.Remove(itemModel.hierarchyItem);
                            }
                            destroyNodesList.Add(itemModel.Node);
                            ChiselObjectUtility.SafeDestroy(item.GameObject);
                            continue;
                        }
                    }
                }
                Profiler.EndSample();
        
                Profiler.BeginSample("UpdateTrampoline.destroyNodesList");
                if (destroyNodesList.Count > 0)
                {
                    //Debug.Log($"destroyNodesList {destroyNodesList.Count}");

                    // Destroy all old nodes after we created new nodes, to make sure we don't get conflicting IDs
                    // TODO: add 'generation' to indices to avoid needing to do this
                    foreach (var item in destroyNodesList)
                    {
                        if (item.Valid)
                            item.Destroy();
                    }
                    destroyNodesList.Clear();
                }
                Profiler.EndSample();
        
                Profiler.BeginSample("UpdateTrampoline.updateChildrenQueue2");
                if (updateChildrenQueue.Count > 0)
                {
                    Profiler.BeginSample("UpdateTrampoline.updateChildrenQueue2.init");
                    foreach (var hierarchyItem in updateChildrenQueue)
                    {
                        if (!hierarchyItem.Component ||
                            !hierarchyItem.Component.IsActive)
                            continue;
                    
                        updateChildrenQueueList.Add(hierarchyItem);
                    }
                    Profiler.EndSample();

                    // TODO: fix this hack and rewrite the whole update queue here
                    {
                        Profiler.BeginSample("UpdateTrampoline.updateChildrenQueue2.RebuildTreeNodes");
                        for (int i = updateChildrenQueueList.Count - 1; i >= 0; i--)
                        {
                            var hierarchyItem = updateChildrenQueueList[i];

                            var parentComponent = hierarchyItem.Component;
                            if (!parentComponent)
                                continue;

                            var parentTreeNode = parentComponent.TopTreeNode;
                            if (!parentTreeNode.Valid)
                            {
                                var node = hierarchyItem.Component;
                                parentTreeNode = node.RebuildTreeNodes();

                                if (parentTreeNode.Valid)
                                    treeNodeLookup[node] = parentTreeNode;
                                else
                                    treeNodeLookup.Remove(node);
                            }
                        }
                        Profiler.EndSample();

                        Profiler.BeginSample("UpdateTrampoline.updateChildrenQueue2.AddChildrenOfHierarchyItem");
                        for (int i = updateChildrenQueueList.Count - 1; i >= 0; i--)
                        {
                            var hierarchyItem = updateChildrenQueueList[i];

                            var parentComponent = hierarchyItem.Component;
                            if (!parentComponent)
                                continue;

                            var parentTreeNode = parentComponent.TopTreeNode;
                            if (!parentTreeNode.Valid)
                                continue;

                            if (!parentComponent.IsContainer ||
                                hierarchyItem.Children.Count == 0)
                                continue;

                            AddChildrenOfHierarchyItem(hierarchyItem, updateChildrenQueueList);
                        }
                        Profiler.EndSample();
                    }

                    Profiler.BeginSample("UpdateTrampoline.updateChildrenQueue2.sort");
                    updateChildrenQueueList.Sort(s_CompareChiselHierarchyGlobalOrder);
                    Profiler.EndSample();

                    Profiler.BeginSample("UpdateTrampoline.updateChildrenQueue2.process");
                    for (int i = 0; i < updateChildrenQueueList.Count; i++)
                    {
                        var hierarchyItem = updateChildrenQueueList[i];

                        var parentComponent = hierarchyItem.Component;
                        if (!parentComponent)
                            continue;

                        if (!parentComponent.IsContainer ||
                            hierarchyItem.Children.Count == 0)
                            continue;

                        var parentTreeNode = parentComponent.TopTreeNode;                    
                        if (!parentTreeNode.Valid)
                        {
                            //Debug.LogWarning($"SetChildren called on a {nameof(ChiselComposite)} ({parentComponent}) that isn't properly initialized", parentComponent);
                            continue;
                        }

                        Profiler.BeginSample("UpdateTrampoline.updateChildrenQueue2.process.GetChildrenOfHierarchyItem");
                        var childNodes = ListPool<CSGTreeNode>.Get();
                        childNodes.Clear();
                        GetChildrenOfHierarchyItemNoAlloc(hierarchyItem, childNodes);
                        Profiler.EndSample();
                        if (childNodes.Count > 0)
                        {
                            Profiler.BeginSample("UpdateTrampoline.updateChildrenQueue2.process.SetChildren");
                            try
                            {
                                if (!parentTreeNode.SetChildren(childNodes))
                                    Debug.LogError($"Failed to assign list of children to {parentComponent.ChiselNodeTypeName}", parentComponent);
                            }
                            catch (Exception ex)
                            {
                                Debug.LogException(ex, hierarchyItem.Component);
                            }
                            Profiler.EndSample();
                        }
                        ListPool<CSGTreeNode>.Release(childNodes);
                    }
                    Profiler.EndSample();

                    updateChildrenQueue.Clear();
                    updateChildrenQueueList.Clear();
                }
                Profiler.EndSample();

                Profiler.BeginSample("UpdateTrampoline.updateTransformationNodes");
                if (updateTransformationNodes.Count > 0)
                {
                    // Make sure we also update the child node matrices
                    AddChildNodesToHashSet(updateTransformationNodes);
                    foreach (var node in updateTransformationNodes)
                    {
                        if (node == null)
                            continue;
                        node.UpdateTransformation();
                        node.hierarchyItem.SetBoundsDirty();
                    }
                    if (TransformationChanged != null)
                        TransformationChanged();
                    updateTransformationNodes.Clear();
                }
                Profiler.EndSample();

                Profiler.BeginSample("UpdateTrampoline.__unregisterNodes");
                if (unregisterNodes.Count > 0)
                {
                    foreach (var node in unregisterNodes)
						ChiselModelManager.Instance.Unregister(node);
                    unregisterNodes.Clear();
                }
                Profiler.EndSample();

                Profiler.BeginSample("UpdateTrampoline.__registerNodes");
                if (registerNodes.Count > 0)
                {
                    foreach (var node in registerNodes)
						ChiselModelManager.Instance.Register(node);
                    registerNodes.Clear();
                }
                Profiler.EndSample();

                // When we're forced to create a default model during this loop, this creates changes. 
                // This forces us to go through everything again, to ensure those changes are properly handled.
                if (forceRerun)
                {
                    forceRerun = false;
                    goto ForceRerun;
                }

                UpdateBrushMeshes();

                Profiler.BeginSample("UpdateTrampoline.End");
                registerNodes	.Clear();
                unregisterNodes	.Clear();

                var prevSceneHierarchy = ListPool<KeyValuePair<int, ChiselSceneHierarchy>>.Get();
                prevSceneHierarchy.AddRange(sceneHierarchies);
                for (int i = 0; i < prevSceneHierarchy.Count; i++)
                {
                    ChiselSceneHierarchy sceneHierarchy = prevSceneHierarchy[i].Value;
                    if (!sceneHierarchy.Scene.IsValid() ||
                        !sceneHierarchy.Scene.isLoaded ||
                        (sceneHierarchy.RootItems.Count == 0 && !sceneHierarchy.DefaultModel))
                    {
                        sceneHierarchies.Remove(prevSceneHierarchy[i].Key);
                    }
                }
                ListPool<KeyValuePair<int, ChiselSceneHierarchy>>.Release(prevSceneHierarchy);

                // Used to redraw windows etc.
                NodeHierarchyModified?.Invoke(); // TODO: Should only call this when necessary!!!
                Profiler.EndSample();
            }
            finally
            {
                HashSetPool<ChiselNodeComponent>.Release(registerNodes);
                HashSetPool<ChiselNodeComponent>.Release(unregisterNodes);
            }
        }


        public static void AddChildrenOfHierarchyItem(ChiselHierarchyItem item, List<ChiselHierarchyItem> updateNodes)
        {
            if (item == null)
                return;

			var knownNodes = HashSetPool<ChiselHierarchyItem>.Get();
            try
            {
                knownNodes.Clear();
                knownNodes.EnsureCapacity(updateNodes.Count);
                for (int i = 0; i < updateNodes.Count; i++)
                    knownNodes.Add(updateNodes[i]);

                if (updateNodes.Capacity < updateNodes.Count + item.Children.Count)
                    updateNodes.Capacity = updateNodes.Count + item.Children.Count;

                for (int i = 0; i < item.Children.Count; i++)
                {
                    var childHierarchyItem = item.Children[i];
                    var childComponent = childHierarchyItem.Component;
                    if (!childComponent && childComponent.IsActive)
                        continue;

                    var topNode = childComponent.TopTreeNode;
                    if (topNode.Valid)
                        continue;

                    topNode = childComponent.RebuildTreeNodes();
                    if (!topNode.Valid)
                        continue;

                    if (knownNodes.Add(childHierarchyItem))
                        updateNodes.Add(childHierarchyItem);
                }
            }
            finally
            {
                HashSetPool<ChiselHierarchyItem>.Release(knownNodes);
			}
        }


        public static void GetChildrenOfHierarchyItemNoAlloc(ChiselHierarchyItem item, List<CSGTreeNode> childNodes)
        {
            if (item == null)
                return;
            item.Children.Sort(s_CompareChiselHierarchyParentOrder);
            for (int i = 0; i < item.Children.Count; i++)
            {
                var childHierarchyItem = item.Children[i];
                if (childHierarchyItem.Component is ChiselModelComponent)
                    continue;
                var childComponent = childHierarchyItem.Component;
                if (!childComponent && childComponent.IsActive)
                    continue;

                var topNode = childComponent.TopTreeNode;
                if (!topNode.Valid)
                    continue;

                childNodes.Add(topNode);
            }
        }

        public static Transform FindModelTransformOfTransform(Transform transform)
        {
            // TODO: optimize this
            do
            {
                if (!transform)
                    return null;
                var model = transform.GetComponentInParent<ChiselModelComponent>();
                if (!model)
                    return null;
                transform = model.hierarchyItem.Transform;
                if (!transform)
                    return null;
                if (model.enabled)
                    return transform;
                transform = transform.parent;
            } while (true);
        }

        public static Matrix4x4 FindModelTransformMatrixOfTransform(Transform transform)
        {
            // TODO: optimize this
            do
            {
                if (!transform)
                    return Matrix4x4.identity;
                var model = transform.GetComponentInParent<ChiselModelComponent>();
                if (!model)
                    return Matrix4x4.identity;
                transform = model.hierarchyItem.Transform;
                if (!transform)
                    return Matrix4x4.identity;
                if (model.enabled)
                    return transform.localToWorldMatrix;
                transform = transform.parent;
            } while (true);
        }
    }
}
