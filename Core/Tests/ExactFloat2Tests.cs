using System;
using NUnit.Framework;
using Unity.Mathematics;
using BigInteger = System.Numerics.BigInteger;
using Random = System.Random;

namespace Chisel.Core.Tests
{
    // Whole-number arithmetic over float coordinates, to check exact predicates against: every finite float is a whole
    // number times 2^-149
    static class FloatReference
    {
        public static BigInteger Whole(float value)
        {
            var bits     = BitConverter.SingleToInt32Bits(value);
            var field    = (bits >> 23) & 0xFF;
            long mantissa = field == 0 ? (bits & 0x7FFFFF) : ((bits & 0x7FFFFF) | 0x800000);
            var exponent = field == 0 ? -149 : field - 150;
            var whole    = new BigInteger(mantissa) << (exponent + 149);
            return bits < 0 ? -whole : whole;
        }

        // (b - a) x (d - c), in units of 2^-298
        public static BigInteger Cross(float2 a, float2 b, float2 c, float2 d)
        {
            return (Whole(b.x) - Whole(a.x)) * (Whole(d.y) - Whole(c.y)) - (Whole(b.y) - Whole(a.y)) * (Whole(d.x) - Whole(c.x));
        }

        // (b - a) . (d - c), in units of 2^-298
        public static BigInteger Dot(float2 a, float2 b, float2 c, float2 d)
        {
            return (Whole(b.x) - Whole(a.x)) * (Whole(d.x) - Whole(c.x)) + (Whole(b.y) - Whole(a.y)) * (Whole(d.y) - Whole(c.y));
        }

        public static int Orientation(float2 a, float2 b, float2 c) => Cross(a, b, a, c).Sign;

        // The float `steps` units in the last place further from zero (closer when negative); not across zero
        public static float Nudged(float value, int steps)
        {
            var bits = BitConverter.SingleToInt32Bits(value);
            return BitConverter.Int32BitsToSingle(bits + steps);
        }
    }

    [TestFixture]
    public class ExactFloat2Tests
    {
        [Test]
        public void Orientation_IsLeftRightOrOn()
        {
            Assert.That(ExactFloat2.Orientation(new float2(0, 0), new float2(1, 0), new float2(0, 1)), Is.EqualTo(1), "left");
            Assert.That(ExactFloat2.Orientation(new float2(0, 0), new float2(1, 0), new float2(0, -1)), Is.EqualTo(-1), "right");
            Assert.That(ExactFloat2.Orientation(new float2(0, 0), new float2(1, 0), new float2(7, 0)), Is.EqualTo(0), "on");
            Assert.That(ExactFloat2.DotSign(new float2(0, 0), new float2(1, 0), new float2(0, 0), new float2(-1, 5)), Is.EqualTo(-1));
            Assert.That(ExactFloat2.DotSign(new float2(0, 0), new float2(1, 0), new float2(3, 3), new float2(3, 9)), Is.EqualTo(0));
        }

        // Points on a line through two others as floats round them, then moved a few units in the last place: the
        // signs a double gets wrong here are the ones that matter
        [Test]
        public void PointsNearlyOnALine_AreDecidedExactly([Values(1, 2, 3)] int seed)
        {
            var random = new Random(seed);
            for (int n = 0; n < 1500; n++)
            {
                var scale = (float)Math.Pow(10, random.Next(-4, 6));
                var a = new float2((float)random.NextDouble() * scale + scale, (float)random.NextDouble() * scale + scale);
                var b = new float2((float)random.NextDouble() * scale + scale, (float)random.NextDouble() * scale + scale);
                var t = (float)random.NextDouble();
                var c = a + (b - a) * t;
                c = new float2(FloatReference.Nudged(c.x, random.Next(-2, 3)), FloatReference.Nudged(c.y, random.Next(-2, 3)));
                var d = new float2(FloatReference.Nudged(b.x, random.Next(-1, 2)), FloatReference.Nudged(b.y, random.Next(-1, 2)));
                Assert.That(ExactFloat2.Orientation(a, b, c), Is.EqualTo(FloatReference.Orientation(a, b, c)), $"orientation of {a} {b} {c}");
                Assert.That(ExactFloat2.CrossSign(a, b, c, d), Is.EqualTo(FloatReference.Cross(a, b, c, d).Sign), $"cross of {a} {b} {c} {d}");
                // perpendicular to the line, nearly
                var p = c + new float2(-(b - a).y, (b - a).x);
                p = new float2(FloatReference.Nudged(p.x, random.Next(-2, 3)), FloatReference.Nudged(p.y, random.Next(-2, 3)));
                Assert.That(ExactFloat2.DotSign(a, b, c, p), Is.EqualTo(FloatReference.Dot(a, b, c, p).Sign), $"dot of {a} {b} {c} {p}");
            }
        }

