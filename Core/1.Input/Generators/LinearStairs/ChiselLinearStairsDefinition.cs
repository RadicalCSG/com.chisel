using System;
using Unity.Collections;
using Unity.Mathematics;
using Unity.Burst;
using UnityEngine;
using Unity.Entities;

namespace Chisel.Core
{
    [Serializable]
    public enum StairsRiserType : byte
    {
        None,
        ThinRiser,
        ThickRiser,
//		Pyramid,
        Smooth,
        FillDown
    }

    [Serializable]
    public enum StairsSideType : byte
    {
        None,
        // TODO: better names
        Down,
        Up,
        DownAndUp
    }

    [Serializable]
    public struct ChiselLinearStairs : IBranchGenerator
    {
        // TODO: set defaults using attributes?
        readonly static ChiselLinearStairs kDefaultSettings = new()
		{
            stepHeight		= 0.20f,
            stepDepth		= 0.20f,
            treadHeight	    = 0.02f,
            nosingDepth	    = 0.02f,
            nosingWidth	    = 0.01f,

            Width			= 1,
            Height			= 1,
            Depth			= 1,

            plateauHeight	= 0,

            riserType		= StairsRiserType.ThinRiser,
            leftSide		= StairsSideType.None,
            rightSide		= StairsSideType.None,
            riserDepth		= 0.05f,
            sideDepth		= 0.125f,
            sideWidth		= 0.125f,
            sideHeight		= 0.5f
        };
		public static ref readonly ChiselLinearStairs DefaultSettings => ref kDefaultSettings;

		// TODO: add all spiral stairs improvements to linear stairs

		public MinMaxAABB bounds;

        [DistanceValue] public float	stepHeight;
        [DistanceValue] public float	stepDepth;

        [DistanceValue] public float	treadHeight;

        [DistanceValue] public float	nosingDepth;
        [DistanceValue] public float	nosingWidth;

        [DistanceValue] public float    plateauHeight;

        public StairsRiserType          riserType;
        [DistanceValue] public float	riserDepth;

        public StairsSideType           leftSide;
        public StairsSideType           rightSide;
        
        [DistanceValue] public float	sideWidth;
        [DistanceValue] public float	sideHeight;
        [DistanceValue] public float	sideDepth;

        //[NamedItems("Top", "Bottom", "Left", "Right", "Front", "Back", "Tread", "Step", overflow = "Side {0}", fixedSize = 8)]
        //public ChiselSurfaceDefinition  surfaceDefinition;

        #region Properties

        const float kStepSmudgeValue = BrushMeshFactory.LineairStairsData.kStepSmudgeValue;


        public float	Width  { get { return BoundsSize.x; } set { var size = BoundsSize; size.x = value; BoundsSize = size; } }
        public float	Height { get { return BoundsSize.y; } set { var size = BoundsSize; size.y = value; BoundsSize = size; } }
        public float	Depth  { get { return BoundsSize.z; } set { var size = BoundsSize; size.z = value; BoundsSize = size; } }

		public float3 BoundsSize
		{
			get { return bounds.Max - bounds.Min; }
			set { bounds.SetExtents(math.abs(value) * 0.5f); }
		}

		public float3 Center
        {
            get { return (bounds.Max + bounds.Min) * 0.5f; }
            set { bounds.SetCenter(value); }
        }
        
        public float3   BoundsMin   { get { return math.min(bounds.Min, bounds.Max); } }
        public float3   BoundsMax   { get { return math.max(bounds.Min, bounds.Max); } }
        
        public float	AbsWidth  { get { return math.abs(BoundsSize.x); } }
        public float	AbsHeight { get { return math.abs(BoundsSize.y); } }
        public float	AbsDepth  { get { return math.abs(BoundsSize.z); } }

        public int StepCount
        {
            get
            {
                return math.max(1,
                            (int)math.floor((AbsHeight - plateauHeight + kStepSmudgeValue) / stepHeight));
            }
        }

        public float StepDepthOffset
        {
            get { return math.max(0, AbsDepth - (StepCount * stepDepth)); }
        }
        #endregion

        #region Generate
        public int PrepareAndCountRequiredBrushMeshes()
        {
            var size = BoundsSize;
            if (math.any(size == 0))
                return 0;

            var description = new BrushMeshFactory.LineairStairsData(bounds,
                                                                        stepHeight, stepDepth,
                                                                        treadHeight,
                                                                        nosingDepth, nosingWidth,
                                                                        plateauHeight,
                                                                        riserType, riserDepth,
                                                                        leftSide, rightSide,
                                                                        sideWidth, sideHeight, sideDepth);
            return description.subMeshCount;
        }

