using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Chisel.Editors
{
    // The colors Chisel draws with in the scene view and the hierarchy, which the user can set in its preferences
#if UNITY_6000_7_OR_NEWER
    // only what each color is; the colors themselves are kept in EditorPrefs
    [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
#endif
    public static class ChiselColors
    {
        public const string kOutlines    = "Outlines";
        public const string kHandles     = "Handles";
        public const string kBrushEditor = "Brush Editor";
        public const string kDecals      = "Decals";
        public const string kHotspots    = "Hotspots";
        public const string kPaint       = "Paint";
        public const string kHierarchy   = "Hierarchy";

        static ChiselColorSetting Setting(string group, string name, string tooltip, Color defaultColor)
        {
            return new ChiselColorSetting("Chisel/" + group + "/" + name, group, name, tooltip, defaultColor);
        }

        static ChiselColorSetting Setting(string group, string name, string tooltip, System.Func<Color> defaultColor)
        {
            return new ChiselColorSetting("Chisel/" + group + "/" + name, group, name, tooltip, defaultColor);
        }

        public static readonly ChiselColorSetting UnselectedOutline = Setting(kOutlines, "Outline",
            "The outline of a brush that isn't selected.", new Color(94f / 255f, 119f / 255f, 155f / 255f, 1f));

        public static readonly ChiselColorSetting PreSelectedOutline = Setting(kOutlines, "Hovered Outline",
            "The outline of a surface under the mouse, before it is selected.", new Color(201f / 255f, 200f / 255f, 144f / 255f, 1f));

        public static readonly ChiselColorSetting SelectedOutline = Setting(kOutlines, "Selected Outline",
            "The outline of a selected brush or surface.", new Color(255f / 255f, 102f / 255f, 0f, 1f));

        public static readonly ChiselColorSetting SelectedHoverOutline = Setting(kOutlines, "Hovered Selected Outline",
            "The outline of a selected surface under the mouse.", new Color(246f / 255f, 242f / 255f, 50f / 255f, 1f));

        public static readonly ChiselColorSetting Handles = Setting(kHandles, "Handles",
            "Chisel's own handles, such as a generator's bounds and the placement tools' shapes.",
            new Color(Mathf.LinearToGammaSpace(61f / 255f), Mathf.LinearToGammaSpace(200f / 255f), Mathf.LinearToGammaSpace(255f / 255f), 0.89f));

        public static readonly ChiselColorSetting HoveredHandle = Setting(kHandles, "Hovered Handle",
            "A handle under the mouse.", new Color(201f / 255, 200f / 255, 144f / 255, 0.89f));

        public static readonly ChiselColorSetting Measurements = Setting(kHandles, "Measurements",
            "Distances, sizes and angles shown in the scene view, and the turn buttons beside them.",
            new Color(Mathf.LinearToGammaSpace(0.788f), Mathf.LinearToGammaSpace(0.784f), Mathf.LinearToGammaSpace(0.565f), 0.890f));

        public static readonly ChiselColorSetting Invalid = Setting(kHandles, "Invalid",
            "What can't be done or isn't valid: a brush being edited that isn't a valid shape, a point drawn on top of another, an edge no trim decal fits on.",
            Color.red);

        public static readonly ChiselColorSetting BrushEditorEdges = Setting(kBrushEditor, "Brush Editor Edges",
            "The edges and vertices of a brush being edited; those facing away are drawn fainter. Unity's Z Axis color unless it is set.",
            () => UnityEditor.Handles.zAxisColor);

        public static readonly ChiselColorSetting BrushEditorPolygons = Setting(kBrushEditor, "Brush Editor Polygons",
            "The handles of the polygons of a brush being edited; those facing away are drawn fainter. Unity's Z Axis color unless it is set.",
            () => UnityEditor.Handles.zAxisColor);

        public static readonly ChiselColorSetting DecalHandles = Setting(kDecals, "Decal Handles",
            "A selected decal's box, outline and handles.", new Color(1.0f, 0.6f, 0.1f, 1.0f));

        public static readonly ChiselColorSetting TrimEdge = Setting(kDecals, "Trim Edge",
            "An edge under the mouse that a trim decal can be put on.", new Color(1.0f, 0.85f, 0.1f, 1.0f));

        public static readonly ChiselColorSetting HotspotEdgeBorder = Setting(kHotspots, "Edge Border",
            "UV Connect: an edge under the mouse whose border a click hides.", Color.yellow);

        public static readonly ChiselColorSetting HotspotHiddenEdgeBorder = Setting(kHotspots, "Hidden Edge Border",
            "UV Connect: an edge under the mouse without a border, which a click brings back.", Color.cyan);

        public static readonly ChiselColorSetting HotspotConnect = Setting(kHotspots, "Connect",
            "UV Connect: dragging onto a surface that will be connected.", Color.green);

        public static readonly ChiselColorSetting HotspotDisconnect = Setting(kHotspots, "Disconnect",
            "UV Connect: dragging onto a connected surface, which will be disconnected.", Color.red);

        public static readonly ChiselColorSetting PaintBrush = Setting(kPaint, "Paint Brush",
            "The circles of the paint brush on a surface.", new Color(1, 1, 1, 0.8f));

        public static readonly ChiselColorSetting ActiveModel = Setting(kHierarchy, "Active Model",
            "The name of the active model in the hierarchy, which new brushes go into.", new Color(0.4f, 0.8f, 1.0f));

        public static readonly IReadOnlyList<ChiselColorSetting> All = new[]
        {
            UnselectedOutline, PreSelectedOutline, SelectedOutline, SelectedHoverOutline,
            Handles, HoveredHandle, Measurements, Invalid,
            BrushEditorEdges, BrushEditorPolygons,
            DecalHandles, TrimEdge,
            HotspotEdgeBorder, HotspotHiddenEdgeBorder, HotspotConnect, HotspotDisconnect,
            PaintBrush,
            ActiveModel
        };
    }
}
