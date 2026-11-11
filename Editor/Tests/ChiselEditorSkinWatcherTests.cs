using NUnit.Framework;

namespace Chisel.Editors.Tests
{
    // Chisel's icons and copied styles are made for the skin the scripts were loaded in: switching between the light and
    // the dark skin reloads the scripts, once, and not while playing
    [TestFixture]
    public class ChiselEditorSkinWatcherTests
    {
        bool loadedInProSkin;
        bool reloadRequested;

        [SetUp]
        public void SetUp()
        {
            loadedInProSkin = ChiselEditorSkinWatcher.s_LoadedInProSkin;
            reloadRequested = ChiselEditorSkinWatcher.s_ReloadRequested;
            ChiselEditorSkinWatcher.s_ReloadRequested = false;
        }

        [TearDown]
        public void TearDown()
        {
            ChiselEditorSkinWatcher.s_LoadedInProSkin = loadedInProSkin;
            ChiselEditorSkinWatcher.s_ReloadRequested = reloadRequested;
        }

        [TestCase(true)]
        [TestCase(false)]
        public void SwitchingSkins_ReloadsTheScriptsOnce(bool loadedInProSkin)
        {
            ChiselEditorSkinWatcher.s_LoadedInProSkin = loadedInProSkin;
            Assert.That(ChiselEditorSkinWatcher.ReloadIfSkinChanged(loadedInProSkin, isPlaying: false), Is.False, "same skin");
            Assert.That(ChiselEditorSkinWatcher.ReloadIfSkinChanged(!loadedInProSkin, isPlaying: false), Is.True, "the other skin");
            Assert.That(ChiselEditorSkinWatcher.ReloadIfSkinChanged(!loadedInProSkin, isPlaying: false), Is.False, "already asked for");
        }

        [Test]
        public void SwitchingSkinsWhilePlaying_ReloadsTheScriptsAfterwards()
        {
            ChiselEditorSkinWatcher.s_LoadedInProSkin = true;
            Assert.That(ChiselEditorSkinWatcher.ReloadIfSkinChanged(false, isPlaying: true), Is.False, "while playing");
            Assert.That(ChiselEditorSkinWatcher.ReloadIfSkinChanged(false, isPlaying: false), Is.True, "after playing");
        }

        [Test]
        public void SwitchingBackBeforeAReload_ReloadsNothing()
        {
            ChiselEditorSkinWatcher.s_LoadedInProSkin = true;
            Assert.That(ChiselEditorSkinWatcher.ReloadIfSkinChanged(false, isPlaying: true), Is.False, "while playing");
            Assert.That(ChiselEditorSkinWatcher.ReloadIfSkinChanged(true, isPlaying: false), Is.False, "back in the skin it was loaded in");
        }
    }
}
