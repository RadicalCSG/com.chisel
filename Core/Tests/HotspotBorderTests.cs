using System.Collections.Generic;
using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace Chisel.Core.Tests
{
    [TestFixture]
    public class HotspotBorderTests
    {
        // A 2048 sheet at 256 pixels a unit with one tile
        sealed class OneTile : System.IDisposable
        {
            public ChiselHotspotLayout layout;
            public NativeList<HotspotTileData> tiles = new(Allocator.Temp);
            public HotspotLayoutData data;
            public HotspotSections border = HotspotSections.Create(Allocator.Temp);

            public OneTile(int borderPixels, params Vector2Int[] points)
            {
                layout = ScriptableObject.CreateInstance<ChiselHotspotLayout>();
                layout.size = new Vector2Int(2048, 2048);
                layout.pixelsPerUnit = 256;
                layout.tiles = new List<HotspotTile> { new() { id = 1, name = "tile", points = points, border = borderPixels } };
                HotspotMatcher.AddTiles(layout, tiles, out data);
            }

            public HotspotChoice Build(in HotspotShape shape)
            {
                var choice = HotspotMatcher.Choose(tiles.AsArray(), data, shape, 0, 0);
                Assert.That(choice.tile, Is.EqualTo(0));
                Assert.That(border.Build(tiles[0], data, shape, choice), Is.True, "no border was built");
                return choice;
            }

            public float2 UV(double x, double y) => border.UV(new double2(x, y));

            public void Dispose()
            {
                border.Dispose();
                tiles.Dispose();
                Object.DestroyImmediate(layout);
            }
        }

        // 1024 x 512 pixels: 4 x 2 units
        static Vector2Int[] B1 => new[] { new Vector2Int(1024, 0), new Vector2Int(2048, 0), new Vector2Int(2048, 512), new Vector2Int(1024, 512) };

        static HotspotShape Shape(params float3[] points)
        {
            using var positions = new NativeArray<float3>(points, Allocator.Temp);
            return HotspotMatcher.ShapeOf(positions, new float3(0, 0, -1));
        }

        static HotspotShape Wall(float width, float height) => Shape(new float3(0, 0, 0), new float3(width, 0, 0), new float3(width, height, 0), new float3(0, height, 0));

        // Across every line between sections, at points along it inside the surface, the uvs do not jump
        static void AssertContinuous(in HotspotSections border, System.Func<double2, bool> inside)
        {
            for (int l = 0; l < border.lines.Length; l++)
            {
                var line = border.lines[l];
                var along = new double2(-line.normal.y, line.normal.x);
                var through = -line.normal * line.offset;
                for (double s = -20; s <= 20; s += 0.0137)
                {
                    var p = through + along * s;
                    if (!inside(p))
                        continue;
                    var step = line.normal * 1e-7;
                    Assert.That(math.distance(border.UV(p - step), border.UV(p + step)), Is.LessThan(1e-5f), $"the uvs jump across line {l} at {p}");
                }
            }
        }

        static void AreEqual(float2 expectedPixels, float2 actual, string what)
        {
            var expected = expectedPixels / 2048;
            Assert.That(math.distance(expected, actual), Is.LessThan(1e-6f), $"{what}: expected {expectedPixels} px, got {actual * 2048} px");
        }

        [Test]
        public void AWallTheSizeOfItsTile_IsMappedAsWithoutABorder()
        {
            using var sheet = new OneTile(32, B1);
            var shape  = Wall(4, 2);
            var choice = sheet.Build(shape);
            var plain  = HotspotMatcher.Mapping(sheet.tiles[0], sheet.data, shape, choice, new float3(0, 0, -1));
            for (float x = 0; x <= 4; x += 0.0625f)
            {
                for (float y = 0; y <= 2; y += 0.0625f)
                {
                    var expected = math.mul(plain, new float4(x, y, 0, 1)).xy;
                    Assert.That(math.distance(expected, sheet.UV(x, y)), Is.LessThan(1e-6f), $"at ({x}, {y})");
                }
            }
        }

        [Test]
        public void AStretchedWall_KeepsItsBorderAtItsPixelWidth_AndStretchesTheRest()
        {
            using var sheet = new OneTile(32, B1);
            sheet.Build(Wall(8, 2));
            // 32 pixels at 256 a unit: an eighth of a unit; B1 is 1024..2048 x 1536..2048 with y up
            AreEqual(new float2(1024, 1536),           sheet.UV(0, 0),          "the bottom left corner");
            AreEqual(new float2(1024 + 32, 1536 + 32), sheet.UV(0.125, 0.125),  "the inner corner at the bottom left");
            AreEqual(new float2(2048 - 32, 2048 - 32), sheet.UV(7.875, 1.875),  "the inner corner at the top right");
            AreEqual(new float2(1024 + 16, 1536 + 16), sheet.UV(0.0625, 0.0625), "inside the corner, unstretched");
            AreEqual(new float2(1536, 1536 + 16),      sheet.UV(4, 0.0625),     "the bottom strip, stretched along only");
            AreEqual(new float2(1024 + 16, 1792),      sheet.UV(0.0625, 1),     "the left strip");
            AreEqual(new float2(1536, 1792),           sheet.UV(4, 1),          "the middle");
            AssertContinuous(sheet.border, p => p.x > 1e-3 && p.x < 8 - 1e-3 && p.y > 1e-3 && p.y < 2 - 1e-3);
        }

        [Test]
        public void ATriangle_KeepsItsBorderAtItsPixelWidth()
        {
            // 256 x 128 pixels, its right angle at the bottom right as the texture shows it: (1792, 896) with y up
            using var sheet = new OneTile(16, new Vector2Int(1792, 1024), new Vector2Int(1792, 1152), new Vector2Int(1536, 1152));
            // larger than the tile and not the same shape, its right angle at the bottom right
            sheet.Build(Shape(new float3(0, 0, 0), new float3(2, 0, 0), new float3(2, 1.5f, 0)));
            AreEqual(new float2(1792, 896),           sheet.UV(2, 0),            "the right angle");
            AreEqual(new float2(1792 - 16, 896 + 16), sheet.UV(2 - 0.0625, 0.0625), "its inner corner, 16 pixels in");
            AreEqual(new float2(1792 - 8, 896 + 8),   sheet.UV(2 - 0.03125, 0.03125), "inside its corner, unstretched");
            AssertContinuous(sheet.border, p => p.y > 1e-3 && p.x < 2 - 1e-3 && p.y < p.x * 0.75 - 1e-3);
        }

        // Every uv over the surface's bounds is a number and lies within the border's depth of the tile's edges
        static void AssertAllBorder(OneTile sheet, double width, double height, float2 tileMin, float2 tileMax, float borderPixels)
        {
            for (double x = 0.0031; x < width; x += width / 97)
            {
                for (double y = 0.0017; y < height; y += height / 31)
                {
                    var pixel = sheet.UV(x, y) * 2048;
                    Assert.That(math.all(math.isfinite(pixel)), Is.True, $"at ({x}, {y}): {pixel}");
                    var depth = math.cmin(math.min(pixel - tileMin, tileMax - pixel));
                    Assert.That(depth, Is.LessThanOrEqualTo(borderPixels + 1e-3f), $"({x}, {y}) is in the tile's interior: {pixel} px");
                }
            }
        }

        [Test]
        public void AWallThinnerThanTwoBorders_IsAllBorder_ItsBorderKeepingItsPixelWidth()
        {
            using var sheet = new OneTile(32, B1);
            var shape  = Wall(4, 0.2f);
            var choice = sheet.Build(shape);
            Assert.That(HotspotSections.IsAllBorder(HotspotSections.Input(sheet.tiles[0], sheet.data, shape, choice, new float3(0, 0, -1))), Is.True);
            // 32 pixels is an eighth of a unit, so the strips along the top and bottom meet in the middle, 0.1 in
            // (25.6 pixels), and keep 256 pixels a unit across
            AreEqual(new float2(1024, 1536),               sheet.UV(0, 0),        "the bottom left corner");
            AreEqual(new float2(1024 + 12.8f, 1536 + 12.8f), sheet.UV(0.05, 0.05), "inside the corner, unstretched");
            AreEqual(new float2(1536, 1536 + 12.8f),        sheet.UV(2, 0.05),    "the bottom strip");
            AreEqual(new float2(1536, 2048 - 12.8f),        sheet.UV(2, 0.15),    "the top strip");
            AssertAllBorder(sheet, 4, 0.2f, new float2(1024, 1536), new float2(2048, 2048), 32);

            var tall = Wall(4, 2);
            var fits = sheet.Build(tall);
            Assert.That(HotspotSections.IsAllBorder(HotspotSections.Input(sheet.tiles[0], sheet.data, tall, fits, new float3(0, 0, -1))), Is.False);
        }

        [Test]
        public void AWallThatIsAllBorder_IsCutWithoutNaN_ItsStripsMeetingOnOneLine()
        {
            // the insets of the top and the bottom are the same line: no point may come from crossing them
            using var sheet = new OneTile(32, B1);
            sheet.Build(Wall(4, 0.2f));
            using var indices  = new NativeList<int>(Allocator.Temp) { 0, 1, 2, 0, 2, 3 };
            using var collider = new NativeList<float3>(Allocator.Temp) { new float3(0, 0, 0), new float3(4, 0, 0), new float3(4, 0.2f, 0), new float3(0, 0.2f, 0) };
            using var select   = new NativeList<SelectVertex>(Allocator.Temp);
            using var render   = new NativeList<RenderVertex>(Allocator.Temp);
            foreach (var p in collider)
            {
                select.Add(new SelectVertex { position = p });
                render.Add(new RenderVertex { position = p, normal = new float3(0, 0, -1) });
            }
            var right = new float3(1, 0, 0); var up = new float3(0, 1, 0);
            sheet.border.Split(right, up, new float3(0, 0, -1), indices, collider, select, render);
            sheet.border.AssignUVs(indices, collider, select, render, right, up);
            var area = 0.0;
            for (int t = 0; t < indices.Length; t += 3)
            {
                var a = (double3)collider[indices[t]]; var b = (double3)collider[indices[t + 1]]; var c = (double3)collider[indices[t + 2]];
                area += ((b.x - a.x) * (c.y - a.y) - (b.y - a.y) * (c.x - a.x)) * 0.5;
            }
            for (int i = 0; i < render.Length; i++)
            {
                Assert.That(math.all(math.isfinite(collider[i])), Is.True, $"vertex {i}: {collider[i]}");
                Assert.That(math.all(math.isfinite(render[i].uv0)), Is.True, $"vertex {i}'s uv: {render[i].uv0}");
            }
            Assert.That(area, Is.EqualTo((double)4 * 0.2f).Within(1e-6), "the pieces cover the wall once");
        }

        // A rectangle's two triangles cut once along its sections (CutOnce)
        static (NativeList<int> indices, NativeList<float3> positions, NativeList<RenderVertex> render) CutOnce(OneTile sheet, float width, float height)
        {
            var indices  = new NativeList<int>(Allocator.Temp) { 0, 1, 2, 0, 2, 3 };
            var collider = new NativeList<float3>(Allocator.Temp) { new float3(0, 0, 0), new float3(width, 0, 0), new float3(width, height, 0), new float3(0, height, 0) };
            var select   = new NativeList<SelectVertex>(Allocator.Temp);
            var render   = new NativeList<RenderVertex>(Allocator.Temp);
            foreach (var p in collider)
            {
                select.Add(new SelectVertex { position = p });
                render.Add(new RenderVertex { position = p, normal = new float3(0, 0, -1) });
            }
            var right = new float3(1, 0, 0); var up = new float3(0, 1, 0);
            Assert.That(sheet.border.CutOnce(right, up, new float3(0, 0, -1), indices, collider, select, render), Is.True, "not cut once");
            sheet.border.AssignUVs(indices, collider, select, render, right, up);
            select.Dispose();
            return (indices, collider, render);
        }

        // Every triangle wound as before, nothing flat, the pieces cover the surface once, and an edge only one triangle
        // has is on the outline: no T-junctions
        public static void AssertWatertight(NativeList<int> indices, NativeList<float3> positions, double expectedArea, System.Func<float3, bool> onOutline)
        {
            var area = 0.0;
            var uses = new Dictionary<(float3, float3), int>();
            for (int t = 0; t < indices.Length; t += 3)
            {
                var a = (double3)positions[indices[t]]; var b = (double3)positions[indices[t + 1]]; var c = (double3)positions[indices[t + 2]];
                var doubled = (b.x - a.x) * (c.y - a.y) - (b.y - a.y) * (c.x - a.x);
                Assert.That(doubled, Is.GreaterThan(0), $"triangle {t / 3} is flat or turned over");
                area += doubled * 0.5;
                for (int k = 0; k < 3; k++)
                {
                    var p = positions[indices[t + k]]; var q = positions[indices[t + (k + 1) % 3]];
                    var key = Less(p, q) ? (p, q) : (q, p);
                    uses.TryGetValue(key, out var n);
                    uses[key] = n + 1;
                }
            }
            Assert.That(area, Is.EqualTo(expectedArea).Within(1e-6 * expectedArea), "the pieces do not cover the surface once");
            foreach (var (edge, n) in uses)
            {
                Assert.That(n, Is.LessThanOrEqualTo(2), $"edge {edge} is used {n} times");
                if (n == 1)
                    Assert.That(onOutline(edge.Item1) && onOutline(edge.Item2), Is.True, $"edge {edge} has one triangle but is not on the outline: a T-junction");
            }
        }

        [Test]
        public void ABorderedWall_IsCutOnce_EachSectionTwoTriangles()
        {
            using var sheet = new OneTile(32, B1);
            sheet.Build(Wall(8, 2));
            var (indices, positions, render) = CutOnce(sheet, 8, 2);
            // four corners, four strips and the interior: nine quads on a grid of 4 x 4 points
            Assert.That(indices.Length / 3, Is.EqualTo(18));
            Assert.That(positions.Length, Is.EqualTo(16));
            AssertWatertight(indices, positions, 16, p => p.x == 0 || p.x == 8 || p.y == 0 || p.y == 2);
            AreEqual(new float2(1024 + 32, 1536 + 32), UVOf(render, new float3(0.125f, 0.125f, 0)), "the inner corner");
            indices.Dispose(); positions.Dispose(); render.Dispose();
        }

        [Test]
        public void AWallThatIsAllBorder_IsCutOnce()
        {
            using var sheet = new OneTile(32, B1);
            sheet.Build(Wall(4, 0.2f));
            var (indices, positions, render) = CutOnce(sheet, 4, 0.2f);
            // four corners and the strips along the top and the bottom, meeting in the middle
            Assert.That(indices.Length / 3, Is.EqualTo(12));
            AssertWatertight(indices, positions, 4 * 0.2f, p => p.x == 0 || p.x == 4 || p.y == 0 || p.y == 0.2f);
            for (int i = 0; i < render.Length; i++)
                Assert.That(math.all(math.isfinite(render[i].uv0)), Is.True);
            indices.Dispose(); positions.Dispose(); render.Dispose();
        }

        static bool Less(float3 a, float3 b) => a.x != b.x ? a.x < b.x : (a.y != b.y ? a.y < b.y : a.z < b.z);

        static float2 UVOf(NativeList<RenderVertex> render, float3 position)
        {
            for (int i = 0; i < render.Length; i++)
            {
                if (math.distance(render[i].position, position) < 1e-5f)
                    return render[i].uv0;
            }
            Assert.Fail($"no vertex at {position}");
            return default;
        }

        [Test]
        public void ATriangleSmallerThanItsBorder_IsAllBorder()
        {
            // 16 pixels: a sixteenth of a unit, more than the triangle's inradius of 0.05
            using var sheet = new OneTile(16, new Vector2Int(1792, 1024), new Vector2Int(1792, 1152), new Vector2Int(1536, 1152));
            var shape  = Shape(new float3(0, 0, 0), new float3(0.2f, 0, 0), new float3(0.2f, 0.15f, 0));
            var choice = sheet.Build(shape);
            Assert.That(HotspotSections.IsAllBorder(HotspotSections.Input(sheet.tiles[0], sheet.data, shape, choice, new float3(0, 0, -1))), Is.True);
            AreEqual(new float2(1792, 896),               sheet.UV(0.2, 0),          "the right angle");
            AreEqual(new float2(1792 - 2.56f, 896 + 2.56f), sheet.UV(0.2 - 0.01, 0.01), "inside its corner, unstretched");
            for (double x = 0.01; x < 0.2; x += 0.0071)
            {
                for (double y = 0.001; y < x * 0.75; y += 0.0043)
                    Assert.That(math.all(math.isfinite(sheet.UV(x, y))), Is.True, $"at ({x}, {y})");
            }
        }

        [Test]
        public void AWall_IsCutWhereItsSectionsMeet_TheTrianglesOnItsDiagonalSharingTheirPoints()
        {
            using var sheet = new OneTile(32, B1);
            sheet.Build(Wall(4.1f, 2.3f));
            using var indices  = new NativeList<int>(Allocator.Temp) { 0, 1, 2, 0, 2, 3 };
            using var collider = new NativeList<float3>(Allocator.Temp) { new float3(0, 0, 0), new float3(4.1f, 0, 0), new float3(4.1f, 2.3f, 0), new float3(0, 2.3f, 0) };
            using var select   = new NativeList<SelectVertex>(Allocator.Temp);
            using var render   = new NativeList<RenderVertex>(Allocator.Temp);
            foreach (var p in collider)
            {
                select.Add(new SelectVertex { position = p });
                render.Add(new RenderVertex { position = p, normal = new float3(0, 0, -1) });
            }
            sheet.border.Split(new float3(1, 0, 0), new float3(0, 1, 0), new float3(0, 0, -1), indices, collider, select, render);

            // 4 corners, 2 points on each side, 4 inner corners and the 4 points where the lines cross the diagonal
            Assert.That(collider.Length, Is.EqualTo(20));
            Assert.That(render.Length, Is.EqualTo(20));
            Assert.That(select.Length, Is.EqualTo(20));
            var area = 0.0;
            var cells = sheet.border.cells;
            for (int t = 0; t < indices.Length; t += 3)
            {
                var a = (double3)collider[indices[t]]; var b = (double3)collider[indices[t + 1]]; var c = (double3)collider[indices[t + 2]];
                var doubled = (b.x - a.x) * (c.y - a.y) - (b.y - a.y) * (c.x - a.x);
                Assert.That(doubled, Is.Not.EqualTo(0), "a flat triangle");
                Assert.That(doubled, Is.GreaterThan(0), "the winding changed");
                area += doubled * 0.5;
                // in one section: the one its middle is in holds all its corners
                var middle = ((a + b + c) / 3).xy;
                var inside = false;
                for (int s = 0; s < cells.Length && !inside; s++)
                {
                    var cell = cells[s];
                    var all = true;
                    foreach (var corner in new[] { middle, a.xy, b.xy, c.xy })
                    {
                        for (int h = cell.halfPlaneStart; h < cell.halfPlaneStart + cell.halfPlaneCount; h++)
                            all &= sheet.border.halfPlanes[h].side * sheet.border.lines[sheet.border.halfPlanes[h].line].Value(corner) >= -1e-9;
                    }
                    inside = all;
                }
                Assert.That(inside, Is.True, $"triangle {t / 3} lies across sections");
            }
            Assert.That(area, Is.EqualTo((double)4.1f * 2.3f).Within(1e-9), "the pieces cover the wall once");
        }
    }
}
