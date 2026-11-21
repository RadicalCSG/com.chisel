using System.Collections.Generic;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;

namespace Chisel.Core.Tests
{
    // A shape built from several brushes is outlined as one: where its surface turns, not along the cuts between its
    // brushes or inside a flat face several share, and with what its subtractive and intersecting brushes take away
    [TestFixture]
    public class ChiselShapeOutlineTests
    {
        static readonly int[][] kBoxPolygons =
        {
            new[] { 0, 1, 3, 2 }, new[] { 4, 6, 7, 5 },    // x min, x max
            new[] { 0, 4, 5, 1 }, new[] { 2, 3, 7, 6 },    // y min, y max
            new[] { 0, 2, 6, 4 }, new[] { 1, 5, 7, 3 }     // z min, z max
        };

        static double3[] BoxCorners(double3 min, double3 max)
        {
            var corners = new double3[8];
            for (int i = 0; i < 8; i++)
                corners[i] = new double3((i & 4) != 0 ? max.x : min.x, (i & 2) != 0 ? max.y : min.y, (i & 1) != 0 ? max.z : min.z);
            return corners;
        }

        // brushes from min to max, in a branch, with their operations
        static ChiselShapeOutline.Shape Shape(params (double3 min, double3 max, CSGOperationType operation)[] boxes)
        {
            var shape = new ChiselShapeOutline.Shape();
            shape.items.Add(new ChiselShapeOutline.Item { brush = -1, firstChild = 1, childCount = boxes.Length });
            foreach (var (min, max, operation) in boxes)
            {
                var polygons = new int[kBoxPolygons.Length][];
                for (int p = 0; p < polygons.Length; p++)
                    polygons[p] = (int[])kBoxPolygons[p].Clone();
                var brush = shape.AddBrush(BoxCorners(min, max), polygons);
                shape.items.Add(new ChiselShapeOutline.Item { brush = brush, operation = operation });
            }
            return shape;
        }

        static List<(float3 a, float3 b)> Lines(ChiselShapeOutline.Shape shape)
        {
            var vertices = new List<float3>();
            var indices  = new List<int>();
            ChiselShapeOutline.Build(shape, vertices, indices);
            var lines = new List<(float3, float3)>();
            for (int i = 0; i < indices.Count; i += 2)
                lines.Add((vertices[indices[i]], vertices[indices[i + 1]]));
            return lines;
        }

        static double Length(List<(float3 a, float3 b)> lines)
        {
            double length = 0;
            foreach (var (a, b) in lines)
                length += math.distance(a, b);
            return length;
        }

        static readonly CSGOperationType Add = CSGOperationType.Additive;

        [Test]
        public void TwoBoxesSideBySide_AreOutlinedAsOneBox()
        {
            var lines = Lines(Shape((new double3(0, 0, 0), new double3(1, 1, 1), Add),
                                    (new double3(1, 0, 0), new double3(2, 1, 1), Add)));

            // its four long edges and its eight short ones
            Assert.That(Length(lines), Is.EqualTo(16).Within(1e-4));
            foreach (var (a, b) in lines)
                Assert.That(math.abs(a.x - 1) < 1e-4f && math.abs(b.x - 1) < 1e-4f, Is.False, $"a line where they touch, {a} to {b}");
        }

        [Test]
        public void AFlatFaceTheBrushesShare_HasNoLineInside()
        {
            // a step: the front and back are each one L shaped face
            var lines = Lines(Shape((new double3(0, 0, 0), new double3(1, 1, 1), Add),
                                    (new double3(1, 0, 0), new double3(2, 2, 1), Add)));

            // the L at the front and at the back, and a line from front to back at each of its six corners
            Assert.That(Length(lines), Is.EqualTo(8 + 8 + 6).Within(1e-4));
            foreach (var (a, b) in lines)
                Assert.That(math.abs(a.x - 1) < 1e-4f && math.abs(b.x - 1) < 1e-4f && math.min(a.y, b.y) < 1 - 1e-4f && math.max(a.y, b.y) < 1 + 1e-4f &&
                            math.abs(a.z - b.z) < 1e-4f, Is.False, $"a line inside the front or back, {a} to {b}");
        }

        [Test]
        public void ACornerASubtractiveBrushTakesAway_IsANotch()
        {
            var lines = Lines(Shape((new double3(0, 0, 0), new double3(2, 2, 2), Add),
                                    (new double3(1, 1, 1), new double3(3, 3, 3), CSGOperationType.Subtractive)));

            // nine edges of 2 not at the corner, three of 1 that reach it, and the notch's nine of 1
            Assert.That(Length(lines), Is.EqualTo(18 + 3 + 9).Within(1e-4));
            foreach (var (a, b) in lines)
                Assert.That(math.cmax(math.max(a, b)), Is.LessThanOrEqualTo(2 + 1e-4f), $"a line of the subtractive brush outside the shape, {a} to {b}");
        }

