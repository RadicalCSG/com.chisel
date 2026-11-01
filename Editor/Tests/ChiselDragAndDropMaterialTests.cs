using System.Collections.Generic;
using System.Linq;
using Chisel.Components;
using Chisel.Core;
using NUnit.Framework;
using UnityEngine;

namespace Chisel.Editors.Tests
{
    // Dragging a material over surfaces shows it there, and puts the old materials back when the drag moves on or is
    // cancelled
    [TestFixture]
    public class ChiselDragAndDropMaterialTests
    {
        readonly List<Object> created = new();

        [SetUp]
        public void SetUp()
        {
            if (Object.FindObjectsByType<ChiselModelComponent>(FindObjectsInactive.Include).Length > 0)
                Assert.Ignore("A Chisel model is loaded; run these in a scene without one");
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var obj in created)
            {
                if (obj)
                    Object.DestroyImmediate(obj);
            }
            created.Clear();
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

        // Every surface of every brush the generator makes, several of them often made from one surface of its definition
        List<SurfaceReference> AllSurfaces(System.Type type, out ChiselGeneratorComponent generator)
        {
            var root = new GameObject("drag material test");
            created.Add(root);
            root.AddComponent<ChiselModelComponent>();
            var go = new GameObject(type.Name);
            go.transform.SetParent(root.transform, false);
            generator = (ChiselGeneratorComponent)go.AddComponent(type);
            Update();

            var surfaces = new List<SurfaceReference>();
            AddSurfaces(generator, generator.TopTreeNode, surfaces);
            Assert.That(surfaces, Is.Not.Empty, $"{type.Name} made no surfaces");
            return surfaces;
        }

        static void AddSurfaces(ChiselGeneratorComponent generator, CSGTreeNode node, List<SurfaceReference> surfaces)
        {
            if (node.Type == CSGNodeType.Brush)
            {
                var brush = (CSGTreeBrush)node;
                var blob = BrushMeshManager.GetBrushMeshBlob(brush.BrushMesh.BrushMeshID);
                for (int p = 0; p < blob.Value.polygons.Length; p++)
                    surfaces.Add(new SurfaceReference(generator, blob.Value.polygons[p].descriptionIndex, brush, p));
                return;
            }
            var branch = (CSGTreeBranch)node;
            for (int i = 0; i < branch.Count; i++)
                AddSurfaces(generator, branch[i], surfaces);
        }

        [Test]
        public void TheStairs_ShareSurfacesOfTheirDefinition()
        {
            var surfaces = AllSurfaces(typeof(ChiselLinearStairsComponent), out _);
            Assert.That(surfaces.Select(s => s.descriptionIndex).Distinct().Count(), Is.LessThan(surfaces.Count),
                        "the test below needs surfaces that share one");
        }

        [TestCaseSource(nameof(kGenerators))]
        public void DraggingOverEverySurfaceAndAway_PutsTheirMaterialsBack(System.Type type)
        {
            var surfaces = AllSurfaces(type, out _);
            var originals = surfaces.Select(s => s.RenderMaterial).ToArray();
            var dragged  = new Material(Shader.Find("Hidden/InternalErrorShader")) { name = "dragged" };
            created.Add(dragged);

            var drag = new ChiselDragAndDropMaterial(dragged);
            drag.ApplyMaterialToSurface(surfaces.ToArray());
            foreach (var surface in surfaces.Where(s => s.Surface != null))
                Assert.That(surface.RenderMaterial, Is.SameAs(dragged), "the dragged material is shown");

            drag.UndoPrevSurfaceReference();
            for (int i = 0; i < surfaces.Count; i++)
                Assert.That(surfaces[i].RenderMaterial, Is.SameAs(originals[i]), $"the material surface {i} had");
        }
    }
}
