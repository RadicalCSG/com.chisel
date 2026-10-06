using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;

namespace Chisel.Core.Tests
{
    // Shared helpers for the Chisel.Core tests.
    static class ChiselTestUtility
    {
        public const float kEpsilon = 1e-4f;

        public static void AssertApproximately(Vector3 expected, Vector3 actual, float epsilon = kEpsilon)
        {
            Assert.That(actual.x, Is.EqualTo(expected.x).Within(epsilon), $"x mismatch (expected {expected}, got {actual})");
            Assert.That(actual.y, Is.EqualTo(expected.y).Within(epsilon), $"y mismatch (expected {expected}, got {actual})");
            Assert.That(actual.z, Is.EqualTo(expected.z).Within(epsilon), $"z mismatch (expected {expected}, got {actual})");
        }

        public static void AssertApproximately(float3 expected, float3 actual, float epsilon = kEpsilon)
        {
            Assert.That(actual.x, Is.EqualTo(expected.x).Within(epsilon), $"x mismatch (expected {expected}, got {actual})");
            Assert.That(actual.y, Is.EqualTo(expected.y).Within(epsilon), $"y mismatch (expected {expected}, got {actual})");
            Assert.That(actual.z, Is.EqualTo(expected.z).Within(epsilon), $"z mismatch (expected {expected}, got {actual})");
        }

        public static void AssertApproximately(double3 expected, double3 actual, double epsilon = kEpsilon)
        {
            Assert.That(actual.x, Is.EqualTo(expected.x).Within(epsilon), $"x mismatch (expected {expected}, got {actual})");
            Assert.That(actual.y, Is.EqualTo(expected.y).Within(epsilon), $"y mismatch (expected {expected}, got {actual})");
            Assert.That(actual.z, Is.EqualTo(expected.z).Within(epsilon), $"z mismatch (expected {expected}, got {actual})");
        }

        // Returns true when the point lies on the plane (within epsilon).
        public static bool IsOnPlane(Plane plane, Vector3 point, float epsilon = 1e-3f)
        {
            return Mathf.Abs(plane.GetDistanceToPoint(point)) <= epsilon;
        }
    }
}
