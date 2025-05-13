using System;
using System.Collections.Generic;
using Chisel.Core;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Profiling;
using UnityEngine.Pool;
using System.Runtime.CompilerServices;
using Unity.Mathematics;

namespace Chisel.Components
{
    public struct ChiselRenderObjectUpdate
    {
        public int                  meshIndex;
        public ChiselRenderObjects  instance;
        public Material             materialOverride;
        public bool                 meshIsModified;
        public ChiselModelComponent model;
	}
    

	public struct SelectionOffset
	{
		public int offset;
		public readonly int Count { get { return selectionIndexDescriptions?.Length ?? 0; } }
		public int hashcode;
		public HashSet<int> skipSelectionID;
		public ChiselRenderObjects renderObjects;
		public SelectionDescription[] selectionIndexDescriptions;
	}


    [Serializable]
    public class ChiselRenderObjects
    {
        [HideInInspector] [SerializeField] internal bool invalid = false;
        public bool Valid 
        { 
            get
            {
                return (this != null) && !invalid;
            }
        }
        public SurfaceDestinationFlags  query;
        public GameObject       container;
        public Mesh             sharedMesh;
        public bool             enabled = true;
#if UNITY_EDITOR
        public Mesh             partialMesh;
        public int              selectionMeshHashcode = ~0;
        public Mesh             selectionMesh;
        [NonSerialized, HideInInspector]
        public bool             visible;
#endif
        public MeshFilter       meshFilter;
        public MeshRenderer     meshRenderer;
        public Material[]       renderMaterials;

		public ManagedSubMeshTriangleLookup triangleBrushes = new();
        
        public uint             geometryHashValue;
        public uint             surfaceHashValue;

        public bool             debugVisualizationRenderer;
        [NonSerialized] public float uvLightmapUpdateTime;

        internal ChiselRenderObjects() { }
        public static ChiselRenderObjects Create(string name, Transform parent, GameObjectState state, SurfaceDestinationFlags query, bool debugVisualizationRenderer = false)
        {
            var renderContainer = ChiselObjectUtility.CreateGameObject(name, parent, state, debugVisualizationRenderer: debugVisualizationRenderer);
            var meshFilter      = renderContainer.AddComponent<MeshFilter>();
            var meshRenderer    = renderContainer.AddComponent<MeshRenderer>();
            meshRenderer.enabled = false;

            var renderObjects = new ChiselRenderObjects
            {
                invalid             = false,     
                visible             = !debugVisualizationRenderer,       
                query               = query,
                container           = renderContainer,
                meshFilter          = meshFilter,
                meshRenderer        = meshRenderer,
                renderMaterials     = new Material[0],
                debugVisualizationRenderer = debugVisualizationRenderer
            };
            renderObjects.EnsureMeshesAllocated();
            renderObjects.Initialize();
            return renderObjects;
        }


        void EnsureMeshesAllocated()
        {
            if (sharedMesh == null) sharedMesh = new Mesh { name = meshFilter.gameObject.name };
#if UNITY_EDITOR
            if (partialMesh == null)
            {
				partialMesh = new Mesh
				{
					name = meshFilter.gameObject.name,
					hideFlags = HideFlags.DontSave
				};
			}
			if (selectionMesh == null)
			{
				selectionMesh = new Mesh
				{
					name = meshFilter.gameObject.name,
					hideFlags = HideFlags.DontSave 
				};
				selectionMeshHashcode = ~HashCode.Combine(triangleBrushes?.hashCode ?? 0, 0, sharedMesh.GetHashCode());
				// TODO: do this on level load instead?
				ChiselUnityVisibilityManager.UpdateVisibility(true);
			}
            if (string.IsNullOrEmpty(sharedMesh.name))
				sharedMesh.name = meshFilter.gameObject.name;
#endif
		}

        public void Destroy()
        {
            if (invalid)
                return;

#if UNITY_EDITOR
            ChiselObjectUtility.SafeDestroy(partialMesh);   partialMesh = null;
            ChiselObjectUtility.SafeDestroy(selectionMesh); selectionMesh = null;
#endif
			ChiselObjectUtility.SafeDestroy(sharedMesh);
            ChiselObjectUtility.SafeDestroy(container, ignoreHierarchyEvents: true);
            container       = null;
            sharedMesh      = null;
            meshFilter      = null;
            meshRenderer    = null;
            renderMaterials = null;
            invalid = true;
        }

