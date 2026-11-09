using System.Collections.Generic;
using Chisel.Components;
using Chisel.Core;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;

namespace Chisel.Editors.Tests
{
    // Chisel's material field and its picker: what is picked reaches the field it was picked for, the eyedropper takes a
    // surface's material, and the picker lists the recently used, the current folder's and the found materials
    [TestFixture]
    public class ChiselMaterialPickerTests
    {
        const string kFolder = "Assets/__ChiselMaterialPickerTests";

        readonly List<GameObject> objects = new();
        readonly Dictionary<string, string> savedPrefs = new();
        Material brick, stone;

        [SetUp]
        public void SetUp()
        {
            foreach (var key in ChiselSurfaceBrowserPrefs.StorageKeys)
                savedPrefs[key] = EditorPrefs.GetString(key, null);
            if (!AssetDatabase.IsValidFolder(kFolder))
                AssetDatabase.CreateFolder("Assets", kFolder.Substring("Assets/".Length));
            brick = new Material(ChiselProjectSettings.DefaultWallMaterial);
            AssetDatabase.CreateAsset(brick, kFolder + "/Brick.mat");
            stone = new Material(ChiselProjectSettings.DefaultWallMaterial);
            AssetDatabase.CreateAsset(stone, kFolder + "/Stone.mat");
            ChiselMaterialField.ClearPicked();
        }

        [TearDown]
        public void TearDown()
        {
            ChiselMaterialField.StopEyedropper();
            ChiselMaterialField.ClearPicked();
            foreach (var go in objects)
                if (go) Object.DestroyImmediate(go);
            objects.Clear();
            AssetDatabase.DeleteAsset(kFolder);
            foreach (var (key, value) in savedPrefs)
            {
                if (value == null) EditorPrefs.DeleteKey(key);
                else EditorPrefs.SetString(key, value);
            }
            savedPrefs.Clear();
        }

        // a renderer's first material slot, as a material property
        SerializedProperty MaterialSlot(out SerializedObject serialized)
        {
            var go = new GameObject("material picker test");
            objects.Add(go);
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterials = new Material[1];
            serialized = new SerializedObject(renderer);
            return serialized.FindProperty("m_Materials.Array.data[0]");
        }

        // What is picked goes to the field it was picked for, once, and is marked recently used
        [Test]
        public void APick_ReachesOnlyItsOwnField()
        {
            var slot = MaterialSlot(out var serialized);
            var other = MaterialSlot(out _);
            ChiselMaterialField.Pick(serialized.targetObject, slot.propertyPath, brick);

            Assert.That(ChiselMaterialField.TakePicked(other), Is.False, "another object's field");
            Assert.That(ChiselMaterialField.TakePicked(slot), Is.True);
            Assert.That(slot.objectReferenceValue, Is.EqualTo(brick));
            Assert.That(ChiselMaterialField.TakePicked(slot), Is.False, "taken once");
            Assert.That(ChiselSurfaceBrowserPrefs.Recent[0], Is.EqualTo(ChiselSurfaceBrowserPrefs.ItemOf(brick)));

            ChiselMaterialField.Pick(serialized.targetObject, slot.propertyPath, null);
            Assert.That(ChiselMaterialField.TakePicked(slot), Is.True, "None");
            Assert.That(slot.objectReferenceValue, Is.Null);
        }

