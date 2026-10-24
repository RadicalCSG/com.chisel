using System.Collections.Generic;
using NUnit.Framework;
using Unity.Collections;
using UnityEngine;

namespace Chisel.Core.Tests
{
    // The registry of the hotspot layouts surfaces are drawn with (ChiselHotspotStore)
    [TestFixture]
    public class HotspotStoreTests
    {
        ChiselHotspotLayout first, second;

        static ChiselHotspotLayout Layout(params int[] widths)
        {
            var layout = ScriptableObject.CreateInstance<ChiselHotspotLayout>();
            layout.size = new Vector2Int(2048, 2048);
            layout.pixelsPerUnit = 256;
            layout.tiles = new List<HotspotTile>();
            for (int i = 0; i < widths.Length; i++)
                layout.tiles.Add(new HotspotTile { id = i + 1, name = "T" + i, points = new[] { new Vector2Int(0, i * 512), new Vector2Int(widths[i], i * 512),
                                                                                                new Vector2Int(widths[i], i * 512 + 256), new Vector2Int(0, i * 512 + 256) } });
            return layout;
        }

        [SetUp]
        public void SetUp()
        {
            first = Layout(512, 1024);
            second = Layout(256);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(first);
            Object.DestroyImmediate(second);
        }

        [Test]
        public void ALayout_IsRegisteredOnce_AndNoLayoutIsNoId()
        {
            var a = ChiselHotspotStore.Register(first);
            Assert.That(a, Is.GreaterThan(0));
            Assert.That(ChiselHotspotStore.Register(first), Is.EqualTo(a), "the same layout, the same id");
            var b = ChiselHotspotStore.Register(second);
            Assert.That(b, Is.Not.EqualTo(a).And.GreaterThan(0), "another layout, another id");
            Assert.That(ChiselHotspotStore.Register(null), Is.Zero, "none");
        }

        [Test]
        public void TheTilesOfALayout_AreWhereItsEntryPoints()
        {
            var a = ChiselHotspotStore.Register(first);
            var b = ChiselHotspotStore.Register(second);
            using var tiles = new NativeList<HotspotTileData>(Allocator.Temp);
            using var layouts = new NativeList<HotspotLayoutData>(Allocator.Temp);
            ChiselHotspotStore.Gather(tiles, layouts);
            Assert.That(layouts.Length, Is.GreaterThanOrEqualTo(System.Math.Max(a, b)));
            Assert.That(layouts[a - 1].tileCount, Is.EqualTo(2), "two tiles");
            Assert.That(layouts[b - 1].tileCount, Is.EqualTo(1), "one tile");
            Assert.That(layouts[b - 1].tileStart, Is.Not.EqualTo(layouts[a - 1].tileStart), "not on top of each other");
            Assert.That(layouts[a - 1].tileStart + layouts[a - 1].tileCount, Is.LessThanOrEqualTo(tiles.Length));
        }

        [Test]
        public void AnEditedLayout_ChangesTheGatheredHashAndItsOwnLayoutHashOnly()
        {
            var a = ChiselHotspotStore.Register(first);
            var b = ChiselHotspotStore.Register(second);
            using var tiles = new NativeList<HotspotTileData>(Allocator.Temp);
            using var layouts = new NativeList<HotspotLayoutData>(Allocator.Temp);
            using var before = new NativeList<uint>(Allocator.Temp);
            using var after = new NativeList<uint>(Allocator.Temp);
            var gathered = ChiselHotspotStore.Gather(tiles, layouts);
            ChiselHotspotStore.LayoutHashes(before);
            Assert.That(ChiselHotspotStore.Gather(tiles, layouts), Is.EqualTo(gathered), "nothing changed, the same hash");

            var tile = first.tiles[0];
            tile.border = 32;
            first.tiles[0] = tile;
            Assert.That(ChiselHotspotStore.Gather(tiles, layouts), Is.Not.EqualTo(gathered), "a tile's border");
            ChiselHotspotStore.LayoutHashes(after);
            Assert.That(after[a - 1], Is.Not.EqualTo(before[a - 1]), "the edited layout");
            Assert.That(after[b - 1], Is.EqualTo(before[b - 1]), "the other layout is not mapped again");
        }

        [Test]
        public void ALayoutThatIsGone_IsKeptAsNothing()
        {
            var a = ChiselHotspotStore.Register(first);
            var other = Layout(128);
            var c = ChiselHotspotStore.Register(other);
            Object.DestroyImmediate(other);
            using var tiles = new NativeList<HotspotTileData>(Allocator.Temp);
            using var layouts = new NativeList<HotspotLayoutData>(Allocator.Temp);
            using var hashes = new NativeList<uint>(Allocator.Temp);
            ChiselHotspotStore.Gather(tiles, layouts);
            ChiselHotspotStore.LayoutHashes(hashes);
            Assert.That(layouts[c - 1].tileCount, Is.Zero, "no tiles");
            Assert.That(hashes[c - 1], Is.Zero, "no hash");
            Assert.That(layouts[a - 1].tileCount, Is.EqualTo(2), "the others are as they were");
        }

        [Test]
        public void BudgetsPutBackByAnUndo_AreFoundOnce()
        {
            var repeats = ChiselProjectSettings.HotspotMaxRepeats;
            var triangles = ChiselProjectSettings.HotspotMaxTriangles;
            try
            {
                ChiselHotspotStore.SettingsChanged();
                Assert.That(ChiselHotspotStore.CheckSettings(), Is.False, "nothing changed");
                ChiselProjectSettings.HotspotMaxTriangles = triangles + 1000;
                Assert.That(ChiselHotspotStore.CheckSettings(), Is.True, "the budget changed without a word");
                Assert.That(ChiselHotspotStore.CheckSettings(), Is.False, "told once");
                ChiselProjectSettings.HotspotMaxRepeats = repeats + 7;
                Assert.That(ChiselHotspotStore.CheckSettings(), Is.True, "the other budget");
            }
            finally
            {
                ChiselProjectSettings.HotspotMaxRepeats = repeats;
                ChiselProjectSettings.HotspotMaxTriangles = triangles;
                ChiselHotspotStore.SettingsChanged();
            }
        }
    }
}
