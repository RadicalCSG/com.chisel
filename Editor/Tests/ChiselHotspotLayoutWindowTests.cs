using System.Collections.Generic;
using Chisel.Core;
using NUnit.Framework;
using Unity.Collections;
using UnityEditor;
using UnityEngine;

namespace Chisel.Editors.Tests
{
    // The hotspot layout window's tools, tile list and settings, and a tile tried on a surface
    [TestFixture]
    public class ChiselHotspotLayoutWindowTests
    {
        ChiselHotspotLayout layout;
        readonly List<Object> created = new();

        static HotspotTile Rect(int id, string name, int x, int y, int w, int h, HotspotTileFlags flags = 0, int border = 0) => new()
        {
            id = id, name = name, flags = flags, border = border,
            points = new[] { new Vector2Int(x, y), new Vector2Int(x + w, y), new Vector2Int(x + w, y + h), new Vector2Int(x, y + h) }
        };

        [SetUp]
        public void SetUp()
        {
            layout = ScriptableObject.CreateInstance<ChiselHotspotLayout>();
            created.Add(layout);
            layout.size = new Vector2Int(2048, 2048);
            layout.pixelsPerUnit = 256;
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var item in created)
                if (item) Object.DestroyImmediate(item);
            created.Clear();
        }

        // Each tile's kind, as the editing view colours it and the legend names it
        [Test]
        public void TilesAreColouredByKind()
        {
            Assert.That(HotspotTileKinds.KindOf(Rect(1, "A", 0, 0, 512, 512)), Is.EqualTo(HotspotTileKind.Square));
            Assert.That(HotspotTileKinds.KindOf(Rect(1, "A", 0, 0, 512, 256)), Is.EqualTo(HotspotTileKind.Wide));
            Assert.That(HotspotTileKinds.KindOf(Rect(1, "A", 0, 0, 1024, 256)), Is.EqualTo(HotspotTileKind.Wider), "4:1");
            Assert.That(HotspotTileKinds.KindOf(Rect(1, "A", 0, 0, 128, 512)), Is.EqualTo(HotspotTileKind.Tall));
            Assert.That(HotspotTileKinds.KindOf(Rect(1, "A", 0, 0, 1024, 64, HotspotTileFlags.RepeatU)), Is.EqualTo(HotspotTileKind.Repeats));
            Assert.That(HotspotTileKinds.KindOf(Rect(1, "A", 0, 0, 256, 256, HotspotTileFlags.Radial)), Is.EqualTo(HotspotTileKind.Radial));
            var triangle = new HotspotTile { id = 1, points = new[] { new Vector2Int(0, 0), new Vector2Int(128, 0), new Vector2Int(64, 128) } };
            Assert.That(HotspotTileKinds.KindOf(triangle), Is.EqualTo(HotspotTileKind.Shape));
            Assert.That(HotspotTileKinds.Names.Count, Is.EqualTo(7), "a legend entry for each kind");
        }

        // The list's tags, and its filter by name or by tag
        [Test]
        public void TheTileList_FiltersByNameOrFlag()
        {
            var pinOnly = Rect(3, "C1", 0, 0, 512, 128, HotspotTileFlags.Alternate, border: 24);
            Assert.That(HotspotTileKinds.TagsOf(pinOnly), Is.EqualTo(new[] { "pin only", "border" }));
            Assert.That(HotspotTileKinds.Matches(pinOnly, "pin"), Is.True, "a tag");
            Assert.That(HotspotTileKinds.Matches(pinOnly, "c1"), Is.True, "the name");
            Assert.That(HotspotTileKinds.Matches(pinOnly, "radial"), Is.False);
            Assert.That(HotspotTileKinds.Matches(pinOnly, ""), Is.True);

            layout.tiles = new List<HotspotTile> { Rect(1, "A1", 0, 0, 512, 512), pinOnly, Rect(2, "R1", 512, 0, 256, 256, HotspotTileFlags.Radial) };
            var window = ScriptableObject.CreateInstance<ChiselHotspotLayoutWindow>();
            created.Add(window);
            window.SetLayout(layout);
            window.TileFilter = "pin";
            Assert.That(window.ListedTiles(), Is.EqualTo(new[] { 3 }));
            window.TileFilter = string.Empty;
            Assert.That(window.ListedTiles().Count, Is.EqualTo(3));
        }

