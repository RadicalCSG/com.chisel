using System;
using System.Runtime.CompilerServices;

using Chisel.Core;

using UnityEngine;
using UnityEngine.Rendering;

namespace Chisel.Components
{
    // Can't use UnityEditor.UnwrapParam since it's not marked as Serializable
    [Serializable]
    public sealed class SerializableUnwrapParam
    {
        public const string kAngleErrorName       = nameof(angleError);
        public const string kAreaErrorName        = nameof(areaError);
        public const string kHardAngleName        = nameof(hardAngle);
        public const string kPackMarginPixelsName = nameof(packMarginPixels);

        public const float minAngleError = 0.001f;
        public const float maxAngleError = 1.000f;

        public const float minAreaError  = 0.001f;
        public const float maxAreaError  = 1.000f;

        public const float minHardAngle  = 1;
        public const float maxHardAngle  = 179;

        public const float minPackMargin = 1;
        public const float maxPackMargin = 256;

		[Range(minAngleError, maxAngleError)] public float angleError;
		[Range(minAreaError,  maxAreaError )] public float areaError;
		[Range(minHardAngle,  maxHardAngle )] public float hardAngle;
		[Range(minPackMargin, maxPackMargin)] public float packMarginPixels;

#if UNITY_EDITOR
        public static float GetDefaultAngleError()
		{
			UnityEditor.UnwrapParam.SetDefaults(out var defaults);
			return defaults.angleError;
		}

		public static float GetDefaultAreaError()
		{
			UnityEditor.UnwrapParam.SetDefaults(out var defaults);
			return defaults.areaError;
		}

		public static float GetDefaultHardAngle()
		{
			UnityEditor.UnwrapParam.SetDefaults(out var defaults);
			return defaults.hardAngle;
		}

		public static float GetDefaultPackMarginPixels()
		{
			UnityEditor.UnwrapParam.SetDefaults(out var defaults);
			return defaults.packMargin * 256;
		}

		public void Reset()
		{
			UnityEditor.UnwrapParam.SetDefaults(out var defaults);
			angleError = defaults.angleError;
			areaError = defaults.areaError;
			hardAngle = defaults.hardAngle;
			packMarginPixels = defaults.packMargin * 256;
		}
#endif
	}

	[Serializable]
    public sealed class ChiselGeneratedColliderSettings
    {
        public const string kIsTriggerName      = nameof(isTrigger);
        public const string kConvexName         = nameof(convex);

        public bool                         isTrigger;
        public bool		                    convex;
        
        // If the cookingOptions are not the default values it would force a full slow rebake later, 
        // even if we already did a Bake in a job
        public const string kCookingOptionsName = nameof(cookingOptions);
//      public const string kSkinWidthName      = nameof(skinWidth);
        public MeshColliderCookingOptions   cookingOptions;
//      public float	                    skinWidth;

        public void Reset()
        {
            isTrigger       = false;
            convex          = false;
            cookingOptions	= (MeshColliderCookingOptions)(2|4|8);
            //skinWidth     = 0.01f;
        }
    }

    [Serializable]
    public sealed class ChiselGeneratedRenderSettings
    {
        public const string kUVGenerationSettingsName       = nameof(uvGenerationSettings);
        public const string kMotionVectorGenerationModeName = nameof(motionVectorGenerationMode);
        public const string kAllowOcclusionWhenDynamicName  = nameof(allowOcclusionWhenDynamic);
        public const string kRenderingLayerMaskName         = nameof(renderingLayerMask);
        public const string kReflectionProbeUsageName       = nameof(reflectionProbeUsage);
        public const string kLightProbeUsageName            = nameof(lightProbeUsage);
        public const string kLightProbeVolumeOverrideName   = nameof(lightProbeProxyVolumeOverride);
        public const string kProbeAnchorName                = nameof(probeAnchor);
        public const string kReceiveGIName                  = nameof(receiveGI);
        
#if UNITY_EDITOR
        public const string kLightmapParametersName             = nameof(lightmapParameters);
        public const string kImportantGIName                    = nameof(importantGI);
        public const string kOptimizeUVsName                    = nameof(optimizeUVs);
        public const string kIgnoreNormalsForChartDetectionName = nameof(ignoreNormalsForChartDetection);
        public const string kScaleInLightmapName                = nameof(scaleInLightmap);
        public const string kAutoUVMaxDistanceName              = nameof(autoUVMaxDistance);
        public const string kAutoUVMaxAngleName                 = nameof(autoUVMaxAngle);
        public const string kMinimumChartSizeName               = nameof(minimumChartSize);
        public const string kStitchLightmapSeamsName            = nameof(stitchLightmapSeams);
#endif

