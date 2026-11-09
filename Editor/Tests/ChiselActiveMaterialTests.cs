using System.Collections.Generic;
using Chisel.Components;
using Chisel.Core;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Chisel.Editors.Tests
{
    // The material picked in the Surface Browser is the active material, which new brushes get on every surface
    [TestFixture]
    public sealed class ChiselActiveMaterialTests
    {
        const string kFolder = "Assets/__ChiselActiveMaterialTests";

        readonly List<GameObject> objects = new();
        readonly Dictionary<string, string> savedPrefs = new();
        ChiselSurfaceBrowserWindow window;
        Material brick, decal;

        [SetUp]
        public void SetUp()
        {
            foreach (var key in ChiselSurfaceBrowserPrefs.StorageKeys)
                savedPrefs[key] = EditorPrefs.GetString(key, null);
            if (!AssetDatabase.IsValidFolder(kFolder))
                AssetDatabase.CreateFolder("Assets", kFolder.Substring("Assets/".Length));
            brick = new Material(ChiselProjectSettings.DefaultWallMaterial);
            AssetDatabase.CreateAsset(brick, kFolder + "/Brick.mat");
            decal = new Material(ChiselProjectSettings.DefaultWallMaterial);
            AssetDatabase.CreateAsset(decal, kFolder + "/Decal.mat");
            var metadata = decal.AddMetadataOfType<ChiselSurfaceMetadata>();
            var defaults = ChiselDecalDefaults.Default;
            defaults.isDecal = true;
            metadata.decalDefaults = defaults;
            ChiselSurfaceBrowserPrefs.ActiveMaterial = null;
        }

        [TearDown]
        public void TearDown()
        {
            if (window) window.Close();
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

        ChiselSurfaceBrowserWindow OpenWindow(bool presets)
        {
            window = EditorWindow.CreateWindow<ChiselSurfaceBrowserWindow>();
            window.SetPresetMode(presets);
            return window;
        }

        T Generator<T>() where T : ChiselGeneratorComponent
        {
            var go = new GameObject(typeof(T).Name);
            objects.Add(go);
            var generator = go.AddComponent<T>();
            generator.OnValidate();
            return generator;
        }

        // Picking a material makes it the active material, a decal doesn't, and deselecting leaves none
        [Test]
        public void APickedMaterial_IsTheActiveMaterial_UntilDeselected()
        {
            if (ChiselSurfaceSelectionManager.Selection.Count > 0)
                Assert.Ignore("Chisel surfaces are selected; picking would change them");
            OpenWindow(presets: false);

            window.ApplyOnSelection(brick);
            Assert.That(ChiselSurfaceBrowserPrefs.ActiveMaterial, Is.EqualTo(brick));

            window.ApplyOnSelection(decal);
            Assert.That(ChiselSurfaceBrowserPrefs.ActiveMaterial, Is.EqualTo(brick), "a decal is placed, not made active");

            window.Deselect();
            Assert.That(ChiselSurfaceBrowserPrefs.ActiveMaterial, Is.Null);
        }

        // Deselecting a preset leaves the active material as it is
        [Test]
        public void DeselectingAPreset_KeepsTheActiveMaterial()
        {
            ChiselSurfaceBrowserPrefs.ActiveMaterial = brick;
            OpenWindow(presets: true);
            window.Deselect();
            Assert.That(ChiselSurfaceBrowserPrefs.ActiveMaterial, Is.EqualTo(brick));
        }

        // A new brush gets the active material on every surface, the parts a generator has its own default for too
        [Test]
        public void ANewBrush_GetsTheActiveMaterial_OnEverySurface()
        {
            ChiselSurfaceBrowserPrefs.ActiveMaterial = brick;
            foreach (var generator in new ChiselGeneratorComponent[] { Generator<ChiselBoxComponent>(), Generator<ChiselLinearStairsComponent>() })
            {
                Assert.That(ChiselActiveMaterial.GiveTo(generator), Is.True);
                var surfaces = generator.SurfaceDefinition.surfaces;
                Assume.That(surfaces.Length, Is.GreaterThan(0));
                foreach (var surface in surfaces)
                    Assert.That(surface.RenderMaterial, Is.EqualTo(brick), generator.name);
            }
        }

        // Without an active material a new brush keeps the project's defaults
        [Test]
        public void WithoutAnActiveMaterial_ANewBrushKeepsItsDefaults()
        {
            var box = Generator<ChiselBoxComponent>();
            Assert.That(ChiselActiveMaterial.GiveTo(box), Is.False);
            foreach (var surface in box.SurfaceDefinition.surfaces)
                Assert.That(surface.RenderMaterial, Is.EqualTo(ChiselProjectSettings.DefaultWallMaterial));
        }
    }
}
