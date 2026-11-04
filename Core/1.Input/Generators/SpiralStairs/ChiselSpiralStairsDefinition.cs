using System;
using Unity.Collections;
using Unity.Mathematics;
using Unity.Burst;
using UnityEngine;
using Unity.Entities;

namespace Chisel.Core
{
    [Serializable]
    public enum SpiralStairsCenterType : byte
    {
        [InspectorName("Open")] Open,
        [InspectorName("Solid Column")] Solid,
        [InspectorName("Hollow Column")] Hollow
    }

    [Serializable]
    public struct ChiselSpiralStairs : IBranchGenerator
    {
        readonly static ChiselSpiralStairs kDefaultSettings = new()
        {
            origin		    = float3.zero,

            stepHeight	    = 0.20f,                          

            treadHeight     = 0.02f,
            nosingDepth	    = 0.02f,
            nosingWidth	    = 0.01f,

            innerDiameter   = 0.25f,
            outerDiameter   = 2,
            height		    = 1,

            startAngle	    = 0,
            rotation	    = 180,

            innerSegments   = 8,
            outerSegments   = 16,

            riserType	    = StairsRiserType.ThickRiser,
            riserDepth	    = 0.03f,

            bottomSmoothingGroup    = 1,

            centerType      = SpiralStairsCenterType.Open,
            columnSmoothingGroup = 2,
            sideSmoothingGroup   = 3,
            columnThickness = 0.1f,

            innerSide       = StairsSideType.None,
            outerSide       = StairsSideType.None,
            sideWidth       = 0.15f,
            sideHeight      = 1.0f,
            sideDepth       = 0.2f
        };
		public static ref readonly ChiselSpiralStairs DefaultSettings => ref kDefaultSettings;

		// TODO: expose this to user
		const int smoothSubDivisions = 3;

        // how far above the object's position the bottom of the stairs is, set by dragging the bottom handle; only its
        // height is used: moving the stairs sideways is what their transform is for
        [Tooltip("How far above the object's position the bottom of the stairs is; dragging the bottom handle changes it")]
        [DistanceValue] public float3   origin;
        [Tooltip("How high the stairs are, from the floor to the top step")]
        [DistanceValue] public float	height;
        [Tooltip("How wide the stairs are, across the outside of the steps")]
        [DistanceValue] public float    outerDiameter;
        [Tooltip("How wide the open space in the middle is; 0 for steps that meet in the center")]
        [DistanceValue] public float    innerDiameter;
        [Tooltip("How high each step rises above the one below it")]
        [DistanceValue] public float    stepHeight;
        [ConditionalName("Tread Thickness"), Tooltip("How thick the slab on top of each step is; 0 for none. There is no tread without a front overhang")]
        [DistanceValue] public float    treadHeight;
        [ConditionalName("Tread Front Overhang"), Tooltip("How far each tread sticks out over the step below it; 0 for no tread")]
        [DistanceValue] public float    nosingDepth;
        [ConditionalName("Tread Side Overhang"), Tooltip("How far the treads stick out past the outside of the steps, and into the open middle")]
        [DistanceValue] public float    nosingWidth;
        [ConditionalName("Riser Thickness"), Tooltip("How thick the board at the back of each step is")]
        [ConditionalHide(nameof(riserType), StairsRiserType.None, StairsRiserType.ThickRiser, StairsRiserType.Smooth, StairsRiserType.FillDown)]
        [DistanceValue] public float    riserDepth;
        [Tooltip("Where the lowest step is around the center, in degrees")]
        [AngleValue   ] public float    startAngle;
        [ConditionalName("Turn Angle"), Tooltip("How far the stairs turn from the lowest step to the top one, in degrees; more than 360 goes round more than once")]
        [AngleValue   ] public float    rotation; // can be >360 degrees
        [ConditionalName("Inner Segments"), Tooltip("How many straight pieces make up the round open middle")]
        public int					    innerSegments;
        [ConditionalName("Outer Segments"), Tooltip("How many straight pieces make up the round outside")]
        public int					    outerSegments;
        [ConditionalName("Under The Steps"), Tooltip("What is under the steps: nothing, a board at the back of each step, solid blocks, a sloped underside, or solid down to the floor")]
        public StairsRiserType		    riserType;

