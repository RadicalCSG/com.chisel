using System;
using Chisel.Core;
using UnityEngine;
using UnityEditor;

namespace Chisel.Editors
{
    [CustomPropertyDrawer(typeof(UVMatrix))]
    public sealed class UVMatrixPropertyDrawer : PropertyDrawer
    {
        const float kSpacing = 2;

        public static float DefaultHeight
        {
            get
            {
                return (2 * (kSpacing + EditorGUI.GetPropertyHeight(SerializedPropertyType.Vector2, GUIContent.none))) + EditorGUI.GetPropertyHeight(SerializedPropertyType.Float, GUIContent.none);
            }
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            return DefaultHeight;
        }

        /*
        readonly static GUIContent[] xyzw = new []
        {
            new("X"),
            new("Y"),
            new("Z"),
            new("W"),
        };
        */
        readonly static GUIContent kTranslationContent  = new("Translation");
        readonly static GUIContent kScaleContent        = new("Scale");
        readonly static GUIContent kRotationContent     = new("Rotation");

        public enum UVPart { Translation, Rotation, Scale, All }

        readonly static GUIContent kResetTranslationContent = new("Reset Translation");
        readonly static GUIContent kResetRotationContent    = new("Reset Rotation");
        readonly static GUIContent kResetScaleContent       = new("Reset Scale");
        readonly static GUIContent kResetAllContent         = new("Reset All");

        // The matrix with one of its parts (or all) back at its default, the others as they were
        public static UVMatrix Reset(UVMatrix uvMatrix, UVPart part)
        {
            uvMatrix.Decompose(out var translation, out var rotation, out var scale);
            if (part == UVPart.Translation || part == UVPart.All) translation = Vector2.zero;
            if (part == UVPart.Rotation    || part == UVPart.All) rotation    = 0;
            if (part == UVPart.Scale       || part == UVPart.All) scale       = Vector2.one;
            return UVMatrix.TRS(translation, uvMatrix.PlaneNormal, rotation, scale);
        }

        static void ShowResetMenu(Rect rowRect, SerializedProperty property, UVPart part, GUIContent partContent)
        {
            var evt = Event.current;
            if (evt.type != EventType.ContextClick || !rowRect.Contains(evt.mousePosition))
                return;

            var menu = new GenericMenu();
            var copy = property.Copy();
            menu.AddItem(partContent, false, () => ApplyReset(copy, part));
            menu.AddItem(kResetAllContent, false, () => ApplyReset(copy, UVPart.All));
            menu.ShowAsContext();
            evt.Use();
        }

        static void ApplyReset(SerializedProperty property, UVPart part)
        {
            property.serializedObject.Update();
            var UProp = property.FindPropertyRelative(nameof(UVMatrix.U));
            var VProp = property.FindPropertyRelative(nameof(UVMatrix.V));
            var reset = Reset(new UVMatrix(UProp.vector4Value, VProp.vector4Value), part);
            UProp.vector4Value = reset.U;
            VProp.vector4Value = reset.V;
            property.serializedObject.ApplyModifiedProperties();
        }

        class UVMatrixState
        {
            public bool     initialized = false;
            public Vector3  normal;
            public Vector2  translation;
            public float    rotation;
            public Vector2  scale;
            public UVMatrix uvMatrix;
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            SerializedProperty UProp    = property.FindPropertyRelative(nameof(UVMatrix.U));
            SerializedProperty VProp    = property.FindPropertyRelative(nameof(UVMatrix.V));

            int translationID   = GUIUtility.GetControlID(FocusType.Keyboard);
            int scaleID         = GUIUtility.GetControlID(FocusType.Keyboard);
            int rotationID      = GUIUtility.GetControlID(FocusType.Keyboard);

            var uvMatrix    = new UVMatrix(UProp.vector4Value, VProp.vector4Value);
            var state       = (UVMatrixState)EditorGUIUtility.GetStateObject(typeof(UVMatrixState), translationID);
            if (!state.initialized || state.uvMatrix != uvMatrix)
            {
                state.uvMatrix = uvMatrix;
                uvMatrix.Decompose(out state.translation, out state.rotation, out state.scale);
                state.normal = uvMatrix.PlaneNormal;
                state.initialized = true;
            }

