using System.Text.RegularExpressions;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.TestTools;

namespace Chisel.Core.Tests
{
    // UVMatrix is the 2x4 surface-UV transform attached to every CSG polygon.
    [TestFixture]
    public class UVMatrixTests
    {
        static void AssertVector4(Vector4 expected, Vector4 actual, float epsilon = 1e-5f)
        {
            Assert.That(actual.x, Is.EqualTo(expected.x).Within(epsilon));
            Assert.That(actual.y, Is.EqualTo(expected.y).Within(epsilon));
            Assert.That(actual.z, Is.EqualTo(expected.z).Within(epsilon));
            Assert.That(actual.w, Is.EqualTo(expected.w).Within(epsilon));
        }

        [Test]
        public void Identity_HasExpectedRows()
        {
            AssertVector4(new Vector4(1, 0, 0, 0), UVMatrix.Identity.U);
            AssertVector4(new Vector4(0, 1, 0, 0), UVMatrix.Identity.V);
        }

        [Test]
        public void Centered_HasHalfOffset()
        {
            AssertVector4(new Vector4(1, 0, 0, 0.5f), UVMatrix.Centered.U);
            AssertVector4(new Vector4(0, 1, 0, 0.5f), UVMatrix.Centered.V);
        }

        [Test]
        public void PlaneNormal_OfIdentity_IsForward()
        {
            var n = UVMatrix.Identity.PlaneNormal;
            Assert.That(n.x, Is.EqualTo(0f).Within(1e-5f));
            Assert.That(n.y, Is.EqualTo(0f).Within(1e-5f));
            Assert.That(n.z, Is.EqualTo(1f).Within(1e-5f));
        }

        [Test]
        public void Equality_Operators()
        {
            Assert.That(UVMatrix.Identity == UVMatrix.Identity, Is.True);
            Assert.That(UVMatrix.Identity != UVMatrix.Centered, Is.True);
            Assert.That(UVMatrix.Identity.Equals((object)UVMatrix.Identity), Is.True);
            Assert.That(UVMatrix.Identity.Equals((object)UVMatrix.Centered), Is.False);
        }

        [Test]
        public void GetHashCode_IsStableForEqualMatrices()
        {
            var a = new UVMatrix(new float4(1, 0, 0, 0.5f), new float4(0, 1, 0, 0.5f));
            Assert.That(a.GetHashCode(), Is.EqualTo(UVMatrix.Centered.GetHashCode()));
        }

        [Test]
        public void Matrix4x4_RoundTrip_PreservesRows()
        {
            foreach (var original in new[] { UVMatrix.Identity, UVMatrix.Centered })
            {
                Matrix4x4 m = original;            // implicit ToMatrix4x4
                UVMatrix roundTrip = m;            // implicit from Matrix4x4
                AssertVector4(original.U, roundTrip.U);
                AssertVector4(original.V, roundTrip.V);
            }
        }

        [Test]
        public void TRS_Translation_RoundTripsThroughDecompose()
        {
            var translation = new Vector2(3, 4);
            var scale       = new float2(1, 1);
            var uv = UVMatrix.TRS(translation, new Vector3(0, 1, 0), 0f, scale);

            uv.Decompose(out var decomposedTranslation, out _, out var decomposedScale);

            Assert.That(decomposedTranslation.x, Is.EqualTo(translation.x).Within(1e-3f));
            Assert.That(decomposedTranslation.y, Is.EqualTo(translation.y).Within(1e-3f));
            Assert.That(Mathf.Abs(decomposedScale.x), Is.EqualTo(scale.x).Within(1e-3f));
            Assert.That(Mathf.Abs(decomposedScale.y), Is.EqualTo(scale.y).Within(1e-3f));
        }

        [Test]
        public void Constructor_ResetsNonFiniteValuesToIdentity()
        {
            LogAssert.Expect(LogType.Error, new Regex("Resetting UV values"));
            var uv = new UVMatrix(new float4(float.NaN, 0, 0, 0), new float4(0, 1, 0, 0));
            AssertVector4(new Vector4(1, 0, 0, 0), uv.U);
            AssertVector4(new Vector4(0, 1, 0, 0), uv.V);
        }
    }
}
