using System.Collections.Generic;
using Chisel.Components;
using Chisel.Core;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;

namespace Chisel.Editors
{
    [CustomEditor(typeof(ChiselDecalComponent)), CanEditMultipleObjects]
#if UNITY_6000_7_OR_NEWER
    [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
#endif
    public sealed class ChiselDecalEditor : Editor
    {
        const string kCreateMenuPath = "GameObject/Chisel/Create Decal";

        static readonly Color kHandleColor = new Color(1.0f, 0.6f, 0.1f, 1.0f);
        static readonly BoxBoundsHandle s_BoxHandle = new BoxBoundsHandle();

        static readonly GUIContent kMaterialContent      = new GUIContent("Material", "What the decal draws with. A decal without a material draws nothing.");
        static readonly GUIContent kCenterContent        = new GUIContent("Center", "The center of the box, in the decal's own space.");
        static readonly GUIContent kRotateContent        = new GUIContent("Rotate", "Turns the image a quarter turn. A box decal's width and height swap with it, so the image keeps its shape; on a trim decal what ran along the edge runs across it.");
        static readonly GUIContent kRotateLeftContent    = new GUIContent("90° Left", "A quarter turn counter-clockwise, as seen looking at the surface.");
        static readonly GUIContent kRotateRightContent   = new GUIContent("90° Right", "A quarter turn clockwise, as seen looking at the surface.");
        static readonly GUIContent kSizeContent           = new GUIContent("Size", "X and Y are the image's width and height (for a perspective decal: at the center of the box), Z is how deep the decal reaches.");
        static readonly GUIContent kOmniSizeContent       = new GUIContent("Size", "X and Y are the image's width and height on each surface; Z is the diameter of the sphere it reaches in, or its cube's edge.");
        static readonly GUIContent kReachContent          = new GUIContent("Reach", "Sphere: every surface no further from the center than half of Size Z takes the decal. Cube: every surface passing through the cube of Size Z, turned with the decal.");
        static readonly GUIContent kSkewContent           = new GUIContent("Skew", "Shears the decal so its image is a parallelogram, in degrees: X leans the image's sides, Y its top and bottom. For slanted geometry such as stair stringers and ramps.");
        static readonly GUIContent kProjectionContent    = new GUIContent("Projection", "Orthographic keeps the image's size at every depth; perspective projects it from a point behind the box, like a projector. Omni goes out from the center in every direction: every surface it reaches takes the whole image, laid flat on it and turned with its texture, like Source's infodecal.");
        static readonly GUIContent kFieldOfViewContent   = new GUIContent("Field Of View", "The perspective projection's vertical angle, in degrees.");
        static readonly GUIContent kFadeDistanceContent  = new GUIContent("Fade Distance", "X: how far from the camera the decal starts to fade, Y: where it is gone (at a 60 degree field of view). A Y of 0 never fades. The decal becomes a renderer of its own, cross-faded by a LODGroup; its material's shader has to support LOD cross-fading, as URP's Lit does.");
        static readonly GUIContent kTransparentContent   = new GUIContent("Transparent", "A transparent decal is drawn over the surfaces it covers. An opaque decal replaces them in the rendered mesh; the collider keeps them.");
        static readonly GUIContent kMaxAngleContent      = new GUIContent("Max Angle", "Surfaces turned further than this from the direction the image comes from don't take the decal. 180 takes every surface that doesn't face away from the box's center.");
        static readonly GUIContent kUVScaleContent       = new GUIContent("UV Scale", "The texture coordinates are UV Offset + UV Scale * p, where p runs from (0,0) at the image's lower left to (1,1) at its upper right.");
        static readonly GUIContent kUVOffsetContent      = new GUIContent("UV Offset");
        static readonly GUIContent kBoxShapeContent      = new GUIContent("Box", "The image is projected through the box.");
        static readonly GUIContent kTrimShapeContent     = new GUIContent("Trim", "The image lies on the edge between two surfaces, its lower half on the first, its upper half on the second.");
        static readonly GUIContent kPolygonShapeContent  = new GUIContent("Polygon", "The image fills an outline drawn on a surface (the Surface Shape tool), and replaces or covers only that surface.");
        internal static readonly GUIContent[] kShapeOptions       = { kBoxShapeContent, kTrimShapeContent, kPolygonShapeContent };
        internal static readonly GUIContent[] kProjectionOptions  = { new GUIContent("Orthographic"), new GUIContent("Perspective"), new GUIContent("Omni") };
        static readonly GUIContent kTargetBrushContent   = new GUIContent("Brush", "The brush whose surface the shape is drawn on.");
        static readonly GUIContent kTargetSurfaceContent = new GUIContent("Surface", "The brush's surface the shape is on, as its surface array numbers them.");
        static readonly GUIContent kDepthContent         = new GUIContent("Depth", "How far the shape reaches either side of its surface, together.");
        static readonly GUIContent kSurfaceUVsContent    = new GUIContent("Surface UVs", "The texture coordinates of the surface it is on instead of the image's, so its material lines up with the surface's texturing.");
        static readonly GUIContent kExtrudeHeightContent = new GUIContent("Extrude Height", "How far Extrude Out and Push In take the shape out of or into its surface.");
        static readonly GUIContent kExtrudeOutContent    = new GUIContent("Extrude Out", "The shape becomes a brush on its surface, Extrude Height tall, that adds to its brush.");
        static readonly GUIContent kPushInContent        = new GUIContent("Push In", "The shape becomes a brush, Extrude Height deep, that cuts into its brush.");
        const string kNoTargetMessage = "The shape isn't on a surface: set the brush it is drawn on.";
        static readonly GUIContent[] kTrimFillOptions    =
        {
            new GUIContent("Stretch", "One image over the whole edge."),
            new GUIContent("Repeat",  "The image repeated at its length, the last one cut short."),
            new GUIContent("Fit",     "The whole number of images nearest to what fits, stretched to fill the edge."),
            new GUIContent("Single",  "One image at its length, round the decal's position (move the decal along the edge to place it).")
        };
        static readonly GUIContent kShapeContent           = new GUIContent("Shape", "Box: the image is projected through the box. Trim: the image lies on the edge between two surfaces, its lower half on the first, its upper half on the second.");
        static readonly GUIContent kImageContent         = new GUIContent("Image", "The whole texture, or a tile of the material's hotspot layout (an image in a trim sheet). A tile can't repeat by wrapping, so it is a piece of its own for every image it repeats.");
        static readonly GUIContent kWholeTextureContent  = new GUIContent("Whole Texture", "The image is the whole texture");
        static readonly GUIContent kRepeatContent        = new GUIContent("Repeat", "How many times the image repeats across the box, in x and y. A perspective decal shows a trim sheet's image once.");
        static readonly GUIContent kTrimMirrorContent     = new GUIContent("Mirror", "The image's lower half on both surfaces, mirrored across the edge, so the trim is the same on either side of it.");
        static readonly GUIContent kTrimFillContent       = new GUIContent("Fill", "Stretch: one image over the whole edge. Repeat: the image repeated at its length, the last one cut short. Fit: the whole number of images nearest to what fits, stretched to fill the edge. Single: one image at its length, round the decal's position (move the decal along the edge to place it).");
        static readonly GUIContent kTrimLengthContent    = new GUIContent("Edge Length", "The image's length along the edge.");
        static readonly GUIContent kTrimHeightContent    = new GUIContent("Size", "The image's size across the edge, over both surfaces: each half reaches half of it into its surface.");
        static readonly GUIContent kPickEdgeContent      = new GUIContent("Pick Edge", "Click a surface near one of its edges in the scene view: the trim goes on that edge, between the surface and the one that meets it there. Shift-click adds the edge to the trim's chain round corners, and goes on picking. Escape stops.");
        static readonly GUIContent kCancelPickContent    = new GUIContent("Cancel Picking");
        static readonly GUIContent kPlaceWhereMeetContent = new GUIContent("Place Where Materials Meet", "Puts a copy of this trim decal on every edge in its model where a surface with its first surface's material meets one with its second surface's material, within a brush or between two.");
        const string kTrimNeedsTwoMessage = "A trim decal lies on the edge between two surfaces: pick its edge.";
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

            var settings = serializedObject.FindProperty(ChiselDecalComponent.kSettingsName);
            var material = serializedObject.FindProperty(ChiselDecalComponent.kMaterialName);
            var transparent = settings.FindPropertyRelative(nameof(ChiselDecalSettings.transparent));
            var shapeValue = serializedObject.FindProperty(ChiselDecalComponent.kShapeName);
            var isTrim = !shapeValue.hasMultipleDifferentValues && shapeValue.enumValueIndex == (int)ChiselDecalShape.Trim;
            EditorGUILayout.PropertyField(material, kMaterialContent);
            MaterialToggles(transparent, isTrim ? serializedObject.FindProperty(ChiselDecalComponent.kTrimMirrorName) : null);
            // opaque decals replace the surface below, so only transparent ones fade
            if (transparent.boolValue || transparent.hasMultipleDifferentValues)
                EditorGUILayout.PropertyField(settings.FindPropertyRelative(nameof(ChiselDecalSettings.fadeDistance)), kFadeDistanceContent);
            if (material.objectReferenceValue == null && !material.hasMultipleDifferentValues)
                EditorGUILayout.HelpBox(kNoMaterialMessage, MessageType.Info);

            var shape    = serializedObject.FindProperty(ChiselDecalComponent.kShapeName);
            EnumToggles(shape, kShapeContent, kShapeOptions, 3);
            var trim    = !shape.hasMultipleDifferentValues && shape.enumValueIndex == (int)ChiselDecalShape.Trim;
            var polygon = !shape.hasMultipleDifferentValues && shape.enumValueIndex == (int)ChiselDecalShape.Polygon;
            if (polygon)
                PolygonFields(settings);
            else if (trim)
            {
                EnumToggles(serializedObject.FindProperty(ChiselDecalComponent.kTrimFillName), kTrimFillContent, kTrimFillOptions, 2);
                var size = settings.FindPropertyRelative(nameof(ChiselDecalSettings.size));
                EditorGUILayout.PropertyField(size.FindPropertyRelative("x"), kTrimLengthContent);
                EditorGUILayout.PropertyField(size.FindPropertyRelative("y"), kTrimHeightContent);
            } else
            {
                var projection = settings.FindPropertyRelative(nameof(ChiselDecalSettings.projection));
                ProjectionField(projection);
                var omni = !projection.hasMultipleDifferentValues && projection.enumValueIndex == (int)ChiselDecalProjection.Omni;
                if (projection.hasMultipleDifferentValues || projection.enumValueIndex == (int)ChiselDecalProjection.Perspective)
                {
                    EditorGUILayout.Slider(settings.FindPropertyRelative(nameof(ChiselDecalSettings.fieldOfView)),
                                           ChiselDecalSettings.kMinFieldOfView, ChiselDecalSettings.kMaxFieldOfView, kFieldOfViewContent);
                }
                if (omni)
                    EditorGUILayout.PropertyField(settings.FindPropertyRelative(nameof(ChiselDecalSettings.reach)), kReachContent);
                else
                    EditorGUILayout.Slider(settings.FindPropertyRelative(nameof(ChiselDecalSettings.maxAngle)), 0, 180, kMaxAngleContent);

                EditorGUILayout.PropertyField(settings.FindPropertyRelative(nameof(ChiselDecalSettings.center)), kCenterContent);
                EditorGUILayout.PropertyField(settings.FindPropertyRelative(nameof(ChiselDecalSettings.size)), omni ? kOmniSizeContent : kSizeContent);
                if (!omni)
                {
                    EditorGUILayout.PropertyField(settings.FindPropertyRelative(nameof(ChiselDecalSettings.skew)), kSkewContent);
                    EditorGUILayout.PropertyField(serializedObject.FindProperty(ChiselDecalComponent.kRepeatName), kRepeatContent);
                }
            }
            ImageField(material, serializedObject.FindProperty(ChiselDecalComponent.kImageTileName));


            RotateButtons(settings, trim);
            EditorGUILayout.PropertyField(settings.FindPropertyRelative(nameof(ChiselDecalSettings.uvScale)), kUVScaleContent);
            EditorGUILayout.PropertyField(settings.FindPropertyRelative(nameof(ChiselDecalSettings.uvOffset)), kUVOffsetContent);

            if (trim && !serializedObject.isEditingMultipleObjects)
            {
                var decal = (ChiselDecalComponent)target;
                if (decal.Targets.Count < 2)
                    EditorGUILayout.HelpBox(kTrimNeedsTwoMessage, MessageType.Info);
                var picking = s_PickingFor == decal;
                if (GUILayout.Button(picking ? kCancelPickContent : kPickEdgeContent))
                {
                    s_PickingFor = picking ? null : decal;
                    s_Candidate  = default;
                    SceneView.RepaintAll();
                }
                using (new EditorGUI.DisabledScope(decal.Targets.Count < 2))
                {
                    if (GUILayout.Button(kPlaceWhereMeetContent))
                        PlaceWhereMaterialsMeet(decal);
                }
            }

            serializedObject.ApplyModifiedProperties();
        }

        // A box decal's projections; a polygon decal's own is set from its shape
        static void ProjectionField(SerializedProperty projection)
        {
            var rect = EditorGUILayout.GetControlRect();
            using (new EditorGUI.PropertyScope(rect, kProjectionContent, projection))
            {
                var index = projection.hasMultipleDifferentValues ? -1 : projection.enumValueIndex;
                EditorGUI.BeginChangeCheck();
                index = EditorGUI.Popup(rect, kProjectionContent, index < kProjectionOptions.Length ? index : 0, kProjectionOptions);
                if (EditorGUI.EndChangeCheck())
                    projection.enumValueIndex = index;
            }
        }

        // A polygon decal: the surface it is drawn on, how deep it reaches, and its texturing
        void PolygonFields(SerializedProperty settings)
        {
            var targetList = serializedObject.FindProperty(ChiselDecalComponent.kTargetsName);
            if (!serializedObject.isEditingMultipleObjects)
            {
                if (targetList.arraySize == 0)
                {
                    targetList.arraySize = 1;
                    targetList.GetArrayElementAtIndex(0).FindPropertyRelative(nameof(ChiselDecalSurfaceTarget.surfaceIndex)).intValue = 0;
                }
                var first = targetList.GetArrayElementAtIndex(0);
                var brush = first.FindPropertyRelative(nameof(ChiselDecalSurfaceTarget.brush));
                EditorGUILayout.PropertyField(brush, kTargetBrushContent);
                EditorGUILayout.PropertyField(first.FindPropertyRelative(nameof(ChiselDecalSurfaceTarget.surfaceIndex)), kTargetSurfaceContent);
                if (brush.objectReferenceValue == null)
                    EditorGUILayout.HelpBox(kNoTargetMessage, MessageType.Warning);
            }
            var depth = settings.FindPropertyRelative(nameof(ChiselDecalSettings.size)).FindPropertyRelative("z");
            EditorGUILayout.PropertyField(depth, kDepthContent);
            EditorGUILayout.Slider(settings.FindPropertyRelative(nameof(ChiselDecalSettings.maxAngle)), 0, 180, kMaxAngleContent);
            EditorGUILayout.PropertyField(settings.FindPropertyRelative(nameof(ChiselDecalSettings.surfaceUVs)), kSurfaceUVsContent);

            if (serializedObject.isEditingMultipleObjects)
                return;
            s_ExtrudeHeight = Mathf.Max(0.001f, EditorGUILayout.FloatField(kExtrudeHeightContent, s_ExtrudeHeight));
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.PrefixLabel(GUIContent.none);
                var height = 0f;
                if (GUILayout.Button(kExtrudeOutContent, EditorStyles.miniButtonLeft))
                    height = s_ExtrudeHeight;
                if (GUILayout.Button(kPushInContent, EditorStyles.miniButtonRight))
                    height = -s_ExtrudeHeight;
                // the decal is gone: nothing more of this inspector
                if (height != 0 && ExtrudeIntoBrush((ChiselDecalComponent)target, height) != null)
                    GUIUtility.ExitGUI();
            }
        }

