using UnityEngine;
using UnityEditor.ShortcutManagement;

namespace Chisel.Editors
{
    public class ChiselKeyboardDefaults
    {
        public const KeyCode kCancelKey = KeyCode.Escape;

        public const string     ShortCutCreateBase      = "Chisel/Create/";
        public const string     ShortCutEditModeBase    = "Chisel/Edit Mode/";
        
        public const KeyCode SwitchToCreateEditMode     = KeyCode.F1;
        //public const KeyCode SwitchToObjectEditMode   = KeyCode.F2;
        public const KeyCode SwitchToShapeEditMode      = KeyCode.F2;
        public const KeyCode SwitchToUVMoveMode         = KeyCode.F3;
        public const KeyCode SwitchToUVRotateMode       = KeyCode.F4;
        public const KeyCode SwitchToUVScaleMode        = KeyCode.F5;
        public const KeyCode SwitchToPivotEditMode      = KeyCode.F6;


        // picking a generator to draw, in the Scene view while Chisel's tools are active; as in RealtimeCSG where it had them
        public const KeyCode BoxGeneratorKey            = KeyCode.B;
        public const KeyCode CylinderGeneratorKey       = KeyCode.C;
        public const KeyCode CapsuleGeneratorKey        = KeyCode.Alpha3;
        public const KeyCode SphereGeneratorKey         = KeyCode.K;
        public const KeyCode HemisphereGeneratorKey     = KeyCode.Alpha5;
        public const KeyCode StadiumGeneratorKey        = KeyCode.Alpha6;
        public const KeyCode TorusGeneratorKey          = KeyCode.Alpha7;
        public const KeyCode LinearStairsGeneratorKey   = KeyCode.L;
        public const KeyCode SpiralStairsGeneratorKey   = KeyCode.P;
        public const KeyCode PathedStairsGeneratorKey   = KeyCode.Alpha0;
        public const KeyCode FreeDrawGeneratorKey       = KeyCode.N;
        public const KeyCode RevolvedShapeGeneratorKey  = KeyCode.Alpha4;


        public const KeyCode            HalfGridSizeKey                 = KeyCode.LeftBracket;
        public const ShortcutModifiers  HalfGridSizeModifiers           = ShortcutModifiers.None;
        public const KeyCode            DoubleGridSizeKey               = KeyCode.RightBracket;
        public const ShortcutModifiers  DoubleGridSizeModifiers         = ShortcutModifiers.None;

        public const KeyCode            ToggleShowGridKey               = KeyCode.G;
        public const ShortcutModifiers  ToggleShowGridModifiers         = ShortcutModifiers.Shift;

        public const KeyCode            ToggleBoundsSnappingKey         = KeyCode.B;
        public const ShortcutModifiers  ToggleBoundsSnappingModifiers   = ShortcutModifiers.Shift;
        public const KeyCode            TogglePivotSnappingKey          = KeyCode.P;
        public const ShortcutModifiers  TogglePivotSnappingModifiers    = ShortcutModifiers.Shift;
    }
}