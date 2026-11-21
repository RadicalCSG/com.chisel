using System.Collections.Generic;
using Chisel.Components;
using Chisel.Core;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Chisel.Editors.Tests
{
    // A selected generator built from several brushes is outlined as its shape, not as every brush in it, unless its
    // pieces are asked for; one of a single brush is that brush's outline
    [TestFixture]
    public sealed class ChiselShapeOutlinesTests
    {
        readonly List<Object> created = new();
        int undoGroup;
        bool showedPieces, showedSubtractive;
        Object[] selection;
        ChiselModelComponent model;

        // the test runner reverts a run's undo steps when the run ends: a test's own are reverted before what it made goes
        [SetUp]
        public void SetUp()
        {
            Undo.IncrementCurrentGroup();
            undoGroup         = Undo.GetCurrentGroup();
            showedPieces      = ChiselCarvingOutlines.BrushPiecesShown;
            showedSubtractive = ChiselCarvingOutlines.Shows(CSGOperationType.Subtractive);
            selection         = Selection.objects;
            Selection.objects = new Object[0];
            ChiselCarvingOutlines.ShowBrushPieces(false);
            ChiselCarvingOutlines.Show(CSGOperationType.Subtractive, false);

            model = new GameObject("Model").AddComponent<ChiselModelComponent>();
            created.Add(model.gameObject);
        }

        [TearDown]
        public void TearDown()
        {
            Selection.objects = selection;
            Undo.RevertAllDownToGroup(undoGroup);
            for (int i = created.Count - 1; i >= 0; i--)
            {
                if (created[i]) Object.DestroyImmediate(created[i]);
            }
            created.Clear();
            Update();
            ChiselCarvingOutlines.ShowBrushPieces(showedPieces);
            ChiselCarvingOutlines.Show(CSGOperationType.Subtractive, showedSubtractive);
            ChiselOutlineRenderer.Instance.UpdateBrushOutlinesNow();
        }

        static void Update()
        {
            for (int i = 0; i < 3; i++)
            {
                ChiselNodeHierarchyManager.Update();
                ChiselModelManager.Instance.UpdateModels();
            }
        }

        T Add<T>(string name) where T : ChiselGeneratorComponent
        {
            var generator = new GameObject(name).AddComponent<T>();
            generator.transform.SetParent(model.transform, false);
            Update();
            return generator;
        }

        static List<CSGTreeBrush> BrushesOf(ChiselGeneratorComponent generator)
        {
            var brushes = new HashSet<CSGTreeBrush>();
            ChiselModelManager.Instance.GetAllTreeBrushes(generator, brushes);
            return new List<CSGTreeBrush>(brushes);
        }

        static void Select(Object target)
        {
            Selection.objects = new[] { target };
            ChiselOutlineRenderer.Instance.UpdateBrushOutlinesNow();
        }

        [Test]
        public void ASelectedGeneratorOfSeveralBrushes_IsOutlinedAsItsShape()
        {
            var stairs  = Add<ChiselLinearStairsComponent>("Stairs");
            var brushes = BrushesOf(stairs);
            Assert.That(brushes.Count, Is.GreaterThan(1), "brushes");

            Select(stairs.gameObject);

            var renderer = ChiselOutlineRenderer.Instance;
            var shape    = renderer.ShapeOutlineOf(stairs.TopTreeNode);
            Assert.That(shape, Is.Not.Null, "its shape");
            Assert.That(shape.VisibleOuterLines, Is.Not.Empty, "its shape's lines");
            foreach (var brush in brushes)
                Assert.That(renderer.HasOutline(brush), Is.False, "a brush in it, outlined on its own");

            // less of it than all of its brushes' edges, and none of it drawn twice
            double piecesLength = 0;
            foreach (var brush in brushes)
                piecesLength += Length(ChiselWireframe.CreateWireframe(brush));
            Assert.That(Length(shape), Is.LessThan(piecesLength), "its length");
            AssertNoLineDrawnTwice(shape);
        }

        static double Length(ChiselWireframe wireframe)
        {
            double length = 0;
            var lines = wireframe.VisibleOuterLines;
            for (int i = 0; i < lines.Length; i += 2)
                length += Unity.Mathematics.math.distance(wireframe.Vertices[lines[i]], wireframe.Vertices[lines[i + 1]]);
            return length;
        }

        static void AssertNoLineDrawnTwice(ChiselWireframe wireframe)
        {
            var vertices = wireframe.Vertices;
            var lines    = wireframe.VisibleOuterLines;
            for (int i = 0; i < lines.Length; i += 2)
            {
                Vector3 a = vertices[lines[i]], b = vertices[lines[i + 1]];
                for (int j = i + 2; j < lines.Length; j += 2)
                {
                    Vector3 c = vertices[lines[j]], d = vertices[lines[j + 1]];
                    var direction = (b - a).normalized;
                    // on one line: both ends of the other on it
                    if (Vector3.Cross(c - a, direction).magnitude > 1e-4f || Vector3.Cross(d - a, direction).magnitude > 1e-4f)
                        continue;
                    var t0 = Mathf.Max(0, Mathf.Min(Vector3.Dot(c - a, direction), Vector3.Dot(d - a, direction)));
                    var t1 = Mathf.Min((b - a).magnitude, Mathf.Max(Vector3.Dot(c - a, direction), Vector3.Dot(d - a, direction)));
                    Assert.That(t1 - t0, Is.LessThan(1e-4f), $"drawn twice: {a} to {b}, and {c} to {d}");
                }
            }
        }

        [Test]
        public void WithItsPiecesShown_EveryBrushInItIsOutlined()
        {
            var stairs = Add<ChiselLinearStairsComponent>("Stairs");
            ChiselCarvingOutlines.ShowBrushPieces(true);
            Select(stairs.gameObject);

            var renderer = ChiselOutlineRenderer.Instance;
            Assert.That(renderer.ShapeOutlineOf(stairs.TopTreeNode), Is.Null, "its shape");
            foreach (var brush in BrushesOf(stairs))
                Assert.That(renderer.HasOutline(brush), Is.True, "a brush in it");
        }

        [Test]
        public void AGeneratorOfOneBrush_IsThatBrushesOutline()
        {
            var box = Add<ChiselBoxComponent>("Box");
            Select(box.gameObject);

            var renderer = ChiselOutlineRenderer.Instance;
            Assert.That(renderer.ShapeOutlineOf(box.TopTreeNode), Is.Null, "a shape");
            foreach (var brush in BrushesOf(box))
                Assert.That(renderer.HasOutline(brush), Is.True, "its brush");
        }

        [Test]
        public void AnUnselectedSubtractiveGeneratorShown_IsOutlinedAsItsShape()
        {
            var stairs = Add<ChiselLinearStairsComponent>("Stairs");
            stairs.Operation = CSGOperationType.Subtractive;
            Update();
            ChiselCarvingOutlines.Show(CSGOperationType.Subtractive, true);
            ChiselOutlineRenderer.Instance.UpdateBrushOutlinesNow();

            var renderer = ChiselOutlineRenderer.Instance;
            Assert.That(renderer.ShapeOutlineOf(stairs.TopTreeNode), Is.Not.Null, "its shape");
            foreach (var brush in BrushesOf(stairs))
            {
                Assert.That(renderer.HasOutline(brush), Is.False, "a brush in it, outlined on its own");
                Assert.That(renderer.TryGetOutlineColor(brush, out var color, out _), Is.True);
                Assert.That((Vector4)color, Is.EqualTo((Vector4)ColorManager.SubtractiveOutlineColor), "its color");
            }
        }

        [Test]
        public void BrushPiecesShown_IsKept()
        {
            ChiselCarvingOutlines.ShowBrushPieces(true);
            ChiselEditorSettings.Load();
            Assert.That(ChiselCarvingOutlines.BrushPiecesShown, Is.True);
        }
    }
}
