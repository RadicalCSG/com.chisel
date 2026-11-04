using Unity.Mathematics;
using UnityEditor;
using UnityEngine;

namespace Chisel.Editors
{
    // Bounds as a foldout with a row each for its min and max. Unity's own drawing of a float3 inside a foldout indents
    // its X, Y and Z labels under the number fields, so these rows draw their fields themselves, unindented
    [CustomPropertyDrawer(typeof(MinMaxAABB))]
#if UNITY_6000_7_OR_NEWER
    [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
#endif
    public sealed class MinMaxAABBDrawer : PropertyDrawer
    {
        static readonly GUIContent[] kAxes = { new("X"), new("Y"), new("Z") };
        static readonly GUIContent   kMin  = new("Min");
        static readonly GUIContent   kMax  = new("Max");

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            var line = EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;
            return property.isExpanded ? line * 3 - EditorGUIUtility.standardVerticalSpacing : EditorGUIUtility.singleLineHeight;
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);
            var row = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);
            property.isExpanded = EditorGUI.Foldout(row, property.isExpanded, label, true);
            if (property.isExpanded)
            {
                using (new EditorGUI.IndentLevelScope())
                {
                    row.y += EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;
                    Row(row, property.FindPropertyRelative(nameof(MinMaxAABB.Min)), kMin);
                    row.y += EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;
                    Row(row, property.FindPropertyRelative(nameof(MinMaxAABB.Max)), kMax);
                }
            }
            EditorGUI.EndProperty();
        }

        static void Row(Rect row, SerializedProperty vector, GUIContent label)
        {
            if (vector == null)
                return;
            var x = vector.FindPropertyRelative("x");
            var y = vector.FindPropertyRelative("y");
            var z = vector.FindPropertyRelative("z");
            EditorGUI.BeginProperty(row, label, vector);
            var fields = EditorGUI.PrefixLabel(row, label);
            var indent = EditorGUI.indentLevel;
            EditorGUI.indentLevel = 0;
            try
            {
                var values = new[] { x.floatValue, y.floatValue, z.floatValue };
                EditorGUI.showMixedValue = vector.hasMultipleDifferentValues;
                EditorGUI.BeginChangeCheck();
                EditorGUI.MultiFloatField(fields, kAxes, values);
                if (EditorGUI.EndChangeCheck())
                {
                    x.floatValue = values[0];
                    y.floatValue = values[1];
                    z.floatValue = values[2];
                }
            }
            finally
            {
                EditorGUI.showMixedValue = false;
                EditorGUI.indentLevel = indent;
                EditorGUI.EndProperty();
            }
        }
    }
}
