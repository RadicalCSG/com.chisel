using NUnit.Framework;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Mathematics;

namespace Chisel.Core.Tests
{
    [TestFixture]
    public class LoopSplitCapacityTests
    {
        const float kHalf = 5f;

        static BlobAssetReference<BrushTreeSpacePlanes> Planes(float4[] planes)
        {
            using var builder = new BlobBuilder(Allocator.Temp);
            ref var root = ref builder.ConstructRoot<BrushTreeSpacePlanes>();
            var array = builder.Allocate(ref root.treeSpacePlanes, planes.Length);
            for (int i = 0; i < planes.Length; i++)
                array[i] = planes[i];
            return builder.CreateBlobAssetReference<BrushTreeSpacePlanes>(Allocator.Persistent);
        }

        // Six planes, with outward normals (n·p + w <= 0 inside).
        static float4[] Diamond(float r)
        {
            var s = math.sqrt(0.5f);
            return new[]
            {
                new float4( s, 0,  s, -r * s),
                new float4( s, 0, -s, -r * s),
                new float4(-s, 0,  s, -r * s),
                new float4(-s, 0, -s, -r * s),
                new float4( 0, -1, 0, -1),
                new float4( 0,  1, 0, -1),
            };
        }

        // The brush the square is a face of.
        static float4[] SquareBrush()
        {
            return new[]
            {
                new float4(-1, 0, 0, -kHalf), new float4(1, 0, 0, -kHalf),
                new float4( 0, -1, 0, -1),    new float4(0, 1, 0, -1),
                new float4( 0, 0, -1, -kHalf), new float4(0, 0, 1, -kHalf),
            };
        }

        static float3[] Square()
        {
            return new[]
            {
                new float3(-kHalf, 0, -kHalf), new float3(kHalf, 0, -kHalf),
                new float3( kHalf, 0,  kHalf), new float3(-kHalf, 0,  kHalf),
            };
        }

        // The square's outline with extra points on every side at +-offset from the side's middle, in loop order.
        static float3[] SplitSquare(params float[] offsets)
        {
            var sorted = new float[offsets.Length * 2];
            for (int i = 0; i < offsets.Length; i++)
            {
                sorted[i] = -offsets[i];
                sorted[offsets.Length + i] = offsets[i];
            }
            System.Array.Sort(sorted);

            var ring = new System.Collections.Generic.List<float3>();
            ring.Add(new float3(-kHalf, 0, -kHalf));
            foreach (var t in sorted) ring.Add(new float3(t, 0, -kHalf));      // along z = -5, x rising
            ring.Add(new float3(kHalf, 0, -kHalf));
            foreach (var t in sorted) ring.Add(new float3(kHalf, 0, t));       // along x = +5, z rising
            ring.Add(new float3(kHalf, 0, kHalf));
            for (int i = sorted.Length - 1; i >= 0; i--)
                ring.Add(new float3(sorted[i], 0, kHalf));                       // along z = +5, x falling
            ring.Add(new float3(-kHalf, 0, kHalf));
            for (int i = sorted.Length - 1; i >= 0; i--)
                ring.Add(new float3(-kHalf, 0, sorted[i]));                      // along x = -5, z falling
            return ring.ToArray();
        }

        // A closed loop through the ring's points, in a list allocated with exactly the given capacity request.
        static UnsafeList<Edge> Loop(HashedVertices vertices, float3[] ring, int capacity)
        {
            Assert.That(capacity, Is.GreaterThanOrEqualTo(ring.Length));
            var edges = new UnsafeList<Edge>(capacity, Allocator.Temp);
            var first = vertices.AddNoResize(ring[0]);
            var previous = first;
            for (int i = 1; i < ring.Length; i++)
            {
                var current = vertices.AddNoResize(ring[i]);
                edges.AddNoResize(new Edge { index1 = previous, index2 = current });
                previous = current;
            }
            edges.AddNoResize(new Edge { index1 = previous, index2 = first });
            return edges;
        }

        [Test]
        public void AFaceCutByManyBrushes_OutgrowsTheEdgeListTheJobAllocates()
        {
            const int kBrushes = 6;
            using var vertices = new HashedVertices(64, Allocator.Temp);
            // Sized as the job sizes a base polygon's list: its own edges plus four per intersecting brush.
            var edges = Loop(vertices, Square(), 4 + kBrushes * 4);
            var capacity = edges.Capacity;

            for (int b = 0; b < kBrushes; b++)
            {
                var diamond = Planes(Diamond(5.5f + 0.3f * b));
                default(FindLoopOverlapIntersectionsJob).FindBasePolygonPlaneIntersections(
                    ref diamond.Value.treeSpacePlanes, WeldIncidenceFilter.Disabled, ref edges, vertices);
                diamond.Dispose();
            }

            Assert.That(edges.Length, Is.EqualTo(4 + kBrushes * 8), "every brush crosses each side of the square twice");
            Assert.That(edges.Length, Is.GreaterThan(capacity), "the fixture must outgrow the list, or it proves nothing");
            Assert.That(LoopValidation.IsValid(in edges, vertices.Length), Is.True, "the face is still one closed loop");
            edges.Dispose();
        }

