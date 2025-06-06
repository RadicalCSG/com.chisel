using System.Linq;
using UnityEditor;
using UnityEngine;
using Chisel.Core;
using System;
using System.Buffers;

namespace Chisel.Editors
{
    public sealed partial class SceneHandles
    {
		internal readonly static int kSlider1DHash = "Slider1DHash".GetHashCode();


        public static Vector3 DirectionHandle(Vector3 handleOrigin, Vector3 handleDirection, float handleSize = 0, float snappingStep = 0, Axis axis = Axis.Y)
        {
            var id = GUIUtility.GetControlID(kSlider1DHash, FocusType.Passive);
            return DirectionHandle(id, handleOrigin, handleDirection, handleSize, snappingStep, axis);
        }

        public static Vector3 DirectionHandle(int id, Vector3 handleOrigin, Vector3 handleDirection, float handleSize = 0, float snappingStep = 0, Axis axis = Axis.Y)
        {
            if (snappingStep == 0)
                snappingStep = Snapping.MoveSnappingSteps[(int)axis];
            if (handleSize == 0)
                handleSize = UnityEditor.HandleUtility.GetHandleSize(handleOrigin);

            var currentFocusControl = Chisel.Editors.SceneHandleUtility.FocusControl;
            var result = Chisel.Editors.SceneHandles.Slider1DHandle(id, axis, handleOrigin, handleDirection, snappingStep, handleSize * 0.05f, Chisel.Editors.SceneHandles.NormalHandleCap);
            if (currentFocusControl == id)
            {
                var sceneView = SceneView.currentDrawingSceneView;
                if (sceneView)
                {
                    var rect = sceneView.position;
                    rect.min = Vector2.zero;
                    EditorGUIUtility.AddCursorRect(rect, Chisel.Editors.SceneHandleUtility.GetCursorForDirection(Vector3.zero, handleDirection));
                }
				Chisel.Editors.SceneHandles.ArrowHandleCap(id, handleOrigin, Quaternion.LookRotation(handleDirection), handleSize, Event.current.type);
            }
            return result;
        }
        

        public static Vector3 Slider1DHandle(Vector3 handleOrigin, Vector3 handleDirection, float snappingStep = 0, float handleSize = 0, Axis axis = Axis.Y)
        {
            var id = GUIUtility.GetControlID(kSlider1DHash, FocusType.Passive);
            return Slider1DHandle(id, axis, handleOrigin, handleDirection, snappingStep, handleSize);
        }

        public static Vector3 Slider1DHandle(int id, Axis axis, Vector3 handleOrigin, Vector3 handleDirection, float snappingStep = 0, float handleSize = 0)
        {
            if (snappingStep == 0)
                snappingStep = Snapping.MoveSnappingSteps[(int)axis];
            if (handleSize == 0)
                handleSize = UnityEditor.HandleUtility.GetHandleSize(handleOrigin) * 0.05f;
            return Chisel.Editors.SceneHandles.Slider1DHandle(id, axis, handleOrigin, handleDirection, snappingStep, handleSize, Chisel.Editors.SceneHandles.OutlinedDotHandleCap);
        }

        public static Vector3 Slider1DHandle(Vector3 handleOrigin, Vector3 handleDirection, CapFunction capFunction, float snappingStep = 0, float handleSize = 0, Axis axis = Axis.Y)
        {
            var id = GUIUtility.GetControlID(kSlider1DHash, FocusType.Passive);
            return Slider1DHandle(id, axis, handleOrigin, handleDirection, capFunction, snappingStep, handleSize);
        }

        public static Vector3 Slider1DHandle(int id, Axis axis, Vector3 handleOrigin, Vector3 handleDirection, CapFunction capFunction, float snappingStep = 0, float handleSize = 0)
        {
            return Chisel.Editors.SceneHandles.Slider1DHandle(id, axis, handleOrigin, handleDirection, snappingStep, handleSize, capFunction);
        }

