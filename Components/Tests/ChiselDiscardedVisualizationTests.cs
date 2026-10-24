using System.Collections.Generic;
using Chisel.Core;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;

namespace Chisel.Components.Tests
{
    // The Discarded debug visualization: what the CSG removed of each surface, only while a draw mode shows it
    [TestFixture]
    public class ChiselDiscardedVisualizationTests
    {
        const int kDiscarded = AssignMeshesJob.kDiscardedVisualization;

        readonly List<GameObject> roots = new();
        int debugVisualizations;

        [SetUp]
        public void SetUp()
        {
            if (Object.FindObjectsByType<ChiselModelComponent>(FindObjectsInactive.Include).Length > 0)
                Assert.Ignore("A Chisel model is loaded; run these in a scene without one");
            debugVisualizations = CompactHierarchyManager.DebugVisualizations;
            CompactHierarchyManager.DebugVisualizations = debugVisualizations & ~(1 << kDiscarded);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var root in roots)
            {
                if (root)
                    Object.DestroyImmediate(root);
            }
            roots.Clear();
            CompactHierarchyManager.DebugVisualizations = debugVisualizations;
            Update();
        }

        static void Update()
        {
            for (int i = 0; i < 3; i++)
            {
                ChiselNodeHierarchyManager.Update();
                ChiselModelManager.Instance.UpdateModels();
            }
        }

        // Two unit boxes overlapping by half: each has a side inside the other, and a half of four sides on the other's
        ChiselModelComponent OverlappingBoxes()
        {
            var root = new GameObject("Discarded visualization Model");
            roots.Add(root);
            var model = root.AddComponent<ChiselModelComponent>();
            Box(model, new float3(0, 0, 0), new float3(1, 1, 1));
            Box(model, new float3(0.5f, 0, 0), new float3(1.5f, 1, 1));
            Update();
            return model;
        }

        static void Box(ChiselModelComponent model, float3 min, float3 max)
        {
            var go = new GameObject("Box");
            go.transform.SetParent(model.transform, false);
            var box = go.AddComponent<ChiselBoxComponent>();
            if (math.any(min > box.Max)) { box.Max = max; box.Min = min; }
            else                         { box.Min = min; box.Max = max; }
        }

        static double Area(Mesh mesh)
        {
            var area = 0.0;
            var vertices = mesh.vertices;
            for (int s = 0; s < mesh.subMeshCount; s++)
            {
                var triangles = mesh.GetTriangles(s);
                for (int t = 0; t + 2 < triangles.Length; t += 3)
                    area += Vector3.Cross(vertices[triangles[t + 1]] - vertices[triangles[t]], vertices[triangles[t + 2]] - vertices[triangles[t]]).magnitude * 0.5;
            }
            return area;
        }

        static double DiscardedArea(ChiselModelComponent model)
        {
            var area = 0.0;
            foreach (var renderable in model.generated.debugVisualizationRenderables)
            {
                if (renderable != null && renderable.debugMode == kDiscarded && renderable.sharedMesh)
                    area += Area(renderable.sharedMesh);
            }
            return area;
        }

        static double DrawnArea(ChiselModelComponent model)
        {
            var area = 0.0;
            foreach (var renderable in model.generated.renderables)
            {
                if (renderable != null && renderable.Valid && renderable.sharedMesh)
                    area += Area(renderable.sharedMesh);
            }
            return area;
        }

        static double ColliderArea(ChiselModelComponent model)
        {
            var area = 0.0;
            foreach (var collider in model.generated.colliders)
            {
                if (collider != null && collider.sharedMesh)
                    area += Area(collider.sharedMesh);
            }
            return area;
        }

        [Test]
        public void NothingIsDiscarded_WhileNoDrawModeShowsIt()
        {
            var model = OverlappingBoxes();
            Assert.That(DiscardedArea(model), Is.EqualTo(0));
        }

        // Each box loses the side inside the other (1 m2 each), and where their top, bottom, front and back lie on one
        // another (0.5 m2 each) one of the two is removed
        [Test]
        public void ShowingIt_ShowsWhatTheCSGRemoved()
        {
            var model = OverlappingBoxes();
            CompactHierarchyManager.DebugVisualizations |= 1 << kDiscarded;
            Update();
            Assert.That(DiscardedArea(model), Is.EqualTo(4.0).Within(1e-4), "the two inner sides and one of each pair on one another");
        }

        // What is removed doesn't change what is drawn or what collides
        [Test]
        public void ShowingIt_LeavesTheOutputAsItIs()
        {
            var model = OverlappingBoxes();
            var drawn    = DrawnArea(model);
            var collider = ColliderArea(model);
            Assert.That(drawn, Is.EqualTo(8.0).Within(1e-4), "a 1.5 x 1 x 1 box");

            CompactHierarchyManager.DebugVisualizations |= 1 << kDiscarded;
            Update();
            Assert.That(DrawnArea(model), Is.EqualTo(drawn).Within(1e-5));
            Assert.That(ColliderArea(model), Is.EqualTo(collider).Within(1e-5));

            CompactHierarchyManager.DebugVisualizations &= ~(1 << kDiscarded);
            Update();
            Assert.That(DiscardedArea(model), Is.EqualTo(0), "hidden again, it is made no more");
            Assert.That(DrawnArea(model), Is.EqualTo(drawn).Within(1e-5));
        }
    }
}