        static float s_ExtrudeHeight = 0.25f;

        // The shape becomes a brush that adds to or cuts into its surface, in place of the decal: one undo
        internal static ChiselExtrudedShapeComponent ExtrudeIntoBrush(ChiselDecalComponent decal, float height)
        {
            var group = Undo.GetCurrentGroup();
            var extruded = decal.ExtrudeIntoBrush(height);
            if (extruded == null)
                return null;
            Undo.DestroyObjectImmediate(decal.gameObject);
            Undo.CollapseUndoOperations(group);
            Undo.SetCurrentGroupName(height > 0 ? "Extrude Surface Shape" : "Push In Surface Shape");
            Selection.activeGameObject = extruded.gameObject;
            return extruded;
        }

        // The image: the whole texture, or a tile picked from the material's hotspot layout (only with a layout)
        void ImageField(SerializedProperty material, SerializedProperty tile)
        {
            var layout = material.hasMultipleDifferentValues ? null : HotspotTransformGUI.LayoutOf(material.objectReferenceValue as Material);
            if (layout == null)
                return;
            var rect = EditorGUILayout.GetControlRect();
            using (new EditorGUI.PropertyScope(rect, kImageContent, tile))
            {
                var field = EditorGUI.PrefixLabel(rect, kImageContent);
                var name  = tile.hasMultipleDifferentValues ? "—" : tile.intValue == 0 ? kWholeTextureContent.text : HotspotTilePicker.NameOf(layout, tile.intValue);
                if (string.IsNullOrEmpty(name))
                    name = "Missing Tile " + tile.intValue;
                if (!GUI.Button(field, name, EditorStyles.popup))
                    return;
                var pages = HotspotTransformGUI.PagesOf((Material)material.objectReferenceValue, layout).FindAll(page => page.sheet == 0);
                var decals = targets;
                var current = tile.hasMultipleDifferentValues ? -1 : tile.intValue;
                PopupWindow.Show(field, new HotspotTilePicker(pages, null, new Vector2Int(0, current), picked =>
                {
                    Undo.RecordObjects(decals, "Decal Image");
                    foreach (var decal in decals)
                    {
                        if (decal is ChiselDecalComponent component)
                        {
                            component.ImageTile = picked.y;
                            EditorUtility.SetDirty(component);
                        }
                    }
                }, kWholeTextureContent));
            }
        }

