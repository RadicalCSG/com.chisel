using NUnit.Framework;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Chisel.Core.Tests
{
    [TestFixture]
    public class BrushFactoryShapesTests
    {
        // Each face of the square pyramid takes the surface with its own description index
        [Test]
        public void SquarePyramid_EachFaceUsesItsOwnSurface()
        {
            using var surfaceArray = BrushMeshManager.BuildInternalSurfaceArrayBlob(5, Allocator.Temp);
            ref var surfaces = ref surfaceArray.Value.surfaces;
            for (int i = 0; i < surfaces.Length; i++)
                surfaces[i] = new InternalChiselSurface { hotspotLayout = 100 + i };

            var pyramid = BrushMeshFactory.CreateSquarePyramidAssetPolygons(new float3(-1, 0, -1),
                                                                            new float3(-1, 0,  1),
                                                                            new float3( 1, 0,  1),
                                                                            new float3( 1, 0, -1),
                                                                            new float3( 0, 1,  0),
                                                                            in surfaceArray, Allocator.Temp);
            try
            {
                Assert.That(pyramid.IsCreated, Is.True);
                ref var polygons = ref pyramid.Value.polygons;
                Assert.That(polygons.Length, Is.EqualTo(5));
                for (int p = 0; p < polygons.Length; p++)
                    Assert.That(polygons[p].surface.hotspotLayout, Is.EqualTo(100 + polygons[p].descriptionIndex), "face " + p);
            }
            finally
            {
                pyramid.Dispose();
            }
        }
    }
}