        // A label is shown only where it fits its tile
        [Test]
        public void Labels_ThatDontFit_AreLeftOut()
        {
            Assert.That(HotspotTileKinds.LabelFits(new Vector2(40, 14), new UnityEngine.Rect(0, 0, 60, 30)), Is.True);
            Assert.That(HotspotTileKinds.LabelFits(new Vector2(70, 14), new UnityEngine.Rect(0, 0, 60, 30)), Is.False, "too wide");
            Assert.That(HotspotTileKinds.LabelFits(new Vector2(40, 14), new UnityEngine.Rect(0, 0, 60, 12)), Is.False, "too low");
            Assert.That(ChiselHotspotLayoutWindow.LabelOf(Rect(1, "B1", 0, 0, 512, 256), true, true), Is.EqualTo($"B1 2:1 512{HotspotTileChoice.kTimes}256"));
        }

        // The Split tool splits a tile of the lines strictly inside it, never a drawn shape or a triangle
        [Test]
        public void TheSplitTool_SplitsOnlyWhereALineCanGo()
        {
            layout.size = new Vector2Int(1024, 1024);
            layout.ResetCuts();
            var whole = layout.tiles[0].id;
            Assert.That(ChiselHotspotLayoutWindow.SplitAt(layout, new Vector2(300, 100), false, 8, out var tile, out var kind, out var position), Is.True);
            Assert.That((tile, kind, position), Is.EqualTo((whole, HotspotCutKind.Vertical, 304)), "snapped");
            Assert.That(ChiselHotspotLayoutWindow.SplitAt(layout, new Vector2(300, 101), true, 8, out _, out kind, out position), Is.True);
            Assert.That((kind, position), Is.EqualTo((HotspotCutKind.Horizontal, 104)), "Shift: horizontally");
            Assert.That(ChiselHotspotLayoutWindow.SplitAt(layout, new Vector2(2, 100), false, 8, out _, out _, out _), Is.False, "on the tile's edge");

            Assert.That(layout.Split(whole, HotspotCutKind.Slash, 0), Is.True);
            var triangle = layout.TileAt(new Vector2(900, 100));
            Assert.That(layout.CanSplit(triangle, HotspotCutKind.Vertical, 512), Is.False, "a triangle");

            layout.ResetCuts();
            var shape = layout.AddShape(new[] { new Vector2Int(100, 100), new Vector2Int(400, 100), new Vector2Int(250, 400) });
            Assume.That(shape, Is.Not.Zero);
            Assert.That(ChiselHotspotLayoutWindow.SplitAt(layout, new Vector2(250, 200), false, 1, out _, out _, out _), Is.False, "a drawn shape");
        }

        // Draw hole only with a drawn shape selected; finishing a shape goes back to Select with it selected
        [Test]
        public void DrawHole_NeedsADrawnShape()
        {
            layout.size = new Vector2Int(1024, 1024);
            layout.ResetCuts();
            var window = ScriptableObject.CreateInstance<ChiselHotspotLayoutWindow>();
            created.Add(window);
            window.SetLayout(layout);
            window.SetTool(ChiselHotspotLayoutWindow.Tool.DrawHole);
            Assert.That(window.CurrentTool, Is.EqualTo(ChiselHotspotLayoutWindow.Tool.Select), "no shape selected");
            var shape = layout.AddShape(new[] { new Vector2Int(100, 100), new Vector2Int(400, 100), new Vector2Int(250, 400) });
            window.SelectedTile = shape;
            window.SetTool(ChiselHotspotLayoutWindow.Tool.DrawHole);
            Assert.That(window.CurrentTool, Is.EqualTo(ChiselHotspotLayoutWindow.Tool.DrawHole));
            Assert.That(ChiselHotspotLayoutWindow.HintOf(ChiselHotspotLayoutWindow.Tool.Split), Does.Contain("Shift"), "the status bar says Shift splits horizontally");
        }

