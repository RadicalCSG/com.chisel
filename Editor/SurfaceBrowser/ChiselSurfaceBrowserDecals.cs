using System.Collections.Generic;
using Chisel.Components;
using Chisel.Core;
using UnityEditor;
using UnityEngine;

namespace Chisel.Editors
{
    // The decal images of the materials marked as decals, and placing them: one per tile of a decal sheet, or the whole
    // texture for a material without a layout. An image dragged into the scene view is placed where it's dropped
    [InitializeOnLoad]
    public static class ChiselSurfaceBrowserDecals
    {
        public const string kDragKey = "ChiselDecalImage";

        static ChiselSurfaceBrowserDecals()
        {
            SceneView.duringSceneGui -= OnSceneGUI;
            SceneView.duringSceneGui += OnSceneGUI;
        }

        /// <summary>Starts dragging an image; not as a material, so dropping it doesn't paint a surface</summary>
        public static void StartDrag(Image image)
        {
            DragAndDrop.PrepareStartDrag();
            DragAndDrop.objectReferences = System.Array.Empty<Object>();
            DragAndDrop.SetGenericData(kDragKey, image);
            DragAndDrop.StartDrag(image.name);
        }

        // the brush surface under a point of the scene view
        static bool PickSurface(Vector2 mousePosition, out ChiselIntersection hit)
        {
            hit = default;
            var intersections = new List<ChiselIntersection>();
            ChiselSelectionManager.GetOverlappingObjects(mousePosition, intersections);
            foreach (var intersection in intersections)
            {
                if (intersection.treeNode != null && intersection.model != null && intersection.brushIntersection.brush.Valid)
                {
                    hit = intersection;
                    return true;
                }
            }
            return false;
        }

        static void OnSceneGUI(SceneView sceneView)
        {
            var evt = Event.current;
            if (evt.type != EventType.DragUpdated && evt.type != EventType.DragPerform)
                return;
            if (DragAndDrop.GetGenericData(kDragKey) is not Image image || image.material == null)
                return;
            var shape = ChiselDecalComponent.DefaultsOf(image.material).shape;
            var onSurface = PickSurface(evt.mousePosition, out var hit);
            DragAndDrop.visualMode = onSurface || shape == ChiselDecalShape.Polygon ? DragAndDropVisualMode.Copy : DragAndDropVisualMode.Rejected;
            if (evt.type == EventType.DragPerform)
            {
                DragAndDrop.AcceptDrag();
                DragAndDrop.SetGenericData(kDragKey, null);
                Drop(image, shape, onSurface, hit, evt.mousePosition);
            }
            evt.Use();
        }

        // a box where it's dropped, a trim on the edge nearest to it; a polygon starts drawing
        static void Drop(Image image, ChiselDecalShape shape, bool onSurface, ChiselIntersection hit, Vector2 mousePosition)
        {
            ChiselSurfaceBrowserPrefs.MarkUsed(image.Key);
            switch (shape)
            {
                case ChiselDecalShape.Polygon:
                    ChiselSurfaceShapeTool.StartDrawing(image.material, image.tile);
                    return;
                case ChiselDecalShape.Trim:
                {
                    var decal = CreateTrim(image.material, image.tile);
                    if (!ChiselDecalEditor.PlaceTrimAt(decal, mousePosition))
                        ChiselDecalEditor.StartPickingEdge(decal, removeIfCancelled: true);
                    else
                        Selection.activeGameObject = decal.gameObject;
                    return;
                }
                default:
                {
                    if (!onSurface)
                        return;
                    var decal = CreateBoxAt(hit.treeNode.transform, image.material, image.tile, hit.worldPlaneIntersection, hit.worldPlane.normal);
                    if (decal == null)
                        return;
                    Undo.RegisterCreatedObjectUndo(decal.gameObject, "Place Decal");
                    Selection.activeGameObject = decal.gameObject;
                    ChiselDecalManager.SetDirty();
                    ChiselSceneMaterialUsage.SetDirty();
                    return;
                }
            }
        }
        public sealed class Image
        {
            public Material material;
            public string   guid;       // the material's
            public int      tile;       // 0: the whole texture
            public string   name;
            public int      index;      // 1 based, of count on its material
            public int      count;

