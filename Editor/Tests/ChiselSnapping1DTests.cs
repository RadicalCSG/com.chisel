using Chisel.Core;
using NUnit.Framework;
using UnityEngine;

namespace Chisel.Editors.Tests
{
    [TestFixture]
    public class ChiselSnapping1DTests
    {
        // A side handle of a brush scaled along x: the extents it snaps are world distances along the line
        [Test]
        public void TheExtentsOfAScaledHandle_AreWorldDistances()
        {
            var localToWorld = Matrix4x4.Scale(new Vector3(2, 1, 1));
            var localPoint   = new Vector3(0.5f, 0, 0);
            var worldPoint   = localToWorld.MultiplyPoint(localPoint);

            var snapping = new Snapping1D();
            snapping.Initialize(Vector2.zero, worldPoint, localToWorld.MultiplyVector(Vector3.right), 1, Axis.X);
            snapping.CalculateExtents(localToWorld, new[] { localPoint });

            var origin   = Grid.ActiveGrid.Center;
            var expected = worldPoint.x - origin.x;
            Assert.That(snapping.WorldSnappedExtents.min, Is.EqualTo(expected).Within(1e-5f));
            Assert.That(snapping.WorldSnappedExtents.max, Is.EqualTo(expected).Within(1e-5f));
        }
    }
}
