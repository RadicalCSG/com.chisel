using System;
using System.Collections.Generic;
using UnityEngine;

namespace Chisel.Core
{
    [Serializable, Flags]
    public enum HotspotTileFlags : int
    {
        None          = 0,
        // Never chosen by matching; a surface can still be pinned to it
        Disabled      = 1 << 0,
        // Only for round surfaces, and round surfaces only take these
        Radial        = 1 << 1,
        // Meant to repeat along its width or its height
        RepeatU       = 1 << 2,
        RepeatV       = 1 << 3,
        // Matching may turn the tile, or mirror it
        AllowRotation = 1 << 4,
        AllowMirror   = 1 << 5,
        // Only taken when pinned, like Valve's "alt" rectangles
        Alternate     = 1 << 6,
        // The border's strips repeat along the edges instead of stretching
        RepeatBorder  = 1 << 7,
        // Repeats keep their exact size, and the last one is cut off, instead of all stretching to fit
        PartialRepeats = 1 << 8,
        // Inside corner pieces for bordered tiles (HotspotTile.insideCorners), never matched: four, one for each way an
        // inside corner turns, the corner where their outer edges meet in the middle
        InsideCorners  = 1 << 9,
        // One inside corner piece, drawn as the top left of the four, turned for the others
        InsideCornerTurned = 1 << 10,
    }

    // How a border strip along one side of a tile takes the length of a surface's edge
    public enum HotspotBorderMode : int
    {
        // as the tile's flags say (RepeatBorder, PartialRepeats)
        Tile          = 0,
        Stretch       = 1,
        Repeat        = 2,
        // repeats of their exact size, the last one cut off
        RepeatPartial = 3,
    }

    // The border along one side of a tile (from its corner to the next, in the order of its points)
    [Serializable]
    public struct HotspotBorderSide
    {
        public HotspotBorderMode mode;
        // Stretching: the most a strip may stretch (2: twice its length in the tile) before it repeats instead, the
        // fewest repeats that keep within it; 0 for no limit
        public float             maxStretch;
    }

    // A hole in a tile: its corners in pixels, origin at the top left of the texture
    [Serializable]
    public struct HotspotTileHole
    {
        public Vector2Int[] points;
    }

    // One region of a hotspot texture: a rectangle, a triangle, or any other outline (with holes), a shaped tile
    [Serializable]
    public struct HotspotTile
    {
        // Stable within its layout; what a surface pins
        public int              id;
        public string           name;
        // Corners in pixels, origin at the top left of the texture, clockwise as the texture shows them
        public Vector2Int[]     points;
        public HotspotTileFlags flags;
        // Width in pixels of the tile's border, 0 for none
        public int              border;
        // Overrides the layout's pixels per unit when above 0
        public float            pixelsPerUnit;
        // The border along each side, in the order of the points (side i from point i to the next); empty: as the flags
        // say along every side
        public HotspotBorderSide[] sides;
        // Holes in it (a shaped tile)
        public HotspotTileHole[] holes;
        // The tile (id) with the pieces for this tile's border at inside corners (InsideCorners, InsideCornerTurned);
        // 0 for none, the strips meeting on a line through the corner
        public int              insideCorners;
    }

    public enum HotspotCutKind : byte
    {
        Tile,
        // A line at x = position: first is left of it, second right
        Vertical,
        // A line at y = position: first is above it, second below
        Horizontal,
        // The diagonal from the bottom left to the top right: first is the upper left triangle, second the lower right
        Slash,
        // The diagonal from the top left to the bottom right: first is the upper right triangle, second the lower left
        Backslash,
        // Lines from the bottom corners to (position, top): first is the left triangle, second the middle, third the right
        Apex,
    }

    // A node of the layout's cuts. The root (index 0) covers the whole texture; a cut divides its region between its
    // children, a tile node is one tile (by id). Triangles are not cut further.
    [Serializable]
    public struct HotspotCut
    {
        public HotspotCutKind kind;
        public int            position;
        public int            first, second, third;
        public int            tile;
    }

