using System.Collections.Generic;
using Chisel.Core;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;

namespace Chisel.Components.Tests
{
    // A material keeps what its new decals start from; each placed decal can still be changed on its own
    [TestFixture]
    public class ChiselDecalDefaultsTests
    {
        readonly List<Object> created = new();

        [TearDown]
        public void TearDown()
        {
            foreach (var item in created)
            {
                if (item)
                    Object.DestroyImmediate(item);
            }
            created.Clear();
            ChiselDecalComponentTests.DeleteSheets();
        }

        ChiselDecalComponent Decal(Material material, int imageTile)
        {
            var gameObject = new GameObject("Decal");
            created.Add(gameObject);
            var decal = gameObject.AddComponent<ChiselDecalComponent>();
            decal.Material  = material;
            decal.ImageTile = imageTile;
            return decal;
        }

        Material Sheet()
        {
            var source = new Material(Shader.Find("Standard"));
            created.Add(source);
            return ChiselDecalComponentTests.SheetMaterial(source);
        }

        // A polygon decal only takes the Polygon projection, and a box never does: changing the shape keeps them so
        [Test]
        public void ChangingTheShape_KeepsTheProjectionOneItTakes()
        {
            var defaults = ChiselDecalDefaults.Default;
            defaults.projection = ChiselDecalProjection.Perspective;

            var polygon = defaults.WithShape(ChiselDecalShape.Polygon);
            Assert.That(polygon.projection, Is.EqualTo(ChiselDecalProjection.Polygon));

            var box = polygon.WithShape(ChiselDecalShape.Box);
            Assert.That(box.projection, Is.EqualTo(ChiselDecalProjection.Orthographic), "back from a polygon");

            var trim = defaults.WithShape(ChiselDecalShape.Trim);
            Assert.That(trim.projection, Is.EqualTo(ChiselDecalProjection.Perspective), "a trim doesn't use it, so it's kept");
            Assert.That(trim.WithShape(ChiselDecalShape.Box).projection, Is.EqualTo(ChiselDecalProjection.Perspective));

            Assert.That(ChiselDecalDefaults.AllowsProjection(ChiselDecalShape.Box, ChiselDecalProjection.Polygon), Is.False);
            Assert.That(ChiselDecalDefaults.AllowsProjection(ChiselDecalShape.Box, ChiselDecalProjection.Omni), Is.True);
            Assert.That(ChiselDecalDefaults.AllowsProjection(ChiselDecalShape.Polygon, ChiselDecalProjection.Orthographic), Is.False);
        }

        [Test]
        public void AHeightOfNothing_IsMadeTheSmallestThereIs()
        {
            var defaults = ChiselDecalDefaults.Default;
            defaults.height = 0;
            Assert.That(defaults.Valid().height, Is.EqualTo(ChiselDecalDefaults.kDefaultHeight));
            defaults.height = 0.0001f;
            Assert.That(defaults.Valid().height, Is.EqualTo(ChiselDecalDefaults.kMinHeight));
        }

        // A new decal takes its material's defaults: a trim sheet's image becomes a trim that repeats at the image's
        // aspect; the projection is left alone, since a trim doesn't use it
        [Test]
        public void ANewDecal_StartsFromItsMaterialsDefaults()
        {
            var material = Sheet();
            var metadata = material.GetMetadataOfType<ChiselSurfaceMetadata>();
            var defaults = ChiselDecalDefaults.Default;
            defaults.isDecal     = true;
            defaults.shape       = ChiselDecalShape.Trim;
            defaults.trimFill    = ChiselTrimFill.Fit;
            defaults.transparent = false;
            defaults.height      = 0.25f;
            metadata.decalDefaults = defaults;

            // tile 1 is 2 by 1 pixels
            var decal = Decal(material, 1);
            var depth = decal.Size.z;
            decal.ApplyMaterialDefaults();

            Assert.That(decal.Shape, Is.EqualTo(ChiselDecalShape.Trim));
            Assert.That(decal.TrimFill, Is.EqualTo(ChiselTrimFill.Fit));
            Assert.That(decal.Settings.transparent, Is.False);
            Assert.That(decal.Settings.projection, Is.EqualTo(ChiselDecalSettings.Default.projection));
            Assert.That((Vector3)decal.Size, Is.EqualTo(new Vector3(0.5f, 0.25f, depth)), "its repeat length from the image's aspect");
        }

        [Test]
        public void ANewBoxDecal_TakesItsProjectionAndReach()
        {
            var material = Sheet();
            var defaults = ChiselDecalDefaults.Default;
            defaults.isDecal    = true;
            defaults.projection = ChiselDecalProjection.Omni;
            defaults.reach      = ChiselDecalReach.Cube;
            defaults.height     = 2;
            material.GetMetadataOfType<ChiselSurfaceMetadata>().decalDefaults = defaults;

            var decal = Decal(material, 1);
            decal.ApplyMaterialDefaults();

            Assert.That(decal.Shape, Is.EqualTo(ChiselDecalShape.Box));
            Assert.That(decal.Settings.projection, Is.EqualTo(ChiselDecalProjection.Omni));
            Assert.That(decal.Settings.reach, Is.EqualTo(ChiselDecalReach.Cube));
            Assert.That(decal.Size.x, Is.EqualTo(4).Within(1e-5f), "as wide as the image is, at its height");
            Assert.That(decal.Size.y, Is.EqualTo(2).Within(1e-5f));
        }

        // A material without Chisel metadata gives the plain defaults: a transparent box at its texture's aspect
        [Test]
        public void AMaterialWithoutMetadata_GivesThePlainDefaults()
        {
            var material = new Material(Shader.Find("Standard"));
            created.Add(material);
            var texture = new Texture2D(8, 4);
            created.Add(texture);
            material.mainTexture = texture;

            var decal = Decal(material, 0);
            decal.ApplyMaterialDefaults();

            Assert.That(decal.Shape, Is.EqualTo(ChiselDecalShape.Box));
            Assert.That(decal.Settings.transparent, Is.True);
            Assert.That(decal.Size.x, Is.EqualTo(2).Within(1e-5f));
            Assert.That(decal.Size.y, Is.EqualTo(1).Within(1e-5f));
        }
    }
}
