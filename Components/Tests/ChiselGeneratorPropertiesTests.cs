using System.Collections.Generic;
using Chisel.Core;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;

namespace Chisel.Components.Tests
{
    // A generator's settings can be set from a script through its component's properties: each is kept, and one that
    // changes the shape builds it again
    [TestFixture]
    public class ChiselGeneratorPropertiesTests
    {
        readonly List<GameObject> roots = new();
        (UnityEngine.SceneManagement.Scene scene, bool wasDirty) remembered;

        [SetUp]
        public void SetUp() { remembered = SceneLeftClean.Remember(); }

        [TearDown]
        public void TearDown()
        {
            foreach (var root in roots)
                if (root) Object.DestroyImmediate(root);
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

        T Generator<T>() where T : ChiselGeneratorComponent
        {
            var model = new GameObject("model").AddComponent<ChiselModelComponent>();
            roots.Add(model.gameObject);
            var generator = new GameObject(typeof(T).Name).AddComponent<T>();
            generator.transform.SetParent(model.transform, false);
            Update();
            return generator;
        }

        // the brushes it's built from, as their vertices
        static string Built(ChiselGeneratorComponent generator)
        {
            var brushes = new List<CSGTreeBrush>();
            ChiselGeneratorParts.BrushesOf(generator, brushes);
            var text = new System.Text.StringBuilder();
            foreach (var brush in brushes)
            {
                if (!brush.BrushMesh.Valid)
                    continue;
                ref var mesh = ref BrushMeshManager.GetBrushMeshBlob(brush.BrushMesh).Value;
                for (int v = 0; v < mesh.localVertices.Length; v++)
                    text.Append(mesh.localVertices[v]).Append(' ');
                text.Append('|');
            }
            return text.ToString();
        }

        void AssertProperty<T>(string name, object value, bool changesShape) where T : ChiselGeneratorComponent
        {
            var generator = Generator<T>();
            var property  = typeof(T).GetProperty(name);
            Assert.That(property, Is.Not.Null, $"{typeof(T).Name}.{name}");
            Assert.That(property.GetValue(generator), Is.Not.EqualTo(value), "a value it doesn't have yet");
            var before = Built(generator);

            property.SetValue(generator, value);
            Update();

            // a float may come back from being validated off by its rounding
            if (value is float number)
                Assert.That((float)property.GetValue(generator), Is.EqualTo(number).Within(1e-5f), "kept");
            else
                Assert.That(property.GetValue(generator), Is.EqualTo(value), "kept");
            if (changesShape)
                Assert.That(Built(generator), Is.Not.EqualTo(before), "built again");
        }

        [TestCase("Width",        2.5f, true)]
        [TestCase("Height",       2.5f, true)]
        [TestCase("Length",       3.5f, true)]
        [TestCase("TopLength",    0.4f, true)]
        [TestCase("BottomLength", 0.4f, true)]
        [TestCase("TopSides",     6,    true)]
        [TestCase("BottomSides",  6,    true)]
        public void Stadium(string name, object value, bool changesShape) { AssertProperty<ChiselStadiumComponent>(name, value, changesShape); }

        [TestCase("OuterDiameter",      2.5f,  true)]
        [TestCase("InnerDiameter",      0.5f,  true)]
        [TestCase("TubeWidth",          0.25f, true)]
        [TestCase("TubeHeight",         0.25f, true)]
        [TestCase("TubeRotation",       30f,   true)]
        [TestCase("StartAngle",         30f,   true)]
        [TestCase("TotalAngle",         180f,  true)]
        [TestCase("HorizontalSegments", 12,    true)]
        [TestCase("VerticalSegments",   12,    true)]
        [TestCase("FitCircle",          false, true)]
        public void Torus(string name, object value, bool changesShape) { AssertProperty<ChiselTorusComponent>(name, value, changesShape); }

        [TestCase("CurveSegments",   4,    false)]
        [TestCase("RevolveSegments", 12,   true)]
        [TestCase("StartAngle",      30f,  true)]
        [TestCase("TotalAngle",      180f, true)]
        public void RevolvedShape(string name, object value, bool changesShape) { AssertProperty<ChiselRevolvedShapeComponent>(name, value, changesShape); }

        // only a smooth underside is made of pieces
        [TestCase("SmoothSubdivisions", 5, false)]
        public void SpiralStairs(string name, object value, bool changesShape) { AssertProperty<ChiselSpiralStairsComponent>(name, value, changesShape); }

        // its guard rails' front offset and how far back they go at the top only show with guard rails
        [TestCase("SideFrontOffset", 0.1f,                     false)]
        [TestCase("TopSide",         StairsTopSideType.Length, false)]
        [TestCase("TopSideLength",   0.5f,                     false)]
        public void LinearStairs(string name, object value, bool changesShape) { AssertProperty<ChiselLinearStairsComponent>(name, value, changesShape); }

        // the settings that change the shape on their own; the others only show with guard rails or an open path
        [TestCase("Height",              2.5f,                         true)]
        [TestCase("Depth",               2.5f,                         true)]
        [TestCase("StepHeight",          0.3f,                         true)]
        [TestCase("StepDepth",           0.3f,                         true)]
        [TestCase("TreadHeight",         0.05f,                        true)]
        [TestCase("NosingDepth",         0.05f,                        true)]
        [TestCase("NosingWidth",         0.05f,                        false)]
        [TestCase("RiserType",           StairsRiserType.FillDown,     true)]
        [TestCase("RiserDepth",          0.1f,                         true)]
        // the base takes up what whole steps leave of the height: 0.4 leaves three of 0.2
        [TestCase("PlateauHeight",       0.4f,                         true)]
        [TestCase("StartSide",           StairsSideType.DownAndUp,     false)]
        [TestCase("EndSide",             StairsSideType.DownAndUp,     false)]
        [TestCase("CornerSide",          StairsSideType.DownAndUp,     true)]
        [TestCase("SideWidth",           0.25f,                        false)]
        [TestCase("SideHeight",          0.75f,                        false)]
        [TestCase("SideDepth",           0.25f,                        false)]
        [TestCase("SideFrontOffset",     0.1f,                         false)]
        [TestCase("TopSide",             StairsTopSideType.Length,     false)]
        [TestCase("TopSideLength",       0.5f,                         false)]
        [TestCase("CornerTopSide",       StairsTopSideType.Length,     false)]
        [TestCase("CornerTopSideLength", 0.5f,                         false)]
        public void PathedStairs(string name, object value, bool changesShape) { AssertProperty<ChiselPathedStairsComponent>(name, value, changesShape); }
    }
}
