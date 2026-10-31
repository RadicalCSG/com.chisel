using System.Collections.Generic;
using Chisel.Core;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;

namespace Chisel.Components.Tests
{
    // The GPU Resident Drawer reads a mesh's index counts only when its renderer is registered: a renderer whose mesh
    // changes its counts in place keeps being drawn with the old counts, cutting off the end of its index buffer
    [TestFixture]
    public class ChiselResizedMeshTests
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

        ChiselModelComponent TwoBoxes(out ChiselBoxComponent moved)
        {
            var root = new GameObject("resized mesh test");
            roots.Add(root);
            var model = root.AddComponent<ChiselModelComponent>();
            var fixedBox = new GameObject("fixed").AddComponent<ChiselBoxComponent>();
            fixedBox.transform.SetParent(root.transform, false);
            fixedBox.Min = new float3(0, 0, 0);
            fixedBox.Max = new float3(2, 2, 2);
            moved = new GameObject("moved").AddComponent<ChiselBoxComponent>();
            moved.transform.SetParent(root.transform, false);
            moved.Min = new float3(4, 0, 0);
            moved.Max = new float3(6, 2, 2);
            Update();
            return model;
        }

        // Both boxes share a material, so they are drawn by the same renderer
        static ChiselRenderObjects Drawn(ChiselModelComponent model)
        {
            ChiselRenderObjects found = null;
            foreach (var renderable in model.generated.renderables)
            {
                if (renderable == null || !renderable.meshRenderer || !renderable.sharedMesh || renderable.sharedMesh.vertexCount == 0 ||
                    (renderable.query & SurfaceDestinationFlags.Renderable) == 0)
                    continue;
                Assert.That(found, Is.Null, "one renderer draws both boxes");
                found = renderable;
            }
            Assert.That(found, Is.Not.Null, "the model draws the boxes");
            return found;
        }

        static long IndexCount(Mesh mesh)
        {
            long count = 0;
            for (int s = 0; s < mesh.subMeshCount; s++)
                count += mesh.GetIndexCount(s);
            return count;
        }

        [Test]
        public void ARendererWhoseIndexCountChanges_DrawsANewMesh()
        {
            var model  = TwoBoxes(out var moved);
            var before = Drawn(model);
            var mesh   = before.sharedMesh;
            var count  = IndexCount(mesh);

            // overlapping the other box splits its faces
            moved.Min = new float3(1, 1, 1);
            moved.Max = new float3(3, 3, 3);
            Update();

            var after = Drawn(model);
            Assert.That(IndexCount(after.sharedMesh), Is.Not.EqualTo(count), "the overlap changes the index count");
            Assert.That(after.sharedMesh, Is.Not.SameAs(mesh), "a new mesh, so its renderer is registered again");
            Assert.That(after.meshFilter.sharedMesh, Is.SameAs(after.sharedMesh), "the renderer draws the new mesh");
            Assert.That(mesh == null, Is.True, "the old mesh is destroyed");
        }

        [Test]
        public void ARendererWhoseIndexCountStays_KeepsItsMesh()
        {
            var model = TwoBoxes(out var moved);
            moved.Min = new float3(6, 0, 0);
            moved.Max = new float3(8, 2, 2);
            Update();
            var mesh  = Drawn(model).sharedMesh;
            var count = IndexCount(mesh);

            moved.Min = new float3(10, 0, 0);
            moved.Max = new float3(12, 2, 2);
            Update();

            var after = Drawn(model);
            Assert.That(IndexCount(after.sharedMesh), Is.EqualTo(count), "moving a box apart keeps the index count");
            Assert.That(after.sharedMesh, Is.SameAs(mesh), "the mesh is updated in place");
        }
    }
}
