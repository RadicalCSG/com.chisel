using System.Collections.Generic;
using Chisel.Core;
using NUnit.Framework;
using UnityEngine;

namespace Chisel.Components.Tests
{
    // Every polygon a generator makes is one of the surfaces of its definition, which the surface tools edit
    [TestFixture]
#if UNITY_6000_7_OR_NEWER
    [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
#endif
    public class ChiselGeneratorSurfacesTests
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

        static readonly System.Type[] kGenerators =
        {
            typeof(ChiselBoxComponent), typeof(ChiselBrushComponent), typeof(ChiselCapsuleComponent), typeof(ChiselCylinderComponent),
            typeof(ChiselExtrudedShapeComponent), typeof(ChiselHemisphereComponent), typeof(ChiselLinearStairsComponent),
            typeof(ChiselPathedStairsComponent), typeof(ChiselRevolvedShapeComponent), typeof(ChiselSphereComponent),
            typeof(ChiselSpiralStairsComponent), typeof(ChiselStadiumComponent), typeof(ChiselTorusComponent)
        };

        static void CheckSurfaces(ChiselGeneratorComponent generator, CSGTreeNode node, ref int checkedCount)
        {
            if (node.Type == CSGNodeType.Brush)
            {
                var brush = (CSGTreeBrush)node;
                var blob = BrushMeshManager.GetBrushMeshBlob(brush.BrushMesh.BrushMeshID);
                ref var polygons = ref blob.Value.polygons;
                for (int p = 0; p < polygons.Length; p++)
                {
                    Assert.That(generator.GetSurface(polygons[p].descriptionIndex), Is.Not.Null,
                                $"{generator.GetType().Name}: polygon {p} of a brush is surface {polygons[p].descriptionIndex}, which its definition doesn't have");
                    checkedCount++;
                }
                return;
            }
            var branch = (CSGTreeBranch)node;
            for (int i = 0; i < branch.Count; i++)
                CheckSurfaces(generator, branch[i], ref checkedCount);
        }

        [TestCaseSource(nameof(kGenerators))]
        public void EveryPolygon_IsASurfaceOfTheDefinition(System.Type type)
        {
            var root = new GameObject("generator surfaces test");
            roots.Add(root);
            root.AddComponent<ChiselModelComponent>();
            var go = new GameObject(type.Name);
            go.transform.SetParent(root.transform, false);
            var generator = (ChiselGeneratorComponent)go.AddComponent(type);
            Update();

            var checkedCount = 0;
            CheckSurfaces(generator, generator.TopTreeNode, ref checkedCount);
            Assert.That(checkedCount, Is.GreaterThan(0), $"{type.Name} made no polygons");
        }

        // A brush added as a component, like any generator, has a shape: a cube like a new box
        [Test]
        public void APlainBrushAddedAsAComponent_IsACube()
        {
            var root = new GameObject("generator surfaces test");
            roots.Add(root);
            var model = root.AddComponent<ChiselModelComponent>();
            var go = new GameObject("brush");
            go.transform.SetParent(root.transform, false);
            var brush = go.AddComponent<ChiselBrushComponent>();
            Update();

            Assert.That(brush.TopTreeNode.Valid, Is.True, "the brush is in the model");
            var blob = BrushMeshManager.GetBrushMeshBlob(((CSGTreeBrush)brush.TopTreeNode).BrushMesh.BrushMeshID);
            Assert.That(blob.IsCreated, Is.True, "the brush has a brush mesh");
            Assert.That(blob.Value.polygons.Length, Is.EqualTo(6), "a cube's six faces");
            var bounds = new Bounds();
            var first = true;
            foreach (var renderable in model.generated.renderables)
            {
                if (renderable == null || !renderable.sharedMesh || renderable.sharedMesh.vertexCount == 0)
                    continue;
                if (first) bounds = renderable.sharedMesh.bounds; else bounds.Encapsulate(renderable.sharedMesh.bounds);
                first = false;
            }
            Assert.That(first, Is.False, "the model draws the brush");
            Assert.That(Vector3.Distance(bounds.min, Vector3.zero), Is.LessThan(1e-4f), $"from (0, 0, 0), not {bounds.min}");
            Assert.That(Vector3.Distance(bounds.max, Vector3.one), Is.LessThan(1e-4f), $"to (1, 1, 1), not {bounds.max}");
        }
    }
}
