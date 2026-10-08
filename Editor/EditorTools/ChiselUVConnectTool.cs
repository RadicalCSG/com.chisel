using System.Collections.Generic;
using Chisel.Core;
using Chisel.Components;
using UnityEngine;
using UnityEditor;
using UnityEditor.ShortcutManagement;
using UnityEditor.EditorTools;
#if !UNITY_2020_2_OR_NEWER
using ToolManager = UnityEditor.EditorTools;
#endif

namespace Chisel.Editors
{
    // Dragging from a hotspot mapped surface onto another connects the two (,
    // surface connections): the other takes the first one's material, and loses its own links when it had another hotspot
    // material. Dragging between two that are connected disconnects them. Clicking an edge of a hotspot mapped surface (it
    // lights up under the mouse) hides the border along it, or brings it back.
    sealed class ChiselUVConnectTool : ChiselEditToolBase
    {
        const string kToolName = "UV Connect";
        public override string ToolName => kToolName;

        public static bool IsActive() { return ToolManager.activeToolType == typeof(ChiselUVConnectTool); }

        #region Keyboard Shortcut
        const string kEditModeShotcutName = kToolName + " Mode";
        [Shortcut(ChiselKeyboardDefaults.ShortCutEditModeBase + kEditModeShotcutName, displayName = kEditModeShotcutName)]
        public static void ActivateTool() { ToolManager.SetActiveTool<ChiselUVConnectTool>(); }
        #endregion
        public override SnapSettings ToolUsedSnappingModes { get { return SnapSettings.None; } }

        public override void OnActivate()
        {
            base.OnActivate();
            ChiselUVToolCommon.Instance.OnActivate();
            ChiselOutlineRenderer.VisualizationMode = VisualizationMode.Surface;
        }

        public override void OnDeactivate()
        {
            base.OnDeactivate();
            ChiselUVToolCommon.Instance.OnDeactivate();
        }

        readonly static int kSurfaceEditModeHash = "SurfaceConnectEditMode".GetHashCode();
        readonly static int kSurfaceConnectHash  = "SurfaceConnect".GetHashCode();

        public override void OnSceneGUI(SceneView sceneView, Rect dragArea)
        {
            var defaultID = GUIUtility.GetControlID(kSurfaceEditModeHash, FocusType.Keyboard, dragArea);
            HandleUtility.AddDefaultControl(defaultID);

            var selectionType = ChiselRectSelectionManager.GetCurrentSelectionType();
            var repaint       = false;
            if (Event.current.type == EventType.MouseMove)
                repaint = UpdateHoverEdge(Event.current.mousePosition);
            repaint = ChiselUVToolCommon.SurfaceSelection(dragArea, selectionType) || repaint;
            repaint = SurfaceConnectTool(selectionType, dragArea) || repaint;

            MouseCursor cursor;
            switch (selectionType)
            {
                default: cursor = MouseCursor.Link; break;
                case SelectionType.Additive:    cursor = MouseCursor.ArrowPlus; break;
                case SelectionType.Subtractive: cursor = MouseCursor.ArrowMinus; break;
            }
            EditorGUIUtility.AddCursorRect(dragArea, cursor);

            if (repaint &&
                Event.current.type != EventType.Layout &&
                Event.current.type != EventType.Repaint)
                SceneView.RepaintAll();
        }

        static bool             s_Connecting;
        static SurfaceReference s_Target;
        static bool             s_Disconnects;
        static Vector3          s_End;
        static readonly List<SurfaceReference> s_FoundSurfaces = new();

        static bool SurfaceConnectTool(SelectionType selectionType, Rect dragArea)
        {
            var id = GUIUtility.GetControlID(kSurfaceConnectHash, FocusType.Keyboard, dragArea);
            switch (Event.current.GetTypeForControl(id))
            {
                case EventType.MouseDown: s_Connecting = false; s_Target = null; break;
                // before the tool lets go of what it dragged
                case EventType.MouseUp:
                {
                    if (s_Connecting && ChiselUVToolCommon.ToolIsDragging && ChiselUVToolCommon.IsToolEnabled(id))
                        ConnectDragged();
                    else if (!ChiselUVToolCommon.ToolIsDragging && ChiselUVToolCommon.IsToolEnabled(id) && s_Edge >= 0)
                        ToggleEdgeBorder();
                    s_Connecting = false;
                    break;
                }
            }
            if (!ChiselUVToolCommon.SurfaceToolBase(id, selectionType, dragArea))
                return false;

            var needRepaint = false;
            switch (Event.current.GetTypeForControl(id))
            {
                case EventType.Repaint:
                {
                    if (ChiselUVToolCommon.ToolIsDragging && s_Connecting)
                        RenderConnection();
                    else if (!ChiselUVToolCommon.ToolIsDragging && s_Edge >= 0)
                        RenderHoverEdge();
                    break;
                }
                case EventType.MouseDrag:
                {
                    if (!ChiselUVToolCommon.IsToolEnabled(id))
                        break;
                    var mousePosition = Event.current.mousePosition;
                    if (ChiselUVToolCommon.StartToolDragging())
                        s_Connecting = ChiselUVToolCommon.IsHotspotMapped(ChiselUVToolCommon.StartSurfaceReference);
                    if (s_Connecting)
                    {
                        UpdateTarget(mousePosition);
                        needRepaint = true;
                    }
                    break;
                }
            }
            return needRepaint;
        }

