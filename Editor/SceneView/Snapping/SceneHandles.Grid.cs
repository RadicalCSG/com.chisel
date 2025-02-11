using System;
using System.Collections.Generic;
using UnityEngine;
using Chisel.Core;
using UnityEngine.Pool;

namespace Chisel.Editors
{
    public enum SnapResult3D
    {
        None	= 0,

        MinX	= 1,
        MaxX	= 2,

        MinY	= 4,
        MaxY	= 8,

        MinZ	= 16,
        MaxZ	= 32,

        PivotX	= 64,
        PivotY	= 128,
        PivotZ	= 256,

        CustomX	= 512,
        CustomY = 1024,
        CustomZ = 2048
    }
    
    public enum SnapResult1D
    {
        None	= 0,
        Min		= 1,
        Max		= 2,
        Pivot	= 4,
        Custom  = 8
    }

    public class Grid
    {
        public static event Action GridModified;

        const float kMinSpacing = (1 / 8192.0f);


        readonly static Matrix4x4 s_XZPlane = new(new Vector4(1, 0, 0, 0),
                                                  new Vector4(0, 1, 0, 0),
                                                  new Vector4(0, 0, 1, 0),
                                                  new Vector4(0, 0, 0, 1));

        readonly static Matrix4x4 s_YZPlane = new(new Vector4(0, 1, 0, 0),
                                                  new Vector4(1, 0, 0, 0),
                                                  new Vector4(0, 0, 1, 0),
                                                  new Vector4(0, 0, 0, 1));

        readonly static Matrix4x4 s_XYPlane = new(new Vector4(1, 0, 0, 0),
                                                  new Vector4(0, 0, 1, 0),
                                                  new Vector4(0, 1, 0, 0),
                                                  new Vector4(0, 0, 0, 1));
        public static Matrix4x4 XZPlane => s_XZPlane;

        public static Matrix4x4 YZPlane => s_YZPlane;

        public static Matrix4x4 XYPlane => s_XYPlane;

		public Grid() { }
        public Grid(Matrix4x4 gridToWorldSpace, Vector3 spacing) { this.GridToWorldSpace = gridToWorldSpace; this.Spacing = spacing; }
        public Grid(Matrix4x4 gridToWorldSpace) { this.GridToWorldSpace = gridToWorldSpace; this.Spacing = DefaultGrid.Spacing; }

        readonly static Grid s_DefaultGrid = new();
        public static Grid DefaultGrid => s_DefaultGrid;

		static Grid s_CurrentGrid = null;
        public static Grid CurrentGrid
        {
            get
            {
                return s_CurrentGrid;
            }
            set
            {
                if (value == s_CurrentGrid)
                    return;
                s_CurrentGrid = value;
                GridModified?.Invoke();
            }
		}

		public static Grid DebugGrid { get; set; }


		public Grid GridYZ
        {
            get { return new Grid(gridToWorldSpace * YZPlane, new Vector3(spacing.y, spacing.x, spacing.z)); }
        }

        public Grid GridXY
        {
            get { return new Grid(gridToWorldSpace * XYPlane, new Vector3(spacing.x, spacing.z, spacing.y)); }
        }



        public static Grid HoverGrid { get; set; }

		public static Grid ActiveGrid
        {
            get
            {
                if (s_CurrentGrid == null)
                    return DefaultGrid;
                return s_CurrentGrid;
            }
        }

        private static bool s_Enabled = false;
        public static bool Enabled
        {
            get
            {
                return s_Enabled;
            }

            set
            {
                s_Enabled = value;
            }
        }

        public bool Hide { get; internal set; }

        Vector3 spacing = Vector3.one;
        public Vector3 Spacing
        {
            get
            {
                return spacing;
            }
            set
            {
                var spacingX = UnityEngine.Mathf.Max(kMinSpacing, value.x);
                var spacingY = UnityEngine.Mathf.Max(kMinSpacing, value.y);
                var spacingZ = UnityEngine.Mathf.Max(kMinSpacing, value.z);
                if (spacing.x == spacingX && 
                    spacing.y == spacingY && 
                    spacing.z == spacingZ)
                    return;
                spacing.x = spacingX;
                spacing.y = spacingY;
                spacing.z = spacingZ;
                if (this == ActiveGrid)
                    GridModified?.Invoke();
            }
        }
        public float SpacingX
        {
            get { return spacing.x; }
            set
            {
                var spacingX = UnityEngine.Mathf.Max(kMinSpacing, value); ;
                if (spacing.x == spacingX)
                    return;
                spacing.x = spacingX;
                if (this == ActiveGrid)
                    GridModified?.Invoke();
            }
        }
        public float SpacingY
        {
            get { return spacing.y; }
            set
            {
                var spacingY = UnityEngine.Mathf.Max(kMinSpacing, value);
                if (spacing.y == spacingY)
                    return;
                spacing.y = spacingY;
                if (this == ActiveGrid)
                    GridModified?.Invoke();
            }
        }
        public float SpacingZ
        {
            get { return spacing.z; }
            set
            {
                var spacingZ = UnityEngine.Mathf.Max(kMinSpacing, value);
                if (spacing.z == spacingZ)
                    return;
                spacing.z = spacingZ;
                if (this == ActiveGrid)
                    GridModified?.Invoke();
            }
        }

