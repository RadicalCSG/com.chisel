using UnityEngine;
using UnityEditor;

using System;
using System.Linq;
using Chisel.Core;
using Chisel.Components;
using UnityEngine.Rendering;
using System.Collections.Generic;
using UnityEngine.Profiling;
using UnityEngine.Pool;

namespace Chisel.Editors
{
    public sealed class ChiselModelDetails : ChiselNodeDetails<ChiselModelComponent>
    {
        const string kModelIconName = "csg_model";

        public override GUIContent GetHierarchyIcon(ChiselModelComponent node)
        {
            return ChiselEditorResources.GetIconContent(kModelIconName, node.ChiselNodeTypeName)[0];
        }
    }

    [CustomEditor(typeof(ChiselModelComponent))]
    public sealed class ChiselModelEditor : ChiselNodeEditor<ChiselModelComponent>
    {
        [MenuItem(kGameObjectMenuModelPath + ChiselModelComponent.kNodeTypeName, false, kGameObjectMenuModelPriority)]
        internal static void CreateAsGameObject(MenuCommand menuCommand) { CreateAsGameObjectMenuCommand(menuCommand, ChiselModelComponent.kNodeTypeName); }


        [ContextMenu("Set Active Model", false)]
        internal static void SetActiveModel(MenuCommand menuCommand)
        {
            var model = (menuCommand.context as GameObject).GetComponent<ChiselModelComponent>();
            if (model)
                ChiselModelManager.Instance.ActiveModel = model;
        }

        [ContextMenu("Set Active Model", true)]
        internal static bool ValidateActiveModel(MenuCommand menuCommand)
        {
            var model = (menuCommand.context as GameObject).GetComponent<ChiselModelComponent>();
            return model;
        }

        readonly static GUIContent kLightingContent                        = new("Lighting");
        readonly static GUIContent kProbesContent                          = new("Probes");
        readonly static GUIContent kAdditionalSettingsContent              = new("Additional Settings");
        readonly static GUIContent kGenerationSettingsContent              = new("Geometry Output");
        readonly static GUIContent kColliderSettingsContent                = new("Collider");
        readonly static GUIContent kDebugContent                           = new("Debug");
        readonly static GUIContent kCreateRenderComponentsContents         = new("Renderable");
        readonly static GUIContent kCreateColliderComponentsContents       = new("Collidable");
        readonly static GUIContent kSubtractiveEditingContents            = new("Subtractive Editing", "New brushes are created as subtractive when enabled");
        readonly static GUIContent kSmoothNormalsContents                 = new("Smooth Normals");
        readonly static GUIContent kSmoothingAngleContents                = new("Smoothing Angle");
        readonly static GUIContent kDebugLogBrushesContents              = new("Debug Log Brushes");
        readonly static GUIContent kDebugLogOutputContents               = new("Debug Log Output");
        readonly static GUIContent kUnwrapParamsContents                   = new("UV Generation");

        readonly static GUIContent kForceBuildUVsContents                  = new("Build", "Manually build lightmap UVs for generated meshes. This operation can be slow for more complicated meshes");
        readonly static GUIContent kForceRebuildUVsContents                = new("Rebuild", "Manually rebuild lightmap UVs for generated meshes. This operation can be slow for more complicated meshes");
        readonly static GUIContent kAutoRebuildUVsContents                 = new("Auto UV Generation", "Automatically lightmap UVs for generated meshes. This operation can be slow for more complicated meshes");
        readonly static GUIContent kNeedsLightmapBuildContents             = new("In order for lightmapping to work properly the lightmap UVs need to be build.");
        readonly static GUIContent kNeedsLightmapRebuildContents           = new("In order for lightmapping to work properly the lightmap UVs need to be rebuild.");

        readonly static GUIContent kMotionVectorsContent                   = new("Motion Vectors", "Specifies whether the Model renders 'Per Object Motion', 'Camera Motion', or 'No Motion' vectors to the Camera Motion Vector Texture.");
        readonly static GUIContent kLightmappingContents                   = new("Lightmapping");
        readonly static GUIContent kGINotEnabledInfoContents               = new("Lightmapping settings are currently disabled. Enable Baked Global Illumination or Realtime Global Illumination to display these settings.");
        readonly static GUIContent kUVChartingContents                     = new("UV Charting Control");
        readonly static GUIContent kImportantGIContents                    = new("Prioritize Illumination", "When enabled, the object will be marked as a priority object and always included in lighting calculations. Useful for objects that will be strongly emissive to make sure that other objects will be illuminated by this object.");
        readonly static GUIContent kScaleInLightmapContents                = new("Scale In Lightmap", "Specifies the relative size of object's UVs within a lightmap. A value of 0 will result in the object not being light mapped, but still contribute lighting to other objects in the Scene.");
        readonly static GUIContent kOptimizeRealtimeUVsContents            = new("Optimize Realtime UVs", "Specifies whether the generated model UVs get optimized for Realtime Global Illumination or not. When enabled, the UVs can get merged, scaled, and packed for optimization purposes. When disabled, the UVs will get scaled and packed, but not merged.");
        readonly static GUIContent kAutoUVMaxDistanceContents              = new("Max Distance", "Specifies the maximum worldspace distance to be used for UV chart simplification. If charts are within this distance they will be simplified for optimization purposes.");
        readonly static GUIContent kAutoUVMaxAngleContents                 = new("Max Angle", "Specifies the maximum angle in degrees between faces sharing a UV edge. If the angle between the faces is below this value, the UV charts will be simplified.");
        readonly static GUIContent kIgnoreNormalsForChartDetectionContents = new("Ignore Normals", "When enabled, prevents the UV charts from being split during the precompute process for Realtime Global Illumination lighting.");
        readonly static GUIContent kLightmapParametersContents             = new("Lightmap Parameters", "Allows the adjustment of advanced parameters that affect the process of generating a lightmap for an object using global illumination.");
        readonly static GUIContent kDynamicOccludeeContents                = new("Dynamic Occluded", "Controls if dynamic occlusion culling should be performed for this model.");
        readonly static GUIContent kProbeAnchorContents                    = new("Anchor Override", "Specifies the Transform ` that will be used for sampling the light probes and reflection probes.");
        readonly static GUIContent kReflectionProbeUsageContents           = new("Reflection Probes", "Specifies if or how the object is affected by reflections in the Scene. This property cannot be disabled in deferred rendering modes.");
		readonly static GUIContent kLightProbeUsageContents                = new("Light Probes", "Specifies how Light Probes will handle the interpolation of lighting and occlusion. Disabled if the object is set to Lightmap Static.");
        readonly static GUIContent kLightProbeVolumeOverrideContents       = new("Proxy Volume Override", "If set, the Model will use the Light Probe Proxy Volume component from another GameObject.");
        readonly static GUIContent kLightProbeCustomContents               = new("The Custom Provided mode is not supported.");
        readonly static GUIContent kLightProbeVolumeContents               = new("A valid Light Probe Proxy Volume component could not be found.");
        readonly static GUIContent kLightProbeVolumeUnsupportedContents    = new("The Light Probe Proxy Volume feature is unsupported by the current graphics hardware or API configuration. Simple 'Blend Probes' mode will be used instead.");
        readonly static GUIContent kRenderingLayerMaskStyle                = new("Rendering Layer Mask", "Mask that can be used with SRP DrawRenderers command to filter renderers outside of the normal layering system.");
        readonly static GUIContent kStaticBatchingWarningContents          = new("This model is statically batched and uses an instanced shader at the same time. Instancing will be disabled in such a case. Consider disabling static batching if you want it to be instanced.");
        readonly static GUIContent kNoNormalsNoLightmappingContents        = new("VertexChannels is set to not have any normals. Normals are needed for lightmapping.");
        //readonly static GUIContent kLightmapInfoBoxContents              = new("To enable generation of lightmaps for this Model, please enable the 'Lightmap Static' property.");
        readonly static GUIContent kClampedPackingResolutionContents       = new("Object's size in the realtime lightmap has reached the maximum size. Try dividing large brushes into smaller pieces.");
        readonly static GUIContent kUVOverlapContents                      = new("This model has overlapping UVs. This is caused by Unity's own code.");
        readonly static GUIContent kClampedSizeContents                    = new("Object's size in lightmap has reached the max atlas size.", "If you need higher resolution for this object, try dividing large brushes into smaller pieces or set higher max atlas size via the LightmapEditorSettings class.");
        readonly static GUIContent kIsTriggerContents                      = new("Is Trigger", "Is this model a trigger? Triggers are only supported on convex models.");
        readonly static GUIContent kConvextContents                        = new("Convex", "Create a convex collider for this model?");
        readonly static GUIContent kVertexChannelMaskContents              = new("Vertex Channel Mask", "Select which vertex channels will be used in the generated meshes");
        //readonly static GUIContent kSkinWidthContents                    = new("Skin Width", "How far out to inflate the mesh when building collision mesh.");
        readonly static GUIContent kCookingOptionsContents                 = new("Cooking Options", "Options affecting the result of the mesh processing by the physics engine.");
        readonly static GUIContent kDefaultModelContents                   = new("This model is the default model, all nodes that are not part of a model are automatically added to this model.");
        readonly static GUIContent kStitchLightmapSeamsContents            = new("Stitch Seams", "When enabled, seams in baked lightmaps will get smoothed.");
        readonly static GUIContent kContributeGIContents                   = new("Contribute Global Illumination", "When enabled, this GameObject influences lightmaps and Light Probes. If you want this object itself to be lightmapped, you must enable this property.");
        readonly static GUIContent kMinimumChartSizeContents               = new("Min Chart Size", "Specifies the minimum texel size used for a UV chart. If stitching is required, a value of 4 will create a chart of 4x4 texels to store lighting and directionality. If stitching is not required, a value of 2 will reduce the texel density and provide better lighting build times and run time performance.");
        readonly static GUIContent kReceiveGITitle                         = new("Receive Global Illumination", "If enabled, this GameObject receives global illumination from lightmaps or Light Probes. To use lightmaps, Contribute Global Illumination must be enabled.");
        
