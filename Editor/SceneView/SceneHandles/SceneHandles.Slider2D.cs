using System.Linq;
using UnityEditor;
using UnityEngine;
using Chisel.Core;
using System.Buffers;

namespace Chisel.Editors
{
    public sealed partial class SceneHandles
    {
		internal readonly static int kSlider2DHash = "Slider2DHash".GetHashCode();
                
        public static Vector3 Slider2DHandle(Vector3 handlePos, Vector3 offset, Vector3 handleDir, Vector3 slideDir1, Vector3 slideDir2, float handleSize, CapFunction capFunction, Axes axes = Axes.None, bool selectLockingAxisOnClick = false, Vector3? snappingSteps = null)
        {
            var id = GUIUtility.GetControlID (kSlider2DHash, FocusType.Keyboard);
			var points = ArrayPool<Vector3>.Shared.Rent(1);
            try
            {
				points[0] = handlePos;
                return Slider2D.Do(id, points, 1, handlePos, offset, handleDir, slideDir1, slideDir2, handleSize, capFunction, axes, selectLockingAxisOnClick, false, snappingSteps)[0];
			}
			finally
			{
				ArrayPool<Vector3>.Shared.Return(points);
			}
		}

        public static Vector3[] Slider2DHandle(int id, Vector3[] points, Vector3 handlePos, Vector3 offset, Vector3 handleDir, Vector3 slideDir1, Vector3 slideDir2, float handleSize, CapFunction capFunction, Axes axes = Axes.None, bool selectLockingAxisOnClick = false, Vector3? snappingSteps = null, bool noSnapping = false)
        {
            return Slider2D.Do(id, points, handlePos, offset, handleDir, slideDir1, slideDir2, handleSize, capFunction, axes, selectLockingAxisOnClick, noSnapping, snappingSteps);
		}

		internal static Vector3[] Slider2DHandle(int id, Vector3[] points, int pointCount, Vector3 handlePos, Vector3 offset, Vector3 handleDir, Vector3 slideDir1, Vector3 slideDir2, float handleSize, CapFunction capFunction, Axes axes = Axes.None, bool selectLockingAxisOnClick = false, Vector3? snappingSteps = null, bool noSnapping = false)
		{
			return Slider2D.Do(id, points, pointCount, handlePos, offset, handleDir, slideDir1, slideDir2, handleSize, capFunction, axes, selectLockingAxisOnClick, noSnapping, snappingSteps);
		}

		public static Vector3 Slider2DHandle(int id, Vector3 handlePos, Vector3 offset, Vector3 handleDir, Vector3 slideDir1, Vector3 slideDir2, float handleSize, CapFunction capFunction, Axes axes = Axes.None, bool selectLockingAxisOnClick = false, Vector3? snappingSteps = null, bool noSnapping = false)
		{
			var points = ArrayPool<Vector3>.Shared.Rent(1);
            try
            {
				points[0] = handlePos;
                return Slider2D.Do(id, points, 1, handlePos, offset, handleDir, slideDir1, slideDir2, handleSize, capFunction, axes, selectLockingAxisOnClick, noSnapping, snappingSteps)[0];
			}
			finally
			{
				ArrayPool<Vector3>.Shared.Return(points);
			}
		}

        public static Vector3 Slider2DHandleOffset(int id, Vector3 handlePos, Vector3 handleDir, float handleSize = 0, CapFunction capFunction = null, bool selectLockingAxisOnClick = false, Vector3? snappingSteps = null)
        {
            var grid        = Grid.ActiveGrid;
            var normalAxis  = grid.GetClosestAxis(handleDir);
            var axes        = grid.GetTangentAxesForAxis(normalAxis, out Vector3 slideDir1, out Vector3 slideDir2);
            if (handleSize == 0)
                handleSize = UnityEditor.HandleUtility.GetHandleSize(handlePos) * 0.05f;
			var points = ArrayPool<Vector3>.Shared.Rent(1);
            try
            {
				points[0] = handlePos;
                return Slider2D.Do(id, points, 1, handlePos, Vector3.zero, handleDir, slideDir1, slideDir2, handleSize, capFunction, axes, selectLockingAxisOnClick, false, snappingSteps)[0] - handlePos;
			}
			finally
			{
				ArrayPool<Vector3>.Shared.Return(points);
			}
		}

        public class Slider2D
		{
			private static readonly Snapping2D s_Snapping2D = new();
			private static Vector2		s_CurrentMousePosition;
            private static Vector3[]	s_StartPoints;
            private static int          s_PrevFocusControl;
            private static bool         s_MovedMouse = false;

            public static Vector3 Do(int id, Vector3 point, Vector3 handleOrigin, Vector3 handleCursorOffset, Vector3 handleNormal, Vector3 slideDir1, Vector3 slideDir2, float handleSize, SceneHandles.CapFunction capFunction, Axes axes = Axes.None, bool selectLockingAxisOnClick = false, bool noSnapping = false, Vector3? snappingSteps = null)
            {
                var points = ArrayPool<Vector3>.Shared.Rent(1);
                try
                {
                    points[0] = point;
                    return Do(id, points, 1, handleOrigin, handleCursorOffset, handleNormal, slideDir1, slideDir2, handleSize, capFunction, axes, selectLockingAxisOnClick, noSnapping, snappingSteps)[0];
                }
                finally
                {
                    ArrayPool<Vector3>.Shared.Return(points);
                }
			}

