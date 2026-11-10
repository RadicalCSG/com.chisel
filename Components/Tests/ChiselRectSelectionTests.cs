using System.Collections.Generic;
using Chisel.Core;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Chisel.Components.Tests
{
    // Rect selection finds a generator only when all of its visible brushes are inside the rectangle
    [TestFixture]
    public class ChiselRectSelectionTests
    {
        readonly List<GameObject> roots = new();
        (Scene scene, bool wasDirty) remembered;

        [SetUp]
        public void SetUp()
        {
            remembered = SceneLeftClean.Remember();
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

        // The six planes around a box, facing out: what a rectangle's frustum is in the tree
        static Plane[] Around(Vector3 min, Vector3 max)
        {
            return new[]
            {
                new Plane(Vector3.right,   -max.x), new Plane(Vector3.left,    min.x),
                new Plane(Vector3.up,      -max.y), new Plane(Vector3.down,    min.y),
                new Plane(Vector3.forward, -max.z), new Plane(Vector3.back,    min.z)
            };
        }

        (ChiselModelComponent model, ChiselLinearStairsComponent stairs, ChiselBoxComponent box) Scene()
        {
            var model = new GameObject("model").AddComponent<ChiselModelComponent>();
            roots.Add(model.gameObject);
            var stairs = new GameObject("stairs").AddComponent<ChiselLinearStairsComponent>();
            stairs.transform.SetParent(model.transform, false);
            var box = new GameObject("box").AddComponent<ChiselBoxComponent>();
            box.transform.SetParent(model.transform, false);
            box.transform.localPosition = new Vector3(10, 0, 0);
            Update();
            return (model, stairs, box);
        }

        static HashSet<string> Found(ChiselModelComponent model, Plane[] planes)
        {
            var queries = ChiselSceneQuery.GetVisibleQueries(ChiselSceneQuery.GetMeshQuery(model), SurfaceDestinationFlags.Renderable);
            var names = new HashSet<string>();
            var found = ChiselSceneQuery.GetNodesInFrustum(model.Node, queries, planes);
            if (found == null)
                return names;
            foreach (var node in found)
            {
                var component = Resources.EntityIdToObject(node.EntityId) as ChiselNodeComponent;
                if (component)
                    names.Add(component.name);
            }
            return names;
        }

        [Test]
        public void ARectangleAroundEverything_FindsTheGeneratorAndTheBox()
        {
            var (model, _, _) = Scene();
            var found = Found(model, Around(new Vector3(-100, -100, -100), new Vector3(100, 100, 100)));
            Assert.That(found, Is.EquivalentTo(new[] { "stairs", "box" }));
        }

        [Test]
        public void ARectangleAroundOneOfItsBrushes_DoesntFindTheGenerator()
        {
            var (model, stairs, _) = Scene();
            var brushes = new List<CSGTreeBrush>();
            ChiselGeneratorParts.BrushesOf(stairs, brushes);
            Assert.That(brushes.Count, Is.GreaterThan(1), "the stairs are made of one brush");

            var bounds = brushes[0].Bounds;
            var margin = new Vector3(0.01f, 0.01f, 0.01f);
            var found = Found(model, Around((Vector3)bounds.Min - margin, (Vector3)bounds.Max + margin));
            Assert.That(found, Does.Not.Contain("stairs"));
        }
    }
}
