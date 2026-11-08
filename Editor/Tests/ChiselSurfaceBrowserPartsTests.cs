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
