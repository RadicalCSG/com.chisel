using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Chisel.Components.Tests
{
    [TestFixture]
    public class ChiselSceneLoadTests
    {
        const int kModelCount = 4;

        readonly List<GameObject> roots = new();
        readonly List<ChiselModelComponent> models = new();

        [SetUp]
        public void SetUp()
        {
            if (Object.FindObjectsByType<ChiselModelComponent>(FindObjectsInactive.Include).Length > 0)
                Assert.Ignore("A Chisel model is loaded; run these in a scene without one");

            for (int i = 0; i < kModelCount; i++)
            {
                var root = new GameObject("Model " + i);
                roots.Add(root);
                models.Add(root.AddComponent<ChiselModelComponent>());
                var boxObject = new GameObject("Box");
                boxObject.transform.SetParent(root.transform, false);
                boxObject.AddComponent<ChiselBoxComponent>().OnValidate();
            }
            Update();
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
            models.Clear();
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

        // What opening the scene again leaves: every node registers anew, and the renderables come back without the
        // meshes that were never saved
        void ReopenScene()
        {
            foreach (var root in roots)
                root.SetActive(false);
            Update();
            foreach (var model in models)
            {
                foreach (var renderable in model.generated.renderables)
                    DropUnsavedMeshes(renderable);
                foreach (var renderable in model.generated.debugVisualizationRenderables)
                    DropUnsavedMeshes(renderable);
            }
            foreach (var root in roots)
                root.SetActive(true);
            Update();
        }

        static void DropUnsavedMeshes(ChiselRenderObjects renderable)
        {
            if (renderable == null || renderable.invalid)
                return;
            Object.DestroyImmediate(renderable.partialMesh);
            Object.DestroyImmediate(renderable.selectionMesh);
            renderable.partialMesh = null;
            renderable.selectionMesh = null;
        }

        static int IndexCount(Mesh mesh)
        {
            var count = 0;
            for (int subMesh = 0; subMesh < mesh.subMeshCount; subMesh++)
                count += (int)mesh.GetIndexCount(subMesh);
            return count;
        }

        [Test]
        public void ReopeningAScene_LooksUpTheBrushesVisibilityAtMostOnce()
        {
            var before = ChiselUnityVisibilityManager.FullUpdateCount;
            ReopenScene();
            var fullUpdates = ChiselUnityVisibilityManager.FullUpdateCount - before;
            Assert.That(fullUpdates, Is.LessThanOrEqualTo(1),
                        $"reopening {kModelCount} models looked up the visibility of every brush {fullUpdates} times");
        }

        // The scene view draws a model through its partial meshes, which only keep the triangles of visible brushes,
        // so they must come back whole
        [Test]
        public void ReopeningAScene_GivesEveryRenderableItsPartialMeshBack()
        {
            ReopenScene();
            foreach (var model in models)
            {
                var drawn = 0;
                foreach (var renderable in model.generated.renderables)
                {
                    if (renderable == null || renderable.invalid || renderable.sharedMesh.vertexCount == 0)
                        continue;
                    Assert.IsNotNull(renderable.partialMesh, renderable.container.name);
                    Assert.AreEqual(IndexCount(renderable.sharedMesh), IndexCount(renderable.partialMesh), renderable.container.name);
                    drawn++;
                }
                Assert.That(drawn, Is.GreaterThan(0), $"{model.name} draws nothing");
            }
        }
    }
}
