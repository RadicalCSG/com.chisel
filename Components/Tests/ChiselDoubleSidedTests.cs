using System.Collections.Generic;
using Chisel.Core;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;

namespace Chisel.Components.Tests
{
    // A surface whose material is double sided (SurfaceOutputFlags.DoubleSided) is drawn from behind too, and collides once
    [TestFixture]
    public class ChiselDoubleSidedTests
    {
        const string kFolder = "Assets/ChiselDoubleSidedTests";

        readonly List<GameObject> roots = new();
        Material material;
        ChiselSurfaceMetadata metadata;

        [SetUp]
        public void SetUp()
        {
            if (Object.FindObjectsByType<ChiselModelComponent>(FindObjectsInactive.Include).Length > 0)
                Assert.Ignore("A Chisel model is loaded; run these in a scene without one");
            if (!AssetDatabase.IsValidFolder(kFolder))
                AssetDatabase.CreateFolder("Assets", kFolder.Substring("Assets/".Length));
            material = new Material(ChiselProjectSettings.DefaultWallMaterial);
            AssetDatabase.CreateAsset(material, kFolder + "/double sided.mat");
            metadata = material.AddMetadataOfType<ChiselSurfaceMetadata>();
            metadata.outputFlags = SurfaceOutputFlags.DoubleSided;
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
            AssetDatabase.DeleteAsset(kFolder);
        }

        static void Update()
        {
            for (int i = 0; i < 3; i++)
            {
                ChiselNodeHierarchyManager.Update();
                ChiselModelManager.Instance.UpdateModels();
            }
        }

        ChiselModelComponent ModelWithABox()
        {
            var root = new GameObject("double sided test");
            roots.Add(root);
            var model = root.AddComponent<ChiselModelComponent>();
            var go = new GameObject("Box");
            go.transform.SetParent(model.transform, false);
            var box = go.AddComponent<ChiselBoxComponent>();
            box.Min = float3.zero;
            box.Max = new float3(1);
            Update();
            foreach (var surface in box.SurfaceDefinition.surfaces)
                surface.SetMaterial(material);
            box.OnValidate();
            Update();
            return model;
        }

        // The triangles drawn with the material, as (normal times twice its area)
        static List<Vector3> DrawnTriangles(ChiselModelComponent model, Material material)
        {
            var found = new List<Vector3>();
            foreach (var renderable in model.generated.renderables)
            {
                if (renderable == null || !renderable.Valid || !renderable.HasGeometry || renderable.meshRenderer.sharedMaterial != material)
                    continue;
                var mesh = renderable.sharedMesh;
                var vertices = mesh.vertices;
                for (int s = 0; s < mesh.subMeshCount; s++)
                {
                    var triangles = mesh.GetTriangles(s);
                    for (int t = 0; t + 2 < triangles.Length; t += 3)
                        found.Add(Vector3.Cross(vertices[triangles[t + 1]] - vertices[triangles[t]], vertices[triangles[t + 2]] - vertices[triangles[t]]));
                }
            }
            return found;
        }

        static int ColliderTriangles(ChiselModelComponent model)
        {
            var count = 0;
            foreach (var collider in model.generated.colliders)
            {
                if (collider != null && collider.sharedMesh)
                    count += collider.sharedMesh.triangles.Length / 3;
            }
            return count;
        }

        static double Area(List<Vector3> triangles)
        {
            var area = 0.0;
            foreach (var t in triangles)
                area += t.magnitude * 0.5;
            return area;
        }

        [Test]
        public void ADoubleSidedBox_IsDrawnFromBothSides()
        {
            var model = ModelWithABox();
            var triangles = DrawnTriangles(model, material);
            Assert.That(Area(triangles), Is.EqualTo(12.0).Within(1e-4), "each face twice");
            // every face is drawn facing out and facing in
            foreach (var axis in new[] { Vector3.right, Vector3.up, Vector3.forward })
            {
                foreach (var sign in new[] { 1, -1 })
                {
                    var facing = 0.0;
                    foreach (var t in triangles)
                    {
                        if (Vector3.Dot(t.normalized, axis * sign) > 0.999f)
                            facing += t.magnitude * 0.5;
                    }
                    Assert.That(facing, Is.EqualTo(2.0).Within(1e-4), $"facing {axis * sign}: a face of the box, and the back of the opposite one");
                }
            }
        }

        [Test]
        public void ADoubleSidedBox_CollidesOnce()
        {
            var model = ModelWithABox();
            Assert.That(ColliderTriangles(model), Is.EqualTo(12), "the back faces don't collide");
        }

        [Test]
        public void ASingleSidedBox_IsDrawnFromOutsideOnly()
        {
            metadata.outputFlags = SurfaceOutputFlags.Default;
            var model = ModelWithABox();
            Assert.That(Area(DrawnTriangles(model, material)), Is.EqualTo(6.0).Within(1e-4));
            Assert.That(ColliderTriangles(model), Is.EqualTo(12));
        }
    }
}
