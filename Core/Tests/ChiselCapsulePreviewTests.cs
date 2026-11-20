using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;

namespace Chisel.Core.Tests
{
    // While placed, a capsule's preview is the capsule that is made: a band round its middle, with rounded ends
    [TestFixture]
    public class ChiselCapsulePreviewTests
    {
        [Test]
        public void ThePlacementPreview_IsACapsule()
        {
            var tool = ScriptableObject.CreateInstance<ChiselCapsulePlacementTool>();
            // not the same length both ways: as wide and deep as the box
            tool.placement = PlacementFlags.GenerateFromCenterXZ;
            try
            {
                // its ends are as high as a quarter of its width, 0.5
                var bounds = new Bounds(new Vector3(2, 1.5f, -1), new Vector3(2, 3, 4));
                var endHeight = math.min(bounds.size.x, bounds.size.z) * ChiselCapsule.kDefaultHemisphereRatio;
                var renderer = new FakeChiselHandles();
                tool.OnPaint(renderer, bounds);

                Assert.That(renderer.lines, Is.Not.Empty, "a preview is drawn");
                var bandBottom = bounds.min.y + endHeight;
                var bandTop    = bounds.max.y - endHeight;
                float top = float.NegativeInfinity, bottom = float.PositiveInfinity;
                var onAnEnd = 0;
                foreach (var (from, to) in renderer.lines)
                {
                    foreach (var point in new[] { from, to })
                    {
                        var x = (point.x - bounds.center.x) / bounds.extents.x;
                        var z = (point.z - bounds.center.z) / bounds.extents.z;
                        var y = point.y > bandTop    ? (point.y - bandTop) / endHeight
                              : point.y < bandBottom ? (bandBottom - point.y) / endHeight
                              : 0;
                        if (y > 1e-4f)
                            onAnEnd++;
                        Assert.That(math.sqrt(x * x + y * y + z * z), Is.EqualTo(1).Within(1e-4f), $"{point} is on the capsule");
                        top    = math.max(top, point.y);
                        bottom = math.min(bottom, point.y);
                    }
                }
                Assert.That(onAnEnd, Is.GreaterThan(0), "its ends are rounded");
                Assert.That(top,    Is.EqualTo(bounds.max.y).Within(1e-4f), "it reaches the top of the box");
                Assert.That(bottom, Is.EqualTo(bounds.min.y).Within(1e-4f), "it reaches the bottom of the box");
            }
            finally
            {
                Object.DestroyImmediate(tool);
            }
        }
    }
}
