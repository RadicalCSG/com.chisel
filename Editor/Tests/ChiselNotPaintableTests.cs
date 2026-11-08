using Chisel.Components;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Chisel.Editors.Tests
{
    [TestFixture]
    public sealed class ChiselNotPaintableTests
    {
        // A shader said to be never painted stops its materials asking to be set up as paintable; taken back, they ask again
        [Test]
        public void AShaderNeverPainted_StopsAsking_AndCanBeTakenBack()
        {
            var settings = ScriptableObject.CreateInstance<ChiselPaintSettings>();
            var shader   = Shader.Find("Standard");
            try
            {
                Assert.That(settings.IsNotPaintable(shader), Is.False);
                ChiselPaintSettingsProvider.SetNotPaintable(settings, shader, true);
                ChiselPaintSettingsProvider.SetNotPaintable(settings, shader, true);
                Assert.That(settings.IsNotPaintable(shader), Is.True);
                Assert.That(settings.notPaintable.Count, Is.EqualTo(1), "once");

                Undo.PerformUndo();
                Assert.That(settings.IsNotPaintable(shader), Is.False, "undoable");
                Undo.PerformRedo();
                Assert.That(settings.IsNotPaintable(shader), Is.True);

                // a profile for it makes it paintable whatever the list says
                settings.profiles.Add(new ChiselPaintProfile { shader = shader });
                Assert.That(settings.IsNotPaintable(shader), Is.False);
                settings.profiles.Clear();

                ChiselPaintSettingsProvider.SetNotPaintable(settings, shader, false);
                Assert.That(settings.IsNotPaintable(shader), Is.False);
            }
            finally
            {
                Object.DestroyImmediate(settings);
            }
        }
    }
}