        readonly static int[]        kMinimumChartSizeValues        = { 2, 4 };
        readonly static GUIContent[] kMinimumChartSizeStrings =
        {
            new("2 (Minimum)"),
            new("4 (Stitchable)"),
        };

        readonly static int[]        kReceiveGILightmapValues = { (int)ReceiveGI.Lightmaps, (int)ReceiveGI.LightProbes };
        readonly static GUIContent[] kReceiveGILightmapStrings =
        {
            new("Lightmaps"),
            new("Light Probes")
        };


        static GUIContent[] s_ReflectionProbeUsageOptionsContents;



        const string kDisplayLightingKey            = "ChiselModelEditor.ShowLightingSettings";
        const string kDisplayProbesKey              = "ChiselModelEditor.ShowProbeSettings";
        const string kDisplayAdditionalSettingsKey  = "ChiselModelEditor.ShowAdditionalSettings";
        const string kDisplayGenerationSettingsKey  = "ChiselModelEditor.ShowGenerationSettings";
        const string kDisplayColliderSettingsKey    = "ChiselModelEditor.ShowColliderSettings";
        const string kDisplayLightmapKey            = "ChiselModelEditor.ShowLightmapSettings";
        const string kDisplayChartingKey            = "ChiselModelEditor.ShowChartingSettings";
        const string kDisplayUnwrapParamsKey        = "ChiselModelEditor.ShowUnwrapParams";
        const string kDisplayDebugKey               = "ChiselModelEditor.ShowDebugSettings";


        SerializedProperty vertexChannelMaskProp;
        SerializedProperty createRenderComponentsProp;
        SerializedProperty createColliderComponentsProp;
        SerializedProperty subtractiveEditingProp;
        SerializedProperty smoothNormalsProp;
        SerializedProperty smoothingAngleProp;
        SerializedProperty debugLogBrushesProp;
        SerializedProperty debugLogOutputProp;
        SerializedProperty autoRebuildUVsProp;
        SerializedProperty angleErrorProp;
        SerializedProperty areaErrorProp;
        SerializedProperty hardAngleProp;
        SerializedProperty packMarginPixelsProp;
        SerializedProperty motionVectorsProp;
        SerializedProperty importantGIProp;
        SerializedProperty receiveGIProp;
        SerializedProperty lightmapScaleProp;
        SerializedProperty preserveUVsProp;
        SerializedProperty autoUVMaxDistanceProp;
        SerializedProperty ignoreNormalsForChartDetectionProp;
        SerializedProperty autoUVMaxAngleProp;
        SerializedProperty minimumChartSizeProp;
        SerializedProperty lightmapParametersProp;
        SerializedProperty allowOcclusionWhenDynamicProp;
        SerializedProperty renderingLayerMaskProp;
        SerializedProperty reflectionProbeUsageProp;
        SerializedProperty lightProbeUsageProp;
        SerializedProperty lightProbeVolumeOverrideProp;
        SerializedProperty probeAnchorProp;
        SerializedProperty stitchLightmapSeamsProp;

        SerializedObject   gameObjectsSerializedObject;
        SerializedProperty staticEditorFlagsProp;


        SerializedProperty convexProp;
        SerializedProperty isTriggerProp;
        SerializedProperty cookingOptionsProp;
        //SerializedProperty skinWidthProp;

        bool showLighting;
        bool showProbes;
        bool showAdditionalSettings;
        bool showGenerationSettings;
        bool showColliderSettings;
        bool showLightmapSettings;
        bool showChartingSettings;
        bool showUnwrapParams;
        bool showDebug;

        UnityEngine.Object[] childNodes;

        readonly static ChiselComponentInspectorMessageHandler s_Messages = new();
        //private Vector2 modelMessagesScrollPosition = Vector2.zero;
        Vector2 childMessagesScrollPosition = Vector2.zero;


        delegate float GetCachedMeshSurfaceAreaDelegate(MeshRenderer meshRenderer);
        delegate bool HasClampedResolutionDelegate(Renderer renderer);
        delegate bool HasUVOverlapsDelegate(Renderer renderer);
        delegate bool HasInstancingDelegate(Shader s);

        delegate void LightmapParametersGUIDelegate(SerializedProperty prop, GUIContent content);
        readonly static LightmapParametersGUIDelegate kLightmapParametersGUI = ReflectionExtensions.CreateDelegate<LightmapParametersGUIDelegate>("UnityEditor.RendererLightingSettings", "LightmapParametersGUI");
        readonly static HasClampedResolutionDelegate kHasClampedResolution = typeof(Lightmapping).CreateDelegate<HasClampedResolutionDelegate>("HasClampedResolution");
		readonly static HasUVOverlapsDelegate kHasUVOverlaps = typeof(Lightmapping).CreateDelegate<HasUVOverlapsDelegate>("HasUVOverlaps");

		readonly static GetCachedMeshSurfaceAreaDelegate kGetCachedMeshSurfaceArea    = ReflectionExtensions.CreateDelegate<GetCachedMeshSurfaceAreaDelegate>("UnityEditor.InternalMeshUtil", "GetCachedMeshSurfaceArea");
		readonly static HasInstancingDelegate            kHasInstancing               = typeof(ShaderUtil).CreateDelegate<HasInstancingDelegate>("HasInstancing");

