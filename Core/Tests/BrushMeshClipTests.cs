using System.Collections.Generic;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;

namespace Chisel.Core.Tests
{
    // A brush clipped by a plane keeps what is behind it: its faces keep their surfaces, the face the cut makes takes the
    // surface it is given, and a plane that doesn't pass through the brush leaves it as it was. Split into two, a brush
    // is clipped by the plane and by the plane turned around.
    [TestFixture]
    public class BrushMeshClipTests
    {
        const int kCutSurface = 99;

        static BrushMesh Box()
        {
            BrushMeshFactory.CreateBox(-Vector3.one, Vector3.one, out var box);
            for (int p = 0; p < box.polygons.Length; p++)
                box.polygons[p].descriptionIndex = 10 + p;
            return box;
        }

        static float4 Plane(float3 normal, float3 point)
        {
            normal = math.normalize(normal);
            return new float4(normal, -math.dot(normal, point));
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

        static readonly float3[] kSides = { new(1, 0, 0), new(-1, 0, 0), new(0, 1, 0), new(0, -1, 0), new(0, 0, 1), new(0, 0, -1) };

        [Test]
        public void ClippedAcrossAFace_KeepsWhatIsBehindThePlane()
        {
            var before = Box();
            var after  = new BrushMesh(before);

            Assert.That(after.Clip(Plane(new float3(1, 0, 0), new float3(0.5f, 0, 0)), kCutSurface), Is.True);

            Assert.That(after.ValidateShape(out var error), Is.True, error);
            Assert.That(after.vertices.Length, Is.EqualTo(8));
            Assert.That(after.polygons.Length, Is.EqualTo(6));
            foreach (var vertex in after.vertices)
                Assert.That(vertex.x, Is.EqualTo(-1).Or.EqualTo(0.5f).Within(1e-5f), $"{vertex}");
            // the corners the cut doesn't touch stay exactly where they were
            var corners = new HashSet<float3>(before.vertices);
            foreach (var vertex in after.vertices)
            {
                if (vertex.x < 0)
                    Assert.That(corners.Contains(vertex), Is.True, $"{vertex} moved");
            }
            foreach (var side in kSides)
            {
                if (side.x > 0)
                    continue;
                AssertKept(before, after, side);
            }
            var cut = FaceFacing(after, new float3(1, 0, 0));
            Assert.That(cut, Is.Not.EqualTo(-1), "the cut");
            Assert.That(after.polygons[cut].descriptionIndex, Is.EqualTo(kCutSurface), "the cut's surface");
        }

        [Test]
        public void ClippedAtACorner_GainsAFace()
        {
            var before = Box();
            var after  = new BrushMesh(before);

            Assert.That(after.Clip(Plane(new float3(1, 1, 1), new float3(0.5f, 0.5f, 0.5f) + new float3(0.25f, 0.25f, 0.25f)), kCutSurface), Is.True);

            Assert.That(after.ValidateShape(out var error), Is.True, error);
            Assert.That(after.polygons.Length, Is.EqualTo(7));
            foreach (var side in kSides)
                AssertKept(before, after, side);
            var cut = FaceFacing(after, new float3(1, 1, 1));
            Assert.That(after.polygons[cut].descriptionIndex, Is.EqualTo(kCutSurface));
        }

        [TestCase( 2, TestName = "APlaneInFrontOfTheBrush_LeavesItAsItWas")]
        [TestCase(-2, TestName = "APlaneBehindTheBrush_LeavesItAsItWas")]
        [TestCase( 1, TestName = "APlaneOnAFace_LeavesItAsItWas")]
        public void APlaneThatDoesntCutThroughTheBrush_LeavesItAsItWas(float x)
        {
            var before = Box();
            var after  = new BrushMesh(before);

            Assert.That(after.Clip(Plane(new float3(1, 0, 0), new float3(x, 0, 0)), kCutSurface), Is.False);

            Assert.That(after.vertices, Is.EqualTo(before.vertices));
            Assert.That(after.polygons, Is.EqualTo(before.polygons));
        }

        [TestCase( 2, BrushMesh.PlaneSide.Behind)]
        [TestCase(-2, BrushMesh.PlaneSide.InFront)]
        [TestCase( 1, BrushMesh.PlaneSide.Behind)]
        [TestCase(-1, BrushMesh.PlaneSide.InFront)]
        [TestCase( 0, BrushMesh.PlaneSide.Both)]
        public void TheSideOfAPlaneTheBrushIsOn(float x, BrushMesh.PlaneSide expected)
        {
            Assert.That(Box().SideOf(Plane(new float3(1, 0, 0), new float3(x, 0, 0))), Is.EqualTo(expected));
        }

        // a split is the brush clipped by the plane and by the plane turned around: the two halves meet at the cut, each
        // with the cut's surface there
        [Test]
        public void SplitByAPlane_TheTwoHalvesMeetAtTheCut()
        {
            var plane  = Plane(new float3(1, 0.5f, 0), new float3(0.25f, 0, 0));
            var behind = Box();
            var front  = Box();

            Assert.That(behind.Clip(plane,  kCutSurface), Is.True);
            Assert.That(front.Clip(-plane, kCutSurface), Is.True);

            Assert.That(behind.ValidateShape(out var error), Is.True, error);
            Assert.That(front.ValidateShape(out error), Is.True, error);
            var behindCut = FaceFacing(behind, plane.xyz);
            var frontCut  = FaceFacing(front, -plane.xyz);
            Assert.That(behind.polygons[behindCut].descriptionIndex, Is.EqualTo(kCutSurface));
            Assert.That(front.polygons[frontCut].descriptionIndex, Is.EqualTo(kCutSurface));
            foreach (var vertex in behind.vertices)
                Assert.That(math.dot(plane, new float4(vertex, 1)), Is.LessThan(1e-4f), $"{vertex} in front");
            foreach (var vertex in front.vertices)
                Assert.That(math.dot(plane, new float4(vertex, 1)), Is.GreaterThan(-1e-4f), $"{vertex} behind");
        }
    }
}
