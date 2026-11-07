using NUnit.Framework;

namespace Chisel.Editors.Tests
{
    [TestFixture]
    public sealed class SurfaceDestinationFlagsDrawerTests
    {
        // Narrow, the toggles go one to a row, which takes more height
        [Test]
        public void Narrow_PutsEachToggleOnItsOwnRow()
        {
            Assert.That(SurfaceDestinationFlagsPropertyDrawer.FitsTwoColumns(1000), Is.True);
            Assert.That(SurfaceDestinationFlagsPropertyDrawer.FitsTwoColumns(60), Is.False);
            var wide   = SurfaceDestinationFlagsPropertyDrawer.HeightFor(1000);
            var narrow = SurfaceDestinationFlagsPropertyDrawer.HeightFor(60);
            Assert.That(narrow / wide, Is.EqualTo(5f / 3f).Within(0.001f));
        }
    }
}
