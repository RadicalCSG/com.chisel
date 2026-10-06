using System.Collections.Generic;
using NUnit.Framework;
using Unity.Mathematics;

namespace Chisel.Core.Tests
{
    // The geometry decals are made of (Documentation~/Design/Decals.md): the volume, the projection, and cutting
    // surface triangles by the volume. Pure functions, no CSG.
    public sealed class DecalClippingTests
    {
        const double kAreaTolerance = 1e-9;

        struct ListSink : IDecalPieceSink
        {
            public List<(DecalPiece piece, bool inside)> pieces;
            public void Add(in DecalPiece piece, bool inside) => pieces.Add((piece, inside));
        }

        static ChiselDecalInstance Decal(float3 size, float4x4 decalToTree, bool transparent = true,
                                         ChiselDecalProjection projection = ChiselDecalProjection.Orthographic,
                                         float fieldOfView = 30, float maxAngle = 180)
        {
            var settings = ChiselDecalSettings.Default;
            settings.size        = size;
            settings.transparent = transparent;
            settings.projection  = projection;
            settings.fieldOfView = fieldOfView;
            settings.maxAngle    = maxAngle;
            return new ChiselDecalInstance
            {
                entityID         = 1,
                decalToTree      = decalToTree,
                settings         = settings,
                renderMaterial   = 42,
                destinationFlags = SurfaceDestinationFlags.Default
            };
        }

        static DecalVolume Volume(ChiselDecalInstance instance)
        {
            Assert.That(DecalVolume.TryCreate(instance, out var volume), Is.True, "the decal should make a volume");
            return volume;
        }

        static DecalVolume UnitBox(bool transparent = true) => Volume(Decal(new float3(1, 1, 1), float4x4.identity, transparent));

        static DecalPiece Triangle(double3 a, double3 b, double3 c)
        {
            return new DecalPiece
            {
                v0 = new DecalClipVertex { position = a, barycentric = new double3(1, 0, 0) },
                v1 = new DecalClipVertex { position = b, barycentric = new double3(0, 1, 0) },
                v2 = new DecalClipVertex { position = c, barycentric = new double3(0, 0, 1) },
                owner = -1
            };
        }

        static double Area(in DecalPiece piece)
        {
            return math.length(math.cross(piece.v1.position - piece.v0.position, piece.v2.position - piece.v0.position)) * 0.5;
        }

        static double3 Normal(in DecalPiece piece)
        {
            return math.cross(piece.v1.position - piece.v0.position, piece.v2.position - piece.v0.position);
        }

        static List<(DecalPiece piece, bool inside)> Split(in DecalVolume volume, in DecalPiece triangle)
        {
            var sink = new ListSink { pieces = new List<(DecalPiece, bool)>() };
            var split = DecalClipping.Split(volume, triangle, ref sink, out var failure);
            var corners = $"{triangle.v0.position} {triangle.v1.position} {triangle.v2.position}";
            Assert.That(split, Is.True, () => $"the split failed ({failure}) for the triangle {corners}");
            return sink.pieces;
        }

        // The pieces must cover the triangle exactly, keep its winding, and lie on the side of the volume they say
        static void AssertValidSplit(in DecalVolume volume, in DecalPiece triangle, List<(DecalPiece piece, bool inside)> pieces,
                                     out double insideArea)
        {
            insideArea = 0;
            var total = 0.0;
            var normal = Normal(triangle);
            foreach (var (piece, inside) in pieces)
            {
                var area = Area(piece);
                total += area;
                Assert.That(math.dot(Normal(piece), normal), Is.GreaterThan(0), "a piece is turned over");
                var center = (piece.v0.position + piece.v1.position + piece.v2.position) / 3.0;
                if (inside)
                {
                    insideArea += area;
                    Assert.That(volume.Contains(piece.v0.position) && volume.Contains(piece.v1.position) && volume.Contains(piece.v2.position),
                                Is.True, "an inside piece reaches outside the volume");
                } else
                    Assert.That(volume.Contains(center) && !OnBoundary(volume, center), Is.False, "an outside piece has its center inside the volume");

                // Every point keeps the barycentric of where it is in the triangle
                foreach (var vertex in new[] { piece.v0, piece.v1, piece.v2 })
                {
                    var b = vertex.barycentric;
                    var expected = b.x * triangle.v0.position + b.y * triangle.v1.position + b.z * triangle.v2.position;
                    Assert.That(math.distance(expected, vertex.position), Is.LessThan(1e-9), "a point lost its barycentric");
                }
            }
            Assert.That(total, Is.EqualTo(Area(triangle)).Within(kAreaTolerance + 1e-9 * Area(triangle)), "the pieces don't cover the triangle");
        }

