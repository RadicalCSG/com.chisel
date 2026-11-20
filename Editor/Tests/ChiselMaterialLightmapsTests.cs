using System.Collections.Generic;
using Chisel.Components;
using Chisel.Core;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;

namespace Chisel.Editors.Tests
{
    // Surfaces whose material's shader can't sample a lightmap get no lightmap texels, and their renderer takes light
    // probes (ChiselMaterialLightmaps)
    [TestFixture]
    public class ChiselMaterialLightmapsTests
    {
        const string kLit   = "Universal Render Pipeline/Lit";
        const string kUnlit = "Universal Render Pipeline/Unlit";

        readonly List<Object> created = new();

        [TearDown]
        public void TearDown()
        {
            foreach (var item in created)
                if (item) Object.DestroyImmediate(item);
            created.Clear();
            Update();
            ChiselMaterialLightmaps.Refresh(null);
        }

        static void Update()
        {
            for (int i = 0; i < 3; i++) { ChiselNodeHierarchyManager.Update(); ChiselModelManager.Instance.UpdateModels(); }
        }

        static Shader ShaderNamed(string name)
        {
            var shader = Shader.Find(name);
            if (shader == null)
                Assert.Ignore($"{name} isn't in this project");
            return shader;
        }

        Material MaterialWith(string shader)
        {
            var material = new Material(ShaderNamed(shader)) { name = shader };
            created.Add(material);
            return material;
        }

        [Test]
        public void OnlyAShaderThatSamplesALightmap_SamplesOne()
        {
            Assert.IsTrue(ChiselMaterialLightmaps.SamplesLightmaps(ShaderNamed(kLit)), "lit");
            Assert.IsTrue(ChiselMaterialLightmaps.SamplesLightmaps(ShaderNamed("Universal Render Pipeline/Baked Lit")), "baked lit");
            Assert.IsFalse(ChiselMaterialLightmaps.SamplesLightmaps(ShaderNamed(kUnlit)), "unlit");
            Assert.IsTrue(ChiselMaterialLightmaps.SamplesLightmaps((Shader)null), "unknown: as before");
            Assert.IsTrue(ChiselMaterialLightmaps.SamplesLightmaps((Material)null), "no material: as before");
        }

        [Test]
        public void AnUnlitMaterial_GetsNoLightmap_UnlessItsParametersSayHowMany()
        {
            var lit   = MaterialWith(kLit);
            var unlit = MaterialWith(kUnlit);
            Assert.That(ChiselMaterialLightmaps.OutputFlagsOf(lit, SurfaceOutputFlags.Default), Is.EqualTo(SurfaceOutputFlags.Default));
            Assert.That(ChiselMaterialLightmaps.OutputFlagsOf(unlit, SurfaceOutputFlags.Default), Is.EqualTo(SurfaceOutputFlags.NoLightmap));
            Assert.That(ChiselMaterialLightmaps.OutputFlagsOf(unlit, SurfaceOutputFlags.DoubleSided),
                        Is.EqualTo(SurfaceOutputFlags.DoubleSided | SurfaceOutputFlags.NoLightmap), "its other flags kept");
            Assert.That(ChiselMaterialLightmaps.OutputFlagsOf(unlit, SurfaceOutputFlags.SingleLightmapTexel),
                        Is.EqualTo(SurfaceOutputFlags.SingleLightmapTexel), "one texel, as it says: a surface that gives off light");
            Assert.That(ChiselMaterialLightmaps.OutputFlagsOf(lit, SurfaceOutputFlags.NoLightmap), Is.EqualTo(SurfaceOutputFlags.NoLightmap));
        }

        ChiselModelComponent LightmappedModel()
        {
            if (Object.FindObjectsByType<ChiselModelComponent>(FindObjectsInactive.Include).Length > 0)
                Assert.Ignore("A Chisel model is loaded; run these in a scene without one");
            var root = new GameObject("lightmapped model");
            created.Add(root);
            var model = root.AddComponent<ChiselModelComponent>();
            GameObjectUtility.SetStaticEditorFlags(root, StaticEditorFlags.ContributeGI);
            model.RenderSettings.receiveGI = ReceiveGI.Lightmaps;
            model.SyncModelSettingsStore();
            return model;
        }

        static ChiselBoxComponent Box(ChiselModelComponent model, float3 min, float3 max, Material material)
        {
            var go = new GameObject("box");
            go.transform.SetParent(model.transform, false);
            var box = go.AddComponent<ChiselBoxComponent>();
            box.Max = new float3(1e4f);
            box.Min = min;
            box.Max = max;
            foreach (var surface in box.SurfaceDefinition.surfaces)
                surface.SetMaterial(material);
            box.OnValidate();
            Update();
            return box;
        }

