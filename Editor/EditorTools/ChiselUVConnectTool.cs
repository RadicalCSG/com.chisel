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
    // material. Dragging between two that are connected disconnects them.
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
            var repaint       = ChiselUVToolCommon.SurfaceSelection(dragArea, selectionType);
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