        public void DestroyWithUndo()
        {
            if (invalid)
                return;
#if UNITY_EDITOR
            ChiselObjectUtility.SafeDestroyWithUndo(partialMesh);
			ChiselObjectUtility.SafeDestroyWithUndo(selectionMesh);
#endif
			ChiselObjectUtility.SafeDestroyWithUndo(sharedMesh);
            ChiselObjectUtility.SafeDestroyWithUndo(container, ignoreHierarchyEvents: true);
        }

        public void RemoveContainerFlags()
        {
            ChiselObjectUtility.RemoveContainerFlags(meshFilter);
            ChiselObjectUtility.RemoveContainerFlags(meshRenderer);
            ChiselObjectUtility.RemoveContainerFlags(container);
        }

        public bool IsValid()
        {
            if (!container  ||
                !sharedMesh ||
                !meshFilter ||
                !meshRenderer)
                return false;
            return true;
        }

        void Initialize()
        {
            meshFilter.sharedMesh = sharedMesh;
            if (!debugVisualizationRenderer)
            { 
                meshRenderer.receiveShadows	= ((query & SurfaceDestinationFlags.ShadowReceiving) == SurfaceDestinationFlags.ShadowReceiving);
                switch (query & (SurfaceDestinationFlags.Renderable | SurfaceDestinationFlags.ShadowCasting))
                {
                    case SurfaceDestinationFlags.None:				meshRenderer.enabled = false; break;
                    case SurfaceDestinationFlags.Renderable:		meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;			break;
                    case SurfaceDestinationFlags.ShadowCasting:		meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly;   break;
                    case SurfaceDestinationFlags.RenderShadowsCasting:	meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;			break;
                }

#if UNITY_EDITOR
                UnityEditor.EditorUtility.SetSelectedRenderState(meshRenderer, UnityEditor.EditorSelectedRenderState.Hidden);
				ChiselUnityUVGenerationManager.SetHasLightmapUVs(sharedMesh, false);
#endif
            } else
            {
                meshRenderer.allowOcclusionWhenDynamic = false;
                meshRenderer.lightProbeUsage = LightProbeUsage.Off;
                meshRenderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
                meshRenderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
                meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
#if UNITY_EDITOR
                meshRenderer.scaleInLightmap = 0.0f;
#endif
            }
        }

#if UNITY_EDITOR
		public static void CheckIfFullMeshNeedsToBeHidden(ChiselModelComponent model, ChiselRenderObjects renderable)
		{
			var shouldHideMesh = true;// (model.generated.visibilityState != VisibilityState.AllVisible && model.generated.visibilityState != VisibilityState.Unknown);
			if (renderable.meshRenderer.forceRenderingOff != shouldHideMesh)
				renderable.meshRenderer.forceRenderingOff = shouldHideMesh;
		}
#endif

		void UpdateSettings(ChiselModelComponent model, GameObjectState state, bool meshIsModified)
        {
#if UNITY_EDITOR
			Profiler.BeginSample("CheckIfFullMeshNeedsToBeHidden");
            // If we need to render partial meshes (where some brushes are hidden) then we shouldn't show the full mesh
            CheckIfFullMeshNeedsToBeHidden(model, this);
            Profiler.EndSample();

			var lightmapStatic = (state.staticFlags & UnityEditor.StaticEditorFlags.ContributeGI) == UnityEditor.StaticEditorFlags.ContributeGI;
			if (meshIsModified && lightmapStatic)
            {
                // Setting the sharedMesh to ensure the meshFilter knows it needs to be updated
                Profiler.BeginSample("OverrideMesh");
                meshFilter.sharedMesh = meshFilter.sharedMesh;
                Profiler.EndSample();

                Profiler.BeginSample("SetDirty");
                UnityEditor.EditorUtility.SetDirty(meshFilter);
                UnityEditor.EditorUtility.SetDirty(model);
                Profiler.EndSample();

                Profiler.BeginSample("SetHasLightmapUVs");
				ChiselUnityUVGenerationManager.SetHasLightmapUVs(sharedMesh, false);
                Profiler.EndSample();

                Profiler.BeginSample("ClearLightmapData");
				if (ChiselUnityUVGenerationManager.ClearLightmapData(state, this))
                {
					//Debug.Log($"ClearLightmapData for {container.name}", container);
				}
                Profiler.EndSample();
            }
#endif 
        }

