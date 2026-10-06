using NUnit.Framework;
using Unity.Collections;
using Unity.Entities;

namespace Chisel.Core.Tests
{
    [TestFixture]
    public class FixupBrushCacheIndicesTests
    {
        static CompactNodeID Brush(int slot) => new CompactNodeID(default, new SlotIndex { index = slot, generation = 1 });

        static BrushIntersection Touch(CompactNodeID brush, int nodeOrder, IntersectionType type, int bottomUp)
        {
            return new BrushIntersection
            {
                nodeIndexOrder = new IndexOrder { compactNodeID = brush, nodeOrder = nodeOrder },
                type           = type,
                bottomUpStart  = bottomUp,
                bottomUpEnd    = bottomUp + 1
            };
        }

        static BlobAssetReference<BrushesTouchedByBrush> TouchCache(params BrushIntersection[] touches)
        {
            using var builder = new BlobBuilder(Allocator.Temp);
            ref var root = ref builder.ConstructRoot<BrushesTouchedByBrush>();
            var array = builder.Allocate(ref root.brushIntersections, touches.Length);
            for (int i = 0; i < touches.Length; i++)
                array[i] = touches[i];
            builder.Allocate(ref root.intersectionBits, 1);
            return builder.CreateBlobAssetReference<BrushesTouchedByBrush>(Allocator.Persistent);
        }

        [Test]
        public void AReorderedTouchCache_KeepsEachBrushWithItsOwnIntersection()
        {
            var owner = Brush(10);
            var x     = Brush(11);
            var y     = Brush(12);

            // The cache was built when the order was owner, x, y; x is behind y now
            var cache        = TouchCache(Touch(x, 1, IntersectionType.AInsideB, 100),
                                          Touch(y, 2, IntersectionType.BInsideA, 200));
            var orders       = new NativeList<IndexOrder>(Allocator.TempJob);
            var idToOrder    = new NativeList<int>(Allocator.TempJob);
            var offset       = new NativeReference<int>(10, Allocator.TempJob);
            var basePolygons = new NativeList<BlobAssetReference<BasePolygonsBlob>>(Allocator.TempJob);
            var touchCaches  = new NativeList<BlobAssetReference<BrushesTouchedByBrush>>(Allocator.TempJob);
            try
            {
                orders.Add(new IndexOrder { compactNodeID = owner, nodeOrder = 0 });
                orders.Add(new IndexOrder { compactNodeID = y,     nodeOrder = 1 });
                orders.Add(new IndexOrder { compactNodeID = x,     nodeOrder = 2 });
                idToOrder.Add(0);   // slot 10, owner
                idToOrder.Add(2);   // slot 11, x
                idToOrder.Add(1);   // slot 12, y
                basePolygons.Add(default);
                touchCaches.Add(cache);

                new FixupBrushCacheIndicesJob
                {
                    allTreeBrushIndexOrders         = orders,
                    nodeIDValueToNodeOrder          = idToOrder,
                    nodeIDValueToNodeOrderOffsetRef = offset,
                    basePolygonCache                = basePolygons,
                    brushesTouchedByBrushCache      = touchCaches
                }.Execute(0);

                ref var touches = ref cache.Value.brushIntersections;
                Assert.That(touches[0].nodeIndexOrder.compactNodeID, Is.EqualTo(y));
                Assert.That(touches[0].nodeIndexOrder.nodeOrder,     Is.EqualTo(1));
                Assert.That(touches[0].type,                         Is.EqualTo(IntersectionType.BInsideA));
                Assert.That(touches[0].bottomUpStart,                Is.EqualTo(200));
                Assert.That(touches[1].nodeIndexOrder.compactNodeID, Is.EqualTo(x));
                Assert.That(touches[1].nodeIndexOrder.nodeOrder,     Is.EqualTo(2));
                Assert.That(touches[1].type,                         Is.EqualTo(IntersectionType.AInsideB));
                Assert.That(touches[1].bottomUpStart,                Is.EqualTo(100));
            }
            finally
            {
                cache.Dispose();
                orders.Dispose();
                idToOrder.Dispose();
                offset.Dispose();
                basePolygons.Dispose();
                touchCaches.Dispose();
            }
        }
    }
}
