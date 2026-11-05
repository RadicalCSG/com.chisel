using Chisel.Core;

using UnityEditor;
using UnityEditor.EditorTools;
using UnityEditor.ShortcutManagement;

using K = Chisel.Editors.ChiselKeyboardDefaults;

namespace Chisel.Editors
{
    // A key for each generator: it is picked and drawn with the create tool. In the Scene view, only while Chisel's
    // tools are active: their tag is registered then, so the keys are free everywhere else
    [InitializeOnLoad]
#if UNITY_6000_7_OR_NEWER
    [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
#endif
    static class ChiselGeneratorShortcuts
    {
        const string kBase = K.ShortCutCreateBase;
        internal const string kTag = "Chisel Tools";

        // the shortcut manager isn't there yet while the editor loads
        static ChiselGeneratorShortcuts() { EditorApplication.delayCall += Initialize; }

        static void Initialize()
        {
            ToolManager.activeContextChanged -= UpdateTag;
            ToolManager.activeContextChanged += UpdateTag;
            UpdateTag();
        }

        internal static bool TagRegistered { get; private set; }

        static void UpdateTag()
        {
            var active = ToolManager.activeContextType == typeof(ChiselToolsContext);
            if (active == TagRegistered)
                return;
            if (active) ShortcutManager.RegisterTag(kTag);
            else        ShortcutManager.UnregisterTag(kTag);
            TagRegistered = active;
        }

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

        [Shortcut(kBase + ChiselBoxDefinition.kNodeTypeName,           typeof(SceneView), kTag, K.BoxGeneratorKey)]           static void Box()           { Pick(ChiselBoxDefinition.kNodeTypeName); }
        [Shortcut(kBase + ChiselCylinderDefinition.kNodeTypeName,      typeof(SceneView), kTag, K.CylinderGeneratorKey)]      static void Cylinder()      { Pick(ChiselCylinderDefinition.kNodeTypeName); }
        [Shortcut(kBase + ChiselCapsuleDefinition.kNodeTypeName,       typeof(SceneView), kTag, K.CapsuleGeneratorKey)]       static void Capsule()       { Pick(ChiselCapsuleDefinition.kNodeTypeName); }
        [Shortcut(kBase + ChiselSphereDefinition.kNodeTypeName,        typeof(SceneView), kTag, K.SphereGeneratorKey)]        static void Sphere()        { Pick(ChiselSphereDefinition.kNodeTypeName); }
        [Shortcut(kBase + ChiselHemisphereDefinition.kNodeTypeName,    typeof(SceneView), kTag, K.HemisphereGeneratorKey)]    static void Hemisphere()    { Pick(ChiselHemisphereDefinition.kNodeTypeName); }
        [Shortcut(kBase + ChiselStadiumDefinition.kNodeTypeName,       typeof(SceneView), kTag, K.StadiumGeneratorKey)]       static void Stadium()       { Pick(ChiselStadiumDefinition.kNodeTypeName); }
        [Shortcut(kBase + ChiselTorusDefinition.kNodeTypeName,         typeof(SceneView), kTag, K.TorusGeneratorKey)]         static void Torus()         { Pick(ChiselTorusDefinition.kNodeTypeName); }
        [Shortcut(kBase + ChiselLinearStairsDefinition.kNodeTypeName,  typeof(SceneView), kTag, K.LinearStairsGeneratorKey)]  static void LinearStairs()  { Pick(ChiselLinearStairsDefinition.kNodeTypeName); }
        [Shortcut(kBase + ChiselSpiralStairsDefinition.kNodeTypeName,  typeof(SceneView), kTag, K.SpiralStairsGeneratorKey)]  static void SpiralStairs()  { Pick(ChiselSpiralStairsDefinition.kNodeTypeName); }
        [Shortcut(kBase + ChiselPathedStairsDefinition.kNodeTypeName,  typeof(SceneView), kTag, K.PathedStairsGeneratorKey)]  static void PathedStairs()  { Pick(ChiselPathedStairsDefinition.kNodeTypeName); }
        [Shortcut(kBase + kFreeDraw,                                   typeof(SceneView), kTag, K.FreeDrawGeneratorKey)]      static void FreeDraw()      { Pick(kFreeDraw); }
        [Shortcut(kBase + ChiselRevolvedShapeDefinition.kNodeTypeName, typeof(SceneView), kTag, K.RevolvedShapeGeneratorKey)] static void RevolvedShape() { Pick(ChiselRevolvedShapeDefinition.kNodeTypeName); }

        internal const string kFreeDraw = "Free Draw";
    }
}