        readonly static ReflectedInstanceProperty<int> HasMultipleDifferentValuesBitwise = typeof(SerializedProperty).GetProperty<int>("hasMultipleDifferentValuesBitwise");

        internal void OnEnable()
        {
            EditorApplication.contextualPropertyMenu -= OnPropertyContextMenu;
            EditorApplication.contextualPropertyMenu += OnPropertyContextMenu;

            if (s_ReflectionProbeUsageOptionsContents == null)
                s_ReflectionProbeUsageOptionsContents = (Enum.GetNames(typeof(ReflectionProbeUsage)).Select(x => ObjectNames.NicifyVariableName(x)).ToArray()).Select(x => new GUIContent(x)).ToArray();

            showLighting            = EditorPrefs.GetBool(kDisplayLightingKey, false);
            showProbes              = EditorPrefs.GetBool(kDisplayProbesKey, false);
            showAdditionalSettings  = EditorPrefs.GetBool(kDisplayAdditionalSettingsKey, false);
            showGenerationSettings  = SessionState.GetBool(kDisplayGenerationSettingsKey, false);
            showColliderSettings    = SessionState.GetBool(kDisplayColliderSettingsKey, false);
            showLightmapSettings    = SessionState.GetBool(kDisplayLightmapKey, true);
            showChartingSettings    = SessionState.GetBool(kDisplayChartingKey, true);
            showUnwrapParams        = SessionState.GetBool(kDisplayUnwrapParamsKey, true);
            showDebug               = SessionState.GetBool(kDisplayDebugKey, false);

            if (!target)
            {
                childNodes = new UnityEngine.Object[] { };
                return;
            }

            vertexChannelMaskProp        = serializedObject.FindProperty($"{ChiselModelComponent.kVertexChannelMaskName}");
           createRenderComponentsProp   = serializedObject.FindProperty($"{ChiselModelComponent.kCreateRenderComponentsName}");
           createColliderComponentsProp = serializedObject.FindProperty($"{ChiselModelComponent.kCreateColliderComponentsName}");
           subtractiveEditingProp       = serializedObject.FindProperty($"{ChiselModelComponent.kSubtractiveEditingName}");
            smoothNormalsProp           = serializedObject.FindProperty($"{ChiselModelComponent.kSmoothNormalsName}");
            smoothingAngleProp          = serializedObject.FindProperty($"{ChiselModelComponent.kSmoothingAngleName}");
            debugLogBrushesProp         = serializedObject.FindProperty($"{ChiselModelComponent.kDebugLogBrushesName}");
            debugLogOutputProp          = serializedObject.FindProperty($"{ChiselModelComponent.kDebugLogOutputName}");
           autoRebuildUVsProp           = serializedObject.FindProperty($"{ChiselModelComponent.kAutoRebuildUVsName}");
            angleErrorProp               = serializedObject.FindProperty($"{ChiselModelComponent.kRenderSettingsName}.{ChiselGeneratedRenderSettings.kUVGenerationSettingsName}.{SerializableUnwrapParam.kAngleErrorName}");
            areaErrorProp                = serializedObject.FindProperty($"{ChiselModelComponent.kRenderSettingsName}.{ChiselGeneratedRenderSettings.kUVGenerationSettingsName}.{SerializableUnwrapParam.kAreaErrorName}");
            hardAngleProp                = serializedObject.FindProperty($"{ChiselModelComponent.kRenderSettingsName}.{ChiselGeneratedRenderSettings.kUVGenerationSettingsName}.{SerializableUnwrapParam.kHardAngleName}");
            packMarginPixelsProp         = serializedObject.FindProperty($"{ChiselModelComponent.kRenderSettingsName}.{ChiselGeneratedRenderSettings.kUVGenerationSettingsName}.{SerializableUnwrapParam.kPackMarginPixelsName}");


            motionVectorsProp                   = serializedObject.FindProperty($"{ChiselModelComponent.kRenderSettingsName}.{ChiselGeneratedRenderSettings.kMotionVectorGenerationModeName}");
            importantGIProp                     = serializedObject.FindProperty($"{ChiselModelComponent.kRenderSettingsName}.{ChiselGeneratedRenderSettings.kImportantGIName}");
            receiveGIProp                       = serializedObject.FindProperty($"{ChiselModelComponent.kRenderSettingsName}.{ChiselGeneratedRenderSettings.kReceiveGIName}");
            lightmapScaleProp                   = serializedObject.FindProperty($"{ChiselModelComponent.kRenderSettingsName}.{ChiselGeneratedRenderSettings.kScaleInLightmapName}");
            preserveUVsProp                     = serializedObject.FindProperty($"{ChiselModelComponent.kRenderSettingsName}.{ChiselGeneratedRenderSettings.kOptimizeUVsName}");
            autoUVMaxDistanceProp               = serializedObject.FindProperty($"{ChiselModelComponent.kRenderSettingsName}.{ChiselGeneratedRenderSettings.kAutoUVMaxDistanceName}");
            autoUVMaxAngleProp                  = serializedObject.FindProperty($"{ChiselModelComponent.kRenderSettingsName}.{ChiselGeneratedRenderSettings.kAutoUVMaxAngleName}");
            ignoreNormalsForChartDetectionProp  = serializedObject.FindProperty($"{ChiselModelComponent.kRenderSettingsName}.{ChiselGeneratedRenderSettings.kIgnoreNormalsForChartDetectionName}");
            minimumChartSizeProp                = serializedObject.FindProperty($"{ChiselModelComponent.kRenderSettingsName}.{ChiselGeneratedRenderSettings.kMinimumChartSizeName}");
            lightmapParametersProp              = serializedObject.FindProperty($"{ChiselModelComponent.kRenderSettingsName}.{ChiselGeneratedRenderSettings.kLightmapParametersName}");
            allowOcclusionWhenDynamicProp       = serializedObject.FindProperty($"{ChiselModelComponent.kRenderSettingsName}.{ChiselGeneratedRenderSettings.kAllowOcclusionWhenDynamicName}");
            renderingLayerMaskProp              = serializedObject.FindProperty($"{ChiselModelComponent.kRenderSettingsName}.{ChiselGeneratedRenderSettings.kRenderingLayerMaskName}");
            reflectionProbeUsageProp            = serializedObject.FindProperty($"{ChiselModelComponent.kRenderSettingsName}.{ChiselGeneratedRenderSettings.kReflectionProbeUsageName}");
            lightProbeUsageProp                 = serializedObject.FindProperty($"{ChiselModelComponent.kRenderSettingsName}.{ChiselGeneratedRenderSettings.kLightProbeUsageName}");
            lightProbeVolumeOverrideProp        = serializedObject.FindProperty($"{ChiselModelComponent.kRenderSettingsName}.{ChiselGeneratedRenderSettings.kLightProbeVolumeOverrideName}");
            probeAnchorProp                     = serializedObject.FindProperty($"{ChiselModelComponent.kRenderSettingsName}.{ChiselGeneratedRenderSettings.kProbeAnchorName}");
            stitchLightmapSeamsProp             = serializedObject.FindProperty($"{ChiselModelComponent.kRenderSettingsName}.{ChiselGeneratedRenderSettings.kStitchLightmapSeamsName}");

            convexProp              = serializedObject.FindProperty($"{ChiselModelComponent.kColliderSettingsName}.{ChiselGeneratedColliderSettings.kConvexName}");
            isTriggerProp           = serializedObject.FindProperty($"{ChiselModelComponent.kColliderSettingsName}.{ChiselGeneratedColliderSettings.kIsTriggerName}");
            cookingOptionsProp      = serializedObject.FindProperty($"{ChiselModelComponent.kColliderSettingsName}.{ChiselGeneratedColliderSettings.kCookingOptionsName}");
            //skinWidthProp         = serializedObject.FindProperty($"{ChiselModel.kColliderSettingsName}.{ChiselGeneratedColliderSettings.kSkinWidthName}");

            gameObjectsSerializedObject = new SerializedObject(serializedObject.targetObjects.Select(t => ((Component)t).gameObject).ToArray());
            staticEditorFlagsProp = gameObjectsSerializedObject.FindProperty("m_StaticEditorFlags");

            for (int t = 0; t < targets.Length; t++)
            {
                var modelTarget = targets[t] as ChiselModelComponent;
                if (!modelTarget)
                    continue;

                if (!modelTarget.IsInitialized)
                    modelTarget.OnInitialize();

            }


            var uniqueChildNodes = HashSetPool<UnityEngine.Object>.Get();
            try
            {
                if (targets.Length == 1)
                {
                    var modelTarget = targets[0] as ChiselModelComponent;
                    if (modelTarget)
                    {
                        foreach (var child in modelTarget.GetComponentsInChildren<ChiselNodeComponent>(includeInactive: false))
                        {
                            if (child.isActiveAndEnabled)
                                uniqueChildNodes.Add(child);
                        }
                    }
                }
                childNodes = uniqueChildNodes.ToArray();
            }
            finally
            {
                HashSetPool<UnityEngine.Object>.Release(uniqueChildNodes);
            }
        }