        [ConditionalName("Underside Smoothing Group"), Tooltip("The smoothing group of the sloped underside, so it shades as one curved surface; 0 to show its flat pieces")]
        [ConditionalHide(nameof(riserType), StairsRiserType.None, StairsRiserType.ThinRiser, StairsRiserType.ThickRiser, StairsRiserType.FillDown)]
        public uint					    bottomSmoothingGroup;

        [ConditionalName("Center"), Tooltip("What is in the center the steps turn around: open, a solid column, or a hollow column with a wall as thick as set. With an inner diameter of 0 the steps meet in the center and there is nothing there")]
        public SpiralStairsCenterType   centerType;
        [ConditionalName("Column Wall Thickness"), Tooltip("How thick the hollow column's wall is")]
        [ConditionalHide(nameof(centerType), SpiralStairsCenterType.Open, SpiralStairsCenterType.Solid)]
        [DistanceValue] public float    columnThickness;
        [ConditionalName("Column Smoothing Group"), Tooltip("The smoothing group of the column's round sides, so they shade as one curved surface; 0 to show its flat pieces")]
        [ConditionalHide(nameof(centerType), SpiralStairsCenterType.Open)]
        public uint                     columnSmoothingGroup;

        [Tooltip("A wall along the outside of the steps: below them, above them (a railing wall), or both")]
        public StairsSideType           outerSide;
        [Tooltip("A wall along the open middle: below the steps, above them (a railing wall), or both. Needs an inner diameter above 0")]
        public StairsSideType           innerSide;

        [ConditionalHideIfAll(StairsSideType.None, nameof(outerSide), nameof(innerSide))]
        [ConditionalName("Side Wall Thickness"), Tooltip("How thick the side walls are")]
        [DistanceValue] public float	sideWidth;
        [ConditionalHideIfAll(StairsSideType.None, nameof(outerSide), nameof(innerSide))]
        [ConditionalName("Side Wall Height Above The Steps"), Tooltip("How high a wall above the steps reaches over them")]
        [DistanceValue] public float	sideHeight;
        [ConditionalHideIfAll(StairsSideType.None, nameof(outerSide), nameof(innerSide))]
        [ConditionalName("Side Wall Depth Below The Steps"), Tooltip("How far a wall below the steps reaches under them")]
        [DistanceValue] public float	sideDepth;
        [ConditionalHideIfAll(StairsSideType.None, nameof(outerSide), nameof(innerSide))]
        [ConditionalName("Side Wall Smoothing Group"), Tooltip("The smoothing group of the side walls' round faces, so they shade as one curved surface; 0 to show their flat pieces")]
        public uint                     sideSmoothingGroup;

        #region Properties

        const float kSmudgeValue = 0.0001f;

        public int StepCount
        {
            get
            {
                return math.max(1, (int)math.floor((math.abs(height) + kSmudgeValue) / stepHeight));
            }
        }
        
        public float AnglePerStep
        {
            get
            {
                return rotation / StepCount;
            }
        }

        const float kEpsilon = 0.001f;

        internal bool HaveInnerCylinder => (innerDiameter >= kEpsilon);
        internal bool HaveTread => (nosingDepth < kEpsilon) ? false : (treadHeight >= kEpsilon);

        internal int CylinderSubMeshCount => HaveInnerCylinder ? 2 : 1;
        internal int SubMeshPerRiser => (riserType == StairsRiserType.None) ? 0 :
                                               (riserType == StairsRiserType.Smooth) ? (2 * smoothSubDivisions)
                                                                            : 1;
        internal int RiserSubMeshCount => (StepCount * SubMeshPerRiser) + ((riserType == StairsRiserType.None) ? 0 : CylinderSubMeshCount);
        internal int TreadSubMeshCount => (HaveTread ? StepCount + CylinderSubMeshCount : 0);
        internal int StairsSubMeshCount => (TreadSubMeshCount + RiserSubMeshCount);

