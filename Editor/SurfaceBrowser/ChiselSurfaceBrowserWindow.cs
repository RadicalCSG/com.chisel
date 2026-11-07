using System;
using System.Collections.Generic;
using Chisel.Components;
using Chisel.Core;
using UnityEditor;
using UnityEngine;

namespace Chisel.Editors
{
    // A window to find materials and apply them to the selected surfaces: quick access, folders (some hidden) and labels
    // on the left, the grid in the middle, the selected material's details on the right
#if UNITY_6000_7_OR_NEWER
    [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
#endif
    public sealed class ChiselSurfaceBrowserWindow : EditorWindow
    {
        const float kSidebarWidth = 220;
        const float kDetailsWidth = 300;
        const float kFooterHeight = 28;
        const float kHeaderHeight = 22;
        const float kMinThumb     = 48;
        const float kMaxThumb     = 160;

        static readonly string[] kTabs = { "Materials", "Decals" };
        static readonly string[] kSortNames = { "Name", "Folder", "Use in scene" };

        // per user, between sessions
        [SerializeField] int     tab;
        [SerializeField] float   thumbSize = 88;
        [SerializeField] int     sort;
        [SerializeField] List<string> expandedFolders = new() { "Assets" };
        [SerializeField] string  search = string.Empty;
        [SerializeField] int     quick;
        [SerializeField] string  folder;
        [SerializeField] string  label;
        [SerializeField] string  selectedGuid;

        readonly ChiselSurfaceBrowserModel model = new();
        List<ChiselSurfaceBrowserModel.Item> shown = new();
        List<ChiselSurfaceBrowserModel.Folder> folders = new();
        List<KeyValuePair<string, int>> labels = new();
        bool dirty = true;

        Vector2 sidebarScroll, gridScroll, detailsScroll;
        string newLabel = string.Empty;
        Editor metadataEditor;
        Editor previewEditor;

        [MenuItem("Window/Chisel/Surface Browser")]
        public static ChiselSurfaceBrowserWindow Open()
        {
            var window = GetWindow<ChiselSurfaceBrowserWindow>();
            window.titleContent = new GUIContent("Surface Browser");
            window.minSize = new Vector2(kSidebarWidth + kDetailsWidth + 260, 360);
            window.Show();
            return window;
        }

        /// <summary>Opens the browser with a material selected</summary>
        public static void Open(Material material)
        {
            var window = Open();
            window.Select(ChiselSurfaceBrowserPrefs.ItemOf(material));
        }

        void OnEnable()
        {
            model.isFavorite  = item => ChiselSurfaceBrowserPrefs.IsFavorite(item.guid);
            model.recent      = () => ChiselSurfaceBrowserPrefs.Recent;
            model.isInScene   = item => ChiselSceneMaterialUsage.SurfacesUsing(Load(item)) > 0;
            model.hasHotspots = item => HotspotLayoutOf(Load(item)) != null;
            EditorApplication.projectChanged += MarkDirty;
            Selection.selectionChanged       += Repaint;
            Undo.undoRedoPerformed           += MarkDirty;
            dirty = true;
        }

        void OnDisable()
        {
            EditorApplication.projectChanged -= MarkDirty;
            Selection.selectionChanged       -= Repaint;
            Undo.undoRedoPerformed           -= MarkDirty;
            if (metadataEditor) DestroyImmediate(metadataEditor);
            if (previewEditor)  DestroyImmediate(previewEditor);
        }

        void MarkDirty() { dirty = true; Repaint(); }

        // hidden folders are changed in the project settings
        void OnFocus() { MarkDirty(); }

        // repaints now and then, so the selection and scene counts stay current
        void OnInspectorUpdate() { Repaint(); }

        static Material Load(ChiselSurfaceBrowserModel.Item item)
        {
            return item != null ? AssetDatabase.LoadAssetAtPath<Material>(item.path) : null;
        }

        static ChiselSurfaceMetadata MetadataOf(Material material)
        {
            return material != null ? material.GetMetadataOfType<ChiselSurfaceMetadata>() : null;
        }

        static ChiselHotspotLayout HotspotLayoutOf(Material material) { return MetadataOf(material)?.hotspotLayout; }

        void Refresh()
        {
            if (!dirty)
                return;
            dirty = false;
            var items = new List<ChiselSurfaceBrowserModel.Item>();
            foreach (var guid in AssetDatabase.FindAssets("t:Material", new[] { "Assets", "Packages" }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (!path.EndsWith(".mat", StringComparison.OrdinalIgnoreCase))
                    continue;
                var asset = AssetDatabase.LoadMainAssetAtPath(path);
                items.Add(new ChiselSurfaceBrowserModel.Item
                {
                    guid   = guid,
                    path   = path,
                    name   = System.IO.Path.GetFileNameWithoutExtension(path),
                    labels = asset != null ? AssetDatabase.GetLabels(asset) : Array.Empty<string>()
                });
            }
            model.SetItems(items);
            folders = model.BuildFolders(includeHidden: false);
            labels  = model.Labels();
            Filter();
        }

        void Filter()
        {
            shown = model.Filter(search, (ChiselSurfaceBrowserModel.QuickAccess)quick, folder, label,
                                 (ChiselSurfaceBrowserModel.Sort)sort, item => ChiselSceneMaterialUsage.SurfacesUsing(Load(item)));
        }

        void Select(string guid)
        {
            selectedGuid = guid;
            newLabel = string.Empty;
            if (metadataEditor) DestroyImmediate(metadataEditor);
            if (previewEditor)  DestroyImmediate(previewEditor);
            Repaint();
        }

        ChiselSurfaceBrowserModel.Item Selected
        {
            get
            {
                if (string.IsNullOrEmpty(selectedGuid))
                    return null;
                foreach (var item in model.Items)
                {
                    if (item.guid == selectedGuid)
                        return item;
                }
                return null;
            }
        }

        void OnGUI()
        {
            Refresh();
            var full = new Rect(0, 0, position.width, position.height);
            var toolbar = new Rect(0, 0, full.width, EditorGUIUtility.singleLineHeight + 6);
            var footer  = new Rect(0, full.height - kFooterHeight, full.width, kFooterHeight);
            var body    = new Rect(0, toolbar.yMax, full.width, footer.y - toolbar.yMax);

            ToolbarGUI(toolbar);
            if (tab != 0)
            {
                // the Decals tab comes with placing decals from here
                GUI.Label(body, "Decals are listed here once decal browsing is built.", EditorStyles.centeredGreyMiniLabel);
                return;
            }
            var sidebar = new Rect(body.x, body.y, kSidebarWidth, body.height);
            var details = new Rect(body.xMax - kDetailsWidth, body.y, kDetailsWidth, body.height);
            var grid    = new Rect(sidebar.xMax, body.y, details.x - sidebar.xMax, body.height);

            SidebarGUI(sidebar);
            GridGUI(grid);
            DetailsGUI(details);
            FooterGUI(footer);

            EditorGUI.DrawRect(new Rect(sidebar.xMax, body.y, 1, body.height), new Color(0, 0, 0, 0.3f));
            EditorGUI.DrawRect(new Rect(details.x - 1, body.y, 1, body.height), new Color(0, 0, 0, 0.3f));
            EditorGUI.DrawRect(new Rect(0, footer.y, full.width, 1), new Color(0, 0, 0, 0.3f));
        }

        #region Toolbar
        void ToolbarGUI(Rect rect)
        {
            GUILayout.BeginArea(rect, EditorStyles.toolbar);
            using (new EditorGUILayout.HorizontalScope())
            {
                tab = GUILayout.Toolbar(tab, kTabs, EditorStyles.toolbarButton, GUILayout.Width(160));
                GUILayout.Space(8);
                EditorGUI.BeginChangeCheck();
                search = EditorGUILayout.TextField(search, EditorStyles.toolbarSearchField, GUILayout.MinWidth(120), GUILayout.MaxWidth(360));
                if (EditorGUI.EndChangeCheck())
                    Filter();
                GUILayout.Label(new GUIContent("l:label", "Type l: and a label to find the materials with it"), EditorStyles.miniLabel, GUILayout.ExpandWidth(false));
                GUILayout.FlexibleSpace();
                thumbSize = GUILayout.HorizontalSlider(thumbSize, kMinThumb, kMaxThumb, GUILayout.Width(100));
                GUILayout.Space(6);
                EditorGUI.BeginChangeCheck();
                sort = EditorGUILayout.Popup(sort, kSortNames, EditorStyles.toolbarPopup, GUILayout.Width(100));
                if (EditorGUI.EndChangeCheck())
                    Filter();
            }
            GUILayout.EndArea();
        }
        #endregion

        #region Sidebar
        static readonly string[] kQuickNames = { "All materials", "Favorites", "Recently used", "In this scene", "With hotspots" };

        bool Row(string text, int count, bool selected, int depth = 0, bool greyed = false)
        {
            var rect = GUILayoutUtility.GetRect(new GUIContent(text), EditorStyles.label, GUILayout.ExpandWidth(true));
            if (selected)
                EditorGUI.DrawRect(rect, new Color(0.24f, 0.48f, 0.9f, 0.5f));
            var textRect = new Rect(rect.x + 6 + depth * 14, rect.y, rect.width - 50 - depth * 14, rect.height);
            var style = greyed ? EditorStyles.miniLabel : EditorStyles.label;
            var prev = GUI.color;
            if (greyed) GUI.color = new Color(prev.r, prev.g, prev.b, 0.5f);
            GUI.Label(textRect, greyed ? "<i>" + text + "</i>" : text, new GUIStyle(style) { richText = true });
            if (count >= 0)
                GUI.Label(new Rect(rect.xMax - 44, rect.y, 40, rect.height), count.ToString(), new GUIStyle(EditorStyles.miniLabel) { alignment = TextAnchor.MiddleRight });
            GUI.color = prev;
            return Event.current.type == EventType.MouseDown && Event.current.button == 0 && rect.Contains(Event.current.mousePosition);
        }

        void SidebarGUI(Rect rect)
        {
            GUILayout.BeginArea(rect);
            sidebarScroll = EditorGUILayout.BeginScrollView(sidebarScroll);

            GUILayout.Label("QUICK ACCESS", EditorStyles.miniBoldLabel);
            for (int i = 0; i < kQuickNames.Length; i++)
            {
                if (Row(kQuickNames[i], model.Count((ChiselSurfaceBrowserModel.QuickAccess)i), quick == i && folder == null && label == null))
                {
                    quick = i; folder = null; label = null;
                    Filter();
                    Event.current.Use();
                }
            }

            GUILayout.Space(6);
            GUILayout.Label("FOLDERS", EditorStyles.miniBoldLabel);
            foreach (var root in folders)
                FolderGUI(root);
            var hiddenCount = ChiselProjectSettings.HiddenFolders.Count;
            if (hiddenCount > 0 &&
                GUILayout.Button(new GUIContent($"{hiddenCount} hidden folder{(hiddenCount == 1 ? "" : "s")}", "Hidden in Project Settings > Chisel > Surface Browser"), EditorStyles.linkLabel))
                SettingsService.OpenProjectSettings(ChiselProjectSettingsEditor.kSurfaceBrowserPath);

            GUILayout.Space(6);
            GUILayout.Label("LABELS", EditorStyles.miniBoldLabel);
            foreach (var pair in labels)
            {
                if (Row(pair.Key, pair.Value, label == pair.Key))
                {
                    label = label == pair.Key ? null : pair.Key;
                    quick = 0;
                    Filter();
                    Event.current.Use();
                }
            }
            EditorGUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        void FolderGUI(ChiselSurfaceBrowserModel.Folder node)
        {
            var expanded = expandedFolders.Contains(node.path);
            var rect = GUILayoutUtility.GetRect(new GUIContent(node.name), EditorStyles.label, GUILayout.ExpandWidth(true));
            var evt = Event.current;
            var hovering = rect.Contains(evt.mousePosition);
            if (folder == node.path)
                EditorGUI.DrawRect(rect, new Color(0.24f, 0.48f, 0.9f, 0.5f));

            var indent = node.depth * 14;
            var foldRect = new Rect(rect.x + 2 + indent, rect.y, 14, rect.height);
            if (node.children.Count > 0)
            {
                EditorGUI.BeginChangeCheck();
                expanded = EditorGUI.Foldout(foldRect, expanded, GUIContent.none, true);
                if (EditorGUI.EndChangeCheck())
                {
                    if (expanded) expandedFolders.Add(node.path); else expandedFolders.Remove(node.path);
                }
            }
            GUI.Label(new Rect(foldRect.xMax, rect.y, rect.width - foldRect.xMax - 44, rect.height), new GUIContent(node.name, node.path));
            GUI.Label(new Rect(rect.xMax - 44, rect.y, 40, rect.height), node.count.ToString(), new GUIStyle(EditorStyles.miniLabel) { alignment = TextAnchor.MiddleRight });

            if (evt.type == EventType.MouseDown && evt.button == 0 && hovering && !foldRect.Contains(evt.mousePosition))
            {
                folder = node.path; quick = 0; label = null;
                Filter();
                evt.Use();
            }
            if (hovering && evt.type == EventType.MouseMove)
                Repaint();

            if (!expanded)
                return;
            foreach (var child in node.children)
                FolderGUI(child);
        }

        #endregion

        #region Grid
        string HeaderText()
        {
            var what = folder ?? (label != null ? "Label: " + label : kQuickNames[quick]);
            return $"{what}  ·  {shown.Count} material{(shown.Count == 1 ? "" : "s")}";
        }

        void GridGUI(Rect rect)
        {
            GUI.Label(new Rect(rect.x + 8, rect.y + 2, rect.width - 16, kHeaderHeight), HeaderText(), EditorStyles.boldLabel);
            var area = new Rect(rect.x, rect.y + kHeaderHeight + 2, rect.width, rect.height - kHeaderHeight - 2);
            var cell = thumbSize + 12;
            var cellHeight = thumbSize + EditorGUIUtility.singleLineHeight + 14;
            var columns = Mathf.Max(1, Mathf.FloorToInt((area.width - 16) / cell));
            var rows = Mathf.CeilToInt(shown.Count / (float)columns);
            var content = new Rect(0, 0, area.width - 16, rows * cellHeight + 8);
            gridScroll = GUI.BeginScrollView(area, gridScroll, content);
            // only the rows in view are drawn
            var firstRow = Mathf.Max(0, Mathf.FloorToInt(gridScroll.y / cellHeight));
            var lastRow  = Mathf.Min(rows - 1, Mathf.CeilToInt((gridScroll.y + area.height) / cellHeight));
            for (int row = firstRow; row <= lastRow; row++)
            {
                for (int column = 0; column < columns; column++)
                {
                    var index = row * columns + column;
                    if (index >= shown.Count)
                        break;
                    var cellRect = new Rect(8 + column * cell, 4 + row * cellHeight, cell - 6, cellHeight - 6);
                    CardGUI(cellRect, shown[index]);
                }
            }
            GUI.EndScrollView();
            KeyboardGUI(columns);
        }

        static Texture PreviewOf(Material material, bool flat)
        {
            if (material == null)
                return null;
            if (flat && material.mainTexture != null)
                return material.mainTexture;
            var preview = AssetPreview.GetAssetPreview(material);
            return preview != null ? preview : AssetPreview.GetMiniThumbnail(material);
        }

        // hotspot and decal materials are sheets: shown flat
        static bool IsSheet(Material material)
        {
            var metadata = MetadataOf(material);
            return metadata != null && (metadata.hotspotLayout != null || metadata.decalDefaults.isDecal);
        }

        void CardGUI(Rect rect, ChiselSurfaceBrowserModel.Item item)
        {
            var evt = Event.current;
            var selected = item.guid == selectedGuid;
            if (selected)
                EditorGUI.DrawRect(rect, new Color(0.24f, 0.48f, 0.9f, 0.45f));
            var thumbRect = new Rect(rect.x + 4, rect.y + 4, thumbSize, thumbSize);
            var material = Load(item);
            if (evt.type == EventType.Repaint)
            {
                EditorGUI.DrawRect(thumbRect, new Color(0, 0, 0, 0.2f));
                var texture = PreviewOf(material, IsSheet(material));
                if (texture != null)
                    GUI.DrawTexture(thumbRect, texture, ScaleMode.ScaleToFit);
                else
                    Repaint();  // previews load in the background
            }

            // indicators: a hotspot layout, a decal material, a favorite, its use in the scene
            var metadata = MetadataOf(material);
            var badge = new GUIStyle(EditorStyles.miniLabel) { normal = { textColor = Color.white } };
            if (metadata != null && metadata.hotspotLayout != null)
                GUI.Label(new Rect(thumbRect.x + 2, thumbRect.y + 2, 30, 14), new GUIContent("HS", "Has a hotspot layout"), badge);
            else if (metadata != null && metadata.decalDefaults.isDecal)
                GUI.Label(new Rect(thumbRect.x + 2, thumbRect.y + 2, 40, 14), new GUIContent("Decal", "A decal material"), badge);
            var favorite = ChiselSurfaceBrowserPrefs.IsFavorite(item.guid);
            var starRect = new Rect(thumbRect.xMax - 18, thumbRect.y + 2, 16, 16);
            if (GUI.Button(starRect, new GUIContent(favorite ? "★" : "☆", favorite ? "Remove from favorites" : "Add to favorites"), badge))
            {
                ChiselSurfaceBrowserPrefs.SetFavorite(item.guid, !favorite);
                Filter();
                evt.Use();
            }
            var uses = ChiselSceneMaterialUsage.SurfacesUsing(material);
            if (uses > 0)
                GUI.Label(new Rect(thumbRect.xMax - 34, thumbRect.yMax - 16, 32, 14), new GUIContent(uses.ToString(), $"{uses} surfaces in the scene use it"), new GUIStyle(badge) { alignment = TextAnchor.MiddleRight });

            GUI.Label(new Rect(rect.x, thumbRect.yMax + 2, rect.width, EditorGUIUtility.singleLineHeight), new GUIContent(item.name, item.path),
                      new GUIStyle(EditorStyles.miniLabel) { alignment = TextAnchor.MiddleCenter, clipping = TextClipping.Clip });

            if (!rect.Contains(evt.mousePosition))
                return;
            switch (evt.type)
            {
                case EventType.MouseDown when evt.button == 0:
                    Select(item.guid);
                    if (evt.clickCount == 2)
                        Apply(material);
                    GUIUtility.hotControl = 0;
                    evt.Use();
                    break;
                case EventType.MouseDrag when evt.button == 0 && material != null:
                    // dropped on a surface in the scene view, it's applied there
                    DragAndDrop.PrepareStartDrag();
                    DragAndDrop.objectReferences = new UnityEngine.Object[] { material };
                    DragAndDrop.StartDrag(item.name);
                    ChiselSurfaceBrowserPrefs.MarkUsed(item.guid);
                    evt.Use();
                    break;
            }
        }

        void KeyboardGUI(int columns)
        {
            var evt = Event.current;
            if (evt.type != EventType.KeyDown || shown.Count == 0 || EditorGUIUtility.editingTextField)
                return;
            var index = shown.FindIndex(item => item.guid == selectedGuid);
            var next = index;
            switch (evt.keyCode)
            {
                case KeyCode.RightArrow: next = index + 1; break;
                case KeyCode.LeftArrow:  next = index - 1; break;
                case KeyCode.DownArrow:  next = index + columns; break;
                case KeyCode.UpArrow:    next = index - columns; break;
                case KeyCode.Return:
                case KeyCode.KeypadEnter:
                    if (index >= 0) Apply(Load(shown[index]));
                    evt.Use();
                    return;
                default: return;
            }
            next = Mathf.Clamp(next < 0 && index < 0 ? 0 : next, 0, shown.Count - 1);
            Select(shown[next].guid);
            evt.Use();
        }
        #endregion

        #region Details
        void DetailsGUI(Rect rect)
        {
            var item = Selected;
            var material = Load(item);
            GUILayout.BeginArea(new Rect(rect.x + 8, rect.y + 6, rect.width - 14, rect.height - 8));
            detailsScroll = EditorGUILayout.BeginScrollView(detailsScroll);
            if (material == null)
            {
                GUILayout.Label("Select a material to see its details.", EditorStyles.centeredGreyMiniLabel);
                EditorGUILayout.EndScrollView();
                GUILayout.EndArea();
                return;
            }

            // a large preview; Unity's own material preview, with its sphere, cube, plane and other shapes
            Editor.CreateCachedEditor(material, null, ref previewEditor);
            if (previewEditor != null && previewEditor.HasPreviewGUI())
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.FlexibleSpace();
                    previewEditor.OnPreviewSettings();
                }
                var previewRect = GUILayoutUtility.GetRect(rect.width - 30, 180);
                previewEditor.OnInteractivePreviewGUI(previewRect, EditorStyles.helpBox);
            }

            GUILayout.Label(item.name, EditorStyles.boldLabel);
            GUILayout.Label(item.path, new GUIStyle(EditorStyles.miniLabel) { wordWrap = true });
            var metadata = MetadataOf(material);
            InfoRow("Shader", material.shader != null ? material.shader.name : "None");
            if (material.mainTexture != null)
                InfoRow("Texture", $"{material.mainTexture.width} × {material.mainTexture.height}");
            if (metadata != null && metadata.hotspotLayout != null)
                InfoRow("Hotspots", 1 + (metadata.hotspotVariants?.Count ?? 0) + (metadata.hotspotSheets?.Count ?? 0) + " sheets (layout, variants and other sheets)");
            var uses = ChiselSceneMaterialUsage.SurfacesUsing(material);
            InfoRow("In scene", uses == 1 ? "1 surface" : uses + " surfaces");

            if (metadata != null && metadata.hotspotLayout != null)
                HotspotBoxGUI(material, metadata);

            GUILayout.Space(8);
            LabelsGUI(material);

            GUILayout.Space(8);
            GUILayout.Label("MATERIAL SETTINGS", EditorStyles.miniBoldLabel);
            if (metadata == null)
            {
                EditorGUILayout.HelpBox("This material has no Chisel settings yet; surfaces using it take Chisel's defaults.", MessageType.None);
                if (GUILayout.Button("Add Chisel settings"))
                {
                    Undo.RecordObject(material, "Add Chisel Settings");
                    material.AddMetadataOfType<ChiselSurfaceMetadata>();
                    EditorUtility.SetDirty(material);
                    AssetDatabase.SaveAssetIfDirty(material);
                    Select(item.guid);
                    GUIUtility.ExitGUI();
                }
            } else
            {
                var applies = new GUIStyle(EditorStyles.miniLabel) { wordWrap = true, normal = { textColor = new Color(1f, 0.75f, 0.3f) } };
                GUILayout.Label(uses > 0 ? $"Applies to all {uses} surface{(uses == 1 ? "" : "s")} using this material" : "Applies to every surface using this material", applies);
                Editor.CreateCachedEditor(metadata, typeof(ChiselSurfaceMetadataEditor), ref metadataEditor);
                metadataEditor?.OnInspectorGUI();

                EditorGUI.BeginChangeCheck();
                var isDecal = EditorGUILayout.ToggleLeft(new GUIContent("Use as decal", "Lists the material in the Decals tab, to place as decals"), metadata.decalDefaults.isDecal);
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(metadata, isDecal ? "Use As Decal" : "Not A Decal");
                    var defaults = metadata.decalDefaults;
                    defaults.isDecal = isDecal;
                    metadata.decalDefaults = defaults;
                    EditorUtility.SetDirty(metadata);
                }
            }

            GUILayout.Space(8);
            if (GUILayout.Button("Set as default for…", EditorStyles.popup))
                DefaultsMenu(material).ShowAsContext();
            if (GUILayout.Button("Select surfaces using this material"))
                SelectSurfacesUsing(material);
            if (GUILayout.Button("Ping in Project"))
                EditorGUIUtility.PingObject(material);

            EditorGUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        static void InfoRow(string name, string value)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Label(name, EditorStyles.miniLabel, GUILayout.Width(70));
                GUILayout.Label(value, new GUIStyle(EditorStyles.miniLabel) { wordWrap = true });
            }
        }

