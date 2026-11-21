using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

namespace Chisel.Core.Tests
{
    // Handles without a scene view, for a generator's edit handles and a placement tool's preview: what is drawn is kept
    // as lines (through the matrix), and one drag can be made along an axis: the first slider or direction handle that
    // moves along it is moved by an amount, and the handles are then modified
    sealed class FakeChiselHandles : IChiselHandles
    {
        public sealed class Handle : IChiselCircleHandle, IChiselEllipsoidHandle, IChiselLineHandle, IChiselLineLoopHandle, IChiselNormalHandle
        {
            public Color     Color       { get; set; }
            public Vector3   Center      { get; set; }
            public Vector3   Normal      { get; set; }
            public float     Diameter    { get; set; }
            public float     DiameterX   { get; set; }
            public float     DiameterZ   { get; set; }
            public float     Rotation    { get; set; }
            public float     StartAngle  { get; set; }
            public float     TotalAngles { get; set; }
            public float     DashSize    { get; set; }
            public Vector3   Outward     { get; set; }
            public Vector3   From        { get; set; }
            public Vector3   To          { get; set; }
            public Vector3[] Vertices    { get; set; }
            public int       Offset      { get; set; }
            public int       Count       { get; set; }
            public Vector3   Origin      { get; set; }

            public Vector3 GetPointAtDegree(float degree)
            {
                var radians = math.radians(degree);
                return Center + new Vector3(math.cos(radians) * DiameterX * 0.5f, 0, math.sin(radians) * DiameterZ * 0.5f);
            }

            public bool TryGetClosestPoint(out Vector3 closestPoint, bool interpolate = true) { closestPoint = Center; return false; }
        }

        public readonly List<(Vector3 from, Vector3 to)> lines = new();
        // where a 2D shape handle was put, if one was
        public Matrix4x4? shapeHandleTransformation;
        public Vector3 dragAxis;
        public float   dragAmount;
        bool dragged;
        // the path point handle, counted in the order they're asked for, that is moved to pathPointMovedTo
        public int             pathPointToMove = -1;
        public ChiselPathPoint pathPointMovedTo;
        int pathPointsAsked;

        // the first handle moving along the drag axis takes the drag
        bool TryDrag(Vector3 direction, out float along)
        {
            along = 0;
            if (dragged || dragAmount == 0 || direction.sqrMagnitude < 1e-12f)
                return false;
            direction.Normalize();
            if (Vector3.Cross(direction, dragAxis).sqrMagnitude > 1e-6f)
                return false;
            dragged  = true;
            modified = true;
            along    = Vector3.Dot(direction, dragAxis) * dragAmount;
            return true;
        }

        void Line(Vector3 from, Vector3 to) { lines.Add((matrix.MultiplyPoint(from), matrix.MultiplyPoint(to))); }

