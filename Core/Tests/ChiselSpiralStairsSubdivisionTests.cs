using NUnit.Framework;
using Unity.Collections;
using Unity.Entities;

namespace Chisel.Core.Tests
{
    // How many pieces a smooth step's sloped underside is made of is a setting: counting the brushes and building them
    // use the same number, and stairs saved before it could be set keep the 3 they had
    [TestFixture]
    public class ChiselSpiralStairsSubdivisionTests
    {
        static ChiselSpiralStairs Smooth(int subdivisions)
        {
            var settings = ChiselSpiralStairs.DefaultSettings;
            settings.riserType          = StairsRiserType.Smooth;
            settings.smoothSubdivisions = subdivisions;
            Assert.That(settings.Validate(), Is.True);
            return settings;
        }

        static BlobAssetReference<InternalChiselSurfaceArray> Surfaces(int count)
        {
            using var builder = new BlobBuilder(Allocator.Temp);
            ref var root = ref builder.ConstructRoot<InternalChiselSurfaceArray>();
            var surfaces = builder.Allocate(ref root.surfaces, count);
            for (int i = 0; i < count; i++)
                surfaces[i] = InternalChiselSurface.Default;
            return builder.CreateBlobAssetReference<InternalChiselSurfaceArray>(Allocator.Temp);
        }

        // how many brushes it's counted to be made of; each of them is made
        static int Built(ChiselSpiralStairs settings)
        {
            var surfaces = Surfaces(settings.RequiredSurfaceCount);
            var count    = settings.PrepareAndCountRequiredBrushMeshes();
            var nodes    = new NativeList<GeneratedNode>(count, Allocator.Temp);
            try
            {
                nodes.Resize(count, NativeArrayOptions.ClearMemory);
                Assert.That(settings.GenerateNodes(surfaces, nodes, Allocator.Temp), Is.True, "generated");
                for (int i = 0; i < nodes.Length; i++)
                    Assert.That(nodes[i].brushMesh.IsCreated, Is.True, $"brush {i} of {count} is made");
                return count;
            }
            finally
            {
                for (int i = 0; i < nodes.Length; i++)
                    if (nodes[i].brushMesh.IsCreated)
                        nodes[i].brushMesh.Dispose();
                nodes.Dispose();
                surfaces.Dispose();
                settings.Dispose();
            }
        }

        // each subdivision is two pieces under every step
        [Test]
        public void EachSubdivision_IsTwoPiecesUnderEveryStep([Values(2, 3, 5, 8)] int subdivisions)
        {
            var settings = Smooth(subdivisions);
            var steps    = settings.StepCount;
            Assert.That(Built(settings) - Built(Smooth(3)), Is.EqualTo(steps * 2 * (subdivisions - 3)));
        }

        // saved before it could be set, it has none: it gets the 3 it had
        [Test]
        public void StairsSavedBeforeIt_KeepThree()
        {
            var settings = Smooth(0);
            Assert.That(settings.smoothSubdivisions, Is.EqualTo(3));
            Assert.That(Built(settings), Is.EqualTo(Built(Smooth(3))));
        }

        [Test]
        public void ItsRange_IsTwoToEight()
        {
            Assert.That(Smooth(1).smoothSubdivisions,  Is.EqualTo(ChiselSpiralStairs.kMinSmoothSubdivisions));
            Assert.That(Smooth(20).smoothSubdivisions, Is.EqualTo(ChiselSpiralStairs.kMaxSmoothSubdivisions));
            Assert.That(ChiselSpiralStairs.kMinSmoothSubdivisions, Is.EqualTo(2));
            Assert.That(ChiselSpiralStairs.kMaxSmoothSubdivisions, Is.EqualTo(8));
        }
    }
}
