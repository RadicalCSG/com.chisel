using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;

namespace Chisel.Components.Tests
{
    [TestFixture]
    public class ChiselModelShadowTests
    {
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

        ChiselModelComponent ModelWithABox()
        {
            var root = new GameObject("Shadow Model");
            roots.Add(root);
            var model = root.AddComponent<ChiselModelComponent>();
            var box = new GameObject("Box");
            box.transform.SetParent(root.transform, false);
            box.AddComponent<ChiselBoxComponent>();
            Update();
            return model;
        }

        // The model's settings as the inspector changes them: the change goes through OnValidate, which rebuilds the model
        static void SetShadows(ChiselModelComponent model, bool cast, bool receive)
        {
            model.RenderSettings.castShadows    = cast;
            model.RenderSettings.receiveShadows = receive;
            model.SyncModelSettingsStore();
            Update();
        }

        static List<MeshRenderer> DrawingRenderers(ChiselModelComponent model)
        {
            var renderers = new List<MeshRenderer>();
            foreach (var renderable in model.generated.renderables)
            {
                if (renderable == null || !renderable.Valid || !renderable.meshRenderer ||
                    renderable.sharedMesh == null || renderable.sharedMesh.vertexCount == 0)
                    continue;
                renderers.Add(renderable.meshRenderer);
            }
            Assert.That(renderers, Is.Not.Empty, "the box drew nothing");
            return renderers;
        }

        [Test]
        public void AModel_CastsAndReceivesShadows()
        {
            var model = ModelWithABox();
            Assert.IsTrue(model.RenderSettings.castShadows);
            Assert.IsTrue(model.RenderSettings.receiveShadows);
            foreach (var renderer in DrawingRenderers(model))
            {
                Assert.AreEqual(ShadowCastingMode.On, renderer.shadowCastingMode, renderer.name);
                Assert.IsTrue(renderer.receiveShadows, renderer.name);
            }
        }

        [Test]
        public void AModelWithoutShadows_NeitherCastsNorReceivesThem()
        {
            var model = ModelWithABox();
            SetShadows(model, cast: false, receive: false);
            foreach (var renderer in DrawingRenderers(model))
            {
                Assert.AreEqual(ShadowCastingMode.Off, renderer.shadowCastingMode, renderer.name);
                Assert.IsFalse(renderer.receiveShadows, renderer.name);
            }
        }

        [Test]
        public void CastingAndReceiving_AreSeparateSwitches()
        {
            var model = ModelWithABox();
            SetShadows(model, cast: false, receive: true);
            foreach (var renderer in DrawingRenderers(model))
            {
                Assert.AreEqual(ShadowCastingMode.Off, renderer.shadowCastingMode, renderer.name);
                Assert.IsTrue(renderer.receiveShadows, renderer.name);
            }
            SetShadows(model, cast: true, receive: false);
            foreach (var renderer in DrawingRenderers(model))
            {
                Assert.AreEqual(ShadowCastingMode.On, renderer.shadowCastingMode, renderer.name);
                Assert.IsFalse(renderer.receiveShadows, renderer.name);
            }
        }

        [Test]
        public void TurningShadowsBackOn_RestoresThem()
        {
            var model = ModelWithABox();
            SetShadows(model, cast: false, receive: false);
            SetShadows(model, cast: true, receive: true);
            foreach (var renderer in DrawingRenderers(model))
            {
                Assert.AreEqual(ShadowCastingMode.On, renderer.shadowCastingMode, renderer.name);
                Assert.IsTrue(renderer.receiveShadows, renderer.name);
            }
        }
    }
}
