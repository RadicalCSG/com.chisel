using System;
using Chisel.Components;
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
        public readonly static GUIContent	kPaintSpacingContents   = new("Paint Spacing", "How far apart the points this surface is painted at are, in metres: the surface is cut at them. Changing it resamples the paint it has.");

        // Whether the material's shader takes paint (has a paint profile)
        public static bool TakesPaint(Material material)
        {
            return material && ChiselPaintProfiles.Find(material.shader) != null;
        }

        // A paint spacing field; 0 (not set) shows the default
        public static void PaintSpacingField(Rect position, SerializedProperty spacingProp, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, spacingProp);
            EditorGUI.showMixedValue = spacingProp.hasMultipleDifferentValues;
            EditorGUI.BeginChangeCheck();
            var spacing = EditorGUI.FloatField(position, label, spacingProp.floatValue > 0 ? spacingProp.floatValue : ChiselSurface.kDefaultPaintSpacing);
            if (EditorGUI.EndChangeCheck())
                spacingProp.floatValue = Mathf.Max(0.01f, spacing);
            EditorGUI.showMixedValue = false;
            EditorGUI.EndProperty();
        }

        // Resamples the paint of the generators these details are on, after their paint spacing changed
        public static void Respace(System.Collections.Generic.List<SurfaceReference> surfaces, string undoName)
        {
            foreach (var surface in surfaces)
            {
                if (surface.node is not ChiselGeneratorComponent generator || generator.SurfacePaint.Count == 0)
                    continue;
                Undo.RecordObject(generator, undoName);
                if (ChiselPaintBrush.Respace(generator))
                    EditorUtility.SetDirty(generator);
            }
        }

        // The material beside these details (the first selected surface's)
        static Material MaterialOf(SerializedProperty property)
        {
            var path = property.propertyPath;
            var dot = path.LastIndexOf('.');
            var parent = dot < 0 ? string.Empty : path.Substring(0, dot + 1);
            var materialProp = property.serializedObject.FindProperty(parent + ChiselSurface.kChiselMaterialName + "." + ChiselMaterial.kMaterialFieldName);
            return materialProp != null ? materialProp.objectReferenceValue as Material : null;
        }

        // The hotspot layout of the material beside these details (the first selected surface's), null for none
        static ChiselHotspotLayout LayoutOf(SerializedProperty property)
        {
            var path = property.propertyPath;
            var dot = path.LastIndexOf('.');
            var parent = dot < 0 ? string.Empty : path.Substring(0, dot + 1);
            var materialProp = property.serializedObject.FindProperty(parent + ChiselSurface.kChiselMaterialName + "." + ChiselMaterial.kMaterialFieldName);
            return materialProp != null ? HotspotTransformGUI.LayoutOf(materialProp.objectReferenceValue as Material) : null;
        }

        // The paint spacing beside these details (on their surface)
        static SerializedProperty PaintSpacingOf(SerializedProperty property)
        {
            var path = property.propertyPath;
            var dot = path.LastIndexOf('.');
            var parent = dot < 0 ? string.Empty : path.Substring(0, dot + 1);
            return property.serializedObject.FindProperty(parent + ChiselSurface.kPaintSpacingName);
        }

        static readonly System.Text.RegularExpressions.Regex kDescriptionIndex = new(@"data\[(\d+)\]\.[^.\[]+$");
        static readonly System.Collections.Generic.List<SurfaceReference> s_Surfaces = new();

        // The surfaces connected to the edited one take its hotspot settings (ChiselHotspotGroupEdit)
        static void SpreadToConnected(SerializedProperty property)
        {
            ChiselHotspotGroupEdit.Spread(SurfacesOf(property), "Modified surface");
        }

        // The surfaces these details are, in every selected generator
        static System.Collections.Generic.List<SurfaceReference> SurfacesOf(SerializedProperty property)
        {
            var edited = new System.Collections.Generic.List<SurfaceReference>();
            var match = kDescriptionIndex.Match(property.propertyPath);
            if (!match.Success)
                return edited;
            var descriptionIndex = int.Parse(match.Groups[1].Value);
            foreach (var target in property.serializedObject.targetObjects)
            {
                s_Surfaces.Clear();
                if (target is Chisel.Components.ChiselGeneratorComponent generator &&
                    ChiselSurfaceSelectionManager.GetAllSurfaceReferences(generator, s_Surfaces))
                {
                    foreach (var surface in s_Surfaces)
                    {
                        if (surface.descriptionIndex == descriptionIndex)
                            edited.Add(surface);
                    }
                }
            }
            return edited;
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
                    SmoothingGroupPropertyDrawer.GetDefaultHeight(smoothingGroupProp.propertyPath, true) +
                    (TakesPaint(MaterialOf(property)) && PaintSpacingOf(property) != null ? kSpacing + EditorGUIUtility.singleLineHeight : 0);
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
                var surfaces = SurfacesOf(property);
                var wasWorld = ChiselTextureLockEdit.WorldSpaceOf(surfaces);
                EditorGUI.BeginChangeCheck();
                {
                    position.height = SurfaceFlagsPropertyDrawer.DefaultHeight;
                    EditorGUI.PropertyField(position, detailFlagsProp, !hasLabel ? GUIContent.none : kSurfaceFlagsContents, false);
                    position.y += position.height + kSpacing;

                    var layout = LayoutOf(property);
                    if (layout != null)
                    {
                        position.height = HotspotTransformGUI.Height;
                        var material = MaterialOf(property);
                        HotspotTransformGUI.OnGUI(position, property.FindPropertyRelative(nameof(SurfaceDetails.hotspot)), layout, hasLabel,
                                                  ChiselHotspotGroupEdit.MostVariations(surfaces),
                                                  () => HotspotTransformGUI.PagesOf(material, layout),
                                                  () => surfaces.Count > 0 ? ChiselHotspotGroupEdit.FitsOf(surfaces[0], layout, material) : null,
                                                  surfaces.Count > 0 ? surfaces[0].descriptionIndex : 0);
                    } else
                    {
                        position.height = UVMatrixPropertyDrawer.DefaultHeight;
                        EditorGUI.PropertyField(position, uv0Prop, !hasLabel ? GUIContent.none : kUV0Contents, false);
                    }
                    position.y += position.height + kSpacing;

                    position.height = SmoothingGroupPropertyDrawer.GetDefaultHeight(smoothingGroupProp.propertyPath, true);
                    EditorGUI.PropertyField(position, smoothingGroupProp, !hasLabel ? GUIContent.none : kSmoothingGroupContents, false);

                    var paintSpacingProp = PaintSpacingOf(property);
                    if (paintSpacingProp != null && TakesPaint(MaterialOf(property)))
                    {
                        position.y += position.height + kSpacing;
                        position.height = EditorGUIUtility.singleLineHeight;
                        PaintSpacingField(position, paintSpacingProp, !hasLabel ? GUIContent.none : kPaintSpacingContents);
                    }
                }
                if (EditorGUI.EndChangeCheck())
                {
                    property.serializedObject.ApplyModifiedProperties();
                    // Lock texture to object toggled: the texture stays where it is
                    if (ChiselTextureLockEdit.KeepLook(surfaces, wasWorld, "Modified surface"))
                        property.serializedObject.Update();
                    SpreadToConnected(property);
                    Respace(surfaces, "Modified surface");
                }
            }
            catch (ExitGUIException) { }
            catch (Exception ex) { Debug.LogException(ex); }
            
            EditorGUI.showMixedValue = prevShowMixedValue;
            EditorGUI.EndProperty();
        }
    }
}