            public string Key => guid + ":" + tile;
        }

        public static bool IsDecal(Material material)
        {
            var metadata = material != null ? material.GetMetadataOfType<ChiselSurfaceMetadata>() : null;
            return metadata != null && metadata.decalDefaults.isDecal;
        }

        /// <summary>A decal material's images: each tile of its hotspot layout, or its whole texture without one; none for
        /// a material that isn't a decal</summary>
        public static List<Image> ImagesOf(Material material, string guid)
        {
            var images = new List<Image>();
            if (!IsDecal(material))
                return images;
            var layout = material.GetMetadataOfType<ChiselSurfaceMetadata>().hotspotLayout;
            if (layout != null && layout.tiles != null && layout.tiles.Count > 0)
            {
                foreach (var tile in layout.tiles)
                {
                    var name = string.IsNullOrEmpty(tile.name) ? $"{material.name} {images.Count + 1}" : tile.name;
                    images.Add(new Image { material = material, guid = guid, tile = tile.id, name = name });
                }
            } else
                images.Add(new Image { material = material, guid = guid, tile = 0, name = material.name });
            for (int i = 0; i < images.Count; i++)
            {
                images[i].index = i + 1;
                images[i].count = images.Count;
            }
            return images;
        }

        /// <summary>
        /// A box decal of an image on a surface: at the middle of its outline (world space), facing it, a child of
        /// parent, with the material's decal defaults
        /// </summary>
        public static ChiselDecalComponent CreateBox(Transform parent, Material material, int tile, IReadOnlyList<Vector3> outline, Vector3 normal)
        {
            if (material == null || outline == null || outline.Count == 0 || normal.sqrMagnitude < 1e-12f)
                return null;
            var center = Vector3.zero;
            foreach (var point in outline)
                center += point;
            center /= outline.Count;
            return CreateBoxAt(parent, material, tile, center, normal);
        }

        /// <summary>A box decal of an image at a point on a surface, facing it, with the material's decal defaults</summary>
        public static ChiselDecalComponent CreateBoxAt(Transform parent, Material material, int tile, Vector3 center, Vector3 normal)
        {
            if (material == null || normal.sqrMagnitude < 1e-12f)
                return null;
            normal.Normalize();
            var up = Mathf.Abs(Vector3.Dot(normal, Vector3.up)) < 0.99f ? Vector3.up : Vector3.forward;

            var gameObject = new GameObject(ChiselDecalComponent.kNodeTypeName);
            gameObject.transform.SetPositionAndRotation(center, Quaternion.LookRotation(-normal, up));
            if (parent != null)
                gameObject.transform.SetParent(parent, true);
            var decal = gameObject.AddComponent<ChiselDecalComponent>();
            decal.Material  = material;
            decal.ImageTile = tile;
            var defaults = ChiselDecalComponent.DefaultsOf(material);
            // a box, whatever the material's shape is
            if (defaults.shape != ChiselDecalShape.Box)
                defaults = defaults.WithShape(ChiselDecalShape.Box);
            decal.Shape = ChiselDecalShape.Box;
            var settings = decal.Settings;
            settings.projection  = defaults.projection;
            settings.reach       = defaults.reach;
            settings.transparent = defaults.transparent;
            settings.size        = defaults.SizeFor(ChiselDecalComponent.ImageAspectOf(material, tile), settings.size.z);
            decal.Settings = settings;
            return decal;
        }

