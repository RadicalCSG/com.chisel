using System.Collections.Generic;
using Chisel.Core;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;

namespace Chisel.Components.Tests
{
    // The normals Chisel draws with: flat, smoothed by the model's smoothing angle, or by the surfaces' smoothing groups
    [TestFixture]
    public class ChiselNormalSmoothingTests
    {
        const float kEpsilon = 1e-4f;

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

        ChiselModelComponent Model(float smoothingAngle = 0)
        {
            var root = new GameObject("smoothing test");
            roots.Add(root);
            var model = root.AddComponent<ChiselModelComponent>();
            if (smoothingAngle > 0)
                Smooth(model, smoothingAngle);
            return model;
        }

        static void Smooth(ChiselModelComponent model, float angle)
        {
            model.renderSettings.normalSmoothing      = angle > 0;
            model.renderSettings.normalSmoothingAngle = angle;
            model.SyncModelSettingsStore();
            Update();
        }

        static T Add<T>(ChiselModelComponent model, string name) where T : ChiselNodeComponent
        {
            var go = new GameObject(name);
            go.transform.SetParent(model.transform, false);
            return go.AddComponent<T>();
        }

        // In its own smoothing group by default, as a new cylinder is; group 0 leaves the smoothing to the model's angle
        static ChiselCylinderComponent Cylinder(ChiselModelComponent model, int sides = 16, uint smoothingGroup = 1)
        {
            var cylinder = Add<ChiselCylinderComponent>(model, "cylinder");
            cylinder.Sides = sides;
            cylinder.SmoothingGroup = smoothingGroup;
            cylinder.Diameter = 2;
            cylinder.Height = 2;
            Update();
            return cylinder;
        }

        static ChiselBoxComponent Box(ChiselModelComponent model, float3 min, float3 max)
        {
            var box = Add<ChiselBoxComponent>(model, "box");
            if (math.any(min > box.Max)) { box.Max = max; box.Min = min; }
            else                         { box.Min = min; box.Max = max; }
            Update();
            return box;
        }

        struct Vertex
        {
            public float3 position, normal;
            public float4 tangent;
        }

        // Every drawn vertex of the model, in the model's space
        static List<Vertex> Vertices(ChiselModelComponent model)
        {
            var vertices = new List<Vertex>();
            foreach (var renderable in model.generated.renderables)
            {
                if (renderable == null || renderable.sharedMesh == null)
                    continue;
                var mesh = renderable.sharedMesh;
                var positions = mesh.vertices; var normals = mesh.normals; var tangents = mesh.tangents;
                for (int i = 0; i < positions.Length; i++)
                    vertices.Add(new Vertex { position = positions[i], normal = normals[i], tangent = i < tangents.Length ? (float4)tangents[i] : float4.zero });
            }
            Assert.That(vertices, Is.Not.Empty, "the model draws nothing");
            return vertices;
        }

        static bool Same(float3 a, float3 b) => math.all(math.abs(a - b) < kEpsilon);

        // The cylinder's sides: what isn't facing up or down
        static bool OnTheSides(Vertex vertex) => math.abs(vertex.normal.y) < 1 - kEpsilon;

        // Where the sides meet: the vertices of the sides at one place, and whether they share their normal
        static int SeamsOnTheSides(List<Vertex> vertices)
        {
            var seams = 0;
            for (int i = 0; i < vertices.Count; i++)
            {
                if (!OnTheSides(vertices[i]))
                    continue;
                for (int j = i + 1; j < vertices.Count; j++)
                {
                    if (OnTheSides(vertices[j]) && Same(vertices[i].position, vertices[j].position) && !Same(vertices[i].normal, vertices[j].normal))
                        seams++;
                }
            }
            return seams;
        }

        [Test]
        public void WithoutSmoothing_EveryNormalIsItsFaces()
        {
            var model = Model();
            Box(model, new float3(-1, 0, -1), new float3(1, 2, 1));
            foreach (var vertex in Vertices(model))
                Assert.That(math.cmax(math.abs(vertex.normal)), Is.EqualTo(1).Within(kEpsilon), $"a box's normal along an axis: {vertex.normal}");
        }

        // 16 sides turn 22.5 degrees from one to the next; the caps are at 90 degrees from them
        [Test]
        public void ASmoothedCylinder_HasNoSeamsBetweenItsSides_AndHardEdgesAtItsCaps()
        {
            var model = Model(30);
            Cylinder(model, smoothingGroup: 0);
            var vertices = Vertices(model);
            Assert.That(SeamsOnTheSides(vertices), Is.Zero, "the sides meet with different normals");
            foreach (var vertex in vertices)
            {
                if (OnTheSides(vertex))
                {
                    // round: straight out from the axis
                    var outwards = math.normalize(new float3(vertex.position.x, 0, vertex.position.z));
                    Assert.That(math.dot(vertex.normal, outwards), Is.EqualTo(1).Within(1e-3f), $"a side's normal at {vertex.position}: {vertex.normal}");
                } else
                    Assert.That(math.abs(vertex.normal.y), Is.EqualTo(1).Within(kEpsilon), $"a cap's normal at {vertex.position}: {vertex.normal}");
            }
        }

        [Test]
        public void TheSmoothingAngle_KeepsSharperEdgesHard()
        {
            var model = Model(20);
            Cylinder(model, smoothingGroup: 0);
            Assert.That(SeamsOnTheSides(Vertices(model)), Is.GreaterThan(0), "22.5 degrees is sharper than 20, so the sides keep their edges");
        }

