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
    }

    // One region of a hotspot texture: a rectangle or a triangle
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
        public List<HotspotTile> tiles         = new();
        // Empty when the tiles were given without them (an import that is not a grid)
        public List<HotspotCut>  cuts          = new();

        public event Action Changed;

        void OnValidate()
        {
            Changed?.Invoke();
            ChiselHotspotStore.LayoutChanged(this);
        }

        // To call after an edit
        public void NotifyChanged() { OnValidate(); }

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
            public string   name;
            public int[]    points;
            public string[] tags;
            public int      border;
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
        // "disabled", "radial", "repeat-u", "repeat-v", "rotate" and "mirror" set the tile's flags. The cuts are found
        // from the tiles when they form a grid (BuildCuts).
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
                if (tile.points == null || (tile.points.Length != 6 && tile.points.Length != 8))
                {
                    error = $"tile '{tile.name}' is not a triangle or a rectangle";
                    return false;
                }
                var points = new Vector2Int[tile.points.Length / 2];
                for (int i = 0; i < points.Length; i++)
                    points[i] = new Vector2Int(tile.points[i * 2], tile.points[i * 2 + 1]);
                tiles.Add(new HotspotTile { id = tiles.Count + 1, name = tile.name, points = points, flags = FlagsOf(tile.tags), border = Math.Max(0, tile.border) });
            }
            layout.size  = new Vector2Int(read.width, read.height);
            layout.tiles = tiles;
            layout.BuildCuts(out _);
            error = null;
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
                }
            }
            return flags;
        }
    }
}