        /// <summary>A box decal on each selected surface, undoably</summary>
        public static List<ChiselDecalComponent> PlaceOn(IEnumerable<SurfaceReference> surfaces, Material material, int tile)
        {
            var placed  = new List<ChiselDecalComponent>();
            var outline = new List<Vector3>();
            Undo.SetCurrentGroupName("Place Decals");
            var group = Undo.GetCurrentGroup();
            foreach (var surface in new List<SurfaceReference>(surfaces))
            {
                if (surface?.node == null || !ChiselSurfaceShapes.SurfaceOutline(surface.node, surface.surfaceIndex, outline, out var plane))
                    continue;
                var decal = CreateBox(surface.node.transform, material, tile, outline, plane.normal);
                if (decal == null)
                    continue;
                Undo.RegisterCreatedObjectUndo(decal.gameObject, "Place Decals");
                placed.Add(decal);
            }
            Undo.CollapseUndoOperations(group);
            ChiselDecalManager.SetDirty();
            ChiselSceneMaterialUsage.SetDirty();
            return placed;
        }

        /// <summary>A trim decal of an image, to be put on an edge picked in the scene view</summary>
        public static ChiselDecalComponent CreateTrim(Material material, int tile)
        {
            var gameObject = new GameObject(ChiselDecalComponent.kNodeTypeName);
            var decal = gameObject.AddComponent<ChiselDecalComponent>();
            decal.Material  = material;
            decal.ImageTile = tile;
            decal.ApplyMaterialDefaults();
            decal.Shape = ChiselDecalShape.Trim;
            Undo.RegisterCreatedObjectUndo(gameObject, "Place Trim Decal");
            return decal;
        }
    }

    public sealed partial class ChiselSurfaceBrowserWindow
    {
        static readonly string[] kShapeFilters = { "All", "Box", "Trim", "Polygon" };
        static readonly string[] kBlendNames   = { "Transparent", "Opaque" };

        [SerializeField] int    decalShapeFilter;
        [SerializeField] string selectedDecal;

        // the decal images shown, grouped by material
        readonly List<List<ChiselSurfaceBrowserDecals.Image>> decalGroups = new();
        bool decalsDirty = true;
        Vector2 decalsGridScroll;

        void RefreshDecals()
        {
            if (!decalsDirty)
                return;
            decalsDirty = false;
            decalGroups.Clear();
            var search = ChiselSurfaceBrowserModel.ParseSearch(this.search);
            foreach (var item in model.Items)
            {
                if (!model.IsVisible(item))
                    continue;
                var material = Load(item);
                if (!ChiselSurfaceBrowserDecals.IsDecal(material))
                    continue;
                var shape = ChiselDecalComponent.DefaultsOf(material).shape;
                if (decalShapeFilter != 0 && (int)shape != decalShapeFilter - 1)
                    continue;
                var images = ChiselSurfaceBrowserDecals.ImagesOf(material, item.guid);
                // a search matches the material (its name and labels) or an image's name
                var materialMatches = ChiselSurfaceBrowserModel.Matches(item, search);
                if (!materialMatches)
                {
                    images.RemoveAll(image => !ChiselSurfaceBrowserModel.Matches(
                        new ChiselSurfaceBrowserModel.Item { name = image.name, path = item.path, labels = item.labels }, search));
                }
                if (images.Count > 0)
                    decalGroups.Add(images);
            }
            decalGroups.Sort((a, b) => string.Compare(a[0].material.name, b[0].material.name, System.StringComparison.OrdinalIgnoreCase));
        }

        ChiselSurfaceBrowserDecals.Image SelectedImage
        {
            get
            {
                foreach (var group in decalGroups)
                    foreach (var image in group)
                        if (image.Key == selectedDecal)
                            return image;
                return null;
            }
        }

        void DecalsGUI(Rect body, Rect footer)
        {
            RefreshDecals();
            DetailsSplitterGUI(body);
            var details = new Rect(body.xMax - detailsWidth, body.y, detailsWidth, body.height);
            var grid    = new Rect(body.x, body.y, details.x - body.x, body.height);
            DecalGridGUI(grid);
            DecalDetailsGUI(details);
            DecalFooterGUI(footer);
            EditorGUI.DrawRect(new Rect(details.x - 1, body.y, 1, body.height), new Color(0, 0, 0, 0.3f));
            EditorGUI.DrawRect(new Rect(0, footer.y, footer.width, 1), new Color(0, 0, 0, 0.3f));
        }