        private void OnDestroy()
        {
            EditorApplication.contextualPropertyMenu -= OnPropertyContextMenu;
        }

        void ClearLightmapData()
        {
            foreach (var obj in targets)
            {
                var modelComponent = target as ChiselModelComponent;
				if (!modelComponent)
                    continue;

                GameObjectState state = GameObjectState.Create(modelComponent.gameObject);

				foreach (var renderable in modelComponent.generated.renderables)
                {
                    if (renderable == null || !renderable.IsValid())
                        continue;
                    if (ChiselUnityUVGenerationManager.ClearLightmapData(state, renderable))
                    {
                        //Debug.Log($"ClearLightmapData for {renderable.container.name}", renderable.container);
                    }
				}
			}
        }

		void OnPropertyContextMenu(GenericMenu menu, SerializedProperty property)
		{
            if (property.propertyPath == angleErrorProp.propertyPath)
			{
				var propertyCopy = property.Copy();
				menu.AddItem(new GUIContent("Reset"), false, () =>
				{
					float val = SerializableUnwrapParam.GetDefaultAngleError();
					propertyCopy.floatValue = val;
					propertyCopy.serializedObject.ApplyModifiedProperties();
					if (autoRebuildUVsProp.boolValue)
					{
						ChiselUnityUVGenerationManager.ForceUpdateDelayedUVGeneration();
						ClearLightmapData();
					}
				});
			} else
            if (property.propertyPath == areaErrorProp.propertyPath)
			{
				var propertyCopy = property.Copy();
				menu.AddItem(new GUIContent("Reset"), false, () =>
                {
                    float val = SerializableUnwrapParam.GetDefaultAreaError();
					propertyCopy.floatValue = val;
					propertyCopy.serializedObject.ApplyModifiedProperties();
					if (autoRebuildUVsProp.boolValue)
					{
						ChiselUnityUVGenerationManager.ForceUpdateDelayedUVGeneration();
						ClearLightmapData();
					}
				});
            } else
			if (property.propertyPath == hardAngleProp.propertyPath)
			{
				var propertyCopy = property.Copy();
				menu.AddItem(new GUIContent("Reset"), false, () =>
                {
                    float val = SerializableUnwrapParam.GetDefaultHardAngle();
					propertyCopy.floatValue = val;
					propertyCopy.serializedObject.ApplyModifiedProperties();
					if (autoRebuildUVsProp.boolValue)
					{
						ChiselUnityUVGenerationManager.ForceUpdateDelayedUVGeneration();
						ClearLightmapData();
					}
				});
			} else
			if (property.propertyPath == packMarginPixelsProp.propertyPath)
			{
				var propertyCopy = property.Copy();
				menu.AddItem(new GUIContent("Reset"), false, () =>
                {
                    float val = SerializableUnwrapParam.GetDefaultPackMarginPixels();
					propertyCopy.floatValue = val;
					propertyCopy.serializedObject.ApplyModifiedProperties();
					if (autoRebuildUVsProp.boolValue)
					{
						ChiselUnityUVGenerationManager.ForceUpdateDelayedUVGeneration();
						ClearLightmapData();
					}
				});
			}
		}

		bool IsPreset
		{
			get
			{
				if (serializedObject == null || serializedObject.targetObject == null)
					return false;

				var type = PrefabUtility.GetPrefabAssetType(serializedObject.targetObject);
				return (type == PrefabAssetType.Regular || type == PrefabAssetType.Model);
			}
		}

		bool IsPrefabAsset
        {
            get
            {
                if (serializedObject == null || serializedObject.targetObject == null)
                    return false;

                var type = PrefabUtility.GetPrefabAssetType(serializedObject.targetObject);
                return (type == PrefabAssetType.Regular || type == PrefabAssetType.Model);
            }
        }

        bool GIEnabled
        {
            get { return (Lightmapping.bakedGI || Lightmapping.realtimeGI) || IsPrefabAsset; }
        }

        bool ContributeGI
        {
            get
            {
                if (staticEditorFlagsProp == null)
                    return false;
                return (staticEditorFlagsProp.intValue & (int)StaticEditorFlags.ContributeGI) != 0;
            }
            set
            {
                if (gameObjectsSerializedObject == null || gameObjectsSerializedObject.targetObjects == null || gameObjectsSerializedObject.targetObjects.Length == 0)
                    return;
                SceneModeUtility.SetStaticFlags(gameObjectsSerializedObject.targetObjects, (int)StaticEditorFlags.ContributeGI, value);
            }
        }

        bool MixedGIValue
        {
            get
            {
                if (staticEditorFlagsProp == null)
                    return false;
                return (HasMultipleDifferentValuesBitwise.GetValue(staticEditorFlagsProp) & (int)StaticEditorFlags.ContributeGI) != 0;
            }
        }


        bool BatchingStatic
        {
            get
            {
                return !staticEditorFlagsProp.hasMultipleDifferentValues && ((StaticEditorFlags)staticEditorFlagsProp.intValue & StaticEditorFlags.BatchingStatic) != 0;
            }
        }

        bool ShowEnlightenSettings
        {
            get
            {
                return (IsPreset || IsPrefabAsset || RealtimeGI) && SupportedRenderingFeatures.active.enlighten;
            }
		}

		bool RealtimeGI
		{
			get
			{
				if (!Lightmapping.TryGetLightingSettings(out LightingSettings lightingSettings))
				{
					lightingSettings = Lightmapping.lightingSettingsDefaults;
				}
				return lightingSettings.realtimeGI;
			}
		}

		bool IsBaked
        {
            get
			{
                if (!Lightmapping.TryGetLightingSettings(out LightingSettings lightingSettings))
                {
                    lightingSettings = Lightmapping.lightingSettingsDefaults;
				}
				return lightingSettings.bakedGI;
			}
        }

		bool ShowProgressiveSettings
        {
            get
            {
                return (IsPrefabAsset || IsPreset || IsBaked);
            }
        }

