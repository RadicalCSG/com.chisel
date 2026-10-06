using System.Collections.Generic;
using Chisel.Core;
using NUnit.Framework;
using UnityEngine;

namespace Chisel.Components.Tests
{
    [TestFixture]
    public class ChiselCollisionHelperTests
    {
        const int kCollisionHelper = ChiselGeneratedObjects.kColliderDebugVisualizationIndex;

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

        // A box, with every face on the given material, or on the defaults without one
        ChiselModelComponent ModelWithABox(Material material = null)
        {
            var root = new GameObject("Collision helper Model");
            roots.Add(root);
            var model = root.AddComponent<ChiselModelComponent>();
            var boxObject = new GameObject("Box");
            boxObject.transform.SetParent(root.transform, false);
            var box = boxObject.AddComponent<ChiselBoxComponent>();
            box.OnValidate();
            if (material)
            {
                var surfaces = box.surfaceArray.surfaces;
                Assert.That(surfaces, Is.Not.Empty);
                for (int i = 0; i < surfaces.Length; i++)
                    surfaces[i] = ChiselSurface.Create(material);
                box.OnValidate();
            }
            Update();
            return model;
        }

        static int IndexCount(Mesh mesh)
        {
            var count = 0;
            for (int s = 0; s < mesh.subMeshCount; s++)
                count += (int)mesh.GetIndexCount(s);
            return count;
        }

        static void AssertShowsTheColliders(ChiselModelComponent model)
        {
            var helper = model.generated.debugVisualizationRenderables[kCollisionHelper];
            Assert.That(helper.sharedMesh.vertexCount, Is.GreaterThan(0), "the collision helper shows nothing");

            // The same triangles: a surface's collider and render vertices share one index buffer
            var colliderIndices = 0;
            foreach (var collider in model.generated.colliders)
                colliderIndices += IndexCount(collider.sharedMesh);
            Assert.That(colliderIndices, Is.GreaterThan(0), "the model has no collider");
            Assert.AreEqual(colliderIndices, IndexCount(helper.sharedMesh), "the helper shows other triangles than the colliders");

            Assert.That(helper.renderMaterials, Is.Not.Empty);
            Assert.That(helper.renderMaterials, Is.All.EqualTo(ChiselProjectSettings.CollisionSurfacesMaterial));
        }

        [Test]
        public void TheCollisionHelper_ShowsWhatTheModelCollidesWith()
        {
            AssertShowsTheColliders(ModelWithABox());
        }

        // A surface that only collides (the Collision helper material; the Source importer puts clip faces on it) draws
        // nothing, and the helper is where it shows
        [Test]
        public void TheCollisionHelper_ShowsSurfacesThatOnlyCollide()
        {
            var collision = ChiselProjectSettings.CollisionSurfacesMaterial;
            Assert.IsNotNull(collision, "Collision");
            var model = ModelWithABox(collision);
            AssertShowsTheColliders(model);
            foreach (var renderable in model.generated.renderables)
            {
                if (renderable == null || !renderable.Valid)
                    continue;
                Assert.IsFalse(renderable.meshRenderer.enabled && renderable.sharedMesh.vertexCount > 0, renderable.container.name);
            }
        }
    }
}