        // A quarter turn of the image; a box decal's width and height (and repeats) swap, so the image keeps its shape
        void RotateButtons(SerializedProperty settings, bool trim)
        {
            var rect = EditorGUILayout.GetControlRect();
            rect = EditorGUI.PrefixLabel(rect, kRotateContent);
            var left  = new Rect(rect.x, rect.y, rect.width * 0.5f, rect.height);
            var right = new Rect(left.xMax, rect.y, rect.width - left.width, rect.height);
            var indent = EditorGUI.indentLevel;
            EditorGUI.indentLevel = 0;
            var turn = 0;
            if (GUI.Button(left, kRotateLeftContent, EditorStyles.miniButtonLeft))   turn = 1;
            if (GUI.Button(right, kRotateRightContent, EditorStyles.miniButtonRight)) turn = 3;
            EditorGUI.indentLevel = indent;
            if (turn == 0)
                return;
            serializedObject.ApplyModifiedProperties();
            foreach (var decalObject in serializedObject.targetObjects)
            {
                using var decal = new SerializedObject(decalObject);
                var decalSettings = decal.FindProperty(ChiselDecalComponent.kSettingsName);
                var turns = decalSettings.FindPropertyRelative(nameof(ChiselDecalSettings.imageTurns));
                turns.intValue = (turns.intValue + turn) & 3;
                if (!trim)
                {
                    var size = decalSettings.FindPropertyRelative(nameof(ChiselDecalSettings.size));
                    var sizeX = size.FindPropertyRelative("x");
                    var sizeY = size.FindPropertyRelative("y");
                    (sizeX.floatValue, sizeY.floatValue) = (sizeY.floatValue, sizeX.floatValue);
                    var repeat = decal.FindProperty(ChiselDecalComponent.kRepeatName);
                    repeat.vector2Value = new Vector2(repeat.vector2Value.y, repeat.vector2Value.x);
                }
                decal.ApplyModifiedProperties();
            }
            serializedObject.Update();
        }

