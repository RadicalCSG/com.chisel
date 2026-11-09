using System;
using System.Collections.Generic;
using Chisel.Components;
using Chisel.Core;
using UnityEditor;
using UnityEngine;

namespace Chisel.Editors
{
    /// <summary>How a tile fits the surfaces it would be pinned on</summary>
    public enum HotspotTileFit { All, Some, None }

    /// <summary>
    /// The surfaces using one hotspot material that a tile is chosen for: what they keep, and how many of them each tile
    /// fits, as matching sees it (HotspotMatcher.Fits)
    /// </summary>
    public sealed class HotspotTileGroup
    {
        public Material             material;
        public ChiselHotspotLayout  layout;
        public readonly List<SurfaceReference> surfaces = new();

        /// <summary>(sheet, id) of the tile they keep: id 0 Automatic, -1 when they keep different ones</summary>
        public Vector2Int pinned;

        // per tile (sheet, id): how many of the surfaces whose shape is known it fits
        internal readonly Dictionary<Vector2Int, int> fitCounts = new();
        // surfaces whose shape isn't known (not mapped yet) count as fitting every tile
        internal int unknown;
        // (sheet, id) of the inside corner pieces, which no surface takes
        internal readonly HashSet<Vector2Int> pieces = new();

        public int Count => surfaces.Count;

        /// <summary>How many of the surfaces a tile fits</summary>
        public int FitCount(Vector2Int tile)
        {
            if (pieces.Contains(tile))
                return 0;
            fitCounts.TryGetValue(tile, out var count);
            return count + unknown;
        }

        /// <summary>How a tile fits: all the surfaces, some, or none</summary>
        public HotspotTileFit FitOf(Vector2Int tile)
        {
            var count = FitCount(tile);
            return count >= Count && count > 0 ? HotspotTileFit.All : count > 0 ? HotspotTileFit.Some : HotspotTileFit.None;
        }

        /// <summary>Whether a tile can be pinned on these surfaces: it fits at least one of them</summary>
        public bool CanPin(Vector2Int tile) => tile.y != 0 && FitOf(tile) != HotspotTileFit.None;
    }

    /// <summary>Choosing the tile surfaces keep: the surfaces by hotspot material, what's said about it, and pinning</summary>
    public static class HotspotTileChoice
    {
        public const string kPinUndoName = "Pin Hotspot Tile";

        /// <summary>
        /// The surfaces with a hotspot material, by material, the material most of them use first. fitsOf: (sheet, id) of
        /// the tiles a surface can take, null when not known; matching's own (ChiselHotspotGroupEdit) when not given
        /// </summary>
        public static List<HotspotTileGroup> GroupsOf(IEnumerable<SurfaceReference> surfaces,
                                                      Func<SurfaceReference, HotspotTileGroup, HashSet<Vector2Int>> fitsOf = null)
        {
            var groups = new List<HotspotTileGroup>();
            if (surfaces == null)
                return groups;
            foreach (var surface in surfaces)
            {
                var material = surface?.RenderMaterial;
                var layout = HotspotTransformGUI.LayoutOf(material);
                if (layout == null)
                    continue;
                var group = groups.Find(g => g.material == material);
                if (group == null)
                {
                    group = new HotspotTileGroup { material = material, layout = layout };
                    groups.Add(group);
                }
                var hotspot = surface.SurfaceDetails.hotspot;
                var pinned = new Vector2Int(hotspot.pinnedTile == 0 ? 0 : hotspot.pinnedSheet, hotspot.pinnedTile);
                group.pinned = group.surfaces.Count == 0 || group.pinned == pinned ? pinned : new Vector2Int(0, -1);
                group.surfaces.Add(surface);
            }
            groups.Sort((a, b) => a.Count != b.Count ? b.Count.CompareTo(a.Count) : string.Compare(a.material.name, b.material.name, StringComparison.OrdinalIgnoreCase));
            foreach (var group in groups)
                CountFits(group, fitsOf);
            return groups;
        }

        static void CountFits(HotspotTileGroup group, Func<SurfaceReference, HotspotTileGroup, HashSet<Vector2Int>> fitsOf)
        {
            group.fitCounts.Clear();
            group.unknown = 0;
            group.pieces.Clear();
            var sheets = SheetCount(group);
            for (int sheet = 0; sheet <= sheets; sheet++)
            {
                var layout = LayoutOfSheet(group, sheet);
                if (layout == null || layout.tiles == null)
                    continue;
                foreach (var tile in layout.tiles)
                {
                    if (IsPieces(tile))
                        group.pieces.Add(new Vector2Int(sheet, tile.id));
                }
            }
            if (fitsOf != null)
            {
                foreach (var surface in group.surfaces)
                    Count(group, fitsOf(surface, group));
                return;
            }
            // the tiles once for all of them
            using var tiles = new Unity.Collections.NativeList<HotspotTileData>(Unity.Collections.Allocator.Temp);
            var sheetOf = new List<int>();
            ChiselHotspotGroupEdit.TilesOf(group.layout, group.material, tiles, sheetOf);
            foreach (var surface in group.surfaces)
                Count(group, ChiselHotspotGroupEdit.FitsOf(surface, tiles, sheetOf));
        }