        float GetLargestCachedMeshSurfaceAreaForTargets(float defaultValue)
        {
            if (target == null || kGetCachedMeshSurfaceArea == null)
                return defaultValue;
            float largestSurfaceArea = -1;
            foreach(var target in targets)
            {
                var model = target as ChiselModelComponent;
                if (!model)
                    continue;
                var renderables = model.generated?.renderables;
                if (renderables == null)
                    continue;
                for (int r = 0; r < renderables.Length; r++)
                {
                    var renderable = renderables[r];
                    if (renderable == null || !renderable.Valid)
                        continue;
                    var meshRenderer = renderable.meshRenderer;
                    if (!meshRenderer)
                        continue;
                    largestSurfaceArea = Mathf.Max(largestSurfaceArea, kGetCachedMeshSurfaceArea(meshRenderer));
                }
            }
            if (largestSurfaceArea >= 0)
                return largestSurfaceArea;
            return defaultValue;
        }

        bool TargetsHaveClampedResolution()
        {
            if (target == null || kHasClampedResolution == null)
                return false;
            foreach (var target in targets)
            {
                var model = target as ChiselModelComponent;
                if (!model)
                    continue;
                var renderables = model.generated?.renderables;
                if (renderables == null)
                    continue;
                for (int r = 0; r < renderables.Length; r++)
                {
                    var renderable = renderables[r];
                    if (renderable == null || !renderable.Valid)
                        continue;
                    var meshRenderer = renderable.meshRenderer;
                    if (!meshRenderer)
                        continue;
                    if (kHasClampedResolution(meshRenderer))
                        return true;
                }
            }
            return false;
        }

        bool TargetsHaveUVOverlaps()
        {
            if (target == null || kHasUVOverlaps == null)
                return false;
            foreach (var target in targets)
            {
                var model = target as ChiselModelComponent;
                if (!model)
                    continue;
                var renderables = model.generated?.renderables;
                if (renderables == null)
                    continue;
                for (int r = 0; r < renderables.Length; r++)
                {
                    var renderable = renderables[r];
                    if (renderable == null || !renderable.Valid)
                        continue;
                    var meshRenderer = renderable.meshRenderer;
                    if (!meshRenderer)
                        continue;
                    if (kHasUVOverlaps(meshRenderer))
                        return true;
                }
            }
            return false;
        }

        bool TargetsUseInstancingShader()
        {
            if (target == null || kHasInstancing == null)
                return false;
            foreach (var target in targets)
            {
                var model = target as ChiselModelComponent;
                if (!model)
                    continue;
                var renderables = model.generated?.renderables;
                if (renderables == null)
                    continue;
                foreach (var renderable in renderables)
                {
                    if (renderable == null ||
                        renderable.renderMaterials == null)
                        continue;
                    foreach (var material in renderable.renderMaterials)
                    {
                        if (material != null && material.enableInstancing && material.shader != null && kHasInstancing(material.shader))
                            return true;
                    }
                }
            }
            return false;
        }

        bool NeedLightmapRebuild()
        {
            if (target == null)
                return false;

            foreach(var target in targets)
            {
                var model = target as ChiselModelComponent;
                if (!model)
                    continue;

                if (ChiselUnityUVGenerationManager.NeedUVGeneration(model))
                    return true;
            }
            return false;
        }

        internal bool IsUsingLightProbeProxyVolume()
        {
            bool isUsingLightProbeVolumes =
                ((targets.Length == 1) && (lightProbeUsageProp.intValue == (int)LightProbeUsage.UseProxyVolume)) ||
                ((targets.Length > 1) && !lightProbeUsageProp.hasMultipleDifferentValues && (lightProbeUsageProp.intValue == (int)LightProbeUsage.UseProxyVolume));

            return isUsingLightProbeVolumes;
        }

        internal bool HasLightProbeProxyOrOverride()
        {/*
            LightProbeProxyVolume lightProbeProxyVol = renderer.GetComponent<LightProbeProxyVolume>();
            bool invalidProxyVolumeOverride = (renderer.lightProbeProxyVolumeOverride == null) ||
                (renderer.lightProbeProxyVolumeOverride.GetComponent<LightProbeProxyVolume>() == null);
            */
            return false;
            // TODO: figure out how to set up LightProxyVolumes
            /*
            var lightProbeProxyVol = renderer.GetComponent<LightProbeProxyVolume>();
            bool invalidProxyVolumeOverride = (renderer.lightProbeProxyVolumeOverride == null) ||
                                                  (renderer.lightProbeProxyVolumeOverride.GetComponent<LightProbeProxyVolume>() == null);
            return lightProbeProxyVol == null && invalidProxyVolumeOverride;
            */
        }

        static internal bool AreLightProbesAllowed(ChiselModelComponent model)
        {
            // TODO: return false if lightmapped or dynamic lightmapped

            /*
            if (!renderer) 
                return false;

            bool isLightmapped = IsLightmappedOrDynamicLightmappedForRendering(renderer->GetLightmapIndices());

            UInt32 lodGroupIndex;
            UInt8 lodMask;
            renderer->GetLODGroupIndexAndMask(&lodGroupIndex, &lodMask);

            return (isLightmapped == false) || ((lodMask & ~1) && GetLightmapSettings().GetGISettings().GetEnableRealtimeLightmaps());
            */
            return true;
        }

        internal bool AreLightProbesAllowed()
        {
            if (targets == null)
                return false;
            foreach (UnityEngine.Object obj in targets)
                if (AreLightProbesAllowed((ChiselModelComponent)obj) == false)
                    return false;
            return true;
        }

        //int selectionCount, Renderer renderer, 
        internal void RenderLightProbeUsage(bool lightProbeAllowed)
        {
            using (new EditorGUI.DisabledScope(!lightProbeAllowed))
            {
                if (lightProbeAllowed)
                {
                    // LightProbeUsage has non-sequential enum values. Extra care is to be taken.
                    Rect r = EditorGUILayout.GetControlRect(true, EditorGUIUtility.singleLineHeight, EditorStyles.popup);
                    EditorGUI.BeginProperty(r, kLightProbeUsageContents, lightProbeUsageProp);
                    EditorGUI.BeginChangeCheck();
                    var newValue = EditorGUI.EnumPopup(r, kLightProbeUsageContents, (LightProbeUsage)lightProbeUsageProp.intValue);
                    if (EditorGUI.EndChangeCheck())
                        lightProbeUsageProp.intValue = (int)(LightProbeUsage)newValue;
                    EditorGUI.EndProperty();

                    if (!lightProbeUsageProp.hasMultipleDifferentValues)
                    {
                        if (SupportedRenderingFeatures.active.lightProbeProxyVolumes &&
                            lightProbeUsageProp.intValue == (int)LightProbeUsage.UseProxyVolume)
                        {
                            EditorGUI.indentLevel++;
                            EditorGUILayout.PropertyField(lightProbeVolumeOverrideProp, kLightProbeVolumeOverrideContents);
                            EditorGUI.indentLevel--;
                        } else 
                        if (lightProbeUsageProp.intValue == (int)LightProbeUsage.CustomProvided)
                        {
                            EditorGUI.indentLevel++;
                            EditorGUILayout.HelpBox(kLightProbeCustomContents.text, MessageType.Error);
                            EditorGUI.indentLevel--;
                        }
                    }
                } else
                {
                    EditorGUILayout.EnumPopup(kLightProbeUsageContents, LightProbeUsage.Off);
                }
            }
        }

        internal void RenderLightProbeProxyVolumeWarningNote()
        {
            if (IsUsingLightProbeProxyVolume())
            {
                if (SupportedRenderingFeatures.active.lightProbeProxyVolumes &&
                    LightProbeProxyVolume.isFeatureSupported)
                {
                    bool hasLightProbeProxyOrOverride = HasLightProbeProxyOrOverride();
                    if (hasLightProbeProxyOrOverride && AreLightProbesAllowed())
                    {
                        EditorGUILayout.HelpBox(kLightProbeVolumeContents.text, MessageType.Warning);
                    }
                } else
                {
                    EditorGUILayout.HelpBox(kLightProbeVolumeUnsupportedContents.text, MessageType.Warning);
                }
            }
        }

