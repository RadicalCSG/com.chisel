using System.Collections.Generic;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;

namespace Chisel.Core.Tests
{
    // A brush without some of its vertices is the convex hull of the ones left: its faces keep their surfaces, the face
    // closing where the vertices were takes one of the faces they were on, and too few vertices left, or all in one plane,
    // leave the brush as it was
    [TestFixture]
    public class BrushMeshRemoveVerticesTests
    {
        static BrushMesh Box()
        {
            BrushMeshFactory.CreateBox(-Vector3.one, Vector3.one, out var box);
            for (int p = 0; p < box.polygons.Length; p++)
                box.polygons[p].descriptionIndex = 10 + p;
            return box;
        }

        static int VertexAt(BrushMesh mesh, float3 position)
        {
            for (int v = 0; v < mesh.vertices.Length; v++)
                if (math.all(mesh.vertices[v] == position)) return v;
            Assert.Fail($"no vertex at {position}");
            return -1;
        }

        // the face of the mesh facing that way, or -1
        static int FaceFacing(BrushMesh mesh, float3 normal)
        {
            for (int p = 0; p < mesh.polygons.Length; p++)
                if (math.dot(mesh.planes[p].xyz, math.normalize(normal)) > 0.9999f) return p;
            return -1;
        }

        static void AssertKept(BrushMesh before, BrushMesh after, float3 normal)
        {
            var was = FaceFacing(before, normal);
            var now = FaceFacing(after, normal);
            Assert.That(now, Is.Not.EqualTo(-1), $"the face facing {normal}");
            Assert.That(after.polygons[now].descriptionIndex, Is.EqualTo(before.polygons[was].descriptionIndex), $"the surface of the face facing {normal}");
        }

        static void AssertCornersWereTheBrushes(BrushMesh before, BrushMesh after)
        {
            var corners = new HashSet<float3>(before.vertices);
            foreach (var vertex in after.vertices)
                Assert.That(corners.Contains(vertex), Is.True, $"{vertex} wasn't one of the brush's corners");
        }

        [Test]
        public void ACornerRemoved_IsCutOff()
        {
            var before = Box();
            var after  = new BrushMesh(before);

            Assert.That(after.RemoveVertices(new[] { VertexAt(after, new float3(1, 1, 1)) }), Is.True);

            Assert.That(after.ValidateShape(out var error), Is.True, error);
            Assert.That(after.vertices.Length, Is.EqualTo(7));
            Assert.That(after.polygons.Length, Is.EqualTo(7));
            AssertCornersWereTheBrushes(before, after);
            foreach (var normal in new[] { new float3(1, 0, 0), new float3(-1, 0, 0), new float3(0, 1, 0), new float3(0, -1, 0), new float3(0, 0, 1), new float3(0, 0, -1) })
                AssertKept(before, after, normal);

            // the cut, facing the corner, with the surface of one of the faces the corner was on
            var cut = FaceFacing(after, new float3(1, 1, 1));
            Assert.That(cut, Is.Not.EqualTo(-1), "the cut");
            var touched = new[] { FaceFacing(before, new float3(1, 0, 0)), FaceFacing(before, new float3(0, 1, 0)), FaceFacing(before, new float3(0, 0, 1)) };
            var touchedSurfaces = new[] { before.polygons[touched[0]].descriptionIndex, before.polygons[touched[1]].descriptionIndex, before.polygons[touched[2]].descriptionIndex };
            Assert.That(touchedSurfaces, Does.Contain(after.polygons[cut].descriptionIndex), "the cut's surface");
        }

        [Test]
        public void AnEdgesVerticesRemoved_TakeTheEdgeAway()
        {
            var before = Box();
            var after  = new BrushMesh(before);

            Assert.That(after.RemoveVertices(new[] { VertexAt(after, new float3(1, 1, 1)), VertexAt(after, new float3(1, 1, -1)) }), Is.True);

            // a wedge: the faces either side of the edge are gone, a slope in their place
            Assert.That(after.ValidateShape(out var error), Is.True, error);
            Assert.That(after.vertices.Length, Is.EqualTo(6));
            Assert.That(after.polygons.Length, Is.EqualTo(5));
            AssertCornersWereTheBrushes(before, after);
            foreach (var normal in new[] { new float3(-1, 0, 0), new float3(0, -1, 0), new float3(0, 0, 1), new float3(0, 0, -1) })
                AssertKept(before, after, normal);
            Assert.That(FaceFacing(after, new float3(1, 1, 0)), Is.Not.EqualTo(-1), "the slope");
        }

        [TestCase(5, TestName = "TooFewLeft_LeaveTheBrushAsItWas")]
        [TestCase(4, TestName = "AllLeftInOnePlane_LeaveTheBrushAsItWas")]
        public void ARemovalThatLeavesNoBrush_LeavesTheBrushAsItWas(int count)
        {
            var before = Box();
            var after  = new BrushMesh(before);
            // the four on top, one more
            var removed = new List<int>();
            for (int v = 0; v < after.vertices.Length; v++)
                if (after.vertices[v].y > 0) removed.Add(v);
            if (count == 5)
                removed.Add(VertexAt(after, new float3(-1, -1, -1)));

            Assert.That(after.RemoveVertices(removed), Is.False);

            Assert.That(after.vertices, Is.EqualTo(before.vertices));
            Assert.That(after.polygons.Length, Is.EqualTo(before.polygons.Length));
        }
    }
}
