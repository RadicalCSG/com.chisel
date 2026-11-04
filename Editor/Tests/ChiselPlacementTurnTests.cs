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
    }
}
