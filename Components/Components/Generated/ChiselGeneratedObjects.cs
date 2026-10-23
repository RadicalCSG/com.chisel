using System;
using Chisel.Core;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Profiling;
using Unity.Jobs;
using UnityEngine.Pool;

namespace Chisel.Components
{        
    public enum DrawModeFlags
    {
        None                = 0,
        Default             = None,
        HideRenderables     = 1,
        ShowColliders       = 2,
        ShowShadowCasters   = 4,
        ShowShadowOnly      = 8,
        ShowShadowReceivers = 16,
        ShowDiscarded       = 32,
        ShowUserHidden      = 64,
        ShowPickingModel    = 128
    }
    
    // TODO:
    // 1. Find out what the dictionary is for, and remove the need for it.
    // 2. Do the same for the rendering equivalent.
    // 3. Separate building/updating the components from building a combined (physics) materials/mesh/render setting.
    // 4. Move the colliders to a single GameObject.
    // 5. Build meshes with submeshes, with a fixed number of meshes: one per rendering setting type (shadow-only etc.).
    // 6. Keep a secondary set of those meshes with partial meshes, and use those for rendering in the model.
    // 7. Identify which triangles belong to which brush, so partial meshes can be built.
    [Serializable]
    public class ChiselGeneratedObjects
    {
        public const string kGeneratedContainerName = "‹[generated]›";
        public const int kGeneratedMeshRenderCount = 16;
        public const int kGeneratedMeshRendererCount = 8;
        public readonly static int[] kGeneratedMeshRendererIndices = { 2, 3, 7, 9, 10, 11, 13, 15 };
        public readonly static string[] kGeneratedMeshRendererNames = new string[kGeneratedMeshRenderCount]
        {
            null,                                                                                   // 0 (invalid option)
            null,                                                                                   // 1 (invalid option)
            "‹[generated-ShadowCasting]›",                                                          // 2 (Shadow-Only)
            "‹[generated-Renderable|ShadowCasting]›",                                               // 3
            null,                                                                                   // 4 (invalid option)
            null,                                                                                   // 5 (invalid option)
            null,                                                                                   // 6 (invalid option)
            "‹[generated-Renderable|ShadowCasting|ShadowReceiving]›",                               // 7
            null,                                                                                   // 8 (invalid option)
            "‹[generated-Renderable|ExcludedFromGlobalIllumination]›",                              // 9
            "‹[generated-ShadowCasting|ExcludedFromGlobalIllumination]›",                           // 10 (Shadow-Only)
            "‹[generated-Renderable|ShadowCasting|ExcludedFromGlobalIllumination]›",                // 11
            null,                                                                                   // 12 (invalid option)
            "‹[generated-Renderable|ShadowReceiving|ExcludedFromGlobalIllumination]›",              // 13
            null,                                                                                   // 14 (invalid option)
            "‹[generated-Renderable|ShadowCasting|ShadowReceiving|ExcludedFromGlobalIllumination]›" // 15
        };

        // The query of the surfaces the renderer at this index draws (the inverse of RendererIndex)
        static SurfaceDestinationFlags RendererQuery(int index)
        {
            var query = (SurfaceDestinationFlags)(index & (int)SurfaceDestinationFlags.RenderShadowReceiveAndCasting);
            if ((index & 8) != 0)
                query |= SurfaceDestinationFlags.ExcludedFromGlobalIllumination;
            return query;
        }


        public const int kVisualizationModeCount = 6;
        public readonly static string[] kGeneratedVisualizationRendererNames = new string[kVisualizationModeCount]
        {
            "‹[debug-UserHidden]›",         // SurfaceDestinationFlags.None
            "‹[debug-ShadowCasting]›",      // SurfaceDestinationFlags.RenderShadowReceiveAndCasting
            "‹[debug-ShadowOnly]›",         // SurfaceDestinationFlags.ShadowCasting
            "‹[debug-ShadowReceiving]›",    // SurfaceDestinationFlags.RenderShadowsReceiving
            "‹[debug-Collidable]›",         // SurfaceDestinationFlags.Collidable
            "‹[debug-Discarded]›"           // SurfaceDestinationFlags.Discarded
        };
        public readonly static DrawModeFlags[] kGeneratedVisualizationShowFlags = new DrawModeFlags[kVisualizationModeCount]
        {
            DrawModeFlags.ShowUserHidden,
            DrawModeFlags.ShowShadowCasters,
            DrawModeFlags.ShowShadowOnly,
            DrawModeFlags.ShowShadowReceivers,
            DrawModeFlags.ShowColliders,
            DrawModeFlags.ShowDiscarded
        };
        public const string kGeneratedMeshColliderName	= "‹[generated-Collider]›";
		public const int kColliderDebugVisualizationIndex = 4;


		public GameObject              generatedDataContainer;
        public GameObject              colliderContainer;
        public ChiselColliderObjects[] colliders;

        public ChiselRenderObjects[]   renderables;
        public MeshRenderer[]          meshRenderers;

        public ChiselRenderObjects[]   debugVisualizationRenderables;
        public MeshRenderer[]          debugMeshRenderers;

