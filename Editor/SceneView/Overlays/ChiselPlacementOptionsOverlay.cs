using UnityEngine;
using UnityEditor;
using UnityEditor.Overlays;

namespace Chisel.Editors
{
    [Overlay(typeof(SceneView), ChiselPlacementOptionsOverlay.kOverlayTitle)]
    public class ChiselPlacementOptionsOverlay : IMGUIOverlay, ITransientOverlay
    {
        public const string kOverlayTitle = "Placement Options";

        // TODO: CLEAN THIS UP
        public const int kMinWidth = ((245 + 32) - ((32 + 2) * ChiselPlacementToolsSelectionWindow.kToolsWide)) + (ChiselPlacementToolsSelectionWindow.kButtonSize * ChiselPlacementToolsSelectionWindow.kToolsWide);
        public readonly static GUILayoutOption kMinWidthLayout = GUILayout.MinWidth(kMinWidth);

        static bool s_Visible = false;
        public bool visible { get { return s_Visible && Tools.current == Tool.Custom; } }

        public static void Show() { s_Visible = true; }
        public static void Hide() { s_Visible = false; }

        static ChiselPlacementToolInstance currentInstance;

        public override void OnGUI()
        {
            EditorGUILayout.GetControlRect(false, 0, kMinWidthLayout);
            var sceneView = containerWindow as SceneView;
            var generatorMode = ChiselGeneratorManager.GeneratorMode;
            if (currentInstance != generatorMode)
            {
                currentInstance = generatorMode;
                this.displayName = $"Create {generatorMode.ToolName}";
            }
            generatorMode.OnSceneSettingsGUI(sceneView);
        }
    }
}
