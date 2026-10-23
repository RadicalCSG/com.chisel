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
        // the lightmap layout changed: its baked lightmap is no longer valid
        public bool                 lightmapIsModified;
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
        public bool HasGeometry => sharedMesh != null && sharedMesh.vertexCount > 0;

        public SurfaceDestinationFlags  query;
        // the material it draws (0: the shadow-only renderers, which draw all of theirs with one)
        public ulong            surfaceParameter;
        // the chunk of space it draws (Documentation~/Design/Chunking.md)
        public uint             chunk;
        // a debug visualization's mode (ChiselGeneratedObjects.kGeneratedVisualizationShowFlags); -1 when unknown
        public int              debugMode = -1;
        public GameObject       container;
        public Mesh             sharedMesh;
        public bool             enabled = true;
        // what ChiselGeneratedObjects.UpdateContainers last applied to the container
        [System.NonSerialized] public bool containerApplied;
        [System.NonSerialized] public bool containerInBakedLight;
#if UNITY_EDITOR
        public Mesh             partialMesh;
        public int              selectionMeshHashcode = ~0;
        [System.NonSerialized] public bool selectionMeshDirty = true;
        [System.NonSerialized] public bool partialMeshDirty = true;
        [System.NonSerialized] int  partialMeshVisibilityVersion = -1;
        [System.NonSerialized] bool partialMeshShown;
        // every brush of the mesh is visible: the scene view draws the mesh itself, not a partial copy of it
        [System.NonSerialized] bool partialIsShared;
        public Mesh DrawnMesh => partialIsShared ? sharedMesh : partialMesh;
        // every brush in its mesh is shown (UpdateVisibilityMesh)
        public bool AllBrushesShown => partialIsShared;
        public Mesh             selectionMesh;
        [NonSerialized, HideInInspector]
        public bool             visible;
