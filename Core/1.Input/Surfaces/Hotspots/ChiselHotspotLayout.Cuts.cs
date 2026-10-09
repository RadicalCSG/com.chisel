using System;
using System.Collections.Generic;
using UnityEngine;

namespace Chisel.Core
{
    // A line of the layout's cuts, in pixels from the top left
    public struct HotspotLine
    {
        public int        node;
        public Vector2Int a, b;
    }

    // Editing the layout as lines: tiles are split by new lines, lines move and are removed again. The tiles' points
    // follow from the cuts (UpdateTiles). Tiles no line makes (shapes drawn or imported over them) keep their points.
    public sealed partial class ChiselHotspotLayout
    {
        public bool HasCuts => cuts.Count > 0;

        // One tile over the whole texture
        public void ResetCuts()
        {
            tiles.Clear();
            tiles.Add(new HotspotTile { id = 1, name = "T1" });
            cuts.Clear();
            cuts.Add(new HotspotCut { kind = HotspotCutKind.Tile, tile = 1 });
            UpdateTiles();
        }

        static bool IsTriangleCut(HotspotCutKind kind)
        {
            return kind == HotspotCutKind.Slash || kind == HotspotCutKind.Backslash || kind == HotspotCutKind.Apex;
        }

        // Each node's region, and its parent (-1 for the root and for nodes no longer in the tree)
        public void GetRegions(List<RectInt> regions, List<int> parents)
        {
            regions.Clear();
            parents.Clear();
            for (int i = 0; i < cuts.Count; i++)
            {
                regions.Add(default);
                parents.Add(-1);
            }
            if (cuts.Count == 0)
                return;
            regions[0] = new RectInt(0, 0, size.x, size.y);
            var stack = new Stack<int>();
            stack.Push(0);
            while (stack.Count > 0)
            {
                var node = stack.Pop();
                var cut  = cuts[node];
                var r    = regions[node];
                switch (cut.kind)
                {
                    case HotspotCutKind.Tile:
                        continue;
                    case HotspotCutKind.Vertical:
                        regions[cut.first]  = new RectInt(r.x, r.y, cut.position - r.x, r.height);
                        regions[cut.second] = new RectInt(cut.position, r.y, r.xMax - cut.position, r.height);
                        break;
                    case HotspotCutKind.Horizontal:
                        regions[cut.first]  = new RectInt(r.x, r.y, r.width, cut.position - r.y);
                        regions[cut.second] = new RectInt(r.x, cut.position, r.width, r.yMax - cut.position);
                        break;
                    default:
                        regions[cut.first] = regions[cut.second] = r;
                        if (cut.kind == HotspotCutKind.Apex)
                            regions[cut.third] = r;
                        break;
                }
                parents[cut.first] = parents[cut.second] = node;
                stack.Push(cut.first);
                stack.Push(cut.second);
                if (cut.kind == HotspotCutKind.Apex)
                {
                    parents[cut.third] = node;
                    stack.Push(cut.third);
                }
            }
        }

        static Vector2Int[] RectanglePoints(RectInt r)
        {
            return new[] { new Vector2Int(r.xMin, r.yMin), new Vector2Int(r.xMax, r.yMin), new Vector2Int(r.xMax, r.yMax), new Vector2Int(r.xMin, r.yMax) };
        }

        // The triangles a diagonal or an apex cut makes of a region, clockwise as the texture shows them
        static Vector2Int[][] TrianglePoints(HotspotCutKind kind, RectInt r, int position)
        {
            var tl = new Vector2Int(r.xMin, r.yMin); var tr = new Vector2Int(r.xMax, r.yMin);
            var br = new Vector2Int(r.xMax, r.yMax); var bl = new Vector2Int(r.xMin, r.yMax);
            switch (kind)
            {
                case HotspotCutKind.Slash:     return new[] { new[] { tl, tr, bl }, new[] { tr, br, bl } };
                case HotspotCutKind.Backslash: return new[] { new[] { tl, tr, br }, new[] { tl, br, bl } };
                default:
                {
                    var apex = new Vector2Int(position, r.yMin);
                    return new[] { new[] { tl, apex, bl }, new[] { apex, br, bl }, new[] { apex, tr, br } };
                }
            }
        }