        public static void UpdateProperties(ChiselModelComponent model, MeshRenderer[] meshRenderers)
        {
            if (meshRenderers == null || meshRenderers.Length == 0)
                return;
            
            var renderSettings = model.RenderSettings;
            Profiler.BeginSample("serializedObject");
#if UNITY_EDITOR
            if (renderSettings.serializedObjectFieldsDirty)
            {
                renderSettings.serializedObjectFieldsDirty = false;
				// These SerializedObject settings can *only* be modified in the inspector, 
				//      so we should only be calling this on creation / 
				//      when something in inspector changed.

				// Warning: calling new UnityEditor.SerializedObject with an empty array crashes Unity
				using var serializedObject = new UnityEditor.SerializedObject(meshRenderers);

				serializedObject.SetPropertyValue("m_ImportantGI",                    renderSettings.ImportantGI);
				serializedObject.SetPropertyValue("m_PreserveUVs",                    renderSettings.OptimizeUVs);
				serializedObject.SetPropertyValue("m_IgnoreNormalsForChartDetection", renderSettings.IgnoreNormalsForChartDetection);
				serializedObject.SetPropertyValue("m_AutoUVMaxDistance",              renderSettings.AutoUVMaxDistance);
				serializedObject.SetPropertyValue("m_AutoUVMaxAngle",                 renderSettings.AutoUVMaxAngle);
				serializedObject.SetPropertyValue("m_MinimumChartSize",               renderSettings.MinimumChartSize);
			}
            Profiler.EndSample();
#endif

            Profiler.BeginSample("meshRenderers");
            for (int i = 0; i < meshRenderers.Length; i++)
            {
                var meshRenderer = meshRenderers[i];
                var isRenderable = meshRenderer.shadowCastingMode != ShadowCastingMode.ShadowsOnly;
                meshRenderer.lightProbeProxyVolumeOverride	= !isRenderable ? null : renderSettings.lightProbeProxyVolumeOverride;
                meshRenderer.probeAnchor					= !isRenderable ? null : renderSettings.probeAnchor;
                meshRenderer.motionVectorGenerationMode		= !isRenderable ? MotionVectorGenerationMode.ForceNoMotion : renderSettings.motionVectorGenerationMode;
                meshRenderer.reflectionProbeUsage			= !isRenderable ? ReflectionProbeUsage.Off : renderSettings.reflectionProbeUsage;
                meshRenderer.lightProbeUsage				= !isRenderable ? LightProbeUsage.Off : renderSettings.lightProbeUsage;
                meshRenderer.allowOcclusionWhenDynamic		= renderSettings.allowOcclusionWhenDynamic;
                meshRenderer.renderingLayerMask				= renderSettings.renderingLayerMask;
#if UNITY_EDITOR
                meshRenderer.stitchLightmapSeams            = isRenderable && renderSettings.stitchLightmapSeams;
                meshRenderer.scaleInLightmap                = !isRenderable ? 0.0f : renderSettings.scaleInLightmap;
                meshRenderer.receiveGI                      = renderSettings.receiveGI;
#endif
            }
            Profiler.EndSample();
        }

#if false // not used?
        public void Clear(ChiselModelComponent model, GameObjectState gameObjectState)
        {
            bool meshIsModified = false;
            {
                Profiler.BeginSample("Clear");
                triangleBrushes.Clear();

                if (sharedMesh.vertexCount > 0)
                {
                    meshIsModified = true;
                    sharedMesh.Clear(keepVertexLayout: true);
                }
                Profiler.EndSample();

                Profiler.BeginSample("SetSharedMesh");
                if (meshFilter.sharedMesh != sharedMesh)
                {
                    meshFilter.sharedMesh = sharedMesh;
                    meshIsModified = true;
                }
                Profiler.EndSample();

                Profiler.BeginSample("SetMaterialsIfModified");
                renderMaterials = Array.Empty<Material>();
                SetMaterialsIfModified(meshRenderer, renderMaterials);
                Profiler.EndSample();

                Profiler.BeginSample("Enable");
                var expectedEnabled = sharedMesh.vertexCount > 0 && !debugVisualizationRenderer;
                if (meshRenderer.enabled != expectedEnabled)
                    meshRenderer.enabled = expectedEnabled;
                Profiler.EndSample();
            }

            Profiler.BeginSample("UpdateSettings");
            UpdateSettings(model, gameObjectState, meshIsModified);
            Profiler.EndSample();
        }
#endif
        
