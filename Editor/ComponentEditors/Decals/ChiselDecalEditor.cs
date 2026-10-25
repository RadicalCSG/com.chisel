using System.Collections.Generic;
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
        static readonly GUIContent kRotateContent        = new GUIContent("Rotate", "Turns the image a quarter turn. A box decal's width and height swap with it, so the image keeps its shape; on a trim decal what ran along the edge runs across it.");
        static readonly GUIContent kRotateLeftContent    = new GUIContent("90° Left", "A quarter turn counter-clockwise, as seen looking at the surface.");
        static readonly GUIContent kRotateRightContent   = new GUIContent("90° Right", "A quarter turn clockwise, as seen looking at the surface.");
        static readonly GUIContent kSizeContent           = new GUIContent("Size", "X and Y are the image's width and height (for a perspective decal: at the center of the box), Z is how deep the decal reaches.");
        static readonly GUIContent kSkewContent           = new GUIContent("Skew", "Shears the decal so its image is a parallelogram, in degrees: X leans the image's sides, Y its top and bottom. For slanted geometry such as stair stringers and ramps.");
        static readonly GUIContent kProjectionContent    = new GUIContent("Projection", "Orthographic keeps the image's size at every depth; perspective projects it from a point behind the box, like a projector.");
        static readonly GUIContent kFieldOfViewContent   = new GUIContent("Field Of View", "The perspective projection's vertical angle, in degrees.");
        static readonly GUIContent kTransparentContent   = new GUIContent("Transparent", "A transparent decal is drawn over the surfaces it covers. An opaque decal replaces them in the rendered mesh; the collider keeps them.");
        static readonly GUIContent kMaxAngleContent      = new GUIContent("Max Angle", "Surfaces turned further than this from the direction the image comes from don't take the decal. 180 takes every surface that doesn't face away from the box's center.");
        static readonly GUIContent kUVScaleContent       = new GUIContent("UV Scale", "The texture coordinates are UV Offset + UV Scale * p, where p runs from (0,0) at the image's lower left to (1,1) at its upper right.");
        static readonly GUIContent kUVOffsetContent      = new GUIContent("UV Offset");
        static readonly GUIContent kBoxShapeContent      = new GUIContent("Box", "The image is projected through the box.");
        static readonly GUIContent kTrimShapeContent     = new GUIContent("Trim", "The image lies on the edge between two surfaces, its lower half on the first, its upper half on the second.");
        static readonly GUIContent[] kShapeOptions       = { kBoxShapeContent, kTrimShapeContent };
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
            if (material.objectReferenceValue == null && !material.hasMultipleDifferentValues)
                EditorGUILayout.HelpBox(kNoMaterialMessage, MessageType.Info);

            var shape    = serializedObject.FindProperty(ChiselDecalComponent.kShapeName);
            EnumToggles(shape, kShapeContent, kShapeOptions, 2);
            var trim = !shape.hasMultipleDifferentValues && shape.enumValueIndex == (int)ChiselDecalShape.Trim;
            if (trim)
            {
                EnumToggles(serializedObject.FindProperty(ChiselDecalComponent.kTrimFillName), kTrimFillContent, kTrimFillOptions, 2);
                var size = settings.FindPropertyRelative(nameof(ChiselDecalSettings.size));
                EditorGUILayout.PropertyField(size.FindPropertyRelative("x"), kTrimLengthContent);
                EditorGUILayout.PropertyField(size.FindPropertyRelative("y"), kTrimHeightContent);
            } else
            {
                var projection = settings.FindPropertyRelative(nameof(ChiselDecalSettings.projection));
                EditorGUILayout.PropertyField(projection, kProjectionContent);
                if (projection.hasMultipleDifferentValues || projection.enumValueIndex == (int)ChiselDecalProjection.Perspective)
                {
                    EditorGUILayout.Slider(settings.FindPropertyRelative(nameof(ChiselDecalSettings.fieldOfView)),
                                           ChiselDecalSettings.kMinFieldOfView, ChiselDecalSettings.kMaxFieldOfView, kFieldOfViewContent);
                }
                EditorGUILayout.Slider(settings.FindPropertyRelative(nameof(ChiselDecalSettings.maxAngle)), 0, 180, kMaxAngleContent);

                EditorGUILayout.PropertyField(settings.FindPropertyRelative(nameof(ChiselDecalSettings.center)), kCenterContent);
                EditorGUILayout.PropertyField(settings.FindPropertyRelative(nameof(ChiselDecalSettings.size)), kSizeContent);
                EditorGUILayout.PropertyField(settings.FindPropertyRelative(nameof(ChiselDecalSettings.skew)), kSkewContent);
                EditorGUILayout.PropertyField(serializedObject.FindProperty(ChiselDecalComponent.kRepeatName), kRepeatContent);
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

        void OnSceneGUI()
        {
            var decal = target as ChiselDecalComponent;
            if (decal == null)
                return;

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
            using (new Handles.DrawingScope(kHandleColor, decal.transform.localToWorldMatrix * (Matrix4x4)ChiselDecalSettings.SkewMatrix(settings.skew)))
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
            foreach (var decal in Object.FindObjectsByType<ChiselDecalComponent>(FindObjectsSortMode.None))
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
