using System.Collections.Generic;
using Chisel.Components;
using Chisel.Core;
using NUnit.Framework;
using UnityEngine;

namespace Chisel.Components.Tests
{
    [TestFixture]
    public sealed class ChiselGeneratorPartsTests
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

        static void UpdateModels()
        {
            for (int i = 0; i < 3; i++)
            {
                ChiselNodeHierarchyManager.Update();
                ChiselModelManager.Instance.UpdateModels();
            }
        }

        ChiselLinearStairsComponent Stairs()
        {
            var model = new GameObject("Model").AddComponent<ChiselModelComponent>();
            created.Add(model.gameObject);
            var stairs = new GameObject("Stairs").AddComponent<ChiselLinearStairsComponent>();
            stairs.transform.SetParent(model.transform, false);
            UpdateModels();
            return stairs;
        }

        // A brush is counted from the end it is nearer to, so the top step stays the top step when steps are added
        [Test]
        public void APart_IsCountedFromTheNearerEnd()
        {
            Assert.That(ChiselGeneratorParts.PlaceOf(new ChiselPartReference { valid = true, index = 1 }, 10), Is.EqualTo(1));
            Assert.That(ChiselGeneratorParts.PlaceOf(new ChiselPartReference { valid = true, index = 0, fromEnd = true }, 10), Is.EqualTo(9));
            Assert.That(ChiselGeneratorParts.PlaceOf(new ChiselPartReference { valid = true, index = 0, fromEnd = true }, 12), Is.EqualTo(11), "the last, however many");
            Assert.That(ChiselGeneratorParts.PlaceOf(new ChiselPartReference { valid = true, index = 5 }, 3), Is.EqualTo(-1), "gone");
            Assert.That(ChiselGeneratorParts.PlaceOf(default, 3), Is.EqualTo(-1));
        }

        // Each brush of a generator made of several is its own part, and the part leads back to it
        [Test]
        public void EveryBrush_OfStairs_IsItsOwnPart()
        {
            var stairs  = Stairs();
            var brushes = new List<CSGTreeBrush>();
            ChiselGeneratorParts.BrushesOf(stairs, brushes);
            Assert.That(brushes.Count, Is.GreaterThan(2), "steps");

            var last = ChiselGeneratorParts.PartOf(stairs, brushes[^1]);
            Assert.That(last.fromEnd, Is.True);
            Assert.That(last.index, Is.EqualTo(0));
            foreach (var brush in brushes)
            {
                var part = ChiselGeneratorParts.PartOf(stairs, brush);
                Assert.That(ChiselGeneratorParts.TryResolve(stairs, part, out var resolved), Is.True);
                Assert.That(resolved, Is.EqualTo(brush));
            }
        }

        // A trim on the edge of a step that isn't the first: its targets name that step, not the first with the surface
        [Test]
        public void ATrimsTarget_OnAStep_IsThatStep()
        {
            var stairs  = Stairs();
            var brushes = new List<CSGTreeBrush>();
            ChiselGeneratorParts.BrushesOf(stairs, brushes);
            var step = brushes[brushes.Count / 2];
            var target = ChiselGeneratorParts.TargetOf(stairs, step, (int)StairsSurface.TreadTop);
            Assert.That(target.part.valid, Is.True);
            Assert.That(ChiselGeneratorParts.TryResolve(stairs, target.part, out var resolved) && resolved == step, Is.True);
            Assert.That(resolved, Is.Not.EqualTo(brushes[0]), "not the first step");
        }
    }
}