        public VisibilityState         visibilityState          = VisibilityState.Unknown;
        public bool                    needVisibilityMeshUpdate = false;
        // what UpdateDebugVisualizationState last saw; while nothing of it changes it has nothing to do
        [NonSerialized] bool           visibilityStateKnown;
        [NonSerialized] int            lastVisibilityVersion;
        [NonSerialized] DrawModeFlags  lastDrawModeFlags;
        [NonSerialized] bool           lastShowWhole;

        public Hash128                 inputHash;

        public Hash128                 componentInputHash;
        
        private ChiselGeneratedObjects() { }

        public static ChiselGeneratedObjects Create(GameObject parentGameObject)
        {
            var parentTransform     = parentGameObject.transform;

            // Make sure there's not a dangling container out there from a previous version
            var existingContainer   = parentTransform.FindChildByName(kGeneratedContainerName);
            ChiselObjectUtility.SafeDestroy(existingContainer, ignoreHierarchyEvents: true);

            var gameObjectState     = GameObjectState.Create(parentGameObject);
            var container           = ChiselObjectUtility.CreateGameObject(kGeneratedContainerName, parentTransform, gameObjectState);
            var containerTransform  = container.transform;
            var colliderContainer   = ChiselObjectUtility.CreateGameObject(kGeneratedMeshColliderName, containerTransform, gameObjectState);

            Debug.Assert((int)SurfaceDestinationFlags.Renderable      == 1);
            Debug.Assert((int)SurfaceDestinationFlags.ShadowCasting   == 2);
            Debug.Assert((int)SurfaceDestinationFlags.ShadowReceiving == 4);
            Debug.Assert((int)SurfaceDestinationFlags.RenderShadowReceiveAndCasting == (1|2|4));

            // the renderers come with the materials that are drawn (FindRenderables)
            var renderables   = Array.Empty<ChiselRenderObjects>();
            var meshRenderers = Array.Empty<MeshRenderer>();

            // and the debug visualizations with the chunks they are shown in (FindDebugRenderables)
            var debugVisualizationRenderables = Array.Empty<ChiselRenderObjects>();
            var debugMeshRenderers = Array.Empty<MeshRenderer>();

            var result = new ChiselGeneratedObjects
            {
                generatedDataContainer  = container,
                colliderContainer       = colliderContainer,
                colliders               = new ChiselColliderObjects[0],
                renderables             = renderables,
                meshRenderers           = meshRenderers,
                debugVisualizationRenderables = debugVisualizationRenderables,
                debugMeshRenderers            = debugMeshRenderers
            };

            Debug.Assert(result.IsValid());

            return result;
        }

        public void Destroy()
        {
            if (!generatedDataContainer)
                return;

            if (colliders != null)
            {
                foreach (var collider in colliders)
                {
					collider?.Destroy();
                }
                colliders = null;
            }
            if (renderables != null)
            {
                foreach (var renderable in renderables)
                {
                    renderable?.Destroy();
                }
                renderables = null;
            }
            if (debugVisualizationRenderables != null)
            {
                foreach (var debugVisualizationRenderable in debugVisualizationRenderables)
                {
                    debugVisualizationRenderable?.Destroy();
                }
                debugVisualizationRenderables = null;
            }
            ChiselObjectUtility.SafeDestroy(colliderContainer, ignoreHierarchyEvents: true);
            ChiselObjectUtility.SafeDestroy(generatedDataContainer, ignoreHierarchyEvents: true);
            generatedDataContainer  = null;
            colliderContainer       = null;

            meshRenderers       = null;
            debugMeshRenderers  = null;
        }

        public void DestroyWithUndo()
        {
            if (!generatedDataContainer)
                return;

            if (colliders != null)
            {
                foreach (var collider in colliders)
                {
                    collider?.DestroyWithUndo();
                }
            }
            if (renderables != null)
            {
                foreach (var renderable in renderables)
                {
                    renderable?.DestroyWithUndo();
                }
            }
            if (debugVisualizationRenderables != null)
            {
                foreach (var debugVisualizationRenderable in debugVisualizationRenderables)
                {
                    debugVisualizationRenderable?.DestroyWithUndo();
                }
            }
            ChiselObjectUtility.SafeDestroyWithUndo(colliderContainer, ignoreHierarchyEvents: true);
            ChiselObjectUtility.SafeDestroyWithUndo(generatedDataContainer, ignoreHierarchyEvents: true);
        }

        public void RemoveContainerFlags()
        {
            if (colliders != null)
            {
                foreach (var collider in colliders)
                {
                    collider?.RemoveContainerFlags();
                }
            }
            if (renderables != null)
            {
                foreach (var renderable in renderables)
                {
                    renderable?.RemoveContainerFlags();
                }
            }
            if (debugVisualizationRenderables != null)
            {
                foreach (var debugVisualizationRenderable in debugVisualizationRenderables)
                {
					debugVisualizationRenderable?.RemoveContainerFlags();
                }
            }
            ChiselObjectUtility.RemoveContainerFlags(colliderContainer);
            ChiselObjectUtility.RemoveContainerFlags(generatedDataContainer);
        }

