using System;
using System.Collections.Generic;
using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;

namespace Chisel.Core.Tests
{
    [TestFixture]
    [Category("Contents")]
    public class DecalPipelineTests
    {
        const ulong kDecalMaterial      = 0xDECA1;
        const ulong kOtherDecalMaterial = 0xDECA2;
        const double kAreaTolerance     = 1e-4;

        // Projects down (local +Z is world -Y), with the image's up along world +Z
        static readonly quaternion kDown = quaternion.LookRotation(new float3(0, -1, 0), new float3(0, 0, 1));

        static ContentsSceneNode Box(string name, float3 min, float3 max)
        {
            return ContentsSceneNode.Brush(ContentsScene.BoxPlanes(min, max), name: name);
        }

        static ChiselDecalInstance Decal(ulong id, float3 position, quaternion rotation, float3 size, bool transparent,
                                         int order = 0, ulong material = kDecalMaterial, float maxAngle = 180,
                                         ChiselDecalProjection projection = ChiselDecalProjection.Orthographic)
        {
            var settings = ChiselDecalSettings.Default;
            settings.size        = size;
            settings.transparent = transparent;
            settings.order       = order;
            settings.maxAngle    = maxAngle;
            settings.projection  = projection;
            return new ChiselDecalInstance
            {
                entityID         = id,
                decalToTree      = float4x4.TRS(position, rotation, new float3(1)),
                settings         = settings,
                renderMaterial   = material,
                destinationFlags = SurfaceDestinationFlags.Default
            };
        }

        static void SetDecals(ContentsTreeHarness harness, params ChiselDecalInstance[] decals)
        {
            using var array = new NativeArray<ChiselDecalInstance>(decals, Allocator.Temp);
            harness.Tree.SetDecals(array);
        }

        static ContentsTreeHarness BuildAndUpdate(ContentsScene scene, params ChiselDecalInstance[] decals)
        {
            var harness = ContentsTreeHarness.Build(scene);
            if (decals.Length > 0)
                SetDecals(harness, decals);
            Assert.That(harness.Update(), Is.True, "the CSG update did not run");
            Assert.That(harness.Delivered, Is.True, "no meshes were delivered: " + harness.LastUpdateReport);
            return harness;
        }

        static double Area(in ContentsTreeHarness.CapturedTriangle triangle)
        {
            return math.length(math.cross((double3)triangle.b - triangle.a, (double3)triangle.c - triangle.a)) * 0.5;
        }

        // The area of the triangles drawn with material that face along normal
        static double AreaOf(ContentsTreeHarness harness, ulong material, float3 normal)
        {
            var area = 0.0;
            foreach (var triangle in harness.Triangles)
            {
                if (triangle.material != material || math.dot(triangle.Normal, normal) < 0.99f)
                    continue;
                area += Area(triangle);
            }
            return area;
        }

        static double AreaOfMaterial(ContentsTreeHarness harness, ulong material)
        {
            var area = 0.0;
            foreach (var triangle in harness.Triangles)
            {
                if (triangle.material == material)
                    area += Area(triangle);
            }
            return area;
        }

        // Everything that isn't drawn with a decal material
        static double SurfaceArea(ContentsTreeHarness harness, float3 normal)
        {
            var area = 0.0;
            foreach (var triangle in harness.Triangles)
            {
                if (triangle.material == kDecalMaterial || triangle.material == kOtherDecalMaterial ||
                    math.dot(triangle.Normal, normal) < 0.99f)
                    continue;
                area += Area(triangle);
            }
            return area;
        }

        // How many triangles facing up cover the point (x, height, z), by material
        static Dictionary<ulong, int> CoverageAt(ContentsTreeHarness harness, double x, double z, double height)
        {
            var result = new Dictionary<ulong, int>();
            foreach (var triangle in harness.Triangles)
            {
                if (triangle.Normal.y < 0.99f)
                    continue;
                if (math.abs(triangle.a.y - height) > 0.01f)
                    continue;
                if (!Contains2D(triangle.a.xz, triangle.b.xz, triangle.c.xz, new double2(x, z)))
                    continue;
                result.TryGetValue(triangle.material, out var count);
                result[triangle.material] = count + 1;
            }
            return result;
        }

