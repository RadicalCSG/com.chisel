using System.Collections.Generic;
using Chisel.Components;
using Chisel.Core;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Chisel.Editors.Tests
{
    // Not elliptical, a cylinder's x and z diameters are one: turning Elliptical off, in the inspector or from a script,
    // keeps the larger of each circle's two, so the cylinder doesn't suddenly shrink
    [TestFixture]
    public sealed class ChiselCylinderEllipticalTests
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

        ChiselCylinderComponent EllipticalFrustum()
        {
            var model = new GameObject("Model").AddComponent<ChiselModelComponent>();
            created.Add(model.gameObject);
            var cylinder = new GameObject("Cylinder").AddComponent<ChiselCylinderComponent>();
            cylinder.transform.SetParent(model.transform, false);
            cylinder.Type            = CylinderShapeType.ConicalFrustum;
            cylinder.IsElliptical    = true;
            cylinder.BottomDiameterX = 1;
            cylinder.BottomDiameterZ = 3;
            cylinder.TopDiameterX    = 2;
            cylinder.TopDiameterZ    = 0.5f;
            return cylinder;
        }

        static void AssertDiameters(ChiselCylinderComponent cylinder, float bottomX, float bottomZ, float topX, float topZ)
        {
            Assert.That(cylinder.BottomDiameterX, Is.EqualTo(bottomX), "bottom x");
            Assert.That(cylinder.BottomDiameterZ, Is.EqualTo(bottomZ), "bottom z");
            Assert.That(cylinder.TopDiameterX,    Is.EqualTo(topX),    "top x");
            Assert.That(cylinder.TopDiameterZ,    Is.EqualTo(topZ),    "top z");
        }

        [Test]
        public void TurningEllipticalOffFromAScript_KeepsTheLargerDiameters()
        {
            var cylinder = EllipticalFrustum();
            cylinder.IsElliptical = false;
            AssertDiameters(cylinder, 3, 3, 2, 2);
        }

        // the inspector's toggle writes the setting itself, and one undo gives the elliptical diameters back
        [Test]
        public void TurningEllipticalOffInTheInspector_KeepsTheLargerDiameters()
        {
            var cylinder = EllipticalFrustum();
            var editor = (ChiselCylinderEditor)UnityEditor.Editor.CreateEditor(cylinder);
            created.Add(editor);

            Undo.IncrementCurrentGroup();
            editor.serializedObject.Update();
            editor.serializedObject.FindProperty("definition.settings.isElliptical").boolValue = false;
            editor.KeepLargerDiametersWhenTurnedRound();
            editor.serializedObject.ApplyModifiedProperties();

            Assert.That(cylinder.IsElliptical, Is.False);
            AssertDiameters(cylinder, 3, 3, 2, 2);

            Undo.PerformUndo();
            Assert.That(cylinder.IsElliptical, Is.True, "undone");
            AssertDiameters(cylinder, 1, 3, 2, 0.5f);
        }
    }
}