        public GameObject                       lightProbeProxyVolumeOverride;
        public Transform                        probeAnchor;
        public MotionVectorGenerationMode		motionVectorGenerationMode		= MotionVectorGenerationMode.Object;
        public ReflectionProbeUsage				reflectionProbeUsage			= ReflectionProbeUsage.BlendProbes;
        public LightProbeUsage					lightProbeUsage					= LightProbeUsage.BlendProbes;
        public bool                             allowOcclusionWhenDynamic       = true;
        public uint                             renderingLayerMask              = ~(uint)0;
        public ReceiveGI						receiveGI						= ReceiveGI.LightProbes;

#if UNITY_EDITOR
        // SerializedObject access Only
        [SerializeField] 
        UnityEditor.LightmapParameters      lightmapParameters				= null;		// TODO: figure out how to apply this, safely, using SerializedObject
        [SerializeField] 
        bool								importantGI						= false;
        [SerializeField] 
        bool								optimizeUVs                     = false;	// "Preserve UVs"
        [SerializeField] 
        bool								ignoreNormalsForChartDetection  = false;
        [SerializeField] 
        float							    autoUVMaxDistance				= 0.5f;
        [SerializeField] 
        float							    autoUVMaxAngle					= 89;
        [SerializeField]
        int								    minimumChartSize				= 4;

        [NonSerialized]
        internal bool serializedObjectFieldsDirty = true;
        public void SetDirty() { serializedObjectFieldsDirty = true; }
        public UnityEditor.LightmapParameters   LightmapParameters				{ get { return lightmapParameters; } set { lightmapParameters = value; serializedObjectFieldsDirty = true; } }
        public bool								ImportantGI						{ get { return importantGI; } set { importantGI = value; serializedObjectFieldsDirty = true; } }
        public bool								OptimizeUVs                     { get { return optimizeUVs; } set { optimizeUVs = value; serializedObjectFieldsDirty = true; } }
        public bool								IgnoreNormalsForChartDetection  {get { return ignoreNormalsForChartDetection; } set { ignoreNormalsForChartDetection = value; serializedObjectFieldsDirty = true; } }
        public float							AutoUVMaxDistance				{get { return autoUVMaxDistance; } set { autoUVMaxDistance = value; serializedObjectFieldsDirty = true; } }
        public float							AutoUVMaxAngle					{get { return autoUVMaxAngle; } set { autoUVMaxAngle = value; serializedObjectFieldsDirty = true; } }
        public int								MinimumChartSize				{get { return minimumChartSize; } set { minimumChartSize = value; serializedObjectFieldsDirty = true; } }
        // SerializedObject access Only

        public bool								stitchLightmapSeams				= false;
        public float							scaleInLightmap                 = 1.0f;

		public SerializableUnwrapParam          uvGenerationSettings;
#endif

		public void Reset()
        {
            lightProbeProxyVolumeOverride   = null;
            probeAnchor                     = null;
            motionVectorGenerationMode		= MotionVectorGenerationMode.Object;
            reflectionProbeUsage			= ReflectionProbeUsage.BlendProbes;
            lightProbeUsage					= LightProbeUsage.BlendProbes;
            allowOcclusionWhenDynamic		= true;
            renderingLayerMask              = ~(uint)0;
            receiveGI                       = ReceiveGI.LightProbes;
#if UNITY_EDITOR
    		lightmapParameters				= new UnityEditor.LightmapParameters();
            importantGI						= false;
            optimizeUVs						= false;
            ignoreNormalsForChartDetection  = false;
            scaleInLightmap                 = 1.0f;
            autoUVMaxDistance				= 0.5f;
            autoUVMaxAngle					= 89;
            minimumChartSize				= 4;
            stitchLightmapSeams				= false;
            
            if (uvGenerationSettings == null)
            {
                uvGenerationSettings = new SerializableUnwrapParam();
				UnityEditor.UnwrapParam.SetDefaults(out var defaults);
				uvGenerationSettings.angleError = defaults.angleError;
                uvGenerationSettings.areaError = defaults.areaError;
                uvGenerationSettings.hardAngle = defaults.hardAngle;
                uvGenerationSettings.packMarginPixels = defaults.packMargin * 256;
            }
#endif
        }
    }


