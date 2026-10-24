using System.Collections.Generic;
using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace Chisel.Core.Tests
{
    [TestFixture]
    public class HotspotMatcherTests
    {
        // A 2048 sheet at 256 pixels a unit, cut like part of the test sheet (SourceAssets~/HotspotTest)
        static ChiselHotspotLayout Layout(params HotspotTile[] tiles)
        {
            var layout = ScriptableObject.CreateInstance<ChiselHotspotLayout>();
            layout.size = new Vector2Int(2048, 2048);
            layout.pixelsPerUnit = 256;
            layout.tiles = new List<HotspotTile>(tiles);
            return layout;
        }

        static HotspotTile Rect(int id, string name, int x, int y, int w, int h, HotspotTileFlags flags = HotspotTileFlags.None)
        {
            return new HotspotTile { id = id, name = name, flags = flags,
                                     points = new[] { new Vector2Int(x, y), new Vector2Int(x + w, y), new Vector2Int(x + w, y + h), new Vector2Int(x, y + h) } };
        }

        static HotspotTile Triangle(int id, string name, Vector2Int a, Vector2Int b, Vector2Int c, HotspotTileFlags flags = HotspotTileFlags.None)
        {
            return new HotspotTile { id = id, name = name, flags = flags, points = new[] { a, b, c } };
        }

        static ChiselHotspotLayout TestSheet()
        {
            return Layout(
                Rect(1, "A1", 0, 0, 512, 512),
                Rect(2, "A2", 512, 0, 512, 512),
                Rect(3, "B1", 1024, 0, 1024, 512),
                Rect(4, "C1", 0, 512, 1024, 256),
                Rect(5, "D1", 1024, 512, 256, 256),
                Rect(6, "E1", 1280, 512, 256, 512),
                Rect(7, "G1", 1536, 512, 128, 128),
                Rect(8, "R1", 0, 1024, 512, 512, HotspotTileFlags.Radial),
                Rect(9, "X", 512, 1024, 1024, 1024, HotspotTileFlags.Disabled),
                // a right triangle 256 x 128, its right angle at the bottom right as the texture shows it
                Triangle(10, "Tr6", new Vector2Int(1792, 1024), new Vector2Int(1792, 1152), new Vector2Int(1536, 1152)));
        }

        sealed class Tiles : System.IDisposable
        {
            public NativeList<HotspotTileData> list = new(Allocator.Temp);
            public HotspotLayoutData data;
            public ChiselHotspotLayout layout;
            public Tiles(ChiselHotspotLayout layout) { this.layout = layout; HotspotMatcher.AddTiles(layout, list, out data); }
            public string NameOf(int index) => index < 0 ? "none" : layout.tiles.Find(t => t.id == list[index].id).name;
            public void Dispose() { list.Dispose(); Object.DestroyImmediate(layout); }
        }

        // A wall facing -z from (x0, y0) to (x1, y1)
        static HotspotShape Wall(float width, float height)
        {
            using var positions = new NativeArray<float3>(new[] { new float3(0, 0, 0), new float3(width, 0, 0), new float3(width, height, 0), new float3(0, height, 0) }, Allocator.Temp);
            return HotspotMatcher.ShapeOf(positions, new float3(0, 0, -1));
        }

        static float2 UV(float4x4 mapping, float3 position) { return math.mul(mapping, new float4(position, 1)).xy; }

        static void AreEqual(float2 expected, float2 actual, string what)
        {
            Assert.That(math.distance(expected, actual), Is.LessThan(1e-5f), $"{what}: expected {expected}, got {actual}");
        }

        [Test]
        public void AnotherSheetsTile_IsOneVariation_WithoutTheLayoutsMaterials()
        {
            // two identical tiles of the layout's own, drawn with two materials, and one more on another sheet: 2 x 2 + 1
            var own   = Layout(Rect(1, "A1", 0, 0, 512, 512), Rect(2, "A2", 512, 0, 512, 512));
            var sheet = Layout(Rect(1, "S1", 0, 0, 512, 512));
            var tiles = new NativeList<HotspotTileData>(Allocator.Temp);
            try
            {
                HotspotMatcher.AddTiles(own, tiles, out var data);
                HotspotMatcher.AddTiles(sheet, tiles, out _);
                var t = tiles[2]; t.sheet = 2; t.sheetIndex = 1; tiles[2] = t;
                data.tileCount = tiles.Length - data.tileStart;
                data.variantCount = 2;
                using var corners = new NativeArray<float3>(new[] { new float3(0, 0, 0), new float3(2, 0, 0), new float3(2, 2, 0), new float3(0, 2, 0) }, Allocator.Temp);
                var shape = HotspotMatcher.ShapeOf(corners, new float3(1, 0, 0), new float3(0, 1, 0));
                var seen = new HashSet<(int tile, int variant)>();
                for (uint seed = 0; seed < 200; seed++)
                {
                    var choice = HotspotMatcher.Choose(tiles.AsArray(), data, shape, seed, 0);
                    Assert.That(choice.variations, Is.EqualTo(5));
                    if (choice.tile == 2)
                        Assert.That(choice.variant, Is.Zero, "the other sheet's tile keeps its own material");
                    seen.Add((choice.tile, choice.variant));
                }
                Assert.That(seen.Count, Is.EqualTo(5), "every variation is a different choice: " + string.Join(", ", seen));
            }
            finally
            {
                tiles.Dispose();
                Object.DestroyImmediate(own);
                Object.DestroyImmediate(sheet);
            }
        }

        [Test]
        public void ALayoutsJson_LinksATileToItsInsideCornerPieces()
        {
            const string json = "{\"format\": \"chisel-hotspot-layout\", \"width\": 1024, \"height\": 1024, \"tiles\": [" +
                                "{\"name\": \"B1\", \"points\": [0, 0, 512, 0, 512, 256, 0, 256], \"border\": 16, \"insideCorners\": \"In\"}," +
                                "{\"name\": \"In\", \"points\": [512, 0, 544, 0, 544, 32, 512, 32], \"tags\": [\"inside-corners\"]}," +
                                "{\"name\": \"In1\", \"points\": [544, 0, 560, 0, 560, 16, 544, 16], \"tags\": [\"inside-corner-turned\"]}]}";
            var layout = ScriptableObject.CreateInstance<ChiselHotspotLayout>();
            try
            {
                Assert.That(ChiselHotspotLayout.TryReadJson(json, layout, out var error), Is.True, error);
                Assert.That(layout.tiles[0].insideCorners, Is.EqualTo(layout.tiles[1].id));
                Assert.That(layout.tiles[1].flags, Is.EqualTo(HotspotTileFlags.InsideCorners));
                Assert.That(layout.tiles[2].flags, Is.EqualTo(HotspotTileFlags.InsideCornerTurned));
                Assert.That(ChiselHotspotLayout.TryReadJson(json.Replace("\"In\"}", "\"Nothing\"}"), layout, out _), Is.False, "a link to no tile");
            }
            finally
            {
                Object.DestroyImmediate(layout);
            }
        }

        [Test]
        public void ALayout_ReadsTheJsonTheTestSheetWrites()
        {
            const string json = "{\"format\": \"chisel-hotspot-layout\", \"version\": 1, \"width\": 2048, \"height\": 1024, \"origin\": \"top-left\", \"tiles\": [" +
                                "{\"name\": \"A1\", \"shape\": \"rectangle\", \"points\": [0, 0, 512, 0, 512, 512, 0, 512], \"tags\": []}," +
                                "{\"name\": \"Tr1\", \"shape\": \"triangle\", \"points\": [512, 0, 640, 0, 512, 128], \"tags\": [\"mirror\"]}," +
                                "{\"name\": \"T1\", \"shape\": \"rectangle\", \"points\": [0, 512, 2048, 512, 2048, 640, 0, 640], \"tags\": [\"repeat-u\", \"radial\"]}]," +
                                "\"cuts\": [\"y\", [[512, {\"name\": \"A1\"}]]]}";
            var layout = ScriptableObject.CreateInstance<ChiselHotspotLayout>();
            try
            {
                Assert.That(ChiselHotspotLayout.TryReadJson(json, layout, out var error), Is.True, error);
                Assert.That(layout.size, Is.EqualTo(new Vector2Int(2048, 1024)));
                Assert.That(layout.tiles.Count, Is.EqualTo(3));
                Assert.That(layout.tiles[0].points, Is.EqualTo(new[] { new Vector2Int(0, 0), new Vector2Int(512, 0), new Vector2Int(512, 512), new Vector2Int(0, 512) }));
                Assert.That(layout.tiles[1].points.Length, Is.EqualTo(3), "the triangle's corners");
                Assert.That(layout.tiles[1].flags, Is.EqualTo(HotspotTileFlags.AllowMirror));
                Assert.That(layout.tiles[2].flags, Is.EqualTo(HotspotTileFlags.RepeatU | HotspotTileFlags.Radial));
                Assert.That(new HashSet<int> { layout.tiles[0].id, layout.tiles[1].id, layout.tiles[2].id }.Count, Is.EqualTo(3), "ids are unique");
                Assert.That(ChiselHotspotLayout.TryReadJson("{\"format\": \"something else\"}", layout, out _), Is.False, "another format");
            }
            finally
            {
                Object.DestroyImmediate(layout);
            }
        }

        [Test]
        public void TheTiles_AreYUpAndCounterClockwise_WhicheverWayTheirCornersWereGiven()
        {
            var layout = Layout(Rect(1, "clockwise", 0, 0, 512, 256),
                                Triangle(2, "counter-clockwise on screen", new Vector2Int(0, 512), new Vector2Int(0, 768), new Vector2Int(256, 768)));
            using var tiles = new Tiles(layout);
            for (int t = 0; t < tiles.list.Length; t++)
            {
                var tile = tiles.list[t];
                var area = 0.0f;
                for (int i = 0; i < tile.cornerCount; i++)
                {
                    var a = tile.Corner(i); var b = tile.Corner((i + 1) % tile.cornerCount);
                    area += a.x * b.y - b.x * a.y;
                }
                Assert.That(area, Is.GreaterThan(0), tiles.NameOf(t) + " is not counter-clockwise");
            }
            Assert.That(tiles.list[0].min, Is.EqualTo(new float2(0, 1792)), "the rectangle's bottom left, y up");
            Assert.That(tiles.list[0].max, Is.EqualTo(new float2(512, 2048)), "the rectangle's top right, y up");
        }

        // A hexagon 512 x 444 pixels (points up and down are flat), from the top left of the texture at (x, y)
        static HotspotTile Hexagon(int id, string name, int x, int y)
        {
            return new HotspotTile { id = id, name = name, points = new[] {
                new Vector2Int(x + 128, y), new Vector2Int(x + 384, y), new Vector2Int(x + 512, y + 222),
                new Vector2Int(x + 384, y + 444), new Vector2Int(x + 128, y + 444), new Vector2Int(x, y + 222) } };
        }

        // A surface facing -z drawing these corners as a fan, in units
        static HotspotShape Fan(params float2[] corners)
        {
            var points = new float3[corners.Length];
            for (int i = 0; i < corners.Length; i++)
                points[i] = new float3(corners[i], 0);
            var fan = new int[(corners.Length - 2) * 3];
            for (int i = 1; i + 1 < corners.Length; i++)
            {
                fan[(i - 1) * 3] = 0; fan[(i - 1) * 3 + 1] = i; fan[(i - 1) * 3 + 2] = i + 1;
            }
            using var positions = new NativeArray<float3>(points, Allocator.Temp);
            using var indices = new NativeArray<int>(fan, Allocator.Temp);
            HotspotMatcher.Frame(new float3(0, 0, -1), out var right, out var up);
            var shape = HotspotMatcher.ShapeOf(positions, right, up);
            HotspotMatcher.MaskOf(ref shape, positions, indices, right, up);
            return shape;
        }

        // The hexagon tile's shape at 256 pixels a unit: 2 x 1.734 units
        static readonly float2[] kHexagonSurface = { new(0.5f, 0), new(1.5f, 0), new(2, 0.867f), new(1.5f, 1.734f), new(0.5f, 1.734f), new(0, 0.867f) };

        [Test]
        public void AShapedTile_IsTakenByASurfaceShapedLikeIt_BeforeARectangle()
        {
            var layout = Layout(Rect(1, "A1", 0, 0, 512, 444), Hexagon(2, "Hex", 1024, 0));
            using var tiles = new Tiles(layout);
            Assert.That(tiles.list[1].shaped, Is.True, "six corners: shaped");
            Assert.That(tiles.list[0].shaped, Is.False);
            var hexagon = Fan(kHexagonSurface);
            Assert.That(HotspotMatcher.Overlap(hexagon, tiles.list[1]), Is.GreaterThan(0.9f), "alike");
            var choice = HotspotMatcher.Choose(tiles.list.AsArray(), tiles.data, hexagon, 0, 0);
            Assert.That(tiles.NameOf(choice.tile), Is.EqualTo("Hex"));
            // a rectangle of the same size is not shaped like it: the rectangle tile
            var rectangle = Fan(new float2(0, 0), new float2(2, 0), new float2(2, 1.734f), new float2(0, 1.734f));
            Assert.That(HotspotMatcher.Overlap(rectangle, tiles.list[1]), Is.LessThan(HotspotMatcher.kShapeMatch));
            choice = HotspotMatcher.Choose(tiles.list.AsArray(), tiles.data, rectangle, 0, 0);
            Assert.That(tiles.NameOf(choice.tile), Is.EqualTo("A1"));
        }

        [Test]
        public void AHoledTile_IsTakenByAWallWithAHoleLikeIt()
        {
            // a 512 square frame with a 256 square hole in its middle, beside a plain 512 square
            var frame = Rect(2, "Frame", 1024, 0, 512, 512);
            frame.holes = new[] { new HotspotTileHole { points = new[] { new Vector2Int(1152, 128), new Vector2Int(1408, 128), new Vector2Int(1408, 384), new Vector2Int(1152, 384) } } };
            var layout = Layout(Rect(1, "Square", 0, 0, 512, 512), frame);
            using var tiles = new Tiles(layout);
            Assert.That(tiles.list[1].shaped, Is.True, "a hole: shaped");
            // a 2 x 2 wall with a 1 x 1 window in its middle, as four quads round it
            using var positions = new NativeArray<float3>(new[] {
                new float3(0, 0, 0), new float3(2, 0, 0), new float3(2, 2, 0), new float3(0, 2, 0),
                new float3(0.5f, 0.5f, 0), new float3(1.5f, 0.5f, 0), new float3(1.5f, 1.5f, 0), new float3(0.5f, 1.5f, 0) }, Allocator.Temp);
            using var indices = new NativeArray<int>(new[] { 0, 1, 5, 0, 5, 4,  1, 2, 6, 1, 6, 5,  2, 3, 7, 2, 7, 6,  3, 0, 4, 3, 4, 7 }, Allocator.Temp);
            HotspotMatcher.Frame(new float3(0, 0, -1), out var right, out var up);
            var wall = HotspotMatcher.ShapeOf(positions, right, up);
            HotspotMatcher.MaskOf(ref wall, positions, indices, right, up);
            var choice = HotspotMatcher.Choose(tiles.list.AsArray(), tiles.data, wall, 0, 0);
            Assert.That(tiles.NameOf(choice.tile), Is.EqualTo("Frame"));
            // a wall without the window: the plain square
            choice = HotspotMatcher.Choose(tiles.list.AsArray(), tiles.data, Fan(new float2(0, 0), new float2(2, 0), new float2(2, 2), new float2(0, 2)), 0, 0);
            Assert.That(tiles.NameOf(choice.tile), Is.EqualTo("Square"));
        }

        [Test]
        public void ALayoutsPadding_LeavesItsPixelsOutAlongEveryTilesEdges_AndTheBorderKeepsItsInnerEdge()
        {
            var bordered = Rect(1, "bordered", 0, 0, 512, 256);
            bordered.border = 32;
            var layout = Layout(bordered, Triangle(2, "triangle", new Vector2Int(0, 512), new Vector2Int(0, 768), new Vector2Int(256, 768)),
                                Rect(3, "tiny", 1024, 0, 3, 3));
            layout.padding = 2;
            using var tiles = new Tiles(layout);
            Assert.That(tiles.list[0].min, Is.EqualTo(new float2(2, 1794)), "the rectangle's bottom left, 2 pixels in");
            Assert.That(tiles.list[0].max, Is.EqualTo(new float2(510, 2046)), "its top right, 2 pixels in");
            Assert.That(tiles.list[0].border, Is.EqualTo(30), "its border 2 pixels narrower: its inner edge where it was");
            // the triangle's corners: each edge 2 pixels in, the right angle at (0, 1280) y up
            var triangle = tiles.list[1];
            Assert.That(triangle.min.x, Is.EqualTo(2).Within(1e-3f), "its left edge 2 pixels in");
            Assert.That(triangle.min.y, Is.EqualTo(1280 + 2).Within(1e-3f), "its bottom edge 2 pixels in");
            Assert.That(tiles.list[2].max - tiles.list[2].min, Is.EqualTo(new float2(3, 3)), "a tile too small for it keeps its corners");
        }

        [TestCase(0, 0, -1,  1, 0, 0,  0, 1, 0, TestName = "AWallFacingMinusZ_ReadsWithRightAlongPlusX")]
        [TestCase(1, 0, 0,   0, 0, 1,  0, 1, 0, TestName = "AWallFacingPlusX_ReadsWithRightAlongPlusZ")]
        [TestCase(0, 1, 0,   1, 0, 0,  0, 0, 1, TestName = "AFloor_ReadsWithUpAlongPlusZ")]
        [TestCase(0, -1, 0, -1, 0, 0,  0, 0, 1, TestName = "ACeiling_ReadsWithUpAlongPlusZ_FromBelow")]
        [TestCase(0.6f, 0.8f, 0, 0, 0, 1,  -0.8f, 0.6f, 0, TestName = "ASlopeFacingPlusX_ReadsUpItsSlope")]
        [TestCase(0, 0.8f, -0.6f, 1, 0, 0,  0, 0.6f, 0.8f, TestName = "ASlopeFacingMinusZ_ReadsUpItsSlope")]
        [TestCase(0.6f, -0.8f, 0, 0, 0, 1,  0.8f, 0.6f, 0, TestName = "AnOverhangFacingPlusX_ReadsUpItsSlope")]
        public void TheFrame(float nx, float ny, float nz, float rx, float ry, float rz, float ux, float uy, float uz)
        {
            HotspotMatcher.Frame(new float3(nx, ny, nz), out var right, out var up);
            Assert.That(math.distance(right, new float3(rx, ry, rz)), Is.LessThan(1e-6f), $"right: {right}");
            Assert.That(math.distance(up, new float3(ux, uy, uz)), Is.LessThan(1e-6f), $"up: {up}");
        }

        [Test]
        public void AShape_IsATriangle_WhenThreeCornersAreLeft_AndOtherwiseItsBounds()
        {
            var wall = Wall(4, 2);
            Assert.That(wall.cornerCount, Is.EqualTo(0), "a rectangle");
            Assert.That(wall.max - wall.min, Is.EqualTo(new float2(4, 2)));

            // a triangle with a point on one of its edges (a T junction the CSG leaves) and one inside
            using var positions = new NativeArray<float3>(new[] { new float3(0, 0, 0), new float3(1, 0, 0), new float3(2, 0, 0), new float3(2, 1, 0), new float3(1.5f, 0.25f, 0) }, Allocator.Temp);
            var triangle = HotspotMatcher.ShapeOf(positions, new float3(0, 0, -1));
            Assert.That(triangle.cornerCount, Is.EqualTo(3));
            var corners = new HashSet<float2> { triangle.c0, triangle.c1, triangle.c2 };
            Assert.That(corners, Is.EquivalentTo(new[] { new float2(0, 0), new float2(2, 0), new float2(2, 1) }));
            var c0 = triangle.c0; var c1 = triangle.c1; var c2 = triangle.c2;
            Assert.That((c1.x - c0.x) * (c2.y - c0.y) - (c1.y - c0.y) * (c2.x - c0.x), Is.GreaterThan(0), "counter-clockwise");
        }

        [TestCase(4, 2, "B1",  TestName = "A4x2Wall_Takes_The2To1Tile")]
        [TestCase(1, 1, "D1",  TestName = "A1x1Wall_Takes_The256Square")]
        [TestCase(0.5f, 0.5f, "G1", TestName = "AHalfUnitWall_Takes_The128Square")]
        [TestCase(1, 2, "E1",  TestName = "A1x2Wall_Takes_TheTallTile")]
        [TestCase(4, 1, "C1",  TestName = "A4x1Wall_Takes_The4To1Tile")]
        public void ARectangle_TakesTheTileClosestInShapeThenSize(float width, float height, string expected)
        {
            using var tiles = new Tiles(TestSheet());
            var choice = HotspotMatcher.Choose(tiles.list.AsArray(), tiles.data, Wall(width, height), 0, 0);
            Assert.That(tiles.NameOf(choice.tile), Is.EqualTo(expected));
        }

        [TestCase(90,  false,  0, 1, 0,  -1, 0, 0, TestName = "TheFrame_TurnedAQuarter_ReadsWithRightAlongUp")]
        [TestCase(180, false, -1, 0, 0,   0, -1, 0, TestName = "TheFrame_TurnedHalfway_IsUpsideDown")]
        [TestCase(0,   true,  -1, 0, 0,   0, 1, 0, TestName = "TheFrame_Mirrored_ReadsWithRightAlongMinusX")]
        [TestCase(90,  true,   0, -1, 0, -1, 0, 0, TestName = "TheFrame_TurnedThenMirrored")]
        public void TheFrame_OfAWallFacingMinusZ(float rotation, bool mirror, float rx, float ry, float rz, float ux, float uy, float uz)
        {
            HotspotMatcher.Frame(new float3(0, 0, -1), rotation, mirror, out var right, out var up);
            Assert.That(math.distance(right, new float3(rx, ry, rz)), Is.LessThan(1e-6f), $"right: {right}");
            Assert.That(math.distance(up, new float3(ux, uy, uz)), Is.LessThan(1e-6f), $"up: {up}");
        }

        [TestCase(0, 0, -1)]
        [TestCase(1, 0, 0)]
        [TestCase(0, 1, 0)]
        [TestCase(0.6f, 0, 0.8f)]
        public void TheFrame_OfAnUnturnedBrush_IsTheTreesFrame(float nx, float ny, float nz)
        {
            var normal = new float3(nx, ny, nz);
            HotspotMatcher.Frame(normal, out var right, out var up);
            HotspotMatcher.Frame(normal, normal, float4x4.Translate(new float3(5, -3, 2)), 0, false, out var brushRight, out var brushUp);
            Assert.That(math.distance(brushRight, right), Is.LessThan(1e-6f), $"right: {brushRight}");
            Assert.That(math.distance(brushUp, up), Is.LessThan(1e-6f), $"up: {brushUp}");
        }

        [Test]
        public void TheFrame_TurnsWithItsBrush()
        {
            // a wall facing -z on a brush rolled 30 degrees around z: up is the brush's up
            var nodeToTree = float4x4.TRS(new float3(1, 2, 3), quaternion.RotateZ(math.radians(30)), new float3(1));
            HotspotMatcher.Frame(new float3(0, 0, -1), new float3(0, 0, -1), nodeToTree, 0, false, out var right, out var up);
            Assert.That(math.distance(up, new float3(-0.5f, math.sqrt(3) / 2, 0)), Is.LessThan(1e-6f), $"up: {up}");
            Assert.That(math.distance(right, new float3(math.sqrt(3) / 2, 0.5f, 0)), Is.LessThan(1e-6f), $"right: {right}");
        }

        [Test]
        public void TheFrame_OfAStretchedBrush_StaysOrthonormal()
        {
            // a sloped face scaled unevenly: its normal in the tree is no longer the local one turned
            var localNormal = math.normalize(new float3(0, 1, 1));
            var nodeToTree  = float4x4.TRS(float3.zero, quaternion.RotateY(0.3f), new float3(1, 3, 0.5f));
            var normal      = math.normalize(math.mul(math.transpose(math.inverse(nodeToTree)), new float4(localNormal, 0)).xyz);
            HotspotMatcher.Frame(normal, localNormal, nodeToTree, 17, false, out var right, out var up);
            Assert.That(math.length(right), Is.EqualTo(1).Within(1e-6f));
            Assert.That(math.length(up), Is.EqualTo(1).Within(1e-6f));
            Assert.That(math.dot(right, up), Is.EqualTo(0).Within(1e-6f));
            Assert.That(math.dot(right, normal), Is.EqualTo(0).Within(1e-6f));
            Assert.That(math.dot(up, normal), Is.EqualTo(0).Within(1e-6f));
        }

        [Test]
        public void AWallTurnedAQuarter_TakesTheTileTurnedWalls_Take()
        {
            using var tiles = new Tiles(TestSheet());
            // 2 x 1 turned a quarter is 1 x 2 in its frame: the tall tile
            HotspotMatcher.Frame(new float3(0, 0, -1), 90, false, out var right, out var up);
            using var positions = new NativeArray<float3>(new[] { new float3(0, 0, 0), new float3(2, 0, 0), new float3(2, 1, 0), new float3(0, 1, 0) }, Allocator.Temp);
            var shape = HotspotMatcher.ShapeOf(positions, right, up);
            Assert.That(math.distance(shape.max - shape.min, new float2(1, 2)), Is.LessThan(1e-6f), $"{shape.max - shape.min}");
            Assert.That(tiles.NameOf(HotspotMatcher.Choose(tiles.list.AsArray(), tiles.data, shape, 0, 0).tile), Is.EqualTo("E1"));
        }

        [TestCase(2f, 2f, 1f)]
        [TestCase(0.5f, 2f, 4f)]
        public void AWallAtADensity_TakesTheTileAWallThatManyTimesItsSizeTakes(float density, float width, float height)
        {
            using var tiles = new Tiles(TestSheet());
            var dense = HotspotMatcher.Choose(tiles.list.AsArray(), tiles.data, Wall(width * density, height * density), 0, 0);
            var asked = HotspotMatcher.Choose(tiles.list.AsArray(), tiles.data, Wall(width, height), 0, 0, density: density);
            Assert.That(tiles.NameOf(asked.tile), Is.EqualTo(tiles.NameOf(dense.tile)));
            Assert.That(asked.scale, Is.EqualTo(dense.scale).Within(1e-5f));
        }

        [Test]
        public void IdenticalTiles_AreToldApartByTheSeed_TheSameSeedAlwaysTheSameTile()
        {
            using var tiles = new Tiles(TestSheet());
            var shape = Wall(2, 2);
            var seen = new HashSet<string>();
            for (uint seed = 0; seed < 16; seed++)
            {
                var first = tiles.NameOf(HotspotMatcher.Choose(tiles.list.AsArray(), tiles.data, shape, seed, 0).tile);
                Assert.That(first, Is.EqualTo("A1").Or.EqualTo("A2"));
                Assert.That(tiles.NameOf(HotspotMatcher.Choose(tiles.list.AsArray(), tiles.data, shape, seed, 0).tile), Is.EqualTo(first), $"seed {seed} changed its mind");
                seen.Add(first);
            }
            Assert.That(seen, Is.EquivalentTo(new[] { "A1", "A2" }), "16 seeds took only one of the two identical tiles");
        }

        [Test]
        public void WithVariantMaterials_TheSeedPicksATileAndAMaterial_AndOneMaterialPicksTheSameTilesAsBefore()
        {
            using var tiles = new Tiles(TestSheet());
            var shape = Wall(2, 2);
            var three = tiles.data; three.variantCount = 3;
            var seen = new HashSet<(string, int)>();
            for (uint seed = 0; seed < 64; seed++)
            {
                var alone = HotspotMatcher.Choose(tiles.list.AsArray(), tiles.data, shape, seed, 0);
                Assert.That(alone.variant, Is.Zero);
                Assert.That(alone.variations, Is.EqualTo(2), "two identical tiles");
                var choice = HotspotMatcher.Choose(tiles.list.AsArray(), three, shape, seed, 0);
                Assert.That(choice.variations, Is.EqualTo(6), "two tiles times three materials");
                Assert.That(choice.variant, Is.InRange(0, 2));
                seen.Add((tiles.NameOf(choice.tile), choice.variant));
            }
            Assert.That(seen.Count, Is.EqualTo(6), "64 seeds reach every tile with every material");
            var pinned = HotspotMatcher.Choose(tiles.list.AsArray(), three, shape, 5, tiles.layout.tiles.Find(t => t.name == "A1").id);
            Assert.That(pinned.variations, Is.EqualTo(3), "a pinned tile still takes any of the materials");
            Assert.That(HotspotMatcher.Choose(tiles.list.AsArray(), tiles.data, shape, 5, tiles.layout.tiles.Find(t => t.name == "A1").id).variations,
                        Is.EqualTo(1), "a pinned tile with one material: nothing to vary");
        }

        [Test]
        public void APinnedTile_IsKept_EvenWhenAnotherFitsBetter_AndADisabledOne()
        {
            using var tiles = new Tiles(TestSheet());
            Assert.That(tiles.NameOf(HotspotMatcher.Choose(tiles.list.AsArray(), tiles.data, Wall(2, 2), 0, 4).tile), Is.EqualTo("C1"));
            Assert.That(tiles.NameOf(HotspotMatcher.Choose(tiles.list.AsArray(), tiles.data, Wall(2, 2), 0, 9).tile), Is.EqualTo("X"), "pinned to a disabled tile");
        }

        [Test]
        public void DisabledRadialAndTriangleTiles_AreNotTakenByARectangle()
        {
            using var tiles = new Tiles(TestSheet());
            for (float size = 0.25f; size <= 16; size *= 2)
            {
                foreach (var shape in new[] { Wall(size, size), Wall(size * 2, size), Wall(size, size * 2) })
                {
                    var name = tiles.NameOf(HotspotMatcher.Choose(tiles.list.AsArray(), tiles.data, shape, 7, 0).tile);
                    Assert.That(name, Is.Not.EqualTo("X").And.Not.EqualTo("R1").And.Not.EqualTo("Tr6"), $"a {shape.max - shape.min} wall");
                }
            }
            Assert.That(tiles.NameOf(HotspotMatcher.Choose(tiles.list.AsArray(), tiles.data, Wall(2, 2), 0, 0, radial: true).tile), Is.EqualTo("R1"), "a radial surface takes the radial tile");
        }

        [Test]
        public void ATileThatMayTurn_IsTurned_WhenThatFitsBetter()
        {
            using var tiles = new Tiles(Layout(Rect(1, "square", 0, 0, 512, 512), Rect(2, "tall, may turn", 512, 0, 512, 1024, HotspotTileFlags.AllowRotation)));
            var choice = HotspotMatcher.Choose(tiles.list.AsArray(), tiles.data, Wall(4, 2), 0, 0);
            Assert.That(tiles.NameOf(choice.tile), Is.EqualTo("tall, may turn"));
            Assert.That(choice.rotated, Is.True);

            using var fixedTiles = new Tiles(Layout(Rect(1, "square", 0, 0, 512, 512), Rect(2, "tall", 512, 0, 512, 1024)));
            Assert.That(fixedTiles.NameOf(HotspotMatcher.Choose(fixedTiles.list.AsArray(), fixedTiles.data, Wall(4, 2), 0, 0).tile), Is.EqualTo("square"), "a tile that may not turn");
        }

        [Test]
        public void ARectangle_MapsItsBoundsOntoTheTile_UpStaysUp()
        {
            using var tiles = new Tiles(TestSheet());
            var shape = Wall(4, 2);
            var choice = HotspotMatcher.Choose(tiles.list.AsArray(), tiles.data, shape, 0, 0);
            var mapping = HotspotMatcher.Mapping(tiles.list[choice.tile], tiles.data, shape, choice, new float3(0, 0, -1));
            // B1 is pixels 1024..2048 x 0..512 from the top: u 0.5..1, v 0.75..1
            AreEqual(new float2(0.5f, 0.75f), UV(mapping, new float3(0, 0, 0)), "bottom left");
            AreEqual(new float2(1.0f, 0.75f), UV(mapping, new float3(4, 0, 0)), "bottom right");
            AreEqual(new float2(0.5f, 1.0f),  UV(mapping, new float3(0, 2, 0)), "top left");
            AreEqual(new float2(1.0f, 1.0f),  UV(mapping, new float3(4, 2, 0)), "top right");
        }

        [Test]
        public void ARectangleTurned_MapsWithoutMirroring()
        {
            using var tiles = new Tiles(Layout(Rect(1, "tall, may turn", 0, 0, 512, 1024, HotspotTileFlags.AllowRotation)));
            var shape = Wall(4, 2);
            var choice = HotspotMatcher.Choose(tiles.list.AsArray(), tiles.data, shape, 0, 0);
            Assert.That(choice.rotated, Is.True);
            var mapping = HotspotMatcher.Mapping(tiles.list[choice.tile], tiles.data, shape, choice, new float3(0, 0, -1));
            var a = UV(mapping, new float3(0, 0, 0)); var b = UV(mapping, new float3(4, 0, 0)); var c = UV(mapping, new float3(0, 2, 0));
            Assert.That((b.x - a.x) * (c.y - a.y) - (b.y - a.y) * (c.x - a.x), Is.GreaterThan(0), "the texture is mirrored");
        }

        // A right triangle 1 x 0.5 facing -z, its right angle in a corner of its bounds: 0 bottom right, 1 top left
        static HotspotShape RightTriangle(int corner)
        {
            var points = corner == 0 ? new[] { new float3(0, 0, 0), new float3(1, 0, 0), new float3(1, 0.5f, 0) }
                                     : new[] { new float3(0, 0.5f, 0), new float3(0, 0, 0), new float3(1, 0.5f, 0) };
            using var positions = new NativeArray<float3>(points, Allocator.Temp);
            return HotspotMatcher.ShapeOf(positions, new float3(0, 0, -1));
        }

        // Two right triangle tiles 256 x 128 cut from one rectangle by the diagonal from its bottom left to its top right
        static ChiselHotspotLayout TwoRightTriangles(HotspotTileFlags flags = HotspotTileFlags.None)
        {
            return Layout(Triangle(1, "right angle top left", new Vector2Int(0, 0), new Vector2Int(256, 0), new Vector2Int(0, 128), flags),
                          Triangle(2, "right angle bottom right", new Vector2Int(256, 0), new Vector2Int(256, 128), new Vector2Int(0, 128), flags));
        }

        [Test]
        public void ATriangle_KeepsTheTexturesUp_TakingTheTileTurnedTheSameWay()
        {
            using var tiles = new Tiles(TwoRightTriangles());
            for (uint seed = 0; seed < 16; seed++)
            {
                Assert.That(tiles.NameOf(HotspotMatcher.Choose(tiles.list.AsArray(), tiles.data, RightTriangle(0), seed, 0).tile), Is.EqualTo("right angle bottom right"), $"seed {seed}");
                Assert.That(tiles.NameOf(HotspotMatcher.Choose(tiles.list.AsArray(), tiles.data, RightTriangle(1), seed, 0).tile), Is.EqualTo("right angle top left"), $"seed {seed}");
            }
        }

        [Test]
        public void ATriangleTileThatMayTurn_IsTurnedToFit()
        {
            using var tiles = new Tiles(Layout(Triangle(1, "right angle top left, may turn", new Vector2Int(0, 0), new Vector2Int(256, 0), new Vector2Int(0, 128), HotspotTileFlags.AllowRotation)));
            var choice = HotspotMatcher.Choose(tiles.list.AsArray(), tiles.data, RightTriangle(0), 0, 0);
            Assert.That(choice.score, Is.LessThan(1e-4f), "turned half a turn it fits exactly");
        }

        [Test]
        public void ATilesOwnPixelsPerUnit_DecidesTheSizeItFits()
        {
            var layout = Layout(Rect(1, "512 square", 0, 0, 512, 512), Rect(2, "512 square at 128 a unit", 512, 0, 512, 512));
            var tile = layout.tiles[1]; tile.pixelsPerUnit = 128; layout.tiles[1] = tile;
            using var tiles = new Tiles(layout);
            // 512 pixels are 2 units at the layout's 256 a unit, and 4 at the tile's own 128
            Assert.That(tiles.NameOf(HotspotMatcher.Choose(tiles.list.AsArray(), tiles.data, Wall(2, 2), 0, 0).tile), Is.EqualTo("512 square"));
            Assert.That(tiles.NameOf(HotspotMatcher.Choose(tiles.list.AsArray(), tiles.data, Wall(4, 4), 0, 0).tile), Is.EqualTo("512 square at 128 a unit"));
        }

        // A regular polygon of this many corners around (0, 0), facing -z, stretched along x
        static HotspotShape Polygon(int corners, float radius, float stretch = 1)
        {
            var points = new float3[corners];
            for (int i = 0; i < corners; i++)
            {
                var angle = 2 * math.PI * i / corners;
                points[i] = new float3(math.cos(angle) * radius * stretch, math.sin(angle) * radius, 0);
            }
            using var positions = new NativeArray<float3>(points, Allocator.Temp);
            return HotspotMatcher.ShapeOf(positions, new float3(0, 0, -1));
        }

        [Test]
        public void ARoundSurface_IsFound_AndTakesTheRadialTile()
        {
            Assert.That(Polygon(24, 1).radial, Is.True, "24 corners");
            Assert.That(Polygon(8, 1).radial, Is.True, "8 corners");
            Assert.That(Polygon(6, 1).radial, Is.False, "a hexagon");
            Assert.That(Wall(2, 2).radial, Is.False, "a square");
            Assert.That(Polygon(24, 1, 2).radial, Is.False, "an ellipse");
            using var tiles = new Tiles(TestSheet());
            Assert.That(tiles.NameOf(HotspotMatcher.Choose(tiles.list.AsArray(), tiles.data, Polygon(24, 1), 0, 0).tile), Is.EqualTo("R1"));
        }

        [Test]
        public void ARoundSurface_WithoutRadialTiles_TakesAnotherTile()
        {
            using var tiles = new Tiles(Layout(Rect(1, "A1", 0, 0, 512, 512), Rect(2, "B1", 1024, 0, 1024, 512)));
            Assert.That(tiles.NameOf(HotspotMatcher.Choose(tiles.list.AsArray(), tiles.data, Polygon(24, 1), 0, 0).tile), Is.EqualTo("A1"));
        }

        [Test]
        public void AnAlternateTile_IsOnlyTakenPinned()
        {
            using var tiles = new Tiles(Layout(Rect(1, "A1", 0, 0, 512, 512, HotspotTileFlags.Alternate), Rect(2, "B1", 1024, 0, 1024, 512)));
            Assert.That(tiles.NameOf(HotspotMatcher.Choose(tiles.list.AsArray(), tiles.data, Wall(2, 2), 0, 0).tile), Is.EqualTo("B1"));
            Assert.That(tiles.NameOf(HotspotMatcher.Choose(tiles.list.AsArray(), tiles.data, Wall(2, 2), 0, 1).tile), Is.EqualTo("A1"));
        }

        [Test]
        public void ASurfaceFarLargerThanAnyTile_RepeatsATile_AsALastResort()
        {
            using var tiles = new Tiles(TestSheet());
            // 40 x 3 units: the tiles are at most 4 x 2; stretched, the best of them would be magnified about six times
            var choice = HotspotMatcher.Choose(tiles.list.AsArray(), tiles.data, Wall(40, 3), 0, 0);
            Assert.That(choice.repeatX * math.max(1, choice.repeatY), Is.GreaterThan(1), $"{tiles.NameOf(choice.tile)} was not repeated");
            Assert.That(choice.scale, Is.GreaterThan(HotspotMatcher.kLastResortScale));
            // the fewest repeats: B1 (4 x 2) ten times by two, not the small tiles hundreds of times
            Assert.That(tiles.NameOf(choice.tile), Is.EqualTo("B1"));
            Assert.That(new int2(choice.repeatX, choice.repeatY), Is.EqualTo(new int2(10, 2)));
            Assert.That(choice.lastResort, Is.True, "it says it was a last resort");
            // a 4 x 2 wall fits B1 and is not repeated
            var fits = HotspotMatcher.Choose(tiles.list.AsArray(), tiles.data, Wall(4, 2), 0, 0);
            Assert.That(math.max(1, fits.repeatX) * math.max(1, fits.repeatY), Is.EqualTo(1));
            Assert.That(fits.lastResort, Is.False);
        }

        [Test]
        public void ATriangle_TakesATriangleTile_CornerToCorner()
        {
            using var tiles = new Tiles(TestSheet());
            // the same right triangle as the tile, 1 x 0.5 units, its right angle at the bottom right
            using var positions = new NativeArray<float3>(new[] { new float3(0, 0, 0), new float3(1, 0, 0), new float3(1, 0.5f, 0) }, Allocator.Temp);
            var shape = HotspotMatcher.ShapeOf(positions, new float3(0, 0, -1));
            Assert.That(shape.cornerCount, Is.EqualTo(3));
            var choice = HotspotMatcher.Choose(tiles.list.AsArray(), tiles.data, shape, 0, 0);
            Assert.That(tiles.NameOf(choice.tile), Is.EqualTo("Tr6"));
            Assert.That(choice.score, Is.LessThan(1e-4f), "the same shape and size");
            var mapping = HotspotMatcher.Mapping(tiles.list[choice.tile], tiles.data, shape, choice, new float3(0, 0, -1));
            // Tr6: (1792, 1024) top right, (1792, 1152) bottom right - the right angle -, (1536, 1152) bottom left, from the top
            AreEqual(new float2(1536f / 2048, (2048f - 1152) / 2048), UV(mapping, new float3(0, 0, 0)), "the sharp corner");
            AreEqual(new float2(1792f / 2048, (2048f - 1152) / 2048), UV(mapping, new float3(1, 0, 0)), "the right angle");
            AreEqual(new float2(1792f / 2048, (2048f - 1024) / 2048), UV(mapping, new float3(1, 0.5f, 0)), "the top");
        }
    }
}
