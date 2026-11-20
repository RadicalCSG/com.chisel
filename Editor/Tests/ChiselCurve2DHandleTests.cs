using NUnit.Framework;
using UnityEngine;

namespace Chisel.Editors.Tests
{
    // A 2D shape's points (a revolved shape's, an extruded shape's) are dragged in the shape's own plane, however its
    // generator is turned, so they stay in it and under the mouse; in a plane of the grid, they snap to the grid's lines
    [TestFixture]
    public sealed class ChiselCurve2DHandleTests
    {
        static void AssertDraggedInItsPlane(Matrix4x4 localToWorld)
        {
            var grid   = SceneHandles.Curve2DHandleLogic.SlideGrid(Grid.DefaultGrid, localToWorld);
            var normal = grid.PlaneXZ.normal;
            var shapeX = localToWorld.MultiplyVector(Vector3.right).normalized;
            var shapeY = localToWorld.MultiplyVector(Vector3.up).normalized;
            Assert.That(Vector3.Dot(normal, shapeX), Is.EqualTo(0).Within(1e-5f), "the shape's x is in the plane dragged in");
            Assert.That(Vector3.Dot(normal, shapeY), Is.EqualTo(0).Within(1e-5f), "the shape's y is in the plane dragged in");
        }

        [Test]
        public void AShapeTurnedOffTheGrid_IsDraggedInItsOwnPlane([Values(30f, 45f, 60f)] float degrees)
        {
            AssertDraggedInItsPlane(Matrix4x4.TRS(new Vector3(1, 2, 3), Quaternion.Euler(0, degrees, 0), Vector3.one));
        }

        // placed on a sloped or upright surface: its up isn't the world's
        [Test]
        public void AShapeStandingOnASlope_IsDraggedInItsOwnPlane()
        {
            AssertDraggedInItsPlane(Matrix4x4.TRS(new Vector3(-2, 0, 1), Quaternion.Euler(35, 20, 70), Vector3.one));
        }

        // in a plane of the grid it is dragged in that plane of the grid itself, so it snaps to the grid's lines
        [Test]
        public void AShapeInAPlaneOfTheGrid_IsDraggedInThatPlaneOfTheGrid([Values(0f, 90f, 180f)] float degrees)
        {
            var localToWorld = Matrix4x4.TRS(new Vector3(1, 2, 3), Quaternion.Euler(0, degrees, 0), Vector3.one);
            AssertDraggedInItsPlane(localToWorld);
            var grid     = SceneHandles.Curve2DHandleLogic.SlideGrid(Grid.DefaultGrid, localToWorld);
            var expected = Grid.DefaultGrid.GetClosestAxis(localToWorld.MultiplyVector(Vector3.forward)) == Chisel.Core.Axis.X ? Grid.DefaultGrid.GridYZ : Grid.DefaultGrid.GridXY;
            Assert.That(grid.GridToWorldSpace, Is.EqualTo(expected.GridToWorldSpace), "the grid's own plane");
        }
    }
}
