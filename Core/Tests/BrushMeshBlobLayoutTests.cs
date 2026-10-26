using System.Text.RegularExpressions;
using NUnit.Framework;
using Unity.Burst;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;
using UnityEngine.TestTools;

namespace Chisel.Core.Tests
{
    [TestFixture]
    public sealed class BrushMeshBlobLayoutTests
    {
        // Burst-compiled jobs lay out brush blobs exactly as managed code reads them
        [Test]
        public void CompiledLayout_IsTheManagedLayout()
        {
            Assume.That(BurstCompiler.IsEnabled, Is.True, "Burst is off: the compiled layout is the managed one");
            var managed  = BrushMeshBlobLayout.Measure();
            var compiled = BrushMeshBlobLayout.MeasureCompiled();
            Assert.That(BrushMeshBlobLayout.Differences(managed, compiled), Is.Empty);
        }

        [Test]
        public void Measure_ReadsTheManagedStructs()
        {
            var layout = BrushMeshBlobLayout.Measure();
            Assert.That(layout.polygonSize, Is.EqualTo(UnsafeUtility.SizeOf<BrushMeshBlob.Polygon>()));
            Assert.That(layout.surfaceSize, Is.EqualTo(UnsafeUtility.SizeOf<InternalChiselSurface>()));
            Assert.That(layout.detailsSize, Is.EqualTo(UnsafeUtility.SizeOf<SurfaceDetails>()));
            Assert.That(layout.polygonFirstEdge, Is.EqualTo(0));
            Assert.That(layout.polygonSurface + layout.surfaceSize, Is.LessThanOrEqualTo(layout.polygonSize));
            Assert.That(layout.surfaceDetails + layout.detailsSize, Is.LessThanOrEqualTo(layout.surfaceParameters));
        }

        // The 10-26 case: old code wrote 120 byte polygons of 104 byte surfaces, read at 128 and 112
        [Test]
        public void Check_LogsEveryFieldThatDiffers()
        {
            var managed = BrushMeshBlobLayout.Measure();
            var stale   = managed;
            stale.polygonSize       -= 8;
            stale.surfaceParameters -= 8;
            Assert.That(BrushMeshBlobLayout.Differences(managed, stale),
                        Is.EqualTo($"polygonSize {managed.polygonSize}/{stale.polygonSize}, surfaceParameters {managed.surfaceParameters}/{stale.surfaceParameters}"));

            LogAssert.Expect(LogType.Error, new Regex("another brush blob layout.*polygonSize"));
            Assert.That(BrushMeshBlobLayout.Check(managed, stale), Is.False);
            Assert.That(BrushMeshBlobLayout.Check(managed, managed), Is.True);
        }
    }
}
