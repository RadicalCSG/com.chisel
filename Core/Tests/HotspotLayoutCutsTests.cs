using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Chisel.Core.Tests
{
    [TestFixture]
    public class HotspotLayoutCutsTests
    {
        ChiselHotspotLayout layout;

        [SetUp]
        public void SetUp()
        {
            layout = ScriptableObject.CreateInstance<ChiselHotspotLayout>();
            layout.size = new Vector2Int(1024, 512);
            layout.ResetCuts();
        }

        [TearDown]
        public void TearDown() { Object.DestroyImmediate(layout); }

        static Vector2Int P(int x, int y) => new(x, y);

        HotspotTile Tile(int id) => layout.tiles[layout.IndexOfTile(id)];

        static long DoubledArea(Vector2Int[] points)
        {
            long area = 0;
            for (int i = 0; i < points.Length; i++)
                area += (long)points[i].x * points[(i + 1) % points.Length].y - (long)points[(i + 1) % points.Length].x * points[i].y;
            return System.Math.Abs(area);
        }

        void AssertCovered()
        {
            Assert.That(layout.tiles.Sum(t => DoubledArea(t.points)), Is.EqualTo(2L * layout.size.x * layout.size.y), "the tiles cover the texture once");
            Assert.That(layout.tiles.Select(t => t.id).Distinct().Count(), Is.EqualTo(layout.tiles.Count), "tile ids are unique");
        }

        [Test]
        public void ANewLayout_IsOneTileOverTheTexture()
        {
            Assert.That(layout.tiles.Count, Is.EqualTo(1));
            Assert.That(layout.tiles[0].points, Is.EqualTo(new[] { P(0, 0), P(1024, 0), P(1024, 512), P(0, 512) }));
            Assert.That(layout.TileAt(new Vector2(700, 300)), Is.EqualTo(layout.tiles[0].id));
        }

        [Test]
        public void SplittingATile_GivesTilesThatCoverIt_TheFirstKeepsItsIdAndTheOthersItsProperties()
        {
            var tile = layout.tiles[0]; tile.flags = HotspotTileFlags.AllowMirror; tile.border = 8; layout.tiles[0] = tile;
            Assert.That(layout.Split(1, HotspotCutKind.Vertical, 512), Is.True);
            Assert.That(layout.Split(1, HotspotCutKind.Horizontal, 128), Is.True);
            Assert.That(layout.tiles.Count, Is.EqualTo(3));
            Assert.That(Tile(1).points, Is.EqualTo(new[] { P(0, 0), P(512, 0), P(512, 128), P(0, 128) }), "the first part keeps the id");
            var right = layout.TileAt(new Vector2(800, 300));
            Assert.That(Tile(right).points, Is.EqualTo(new[] { P(512, 0), P(1024, 0), P(1024, 512), P(512, 512) }));
            Assert.That(Tile(right).flags, Is.EqualTo(HotspotTileFlags.AllowMirror));
            Assert.That(Tile(right).border, Is.EqualTo(8));
            AssertCovered();

            Assert.That(layout.Split(1, HotspotCutKind.Vertical, 512), Is.False, "a line on the tile's edge");
            Assert.That(layout.Split(1, HotspotCutKind.Horizontal, 300), Is.False, "a line outside the tile");
        }

        [Test]
        public void DiagonalsAndApexes_GiveTriangles_ThatAreNotCutFurther()
        {
            layout.Split(1, HotspotCutKind.Vertical, 256);
            layout.Split(1, HotspotCutKind.Slash, 0);
            var right = layout.TileAt(new Vector2(600, 300));
            layout.Split(right, HotspotCutKind.Apex, 640);
            Assert.That(layout.tiles.Count(t => t.points.Length == 3), Is.EqualTo(5));
            Assert.That(layout.tiles.Count, Is.EqualTo(5));
            AssertCovered();

            Assert.That(layout.TileAt(new Vector2(10, 10)), Is.EqualTo(1), "the upper left triangle of the slash");
            var lowerRight = layout.TileAt(new Vector2(250, 500));
            Assert.That(Tile(lowerRight).points, Is.EquivalentTo(new[] { P(256, 0), P(256, 512), P(0, 512) }));
            var middle = layout.TileAt(new Vector2(640, 400));
            Assert.That(Tile(middle).points, Is.EquivalentTo(new[] { P(640, 0), P(1024, 512), P(256, 512) }));
            Assert.That(layout.Split(middle, HotspotCutKind.Vertical, 640), Is.False, "a triangle is not cut");
        }

        [Test]
        public void MovingALine_MovesTheTilesBesideIt_AndStopsAtTheLinesInsideThem()
        {
            layout.Split(1, HotspotCutKind.Vertical, 512);
            var right = layout.TileAt(new Vector2(800, 100));
            layout.Split(right, HotspotCutKind.Vertical, 768);
            layout.Split(1, HotspotCutKind.Apex, 256);
            var lines = new List<HotspotLine>();
            layout.GetLines(lines);
            var middle = lines.First(l => l.a.x == 512 && l.b.x == 512).node;

            Assert.That(layout.MoveRange(middle, out var min, out var max), Is.True);
            Assert.That((min, max), Is.EqualTo((257, 767)), "between the apex on its left and the line on its right");
            Assert.That(layout.MoveCut(middle, 400), Is.True);
            Assert.That(Tile(right).points, Is.EqualTo(new[] { P(400, 0), P(768, 0), P(768, 512), P(400, 512) }));
            Assert.That(layout.MoveCut(middle, 2000), Is.True);
            Assert.That(Tile(right).points[0], Is.EqualTo(P(767, 0)), "it stops short of the line inside");
            AssertCovered();

            layout.GetLines(lines);
            var diagonal = layout.ParentOf(layout.NodeOfTile(1));
            Assert.That(layout.cuts[diagonal].kind, Is.EqualTo(HotspotCutKind.Apex));
            Assert.That(layout.MoveCut(diagonal, 10), Is.True, "an apex moves along the top");
            Assert.That(layout.Split(right, HotspotCutKind.Slash, 0), Is.True);
            Assert.That(layout.MoveRange(layout.ParentOf(layout.NodeOfTile(right)), out _, out _), Is.False, "a diagonal does not move");
        }

        [Test]
        public void RemovingALine_MergesTheTilesBesideIt_IntoTheLargestOfThem()
        {
            layout.Split(1, HotspotCutKind.Vertical, 256);
            var right = layout.TileAt(new Vector2(800, 100));
            layout.Split(right, HotspotCutKind.Horizontal, 384);
            var lowerRight = layout.TileAt(new Vector2(800, 450));
            Assert.That(layout.tiles.Count, Is.EqualTo(3));

            // the larger tile is the first of the two
            Assert.That(layout.RemoveCut(layout.ParentOf(layout.NodeOfTile(right))), Is.True);
            Assert.That(layout.tiles.Count, Is.EqualTo(2));
            Assert.That(layout.IndexOfTile(right), Is.GreaterThanOrEqualTo(0), "the larger tile is kept");
            Assert.That(layout.IndexOfTile(lowerRight), Is.LessThan(0));
            Assert.That(Tile(right).points, Is.EqualTo(new[] { P(256, 0), P(1024, 0), P(1024, 512), P(256, 512) }));
            AssertCovered();

            Assert.That(layout.RemoveCut(0), Is.True, "the first line");
            Assert.That(layout.tiles.Count, Is.EqualTo(1));
            Assert.That(layout.cuts.Count, Is.EqualTo(1), "nothing is left of the removed lines");
            AssertCovered();
        }

        [Test]
        public void ANewTextureSize_KeepsTheLinesWhereTheyAreOnTheTexture()
        {
            layout.Split(1, HotspotCutKind.Vertical, 256);
            layout.Split(1, HotspotCutKind.Horizontal, 128);
            layout.Resize(new Vector2Int(2048, 1024));
            Assert.That(Tile(1).points, Is.EqualTo(new[] { P(0, 0), P(512, 0), P(512, 256), P(0, 256) }));
            AssertCovered();
        }

        static HotspotTile Given(int id, params int[] xy)
        {
            var points = new Vector2Int[xy.Length / 2];
            for (int i = 0; i < points.Length; i++)
                points[i] = P(xy[i * 2], xy[i * 2 + 1]);
            return new HotspotTile { id = id, name = "given " + id, points = points };
        }

        [Test]
        public void TilesGivenWithoutCuts_GetTheirCuts_WhenTheyAreAGrid()
        {
            layout.size = new Vector2Int(1024, 512);
            layout.cuts.Clear();
            layout.tiles = new List<HotspotTile>
            {
                Given(1, 0, 0, 512, 0, 512, 256, 0, 256),
                Given(2, 0, 256, 256, 256, 0, 512),                 // the slash's upper left triangle
                Given(3, 256, 256, 256, 512, 0, 512),               // its lower right
                Given(4, 512, 0, 640, 0, 512, 128),                 // an apex at 640 over 512..1024 x 0..128
                Given(5, 640, 0, 1024, 128, 512, 128),
                Given(6, 640, 0, 1024, 0, 1024, 128),
                // 256..512 x 256..512 and 512..1024 x 128..512 are not given
            };
            Assert.That(layout.BuildCuts(out var error), Is.True, error);
            Assert.That(layout.tiles.Count, Is.EqualTo(8), "the parts no tile covers are tiles too");
            Assert.That(layout.tiles.Count(t => (t.flags & HotspotTileFlags.Disabled) != 0), Is.EqualTo(2), "disabled");
            AssertCovered();
            Assert.That(Tile(1).points, Is.EqualTo(new[] { P(0, 0), P(512, 0), P(512, 256), P(0, 256) }), "given tiles keep where they are");
            Assert.That(Tile(5).points, Is.EquivalentTo(new[] { P(640, 0), P(1024, 128), P(512, 128) }));
            Assert.That(layout.TileAt(new Vector2(100, 300)), Is.EqualTo(2));
            Assert.That(layout.TileAt(new Vector2(250, 500)), Is.EqualTo(3));
        }

        [Test]
        public void TilesGivenWithoutCuts_KeepNoCuts_WhenTheyAreNotAGrid()
        {
            // a pinwheel: four rectangles around a square, no line goes all the way across
            layout.size = new Vector2Int(3, 3);
            layout.cuts.Clear();
            layout.tiles = new List<HotspotTile>
            {
                Given(1, 0, 0, 2, 0, 2, 1, 0, 1), Given(2, 2, 0, 3, 0, 3, 2, 2, 2), Given(3, 1, 2, 3, 2, 3, 3, 1, 3),
                Given(4, 0, 1, 1, 1, 1, 3, 0, 3), Given(5, 1, 1, 2, 1, 2, 2, 1, 2),
            };
            Assert.That(layout.BuildCuts(out var error), Is.False);
            Assert.That(error, Is.Not.Null.And.Not.Empty);
            Assert.That(layout.HasCuts, Is.False);
            Assert.That(layout.tiles.Count, Is.EqualTo(5), "the tiles stay as they were given");
            Assert.That(layout.TileAt(new Vector2(1.5f, 1.5f)), Is.EqualTo(5));
        }

        [Test]
        public void ADrawnShape_StaysOverTheCuts_WhenTheirLinesChange()
        {
            Assert.That(layout.Split(1, HotspotCutKind.Vertical, 512), Is.True);
            var hexagon = new[] { P(128, 0), P(384, 0), P(512, 222), P(384, 444), P(128, 444), P(0, 222) };
            var shape = layout.AddShape(hexagon);
            Assert.That(shape, Is.Not.Zero);
            Assert.That(layout.IsDrawn(shape), Is.True);
            Assert.That(layout.IsDrawn(1), Is.False, "the cuts' tiles are not drawn");
            Assert.That(ChiselHotspotLayout.IsShaped(Tile(shape)), Is.True);

            Assert.That(layout.Split(1, HotspotCutKind.Horizontal, 256), Is.True);
            var lines = new List<HotspotLine>();
            layout.GetLines(lines);
            var vertical = lines.First(l => l.a.x == 512 && l.b.x == 512).node;
            Assert.That(layout.MoveCut(vertical, 600), Is.True);
            Assert.That(layout.IndexOfTile(shape), Is.GreaterThanOrEqualTo(0), "kept when lines are added and moved");
            Assert.That(Tile(shape).points, Is.EqualTo(hexagon), "its points are its own");
            Assert.That(layout.RemoveCut(vertical), Is.True);
            Assert.That(layout.IndexOfTile(shape), Is.GreaterThanOrEqualTo(0), "kept when lines are removed");
            Assert.That(layout.tiles.Count, Is.EqualTo(2));
        }

        [Test]
        public void ADrawnShape_IsFoundBeforeTheTileUnderIt_ExceptInItsHoles()
        {
            var square = layout.AddShape(new[] { P(100, 100), P(400, 100), P(400, 400), P(100, 400) });
            var triangle = layout.AddShape(new[] { P(600, 100), P(900, 100), P(600, 400) });
            Assert.That(layout.AddHole(square, new[] { P(200, 200), P(300, 200), P(300, 300), P(200, 300) }), Is.True);
            Assert.That(layout.AddHole(1, new[] { P(10, 10), P(20, 10), P(20, 20) }), Is.False, "the cuts' tiles take no holes");
            Assert.That(layout.TileAt(new Vector2(150, 150)), Is.EqualTo(square));
            Assert.That(layout.TileAt(new Vector2(250, 250)), Is.EqualTo(1), "its hole shows the tile under it");
            Assert.That(layout.TileAt(new Vector2(650, 150)), Is.EqualTo(triangle));
            Assert.That(layout.TileAt(new Vector2(850, 350)), Is.EqualTo(1), "outside the triangle, inside its bounds");
            Assert.That(layout.AddShape(new[] { P(0, 0), P(10, 0), P(20, 0) }), Is.Zero, "no area");

            Assert.That(layout.RemoveShape(1), Is.False, "the cuts' tiles go with their lines");
            Assert.That(layout.RemoveShape(square), Is.True);
            Assert.That(layout.TileAt(new Vector2(150, 150)), Is.EqualTo(1));
        }

        [Test]
        public void ADrawnShape_HasItsCornersMovedAddedAndRemoved_AndMovesAsAWhole()
        {
            var square = layout.AddShape(new[] { P(100, 100), P(400, 100), P(400, 400), P(100, 400) });
            layout.AddHole(square, new[] { P(200, 200), P(300, 200), P(300, 300) });
            Assert.That(layout.MoveShapePoint(square, -1, 2, P(500, 450)), Is.True);
            Assert.That(Tile(square).points[2], Is.EqualTo(P(500, 450)));
            Assert.That(layout.InsertShapePoint(square, -1, 0, P(250, 50)), Is.True);
            Assert.That(Tile(square).points, Is.EqualTo(new[] { P(100, 100), P(250, 50), P(400, 100), P(500, 450), P(100, 400) }));
            Assert.That(layout.RemoveShapePoint(square, -1, 1), Is.True);
            Assert.That(Tile(square).points.Length, Is.EqualTo(4));
            Assert.That(layout.RemoveShapePoint(square, 0, 0), Is.False, "a hole keeps three corners");
            Assert.That(layout.MoveShapePoint(square, 0, 2, P(250, 200)), Is.False, "no corner leaves its loop without area");
            Assert.That(layout.MoveShape(square, P(10, -20)), Is.True);
            Assert.That(Tile(square).points[0], Is.EqualTo(P(110, 80)));
            Assert.That(Tile(square).holes[0].points[0], Is.EqualTo(P(210, 180)), "its holes with it");
            Assert.That(layout.RemoveShapeHole(square, 0), Is.True);
            Assert.That(Tile(square).holes, Is.Null);
            Assert.That(layout.MoveShapePoint(1, -1, 0, P(5, 5)), Is.False, "the cuts' tiles follow their lines");
        }

        [Test]
        public void ANewTextureSize_MovesDrawnShapesAndTheirHoles()
        {
            var square = layout.AddShape(new[] { P(100, 100), P(400, 100), P(400, 400), P(100, 400) });
            layout.AddHole(square, new[] { P(200, 200), P(300, 200), P(300, 300), P(200, 300) });
            layout.Resize(new Vector2Int(2048, 256));
            Assert.That(Tile(square).points, Is.EqualTo(new[] { P(200, 50), P(800, 50), P(800, 200), P(200, 200) }));
            Assert.That(Tile(square).holes[0].points, Is.EqualTo(new[] { P(400, 100), P(600, 100), P(600, 150), P(400, 150) }));
        }

        [Test]
        public void ShapedTiles_StayOverTheGrid_WhenItsCutsAreFound()
        {
            layout.cuts.Clear();
            layout.tiles.Clear();
            layout.tiles.Add(new HotspotTile { id = 1, name = "L", points = new[] { P(0, 0), P(512, 0), P(512, 512), P(0, 512) } });
            layout.tiles.Add(new HotspotTile { id = 2, name = "R", points = new[] { P(512, 0), P(1024, 0), P(1024, 512), P(512, 512) } });
            layout.tiles.Add(new HotspotTile { id = 3, name = "hex", points = new[] { P(128, 0), P(384, 0), P(512, 222), P(384, 444), P(128, 444), P(0, 222) } });
            Assert.That(layout.BuildCuts(out var error), Is.True, error);
            Assert.That(layout.HasCuts, Is.True);
            Assert.That(layout.IsDrawn(3), Is.True);
            Assert.That(layout.IsDrawn(1), Is.False);
            Assert.That(layout.TileAt(new Vector2(256, 222)), Is.EqualTo(3));
            Assert.That(layout.TileAt(new Vector2(800, 222)), Is.EqualTo(2));
        }

        [Test]
        public void TheTestSheetsJson_IsAGrid()
        {
            const string json = "{\"format\": \"chisel-hotspot-layout\", \"width\": 1024, \"height\": 512, \"origin\": \"top-left\", \"tiles\": [" +
                                "{\"name\": \"A\", \"points\": [0, 0, 512, 0, 512, 512, 0, 512], \"tags\": [\"rotate\"], \"border\": 16}," +
                                "{\"name\": \"Tr1\", \"points\": [512, 0, 1024, 0, 512, 512], \"tags\": []}," +
                                "{\"name\": \"Tr2\", \"points\": [1024, 0, 1024, 512, 512, 512], \"tags\": []}]}";
            Assert.That(ChiselHotspotLayout.TryReadJson(json, layout, out var error), Is.True, error);
            Assert.That(layout.HasCuts, Is.True);
            Assert.That(layout.cuts[0].kind, Is.EqualTo(HotspotCutKind.Vertical));
            Assert.That(layout.cuts[layout.cuts[0].second].kind, Is.EqualTo(HotspotCutKind.Slash));
            Assert.That(layout.tiles[0].border, Is.EqualTo(16));
            Assert.That(layout.tiles[0].flags, Is.EqualTo(HotspotTileFlags.AllowRotation));
            AssertCovered();
        }
    }
}