        [Test]
        public void AnIntersectingBrush_LeavesWhatIsInBoth()
        {
            var lines = Lines(Shape((new double3(0, 0, 0), new double3(2, 2, 2), Add),
                                    (new double3(1, 1, 1), new double3(3, 3, 3), CSGOperationType.Intersecting)));

            Assert.That(Length(lines), Is.EqualTo(12).Within(1e-4));
            foreach (var (a, b) in lines)
            {
                Assert.That(math.cmin(math.min(a, b)), Is.GreaterThanOrEqualTo(1 - 1e-4f), $"{a} to {b}");
                Assert.That(math.cmax(math.max(a, b)), Is.LessThanOrEqualTo(2 + 1e-4f), $"{a} to {b}");
            }
        }

        // Four wedges around the up axis, each a box with its inner side folded to the axis (as the pathed stairs make
        // them where a closed path's sides meet), are one square block
        [Test]
        public void BrushesWithASideFoldedToAnEdge_AreOutlinedAsTheShapeTheyMake()
        {
            var square = new[] { new double3(1, 0, 1), new double3(-1, 0, 1), new double3(-1, 0, -1), new double3(1, 0, -1) };
            var shape  = new ChiselShapeOutline.Shape();
            shape.items.Add(new ChiselShapeOutline.Item { brush = -1, firstChild = 1, childCount = 4 });
            for (int k = 0; k < 4; k++)
            {
                var corners = new double3[8];
                for (int i = 0; i < 8; i++)
                {
                    var y = (i & 2) != 0 ? 1.0 : 0.0;
                    // the box's x min side, both its corners at the axis
                    corners[i] = (i & 4) == 0 ? new double3(0, y, 0) : square[(k + (i & 1)) % 4] + new double3(0, y, 0);
                }
                var polygons = new int[kBoxPolygons.Length][];
                for (int p = 0; p < polygons.Length; p++)
                    polygons[p] = (int[])kBoxPolygons[p].Clone();
                shape.items.Add(new ChiselShapeOutline.Item { brush = shape.AddBrush(corners, polygons), operation = Add });
            }
            var lines = Lines(shape);

            // its four upright edges, and the squares at its top and bottom
            Assert.That(Length(lines), Is.EqualTo(4 + 8 + 8).Within(1e-4));
            foreach (var (a, b) in lines)
                Assert.That(math.length(a.xz) < 1e-4f && math.length(b.xz) < 1e-4f, Is.False, $"a line along the axis, {a} to {b}");
        }

        static ChiselSurfaceArray SurfacesFor(BrushMesh brushMesh)
        {
            var surfaceArray = new ChiselSurfaceArray();
            surfaceArray.EnsureSize(brushMesh.polygons.Length);
            for (int i = 0; i < surfaceArray.surfaces.Length; i++)
                surfaceArray.surfaces[i] = new ChiselSurface();
            return surfaceArray;
        }

        // A branch in a tree, as a generator's, of two boxes side by side
        [Test]
        public void ABranchOfBrushes_IsOutlinedInItsOwnSpace_AndOnlyWorkedOutAgainWhenItsShapeChanges()
        {
            BrushMeshFactory.CreateBox(Vector3.zero, Vector3.one, out var box);
            var instance = BrushMeshInstance.Create(box, SurfacesFor(box));
            var left   = CSGTreeBrush.Create(Matrix4x4.identity, brushMesh: instance);
            var right  = CSGTreeBrush.Create(Matrix4x4.Translate(Vector3.right), brushMesh: instance);
            var branch = CSGTreeBranch.Create(default, CSGOperationType.Additive, left, right);
            var tree   = CSGTree.Create(branch);
            try
            {
                branch.LocalTransformation = Matrix4x4.TRS(new Vector3(5, 6, 7), Quaternion.Euler(10, 20, 30), Vector3.one);
                var vertices = new List<float3>();
                var indices  = new List<int>();
                ChiselShapeOutline.Build(branch, vertices, indices);
                double length = 0;
                for (int i = 0; i < indices.Count; i += 2)
                    length += math.distance(vertices[indices[i]], vertices[indices[i + 1]]);
                Assert.That(length, Is.EqualTo(16).Within(1e-3), "one box of 2 by 1 by 1");
                foreach (var vertex in vertices)
                    Assert.That(math.all(vertex > -1e-3f) && math.all(vertex < new float3(2, 1, 1) + 1e-3f), Is.True, $"{vertex}, in the branch's space");

                var hash = ChiselShapeOutline.Hash(branch);
                branch.LocalTransformation = Matrix4x4.Translate(new Vector3(-3, 0, 0));
                Assert.That(ChiselShapeOutline.Hash(branch), Is.EqualTo(hash), "the branch moved");
                right.LocalTransformation = Matrix4x4.Translate(new Vector3(2, 0, 0));
                Assert.That(ChiselShapeOutline.Hash(branch), Is.Not.EqualTo(hash), "a brush in it moved");
            }
            finally
            {
                if (left.Valid)   left.Destroy();
                if (right.Valid)  right.Destroy();
                if (branch.Valid) branch.Destroy();
                if (tree.Valid)   tree.Destroy();
                instance.Destroy();
            }
        }
    }
}
