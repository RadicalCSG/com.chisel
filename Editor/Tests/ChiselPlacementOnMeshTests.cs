using NUnit.Framework;
using UnityEngine;

namespace Chisel.Editors.Tests
{
    // Placing over a mesh that isn't Chisel's goes on that mesh. Chisel's picking finds only its own brushes, so the
    // mesh was never found and the grid under it was used
    public sealed class ChiselPlacementOnMeshTests
    {
        GameObject m_Quad;

        [SetUp]
        public void SetUp()
        {
            m_Quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            m_Quad.transform.SetPositionAndRotation(new Vector3(0, -9.62f, 0), Quaternion.Euler(90, 0, 0));   // facing up
            m_Quad.transform.localScale = new Vector3(10000, 10000, 1);
        }

        [TearDown]
        public void TearDown() { Object.DestroyImmediate(m_Quad); }

        [Test]
        public void PlacingOverAMesh_IsOnTheMesh()
        {
            var ray   = new Ray(new Vector3(1, 10, 2), new Vector3(0.2f, -1, 0.1f).normalized);
            var found = ChiselClickSelectionManager.GetPlaneIntersection(m_Quad, ray);
            Assert.That(found, Is.Not.Null, "nothing found");
            Assert.That(found.point.y, Is.EqualTo(-9.62f).Within(1e-2f), $"on {found.point}, not on the quad");
            Assert.That(Vector3.Dot(found.plane.normal, Vector3.up), Is.GreaterThan(0.999f), $"facing {found.plane.normal}");
        }

        // Chisel's geometry is never what Unity's picking finds, so it is the nearer of the two: on CSG in front of a
        // mesh it goes on the CSG, on a mesh in front of CSG on the mesh
        [Test]
        public void PlacingOverCsgAndAMesh_IsOnTheNearer()
        {
            var ray     = new Ray(new Vector3(0, 10, 0), Vector3.down);
            var onCsg   = new PlaneIntersection(new Vector3(0, 2, 0), Vector3.up);
            var onMesh  = new PlaneIntersection(new Vector3(0, -9.62f, 0), Vector3.up);
            Assert.That(ChiselClickSelectionManager.Nearest(ray, onCsg, onMesh), Is.SameAs(onCsg),  "CSG in front");
            Assert.That(ChiselClickSelectionManager.Nearest(ray, onMesh, onCsg), Is.SameAs(onCsg),  "CSG in front, the other way round");
            var csgBehind = new PlaneIntersection(new Vector3(0, -20, 0), Vector3.up);
            Assert.That(ChiselClickSelectionManager.Nearest(ray, csgBehind, onMesh), Is.SameAs(onMesh), "mesh in front");
            Assert.That(ChiselClickSelectionManager.Nearest(ray, onCsg, null), Is.SameAs(onCsg), "only CSG");
        }

        // with nothing under the mouse it is the grid
        [Test]
        public void PlacingOverNothing_IsOnTheGrid()
        {
            var ray   = new Ray(new Vector3(1, 10, 2), Vector3.down);
            var found = ChiselClickSelectionManager.GetPlaneIntersection(null, ray);
            Assert.That(found, Is.Not.Null, "nothing found");
            Assert.That(found.plane.GetDistanceToPoint(found.point), Is.EqualTo(0).Within(1e-4f));
            Assert.That(Grid.ActiveGrid.PlaneXZ.GetDistanceToPoint(found.point), Is.EqualTo(0).Within(1e-3f), "not on the grid");
        }
    }
}
