using System.Collections.Generic;
using Chisel.Components;
using Chisel.Core;
using UnityEditor;
using UnityEngine;

namespace Chisel.Editors
{
    // How many surfaces of the open scenes use each material, and how many placed decals each decal image. Counted
    // again only when something in a scene has changed since, never on every repaint
#if UNITY_6000_7_OR_NEWER
    [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
#endif
    [InitializeOnLoad]
    public static class ChiselSceneMaterialUsage
    {
        static readonly Dictionary<Material, int> s_Surfaces = new();
        static readonly Dictionary<(Material, int), int> s_Decals = new();
        static bool s_Dirty = true;

        /// <summary>How many times the counts were made; for tests</summary>
        internal static int RecountCount { get; private set; }

        static ChiselSceneMaterialUsage()
        {
            ObjectChangeEvents.changesPublished -= OnChangesPublished;
            ObjectChangeEvents.changesPublished += OnChangesPublished;
            ChiselNodeHierarchyManager.NodeHierarchyModified -= SetDirty;
            ChiselNodeHierarchyManager.NodeHierarchyModified += SetDirty;
            Undo.undoRedoPerformed -= SetDirty;
            Undo.undoRedoPerformed += SetDirty;
            EditorApplication.hierarchyChanged -= SetDirty;
            EditorApplication.hierarchyChanged += SetDirty;
            UnityEditor.SceneManagement.EditorSceneManager.sceneOpened -= OnSceneOpened;
            UnityEditor.SceneManagement.EditorSceneManager.sceneOpened += OnSceneOpened;
            // asked for while the scene is still coming back after a reload, nothing is counted; count again once settled
            EditorApplication.delayCall += SetDirty;
        }

        static void OnSceneOpened(UnityEngine.SceneManagement.Scene scene, UnityEditor.SceneManagement.OpenSceneMode mode) { SetDirty(); }

        static void OnChangesPublished(ref ObjectChangeEventStream stream) { SetDirty(); }

        /// <summary>Counts again the next time they're asked for</summary>
        public static void SetDirty() { s_Dirty = true; }

        /// <summary>The surfaces using a material: a surface without one uses the project's default</summary>
        public static int SurfacesUsing(Material material)
        {
            Recount();
            return material != null && s_Surfaces.TryGetValue(material, out var count) ? count : 0;
        }

        /// <summary>The placed decals showing an image: a tile of a material, or 0 for its whole texture</summary>
        public static int DecalsUsing(Material material, int tile)
        {
            Recount();
            return material != null && s_Decals.TryGetValue((material, tile), out var count) ? count : 0;
        }

        /// <summary>The placed decals with a material, whatever their image</summary>
        public static int DecalsUsing(Material material)
        {
            Recount();
            if (material == null)
                return 0;
            var count = 0;
            foreach (var pair in s_Decals)
            {
                if (pair.Key.Item1 == material)
                    count += pair.Value;
            }
            return count;
        }

        static void Recount()
        {
            if (!s_Dirty)
                return;
            s_Dirty = false;
            RecountCount++;
            s_Surfaces.Clear();
            s_Decals.Clear();

            foreach (var generator in Object.FindObjectsByType<ChiselGeneratorComponent>(FindObjectsInactive.Exclude))
            {
                if (EditorUtility.IsPersistent(generator))
                    continue;
                var surfaces = generator.SurfaceDefinition?.surfaces;
                if (surfaces == null)
                    continue;
                foreach (var surface in surfaces)
                {
                    if (surface == null)
                        continue;
                    var material = surface.RenderMaterial;
                    if (material == null)
                        continue;
                    s_Surfaces.TryGetValue(material, out var count);
                    s_Surfaces[material] = count + 1;
                }
            }

            foreach (var decal in Object.FindObjectsByType<ChiselDecalComponent>(FindObjectsInactive.Exclude))
            {
                if (EditorUtility.IsPersistent(decal) || decal.Material == null)
                    continue;
                var key = (decal.Material, decal.ImageTile);
                s_Decals.TryGetValue(key, out var count);
                s_Decals[key] = count + 1;
            }
        }
    }
}
