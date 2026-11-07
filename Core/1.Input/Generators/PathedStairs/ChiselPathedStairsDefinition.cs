using System;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using Unity.Burst;
using UnityEngine;
using Unity.Entities;

namespace Chisel.Core
{
    [Serializable]
    public struct ChiselPathedStairs : IBranchGenerator, IChiselSurfaceNames
    {
        readonly static ChiselPathedStairs kDefaultSettings = new()
		{
            closed         = true,
            stairs          = ChiselLinearStairs.DefaultSettings
		};
		public static ref readonly ChiselPathedStairs DefaultSettings => ref kDefaultSettings;

		[NonSerialized] public bool closed;

        // a curve is cut into parts that each turn at most this much
        public const float kMaxTurnPerPart = math.PI / 12;

        // the side wall where two parts of the path meet, standing on the plane they share; the stairs' own left and
        // right side walls are at an open path's start and end
        public StairsSideType cornerSide;
        // how far back that wall goes beside the floor at the top, as the stairs' own side walls' do
        public StairsTopSideType cornerTopSide;
        [DistanceValue] public float cornerTopSideLength;

        public readonly float CornerTopSideLength => cornerTopSide switch
        {
            StairsTopSideType.None   => 0,
            StairsTopSideType.Length => math.max(0, cornerTopSideLength),
            _                        => float.PositiveInfinity
        };

        // TODO: do not use this data structure, find common stuff and share between the definitions ...
        [HideFoldout] public ChiselLinearStairs stairs;

        [UnityEngine.HideInInspector, NonSerialized] public BlobAssetReference<ChiselCurve2DBlob> curveBlob;
        [UnityEngine.HideInInspector, NonSerialized] internal UnsafeList<SegmentVertex> shapeVertices;

        #region Generate
        public int PrepareAndCountRequiredBrushMeshes()
        {
            ref var curve = ref curveBlob.Value;
            var bounds = stairs.bounds;
            if (shapeVertices.IsCreated) shapeVertices.Dispose();
			shapeVertices = curve.GetPathVerticesByTurn(kMaxTurnPerPart, Allocator.Persistent);
            if (shapeVertices.Length < 2)
                return 0;

            return BrushMeshFactory.CountPathedStairBrushes(shapeVertices, closed, bounds,
                                                            stairs.stepHeight, stairs.stepDepth, stairs.treadHeight, stairs.nosingDepth,
                                                            stairs.plateauHeight, stairs.riserType, stairs.riserDepth,
                                                            stairs.leftSide, stairs.rightSide, cornerSide,
                                                            stairs.sideWidth, stairs.sideHeight, stairs.sideDepth, stairs.sideFrontOffset, stairs.TopSideLength, CornerTopSideLength);
        }

        public bool GenerateNodes(BlobAssetReference<InternalChiselSurfaceArray> surfaceDefinitionBlob, NativeList<GeneratedNode> nodes, 
                                  Allocator allocator = Allocator.Persistent) // Indirect
        {
            if (!shapeVertices.IsCreated)
                return false;
            NativeList<BlobAssetReference<BrushMeshBlob>> generatedBrushMeshes;
			using var _generatedBrushMeshes = generatedBrushMeshes = new NativeList<BlobAssetReference<BrushMeshBlob>>(nodes.Length, Allocator.Temp);
            generatedBrushMeshes.Resize(nodes.Length, NativeArrayOptions.ClearMemory);
                
            ref var curve = ref curveBlob.Value;
            var bounds = stairs.bounds;

            using var brushTransformations = new NativeList<float4x4>(nodes.Length, Allocator.Temp);
            brushTransformations.Resize(nodes.Length, NativeArrayOptions.ClearMemory);
            if (!BrushMeshFactory.GeneratePathedStairs(generatedBrushMeshes, brushTransformations,
                                                        shapeVertices, closed, bounds,
                                                        stairs.stepHeight, stairs.stepDepth, stairs.treadHeight, stairs.nosingDepth, stairs.nosingWidth,
                                                        stairs.plateauHeight, stairs.riserType, stairs.riserDepth,
                                                        stairs.leftSide, stairs.rightSide, cornerSide,
                                                        stairs.sideWidth, stairs.sideHeight, stairs.sideDepth, stairs.sideFrontOffset, stairs.TopSideLength, CornerTopSideLength,
                                                        in surfaceDefinitionBlob, allocator)) // Indirect
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
                nodes[i] = GeneratedNode.GenerateBrush(generatedBrushMeshes[i], brushTransformations[i]); // Confirmed to dispose
			return true;
        }

