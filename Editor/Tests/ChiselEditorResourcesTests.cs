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
            var first = ChiselEditorResources.GetIconContent(new ChiselIcon("no such icon"), "tip");
            Assert.That(first[0].image, Is.Null);
            Assert.That(first[0].text, Is.EqualTo("no such icon"));
            Assert.That(ChiselEditorResources.s_AnyMissing, Is.True);
            var again = ChiselEditorResources.GetIconContent(new ChiselIcon("no such icon"), "tip");
            Assert.That(again, Is.Not.SameAs(first), "it is looked for again, not taken from the cache");
        }

        [Test]
        public void AnIconThatIsThere_IsKept()
        {
            ChiselEditorResources.ClearCache();
            var first = ChiselEditorResources.GetIconContent(ChiselIcon.Additive, "tip");
            Assert.That(first[0].image, Is.Not.Null, "the package's own icon");
            Assert.That(ChiselEditorResources.GetIconContent(ChiselIcon.Additive, "tip"), Is.SameAs(first));
        }

        // On a new install the package manager may not know the package yet when the scripts first run: its icons are
        // still looked for, and their import is still noticed, by the package's own Unity path, before the project's
        [Test]
        public void ThePackagesIcons_AreLookedForFirst_ByItsUnityPath()
        {
            Assert.That(ChiselEditorResources.kSearchFolders[0] + "/Icons/", Is.EqualTo(kIconsPath));
        }

        // Once icons were missing, the import that brings Chisel's icons has what was made without them made again
        [Test]
        public void ImportingChiselsIcons_AfterSomeWereMissing_IsNoticed()
        {
            ChiselEditorResources.ClearCache();
            Assert.That(ChiselEditorResources.BringsMissingIcons(new[] { kIconsPath + "csg_additive.png" }), Is.False, "nothing was missing");
            ChiselEditorResources.GetIconContent(new ChiselIcon("no such icon"));
            Assert.That(ChiselEditorResources.BringsMissingIcons(new[] { kIconsPath + "csg_additive.png" }), Is.True);
            Assert.That(ChiselEditorResources.BringsMissingIcons(new[] { "Assets/Textures/brick.png" }), Is.False, "not Chisel's");
            Assert.That(ChiselEditorResources.BringsMissingIcons(new[] { kIconsPath + "readme.txt" }), Is.False, "not an image");
        }
    }
}
