using System.Collections.Generic;
using Chisel.Core;
using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;

namespace Chisel.Components.Tests
{
    // What the hotspot sections' cut leaves of a closed model is still closed: every edge drawn is drawn by exactly two
    // triangles with corners at exactly the same positions, also where surfaces that are cut apart from each other meet
    [TestFixture]
    public class HotspotWatertightTests
    {
        const string kFolder = "Assets/HotspotWatertightTests";

        readonly List<GameObject> roots = new();
        ChiselHotspotLayout layout;
        Material material;

        [SetUp]
        public void SetUp()
        {
            if (Object.FindObjectsByType<ChiselModelComponent>(FindObjectsInactive.Include).Length > 0)
                Assert.Ignore("A Chisel model is loaded; run these in a scene without one");

            // one bordered tile, so every surface is cut along its border and its repeats
            layout = ScriptableObject.CreateInstance<ChiselHotspotLayout>();
            layout.size = new Vector2Int(2048, 2048);
            layout.pixelsPerUnit = 256;
            layout.tiles = new List<HotspotTile>
            {
                new() { id = 1, name = "B1", border = 32,
                        points = new[] { new Vector2Int(1024, 0), new Vector2Int(2048, 0), new Vector2Int(2048, 512), new Vector2Int(1024, 512) } }
            };
            if (!AssetDatabase.IsValidFolder(kFolder))
                AssetDatabase.CreateFolder("Assets", kFolder.Substring("Assets/".Length));
            material = new Material(ChiselProjectSettings.DefaultWallMaterial);
            AssetDatabase.CreateAsset(material, kFolder + "/hotspot.mat");
            material.AddMetadataOfType<ChiselSurfaceMetadata>().hotspotLayout = layout;
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
            if (layout)
                Object.DestroyImmediate(layout);
        }

        static void Update()
        {
            for (int i = 0; i < 3; i++)
            {
                ChiselNodeHierarchyManager.Update();
                ChiselModelManager.Instance.UpdateModels();
            }
        }

        ChiselModelComponent Model()
        {
            var root = new GameObject("hotspot watertight test");
            roots.Add(root);
            return root.AddComponent<ChiselModelComponent>();
        }

        ChiselBoxComponent Box(ChiselModelComponent model, float3 min, float3 max, bool hotspot = true)
        {
            var go = new GameObject("box");
            go.transform.SetParent(model.transform, false);
            var box = go.AddComponent<ChiselBoxComponent>();
            if (math.any(min > box.Max)) { box.Max = max; box.Min = min; }
            else                         { box.Min = min; box.Max = max; }
            Update();
            if (!hotspot)
                return box;
            foreach (var surface in box.SurfaceDefinition.surfaces)
                surface.SetMaterial(material);
            box.OnValidate();
            Update();
            return box;
        }

        // Every triangle the model draws, its corners merged only where they are the same
        static MeshManifoldReport Classify(ChiselModelComponent model, out int triangleCount)
        {
            var vertices = new List<float3>();
            var indices  = new List<int>();
            foreach (var renderable in model.generated.renderables)
            {
                if (renderable == null || renderable.sharedMesh == null)
                    continue;
                var mesh = renderable.sharedMesh;
                var meshVertices = mesh.vertices;
                var offset = vertices.Count;
                foreach (var v in meshVertices)
                    vertices.Add(v);
                foreach (var i in mesh.triangles)
                    indices.Add(offset + i);
            }
            triangleCount = indices.Count / 3;
            using var vertexArray = new NativeArray<float3>(vertices.ToArray(), Allocator.Temp);
            using var indexArray  = new NativeArray<int>(indices.ToArray(), Allocator.Temp);
            return MeshManifoldValidation.Classify(vertexArray, indexArray, 0);
        }

