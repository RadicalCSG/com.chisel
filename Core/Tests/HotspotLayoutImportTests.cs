using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Chisel.Core.Tests
{
    [TestFixture]
    public class HotspotLayoutImportTests
    {
        ChiselHotspotLayout layout;

        [SetUp]
        public void SetUp() { layout = ScriptableObject.CreateInstance<ChiselHotspotLayout>(); }

        [TearDown]
        public void TearDown() { Object.DestroyImmediate(layout); }

        static Vector2Int P(int x, int y) => new(x, y);

        HotspotTile Named(string name) => layout.tiles.Single(t => t.name == name);

        void AssertCovered()
        {
            long area = 0;
            foreach (var tile in layout.tiles)
            {
                var p = tile.points;
                for (int i = 0; i < p.Length; i++)
                    area += (long)p[i].x * p[(i + 1) % p.Length].y - (long)p[(i + 1) % p.Length].x * p[i].y;
            }
            Assert.That(System.Math.Abs(area), Is.EqualTo(2L * layout.size.x * layout.size.y), "the tiles cover the texture once");
        }

        [Test]
        public void AValveRectFile_IsRead_WithItsFlags()
        {
            const string rect = "// a comment\n" +
                                "Rectangles\n{\n" +
                                "\trectangle\n\t{\n\t\tmin\t\t\"0 0\"\n\t\tmax\t\t\"512 32\"\n\t\ttile 1\n\t}\n" +
                                "\t\"rectangle\"\n\t{\n\t\t\"min\" \"512 0\"\n\t\t\"max\" \"1024 32\"\n\t\treflect 1\n\t\trotate 1\n\t\talt 1\n\t}\n" +
                                "\trectangle\n\t{\n\t\tmin \"0 32\"\n\t\tmax \"1024 64\"\n\t\ttile 1\n\t}\n" +
                                "}\n";
            Assert.That(ChiselHotspotLayout.TryReadRect(rect, new Vector2Int(1024, 512), layout, out var error), Is.True, error);
            Assert.That(layout.size, Is.EqualTo(new Vector2Int(1024, 512)));
            Assert.That(layout.tiles.Count(t => (t.flags & HotspotTileFlags.Disabled) == 0), Is.EqualTo(3), "the three rectangles");
            var first = layout.tiles.Single(t => t.id == 1);
            Assert.That(first.points, Is.EqualTo(new[] { P(0, 0), P(512, 0), P(512, 32), P(0, 32) }));
            Assert.That(first.flags, Is.EqualTo(HotspotTileFlags.None), "tile, but it does not reach across the texture");
            Assert.That(layout.tiles.Single(t => t.id == 2).flags, Is.EqualTo(HotspotTileFlags.AllowMirror | HotspotTileFlags.AllowRotation | HotspotTileFlags.Alternate));
            Assert.That(layout.tiles.Single(t => t.id == 3).flags, Is.EqualTo(HotspotTileFlags.RepeatU), "tile, and it reaches across the texture");
            Assert.That(layout.HasCuts, Is.True, "the rectangles are a grid; the rest of the texture is a disabled tile");
            AssertCovered();

            Assert.That(ChiselHotspotLayout.TryReadRect(rect, Vector2Int.zero, layout, out error), Is.False, "a .rect file has no size of its own");
            Assert.That(ChiselHotspotLayout.TryReadRect("Rectangles { }", new Vector2Int(64, 64), layout, out _), Is.False, "no rectangles");
        }

        [Test]
        public void AnSvg_IsRead_RectsAndPolygons_WithNamesTagsAndBorders()
        {
            const string svg = "<svg xmlns=\"http://www.w3.org/2000/svg\" xmlns:inkscape=\"http://www.inkscape.org/namespaces/inkscape\" viewBox=\"0 0 1024 512\" width=\"1024px\" height=\"512px\">" +
                               "<rect id=\"panel\" x=\"0\" y=\"0\" width=\"512\" height=\"512\" class=\"rotate border-16\"/>" +
                               "<g><rect inkscape:label=\"round\" x=\"512\" y=\"0\" width=\"256\" height=\"256\" data-tags=\"radial\"/></g>" +
                               "<polygon id=\"upper\" points=\"768,0 1024,0 768,256\"/>" +
                               "<polygon id=\"lower\" points=\"1024,0 1024,256 768,256\"/>" +
                               "<polygon id=\"strip\" points=\"512,256 1024,256 1024,512 512,512 512,256\" class=\"Repeat-U\"/>" +
                               "</svg>";
            Assert.That(ChiselHotspotLayout.TryReadSvg(svg, layout, out var error), Is.True, error);
            Assert.That(layout.size, Is.EqualTo(new Vector2Int(1024, 512)));
            Assert.That(layout.tiles.Count, Is.EqualTo(5));
            Assert.That(Named("panel").points, Is.EqualTo(new[] { P(0, 0), P(512, 0), P(512, 512), P(0, 512) }));
            Assert.That(Named("panel").flags, Is.EqualTo(HotspotTileFlags.AllowRotation));
            Assert.That(Named("panel").border, Is.EqualTo(16));
            Assert.That(Named("round").flags, Is.EqualTo(HotspotTileFlags.Radial), "named by its inkscape label, tagged by data-tags");
            Assert.That(Named("upper").points.Length, Is.EqualTo(3));
            Assert.That(Named("strip").flags, Is.EqualTo(HotspotTileFlags.RepeatU), "a closed polygon, tags in any case");
            Assert.That(layout.HasCuts, Is.True);
            AssertCovered();

            const string transformed = "<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 64 64\"><g transform=\"scale(2)\"><rect x=\"0\" y=\"0\" width=\"8\" height=\"8\"/></g></svg>";
            Assert.That(ChiselHotspotLayout.TryReadSvg(transformed, layout, out error), Is.False);
            Assert.That(error, Does.Contain("transform"));
            Assert.That(ChiselHotspotLayout.TryReadSvg("<html/>", layout, out _), Is.False, "not an SVG");
        }

        [Test]
        public void AnSvg_LinksATileToItsInsideCornerPieces()
        {
            const string svg = "<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 128 64\">" +
                               "<rect id=\"trim\" x=\"0\" y=\"0\" width=\"64\" height=\"64\" class=\"border-16\" data-inside-corners=\"corners\"/>" +
                               "<rect id=\"corners\" x=\"64\" y=\"0\" width=\"32\" height=\"32\" class=\"inside-corners\"/>" +
                               "<rect id=\"one\" x=\"96\" y=\"0\" width=\"16\" height=\"16\" data-tags=\"inside-corner-turned\"/>" +
                               "</svg>";
            Assert.That(ChiselHotspotLayout.TryReadSvg(svg, layout, out var error), Is.True, error);
            Assert.That(Named("trim").insideCorners, Is.EqualTo(Named("corners").id));
            Assert.That(Named("corners").flags, Is.EqualTo(HotspotTileFlags.InsideCorners));
            Assert.That(Named("one").flags, Is.EqualTo(HotspotTileFlags.InsideCornerTurned));
            Assert.That(ChiselHotspotLayout.TryReadSvg(svg.Replace("data-inside-corners=\"corners\"", "data-inside-corners=\"none\""), layout, out _), Is.False, "a link to no tile");
        }

        [Test]
        public void AnSvg_IsRead_PolygonsOfAnyShape_AndPathsWithHoles_AsShapedTiles()
        {
            const string svg = "<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 1024 512\">" +
                               "<polygon id=\"hex\" points=\"128,0 384,0 512,222 384,444 128,444 0,222\"/>" +
                               "<path id=\"frame\" d=\"M 512 0 H 1024 V 512 H 512 Z m 128 128 h 256 v 256 h -256 z\" class=\"border-8\"/>" +
                               "</svg>";
            Assert.That(ChiselHotspotLayout.TryReadSvg(svg, layout, out var error), Is.True, error);
            Assert.That(layout.tiles.Count, Is.GreaterThanOrEqualTo(2));
            Assert.That(Named("hex").points.Length, Is.EqualTo(6), "six corners kept");
            Assert.That(Named("hex").holes, Is.Null.Or.Empty);
            var frame = Named("frame");
            Assert.That(frame.points, Is.EqualTo(new[] { P(512, 0), P(1024, 0), P(1024, 512), P(512, 512) }), "the path's first loop");
            Assert.That(frame.holes.Length, Is.EqualTo(1), "its second loop a hole");
            Assert.That(frame.holes[0].points, Is.EqualTo(new[] { P(640, 128), P(896, 128), P(896, 384), P(640, 384) }), "relative moves and lines");
            Assert.That(frame.border, Is.EqualTo(8));
            // shaped tiles are not a grid: kept without lines
            Assert.That(layout.HasCuts, Is.False);
            Assert.That(ChiselHotspotLayout.TryReadSvg("<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 8 8\"><path d=\"M 0 0 C 1 1 2 2 3 3 Z\"/></svg>", layout, out error), Is.False);
            Assert.That(error, Does.Contain("straight lines"));
        }

        [Test]
        public void AColourMask_IsRead_OneTilePerColour()
        {
            // 8 x 4, the bottom row first: red on the left half, green at the top right, black (no tile) at the bottom right
            var red = new Color32(255, 0, 0, 255); var green = new Color32(0, 255, 0, 255); var black = new Color32(0, 0, 0, 255);
            var pixels = new Color32[8 * 4];
            for (int row = 0; row < 4; row++)
                for (int x = 0; x < 8; x++)
                    pixels[row * 8 + x] = x < 4 ? red : (row >= 2 ? green : black);
            Assert.That(ChiselHotspotLayout.TryReadColorMask(pixels, 8, 4, layout, out var error), Is.True, error);
            Assert.That(layout.size, Is.EqualTo(new Vector2Int(8, 4)));
            Assert.That(Named("#FF0000").points, Is.EqualTo(new[] { P(0, 0), P(4, 0), P(4, 4), P(0, 4) }));
            Assert.That(Named("#00FF00").points, Is.EqualTo(new[] { P(4, 0), P(8, 0), P(8, 2), P(4, 2) }), "the top right, from the top");
            Assert.That(layout.tiles.Count, Is.EqualTo(3), "and the black part as a disabled tile");
            AssertCovered();

            pixels[0] = green;
            Assert.That(ChiselHotspotLayout.TryReadColorMask(pixels, 8, 4, layout, out error), Is.False, "green is not one rectangle now");
            Assert.That(error, Does.Contain("00FF00"));
        }
    }
}
