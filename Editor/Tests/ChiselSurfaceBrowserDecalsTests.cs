using System.Collections.Generic;
using Chisel.Components;
using Chisel.Core;
using NUnit.Framework;
using UnityEngine;

namespace Chisel.Editors.Tests
{
    [TestFixture]
    public sealed class ChiselSurfaceBrowserDecalsTests
    {
        const string kFolder = "Assets/ChiselBrowserDecalTests";
        readonly List<Object> created = new();

        [TearDown]
        public void TearDown()
        {
            for (int i = created.Count - 1; i >= 0; i--)
            {
                if (created[i]) Object.DestroyImmediate(created[i]);
            }
            created.Clear();
            UnityEditor.AssetDatabase.DeleteAsset(kFolder);
        }

        // metadata goes only on a material asset
        Material DecalMaterial(ChiselDecalDefaults defaults, ChiselHotspotLayout layout = null)
        {
            if (!UnityEditor.AssetDatabase.IsValidFolder(kFolder))
                UnityEditor.AssetDatabase.CreateFolder("Assets", kFolder.Substring("Assets/".Length));
            var material = new Material(Shader.Find("Standard"));
            UnityEditor.AssetDatabase.CreateAsset(material, UnityEditor.AssetDatabase.GenerateUniqueAssetPath(kFolder + "/Decals.mat"));
            var metadata = material.AddMetadataOfType<ChiselSurfaceMetadata>();
            defaults.isDecal = true;
            metadata.decalDefaults = defaults;
            metadata.hotspotLayout = layout;
            return material;
        }

        ChiselHotspotLayout Layout(params (int id, string name, Vector2Int min, Vector2Int max)[] tiles)
        {
            var layout = ScriptableObject.CreateInstance<ChiselHotspotLayout>();
            created.Add(layout);
            layout.size = new Vector2Int(100, 100);
            foreach (var (id, name, min, max) in tiles)
            {
                layout.tiles.Add(new HotspotTile
                {
                    id = id, name = name,
                    points = new[] { min, new Vector2Int(max.x, min.y), max, new Vector2Int(min.x, max.y) }
                });
            }
            return layout;
        }

        // A sheet's tiles are its images; without a layout the whole texture is one; a material not marked has none
        [Test]
        public void Images_AreTheTilesOfTheSheet()
        {
            var sheet = DecalMaterial(ChiselDecalDefaults.Default, Layout((3, "Crack", Vector2Int.zero, new Vector2Int(50, 50)),
                                                                          (7, "",      new Vector2Int(50, 0), new Vector2Int(100, 25))));
            var images = ChiselSurfaceBrowserDecals.ImagesOf(sheet, "g");
            Assert.That(images.ConvertAll(image => image.tile), Is.EqualTo(new[] { 3, 7 }));
            Assert.That(images[0].name, Is.EqualTo("Crack"));
            Assert.That(images[1].name, Is.EqualTo("Decals 2"), "an unnamed tile is numbered");
            Assert.That(images[1].index, Is.EqualTo(2));
            Assert.That(images[1].count, Is.EqualTo(2));
            Assert.That(images[1].Key, Is.EqualTo("g:7"));

            var whole = ChiselSurfaceBrowserDecals.ImagesOf(DecalMaterial(ChiselDecalDefaults.Default), "h");
            Assert.That(whole.ConvertAll(image => image.tile), Is.EqualTo(new[] { 0 }));

            var plain = new Material(Shader.Find("Standard"));
            created.Add(plain);
            Assert.That(ChiselSurfaceBrowserDecals.ImagesOf(plain, "p"), Is.Empty);
        }

        // Placed on a surface: at its middle, facing it, a box with the material's defaults and the image's aspect
        [Test]
        public void ABoxDecal_IsCenteredOnTheSurface_AndFacesIt()
        {
            var defaults = ChiselDecalDefaults.Default;
            defaults.height      = 0.5f;
            defaults.transparent = false;
            defaults.projection  = ChiselDecalProjection.Perspective;
            var material = DecalMaterial(defaults, Layout((1, "Wide", Vector2Int.zero, new Vector2Int(100, 50))));

            var outline = new[] { new Vector3(0, 0, 0), new Vector3(2, 0, 0), new Vector3(2, 2, 0), new Vector3(0, 2, 0) };
            var decal = ChiselSurfaceBrowserDecals.CreateBox(null, material, 1, outline, Vector3.back);
            created.Add(decal.gameObject);

            Assert.That(decal.transform.position, Is.EqualTo(new Vector3(1, 1, 0)));
            Assert.That(Vector3.Dot(decal.transform.forward, Vector3.forward), Is.EqualTo(1).Within(1e-5f), "projects into the surface");
            Assert.That(decal.Shape, Is.EqualTo(ChiselDecalShape.Box));
            Assert.That(decal.ImageTile, Is.EqualTo(1));
            Assert.That(decal.Settings.projection, Is.EqualTo(ChiselDecalProjection.Perspective));
            Assert.That(decal.Settings.transparent, Is.False);
            Assert.That(decal.Settings.size.x, Is.EqualTo(1.0f).Within(1e-5f), "twice as wide as high");
            Assert.That(decal.Settings.size.y, Is.EqualTo(0.5f).Within(1e-5f));
        }

        // A decal without a texture shows its color
        [Test]
        public void ADecalsColor_IsItsBaseColor()
        {
            var material = DecalMaterial(ChiselDecalDefaults.Default);
            material.SetColor("_Color", Color.green);
            Assert.That(ChiselSurfaceBrowserDecals.ColorOf(material), Is.EqualTo(Color.green));
            Assert.That(ChiselSurfaceBrowserDecals.ColorOf(null), Is.EqualTo(Color.white));
        }

        // The material's inspector knows a decal material, to show only what decals use
        [Test]
        public void TheMaterialInspector_KnowsADecalMaterial()
        {
            var decal = DecalMaterial(ChiselDecalDefaults.Default);
            var editor = (ChiselSurfaceMetadataEditor)UnityEditor.Editor.CreateEditor(decal.GetMetadataOfType<ChiselSurfaceMetadata>());
            created.Add(editor);
            Assert.That(editor.IsDecal, Is.True);

            var metadata = decal.GetMetadataOfType<ChiselSurfaceMetadata>();
            var defaults = metadata.decalDefaults;
            defaults.isDecal = false;
            metadata.decalDefaults = defaults;
            editor.serializedObject.Update();
            Assert.That(editor.IsDecal, Is.False);
        }

        // A material whose decals are trims or polygons still places as a box when placed on surfaces
        [Test]
        public void ABoxDecal_FromAPolygonMaterial_TakesABoxProjection()
        {
            var material = DecalMaterial(ChiselDecalDefaults.Default.WithShape(ChiselDecalShape.Polygon));
            var outline = new[] { Vector3.zero, Vector3.right, Vector3.up };
            var decal = ChiselSurfaceBrowserDecals.CreateBox(null, material, 0, outline, Vector3.back);
            created.Add(decal.gameObject);
            Assert.That(decal.Shape, Is.EqualTo(ChiselDecalShape.Box));
            Assert.That(decal.Settings.projection, Is.EqualTo(ChiselDecalProjection.Orthographic));
        }
    }
}
