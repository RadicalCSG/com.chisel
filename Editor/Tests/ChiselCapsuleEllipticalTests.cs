using System.Collections.Generic;
using Chisel.Components;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Chisel.Editors.Tests
{
    // Not elliptical, a capsule's x and z diameters are one: turning Elliptical off, in the inspector or from a script,
    // keeps the larger of the two, as a cylinder does
    [TestFixture]
    public sealed class ChiselCapsuleEllipticalTests
    {
        readonly List<Object> created = new();
        int undoGroup;

        // the test runner reverts a run's undo steps when the run ends: a test's own are reverted before what it made goes
        [SetUp]
        public void SetUp()
        {
            Undo.IncrementCurrentGroup();
            undoGroup = Undo.GetCurrentGroup();
        }

        [TearDown]
        public void TearDown()
        {
            Undo.RevertAllDownToGroup(undoGroup);
            for (int i = created.Count - 1; i >= 0; i--)
            {
                if (created[i]) Object.DestroyImmediate(created[i]);
            }
            created.Clear();
        }

        ChiselCapsuleComponent EllipticalCapsule()
        {
            var model = new GameObject("Model").AddComponent<ChiselModelComponent>();
            created.Add(model.gameObject);
            var capsule = new GameObject("Capsule").AddComponent<ChiselCapsuleComponent>();
            capsule.transform.SetParent(model.transform, false);
            capsule.IsElliptical = true;
            capsule.DiameterX    = 1;
            capsule.DiameterZ    = 3;
            return capsule;
        }

        [Test]
        public void TurningEllipticalOffFromAScript_KeepsTheLargerDiameter()
        {
            var capsule = EllipticalCapsule();
            capsule.IsElliptical = false;
            Assert.That(capsule.DiameterX, Is.EqualTo(3), "x");
            Assert.That(capsule.DiameterZ, Is.EqualTo(3), "z");
        }

        // the inspector's toggle writes the setting itself, and one undo gives the elliptical diameters back
        [Test]
        public void TurningEllipticalOffInTheInspector_KeepsTheLargerDiameter()
        {
            var capsule = EllipticalCapsule();
            var editor = (ChiselCapsuleEditor)UnityEditor.Editor.CreateEditor(capsule);
            created.Add(editor);

            Undo.IncrementCurrentGroup();
            editor.serializedObject.Update();
            editor.serializedObject.FindProperty("definition.settings.isElliptical").boolValue = false;
            editor.KeepLargerDiameterWhenTurnedRound();
            editor.serializedObject.ApplyModifiedProperties();

            Assert.That(capsule.IsElliptical, Is.False);
            Assert.That(capsule.DiameterX, Is.EqualTo(3), "x");
            Assert.That(capsule.DiameterZ, Is.EqualTo(3), "z");

            Undo.PerformUndo();
            Assert.That(capsule.IsElliptical, Is.True, "undone");
            Assert.That(capsule.DiameterX, Is.EqualTo(1), "undone x");
            Assert.That(capsule.DiameterZ, Is.EqualTo(3), "undone z");
        }
    }
}
