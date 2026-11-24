using UnityEditor;
using UnityEditor.EditorTools;
using UnityEditor.ShortcutManagement;

using K = Chisel.Editors.ChiselKeyboardDefaults;

namespace Chisel.Editors
{
    // While a brush is edited, Unity's Move, Rotate and Scale keys pick the handle on its selected vertices instead of
    // leaving the edit tool. Their shortcut context is only active then (in the Scene view, the edit tool active, a brush
    // selected); everywhere else the keys are Unity's, as Unity only looks at its global shortcuts when no active context
    // has the key
    [InitializeOnLoad]
#if UNITY_6000_7_OR_NEWER
    [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
#endif
    static class ChiselVertexHandleShortcuts
    {
        internal const string kMoveId   = K.ShortCutEditModeBase + "Move Vertices";
        internal const string kRotateId = K.ShortCutEditModeBase + "Rotate Vertices";
        internal const string kScaleId  = K.ShortCutEditModeBase + "Scale Vertices";

        sealed class Context : IShortcutContext
        {
            public bool active => IsInUse(EditorWindow.focusedWindow is SceneView,
                                          ChiselEditGeneratorTool.IsActive(),
                                          ChiselEditGeneratorTool.IsABrushSelected());
        }

        static readonly Context s_Context = new();

        internal static bool IsInUse(bool inSceneView, bool editToolActive, bool brushSelected) => inSceneView && editToolActive && brushSelected;

        // the shortcut manager isn't there yet while the editor loads
        static ChiselVertexHandleShortcuts() { EditorApplication.delayCall += Initialize; }

        static void Initialize()
        {
            ShortcutManager.RegisterContext(s_Context);
            ToolManager.activeToolChanged -= OnActiveToolChanged;
            ToolManager.activeToolChanged += OnActiveToolChanged;
        }

        // each time the edit tool is picked it starts with the move handle
        static void OnActiveToolChanged()
        {
            if (ChiselEditGeneratorTool.IsActive())
                ChiselEditableOutline.VertexHandle = Tool.Move;
        }

        internal static void Pick(Tool tool)
        {
            ChiselEditableOutline.VertexHandle = tool;
            SceneView.RepaintAll();
        }

        [Shortcut(kMoveId,   typeof(Context), K.MoveVerticesKey)]   static void Move()   { Pick(Tool.Move); }
        [Shortcut(kRotateId, typeof(Context), K.RotateVerticesKey)] static void Rotate() { Pick(Tool.Rotate); }
        [Shortcut(kScaleId,  typeof(Context), K.ScaleVerticesKey)]  static void Scale()  { Pick(Tool.Scale); }
    }
}
