using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;

namespace Chisel.Core.Tests
{
    [TestFixture]
    public class InternedPlanesTests
    {
        static float4 Plane(float3 normal, float3 pointOn)
        {
            var n = math.normalize(normal);
            return new float4(n, -math.dot(n, pointOn));
        }

        [Test]
        public void IdenticalPlanes_ShareOneId()
        {
            using var table = new InternedPlanes(8, Allocator.Temp);
            var on = new float3(0, 5, 0);
            var p = Plane(new float3(0, 1, 0), on);

            var a = table.Add(p, on, out bool flippedA);
            var b = table.Add(p, on, out bool flippedB);

            Assert.That(b, Is.EqualTo(a));
            Assert.That(flippedA, Is.False);
            Assert.That(flippedB, Is.False);
            Assert.That(table.Count, Is.EqualTo(1), "an identical plane must not create a second entry");
        }

        [Test]
        public void OppositeFacingPlane_SharesIdAndReportsFlipped()
        {
            using var table = new InternedPlanes(8, Allocator.Temp);
            var on   = new float3(0, 5, 0);
            var up   = Plane(new float3(0, 1, 0), on);
            var down = Plane(new float3(0, -1, 0), on);

            var a = table.Add(up, on, out bool flippedA);
            var b = table.Add(down, on, out bool flippedB);

            Assert.That(b, Is.EqualTo(a), "a face and the face it abuts lie on ONE plane");
            Assert.That(flippedA, Is.False);
            Assert.That(flippedB, Is.True, "the caller must be told, or its inside/outside test inverts");
            Assert.That(table.Count, Is.EqualTo(1));
        }

        [Test]
        public void PlanesWithinTolerance_Share_OutsideTolerance_Differ()
        {
            using var table = new InternedPlanes(8, Allocator.Temp);
            var onBase = new float3(10, 0, 0);
            var baseline = Plane(new float3(1, 0, 0), onBase);

            // shifted by less than kPlaneDAlignEpsilon (0.0006), and by clearly more - the 0.0184
            // gap that produced the real hole in bm_c0a0a
            var onWithin  = new float3(10 + 0.0004f, 0, 0);
            var onWithout = new float3(10 + 0.0184f, 0, 0);

            var idBase    = table.Add(baseline, onBase, out _);
            var idWithin  = table.Add(Plane(new float3(1, 0, 0), onWithin),  onWithin,  out _);
            var idWithout = table.Add(Plane(new float3(1, 0, 0), onWithout), onWithout, out _);

            Assert.That(idWithin, Is.EqualTo(idBase));
            Assert.That(idWithout, Is.Not.EqualTo(idBase),
                        "0.0184 apart is two genuinely different walls; merging them would delete geometry");
            Assert.That(table.Count, Is.EqualTo(2));
        }

        [Test]
        public void UnnormalizedInput_MatchesNormalized()
        {
            using var table = new InternedPlanes(8, Allocator.Temp);
            var on = new float3(0, 0, 3);
            var p = Plane(new float3(0, 0, 1), on);

            var a = table.Add(p, on, out _);
            var b = table.Add(p * 7.5f, on, out bool flipped);   // same plane, arbitrary scale

            Assert.That(b, Is.EqualTo(a));
            Assert.That(flipped, Is.False);
            Assert.That(table.Count, Is.EqualTo(1));
        }

        [Test]
        public void NegativelyScaledInput_MatchesAndReportsFlipped()
        {
            using var table = new InternedPlanes(8, Allocator.Temp);
            var on = new float3(0, 0, 3);
            var p = Plane(new float3(0, 0, 1), on);

            var a = table.Add(p, on, out _);
            var b = table.Add(p * -2.0f, on, out bool flipped);

            Assert.That(b, Is.EqualTo(a));
            Assert.That(flipped, Is.True);
        }