        static bool Contains2D(double2 a, double2 b, double2 c, double2 p)
        {
            double Cross(double2 u, double2 v, double2 w) => (v.x - u.x) * (w.y - u.y) - (v.y - u.y) * (w.x - u.x);
            var d0 = Cross(a, b, p);
            var d1 = Cross(b, c, p);
            var d2 = Cross(c, a, p);
            return (d0 >= 0 && d1 >= 0 && d2 >= 0) || (d0 <= 0 && d1 <= 0 && d2 <= 0);
        }

        static int CountOf(Dictionary<ulong, int> coverage)
        {
            var total = 0;
            foreach (var pair in coverage)
                total += pair.Value;
            return total;
        }

        // Samples a grid over the top of a floor at height 0. Offsets are chosen to stay off triangle edges.
        static IEnumerable<double2> FloorSamples(double min, double max)
        {
            const int kSteps = 23;
            for (int i = 0; i < kSteps; i++)
            {
                for (int j = 0; j < kSteps; j++)
                {
                    var x = min + (max - min) * (i + 0.37) / kSteps;
                    var z = min + (max - min) * (j + 0.61) / kSteps;
                    yield return new double2(x, z);
                }
            }
        }

        static bool InsideSquare(double2 point, double2 center, double halfSize)
        {
            return math.all(math.abs(point - center) < halfSize);
        }

        [Test]
        public void TransparentDecal_IsDrawnOverTheFloor()
        {
            var scene = new ContentsScene().Add(Box("floor", new float3(-2, -1, -2), new float3(2, 0, 2)));
            using var harness = BuildAndUpdate(scene, Decal(1, float3.zero, kDown, new float3(1, 1, 0.5f), transparent: true));

            Assert.That(AreaOf(harness, kDecalMaterial, new float3(0, 1, 0)), Is.EqualTo(1.0).Within(kAreaTolerance), "the decal covers its 1 by 1 footprint");
            Assert.That(AreaOfMaterial(harness, kDecalMaterial), Is.EqualTo(1.0).Within(kAreaTolerance), "and nothing else");
            Assert.That(SurfaceArea(harness, new float3(0, 1, 0)), Is.EqualTo(16.0).Within(kAreaTolerance), "the floor is all still there");

            foreach (var triangle in harness.Triangles)
            {
                if (triangle.material != kDecalMaterial)
                    continue;
                Assert.That(triangle.a.y, Is.EqualTo(ChiselDecalSettings.kDefaultSurfaceOffset).Within(1e-6), "lifted off the floor");
                foreach (var uv in new[] { triangle.uvA, triangle.uvB, triangle.uvC })
                    Assert.That(math.all(uv >= -1e-5f) && math.all(uv <= 1 + 1e-5f), Is.True, $"uv {uv} is outside the image");
            }

            foreach (var point in FloorSamples(-2, 2))
            {
                var coverage = CoverageAt(harness, point.x, point.y, 0);
                var inDecal  = InsideSquare(point, double2.zero, 0.5);
                coverage.TryGetValue(kDecalMaterial, out var decal);
                Assert.That(decal, Is.EqualTo(inDecal ? 1 : 0), $"decal at {point}");
                Assert.That(CountOf(coverage) - decal, Is.EqualTo(1), $"floor at {point}");
            }
        }

