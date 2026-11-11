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
        [InspectorName("None")] None,
        [InspectorName("Thin Risers")] ThinRiser,
        [InspectorName("Thick Risers")] ThickRiser,
//		Pyramid,
        [InspectorName("Sloped Underside")] Smooth,
        [InspectorName("Solid To The Floor")] FillDown
    }

    [Serializable]
    public enum StairsSideType : byte
    {
        [InspectorName("None")] None,
        [InspectorName("Below The Steps")] Down,
        [InspectorName("Above The Steps")] Up,
        [InspectorName("Both")] DownAndUp
    }

    // How far back the guard rails go beside the floor at the top of the stairs
    [Serializable]
    public enum StairsTopSideType : byte
    {
        [InspectorName("To The Back")] Full,
        [InspectorName("None")] None,
        [InspectorName("Set Length")] Length
    }

    [Serializable]
    public struct ChiselLinearStairs : IBranchGenerator, IChiselSurfaceNames, IChiselGeneratorParts, IChiselListedSurfaces
    {
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

		public MinMaxAABB bounds;

        [Tooltip("How high each step rises above the one below it")]
        [DistanceValue] public float	stepHeight;
        [Tooltip("How deep each step is, front to back")]
        [DistanceValue] public float	stepDepth;

        [ConditionalName("Tread Thickness"), Tooltip("How thick the slab on top of each step is; 0 for none")]
        [DistanceValue] public float	treadHeight;

        [ConditionalName("Tread Front Overhang"), Tooltip("How far each tread sticks out over the step below it")]
        [DistanceValue] public float	nosingDepth;
        [ConditionalName("Tread Side Overhang"), Tooltip("How far the treads stick out past a side without a wall")]
        [DistanceValue] public float	nosingWidth;

        [ConditionalName("Base Height"), Tooltip("How high the lowest step starts: the solid part under it. Stairs taller than their steps fit in get a higher base")]
        [DistanceValue] public float    plateauHeight;

        [ConditionalName("Under The Steps"), Tooltip("What is under the steps: nothing, a board at the back of each step, solid blocks, a sloped underside, or solid down to the floor")]
        public StairsRiserType          riserType;
        [ConditionalName("Riser Thickness"), Tooltip("How thick the board at the back of each step is")]
        [ConditionalHide(nameof(riserType), StairsRiserType.None, StairsRiserType.ThickRiser, StairsRiserType.Smooth, StairsRiserType.FillDown)]
        [DistanceValue] public float	riserDepth;

        [ConditionalName("Left"), Tooltip("A guard rail on the left side: below the steps, above them, or both")]
        public StairsSideType           leftSide;
        [ConditionalName("Right"), Tooltip("A guard rail on the right side: below the steps, above them, or both")]
        public StairsSideType           rightSide;
        
        [ConditionalHideIfAll(StairsSideType.None, nameof(leftSide), nameof(rightSide))]
        [ConditionalName("Thickness"), Tooltip("How thick the guard rails are")]
        [DistanceValue] public float	sideWidth;
        [ConditionalHideIfAll(StairsSideType.None, nameof(leftSide), nameof(rightSide))]
        [ConditionalName("Height Above"), Tooltip("How high a guard rail above the steps reaches over them")]
        [DistanceValue] public float	sideHeight;
        [ConditionalHideIfAll(StairsSideType.None, nameof(leftSide), nameof(rightSide))]
        [ConditionalName("Depth Below"), Tooltip("How far a guard rail below the steps reaches under them")]
        [DistanceValue] public float	sideDepth;
        [ConditionalHideIfAll(StairsSideType.None, nameof(leftSide), nameof(rightSide))]
        [ConditionalName("Front Offset"), Tooltip("How far the guard rails reach out past the steps' fronts")]
        [DistanceValue] public float	sideFrontOffset;
        [ConditionalHideIfAll(StairsSideType.None, nameof(leftSide), nameof(rightSide))]
        [ConditionalName("End"), Tooltip("How far back the guard rails go beside the floor at the top of the stairs")]
        public StairsTopSideType        topSide;
        [ConditionalHideIfAll(StairsSideType.None, nameof(leftSide), nameof(rightSide))]
        [ConditionalName("End Length"), Tooltip("How far back from the top step the guard rails go, when set")]
        [DistanceValue] public float	topSideLength;

        // how far back from the top step the guard rails go beside the top floor
        public readonly float TopSideLength => topSide switch
        {
            StairsTopSideType.None   => 0,
            StairsTopSideType.Length => math.max(0, topSideLength),
            _                        => float.PositiveInfinity
        };

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
        // the bounds with min below max on every axis, as the stairs are built (bounds keeps the sides as dragged)
        public readonly MinMaxAABB SortedBounds { get { return new MinMaxAABB { Min = math.min(bounds.Min, bounds.Max), Max = math.max(bounds.Min, bounds.Max) }; } }
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

        // How deep the flat part at the top is, from the back to where the steps start: the back moves, the steps stay as they are
        public void SetTopDepth(float topDepth)
        {
            var depth = StepCount * stepDepth + math.max(0, topDepth);
            var min   = bounds.Min;
            var max   = bounds.Max;
            if (min.z <= max.z) min.z = max.z - depth;
            else                max.z = min.z - depth;
            bounds.Min = min;
            bounds.Max = max;
        }
        #endregion

        #region Parts
        // the brushes are built in this order: the steps (and the shape under them), the treads, each side, the side
        // fronts, the base
        public enum Part { Step, Tread, LeftSide, RightSide, SideFront, Base }
        static readonly string[] kPartNames = { "Step", "Tread", "Left Side", "Right Side", "Side Front", "Base" };

        public readonly int PartRoleCount => kPartNames.Length;
        public readonly string GetPartRoleName(int role) { return role >= 0 && role < kPartNames.Length ? kPartNames[role] : null; }

        [BurstDiscard]
        public readonly int GetPartRole(int brushIndex)
        {
            var description = new BrushMeshFactory.LineairStairsData(SortedBounds,
                                                                    stepHeight, stepDepth,
                                                                    treadHeight,
                                                                    nosingDepth, nosingWidth,
                                                                    plateauHeight,
                                                                    riserType, riserDepth,
                                                                    leftSide, rightSide,
                                                                    sideWidth, sideHeight, sideDepth, sideFrontOffset: sideFrontOffset, topSideLength: TopSideLength);
            return (int)PartOf(in description, brushIndex);
        }

        /// <summary>The part the brush at this place of stairs built from a description is</summary>
        internal static Part PartOf(in BrushMeshFactory.LineairStairsData description, int brushIndex)
        {
            if (brushIndex < description.startTread)      return Part.Step;
            if (brushIndex < description.startLeftSide)   return Part.Tread;
            if (brushIndex < description.startRightSide)  return Part.LeftSide;
            if (brushIndex < description.startSideFronts) return Part.RightSide;
            if (brushIndex < description.startBase)       return Part.SideFront;
            return Part.Base;
        }
        #endregion

        #region Generate
        public int PrepareAndCountRequiredBrushMeshes()
        {
            var size = BoundsSize;
            if (math.any(size == 0))
                return 0;

            var description = new BrushMeshFactory.LineairStairsData(SortedBounds,
                                                                        stepHeight, stepDepth,
                                                                        treadHeight,
                                                                        nosingDepth, nosingWidth,
                                                                        plateauHeight,
                                                                        riserType, riserDepth,
                                                                        leftSide, rightSide,
                                                                        sideWidth, sideHeight, sideDepth, sideFrontOffset: sideFrontOffset, topSideLength: TopSideLength);
            return description.subMeshCount;
        }

        public readonly bool GenerateNodes(BlobAssetReference<InternalChiselSurfaceArray> surfaceDefinitionBlob, 
                                           NativeList<GeneratedNode> nodes, Allocator allocator = Allocator.Persistent)// Indirect
		{
            NativeList<BlobAssetReference<BrushMeshBlob>> generatedBrushMeshes;
			using var _generatedBrushMeshes = generatedBrushMeshes = new NativeList<BlobAssetReference<BrushMeshBlob>>(nodes.Length, Allocator.Temp);

            generatedBrushMeshes.Resize(nodes.Length, NativeArrayOptions.ClearMemory);
            var description = new BrushMeshFactory.LineairStairsData(SortedBounds,
                                                                    stepHeight, stepDepth,
                                                                    treadHeight,
                                                                    nosingDepth, nosingWidth,
                                                                    plateauHeight,
                                                                    riserType, riserDepth,
                                                                    leftSide, rightSide,
                                                                    sideWidth, sideHeight, sideDepth, sideFrontOffset: sideFrontOffset, topSideLength: TopSideLength);
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
        [BurstDiscard]
        public int RequiredSurfaceCount { get { return StairsSurfaces.kCount; } }

        [BurstDiscard]
        public readonly string GetSurfaceName(int index) { return StairsSurfaces.Name(index); }

        public readonly bool IsSurfaceAlwaysListed(int index) { return StairsSurfaces.IsAlwaysAPart(index); }

        [BurstDiscard]
        public readonly bool IsSurfaceUsed(int index)
        {
            return StairsSurfaces.IsUsed(index, haveTread: treadHeight >= 0.0001f, haveRiser: riserType != StairsRiserType.None,
                                         haveWall: leftSide != StairsSideType.None || rightSide != StairsSideType.None, haveColumn: false,
                                         haveBase: riserType != StairsRiserType.None && riserType != StairsRiserType.FillDown && plateauHeight > 0.001f);
        }

        [BurstDiscard]
        public readonly void UpdateSurfaces(ref ChiselSurfaceArray surfaceDefinition)
        {
            StairsSurfaces.UpdateSurfaces(ref surfaceDefinition);
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

            // a negative size stays as given, and a size is changed by moving its far side only (the side being dragged),
            // never 0
            var max         = bounds.Max;
            max.x           = bounds.Min.x + ChiselGeneratorLimits.NotZero(Width, kMinWidth);
            max.y           = bounds.Min.y + ChiselGeneratorLimits.NotZero(Height);
            max.z           = bounds.Min.z + math.max(stepDepth, AbsDepth) * (Depth < 0 ? -1 : 1);
            bounds.Max      = max;

            riserDepth		= math.max(kMinRiserDepth, riserDepth);
            sideDepth		= math.max(0, sideDepth);
            sideFrontOffset	= math.max(0, sideFrontOffset);
            sideWidth		= math.max(kMinSideWidth, sideWidth);
            sideHeight		= math.max(0, sideHeight);

            var realHeight          = math.max(stepHeight, AbsHeight);
            var maxPlateauHeight    = realHeight - stepHeight;

            plateauHeight	= math.clamp(plateauHeight, 0, maxPlateauHeight);

            var totalSteps          = math.max(1, (int)math.floor((realHeight - plateauHeight + ChiselLinearStairs.kStepSmudgeValue) / stepHeight));
            // no more steps than fit in the depth at their depth: taller than that, the steps keep their size and start
            // higher up (the base under the lowest step grows), rather than every step getting shallower
            var stepsThatFit        = math.max(1, (int)math.floor((AbsDepth + ChiselLinearStairs.kStepSmudgeValue) / stepDepth));
            totalSteps              = math.min(totalSteps, stepsThatFit);
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
        //

        // the lines dragged to change the base, the steps and the top: thicker than the others
        const float kThickLine = 5.0f;

        public override void OnEdit(IChiselHandles handles)
        {
            {
                var stepDepthOffset = settings.StepDepthOffset;
                var stepHeight      = settings.stepHeight;
                var stepCount       = settings.StepCount;
                var bounds          = settings.bounds;

                var steps		    = handles.moveSnappingSteps;
                steps.y			    = stepHeight;

                // the bottom stays: moving the stairs is the transform's, with the move gizmo there. A drag that would make them
                // flat isn't taken
                var boundsBefore = bounds;
                if (handles.DoBoundsHandle(ref bounds, snappingSteps: steps, fixedBottom: true))
                {
                    if (math.any(math.abs(bounds.Max - bounds.Min) < ChiselLinearStairs.kMinWidth))
                        bounds = boundsBefore;
                    settings.bounds = bounds;
                }

                var min			= math.min(bounds.Min, bounds.Max);
                var max			= math.max(bounds.Min, bounds.Max);

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

                // each side's profile as the pathed stairs draw theirs: whole where nothing is in front, dashed where something is
                {
                    var color  = handles.color;
                    var hidden = ChiselHandleColors.Hidden(color);
                    void Line(Vector3 from, Vector3 to)
                    {
                        handles.color = hidden;
                        handles.DrawLine(from, to, lineMode: LineMode.NoZTest, thickness: 0.5f, dashSize: 2.0f);
                        handles.color = color;
                        handles.DrawLine(from, to, lineMode: LineMode.ZTest, thickness: 1.0f);
                    }
                    foreach (var x in new[] { min.x, max.x })
                    {
                        var floorFront = new Vector3(x, min.y, max.z);
                        var baseFront  = new Vector3(x, edgeHeight, max.z);
                        var slopeTop   = new Vector3(x, max.y, depthStart + stepDepthOffset);
                        var backTop    = new Vector3(x, max.y, depthStart);
                        var backFloor  = new Vector3(x, min.y, depthStart);
                        Line(floorFront, baseFront);
                        Line(baseFront, slopeTop);
                        Line(slopeTop, backTop);
                        Line(backTop, backFloor);
                        Line(backFloor, floorFront);
                    }
                    handles.color = color;
                }

                if (handles.DoEdgeHandle1D(out edgeHeight, Axis.Y, pHeight0, pHeight1, snappingStep: stepHeight, thickness: kThickLine))
                {
                    var totalStepHeight = math.clamp((heightStart - edgeHeight), size.y % stepHeight, size.y);
                    const float kSmudgeValue = 0.0001f;
                    var oldStepCount = settings.StepCount;
                    var newStepCount = math.max(1, (int)math.floor((math.abs(totalStepHeight) + kSmudgeValue) / stepHeight));

                    settings.stepDepth     = (oldStepCount * settings.stepDepth) / newStepCount;
                    settings.plateauHeight = size.y - (stepHeight * newStepCount);
                }
                var baseFocused = handles.lastHandleHadFocus;

                if (handles.DoEdgeHandle1D(out stepDepthOffset, Axis.Z, pDepth0, pDepth1, snappingStep: ChiselLinearStairs.kMinStepDepth, thickness: kThickLine))
                {
                    stepDepthOffset -= depthStart;
                    stepDepthOffset = math.clamp(stepDepthOffset, 0, settings.AbsDepth - ChiselLinearStairs.kMinStepDepth);
                    settings.stepDepth = ((settings.AbsDepth - stepDepthOffset) / settings.StepCount);
                }
                var topFocused = handles.lastHandleHadFocus;

                float heightOffset;
                var prevModified = handles.modified;
                {
                    var direction = Vector3.Cross(Vector3.forward, pHeight0 - pDepth0).normalized;
                    handles.DoEdgeHandle1DOffset(out var height0vec, Axis.Y, pHeight0, pDepth0, direction, snappingStep: stepHeight, thickness: kThickLine);
                    handles.DoEdgeHandle1DOffset(out var height1vec, Axis.Y, pHeight1, pDepth1, direction, snappingStep: stepHeight, thickness: kThickLine);
                    var height0 = Vector3.Dot(direction, height0vec);
                    var height1 = Vector3.Dot(direction, height1vec);
                    if (math.abs(height0) > math.abs(height1)) heightOffset = height0; else heightOffset = height1;
                }
                if (prevModified != handles.modified)
                {
                    // direction points away from where the diagonal is dragged
                    settings.plateauHeight -= heightOffset;
                }
                var sorted = settings.SortedBounds;
                handles.DoMoveGizmo(new Vector3((sorted.Min.x + sorted.Max.x) * 0.5f, sorted.Min.y, (sorted.Min.z + sorted.Max.z) * 0.5f));

                // with the base or the top's line hovered, only what it changes; not while a measurement is typed in
                var editing = handles.isEditingMeasurement;
                if (baseFocused && !editing)
                {
                    var typed = settings.plateauHeight;
                    if (handles.DoDistanceMeasurement(ref typed, new Vector3(min.x, min.y, max.z), pHeight0, name: "Base Height"))
                        settings.plateauHeight = math.clamp(typed, 0, size.y - stepHeight);
                    return;
                }
                if (topFocused && !editing)
                {
                    var typed = settings.StepDepthOffset;
                    if (typed > 0 && handles.DoDistanceMeasurement(ref typed, new Vector3(min.x, max.y, depthStart), pDepth0, name: "Top Depth"))
                        settings.SetTopDepth(typed);
                    return;
                }

                boundsBefore = settings.bounds;
                if (handles.DoBoxMeasurements(ref settings.bounds) &&
                    math.any(math.abs(settings.bounds.Max - settings.bounds.Min) < ChiselLinearStairs.kMinWidth))
                    settings.bounds = boundsBefore;
            }
        }
        #endregion
    }
}