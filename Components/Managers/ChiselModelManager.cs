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
                var foundInstances = UnityEngine.Resources.FindObjectsOfTypeAll<ChiselModelManager>();
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
#if UNITY_EDITOR
			s_SavedOutputCandidates.Clear();
			s_SkippedModels.Clear();
			// Remove destroyed models that can no longer unregister themselves.
			s_ModelsWithoutNodes.RemoveWhere(model => !model);
			s_ModelsToBuild.RemoveWhere(model => !model);
#endif
			s_RegisteredNodes.Clear();
			s_RegisteredModels.Clear();
			s_RegisteredGenerators.Clear();

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

#if UNITY_EDITOR
		// Newly created trees that may reuse their model's saved output.
		readonly Dictionary<CSGTree, ChiselModelComponent> s_SavedOutputCandidates = new();
		// Models whose saved output still needs transient state restored.
		readonly List<ChiselModelComponent> s_SkippedModels = new();

		readonly HashSet<ChiselModelComponent> s_ModelsWithoutNodes = new();

		readonly HashSet<ChiselModelComponent> s_ModelsToBuild = new();

		/// <summary>Scratch for <see cref="BuildTheNodesOfEveryModel"/>, which writes to one set while reading the other.</summary>
		readonly List<ChiselModelComponent> s_ModelsBeingAsked = new();

		/// <summary>Set by <see cref="ForgetSavedOutputs"/> until the next update is done. A rebuild asks for
		/// everything to be built before the trees are remade, so the answer has to outlive <see cref="OnTreeCreated"/>
		/// running again.</summary>
		bool s_BuildEverything;

		static bool CanSkipTreeUpdate(CSGTree tree)
		{
			var instance = Instance;
			if (!instance.s_SavedOutputCandidates.Remove(tree, out var model) || !model)
				return false;

			if (!instance.s_ModelsWithoutNodes.Contains(model))
				return false;

			instance.s_SkippedModels.Add(model);
			return true;
		}

		readonly static CanSkipTreeUpdate canSkipTreeUpdateMethod = (CanSkipTreeUpdate)CanSkipTreeUpdate;
#endif

#if UNITY_EDITOR
		/// <summary>
		/// Whether the meshes saved with this model are still what its input builds - asked without a tree, and
		/// without changing anything. One decider per stage: it is asked once per model, in
		/// <see cref="OnTreeCreated"/>, and nothing asks it again.
		/// </summary>
		public static bool KeepsItsSavedMeshes(ChiselModelComponent model)
		{
			if (!model)
				return false;
			var generated = model.generated;
			return generated != null &&
				   generated.componentInputHash.isValid &&
				   generated.IsValid() &&
				   GetComponentInputHash(model) == generated.componentInputHash;
		}

		/// <summary>Whether this model's generators have no CSG nodes. Asked by the hierarchy manager, which is also
		/// the only thing that can make the answer false.</summary>
		internal static bool HasNoNodes(ChiselModelComponent model)
		{
			return model && Instance.s_ModelsWithoutNodes.Contains(model);
		}

		/// <summary>Whether something has asked for this model's nodes since it was skipped. Still unbuilt, and its
		/// CSG still skipped, until the update phase gets to it.</summary>
		internal static bool WasAskedToBuild(ChiselModelComponent model)
		{
			return model && Instance.s_ModelsToBuild.Contains(model);
		}

		/// <summary>Called by the hierarchy manager when it has actually built a model's children. That is what takes
		/// a model out of <see cref="s_ModelsWithoutNodes"/> - the set stops describing it at the moment it stops
		/// being true, and not before.</summary>
		internal static void NoteTheNodesWereBuilt(ChiselModelComponent model)
		{
			if (!model)
				return;
			var instance = Instance;
			instance.s_ModelsWithoutNodes.Remove(model);
			instance.s_ModelsToBuild.Remove(model);
		}

		/// <summary>
		/// Asks for a model's generators to be given their CSG nodes: an edit, a scene query, a click, a rebuild.
		/// <para>
		/// It does not build them here. The model stays in <see cref="s_ModelsWithoutNodes"/>, so its CSG stays
		/// skipped, until the ordinary update phase makes the children - which is what keeps a model that has been
		/// asked for but not yet built from having its CSG run on an empty tree.
		/// </para>
		/// </summary>
		internal static void BuildTheNodesOf(ChiselModelComponent model)
		{
			var instance = Instance;
			if (!model || !instance.s_ModelsWithoutNodes.Contains(model) || !instance.s_ModelsToBuild.Add(model))
				return;
			ChiselNodeHierarchyManager.QueueChildrenOf(model);
		}

		public static void BuildTheNodesOfNow(ChiselModelComponent model)
		{
			if (!HasNoNodes(model))
				return;
			BuildTheNodesOf(model);
			if (ChiselNodeHierarchyManager.IsUpdating)
				return;
			ChiselNodeHierarchyManager.Update();
			Instance.UpdateModels();
		}

		/// <summary>Asks for every model that has no nodes. Returns whether anything was asked for.</summary>
		internal static bool BuildTheNodesOfEveryModel()
		{
			var instance = Instance;
			if (instance.s_ModelsWithoutNodes.Count == 0)
				return false;
			// Copied first: BuildTheNodesOf writes to s_ModelsToBuild, and the two sets are walked together
			instance.s_ModelsBeingAsked.Clear();
			instance.s_ModelsBeingAsked.AddRange(instance.s_ModelsWithoutNodes);
			foreach (var model in instance.s_ModelsBeingAsked)
				BuildTheNodesOf(model);
			instance.s_ModelsBeingAsked.Clear();
			return true;
		}
