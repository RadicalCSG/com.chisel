using System.Linq;

using NUnit.Framework;

using UnityEditor.ShortcutManagement;

namespace Chisel.Editors.Tests
{
    // Every generator in the create tool has a key that picks it
    public sealed class ChiselGeneratorShortcutsTests
    {
        [Test]
        public void EveryGenerator_HasAShortcut()
        {
            var ids     = ShortcutManager.instance.GetAvailableShortcutIds().ToHashSet();
            var missing = ChiselGeneratorManager.s_GeneratorModes
                                                .Select(mode => ChiselKeyboardDefaults.ShortCutCreateBase + mode.ToolName)
                                                .Where(id => !ids.Contains(id))
                                                .ToArray();
            Assert.That(missing, Is.Empty);
        }

        [Test]
        public void EveryShortcut_PicksAGenerator()
        {
            var names = ChiselGeneratorManager.s_GeneratorModes.Select(mode => mode.ToolName).ToHashSet();
            var ids   = ShortcutManager.instance.GetAvailableShortcutIds()
                                       .Where(id => id.StartsWith(ChiselKeyboardDefaults.ShortCutCreateBase))
                                       .ToArray();
            Assert.That(ids, Is.Not.Empty);
            foreach (var id in ids)
                Assert.That(names, Does.Contain(id.Substring(ChiselKeyboardDefaults.ShortCutCreateBase.Length)), id);
        }

        // without Chisel's tools active the keys are left to Unity
        [Test]
        public void WithoutChiselTools_ShortcutsAreInactive()
        {
            if (UnityEditor.EditorTools.ToolManager.activeContextType == typeof(ChiselToolsContext))
                Assert.Ignore("Chisel's tools are active");
            Assert.That(ChiselGeneratorShortcuts.TagRegistered, Is.False);
        }
    }
}