        // The edges only one triangle draws, each with any corner that lies on it (a T-junction) or "gap"; or with many,
        // the edges more than two draw
        static string OpenEdges(ChiselModelComponent model, bool many = false)
        {
            var points = new List<float3>();
            var uses   = new Dictionary<(float3, float3), int>();
            foreach (var renderable in model.generated.renderables)
            {
                if (renderable == null || renderable.sharedMesh == null)
                    continue;
                var mesh = renderable.sharedMesh;
                var v = mesh.vertices; var t = mesh.triangles;
                foreach (var p in v) points.Add(p);
                for (int i = 0; i < t.Length; i += 3)
                {
                    for (int k = 0; k < 3; k++)
                    {
                        float3 a = v[t[i + k]], b = v[t[i + (k + 1) % 3]];
                        var key = Less(a, b) ? (a, b) : (b, a);
                        uses.TryGetValue(key, out var n);
                        uses[key] = n + 1;
                    }
                }
            }
            var sb = new System.Text.StringBuilder();
            var shown = 0;
            foreach (var (edge, n) in uses)
            {
                if ((many ? n <= 2 : n != 1) || shown++ >= 12)
                    continue;
                var (a, b) = edge;
                if (many)
                {
                    sb.Append($"\n  {a} - {b}: {n}");
                    continue;
                }
                var on = "gap";
                foreach (var p in points)
                {
                    if (p.Equals(a) || p.Equals(b))
                        continue;
                    var ab = (double3)b - a; var ap = (double3)p - a;
                    var t = math.dot(ap, ab) / math.dot(ab, ab);
                    if (t > 0 && t < 1 && math.length(ap - ab * t) < 1e-5)
                    {
                        on = $"T at {p}";
                        break;
                    }
                }
                sb.Append($"\n  {a} - {b}: {on}");
            }
            return sb.ToString();
        }

        static bool Less(float3 a, float3 b) => a.x != b.x ? a.x < b.x : (a.y != b.y ? a.y < b.y : a.z < b.z);

        static void AssertClosed(ChiselModelComponent model, bool cut = true)
        {
            var report = Classify(model, out var triangleCount);
            if (cut)
                Assert.That(triangleCount, Is.GreaterThan(12), "the surfaces were not cut");
            Assert.That(report.degenerateTriangleCount, Is.Zero, "triangles without area");
            Assert.That(report.nonManifoldEdgeCount, Is.Zero, $"edges drawn by more than two triangles:{OpenEdges(model, many: true)}");
            Assert.That(report.boundaryEdgeCount, Is.Zero,
                        $"edges drawn by one triangle, {report.boundaryEdgeLength} long in all:{OpenEdges(model)}");
        }

        [Test]
        public void ABox_IsClosed()
        {
            var model = Model();
            Box(model, new float3(0, 0, 0), new float3(3, 1, 2));
            AssertClosed(model);
        }

        [Test]
        public void TwoBoxesSideBySide_AreClosed()
        {
            var model = Model();
            Box(model, new float3(0, 0, 0), new float3(2, 1, 3));
            Box(model, new float3(2, 0, 0), new float3(5, 1, 3));
            AssertClosed(model);
        }

        // where one box's top meets the other's wall, two surfaces that aren't one plane are cut apart
        [Test]
        public void AStep_IsClosed()
        {
            var model = Model();
            Box(model, new float3(0, 0, 0), new float3(2, 1, 3));
            Box(model, new float3(2, 0, 0), new float3(4, 2.3f, 3));
            AssertClosed(model);
        }

        [Test]
        public void RandomBoxes_AreClosed([NUnit.Framework.Range(0, 59)] int seed, [Values] bool hotspot)
        {
            var random = new Unity.Mathematics.Random((uint)(seed * 104729 + 7));
            var model  = Model();
            var count  = 2 + seed % 3;
            for (int b = 0; b < count; b++)
            {
                // each on the one before it or beside it, so they make one solid
                var min  = new float3(random.NextFloat(-1, 1), random.NextFloat(-0.5f, 0.5f), random.NextFloat(-1, 1));
                var size = new float3(random.NextFloat(1.2f, 4), random.NextFloat(0.7f, 3), random.NextFloat(1.2f, 4));
                Box(model, min, min + size, hotspot);
            }
            AssertClosed(model, hotspot);
        }

        // Turned every way, so no crease or section line lies along an axis
        [Test]
        public void RandomTurnedBoxes_AreClosed([NUnit.Framework.Range(0, 59)] int seed)
        {
            var random = new Unity.Mathematics.Random((uint)(seed * 7907 + 3));
            var model  = Model();
            var count  = 2 + seed % 4;
            for (int b = 0; b < count; b++)
            {
                var min  = new float3(random.NextFloat(-1, 1), random.NextFloat(-0.5f, 0.5f), random.NextFloat(-1, 1));
                var size = new float3(random.NextFloat(1.2f, 4), random.NextFloat(0.7f, 3), random.NextFloat(1.2f, 4));
                var box  = Box(model, min, min + size);
                box.transform.rotation = quaternion.Euler(random.NextFloat3(-0.6f, 0.6f));
                Update();
            }
            AssertClosed(model);
        }
    }
}