        Matrix4x4 gridToWorldSpace = Matrix4x4.identity;
        Matrix4x4 worldToGridSpace = Matrix4x4.identity;

        public Matrix4x4 GridToWorldSpace
        {
            get
            {
                return gridToWorldSpace;
            }
            set
            {
                if (gridToWorldSpace == value)
                    return;
                gridToWorldSpace = value;
                worldToGridSpace = Matrix4x4.Inverse(gridToWorldSpace);
                if (this == ActiveGrid)
                    GridModified?.Invoke();
            }
        }

        public Matrix4x4 WorldToGridSpace
        {
            get
            {
                return worldToGridSpace;
            }
            set
            {
                if (worldToGridSpace == value)
                    return;
                worldToGridSpace = value;
                gridToWorldSpace = Matrix4x4.Inverse(worldToGridSpace);
                if (this == ActiveGrid)
                    GridModified?.Invoke();
            }
        }
        
        public Vector3	Center
        {
            get
            {
                var center = (Vector3)gridToWorldSpace.GetColumn(3);
                return center;
            }
        }
        
        public Vector3 Up
        {
            get
            {
                return (Vector3)gridToWorldSpace.GetColumn(1);
            }
        }

        public Vector3 Right
        {
            get
            {
                return (Vector3)gridToWorldSpace.GetColumn(0);
            }
        }

        public Vector3 Forward
        {
            get
            {
                return (Vector3)gridToWorldSpace.GetColumn(2);
            }
        }

        public Plane PlaneXZ
        {
            get
            {
                return new Plane(Up, Center);
            }
        }

        public Plane PlaneYZ
        {
            get
            {
                return new Plane(Right, Center);
            }
        }

        public Plane PlaneXY
        {
            get
            {
                return new Plane(Forward, Center);
            }
        }

        public Axes GetTangentAxesForAxis(Axis axis)
        {
            switch (axis)
            {
                case Axis.X: return Axes.YZ; 
                default:
                case Axis.Y: return Axes.XZ; 
                case Axis.Z: return Axes.XY; 
            }
        }

        public Axes GetTangentAxesForAxis(Axis axis, out Vector3 slideDir1, out Vector3 slideDir2)
        {
            switch (axis)
            {
                case Axis.X: { slideDir1 = Forward; slideDir2 = Up;      return Axes.YZ; }
                default:
                case Axis.Y: { slideDir1 = Right;   slideDir2 = Forward; return Axes.XZ; }
                case Axis.Z: { slideDir1 = Right;   slideDir2 = Up;      return Axes.XY; }
            }
        }

        public Axis GetClosestAxis(Vector3 vector)
        {
            var vector_x = Right;
            var vector_y = Up;
            var vector_z = Forward;

            var dot_x = Mathf.Abs(Vector3.Dot(vector_x, vector));
            var dot_y = Mathf.Abs(Vector3.Dot(vector_y, vector));
            var dot_z = Mathf.Abs(Vector3.Dot(vector_z, vector));

            if (dot_x > dot_y)
            {
                if (dot_x > dot_z)
                    return Axis.X;
            } else
            if (dot_y > dot_z)
                return Axis.Y;
            return Axis.Z;
        }