            var hasLabel = ChiselGUIUtility.LabelHasContent(label);

            var prevIndenLevel = EditorGUI.indentLevel;
            EditorGUI.BeginProperty(position, label, property);
            {
                EditorGUI.BeginChangeCheck();

                var prevMixedValues = EditorGUI.showMixedValue;
                EditorGUI.showMixedValue = property.hasMultipleDifferentValues;

                var translationContent  = (label == null) ? GUIContent.none : kTranslationContent;
                var scaleContent        = (label == null) ? GUIContent.none : kScaleContent;
                var rotationContent     = (label == null) ? GUIContent.none : kRotationContent;


                position.height = EditorGUI.GetPropertyHeight(SerializedPropertyType.Vector2, GUIContent.none);
                ShowResetMenu(position, property, UVPart.Translation, kResetTranslationContent);
                var fieldRect = EditorGUI.PrefixLabel(position, translationID, !hasLabel ? GUIContent.none : translationContent);
                EditorGUI.indentLevel = 0;
                state.translation = EditorGUI.Vector2Field(fieldRect,  GUIContent.none,     state.translation);
                EditorGUI.indentLevel = prevIndenLevel;
                position.y += position.height + kSpacing;

                position.height = EditorGUI.GetPropertyHeight(SerializedPropertyType.Vector2, GUIContent.none);
                ShowResetMenu(position, property, UVPart.Scale, kResetScaleContent);
                fieldRect = EditorGUI.PrefixLabel(position, scaleID, !hasLabel ? GUIContent.none : scaleContent);
                EditorGUI.indentLevel = 0;
                state.scale = EditorGUI.Vector2Field(fieldRect,        GUIContent.none,     state.scale);

                EditorGUI.indentLevel = prevIndenLevel;
                position.y += position.height + kSpacing;

                position.height = EditorGUI.GetPropertyHeight(SerializedPropertyType.Float, GUIContent.none);
                ShowResetMenu(position, property, UVPart.Rotation, kResetRotationContent);
                fieldRect = EditorGUI.PrefixLabel(position, rotationID, !hasLabel ? GUIContent.none : rotationContent);
                EditorGUI.indentLevel = 0;
                state.rotation = HotspotTransformGUI.RotationField(fieldRect, state.rotation);
                EditorGUI.indentLevel = prevIndenLevel;
                position.y += position.height + kSpacing;

                EditorGUI.showMixedValue = prevMixedValues;

                if (EditorGUI.EndChangeCheck())
                {
                    state.scale.x = (float)Math.Max(Math.Abs(state.scale.x), UVMatrix.kMinScale) * Math.Sign(state.scale.x);
                    state.scale.y = (float)Math.Max(Math.Abs(state.scale.y), UVMatrix.kMinScale) * Math.Sign(state.scale.y);

                    uvMatrix = UVMatrix.TRS(state.translation, state.normal, state.rotation, state.scale);
                    UProp.vector4Value = uvMatrix.U;
                    VProp.vector4Value = uvMatrix.V;
                    state.uvMatrix = uvMatrix;
                    property.serializedObject.ApplyModifiedProperties();
                }

            }
            EditorGUI.EndProperty();
            /*
            
            SerializedProperty UxProp   = property.FindPropertyRelative($"{nameof(UVMatrix.U)}.{nameof(UVMatrix.U.x)}");
            SerializedProperty VxProp   = property.FindPropertyRelative($"{nameof(UVMatrix.V)}.{nameof(UVMatrix.V.x)}");

            EditorGUI.BeginProperty(position, label, property);
            {
                position.height = EditorGUI.GetPropertyHeight(SerializedPropertyType.Vector4, GUIContent.none);
                position = EditorGUI.PrefixLabel(position, GUIUtility.GetControlID(FocusType.Keyboard), label);
                
                EditorGUI.MultiPropertyField(position, xyzw, UxProp);
                position.y += position.height + kSpace;
                    
                EditorGUI.MultiPropertyField(position, xyzw, VxProp);
            }
            EditorGUI.EndProperty();
            */
        }
    }
}
