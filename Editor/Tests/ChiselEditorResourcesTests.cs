using NUnit.Framework;

namespace Chisel.Editors.Tests
{
    [TestFixture]
    public class ChiselEditorResourcesTests
    {
        const string kIconsPath = "Packages/com.chisel/Editor Resources/Icons/";

        [TearDown]
        public void TearDown()
        {
            ChiselEditorResources.ClearCache();
        }

        // On a new install the scripts run before the icons are imported: what they got then isn't kept
        [Test]
        public void AnIconThatIsNotThere_IsLookedForAgain()
        {
            ChiselEditorResources.ClearCache();
            var first = ChiselEditorResources.GetIconContent("no such icon", "tip");
            Assert.That(first[0].image, Is.Null);
            Assert.That(first[0].text, Is.EqualTo("no such icon"));
            Assert.That(ChiselEditorResources.s_AnyMissing, Is.True);
            var again = ChiselEditorResources.GetIconContent("no such icon", "tip");
            Assert.That(again, Is.Not.SameAs(first), "it is looked for again, not taken from the cache");
        }

        [Test]
        public void AnIconThatIsThere_IsKept()
        {
            ChiselEditorResources.ClearCache();
            var first = ChiselEditorResources.GetIconContent("csg_additive", "tip");
            Assert.That(first[0].image, Is.Not.Null, "the package's own icon");
            Assert.That(ChiselEditorResources.GetIconContent("csg_additive", "tip"), Is.SameAs(first));
        }

        // On a new install the package manager may not know the package yet when the scripts first run: its icons are
        // still looked for, and their import is still noticed, by the package's own path
        [Test]
        public void WithoutThePackagesFolder_ItsIconsAreStillLookedFor()
        {
            var paths = ChiselEditorResources.SearchPathsFor(null);
            Assert.That(paths[0].Item1 + "/Icons/", Is.EqualTo(kIconsPath));
            Assert.That(paths[0].Item2, Is.Null, "no folder on disk to check");
        }

        // Once icons were missing, the import that brings Chisel's icons has what was made without them made again
        [Test]
        public void ImportingChiselsIcons_AfterSomeWereMissing_IsNoticed()
        {
            ChiselEditorResources.ClearCache();
            Assert.That(ChiselEditorResources.BringsMissingIcons(new[] { kIconsPath + "csg_additive.png" }), Is.False, "nothing was missing");
            ChiselEditorResources.GetIconContent("no such icon");
            Assert.That(ChiselEditorResources.BringsMissingIcons(new[] { kIconsPath + "csg_additive.png" }), Is.True);
            Assert.That(ChiselEditorResources.BringsMissingIcons(new[] { "Assets/Textures/brick.png" }), Is.False, "not Chisel's");
            Assert.That(ChiselEditorResources.BringsMissingIcons(new[] { kIconsPath + "readme.txt" }), Is.False, "not an image");
        }
    }
}