        // IChiselHandleRenderer
        public Matrix4x4 matrix        { get; set; } = Matrix4x4.identity;
        public Matrix4x4 inverseMatrix => matrix.inverse;
        public Color     color         { get; set; } = Color.white;
        public bool      disabled      => false;
        public bool      backfaced     { get; set; }
        public bool      IsIn2DMode    => false;
        public Color GetStateColor(Color baseColor, bool hasFocus, bool isBackfaced) { return baseColor; }
        public void RenderBox(Bounds bounds) { }
        public void RenderBoxMeasurements(Bounds bounds) { }
        public void RenderCylinder(Bounds bounds, int segments) { }
        public void RenderDistanceMeasurement(Vector3 from, Vector3 to) { }
        public void RenderDistanceMeasurement(Vector3 from, Vector3 to, float forceValue, string name = null) { }
        public void RenderDistanceMeasurement(Vector3 from, Vector3 to, float forceValue, string name, float secondValue, string secondName) { }
        public void RenderLabel(Vector3 position, Vector3 toward, string text) { }
        public void RenderShape(Curve2D shape, float height) { }
        public void DrawLine(Vector3 from, Vector3 to, LineMode lineMode = LineMode.NoZTest, float thickness = 1.0f, float dashSize = 0.0f) { Line(from, to); }
        public void DrawContinuousLines(Vector3[] points, LineMode lineMode = LineMode.NoZTest, float thickness = 1.0f, float dashSize = 0.0f) { DrawContinuousLines(points, 0, points.Length, lineMode, thickness, dashSize); }
        public void DrawContinuousLines(float3[] points, LineMode lineMode = LineMode.NoZTest, float thickness = 1.0f, float dashSize = 0.0f) { DrawContinuousLines(points, 0, points.Length, lineMode, thickness, dashSize); }
        public void DrawContinuousLines(Vector3[] points, int startIndex, int length, LineMode lineMode = LineMode.NoZTest, float thickness = 1.0f, float dashSize = 0.0f)
        {
            for (int i = startIndex + 1; i < startIndex + length; i++)
                Line(points[i - 1], points[i]);
        }
        public void DrawContinuousLines(float3[] points, int startIndex, int length, LineMode lineMode = LineMode.NoZTest, float thickness = 1.0f, float dashSize = 0.0f)
        {
            for (int i = startIndex + 1; i < startIndex + length; i++)
                Line(points[i - 1], points[i]);
        }
        public void DrawLineLoop(Vector3[] points, LineMode lineMode = LineMode.NoZTest, float thickness = 1.0f, float dashSize = 0.0f) { DrawLineLoop(points, 0, points.Length, lineMode, thickness, dashSize); }
        public void DrawLineLoop(float3[] points, LineMode lineMode = LineMode.NoZTest, float thickness = 1.0f, float dashSize = 0.0f) { DrawLineLoop(points, 0, points.Length, lineMode, thickness, dashSize); }
        public void DrawLineLoop(Vector3[] points, int startIndex, int length, LineMode lineMode = LineMode.NoZTest, float thickness = 1.0f, float dashSize = 0.0f)
        {
            for (int i = 0; i < length; i++)
                Line(points[startIndex + i], points[startIndex + (i + 1) % length]);
        }
        public void DrawLineLoop(float3[] points, int startIndex, int length, LineMode lineMode = LineMode.NoZTest, float thickness = 1.0f, float dashSize = 0.0f)
        {
            for (int i = 0; i < length; i++)
                Line(points[startIndex + i], points[startIndex + (i + 1) % length]);
        }

