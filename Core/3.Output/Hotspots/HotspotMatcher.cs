using System.Collections.Generic;
using Unity.Collections;
using Unity.Mathematics;

namespace Chisel.Core
{
    // A tile as matching sees it: corners in pixels with y up (origin at the bottom left), counter-clockwise (AddTiles)
    public struct HotspotTileData
    {
        public int              id;
        public int              cornerCount;    // 3 or 4
        public float2           p0, p1, p2, p3;
        public float2           min, max;       // its bounds in pixels
        public HotspotTileFlags flags;
        public float            pixelsPerUnit;
        // Width of its border in pixels, 0 for none
        public float            border;

        public readonly float2 Corner(int index)
        {
            switch (index)
            {
                case 0:  return p0;
                case 1:  return p1;
                case 2:  return p2;
                default: return p3;
            }
        }
    }

    public struct HotspotLayoutData
    {
        public float2 textureSize;
        public float  pixelsPerUnit;
        public int    tileStart;
        public int    tileCount;
    }

    // A surface as matching sees it, in its frame (HotspotMatcher.Frame), in the units of the tree
    public struct HotspotShape
    {
        public float2 min, max;
        // 3 when what is drawn is a triangle (its corners c0, c1, c2 counter-clockwise), otherwise 0
        public int    cornerCount;
        public float2 c0, c1, c2;
        // Round: at least kRadialCorners corners, all about as far from the middle of its bounds, which are square
        public bool   radial;
        // Its hull is its bounds
        public bool   boxed;

        public readonly float2 Corner(int index) { return index == 0 ? c0 : (index == 1 ? c1 : c2); }
    }

    public struct HotspotChoice
    {
        public int   tile;      // index into the tiles, -1 when nothing fits
        public int   corner;    // the tile corner the surface's first corner goes to
        public bool  mirrored;
        public bool  rotated;   // a rectangle turned a quarter turn
        public float score;     // lower is better: distortion of shape and of size
        public int   repeatX;   // how many times a repeating tile repeats across the surface's bounds (0 or 1: once)
        public int   repeatY;   // and up them
        public float2 lastRepeat; // how much of the last repeat across and up is used, with PartialRepeats (0: all of it)

        // More than one section: repeats, or a part of one
        public readonly bool Repeated => repeatX > 1 || repeatY > 1 || (lastRepeat.x > 0 && lastRepeat.x < 1) || (lastRepeat.y > 0 && lastRepeat.y < 1);
        public float scale;     // how many texture pixels a pixel of the surface (at the tile's density) takes
        public bool  lastResort; // repeated because no tile was large enough
    }

    public static class HotspotMatcher
    {
        // How much more a change of shape counts than a change of size
        public const float kShapeWeight = 2.0f;
        // A round surface has at least this many corners, its corners within this fraction of the same distance from
        // its middle, and its bounds within this fraction of square
        public const int    kRadialCorners   = 8;
        public const double kRadialTolerance = 0.1;
        // A tile magnified more than this is too small for the surface: then, as a last resort, a tile repeats at its own
        // density even when it is not meant to repeat (target capacity)
        public const float kLastResortScale = 1 / 3f;
        // As a last resort, fewest repeats first, then least stretch (the design's repeat rule): each repeat costs more
        // than any stretch the rounding of repeats leaves
        const float kLastResortRepeatCost = 100;

        // The texture's right and up on a surface: up follows the tree's up on walls and its +z on floors and ceilings,
        // and right is normal x up, so the texture reads the right way round from in front of the surface
        public static void Frame(float3 normal, out float3 right, out float3 up)
        {
            var n = math.normalize(normal);
            var a = math.abs(n);
            var axis = (a.y >= a.x && a.y >= a.z) ? new float3(0, 0, 1) : new float3(0, 1, 0);
            up    = math.normalize(axis - n * math.dot(n, axis));
            right = math.cross(n, up);
        }