        static int ChildIndex(in HotspotCut parent, int node)
        {
            return node == parent.first ? 0 : (node == parent.second ? 1 : 2);
        }

        // The tiles' points from the cuts; a tile node without a tile gets a new one
        public void UpdateTiles()
        {
            if (cuts.Count == 0)
                return;
            var regions = new List<RectInt>();
            var parents = new List<int>();
            GetRegions(regions, parents);
            var used = new HashSet<int>();
            for (int node = 0; node < cuts.Count; node++)
            {
                var cut = cuts[node];
                if (cut.kind != HotspotCutKind.Tile || (node != 0 && parents[node] < 0))
                    continue;
                var index = IndexOfTile(cut.tile);
                if (index < 0 || used.Contains(cut.tile))
                {
                    var id = NextTileId();
                    tiles.Add(new HotspotTile { id = id, name = "T" + id });
                    cut.tile = id;
                    cuts[node] = cut;
                    index = tiles.Count - 1;
                }
                used.Add(cut.tile);
                var parent = parents[node];
                var tile = tiles[index];
                tile.points = parent >= 0 && IsTriangleCut(cuts[parent].kind)
                            ? TrianglePoints(cuts[parent].kind, regions[parent], cuts[parent].position)[ChildIndex(cuts[parent], node)]
                            : RectanglePoints(regions[node]);
                tiles[index] = tile;
            }
        }

        // Not a rectangle with upright sides or a triangle: an outline of more corners, or with holes
        public static bool IsShaped(in HotspotTile tile)
        {
            if (tile.holes != null && tile.holes.Length > 0)
                return true;
            if (tile.points == null || tile.points.Length == 3)
                return false;
            if (tile.points.Length > 4)
                return true;
            for (int i = 0; i < 4; i++)
            {
                var a = tile.points[i]; var b = tile.points[(i + 1) % 4];
                if (a.x != b.x && a.y != b.y)
                    return true;
            }
            return false;
        }

        // The tiles the cuts make
        public HashSet<int> TilesOfCuts()
        {
            var owned = new HashSet<int>();
            if (cuts.Count == 0)
                return owned;
            var regions = new List<RectInt>();
            var parents = new List<int>();
            GetRegions(regions, parents);
            for (int node = 0; node < cuts.Count; node++)
            {
                if (cuts[node].kind == HotspotCutKind.Tile && (node == 0 || parents[node] >= 0))
                    owned.Add(cuts[node].tile);
            }
            return owned;
        }

        // A tile drawn by its outline: no line of the cuts makes it
        public bool IsDrawn(int id)
        {
            return IndexOfTile(id) >= 0 && !TilesOfCuts().Contains(id);
        }

        // A new tile of this outline (pixels, any winding) over the others; its id, 0 when it has fewer than 3 corners
        public int AddShape(Vector2Int[] points)
        {
            if (points == null || points.Length < 3 || DoubledArea(points) == 0)
                return 0;
            var id = NextTileId();
            tiles.Add(new HotspotTile { id = id, name = "S" + id, points = (Vector2Int[])points.Clone() });
            return id;
        }

        // A hole in a drawn tile
        public bool AddHole(int id, Vector2Int[] points)
        {
            var index = IndexOfTile(id);
            if (index < 0 || !IsDrawn(id) || points == null || points.Length < 3 || DoubledArea(points) == 0)
                return false;
            var tile = tiles[index];
            var holes = new List<HotspotTileHole>(tile.holes ?? Array.Empty<HotspotTileHole>());
            holes.Add(new HotspotTileHole { points = (Vector2Int[])points.Clone() });
            tile.holes = holes.ToArray();
            tiles[index] = tile;
            return true;
        }