        static bool OnBoundary(in DecalVolume volume, double3 point)
        {
            for (int i = 0; i < DecalPlanes.kCount; i++)
            {
                if (math.abs(volume.Distance(i, point)) <= DecalVolume.kPlaneEpsilon)
                    return true;
            }
            return false;
        }

        #region Volume
        [Test]
        public void UnitBox_ContainsWhatIsInsideIt()
        {
            var volume = UnitBox();
            Assert.That(volume.Contains(new double3(0, 0, 0)), Is.True);
            Assert.That(volume.Contains(new double3(0.49, -0.49, 0.49)), Is.True);
            Assert.That(volume.Contains(new double3(0.5, 0.5, 0.5)), Is.True, "the boundary is inside");
            Assert.That(volume.Contains(new double3(0.51, 0, 0)), Is.False);
            Assert.That(volume.Contains(new double3(0, 0, -0.51)), Is.False);
            for (int i = 0; i < DecalPlanes.kCount; i++)
                Assert.That(math.length(volume.planes[i].xyz), Is.EqualTo(1.0).Within(1e-12));
            Assert.That(volume.boundsMin, Is.EqualTo(new float3(-0.5f) - (float)DecalVolume.kPlaneEpsilon));
        }

        [Test]
        public void Decal_WithoutMaterialOrSizeOrInverse_MakesNoVolume()
        {
            var noMaterial = Decal(new float3(1), float4x4.identity);
            noMaterial.renderMaterial = 0;
            Assert.That(DecalVolume.TryCreate(noMaterial, out _), Is.False);

            var flat = Decal(new float3(1, 1, 0), float4x4.identity);
            Assert.That(DecalVolume.TryCreate(flat, out _), Is.False);

            var squashed = Decal(new float3(1), float4x4.Scale(1, 1, 0));
            Assert.That(DecalVolume.TryCreate(squashed, out _), Is.False);

            var hidden = Decal(new float3(1), float4x4.identity);
            hidden.destinationFlags = SurfaceDestinationFlags.Collidable;
            Assert.That(DecalVolume.TryCreate(hidden, out _), Is.False, "a material that isn't rendered draws nothing");
        }

        [Test]
        public void OrthographicImage_SpansTheBox_AtEveryDepth()
        {
            var volume = Volume(Decal(new float3(2, 4, 1), float4x4.identity));
            Assert.That(volume.ProjectImage(new double3(0, 0, 0)), Is.EqualTo(new double2(0.5, 0.5)));
            Assert.That(volume.ProjectImage(new double3(1, 2, 0.4)), Is.EqualTo(new double2(1, 1)));
            Assert.That(volume.ProjectImage(new double3(-1, -2, -0.4)), Is.EqualTo(new double2(0, 0)));
            Assert.That(volume.ProjectImage(new double3(0.5, -1, 0.3)), Is.EqualTo(new double2(0.75, 0.25)));
        }

        [Test]
        public void UVScaleAndOffset_MapTheImage()
        {
            var instance = Decal(new float3(1), float4x4.identity);
            instance.settings.uvScale  = new float2(-1, 2);
            instance.settings.uvOffset = new float2(1, 0.5f);
            var volume = Volume(instance);
            Assert.That(volume.ProjectUV(new double3(-0.5, -0.5, 0)), Is.EqualTo(new float2(1, 0.5f)));
            Assert.That(volume.ProjectUV(new double3(0.5, 0.5, 0)), Is.EqualTo(new float2(0, 2.5f)));
        }

