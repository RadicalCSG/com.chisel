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
                AssertLeftAsItIs(material);
                var drawn = ChiselRenderObjects.LightmappedMaterialOf(material, directional: true);
                Assert.IsTrue(drawn.IsKeywordEnabled("LIGHTMAP_ON"));
                Assert.IsTrue(drawn.IsKeywordEnabled("DIRLIGHTMAP_COMBINED"), "drawn as directional");
            });
        }

        [Test]
        public void NonDirectionalLightmaps_AreDrawnWithoutADirection()
        {
            DrawnWithBakedLightmap(LightmapsMode.NonDirectional, (block, material, color, direction, shadowMask) =>
            {
                Assert.That(block.GetTexture("unity_Lightmap"), Is.SameAs(color));
                AssertLeftAsItIs(material);
                var drawn = ChiselRenderObjects.LightmappedMaterialOf(material, directional: false);
                Assert.IsTrue(drawn.IsKeywordEnabled("LIGHTMAP_ON"));
                Assert.IsFalse(drawn.IsKeywordEnabled("DIRLIGHTMAP_COMBINED"), "drawn as directional");
            });
        }

        // Drawing a copy leaves its material as it is: the keywords it is drawn with are its clone's
        static void AssertLeftAsItIs(Material material)
        {
            Assert.IsFalse(material.IsKeywordEnabled("LIGHTMAP_ON"), "the material itself was given the lightmap keyword");
            Assert.IsFalse(material.IsKeywordEnabled("DIRLIGHTMAP_COMBINED"), "the material itself was given the directional keyword");
        }

        // The clone a lightmapped copy is drawn with follows its material: its properties, keywords and shader
        [Test]
        public void TheLightmappedClone_FollowsItsMaterial()
        {
            var material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            try
            {
                var clone = ChiselRenderObjects.LightmappedMaterialOf(material, directional: false);
                Assert.That(clone, Is.Not.SameAs(material));
                Assert.That(ChiselRenderObjects.LightmappedMaterialOf(material, directional: false), Is.SameAs(clone), "made once");

                material.SetColor("_BaseColor", Color.red);
                material.EnableKeyword("_NORMALMAP");
                Assert.That(ChiselRenderObjects.LightmappedMaterialOf(material, directional: false), Is.SameAs(clone));
                Assert.That(clone.GetColor("_BaseColor"), Is.EqualTo(Color.red), "the material's color");
                Assert.IsTrue(clone.IsKeywordEnabled("_NORMALMAP"), "the material's keyword");
                Assert.IsTrue(clone.IsKeywordEnabled("LIGHTMAP_ON"), "and its own");

                material.shader = Shader.Find("Universal Render Pipeline/Simple Lit");
                Assert.That(ChiselRenderObjects.LightmappedMaterialOf(material, directional: false).shader, Is.SameAs(material.shader), "the material's shader");
                AssertLeftAsItIs(material);
            }
            finally
            {
                Object.DestroyImmediate(material);
            }
        }

        // The clone a lightmapped copy is drawn with doesn't keep its material loaded
        [Test]
        public void AMaterialDrawnLightmapped_StaysUnloadable()
        {
            var material = DrawLightmappedWithAMaterialOfItsOwn();
            System.GC.Collect();
            System.GC.WaitForPendingFinalizers();
            System.GC.Collect();
            UnityEditor.EditorUtility.UnloadUnusedAssetsImmediate(true);
            Assert.That((bool)Resources.EntityIdToObject(material), Is.False, "the material is still loaded: something still refers to it");
        }

        // In a method of its own, so that the test refers to nothing of the material when the unload runs
        static EntityId DrawLightmappedWithAMaterialOfItsOwn()
        {
            var material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "Lightmapped Material" };
            Assert.That(ChiselRenderObjects.LightmappedMaterialOf(material, directional: true), Is.Not.Null);
            return material.GetEntityId();
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

                var block = new MaterialPropertyBlock();
                Assert.DoesNotThrow(() => ChiselRenderObjects.RenderChiselRenderObjects(new[] { renderable }, block,
                                                                                        Matrix4x4.identity, 0, camera, hasLightmaps: true));
                Assert.IsFalse(block.HasTexture("unity_Lightmap"), "drawn with a lightmap it doesn't have");
                AssertLeftAsItIs(material);
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
