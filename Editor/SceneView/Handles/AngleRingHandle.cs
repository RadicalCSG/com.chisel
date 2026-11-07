using UnityEditor;
using UnityEngine;
using Chisel.Core;
using Snapping = Chisel.Editors.Snapping;

namespace Chisel.Editors
{
    // An angle around an axis: a knob on a dashed ring just outside a radius, with curved arrows on either side of it.
    // The knob is dragged around the ring; while it is, the ring from zero to the angle is drawn solid, with the angle
    // next to the knob
#if UNITY_6000_7_OR_NEWER
    [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
#endif
    public static class AngleRingHandle
    {
        readonly static int kAngleRingHash = "AngleRing".GetHashCode();

        // in screen size, as parts of the handle size
        const float kRingOffset     = 0.25f;
        const float kKnobSize       = 0.075f;
        const float kArrowGap       = 0.12f;
        const float kArrowLength    = 0.3f;
        const float kArrowHeadSize  = 0.06f;
        const float kZeroTickSize   = 0.12f;
        const float kLabelGap       = 0.03f;
        const float kKnobOutline    = 1.3f;
        const int   kArcPoints      = 16;
        const int   kRingPoints     = 96;

        // in pixels; every line has a dark edge around it so it reads on any surface
        const float kRingWidth      = 3.0f;
        const float kArrowWidth     = 4.5f;
        const float kHotArrowWidth  = 5.5f;
        const float kSweepWidth     = 4.5f;
        const float kOutlineWidth   = 2.5f;
        readonly static Color kOutlineColor = new(0, 0, 0, 0.7f);

        // the drag is kept turning past a full turn: each mouse move adds the smallest turn from where it was
        static float s_StartAngle;
        static float s_Turned;
        static float s_PrevMouseAngle;

        readonly static Vector3[] s_ArcPoints  = new Vector3[kArcPoints];
        readonly static Vector3[] s_RingPoints = new Vector3[kRingPoints + 1];

        // the point at an angle around the axis, at zero along zeroDirection, turning as Quaternion.AngleAxis does
        public static Vector3 PointAt(Vector3 center, Vector3 axis, Vector3 zeroDirection, float radius, float angle)
        {
            return center + (Quaternion.AngleAxis(angle, axis) * zeroDirection.normalized) * radius;
        }

        // the angle of a point around the axis, from -180 to 180
        public static float AngleOf(Vector3 center, Vector3 axis, Vector3 zeroDirection, Vector3 point)
        {
            var flat = Vector3.ProjectOnPlane(point - center, axis);
            if (flat.sqrMagnitude < 1e-12f)
                return 0;
            return MathExtensions.SignedAngle(zeroDirection.normalized, flat.normalized, axis.normalized);
        }

        // how far a drag has turned after the mouse moved from one angle to another, going the short way
        public static float AddTurn(float turned, float previousMouseAngle, float mouseAngle)
        {
            return turned + Mathf.DeltaAngle(previousMouseAngle, mouseAngle);
        }

        // the angle a drag ends on, snapped when rotation snapping is on
        public static float Snap(float angle, bool snapping, float snapStep)
        {
            if (!snapping || snapStep <= 0)
                return angle;
            return Mathf.Round(angle / snapStep) * snapStep;
        }

        // away from the axis through the knob, flat on the ring's plane
        public static Vector3 LabelDirection(Vector3 center, Vector3 axis, Vector3 knob)
        {
            var outward = Vector3.ProjectOnPlane(knob - center, axis);
            return outward.sqrMagnitude < 1e-12f ? Vector3.zero : outward.normalized;
        }

        // what the label shows
        public static string AngleText(float angle, string name = null)
        {
            var text = Units.ToAngleString(Mathf.Round(angle * 10) / 10).Trim();
            return string.IsNullOrEmpty(name) ? text : text + " (" + name + ")";
        }

        public static float DoHandle(int id, float angle, Vector3 center, Vector3 axis, Vector3 zeroDirection, float radius, string name = null)
        {
            axis = axis.normalized;
            zeroDirection = Vector3.ProjectOnPlane(zeroDirection, axis).normalized;

            var handleSize  = UnityEditor.HandleUtility.GetHandleSize(center);
            var ringRadius  = radius + handleSize * kRingOffset;
            var knob        = PointAt(center, axis, zeroDirection, ringRadius, angle);
            var knobSize    = UnityEditor.HandleUtility.GetHandleSize(knob) * kKnobSize;
            var evt         = Event.current;
            var hot         = GUIUtility.hotControl == id;

            switch (evt.GetTypeForControl(id))
            {
                case EventType.Layout:
                {
                    if (SceneHandles.InCameraOrbitMode)
                        break;
                    var distance = Mathf.Max(0, UnityEditor.HandleUtility.DistanceToCircle(knob, knobSize * 1.5f));
                    distance = Mathf.Min(distance, DistanceToArrow(center, axis, zeroDirection, ringRadius, angle, handleSize, +1));
                    distance = Mathf.Min(distance, DistanceToArrow(center, axis, zeroDirection, ringRadius, angle, handleSize, -1));
                    UnityEditor.HandleUtility.AddControl(id, distance);
                    break;
                }
                case EventType.MouseDown:
                {
                    if (GUIUtility.hotControl != 0 || evt.button != 0 || UnityEditor.HandleUtility.nearestControl != id ||
                        SceneHandles.InCameraOrbitMode)
                        break;
                    if (!TryGetMouseAngle(center, axis, zeroDirection, evt.mousePosition, out var mouseAngle))
                        break;
                    GUIUtility.hotControl = GUIUtility.keyboardControl = id;
                    s_StartAngle     = angle;
                    s_Turned         = 0;
                    s_PrevMouseAngle = mouseAngle;
                    evt.Use();
                    EditorGUIUtility.SetWantsMouseJumping(1);
                    break;
                }
                case EventType.MouseDrag:
                {
                    if (!hot)
                        break;
                    if (TryGetMouseAngle(center, axis, zeroDirection, evt.mousePosition, out var mouseAngle))
                    {
                        s_Turned         = AddTurn(s_Turned, s_PrevMouseAngle, mouseAngle);
                        s_PrevMouseAngle = mouseAngle;
                        var newAngle = Snap(s_StartAngle + s_Turned, Snapping.RotateSnappingActive, Snapping.RotateSnappingStep);
                        if (newAngle != angle)
                        {
                            angle = newAngle;
                            GUI.changed = true;
                        }
                    }
                    evt.Use();
                    break;
                }
                case EventType.MouseUp:
                {
                    if (!hot || (evt.button != 0 && evt.button != 2))
                        break;
                    GUIUtility.hotControl = 0;
                    evt.Use();
                    EditorGUIUtility.SetWantsMouseJumping(0);
                    break;
                }
                case EventType.Repaint:
                {
                    SceneHandles.SetCursor(id, SceneHandleUtility.GetCursorForCircleTangent(knob, axis));
                    Draw(id, angle, center, axis, zeroDirection, ringRadius, handleSize, knob, knobSize, hot, name);
                    break;
                }
            }
            return angle;
        }

        static bool TryGetMouseAngle(Vector3 center, Vector3 axis, Vector3 zeroDirection, Vector2 mousePosition, out float angle)
        {
            var worldRay = UnityEditor.HandleUtility.GUIPointToWorldRay(mousePosition);
            return TryGetRayAngle(Handles.inverseMatrix, worldRay, center, axis, zeroDirection, out angle);
        }

        // the angle around the axis where a ray in world space meets the ring's plane; the ring is in the space the
        // handles are drawn in, which inverseMatrix takes world space to
        public static bool TryGetRayAngle(Matrix4x4 inverseMatrix, Ray worldRay, Vector3 center, Vector3 axis, Vector3 zeroDirection, out float angle)
        {
            angle = 0;
            var ray   = new Ray(inverseMatrix.MultiplyPoint(worldRay.origin), inverseMatrix.MultiplyVector(worldRay.direction));
            var plane = new Plane(axis, center);
            if (!plane.Raycast(ray, out var enter))
                return false;
            angle = AngleOf(center, axis, zeroDirection, ray.GetPoint(enter));
            return true;
        }

        // an arc on the ring beside the knob, going away from it on one side
        static void GetArrowArc(Vector3 center, Vector3 axis, Vector3 zeroDirection, float ringRadius, float angle, float handleSize, int side)
        {
            var radius   = ringRadius;
            var toDegree = Mathf.Rad2Deg / Mathf.Max(1e-5f, radius);
            var gap      = handleSize * kArrowGap * toDegree;
            var length   = handleSize * kArrowLength * toDegree;
            var from     = angle + side * gap;
            for (int i = 0; i < kArcPoints; i++)
                s_ArcPoints[i] = PointAt(center, axis, zeroDirection, radius, from + side * length * i / (kArcPoints - 1));
        }

        static float DistanceToArrow(Vector3 center, Vector3 axis, Vector3 zeroDirection, float ringRadius, float angle, float handleSize, int side)
        {
            GetArrowArc(center, axis, zeroDirection, ringRadius, angle, handleSize, side);
            return UnityEditor.HandleUtility.DistanceToPolyLine(s_ArcPoints);
        }

        static void DrawArrow(Vector3 center, Vector3 axis, Vector3 zeroDirection, float ringRadius, float angle, float handleSize, int side, float width)
        {
            GetArrowArc(center, axis, zeroDirection, ringRadius, angle, handleSize, side);
            var tip      = s_ArcPoints[kArcPoints - 1];
            var back     = (s_ArcPoints[kArcPoints - 2] - tip).normalized;
            var across   = Vector3.Cross(axis, back).normalized;
            var headSize = handleSize * kArrowHeadSize;
            s_HeadPoints[0] = tip + (back + across) * headSize;
            s_HeadPoints[1] = tip;
            s_HeadPoints[2] = tip + (back - across) * headSize;

            var color = Handles.color;
            Handles.color = kOutlineColor;
            SceneHandles.DrawAAPolyLine(width + kOutlineWidth, s_ArcPoints);
            SceneHandles.DrawAAPolyLine(width + kOutlineWidth, s_HeadPoints);
            Handles.color = color;
            SceneHandles.DrawAAPolyLine(width, s_ArcPoints);
            SceneHandles.DrawAAPolyLine(width, s_HeadPoints);
        }
        readonly static Vector3[] s_HeadPoints = new Vector3[3];

        // a line with a dark edge around it
        static void DrawOutlined(float width, int count, Vector3[] points)
        {
            var color = Handles.color;
            Handles.color = kOutlineColor;
            SceneHandles.DrawAAPolyLine(width + kOutlineWidth, count, points);
            Handles.color = color;
            SceneHandles.DrawAAPolyLine(width, count, points);
        }

        static void Draw(int id, float angle, Vector3 center, Vector3 axis, Vector3 zeroDirection, float ringRadius, float handleSize, Vector3 knob, float knobSize, bool hot, string name)
        {
            var hovered = GUIUtility.hotControl == 0 && UnityEditor.HandleUtility.nearestControl == id;
            var knobRadius = knobSize * (hot ? 1.25f : 1.0f);
            // the ring, dashed: every other piece of it
            for (int i = 0; i < kRingPoints; i += 2)
            {
                s_RingPoints[0] = PointAt(center, axis, zeroDirection, ringRadius,  i      * 360.0f / kRingPoints);
                s_RingPoints[1] = PointAt(center, axis, zeroDirection, ringRadius, (i + 1) * 360.0f / kRingPoints);
                DrawOutlined(kRingWidth, 2, s_RingPoints);
            }

            // while dragged: the ring from zero to the angle, and a tick at zero
            if (hot)
            {
                s_RingPoints[0] = PointAt(center, axis, zeroDirection, ringRadius - handleSize * kZeroTickSize * 0.5f, 0);
                s_RingPoints[1] = PointAt(center, axis, zeroDirection, ringRadius + handleSize * kZeroTickSize * 0.5f, 0);
                DrawOutlined(kRingWidth, 2, s_RingPoints);

                var sweep  = Mathf.Clamp(angle, -360, 360);
                var points = Mathf.Max(2, Mathf.CeilToInt(Mathf.Abs(sweep) / 360.0f * kRingPoints) + 1);
                for (int i = 0; i < points; i++)
                    s_RingPoints[i] = PointAt(center, axis, zeroDirection, ringRadius, sweep * i / (points - 1));
                DrawOutlined(kSweepWidth, points, s_RingPoints);
            }

            // the knob, facing the camera, with a dark edge
            var camera = Camera.current;
            var facing = camera ? Handles.inverseMatrix.MultiplyVector(camera.transform.forward) : axis;
            var color  = Handles.color;
            Handles.color = kOutlineColor;
            Handles.DrawSolidDisc(knob, facing, knobRadius * kKnobOutline);
            Handles.color = color;
            Handles.DrawSolidDisc(knob, facing, knobRadius);

            // hovered or dragged, the angle beside the knob: further out from the middle than the ring, clear of the knob
            if (hot || hovered)
            {
                var outward = LabelDirection(center, axis, knob);
                var labelAt = knob + outward * (knobRadius * kKnobOutline + handleSize * kLabelGap);
                Measurements.DrawLabel(labelAt, outward, 2, AngleText(angle, name));
            }

            var width = (hot || GUIUtility.keyboardControl == id) ? kHotArrowWidth : kArrowWidth;
            DrawArrow(center, axis, zeroDirection, ringRadius, angle, handleSize, +1, width);
            DrawArrow(center, axis, zeroDirection, ringRadius, angle, handleSize, -1, width);
        }
    }
}
