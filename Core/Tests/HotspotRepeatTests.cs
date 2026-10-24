using System.Collections.Generic;
using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace Chisel.Core.Tests
{
    [TestFixture]
    public class HotspotRepeatTests
    {
        // A 2048 sheet at 256 pixels a unit with the given tiles
        sealed class Sheet : System.IDisposable
        {
            public ChiselHotspotLayout layout;
            public NativeList<HotspotTileData> tiles = new(Allocator.Temp);
            public HotspotLayoutData data;
            public HotspotSections sections = HotspotSections.Create(Allocator.Temp);

            public Sheet(params HotspotTile[] tiles)
            {
                layout = ScriptableObject.CreateInstance<ChiselHotspotLayout>();
                layout.size = new Vector2Int(2048, 2048);
                layout.pixelsPerUnit = 256;
                layout.tiles = new List<HotspotTile>(tiles);
                HotspotMatcher.AddTiles(layout, this.tiles, out data);
            }

            public HotspotChoice Choose(in HotspotShape shape) => HotspotMatcher.Choose(tiles.AsArray(), data, shape, 0, 0);

            public void Dispose()
            {
                sections.Dispose();
                tiles.Dispose();
                Object.DestroyImmediate(layout);
            }
        }

        static HotspotTile Rect(int id, int x, int y, int w, int h, HotspotTileFlags flags = HotspotTileFlags.None, int border = 0)
        {
            return new HotspotTile { id = id, name = "tile " + id, flags = flags, border = border,
                                     points = new[] { new Vector2Int(x, y), new Vector2Int(x + w, y), new Vector2Int(x + w, y + h), new Vector2Int(x, y + h) } };
        }

        // 2048 x 128 pixels at the bottom of the sheet: 8 x 0.5 units, repeating across
        static HotspotTile Strip(HotspotTileFlags flags = HotspotTileFlags.RepeatU) => Rect(1, 0, 1920, 2048, 128, flags);

        static HotspotShape Wall(float width, float height)
        {
            using var positions = new NativeArray<float3>(new[] { new float3(0, 0, 0), new float3(width, 0, 0), new float3(width, height, 0), new float3(0, height, 0) }, Allocator.Temp);
            return HotspotMatcher.ShapeOf(positions, new float3(0, 0, -1));
        }

        [TestCase(40, 5,  TestName = "A40UnitStrip_Repeats5Times")]
        [TestCase(41, 5,  TestName = "A41UnitStrip_Repeats5Times_StretchedALittle")]
        [TestCase(2,  1,  TestName = "A2UnitStrip_TakesItOnce")]
        public void AStrip_RepeatsTheTile_AsManyTimesAsFitAtItsHeight(float length, int repeats)
        {
            using var sheet = new Sheet(Strip());
            var choice = sheet.Choose(Wall(length, 0.5f));
            Assert.That(choice.tile, Is.EqualTo(0));
            Assert.That(math.max(1, choice.repeatX), Is.EqualTo(repeats));
            Assert.That(math.max(1, choice.repeatY), Is.EqualTo(1));
        }

        [Test]
        public void ATileThatRepeats_FitsBetter_ThanOneThatStretches()
        {
            using var sheet = new Sheet(Strip());
            using var stretched = new Sheet(Strip(HotspotTileFlags.None));
            Assert.That(sheet.Choose(Wall(40, 0.5f)).score, Is.LessThan(1e-4f));
            Assert.That(stretched.Choose(Wall(40, 0.5f)).score, Is.GreaterThan(1));
        }

        [Test]
        public void ATileThatRepeatsBothWays_RepeatsAtItsDensity()
        {
            using var sheet = new Sheet(Rect(1, 0, 0, 256, 256, HotspotTileFlags.RepeatU | HotspotTileFlags.RepeatV));
            var choice = sheet.Choose(Wall(3, 2));
            Assert.That(new int2(choice.repeatX, choice.repeatY), Is.EqualTo(new int2(3, 2)));
        }

        [Test]
        public void EachRepeat_IsTheWholeTile_AndTheSeamsHaveAVertexForEachSide()
        {
            using var sheet = new Sheet(Strip());
            var shape = Wall(40, 0.5f);
            var choice = sheet.Choose(shape);
            Assert.That(sheet.sections.Build(sheet.tiles[0], sheet.data, shape, choice), Is.True);
            Assert.That(sheet.sections.cells.Length, Is.EqualTo(5));
            // u across the strip's 2048 pixels, v 0 .. 128 / 2048 at the bottom of the sheet
            Assert.That(math.distance(sheet.sections.UV(new double2(4, 0.25)), new float2(0.5f, 0.03125f)), Is.LessThan(1e-6f));
            Assert.That(math.distance(sheet.sections.UV(new double2(12, 0.25)), new float2(0.5f, 0.03125f)), Is.LessThan(1e-6f), "the second repeat");
            Assert.That(sheet.sections.UV(new double2(8 - 1e-6, 0)).x, Is.EqualTo(1).Within(1e-5f), "the end of the first");
            Assert.That(sheet.sections.UV(new double2(8 + 1e-6, 0)).x, Is.EqualTo(0).Within(1e-5f), "the start of the second");

            using var indices  = new NativeList<int>(Allocator.Temp) { 0, 1, 2, 0, 2, 3 };
            using var collider = new NativeList<float3>(Allocator.Temp) { new float3(0, 0, 0), new float3(40, 0, 0), new float3(40, 0.5f, 0), new float3(0, 0.5f, 0) };
            using var select   = new NativeList<SelectVertex>(Allocator.Temp);
            using var render   = new NativeList<RenderVertex>(Allocator.Temp);
            foreach (var p in collider)
            {
                select.Add(new SelectVertex { position = p });
                render.Add(new RenderVertex { position = p, normal = new float3(0, 0, -1) });
            }
            var right = new float3(1, 0, 0); var up = new float3(0, 1, 0);
            sheet.sections.Split(right, up, new float3(0, 0, -1), indices, collider, select, render);
            sheet.sections.AssignUVs(indices, collider, select, render, right, up);
            Assert.That(render.Length, Is.EqualTo(collider.Length));
            Assert.That(select.Length, Is.EqualTo(collider.Length));
            for (int t = 0; t < indices.Length; t += 3)
            {
                var a = render[indices[t]].uv0; var b = render[indices[t + 1]].uv0; var c = render[indices[t + 2]].uv0;
                var minU = math.min(a.x, math.min(b.x, c.x)); var maxU = math.max(a.x, math.max(b.x, c.x));
                Assert.That(minU, Is.GreaterThanOrEqualTo(-1e-5f), $"triangle {t / 3} reaches out of the tile");
                Assert.That(maxU, Is.LessThanOrEqualTo(1 + 1e-5f), $"triangle {t / 3} reaches out of the tile");
                // the tile's u for each corner, from where it is in its repeat
                foreach (var i in new[] { indices[t], indices[t + 1], indices[t + 2] })
                {
                    var x = render[i].position.x;
                    var within = x / 8 - math.floor(x / 8);
                    var u = render[i].uv0.x;
                    Assert.That(math.abs(u - within) < 1e-4f || (within < 1e-4f && math.abs(u - 1) < 1e-4f), $"a vertex at x {x} has u {u}");
                }
            }
            var atSeam = 0;
            for (int i = 0; i < render.Length; i++)
                atSeam += math.abs(render[i].position.x - 8) < 1e-5f && render[i].position.y == 0 ? 1 : 0;
            Assert.That(atSeam, Is.EqualTo(2), "the seam's bottom corner, once for each side");
        }

        static HotspotSectionInput Moved(Sheet sheet, in HotspotShape shape, float2 offset)
        {
            var choice = sheet.Choose(shape);
            return HotspotSections.Input(sheet.tiles[choice.tile], sheet.data, shape, choice, new float3(1, 0, 0), new float3(0, 1, 0), 1, offset);
        }

        [Test]
        public void RepeatsMovedAlongHalfARepeat_StartHalfWayAlongTheTile()
        {
            using var sheet = new Sheet(Strip());
            Assert.That(sheet.sections.Build(Moved(sheet, Wall(40, 0.5f), new float2(0.5f, 0))), Is.True);
            // five 8 unit repeats moved along by 4: half of one, four whole ones, half of one
            Assert.That(sheet.sections.cells.Length, Is.EqualTo(6));
            Assert.That(sheet.sections.UV(new double2(0, 0.25)).x, Is.EqualTo(0.5f).Within(1e-6f), "the start");
            Assert.That(sheet.sections.UV(new double2(2, 0.25)).x, Is.EqualTo(0.75f).Within(1e-6f));
            Assert.That(sheet.sections.UV(new double2(4 - 1e-6, 0)).x, Is.EqualTo(1).Within(1e-5f), "the end of the first");
            Assert.That(sheet.sections.UV(new double2(4 + 1e-6, 0)).x, Is.EqualTo(0).Within(1e-5f), "the start of the second");
            Assert.That(sheet.sections.UV(new double2(40, 0.25)).x, Is.EqualTo(0.5f).Within(1e-6f), "the end");
            // whole repeats moved along are where they were
            Assert.That(sheet.sections.Build(Moved(sheet, Wall(40, 0.5f), new float2(3, 0))), Is.True);
            Assert.That(sheet.sections.cells.Length, Is.EqualTo(5));
            Assert.That(sheet.sections.UV(new double2(2, 0.25)).x, Is.EqualTo(0.25f).Within(1e-6f));
        }

        [Test]
        public void ASurfaceOneRepeatLong_MovedAlong_IsCutInTwo_ButNotAlongTheAxisThatDoesNotRepeat()
        {
            using var sheet = new Sheet(Strip());
            var input = Moved(sheet, Wall(8, 0.5f), new float2(0.25f, 0.5f));
            Assert.That(input.offset, Is.EqualTo(new float2(0.25f, 0)), "the strip does not repeat up");
            Assert.That(sheet.sections.Build(input), Is.True);
            Assert.That(sheet.sections.cells.Length, Is.EqualTo(2));
            Assert.That(sheet.sections.UV(new double2(0, 0.25)).x, Is.EqualTo(0.25f).Within(1e-6f));
            Assert.That(sheet.sections.UV(new double2(6 + 1e-6, 0.25)).x, Is.EqualTo(0).Within(1e-5f), "the seam, 6 along");
            Assert.That(sheet.sections.UV(new double2(8, 0.25)).x, Is.EqualTo(0.25f).Within(1e-6f), "the end");
        }

        [Test]
        public void PartialRepeats_KeepTheirSize_AndTheLastIsCutOff()
        {
            using var sheet = new Sheet(Strip(HotspotTileFlags.RepeatU | HotspotTileFlags.PartialRepeats));
            var shape = Wall(20, 0.5f);
            var choice = sheet.Choose(shape);
            // 8 units a repeat: 8, 8 and half of the third
            Assert.That(choice.repeatX, Is.EqualTo(3));
            Assert.That(choice.lastRepeat.x, Is.EqualTo(0.5f).Within(1e-6f));
            Assert.That(choice.score, Is.LessThan(1e-4f), "nothing is stretched");
            Assert.That(sheet.sections.Build(sheet.tiles[0], sheet.data, shape, choice), Is.True);
            Assert.That(sheet.sections.UV(new double2(17, 0.25)).x, Is.EqualTo(0.125f).Within(1e-6f), "a unit into the third repeat");
            Assert.That(sheet.sections.UV(new double2(20, 0.25)).x, Is.EqualTo(0.5f).Within(1e-6f), "the end: half way along the tile");

            // a whole number of repeats is not cut
            var whole = sheet.Choose(Wall(16, 0.5f));
            Assert.That(whole.repeatX, Is.EqualTo(2));
        }

        [Test]
        public void ABorderThatRepeatsPartially_KeepsItsRepeatsTheirSize()
        {
            using var sheet = new Sheet(Rect(1, 1024, 0, 1024, 512, HotspotTileFlags.RepeatBorder | HotspotTileFlags.PartialRepeats, 32));
            var shape = Wall(16, 2);
            Assert.That(sheet.sections.Build(sheet.tiles[0], sheet.data, shape, sheet.Choose(shape)), Is.True);
            // the bottom strip: 15.75 units of 3.75 unit repeats, the fifth a fifth of one
            const double start = 0.125, period = 3.75;
            for (int r = 0; r < 5; r++)
                Assert.That(sheet.sections.UV(new double2(start + period * r + 1e-6, 0.0625)).x * 2048, Is.EqualTo(1024 + 32).Within(0.01f), $"repeat {r} starts at the strip's start");
            Assert.That(sheet.sections.UV(new double2(start + period * 4.1, 0.0625)).x * 2048, Is.EqualTo(1024 + 32 + 96).Within(0.01f), "a tenth into the fifth");
        }

        [Test]
        public void ABorderThatRepeats_RepeatsAlongTheEdges_AtItsPixelWidth()
        {
            // 1024 x 512, border 32: its strip along the bottom is 960 pixels long
            using var sheet = new Sheet(Rect(1, 1024, 0, 1024, 512, HotspotTileFlags.RepeatBorder, 32));
            var shape = Wall(16, 2);
            var choice = sheet.Choose(shape);
            Assert.That(sheet.sections.Build(sheet.tiles[0], sheet.data, shape, choice), Is.True);
            // the bottom strip is 16 - 2 x 0.125 = 15.75 units; the tile's 960 pixels at the band's 32 pixels an eighth of
            // a unit are 3.75 units: 4 repeats of 3.9375
            const double start = 0.125, period = 15.75 / 4;
            for (int r = 0; r < 4; r++)
            {
                var uvStart  = sheet.sections.UV(new double2(start + period * r + 1e-6, 0.0625));
                var uvMiddle = sheet.sections.UV(new double2(start + period * (r + 0.5), 0.0625));
                Assert.That(uvStart.x * 2048, Is.EqualTo(1024 + 32).Within(0.01f), $"repeat {r} starts at the start of the tile's strip");
                Assert.That(uvMiddle.x * 2048, Is.EqualTo(1024 + 32 + 480).Within(0.01f), $"repeat {r} has the middle of the strip in its middle");
                Assert.That(uvMiddle.y * 2048, Is.EqualTo(1536 + 16).Within(0.01f), "half way across the band");
            }
            // the left strip is 1.75 units, the tile's 448 pixels make 1.75 units: once
            Assert.That(sheet.sections.UV(new double2(0.0625, 1)).y * 2048, Is.EqualTo(1792).Within(0.01f));
        }
    }
}