        [Test]
        public void AnIntersectionLoopCutByManyLoops_OutgrowsTheEdgeListCopyFromAllocates()
        {
            const int kBrushes = 6;
            var planes = new NativeArray<BlobAssetReference<BrushTreeSpacePlanes>>(kBrushes, Allocator.Temp);
            for (int b = 0; b < kBrushes; b++)
                planes[b] = Planes(Diamond(5.5f + 0.3f * b));

            using var vertices = new HashedVertices(64, Allocator.Temp);
            // Sized as CopyFrom sizes an intersection loop: its own edges plus four per loop of the brush.
            var edges = Loop(vertices, Square(), 4 + kBrushes * 4);
            var capacity = edges.Capacity;

            for (int b = 0; b < kBrushes; b++)
            {
                default(FindLoopOverlapIntersectionsJob).FindLoopPlaneIntersections(
                    planes, WeldIncidenceFilter.Disabled, b, vertices, ref edges);
            }

            Assert.That(edges.Length, Is.EqualTo(4 + kBrushes * 8), "every brush crosses each side of the square twice");
            Assert.That(edges.Length, Is.GreaterThan(capacity), "the fixture must outgrow the list, or it proves nothing");
            Assert.That(LoopValidation.IsValid(in edges, vertices.Length), Is.True, "the loop is still one closed loop");

            edges.Dispose();
            for (int b = 0; b < kBrushes; b++)
                planes[b].Dispose();
            planes.Dispose();
        }

        [Test]
        public void ABrushCrossingTwicePerPlane_DoesNotOverflowTheVertexList()
        {
            const int kPlanes = 6, kCrossings = 8;
            var diamond = Planes(Diamond(6f));
            using var vertices = new HashedVertices(64, Allocator.Temp);
            var edges = Loop(vertices, Square(), 16);

            // Leave room for one new vertex per plane but not one per crossing. That is the state in which reserving
            // one vertex per plane did not grow the list, and the eight crossings then wrote past its end.
            var y = 100f;
            while (vertices.Capacity - vertices.Length >= kCrossings)
            {
                vertices.AddNoResize(new float3(0, y, 0));     // unrelated vertices, far apart and far from the face
                y += 1f;
            }
            Assert.That(vertices.Capacity - vertices.Length, Is.GreaterThanOrEqualTo(kPlanes),
                        "the fixture must leave room for one vertex per plane, or the old reservation would have grown it");
            var before = vertices.Length;

            default(FindLoopOverlapIntersectionsJob).FindBasePolygonPlaneIntersections(
                ref diamond.Value.treeSpacePlanes, WeldIncidenceFilter.Disabled, ref edges, vertices);

            Assert.That(vertices.Length - before, Is.EqualTo(kCrossings), "one new vertex per crossing");
            Assert.That(edges.Length, Is.EqualTo(4 + kCrossings));
            Assert.That(LoopValidation.IsValid(in edges, vertices.Length), Is.True);

            edges.Dispose();
            diamond.Dispose();
        }

        [Test]
        public void ManyVerticesLyingOnALoop_OutgrowItsEdgeList()
        {
            var brush = Planes(SquareBrush());
            using var vertices = new HashedVertices(64, Allocator.Temp);
            var self = Loop(vertices, Square(), 4);
            var capacity = self.Capacity;
            // A neighbouring loop that shares the corners and has six more points on every side.
            var other = Loop(vertices, SplitSquare(0.5f, 0.8f, 1.1f), 64);

            default(FindLoopOverlapIntersectionsJob).FindLoopVertexOverlaps(ref brush.Value.treeSpacePlanes, ref self, other, vertices);

            Assert.That(self.Length, Is.EqualTo(4 + 4 * 6), "each side is split at the other loop's six points on it");
            Assert.That(self.Length, Is.GreaterThan(capacity), "the fixture must outgrow the list, or it proves nothing");
            Assert.That(LoopValidation.IsValid(in self, vertices.Length), Is.True);

            other.Dispose();
            self.Dispose();
            brush.Dispose();
        }
    }
}
