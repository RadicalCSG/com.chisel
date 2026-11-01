using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;

namespace Chisel.Core.Tests
{
    [TestFixture]
    public class ChiselDecalOutlineTests
    {
        static readonly float2[] kLShape =
        {
            new float2(-0.8f, -0.4f), new float2(0.8f, -0.4f), new float2(0.8f, 0), new float2(0, 0), new float2(0, 0.4f), new float2(-0.8f, 0.4f)
        };
        const double kLShapeArea = 0.96;

        static NativeArray<float2> Outline(bool clockwise)
        {
            var outline = new NativeArray<float2>(kLShape, Allocator.Temp);
            if (clockwise)
            {
                for (int i = 0; i < outline.Length / 2; i++)
                    (outline[i], outline[outline.Length - 1 - i]) = (outline[outline.Length - 1 - i], outline[i]);
            }
            return outline;
        }

        static double Cross(float2 a, float2 b, float2 c)
        {
            return ((double)b.x - a.x) * ((double)c.y - a.y) - ((double)b.y - a.y) * ((double)c.x - a.x);
        }

        [Test]
        public void Triangulate_CoversAConcaveOutline_EitherWayRound([Values(false, true)] bool clockwise)
        {
            using var outline   = Outline(clockwise);
            using var triangles = new NativeList<int>(Allocator.Temp);

            Assert.That(ChiselDecalOutline.Triangulate(outline, triangles), Is.True);
            Assert.That(triangles.Length, Is.EqualTo((outline.Length - 2) * 3));
            var area = 0.0;
            for (int t = 0; t < triangles.Length; t += 3)
            {
                var twice = Cross(outline[triangles[t]], outline[triangles[t + 1]], outline[triangles[t + 2]]);
                Assert.That(twice, Is.GreaterThan(0), "each triangle is counter-clockwise");
                area += twice * 0.5;
            }
            Assert.That(area, Is.EqualTo(kLShapeArea).Within(1e-5));
        }

        [Test]
        public void Triangulate_RefusesAnOutlineThatCrossesItself()
        {
            using var bowTie    = new NativeArray<float2>(new[] { new float2(0, 0), new float2(1, 1), new float2(1, 0), new float2(0, 1) }, Allocator.Temp);
            using var triangles = new NativeList<int>(Allocator.Temp);
            Assert.That(ChiselDecalOutline.Triangulate(bowTie, triangles), Is.False);
        }

        // An L is two rectangles: its triangles join into convex parts of four corners
        [Test]
        public void ConvexParts_AreConvex_AndCoverTheOutline([Values(false, true)] bool clockwise)
        {
            using var outline = Outline(clockwise);
            using var parts   = new NativeList<int4>(Allocator.Temp);

            Assert.That(ChiselDecalOutline.ConvexParts(outline, parts), Is.True);
            Assert.That(parts.Length, Is.LessThan(outline.Length - 2), "fewer parts than triangles");
            var area = 0.0;
            foreach (var part in parts)
            {
                var count = part.w < 0 ? 3 : 4;
                for (int c = 0; c < count; c++)
                {
                    Assert.That(Cross(outline[part[c]], outline[part[(c + 1) % count]], outline[part[(c + 2) % count]]), Is.GreaterThan(0),
                                "each part turns left at every corner");
                }
                for (int c = 1; c + 1 < count; c++)
                    area += Cross(outline[part[0]], outline[part[c]], outline[part[c + 1]]) * 0.5;
            }
            Assert.That(area, Is.EqualTo(kLShapeArea).Within(1e-5));
        }
    }
}
