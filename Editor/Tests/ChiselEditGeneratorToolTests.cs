using NUnit.Framework;

namespace Chisel.Editors.Tests
{
    [TestFixture]
    public sealed class ChiselEditGeneratorToolTests
    {
        // Another tool, like move, leaves every brush's outline shown; the edit tool only hid them when it was activated,
        // which coming back from that tool didn't always do
        [Test]
        public void TheEditGeneratorTool_HidesTheBrushOutlines_LeftOnByAnotherTool()
        {
            var previousMode = ChiselOutlineRenderer.VisualizationMode;
            try
            {
                ChiselOutlineRenderer.VisualizationMode = VisualizationMode.Outline;
                ChiselEditGeneratorTool.HideBrushOutlines();
                Assert.That(ChiselOutlineRenderer.VisualizationMode, Is.EqualTo(VisualizationMode.None));
            }
            finally
            {
                ChiselOutlineRenderer.VisualizationMode = previousMode;
            }
        }
    }
}