        // a hollow column is a column with a smaller one cut out of it, when there is room for one
        internal float ColumnHoleDiameter => innerDiameter - (2 * columnThickness);
        internal bool HaveColumn => HaveInnerCylinder && centerType != SpiralStairsCenterType.Open;
        internal bool HaveColumnHole => HaveColumn && centerType == SpiralStairsCenterType.Hollow && ColumnHoleDiameter >= kEpsilon;
        internal int ColumnSubMeshCount => (HaveColumn ? 1 : 0) + (HaveColumnHole ? 1 : 0);

        // each side is two wedges for every piece of a step: a step is cut where the round side it follows has a corner, so
        // the side has the same straight pieces as the cylinder the steps are trimmed by
        internal bool HaveOuterSide => outerSide != StairsSideType.None;
        internal bool HaveInnerSide => innerSide != StairsSideType.None && HaveInnerCylinder;
        internal int OuterSideSubMeshCount => HaveOuterSide ? CountSidePieces(outerSegments) * 2 : 0;
        internal int InnerSideSubMeshCount => HaveInnerSide ? CountSidePieces(innerSegments) * 2 : 0;
        internal int SideSubMeshCount => OuterSideSubMeshCount + InnerSideSubMeshCount;

        int CountSidePieces(int segments)
        {
            var start   = math.radians(startAngle);
            var perStep = math.radians(rotation / StepCount);
            var count   = 0;
            for (int step = 0; step < StepCount; step++)
            {
                var angle = start + perStep * step;
                var end   = angle + perStep;
                while (angle < end - kAngleEpsilon)
                {
                    angle = NextSideCorner(angle, end, segments);
                    count++;
                }
            }
            return count;
        }

        internal const float kAngleEpsilon = 0.0001f;

        // A cylinder has its corners at (cos t, sin t), the stairs have their angles at (sin a, cos a): the corner after
        // an angle, or the end when there is none before it
        internal static float NextSideCorner(float angle, float end, int segments)
        {
            var segmentAngle = (math.PI * 2) / segments;
            var offset       = ((segments & 1) == 1) ? 0.0f : segmentAngle * 0.5f;
            var t            = (math.PI * 0.5f) - angle;
            var corner       = offset + math.floor((t - offset) / segmentAngle - kAngleEpsilon) * segmentAngle;
            var next         = (math.PI * 0.5f) - corner;
            return (next >= end - kAngleEpsilon) ? end : next;
        }

        // How far out the straight piece of the cylinder that the middle of a piece is on lies, at an angle on it
        internal static float RadiusOnSide(float angle, float middleAngle, float apothem, int segments)
        {
            var segmentAngle = (math.PI * 2) / segments;
            var offset       = ((segments & 1) == 1) ? 0.0f : segmentAngle * 0.5f;
            var t            = (math.PI * 0.5f) - middleAngle;
            var edgeMiddle   = offset + (math.floor((t - offset) / segmentAngle) + 0.5f) * segmentAngle;
            return apothem / math.cos(((math.PI * 0.5f) - angle) - edgeMiddle);
        }

        internal int RequiredSubMeshCount => StairsSubMeshCount + ColumnSubMeshCount + SideSubMeshCount;


        internal bool HaveRiser => riserType != StairsRiserType.None;
        internal int TreadStart => !HaveRiser ? 0 : RiserSubMeshCount;
        #endregion

        #region Generate
        public int PrepareAndCountRequiredBrushMeshes()
        {
            return RequiredSubMeshCount;
        }

