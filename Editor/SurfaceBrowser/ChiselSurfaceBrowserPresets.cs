using System.Collections.Generic;
using Chisel.Components;
using UnityEditor;
using UnityEngine;

namespace Chisel.Editors
{
    // Generator material presets in the browser: listed, searched, labeled and made favorites like materials; picked,
    // a preset goes on the selected generators
    public sealed partial class ChiselSurfaceBrowserWindow
    {
        static ChiselGeneratorMaterialPreset LoadPreset(ChiselSurfaceBrowserModel.Item item)
        {
            return item != null && item.preset ? AssetDatabase.LoadAssetAtPath<ChiselGeneratorMaterialPreset>(item.path) : null;
        }

        static void AddPresetItems(List<ChiselSurfaceBrowserModel.Item> items)
        {
            foreach (var guid in AssetDatabase.FindAssets("t:" + nameof(ChiselGeneratorMaterialPreset), new[] { "Assets", "Packages" }))
            {
                var path  = AssetDatabase.GUIDToAssetPath(guid);
                var asset = AssetDatabase.LoadMainAssetAtPath(path);
                if (asset is not ChiselGeneratorMaterialPreset)
                    continue;
                items.Add(new ChiselSurfaceBrowserModel.Item
                {
                    guid   = guid,
                    path   = path,
                    name   = System.IO.Path.GetFileNameWithoutExtension(path),
                    labels = ChiselAssetLabels.Get(asset),
                    preset = true
                });
            }
        }

        // the browser lists presets, else materials; the two don't mix
        [SerializeField] bool presetMode;

        internal bool PresetMode => presetMode;

        /// <summary>Lists presets or materials. In the presets' mode the selected generators' preset is selected</summary>
        internal void SetPresetMode(bool presets)
        {
            if (presetMode == presets && model.presets == presets)
                return;
            presetMode = presets;
            model.presets = presets;
            if (!ChiselSurfaceBrowserModel.AppliesToPresets((ChiselSurfaceBrowserModel.QuickAccess)quick))
                quick = (int)ChiselSurfaceBrowserModel.QuickAccess.All;
            folders.Clear();
            label = null;
            Select(null);
            folderTree = model.BuildFolders(includeHidden: false);
            labels = model.Labels();
            Filter();
            if (presets)
                SelectPresetOf(SelectedGenerators());
            Repaint();
        }

        // the preset made last, renamed on its card once the grid has it
        string renameAfterRefresh;

        /// <summary>A new preset from a generator, in the presets' folder picked (else the presets' own folder), shown
        /// among the presets and renamed in place</summary>
        void NewPreset(ChiselGeneratorComponent generator)
        {
            // a folder picked among the materials isn't one picked for presets
            var folder = ChiselGeneratorMaterialPresets.FolderFor(presetMode ? folders : null);
            var preset = ChiselGeneratorMaterialPresets.CreateIn(generator, folder);
            if (preset == null)
            {
                ShowNotification(new GUIContent("The preset couldn't be made there"));
                return;
            }
            var guid = ChiselSurfaceBrowserPrefs.ItemOf(preset);
            SetPresetMode(true);
            search = string.Empty;
            Select(guid);
            renameAfterRefresh = guid;
            MarkDirty();
        }

        void RenameNewPreset()
        {
            if (renameAfterRefresh == null)
                return;
            var item = Selected;
            if (item != null && item.guid == renameAfterRefresh)
                StartRename(item);
            renameAfterRefresh = null;
        }

        /// <summary>
        /// In the presets' mode, the preset these generators are dressed with is selected, or none when they aren't
        /// dressed with one, so a click on any preset puts it on them
        /// </summary>
        internal void SelectPresetOf(IReadOnlyList<ChiselGeneratorComponent> generators)
        {
            if (!presetMode || generators == null || generators.Count == 0)
                return;
            var presets = new List<ChiselGeneratorMaterialPreset>();
            var guids   = new List<string>();
            foreach (var other in model.Items)
            {
                if (!other.preset || !model.IsVisible(other))
                    continue;
                var preset = LoadPreset(other);
                if (preset == null)
                    continue;
                presets.Add(preset);
                guids.Add(other.guid);
            }
            var dressing = ChiselGeneratorMaterialPresets.PresetDressing(presets, generators);
            var guid = dressing != null ? guids[presets.IndexOf(dressing)] : null;
            if (guid != selectedGuid)
                Select(guid);
        }

