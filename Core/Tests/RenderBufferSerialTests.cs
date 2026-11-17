using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;

namespace Chisel.Core.Tests
{
    // A render buffer's serial tells it from every other buffer built (ChiselBrushRenderBuffer.serial), but is no part of
    // what it holds: buffers built the same hash and compare the same, whatever their serials
    public class RenderBufferSerialTests
    {
        static readonly float3[] kSquare = { new(0, 0, 0), new(2, 0, 0), new(2, 2, 0), new(0, 0, 0), new(2, 2, 0), new(0, 2, 0) };
        static readonly float3[] kOther  = { new(0, 0, 1), new(2, 0, 1), new(2, 2, 1), new(0, 0, 1), new(2, 2, 1), new(0, 2, 1) };

        [Test]
        public void BuffersBuiltTheSame_HaveSerialsOfTheirOwn_ButHashAndCompareTheSame()
        {
            var a = OutputMeshCleanupTests.Brush(-1, kSquare, kOther);
            var b = OutputMeshCleanupTests.Brush(-1, kSquare, kOther);
            try
            {
                Assert.That(a.Value.serial, Is.Not.EqualTo(0ul));
                Assert.That(b.Value.serial, Is.Not.EqualTo(a.Value.serial));
                Assert.That(OutputMeshCleanup.KeyOf(b), Is.EqualTo(OutputMeshCleanup.KeyOf(a)), "the cleanup's key");
                Assert.That(OutputHotspotSections.ContentHash(a, out var hashA) && OutputHotspotSections.ContentHash(b, out var hashB) && hashA.Equals(hashB), Is.True, "the cut's hash");
                Assert.That(ChiselBrushRenderBuffer.ContentHash128(a, out var weldA) && ChiselBrushRenderBuffer.ContentHash128(b, out var weldB) && weldA.Equals(weldB), Is.True, "the weld's hash");
                Assert.That(OutputModelWeld.SameBuffer(a, b), Is.True, "the weld's comparison");
            }
            finally
            {
                a.Dispose();
                b.Dispose();
            }
        }

        // The weld's copy of a brush's buffer takes the hotspot groups the cut wrote into the buffer: a copy that changes
        // in place gets a new serial, one that keeps what it had keeps its serial
        [Test]
        public void GroupsWrittenIntoACopyInPlace_GiveItANewSerial_OnlyWhenTheyChange()
        {
            var input  = OutputMeshCleanupTests.Brush(-1, kSquare, kOther);
            var output = OutputMeshCleanupTests.Brush(-1, kSquare, kOther);
            try
            {
                var serial = output.Value.serial;
                OutputModelWeld.KeepHotspotGroups(input, output);
                Assert.That(output.Value.serial, Is.EqualTo(serial), "the same groups");

                input.Value.surfaces[1].hotspot.group = 7;
                OutputModelWeld.KeepHotspotGroups(input, output);
                Assert.That(output.Value.surfaces[1].hotspot.group, Is.EqualTo(7));
                Assert.That(output.Value.serial, Is.Not.EqualTo(serial), "other groups");
            }
            finally
            {
                input.Dispose();
                output.Dispose();
            }
        }
    }
}
