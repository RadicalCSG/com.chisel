using NUnit.Framework;
using UnityEngine;

namespace Chisel.Editors.Tests
{
    // Stairs drawn wider than deep are turned to run along the long side, and still get a positive size to build
    public sealed class ChiselPlacementTurnTests
    {
        [Test]
        public void ABoxDrawnWiderThanDeep_IsTurnedWithAPositiveSize()
        {
            var transformation = Matrix4x4.identity;
            var bounds = new Bounds(new Vector3(0, 2.5f, 0), new Vector3(12, 5, 10));
            ChiselPlacementTurn.AlongLongSide(ref transformation, ref bounds);

            Assert.AreEqual(10, bounds.size.x, 1e-4f);
            Assert.AreEqual(5,  bounds.size.y, 1e-4f);
            Assert.AreEqual(12, bounds.size.z, 1e-4f);
            Assert.AreEqual(1.0f, Vector3.Dot(transformation.GetColumn(1), Vector3.up), 1e-5f);
        }

        [Test]
        public void ABoxDrawnDeeperThanWide_IsLeftAsIs()
        {
            var transformation = Matrix4x4.identity;
            var bounds = new Bounds(new Vector3(0, 3, 0), new Vector3(9, 6, 24));
            ChiselPlacementTurn.AlongLongSide(ref transformation, ref bounds);

            Assert.AreEqual(new Vector3(9, 6, 24), bounds.size);
            Assert.AreEqual(Matrix4x4.identity, transformation);
        }

        // Drawn on a floor, the grid on it is the grid itself, not turned round its up: what is drawn there faces the way
        // it does on the grid
        [Test]
        public void TheGridOnAFloor_IsNotTurned()
        {
            var turn = ChiselPlacementTurn.SurfaceTurn(Vector3.up, Vector3.up);
            Assert.AreEqual(0, Quaternion.Angle(turn, Quaternion.identity), 1e-3f);
        }

        // Stairs drawn out of a wall stand up, turned (not mirrored), and fill the box that was drawn: from the wall out as
        // far as drawn, around where it was drawn along the wall
        [TestCase(1, 0, 0)]
        [TestCase(-1, 0, 0)]
        [TestCase(0, 0, 1)]
        [TestCase(0, 0, -1)]
        public void StairsDrawnOutOfAWall_StandUpInWhatWasDrawn(float x, float y, float z)
        {
            var normal = new Vector3(x, y, z);
            var at     = new Vector3(5, 2, -3);
            var transformation = Matrix4x4.TRS(at, ChiselPlacementTurn.SurfaceTurn(Vector3.up, normal), Vector3.one);
            // a square of 2 drawn from its middle on the wall, drawn out 3 from it
            var bounds    = new Bounds(new Vector3(0, 1.5f, 0), new Vector3(2, 3, 2));
            var extrusion = Vector3.up;
            ChiselPlacementTurn.FaceUp(ref transformation, ref bounds, ref extrusion, Vector3.up);
            ChiselPlacementTurn.FromBottomMiddle(ref transformation, ref bounds, extrusion, 3);

            Assert.AreEqual(1, Vector3.Dot(transformation.GetColumn(1), Vector3.up), 1e-5f, "it doesn't stand up");
            Assert.That(transformation.determinant, Is.GreaterThan(0), "it is mirrored");
            Assert.AreEqual(2, bounds.size.y, 1e-5f, "as high as drawn along the wall");

            // the box it is in, in the world, is what was drawn
            var min = Vector3.positiveInfinity;
            var max = Vector3.negativeInfinity;
            for (int c = 0; c < 8; c++)
            {
                var corner = bounds.center + Vector3.Scale(bounds.extents, new Vector3((c & 1) == 0 ? -1 : 1, (c & 2) == 0 ? -1 : 1, (c & 4) == 0 ? -1 : 1));
                var world  = transformation.MultiplyPoint(corner);
                min = Vector3.Min(min, world);
                max = Vector3.Max(max, world);
            }
            var drawnMin = at + Vector3.Min(Vector3.zero, normal * 3) - (Vector3.one - Abs(normal) - Vector3.up) - Vector3.up;
            var drawnMax = at + Vector3.Max(Vector3.zero, normal * 3) + (Vector3.one - Abs(normal) - Vector3.up) + Vector3.up;
            Assert.AreEqual(drawnMin.x, min.x, 1e-4f); Assert.AreEqual(drawnMin.y, min.y, 1e-4f); Assert.AreEqual(drawnMin.z, min.z, 1e-4f);
            Assert.AreEqual(drawnMax.x, max.x, 1e-4f); Assert.AreEqual(drawnMax.y, max.y, 1e-4f); Assert.AreEqual(drawnMax.z, max.z, 1e-4f);
        }

        static Vector3 Abs(Vector3 v) => new Vector3(Mathf.Abs(v.x), Mathf.Abs(v.y), Mathf.Abs(v.z));

        // A cylinder without a size (spiral stairs without an inner one) still has its horizon lines, on its axis, with a
        // way across the view to drag them out: they can be dragged from nothing, and a drag passing through it keeps them
        [TestCase(false)]
        [TestCase(true)]
        public void ACircleWithoutASize_HasHorizonLinesOnItsAxis(bool orthographic)
        {
            var center = new Vector3(1, 0, 2);
            Assert.That(Chisel.Core.IChiselHandleRendererExtensions.HorizonPoints(center, 0, new Vector3(0, 3, 5), orthographic, out var left, out var right, out var side), Is.True);
            Assert.AreEqual(center, left);
            Assert.AreEqual(center, right);
            Assert.AreEqual(1, side.magnitude, 1e-5f, "no way to drag them out");
            Assert.AreEqual(0, Vector3.Dot(side, new Vector3(0, 0, 1)), 1e-5f, "not across the view");
        }
    }
}
