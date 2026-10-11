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
        // The border along each side (from corner i to the next, HotspotBorderMode, maximum stretch)
        public int4             sideMode;
        public float4           sideStretch;
        // A shaped tile (not a rectangle or a triangle; with holes): taken for surfaces whose outline is like its own
        // (mask, kMaskSize x kMaskSize cells over its bounds), mapped by its bounds (its corners), its border measured
        // from its own sides (sideStart, sideCount: in the layouts' sides, ChiselHotspotStore.Gather)
        public bool             shaped;
        public uint4            mask0, mask1;
        public int              sideStart, sideCount;
        // The bounds in pixels of its inside corner pieces (HotspotTile.insideCorners; none when not above insideMin), and
        // whether that is one piece turned (InsideCornerTurned) rather than four
        public float2           insideMin, insideMax;
        public bool             insideOne;
        // The sheet it is on (ChiselHotspotStore.Gather): 0, the layout's own; otherwise the layouts' entry (index + 1) of
        // another sheet drawn with it, whose texture size, padding and material it takes (SheetOf)
        public int              sheet;
        // and its place in the material's other sheets (ChiselSurfaceMetadata.hotspotSheets, index + 1; 0: its own), what
        // a pin names (HotspotSurface.pinnedSheet)
        public int              sheetIndex;

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
        // budgets for one surface, the layout's or the project's (ChiselHotspotLayout.maxRepeats, maxTriangles)
        public int    maxRepeats, maxTriangles;
        // pixels left out along the tiles' edges (ChiselHotspotLayout.padding), and along a bordered tile's interior where
        // it meets the border (interiorPadding)
        public float  padding, interiorPadding;
        // the materials drawn on it, a surface given one of them (ChiselHotspotStore.Gather's variants); 0: its own only
        public int    variantStart, variantCount;
        // a sheet's own material (render material id), given to a surface that takes one of its tiles; 0: the surface's own
        public ulong  material;
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
        // What it draws over its bounds, kMaskSize x kMaskSize cells (MaskOf), for shaped tiles
        public bool   masked;
        public uint4  mask0, mask1;

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
        public int   variant;    // which of the layout's materials (HotspotLayoutData.variantCount), when it has more than one
        public int   variations; // how many choices the seed had: the tiles that tie times the materials
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
        // A surface tilted less than this (the square of the sine of its tilt) is level
        const float kLevel = 1e-10f;

        // The texture's right and up on a surface: up follows +y, up the slope on a sloped surface, and +z on a level floor
        // or ceiling, and right is normal x up, so the texture reads the right way round from in front of the surface
        public static void Frame(float3 normal, out float3 right, out float3 up)
        {
            Frame(normal, UpAxis(normal), out right, out up);
        }

        // The axis up follows on a surface with this normal: +z on a level floor or ceiling, +y on anything else
        public static float3 UpAxis(float3 normal)
        {
            var level = normal.x * normal.x + normal.z * normal.z <= kLevel * math.lengthsq(normal);
            return level ? new float3(0, 0, 1) : new float3(0, 1, 0);
        }

        // Up along axis, as it lies on the surface
        public static void Frame(float3 normal, float3 axis, out float3 right, out float3 up)
        {
            var n = math.normalize(normal);
            up    = math.normalize(axis - n * math.dot(n, axis));
            right = math.cross(n, up);
        }

        // In the brush's own space: up is found from its local normal and carried into the tree, so the texture turns
        // with the brush
        public static void Frame(float3 normal, float3 localNormal, float4x4 nodeToTree, float rotation, bool mirror, out float3 right, out float3 up)
        {
            Frame(localNormal, out _, out var localUp);
            Frame(normal, math.mul(nodeToTree, new float4(localUp, 0)).xyz, rotation, mirror, out right, out up);
        }

        // Locked to the object, measured in the brush's own units: how many of them a unit of the tree is along the frame
        // (the geometric mean across and up; 1 on a brush that is not scaled), which multiplies the surface's density so
        // a scaled brush's texture scales with it
        public static float OwnUnits(float3 normal, float3 localNormal, float4x4 treeToNode, float rotation, bool mirror)
        {
            LocalFrame(normal, localNormal, treeToNode, rotation, mirror, out var right, out var up);
            var units = math.sqrt(math.length(right) * math.length(up));
            return units > 0 && math.isfinite(units) ? units : 1;
        }

        // The frame in the brush's own space (localNormal), turned and mirrored there, as what a tree position is dotted
        // with to give the brush's coordinates along it (treeToNode's transpose carries it), less its part along the normal
        static void LocalFrame(float3 normal, float3 localNormal, float4x4 treeToNode, float rotation, bool mirror, out float3 right, out float3 up)
        {
            Frame(localNormal, rotation, mirror, out var localRight, out var localUp);
            var carry = math.transpose(new float3x3(treeToNode));
            right = math.mul(carry, localRight);
            up    = math.mul(carry, localUp);
            var n = math.normalizesafe(normal);
            right -= n * math.dot(n, right);
            up    -= n * math.dot(n, up);
        }

        // The frame turned around the normal (degrees, counter-clockwise seen from in front), and mirrored left to right
        public static void Frame(float3 normal, float rotation, bool mirror, out float3 right, out float3 up)
        {
            Frame(normal, UpAxis(normal), rotation, mirror, out right, out up);
        }

        public static void Frame(float3 normal, float3 axis, float rotation, bool mirror, out float3 right, out float3 up)
        {
            Frame(normal, axis, out right, out up);
            if (rotation != 0)
            {
                double sin, cos;
                var quarters = rotation / 90.0;
                if (quarters == math.round(quarters))
                {
                    // a quarter turn exactly, so a rectangle stays its own bounds
                    var q = ((int)math.round(quarters) % 4 + 4) % 4;
                    sin = q == 1 ? 1 : (q == 3 ? -1 : 0);
                    cos = q == 0 ? 1 : (q == 2 ? -1 : 0);
                } else
                    math.sincos(math.radians((double)rotation), out sin, out cos);
                var r = (double3)right; var u = (double3)up;
                right = (float3)(r * cos + u * sin);
                up    = (float3)(u * cos - r * sin);
            }
            if (mirror)
                right = -right;
        }

        // The outline of what a surface draws: its bounds in the frame, and its corners when it is a triangle
        public static HotspotShape ShapeOf(NativeArray<float3> positions, float3 normal)
        {
            Frame(normal, out var right, out var up);
            return ShapeOf(positions, right, up);
        }

        public static HotspotShape ShapeOf(NativeArray<float3> positions, float3 right, float3 up)
        {
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
        // density: how many times the tiles' pixels a unit the surface takes
        public static HotspotChoice Choose(NativeArray<HotspotTileData> tiles, in HotspotLayoutData layout, in HotspotShape shape, uint seed, int pinnedId, bool radial = false, float density = 1,
                                           int pinnedSheet = 0)
        {
            var best = new HotspotChoice { tile = -1, score = float.PositiveInfinity };
            var size = shape.max - shape.min;
            if (!(size.x > 0) || !(size.y > 0))
                return best;

            if (pinnedId != 0)
            {
                for (int t = layout.tileStart; t < layout.tileStart + layout.tileCount; t++)
                {
                    if (tiles[t].id != pinnedId || tiles[t].sheetIndex != pinnedSheet)
                        continue;
                    var pinned = Score(tiles[t], shape, density, true);
                    pinned.tile = t;
                    Pick(ref pinned, layout, seed, 1);
                    return pinned;
                }
            }

            var round = radial || shape.radial;
            best = Best(tiles, layout, shape, seed, round, false, density);
            if (round && best.tile < 0)
                best = Best(tiles, layout, shape, seed, false, false, density);
            if (best.tile >= 0 && best.scale < kLastResortScale && shape.cornerCount != 3)
            {
                // the best tile failed: the one that repeats fewest times instead
                var repeated = Best(tiles, layout, shape, seed, round, true, density);
                if (repeated.tile >= 0)
                {
                    best = repeated;
                    best.lastResort = true;
                }
            }
            return best;
        }

        static HotspotChoice Best(NativeArray<HotspotTileData> tiles, in HotspotLayoutData layout, in HotspotShape shape, uint seed, bool radial, bool lastResort, float density)
        {
            var best = new HotspotChoice { tile = -1, score = float.PositiveInfinity };
            var ties = 0;
            for (int t = layout.tileStart; t < layout.tileStart + layout.tileCount; t++)
            {
                if (!Candidate(tiles[t], shape, radial))
                    continue;
                var choice = Score(tiles[t], shape, density, false, lastResort);
                // a shaped tile that fits before any other
                var key = Key(choice, tiles[t]); var bestKey = best.tile < 0 ? float.PositiveInfinity : Key(best, tiles[best.tile]);
                if (key < bestKey) { best = choice; best.tile = t; ties = 1; }
                else if (key == bestKey) ties++;
            }
            if (ties <= 1)
            {
                Pick(ref best, layout, seed, 1);
                return best;
            }

            // the seed picks one of the tiles that tie (and a material)
            var pick = Pick(ref best, layout, seed, ties);
            var variant = best.variant;
            for (int t = layout.tileStart; t < layout.tileStart + layout.tileCount; t++)
            {
                if (!Candidate(tiles[t], shape, radial))
                    continue;
                var choice = Score(tiles[t], shape, density, false, lastResort);
                if (Key(choice, tiles[t]) != Key(best, tiles[best.tile]))
                    continue;
                if (pick-- == 0)
                {
                    choice.tile = t;
                    choice.variant = variant;
                    choice.variations = best.variations;
                    return choice;
                }
            }
            return best;
        }

        // The seed's choice among ties tiles times the layout's materials: the tile (returned, of the ties) and the
        // material (choice.variant). With one material the tile is as the seed alone picks it.
        static int Pick(ref HotspotChoice choice, in HotspotLayoutData layout, uint seed, int ties)
        {
            var materials = math.max(1, layout.variantCount);
            var all = (uint)(ties * materials);
            var pick = (int)(math.hash(new uint2(seed, 0x9E3779B9u)) % all);
            choice.variant    = pick / ties;
            choice.variations = (int)all;
            return pick % ties;
        }

        static float Key(in HotspotChoice choice, in HotspotTileData tile) => tile.shaped ? choice.score : choice.score + kUnshaped;
        const float kUnshaped = 1e6f;

        // Whether matching would consider a tile for a surface of this shape (a radial tile for a round one), its flags
        // aside: a disabled or alternate tile can still be pinned
        public static bool Fits(HotspotTileData tile, in HotspotShape shape, bool radial)
        {
            tile.flags &= ~(HotspotTileFlags.Disabled | HotspotTileFlags.Alternate);
            return Candidate(tile, shape, radial);
        }

        static bool Candidate(in HotspotTileData tile, in HotspotShape shape, bool radial)
        {
            if ((tile.flags & (HotspotTileFlags.Disabled | HotspotTileFlags.Alternate | HotspotTileFlags.InsideCorners | HotspotTileFlags.InsideCornerTurned)) != 0)
                return false;
            if (((tile.flags & HotspotTileFlags.Radial) != 0) != radial)
                return false;
            // a shaped tile only for a surface shaped like it
            if (tile.shaped)
                return shape.cornerCount != 3 && Overlap(shape, tile) >= kShapeMatch;
            return (shape.cornerCount == 3) == (tile.cornerCount == 3);
        }

        // Cells of a surface's or a shaped tile's bounds, across and up, that its mask tells apart
        public const int   kMaskSize  = 16;
        // How much a surface's mask and a shaped tile's must have in common (in common over in either) for it to take it
        public const float kShapeMatch = 0.85f;

        static void SetCell(ref uint4 mask0, ref uint4 mask1, int cell)
        {
            var word = cell >> 5; var bit = 1u << (cell & 31);
            if (word < 4) mask0[word] |= bit; else mask1[word - 4] |= bit;
        }

        // In common over in either, of a surface's mask and a shaped tile's
        public static float Overlap(in HotspotShape shape, in HotspotTileData tile)
        {
            if (!shape.masked)
                return 0;
            var both   = math.csum(math.countbits(shape.mask0 & tile.mask0)) + math.csum(math.countbits(shape.mask1 & tile.mask1));
            var either = math.csum(math.countbits(shape.mask0 | tile.mask0)) + math.csum(math.countbits(shape.mask1 | tile.mask1));
            return either > 0 ? (float)both / either : 0;
        }

        // Which cells of its bounds what a surface draws (triangles in its frame) covers, by their centres
        public static void MaskOf(ref HotspotShape shape, NativeArray<float3> positions, NativeArray<int> indices, float3 right, float3 up)
        {
            shape.mask0 = shape.mask1 = uint4.zero;
            shape.masked = false;
            var size = shape.max - shape.min;
            if (!(size.x > 0) || !(size.y > 0) || indices.Length < 3)
                return;
            for (int t = 0; t + 2 < indices.Length; t += 3)
            {
                var a = new float2(math.dot(positions[indices[t]], right), math.dot(positions[indices[t]], up));
                var b = new float2(math.dot(positions[indices[t + 1]], right), math.dot(positions[indices[t + 1]], up));
                var c = new float2(math.dot(positions[indices[t + 2]], right), math.dot(positions[indices[t + 2]], up));
                var lo = math.min(a, math.min(b, c)); var hi = math.max(a, math.max(b, c));
                var i0 = math.max(0, (int)math.floor((lo.x - shape.min.x) / size.x * kMaskSize - 0.5f));
                var i1 = math.min(kMaskSize - 1, (int)math.ceil((hi.x - shape.min.x) / size.x * kMaskSize - 0.5f));
                var j0 = math.max(0, (int)math.floor((lo.y - shape.min.y) / size.y * kMaskSize - 0.5f));
                var j1 = math.min(kMaskSize - 1, (int)math.ceil((hi.y - shape.min.y) / size.y * kMaskSize - 0.5f));
                var area = Cross(b - a, c - a);
                if (area == 0)
                    continue;
                for (int j = j0; j <= j1; j++)
                {
                    for (int i = i0; i <= i1; i++)
                    {
                        var p = shape.min + size * new float2((i + 0.5f) / kMaskSize, (j + 0.5f) / kMaskSize);
                        var w0 = Cross(b - a, p - a) * area; var w1 = Cross(c - b, p - b) * area; var w2 = Cross(a - c, p - c) * area;
                        if (w0 >= 0 && w1 >= 0 && w2 >= 0)
                            SetCell(ref shape.mask0, ref shape.mask1, j * kMaskSize + i);
                    }
                }
            }
            shape.masked = true;
        }

        static float Cross(float2 a, float2 b) => a.x * b.y - a.y * b.x;

        // A shaped tile's mask: which cells of its bounds its outline (less its holes) covers, by their centres
        static void TileMask(ref HotspotTileData tile, NativeList<float2> loopPoints, NativeList<int2> loops)
        {
            tile.mask0 = tile.mask1 = uint4.zero;
            var size = tile.max - tile.min;
            for (int j = 0; j < kMaskSize; j++)
            {
                for (int i = 0; i < kMaskSize; i++)
                {
                    var p = tile.min + size * new float2((i + 0.5f) / kMaskSize, (j + 0.5f) / kMaskSize);
                    var inside = false;
                    for (int l = 0; l < loops.Length; l++)
                    {
                        for (int k = 0; k < loops[l].y; k++)
                        {
                            var a = loopPoints[loops[l].x + k]; var b = loopPoints[loops[l].x + (k + 1) % loops[l].y];
                            if ((a.y > p.y) != (b.y > p.y) && p.x < a.x + (b.x - a.x) * (p.y - a.y) / (b.y - a.y))
                                inside = !inside;
                        }
                    }
                    if (inside)
                        SetCell(ref tile.mask0, ref tile.mask1, j * kMaskSize + i);
                }
            }
        }

        // Distortion of the best way to put the surface on the tile: 2 |ln(s1 / s2)| + |ln(s1 s2)| / 2 for the scale
        // factors s1, s2 of the map. A tile that may not turn keeps the texture's up: a triangle goes corner to corner
        // the way that turns it least. A pinned tile may be turned or mirrored whatever its flags say.
        static HotspotChoice Score(in HotspotTileData tile, in HotspotShape shape, float density, bool any, bool lastResort = false)
        {
            var pixelsPerUnit = tile.pixelsPerUnit * density;
            var best = new HotspotChoice { tile = -1, score = float.PositiveInfinity };
            var rotate = !tile.shaped && (any || (tile.flags & HotspotTileFlags.AllowRotation) != 0);
            var mirror = !tile.shaped && (any || (tile.flags & HotspotTileFlags.AllowMirror) != 0);
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
            return Mapping(tile, layout, shape, choice, right, up);
        }

        // In the frame the shape was found in (ShapeOf)
        public static float4x4 Mapping(in HotspotTileData tile, in HotspotLayoutData layout, in HotspotShape shape, in HotspotChoice choice, float3 right, float3 up)
        {
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
        // The layout a tile is drawn with: its own sheet's (HotspotTileData.sheet), or the one it was matched in
        public static HotspotLayoutData SheetOf(in HotspotTileData tile, NativeArray<HotspotLayoutData> layouts, in HotspotLayoutData layout)
        {
            return tile.sheet > 0 && layouts.IsCreated && tile.sheet <= layouts.Length ? layouts[tile.sheet - 1] : layout;
        }

        public static void AddTiles(ChiselHotspotLayout layout, NativeList<HotspotTileData> tiles, out HotspotLayoutData data)
        {
            AddTiles(layout, tiles, out data, default);
        }

        // The same, and the sides of its shaped tiles (from, to in pixels, y up, the tile on their left) added to sides
        public static void AddTiles(ChiselHotspotLayout layout, NativeList<HotspotTileData> tiles, out HotspotLayoutData data, NativeList<float4> sides)
        {
            data = new HotspotLayoutData
            {
                textureSize   = new float2(layout.size.x, layout.size.y),
                pixelsPerUnit = layout.pixelsPerUnit,
                tileStart     = tiles.Length,
                maxRepeats    = layout.maxRepeats   > 0 ? layout.maxRepeats   : ChiselProjectSettings.HotspotMaxRepeats,
                maxTriangles  = layout.maxTriangles > 0 ? layout.maxTriangles : ChiselProjectSettings.HotspotMaxTriangles,
                padding       = math.max(0, layout.padding),
                interiorPadding = math.max(0, layout.interiorPadding)
            };
            foreach (var tile in layout.tiles)
            {
                if (tile.points == null || tile.points.Length < 3)
                    continue;
                if (ChiselHotspotLayout.IsShaped(tile))
                {
                    AddShapedTile(layout, tile, tiles, sides);
                    continue;
                }
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
                    // the side from here to the next corner: the same side of the points, the other way round when reversed
                    var side = area >= 0 ? i : (2 * count - 2 - i) % count;
                    if (tile.sides != null && side < tile.sides.Length)
                    {
                        t.sideMode[i]    = (int)tile.sides[side].mode;
                        t.sideStretch[i] = math.max(0, tile.sides[side].maxStretch);
                    }
                }
                if (layout.padding > 0)
                    Pad(ref t, layout.padding);
                for (int i = 0; i < count; i++)
                {
                    t.min = math.min(t.min, t.Corner(i));
                    t.max = math.max(t.max, t.Corner(i));
                }
                tiles.Add(t);
            }
            data.tileCount = tiles.Length - data.tileStart;
            LinkInsideCorners(layout, tiles, data.tileStart);
        }

        // Each tile's inside corner pieces, from the tile it links to
        static void LinkInsideCorners(ChiselHotspotLayout layout, NativeList<HotspotTileData> tiles, int tileStart)
        {
            const HotspotTileFlags kPieces = HotspotTileFlags.InsideCorners | HotspotTileFlags.InsideCornerTurned;
            foreach (var tile in layout.tiles)
            {
                if (tile.insideCorners == 0 || tile.insideCorners == tile.id)
                    continue;
                int own = -1, pieces = -1;
                for (int i = tileStart; i < tiles.Length; i++)
                {
                    if (tiles[i].id == tile.id) own = i;
                    if (tiles[i].id == tile.insideCorners && (tiles[i].flags & kPieces) != 0) pieces = i;
                }
                if (own < 0 || pieces < 0)
                    continue;
                var t = tiles[own];
                t.insideMin = tiles[pieces].min;
                t.insideMax = tiles[pieces].max;
                t.insideOne = (tiles[pieces].flags & HotspotTileFlags.InsideCorners) == 0;
                tiles[own] = t;
            }
        }

        static void AddShapedTile(ChiselHotspotLayout layout, in HotspotTile tile, NativeList<HotspotTileData> tiles, NativeList<float4> sides)
        {
            var t = new HotspotTileData { id = tile.id, cornerCount = 4, flags = tile.flags, shaped = true,
                                          pixelsPerUnit = tile.pixelsPerUnit > 0 ? tile.pixelsPerUnit : layout.pixelsPerUnit,
                                          border = math.max(0, tile.border),
                                          min = new float2(float.PositiveInfinity), max = new float2(float.NegativeInfinity) };
            // its loops, y up: the outline counter-clockwise, its holes clockwise, so the tile is on the left of each side
            var points = new NativeList<float2>(32, Allocator.Temp);
            var loops  = new NativeList<int2>(4, Allocator.Temp);
            void AddLoop(UnityEngine.Vector2Int[] loop, bool outline)
            {
                if (loop == null || loop.Length < 3)
                    return;
                var start = points.Length;
                var area = 0.0f;
                for (int i = 0; i < loop.Length; i++)
                {
                    var a = new float2(loop[i].x, layout.size.y - loop[i].y); var b = new float2(loop[(i + 1) % loop.Length].x, layout.size.y - loop[(i + 1) % loop.Length].y);
                    area += a.x * b.y - b.x * a.y;
                }
                var reverse = outline ? area < 0 : area > 0;
                for (int i = 0; i < loop.Length; i++)
                {
                    var point = loop[reverse ? loop.Length - 1 - i : i];
                    points.Add(new float2(point.x, layout.size.y - point.y));
                }
                loops.Add(new int2(start, loop.Length));
            }
            AddLoop(tile.points, true);
            if (tile.holes != null)
            {
                foreach (var hole in tile.holes)
                    AddLoop(hole.points, false);
            }
            for (int i = 0; i < loops[0].y; i++)
            {
                t.min = math.min(t.min, points[i]);
                t.max = math.max(t.max, points[i]);
            }
            // mapped by its bounds, as a rectangle is
            t.p0 = t.min; t.p1 = new float2(t.max.x, t.min.y); t.p2 = t.max; t.p3 = new float2(t.min.x, t.max.y);
            TileMask(ref t, points, loops);
            if (sides.IsCreated)
            {
                t.sideStart = sides.Length;
                for (int l = 0; l < loops.Length; l++)
                {
                    for (int k = 0; k < loops[l].y; k++)
                        sides.Add(new float4(points[loops[l].x + k], points[loops[l].x + (k + 1) % loops[l].y]));
                }
                t.sideCount = sides.Length - t.sideStart;
            }
            if (layout.padding > 0)
                Pad(ref t, layout.padding);
            points.Dispose();
            loops.Dispose();
            tiles.Add(t);
        }

        // A tile without the padding along its edges (counter-clockwise, y up): each edge moved in, the corners where the
        // moved edges meet; its border as much narrower. A tile too small for it keeps its corners.
        static void Pad(ref HotspotTileData tile, float padding)
        {
            var count = tile.cornerCount;
            var moved = new float2x4();
            for (int i = 0; i < count; i++)
            {
                var previous = (i + count - 1) % count;
                var a = (double2)tile.Corner(previous); var b = (double2)tile.Corner(i); var c = (double2)tile.Corner((i + 1) % count);
                var d0 = math.normalize(b - a); var d1 = math.normalize(c - b);
                var n0 = new double2(-d0.y, d0.x); var n1 = new double2(-d1.y, d1.x);
                // where a + n0 * padding + d0 * s meets b + n1 * padding + d1 * t
                var p0 = a + n0 * padding; var p1 = b + n1 * padding;
                var determinant = d0.x * d1.y - d0.y * d1.x;
                if (math.abs(determinant) < 1e-12)
                    return;
                var s = ((p1.x - p0.x) * d1.y - (p1.y - p0.y) * d1.x) / determinant;
                moved[i] = (float2)(p0 + d0 * s);
            }
            // every edge still the way it went, and every corner inside the tile as it was: otherwise it was too small
            for (int i = 0; i < count; i++)
            {
                if (!(math.dot(moved[(i + 1) % count] - moved[i], tile.Corner((i + 1) % count) - tile.Corner(i)) > 0))
                    return;
            }
            for (int i = 0; i < count; i++)
            {
                for (int k = 0; k < count; k++)
                {
                    var a = tile.Corner(k); var b = tile.Corner((k + 1) % count);
                    if ((b.x - a.x) * (moved[i].y - a.y) - (b.y - a.y) * (moved[i].x - a.x) <= 0)
                        return;
                }
            }
            tile.p0 = moved[0]; tile.p1 = moved[1]; tile.p2 = moved[2];
            if (count == 4) tile.p3 = moved[3];
            tile.border = math.max(0, tile.border - padding);
        }
    }
}