        // The eyedropper takes the material of the surface clicked, for the field it was started from, and stops
        [Test]
        public void TheEyedropper_TakesASurfacesMaterial()
        {
            if (Object.FindObjectsByType<ChiselModelComponent>(FindObjectsInactive.Include).Length > 0)
                Assert.Ignore("A Chisel model is loaded; run these in a scene without one");
            var root = new GameObject("material picker model");
            objects.Add(root);
            var model = root.AddComponent<ChiselModelComponent>();
            var go = new GameObject("box");
            go.transform.SetParent(model.transform, false);
            var box = go.AddComponent<ChiselBoxComponent>();
            box.Max = new float3(1, 1, 1);
            box.Min = new float3(0, 0, 0);
            for (int i = 0; i < 3; i++) { ChiselNodeHierarchyManager.Update(); ChiselModelManager.Instance.UpdateModels(); }
            foreach (var surface in box.SurfaceDefinition.surfaces)
                surface.SetMaterial(stone);
            box.OnValidate();
            for (int i = 0; i < 3; i++) { ChiselNodeHierarchyManager.Update(); ChiselModelManager.Instance.UpdateModels(); }
            var surfaces = new List<SurfaceReference>();
            ChiselSurfaceSelectionManager.GetAllSurfaceReferences(box, surfaces);
            Assume.That(surfaces.Count, Is.GreaterThan(0));

            var slot = MaterialSlot(out var serialized);
            Assert.That(ChiselMaterialField.PickFrom(surfaces[0]), Is.False, "no eyedropper started");
            ChiselMaterialField.StartEyedropper(serialized.targetObject, slot.propertyPath);
            Assert.That(ChiselMaterialField.EyedropperActive, Is.True);
            Assert.That(ChiselMaterialField.PickFrom(surfaces[0]), Is.True);
            Assert.That(ChiselMaterialField.EyedropperActive, Is.False, "stopped after a pick");
            Assert.That(ChiselMaterialField.TakePicked(slot), Is.True);
            Assert.That(slot.objectReferenceValue, Is.EqualTo(stone));
        }

        // The eyedropper button is Unity's eyedropper icon
        [Test]
        public void TheEyedropperButton_IsAnIcon()
        {
            Assert.That(ChiselMaterialPicker.EyedropperContent().image, Is.Not.Null);
        }

        // The recently used are materials, not decal images of a sheet, nor in hidden folders, each once
        [Test]
        public void TheRecentRow_LeavesOutHiddenAndOtherItems()
        {
            var paths = new Dictionary<string, string>
            {
                ["a"] = "Assets/Walls/Brick.mat", ["b"] = "Assets/Hidden/Secret.mat", ["c"] = "Assets/Walls/Brick.png",
            };
            var recent = ChiselMaterialPicker.RecentPaths(new[] { "a", "b", "c", "a:3", "a", "gone" },
                                                          guid => paths.TryGetValue(guid, out var path) ? path : string.Empty,
                                                          path => path.StartsWith("Assets/Hidden"));
            Assert.That(recent, Is.EqualTo(new[] { "Assets/Walls/Brick.mat" }));
        }

        // The list is the current material's folder, or what the search finds anywhere, ignoring case
        [Test]
        public void TheList_IsTheCurrentFolder_OrWhatIsFound()
        {
            var all = new[] { "Assets/Walls/Brick.mat", "Assets/Walls/Stone.mat", "Assets/Floors/Brick Floor.mat", "Assets/Walls/Old/Plaster.mat" };
            Assert.That(ChiselMaterialPicker.FolderOf("Assets/Walls/Brick.mat"), Is.EqualTo("Assets/Walls"));
            Assert.That(ChiselMaterialPicker.InFolder(all, "Assets/Walls"), Is.EqualTo(new[] { "Assets/Walls/Brick.mat", "Assets/Walls/Stone.mat" }), "not its subfolders");
            Assert.That(ChiselMaterialPicker.Found(all, "brick", 10), Is.EqualTo(new[] { "Assets/Walls/Brick.mat", "Assets/Floors/Brick Floor.mat" }));
            Assert.That(ChiselMaterialPicker.Found(all, "brick", 1).Count, Is.EqualTo(1), "at most as many as asked");

            var picker = new ChiselMaterialPicker(brick, _ => { });
            Assert.That(picker.Listed, Is.EqualTo(new[] { kFolder + "/Brick.mat", kFolder + "/Stone.mat" }));
            Assert.That(picker.ListTitle, Is.EqualTo(kFolder.ToUpperInvariant()));
            picker.Search = "stONE";
            Assert.That(picker.Listed, Does.Contain(kFolder + "/Stone.mat"));
            Assert.That(picker.Listed, Does.Not.Contain(kFolder + "/Brick.mat"));
        }

        // Materials in hidden folders aren't listed
        [Test]
        public void HiddenFolders_AreLeftOut()
        {
            var paths = ChiselMaterialPicker.MaterialPaths(path => path.StartsWith(kFolder));
            Assert.That(paths, Does.Not.Contain(kFolder + "/Brick.mat"));
            Assert.That(ChiselMaterialPicker.MaterialPaths(_ => false), Does.Contain(kFolder + "/Brick.mat"));
        }
    }
}
