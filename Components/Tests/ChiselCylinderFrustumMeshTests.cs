using System.Collections.Generic;
using Chisel.Core;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;

namespace Chisel.Components.Tests
{
    // A warped conical frustum, built in a model from overlapping convex brushes: its mesh is its caps and sides, nothing
    // of the faces between the brushes
    [TestFixture]
    public class ChiselCylinderFrustumMeshTests
    {
        readonly List<GameObject> roots = new();
        (UnityEngine.SceneManagement.Scene scene, bool wasDirty) remembered;

        [SetUp]
        public void SetUp() { remembered = SceneLeftClean.Remember(); }

        [TearDown]
        public void TearDown()
        {
            foreach (var root in roots)
                if (root) Object.DestroyImmediate(root);
            roots.Clear();
            Update();
            SceneLeftClean.Restore(remembered);
        }

        static void Update()
        {
            for (int i = 0; i < 3; i++)
            {
                ChiselNodeHierarchyManager.Update();
                ChiselModelManager.Instance.UpdateModels();
            }
        }

        static double Area(float3 a, float3 b, float3 c) => math.length(math.cross((double3)(b - a), (double3)(c - a))) * 0.5;

        [Test]
        public void AWarpedFrustumsMesh_IsItsCapsAndSides([Values(3, 7, 16)] int sides)
        {
            var model = new GameObject("model").AddComponent<ChiselModelComponent>();
            roots.Add(model.gameObject);
            var cylinder = new GameObject("cylinder").AddComponent<ChiselCylinderComponent>();
            cylinder.transform.SetParent(model.transform, false);
            cylinder.Type            = CylinderShapeType.ConicalFrustum;
            cylinder.IsElliptical    = true;
            cylinder.Sides           = sides;
            cylinder.Height          = 2;
            cylinder.TopDiameterX    = 2;
            cylinder.TopDiameterZ    = 0.5f;
            cylinder.BottomDiameterX = 0.5f;
            cylinder.BottomDiameterZ = 2;
            Update();

            // what it should be: the faces of its brushes that are output
            var brushes = new List<CSGTreeBrush>();
            ChiselGeneratorParts.BrushesOf(cylinder, brushes);
            Assert.That(brushes.Count, Is.EqualTo(sides), "a brush for each side");
            double expected = 0;
            foreach (var brush in brushes)
            {
                ref var mesh = ref BrushMeshManager.GetBrushMeshBlob(brush.BrushMesh).Value;
                for (int p = 0; p < mesh.polygons.Length; p++)
                {
                    ref var polygon = ref mesh.polygons[p];
                    if (polygon.surface.destinationFlags == SurfaceDestinationFlags.None)
                        continue;
                    var first = mesh.localVertices[mesh.halfEdges[polygon.firstEdge].vertexIndex];
                    for (int e = 1; e + 1 < polygon.edgeCount; e++)
                        expected += Area(first, mesh.localVertices[mesh.halfEdges[polygon.firstEdge + e].vertexIndex],
                                                mesh.localVertices[mesh.halfEdges[polygon.firstEdge + e + 1].vertexIndex]);
                }
            }

            // what it is: the triangles drawn
            double drawn = 0;
            foreach (var renderable in model.generated.renderables)
            {
                if (renderable == null || !renderable.Valid || !renderable.HasGeometry ||
                    (renderable.query & SurfaceDestinationFlags.Renderable) == 0)
                    continue;
                var mesh = renderable.sharedMesh;
                var vertices = mesh.vertices;
                var triangles = mesh.triangles;
                for (int t = 0; t < triangles.Length; t += 3)
                    drawn += Area(vertices[triangles[t]], vertices[triangles[t + 1]], vertices[triangles[t + 2]]);
            }
            Assert.That(drawn, Is.EqualTo(expected).Within(expected * 1e-4), "the drawn mesh isn't the frustum's caps and sides");
        }
    }
}
