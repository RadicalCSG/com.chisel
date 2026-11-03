using System.Collections.Generic;
using Chisel.Core;
using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace Chisel.Components.Tests
{
    // Displacement painted across the seam of two brushes side by side: the two raised surfaces meet without a gap
    [TestFixture]
    public class ChiselDisplacementSeamTests
    {
        readonly List<GameObject> roots = new();

        [SetUp]
        public void SetUp()
        {
            if (Object.FindObjectsByType<ChiselModelComponent>(FindObjectsInactive.Include).Length > 0)
                Assert.Ignore("A Chisel model is loaded; run these in a scene without one");
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var root in roots)
            {
                if (root)
                    Object.DestroyImmediate(root);
            }
            roots.Clear();
            Update();
        }

        static void Update()
        {
            for (int i = 0; i < 3; i++)
            {
                ChiselNodeHierarchyManager.Update();
                ChiselModelManager.Instance.UpdateModels();
            }
        }

        ChiselModelComponent Model()
        {
            var root = new GameObject("displacement seam test");
            roots.Add(root);
            return root.AddComponent<ChiselModelComponent>();
        }

        static ChiselBoxComponent Box(ChiselModelComponent model, float3 min, float3 max)
        {
            var go = new GameObject("box");
            go.transform.SetParent(model.transform, false);
            var box = go.AddComponent<ChiselBoxComponent>();
            // in the order that never puts the minimum past the maximum
            if (math.any(min > box.Max)) { box.Max = max; box.Min = min; }
            else                         { box.Min = min; box.Max = max; }
            Update();
            Assert.That((Vector3)box.Min, Is.EqualTo((Vector3)min), "the box's minimum");
            Assert.That((Vector3)box.Max, Is.EqualTo((Vector3)max), "the box's maximum");
            return box;
        }

        // A dab on a box's top round a point in the model (the boxes have no transform of their own)
        static void Raise(ChiselBoxComponent box, Vector3 at, float radius, float height)
        {
            var heights = ChiselPaintBrush.DisplacementFor(box, 0);
            Assert.That(heights, Is.Not.Null, "the top can't be displaced");
            ChiselPaintBrush.Dab(heights, ChiselPaintBrush.kDisplacementMode, ChiselPaintOperation.Add, Vector4.one * height, at, radius, 1, 0.5f);
            box.DisplacementChanged();
            Update();
        }

        static MeshManifoldReport Classify(ChiselModelComponent model)
        {
            var vertices = new List<float3>();
            var indices  = new List<int>();
            foreach (var renderable in model.generated.renderables)
            {
                if (renderable == null || renderable.sharedMesh == null)
                    continue;
                var mesh = renderable.sharedMesh;
                var offset = vertices.Count;
                foreach (var v in mesh.vertices)
                    vertices.Add(v);
                foreach (var i in mesh.triangles)
                    indices.Add(offset + i);
            }
            using var vertexArray = new NativeArray<float3>(vertices.ToArray(), Allocator.Temp);
            using var indexArray  = new NativeArray<int>(indices.ToArray(), Allocator.Temp);
            return MeshManifoldValidation.Classify(vertexArray, indexArray, 0);
        }

        // the seam is off the paint spacing on purpose: the two grids start at different points
        [TestCase(2.0f, 0f)]
        [TestCase(2.13f, 0f)]
        [TestCase(2.0f, 2.2e-5f)]
        [TestCase(2.0f, -2.2e-5f)]
        public void PaintedAcrossTheSeam_TheTwoTopsMeet(float seam, float apart)
        {
            var model = Model();
            var a = Box(model, new float3(0, 0, 0), new float3(seam, 1, 2));
            var b = Box(model, new float3(seam + apart, 0, 0), new float3(seam + 2.4f, 1, 2));
            var at = new Vector3(seam + 0.1f, 1, 1.05f);
            Raise(a, at, 1, 0.4f);
            Raise(b, at, 1, 0.4f);
            var report = Classify(model);
            Assert.That(report.boundaryEdgeCount, Is.Zero, $"edges drawn by one triangle, {report.boundaryEdgeLength} long in all");
            Assert.That(report.nonManifoldEdgeCount, Is.Zero, "edges drawn by more than two triangles");
        }

        // A dab where the cursor is on one top reaches the other, in the same plane, as the paint tool's dab does
        static void DabAsTheTool(ChiselGeneratorComponent on, Vector3 at, float radius, float height)
        {
            var reached = new List<(ChiselGeneratorComponent generator, int surfaceIndex)>();
            ChiselPaintBrush.DisplacedInReach(on, 0, at, radius, reached);
            foreach (var (generator, surfaceIndex) in reached)
            {
                var heights = ChiselPaintBrush.DisplacementFor(generator, surfaceIndex);
                ChiselPaintBrush.Dab(heights, ChiselPaintBrush.kDisplacementMode, ChiselPaintOperation.Add, Vector4.one * height,
                                     generator.transform.InverseTransformPoint(at), radius, 1, 0.5f);
                generator.DisplacementChanged();
            }
            Update();
        }

        [Test]
        public void ADabOverTheSeam_ReachesTheTopNextToIt_AndNothingElse()
        {
            var model = Model();
            var a = Box(model, new float3(0, 0, 0), new float3(2, 1, 2));
            var b = Box(model, new float3(2, 0, 0), new float3(4.4f, 1, 2));
            Box(model, new float3(2, 0, 3), new float3(4, 1.5f, 5));   // another top, higher up
            var reached = new List<(ChiselGeneratorComponent generator, int surfaceIndex)>();
            ChiselPaintBrush.DisplacedInReach(a, 0, new Vector3(1.8f, 1, 1), 0.5f, reached);
            Assert.That(reached, Is.EquivalentTo(new[] { ((ChiselGeneratorComponent)a, 0), (b, 0) }));
            ChiselPaintBrush.DisplacedInReach(a, 0, new Vector3(1.0f, 1, 1), 0.5f, reached);
            Assert.That(reached, Is.EquivalentTo(new[] { ((ChiselGeneratorComponent)a, 0) }),
                        "out of reach of the other top: " + string.Join(", ", reached.ConvertAll(r => $"{(r.generator == a ? "a" : r.generator == b ? "b" : "higher")} {r.surfaceIndex}")));
        }

        // A subtractive box takes the end of the top away: a dab over its edge raises only what is still drawn, or the
        // raised columns would stand over the hole on nothing
        [Test]
        public void ADabOverARemovedPart_LeavesItFlat()
        {
            var model = Model();
            var top = Box(model, new float3(0, 0, 0), new float3(2, 1, 2));
            var hole = Box(model, new float3(1.3f, 0.5f, -1), new float3(3, 1.5f, 3));
            hole.Operation = CSGOperationType.Subtractive;
            Update();
            var heights = ChiselPaintBrush.DisplacementFor(top, 0);
            var drawn = new List<Vector3>();
            Assert.That(ChiselPaintBrush.DrawnTriangles(top, Vector3.Cross(heights.cellU, heights.cellV), drawn), Is.True, "the model was built");
            ChiselPaintBrush.Dab(heights, ChiselPaintBrush.kDisplacementMode, ChiselPaintOperation.Add, Vector4.one * 0.4f,
                                 new Vector3(1.2f, 1, 1), 0.8f, 1, 0.5f, drawn: drawn);
            int raised = 0, raisedOverTheHole = 0;
            for (int j = 0; j < heights.resolution.y; j++)
            {
                for (int i = 0; i < heights.resolution.x; i++)
                {
                    if (!(heights[i, j].x > 0))
                        continue;
                    var point = heights.origin + i * heights.cellU + j * heights.cellV;
                    if (point.x > 1.3f + 1e-3f) raisedOverTheHole++;
                    else                        raised++;
                }
            }
            Assert.That(raised, Is.GreaterThan(0), "nothing raised where the top is drawn");
            Assert.That(raisedOverTheHole, Is.Zero, "raised over the hole");
        }

        // A needle (three corners on a line) among what is drawn covers nothing
        [Test]
        public void ANeedleAmongWhatIsDrawn_CoversNothing()
        {
            var grid = ChiselSurfacePaint.Create(0, ChiselPaintAttribute.Color, ChiselPaintComponents.X, Vector4.zero,
                                                 new Vector3(0, 1, 0), Vector3.right * 0.25f, Vector3.forward * 0.25f, new Vector2Int(9, 9));
            var drawn = new List<Vector3>
            {
                new Vector3(0, 1, 0), new Vector3(0.5f, 1, 0.5f), new Vector3(2, 1, 2),          // a needle across all of it
                new Vector3(0, 1, 0), new Vector3(0, 1, 0.6f), new Vector3(0.6f, 1, 0),        // what is drawn: a corner
            };
            ChiselPaintBrush.Dab(grid, ChiselPaintBrush.kDisplacementMode, ChiselPaintOperation.Add, Vector4.one * 0.4f,
                                 new Vector3(1, 1, 1), 5, 1, 1, drawn: drawn);
            for (int j = 0; j < 9; j++)
            {
                for (int i = 0; i < 9; i++)
                    Assert.That(grid[i, j].x > 0, Is.EqualTo(i + j <= 2), $"point {i}, {j}");
            }
        }

        // where the cursor was on one top, then the other: the grid points the two share along the seam end up alike
        [Test]
        public void StrokesOnEitherSideOfTheSeam_LeaveTheSeamAlike()
        {
            var model = Model();
            var a = Box(model, new float3(0, 0, 0), new float3(2, 1, 2));
            var b = Box(model, new float3(2, 0, 0), new float3(4.4f, 1, 2));
            DabAsTheTool(a, new Vector3(1.8f, 1, 1.05f), 0.8f, 0.4f);
            DabAsTheTool(b, new Vector3(2.3f, 1, 0.8f), 0.8f, 0.3f);
            var left  = ChiselPaintBrush.DisplacementFor(a, 0);
            var right = ChiselPaintBrush.DisplacementFor(b, 0);
            var compared = 0;
            for (int j = 0; j < left.resolution.y; j++)
            {
                for (int i = 0; i < left.resolution.x; i++)
                {
                    var point = left.origin + i * left.cellU + j * left.cellV;
                    if (Mathf.Abs(point.x - 2) > 1e-4f)
                        continue;
                    for (int jj = 0; jj < right.resolution.y; jj++)
                    {
                        for (int ii = 0; ii < right.resolution.x; ii++)
                        {
                            var other = right.origin + ii * right.cellU + jj * right.cellV;
                            if (Vector3.Distance(point, other) > 1e-4f)
                                continue;
                            Assert.That(right[ii, jj].x, Is.EqualTo(left[i, j].x).Within(1e-5f), $"at {point}");
                            compared++;
                        }
                    }
                }
            }
            Assert.That(compared, Is.GreaterThan(4), "the grids share no points along the seam");
            var report = Classify(model);
            Assert.That(report.boundaryEdgeCount, Is.Zero, "edges drawn by one triangle");
        }
    }
}
