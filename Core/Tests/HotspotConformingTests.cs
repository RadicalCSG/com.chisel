using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace Chisel.Core.Tests
{
    [TestFixture]
    public class HotspotConformingTests
    {
        // B1 of the test sheet alone: 1024 x 512 pixels at the top right of a 2048 sheet, 256 pixels a unit, a 32 pixel
        // border: an eighth of a unit
        sealed class Surface : System.IDisposable
        {
            public ChiselHotspotLayout layout;
            public NativeList<HotspotTileData> tiles = new(Allocator.Temp);
            public HotspotLayoutData data;
            public HotspotSections sections = HotspotSections.Create(Allocator.Temp);
            public HotspotSectionInput input;
            public NativeList<int> indices = new(Allocator.Temp);
            public NativeList<float3> positions = new(Allocator.Temp);

            public Surface(params float2[] triangles) : this(32, triangles) { }

            public Surface(int border, params float2[] triangles) : this(border, 0, triangles) { }

            public Surface(int border, float padding, params float2[] triangles) : this(border, padding, padding, triangles) { }

            public Surface(int border, float padding, float interiorPadding, params float2[] triangles) : this(border, padding, interiorPadding, null, triangles) { }

            // B1 linked to its inside corner pieces
            public Surface(HotspotTile pieces, params float2[] triangles) : this(32, 0, 0, pieces, triangles) { }

            Surface(int border, float padding, float interiorPadding, HotspotTile? pieces, float2[] triangles)
            {
                layout = ScriptableObject.CreateInstance<ChiselHotspotLayout>();
                layout.size = new Vector2Int(2048, 2048);
                layout.pixelsPerUnit = 256;
                layout.padding = padding;
                layout.interiorPadding = interiorPadding;
                layout.tiles = new List<HotspotTile> { new() { id = 1, name = "B1", border = border, insideCorners = pieces?.id ?? 0,
                    points = new[] { new Vector2Int(1024, 0), new Vector2Int(2048, 0), new Vector2Int(2048, 512), new Vector2Int(1024, 512) } } };
                if (pieces.HasValue)
                    layout.tiles.Add(pieces.Value);
                HotspotMatcher.AddTiles(layout, tiles, out data);
                var ids = new Dictionary<float2, int>();
                foreach (var p in triangles)
                {
                    if (!ids.TryGetValue(p, out var id))
                    {
                        id = positions.Length;
                        ids.Add(p, id);
                        positions.Add(new float3(p, 0));
                    }
                    indices.Add(id);
                }
                // facing -z: the frame's right is +x and its up +y
                var shape = HotspotMatcher.ShapeOf(positions.AsArray(), new float3(0, 0, -1));
                var choice = HotspotMatcher.Choose(tiles.AsArray(), data, shape, 0, 0);
                Assert.That(choice.tile, Is.EqualTo(0));
                input = HotspotSections.Input(tiles[0], data, shape, choice, new float3(0, 0, -1));
            }

            public float2 Pixels(double x, double y) => sections.UV(new double2(x, y)) * 2048;

            public void Dispose()
            {
                sections.Dispose();
                tiles.Dispose();
                indices.Dispose();
                positions.Dispose();
                Object.DestroyImmediate(layout);
            }
        }

        static float2[] Rectangle(float w, float h) => new[] { new float2(0, 0), new float2(w, 0), new float2(w, h), new float2(0, 0), new float2(w, h), new float2(0, h) };

        // Edges given a border (border 1) or none (-1) by hand, each between two corners
        static NativeArray<HotspotBorderCorner> Rules(int border, params float2[] corners)
        {
            var rules = new NativeArray<HotspotBorderCorner>(corners.Length, Allocator.Temp);
            for (int i = 0; i < corners.Length; i++)
                rules[i] = new HotspotBorderCorner { position = new float3(corners[i], 0), rule = 1, border = border };
            return rules;
        }

        // An 8 x 2 wall with a corner on its bottom edge at x = 2
        static float2[] WallWithACornerOnItsBottom() => new[]
        {
            new float2(0, 0), new float2(2, 0), new float2(0, 2),
            new float2(2, 0), new float2(8, 0), new float2(8, 2),
            new float2(2, 0), new float2(8, 2), new float2(0, 2),
        };

        [Test]
        public void TwoSquaresWithTheEdgeTheyShareKeptByHand_AreTwoLoops()
        {
            var squares = new List<float2>(Rectangle(2, 2));
            foreach (var p in Rectangle(2, 2)) squares.Add(p + new float2(2, 0));
            using var wall = new Surface(squares.ToArray());
            using var corners  = new NativeList<double2>(Allocator.Temp);
            using var loops    = new NativeList<int2>(Allocator.Temp);
            using var bordered = new NativeList<bool>(Allocator.Temp);
            HotspotSections.OutlineOf(wall.indices, wall.positions, new float3(1, 0, 0), new float3(0, 1, 0), default, corners, loops, bordered);
            Assert.That(loops.Length, Is.EqualTo(1), "together, one loop");
            using var rules = Rules(1, new float2(2, 0), new float2(2, 2));
            HotspotSections.OutlineOf(wall.indices, wall.positions, new float3(1, 0, 0), new float3(0, 1, 0), rules, corners, loops, bordered);
            Assert.That(loops.Length, Is.EqualTo(2), "with the edge kept, a loop each");
            Assert.That(loops[0].y, Is.EqualTo(4));
            Assert.That(loops[1].y, Is.EqualTo(4));
            foreach (var b in bordered)
                Assert.That(b, Is.True, "every edge has a border");
        }

        [Test]
        public void AnEdgeWithoutABorderOnPartOfIt_KeepsTheCornerWhereTheBorderStops()
        {
            using var wall = new Surface(WallWithACornerOnItsBottom());
            using var corners  = new NativeList<double2>(Allocator.Temp);
            using var loops    = new NativeList<int2>(Allocator.Temp);
            using var bordered = new NativeList<bool>(Allocator.Temp);
            HotspotSections.OutlineOf(wall.indices, wall.positions, new float3(1, 0, 0), new float3(0, 1, 0), default, corners, loops, bordered);
            Assert.That(loops.Length == 1 && loops[0].y == 4, Is.True, "by itself: no corner on a straight edge");
            using var rules = Rules(-1, new float2(2, 0), new float2(8, 0));
            HotspotSections.OutlineOf(wall.indices, wall.positions, new float3(1, 0, 0), new float3(0, 1, 0), rules, corners, loops, bordered);
            Assert.That(loops.Length == 1 && loops[0].y == 5, Is.True, "the corner where the border stops");
            var without = 0;
            for (int k = 0; k < corners.Length; k++)
            {
                if (bordered[k])
                    continue;
                without++;
                AreEqual(new float2(2, 0), (float2)corners[k], "the edge without a border starts at x = 2");
                AreEqual(new float2(8, 0), (float2)corners[(k + 1) % corners.Length], "and ends at the corner");
            }
            Assert.That(without, Is.EqualTo(1));
        }

        [Test]
        public void AnEdgeWithoutABorderOnPartOfIt_HasAStripOnlyWhereItHasOne()
        {
            using var wall = new Surface(WallWithACornerOnItsBottom());
            using var rules = Rules(-1, new float2(2, 0), new float2(8, 0));
            Assert.That(wall.sections.BuildConforming(wall.input, wall.indices, wall.positions, rules), Is.True);
            // B1 is 1024..2048 x 1536..2048 with y up: the bottom strip 16 pixels up where it has a border
            Assert.That(wall.Pixels(1, 0.0625).y, Is.EqualTo(1536 + 16).Within(1e-3f), "the bottom strip");
            Assert.That(wall.Pixels(2 - 0.01, 0.0625).y, Is.EqualTo(1536 + 16).Within(1e-3f), "up to where it stops");
            // past it, the interior (here its density up the wall is the border's, so the pixel alone cannot tell)
            Assert.That(wall.sections.Locate(new double2(2 - 0.01, 0.0625)), Is.Not.EqualTo(wall.sections.interiorCell), "the strip up to where it stops");
            Assert.That(wall.sections.Locate(new double2(2 + 0.01, 0.0625)), Is.EqualTo(wall.sections.interiorCell), "not past it");
            Assert.That(wall.sections.Locate(new double2(5, 0.0625)), Is.EqualTo(wall.sections.interiorCell), "nor further along");
            // the right side keeps its strip down to the bottom
            Assert.That(wall.Pixels(8 - 0.0625, 0.01).x, Is.EqualTo(2048 - 16).Within(1e-3f), "the right strip at the bottom");
            Assert.That(wall.sections.interiorToEdges, Is.True);
            Assert.That(wall.sections.openSides, Is.EqualTo(0), "the bottom has a border along part of it");
        }

        [Test]
        public void TheOffset_MovesABorderedTilesInteriorRepeatsAlong()
        {
            using var wall = new Surface(Rectangle(8, 2));
            Assert.That(wall.sections.BuildConforming(wall.input, wall.indices, wall.positions), Is.True);
            // B1's inside is 1056..2016 wide: 3.75 units a repeat at 256 pixels a unit
            using var repeats = HotspotSections.Create(Allocator.Temp);
            Assert.That(repeats.BuildInteriorRepeats(wall.input), Is.True);
            Assert.That((repeats.UV(new double2(0.125 + 1e-6, 1)) * 2048).x, Is.EqualTo(1056).Within(1e-2f), "unmoved, a repeat starts at the inner corner");
            var input = wall.input;
            input.interiorOffset = new float2(0.5f, 0);
            Assert.That(repeats.BuildInteriorRepeats(input), Is.True);
            Assert.That((repeats.UV(new double2(0.125 + 1e-6, 1)) * 2048).x, Is.EqualTo(1056 + 480).Within(1e-2f), "moved half a repeat along");
            Assert.That((repeats.UV(new double2(0.125 + 1.875 + 1e-6, 1)) * 2048).x, Is.EqualTo(1056).Within(1e-2f), "the next repeat starts half a repeat in");
        }

        // The border along one edge (from, to) given by hand
        static NativeArray<HotspotBorderCorner> Edge(int border, float2 from, float2 to)
        {
            return new NativeArray<HotspotBorderCorner>(new[] { new HotspotBorderCorner { position = new float3(from, 0), end = new float3(to, 0), segment = true, rule = 1, border = border } }, Allocator.Temp);
        }

        [Test]
        public void ARectangle_WithTheBorderAlongOneEdgeHidden_HasNoStripAlongIt()
        {
            using var wall = new Surface(Rectangle(8, 2));
            // the brush's edge a little longer than what is drawn of it, and a float step off: the outline's edge lies along it
            using var rules = Edge(-1, new float2(-1, 1e-6f), new float2(9, 0));
            Assert.That(wall.sections.BuildConforming(wall.input, wall.indices, wall.positions, rules), Is.True);
            Assert.That(wall.sections.openSides, Is.EqualTo(1), "the bottom");
            AreEqual(new float2(1024 + 16, 1536 + 32), wall.Pixels(0.0625, 0), "the left strip at the bottom");
            Assert.That(wall.Pixels(4, 2 - 0.0625).y, Is.EqualTo(2048 - 16).Within(1e-3f), "the top keeps its strip");
        }

        [Test]
        public void ARectangle_WithTheBorderAlongOneEdgeHidden_KeepsItsOtherEdges()
        {
            using var wall = new Surface(Rectangle(8, 2));
            // a rule along a line the outline only touches at a corner changes nothing
            using var rules = Edge(-1, new float2(8, 0), new float2(12, 0));
            Assert.That(wall.sections.BuildConforming(wall.input, wall.indices, wall.positions, rules), Is.True);
            Assert.That(wall.sections.openSides, Is.EqualTo(0));
            Assert.That(wall.sections.interiorToEdges, Is.False);
        }

        [Test]
        public void ARectangle_WithItsBottomBorderHiddenByHand_HasItsInteriorAndSidesRunOnToTheBottom()
        {
            using var wall = new Surface(Rectangle(8, 2));
            using var rules = Rules(-1, new float2(0, 0), new float2(8, 0));
            Assert.That(wall.sections.BuildConforming(wall.input, wall.indices, wall.positions, rules), Is.True);
            Assert.That(wall.sections.openSides, Is.EqualTo(1), "the bottom");
            // B1 is 1024..2048 x 1536..2048 with y up; its inside starts 32 pixels up. The sides' strips run down to the
            // bottom on the tile's side strips, and the interior starts at the bottom
            AreEqual(new float2(1024 + 16, 1536 + 32), wall.Pixels(0.0625, 0), "the left strip at the bottom");
            AreEqual(new float2(2048 - 16, 1536 + 32), wall.Pixels(8 - 0.0625, 0), "the right strip at the bottom");
            Assert.That(wall.Pixels(4, 1e-6).y, Is.EqualTo(1536 + 32).Within(1e-3f), "the interior at the bottom");
            // the top keeps its border
            Assert.That(wall.Pixels(4, 2 - 0.0625).y, Is.EqualTo(2048 - 16).Within(1e-3f), "the top strip");
            // and the interior's repeats start at the bottom too
            using var repeats = HotspotSections.Create(Allocator.Temp);
            Assert.That(repeats.BuildInteriorRepeats(wall.input, wall.sections.openSides, wall.sections.interiorToEdges), Is.True);
            Assert.That((repeats.UV(new double2(4, 1e-6)) * 2048).y, Is.EqualTo(1536 + 32).Within(1e-2f), "the first repeat at the bottom");
        }

        // A 6 x 3 wall with a doorway 1.2 wide and 2.3 high in its middle, in 6 triangles without T junctions
        static float2[] Doorway() => new[]
        {
            new float2(0, 0), new float2(2.4f, 0), new float2(2.4f, 2.3f),     new float2(0, 0), new float2(2.4f, 2.3f), new float2(0, 3),
            new float2(0, 3), new float2(2.4f, 2.3f), new float2(3.6f, 2.3f),  new float2(0, 3), new float2(3.6f, 2.3f), new float2(6, 3),
            new float2(3.6f, 2.3f), new float2(3.6f, 0), new float2(6, 0),     new float2(3.6f, 2.3f), new float2(6, 0), new float2(6, 3),
        };

        // A 6 x 3 wall with a window 1.5 x 1 in it, in 8 triangles without T junctions
        static float2[] Window() => new[]
        {
            new float2(0, 0), new float2(6, 0), new float2(3.75f, 1),       new float2(0, 0), new float2(3.75f, 1), new float2(2.25f, 1),
            new float2(6, 0), new float2(6, 3), new float2(3.75f, 2),       new float2(6, 0), new float2(3.75f, 2), new float2(3.75f, 1),
            new float2(6, 3), new float2(0, 3), new float2(2.25f, 2),       new float2(6, 3), new float2(2.25f, 2), new float2(3.75f, 2),
            new float2(0, 3), new float2(0, 0), new float2(2.25f, 1),       new float2(0, 3), new float2(2.25f, 1), new float2(2.25f, 2),
        };

        // A trapezoid 4 wide at its foot and 2 at its top, 2 high
        static float2[] Trapezoid() => new[] { new float2(0, 0), new float2(4, 0), new float2(3, 2), new float2(0, 0), new float2(3, 2), new float2(1, 2) };

        // An L: 4 x 2 with its top right 2 x 1 cut away
        static float2[] LShape() => new[]
        {
            new float2(0, 0), new float2(4, 0), new float2(4, 1),  new float2(0, 0), new float2(4, 1), new float2(2, 1),
            new float2(0, 0), new float2(2, 1), new float2(2, 2),  new float2(0, 0), new float2(2, 2), new float2(0, 2),
        };

        // Inside corner pieces at the sheet's top left: four in 64 x 64 pixels, their middle at (32, 2016) y up; or one,
        // 32 x 32, the corner of its outer edges at (32, 2016)
        static HotspotTile FourPieces => new() { id = 2, name = "Inside", flags = HotspotTileFlags.InsideCorners,
            points = new[] { new Vector2Int(0, 0), new Vector2Int(64, 0), new Vector2Int(64, 64), new Vector2Int(0, 64) } };
        static HotspotTile OnePiece => new() { id = 2, name = "Inside", flags = HotspotTileFlags.InsideCornerTurned,
            points = new[] { new Vector2Int(0, 0), new Vector2Int(32, 0), new Vector2Int(32, 32), new Vector2Int(0, 32) } };

        // A convex polygon as a fan of triangles from its first corner
        static float2[] Fan(params float2[] corners)
        {
            var triangles = new List<float2>();
            for (int i = 1; i + 1 < corners.Length; i++)
                triangles.AddRange(new[] { corners[0], corners[i], corners[i + 1] });
            return triangles.ToArray();
        }

        // A hexagon 4 wide and 2 high with a point at its top and its foot, as a cap seen from above
        static readonly float2[] kHexagon = { new(2, 0), new(4, 0.5f), new(4, 1.5f), new(2, 2), new(0, 1.5f), new(0, 0.5f) };

        // No triangle lies flat in the tile: its uvs have area
        static void AssertNoneFlat(Surface surface, NativeList<RenderVertex> render)
        {
            for (int t = 0; t < surface.indices.Length; t += 3)
            {
                var a = render[surface.indices[t]]; var b = render[surface.indices[t + 1]]; var c = render[surface.indices[t + 2]];
                var area = math.length(math.cross(b.position - a.position, c.position - a.position));
                if (area < 1e-6f)
                    continue;
                var uvArea = math.abs((b.uv0.x - a.uv0.x) * (c.uv0.y - a.uv0.y) - (b.uv0.y - a.uv0.y) * (c.uv0.x - a.uv0.x)) * 2048 * 2048;
                Assert.That(uvArea, Is.GreaterThan(1e-3f), $"triangle {t / 3} ({a.position}, {b.position}, {c.position}) lies flat in the tile");
            }
        }

        [Test]
        public void ABorderedHexagon_HasNoTriangleFlatInTheTile()
        {
            using var hexagon = new Surface(32, Fan(kHexagon));
            Assert.That(hexagon.sections.BuildConforming(hexagon.input, hexagon.indices, hexagon.positions), Is.True);
            using var render = Map(hexagon);
            AssertNoneFlat(hexagon, render);
            AssertNotFolded(hexagon, render);
        }

        [Test]
        public void ATrapezoidWithABorder_HasItsInteriorAsItsBoundsMapIt()
        {
            // undistorted: the inset bounds, 0.125 in from 4 x 2, onto the tile's, 32 pixels in
            using var trapezoid = new Surface(32, Trapezoid());
            Assert.That(trapezoid.sections.BuildConforming(trapezoid.input, trapezoid.indices, trapezoid.positions), Is.True);
            AreEqual(new float2(1536, 1792), trapezoid.Pixels(2, 1), "the middle");
            AreEqual(new float2(1408, 1664), trapezoid.Pixels(1.5, 0.5), "lower left");
            using var render = Map(trapezoid);
            AssertNotFolded(trapezoid, render);
        }

        [Test]
        public void AFaceWithoutABorder_IsNotCut()
        {
            using var trapezoid = new Surface(0, Trapezoid());
            Assert.That(trapezoid.sections.BuildConforming(trapezoid.input, trapezoid.indices, trapezoid.positions), Is.False);
            using var hexagon = new Surface(0, Fan(kHexagon));
            Assert.That(hexagon.sections.BuildConforming(hexagon.input, hexagon.indices, hexagon.positions), Is.False);
        }

        // The end of a spiral staircase's step: two rectangles touching at one corner, (1, 1)
        static float2[] TwoRectanglesTouchingAtACorner() => new[]
        {
            new float2(1, 0), new float2(3, 0), new float2(3, 1),  new float2(1, 0), new float2(3, 1), new float2(1, 1),
            new float2(0, 1), new float2(1, 1), new float2(1, 2),  new float2(0, 1), new float2(1, 2), new float2(0, 2),
        };

        [Test]
        public void TwoSurfacesMeetingAlongAnEdge_WithACornerOfOneOnTheOthersEdge_AreOneLoop()
        {
            // a 4 x 1 strip below two 2 x 1 pieces: the pieces' shared corner (2, 1) is on the strip's top edge
            using var wall = new Surface(0, new[]
            {
                new float2(0, 0), new float2(4, 0), new float2(4, 1),  new float2(0, 0), new float2(4, 1), new float2(0, 1),
                new float2(0, 1), new float2(2, 1), new float2(2, 2),  new float2(0, 1), new float2(2, 2), new float2(0, 2),
                new float2(2, 1), new float2(4, 1), new float2(4, 2),  new float2(2, 1), new float2(4, 2), new float2(2, 2),
            });
            using var corners = new NativeList<double2>(Allocator.Temp);
            using var loops = new NativeList<int2>(Allocator.Temp);
            HotspotSections.OutlineOf(wall.indices, wall.positions, new float3(1, 0, 0), new float3(0, 1, 0), corners, loops);
            Assert.That(loops.Length, Is.EqualTo(1), "loops");
            Assert.That(loops[0].y, Is.EqualTo(4), "corners: the 4 x 2 rectangle");
        }

        [Test]
        public void TwoRectanglesTouchingAtACorner_AreTwoLoops()
        {
            using var steps = new Surface(0, TwoRectanglesTouchingAtACorner());
            using var corners = new NativeList<double2>(Allocator.Temp);
            using var loops = new NativeList<int2>(Allocator.Temp);
            HotspotSections.OutlineOf(steps.indices, steps.positions, new float3(1, 0, 0), new float3(0, 1, 0), corners, loops);
            Assert.That(loops.Length, Is.EqualTo(2), "loops");
            Assert.That(loops[0].y, Is.EqualTo(4), "corners of the first");
            Assert.That(loops[1].y, Is.EqualTo(4), "corners of the second");
        }

        [Test]
        public void TwoRectanglesTouchingAtACorner_KeepTheirInteriorAsTheBoundsMapIt()
        {
            using var steps = new Surface(32, TwoRectanglesTouchingAtACorner());
            Assert.That(steps.sections.BuildConforming(steps.input, steps.indices, steps.positions), Is.True);
            using var render = Map(steps);
            AssertNoneFlat(steps, render);
        }

        // The surface cut and given its uvs as OutputHotspotSections does; render vertices returned
        static NativeList<RenderVertex> Map(Surface surface)
        {
            var select = new NativeList<SelectVertex>(Allocator.Temp);
            var render = new NativeList<RenderVertex>(Allocator.Temp);
            foreach (var p in surface.positions)
            {
                select.Add(new SelectVertex { position = p });
                render.Add(new RenderVertex { position = p, normal = new float3(0, 0, -1) });
            }
            var right = new float3(1, 0, 0); var up = new float3(0, 1, 0);
            if (!surface.sections.CutOnce(right, up, new float3(0, 0, -1), surface.indices, surface.positions, select, render))
                surface.sections.Split(right, up, new float3(0, 0, -1), surface.indices, surface.positions, select, render);
            surface.sections.AssignUVs(surface.indices, surface.positions, select, render, right, up);
            select.Dispose();
            return render;
        }

        [Test]
        public void ASliverOfTheCut_TakesItsNeighboursSection()
        {
            // two border bands and 0.2 mm between them: the interior there is a sliver, far less than a texel high
            using var wall = new Surface(32, Rectangle(4, 0.25f + 0.0002f));
            Assert.That(wall.sections.BuildConforming(wall.input, wall.indices, wall.positions), Is.True);
            using var render = Map(wall);
            var worst = 0.0;
            for (int t = 0; t + 2 < wall.indices.Length; t += 3)
            {
                var a = render[wall.indices[t]]; var b = render[wall.indices[t + 1]]; var c = render[wall.indices[t + 2]];
                if (!HotspotDiagnostics.Measure(a.position, b.position, c.position, a.uv0, b.uv0, c.uv0, new float2(2048), 256, out var s1, out var s2, out _))
                    continue;
                worst = math.max(worst, s2 > 0 ? s1 / s2 : double.PositiveInfinity);
            }
            Assert.That(worst, Is.LessThan(1.01), "no triangle squeezes a section into the sliver");
        }

        static float2 PixelsAt(NativeList<RenderVertex> render, float2 position)
        {
            for (int i = 0; i < render.Length; i++)
            {
                if (math.distance(render[i].position.xy, position) < 1e-5f)
                    return render[i].uv0 * 2048;
            }
            Assert.Fail($"no vertex at {position}");
            return default;
        }

        static void AssertNotFolded(Surface surface, NativeList<RenderVertex> render)
        {
            for (int t = 0; t < surface.indices.Length; t += 3)
            {
                var a = render[surface.indices[t]].uv0; var b = render[surface.indices[t + 1]].uv0; var c = render[surface.indices[t + 2]].uv0;
                var area = (b.x - a.x) * (c.y - a.y) - (b.y - a.y) * (c.x - a.x);
                Assert.That(area, Is.GreaterThan(0), $"triangle {t / 3} is folded over in the tile");
                foreach (var uv in new[] { a, b, c })
                    Assert.That(uv.x >= 0.5f - 1e-6f && uv.x <= 1 + 1e-6f && uv.y >= 0.75f - 1e-6f && uv.y <= 1 + 1e-6f, Is.True, $"{uv * 2048} is outside the tile");
            }
        }

        static bool OnSegment(float3 p, float2 a, float2 b)
        {
            var d = b - a;
            var t = math.dot(p.xy - a, d) / math.lengthsq(d);
            return t > -1e-6f && t < 1 + 1e-6f && math.distance(a + d * t, p.xy) < 1e-5f;
        }

        static System.Func<float3, bool> OnOutline(params float2[] corners) =>
            p => System.Linq.Enumerable.Range(0, corners.Length).Any(k => OnSegment(p, corners[k], corners[(k + 1) % corners.Length]));

        [Test]
        public void ABorderedTrapezoid_IsCutOnce_WithoutTJunctions()
        {
            using var trapezoid = new Surface(32, Trapezoid());
            Assert.That(trapezoid.sections.BuildConforming(trapezoid.input, trapezoid.indices, trapezoid.positions), Is.True);
            using var render = Map(trapezoid);
            // four strips, four corners in two halves and the interior
            Assert.That(trapezoid.indices.Length / 3, Is.LessThanOrEqualTo(26), "more triangles than its sections need");
            HotspotBorderTests.AssertWatertight(trapezoid.indices, trapezoid.positions, 6,
                                                OnOutline(new float2(0, 0), new float2(4, 0), new float2(3, 2), new float2(1, 2)));
        }

        [Test]
        public void ABorderedRectangleTurnedInItsFrame_IsCutOnce_WithoutTJunctions()
        {
            // a 3 x 1.5 rectangle turned 30 degrees, as a tumbled box's face is in the texture's frame
            math.sincos(math.radians(30f), out float sin, out float cos);
            var c0 = new float2(0, 0); var c1 = new float2(3 * cos, 3 * sin); var c3 = new float2(-1.5f * sin, 1.5f * cos); var c2 = c1 + c3;
            using var turned = new Surface(32, c0, c1, c2, c0, c2, c3);
            Assert.That(turned.sections.BuildConforming(turned.input, turned.indices, turned.positions), Is.True);
            using var render = Map(turned);
            Assert.That(turned.indices.Length / 3, Is.LessThanOrEqualTo(26), "more triangles than its sections need");
            HotspotBorderTests.AssertWatertight(turned.indices, turned.positions, 3 * 1.5, OnOutline(c0, c1, c2, c3));
            for (int i = 0; i < render.Length; i++)
                Assert.That(math.all(math.isfinite(render[i].uv0)), Is.True, $"vertex {i}'s uv");
        }

        [Test]
        public void AnLAWindowAndARectangle_KeepTheirInteriorAsTheBoundsMapIt()
        {
            using var l = new Surface(32, LShape());
            Assert.That(l.sections.BuildConforming(l.input, l.indices, l.positions), Is.True);
            using var window = new Surface(32, Window());
            Assert.That(window.sections.BuildConforming(window.input, window.indices, window.positions), Is.True);
            using var rectangle = new Surface(0, Rectangle(4, 2));
            Assert.That(rectangle.sections.BuildConforming(rectangle.input, rectangle.indices, rectangle.positions), Is.False, "a rectangle without a border is one section");
        }

        [Test]
        public void AnInsideCorner_TakesThePieceOnTheSideItsSquareLies()
        {
            // the L's inside corner at (2, 1): its square, an eighth of a unit each way, lies below and left of it
            using var l = new Surface(FourPieces, LShape());
            Assert.That(l.sections.BuildConforming(l.input, l.indices, l.positions), Is.True);
            AreEqual(new float2(32 - 2.56f, 2016 - 1.28f), l.Pixels(2 - 0.01, 1 - 0.005), "in the bottom left piece");
            AreEqual(new float2(0.256f, 1984.256f), l.Pixels(2 - 0.125 + 0.001, 1 - 0.125 + 0.001), "its far corner");

            // the doorway's: above and left of the first, above and right of the second
            using var door = new Surface(FourPieces, Doorway());
            Assert.That(door.sections.BuildConforming(door.input, door.indices, door.positions), Is.True);
            AreEqual(new float2(32 - 5.12f, 2016 + 1.28f), door.Pixels(2.4 - 0.02, 2.3 + 0.005), "in the top left piece");
            AreEqual(new float2(32 + 5.12f, 2016 + 1.28f), door.Pixels(3.6 + 0.02, 2.3 + 0.005), "in the top right piece");
        }

        [Test]
        public void OneInsideCornerPiece_IsTurnedNeverMirrored()
        {
            using var door = new Surface(OnePiece, Doorway());
            Assert.That(door.sections.BuildConforming(door.input, door.indices, door.positions), Is.True);
            // the first corner's square lies as the piece is drawn, the top left
            AreEqual(new float2(32 - 5.12f, 2016 + 1.28f), door.Pixels(2.4 - 0.02, 2.3 + 0.005), "as drawn");
            // the second's, its mirror image, takes the piece turned a quarter turn
            AreEqual(new float2(32 - 1.28f, 2016 + 5.12f), door.Pixels(3.6 + 0.02, 2.3 + 0.005), "turned");

            using var l = new Surface(OnePiece, LShape());
            Assert.That(l.sections.BuildConforming(l.input, l.indices, l.positions), Is.True);
            AreEqual(new float2(32 - 1.28f, 2016 + 2.56f), l.Pixels(2 - 0.01, 1 - 0.005), "turned a quarter turn the other way");
        }

        [Test]
        public void AtAnInsideCornerWithPieces_TheStripsStopAtEachOthersEdge()
        {
            using var with = new Surface(FourPieces, LShape());
            Assert.That(with.sections.BuildConforming(with.input, with.indices, with.positions), Is.True);
            using var without = new Surface(LShape());
            Assert.That(without.sections.BuildConforming(without.input, without.indices, without.positions), Is.True);
            // beside the square the strips are as they were
            foreach (var p in new[] { new double2(2.001, 0.95), new double2(2.3, 0.9), new double2(1.95, 1.001), new double2(1.9, 1.4) })
                AreEqual(without.Pixels(p.x, p.y), with.Pixels(p.x, p.y), $"at {p}");
            // inside it, without pieces, the strips' (the tile's right half)
            Assert.That(without.Pixels(2 - 0.01, 1 - 0.005).x, Is.GreaterThanOrEqualTo(1024));
            Assert.That(with.Pixels(2 - 0.01, 1 - 0.005).x, Is.LessThan(64));
        }

        [Test]
        public void InsideCornerPieces_AreNeverMatched()
        {
            using var l = new Surface(FourPieces, LShape());
            Assert.That(l.tiles.Length, Is.EqualTo(2));
            Assert.That(l.tiles[0].insideMax, Is.EqualTo(new float2(64, 2048)));
            Assert.That(l.tiles[0].insideMin, Is.EqualTo(new float2(0, 1984)));
            Assert.That(l.tiles[0].insideOne, Is.False);
            // a 0.25 square fits the pieces best by shape and size, yet takes B1
            using var small = new Surface(FourPieces, Rectangle(0.25f, 0.25f));
            Assert.That(small.input.t1.x, Is.GreaterThanOrEqualTo(1024));
        }

        // A torus's small faces are all border and their strips meet on one line: no NaN points where it crosses itself
        [TestCase(0.31f, 0.29f, 0.13f)]
        [TestCase(0.2f, 0.18f, 0.2f)]
        [TestCase(0.123f, 0.117f, 0.071f)]
        [TestCase(0.4f, 0.4f, 0.2f)]
        public void ASmallQuadThatIsAllBorder_IsMappedWithoutNaN(float foot, float top, float height)
        {
            var inset = (foot - top) * 0.5f;
            using var quad = new Surface(32, new float2(0, 0), new float2(foot, 0), new float2(foot - inset, height),
                                             new float2(0, 0), new float2(foot - inset, height), new float2(inset, height));
            Assert.That(quad.sections.BuildConforming(quad.input, quad.indices, quad.positions), Is.True);
            using var render = Map(quad);
            for (int i = 0; i < render.Length; i++)
            {
                Assert.That(math.all(math.isfinite(render[i].position)), Is.True, $"vertex {i}: {render[i].position}");
                Assert.That(math.all(math.isfinite(render[i].uv0)), Is.True, $"vertex {i}'s uv: {render[i].uv0}");
            }
        }

        [Test]
        public void AWallThinnerThanTwoBorders_IsAllBorder()
        {
            // 32 pixels is an eighth of a unit: the strips along the top and bottom meet 0.1 in, 256 pixels a unit across
            using var wall = new Surface(32, Rectangle(4, 0.2f));
            Assert.That(wall.sections.BuildConforming(wall.input, wall.indices, wall.positions), Is.True);
            AreEqual(new float2(1024 + 12.8f, 1536 + 12.8f), wall.Pixels(0.05, 0.05), "inside the corner, unstretched");
            AreEqual(new float2(1536, 1536 + 12.8f),          wall.Pixels(2, 0.05),    "the bottom strip");
            AreEqual(new float2(1536, 2048 - 12.8f),          wall.Pixels(2, 0.15),    "the top strip");
            AssertAllBorder(wall, p => true, 4, 0.2);

            // not a rectangle, and too thin to conform: all border as well
            using var sliver = new Surface(32, new float2(0, 0), new float2(4, 0), new float2(3.9f, 0.2f), new float2(0, 0), new float2(3.9f, 0.2f), new float2(0.1f, 0.2f));
            Assert.That(sliver.sections.BuildConforming(sliver.input, sliver.indices, sliver.positions), Is.True);
            AssertAllBorder(sliver, p => p.x > p.y * 0.5 && p.x < 4 - p.y * 0.5, 4, 0.2);
        }

        // Every uv inside is a number and lies within the border's depth of B1's edges
        static void AssertAllBorder(Surface surface, System.Func<double2, bool> inside, double width, double height)
        {
            for (double x = 0.0031; x < width; x += width / 97)
            {
                for (double y = 0.0017; y < height; y += height / 31)
                {
                    if (!inside(new double2(x, y)))
                        continue;
                    var pixel = surface.Pixels(x, y);
                    Assert.That(math.all(math.isfinite(pixel)), Is.True, $"at ({x}, {y}): {pixel}");
                    var depth = math.cmin(math.min(pixel - new float2(1024, 1536), new float2(2048, 2048) - pixel));
                    Assert.That(depth, Is.LessThanOrEqualTo(32 + 1e-3f), $"({x}, {y}) is in the tile's interior: {pixel} px");
                }
            }
        }

        static void AreEqual(float2 expected, float2 actual, string what)
        {
            Assert.That(math.distance(expected, actual), Is.LessThan(1e-3f), $"{what}: expected {expected} px, got {actual} px");
        }

        [Test]
        public void TheOutline_IsTheLoopsOfWhatTheSurfaceDraws()
        {
            using var door = new Surface(Doorway());
            using var corners = new NativeList<double2>(Allocator.Temp);
            using var loops = new NativeList<int2>(Allocator.Temp);
            HotspotSections.OutlineOf(door.indices, door.positions, new float3(1, 0, 0), new float3(0, 1, 0), corners, loops);
            Assert.That(loops.Length, Is.EqualTo(1));
            Assert.That(loops[0].y, Is.EqualTo(8), "the doorway's U has 8 corners");

            using var window = new Surface(Window());
            HotspotSections.OutlineOf(window.indices, window.positions, new float3(1, 0, 0), new float3(0, 1, 0), corners, loops);
            Assert.That(loops.Length, Is.EqualTo(2), "the wall and its hole");
            Assert.That(loops[0].y + loops[1].y, Is.EqualTo(8));
        }

        // A 4 x 2 wall whose bottom right corner the CSG left cut off by a tenth of a millimetre (as where a capsule's side
        // meets its cap)
        static float2[] WallWithATinyChamfer() => Fan(new float2(0, 0), new float2(4 - 1e-4f, 0), new float2(4, 1e-4f), new float2(4, 2), new float2(0, 2));

        [Test]
        public void ACornerCutOffByLessThanAPixel_IsNoCornerOfTheOutline()
        {
            using var wall = new Surface(WallWithATinyChamfer());
            using var corners = new NativeList<double2>(Allocator.Temp);
            using var loops   = new NativeList<int2>(Allocator.Temp);
            HotspotSections.OutlineOf(wall.indices, wall.positions, new float3(1, 0, 0), new float3(0, 1, 0), default, corners, loops, default, 0.25 / 256);
            Assert.That(loops.Length == 1 && loops[0].y == 4, Is.True, $"four corners: {loops.Length} loops, {(loops.Length > 0 ? loops[0].y : 0)} corners");
        }

        [Test]
        public void AWallThinnerThanTwoBorders_WithItsTopAFloatStepOff_HasNoSliverWhereItsStripsMeet()
        {
            // the strips along the top and bottom meet in the middle, their lines a float step apart (as on a capsule)
            using var wall = new Surface(32, new float2(0, 0), new float2(4, 0), new float2(4, 0.2000001f), new float2(0, 0), new float2(4, 0.2000001f), new float2(0, 0.2f));
            Assert.That(wall.sections.BuildConforming(wall.input, wall.indices, wall.positions), Is.True);
            using var render = Map(wall);
            for (int t = 0; t < wall.indices.Length; t += 3)
            {
                var a = wall.positions[wall.indices[t]]; var b = wall.positions[wall.indices[t + 1]]; var c = wall.positions[wall.indices[t + 2]];
                var longest = math.max(math.distance(a, b), math.max(math.distance(b, c), math.distance(c, a)));
                var height = math.length(math.cross(b - a, c - a)) / longest;
                Assert.That(height, Is.GreaterThan(1e-5f), $"triangle {t / 3}: {a} {b} {c}");
            }
        }

        static HotspotSectionInput SideInput(int side, HotspotBorderMode mode, float maxStretch = 0, HotspotTileFlags flags = HotspotTileFlags.None)
        {
            var input = new HotspotSectionInput { flags = flags };
            input.sideMode[side] = (int)mode;
            input.sideStretch[side] = maxStretch;
            return input;
        }

        [Test]
        public void AStripAlongASideThatRepeats_TakesWholeRepeatsStretchedToFit()
        {
            Assert.That(HotspotSections.StripRepeats(SideInput(1, HotspotBorderMode.Repeat), 1, 9, 4, out var step), Is.EqualTo(2));
            Assert.That(step, Is.EqualTo(4.5).Within(1e-9));
            // the other sides as the tile's flags say: stretched
            Assert.That(HotspotSections.StripRepeats(SideInput(1, HotspotBorderMode.Repeat), 0, 9, 4, out step), Is.EqualTo(1));
            Assert.That(step, Is.EqualTo(9).Within(1e-9));
        }

        [Test]
        public void AStripAlongASideThatRepeatsPartially_KeepsItsSize_AndTheLastIsCutOff()
        {
            Assert.That(HotspotSections.StripRepeats(SideInput(2, HotspotBorderMode.RepeatPartial), 2, 9, 4, out var step), Is.EqualTo(3));
            Assert.That(step, Is.EqualTo(4).Within(1e-9));
        }

        [Test]
        public void AStripAlongASideThatStretches_RepeatsTheFewestTimes_WhenItWouldStretchTooFar()
        {
            // 9 over a strip of 4 is 2.25 times: within 3 it stretches, beyond 1.5 it takes 2 repeats of 4.5
            Assert.That(HotspotSections.StripRepeats(SideInput(0, HotspotBorderMode.Stretch, 3), 0, 9, 4, out var step), Is.EqualTo(1));
            Assert.That(step, Is.EqualTo(9).Within(1e-9));
            Assert.That(HotspotSections.StripRepeats(SideInput(0, HotspotBorderMode.Stretch, 1.5f), 0, 9, 4, out step), Is.EqualTo(2));
            Assert.That(step, Is.EqualTo(4.5).Within(1e-9));
            // a side that follows the tile's flags: they make it repeat
            Assert.That(HotspotSections.StripRepeats(SideInput(0, HotspotBorderMode.Tile, 0, HotspotTileFlags.RepeatBorder), 0, 9, 4, out step), Is.EqualTo(2));
        }

        [Test]
        public void AStripThatWouldRepeatMoreThanItsBudget_Stretches()
        {
            var input = SideInput(0, HotspotBorderMode.Repeat);
            Assert.That(HotspotSections.StripRepeats(input, 0, 9, 2, out _), Is.EqualTo(4), "within the hard limit: 4 repeats");
            input.maxRepeats = 3;
            Assert.That(HotspotSections.StripRepeats(input, 0, 9, 2, out var step), Is.EqualTo(1), "over a budget of 3: stretched");
            Assert.That(step, Is.EqualTo(9).Within(1e-9));
        }

        [Test]
        public void ThePadding_KeepsTheInteriorAndItsRepeatsOffTheBorder()
        {
            // a padding of 2: the tile is 1026..2046, its border 30 deep (30 / 256 units on the surface) keeps its inner
            // edge at 1056..2016, and the interior samples 1058..2014, 956 pixels: 3.734375 units a repeat
            const double band = 30.0 / 256;
            using var wall = new Surface(32, 2, Rectangle(8, 2));
            Assert.That(wall.sections.BuildConforming(wall.input, wall.indices, wall.positions), Is.True);
            Assert.That(wall.Pixels(band + 1e-6, 1).x, Is.EqualTo(1058).Within(1e-2f), "the interior starts the padding in");
            Assert.That(wall.Pixels(band - 1e-6, 1).x, Is.EqualTo(1056).Within(1e-2f), "the strip still ends at the border's inner edge");
            using var repeats = HotspotSections.Create(Allocator.Temp);
            Assert.That(repeats.BuildInteriorRepeats(wall.input), Is.True);
            Assert.That((repeats.UV(new double2(band + 1e-6, 1)) * 2048).x, Is.EqualTo(1058).Within(1e-2f), "a repeat starts the padding in");
            Assert.That((repeats.UV(new double2(band + 3.734375 - 1e-6, 1)) * 2048).x, Is.EqualTo(2014).Within(1e-2f), "and ends the padding short");
            Assert.That((repeats.UV(new double2(band + 3.734375 + 1e-6, 1)) * 2048).x, Is.EqualTo(1058).Within(1e-2f), "where the next starts again");
            for (double y = 0.13; y < 1.87; y += 0.05)
            {
                var v = (repeats.UV(new double2(4, y)) * 2048).y;
                Assert.That(2048 - v, Is.InRange(34 - 1e-2, 478 + 1e-2), "never in the border's rows");
            }
        }

        [Test]
        public void TheInteriorPadding_IsItsOwn_AndNoneByDefault()
        {
            // no padding along the tile's edges, 2 pixels where the interior meets the border: 1058..2014
            using (var inner = new Surface(32, 0, 2, Rectangle(8, 2)))
            {
                using var repeats = HotspotSections.Create(Allocator.Temp);
                Assert.That(repeats.BuildInteriorRepeats(inner.input), Is.True);
                Assert.That((repeats.UV(new double2(0.125 + 1e-6, 1)) * 2048).x, Is.EqualTo(1058).Within(1e-2f), "the interior padded");
            }
            // 2 pixels along the tile's edges only: the interior reaches the border's inner edge, 1056
            using (var outer = new Surface(32, 2, 0, Rectangle(8, 2)))
            {
                using var repeats = HotspotSections.Create(Allocator.Temp);
                Assert.That(repeats.BuildInteriorRepeats(outer.input), Is.True);
                Assert.That((repeats.UV(new double2(30.0 / 256 + 1e-6, 1)) * 2048).x, Is.EqualTo(1056).Within(1e-2f), "the interior not padded");
            }
            var fresh = ScriptableObject.CreateInstance<ChiselHotspotLayout>();
            Assert.That(fresh.interiorPadding, Is.Zero, "none by default");
            Object.DestroyImmediate(fresh);
        }

        [Test]
        public void AnInteriorThatWouldRepeatMoreThanItsBudget_IsNotRepeated()
        {
            using var wall = new Surface(Rectangle(8, 2));
            using var repeats = HotspotSections.Create(Allocator.Temp);
            // B1's inside is 3.75 units a repeat: an 8 x 2 wall takes 3 of them
            Assert.That(repeats.BuildInteriorRepeats(wall.input), Is.True);
            Assert.That(repeats.cells.Length, Is.EqualTo(3));
            var input = wall.input;
            input.maxRepeats = 2;
            Assert.That(repeats.BuildInteriorRepeats(input), Is.False, "over a budget of 2");
        }

        [Test]
        public void ATilesSideRules_FollowTheTile_WhereverTheSectionsLayItsSides()
        {
            var layout = ScriptableObject.CreateInstance<ChiselHotspotLayout>();
            layout.size = new Vector2Int(2048, 2048);
            layout.pixelsPerUnit = 256;
            // points from the top left, clockwise as the texture shows them: side 0 is the top, side 1 the right
            var sides = new HotspotBorderSide[4];
            sides[0].mode = HotspotBorderMode.Repeat;
            sides[1].mode = HotspotBorderMode.RepeatPartial;
            layout.tiles = new List<HotspotTile> { new() { id = 1, name = "B1", border = 32, sides = sides,
                points = new[] { new Vector2Int(1024, 0), new Vector2Int(2048, 0), new Vector2Int(2048, 512), new Vector2Int(1024, 512) } } };
            using var tiles = new NativeList<HotspotTileData>(Allocator.Temp);
            HotspotMatcher.AddTiles(layout, tiles, out var data);
            using var positions = new NativeArray<float3>(new[] { new float3(0, 0, 0), new float3(4, 0, 0), new float3(4, 2, 0), new float3(0, 2, 0) }, Allocator.Temp);
            var shape  = HotspotMatcher.ShapeOf(positions, new float3(0, 0, -1));
            var choice = HotspotMatcher.Choose(tiles.AsArray(), data, shape, 0, 0);
            var input  = HotspotSections.Input(tiles[0], data, shape, choice, new float3(0, 0, -1));
            // as the sections lay a tile that is not turned: bottom, right, top, left
            Assert.That((HotspotBorderMode)input.sideMode[2], Is.EqualTo(HotspotBorderMode.Repeat), "the top");
            Assert.That((HotspotBorderMode)input.sideMode[1], Is.EqualTo(HotspotBorderMode.RepeatPartial), "the right");
            Assert.That((HotspotBorderMode)input.sideMode[0], Is.EqualTo(HotspotBorderMode.Tile), "the bottom");
            Assert.That((HotspotBorderMode)input.sideMode[3], Is.EqualTo(HotspotBorderMode.Tile), "the left");
            // turned a quarter, the tile's top lies along another of the sections' sides
            choice.rotated = true;
            var turned = HotspotSections.Input(tiles[0], data, shape, choice, new float3(0, 0, -1));
            var repeating = 0;
            for (int j = 0; j < 4; j++)
                repeating += (HotspotBorderMode)turned.sideMode[j] == HotspotBorderMode.Repeat ? 1 : 0;
            Assert.That(repeating, Is.EqualTo(1), "one side still repeats");
            Assert.That((HotspotBorderMode)turned.sideMode[2], Is.Not.EqualTo(HotspotBorderMode.Repeat), "but not the one at the top of the sections");
            Object.DestroyImmediate(layout);
        }

        [Test]
        public void ABorderedWall_WithItsTopSideRepeating_HasTheTopStripInRepeats()
        {
            using var plain = new Surface(Rectangle(8, 2));
            Assert.That(plain.sections.BuildConforming(plain.input, plain.indices, plain.positions), Is.True);
            using var wall = new Surface(Rectangle(8, 2));
            var input = wall.input;
            input.sideMode[2] = (int)HotspotBorderMode.Repeat;   // the top, as the sections lay it
            Assert.That(wall.sections.BuildConforming(input, wall.indices, wall.positions), Is.True);
            Assert.That(wall.sections.cells.Length, Is.GreaterThan(plain.sections.cells.Length), "more sections: the top strip's repeats");
            // the bottom strip still stretches: one section
            Assert.That(wall.Pixels(4, 0.0625).y, Is.EqualTo(plain.Pixels(4, 0.0625).y).Within(1e-3f));
        }

        [Test]
        public void AHexagonOnAShapedTile_HasItsBorderMeasuredFromTheTilesOwnSides()
        {
            var layout = ScriptableObject.CreateInstance<ChiselHotspotLayout>();
            layout.size = new Vector2Int(2048, 2048);
            layout.pixelsPerUnit = 256;
            // the hexagon tile at (1024, 0) from the top left: 512 x 444, a 32 pixel border
            layout.tiles = new List<HotspotTile> { new() { id = 1, name = "Hex", border = 32, points = new[] {
                new Vector2Int(1152, 0), new Vector2Int(1408, 0), new Vector2Int(1536, 222),
                new Vector2Int(1408, 444), new Vector2Int(1152, 444), new Vector2Int(1024, 222) } } };
            using var tiles = new NativeList<HotspotTileData>(Allocator.Temp);
            using var tileSides = new NativeList<float4>(Allocator.Temp);
            HotspotMatcher.AddTiles(layout, tiles, out var data, tileSides);
            Assert.That(tileSides.Length, Is.EqualTo(6));
            using var hexagon = new Surface(32, Fan(new float2(0.5f, 0), new float2(1.5f, 0), new float2(2, 0.867f), new float2(1.5f, 1.734f), new float2(0.5f, 1.734f), new float2(0, 0.867f)));
            var shape = HotspotMatcher.ShapeOf(hexagon.positions.AsArray(), new float3(0, 0, -1));
            HotspotMatcher.MaskOf(ref shape, hexagon.positions.AsArray(), hexagon.indices.AsArray(), new float3(1, 0, 0), new float3(0, 1, 0));
            var choice = HotspotMatcher.Choose(tiles.AsArray(), data, shape, 0, 0);
            Assert.That(choice.tile, Is.EqualTo(0));
            var input = HotspotSections.Input(tiles[0], data, shape, choice, new float3(0, 0, -1));
            Assert.That(input.sideCount, Is.EqualTo(6));
            Assert.That(hexagon.sections.BuildConforming(input, hexagon.indices, hexagon.positions, default, tileSides.AsArray()), Is.True);
            // 16 pixels in from the middle of the lower right edge: 16 pixels from the tile's lower right side
            var a = new double2(1.5, 0); var b = new double2(2, 0.867);
            var d = math.normalize(b - a); var n = new double2(-d.y, d.x);
            var p = (a + b) * 0.5 + n * 0.0625;
            var pixel = (double2)hexagon.sections.UV(p) * 2048;
            // the tile's lower right side, y up: from (1408, 2048 - 444) to (1536, 2048 - 222)
            var ta = new double2(1408, 2048 - 444); var tb = new double2(1536, 2048 - 222);
            var td = math.normalize(tb - ta); var tn = new double2(-td.y, td.x);
            Assert.That(math.dot(pixel - ta, tn), Is.EqualTo(16).Within(1e-2), $"{pixel} px: 16 in from the tile's own side");
            var along = math.dot(pixel - ta, td) / math.length(tb - ta);
            Assert.That(along, Is.InRange(0.0, 1.0), "along that side");
            Object.DestroyImmediate(layout);
        }

        [Test]
        public void ARectangle_HasTheBorderItsBoundsHave()
        {
            using var wall = new Surface(Rectangle(8, 2));
            using var bounds = HotspotSections.Create(Allocator.Temp);
            Assert.That(bounds.Build(wall.input), Is.True);
            Assert.That(wall.sections.BuildConforming(wall.input, wall.indices, wall.positions), Is.True);
            for (double x = 0.013; x < 8; x += 0.171)
            {
                for (double y = 0.007; y < 2; y += 0.0613)
                {
                    var p = new double2(x, y);
                    Assert.That(math.distance(bounds.UV(p), wall.sections.UV(p)), Is.LessThan(1e-6f), $"at {p}");
                }
            }
        }

        [Test]
        public void ADoorway_HasABorderAlongItsEdges()
        {
            using var door = new Surface(Doorway());
            Assert.That(door.sections.BuildConforming(door.input, door.indices, door.positions), Is.True);
            // B1 is 1024..2048 x 1536..2048 with y up; the wall's outside sides as before
            AreEqual(new float2(1024 + 16, door.Pixels(0.0625, 1.5).y), door.Pixels(0.0625, 1.5), "the wall's left side");
            // the doorway's left edge faces right: the tile's right side, 16 pixels in
            AreEqual(new float2(2048 - 16, door.Pixels(1, 1).y), door.Pixels(2.4 - 0.0625, 1), "the doorway's left edge");
            // its right edge faces left
            AreEqual(new float2(1024 + 16, door.Pixels(5, 1).y), door.Pixels(3.6 + 0.0625, 1), "the doorway's right edge");
            // its top faces down
            AreEqual(new float2(door.Pixels(3, 2.8).x, 1536 + 16), door.Pixels(3, 2.3 + 0.0625), "the doorway's top");
            // where the doorway meets the floor: the tile's bottom right corner
            AreEqual(new float2(2048 - 16, 1536 + 16), door.Pixels(2.4 - 0.0625, 0.0625), "the corner beside the doorway");
            // away from every edge: the interior, as the bounds map it
            using var bounds = HotspotSections.Create(Allocator.Temp);
            bounds.Build(door.input);
            AreEqual(bounds.UV(new double2(1, 1.5)) * 2048, door.Pixels(1, 1.5), "the interior");
        }

        [Test]
        public void AWindow_HasABorderAroundItsHole()
        {
            using var window = new Surface(Window());
            Assert.That(window.sections.BuildConforming(window.input, window.indices, window.positions), Is.True);
            AreEqual(new float2(1024 + 16, window.Pixels(5, 1.5).y), window.Pixels(3.75 + 0.0625, 1.5), "right of the window: the tile's left side");
            AreEqual(new float2(2048 - 16, window.Pixels(1, 1.5).y), window.Pixels(2.25 - 0.0625, 1.5), "left of it: its right side");
            AreEqual(new float2(window.Pixels(3, 0.5).x, 2048 - 16), window.Pixels(3, 1 - 0.0625), "below it: its top");
            AreEqual(new float2(window.Pixels(3, 2.5).x, 1536 + 16), window.Pixels(3, 2 + 0.0625), "above it: its bottom");
        }

        [Test]
        public void ADoorway_IsCutSoEachTriangleLiesInOneSection()
        {
            using var door = new Surface(Doorway());
            Assert.That(door.sections.BuildConforming(door.input, door.indices, door.positions), Is.True);
            using var select = new NativeList<SelectVertex>(Allocator.Temp);
            using var render = new NativeList<RenderVertex>(Allocator.Temp);
            foreach (var p in door.positions)
            {
                select.Add(new SelectVertex { position = p });
                render.Add(new RenderVertex { position = p, normal = new float3(0, 0, -1) });
            }
            var right = new float3(1, 0, 0); var up = new float3(0, 1, 0);
            door.sections.Split(right, up, new float3(0, 0, -1), door.indices, door.positions, select, render);
            door.sections.AssignUVs(door.indices, door.positions, select, render, right, up);
            var area = 0.0;
            for (int t = 0; t < door.indices.Length; t += 3)
            {
                var a = (double3)door.positions[door.indices[t]]; var b = (double3)door.positions[door.indices[t + 1]]; var c = (double3)door.positions[door.indices[t + 2]];
                area += ((b.x - a.x) * (c.y - a.y) - (b.y - a.y) * (c.x - a.x)) * 0.5;
                var cell = door.sections.Locate(((a + b + c) / 3).xy);
                foreach (var i in new[] { door.indices[t], door.indices[t + 1], door.indices[t + 2] })
                {
                    var expected = door.sections.UV(cell, ((double3)render[i].position).xy);
                    Assert.That(math.distance(render[i].uv0, expected), Is.LessThan(1e-5f), $"triangle {t / 3} is not mapped by its section");
                }
            }
            Assert.That(area, Is.EqualTo(6 * 3 - 1.2 * 2.3).Within(1e-4), "the pieces cover the wall once");
        }

        // Cut once (CutOnce) and by Split, on two copies: triangles of each, uvs assigned
        static int CutBoth(System.Func<Surface> make, out Surface once)
        {
            using var split = make();
            Assert.That(split.sections.BuildConforming(split.input, split.indices, split.positions), Is.True);
            var right = new float3(1, 0, 0); var up = new float3(0, 1, 0);
            using (var select = new NativeList<SelectVertex>(Allocator.Temp))
            using (var render = new NativeList<RenderVertex>(Allocator.Temp))
            {
                foreach (var p in split.positions) { select.Add(new SelectVertex { position = p }); render.Add(new RenderVertex { position = p, normal = new float3(0, 0, -1) }); }
                split.sections.Split(right, up, new float3(0, 0, -1), split.indices, split.positions, select, render);
            }
            once = make();
            Assert.That(once.sections.BuildConforming(once.input, once.indices, once.positions), Is.True);
            using (var select = new NativeList<SelectVertex>(Allocator.Temp))
            using (var render = new NativeList<RenderVertex>(Allocator.Temp))
            {
                foreach (var p in once.positions) { select.Add(new SelectVertex { position = p }); render.Add(new RenderVertex { position = p, normal = new float3(0, 0, -1) }); }
                Assert.That(once.sections.CutOnce(right, up, new float3(0, 0, -1), once.indices, once.positions, select, render), Is.True, "not cut once");
            }
            return split.indices.Length / 3;
        }

        // every triangle lies in one section: its corners are all in the section its middle is in
        static void AssertEachTriangleInOneSection(Surface surface)
        {
            for (int t = 0; t < surface.indices.Length; t += 3)
            {
                var a = (double3)surface.positions[surface.indices[t]]; var b = (double3)surface.positions[surface.indices[t + 1]]; var c = (double3)surface.positions[surface.indices[t + 2]];
                var cell = surface.sections.cells[surface.sections.Locate(((a + b + c) / 3).xy)];
                for (int h = cell.halfPlaneStart; h < cell.halfPlaneStart + cell.halfPlaneCount; h++)
                {
                    var line = surface.sections.lines[surface.sections.halfPlanes[h].line]; var side = surface.sections.halfPlanes[h].side;
                    foreach (var p in new[] { a.xy, b.xy, c.xy })
                        Assert.That(side * line.Value(p), Is.GreaterThanOrEqualTo(-1e-6), $"triangle {t / 3} lies across sections");
                }
            }
        }

        [Test]
        public void ADoorway_IsCutOnce_WithFewerTrianglesThanTriangleByTriangle()
        {
            var bySplit = CutBoth(() => new Surface(Doorway()), out var door);
            using (door)
            {
                Assert.That(door.indices.Length / 3, Is.LessThan(bySplit), "no fewer triangles than cutting triangle by triangle");
                HotspotBorderTests.AssertWatertight(door.indices, door.positions, 6 * 3 - 1.2 * 2.3,
                    OnOutline(new float2(0, 0), new float2(2.4f, 0), new float2(2.4f, 2.3f), new float2(3.6f, 2.3f), new float2(3.6f, 0), new float2(6, 0), new float2(6, 3), new float2(0, 3)));
                AssertEachTriangleInOneSection(door);
            }
        }

        [Test]
        public void AWindow_IsCutOnce_WithFewerTrianglesThanTriangleByTriangle()
        {
            var bySplit = CutBoth(() => new Surface(Window()), out var window);
            using (window)
            {
                Assert.That(window.indices.Length / 3, Is.LessThan(bySplit), "no fewer triangles than cutting triangle by triangle");
                var outer = OnOutline(new float2(0, 0), new float2(6, 0), new float2(6, 3), new float2(0, 3));
                var hole  = OnOutline(new float2(2.25f, 1), new float2(3.75f, 1), new float2(3.75f, 2), new float2(2.25f, 2));
                HotspotBorderTests.AssertWatertight(window.indices, window.positions, 6 * 3 - 1.5, p => outer(p) || hole(p));
                AssertEachTriangleInOneSection(window);
            }
        }
    }
}
