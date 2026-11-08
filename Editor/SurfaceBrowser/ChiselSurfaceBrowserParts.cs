using System.Collections.Generic;
using Chisel.Components;
using UnityEditor;
using UnityEngine;

namespace Chisel.Editors
{
    // The parts of the selected generators (their surfaces by name: tread, riser, step nose...) and the material each
    // has; a material is put on a part by dropping it on its row, or from a material's menu
    public sealed partial class ChiselSurfaceBrowserWindow
    {
        public struct Part
        {
            public int      index;      // the generator's surface
            public string   name;
            public Material material;   // null: the parts' materials differ between the generators
            public bool     mixed;
        }

        /// <summary>The selected generators</summary>
        static List<ChiselGeneratorComponent> SelectedGenerators()
        {
            var generators = new List<ChiselGeneratorComponent>();
            foreach (var gameObject in Selection.gameObjects)
            {
                if (gameObject != null && gameObject.TryGetComponent<ChiselGeneratorComponent>(out var generator) && !generators.Contains(generator))
                    generators.Add(generator);
            }
            return generators;
        }

        /// <summary>
        /// The parts the generators have: their used surfaces, by name, in order. With several generators, the parts of
        /// the first that the others have too (the same surface, the same name), and whether their materials differ
        /// </summary>
        public static List<Part> PartsOf(IReadOnlyList<ChiselGeneratorComponent> generators)
        {
            var parts = new List<Part>();
            if (generators == null || generators.Count == 0 || generators[0] == null)
                return parts;
            var first = generators[0];
            var surfaces = first.SurfaceDefinition?.surfaces;
            if (surfaces == null)
                return parts;
            for (int i = 0; i < surfaces.Length; i++)
            {
                if (!first.IsSurfaceUsed(i))
                    continue;
                var name = first.GetSurfaceName(i);
                if (string.IsNullOrEmpty(name))
                    name = "Surface " + (i + 1);
                var part = new Part { index = i, name = name, material = surfaces[i]?.RenderMaterial };
                var shared = true;
                for (int g = 1; g < generators.Count && shared; g++)
                {
                    var other = generators[g];
                    var otherSurfaces = other != null ? other.SurfaceDefinition?.surfaces : null;
                    if (otherSurfaces == null || i >= otherSurfaces.Length || !other.IsSurfaceUsed(i) ||
                        (other.GetSurfaceName(i) ?? string.Empty) != (first.GetSurfaceName(i) ?? string.Empty))
                        shared = false;
                    else if (otherSurfaces[i]?.RenderMaterial != part.material)
                        part.mixed = true;
                }
                if (!shared)
                    continue;
                if (part.mixed)
                    part.material = null;
                parts.Add(part);
            }
            return parts;
        }

        // the part clicked last, -1 for none; the footer's button then puts the selected material on it
        [SerializeField] int selectedPart = -1;

        /// <summary>The parts the selected surfaces are of: those of the generators' surfaces that are selected</summary>
        public static HashSet<int> PartsOfSurfaces(IReadOnlyList<ChiselGeneratorComponent> generators, IEnumerable<SurfaceReference> surfaces)
        {
            var parts = new HashSet<int>();
            if (generators == null || surfaces == null)
                return parts;
            foreach (var surface in surfaces)
            {
                if (surface?.node == null)
                    continue;
                foreach (var generator in generators)
                {
                    // the generator's surface (its part) is the description, not the brush's polygon
                    if (surface.node == generator)
                    {
                        parts.Add(surface.descriptionIndex);
                        break;
                    }
                }
            }
            return parts;
        }

        /// <summary>The part clicked, when the selected generators still have it</summary>
        Part? SelectedPart(List<Part> parts)
        {
            foreach (var part in parts)
            {
                if (part.index == selectedPart)
                    return part;
            }
            return null;
        }

        /// <summary>Puts a material on the part clicked, of the selected generators; false when no part is selected</summary>
        bool ApplyToSelectedPart(Material material)
        {
            if (material == null || selectedPart < 0)
                return false;
            var generators = SelectedGenerators();
            if (SelectedPart(PartsOf(generators)) is not Part part || (!part.mixed && part.material == material))
                return false;
            ApplyToPart(generators, part.index, material);
            MarkDirty();
            return true;
        }

        /// <summary>Puts a material on a part (a surface) of each generator, undoably</summary>
        public static void ApplyToPart(IReadOnlyList<ChiselGeneratorComponent> generators, int surfaceIndex, Material material)
        {
            if (material == null || generators == null)
                return;
            var changed = new List<Object>();
            foreach (var generator in generators)
            {
                if (generator != null)
                    changed.Add(generator);
            }
            Undo.RecordObjects(changed.ToArray(), "Apply Material To Part");
            foreach (var generator in generators)
            {
                var surfaces = generator != null ? generator.SurfaceDefinition?.surfaces : null;
                if (surfaces == null || surfaceIndex < 0 || surfaceIndex >= surfaces.Length || surfaces[surfaceIndex] == null)
                    continue;
                surfaces[surfaceIndex].RenderMaterial = material;
                // rebuilt, so the scene shows it
                generator.SetDirty();
                EditorUtility.SetDirty(generator);
            }
            ChiselSurfaceBrowserPrefs.MarkUsed(ChiselSurfaceBrowserPrefs.ItemOf(material));
            ChiselSceneMaterialUsage.SetDirty();
        }

