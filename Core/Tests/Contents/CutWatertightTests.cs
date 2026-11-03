using System.Collections.Generic;
using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;

namespace Chisel.Core.Tests
{
    // What the cuts after the CSG (decals, hotspot sections) leave of a closed model is still closed: every edge drawn is
    // drawn by exactly two triangles, with corners at exactly the same positions, and no triangle is without area
    [TestFixture]
    [Category("Contents")]
    public class CutWatertightTests
    {
        const ulong kDecalMaterial = 0xDECA1;

        static readonly quaternion kDown = quaternion.LookRotation(new float3(0, -1, 0), new float3(0, 0, 1));

        // Two boxes side by side, one closed solid
        static ContentsScene TwoBoxes()
        {
            return new ContentsScene()
                .Add(ContentsSceneNode.Brush(ContentsScene.BoxPlanes(new float3(-2, -1, -2), new float3(0, 0, 2)), name: "a"))
                .Add(ContentsSceneNode.Brush(ContentsScene.BoxPlanes(new float3( 0, -1, -2), new float3(2, 0, 2)), name: "b"));
        }

        static ChiselDecalInstance Decal(ulong id, float3 position, quaternion rotation, float3 size,
                                         ChiselDecalProjection projection = ChiselDecalProjection.Orthographic)
        {
            var settings = ChiselDecalSettings.Default;
            settings.size        = size;
            settings.transparent = false;
            settings.projection  = projection;
            return new ChiselDecalInstance
            {
                entityID         = id,
                decalToTree      = float4x4.TRS(position, rotation, new float3(1)),
                settings         = settings,
                renderMaterial   = kDecalMaterial,
                destinationFlags = SurfaceDestinationFlags.Default
            };
        }

        static ContentsTreeHarness Build(ChiselDecalInstance[] decals, float2[] outline = null)
        {
            var harness = ContentsTreeHarness.Build(TwoBoxes());
            using var array    = new NativeArray<ChiselDecalInstance>(decals, Allocator.Temp);
            using var outlines = new NativeArray<float2>(outline ?? new float2[0], Allocator.Temp);
            if (outline != null)
                harness.Tree.SetDecals(array, default, outlines);
            else
                harness.Tree.SetDecals(array);
            Assert.That(harness.Update(), Is.True, "the CSG update did not run");
            Assert.That(harness.Delivered, Is.True, "no meshes were delivered: " + harness.LastUpdateReport);
            return harness;
        }

        static MeshManifoldReport Classify(ContentsTreeHarness harness, int leaveOut = -1)
        {
            var vertices = new List<float3>();
            var indices  = new List<int>();
            for (int t = 0; t < harness.Triangles.Count; t++)
            {
                if (t == leaveOut)
                    continue;
                var triangle = harness.Triangles[t];
                indices.Add(vertices.Count); vertices.Add(triangle.a);
                indices.Add(vertices.Count); vertices.Add(triangle.b);
                indices.Add(vertices.Count); vertices.Add(triangle.c);
            }
            using var vertexArray = new NativeArray<float3>(vertices.ToArray(), Allocator.Temp);
            using var indexArray  = new NativeArray<int>(indices.ToArray(), Allocator.Temp);
            return MeshManifoldValidation.Classify(vertexArray, indexArray, 0);
        }

        static void AssertClosed(ContentsTreeHarness harness)
        {
            var report = Classify(harness);
            Assert.That(report.degenerateTriangleCount, Is.Zero, "triangles without area");
            Assert.That(report.nonManifoldEdgeCount, Is.Zero, "edges drawn by more than two triangles");
            Assert.That(report.boundaryEdgeCount, Is.Zero,
                        $"edges drawn by one triangle, {report.boundaryEdgeLength} long in all, the longest from {report.longestBoundaryEdgeFrom}");
        }

        // the check itself: a triangle left out leaves its edges open
        [Test]
        public void ATriangleLeftOut_IsFound()
        {
            using var harness = Build(new ChiselDecalInstance[0]);
            Assert.That(Classify(harness, leaveOut: 0).boundaryEdgeCount, Is.EqualTo(3));
        }

        [Test]
        public void WithoutDecals_TheModelIsClosed()
        {
            using var harness = Build(new ChiselDecalInstance[0]);
            AssertClosed(harness);
        }

        [Test]
        public void ADecalAcrossTheSeam_LeavesTheModelClosed()
        {
            var rotated = math.mul(kDown, quaternion.RotateZ(0.4f));
            using var harness = Build(new[] { Decal(1, new float3(0.1f, 0, 0.2f), rotated, new float3(1.3f, 0.9f, 0.5f)) });
            AssertClosed(harness);
        }

        [Test]
        public void OverlappingDecals_LeaveTheModelClosed()
        {
            using var harness = Build(new[]
            {
                Decal(1, new float3(0.1f, 0, 0.2f), math.mul(kDown, quaternion.RotateZ(0.4f)), new float3(1.3f, 0.9f, 0.5f)),
                Decal(2, new float3(-0.3f, 0, -0.1f), math.mul(kDown, quaternion.RotateZ(-0.7f)), new float3(0.8f, 1.6f, 0.5f))
            });
            AssertClosed(harness);
        }

        [Test]
        public void APolygonDecalAcrossTheSeam_LeavesTheModelClosed()
        {
            float2[] lShape =
            {
                new float2(-0.9f, -1.1f), new float2(1.1f, -1.1f), new float2(1.1f, -0.1f),
                new float2( 0.1f, -0.1f), new float2(0.1f,  0.9f), new float2(-0.9f, 0.9f)
            };
            var decal = Decal(1, new float3(0.13f, 0, 0.07f), math.mul(kDown, quaternion.RotateZ(0.3f)), new float3(1, 1, 0.5f),
                              ChiselDecalProjection.Polygon);
            decal.outlineCount = lShape.Length;
            using var harness = Build(new[] { decal }, lShape);
            AssertClosed(harness);
        }

        // Decals placed at random over the top and its edges, alone and overlapping
        [Test]
        public void RandomDecals_LeaveTheModelClosed([Range(0, 199)] int seed)
        {
            var random = new Random((uint)(seed * 7919 + 13));
            var count  = 1 + seed % 3;
            var decals = new ChiselDecalInstance[count];
            for (int d = 0; d < count; d++)
            {
                var direction = math.normalize(new float3(random.NextFloat(-0.6f, 0.6f), -1, random.NextFloat(-0.6f, 0.6f)));
                var rotation  = math.mul(quaternion.LookRotation(direction, new float3(0, 0, 1)), quaternion.RotateZ(random.NextFloat(0, 6.28f)));
                var position  = new float3(random.NextFloat(-2.3f, 2.3f), random.NextFloat(-0.3f, 0.2f), random.NextFloat(-2.3f, 2.3f));
                var size      = new float3(random.NextFloat(0.2f, 2.5f), random.NextFloat(0.2f, 2.5f), random.NextFloat(0.3f, 1.5f));
                decals[d] = Decal((ulong)(d + 1), position, rotation, size);
            }
            using var harness = Build(decals);
            AssertClosed(harness);
        }

        [Test]
        public void ADecalOverAnEdge_LeavesTheModelClosed()
        {
            // over the top's edge at x = 2, reaching down the side
            var slanted = math.mul(quaternion.LookRotation(math.normalize(new float3(-1, -1, 0)), new float3(0, 0, 1)), quaternion.RotateZ(0.2f));
            using var harness = Build(new[] { Decal(1, new float3(2, 0, 0.3f), slanted, new float3(0.9f, 0.7f, 1.2f)) });
            AssertClosed(harness);
        }
    }
}