        public readonly bool GenerateNodes(BlobAssetReference<InternalChiselSurfaceArray> surfaceDefinitionBlob, 
                                           NativeList<GeneratedNode> nodes, Allocator allocator = Allocator.Persistent)// Indirect
		{
            NativeList<BlobAssetReference<BrushMeshBlob>> generatedBrushMeshes;
			using var _generatedBrushMeshes = generatedBrushMeshes = new NativeList<BlobAssetReference<BrushMeshBlob>>(nodes.Length, Allocator.Temp);

            generatedBrushMeshes.Resize(nodes.Length, NativeArrayOptions.ClearMemory);
            var description = new BrushMeshFactory.LineairStairsData(bounds,
                                                                    stepHeight, stepDepth,
                                                                    treadHeight,
                                                                    nosingDepth, nosingWidth,
                                                                    plateauHeight,
                                                                    riserType, riserDepth,
                                                                    leftSide, rightSide,
                                                                    sideWidth, sideHeight, sideDepth);
            const int subMeshOffset = 0;
            if (!BrushMeshFactory.GenerateLinearStairsSubMeshes(generatedBrushMeshes,
                                                                subMeshOffset,
                                                                in description,
                                                                in surfaceDefinitionBlob,
                                                                allocator))// Indirect
			{
                for (int i = 0; i < generatedBrushMeshes.Length; i++)
                {
                    if (generatedBrushMeshes[i].IsCreated)
                        generatedBrushMeshes[i].Dispose();
                    generatedBrushMeshes[i] = default;
                }
                return false;
            }
            for (int i = 0; i < generatedBrushMeshes.Length; i++)
                nodes[i] = GeneratedNode.GenerateBrush(generatedBrushMeshes[i]);
            
            return true;
        }

        public readonly void Dispose() {}
        #endregion

        #region Surfaces
        public enum SurfaceSides : byte
        {
            Top     = 0,
            Bottom  = 1,
            Left    = 2,
            Right   = 3,
            Front   = 4,
            Back    = 5,
            Tread   = 6,
            Step    = 7,

            TotalSides
        }


        [BurstDiscard]
        public int RequiredSurfaceCount { get { return (int)SurfaceSides.TotalSides; } }

        [BurstDiscard]
        public readonly void UpdateSurfaces(ref ChiselSurfaceArray surfaceDefinition)
        {
            var surfaces = surfaceDefinition.surfaces;
            if (!surfaces[(int)SurfaceSides.Top   ].HasMaterial) surfaces[(int)SurfaceSides.Top   ].SetMaterial(ChiselProjectSettings.DefaultFloorMaterial);
            if (!surfaces[(int)SurfaceSides.Bottom].HasMaterial) surfaces[(int)SurfaceSides.Bottom].SetMaterial(ChiselProjectSettings.DefaultFloorMaterial);
            if (!surfaces[(int)SurfaceSides.Left  ].HasMaterial) surfaces[(int)SurfaceSides.Left  ].SetMaterial(ChiselProjectSettings.DefaultWallMaterial);
            if (!surfaces[(int)SurfaceSides.Right ].HasMaterial) surfaces[(int)SurfaceSides.Right ].SetMaterial(ChiselProjectSettings.DefaultWallMaterial);
            if (!surfaces[(int)SurfaceSides.Front ].HasMaterial) surfaces[(int)SurfaceSides.Front ].SetMaterial(ChiselProjectSettings.DefaultWallMaterial);
            if (!surfaces[(int)SurfaceSides.Back  ].HasMaterial) surfaces[(int)SurfaceSides.Back  ].SetMaterial(ChiselProjectSettings.DefaultWallMaterial);
            if (!surfaces[(int)SurfaceSides.Tread ].HasMaterial) surfaces[(int)SurfaceSides.Tread ].SetMaterial(ChiselProjectSettings.DefaultTreadMaterial);
            if (!surfaces[(int)SurfaceSides.Step  ].HasMaterial) surfaces[(int)SurfaceSides.Step  ].SetMaterial(ChiselProjectSettings.DefaultStepMaterial);

            for (int i = 0; i < surfaceDefinition.surfaces.Length; i++)
            {
                if (!surfaceDefinition.surfaces[i].HasMaterial)
                    surfaceDefinition.surfaces[i].SetMaterial(ChiselProjectSettings.DefaultWallMaterial);
            }
        }
        #endregion

        #region Validation
        public const float	kMinStepHeight			= 0.01f;
        public const float	kMinStepDepth			= 0.01f;
        public const float  kMinRiserDepth          = 0.01f;
        public const float  kMinSideWidth			= 0.01f;
        public const float	kMinWidth				= 0.0001f;

        public bool Validate()
        {
            stepHeight		= math.max(kMinStepHeight, stepHeight);
            stepDepth		= math.clamp(stepDepth, kMinStepDepth, AbsDepth);
            treadHeight	    = math.max(0, treadHeight);
            nosingDepth	    = math.max(0, nosingDepth);
            nosingWidth	    = math.max(0, nosingWidth);

            Width			= math.max(kMinWidth, AbsWidth) * (Width < 0 ? -1 : 1);
            Depth			= math.max(stepDepth, AbsDepth) * (Depth < 0 ? -1 : 1);

            riserDepth		= math.max(kMinRiserDepth, riserDepth);
            sideDepth		= math.max(0, sideDepth);
            sideWidth		= math.max(kMinSideWidth, sideWidth);
            sideHeight		= math.max(0, sideHeight);

            var realHeight          = math.max(stepHeight, AbsHeight);
            var maxPlateauHeight    = realHeight - stepHeight;

            plateauHeight	= math.clamp(plateauHeight, 0, maxPlateauHeight);

            var totalSteps          = math.max(1, (int)math.floor((realHeight - plateauHeight + ChiselLinearStairs.kStepSmudgeValue) / stepHeight));
            var totalStepHeight     = totalSteps * stepHeight;

            plateauHeight	= math.max(0, realHeight - totalStepHeight);
            stepDepth		= math.clamp(stepDepth, kMinStepDepth, AbsDepth / totalSteps);
            return true;
        }