        [Test]
        public void TheSmoothingAngle_SmoothsSofterEdges()
        {
            var model = Model(25);
            Cylinder(model, smoothingGroup: 0);
            Assert.That(SeamsOnTheSides(Vertices(model)), Is.Zero, "22.5 degrees is softer than 25, so the sides are smoothed");
        }

        // The tangents of normal mapped materials are square to the smoothed normals
        [Test]
        public void TheTangents_FollowTheSmoothedNormals()
        {
            var model = Model(30);
            Cylinder(model, smoothingGroup: 0);
            foreach (var vertex in Vertices(model))
                Assert.That(math.dot(vertex.tangent.xyz, vertex.normal), Is.EqualTo(0).Within(1e-3f), $"the tangent at {vertex.position}: {vertex.tangent} for {vertex.normal}");
        }

        // A cylinder's surfaces are in a smoothing group (1), which its sides share
        [Test]
        public void ACylindersSmoothingGroup_SmoothsItsSides_WithoutTheModelsSmoothing()
        {
            var model = Model();
            var cylinder = Cylinder(model);
            Assert.That(cylinder.SmoothingGroup, Is.Not.Zero);
            var vertices = Vertices(model);
            Assert.That(SeamsOnTheSides(vertices), Is.Zero, "the sides of one smoothing group meet with different normals");
            // the caps aren't in the group
            foreach (var vertex in vertices)
            {
                if (!OnTheSides(vertex))
                    Assert.That(math.abs(vertex.normal.y), Is.EqualTo(1).Within(kEpsilon), $"a cap's normal at {vertex.position}: {vertex.normal}");
            }
        }

        // Without a smoothing group, the sides keep their own normals
        [Test]
        public void ACylinderWithoutASmoothingGroup_KeepsItsEdges()
        {
            var model = Model();
            Cylinder(model, smoothingGroup: 0);
            Assert.That(SeamsOnTheSides(Vertices(model)), Is.GreaterThan(0));
        }

        // A smoothing group is the brush's own: another brush's surfaces in a group of the same number don't join it
        [Test]
        public void ASmoothingGroup_DoesntReachAnotherBrush()
        {
            var model = Model();
            Cylinder(model);
            var alone = Vertices(model).FindAll(OnTheSides);
            // a narrowing cylinder on top, its sides in group 1 as well and leaning in (their normals up), standing on the
            // first one's top edge
            var above = Cylinder(model);
            above.Type = CylinderShapeType.ConicalFrustum;
            above.TopDiameterX = 1;
            above.TopDiameterZ = 1;
            above.transform.localPosition = new Vector3(0, 2, 0);
            ChiselNodeHierarchyManager.UpdateTreeNodeTransformation(above);
            Update();
            // the first cylinder's sides are as they were by themselves, where the other one stands on them too
            var together = Vertices(model);
            Assert.That(alone.Exists(vertex => vertex.position.y > 2 - kEpsilon), Is.True, "no side vertices where the cylinders meet");
            foreach (var vertex in alone)
                Assert.That(together.Exists(other => Same(other.position, vertex.position) && Same(other.normal, vertex.normal)), Is.True,
                            $"the first cylinder's side at {vertex.position} no longer has its normal {vertex.normal}");
        }

        // Only surfaces that meet a vertex smooth it, not every surface whose plane happens to go through it
        [Test]
        public void ASurfaceFarAway_DoesntBendANormal()
        {
            var model = Model(70);
            Box(model, new float3(-0.5f, -0.5f, -0.5f), new float3(0.5f, 0.5f, 0.5f));
            // another box, turned 30 degrees and 10 units away, whose right side's plane goes through the first one's corner
            var turned = Box(model, new float3(-0.5f, -0.5f, -0.5f), new float3(0.5f, 0.5f, 0.5f));
            var rotation = Quaternion.Euler(0, 0, 30);
            var normal   = (float3)(rotation * Vector3.right);
            var corner   = new float3(0.5f, 0.5f, 0.5f);
            turned.transform.localRotation = rotation;
            turned.transform.localPosition = (Vector3)(corner - normal * 0.5f + new float3(0, 0, 10));
            ChiselNodeHierarchyManager.UpdateTreeNodeTransformation(turned);
            Update();
            foreach (var vertex in Vertices(model))
            {
                if (vertex.position.z > 2)
                    continue;
                Assert.That(math.cmax(math.abs(vertex.normal)), Is.EqualTo(1).Within(kEpsilon), $"the first box's normal at {vertex.position}: {vertex.normal}");
            }
        }

        // Rebuilding one brush gives the normals rebuilding all of them does
        [Test]
        public void AnIncrementalRebuild_GivesTheNormalsOfAFullOne()
        {
            var model = Model(30);
            Cylinder(model);
            var box = Box(model, new float3(0.5f, -1, -0.25f), new float3(2, 3, 0.25f));
            box.Max = new float3(2.5f, 3, 0.25f);
            Update();
            var incremental = Vertices(model);
            ChiselNodeHierarchyManager.Rebuild();
            Update();
            var full = Vertices(model);
            Assert.That(full.Count, Is.EqualTo(incremental.Count), "the same vertices");
            foreach (var vertex in full)
            {
                var found = incremental.Exists(other => Same(other.position, vertex.position) && Same(other.normal, vertex.normal));
                Assert.That(found, Is.True, $"after a full rebuild, the normal at {vertex.position} is {vertex.normal}, which the incremental rebuild didn't give");
            }
        }
    }
}
