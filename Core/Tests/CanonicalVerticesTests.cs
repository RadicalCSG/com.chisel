using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Chisel.Core.Tests
{
    [TestFixture]
    public class CanonicalVerticesTests
    {
        // The pipeline inputs canonical vertices read, for brushes given as tree-space face planes with outward normals
        // (n·x + w <= 0 inside). Every brush touches every other one.
        sealed class Scene : IDisposable
        {
            public NativeList<BlobAssetReference<BrushTreeSpacePlanes>>  treeSpacePlanes;
            public NativeList<BlobAssetReference<BrushesTouchedByBrush>> touched;

            public Scene(params float4[][] brushes)
            {
                treeSpacePlanes = new NativeList<BlobAssetReference<BrushTreeSpacePlanes>>(brushes.Length, Allocator.Persistent);
                touched         = new NativeList<BlobAssetReference<BrushesTouchedByBrush>>(brushes.Length, Allocator.Persistent);
                for (int b = 0; b < brushes.Length; b++)
                {
                    // Normalized as CreateBrushTreeSpacePlanesJob does it
                    treeSpacePlanes.Add(Blob(brushes[b].Select(p => p / math.length(p.xyz)).ToArray()));
                    touched.Add(Touching(b, brushes.Length));
                }
            }

            public CanonicalVertices Canonical(CanonicalVertexStage stage = CanonicalVertexStage.Positions)
            {
                return CanonicalVertices.Create(stage, treeSpacePlanes, touched);
            }

            public void Dispose()
            {
                foreach (var blob in treeSpacePlanes) blob.Dispose();
                foreach (var blob in touched) blob.Dispose();
                treeSpacePlanes.Dispose();
                touched.Dispose();
            }

            static BlobAssetReference<BrushTreeSpacePlanes> Blob(float4[] planes)
            {
                using var builder = new BlobBuilder(Allocator.Temp);
                ref var root = ref builder.ConstructRoot<BrushTreeSpacePlanes>();
                var array = builder.Allocate(ref root.treeSpacePlanes, planes.Length);
                for (int i = 0; i < planes.Length; i++)
                    array[i] = planes[i];
                root.faceCount = planes.Length;
                return builder.CreateBlobAssetReference<BrushTreeSpacePlanes>(Allocator.Persistent);
            }

            static BlobAssetReference<BrushesTouchedByBrush> Touching(int self, int count)
            {
                using var builder = new BlobBuilder(Allocator.Temp);
                ref var root = ref builder.ConstructRoot<BrushesTouchedByBrush>();
                var list = builder.Allocate(ref root.brushIntersections, count - 1);
                int n = 0;
                for (int i = 0; i < count; i++)
                {
                    if (i != self)
                        list[n++] = new BrushIntersection { nodeIndexOrder = new IndexOrder { nodeOrder = i }, type = IntersectionType.Intersection };
                }
                builder.Allocate(ref root.intersectionBits, 0);
                return builder.CreateBlobAssetReference<BrushesTouchedByBrush>(Allocator.Persistent);
            }
        }

        // An axis-aligned box as six outward planes.
        static float4[] Box(float3 min, float3 max)
        {
            return new[]
            {
                new float4(-1, 0, 0,  min.x), new float4(1, 0, 0, -max.x),
                new float4(0, -1, 0,  min.y), new float4(0, 1, 0, -max.y),
                new float4(0, 0, -1,  min.z), new float4(0, 0, 1, -max.z),
            };
        }

        // Rotates planes: a plane (n, w) through a rotation R becomes (R n, w).
        static float4[] Rotate(float4[] planes, quaternion rotation)
        {
            return planes.Select(p => new float4(math.mul(rotation, p.xyz), p.w)).ToArray();
        }

        static readonly quaternion kTurn = quaternion.Euler(0.3f, 0.7f, -0.2f);

        static bool SameBits(float a, float b) => BitConverter.SingleToInt32Bits(a) == BitConverter.SingleToInt32Bits(b);
        static bool SameBits(float3 a, float3 b) => SameBits(a.x, b.x) && SameBits(a.y, b.y) && SameBits(a.z, b.z);
        static bool SameBits(float4 a, float4 b) => SameBits(a.xyz, b.xyz) && SameBits(a.w, b.w);

        // Two turned boxes side by side. The second's shared face is nudged 0.2 um, so the brushes' own planes
        // differ, as they do after transforms.
        static float4[][] TwoTurnedBoxes()
        {
            var a = Rotate(Box(new float3(0, 0, 0), new float3(1, 1, 1)), kTurn);
            var b = Rotate(Box(new float3(1, 0, 0), new float3(2, 1, 1)), kTurn);
            b[0].w += 2e-7f;
            return new[] { a, b };
        }

        static float3 SharedCorner => math.mul(kTurn, new float3(1, 0, 0));

        [Test]
        public void ASharedCorner_HasTheSameBitsFromEitherBrush()
        {
            using var scene = new Scene(TwoTurnedBoxes());
            var canonical = scene.Canonical();

            var fromA = canonical.Canonicalize(SharedCorner + new float3(1e-6f, -2e-6f, 0), 0);
            var fromB = canonical.Canonicalize(SharedCorner + new float3(-2e-6f, 1e-6f, 1e-6f), 1);

            Assert.That(SameBits(fromA, fromB), Is.True, $"{fromA} from the first brush, {fromB} from the second");
            Assert.That(math.distance(fromA, SharedCorner), Is.LessThan(1e-5f));
        }

        [Test]
        public void CanonicalizingTwice_ChangesNothing()
        {
            using var scene = new Scene(TwoTurnedBoxes());
            var canonical = scene.Canonical();

            var once  = canonical.Canonicalize(SharedCorner + new float3(3e-6f, 0, -1e-6f), 0);
            var twice = canonical.Canonicalize(once, 1);

            Assert.That(SameBits(once, twice), Is.True);
        }

        // The key is sorted by plane, so which brush comes first, and therefore the order its planes were gathered in,
        // doesn't matter.
        [Test]
        public void BrushOrder_DoesNotChangeTheResult()
        {
            var brushes = TwoTurnedBoxes();
            using var forward  = new Scene(brushes[0], brushes[1]);
            using var backward = new Scene(brushes[1], brushes[0]);

            var approximate = SharedCorner + new float3(1e-6f, 1e-6f, 0);
            var fromForward  = forward.Canonical().Canonicalize(approximate, 0);
            var fromBackward = backward.Canonical().Canonicalize(approximate, 1);

            Assert.That(SameBits(fromForward, fromBackward), Is.True, $"{fromForward} against {fromBackward}");
        }

        // The trim case in miniature: a neighbour whose bottom face sits a little above this brush's, so their
        // corners are a sliver apart. Thicker than τ it is a real feature and both corners stay.
        [Test]
        public void ASliverThickerThanTau_KeepsBothCorners()
        {
            const float kSliver = 0.002f;
            using var scene = new Scene(Box(new float3(0, 0, 0), new float3(1, 1, 1)),
                                        Box(new float3(1, kSliver, 0), new float3(2, 1, 1)));
            var canonical = scene.Canonical();

            var corner = canonical.Canonicalize(new float3(1, 0, 0), 0);
            var raised = canonical.Canonicalize(new float3(1, kSliver, 0), 1);

            Assert.That(corner.y, Is.EqualTo(0).Within(1e-6f));
            Assert.That(raised.y, Is.EqualTo(kSliver).Within(1e-6f));
            Assert.That(math.distance(corner, raised), Is.GreaterThan(CanonicalVertices.kSameVertex),
                        "two different vertices, however close");
        }

        // Thinner than τ both bottom faces pass through the corner, and both brushes pick the same one of them.
        [Test]
        public void ASliverThinnerThanTau_BecomesOneCornerOnBothBrushes()
        {
            const float kSliver = 0.0003f;
            using var scene = new Scene(Box(new float3(0, 0, 0), new float3(1, 1, 1)),
                                        Box(new float3(1, kSliver, 0), new float3(2, 1, 1)));
            var canonical = scene.Canonical();

            var corner = canonical.Canonicalize(new float3(1, 0, 0), 0);
            var raised = canonical.Canonicalize(new float3(1, kSliver, 0), 1);

            Assert.That(SameBits(corner, raised), Is.True, $"{corner} against {raised}");
        }

        [Test]
        public void ABrushAddedOnTheSamePlane_DoesNotMoveCornersItDoesNotContain()
        {
            var left   = Box(new float3(0, 0, 0), new float3(1, 1, 1));
            var middle = Box(new float3(1, 0, 0), new float3(2, 1, 1));
            // Added by the edit: it touches the middle box only, and its top is 0.3 mm higher, less than τ
            var right  = Box(new float3(2, 0, 0), new float3(3, 1.0003f, 1));
            using var before = new Scene(left, middle);
            using var after  = new Scene(left, middle, right);

            // The corner the left and middle boxes share, as the middle box finds it before and after the edit
            var approximate = new float3(1, 1, 0) + new float3(1e-6f, -1e-6f, 0);
            var unedited = before.Canonical().Canonicalize(approximate, 1);
            var edited   = after.Canonical().Canonicalize(approximate, 1);
            Assert.That(SameBits(unedited, edited), Is.True, $"{unedited} before the edit, {edited} after it");
            Assert.That(unedited, Is.EqualTo(new float3(1, 1, 0)));

            // Where the new brush does meet the middle box, the two agree
            var fromMiddle = after.Canonical().Canonicalize(new float3(2, 1, 0), 1);
            var fromRight  = after.Canonical().Canonicalize(new float3(2, 1.0003f, 0), 2);
            Assert.That(SameBits(fromMiddle, fromRight), Is.True, $"{fromMiddle} from the middle box, {fromRight} from the new one");
        }

        // 9e's seventh-plane case: a plane that only touches the brush along an edge lies on the corners of that edge.
        // It joins their key, but the corner stays where its three faces put it.
        [Test]
        public void APlaneThroughAnEdge_JoinsTheKeyButDoesNotMoveTheCorner()
        {
            var box = Box(new float3(0, 0, 0), new float3(1, 1, 1));
            // x + y <= 2: it touches the box only along the edge x = 1, y = 1
            var withDiagonal = box.Append(new float4(math.normalize(new float3(1, 1, 0)), -math.sqrt(2f))).ToArray();
            using var scene = new Scene(withDiagonal);
            var canonical = scene.Canonical();

            var key = new PlaneKey();
            canonical.Gather(new float3(1, 1, 0), 0, ref key);
            Assert.That(key.Length, Is.EqualTo(4), "x = 1, y = 1, z = 0 and the diagonal");

            var corner = canonical.Canonicalize(new float3(1, 1, 0) + new float3(2e-6f, -1e-6f, 1e-6f), 0);
            Assert.That(corner.x, Is.EqualTo(1f));
            Assert.That(corner.y, Is.EqualTo(1f));
            Assert.That(corner.z, Is.EqualTo(0f));
        }

        // A plane is infinite. A brush far away with a face on the same plane does not contain the corner, so it
        // does not take part.
        [Test]
        public void AFarBrushOnTheSamePlane_DoesNotChangeTheCorner()
        {
            var near = Box(new float3(0, 0, 0), new float3(1, 1, 1));
            var far  = Box(new float3(1, 5, 0), new float3(1.5f, 6, 1));   // its -x face is the plane x = 1
            using var alone = new Scene(near);
            using var both  = new Scene(near, far);

            var approximate = new float3(1, 0, 0) + new float3(1e-6f, 0, 0);
            var key = new PlaneKey();
            both.Canonical().Gather(approximate, 0, ref key);
            Assert.That(key.Length, Is.EqualTo(3));
            Assert.That(SameBits(alone.Canonical().Canonicalize(approximate, 0), both.Canonical().Canonicalize(approximate, 0)), Is.True);
        }

        // A key holds each plane once, counting a plane and its twin as one, and what it holds doesn't depend on the
        // order the planes came in, even when there are more of them than it has room for.
        [Test]
        public void APlaneKey_DependsOnlyOnItsPlanes()
        {
            var random = new Unity.Mathematics.Random(12345);
            var planes = new List<float4>();
            for (int i = 0; i < PlaneKey.kCapacity + 8; i++)
                planes.Add(new float4(random.NextFloat3Direction(), random.NextFloat(-2, 2)));

            var forward = new PlaneKey();
            foreach (var plane in planes)
                forward.Add(plane);
            var backward = new PlaneKey();
            for (int i = planes.Count - 1; i >= 0; i--)
            {
                backward.Add(-planes[i]);
                backward.Add(planes[i]);
            }

            Assert.That(forward.Overflowed, Is.True);
            Assert.That(forward.Length, Is.EqualTo(PlaneKey.kCapacity));
            Assert.That(backward.Length, Is.EqualTo(PlaneKey.kCapacity));
            for (int i = 0; i < forward.Length; i++)
                Assert.That(SameBits(forward[i], backward[i]), Is.True, $"plane {i}: {forward[i]} against {backward[i]}");
        }

        // -0 and +0 are equal but have different bits, and a twin's negation is full of -0.
        [Test]
        public void APlaneKey_StoresOneKindOfZero()
        {
            var key = new PlaneKey();
            key.Add(new float4(0, -1, 0, 0));   // turned around, this is (-0, 1, -0, -0)
            key.Add(new float4(0, 1, 0, 0));
            Assert.That(key.Length, Is.EqualTo(1));
            Assert.That(SameBits(key[0], new float4(0, 1, 0, 0)), Is.True, $"{key[0]}");
        }

        [Test]
        public void Measure_CountsButDoesNotMove()
        {
            using var scene = new Scene(TwoTurnedBoxes());
            var approximate = SharedCorner + new float3(3e-6f, 0, 0);

            CanonicalVertexStats.Reset();
            var measured = scene.Canonical(CanonicalVertexStage.Measure).Canonicalize(approximate, 0);

            Assert.That(SameBits(measured, approximate), Is.True);
            Assert.That(CanonicalVertexStats.Get(CanonicalVertexStats.kComputed), Is.EqualTo(1));
            Assert.That(CanonicalVertexStats.Get(CanonicalVertexStats.kMoved), Is.EqualTo(1));
        }

        [Test]
        public void Off_IsInert()
        {
            var approximate = new float3(1.25f, -3.5f, 7);
            Assert.That(SameBits(CanonicalVertices.Disabled.Canonicalize(approximate, 0), approximate), Is.True);
            Assert.That(CanonicalVertices.Disabled.Computes, Is.False);
        }

        // With canonical positions, the weld merges only the same vertex, however close another one is.
        [Test]
        public void TheSameVertexWeld_MergesOnlyTheSameVertex()
        {
            var filter = WeldIncidenceFilter.SameVertexOnly;
            var corner = new float3(4, 5, 6);
            Assert.That(filter.Allows(corner, corner), Is.True);
            Assert.That(filter.Allows(corner, corner + new float3(0.000005f, 0, 0)), Is.True, "0.005 mm: rounding");
            Assert.That(filter.Allows(corner, corner + new float3(0.0001f, 0, 0)), Is.False, "0.1 mm: a different vertex");

            using var hashed = new HashedVertices(16, Allocator.Temp);
            var first  = hashed.AddNoResize(corner, in filter);
            var second = hashed.AddNoResize(corner + new float3(0, 0.002f, 0), in filter);
            Assert.That(second, Is.Not.EqualTo(first), "2 mm apart: two vertices, where the distance weld made one");
        }
    }
}
