using UnityEditor;
using UnityEngine;
using Chisel.Core;

namespace Chisel.Editors
{
    public sealed partial class SceneHandles
    {
        internal readonly static int kRectHash0 = "RectHash0".GetHashCode();
		internal readonly static int kRectHash1 = "RectHash1".GetHashCode();
		internal readonly static int kRectHash2 = "RectHash2".GetHashCode();
		internal readonly static int kRectHash3 = "RectHash3".GetHashCode();
		internal readonly static int kRectHash4 = "RectHash4".GetHashCode();
		internal readonly static int kRectHash5 = "RectHash5".GetHashCode();
		internal readonly static int kRectHash6 = "RectHash6".GetHashCode();
		internal readonly static int kRectHash7 = "RectHash7".GetHashCode();
        
        public static Rect RectHandle(Rect rect, Quaternion rotation, CapFunction capFunction)
        {
            var originalMatrix = SceneHandles.Matrix;
            SceneHandles.Matrix = originalMatrix * Matrix4x4.TRS(Vector3.zero, rotation, Vector3.one);
            var result = RectHandle(rect, capFunction);
            SceneHandles.Matrix = originalMatrix;
            return result;
        }
        
        public static Rect RectHandle(Rect rect, CapFunction capFunction)
        {
            var handlesMatrix = SceneHandles.Matrix;

            var direction = Vector3.forward;
            var slideDirX = Vector3.right;
            var slideDirY = Vector3.up;

            var point1Id = GUIUtility.GetControlID (kRectHash0, FocusType.Keyboard);
            var point2Id = GUIUtility.GetControlID (kRectHash1, FocusType.Keyboard);
            var point3Id = GUIUtility.GetControlID (kRectHash2, FocusType.Keyboard);
            var point4Id = GUIUtility.GetControlID (kRectHash3, FocusType.Keyboard);
            
            var edge1Id  = GUIUtility.GetControlID (kRectHash4, FocusType.Keyboard);
            var edge2Id  = GUIUtility.GetControlID (kRectHash5, FocusType.Keyboard);
            var edge3Id  = GUIUtility.GetControlID (kRectHash6, FocusType.Keyboard);
            var edge4Id	 = GUIUtility.GetControlID (kRectHash7, FocusType.Keyboard);
            
            int currentFocusControl = SceneHandleUtility.FocusControl;

            bool highlightEdge1 = (currentFocusControl == edge1Id) || (currentFocusControl == point1Id) || (currentFocusControl == point2Id);
            bool highlightEdge2 = (currentFocusControl == edge2Id) || (currentFocusControl == point3Id) || (currentFocusControl == point4Id);
            bool highlightEdge3 = (currentFocusControl == edge3Id) || (currentFocusControl == point2Id) || (currentFocusControl == point3Id);
            bool highlightEdge4 = (currentFocusControl == edge4Id) || (currentFocusControl == point4Id) || (currentFocusControl == point1Id);
            
            var selectedAxes = ((highlightEdge3 || highlightEdge4) ? Axes.X : Axes.None) |
                               ((highlightEdge1 || highlightEdge2) ? Axes.Y : Axes.None);

            var xMin = rect.xMin;
            var xMax = rect.xMax;
            var yMin = rect.yMin;
            var yMax = rect.yMax;

            var point1 = new Vector3(xMin, yMin, 0);
            var point2 = new Vector3(xMax, yMin, 0);
            var point3 = new Vector3(xMax, yMax, 0);
            var point4 = new Vector3(xMin, yMax, 0);
            
            var isStatic		= (!Tools.hidden && EditorApplication.isPlaying && GameObjectExtensions.ContainsStatic(Selection.gameObjects));
            var prevDisabled	= SceneHandles.Disabled;
            var prevColor		= SceneHandles.Color;

            var xAxisDisabled	= isStatic || prevDisabled || Snapping.AxisLocking[0];
            var yAxisDisabled	= isStatic || prevDisabled || Snapping.AxisLocking[1];
            var xyAxiDisabled	= xAxisDisabled && yAxisDisabled;

            Vector3 position, offset;
            var prevGUIchanged = GUI.changed;
            

            SceneHandles.Disabled = yAxisDisabled;
            { 
                GUI.changed = false;
                position = (point1 + point2) * 0.5f;
                SceneHandles.Color = SceneHandles.StateColor(SceneHandles.YAxisColor, xAxisDisabled, highlightEdge1);
                offset = Edge1DHandleOffset(edge1Id, Axis.Y, point1, point2, position, slideDirY, Snapping.MoveSnappingSteps.y, UnityEditor.HandleUtility.GetHandleSize(position) * 0.05f, null);
                if (GUI.changed) { yMin += offset.y; prevGUIchanged = true; }

                GUI.changed = false;
                position = (point3 + point4) * 0.5f;
                SceneHandles.Color = SceneHandles.StateColor(SceneHandles.YAxisColor, xAxisDisabled, highlightEdge2);
                offset = Edge1DHandleOffset(edge2Id, Axis.Y, point3, point4, position, slideDirY, Snapping.MoveSnappingSteps.y, UnityEditor.HandleUtility.GetHandleSize(position) * 0.05f, null);
                if (GUI.changed) { yMax += offset.y; prevGUIchanged = true; }
            }
            

            SceneHandles.Disabled = xAxisDisabled;
            { 
                GUI.changed = false;
                position = (point2 + point3) * 0.5f;
                SceneHandles.Color = SceneHandles.StateColor(SceneHandles.YAxisColor, xAxisDisabled, highlightEdge3);
                offset = Edge1DHandleOffset(edge3Id, Axis.X, point2, point3, position, slideDirX, Snapping.MoveSnappingSteps.x, UnityEditor.HandleUtility.GetHandleSize(position) * 0.05f, null);
                if (GUI.changed) { xMax += offset.x; prevGUIchanged = true; }

                GUI.changed = false;
                position = (point4 + point1) * 0.5f;
                SceneHandles.Color = SceneHandles.StateColor(SceneHandles.YAxisColor, xAxisDisabled, highlightEdge4);
                offset = Edge1DHandleOffset(edge4Id, Axis.X, point4, point1, position, slideDirX, Snapping.MoveSnappingSteps.x, UnityEditor.HandleUtility.GetHandleSize(position) * 0.05f, null);
                if (GUI.changed) { xMin += offset.x; prevGUIchanged = true; }
            }
            
            
            SceneHandles.Disabled = xyAxiDisabled;
            SceneHandles.Color = SceneHandles.StateColor(SceneHandles.YAxisColor, xyAxiDisabled, false);
            { 

                GUI.changed = false;
                point1 = Slider2DHandle(point1Id, point1, Vector3.zero, direction, slideDirX, slideDirY, UnityEditor.HandleUtility.GetHandleSize(point1) * 0.05f, capFunction, Axes.XZ); 
                if (GUI.changed) { xMin = point1.x; yMin = point1.y; prevGUIchanged = true; }
            
                GUI.changed = false;
                point2 = Slider2DHandle(point2Id, point2, Vector3.zero, direction, slideDirX, slideDirY, UnityEditor.HandleUtility.GetHandleSize(point2) * 0.05f, capFunction, Axes.XZ);
                if (GUI.changed) { xMax = point2.x; yMin = point2.y; prevGUIchanged = true; }
            
                GUI.changed = false;
                point3 = Slider2DHandle(point3Id, point3, Vector3.zero, direction, slideDirX, slideDirY, UnityEditor.HandleUtility.GetHandleSize(point3) * 0.05f, capFunction, Axes.XZ);
                if (GUI.changed) { xMax = point3.x; yMax = point3.y; prevGUIchanged = true; }
            
                GUI.changed = false;
                point4 = Slider2DHandle(point4Id, point4, Vector3.zero, direction, slideDirX, slideDirY, UnityEditor.HandleUtility.GetHandleSize(point4) * 0.05f, capFunction, Axes.XZ);
                if (GUI.changed) { xMin = point4.x; yMax = point4.y; prevGUIchanged = true; }
            }
            GUI.changed = prevGUIchanged;
            
            rect.x = xMin; rect.width  = xMax - xMin;
            rect.y = yMin; rect.height = yMax - yMin;

            SceneHandles.Disabled = prevDisabled;
            SceneHandles.Color = prevColor;

            return rect;
        }
    }
}