        public static void SetTriangleBrushes(List<ChiselMeshUpdate> meshUpdates, List<ChiselRenderObjectUpdate> objectUpdates, ref VertexBufferContents vertexBufferContents)
        {
            Profiler.BeginSample("SetTriangleBrushes");
            for (int u = 0; u < objectUpdates.Count; u++)
            {
                var meshUpdate   = meshUpdates[u];
                var objectUpdate = objectUpdates[u];
                var instance     = objectUpdate.instance;
                var subMeshTriangleLookups = vertexBufferContents.subMeshTriangleLookups[meshUpdate.contentsIndex];
                if (subMeshTriangleLookups.IsCreated)
                    subMeshTriangleLookups.Value.CopyTo(instance.triangleBrushes);
            }
            Profiler.EndSample();
        }

        public static void UpdateMaterials(List<ChiselMeshUpdate> meshUpdates, List<ChiselRenderObjectUpdate> objectUpdates, ref VertexBufferContents vertexBufferContents)
        {
            Profiler.BeginSample("UpdateMaterials");
            for (int u = 0; u < objectUpdates.Count; u++)
            {
                var meshUpdate          = meshUpdates[u];
                var objectUpdate        = objectUpdates[u];
                var instance            = objectUpdate.instance;
                var contentsIndex       = meshUpdate.contentsIndex;
                var materialOverride    = objectUpdate.materialOverride;
                var startIndex          = vertexBufferContents.subMeshSections[contentsIndex].startIndex;
                var endIndex            = vertexBufferContents.subMeshSections[contentsIndex].endIndex;
                var desiredCapacity     = Math.Max(1, endIndex - startIndex);
                if (instance.renderMaterials == null || instance.renderMaterials.Length != desiredCapacity)
                    instance.renderMaterials = new Material[desiredCapacity];
				if (materialOverride)
                {
                    for (int i = 0; i < instance.renderMaterials.Length; i++)
                    {
                        instance.renderMaterials[i] = materialOverride;
                    }
				} else
                {
                    for (int i = 0; i < desiredCapacity; i++)
                    {
                        var meshDescription = vertexBufferContents.meshDescriptions[startIndex + i];
                        var renderMaterial  = meshDescription.surfaceParameter == 0 ? null : Resources.InstanceIDToObject(meshDescription.surfaceParameter) as Material;
                        instance.renderMaterials[i] = renderMaterial;
                    }
                    instance.SetMaterialsIfModified(instance.meshRenderer, instance.renderMaterials);
                }
            }
            Profiler.EndSample();
        }


