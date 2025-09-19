using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Pool;
using Chisel.Core;
using UnityEngine.Profiling;
using Unity.Jobs;

namespace Chisel.Components
{
    public class ChiselModelManager : ScriptableObject, ISerializationCallbackReceiver
	{
		public const string kGeneratedDefaultModelName = "‹[default-model]›";
		
		const string kDefaultModelName = "Model";

        #region Instance
        static ChiselModelManager _instance;
        public static ChiselModelManager Instance
        {
            get
            {
                if (_instance)
                    return _instance;
                var foundInstances = UnityEngine.Object.FindObjectsByType<ChiselModelManager>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
                if (foundInstances == null ||
                    foundInstances.Length == 0)
                {
                    _instance = ScriptableObject.CreateInstance<ChiselModelManager>();
                    _instance.hideFlags = HideFlags.HideAndDontSave;
                    return _instance;
                }

                if (foundInstances.Length > 1)
                {
                    for (int i = 1; i < foundInstances.Length; i++)
                        ChiselObjectUtility.SafeDestroy(foundInstances[i]);
                }

                _instance = foundInstances[0];
                return _instance;
            }
        }
		#endregion

		public event Action PostReset;
		public event Action PostUpdateModels;

		internal void Reset()
		{
			PostReset?.Invoke();
		}


		static int FinishMeshUpdates(CSGTree tree, ChiselMeshUpdates meshUpdates, JobHandle dependencies)
		{
			ChiselModelComponent foundModel = null;
			var models = Instance.Models;

			foreach (var model in models)
			{
				if (!model)
					continue;

				if (model.Node == tree)
					foundModel = model;
			}

			if (foundModel == null)
			{
				if (meshUpdates.meshDataArray.Length > 0) meshUpdates.meshDataArray.Dispose();
				meshUpdates.meshDataArray = default;
				return 0;
			}

			if (foundModel.generated == null ||
				!foundModel.generated.IsValid())
			{
				foundModel.generated?.Destroy();
				foundModel.generated = ChiselGeneratedObjects.Create(foundModel.gameObject);
			}

			var count = foundModel.generated.FinishMeshUpdates(foundModel, meshUpdates, dependencies);
			Instance.Rebuild(foundModel);
			return count;
		}

		readonly static FinishMeshUpdate finishMeshUpdatesMethod = (FinishMeshUpdate)FinishMeshUpdates;

		public void UpdateModels()
		{

			// Update the tree meshes
			Profiler.BeginSample("Flush");
			try
			{
				if (!CompactHierarchyManager.Flush(finishMeshUpdatesMethod))
				{
					ChiselUnityUVGenerationManager.DelayedUVGeneration();
					return; // Nothing to update ..
				}
			}
			finally
			{
				Profiler.EndSample();
			}

			{
				Profiler.BeginSample("PostUpdateModels");
				PostUpdateModels?.Invoke();
				Profiler.EndSample();
			}
		}

		// TODO: potentially have a history per scene, so when one model turns out to be invalid, go back to the previously selected model
		readonly Dictionary<Scene, ChiselModelComponent> activeModels = new();

        #region ActiveModel Serialization
        [Serializable] public struct SceneModelPair { public Scene Key; public ChiselModelComponent Value; }
        [SerializeField] SceneModelPair[] activeModelsArray;

        public void OnBeforeSerialize()
        {
            var foundModels = ListPool<SceneModelPair>.Get();
            //if (foundModels != null)
            {
                if (foundModels.Capacity < activeModels.Count)
                    foundModels.Capacity = activeModels.Count;
                foreach (var pair in activeModels)
                    foundModels.Add(new SceneModelPair { Key = pair.Key, Value = pair.Value });
                if (activeModelsArray != null && activeModelsArray.Length == foundModels.Count)
                {
                    foundModels.CopyTo(activeModelsArray);
                }
                else
                    activeModelsArray = foundModels.ToArray();
                ListPool<SceneModelPair>.Release(foundModels);
            }
        }

        public void OnAfterDeserialize()
        {
            if (activeModelsArray == null)
                return;
            foreach (var pair in activeModelsArray)
                activeModels[pair.Key] = pair.Value;
            activeModelsArray = null;
        }
		#endregion


		readonly HashSet<ChiselModelComponent> s_RegisteredModels = new();
		readonly HashSet<ChiselNodeComponent> s_RegisteredNodes = new();
		readonly HashSet<ChiselGeneratorComponent> s_RegisteredGenerators = new();

