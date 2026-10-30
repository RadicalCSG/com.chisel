using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Chisel.Components.Tests
{
    // Clicking a surface picks its brush: Unity's own picking stays off the meshes generated for it
    [TestFixture]
    public class ChiselGeneratedPickingTests
    {
        readonly List<GameObject> roots = new();

        [SetUp]
        public void SetUp()
        {
            if (Object.FindObjectsByType<ChiselModelComponent>(FindObjectsInactive.Include).Length > 0)
                Assert.Ignore("A Chisel model is loaded; run these in a scene without one");
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
        }

        static void Update()
        {
            for (int i = 0; i < 3; i++)
            {
                ChiselNodeHierarchyManager.Update();
                ChiselModelManager.Instance.UpdateModels();
            }
        }

        [Test]
        public void TheGeneratedMeshes_CantBePicked_TheBrushCan()
        {
            var root = new GameObject("picking test");
            roots.Add(root);
            var model = root.AddComponent<ChiselModelComponent>();
            var box = new GameObject("box");
            box.transform.SetParent(model.transform, false);
            box.AddComponent<ChiselBoxComponent>();
            Update();

            var container = model.generated.generatedDataContainer;
            Assert.That(container, Is.Not.Null);
            Assert.That(model.generated.renderables, Is.Not.Empty, "something is drawn");
            var visibility = SceneVisibilityManager.instance;
            Assert.That(visibility.IsPickingDisabledOnAllDescendants(container), Is.True, "Unity's picking is off the generated meshes");
            Assert.That(visibility.IsPickingDisabled(box), Is.False, "the brush is picked");
        }
    }
}