        [Test]
        public void TransformedDecal_MatchesItsTransformation()
        {
            var decalToTree = float4x4.TRS(new float3(3, -2, 5), quaternion.EulerXYZ(0.3f, 1.1f, -0.7f), new float3(2, 0.5f, 3));
            var volume = Volume(Decal(new float3(1, 1, 1), decalToTree));
            var random = new Random(7);
            for (int i = 0; i < 200; i++)
            {
                var local  = random.NextFloat3(-0.7f, 0.7f);
                var tree   = math.transform(decalToTree, local);
                var inside = math.all(math.abs(local) <= 0.5f);
                var nearBoundary = math.any(math.abs(math.abs(local) - 0.5f) < 1e-3f);
                if (!nearBoundary)
                    Assert.That(volume.Contains(tree), Is.EqualTo(inside), $"local {local}");
                var image = volume.ProjectImage(tree);
                Assert.That(image.x, Is.EqualTo(local.x + 0.5).Within(1e-5));
                Assert.That(image.y, Is.EqualTo(local.y + 0.5).Within(1e-5));
            }
        }

        [Test]
        public void PerspectiveImage_HasTheBoxSizeAtItsCenter_AndGrowsWithDepth()
        {
            var volume = Volume(Decal(new float3(2, 2, 2), float4x4.identity, projection: ChiselDecalProjection.Perspective, fieldOfView: 90));
            // fov 90 with a half height of 1: the apex is 1 behind the center plane, at z = -1
            Assert.That(volume.focal, Is.EqualTo(1.0).Within(1e-12));
            Assert.That(volume.ProjectImage(new double3(1, 1, 0)).x, Is.EqualTo(1.0).Within(1e-12));
            // Twice as far from the apex, the image is twice as large
            Assert.That(volume.ProjectImage(new double3(2, 0, 1)).x, Is.EqualTo(1.0).Within(1e-12));
            Assert.That(volume.Contains(new double3(1.9, 1.9, 1)), Is.True, "the far face is wider");
            Assert.That(volume.Contains(new double3(1.1, 0, 0)), Is.False);
            // The near face (z = -1) would sit on the apex; it is moved just in front of it
            Assert.That(volume.Contains(new double3(0, 0, -0.9)), Is.True);
            Assert.That(volume.Contains(new double3(0.2, 0, -0.9)), Is.False, "near the apex the frustum is narrow");
        }

        [Test]
        public void Surface_FacingAwayFromTheCenter_DoesNotTakeTheDecal()
        {
            var volume = UnitBox();
            // A floor at the bottom of the box, facing up at the center
            Assert.That(volume.Accepts(new double3(0, 1, 0), new double3(0, -0.4, 0)), Is.True);
            // Its underside
            Assert.That(volume.Accepts(new double3(0, -1, 0), new double3(0, -0.4, 0)), Is.False);
            // A surface through the center faces it either way
            Assert.That(volume.Accepts(new double3(0, -1, 0), new double3(0, 0, 0)), Is.True);
            // Without an angle limit a wall at the side takes it too
            Assert.That(volume.Accepts(new double3(-1, 0, 0), new double3(0.4, 0, 0)), Is.True);
        }

        [Test]
        public void AngleLimit_SkipsSteepSurfaces()
        {
            // Projecting along +Z: a surface facing -Z faces the projector
            var volume = Volume(Decal(new float3(1), float4x4.identity, maxAngle: 45));
            Assert.That(volume.Accepts(new double3(0, 0, -1), new double3(0, 0, 0.4)), Is.True);
            Assert.That(volume.Accepts(math.normalize(new double3(0, 0.5, -1)), new double3(0, 0, 0)), Is.True);
            Assert.That(volume.Accepts(math.normalize(new double3(0, 1.5, -1)), new double3(0, 0, 0)), Is.False);
            Assert.That(volume.Accepts(new double3(-1, 0, 0), new double3(0.4, 0, 0)), Is.False);
        }