#endif
        public MeshFilter       meshFilter;
        public MeshRenderer     meshRenderer;
        public Material[]       renderMaterials;

		public ManagedSubMeshTriangleLookup triangleBrushes = new();

		[SerializeField] internal ChiselGeneratorComponent[] selectionOwners        = Array.Empty<ChiselGeneratorComponent>();
		[SerializeField] internal int[]                      selectionOwnerIndices  = Array.Empty<int>();
		[SerializeField] internal int[]                      selectionBrushIndices  = Array.Empty<int>();
		[SerializeField] internal int[]                      selectionSurfaces      = Array.Empty<int>();
		[SerializeField] internal int[]                      selectionEntityIndices = Array.Empty<int>();
		[SerializeField] internal UnityEngine.Object[]       selectionEntities      = Array.Empty<UnityEngine.Object>();
		[NonSerialized] int selectionOwnersHash;
        
        public uint             geometryHashValue;
        public uint             surfaceHashValue;
        public uint             lightmapHashValue;

        public bool             debugVisualizationRenderer;

        internal ChiselRenderObjects() { }
        public static ChiselRenderObjects Create(string name, Transform parent, GameObjectState state, SurfaceDestinationFlags query, bool debugVisualizationRenderer = false)
        {
            var renderContainer = ChiselObjectUtility.CreateGameObject(name, parent, state, debugVisualizationRenderer: debugVisualizationRenderer);
            var meshFilter      = renderContainer.AddComponent<MeshFilter>();
            var meshRenderer    = renderContainer.AddComponent<MeshRenderer>();
            meshRenderer.enabled = false;
            meshRenderer.staticShadowCaster = true; // TODO: this should depend on the model being occluder static, 
                                                    // we have the information here but we also need to update it when the model's static flags change

			var renderObjects = new ChiselRenderObjects
            {
                invalid             = false,     
#if UNITY_EDITOR
                visible             = !debugVisualizationRenderer,
#endif
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


#if UNITY_EDITOR
        const HideFlags kUnsavedMeshFlags = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild;
#endif

        void EnsureMeshesAllocated()
        {
            if (sharedMesh == null) sharedMesh = new Mesh { name = meshFilter.gameObject.name };
#if UNITY_EDITOR
            if (partialMesh == null)
            {
				partialMesh = new Mesh
				{
					name = meshFilter.gameObject.name,
					hideFlags = kUnsavedMeshFlags
				};
				partialMeshDirty = true;
			}
			if (selectionMesh == null)
			{
				selectionMesh = new Mesh
				{
					name = meshFilter.gameObject.name,
					hideFlags = kUnsavedMeshFlags
				};
				// Deliberately the inverse of the real key (see RenderScenePickingPass) so a freshly
				// allocated selection mesh never matches and is always generated once.
				selectionMeshHashcode = ~HashCode.Combine(triangleBrushes?.hashCode ?? 0, 0, sharedMesh.GetHashCode(),
														  geometryHashValue, surfaceHashValue);
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
                    case SurfaceDestinationFlags.None:				    meshRenderer.enabled = false; break;
                    case SurfaceDestinationFlags.Renderable:		    meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;			break;
                    case SurfaceDestinationFlags.ShadowCasting:		    meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;			break;
                    case SurfaceDestinationFlags.RenderShadowsCasting:	meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;			break;
                }

#if UNITY_EDITOR
                UnityEditor.EditorUtility.SetSelectedRenderState(meshRenderer, UnityEditor.EditorSelectedRenderState.Hidden);
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

        /// <summary>
        /// Puts the model's shadow settings on top of what this renderer's query allows. Without shadows, a renderer that
        /// draws and casts only draws, and one that only casts draws nothing. The shadow-only renderer casts with shadows
        /// On, because URP casts no shadows from ShadowsOnly (see <see cref="Initialize"/>).
        /// </summary>
        internal void ApplyShadowSettings(ChiselGeneratedRenderSettings renderSettings)
        {
            if (debugVisualizationRenderer || !meshRenderer)
                return;

            var flags = query & SurfaceDestinationFlags.RenderShadowsCasting;
            if (renderSettings != null && !renderSettings.castShadows)
                flags &= ~SurfaceDestinationFlags.ShadowCasting;
            var shadowCastingMode = flags switch
            {
                SurfaceDestinationFlags.RenderShadowsCasting => ShadowCastingMode.On,
                SurfaceDestinationFlags.ShadowCasting        => ShadowCastingMode.On,
                _                                            => ShadowCastingMode.Off,
            };
            if (meshRenderer.shadowCastingMode != shadowCastingMode)
                meshRenderer.shadowCastingMode = shadowCastingMode;

            var receiveShadows = (renderSettings == null || renderSettings.receiveShadows) &&
                                 (query & SurfaceDestinationFlags.ShadowReceiving) == SurfaceDestinationFlags.ShadowReceiving;
            if (meshRenderer.receiveShadows != receiveShadows)
                meshRenderer.receiveShadows = receiveShadows;

            if (flags == SurfaceDestinationFlags.None && meshRenderer.enabled)
                meshRenderer.enabled = false;
        }

#if UNITY_EDITOR
		public static void CheckIfFullMeshNeedsToBeHidden(ChiselModelComponent model, ChiselRenderObjects renderable)
		{
			var shouldHideMesh = true;// (model.generated.visibilityState != VisibilityState.AllVisible && model.generated.visibilityState != VisibilityState.Unknown);
			if (renderable.meshRenderer.forceRenderingOff != shouldHideMesh)
				renderable.meshRenderer.forceRenderingOff = shouldHideMesh;
		}
#endif

		void UpdateSettings(ChiselModelComponent model, GameObjectState state, bool lightmapIsModified)
        {
            ApplyShadowSettings(model.RenderSettings);
#if UNITY_EDITOR
			Profiler.BeginSample("CheckIfFullMeshNeedsToBeHidden");
            // If we need to render partial meshes (where some brushes are hidden) then we shouldn't show the full mesh
            CheckIfFullMeshNeedsToBeHidden(model, this);
            Profiler.EndSample();

			// A lightmapped model's lightmap no longer fits a changed mesh; a model lit by light probes has none
			if (lightmapIsModified && ChiselLightmapUVManager.IsLightmapped(model, state.staticFlags))
            {
                // Setting the sharedMesh to ensure the meshFilter knows it needs to be updated
                Profiler.BeginSample("OverrideMesh");
                meshFilter.sharedMesh = meshFilter.sharedMesh;
                Profiler.EndSample();

                Profiler.BeginSample("SetDirty");
                UnityEditor.EditorUtility.SetDirty(meshFilter);
                UnityEditor.EditorUtility.SetDirty(model);
                Profiler.EndSample();

                Profiler.BeginSample("ClearLightmapData");
				if (ChiselLightmapUVManager.ClearLightmapData(model, state, this))
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
            var forceShadowOnlyMaterial = ChiselProjectSettings.ForceShadowOnlySurfacesMaterial;
            for (int i = 0; i < meshRenderers.Length; i++)
                ApplyProperties(meshRenderers[i], renderSettings, forceShadowOnlyMaterial);
            Profiler.EndSample();
        }

        // The settings UpdateProperties applies, to tell whether they changed since it last ran
        public static (GameObject, Transform, MotionVectorGenerationMode, ReflectionProbeUsage, LightProbeUsage, bool, uint, ReceiveGI, bool, float, Material)
            PropertiesOf(ChiselModelComponent model)
        {
            var s = model.RenderSettings;
            return (s.lightProbeProxyVolumeOverride, s.probeAnchor, s.motionVectorGenerationMode, s.reflectionProbeUsage, s.lightProbeUsage,
                    s.allowOcclusionWhenDynamic, s.renderingLayerMask, s.receiveGI, s.stitchLightmapSeams, s.scaleInLightmap,
                    ChiselProjectSettings.ForceShadowOnlySurfacesMaterial);
        }

        /// <summary>UpdateProperties for the renderers of these updates only, when the settings are the same</summary>
        public static void UpdateProperties(ChiselModelComponent model, List<ChiselRenderObjectUpdate> objectUpdates)
        {
            var renderSettings = model.RenderSettings;
            var forceShadowOnlyMaterial = ChiselProjectSettings.ForceShadowOnlySurfacesMaterial;
            for (int u = 0; u < objectUpdates.Count; u++)
            {
                var instance = objectUpdates[u].instance;
                if (instance == null || instance.debugVisualizationRenderer || !instance.meshRenderer)
                    continue;
                ApplyProperties(instance.meshRenderer, renderSettings, forceShadowOnlyMaterial);
            }
        }

        static void ApplyProperties(MeshRenderer meshRenderer, ChiselGeneratedRenderSettings renderSettings, Material forceShadowOnlyMaterial)
        {
            {
                // The shadow-only renderer draws with ForceShadowOnly, and with shadows On (see Initialize)
                var isRenderable = meshRenderer.shadowCastingMode != ShadowCastingMode.ShadowsOnly &&
                                   (!forceShadowOnlyMaterial || meshRenderer.sharedMaterial != forceShadowOnlyMaterial);
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
                {
                    subMeshTriangleLookups.Value.CopyTo(instance.triangleBrushes);
                    instance.SelectionOwnersChanged();
                }
#if UNITY_EDITOR
                instance.selectionMeshDirty = true;
                instance.partialMeshDirty = true;
#endif
            }
            Profiler.EndSample();
        }

#if UNITY_EDITOR
        // The owners are only read once the scene is loaded again, so an edit leaves them for later: they are stored
        // before anything is saved (ChiselModelManager.StoreInputHashes) or reloaded, or once the edits stop. Weakly
        // held, so a destroyed model's renderables don't stay loaded.
        static readonly List<WeakReference<ChiselRenderObjects>> s_PendingOwners = new();
        static double s_LastOwnersChange;
        const double kOwnersIdleSeconds = 0.5;
        [NonSerialized] bool selectionOwnersPending;

        void SelectionOwnersChanged()
        {
            s_LastOwnersChange = UnityEditor.EditorApplication.timeSinceStartup;
            if (selectionOwnersPending)
                return;
            selectionOwnersPending = true;
            s_PendingOwners.Add(new WeakReference<ChiselRenderObjects>(this));
        }

        public static void StorePendingSelectionOwners()
        {
            if (s_PendingOwners.Count == 0)
                return;
            var pending = s_PendingOwners.ToArray();
            s_PendingOwners.Clear();
            foreach (var reference in pending)
            {
                if (reference.TryGetTarget(out var renderable))
                    renderable.StorePendingOwners();
            }
        }

        void StorePendingOwners()
        {
            if (!selectionOwnersPending)
                return;
            selectionOwnersPending = false;
            if (!invalid && container)
                StoreSelectionOwners();
        }

        [UnityEditor.InitializeOnLoadMethod]
        static void RegisterPendingOwners()
        {
            UnityEditor.SceneManagement.PrefabStage.prefabSaving -= OnPrefabSaving;
            UnityEditor.SceneManagement.PrefabStage.prefabSaving += OnPrefabSaving;
            UnityEditor.AssemblyReloadEvents.beforeAssemblyReload -= StorePendingSelectionOwners;
            UnityEditor.AssemblyReloadEvents.beforeAssemblyReload += StorePendingSelectionOwners;
            UnityEditor.EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            UnityEditor.EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            UnityEditor.EditorApplication.update -= OnUpdate;
            UnityEditor.EditorApplication.update += OnUpdate;
        }

        static void OnPrefabSaving(GameObject prefab) => StorePendingSelectionOwners();
        static void OnPlayModeStateChanged(UnityEditor.PlayModeStateChange state)
        {
            if (state == UnityEditor.PlayModeStateChange.ExitingEditMode)
                StorePendingSelectionOwners();
        }

        static void OnUpdate()
        {
            if (s_PendingOwners.Count > 0 &&
                UnityEditor.EditorApplication.timeSinceStartup - s_LastOwnersChange > kOwnersIdleSeconds)
                StorePendingSelectionOwners();
        }
#else
        void SelectionOwnersChanged() => StoreSelectionOwners();
#endif

        // Only when the selection ids changed: an edit that doesn't add, remove or rebuild a brush keeps them (the owners
        // follow from the descriptions alone, not from the triangles)
        void StoreSelectionOwners()
        {
            var descriptions = triangleBrushes.selectionIndexDescriptions;
            var hash = DescriptionsHash(descriptions);
            if (selectionOwnersHash == hash &&
                selectionOwnerIndices.Length == descriptions.Length)
                return;
            selectionOwnersHash = hash;

            var count         = descriptions.Length;
            var ownerIndices  = new int[count];
            var brushIndices  = new int[count];
            var surfaces      = new int[count];
            var owners        = new List<ChiselGeneratorComponent>();
            var indexOfOwner  = new Dictionary<ChiselGeneratorComponent, int>();
            var entityIndices = new List<int>();
            var entities      = new List<UnityEngine.Object>();
            var brushOrders   = new Dictionary<ChiselGeneratorComponent, Dictionary<CompactNodeID, int>>();
            var ownerOfEntity = new Dictionary<ulong, ChiselGeneratorComponent>();
            // a brush's surfaces have one description each: each brush is looked up once
            var known         = new Dictionary<CompactNodeID, (ulong entity, int owner, int brush)>();
            for (int s = 0; s < count; s++)
            {
                var description = descriptions[s];
                if (!known.TryGetValue(description.brushNodeID, out var brushInfo))
                {
                    var entity = BrushEntityID(description.brushNodeID);
                    if (!ownerOfEntity.TryGetValue(entity, out var owner))
                    {
                        owner = Resources.EntityIdToObject(UnityEngine.EntityId.FromULong(entity)) as ChiselGeneratorComponent;
                        ownerOfEntity.Add(entity, owner);
                    }
                    brushInfo = (entity, -1, -1);
                    if (owner != null)
                    {
                        if (!indexOfOwner.TryGetValue(owner, out var ownerIndex))
                        {
                            ownerIndex = owners.Count;
                            owners.Add(owner);
                            indexOfOwner.Add(owner, ownerIndex);
                        }
                        brushInfo = (entity, ownerIndex, BrushIndexIn(owner, description.brushNodeID, brushOrders));
                    }
                    known.Add(description.brushNodeID, brushInfo);
                }
                var brushEntity = brushInfo.entity;
                ownerIndices[s] = brushInfo.owner;
                brushIndices[s] = brushInfo.owner < 0 ? -1 : brushInfo.brush;
                surfaces[s] = description.surfaceIndex;
                if (description.entityID != brushEntity)
                {
                    entityIndices.Add(s);
                    entities.Add(Resources.EntityIdToObject(UnityEngine.EntityId.FromULong(description.entityID)));
                }
            }
            selectionOwners        = owners.ToArray();
            selectionOwnerIndices  = ownerIndices;
            selectionBrushIndices  = brushIndices;
            selectionSurfaces      = surfaces;
            selectionEntityIndices = entityIndices.ToArray();
            selectionEntities      = entities.ToArray();
        }

        // Where a brush is among the brushes its generator makes (-1 when it is not one of them)
        static int BrushIndexIn(ChiselGeneratorComponent generator, CompactNodeID brush, Dictionary<ChiselGeneratorComponent, Dictionary<CompactNodeID, int>> brushOrders)
        {
            // most generators make one brush
            var top = generator.TopTreeNode;
            if (top.Valid && top.Type == CSGNodeType.Brush)
                return CompactHierarchyManager.GetCompactNodeID(top) == brush ? 0 : -1;
            if (!brushOrders.TryGetValue(generator, out var indices))
            {
                var brushes = BrushesOf(generator, new Dictionary<ChiselGeneratorComponent, List<CompactNodeID>>());
                indices = new Dictionary<CompactNodeID, int>(brushes.Count);
                for (int i = 0; i < brushes.Count; i++)
                    indices.TryAdd(brushes[i], i);
                brushOrders.Add(generator, indices);
            }
            return indices.TryGetValue(brush, out var index) ? index : -1;
        }

        static int DescriptionsHash(SelectionDescription[] descriptions)
        {
            if (descriptions == null)
                return 0;
            var hash = 0u;
            for (int i = 0; i < descriptions.Length; i++)
            {
                var d = descriptions[i];
                hash = math.hash(new uint4(hash, (uint)d.brushNodeID.GetHashCode(), (uint)d.surfaceIndex, (uint)d.entityID ^ (uint)(d.entityID >> 32)));
            }
            return (int)hash;
        }

        // The brushes a generator makes, in the order RestoreSelection finds them in again
        static List<CompactNodeID> BrushesOf(ChiselGeneratorComponent generator, Dictionary<ChiselGeneratorComponent, List<CompactNodeID>> brushOrders)
        {
            if (brushOrders.TryGetValue(generator, out var brushes))
                return brushes;
            brushes = new List<CompactNodeID>();
            var nodes = new Stack<CSGTreeNode>();
            nodes.Push(generator.TopTreeNode);
            while (nodes.Count > 0)
            {
                var node = nodes.Pop();
                if (!node.Valid)
                    continue;
                if (node.Type == CSGNodeType.Brush)
                {
                    brushes.Add(CompactHierarchyManager.GetCompactNodeID(node));
                    continue;
                }
                for (int i = node.Count - 1; i >= 0; i--)
                    nodes.Push(node[i]);
            }
            brushOrders.Add(generator, brushes);
            return brushes;
        }

        static ulong BrushEntityID(CompactNodeID brushNodeID)
        {
            if (brushNodeID == CompactNodeID.Invalid || !CompactHierarchyManager.IsValidCompactNodeID(brushNodeID))
                return 0;
            ref var hierarchy = ref CompactHierarchyManager.GetHierarchy(brushNodeID);
            if (!hierarchy.IsValidCompactNodeID(brushNodeID))
                return 0;
            return hierarchy.GetChildRef(brushNodeID).entityID;
        }

        public SelectionDescription DescriptionForPicking(ChiselModelComponent model, int index)
        {
            var descriptions = GetSelectionIndexDescriptionArray();
            if (index < 0 || index >= descriptions.Length)
                return default;
            if (descriptions[index].brushNodeID != CompactNodeID.Invalid)
                return descriptions[index];

            ChiselModelManager.BuildTheNodesOfNow(model);
            RestoreSelection();

            descriptions = GetSelectionIndexDescriptionArray();
            return (index < descriptions.Length) ? descriptions[index] : default;
        }

        // What RestoreSelection and the CSG leave behind for picking: runtime data, so players have it too
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public SelectionDescription[] GetSelectionIndexDescriptionArray()
        {
            return triangleBrushes.selectionIndexDescriptions;
        }

        internal void RestoreSelection()
        {
            if (invalid || !sharedMesh)
                return;
#if UNITY_EDITOR
            StorePendingOwners();
#endif

            var count        = Math.Min(Math.Min(selectionOwnerIndices.Length, selectionBrushIndices.Length), selectionSurfaces.Length);
            var descriptions = new SelectionDescription[count];
            var brushOrders  = new Dictionary<ChiselGeneratorComponent, List<CompactNodeID>>();
            for (int s = 0; s < count; s++)
            {
                var ownerIndex  = selectionOwnerIndices[s];
                var owner       = (ownerIndex >= 0 && ownerIndex < selectionOwners.Length) ? selectionOwners[ownerIndex] : null;
                var brushIndex  = selectionBrushIndices[s];
                var brushNodeID = CompactNodeID.Invalid;
                if (owner && brushIndex >= 0)
                {
                    var brushes = BrushesOf(owner, brushOrders);
                    if (brushIndex < brushes.Count)
                        brushNodeID = brushes[brushIndex];
                }
                descriptions[s] = new SelectionDescription
                {
                    entityID     = owner ? UnityEngine.EntityId.ToULong(owner.GetEntityId()) : 0,
                    surfaceIndex = selectionSurfaces[s],
                    brushNodeID  = brushNodeID
                };
            }

            // The ids that select something else than their brush's generator: a decal
            for (int e = 0; e < Math.Min(selectionEntityIndices.Length, selectionEntities.Length); e++)
            {
                var s      = selectionEntityIndices[e];
                var entity = selectionEntities[e];
                if (s < 0 || s >= descriptions.Length || !entity)
                    continue;
                descriptions[s].entityID = UnityEngine.EntityId.ToULong(entity.GetEntityId());
            }

            // The per-triangle arrays only ever grow, so they can be longer than the mesh has triangles
            var selectionIDs  = triangleBrushes.perTriangleSelectionIDLookup;
            var triangleCount = 0;
            for (int subMesh = 0; subMesh < sharedMesh.subMeshCount; subMesh++)
                triangleCount += (int)sharedMesh.GetIndexCount(subMesh) / 3;
            triangleCount = Math.Min(triangleCount, selectionIDs.Length);

            var brushNodeIDs = new CompactNodeID[triangleCount];
            for (int n = 0; n < triangleCount; n++)
            {
                var selectionID = selectionIDs[n];
                brushNodeIDs[n] = (selectionID >= 0 && selectionID < descriptions.Length) ? descriptions[selectionID].brushNodeID : CompactNodeID.Invalid;
            }

            triangleBrushes.selectionIndexDescriptions = descriptions;
            triangleBrushes.perTriangleNodeIDLookup    = brushNodeIDs;
            triangleBrushes.validTriangleCount         = triangleCount;
            selectionOwnersHash = DescriptionsHash(descriptions);
#if UNITY_EDITOR
            selectionMeshDirty  = true;
            partialMeshDirty    = true;
#endif
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
                    // The debug visualization renderers are only drawn by the editor, from renderMaterials. The shadow-only
                    // renderer's MeshRenderer draws with its override too.
                    if (!instance.debugVisualizationRenderer)
                        instance.SetMaterialsIfModified(instance.meshRenderer, instance.renderMaterials);
				} else
                {
                    for (int i = 0; i < desiredCapacity; i++)
                    {
                        var meshDescription = vertexBufferContents.meshDescriptions[startIndex + i];
                        var renderMaterial  = meshDescription.surfaceParameter == 0 ? null : Resources.EntityIdToObject(UnityEngine.EntityId.FromULong(meshDescription.surfaceParameter)) as Material;
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
                // a hotspot change redraws the mesh without moving its lightmap
                objectUpdate.lightmapIsModified = (instance.lightmapHashValue != meshUpdate.lightmapHashValue) ||
                                                  (objectUpdate.meshIsModified && meshUpdate.lightmapHashValue == 0);
                instance.lightmapHashValue = meshUpdate.lightmapHashValue;
#if UNITY_EDITOR
                // Geometry re-uploaded => the selection and partial meshes mirroring it must be rebuilt too.
                if (objectUpdate.meshIsModified)
                {
                    instance.selectionMeshDirty = true;
                    instance.partialMeshDirty = true;
                }
#endif

                  
				var gameObjectState = gameObjectStates[objectUpdate.model];
                var expectedEnabled = !instance.debugVisualizationRenderer &&
					vertexBufferContents.subMeshTriangleLookups[contentsIndex].IsCreated && 
					vertexBufferContents.subMeshTriangleLookups[contentsIndex].Value.triangleCount > 0;
                if (instance.meshRenderer.enabled != expectedEnabled)
                    instance.meshRenderer.enabled = expectedEnabled;

                instance.UpdateSettings(objectUpdate.model, gameObjectState, objectUpdate.lightmapIsModified);
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
            EnsureMeshesAllocated();
            if (!partialMeshDirty &&
                partialMeshShown == showMesh &&
                partialMeshVisibilityVersion == visibilityLookup.Version)
                return;
            partialMeshDirty             = false;
            partialMeshShown             = showMesh;
            partialMeshVisibilityVersion = visibilityLookup.Version;

            var srcMesh = sharedMesh;
            var dstMesh = partialMesh;

            partialIsShared = false;
            if (!showMesh)
            {
                dstMesh.Clear(keepVertexLayout: true);
                return;
            }

            if (AllBrushesVisible(visibilityLookup))
            {
                partialIsShared = true;
                if (dstMesh.vertexCount > 0)
                    dstMesh.Clear(keepVertexLayout: true);
                return;
            }

            triangleBrushes.GenerateSubMesh(visibilityLookup, srcMesh, dstMesh);
		}

        bool AllBrushesVisible(BrushVisibilityLookup visibilityLookup)
        {
            return visibilityLookup.AllBrushesVisible(triangleBrushes.selectionIndexDescriptions);
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
				return meshRenderer && meshRenderer.enabled;
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
			hashcode = HashCode.Combine(triangleBrushes.hashCode, hashcode, sharedMesh.GetHashCode(),
										geometryHashValue, surfaceHashValue);
			if (selectionMeshDirty || selectionMeshHashcode != hashcode || selectionMesh == null)
			{
				EnsureMeshesAllocated();
				triangleBrushes.GenerateSelectionSubMesh(skipSelectionID, sharedMesh, selectionMesh);
				selectionMeshHashcode = hashcode;
				selectionMeshDirty = false;
			}
            if (!IsValidMesh(selectionMesh))
			{
				return;
            }

			if (BrushPickingMaterial.SetScenePickingPass(offset))
			{
				var matrix = container.transform.localToWorldMatrix;
				for (int subMesh = 0; subMesh < selectionMesh.subMeshCount; subMesh++)
					Graphics.DrawMeshNow(selectionMesh, matrix, subMesh);
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
                entityId                 = default,
                layer                    = layer,
                lightProbeUsage          = meshRenderer.lightProbeUsage,
                matProps                 = materialPropertyBlock,
                motionVectorMode         = meshRenderer.motionVectorGenerationMode,
                overrideSceneCullingMask = true,
                receiveShadows           = meshRenderer.receiveShadows,
                reflectionProbeUsage     = meshRenderer.reflectionProbeUsage,
                rendererPriority         = meshRenderer.rendererPriority,
                renderingLayerMask       = meshRenderer.renderingLayerMask,
                sceneCullingMask         = meshRenderer.gameObject.sceneCullingMask,
                shadowCastingMode        = meshRenderer.shadowCastingMode,
                worldBounds              = meshRenderer.bounds
			};

            if (enableLightmaps)
            { 
                var lightmapScaleOffset = meshRenderer.lightmapScaleOffset;
                // Only an index into the baked lightmaps has one: it is -1 before a bake, 0xFFFE for a renderer the bake
                // took into account without giving it a lightmap, and whatever it was while the lightmaps are gone
                var lightmapIndex = meshRenderer.lightmapIndex;
                var lightmaps = LightmapSettings.lightmaps;
                if (lightmapIndex >= 0 && lightmapIndex < lightmaps.Length)
                {
                    var lightmapData = lightmaps[lightmapIndex];
                    materialPropertyBlock.SetTexture("unity_Lightmap", lightmapData.lightmapColor);
                    materialPropertyBlock.SetVector("unity_LightmapST", lightmapScaleOffset);
                } else
				    enableLightmaps = false;
                  
                // TODO: support all lightmaps functionality
			}

			// TODO: use commandbuffers instead?
			var materialCount = (renderMaterials == null) ? 0 : renderMaterials.Length;
			for (int submeshIndex = 0; submeshIndex < mesh.subMeshCount; submeshIndex++)
            {
                var material = (submeshIndex < materialCount) ? renderMaterials[submeshIndex] : null;
                if (material == null)
                    continue;

                renderParams.material = material;
                if (enableLightmaps) material.EnableKeyword("LIGHTMAP_ON");
                else material.DisableKeyword("LIGHTMAP_ON");
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
				if (!renderable.IsEnabled() || DrawsItself(renderable))
					continue;
				renderable.RenderMesh(renderable.DrawnMesh, materialPropertyBlock, matrix, layer, camera, hasLightmaps);
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
				if (!renderable.IsEnabled() || DrawsItself(renderable))
					continue;
				renderable.RenderMesh(renderable.sharedMesh, materialPropertyBlock, matrix, layer, camera, hasLightmaps);
			}
		}

		// Unity draws it (UpdateDebugVisualizationState left its renderer on); a debug visualization is only ever drawn here
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		static bool DrawsItself(ChiselRenderObjects renderable)
		{
			return !renderable.debugVisualizationRenderer && !renderable.meshRenderer.forceRenderingOff;
		}
        

		private static int GetInvisibleEntityIDs(BrushVisibilityLookup brushVisibilityLookup, ManagedSubMeshTriangleLookup.NeedToRenderForPicking needToRenderForPicking, SelectionDescription[] selectionToEntityIDs, HashSet<int> skipSelectionID)
        {
            skipSelectionID.Clear();
            int hashcode = 0;
			for (int selectionID = 0; selectionID < selectionToEntityIDs.Length; selectionID++)
            {
                var entityID = selectionToEntityIDs[selectionID].entityID;

                GameObject instanceGameObject = null;
                if (!brushVisibilityLookup.IsBrushHidden(entityID))
                {
                    var instanceObj = Resources.EntityIdToObject(UnityEngine.EntityId.FromULong(entityID));
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
				// a model can be destroyed a while before it is unregistered
				if (!model)
					continue;

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
					var hashcode = GetInvisibleEntityIDs(brushVisibilityLookup, needToRenderForPicking, selectionIndexDescriptions, skipSelectionID);
                                       
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
					var hashcode = GetInvisibleEntityIDs(brushVisibilityLookup, needToRenderForPicking, selectionIndexDescriptions, skipSelectionID);
					
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
			// A model that contributes to global illumination can take its own from light probes, and then it has no lightmap
			var lightmapStatic = ChiselLightmapUVManager.IsLightmapped(model, staticFlags);

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