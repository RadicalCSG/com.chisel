using System.Collections.Generic;
using NUnit.Framework;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Chisel.Core.Tests
{
    // A conical frustum whose top isn't its bottom scaled the same along x and z has sides that don't lie in planes, and
    // isn't convex: it is made of convex brushes that together are its shape
    [TestFixture]
    public class ChiselCylinderFrustumTests
    {
        const float kEpsilon = 1e-4f;

        static ChiselCylinder Frustum(float2 top, float2 bottom, int sides)
        {
            var settings = ChiselCylinder.DefaultSettings;
            settings.type            = CylinderShapeType.ConicalFrustum;
            settings.isElliptical    = true;
            settings.topDiameterX    = top.x;
            settings.topDiameterZ    = top.y;
            settings.bottomDiameterX = bottom.x;
            settings.bottomDiameterZ = bottom.y;
            settings.height          = 2;
            settings.sides           = sides;
            return settings;
        }

        // Every surface drawn and collided with, so a face that isn't output can be told apart
        static BlobAssetReference<InternalChiselSurfaceArray> Surfaces(int count)
        {
            using var builder = new BlobBuilder(Allocator.Temp);
            ref var root = ref builder.ConstructRoot<InternalChiselSurfaceArray>();
            var surfaces = builder.Allocate(ref root.surfaces, count);
            for (int i = 0; i < count; i++)
            {
                var surface = InternalChiselSurface.Default;
                surface.destinationFlags = SurfaceDestinationFlags.Default;
                surfaces[i] = surface;
            }
            return builder.CreateBlobAssetReference<InternalChiselSurfaceArray>(Allocator.Persistent);
        }

        struct Face
        {
            public float3[] corners;   // in order, going round it facing out
            public int      surface;
            public bool     shown;
            public float4   plane;
        }

        // The faces of each brush the generator makes
        static List<List<Face>> Brushes(ChiselCylinder settings)
        {
            Assert.That(settings.Validate(), Is.True);
            var surfaces = Surfaces(settings.RequiredSurfaceCount);
            var count    = settings.PrepareAndCountRequiredBrushMeshes();
            var nodes    = new NativeList<GeneratedNode>(count, Allocator.Temp);
            try
            {
                nodes.Resize(count, NativeArrayOptions.ClearMemory);
                Assert.That(settings.GenerateNodes(surfaces, nodes, Allocator.Temp), Is.True, "generated");
                var brushes = new List<List<Face>>();
                foreach (var node in nodes)
                {
                    Assert.That(node.brushMesh.IsCreated, Is.True, "every brush is made");
                    ref var mesh = ref node.brushMesh.Value;
                    var faces = new List<Face>();
                    for (int p = 0; p < mesh.polygons.Length; p++)
                    {
                        ref var polygon = ref mesh.polygons[p];
                        var corners = new float3[polygon.edgeCount];
                        for (int e = 0; e < polygon.edgeCount; e++)
                            corners[e] = mesh.localVertices[mesh.halfEdges[polygon.firstEdge + e].vertexIndex];
                        faces.Add(new Face
                        {
                            corners = corners,
                            surface = polygon.descriptionIndex,
                            shown   = polygon.surface.destinationFlags != SurfaceDestinationFlags.None,
                            plane   = mesh.localPlanes[p]
                        });
                    }
                    brushes.Add(faces);
                    node.brushMesh.Dispose();
                }
                return brushes;
            }
            finally
            {
                nodes.Dispose();
                surfaces.Dispose();
                settings.Dispose();
            }
        }

        static void AssertConvexAndPlanar(List<List<Face>> brushes)
        {
            for (int b = 0; b < brushes.Count; b++)
            {
                foreach (var face in brushes[b])
                {
                    foreach (var corner in face.corners)
                        Assert.That(math.abs(math.dot(face.plane.xyz, corner) + face.plane.w), Is.LessThan(kEpsilon), $"brush {b}: a face out of its plane");
                    foreach (var other in brushes[b])
                    {
                        foreach (var corner in other.corners)
                            Assert.That(math.dot(face.plane.xyz, corner) + face.plane.w, Is.LessThan(kEpsilon), $"brush {b} isn't convex");
                    }
                }
            }
        }

        static bool Inside(List<Face> brush, float3 point)
        {
            foreach (var face in brush)
            {
                if (math.dot(face.plane.xyz, point) + face.plane.w > 1e-6f)
                    return false;
            }
            return true;
        }

        // Points of a face: its middle, and towards each corner and the middle of each edge from it
        static IEnumerable<float3> Samples(Face face)
        {
            var center = float3.zero;
            foreach (var corner in face.corners)
                center += corner;
            center /= face.corners.Length;
            yield return center;
            for (int i = 0; i < face.corners.Length; i++)
            {
                var next = face.corners[(i + 1) % face.corners.Length];
                yield return math.lerp(center, face.corners[i], 0.9f);
                yield return math.lerp(center, (face.corners[i] + next) * 0.5f, 0.9f);
            }
        }

        // The faces that are output close round the shape and are its outside, no other brush over them; every face
        // that isn't is inside another brush, so the brushes leave no gap
        static void AssertTheShownFacesCloseRoundTheBrushes(List<List<Face>> brushes)
        {
            var shown = new List<Face>();
            foreach (var brush in brushes)
                foreach (var face in brush)
                    if (face.shown) shown.Add(face);

            var edges = new Dictionary<(float3, float3), int>();
            foreach (var face in shown)
            {
                for (int i = 0; i < face.corners.Length; i++)
                {
                    var key = (face.corners[i], face.corners[(i + 1) % face.corners.Length]);
                    edges.TryGetValue(key, out var n);
                    edges[key] = n + 1;
                }
            }
            foreach (var edge in edges)
            {
                edges.TryGetValue((edge.Key.Item2, edge.Key.Item1), out var back);
                Assert.That(back, Is.EqualTo(edge.Value), $"the shown faces have a hole at {edge.Key.Item1} to {edge.Key.Item2}");
            }

            for (int b = 0; b < brushes.Count; b++)
            {
                foreach (var face in brushes[b])
                {
                    foreach (var sample in Samples(face))
                    {
                        var outside = sample + face.plane.xyz * 1e-4f;
                        var covered = false;
                        for (int o = 0; o < brushes.Count && !covered; o++)
                            covered = o != b && Inside(brushes[o], outside);
                        if (face.shown)
                            Assert.That(covered, Is.False, $"brush {b}: a shown face at {sample} is inside another brush");
                        else
                            Assert.That(covered, Is.True, $"brush {b}: a face inside the shape at {sample} is a hole");
                    }
                }
            }
        }

        // The caps on their planes, the bottom lowest, each side with its own surface
        static void AssertTheSurfaces(List<List<Face>> brushes, int sides)
        {
            float lowest = float.MaxValue, highest = float.MinValue;
            foreach (var brush in brushes)
                foreach (var face in brush)
                    foreach (var corner in face.corners)
                    {
                        lowest  = math.min(lowest, corner.y);
                        highest = math.max(highest, corner.y);
                    }
            var used = new HashSet<int>();
            foreach (var brush in brushes)
            {
                foreach (var face in brush)
                {
                    if (!face.shown)
                        continue;
                    used.Add(face.surface);
                    if (face.surface == 0)
                        foreach (var corner in face.corners) Assert.That(corner.y, Is.EqualTo(lowest).Within(kEpsilon), "the bottom on the bottom");
                    else if (face.surface == 1)
                        foreach (var corner in face.corners) Assert.That(corner.y, Is.EqualTo(highest).Within(kEpsilon), "the top on the top");
                    else
                        Assert.That(face.surface, Is.InRange(2, sides + 1), "a side's surface");
                }
            }
            for (int s = 0; s < sides + 2; s++)
                Assert.That(used, Does.Contain(s), $"surface {s} is shown");
        }

        [Test]
        public void AnUndistortedEllipticalFrustum_IsOneBrush()
        {
            var brushes = Brushes(Frustum(new float2(1, 0.5f), new float2(2, 1), 16));
            Assert.That(brushes.Count, Is.EqualTo(1));
            AssertConvexAndPlanar(brushes);
            AssertTheSurfaces(brushes, 16);
        }

        [Test]
        public void AWarpedFrustum_IsMadeOfConvexBrushes_ThatAreItsShape([Values(3, 5, 7, 16)] int sides)
        {
            var brushes = Brushes(Frustum(new float2(2, 0.5f), new float2(0.5f, 2), sides));
            Assert.That(brushes.Count, Is.EqualTo(sides), "it isn't convex: a brush for each side");
            AssertConvexAndPlanar(brushes);
            AssertTheShownFacesCloseRoundTheBrushes(brushes);
            AssertTheSurfaces(brushes, sides);
        }

        [Test]
        public void AFrustumTwistedFarAround_IsMadeOfConvexBrushes_ThatAreItsShape()
        {
            var brushes = Brushes(Frustum(new float2(10, 0.1f), new float2(0.1f, 10), 7));
            AssertConvexAndPlanar(brushes);
            AssertTheShownFacesCloseRoundTheBrushes(brushes);
            AssertTheSurfaces(brushes, 7);
        }

        // Four sides, each across one axis, lie in their planes however the top and bottom are stretched: one brush
        [Test]
        public void AFrustumWithFourFlatSides_IsOneBrush()
        {
            var brushes = Brushes(Frustum(new float2(2, 0.5f), new float2(0.5f, 2), 4));
            Assert.That(brushes.Count, Is.EqualTo(1));
            AssertConvexAndPlanar(brushes);
            AssertTheSurfaces(brushes, 4);
        }

        [Test]
        public void AFrustumWiderAtTheTop_IsMadeOfConvexBrushes_ThatAreItsShape()
        {
            var brushes = Brushes(Frustum(new float2(4, 1), new float2(0.5f, 2), 12));
            AssertConvexAndPlanar(brushes);
            AssertTheShownFacesCloseRoundTheBrushes(brushes);
            AssertTheSurfaces(brushes, 12);
        }

        // A top with one diameter at 0 is a line: it gets the smallest size, so its top is a face, and it isn't a cone
        [Test]
        public void ATopWithOneDiameterAtZero_MakesAValidShape([Values] bool zeroAlongX)
        {
            var top      = zeroAlongX ? new float2(0, 1) : new float2(1, 0);
            var settings = Frustum(top, new float2(2, 2), 16);
            var brushes  = Brushes(settings);
            AssertConvexAndPlanar(brushes);
            AssertTheShownFacesCloseRoundTheBrushes(brushes);
            AssertTheSurfaces(brushes, 16);

            var extent = float2.zero;
            foreach (var brush in brushes)
                foreach (var face in brush)
                    if (face.shown && face.surface == 1)
                        foreach (var corner in face.corners)
                            extent = math.max(extent, math.abs(corner.xz));
            var along  = zeroAlongX ? extent.y : extent.x;
            var across = zeroAlongX ? extent.x : extent.y;
            Assert.That(along, Is.EqualTo(0.5f).Within(kEpsilon), "the top is as wide as it was set");
            Assert.That(across, Is.EqualTo(ChiselGeneratorLimits.kMinSize * 0.5f).Within(kEpsilon), "and the smallest size across");
        }
    }
}
