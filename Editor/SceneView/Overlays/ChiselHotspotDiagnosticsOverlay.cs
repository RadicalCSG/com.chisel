using UnityEditor;
using UnityEditor.Overlays;
using UnityEngine;

namespace Chisel.Editors
{
    // The hotspot diagnostic view's controls (ChiselHotspotDiagnosticsView); Tools > Chisel > Hotspot Diagnostics shows it
    [Overlay(typeof(SceneView), kOverlayId, kOverlayTitle)]
    public class ChiselHotspotDiagnosticsOverlay : IMGUIOverlay
    {
        const string kOverlayId    = "chisel-hotspot-diagnostics";
        const string kOverlayTitle = "Chisel Hotspot Diagnostics";

        readonly static GUIContent kMode  = new("View",  "What the colours show: the texture's density against its tile's, how much more one way than the other, or both");
        readonly static GUIContent kEdges = new("Edges", "The edges of the triangles that are drawn");
        readonly static GUIContent kSeams = new("Seams", "Where the uvs jump between triangles");

        [MenuItem("Tools/Chisel/Hotspot Diagnostics")]
        static void Show()
        {
            var sceneView = SceneView.lastActiveSceneView;
            if (sceneView && sceneView.TryGetOverlay(kOverlayId, out var overlay))
            {
                overlay.displayed = true;
                overlay.collapsed = false;
            }
            if (ChiselHotspotDiagnosticsView.Mode == HotspotDiagnosticMode.Off)
                ChiselHotspotDiagnosticsView.Mode = HotspotDiagnosticMode.Distortion;
        }

        public override void OnGUI()
        {
            ChiselHotspotDiagnosticsView.Mode      = (HotspotDiagnosticMode)EditorGUILayout.EnumPopup(kMode, ChiselHotspotDiagnosticsView.Mode);
            using (new EditorGUI.DisabledScope(ChiselHotspotDiagnosticsView.Mode == HotspotDiagnosticMode.Off))
            {
                ChiselHotspotDiagnosticsView.ShowEdges = EditorGUILayout.ToggleLeft(kEdges, ChiselHotspotDiagnosticsView.ShowEdges);
                ChiselHotspotDiagnosticsView.ShowSeams = EditorGUILayout.ToggleLeft(kSeams, ChiselHotspotDiagnosticsView.ShowSeams);
            }
            var mode = ChiselHotspotDiagnosticsView.Mode;
            if (mode == HotspotDiagnosticMode.Off)
                return;

            EditorGUILayout.Space();
            switch (mode)
            {
                case HotspotDiagnosticMode.Density:
                    Legend(Color.red,   "stretched: half the tile's pixels or fewer");
                    Legend(Color.green, "the tile's own density");
                    Legend(Color.blue,  "squeezed: twice the tile's pixels or more");
                    break;
                case HotspotDiagnosticMode.Anisotropy:
                    Legend(Color.green,  "as much one way as the other");
                    Legend(Color.yellow, "1.4 times");
                    Legend(Color.red,    "twice or more");
                    break;
                default:
                    Legend(Color.green,  "no distortion");
                    Legend(Color.yellow, "some");
                    Legend(Color.red,    "much (energy 4 or more)");
                    break;
            }
            Legend(ChiselHotspotDiagnosticsView.kMirroredColor,   "mirrored against the rest of its surface");
            Legend(ChiselHotspotDiagnosticsView.kLastResortColor, "outline: repeated, no tile large enough");
            Legend(ChiselHotspotDiagnosticsView.kAllBorderColor,  "outline: too small for its border, all border");

            var summary = ChiselHotspotDiagnosticsView.LastSummary;
            EditorGUILayout.Space();
            EditorGUILayout.LabelField($"{summary.surfaces} surfaces, {summary.triangles} triangles in {summary.models} models", EditorStyles.miniLabel);
            if (summary.triangles > 0)
            {
                EditorGUILayout.LabelField($"density {summary.leastDensity:0.00} .. {summary.mostDensity:0.00}, anisotropy up to {summary.mostAnisotropy:0.00}", EditorStyles.miniLabel);
                EditorGUILayout.LabelField($"energy up to {summary.mostEnergy:0.00}, {summary.mirrored} mirrored", EditorStyles.miniLabel);
                EditorGUILayout.LabelField($"{summary.lastResort} repeated as a last resort, {summary.allBorder} all border, {summary.pinned} pinned", EditorStyles.miniLabel);
            }
        }

        static void Legend(Color color, string text)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                var rect = GUILayoutUtility.GetRect(12, 12, GUILayout.Width(12), GUILayout.Height(12));
                color.a = 1;
                EditorGUI.DrawRect(rect, color);
                EditorGUILayout.LabelField(text, EditorStyles.miniLabel);
            }
        }
    }
}