        [Test]
        public void OpaqueDecal_ReplacesTheFloorUnderIt()
        {
            var scene = new ContentsScene().Add(Box("floor", new float3(-2, -1, -2), new float3(2, 0, 2)));
            using var harness = BuildAndUpdate(scene);
            var colliderArea = harness.ColliderArea;

            SetDecals(harness, Decal(1, float3.zero, kDown, new float3(1, 1, 0.5f), transparent: false));
            Assert.That(harness.Update(), Is.True);

            Assert.That(AreaOf(harness, kDecalMaterial, new float3(0, 1, 0)), Is.EqualTo(1.0).Within(kAreaTolerance));
            Assert.That(SurfaceArea(harness, new float3(0, 1, 0)), Is.EqualTo(15.0).Within(kAreaTolerance), "the floor under the decal is gone");
            Assert.That(harness.ColliderArea, Is.EqualTo(colliderArea).Within(kAreaTolerance), "the collider keeps the whole floor");

            foreach (var triangle in harness.Triangles)
            {
                if (triangle.material == kDecalMaterial)
                    Assert.That(triangle.a.y, Is.EqualTo(0).Within(1e-6), "an opaque decal is not lifted");
            }

            foreach (var point in FloorSamples(-2, 2))
            {
                var coverage = CoverageAt(harness, point.x, point.y, 0);
                var inDecal  = InsideSquare(point, double2.zero, 0.5);
                coverage.TryGetValue(kDecalMaterial, out var decal);
                Assert.That(CountOf(coverage), Is.EqualTo(1), $"exactly one triangle at {point}");
                Assert.That(decal, Is.EqualTo(inDecal ? 1 : 0), $"decal at {point}");
            }
        }

        [Test]
        public void Surface_FacingAwayFromTheDecal_IsLeftAlone()
        {
            // A thin wall. The decal box reaches through it, but its center is in front of the wall.
            var scene = new ContentsScene().Add(Box("wall", new float3(-2, -2, -0.1f), new float3(2, 2, 0.1f)));
            var towardsWall = quaternion.LookRotation(new float3(0, 0, -1), new float3(0, 1, 0));
            using var harness = BuildAndUpdate(scene, Decal(1, new float3(0, 0, 0.3f), towardsWall, new float3(1, 1, 1), transparent: false));

            Assert.That(AreaOf(harness, kDecalMaterial, new float3(0, 0, 1)), Is.EqualTo(1.0).Within(kAreaTolerance), "the front takes the decal");
            Assert.That(AreaOf(harness, kDecalMaterial, new float3(0, 0, -1)), Is.EqualTo(0.0).Within(kAreaTolerance), "the back faces away from it");
            Assert.That(SurfaceArea(harness, new float3(0, 0, -1)), Is.EqualTo(16.0).Within(kAreaTolerance), "the back is whole");
        }

        [Test]
        public void AngleLimit_KeepsTheDecalOffTheWall()
        {
            // A floor with a wall standing on it; the decal box covers the corner between them
            var scene = new ContentsScene()
                .Add(Box("floor", new float3(-2, -1, -2), new float3(2, 0, 2)))
                .Add(Box("wall",  new float3(0.25f, 0, -2), new float3(1, 2, 2)));
            using (var unlimited = BuildAndUpdate(scene, Decal(1, new float3(0, 0.25f, 0), kDown, new float3(1, 1, 1), transparent: true)))
            {
                Assert.That(AreaOf(unlimited, kDecalMaterial, new float3(-1, 0, 0)), Is.GreaterThan(0.1),
                            "without a limit the wall facing the center takes the decal");
            }
            using (var limited = BuildAndUpdate(scene, Decal(1, new float3(0, 0.25f, 0), kDown, new float3(1, 1, 1), transparent: true, maxAngle: 60)))
            {
                Assert.That(AreaOf(limited, kDecalMaterial, new float3(-1, 0, 0)), Is.EqualTo(0.0).Within(kAreaTolerance));
                Assert.That(AreaOf(limited, kDecalMaterial, new float3(0, 1, 0)), Is.EqualTo(0.75).Within(kAreaTolerance),
                            "the floor from the decal's edge to the wall");
            }
        }

