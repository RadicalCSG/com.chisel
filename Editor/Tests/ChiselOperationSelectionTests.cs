using System.Collections;
using System.Collections.Generic;
using Chisel.Components;
using Chisel.Core;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace Chisel.Editors.Tests
{
    [TestFixture]
    public sealed class ChiselOperationSelectionTests
    {
        readonly List<Object> created = new();
        Object[] previousSelection;

        [SetUp]
        public void SetUp()
        {
            previousSelection = Selection.objects;
        }

        [TearDown]
        public void TearDown()
        {
            Selection.objects = previousSelection;
            ChiselSelectionManager.UpdateSelection();
            for (int i = created.Count - 1; i >= 0; i--)
            {
                if (created[i]) Object.DestroyImmediate(created[i]);
            }
            created.Clear();
        }

        // The operation shown for the selection follows a change made outside the operation toolbar (the inspector)
        [UnityTest]
        public IEnumerator TheSelectionsOperation_FollowsAChangeMadeElsewhere()
        {
            var box = new GameObject("Box").AddComponent<ChiselBoxComponent>();
            created.Add(box.gameObject);
            Selection.activeGameObject = box.gameObject;
            ChiselSelectionManager.UpdateSelection();
            Assert.That(ChiselSelectionManager.OperationOfSelectedNodes, Is.EqualTo(CSGOperationType.Additive));

            var serializedObject = new SerializedObject(box);
            serializedObject.FindProperty("operation").intValue = (int)CSGOperationType.Subtractive;
            serializedObject.ApplyModifiedProperties();
            for (int frame = 0; frame < 3; frame++)
                yield return null;

            Assert.That(ChiselSelectionManager.OperationOfSelectedNodes, Is.EqualTo(CSGOperationType.Subtractive));
        }

        // Setting a composite's operation from its context menu can be undone
        [Test]
        public void SettingACompositesOperationFromItsMenu_CanBeUndone()
        {
            var composite = new GameObject("Composite").AddComponent<ChiselCompositeComponent>();
            created.Add(composite.gameObject);
            Undo.IncrementCurrentGroup();

            ChiselNodeEditorBase.MenuSetOperationTo(new MenuCommand(composite.gameObject), CSGOperationType.Subtractive);
            Assert.That(composite.Operation, Is.EqualTo(CSGOperationType.Subtractive));

            Undo.PerformUndo();
            Assert.That(composite.Operation, Is.EqualTo(CSGOperationType.Additive));
        }
    }
}