        [Test]
        public void DestinationFlags_FollowTheSurface()
        {
            var transparent = UnitBox(transparent: true);
            var opaque      = UnitBox(transparent: false);
            var surface     = SurfaceDestinationFlags.Default;
            Assert.That(transparent.GetDestinationFlags(surface),
                        Is.EqualTo(SurfaceDestinationFlags.RenderShadowsReceiving | SurfaceDestinationFlags.ExcludedFromGlobalIllumination),
                        "a transparent decal neither casts shadows nor collides, so it is out of the baked lighting too");
            Assert.That(opaque.GetDestinationFlags(surface), Is.EqualTo(SurfaceDestinationFlags.Default),
                        "an opaque decal stands in for the surface it replaces, baked lighting and all");
            Assert.That(opaque.GetDestinationFlags(SurfaceDestinationFlags.Renderable),
                        Is.EqualTo(SurfaceDestinationFlags.Renderable | SurfaceDestinationFlags.ExcludedFromGlobalIllumination),
                        "a surface that casts no shadows keeps the decal out of the bake as well");
            Assert.That(opaque.GetDestinationFlags(SurfaceDestinationFlags.Collidable), Is.EqualTo(SurfaceDestinationFlags.None),
                        "a surface that isn't rendered gets no decal");
        }
        #endregion

        #region Edge points
        [Test]
        public void EdgeThroughTheBox_GetsItsEntryAndExit_InOrder()
        {
            var volume = UnitBox();
            var a = new DecalClipVertex { position = new double3(-2, 0, 0), barycentric = new double3(1, 0, 0) };
            var b = new DecalClipVertex { position = new double3( 2, 0, 0), barycentric = new double3(0, 1, 0) };
            Assert.That(DecalClipping.GetEdgePoints(volume, a, b, out var first, out var second), Is.EqualTo(2));
            Assert.That(first.position.x, Is.EqualTo(-0.5).Within(1e-12));
            Assert.That(second.position.x, Is.EqualTo(0.5).Within(1e-12));
            Assert.That(first.barycentric, Is.EqualTo(new double3(0.625, 0.375, 0)));

            Assert.That(DecalClipping.GetEdgePoints(volume, b, a, out var backFirst, out var backSecond), Is.EqualTo(2));
            Assert.That(backFirst.position.x, Is.EqualTo(0.5).Within(1e-12), "walked backwards, the exit comes first");
            Assert.That(backSecond.position.x, Is.EqualTo(-0.5).Within(1e-12));
        }

        [Test]
        public void EdgePoints_AreTheSame_WhicheverWayTheEdgeIsWalked()
        {
            var volume = Volume(Decal(new float3(1.3f, 0.7f, 0.9f),
                                      float4x4.TRS(new float3(0.1f, 0.2f, -0.3f), quaternion.EulerXYZ(0.2f, 0.4f, 0.8f), new float3(1))));
            var random = new Random(11);
            var checkedCount = 0;
            for (int i = 0; i < 500; i++)
            {
                var a = new DecalClipVertex { position = random.NextFloat3(-2, 2), barycentric = new double3(1, 0, 0) };
                var b = new DecalClipVertex { position = random.NextFloat3(-2, 2), barycentric = new double3(0, 1, 0) };
                var forward  = DecalClipping.GetEdgePoints(volume, a, b, out var f0, out var f1);
                var backward = DecalClipping.GetEdgePoints(volume, b, a, out var b0, out var b1);
                Assert.That(backward, Is.EqualTo(forward));
                if (forward == 1)
                    Assert.That(f0.position.Equals(b0.position), Is.True, "bit for bit");
                if (forward == 2)
                {
                    Assert.That(f0.position.Equals(b1.position), Is.True, "bit for bit");
                    Assert.That(f1.position.Equals(b0.position), Is.True, "bit for bit");
                    checkedCount++;
                }
            }
            Assert.That(checkedCount, Is.GreaterThan(10), "too few edges crossed the box to mean anything");
        }

        [Test]
        public void EdgeOutsideOrInside_GetsNoPoints()
        {
            var volume = UnitBox();
            var outside = DecalClipping.GetEdgePoints(volume,
                new DecalClipVertex { position = new double3(-2, 1, 0) }, new DecalClipVertex { position = new double3(2, 1, 0) }, out _, out _);
            Assert.That(outside, Is.EqualTo(0));
            var inside = DecalClipping.GetEdgePoints(volume,
                new DecalClipVertex { position = new double3(-0.2, 0, 0) }, new DecalClipVertex { position = new double3(0.2, 0, 0) }, out _, out _);
            Assert.That(inside, Is.EqualTo(0));
            var endingOnTheBoundary = DecalClipping.GetEdgePoints(volume,
                new DecalClipVertex { position = new double3(-2, 0, 0) }, new DecalClipVertex { position = new double3(-0.5, 0, 0) }, out _, out _);
            Assert.That(endingOnTheBoundary, Is.EqualTo(0), "an end on the boundary is no new point");
        }