        // Under the material, on one line: Transparent, and for a trim decal Mirror
        static void MaterialToggles(SerializedProperty transparent, SerializedProperty mirror)
        {
            var rect = EditorGUILayout.GetControlRect();
            rect.xMin += EditorGUIUtility.labelWidth;
            var half = rect.width * 0.5f;
            var indent = EditorGUI.indentLevel;
            EditorGUI.indentLevel = 0;
            ToggleLeft(new Rect(rect.x, rect.y, half, rect.height), transparent, kTransparentContent);
            if (mirror != null)
                ToggleLeft(new Rect(rect.x + half, rect.y, rect.width - half, rect.height), mirror, kTrimMirrorContent);
            EditorGUI.indentLevel = indent;
        }

        static void ToggleLeft(Rect rect, SerializedProperty property, GUIContent label)
        {
            using (new EditorGUI.PropertyScope(rect, label, property))
            {
                EditorGUI.BeginChangeCheck();
                EditorGUI.showMixedValue = property.hasMultipleDifferentValues;
                var value = EditorGUI.ToggleLeft(rect, label, property.boolValue);
                EditorGUI.showMixedValue = false;
                if (EditorGUI.EndChangeCheck())
                    property.boolValue = value;
            }
        }

        // An enum as toggles, one of them on (none while the selection differs), perRow to a line
        static void EnumToggles(SerializedProperty property, GUIContent label, GUIContent[] options, int perRow)
        {
            var mixed = property.hasMultipleDifferentValues;
            var rows  = (options.Length + perRow - 1) / perRow;
            var line  = EditorGUIUtility.singleLineHeight;
            var rect  = EditorGUILayout.GetControlRect(true, rows * line + (rows - 1) * EditorGUIUtility.standardVerticalSpacing);
            using (new EditorGUI.PropertyScope(rect, label, property))
            {
                rect = EditorGUI.PrefixLabel(new Rect(rect.x, rect.y, rect.width, line), label);
                var indent = EditorGUI.indentLevel;
                EditorGUI.indentLevel = 0;
                for (int i = 0; i < options.Length; i++)
                {
                    int row = i / perRow, column = i % perRow;
                    var width  = rect.width / perRow;
                    var button = new Rect(rect.x + column * width, rect.y + row * (line + EditorGUIUtility.standardVerticalSpacing), width, line);
                    var style  = perRow == 1 ? EditorStyles.miniButton : column == 0 ? EditorStyles.miniButtonLeft : column == perRow - 1 ? EditorStyles.miniButtonRight : EditorStyles.miniButtonMid;
                    var on     = !mixed && property.enumValueIndex == i;
                    if (GUI.Toggle(button, on, options[i], style) && !on)
                        property.enumValueIndex = i;
                }
                EditorGUI.indentLevel = indent;
            }
        }