        static void Count(HotspotTileGroup group, HashSet<Vector2Int> fits)
        {
            if (fits == null)
            {
                group.unknown++;
                return;
            }
            foreach (var tile in fits)
            {
                group.fitCounts.TryGetValue(tile, out var count);
                group.fitCounts[tile] = count + 1;
            }
        }

        /// <summary>A tile of inside corner pieces, which no surface takes</summary>
        public static bool IsPieces(in HotspotTile tile) => (tile.flags & (HotspotTileFlags.InsideCorners | HotspotTileFlags.InsideCornerTurned)) != 0;

        /// <summary>How many other sheets the group's material is drawn with</summary>
        public static int SheetCount(HotspotTileGroup group)
        {
            var metadata = group.material != null ? group.material.GetMetadataOfType<ChiselSurfaceMetadata>() : null;
            return metadata?.hotspotSheets?.Count ?? 0;
        }

        /// <summary>How many tiles of a sheet fit all the surfaces, and how many some: "3 fit all 4, 9 fit some", dot separated</summary>
        public static string Summary(HotspotTileGroup group, ChiselHotspotLayout layout, int sheet)
        {
            int all = 0, some = 0;
            if (layout != null && layout.tiles != null)
            {
                foreach (var tile in layout.tiles)
                {
                    switch (group.FitOf(new Vector2Int(sheet, tile.id)))
                    {
                        case HotspotTileFit.All:  all++;  break;
                        case HotspotTileFit.Some: some++; break;
                    }
                }
            }
            return some > 0 ? $"{all} fit all {group.Count} {kDot} {some} fit some" : $"{all} fit all {group.Count}";
        }

        internal const string kDot   = "\u00B7";
        internal const string kTimes = "\u00D7";
        internal const string kDash  = "\u2014";

        /// <summary>How many of the surfaces a tile fits, as the info row says it</summary>
        public static string FitText(HotspotTileGroup group, Vector2Int tile)
        {
            if (group.pieces.Contains(tile))
                return "Inside corner pieces, never matched";
            var count = group.FitCount(tile);
            return count >= group.Count ? $"Fits all {group.Count}" : count > 0 ? $"Fits {count} of {group.Count}" : "Doesn't fit";
        }

        /// <summary>The id of the tile with a tile's inside corner pieces, 0 for none</summary>
        public static int CornersOf(ChiselHotspotLayout layout, int id)
        {
            var index = layout != null ? layout.IndexOfTile(id) : -1;
            if (index < 0)
                return 0;
            var corners = layout.tiles[index].insideCorners;
            var at = corners != 0 && corners != id ? layout.IndexOfTile(corners) : -1;
            return at >= 0 && IsPieces(layout.tiles[at]) ? corners : 0;
        }

        /// <summary>The ids of the tiles that take their inside corner pieces from this one</summary>
        public static List<int> UsersOf(ChiselHotspotLayout layout, int pieces)
        {
            var users = new List<int>();
            if (layout == null || layout.tiles == null || pieces == 0)
                return users;
            foreach (var tile in layout.tiles)
            {
                if (tile.id != pieces && CornersOf(layout, tile.id) == pieces)
                    users.Add(tile.id);
            }
            return users;
        }

        /// <summary>A width to height ratio as a ratio of small whole numbers when it is one ("2 : 1"), or else to 1</summary>
        public static string AspectText(int width, int height)
        {
            if (width <= 0 || height <= 0)
                return string.Empty;
            int a = width, b = height;
            while (b != 0) { var r = a % b; a = b; b = r; }
            int w = width / a, h = height / a;
            return w <= 16 && h <= 16 ? $"{w} : {h}" : ((float)width / height).ToString("0.##", System.Globalization.CultureInfo.InvariantCulture) + " : 1";
        }