        #region Decal grid
        void DecalGridGUI(Rect rect)
        {
            // the shape chips filter by each material's default shape
            var chipsRect = new Rect(rect.x + 8, rect.y + 3, Mathf.Min(rect.width - 16, 280), kHeaderHeight - 4);
            EditorGUI.BeginChangeCheck();
            decalShapeFilter = GUI.Toolbar(chipsRect, decalShapeFilter, kShapeFilters, EditorStyles.miniButton);
            if (EditorGUI.EndChangeCheck())
                decalsDirty = true;

            var area = new Rect(rect.x, rect.y + kHeaderHeight + 2, rect.width, rect.height - kHeaderHeight - 2);
            var cell = thumbSize + 14;
            var cellHeight = thumbSize + 26;
            var columns = Mathf.Max(1, Mathf.FloorToInt((area.width - 24) / cell));
            var groupHeader = EditorGUIUtility.singleLineHeight + 6;

            var height = 4f;
            foreach (var group in decalGroups)
                height += groupHeader + Mathf.CeilToInt(group.Count / (float)columns) * cellHeight;
            var content = new Rect(0, 0, area.width - 16, height);
            decalsGridScroll = GUI.BeginScrollView(area, decalsGridScroll, content);
            if (decalGroups.Count == 0)
                GUI.Label(new Rect(0, 20, content.width, 40), "No decal materials. Tick \"Use as decal\" on a material in the Materials tab.", EditorStyles.centeredGreyMiniLabel);
            var y = 4f;
            foreach (var group in decalGroups)
            {
                var first  = group[0];
                var shape  = ChiselDecalComponent.DefaultsOf(first.material).shape;
                var header = new Rect(8, y, content.width - 16, groupHeader);
                // the material's name opens it in the Materials tab
                if (GUI.Button(new Rect(header.x, header.y + 2, header.width, EditorGUIUtility.singleLineHeight),
                               new GUIContent($"{first.material.name}  -  {shape}  -  {group.Count} image{(group.Count == 1 ? "" : "s")}", "Open in the Materials tab"),
                               EditorStyles.boldLabel))
                {
                    tab = 0;
                    Select(first.guid);
                    GUIUtility.ExitGUI();
                }
                y += groupHeader;
                for (int i = 0; i < group.Count; i++)
                {
                    var row = i / columns;
                    var column = i % columns;
                    DecalCardGUI(new Rect(8 + column * cell, y + row * cellHeight, cell - 6, cellHeight - 6), group[i]);
                }
                y += Mathf.CeilToInt(group.Count / (float)columns) * cellHeight;
            }
            GUI.EndScrollView();
        }

        Texture2D checker;

        // a transparency checkerboard under an image
        void DrawChecker(Rect rect)
        {
            if (checker == null)
            {
                checker = new Texture2D(2, 2) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Repeat, hideFlags = HideFlags.HideAndDontSave };
                var light = new Color(0.42f, 0.42f, 0.42f);
                var dark  = new Color(0.32f, 0.32f, 0.32f);
                checker.SetPixels(new[] { light, dark, dark, light });
                checker.Apply();
            }
            GUI.DrawTextureWithTexCoords(rect, checker, new Rect(0, 0, rect.width / 16, rect.height / 16));
        }

        void DestroyDecalResources()
        {
            if (checker) DestroyImmediate(checker);
        }

        // only the image's own tile, at its aspect, fitted into rect
        void DrawImage(Rect rect, ChiselSurfaceBrowserDecals.Image image)
        {
            var texture = image.material != null ? image.material.mainTexture : null;
            var aspect  = ChiselDecalComponent.ImageAspectOf(image.material, image.tile);
            var fitted  = rect;
            if (aspect > rect.width / rect.height)
            {
                fitted.height = rect.width / aspect;
                fitted.y += (rect.height - fitted.height) / 2;
            } else
            {
                fitted.width = rect.height * aspect;
                fitted.x += (rect.width - fitted.width) / 2;
            }
            DrawChecker(fitted);
            if (texture != null)
                GUI.DrawTextureWithTexCoords(fitted, texture, ChiselDecalComponent.ImageRectOf(image.material, image.tile), true);
        }

