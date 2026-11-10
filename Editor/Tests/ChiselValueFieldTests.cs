using Chisel.Core;
using NUnit.Framework;
using UnityEngine;

namespace Chisel.Editors.Tests
{
    // Inspector fields marked with Chisel's value attributes show their unit, and UV values can be reset one at a time
    [TestFixture]
    public sealed class ChiselValueFieldTests
    {
        DistanceUnit previousUnit;

        [SetUp]
        public void SetUp()
        {
            previousUnit = Units.ActiveDistanceUnit;
        }

        [TearDown]
        public void TearDown()
        {
            Units.ActiveDistanceUnit = previousUnit;
        }

        [Test]
        public void ADistance_IsShownInTheActiveUnit()
        {
            Units.ActiveDistanceUnit = DistanceUnit.Centimeters;
            Assert.That(ChiselValueFieldGUI.Suffix(ChiselValueKind.Distance), Is.EqualTo("cm"));
            Assert.That(ChiselValueFieldGUI.ToShown(ChiselValueKind.Distance, 1.5f), Is.EqualTo(150f).Within(1e-4f));
            Assert.That(ChiselValueFieldGUI.FromShown(ChiselValueKind.Distance, 150f), Is.EqualTo(1.5f).Within(1e-6f));
        }

        [Test]
        public void AnAngle_IsShownInDegrees()
        {
            Assert.That(ChiselValueFieldGUI.Suffix(ChiselValueKind.Angle), Is.EqualTo("\u00B0"));
            Assert.That(ChiselValueFieldGUI.ToShown(ChiselValueKind.Angle, 45f), Is.EqualTo(45f));
            Assert.That(ChiselValueFieldGUI.FromShown(ChiselValueKind.Angle, 45f), Is.EqualTo(45f));
        }

        [Test]
        public void AScale_IsShownAsAPercentage()
        {
            Assert.That(ChiselValueFieldGUI.Suffix(ChiselValueKind.Scale), Is.EqualTo("%"));
            Assert.That(ChiselValueFieldGUI.ToShown(ChiselValueKind.Scale, 0.5f), Is.EqualTo(50f).Within(1e-4f));
            Assert.That(ChiselValueFieldGUI.FromShown(ChiselValueKind.Scale, 50f), Is.EqualTo(0.5f).Within(1e-6f));
        }

        // Changing one component writes only that one back: the others keep their exact values, whatever the unit's rounding
        [Test]
        public void ChangingOneComponent_KeepsTheOthersExactly()
        {
            Units.ActiveDistanceUnit = DistanceUnit.Inches;
            var value  = new Vector3(0.1234567f, 1.7654321f, 2.2222222f);
            var shown  = ChiselValueFieldGUI.ToShown(ChiselValueKind.Distance, value);
            var edited = shown;
            edited.y = 10;

            var result = ChiselValueFieldGUI.Edited(ChiselValueKind.Distance, value, shown, edited);
            Assert.That(result.x, Is.EqualTo(value.x));
            Assert.That(result.z, Is.EqualTo(value.z));
            Assert.That(result.y, Is.EqualTo(10 / 39.37007874f).Within(1e-6f));
        }

        static UVMatrix Matrix(Vector2 translation, float rotation, Vector2 scale)
        {
            return UVMatrix.TRS(translation, Vector3.up, rotation, scale);
        }

        static void AssertDecomposes(UVMatrix matrix, Vector2 translation, float rotation, Vector2 scale)
        {
            matrix.Decompose(out var t, out var r, out var s);
            Assert.That(t.x, Is.EqualTo(translation.x).Within(1e-4f), "translation x");
            Assert.That(t.y, Is.EqualTo(translation.y).Within(1e-4f), "translation y");
            Assert.That(r,   Is.EqualTo(rotation).Within(1e-3f), "rotation");
            Assert.That(s.x, Is.EqualTo(scale.x).Within(1e-4f), "scale x");
            Assert.That(s.y, Is.EqualTo(scale.y).Within(1e-4f), "scale y");
        }

        [Test]
        public void ResettingAUVValue_KeepsTheOtherTwo()
        {
            var matrix = Matrix(new Vector2(2, 3), 30, new Vector2(2, 4));
            AssertDecomposes(UVMatrixPropertyDrawer.Reset(matrix, UVMatrixPropertyDrawer.UVPart.Translation), Vector2.zero,        30, new Vector2(2, 4));
            AssertDecomposes(UVMatrixPropertyDrawer.Reset(matrix, UVMatrixPropertyDrawer.UVPart.Rotation),    new Vector2(2, 3),  0, new Vector2(2, 4));
            AssertDecomposes(UVMatrixPropertyDrawer.Reset(matrix, UVMatrixPropertyDrawer.UVPart.Scale),       new Vector2(2, 3), 30, Vector2.one);
            AssertDecomposes(UVMatrixPropertyDrawer.Reset(matrix, UVMatrixPropertyDrawer.UVPart.All),         Vector2.zero,        0, Vector2.one);
        }
    }
}
