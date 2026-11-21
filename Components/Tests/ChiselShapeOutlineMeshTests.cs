using System.Collections.Generic;
using Chisel.Core;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;

namespace Chisel.Components.Tests
{
    // The outline of a generator's shape (ChiselShapeOutline) is of the shape Chisel builds from it: on its own in a
    // model, every line of it lies on the mesh drawn for it, with what its subtractive and intersecting brushes take away
    [TestFixture]
    public class ChiselShapeOutlineMeshTests
    {
        readonly List<GameObject> roots = new();
        (UnityEngine.SceneManagement.Scene scene, bool wasDirty) remembered;

        [SetUp]
        public void SetUp() { remembered = SceneLeftClean.Remember(); }

        [TearDown]
        public void TearDown()
        {
            foreach (var root in roots)
                if (root) Object.DestroyImmediate(root);
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

        static float DistanceToTriangle(float3 p, float3 a, float3 b, float3 c)
        {
            // Ericson, closest point on a triangle
            float3 ab = b - a, ac = c - a, ap = p - a;
            float d1 = math.dot(ab, ap), d2 = math.dot(ac, ap);
            if (d1 <= 0 && d2 <= 0) return math.distance(p, a);
            float3 bp = p - b;
            float d3 = math.dot(ab, bp), d4 = math.dot(ac, bp);
            if (d3 >= 0 && d4 <= d3) return math.distance(p, b);
            float vc = d1 * d4 - d3 * d2;
            if (vc <= 0 && d1 >= 0 && d3 <= 0) return math.distance(p, a + ab * (d1 / (d1 - d3)));
            float3 cp = p - c;
            float d5 = math.dot(ab, cp), d6 = math.dot(ac, cp);
            if (d6 >= 0 && d5 <= d6) return math.distance(p, c);
            float vb = d5 * d2 - d1 * d6;
            if (vb <= 0 && d2 >= 0 && d6 <= 0) return math.distance(p, a + ac * (d2 / (d2 - d6)));
            float va = d3 * d6 - d5 * d4;
            if (va <= 0 && (d4 - d3) >= 0 && (d5 - d6) >= 0) return math.distance(p, b + (c - b) * ((d4 - d3) / ((d4 - d3) + (d5 - d6))));
            float denom = 1 / (va + vb + vc);
            return math.distance(p, a + ab * (vb * denom) + ac * (vc * denom));
        }

        static ChiselGeneratorComponent Make(string name, Transform parent)
        {
            switch (name)
            {
                case "linear stairs":  return Add<ChiselLinearStairsComponent>(parent);
                case "spiral stairs":  return Add<ChiselSpiralStairsComponent>(parent);
                case "smooth spiral stairs":
                {
                    var stairs = Add<ChiselSpiralStairsComponent>(parent);
                    stairs.RiserType = StairsRiserType.Smooth;
                    return stairs;
                }
                case "torus":          return Add<ChiselTorusComponent>(parent);
                case "revolved shape": return Add<ChiselRevolvedShapeComponent>(parent);
                case "pathed stairs":  return Add<ChiselPathedStairsComponent>(parent);
                default:               return null;
            }
        }

        static T Add<T>(Transform parent) where T : ChiselGeneratorComponent
        {
            var generator = new GameObject(typeof(T).Name).AddComponent<T>();
            generator.transform.SetParent(parent, false);
            return generator;
        }

        static IEnumerable<string> Generators()
        {
            return new[] { "linear stairs", "spiral stairs", "smooth spiral stairs", "torus", "revolved shape", "pathed stairs" };
        }

        [TestCaseSource(nameof(Generators))]
        public void EveryLineOfAGeneratorsShape_IsOnItsMesh(string name)
        {
            var model = new GameObject("model").AddComponent<ChiselModelComponent>();
            roots.Add(model.gameObject);
            var generator = Make(name, model.transform);
            generator.OnValidate();
            Update();
            Assert.That(generator.TopTreeNode.Type, Is.EqualTo(CSGNodeType.Branch), "built from several brushes");

            var triangles = new List<float3>();
            foreach (var renderable in model.generated.renderables)
            {
                if (renderable == null || !renderable.Valid || !renderable.HasGeometry || (renderable.query & SurfaceDestinationFlags.Renderable) == 0)
                    continue;
                var vertices = renderable.sharedMesh.vertices;
                foreach (var index in renderable.sharedMesh.triangles)
                    triangles.Add(vertices[index]);
            }
            Assert.That(triangles, Is.Not.Empty, "drawn");

            var outlineVertices = new List<float3>();
            var lines           = new List<int>();
            ChiselShapeOutline.Build(generator.TopTreeNode, outlineVertices, lines);
            Assert.That(lines, Is.Not.Empty, "its outline");
            var toModel = generator.TopTreeNode.NodeToTreeSpaceMatrix;

            for (int l = 0; l < lines.Count; l += 2)
            {
                var a = math.transform(toModel, outlineVertices[lines[l]]);
                var b = math.transform(toModel, outlineVertices[lines[l + 1]]);
                foreach (var t in new[] { 0.25f, 0.5f, 0.75f })
                {
                    var point   = math.lerp(a, b, t);
                    var nearest = float.MaxValue;
                    for (int i = 0; i < triangles.Count && nearest > 1e-3f; i += 3)
                        nearest = math.min(nearest, DistanceToTriangle(point, triangles[i], triangles[i + 1], triangles[i + 2]));
                    Assert.That(nearest, Is.LessThan(1e-3f), $"{name}: the line from {a} to {b}, at {point}, isn't on its mesh");
                }

                // and where the mesh turns: not inside a flat part of it
                var middle  = math.lerp(a, b, 0.5f);
                var normals = new List<float3>();
                for (int i = 0; i < triangles.Count; i += 3)
                {
                    if (DistanceToTriangle(middle, triangles[i], triangles[i + 1], triangles[i + 2]) > 1e-3f)
                        continue;
                    var normal = math.normalizesafe(math.cross(triangles[i + 1] - triangles[i], triangles[i + 2] - triangles[i]));
                    if (math.lengthsq(normal) > 0)
                        normals.Add(normal);
                }
                var turns = false;
                for (int n = 1; n < normals.Count && !turns; n++)
                    turns = math.dot(normals[0], normals[n]) < 0.9999f;
                Assert.That(turns, Is.True, $"{name}: the line from {a} to {b} is inside a flat part of its mesh");
            }
        }
    }
}