        // Removes a drawn tile; the cuts' tiles go with their lines
        public bool RemoveShape(int id)
        {
            if (!IsDrawn(id))
                return false;
            tiles.RemoveAt(IndexOfTile(id));
            return true;
        }

        // A new texture size; the lines and tiles keep where they are on the texture
        public void Resize(Vector2Int newSize)
        {
            if (newSize.x <= 0 || newSize.y <= 0 || newSize == size)
                return;
            var old = size;
            int X(int x) => (int)Math.Round((double)x * newSize.x / old.x);
            int Y(int y) => (int)Math.Round((double)y * newSize.y / old.y);
            Vector2Int[] Points(Vector2Int[] points) => points == null ? null : Array.ConvertAll(points, p => new Vector2Int(X(p.x), Y(p.y)));
            HotspotTile Scaled(HotspotTile tile)
            {
                tile.points = Points(tile.points);
                if (tile.holes != null)
                    tile.holes = Array.ConvertAll(tile.holes, h => new HotspotTileHole { points = Points(h.points) });
                return tile;
            }
            size = newSize;
            if (cuts.Count == 0)
            {
                for (int i = 0; i < tiles.Count; i++)
                {
                    var tile = tiles[i];
                    if (tile.points == null)
                        continue;
                    tiles[i] = Scaled(tile);
                }
                return;
            }
            var owned = TilesOfCuts();
            for (int i = 0; i < tiles.Count; i++)
            {
                if (tiles[i].points != null && !owned.Contains(tiles[i].id))
                    tiles[i] = Scaled(tiles[i]);
            }
            for (int i = 0; i < cuts.Count; i++)
            {
                var cut = cuts[i];
                if (cut.kind == HotspotCutKind.Vertical || cut.kind == HotspotCutKind.Apex) cut.position = X(cut.position);
                else if (cut.kind == HotspotCutKind.Horizontal)                             cut.position = Y(cut.position);
                cuts[i] = cut;
            }
            UpdateTiles();
        }

        public int NodeOfTile(int id)
        {
            for (int node = 0; node < cuts.Count; node++)
            {
                if (cuts[node].kind == HotspotCutKind.Tile && cuts[node].tile == id)
                    return node;
            }
            return -1;
        }

        public int ParentOf(int node)
        {
            for (int i = 0; i < cuts.Count; i++)
            {
                var cut = cuts[i];
                if (cut.kind != HotspotCutKind.Tile && (cut.first == node || cut.second == node || (cut.kind == HotspotCutKind.Apex && cut.third == node)))
                    return i;
            }
            return -1;
        }

        // Splits a tile with a new line; the first part keeps the tile, the others are new tiles like it.
        // A position is the line's x (Vertical, Apex) or y (Horizontal), strictly inside the tile; diagonals take none.
        public bool Split(int tileId, HotspotCutKind kind, int position)
        {
            var node = NodeOfTile(tileId);
            if (node < 0 || kind == HotspotCutKind.Tile)
                return false;
            var regions = new List<RectInt>();
            var parents = new List<int>();
            GetRegions(regions, parents);
            if (parents[node] >= 0 && IsTriangleCut(cuts[parents[node]].kind))
                return false;
            var r = regions[node];
            switch (kind)
            {
                case HotspotCutKind.Vertical:
                case HotspotCutKind.Apex:       if (position <= r.xMin || position >= r.xMax) return false; break;
                case HotspotCutKind.Horizontal: if (position <= r.yMin || position >= r.yMax) return false; break;
                default:                        position = 0; break;
            }
            var original = tiles[IndexOfTile(tileId)];
            int Child(int id)
            {
                cuts.Add(new HotspotCut { kind = HotspotCutKind.Tile, tile = id });
                return cuts.Count - 1;
            }
            int Copy()
            {
                var id = NextTileId();
                tiles.Add(new HotspotTile { id = id, name = "T" + id, flags = original.flags, border = original.border, pixelsPerUnit = original.pixelsPerUnit });
                return Child(id);
            }
            var split = new HotspotCut { kind = kind, position = position, first = Child(tileId) };
            split.second = Copy();
            if (kind == HotspotCutKind.Apex)
                split.third = Copy();
            cuts[node] = split;
            UpdateTiles();
            return true;
        }

