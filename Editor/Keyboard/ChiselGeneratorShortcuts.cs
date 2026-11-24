using Chisel.Core;

using UnityEditor;
using UnityEditor.EditorTools;
using UnityEditor.ShortcutManagement;

using K = Chisel.Editors.ChiselKeyboardDefaults;

namespace Chisel.Editors
{
    // A key for each generator: it is picked and drawn with the create tool. Their shortcut context is only active in
    // the Scene view while Chisel's tools are; everywhere else the keys are Unity's, as Unity only looks at its global
    // shortcuts when no active context has the key
    [InitializeOnLoad]
#if UNITY_6000_7_OR_NEWER
    [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
#endif
    static class ChiselGeneratorShortcuts
    {
        const string kBase = K.ShortCutCreateBase;

        sealed class Context : IShortcutContext
        {
            public bool active => IsInUse(EditorWindow.focusedWindow is SceneView, ToolManager.activeContextType == typeof(ChiselToolsContext));
        }

        static readonly Context s_Context = new();

        internal static bool IsInUse(bool inSceneView, bool chiselTools) => inSceneView && chiselTools;

        // the shortcut manager isn't there yet while the editor loads
        static ChiselGeneratorShortcuts() { EditorApplication.delayCall += () => ShortcutManager.RegisterContext(s_Context); }

        // the key the generator with this name is bound to now, empty if none
        internal static string KeyOf(string toolName)
        {
            try { return ShortcutManager.instance.GetShortcutBinding(kBase + toolName).ToString(); }
            catch (System.ArgumentException) { return string.Empty; }
        }

        // the generator with this name in the create tool, which is then switched to
        internal static bool Pick(string toolName)
        {
            var modes = ChiselGeneratorManager.s_GeneratorModes;
            if (modes == null)
                return false;
            foreach (var mode in modes)
            {
                if (mode.ToolName != toolName)
                    continue;
                ChiselGeneratorManager.GeneratorMode = mode;
                ChiselPlacementTool.ActivateTool();
                return true;
            }
            return false;
        }

        [Shortcut(kBase + ChiselBoxDefinition.kNodeTypeName,           typeof(Context), K.BoxGeneratorKey)]           static void Box()           { Pick(ChiselBoxDefinition.kNodeTypeName); }
        [Shortcut(kBase + ChiselCylinderDefinition.kNodeTypeName,      typeof(Context), K.CylinderGeneratorKey)]      static void Cylinder()      { Pick(ChiselCylinderDefinition.kNodeTypeName); }
        [Shortcut(kBase + ChiselCapsuleDefinition.kNodeTypeName,       typeof(Context), K.CapsuleGeneratorKey)]       static void Capsule()       { Pick(ChiselCapsuleDefinition.kNodeTypeName); }
        [Shortcut(kBase + ChiselSphereDefinition.kNodeTypeName,        typeof(Context), K.SphereGeneratorKey)]        static void Sphere()        { Pick(ChiselSphereDefinition.kNodeTypeName); }
        [Shortcut(kBase + ChiselHemisphereDefinition.kNodeTypeName,    typeof(Context), K.HemisphereGeneratorKey)]    static void Hemisphere()    { Pick(ChiselHemisphereDefinition.kNodeTypeName); }
        [Shortcut(kBase + ChiselStadiumDefinition.kNodeTypeName,       typeof(Context), K.StadiumGeneratorKey)]       static void Stadium()       { Pick(ChiselStadiumDefinition.kNodeTypeName); }
        [Shortcut(kBase + ChiselTorusDefinition.kNodeTypeName,         typeof(Context), K.TorusGeneratorKey)]         static void Torus()         { Pick(ChiselTorusDefinition.kNodeTypeName); }
        [Shortcut(kBase + ChiselLinearStairsDefinition.kNodeTypeName,  typeof(Context), K.LinearStairsGeneratorKey)]  static void LinearStairs()  { Pick(ChiselLinearStairsDefinition.kNodeTypeName); }
        [Shortcut(kBase + ChiselSpiralStairsDefinition.kNodeTypeName,  typeof(Context), K.SpiralStairsGeneratorKey)]  static void SpiralStairs()  { Pick(ChiselSpiralStairsDefinition.kNodeTypeName); }
        [Shortcut(kBase + ChiselPathedStairsDefinition.kNodeTypeName,  typeof(Context), K.PathedStairsGeneratorKey)]  static void PathedStairs()  { Pick(ChiselPathedStairsDefinition.kNodeTypeName); }
        [Shortcut(kBase + kFreeDraw,                                   typeof(Context), K.FreeDrawGeneratorKey)]      static void FreeDraw()      { Pick(kFreeDraw); }
        [Shortcut(kBase + ChiselRevolvedShapeDefinition.kNodeTypeName, typeof(Context), K.RevolvedShapeGeneratorKey)] static void RevolvedShape() { Pick(ChiselRevolvedShapeDefinition.kNodeTypeName); }

        internal const string kFreeDraw = "Free Draw";
    }
}