        // The outline of what a surface draws: its bounds in the frame, and its corners when it is a triangle
        public static HotspotShape ShapeOf(NativeArray<float3> positions, float3 normal)
        {
            Frame(normal, out var right, out var up);
            var shape = new HotspotShape { min = new float2(float.PositiveInfinity), max = new float2(float.NegativeInfinity) };
            if (positions.Length == 0)
                return shape;
            var points = new NativeArray<double2>(positions.Length, Allocator.Temp);
            for (int i = 0; i < positions.Length; i++)
            {
                var p = (double3)positions[i];
                var q = new double2(math.dot((double3)right, p), math.dot((double3)up, p));
                points[i] = q;
                shape.min = math.min(shape.min, (float2)q);
                shape.max = math.max(shape.max, (float2)q);
            }

            // The convex hull, counter-clockwise; three corners left once points exactly on a line between two others go
            var hull = new NativeList<int>(positions.Length + 1, Allocator.Temp);
            ConvexHull(points, hull);
            var corners = new NativeList<int>(hull.Length, Allocator.Temp);
            for (int i = 0; i < hull.Length; i++)
            {
                var previous = positions[hull[(i + hull.Length - 1) % hull.Length]];
                var current  = positions[hull[i]];
                var next     = positions[hull[(i + 1) % hull.Length]];
                if (!OutputWeld.OnOneLine(previous, current, next))
                    corners.Add(hull[i]);
            }
            if (corners.Length >= kRadialCorners)
            {
                var middle = ((double2)shape.min + (double2)shape.max) * 0.5;
                var extent = (double2)shape.max - (double2)shape.min;
                double nearest = double.PositiveInfinity, farthest = 0;
                for (int i = 0; i < corners.Length; i++)
                {
                    var distance = math.length(points[corners[i]] - middle);
                    nearest  = math.min(nearest, distance);
                    farthest = math.max(farthest, distance);
                }
                shape.radial = nearest >= farthest * (1 - kRadialTolerance) &&
                               math.abs(extent.x - extent.y) <= math.max(extent.x, extent.y) * kRadialTolerance;
            }
            if (corners.Length == 4)
            {
                shape.boxed = true;
                for (int i = 0; i < 4; i++)
                {
                    var c = (float2)points[corners[i]];
                    shape.boxed &= (c.x == shape.min.x || c.x == shape.max.x) && (c.y == shape.min.y || c.y == shape.max.y);
                }
            }
            if (corners.Length == 3)
            {
                shape.cornerCount = 3;
                shape.c0 = (float2)points[corners[0]];
                shape.c1 = (float2)points[corners[1]];
                shape.c2 = (float2)points[corners[2]];
            }
            corners.Dispose();
            hull.Dispose();
            points.Dispose();
            return shape;
        }

        // Andrew's monotone chain; collinear points are left out
        static void ConvexHull(NativeArray<double2> points, NativeList<int> hull)
        {
            var order = new NativeArray<int>(points.Length, Allocator.Temp);
            for (int i = 0; i < order.Length; i++)
                order[i] = i;
            order.Sort(new ByPosition { points = points });
            hull.Clear();
            for (int pass = 0; pass < 2; pass++)
            {
                var start = hull.Length;
                for (int k = 0; k < order.Length; k++)
                {
                    var i = order[pass == 0 ? k : order.Length - 1 - k];
                    while (hull.Length >= start + 2 && Cross(points[hull[hull.Length - 2]], points[hull[hull.Length - 1]], points[i]) <= 0)
                        hull.RemoveAt(hull.Length - 1);
                    hull.Add(i);
                }
                hull.RemoveAt(hull.Length - 1);
            }
            order.Dispose();
        }

        struct ByPosition : IComparer<int>
        {
            public NativeArray<double2> points;
            public int Compare(int a, int b)
            {
                var p = points[a]; var q = points[b];
                if (p.x != q.x) return p.x < q.x ? -1 : 1;
                if (p.y != q.y) return p.y < q.y ? -1 : 1;
                return 0;
            }
        }

        static double Cross(double2 o, double2 a, double2 b) { return (a.x - o.x) * (b.y - o.y) - (a.y - o.y) * (b.x - o.x); }

