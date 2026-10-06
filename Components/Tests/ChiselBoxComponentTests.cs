using System.Collections.Generic;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;

namespace Chisel.Components.Tests
{
    [TestFixture]
    public class ChiselBoxComponentTests
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

        ChiselBoxComponent Box(out ChiselModelComponent model)
        {
            var root = new GameObject("box test");
            roots.Add(root);
            model = root.AddComponent<ChiselModelComponent>();
            var box = new GameObject("box");
            box.transform.SetParent(root.transform, false);
            return box.AddComponent<ChiselBoxComponent>();
        }

        // The bounds of what the model draws
        static Bounds DrawnBounds(ChiselModelComponent model)
        {
            var bounds = new Bounds();
            var first = true;
            foreach (var renderable in model.generated.renderables)
            {
                if (renderable == null || renderable.sharedMesh == null || renderable.sharedMesh.vertexCount == 0)
                    continue;
                if (first) bounds = renderable.sharedMesh.bounds;
                else       bounds.Encapsulate(renderable.sharedMesh.bounds);
                first = false;
            }
            Assert.That(first, Is.False, "the model draws nothing");
            return bounds;
        }

        static void AreEqual(Vector3 expected, Vector3 actual, string message)
        {
            Assert.That(Vector3.Distance(expected, actual), Is.LessThan(1e-4f), $"{message}: expected {expected}, got {actual}");
        }

        [Test]
        public void MinAndMax_SetFromCode_ResizeTheBrush()
        {
            var box = Box(out var model);
            box.Min = new float3(-2, -1, -2);
            box.Max = new float3(2, 0, 2);
            Update();
            var drawn = DrawnBounds(model);
            AreEqual(new Vector3(-2, -1, -2), drawn.min, "min");
            AreEqual(new Vector3(2, 0, 2), drawn.max, "max");
        }

        [Test]
        public void SizeSetFromCode_ResizesABoxThatWasAlreadyBuilt()
        {
            var box = Box(out var model);
            box.Size = new float3(2, 2, 2);
            Update();
            AreEqual(new Vector3(2, 2, 2), DrawnBounds(model).size, "the box wasn't built at its first size");

            box.Size = new float3(6, 2, 2);
            Update();
            AreEqual(new Vector3(6, 2, 2), DrawnBounds(model).size, "resizing a built box never reached its mesh");
        }

        [Test]
        public void SizeAndCenter_SetFromCode_MoveAndResizeTheBrush()
        {
            var box = Box(out var model);
            box.Size = new float3(2, 4, 6);
            box.Center = new float3(-5, 3, 1);
            Update();
            var drawn = DrawnBounds(model);
            AreEqual(new Vector3(-5, 3, 1), drawn.center, "center");
            AreEqual(new Vector3(2, 4, 6), drawn.size, "size");
        }
    }
}