        // The case that breaks canonicalising orientation up front: two near-identical normals
        // sitting either side of a hemisphere rule's boundary. They must still intern together.
        [Test]
        public void NormalsStraddlingAHemisphereBoundary_StillShareOneId()
        {
            using var table = new InternedPlanes(8, Allocator.Temp);
            var on = new float3(0, 3.0053f, 3.0053f);
            var na = math.normalize(new float3(+1e-8f, 0.70710678f, 0.70710678f));
            var nb = math.normalize(new float3(-1e-8f, 0.70710678f, 0.70710678f));

            var idA = table.Add(Plane(na, on), on, out _);
            var idB = table.Add(Plane(nb, on), on, out _);

            Assert.That(idB, Is.EqualTo(idA));
            Assert.That(table.Count, Is.EqualTo(1));
        }

        [Test]
        public void ParallelButDistinctPlanes_StayDistinct()
        {
            using var table = new InternedPlanes(8, Allocator.Temp);
            var on0 = new float3(0, 0, 0);
            var on1 = new float3(1, 0, 0);

            var id0  = table.Add(Plane(new float3(1, 0, 0), on0), on0, out _);
            var id1  = table.Add(Plane(new float3(1, 0, 0), on1), on1, out _);
            var id0b = table.Add(Plane(new float3(-1, 0, 0), on0), on0, out bool flipped);

            Assert.That(id1, Is.Not.EqualTo(id0), "two walls a metre apart are two planes");
            Assert.That(id0b, Is.EqualTo(id0));
            Assert.That(flipped, Is.True);
            Assert.That(table.Count, Is.EqualTo(2));
        }

        const float kTilt = 4e-4f;
        static float4 TiltedThroughOrigin() { return Plane(new float3(0, 1, kTilt), float3.zero); }
        static float3 OnTiltedAt(float z)   { return new float3(0, -kTilt * z, z); }

        [Test]
        public void TiltIsInsideTheAngularTolerance_SoOnlyThePointTestCanRejectIt()
        {
            var flat = Plane(new float3(0, 1, 0), float3.zero);
            var deviation = 1.0 - math.abs(math.dot(flat.xyz, TiltedThroughOrigin().xyz));
            Assert.That(deviation, Is.LessThan(InternedPlanes.kMaxNormalDeviation),
                        "if this ever fails the two tests below stop testing what they claim");
            // and the far point really is on the tilted plane, not the flat one
            Assert.That(math.abs(InternedPlanes.Distance(TiltedThroughOrigin(), OnTiltedAt(100))),
                        Is.LessThan(1e-5));
            Assert.That(math.abs(InternedPlanes.Distance(flat, OnTiltedAt(100))),
                        Is.GreaterThan(CSGConstants.kPlaneDAlignEpsilon));
        }

        [Test]
        public void NearlyParallelPlanes_ThatDivergeFarFromOrigin_AreNotMerged()
        {
            using var table = new InternedPlanes(8, Allocator.Temp);

            var idFlat   = table.Add(Plane(new float3(0, 1, 0), float3.zero), float3.zero, out _);
            var idTilted = table.Add(TiltedThroughOrigin(), OnTiltedAt(100), out _);

            Assert.That(idTilted, Is.Not.EqualTo(idFlat),
                        "they agree exactly at the origin but are 0.04 apart where the geometry is");
            Assert.That(table.Count, Is.EqualTo(2));
        }

        [Test]
        public void AllSuppliedPoints_MustLieOnTheMatchedPlane()
        {
            var flat = Plane(new float3(0, 1, 0), float3.zero);

            // vouching only for the shared point at the origin accepts the match ...
            using (var nearOnly = new InternedPlanes(8, Allocator.Temp))
            {
                var idFlat = nearOnly.Add(flat, float3.zero, out _);
                var idTilt = nearOnly.Add(TiltedThroughOrigin(), float3.zero, out _);
                Assert.That(idTilt, Is.EqualTo(idFlat),
                            "at the origin the two planes really are indistinguishable");
            }

            // ... but vouching for the whole face must not, because part of it is 0.04 away
            using (var wholeFace = new InternedPlanes(8, Allocator.Temp))
            {
                var idFlat = wholeFace.Add(flat, float3.zero, out _);
                using var pts = new NativeArray<float3>(new[] { float3.zero, OnTiltedAt(100) }, Allocator.Temp);
                var idBoth = wholeFace.Add(TiltedThroughOrigin(), pts, out _);
                Assert.That(idBoth, Is.Not.EqualTo(idFlat),
                            "a face is only on a plane if ALL of it is on that plane");
            }
        }