    // The regions of a hotspot texture, and how many pixels of it a unit of surface takes
    [CreateAssetMenu(menuName = "Chisel/Hotspot Layout", fileName = "Hotspot Layout")]
    public sealed partial class ChiselHotspotLayout : ScriptableObject
    {
        public Texture2D         texture;
        public Vector2Int        size          = new(2048, 2048);
        public float             pixelsPerUnit = 256;
        // Pixels left out along every tile's edges (a gutter), so filtering never reaches a neighbouring tile; a tile's
        // border keeps its inner edge, so it narrows by as much
        public float             padding       = 0;
        // Pixels a bordered tile's interior leaves out where it meets the border (0 for none), so filtering at the
        // interior's repeat seams never reaches the border
        public float             interiorPadding = 0;
        // Budgets for one surface (0: the project's, ChiselProjectSettings): the most repeats a strip, a repeating tile
        // or an interior lays (more: stretched instead), and the most triangles its cut may draw (more: the interior's
        // repeats left out, then the cut)
        public int               maxRepeats    = 0;
        public int               maxTriangles  = 0;
        public List<HotspotTile> tiles         = new();
        // Empty when the tiles were given without them (an import that is not a grid)
        public List<HotspotCut>  cuts          = new();

        public event Action Changed;

        /// <summary>Any layout changed</summary>
        public static event Action<ChiselHotspotLayout> AnyChanged;

        void OnValidate()
        {
            Changed?.Invoke();
            AnyChanged?.Invoke(this);
            ChiselHotspotStore.LayoutChanged(this);
        }

        // To call after an edit
        public void NotifyChanged() { OnValidate(); }

        // What mapping takes from the layout, the same in every session: its settings and its tiles (not their names,
        // the texture or the cuts)
        public void AppendContentHash(ref Hash128 hash)
        {
            var settings = new Unity.Mathematics.float4(pixelsPerUnit, padding, interiorPadding, 0);
            var sizeAndBudgets = new Unity.Mathematics.int4(size.x, size.y, maxRepeats, maxTriangles);
            hash.Append(ref settings);
            hash.Append(ref sizeAndBudgets);
            var count = tiles?.Count ?? 0;
            hash.Append(ref count);
            for (int i = 0; i < count; i++)
            {
                var tile = tiles[i];
                var header = new Unity.Mathematics.int4(tile.id, (int)tile.flags, tile.border, tile.insideCorners);
                var density = tile.pixelsPerUnit;
                hash.Append(ref header);
                hash.Append(ref density);
                AppendPoints(ref hash, tile.points);
                var sideCount = tile.sides?.Length ?? 0;
                hash.Append(ref sideCount);
                for (int j = 0; j < sideCount; j++)
                {
                    var mode = (int)tile.sides[j].mode;
                    var stretch = tile.sides[j].maxStretch;
                    hash.Append(ref mode);
                    hash.Append(ref stretch);
                }
                var holeCount = tile.holes?.Length ?? 0;
                hash.Append(ref holeCount);
                for (int j = 0; j < holeCount; j++)
                    AppendPoints(ref hash, tile.holes[j].points);
            }
        }

        static void AppendPoints(ref Hash128 hash, Vector2Int[] points)
        {
            var count = points?.Length ?? 0;
            hash.Append(ref count);
            for (int i = 0; i < count; i++)
            {
                var point = points[i];
                hash.Append(ref point);
            }
        }

        public int NextTileId()
        {
            var next = 1;
            foreach (var tile in tiles)
                next = Math.Max(next, tile.id + 1);
            return next;
        }

        public int IndexOfTile(int id)
        {
            for (int i = 0; i < tiles.Count; i++)
            {
                if (tiles[i].id == id)
                    return i;
            }
            return -1;
        }

        [Serializable]
        sealed class JsonTile
        {
            public string     name;
            public int[]      points;
            public string[]   tags;
            public int        border;
            public JsonHole[] holes;
            // the name of the tile with its inside corner pieces
            public string     insideCorners;
        }

        [Serializable]
        sealed class JsonHole
        {
            public int[] points;
        }

        static Vector2Int[] PointsOf(int[] numbers)
        {
            var points = new Vector2Int[numbers.Length / 2];
            for (int i = 0; i < points.Length; i++)
                points[i] = new Vector2Int(numbers[i * 2], numbers[i * 2 + 1]);
            return points;
        }

        [Serializable]
        sealed class JsonLayout
        {
            public string     format;
            public int        width;
            public int        height;
            public string     origin;
            public JsonTile[] tiles;
        }

        public const string kJsonFormat = "chisel-hotspot-layout";

