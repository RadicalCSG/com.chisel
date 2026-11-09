using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Chisel.Editors.Tests
{
    // The Surface Options overlay's material block, and the hotspot diagnostics drawn only where their overlay is
    [TestFixture]
    public class SurfaceOptionsLayoutTests
    {
        // However wide the overlay, the surface flags fit between the material and the physics material: in a narrow one
        // they go one to a row, and the block grows for them
        [Test]
        public void TheSurfaceFlags_FitTheirRoom_AtEveryWidth()
        {
            for (float width = 150; width <= 700; width += 10)
            {
                var position = new Rect(0, 0, width, ChiselUVToolCommon.MaterialPropHeight(width));
                var flags = ChiselUVToolCommon.FlagsRect(position);
                Assert.That(SurfaceDestinationFlagsPropertyDrawer.UsedHeight(flags), Is.LessThanOrEqualTo(flags.height + 0.01f), $"at {width} wide");
            }
        }

        // A view mode left on doesn't colour the scene where its overlay (with the controls to turn it off) isn't shown
        [Test]
        public void HotspotDiagnostics_AreOnlyDrawnWhereTheirOverlayIs()
        {
            var mode = ChiselHotspotDiagnosticsView.Mode;
            var sceneView = EditorWindow.CreateWindow<SceneView>();
            try
            {
                ChiselHotspotDiagnosticsView.Mode = HotspotDiagnosticMode.Distortion;
                Assume.That(sceneView.TryGetOverlay("chisel-hotspot-diagnostics", out var overlay), Is.True);
                overlay.displayed = false;
                Assert.That(ChiselHotspotDiagnosticsView.DrawsIn(sceneView), Is.False);
                overlay.displayed = true;
                Assert.That(ChiselHotspotDiagnosticsView.DrawsIn(sceneView), Is.True);
            }
            finally
            {
                ChiselHotspotDiagnosticsView.Mode = mode;
                sceneView.Close();
            }
        }
    }
}
