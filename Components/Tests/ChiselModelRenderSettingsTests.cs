using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Chisel.Components.Tests
{
    // The lightmapping settings of a model go on the renderers it makes
    [TestFixture]
    public class ChiselModelRenderSettingsTests
    {
        readonly List<Object> created = new();

        [SetUp]
        public void SetUp()
        {
            if (Object.FindObjectsByType<ChiselModelComponent>(FindObjectsInactive.Include).Length > 0)
                Assert.Ignore("A Chisel model is loaded; run these in a scene without one");
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var item in created)
                if (item) Object.DestroyImmediate(item);
            created.Clear();
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

        [Test]
        public void TheModelsLightmapSettings_ReachItsRenderers()
        {
            var root = new GameObject("render settings test");
            created.Add(root);
            var model = root.AddComponent<ChiselModelComponent>();
            var parameters = new LightmapParameters();
            created.Add(parameters);
            var settings = model.RenderSettings;
            settings.ImportantGI       = true;
            settings.MinimumChartSize  = 2;   // Unity takes 2 or 4
            settings.AutoUVMaxDistance = 1.25f;
            settings.LightmapParameters = parameters;

            var go = new GameObject("box");
            go.transform.SetParent(root.transform, false);
            go.AddComponent<ChiselBoxComponent>();
            Update();

            var renderers = 0;
            foreach (var renderable in model.generated.renderables)
            {
                if (renderable == null || !renderable.Valid || !renderable.HasGeometry)
                    continue;
                renderers++;
                using var serialized = new SerializedObject(renderable.meshRenderer);
                Assert.That(serialized.FindProperty("m_ImportantGI").boolValue, Is.True, "important GI");
                Assert.That(serialized.FindProperty("m_MinimumChartSize").intValue, Is.EqualTo(2), "minimum chart size");
                Assert.That(serialized.FindProperty("m_AutoUVMaxDistance").floatValue, Is.EqualTo(1.25f), "auto UV max distance");
                Assert.That(serialized.FindProperty("m_LightmapParameters").objectReferenceValue, Is.SameAs(parameters), "lightmap parameters");
            }
            Assert.That(renderers, Is.GreaterThan(0), "the box is drawn");
        }

        // Two surfaces of a brush whose indices are swapped are told apart by their hash
        [Test]
        public void SurfaceReferences_WithSwappedIndices_HashDifferently()
        {
            var a = new SurfaceReference(null, 1, default, 2);
            var b = new SurfaceReference(null, 2, default, 1);
            Assert.That(a.GetHashCode(), Is.Not.EqualTo(b.GetHashCode()));
        }
    }
}
