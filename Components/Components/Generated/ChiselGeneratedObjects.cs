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
    
    //
    // 1. figure out what you where trying to do here, and remove need for the dictionary
    // 2. then do the same for the rendering equiv.
    // 3. separate building/updating the components from building a combined (phsyics)materials/mesh/rendersetting
    // 4. move colliders to single gameobject
    // 5. build meshes with submeshes, have a fixed number of meshes. one mesh per renderingsetting type (shadow-only etc.)
    // 6. have a secondary version of these fixed number of meshes that has partial meshes & use those for rendering in chiselmodel
    // 7. have a way to identify which triangles belong to which brush. so we can build partial meshes
    // 8. profit!
    //
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

            var renderables = new ChiselRenderObjects[kGeneratedMeshRenderCount];
            for (int i = 0; i < kGeneratedMeshRenderCount; i++)
                renderables[i] = new() { invalid = true };
            var meshRenderers = new MeshRenderer[kGeneratedMeshRendererCount];
            for (int r = 0; r < kGeneratedMeshRendererCount; r++)
            {
                var index = kGeneratedMeshRendererIndices[r];
                Debug.Assert(RendererQuery(index).RendererIndex() == index);
                renderables[index] = ChiselRenderObjects.Create(kGeneratedMeshRendererNames[index], containerTransform, gameObjectState, RendererQuery(index));
                renderables[index].invalid = false;
                meshRenderers[r] = renderables[index].meshRenderer;
            }

            var debugVisualizationRenderables = new ChiselRenderObjects[kVisualizationModeCount];
            var debugMeshRenderers = new MeshRenderer[kVisualizationModeCount];
            for (int i = 0; i < kVisualizationModeCount; i++)
            {
                debugVisualizationRenderables[i] = ChiselRenderObjects.Create(kGeneratedVisualizationRendererNames[i], containerTransform, gameObjectState, AssignMeshesJob.kGeneratedDebugRendererFlags[i].Item1, debugVisualizationRenderer: true);
                debugMeshRenderers[i] = debugVisualizationRenderables[i].meshRenderer;
                debugVisualizationRenderables[i].invalid = false;
            }

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
                renderables.Length != kGeneratedMeshRenderCount ||
                meshRenderers == null ||
                meshRenderers.Length != kGeneratedMeshRendererCount)
                return false;

            if (debugVisualizationRenderables == null ||
                debugVisualizationRenderables.Length != kVisualizationModeCount ||
                debugMeshRenderers == null ||
                debugMeshRenderers.Length != kVisualizationModeCount)
                return false;

            // These queries are valid, and should never be null (We don't care about the other queries)
            foreach (var index in kGeneratedMeshRendererIndices)
            {
                if (renderables[index] == null)
                    return false;
            }

            // These queries are valid, and should never be null (We don't care about the other queries)
            for (int i = 0; i < kVisualizationModeCount;i++)
            { 
                if (debugVisualizationRenderables[i] == null)
                    return false;
            }
            
            for (int i = 0; i < renderables.Length; i++)
            {
                if (renderables[i] != null)
                    renderables[i].invalid = Array.IndexOf(kGeneratedMeshRendererIndices, i) < 0;
            }

            for (int i = 0; i < kVisualizationModeCount; i++)
                debugVisualizationRenderables[i].invalid = false;

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

        internal void UpdateContainersWhenModelStateChanged(ChiselModelComponent model)
        {
            if (!model || generatedDataContainer == null)
                return;
            var state = GameObjectState.Create(model.gameObject);
            if (hasAppliedState && state.SameAs(lastAppliedState))
                return;
            UpdateContainers(model.gameObject);
        }

        GameObjectState UpdateContainers(GameObject modelGameObject)
        {
            var modelTransform  = modelGameObject.transform;
            var gameObjectState = GameObjectState.Create(modelGameObject);
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
                var renderableContainer = renderables[i].container;
                ChiselObjectUtility.UpdateContainerFlags(renderableContainer, gameObjectState, isRenderable: isRenderable, contributesGI: isInBakedLight);
                ChiselObjectUtility.ResetTransform(renderableContainer.transform, requiredParent: containerTransform);
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
            UpdateContainers(model.gameObject);
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
			var usedDebugVisualizations = HashSetPool<int>.Get();
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
                usedDebugVisualizations.Clear();
                for (int i = 0; i < meshUpdates.meshUpdatesDebugVisualizations.Length; i++)
                {
                    var debugVisualizationMeshUpdate = meshUpdates.meshUpdatesDebugVisualizations[i];
                    usedDebugVisualizations.Add((int)debugVisualizationMeshUpdate.objectIndex);
                    var instance = debugVisualizationRenderables[debugVisualizationMeshUpdate.objectIndex];
                    renderObjectUpdates.Add(new ChiselRenderObjectUpdate
                    {
                        meshIndex         = debugVisualizationMeshUpdate.meshIndex,
						materialOverride  = ChiselProjectSettings.DebugVisualizationMaterials[debugVisualizationMeshUpdate.objectIndex],
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
                for (int i = 0; i < meshUpdates.meshUpdatesColliders.Length; i++)
                {
                    var colliderMeshUpdate = meshUpdates.meshUpdatesColliders[i];

                    var surfaceParameter = colliderMeshUpdate.objectIndex;
                    var colliderIndex = colliderMeshUpdate.colliderIndex;

                    // TODO: optimize
                    for (int j = 0; j < colliders.Length; j++)
                    {
                        if (colliders[j] == null)
                            continue;
                        if (colliders[j].surfaceParameter != surfaceParameter)
                            continue;

                        colliderObjects[colliderIndex] = colliders[j];
                        colliders[j] = null;
                        break;
                    }

                    Profiler.BeginSample("Create.Colliders");
                    if (colliderObjects[colliderIndex] == null)
                        colliderObjects[colliderIndex] = ChiselColliderObjects.Create(colliderContainer, surfaceParameter);
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


                Profiler.BeginSample("CleanUp.Colliders");
                for (int j = 0; j < colliders.Length; j++)
                {
                    if (colliders[j] != null)
                        colliders[j].Destroy();
                }
                Profiler.EndSample();

                Profiler.BeginSample("Assign.Colliders");
                if (colliders.Length != colliderCount)
                    colliders = new ChiselColliderObjects[colliderCount];
                for (int i = 0; i < colliderCount; i++)
                    colliders[i] = colliderObjects[i];
                Profiler.EndSample();

                Profiler.BeginSample("Renderers.Update");
                ChiselRenderObjects.UpdateSettings(renderMeshUpdates, renderObjectUpdates, gameObjectStates, ref meshUpdates.vertexBufferContents);
                Profiler.EndSample();

                Debug.Assert(foundMeshes.Count <= meshUpdates.meshDataArray.Length);

                var realMeshDataArraySize = meshUpdates.meshDataArray.Length;
                {
                    // FIXME: This is a hack to ensure foundMeshes is the same exact length as meshDataArray
                    // (All these need to be set to empty anyway)

                    // TODO: figure out why the maximum meshDataArray.Length does not match the maximum used meshes?

                    int meshDataArrayOffset = foundMeshes.Count;
                    for (int i = 0; i < renderables.Length; i++)
                    {
                        if (usedRenderMeshes.Contains(i))
                            continue;

                        var instance = renderables[i];
                        if (instance.meshRenderer &&
                            instance.meshRenderer.enabled)
                            instance.meshRenderer.enabled = false;
                        if (foundMeshes.Count < meshUpdates.meshDataArray.Length)
                        {
                            var sharedMesh = instance.sharedMesh;
                            if (!sharedMesh || foundMeshes.Contains(sharedMesh))
                                continue;

                            instance.geometryHashValue = 0;
							instance.surfaceHashValue = 0;

							foundMeshes.Add(sharedMesh);
                            var emptyMeshData = meshUpdates.meshDataArray[meshDataArrayOffset];
                            emptyMeshData.SetIndexBufferParams(0, IndexFormat.UInt32);
                            emptyMeshData.SetVertexBufferParams(0, VertexBufferContents.RenderDescriptors);
                            emptyMeshData.subMeshCount = 0;
                            meshDataArrayOffset++;
                        }
                    }

                    for (int i = 0; i < debugVisualizationRenderables.Length; i++)
                    {
                        if (usedDebugVisualizations.Contains(i))
                            continue;

                        var instance = debugVisualizationRenderables[i];
                        if (instance.meshRenderer &&
                            instance.meshRenderer.enabled)
                            instance.meshRenderer.enabled = false;
                        if (foundMeshes.Count < meshUpdates.meshDataArray.Length)
                        {
                            var sharedMesh = instance.sharedMesh;
                            if (!sharedMesh || foundMeshes.Contains(sharedMesh))
                                continue;

                            foundMeshes.Add(sharedMesh);
                            var emptyMeshData = meshUpdates.meshDataArray[meshDataArrayOffset];
                            emptyMeshData.SetIndexBufferParams(0, IndexFormat.UInt32);
                            emptyMeshData.SetVertexBufferParams(0, VertexBufferContents.RenderDescriptors);
                            emptyMeshData.subMeshCount = 0;
                            meshDataArrayOffset++;
                        }
                    }
                }

                Profiler.BeginSample("UpdateMeshRenderers");
                ChiselRenderObjects.UpdateProperties(model, this.meshRenderers);
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
                for (int i = 0; i < colliders.Length; i++)
                    colliders[i].sharedMesh.RecalculateBounds();
                Profiler.EndSample();

                Profiler.BeginSample("Schedule Collider bake");
                ChiselColliderObjects.ScheduleColliderBake(model, this.colliders);
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
				HashSetPool<int>.Release(usedDebugVisualizations);
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

        public void UpdateDebugVisualizationState(BrushVisibilityLookup brushVisibilityLookup, DrawModeFlags drawModeFlags, bool ignoreBrushVisibility = false)
        {
			//if (!ignoreBrushVisibility)
			ChiselUnityVisibilityManager.UpdateVisibility();
            
            var shouldHideMesh  = true/* !ignoreBrushVisibility &&
                                  visibilityState != VisibilityState.AllVisible &&
                                  visibilityState != VisibilityState.Unknown*/;
                                  
            var showRenderables = (drawModeFlags & DrawModeFlags.HideRenderables) == DrawModeFlags.None;
            for (int i = 0; i < renderables.Length; i++)
            {
                var renderable = renderables[i];
                if (renderable == null ||
                    renderable.invalid)
                    continue;

                if (renderable.meshRenderer != null)
                    renderable.meshRenderer.forceRenderingOff = shouldHideMesh || !showRenderables;
            }

            if (debugVisualizationRenderables != null)
            {
                for (int i = 0; i < debugVisualizationRenderables.Length; i++)
                {
                    var showState = (drawModeFlags & kGeneratedVisualizationShowFlags[i]) != DrawModeFlags.None;
                    debugVisualizationRenderables[i].visible = showState;
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
                        renderable.invalid)
                        continue;

                    renderable.UpdateVisibilityMesh(brushVisibilityLookup, showRenderables);
                }

                if (debugVisualizationRenderables != null)
                {
                    for (int i = 0; i < debugVisualizationRenderables.Length; i++)
                    {
                        var show = (drawModeFlags & kGeneratedVisualizationShowFlags[i]) != DrawModeFlags.None;
                        var debugVisualizationRenderable = debugVisualizationRenderables[i];
                        if (debugVisualizationRenderable == null)
                            continue;

                        debugVisualizationRenderable.UpdateVisibilityMesh(brushVisibilityLookup, show);
                    }
                }
            }

            needVisibilityMeshUpdate = false;
        }
#endif
    }
}