using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;

namespace Chisel.Core.Tests
{
    // The paint a tree keeps between updates (ChiselPaintStore): replaced an entity at a time
    [TestFixture]
    public sealed class ChiselPaintStoreTests
    {
        CSGTree tree;

        [SetUp]
        public void SetUp() { tree = CSGTree.Create(default(UnityEngine.EntityId)); }

        [TearDown]
        public void TearDown()
        {
            if (tree.Valid)
            {
                ChiselTreeLookup.Value[tree].lastJobHandle.Complete();
                tree.Destroy();
            }
        }

        // A 2 x 2 grid on a surface, every point at value
        static bool Paint(CSGTree tree, ulong entityID, int surfaceIndex, float value)
        {
            NativeArray<ChiselPaintLayer> layers;
            using var _layers = layers = new NativeArray<ChiselPaintLayer>(1, Allocator.Temp);
            NativeArray<float> values;
            using var _values = values = new NativeArray<float>(4 * 4, Allocator.Temp);
            layers[0] = new ChiselPaintLayer { surfaceIndex = surfaceIndex, attribute = ChiselPaintAttribute.Color, components = ChiselPaintComponents.XYZW,
                                               cellU = new float3(1, 0, 0), cellV = new float3(0, 0, 1), resolution = new int2(2, 2) };
            for (int i = 0; i < values.Length; i++)
                values[i] = value;
            return ChiselPaintStore.SetPaint(tree, entityID, layers, values);
        }

        static float ValueOf(CSGTree tree, ulong entityID, int surfaceIndex)
        {
            var lookup = ChiselPaintLookup.Of(ChiselTreeLookup.Value[tree]);
            Assert.That(lookup.TryGetRange(entityID, surfaceIndex, out var range), Is.True, $"surface {surfaceIndex} of {entityID}");
            return lookup.values[lookup.layers[range.start].valueStart];
        }

        [Test]
        public void AnEntitysPaint_IsReplacedWithoutTouchingTheOthers()
        {
            Assert.That(Paint(tree, 1, 0, 0.25f), Is.True);
            Assert.That(Paint(tree, 2, 3, 0.5f), Is.True);
            Assert.That(Paint(tree, 1, 0, 0.25f), Is.False, "the same paint: no change");

            Assert.That(Paint(tree, 1, 0, 0.75f), Is.True);
            Assert.That(ValueOf(tree, 1, 0), Is.EqualTo(0.75f));
            Assert.That(ValueOf(tree, 2, 3), Is.EqualTo(0.5f));

            using var none = new NativeArray<ChiselPaintLayer>(0, Allocator.Temp);
            Assert.That(ChiselPaintStore.SetPaint(tree, 1, none, default), Is.True, "its paint removed");
            Assert.That(ChiselPaintLookup.Of(ChiselTreeLookup.Value[tree]).TryGetRange(1, 0, out _), Is.False);
            Assert.That(ValueOf(tree, 2, 3), Is.EqualTo(0.5f));
        }

        // Values of replaced layers are dropped once they are half of all of them
        [Test]
        public void ReplacedValues_AreCompactedAway()
        {
            Paint(tree, 1, 0, 0);
            for (int i = 1; i <= 10; i++)
                Paint(tree, 1, 0, i);
            var data = ChiselTreeLookup.Value[tree];
            Assert.That(data.paintValues.Length, Is.LessThanOrEqualTo(2 * 4 * 4));
            Assert.That(ValueOf(tree, 1, 0), Is.EqualTo(10));
        }
    }
}