        [Test]
        public void MatchedPlane_StaysWithinToleranceOfTheCallersPoints()
        {
            using var table = new InternedPlanes(16, Allocator.Temp);
            var on0 = new float3(3, 7, 11);
            var id  = table.Add(Plane(new float3(0.3f, 0.9f, 0.31f), on0), on0, out _);

            // a slightly different plane through a nearby point that still matches
            var on1 = new float3(3.0001f, 7.0001f, 11.0001f);
            var id2 = table.Add(Plane(new float3(0.3f, 0.9f, 0.31f), on1), on1, out _);

            Assert.That(id2, Is.EqualTo(id), "these are the same wall");
            var stored = table[id];
            Assert.That(math.abs(InternedPlanes.Distance(stored, on1)),
                        Is.LessThanOrEqualTo(CSGConstants.kPlaneDAlignEpsilon),
                        "the invariant the whole design rests on: the shared plane still passes through the face");
        }

        [Test]
        public void TryFind_DoesNotInsert()
        {
            using var table = new InternedPlanes(8, Allocator.Temp);
            var on = new float3(0, 2, 0);
            var p = Plane(new float3(0, 1, 0), on);

            Assert.That(table.TryFind(p, on, out _, out _), Is.False);
            Assert.That(table.Count, Is.EqualTo(0));

            var id = table.Add(p, on, out _);
            Assert.That(table.TryFind(p, on, out int found, out _), Is.True);
            Assert.That(found, Is.EqualTo(id));
            Assert.That(table.Count, Is.EqualTo(1));
        }

        [Test]
        public void StoredPlane_IsNormalizedAndRetrievable()
        {
            using var table = new InternedPlanes(8, Allocator.Temp);
            var on = new float3(0, 3, 0);
            var id = table.Add(new float4(0, 4, 0, -12), on, out _);   // normal length 4

            var stored = table[id];
            Assert.That(math.length(stored.xyz), Is.EqualTo(1f).Within(1e-5f));
            Assert.That(stored.w, Is.EqualTo(-3f).Within(1e-5f));
        }

        [Test]
        public void ManyDistinctPlanes_AllGetDistinctIds_AndReAddingFindsThem()
        {
            using var table = new InternedPlanes(64, Allocator.Temp);
            const int kCount = 200;
            var ids = new int[kCount];
            for (int i = 0; i < kCount; i++)
            {
                var on = new float3(i * 0.5f, 0, 0);
                ids[i] = table.Add(Plane(new float3(1, 0, 0), on), on, out _);
            }

            Assert.That(table.Count, Is.EqualTo(kCount), "hash chaining must not merge distinct planes");
            for (int i = 1; i < kCount; i++)
                Assert.That(ids[i], Is.Not.EqualTo(ids[i - 1]));

            for (int i = 0; i < kCount; i++)
            {
                var on = new float3(i * 0.5f, 0, 0);
                Assert.That(table.Add(Plane(new float3(1, 0, 0), on), on, out _), Is.EqualTo(ids[i]));
            }
            Assert.That(table.Count, Is.EqualTo(kCount), "re-adding must find, not grow");
        }

        [Test]
        public void Clear_EmptiesTheTableAndItsHashChains()
        {
            using var table = new InternedPlanes(8, Allocator.Temp);
            var onA = new float3(0, 1, 0);
            var onB = new float3(1, 0, 0);
            table.Add(Plane(new float3(0, 1, 0), onA), onA, out _);
            table.Add(Plane(new float3(1, 0, 0), onB), onB, out _);
            Assert.That(table.Count, Is.EqualTo(2));

            table.Clear();
            Assert.That(table.Count, Is.EqualTo(0));
            Assert.That(table.TryFind(Plane(new float3(0, 1, 0), onA), onA, out _, out _), Is.False,
                        "a stale hash chain would hand back an id into an empty list");
        }
    }
}
