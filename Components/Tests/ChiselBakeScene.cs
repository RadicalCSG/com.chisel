using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Chisel.Components.Tests
{
    // Baking lighting in a test. Lightmapping.Bake doesn't bake the test runner's unsaved scene: a bake scene is a saved
    // scene of its own in its place, with lighting settings small enough to bake in seconds. Disposed, it clears the bake,
    // gives the runner an empty scene and deletes its folder. Also what the tests that look at lighting share: rendering
    // with only the scene's own lights, and what a bake left a model with.
    sealed class ChiselBakeScene : IDisposable
    {
        public const string kFolder = "Assets/ChiselBakeTest";

        public readonly LightingSettings settings;

        public ChiselBakeScene(int texelsPerUnit = 4, LightmapsMode directionality = LightmapsMode.NonDirectional)
        {
            if (!AssetDatabase.IsValidFolder(kFolder))
                AssetDatabase.CreateFolder("Assets", kFolder.Substring("Assets/".Length));
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            Assume.That(EditorSceneManager.SaveScene(scene, kFolder + "/bake.unity"), Is.True, "the scene is saved");
            settings = new LightingSettings
            {
                bakedGI                = true,
                lightmapResolution     = texelsPerUnit,
                lightmapPadding        = 2,
                lightmapMaxSize        = 256,
                directSampleCount      = 8,
                indirectSampleCount    = 8,
                environmentSampleCount = 8,
                maxBounces             = 1,
                ao                     = false,
                directionalityMode     = directionality,
                filteringMode          = LightingSettings.FilterMode.None
            };
            Lightmapping.lightingSettings = settings;
            UpdateModels();
        }

        public void Dispose()
        {
            Lightmapping.Clear();
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            UpdateModels();
            UnityEngine.Object.DestroyImmediate(settings);
            AssetDatabase.DeleteAsset(kFolder);
        }

        public static void UpdateModels()
        {
            for (int i = 0; i < 3; i++)
            {
                ChiselNodeHierarchyManager.Update();
                ChiselModelManager.Instance.UpdateModels();
            }
        }

        // Bakes the scene with only its own lights: none from the sky
        public static void Bake()
        {
            Assert.That(InTheDark(() => Lightmapping.Bake()), Is.True, "baking");
        }

        // Only the lights of the scene: none from the sky or reflections
        public static T InTheDark<T>(Func<T> lit)
        {
            var ambientMode         = RenderSettings.ambientMode;
            var ambientLight        = RenderSettings.ambientLight;
            var skybox              = RenderSettings.skybox;
            var reflectionIntensity = RenderSettings.reflectionIntensity;
            try
            {
                RenderSettings.ambientMode         = UnityEngine.Rendering.AmbientMode.Flat;
                RenderSettings.ambientLight        = Color.black;
                RenderSettings.skybox              = null;
                RenderSettings.reflectionIntensity = 0;
                Physics.SyncTransforms();
                return lit();
            }
            finally
            {
                RenderSettings.ambientMode         = ambientMode;
                RenderSettings.ambientLight        = ambientLight;
                RenderSettings.skybox              = skybox;
                RenderSettings.reflectionIntensity = reflectionIntensity;
            }
        }

        // How bright what the camera sees is, on average (0 to 1)
        public static float Brightness(Camera camera)
        {
            const int kSize = 32;
            var target = RenderTexture.GetTemporary(kSize, kSize, 24);
            var image  = new Texture2D(kSize, kSize, TextureFormat.RGBA32, false);
            var active = RenderTexture.active;
            try
            {
                camera.targetTexture = target;
                camera.Render();
                RenderTexture.active = target;
                image.ReadPixels(new Rect(0, 0, kSize, kSize), 0, 0);
                image.Apply();
                var sum = 0f;
                foreach (var color in image.GetPixels())
                    sum += color.grayscale;
                return sum / (kSize * kSize);
            }
            finally
            {
                RenderTexture.active = active;
                camera.targetTexture = null;
                RenderTexture.ReleaseTemporary(target);
                UnityEngine.Object.DestroyImmediate(image);
            }
        }

        // A camera that only renders when asked, at a point looking one way
        public static Camera Looking(List<GameObject> owner, Vector3 at, Vector3 looking)
        {
            var cameraObject = new GameObject("camera");
            owner.Add(cameraObject);
            var up = Mathf.Abs(looking.y) > 0.5f ? Vector3.forward : Vector3.up;
            cameraObject.transform.SetPositionAndRotation(at, Quaternion.LookRotation(looking, up));
            var camera = cameraObject.AddComponent<Camera>();
            camera.enabled         = false;
            camera.clearFlags      = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.black;
            camera.nearClipPlane   = 0.05f;
            camera.farClipPlane    = 20;
            camera.fieldOfView     = 60;
            return camera;
        }

        // A model that contributes to the bake and is lit by lightmaps
        public static void Lightmapped(ChiselModelComponent model)
        {
            GameObjectUtility.SetStaticEditorFlags(model.gameObject, StaticEditorFlags.ContributeGI);
            model.RenderSettings.receiveGI = ReceiveGI.Lightmaps;
            model.SyncModelSettingsStore();
            UpdateModels();
        }

        // The renderers that draw the model's surfaces (not its debug views)
        public static List<ChiselRenderObjects> Renderables(ChiselModelComponent model)
        {
            var found = new List<ChiselRenderObjects>();
            foreach (var renderable in model.generated.renderables)
            {
                if (renderable != null && renderable.Valid && renderable.HasGeometry && !renderable.debugVisualizationRenderer)
                    found.Add(renderable);
            }
            return found;
        }

        public static MeshRenderer RendererOf(ChiselModelComponent model)
        {
            var renderables = Renderables(model);
            Assert.That(renderables, Is.Not.Empty, "the model has no renderer");
            return renderables[0].meshRenderer;
        }

        // How bright the light probes are at a point: their constant term, averaged over red, green and blue
        public static float ProbeBrightness(Vector3 at)
        {
            LightProbes.GetInterpolatedProbe(at, null, out var probe);
            return (probe[0, 0] + probe[1, 0] + probe[2, 0]) / 3;
        }

        // What a bake left a renderer with, for a failure's message
        public static string BakeState(MeshRenderer renderer)
        {
            var index     = renderer.lightmapIndex;
            var lightmaps = LightmapSettings.lightmaps;
            var texture   = index >= 0 && index < lightmaps.Length ? lightmaps[index].lightmapColor : null;
            return $"lightmap {index} of {lightmaps.Length}, its texture {(texture ? $"{texture.width}x{texture.height}" : "missing")}, " +
                   $"scale and offset {renderer.lightmapScaleOffset}, lighting data {(Lightmapping.lightingDataAsset ? "there" : "missing")}";
        }

        // Bakes the lamp, and how bright the camera sees the baked light alone: with the lamp off, as before a bake a baked
        // light is drawn live
        public static float BakedBrightness(Light lamp, Camera camera)
        {
            lamp.enabled = true;
            Bake();
            lamp.enabled = false;
            return InTheDark(() => Brightness(camera));
        }
    }
}