        // The tile choice of the selected surfaces using this hotspot material, and the way to its layout
        void HotspotBoxGUI(Material material, ChiselSurfaceMetadata metadata)
        {
            int using_ = 0, pinned = 0;
            foreach (var surface in ChiselSurfaceSelectionManager.Selection)
            {
                if (surface.RenderMaterial != material)
                    continue;
                using_++;
                var details = surface.Surface?.surfaceDetails;
                if (details.HasValue && details.Value.hotspot.pinnedTile != 0)
                    pinned++;
            }
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                var choice = using_ == 0 ? "-" : pinned == 0 ? "Automatic" : pinned == using_ ? "Pinned" : "Mixed";
                InfoRow("Hotspot tile", choice);
                var count = ChiselSurfaceSelectionManager.Selection.Count;
                GUILayout.Label($"{using_} of the {count} selected surface{(count == 1 ? "" : "s")} use it", EditorStyles.miniLabel);
                if (GUILayout.Button("Edit layout…"))
                    ChiselHotspotLayoutWindow.Open(metadata.hotspotLayout, material);
            }
        }

        // Its labels as chips; Enter adds the typed one, Backspace in the empty field removes the last
        void LabelsGUI(Material material)
        {
            var current = ChiselAssetLabels.Get(material);
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Label("Labels", EditorStyles.miniBoldLabel);
                GUILayout.FlexibleSpace();
                GUILayout.Label("Enter adds · Backspace removes last", EditorStyles.miniLabel);
            }
            Chips(current, label => { ChiselAssetLabels.Remove(material, label); MarkDirty(); }, "×");

            const string kControl = "ChiselNewLabel";
            var evt = Event.current;
            if (evt.type == EventType.KeyDown && GUI.GetNameOfFocusedControl() == kControl)
            {
                if ((evt.keyCode == KeyCode.Return || evt.keyCode == KeyCode.KeypadEnter) && newLabel.Trim().Length > 0)
                {
                    ChiselAssetLabels.Add(material, newLabel, model.ExistingLabelFor(newLabel.Trim()));
                    newLabel = string.Empty;
                    MarkDirty();
                    evt.Use();
                } else if (evt.keyCode == KeyCode.Backspace && newLabel.Length == 0 && current.Length > 0)
                {
                    ChiselAssetLabels.Remove(material, current[^1]);
                    MarkDirty();
                    evt.Use();
                }
            }
            GUI.SetNextControlName(kControl);
            newLabel = EditorGUILayout.TextField(newLabel, EditorStyles.toolbarSearchField);

            // the project's labels it doesn't have, filtered by what's typed
            var suggestions = new List<string>();
            foreach (var pair in labels)
            {
                if (Array.Exists(current, l => string.Equals(l, pair.Key, StringComparison.OrdinalIgnoreCase)))
                    continue;
                if (newLabel.Length > 0 && pair.Key.IndexOf(newLabel, StringComparison.OrdinalIgnoreCase) < 0)
                    continue;
                suggestions.Add(pair.Key);
            }
            Chips(suggestions.ToArray(), label => { ChiselAssetLabels.Add(material, label); newLabel = string.Empty; MarkDirty(); }, "+", prefix: true);
        }

        static void Chips(string[] items, Action<string> clicked, string mark, bool prefix = false)
        {
            if (items.Length == 0)
                return;
            var style = new GUIStyle(EditorStyles.miniButton) { stretchWidth = false };
            var width = 0f;
            var maxWidth = kDetailsWidth - 30;
            EditorGUILayout.BeginHorizontal();
            foreach (var item in items)
            {
                var text = prefix ? mark + " " + item : item + " " + mark;
                var size = style.CalcSize(new GUIContent(text)).x + 4;
                if (width + size > maxWidth && width > 0)
                {
                    EditorGUILayout.EndHorizontal();
                    EditorGUILayout.BeginHorizontal();
                    width = 0;
                }
                width += size;
                if (GUILayout.Button(text, style))
                {
                    clicked(item);
                    GUIUtility.ExitGUI();
                }
            }
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
        }

        // Chisel's default parts: each with its current default, ticked where it's this material
        static readonly (string name, Func<Material> get, Action<Material> set)[] kParts =
        {
            ("Wall",         () => ChiselProjectSettings.DefaultWallMaterial,        m => ChiselProjectSettings.DefaultWallMaterial = m),
            ("Floor",        () => ChiselProjectSettings.DefaultFloorMaterial,       m => ChiselProjectSettings.DefaultFloorMaterial = m),
            ("Tread",        () => ChiselProjectSettings.DefaultTreadMaterial,       m => ChiselProjectSettings.DefaultTreadMaterial = m),
            ("Riser",        () => ChiselProjectSettings.DefaultRiserMaterial,       m => ChiselProjectSettings.DefaultRiserMaterial = m),
            ("Step nose",    () => ChiselProjectSettings.DefaultStepNoseMaterial,    m => ChiselProjectSettings.DefaultStepNoseMaterial = m),
            ("Step sides",   () => ChiselProjectSettings.DefaultStepSidesMaterial,   m => ChiselProjectSettings.DefaultStepSidesMaterial = m),
            ("Tread bottom", () => ChiselProjectSettings.DefaultTreadBottomMaterial, m => ChiselProjectSettings.DefaultTreadBottomMaterial = m),
            ("Column",       () => ChiselProjectSettings.DefaultColumnMaterial,      m => ChiselProjectSettings.DefaultColumnMaterial = m),
        };

        static GenericMenu DefaultsMenu(Material material)
        {
            var menu = new GenericMenu();
            foreach (var part in kParts)
            {
                var current = part.get();
                var text = $"{part.name}\t({(current != null ? current.name : "none")})";
                var set = part.set;
                menu.AddItem(new GUIContent(text), current == material, () =>
                {
                    Undo.RecordObject(ChiselProjectSettings.instance, "Set Default Material");
                    set(material);
                    EditorUtility.SetDirty(ChiselProjectSettings.instance);
                    ChiselProjectSettings.instance.Save();
                });
            }
            return menu;
        }

        static void SelectSurfacesUsing(Material material)
        {
            var found = new HashSet<SurfaceReference>();
            var surfaces = new List<SurfaceReference>();
            foreach (var generator in FindObjectsByType<ChiselGeneratorComponent>(FindObjectsInactive.Exclude))
            {
                surfaces.Clear();
                if (!ChiselSurfaceSelectionManager.GetAllSurfaceReferences(generator, surfaces))
                    continue;
                foreach (var surface in surfaces)
                {
                    if (surface.RenderMaterial == material)
                        found.Add(surface);
                }
            }
            ChiselSurfaceSelectionManager.DeselectAll();
            ChiselSurfaceSelectionManager.Select(found);
            SceneView.RepaintAll();
        }
        #endregion

        #region Footer and applying
        static string SelectionText()
        {
            var surfaces = ChiselSurfaceSelectionManager.Selection;
            if (surfaces.Count == 0)
                return "Nothing selected";
            var nodes = new HashSet<ChiselNodeComponent>();
            var materials = new HashSet<Material>();
            foreach (var surface in surfaces)
            {
                if (surface.node) nodes.Add(surface.node);
                materials.Add(surface.RenderMaterial);
            }
            var text = $"{surfaces.Count} surface{(surfaces.Count == 1 ? "" : "s")} on {nodes.Count} brush{(nodes.Count == 1 ? "" : "es")}";
            text += materials.Count > 1 ? $"  ·  {materials.Count} materials (mixed)" : "";
            return text;
        }

        void FooterGUI(Rect rect)
        {
            GUILayout.BeginArea(new Rect(rect.x + 8, rect.y + 4, rect.width - 16, rect.height - 6));
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Label("Selection  " + SelectionText(), EditorStyles.miniLabel);
                GUILayout.FlexibleSpace();
                GUILayout.Label("Double-click to apply · Drag onto a surface", EditorStyles.miniLabel);
                if (GUILayout.Button("Close", GUILayout.Width(60)))
                    Close();
                var count = ChiselSurfaceSelectionManager.Selection.Count;
                var material = Load(Selected);
                using (new EditorGUI.DisabledScope(count == 0 || material == null))
                {
                    if (GUILayout.Button($"Apply to {count} surface{(count == 1 ? "" : "s")}", GUILayout.Width(140)))
                        Apply(material);
                }
            }
            GUILayout.EndArea();
        }

        /// <summary>Gives the selected surfaces a material, undoably</summary>
        internal static void Apply(Material material)
        {
            var surfaces = ChiselSurfaceSelectionManager.Selection;
            if (material == null || surfaces.Count == 0)
                return;
            ApplyTo(material, surfaces);
        }

        internal static void ApplyTo(Material material, ICollection<SurfaceReference> surfaces)
        {
            var nodes = new HashSet<ChiselNodeComponent>();
            foreach (var surface in surfaces)
            {
                if (surface.node) nodes.Add(surface.node);
            }
            var array = new UnityEngine.Object[nodes.Count];
            var i = 0;
            foreach (var node in nodes)
                array[i++] = node;
            Undo.RecordObjects(array, "Apply Material");
            // every surface is changed after all are found: surfaces of one generator can share a definition
            var list = new List<SurfaceReference>(surfaces);
            foreach (var surface in list)
                surface.RenderMaterial = material;
            foreach (var node in nodes)
                EditorUtility.SetDirty(node);
            ChiselSurfaceBrowserPrefs.MarkUsed(ChiselSurfaceBrowserPrefs.ItemOf(material));
            ChiselSceneMaterialUsage.SetDirty();
        }
        #endregion
    }
}
