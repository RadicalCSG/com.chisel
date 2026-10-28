using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace Chisel.Editors.Tests
{
    // Every Unity member Chisel reaches through ChiselUnityInternals still exists, in the shape Chisel calls it
    public sealed class ChiselUnityInternalsTests
    {
        const string kTestFeature = "a test of missing members";

        [Test]
        public void EveryFeatureThatNeedsUnityInternals_FindsThem()
        {
            Assert.That(ChiselRectSelection.Valid, Is.True, "rect selection");
            Assert.That(ChiselSceneViewGrid.Available, Is.True, "scene view grid");
            Assert.That(ChiselMeshPicking.Available, Is.True, "mesh picking");
            Assert.That(ChiselLightmappingInternals.Available, Is.True, "lightmapping warnings");
            Assert.That(ChiselUnityInternals.Missing.Where(missing => !missing.Contains(kTestFeature)), Is.Empty);
        }

        // A member Unity no longer has: one warning naming it and the feature, and nothing to use
        [Test]
        public void AMissingMember_LogsOneWarning_AndIsNotUsed()
        {
            LogAssert.Expect(LogType.Warning, "Chisel: this version of Unity has no UnityEditor.SceneView.m_NoSuchMember, so " + kTestFeature + " is turned off");
            Assert.That(ChiselUnityInternals.Field(typeof(SceneView), "m_NoSuchMember", kTestFeature), Is.Null);
            Assert.That(ChiselUnityInternals.Field(typeof(SceneView), "m_NoSuchMember", kTestFeature), Is.Null, "a second time, without another warning");
            Assert.That(ChiselUnityInternals.Missing, Has.Some.Contains("UnityEditor.SceneView.m_NoSuchMember"));
            LogAssert.NoUnexpectedReceived();
        }
    }
}
