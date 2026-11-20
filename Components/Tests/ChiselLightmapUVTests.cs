using NUnit.Framework;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using static Chisel.Components.Tests.ChiselBakeScene;

namespace Chisel.Components.Tests
{
    [TestFixture]
    public class ChiselLightmapUVTests
    {
        [Test]
        public void OnlyModelsLitByLightmaps_AreLightmapped()
        {
            var root = new GameObject("Lightmap UV Model");
            root.SetActive(false);
            try
            {
                var model = root.AddComponent<ChiselModelComponent>();

                model.RenderSettings.receiveGI = ReceiveGI.LightProbes;
                Assert.IsFalse(ChiselLightmapUVManager.IsLightmapped(model, StaticEditorFlags.ContributeGI), "lit by light probes");

                model.RenderSettings.receiveGI = ReceiveGI.Lightmaps;
                Assert.IsTrue(ChiselLightmapUVManager.IsLightmapped(model, StaticEditorFlags.ContributeGI | StaticEditorFlags.BatchingStatic));
                Assert.IsFalse(ChiselLightmapUVManager.IsLightmapped(model, StaticEditorFlags.BatchingStatic), "not in the bake at all");
                Assert.IsFalse(ChiselLightmapUVManager.IsLightmapped(null, StaticEditorFlags.ContributeGI));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        // A renderer an update makes, for a chunk that had none, goes into the bake with the model at once: not only
        // after some later update
        [Test]
        public void ARendererMadeByAnUpdate_IsInTheBake()
        {
            var root = new GameObject("Lightmap UV Model");
            try
            {
                var model = root.AddComponent<ChiselModelComponent>();
                Lightmapped(model);
                AddBox(model, new float3(0, 0, 0));
                AddBox(model, new float3(40, 0, 0));

                var renderables = Renderables(model);
                Assert.That(renderables.Count, Is.EqualTo(2), "a renderer in each chunk");
                foreach (var renderable in renderables)
                {
                    Assert.That(GameObjectUtility.GetStaticEditorFlags(renderable.container) & StaticEditorFlags.ContributeGI,
                                Is.EqualTo(StaticEditorFlags.ContributeGI), $"the renderer at {renderable.sharedMesh.bounds.center}");
                }
            }
            finally
            {
                Object.DestroyImmediate(root);
                UpdateModels();
            }
        }

        static void AddBox(ChiselModelComponent model, float3 min)
        {
            var go = new GameObject("box");
            go.transform.SetParent(model.transform, false);
            var box = go.AddComponent<ChiselBoxComponent>();
            box.Max = new float3(1e4f);
            box.Min = min;
            box.Max = min + 1;
            UpdateModels();
        }
    }
}
