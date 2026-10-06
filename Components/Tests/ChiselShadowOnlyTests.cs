using System.Collections.Generic;
using Chisel.Core;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;

namespace Chisel.Components.Tests
{
    [TestFixture]
    public class ChiselShadowOnlyTests
    {
        const int kShadowOnlyRenderable = 2;        // ChiselGeneratedObjects.kGeneratedMeshRendererNames
        const int kShadowOnlyDebugHelper = 2;       // ChiselGeneratedObjects.kGeneratedVisualizationRendererNames

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

        // A box with its faces on the given materials, in turn
        ChiselModelComponent ModelWithABoxOf(params Material[] materials)
        {
            var root = new GameObject("Shadow-only Model");
            roots.Add(root);
            var model = root.AddComponent<ChiselModelComponent>();
            var boxObject = new GameObject("Box");
            boxObject.transform.SetParent(root.transform, false);
            var box = boxObject.AddComponent<ChiselBoxComponent>();
            box.OnValidate();

            var surfaces = box.surfaceArray.surfaces;
            Assert.That(surfaces, Is.Not.Empty);
            for (int i = 0; i < surfaces.Length; i++)
                surfaces[i] = ChiselSurface.Create(materials[i % materials.Length]);
            box.OnValidate();
            Update();
            return model;
        }

        // By name: NUnit builds the test cases before Chisel's project settings are loaded, so the materials themselves
        // would come out null
        static readonly string[] kShadowOnlyMaterials = { "ShadowOnly", "ForceShadowOnly" };

        static Material ShadowOnlyMaterial(string name)
        {
            var material = (name == "ShadowOnly") ? ChiselProjectSettings.ShadowOnlySurfacesMaterial
                                                  : ChiselProjectSettings.ForceShadowOnlySurfacesMaterial;
            Assert.IsNotNull(material, name);
            return material;
        }

        // Every sub-mesh is drawn with ForceShadowOnly: by the MeshRenderer, and by the editor, which draws the generated
        // meshes itself from renderMaterials (ChiselRenderObjects.RenderMesh)
        static void AssertCastWithForceShadowOnly(ChiselRenderObjects renderable)
        {
            var forceShadowOnly = ShadowOnlyMaterial("ForceShadowOnly");
            var subMeshCount = renderable.sharedMesh.subMeshCount;
            Assert.That(renderable.sharedMesh.vertexCount, Is.GreaterThan(0), "the shadow-only renderer got no surfaces");
            Assert.IsTrue(renderable.meshRenderer.enabled);
            Assert.AreEqual(ShadowCastingMode.On, renderable.meshRenderer.shadowCastingMode, "URP casts nothing from ShadowsOnly");
            Assert.AreEqual(subMeshCount, renderable.meshRenderer.sharedMaterials.Length);
            Assert.That(renderable.meshRenderer.sharedMaterials, Is.All.EqualTo(forceShadowOnly));
            Assert.AreEqual(subMeshCount, renderable.renderMaterials.Length);
            Assert.That(renderable.renderMaterials, Is.All.EqualTo(forceShadowOnly));
            Assert.IsTrue(renderable.IsRendered(), "the editor doesn't draw the shadow-only renderer");
        }

        [Test]
        public void AShadowOnlySurface_IsCastWithForceShadowOnly([ValueSource(nameof(kShadowOnlyMaterials))] string materialName)
        {
            var model = ModelWithABoxOf(ShadowOnlyMaterial(materialName));
            AssertCastWithForceShadowOnly(model.generated.renderables[kShadowOnlyRenderable]);
        }

        // Each material is a sub-mesh of its own
        [Test]
        public void ShadowOnlySurfacesOnSeveralMaterials_AreAllCastWithForceShadowOnly()
        {
            var model = ModelWithABoxOf(ShadowOnlyMaterial("ShadowOnly"), ShadowOnlyMaterial("ForceShadowOnly"));
            var renderable = model.generated.renderables[kShadowOnlyRenderable];
            Assert.AreEqual(2, renderable.sharedMesh.subMeshCount);
            AssertCastWithForceShadowOnly(renderable);
        }

        [Test]
        public void AShadowOnlySurface_IsNotDrawnVisibly([ValueSource(nameof(kShadowOnlyMaterials))] string materialName)
        {
            var model = ModelWithABoxOf(ShadowOnlyMaterial(materialName));
            for (int i = 0; i < model.generated.renderables.Length; i++)
            {
                var renderable = model.generated.renderables[i];
                if (i == kShadowOnlyRenderable || renderable == null || !renderable.Valid)
                    continue;
                Assert.IsFalse(renderable.meshRenderer.enabled && renderable.sharedMesh.vertexCount > 0, renderable.container.name);
            }
        }

        [Test]
        public void TheShadowOnlyDebugHelper_ShowsTheSurfacesWithTheShadowOnlyMaterial([ValueSource(nameof(kShadowOnlyMaterials))] string materialName)
        {
            var model = ModelWithABoxOf(ShadowOnlyMaterial(materialName));
            var helper = model.generated.debugVisualizationRenderables[kShadowOnlyDebugHelper];
            Assert.That(helper.sharedMesh.vertexCount, Is.GreaterThan(0), "the ShadowOnly debug helper shows nothing");
            Assert.That(helper.renderMaterials, Is.Not.Empty);
            foreach (var helperMaterial in helper.renderMaterials)
                Assert.AreSame(ChiselProjectSettings.ShadowOnlySurfacesMaterial, helperMaterial);
        }

        [Test]
        public void AShadowOnlySurface_Collides([ValueSource(nameof(kShadowOnlyMaterials))] string materialName)
        {
            var model = ModelWithABoxOf(ShadowOnlyMaterial(materialName));
            var vertexCount = 0;
            foreach (var collider in model.generated.colliders)
                vertexCount += collider.sharedMesh.vertexCount;
            Assert.That(vertexCount, Is.GreaterThan(0), "the shadow-only surfaces got no collider");
        }

        [Test]
        public void AModelWithoutShadows_DoesNotCastItsShadowOnlySurfaces()
        {
            var model = ModelWithABoxOf(ShadowOnlyMaterial("ShadowOnly"));
            model.RenderSettings.castShadows = false;
            model.SyncModelSettingsStore();
            Update();
            Assert.IsFalse(model.generated.renderables[kShadowOnlyRenderable].meshRenderer.enabled);
        }
    }
}
