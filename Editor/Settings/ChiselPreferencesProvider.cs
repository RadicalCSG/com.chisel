using UnityEditor;
using UnityEngine;

namespace Chisel.Editors
{
    // Chisel's settings for this user, in Unity's Preferences
#if UNITY_6000_7_OR_NEWER
    [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
#endif
    sealed class ChiselPreferencesProvider : SettingsProvider
    {
        public const string kPath = "Preferences/Chisel";

        static readonly GUIContent kDistanceUnitContent = new("Distance Unit", "The unit lengths are shown in, in the scene view and the inspector, and typed in when one is edited in the scene view. A length typed with a unit after it (\"50 cm\") is read in that unit.");
        static readonly GUIContent kDefaultUVScaleContent = new("Default UV Scale", "The UV scale every surface of a new brush starts with: the Scale its UVs show in the inspector.");

        ChiselPreferencesProvider(string path, SettingsScope scope) : base(path, scope) { }

        [SettingsProvider]
        static SettingsProvider Create()
        {
            return new ChiselPreferencesProvider(kPath, SettingsScope.User) { keywords = new[] { "unit", "distance", "meters", "inches", "feet", "uv", "scale", "texture" } };
        }

        public override void OnGUI(string searchContext)
        {
            EditorGUI.BeginChangeCheck();
            var unit = (DistanceUnit)EditorGUILayout.EnumPopup(kDistanceUnitContent, ChiselEditorSettings.DistanceUnit);
            if (EditorGUI.EndChangeCheck())
            {
                ChiselEditorSettings.DistanceUnit  = unit;
                ChiselEditorSettings.Save();
                SceneView.RepaintAll();
            }

            EditorGUI.BeginChangeCheck();
            var uvScale = EditorGUILayout.FloatField(kDefaultUVScaleContent, ChiselEditorSettings.DefaultUVScale);
            if (EditorGUI.EndChangeCheck())
            {
                ChiselEditorSettings.DefaultUVScale = Mathf.Max(uvScale, (float)Chisel.Core.UVMatrix.kMinScale);
                ChiselEditorSettings.Save();
            }
        }
    }
}
