using NUnit.Framework;
using UnityEngine;

namespace Chisel.Editors.Tests
{
    // Placing on a mesh that isn't Chisel's goes by the plane of the triangle under the cursor. It went by the hit's
    // normal, the mesh's vertex normals blended and turned by its matrix with the scale left in, so a generator could
    // lean or lie on its side
    public sealed class ChiselMeshPickingTests
    {
        Mesh m_Mesh;

        // a flat square facing up, with vertex normals that say it faces along x
        [SetUp]
        public void SetUp()
        {
            m_Mesh = new Mesh();
            m_Mesh.vertices  = new[] { new Vector3(-1, 0, -1), new Vector3(-1, 0, 1), new Vector3(1, 0, 1), new Vector3(1, 0, -1) };
            m_Mesh.triangles = new[] { 0, 1, 2, 0, 2, 3 };
            m_Mesh.normals   = new[] { Vector3.right, Vector3.right, Vector3.right, Vector3.right };
        }

        [TearDown]
        public void TearDown() { Object.DestroyImmediate(m_Mesh); }

        static readonly Ray kDown = new(new Vector3(0.3f, 5, 0.2f), Vector3.down);

        [Test]
        public void AMeshHit_FacesTheWayItsTriangleDoes()
        {
            Assert.That(ChiselMeshPicking.IntersectRayMeshFace(kDown, m_Mesh, Matrix4x4.identity, out var point, out var normal), Is.True);
            Assert.That(Vector3.Distance(point, new Vector3(0.3f, 0, 0.2f)), Is.LessThan(1e-4f), "point");
            Assert.That(Vector3.Distance(normal, Vector3.up), Is.LessThan(1e-4f), $"normal {normal}");
        }

        [Test]
        public void AMeshHit_OnATurnedAndStretchedMesh_FacesTheWayItsTriangleDoes()
        {
            var turn   = Quaternion.Euler(0, 0, 30);
            var matrix = Matrix4x4.TRS(Vector3.zero, turn, new Vector3(3, 1, 1));
            Assert.That(ChiselMeshPicking.IntersectRayMeshFace(kDown, m_Mesh, matrix, out _, out var normal), Is.True);
            Assert.That(Vector3.Distance(normal, turn * Vector3.up), Is.LessThan(1e-4f), $"normal {normal}");
        }

        // seen from below, the plane faces the other way, towards where it was seen from
        [Test]
        public void AMeshHitFromBehind_FacesTheRay()
        {
            var up = new Ray(new Vector3(0.3f, -5, 0.2f), Vector3.up);
            Assert.That(ChiselMeshPicking.IntersectRayMeshFace(up, m_Mesh, Matrix4x4.identity, out _, out var normal), Is.True);
            Assert.That(Vector3.Distance(normal, Vector3.down), Is.LessThan(1e-4f), $"normal {normal}");
        }
    }
}