        void DecalCardGUI(Rect rect, ChiselSurfaceBrowserDecals.Image image)
        {
            var evt = Event.current;
            if (image.Key == selectedDecal)
                EditorGUI.DrawRect(rect, new Color(0.24f, 0.48f, 0.9f, 0.45f));
            var thumbRect = new Rect(rect.x + 4, rect.y + 4, thumbSize, thumbSize);
            if (evt.type == EventType.Repaint)
            {
                EditorGUI.DrawRect(thumbRect, new Color(0, 0, 0, 0.2f));
                DrawImage(thumbRect, image);
            }
            var uses = ChiselSceneMaterialUsage.DecalsUsing(image.material, image.tile);
            if (uses > 0)
            {
                var badge = new GUIStyle(EditorStyles.miniLabel) { alignment = TextAnchor.MiddleRight, normal = { textColor = Color.white } };
                GUI.Label(new Rect(thumbRect.xMax - 34, thumbRect.yMax - 16, 32, 14), new GUIContent(uses.ToString(), $"{uses} decals in the scene use it"), badge);
            }
            GUI.Label(new Rect(rect.x, thumbRect.yMax + 2, rect.width, EditorGUIUtility.singleLineHeight), new GUIContent(image.name, image.material.name),
                      new GUIStyle(EditorStyles.miniLabel) { alignment = TextAnchor.MiddleCenter, clipping = TextClipping.Clip });

            if (evt.type == EventType.MouseDown && evt.button == 0 && rect.Contains(evt.mousePosition))
            {
                selectedDecal = image.Key;
                if (evt.clickCount == 2)
                    PlaceSelectedDecal();
                evt.Use();
            } else if (evt.type == EventType.MouseDrag && evt.button == 0 && rect.Contains(evt.mousePosition))
            {
                // dropped in the scene view, it's placed there
                selectedDecal = image.Key;
                ChiselSurfaceBrowserDecals.StartDrag(image);
                evt.Use();
            }
        }
        #endregion

        #region Decal details
        Vector2 decalDetailsScroll;