        [BurstDiscard]
        public void GetWarningMessages(IChiselMessageHandler messages) { }
        #endregion

        #region Reset
        public void Reset() { this = DefaultSettings; }
        #endregion
    }

    // https://www.archdaily.com/892647/how-to-make-calculations-for-staircase-designs
    // https://inspectapedia.com/Stairs/2024s.jpg
    // https://landarchbim.com/2014/11/18/stair-nosing-treads-and-stringers/
    // https://en.wikipedia.org/wiki/Stairs
    [Serializable]
    public class ChiselLinearStairsDefinition : SerializedBranchGenerator<ChiselLinearStairs>
    {
        public const string kNodeTypeName = "Linear Stairs";

        //[NamedItems("Top", "Bottom", "Left", "Right", "Front", "Back", "Tread", "Step", overflow = "Side {0}", fixedSize = 8)]
        //public ChiselSurfaceDefinition  surfaceDefinition;

        #region OnEdit
        //
        // TODO: code below needs to be cleaned up & simplified 
        //

        public override void OnEdit(IChiselHandles handles)
        {
            {
                var stepDepthOffset = settings.StepDepthOffset;
                var stepHeight      = settings.stepHeight;
                var stepCount       = settings.StepCount;
                var bounds          = settings.bounds;

                var steps		    = handles.moveSnappingSteps;
                steps.y			    = stepHeight;

                if (handles.DoBoundsHandle(ref bounds, snappingSteps: steps))
                    settings.bounds = bounds;

                var min			= math.min(bounds.Min, bounds.Max);
                var max			= math.min(bounds.Min, bounds.Max);

                var size        = (max - min);

                var heightStart = bounds.Max.y + (size.y < 0 ? size.y : 0);

                var edgeHeight  = heightStart - stepHeight * stepCount;
                var pHeight0	= new Vector3(min.x, edgeHeight, max.z);
                var pHeight1	= new Vector3(max.x, edgeHeight, max.z);

                var depthStart = bounds.Min.z - (size.z < 0 ? size.z : 0);

                var pDepth0		= new Vector3(min.x, max.y, depthStart + stepDepthOffset);
                var pDepth1		= new Vector3(max.x, max.y, depthStart + stepDepthOffset);

                if (handles.DoTurnHandle(ref bounds))
                    settings.bounds = bounds;

                if (handles.DoEdgeHandle1D(out edgeHeight, Axis.Y, pHeight0, pHeight1, snappingStep: stepHeight))
                {
                    var totalStepHeight = math.clamp((heightStart - edgeHeight), size.y % stepHeight, size.y);
                    const float kSmudgeValue = 0.0001f;
                    var oldStepCount = settings.StepCount;
                    var newStepCount = math.max(1, (int)math.floor((math.abs(totalStepHeight) + kSmudgeValue) / stepHeight));

                    settings.stepDepth     = (oldStepCount * settings.stepDepth) / newStepCount;
                    settings.plateauHeight = size.y - (stepHeight * newStepCount);
                }

                if (handles.DoEdgeHandle1D(out stepDepthOffset, Axis.Z, pDepth0, pDepth1, snappingStep: ChiselLinearStairs.kMinStepDepth))
                {
                    stepDepthOffset -= depthStart;
                    stepDepthOffset = math.clamp(stepDepthOffset, 0, settings.AbsDepth - ChiselLinearStairs.kMinStepDepth);
                    settings.stepDepth = ((settings.AbsDepth - stepDepthOffset) / settings.StepCount);
                }

                float heightOffset;
                var prevModified = handles.modified;
                {
                    var direction = Vector3.Cross(Vector3.forward, pHeight0 - pDepth0).normalized;
                    handles.DoEdgeHandle1DOffset(out var height0vec, Axis.Y, pHeight0, pDepth0, direction, snappingStep: stepHeight);
                    handles.DoEdgeHandle1DOffset(out var height1vec, Axis.Y, pHeight1, pDepth1, direction, snappingStep: stepHeight);
                    var height0 = Vector3.Dot(direction, height0vec);
                    var height1 = Vector3.Dot(direction, height1vec);
                    if (math.abs(height0) > math.abs(height1)) heightOffset = height0; else heightOffset = height1;
                }
                if (prevModified != handles.modified)
                {
                    settings.plateauHeight += heightOffset;
                }
            }
        }
        #endregion
    }
}