        // IChiselHandleAllocation
        public IChiselCircleHandle CreateCircleHandle(Vector3 center, Vector3 normal, float diameter, Color color, float startAngle = 0, float angles = 360, float dashSize = 0) { return CreateCircleHandle(center, normal, diameter, startAngle, angles, dashSize); }
        public IChiselCircleHandle CreateCircleHandle(Vector3 center, Vector3 normal, float diameter, float startAngle = 0, float angles = 360, float dashSize = 0)
        {
            return new Handle { Center = center, Normal = normal, Diameter = diameter, DiameterX = diameter, DiameterZ = diameter, StartAngle = startAngle, TotalAngles = angles, DashSize = dashSize };
        }
        public IChiselEllipsoidHandle CreateEllipsoidHandle(Vector3 center, Vector3 normal, float diameterX, float diameterZ, Color color, float rotation = 0, float startAngle = 0, float angles = 360, float dashSize = 0) { return CreateEllipsoidHandle(center, normal, diameterX, diameterZ, rotation, startAngle, angles, dashSize); }
        public IChiselEllipsoidHandle CreateEllipsoidHandle(Vector3 center, Vector3 normal, float diameterX, float diameterZ, float rotation = 0, float startAngle = 0, float angles = 360, float dashSize = 0)
        {
            return new Handle { Center = center, Normal = normal, DiameterX = diameterX, DiameterZ = diameterZ, Rotation = rotation, StartAngle = startAngle, TotalAngles = angles, DashSize = dashSize };
        }
        public IChiselLineHandle CreateLineHandle(Vector3 from, Vector3 to, Color color, float dashSize = 0, bool highlightOnly = false) { return CreateLineHandle(from, to, dashSize, highlightOnly); }
        public IChiselLineHandle CreateLineHandle(Vector3 from, Vector3 to, float dashSize = 0, bool highlightOnly = false) { return new Handle { From = from, To = to, DashSize = dashSize }; }
        public IChiselLineLoopHandle CreateLineLoopHandle(Vector3[] vertices, Color color, float dashSize = 0) { return CreateLineLoopHandle(vertices, 0, vertices.Length, dashSize); }
        public IChiselLineLoopHandle CreateLineLoopHandle(Vector3[] vertices, float dashSize = 0) { return CreateLineLoopHandle(vertices, 0, vertices.Length, dashSize); }
        public IChiselLineLoopHandle CreateLineLoopHandle(Vector3[] vertices, int offset, int count, Color color, float dashSize = 0) { return CreateLineLoopHandle(vertices, offset, count, dashSize); }
        public IChiselLineLoopHandle CreateLineLoopHandle(Vector3[] vertices, int offset, int count, float dashSize = 0) { return new Handle { Vertices = vertices, Offset = offset, Count = count, DashSize = dashSize }; }
        public IChiselNormalHandle CreateNormalHandle(Vector3 origin, Vector3 normal, Color color, float dashSize = 0) { return CreateNormalHandle(origin, normal, dashSize); }
        public IChiselNormalHandle CreateNormalHandle(Vector3 origin, Vector3 normal, float dashSize = 0) { return new Handle { Origin = origin, Normal = normal, DashSize = dashSize }; }

        // IChiselHandles
        public bool    modified             { get; private set; }
        public bool    lastHandleHadFocus   => false;
        public bool    lastHandleIsDragged  => false;
        public Vector3 moveSnappingSteps    => Vector3.zero;
        public object  generatorState       { get; set; }
        public Ray     mouseRay             => default;
        public bool    isEditingMeasurement => false;

        public bool TryGetClosestPoint(IChiselHandle[] handles, out Vector3 closestPoint, bool interpolate = true) { closestPoint = default; return false; }
        public bool TryGetClosestPoint(IChiselHandle handle, out Vector3 closestPoint, bool interpolate = true) { closestPoint = default; return false; }
        public void DoRenderHandles(IChiselHandle[] handles) { }

        public bool DoSlider1DHandle(ref Vector3 position, Vector3 direction, IChiselHandle[] handles, float snappingStep = 0, string undoMessage = null) { return DoDirectionHandle(ref position, direction); }
        public bool DoSlider1DHandle(ref Vector3 position, Vector3 direction, IChiselHandle handle, float snappingStep = 0, string undoMessage = null) { return DoDirectionHandle(ref position, direction); }
        public bool DoSlider1DHandle(ref float distance, Vector3 center, Vector3 direction, IChiselHandle[] handles, float snappingStep = 0, string undoMessage = null)
        {
            if (!TryDrag(direction, out var along))
                return false;
            distance += along;
            return true;
        }
        public bool DoSlider1DHandle(ref float distance, Vector3 center, Vector3 direction, IChiselHandle handle, float snappingStep = 0, string undoMessage = null) { return DoSlider1DHandle(ref distance, center, direction, (IChiselHandle[])null, snappingStep, undoMessage); }
        public bool DoDirectionHandle(ref Vector3 position, Vector3 direction, float snappingStep = 0, string undoMessage = null)
        {
            if (!TryDrag(direction, out _))
                return false;
            position += dragAxis * dragAmount;
            return true;
        }

