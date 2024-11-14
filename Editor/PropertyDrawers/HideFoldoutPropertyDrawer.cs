using Chisel.Core;
using UnityEngine;
using UnityEditor;

namespace Chisel.Editors
{
    [CustomPropertyDrawer(typeof(HideFoldoutAttribute))]
    public sealed class HideFoldoutPropertyDrawer : PropertyDrawer
	{
#if !UNITY_2023_1_OR_NEWER
        public override bool CanCacheInspectorGUI(SerializedProperty property) { return true; }
#endif

		static GUIContent s_TempGUIContent = new();

		public override float GetPropertyHeight(SerializedProperty iterator, GUIContent label)
        {
            float totalHeight = 0;
            int startingDepth = iterator.depth;
            EditorGUIUtility.wideMode = true;
            if (iterator.NextVisible(true))
            {
                do
                {
                    s_TempGUIContent.text = iterator.displayName;
                    totalHeight += EditorGUI.GetPropertyHeight(iterator, s_TempGUIContent, includeChildren: false) + EditorGUIUtility.standardVerticalSpacing;
                }
                while (iterator.NextVisible(iterator.isExpanded) && iterator.depth > startingDepth);
            }
            totalHeight += EditorGUIUtility.standardVerticalSpacing;
            return totalHeight;
        }

        public override void OnGUI(Rect position, SerializedProperty iterator, GUIContent label)
        {
            var indentLevel = EditorGUI.indentLevel;
            try
            {
                EditorGUIUtility.wideMode = true;
                EditorGUI.BeginChangeCheck();
                float y = position.y + EditorGUIUtility.standardVerticalSpacing;
                if (iterator.NextVisible(true))
                {
                    int startingDepth = iterator.depth;
                    do
                    {
                        EditorGUI.indentLevel = iterator.depth - startingDepth;
                        s_TempGUIContent.text = iterator.displayName;
                        var height = EditorGUI.GetPropertyHeight(iterator, s_TempGUIContent, includeChildren: false);
                        EditorGUI.PropertyField(new Rect(position.x, y, position.width, height), iterator, s_TempGUIContent);
                        y += height + EditorGUIUtility.standardVerticalSpacing;
                    }
                    while (iterator.NextVisible(iterator.isExpanded) && iterator.depth >= startingDepth);
                }
                if (EditorGUI.EndChangeCheck())
                    iterator.serializedObject.ApplyModifiedProperties();
            }
            finally
            {
                EditorGUI.indentLevel = indentLevel;
            }
        }
    }
}
