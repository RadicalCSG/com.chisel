using Chisel;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;

namespace Chisel.Core.Tests
{
    // The setters in BoundsExtensions change the bounds they are called on. They used to take the bounds by value and
    // change a copy, so setting a box's Min, Max, Size or Center from code did nothing.
    [TestFixture]
    public class BoundsExtensionsTests
    {
        [Test]
        public void IsValid_TrueForNonDegenerateBounds()
        {
            Assert.That(BoundsExtensions.IsValid(new float3(-1, -1, -1), new float3(1, 1, 1)), Is.True);
        }

        [Test]
        public void IsValid_FalseForZeroSizeBounds()
        {
            Assert.That(BoundsExtensions.IsValid(float3.zero, float3.zero), Is.False);
        }

        [Test]
        public void IsValid_FalseWhenOneAxisIsTooThin()
        {
            // y axis has zero extent -> degenerate
            Assert.That(BoundsExtensions.IsValid(new float3(-1, 0, -1), new float3(1, 0, 1)), Is.False);
        }

        [Test]
        public void IsValid_FalseForNonFiniteValues()
        {
            Assert.That(BoundsExtensions.IsValid(new float3(float.NaN, 0, 0), new float3(1, 1, 1)), Is.False);
            Assert.That(BoundsExtensions.IsValid(new float3(0, 0, 0), new float3(float.PositiveInfinity, 1, 1)), Is.False);
        }

        static void AreEqual(float3 expected, float3 actual, string message)
        {
            Assert.That(math.distance(expected, actual), Is.LessThan(1e-5f), $"{message}: expected {expected}, got {actual}");
        }

        [Test]
        public void MinMaxAABB_Setters_ChangeTheBounds()
        {
            var bounds = new MinMaxAABB { Min = new float3(0), Max = new float3(1) };
            bounds.SetMin(new float3(-2, -1, -2));
            bounds.SetMax(new float3(2, 0, 2));
            AreEqual(new float3(-2, -1, -2), bounds.Min, "min");
            AreEqual(new float3(2, 0, 2), bounds.Max, "max");

            // The size (4, 1, 4) stays
            bounds.SetCenter(new float3(-10, 5, 0));
            AreEqual(new float3(-12, 4.5f, -2), bounds.Min, "moved min");
            AreEqual(new float3(-8, 5.5f, 2), bounds.Max, "moved max");

            // Around the center, which is negative
            bounds.SetExtents(new float3(1, 2, 3));
            AreEqual(new float3(-11, 3, -3), bounds.Min, "resized min");
            AreEqual(new float3(-9, 7, 3), bounds.Max, "resized max");

            bounds.SetMinMax(new float3(1, 2, 3), new float3(4, 5, 6));
            AreEqual(new float3(1, 2, 3), bounds.Min, "set min");
            AreEqual(new float3(4, 5, 6), bounds.Max, "set max");
        }

        [Test]
        public void AABB_Setters_ChangeTheBounds()
        {
            var bounds = new AABB { Center = float3.zero, Extents = new float3(1) };
            bounds.SetMin(new float3(-3, -1, -5));
            AreEqual(new float3(-3, -1, -5), bounds.Min, "min");
            AreEqual(new float3(1, 1, 1), bounds.Max, "the max stays");

            bounds.SetMax(new float3(5, 3, 1));
            AreEqual(new float3(-3, -1, -5), bounds.Min, "the min stays");
            AreEqual(new float3(5, 3, 1), bounds.Max, "max");

            bounds.SetCenter(new float3(-10, 0, 0));
            AreEqual(new float3(-10, 0, 0), bounds.Center, "center");
            bounds.SetExtents(new float3(2, 2, 2));
            AreEqual(new float3(-12, -2, -2), bounds.Min, "resized");

            bounds.SetMinMax(new float3(-1, -2, -3), new float3(1, 2, 3));
            AreEqual(float3.zero, bounds.Center, "set center");
            AreEqual(new float3(1, 2, 3), bounds.Extents, "set extents");

            bounds.Encapsulate(new float3(5, 0, 0));
            AreEqual(new float3(-1, -2, -3), bounds.Min, "encapsulated min");
            AreEqual(new float3(5, 2, 3), bounds.Max, "encapsulated max");
            bounds.Encapsulate(new AABB { Center = new float3(0, 0, -10), Extents = new float3(1) });
            AreEqual(new float3(-1, -2, -11), bounds.Min, "encapsulated box");
        }

        [Test]
        public void Bounds_Setters_ChangeTheBounds()
        {
            var bounds = new Bounds(Vector3.zero, Vector3.one * 2);
            bounds.SetMin(new float3(-3, -1, -5));
            AreEqual(new float3(-3, -1, -5), bounds.min, "min");
            AreEqual(new float3(1, 1, 1), bounds.max, "the max stays");

            bounds.SetMax(new float3(5, 3, 1));
            AreEqual(new float3(5, 3, 1), bounds.max, "max");

            bounds.SetCenter(new float3(0, 10, 0));
            AreEqual(new float3(0, 10, 0), bounds.center, "center");
            bounds.SetExtents(new float3(1, 1, 1));
            AreEqual(new float3(-1, 9, -1), bounds.min, "resized");
        }

        [Test]
        public void BoxSettings_MinMaxSizeAndCenter_ChangeTheBox()
        {
            var box = ChiselBox.DefaultSettings;
            box.Min = new float3(-2, -1, -2);
            box.Max = new float3(2, 0, 2);
            AreEqual(new float3(-2, -1, -2), box.bounds.Min, "min");
            AreEqual(new float3(2, 0, 2), box.bounds.Max, "max");

            box.Size = new float3(2, 4, 6);
            AreEqual(new float3(0, -0.5f, 0), box.Center, "resized around the center");
            AreEqual(new float3(2, 4, 6), box.Size, "size");

            box.Center = new float3(10, 0, 0);
            AreEqual(new float3(9, -2, -3), box.bounds.Min, "moved");
            AreEqual(new float3(2, 4, 6), box.Size, "the size stays");
        }

        [Test]
        public void LinearStairsSettings_SizeAndCenter_ChangeTheBounds()
        {
            var stairs = new ChiselLinearStairs();
            stairs.Reset();
            var center = stairs.Center;
            stairs.BoundsSize = new float3(3, 2, 5);
            AreEqual(new float3(3, 2, 5), stairs.BoundsSize, "size");
            AreEqual(center, stairs.Center, "resized around the center");

            stairs.Center = new float3(-4, 1, 7);
            AreEqual(new float3(-4, 1, 7), stairs.Center, "center");
            AreEqual(new float3(3, 2, 5), stairs.BoundsSize, "the size stays");
        }
    }
}