        // The edge of a hotspot mapped surface under the mouse, if it is this close to one (pixels)
        const float kEdgePickDistance = 6;
        static SurfaceReference s_EdgeSurface;
        static int              s_Edge = -1;
        static Vector3          s_EdgeFrom, s_EdgeTo;

        static bool UpdateHoverEdge(Vector2 mousePosition)
        {
            var before = s_Edge; var beforeSurface = s_EdgeSurface;
            s_Edge = -1; s_EdgeSurface = null;
            s_FoundSurfaces.Clear();
            if (ChiselClickSelectionManager.FindSurfaceReferences(mousePosition, false, s_FoundSurfaces, out _, out var surface) &&
                !ReferenceEquals(surface, null) && ChiselUVToolCommon.IsHotspotMapped(surface))
            {
                var modelMatrix = ChiselNodeHierarchyManager.FindModelTransformMatrixOfTransform(surface.node.transform);
                var best = kEdgePickDistance;
                for (int edge = 0; ChiselHotspotLinks.EdgeOf(surface.brush, surface.surfaceIndex, edge, out var from, out var to); edge++)
                {
                    var a = modelMatrix.MultiplyPoint3x4(from); var b = modelMatrix.MultiplyPoint3x4(to);
                    var distance = HandleUtility.DistanceToLine(a, b);
                    if (distance >= best)
                        continue;
                    best = distance;
                    s_Edge = edge; s_EdgeSurface = surface; s_EdgeFrom = a; s_EdgeTo = b;
                }
            }
            return s_Edge != before || !Equals(s_EdgeSurface, beforeSurface);
        }

        // yellow: clicking hides the border along it; cyan: it has none, clicking brings it back
        static void RenderHoverEdge()
        {
            var hidden = ChiselHotspotLinks.EdgeBorderOf(s_EdgeSurface, s_Edge) == ChiselHotspotBorderState.Hidden;
            ChiselOutlineRenderer.DrawLine(s_EdgeFrom, s_EdgeTo, hidden ? Color.cyan : Color.yellow, LineMode.NoZTest, 4, 0);
        }

        static void ToggleEdgeBorder()
        {
            var model = s_EdgeSurface.node ? s_EdgeSurface.node.hierarchyItem.Model : null;
            if (!model)
                return;
            Undo.RecordObject(model, "Hotspot Edge Border");
            var hidden = ChiselHotspotLinks.EdgeBorderOf(s_EdgeSurface, s_Edge) == ChiselHotspotBorderState.Hidden;
            ChiselHotspotLinks.SetEdgeBorder(s_EdgeSurface, s_Edge, hidden ? ChiselHotspotBorderState.Automatic : ChiselHotspotBorderState.Hidden);
            EditorUtility.SetDirty(model);
        }

        static void UpdateTarget(Vector2 mousePosition)
        {
            var start = ChiselUVToolCommon.StartSurfaceReference;
            s_Target = null;
            s_End    = ChiselUVToolCommon.s_WorldIntersection;
            s_FoundSurfaces.Clear();
            if (!ChiselClickSelectionManager.FindSurfaceReferences(mousePosition, false, s_FoundSurfaces, out var intersection, out var surface) ||
                ReferenceEquals(surface, null) || ReferenceEquals(start, null) || start.Equals(surface))
                return;
            s_End = intersection.worldPlaneIntersection;
            if (!ChiselHotspotLinks.ModelOfSurfaces(start, surface))
                return;
            s_Target      = surface;
            s_Disconnects = ChiselHotspotLinks.StateOf(start, surface) == ChiselHotspotLinkState.Connected;
        }

        // green: they will be connected; red: they are, and will be disconnected; white: nothing to connect to
        static void RenderConnection()
        {
            var color = ReferenceEquals(s_Target, null) ? Color.white : (s_Disconnects ? Color.red : Color.green);
            ChiselOutlineRenderer.DrawLine(ChiselUVToolCommon.s_WorldStartPosition, s_End, color, LineMode.NoZTest, 4, 0);
            HandleRendering.RenderVertexBox(s_End);
        }

        static void ConnectDragged()
        {
            var source = ChiselUVToolCommon.StartSurfaceReference;
            var target = s_Target;
            s_Target = null;
            if (ReferenceEquals(source, null) || ReferenceEquals(target, null) || source.Equals(target))
                return;
            var model = ChiselHotspotLinks.ModelOfSurfaces(source, target);
            if (!model)
                return;
            if (s_Disconnects)
            {
                Undo.RecordObject(model, "Hotspot Disconnect");
                ChiselHotspotLinks.Disconnect(source, target);
                EditorUtility.SetDirty(model);
                return;
            }
            Undo.RecordObjects(new Object[] { model, target.node }, "Hotspot Connect");
            var material = source.RenderMaterial;
            if (target.RenderMaterial != material)
            {
                if (ChiselUVToolCommon.IsHotspotMapped(target))
                    ChiselHotspotLinks.ForgetAllOf(target);
                target.RenderMaterial = material;
            }
            ChiselHotspotLinks.Connect(source, target);
            EditorUtility.SetDirty(model);
            EditorUtility.SetDirty(target.node);
        }
    }
}