            public static Vector3[] Do(int id, Vector3[] points, Vector3 handleOrigin, Vector3 handleCursorOffset, Vector3 handleNormal, Vector3 slideDir1, Vector3 slideDir2, float handleSize, SceneHandles.CapFunction capFunction, Axes axes = Axes.None, bool selectLockingAxisOnClick = false, bool noSnapping = false, Vector3? snappingSteps = null)
            {
                return Do(id, points, points.Length, handleOrigin, handleCursorOffset, handleNormal, slideDir1, slideDir2, handleSize, capFunction, axes, selectLockingAxisOnClick, noSnapping, snappingSteps);
			}

			internal static Vector3[] Do(int id, Vector3[] points, int pointCount, Vector3 handleOrigin, Vector3 handleCursorOffset, Vector3 handleNormal, Vector3 slideDir1, Vector3 slideDir2, float handleSize, SceneHandles.CapFunction capFunction, Axes axes = Axes.None, bool selectLockingAxisOnClick = false, bool noSnapping = false, Vector3? snappingSteps = null)
            {
                var evt = Event.current;
                switch (evt.GetTypeForControl(id))
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
                            
                        var localToWorldMatrix	= UnityEditor.Handles.matrix;
                        var	center				= Grid.ActiveGrid.Center;
                        Matrix4x4 gridSpace = Matrix4x4.identity;
                        gridSpace.SetColumn(0, localToWorldMatrix.MultiplyVector(slideDir1).normalized);
                        gridSpace.SetColumn(1, localToWorldMatrix.MultiplyVector(handleNormal).normalized);
                        gridSpace.SetColumn(2, localToWorldMatrix.MultiplyVector(slideDir2).normalized);
                        gridSpace.SetColumn(3, new Vector4(center.x, center.y, center.z, 1.0f));

                        var workGrid = new Grid(gridSpace, snappingSteps.HasValue ? snappingSteps.Value : Snapping.MoveSnappingSteps);
                        
                        s_Snapping2D.Initialize(workGrid, s_CurrentMousePosition, handleOrigin, localToWorldMatrix);
                        s_Snapping2D.CalculateExtents(s_StartPoints);
                        s_MovedMouse = false;
                        break;
                    }
                    case EventType.MouseDrag:
                    {
                        if (GUIUtility.hotControl != id)
                            break;

                        s_MovedMouse = true;

                        if (SceneHandles.Disabled || Snapping.AreAxisLocked(axes))
                            break;

                        s_CurrentMousePosition += evt.delta;
                        evt.Use();

                        if (!s_Snapping2D.DragTo(s_CurrentMousePosition, noSnapping ? SnappingMode.Never: SnappingMode.Default))
                            break;

                        var handleInverseMatrix = UnityEditor.Handles.inverseMatrix;
                        var localPointDelta     = s_Snapping2D.WorldSnappedDelta;

                        var pointDelta			= handleInverseMatrix.MultiplyVector(localPointDelta);

                        if (axes != Axes.None)
                        {
                            if ((axes & Axes.X) == Axes.None) pointDelta.x = 0;
                            if ((axes & Axes.Y) == Axes.None) pointDelta.y = 0;
                            if ((axes & Axes.Z) == Axes.None) pointDelta.z = 0;
                        }

                        if (s_StartPoints != null)
                        {
                            points = new Vector3[pointCount]; // if we don't, it's hard to do Undo properly
                            for (int i = 0; i < pointCount; i++)
                                points[i] = 
                                    SnappingUtility.Quantize(s_StartPoints[i] + pointDelta);
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
                            s_StartPoints = null;
                            s_Snapping2D.Reset();
                            evt.Use();
                            EditorGUIUtility.SetWantsMouseJumping(0);
                            SceneView.RepaintAll();
                            if (!s_MovedMouse && selectLockingAxisOnClick)
                                Snapping.ActiveAxes = axes;
                        }
                        break;
                    }
                    case EventType.Layout:
                    {
                        if (SceneHandles.InCameraOrbitMode)
                            break;

                        var position = handleOrigin + handleCursorOffset;
                        var rotation = Quaternion.LookRotation(handleNormal, slideDir1);

                        if (capFunction != null)
                            capFunction(id, position, rotation, handleSize, EventType.Layout);
                        else
                            UnityEditor.HandleUtility.AddControl(id, UnityEditor.HandleUtility.DistanceToCircle(position, handleSize * .5f));

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
                        if (axes != Axes.None)
                        {
                            if (GUIUtility.hotControl == id &&
                                s_Snapping2D.WorldSlideGrid != null)
                            {
                                var selectedColor = UnityEditor.Handles.selectedColor;
                                selectedColor.a = 0.5f;
                                using (new SceneHandles.DrawingScope(selectedColor))
                                    HandleRendering.RenderSnapping3D(s_Snapping2D.WorldSlideGrid, s_Snapping2D.WorldSnappedExtents, s_Snapping2D.GridSnappedPosition, s_Snapping2D.SnapResult);
                            }
                        }

                        if (capFunction == null)
                            break;

                        var position = handleOrigin + handleCursorOffset;
                        var rotation = Quaternion.LookRotation(handleNormal, slideDir1);
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