        [Test]
        public void OpaqueDecal_OverTwoBrushes_LeavesNoCrackBetweenThem()
        {
            var a = Box("a", new float3(-2, -1, -2), new float3(0, 0, 2));
            var b = Box("b", new float3(0, -1, -2), new float3(2, 0, 2));
            var scene = new ContentsScene().Add(a).Add(b);
            var rotated = math.mul(kDown, quaternion.RotateZ(0.4f));
            using var harness = BuildAndUpdate(scene, Decal(1, new float3(0.1f, 0, 0.2f), rotated, new float3(1.3f, 0.9f, 0.5f), transparent: false));

            Assert.That(AreaOf(harness, kDecalMaterial, new float3(0, 1, 0)), Is.EqualTo(1.3 * 0.9).Within(kAreaTolerance));
            Assert.That(AreaOf(harness, kDecalMaterial, new float3(0, 1, 0)) + SurfaceArea(harness, new float3(0, 1, 0)),
                        Is.EqualTo(16.0).Within(kAreaTolerance));

            // Both brushes put the same points on the line they share
            var onSeamA = new HashSet<float3>();
            var onSeamB = new HashSet<float3>();
            foreach (var triangle in harness.Triangles)
            {
                if (triangle.Normal.y < 0.99f)
                    continue;
                var node = harness.NodeOf(triangle.brushID);
                foreach (var point in new[] { triangle.a, triangle.b, triangle.c })
                {
                    if (point.x != 0)
                        continue;
                    (node == a ? onSeamA : onSeamB).Add(point);
                }
            }
            Assert.That(onSeamA.Count, Is.GreaterThan(2), "the decal should put points on the seam");
            Assert.That(onSeamB, Is.EquivalentTo(onSeamA));
        }

        [Test]
        public void HigherOpaqueDecal_HidesTheLowerOne()
        {
            var scene = new ContentsScene().Add(Box("floor", new float3(-2, -1, -2), new float3(2, 0, 2)));
            using var harness = BuildAndUpdate(scene,
                Decal(1, new float3(0, 0, 0), kDown, new float3(1, 1, 0.5f), transparent: false, order: 0, material: kDecalMaterial),
                Decal(2, new float3(0.5f, 0, 0), kDown, new float3(1, 1, 0.5f), transparent: false, order: 1, material: kOtherDecalMaterial));

            Assert.That(AreaOf(harness, kOtherDecalMaterial, new float3(0, 1, 0)), Is.EqualTo(1.0).Within(kAreaTolerance), "the higher decal is whole");
            Assert.That(AreaOf(harness, kDecalMaterial, new float3(0, 1, 0)), Is.EqualTo(0.5).Within(kAreaTolerance), "the lower one is half hidden");
            Assert.That(SurfaceArea(harness, new float3(0, 1, 0)), Is.EqualTo(14.5).Within(kAreaTolerance));
            foreach (var point in FloorSamples(-2, 2))
                Assert.That(CountOf(CoverageAt(harness, point.x, point.y, 0)), Is.EqualTo(1), $"exactly one triangle at {point}");
        }

        [Test]
        public void TransparentDecal_UnderAnOpaqueOne_IsHiddenByIt()
        {
            var scene = new ContentsScene().Add(Box("floor", new float3(-2, -1, -2), new float3(2, 0, 2)));
            using var harness = BuildAndUpdate(scene,
                Decal(1, new float3(0, 0, 0), kDown, new float3(1, 1, 0.5f), transparent: true, order: 0, material: kDecalMaterial),
                Decal(2, new float3(0.5f, 0, 0), kDown, new float3(1, 1, 0.5f), transparent: false, order: 1, material: kOtherDecalMaterial));
            Assert.That(AreaOf(harness, kDecalMaterial, new float3(0, 1, 0)), Is.EqualTo(0.5).Within(kAreaTolerance));

            using var above = BuildAndUpdate(scene,
                Decal(1, new float3(0, 0, 0), kDown, new float3(1, 1, 0.5f), transparent: true, order: 2, material: kDecalMaterial),
                Decal(2, new float3(0.5f, 0, 0), kDown, new float3(1, 1, 0.5f), transparent: false, order: 1, material: kOtherDecalMaterial));
            Assert.That(AreaOf(above, kDecalMaterial, new float3(0, 1, 0)), Is.EqualTo(1.0).Within(kAreaTolerance), "on top, it is drawn whole");
        }

