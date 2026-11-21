using Chisel.Components;

using UnityEditor;
using UnityEditor.EditorTools;
using UnityEditor.ShortcutManagement;

using K = Chisel.Editors.ChiselKeyboardDefaults;

namespace Chisel.Editors
{
    // While a brush is edited, Unity's Move, Rotate and Scale keys pick the handle on its selected vertices instead of
    // leaving the edit tool. In the Scene view, only while the edit tool is active: their tag is registered then, so the
    // keys are Unity's everywhere else; with no brush selected they pick Unity's tool, as they would have
    [InitializeOnLoad]
#if UNITY_6000_7_OR_NEWER
    [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
#endif
    static class ChiselVertexHandleShortcuts
    {
        internal const string kTag      = "Chisel Edit Generator";
        internal const string kMoveId   = K.ShortCutEditModeBase + "Move Vertices";
        internal const string kRotateId = K.ShortCutEditModeBase + "Rotate Vertices";
        internal const string kScaleId  = K.ShortCutEditModeBase + "Scale Vertices";

        // the shortcut manager isn't there yet while the editor loads
        static ChiselVertexHandleShortcuts() { EditorApplication.delayCall += Initialize; }

        static void Initialize()
        {
            ToolManager.activeToolChanged -= UpdateTag;
            ToolManager.activeToolChanged += UpdateTag;
            UpdateTag();
        }

        internal static bool TagRegistered { get; private set; }

        static void UpdateTag()
        {
            var active = ChiselEditGeneratorTool.IsActive();
            if (active == TagRegistered)
                return;
            if (active)
            {
                // each time the tool is picked it starts with the move handle
                ChiselEditableOutline.VertexHandle = Tool.Move;
                ShortcutManager.RegisterTag(kTag);
            } else
                ShortcutManager.UnregisterTag(kTag);
            TagRegistered = active;
        }

        static bool IsABrushSelected()
        {
            foreach (var gameObject in Selection.gameObjects)
            {
                if (gameObject.TryGetComponent<ChiselBrushComponent>(out _))
                    return true;
            }
            return false;
        }

        internal static void Pick(Tool tool)
        {
            if (!IsABrushSelected())
            {
                Tools.current = tool;
                return;
            }
            ChiselEditableOutline.VertexHandle = tool;
            SceneView.RepaintAll();
        }

        [Shortcut(kMoveId,   typeof(SceneView), kTag, K.MoveVerticesKey)]   static void Move()   { Pick(Tool.Move); }
        [Shortcut(kRotateId, typeof(SceneView), kTag, K.RotateVerticesKey)] static void Rotate() { Pick(Tool.Rotate); }
        [Shortcut(kScaleId,  typeof(SceneView), kTag, K.ScaleVerticesKey)]  static void Scale()  { Pick(Tool.Scale); }
    }
}
