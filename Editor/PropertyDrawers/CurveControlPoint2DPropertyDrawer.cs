using Chisel.Components;
using Chisel.Core;
using UnityEditor;
using UnityEngine;

namespace Chisel.Editors
{
    // A point of a 2D shape as its fields are, but whether the side from it is smooth only where the shape has sides
    // that can be: the extruded and the revolved shape
    [CustomPropertyDrawer(typeof(CurveControlPoint2D))]
    public sealed class CurveControlPoint2DPropertyDrawer : PropertyDrawer
    {
        static readonly GUIContent kSmoothSideContent = new("Smooth Side", "Whether the side from this point to the next shades smooth, as one surface with the smooth sides next to it");

        internal static bool ShowsSmoothSide(SerializedObject serializedObject)
        {
            foreach (var target in serializedObject.targetObjects)
            {
                if (target is not ChiselExtrudedShapeComponent && target is not ChiselRevolvedShapeComponent)
                    return false;
            }
            return true;
        }

        static bool Shown(SerializedProperty child, bool showsSmoothSide)
        {
            return showsSmoothSide || child.name != nameof(CurveControlPoint2D.smoothSide);
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            var height = EditorGUIUtility.singleLineHeight;
            if (!property.isExpanded)
                return height;
            var showsSmoothSide = ShowsSmoothSide(property.serializedObject);
            var child = property.Copy();
            var end   = property.GetEndProperty();
            for (var more = child.NextVisible(true); more && !SerializedProperty.EqualContents(child, end); more = child.NextVisible(false))
            {
                if (Shown(child, showsSmoothSide))
                    height += EditorGUIUtility.standardVerticalSpacing + EditorGUI.GetPropertyHeight(child, true);
            }
            return height;
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);
            var line = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);
            property.isExpanded = EditorGUI.Foldout(line, property.isExpanded, label, true);
            if (property.isExpanded)
            {
                var showsSmoothSide = ShowsSmoothSide(property.serializedObject);
                EditorGUI.indentLevel++;
                var child = property.Copy();
                var end   = property.GetEndProperty();
                for (var more = child.NextVisible(true); more && !SerializedProperty.EqualContents(child, end); more = child.NextVisible(false))
                {
                    if (!Shown(child, showsSmoothSide))
                        continue;
                    line.y     += line.height + EditorGUIUtility.standardVerticalSpacing;
                    line.height = EditorGUI.GetPropertyHeight(child, true);
                    if (child.name == nameof(CurveControlPoint2D.smoothSide))
                        EditorGUI.PropertyField(line, child, kSmoothSideContent, true);
                    else
                        EditorGUI.PropertyField(line, child, true);
                }
                EditorGUI.indentLevel--;
            }
            EditorGUI.EndProperty();
        }
    }
}