        internal void RenderReflectionProbeUsage(bool isDeferredRenderingPath, bool isDeferredReflections)
        {
            if (!SupportedRenderingFeatures.active.reflectionProbes)
                return;

            using (new EditorGUI.DisabledScope(isDeferredRenderingPath))
            {
                // reflection probe usage field; UI disabled when using deferred reflections
                if (isDeferredReflections)
                {
                    EditorGUILayout.EnumPopup(kReflectionProbeUsageContents, (reflectionProbeUsageProp.intValue != (int)ReflectionProbeUsage.Off) ? ReflectionProbeUsage.Simple : ReflectionProbeUsage.Off);
                } else
                {
                    Rect r = EditorGUILayout.GetControlRect(true, EditorGUIUtility.singleLineHeight, EditorStyles.popup);
                    ChiselEditorUtility.Popup(r, reflectionProbeUsageProp, s_ReflectionProbeUsageOptionsContents, kReflectionProbeUsageContents);
                }
            }
        }

        internal bool RenderProbeAnchor()
        {
            bool useReflectionProbes	= !reflectionProbeUsageProp.hasMultipleDifferentValues && (ReflectionProbeUsage)reflectionProbeUsageProp.intValue != ReflectionProbeUsage.Off;
            bool lightProbesEnabled		= !lightProbeUsageProp.hasMultipleDifferentValues && (LightProbeUsage)lightProbeUsageProp.intValue != LightProbeUsage.Off;
            bool needsRendering			= useReflectionProbes || lightProbesEnabled;

            if (needsRendering)
                EditorGUILayout.PropertyField(probeAnchorProp, kProbeAnchorContents);

            return needsRendering;
        }


        void RenderProbeFieldsGUI(bool isDeferredRenderingPath)
		{
			bool isDeferredReflections = isDeferredRenderingPath && ChiselEditorUtility.IsDeferredReflections();
            bool areLightProbesAllowed = AreLightProbesAllowed();

            RenderLightProbeUsage(areLightProbesAllowed);

            RenderLightProbeProxyVolumeWarningNote();

            RenderReflectionProbeUsage(isDeferredRenderingPath, isDeferredReflections);

            RenderProbeAnchor();
        }

        float LightmapScaleGUI(float lodScale)
        {
            var lightmapScaleValue = lodScale * lightmapScaleProp.floatValue;

            var rect = EditorGUILayout.GetControlRect();
            EditorGUI.BeginProperty(rect, kScaleInLightmapContents, lightmapScaleProp);
            EditorGUI.BeginChangeCheck();
            {
                lightmapScaleValue = EditorGUI.FloatField(rect, kScaleInLightmapContents, lightmapScaleValue);
            }
            if (EditorGUI.EndChangeCheck())
                lightmapScaleProp.floatValue = Mathf.Max(lightmapScaleValue / Mathf.Max(lodScale, float.Epsilon), 0.0f);
            EditorGUI.EndProperty();

            return lightmapScaleValue;
        }

        // To be used by internal code when just reading settings, not settings them
        static LightingSettings GetLightingSettingsOrDefaultsFallback()
        {
            LightingSettings lightingSettings;
            try
            {
                lightingSettings = Lightmapping.lightingSettings;
            }
            catch 
            {
                lightingSettings = null;
            }

            if (lightingSettings != null)
                return lightingSettings;

            return Lightmapping.lightingSettingsDefaults;
        }

        void ShowClampedSizeInLightmapGUI(float lightmapScale)
        {
            var lightingSettings = GetLightingSettingsOrDefaultsFallback();

            var cachedSurfaceArea = GetLargestCachedMeshSurfaceAreaForTargets(defaultValue: 1.0f);
            var sizeInLightmap = Mathf.Sqrt(cachedSurfaceArea) * lightingSettings.lightmapResolution * lightmapScale;
            
            if (sizeInLightmap > lightingSettings.lightmapMaxSize)
                EditorGUILayout.HelpBox(kClampedSizeContents.text, MessageType.Info);
        }

        void RendererUVSettings()
        {
            EditorGUI.indentLevel++;

            var optimizeRealtimeUVs = !preserveUVsProp.boolValue;
            EditorGUI.BeginChangeCheck();
            optimizeRealtimeUVs = EditorGUILayout.Toggle(kOptimizeRealtimeUVsContents, optimizeRealtimeUVs);

            if (EditorGUI.EndChangeCheck())
                preserveUVsProp.boolValue = !optimizeRealtimeUVs;

            EditorGUI.indentLevel++;
            {
                var disabledAutoUVs = preserveUVsProp.boolValue;
                using (new EditorGUI.DisabledScope(disabledAutoUVs))
                {
                    EditorGUILayout.PropertyField(autoUVMaxDistanceProp, kAutoUVMaxDistanceContents);
                    if (autoUVMaxDistanceProp.floatValue < 0.0f)
                        autoUVMaxDistanceProp.floatValue = 0.0f;
                    EditorGUILayout.Slider(autoUVMaxAngleProp, 0, 180, kAutoUVMaxAngleContents);
                    if (autoUVMaxAngleProp.floatValue < 0.0f)
                        autoUVMaxAngleProp.floatValue = 0.0f;
                    if (autoUVMaxAngleProp.floatValue > 180.0f)
                        autoUVMaxAngleProp.floatValue = 180.0f;
                }
            }
            EditorGUI.indentLevel--;

            EditorGUILayout.PropertyField(ignoreNormalsForChartDetectionProp, kIgnoreNormalsForChartDetectionContents);

            EditorGUILayout.IntPopup(minimumChartSizeProp, kMinimumChartSizeStrings, kMinimumChartSizeValues, kMinimumChartSizeContents);
            EditorGUI.indentLevel--;
        }

        void RenderAdditionalSettingsGUI()
        {
            if (serializedObject == null || gameObjectsSerializedObject == null || gameObjectsSerializedObject.targetObjects.Length == 0)
                return;

            if (SupportedRenderingFeatures.active.motionVectors && motionVectorsProp != null)
            {
                EditorGUILayout.IntPopup(motionVectorsProp, new[] { new GUIContent("Camera Motion Only"), new GUIContent("Per Object Motion"), new GUIContent("Force No Motion") }, new[] { 0, 1, 2 }, kMotionVectorsContent);
            }

            if (allowOcclusionWhenDynamicProp != null)
                EditorGUILayout.PropertyField(allowOcclusionWhenDynamicProp, kDynamicOccludeeContents);

            RenderRenderingLayer();
        }
        

        void RenderGenerationSettingsGUI()
        {
            if (serializedObject == null || gameObjectsSerializedObject == null || gameObjectsSerializedObject.targetObjects.Length == 0)
                return;

            // TODO: Make Position show up instead of "None" when nothing is selected
            ChiselEditorUtility.EnumFlagsField(kVertexChannelMaskContents, vertexChannelMaskProp, typeof(VertexChannelFlags), EditorStyles.popup);
        }


        bool ContributeGISettings()
        {
            bool contributeGI   = ContributeGI;
            bool mixedValue     = MixedGIValue;
            EditorGUI.showMixedValue = mixedValue;

            EditorGUI.BeginChangeCheck();
            contributeGI = EditorGUILayout.Toggle(kContributeGIContents, contributeGI);

            if (EditorGUI.EndChangeCheck())
            {
                ContributeGI = contributeGI;
                gameObjectsSerializedObject.SetIsDifferentCacheDirty();
                gameObjectsSerializedObject.Update();
            }

            EditorGUI.showMixedValue = false;

            return contributeGI && !mixedValue;
        }

