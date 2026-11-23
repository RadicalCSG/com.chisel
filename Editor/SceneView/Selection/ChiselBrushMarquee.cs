using UnityEditor;
using UnityEngine;

namespace Chisel.Editors
{
    // While a brush is edited (the edit tool, a brush selected), a drag on empty space selects the brush's vertices inside
    // a rectangle instead of box-selecting objects. A press on empty space is first seen by Unity's rect selection, which
    // notes it for picking; the marquee then holds the mouse (duringSceneGui), so Unity doesn't start a box selection when
    // it is dragged, which it only does while nothing holds the mouse. Released without a drag, it lets go and Unity picks
    // as before; dragged, Unity is told to forget the press, as it would otherwise pick on the release too, at where the
    // drag started. Each brush editor selects in its brush on release (TryGetReleased), read in the release's event: the
    // release is handled before them (beforeSceneGui). The drag's state is reset on entering play mode, as a domain reload
    // would.
#if UNITY_6000_7_OR_NEWER
    [Unity.Scripting.LifecycleManagement.AutoStaticsCleanup]
#endif
    static partial class ChiselBrushMarquee
    {
        static readonly int kMarqueeHash = "ChiselBrushMarquee".GetHashCode();

        // further than this from where the mouse was pressed, it is a drag rather than a click
        internal const float kDragThreshold = 6;

        static int           s_ControlID;
        static Vector2       s_Start;
        static Vector2       s_Current;
        static bool          s_Dragged;
        static bool          s_Released;
        static SelectionType s_ReleasedType;
        // the key up of an Escape that cancelled a drag, which would otherwise deselect the brush
        static bool          s_SwallowEscape;

        static bool Holding  => s_ControlID != 0 && GUIUtility.hotControl == s_ControlID;
        internal static bool Dragging => Holding && s_Dragged;

        internal static Rect Rect => Rect.MinMaxRect(Mathf.Min(s_Start.x, s_Current.x), Mathf.Min(s_Start.y, s_Current.y),
                                                     Mathf.Max(s_Start.x, s_Current.x), Mathf.Max(s_Start.y, s_Current.y));

        // the rectangle and how it selects, in the event the mouse was released in
        internal static bool TryGetReleased(out Rect rect, out SelectionType selectionType)
        {
            rect          = Rect;
            selectionType = s_ReleasedType;
            return s_Released;
        }

        internal static void Cancel()
        {
            if (Holding)
                GUIUtility.hotControl = 0;
            s_Dragged  = false;
            s_Released = false;
        }

        static bool IsEditingABrush() => ChiselEditGeneratorTool.IsActive() && ChiselEditGeneratorTool.IsABrushSelected();

        internal static void OnBeforeSceneGUI(SceneView sceneView)
        {
            var controlID   = GUIUtility.GetControlID(kMarqueeHash, FocusType.Passive);
            var wasDragging = Dragging;
            // Unity would pick on the release too, at where the drag started
            if (HandleEvent(Event.current, controlID, IsEditingABrush()) &&
                ChiselRectSelection.Valid && ChiselRectSelection.SceneView != null)
                ChiselRectSelection.IsNearestControl = false;
            if (wasDragging || Dragging)
                sceneView.Repaint();
        }

        internal static void OnDuringSceneGUI(SceneView sceneView)
        {
            var evt = Event.current;
            if (evt.type == EventType.MouseDown && ChiselRectSelection.Valid)
            {
                ChiselRectSelection.SceneView = sceneView;
                HandlePress(evt, s_ControlID, ChiselRectSelection.IsNearestControl, IsEditingABrush());
            }
            if (Dragging && evt.type == EventType.Repaint)
                SceneHandleUtility.DrawSelectionRectangle(Rect);
        }

        // A press on empty space, after Unity's rect selection noted it: held, so a drag is the marquee's
        internal static void HandlePress(Event evt, int controlID, bool unityNotedIt, bool editing)
        {
            if (!editing || !unityNotedIt || evt.type != EventType.MouseDown || evt.button != 0 || evt.alt ||
                GUIUtility.hotControl != 0 || controlID == 0)
                return;
            s_ControlID = controlID;
            GUIUtility.hotControl = controlID;
            s_Start   = evt.mousePosition;
            s_Current = evt.mousePosition;
            s_Dragged = false;
        }

        // The drag, release and Escape of a held press, before anything else sees them; true in the event the press
        // becomes a drag, when Unity's rect selection is to forget it
        internal static bool HandleEvent(Event evt, int controlID, bool editing)
        {
            // a release is read by the brush editors in the event it happens in
            s_Released  = false;
            s_ControlID = controlID;
            if (!editing)
            {
                Cancel();
                return false;
            }

            switch (evt.type)
            {
                case EventType.MouseDrag:
                {
                    if (!Holding)
                        break;
                    s_Current = evt.mousePosition;
                    evt.Use();
                    if (s_Dragged || (s_Current - s_Start).magnitude <= kDragThreshold)
                        break;
                    s_Dragged = true;
                    return true;
                }
                case EventType.MouseUp:
                {
                    if (evt.button != 0 || !Holding)
                        break;
                    GUIUtility.hotControl = 0;
                    if (!s_Dragged)
                        break;  // a click: Unity picks, as it noted the press
                    s_Dragged      = false;
                    s_Current      = evt.mousePosition;
                    s_Released     = true;
                    s_ReleasedType = ChiselRectSelectionManager.GetSelectionType(evt);
                    break;
                }
                case EventType.KeyDown:
                {
                    if (evt.keyCode != KeyCode.Escape || !Holding)
                        break;
                    Cancel();
                    s_SwallowEscape = true;
                    evt.Use();
                    break;
                }
                case EventType.KeyUp:
                {
                    if (evt.keyCode != KeyCode.Escape || !s_SwallowEscape)
                        break;
                    s_SwallowEscape = false;
                    evt.Use();
                    break;
                }
            }
            return false;
        }
    }
}
