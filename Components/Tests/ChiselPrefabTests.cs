using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;

namespace Chisel.Components.Tests
{
    // A model saved in a prefab keeps the meshes it generated, as sub-assets of the prefab: its instances draw them as
    // they are, an instance whose geometry is overridden builds its own, and the prefab only ever keeps the meshes it uses
    [TestFixture]
    public class ChiselPrefabTests
    {
        const string kFolder = "Assets/ChiselPrefabTestsTemp";

        readonly List<GameObject> roots = new();

        [SetUp]
        public void SetUp()
        {
            if (Object.FindObjectsByType<ChiselModelComponent>(FindObjectsInactive.Include).Length > 0)
                Assert.Ignore("A Chisel model is loaded; run these in a scene without one");
            if (!AssetDatabase.IsValidFolder(kFolder))
                AssetDatabase.CreateFolder("Assets", "ChiselPrefabTestsTemp");
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var root in roots)
            {
                if (root)
                    Object.DestroyImmediate(root);
            }
            roots.Clear();
            Update();
            AssetDatabase.DeleteAsset(kFolder);
        }

        static void Update()
        {
            for (int i = 0; i < 3; i++)
            {
                ChiselNodeHierarchyManager.Update();
                ChiselModelManager.Instance.UpdateModels();
            }
        }

        static string PathOf(string name) => $"{kFolder}/{name}.prefab";

        // A model with a box from min to max, in the scene
        GameObject BoxModel(string name, float3 min, float3 max)
        {
            var root = new GameObject(name);
            roots.Add(root);
            root.AddComponent<ChiselModelComponent>();
            AddBox(root, min, max);
            Update();
            return root;
        }

        static ChiselBoxComponent AddBox(GameObject model, float3 min, float3 max)
        {
            var boxObject = new GameObject("box");
            boxObject.transform.SetParent(model.transform, false);
            var box = boxObject.AddComponent<ChiselBoxComponent>();
            box.Min = min;
            box.Max = max;
            return box;
        }