        public bool IsValid()
        {
            if (!generatedDataContainer)
                return false;

            if (!colliderContainer ||
                colliders == null)   // must be an array, even if 0 length
                return false;

            if (renderables == null ||
                meshRenderers == null)
                return false;

            if (debugVisualizationRenderables == null ||
                debugMeshRenderers == null)
                return false;

            for (int i = 0; i < renderables.Length; i++)
            {
                if (renderables[i] == null ||
                    renderables[i].invalid)
                    continue;
                if (!renderables[i].IsValid())
                    return false;
            }

            for (int i = 0; i < debugVisualizationRenderables.Length; i++)
            {
                if (debugVisualizationRenderables[i] == null)
                    continue;
                if (!debugVisualizationRenderables[i].IsValid())
                    return false;
            }

            for (int i = 0; i < colliders.Length; i++)
            {
                if (colliders[i] == null ||
					!colliders[i].IsValid())
                    return false;
            }

            return true;
        }

        [NonSerialized] GameObjectState lastAppliedState;
        [NonSerialized] bool hasAppliedState;
        // the renderer settings FinishMeshUpdates last applied to every renderer
        [NonSerialized] bool rendererPropertiesApplied;
        [NonSerialized] (GameObject, Transform, MotionVectorGenerationMode, ReflectionProbeUsage, LightProbeUsage, bool, uint, ReceiveGI, bool, float, Material) lastRendererProperties;

        internal void UpdateContainersWhenModelStateChanged(ChiselModelComponent model)
        {
            if (!model || generatedDataContainer == null)
                return;
            var state = GameObjectState.Create(model.gameObject);
            if (hasAppliedState && state.SameAs(lastAppliedState))
                return;
            UpdateContainers(model.gameObject);
        }

        GameObjectState UpdateContainers(GameObject modelGameObject, bool force = false)
        {
            var modelTransform  = modelGameObject.transform;
            var gameObjectState = GameObjectState.Create(modelGameObject);
            // a renderable's container only changes with the model's state, or when its geometry comes or goes
            var sameState       = !force && hasAppliedState && gameObjectState.SameAs(lastAppliedState);
            lastAppliedState    = gameObjectState;
            hasAppliedState     = true;
            ChiselObjectUtility.UpdateContainerFlags(generatedDataContainer, gameObjectState);

            var containerTransform = generatedDataContainer.transform;
            var colliderTransform = colliderContainer.transform;

            // Make sure we're always a child of the model
            ChiselObjectUtility.ResetTransform(containerTransform, requiredParent: modelTransform);
            ChiselObjectUtility.ResetTransform(colliderTransform, requiredParent: containerTransform);
            ChiselObjectUtility.UpdateContainerFlags(colliderContainer, gameObjectState);

            for (int i = 0; i < renderables.Length; i++)
            {
                if (renderables[i] == null || renderables[i].invalid)
                    continue;
                var query = renderables[i].query;
                bool isRenderable   = (query & SurfaceDestinationFlags.Renderable) == SurfaceDestinationFlags.Renderable;
                // An empty slot takes no part in the baked lighting whatever its surfaces say - there is nothing there
                // to light, and claiming otherwise makes Unity trace rays against an empty mesh (see HasGeometry)
                bool isInBakedLight = renderables[i].HasGeometry &&
                                      (query & SurfaceDestinationFlags.ExcludedFromGlobalIllumination) == SurfaceDestinationFlags.None;
                if (sameState && renderables[i].containerApplied && renderables[i].containerInBakedLight == isInBakedLight)
                    continue;
                var renderableContainer = renderables[i].container;
                ChiselObjectUtility.UpdateContainerFlags(renderableContainer, gameObjectState, isRenderable: isRenderable, contributesGI: isInBakedLight);
                ChiselObjectUtility.ResetTransform(renderableContainer.transform, requiredParent: containerTransform);
                renderables[i].containerApplied      = true;
                renderables[i].containerInBakedLight = isInBakedLight;
            }

            if (debugVisualizationRenderables != null)
            {
                for (int i = 0; i < debugVisualizationRenderables.Length; i++)
                {
                    if (debugVisualizationRenderables[i] == null || debugVisualizationRenderables[i].invalid)
                        continue;
                    var renderableContainer = debugVisualizationRenderables[i].container;
                    ChiselObjectUtility.UpdateContainerFlags(renderableContainer, gameObjectState, isRenderable: true, debugVisualizationRenderer: true);
                    ChiselObjectUtility.ResetTransform(renderableContainer.transform, requiredParent: containerTransform);
                }
            }
            return gameObjectState;
        }

        internal void RestoreSkippedUpdate(ChiselModelComponent model)
        {
            UpdateContainers(model.gameObject, force: true);
            for (int i = 0; i < renderables.Length; i++)
            {
                if (renderables[i] == null || renderables[i].invalid)
                    continue;
                renderables[i].ApplyShadowSettings(model.RenderSettings);
                renderables[i].RestoreSelection();
            }
            for (int i = 0; i < debugVisualizationRenderables.Length; i++)
            {
                if (debugVisualizationRenderables[i] != null && !debugVisualizationRenderables[i].invalid)
                    debugVisualizationRenderables[i].RestoreSelection();
            }
            ChiselRenderObjects.UpdateProperties(model, meshRenderers);
            ChiselColliderObjects.UpdateProperties(model, colliders);
            needVisibilityMeshUpdate = true;
        }

