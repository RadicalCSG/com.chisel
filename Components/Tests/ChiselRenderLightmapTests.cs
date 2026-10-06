using Chisel.Core;
using NUnit.Framework;
using UnityEngine;

namespace Chisel.Components.Tests
{
    [TestFixture]
    public class ChiselRenderLightmapTests
    {
        [Test]
        public void ARendererTheBakeOnlyTookIntoAccount_DrawsWithoutALightmap()
        {
            DrawsWithoutALightmap(0xFFFE);
        }

        [Test]
        public void AnIndexPastTheBakedLightmaps_DrawsWithoutALightmap()
        {
            DrawsWithoutALightmap(LightmapSettings.lightmaps.Length);
        }

        [Test]
        public void ARendererThatWasNeverBaked_DrawsWithoutALightmap()
        {
            DrawsWithoutALightmap(-1);
        }

        static void DrawsWithoutALightmap(int lightmapIndex)
        {
            var root = new GameObject("Lightmapped Model");
            root.SetActive(false);
            var material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            ChiselRenderObjects renderable = null;
            try
            {
                var camera = new GameObject("Camera", typeof(Camera)).GetComponent<Camera>();
                camera.transform.SetParent(root.transform, false);
                camera.enabled = false;

                renderable = ChiselRenderObjects.Create("Renderable", root.transform, GameObjectState.Create(root), SurfaceDestinationFlags.Renderable);
                renderable.sharedMesh.SetVertices(new[] { Vector3.zero, Vector3.right, Vector3.up });
                renderable.sharedMesh.SetTriangles(new[] { 0, 1, 2 }, 0);
                renderable.renderMaterials = new[] { material };
                renderable.meshRenderer.enabled = true;
                renderable.meshRenderer.forceRenderingOff = true;
                renderable.meshRenderer.lightmapIndex = lightmapIndex;
                Assert.IsTrue(renderable.IsEnabled());
                material.EnableKeyword("LIGHTMAP_ON");

                Assert.DoesNotThrow(() => ChiselRenderObjects.RenderChiselRenderObjects(new[] { renderable }, new MaterialPropertyBlock(),
                                                                                        Matrix4x4.identity, 0, camera, hasLightmaps: true));
                Assert.IsFalse(material.IsKeywordEnabled("LIGHTMAP_ON"), "drawn with a lightmap it doesn't have");
            }
            finally
            {
                renderable?.Destroy();
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(material);
            }
        }
    }
}
