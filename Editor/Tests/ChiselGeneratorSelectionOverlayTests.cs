using NUnit.Framework;

namespace Chisel.Editors.Tests
{
    [TestFixture]
    public sealed class ChiselGeneratorSelectionOverlayTests
    {
        // The overlay's buttons wrap to its width: a row of so many buttons fits exactly that many, and a little less
        // fits one fewer. It was always 8 wide, whatever its width
        [Test]
        public void TheGeneratorButtons_WrapToTheWidthTheyHave()
        {
            for (int columns = 1; columns <= 12; columns++)
            {
                var width = ChiselPlacementToolsSelectionWindow.RowWidth(columns);
                Assert.That(ChiselPlacementToolsSelectionWindow.ColumnsThatFit(width), Is.EqualTo(columns), $"a row {width} wide");
                if (columns > 1)
                    Assert.That(ChiselPlacementToolsSelectionWindow.ColumnsThatFit(width - 1), Is.EqualTo(columns - 1), $"a row just under {width} wide");
            }
            Assert.That(ChiselPlacementToolsSelectionWindow.ColumnsThatFit(1), Is.EqualTo(1), "less than one button wide");
        }
    }
}