		public static bool IsObjectGenerated(UnityEngine.Object obj)
		{
			if (!obj)
				return false;

			var gameObject = obj as GameObject;
			if (Equals(gameObject, null))
			{
				var component = obj as Component;
				gameObject = Equals(component, null) ? null : component.gameObject;
				if (Equals(gameObject, null))
					return false;
			}

			if (gameObject.name == kGeneratedContainerName)
				return true;

			var parent = gameObject.transform.parent;
			if (Equals(parent, null))
				return false;

			return parent.name == kGeneratedContainerName;
		}

        // Empty meshes that take the mesh data no renderer used (Mesh.ApplyAndDisposeWritableMeshData wants a mesh for each)
        static readonly System.Collections.Generic.List<Mesh> s_PaddingMeshes = new();

        static Mesh PaddingMesh(int index)
        {
            while (s_PaddingMeshes.Count <= index)
                s_PaddingMeshes.Add(null);
            if (!s_PaddingMeshes[index])
                s_PaddingMeshes[index] = new Mesh { name = "‹[generated-Padding]›", hideFlags = HideFlags.HideAndDontSave };
            return s_PaddingMeshes[index];
        }

        // The renderers of this update's render meshes: the one drawing the same material for the same query again, a new
        // one for a material that is new; the renderers of materials no longer drawn are destroyed. Each update's
        // objectIndex becomes its renderer's place in renderables. A kept mesh's renderer stays as it is; when one is
        // missing (its objects were deleted) the model is built again, its meshes all made anew.
        void FindRenderables(ChiselModelComponent model, GameObjectState gameObjectState, ref ChiselMeshUpdates meshUpdates)
        {
            var existing = DictionaryPool<(SurfaceDestinationFlags, ulong, uint), ChiselRenderObjects>.Get();
            try
            {
                if (renderables != null)
                {
                    foreach (var renderable in renderables)
                    {
                        if (renderable == null || renderable.invalid || !renderable.IsValid())
                        {
                            renderable?.Destroy();
                            continue;
                        }
                        if (!existing.TryAdd((renderable.query, renderable.surfaceParameter, renderable.chunk), renderable))
                            renderable.Destroy();
                    }
                }
                var containerTransform = generatedDataContainer.transform;
                var keptCount = meshUpdates.meshUpdatesKept.IsCreated ? meshUpdates.meshUpdatesKept.Length : 0;
                var found = new ChiselRenderObjects[meshUpdates.meshUpdatesRenderables.Length + keptCount];
                var missing = false;
                for (int k = 0; k < keptCount; k++)
                {
                    var kept  = meshUpdates.meshUpdatesKept[k];
                    var query = RendererQuery((int)kept.objectIndex);
                    if (!existing.Remove((query, kept.surfaceParameter, kept.chunk), out var renderable) || !renderable.HasGeometry)
                    {
                        missing = true;
                        renderable?.Destroy();
                        renderable = null;
                    }
                    found[meshUpdates.meshUpdatesRenderables.Length + k] = renderable;
                }
                for (int i = 0; i < meshUpdates.meshUpdatesRenderables.Length; i++)
                {
                    var update = meshUpdates.meshUpdatesRenderables[i];
                    var query  = RendererQuery((int)update.objectIndex);
                    if (!existing.Remove((query, update.surfaceParameter, update.chunk), out var renderable))
                    {
                        renderable = ChiselRenderObjects.Create(RendererName((int)update.objectIndex, update.surfaceParameter), containerTransform, gameObjectState, query);
                        renderable.surfaceParameter = update.surfaceParameter;
                        renderable.chunk = update.chunk;
                    }
                    found[i] = renderable;
                    update.objectIndex = (ulong)i;
                    meshUpdates.meshUpdatesRenderables[i] = update;
                }
                foreach (var unused in existing.Values)
                    unused.Destroy();
                if (missing)
                {
                    found = Array.FindAll(found, renderable => renderable != null);
                    ChiselNodeHierarchyManager.RebuildTreeNodes(model);
                }
                renderables   = found;
                meshRenderers = new MeshRenderer[found.Length];
                for (int i = 0; i < found.Length; i++)
                    meshRenderers[i] = found[i].meshRenderer;
            }
            finally
            {
                DictionaryPool<(SurfaceDestinationFlags, ulong, uint), ChiselRenderObjects>.Release(existing);
            }
        }

