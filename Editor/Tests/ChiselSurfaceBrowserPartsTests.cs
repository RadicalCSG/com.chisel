using System.Collections.Generic;
using Chisel.Components;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Chisel.Editors.Tests
{
    [TestFixture]
    public sealed class ChiselSurfaceBrowserPartsTests
    {
        readonly List<Object> created = new();

        [TearDown]
        public void TearDown()
        {
            for (int i = created.Count - 1; i >= 0; i--)
            {
                if (created[i]) Object.DestroyImmediate(created[i]);
            }
            created.Clear();
        }

        ChiselLinearStairsComponent Stairs()
        {
            var stairs = new GameObject("Stairs").AddComponent<ChiselLinearStairsComponent>();
            created.Add(stairs.gameObject);
            return stairs;
        }

        Material NewMaterial()
        {
            var material = new Material(Shader.Find("Standard"));
            created.Add(material);
            return material;
        }

        // A generator's parts are its used surfaces by name; a material dropped on one goes on that surface, undoably
        [Test]
        public void APart_TakesTheMaterial_AndCanBeUndone()
        {
            var stairs = Stairs();
            var parts = ChiselSurfaceBrowserWindow.PartsOf(new[] { stairs });
            Assert.That(parts, Is.Not.Empty);
            var tread = parts.Find(part => part.index == (int)Chisel.Core.StairsSurface.TreadTop);
            Assert.That(tread.name, Is.EqualTo("Tread Top"), "stairs name their parts");

            var before   = stairs.SurfaceDefinition.surfaces[tread.index].RenderMaterial;
            var material = NewMaterial();
            Undo.IncrementCurrentGroup();
            ChiselSurfaceBrowserWindow.ApplyToPart(new[] { stairs }, tread.index, material);
            Assert.That(stairs.SurfaceDefinition.surfaces[tread.index].RenderMaterial, Is.SameAs(material));
            Assert.That(ChiselSurfaceBrowserWindow.PartsOf(new[] { stairs }).Find(part => part.index == tread.index).material, Is.SameAs(material));

            Undo.PerformUndo();
            Assert.That(stairs.SurfaceDefinition.surfaces[tread.index].RenderMaterial, Is.SameAs(before));
        }

        static void UpdateModels()
        {
            for (int i = 0; i < 3; i++)
            {
                ChiselNodeHierarchyManager.Update();
                ChiselModelManager.Instance.UpdateModels();
            }
        }

        static bool Draws(ChiselModelComponent model, Material material)
        {
            foreach (var renderable in model.generated.renderables)
            {
                if (renderable == null || renderable.sharedMesh == null || renderable.sharedMesh.vertexCount == 0)
                    continue;
                if (System.Array.IndexOf(renderable.meshRenderer.sharedMaterials, material) >= 0)
                    return true;
            }
            return false;
        }

        // A material put on a part shows in the scene: the generator is rebuilt with it
        [Test]
        public void APartsMaterial_ShowsInTheScene()
        {
            var model = new GameObject("Model").AddComponent<ChiselModelComponent>();
            created.Add(model.gameObject);
            var stairs = new GameObject("Stairs").AddComponent<ChiselLinearStairsComponent>();
            stairs.transform.SetParent(model.transform, false);
            UpdateModels();

            var material = NewMaterial();
            Assert.That(Draws(model, material), Is.False);
            ChiselSurfaceBrowserWindow.ApplyToPart(new[] { stairs }, (int)Chisel.Core.StairsSurface.TreadTop, material);
            UpdateModels();
            Assert.That(Draws(model, material), Is.True);
        }

        // Selecting a part selects all its surfaces: every brush's surface made from it, and nothing else
        [Test]
        public void APartsSurfaces_AreAllOfItsBrushes()
        {
            var model = new GameObject("Model").AddComponent<ChiselModelComponent>();
            created.Add(model.gameObject);
            var stairs = new GameObject("Stairs").AddComponent<ChiselLinearStairsComponent>();
            stairs.transform.SetParent(model.transform, false);
            UpdateModels();

            var part = (int)Chisel.Core.StairsSurface.TreadTop;
            var surfaces = ChiselSurfaceBrowserWindow.SurfacesOfPart(new[] { stairs }, part);
            Assert.That(surfaces.Count, Is.GreaterThan(1), "a tread on every step");
            foreach (var surface in surfaces)
                Assert.That(surface.descriptionIndex, Is.EqualTo(part));
            var brushes = new HashSet<Chisel.Core.CSGTreeBrush>();
            foreach (var surface in surfaces)
                brushes.Add(surface.brush);
            Assert.That(brushes.Count, Is.GreaterThan(1), "of more than one brush");
        }

        // The parts the selected surfaces are of, for the generators shown; surfaces of other generators don't count
        [Test]
        public void SelectedSurfaces_MarkTheirParts()
        {
            var stairs = Stairs();
            var other  = Stairs();
            var surfaces = new[]
            {
                // a part is the generator's surface (the description), whichever polygon of whichever brush
                new SurfaceReference(stairs, (int)Chisel.Core.StairsSurface.RiserFront, default, 7),
                new SurfaceReference(other,  (int)Chisel.Core.StairsSurface.TreadTop,   default, 0),
            };
            var parts = ChiselSurfaceBrowserWindow.PartsOfSurfaces(new[] { stairs }, surfaces);
            Assert.That(parts, Is.EquivalentTo(new[] { (int)Chisel.Core.StairsSurface.RiserFront }));
        }

        // With several generators, a part whose materials differ is mixed; applying sets it on all of them
        [Test]
        public void SeveralGenerators_ShowMixed_AndAllTakeTheMaterial()
        {
            var a = Stairs();
            var b = Stairs();
            var tread = ChiselSurfaceBrowserWindow.PartsOf(new[] { a }).Find(part => part.index == (int)Chisel.Core.StairsSurface.TreadTop);
            b.SurfaceDefinition.surfaces[tread.index].RenderMaterial = NewMaterial();

            var both = new[] { a, b };
            var mixed = ChiselSurfaceBrowserWindow.PartsOf(both).Find(part => part.index == tread.index);
            Assert.That(mixed.mixed, Is.True);
            Assert.That(mixed.material, Is.Null);

            var material = NewMaterial();
            ChiselSurfaceBrowserWindow.ApplyToPart(both, tread.index, material);
            var same = ChiselSurfaceBrowserWindow.PartsOf(both).Find(part => part.index == tread.index);
            Assert.That(same.mixed, Is.False);
            Assert.That(same.material, Is.SameAs(material));
        }
    }
}