        [Test]
        public void PerspectiveDecal_IsCenteredOnItsImage()
        {
            var scene = new ContentsScene().Add(Box("floor", new float3(-2, -1, -2), new float3(2, 0, 2)));
            using var harness = BuildAndUpdate(scene,
                Decal(1, new float3(0, 0, 0), kDown, new float3(1, 1, 1), transparent: true, projection: ChiselDecalProjection.Perspective));
            // The floor is at the box's center plane, where the image has the box's size
            Assert.That(AreaOf(harness, kDecalMaterial, new float3(0, 1, 0)), Is.EqualTo(1.0).Within(kAreaTolerance));
        }

        [Test]
        public void MovingADecal_RebuildsOnlyTheBrushesItReaches()
        {
            var left   = Box("left",   new float3(-10, -1, -2), new float3(-6, 0, 2));
            var right  = Box("right",  new float3(6, -1, -2),   new float3(10, 0, 2));
            var middle = Box("middle", new float3(-1, -1, 20),  new float3(1, 0, 24));
            var scene  = new ContentsScene().Add(left).Add(right).Add(middle);
            using var harness = BuildAndUpdate(scene, Decal(1, new float3(-8, 0, 0), kDown, new float3(1, 1, 0.5f), transparent: false));
            Assert.That(AreaOf(harness, kDecalMaterial, new float3(0, 1, 0)), Is.EqualTo(1.0).Within(kAreaTolerance));

            SetDecals(harness, Decal(1, new float3(8, 0, 0), kDown, new float3(1, 1, 0.5f), transparent: false));
            Assert.That(harness.Update(), Is.True);
            Assert.That(CompactHierarchyManager.LastUpdateModifiedBrushCount, Is.EqualTo(2), "only where the decal was and is: " + harness.LastUpdateReport);
            Assert.That(AreaOf(harness, kDecalMaterial, new float3(0, 1, 0)), Is.EqualTo(1.0).Within(kAreaTolerance));
            foreach (var triangle in harness.Triangles)
            {
                if (triangle.material == kDecalMaterial)
                    Assert.That(triangle.a.x, Is.GreaterThan(0), "the decal left the left brush");
            }
            Assert.That(SurfaceArea(harness, new float3(0, 1, 0)), Is.EqualTo(16 + 15 + 8).Within(kAreaTolerance));
        }

        [Test]
        public void RemovingDecals_RestoresTheSurface()
        {
            var scene = new ContentsScene().Add(Box("floor", new float3(-2, -1, -2), new float3(2, 0, 2)));
            using var harness = BuildAndUpdate(scene, Decal(1, float3.zero, kDown, new float3(1, 1, 0.5f), transparent: false));
            Assert.That(harness.Tree.GetDecalCount(), Is.EqualTo(1));

            harness.Tree.ClearDecals();
            Assert.That(harness.Update(), Is.True);
            Assert.That(harness.Tree.GetDecalCount(), Is.EqualTo(0));
            Assert.That(AreaOfMaterial(harness, kDecalMaterial), Is.EqualTo(0.0));
            Assert.That(SurfaceArea(harness, new float3(0, 1, 0)), Is.EqualTo(16.0).Within(kAreaTolerance));
        }

        [Test]
        public void SettingTheSameDecals_RebuildsNothing()
        {
            var scene = new ContentsScene().Add(Box("floor", new float3(-2, -1, -2), new float3(2, 0, 2)));
            var decal = Decal(1, float3.zero, kDown, new float3(1, 1, 0.5f), transparent: false);
            using var harness = BuildAndUpdate(scene, decal);
            SetDecals(harness, decal);
            Assert.That(harness.Tree.IsStatusFlagSet(NodeStatusFlags.TreeNeedsUpdate), Is.False, "nothing changed");
        }

        [Test]
        public void DecalInEmptySpace_DrawsNothing()
        {
            var scene = new ContentsScene().Add(Box("floor", new float3(-2, -1, -2), new float3(2, 0, 2)));
            using var harness = BuildAndUpdate(scene, Decal(1, new float3(0, 5, 0), kDown, new float3(1, 1, 0.5f), transparent: false));
            Assert.That(AreaOfMaterial(harness, kDecalMaterial), Is.EqualTo(0.0));
            Assert.That(SurfaceArea(harness, new float3(0, 1, 0)), Is.EqualTo(16.0).Within(kAreaTolerance));
        }

