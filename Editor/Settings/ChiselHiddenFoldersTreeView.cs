using System.Collections.Generic;
using Chisel.Core;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;

namespace Chisel.Editors
{
    // The project's folders, each ticked while the Surface Browser shows it, with how many materials it holds. A folder
    // hidden through its parent is unticked and can only be shown through its parent; one without materials is greyed
    sealed class ChiselHiddenFoldersTreeView : TreeView<int>
    {
        sealed class FolderItem : TreeViewItem<int>
        {
            public string path;
            public int    materials;
        }

        readonly Dictionary<string, int> materialCounts;

        public ChiselHiddenFoldersTreeView(TreeViewState<int> state, Dictionary<string, int> materialCounts) : base(state)
        {
            this.materialCounts = materialCounts;
            showAlternatingRowBackgrounds = true;
            Reload();
        }

        protected override TreeViewItem<int> BuildRoot()
        {
            var root = new TreeViewItem<int> { id = 0, depth = -1, displayName = "Root" };
            var nextId = 1;
            foreach (var top in new[] { "Assets", "Packages" })
                root.AddChild(Build(top, top, 0, ref nextId));
            SetupDepthsFromParentsAndChildren(root);
            return root;
        }

        FolderItem Build(string path, string name, int depth, ref int nextId)
        {
            materialCounts.TryGetValue(path, out var count);
            var item = new FolderItem { id = nextId++, depth = depth, displayName = name, path = path, materials = count };
            foreach (var sub in ChiselProjectSettingsEditor.SubFoldersOf(path))
            {
                var label = ChiselSurfaceBrowserModel.FolderLabel(sub, sub.Substring(sub.LastIndexOf('/') + 1));
                item.AddChild(Build(sub, label, depth + 1, ref nextId));
            }
            return item;
        }

        static string ParentOf(string path)
        {
            var slash = path.LastIndexOf('/');
            return slash > 0 ? path.Substring(0, slash) : string.Empty;
        }

        protected override void RowGUI(RowGUIArgs args)
        {
            var item = (FolderItem)args.item;
            var rect = args.rowRect;
            var indent = GetContentIndent(item);
            var parentHidden = ChiselProjectSettings.IsHidden(ParentOf(item.path));
            var hidden = ChiselProjectSettings.IsHidden(item.path);

            var toggleRect = new Rect(rect.x + indent, rect.y, 18, rect.height);
            using (new EditorGUI.DisabledScope(parentHidden))
            {
                var shown = EditorGUI.Toggle(toggleRect, new GUIContent(string.Empty, "Show this folder and everything in it in the Surface Browser"), !hidden);
                if (!parentHidden && shown == hidden)
                {
                    if (shown) ChiselProjectSettings.UnhideFolder(item.path);
                    else       ChiselProjectSettings.HideFolder(item.path);
                }
            }

            // one without materials is greyed, but can still be hidden
            var prev = GUI.color;
            if (item.materials == 0) GUI.color = new Color(prev.r, prev.g, prev.b, 0.45f);
            GUI.Label(new Rect(toggleRect.xMax + 2, rect.y, rect.xMax - toggleRect.xMax - 50, rect.height), new GUIContent(item.displayName, item.path));
            GUI.Label(new Rect(rect.xMax - 46, rect.y, 42, rect.height),
                      new GUIContent(item.materials.ToString(), item.materials == 1 ? "1 material" : item.materials + " materials"),
                      new GUIStyle(EditorStyles.miniLabel) { alignment = TextAnchor.MiddleRight });
            GUI.color = prev;
        }
    }
}