        // The matching tile: the lowest score among the enabled tiles of the surface's kind (triangles for a triangle,
        // rectangles otherwise; radial tiles for round surfaces, the others when there are none). Tiles that score
        // exactly the same - identical tiles - are told apart by the seed. A pinned tile (its id) is always the one.
        public static HotspotChoice Choose(NativeArray<HotspotTileData> tiles, in HotspotLayoutData layout, in HotspotShape shape, uint seed, int pinnedId, bool radial = false)
        {
            var best = new HotspotChoice { tile = -1, score = float.PositiveInfinity };
            var size = shape.max - shape.min;
            if (!(size.x > 0) || !(size.y > 0))
                return best;

            if (pinnedId != 0)
            {
                for (int t = layout.tileStart; t < layout.tileStart + layout.tileCount; t++)
                {
                    if (tiles[t].id != pinnedId)
                        continue;
                    var pinned = Score(tiles[t], shape, true);
                    pinned.tile = t;
                    return pinned;
                }
            }

            var round = radial || shape.radial;
            best = Best(tiles, layout, shape, seed, round, false);
            if (round && best.tile < 0)
                best = Best(tiles, layout, shape, seed, false, false);
            if (best.tile >= 0 && best.scale < kLastResortScale && shape.cornerCount != 3)
            {
                // the best tile failed: the one that repeats fewest times instead
                var repeated = Best(tiles, layout, shape, seed, round, true);
                if (repeated.tile >= 0)
                {
                    best = repeated;
                    best.lastResort = true;
                }
            }
            return best;
        }

        static HotspotChoice Best(NativeArray<HotspotTileData> tiles, in HotspotLayoutData layout, in HotspotShape shape, uint seed, bool radial, bool lastResort)
        {
            var best = new HotspotChoice { tile = -1, score = float.PositiveInfinity };
            var ties = 0;
            for (int t = layout.tileStart; t < layout.tileStart + layout.tileCount; t++)
            {
                if (!Candidate(tiles[t], shape, radial))
                    continue;
                var choice = Score(tiles[t], shape, false, lastResort);
                if (choice.score < best.score) { best = choice; best.tile = t; ties = 1; }
                else if (choice.score == best.score) ties++;
            }
            if (ties <= 1)
                return best;

            // the seed picks one of the tiles that tie
            var pick = (int)(math.hash(new uint2(seed, 0x9E3779B9u)) % (uint)ties);
            for (int t = layout.tileStart; t < layout.tileStart + layout.tileCount; t++)
            {
                if (!Candidate(tiles[t], shape, radial))
                    continue;
                var choice = Score(tiles[t], shape, false, lastResort);
                if (choice.score != best.score)
                    continue;
                if (pick-- == 0) { choice.tile = t; return choice; }
            }
            return best;
        }

        static bool Candidate(in HotspotTileData tile, in HotspotShape shape, bool radial)
        {
            if ((tile.flags & (HotspotTileFlags.Disabled | HotspotTileFlags.Alternate)) != 0)
                return false;
            if (((tile.flags & HotspotTileFlags.Radial) != 0) != radial)
                return false;
            return (shape.cornerCount == 3) == (tile.cornerCount == 3);
        }

        // Distortion of the best way to put the surface on the tile: 2 |ln(s1 / s2)| + |ln(s1 s2)| / 2 for the scale
        // factors s1, s2 of the map. A tile that may not turn keeps the texture's up: a triangle goes corner to corner
        // the way that turns it least. A pinned tile may be turned or mirrored whatever its flags say.
        static HotspotChoice Score(in HotspotTileData tile, in HotspotShape shape, bool any, bool lastResort = false)
        {
            var pixelsPerUnit = tile.pixelsPerUnit;
            var best = new HotspotChoice { tile = -1, score = float.PositiveInfinity };
            var rotate = any || (tile.flags & HotspotTileFlags.AllowRotation) != 0;
            var mirror = any || (tile.flags & HotspotTileFlags.AllowMirror) != 0;
            if (shape.cornerCount != 3 || tile.cornerCount != 3)
            {
                // the surface's bounds onto the tile's bounds, or onto as many repeats of it as fit best
                var face = (shape.max - shape.min) * pixelsPerUnit;
                var size = tile.max - tile.min;
                var repeatU = lastResort || (tile.flags & HotspotTileFlags.RepeatU) != 0;
                var repeatV = lastResort || (tile.flags & HotspotTileFlags.RepeatV) != 0;
                var partial = (tile.flags & HotspotTileFlags.PartialRepeats) != 0;
                for (int quarter = 0; quarter < (rotate ? 2 : 1); quarter++)
                {
                    var target  = quarter == 0 ? size : size.yx;
                    Repeats(target, face, quarter == 0 ? repeatU : repeatV, quarter == 0 ? repeatV : repeatU, partial,
                            out var repeats, out var scales, out var last);
                    var score = Distortion(scales.x, scales.y) + (lastResort ? repeats.x * repeats.y * kLastResortRepeatCost : 0);
                    if (score < best.score)
                        best = new HotspotChoice { tile = -1, score = score, rotated = quarter == 1, repeatX = repeats.x, repeatY = repeats.y,
                                                   lastRepeat = last, scale = math.sqrt(scales.x * scales.y) };
                }
                return best;
            }

            // triangle onto triangle, corner to corner
            var f0 = shape.c0 * pixelsPerUnit; var f1 = shape.c1 * pixelsPerUnit; var f2 = shape.c2 * pixelsPerUnit;
            var faceEdges = new float2x2(f1 - f0, f2 - f0);
            if (math.determinant(faceEdges) == 0)
                return best;
            var inverse = math.inverse(faceEdges);
            var bestTurn = float.PositiveInfinity;
            for (int flip = 0; flip < (mirror ? 2 : 1); flip++)
            {
                for (int corner = 0; corner < 3; corner++)
                {
                    var t0 = tile.Corner(corner);
                    var t1 = tile.Corner(flip == 0 ? (corner + 1) % 3 : (corner + 2) % 3);
                    var t2 = tile.Corner(flip == 0 ? (corner + 2) % 3 : (corner + 1) % 3);
                    var map = math.mul(new float2x2(t1 - t0, t2 - t0), inverse);
                    SingularValues(map, out var s1, out var s2);
                    var score = Distortion(s1, s2);
                    var turn  = rotate ? 0 : Turn(flip == 0 ? map : math.mul(map, new float2x2(-1, 0, 0, 1)));
                    if (turn < bestTurn || (turn == bestTurn && score < best.score))
                    {
                        bestTurn = turn;
                        best = new HotspotChoice { tile = -1, score = score, corner = corner, mirrored = flip == 1, scale = math.sqrt(s1 * s2) };
                    }
                }
            }
            return best;
        }

