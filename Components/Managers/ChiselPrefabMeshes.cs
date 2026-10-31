#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Chisel.Components
{
    // A prefab keeps the meshes its models generated, as sub-assets of the prefab: a mesh that isn't an asset is lost when
    // a prefab is saved, and its instances would have nothing to draw. Each time a prefab with a model is saved, the meshes
    // its generated objects use are added to it, and the meshes it kept that nothing uses anymore are taken out of it.
    sealed class ChiselPrefabMeshes : AssetPostprocessor
    {
        const string kPrefabExtension = ".prefab";

        // the meshes of generated objects are named after them; only those are taken out of a prefab
        const string kGeneratedPrefix = "\u2039[generated";

        static bool s_Storing;

        static bool IsGenerated(Mesh mesh) => mesh.name.StartsWith(kGeneratedPrefix, System.StringComparison.Ordinal);

        static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets, string[] movedAssets, string[] movedFromAssetPaths)
        {
            if (s_Storing)
                return;
            foreach (var path in importedAssets)
            {
                if (!path.EndsWith(kPrefabExtension, System.StringComparison.OrdinalIgnoreCase))
                    continue;
                var root = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (root && NeedsStoring(root, path))
                    Store(path);
            }
        }

        // The models of the prefab itself, not those of the prefabs nested in it, which keep their own meshes
        static IEnumerable<ChiselModelComponent> OwnModels(GameObject root)
        {
            foreach (var model in root.GetComponentsInChildren<ChiselModelComponent>(true))
            {
                if (model.transform != root.transform && PrefabUtility.IsPartOfPrefabInstance(model))
                    continue;
                yield return model;
            }
        }

        static IEnumerable<Mesh> GeneratedMeshes(ChiselModelComponent model)
        {
            var generated = model.generated;
            if (generated == null)
                yield break;
            if (generated.renderables != null)
            {
                foreach (var renderable in generated.renderables)
                    if (renderable != null && !renderable.invalid) yield return renderable.meshFilter ? renderable.meshFilter.sharedMesh : null;
            }
            if (generated.debugVisualizationRenderables != null)
            {
                foreach (var renderable in generated.debugVisualizationRenderables)
                    if (renderable != null && !renderable.invalid) yield return renderable.meshFilter ? renderable.meshFilter.sharedMesh : null;
            }
            if (generated.colliders != null)
            {
                foreach (var collider in generated.colliders)
                    if (collider != null) yield return collider.meshCollider ? collider.meshCollider.sharedMesh : null;
            }
        }

        // Whether a model of the prefab changed since its meshes were made, a generated object lost its mesh, or the prefab
        // keeps a generated mesh nothing uses (also when its models are gone)
        internal static bool NeedsStoring(GameObject root, string path)
        {
            var used = new HashSet<Mesh>();
            foreach (var model in OwnModels(root))
            {
                // its meshes were made from something other than what it is now
                if (model.generated == null ||
                    model.generated.componentInputHash != ChiselModelManager.GetComponentInputHash(model))
                    return true;
                foreach (var mesh in GeneratedMeshes(model))
                {
                    if (!mesh || !EditorUtility.IsPersistent(mesh))
                        return true;
                    used.Add(mesh);
                }
            }
            foreach (var asset in AssetDatabase.LoadAllAssetRepresentationsAtPath(path))
            {
                if (asset is Mesh mesh && IsGenerated(mesh) && !used.Contains(mesh))
                    return true;
            }
            return false;
        }

        // The prefab's models built, their meshes added to the prefab and the ones nothing uses taken out of it
        internal static void Store(string path)
        {
            s_Storing = true;
            var contents = PrefabUtility.LoadPrefabContents(path);
            try
            {
                for (int i = 0; i < 3; i++)
                {
                    ChiselNodeHierarchyManager.Update();
                    ChiselModelManager.Instance.UpdateModels();
                }

                var used = new HashSet<Mesh>();
                foreach (var model in OwnModels(contents))
                {
                    foreach (var mesh in GeneratedMeshes(model))
                    {
                        if (!mesh)
                            continue;
                        if (!EditorUtility.IsPersistent(mesh))
                            AssetDatabase.AddObjectToAsset(mesh, path);
                        used.Add(mesh);
                    }
                    // as a saved scene does: an instance whose input is the prefab's keeps these meshes
                    model.generated.inputHash          = ChiselModelManager.GetInputHash(model);
                    model.generated.componentInputHash = ChiselModelManager.GetComponentInputHash(model);
                }

                foreach (var asset in AssetDatabase.LoadAllAssetRepresentationsAtPath(path))
                {
                    if (asset is Mesh mesh && IsGenerated(mesh) && !used.Contains(mesh))
                        Object.DestroyImmediate(mesh, true);
                }
                PrefabUtility.SaveAsPrefabAsset(contents, path);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
                s_Storing = false;
            }
        }
    }
}
#endif