    [ExecuteInEditMode, HelpURL(kDocumentationBaseURL + kNodeTypeName + kDocumentationExtension)]
    [DisallowMultipleComponent, AddComponentMenu("Chisel/" + kNodeTypeName)]
    public sealed class ChiselModelComponent : ChiselNodeComponent
    {
        public const string kRenderSettingsName           = nameof(renderSettings);
        public const string kColliderSettingsName         = nameof(colliderSettings);
        public const string kCreateRenderComponentsName   = nameof(CreateRenderComponents);
        public const string kCreateColliderComponentsName = nameof(CreateColliderComponents);
        public const string kAutoRebuildUVsName           = nameof(AutoRebuildUVs);
        public const string kVertexChannelMaskName        = nameof(VertexChannelMask);
        public const string kSubtractiveEditingName       = nameof(SubtractiveEditing);
        public const string kSmoothNormalsName            = nameof(SmoothNormals);
        public const string kSmoothingAngleName           = nameof(SmoothingAngle);
        public const string kDebugLogBrushesName          = nameof(DebugLogBrushes);


        public const string kNodeTypeName = "Model";
        public override string ChiselNodeTypeName { get { return kNodeTypeName; } }

        
        public ChiselGeneratedRenderSettings RenderSettings { get { return renderSettings; } }
        public ChiselGeneratedColliderSettings ColliderSettings { get { return colliderSettings; } }        
        public override CSGTreeNode TopTreeNode { get { return Node; } protected set { Node = (CSGTree)value; } }
		public override bool IsContainer { get { return IsActive; } }
		public bool IsInitialized  { get; private set; } = false;
		public bool IsDefaultModel { get; internal set; } = false;


		[HideInInspector] public ChiselGeneratedObjects generated;
		[HideInInspector] public CSGTree Node;

        public ChiselGeneratedColliderSettings colliderSettings;
        public ChiselGeneratedRenderSettings renderSettings;


        // TODO: put all bools in flags (makes it harder to work with in the ModelEditor though)
		public bool               CreateRenderComponents   = true;
        public bool               CreateColliderComponents = true;
        public bool               AutoRebuildUVs           = true;
        public VertexChannelFlags VertexChannelMask        = VertexChannelFlags.All;
        public bool               SubtractiveEditing      = false;
        public bool               SmoothNormals           = false;
        [Range(0, 180)]
        public float              SmoothingAngle          = 45.0f;
        [NonSerialized]          bool prevSubtractiveEditing;
        [NonSerialized]          bool prevSmoothNormals;
        [NonSerialized]          float prevSmoothingAngle;

        #region Debug
        // When enabled all brush geometry will be printed out to the console
        // at the start of the CSG job update
        public bool DebugLogBrushes = false;
        #endregion

        
        public ChiselModelComponent() : base() { }


        // Will show a warning icon in hierarchy when generator has a problem (do not make this method slow, it is called a lot!)
		public override void GetMessages(IChiselMessageHandler messages)
        {
            // TODO: improve warning messages
            const string kModelHasNoChildrenMessage   = kNodeTypeName + " has no children and will not have an effect";
            const string kFailedToGenerateNodeMessage = "Failed to generate internal representation of " + kNodeTypeName + "\nThe model might not have any (active) brushes inside it.";
		    //const string kChildNodesWithProblems    = kNodeTypeName + " has children with errors";

            if (!Node.Valid)
                messages.Warning(kFailedToGenerateNodeMessage);

            // A model makes no sense without any children
            if (transform.childCount == 0)
            { 
                messages.Warning(kModelHasNoChildrenMessage);
            } else
			{
                if (transform.GetComponentInChildren<ChiselNodeComponent>() == null)
				{
					messages.Warning(kModelHasNoChildrenMessage);
				}
			}

            //if (invalidChildNodes.Count > 0)
			//	messages.Warning(kChildNodesWithProblems);
		}

        public override void SetDirty() { if (Node.Valid) Node.SetDirty(); }