		public IEnumerable<ChiselModelComponent> Models { get { return s_RegisteredModels; } }
		public IEnumerable<ChiselNodeComponent> Nodes { get { return s_RegisteredNodes; } }
		public IEnumerable<ChiselGeneratorComponent> Generators { get { return s_RegisteredGenerators; } }


		public void Register(ChiselNodeComponent node)
		{
			if (!s_RegisteredNodes.Add(node))
				return;

			var generator = node as ChiselGeneratorComponent;
			if (!ReferenceEquals(generator, null)) { s_RegisteredGenerators.Add(generator); }			

			var model = node as ChiselModelComponent;
			if (!ReferenceEquals(model, null)) { s_RegisteredModels.Add(model); }
		}

		public void Unregister(ChiselNodeComponent node)
		{
			if (!s_RegisteredNodes.Remove(node))
				return;

			var generator = node as ChiselGeneratorComponent;
			if (!ReferenceEquals(generator, null)) { s_RegisteredGenerators.Remove(generator); }

			var model = node as ChiselModelComponent;
			if (!ReferenceEquals(model, null))
			{
				// If we removed our model component, we should remove the containers
				if (!model && model.hierarchyItem.GameObject)
					RemoveContainerGameObjectWithUndo(model);

				s_RegisteredModels.Remove(model);
			}
		}

		public void Rebuild(ChiselModelComponent model)
		{
			if (!model.IsInitialized)
			{
				model.OnInitialize();
			}

			if (model.generated == null ||
				!model.generated.IsValid())
			{
				model.generated?.Destroy();
				model.generated = ChiselGeneratedObjects.Create(model.gameObject);
			}

			UpdateModelFlags(model);

			if(model.AutoRebuildUVs)
			{
				ForceUpdateDelayedUVGeneration();
			}
		}

		public bool IsDefaultModel(UnityEngine.Object obj)
		{
			var component = obj as Component;
			if (!Equals(component, null))
				return IsDefaultModel(component);
			var gameObject = obj as GameObject;
			if (!Equals(gameObject, null))
				return IsDefaultModel(gameObject);
			return false;
		}

		internal bool IsDefaultModel(GameObject gameObject)
		{
			if (!gameObject)
				return false;
			var model = gameObject.GetComponent<ChiselModelComponent>();
			if (!model)
				return false;
			return (model.IsDefaultModel);
		}

		internal bool IsDefaultModel(Component component)
		{
			if (!component)
				return false;
			ChiselModelComponent model = component as ChiselModelComponent;
			if (!model)
			{
				model = component.GetComponent<ChiselModelComponent>();
				if (!model)
					return false;
			}
			return (model.IsDefaultModel);
		}

		internal bool IsDefaultModel(ChiselModelComponent model)
		{
			if (!model)
				return false;
			return (model.IsDefaultModel);
		}

		internal ChiselModelComponent CreateDefaultModel(ChiselSceneHierarchy sceneHierarchy)
		{
			var currentScene = sceneHierarchy.Scene;
			var rootGameObjects = ListPool<GameObject>.Get();
			currentScene.GetRootGameObjects(rootGameObjects);
			for (int i = 0; i < rootGameObjects.Count; i++)
			{
				if (!IsDefaultModel(rootGameObjects[i]))
					continue;

				var gameObject = rootGameObjects[i];
				var model = gameObject.GetComponent<ChiselModelComponent>();
				if (model)
					return model;

				var transform = gameObject.GetComponent<Transform>();
				ChiselObjectUtility.ResetTransform(transform);

				model = gameObject.AddComponent<ChiselModelComponent>();
				UpdateModelFlags(model);
				return model;
			}
			ListPool<GameObject>.Release(rootGameObjects);


			var oldActiveScene = SceneManager.GetActiveScene();
			if (currentScene != oldActiveScene)
				SceneManager.SetActiveScene(currentScene);

			try
			{
				var model = ChiselComponentFactory.Create<ChiselModelComponent>(kGeneratedDefaultModelName);
				model.IsDefaultModel = true;
				UpdateModelFlags(model);
				return model;
			}
			finally
			{
				if (currentScene != oldActiveScene)
					SceneManager.SetActiveScene(oldActiveScene);
			}
		}

		// TODO: find a better place for this
		public bool IsValidModelToBeSelected(ChiselModelComponent model)
		{
			if (!model || !model.isActiveAndEnabled || model.generated == null)
				return false;
#if UNITY_EDITOR
			var gameObject = model.gameObject;
			var sceneVisibilityManager = UnityEditor.SceneVisibilityManager.instance;
			if (sceneVisibilityManager.AreAllDescendantsHidden(gameObject) ||
				sceneVisibilityManager.IsPickingDisabledOnAllDescendants(gameObject))
				return false;
#endif
			return true;
		}

