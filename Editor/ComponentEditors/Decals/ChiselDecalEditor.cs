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
        static readonly GUIContent kShapeContent         = new GUIContent("Shape", "Box: the image is projected through the box. Trim: the image lies on the edge between two surfaces, its lower half on the first, its upper half on the second.");
        static readonly GUIContent kTrimRepeatContent    = new GUIContent("Along The Edge", "Stretch: one image over the whole edge. Repeat: the image repeated at its length, the last one cut short. Fit: the whole number of images nearest to what fits, stretched to fill the edge.");
        static readonly GUIContent kTrimLengthContent    = new GUIContent("Length", "The image's length along the edge.");
        static readonly GUIContent kTrimHeightContent    = new GUIContent("Height", "The image's height across both surfaces: each half reaches half of it into its surface.");
        static readonly GUIContent kTrimSurfacesContent  = new GUIContent("Surfaces", "The two surfaces whose edge the trim lies on: the first takes the image's lower half, the second its upper half.");
        const string kTrimNeedsTwoMessage = "A trim decal lies on the edge between two surfaces: give it two.";
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
            var shape    = serializedObject.FindProperty(ChiselDecalComponent.kShapeName);
            EditorGUILayout.PropertyField(shape, kShapeContent);
            var trim = !shape.hasMultipleDifferentValues && shape.enumValueIndex == (int)ChiselDecalShape.Trim;
            if (trim)
            {
                EditorGUILayout.PropertyField(serializedObject.FindProperty(ChiselDecalComponent.kTrimRepeatName), kTrimRepeatContent);
                var size = settings.FindPropertyRelative(nameof(ChiselDecalSettings.size));
                EditorGUILayout.PropertyField(size.FindPropertyRelative("x"), kTrimLengthContent);
                EditorGUILayout.PropertyField(size.FindPropertyRelative("y"), kTrimHeightContent);
            } else
            {
                EditorGUILayout.PropertyField(settings.FindPropertyRelative(nameof(ChiselDecalSettings.center)), kCenterContent);
                EditorGUILayout.PropertyField(settings.FindPropertyRelative(nameof(ChiselDecalSettings.size)), kSizeContent);

                var projection = settings.FindPropertyRelative(nameof(ChiselDecalSettings.projection));
                EditorGUILayout.PropertyField(projection, kProjectionContent);
                if (projection.hasMultipleDifferentValues || projection.enumValueIndex == (int)ChiselDecalProjection.Perspective)
                {
                    EditorGUILayout.Slider(settings.FindPropertyRelative(nameof(ChiselDecalSettings.fieldOfView)),
                                           ChiselDecalSettings.kMinFieldOfView, ChiselDecalSettings.kMaxFieldOfView, kFieldOfViewContent);
                }
            }

            var transparent = settings.FindPropertyRelative(nameof(ChiselDecalSettings.transparent));
            EditorGUILayout.PropertyField(transparent, kTransparentContent);
            EditorGUILayout.PropertyField(settings.FindPropertyRelative(nameof(ChiselDecalSettings.order)), kOrderContent);
            if (!trim)
                EditorGUILayout.Slider(settings.FindPropertyRelative(nameof(ChiselDecalSettings.maxAngle)), 0, 180, kMaxAngleContent);
            if (transparent.hasMultipleDifferentValues || transparent.boolValue)
                EditorGUILayout.PropertyField(settings.FindPropertyRelative(nameof(ChiselDecalSettings.surfaceOffset)), kSurfaceOffsetContent);

            EditorGUILayout.PropertyField(settings.FindPropertyRelative(nameof(ChiselDecalSettings.uvScale)), kUVScaleContent);
            EditorGUILayout.PropertyField(settings.FindPropertyRelative(nameof(ChiselDecalSettings.uvOffset)), kUVOffsetContent);

            EditorGUILayout.PropertyField(serializedObject.FindProperty(ChiselDecalComponent.kTargetsName), trim ? kTrimSurfacesContent : kTargetsContent, true);
            if (trim && !serializedObject.isEditingMultipleObjects && ((ChiselDecalComponent)target).Targets.Count < 2)
                EditorGUILayout.HelpBox(kTrimNeedsTwoMessage, MessageType.Info);

            serializedObject.ApplyModifiedProperties();
        }

        void OnSceneGUI()
        {
            var decal = target as ChiselDecalComponent;
            if (decal == null)
                return;

            // a trim decal follows its edge, not its transform: its outline shows where it lies
            if (decal.Shape == ChiselDecalShape.Trim)
            {
                DrawTrimOutline(decal);
                return;
            }
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

        // Each half's box where it lies on its surface, and the edge they share
        static void DrawTrimOutline(ChiselDecalComponent decal)
        {
            if (!ChiselTrimDecals.TryDescribe(decal, 0, 0, SurfaceDestinationFlags.None, out var model, out var a, out var b) || model == null)
                return;
            var modelToWorld = model.transform.localToWorldMatrix;
            foreach (var half in new[] { a, b })
            {
                var settings = half.decal.settings;
                using (new Handles.DrawingScope(kHandleColor, modelToWorld * (Matrix4x4)half.decal.decalToTree))
                    Handles.DrawWireCube(settings.center, settings.size);
            }
            // the edge: the first half's near side, along its length
            var edge   = modelToWorld * (Matrix4x4)a.decal.decalToTree;
            var length = a.decal.settings.size.x;
            using (new Handles.DrawingScope(kHandleColor, edge))
                Handles.DrawAAPolyLine(4.0f, new Vector3(-length * 0.5f, 0, 0), new Vector3(length * 0.5f, 0, 0));
        }
    }
}
