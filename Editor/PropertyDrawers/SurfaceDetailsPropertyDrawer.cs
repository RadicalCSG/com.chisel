using System;
using Chisel.Core;
using UnityEngine;
using UnityEditor;

namespace Chisel.Editors
{
    [CustomPropertyDrawer(typeof(SurfaceDetails))]
    public sealed class SurfaceDetailsPropertyDrawer : PropertyDrawer
    {
        const float kSpacing = 2;
        public readonly static GUIContent	kUV0Contents            = new("UV");
        public readonly static GUIContent	kSurfaceFlagsContents   = new("Surface Flags");
        public readonly static GUIContent	kSmoothingGroupContents = new("Smoothing Groups");

        // The hotspot layout of the material beside these details (the first selected surface's), null for none
        static ChiselHotspotLayout LayoutOf(SerializedProperty property)
        {
            var path = property.propertyPath;
            var dot = path.LastIndexOf('.');
            var parent = dot < 0 ? string.Empty : path.Substring(0, dot + 1);
            var materialProp = property.serializedObject.FindProperty(parent + ChiselSurface.kChiselMaterialName + "." + ChiselMaterial.kMaterialFieldName);
            return materialProp != null ? HotspotTransformGUI.LayoutOf(materialProp.objectReferenceValue as Material) : null;
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            if (ChiselNodeEditorBase.InSceneSettingsContext)
                return 0;

            var smoothingGroupProp   = property.FindPropertyRelative(nameof(SurfaceDetails.smoothingGroup));
            return  (LayoutOf(property) != null ? HotspotTransformGUI.Height : UVMatrixPropertyDrawer.DefaultHeight) +
                    kSpacing + 
                    SurfaceFlagsPropertyDrawer.DefaultHeight +
                    kSpacing + 
                    SmoothingGroupPropertyDrawer.GetDefaultHeight(smoothingGroupProp.propertyPath, true);
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            if (ChiselNodeEditorBase.InSceneSettingsContext)
            {
                EditorGUI.BeginProperty(position, label, property);
                EditorGUI.EndProperty();
                return;
            }
            var hasLabel = ChiselGUIUtility.LabelHasContent(label);

            var uv0Prop            = property.FindPropertyRelative(nameof(SurfaceDetails.UV0));
            var detailFlagsProp    = property.FindPropertyRelative(nameof(SurfaceDetails.detailFlags));
            var smoothingGroupProp = property.FindPropertyRelative(nameof(SurfaceDetails.smoothingGroup));

            EditorGUI.BeginProperty(position, label, property);
            bool prevShowMixedValue = EditorGUI.showMixedValue;
            try
            {
                property.serializedObject.Update();
                EditorGUI.BeginChangeCheck();
                {
                    position.height = SurfaceFlagsPropertyDrawer.DefaultHeight;
                    EditorGUI.PropertyField(position, detailFlagsProp, !hasLabel ? GUIContent.none : kSurfaceFlagsContents, false);
                    position.y += position.height + kSpacing;

                    var layout = LayoutOf(property);
                    if (layout != null)
                    {
                        position.height = HotspotTransformGUI.Height;
                        HotspotTransformGUI.OnGUI(position, property.FindPropertyRelative(nameof(SurfaceDetails.hotspot)), layout, hasLabel);
                    } else
                    {
                        position.height = UVMatrixPropertyDrawer.DefaultHeight;
                        EditorGUI.PropertyField(position, uv0Prop, !hasLabel ? GUIContent.none : kUV0Contents, false);
                    }
                    position.y += position.height + kSpacing;

                    position.height = SmoothingGroupPropertyDrawer.GetDefaultHeight(smoothingGroupProp.propertyPath, true);
                    EditorGUI.PropertyField(position, smoothingGroupProp, !hasLabel ? GUIContent.none : kSmoothingGroupContents, false);
                }
                if (EditorGUI.EndChangeCheck())
                {
                    property.serializedObject.ApplyModifiedProperties();
                }
            }
            catch (ExitGUIException) { }
            catch (Exception ex) { Debug.LogException(ex); }
            
            EditorGUI.showMixedValue = prevShowMixedValue;
            EditorGUI.EndProperty();
        }
    }
}