        public bool DoCircleRotationHandle(ref float angle, Vector3 center, Vector3 normal, IChiselHandle handle, string undoMessage = null) { return false; }
        public bool DoCircleRotationHandle(ref float angle, Vector3 center, Vector3 normal, IChiselHandle[] handles, string undoMessage = null) { return false; }
        public bool DoDistanceHandle(ref float distance, Vector3 center, Vector3 normal, IChiselHandle handle, string undoMessage = null) { return false; }
        public bool DoDistanceHandle(ref float distance, Vector3 center, Vector3 normal, IChiselHandle[] handles, string undoMessage = null) { return false; }
        public bool IsSufaceBackFaced(Vector3 point, Vector3 normal) { return false; }
        public bool DoBoundsHandle(ref Bounds bounds, Vector3? snappingSteps = null, string undoMessage = null, bool fixedBottom = false) { return false; }
        public bool DoShapeHandle(ref Curve2D shape, string undoMessage = null) { return false; }
        public bool DoShapeHandle(ref Curve2D shape, Matrix4x4 transformation, string undoMessage = null) { shapeHandleTransformation = transformation; return false; }
        public bool DoRadiusHandle(ref float outerRadius, Vector3 direction, Vector3 position, bool renderDisc = true, string undoMessage = null) { return false; }
        public bool DoRadius2DHandle(ref Vector3 radius1, ref Vector3 radius2, Vector3 center, Vector3 up, float minRadius1 = 0, float minRadius2 = 0, float maxRadius1 = float.PositiveInfinity, float maxRadius2 = float.PositiveInfinity, bool renderDisc = true, string undoMessage = null) { return false; }
        public bool DoRadius2DHandle(ref Vector3 radius, Vector3 center, Vector3 up, float minRadius = 0, float maxRadius = float.PositiveInfinity, bool renderDisc = true, string undoMessage = null) { return false; }
        public bool DoRotatableLineHandle(ref float angle, Vector3 origin, float diameter, Vector3 handleDir, Vector3 slideDir1, Vector3 slideDir2, string undoMessage = null, float lineStart = 0) { return false; }
        public bool DoAngleRingHandle(ref float angle, Vector3 center, Vector3 axis, Vector3 zeroDirection, float radius, string undoMessage = null, string name = null) { return false; }
        public bool DoTurnHandle(ref Bounds bounds, string undoMessage = null) { return false; }
        public bool DoBoxMeasurements(ref Bounds bounds, string undoMessage = null) { return false; }
        public bool DoDistanceMeasurement(ref float distance, Vector3 from, Vector3 to, string undoMessage = null, Vector3 offsetDirection = default, string name = null) { return false; }
        public bool DoEdgeHandle1D(out float offset, Axis axis, Vector3 from, Vector3 to, float snappingStep = 0, float handleSize = 0, string undoMessage = null, float thickness = 3.0f) { offset = 0; return false; }
        public bool DoEdgeHandle1DOffset(out Vector3 offset, Axis axis, Vector3 from, Vector3 to, Vector3 direction, float snappingStep = 0, float handleSize = 0, bool renderLine = true, string undoMessage = null, float thickness = 3.0f) { offset = default; return false; }
        public bool DoEdgeHandle1DOffset(out float offset, Axis axis, Vector3 from, Vector3 to, float snappingStep = 0, float handleSize = 0, bool renderLine = true, string undoMessage = null) { offset = 0; return false; }
        public bool DoMoveGizmo(Vector3 point) { return false; }
        public bool DoPathPointHandle(ref ChiselPathPoint pathPoint, string undoMessage = null)
        {
            if (pathPointsAsked++ != pathPointToMove)
                return false;
            pathPoint = pathPointMovedTo;
            modified  = true;
            return true;
        }
        public bool DoPositionHandle(ref Vector3 position, Quaternion rotation, string undoMessage = null) { return false; }
        public bool DoRotationHandle(ref Quaternion rotation, Vector3 position, string undoMessage = null) { return false; }
        public bool DoPlanarScaleHandle(ref Vector2 scale2D, Vector3 position, Quaternion rotation, string undoMessage = null) { return false; }
    }
}
