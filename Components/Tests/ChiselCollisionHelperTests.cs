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
        int debugVisualizations;

        [SetUp]
        public void SetUp()
        {
            if (Object.FindObjectsByType<ChiselModelComponent>(FindObjectsInactive.Include).Length > 0)
                Assert.Ignore("A Chisel model is loaded; run these in a scene without one");
            // the collision helper is a debug visualization: made while a draw mode shows it
            debugVisualizations = CompactHierarchyManager.DebugVisualizations;
            CompactHierarchyManager.DebugVisualizations = debugVisualizations | (1 << kCollisionHelper);
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
            CompactHierarchyManager.DebugVisualizations = debugVisualizations;
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
            AddBox(model, Vector3.zero, material);
            Update();
            return model;
        }

        static ChiselBoxComponent AddBox(ChiselModelComponent model, Vector3 position, Material material = null)
        {
            var boxObject = new GameObject("Box");
            boxObject.transform.SetParent(model.transform, false);
            boxObject.transform.localPosition = position;
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
            return box;
        }

        // The collision helper's renderers: one per chunk it shows
        static List<ChiselRenderObjects> Helpers(ChiselModelComponent model)
        {
            var helpers = new List<ChiselRenderObjects>();
            foreach (var renderable in model.generated.debugVisualizationRenderables)
            {
                if (renderable != null && renderable.debugMode == kCollisionHelper)
                    helpers.Add(renderable);
            }
            return helpers;
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
            var helpers = Helpers(model);
            var helperVertices = 0;
            var helperIndices  = 0;
            foreach (var helper in helpers)
            {
                helperVertices += helper.sharedMesh.vertexCount;
                helperIndices  += IndexCount(helper.sharedMesh);
                Assert.That(helper.renderMaterials, Is.Not.Empty);
                Assert.That(helper.renderMaterials, Is.All.EqualTo(ChiselProjectSettings.CollisionSurfacesMaterial));
            }
            Assert.That(helperVertices, Is.GreaterThan(0), "the collision helper shows nothing");

            // The same triangles: a surface's collider and render vertices share one index buffer
            var colliderIndices = 0;
            foreach (var collider in model.generated.colliders)
                colliderIndices += IndexCount(collider.sharedMesh);
            Assert.That(colliderIndices, Is.GreaterThan(0), "the model has no collider");
            Assert.AreEqual(colliderIndices, helperIndices, "the helper shows other triangles than the colliders");
        }

        [Test]
        public void TheCollisionHelper_ShowsWhatTheModelCollidesWith()
        {
            AssertShowsTheColliders(ModelWithABox());
        }

        // A debug visualization no draw mode shows is not made; a draw mode that shows it makes it at the next update,
        // without building a brush again
        [Test]
        public void TheCollisionHelper_IsMadeOnlyWhileADrawModeShowsIt()
        {
            CompactHierarchyManager.DebugVisualizations = debugVisualizations & ~(1 << kCollisionHelper);
            var model = ModelWithABox();
            Assert.That(Helpers(model), Is.Empty, "the helper was made with no draw mode showing it");

            CompactHierarchyManager.DebugVisualizations = debugVisualizations | (1 << kCollisionHelper);
            Update();
            AssertShowsTheColliders(model);
            Assert.That(CompactHierarchyManager.LastUpdateModifiedBrushCount, Is.EqualTo(0), "a brush was built again");
        }

        static readonly Bounds kMarker = new(new Vector3(1000, 1000, 1000), new Vector3(1, 1, 1));

        // Like the render meshes, a mesh per 32 m chunk, and one that is made from the same as last time is kept
        [Test]
        public void TheCollisionHelper_IsAMeshPerChunk_AndAnEditInOneKeepsTheOther()
        {
            var model = ModelWithABox();
            var near  = model.GetComponentInChildren<ChiselBoxComponent>();
            AddBox(model, new Vector3(40, 0, 0));
            Update();
            var helpers = Helpers(model);
            Assert.That(helpers.Count, Is.EqualTo(2), "a mesh per chunk");
            AssertShowsTheColliders(model);
            var far = helpers.Find(h => h.sharedMesh.bounds.center.x > 32);
            Assert.That(far, Is.Not.Null, "the far box's chunk");
            far.sharedMesh.bounds = kMarker;

            near.transform.position += new Vector3(0, 0, 0.5f);
            ChiselNodeHierarchyManager.UpdateTreeNodeTransformation(near);
            Update();

            Assert.That(Helpers(model), Does.Contain(far), "the far chunk's renderer");
            Assert.That(far.sharedMesh.bounds, Is.EqualTo(kMarker), "its mesh was not made again");
        }

        // A surface that only collides (the Collision helper material) draws nothing, and the helper is where it shows
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