        // The model saved as a prefab; the scene object is gone again
        GameObject Save(GameObject root, string name)
        {
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, PathOf(name));
            roots.Remove(root);
            Object.DestroyImmediate(root);
            Update();
            return AssetDatabase.LoadAssetAtPath<GameObject>(PathOf(name));
        }

        GameObject BoxPrefab(string name, float3 min, float3 max) => Save(BoxModel(name, min, max), name);

        ChiselModelComponent Instantiate(GameObject prefab)
        {
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            roots.Add(instance);
            Update();
            return instance.GetComponent<ChiselModelComponent>();
        }

        // Edits the prefab's contents and saves them, as prefab mode does
        static void EditPrefab(GameObject prefab, System.Action<GameObject> edit)
        {
            var path     = AssetDatabase.GetAssetPath(prefab);
            var contents = PrefabUtility.LoadPrefabContents(path);
            try
            {
                edit(contents);
                PrefabUtility.SaveAsPrefabAsset(contents, path);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }
            Update();
        }

        static Mesh[] MeshSubAssets(GameObject prefab)
        {
            return AssetDatabase.LoadAllAssetRepresentationsAtPath(AssetDatabase.GetAssetPath(prefab)).OfType<Mesh>().ToArray();
        }

        // The meshes the model's generated renderers and colliders use
        static Mesh[] GeneratedMeshes(ChiselModelComponent model)
        {
            var meshes = new List<Mesh>();
            foreach (var renderable in model.generated.renderables)
            {
                if (renderable != null && !renderable.invalid)
                    meshes.Add(renderable.meshFilter.sharedMesh);
            }
            foreach (var collider in model.generated.colliders)
            {
                if (collider != null)
                    meshes.Add(collider.meshCollider.sharedMesh);
            }
            return meshes.ToArray();
        }

        // The bounds of what the model draws, in the world
        static Bounds DrawnBounds(ChiselModelComponent model)
        {
            var bounds = new Bounds();
            var first = true;
            foreach (var renderable in model.generated.renderables)
            {
                if (renderable == null || renderable.invalid || renderable.sharedMesh == null || renderable.sharedMesh.vertexCount == 0)
                    continue;
                var meshBounds = renderable.sharedMesh.bounds;
                var world = new Bounds(renderable.meshRenderer.transform.TransformPoint(meshBounds.center), meshBounds.size);
                if (first) bounds = world;
                else       bounds.Encapsulate(world);
                first = false;
            }
            Assert.That(first, Is.False, "the model draws nothing");
            return bounds;
        }

        static void AreEqual(Vector3 expected, Vector3 actual, string message)
        {
            Assert.That(Vector3.Distance(expected, actual), Is.LessThan(1e-4f), $"{message}: expected {expected}, got {actual}");
        }

        // What the model's update left as overrides on the instance
        static List<string> Overrides(ChiselModelComponent model)
        {
            var root = model.gameObject;
            var found = new List<string>();
            foreach (var added in PrefabUtility.GetAddedGameObjects(root))
                found.Add($"added {added.instanceGameObject.name}");
            foreach (var added in PrefabUtility.GetAddedComponents(root))
                found.Add($"added {added.instanceComponent.GetType().Name} on {added.instanceComponent.name}");
            foreach (var change in PrefabUtility.GetPropertyModifications(root) ?? new PropertyModification[0])
            {
                if (change.target != null && PrefabUtility.IsDefaultOverride(change))
                    continue;
                found.Add($"{change.target?.name}.{change.propertyPath}");
            }
            return found;
        }

        [Test]
        public void ASavedPrefab_KeepsTheMeshesItsModelGenerated()
        {
            var prefab = BoxPrefab("kept", new float3(-1, 0, -1), new float3(1, 2, 1));
            var model  = prefab.GetComponent<ChiselModelComponent>();
            var meshes = GeneratedMeshes(model);
            Assert.That(meshes, Is.Not.Empty, "the prefab's model has generated objects");
            var path = AssetDatabase.GetAssetPath(prefab);
            foreach (var mesh in meshes)
            {
                Assert.That(mesh, Is.Not.Null, "a generated object lost its mesh");
                Assert.That(AssetDatabase.GetAssetPath(mesh), Is.EqualTo(path), $"{mesh.name} is kept in the prefab");
                Assert.That(mesh.vertexCount, Is.GreaterThan(0), $"{mesh.name} has its geometry");
            }
            Assert.That(MeshSubAssets(prefab), Is.EquivalentTo(meshes.Distinct()), "the prefab keeps exactly the meshes it uses");
        }

        [Test]
        public void AnInstance_DrawsThePrefabsMeshes_WithoutOverrides()
        {
            var prefab = BoxPrefab("drawn", new float3(-1, 0, -1), new float3(1, 2, 1));
            var model  = Instantiate(prefab);
            AreEqual(new Vector3(2, 2, 2), DrawnBounds(model).size, "the instance draws its box");
            Assert.That(GeneratedMeshes(model), Is.EquivalentTo(GeneratedMeshes(prefab.GetComponent<ChiselModelComponent>())), "the prefab's own meshes");
            Assert.That(Overrides(model), Is.Empty, "overrides on an unchanged instance");
        }

        [Test]
        public void InstancesElsewhere_ShareThePrefabsMeshes()
        {
            var prefab = BoxPrefab("shared", new float3(-1, 0, -1), new float3(1, 2, 1));
            var first  = Instantiate(prefab);
            var second = Instantiate(prefab);
            second.transform.position = new Vector3(10, 0, 0);
            Update();
            AreEqual(new Vector3(10, 1, 0), DrawnBounds(second).center, "the moved instance draws where it is");
            AreEqual(new Vector3(0, 1, 0),  DrawnBounds(first).center,  "the other is where it was");
            Assert.That(GeneratedMeshes(second), Is.EquivalentTo(GeneratedMeshes(first)), "both draw the prefab's meshes");
            Assert.That(Overrides(second).Where(o => !o.StartsWith("shared.m_Local")), Is.Empty, "overrides other than its position");
        }

        [Test]
        public void AnInstanceWithItsGeometryOverridden_BuildsItsOwnMeshes_AndLeavesThePrefabsAlone()
        {
            var prefab       = BoxPrefab("overridden", new float3(-1, 0, -1), new float3(1, 2, 1));
            var prefabMeshes = MeshSubAssets(prefab);
            var vertexCounts = prefabMeshes.Select(m => m.vertexCount).ToArray();
            var model        = Instantiate(prefab);
            var other        = Instantiate(prefab);

            var box = model.GetComponentInChildren<ChiselBoxComponent>();
            Undo.RecordObject(box, "Resize");
            box.Max = new float3(1, 5, 1);
            PrefabUtility.RecordPrefabInstancePropertyModifications(box);
            Update();

            AreEqual(new Vector3(2, 5, 2), DrawnBounds(model).size, "the overridden instance");
            foreach (var mesh in GeneratedMeshes(model))
                Assert.That(EditorUtility.IsPersistent(mesh), Is.False, $"{mesh.name}: the instance draws its own mesh");
            AreEqual(new Vector3(2, 2, 2), DrawnBounds(other).size, "another instance still draws the prefab's");
            Assert.That(GeneratedMeshes(other), Is.EquivalentTo(prefabMeshes), "the other instance's meshes");
            Assert.That(MeshSubAssets(prefab), Is.EquivalentTo(prefabMeshes), "the prefab's meshes are still there");
            Assert.That(prefabMeshes.Select(m => m.vertexCount), Is.EqualTo(vertexCounts), "the prefab's meshes weren't written to");

            PrefabUtility.RevertObjectOverride(box, InteractionMode.AutomatedAction);
            Update();
            AreEqual(new Vector3(2, 2, 2), DrawnBounds(model).size, "reverted");
        }

        [Test]
        public void EditingThePrefab_UpdatesItsMeshes_AndItsInstances()
        {
            var prefab = BoxPrefab("edited", new float3(-1, 0, -1), new float3(1, 2, 1));
            var model  = Instantiate(prefab);
            AreEqual(new Vector3(2, 2, 2), DrawnBounds(model).size, "before");

            EditPrefab(prefab, contents => contents.GetComponentInChildren<ChiselBoxComponent>().Max = new float3(3, 2, 1));

            prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PathOf("edited"));
            AreEqual(new Vector3(4, 2, 2), DrawnBounds(prefab.GetComponent<ChiselModelComponent>()).size, "the prefab's own meshes");
            AreEqual(new Vector3(4, 2, 2), DrawnBounds(model).size, "the instance");
            Assert.That(Overrides(model), Is.Empty, "overrides after the prefab changed");
        }

        [Test]
        public void SavingThePrefabAgainAndAgain_KeepsOnlyTheMeshesItUses()
        {
            var prefab = BoxPrefab("again", new float3(-1, 0, -1), new float3(1, 2, 1));
            var count  = MeshSubAssets(prefab).Length;
            for (int i = 0; i < 5; i++)
            {
                var height = 2 + i;
                EditPrefab(prefab, contents => contents.GetComponentInChildren<ChiselBoxComponent>().Max = new float3(1, height, 1));
                prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PathOf("again"));
                Assert.That(MeshSubAssets(prefab).Length, Is.EqualTo(count), $"after save {i + 1}");
                Assert.That(MeshSubAssets(prefab), Is.EquivalentTo(GeneratedMeshes(prefab.GetComponent<ChiselModelComponent>()).Distinct()),
                            $"after save {i + 1}, the prefab keeps exactly the meshes it uses");
            }
        }

        [Test]
        public void RemovingTheModelFromThePrefab_TakesItsMeshesOutOfIt()
        {
            var prefab = BoxPrefab("removed", new float3(-1, 0, -1), new float3(1, 2, 1));
            Assert.That(MeshSubAssets(prefab), Is.Not.Empty, "before");

            EditPrefab(prefab, contents =>
            {
                var model = contents.GetComponent<ChiselModelComponent>();
                var children = new List<GameObject>();
                foreach (Transform child in contents.transform)
                    children.Add(child.gameObject);
                foreach (var child in children)
                    Object.DestroyImmediate(child);
                Object.DestroyImmediate(model);
            });
            prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PathOf("removed"));
            Assert.That(MeshSubAssets(prefab), Is.Empty, "no model is left to use them");
        }

        [Test]
        public void AMeshOfTheUsersOwnInThePrefab_IsLeftAlone()
        {
            var prefab = BoxPrefab("own", new float3(-1, 0, -1), new float3(1, 2, 1));
            var path   = AssetDatabase.GetAssetPath(prefab);
            var own    = new Mesh { name = "my mesh" };
            AssetDatabase.AddObjectToAsset(own, path);
            AssetDatabase.SaveAssets();

            EditPrefab(prefab, contents => contents.GetComponentInChildren<ChiselBoxComponent>().Max = new float3(1, 3, 1));
            prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PathOf("own"));
            Assert.That(MeshSubAssets(prefab).Select(m => m.name), Does.Contain("my mesh"));
        }

        [Test]
        public void APrefabWithoutAModel_GetsNoMeshes()
        {
            var root = new GameObject("plain");
            roots.Add(root);
            root.AddComponent<BoxCollider>();
            var prefab = Save(root, "plain");
            Assert.That(MeshSubAssets(prefab), Is.Empty);
        }

        [Test]
        public void ANestedPrefab_KeepsItsMeshesInItsOwnPrefab()
        {
            var inner     = BoxPrefab("inner", new float3(-1, 0, -1), new float3(1, 2, 1));
            var innerMeshes = MeshSubAssets(inner);
            var outerRoot = new GameObject("outer");
            roots.Add(outerRoot);
            var nested = (GameObject)PrefabUtility.InstantiatePrefab(inner);
            nested.transform.SetParent(outerRoot.transform, false);
            nested.transform.localPosition = new Vector3(5, 0, 0);
            Update();
            var outer = Save(outerRoot, "outer");

            Assert.That(MeshSubAssets(outer), Is.Empty, "the outer prefab doesn't copy the nested prefab's meshes");
            Assert.That(MeshSubAssets(inner), Is.EquivalentTo(innerMeshes), "the nested prefab's meshes");

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(outer);
            roots.Add(instance);
            Update();
            var model = instance.GetComponentInChildren<ChiselModelComponent>();
            AreEqual(new Vector3(5, 1, 0), DrawnBounds(model).center, "the nested model draws where it was placed");
            Assert.That(GeneratedMeshes(model), Is.EquivalentTo(innerMeshes), "drawn with the nested prefab's meshes");
        }

        [Test]
        public void AnUnpackedInstance_BuildsItsOwnMeshes_AndLeavesThePrefabsAlone()
        {
            var prefab       = BoxPrefab("unpacked", new float3(-1, 0, -1), new float3(1, 2, 1));
            var prefabMeshes = MeshSubAssets(prefab);
            var model        = Instantiate(prefab);
            PrefabUtility.UnpackPrefabInstance(model.gameObject, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            Update();
            AreEqual(new Vector3(2, 2, 2), DrawnBounds(model).size, "unpacked");

            model.GetComponentInChildren<ChiselBoxComponent>().Max = new float3(1, 3, 1);
            Update();
            AreEqual(new Vector3(2, 3, 2), DrawnBounds(model).size, "edited after unpacking");
            Assert.That(MeshSubAssets(prefab), Is.EquivalentTo(prefabMeshes), "the prefab's meshes are still there");
            AreEqual(new Vector3(2, 2, 2), DrawnBounds(prefab.GetComponent<ChiselModelComponent>()).size, "and unchanged");
        }

        [Test]
        public void DestroyingAnInstance_LeavesThePrefabsMeshes()
        {
            var prefab       = BoxPrefab("destroyed", new float3(-1, 0, -1), new float3(1, 2, 1));
            var prefabMeshes = MeshSubAssets(prefab);
            var model        = Instantiate(prefab);
            roots.Remove(model.gameObject);
            Object.DestroyImmediate(model.gameObject);
            Update();
            Assert.That(prefabMeshes.All(m => m != null), Is.True, "a mesh of the prefab was destroyed");
            Assert.That(MeshSubAssets(prefab), Is.EquivalentTo(prefabMeshes));
        }

        [Test]
        public void TheEditorsHelperMeshes_AreNotKeptInThePrefab()
        {
            var prefab = BoxPrefab("helpers", new float3(-1, 0, -1), new float3(1, 2, 1));
            var model  = Instantiate(prefab);
            var hidden = model.GetComponentInChildren<ChiselBoxComponent>();
            SceneVisibilityManager.instance.Hide(hidden.gameObject, false);
            try
            {
                ChiselUnityVisibilityManager.SetDirty();
                Update();
                foreach (var renderable in model.generated.renderables)
                {
                    if (renderable == null || renderable.invalid)
                        continue;
                    Assert.That(EditorUtility.IsPersistent(renderable.partialMesh), Is.False, "the partial mesh is the instance's own");
                }
                Assert.That(Overrides(model), Is.Empty, "overrides from the editor's helper meshes");
                Assert.That(MeshSubAssets(prefab), Is.EquivalentTo(GeneratedMeshes(prefab.GetComponent<ChiselModelComponent>()).Distinct()),
                            "only the generated meshes are kept in the prefab");
            }
            finally
            {
                SceneVisibilityManager.instance.Show(hidden.gameObject, false);
            }
        }
    }
}