        // A budget of 0 says what it takes: the project's setting
        [Test]
        public void ABudgetOfZero_ShowsTheProjectsSetting()
        {
            Assert.That(ChiselHotspotLayoutWindow.ProjectSettingText(ChiselProjectSettings.HotspotMaxRepeats),
                        Is.EqualTo($"Project setting ({ChiselProjectSettings.HotspotMaxRepeats})"));
            Assert.That(ChiselHotspotLayoutWindow.OnASurface(new Vector2Int(512, 256), 256), Is.EqualTo($"2 {HotspotTileChoice.kTimes} 1 m"));
        }

        // A tile of the surface's aspect fits it exactly: the mapping's uvs are the tile's bounds
        [Test]
        public void TryingATile_OnItsOwnAspect_FitsWithoutStretching()
        {
            layout.tiles = new List<HotspotTile> { Rect(1, "B1", 1024, 0, 1024, 512) };
            var tried = HotspotTryOn.Run(layout, 1, 2);
            Assert.That(tried.mapped, Is.True);
            Assert.That(tried.size, Is.EqualTo(new Vector2(4, 2)), "its longer side at the layout's pixels per unit");
            Assert.That(tried.Describe(out var good), Is.EqualTo("Fits without stretching"));
            Assert.That(good, Is.True);
            Assert.That(tried.indices.Count, Is.GreaterThan(0));
            var min = new Vector2(float.MaxValue, float.MaxValue); var max = new Vector2(float.MinValue, float.MinValue);
            foreach (var uv in tried.uvs) { min = Vector2.Min(min, uv); max = Vector2.Max(max, uv); }
            // the tile's pixels, y up
            Assert.That(Vector2.Distance(min, new Vector2(0.5f, 0.75f)), Is.LessThan(1e-4f));
            Assert.That(Vector2.Distance(max, new Vector2(1, 1)), Is.LessThan(1e-4f));
        }

        // On a surface of another aspect it says how matching lays it: stretched, the border keeping its width, or
        // repeated; never an estimate from the aspects
        [Test]
        public void TryingATile_SaysHowMatchingLaysIt()
        {
            layout.tiles = new List<HotspotTile>
            {
                Rect(1, "B1", 1024, 0, 1024, 512, border: 32),
                Rect(2, "T1", 0, 1024, 1024, 64, HotspotTileFlags.RepeatU),
            };
            var bordered = HotspotTryOn.Run(layout, 1, 4);
            var text = bordered.Describe(out var good);
            Assert.That(good, Is.False);
            Assert.That(text, Does.Contain("taller").Or.Contain("lower").Or.Contain("wider").Or.Contain("narrower"));
            Assert.That(text, Does.Contain("the border keeps its width"));

            // the stretch is matching's own (its choice's scales)
            Assert.That(bordered.Stretch.x, Is.EqualTo(1 / bordered.choice.scales.x).Within(1e-5f));

            var repeating = HotspotTryOn.Run(layout, 2, 8);
            Assert.That(repeating.mapped, Is.True);
            Assert.That(repeating.size, Is.EqualTo(new Vector2(4, 0.5f)));
            Assert.That(repeating.Describe(out _), Does.Contain("taller"), "8:1 is twice as high as 16:1 at the tile's length");
        }

        // The matcher says how much a tile stretches along each of the surface's axes
        [Test]
        public void TheMatcher_SaysTheStretchAlongEachAxis()
        {
            layout.tiles = new List<HotspotTile> { Rect(1, "B1", 1024, 0, 1024, 512) };
            using var tiles = new NativeList<HotspotTileData>(Allocator.Temp);
            HotspotMatcher.AddTiles(layout, tiles, out var data);
            // a 4 x 1 unit surface: 1024 x 256 pixels at 256 a unit
            using var positions = new NativeArray<Unity.Mathematics.float3>(new Unity.Mathematics.float3[] { new(0, 0, 0), new(4, 0, 0), new(4, 1, 0), new(0, 1, 0) }, Allocator.Temp);
            var shape = HotspotMatcher.ShapeOf(positions, new Unity.Mathematics.float3(1, 0, 0), new Unity.Mathematics.float3(0, 1, 0));
            var choice = HotspotMatcher.Choose(tiles.AsArray(), data, shape, 0, 1);
            Assert.That(choice.scales.x, Is.EqualTo(1).Within(1e-4f), "as long as the tile");
            Assert.That(choice.scales.y, Is.EqualTo(2).Within(1e-4f), "half as high: two tile pixels to a surface pixel");
        }
    }
}