        public static void UpdateSettings(List<ChiselMeshUpdate> meshUpdates, List<ChiselRenderObjectUpdate> objectUpdates, Dictionary<ChiselModelComponent, GameObjectState> gameObjectStates, ref VertexBufferContents vertexBufferContents)
        {
            Profiler.BeginSample("UpdateSettings");
            for (int u = 0; u < objectUpdates.Count; u++)
            {
                var objectUpdate    = objectUpdates[u];
                var meshUpdate      = meshUpdates[u];
                var instance        = objectUpdate.instance;
                var contentsIndex   = meshUpdate.contentsIndex;
                var sharedMesh      = instance.sharedMesh;

                if (sharedMesh.subMeshCount > 0)
                {
                    var bounds = sharedMesh.GetSubMesh(0).bounds;
                    for (int s = 1; s < sharedMesh.subMeshCount; s++)
                        bounds.Encapsulate(sharedMesh.GetSubMesh(s).bounds);
                    sharedMesh.bounds = bounds;
                }

                if (instance.meshFilter.sharedMesh != sharedMesh)
                {
                    instance.meshFilter.sharedMesh = sharedMesh;
                    objectUpdate.meshIsModified = true;
                    objectUpdates[u] = objectUpdate;
                }
                
                var startIndex = vertexBufferContents.subMeshSections[contentsIndex].startIndex;
                var endIndex   = vertexBufferContents.subMeshSections[contentsIndex].endIndex;

				var meshDescription = vertexBufferContents.meshDescriptions[startIndex];
                var geometryHashValue = meshDescription.geometryHashValue;
				var surfaceHashValue  = meshDescription.surfaceHashValue;
				for (int m = startIndex + 1; m < endIndex; m++)
                {
                    meshDescription = vertexBufferContents.meshDescriptions[m];
					geometryHashValue = math.hash(new uint2(geometryHashValue, meshDescription.geometryHashValue));
					surfaceHashValue = math.hash(new uint2(surfaceHashValue, meshDescription.surfaceHashValue));
				}

				// TODO: why is geometryHashValue not stable
				objectUpdate.meshIsModified = (instance.geometryHashValue != geometryHashValue) ||
					                          (instance.surfaceHashValue != surfaceHashValue);
                if (objectUpdate.meshIsModified)
				{
					//Debug.Log($"instance startIndex {startIndex} endIndex {endIndex} instance {instance.container.name}");
					//Debug.Log($"instance.geometryHashValue {instance.geometryHashValue} => geometryHashValue {geometryHashValue}");
					//Debug.Log($"instance.surfaceHashValue {instance.surfaceHashValue} => surfaceHashValue {surfaceHashValue}");
				}
                instance.geometryHashValue = geometryHashValue;
				instance.surfaceHashValue = surfaceHashValue;

                  
				var gameObjectState = gameObjectStates[objectUpdate.model];
                var expectedEnabled = !instance.debugVisualizationRenderer &&
					vertexBufferContents.subMeshTriangleLookups[contentsIndex].IsCreated && 
					vertexBufferContents.subMeshTriangleLookups[contentsIndex].Value.perTriangleNodeIDLookup.Length > 0;
                if (instance.meshRenderer.enabled != expectedEnabled)
                    instance.meshRenderer.enabled = expectedEnabled;

                instance.UpdateSettings(objectUpdate.model, gameObjectState, objectUpdate.meshIsModified);
			}
            Profiler.EndSample();
        }


        public static void UpdateBounds(List<ChiselRenderObjectUpdate> objectUpdates)
        {
            Profiler.BeginSample("UpdateBounds");
            for (int u = 0; u < objectUpdates.Count; u++)
            {
                var objectUpdate    = objectUpdates[u];
                var instance        = objectUpdate.instance;
                var sharedMesh      = instance.sharedMesh;

                if (sharedMesh.subMeshCount > 0)
                {
                    var bounds = sharedMesh.GetSubMesh(0).bounds;
                    for (int s = 1; s < sharedMesh.subMeshCount; s++)
                        bounds.Encapsulate(sharedMesh.GetSubMesh(s).bounds);
                    sharedMesh.bounds = bounds;
                }
            }
            Profiler.EndSample();
        }

        
        private void SetMaterialsIfModified(MeshRenderer meshRenderer, Material[] renderMaterials)
        {
            var s_SharedMaterials = ListPool<Material>.Get();
            try
            {
                meshRenderer.GetSharedMaterials(s_SharedMaterials);
                if (s_SharedMaterials != null &&
                    s_SharedMaterials.Count == renderMaterials.Length)
                {
                    for (int i = 0; i < renderMaterials.Length; i++)
                    {
                        if (renderMaterials[i] != s_SharedMaterials[i])
                            goto SetMaterials;
                    }
                    return;
                }
            SetMaterials:
                meshRenderer.sharedMaterials = renderMaterials;
            }
            finally
            {
                ListPool<Material>.Release(s_SharedMaterials);
            }
        }

#if UNITY_EDITOR
        internal void UpdateVisibilityMesh(BrushVisibilityLookup visibilityLookup, bool showMesh)
        {
            // TODO: FIXME: we need to cache this, this is re-generated each frame and causes a lot of garbage!!

            EnsureMeshesAllocated();
            var srcMesh = sharedMesh;
            var dstMesh = partialMesh;

            if (!showMesh)
            {
                dstMesh.Clear(keepVertexLayout: true);
                return;
            }

            triangleBrushes.GenerateSubMesh(visibilityLookup, srcMesh, dstMesh);
		}


		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public SelectionDescription[] GetSelectionIndexDescriptionArray()
		{
			return triangleBrushes.selectionIndexDescriptions;
		}


