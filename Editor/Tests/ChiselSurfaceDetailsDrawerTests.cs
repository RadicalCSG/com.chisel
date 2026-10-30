using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Chisel.Editors.Tests
{
    [TestFixture]
    public class ChiselSurfaceDetailsDrawerTests
    {
        sealed class Holder : ScriptableObject
        {
            public int contents;
        }

        // The inspector draws the contents before the surfaces, and applies what changed after both: the surfaces'
        // drawer reading the object again must not lose the contents chosen just before
        [Test]
        public void ReadingTheObjectAgain_KeepsWhatWasChangedBefore()
        {
            var holder = ScriptableObject.CreateInstance<Holder>();
            try
            {
                using var serializedObject = new SerializedObject(holder);
                serializedObject.FindProperty(nameof(Holder.contents)).intValue = 2;
                SurfaceDetailsPropertyDrawer.Refresh(serializedObject);
                Assert.That(serializedObject.FindProperty(nameof(Holder.contents)).intValue, Is.EqualTo(2));
                Assert.That(holder.contents, Is.EqualTo(2));
            }
            finally
            {
                Object.DestroyImmediate(holder);
            }
        }
    }
}