        void OnSelectionChanged()
        {
            SelectPresetOf(SelectedGenerators());
            Repaint();
        }

        /// <summary>A preset picked goes on the selected generators</summary>
        void ApplyPresetOnSelection(ChiselGeneratorMaterialPreset preset)
        {
            if (preset == null)
                return;
            if (ChiselGeneratorMaterialPresets.Apply(preset, SelectedGenerators()) > 0)
                MarkDirty();
        }

        // the materials a preset sets, at most four different ones, to show it by
        static List<Material> MaterialsShown(ChiselGeneratorMaterialPreset preset)
        {
            var materials = new List<Material>();
            if (preset == null)
                return materials;
            foreach (var part in preset.parts)
            {
                if (part.material != null && !materials.Contains(part.material))
                    materials.Add(part.material);
                if (materials.Count == 4)
                    break;
            }
            return materials;
        }

        void MaterialThumbGUI(Rect rect, Material material)
        {
            if (material == null)
                return;
            if (ChiselSurfaceBrowserDecals.IsDecal(material))
            {
                DrawImage(rect, material, 0);
                return;
            }
            var texture = PreviewOf(material, IsSheet(material));
            if (texture != null)
                GUI.DrawTexture(rect, texture, ScaleMode.ScaleToFit);
            else
                Repaint();  // previews load in the background
        }

        void PresetCardGUI(Rect rect, ChiselSurfaceBrowserModel.Item item)
        {
            var evt = Event.current;
            var preset = LoadPreset(item);
            if (item.guid == selectedGuid)
                EditorGUI.DrawRect(rect, new Color(0.24f, 0.48f, 0.9f, 0.45f));
            var thumbRect = new Rect(rect.x + 4, rect.y + 4, thumbSize, thumbSize);
            if (evt.type == EventType.Repaint)
            {
                EditorGUI.DrawRect(thumbRect, new Color(0, 0, 0, 0.2f));
                // its materials side by side, two by two
                var materials = MaterialsShown(preset);
                var half = thumbSize * 0.5f;
                for (int i = 0; i < materials.Count; i++)
                {
                    var cell = materials.Count == 1 ? thumbRect
                             : new Rect(thumbRect.x + (i % 2) * half, thumbRect.y + (i / 2) * half, half, half);
                    MaterialThumbGUI(cell, materials[i]);
                }
            }

            var badge = new GUIStyle(EditorStyles.miniLabel) { normal = { textColor = Color.white } };
            var kind = preset != null ? ChiselGeneratorMaterialPresets.KindName(preset.generator) : "Preset";
            var readOnly = ReadOnlyReason(item.path);
            if (readOnly != null)
                ChiselSurfaceBrowserIcons.LockBadge(thumbRect, readOnly);
            var labelX = ChiselSurfaceBrowserIcons.LabelX(thumbRect, readOnly != null);
            GUI.Label(new Rect(labelX, thumbRect.y + 2, thumbRect.xMax - ChiselSurfaceBrowserIcons.kStarSize - 4 - labelX, 14), new GUIContent("Preset", $"Materials for {kind}"), badge);
            var favorite = ChiselSurfaceBrowserPrefs.IsFavorite(item.guid);
            if (ChiselSurfaceBrowserIcons.FavoriteButton(thumbRect, favorite))
            {
                ChiselSurfaceBrowserPrefs.SetFavorite(item.guid, !favorite);
                Filter();
                Repaint();
            }

            var nameRect = new Rect(rect.x, thumbRect.yMax + 2, rect.width, EditorGUIUtility.singleLineHeight);
            if (cardRenameGuid == item.guid)
            {
                CardRenameGUI(nameRect, item);
                return;
            }
            GUI.Label(nameRect, new GUIContent(item.name, $"{item.path}\nMaterials for {kind}"),
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
                    // put on the selected generators even when it was selected already: they may not have it
                    partsFocused = false;
                    Select(item.guid);
                    ApplyPresetOnSelection(preset);
                    GUIUtility.hotControl = 0;
                    evt.Use();
                    break;
                case EventType.ContextClick when preset != null:
                    Select(item.guid);
                    PresetMenu(item, preset).ShowAsContext();
                    evt.Use();
                    break;
            }
        }