        void RenderMeshSettingsGUI(ReceiveGI receiveGI)
        {
            if (serializedObject == null || gameObjectsSerializedObject == null || gameObjectsSerializedObject.targetObjects.Length == 0)
                return;

            var contributeGI            = ContributeGISettings();

            var showMixedGIValue        = MixedGIValue;
            var showEnlightenSettings   = ShowEnlightenSettings;

            if (!GIEnabled)
            {
                EditorGUILayout.HelpBox(kGINotEnabledInfoContents.text, MessageType.Info);
                return;
            }

            if (contributeGI)
            {
                EditorGUI.BeginChangeCheck();

                EditorGUI.showMixedValue = showMixedGIValue;
                receiveGI = (ReceiveGI)EditorGUILayout.IntPopup(kReceiveGITitle, (int)receiveGI, kReceiveGILightmapStrings, kReceiveGILightmapValues);
                EditorGUI.showMixedValue = false;

                if (EditorGUI.EndChangeCheck())
                    receiveGIProp.intValue = (int)receiveGI;

                if (showEnlightenSettings) EditorGUILayout.PropertyField(importantGIProp, kImportantGIContents);

                if (receiveGI == ReceiveGI.LightProbes && !showMixedGIValue)
                {
                    //LightmapScaleGUI(true, AlbedoScale, true);
                }
            }
            else
            {
                using (new EditorGUI.DisabledScope(true))
                {
                    EditorGUI.showMixedValue = showMixedGIValue;
                    EditorGUILayout.IntPopup(kReceiveGITitle, (int)ReceiveGI.LightProbes, kReceiveGILightmapStrings, kReceiveGILightmapValues);
                    EditorGUI.showMixedValue = false;
                }
            }
            /*
            if (!ContributeGI)
            {
                EditorGUILayout.HelpBox(LightmapInfoBoxContents.text, MessageType.Info);
            }*/
        }

        static string[] s_layerNameCache;
        static string[] LayerNames
        {
            get
            {
                if (s_layerNameCache == null)
                {
                    s_layerNameCache = new string[32];
                    for (int i = 0; i < s_layerNameCache.Length; ++i)
                    {
                        s_layerNameCache[i] = string.Format("Layer{0}", i + 1);
                    }
                }
                return s_layerNameCache;
            }
        }

        void RenderRenderingLayer()
        {
            if (target == null || Tools.current != Tool.Custom || !ChiselEditGeneratorTool.IsActive())
                return;

            // TODO: why are we doing this again?
            bool usingSRP = GraphicsSettings.defaultRenderPipeline != null;
            if (!usingSRP)
                return;

            EditorGUI.showMixedValue = renderingLayerMaskProp.hasMultipleDifferentValues;

            var model		= (ChiselModelComponent)target;
            var mask		= (int)model.RenderSettings.renderingLayerMask;

            EditorGUI.BeginChangeCheck();

            var rect = EditorGUILayout.GetControlRect();
            EditorGUI.BeginProperty(rect, kRenderingLayerMaskStyle, renderingLayerMaskProp);
            mask = EditorGUI.MaskField(rect, kRenderingLayerMaskStyle, mask, LayerNames);
            EditorGUI.EndProperty();

            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObjects(targets, "Set rendering layer mask");
                foreach (var t in targets)
                {
                    var m = t as ChiselModelComponent;
                    if (m != null)
                    {
                        m.RenderSettings.renderingLayerMask = (uint)mask;
                        EditorUtility.SetDirty(t);
                    }
                }
            }
            EditorGUI.showMixedValue = false;
        }

        void MeshRendererLightingGUI()
        {
            var oldShowLighting				= showLighting;
            var oldShowProbes				= showProbes;
            var oldShowAdditionalSettings   = showAdditionalSettings;
            var oldShowLightmapSettings		= showLightmapSettings;
            var oldShowChartingSettings		= showChartingSettings;
            var oldShowUnwrapParams			= showUnwrapParams;

            if (TargetsUseInstancingShader())
            {
                if (BatchingStatic)
                {
                    EditorGUILayout.HelpBox(kStaticBatchingWarningContents.text, MessageType.Warning, true);
                }
            }

            var receiveGI           = (ReceiveGI)receiveGIProp.intValue;
            var showMixedGIValue    = MixedGIValue;
            var haveLightmaps       = GIEnabled && ContributeGI && receiveGI == ReceiveGI.Lightmaps && !showMixedGIValue;


            bool isDeferredRenderingPath = ChiselEditorUtility.IsUsingDeferredRenderingPath();


            if (haveLightmaps)
            {
                var needLightmapRebuild = NeedLightmapRebuild();
                if (!autoRebuildUVsProp.boolValue && needLightmapRebuild)
                {
                    EditorGUILayout.BeginHorizontal(EditorStyles.helpBox); 
                    var messageContents = needLightmapRebuild ? kNeedsLightmapBuildContents : kNeedsLightmapRebuildContents;
                    GUILayout.Label(EditorGUIUtility.TrTextContent(messageContents.text, ChiselEditorUtility.GetHelpIcon(MessageType.Warning)), EditorStyles.wordWrappedLabel);
                    GUILayout.Space(3);
                    var buttonContents = needLightmapRebuild ? kForceBuildUVsContents : kForceRebuildUVsContents;
                    if (GUILayout.Button(buttonContents, GUILayout.ExpandWidth(false)))
                    {
						ChiselUnityUVGenerationManager.DelayedUVGeneration(force: true);
                    }
                    EditorGUILayout.EndHorizontal();
                }
            }


            showLighting = EditorGUILayout.BeginFoldoutHeaderGroup(showLighting, kLightingContent);
            if (showLighting)
            {
                EditorGUI.indentLevel++;
                RenderMeshSettingsGUI(receiveGI);
                EditorGUI.indentLevel--;
            }
            EditorGUILayout.EndFoldoutHeaderGroup();

            if (haveLightmaps)
			{
				showUnwrapParams = EditorGUILayout.BeginFoldoutHeaderGroup(showUnwrapParams, kUnwrapParamsContents);
                if (showUnwrapParams)
                {
                    EditorGUI.indentLevel++;
                    EditorGUI.BeginChangeCheck();
                    EditorGUILayout.PropertyField(autoRebuildUVsProp, kAutoRebuildUVsContents);
                    EditorGUILayout.PropertyField(angleErrorProp);
                    EditorGUILayout.PropertyField(areaErrorProp);
                    EditorGUILayout.PropertyField(hardAngleProp);
                    EditorGUILayout.PropertyField(packMarginPixelsProp);
					if (EditorGUI.EndChangeCheck())
					{
                        if (autoRebuildUVsProp.boolValue)
                        {
                            ChiselUnityUVGenerationManager.ForceUpdateDelayedUVGeneration();
							ClearLightmapData();
						}
					}
					EditorGUI.indentLevel--;
                }
                EditorGUILayout.EndFoldoutHeaderGroup();

                showLightmapSettings = EditorGUILayout.BeginFoldoutHeaderGroup(showLightmapSettings, kLightmappingContents);
                if (showLightmapSettings)
                {
                    EditorGUI.indentLevel++;
                    var showEnlightenSettings   = ShowEnlightenSettings; 
                    var showProgressiveSettings = ShowProgressiveSettings;
                    
                    if (showEnlightenSettings)
                    {
                        showChartingSettings = EditorGUILayout.BeginFoldoutHeaderGroup(showChartingSettings, kUVChartingContents);
                        if (showChartingSettings)
                            RendererUVSettings();
                        EditorGUILayout.EndFoldoutHeaderGroup();
                    }

                    float lightmapScale = LightmapScaleGUI(1.0f);

                    ShowClampedSizeInLightmapGUI(lightmapScale);

                    if (showEnlightenSettings) EditorGUILayout.PropertyField(importantGIProp, kImportantGIContents);

                    if (showProgressiveSettings && stitchLightmapSeamsProp != null)
                        EditorGUILayout.PropertyField(stitchLightmapSeamsProp, kStitchLightmapSeamsContents);

                    if (kLightmapParametersGUI != null && lightmapParametersProp != null)
                        kLightmapParametersGUI(lightmapParametersProp, kLightmapParametersContents);

                    EditorGUILayout.Space();

                    if (TargetsHaveClampedResolution())
                        EditorGUILayout.HelpBox(kClampedPackingResolutionContents.text, MessageType.Warning);

                    if ((vertexChannelMaskProp.intValue & (int)VertexChannelFlags.Normal) != (int)VertexChannelFlags.Normal)
                        EditorGUILayout.HelpBox(kNoNormalsNoLightmappingContents.text, MessageType.Warning);

                    if (TargetsHaveUVOverlaps())
                        EditorGUILayout.HelpBox(kUVOverlapContents.text, MessageType.Warning);
                    EditorGUI.indentLevel--;
                }
                EditorGUILayout.EndFoldoutHeaderGroup();
            }



            if (SupportedRenderingFeatures.active.rendererProbes)
            {
                showProbes = EditorGUILayout.BeginFoldoutHeaderGroup(showProbes, kProbesContent);
                if (showProbes)
                {
                    EditorGUI.indentLevel++;
                    RenderProbeFieldsGUI(isDeferredRenderingPath);
                    EditorGUI.indentLevel--;
                }
                EditorGUILayout.EndFoldoutHeaderGroup();
            }

            showAdditionalSettings = EditorGUILayout.BeginFoldoutHeaderGroup(showAdditionalSettings, kAdditionalSettingsContent);
            if (showAdditionalSettings)
            {
                EditorGUI.indentLevel++;
                RenderAdditionalSettingsGUI();
                EditorGUI.indentLevel--;
            }
            EditorGUILayout.EndFoldoutHeaderGroup();


            serializedObject.ApplyModifiedProperties();


            if (showLighting            != oldShowLighting          ) EditorPrefs.SetBool(kDisplayLightingKey, showLighting);
            if (showProbes              != oldShowProbes            ) EditorPrefs.SetBool(kDisplayProbesKey, showProbes);
            if (showAdditionalSettings  != oldShowAdditionalSettings) EditorPrefs.SetBool(kDisplayAdditionalSettingsKey, showAdditionalSettings);
            if (showLightmapSettings    != oldShowLightmapSettings  ) SessionState.SetBool(kDisplayLightmapKey, showLightmapSettings);
            if (showChartingSettings    != oldShowChartingSettings  ) SessionState.SetBool(kDisplayChartingKey, showChartingSettings);
            if (showUnwrapParams        != oldShowUnwrapParams      ) SessionState.SetBool(kDisplayUnwrapParamsKey, showUnwrapParams);
        }