#else
		// A player keeps no saved meshes - the skip above is an editor feature - so every model builds its CSG, none is
		// ever without nodes, and there is nothing to ask for
		public static bool KeepsItsSavedMeshes(ChiselModelComponent model) { return false; }
		internal static bool HasNoNodes(ChiselModelComponent model) { return false; }
		internal static bool WasAskedToBuild(ChiselModelComponent model) { return false; }
		internal static void NoteTheNodesWereBuilt(ChiselModelComponent model) { }
		internal static void BuildTheNodesOf(ChiselModelComponent model) { }
		public static void BuildTheNodesOfNow(ChiselModelComponent model) { }
		internal static bool BuildTheNodesOfEveryModel() { return false; }
#endif

		// Called when the model's tree is made: a model that was loaded may keep its saved meshes (see CanSkipTreeUpdate)
		internal void OnTreeCreated(ChiselModelComponent model)
		{
#if UNITY_EDITOR
			if (s_BuildEverything || !KeepsItsSavedMeshes(model))
				return;
			s_SavedOutputCandidates[model.Node] = model;
			s_ModelsWithoutNodes.Add(model);
#endif
		}

		/// <summary>
		/// Makes every model whose tree was just made build its CSG at the next update, instead of keeping the meshes
		/// it was saved with.
		/// </summary>
		public void ForgetSavedOutputs()
		{
#if UNITY_EDITOR
			// A rebuild is asked for BEFORE the trees are remade, and OnTreeCreated runs again while it happens, so
			// clearing the sets is not enough on its own - the latch is what makes the answer survive that.
			s_BuildEverything = true;
			s_SavedOutputCandidates.Clear();
			BuildTheNodesOfEveryModel();
#endif
		}

		/// <summary>
		/// Builds the CSG of every model that kept the meshes it was saved with (see <see cref="CompactHierarchyManager.IsTreeUpdateSkipped"/>),
		/// for what needs the CSG's own results besides the meshes, such as scene queries.
		/// </summary>
		public void BuildModelsWithSavedOutputs()
		{
			// The generators first. Un-skipping a tree whose children were never made would run the CSG on an empty
			// tree, and that deletes the model's geometry rather than leaving it stale.
			var building = BuildTheNodesOfEveryModel();
			if (building)
				ChiselNodeHierarchyManager.Update();

			if (CompactHierarchyManager.HasSkippedTreeUpdates)
				CompactHierarchyManager.UpdateSkippedTrees();
			else if (!building)
				return;
			UpdateModels();
		}

		/// <summary>
		/// Gives every model's generated objects its layer and static flags when they changed, which building the
		/// meshes would otherwise be the only thing to do (ChiselGeneratedObjects.UpdateContainersWhenModelStateChanged).
		/// Builds nothing, so the models that kept their saved meshes keep them.
		/// </summary>
		void UpdateContainerStates()
		{
			foreach (var model in Models)
			{
				if (model && model.generated != null)
					model.generated.UpdateContainersWhenModelStateChanged(model);
			}
		}

		public void UpdateModels()
		{
#if UNITY_EDITOR
			// Before the Flush, which returns early when no tree has to be built - and a model whose static flags
			// changed has nothing to build
			UpdateContainerStates();

			// The decals go to their models' trees first, so this update draws them
			ChiselDecalManager.Update();

			// Update the tree meshes
			Profiler.BeginSample("Flush");
			try
			{
				if (!CompactHierarchyManager.Flush(finishMeshUpdatesMethod, canSkipTreeUpdateMethod))
				{
					ChiselLightmapUVManager.UpdateLightmapResolution();
					return; // Nothing to update ..
				}
			}
			finally
			{
				// The Flush asked about every tree made since the last one, so a candidate that's left had its tree
				// destroyed before that
				s_SavedOutputCandidates.Clear();
				Profiler.EndSample();
			}

			// The models that kept their saved meshes get back what the scene doesn't keep of them
			foreach (var model in s_SkippedModels)
			{
				if (model && model.generated != null)
					model.generated.RestoreSkippedUpdate(model);
			}
			s_SkippedModels.Clear();
			// The rebuild this update was asked for has happened, so models may keep their saved meshes again
			s_BuildEverything = false;
#endif

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
#if UNITY_EDITOR
			// Destroyed models cannot complete deferred node construction.
			if (node is ChiselModelComponent destroyedModel && !destroyedModel)
			{
				s_ModelsWithoutNodes.Remove(destroyedModel);
				s_ModelsToBuild.Remove(destroyedModel);
			}
#endif
			if (!s_RegisteredNodes.Remove(node))
				return;

			var generator = node as ChiselGeneratorComponent;
			if (!ReferenceEquals(generator, null)) { s_RegisteredGenerators.Remove(generator); }

			var model = node as ChiselModelComponent;
			if (!ReferenceEquals(model, null))
			{
				if (!model && model.hierarchyItem.RegisteredTransform)
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
#if UNITY_EDITOR
			foreach (var model in Models)
			{
				if (model == null)
					continue;
				ChiselRenderObjects.OnRenderModel(camera, model, drawModeFlags);
			}
#endif
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
#if UNITY_EDITOR
			if (!model || !model.isActiveAndEnabled)
#endif
				return false;

#if UNITY_EDITOR
			var sceneVisibilityManager = UnityEditor.SceneVisibilityManager.instance;
            var visible         = !sceneVisibilityManager.IsHidden(model.gameObject);
            var pickingEnabled  = !sceneVisibilityManager.IsPickingDisabled(model.gameObject);
            return visible && pickingEnabled;
#endif
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
			if (foundBrushes == null || !component)
				return;

			BuildTheNodesOfNow(component.hierarchyItem.Model);

			if (!component.TopTreeNode.Valid)
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
		// Distinguishes output produced by incompatible mesh-building code.
		static Hash128 s_CodeVersion;

		static readonly Dictionary<ulong, Hash128> s_SurfaceParameterIdentities = new();
		static readonly Hash128 s_SessionIdentity = Hash128.Compute(Guid.NewGuid().ToString());
		readonly static SurfaceParameterIdentity surfaceParameterIdentityMethod = (SurfaceParameterIdentity)GetSurfaceParameterIdentity;

		/// <summary>
		/// How a surface parameter is identified in a way that outlasts the session, for whatever else hashes what a
		/// model is built from (see <see cref="ChiselStagedInputHashes"/>).
		/// </summary>
		public static SurfaceParameterIdentity SurfaceParameterIdentityMethod { get { return surfaceParameterIdentityMethod; } }

		static Hash128 GetSurfaceParameterIdentity(ulong surfaceParameter)
		{
			if (s_SurfaceParameterIdentities.TryGetValue(surfaceParameter, out var identity))
				return identity;
			var asset = Resources.EntityIdToObject(EntityId.FromULong(surfaceParameter));
			if (asset != null &&
				UnityEditor.AssetDatabase.TryGetGUIDAndLocalFileIdentifier(asset, out string guid, out long localId))
			{
				identity = Hash128.Compute(guid);
				identity.Append(ref localId);  // by reference: a long would silently go to Append(float)
			} else
			{
				identity = s_SessionIdentity;
				identity.Append(ref surfaceParameter);
			}
			s_SurfaceParameterIdentities[surfaceParameter] = identity;
			return identity;
		}

		/// <summary>
		/// The hash of everything the model's meshes are built from: its tree's input, the same in every session for the
		/// same input (see <see cref="CompactHierarchyManager.GetTreeInputHash"/>), and the version of the code that
		/// builds them. Invalid for a model without a tree.
		/// </summary>
		public static Hash128 GetInputHash(ChiselModelComponent model)
		{
			if (!model || !model.Node.Valid)
				return default;
			var hash = CompactHierarchyManager.GetTreeInputHash(model.Node, surfaceParameterIdentityMethod);
			if (!hash.isValid)
				return default;
			AppendEnvironment(ref hash);
			return hash;
		}

		/// <summary>
		/// The same question as <see cref="GetInputHash"/>, asked of the model's components instead of the tree built
		/// from them (<see cref="ChiselStagedInputHashes.GetCSGInputHash"/>), so that it can be asked before there is
		/// a tree to ask it of. This is what decides whether a model keeps the meshes saved with its scene.
		/// </summary>
		public static Hash128 GetComponentInputHash(ChiselModelComponent model)
		{
			if (!model)
				return default;
			var hash = ChiselStagedInputHashes.GetCSGInputHash(model, surfaceParameterIdentityMethod);
			if (!hash.isValid)
				return default;
			AppendEnvironment(ref hash);
			return hash;
		}

		/// <summary>
		/// What both hashes have to cover and neither can see in the model: the version of the code that builds the
		/// meshes, which of its two CSG algorithms builds them, and the helper materials <c>FinishMeshUpdates</c> hands
		/// out, which no surface names. Without the code version a saved scene would keep meshes built by code that has
		/// since changed, and without the algorithm meshes built by the other one.
		/// </summary>
		static void AppendEnvironment(ref Hash128 hash)
		{
			if (!s_CodeVersion.isValid)
			{
				s_CodeVersion = Hash128.Compute(typeof(CompactHierarchyManager).Assembly.ManifestModule.ModuleVersionId.ToString());
				s_CodeVersion.Append(typeof(ChiselModelManager).Assembly.ManifestModule.ModuleVersionId.ToString());
			}
			hash.Append(ref s_CodeVersion);

			int algorithm = CompactHierarchyManager.TreeUpdate.kExactCSG ? 1 : 0;
			hash.Append(ref algorithm);

			AppendMaterialIdentity(ref hash, ChiselProjectSettings.ForceShadowOnlySurfacesMaterial);
			AppendMaterialIdentity(ref hash, ChiselProjectSettings.CollisionSurfacesMaterial);
			var debugVisualizationMaterials = ChiselProjectSettings.DebugVisualizationMaterials;
			for (int i = 0; debugVisualizationMaterials != null && i < debugVisualizationMaterials.Length; i++)
				AppendMaterialIdentity(ref hash, debugVisualizationMaterials[i]);
		}

		static void AppendMaterialIdentity(ref Hash128 hash, Material material)
		{
			var identity = material ? GetSurfaceParameterIdentity(EntityId.ToULong(material.GetEntityId())) : default;
			hash.Append(ref identity);
		}

		/// <summary>
		/// Stores with every model of the scene the hash of the input its meshes are built from, so that when the scene
		/// opens again a model whose input is the same keeps these meshes (see <see cref="CanSkipTreeUpdate"/>). What is
		/// pending is built first, so the meshes the scene keeps are the ones the hash describes.
		/// </summary>
		public void StoreInputHashes(Scene scene)
		{
			var updating = ChiselNodeHierarchyManager.IsUpdating;
			if (!updating)
				ChiselNodeHierarchyManager.Update();
			UpdateModels();
			foreach (var model in Models)
			{
				if (!model || model.generated == null || model.gameObject.scene != scene)
					continue;
				if (HasNoNodes(model))
					continue;
				var settled = !updating && model.Node.Valid && !model.Node.Dirty;
				model.generated.inputHash          = settled ? GetInputHash(model) : default;
				model.generated.componentInputHash = settled ? GetComponentInputHash(model) : default;
			}
		}

		[UnityEditor.InitializeOnLoadMethod]
		static void RegisterSceneSaving()
		{
			UnityEditor.SceneManagement.EditorSceneManager.sceneSaving -= OnSceneSaving;
			UnityEditor.SceneManagement.EditorSceneManager.sceneSaving += OnSceneSaving;
		}

		static void OnSceneSaving(Scene scene, string path)
		{
			Instance.StoreInputHashes(scene);
		}

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
