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
        static readonly GUIContent kColorsContent         = new("Colors");
        static readonly GUIContent kResetColorContent     = new("Reset", "Back to its default");
        static readonly GUIContent kUseDefaultsContent    = new("Use Defaults", "Every color back to its default");

        ChiselPreferencesProvider(string path, SettingsScope scope) : base(path, scope) { }

        [SettingsProvider]
        static SettingsProvider Create()
        {
            return new ChiselPreferencesProvider(kPath, SettingsScope.User) { keywords = new[] { "unit", "distance", "meters", "inches", "feet", "uv", "scale", "texture", "color", "colour", "outline", "brush editor" } };
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

            EditorGUILayout.Space();
            EditorGUILayout.LabelField(kColorsContent, EditorStyles.boldLabel);
            var colorsChanged = false;
            string group = null;
            foreach (var setting in ChiselColors.All)
            {
                if (setting.Group != group)
                {
                    group = setting.Group;
                    EditorGUILayout.LabelField(group, EditorStyles.miniBoldLabel);
                }
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUI.BeginChangeCheck();
                    var color = EditorGUILayout.ColorField(setting.Content, setting.Value);
                    if (EditorGUI.EndChangeCheck())
                    {
                        setting.Value = color;
                        colorsChanged = true;
                    }
                    using (new EditorGUI.DisabledScope(!setting.IsSet))
                    {
                        if (GUILayout.Button(kResetColorContent, EditorStyles.miniButton, GUILayout.ExpandWidth(false)))
                        {
                            setting.Reset();
                            colorsChanged = true;
                        }
                    }
                }
            }
            if (GUILayout.Button(kUseDefaultsContent, GUILayout.ExpandWidth(false)))
            {
                foreach (var setting in ChiselColors.All)
                    setting.Reset();
                colorsChanged = true;
            }
            if (colorsChanged)
            {
                ChiselOutlineRenderer.OnColorsChanged();
                SceneView.RepaintAll();
                EditorApplication.RepaintHierarchyWindow();
            }
        }
    }
}
