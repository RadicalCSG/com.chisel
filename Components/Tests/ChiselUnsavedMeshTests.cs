using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Chisel.Components.Tests
{
    [TestFixture]
    public class ChiselUnsavedMeshTests
    {
        const int kModelCount = 2;

        readonly List<GameObject> roots = new();

        [SetUp]
        public void SetUp()
        {
            if (Object.FindObjectsByType<ChiselModelComponent>(FindObjectsInactive.Include).Length > 0)
                Assert.Ignore("A Chisel model is loaded; run these in a scene without one");
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

        List<Mesh> BuildModelsAndTheirUnsavedMeshes()
        {
            var models = new List<ChiselModelComponent>();
            for (int i = 0; i < kModelCount; i++)
            {
                var root = new GameObject("Unsaved Mesh Model " + i);
                roots.Add(root);
                models.Add(root.AddComponent<ChiselModelComponent>());
                var boxObject = new GameObject("Box");
                boxObject.transform.SetParent(root.transform, false);
                boxObject.AddComponent<ChiselBoxComponent>().OnValidate();
            }
            Update();

            var unsaved = new List<Mesh>();
            foreach (var model in models)
            {
                foreach (var renderables in new[] { model.generated.renderables, model.generated.debugVisualizationRenderables })
                {
                    foreach (var renderable in renderables)
                    {
                        if (renderable == null || renderable.invalid)
                            continue;
                        if (renderable.partialMesh)   unsaved.Add(renderable.partialMesh);
                        if (renderable.selectionMesh) unsaved.Add(renderable.selectionMesh);
                    }
                }
            }
            Assert.That(unsaved.Count, Is.GreaterThan(0), "the models allocated no unsaved meshes: nothing is measured");
            return unsaved;
        }

        [Test]
        public void AModelsUnsavedMeshes_AreNotSavedButCanBeUnloaded()
        {
            foreach (var mesh in BuildModelsAndTheirUnsavedMeshes())
            {
                Assert.That(mesh.hideFlags & HideFlags.DontSaveInEditor, Is.EqualTo(HideFlags.DontSaveInEditor), mesh.name + " would be saved with its scene");
                Assert.That(mesh.hideFlags & HideFlags.DontSaveInBuild, Is.EqualTo(HideFlags.DontSaveInBuild), mesh.name + " would be saved in a build");
                Assert.That(mesh.hideFlags & HideFlags.DontUnloadUnusedAsset, Is.EqualTo(HideFlags.None), mesh.name + " can never be unloaded");
            }
        }
    }
}
