using System;
using NUnit.Framework;
using Unity.Mathematics;
using static Chisel.Core.Tests.ChiselTestUtility;

namespace Chisel.Core.Tests
{
    // Pins the behavior of the centralized CSG math (CSGMath): these assert the exact geometric results
    // of the operations the library provides, on well-separated cases that don't exercise rounding.
    [TestFixture]
    public class CSGMathTests
    {
        [Test]
        public void SignedDistance_IsDotPlanePoint()
        {
            var plane = new float4(1, 0, 0, -1); // x = 1
            Assert.That(CSGMath.SignedDistance(plane, new float3(3, 0, 0)), Is.EqualTo(2f).Within(1e-5f));
            Assert.That(CSGMath.SignedDistance(plane, new float3(1, 5, 9)), Is.EqualTo(0f).Within(1e-5f));
            Assert.That(CSGMath.SignedDistance(plane, new float3(0, 0, 0)), Is.EqualTo(-1f).Within(1e-5f));
            // float4 overload agrees
            Assert.That(CSGMath.SignedDistance(plane, new float4(3, 0, 0, 1)), Is.EqualTo(2f).Within(1e-5f));
        }

        [Test]
        public void SqrDistance_And_VerticesEqual()
        {
            Assert.That(CSGMath.SqrDistance(new float3(0, 0, 0), new float3(3, 4, 0)), Is.EqualTo(25f).Within(1e-4f));
            // within the weld tolerance (kVertexEqualEpsilon ~0.0125): nearly coincident -> equal
            Assert.That(CSGMath.VerticesEqual(new float3(0, 0, 0), new float3(0.001f, 0, 0)), Is.True);
            // clearly separated -> not equal
            Assert.That(CSGMath.VerticesEqual(new float3(0, 0, 0), new float3(1, 0, 0)), Is.False);
        }

        [Test]
        public void EdgePlaneCrossing_HitsThePlane()
        {
            // segment (0,0,0)[d=+1] -> (0,0,2)[d=-1] crosses at the midpoint (0,0,1).
            var p = CSGMath.EdgePlaneCrossing(new float3(0, 0, 0), new float3(0, 0, 2), 1f, -1f);
            AssertApproximately(new float3(0, 0, 1), p, 1e-4f);
            // direction-independent: same segment given in reverse with swapped distances -> same point.
            var q = CSGMath.EdgePlaneCrossing(new float3(0, 0, 2), new float3(0, 0, 0), -1f, 1f);
            AssertApproximately(new float3(0, 0, 1), q, 1e-4f);
        }

        [Test]
        public void EdgePlaneCrossing_OffCenter()
        {
            // d0=3, d1=-1 -> delta=3/4, crossing at v0 - (v0-v1)*0.75 = (0,0,0)-(0,0,-4)*0.75 = (0,0,3)
            var p = CSGMath.EdgePlaneCrossing(new float3(0, 0, 0), new float3(0, 0, 4), 3f, -1f);
            AssertApproximately(new float3(0, 0, 3), p, 1e-4f);
        }

        [Test]
        public void PlaneIntersection_ThreeAxisPlanes()
        {
            var x1 = new double4(1, 0, 0, -1); // x = 1
            var y2 = new double4(0, 1, 0, -2); // y = 2
            var z3 = new double4(0, 0, 1, -3); // z = 3
            var p = CSGMath.PlaneIntersection(x1, y2, z3);
            AssertApproximately(new double3(1, 2, 3), p, 1e-3);
        }

        [Test]
        public void Orient2D_SignsAreConsistent()
        {
            var a = new double2(0, 0);
            var b = new double2(1, 0);
            var c = new double2(0, 1);
            var ccw = CSGMath.Orient2D(a, b, c);
            var cw  = CSGMath.Orient2D(a, c, b); // reversed winding
            Assert.That(ccw, Is.Not.EqualTo(0.0));
            Assert.That(math.sign(ccw), Is.EqualTo(-math.sign(cw))); // opposite signs
            // collinear -> zero
            Assert.That(CSGMath.Orient2D(new double2(0, 0), new double2(1, 1), new double2(2, 2)), Is.EqualTo(0.0).Within(1e-12));
        }

        // ---- error-free transformations ----

        static System.Numerics.BigInteger Exact(double value)
        {
            long bits   = BitConverter.DoubleToInt64Bits(value);
            int  rawExp = (int)((bits >> 52) & 0x7FF);
            long man    = bits & 0xFFFFFFFFFFFFFL;
            if (rawExp == 0) rawExp = 1;          // subnormal: no implicit leading bit
            else             man |= 1L << 52;     // normal: restore the implicit leading bit
            var scaled = new System.Numerics.BigInteger(man) << rawExp;   // == value * 2^1075
            return bits < 0 ? -scaled : scaled;
        }

