﻿using System;
using UnityEngine;
using UnityEditor;
using Chisel.Core;

namespace Chisel.Editors
{
    // TODO: show unit
    [CustomPropertyDrawer(typeof(DistanceValueAttribute))]
    public class DistanceValuesDrawer : PropertyDrawer
    {
        // TODO: use UnityTypes
        UnitType Type { get { return ((DistanceValueAttribute)attribute).Type; } }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            if (property.propertyType == SerializedPropertyType.Float)
                return EditorGUIUtility.singleLineHeight;
            return EditorGUIUtility.singleLineHeight * (EditorGUIUtility.wideMode ? 1f : 2f);
        }

        void ResetValues(object userdata)
        {
            var property = (SerializedProperty)userdata;
            if (property.propertyType == SerializedPropertyType.Float) property.floatValue = 0;
            if (property.propertyType == SerializedPropertyType.Vector2) property.vector2Value = Vector2.zero;
            if (property.propertyType == SerializedPropertyType.Vector3) property.vector3Value = Vector3.zero;
            property.serializedObject.ApplyModifiedProperties();
        }

        static readonly GUIContent kResetValuesContent = new("Reset Values");
        
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);
            try
            {
                Event e = Event.current;
     
                if (e.type == EventType.MouseDown && e.button == 1 && position.Contains(e.mousePosition)) {
         
                    var context = new GenericMenu ();
         
                    context.AddItem (kResetValuesContent, false, ResetValues, property);
         
                    context.ShowAsContext ();
                }
                
                EditorGUI.PropertyField(position, property, label);
            }
            catch (ExitGUIException) { }
            catch (Exception ex) { Debug.LogException(ex); }
            EditorGUI.EndProperty();
        }
    }
}