        /// <summary>A tile's size in pixels, its aspect, and what its flags say: "512 x 256, 2 : 1, Border", dot separated</summary>
        public static string InfoOf(ChiselHotspotLayout layout, int id)
        {
            var index = layout != null ? layout.IndexOfTile(id) : -1;
            if (index < 0)
                return string.Empty;
            var tile = layout.tiles[index];
            var parts = new List<string>();
            if (tile.points != null && tile.points.Length > 0)
            {
                var min = tile.points[0]; var max = tile.points[0];
                foreach (var point in tile.points) { min = Vector2Int.Min(min, point); max = Vector2Int.Max(max, point); }
                var size = max - min;
                parts.Add($"{size.x} {kTimes} {size.y}");
                var aspect = AspectText(size.x, size.y);
                if (aspect.Length > 0)
                    parts.Add(aspect);
            }
            if (tile.border > 0)                                           parts.Add("Border");
            if ((tile.flags & HotspotTileFlags.Disabled) != 0)             parts.Add("Disabled");
            if ((tile.flags & HotspotTileFlags.Alternate) != 0)            parts.Add("Alternate");
            if ((tile.flags & HotspotTileFlags.Radial) != 0)               parts.Add("Radial");
            if ((tile.flags & HotspotTileFlags.RepeatU) != 0)              parts.Add("Repeats across");
            if ((tile.flags & HotspotTileFlags.RepeatV) != 0)              parts.Add("Repeats up");
            if ((tile.flags & HotspotTileFlags.InsideCorners) != 0)        parts.Add("Inside corners");
            if ((tile.flags & HotspotTileFlags.InsideCornerTurned) != 0)   parts.Add("Inside corner (turned)");
            return string.Join($" {kDot} ", parts);
        }

        /// <summary>A pin only tile: Disabled or Alternate, never chosen by matching</summary>
        public static bool IsPinOnly(in HotspotTile tile) => (tile.flags & (HotspotTileFlags.Disabled | HotspotTileFlags.Alternate)) != 0;

        /// <summary>A tile's name, "Tile 7" when it has none; empty when the layout has no such tile</summary>
        public static string NameOf(ChiselHotspotLayout layout, int id)
        {
            var index = layout != null ? layout.IndexOfTile(id) : -1;
            return index < 0 ? string.Empty : (string.IsNullOrEmpty(layout.tiles[index].name) ? "Tile " + id : layout.tiles[index].name);
        }

        /// <summary>What a group keeps, as the field shows it: "Automatic", the tile's name, or a dash when mixed</summary>
        public static string ChoiceOf(HotspotTileGroup group)
        {
            if (group.pinned.y < 0)
                return kDash;
            if (group.pinned.y == 0)
                return "Automatic";
            var layout = LayoutOfSheet(group, group.pinned.x);
            var name = NameOf(layout, group.pinned.y);
            return string.IsNullOrEmpty(name) ? $"Missing ({group.pinned.y})" : name;
        }

        /// <summary>The field's text: each material's choice, most used first: "B2 / Automatic"</summary>
        public static string FieldText(IReadOnlyList<HotspotTileGroup> groups)
        {
            if (groups == null || groups.Count == 0)
                return string.Empty;
            var parts = new List<string>();
            foreach (var group in groups)
                parts.Add(ChoiceOf(group));
            return string.Join(" / ", parts);
        }

        /// <summary>Whether a tile's name has what's typed in it, ignoring case; anything does when nothing is typed</summary>
        public static bool NameMatches(string name, string find)
        {
            return string.IsNullOrWhiteSpace(find) || (name ?? string.Empty).IndexOf(find.Trim(), StringComparison.OrdinalIgnoreCase) >= 0;
        }

        /// <summary>The layout of a sheet of the group's material: its own (0), or one of its other sheets by place</summary>
        public static ChiselHotspotLayout LayoutOfSheet(HotspotTileGroup group, int sheet)
        {
            if (sheet == 0)
                return group.layout;
            var metadata = group.material != null ? group.material.GetMetadataOfType<ChiselSurfaceMetadata>() : null;
            var sheets = metadata?.hotspotSheets;
            if (sheets == null || sheet < 1 || sheet > sheets.Count || sheets[sheet - 1] == null)
                return null;
            return sheets[sheet - 1].GetMetadataOfType<ChiselSurfaceMetadata>()?.hotspotLayout;
        }

        /// <summary>
        /// Pins a tile (sheet, id) on the group's surfaces, undoably; id 0 lets matching choose again. Surfaces connected
        /// to them take it too (ChiselHotspotGroupEdit.Spread). False when nothing changed
        /// </summary>
        public static bool Pin(HotspotTileGroup group, Vector2Int tile)
        {
            if (group == null)
                return false;
            var sheet = tile.y == 0 ? 0 : tile.x;
            var edited = new List<SurfaceReference>();
            var done = new HashSet<(ChiselGeneratorComponent, int)>();
            foreach (var surface in group.surfaces)
            {
                if (surface == null || !surface.node || !done.Add((surface.node, surface.descriptionIndex)))
                    continue;
                var details = surface.SurfaceDetails;
                if (details.hotspot.pinnedTile == tile.y && details.hotspot.pinnedSheet == sheet)
                    continue;
                Undo.RecordObject(surface.node, kPinUndoName);
                details.hotspot.pinnedTile  = tile.y;
                details.hotspot.pinnedSheet = sheet;
                surface.SurfaceDetails = details;
                EditorUtility.SetDirty(surface.node);
                edited.Add(surface);
            }
            if (edited.Count == 0)
                return false;
            ChiselHotspotGroupEdit.Spread(edited, kPinUndoName);
            group.pinned = new Vector2Int(sheet, tile.y);
            return true;
        }
    }
}
