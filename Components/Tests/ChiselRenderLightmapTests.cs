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

        // A copy the scene view draws gets the same baked lighting Unity draws the renderer with: its directional
        // lightmap when the lightmaps are directional, and its shadow mask
        [Test]
        public void ADirectionalLightmapAndShadowMask_AreDrawnWithTheCopy()
        {
            DrawnWithBakedLightmap(LightmapsMode.CombinedDirectional, (block, material, color, direction, shadowMask) =>
            {
                Assert.That(block.GetTexture("unity_Lightmap"),    Is.SameAs(color));
                Assert.That(block.GetTexture("unity_LightmapInd"), Is.SameAs(direction), "the directional lightmap");
                Assert.That(block.GetTexture("unity_ShadowMask"),  Is.SameAs(shadowMask), "the shadow mask");
                Assert.IsTrue(material.IsKeywordEnabled("LIGHTMAP_ON"));
                Assert.IsTrue(material.IsKeywordEnabled("DIRLIGHTMAP_COMBINED"), "drawn as directional");
            });
        }

        [Test]
        public void NonDirectionalLightmaps_AreDrawnWithoutADirection()
        {
            DrawnWithBakedLightmap(LightmapsMode.NonDirectional, (block, material, color, direction, shadowMask) =>
            {
                Assert.That(block.GetTexture("unity_Lightmap"), Is.SameAs(color));
                Assert.IsTrue(material.IsKeywordEnabled("LIGHTMAP_ON"));
                Assert.IsFalse(material.IsKeywordEnabled("DIRLIGHTMAP_COMBINED"), "drawn as directional");
            });
        }

        static void DrawnWithBakedLightmap(LightmapsMode mode, System.Action<MaterialPropertyBlock, Material, Texture2D, Texture2D, Texture2D> check)
        {
            var remembered = SceneLeftClean.Remember();
            var lightmaps  = LightmapSettings.lightmaps;
            var lightmapsMode = LightmapSettings.lightmapsMode;
            var color      = new Texture2D(4, 4);
            var direction  = new Texture2D(4, 4);
            var shadowMask = new Texture2D(4, 4);
            var root = new GameObject("Lightmapped Model");
            root.SetActive(false);
            var material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            ChiselRenderObjects renderable = null;
            try
            {
                LightmapSettings.lightmaps = new[] { new LightmapData { lightmapColor = color, lightmapDir = direction, shadowMask = shadowMask } };
                LightmapSettings.lightmapsMode = mode;

                var camera = new GameObject("Camera", typeof(Camera)).GetComponent<Camera>();
                camera.transform.SetParent(root.transform, false);
                camera.enabled = false;

                renderable = ChiselRenderObjects.Create("Renderable", root.transform, GameObjectState.Create(root), SurfaceDestinationFlags.Renderable);
                renderable.sharedMesh.SetVertices(new[] { Vector3.zero, Vector3.right, Vector3.up });
                renderable.sharedMesh.SetTriangles(new[] { 0, 1, 2 }, 0);
                renderable.renderMaterials = new[] { material };
                renderable.meshRenderer.enabled = true;
                renderable.meshRenderer.forceRenderingOff = true;
                renderable.meshRenderer.lightmapIndex = 0;

                var block = new MaterialPropertyBlock();
                ChiselRenderObjects.RenderChiselRenderObjects(new[] { renderable }, block, Matrix4x4.identity, 0, camera, hasLightmaps: true);
                check(block, material, color, direction, shadowMask);
            }
            finally
            {
                LightmapSettings.lightmaps     = lightmaps;
                LightmapSettings.lightmapsMode = lightmapsMode;
                renderable?.Destroy();
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(material);
                Object.DestroyImmediate(color);
                Object.DestroyImmediate(direction);
                Object.DestroyImmediate(shadowMask);
                SceneLeftClean.Restore(remembered);
            }
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