        #region Targets
        static void SetDecals(ContentsTreeHarness harness, ChiselDecalInstance[] decals, ChiselDecalTarget[] targets)
        {
            using var decalArray  = new NativeArray<ChiselDecalInstance>(decals, Allocator.Temp);
            using var targetArray = new NativeArray<ChiselDecalTarget>(targets, Allocator.Temp);
            harness.Tree.SetDecals(decalArray, targetArray);
        }

        static ChiselDecalInstance Targeting(ChiselDecalInstance decal, int start, int count)
        {
            decal.targetStart = start;
            decal.targetCount = count;
            return decal;
        }

        static ContentsSceneNode Box(string name, float3 min, float3 max, ulong entityID)
        {
            var box = Box(name, min, max);
            box.entityID = entityID;
            return box;
        }

        static ContentsTreeHarness BuildAndUpdate(ContentsScene scene, ChiselDecalInstance[] decals, ChiselDecalTarget[] targets)
        {
            var harness = ContentsTreeHarness.Build(scene);
            SetDecals(harness, decals, targets);
            Assert.That(harness.Update(), Is.True, "the CSG update did not run");
            Assert.That(harness.Delivered, Is.True, "no meshes were delivered: " + harness.LastUpdateReport);
            return harness;
        }

        // Plane 3 of ContentsScene.BoxPlanes is its top (+Y), plane 5 its front (+Z)
        const int kTopSurface   = 3;
        const int kFrontSurface = 5;

        [Test]
        public void TargetedTransparentDecal_DrawsOnlyOnItsTarget()
        {
            var scene = new ContentsScene()
                .Add(Box("a", new float3(-2, -1, -2), new float3(0, 0, 2), 101))
                .Add(Box("b", new float3(0, -1, -2),  new float3(2, 0, 2), 102));
            using var harness = BuildAndUpdate(scene,
                new[] { Targeting(Decal(1, float3.zero, kDown, new float3(1, 1, 0.5f), transparent: true), 0, 1) },
                new[] { new ChiselDecalTarget { brushEntityID = 101, surfaceIndex = ChiselDecalTarget.kAllSurfaces } });

            Assert.That(AreaOf(harness, kDecalMaterial, new float3(0, 1, 0)), Is.EqualTo(0.5).Within(kAreaTolerance), "only the half on a");
            foreach (var triangle in harness.Triangles)
            {
                if (triangle.material == kDecalMaterial)
                    Assert.That(triangle.Center.x, Is.LessThan(0), "the decal is on b");
            }
        }

        [Test]
        public void TargetedOpaqueDecal_CutsTheOtherBrushWithoutCoveringIt()
        {
            var a = Box("a", new float3(-2, -1, -2), new float3(0, 0, 2), 101);
            var b = Box("b", new float3(0, -1, -2),  new float3(2, 0, 2), 102);
            var scene = new ContentsScene().Add(a).Add(b);
            var rotated = math.mul(kDown, quaternion.RotateZ(0.4f));
            using var harness = BuildAndUpdate(scene,
                new[] { Targeting(Decal(1, new float3(0.1f, 0, 0.2f), rotated, new float3(1.3f, 0.9f, 0.5f), transparent: false), 0, 1) },
                new[] { new ChiselDecalTarget { brushEntityID = 101, surfaceIndex = kTopSurface } });

            var decalArea = AreaOf(harness, kDecalMaterial, new float3(0, 1, 0));
            Assert.That(decalArea, Is.GreaterThan(0.1).And.LessThan(1.3 * 0.9 - 0.1), "only a's part");
            Assert.That(decalArea + SurfaceArea(harness, new float3(0, 1, 0)), Is.EqualTo(16.0).Within(kAreaTolerance),
                        "b keeps its whole top");

            // b was still cut, so both brushes put the same points on the line they share
            var onSeamA = new HashSet<float3>();
            var onSeamB = new HashSet<float3>();
            foreach (var triangle in harness.Triangles)
            {
                if (triangle.Normal.y < 0.99f)
                    continue;
                var node = harness.NodeOf(triangle.brushID);
                foreach (var point in new[] { triangle.a, triangle.b, triangle.c })
                {
                    if (point.x != 0)
                        continue;
                    (node == a ? onSeamA : onSeamB).Add(point);
                }
            }
            Assert.That(onSeamA.Count, Is.GreaterThan(2), "the decal should put points on the seam");
            Assert.That(onSeamB, Is.EquivalentTo(onSeamA));
        }

