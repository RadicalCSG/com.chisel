using System.Collections.Generic;
using System.Text;
using NUnit.Framework;
using UnityEngine;

namespace Chisel.Components.Tests
{
    [TestFixture]
    public class ChiselHierarchyEditTests
    {
        // A box of the default size that touches nothing draws two triangles per face
        const int kLoneBox = 12;

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

        ChiselModelComponent Model(string name)
        {
            var root = new GameObject(name);
            roots.Add(root);
            return root.AddComponent<ChiselModelComponent>();
        }

        static GameObject Child(string name, Transform parent, Vector3 localPosition)
        {
            var gameObject = new GameObject(name);
            gameObject.transform.SetParent(parent, false);
            gameObject.transform.localPosition = localPosition;
            return gameObject;
        }

        static GameObject Box(string name, Transform parent, Vector3 localPosition)
        {
            var gameObject = Child(name, parent, localPosition);
            gameObject.AddComponent<ChiselBoxComponent>();
            return gameObject;
        }

        static int RenderedTriangles(ChiselModelComponent model)
        {
            var count = 0;
            if (model.generated == null || model.generated.renderables == null)
                return count;
            foreach (var renderable in model.generated.renderables)
            {
                if (renderable == null || renderable.sharedMesh == null)
                    continue;
                for (int s = 0; s < renderable.sharedMesh.subMeshCount; s++)
                    count += (int)renderable.sharedMesh.GetIndexCount(s) / 3;
            }
            return count;
        }

        // The names of the Chisel children of a GameObject, in scene order
        static string SceneOrder(Transform parent)
        {
            var names = new StringBuilder();
            for (int i = 0; i < parent.childCount; i++)
            {
                var child = parent.GetChild(i);
                if (!child.TryGetComponent<ChiselNodeComponent>(out var node) || !node.isActiveAndEnabled)
                    continue;
                names.Append(names.Length > 0 ? "," : "").Append(child.name);
            }
            return names.ToString();
        }

        // The names of the components behind the model's top-level tree nodes, in tree order
        static string TreeOrder(ChiselModelComponent model)
        {
            var names = new StringBuilder();
            var components = model.GetComponentsInChildren<ChiselNodeComponent>(true);
            for (int i = 0; i < model.Node.Count; i++)
            {
                var node = model.Node[i];
                var name = "?";
                foreach (var component in components)
                {
                    if (component != model && component.TopTreeNode == node)
                    {
                        name = component.name;
                        break;
                    }
                }
                names.Append(names.Length > 0 ? "," : "").Append(name);
            }
            return names.ToString();
        }

        [Test]
        public void DeletingABrush_RebuildsTheBrushItTouched()
        {
            var model = Model("deletion");
            Box("wall", model.transform, Vector3.zero);
            var step = Box("step", model.transform, new Vector3(0.5f, 0.5f, 0.5f));
            Update();
            Assert.That(RenderedTriangles(model), Is.GreaterThan(kLoneBox), "the two boxes draw their union");

            Object.DestroyImmediate(step);
            Update();
            Assert.That(RenderedTriangles(model), Is.EqualTo(kLoneBox));
        }

        // The editor doesn't set the children of a composite that has none left, so after this delete no brush that
        // remains is flagged, and only the update itself can notice the removal
        [Test]
        public void DeletingTheOnlyBrushOfAComposite_RemovesIt()
        {
            var model = Model("composite deletion");
            Box("wall", model.transform, Vector3.zero);
            var steps = Child("steps", model.transform, Vector3.zero);
            steps.AddComponent<ChiselCompositeComponent>();
            var step = Box("step", steps.transform, new Vector3(0.5f, 0.5f, 0.5f));
            Update();
            Assert.That(RenderedTriangles(model), Is.GreaterThan(kLoneBox), "the two boxes draw their union");

            Object.DestroyImmediate(step);
            Update();
            Assert.That(RenderedTriangles(model), Is.EqualTo(kLoneBox));
        }

        // A Chisel parent refreshes its children's sibling indices when they change; a plain GameObject doesn't, so
        // there the tree order relies on the refresh the update does after an insert
        [Test]
        public void InsertingIntoAPlainGroup_KeepsTheSceneOrder()
        {
            var model = Model("plain group");
            var group = Child("group", model.transform, Vector3.zero);
            var p = Box("p", group.transform, Vector3.zero);
            Box("q", group.transform, new Vector3(3, 0, 0));
            Update();
            Assert.That(TreeOrder(model), Is.EqualTo(SceneOrder(group.transform)));

            var r = Box("r", group.transform, new Vector3(6, 0, 0));
            r.transform.SetSiblingIndex(1);
            Update();
            Assert.That(TreeOrder(model), Is.EqualTo(SceneOrder(group.transform)), "a brush inserted in the middle");

            // What Duplicate does: a copy right after the original
            var p2 = Object.Instantiate(p, group.transform);
            p2.name = "p2";
            p2.transform.SetSiblingIndex(1);
            Update();
            Assert.That(TreeOrder(model), Is.EqualTo(SceneOrder(group.transform)), "a duplicated brush");
        }

        [Test]
        public void ReorderingUnderAModel_KeepsTheSceneOrder()
        {
            var model = Model("reorder");
            Box("a", model.transform, Vector3.zero);
            var b = Box("b", model.transform, new Vector3(3, 0, 0));
            Box("c", model.transform, new Vector3(6, 0, 0));
            Update();

            b.transform.SetSiblingIndex(0);
            Update();
            Assert.That(TreeOrder(model), Is.EqualTo(SceneOrder(model.transform)));
        }
    }
}