		public void OnRenderModels(Camera camera, DrawModeFlags drawModeFlags)
		{
			foreach (var model in Models)
			{
				if (model == null)
					continue;
				ChiselRenderObjects.OnRenderModel(camera, model, drawModeFlags);
			}
		}

		private void UpdateModelFlags(ChiselModelComponent model)
		{
			if (!IsDefaultModel(model))
				return;

			const HideFlags DefaultGameObjectHideFlags = HideFlags.NotEditable;
			const HideFlags DefaultTransformHideFlags = HideFlags.NotEditable;// | HideFlags.HideInInspector;

			var gameObject = model.gameObject;
			var transform = model.transform;
			if (gameObject.hideFlags != DefaultGameObjectHideFlags) gameObject.hideFlags = DefaultGameObjectHideFlags;
			if (transform.hideFlags != DefaultTransformHideFlags) transform.hideFlags = DefaultTransformHideFlags;

			if (transform.parent != null)
			{
				transform.SetParent(null, false);
				ChiselObjectUtility.ResetTransform(transform);
			}
		}

		private void RemoveContainerGameObjectWithUndo(ChiselModelComponent model)
		{
			if (model.generated != null)
				model.generated.DestroyWithUndo();
		}


		public bool IsSelectable(ChiselModelComponent model)
        {
            if (!model || !model.isActiveAndEnabled)
                return false;

            var sceneVisibilityManager = UnityEditor.SceneVisibilityManager.instance;
            var visible         = !sceneVisibilityManager.IsHidden(model.gameObject);
            var pickingEnabled  = !sceneVisibilityManager.IsPickingDisabled(model.gameObject);
            return visible && pickingEnabled;
        }


        public ChiselModelComponent ActiveModel
        { 
            get
            {
                // Find our active model for the current active scene
                var activeScene = SceneManager.GetActiveScene();
                Instance.activeModels.TryGetValue(activeScene, out var activeModel);

                // Check if the activeModel is valid & if it's scene actually points to the active Scene
                if (ReferenceEquals(activeModel, null) ||
                    !activeModel || activeModel.gameObject.scene != activeScene)
                {
                    // If active model is invalid or missing, find another model the active model
                    Instance.activeModels[activeScene] = FindModelInScene(activeScene);
                    return null;
                }

                // If we have an active model, but it's actually disabled, do not use it
                // This prevents users from accidentally adding generators to a model that is inactive, 
                // and then be confused why nothing is visible.
                if (!IsSelectable(activeModel))
                    return null;
                return activeModel;
            }
            set
            {
                // When we set a model to be active, make sure we use the scene of its gameobject
                var modelScene = value.gameObject.scene;
                Instance.activeModels[modelScene] = value;

                // And then make sure that scene is active
                if (modelScene != SceneManager.GetActiveScene())
                    SceneManager.SetActiveScene(modelScene);
            }
        }

		// Get all brushes directly contained by this CSGNode (not its children)
		public void GetAllTreeBrushes(ChiselGeneratorComponent component, HashSet<CSGTreeBrush> foundBrushes)
		{
			if (foundBrushes == null ||
				!component.TopTreeNode.Valid)
				return;

			var brush = (CSGTreeBrush)component.TopTreeNode;
			if (brush.Valid)
			{
				foundBrushes.Add(brush);
			}
			else
			{
				var nodes = new List<CSGTreeNode>();
				nodes.Add(component.TopTreeNode);
				while (nodes.Count > 0)
				{
					var lastIndex = nodes.Count - 1;
					var current = nodes[lastIndex];
					nodes.RemoveAt(lastIndex);
					var nodeType = current.Type;
					if (nodeType == CSGNodeType.Brush)
					{
						brush = (CSGTreeBrush)current;
						foundBrushes.Add(brush);
					}
					else
					{
						for (int i = current.Count - 1; i >= 0; i--)
							nodes.Add(current[i]);
					}
				}
			}
		}

		public ChiselModelComponent GetActiveModelOrCreate(ChiselModelComponent overrideModel = null)
        {
            if (overrideModel)
            {
                ActiveModel = overrideModel;
                return overrideModel;
            }

            var activeModel = ActiveModel;
            if (!activeModel)
            {
                // TODO: handle scene being locked by version control
                activeModel = CreateNewModel();
                ActiveModel = activeModel; 
            }
            return activeModel;
        }

        public ChiselModelComponent CreateNewModel(Transform parent = null)
        {
            return ChiselComponentFactory.Create<ChiselModelComponent>(kDefaultModelName, parent);
        }