        // The same for the debug visualizations, by mode and chunk: a mode no draw mode shows has none
        void FindDebugRenderables(ChiselModelComponent model, GameObjectState gameObjectState, ref ChiselMeshUpdates meshUpdates)
        {
            var existing = DictionaryPool<(int, uint), ChiselRenderObjects>.Get();
            try
            {
                if (debugVisualizationRenderables != null)
                {
                    foreach (var renderable in debugVisualizationRenderables)
                    {
                        if (renderable == null || renderable.invalid || !renderable.IsValid() ||
                            renderable.debugMode < 0 || renderable.debugMode >= kVisualizationModeCount ||
                            !existing.TryAdd((renderable.debugMode, renderable.chunk), renderable))
                            renderable?.Destroy();
                    }
                }
                var containerTransform = generatedDataContainer.transform;
                var updates   = meshUpdates.meshUpdatesDebugVisualizations;
                var keptCount = meshUpdates.meshUpdatesKeptDebugVisualizations.IsCreated ? meshUpdates.meshUpdatesKeptDebugVisualizations.Length : 0;
                var found     = new ChiselRenderObjects[updates.Length + keptCount];
                var missing   = false;
                for (int k = 0; k < keptCount; k++)
                {
                    var kept = meshUpdates.meshUpdatesKeptDebugVisualizations[k];
                    if (!existing.Remove(((int)kept.objectIndex, kept.chunk), out var renderable) || !renderable.HasGeometry)
                    {
                        missing = true;
                        renderable?.Destroy();
                        renderable = null;
                    }
                    found[updates.Length + k] = renderable;
                }
                for (int i = 0; i < updates.Length; i++)
                {
                    var update = updates[i];
                    var mode   = (int)update.objectIndex;
                    if (!existing.Remove((mode, update.chunk), out var renderable))
                    {
                        renderable = ChiselRenderObjects.Create(kGeneratedVisualizationRendererNames[mode], containerTransform, gameObjectState,
                                                                AssignMeshesJob.kGeneratedDebugRendererFlags[mode].Item1, debugVisualizationRenderer: true);
                        renderable.debugMode = mode;
                        renderable.chunk     = update.chunk;
                    }
                    found[i] = renderable;
                    update.objectIndex = (ulong)i;
                    updates[i] = update;
                }
                foreach (var unused in existing.Values)
                    unused.Destroy();
                if (missing)
                {
                    found = Array.FindAll(found, renderable => renderable != null);
                    ChiselNodeHierarchyManager.RebuildTreeNodes(model);
                }
                debugVisualizationRenderables = found;
                debugMeshRenderers = new MeshRenderer[found.Length];
                for (int i = 0; i < found.Length; i++)
                    debugMeshRenderers[i] = found[i].meshRenderer;
            }
            finally
            {
                DictionaryPool<(int, uint), ChiselRenderObjects>.Release(existing);
            }
        }

        static string RendererName(int index, ulong surfaceParameter)
        {
            var name = (index >= 0 && index < kGeneratedMeshRendererNames.Length ? kGeneratedMeshRendererNames[index] : null) ?? "‹[generated]›";
            if (surfaceParameter == 0)
                return name;
            var material = Resources.EntityIdToObject(UnityEngine.EntityId.FromULong(surfaceParameter)) as Material;
            return material ? $"{name} {material.name}" : name;
        }