        void DecalDetailsGUI(Rect rect)
        {
            var image = SelectedImage;
            GUILayout.BeginArea(new Rect(rect.x + 8, rect.y + 6, rect.width - 14, rect.height - 8));
            decalDetailsScroll = EditorGUILayout.BeginScrollView(decalDetailsScroll, false, false, GUIStyle.none, GUI.skin.verticalScrollbar, GUIStyle.none);
            var contentWidth = rect.width - 14 - 16;
            var oldLabelWidth = EditorGUIUtility.labelWidth;
            EditorGUIUtility.labelWidth = Mathf.Max(80, contentWidth * 0.4f);
            try
            {
                if (image == null)
                    GUILayout.Label("Select a decal image to see its details.", EditorStyles.centeredGreyMiniLabel);
                else
                    DecalDetailsContentGUI(image, contentWidth);
            }
            finally { EditorGUIUtility.labelWidth = oldLabelWidth; }
            EditorGUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        void DecalDetailsContentGUI(ChiselSurfaceBrowserDecals.Image image, float contentWidth)
        {
            var material = image.material;
            var previewRect = GUILayoutUtility.GetRect(contentWidth, previewHeight, GUILayout.Width(contentWidth), GUILayout.Height(previewHeight));
            if (Event.current.type == EventType.Repaint)
            {
                EditorGUI.DrawRect(previewRect, new Color(0.5f, 0.5f, 0.5f, 1));
                DrawImage(new Rect(previewRect.x + 8, previewRect.y + 8, previewRect.width - 16, previewRect.height - 16), image);
            }
            PreviewResizeGUI();

            GUILayout.Label(image.name, EditorStyles.boldLabel);
            GUILayout.Label($"Image {image.index} of {image.count} on {material.name}", EditorStyles.miniLabel);
            var path = AssetDatabase.GetAssetPath(material);
            GUILayout.Label(path, new GUIStyle(EditorStyles.miniLabel) { wordWrap = true });
            InfoRow("In scene", $"{ChiselSceneMaterialUsage.DecalsUsing(material, image.tile)} decals");

            var editable = IsEditable(path);
            if (!editable)
                EditorGUILayout.HelpBox("This material can't be changed: it is locked by version control or in a read-only package.", MessageType.Info);
            EditorGUI.BeginDisabledGroup(!editable);
            GUILayout.Space(8);
            LabelsGUI(material, contentWidth);

            GUILayout.Space(8);
            GUILayout.Label("DEFAULTS FOR NEW DECALS", EditorStyles.miniBoldLabel);
            var applies = new GUIStyle(EditorStyles.miniLabel) { wordWrap = true, normal = { textColor = new Color(1f, 0.75f, 0.3f) } };
            GUILayout.Label($"Belong to {material.name}: every image on it starts from them", applies);
            if (DecalDefaultsGUI(material, image))
                decalsDirty = true;     // the shape filter and group headers follow the defaults

            GUILayout.Space(8);
            if (GUILayout.Button(new GUIContent("Not a decal", "Removes the material from the Decals tab")))
            {
                var metadata = MetadataOf(material);
                Undo.RecordObject(metadata, "Not A Decal");
                var defaults = metadata.decalDefaults;
                defaults.isDecal = false;
                metadata.decalDefaults = defaults;
                EditorUtility.SetDirty(metadata);
                MarkDirty();
                GUIUtility.ExitGUI();
            }

            EditorGUI.EndDisabledGroup();
            GUILayout.Space(8);
            EditorGUILayout.HelpBox(PlacingHint(ChiselDecalComponent.DefaultsOf(material).shape), MessageType.None);
        }

        static string PlacingHint(ChiselDecalShape shape)
        {
            switch (shape)
            {
                case ChiselDecalShape.Trim:    return "Pick edge: hover an edge between two surfaces in the Scene view and click to place the trim along it. Escape cancels. Or drag the image onto an edge in the Scene view.";
                case ChiselDecalShape.Polygon: return "Draw on surface: click the corners on a surface, then click the first corner again or press Enter to close it. Backspace takes back a corner, Escape cancels.";
                default:                       return "Place on the selected surfaces: one decal on each, centered on it and facing it. Double-click an image to place it, or drag it onto a surface in the Scene view.";
            }
        }

        // every row is always shown; the ones that don't apply to the shape are disabled with a reason
        static bool DecalDefaultsGUI(Material material, ChiselSurfaceBrowserDecals.Image image)
        {
            var metadata = MetadataOf(material);
            if (metadata == null)
                return false;
            var defaults = metadata.decalDefaults.Valid();
            var changed  = defaults;
            EditorGUI.BeginChangeCheck();

            var shape = (ChiselDecalShape)EditorGUILayout.EnumPopup("Shape", defaults.shape);
            if (shape != defaults.shape)
                changed = changed.WithShape(shape);

            var trim    = changed.shape == ChiselDecalShape.Trim;
            var polygon = changed.shape == ChiselDecalShape.Polygon;
            using (new EditorGUI.DisabledScope(trim || polygon))
            {
                var label = new GUIContent("Projection", trim ? "Not used by Trim" : polygon ? "A polygon always uses Polygon" : null);
                var projection = (ChiselDecalProjection)EditorGUILayout.EnumPopup(label, changed.projection,
                    value => ChiselDecalDefaults.AllowsProjection(changed.shape, (ChiselDecalProjection)value), false);
                if (!trim && !polygon)
                    changed.projection = projection;
            }
            if (trim)        DisabledReason("Not used by Trim");
            else if (polygon) DisabledReason("Locked to Polygon");

            var omni = changed.shape == ChiselDecalShape.Box && changed.projection == ChiselDecalProjection.Omni;
            using (new EditorGUI.DisabledScope(!omni))
                changed.reach = (ChiselDecalReach)EditorGUILayout.EnumPopup("Reach", changed.reach);
            if (!omni) DisabledReason("Only for Box with Omni");

            changed.transparent = EditorGUILayout.Popup("Blend", changed.transparent ? 0 : 1, kBlendNames) == 0;

            using (new EditorGUI.DisabledScope(!trim))
                changed.trimFill = (ChiselTrimFill)EditorGUILayout.EnumPopup("Trim fill", changed.trimFill);
            if (!trim) DisabledReason("Only for Trim");

            changed.height = EditorGUILayout.FloatField(new GUIContent(polygon ? "Depth" : "Height", polygon ? "How deep a drawn outline reaches" : "The image's height; its width follows its aspect"), changed.height);
            if (!polygon)
            {
                var size = changed.Valid().SizeFor(ChiselDecalComponent.ImageAspectOf(material, image.tile), 0);
                EditorGUILayout.LabelField(trim ? "Repeat length" : "Size", trim ? $"{size.x:0.###} along the edge, {size.y:0.###} across" : $"{size.x:0.###} x {size.y:0.###}");
            }

            if (!EditorGUI.EndChangeCheck())
                return false;
            changed = changed.Valid();
            if (changed.Equals(metadata.decalDefaults))
                return false;
            Undo.RecordObject(metadata, "Change Decal Defaults");
            metadata.decalDefaults = changed;
            EditorUtility.SetDirty(metadata);
            return true;
        }

        static void DisabledReason(string reason)
        {
            using (new EditorGUI.IndentLevelScope(1))
                EditorGUILayout.LabelField(reason, EditorStyles.centeredGreyMiniLabel);
        }
        #endregion

        #region Decal footer and placing
        void DecalFooterGUI(Rect rect)
        {
            GUILayout.BeginArea(new Rect(rect.x + 8, rect.y + 4, rect.width - 16, rect.height - 6));
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Label("Selection  " + SelectionText(), EditorStyles.miniLabel);
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("Close", GUILayout.Width(60)))
                    Close();
                var image = SelectedImage;
                var shape = image != null ? ChiselDecalComponent.DefaultsOf(image.material).shape : ChiselDecalShape.Box;
                var count = ChiselSurfaceSelectionManager.Selection.Count;
                string action;
                switch (shape)
                {
                    case ChiselDecalShape.Trim:    action = "Pick edge in Scene view"; break;
                    case ChiselDecalShape.Polygon: action = "Draw on surface"; break;
                    default:                       action = $"Place on {count} surface{(count == 1 ? "" : "s")}"; break;
                }
                using (new EditorGUI.DisabledScope(image == null || (shape == ChiselDecalShape.Box && count == 0)))
                {
                    if (GUILayout.Button(action, GUILayout.Width(170)))
                        PlaceSelectedDecal();
                }
            }
            GUILayout.EndArea();
        }

        void PlaceSelectedDecal()
        {
            var image = SelectedImage;
            if (image == null)
                return;
            ChiselSurfaceBrowserPrefs.MarkUsed(image.Key);
            switch (ChiselDecalComponent.DefaultsOf(image.material).shape)
            {
                case ChiselDecalShape.Trim:
                    ChiselDecalEditor.StartPickingEdge(ChiselSurfaceBrowserDecals.CreateTrim(image.material, image.tile), removeIfCancelled: true);
                    break;
                case ChiselDecalShape.Polygon:
                    ChiselSurfaceShapeTool.StartDrawing(image.material, image.tile);
                    break;
                default:
                    var placed = ChiselSurfaceBrowserDecals.PlaceOn(ChiselSurfaceSelectionManager.Selection, image.material, image.tile);
                    if (placed.Count == 0)
                        ShowNotification(new GUIContent("Select surfaces to place the decal on"));
                    break;
            }
            decalsDirty = true;
        }
        #endregion
    }
}
