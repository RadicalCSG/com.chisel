using System.Collections.Generic;
using Chisel.Core;
using UnityEngine;

namespace Chisel.Editors
{
    // What kind of surface a tile is for, as the layout editor colours it, and the short tags its list shows
    enum HotspotTileKind { Square, Wide, Wider, Tall, Repeats, Radial, Shape }

    static class HotspotTileKinds
    {
        // longer than wide by this much (or the other way round) is no longer square
        const float kSquare = 1.5f;
        const float kWider  = 4;

        static readonly Color[] kColors =
        {
            new(0.29f, 0.45f, 0.67f),   // square
            new(0.35f, 0.58f, 0.36f),   // wide
            new(0.78f, 0.53f, 0.24f),   // 4:1 and wider
            new(0.53f, 0.36f, 0.62f),   // tall
            new(0.65f, 0.31f, 0.30f),   // repeats
            new(0.20f, 0.55f, 0.55f),   // radial
            new(0.70f, 0.58f, 0.24f),   // triangle or shape
        };

        static readonly string[] kNames = { "Square", "Wide", "4:1 and wider", "Tall", "Repeats", "Radial", "Triangle or shape" };

        public static IReadOnlyList<string> Names => kNames;

        public static Color ColorOf(HotspotTileKind kind) => kColors[(int)kind];
        public static string NameOf(HotspotTileKind kind) => kNames[(int)kind];

        public static Vector2Int SizeOf(in HotspotTile tile)
        {
            if (tile.points == null || tile.points.Length == 0)
                return Vector2Int.zero;
            var min = tile.points[0]; var max = tile.points[0];
            foreach (var point in tile.points) { min = Vector2Int.Min(min, point); max = Vector2Int.Max(max, point); }
            return max - min;
        }

        public static HotspotTileKind KindOf(in HotspotTile tile)
        {
            if ((tile.flags & HotspotTileFlags.Radial) != 0)
                return HotspotTileKind.Radial;
            if (tile.points == null || tile.points.Length == 3 || ChiselHotspotLayout.IsShaped(tile))
                return HotspotTileKind.Shape;
            if ((tile.flags & (HotspotTileFlags.RepeatU | HotspotTileFlags.RepeatV)) != 0)
                return HotspotTileKind.Repeats;
            var size = SizeOf(tile);
            if (size.x <= 0 || size.y <= 0)
                return HotspotTileKind.Square;
            var aspect = (float)size.x / size.y;
            if (aspect >= kWider)       return HotspotTileKind.Wider;
            if (aspect >= kSquare)      return HotspotTileKind.Wide;
            if (aspect <= 1 / kSquare)  return HotspotTileKind.Tall;
            return HotspotTileKind.Square;
        }

        // The short tags the tile list shows: pin only, radial, repeats, border, inside corners
        public static List<string> TagsOf(in HotspotTile tile)
        {
            var tags = new List<string>();
            if (HotspotTileChoice.IsPinOnly(tile))                                                tags.Add("pin only");
            if ((tile.flags & HotspotTileFlags.Radial) != 0)                                      tags.Add("radial");
            if ((tile.flags & (HotspotTileFlags.RepeatU | HotspotTileFlags.RepeatV)) != 0)        tags.Add("repeats");
            if (tile.border > 0)                                                                  tags.Add("border");
            if (HotspotTileChoice.IsPieces(tile))                                                 tags.Add("inside corners");
            return tags;
        }

        // Whether the list shows a tile for what's typed: its name, or one of its tags
        public static bool Matches(in HotspotTile tile, string filter)
        {
            if (string.IsNullOrWhiteSpace(filter))
                return true;
            if (HotspotTileChoice.NameMatches(tile.name, filter))
                return true;
            foreach (var tag in TagsOf(tile))
            {
                if (HotspotTileChoice.NameMatches(tag, filter))
                    return true;
            }
            return false;
        }

        // A label fits a tile when it is no wider and no higher than the tile's bounds on the canvas, less a margin
        public static bool LabelFits(Vector2 label, Rect tile, float margin = 2)
        {
            return label.x + 2 * margin <= tile.width && label.y + 2 * margin <= tile.height;
        }
    }
}
