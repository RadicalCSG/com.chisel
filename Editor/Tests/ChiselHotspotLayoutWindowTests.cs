using System.Collections.Generic;
using Chisel.Core;
using NUnit.Framework;
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


        // The Split tool splits a tile of the lines strictly inside it, never a drawn shape or a triangle, with the kind
        // of split chosen; Shift swaps vertical and horizontal
        [Test]
        public void TheSplitTool_SplitsOnlyWhereALineCanGo()
        {
            layout.size = new Vector2Int(1024, 1024);
            layout.ResetCuts();
            var whole = layout.tiles[0].id;
            Assert.That(ChiselHotspotLayoutWindow.SplitAt(layout, new Vector2(300, 100), HotspotCutKind.Vertical, 8, out var tile, out var position), Is.True);
            Assert.That((tile, position), Is.EqualTo((whole, 304)), "snapped");
            var kind = ChiselHotspotLayoutWindow.SplitKindWith(HotspotCutKind.Vertical, shift: true);
            Assert.That(kind, Is.EqualTo(HotspotCutKind.Horizontal), "Shift");
            Assert.That(ChiselHotspotLayoutWindow.SplitAt(layout, new Vector2(300, 101), kind, 8, out _, out position), Is.True);
            Assert.That(position, Is.EqualTo(104), "horizontally: at y");
            Assert.That(ChiselHotspotLayoutWindow.SplitKindWith(HotspotCutKind.Apex, shift: true), Is.EqualTo(HotspotCutKind.Apex), "only vertical and horizontal swap");
            Assert.That(ChiselHotspotLayoutWindow.SplitAt(layout, new Vector2(2, 100), HotspotCutKind.Vertical, 8, out _, out _), Is.False, "on the tile's edge");
            Assert.That(ChiselHotspotLayoutWindow.SplitAt(layout, new Vector2(300, 100), HotspotCutKind.Slash, 8, out _, out position), Is.True);
            Assert.That(position, Is.Zero, "a diagonal takes no position");

            Assert.That(layout.Split(whole, HotspotCutKind.Slash, 0), Is.True);
            var triangle = layout.TileAt(new Vector2(900, 100));
            Assert.That(layout.CanSplit(triangle, HotspotCutKind.Vertical, 512), Is.False, "a triangle");

            layout.ResetCuts();
            var shape = layout.AddShape(new[] { new Vector2Int(100, 100), new Vector2Int(400, 100), new Vector2Int(250, 400) });
            Assume.That(shape, Is.Not.Zero);
            Assert.That(ChiselHotspotLayoutWindow.SplitAt(layout, new Vector2(250, 200), HotspotCutKind.Vertical, 1, out _, out _), Is.False, "a drawn shape");
        }

        // The dashed preview is the lines the layout gets: an apex's two from the bottom corners to the top
        [Test]
        public void TheSplitPreview_IsTheLinesTheSplitMakes()
        {
            layout.size = new Vector2Int(1024, 1024);
            layout.ResetCuts();
            var whole = layout.tiles[0].id;
            var preview = ChiselHotspotLayoutWindow.SplitLines(HotspotCutKind.Apex, 400, Vector2Int.zero, new Vector2Int(1024, 1024));
            Assert.That(layout.Split(whole, HotspotCutKind.Apex, 400), Is.True);
            var made = new List<HotspotLine>();
            layout.GetLines(made);
            Assert.That(preview.Count, Is.EqualTo(made.Count));
            for (int i = 0; i < made.Count; i++)
                Assert.That((preview[i].a, preview[i].b), Is.EqualTo(((Vector2)made[i].a, (Vector2)made[i].b)));
            Assert.That(ChiselHotspotLayoutWindow.SplitKindName(HotspotCutKind.Backslash), Is.EqualTo("Diagonal \\"));
        }

        // Without lines (an import whose tiles aren't a grid) there is nothing to split: the Split tool can't be chosen,
        // and its tooltip says why
        [Test]
        public void TheSplitTool_NeedsALayoutWithLines()
        {
            layout.tiles = new List<HotspotTile> { Rect(1, "A1", 0, 0, 512, 512) };
            Assume.That(layout.HasCuts, Is.False);
            var window = ScriptableObject.CreateInstance<ChiselHotspotLayoutWindow>();
            created.Add(window);
            window.SetLayout(layout);
            window.SetTool(ChiselHotspotLayoutWindow.Tool.Split);
            Assert.That(window.CurrentTool, Is.EqualTo(ChiselHotspotLayoutWindow.Tool.Select));
            Assert.That(ChiselHotspotLayoutWindow.kSplitDisabled.tooltip, Does.Contain("no lines"), "the tooltip says why");
            Assert.That(ChiselHotspotLayoutWindow.kResetName, Is.EqualTo("Reset"));
        }

        // Draw hole only with a drawn shape selected; the tools' tooltips hold their clicks and keys
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
            Assert.That(ChiselHotspotLayoutWindow.HintOf(ChiselHotspotLayoutWindow.Tool.Split), Does.Contain("Shift"), "Split's tooltip says Shift swaps");
        }

        // One dropdown sets whether a tile is the pieces of inside corners: none, four pieces or one turned
        [Test]
        public void CornerPieces_AreOneChoice()
        {
            var flags = HotspotTileFlags.RepeatU;
            Assert.That(ChiselHotspotLayoutWindow.CornerPiecesOf(flags), Is.EqualTo(ChiselHotspotLayoutWindow.CornerPieces.None));
            flags = ChiselHotspotLayoutWindow.WithCornerPieces(flags, ChiselHotspotLayoutWindow.CornerPieces.Four);
            Assert.That(flags, Is.EqualTo(HotspotTileFlags.RepeatU | HotspotTileFlags.InsideCorners));
            flags = ChiselHotspotLayoutWindow.WithCornerPieces(flags, ChiselHotspotLayoutWindow.CornerPieces.OneTurned);
            Assert.That(flags, Is.EqualTo(HotspotTileFlags.RepeatU | HotspotTileFlags.InsideCornerTurned), "the other one taken off");
            flags = ChiselHotspotLayoutWindow.WithCornerPieces(flags, ChiselHotspotLayoutWindow.CornerPieces.None);
            Assert.That(flags, Is.EqualTo(HotspotTileFlags.RepeatU));
            var both = HotspotTileFlags.InsideCorners | HotspotTileFlags.InsideCornerTurned;
            Assert.That(ChiselHotspotLayoutWindow.WithCornerPieces(both, ChiselHotspotLayoutWindow.CornerPieces.Four), Is.EqualTo(both), "left as it is when unchanged");
        }

        // A budget of 0 says what it takes: the project's setting
        [Test]
        public void ABudgetOfZero_ShowsTheProjectsSetting()
        {
            Assert.That(ChiselHotspotLayoutWindow.ProjectSettingText(ChiselProjectSettings.HotspotMaxRepeats),
                        Is.EqualTo($"Project setting ({ChiselProjectSettings.HotspotMaxRepeats})"));
            Assert.That(ChiselHotspotLayoutWindow.OnASurface(new Vector2Int(512, 256), 256), Is.EqualTo($"2 {HotspotTileChoice.kTimes} 1 m"));
        }
    }
}