        // Where a line can go without crossing the lines inside the tiles beside it; false for lines that do not move
        public bool MoveRange(int node, out int min, out int max)
        {
            min = max = 0;
            if (node < 0 || node >= cuts.Count)
                return false;
            var cut = cuts[node];
            if (cut.kind != HotspotCutKind.Vertical && cut.kind != HotspotCutKind.Horizontal && cut.kind != HotspotCutKind.Apex)
                return false;
            var regions = new List<RectInt>();
            var parents = new List<int>();
            GetRegions(regions, parents);
            var r = regions[node];
            var vertical = cut.kind != HotspotCutKind.Horizontal;
            min = (vertical ? r.xMin : r.yMin) + 1;
            max = (vertical ? r.xMax : r.yMax) - 1;
            if (cut.kind == HotspotCutKind.Apex)
                return min <= max;
            var stack = new Stack<(int node, bool first)>();
            stack.Push((cut.first, true));
            stack.Push((cut.second, false));
            while (stack.Count > 0)
            {
                var (n, first) = stack.Pop();
                var inner = cuts[n];
                if (inner.kind == HotspotCutKind.Tile)
                    continue;
                if ((vertical && (inner.kind == HotspotCutKind.Vertical || inner.kind == HotspotCutKind.Apex)) ||
                    (!vertical && inner.kind == HotspotCutKind.Horizontal))
                {
                    if (first) min = Math.Max(min, inner.position + 1);
                    else       max = Math.Min(max, inner.position - 1);
                }
                stack.Push((inner.first, first));
                stack.Push((inner.second, first));
                if (inner.kind == HotspotCutKind.Apex)
                    stack.Push((inner.third, first));
            }
            return min <= max;
        }

        // Moves a line as far toward position as it can go
        public bool MoveCut(int node, int position)
        {
            if (!MoveRange(node, out var min, out var max))
                return false;
            var cut = cuts[node];
            cut.position = Math.Clamp(position, min, max);
            cuts[node] = cut;
            UpdateTiles();
            return true;
        }

        static long DoubledArea(Vector2Int[] points)
        {
            long area = 0;
            for (int i = 0; i < points.Length; i++)
            {
                var a = points[i]; var b = points[(i + 1) % points.Length];
                area += (long)a.x * b.y - (long)b.x * a.y;
            }
            return Math.Abs(area);
        }

        // Removes a line and every line inside the tiles beside it: they become one tile, the largest of them
        public bool RemoveCut(int node)
        {
            if (node < 0 || node >= cuts.Count || cuts[node].kind == HotspotCutKind.Tile)
                return false;
            var keep = -1;
            var keepArea = -1L;
            var stack = new Stack<int>();
            stack.Push(node);
            var removed = new HashSet<int>();
            while (stack.Count > 0)
            {
                var cut = cuts[stack.Pop()];
                if (cut.kind == HotspotCutKind.Tile)
                {
                    removed.Add(cut.tile);
                    var index = IndexOfTile(cut.tile);
                    var area = index < 0 ? 0 : DoubledArea(tiles[index].points);
                    if (area > keepArea) { keepArea = area; keep = cut.tile; }
                    continue;
                }
                stack.Push(cut.first);
                stack.Push(cut.second);
                if (cut.kind == HotspotCutKind.Apex)
                    stack.Push(cut.third);
            }
            removed.Remove(keep);
            tiles.RemoveAll(t => removed.Contains(t.id));
            cuts[node] = new HotspotCut { kind = HotspotCutKind.Tile, tile = keep };
            Compact();
            UpdateTiles();
            return true;
        }

