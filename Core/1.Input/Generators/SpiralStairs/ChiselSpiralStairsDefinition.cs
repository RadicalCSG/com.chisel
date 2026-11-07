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

    // Spiral Stairs data
    [Serializable]
    public struct ChiselSpiralStairs : IBranchGenerator, IChiselSurfaceNames
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

        [ConditionalName("Outer"), Tooltip("A guard rail along the outside of the steps: below them, above them, or both")]
        public StairsSideType           outerSide;
        [ConditionalName("Inner"), Tooltip("A guard rail along the open middle: below the steps, above them, or both. Needs an inner diameter above 0")]
        public StairsSideType           innerSide;

        [ConditionalHideIfAll(StairsSideType.None, nameof(outerSide), nameof(innerSide))]
        [ConditionalName("Thickness"), Tooltip("How thick the guard rails are")]
        [DistanceValue] public float	sideWidth;
        [ConditionalHideIfAll(StairsSideType.None, nameof(outerSide), nameof(innerSide))]
        [ConditionalName("Height Above"), Tooltip("How high a guard rail above the steps reaches over them")]
        [DistanceValue] public float	sideHeight;
        [ConditionalHideIfAll(StairsSideType.None, nameof(outerSide), nameof(innerSide))]
        [ConditionalName("Depth Below"), Tooltip("How far a guard rail below the steps reaches under them")]
        [DistanceValue] public float	sideDepth;
        [ConditionalHideIfAll(StairsSideType.None, nameof(outerSide), nameof(innerSide))]
        [ConditionalName("Front Offset"), Tooltip("How far the guard rails reach out past the steps' fronts, round the stairs")]
        [DistanceValue] public float	sideFrontOffset;
        [ConditionalHideIfAll(StairsSideType.None, nameof(outerSide), nameof(innerSide))]
        [ConditionalName("Smoothing Group"), Tooltip("The smoothing group of the guard rails' round faces, so they shade as one curved surface; 0 to show their flat pieces")]
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

        // raised above the object's position, the part under the first step is its own, with its own surface; solid down
        // to the floor, every step goes down there and there is no part of it to tell apart
        internal bool HaveBase => riserType != StairsRiserType.None && riserType != StairsRiserType.FillDown && math.min(origin.y, origin.y + height) > 0.001f;

        // the base is in one piece for each straight side of the outer cylinder under the first step, so it has that
        // cylinder's shape without being trimmed by it, which would give its outside the cylinder's surface
        // with a guard rail on the outside the steps end halfway into it, so its outer face isn't on theirs
        internal float StepsOuterDiameter => HaveOuterSide ? math.max(0.001f, outerDiameter - sideWidth / math.cos(math.PI / outerSegments)) : outerDiameter;

        internal bool BaseHasOpenMiddle => innerDiameter >= 0.001f && !HaveColumn;

        internal int BaseSubMeshCount
        {
            get
            {
                if (!HaveBase)
                    return 0;
                var start = math.radians(startAngle);
                var end   = start + math.radians(AnglePerStep);
                var low   = math.min(start, end);
                var high  = math.max(start, end);
                var count = 0;
                for (var angle = low; angle < high - kAngleEpsilon && count < outerSegments + 2; count++)
                    angle = NextSideCorner(angle, high, outerSegments);
                // around an open middle each piece is a four sided one, in two
                return BaseHasOpenMiddle ? count * 2 : count;
            }
        }
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
            var end     = start + perStep * StepCount;
            var split   = SideFloorAngle(start, perStep);
            var count   = 0;
            var angle   = start;
            while (angle < end - kAngleEpsilon)
            {
                angle = NextSidePieceEnd(angle, end, segments, split);
                count++;
            }
            // one more in front of the first step, when it reaches out past it
            if (count > 0 && sideFrontOffset > 0)
                count++;
            return count;
        }

        // Where the bottom of a wall below the steps reaches the floor: before it the wall stands on the floor, after it
        // the wall follows the steps, so a piece of wall can't be flat across it
        internal float SideFloorAngle(float start, float perStep)
        {
            var angle = start + perStep * (stepHeight > 0 ? sideDepth / stepHeight : 0);
            // raised above the object's position, it stands on that beside the first step, solid down to it there
            if (origin.y > 0)
                angle = math.max(angle, start + perStep);
            return angle;
        }

        // A wall runs along the straight sides of its cylinder in one piece each, so each side shows one face; it is also
        // cut where it reaches the floor
        internal static float NextSidePieceEnd(float angle, float end, int segments, float split)
        {
            var next = NextSideCorner(angle, end, segments);
            if (split > angle + kAngleEpsilon && split < next - kAngleEpsilon)
                next = split;
            return next;
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

        internal int RequiredSubMeshCount => StairsSubMeshCount + ColumnSubMeshCount + SideSubMeshCount + BaseSubMeshCount;


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
        [BurstDiscard]
        public int RequiredSurfaceCount { get { return StairsSurfaces.kCount; } }

        [BurstDiscard]
        public readonly string GetSurfaceName(int index) { return StairsSurfaces.Name(index); }

        [BurstDiscard]
        public bool IsSurfaceUsed(int index)
        {
            return StairsSurfaces.IsUsed(index, haveTread: treadHeight >= 0.0001f, haveRiser: HaveRiser,
                                         haveWall: HaveOuterSide || HaveInnerSide, haveColumn: HaveColumn, haveBase: HaveBase);
        }

        [BurstDiscard]
        public void UpdateSurfaces(ref ChiselSurfaceArray surfaceDefinition)
        {
            StairsSurfaces.UpdateSurfaces(ref surfaceDefinition);
        }
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
            sideFrontOffset = math.max(0, sideFrontOffset);
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
    // https://www.google.com/imgres?imgurl=https%3A%2F%2Fwww.visualarq.com%2Fwp-content%2Fuploads%2Fsites%2F2%2F2014%2F07%2FSpiral-stair-landings.png&imgrefurl=https%3A%2F%2Fwww.visualarq.com%2Fsupport%2Ftips%2Fhow-can-i-create-spiral-stairs-can-i-add-landings%2F&docid=Tk82BDe0l2fZmM&tbnid=DTs7Bc10UxKpWM%3A&vet=10ahUKEwiL8_nBtLXeAhWC-qQKHUO4CBQQMwhCKAIwAg..i&w=880&h=656&client=firefox-b-ab&bih=625&biw=1649&q=spiral%20stairs%20parameters&ved=0ahUKEwiL8_nBtLXeAhWC-qQKHUO4CBQQMwhCKAIwAg&iact=mrc&uact=8#h=656&imgdii=DTs7Bc10UxKpWM:&vet=10ahUKEwiL8_nBtLXeAhWC-qQKHUO4CBQQMwhCKAIwAg..i&w=880
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

        // Read only, along an angle line: how wide a step is there, the guard rails included: from the inside of an inner
        // rail, or the inner cylinder, to the outer cylinder
        void StepMeasurements(IChiselHandles handles, float angleInDegrees, bool atTheTop)
        {
            var low     = settings.origin.y + math.min(0, settings.height);
            var y       = atTheTop ? low + math.abs(settings.height) : low;
            var angle   = math.radians(angleInDegrees);
            var outward = new Vector3(math.sin(angle), 0, math.cos(angle));
            var center  = new Vector3(settings.origin.x, y, settings.origin.z);

            var outer   = ChiselSpiralStairs.RadiusOnSide(angle, angle, settings.outerDiameter * 0.5f * math.cos(math.PI / settings.outerSegments), settings.outerSegments);
            var inner   = 0f;
            if (settings.innerDiameter > 0)
            {
                var apothem = settings.innerDiameter * 0.5f * math.cos(math.PI / settings.innerSegments);
                // beside a column the inner rail stands outside the cylinder, around an open middle inside it
                if (settings.HaveInnerSide && !settings.HaveColumn)
                    apothem = math.max(0, apothem - settings.sideWidth);
                if (apothem > 0)
                    inner = ChiselSpiralStairs.RadiusOnSide(angle, angle, apothem, settings.innerSegments);
            }
            handles.RenderDistanceMeasurement(center + outward * inner, center + outward * outer, outer - inner, "Step Width");
        }

        // A cylinder's outline: the sides facing the camera drawn clearly over a dark edge, the ones facing away fainter,
        // and every line dashed where something in front of it hides it, so its shape reads
        // returns the corners on its horizon, the last one found second
        static int2 DrawCylinder(IChiselHandles handles, Vector3[] vertices, int sides, Vector3 center, bool inward, Color color, Color outlineColor, bool uprights = true)
        {
            var horizon = new int2(-1, 0);
            var faint  = color; faint.a *= 0.6f;
            // a ring seen from inside the cylinder: its part behind the horizon fainter still
            var behind = color; behind.a *= 0.2f;
            var cameraY = Camera.current != null ? handles.inverseMatrix.MultiplyPoint(Camera.current.transform.position).y : 0;
            var topInside    = Camera.current != null && cameraY < vertices[0].y;
            var bottomInside = Camera.current != null && cameraY > vertices[sides].y;
            if (vertices[0].y < vertices[sides].y)
                (topInside, bottomInside) = (cameraY > vertices[0].y, cameraY < vertices[sides].y);
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

                // without all uprights only those on its horizon: between a side facing the camera and one facing away
                var onHorizon   = sideFacing != Facing(k, i);
                if (onHorizon)
                {
                    if (horizon.x < 0) horizon.x = i;
                    horizon.y = i;
                }
                var drawUpright = onHorizon;
                // the others subtle
                if (uprights && !onHorizon)
                {
                    var subtle = color; subtle.a *= 0.25f;
                    handles.color = subtle;
                    handles.DrawLine(t0, b0, lineMode: LineMode.NoZTest, thickness: 0.5f, dashSize: kHiddenDashSize);
                    handles.DrawLine(t0, b0, lineMode: LineMode.ZTest, thickness: 0.5f);
                }
                if (drawUpright && uprightFacing) { handles.color = outlineColor; handles.DrawLine(t0, b0, lineMode: LineMode.ZTest, thickness: 1.0f); }
                if (sideFacing)    { handles.color = outlineColor; handles.DrawLine(t0, t1, lineMode: LineMode.ZTest, thickness: 1.0f); handles.DrawLine(b0, b1, lineMode: LineMode.ZTest, thickness: 1.0f); }

                if (drawUpright)
                    Line(t0, b0, uprightFacing ? color : faint, 1.0f);
                Line(t0, t1, sideFacing ? color : (topInside ? behind : faint), 1.0f);
                Line(b0, b1, sideFacing ? color : (bottomInside ? behind : faint), 1.0f);
            }
            if (horizon.x < 0) horizon.x = horizon.y;
            return horizon;
        }

        // A cylinder's two lines on its horizon, from low to high, dragged out or in to resize it; returns whether one has focus
        bool HorizonHandles(IChiselHandles handles, float lowY, float highY, ref float diameter, string radiusName, float innerRadius = 0, string innerName = null)
        {
            var center = new Vector3(settings.origin.x, 0, settings.origin.z);
            if (highY <= lowY || !handles.TryGetHorizonPoints(center, diameter * 0.5f, out var left, out var right))
                return false;
            var focus = false;
            foreach (var point in new[] { left, right })
            {
                var change = handles.DoHorizonLineHandle(new Vector3(point.x, lowY, point.z), new Vector3(point.x, highY, point.z), center, radiusName, innerRadius, innerName);
                diameter = math.max(0, diameter + change);
                focus |= handles.lastHandleHadFocus;
            }
            return focus;
        }

        // A guard rail's top and bottom, as it is built: from the line through the treads' fronts, its bottom never below the floor
        void DrawRailSpirals(IChiselHandles handles, Color color, StairsSideType side, float apothem, int sides)
        {
            var low   = settings.origin.y + math.min(0, settings.height);
            var line  = low + settings.stepHeight;
            var above = (side == StairsSideType.Up   || side == StairsSideType.DownAndUp) ? settings.sideHeight : 0;
            var below = (side == StairsSideType.Down || side == StairsSideType.DownAndUp) ? settings.stepHeight + settings.sideDepth : 0;
            if (settings.riserType == StairsRiserType.FillDown)
                below = math.abs(settings.height) + settings.stepHeight;
            DrawSpiral(handles, color, apothem, sides, line + above);
            DrawSpiral(handles, color, apothem, sides, line - below, math.min(0, low));

            // upright where each of its pieces starts and ends
            var hidden    = Color.Lerp(color, Color.black, 0.45f); hidden.a = color.a;
            var floor     = math.min(0, low);
            var start     = math.radians(settings.startAngle);
            var perStep   = math.radians(settings.AnglePerStep);
            var end       = start + perStep * settings.StepCount;
            var split     = settings.SideFloorAngle(start, perStep);
            var firstStep = start + perStep;
            var angle     = start;

            void RailLine(Vector3 lineFrom, Vector3 lineTo)
            {
                handles.color = hidden;
                handles.DrawLine(lineFrom, lineTo, lineMode: LineMode.NoZTest, thickness: 0.5f, dashSize: kHiddenDashSize);
                handles.color = color;
                handles.DrawLine(lineFrom, lineTo, lineMode: LineMode.ZTest, thickness: 1.0f);
            }

            // the piece in front of the first step: straight on from the rail's first piece, flat on top and at the bottom
            if (settings.sideFrontOffset > 0 && start < end - ChiselSpiralStairs.kAngleEpsilon)
            {
                var first   = ChiselSpiralStairs.NextSidePieceEnd(start, end, sides, split);
                var middle  = (start + first) * 0.5f;
                float2 At(float at) => new float2(settings.origin.x, settings.origin.z) + new float2(math.sin(at), math.cos(at)) * ChiselSpiralStairs.RadiusOnSide(at, middle, apothem, sides);
                var rail    = At(start);
                var front   = rail + math.normalizesafe(rail - At(first)) * settings.sideFrontOffset;
                var top     = line + above;
                var bottom  = (below > 0 && low > floor) ? floor : math.max(floor, line - below);
                var railTop = new Vector3(rail.x, top, rail.y);
                var railLow = new Vector3(rail.x, bottom, rail.y);
                var frontTop = new Vector3(front.x, top, front.y);
                var frontLow = new Vector3(front.x, bottom, front.y);
                RailLine(frontLow, frontTop);
                RailLine(railTop, frontTop);
                RailLine(railLow, frontLow);
            }

            for (int piece = 0; piece < sides * 64; piece++)
            {
                var y       = line + settings.stepHeight * (angle - start) / perStep;
                // raised, beside the first step it goes down to the ground
                var bottom  = (below > 0 && low > floor && angle <= firstStep + ChiselSpiralStairs.kAngleEpsilon) ? floor : math.max(floor, y - below);
                var radius  = ChiselSpiralStairs.RadiusOnSide(angle, angle, apothem, sides);
                var x       = settings.origin.x + math.sin(angle) * radius;
                var z       = settings.origin.z + math.cos(angle) * radius;
                var from    = new Vector3(x, bottom, z);
                var to      = new Vector3(x, y + above, z);
                handles.color = hidden;
                handles.DrawLine(from, to, lineMode: LineMode.NoZTest, thickness: 0.5f, dashSize: kHiddenDashSize);
                handles.color = color;
                handles.DrawLine(from, to, lineMode: LineMode.ZTest, thickness: 1.0f);
                if (angle >= end - ChiselSpiralStairs.kAngleEpsilon)
                    break;
                angle = ChiselSpiralStairs.NextSidePieceEnd(angle, end, sides, split);
            }
        }

        // The line up the outer cylinder through the bottom of each step's front, and the guard rails' tops and bottoms
        void DrawSpirals(IChiselHandles handles, Color color)
        {
            var low         = settings.origin.y + math.min(0, settings.height);
            var outerSides  = settings.outerSegments;
            var innerSides  = settings.innerSegments;
            var outer       = settings.outerDiameter * 0.5f * math.cos(math.PI / outerSides);
            var inner       = settings.innerDiameter * 0.5f * math.cos(math.PI / innerSides);

            DrawSpiral(handles, color, outer, outerSides, low);
            if (settings.HaveOuterSide)
                DrawRailSpirals(handles, color, settings.outerSide, outer, outerSides);
            if (settings.HaveInnerSide)
                DrawRailSpirals(handles, color, settings.innerSide, settings.HaveColumn ? inner + settings.sideWidth : math.max(0, inner - settings.sideWidth), innerSides);
        }

        // Around the sides at this distance from the middle, starting at this height, a step up per step, never below the floor
        void DrawSpiral(IChiselHandles handles, Color color, float apothem, int sides, float bottom, float floor = float.NegativeInfinity)
        {
            const int kSamplesPerSide = 4;
            var hidden    = Color.Lerp(color, Color.black, 0.45f); hidden.a = color.a;
            var start     = math.radians(settings.startAngle);
            var stepCount = settings.StepCount;
            var turn      = math.radians(settings.AnglePerStep) * stepCount;
            var rise      = settings.stepHeight * stepCount;
            var samples   = math.max(1, (int)math.ceil(math.abs(turn) / (math.PI * 2) * sides * kSamplesPerSide));
            Vector3 At(int i)
            {
                var t      = i / (float)samples;
                var angle  = start + turn * t;
                var radius = ChiselSpiralStairs.RadiusOnSide(angle, angle, apothem, sides);
                return new Vector3(settings.origin.x + math.sin(angle) * radius, math.max(floor, bottom + rise * t), settings.origin.z + math.cos(angle) * radius);
            }
            var from = At(0);
            for (int i = 1; i <= samples; i++)
            {
                var to = At(i);
                handles.color = hidden;
                handles.DrawLine(from, to, lineMode: LineMode.NoZTest, thickness: 0.5f, dashSize: kHiddenDashSize);
                handles.color = color;
                handles.DrawLine(from, to, lineMode: LineMode.ZTest, thickness: 1.0f);
                from = to;
            }
        }

        public override void OnEdit(IChiselHandles handles)
        {
            handles.DoMoveGizmo(Vector3.zero);

            var normal		 = Vector3.up;
            var topDirection = Vector3.forward;
            var lowDirection = Vector3.forward;
                                     
            var originalOuterDiameter = settings.outerDiameter;
            var originalInnerDiameter = settings.innerDiameter;
            var originalStartAngle	  = settings.startAngle;
            var originalStepHeight	  = settings.stepHeight;
            var originalRotation	  = settings.rotation;
            var originalHeight		  = settings.height;
            var originalOrigin		  = settings.origin;
            var cylinderTop			  = new BrushMeshFactory.ChiselCircleDefinition { diameterX = 1, diameterZ = 1, height = originalOrigin.y + originalHeight };
            var cylinderLow			  = new BrushMeshFactory.ChiselCircleDefinition { diameterX = 1, diameterZ = 1, height = originalOrigin.y };
            var originalTopPoint	  = normal * cylinderTop.height;
            var originalLowPoint	  = normal * cylinderLow.height;
            var originalMidPoint	  = (originalTopPoint + originalLowPoint) * 0.5f;
                    
            var outerDiameter = originalOuterDiameter;
            var innerDiameter = originalInnerDiameter;
            var topPoint	  = originalTopPoint;
            var lowPoint	  = originalLowPoint;
            var midPoint	  = originalMidPoint;
            var startAngle	  = originalStartAngle;
            var rotation	  = originalRotation;

            {
                var currRotation = startAngle + rotation;
                // where the lowest step starts: a knob on a ring around the bottom, just outside the stairs
                var lowCenter = new Vector3(originalOrigin.x, lowPoint.y, originalOrigin.z);
                handles.DoAngleRingHandle(ref startAngle, lowCenter, normal, lowDirection, outerDiameter * 0.5f, "Change Start Angle", name: "Start Angle");
                var bottomAngleFocus = handles.lastHandleHadFocus;
                var angleDragged     = handles.lastHandleIsDragged;
                // where the top step is: a knob on a ring around the top, its end angle counted from the same zero as the
                // start angle; moving the lowest step leaves the top one where it is
                var topCenter      = new Vector3(originalOrigin.x, topPoint.y, originalOrigin.z);
                var endAngle       = currRotation;
                handles.DoAngleRingHandle(ref endAngle, topCenter, normal, topDirection, outerDiameter * 0.5f, "Change End Angle", name: "End Angle");
                var turn           = endAngle - startAngle;
                // while an angle is hovered or dragged, only it is measured
                angleDragged |= handles.lastHandleIsDragged | handles.lastHandleHadFocus | bottomAngleFocus;
                if (handles.modified)
                    rotation = turn;


                // TODO: properly show things as backfaced
                // TODO: temporarily show inner or outer diameter as disabled when resizing one or the other
                // TODO: FIXME: why aren't there any arrows?
                // dashed from the height's handle down to the object's position
                {
                    var prevColor = handles.color;
                    var dashed = Color.Lerp(prevColor, Color.black, 0.45f); dashed.a = prevColor.a;
                    handles.color = dashed;
                    handles.DrawLine(topPoint, Vector3.zero, lineMode: LineMode.NoZTest, thickness: 1.0f, dashSize: 2.0f);
                    handles.color = prevColor;
                }
                handles.DoDirectionHandle(ref topPoint, normal, snappingStep: originalStepHeight);
                // while the height is hovered or dragged, only it is measured
                var heightFocus = handles.lastHandleHadFocus;

                // the top step's edge, from the inner to the outer cylinder, dragged up or down: the height
                {
                    var along    = Quaternion.AngleAxis(startAngle + rotation, normal) * topDirection;
                    var edgeFrom = topCenter + along * (innerDiameter * 0.5f);
                    var edgeTo   = topCenter + along * (outerDiameter * 0.5f);
                    if (handles.DoEdgeHandle1DOffset(out Vector3 topEdgeMoved, Axis.Y, edgeFrom, edgeTo, normal, snappingStep: originalStepHeight, undoMessage: "Change Height", thickness: 5.0f))
                        topPoint.y = originalTopPoint.y + Vector3.Dot(topEdgeMoved, normal);
                    // what it changes, on the side of it the steps don't go on to: on past the top step
                    if (handles.lastHandleHadFocus)
                    {
                        heightFocus = true;
                        var onward = Quaternion.AngleAxis(startAngle + rotation + 90, normal) * topDirection;
                        if (rotation < 0)
                            onward = -onward;
                        handles.RenderLabel((edgeFrom + edgeTo) * 0.5f, onward, "Height");
                    }
                }
                topPoint.y		= math.max(lowPoint.y + originalStepHeight, topPoint.y);

                // the bottom of the lowest step's front, dragged up or down: how high the stairs start, the top staying where it is
                var firstAngle = math.radians(startAngle);
                var front      = new Vector3(math.sin(firstAngle), 0, math.cos(firstAngle));
                var midRadius  = (outerDiameter + innerDiameter) * 0.25f;
                var basePoint  = lowCenter + front * midRadius;
                if (handles.DoEdgeHandle1DOffset(out Vector3 raised, Axis.Y, lowCenter + front * (innerDiameter * 0.5f), lowCenter + front * (outerDiameter * 0.5f), normal,
                                                 snappingStep: originalStepHeight, undoMessage: "Change Base Height"))
                    lowPoint.y = math.clamp(originalLowPoint.y + raised.y, math.min(0, originalLowPoint.y), topPoint.y - originalStepHeight);
                // while it's hovered or dragged, only the base height is measured; with none, what it is, below it
                var baseEdgeFocus = handles.lastHandleHadFocus;
                if (baseEdgeFocus && originalLowPoint.y <= 0)
                    handles.RenderLabel(basePoint, -normal, "Base Height");

                // the lines on each cylinder's horizon, from the ground to the top, are dragged out or in to resize it; while
                // one shows its radius, nothing else is measured
                var horizonLow   = math.min(0, originalOrigin.y);
                var horizonFocus = HorizonHandles(handles, horizonLow, topPoint.y, ref outerDiameter, "Outer Radius", innerDiameter * 0.5f, "Step Width");
                outerDiameter = math.max(innerDiameter + ChiselSpiralStairs.kMinStairsDepth, outerDiameter);
                horizonFocus |= HorizonHandles(handles, horizonLow, topPoint.y, ref innerDiameter, "Inner Radius");
                innerDiameter = math.clamp(innerDiameter, 0, outerDiameter - ChiselSpiralStairs.kMinStairsDepth);

                float minOuterDiameter = innerDiameter + ChiselSpiralStairs.kMinStairsDepth;
                // circles to drag directly, top and bottom of each cylinder, as a cylinder has
                if (handles.generatorState is not IChiselEllipsoidHandle[] circles || circles.Length != 7)
                {
                    circles = new IChiselEllipsoidHandle[7];
                    for (int c = 0; c < circles.Length; c++)
                        circles[c] = handles.CreateEllipsoidHandle(Vector3.zero, Vector3.up, 0, 0);
                    handles.generatorState = circles;
                }
                var groundPoint = normal * math.min(0, originalOrigin.y);
                // a circle between the ends is always seen from inside: its outside is away from the camera
                var cameraY = Camera.current != null ? handles.inverseMatrix.MultiplyPoint(Camera.current.transform.position).y : 0;
                Vector3 AwayFromCamera(Vector3 at) => cameraY > at.y ? Vector3.down : Vector3.up;
                // while a circle shows its radius, nothing else is measured
                var circleFocus = false;
                handles.DoDraggableCircle(ref outerDiameter, topPoint, circles[0], "Outer Radius", outward: Vector3.up, innerRadius: innerDiameter * 0.5f, innerName: "Step Width");
                circleFocus |= handles.lastHandleHadFocus;
                handles.DoDraggableCircle(ref outerDiameter, groundPoint, circles[1], "Outer Radius", outward: Vector3.down, innerRadius: innerDiameter * 0.5f, innerName: "Step Width");
                circleFocus |= handles.lastHandleHadFocus;
                // and at the height of the lowest step's angle line, when the stairs start above the ground
                var atBaseLine = new Vector3(groundPoint.x, lowPoint.y, groundPoint.z);
                var hasBaseLine = lowPoint.y > groundPoint.y;
                if (hasBaseLine)
                {
                    handles.DoDraggableCircle(ref outerDiameter, atBaseLine, circles[5], "Outer Radius", outward: AwayFromCamera(atBaseLine), innerRadius: innerDiameter * 0.5f, innerName: "Step Width");
                    circleFocus |= handles.lastHandleHadFocus;
                }
                outerDiameter = math.max(minOuterDiameter, outerDiameter);

                float maxInnerDiameter = outerDiameter - ChiselSpiralStairs.kMinStairsDepth;
                handles.DoDraggableCircle(ref innerDiameter, topPoint, circles[2], "Inner Radius", outward: Vector3.up);
                circleFocus |= handles.lastHandleHadFocus;
                handles.DoDraggableCircle(ref innerDiameter, groundPoint, circles[3], "Inner Radius", outward: Vector3.down);
                circleFocus |= handles.lastHandleHadFocus;
                if (hasBaseLine)
                {
                    handles.DoDraggableCircle(ref innerDiameter, atBaseLine, circles[6], "Inner Radius", outward: AwayFromCamera(atBaseLine));
                    circleFocus |= handles.lastHandleHadFocus;
                }
                innerDiameter = math.min(maxInnerDiameter, innerDiameter);

                // one being typed in stays, so nothing is hidden then
                var editing    = handles.isEditingMeasurement;
                var showOthers = editing || (!angleDragged && !heightFocus && !circleFocus && !baseEdgeFocus && !horizonFocus);
                if (showOthers)
                {
                    handles.DoDiameterMeasurement(ref outerDiameter, topPoint, groundPoint, name: "Outer Diameter");
                    handles.DoDiameterMeasurement(ref innerDiameter, topPoint, groundPoint, furthest: true, name: "Inner Diameter");
                }

                // how high the stairs start above the object's position, which can be typed in
                if (originalLowPoint.y > 0 && (editing || (!bottomAngleFocus && (showOthers || baseEdgeFocus))))
                {
                    var typedBase = originalLowPoint.y;
                    var onGround  = new Vector3(basePoint.x, 0, basePoint.z);
                    var atBase    = new Vector3(basePoint.x, originalLowPoint.y, basePoint.z);
                    if (handles.DoDistanceMeasurement(ref typedBase, onGround, atBase, "Change Base Height", name: "Base Height"))
                        lowPoint.y = math.clamp(typedBase, 0, topPoint.y - originalStepHeight);
                }



                // the cylinders reach down to the ground, also when the stairs start higher up
                cylinderLow.height = math.min(0, originalOrigin.y);

                // TODO: somehow put this into a separate renderer
                cylinderTop.diameterZ = cylinderTop.diameterX = cylinderLow.diameterZ = cylinderLow.diameterX = originalInnerDiameter;
                BrushMeshFactory.GetConicalFrustumVertices(cylinderLow, cylinderTop, 0, settings.innerSegments, ref s_InnerVertices);

                cylinderTop.diameterZ = cylinderTop.diameterX = cylinderLow.diameterZ = cylinderLow.diameterX = originalOuterDiameter;
                BrushMeshFactory.GetConicalFrustumVertices(cylinderLow, cylinderTop, 0, settings.outerSegments, ref s_OuterVertices);
                
                var originalColor	= handles.color;
                var color			= handles.color;
                var outlineColor	= Color.black;
                outlineColor.a = color.a;

                var horizon = DrawCylinder(handles, s_OuterVertices, settings.outerSegments, settings.origin, inward: false, color, outlineColor, uprights: false);

                // the total height, base included, just outside a line on the horizon; can be typed in
                if (originalHeight > 0 && (editing || (!angleDragged && !circleFocus && !baseEdgeFocus && !horizonFocus)))
                {
                    var ground     = cylinderLow.height;
                    var totalTop   = originalOrigin.y + originalHeight;
                    var corner     = s_OuterVertices[horizon.y];
                    var outside    = new Vector3(corner.x, 0, corner.z);
                    var typedTotal = totalTop - ground;
                    if (handles.DoDistanceMeasurement(ref typedTotal, outside + normal * ground, outside + normal * totalTop, "Change Height", offsetDirection: outside.normalized, name: "Height"))
                        topPoint.y = math.max(lowPoint.y + originalStepHeight, ground + typedTotal);
                    handles.color = originalColor;
                }
                DrawSpirals(handles, color);
                var innerHorizon = DrawCylinder(handles, s_InnerVertices, settings.innerSegments, settings.origin, inward: true,  color, outlineColor, uprights: settings.HaveColumn);
                handles.color = originalColor;

                // the lines on each cylinder's horizon are dragged out or in to resize it; while one is, its radius
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
        }
        #endregion
    }
}