        GenericMenu PresetMenu(ChiselSurfaceBrowserModel.Item item, ChiselGeneratorMaterialPreset preset)
        {
            var menu = new GenericMenu();
            menu.AddItem(new GUIContent("Show in Project Tab"), false, () => EditorGUIUtility.PingObject(preset));
            menu.AddItem(new GUIContent("Show In Explorer"), false, () => EditorUtility.RevealInFinder(item.path));
            var editable = IsEditable(item.path);
            if (editable)
                menu.AddItem(new GUIContent("Rename"), false, () => StartRename(item));
            else
                menu.AddDisabledItem(new GUIContent("Rename"));
            menu.AddItem(new GUIContent("Duplicate"), false, () =>
            {
                var copy = ChiselGeneratorMaterialPresets.Duplicate(preset);
                if (copy == null)
                    return;
                var guid = ChiselSurfaceBrowserPrefs.ItemOf(copy);
                Select(guid);
                renameAfterRefresh = guid;
                MarkDirty();
            });
            if (editable)
                menu.AddItem(new GUIContent("Delete..."), false, () =>
                {
                    if (!EditorUtility.DisplayDialog("Delete Material Preset", $"Move {item.name} to the trash?", "Delete", "Cancel"))
                        return;
                    AssetDatabase.MoveAssetToTrash(item.path);
                    Select(null);
                    MarkDirty();
                });
            else
                menu.AddDisabledItem(new GUIContent("Delete..."));
            menu.AddSeparator(string.Empty);
            var generators = SelectedGenerators();
            if (generators.Count > 0)
                menu.AddItem(new GUIContent("Apply to Selected Generators"), false, () => ApplyPresetOnSelection(preset));
            else
                menu.AddDisabledItem(new GUIContent("Apply to Selected Generators"));
            return menu;
        }

        void PresetDetailsGUI(ChiselSurfaceBrowserModel.Item item, ChiselGeneratorMaterialPreset preset, float contentWidth)
        {
            if (preset == null)
                return;
            var editable = IsEditable(item.path);
            NameGUI(item, editable);
            GUILayout.Label(item.path, new GUIStyle(EditorStyles.miniLabel) { wordWrap = true });
            GUILayout.Label("Material preset for " + ChiselGeneratorMaterialPresets.KindName(preset.generator), EditorStyles.miniLabel);
            if (!editable)
                ReadOnlyNote(ReadOnlyReason(item.path));
            GUILayout.Space(6);

            // picking it puts it on the selected generators; this takes theirs into it
            var generators = SelectedGenerators();
            using (new EditorGUI.DisabledScope(generators.Count != 1 || !editable))
            {
                if (GUILayout.Button(new GUIContent("Copy from Selected Generator", "Take the selected generator's materials, as it is now")))
                {
                    ChiselGeneratorMaterialPresets.UpdateFrom(preset, generators[0]);
                    MarkDirty();
                }
            }

            // each part's material: picked or dropped on it; none leaves the part as it is
            GUILayout.Space(6);
            GUILayout.Label(new GUIContent("PARTS", "A part without a material is left as it is"), EditorStyles.miniBoldLabel);
            using (new EditorGUI.DisabledScope(!editable))
            {
                for (int i = 0; i < preset.parts.Count; i++)
                {
                    var part = preset.parts[i];
                    var rect = GUILayoutUtility.GetRect(new GUIContent(part.name), EditorStyles.label, GUILayout.ExpandWidth(true), GUILayout.Height(20));
                    var thumb = new Rect(rect.x + 2, rect.y + 2, 16, 16);
                    if (Event.current.type == EventType.Repaint)
                        MaterialThumbGUI(thumb, part.material);
                    GUI.Label(new Rect(thumb.xMax + 4, rect.y, rect.width * 0.45f - thumb.width - 4, rect.height), new GUIContent(part.name, part.name));
                    var field = new Rect(rect.x + rect.width * 0.45f, rect.y + 1, rect.width * 0.55f, EditorGUIUtility.singleLineHeight);
                    EditorGUI.BeginChangeCheck();
                    var material = (Material)EditorGUI.ObjectField(field, part.material, typeof(Material), false);
                    if (EditorGUI.EndChangeCheck())
                    {
                        ChiselGeneratorMaterialPresets.SetMaterial(preset, i, material);
                        MarkDirty();
                    }
                }
            }
            GUILayout.Space(8);
            using (new EditorGUI.DisabledScope(!editable))
                LabelsGUI(preset, contentWidth);
        }
    }
}