		// TODO: improve on this, make this work well with debug modes

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool IsRendered()
        {
            return IsEnabled() && IsValidMesh(sharedMesh);
		}


		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool IsEnabled()
		{
			if (!Valid || !enabled)
				return false;

			if (debugVisualizationRenderer)
            {
                return meshRenderer && visible;
            } else
			{
                // TODO: get rid of needing meshRenderer?
				return meshRenderer && meshRenderer.enabled && meshRenderer.forceRenderingOff;
			}
		}


		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		internal bool IsValidMesh(Mesh mesh)
		{
			return mesh != null && mesh.vertexCount != 0 && mesh.subMeshCount != 0;
		}


		public void RenderScenePickingPass(int hashcode, HashSet<int> skipSelectionID, int offset)
		{
			// TODO: use commandbuffers instead?
			hashcode = HashCode.Combine(triangleBrushes.hashCode, hashcode, sharedMesh.GetHashCode());
			if (selectionMeshHashcode != hashcode || selectionMesh == null)
			{
				EnsureMeshesAllocated();
				triangleBrushes.GenerateSelectionSubMesh(skipSelectionID, sharedMesh, selectionMesh);
				selectionMeshHashcode = hashcode;
			}
            if (!IsValidMesh(selectionMesh))
			{
				return;
            }

			if (BrushPickingMaterial.SetScenePickingPass(offset))
			{
				Graphics.DrawMeshNow(selectionMesh, container.transform.localToWorldMatrix);
			}
		}


		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		void RenderMesh(Mesh mesh, MaterialPropertyBlock materialPropertyBlock, Matrix4x4 matrix, int layer, Camera camera, bool enableLightmaps)
		{
			enabled = false;
            if (!IsValidMesh(mesh))
			{
				return;
			}

			enabled = true;
			meshRenderer.GetPropertyBlock(materialPropertyBlock);

            var renderParams = new RenderParams
			{
                camera                   = camera,
                instanceID               = 0,
                layer                    = layer,
                lightProbeProxyVolume    = meshRenderer.lightProbeProxyVolumeOverride == null ? null : meshRenderer.lightProbeProxyVolumeOverride.GetComponent<LightProbeProxyVolume>(),
                lightProbeUsage          = meshRenderer.lightProbeUsage,
                matProps                 = materialPropertyBlock,
                motionVectorMode         = meshRenderer.motionVectorGenerationMode,
                overrideSceneCullingMask = false,
                receiveShadows           = meshRenderer.receiveShadows,
                reflectionProbeUsage     = meshRenderer.reflectionProbeUsage,
                rendererPriority         = meshRenderer.rendererPriority,
                renderingLayerMask       = meshRenderer.renderingLayerMask,
                sceneCullingMask         = 0,
                shadowCastingMode        = meshRenderer.shadowCastingMode,
                worldBounds              = meshRenderer.bounds
			};

            if (enableLightmaps)
            { 
                var lightmapScaleOffset = meshRenderer.lightmapScaleOffset;
                var lightmapIndex = meshRenderer.lightmapIndex;
                if (lightmapIndex != -1)
                {
                    var lightmapData = LightmapSettings.lightmaps[lightmapIndex];
                    materialPropertyBlock.SetTexture("unity_Lightmap", lightmapData.lightmapColor);
                    materialPropertyBlock.SetVector("unity_LightmapST", lightmapScaleOffset);
                } else
				    enableLightmaps = false;
                  
                // TODO: support all lightmaps functionality
			}

			// TODO: use commandbuffers instead?
			for (int submeshIndex = 0; submeshIndex < mesh.subMeshCount; submeshIndex++)
            {
                renderParams.material = renderMaterials[submeshIndex];
                if (enableLightmaps) renderParams.material.EnableKeyword("LIGHTMAP_ON");
                else renderParams.material.DisableKeyword("LIGHTMAP_ON");
                Graphics.RenderMesh(in renderParams, mesh, submeshIndex, matrix);
            }
		}


		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		internal static void RenderChiselRenderPartialObjects(ChiselRenderObjects[] renderables, MaterialPropertyBlock materialPropertyBlock, Matrix4x4 matrix, int layer, Camera camera, bool hasLightmaps)
		{
			// TODO: use commandbuffers instead?
			foreach (var renderable in renderables)
			{
				if (renderable == null)
					continue;
				if (!renderable.IsEnabled())
					continue;
				renderable.RenderMesh(renderable.partialMesh, materialPropertyBlock, matrix, layer, camera, hasLightmaps);
			}
		}


		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		internal static void RenderChiselRenderObjects(ChiselRenderObjects[] renderables, MaterialPropertyBlock materialPropertyBlock, Matrix4x4 matrix, int layer, Camera camera, bool hasLightmaps)
		{
			// TODO: use commandbuffers instead?
			foreach (var renderable in renderables)
			{
                if (renderable == null)
                    continue;
				if (!renderable.IsEnabled())
					continue;
				renderable.RenderMesh(renderable.sharedMesh, materialPropertyBlock, matrix, layer, camera, hasLightmaps);
			}
		}
        

