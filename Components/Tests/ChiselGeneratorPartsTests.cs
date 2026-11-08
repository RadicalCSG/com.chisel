using System.Collections.Generic;
using Chisel.Components;
using Chisel.Core;
using NUnit.Framework;
using Unity.Mathematics;
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

            foreach (var brush in brushes)
            {
                var part = ChiselGeneratorParts.PartOf(stairs, brush);
                Assert.That(ChiselGeneratorParts.TryResolve(stairs, part, out var resolved), Is.True);
                Assert.That(resolved, Is.EqualTo(brush));
            }
        }

        // Stairs say what their brushes are: a target on a tread is a tread, counted among the treads
        [Test]
        public void Stairs_NameTheirParts()
        {
            var stairs  = Stairs();
            var brushes = new List<CSGTreeBrush>();
            ChiselGeneratorParts.BrushesOf(stairs, brushes);
            var roles = new HashSet<int>();
            for (int i = 0; i < brushes.Count; i++)
                roles.Add(ChiselGeneratorParts.RoleOf(stairs, i));
            Assert.That(roles, Does.Contain((int)ChiselLinearStairs.Part.Step));
            Assert.That(ChiselGeneratorParts.RoleName(stairs, (int)ChiselLinearStairs.Part.Step), Is.EqualTo("Step"));

            var lastStep = -1;
            for (int i = 0; i < brushes.Count; i++)
            {
                if (ChiselGeneratorParts.RoleOf(stairs, i) == (int)ChiselLinearStairs.Part.Step)
                    lastStep = i;
            }
            var part = ChiselGeneratorParts.PartOf(stairs, brushes[lastStep]);
            Assert.That(part.role, Is.EqualTo((int)ChiselLinearStairs.Part.Step));
            Assert.That(part.fromEnd && part.index == 0, Is.True, "the last step, counted among the steps");
            Assert.That(ChiselGeneratorParts.NameOf(stairs, part), Is.EqualTo("Step 1 from the end"));
        }

        T Generator<T>() where T : ChiselGeneratorComponent
        {
            var model = new GameObject("Model").AddComponent<ChiselModelComponent>();
            created.Add(model.gameObject);
            var generator = new GameObject(typeof(T).Name).AddComponent<T>();
            generator.transform.SetParent(model.transform, false);
            UpdateModels();
            return generator;
        }

        static HashSet<int> RolesOf(ChiselGeneratorComponent generator)
        {
            var brushes = new List<CSGTreeBrush>();
            ChiselGeneratorParts.BrushesOf(generator, brushes);
            var roles = new HashSet<int>();
            for (int i = 0; i < brushes.Count; i++)
                roles.Add(ChiselGeneratorParts.RoleOf(generator, i));
            return roles;
        }

        // Spiral stairs tell their steps apart from their column
        [Test]
        public void SpiralStairs_NameTheirParts()
        {
            var roles = RolesOf(Generator<ChiselSpiralStairsComponent>());
            Assert.That(roles.Contains((int)ChiselSpiralStairs.Part.Step) || roles.Contains((int)ChiselSpiralStairs.Part.Tread), Is.True, "steps");            Assert.That(roles.Count, Is.GreaterThan(1));
        }

        // Pathed stairs name the parts of each stretch as linear stairs do, and their corners
        [Test]
        public void PathedStairs_NameTheirParts()
        {
            var stairs = Generator<ChiselPathedStairsComponent>();
            var roles  = RolesOf(stairs);
            Assert.That(roles, Does.Contain((int)ChiselLinearStairs.Part.Step));
            Assert.That(roles, Has.No.Member(-1), "every brush has a part");
            Assert.That(ChiselGeneratorParts.RoleName(stairs, ChiselPathedStairs.kCornerPart), Is.EqualTo("Corner"));
        }

        // A box decal on every step: a copy on each other step, moved as that step is from its own
        [Test]
        public void ABoxDecal_OnEveryStep_IsCopiedOntoTheOthers()
        {
            var stairs  = Stairs();
            var brushes = new List<CSGTreeBrush>();
            ChiselGeneratorParts.BrushesOf(stairs, brushes);
            var target = ChiselGeneratorParts.TargetOf(stairs, brushes[0], (int)StairsSurface.RiserFront);
            var steps  = ChiselGeneratorParts.CountOf(stairs, target.part.role);
            Assert.That(steps, Is.GreaterThan(1));

            var copies = new List<float4x4>();
            ChiselTrimDecals.CopiesOnEveryPart(target, copies);
            Assert.That(copies.Count, Is.EqualTo(0), "only its own step");

            target.part.all = true;
            ChiselTrimDecals.CopiesOnEveryPart(target, copies);
            Assert.That(copies.Count, Is.EqualTo(steps - 1), "every other step");
            var moves = new HashSet<float3>();
            foreach (var copy in copies)
            {
                Assert.That(math.dot(copy.c0.xyz, new float3(1, 0, 0)), Is.EqualTo(1).Within(1e-3f), "straight stairs don't turn");
                Assert.That(math.length(copy.c3.xyz), Is.GreaterThan(1e-3f), "moved onto another step");
                moves.Add(math.round(copy.c3.xyz * 1000));
            }
            Assert.That(moves.Count, Is.EqualTo(copies.Count), "each onto a step of its own");
        }

        // A trim on every step runs along the same edge of each
        [Test]
        public void ATrim_OnEveryStep_RunsAlongEachStep()
        {
            var stairs  = Stairs();
            var brushes = new List<CSGTreeBrush>();
            ChiselGeneratorParts.BrushesOf(stairs, brushes);
            var a = ChiselGeneratorParts.TargetOf(stairs, brushes[0], (int)StairsSurface.RiserFront);
            var b = ChiselGeneratorParts.TargetOf(stairs, brushes[0], (int)StairsSurface.StepSides);
            var decal = new GameObject("Trim").AddComponent<ChiselDecalComponent>();
            created.Add(decal.gameObject);
            decal.transform.SetParent(stairs.transform.parent, false);
            decal.Shape = ChiselDecalShape.Trim;
            decal.Size  = new Vector3(1, 0.1f, 1);
            decal.SetTargets(new[] { a, b });
            UpdateModels();

            var halves = new List<ChiselTrimDecals.Half>();
            Assume.That(ChiselTrimDecals.TryDescribe(decal, 1, 1, SurfaceDestinationFlags.Default, out _, halves, out _, out _), Is.True, "a step's front meets its side");
            var one = halves.Count;
            decal.OnEveryPart = true;
            Assert.That(decal.OnEveryPart, Is.True);
            Assert.That(ChiselTrimDecals.TryDescribe(decal, 1, 1, SurfaceDestinationFlags.Default, out _, halves, out _, out _), Is.True);
            Assert.That(halves.Count, Is.GreaterThan(one), "the edge of the other steps too");
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