        [Test]
        public void TargetingOneSurface_LeavesTheBrushsOtherSurfacesAlone()
        {
            // The decal box hangs over the block's top front edge, so it reaches a quarter of a square metre of both
            var scene = new ContentsScene().Add(Box("block", new float3(-1, -1, -1), new float3(1, 1, 1), 201));
            var decal = Decal(1, new float3(0, 1.25f, 1.25f), kDown, new float3(1, 1, 1), transparent: true);

            using (var everything = BuildAndUpdate(scene, decal))
            {
                Assert.That(AreaOf(everything, kDecalMaterial, new float3(0, 1, 0)), Is.EqualTo(0.25).Within(kAreaTolerance));
                Assert.That(AreaOf(everything, kDecalMaterial, new float3(0, 0, 1)), Is.EqualTo(0.25).Within(kAreaTolerance),
                            "without targets the front takes it too");
                Assert.That(AreaOfMaterial(everything, kDecalMaterial), Is.EqualTo(0.5).Within(kAreaTolerance));
            }
            using (var top = BuildAndUpdate(scene, new[] { Targeting(decal, 0, 1) },
                                            new[] { new ChiselDecalTarget { brushEntityID = 201, surfaceIndex = kTopSurface } }))
            {
                Assert.That(AreaOf(top, kDecalMaterial, new float3(0, 1, 0)), Is.EqualTo(0.25).Within(kAreaTolerance));
                Assert.That(AreaOfMaterial(top, kDecalMaterial), Is.EqualTo(0.25).Within(kAreaTolerance), "only the top");
            }
            using (var front = BuildAndUpdate(scene, new[] { Targeting(decal, 0, 1) },
                                              new[] { new ChiselDecalTarget { brushEntityID = 201, surfaceIndex = kFrontSurface } }))
            {
                Assert.That(AreaOf(front, kDecalMaterial, new float3(0, 0, 1)), Is.EqualTo(0.25).Within(kAreaTolerance));
                Assert.That(AreaOfMaterial(front, kDecalMaterial), Is.EqualTo(0.25).Within(kAreaTolerance), "only the front");
            }
        }

        [Test]
        public void DecalLimitedToSurfacesThatArentThere_DrawsNothing()
        {
            var scene = new ContentsScene().Add(Box("floor", new float3(-2, -1, -2), new float3(2, 0, 2), 301));
            using var harness = BuildAndUpdate(scene,
                new[]
                {
                    Targeting(Decal(1, float3.zero, kDown, new float3(1, 1, 0.5f), transparent: false), 0, 1),
                    // Its targets are past the end of the array
                    Targeting(Decal(2, float3.zero, kDown, new float3(1, 1, 0.5f), transparent: false, material: kOtherDecalMaterial), 5, 2)
                },
                new[] { new ChiselDecalTarget { brushEntityID = 999, surfaceIndex = ChiselDecalTarget.kAllSurfaces } });
            Assert.That(AreaOfMaterial(harness, kDecalMaterial), Is.EqualTo(0.0));
            Assert.That(AreaOfMaterial(harness, kOtherDecalMaterial), Is.EqualTo(0.0));
            Assert.That(SurfaceArea(harness, new float3(0, 1, 0)), Is.EqualTo(16.0).Within(kAreaTolerance));
        }

