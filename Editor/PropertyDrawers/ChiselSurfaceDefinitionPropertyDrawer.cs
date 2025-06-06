﻿using Chisel.Core;
using UnityEngine;
using UnityEditor;

namespace Chisel.Editors
{
    
    [CustomPropertyDrawer(typeof(ChiselSurfaceArray))]
    public sealed class ChiselSurfaceDefinitionPropertyDrawer : PropertyDrawer
    {
		// TODO: make these shared resources since this name is used in several places (with identical context)
		private readonly static GUIContent  kSurfacesContent        = new("Surfaces");
		private const string                kSurfacePropertyName    = "Surface {0}";
        private static GUIContent           s_SurfacePropertyContent = new();


        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            if (ChiselNodeEditorBase.InSceneSettingsContext ||
                property.serializedObject.isEditingMultipleObjects || 
                property.hasMultipleDifferentValues)
                return 0;
            return base.GetPropertyHeight(property, label);
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            if (ChiselNodeEditorBase.InSceneSettingsContext)
            {
                EditorGUI.BeginProperty(position, label, property);
                EditorGUI.EndProperty();
                return;
            }

            var surfacesProp = property.FindPropertyRelative(nameof(ChiselSurfaceArray.surfaces));

            EditorGUI.BeginProperty(position, label, surfacesProp);
            if (!surfacesProp.serializedObject.isEditingMultipleObjects && !property.hasMultipleDifferentValues && !ChiselNodeEditorBase.InSceneSettingsContext)
            {
                
                EditorGUI.BeginChangeCheck();
                var path = surfacesProp.propertyPath;
                var surfacesVisible = SessionState.GetBool(path, false);
                surfacesVisible = EditorGUILayout.BeginFoldoutHeaderGroup(surfacesVisible, kSurfacesContent);
                try
                {
                    if (EditorGUI.EndChangeCheck())
                        SessionState.SetBool(path, surfacesVisible);
                    if (surfacesVisible)
                    {
                        EditorGUI.BeginChangeCheck();
                        EditorGUI.indentLevel++;
                        try
                        {
                            SerializedProperty elementProperty;
                            for (int i = 0; i < surfacesProp.arraySize; i++)
                            {
                                s_SurfacePropertyContent.text = string.Format(kSurfacePropertyName, (i + 1));
                                elementProperty = surfacesProp.GetArrayElementAtIndex(i);
                                EditorGUILayout.PropertyField(elementProperty, s_SurfacePropertyContent, true);
                            }
                        }
                        finally
                        {
                            EditorGUI.indentLevel--;
                            if (EditorGUI.EndChangeCheck())
                            {
                                property.serializedObject.ApplyModifiedProperties();
                            }
                        }
                    }
                }
                finally { EditorGUILayout.EndFoldoutHeaderGroup(); }
            }
            EditorGUI.EndProperty();
        }
    }
}