        // How far a map turns: the angle of the rotation in its polar decomposition
        static float Turn(float2x2 m)
        {
            return math.abs(math.atan2(m.c0.y - m.c1.x, m.c0.x + m.c1.y));
        }

        // How many times a tile repeats across and up a surface (in pixels): along one axis as many as fit at the scale
        // that fits the other, along both as many as fit at the tile's own density. Whole repeats stretch to fit;
        // partial ones keep that scale and cut the last one off (last: how much of it is used). scales: tile pixels per
        // surface pixel across and up.
        static void Repeats(float2 tile, float2 face, bool across, bool up, bool partial, out int2 count, out float2 scales, out float2 last)
        {
            count  = new int2(1);
            scales = tile / face;
            last   = float2.zero;
            if (!across && !up)
                return;
            var scale = across && up ? 1.0 : (across ? (double)tile.y / face.y : (double)tile.x / face.x);
            var x = across ? (partial ? HotspotSections.PartialRepeats(face.x * scale, tile.x, out var lastX) : HotspotSections.Repeats(face.x * scale, tile.x)) : 1;
            var y = up     ? (partial ? HotspotSections.PartialRepeats(face.y * scale, tile.y, out var lastY) : HotspotSections.Repeats(face.y * scale, tile.y)) : 1;
            // too many: a sliver, stretched instead
            if (x == 0 || y == 0)
                return;
            count = new int2(x, y);
            if (across) scales.x = partial ? (float)scale : tile.x * x / face.x;
            if (up)     scales.y = partial ? (float)scale : tile.y * y / face.y;
            if (partial)
            {
                if (across) last.x = (float)(face.x * scale / tile.x - (x - 1));
                if (up)     last.y = (float)(face.y * scale / tile.y - (y - 1));
            }
        }

        static float Distortion(float s1, float s2)
        {
            if (!(s1 > 0) || !(s2 > 0))
                return float.PositiveInfinity;
            return kShapeWeight * math.abs(math.log(s1 / s2)) + 0.5f * math.abs(math.log(s1 * s2));
        }

        static void SingularValues(float2x2 m, out float s1, out float s2)
        {
            // of a 2x2 matrix, in closed form
            var a = m.c0.x; var b = m.c1.x; var c = m.c0.y; var d = m.c1.y;
            var e = (a + d) * 0.5f; var f = (a - d) * 0.5f; var g = (c + b) * 0.5f; var h = (c - b) * 0.5f;
            var q = math.sqrt(e * e + h * h);
            var r = math.sqrt(f * f + g * g);
            s1 = q + r;
            s2 = math.abs(q - r);
        }