        public Vector3 GetClosestAxisVector(Vector3 vector)
        {
            var vector_x = Right;
            var vector_y = Up;
            var vector_z = Forward;

            var dot_x = Vector3.Dot(vector_x, vector);
            var dot_y = Vector3.Dot(vector_y, vector);
            var dot_z = Vector3.Dot(vector_z, vector);
            var a_dot_x = Mathf.Abs(dot_x);
            var a_dot_y = Mathf.Abs(dot_y);
            var a_dot_z = Mathf.Abs(dot_z);

            if (a_dot_x > a_dot_y)
            {
                if (a_dot_x > a_dot_z)
                    return (dot_x < 0 ? -1 : 1) * (Vector3)gridToWorldSpace.GetColumn(0);
            } else
            if (a_dot_y > a_dot_z)
            {
                return (dot_y < 0 ? -1 : 1) * (Vector3)gridToWorldSpace.GetColumn(1);
            }
            return (dot_z < 0 ? -1 : 1) * (Vector3)gridToWorldSpace.GetColumn(2);
        }

        public Vector3 GetAxisVector(Axis axis)
        {
            switch (axis)
            {
                default:
                case Axis.X: return (Vector3)gridToWorldSpace.GetColumn(0);
                case Axis.Y: return (Vector3)gridToWorldSpace.GetColumn(1);
                case Axis.Z: return (Vector3)gridToWorldSpace.GetColumn(2);
            }
        }

        public float GetAxisSnapping(Axis axis)
        {
            switch (axis)
            {
                default:
                case Axis.X: return spacing.x;
                case Axis.Y: return spacing.y;
                case Axis.Z: return spacing.z;
            }
        }

        public Grid Transform(Matrix4x4 matrix)
        {
            var activeGrid = ActiveGrid;
            return new Grid(activeGrid.GridToWorldSpace * matrix, activeGrid.Spacing);
        }

        public Extents3D GetGridExtentsOfPointArray(Matrix4x4 localToWorldMatrix, Vector3[] points)
        {
            var toMatrix = worldToGridSpace * localToWorldMatrix;

            var min = new Vector3(float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity);
            var max = new Vector3(float.NegativeInfinity, float.NegativeInfinity, float.NegativeInfinity);
            for (var i = 0; i < points.Length; i++)
            {
                var distance = toMatrix.MultiplyPoint(points[i]);
                min.x = Mathf.Min(min.x, distance.x);
                min.y = Mathf.Min(min.y, distance.y);
                min.z = Mathf.Min(min.z, distance.z);

                max.x = Mathf.Max(max.x, distance.x);
                max.y = Mathf.Max(max.y, distance.y);
                max.z = Mathf.Max(max.z, distance.z);
            }
            return new Extents3D(min, max);
        }
        