        public ChiselModelComponent FindModelInScene(Scene scene)
        {
            if (!scene.isLoaded ||
                !scene.IsValid())
                return null;

            var allRootGameObjects = scene.GetRootGameObjects();
            if (allRootGameObjects == null)
                return null;

            // We prever last model (more likely last created), so we iterate backwards
            for (int n = allRootGameObjects.Length - 1; n >= 0; n--)
            {
                var rootGameObject = allRootGameObjects[n];
                // Skip all gameobjects that are disabled
                if (!rootGameObject.activeInHierarchy)
                    continue;

                // Go through all it's models, this method returns the top most models first
                var models = rootGameObject.GetComponentsInChildren<ChiselModelComponent>(includeInactive: false);
                foreach (var model in models)
                {
                    // Skip all inactive models
                    if (!IsSelectable(model))
                        continue;

                    return model;
                }
            }
            return null;
        }

        public void CheckActiveModels()
        {
            // Go through all activeModels, which we store per scene, and make sure they still make sense

            var removeScenes = ListPool<Scene>.Get();
            var setSceneModels = DictionaryPool<Scene, ChiselModelComponent>.Get();
            try
            {
                foreach (var pair in Instance.activeModels)
                {
                    // If the scene is no longer loaded, remove it from our list
                    if (!pair.Key.isLoaded || !pair.Key.IsValid())
                    {
                        removeScenes.Add(pair.Key);
                    }

                    // Check if a current activeModel still exists
                    var sceneActiveModel = pair.Value;
                    if (!sceneActiveModel)
                    {
                        setSceneModels[pair.Key] = FindModelInScene(pair.Key);
                        //Instance.activeModels[pair.Key] = FindModelInScene(pair.Key);
                        continue;
                    }

                    // Check if a model has been moved to another scene, and correct this if it has
                    var gameObjectScene = sceneActiveModel.gameObject.scene;
                    if (gameObjectScene != pair.Key)
                    {
                        setSceneModels[pair.Key] = FindModelInScene(pair.Key);
                        setSceneModels[gameObjectScene] = FindModelInScene(pair.Key);
                    }
                }


                foreach (var scene in removeScenes)
                    Instance.activeModels.Remove(scene);


                foreach (var pair in setSceneModels)
                    Instance.activeModels[pair.Key] = pair.Value;
            }
            finally
            {
                ListPool<Scene>.Release(removeScenes);
                DictionaryPool<Scene, ChiselModelComponent>.Release(setSceneModels);
            }
        }

#if UNITY_EDITOR
		public void InitializeOnLoad(Scene scene)
		{
			HideDebugVisualizationSurfaces(scene);
		}

		public void HideDebugVisualizationSurfaces()
		{
			var scene = SceneManager.GetActiveScene();
			HideDebugVisualizationSurfaces(scene);
		}

		public void HideDebugVisualizationSurfaces(Scene scene)
		{
			foreach (var go in scene.GetRootGameObjects())
			{
				foreach (var model in go.GetComponentsInChildren<ChiselModelComponent>())
				{
					if (!model || !model.isActiveAndEnabled || model.generated == null)
						continue;
					model.generated.HideDebugVisualizationSurfaces();
				}
			}
		}

		public void OnWillFlushUndoRecord()
        {
            // Called on Undo, which happens when moving model to another scene
            CheckActiveModels();
        }
         
        public void OnActiveSceneChanged(Scene _, Scene newScene)
        {
            if (Instance.activeModels.TryGetValue(newScene, out var activeModel) && IsSelectable(activeModel))
                return;

            Instance.activeModels[newScene] = FindModelInScene(newScene);
            CheckActiveModels();
        }

        ChiselModelComponent GetSelectedModel()
        {
            var selectedGameObjects = UnityEditor.Selection.gameObjects;
            if (selectedGameObjects == null ||
                selectedGameObjects.Length == 1)
            { 
                var selection = selectedGameObjects[0];
                return selection.GetComponent<ChiselModelComponent>();
            }
            return null;
        }

        const string SetActiveModelMenuName = "GameObject/Set Active Model";
        [UnityEditor.MenuItem(SetActiveModelMenuName, false, -100000)]
        internal static void SetActiveModel()
        {
            var model = Instance.GetSelectedModel();
            if (!model)
                return;
            Instance.ActiveModel = model;
        }

        [UnityEditor.MenuItem(SetActiveModelMenuName, true, -100000)]
        internal static bool ValidateSetActiveModel()
        {
            var model = Instance.GetSelectedModel();
            return (model != null);
        }
#endif
    }
}