        // Unity's move handle, hidden while a polygon decal shows its own
        static bool s_HidTools;

        static void HideTools()
        {
            if (Tools.hidden)
                return;
            Tools.hidden = true;
            s_HidTools = true;
        }

        static void RestoreTools()
        {
            if (!s_HidTools)
                return;
            Tools.hidden = false;
            s_HidTools = false;
        }

        void OnDisable() => RestoreTools();

        static readonly int kExtrudeHandleHash = "ChiselDecalExtrude".GetHashCode();
        // how far the extrude arrow is dragged out of the surface (into it: below zero)
        static float s_DragHeight;

        // Arrows along the surface, in the surface's space whatever the handle rotation, and a square to slide it in the
        // surface's plane; the arrow out of the surface extrudes the shape when let go, out of it or into it
        static void SurfaceHandles(ChiselDecalComponent decal)
        {
            var transform = decal.transform;
            var position  = transform.position;
            var right     = transform.right;
            var up        = transform.up;
            var normal    = -transform.forward;
            var size      = HandleUtility.GetHandleSize(position);
            var snap      = EditorSnapSettings.move;

            EditorGUI.BeginChangeCheck();
            Handles.color = Handles.xAxisColor;
            var alongRight = Handles.Slider(position, right, size, Handles.ArrowHandleCap, snap.x);
            Handles.color = Handles.yAxisColor;
            var alongUp = Handles.Slider(position, up, size, Handles.ArrowHandleCap, snap.y);
            Handles.color = Handles.zAxisColor;
            var squareOffset = (right + up) * (size * 0.25f);
            var inPlane = Handles.Slider2D(position + squareOffset, normal, right, up, size * 0.12f, Handles.RectangleHandleCap,
                                           new Vector2(snap.x, snap.y)) - squareOffset;
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(transform, "Move Surface Shape");
                transform.position = position + (alongRight - position) + (alongUp - position) + (inPlane - position);
            }

            CornerHandles(decal);

