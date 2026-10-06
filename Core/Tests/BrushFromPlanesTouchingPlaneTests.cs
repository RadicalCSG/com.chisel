using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;

namespace Chisel.Core.Tests
{
    [TestFixture]
    public class BrushFromPlanesTouchingPlaneTests
    {
        static readonly Bounds kBounds = new Bounds(Vector3.zero, new Vector3(16, 16, 16));
        static readonly float3 kEdgeNormal   = math.normalize(new float3(1, 1, 0));
        static readonly float3 kCornerNormal = math.normalize(new float3(1, 1, 1));

        // A unit box around the origin: -x, +x, -y, +y, -z, +z
        static readonly float4[] kBox =
        {
            new float4(-1,  0,  0, -1),
            new float4( 1,  0,  0, -1),
            new float4( 0, -1,  0, -1),
            new float4( 0,  1,  0, -1),
            new float4( 0,  0, -1, -1),
            new float4( 0,  0,  1, -1)
        };

        // Touches the box along its (+x, +y) edge, with the box behind it
        static float4 EdgePlane   => new float4(kEdgeNormal, -math.dot(kEdgeNormal, new float3(1, 1, 0)));
        // Touches the box at its (+x, +y, +z) corner, with the box behind it
        static float4 CornerPlane => new float4(kCornerNormal, -math.dot(kCornerNormal, new float3(1, 1, 1)));

        static BrushMesh Build(float4[] planes)
        {
            var surfaceArray = new ChiselSurfaceArray();
            surfaceArray.EnsureSize(planes.Length);
            for (int i = 0; i < surfaceArray.surfaces.Length; i++)
                surfaceArray.surfaces[i] = new ChiselSurface();
            BrushMeshFactory.CreateFromPlanes(planes, kBounds, ref surfaceArray, out var brushMesh);
            return brushMesh;
        }

        static float4[] BoxAnd(float4 plane)
        {
            var planes = new float4[kBox.Length + 1];
            kBox.CopyTo(planes, 0);
            planes[kBox.Length] = plane;
            return planes;
        }

        static void AssertIsTheBox(BrushMesh brushMesh)
        {
            Assert.That(brushMesh.Validate(), Is.True, "a valid brush");
            Assert.That(brushMesh.polygons.Length, Is.EqualTo(6), "faces");
            Assert.That(brushMesh.vertices.Length, Is.EqualTo(8), "vertices");
            foreach (var vertex in brushMesh.vertices)
                Assert.That(math.cmax(math.abs(math.abs(vertex) - 1)), Is.LessThan(1e-5f), $"vertex {vertex} is a corner of the box");
        }

        static void AssertIsEmpty(BrushMesh brushMesh)
        {
            Assert.That(brushMesh.polygons == null || brushMesh.polygons.Length == 0, Is.True, "no faces");
        }

        [Test]
        public void PlaneTouchingAnEdge_KeepsTheBox()
        {
            AssertIsTheBox(Build(BoxAnd(EdgePlane)));
        }

        // Cut first, the plane still has the whole bounds to cut, and its face shrinks away later on
        [Test]
        public void PlaneTouchingAnEdge_KeepsTheBox_WhenItComesFirst()
        {
            var planes = new float4[kBox.Length + 1];
            planes[0] = EdgePlane;
            kBox.CopyTo(planes, 1);
            AssertIsTheBox(Build(planes));
        }

        [Test]
        public void PlaneTouchingACorner_KeepsTheBox()
        {
            AssertIsTheBox(Build(BoxAnd(CornerPlane)));
        }

        [Test]
        public void PlaneOnAFace_KeepsTheBox_AndTheFaceTakesIt()
        {
            var brushMesh = Build(BoxAnd(kBox[1]));
            AssertIsTheBox(brushMesh);
            var onPlane = 0;
            foreach (var polygon in brushMesh.polygons)
            {
                if (polygon.descriptionIndex == kBox.Length)
                    onPlane++;
            }
            Assert.That(onPlane, Is.EqualTo(1), "the +x face is described by the extra plane");
        }

        [Test]
        public void PlaneMissingTheBox_KeepsIt([Values(-1, 1)] int side)
        {
            // x >= -3 or x <= 3: the box is well inside either
            AssertIsTheBox(Build(BoxAnd(new float4(side, 0, 0, -3))));
        }

        [Test]
        public void PlaneWithTheBoxOutsideIt_RemovesIt([Values(-1, 1)] int side)
        {
            // x <= -3 or x >= 3
            AssertIsEmpty(Build(BoxAnd(new float4(side, 0, 0, 3))));
        }

        [Test]
        public void PlaneTouchingAnEdge_WithTheBoxOutsideIt_RemovesIt()
        {
            AssertIsEmpty(Build(BoxAnd(-EdgePlane)));
        }

        [Test]
        public void PlaneTouchingACorner_WithTheBoxOutsideIt_RemovesIt()
        {
            AssertIsEmpty(Build(BoxAnd(-CornerPlane)));
        }

        // x <= 1 and x >= 1 leave only a flat slab, which is no brush
        [Test]
        public void PlaneOnAFace_FacingOutwards_RemovesTheBox()
        {
            AssertIsEmpty(Build(BoxAnd(-kBox[1])));
        }
    }
}