        // Drops the nodes no longer in the tree
        void Compact()
        {
            var compacted = new List<HotspotCut>();
            int Copy(int node)
            {
                var index = compacted.Count;
                var cut = cuts[node];
                compacted.Add(cut);
                if (cut.kind != HotspotCutKind.Tile)
                {
                    cut.first  = Copy(cut.first);
                    cut.second = Copy(cut.second);
                    if (cut.kind == HotspotCutKind.Apex)
                        cut.third = Copy(cut.third);
                    compacted[index] = cut;
                }
                return index;
            }
            if (cuts.Count > 0)
                Copy(0);
            cuts = compacted;
        }

        static bool Inside(Vector2Int[] triangle, Vector2 p)
        {
            // clockwise on screen with y down: the same side of all three edges
            var sign = 0;
            for (int i = 0; i < 3; i++)
            {
                var a = triangle[i]; var b = triangle[(i + 1) % 3];
                var cross = (b.x - a.x) * (p.y - a.y) - (b.y - a.y) * (p.x - a.x);
                var s = cross > 0 ? 1 : (cross < 0 ? -1 : 0);
                if (s == 0) continue;
                if (sign == 0) sign = s;
                else if (s != sign) return false;
            }
            return true;
        }

        // Inside a loop of any shape (even-odd)
        static bool InsideLoop(Vector2Int[] loop, Vector2 p)
        {
            var inside = false;
            for (int i = 0, j = loop.Length - 1; i < loop.Length; j = i++)
            {
                Vector2 a = loop[i], b = loop[j];
                if ((a.y > p.y) != (b.y > p.y) && p.x < a.x + (p.y - a.y) * (b.x - a.x) / (b.y - a.y))
                    inside = !inside;
            }
            return inside;
        }

        static bool InsideShape(in HotspotTile tile, Vector2 p)
        {
            if (!InsideLoop(tile.points, p))
                return false;
            if (tile.holes != null)
            {
                foreach (var hole in tile.holes)
                {
                    if (hole.points != null && hole.points.Length >= 3 && InsideLoop(hole.points, p))
                        return false;
                }
            }
            return true;
        }

        // The tile at a pixel position, 0 for none: a drawn tile over the cuts' (the last drawn on top)
        public int TileAt(Vector2 pixel)
        {
            var owned = TilesOfCuts();
            for (int i = tiles.Count - 1; i >= 0; i--)
            {
                var tile = tiles[i];
                if (tile.points == null || tile.points.Length < 3 || owned.Contains(tile.id))
                    continue;
                var b = BoundsOf(tile.points);
                if (IsShaped(tile) || tile.points.Length == 3 ? InsideShape(tile, pixel)
                                                              : (pixel.x >= b.xMin && pixel.x <= b.xMax && pixel.y >= b.yMin && pixel.y <= b.yMax))
                    return tile.id;
            }
            if (cuts.Count == 0)
                return 0;
            if (pixel.x < 0 || pixel.y < 0 || pixel.x > size.x || pixel.y > size.y)
                return 0;
            var regions = new List<RectInt>();
            var parents = new List<int>();
            GetRegions(regions, parents);
            var node = 0;
            while (true)
            {
                var cut = cuts[node];
                switch (cut.kind)
                {
                    case HotspotCutKind.Tile:       return cut.tile;
                    case HotspotCutKind.Vertical:   node = pixel.x < cut.position ? cut.first : cut.second; break;
                    case HotspotCutKind.Horizontal: node = pixel.y < cut.position ? cut.first : cut.second; break;
                    default:
                    {
                        var triangles = TrianglePoints(cut.kind, regions[node], cut.position);
                        var children  = new[] { cut.first, cut.second, cut.third };
                        for (int i = 0; i < triangles.Length; i++)
                        {
                            if (Inside(triangles[i], pixel))
                                return cuts[children[i]].tile;
                        }
                        return 0;
                    }
                }
            }
        }

