using System.Collections.Generic;
using Chisel.Components;
using NUnit.Framework;
using UnityEngine;

namespace Chisel.Editors.Tests
{
    // A hierarchy row's warning comes from its node's messages (GetMessages), which change only with the hierarchy, a
    // model update or a reset: the poll asks for them again only after one of those
    [TestFixture]
    public sealed class ChiselHierarchyDecoratorTests
    {
        readonly List<Object> created = new();

        [TearDown]
        public void TearDown()
        {
            for (int i = created.Count - 1; i >= 0; i--)
            {
                if (created[i]) Object.DestroyImmediate(created[i]);
            }
            created.Clear();
            ChiselNodeHierarchyManager.Update();
            ChiselModelManager.Instance.UpdateModels();
        }

        static void Update()
        {
            ChiselNodeHierarchyManager.Update();
            ChiselModelManager.Instance.UpdateModels();
        }

        [Test]
        public void ARowsWarning_IsCheckedAgainOnlyAfterAChange()
        {
            var model = new GameObject("Model").AddComponent<ChiselModelComponent>();
            created.Add(model.gameObject);
            var composite = new GameObject("Composite").AddComponent<ChiselCompositeComponent>();
            composite.transform.SetParent(model.transform, false);
            created.Add(composite.gameObject);
            Update();

            var row = new ChiselHierarchyDecorator.RowState { node = composite };
            ChiselHierarchyDecorator.Evaluate(row, checkMessages: true);
            Assert.That(row.hasWarning, Is.True, "a composite without children has no effect");

            ChiselHierarchyDecorator.MessagesMayHaveChanged = false;
            var box = new GameObject("Box").AddComponent<ChiselBoxComponent>();
            box.transform.SetParent(composite.transform, false);
            created.Add(box.gameObject);
            Update();
            Assert.That(ChiselHierarchyDecorator.MessagesMayHaveChanged, Is.True, "a box put in the composite changes the hierarchy");

            ChiselHierarchyDecorator.Evaluate(row, checkMessages: false);
            Assert.That(row.hasWarning, Is.True, "a poll that doesn't check the messages keeps what was found");
            ChiselHierarchyDecorator.Evaluate(row, checkMessages: true);
            Assert.That(row.hasWarning, Is.False, "the composite has a child now");
        }

        [Test]
        public void APollWithoutAChange_DoesNotCheckTheMessages()
        {
            var model = new GameObject("Model").AddComponent<ChiselModelComponent>();
            created.Add(model.gameObject);
            var box = new GameObject("Box").AddComponent<ChiselBoxComponent>();
            box.transform.SetParent(model.transform, false);
            created.Add(box.gameObject);
            Update();

            ChiselHierarchyDecorator.MessagesMayHaveChanged = false;
            Update();
            Assert.That(ChiselHierarchyDecorator.MessagesMayHaveChanged, Is.False, "nothing changed");
            Assert.That(ChiselHierarchyDecorator.TakeMessagesMayHaveChanged(), Is.False);

            box.transform.position += Vector3.right;
            ChiselNodeHierarchyManager.UpdateTreeNodeTransformation(box);
            Update();
            Assert.That(ChiselHierarchyDecorator.TakeMessagesMayHaveChanged(), Is.True, "moving a node can change what it is made of");
            Assert.That(ChiselHierarchyDecorator.MessagesMayHaveChanged, Is.False, "taken");
        }
    }
}
