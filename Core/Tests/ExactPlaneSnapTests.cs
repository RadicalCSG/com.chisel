using NUnit.Framework;

namespace Chisel.Core.Tests
{
    [TestFixture]
    public class ExactPlaneSnapTests
    {
        static bool Same(ExactPlane p, ExactPlane q) => p.a == q.a && p.b == q.b && p.c == q.c && p.w == q.w;

        // The sample scene's pit: two carves meant to meet at y = -2, their planes fitted to float vertices
        [Test]
        public void PlanesAFloatsNoiseApart_BecomeOnePlane()
        {
            var lower = ExactPlane.Quantize(0, 1,  5.960464e-08, 2 + 5.960464e-08 * 30);
            var upper = ExactPlane.Quantize(0, 1,  8.141123e-08, 2 + 8.141123e-08 * 30);
            Assert.That(Same(lower, upper), Is.True, $"({lower.a}, {lower.b}, {lower.c}, {lower.w}) vs ({upper.a}, {upper.b}, {upper.c}, {upper.w})");
            Assert.That(Same(lower, ExactPlane.Quantize(0, 1, 0, 2)), Is.True, "not the plane y = -2 they were meant to be");
        }

        [Test]
        public void PlanesOneStepApart_StayTwo()
        {
            const double step = 1.0 / ExactPlane.kOffsetScale;
            var p = ExactPlane.Quantize(0, 1, 0, 2);
            var q = ExactPlane.Quantize(0, 1, 0, 2 + step);
            Assert.That(Same(p, q), Is.False, "an offset step merged");
            Assert.That(q.w - p.w, Is.EqualTo((long)(ExactPlane.kNormalScale / ExactPlane.kOffsetScale)));

            var tilted = ExactPlane.Quantize(0, 1, 1.0 / ExactPlane.kNormalScale, 2);
            Assert.That(Same(p, tilted), Is.False, "a normal step merged");
        }

        [Test]
        public void TheOppositePlane_IsExactlyTheNegation()
        {
            var p = ExactPlane.Quantize(0.3, -0.7, 0.2, 17.123456);
            var q = ExactPlane.Quantize(-0.3, 0.7, -0.2, -17.123456);
            Assert.That(Same(p.Flipped(), q), Is.True);
        }

        // The snap moves a plane by at most half a step: its offset by 2^-15, its normal by 2^-21 in each component
        [Test]
        public void TheSnap_MovesAPlaneByAtMostHalfAStep()
        {
            var random = new System.Random(1234);
            for (int i = 0; i < 1000; i++)
            {
                double x = random.NextDouble() * 2 - 1, y = random.NextDouble() * 2 - 1, z = random.NextDouble() * 2 - 1;
                double length = System.Math.Sqrt(x * x + y * y + z * z);
                if (length < 1e-3) continue;
                x /= length; y /= length; z /= length;
                double d = (random.NextDouble() * 2 - 1) * 1000;
                var p = ExactPlane.Quantize(x, y, z, d);
                Assert.That(System.Math.Abs(p.a / ExactPlane.kNormalScale - x), Is.LessThanOrEqualTo(0.5 / ExactPlane.kNormalScale));
                Assert.That(System.Math.Abs(p.b / ExactPlane.kNormalScale - y), Is.LessThanOrEqualTo(0.5 / ExactPlane.kNormalScale));
                Assert.That(System.Math.Abs(p.c / ExactPlane.kNormalScale - z), Is.LessThanOrEqualTo(0.5 / ExactPlane.kNormalScale));
                Assert.That(System.Math.Abs(p.w / ExactPlane.kNormalScale - d), Is.LessThanOrEqualTo(0.5 / ExactPlane.kOffsetScale));
            }
        }
    }
}
