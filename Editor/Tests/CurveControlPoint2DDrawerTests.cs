using System.Collections.Generic;
using Chisel.Components;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Chisel.Editors.Tests
{
    // A point of a 2D shape shows whether the side from it is smooth only where that does something: on the extruded
    // and the revolved shape, not on the pathed stairs' path
    [TestFixture]
    public sealed class CurveControlPoint2DDrawerTests
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

        // the height of the first point of the component's shape, unfolded
        float PointHeight<T>() where T : Component
        {
            var model = new GameObject("Model").AddComponent<ChiselModelComponent>();
            created.Add(model.gameObject);
            var generator = new GameObject(typeof(T).Name).AddComponent<T>();
            generator.transform.SetParent(model.transform, false);
            var serializedObject = new SerializedObject(generator);
            var point = serializedObject.FindProperty("definition.shape.controlPoints.Array.data[0]");
            Assert.That(point, Is.Not.Null, typeof(T).Name);
            point.isExpanded = true;
            return EditorGUI.GetPropertyHeight(point, true);
        }

        [Test]
        public void ShapesWithSides_ShowWhetherASideIsSmooth_APathDoesNot()
        {
            var path     = PointHeight<ChiselPathedStairsComponent>();
            var oneMore  = path + EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;
            Assert.That(PointHeight<ChiselExtrudedShapeComponent>(), Is.EqualTo(oneMore).Within(1e-3f), "extruded shape");
            Assert.That(PointHeight<ChiselRevolvedShapeComponent>(), Is.EqualTo(oneMore).Within(1e-3f), "revolved shape");
        }
    }
}