        protected override void OnCleanup()
        {
            if (generated != null)
            {
                if (!this && generated.generatedDataContainer)
                    generated.DestroyWithUndo();
            }
            base.OnCleanup();
        }

        internal override CSGTreeNode RebuildTreeNodes()
        {
            if (Node.Valid)
                Debug.LogWarning($"{nameof(ChiselModelComponent)} already has a treeNode, but trying to create a new one?", this);
            var instanceID = GetInstanceID();
            Node = CSGTree.Create(instanceID: instanceID);
            return Node;
        }

        protected override void OnValidateState()
        {
            base.OnValidateState();

            if (prevSubtractiveEditing != SubtractiveEditing && generated != null)
            {
                FlipGeneratedMeshes();
                prevSubtractiveEditing = SubtractiveEditing;
            }

            if ((prevSmoothNormals != SmoothNormals || !Mathf.Approximately(prevSmoothingAngle, SmoothingAngle)) && generated != null)
            {
                SmoothGeneratedMeshes();
                prevSmoothNormals   = SmoothNormals;
                prevSmoothingAngle = SmoothingAngle;
            }
        }
        
        public override void OnInitialize()
        {
            base.OnInitialize();

			if (colliderSettings == null)
			{
				colliderSettings = new ChiselGeneratedColliderSettings();
				colliderSettings.Reset();
			}

			if (renderSettings == null)
			{
				renderSettings = new ChiselGeneratedRenderSettings();
				renderSettings.Reset();
			}

			if (generated != null &&
                !generated.generatedDataContainer)
                generated.Destroy();

            generated ??= ChiselGeneratedObjects.Create(gameObject);

            // TODO: move out of here
#if !UNITY_EDITOR
            if (generated != null && generated.meshRenderers != null)
            {
                foreach(var renderable in generated.renderables)
                {                    
                    renderable.meshRenderer.forceRenderingOff = true;
                    renderable.meshRenderer.enabled = renderable.sharedMesh.vertexCount == 0;
                }
            }
#endif

            // Legacy solution
            if (!IsDefaultModel &&
                name == ChiselModelManager.kGeneratedDefaultModelName)
                IsDefaultModel = true;

            prevSubtractiveEditing = SubtractiveEditing;
            prevSmoothNormals   = SmoothNormals;
            prevSmoothingAngle = SmoothingAngle;
            IsInitialized = true;
        }

        void FlipGeneratedMeshes()
        {
            if (generated == null)
                return;

            if (generated.renderables != null)
            {
                foreach (var renderable in generated.renderables)
                    if (renderable != null && renderable.sharedMesh)
                        ChiselMeshUtility.FlipNormals(renderable.sharedMesh);
            }

            if (generated.debugVisualizationRenderables != null)
            {
                foreach (var renderable in generated.debugVisualizationRenderables)
                    if (renderable != null && renderable.sharedMesh)
                        ChiselMeshUtility.FlipNormals(renderable.sharedMesh);
            }

            if (generated.colliders != null)
            {
                foreach (var collider in generated.colliders)
                    if (collider != null && collider.sharedMesh)
                        ChiselMeshUtility.FlipNormals(collider.sharedMesh);
            }
        }

        void SmoothGeneratedMeshes()
        {
            if (generated == null)
                return;

            float angle = SmoothNormals ? SmoothingAngle : 0.0f;

            if (generated.renderables != null)
            {
                foreach (var renderable in generated.renderables)
                    if (renderable != null && renderable.sharedMesh)
                        ChiselMeshUtility.SmoothNormals(renderable.sharedMesh, angle);
            }

            if (generated.debugVisualizationRenderables != null)
            {
                foreach (var renderable in generated.debugVisualizationRenderables)
                    if (renderable != null && renderable.sharedMesh)
                        ChiselMeshUtility.SmoothNormals(renderable.sharedMesh, angle);
            }

            if (generated.colliders != null)
            {
                foreach (var collider in generated.colliders)
                    if (collider != null && collider.sharedMesh)
                        ChiselMeshUtility.SmoothNormals(collider.sharedMesh, angle);
            }
        }

#if UNITY_EDITOR
        // TODO: remove from here, shouldn't be public
		public MaterialPropertyBlock materialPropertyBlock;

        public VisibilityState VisibilityState
		{
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get { return generated.visibilityState; } 
        }
#endif
    }
}
