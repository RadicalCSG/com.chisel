using System.Collections.Generic;
using Chisel.Components;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Chisel.Editors.Tests
{
    // Objects moved under a new composite or model keep their hierarchy order, whatever order they were selected in:
    // the CSG order follows the hierarchy
    [TestFixture]
    public sealed class ChiselHierarchyOrderTests
    {
        readonly List<Object> created = new();
        Object[] previousSelection;
        ChiselModelComponent previousActiveModel;

        [SetUp]
        public void SetUp()
        {
            previousSelection = Selection.objects;
            previousActiveModel = ChiselModelManager.Instance.ActiveModel;
        }

        [TearDown]
        public void TearDown()
        {
            Selection.objects = previousSelection;
            if (previousActiveModel)
                ChiselModelManager.Instance.ActiveModel = previousActiveModel;
            for (int i = created.Count - 1; i >= 0; i--)
            {
                if (created[i]) Object.DestroyImmediate(created[i]);
            }
            created.Clear();
        }

        ChiselBoxComponent Box(string name, Transform parent)
        {
            var box = new GameObject(name).AddComponent<ChiselBoxComponent>();
            box.transform.SetParent(parent, false);
            return box;
        }

        [Test]
        public void EncapsulatingInAComposite_KeepsTheHierarchyOrder()
        {
            var root = new GameObject("Root");
            created.Add(root);
            var first  = Box("First", root.transform);
            var second = Box("Second", root.transform);

            var composite = ChiselNodeEditorBase.EncapsulateInComposite(new[] { second.gameObject, first.gameObject });
            Assert.That(composite, Is.Not.Null);
            created.Add(composite.gameObject);

            Assert.That(composite.transform.parent, Is.SameAs(root.transform));
            Assert.That(composite.transform.GetSiblingIndex(), Is.EqualTo(0), "the composite takes the place of the topmost object");
            Assert.That(ChildNames(composite.transform), Is.EqualTo(new[] { "First", "Second" }));
        }

        // The names of the Chisel nodes under the parent, in order (a model also holds its generated meshes)
        static string[] ChildNames(Transform parent)
        {
            var names = new List<string>();
            for (int i = 0; i < parent.childCount; i++)
            {
                var child = parent.GetChild(i);
                if (child.GetComponent<ChiselNodeComponent>())
                    names.Add(child.name);
            }
            return names.ToArray();
        }

        [Test]
        public void CreatingAModelForObjects_KeepsTheHierarchyOrder()
        {
            var root = new GameObject("Root");
            created.Add(root);
            var first  = Box("First", root.transform);
            var second = Box("Second", root.transform);

            var model = ChiselNodeEditorBase.CreateModelFor(new Object[] { second, first });
            Assert.That(model, Is.Not.Null);
            created.Add(model.gameObject);

            Assert.That(model.transform.parent, Is.SameAs(root.transform));
            Assert.That(model.transform.GetSiblingIndex(), Is.EqualTo(0), "the model takes the place of the topmost object");
            Assert.That(ChildNames(model.transform), Is.EqualTo(new[] { "First", "Second" }));
        }

        [Test]
        public void MovingObjectsUnderAModel_KeepsTheHierarchyOrder()
        {
            var root = new GameObject("Root");
            created.Add(root);
            var first  = Box("First", root.transform);
            var second = Box("Second", root.transform);
            var model = new GameObject("Model").AddComponent<ChiselModelComponent>();
            created.Add(model.gameObject);

            ChiselNodeEditorBase.MoveTargetsUnderModel(new Object[] { second, first }, model);

            Assert.That(ChildNames(model.transform), Is.EqualTo(new[] { "First", "Second" }));
        }
    }
}
