using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Chisel.Components.Tests
{
    [TestFixture]
    public class ChiselDestroyedModelTests
    {
        const int kModelCount = 2;

        (Scene scene, bool wasDirty) sceneAtStart;

        [SetUp]
        public void SetUp()
        {
            if (Object.FindObjectsByType<ChiselModelComponent>(FindObjectsInactive.Include).Length > 0)
                Assert.Ignore("A Chisel model is loaded; run these in a scene without one");
            sceneAtStart = SceneLeftClean.Remember();
        }

        // After everything a test measured
        [TearDown]
        public void TearDown()
        {
            SceneLeftClean.Restore(sceneAtStart);
        }

        static void Update()
        {
            for (int i = 0; i < 3; i++)
            {
                ChiselNodeHierarchyManager.Update();
                ChiselModelManager.Instance.UpdateModels();
            }
        }

        // Every mesh a model made, by id: what the unload is asked about
        internal static void RecordMeshes(ChiselModelComponent model, List<(string what, EntityId id)> meshes)
        {
            foreach (var renderables in new[] { model.generated.renderables, model.generated.debugVisualizationRenderables })
            {
                foreach (var renderable in renderables)
                {
                    if (renderable == null || renderable.invalid)
                        continue;
                    if (renderable.sharedMesh)    meshes.Add((renderable.sharedMesh.name + " (render mesh)", renderable.sharedMesh.GetEntityId()));
                    if (renderable.partialMesh)   meshes.Add((renderable.partialMesh.name + " (partial mesh)", renderable.partialMesh.GetEntityId()));
                    if (renderable.selectionMesh) meshes.Add((renderable.selectionMesh.name + " (selection mesh)", renderable.selectionMesh.GetEntityId()));
                }
            }
            foreach (var collider in model.generated.colliders)
            {
                if (collider != null && collider.sharedMesh)
                    meshes.Add((collider.sharedMesh.name + " (collider mesh)", collider.sharedMesh.GetEntityId()));
            }
        }

        static List<(string what, EntityId id)> BuildAndDestroyModels(bool keptTheirSavedMeshes, bool rebuildFirst = false)
        {
            var roots  = new List<GameObject>();
            var models = new List<ChiselModelComponent>();
            for (int i = 0; i < kModelCount; i++)
            {
                var root = new GameObject("Destroyed Model " + i);
                roots.Add(root);
                models.Add(root.AddComponent<ChiselModelComponent>());
                var boxObject = new GameObject("Box");
                boxObject.transform.SetParent(root.transform, false);
                boxObject.AddComponent<ChiselBoxComponent>().OnValidate();
            }
            Update();

            if (keptTheirSavedMeshes)
            {
                ChiselModelManager.Instance.StoreInputHashes(roots[0].scene);
                foreach (var root in roots)
                    root.SetActive(false);
                Update();
                foreach (var root in roots)
                    root.SetActive(true);
                Update();
                foreach (var model in models)
                    Assert.That(ChiselModelManager.HasNoNodes(model), Is.True, $"{model.name} did not keep its saved meshes: this measures nothing");
            }

            var meshes = new List<(string what, EntityId id)>();
            foreach (var model in models)
                RecordMeshes(model, meshes);
            Assert.That(meshes.Count, Is.GreaterThan(0), "the models made no meshes: nothing is measured");

            foreach (var root in roots)
                Object.DestroyImmediate(root);
            if (rebuildFirst)
                ChiselNodeHierarchyManager.Rebuild();
            Update();
            return meshes;
        }

        internal static void AssertNoneLoadedAfterAnUnload(List<(string what, EntityId id)> meshes)
        {
            System.GC.Collect();
            System.GC.WaitForPendingFinalizers();
            System.GC.Collect();
            EditorUtility.UnloadUnusedAssetsImmediate(true);

            var stillLoaded = new List<string>();
            foreach (var (what, id) in meshes)
            {
                if (Resources.EntityIdToObject(id))
                    stillLoaded.Add(what);
            }
            Assert.That(stillLoaded, Is.Empty, $"{stillLoaded.Count} of the {meshes.Count} meshes of the destroyed models are still loaded: something still refers to the models");
        }

        [Test]
        public void ADestroyedModel_LeavesNoMeshLoaded()
        {
            AssertNoneLoadedAfterAnUnload(BuildAndDestroyModels(keptTheirSavedMeshes: false));
        }

        [Test]
        public void ADestroyedModelThatKeptItsSavedMeshes_LeavesNoMeshLoaded()
        {
            AssertNoneLoadedAfterAnUnload(BuildAndDestroyModels(keptTheirSavedMeshes: true));
        }

        [Test]
        public void ADestroyedModel_LeavesNoMeshLoaded_WhenARebuildComesFirst()
        {
            AssertNoneLoadedAfterAnUnload(BuildAndDestroyModels(keptTheirSavedMeshes: false, rebuildFirst: true));
        }

        [Test]
        public void ADestroyedModelThatKeptItsSavedMeshes_LeavesNoMeshLoaded_WhenARebuildComesFirst()
        {
            AssertNoneLoadedAfterAnUnload(BuildAndDestroyModels(keptTheirSavedMeshes: true, rebuildFirst: true));
        }

        // Made the way an import makes a map's models (ImporterContextMenu.ImportMap, VmfBrushImportTests): under a root
        // that stays inactive, here destroyed before it was ever activated
        static List<(string what, EntityId id)> BuildAndDestroyANeverActiveModel()
        {
            var root = new GameObject("Never Active");
            root.SetActive(false);
            var model = ChiselModelManager.Instance.CreateNewModel(root.transform);
            ChiselComponentFactory.Create<ChiselBoxComponent>(model);
            Assert.That(model.generated, Is.Not.Null, "the model made no generated objects: this measures nothing");
            var meshes = new List<(string what, EntityId id)>();
            RecordMeshes(model, meshes);
            Assert.That(meshes.Count, Is.GreaterThan(0), "the model made no meshes: nothing is measured");

            Object.DestroyImmediate(root);
            Update();
            return meshes;
        }

        [Test]
        public void AModelThatNeverBecameActive_LeavesNoMeshLoaded()
        {
            AssertNoneLoadedAfterAnUnload(BuildAndDestroyANeverActiveModel());
        }

        // Only the component: its GameObject stays, and with it the generated objects under it, which drew the model's
        // last meshes for as long as the scene stayed open
        [Test]
        public void ARemovedModelComponent_TakesWhatItMadeWithIt()
        {
            var root = new GameObject("Removed Model Component");
            try
            {
                // No brush: once the model is gone it would go to the scene's default model, and make one
                var model = root.AddComponent<ChiselModelComponent>();
                Update();
                var container = model.generated?.generatedDataContainer;
                Assert.That((bool)container, Is.True, "the model made no generated objects: this measures nothing");

                Object.DestroyImmediate(model);
                Update();
                Assert.That((bool)container, Is.False, "the removed model component's generated objects are still there");
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }
    }
}