        // Every line of the cuts
        public void GetLines(List<HotspotLine> lines)
        {
            lines.Clear();
            var regions = new List<RectInt>();
            var parents = new List<int>();
            GetRegions(regions, parents);
            for (int node = 0; node < cuts.Count; node++)
            {
                if (node != 0 && parents[node] < 0)
                    continue;
                var cut = cuts[node];
                var r   = regions[node];
                switch (cut.kind)
                {
                    case HotspotCutKind.Vertical:   lines.Add(new HotspotLine { node = node, a = new Vector2Int(cut.position, r.yMin), b = new Vector2Int(cut.position, r.yMax) }); break;
                    case HotspotCutKind.Horizontal: lines.Add(new HotspotLine { node = node, a = new Vector2Int(r.xMin, cut.position), b = new Vector2Int(r.xMax, cut.position) }); break;
                    case HotspotCutKind.Slash:      lines.Add(new HotspotLine { node = node, a = new Vector2Int(r.xMin, r.yMax), b = new Vector2Int(r.xMax, r.yMin) }); break;
                    case HotspotCutKind.Backslash:  lines.Add(new HotspotLine { node = node, a = new Vector2Int(r.xMin, r.yMin), b = new Vector2Int(r.xMax, r.yMax) }); break;
                    case HotspotCutKind.Apex:
                        lines.Add(new HotspotLine { node = node, a = new Vector2Int(r.xMin, r.yMax), b = new Vector2Int(cut.position, r.yMin) });
                        lines.Add(new HotspotLine { node = node, a = new Vector2Int(cut.position, r.yMin), b = new Vector2Int(r.xMax, r.yMax) });
                        break;
                }
            }
        }

        static RectInt BoundsOf(Vector2Int[] points)
        {
            int x0 = int.MaxValue, y0 = int.MaxValue, x1 = int.MinValue, y1 = int.MinValue;
            foreach (var p in points)
            {
                x0 = Math.Min(x0, p.x); y0 = Math.Min(y0, p.y);
                x1 = Math.Max(x1, p.x); y1 = Math.Max(y1, p.y);
            }
            return new RectInt(x0, y0, x1 - x0, y1 - y0);
        }

        static bool SamePoints(Vector2Int[] a, Vector2Int[] b)
        {
            if (a.Length != b.Length)
                return false;
            foreach (var p in a)
            {
                if (Array.IndexOf(b, p) < 0)
                    return false;
            }
            return true;
        }