        public static Vector3 Slider1DHandle(int id, Vector3 handleOrigin, Vector3 handleDirection, CapFunction capFunction, float snappingStep = 0, float handleSize = 0)
        {
            Axis axis = Grid.ActiveGrid.GetClosestAxis(Handles.matrix.MultiplyVector(handleDirection));
            return Chisel.Editors.SceneHandles.Slider1DHandle(id, axis, handleOrigin, handleDirection, snappingStep, handleSize, capFunction);
        }

        public static Vector3[] Slider1DHandle(int id, Axis axis, Vector3[] snapPoints, Vector3 handleOrigin, Vector3 handleDirection, float snappingStep, float handleSize, CapFunction capFunction, bool selectLockingAxisOnClick = false) 
        {
            return Slider1D.Do(id, axis, snapPoints, handleOrigin, handleDirection, snappingStep, handleSize, capFunction, selectLockingAxisOnClick);
        }

        public static Vector3 Slider1DHandleOffset(int id, Axis axis, Vector3[] snapPoints, Vector3 handleDirection, float snappingStep = 0, float handleSize = 0, CapFunction capFunction = null, bool selectLockingAxisOnClick = false)
        {
            return Slider1D.Do(id, axis, snapPoints, snapPoints[0], handleDirection, snappingStep, handleSize, capFunction, selectLockingAxisOnClick)[0] - snapPoints[0];
        }

        public static Vector3 Slider1DHandleOffset(int id, Vector3[] snapPoints, Vector3 handleDirection, float snappingStep = 0, float handleSize = 0, CapFunction capFunction = null, bool selectLockingAxisOnClick = false)
        {
            Axis axis = Grid.ActiveGrid.GetClosestAxis(Handles.matrix.MultiplyVector(handleDirection));
            return Slider1D.Do(id, axis, snapPoints, snapPoints[0], handleDirection, snappingStep, handleSize, capFunction, selectLockingAxisOnClick)[0] - snapPoints[0];
        }

        public static Vector3 Slider1DHandleOffset(int id, Vector3 handleOrigin, Vector3 handleDirection, float snappingStep = 0, float handleSize = 0, CapFunction capFunction = null, bool selectLockingAxisOnClick = false)
        {
            Axis axis = Grid.ActiveGrid.GetClosestAxis(Handles.matrix.MultiplyVector(handleDirection));
            return Chisel.Editors.SceneHandles.Slider1DHandle(id, axis, handleOrigin, handleDirection, snappingStep, handleSize, capFunction) - handleOrigin;
        }

        public static Vector3 Slider1DHandleAlignedOffset(int id, Vector3 from, Vector3 to, float snappingStep = 0, float handleSize = 0, CapFunction capFunction = null, bool selectLockingAxisOnClick = false)
        {
            var snapPoints      = new[] { from, to };
            var handleDirection = (from - to).normalized;
            var grid            = Grid.ActiveGrid;
            var axis            = grid.GetClosestAxis(handleDirection);
            return Slider1D.Do(id, axis, snapPoints, from, handleDirection, snappingStep, handleSize, capFunction, selectLockingAxisOnClick)[0] - from;
        }

        public static Vector3 Slider1DHandle(int id, Axis axis, Vector3 handleOrigin, Vector3 handleDirection, float snappingStep, float handleSize, CapFunction capFunction, bool selectLockingAxisOnClick = false) 
        {
            var pointArray = ArrayPool<Vector3>.Shared.Rent(1);
            try
            {
                pointArray[0] = handleOrigin;
                return Slider1D.Do(id, axis, pointArray, handleOrigin, handleDirection, snappingStep, handleSize, capFunction, selectLockingAxisOnClick)[0];
            }
            finally
			{
				ArrayPool<Vector3>.Shared.Return(pointArray);
			}
        }