        public bool GenerateNodes(BlobAssetReference<InternalChiselSurfaceArray> surfaceDefinitionBlob, NativeList<GeneratedNode> nodes, Allocator allocator = Allocator.Persistent)// Indirect
		{
            NativeList<BlobAssetReference<BrushMeshBlob>> generatedBrushMeshes;
			using var _generatedBrushMeshes = generatedBrushMeshes = new NativeList<BlobAssetReference<BrushMeshBlob>>(nodes.Length, Allocator.Temp);
            generatedBrushMeshes.Resize(nodes.Length, NativeArrayOptions.ClearMemory);
            if (!BrushMeshFactory.GenerateSpiralStairs(generatedBrushMeshes,
                                                        ref this,
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
                nodes[i] = GeneratedNode.GenerateBrush(generatedBrushMeshes[i]); // Confirmed to dispose

            // the risers and the treads each end in an outer cylinder that trims them and, with an open middle, an inner one
            // that cuts it out; without risers there are only the treads' cylinders
            if (HaveRiser)
                TrimWithCylinders(nodes, TreadStart);
            TrimWithCylinders(nodes, StairsSubMeshCount);

            // a hollow column has its hole cut out after it; the sides after that are added whole
            if (HaveColumnHole)
            {
                var hole = nodes[StairsSubMeshCount + 1];
                hole.operation = CSGOperationType.Subtractive;
                nodes[StairsSubMeshCount + 1] = hole;
            }

            return true;
        }

        void TrimWithCylinders(NativeList<GeneratedNode> nodes, int end)
        {
            var node = nodes[end - CylinderSubMeshCount];
            node.operation = CSGOperationType.Intersecting;
            nodes[end - CylinderSubMeshCount] = node;

            if (HaveInnerCylinder)
            {
                node = nodes[end - 1];
                node.operation = CSGOperationType.Subtractive;
                nodes[end - 1] = node;
            }
        }

        public void Dispose() {}
        #endregion

        #region Surfaces
        public enum SurfaceSides : byte
        { 
            TreadTopSurface       = 0,
            TreadBottomSurface    = 1,
            TreadFrontSurface     = 2,
            TreadBackSurface      = 3,
            RiserFrontSurface     = 4,
            RiserBackSurface      = 5,
            InnerSurface          = 6,
            OuterSurface          = 7,

            TotalSides
        }

        [BurstDiscard]
        public int RequiredSurfaceCount { get { return 8; } }

        [BurstDiscard]
        public void UpdateSurfaces(ref ChiselSurfaceArray surfaceDefinition) { }
        #endregion
        
        #region Validation
        public const float	kMinStepHeight			= 0.01f;
        public const float  kMinStairsDepth         = 0.1f;
        public const float  kMinRiserDepth          = 0.01f;
        public const float	kMinRotation			= 15;
        public const int    kMinSegments            = 3;
        public const float	kMinInnerDiameter		= 0.00f;
        public const float	kMinOuterDiameter		= 0.01f;
        public const float  kMinSideWidth           = 0.01f;

        public bool Validate()
        {
            // only the height of the origin is used: moving the stairs sideways is what their transform is for, and the
            // cylinders that trim the steps stay on the center
            origin.x        = 0;
            origin.z        = 0;
            stepHeight      = math.max(kMinStepHeight, stepHeight);

            innerDiameter   = math.min(outerDiameter - kMinStairsDepth, innerDiameter);
            innerDiameter   = math.max(kMinInnerDiameter, innerDiameter);
            outerDiameter   = math.max(innerDiameter + kMinStairsDepth, outerDiameter);
            outerDiameter   = math.max(kMinOuterDiameter, outerDiameter);
            height          = math.max(stepHeight, math.abs(height)) * (height < 0 ? -1 : 1);
            treadHeight     = math.max(0, treadHeight);
            nosingDepth     = math.max(0, nosingDepth);
            nosingWidth     = math.max(0, nosingWidth);

            riserDepth      = math.max(kMinRiserDepth, riserDepth);

            rotation        = math.max(kMinRotation, rotation);    // never turning the other way

            innerSegments   = math.max(kMinSegments, innerSegments);
            outerSegments   = math.max(kMinSegments, outerSegments);

            // stairs saved before the column existed have none set: they get the default
            if (columnThickness == 0)
                columnThickness = kDefaultSettings.columnThickness;
            columnThickness = math.clamp(columnThickness, kMinSideWidth, math.max(kMinSideWidth, innerDiameter * 0.5f));
            if (sideWidth == 0)
                sideWidth = kDefaultSettings.sideWidth;
            sideWidth       = math.max(kMinSideWidth, sideWidth);
            sideHeight      = math.max(0, sideHeight);
            sideDepth       = math.max(0, sideDepth);
            return true;
        }

        [BurstDiscard]
        public void GetWarningMessages(IChiselMessageHandler messages) { }
        #endregion

        #region Reset
        public void Reset() { this = DefaultSettings; }
        #endregion
    }

    // https://www.archdaily.com/896537/how-to-calculate-spiral-staircase-dimensions-and-designs
    // http://www.zhitov.ru/en/spiral_stairs/
    // https://easystair.net/en/spiral-staircase.php
    // https://www.google.com/imgres?imgurl=https%3A%2F%2Fwww.visualarq.com%2Fwp-content%2Fuploads%2Fsites%2F2%2F2014%2F07%2FSpiral-stair-landings.png&imgrefurl=https%3A%2F%2Fwww.visualarq.com%2Fsupport%2Ftips%2Fhow-can-i-create-spiral-stairs-can-i-add-landings%2F&docid=Tk82BDe0l2fZmM&tbnid=DTs7Bc10UxKpWM%3A&vet=10ahUKEwiL8_nBtLXeAhWC-qQKHUO4CBQQMwhCKAIwAg..i&w=880&h=656&client=firefox-b-ab&bih=625&biw=1649&q=spiral%20stairs%20parameters&ved=0ahUKEwiL8_nBtLXeAhWC-qQKHUO4CBQQMwhCKAIwAg&iact=mrc&uact=8#h=656&imgdii=DTs7Bc10UxKpWM:&vet=10ahUKEwiL8_nBtLXeAhWC-qQKHUO4CBQQMwhCKAIwAg..i&w=880
    // https://www.google.com/imgres?imgurl=https%3A%2F%2Fwww.visualarq.com%2Fwp-content%2Fuploads%2Fsites%2F2%2F2014%2F07%2FSpiral-stair-landings.png&imgrefurl=https%3A%2F%2Fwww.visualarq.com%2Fsupport%2Ftips%2Fhow-can-i-create-spiral-stairs-can-i-add-landings%2F&docid=Tk82BDe0l2fZmM&tbnid=DTs7Bc10UxKpWM%3A&vet=10ahUKEwiL8_nBtLXeAhWC-qQKHUO4CBQQMwhCKAIwAg..i&w=880&h=656&client=firefox-b-ab&bih=625&biw=1649&q=spiral%20stairs%20parameters&ved=0ahUKEwiL8_nBtLXeAhWC-qQKHUO4CBQQMwhCKAIwAg&iact=mrc&uact=8#h=656&imgdii=DPwskqkaN7e_wM:&vet=10ahUKEwiL8_nBtLXeAhWC-qQKHUO4CBQQMwhCKAIwAg..i&w=880
    [Serializable]
#if UNITY_6000_7_OR_NEWER
    [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
#endif
    public class ChiselSpiralStairsDefinition : SerializedBranchGenerator<ChiselSpiralStairs>
    {
        public const string kNodeTypeName = "Spiral Stairs";

        #region OnEdit
        //
        // TODO: code below needs to be cleaned up & simplified 
        //


        static Vector3[] s_InnerVertices;
        static Vector3[] s_OuterVertices;

        const float kHiddenDashSize = 2.0f;

        // A cylinder's outline: the sides facing the camera drawn clearly over a dark edge, the ones facing away fainter,
        // and every line dashed where something in front of it hides it, so its shape reads
        static void DrawCylinder(IChiselHandles handles, Vector3[] vertices, int sides, Vector3 center, bool inward, Color color, Color outlineColor)
        {
            var faint  = color; faint.a *= 0.6f;
            var hidden = Color.Lerp(color, Color.black, 0.45f); hidden.a = color.a;
            bool Facing(int i, int j)
            {
                var middle = (vertices[i] + vertices[j] + vertices[i + sides] + vertices[j + sides]) * 0.25f;
                var normal = middle - center; normal.y = 0;
                return !handles.IsSufaceBackFaced(middle, normal);
            }

            // dashed through everything first, then whole where nothing is in front, which covers the dashes there
            void Line(Vector3 from, Vector3 to, Color lineColor, float thickness)
            {
                handles.color = hidden;
                handles.DrawLine(from, to, lineMode: LineMode.NoZTest, thickness: 0.5f, dashSize: kHiddenDashSize);
                handles.color = lineColor;
                handles.DrawLine(from, to, lineMode: LineMode.ZTest, thickness: thickness);
            }

            for (int i = 0, j = sides - 1; i < sides; j = i, i++)
            {
                var t0 = vertices[i];
                var t1 = vertices[j];
                var b0 = vertices[i + sides];
                var b1 = vertices[j + sides];
                var k  = (i + 1) % sides;
                // a side's edges, and the upright between it and the next, which shows when either side does
                var sideFacing    = Facing(i, j);
                var uprightFacing = sideFacing || Facing(k, i);

                if (uprightFacing) { handles.color = outlineColor; handles.DrawLine(t0, b0, lineMode: LineMode.ZTest, thickness: 1.0f); }
                if (sideFacing)    { handles.color = outlineColor; handles.DrawLine(t0, t1, lineMode: LineMode.ZTest, thickness: 1.0f); handles.DrawLine(b0, b1, lineMode: LineMode.ZTest, thickness: 1.0f); }

                Line(t0, b0, uprightFacing ? color : faint, 1.0f);
                Line(t0, t1, sideFacing ? color : faint, 1.0f);
                Line(b0, b1, sideFacing ? color : faint, 1.0f);
                if (inward)
                    Line((t0 + b0) * 0.5f, (t1 + b1) * 0.5f, sideFacing ? color : faint, sideFacing ? 2.0f : 1.0f);
            }
        }

        public override void OnEdit(IChiselHandles handles)
        {
            var normal					= Vector3.up;
            var topDirection			= Vector3.forward;
            var lowDirection			= Vector3.forward;

            var originalOuterDiameter	= settings.outerDiameter;
            var originalInnerDiameter	= settings.innerDiameter;
            var originalStartAngle		= settings.startAngle;
            var originalStepHeight		= settings.stepHeight;
            var originalRotation		= settings.rotation;
            var originalHeight			= settings.height;
            var originalOrigin			= settings.origin;
            var cylinderTop				= new BrushMeshFactory.ChiselCircleDefinition { diameterX = 1, diameterZ = 1, height = originalOrigin.y + originalHeight };
            var cylinderLow				= new BrushMeshFactory.ChiselCircleDefinition { diameterX = 1, diameterZ = 1, height = originalOrigin.y };
            var originalTopPoint		= normal * cylinderTop.height;
            var originalLowPoint		= normal * cylinderLow.height;
            var originalMidPoint		= (originalTopPoint + originalLowPoint) * 0.5f;
                    
            var outerDiameter		= originalOuterDiameter;
            var innerDiameter		= originalInnerDiameter;
            var topPoint			= originalTopPoint;
            var lowPoint			= originalLowPoint;
            var midPoint			= originalMidPoint;
            var startAngle			= originalStartAngle;
            var rotation			= originalRotation;

            {
                var currRotation = startAngle + rotation;
                handles.DoRotatableLineHandle(ref startAngle  , lowPoint, outerDiameter * 0.5f, normal, lowDirection, Vector3.Cross(normal, lowDirection));
                handles.DoRotatableLineHandle(ref currRotation, topPoint, outerDiameter * 0.5f, normal, topDirection, Vector3.Cross(normal, topDirection));
                if (handles.modified)
                    rotation = currRotation - startAngle;


                // TODO: properly show things as backfaced
                // TODO: temporarily show inner or outer diameter as disabled when resizing one or the other
                // TODO: FIXME: why aren't there any arrows?
                handles.DoDirectionHandle(ref topPoint, normal, snappingStep: originalStepHeight);
                topPoint.y		= math.max(lowPoint.y + originalStepHeight, topPoint.y);
                handles.DoDirectionHandle(ref lowPoint, -normal, snappingStep: originalStepHeight);
                lowPoint.y		= math.min(topPoint.y - originalStepHeight, lowPoint.y);

                float minOuterDiameter = innerDiameter + ChiselSpiralStairs.kMinStairsDepth;
                { 
                    var outerRadius = outerDiameter * 0.5f;
                    handles.DoRadiusHandle(ref outerRadius, Vector3.up, topPoint, renderDisc: false);
                    handles.DoRadiusHandle(ref outerRadius, Vector3.up, lowPoint, renderDisc: false);
                    outerDiameter = math.max(minOuterDiameter, outerRadius * 2.0f);
                }
                        
                float maxInnerDiameter = outerDiameter - ChiselSpiralStairs.kMinStairsDepth;
                { 
                    var innerRadius = innerDiameter * 0.5f;
                    handles.DoRadiusHandle(ref innerRadius, Vector3.up, midPoint, renderDisc: false);
                    innerDiameter = math.min(maxInnerDiameter, innerRadius * 2.0f);
                }



                // TODO: somehow put this into a separate renderer
                cylinderTop.diameterZ = cylinderTop.diameterX = cylinderLow.diameterZ = cylinderLow.diameterX = originalInnerDiameter;
                BrushMeshFactory.GetConicalFrustumVertices(cylinderLow, cylinderTop, 0, settings.innerSegments, ref s_InnerVertices);

                cylinderTop.diameterZ = cylinderTop.diameterX = cylinderLow.diameterZ = cylinderLow.diameterX = originalOuterDiameter;
                BrushMeshFactory.GetConicalFrustumVertices(cylinderLow, cylinderTop, 0, settings.outerSegments, ref s_OuterVertices);
                
                var originalColor	= handles.color;
                var color			= handles.color;
                var outlineColor	= Color.black;
                outlineColor.a = color.a;

                DrawCylinder(handles, s_OuterVertices, settings.outerSegments, settings.origin, inward: false, color, outlineColor);
                DrawCylinder(handles, s_InnerVertices, settings.innerSegments, settings.origin, inward: true,  color, outlineColor);
                handles.color = originalColor;
            }
            if (handles.modified)
            {
                settings.outerDiameter = outerDiameter;
                settings.innerDiameter = innerDiameter;
                settings.startAngle    = startAngle;
                settings.rotation	   = rotation;

                if (topPoint != originalTopPoint)
                    settings.height = topPoint.y - lowPoint.y;

                if (lowPoint != originalLowPoint)
                {
                    settings.height	= topPoint.y - lowPoint.y;
                    var newOrigin = originalOrigin;
                    newOrigin.y += lowPoint.y - originalLowPoint.y;
                    settings.origin = newOrigin;
                }
            }

            // the sizes of the box around it, as when it was placed; a typed width sets the outer diameter, a typed height the height
            var box        = new Bounds(new Vector3(settings.origin.x, settings.origin.y + settings.height * 0.5f, settings.origin.z),
                                        new Vector3(settings.outerDiameter, math.abs(settings.height), settings.outerDiameter));
            var sizeBefore = box.size;
            if (handles.DoBoxMeasurements(ref box))
            {
                var size = box.size;
                if (size.x != sizeBefore.x) settings.outerDiameter = size.x;
                if (size.z != sizeBefore.z) settings.outerDiameter = size.z;
                if (size.y != sizeBefore.y) settings.height = (settings.height < 0 ? -1 : 1) * size.y;
            }
        }
        #endregion
    }
}