        // Reads a layout written as JSON; tile tags
        // "disabled", "radial", "repeat-u", "repeat-v", "rotate", "mirror", "inside-corners" and "inside-corner-turned"
        // set the tile's flags; "insideCorners" names the tile with its inside corner pieces. A tile's points may
        // be any outline, with "holes" (each its own points): a shaped tile. The cuts are found from the tiles when they
        // form a grid (BuildCuts).
        public static bool TryReadJson(string json, ChiselHotspotLayout layout, out string error)
        {
            JsonLayout read;
            try { read = JsonUtility.FromJson<JsonLayout>(json); }
            catch (Exception exception) { error = exception.Message; return false; }
            if (read == null || read.format != kJsonFormat) { error = $"not a {kJsonFormat} file"; return false; }
            if (read.origin != null && read.origin != "top-left") { error = $"origin '{read.origin}' is not supported"; return false; }
            if (read.width <= 0 || read.height <= 0) { error = "no size"; return false; }

            var tiles = new List<HotspotTile>();
            foreach (var tile in read.tiles ?? Array.Empty<JsonTile>())
            {
                if (tile.points == null || tile.points.Length < 6 || tile.points.Length % 2 != 0)
                {
                    error = $"tile '{tile.name}' has fewer than three points";
                    return false;
                }
                HotspotTileHole[] holes = null;
                if (tile.holes != null && tile.holes.Length > 0)
                {
                    holes = new HotspotTileHole[tile.holes.Length];
                    for (int h = 0; h < tile.holes.Length; h++)
                    {
                        if (tile.holes[h].points == null || tile.holes[h].points.Length < 6 || tile.holes[h].points.Length % 2 != 0)
                        {
                            error = $"a hole of tile '{tile.name}' has fewer than three points";
                            return false;
                        }
                        holes[h] = new HotspotTileHole { points = PointsOf(tile.holes[h].points) };
                    }
                }
                tiles.Add(new HotspotTile { id = tiles.Count + 1, name = tile.name, points = PointsOf(tile.points), holes = holes, flags = FlagsOf(tile.tags), border = Math.Max(0, tile.border) });
            }
            if (!LinkInsideCorners(tiles, Array.ConvertAll(read.tiles, tile => tile.insideCorners), out error))
                return false;
            layout.size  = new Vector2Int(read.width, read.height);
            layout.tiles = tiles;
            layout.BuildCuts(out _);
            error = null;
            return true;
        }

        // Each tile's inside corner pieces, by the name of their tile (links, one a tile; empty for none)
        static bool LinkInsideCorners(List<HotspotTile> tiles, IReadOnlyList<string> links, out string error)
        {
            error = null;
            for (int i = 0; i < tiles.Count; i++)
            {
                var link = links[i];
                if (string.IsNullOrEmpty(link))
                    continue;
                var linked = tiles.FindIndex(other => other.name == link);
                if (linked < 0) { error = $"tile '{tiles[i].name}': no tile '{link}' for its inside corners"; return false; }
                var tile = tiles[i];
                tile.insideCorners = tiles[linked].id;
                tiles[i] = tile;
            }
            return true;
        }

        static HotspotTileFlags FlagsOf(string[] tags)
        {
            var flags = HotspotTileFlags.None;
            foreach (var tag in tags ?? Array.Empty<string>())
            {
                switch (tag.ToLowerInvariant())
                {
                    case "disabled": flags |= HotspotTileFlags.Disabled; break;
                    case "radial":   flags |= HotspotTileFlags.Radial; break;
                    case "repeat-u": flags |= HotspotTileFlags.RepeatU; break;
                    case "repeat-v": flags |= HotspotTileFlags.RepeatV; break;
                    case "rotate":   flags |= HotspotTileFlags.AllowRotation; break;
                    case "mirror":   flags |= HotspotTileFlags.AllowMirror; break;
                    case "alt":
                    case "alternate": flags |= HotspotTileFlags.Alternate; break;
                    case "repeat-border": flags |= HotspotTileFlags.RepeatBorder; break;
                    case "repeat-partial": flags |= HotspotTileFlags.PartialRepeats; break;
                    case "inside-corners": flags |= HotspotTileFlags.InsideCorners; break;
                    case "inside-corner-turned": flags |= HotspotTileFlags.InsideCornerTurned; break;
                }
            }
            return flags;
        }
    }
}
