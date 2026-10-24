using System;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;

namespace Chisel.Core.Tests
{
    // End-to-end checks over the managed BrushMesh: building a box through the factory and
    // exercising the validation / topology / containment queries the CSG input relies on.
    [TestFixture]
    public class BrushMeshTests
    {
        static BrushMesh MakeUnitBox()
        {
            BrushMeshFactory.CreateBox(new Vector3(2, 2, 2), 0, out var box); // centered, -1..1
            Assert.That(box, Is.Not.Null, "factory failed to create a box");
            return box;
        }

        [Test]
        public void CreateBox_ProducesValidTopology()
        {
            var box = MakeUnitBox();

            Assert.That(box.vertices.Length,  Is.EqualTo(8));
            Assert.That(box.polygons.Length,  Is.EqualTo(6));
            Assert.That(box.halfEdges.Length, Is.EqualTo(24));
            Assert.That(box.planes.Length,    Is.EqualTo(6));
            Assert.That(box.halfEdgePolygonIndices.Length, Is.EqualTo(24));
        }

        [Test]
        public void CreateBox_PassesValidation()
        {
            var box = MakeUnitBox();
            Assert.That(box.Validate(logErrors: true), Is.True);
            Assert.That(box.ValidateData(out var message), Is.True);
            Assert.That(message, Is.Null);
            Assert.That(box.ValidateShape(out _), Is.True);
        }

        [Test]
        public void CreateBox_ShapeClassification()
        {
            var box = MakeUnitBox();
            Assert.That(box.IsEmpty(),          Is.False);
            Assert.That(box.HasVolume(),        Is.True);
            Assert.That(box.IsConcave(),        Is.False);
            Assert.That(box.IsInsideOut(),      Is.False);
            Assert.That(box.IsSelfIntersecting(), Is.False);
        }

        [Test]
        public void IsInside_DistinguishesInteriorExteriorAndSurface()
        {
            var box = MakeUnitBox();
            Assert.That(box.IsInside(float3.zero),          Is.True);
            Assert.That(box.IsInside(new float3(5, 5, 5)),  Is.False);

            // A point exactly on a face is not strictly inside, but is inside-or-on.
            Assert.That(box.IsInside(new float3(1, 0, 0)),       Is.False);
            Assert.That(box.IsInsideOrOn(new float3(1, 0, 0)),   Is.True);
            Assert.That(box.IsInsideOrOn(new float3(1.1f, 0, 0)), Is.False);
        }

        [Test]
        public void GetPolygonCenter_ReturnsUnitDistanceFaceCenters()
        {
            var box = MakeUnitBox();
            for (int p = 0; p < box.polygons.Length; p++)
            {
                var center = box.GetPolygonCenter(p);
                Assert.That(math.length(center), Is.EqualTo(1f).Within(1e-4f),
                            $"polygon {p} center {center} should be one unit from the box centre");
            }
        }

        [Test]
        public void GetPolygonCenter_OutOfRange_Throws()
        {
            var box = MakeUnitBox();
            Assert.Throws<IndexOutOfRangeException>(() => box.GetPolygonCenter(99));
            Assert.Throws<IndexOutOfRangeException>(() => box.GetPolygonCenter(-1));
        }

        [Test]
        public void FindVertexIndexOfVertex_FindsExistingMissesAbsent()
        {
            var box = MakeUnitBox();
            Assert.That(box.FindVertexIndexOfVertex(box.vertices[3]), Is.EqualTo(3));
            Assert.That(box.FindVertexIndexOfVertex(new float3(100, 100, 100)), Is.EqualTo(-1));
        }

        [Test]
        public void FindEdgeByVertexIndices_FindsConnectedVertices()
        {
            var box = MakeUnitBox();
            var headVertex = box.halfEdges[0].vertexIndex;
            var tailVertex = box.halfEdges[box.halfEdges[0].twinIndex].vertexIndex;

            Assert.That(box.FindEdgeByVertexIndices(tailVertex, headVertex), Is.GreaterThanOrEqualTo(0));
        }

        [Test]
        public void CreateBox_MinMaxOverload_Matches()
        {
            BrushMeshFactory.CreateBox(new Vector3(-1, -1, -1), new Vector3(1, 1, 1), out var box);
            Assert.That(box, Is.Not.Null);
            Assert.That(box.Validate(), Is.True);
            Assert.That(box.IsInside(float3.zero), Is.True);
        }

        [Test]
        public void CreateBox_DegenerateBounds_ProducesNull()
        {
            BrushMeshFactory.CreateBox(Vector3.zero, Vector3.zero, out var box);
            Assert.That(box, Is.Null);
        }

        [Test]
        public void CopyConstructor_ProducesIndependentValidMesh()
        {
            var box  = MakeUnitBox();
            var copy = new BrushMesh(box);

            Assert.That(copy.Validate(), Is.True);
            Assert.That(copy.vertices.Length,  Is.EqualTo(box.vertices.Length));
            Assert.That(copy.halfEdges.Length, Is.EqualTo(box.halfEdges.Length));

            // Mutating the copy must not touch the original.
            copy.vertices[0] = new float3(42, 42, 42);
            Assert.That(box.vertices[0].x, Is.Not.EqualTo(42f));
        }

        [Test]
        public void EmptyBrushMesh_FailsValidation()
        {
            var empty = new BrushMesh();
            Assert.That(empty.IsEmpty(), Is.True);
            Assert.That(empty.Validate(), Is.False);
            Assert.That(empty.ValidateData(out var message), Is.False);
            Assert.That(message, Is.Not.Null);
        }

        [Test]
        public void ValidateData_DetectsBrokenTwinIndex()
        {
            var broken = new BrushMesh(MakeUnitBox());
            broken.halfEdges[0] = new BrushMesh.HalfEdge
            {
                vertexIndex = broken.halfEdges[0].vertexIndex,
                twinIndex   = 999 // out of range
            };
            Assert.That(broken.ValidateData(out _), Is.False);
        }

        [Test]
        public void ValidateData_DetectsOutOfRangeVertexIndex()
        {
            var broken = new BrushMesh(MakeUnitBox());
            broken.halfEdges[0] = new BrushMesh.HalfEdge
            {
                vertexIndex = 999, // out of range
                twinIndex   = broken.halfEdges[0].twinIndex
            };
            Assert.That(broken.ValidateData(out _), Is.False);
        }
    }
}