        [Test]
        public void Exact_RoundTripsDoublesThatDecimalWouldRound()
        {
            // guards the helper above, and pins the reason decimal is unfit for this job
            Assert.That(Exact(1.0) + Exact(1.0), Is.EqualTo(Exact(2.0)));
            Assert.That(Exact(-1e8) + Exact(1e8), Is.EqualTo(System.Numerics.BigInteger.Zero));
            Assert.That(Exact(0.0), Is.EqualTo(System.Numerics.BigInteger.Zero));

            // The case that broke the old tests. The double sum genuinely loses information - that
            // is precisely what err carries - so a correct reference must see s + err restore it.
            double a = -1e8, b = 7.000001;
            CSGMath.TwoSum(a, b, out var s, out var err);
            Assert.That(Exact(s), Is.Not.EqualTo(Exact(a) + Exact(b)),
                        "the double sum alone cannot represent a + b here");
            Assert.That(Exact(s) + Exact(err), Is.EqualTo(Exact(a) + Exact(b)),
                        "but s + err does, exactly - which is the whole point of the transformation");

            // decimal cannot show that: at this magnitude it rounds to 1e-7, coarser than err
            // (~1.6e-9), so the old formulation reports a mismatch against correct code.
            Assert.That((decimal)s + (decimal)err, Is.Not.EqualTo((decimal)a + (decimal)b),
                        "this is the old assertion; it fails even though TwoSum is exact");
        }

        [Test]
        public void TwoSum_IsExact()
        {
            // a + b == s + err EXACTLY, even when b is far below a's ulp.
            foreach (var (a, b) in new[] { (1e16, 1.0), (1.0, 1e-16), (3.0, 1e0), (-1e8, 7.000001) })
            {
                CSGMath.TwoSum(a, b, out var s, out var err);
                Assert.That(Exact(s) + Exact(err), Is.EqualTo(Exact(a) + Exact(b)),
                            $"TwoSum({a},{b}) not exact");
            }
        }

        [Test]
        public void TwoProduct_IsExact()
        {
            // a * b == p + err EXACTLY. Both sides are scaled by 2^2150 so the product's scale matches.
            foreach (var (a, b) in new[] { (100000001.0, 100000001.0), (1.0000000001, 1.0000000001), (12345.6789, -98765.4321) })
            {
                CSGMath.TwoProduct(a, b, out var p, out var err);
                Assert.That((Exact(p) + Exact(err)) << 1075, Is.EqualTo(Exact(a) * Exact(b)),
                            $"TwoProduct({a},{b}) not exact");
            }
        }

        [Test]
        public void Orient2D_CompensatedBeatsNaive_OnCatastrophicCancellation()
        {
            var p = new double2(0, 0);
            var q = new double2(1e8, 1e8 + 1);
            var r = new double2(2e8 + 1, 2e8 + 3);

            double a = q.y - p.y, b = r.x - q.x, c = q.x - p.x, d = r.y - q.y;
            double naive = a * b - c * d;
            Assert.That(naive, Is.EqualTo(0.0), "expected naive double to lose the cancellation");

            // compensated orient2d recovers the true sign (+).
            Assert.That(CSGMath.Orient2D(p, q, r), Is.GreaterThan(0.0));
        }

        [Test]
        public void Orient2D_MatchesExactReference_OnRandomishCancellation()
        {
            // For a battery of near-collinear triples, the compensated orient2d sign must equal the exact
            // (decimal) sign of a*b - c*d computed from the same double differences.
            var cases = new[]
            {
                (new double2(0, 0), new double2(1e8, 1e8 + 1), new double2(2e8 + 1, 2e8 + 3)),
                (new double2(0.5, 0.5), new double2(1e7 + 0.5, 1e7 + 0.5), new double2(2e7 + 0.5, 2e7 + 0.5)),
                (new double2(-1e6, 3), new double2(1e6 + 1, 5), new double2(3e6 + 2, 7)),
            };
            foreach (var (p, q, r) in cases)
            {
                decimal a = (decimal)(q.y - p.y), b = (decimal)(r.x - q.x), c = (decimal)(q.x - p.x), d = (decimal)(r.y - q.y);
                int refSign = Math.Sign(a * b - c * d);
                Assert.That(Math.Sign(CSGMath.Orient2D(p, q, r)), Is.EqualTo(refSign));
            }
        }

        [Test]
        public void NewellTerm_AccumulatesUnitSquareNormal()
        {
            // CCW unit square in the XY plane -> normal should point +Z.
            var v = new[] { new float3(0, 0, 0), new float3(1, 0, 0), new float3(1, 1, 0), new float3(0, 1, 0) };
            var n = float3.zero;
            for (int i = 0; i < v.Length; i++)
                n += CSGMath.NewellTerm(v[i], v[(i + 1) % v.Length]);
            n = math.normalize(n);
            AssertApproximately(new float3(0, 0, 1), n, 1e-4f);
        }
    }
}
