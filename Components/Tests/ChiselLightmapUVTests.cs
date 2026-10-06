using NUnit.Framework;
using UnityEditor;
using UnityEngine;

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
    }
}
