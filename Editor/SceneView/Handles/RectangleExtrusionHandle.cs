using System.Collections.Generic;
using Chisel.Core;
using Chisel.Components;
using UnityEngine;
using Grid = Chisel.Editors.Grid;

namespace Chisel.Editors
{
#if UNITY_6000_7_OR_NEWER
    [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
#endif
    public static class RectangleExtrusionHandle
    {
        static Matrix4x4		s_Transformation = Matrix4x4.identity;
        static ChiselModelComponent		s_ModelBeneathCursor;
        static List<Vector3>	s_Points = new();
        static bool             s_ModifyMode = false;
        static float            s_LastHeight = 1.0f;
        static float            s_NextHeight = 1.0f;
        static float            s_DefaultHeight = 0;
        
        public static void Reset()
        {
            s_Points.Clear();
            s_Transformation = Matrix4x4.identity;
            s_ModelBeneathCursor = null;
            s_ModifyMode = false;
            s_DefaultHeight = 0;
            s_NextHeight = 0;
            GUIUtility.hotControl = 0;
            PointDrawing.Reset();
        }

        static Bounds GetBounds(PlacementFlags flags, Axis upAxis)
        {
            if (s_Points.Count == 0) return new Bounds();
            
            var bounds = new Bounds( s_Points[0], Vector3.zero);
            if (s_Points.Count == 1) return bounds;

            if ((flags & PlacementFlags.SameLengthXZ) == PlacementFlags.SameLengthXZ)
            {
                var pt0 = s_Points[0];
                var pt1 = s_Points[1];
                var xDelta = pt1.x - pt0.x;
                var zDelta = pt1.z - pt0.z;
                var xAbsDelta = Mathf.Abs(xDelta);
                var zAbsDelta = Mathf.Abs(zDelta);
                if (xAbsDelta != zAbsDelta)
                {
                    if (xAbsDelta > zAbsDelta)
                    {
                        zDelta = xAbsDelta * Mathf.Sign(zDelta);
                        pt1.z = pt0.z + zDelta;
                    } else
                    {
                        xDelta = zAbsDelta * Mathf.Sign(xDelta);
                        pt1.x = pt0.x + xDelta;
                    }
                    s_Points[1] = pt1;
                }
            }
            if ((flags & PlacementFlags.GenerateFromCenterXZ) == PlacementFlags.GenerateFromCenterXZ)
            {
                var radius = s_Points[1] - s_Points[0];
                bounds.Encapsulate(s_Points[0] - radius);
                bounds.Encapsulate(s_Points[0] + radius);
            } else
                bounds.Encapsulate(s_Points[1]);

            var height = GetHeight(flags, upAxis);
            if (height == 0)
                return bounds;

            var size = bounds.size;
            size[(int)upAxis] = height;
            bounds.size = size;
            
            if ((flags & PlacementFlags.GenerateFromCenterY) != PlacementFlags.GenerateFromCenterY)
            {
                var center = bounds.center;
                center[(int)upAxis] += height * 0.5f;
                bounds.center = center;
            }
            return bounds;
        }

        static float GetHeight(PlacementFlags flags, Axis upAxis)
        {
            if (s_Points.Count <= 1) 
                return 0;

            if ((flags & PlacementFlags.HeightEqualsHalfXZ) == PlacementFlags.HeightEqualsHalfXZ ||
                (flags & PlacementFlags.HeightEqualsXZ    ) == PlacementFlags.HeightEqualsXZ)
            {
                var heightMultiplier = ((flags & PlacementFlags.HeightEqualsHalfXZ) == PlacementFlags.HeightEqualsHalfXZ) ? 0.5f : 1.0f;

                var axis1 = (((int)upAxis) + 1) % 3;
                var axis2 = (((int)upAxis) + 2) % 3;

                if ((flags & PlacementFlags.GenerateFromCenterXZ) == PlacementFlags.GenerateFromCenterXZ)
                    heightMultiplier *= 2;

                var length1 = Mathf.Abs((s_Points[0] - s_Points[1])[(int)axis1]);
                var length2 = Mathf.Abs((s_Points[0] - s_Points[1])[(int)axis2]);

                s_DefaultHeight = s_NextHeight = Mathf.Min(length1, length2) * heightMultiplier;
                return s_DefaultHeight;
            }

            if (s_Points.Count <= 2)
            {
                if ((flags & PlacementFlags.UseLastHeight) == PlacementFlags.UseLastHeight)
                    s_DefaultHeight = s_NextHeight = s_LastHeight;
                return s_DefaultHeight;
            }

            if ((flags & PlacementFlags.UseLastHeight) == PlacementFlags.UseLastHeight)
            {
                s_DefaultHeight = s_NextHeight = s_LastHeight;
            } else
                s_DefaultHeight = s_NextHeight = (s_Points[2] - s_Points[1])[(int)upAxis];
            return s_DefaultHeight;
        }

        public static GeneratorModeState Do(Rect dragArea, out Bounds bounds, out float height, out ChiselModelComponent modelBeneathCursor, out Matrix4x4 transformation, PlacementFlags flags, Axis upAxis, float? snappingSteps = null)
        {

            bool doCommit = false;
            try
            {
                if (SceneHandles.InCameraOrbitMode)
                    return GeneratorModeState.None;

                height = GetHeight(flags, upAxis);
                if (s_Points.Count <= 2)
                {
                    PointDrawing.PointDrawHandle(dragArea, ref s_Points, out s_Transformation, out s_ModelBeneathCursor, releaseOnMouseUp: false, Chisel.Editors.SceneHandles.OutlinedDotHandleCap);
                    if (s_Points.Count <= 1)
                        return GeneratorModeState.None;
                }
                if (s_Points.Count > 2)
                {
                    if (!s_ModifyMode)
                    {
                        PointDrawing.Release();
                        if ((flags & PlacementFlags.HeightEqualsHalfXZ) == PlacementFlags.HeightEqualsHalfXZ ||
                            (flags & PlacementFlags.HeightEqualsXZ) == PlacementFlags.HeightEqualsXZ ||
                            (flags & PlacementFlags.UseLastHeight) == PlacementFlags.UseLastHeight)
                        {
                            if (height > 0)
                            {
                                s_ModifyMode = false;
                                doCommit = true;
                                return GeneratorModeState.Commit;
                            }
                        }
                        
                        s_Points[2] = s_Points[1];
                        s_ModifyMode = true;
                        return GeneratorModeState.Update;
                    }
                    if (s_ModifyMode)
                    {
                        var oldMatrix = UnityEditor.Handles.matrix;
                        UnityEditor.Handles.matrix *= s_Transformation;
                        var tempPoint = s_Points[2];
                        var extrusionState = ExtrusionHandle.DoHandle(ref tempPoint, upAxis, snappingSteps: snappingSteps);
                        s_Points[2] = tempPoint;
                        UnityEditor.Handles.matrix = oldMatrix;

                        switch (extrusionState)
                        {
                            case ExtrusionState.Cancel:     { s_ModifyMode = false; return GeneratorModeState.Cancel; }
                            case ExtrusionState.Commit:     
                            { 
                                s_ModifyMode = false; 
                                doCommit = true;
                                return GeneratorModeState.Commit; 
                            }
                        }
                    }
                }
                return GeneratorModeState.Update;
            }
            finally
            {
                modelBeneathCursor  = s_ModelBeneathCursor;
                bounds			    = GetBounds(flags, upAxis);
                height			    = bounds.size[(int)upAxis];

                var center          = bounds.center;
                if ((flags & PlacementFlags.GenerateFromCenterY) != PlacementFlags.GenerateFromCenterY)
                    center[(int)upAxis] -= height * 0.5f;

                transformation      = s_Transformation * Matrix4x4.TRS(center, Quaternion.identity, Vector3.one);
                // which way it was drawn out of the surface, in the frame (turned with it)
                var extrusion = Vector3.zero;
                extrusion[(int)upAxis] = 1;
#if true
                //if (height > 0)
                {
                    if ((flags & PlacementFlags.AlwaysFaceUp) == PlacementFlags.AlwaysFaceUp)
                        ChiselPlacementTurn.FaceUp(ref transformation, ref bounds, ref extrusion, Grid.ActiveGrid.Up);

                    if (!s_ModelBeneathCursor &&
                        (flags & PlacementFlags.AlwaysFaceCameraXZ) == PlacementFlags.AlwaysFaceCameraXZ)
                    {
                        var currentForward  = transformation.MultiplyVector(Vector3.forward);
                        var currentRight    = transformation.MultiplyVector(Vector3.right);
                        var cameraOffset    = Camera.current.transform.forward;
                        var cameraForward   = (new Vector3(cameraOffset.x, 0, cameraOffset.z)).normalized;
                        var dotZ = Vector3.Dot(currentForward, cameraForward);
                        var dotX = Vector3.Dot(currentRight,   cameraForward);

                        var angle = 0;
                        if (Mathf.Abs(dotX) < Mathf.Abs(dotZ))
                        {
                            if (dotZ > 0)
                                angle += 180;
                        } else
                        {
                            if (dotX < 0)
                                angle += 90;
                            else
                                angle -= 90;
                            
                            var size = bounds.size;
                            var t = size.x; size.x = size.z; size.z = t;
                            bounds.size = size;
                        }

                        var turn = Quaternion.Euler(0, angle, 0);
                        transformation *= Matrix4x4.Rotate(turn);
                        extrusion = Quaternion.Inverse(turn) * extrusion;
                    }
                }
#endif

                // the box from where it was drawn out, along the way it was drawn; the frame moved to the middle of its
                // bottom, so a generator built around its frame's middle is in the box also when the box was turned to
                // stand up (drawn out of a wall, its middle is away from the wall, its bottom below where it was drawn)
                if ((flags & PlacementFlags.GenerateFromCenterY) != PlacementFlags.GenerateFromCenterY)
                    ChiselPlacementTurn.FromBottomMiddle(ref transformation, ref bounds, extrusion, height);
                else
                    bounds.center = Vector3.zero;
                if (doCommit)
                {
                    s_LastHeight = s_NextHeight;
                }
            }
        }
    }
}