            var id = GUIUtility.GetControlID(kExtrudeHandleHash, FocusType.Passive);
            var wasDragging = GUIUtility.hotControl == id;
            Handles.color = kHandleColor;
            // while dragged the arrow is where it was dragged to: the slider only moves it on drag events
            var at  = wasDragging ? position + normal * s_DragHeight : position;
            var tip = Handles.Slider(id, at, Vector3.zero, normal, size, Handles.ArrowHandleCap, snap.y);
            if (GUIUtility.hotControl == id)
            {
                s_DragHeight = Vector3.Dot(tip - position, normal);
                DragExtrusion(decal, s_DragHeight);
                DrawExtrusion(decal, normal * s_DragHeight);
            } else if (wasDragging)
            {
                s_DragHeight = 0;
                if (FinishExtrusion(decal))
                    GUIUtility.ExitGUI();
            }
        }

        static readonly int kCornerHandleHash = "ChiselDecalCorner".GetHashCode();
        static readonly List<Vector2> s_Corners = new();
        static readonly List<Vector3> s_Outline = new();
        // How near (in pixels) the surface's corners and edges pull a dragged corner onto them
        const float kSnapPixels = 10;

        // A shape's corners where its surface has them: dragged along the surface (onto its edges and corners, where they
        // stay), Ctrl-click takes one away, the dot halfway along a side adds one there
        static void CornerHandles(ChiselDecalComponent decal)
        {
            ChiselSurfaceShapes.Resolve(decal, out var frame, s_Corners);
            var count = s_Corners.Count;
            if (count < 3)
                return;
            var normal  = -(Vector3)frame.GetColumn(2).normalized;
            var right   = ((Vector3)frame.GetColumn(0)).normalized;
            var up      = ((Vector3)frame.GetColumn(1)).normalized;
            var targets = decal.Targets;
            var onSurface = targets.Count > 0 && targets[0].brush != null &&
                            ChiselSurfaceShapes.SurfaceOutline(targets[0].brush, targets[0].surfaceIndex, s_Outline, out _);
            var snap = EditorSnapSettings.move;
            var evt  = Event.current;

            for (int i = 0; i < count; i++)
            {
                var world = frame.MultiplyPoint3x4(s_Corners[i]);
                var size  = HandleUtility.GetHandleSize(world);
                var id    = GUIUtility.GetControlID(kCornerHandleHash, FocusType.Passive);
                if (evt.type == EventType.MouseDown && evt.button == 0 && (evt.control || evt.command) &&
                    HandleUtility.nearestControl == id && count > 3)
                {
                    evt.Use();
                    s_Corners.RemoveAt(i);
                    SetCorners(decal, frame, "Remove Surface Shape Corner");
                    GUIUtility.ExitGUI();
                }
                Handles.color = decal.AnchorOf(i).IsAnchored ? Handles.selectedColor : kHandleColor;
                EditorGUI.BeginChangeCheck();
                var moved = Handles.Slider2D(id, world, Vector3.zero, normal, right, up, size * 0.06f, Handles.DotHandleCap,
                                             new Vector2(snap.x, snap.y), false);
                if (EditorGUI.EndChangeCheck())
                {
                    var reach = size * kSnapPixels / 80.0f;
                    if (onSurface && ChiselSurfaceShapes.NearestOnOutline(s_Outline, moved, reach, reach * 1.5f, out var snapped))
                        moved = snapped;
                    s_Corners[i] = frame.inverse.MultiplyPoint3x4(moved);
                    SetCorners(decal, frame, "Move Surface Shape Corner");
                }
            }

            for (int i = 0; i < count; i++)
            {
                var a = frame.MultiplyPoint3x4(s_Corners[i]);
                var b = frame.MultiplyPoint3x4(s_Corners[(i + 1) % count]);
                var middle = (a + b) * 0.5f;
                var size = HandleUtility.GetHandleSize(middle);
                Handles.color = kHandleColor;
                if (Handles.Button(middle, Quaternion.LookRotation(normal), size * 0.03f, size * 0.05f, Handles.DotHandleCap))
                {
                    s_Corners.Insert(i + 1, frame.inverse.MultiplyPoint3x4(middle));
                    SetCorners(decal, frame, "Add Surface Shape Corner");
                    GUIUtility.ExitGUI();
                }
            }
        }

        // The corners, in the frame their surface has them in, as the decal's own: anchored again where they lie on edges
        static void SetCorners(ChiselDecalComponent decal, Matrix4x4 frame, string undoName)
        {
            var toDecal = decal.transform.worldToLocalMatrix * frame;
            var corners = new List<Vector2>(s_Corners.Count);
            foreach (var corner in s_Corners)
                corners.Add(toDecal.MultiplyPoint3x4(corner));
            Undo.RecordObject(decal, undoName);
            decal.SetPolygon(corners);
        }

        // The outline of the brush being dragged, over it
        static void DrawExtrusion(ChiselDecalComponent decal, Vector3 offset)
        {
            var outline = decal.Polygon;
            if (outline.Count < 3 || Event.current.type != EventType.Repaint)
                return;
            Handles.color = kHandleColor;
            for (int i = 0; i < outline.Count; i++)
            {
                var a = decal.transform.TransformPoint(outline[i]);
                var b = decal.transform.TransformPoint(outline[(i + 1) % outline.Count]);
                Handles.DrawLine(a + offset, b + offset);
                Handles.DrawLine(a, a + offset);
            }
        }

        // The brush being dragged out of or into the surface, from the undo group it began in
        static ChiselExtrudedShapeComponent s_Dragged;
        static int s_DragUndoGroup = -1;

        // The brush is made as soon as the arrow leaves the surface, and follows it
        static void DragExtrusion(ChiselDecalComponent decal, float height)
        {
            if (Event.current.type == EventType.Layout || Event.current.type == EventType.Repaint)
                return;
            if (s_Dragged == null)
            {
                if (!(Mathf.Abs(height) > 1e-3f))
                    return;
                s_DragUndoGroup = Undo.GetCurrentGroup();
                s_Dragged = decal.ExtrudeIntoBrush(height);
                return;
            }
            Undo.RecordObject(s_Dragged, "Extrude Surface Shape");
            // never flat: it is taken away when let go on the surface
            ChiselDecalComponent.SetExtrusionHeight(s_Dragged, Mathf.Abs(height) > 1e-3f ? height : (height < 0 ? -1e-3f : 1e-3f));
        }

        // Let go: the brush in the shape's place, one undo; let go on the surface: no brush
        static bool FinishExtrusion(ChiselDecalComponent decal)
        {
            var extruded = s_Dragged;
            var height   = extruded ? extruded.definition.path.segments[1].position.y : 0;
            s_Dragged = null;
            if (extruded == null)
                return false;
            if (!(Mathf.Abs(height) > 1e-3f + 1e-6f))
            {
                Undo.RevertAllDownToGroup(s_DragUndoGroup);
                return true;
            }
            Undo.DestroyObjectImmediate(decal.gameObject);
            Undo.CollapseUndoOperations(s_DragUndoGroup);
            Undo.SetCurrentGroupName(height > 0 ? "Extrude Surface Shape" : "Push In Surface Shape");
            Selection.activeGameObject = extruded.gameObject;
            return true;
        }

        void OnSceneGUI()
        {
            var decal = target as ChiselDecalComponent;
            if (decal == null)
                return;

            // a polygon decal lies on its surface: it moves in the surface's plane, and out of it is extruding
            if (decal.Shape == ChiselDecalShape.Polygon)
            {
                if (Tools.current == Tool.Move && targets.Length == 1)
                {
                    HideTools();
                    SurfaceHandles(decal);
                } else
                    RestoreTools();
                return;
            }
            RestoreTools();

            // a trim decal follows its edge, not its transform: its outline shows where it lies
            if (decal.Shape == ChiselDecalShape.Trim)
            {
                if (s_PickingFor == decal)
                    PickEdge(decal);
                DrawTrimOutline(decal);
                return;
            }
            var settings = decal.Settings;
            // the box handle in the decal's unsheared space, so a skewed decal's handle is its parallelogram
            var skew = settings.projection == ChiselDecalProjection.Omni ? Unity.Mathematics.float2.zero : settings.skew;
            using (new Handles.DrawingScope(kHandleColor, decal.transform.localToWorldMatrix * (Matrix4x4)ChiselDecalSettings.SkewMatrix(skew)))
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
        // Copies of a trim decal on every edge where its two surfaces' materials meet, under a "Trim Decals" object next to
        // it; an edge that already has a trim decal of these surfaces gets none
        static void PlaceWhereMaterialsMeet(ChiselDecalComponent template)
        {
            var model = template.Targets[0].brush ? template.Targets[0].brush.hierarchyItem.Model : null;
            var edges = new List<ChiselTrimDecals.Edge>();
            ChiselTrimDecals.EdgesBetween(model, ChiselTrimDecals.MaterialOf(template.Targets[0]), ChiselTrimDecals.MaterialOf(template.Targets[1]), edges);
            if (edges.Count == 0)
                return;

            var existing = new HashSet<(ChiselNodeComponent, int, ChiselNodeComponent, int)>();
            foreach (var decal in Object.FindObjectsByType<ChiselDecalComponent>(FindObjectsInactive.Exclude))
            {
                if (decal.Shape == ChiselDecalShape.Trim && decal.Targets.Count >= 2)
                    existing.Add((decal.Targets[0].brush, decal.Targets[0].surfaceIndex, decal.Targets[1].brush, decal.Targets[1].surfaceIndex));
            }

            Undo.SetCurrentGroupName("Place Trim Decals");
            var undoGroup = Undo.GetCurrentGroup();
            var parent = template.transform.parent;
            var group  = parent ? parent.Find(kTrimGroupName) : null;
            if (group == null)
            {
                group = new GameObject(kTrimGroupName).transform;
                group.SetParent(parent, false);
                Undo.RegisterCreatedObjectUndo(group.gameObject, "Place Trim Decals");
            }
            var modelToWorld = model.transform.localToWorldMatrix;
            foreach (var edge in edges)
            {
                if (!existing.Add((edge.a.brush, edge.a.surfaceIndex, edge.b.brush, edge.b.surfaceIndex)))
                    continue;
                var copy = Object.Instantiate(template.gameObject, group);
                copy.name = template.name;
                Undo.RegisterCreatedObjectUndo(copy, "Place Trim Decals");
                copy.transform.position = modelToWorld.MultiplyPoint3x4(edge.middle);
                var copyDecal = copy.GetComponent<ChiselDecalComponent>();
                copyDecal.SetTargets(new[] { edge.a, edge.b });
                copyDecal.SetTrimEdge(edge.edgeOnB, edge.edgeIndex);
            }
            Undo.CollapseUndoOperations(undoGroup);
        }

        const string kTrimGroupName = "Trim Decals";

        // Picking a trim decal's edge in the scene view (Pick Edge)
        static ChiselDecalComponent s_PickingFor;
        static EdgeCandidate s_Candidate;
        static readonly Color kCandidateColor = new Color(0.2f, 0.9f, 1.0f, 1.0f);

        struct EdgeCandidate
        {
            public bool valid;
            public ChiselDecalSurfaceTarget a, b;
            public Vector3 from, to;    // the edge, in world space
            public Vector3 at;          // the point on it nearest to where was clicked
            public int edgeIndex;       // its number along a's outline
        }

        static void PickEdge(ChiselDecalComponent decal)
        {
            var id  = GUIUtility.GetControlID(FocusType.Passive);
            var evt = Event.current;
            switch (evt.GetTypeForControl(id))
            {
                case EventType.Layout:
                    HandleUtility.AddDefaultControl(id);
                    break;
                case EventType.MouseMove:
                    s_Candidate = FindEdge(evt.mousePosition);
                    HandleUtility.Repaint();
                    break;
                case EventType.MouseDown:
                    if (evt.button != 0 || evt.alt)
                        break;
                    s_Candidate = FindEdge(evt.mousePosition);
                    // shift adds the edge to the chain, and goes on picking
                    var addToChain = evt.shift && decal.Targets.Count >= 2;
                    if (s_Candidate.valid)
                    {
                        Undo.RecordObjects(new Object[] { decal, decal.transform }, "Pick Trim Edge");
                        if (addToChain)
                            decal.AddTrimEdge(s_Candidate.a, s_Candidate.b, false, s_Candidate.edgeIndex);
                        else
                        {
                            decal.SetTargets(new[] { s_Candidate.a, s_Candidate.b });
                            decal.SetTrimEdge(false, s_Candidate.edgeIndex);
                            decal.transform.position = s_Candidate.at;
                        }
                        EditorUtility.SetDirty(decal);
                    }
                    if (!addToChain)
                        s_PickingFor = null;
                    s_Candidate  = default;
                    evt.Use();
                    break;
                case EventType.KeyDown:
                    if (evt.keyCode != KeyCode.Escape)
                        break;
                    s_PickingFor = null;
                    s_Candidate  = default;
                    evt.Use();
                    break;
                case EventType.Repaint:
                    if (s_Candidate.valid)
                    {
                        using (new Handles.DrawingScope(kCandidateColor))
                            Handles.DrawAAPolyLine(6.0f, s_Candidate.from, s_Candidate.to);
                    }
                    break;
            }
        }

        static readonly List<ChiselIntersection> s_Intersections = new();

        // The nearest brush surface under a point of the scene view (a decal in front of it is passed by)
        static bool PickSurface(Vector2 position, out ChiselIntersection surface)
        {
            surface = default;
            ChiselSelectionManager.GetOverlappingObjects(position, s_Intersections);
            foreach (var intersection in s_Intersections)
            {
                if (intersection.treeNode != null && intersection.model != null && intersection.brushIntersection.brush.Valid)
                {
                    surface = intersection;
                    return true;
                }
            }
            return false;
        }

        // The edge of the clicked surface nearest to where it was clicked, and the surface that meets it there: the one
        // just across the edge when it lies along it (a floor under a wall), else the same brush's surface on the edge's
        // other side (a box's edge)
        static EdgeCandidate FindEdge(Vector2 mousePosition)
        {
            if (!PickSurface(mousePosition, out var hit))
                return default;
            var brush   = hit.brushIntersection.brush;
            var polygon = hit.brushIntersection.surfaceIndex;
            var point   = (Unity.Mathematics.float3)hit.brushIntersection.surfaceIntersection.treePlaneIntersection;
            if (!ChiselTrimDecals.NearestEdge(brush, polygon, point, out var from, out var to, out var otherPolygon, out var edgeIndex))
                return default;
            var edge    = to - from;
            var nearest = from + edge * Unity.Mathematics.math.saturate(Unity.Mathematics.math.dot(point - from, edge) / Unity.Mathematics.math.max(Unity.Mathematics.math.lengthsq(edge), 1e-12f));
            var modelToWorld = hit.model.transform.localToWorldMatrix;
            var worldNearest = modelToWorld.MultiplyPoint3x4(nearest);

            var b = default(ChiselDecalSurfaceTarget);
            var across = nearest - point;
            var acrossLength = Unity.Mathematics.math.length(across);
            if (acrossLength > 1e-5f)
            {
                var probe = nearest + across / acrossLength * (HandleUtility.GetHandleSize(worldNearest) * 0.05f);
                if (PickSurface(HandleUtility.WorldToGUIPoint(modelToWorld.MultiplyPoint3x4(probe)), out var beyond) &&
                    !(beyond.brushIntersection.brush == brush && beyond.brushIntersection.surfaceIndex == polygon) &&
                    beyond.model == hit.model &&
                    ChiselTrimDecals.IsDrawn(hit.model, beyond.brushIntersection.brush, beyond.brushIntersection.surfaceIndex) &&
                    ChiselTrimDecals.PlaneHolds(beyond.brushIntersection.brush, beyond.brushIntersection.surfaceIndex, from, to))
                    b = new ChiselDecalSurfaceTarget { brush = beyond.treeNode, surfaceIndex = ChiselTrimDecals.SurfaceOf(beyond.brushIntersection.brush, beyond.brushIntersection.surfaceIndex) };
            }
            // the click past the edge can miss (it depends on the zoom and the angle): then another brush's drawn surface
            // that goes on across the edge, and only failing that the clicked brush's own face there, when it is drawn
            if (b.brush == null && acrossLength > 1e-5f && Camera.current != null)
            {
                var viewDirection = (Unity.Mathematics.float3)hit.model.transform.InverseTransformDirection(Camera.current.transform.forward);
                var beyondPoint = nearest + across / acrossLength * 0.02f;
                if (ChiselTrimDecals.SurfaceAcross(hit.model, hit.treeNode, from, to, beyondPoint, viewDirection, out var acrossTarget))
                    b = acrossTarget;
            }
            if (b.brush == null && otherPolygon >= 0 && ChiselTrimDecals.IsDrawn(hit.model, brush, otherPolygon))
                b = new ChiselDecalSurfaceTarget { brush = hit.treeNode, surfaceIndex = ChiselTrimDecals.SurfaceOf(brush, otherPolygon) };
            if (b.brush == null || b.surfaceIndex < 0)
                return default;
            return new EdgeCandidate
            {
                valid = true,
                a     = new ChiselDecalSurfaceTarget { brush = hit.treeNode, surfaceIndex = ChiselTrimDecals.SurfaceOf(brush, polygon) },
                b     = b,
                from  = modelToWorld.MultiplyPoint3x4(from),
                to    = modelToWorld.MultiplyPoint3x4(to),
                at    = worldNearest,
                edgeIndex = edgeIndex
            };
        }

        static readonly List<ChiselTrimDecals.Half> s_OutlineHalves = new();

        static void DrawTrimOutline(ChiselDecalComponent decal)
        {
            if (!ChiselTrimDecals.TryDescribe(decal, 0, 0, SurfaceDestinationFlags.None, out var model, s_OutlineHalves, out var edgeFrom, out var edgeTo) || model == null)
                return;
            var modelToWorld = model.transform.localToWorldMatrix;
            foreach (var half in s_OutlineHalves)
            {
                var settings = half.decal.settings;
                using (new Handles.DrawingScope(kHandleColor, modelToWorld * (Matrix4x4)half.decal.decalToTree))
                    Handles.DrawWireCube(settings.center, settings.size);
            }
            using (new Handles.DrawingScope(kHandleColor, modelToWorld))
                Handles.DrawAAPolyLine(4.0f, edgeFrom, edgeTo);
        }
    }
}