		private static int GetInvisibleInstanceIds(BrushVisibilityLookup brushVisibilityLookup, ManagedSubMeshTriangleLookup.NeedToRenderForPicking needToRenderForPicking, SelectionDescription[] selectionToInstanceIDs, HashSet<int> skipSelectionID)
        {
            skipSelectionID.Clear();
            int hashcode = 0;
			for (int selectionID = 0; selectionID < selectionToInstanceIDs.Length; selectionID++)
            {
                var instanceID = selectionToInstanceIDs[selectionID].instanceID;

                GameObject instanceGameObject = null;
                if (brushVisibilityLookup.IsBrushVisible(instanceID))
                {
                    var instanceObj = Resources.InstanceIDToObject(instanceID);
                    if (instanceObj is MonoBehaviour component) instanceGameObject = component.gameObject;
                    else if (instanceObj is GameObject go) instanceGameObject = go;
                }

                if (!instanceGameObject || !needToRenderForPicking(instanceGameObject))
                {
                    skipSelectionID.Add(selectionID);
                    hashcode = HashCode.Combine(hashcode, selectionID);
                }
            }
            return hashcode;
        }

		public static int RenderPickingModels(ManagedSubMeshTriangleLookup.NeedToRenderForPicking needToRenderForPicking, int selectionIndexOffset, List<SelectionOffset> selectionOffsets)
		{
			var brushVisibilityLookup = ChiselUnityVisibilityManager.BrushVisibilityLookup;
            
            selectionOffsets.Clear();
            var instance = ChiselModelManager.Instance;
			foreach (var model in instance.Models)
			{
				var generated = model.generated;

				// TODO: remove gameobject/meshrenderer generation for our meshes
				//          -> although we do need this for lightmapping? and as a final bake?

				// TODO: remove mesh generation from CSG code, request on demand w/ hashcode for caching

				// TODO: combine generated.renderables & generated.debugVisualizationRenderables
				foreach (var renderObjects in generated.renderables)
				{
					if (renderObjects == null || !renderObjects.IsRendered())
						continue;

					var selectionIndexDescriptions = renderObjects.GetSelectionIndexDescriptionArray();
                    if (selectionIndexDescriptions.Length == 0)
                        continue;

					var skipSelectionID = HashSetPool<int>.Get();
					var hashcode = GetInvisibleInstanceIds(brushVisibilityLookup, needToRenderForPicking, selectionIndexDescriptions, skipSelectionID);
                                       
					selectionOffsets.Add(new SelectionOffset
					{
						offset = selectionIndexOffset,
						hashcode = hashcode,
						renderObjects = renderObjects,
						skipSelectionID = skipSelectionID,
						selectionIndexDescriptions = selectionIndexDescriptions
					});

					selectionIndexOffset += selectionIndexDescriptions.Length;
				}

				foreach (var renderObjects in generated.debugVisualizationRenderables)
				{
					if (renderObjects == null || !renderObjects.IsRendered())
						continue;

					var selectionIndexDescriptions = renderObjects.GetSelectionIndexDescriptionArray();
					if (selectionIndexDescriptions.Length == 0)
						continue;

					var skipSelectionID = HashSetPool<int>.Get();
					var hashcode = GetInvisibleInstanceIds(brushVisibilityLookup, needToRenderForPicking, selectionIndexDescriptions, skipSelectionID);
					
					selectionOffsets.Add(new SelectionOffset
					{
						offset = selectionIndexOffset,
						hashcode = hashcode,
						renderObjects = renderObjects,
						skipSelectionID = skipSelectionID,
						selectionIndexDescriptions = selectionIndexDescriptions
					});

					selectionIndexOffset += selectionIndexDescriptions.Length;
				}
			}

            foreach (var selectionOffset in selectionOffsets)
			{
				var renderObjects = selectionOffset.renderObjects;
				renderObjects.RenderScenePickingPass(selectionOffset.hashcode, selectionOffset.skipSelectionID, selectionOffset.offset);
				HashSetPool<int>.Release(selectionOffset.skipSelectionID);
			}
			return selectionIndexOffset;
		}