        [Test]
        public void EdgeTouchingACorner_GetsTheTouchingPoint()
        {
            var volume = UnitBox();
            // Passes through the box's edge at (0.5, 0.5, z) only
            var count = DecalClipping.GetEdgePoints(volume,
                new DecalClipVertex { position = new double3(0, 1, 0) }, new DecalClipVertex { position = new double3(1, 0, 0) }, out var point, out _);
            Assert.That(count, Is.EqualTo(1));
            Assert.That(math.distance(point.position, new double3(0.5, 0.5, 0)), Is.LessThan(1e-9));
        }
        #endregion

        #region Split
        [Test]
        public void TriangleInside_IsAllInside()
        {
            var volume = UnitBox();
            var triangle = Triangle(new double3(-0.2, 0, 0), new double3(0, 0.3, 0), new double3(0.2, 0, 0));
            var pieces = Split(volume, triangle);
            AssertValidSplit(volume, triangle, pieces, out var inside);
            Assert.That(inside, Is.EqualTo(Area(triangle)).Within(kAreaTolerance));
        }

        [Test]
        public void TriangleOutside_StaysWhole()
        {
            var volume = UnitBox();
            var triangle = Triangle(new double3(2, 0, 0), new double3(2, 1, 0), new double3(3, 0, 0));
            var pieces = Split(volume, triangle);
            Assert.That(pieces.Count, Is.EqualTo(1));
            Assert.That(pieces[0].inside, Is.False);
            Assert.That(pieces[0].piece.v0.position, Is.EqualTo(triangle.v0.position));
        }

        [Test]
        public void TriangleAroundTheBox_KeepsARingAroundIt()
        {
            var volume = UnitBox();
            // A floor triangle (in the XZ plane at y = 0) far larger than the box
            var triangle = Triangle(new double3(-10, 0, -10), new double3(0, 0, 10), new double3(10, 0, -10));
            var pieces = Split(volume, triangle);
            AssertValidSplit(volume, triangle, pieces, out var inside);
            Assert.That(inside, Is.EqualTo(1.0).Within(1e-9), "the box's cross section is 1 by 1");
        }

        [Test]
        public void TriangleOverTheBoxCorner_SplitsAlongTheBox()
        {
            var volume = UnitBox();
            // The triangle's corner (0,0,0) is at the box's center: a quarter of the box's cross section is inside
            var triangle = Triangle(new double3(0, 0, 0), new double3(0, 0, 4), new double3(4, 0, 0));
            var pieces = Split(volume, triangle);
            AssertValidSplit(volume, triangle, pieces, out var inside);
            // Inside: the part of the triangle with x <= 0.5 and z <= 0.5, which is the square [0,0.5]^2
            Assert.That(inside, Is.EqualTo(0.25).Within(1e-9));
        }

        [Test]
        public void TriangleThroughTheBox_SplitsIntoThree()
        {
            var volume = UnitBox();
            // A long thin triangle crossing the box from side to side
            var triangle = Triangle(new double3(-3, 0, -0.2), new double3(3, 0, 0.2), new double3(3, 0, -0.2));
            var pieces = Split(volume, triangle);
            AssertValidSplit(volume, triangle, pieces, out var inside);
            var expected = 0.0;
            for (int i = 0; i < 1000; i++)
            {
                var x = -0.5 + (i + 0.5) / 1000.0;
                var top = -0.2 + (x + 3) / 6.0 * 0.4;
                expected += (top + 0.2) / 1000.0;
            }
            Assert.That(inside, Is.EqualTo(expected).Within(1e-6));
        }

        [Test]
        public void TriangleFacingEitherWay_KeepsItsWinding()
        {
            var volume = UnitBox();
            var front = Triangle(new double3(-3, 0, -3), new double3(0, 0, 3), new double3(3, 0, -3));
            var back  = Triangle(new double3(-3, 0, -3), new double3(3, 0, -3), new double3(0, 0, 3));
            AssertValidSplit(volume, front, Split(volume, front), out var frontInside);
            AssertValidSplit(volume, back, Split(volume, back), out var backInside);
            Assert.That(frontInside, Is.EqualTo(1.0).Within(1e-9));
            Assert.That(backInside, Is.EqualTo(1.0).Within(1e-9));
        }

