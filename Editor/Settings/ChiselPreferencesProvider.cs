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

        static readonly GUIContent kDistanceUnitContent = new("Distance Unit", "The unit lengths are shown in in the scene view, and typed in when one is edited there. A length typed with a unit after it (\"50 cm\") is read in that unit.");

        ChiselPreferencesProvider(string path, SettingsScope scope) : base(path, scope) { }

        [SettingsProvider]
        static SettingsProvider Create()
        {
            return new ChiselPreferencesProvider(kPath, SettingsScope.User) { keywords = new[] { "unit", "distance", "meters", "inches", "feet" } };
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
        }
    }
}
