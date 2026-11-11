using UnityEditor;
using UnityEngine;

namespace Chisel.Editors
{
    public enum TurnState
    {
        None,
        ClockWise,
        AntiClockWise
    }

#if UNITY_6000_7_OR_NEWER
    [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
#endif
    public static class TurnHandle
    {
		readonly static GUIContent kClockWiseRotation     = new("\u21BB");
		readonly static GUIContent kAntiClockWiseRotation = new("\u21BA");

        public static TurnState DoHandle(Bounds bounds)
        {
            var cameraPosition  = Camera.current.transform.position;
            var matrix          = Handles.matrix;

            var min			= new Vector3(Mathf.Min(bounds.min.x, bounds.max.x), Mathf.Min(bounds.min.y, bounds.max.y), Mathf.Min(bounds.min.z, bounds.max.z));
            var max			= new Vector3(Mathf.Max(bounds.min.x, bounds.max.x), Mathf.Max(bounds.min.y, bounds.max.y), Mathf.Max(bounds.min.z, bounds.max.z));

            var center      = (max + min) * 0.5f;

            var worldCenter     = matrix.MultiplyPoint(center);
            var iconDirection   = (cameraPosition - worldCenter).normalized;

            var dotX        = Vector3.Dot(matrix.MultiplyVector(new Vector3(1, 0, 0)), iconDirection);
            var dotZ        = Vector3.Dot(matrix.MultiplyVector(new Vector3(0, 0, 1)), iconDirection);
            var dotY        = Vector3.Dot(matrix.MultiplyVector(new Vector3(0, 1, 0)), iconDirection);

            if ((dotX > -0.2f) && (dotX < 0.2f)) dotX = 0;
            if ((dotZ > -0.2f) && (dotZ < 0.2f)) dotZ = 0;

            var axisY  = (dotY > 0);
            var axis0X = (dotX == 0) ? (dotZ > 0) : (dotZ == 0) ? (dotX > 0) : ((dotX < 0) ^ (dotX < 0) ^ (dotZ > 0));
            var axis0Z = (dotX == 0) ? (dotZ > 0) : (dotZ == 0) ? (dotX < 0) : ((dotZ > 0) ^ (dotX < 0) ^ (dotZ > 0));
            var axis1X = (dotX == 0) ? (dotZ < 0) : (dotZ == 0) ? (dotX > 0) : ((dotX > 0) ^ (dotX < 0) ^ (dotZ > 0));
            var axis1Z = (dotX == 0) ? (dotZ > 0) : (dotZ == 0) ? (dotX > 0) : ((dotZ < 0) ^ (dotX < 0) ^ (dotZ > 0));

            var pLabel0     = new Vector3(axis0X ? min.x : max.x, axisY ? max.y : min.y, axis0Z ? min.z : max.z);
            var pLabel1     = new Vector3(axis1X ? min.x : max.x, axisY ? max.y : min.y, axis1Z ? min.z : max.z);

            // out past the corner, clear of the size measurements drawn along its edges
            var dir0 = (pLabel0 - center).normalized;
            var dir1 = (pLabel1 - center).normalized;
            pLabel0 += dir0 * (HandleUtility.GetHandleSize(pLabel0) * 0.5f);
            pLabel1 += dir1 * (HandleUtility.GetHandleSize(pLabel1) * 0.5f);

            var prevColor = Handles.color;
            Handles.color = SceneHandles.MeasureColor;

            var result = TurnState.None;

            if (SceneHandles.ClickableLabel(pLabel1, dir1, kClockWiseRotation, fontSize: 32, fontStyle: FontStyle.Bold))
            {
                result = TurnState.ClockWise;
            }

            if (SceneHandles.ClickableLabel(pLabel0, dir0, kAntiClockWiseRotation, fontSize: 32, fontStyle: FontStyle.Bold))
            {
                result = TurnState.AntiClockWise;
            }

            Handles.color = prevColor;

            return result;
        }
    }
}