        public class Slider1D
        {
            private static readonly Snapping1D	s_Snapping1D = new();
            private static Vector2   s_CurrentMousePosition;
            private static Vector3[] s_StartPoints;
            private static int       s_PrevFocusControl;
            //private static Grid    s_PrevGrid;
            private static bool      s_MovedMouse = false;

            internal static Vector3[] Do(int id, Axis axis, Vector3[] points, Vector3 handleOrigin, Vector3 handleDirection, float snappingStep = 0, float handleSize = 0, SceneHandles.CapFunction capFunction = null, bool selectLockingAxisOnClick = false)
            {
                return Do(id, axis, points, handleOrigin, handleDirection, handleDirection, snappingStep, handleSize, capFunction, selectLockingAxisOnClick);
            }

            internal static Vector3[] Do(int id, Axis axis, Vector3[] points, Vector3 handleOrigin, Vector3 handleDirection, Vector3 slideDirection, float snappingStep = 0, float handleSize = 0, SceneHandles.CapFunction capFunction = null, bool selectLockingAxisOnClick = false)
			{
                return Do(id, axis, points, points.Length, handleOrigin, handleDirection, slideDirection, snappingStep, handleSize, capFunction, selectLockingAxisOnClick);

			}

			internal static Vector3[] Do(int id, Axis axis, Vector3[] points, int pointCount, Vector3 handleOrigin, Vector3 handleDirection, Vector3 slideDirection, float snappingStep = 0, float handleSize = 0, SceneHandles.CapFunction capFunction = null, bool selectLockingAxisOnClick = false)
            {
                if (snappingStep == 0)
                    snappingStep = Snapping.MoveSnappingSteps[(int)axis];
                if (handleSize == 0)
                    handleSize = UnityEditor.HandleUtility.GetHandleSize(handleOrigin) * 0.05f;

                if (handleDirection.sqrMagnitude == 0)
                    return points;

                var evt = Event.current;
                var type = evt.GetTypeForControl(id);
                switch (type)
                {
                    case EventType.MouseDown:
                    {
                        if (SceneHandles.InCameraOrbitMode)
                            break;

                        if (GUIUtility.hotControl != 0)
                            break;

                        if ((UnityEditor.HandleUtility.nearestControl != id || evt.button != 0) &&
                            (GUIUtility.keyboardControl != id || evt.button != 2))
                            break;

                        GUIUtility.hotControl = GUIUtility.keyboardControl = id;
                        evt.Use();
                        EditorGUIUtility.SetWantsMouseJumping(1);

                        s_CurrentMousePosition = evt.mousePosition;
                        s_StartPoints = new Vector3[pointCount];
                        for (int i = 0; i < pointCount; i++)
                            s_StartPoints[i] = points[i];
                        var handleMatrix = SceneHandles.Matrix;

                        s_Snapping1D.Initialize(s_CurrentMousePosition, 
                                                handleMatrix.MultiplyPoint(handleOrigin),
                                                handleMatrix.MultiplyVector(slideDirection), 
                                                snappingStep, axis);
                        s_Snapping1D.CalculateExtents(SceneHandles.InverseMatrix, s_StartPoints);
                        s_MovedMouse = false;
                        break;
                    }
                    case EventType.MouseDrag:
                    {
                        if (GUIUtility.hotControl != id)
                            break;

                        s_MovedMouse = true;

                        if (SceneHandles.Disabled || Snapping.IsAxisLocked(axis))
                            break;

                        s_CurrentMousePosition += evt.delta;
                        evt.Use();

                        if (!s_Snapping1D.Move(s_CurrentMousePosition))
                            break;

                        var handleInverseMatrix = SceneHandles.InverseMatrix;
                        var pointDelta = handleInverseMatrix.MultiplyVector(s_Snapping1D.WorldSnappedOffset);

                        if (s_StartPoints != null)
                        {
                            points = new Vector3[pointCount]; // if we don't, it's hard to do Undo properly
                            for (int i = 0; i < pointCount; i++)
                                points[i] = SnappingUtility.Quantize(s_StartPoints[i] + pointDelta);
                        }
                        //SceneView.RepaintAll();
                        GUI.changed = true;
                        break;
                    }
                    case EventType.MouseUp:
                    {
                        if (GUIUtility.hotControl == id && (evt.button == 0 || evt.button == 2))
                        {
                            GUIUtility.hotControl = 0;
                            GUIUtility.keyboardControl = 0;
                            //Grid.currentGrid = s_PrevGrid;
                            s_StartPoints = null;
                            evt.Use();
                            EditorGUIUtility.SetWantsMouseJumping(0);
                            if (!s_MovedMouse && selectLockingAxisOnClick)
                            {
                                switch (axis)
                                {
                                    case Axis.X: { Snapping.ActiveAxes = Axes.X; break; }
                                    case Axis.Y: { Snapping.ActiveAxes = Axes.Y; break; }
                                    case Axis.Z: { Snapping.ActiveAxes = Axes.Z; break; }
                                }
                            }
                            SceneView.RepaintAll();
                        }
                        break;
                    }
                    case EventType.MouseMove:
                    {
                        if (SceneHandles.InCameraOrbitMode)
                            break;

                        var position = handleOrigin;
                        var rotation = Quaternion.LookRotation(handleDirection);

                        if (handleSize > 0)
                        {
                            if (capFunction != null)
                                capFunction(id, position, rotation, handleSize, type);
                        }

                        int currentFocusControl = SceneHandleUtility.FocusControl;
                        if ((currentFocusControl == id && s_PrevFocusControl != id) ||
                            (currentFocusControl != id && s_PrevFocusControl == id))
                        {
                            s_PrevFocusControl = currentFocusControl;
                            SceneView.RepaintAll();
                        }
                        break;
                    }
                    case EventType.Layout:
                    {
                        if (SceneHandles.InCameraOrbitMode)
                            break;

                        var position = handleOrigin;
                        var rotation = Quaternion.LookRotation(handleDirection);

                        if (handleSize > 0)
                        {
                            if (capFunction != null)
                                capFunction(id, position, rotation, handleSize, type);
                            else
                                UnityEditor.HandleUtility.AddControl(id, UnityEditor.HandleUtility.DistanceToCircle(position, handleSize * .2f));
                        }

                        int currentFocusControl = SceneHandleUtility.FocusControl;
                        if ((currentFocusControl == id && s_PrevFocusControl != id) ||
                            (currentFocusControl != id && s_PrevFocusControl == id))
                        {
                            s_PrevFocusControl = currentFocusControl;
                            SceneView.RepaintAll();
                        }
                        break;
                    }
                    case EventType.Repaint:
                    {
                        if (axis != Axis.None)
                        {
                            if (GUIUtility.hotControl == id)
                            {
                                var selectedColor = SceneHandles.StateColor(SceneHandles.MultiplyTransparency(SceneHandles.SelectedColor, 0.5f));
                                using (new SceneHandles.DrawingScope(selectedColor))
                                    HandleRendering.RenderSnapping1D(s_Snapping1D.Min, s_Snapping1D.Max, s_Snapping1D.WorldSnappedPosition, s_Snapping1D.SlideDirection, s_Snapping1D.SnapResult, axis);
                            }
                        }

                        if (capFunction == null)
                            break;

                        var position = handleOrigin;
                        var rotation = Quaternion.LookRotation(handleDirection);
                        var color	 = SceneHandles.StateColor(SceneHandles.Color, isSelected: (id == s_PrevFocusControl));

                        using (new SceneHandles.DrawingScope(color))
                        {
                            capFunction(id, position, rotation, handleSize, EventType.Repaint);
                        }
                        break;
                    }
                }
                return points;
            }
        }
    }
}
