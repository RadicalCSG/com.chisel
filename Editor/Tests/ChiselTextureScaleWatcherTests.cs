using System.Collections;
using System.Collections.Generic;
using Chisel.Components;
using Chisel.Core;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace Chisel.Editors.Tests
{
    // A material's texture swapped for one of another size: the generators using the material are built again
    [TestFixture]
    public class ChiselTextureScaleWatcherTests
    {
        readonly List<Object> created = new();

        [TearDown]
        public void TearDown()
        {
            foreach (var item in created)
                if (item) Object.DestroyImmediate(item);
            created.Clear();
            for (int i = 0; i < 3; i++) { ChiselNodeHierarchyManager.Update(); ChiselModelManager.Instance.UpdateModels(); }
            ChiselTextureScale.Refresh(null);
        }

        static void Update()
        {
            for (int i = 0; i < 3; i++) { ChiselNodeHierarchyManager.Update(); ChiselModelManager.Instance.UpdateModels(); }
        }

        Texture2D Texture(int width, int height)
        {
            var texture = new Texture2D(width, height);
            created.Add(texture);
            return texture;
        }

        // How far the uvs of the face facing up reach along v
        static float TopVRange(ChiselModelComponent model, Material material)
        {
            float min = float.MaxValue, max = float.MinValue;
            foreach (var renderable in model.generated.renderables)
            {
                if (renderable == null || !renderable.Valid || !renderable.HasGeometry || renderable.meshRenderer.sharedMaterial != material)
                    continue;
                var mesh = renderable.sharedMesh;
                var normals = mesh.normals; var uvs = mesh.uv;
                for (int i = 0; i < normals.Length; i++)
                {
                    if (normals[i].y < 0.999f)
                        continue;
                    min = math.min(min, uvs[i].y);
                    max = math.max(max, uvs[i].y);
                }
            }
            return max - min;
        }

        [Test]
        public void ATextureOfAnotherSize_BuildsTheSurfacesUsingItAgain()
        {
            if (Object.FindObjectsByType<ChiselModelComponent>(FindObjectsInactive.Include).Length > 0)
                Assert.Ignore("A Chisel model is loaded; run these in a scene without one");
            var material = new Material(ChiselProjectSettings.DefaultWallMaterial) { mainTexture = Texture(256, 64) };
            created.Add(material);
            var root = new GameObject("texture scale watcher test");
            created.Add(root);
            var model = root.AddComponent<ChiselModelComponent>();
            var go = new GameObject("box");
            go.transform.SetParent(root.transform, false);
            var box = go.AddComponent<ChiselBoxComponent>();
            box.Min = new float3(0, 0, 0);
            box.Max = new float3(1, 1, 1);
            Update();
            foreach (var surface in box.SurfaceDefinition.surfaces)
                surface.SetMaterial(material);
            box.OnValidate();
            Update();
            Assume.That(TopVRange(model, material), Is.EqualTo(4).Within(1e-4f));

            material.mainTexture = Texture(256, 128);
            ChiselTextureScaleWatcher.Refresh();
            Update();
            Assert.That(TopVRange(model, material), Is.EqualTo(2).Within(1e-4f));
        }

        const string kFolder = "Assets/__ChiselTextureScaleWatcherTests";

        // Undoing a change to a material's own uv scale (its Chisel Surface Parameters) builds the surfaces using it with
        // the scale they had
        [UnityTest]
        public IEnumerator UndoingAMaterialsUVScale_BuildsTheSurfacesUsingItAgain()
        {
            if (Object.FindObjectsByType<ChiselModelComponent>(FindObjectsInactive.Include).Length > 0)
                Assert.Ignore("A Chisel model is loaded; run these in a scene without one");
            if (!AssetDatabase.IsValidFolder(kFolder))
                AssetDatabase.CreateFolder("Assets", kFolder.Substring("Assets/".Length));
            try
            {
                var texture = new Texture2D(256, 64);
                AssetDatabase.CreateAsset(texture, kFolder + "/texture.asset");
                var material = new Material(ChiselProjectSettings.DefaultWallMaterial) { mainTexture = texture };
                AssetDatabase.CreateAsset(material, kFolder + "/material.mat");
                var metadata = material.AddMetadataOfType<ChiselSurfaceMetadata>();

                var root = new GameObject("texture scale undo test");
                created.Add(root);
                var model = root.AddComponent<ChiselModelComponent>();
                var go = new GameObject("box");
                go.transform.SetParent(root.transform, false);
                var box = go.AddComponent<ChiselBoxComponent>();
                box.Min = new float3(0, 0, 0);
                box.Max = new float3(1, 1, 1);
                Update();
                foreach (var surface in box.SurfaceDefinition.surfaces)
                    surface.SetMaterial(material);
                box.OnValidate();
                Update();
                Assume.That(TopVRange(model, material), Is.EqualTo(4).Within(1e-4f), "the texture's scale");

                // as the material's inspector changes it
                Undo.IncrementCurrentGroup();
                var serialized = new SerializedObject(metadata);
                serialized.FindProperty(ChiselSurfaceMetadata.kOverrideUVScaleFieldName).boolValue = true;
                serialized.FindProperty(ChiselSurfaceMetadata.kUVScaleFieldName).vector2Value = new Vector2(1, 2);
                serialized.ApplyModifiedProperties();
                Undo.IncrementCurrentGroup();
                ChiselTextureScaleWatcher.Refresh();
                Update();
                Assume.That(TopVRange(model, material), Is.EqualTo(2).Within(1e-4f), "the material's own scale");

                Undo.PerformUndo();
                for (int i = 0; i < 3; i++)
                {
                    yield return null;
                    Update();
                }
                Assert.That(metadata.overrideUVScale, Is.False, "the change is undone");
                Assert.That(TopVRange(model, material), Is.EqualTo(4).Within(1e-4f), "built again with the texture's scale");
            }
            finally
            {
                AssetDatabase.DeleteAsset(kFolder);
            }
        }
    }
}
