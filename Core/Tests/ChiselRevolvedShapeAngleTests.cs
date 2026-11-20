using System.Collections.Generic;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;

namespace Chisel.Core.Tests
{
    // A revolved shape goes round from its start angle as far as its total angle: its brushes, its start and end
    // surfaces and its shape's handle are where it is
    [TestFixture]
    public class ChiselRevolvedShapeAngleTests
    {
        const float kDegreesEpsilon = 0.01f;

        static ChiselRevolvedShapeDefinition Revolved(float startAngle, float totalAngle)
        {
            return ChiselRevolvedShapeBrushTests.Revolved(ChiselRevolvedShapeBrushTests.Square(1, 2), startAngle, totalAngle);
        }

        // how far round the up axis each vertex of its brushes is, in degrees, as a turn round up takes x
        static List<float> AnglesOf(ChiselRevolvedShapeDefinition definition)
        {
            var angles = new List<float>();
            foreach (var (vertices, _) in ChiselRevolvedShapeBrushTests.Brushes(definition))
                foreach (var vertex in vertices)
                    angles.Add(math.degrees(math.atan2(-vertex.z, vertex.x)));
            return angles;
        }

        [Test]
        public void ItsBrushes_GoFromItsStartAngle_AsFarAsItsTotalAngle([Values(0f, 30f)] float startAngle, [Values(90f, 200f)] float totalAngle)
        {
            var angles = AnglesOf(Revolved(startAngle, totalAngle));
            Assert.That(angles, Is.Not.Empty);
            var from = float.PositiveInfinity;
            var to   = float.NegativeInfinity;
            foreach (var angle in angles)
            {
                // measured from the start, so a span past 180 degrees isn't cut in two
                var along = Mathf.Repeat(angle - startAngle + kDegreesEpsilon, 360) - kDegreesEpsilon;
                from = math.min(from, along);
                to   = math.max(to,   along);
            }
            Assert.That(from, Is.EqualTo(0).Within(kDegreesEpsilon), "it starts at its start angle");
            Assert.That(to,   Is.EqualTo(totalAngle).Within(kDegreesEpsilon), "it ends its total angle further round");
        }

        // all the way round it has no start or end; part of the way, they close it
        [Test]
        public void ItsStartAndEnd_AreUsed_WhenItDoesntGoAllTheWayRound()
        {
            var part = Revolved(30, 90);
            Assert.That(part.IsSurfaceUsed(0), Is.True, "start");
            Assert.That(part.IsSurfaceUsed(1), Is.True, "end");
            var whole = Revolved(30, 360);
            Assert.That(whole.IsSurfaceUsed(0), Is.False, "no start");
            Assert.That(whole.IsSurfaceUsed(1), Is.False, "no end");
        }

        // its shape is edited where it starts, where its brushes begin
        [Test]
        public void ItsShapesHandle_IsAtItsStartAngle()
        {
            var definition = Revolved(30, 90);
            var handles = new FakeChiselHandles();
            definition.OnEdit(handles);
            Assert.That(handles.shapeHandleTransformation.HasValue, Is.True, "a shape handle");
            var x = handles.shapeHandleTransformation.Value.MultiplyVector(Vector3.right);
            Assert.That(math.degrees(math.atan2(-x.z, x.x)), Is.EqualTo(30).Within(kDegreesEpsilon));
        }
    }
}
