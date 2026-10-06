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
        // Matching may turn the tile a quarter turn, or mirror it
        AllowRotation = 1 << 4,
        AllowMirror   = 1 << 5,
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
    }

    // The regions of a hotspot texture, and how many pixels of it a unit of surface takes
    [CreateAssetMenu(menuName = "Chisel/Hotspot Layout", fileName = "Hotspot Layout")]
    public sealed class ChiselHotspotLayout : ScriptableObject
    {
        public Vector2Int        size          = new(2048, 2048);
        public float             pixelsPerUnit = 256;
        public List<HotspotTile> tiles         = new();

        public int NextTileId()
        {
            var next = 1;
            foreach (var tile in tiles)
                next = Math.Max(next, tile.id + 1);
            return next;
        }

        [Serializable]
        sealed class JsonTile
        {
            public string   name;
            public int[]    points;
            public string[] tags;
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
        // "disabled", "radial", "repeat-u", "repeat-v", "rotate" and "mirror" set the tile's flags
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
                tiles.Add(new HotspotTile { id = tiles.Count + 1, name = tile.name, points = points, flags = FlagsOf(tile.tags) });
            }
            layout.size  = new Vector2Int(read.width, read.height);
            layout.tiles = tiles;
            error = null;
            return true;
        }

        static HotspotTileFlags FlagsOf(string[] tags)
        {
            var flags = HotspotTileFlags.None;
            foreach (var tag in tags ?? Array.Empty<string>())
            {
                switch (tag)
                {
                    case "disabled": flags |= HotspotTileFlags.Disabled; break;
                    case "radial":   flags |= HotspotTileFlags.Radial; break;
                    case "repeat-u": flags |= HotspotTileFlags.RepeatU; break;
                    case "repeat-v": flags |= HotspotTileFlags.RepeatV; break;
                    case "rotate":   flags |= HotspotTileFlags.AllowRotation; break;
                    case "mirror":   flags |= HotspotTileFlags.AllowMirror; break;
                }
            }
            return flags;
        }
    }
}
