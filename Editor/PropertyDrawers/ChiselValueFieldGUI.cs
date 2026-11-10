using System;
using UnityEditor;
using UnityEngine;

namespace Chisel.Editors
{
    public enum ChiselValueKind
    {
        Distance,   // stored in meters, shown in the active distance unit
        Angle,      // degrees
        Scale       // a factor, shown as a percentage
    }

    // Draws a float or vector field marked with a value attribute, its numbers shown in their unit with the unit after them
#if UNITY_6000_7_OR_NEWER
    [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
#endif
    public static class ChiselValueFieldGUI
    {
        const string kDegreesSuffix = "\u00B0";
        const string kPercentSuffix = "%";
        const float  kSuffixPadding = 4;

        readonly static GUIContent[] kComponentLabels = { new("X"), new("Y"), new("Z") };
        readonly static GUIContent   kSuffixContent   = new();
        static GUIStyle s_SuffixStyle;

        public static string Suffix(ChiselValueKind kind)
        {
            switch (kind)
            {
                case ChiselValueKind.Distance: return Units.GetUnitString(Units.ActiveDistanceUnit);
                case ChiselValueKind.Angle:    return kDegreesSuffix;
                case ChiselValueKind.Scale:    return kPercentSuffix;
            }
            return string.Empty;
        }

        public static float ToShown(ChiselValueKind kind, float value)
        {
            switch (kind)
            {
                case ChiselValueKind.Distance: return (float)Units.UnityToDistanceUnit(Units.ActiveDistanceUnit, value);
                case ChiselValueKind.Scale:    return value * 100.0f;
            }
            return value;
        }

        public static float FromShown(ChiselValueKind kind, float shown)
        {
            switch (kind)
            {
                case ChiselValueKind.Distance: return Units.DistanceUnitToUnity(Units.ActiveDistanceUnit, shown);
                case ChiselValueKind.Scale:    return shown / 100.0f;
            }
            return shown;
        }

        public static Vector3 ToShown(ChiselValueKind kind, Vector3 value)
        {
            return new Vector3(ToShown(kind, value.x), ToShown(kind, value.y), ToShown(kind, value.z));
        }

        // Only the components that were changed are converted back, so the others keep their exact values
        public static Vector3 Edited(ChiselValueKind kind, Vector3 value, Vector3 shownBefore, Vector3 shownAfter)
        {
            for (int i = 0; i < 3; i++)
            {
                if (shownAfter[i] != shownBefore[i])
                    value[i] = FromShown(kind, shownAfter[i]);
            }
            return value;
        }

        public static float GetPropertyHeight(SerializedProperty property)
        {
            if (property.propertyType == SerializedPropertyType.Float)
                return EditorGUIUtility.singleLineHeight;
            return EditorGUIUtility.singleLineHeight * (EditorGUIUtility.wideMode ? 1f : 2f);
        }

        public static void PropertyField(Rect position, SerializedProperty property, GUIContent label, ChiselValueKind kind)
        {
            switch (property.propertyType)
            {
                case SerializedPropertyType.Float:
                {
                    EditorGUI.showMixedValue = property.hasMultipleDifferentValues;
                    var newValue = FloatField(position, label, property.floatValue, kind);
                    EditorGUI.showMixedValue = false;
                    if (newValue.HasValue)
                        property.floatValue = newValue.Value;
                    return;
                }
                case SerializedPropertyType.Vector2:
                {
                    var newValue = VectorField(position, label, property.vector2Value, 2, kind, property.hasMultipleDifferentValues);
                    if (newValue.HasValue)
                        property.vector2Value = newValue.Value;
                    return;
                }
                case SerializedPropertyType.Vector3:
                {
                    var newValue = VectorField(position, label, property.vector3Value, 3, kind, property.hasMultipleDifferentValues);
                    if (newValue.HasValue)
                        property.vector3Value = newValue.Value;
                    return;
                }
            }
            EditorGUI.PropertyField(position, property, label);
        }

        // The new value when it was changed
        static float? FloatField(Rect position, GUIContent label, float value, ChiselValueKind kind)
        {
            var shown = ToShown(kind, value);
            EditorGUI.BeginChangeCheck();
            var newShown = EditorGUI.FloatField(position, label, shown);
            var rect = position;
            if (ChiselGUIUtility.LabelHasContent(label))
                rect.xMin += EditorGUIUtility.labelWidth;
            DrawSuffix(rect, kind);
            if (!EditorGUI.EndChangeCheck() || newShown == shown)
                return null;
            return FromShown(kind, newShown);
        }

        // The new value when it was changed; a vector of count components, each in its own field with the unit after it
        public static Vector3? VectorField(Rect position, GUIContent label, Vector3 value, int count, ChiselValueKind kind, bool mixed)
        {
            var shown = ToShown(kind, value);
            var newShown = shown;

            EditorGUI.BeginChangeCheck();
            var fieldsRect = EditorGUI.PrefixLabel(position, GUIUtility.GetControlID(FocusType.Passive), label);
            if (!EditorGUIUtility.wideMode)
            {
                fieldsRect = position;
                fieldsRect.y += EditorGUIUtility.singleLineHeight;
                fieldsRect.height = EditorGUIUtility.singleLineHeight;
                fieldsRect = EditorGUI.IndentedRect(fieldsRect);
            }

            var prevIndent      = EditorGUI.indentLevel;
            var prevLabelWidth  = EditorGUIUtility.labelWidth;
            var prevMixed       = EditorGUI.showMixedValue;
            EditorGUI.indentLevel = 0;
            EditorGUI.showMixedValue = mixed;
            try
            {
                const float kSpacing = 2;
                var width = (fieldsRect.width - (kSpacing * (count - 1))) / count;
                for (int i = 0; i < count; i++)
                {
                    var componentRect = new Rect(fieldsRect.x + (i * (width + kSpacing)), fieldsRect.y, width, fieldsRect.height);
                    EditorGUIUtility.labelWidth = 13;
                    newShown[i] = EditorGUI.FloatField(componentRect, kComponentLabels[i], shown[i]);
                    componentRect.xMin += EditorGUIUtility.labelWidth;
                    DrawSuffix(componentRect, kind);
                }
            }
            finally
            {
                EditorGUI.indentLevel = prevIndent;
                EditorGUIUtility.labelWidth = prevLabelWidth;
                EditorGUI.showMixedValue = prevMixed;
            }
            if (!EditorGUI.EndChangeCheck() || newShown == shown)
                return null;
            return Edited(kind, value, shown, newShown);
        }

        // The unit, dimmed at the right end of a number field
        static void DrawSuffix(Rect fieldRect, ChiselValueKind kind)
        {
            if (Event.current.type != EventType.Repaint)
                return;
            s_SuffixStyle ??= new GUIStyle(EditorStyles.miniLabel) { alignment = TextAnchor.MiddleRight, normal = { textColor = new Color(0.5f, 0.5f, 0.5f) } };
            kSuffixContent.text = Suffix(kind);
            var rect = fieldRect;
            rect.xMax -= kSuffixPadding;
            s_SuffixStyle.Draw(rect, kSuffixContent, false, false, false, false);
        }
    }
}
