using System;
using System.Collections.Generic;

using Chisel.Core;

using UnityEngine;
using Unity.Collections;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Chisel.Components
{
#if UNITY_EDITOR
	public struct BrushVisibilityLookup : IBrushVisibilityLookup, IDisposable
	{
		NativeHashMap<CompactNodeID, VisibilityState> compactNodeIDVisibilityStateLookup;
		NativeHashMap<int, VisibilityState> instanceIDVisibilityStateLookup;

		public void Dispose()
		{
			// Confirmed to be called
			if (compactNodeIDVisibilityStateLookup.IsCreated)  
                compactNodeIDVisibilityStateLookup.Dispose();
            compactNodeIDVisibilityStateLookup = default;

			if (instanceIDVisibilityStateLookup.IsCreated)
				instanceIDVisibilityStateLookup.Dispose();
			instanceIDVisibilityStateLookup = default;
		}

        internal void Clear()
		{
			compactNodeIDVisibilityStateLookup.Clear();
			instanceIDVisibilityStateLookup.Clear();
		}

        [GenerateTestsForBurstCompatibility]
		public bool IsBrushVisible(CompactNodeID brushID) 
        { 
            return compactNodeIDVisibilityStateLookup.TryGetValue(brushID, out VisibilityState state) && state == VisibilityState.AllVisible;
		}

		[GenerateTestsForBurstCompatibility]
		public bool IsBrushVisible(int instanceID)
		{
			return instanceIDVisibilityStateLookup.TryGetValue(instanceID, out VisibilityState state) && state == VisibilityState.AllVisible;
		}

		private readonly VisibilityState GetVisibilityState(SceneVisibilityManager instance, ChiselGeneratorComponent generator)
        {
            var resultState     = VisibilityState.Unknown;
            var visible         = !instance.IsHidden(generator.gameObject);
            var pickingEnabled  = !instance.IsPickingDisabled(generator.gameObject);
            var topNode         = generator.TopTreeNode;
            if (topNode.Valid)
            {
                topNode.Visible         = visible;
                topNode.PickingEnabled  = pickingEnabled;

                if (visible)
                    resultState |= VisibilityState.AllVisible;
                else
                    resultState |= VisibilityState.AllInvisible;
            }
            return resultState;
        }

        public bool HasVisibilityInitialized(ChiselGeneratorComponent node)
        {
            if (!compactNodeIDVisibilityStateLookup.IsCreated || 
                !node.TopTreeNode.Valid)
                return false;

            var compactNodeID = CompactHierarchyManager.GetCompactNodeID(node.TopTreeNode);
            foreach (var childCompactNodeID in CompactHierarchyManager.GetAllChildren(compactNodeID))
            {
                if (!compactNodeIDVisibilityStateLookup.ContainsKey(childCompactNodeID))
                    return false;
            }
            return true;
        }

        void UpdateVisibility(SceneVisibilityManager sceneVisibilityManager, ChiselGeneratorComponent node)
        {
            var treeNode = node.TopTreeNode;
            if (!treeNode.Valid)
                return;

            var model = node.hierarchyItem.Model;
            if (model == null)
                Debug.LogError($"{node.hierarchyItem.Component} model {model} == null", node.hierarchyItem.Component);
            if (!model)
                return;

            var modelNode = model.TopTreeNode;
            var compactNodeID = CompactHierarchyManager.GetCompactNodeID(treeNode);
            var modelCompactNodeID = CompactHierarchyManager.GetCompactNodeID(modelNode);
            if (!compactNodeIDVisibilityStateLookup.TryGetValue(modelCompactNodeID, out VisibilityState prevState))
                prevState = VisibilityState.Unknown;
            var state = GetVisibilityState(sceneVisibilityManager, node);

            foreach (var childCompactNodeID in CompactHierarchyManager.GetAllChildren(compactNodeID))
                compactNodeIDVisibilityStateLookup[childCompactNodeID] = state;
            compactNodeIDVisibilityStateLookup[modelCompactNodeID] = state | prevState;
			instanceIDVisibilityStateLookup[node.GetInstanceID()] = state | prevState;
		}

        public void UpdateVisibility(IEnumerable<ChiselModelComponent> models)
        {
            // TODO: 1. turn off rendering regular meshes when we have partial visibility of model contents
            //       2. find a way to render partial mesh instead
            //          A. needs to show lightmap of original mesh, even when modified
            //          B. updating lightmaps needs to still work as if original mesh is changed
            if (!compactNodeIDVisibilityStateLookup.IsCreated)
				compactNodeIDVisibilityStateLookup = new NativeHashMap<CompactNodeID, VisibilityState>(2048, Allocator.Persistent); // Confirmed to be disposed
			compactNodeIDVisibilityStateLookup.Clear();

			if (!instanceIDVisibilityStateLookup.IsCreated)
				instanceIDVisibilityStateLookup = new NativeHashMap<int, VisibilityState>(2048, Allocator.Persistent); // Confirmed to be disposed
			instanceIDVisibilityStateLookup.Clear();

			var sceneVisibilityManager = SceneVisibilityManager.instance;
            foreach (var generator in ChiselModelManager.Instance.Generators)
            {
                if (!generator || !generator.isActiveAndEnabled)
                    continue;

                UpdateVisibility(sceneVisibilityManager, generator);
            }

            foreach (var model in models)
            {
                if (!model || !model.isActiveAndEnabled || model.generated == null)
                    continue;
                var modelNode = model.TopTreeNode;
                if (!modelNode.Valid)
                    continue;
                var modelCompactNodeID  = CompactHierarchyManager.GetCompactNodeID(modelNode);
                if (!compactNodeIDVisibilityStateLookup.TryGetValue(modelCompactNodeID, out VisibilityState state))
                {
                    compactNodeIDVisibilityStateLookup[modelCompactNodeID] = VisibilityState.AllVisible;
                    instanceIDVisibilityStateLookup[model.GetInstanceID()] = VisibilityState.AllVisible;
					model.generated.visibilityState = VisibilityState.AllVisible;
                    continue; 
                }
                if (state == VisibilityState.Mixed ||
                    state != model.generated.visibilityState)
                    model.generated.needVisibilityMeshUpdate = true;
                model.generated.visibilityState = state;
            }
        }
	}
#endif
}