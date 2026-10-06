using NUnit.Framework;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Chisel.Core.Tests
{
    [TestFixture]
    public class AlignedFaceDetectionTests
    {
        // FaceLiesOnPlane takes a BlobArray, so the vertices have to live in a blob
        static BlobAssetReference<VertexBlob> Blob(params float3[] vertices)
        {
            using var builder = new BlobBuilder(Allocator.Temp);
            ref var root = ref builder.ConstructRoot<VertexBlob>();
            var array = builder.Allocate(ref root.vertices, vertices.Length);
            for (int i = 0; i < vertices.Length; i++)
                array[i] = vertices[i];
            return builder.CreateBlobAssetReference<VertexBlob>(Allocator.Persistent);
        }

        struct VertexBlob { public BlobArray<float3> vertices; }

        static bool FaceLiesOnPlane(float4 facePlane, float4 otherPlane, params float3[] vertices)
        {
            var blob = Blob(vertices);
            var result = PrepareBrushPairIntersectionsJob.FaceLiesOnPlane(ref blob.Value.vertices, facePlane, otherPlane);
            blob.Dispose();
            return result;
        }

        static bool FaceLiesOnPlaneWithin(float epsilon, float4 facePlane, float4 otherPlane, params float3[] vertices)
        {
            var blob = Blob(vertices);
            var result = PrepareBrushPairIntersectionsJob.FaceLiesOnPlane(ref blob.Value.vertices, facePlane, otherPlane, epsilon);
            blob.Dispose();
            return result;
        }

        // a unit square on z = 0, far enough from the origin that tilt matters
        static readonly float3[] kFaceOnZ0 =
        {
            new float3(100, 100, 0),
            new float3(101, 100, 0),
            new float3(101, 101, 0),
            new float3(100, 101, 0),
        };

        static readonly float4 kPlaneZ0 = new float4(0, 0, 1, 0);

        [Test]
        public void AFaceLyingExactlyOnTheOtherPlane_Qualifies()
        {
            Assert.That(FaceLiesOnPlane(kPlaneZ0, kPlaneZ0, kFaceOnZ0), Is.True);
        }

        [Test]
        public void AnOppositeFacingCoincidentPlane_Qualifies()
        {
            // the caller negates the plane before asking, so the normals already agree here; what is
            // being pinned is that a flipped-and-negated plane still measures zero distance
            Assert.That(FaceLiesOnPlane(kPlaneZ0, -(-kPlaneZ0), kFaceOnZ0), Is.True);
        }

        [Test]
        public void ATiltedButCoincidentPlane_Qualifies_EvenThoughItsOffsetAtTheOriginDisagrees()
        {
            var tilt = math.normalize(new float3(1e-4f, 0, 1));
            var tiltedPlane = new float4(tilt, -math.dot(tilt, kFaceOnZ0[0]));

            // the offset comparison the original test used would reject this outright
            var offsetDisagreement = math.abs(kPlaneZ0.w - tiltedPlane.w);
            Assert.That(offsetDisagreement, Is.GreaterThan(CSGConstants.kPlaneDAlignEpsilon),
                        "the fixture must actually exercise the tilt case");
            // while the normals are nowhere near far enough apart to be rejected for that
            Assert.That(math.dot(kPlaneZ0.xyz, tiltedPlane.xyz),
                        Is.GreaterThan(CSGConstants.kNormalDotAlignEpsilon));

            Assert.That(FaceLiesOnPlane(kPlaneZ0, tiltedPlane, kFaceOnZ0), Is.True);
        }

        [Test]
        public void AGenuinelyOffsetPlane_DoesNotQualify()
        {
            var farPlane = new float4(0, 0, 1, -1.0f);   // a whole unit away
            Assert.That(FaceLiesOnPlane(kPlaneZ0, farPlane, kFaceOnZ0), Is.False);
        }

        // The bias must stay towards missing a match: a wrong match moves geometry, a missed one
        // only falls back to today's behaviour.
        [Test]
        public void APlaneJustBeyondTheTolerance_DoesNotQualify()
        {
            var justTooFar = new float4(0, 0, 1, -0.02f);
            Assert.That(FaceLiesOnPlane(kPlaneZ0, justTooFar, kFaceOnZ0), Is.False);
        }

        // With canonical vertices, a face 1 mm from another is a real 1 mm step, which both brushes now draw, not
        // one surface: only faces less than τ apart are aligned.
        [Test]
        public void WithCanonicalAlignment_OnlyFacesLessThanTauApartQualify()
        {
            var oneMillimetre = new float4(0, 0, 1, -0.001f);
            var tenthOfAMillimetre = new float4(0, 0, 1, -0.0001f);
            Assert.That(FaceLiesOnPlane(kPlaneZ0, oneMillimetre, kFaceOnZ0), Is.True, "10 mm: aligned");
            Assert.That(FaceLiesOnPlaneWithin(CSGConstants.kFatPlaneWidthEpsilon, kPlaneZ0, oneMillimetre, kFaceOnZ0), Is.False,
                        "τ: a 1 mm step");
            Assert.That(FaceLiesOnPlaneWithin(CSGConstants.kFatPlaneWidthEpsilon, kPlaneZ0, tenthOfAMillimetre, kFaceOnZ0), Is.True,
                        "τ: 0.1 mm is the same surface");
        }

        [Test]
        public void FewerThanThreeVerticesOnTheFace_DoesNotQualify()
        {
            // two vertices define a line, not a face; accepting that would let an edge masquerade
            // as a coplanar surface
            Assert.That(FaceLiesOnPlane(kPlaneZ0, kPlaneZ0,
                                        new float3(100, 100, 0),
                                        new float3(101, 100, 0),
                                        new float3(100, 100, 5)), Is.False);
        }

        [Test]
        public void VerticesNotOnTheFaceAreIgnored_NotCountedAgainstIt()
        {
            // only the vertices lying on facePlane are the face; the rest of the brush is irrelevant
            Assert.That(FaceLiesOnPlane(kPlaneZ0, kPlaneZ0,
                                        new float3(100, 100, 0),
                                        new float3(101, 100, 0),
                                        new float3(101, 101, 0),
                                        new float3(100, 100, 7),     // elsewhere on the brush
                                        new float3(101, 101, 9)), Is.True);
        }
    }
}
