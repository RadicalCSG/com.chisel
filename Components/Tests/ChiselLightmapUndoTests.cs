using System.Collections.Generic;
using Chisel.Core;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;

namespace Chisel.Components.Tests
{
    // An edit that changes a lightmapped mesh's lightmap layout takes its baked lightmap off; undoing the edit brings the
    // layout, and so the lightmap, back
    [TestFixture]
    public class ChiselLightmapUndoTests
    {
        static readonly Vector4 kBakedScaleOffset = new(0.5f, 0.25f, 0.125f, 0.0625f);

        readonly List<GameObject> roots = new();
        (UnityEngine.SceneManagement.Scene scene, bool wasDirty) remembered;

        [SetUp]
        public void SetUp()
        {
            if (Object.FindObjectsByType<ChiselModelComponent>(FindObjectsInactive.Include).Length > 0)
                Assert.Ignore("A Chisel model is loaded; run these in a scene without one");
            remembered = SceneLeftClean.Remember();
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var root in roots)
            {
                if (root)
                    Object.DestroyImmediate(root);
            }
            roots.Clear();
            Update();
            SceneLeftClean.Restore(remembered);
        }

        static void Update()
        {
            for (int i = 0; i < 3; i++)
            {
                ChiselNodeHierarchyManager.Update();
                ChiselModelManager.Instance.UpdateModels();
            }
        }

        ChiselBoxComponent LightmappedBox(out ChiselModelComponent model)
        {
            var root = new GameObject("lightmap undo test");
            roots.Add(root);
            model = root.AddComponent<ChiselModelComponent>();
            GameObjectUtility.SetStaticEditorFlags(model.gameObject, StaticEditorFlags.ContributeGI);
            model.RenderSettings.receiveGI = ReceiveGI.Lightmaps;
            model.SyncModelSettingsStore();

            var go = new GameObject("box");
            go.transform.SetParent(model.transform, false);
            var box = go.AddComponent<ChiselBoxComponent>();
            box.Min = new float3(-1, 0, -1);
            box.Max = new float3(1, 2, 1);
            Update();
            return box;
        }

        // The renderer drawing the box, found again each time: an update may give it a new one
        static MeshRenderer RendererOf(ChiselModelComponent model)
        {
            foreach (var renderable in model.generated.renderables)
            {
                if (renderable != null && renderable.Valid && renderable.HasGeometry && !renderable.debugVisualizationRenderer)
                    return renderable.meshRenderer;
            }
            Assert.Fail("the box has no renderer");
            return null;
        }

        static void Bake(MeshRenderer renderer, int index, Vector4 scaleOffset)
        {
            renderer.lightmapIndex       = index;
            renderer.lightmapScaleOffset = scaleOffset;
        }

        static void Grow(ChiselBoxComponent box)
        {
            Undo.IncrementCurrentGroup();
            Undo.RecordObject(box, "Grow");
            box.Max = new float3(2, 3, 2);
            box.OnValidate();
            Update();
        }

        static void UndoAndUpdate()
        {
            Undo.PerformUndo();
            Update();
        }

        [Test]
        public void UndoingAnEdit_BringsTheBakedLightmapBack()
        {
            var box = LightmappedBox(out var model);
            Bake(RendererOf(model), 0, kBakedScaleOffset);

            Grow(box);
            Assert.That(RendererOf(model).lightmapIndex, Is.EqualTo(-1), "the edited mesh kept a lightmap that doesn't fit it");

            UndoAndUpdate();
            var renderer = RendererOf(model);
            Assert.That(renderer.lightmapIndex, Is.EqualTo(0), "the lightmap wasn't given back");
            Assert.That(renderer.lightmapScaleOffset, Is.EqualTo(kBakedScaleOffset), "nor where it is in the lightmap");
        }

        // Once the baked lightmaps are made again or cleared, the ones kept are no longer theirs to give back
        [Test]
        public void ForgottenLightmaps_AreNotGivenBack()
        {
            var box = LightmappedBox(out var model);
            Bake(RendererOf(model), 0, kBakedScaleOffset);

            Grow(box);
            ChiselLightmapUVManager.ForgetBakedLightmaps();

            UndoAndUpdate();
            Assert.That(RendererOf(model).lightmapIndex, Is.EqualTo(-1));
        }
    }
}
