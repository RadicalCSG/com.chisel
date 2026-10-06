using NUnit.Framework;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Chisel.Core.Tests
{
    [TestFixture]
    public class WeldIncidenceTests
    {
        static BlobAssetReference<BrushTreeSpacePlanes> Planes(params float4[] planes)
        {
            using var builder = new BlobBuilder(Allocator.Temp);
            ref var root = ref builder.ConstructRoot<BrushTreeSpacePlanes>();
            var array = builder.Allocate(ref root.treeSpacePlanes, planes.Length);
            for (int i = 0; i < planes.Length; i++)
                array[i] = planes[i];
            return builder.CreateBlobAssetReference<BrushTreeSpacePlanes>(Allocator.Persistent);
        }

        // A VMF side is written as n·x = d, while Chisel stores n·x + w = 0 with outward normals, so w = -d.
        static float4 Side(double nx, double ny, double nz, double d)
        {
            return new float4((float)nx, (float)ny, (float)nz, (float)-d);
        }

        // Solid 126232, the trim: a 6-sided prism along z. Its face at x = 59.70375 is 19.06 mm tall.
        static float4[] TrimSides()
        {
            return new[]
            {
                Side( 0, 0,  1,  72.20875000000001),
                Side( 0, 0, -1, -28.89875),
                Side(-1, 0,  0, -59.70375000000001),
                Side(-0.11684124756739721,  0.9931506043228763, 0, -11.63311624959858),
                Side(-0.37863284572050415, -0.9255469562056768, 0, -18.247920766075882),
                Side( 0.923076923076923,    0.3846153846153846, 0,  53.621346153846154),
            };
        }

        // Solid 46225, the wall whose sloped underside (side 2 here) cuts the trim's face.
        static float4[] WallSides()
        {
            return new[]
            {
                Side( 0, 0, -1, -28.28875),
                Side( 0, 0,  1,  72.81875),
                Side( 0.1501760908452158, -0.9886592647310041, 0, 13.613243628319662),
                Side( 0, 1, 0, -2.44),
                Side(-1, 0, 0, -58.521875),
                Side( 0.9917428264343938, 0.12824260686651645, 0, 58.93566400823595),
            };
        }

        const float kTrimFaceX      = 59.70375f;
        const float kTrimBottomY    = -4.7084375f;      // the face's bottom edge
        const float kMidZ           = 50f;              // mid-way along the 43 m trim, away from its end caps

        // where the wall's underside crosses the trim's face
        static float3 CutPoint()
        {
            var underside = WallSides()[2];
            float y = -(underside.x * kTrimFaceX + underside.w) / underside.y;
            return new float3(kTrimFaceX, y, kMidZ);
        }

        static float3 CornerPoint() { return new float3(kTrimFaceX, kTrimBottomY, kMidZ); }

        [Test]
        public void TheFixtureMatchesTheMap()
        {
            var cut = CutPoint();
            var corner = CornerPoint();
            Assert.That(cut.y, Is.EqualTo(-4.70047f).Within(1e-4f), "the cut sits where the wall's underside crosses the face");
            Assert.That(math.distance(cut, corner), Is.EqualTo(0.00796f).Within(1e-4f), "7.96 mm of face below the cut");
            Assert.That(math.distance(cut, corner), Is.LessThan(CSGConstants.kVertexEqualEpsilon),
                        "the whole point of the case: the cut is inside the weld radius of the corner");
        }

        [Test]
        public void TheCutAndTheCorner_AreNotMerged()
        {
            var trim = Planes(TrimSides());
            var wall = Planes(WallSides());
            var filter = WeldIncidenceFilter.Create(ref trim.Value.treeSpacePlanes, 6, ref wall.Value.treeSpacePlanes, 6);

            Assert.That(filter.Allows(CutPoint(), CornerPoint()), Is.False,
                        "the corner is 7.9 mm off the wall underside that the cut lies on");
            Assert.That(filter.Allows(CornerPoint(), CutPoint()), Is.False, "and the test is symmetric");

            wall.Dispose();
            trim.Dispose();
        }

        [Test]
        public void ThePlainWeldMergesThem_WhichIsTheDefect()
        {
            using var plain = new HashedVertices(64, Allocator.Temp);
            var corner = plain.AddNoResize(CornerPoint());
            Assert.That(plain.AddNoResize(CutPoint()), Is.EqualTo(corner),
                        "distance alone merges the cut into the corner, which is how the cut is lost");
        }

        [Test]
        public void TheGatedWeld_KeepsThemApart()
        {
            var trim = Planes(TrimSides());
            var wall = Planes(WallSides());
            var filter = WeldIncidenceFilter.Create(ref trim.Value.treeSpacePlanes, 6, ref wall.Value.treeSpacePlanes, 6);

            using var hashed = new HashedVertices(64, Allocator.Temp);
            var corner = hashed.AddNoResize(CornerPoint(), in filter);
            var cut = hashed.AddNoResize(CutPoint(), in filter);
            Assert.That(cut, Is.Not.EqualTo(corner), "the cut must become its own vertex");
            Assert.That(hashed.Length, Is.EqualTo(2));

            wall.Dispose();
            trim.Dispose();
        }

        // The gate must not break what the weld is for: one point computed twice, a fraction of a millimetre apart.
        [Test]
        public void AVertexComputedTwice_IsStillMerged()
        {
            var trim = Planes(TrimSides());
            var wall = Planes(WallSides());
            var filter = WeldIncidenceFilter.Create(ref trim.Value.treeSpacePlanes, 6, ref wall.Value.treeSpacePlanes, 6);

            var cut = CutPoint();
            var again = cut + new float3(0.0002f, -0.0001f, 0.0003f);   // 0.37 mm of float and conditioning noise
            Assert.That(filter.Allows(cut, again), Is.True);

            using var hashed = new HashedVertices(64, Allocator.Temp);
            Assert.That(hashed.AddNoResize(again, in filter), Is.EqualTo(hashed.AddNoResize(cut, in filter)));

            wall.Dispose();
            trim.Dispose();
        }

        static float4[] UnitBoxSides()
        {
            return new[]
            {
                new float4(-1,  0,  0, 0), new float4(1, 0, 0, -1),
                new float4( 0, -1,  0, 0), new float4(0, 1, 0, -1),
                new float4( 0,  0, -1, 0), new float4(0, 0, 1, -1),
            };
        }

        [Test]
        public void AMergeOffAFaceItLiesOn_IsRefused()
        {
            var box = Planes(UnitBoxSides());
            var filter = WeldIncidenceFilter.Create(ref box.Value.treeSpacePlanes, 6);

            var onTop = new float3(0.5f, 1f, 0.5f);
            Assert.That(filter.Allows(onTop, onTop - new float3(0, 0.005f, 0)), Is.False, "5 mm below the face it lies on");
            Assert.That(filter.Allows(onTop, onTop - new float3(0, 0.0005f, 0)), Is.True, "0.5 mm is within the plane tolerance");

            box.Dispose();
        }

        // A plane is infinite, so a point far outside a brush can sit on the plane of one of its faces by accident.
        // Such a point does not belong to the brush and must not be gated by it.
        [Test]
        public void PointsOutsideTheBrush_AreNotGatedByIt()
        {
            var box = Planes(UnitBoxSides());
            var filter = WeldIncidenceFilter.Create(ref box.Value.treeSpacePlanes, 6);

            var outside = new float3(0.5f, 5f, 0.5f);
            Assert.That(filter.Allows(outside, outside + new float3(0, 0.005f, 0)), Is.True);

            box.Dispose();
        }

        [Test]
        public void EdgePlanesBeyondTheFaceCount_AreIgnored()
        {
            var withEdgePlane = new[]
            {
                UnitBoxSides()[0], UnitBoxSides()[1], UnitBoxSides()[2],
                UnitBoxSides()[3], UnitBoxSides()[4], UnitBoxSides()[5],
                new float4(math.normalize(new float3(1, 1, 0)), -0.25f),    // slices through the middle
            };
            var box = Planes(withEdgePlane);
            var onTop = new float3(0.5f, 1f, 0.5f);
            var below = onTop - new float3(0, 0.005f, 0);

            var faces = WeldIncidenceFilter.Create(ref box.Value.treeSpacePlanes, 6);
            Assert.That(faces.Allows(onTop, below), Is.False, "with only the face planes the top face still gates it");

            var everything = WeldIncidenceFilter.Create(ref box.Value.treeSpacePlanes, 7);
            Assert.That(everything.Allows(onTop, below), Is.True,
                        "the fixture must actually exercise the case: counting the edge plane puts the point outside");

            box.Dispose();
        }

        [Test]
        public void TheDisabledFilter_AllowsEverything()
        {
            var filter = WeldIncidenceFilter.Disabled;
            Assert.That(filter.IsEnabled, Is.False);
            Assert.That(filter.Allows(CutPoint(), CornerPoint()), Is.True);
        }
    }
}
