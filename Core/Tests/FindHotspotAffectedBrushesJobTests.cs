using System.Collections.Generic;
using NUnit.Framework;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;

namespace Chisel.Core.Tests
{
    // A hotspot layout edit rebuilds the brushes with a surface on that layout, besides the ones rebuilt anyway
    [TestFixture]
    public class FindHotspotAffectedBrushesJobTests
    {
        readonly List<BlobAssetReference<BasePolygonsBlob>> blobs = new();

        [TearDown]
        public void TearDown()
        {
            foreach (var blob in blobs)
                if (blob.IsCreated) blob.Dispose();
            blobs.Clear();
        }

        // A brush's base polygons with one surface per layout given (0: no hotspot layout)
        BlobAssetReference<BasePolygonsBlob> Brush(params int[] layouts)
        {
            var builder = new BlobBuilder(Allocator.Temp);
            ref var root = ref builder.ConstructRoot<BasePolygonsBlob>();
            builder.Allocate(ref root.polygons, 0);
            builder.Allocate(ref root.edges, 0);
            builder.Allocate(ref root.vertices, 0);
            var surfaces = builder.Allocate(ref root.surfaces, layouts.Length);
            for (int s = 0; s < layouts.Length; s++)
                surfaces[s] = new BaseSurface { hotspotLayout = layouts[s] };
            var blob = builder.CreateBlobAssetReference<BasePolygonsBlob>(Allocator.Persistent);
            builder.Dispose();
            blobs.Add(blob);
            return blob;
        }

        static IndexOrder Order(int nodeOrder) => new() { compactNodeID = new CompactNodeID(default, new SlotIndex { index = nodeOrder + 1, generation = 1 }), nodeOrder = nodeOrder };

        // The node orders listed to rebuild after the job, for these brushes, layouts changed and brushes already listed
        List<int> Rebuilt(BlobAssetReference<BasePolygonsBlob>[] brushes, bool[] changed, params int[] listed)
        {
            using var cache   = new NativeList<BlobAssetReference<BasePolygonsBlob>>(Allocator.TempJob);
            using var orders  = new NativeList<IndexOrder>(Allocator.TempJob);
            using var rebuild = new NativeList<IndexOrder>(Allocator.TempJob);
            using var changedLayouts = new NativeArray<bool>(changed, Allocator.TempJob);
            for (int b = 0; b < brushes.Length; b++)
            {
                cache.Add(brushes[b]);
                orders.Add(Order(b));
            }
            foreach (var b in listed)
                rebuild.Add(Order(b));
            new FindHotspotAffectedBrushesJob
            {
                changedLayouts = changedLayouts,
                basePolygonCache = cache,
                allTreeBrushIndexOrders = orders,
                brushCount = brushes.Length,
                rebuildTreeBrushIndexOrders = rebuild
            }.Run();
            var result = new List<int>();
            foreach (var order in rebuild)
                result.Add(order.nodeOrder);
            return result;
        }

        BlobAssetReference<BasePolygonsBlob>[] Model() => new[]
        {
            Brush(0, 1),    // a surface on layout 1
            Brush(2),       // on layout 2
            Brush(0),       // no hotspot surface
            Brush(1, 2),    // on both
            default         // new: no polygons yet, rebuilt anyway
        };

        [Test]
        public void NoLayoutChanged_ListsNothingMore()
        {
            Assert.That(Rebuilt(Model(), new[] { false, false, false }, 2), Is.EqualTo(new[] { 2 }));
        }

        [Test]
        public void ALayoutChanged_ListsTheBrushesOnIt_InTreeOrder()
        {
            Assert.That(Rebuilt(Model(), new[] { false, true, false }, 2), Is.EqualTo(new[] { 0, 2, 3 }));
            Assert.That(Rebuilt(Model(), new[] { false, false, true }), Is.EqualTo(new[] { 1, 3 }));
        }

        [Test]
        public void ABrushListedAlready_IsListedOnce()
        {
            Assert.That(Rebuilt(Model(), new[] { false, true, true }, 3), Is.EqualTo(new[] { 0, 1, 3 }));
        }

        [Test]
        public void TheNoneLayout_ChangesNothing()
        {
            // index 0 is "no layout": a surface without one is never rebuilt for a layout edit
            Assert.That(Rebuilt(Model(), new[] { true, false, false }), Is.Empty);
        }

        [Test]
        public void ALayoutPastTheChangedOnes_IsLeftAlone()
        {
            // the store knows two layouts here: a surface on a third is not one of them
            Assert.That(Rebuilt(new[] { Brush(3) }, new[] { false, true, true }), Is.Empty);
        }
    }
}
