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

        // The edge shown while a trim is dragged is the one it lies on: here the box's top front edge
        [Test]
        public void TheHoveredEdge_IsTheOneTheTrimLiesOn()
        {
            var model = new GameObject("Model").AddComponent<ChiselModelComponent>();
            created.Add(model.gameObject);
            var box = new GameObject("Box").AddComponent<ChiselBoxComponent>();
            box.transform.SetParent(model.transform, false);
            box.Max = new Vector3(1e4f, 1e4f, 1e4f);
            box.Min = new Vector3(-1, -1, -1);
            box.Max = new Vector3(1, 1, 1);
            for (int i = 0; i < 3; i++)
            {
                ChiselNodeHierarchyManager.Update();
                ChiselModelManager.Instance.UpdateModels();
            }
            int Facing(Vector3 normal)
            {
                ref var mesh = ref BrushMeshManager.GetBrushMeshBlob(((CSGTreeBrush)box.TopTreeNode).BrushMesh).Value;
                for (int p = 0; p < mesh.localPlanes.Length; p++)
                    if (Vector3.Dot(((Vector3)mesh.localPlanes[p].xyz).normalized, normal) > 0.999f)
                        return mesh.polygons[p].descriptionIndex;
                return -1;
            }
            var decal = new GameObject("Trim").AddComponent<ChiselDecalComponent>();
            created.Add(decal.gameObject);
            decal.transform.SetParent(model.transform, false);
            decal.transform.position = new Vector3(0, 1, 1);
            decal.Shape = ChiselDecalShape.Trim;
            decal.Size  = new Vector3(1, 0.2f, 1);
            decal.SetTargets(new[]
            {
                new ChiselDecalSurfaceTarget { brush = box, surfaceIndex = Facing(Vector3.up) },
                new ChiselDecalSurfaceTarget { brush = box, surfaceIndex = Facing(Vector3.forward) }
            });

            Assert.That(ChiselSurfaceBrowserDecals.HoveredEdge(decal, out var from, out var to), Is.True);
            Assert.That(from.y, Is.EqualTo(1).Within(1e-4f));
            Assert.That(to.y,   Is.EqualTo(1).Within(1e-4f));
            Assert.That(from.z, Is.EqualTo(1).Within(1e-4f));
            Assert.That(to.z,   Is.EqualTo(1).Within(1e-4f));
            Assert.That(Mathf.Abs(to.x - from.x), Is.EqualTo(2).Within(1e-4f), "the whole edge");
        }

        // A box decal dropped on stairs remembers the step it landed on; with shift, every step
        [Test]
        public void ABoxDropped_OnStairs_RemembersItsPart_OrEveryOne()
        {
            var model = new GameObject("Model").AddComponent<ChiselModelComponent>();
            created.Add(model.gameObject);
            var stairs = new GameObject("Stairs").AddComponent<ChiselLinearStairsComponent>();
            stairs.transform.SetParent(model.transform, false);
            for (int i = 0; i < 3; i++)
            {
                ChiselNodeHierarchyManager.Update();
                ChiselModelManager.Instance.UpdateModels();
            }
            var brushes = new List<CSGTreeBrush>();
            ChiselGeneratorParts.BrushesOf(stairs, brushes);
            var decal = new GameObject("Decal").AddComponent<ChiselDecalComponent>();
            created.Add(decal.gameObject);

            var hit = new ChiselIntersection { treeNode = stairs };
            hit.brushIntersection.brush        = brushes[1];
            hit.brushIntersection.surfaceIndex = 0;
            ChiselSurfaceBrowserDecals.OnPartOf(decal, hit, every: false);
            Assert.That(decal.Targets.Count, Is.EqualTo(1));
            Assert.That(decal.Targets[0].part.valid, Is.True, "the step it landed on");
            Assert.That(ChiselGeneratorParts.TryResolve(stairs, decal.Targets[0].part, out var step) && step == brushes[1], Is.True);
            Assert.That(decal.OnEveryPart, Is.False);

            ChiselSurfaceBrowserDecals.OnPartOf(decal, hit, every: true);
            Assert.That(decal.OnEveryPart, Is.True, "every step");

            var box = new GameObject("Box").AddComponent<ChiselBoxComponent>();
            created.Add(box.gameObject);
            ChiselSurfaceBrowserDecals.OnPartOf(decal, new ChiselIntersection { treeNode = box }, every: true);
            Assert.That(decal.Targets.Count, Is.EqualTo(0), "not a generator of parts: it reaches every surface");
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
            Assert.That(Vector3.Dot(decal.transform.forward, Vector3.back), Is.EqualTo(1).Within(1e-5f), "its blue arrow out of the surface");
            Assert.That(decal.Shape, Is.EqualTo(ChiselDecalShape.Box));
            Assert.That(decal.ImageTile, Is.EqualTo(1));
            Assert.That(decal.Settings.projection, Is.EqualTo(ChiselDecalProjection.Perspective));
            Assert.That(decal.Settings.transparent, Is.False);
            Assert.That(decal.Settings.size.x, Is.EqualTo(1.0f).Within(1e-5f), "twice as wide as high");
            Assert.That(decal.Settings.size.y, Is.EqualTo(0.5f).Within(1e-5f));
        }

        // While dragging, the preview is a decal set up exactly like the one that would be placed; it is hidden, never
        // saved, not undoable, and gone once cleared
        [Test]
        public void ThePreview_IsTheDecalThatWouldBePlaced_AndLeavesNothingBehind()
        {
            var defaults = ChiselDecalDefaults.Default;
            defaults.height = 0.5f;
            var material = DecalMaterial(defaults, Layout((1, "Wide", Vector2Int.zero, new Vector2Int(100, 50))));
            var image = ChiselSurfaceBrowserDecals.ImagesOf(material, "g")[0];
            try
            {
                var undoBefore = UnityEditor.Undo.GetCurrentGroup();
                var preview = ChiselSurfaceBrowserDecals.EnsurePreview(image);
                ChiselSurfaceBrowserDecals.ConfigureBox(preview, material, 1, new Vector3(1, 2, 0), Vector3.back);
                var placed = ChiselSurfaceBrowserDecals.CreateBoxAt(null, material, 1, new Vector3(1, 2, 0), Vector3.back);
                created.Add(placed.gameObject);

                Assert.That(preview.Settings, Is.EqualTo(placed.Settings));
                Assert.That(preview.Shape, Is.EqualTo(placed.Shape));
                Assert.That(preview.transform.position, Is.EqualTo(placed.transform.position));
                Assert.That(preview.transform.rotation, Is.EqualTo(placed.transform.rotation));
                Assert.That(preview.gameObject.hideFlags & HideFlags.DontSave, Is.EqualTo(HideFlags.DontSave), "never saved");
                Assert.That(preview.gameObject.hideFlags & HideFlags.HideInHierarchy, Is.EqualTo(HideFlags.HideInHierarchy));
                Assert.That(ChiselSurfaceBrowserDecals.EnsurePreview(image), Is.SameAs(preview), "one at a time");
                // a hidden object isn't in a scene of its own accord, and a decal draws only on the models of its scene
                ChiselSurfaceBrowserDecals.MoveToScene(preview, placed.gameObject.scene);
                Assert.That(preview.gameObject.scene, Is.EqualTo(placed.gameObject.scene), "in the scene of what it draws on");
                Assert.That(preview.gameObject.hideFlags & HideFlags.DontSave, Is.EqualTo(HideFlags.DontSave), "still never saved");

                var go = preview.gameObject;
                ChiselSurfaceBrowserDecals.ClearPreview();
                Assert.That(go == null, Is.True, "gone");
                Assert.That(ChiselSurfaceBrowserDecals.PreviewDecal == null, Is.True);
                Assert.That(UnityEditor.Undo.GetCurrentGroup(), Is.EqualTo(undoBefore), "nothing to undo");
            }
            finally
            {
                ChiselSurfaceBrowserDecals.ClearPreview();
            }
        }

        // A trim decal made outside any model goes under the brush of its edge, so it draws; one already in a model stays
        [Test]
        public void ATrimOutsideAModel_IsPutUnderItsBrush()
        {
            var model = new GameObject("Model").AddComponent<ChiselModelComponent>();
            created.Add(model.gameObject);
            var brush = new GameObject("Brush").AddComponent<ChiselBrushComponent>();
            brush.transform.SetParent(model.transform, false);
            var decal = new GameObject("Decal").AddComponent<ChiselDecalComponent>();
            created.Add(decal.gameObject);

            ChiselDecalEditor.PutInModelOf(decal, brush, recordUndo: false);
            Assert.That(decal.transform.parent, Is.SameAs(brush.transform));

            var other = new GameObject("Other").AddComponent<ChiselBrushComponent>();
            other.transform.SetParent(model.transform, false);
            ChiselDecalEditor.PutInModelOf(decal, other, recordUndo: false);
            Assert.That(decal.transform.parent, Is.SameAs(brush.transform), "already in a model: left where it is");
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
