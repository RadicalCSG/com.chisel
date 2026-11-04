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
            Assert.That(ChiselMeshPicking.Available, Is.True, "mesh picking");
            Assert.That(ChiselLightmappingInternals.Available, Is.True, "lightmapping warnings");
            Assert.That(ChiselGeneratedPicking.Available, Is.True, "picking off generated objects without undo");
            Assert.That(ChiselUnityInternals.Missing.Where(missing => !missing.Contains(kTestFeature)), Is.Empty);
        }

        // Keeping Unity's picking off the generated objects is no undo step: dropping a material that made new generated
        // objects left one, so two drops took three undos
        [Test]
        public void KeepingGeneratedObjectsOutOfPicking_IsNoUndoStep()
        {
            var getRecords = typeof(Undo).GetMethod("GetRecords", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public,
                                                    null, new[] { typeof(System.Collections.Generic.List<string>), typeof(System.Collections.Generic.List<string>) }, null);
            if (getRecords == null)
                Assert.Ignore("this version of Unity can't list its undo steps");
            int Steps()
            {
                var undo = new System.Collections.Generic.List<string>();
                getRecords.Invoke(null, new object[] { undo, new System.Collections.Generic.List<string>() });
                return undo.Count;
            }

            var container = new GameObject("picking test");
            new GameObject("child").transform.SetParent(container.transform, false);
            try
            {
                Undo.IncrementCurrentGroup();
                var before = Steps();
                Chisel.Components.ChiselGeneratedObjects.KeepOutOfPicking(container);
                Undo.IncrementCurrentGroup();
                Assert.That(SceneVisibilityManager.instance.IsPickingDisabledOnAllDescendants(container), Is.True, "picking is off");
                Assert.That(Steps(), Is.EqualTo(before), "an undo step was added");
            }
            finally { Object.DestroyImmediate(container); }
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