        void PartsGUI()
        {
            var generators = SelectedGenerators();
            var parts = PartsOf(generators);
            if (parts.Count == 0)
                return;
            GUILayout.Space(6);
            var title = generators.Count == 1 ? generators[0].name : $"{generators.Count} generators";
            GUILayout.Label(new GUIContent("PARTS OF " + title.ToUpperInvariant(), "Drop a material on a part to put it there"), EditorStyles.miniBoldLabel);
            var evt = Event.current;
            // the parts the surfaces selected in the scene are of, lighter than the one clicked
            var inScene = PartsOfSurfaces(generators, ChiselSurfaceSelectionManager.Selection);
            foreach (var part in parts)
            {
                var rect = GUILayoutUtility.GetRect(new GUIContent(part.name), EditorStyles.label, GUILayout.ExpandWidth(true), GUILayout.Height(20));
                var hovering = rect.Contains(evt.mousePosition);
                var dragged = hovering && DragAndDrop.objectReferences.Length == 1 && DragAndDrop.objectReferences[0] is Material;
                if (evt.type == EventType.Repaint)
                {
                    if (part.index == selectedPart)
                        EditorGUI.DrawRect(rect, new Color(0.24f, 0.48f, 0.9f, 0.5f));
                    else if (inScene.Contains(part.index))
                        EditorGUI.DrawRect(rect, new Color(0.24f, 0.48f, 0.9f, 0.22f));
                    if (dragged)
                        EditorGUI.DrawRect(rect, new Color(0.24f, 0.48f, 0.9f, 0.35f));
                }

                // the material itself, as in the grid: its preview, or a decal's image flat
                var thumb = new Rect(rect.x + 6, rect.y + 2, 16, 16);
                if (evt.type == EventType.Repaint && part.material != null)
                {
                    if (ChiselSurfaceBrowserDecals.IsDecal(part.material))
                        DrawImage(thumb, part.material, 0);
                    else
                    {
                        // never the generic material icon: nothing until the preview has loaded
                        var preview = IsSheet(part.material) && part.material.mainTexture != null
                                    ? part.material.mainTexture : AssetPreview.GetAssetPreview(part.material);
                        if (preview != null)
                            GUI.DrawTexture(thumb, preview, ScaleMode.ScaleToFit);
                        else if (AssetPreview.IsLoadingAssetPreview(part.material.GetEntityId()))
                            Repaint();
                    }
                }
                var materialName = part.mixed ? "(mixed)" : part.material != null ? part.material.name : "(none)";
                GUI.Label(new Rect(thumb.xMax + 4, rect.y, rect.width * 0.45f, rect.height), part.name);
                GUI.Label(new Rect(rect.x + rect.width * 0.5f, rect.y, rect.width * 0.5f - 4, rect.height), new GUIContent(materialName, materialName),
                          new GUIStyle(EditorStyles.miniLabel) { alignment = TextAnchor.MiddleRight, clipping = TextClipping.Clip });

                switch (evt.type)
                {
                    case EventType.DragUpdated when dragged:
                        DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
                        evt.Use();
                        break;
                    case EventType.DragPerform when dragged:
                        DragAndDrop.AcceptDrag();
                        ApplyToPart(generators, part.index, (Material)DragAndDrop.objectReferences[0]);
                        MarkDirty();
                        evt.Use();
                        break;
                    case EventType.MouseDown when evt.button == 0 && hovering:
                        // selected (again: no longer), with its material in the grid
                        selectedPart = selectedPart == part.index ? -1 : part.index;
                        if (selectedPart >= 0 && part.material != null)
                            Select(ChiselSurfaceBrowserPrefs.ItemOf(part.material));
                        evt.Use();
                        break;
                }
            }
        }

        // the material's menu: put it on a part of the selected generators
        void AddPartsMenu(GenericMenu menu, Material material)
        {
            var generators = SelectedGenerators();
            var parts = PartsOf(generators);
            if (parts.Count == 0 || ChiselSurfaceBrowserDecals.IsDecal(material))
                return;
            foreach (var part in parts)
            {
                var index = part.index;
                menu.AddItem(new GUIContent("Apply to Part/" + part.name), !part.mixed && part.material == material,
                             () => { ApplyToPart(generators, index, material); MarkDirty(); });
            }
        }
    }
}