        [Test]
        public void RandomTrianglesAndDecals_SplitValidly()
        {
            var random = new Random(1234);
            var splits = 0;
            for (int i = 0; i < 400; i++)
            {
                var decalToTree = float4x4.TRS(random.NextFloat3(-1, 1),
                                               quaternion.EulerXYZ(random.NextFloat3(-3, 3)),
                                               random.NextFloat3(0.5f, 2));
                var perspective = (i % 3) == 0;
                var volume = Volume(Decal(random.NextFloat3(0.3f, 2), decalToTree,
                                          projection: perspective ? ChiselDecalProjection.Perspective : ChiselDecalProjection.Orthographic,
                                          fieldOfView: random.NextFloat(10, 120)));
                var triangle = Triangle(random.NextFloat3(-3, 3), random.NextFloat3(-3, 3), random.NextFloat3(-3, 3));
                if (Area(triangle) < 1e-3)
                    continue;
                var pieces = Split(volume, triangle);
                AssertValidSplit(volume, triangle, pieces, out var inside);

                // What is inside matches clipping the triangle on its own
                var clipped = new ListSink { pieces = new List<(DecalPiece, bool)>() };
                var expected = 0.0;
                if (DecalClipping.AddInside(volume, triangle, 0, ref clipped))
                {
                    foreach (var (piece, _) in clipped.pieces)
                        expected += Area(piece);
                }
                Assert.That(inside, Is.EqualTo(expected).Within(1e-9 + 1e-9 * Area(triangle)), $"case {i}");
                if (inside > 0 && inside < Area(triangle) - 1e-6)
                    splits++;
            }
            Assert.That(splits, Is.GreaterThan(40), "too few triangles were actually split to mean anything");
        }

        // Two triangles sharing an edge, cut by the same decal, must put the same points on that edge: otherwise the
        // pieces on either side leave a crack (a T-junction) along it.
        [Test]
        public void NeighbouringTriangles_GetTheSamePointsOnTheirSharedEdge()
        {
            var random = new Random(99);
            var compared = 0;
            for (int i = 0; i < 300; i++)
            {
                var decalToTree = float4x4.TRS(random.NextFloat3(-0.5f, 0.5f), quaternion.EulerXYZ(random.NextFloat3(-3, 3)), new float3(1));
                var volume = Volume(Decal(random.NextFloat3(0.5f, 1.5f), decalToTree));
                var a = (double3)random.NextFloat3(-2, 2);
                var b = (double3)random.NextFloat3(-2, 2);
                // Two triangles on either side of a-b, in one plane, wound the same way (so they walk a-b oppositely)
                var side = math.normalize(math.cross(b - a, random.NextFloat3Direction()));
                var mid = (a + b) * 0.5;
                var left  = Triangle(a, b, mid + side);
                var right = Triangle(b, a, mid - side);
                if (Area(left) < 1e-3 || Area(right) < 1e-3)
                    continue;

                var leftPoints  = PointsOnSegment(Split(volume, left), a, b);
                var rightPoints = PointsOnSegment(Split(volume, right), a, b);
                Assert.That(rightPoints, Is.EquivalentTo(leftPoints), $"case {i}");
                if (leftPoints.Count > 2)
                    compared++;
            }
            Assert.That(compared, Is.GreaterThan(20), "too few shared edges crossed the decal to mean anything");
        }

        static List<double3> PointsOnSegment(List<(DecalPiece piece, bool inside)> pieces, double3 a, double3 b)
        {
            var result = new HashSet<double3>();
            var direction = b - a;
            var lengthSqr = math.lengthsq(direction);
            foreach (var (piece, _) in pieces)
            {
                foreach (var vertex in new[] { piece.v0, piece.v1, piece.v2 })
                {
                    var t = math.dot(vertex.position - a, direction) / lengthSqr;
                    if (t < -1e-12 || t > 1 + 1e-12)
                        continue;
                    if (math.distancesq(a + direction * t, vertex.position) > 1e-18)
                        continue;
                    result.Add(vertex.position);
                }
            }
            return new List<double3>(result);
        }
        #endregion
    }
}