        public void Dispose()
        {
            if (shapeVertices.IsCreated) shapeVertices.Dispose(); shapeVertices = default;
			if (curveBlob.IsCreated) curveBlob.Dispose(); curveBlob = default;
        }
        #endregion

        #region Surfaces
        [BurstDiscard]
        public int RequiredSurfaceCount { get { return stairs.RequiredSurfaceCount; } }

        public readonly string GetSurfaceName(int index) { return StairsSurfaces.Name(index); }
        public readonly bool IsSurfaceUsed(int index) { return stairs.IsSurfaceUsed(index); }

        [BurstDiscard]
        public void UpdateSurfaces(ref ChiselSurfaceArray surfaceDefinition)
        {
            stairs.UpdateSurfaces(ref surfaceDefinition);
        }
        #endregion

        #region Validation
        public bool Validate()
        {
            return stairs.Validate();
        }

        [BurstDiscard]
        public void GetWarningMessages(IChiselMessageHandler messages) { }
        #endregion

        #region Reset
        public void Reset() { this = DefaultSettings; }
        #endregion
    }

    [Serializable]
#if UNITY_6000_7_OR_NEWER
    [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
#endif
    public class ChiselPathedStairsDefinition : SerializedBranchGenerator<ChiselPathedStairs>
    {
        public const string kNodeTypeName = "Pathed Stairs";

        readonly static Curve2D	kDefaultShape = new(new[]{ new CurveControlPoint2D(-1,-1), new CurveControlPoint2D( 1,-1), new CurveControlPoint2D( 1, 1), new CurveControlPoint2D(-1, 1) });
        public static ref readonly Curve2D DefaultShape => ref kDefaultShape;


		public Curve2D shape;
        
        // TODO: do not use this data structure, find common stuff and share between the definitions ...
        //public ChiselLinearStairsDefinition stairs;

        public override void Reset()
        {
            shape = DefaultShape;
            base.Reset();
        }

        public override bool Validate() 
        {
            shape ??= DefaultShape;
            return base.Validate(); 
        }

        const Allocator defaultAllocator = Allocator.TempJob;
        public override ChiselPathedStairs GetBranchGenerator()
        {
            settings.curveBlob = ChiselCurve2DBlob.Convert(shape, defaultAllocator);
            settings.closed = shape.closed;
            return base.GetBranchGenerator();
        }

        // the lines dragged to change the base, the steps and the top: thicker than the others
        const float kThickLine = 5.0f;

        public override void OnEdit(IChiselHandles handles)
        {
            handles.DoMoveGizmo(PathCenter);
            handles.DoShapeHandle(ref shape);
            if (shape == null || shape.controlPoints == null || shape.controlPoints.Length < 2)
                return;

            // along every part of the path, lines to drag: the top at the back (the height), the top of the base at the
            // front, the back on the floor (the depth) and where the steps end at the top (how deep, so how steep, they are). The
            // lowest step is at the path, the floor at the top furthest from it
            ref var stairs   = ref settings.stairs;
            var stepHeight   = stairs.stepHeight;
            var stepCount    = stairs.StepCount;
            var height       = stairs.AbsHeight;
            var depth        = stairs.AbsDepth;
            var baseHeight   = stairs.plateauHeight;
            var run          = stepCount * stairs.stepDepth;

            // the path as the stairs are built along it, curves cut into their parts
            Vector2[] points;
            bool[] isControlPoint;
            using (var curveBlob = ChiselCurve2DBlob.Convert(shape, Allocator.Temp))
            {
                var pathVertices = curveBlob.Value.GetPathVerticesByTurn(ChiselPathedStairs.kMaxTurnPerPart, Allocator.Temp);
                points = new Vector2[pathVertices.Length];
                isControlPoint = new bool[pathVertices.Length];
                for (int i = 0; i < pathVertices.Length; i++)
                {
                    points[i] = pathVertices[i].position;
                    isControlPoint[i] = i == 0 || pathVertices[i].segmentIndex != pathVertices[i - 1].segmentIndex;
                }
                pathVertices.Dispose();
            }
            var count  = points.Length;
            var closed = shape.closed;
            var parts  = closed ? count : count - 1;
            var up     = Vector3.up;

            // the stairs stand at the object's position; moving that is the transform's
            Vector3 Point(int i) { var p = points[(i + count) % count]; return new Vector3(p.x, 0, p.y); }
            Vector3 Inward(int part) { var along = (Point(part + 1) - Point(part)).normalized; return new Vector3(-along.z, 0, along.x); }
            // a point on the cutting plane at a corner (square to the path at an open end), at a distance from the path
            Vector3 Offset(int i, float distance)
            {
                var hasBefore = closed || i > 0;
                var hasAfter  = closed || i < count - 1;
                var after     = Inward(hasAfter ? i : i - 1);
                if (!hasBefore || !hasAfter)
                    return Point(i) + after * distance;
                var before    = Inward(i - 1);
                var miter     = (before + after).normalized;
                var cos       = Vector3.Dot(miter, after);
                return Point(i) + miter * (cos > 0.01f ? distance / cos : distance);
            }

            // whole where nothing is in front, dashed where something is
            var color  = handles.color;
            var hidden = Color.Lerp(color, Color.black, 0.45f); hidden.a = color.a;
            var subtle = color; subtle.a *= 0.35f;
            void Line(Vector3 from, Vector3 to)
            {
                handles.color = hidden;
                handles.DrawLine(from, to, lineMode: LineMode.NoZTest, thickness: 0.5f, dashSize: 2.0f);
                handles.color = color;
                handles.DrawLine(from, to, lineMode: LineMode.ZTest, thickness: 1.0f);
            }

            // on every cutting plane the stairs' profile: up the front to the base, along the slope of the steps, across
            // the flat top to the back, down it and back along the floor. The slope is dragged across itself to move the
            // steps up or down: they keep their size, so there are fewer or more of them
            for (int i = 0; i < count; i++)
            {
                var front = Point(i) + up * baseHeight;
                var slope = Offset(i, run) + up * height;
                var top   = Offset(i, depth) + up * height;
                // inside a curve: faint, and only where nothing is in front
                if (!isControlPoint[i])
                {
                    handles.color = subtle;
                    if (baseHeight > 0)
                        handles.DrawLine(Point(i), front, lineMode: LineMode.ZTest, thickness: 1.0f);
                    handles.DrawLine(front, slope, lineMode: LineMode.ZTest, thickness: 1.0f);
                    handles.DrawLine(slope, top, lineMode: LineMode.ZTest, thickness: 1.0f);
                    handles.DrawLine(top, Offset(i, depth), lineMode: LineMode.ZTest, thickness: 1.0f);
                    handles.DrawLine(Offset(i, depth), Point(i), lineMode: LineMode.ZTest, thickness: 1.0f);
                    handles.color = color;
                    continue;
                }
                if (baseHeight > 0)
                    Line(Point(i), front);
                Line(slope, top);
                Line(top, Offset(i, depth));
                Line(Offset(i, depth), Point(i));

                var across = Vector3.Cross(slope - front, Vector3.Cross(up, slope - front)).normalized;
                if (Vector3.Dot(across, up) < 0)
                    across = -across;
                if (handles.DoEdgeHandle1DOffset(out Vector3 shifted, Axis.Y, front, slope, across, undoMessage: "Move Steps", thickness: kThickLine))
                {
                    var rise = Vector3.Dot(shifted, across) / math.max(0.01f, Vector3.Dot(up, across));
                    var steps = math.round(rise / stepHeight);
                    stairs.plateauHeight = math.clamp(baseHeight + steps * stepHeight, 0, height - stepHeight);
                }
            }

            bool baseFocused = false, topFocused = false;
            for (int i = 0; i < parts; i++)
            {
                var j = i + 1;
                if ((Point(j) - Point(i)).sqrMagnitude <= 0)
                    continue;
                var inward = Inward(i);
                // inside a curve, lines without a handle point on each short part
                var withPoint = isControlPoint[i] && isControlPoint[j % count];
                // inside a curve, the top at the back between its profiles
                if (!withPoint)
                {
                    handles.color = subtle;
                    handles.DrawLine(Offset(i, depth) + up * height, Offset(j, depth) + up * height, lineMode: LineMode.ZTest, thickness: 1.0f);
                    handles.color = color;
                }

                // the base alone: the steps share what's left of the height, so they change height, not number
                if (handles.DoEdgeHandle1DOffset(out Vector3 moved, Axis.Y, Point(i) + up * baseHeight, Point(j) + up * baseHeight, up, renderLine: withPoint, undoMessage: "Change Base Height", thickness: kThickLine))
                    SetBaseHeight(baseHeight + moved.y);
                baseFocused |= handles.lastHandleHadFocus;
                if (handles.DoEdgeHandle1DOffset(out moved, Axis.Z, Offset(i, depth), Offset(j, depth), inward, snappingStep: stairs.stepDepth, renderLine: withPoint, undoMessage: "Change Depth"))
                    SetSize(ref stairs.bounds, 2, math.max(stairs.stepDepth, depth + Vector3.Dot(moved, inward)));
                if (handles.DoEdgeHandle1DOffset(out moved, Axis.Z, Offset(i, run) + up * height, Offset(j, run) + up * height, inward, renderLine: withPoint, undoMessage: "Change Step Depth", thickness: kThickLine))
                    stairs.stepDepth = math.clamp(run + Vector3.Dot(moved, inward), ChiselLinearStairs.kMinStepDepth * stepCount, depth) / stepCount;
                topFocused |= handles.lastHandleHadFocus;
            }

            // the line being hovered or dragged lit up all along the path
            if (baseFocused || topFocused)
            {
                handles.color = handles.GetStateColor(color, hasFocus: true, isBackfaced: false);
                for (int i = 0; i < parts; i++)
                {
                    var j = i + 1;
                    if (baseFocused)
                        handles.DrawLine(Point(i) + up * baseHeight, Point(j) + up * baseHeight, lineMode: LineMode.NoZTest, thickness: 3.0f);
                    else
                        handles.DrawLine(Offset(i, run) + up * height, Offset(j, run) + up * height, lineMode: LineMode.NoZTest, thickness: 3.0f);
                }
            }
            handles.color = color;

            // the height: a point at the top in the middle of the path, dashed down to the floor, as a cylinder's
            // from the control points, so it doesn't move when a curve is cut into more parts
            var center = Vector3.zero;
            foreach (var controlPoint in shape.controlPoints)
                center += new Vector3(controlPoint.position.x, 0, controlPoint.position.y);
            center /= shape.controlPoints.Length;
            if (handles.generatorState is not IChiselNormalHandle topHandle)
                handles.generatorState = topHandle = handles.CreateNormalHandle(Vector3.zero, Vector3.up);
            topHandle.Origin = center + up * height;
            topHandle.Normal = up;
            handles.color = hidden;
            handles.DrawLine(center, center + up * height, lineMode: LineMode.NoZTest, thickness: 1.0f, dashSize: 2.0f);
            handles.color = color;
            var topPoint = center + up * height;
            if (handles.DoSlider1DHandle(ref topPoint, up, topHandle, snappingStep: stepHeight, undoMessage: "Change Height"))
                SetSize(ref stairs.bounds, 1, math.max(stepHeight, topPoint.y));

            // how high, how deep and how high the base is, where the first part of the path starts; with the base or the
            // top's line hovered, only what it changes
            var editing = handles.isEditingMeasurement;
            var showAll = editing || (!baseFocused && !topFocused);
            var typed = height;
            if (showAll && handles.DoDistanceMeasurement(ref typed, center, center + up * height, name: "Height"))
                SetSize(ref stairs.bounds, 1, math.max(stepHeight, typed));
            if (count > 1)
            {
                typed = depth;
                if (showAll && handles.DoDistanceMeasurement(ref typed, Point(0), Offset(0, depth), name: "Depth"))
                    SetSize(ref stairs.bounds, 2, math.max(stairs.stepDepth, typed));
                // the flat part at the top, from the back to where the steps start
                typed = depth - run;
                if ((editing || !baseFocused) && typed > 0 && handles.DoDistanceMeasurement(ref typed, Offset(0, run) + up * height, Offset(0, depth) + up * height, name: "Top Depth"))
                    stairs.SetTopDepth(typed);
                typed = baseHeight;
                if ((editing || !topFocused) && baseHeight > 0 && handles.DoDistanceMeasurement(ref typed, Point(0), Point(0) + up * baseHeight, name: "Base Height"))
                    SetBaseHeight(typed);
            }
        }

        // the base alone: the steps share what's left of the height, so they change height, not number
        public void SetBaseHeight(float baseHeight)
        {
            ref var stairs  = ref settings.stairs;
            var stepCount   = stairs.StepCount;
            var height      = stairs.AbsHeight;
            var newBase     = math.clamp(baseHeight, 0, height - ChiselLinearStairs.kMinStepHeight * stepCount);
            stairs.plateauHeight = newBase;
            stairs.stepHeight    = (height - newBase) / stepCount;
        }

        // the middle of the path on the floor: where the stairs' own position is shown, to move them by
        public Vector3 PathCenter
        {
            get
            {
                var center = Vector3.zero;
                if (shape == null || shape.controlPoints == null || shape.controlPoints.Length == 0)
                    return center;
                foreach (var point in shape.controlPoints)
                    center += new Vector3(point.position.x, 0, point.position.y);
                return center / shape.controlPoints.Length;
            }
        }

        // a size is changed by moving its far side only, keeping its sign
        static void SetSize(ref MinMaxAABB bounds, int axis, float size)
        {
            var max = bounds.Max;
            max[axis] = bounds.Min[axis] + size * (bounds.Max[axis] < bounds.Min[axis] ? -1 : 1);
            bounds.Max = max;
        }
    }
}