		// in between UpdateMeshes and FinishMeshUpdates our jobs should be force completed, so we can now upload our meshes to unity Meshes
		public int FinishMeshUpdates(ChiselModelComponent model, 
                                     ChiselMeshUpdates meshUpdates, JobHandle dependencies)
		{
            GameObject modelGameObject = model.gameObject;

            var gameObjectStates = DictionaryPool<ChiselModelComponent, GameObjectState>.Get();
			var renderMeshUpdates = ListPool<ChiselMeshUpdate>.Get();
			//var colliderObjectUpdates = ListPool<ChiselColliderObjectUpdate>.Get();
			var renderObjectUpdates = ListPool<ChiselRenderObjectUpdate>.Get();
			var colliderObjects = ListPool<ChiselColliderObjects>.Get();
			var foundMeshes = ListPool<Mesh>.Get();
			var usedRenderMeshes = HashSetPool<int>.Get();
			try
			{
                GameObjectState gameObjectState;
                {
                    Profiler.BeginSample("Setup");
                    gameObjectState = UpdateContainers(modelGameObject);
                    gameObjectStates.Add(model, gameObjectState);
                    Profiler.EndSample();
                }

                Debug.Assert(SurfaceParameterIndex.Parameter1 < SurfaceParameterIndex.Parameter2);
                Debug.Assert((SurfaceParameterIndex.Parameter1 + 1) == SurfaceParameterIndex.Parameter2);

                dependencies.Complete();

                Profiler.BeginSample("FindRenderables");
                FindRenderables(model, gameObjectState, ref meshUpdates);
                FindDebugRenderables(model, gameObjectState, ref meshUpdates);
                Profiler.EndSample();

                Debug.Assert(!meshUpdates.vertexBufferContents.meshDescriptions.IsCreated ||
                             meshUpdates.vertexBufferContents.meshDescriptions.Length == 0 ||
                             meshUpdates.vertexBufferContents.meshDescriptions[0].meshQuery.LayerParameterIndex >= SurfaceParameterIndex.None);


                Profiler.BeginSample("Init");
                var colliderCount = meshUpdates.meshUpdatesColliders.Length;
                if (colliderObjects.Capacity < colliderCount)
                    colliderObjects.Capacity = colliderCount;
                for (int i = 0; i < colliderCount; i++)
                    colliderObjects.Add(null);
                for (int i = 0; i < meshUpdates.meshUpdatesDebugVisualizations.Length; i++)
                    renderMeshUpdates.Add(meshUpdates.meshUpdatesDebugVisualizations[i]);
                for (int i = 0; i < meshUpdates.meshUpdatesRenderables.Length; i++)
                    renderMeshUpdates.Add(meshUpdates.meshUpdatesRenderables[i]);
                renderMeshUpdates.Sort(delegate (ChiselMeshUpdate x, ChiselMeshUpdate y)
                {
                    return x.contentsIndex - y.contentsIndex;
                });
                Profiler.EndSample();



                // Now do all kinds of book-keeping code that we might as well do while our jobs are running on other threads
                Profiler.BeginSample("new_ChiselDebugObjectUpdate");
                for (int i = 0; i < meshUpdates.meshUpdatesDebugVisualizations.Length; i++)
                {
                    var debugVisualizationMeshUpdate = meshUpdates.meshUpdatesDebugVisualizations[i];
                    var instance = debugVisualizationRenderables[debugVisualizationMeshUpdate.objectIndex];
                    renderObjectUpdates.Add(new ChiselRenderObjectUpdate
                    {
                        meshIndex         = debugVisualizationMeshUpdate.meshIndex,
						materialOverride  = ChiselProjectSettings.DebugVisualizationMaterials[instance.debugMode],
                        instance          = instance,
                        model             = model
                    });
                }
                Profiler.EndSample();

                Profiler.BeginSample("new_ChiselRenderObjectUpdate");
                usedRenderMeshes.Clear();
                var forceShadowOnlyMaterial = ChiselProjectSettings.ForceShadowOnlySurfacesMaterial;
                for (int i = 0; i < meshUpdates.meshUpdatesRenderables.Length; i++)
                {
                    var renderMeshUpdate = meshUpdates.meshUpdatesRenderables[i];
                    usedRenderMeshes.Add((int)renderMeshUpdate.objectIndex);
                    
					var instance = renderables[renderMeshUpdate.objectIndex];
                    renderObjectUpdates.Add(new ChiselRenderObjectUpdate
                    {
                        meshIndex         = renderMeshUpdate.meshIndex,
						materialOverride  = ((instance.query & SurfaceDestinationFlags.RenderShadowsCasting) == SurfaceDestinationFlags.ShadowCasting) ? forceShadowOnlyMaterial : null,
                        instance          = instance,
                        model             = model
					});
                }
                Profiler.EndSample();

                Profiler.BeginSample("foundMeshes.InMeshIndexOrder");
                for (int i = 0; i < renderMeshUpdates.Count; i++)
                {
                    var meshUpdate = renderMeshUpdates[i];
                    var instance = (meshUpdate.type == ChiselMeshType.DebugVisualization)
                                 ? debugVisualizationRenderables[meshUpdate.objectIndex]
                                 : renderables[meshUpdate.objectIndex];
                    foundMeshes.Add(instance.sharedMesh);
                }
                Profiler.EndSample();

                Profiler.BeginSample("new_ChiselPhysicsObjectUpdate");
                // the existing colliders by physics material and chunk
                var colliderOf = new System.Collections.Generic.Dictionary<(ulong, uint), int>(colliders.Length);
                for (int j = 0; j < colliders.Length; j++)
                {
                    if (colliders[j] != null)
                        colliderOf.TryAdd((colliders[j].surfaceParameter, colliders[j].chunk), j);
                }
                for (int i = 0; i < meshUpdates.meshUpdatesColliders.Length; i++)
                {
                    var colliderMeshUpdate = meshUpdates.meshUpdatesColliders[i];

                    var surfaceParameter = colliderMeshUpdate.objectIndex;
                    var colliderIndex = colliderMeshUpdate.colliderIndex;

                    if (colliderOf.Remove((surfaceParameter, colliderMeshUpdate.chunk), out var existing))
                    {
                        colliderObjects[colliderIndex] = colliders[existing];
                        colliders[existing] = null;
                    }

                    Profiler.BeginSample("Create.Colliders");
                    if (colliderObjects[colliderIndex] == null)
                        colliderObjects[colliderIndex] = ChiselColliderObjects.Create(colliderContainer, surfaceParameter, colliderMeshUpdate.chunk);
                    Profiler.EndSample();

                    var instance = colliderObjects[colliderIndex];
                    foundMeshes.Add(instance.sharedMesh);
                    //colliderObjectUpdates.Add(new ChiselColliderObjectUpdate { meshIndex = colliderMeshUpdate.meshIndex, });
                }
                Profiler.EndSample();

                Profiler.BeginSample("Renderers.UpdateMaterials");
                ChiselRenderObjects.SetTriangleBrushes(renderMeshUpdates, renderObjectUpdates, ref meshUpdates.vertexBufferContents);
                ChiselRenderObjects.UpdateMaterials(renderMeshUpdates, renderObjectUpdates, ref meshUpdates.vertexBufferContents);
                Profiler.EndSample();


                // a kept collider keeps its mesh and its bake; one that has gone missing builds the model again
                Profiler.BeginSample("Kept.Colliders");
                var keptColliders = ListPool<ChiselColliderObjects>.Get();
                if (meshUpdates.meshUpdatesKeptColliders.IsCreated)
                {
                    foreach (var keptUpdate in meshUpdates.meshUpdatesKeptColliders)
                    {
                        if (!colliderOf.Remove((keptUpdate.objectIndex, keptUpdate.chunk), out var found) ||
                            colliders[found] == null || !colliders[found].IsValid())
                            found = -1;
                        if (found < 0)
                        {
                            ChiselNodeHierarchyManager.RebuildTreeNodes(model);
                            continue;
                        }
                        keptColliders.Add(colliders[found]);
                        colliders[found] = null;
                    }
                }
                Profiler.EndSample();

                Profiler.BeginSample("CleanUp.Colliders");
                for (int j = 0; j < colliders.Length; j++)
                {
                    if (colliders[j] != null)
                        colliders[j].Destroy();
                }
                Profiler.EndSample();

                Profiler.BeginSample("Assign.Colliders");
                var changedColliders = new ChiselColliderObjects[colliderCount];
                for (int i = 0; i < colliderCount; i++)
                    changedColliders[i] = colliderObjects[i];
                colliders = new ChiselColliderObjects[colliderCount + keptColliders.Count];
                for (int i = 0; i < colliderCount; i++)
                    colliders[i] = colliderObjects[i];
                for (int i = 0; i < keptColliders.Count; i++)
                    colliders[colliderCount + i] = keptColliders[i];
                ListPool<ChiselColliderObjects>.Release(keptColliders);
                Profiler.EndSample();

                Profiler.BeginSample("Renderers.Update");
                ChiselRenderObjects.UpdateSettings(renderMeshUpdates, renderObjectUpdates, gameObjectStates, ref meshUpdates.vertexBufferContents);
                Profiler.EndSample();

                // the model's shadow settings reach the renderers whose meshes were kept too
                Profiler.BeginSample("Renderers.ShadowSettings");
                foreach (var renderable in renderables)
                    renderable?.ApplyShadowSettings(model.RenderSettings);
                Profiler.EndSample();

                Debug.Assert(foundMeshes.Count <= meshUpdates.meshDataArray.Length);

                var realMeshDataArraySize = meshUpdates.meshDataArray.Length;
                {
                    // foundMeshes has to be exactly as long as meshDataArray; the extra entries are set to empty.

                    // TODO: figure out why the maximum meshDataArray.Length does not match the maximum used meshes?

                    int meshDataArrayOffset = foundMeshes.Count;

                    // the mesh data allocated for materials no renderer drew this time
                    for (int pad = 0; foundMeshes.Count < meshUpdates.meshDataArray.Length; pad++)
                    {
                        foundMeshes.Add(PaddingMesh(pad));
                        var emptyMeshData = meshUpdates.meshDataArray[meshDataArrayOffset];
                        emptyMeshData.SetIndexBufferParams(0, IndexFormat.UInt32);
                        emptyMeshData.SetVertexBufferParams(0, VertexBufferContents.RenderDescriptors);
                        emptyMeshData.subMeshCount = 0;
                        meshDataArrayOffset++;
                    }
                }

                Profiler.BeginSample("UpdateMeshRenderers");
                // unchanged settings only go to the renderers that were updated
                var rendererProperties = ChiselRenderObjects.PropertiesOf(model);
                var rendererPropertiesChanged = !rendererPropertiesApplied || !rendererProperties.Equals(lastRendererProperties);
#if UNITY_EDITOR
                rendererPropertiesChanged |= model.RenderSettings.serializedObjectFieldsDirty;
#endif
                if (rendererPropertiesChanged)
                {
                    ChiselRenderObjects.UpdateProperties(model, this.meshRenderers);
                    rendererPropertiesApplied = true;
                    lastRendererProperties    = rendererProperties;
                } else
                    ChiselRenderObjects.UpdateProperties(model, renderObjectUpdates);
                Profiler.EndSample();

                // Updates the Unity Mesh-es that are used in our MeshRenderers and MeshColliders
                // MeshUpdateFlags => Bounds are never updated no matter what flag you use
                Profiler.BeginSample("ApplyAndDisposeWritableMeshData");
                if (meshUpdates.meshDataArray.Length > 0)
                {
                    if (realMeshDataArraySize > 0) // possible that all meshes are empty
                        Mesh.ApplyAndDisposeWritableMeshData(meshUpdates.meshDataArray, foundMeshes,
                                                             UnityEngine.Rendering.MeshUpdateFlags.DontRecalculateBounds);
                    else
                        meshUpdates.meshDataArray.Dispose();
                }
                meshUpdates.meshDataArray = default;
                Profiler.EndSample();

                // TODO: user meshDataArray data to determine if colliders are visible or not, then we can move this before the Apply
                Profiler.BeginSample("UpdateColliders");
                ChiselColliderObjects.UpdateProperties(model, this.colliders);
                Profiler.EndSample();

                // So we need to update the bounds ourselves
                Profiler.BeginSample("Mesh.UpdateBounds");
                ChiselRenderObjects.UpdateBounds(renderObjectUpdates);
                // TODO: user meshDataArray data to determine the bounds and set it directly
                for (int i = 0; i < changedColliders.Length; i++)
                    changedColliders[i].sharedMesh.RecalculateBounds();
                Profiler.EndSample();

                Profiler.BeginSample("Schedule Collider bake");
                if (changedColliders.Length > 0)
                    ChiselColliderObjects.ScheduleColliderBake(model, changedColliders);
                Profiler.EndSample();

                this.needVisibilityMeshUpdate = true;

                var foundMeshCount = foundMeshes.Count;
                foundMeshes.Clear();
                return foundMeshCount;
            }
            finally
			{
				DictionaryPool<ChiselModelComponent, GameObjectState>.Release(gameObjectStates);
				ListPool<ChiselMeshUpdate>.Release(renderMeshUpdates);
				//ListPool<ChiselColliderObjectUpdate>.Release(colliderObjectUpdates);
				ListPool<ChiselRenderObjectUpdate>.Release(renderObjectUpdates);
				ListPool<ChiselColliderObjects>.Release(colliderObjects);
				ListPool<Mesh>.Release(foundMeshes);
				HashSetPool<int>.Release(usedRenderMeshes);
			}
        }

#if UNITY_EDITOR
        public void HideDebugVisualizationSurfaces()
        {
            for (int i = 0; i < renderables.Length; i++)
            {
                var renderable = renderables[i];
                if (renderable == null ||
                    renderable.invalid ||
                    !renderable.meshRenderer)
                {
                    if (renderable.container)
                        UnityEngine.Object.DestroyImmediate(renderable.container);
                    continue;
                }

                renderable.meshRenderer.forceRenderingOff = false;
            }

            for (int i = 0; i < debugVisualizationRenderables.Length; i++)
            {
                if (debugVisualizationRenderables[i].container)
                    UnityEngine.Object.DestroyImmediate(debugVisualizationRenderables[i].container);
            }
        }