        // Coordinates from the smallest float to the largest that still leaves room: a fixed-size integer can't hold
        // them all, the exact arithmetic has to
        [Test]
        public void FarApartMagnitudes_AreDecidedExactly([Values(1, 2, 3)] int seed)
        {
            var random = new Random(seed);
            var magnitudes = new[] { 0f, 1e-45f, 3e-42f, 1e-38f, 1e-20f, 1e-7f, 1f, 3e5f, 1e15f, 1e30f, 1e36f };
            float Pick()
            {
                var value = magnitudes[random.Next(magnitudes.Length)] * (float)(random.NextDouble() + 0.5);
                return random.Next(2) == 0 ? -value : value;
            }
            for (int n = 0; n < 1500; n++)
            {
                var a = new float2(Pick(), Pick());
                var b = new float2(Pick(), Pick());
                var c = new float2(Pick(), Pick());
                var d = new float2(Pick(), Pick());
                Assert.That(ExactFloat2.CrossSign(a, b, c, d), Is.EqualTo(FloatReference.Cross(a, b, c, d).Sign), $"cross of {a} {b} {c} {d}");
                Assert.That(ExactFloat2.DotSign(a, b, c, d), Is.EqualTo(FloatReference.Dot(a, b, c, d).Sign), $"dot of {a} {b} {c} {d}");
                Assert.That(ExactFloat2.Orientation(a, b, c), Is.EqualTo(FloatReference.Orientation(a, b, c)), $"orientation of {a} {b} {c}");
            }
        }

        // Points on grids of whole numbers and short fractions, where double works the expressions out without rounding
        // and many are zero
        [Test]
        public void PointsOnAGrid_AreDecidedExactly([Values(1f, 0.25f, 1024f, 1e-20f)] float spacing)
        {
            var random = new Random(11);
            float Pick() => (random.Next(9) - 4) * spacing;
            for (int n = 0; n < 1500; n++)
            {
                var a = new float2(Pick(), Pick());
                var b = new float2(Pick(), Pick());
                var c = new float2(Pick(), Pick());
                var d = new float2(Pick(), Pick());
                Assert.That(ExactFloat2.CrossSign(a, b, c, d), Is.EqualTo(FloatReference.Cross(a, b, c, d).Sign), $"cross of {a} {b} {c} {d}");
                Assert.That(ExactFloat2.DotSign(a, b, c, d), Is.EqualTo(FloatReference.Dot(a, b, c, d).Sign), $"dot of {a} {b} {c} {d}");
            }
        }

        // Large whole numbers whose products cancel to within the double filter's bound, all but one: (s - 1)(s + 1) - s s
        // is -1. Where double holds the products exactly the sign comes from them, else from the exact arithmetic
        [Test]
        public void LargeWholeNumbersNearlyCancelling_AreDecidedExactly([Values(1 << 25, 3 << 24, 5 << 23, 1 << 26, 1 << 30)] int whole)
        {
            float s = whole;
            var cross = (a: new float2(1, 0), b: new float2(s, s), c: new float2(0, -1), d: new float2(s, s));
            Assert.That(ExactFloat2.CrossSign(cross.a, cross.b, cross.c, cross.d), Is.EqualTo(-1));
            Assert.That(ExactFloat2.CrossSign(cross.c, cross.d, cross.a, cross.b), Is.EqualTo(1));
            var dot = (a: new float2(1, 0), b: new float2(s, s), c: new float2(-1, s), d: new float2(s, 0));
            Assert.That(ExactFloat2.DotSign(dot.a, dot.b, dot.c, dot.d), Is.EqualTo(-1));
            Assert.That(FloatReference.Cross(cross.a, cross.b, cross.c, cross.d).Sign, Is.EqualTo(-1));
            Assert.That(FloatReference.Dot(dot.a, dot.b, dot.c, dot.d).Sign, Is.EqualTo(-1));
        }

        // Three of the four points the same: the expression is zero, or the sign of what is left
        [Test]
        public void RepeatedPoints_GiveZero()
        {
            var a = new float2(0.1f, 0.7f);
            var b = new float2(0.3f, 1e-30f);
            Assert.That(ExactFloat2.Orientation(a, a, b), Is.EqualTo(0));
            Assert.That(ExactFloat2.Orientation(a, b, a), Is.EqualTo(0));
            Assert.That(ExactFloat2.DotSign(a, a, a, b), Is.EqualTo(0));
            Assert.That(ExactFloat2.DotSign(a, b, a, b), Is.EqualTo(1));
            Assert.That(ExactFloat2.DotSign(a, b, b, a), Is.EqualTo(-1));
        }
    }
}
