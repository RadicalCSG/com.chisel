using System.Collections.Generic;
using System.Globalization;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;

namespace Chisel.Core.Tests
{
    // An extruded shape goes along every step of its path, a brush for each piece of the shape along each, and its path
    // points keep the rotations they're given
    [TestFixture]
    public class ChiselExtrudedShapePathTests
    {
        static Curve2D Square() => new(new[] { new CurveControlPoint2D(-1, -1), new CurveControlPoint2D(1, -1), new CurveControlPoint2D(1, 1), new CurveControlPoint2D(-1, 1) });

        // two pieces
        static Curve2D L() => new(new[] { new CurveControlPoint2D(0, 0), new CurveControlPoint2D(2, 0), new CurveControlPoint2D(2, 1),
                                          new CurveControlPoint2D(1, 1), new CurveControlPoint2D(1, 2), new CurveControlPoint2D(0, 2) });

        static ChiselExtrudedShapeDefinition Extruded(Curve2D shape, params ChiselPathPoint[] path)
        {
            var definition = new ChiselExtrudedShapeDefinition();
            definition.Reset();
            definition.shape = shape;
            if (path.Length > 0)
                definition.path = new ChiselPath(path);
            return definition;
        }

        // points one apart, straight up, none turned
        static ChiselPathPoint[] Straight(int count)
        {
            var points = new ChiselPathPoint[count];
            for (int i = 0; i < count; i++)
                points[i] = new ChiselPathPoint(new Vector3(0, i, 0));
            return points;
        }

        static void AssertAlongTheSteps(List<(float3[] vertices, float4[] planes)> brushes, int pieces, int steps)
        {
            Assert.That(brushes.Count, Is.EqualTo(pieces * steps), "brushes");
            // a piece's brushes one after another, each from one step to the next
            for (int b = 0; b < brushes.Count; b++)
            {
                var step = b % steps;
                foreach (var vertex in brushes[b].vertices)
                    Assert.That(vertex.y, Is.InRange(step - 1e-4f, step + 1 + 1e-4f), $"brush {b}");
            }
        }

        [Test]
        public void APath_IsABrushAlongEachOfItsSteps([Values(2, 3, 5)] int points)
        {
            AssertAlongTheSteps(ChiselRevolvedShapeBrushTests.Brushes(Extruded(Square(), Straight(points))), 1, points - 1);
        }

        [Test]
        public void APath_IsABrushForEachPieceAlongEachOfItsSteps()
        {
            AssertAlongTheSteps(ChiselRevolvedShapeBrushTests.Brushes(Extruded(L(), Straight(3))), 2, 2);
        }

        // the end turned round the shape's own x: a bend, so its sides stay flat
        static ChiselPathPoint Bent(Vector3 position, float degrees)
        {
            return new ChiselPathPoint(position, Quaternion.AngleAxis(degrees, Vector3.right) * ChiselPathPoint.DefaultRotation, Vector3.one);
        }

        [Test]
        public void APathPointsRotation_TurnsTheShape()
        {
            var end = Bent(new Vector3(0, 2, 0), 30);
            var brushes = ChiselRevolvedShapeBrushTests.Brushes(Extruded(Square(), new ChiselPathPoint(Vector3.zero), end));
            Assert.That(brushes.Count, Is.GreaterThan(1), "turned in steps");

            // the shape's corners where the path ends, as the end point has it
            var matrix = end.ToMatrix();
            foreach (var corner in Square().controlPoints)
            {
                var expected = (float3)matrix.MultiplyPoint(new Vector3(corner.position.x, corner.position.y, 0));
                var found = false;
                foreach (var brush in brushes)
                    foreach (var vertex in brush.vertices)
                        found |= math.distance(vertex, expected) < 1e-4f;
                Assert.That(found, Is.True, $"the corner {corner.position} at the end, at {expected}");
            }
        }

        [Test]
        public void APathPointTurnedInTheSceneView_KeepsItsRotation()
        {
            var definition = Extruded(Square(), new ChiselPathPoint(Vector3.zero), new ChiselPathPoint(new Vector3(0, 2, 0)));
            var turned = Bent(new Vector3(0, 2, 0), 30);
            definition.OnEdit(new FakeChiselHandles { pathPointToMove = 1, pathPointMovedTo = turned });
            // and the next time it's drawn
            definition.OnEdit(new FakeChiselHandles());

            Assert.That(Quaternion.Angle(definition.path.segments[1].rotation, turned.rotation), Is.LessThan(1e-3f));
        }

        [Test]
        public void APathSavedWithoutAVersion_HasItsRotationsSetUp_AndSoDoesACopy()
        {
            var turned = Bent(Vector3.zero, 30).rotation;
            string Number(float value) => value.ToString("R", CultureInfo.InvariantCulture);
            var json = "{\"segments\":[{\"position\":{\"x\":0,\"y\":0,\"z\":0},\"rotation\":{\"x\":" + Number(turned.x) + ",\"y\":" + Number(turned.y) +
                       ",\"z\":" + Number(turned.z) + ",\"w\":" + Number(turned.w) + "},\"scale\":{\"x\":1,\"y\":1}}]}";
            var saved = JsonUtility.FromJson<ChiselPath>(json);
            var copy  = new ChiselPath(saved);

            saved.UpgradeIfNecessary();
            copy.UpgradeIfNecessary();

            Assert.That(Quaternion.Angle(saved.segments[0].rotation, ChiselPathPoint.DefaultRotation), Is.LessThan(1e-3f), "saved");
            Assert.That(Quaternion.Angle(copy.segments[0].rotation,  ChiselPathPoint.DefaultRotation), Is.LessThan(1e-3f), "its copy");
        }
    }
}
