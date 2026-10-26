using Chisel.Components;
using Chisel.Core;
using UnityEditor;
using UnityEngine;

namespace Chisel.Editors
{
    // A paint mode shows what its kind uses: a color mode its default color, a value mode its default, range and group,
    // and the components as toggles (named RGBA for the Color attribute)
    [CustomPropertyDrawer(typeof(ChiselPaintMode))]
    sealed class ChiselPaintModeDrawer : PropertyDrawer
    {
        static readonly GUIContent kNameContent       = new("Name", "What the mode is called in the paint tool.");
        static readonly GUIContent kKindContent       = new("Kind", "Color: the brush paints a color. Value: the brush paints a number (a height, a layer's weight), up or down.");
        static readonly GUIContent kAttributeContent  = new("Attribute", "The vertex attribute the paint is written to; the shader reads it from there. TexCoord0 and TexCoord1 are taken.");
        static readonly GUIContent kComponentsContent = new("Components", "The components of the attribute the mode writes; the others keep their default.");
        static readonly GUIContent kDefaultColor      = new("Default", "What an unpainted surface has.");
        static readonly GUIContent kDefaultValue      = new("Default", "What an unpainted surface has, in each of the mode's components.");
        static readonly GUIContent kRangeContent      = new("Range", "The lowest and highest value: the brush keeps paint between them.");
        static readonly GUIContent kGroupContent      = new("Group", "Value modes with the same group (above 0), in one attribute, are weights that always sum to one: painting one takes from the others. 0 is no group.");
        static readonly string[]   kXYZW = { "X", "Y", "Z", "W" };
        static readonly string[]   kRGBA = { "R", "G", "B", "A" };

        static float Line => EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            if (!property.isExpanded)
                return EditorGUIUtility.singleLineHeight;
            var kind = property.FindPropertyRelative(nameof(ChiselPaintMode.kind));
            var value = kind.hasMultipleDifferentValues || kind.enumValueIndex == (int)ChiselPaintKind.Value;
            // foldout, name, kind, attribute, components, default, and range and group for a value
            return Line * (value ? 8 : 6);
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            var name       = property.FindPropertyRelative(nameof(ChiselPaintMode.name));
            var kind       = property.FindPropertyRelative(nameof(ChiselPaintMode.kind));
            var attribute  = property.FindPropertyRelative(nameof(ChiselPaintMode.attribute));
            var components = property.FindPropertyRelative(nameof(ChiselPaintMode.components));
            var defaults   = property.FindPropertyRelative(nameof(ChiselPaintMode.defaultValue));
            var range      = property.FindPropertyRelative(nameof(ChiselPaintMode.range));
            var group      = property.FindPropertyRelative(nameof(ChiselPaintMode.group));

            var row = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);
            var title = string.IsNullOrEmpty(name.stringValue) ? label : new GUIContent(name.stringValue);
            property.isExpanded = EditorGUI.Foldout(row, property.isExpanded, title, true);
            if (!property.isExpanded)
                return;

            using (new EditorGUI.IndentLevelScope())
            {
                row.y += Line; EditorGUI.PropertyField(row, name, kNameContent);
                row.y += Line; EditorGUI.PropertyField(row, kind, kKindContent);
                row.y += Line; EditorGUI.PropertyField(row, attribute, kAttributeContent);
                row.y += Line; ComponentToggles(row, components, attribute.enumValueIndex == 0 ? kRGBA : kXYZW);

                var isValue = kind.hasMultipleDifferentValues || kind.enumValueIndex == (int)ChiselPaintKind.Value;
                row.y += Line;
                if (!isValue)
                {
                    EditorGUI.BeginChangeCheck();
                    var color = EditorGUI.ColorField(row, kDefaultColor, (Vector4)defaults.vector4Value, true, true, false);
                    if (EditorGUI.EndChangeCheck())
                        defaults.vector4Value = color;
                    return;
                }

                // one default for all of a value mode's components
                EditorGUI.BeginChangeCheck();
                var first = FirstComponent(components.intValue);
                var value = EditorGUI.FloatField(row, kDefaultValue, defaults.vector4Value[first]);
                if (EditorGUI.EndChangeCheck())
                    defaults.vector4Value = new Vector4(value, value, value, value);
                row.y += Line; EditorGUI.PropertyField(row, range, kRangeContent);
                row.y += Line; group.intValue = Mathf.Max(0, EditorGUI.IntField(row, kGroupContent, group.intValue));
            }
        }

        static int FirstComponent(int components)
        {
            for (int c = 0; c < 4; c++)
            {
                if ((components & (1 << c)) != 0)
                    return c;
            }
            return 0;
        }

        // The components as four toggle buttons
        static void ComponentToggles(Rect row, SerializedProperty components, string[] names)
        {
            using (new EditorGUI.PropertyScope(row, kComponentsContent, components))
            {
                var field = EditorGUI.PrefixLabel(row, kComponentsContent);
                var indent = EditorGUI.indentLevel;
                EditorGUI.indentLevel = 0;
                var width = field.width / 4;
                var value = components.intValue;
                EditorGUI.BeginChangeCheck();
                for (int c = 0; c < 4; c++)
                {
                    var style = c == 0 ? EditorStyles.miniButtonLeft : c == 3 ? EditorStyles.miniButtonRight : EditorStyles.miniButtonMid;
                    var on = GUI.Toggle(new Rect(field.x + width * c, field.y, width, field.height), (value & (1 << c)) != 0, names[c], style);
                    value = on ? value | (1 << c) : value & ~(1 << c);
                }
                if (EditorGUI.EndChangeCheck())
                    components.intValue = value;
                EditorGUI.indentLevel = indent;
            }
        }
    }
}
