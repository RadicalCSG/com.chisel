using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;

namespace Chisel.Core.Tests
{
    [TestFixture]
    public class BrushFromPlanesPrecisionTests
    {
        // what the Source importer passes
        static readonly Bounds kImporterBounds = new Bounds(Vector3.zero, new Vector3(8192, 8192, 8192));

        // well below the CSG's 0.6 mm tolerance, well above float precision ~120 m from the origin
        const double kWorldTolerance = 1e-4;

        // Solid 503923 and Solid 503920 of bm_c0a0a as the importer converts them to Unity space: two tetrahedra
        // about 120 m from the origin that share the vertical face on side 2.
        const int kSharedSide = 2;

        static readonly float4[] kPlanesA =
        {
            new float4(0.0f, 1.0f, 0.0f, -1.1437499523162842f),
            new float4(-0.5765119194984436f, -0.6831666231155396f, -0.4482380151748657f, 68.49883270263672f),
            new float4(0.7071067690849304f, 0.0f, -0.7071067690849304f, 62.32792663574219f),
            new float4(0.6139405965805054f, 0.0f, 0.7893522381782532f, -108.4188461303711f),
        };

        // exact, computed from the map's plane points
        static readonly double3[] kVerticesA =
        {
            new double3(27.52625, 1.14375, 115.67125),
            new double3(26.99298225579761, 1.14375, 116.35712491215742),
            new double3(27.67875, 1.14375, 115.82375),
            new double3(27.67875, 0.915, 115.82375),
        };

        static readonly float4[] kPlanesB =
        {
            new float4(0.0f, 1.0f, 0.0f, -1.1437499523162842f),
            new float4(-0.4483332931995392f, -0.6831745505332947f, -0.5764285326004028f, 79.79852294921875f),
            new float4(-0.7071067690849304f, 0.0f, 0.7071067690849304f, -62.32792663574219f),
            new float4(0.7893522381782532f, 0.0f, 0.6139405965805054f, -92.95718383789062f),
        };

        static readonly double3[] kVerticesB =
        {
            new double3(27.52625, 1.14375, 115.67125),
            new double3(28.2125, 1.14375, 115.1375),
            new double3(27.67875, 1.14375, 115.82375),
            new double3(27.67875, 0.915, 115.82375),
        };

        static BrushMesh CreateFromPlanes(float4[] planes, out ChiselSurfaceArray surfaceArray)
        {
            surfaceArray = new ChiselSurfaceArray();
            surfaceArray.EnsureSize(planes.Length);
            for (int i = 0; i < surfaceArray.surfaces.Length; i++)
                surfaceArray.surfaces[i] = new ChiselSurface();
            BrushMeshFactory.CreateFromPlanes(planes, kImporterBounds, ref surfaceArray, out var brushMesh);
            return brushMesh;
        }

        // the brush the way the importer finishes it, with its vertices moved back into world space
        static double3[] ImportedWorldVertices(float4[] planes, out BrushMesh brushMesh)
        {
            brushMesh = CreateFromPlanes(planes, out var surfaceArray);
            double3 center = (float3)brushMesh.CenterAndSnapPlanes(ref surfaceArray);
            var vertices = new double3[brushMesh.vertices.Length];
            for (int v = 0; v < vertices.Length; v++)
                vertices[v] = (double3)brushMesh.vertices[v] + center;
            return vertices;
        }

        [Test]
        public void EveryFace_KeepsThePlaneItWasCutWith()
        {
            var brushMesh = CreateFromPlanes(kPlanesA, out _);

            Assert.That(brushMesh.polygons.Length, Is.EqualTo(kPlanesA.Length));
            for (int p = 0; p < brushMesh.polygons.Length; p++)
            {
                var expected = kPlanesA[brushMesh.polygons[p].descriptionIndex];
                var actual   = brushMesh.planes[p];
                Assert.That(math.cmax(math.abs(actual.xyz - expected.xyz)), Is.LessThan(1e-6f), $"normal of face {p}");
                Assert.That(math.abs(actual.w - expected.w), Is.LessThan(1e-5f), $"offset of face {p}");
            }
        }

        [Test]
        public void ImportedVertices_AreWhereTheMapsPlanesMeet()
        {
            AssertMatchesExactVertices(kPlanesA, kVerticesA);
            AssertMatchesExactVertices(kPlanesB, kVerticesB);
        }

        [Test]
        public void TwoBrushesSharingAFacePlane_BothEndUpOnIt()
        {
            // the shared plane exactly as the map defines it, seen from brush A
            var shared = (double4)kPlanesA[kSharedSide];

            var verticesA = ImportedWorldVertices(kPlanesA, out var brushA);
            var verticesB = ImportedWorldVertices(kPlanesB, out var brushB);

            AssertFaceLiesOnPlane(brushA, verticesA, kSharedSide,  shared);
            AssertFaceLiesOnPlane(brushB, verticesB, kSharedSide, -shared);
        }

        static void AssertMatchesExactVertices(float4[] planes, double3[] expected)
        {
            var vertices = ImportedWorldVertices(planes, out _);
            Assert.That(vertices.Length, Is.EqualTo(expected.Length));
            foreach (var vertex in vertices)
            {
                var nearest = double.MaxValue;
                foreach (var exact in expected)
                    nearest = math.min(nearest, math.distance(vertex, exact));
                Assert.That(nearest, Is.LessThan(kWorldTolerance), $"vertex {vertex}");
            }
        }

        static void AssertFaceLiesOnPlane(BrushMesh brushMesh, double3[] worldVertices, int side, double4 plane)
        {
            int faceCount = 0;
            for (int p = 0; p < brushMesh.polygons.Length; p++)
            {
                ref var polygon = ref brushMesh.polygons[p];
                if (polygon.descriptionIndex != side)
                    continue;
                faceCount++;
                for (int e = polygon.firstEdge; e < polygon.firstEdge + polygon.edgeCount; e++)
                {
                    var vertex = worldVertices[brushMesh.halfEdges[e].vertexIndex];
                    var distance = math.dot(plane.xyz, vertex) + plane.w;
                    Assert.That(math.abs(distance), Is.LessThan(kWorldTolerance), $"vertex {vertex} of the face on side {side}");
                }
            }
            Assert.That(faceCount, Is.EqualTo(1), $"faces on side {side}");
        }
    }
}
