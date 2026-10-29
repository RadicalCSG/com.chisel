using System.Collections.Generic;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;

namespace Chisel.Components.Tests
{
    // A sphere's uvs are one map of it: the texture goes on from one face to the next
    [TestFixture]
    public class ChiselSphereUVTests
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

        // The largest difference between the uvs two faces give a corner they share, away from the poles (where every
        // face has its own longitude) and from where the map is cut (behind the sphere)
        float LargestJump(float3 diameter, out string where)
        {
            var root = new GameObject("sphere uv test");
            roots.Add(root);
            var model = root.AddComponent<ChiselModelComponent>();
            var go = new GameObject("sphere");
            go.transform.SetParent(model.transform, false);
            var sphere = go.AddComponent<ChiselSphereComponent>();
            sphere.DiameterXYZ = diameter;
            Update();

            var positions = new List<float3>();
            var uvs = new List<float2>();
            foreach (var renderable in model.generated.renderables)
            {
                if (renderable == null || renderable.sharedMesh == null)
                    continue;
                var mesh = renderable.sharedMesh;
                var vertices = mesh.vertices; var meshUVs = mesh.uv;
                for (int i = 0; i < vertices.Length; i++) { positions.Add(vertices[i]); uvs.Add(meshUVs[i]); }
            }
            Assert.That(positions, Is.Not.Empty);
            var centre = new float3(0, diameter.y * 0.5f, 0);

            var largest = 0.0f; where = "";
            for (int i = 0; i < positions.Count; i++)
            {
                var offset = positions[i] - centre;
                var latitude  = math.degrees(math.atan2(offset.y / (diameter.y * 0.5f), math.length(offset.xz) / (diameter.x * 0.5f)));
                var longitude = math.degrees(math.atan2(offset.z, offset.x));
                if (math.abs(latitude) > 89 || math.abs(longitude) > 150)
                    continue;
                for (int j = i + 1; j < positions.Count; j++)
                {
                    if (math.lengthsq(positions[i] - positions[j]) > 1e-8f)
                        continue;
                    var jump = math.length(uvs[i] - uvs[j]);
                    if (jump > largest) { largest = jump; where = $"at {positions[i]}: {uvs[i]} and {uvs[j]}"; }
                }
            }
            return largest;
        }

        [Test]
        public void TheFacesOfASphere_MeetInItsUVs()
        {
            var jump = LargestJump(new float3(1), out var where);
            Assert.That(jump, Is.LessThan(1e-4f), $"the texture jumps where faces meet, the most {where}");
        }

        [Test]
        public void TheFacesOfAStretchedSphere_MeetInItsUVs()
        {
            var jump = LargestJump(new float3(2, 1, 2), out var where);
            Assert.That(jump, Is.LessThan(1e-4f), $"the texture jumps where faces meet, the most {where}");
        }
    }
}
