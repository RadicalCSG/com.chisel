using System.Collections.Generic;
using NUnit.Framework;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace Chisel.Core.Tests
{
    // The sides of an extruded or revolved shape set smooth (CurveControlPoint2D.smoothSide) are a smoothing group: the
    // cuts between its convex pieces aren't, and a revolved shape is smooth all round
    [TestFixture]
    public class ChiselShapeSmoothSidesTests
    {
        struct Face
        {
            public int    descriptionIndex;
            public uint   smoothingGroup;
            public float4 plane;
        }

        static BlobAssetReference<InternalChiselSurfaceArray> Surfaces(int count)
        {
            using var builder = new BlobBuilder(Allocator.Temp);
            ref var root = ref builder.ConstructRoot<InternalChiselSurfaceArray>();
            var surfaces = builder.Allocate(ref root.surfaces, count);
            for (int i = 0; i < count; i++)
                surfaces[i] = InternalChiselSurface.Default;
            return builder.CreateBlobAssetReference<InternalChiselSurfaceArray>(Allocator.Temp);
        }

        // each brush's faces, its ends (the first two) left out
        static List<Face[]> Brushes<T>(SerializedBranchGenerator<T> definition) where T : unmanaged, IBranchGenerator
        {
            Assert.That(definition.Validate(), Is.True);
            var settings = definition.GetBranchGenerator();
            var surfaces = Surfaces(definition.RequiredSurfaceCount);
            try
            {
                var count = settings.PrepareAndCountRequiredBrushMeshes();
                Assert.That(count, Is.GreaterThan(0), "brushes");
                var nodes = new NativeList<GeneratedNode>(count, Allocator.Temp);
                try
                {
                    nodes.Resize(count, NativeArrayOptions.ClearMemory);
                    Assert.That(settings.GenerateNodes(surfaces, nodes, Allocator.Temp), Is.True, "generated");
                    var brushes = new List<Face[]>();
                    foreach (var node in nodes)
                    {
                        ref var mesh = ref node.brushMesh.Value;
                        var faces = new Face[mesh.polygons.Length - 2];
                        for (int p = 2; p < mesh.polygons.Length; p++)
                        {
                            faces[p - 2] = new Face
                            {
                                descriptionIndex = mesh.polygons[p].descriptionIndex,
                                smoothingGroup   = mesh.polygons[p].surface.details.smoothingGroup,
                                plane            = mesh.localPlanes[p]
                            };
                        }
                        brushes.Add(faces);
                        node.brushMesh.Dispose();
                    }
                    return brushes;
                }
                finally
                {
                    nodes.Dispose();
                }
            }
            finally
            {
                surfaces.Dispose();
                settings.Dispose();
            }
        }

        // a square with its side from the second point to the third bulging out
        static Curve2D BulgingSquare()
        {
            var points = new[] { new CurveControlPoint2D(-1, -1), new CurveControlPoint2D(1, -1), new CurveControlPoint2D(1, 1), new CurveControlPoint2D(-1, 1) };
            points[1].constraint2 = ControlPointConstraint.Broken;
            points[1].tangent2    = new Vector2(-0.5f, -0.5f);
            points[2].constraint1 = ControlPointConstraint.Broken;
            points[2].tangent1    = new Vector2(-0.5f, 0.5f);
            return new Curve2D(points);
        }

        static ChiselExtrudedShapeDefinition Extruded(Curve2D shape)
        {
            var definition = new ChiselExtrudedShapeDefinition();
            definition.Reset();
            definition.shape = shape;
            return definition;
        }

        static IEnumerable<Face> OfSide(List<Face[]> brushes, int side)
        {
            foreach (var faces in brushes)
                foreach (var face in faces)
                    if (face.descriptionIndex == side + 2)
                        yield return face;
        }

        [Test]
        public void ASmoothSide_IsOneSmoothingGroup_AndTheOtherSidesAreNone()
        {
            var shape = BulgingSquare();
            shape.controlPoints[1].smoothSide = true;
            var brushes = Brushes(Extruded(shape));

            var curved = new List<Face>(OfSide(brushes, 1));
            Assert.That(curved.Count, Is.GreaterThan(1), "the curved side's faces");
            foreach (var face in curved)
                Assert.That(face.smoothingGroup, Is.Not.Zero.And.EqualTo(curved[0].smoothingGroup), "the smooth side");
            foreach (var side in new[] { 0, 2, 3 })
                foreach (var face in OfSide(brushes, side))
                    Assert.That(face.smoothingGroup, Is.Zero, $"side {side}");
        }

        [Test]
        public void SmoothSidesNextToEachOther_AreOneGroup()
        {
            var shape = BulgingSquare();
            shape.controlPoints[1].smoothSide = true;
            shape.controlPoints[2].smoothSide = true;
            var brushes = Brushes(Extruded(shape));

            var group = new List<Face>(OfSide(brushes, 1))[0].smoothingGroup;
            Assert.That(group, Is.Not.Zero);
            foreach (var face in OfSide(brushes, 2))
                Assert.That(face.smoothingGroup, Is.EqualTo(group), "the side after it");
        }

        // the cut between two pieces has a face in each, against each other
        static bool OnACut(Face face, List<Face[]> brushes, int brush)
        {
            for (int b = 0; b < brushes.Count; b++)
            {
                if (b == brush)
                    continue;
                foreach (var other in brushes[b])
                    if (math.all(math.abs(other.plane + face.plane) < 1e-4f))
                        return true;
            }
            return false;
        }

        [Test]
        public void TheCutsBetweenAShapesPieces_AreNotSmooth()
        {
            // an L, every side of it smooth: two pieces
            var shape = new Curve2D(new[] { new CurveControlPoint2D(0, 0), new CurveControlPoint2D(2, 0), new CurveControlPoint2D(2, 1),
                                            new CurveControlPoint2D(1, 1), new CurveControlPoint2D(1, 2), new CurveControlPoint2D(0, 2) });
            for (int i = 0; i < shape.controlPoints.Length; i++)
                shape.controlPoints[i].smoothSide = true;
            var brushes = Brushes(Extruded(shape));
            Assert.That(brushes.Count, Is.GreaterThan(1), "pieces");

            var cuts = 0;
            for (int b = 0; b < brushes.Count; b++)
            {
                foreach (var face in brushes[b])
                {
                    if (OnACut(face, brushes, b))
                    {
                        cuts++;
                        Assert.That(face.smoothingGroup, Is.Zero, "a cut");
                    } else
                        Assert.That(face.smoothingGroup, Is.Not.Zero, "a side");
                }
            }
            Assert.That(cuts, Is.GreaterThan(0), "cuts");
        }

        [Test]
        public void AnExtrudedShape_IsSmoothAllAlongItsPath()
        {
            var shape = BulgingSquare();
            shape.controlPoints[1].smoothSide = true;
            var definition = Extruded(shape);
            // two steps
            definition.path = new ChiselPath(new[] { new ChiselPathPoint(Vector3.zero), new ChiselPathPoint(Vector3.up), new ChiselPathPoint(2 * Vector3.up) });
            var brushes = Brushes(definition);
            Assert.That(brushes.Count, Is.EqualTo(2), "steps");

            var curved = new List<Face>(OfSide(brushes, 1));
            foreach (var face in curved)
                Assert.That(face.smoothingGroup, Is.Not.Zero.And.EqualTo(curved[0].smoothingGroup));
        }

        [Test]
        public void ARevolvedShape_IsSmoothAllRound()
        {
            var definition = ChiselRevolvedShapeBrushTests.Revolved(ChiselRevolvedShapeBrushTests.Square(1, 2));
            // the outside
            definition.shape.controlPoints[1].smoothSide = true;
            var brushes = Brushes(definition);
            Assert.That(brushes.Count, Is.GreaterThan(1));

            var outside = new List<Face>(OfSide(brushes, 1));
            Assert.That(outside.Count, Is.EqualTo(brushes.Count), "a face of the outside in each brush");
            foreach (var face in outside)
                Assert.That(face.smoothingGroup, Is.Not.Zero.And.EqualTo(outside[0].smoothingGroup));
            foreach (var side in new[] { 0, 2, 3 })
                foreach (var face in OfSide(brushes, side))
                    Assert.That(face.smoothingGroup, Is.Zero, $"side {side}");
        }

        [Test]
        public void ARevolvedShapeAcrossItsAxis_HasEachSideOnItsOwnSurface()
        {
            // its bottom, its side at x 1, its top and its side at x -1; one piece, cut in two along the axis
            var definition = ChiselRevolvedShapeBrushTests.Revolved(ChiselRevolvedShapeBrushTests.Square(-1, 1));
            var brushes = Brushes(definition);
            var revolveSegments = definition.settings.revolveSegments;
            Assert.That(brushes.Count, Is.EqualTo(2 * revolveSegments), "the halves either side of the axis");

            for (int b = 0; b < brushes.Count; b++)
            {
                // the half on the positive side first
                var outside = b < revolveSegments ? 1 : 3;
                foreach (var face in brushes[b])
                {
                    var normal = face.plane.xyz;
                    var expected = normal.y < -0.9f ? 0 : normal.y > 0.9f ? 2 : outside;
                    Assert.That(face.descriptionIndex, Is.EqualTo(expected + 2), $"brush {b}, a face facing {normal}");
                }
            }
        }

        [Test]
        public void APointPutOnASmoothSide_LeavesBothHalvesSmooth([Values(1, 3)] int side)
        {
            var points = new[] { new CurveControlPoint2D(0, 0), new CurveControlPoint2D(1, 0), new CurveControlPoint2D(1, 1), new CurveControlPoint2D(0, 1) };
            points[side].smoothSide = true;
            var position = Vector2.Lerp(points[side].position, points[(side + 1) % points.Length].position, 0.5f);

            var inserted = CurveControlPoint2D.InsertedOnSide(points, side, position);

            Assert.That(inserted.Length, Is.EqualTo(5));
            var at = (side + 1) % points.Length;
            Assert.That(inserted[at].position, Is.EqualTo(position), "the new point, after the side's first");
            for (int i = 0; i < inserted.Length; i++)
            {
                var smooth = inserted[i].position == points[side].position || i == at;
                Assert.That(inserted[i].smoothSide, Is.EqualTo(smooth), $"the side from {inserted[i].position}");
            }
        }
    }
}
