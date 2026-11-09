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
    public sealed partial class ChiselSurfaceBrowserWindow : EditorWindow
    {
        internal const float kSidebarWidth    = 220;
        internal const float kMinDetailsWidth = 260;
        internal const float kMinGridWidth    = 200;
        const float kSplitterWidth = 6;

        [SerializeField] float detailsWidth = 380;
        bool draggingSplitter;

        /// <summary>The details panel's width, leaving the grid its smallest; never under its own smallest</summary>
        internal static float ClampDetailsWidth(float width, float bodyWidth, float sidebarWidth = kSidebarWidth)
        {
            return Mathf.Max(kMinDetailsWidth, Mathf.Min(width, bodyWidth - sidebarWidth - kMinGridWidth));
        }
        const float kFooterHeight = 28;
        const float kHeaderHeight = 22;
        const float kMinThumb     = 48;
        const float kMaxThumb     = 160;

        static readonly string[] kSortNames = { "Name", "Folder", "Use in scene" };

        // per user, between sessions
        [SerializeField] float   thumbSize = 88;
        [SerializeField] int     sort;
        [SerializeField] List<string> expandedFolders = new() { "Assets" };
        [SerializeField] string  search = string.Empty;
        [SerializeField] int     quick;
        [SerializeField] List<string> folders = new();
        [SerializeField] string  label;
        [SerializeField] string  selectedGuid;

        readonly ChiselSurfaceBrowserModel model = new();
        List<ChiselSurfaceBrowserModel.Item> shown = new();
        List<ChiselSurfaceBrowserModel.Folder> folderTree = new();
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
            window.minSize = new Vector2(kSidebarWidth + kMinDetailsWidth + kMinGridWidth, 360);
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
            model.isDecal     = item => ChiselSurfaceBrowserDecals.IsDecal(Load(item));
            model.isSetUp     = item => MetadataOf(Load(item)) != null;
            model.traits.Clear();
            model.traits.AddRange(Traits());
            // only unticked filters are kept; one ticked under the earlier "with it" filters is just ticked now
            foreach (var saved in traitFilters)
            {
                if (saved.Length > 1 && saved[0] == '-')
                    model.SetFilter(saved.Substring(1), ChiselSurfaceBrowserModel.TraitFilter.Without);
            }
            model.presets = presetMode;
            // a list kept from before the modes, or one that doesn't apply to presets
            if (quick < 0 || quick > (int)ChiselSurfaceBrowserModel.QuickAccess.NotSetUp ||
                (presetMode && !ChiselSurfaceBrowserModel.AppliesToPresets((ChiselSurfaceBrowserModel.QuickAccess)quick)))
                quick = (int)ChiselSurfaceBrowserModel.QuickAccess.All;
            wantsMouseMove = true;     // moving the mouse cancels a rename that's waiting to start
            EditorApplication.projectChanged += MarkDirty;
            Selection.selectionChanged       += OnSelectionChanged;
            Undo.undoRedoPerformed           += MarkDirty;
            dirty = true;
        }

        void OnDisable()
        {
            EditorApplication.projectChanged -= MarkDirty;
            Selection.selectionChanged       -= OnSelectionChanged;
            Undo.undoRedoPerformed           -= MarkDirty;
            if (metadataEditor) DestroyImmediate(metadataEditor);
            if (previewEditor)  DestroyImmediate(previewEditor);
            DestroyDecalResources();
            ChiselOutlineRenderer.ShowSelectedSurfaces = false;
        }

        void MarkDirty() { dirty = true; Repaint(); }

        // hidden folders are changed in the project settings
        void OnFocus() { MarkDirty(); UpdatePartHighlight(); }

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

        // "+name" or "-name" per trait filtered on
        [SerializeField] List<string> traitFilters = new();

        static ChiselPaintProfile PaintProfileOf(Material material)
        {
            return material != null && material.shader != null ? ChiselPaintProfiles.Find(material.shader) : null;
        }

        static bool HasPaintMode(Material material, Func<ChiselPaintMode, bool> mode)
        {
            var profile = PaintProfileOf(material);
            if (profile == null)
                return false;
            foreach (var m in profile.modes)
            {
                if (mode(m))
                    return true;
            }
            return false;
        }

        // what the filters can ask of a material, each also by its absence
        static ChiselSurfaceBrowserModel.Trait[] Traits()
        {
            ChiselSurfaceBrowserModel.Trait Trait(string name, Func<Material, bool> has) =>
                new() { name = name, has = item => has(Load(item)) };
            var traits = new List<ChiselSurfaceBrowserModel.Trait>
            {
                Trait("Paintable",     material => PaintProfileOf(material) != null),
                Trait("Color paint",   material => HasPaintMode(material, mode => mode.kind == ChiselPaintKind.Color)),
                Trait("Value paint",   material => HasPaintMode(material, mode => mode.kind == ChiselPaintKind.Value)),
                Trait("Blends layers", material => HasPaintMode(material, mode => mode.group > 0)),
                Trait("Skybox",        material => material != null && material.shader != null && material.shader.name.StartsWith("Skybox/", StringComparison.OrdinalIgnoreCase)),
                Trait("Hotspots",      material => HotspotLayoutOf(material) != null),
                Trait("Box decal",     material => ChiselSurfaceBrowserDecals.IsDecal(material) && ChiselSurfaceBrowserDecals.ShapeOf(material) == ChiselDecalShape.Box),
                Trait("Trim decal",    material => ChiselSurfaceBrowserDecals.IsDecal(material) && ChiselSurfaceBrowserDecals.ShapeOf(material) == ChiselDecalShape.Trim),
            };
            traits.Add(ChiselSurfaceBrowserModel.PlainTrait(traits));
            return traits.ToArray();
        }

        void SaveTraitFilters()
        {
            traitFilters.Clear();
            foreach (var trait in model.traits)
            {
                var filter = model.FilterOf(trait.name);
                if (filter != ChiselSurfaceBrowserModel.TraitFilter.Any)
                    traitFilters.Add((filter == ChiselSurfaceBrowserModel.TraitFilter.With ? "+" : "-") + trait.name);
            }
        }

        // a filter per trait, all ticked at first; unticked, the materials with it are left out
        void TraitsGUI()
        {
            GUILayout.Space(6);
            GUILayout.Label("FILTERS", EditorStyles.miniBoldLabel);
            foreach (var trait in model.traits)
            {
                var shown = model.FilterOf(trait.name) != ChiselSurfaceBrowserModel.TraitFilter.Without;
                var rect = GUILayoutUtility.GetRect(new GUIContent(trait.name), EditorStyles.label, GUILayout.ExpandWidth(true));
                var count = model.CountWith(trait);
                var countRect = CountRect(rect, count);
                var newShown = EditorGUI.ToggleLeft(new Rect(rect.x + 4, rect.y, WidthBefore(countRect, rect.x + 4), rect.height),
                                                    new GUIContent(trait.name, "Show the materials with this"), shown);
                CountGUI(countRect, count);
                if (newShown == shown)
                    continue;
                model.SetFilter(trait.name, newShown ? ChiselSurfaceBrowserModel.TraitFilter.Any : ChiselSurfaceBrowserModel.TraitFilter.Without);
                SaveTraitFilters();
                Filter();
            }
        }

        void Refresh()
        {
            if (!dirty)
                return;
            dirty = false;
            ChiselProjectSettingsEditor.EnsureDefaultHiddenFolders();
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
                    labels = ChiselAssetLabels.Get(asset)
                });
            }
            AddPresetItems(items);
            model.SetItems(items);
            folderTree = model.BuildFolders(includeHidden: false);
            labels  = model.Labels();
            Filter();
            RenameNewPreset();
        }

        void Filter()
        {
            shown = model.Filter(search, (ChiselSurfaceBrowserModel.QuickAccess)quick, folders, label,
                                 (ChiselSurfaceBrowserModel.Sort)sort, item => ChiselSceneMaterialUsage.SurfacesUsing(Load(item)));
            // a card being renamed that is no longer shown is renamed as typed so far, not left waiting
            if (cardRenameGuid != null && !shown.Exists(item => item.guid == cardRenameGuid))
            {
                ChiselSurfaceBrowserModel.Item renamed = null;
                foreach (var item in model.Items)
                {
                    if (item.guid == cardRenameGuid)
                        renamed = item;
                }
                if (renamed != null) FinishCardRename(renamed);
                else                 cardRenameGuid = null;
            }
        }

        /// <summary>Shows one of the quick access lists, as clicking it does</summary>
        internal void ShowQuickAccess(ChiselSurfaceBrowserModel.QuickAccess list)
        {
            quick = (int)list;
            folders.Clear();
            label = null;
            Filter();
        }

        /// <summary>Searches, as typing in the search field does</summary>
        internal void SetSearch(string text)
        {
            search = text ?? string.Empty;
            Filter();
        }

        internal string SelectedGuid => selectedGuid;
        internal IReadOnlyList<ChiselSurfaceBrowserModel.Item> Shown => shown;
        internal bool IsRenamingCard => cardRenameGuid != null;

        internal void Select(string guid)
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

        // a click anywhere ends typing in a text field (a text field clicked on takes the keys again itself), as does
        // the window losing focus
        bool dropTextFocus;

        internal static void DropTextFocus()
        {
            GUIUtility.keyboardControl = 0;
            EditorGUIUtility.editingTextField = false;
        }

        /// <summary>Whether the arrow keys go through the grid: not while typing in a text field that has the keys</summary>
        internal static bool GridTakesKeys(bool editingTextField, int keyboardControl)
        {
            return !editingTextField || keyboardControl == 0;
        }

        void OnGUI()
        {
            if (Event.current.type == EventType.MouseDown || dropTextFocus)
            {
                dropTextFocus = false;
                DropTextFocus();
            }
            Refresh();
            var full = new Rect(0, 0, position.width, position.height);
            var toolbar = new Rect(0, 0, full.width, EditorGUIUtility.singleLineHeight + 6);
            var footer  = new Rect(0, full.height - kFooterHeight, full.width, kFooterHeight);
            var body    = new Rect(0, toolbar.yMax, full.width, footer.y - toolbar.yMax);

            ToolbarGUI(toolbar);
            // either side panel can be collapsed; both can be resized by dragging their edge
            var sidebarShown = showSidebar ? ClampSidebarWidth(sidebarWidth, body.width, showDetails ? detailsWidth : 0) : 0;
            if (showSidebar)
                SidebarSplitterGUI(body);
            sidebarShown = showSidebar ? sidebarWidth : 0;
            if (showDetails)
                DetailsSplitterGUI(body, sidebarShown);
            var detailsShown = showDetails ? detailsWidth : 0;
            var sidebar = new Rect(body.x, body.y, sidebarShown, body.height);
            var details = new Rect(body.xMax - detailsShown, body.y, detailsShown, body.height);
            var grid    = new Rect(sidebar.xMax, body.y, details.x - sidebar.xMax, body.height);

            if (showSidebar) SidebarGUI(sidebar);
            GridGUI(grid);
            if (showDetails) DetailsGUI(details);
            FooterGUI(footer);

            if (showSidebar) EditorGUI.DrawRect(new Rect(sidebar.xMax, body.y, 1, body.height), new Color(0, 0, 0, 0.3f));
            if (showDetails) EditorGUI.DrawRect(new Rect(details.x - 1, body.y, 1, body.height), new Color(0, 0, 0, 0.3f));
            EditorGUI.DrawRect(new Rect(0, footer.y, full.width, 1), new Color(0, 0, 0, 0.3f));
        }

        internal const float kMinSidebarWidth = 140;
        [SerializeField] float sidebarWidth = kSidebarWidth;
        [SerializeField] bool  showSidebar  = true;
        [SerializeField] bool  showDetails  = true;
        bool draggingSidebar;

        /// <summary>The sidebar's width, leaving the grid its smallest; never under its own smallest</summary>
        internal static float ClampSidebarWidth(float width, float bodyWidth, float detailsWidth)
        {
            return Mathf.Max(kMinSidebarWidth, Mathf.Min(width, bodyWidth - detailsWidth - kMinGridWidth));
        }

        // dragging the edge between the sidebar and the grid resizes the sidebar
        void SidebarSplitterGUI(Rect body)
        {
            var others = showDetails ? detailsWidth : 0;
            sidebarWidth = ClampSidebarWidth(sidebarWidth, body.width, others);
            var splitter = new Rect(body.x + sidebarWidth - kSplitterWidth / 2, body.y, kSplitterWidth, body.height);
            EditorGUIUtility.AddCursorRect(splitter, MouseCursor.ResizeHorizontal);
            var evt = Event.current;
            switch (evt.type)
            {
                case EventType.MouseDown when evt.button == 0 && splitter.Contains(evt.mousePosition):
                    draggingSidebar = true;
                    evt.Use();
                    break;
                case EventType.MouseDrag when draggingSidebar:
                    sidebarWidth = ClampSidebarWidth(evt.mousePosition.x - body.x, body.width, others);
                    Repaint();
                    evt.Use();
                    break;
                case EventType.MouseUp when draggingSidebar:
                    draggingSidebar = false;
                    evt.Use();
                    break;
            }
        }

        // dragging the edge between the grid and the details panel resizes the panel
        void DetailsSplitterGUI(Rect body, float sidebarShown)
        {
            detailsWidth = ClampDetailsWidth(detailsWidth, body.width, sidebarShown);
            var splitter = new Rect(body.xMax - detailsWidth - kSplitterWidth / 2, body.y, kSplitterWidth, body.height);
            EditorGUIUtility.AddCursorRect(splitter, MouseCursor.ResizeHorizontal);
            var evt = Event.current;
            switch (evt.type)
            {
                case EventType.MouseDown when evt.button == 0 && splitter.Contains(evt.mousePosition):
                    draggingSplitter = true;
                    evt.Use();
                    break;
                case EventType.MouseDrag when draggingSplitter:
                    detailsWidth = ClampDetailsWidth(body.xMax - evt.mousePosition.x, body.width, sidebarShown);
                    Repaint();
                    evt.Use();
                    break;
                case EventType.MouseUp when draggingSplitter:
                    draggingSplitter = false;
                    evt.Use();
                    break;
            }
        }

        #region Toolbar
        void ToolbarGUI(Rect rect)
        {
            GUILayout.BeginArea(rect, EditorStyles.toolbar);
            using (new EditorGUILayout.HorizontalScope())
            {
                showSidebar = GUILayout.Toggle(showSidebar, new GUIContent("Sidebar", "Show quick access, filters, folders and labels"), EditorStyles.toolbarButton, GUILayout.ExpandWidth(false));
                GUILayout.Space(4);
                // materials or generator material presets: one or the other is listed
                var materials = GUILayout.Toggle(!presetMode, new GUIContent("Materials", "List materials"), EditorStyles.toolbarButton, GUILayout.ExpandWidth(false));
                var presets   = GUILayout.Toggle(presetMode,  new GUIContent("Presets", "List generator material presets"), EditorStyles.toolbarButton, GUILayout.ExpandWidth(false));
                if (presets != presetMode && presets)                  SetPresetMode(true);
                else if (materials != !presetMode && materials)        SetPresetMode(false);
                GUILayout.Space(4);
                EditorGUI.BeginChangeCheck();
                var searchRect = GUILayoutUtility.GetRect(120, 360, EditorGUIUtility.singleLineHeight, EditorGUIUtility.singleLineHeight, EditorStyles.toolbarSearchField,
                                                          GUILayout.MinWidth(120), GUILayout.MaxWidth(360));
                search = EditorGUI.TextField(searchRect, search, EditorStyles.toolbarSearchField);
                GUI.Label(searchRect, kSearchTooltip);
                if (EditorGUI.EndChangeCheck())
                    Filter();
                GUILayout.FlexibleSpace();
                thumbSize = GUILayout.HorizontalSlider(thumbSize, kMinThumb, kMaxThumb, GUILayout.Width(100));
                GUILayout.Space(6);
                EditorGUI.BeginChangeCheck();
                sort = EditorGUILayout.Popup(sort, kSortNames, EditorStyles.toolbarPopup, GUILayout.Width(100));
                if (EditorGUI.EndChangeCheck())
                    Filter();
                GUILayout.Space(4);
                showDetails = GUILayout.Toggle(showDetails, new GUIContent("Details", "Show the selected material's details"), EditorStyles.toolbarButton, GUILayout.ExpandWidth(false));
            }
            GUILayout.EndArea();
        }
        #endregion

        #region Sidebar
        static readonly string[] kQuickNames = { "All materials", "Favorites", "Recently used", "In this scene", "Unused in this scene", "Decals", "Not set up" };

        string QuickName(int list)
        {
            return presetMode && list == (int)ChiselSurfaceBrowserModel.QuickAccess.All ? "All presets" : kQuickNames[list];
        }

        // a sidebar row's count, at its end: as wide as its number, so the name before it gets the rest of the row
        const float kCountMargin = 4;
        const float kCountGap    = 4;

        internal static Rect CountRect(Rect row, int count)
        {
            var width = EditorStyles.miniLabel.CalcSize(new GUIContent(count.ToString())).x;
            return new Rect(row.xMax - kCountMargin - width, row.y, width, row.height);
        }

        /// <summary>How wide something starting at x can be, up to a count with a gap before it</summary>
        internal static float WidthBefore(Rect count, float x) { return Mathf.Max(0, count.x - kCountGap - x); }

        static void CountGUI(Rect rect, int count)
        {
            GUI.Label(rect, count.ToString(), new GUIStyle(EditorStyles.miniLabel) { alignment = TextAnchor.MiddleRight });
        }

        bool Row(string text, int count, bool selected, int depth = 0, bool greyed = false)
        {
            var rect = GUILayoutUtility.GetRect(new GUIContent(text), EditorStyles.label, GUILayout.ExpandWidth(true));
            if (selected)
                EditorGUI.DrawRect(rect, new Color(0.24f, 0.48f, 0.9f, 0.5f));
            var textX    = rect.x + 6 + depth * 14;
            var textRect = new Rect(textX, rect.y, count >= 0 ? WidthBefore(CountRect(rect, count), textX) : rect.xMax - textX, rect.height);
            var style = greyed ? EditorStyles.miniLabel : EditorStyles.label;
            var prev = GUI.color;
            if (greyed) GUI.color = new Color(prev.r, prev.g, prev.b, 0.5f);
            GUI.Label(textRect, greyed ? "<i>" + text + "</i>" : text, new GUIStyle(style) { richText = true });
            if (count >= 0)
                CountGUI(CountRect(rect, count), count);
            GUI.color = prev;
            return Event.current.type == EventType.MouseDown && Event.current.button == 0 && rect.Contains(Event.current.mousePosition);
        }

        void SidebarGUI(Rect rect)
        {
            GUILayout.BeginArea(rect);
            sidebarScroll = EditorGUILayout.BeginScrollView(sidebarScroll);

            PartsGUI();

            GUILayout.Label("QUICK ACCESS", EditorStyles.miniBoldLabel);
            for (int i = 0; i < kQuickNames.Length; i++)
            {
                if (presetMode && !ChiselSurfaceBrowserModel.AppliesToPresets((ChiselSurfaceBrowserModel.QuickAccess)i))
                    continue;
                if (Row(QuickName(i), model.Count((ChiselSurfaceBrowserModel.QuickAccess)i), quick == i && folders.Count == 0 && string.IsNullOrEmpty(label)))
                {
                    quick = i; folders.Clear(); label = null;
                    Filter();
                    Event.current.Use();
                }
            }

            // the filters are a material's
            if (!presetMode)
                TraitsGUI();

            GUILayout.Space(6);
            GUILayout.Label("FOLDERS", EditorStyles.miniBoldLabel);
            foreach (var root in folderTree)
                FolderGUI(root);

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
            if (folders.Contains(node.path))
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
            var countRect = CountRect(rect, node.count);
            GUI.Label(new Rect(foldRect.xMax, rect.y, WidthBefore(countRect, foldRect.xMax), rect.height), new GUIContent(node.name, node.path));
            CountGUI(countRect, node.count);

            if (evt.type == EventType.MouseDown && evt.button == 0 && hovering && !foldRect.Contains(evt.mousePosition))
            {
                // ctrl, cmd or shift adds or removes a folder, a plain click picks only it
                if (evt.control || evt.command || evt.shift)
                {
                    if (!folders.Remove(node.path))
                        folders.Add(node.path);
                } else
                {
                    folders.Clear();
                    folders.Add(node.path);
                }
                quick = 0; label = null;
                Filter();
                evt.Use();
            }
            if (evt.type == EventType.ContextClick && hovering)
            {
                if (!folders.Contains(node.path))
                {
                    folders.Clear();
                    folders.Add(node.path);
                    quick = 0; label = null;
                    Filter();
                }
                var toHide = new List<string>(folders);
                var menu = new GenericMenu();
                menu.AddItem(new GUIContent(toHide.Count == 1 ? "Hide Folder" : $"Hide {toHide.Count} Folders"), false, () =>
                {
                    HideFolders(toHide);
                    folders.Clear();
                    Filter();
                    MarkDirty();
                    EditorUtility.DisplayDialog(toHide.Count == 1 ? "Folder hidden" : "Folders hidden",
                        (toHide.Count == 1 ? "The folder is hidden" : "The folders are hidden") +
                        " from the Surface Browser. Show it again in Project Settings > Chisel > Surface Browser.", "OK");
                });
                menu.AddSeparator(string.Empty);
                menu.AddItem(new GUIContent("Settings..."), false, () => SettingsService.OpenProjectSettings(ChiselProjectSettingsEditor.kSurfaceBrowserPath));
                menu.ShowAsContext();
                evt.Use();
            }
            if (hovering && evt.type == EventType.MouseMove)
                Repaint();

            if (!expanded)
                return;
            foreach (var child in node.children)
                FolderGUI(child);
        }

        /// <summary>Hides the folders as one undo step</summary>
        internal static void HideFolders(IReadOnlyList<string> paths)
        {
            Undo.IncrementCurrentGroup();
            var group = Undo.GetCurrentGroup();
            foreach (var path in paths)
                ChiselProjectSettings.HideFolder(path);
            Undo.SetCurrentGroupName(paths.Count == 1 ? "Hide Folder" : "Hide Folders");
            Undo.CollapseUndoOperations(group);
        }
        #endregion

        #region Grid
        string HeaderText()
        {
            var what = folders.Count == 1 ? folders[0] : folders.Count > 1 ? $"{folders.Count} folders" : (!string.IsNullOrEmpty(label) ? "Label: " + label : QuickName(quick));
            var kind = presetMode ? "preset" : "material";
            return $"{what}  -  {shown.Count} {kind}{(shown.Count == 1 ? "" : "s")}";
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
            // a click in the grid that no card takes, left of its scrollbar, deselects
            var evt = Event.current;
            var clickInGrid = evt.type == EventType.MouseDown && evt.button == 0 &&
                              new Rect(area.x, area.y, area.width - 16, area.height).Contains(evt.mousePosition);
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
            if (clickInGrid && evt.type == EventType.MouseDown)
            {
                partsFocused = false;
                pendingRenameGuid = null;
                Select(null);
                evt.Use();
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
            if (item.preset)
            {
                PresetCardGUI(rect, item);
                return;
            }
            var evt = Event.current;
            var selected = item.guid == selectedGuid;
            if (selected)
                EditorGUI.DrawRect(rect, new Color(0.24f, 0.48f, 0.9f, 0.45f));
            var thumbRect = new Rect(rect.x + 4, rect.y + 4, thumbSize, thumbSize);
            var material = Load(item);
            if (evt.type == EventType.Repaint)
            {
                EditorGUI.DrawRect(thumbRect, new Color(0, 0, 0, 0.2f));
                if (ChiselSurfaceBrowserDecals.IsDecal(material))
                    DrawImage(thumbRect, material, 0);      // flat: a decal draws nothing on a preview sphere
                else
                {
                    var texture = PreviewOf(material, IsSheet(material));
                    if (texture != null)
                        GUI.DrawTexture(thumbRect, texture, ScaleMode.ScaleToFit);
                    else
                        Repaint();  // previews load in the background
                }
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
            // a star: bright when a favorite, faint otherwise
            var star = new GUIStyle(badge) { fontStyle = FontStyle.Bold, normal = { textColor = favorite ? new Color(1f, 0.8f, 0.2f) : new Color(1, 1, 1, 0.35f) } };
            if (GUI.Button(starRect, new GUIContent("*", favorite ? "Remove from favorites" : "Add to favorites"), star))
            {
                ChiselSurfaceBrowserPrefs.SetFavorite(item.guid, !favorite);
                Filter();
                evt.Use();
            }
            var cardIsDecal = ChiselSurfaceBrowserDecals.IsDecal(material);
            var uses = cardIsDecal ? ChiselSceneMaterialUsage.DecalsUsing(material) : ChiselSceneMaterialUsage.SurfacesUsing(material);
            if (uses > 0)
                GUI.Label(new Rect(thumbRect.xMax - 34, thumbRect.yMax - 16, 32, 14), new GUIContent(uses.ToString(), $"{uses} {(cardIsDecal ? "decals" : "surfaces")} in the scene use it"), new GUIStyle(badge) { alignment = TextAnchor.MiddleRight });

            var nameRect = new Rect(rect.x, thumbRect.yMax + 2, rect.width, EditorGUIUtility.singleLineHeight);
            if (cardRenameGuid == item.guid)
            {
                CardRenameGUI(nameRect, item);
                return;
            }
            GUI.Label(nameRect, new GUIContent(item.name, item.path),
                      new GUIStyle(EditorStyles.miniLabel) { alignment = TextAnchor.MiddleCenter, clipping = TextClipping.Clip });

            if (evt.type == EventType.MouseMove && pendingRenameGuid != null &&
                (GUIUtility.GUIToScreenPoint(evt.mousePosition) - pendingRenameMouse).sqrMagnitude > kRenameStillPixels * kRenameStillPixels)
                pendingRenameGuid = null;
            if (!rect.Contains(evt.mousePosition))
                return;
            switch (evt.type)
            {
                case EventType.MouseDown when evt.button == 0:
                    // a click on the name of the one already selected: renamed when the mouse then stays still
                    pendingRenameGuid = null;
                    if (evt.clickCount == 1 && item.guid == selectedGuid && nameRect.Contains(evt.mousePosition) && IsEditable(item.path))
                    {
                        pendingRenameGuid  = item.guid;
                        pendingRenameTime  = EditorApplication.timeSinceStartup;
                        pendingRenameMouse = GUIUtility.GUIToScreenPoint(evt.mousePosition);
                    }
                    var changedMaterial = item.guid != selectedGuid;
                    partsFocused = false;   // the grid takes the arrow keys again
                    Select(item.guid);
                    if (changedMaterial)
                        ApplyOnSelection(material);
                    GUIUtility.hotControl = 0;
                    evt.Use();
                    break;
                case EventType.ContextClick when material != null:
                    Select(item.guid);
                    MaterialMenu(item, material).ShowAsContext();
                    evt.Use();
                    break;
                case EventType.MouseDrag when evt.button == 0 && material != null && cardIsDecal:
                    // dropped in the scene view, a decal is placed there
                    Select(item.guid);
                    var image = SelectedImage;
                    if (image != null)
                        ChiselSurfaceBrowserDecals.StartDrag(image);
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

        GenericMenu MaterialMenu(ChiselSurfaceBrowserModel.Item item, Material material)
        {
            var menu = new GenericMenu();
            menu.AddItem(new GUIContent("Show in Project Tab"), false, () => EditorGUIUtility.PingObject(material));
            menu.AddItem(new GUIContent("Show In Explorer"), false, () => EditorUtility.RevealInFinder(item.path));
            if (IsEditable(item.path))
                menu.AddItem(new GUIContent("Rename"), false, () => StartRename(item));
            else
                menu.AddDisabledItem(new GUIContent("Rename"));
            if (MetadataOf(material) == null)
            {
                if (IsEditable(item.path))
                    menu.AddItem(new GUIContent(kAddParametersText), false, () => { AddSurfaceParameters(material); MarkDirty(); });
                else
                    menu.AddDisabledItem(new GUIContent(kAddParametersText));
            }
            menu.AddSeparator(string.Empty);
            menu.AddItem(new GUIContent("Select Surfaces Using This Material"), false, () => SelectSurfacesUsing(material));
            AddPartsMenu(menu, material);
            return menu;
        }

        const string kAddParametersText = "Set up material";

        /// <summary>Gives a material Chisel's surface parameters (its metadata), undoably; false when it has them or
        /// can't be changed</summary>
        internal static bool AddSurfaceParameters(Material material)
        {
            if (material == null || MetadataOf(material) != null || !IsEditable(AssetDatabase.GetAssetPath(material)))
                return false;
            Undo.RecordObject(material, kAddParametersText);
            material.AddMetadataOfType<ChiselSurfaceMetadata>();
            EditorUtility.SetDirty(material);
            AssetDatabase.SaveAssetIfDirty(material);
            return MetadataOf(material) != null;
        }

        /// <summary>Whether an asset can be changed: not locked or checked in by version control, not in a package that
        /// can't be changed (one from a registry or built in)</summary>
        internal static bool IsEditable(string path)
        {
            if (string.IsNullOrEmpty(path))
                return false;
            var package = path.StartsWith("Packages/", StringComparison.OrdinalIgnoreCase) ? UnityEditor.PackageManager.PackageInfo.FindForAssetPath(path) : null;
            return IsEditable(package?.source, AssetDatabase.IsOpenForEdit(path, StatusQueryOptions.UseCachedIfPossible));
        }

        /// <summary>source: the package it is in, null outside packages; openForEdit: not locked by version control</summary>
        internal static bool IsEditable(UnityEditor.PackageManager.PackageSource? source, bool openForEdit)
        {
            if (source.HasValue && source.Value != UnityEditor.PackageManager.PackageSource.Embedded &&
                source.Value != UnityEditor.PackageManager.PackageSource.Local)
                return false;
            return openForEdit;
        }

        const string kRenameControl     = "ChiselRenameMaterial";
        const string kCardRenameControl = "ChiselRenameCard";
        bool focusRename;

        // renaming in place on a card
        const double kRenameDelay       = 1.0;
        const float  kRenameStillPixels = 4;
        string  cardRenameGuid;
        string  cardRenameText;
        bool    focusCardRename;
        string  pendingRenameGuid;
        double  pendingRenameTime;
        Vector2 pendingRenameMouse;

        /// <summary>A click on a selected item's name starts renaming it when the mouse stays still long enough</summary>
        internal static bool ShouldStartRename(double clickTime, double now, Vector2 clickMouse, Vector2 mouse)
        {
            return now - clickTime >= kRenameDelay && (mouse - clickMouse).sqrMagnitude <= kRenameStillPixels * kRenameStillPixels;
        }

        internal void StartRename(ChiselSurfaceBrowserModel.Item item)
        {
            pendingRenameGuid = null;
            cardRenameGuid    = item.guid;
            cardRenameText    = item.name;
            focusCardRename   = true;
            Repaint();
        }

        void Update()
        {
            if (pendingRenameGuid == null)
                return;
            if (pendingRenameGuid != selectedGuid)
            {
                pendingRenameGuid = null;
                return;
            }
            if (!ShouldStartRename(pendingRenameTime, EditorApplication.timeSinceStartup, pendingRenameMouse, pendingRenameMouse))
                return;
            var item = Selected;
            pendingRenameGuid = null;
            if (item != null)
                StartRename(item);
        }

        // the card's name as a text field: Enter or leaving it renames, Escape keeps the name
        void CardRenameGUI(Rect rect, ChiselSurfaceBrowserModel.Item item)
        {
            var evt = Event.current;
            var focused = GUI.GetNameOfFocusedControl() == kCardRenameControl;
            if (evt.type == EventType.KeyDown && focused)
            {
                if (evt.keyCode == KeyCode.Escape)
                {
                    cardRenameGuid = null;
                    DropTextFocus();
                    evt.Use();
                    Repaint();
                    return;
                }
                if (evt.keyCode == KeyCode.Return || evt.keyCode == KeyCode.KeypadEnter)
                {
                    FinishCardRename(item);
                    evt.Use();
                    GUIUtility.ExitGUI();
                }
            }
            GUI.SetNextControlName(kCardRenameControl);
            cardRenameText = GUI.TextField(rect, cardRenameText, new GUIStyle(EditorStyles.textField) { alignment = TextAnchor.MiddleCenter, fontSize = EditorStyles.miniLabel.fontSize });
            if (focusCardRename)
            {
                focusCardRename = false;
                cardRenameHadFocus = false;
                EditorGUI.FocusTextInControl(kCardRenameControl);
            } else if (focused)
                cardRenameHadFocus = true;
            else if (evt.type == EventType.Repaint && cardRenameHadFocus)
                FinishCardRename(item);    // clicked elsewhere
        }

        bool cardRenameHadFocus;

        void FinishCardRename(ChiselSurfaceBrowserModel.Item item)
        {
            var text = cardRenameText;
            cardRenameGuid = null;
            DropTextFocus();
            var error = Rename(item.path, text);
            if (error != null)
                ShowNotification(new GUIContent(error));
            MarkDirty();
        }

        /// <summary>Renames an asset; the error when it can't be, null when it was</summary>
        internal static string Rename(string path, string newName)
        {
            newName = newName?.Trim();
            if (string.IsNullOrEmpty(newName))
                return "A name can't be empty";
            if (newName == System.IO.Path.GetFileNameWithoutExtension(path))
                return null;
            if (!IsEditable(path))
                return "This material can't be changed";
            var error = AssetDatabase.RenameAsset(path, newName);
            return string.IsNullOrEmpty(error) ? null : error;
        }

        // the name, always a text field (read-only when the material can't be changed); Enter or leaving it renames,
        // Escape keeps the name
        void NameGUI(ChiselSurfaceBrowserModel.Item item, bool editable)
        {
            using (new EditorGUI.DisabledScope(!editable))
            {
                GUI.SetNextControlName(kRenameControl);
                var newName = EditorGUILayout.DelayedTextField(item.name, new GUIStyle(EditorStyles.textField) { fontStyle = FontStyle.Bold });
                if (focusRename)
                {
                    focusRename = false;
                    EditorGUI.FocusTextInControl(kRenameControl);
                }
                if (!editable || newName == item.name)
                    return;
                var error = Rename(item.path, newName);
                if (error != null)
                    ShowNotification(new GUIContent(error));
                MarkDirty();
                GUIUtility.ExitGUI();
            }
        }

        void KeyboardGUI(int columns)
        {
            var evt = Event.current;
            if (evt.type != EventType.KeyDown || shown.Count == 0 || !GridTakesKeys(EditorGUIUtility.editingTextField, GUIUtility.keyboardControl))
                return;
            var index = shown.FindIndex(item => item.guid == selectedGuid);
            var next = index;
            switch (evt.keyCode)
            {
                case KeyCode.RightArrow: next = index + 1; break;
                case KeyCode.LeftArrow:  next = index - 1; break;
                case KeyCode.DownArrow:  next = index + columns; break;
                case KeyCode.UpArrow:    next = index - columns; break;
                default: return;
            }
            next = Mathf.Clamp(next < 0 && index < 0 ? 0 : next, 0, shown.Count - 1);
            if (shown[next].guid != selectedGuid)
            {
                Select(shown[next].guid);
                if (shown[next].preset) ApplyPresetOnSelection(LoadPreset(shown[next]));
                else                    ApplyOnSelection(Load(shown[next]));
            }
            evt.Use();
        }
        #endregion

        #region Details
        void DetailsGUI(Rect rect)
        {
            var item = Selected;
            var material = Load(item);
            GUILayout.BeginArea(new Rect(rect.x + 8, rect.y + 6, rect.width - 14, rect.height - 8));
            // never scrolls sideways: everything fits the panel's width
            detailsScroll = EditorGUILayout.BeginScrollView(detailsScroll, false, false, GUIStyle.none, GUI.skin.verticalScrollbar, GUIStyle.none);
            var contentWidth = rect.width - 14 - 16;
            var oldLabelWidth = EditorGUIUtility.labelWidth;
            EditorGUIUtility.labelWidth = Mathf.Max(80, contentWidth * 0.4f);
            try { DetailsContentGUI(item, material, contentWidth); }
            finally { EditorGUIUtility.labelWidth = oldLabelWidth; }
            EditorGUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        void DetailsContentGUI(ChiselSurfaceBrowserModel.Item item, Material material, float contentWidth)
        {
            if (item != null && item.preset)
            {
                PresetDetailsGUI(item, LoadPreset(item), contentWidth);
                return;
            }
            if (material == null)
                return;

            // a large preview; Unity's own material preview, with its sphere, cube, plane and other shapes; a decal's
            // image flat (a decal draws nothing on the preview's shapes)
            var isDecal = ChiselSurfaceBrowserDecals.IsDecal(material);
            if (isDecal)
                DecalPreviewGUI(material, contentWidth);
            else
                Editor.CreateCachedEditor(material, null, ref previewEditor);
            if (!isDecal && previewEditor != null && previewEditor.HasPreviewGUI())
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.FlexibleSpace();
                    previewEditor.OnPreviewSettings();
                }
                var previewRect = GUILayoutUtility.GetRect(contentWidth, previewHeight, GUILayout.Width(contentWidth), GUILayout.Height(previewHeight));
                previewEditor.OnInteractivePreviewGUI(previewRect, EditorStyles.helpBox);
                PreviewResizeGUI();
            }

            var editable = IsEditable(item.path);
            NameGUI(item, editable);
            GUILayout.Label(item.path, new GUIStyle(EditorStyles.miniLabel) { wordWrap = true });
            if (!editable)
                ReadOnlyNote("This material can't be changed: it is locked by version control or in a read-only package");
            var metadata = MetadataOf(material);
            InfoRow("Shader", material.shader != null ? material.shader.name : "None");
            if (material.mainTexture != null)
                InfoRow("Texture", $"{material.mainTexture.width} x {material.mainTexture.height}");
            if (metadata != null && metadata.hotspotLayout != null)
                InfoRow("Hotspots", 1 + (metadata.hotspotVariants?.Count ?? 0) + (metadata.hotspotSheets?.Count ?? 0) + " sheets (layout, variants and other sheets)");
            var uses = ChiselSceneMaterialUsage.SurfacesUsing(material);
            if (isDecal)
                DecalInfoGUI(material);
            else
            {
                InfoRow("In scene", uses == 1 ? "1 surface" : uses + " surfaces");
                if (metadata != null && metadata.hotspotLayout != null)
                    HotspotBoxGUI(material, metadata);
            }

            // everything stored in the material shows read-only when it can't be changed
            EditorGUI.BeginDisabledGroup(!editable);
            GUILayout.Space(8);
            LabelsGUI(material, contentWidth);

            GUILayout.Space(8);
            GUILayout.Label(new GUIContent("MATERIAL SETTINGS", SettingsTooltip(isDecal, uses)), EditorStyles.miniBoldLabel);
            if (metadata == null)
            {
                if (GUILayout.Button(kAddParametersText))
                {
                    AddSurfaceParameters(material);
                    MarkDirty();
                    Select(item.guid);
                    GUIUtility.ExitGUI();
                }
            } else
            {
                Editor.CreateCachedEditor(metadata, typeof(ChiselSurfaceMetadataEditor), ref metadataEditor);
                metadataEditor?.OnInspectorGUI();
            }

            EditorGUI.EndDisabledGroup();
        }

        internal const float kMinPreviewHeight = 60;
        internal const float kMaxPreviewHeight = 1000;
        [SerializeField] float previewHeight = 220;
        bool draggingPreview;

        internal static float ClampPreviewHeight(float height) { return Mathf.Clamp(height, kMinPreviewHeight, kMaxPreviewHeight); }

        // a handle under the preview: dragging it makes the preview taller or shorter
        void PreviewResizeGUI()
        {
            var handle = GUILayoutUtility.GetRect(0, 6, GUILayout.ExpandWidth(true));
            EditorGUIUtility.AddCursorRect(handle, MouseCursor.ResizeVertical);
            var evt = Event.current;
            if (evt.type == EventType.Repaint)
                EditorGUI.DrawRect(new Rect(handle.center.x - 12, handle.center.y - 1, 24, 2), new Color(1, 1, 1, 0.25f));
            switch (evt.type)
            {
                case EventType.MouseDown when evt.button == 0 && handle.Contains(evt.mousePosition):
                    draggingPreview = true;
                    evt.Use();
                    break;
                case EventType.MouseDrag when draggingPreview:
                    previewHeight = ClampPreviewHeight(previewHeight + evt.delta.y);
                    Repaint();
                    evt.Use();
                    break;
                case EventType.MouseUp when draggingPreview:
                    draggingPreview = false;
                    evt.Use();
                    break;
            }
        }

        static readonly GUIContent kSearchTooltip = new(string.Empty, "Find by name; type l: and a label to find the materials with it");

        // What the material settings apply to, said in their header's tooltip
        internal static string SettingsTooltip(bool isDecal, int uses)
        {
            return isDecal  ? "Applies to every decal of this material" :
                   uses == 1 ? "Applies to the 1 surface using this material" :
                   uses > 1  ? $"Applies to all {uses} surfaces using this material" : "Applies to every surface using this material";
        }

        // That something can't be changed, the reason in its tooltip
        internal static void ReadOnlyNote(string reason)
        {
            GUILayout.Label(new GUIContent("Read only", reason), EditorStyles.miniBoldLabel);
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
            var group = HotspotTileChoice.GroupsOf(ChiselSurfaceSelectionManager.Selection, (_, _) => null).Find(g => g.material == material);
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                InfoRow("Hotspot tile", group != null ? HotspotTileChoice.ChoiceOf(group) : "-");
                using (new EditorGUILayout.HorizontalScope())
                {
                    using (new EditorGUI.DisabledScope(group == null))
                    {
                        var choose = new GUIContent("Choose tile...", group == null ? "None of the selected surfaces use this material" : "Choose the tile the selected surfaces using this material keep");
                        if (GUILayout.Button(choose))
                            HotspotTilePicker.Show(GUILayoutUtility.GetLastRect(), () => ChiselSurfaceSelectionManager.Selection, material);
                    }
                    if (GUILayout.Button("Edit layout..."))
                        ChiselHotspotLayoutWindow.Open(metadata.hotspotLayout, material);
                }
            }
        }

        // Its labels as chips; Enter adds the typed one
        void LabelsGUI(UnityEngine.Object material, float width)
        {
            var current = ChiselAssetLabels.Get(material);
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Label("Labels", EditorStyles.miniBoldLabel);
                GUILayout.FlexibleSpace();
            }
            Chips(current, width, label => { ChiselAssetLabels.Remove(material, label); MarkDirty(); }, "x", applied: true);

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
            Chips(suggestions.ToArray(), width, label => { ChiselAssetLabels.Add(material, label); newLabel = string.Empty; MarkDirty(); }, "+", prefix: true);
        }

        internal const int kChipSpacing = 3;

        /// <summary>A label chip: apart from the ones beside and below it</summary>
        internal static GUIStyle ChipStyle()
        {
            return new GUIStyle(EditorStyles.miniButton)
            {
                stretchWidth = false,
                margin = new RectOffset(0, kChipSpacing, 0, kChipSpacing)
            };
        }

        /// <summary>The tint of a label chip: a label it has stands out from the ones that can be added</summary>
        internal static Color ChipTint(bool applied)
        {
            return applied ? new Color(0.45f, 0.7f, 1f) : Color.white;
        }

        /// <summary>The outline of a label chip: a label it has is outlined, one that can be added isn't (clear)</summary>
        internal static Color ChipBorder(bool applied)
        {
            return applied ? new Color(0.4f, 0.7f, 1f, 1f) : Color.clear;
        }

        // rounded as a button's corners are
        internal const float kChipCornerRadius = 3;

        static void DrawBorder(Rect rect, Color color)
        {
            GUI.DrawTexture(rect, Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0, color, 1, kChipCornerRadius);
        }

        // applied: the item's own labels (a click removes one), else labels that can be added
        static void Chips(string[] items, float maxWidth, Action<string> clicked, string mark, bool prefix = false, bool applied = false)
        {
            if (items.Length == 0)
                return;
            var style = ChipStyle();
            var oldTint = GUI.backgroundColor;
            GUI.backgroundColor = ChipTint(applied);
            try
            {
                var width = 0f;
                EditorGUILayout.BeginHorizontal();
                foreach (var item in items)
                {
                    var text = prefix ? mark + " " + item : item + " " + mark;
                    var size = style.CalcSize(new GUIContent(text)).x + style.margin.horizontal;
                    if (width + size > maxWidth && width > 0)
                    {
                        GUILayout.FlexibleSpace();
                        EditorGUILayout.EndHorizontal();
                        EditorGUILayout.BeginHorizontal();
                        width = 0;
                    }
                    width += size;
                    var pressed = GUILayout.Button(text, style);
                    var border = ChipBorder(applied);
                    if (border.a > 0 && Event.current.type == EventType.Repaint)
                        DrawBorder(GUILayoutUtility.GetLastRect(), border);
                    if (pressed)
                    {
                        clicked(item);
                        GUIUtility.ExitGUI();
                    }
                }
                GUILayout.FlexibleSpace();
                EditorGUILayout.EndHorizontal();
            }
            finally { GUI.backgroundColor = oldTint; }
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
                return "No surfaces selected";
            var nodes = new HashSet<ChiselNodeComponent>();
            var materials = new HashSet<Material>();
            foreach (var surface in surfaces)
            {
                if (surface.node) nodes.Add(surface.node);
                materials.Add(surface.RenderMaterial);
            }
            var text = $"{surfaces.Count} surface{(surfaces.Count == 1 ? "" : "s")} on {nodes.Count} brush{(nodes.Count == 1 ? "" : "es")}";
            text += materials.Count > 1 ? $"  -  {materials.Count} materials (mixed)" : "";
            return text;
        }

        void FooterGUI(Rect rect)
        {
            GUILayout.BeginArea(new Rect(rect.x + 8, rect.y + 4, rect.width - 16, rect.height - 6));
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Label("Selection  " + SelectionText(), EditorStyles.miniLabel);
                GUILayout.FlexibleSpace();
                // a material is applied as it's selected; a decal is placed, which makes something new, so on asking
                var material = Load(Selected);
                if (ChiselSurfaceBrowserDecals.IsDecal(material))
                {
                    using (new EditorGUI.DisabledScope(!CanPlace(material)))
                    {
                        if (GUILayout.Button(new GUIContent(PlaceActionText(material), PlaceActionTooltip(material)), GUILayout.Width(120)))
                            PlaceSelectedDecal();
                    }
                }
            }
            GUILayout.EndArea();
        }

        /// <summary>A material picked goes on what's selected: the part clicked, else the selected surfaces. A decal
        /// material is placed only on asking</summary>
        void ApplyOnSelection(Material material)
        {
            if (material == null || ChiselSurfaceBrowserDecals.IsDecal(material))
                return;
            if (ApplyToSelectedPart(material))
                return;
            if (ChiselSurfaceSelectionManager.Selection.Count > 0)
                Apply(material);
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