		static bool NeedToRenderForPicking(GameObject _) { return true; }

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void OnRenderModel(Camera camera, ChiselModelComponent model, DrawModeFlags drawModeFlags)
		{
			// When we toggle visibility on brushes in the editor hierarchy, we want to render a different mesh
			// but still have the same lightmap, and keep lightmap support.
			// We do this by setting forceRenderingOff to true on all MeshRenderers.
			// This makes them behave like before, except that they don't render. This means they are still 
			// part of things such as lightmap generation. At the same time we use Graphics.DrawMesh to
			// render the sub-mesh with the exact same settings as the MeshRenderer.
			model.materialPropertyBlock ??= new MaterialPropertyBlock();

			var layer = model.gameObject.layer;
			var matrix = model.transform.localToWorldMatrix;

            var staticFlags = UnityEditor.GameObjectUtility.GetStaticEditorFlags(model.gameObject);
			var lightmapStatic = (staticFlags & UnityEditor.StaticEditorFlags.ContributeGI) == UnityEditor.StaticEditorFlags.ContributeGI;

            if (drawModeFlags == DrawModeFlags.ShowPickingModel)
			{
				List<SelectionOffset> selectionOffsets = new();
				RenderPickingModels(NeedToRenderForPicking, 0, selectionOffsets);
				return;
			}

			if (model.VisibilityState != VisibilityState.Mixed)
			{
				if ((drawModeFlags & DrawModeFlags.HideRenderables) == DrawModeFlags.None)
					RenderChiselRenderObjects(model.generated.renderables, model.materialPropertyBlock, matrix, layer, camera, lightmapStatic);

				if ((drawModeFlags & ~DrawModeFlags.HideRenderables) != DrawModeFlags.None)
					RenderChiselRenderObjects(model.generated.debugVisualizationRenderables, model.materialPropertyBlock, matrix, layer, camera, false);
				return;
			}

			if ((drawModeFlags & DrawModeFlags.HideRenderables) == DrawModeFlags.None)
				RenderChiselRenderPartialObjects(model.generated.renderables, model.materialPropertyBlock, matrix, layer, camera, lightmapStatic);

			if ((drawModeFlags & ~DrawModeFlags.HideRenderables) != DrawModeFlags.None)
				RenderChiselRenderPartialObjects(model.generated.debugVisualizationRenderables, model.materialPropertyBlock, matrix, layer, camera, false);
		}
#endif
        }
}