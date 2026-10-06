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
		NativeHashMap<ulong, VisibilityState> entityIDVisibilityStateLookup;

		// Bumped on every change to the tables above, so consumers that derive data from them (the
		// per-renderable partial meshes) can tell whether their cached result is still current.
		public int Version { get; private set; }

		public void Dispose()
		{
			// Confirmed to be called
			if (compactNodeIDVisibilityStateLookup.IsCreated)  
                compactNodeIDVisibilityStateLookup.Dispose();
            compactNodeIDVisibilityStateLookup = default;

			if (entityIDVisibilityStateLookup.IsCreated)
				entityIDVisibilityStateLookup.Dispose();
			entityIDVisibilityStateLookup = default;
		}

        internal void Clear()
		{
			compactNodeIDVisibilityStateLookup.Clear();
			entityIDVisibilityStateLookup.Clear();
			Version++;
		}

        [GenerateTestsForBurstCompatibility]
		public bool IsBrushVisible(CompactNodeID brushID) 
        { 
            return compactNodeIDVisibilityStateLookup.TryGetValue(brushID, out VisibilityState state) && state == VisibilityState.AllVisible;
		}

		[GenerateTestsForBurstCompatibility]
		public bool IsBrushVisible(ulong entityID)
		{
			return entityIDVisibilityStateLookup.TryGetValue(entityID, out VisibilityState state) && state == VisibilityState.AllVisible;
		}

		[GenerateTestsForBurstCompatibility]
		public bool IsBrushHidden(ulong entityID)
		{
			return entityIDVisibilityStateLookup.TryGetValue(entityID, out VisibilityState state) && state == VisibilityState.AllInvisible;
		}

		public bool TryGetBrushVisibility(ulong entityID, out VisibilityState state)
		{
			return entityIDVisibilityStateLookup.TryGetValue(entityID, out state);
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

        void EnsureCreated()
        {
            if (!compactNodeIDVisibilityStateLookup.IsCreated)
				compactNodeIDVisibilityStateLookup = new NativeHashMap<CompactNodeID, VisibilityState>(2048, Allocator.Persistent); // Confirmed to be disposed
			if (!entityIDVisibilityStateLookup.IsCreated)
				entityIDVisibilityStateLookup = new NativeHashMap<ulong, VisibilityState>(2048, Allocator.Persistent); // Confirmed to be disposed
        }

        internal void UpdateNodeVisibility(ChiselGeneratorComponent node)
        {
            EnsureCreated();
            UpdateVisibility(SceneVisibilityManager.instance, node);
            Version++;
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
			entityIDVisibilityStateLookup[UnityEngine.EntityId.ToULong(node.GetEntityId())] = state | prevState;
		}

        public void UpdateVisibility(IEnumerable<ChiselModelComponent> models)
        {
            // TODO: 1. turn off rendering regular meshes when we have partial visibility of model contents
            //       2. find a way to render partial mesh instead
            //          A. needs to show lightmap of original mesh, even when modified
            //          B. updating lightmaps needs to still work as if original mesh is changed
            EnsureCreated();
			compactNodeIDVisibilityStateLookup.Clear();
			entityIDVisibilityStateLookup.Clear();
			Version++;

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
                    entityIDVisibilityStateLookup[UnityEngine.EntityId.ToULong(model.GetEntityId())] = VisibilityState.AllVisible;
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