        bool IsDefaultModel()
        {
            if (serializedObject.targetObjects == null)
                return false;

            for (int i = 0; i < serializedObject.targetObjects.Length; i++)
            {
                if (ChiselModelManager.Instance.IsDefaultModel(serializedObject.targetObjects[i]))
                    return true;
            }
            return false;
        }


		protected override void OnEditSettingsGUI(SceneView sceneView)
        {
            if (Tools.current != Tool.Custom)
                return;


        }

		public override void OnInspectorGUI()
        {
            Profiler.BeginSample("OnInspectorGUI");
            try
            { 
                CheckForTransformationChanges(serializedObject);
            
                var oldShowGenerationSettings   = showGenerationSettings;
                var oldShowColliderSettings     = showColliderSettings;
                var oldShowDebug                = showDebug;

                if (gameObjectsSerializedObject != null) gameObjectsSerializedObject.Update();
                if (serializedObject != null) serializedObject.Update();

                if (IsDefaultModel())
                    EditorGUILayout.HelpBox(kDefaultModelContents.text, MessageType.Warning);

                if (targets.Length == 1)
                {
                    s_Messages.StartWarnings(childMessagesScrollPosition);
                    ChiselMessages.ShowMessages(childNodes, s_Messages);
					childMessagesScrollPosition = s_Messages.EndWarnings();
                }

				EditorGUI.BeginChangeCheck();
                {
                    showGenerationSettings = EditorGUILayout.BeginFoldoutHeaderGroup(showGenerationSettings, kGenerationSettingsContent);
                    if (showGenerationSettings)
                    {
                        EditorGUI.indentLevel++;
                        EditorGUILayout.PropertyField(createColliderComponentsProp, kCreateColliderComponentsContents);
                        EditorGUILayout.PropertyField(createRenderComponentsProp, kCreateRenderComponentsContents);
                        EditorGUILayout.PropertyField(subtractiveEditingProp, kSubtractiveEditingContents);
                        EditorGUILayout.PropertyField(smoothNormalsProp, kSmoothNormalsContents);
                        if (smoothNormalsProp.boolValue)
                            EditorGUILayout.PropertyField(smoothingAngleProp, kSmoothingAngleContents);

                        EditorGUI.BeginDisabledGroup(!createRenderComponentsProp.boolValue);
                        {
                            RenderGenerationSettingsGUI();
                        }
                        EditorGUI.EndDisabledGroup();
                        EditorGUI.indentLevel--;
                    }
                    EditorGUILayout.EndFoldoutHeaderGroup();

                    if (createRenderComponentsProp.boolValue)
                    {
                        MeshRendererLightingGUI();
                    }

                    if (createColliderComponentsProp.boolValue)
                    {
                        showColliderSettings = EditorGUILayout.BeginFoldoutHeaderGroup(showColliderSettings, kColliderSettingsContent);
                        if (showColliderSettings)
                        {
                            EditorGUI.indentLevel++;
                            EditorGUILayout.PropertyField(convexProp, kConvextContents);
                            using (new EditorGUI.DisabledScope(!convexProp.boolValue))
                            {
                                EditorGUILayout.PropertyField(isTriggerProp, kIsTriggerContents);
                            }
                            {
                                ChiselEditorUtility.EnumFlagsField(kCookingOptionsContents, cookingOptionsProp, typeof(MeshColliderCookingOptions), EditorStyles.popup);
                            }
                            EditorGUI.indentLevel--;
                        }
                        EditorGUILayout.EndFoldoutHeaderGroup();
                    }

                    showDebug = EditorGUILayout.BeginFoldoutHeaderGroup(showDebug, kDebugContent);
                    if (showDebug)
                    {
                        EditorGUI.indentLevel++;
                        EditorGUILayout.PropertyField(debugLogBrushesProp, kDebugLogBrushesContents);
                        EditorGUILayout.PropertyField(debugLogOutputProp, kDebugLogOutputContents);
                        EditorGUI.indentLevel--;
                    }
                    EditorGUILayout.EndFoldoutHeaderGroup();
                }
                if (EditorGUI.EndChangeCheck())
                {
                    if (gameObjectsSerializedObject != null)
                        gameObjectsSerializedObject.ApplyModifiedProperties();
                    if (serializedObject != null)
                        serializedObject.ApplyModifiedProperties();
                    ForceUpdateNodeContents(serializedObject); 
                }
            
                if (showGenerationSettings  != oldShowGenerationSettings) SessionState.SetBool(kDisplayGenerationSettingsKey, showGenerationSettings);
                if (showColliderSettings    != oldShowColliderSettings  ) SessionState.SetBool(kDisplayColliderSettingsKey, showColliderSettings);
                if (showDebug               != oldShowDebug           ) SessionState.SetBool(kDisplayDebugKey, showDebug);
            }
            finally
            { 
                Profiler.EndSample();
            }
        }
    }
}
