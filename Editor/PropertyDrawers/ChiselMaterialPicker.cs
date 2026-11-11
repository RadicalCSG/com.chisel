using System;
using System.Collections.Generic;
using System.IO;
using Chisel.Components;
using Chisel.Core;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace Chisel.Editors
{
    // Chisel's material field: the material's thumbnail and name; a click opens ChiselMaterialPicker, a material dragged
    // onto it is taken. What the picker or the eyedropper picks reaches the field on its next draw, inside the caller's
    // change check, so it is applied and undone as any edit of the field is
#if UNITY_6000_7_OR_NEWER
    [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
#endif
    public static class ChiselMaterialField
    {
        static readonly int kFieldHash = nameof(ChiselMaterialField).GetHashCode();

        // a material picked for a field (its object and property path), taken when the field is drawn next
        static (UnityEngine.Object target, string path, Material material)? s_Picked;
        // the field the eyedropper picks for, while it is active
        static (UnityEngine.Object target, string path)? s_Eyedropper;

        internal static bool EyedropperActive => s_Eyedropper.HasValue;

        // Picks a material for a field: it takes it when it is drawn next
        internal static void Pick(UnityEngine.Object target, string path, Material material)
        {
            s_Picked = (target, path, material);
            if (material != null)
                ChiselSurfaceBrowserPrefs.MarkUsed(ChiselSurfaceBrowserPrefs.ItemOf(material));
            InternalEditorUtility.RepaintAllViews();
        }

        // Gives a material property what was picked for it; true when it took something
        internal static bool TakePicked(SerializedProperty property)
        {
            if (!s_Picked.HasValue || property == null)
                return false;
            var picked = s_Picked.Value;
            if (picked.target != property.serializedObject.targetObject || picked.path != property.propertyPath)
                return false;
            s_Picked = null;
            property.objectReferenceValue = picked.material;
            GUI.changed = true;
            return true;
        }

        internal static void ClearPicked() { s_Picked = null; }

        public static void Draw(Rect position, SerializedProperty property)
        {
            TakePicked(property);
            var id = GUIUtility.GetControlID(kFieldHash, FocusType.Keyboard, position);
            var current = Event.current;
            var material = property.objectReferenceValue as Material;
            var mixed = property.hasMultipleDifferentValues;

            switch (current.type)
            {
                case EventType.DragUpdated:
                case EventType.DragPerform:
                    if (position.Contains(current.mousePosition) && Dragged() is Material dragged)
                    {
                        DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
                        if (current.type == EventType.DragPerform)
                        {
                            DragAndDrop.AcceptDrag();
                            property.objectReferenceValue = dragged;
                            ChiselSurfaceBrowserPrefs.MarkUsed(ChiselSurfaceBrowserPrefs.ItemOf(dragged));
                            GUI.changed = true;
                        }
                        current.Use();
                    }
                    break;
                case EventType.MouseDown when current.button == 0 && position.Contains(current.mousePosition):
                    GUIUtility.keyboardControl = id;
                    Open(position, property);
                    current.Use();
                    break;
                case EventType.KeyDown when GUIUtility.keyboardControl == id &&
                                            (current.keyCode == KeyCode.Return || current.keyCode == KeyCode.KeypadEnter || current.keyCode == KeyCode.Space):
                    Open(position, property);
                    current.Use();
                    break;
                case EventType.Repaint:
                {
                    var content = mixed ? new GUIContent(HotspotTileChoice.kDash) : material != null
                        ? new GUIContent(material.name, AssetPreview.GetMiniThumbnail(material))
                        : new GUIContent("None");
                    EditorStyles.popup.Draw(position, content, id, false, position.Contains(current.mousePosition));
                    break;
                }
            }
        }

        static UnityEngine.Object Dragged()
        {
            foreach (var dragged in DragAndDrop.objectReferences)
                if (dragged is Material) return dragged;
            return null;
        }

        static void Open(Rect position, SerializedProperty property)
        {
            var target = property.serializedObject.targetObject;
            var path = property.propertyPath;
            var current = property.hasMultipleDifferentValues ? null : property.objectReferenceValue as Material;
            PopupWindow.Show(position, new ChiselMaterialPicker(current, picked => Pick(target, path, picked), () => StartEyedropper(target, path)));
        }

        // The eyedropper: a click on a Chisel surface in the Scene view picks its material; anywhere else nothing, and
        // Escape stops it
        internal static void StartEyedropper(UnityEngine.Object target, string path)
        {
            s_Eyedropper = (target, path);
            SceneView.duringSceneGui -= EyedropperGUI;
            SceneView.duringSceneGui += EyedropperGUI;
            SceneView.lastActiveSceneView?.Focus();
            SceneView.RepaintAll();
        }

        internal static void StopEyedropper()
        {
            s_Eyedropper = null;
            SceneView.duringSceneGui -= EyedropperGUI;
            SceneView.RepaintAll();
        }

        static readonly int kEyedropperHash = "ChiselMaterialEyedropper".GetHashCode();
        static readonly List<SurfaceReference> s_Found = new();

        // The material of the Chisel surface at a point of a scene view, null when there is none
        static Material MaterialAt(Vector2 mousePosition, out bool overSurface)
        {
            s_Found.Clear();
            overSurface = ChiselClickSelectionManager.FindSurfaceReferences(mousePosition, false, s_Found, out _, out var surface) && surface != null;
            return overSurface ? surface.RenderMaterial : null;
        }

        // Picks the material of a surface for the field the eyedropper picks for, and stops it
        internal static bool PickFrom(SurfaceReference surface)
        {
            if (!s_Eyedropper.HasValue || surface == null)
                return false;
            var field = s_Eyedropper.Value;
            StopEyedropper();
            Pick(field.target, field.path, surface.RenderMaterial);
            return true;
        }

        static void EyedropperGUI(SceneView sceneView)
        {
            if (!s_Eyedropper.HasValue)
            {
                SceneView.duringSceneGui -= EyedropperGUI;
                return;
            }
            var current = Event.current;
            var id = GUIUtility.GetControlID(kEyedropperHash, FocusType.Passive);
            switch (current.GetTypeForControl(id))
            {
                case EventType.Layout:
                    HandleUtility.AddDefaultControl(id);
                    break;
                case EventType.MouseMove:
                    sceneView.Repaint();
                    break;
                case EventType.Repaint:
                {
                    MaterialAt(current.mousePosition, out var over);
                    EditorGUIUtility.AddCursorRect(new Rect(0, 0, sceneView.position.width, sceneView.position.height), over ? MouseCursor.ArrowPlus : MouseCursor.NotAllowed);
                    break;
                }
                case EventType.MouseDown when current.button == 0:
                {
                    MaterialAt(current.mousePosition, out var over);
                    if (over)
                    {
                        s_Found.Clear();
                        ChiselClickSelectionManager.FindSurfaceReferences(current.mousePosition, false, s_Found, out _, out var surface);
                        PickFrom(surface);
                    }
                    current.Use();
                    break;
                }
                case EventType.KeyDown when current.keyCode == KeyCode.Escape:
                    StopEyedropper();
                    current.Use();
                    break;
            }
        }
    }

    // A compact material picker for Chisel's material fields: search, an eyedropper for Chisel surfaces, the recently
    // used materials, the materials beside the current one (or found by the search), None, and the Surface Browser.
    // Hidden folders are left out
    sealed class ChiselMaterialPicker : PopupWindowContent
    {
        const float kWidth     = 320;
        const float kPad       = 6;
        const float kRow       = 24;
        const float kThumb     = 42;
        const int   kMaxRows   = 10;
        const int   kMaxFound  = 200;
        const string kSearchControl = "ChiselMaterialPickerSearch";


        const string kEyedropperTooltip = "Pick a material from a Chisel surface in the Scene view (Escape stops)";
        static readonly GUIContent kNoneContent      = new("None", "No material");
        static readonly GUIContent kBrowserContent    = new("Open Surface Browser", "Open the Surface Browser with this material selected");
        static readonly GUIContent kHotspotContent    = new(string.Empty, "Has a hotspot layout");

        readonly Material current;
        readonly Action<Material> pick;
        readonly Action eyedropper;
        readonly GUIContent eyedropperContent = EyedropperContent();
        readonly List<string> allPaths;
        readonly List<string> recent;
        string search = string.Empty;
        List<string> listed;
        string listTitle;
        int highlighted = -1;
        Vector2 scroll;
        bool focused;

        public ChiselMaterialPicker(Material current, Action<Material> pick, Action eyedropper = null)
        {
            this.current = current;
            this.pick = pick;
            this.eyedropper = eyedropper;
            allPaths = MaterialPaths(ChiselProjectSettings.IsHidden);
            recent = RecentPaths(ChiselSurfaceBrowserPrefs.Recent, AssetDatabase.GUIDToAssetPath, ChiselProjectSettings.IsHidden);
            UpdateList();
        }

        internal IReadOnlyList<string> Listed => listed;
        internal string ListTitle => listTitle;
        internal IReadOnlyList<string> RecentlyUsed => recent;
        internal string Search { get => search; set { search = value ?? string.Empty; UpdateList(); } }

        // Unity's eyedropper icon, as the color field has it
        internal static GUIContent EyedropperContent()
        {
            var icon = EditorGUIUtility.isProSkin ? EditorGUIUtility.FindTexture("d_eyeDropper.Large") : null;
            if (icon == null)
                icon = EditorGUIUtility.FindTexture("eyeDropper.Large");
            return icon != null ? new GUIContent(icon, kEyedropperTooltip) : new GUIContent("Pick", kEyedropperTooltip);
        }

        // Every material's path in the project, but for those in hidden folders
        internal static List<string> MaterialPaths(Func<string, bool> isHidden)
        {
            var paths = new List<string>();
            foreach (var guid in AssetDatabase.FindAssets("t:Material"))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (path.EndsWith(".mat", StringComparison.OrdinalIgnoreCase) && !isHidden(path))
                    paths.Add(path);
            }
            paths.Sort(StringComparer.OrdinalIgnoreCase);
            return paths;
        }

        // The recently used materials (item guids, the most recent first) that are materials and aren't hidden
        internal static List<string> RecentPaths(IReadOnlyList<string> items, Func<string, string> pathOf, Func<string, bool> isHidden)
        {
            var paths = new List<string>();
            foreach (var item in items)
            {
                if (item.Contains(":"))
                    continue;   // a decal image of a sheet
                var path = pathOf(item);
                if (!string.IsNullOrEmpty(path) && path.EndsWith(".mat", StringComparison.OrdinalIgnoreCase) && !isHidden(path) && !paths.Contains(path))
                    paths.Add(path);
            }
            return paths;
        }

        internal static string FolderOf(string path) => string.IsNullOrEmpty(path) ? string.Empty : Path.GetDirectoryName(path).Replace('\\', '/');

        // The materials directly in a folder
        internal static List<string> InFolder(IEnumerable<string> paths, string folder)
        {
            var found = new List<string>();
            foreach (var path in paths)
                if (string.Equals(FolderOf(path), folder, StringComparison.OrdinalIgnoreCase)) found.Add(path);
            return found;
        }

        // The materials whose name has the search in it, ignoring case
        internal static List<string> Found(IEnumerable<string> paths, string search, int max)
        {
            var found = new List<string>();
            foreach (var path in paths)
            {
                if (Path.GetFileNameWithoutExtension(path).IndexOf(search.Trim(), StringComparison.OrdinalIgnoreCase) < 0)
                    continue;
                found.Add(path);
                if (found.Count >= max)
                    break;
            }
            return found;
        }

        void UpdateList()
        {
            if (!string.IsNullOrWhiteSpace(search))
            {
                listed = Found(allPaths, search, kMaxFound);
                listTitle = "FOUND";
            } else
            {
                var currentPath = current != null ? AssetDatabase.GetAssetPath(current) : null;
                var folder = FolderOf(!string.IsNullOrEmpty(currentPath) ? currentPath : recent.Count > 0 ? recent[0] : "Assets/x");
                listed = InFolder(allPaths, folder);
                listTitle = folder.ToUpperInvariant();
            }
            var currentAt = current != null ? listed.IndexOf(AssetDatabase.GetAssetPath(current)) : -1;
            highlighted = listed.Count == 0 ? -1 : Mathf.Max(0, currentAt);
        }

        float ListHeight => Mathf.Max(1, Mathf.Min(listed.Count, kMaxRows)) * kRow;
        float RecentHeight => recent.Count > 0 ? 18 + kThumb + kPad : 0;

        public override Vector2 GetWindowSize()
        {
            return new Vector2(kWidth, kPad + 22 + kPad + RecentHeight + 18 + kMaxRows * kRow + kPad + 24 + kPad);
        }

        public override void OnOpen()
        {
            editorWindow.wantsMouseMove = true;
        }

        public override void OnGUI(Rect rect)
        {
            var currentEvent = Event.current;
            if (currentEvent.type == EventType.KeyDown)
                Keys(currentEvent);

            // search and the eyedropper
            var y = rect.y + kPad;
            var eyeWidth = eyedropperContent.image != null ? 28f : 44f;
            GUI.SetNextControlName(kSearchControl);
            EditorGUI.BeginChangeCheck();
            var typed = EditorGUI.TextField(new Rect(rect.x + kPad, y + 2, rect.width - 3 * kPad - eyeWidth, 18), search, EditorStyles.toolbarSearchField);
            if (EditorGUI.EndChangeCheck())
                Search = typed;
            if (!focused)
            {
                EditorGUI.FocusTextInControl(kSearchControl);
                focused = true;
            }
            using (new EditorGUI.DisabledScope(eyedropper == null))
            {
                if (GUI.Button(new Rect(rect.xMax - kPad - eyeWidth, y, eyeWidth, 22), eyedropperContent, EditorStyles.miniButton))
                {
                    eyedropper?.Invoke();
                    editorWindow.Close();
                    GUIUtility.ExitGUI();
                }
            }
            y += 22 + kPad;

            // recently used
            if (recent.Count > 0)
            {
                Header(new Rect(rect.x, y, rect.width, 18), "RECENT");
                y += 18;
                var x = rect.x + kPad;
                foreach (var path in recent)
                {
                    if (x + kThumb > rect.xMax - kPad)
                        break;
                    var cell = new Rect(x, y + 2, kThumb, kThumb);
                    var material = AssetDatabase.LoadAssetAtPath<Material>(path);
                    if (currentEvent.type == EventType.Repaint)
                    {
                        if (material == current && current != null)
                            EditorGUI.DrawRect(new Rect(cell.x - 2, cell.y - 2, cell.width + 4, cell.height + 4), ChiselEditorColors.ChosenRow);
                        var preview = AssetPreview.GetAssetPreview(material) ?? AssetPreview.GetMiniThumbnail(material);
                        if (preview != null) GUI.DrawTexture(cell, preview, ScaleMode.ScaleToFit);
                    }
                    GUI.Label(cell, new GUIContent(string.Empty, material != null ? material.name : path));
                    if (currentEvent.type == EventType.MouseDown && currentEvent.button == 0 && cell.Contains(currentEvent.mousePosition))
                        Choose(material);
                    x += kThumb + 4;
                }
                y += kThumb + kPad;
            }

            // the folder's materials, or the found ones
            Header(new Rect(rect.x, y, rect.width, 18), listTitle);
            y += 18;
            var listRect = new Rect(rect.x, y, rect.width, kMaxRows * kRow);
            var content = new Rect(0, 0, rect.width - (listed.Count > kMaxRows ? 14 : 0), listed.Count * kRow);
            scroll = GUI.BeginScrollView(listRect, scroll, content);
            for (int i = 0; i < listed.Count; i++)
            {
                var row = new Rect(0, i * kRow, content.width, kRow);
                if (row.yMax < scroll.y || row.y > scroll.y + listRect.height)
                    continue;
                var material = AssetDatabase.LoadAssetAtPath<Material>(listed[i]);
                if (currentEvent.type == EventType.Repaint)
                {
                    if (i == highlighted)
                        EditorGUI.DrawRect(row, ChiselEditorColors.ChosenRow);
                    else if (row.Contains(currentEvent.mousePosition))
                        EditorGUI.DrawRect(row, ChiselEditorColors.RowHover);
                    var thumb = AssetPreview.GetMiniThumbnail(material);
                    if (thumb != null) GUI.DrawTexture(new Rect(row.x + kPad, row.y + 2, kRow - 4, kRow - 4), thumb, ScaleMode.ScaleToFit);
                    GUI.Label(new Rect(row.x + kPad + kRow + 2, row.y, row.width - kRow * 2 - kPad, row.height), Path.GetFileNameWithoutExtension(listed[i]));
                }
                if (HotspotTransformGUI.LayoutOf(material) != null)
                {
                    var icon = new Rect(row.xMax - kRow, row.y + 5, 14, 14);
                    if (currentEvent.type == EventType.Repaint)
                        DrawHotspotIcon(icon);
                    GUI.Label(icon, kHotspotContent);
                }
                if (currentEvent.type == EventType.MouseDown && currentEvent.button == 0 && row.Contains(currentEvent.mousePosition))
                    Choose(material);
                if (currentEvent.type == EventType.MouseMove && row.Contains(currentEvent.mousePosition))
                    editorWindow.Repaint();
            }
            GUI.EndScrollView();
            y += listRect.height + kPad;

            // None and the browser
            if (GUI.Button(new Rect(rect.x + kPad, y, 70, 22), kNoneContent))
                Choose(null);
            var browserWidth = EditorStyles.miniButton.CalcSize(kBrowserContent).x + 16;
            if (GUI.Button(new Rect(rect.xMax - kPad - browserWidth, y, browserWidth, 22), kBrowserContent))
            {
                var material = current;
                editorWindow.Close();
                if (material != null) ChiselSurfaceBrowserWindow.Open(material);
                else ChiselSurfaceBrowserWindow.Open();
                GUIUtility.ExitGUI();
            }
        }

        static void Header(Rect rect, string title)
        {
            if (Event.current.type == EventType.Repaint)
                EditorGUI.DrawRect(rect, ChiselEditorColors.Header);
            GUI.Label(new Rect(rect.x + kPad, rect.y, rect.width - 2 * kPad, rect.height), new GUIContent(title, title), EditorStyles.miniBoldLabel);
        }

        // a small sheet of tiles: a hotspot layout
        static void DrawHotspotIcon(Rect rect)
        {
            const float t = 1.5f;
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, rect.width, t), ChiselEditorColors.Highlight);
            EditorGUI.DrawRect(new Rect(rect.x, rect.yMax - t, rect.width, t), ChiselEditorColors.Highlight);
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, t, rect.height), ChiselEditorColors.Highlight);
            EditorGUI.DrawRect(new Rect(rect.xMax - t, rect.y, t, rect.height), ChiselEditorColors.Highlight);
            EditorGUI.DrawRect(new Rect(rect.x, rect.y + rect.height * 0.4f, rect.width, t), ChiselEditorColors.Highlight);
            EditorGUI.DrawRect(new Rect(rect.x + rect.width * 0.45f, rect.y + rect.height * 0.4f, t, rect.height * 0.6f), ChiselEditorColors.Highlight);
        }

        // Up and down go through the list, Enter picks, Escape closes
        void Keys(Event currentEvent)
        {
            switch (currentEvent.keyCode)
            {
                case KeyCode.DownArrow:
                case KeyCode.UpArrow:
                    if (listed.Count == 0)
                        return;
                    highlighted = Mathf.Clamp(highlighted + (currentEvent.keyCode == KeyCode.DownArrow ? 1 : -1), 0, listed.Count - 1);
                    var top = highlighted * kRow;
                    if (top < scroll.y) scroll.y = top;
                    else if (top + kRow > scroll.y + kMaxRows * kRow) scroll.y = top + kRow - kMaxRows * kRow;
                    currentEvent.Use();
                    editorWindow.Repaint();
                    break;
                case KeyCode.Return:
                case KeyCode.KeypadEnter:
                    if (highlighted >= 0 && highlighted < listed.Count)
                        Choose(AssetDatabase.LoadAssetAtPath<Material>(listed[highlighted]));
                    currentEvent.Use();
                    break;
                case KeyCode.Escape:
                    editorWindow.Close();
                    currentEvent.Use();
                    GUIUtility.ExitGUI();
                    break;
            }
        }

        void Choose(Material material)
        {
            pick?.Invoke(material);
            editorWindow.Close();
            GUIUtility.ExitGUI();
        }
    }
}
