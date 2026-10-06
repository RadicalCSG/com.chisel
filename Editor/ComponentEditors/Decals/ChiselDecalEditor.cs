using Chisel.Components;
using Chisel.Core;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;

namespace Chisel.Editors
{
    [CustomEditor(typeof(ChiselDecalComponent)), CanEditMultipleObjects]
    public sealed class ChiselDecalEditor : Editor
    {
        const string kCreateMenuPath = "GameObject/Chisel/Create Decal";

        static readonly Color kHandleColor = new Color(1.0f, 0.6f, 0.1f, 1.0f);
        static readonly BoxBoundsHandle s_BoxHandle = new BoxBoundsHandle();

        static readonly GUIContent kMaterialContent      = new GUIContent("Material", "What the decal draws with. A decal without a material draws nothing.");
        static readonly GUIContent kCenterContent        = new GUIContent("Center", "The center of the box, in the decal's own space.");
        static readonly GUIContent kSizeContent          = new GUIContent("Size", "X and Y are the image's width and height (for a perspective decal: at the center of the box), Z is how deep the decal reaches.");
        static readonly GUIContent kProjectionContent    = new GUIContent("Projection", "Orthographic keeps the image's size at every depth; perspective projects it from a point behind the box, like a projector.");
        static readonly GUIContent kFieldOfViewContent   = new GUIContent("Field Of View", "The perspective projection's vertical angle, in degrees.");
        static readonly GUIContent kTransparentContent   = new GUIContent("Transparent", "A transparent decal is drawn over the surfaces it covers. An opaque decal replaces them in the rendered mesh; the collider keeps them.");
        static readonly GUIContent kOrderContent         = new GUIContent("Order", "Decals with a higher order are drawn on top of those with a lower order.");
        static readonly GUIContent kMaxAngleContent      = new GUIContent("Max Angle", "Surfaces turned further than this from the direction the image comes from don't take the decal. 180 takes every surface that doesn't face away from the box's center.");
        static readonly GUIContent kSurfaceOffsetContent = new GUIContent("Surface Offset", "How far a transparent decal is lifted off the surface, so it doesn't fight it for depth.");
        static readonly GUIContent kUVScaleContent       = new GUIContent("UV Scale", "The texture coordinates are UV Offset + UV Scale * p, where p runs from (0,0) at the image's lower left to (1,1) at its upper right.");
        static readonly GUIContent kUVOffsetContent      = new GUIContent("UV Offset");
        static readonly GUIContent kTargetsContent       = new GUIContent("Targets", "Limits the decal to these brush surfaces (surface index -1: all of the brush's surfaces). Empty: every surface it reaches.");
        const string kNoMaterialMessage = "A decal without a material draws nothing.";

        [MenuItem(kCreateMenuPath, false, -2)]
        static void CreateDecal(MenuCommand menuCommand)
        {
            var parent = menuCommand.context as GameObject;
            var gameObject = new GameObject(ChiselDecalComponent.kNodeTypeName);
            GameObjectUtility.SetParentAndAlign(gameObject, parent);
            if (parent == null && SceneView.lastActiveSceneView != null)
                gameObject.transform.position = SceneView.lastActiveSceneView.pivot;
            Undo.RegisterCreatedObjectUndo(gameObject, "Create " + gameObject.name);
            Undo.AddComponent<ChiselDecalComponent>(gameObject);
            Selection.activeObject = gameObject;
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            var material = serializedObject.FindProperty(ChiselDecalComponent.kMaterialName);
            EditorGUILayout.PropertyField(material, kMaterialContent);
            if (material.objectReferenceValue == null && !material.hasMultipleDifferentValues)
                EditorGUILayout.HelpBox(kNoMaterialMessage, MessageType.Info);

            var settings = serializedObject.FindProperty(ChiselDecalComponent.kSettingsName);
            EditorGUILayout.PropertyField(settings.FindPropertyRelative(nameof(ChiselDecalSettings.center)), kCenterContent);
            EditorGUILayout.PropertyField(settings.FindPropertyRelative(nameof(ChiselDecalSettings.size)), kSizeContent);

            var projection = settings.FindPropertyRelative(nameof(ChiselDecalSettings.projection));
            EditorGUILayout.PropertyField(projection, kProjectionContent);
            if (projection.hasMultipleDifferentValues || projection.enumValueIndex == (int)ChiselDecalProjection.Perspective)
            {
                EditorGUILayout.Slider(settings.FindPropertyRelative(nameof(ChiselDecalSettings.fieldOfView)),
                                       ChiselDecalSettings.kMinFieldOfView, ChiselDecalSettings.kMaxFieldOfView, kFieldOfViewContent);
            }

            var transparent = settings.FindPropertyRelative(nameof(ChiselDecalSettings.transparent));
            EditorGUILayout.PropertyField(transparent, kTransparentContent);
            EditorGUILayout.PropertyField(settings.FindPropertyRelative(nameof(ChiselDecalSettings.order)), kOrderContent);
            EditorGUILayout.Slider(settings.FindPropertyRelative(nameof(ChiselDecalSettings.maxAngle)), 0, 180, kMaxAngleContent);
            if (transparent.hasMultipleDifferentValues || transparent.boolValue)
                EditorGUILayout.PropertyField(settings.FindPropertyRelative(nameof(ChiselDecalSettings.surfaceOffset)), kSurfaceOffsetContent);

            EditorGUILayout.PropertyField(settings.FindPropertyRelative(nameof(ChiselDecalSettings.uvScale)), kUVScaleContent);
            EditorGUILayout.PropertyField(settings.FindPropertyRelative(nameof(ChiselDecalSettings.uvOffset)), kUVOffsetContent);

            EditorGUILayout.PropertyField(serializedObject.FindProperty(ChiselDecalComponent.kTargetsName), kTargetsContent, true);

            serializedObject.ApplyModifiedProperties();
        }

        void OnSceneGUI()
        {
            var decal = target as ChiselDecalComponent;
            if (decal == null)
                return;

            var settings = decal.Settings;
            using (new Handles.DrawingScope(kHandleColor, decal.transform.localToWorldMatrix))
            {
                s_BoxHandle.center = settings.center;
                s_BoxHandle.size   = settings.size;
                EditorGUI.BeginChangeCheck();
                s_BoxHandle.DrawHandle();
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(decal, "Resize Decal");
                    settings.center = s_BoxHandle.center;
                    settings.size   = s_BoxHandle.size;
                    decal.Settings  = settings;
                }
            }
        }
    }
}
