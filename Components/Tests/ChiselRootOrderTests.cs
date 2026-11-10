using System.Collections.Generic;
using System.Text;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Chisel.Components.Tests
{
    // Nodes with no Chisel parent (at the scene root, or under plain GameObjects) go into the scene's default model, in
    // hierarchy order: reordering them reorders the CSG as it does under a model
    [TestFixture]
    public class ChiselRootOrderTests
    {
        readonly List<ChiselNodeComponent> nodes = new();
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
            nodes.Clear();
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

        // What the editor does when the hierarchy changed, then the update
        static void HierarchyChanged()
        {
            ChiselNodeHierarchyManager.CheckOrderOfChildNodesModifiedOfNonNodeGameObject();
            Update();
        }

        GameObject Box(string name, Transform parent, Vector3 position)
        {
            var gameObject = new GameObject(name);
            if (parent == null)
                roots.Add(gameObject);
            gameObject.transform.SetParent(parent, false);
            gameObject.transform.position = position;
            nodes.Add(gameObject.AddComponent<ChiselBoxComponent>());
            return gameObject;
        }

        // The names of the default model's top-level tree nodes made by this test's boxes, in tree order
        string TreeOrder()
        {
            var model = nodes[0].hierarchyItem.Model;
            Assert.That(model, Is.Not.Null, "the boxes are in no model");
            var names = new StringBuilder();
            for (int i = 0; i < model.Node.Count; i++)
            {
                var treeNode = model.Node[i];
                foreach (var node in nodes)
                {
                    if (node && node.TopTreeNode == treeNode)
                    {
                        names.Append(names.Length > 0 ? "," : "").Append(node.name);
                        break;
                    }
                }
            }
            return names.ToString();
        }

        [Test]
        public void ReorderingAtTheSceneRoot_ReordersTheCSG()
        {
            Box("a", null, Vector3.zero);
            var b = Box("b", null, new Vector3(3, 0, 0));
            Box("c", null, new Vector3(6, 0, 0));
            Update();
            Assert.That(TreeOrder(), Is.EqualTo("a,b,c"));

            b.transform.SetSiblingIndex(0);
            HierarchyChanged();
            Assert.That(TreeOrder(), Is.EqualTo("b,a,c"));
        }

        // A hierarchy change that moved nothing leaves the nodes as they are, instead of making their tree nodes again
        [Test]
        public void AHierarchyChangeThatReordersNothing_KeepsTheNodes()
        {
            var a = Box("a", null, Vector3.zero);
            var group = new GameObject("group");
            roots.Add(group);
            var b = Box("b", group.transform, new Vector3(3, 0, 0));
            Update();
            var aNode = a.GetComponent<ChiselNodeComponent>().TopTreeNode;
            var bNode = b.GetComponent<ChiselNodeComponent>().TopTreeNode;

            HierarchyChanged();
            Assert.That(a.GetComponent<ChiselNodeComponent>().TopTreeNode, Is.EqualTo(aNode));
            Assert.That(b.GetComponent<ChiselNodeComponent>().TopTreeNode, Is.EqualTo(bNode));
        }

        [Test]
        public void ReorderingUnderAPlainGameObjectInAModel_ReordersTheCSG()
        {
            var model = new GameObject("model").AddComponent<ChiselModelComponent>();
            roots.Add(model.gameObject);
            var group = new GameObject("group");
            group.transform.SetParent(model.transform, false);
            Box("a", group.transform, Vector3.zero);
            var b = Box("b", group.transform, new Vector3(3, 0, 0));
            Box("c", group.transform, new Vector3(6, 0, 0));
            Update();
            Assert.That(TreeOrder(), Is.EqualTo("a,b,c"));

            b.transform.SetSiblingIndex(0);
            HierarchyChanged();
            Assert.That(TreeOrder(), Is.EqualTo("b,a,c"));
        }

        [Test]
        public void ReorderingUnderAPlainGameObjectWithoutAModel_ReordersTheCSG()
        {
            var group = new GameObject("group");
            roots.Add(group);
            Box("a", group.transform, Vector3.zero);
            var b = Box("b", group.transform, new Vector3(3, 0, 0));
            Box("c", group.transform, new Vector3(6, 0, 0));
            Update();
            Assert.That(TreeOrder(), Is.EqualTo("a,b,c"));

            b.transform.SetSiblingIndex(0);
            HierarchyChanged();
            Assert.That(TreeOrder(), Is.EqualTo("b,a,c"));
        }
    }
}