        static bool ShowsDebugMode(DrawModeFlags drawModeFlags, int debugMode)
        {
            return debugMode >= 0 && debugMode < kVisualizationModeCount &&
                   (drawModeFlags & kGeneratedVisualizationShowFlags[debugMode]) != DrawModeFlags.None;
        }

        public void UpdateDebugVisualizationState(in BrushVisibilityLookup brushVisibilityLookup, DrawModeFlags drawModeFlags, bool ignoreBrushVisibility = false)
        {
			//if (!ignoreBrushVisibility)
			ChiselUnityVisibilityManager.UpdateVisibility();
            
            var showRenderables = (drawModeFlags & DrawModeFlags.HideRenderables) == DrawModeFlags.None;
            // the occlusion culling visualizer shows what occlusion culling sees: every mesh whole, hidden brushes too
            var showWhole = UnityEditor.StaticOcclusionCullingVisualization.showOcclusionCulling;

            // with the same visibility and draw mode only the renderables whose meshes changed have anything to do
            var unchanged = visibilityStateKnown &&
                            lastVisibilityVersion == brushVisibilityLookup.Version &&
                            lastDrawModeFlags == drawModeFlags && lastShowWhole == showWhole;
            if (unchanged && !needVisibilityMeshUpdate)
                return;
            visibilityStateKnown  = true;
            lastVisibilityVersion = brushVisibilityLookup.Version;
            lastDrawModeFlags     = drawModeFlags;
            lastShowWhole         = showWhole;

            if (debugVisualizationRenderables != null)
            {
                for (int i = 0; i < debugVisualizationRenderables.Length; i++)
                {
                    if (debugVisualizationRenderables[i] != null)
                        debugVisualizationRenderables[i].visible = ShowsDebugMode(drawModeFlags, debugVisualizationRenderables[i].debugMode);
                }
            }

            //if (ignoreBrushVisibility || !needVisibilityMeshUpdate)
            //    return;

            //if (visibilityState == VisibilityState.Mixed)
            {
                for (int i = 0; i < renderables.Length; i++)
                {
                    var renderable = renderables[i];
                    if (renderable == null ||
                        renderable.invalid ||
                        (unchanged && !renderable.partialMeshDirty))
                        continue;

                    renderable.UpdateVisibilityMesh(brushVisibilityLookup, showRenderables);

                    // A renderer whose brushes are all shown draws itself, so Unity culls it (occlusion culling, its
                    // visualizer); otherwise it is drawn by OnRenderModel, through its copy without the hidden brushes
                    if (renderable.meshRenderer == null)
                        continue;
                    var drawsItself = showRenderables && (renderable.AllBrushesShown || showWhole);
                    if (renderable.meshRenderer.forceRenderingOff == drawsItself)
                        renderable.meshRenderer.forceRenderingOff = !drawsItself;
                    if (drawsItself)
                        renderable.enabled = true;
                }

                if (debugVisualizationRenderables != null)
                {
                    for (int i = 0; i < debugVisualizationRenderables.Length; i++)
                    {
                        var debugVisualizationRenderable = debugVisualizationRenderables[i];
                        if (debugVisualizationRenderable == null)
                            continue;

                        debugVisualizationRenderable.UpdateVisibilityMesh(brushVisibilityLookup, ShowsDebugMode(drawModeFlags, debugVisualizationRenderable.debugMode));
                    }
                }
            }

            needVisibilityMeshUpdate = false;
        }
#endif
    }
}