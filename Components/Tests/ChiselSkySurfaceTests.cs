using System.Collections.Generic;
using Chisel.Core;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Chisel.Components.Tests
{
    // Sky surfaces (Chisel's Sky material) draw the scene's sky: the skybox's own cubemap when it has one, otherwise the
    // reflection cubemap Unity renders from the sky
    [TestFixture]
    public class ChiselSkySurfaceTests
    {
        const string kSkyMaterialPath = "Packages/com.chisel/Package Resources/Materials/URP/Tools/Sky.mat";

        readonly List<Object> created = new();
        Cubemap fallback;
        static readonly Vector4 kFallbackHDR = new(2, 1, 0, 0);

        [SetUp]
        public void SetUp()
        {
            fallback = Cube();
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var obj in created)
            {
                if (obj) Object.DestroyImmediate(obj);
            }
            created.Clear();
        }

        Cubemap Cube()
        {
            var cube = new Cubemap(4, TextureFormat.RGBA32, false);
            created.Add(cube);
            return cube;
        }

        Material Skybox(string shaderName)
        {
            var shader = Shader.Find(shaderName);
            Assert.That(shader, Is.Not.Null, shaderName);
            var material = new Material(shader);
            created.Add(material);
            return material;
        }

        static float ColorSpaceDouble => QualitySettings.activeColorSpace == ColorSpace.Linear ? 4.59479380f : 2.0f;

        [Test]
        public void ACubemapSkybox_GivesItsCubemap_RotationAndBrightness()
        {
            var skybox = Skybox("Skybox/Cubemap");
            var cube = Cube();
            skybox.SetTexture("_Tex", cube);
            skybox.SetFloat("_Rotation", 45);
            skybox.SetFloat("_Exposure", 2);
            skybox.SetColor("_Tint", Color.white);

            var sky = ChiselSkyCubemap.SkyFor(skybox, fallback, kFallbackHDR);
            Assert.That(sky.cubemap, Is.SameAs(cube));
            Assert.That(sky.rotation, Is.EqualTo(45));
            // the skybox shader's own scale: tint, doubled for the color space, times the exposure
            Assert.That(sky.multiplier.x, Is.EqualTo(ColorSpaceDouble * 2).Within(1e-4f));
            Assert.That(sky.multiplier.y, Is.EqualTo(ColorSpaceDouble * 2).Within(1e-4f));
        }

        [Test]
        public void AProceduralSkybox_GivesTheReflectionCubemap()
        {
            var sky = ChiselSkyCubemap.SkyFor(Skybox("Skybox/Procedural"), fallback, kFallbackHDR);
            Assert.That(sky.cubemap, Is.SameAs(fallback));
            Assert.That(sky.decodeHDR, Is.EqualTo(kFallbackHDR));
            Assert.That(sky.rotation, Is.EqualTo(0));
            Assert.That(sky.multiplier, Is.EqualTo(Vector3.one));
        }

        [Test]
        public void NoSkybox_GivesTheReflectionCubemap()
        {
            var sky = ChiselSkyCubemap.SkyFor(null, fallback, kFallbackHDR);
            Assert.That(sky.cubemap, Is.SameAs(fallback));
        }

        [Test]
        public void ACubemapSkyboxWithoutACubemap_GivesTheReflectionCubemap()
        {
            var sky = ChiselSkyCubemap.SkyFor(Skybox("Skybox/Cubemap"), fallback, kFallbackHDR);
            Assert.That(sky.cubemap, Is.SameAs(fallback));
        }

        [Test]
        public void TheSky_IsSetForTheShader()
        {
            var cube = Cube();
            var previous = Shader.GetGlobalTexture(ChiselSkyCubemap.kCubemapID);
            try
            {
                ChiselSkyCubemap.Apply(new ChiselSkyCubemap.Sky { cubemap = cube, decodeHDR = Vector4.one, rotation = 30, multiplier = new Vector3(1, 2, 3) });
                Assert.That(Shader.GetGlobalTexture(ChiselSkyCubemap.kCubemapID), Is.SameAs(cube));
                Assert.That(Shader.GetGlobalFloat(ChiselSkyCubemap.kRotationID), Is.EqualTo(30));
                Assert.That((Vector3)Shader.GetGlobalVector(ChiselSkyCubemap.kMultiplierID), Is.EqualTo(new Vector3(1, 2, 3)));
            }
            finally
            {
                Shader.SetGlobalTexture(ChiselSkyCubemap.kCubemapID, previous);
            }
        }

        [Test]
        public void TheSkyMaterial_DrawsWithTheSkyShader()
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(kSkyMaterialPath);
            Assert.That(material, Is.Not.Null, kSkyMaterialPath);
            Assert.That(material.shader.name, Is.EqualTo("Chisel/Sky"));
            Assert.That(ShaderUtil.ShaderHasError(material.shader), Is.False, "the shader doesn't compile");
            Assert.That(material.FindPass("ShadowCaster"), Is.EqualTo(-1), "a sky surface casts no shadow");
            Assert.That(material.FindPass("DepthOnly"), Is.Not.EqualTo(-1));
            Assert.That(material.FindPass("DepthNormalsOnly"), Is.Not.EqualTo(-1));
        }

        // Like Source's sky: it seals the level, but casts no shadow and takes no part in a bake
        [Test]
        public void TheSkyMaterial_CollidesButCastsNoShadowAndIsLeftOutOfTheBake()
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(kSkyMaterialPath);
            Assert.That(material, Is.Not.Null, kSkyMaterialPath);
            var metadata = material.GetMetadataOfType<ChiselSurfaceMetadata>();
            Assert.That(metadata, Is.Not.Null, "the Sky material has no Chisel surface parameters");
            var flags = metadata.destinationFlags.Normalize();
            Assert.That(flags & SurfaceDestinationFlags.Renderable, Is.Not.EqualTo(SurfaceDestinationFlags.None));
            Assert.That(flags & SurfaceDestinationFlags.Collidable, Is.Not.EqualTo(SurfaceDestinationFlags.None));
            Assert.That(flags & SurfaceDestinationFlags.ShadowCasting, Is.EqualTo(SurfaceDestinationFlags.None));
            Assert.That(flags & SurfaceDestinationFlags.ExcludedFromGlobalIllumination, Is.Not.EqualTo(SurfaceDestinationFlags.None));
            Assert.That(metadata.outputFlags & SurfaceOutputFlags.NoLightmap, Is.Not.EqualTo(SurfaceOutputFlags.None));
        }
    }
}