        static ChiselRenderObjects RendererOf(ChiselModelComponent model, Material material)
        {
            foreach (var renderable in model.generated.renderables)
            {
                if (renderable != null && renderable.Valid && renderable.HasGeometry && renderable.meshRenderer.sharedMaterial == material)
                    return renderable;
            }
            Assert.Fail($"no renderer draws {material.name}");
            return null;
        }

        // The area the lightmap uvs of the renderer's mesh cover
        static float LightmapArea(ChiselRenderObjects renderable)
        {
            var mesh = renderable.sharedMesh;
            var uv2 = mesh.uv2;
            var triangles = mesh.triangles;
            var area = 0f;
            for (int t = 0; t < triangles.Length; t += 3)
            {
                var ab = uv2[triangles[t + 1]] - uv2[triangles[t]];
                var ac = uv2[triangles[t + 2]] - uv2[triangles[t]];
                area += math.abs(ab.x * ac.y - ab.y * ac.x) * 0.5f;
            }
            return area;
        }

        [Test]
        public void AnUnlitBrush_GetsNoLightmapTexels_AndItsRendererTakesLightProbes()
        {
            var lit   = MaterialWith(kLit);
            var unlit = MaterialWith(kUnlit);
            var model = LightmappedModel();
            Box(model, new float3(0, 0, 0), new float3(2, 2, 2), lit);
            Box(model, new float3(4, 0, 0), new float3(6, 2, 2), unlit);

            var litRenderer   = RendererOf(model, lit);
            var unlitRenderer = RendererOf(model, unlit);
            Assert.That(LightmapArea(litRenderer), Is.GreaterThan(0.1f), "the lit brush has lightmap texels");
            Assert.That(litRenderer.meshRenderer.receiveGI, Is.EqualTo(ReceiveGI.Lightmaps));
            Assert.That(LightmapArea(unlitRenderer), Is.Zero, "the unlit brush has none");
            Assert.That(unlitRenderer.meshRenderer.receiveGI, Is.EqualTo(ReceiveGI.LightProbes), "lit by light probes, not a lightmap");
            Assert.That(GameObjectUtility.GetStaticEditorFlags(unlitRenderer.container) & StaticEditorFlags.ContributeGI,
                        Is.EqualTo(StaticEditorFlags.ContributeGI), "it still takes part in the bake: its shadow");
        }

        // A material whose shader is swapped for one that samples a lightmap gives its surfaces lightmap texels again
        [Test]
        public void AMaterialGivenALitShader_GivesItsSurfacesLightmapTexels()
        {
            var material = MaterialWith(kUnlit);
            var model = LightmappedModel();
            Box(model, new float3(0, 0, 0), new float3(2, 2, 2), material);
            Assume.That(LightmapArea(RendererOf(model, material)), Is.Zero, "unlit: no lightmap texels");

            material.shader = ShaderNamed(kLit);
            ChiselMaterialWatcher.Refresh();
            Update();
            var renderer = RendererOf(model, material);
            Assert.That(LightmapArea(renderer), Is.GreaterThan(0.1f), "lit: lightmap texels");
            Assert.That(renderer.meshRenderer.receiveGI, Is.EqualTo(ReceiveGI.Lightmaps));
        }

        // An unlit surface that gives off light keeps the one texel its parameters give it, and its lightmap
        [Test]
        public void AnUnlitMaterialWithOneTexel_KeepsIt()
        {
            var folder = "Assets/__ChiselMaterialLightmapsTests";
            if (!AssetDatabase.IsValidFolder(folder))
                AssetDatabase.CreateFolder("Assets", folder.Substring("Assets/".Length));
            try
            {
                var material = new Material(ShaderNamed(kUnlit));
                AssetDatabase.CreateAsset(material, folder + "/unlit.mat");
                material.AddMetadataOfType<ChiselSurfaceMetadata>().outputFlags = SurfaceOutputFlags.SingleLightmapTexel;
                var model = LightmappedModel();
                Box(model, new float3(0, 0, 0), new float3(2, 2, 2), material);
                var renderer = RendererOf(model, material);
                Assert.That(LightmapArea(renderer), Is.GreaterThan(0), "a texel for each surface");
                Assert.That(renderer.meshRenderer.receiveGI, Is.EqualTo(ReceiveGI.Lightmaps));
            }
            finally
            {
                foreach (var item in created)
                    if (item) Object.DestroyImmediate(item);
                created.Clear();
                Update();
                AssetDatabase.DeleteAsset(folder);
            }
        }
    }
}