        [Test]
        public void ChangingTheTargets_MovesTheDecal()
        {
            var scene = new ContentsScene()
                .Add(Box("a", new float3(-2, -1, -2), new float3(0, 0, 2), 101))
                .Add(Box("b", new float3(0, -1, -2),  new float3(2, 0, 2), 102));
            var decal = Targeting(Decal(1, float3.zero, kDown, new float3(1, 1, 0.5f), transparent: true), 0, 1);
            using var harness = BuildAndUpdate(scene, new[] { decal },
                new[] { new ChiselDecalTarget { brushEntityID = 101, surfaceIndex = ChiselDecalTarget.kAllSurfaces } });

            // The same decal and targets at another place in the array are no change
            SetDecals(harness, new[] { Targeting(decal, 1, 1) },
                      new[] { new ChiselDecalTarget { brushEntityID = 999 }, new ChiselDecalTarget { brushEntityID = 101, surfaceIndex = ChiselDecalTarget.kAllSurfaces } });
            Assert.That(harness.Tree.IsStatusFlagSet(NodeStatusFlags.TreeNeedsUpdate), Is.False, "nothing changed");

            SetDecals(harness, new[] { decal },
                      new[] { new ChiselDecalTarget { brushEntityID = 102, surfaceIndex = ChiselDecalTarget.kAllSurfaces } });
            Assert.That(harness.Update(), Is.True);
            foreach (var triangle in harness.Triangles)
            {
                if (triangle.material == kDecalMaterial)
                    Assert.That(triangle.Center.x, Is.GreaterThan(0), "the decal stayed on a");
            }
            Assert.That(AreaOf(harness, kDecalMaterial, new float3(0, 1, 0)), Is.EqualTo(0.5).Within(kAreaTolerance));
        }
        #endregion

        [Test]
        public void ClickingADecal_SelectsTheDecal()
        {
            const ulong kFloor = 401, kTransparent = 7001, kOpaque = 7002;
            var scene = new ContentsScene().Add(Box("floor", new float3(-2, -1, -2), new float3(2, 0, 2), kFloor));
            using var harness = BuildAndUpdate(scene,
                Decal(kTransparent, float3.zero, kDown, new float3(1, 1, 0.5f), transparent: true),
                Decal(kOpaque, new float3(1.25f, 0, 1.25f), kDown, new float3(1, 1, 0.5f), transparent: false, material: kOtherDecalMaterial));

            int transparentTriangles = 0, opaqueTriangles = 0, floorTriangles = 0;
            foreach (var triangle in harness.Triangles)
            {
                if (triangle.material == kDecalMaterial)
                {
                    transparentTriangles++;
                    Assert.That(triangle.selectedEntityID, Is.EqualTo(kTransparent), "a transparent decal's triangle");
                } else
                if (triangle.material == kOtherDecalMaterial)
                {
                    opaqueTriangles++;
                    Assert.That(triangle.selectedEntityID, Is.EqualTo(kOpaque), "an opaque decal's triangle");
                } else
                {
                    floorTriangles++;
                    Assert.That(triangle.selectedEntityID, Is.EqualTo(kFloor), "the floor's triangle");
                }
            }
            Assert.That(transparentTriangles, Is.GreaterThan(0));
            Assert.That(opaqueTriangles, Is.GreaterThan(0));
            Assert.That(floorTriangles, Is.GreaterThan(0));
        }

        [Test]
        public void DecalOnAFaceTheCSGRemoved_DrawsNothingThere()
        {
            // Two stacked boxes: the lower one's top is inside the upper one and isn't drawn
            var scene = new ContentsScene()
                .Add(Box("lower", new float3(-2, -1, -2), new float3(2, 0, 2)))
                .Add(Box("upper", new float3(-2, 0, -2),  new float3(2, 1, 2)));
            using var harness = BuildAndUpdate(scene, Decal(1, new float3(0, 0, 0), kDown, new float3(1, 1, 0.5f), transparent: true));
            Assert.That(AreaOfMaterial(harness, kDecalMaterial), Is.EqualTo(0.0).Within(kAreaTolerance));
        }
    }
}
