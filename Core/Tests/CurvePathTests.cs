using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace Chisel.Core.Tests
{
    public class CurvePathTests
    {
        static int PartsOf(Curve2D curve, float maxTurn)
        {
            using var blob = ChiselCurve2DBlob.Convert(curve, Allocator.Temp);
            var vertices = blob.Value.GetPathVerticesByTurn(maxTurn, Allocator.Temp);
            var count = vertices.Length;
            vertices.Dispose();
            return count;
        }

        // A curve is cut by how much it turns, not into a fixed number of parts
        [Test]
        public void ACurve_IsCutByHowMuchItTurns()
        {
            // close to a quarter circle: 90 degrees in parts of at most 15 is 6 parts, so 7 points
            const float k = 0.5523f;
            var start = new CurveControlPoint2D(0, -2) { tangent2 = new Vector2(-2 * k, 0), constraint2 = ControlPointConstraint.Mirrored };
            var end   = new CurveControlPoint2D(2,  0) { tangent1 = new Vector2(0, 2 * k), constraint1 = ControlPointConstraint.Mirrored };
            var quarter = PartsOf(new Curve2D(false, start, end), math.PI / 12);
            Assert.That(quarter, Is.EqualTo(6 + 1), "a quarter circle in 15 degree parts");

            // a sharper limit cuts the same curve into more parts
            Assert.That(PartsOf(new Curve2D(false, start, end), math.PI / 24), Is.EqualTo(12 + 1));

            // a straight line isn't cut at all
            var line = new Curve2D(false, new CurveControlPoint2D(0, 0), new CurveControlPoint2D(5, 0));
            Assert.That(PartsOf(line, math.PI / 12), Is.EqualTo(2));
        }

        // Each part turns as much as the others
        [Test]
        public void ACurve_IsCutIntoPartsThatTurnTheSame()
        {
            const float k = 0.5523f;
            var start = new CurveControlPoint2D(0, -2) { tangent2 = new Vector2(-2 * k, 0), constraint2 = ControlPointConstraint.Mirrored };
            var end   = new CurveControlPoint2D(2,  0) { tangent1 = new Vector2(0, 2 * k), constraint1 = ControlPointConstraint.Mirrored };
            using var blob = ChiselCurve2DBlob.Convert(new Curve2D(false, start, end), Allocator.Temp);
            var vertices = blob.Value.GetPathVerticesByTurn(math.PI / 12, Allocator.Temp);
            try
            {
                // on a circle around (0, 0) the points are evenly spread around it
                for (int i = 1; i < vertices.Length - 1; i++)
                {
                    var a = vertices[i - 1].position;
                    var b = vertices[i].position;
                    var c = vertices[i + 1].position;
                    Assert.That(math.distance(a, b), Is.EqualTo(math.distance(b, c)).Within(0.05f), $"point {i} isn't halfway round");
                }
            }
            finally { vertices.Dispose(); }
        }
    }
}