        public Vector3 SnapExtents3D(Extents3D extentsInGridSpace, Vector3 worldCurrentPosition, Vector3 worldStartPosition, Grid worldSlideGrid, out SnapResult3D snapResult, Axes enabledAxes = Axes.XYZ, bool ignoreStartPoint = false)
		{
			var customSnapPoints = ListPool<Vector3>.Get();
			var customDistances = ListPool<Vector3>.Get();
            try
            { 
			    customSnapPoints.Clear();
                // TODO: have a method that handles multiple dimensions at the same time
                var haveCustomSnapping = Snapping.GetCustomSnappingPoints(worldStartPosition, worldCurrentPosition, worldSlideGrid, 0, customSnapPoints);
            
                var boundsActive    = Snapping.BoundsSnappingActive;
                var pivotActive     = Snapping.PivotSnappingActive;

                snapResult = SnapResult3D.None;
                if (!boundsActive && !pivotActive && !haveCustomSnapping)
                    return worldCurrentPosition;

                const float kMinPointSnap = 0.25f;
                float minPointSnap = !(boundsActive || pivotActive) ? kMinPointSnap : float.PositiveInfinity;


                var offsetInWorldSpace		= worldCurrentPosition - worldStartPosition;
                var offsetInGridSpace		= worldToGridSpace.MultiplyVector(offsetInWorldSpace);
                var pivotInGridSpace		= worldToGridSpace.MultiplyVector(worldCurrentPosition - Center);
            
                // Snap our extents in grid space
                var movedExtentsInGridspace	= extentsInGridSpace + offsetInGridSpace;


                var snappedOffset       = Vector3.zero;
                var absSnappedOffset    = new Vector3(float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity);

                var enabledAxisLookup   = new[] { (enabledAxes & Axes.X) > 0, (enabledAxes & Axes.Y) > 0, (enabledAxes & Axes.Z) > 0 };

                var quantized_pivot         = new Vector3(float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity);
                var quantized_min_extents   = new Vector3(float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity);
                var quantized_max_extents   = new Vector3(float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity);

                for (int i = 0; i < 3; i++)
                {
                    if (!enabledAxisLookup[i])
                        continue;

                    if (pivotActive)
                    {
                        (float abs_pivot_offset, float snappedPivot, float quantized_offset) = Snapping.SnapPoint(pivotInGridSpace[i], spacing[i]);
                        quantized_pivot[i] = quantized_offset;
                        if (absSnappedOffset[i] > abs_pivot_offset) { absSnappedOffset[i] = abs_pivot_offset; snappedOffset[i] = snappedPivot; }
                    }

                    if (boundsActive)
                    {
                        (float abs_bounds_distance, float snappedBoundsOffset, float quantized_min, float quantized_max) = Snapping.SnapBounds(movedExtentsInGridspace[i], spacing[i]);
                        quantized_min_extents[i] = quantized_min;
                        quantized_max_extents[i] = quantized_max;

                        if (absSnappedOffset[i] > abs_bounds_distance) { absSnappedOffset[i] = abs_bounds_distance; snappedOffset[i] = snappedBoundsOffset; }
                    }
                }

                if (haveCustomSnapping)
                {
                    (Vector3 abs_distance, Vector3 snappedCustomOffset) = Snapping.SnapCustom(customSnapPoints, pivotInGridSpace, enabledAxes, minPointSnap, customDistances);
                    if (absSnappedOffset.sqrMagnitude > abs_distance.sqrMagnitude) { absSnappedOffset = abs_distance; snappedOffset = snappedCustomOffset; }
                }

                // Snap against drag start position
                if (!ignoreStartPoint)
                {
                    if (Mathf.Abs(snappedOffset.x) > Mathf.Abs(offsetInGridSpace.x)) { offsetInGridSpace.x = snappedOffset.x = 0; }
                    if (Mathf.Abs(snappedOffset.y) > Mathf.Abs(offsetInGridSpace.y)) { offsetInGridSpace.y = snappedOffset.y = 0; }
                    if (Mathf.Abs(snappedOffset.z) > Mathf.Abs(offsetInGridSpace.z)) { offsetInGridSpace.z = snappedOffset.z = 0; }
                }

                var quantizedOffset = new Vector3(SnappingUtility.Quantize(snappedOffset.x),
                                                  SnappingUtility.Quantize(snappedOffset.y),
                                                  SnappingUtility.Quantize(snappedOffset.z));

                // Figure out what kind of snapping visualization to show, this needs to be done afterwards since 
                // while we're snapping each type of snap can override the next one. 
                // Yet at the same time it's possible to snap with multiple snap-types at the same time.

                if (boundsActive)
                {
                    if (quantized_min_extents.x == quantizedOffset.x) snapResult |= SnapResult3D.MinX;
                    if (quantized_max_extents.x == quantizedOffset.x) snapResult |= SnapResult3D.MaxX;

                    if (quantized_min_extents.y == quantizedOffset.y) snapResult |= SnapResult3D.MinY;
                    if (quantized_max_extents.y == quantizedOffset.y) snapResult |= SnapResult3D.MaxY;

                    if (quantized_min_extents.z == quantizedOffset.z) snapResult |= SnapResult3D.MinZ;
                    if (quantized_max_extents.z == quantizedOffset.z) snapResult |= SnapResult3D.MaxZ;
                }

                if (pivotActive)
                {
                    if (quantized_pivot.x == quantizedOffset.x) snapResult |= SnapResult3D.PivotX;
                    if (quantized_pivot.y == quantizedOffset.y) snapResult |= SnapResult3D.PivotY;
                    if (quantized_pivot.z == quantizedOffset.z) snapResult |= SnapResult3D.PivotZ;
                }

                if (haveCustomSnapping) 
                    Snapping.SendCustomSnappedEvents(quantizedOffset, customDistances, 0);

                if (absSnappedOffset.x == 0 &&
                    absSnappedOffset.y == 0 &&
                    absSnappedOffset.z == 0)
                    return worldStartPosition;

                var snappedOffsetInWorldSpace	= gridToWorldSpace.MultiplyVector(offsetInGridSpace - snappedOffset);
                var snappedPositionInWorldSpace	= (worldStartPosition + snappedOffsetInWorldSpace);

                //Debug.Log($"{(float3)snappedOffsetInWorldSpace} {(float3)snappedOffset} {(float3)snappedPositionInWorldSpace}");

                return snappedPositionInWorldSpace;
            }
            finally
			{
				ListPool<Vector3>.Release(customSnapPoints);
				ListPool<Vector3>.Release(customDistances);
			}
        }
    }
}
