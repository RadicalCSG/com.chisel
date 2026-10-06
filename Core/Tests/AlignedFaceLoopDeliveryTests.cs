using NUnit.Framework;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Chisel.Core.Tests
{
    [TestFixture]
    public class AlignedFaceLoopDeliveryTests
    {
        struct VertexBlob { public BlobArray<float3> vertices; }

        static BlobAssetReference<BrushTreeSpacePlanes> Planes(params float4[] planes)
        {
            using var builder = new BlobBuilder(Allocator.Temp);
            ref var root = ref builder.ConstructRoot<BrushTreeSpacePlanes>();
            var array = builder.Allocate(ref root.treeSpacePlanes, planes.Length);
            for (int i = 0; i < planes.Length; i++)
                array[i] = planes[i];
            return builder.CreateBlobAssetReference<BrushTreeSpacePlanes>(Allocator.Persistent);
        }

        static BlobAssetReference<VertexBlob> Vertices(params float3[] vertices)
        {
            using var builder = new BlobBuilder(Allocator.Temp);
            ref var root = ref builder.ConstructRoot<VertexBlob>();
            var array = builder.Allocate(ref root.vertices, vertices.Length);
            for (int i = 0; i < vertices.Length; i++)
                array[i] = vertices[i];
            return builder.CreateBlobAssetReference<VertexBlob>(Allocator.Persistent);
        }

        // An axis-aligned box as 6 outward tree-space planes: dot(plane, p) <= 0 means inside.
        static float4[] BoxPlanes(float3 min, float3 max)
        {
            return new[]
            {
                new float4(-1,  0,  0,  min.x),
                new float4( 1,  0,  0, -max.x),
                new float4( 0, -1,  0,  min.y),
                new float4( 0,  1,  0, -max.y),
                new float4( 0,  0, -1,  min.z),
                new float4( 0,  0,  1, -max.z),
            };
        }

        // Brush0 is a unit quad lying on z = 0 (plane index 0 is the face under test). Brush1 is a box.
        // Returns how many found-indices the fix added for plane 0.
        static int AddedIndicesForFace(float3[] faceVertices, float4[] brush1Planes,
                                       CategoryIndex category, int preExistingOnPlane0 = 0,
                                       int facePlaneCount1 = -1)
        {
            if (facePlaneCount1 < 0) facePlaneCount1 = brush1Planes.Length;
            var facePlane  = new float4(0, 0, 1, 0);
            var brush0     = Planes(facePlane, new float4(0, 0, -1, 0));
            var brush1     = Planes(brush1Planes);
            var vertexBlob = Vertices(faceVertices);

            var surfaceInfos = new NativeArray<SurfaceInfo>(2, Allocator.Temp);
            surfaceInfos[0] = new SurfaceInfo { basePlaneIndex = 0, interiorCategory = (byte)category };
            surfaceInfos[1] = new SurfaceInfo { basePlaneIndex = 1, interiorCategory = (byte)CategoryIndex.Inside };

            var intersectingPlaneIndices = new NativeArray<int>(new[] { 0, 1 }, Allocator.Temp);
            var foundIndices = new NativeArray<CreateIntersectionLoopsJob.PlaneVertexIndexPair>(64, Allocator.Temp);

            var hashedVertices = new HashedVertices(64, Allocator.Temp);
            var snapVertices   = new HashedVertices(64, Allocator.Temp);

            int foundIndicesLength = 0;
            // pre-seed the plane with vertices, to prove a face that already has a real loop is skipped
            for (int i = 0; i < preExistingOnPlane0; i++)
            {
                var index = hashedVertices.AddNoResize(new float3(-50 - i, -50, 0));
                foundIndices[foundIndicesLength++] = new CreateIntersectionLoopsJob.PlaneVertexIndexPair
                { planeIndex = 0, vertexIndex = index };
            }
            var before = foundIndicesLength;

            var job = new CreateIntersectionLoopsJob();
            job.AddFullSurfaceLoopsForAlignedFaces(
                ref vertexBlob.Value.vertices,
                ref brush0.Value, ref brush1.Value,
                surfaceInfos, 2,
                intersectingPlaneIndices, 2,
                facePlaneCount1,
                foundIndices, ref foundIndicesLength,
                ref hashedVertices, ref snapVertices);

            int addedOnPlane0 = 0;
            for (int i = before; i < foundIndicesLength; i++)
                if (foundIndices[i].planeIndex == 0)
                    addedOnPlane0++;

            snapVertices.Dispose();
            hashedVertices.Dispose();
            foundIndices.Dispose();
            intersectingPlaneIndices.Dispose();
            surfaceInfos.Dispose();
            vertexBlob.Dispose();
            brush1.Dispose();
            brush0.Dispose();
            return addedOnPlane0;
        }

        // a unit quad on z = 0
        static readonly float3[] kQuadOnZ0 =
        {
            new float3(0, 0, 0), new float3(1, 0, 0),
            new float3(1, 1, 0), new float3(0, 1, 0),
        };

        // THE CASE THAT WAS BROKEN: a coplanar face wholly inside the other brush got no loop, so its
        // ReverseAligned category never reached PerformCSGJob and the face was emitted anyway.
        [Test]
        public void AReverseAlignedFaceInsideTheOtherBrush_GetsItsOwnVerticesAsALoop()
        {
            var covering = BoxPlanes(new float3(-1, -1, -1), new float3(2, 2, 1));
            Assert.That(AddedIndicesForFace(kQuadOnZ0, covering, CategoryIndex.ReverseAligned),
                        Is.EqualTo(4), "all four corners of the face should become the loop");
        }

        [Test]
        public void AnAlignedFaceInsideTheOtherBrush_AlsoGetsALoop()
        {
            var covering = BoxPlanes(new float3(-1, -1, -1), new float3(2, 2, 1));
            Assert.That(AddedIndicesForFace(kQuadOnZ0, covering, CategoryIndex.Aligned),
                        Is.EqualTo(4));
        }

        [Test]
        public void AGapToTheCoplanarCounterpart_DoesNotVetoCoverage()
        {
            var covering = BoxPlanes(new float3(-1, -1, -1), new float3(2, 2, 1));
            covering[5] = new float4(0, 0, 1, -0.0005f);   // the coplanar face, a hair outside
            Assert.That(AddedIndicesForFace(kQuadOnZ0, covering, CategoryIndex.ReverseAligned),
                        Is.EqualTo(4));
        }

        // The guard that keeps this from over-removing: a face only partly covered must be left alone,
        // because dropping it whole would open a hole. On bm_c0a0a 17 of 55 pairs are exactly this.
        [Test]
        public void APartlyCoveredFace_IsLeftAlone()
        {
            var partial = BoxPlanes(new float3(-1, -1, -1), new float3(0.5f, 2, 1));
            Assert.That(AddedIndicesForFace(kQuadOnZ0, partial, CategoryIndex.ReverseAligned),
                        Is.EqualTo(0));
        }

        [Test]
        public void AFaceCompletelyOutsideTheOtherBrush_IsLeftAlone()
        {
            var elsewhere = BoxPlanes(new float3(10, 10, 10), new float3(12, 12, 12));
            Assert.That(AddedIndicesForFace(kQuadOnZ0, elsewhere, CategoryIndex.ReverseAligned),
                        Is.EqualTo(0));
        }

        // Only a coplanar pair is short of vertices; anything else already has a proper loop and must
        // not be touched.
        [Test]
        public void AFaceThatIsNotAligned_IsLeftAlone()
        {
            var covering = BoxPlanes(new float3(-1, -1, -1), new float3(2, 2, 1));
            Assert.That(AddedIndicesForFace(kQuadOnZ0, covering, CategoryIndex.Inside), Is.EqualTo(0));
            Assert.That(AddedIndicesForFace(kQuadOnZ0, covering, CategoryIndex.Outside), Is.EqualTo(0));
            Assert.That(AddedIndicesForFace(kQuadOnZ0, covering, CategoryIndex.SelfAligned), Is.EqualTo(0));
        }

        // A face that already collected the 3 vertices GenerateLoop needs has a real crossing loop;
        // adding the whole face on top of it would corrupt that loop.
        [Test]
        public void AFaceThatAlreadyHasALoop_IsLeftAlone()
        {
            var covering = BoxPlanes(new float3(-1, -1, -1), new float3(2, 2, 1));
            Assert.That(AddedIndicesForFace(kQuadOnZ0, covering, CategoryIndex.ReverseAligned,
                                            preExistingOnPlane0: 3),
                        Is.EqualTo(0));
        }

        [Test]
        public void EdgePlanesBeyondTheFaceCount_DoNotVetoContainment()
        {
            var covering = BoxPlanes(new float3(-1, -1, -1), new float3(2, 2, 1));
            // six face planes, then edge planes slicing through the middle of the box; every one of
            // them would report the face as outside if it were treated as a bound
            var withEdgePlanes = new float4[]
            {
                covering[0], covering[1], covering[2], covering[3], covering[4], covering[5],
                new float4(0, 0, 1, -0.5f),
                new float4(1, 0, 0, -0.5f),
                new float4(math.normalize(new float3(1, 1, 0)), -0.25f),
            };

            // sanity: those extra planes really would reject it
            Assert.That(AddedIndicesForFace(kQuadOnZ0, withEdgePlanes, CategoryIndex.ReverseAligned,
                                            facePlaneCount1: withEdgePlanes.Length),
                        Is.EqualTo(0), "the fixture must actually exercise the edge-plane case");

            // with the face count respected, the face is contained as it should be
            Assert.That(AddedIndicesForFace(kQuadOnZ0, withEdgePlanes, CategoryIndex.ReverseAligned,
                                            facePlaneCount1: 6),
                        Is.EqualTo(4));
        }

        [Test]
        public void TheCounterpartIsTheNearestParallelFace_NotTheFirstOneFound()
        {
            const float gap = 0.0008f;
            Assert.That(gap, Is.GreaterThan(CSGConstants.kFatPlaneWidthEpsilon),
                        "the fixture must exceed the containment tolerance, or excluding the wrong face would not matter");

            // box from z = -1 up to z = -gap: the face on z = 0 sits `gap` above its top
            var covering = BoxPlanes(new float3(-1, -1, -1), new float3(2, 2, -gap));
            Assert.That(math.abs(covering[4].z), Is.EqualTo(1f), "the far face must come before the near one");
            Assert.That(covering[5].z, Is.EqualTo(1f));

            Assert.That(AddedIndicesForFace(kQuadOnZ0, covering, CategoryIndex.ReverseAligned),
                        Is.EqualTo(4), "excluding the near coincident face leaves only real bounds in the test");
        }

        // Fewer planes than a tetrahedron cannot bound a volume, so containment is meaningless.
        [Test]
        public void TooFewFacePlanesToBoundAVolume_IsLeftAlone()
        {
            var covering = BoxPlanes(new float3(-1, -1, -1), new float3(2, 2, 1));
            Assert.That(AddedIndicesForFace(kQuadOnZ0, covering, CategoryIndex.ReverseAligned,
                                            facePlaneCount1: 3),
                        Is.EqualTo(0));
        }

        // Two vertices are an edge, not a face; treating one as a surface would remove geometry that
        // was never there.
        [Test]
        public void AFaceWithFewerThanThreeVerticesOnIt_IsLeftAlone()
        {
            var covering = BoxPlanes(new float3(-1, -1, -1), new float3(2, 2, 1));
            var twoOnPlane = new[]
            {
                new float3(0, 0, 0), new float3(1, 0, 0),
                new float3(0, 0, 0.5f),   // off the face
            };
            Assert.That(AddedIndicesForFace(twoOnPlane, covering, CategoryIndex.ReverseAligned),
                        Is.EqualTo(0));
        }
    }
}