        // The uv matrix for a surface on its tile: uv = (M * (position, 1)).xy for positions in the tree's space
        public static float4x4 Mapping(in HotspotTileData tile, in HotspotLayoutData layout, in HotspotShape shape, in HotspotChoice choice, float3 normal)
        {
            Frame(normal, out var right, out var up);
            Corners(tile, shape, choice, out _, out var f0, out var f1, out var f2, out var t0, out var t1, out var t2);
            // into uv
            t0 /= layout.textureSize; t1 /= layout.textureSize; t2 /= layout.textureSize;
            var map    = math.mul(new float2x2(t1 - t0, t2 - t0), math.inverse(new float2x2(f1 - f0, f2 - f0)));
            var offset = t0 - math.mul(map, f0);
            var rowU   = new float4(right * map.c0.x + up * map.c1.x, offset.x);
            var rowV   = new float4(right * map.c0.y + up * map.c1.y, offset.y);
            return math.transpose(new float4x4(rowU, rowV, float4.zero, float4.zero));
        }

        // Three corners of the surface in its frame and the tile corners (pixels) they go to: a triangle's corners, or
        // the bottom left, bottom right and top left of the surface's bounds. count is 3 or 4 (a rectangle, whose
        // fourth corner is f1 + f2 - f0).
        public static void Corners(in HotspotTileData tile, in HotspotShape shape, in HotspotChoice choice, out int count,
                                   out float2 f0, out float2 f1, out float2 f2, out float2 t0, out float2 t1, out float2 t2)
        {
            if (shape.cornerCount == 3 && tile.cornerCount == 3)
            {
                count = 3;
                f0 = shape.c0; f1 = shape.c1; f2 = shape.c2;
                var corner = choice.corner;
                t0 = tile.Corner(corner);
                t1 = tile.Corner(choice.mirrored ? (corner + 2) % 3 : (corner + 1) % 3);
                t2 = tile.Corner(choice.mirrored ? (corner + 1) % 3 : (corner + 2) % 3);
            } else
            {
                count = 4;
                f0 = shape.min; f1 = new float2(shape.max.x, shape.min.y); f2 = new float2(shape.min.x, shape.max.y);
                var lo = tile.min; var hi = tile.max;
                if (!choice.rotated) { t0 = lo; t1 = new float2(hi.x, lo.y); t2 = new float2(lo.x, hi.y); }
                else                 { t0 = new float2(hi.x, lo.y); t1 = hi; t2 = lo; }
            }
        }

        // A layout's tiles as matching sees them
        public static void AddTiles(ChiselHotspotLayout layout, NativeList<HotspotTileData> tiles, out HotspotLayoutData data)
        {
            data = new HotspotLayoutData
            {
                textureSize   = new float2(layout.size.x, layout.size.y),
                pixelsPerUnit = layout.pixelsPerUnit,
                tileStart     = tiles.Length
            };
            foreach (var tile in layout.tiles)
            {
                if (tile.points == null || (tile.points.Length != 3 && tile.points.Length != 4))
                    continue;
                var t = new HotspotTileData { id = tile.id, cornerCount = tile.points.Length, flags = tile.flags,
                                              pixelsPerUnit = tile.pixelsPerUnit > 0 ? tile.pixelsPerUnit : layout.pixelsPerUnit,
                                              border = math.max(0, tile.border),
                                              min = new float2(float.PositiveInfinity), max = new float2(float.NegativeInfinity) };
                var count = tile.points.Length;
                var area  = 0.0f;
                for (int i = 0; i < count; i++)
                {
                    var a = tile.points[i]; var b = tile.points[(i + 1) % count];
                    area += (float)a.x * (layout.size.y - b.y) - (float)b.x * (layout.size.y - a.y);
                }
                for (int i = 0; i < count; i++)
                {
                    // y up, and counter-clockwise whichever way round the corners were given
                    var point = tile.points[area >= 0 ? i : count - 1 - i];
                    var p = new float2(point.x, layout.size.y - point.y);
                    switch (i) { case 0: t.p0 = p; break; case 1: t.p1 = p; break; case 2: t.p2 = p; break; default: t.p3 = p; break; }
                    t.min = math.min(t.min, p);
                    t.max = math.max(t.max, p);
                }
                tiles.Add(t);
            }
            data.tileCount = tiles.Length - data.tileStart;
        }
    }
}