        // Finds the cuts of tiles that were given without them: straight lines across, diagonals and apexes. Uncovered
        // parts become disabled tiles. False, with no cuts, when the tiles are not such a grid.
        public bool BuildCuts(out string error)
        {
            var built  = new List<HotspotCut>();
            var added  = new List<HotspotTile>();
            var nextId = NextTileId();
            var failed = (string)null;
            var bounds = new RectInt[tiles.Count];
            for (int i = 0; i < tiles.Count; i++)
                bounds[i] = tiles[i].points == null ? default : BoundsOf(tiles[i].points);

            int Leaf(int node, int id)
            {
                built[node] = new HotspotCut { kind = HotspotCutKind.Tile, tile = id };
                return node;
            }

            int Build(RectInt r, List<int> inside)
            {
                var node = built.Count;
                built.Add(default);
                if (inside.Count == 0)
                {
                    var id = nextId++;
                    added.Add(new HotspotTile { id = id, name = "T" + id, flags = HotspotTileFlags.Disabled, points = RectanglePoints(r) });
                    return Leaf(node, id);
                }
                if (inside.Count == 1 && tiles[inside[0]].points.Length == 4 && bounds[inside[0]].Equals(r))
                    return Leaf(node, tiles[inside[0]].id);

                if (inside.TrueForAll(i => tiles[i].points.Length == 3) && (inside.Count == 2 || inside.Count == 3))
                {
                    var tries = new List<(HotspotCutKind kind, int position)>();
                    if (inside.Count == 2)
                    {
                        tries.Add((HotspotCutKind.Slash, 0));
                        tries.Add((HotspotCutKind.Backslash, 0));
                    } else
                    {
                        foreach (var i in inside)
                        {
                            foreach (var p in tiles[i].points)
                            {
                                if (p.y == r.yMin && p.x > r.xMin && p.x < r.xMax)
                                    tries.Add((HotspotCutKind.Apex, p.x));
                            }
                        }
                    }
                    foreach (var (kind, position) in tries)
                    {
                        var triangles = TrianglePoints(kind, r, position);
                        var ids = new int[triangles.Length];
                        var all = true;
                        for (int k = 0; k < triangles.Length && all; k++)
                        {
                            var match = inside.FindIndex(i => SamePoints(tiles[i].points, triangles[k]));
                            all = match >= 0;
                            if (all)
                                ids[k] = tiles[inside[match]].id;
                        }
                        if (!all)
                            continue;
                        var cut = new HotspotCut { kind = kind, position = position };
                        for (int k = 0; k < ids.Length; k++)
                        {
                            var child = built.Count;
                            built.Add(default);
                            Leaf(child, ids[k]);
                            if (k == 0) cut.first = child; else if (k == 1) cut.second = child; else cut.third = child;
                        }
                        built[node] = cut;
                        return node;
                    }
                }

                for (int axis = 0; axis < 2; axis++)
                {
                    var vertical = axis == 0;
                    var lines = new SortedSet<int>();
                    foreach (var i in inside)
                    {
                        var b = bounds[i];
                        foreach (var c in vertical ? new[] { b.xMin, b.xMax } : new[] { b.yMin, b.yMax })
                        {
                            if (c > (vertical ? r.xMin : r.yMin) && c < (vertical ? r.xMax : r.yMax))
                                lines.Add(c);
                        }
                    }
                    foreach (var c in lines)
                    {
                        var crossed = inside.Exists(i => vertical ? (bounds[i].xMin < c && bounds[i].xMax > c) : (bounds[i].yMin < c && bounds[i].yMax > c));
                        if (crossed)
                            continue;
                        var before = inside.FindAll(i => vertical ? bounds[i].xMax <= c : bounds[i].yMax <= c);
                        var after  = inside.FindAll(i => vertical ? bounds[i].xMin >= c : bounds[i].yMin >= c);
                        var cut = new HotspotCut { kind = vertical ? HotspotCutKind.Vertical : HotspotCutKind.Horizontal, position = c };
                        cut.first = Build(vertical ? new RectInt(r.x, r.y, c - r.x, r.height) : new RectInt(r.x, r.y, r.width, c - r.y), before);
                        if (cut.first < 0)
                            return -1;
                        cut.second = Build(vertical ? new RectInt(c, r.y, r.xMax - c, r.height) : new RectInt(r.x, c, r.width, r.yMax - c), after);
                        if (cut.second < 0)
                            return -1;
                        built[node] = cut;
                        return node;
                    }
                }
                failed = $"the tiles in ({r.x}, {r.y}, {r.width} x {r.height}) are not divided by straight lines across";
                return -1;
            }

            // shaped tiles are not part of the grid: they stay over it as they are
            var everything = new List<int>();
            var shapes = 0;
            for (int i = 0; i < tiles.Count; i++)
            {
                if (tiles[i].points != null && tiles[i].points.Length >= 3 && IsShaped(tiles[i]))
                    shapes++;
                else if (tiles[i].points != null && (tiles[i].points.Length == 3 || tiles[i].points.Length == 4))
                    everything.Add(i);
            }
            if (everything.Count + shapes != tiles.Count)
                failed = "a tile is not a triangle or a rectangle";
            else if (everything.Count == 0)
                failed = "there are no rectangles or triangles to find lines between";
            else if (Build(new RectInt(0, 0, size.x, size.y), everything) >= 0)
            {
                tiles.AddRange(added);
                cuts = built;
                UpdateTiles();
                error = null;
                return true;
            }
            cuts.Clear();
            error = failed;
            return false;
        }
    }
}
