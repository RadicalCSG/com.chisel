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

        // Building the meshes from OnValidate makes Unity warn about SendMessage when the generated objects are made
        [Test]
        public void ModelOnValidate_LeavesTheMeshesToTheNextUpdate()
        {
            var box = Box(out var model);
            box.Min = new float3(-1, -1, -1);
            box.Max = new float3(1, 1, 1);
            Update();
            var before = DrawnBounds(model);

            box.Max = new float3(3, 1, 1);
            model.OnValidate();
            AreEqual(before.size, DrawnBounds(model).size, "after the model's OnValidate");

            Update();
            AreEqual(new Vector3(4, 2, 2), DrawnBounds(model).size, "after the next update");
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

        // A side dragged past the one across from it: the box keeps the sides as they were given (swapping them would
        // hand the drag to the other side, which then follows the mouse) and is built the right way out
        [Test]
        public void ANegativeSize_StaysAsGiven_AndIsBuiltTheRightWayOut()
        {
            var box = Box(out var model);
            box.Min = new float3(-1, 0, -1);
            box.Max = new float3(1, 2, 1);
            Update();
            box.Max = new float3(1, -1.5f, 1);   // the top dragged down past the bottom
            Update();
            Assert.That((Vector3)box.Min, Is.EqualTo(new Vector3(-1, 0, -1)), "the bottom moved");
            Assert.That((Vector3)box.Max, Is.EqualTo(new Vector3(1, -1.5f, 1)), "the top isn't where it was dragged to");
            var drawn = DrawnBounds(model);
            AreEqual(new Vector3(-1, -1.5f, -1), drawn.min, "min");
            AreEqual(new Vector3(1, 0, 1), drawn.max, "max");
            var triangles = 0;
            foreach (var renderable in model.generated.renderables)
            {
                if (renderable == null || renderable.sharedMesh == null)
                    continue;
                var mesh = renderable.sharedMesh; var v = mesh.vertices; var t = mesh.triangles;
                for (int i = 0; i < t.Length; i += 3)
                {
                    // facing out of the box: from its centre to the triangle
                    var normal = Vector3.Cross(v[t[i + 1]] - v[t[i]], v[t[i + 2]] - v[t[i]]);
                    var centre = (v[t[i]] + v[t[i + 1]] + v[t[i + 2]]) / 3 - new Vector3(0, -0.75f, 0);
                    Assert.That(Vector3.Dot(normal, centre), Is.GreaterThan(0), "a triangle faces into the box");
                    triangles++;
                }
            }
            Assert.That(triangles, Is.EqualTo(12), "a box");
        }

        // An existing box can't be made flat: a size of 0 is taken as the smallest a box can have
        [Test]
        public void AZeroSize_IsTheSmallestSize()
        {
            var box = Box(out var model);
            box.Min = new float3(-1, 0, -1);
            box.Max = new float3(1, 0, 1);
            Update();
            Assert.That(box.Max.y - box.Min.y, Is.EqualTo(Core.ChiselBox.kMinSize).Within(1e-7f));
            Assert.That(box.TopTreeNode.Valid, Is.True, "no brush");
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
