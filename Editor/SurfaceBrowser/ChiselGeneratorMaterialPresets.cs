using System;
using System.Collections.Generic;
using Chisel.Components;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Chisel.Editors
{
    // Making generator material presets and putting them on generators, from the Surface Browser and the inspector
    public static class ChiselGeneratorMaterialPresets
    {
        public const string kApplyUndoName = "Apply Material Preset";

        /// <summary>Puts a preset's materials on the generators' parts it sets, in one undo step; how many changed</summary>
        public static int Apply(ChiselGeneratorMaterialPreset preset, IReadOnlyList<ChiselGeneratorComponent> generators)
        {
            if (preset == null || generators == null)
                return 0;
            var changed = new List<ChiselGeneratorComponent>();
            foreach (var generator in generators)
            {
                if (generator != null && !changed.Contains(generator) && preset.ChangesFor(generator).Count > 0)
                    changed.Add(generator);
            }
            ChiselSurfaceBrowserPrefs.MarkUsed(ChiselSurfaceBrowserPrefs.ItemOf(preset));
            if (changed.Count == 0)
                return 0;
            Undo.RecordObjects(changed.ToArray(), kApplyUndoName);
            foreach (var generator in changed)
            {
                var surfaces = generator.SurfaceDefinition.surfaces;
                foreach (var change in preset.ChangesFor(generator))
                    surfaces[change.Key].RenderMaterial = change.Value;
                // rebuilt, so the scene shows it
                generator.SetDirty();
                EditorUtility.SetDirty(generator);
            }
            ChiselSceneMaterialUsage.SetDirty();
            return changed.Count;
        }

        /// <summary>A new preset asset from a generator as it is now, at a path in the project</summary>
        public static ChiselGeneratorMaterialPreset Create(ChiselGeneratorComponent generator, string path)
        {
            if (generator == null || string.IsNullOrEmpty(path))
                return null;
            var preset = ScriptableObject.CreateInstance<ChiselGeneratorMaterialPreset>();
            preset.CopyFrom(generator);
            AssetDatabase.CreateAsset(preset, AssetDatabase.GenerateUniqueAssetPath(path));
            AssetDatabase.SaveAssets();
            return preset;
        }

        /// <summary>Asks where to save a generator's materials as a preset, and saves it there</summary>
        public static ChiselGeneratorMaterialPreset SaveAs(ChiselGeneratorComponent generator)
        {
            if (generator == null)
                return null;
            var path = EditorUtility.SaveFilePanelInProject("Save Material Preset", generator.name + " Materials", "asset",
                                                            "Save the materials of this generator's parts as a preset");
            var preset = Create(generator, path);
            if (preset != null)
                EditorGUIUtility.PingObject(preset);
            return preset;
        }

        public const string kPresetsFolder = "Assets/Material Presets";

        /// <summary>Where a new preset goes: the one folder picked in the browser when it can hold assets, else the
        /// presets' own folder</summary>
        public static string FolderFor(IReadOnlyList<string> folders)
        {
            if (folders != null && folders.Count == 1 && !string.IsNullOrEmpty(folders[0]) &&
                (folders[0] == "Assets" || folders[0].StartsWith("Assets/", StringComparison.Ordinal)))
                return folders[0];
            return kPresetsFolder;
        }

        /// <summary>A new preset from a generator as it is now, in a folder (made when it isn't there), named after it</summary>
        public static ChiselGeneratorMaterialPreset CreateIn(ChiselGeneratorComponent generator, string folder)
        {
            if (generator == null || string.IsNullOrEmpty(folder))
                return null;
            if (!AssetDatabase.IsValidFolder(folder))
            {
                var slash = folder.LastIndexOf('/');
                if (slash <= 0 || !AssetDatabase.IsValidFolder(folder.Substring(0, slash)))
                    return null;
                AssetDatabase.CreateFolder(folder.Substring(0, slash), folder.Substring(slash + 1));
            }
            return Create(generator, $"{folder}/{generator.name} Materials.asset");
        }

        /// <summary>A copy of a preset beside it</summary>
        public static ChiselGeneratorMaterialPreset Duplicate(ChiselGeneratorMaterialPreset preset)
        {
            var path = AssetDatabase.GetAssetPath(preset);
            if (string.IsNullOrEmpty(path))
                return null;
            var copy = AssetDatabase.GenerateUniqueAssetPath(path);
            return AssetDatabase.CopyAsset(path, copy) ? AssetDatabase.LoadAssetAtPath<ChiselGeneratorMaterialPreset>(copy) : null;
        }

        /// <summary>Sets the material a preset gives one of its parts (null: it leaves the part as it is), undoably</summary>
        public static void SetMaterial(ChiselGeneratorMaterialPreset preset, int part, Material material)
        {
            if (preset == null || part < 0 || part >= preset.parts.Count || preset.parts[part].material == material)
                return;
            Undo.RecordObject(preset, "Change Material Preset");
            var changed = preset.parts[part];
            changed.material = material;
            preset.parts[part] = changed;
            EditorUtility.SetDirty(preset);
            if (EditorUtility.IsPersistent(preset))
                AssetDatabase.SaveAssetIfDirty(preset);
        }

        /// <summary>Takes a generator's materials into a preset, undoably</summary>
        public static void UpdateFrom(ChiselGeneratorMaterialPreset preset, ChiselGeneratorComponent generator)
        {
            if (preset == null || generator == null)
                return;
            Undo.RecordObject(preset, "Update Material Preset");
            preset.CopyFrom(generator);
            EditorUtility.SetDirty(preset);
            if (EditorUtility.IsPersistent(preset))
                AssetDatabase.SaveAssetIfDirty(preset);
        }

        /// <summary>Whether a generator is dressed with exactly a preset's materials: every one of its parts has the
        /// material the preset sets for it</summary>
        public static bool Dresses(ChiselGeneratorMaterialPreset preset, ChiselGeneratorComponent generator)
        {
            var surfaces = generator != null ? generator.SurfaceDefinition?.surfaces : null;
            if (preset == null || surfaces == null)
                return false;
            var any = false;
            for (int i = 0; i < surfaces.Length; i++)
            {
                if (surfaces[i] == null || !generator.IsSurfaceListed(i))
                    continue;
                var material = preset.MaterialFor(ChiselGeneratorMaterialPreset.PartName(generator, i));
                if (material == null || material != surfaces[i].RenderMaterial)
                    return false;
                any = true;
            }
            return any;
        }

        /// <summary>The preset all the generators are dressed with, one made for their kind first; null when none is</summary>
        public static ChiselGeneratorMaterialPreset PresetDressing(IEnumerable<ChiselGeneratorMaterialPreset> presets, IReadOnlyList<ChiselGeneratorComponent> generators)
        {
            if (presets == null || generators == null || generators.Count == 0)
                return null;
            ChiselGeneratorMaterialPreset otherKind = null;
            foreach (var preset in presets)
            {
                var all = true;
                foreach (var generator in generators)
                {
                    if (!Dresses(preset, generator))
                    {
                        all = false;
                        break;
                    }
                }
                if (!all)
                    continue;
                if (preset.IsFor(generators[0]))
                    return preset;
                otherKind ??= preset;
            }
            return otherKind;
        }

        /// <summary>Every preset in the project</summary>
        public static List<ChiselGeneratorMaterialPreset> All()
        {
            var presets = new List<ChiselGeneratorMaterialPreset>();
            foreach (var guid in AssetDatabase.FindAssets("t:" + nameof(ChiselGeneratorMaterialPreset)))
            {
                var preset = AssetDatabase.LoadAssetAtPath<ChiselGeneratorMaterialPreset>(AssetDatabase.GUIDToAssetPath(guid));
                if (preset != null)
                    presets.Add(preset);
            }
            presets.Sort((a, b) => string.Compare(a.name, b.name, StringComparison.OrdinalIgnoreCase));
            return presets;
        }

        /// <summary>A kind of generator as people say it: "Linear Stairs"</summary>
        public static string KindName(string kind)
        {
            if (string.IsNullOrEmpty(kind))
                return "Generator";
            var dot = kind.LastIndexOf('.');
            var name = dot >= 0 ? kind.Substring(dot + 1) : kind;
            if (name.StartsWith("Chisel", StringComparison.Ordinal))
                name = name.Substring("Chisel".Length);
            if (name.EndsWith("Component", StringComparison.Ordinal))
                name = name.Substring(0, name.Length - "Component".Length);
            return ObjectNames.NicifyVariableName(name);
        }

        /// <summary>A menu of the presets to put on the generators: those made for their kind first, the others after</summary>
        public static GenericMenu ApplyMenu(IReadOnlyList<ChiselGeneratorComponent> generators, Action applied = null)
        {
            var menu = new GenericMenu();
            var first = generators != null && generators.Count > 0 ? generators[0] : null;
            var others = new List<ChiselGeneratorMaterialPreset>();
            var any = false;
            foreach (var preset in All())
            {
                if (!preset.IsFor(first))
                {
                    others.Add(preset);
                    continue;
                }
                AddApply(menu, preset.name, preset, generators, applied);
                any = true;
            }
            foreach (var preset in others)
                AddApply(menu, $"{KindName(preset.generator)}/{preset.name}", preset, generators, applied);
            if (!any && others.Count == 0)
                menu.AddDisabledItem(new GUIContent("No material presets yet"));
            return menu;
        }

        static void AddApply(GenericMenu menu, string path, ChiselGeneratorMaterialPreset preset,
                             IReadOnlyList<ChiselGeneratorComponent> generators, Action applied)
        {
            menu.AddItem(new GUIContent(path), false, () => { Apply(preset, generators); applied?.Invoke(); });
        }

        /// <summary>The inspector's row: apply a preset to the generators shown, or save the one shown as a preset</summary>
        public static void InspectorGUI(Object[] targets)
        {
            var presetContent = new GUIContent("Material Preset", "Give all of the generator's parts their materials in one go");
            var applyContent  = new GUIContent("Apply...", "Put a preset's materials on the parts it sets");
            var saveContent   = new GUIContent("Save as Preset...", "Save the materials of this generator's parts as a preset");
            var generators = new List<ChiselGeneratorComponent>();
            foreach (var target in targets)
            {
                if (target is ChiselGeneratorComponent generator)
                    generators.Add(generator);
            }
            if (generators.Count == 0)
                return;
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.PrefixLabel(presetContent);
                var rect = GUILayoutUtility.GetRect(applyContent, EditorStyles.miniButtonLeft);
                if (GUI.Button(rect, applyContent, EditorStyles.miniButtonLeft))
                    ApplyMenu(generators).DropDown(rect);
                using (new EditorGUI.DisabledScope(generators.Count != 1))
                {
                    if (GUILayout.Button(saveContent, EditorStyles.miniButtonRight))
                    {
                        SaveAs(generators[0]);
                        GUIUtility.ExitGUI();
                    }
                }
            }
        }
    }
}
