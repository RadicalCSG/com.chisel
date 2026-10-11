using System;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;

namespace Chisel.Core
{
    // A line in a surface's frame (HotspotMatcher.Frame): Value(p) = dot(normal, p) + offset
    public struct HotspotSectionLine
    {
        public double2 normal;
        public double  offset;
        public readonly double Value(double2 p) => math.dot(normal, p) + offset;
    }

    // The side of a line where Value * side >= 0
    public struct HotspotSectionHalfPlane
    {
        public int line;
        public int side;
    }

    // What a surface's sections are built from: its corners in its frame and the tile's they go to
    // (HotspotMatcher.Corners; count 0: no sections), the tile's border and repeats, and the frame
    public struct HotspotSectionInput
    {
        public int              count;
        public float2           f0, f1, f2, t0, t1, t2;
        public float2           textureSize;
        public float            border, pixelsPerUnit;
        public HotspotTileFlags flags;
        public int              repeatX, repeatY;
        public float2           lastRepeat;
        // how far the repeats are moved along, a part of one (0 along an axis that does not repeat)
        public float2           offset;
        // how far a bordered tile's interior repeats are moved along, a part of one (BuildInteriorRepeats)
        public float2           interiorOffset;
        // the border along each side of the tile as the sections lay it (t0, t1, t1 + t2 - t0, t2; or the triangle's):
        // HotspotBorderMode and maximum stretch, from the tile's side with the same ends
        public int4             sideMode;
        public float4           sideStretch;
        // the layout's budgets for one surface (0: the hard limits)
        public int              maxRepeats, maxTriangles;
        // a shaped tile's own sides (HotspotTileData.sideStart, sideCount), its border measured from them
        public int              sideStart, sideCount;
        // the layout's padding: the interior samples its rectangle less this along every side, so filtering at its seams
        // never reaches the border
        public float            padding;
        // the texture is not locked to the object: repeats that keep their size lie on a grid fixed in the frame's
        // origin (the tree's), so moving the object moves it over them
        public bool             world;

        public readonly int MaxRepeats   => maxRepeats > 0 ? math.min(maxRepeats, HotspotSections.kMaxRepeats) : HotspotSections.kMaxRepeats;
        public readonly int MaxTriangles => maxTriangles > 0 ? maxTriangles : int.MaxValue;
        public float3           right, up;

        public readonly bool Repeated => repeatX > 1 || repeatY > 1 || (lastRepeat.x > 0 && lastRepeat.x < 1) || (lastRepeat.y > 0 && lastRepeat.y < 1) ||
                                         math.any(offset != 0);
    }

    // A section of a surface on a bordered tile, mapped affinely: uv = uvOrigin + map * (p - origin). A cell without
    // half planes is the interior: where no other cell is.
    public struct HotspotSectionCell
    {
        public int       halfPlaneStart, halfPlaneCount;
        public double2   origin;
        public double2   uvOrigin;
        public double2x2 map;
    }

    // A surface on its tile in sections, each mapped affinely:
    // - a tile that repeats: the surface is cut where the repeats meet, each repeat the whole tile;
    // - a tile with a border: a strip along each edge keeps the border's width in pixels at the tile's pixels per unit
    //   and stretches (or repeats, RepeatBorder) along the edge, a corner where two strips meet, the interior takes the
    //   rest. These sections meet without seams. A border keeps its width: a surface too small for it is all border,
    //   its strips meeting in the middle and cut off there.
    // A repeating tile's border is not drawn.
    public partial struct HotspotSections : IDisposable
    {
        public NativeList<HotspotSectionLine>      lines;
        public NativeList<HotspotSectionHalfPlane> halfPlanes;
        public NativeList<HotspotSectionCell>      cells;
        // The interior's cell (BuildConforming), -1: none; where its repeats are laid (BuildInteriorRepeats)
        public int                                 interiorCell;
        // The interior reaches the outline where an edge has no border (HotspotBorderCorner), so its repeats go on to it;
        // the sides of the bounds (a bit each) along which the outline has no border, where the interior starts
        public bool                                interiorToEdges;
        public int                                 openSides;

        // What the sections of a surface on its tile are built from
        public static HotspotSectionInput Input(in HotspotTileData tile, in HotspotLayoutData layout, in HotspotShape shape, in HotspotChoice choice, float3 normal)
        {
            HotspotMatcher.Frame(normal, out var right, out var up);
            return Input(tile, layout, shape, choice, right, up, 1);
        }

        // In the frame the shape was found in (HotspotMatcher.ShapeOf), at density times the tile's pixels a unit, its
        // repeats moved along by offset (in repeats, along the texture's right and up)
        public static HotspotSectionInput Input(in HotspotTileData tile, in HotspotLayoutData layout, in HotspotShape shape, in HotspotChoice choice, float3 right, float3 up, float density,
                                                float2 offset = default, bool world = false)
        {
            HotspotMatcher.Corners(tile, shape, choice, out var count, out var f0, out var f1, out var f2, out var t0, out var t1, out var t2);
            // the axes along which the tile repeats, in the surface's frame
            var repeatU = choice.lastResort || (tile.flags & HotspotTileFlags.RepeatU) != 0;
            var repeatV = choice.lastResort || (tile.flags & HotspotTileFlags.RepeatV) != 0;
            var across  = count == 4 && (choice.rotated ? repeatV : repeatU);
            var along   = count == 4 && (choice.rotated ? repeatU : repeatV);
            var shift   = offset - math.floor(offset);
            var input = new HotspotSectionInput
            {
                count = count, f0 = f0, f1 = f1, f2 = f2, t0 = t0, t1 = t1, t2 = t2,
                textureSize = layout.textureSize, border = tile.border, pixelsPerUnit = tile.pixelsPerUnit * density, flags = tile.flags,
                repeatX = choice.repeatX, repeatY = choice.repeatY, lastRepeat = choice.lastRepeat, right = right, up = up,
                offset = new float2(across ? shift.x : 0, along ? shift.y : 0),
                interiorOffset = count == 4 ? shift : float2.zero,
                maxRepeats = layout.maxRepeats, maxTriangles = layout.maxTriangles,
                sideStart = tile.sideStart, sideCount = tile.sideCount, padding = layout.padding, world = world
            };
            // repeats of their exact size, in the world: moved along so a seam falls on the frame's origin
            if (world && count == 4 && (tile.flags & HotspotTileFlags.PartialRepeats) != 0)
            {
                var acrossLength = math.length(f1 - f0) / (choice.repeatX - 1 + (choice.lastRepeat.x > 0 ? choice.lastRepeat.x : 1));
                var upLength     = math.length(f2 - f0) / (choice.repeatY - 1 + (choice.lastRepeat.y > 0 ? choice.lastRepeat.y : 1));
                if (across && acrossLength > 0)
                {
                    var at = input.offset.x + math.dot(f0, math.normalize(f1 - f0)) / acrossLength;
                    input.offset.x = at - math.floor(at);
                }
                if (along && upLength > 0)
                {
                    var at = input.offset.y + math.dot(f0, math.normalize(f2 - f0)) / upLength;
                    input.offset.y = at - math.floor(at);
                }
            }
            // each side as the sections lay it takes the rule of the tile's side with the same ends (turned or mirrored)
            var corners = count == 4 ? new float2x4(t0, t1, t1 + t2 - t0, t2) : new float2x4(t0, t1, t2, t2);
            for (int j = 0; j < count && !tile.shaped; j++)
            {
                var a = corners[j]; var b = corners[(j + 1) % count];
                for (int k = 0; k < tile.cornerCount; k++)
                {
                    var pa = tile.Corner(k); var pb = tile.Corner((k + 1) % tile.cornerCount);
                    if (math.distance(a, pa) + math.distance(b, pb) < 0.5f || math.distance(a, pb) + math.distance(b, pa) < 0.5f)
                    {
                        input.sideMode[j]    = tile.sideMode[k];
                        input.sideStretch[j] = tile.sideStretch[k];
                        break;
                    }
                }
            }
            return input;
        }

        public static HotspotSections Create(Allocator allocator)
        {
            return new HotspotSections
            {
                lines        = new NativeList<HotspotSectionLine>(12, allocator),
                halfPlanes   = new NativeList<HotspotSectionHalfPlane>(32, allocator),
                cells        = new NativeList<HotspotSectionCell>(13, allocator),
                interiorCell = -1
            };
        }

        public readonly bool IsEmpty => !cells.IsCreated || cells.Length == 0;

        public void Dispose()
        {
            if (lines.IsCreated)      lines.Dispose();
            if (halfPlanes.IsCreated) halfPlanes.Dispose();
            if (cells.IsCreated)      cells.Dispose();
            this = default;
        }

        struct Points4
        {
            public double2 a, b, c, d;
            public double2 this[int i]
            {
                readonly get => i == 0 ? a : (i == 1 ? b : (i == 2 ? c : d));
                set { if (i == 0) a = value; else if (i == 1) b = value; else if (i == 2) c = value; else d = value; }
            }
        }

        static double Cross(double2 a, double2 b) => a.x * b.y - a.y * b.x;

        // A rectangle or a triangle, and its band: inner corners, and where they are square to the edges
        struct Outline
        {
            public int     count;
            public Points4 corner, along, inward, inner, footNext, footPrevious;

            public Outline(int count, float2 f0, float2 f1, float2 f2)
            {
                this = default;
                this.count = count;
                corner[0] = f0;
                corner[1] = f1;
                if (count == 3)
                    corner[2] = f2;
                else
                {
                    corner[2] = (double2)f1 + (double2)f2 - (double2)f0;
                    corner[3] = f2;
                }
                var area = 0.0;
                for (int i = 0; i < count; i++)
                    area += Cross(corner[i], corner[(i + 1) % count]);
                var orientation = area > 0 ? 1.0 : -1.0;
                for (int i = 0; i < count; i++)
                {
                    var edge = math.normalize(corner[(i + 1) % count] - corner[i]);
                    along[i]  = edge;
                    inward[i] = new double2(-edge.y, edge.x) * orientation;
                }
            }

            // The widest band: where the strips meet, half the shorter side of a rectangle, the inradius of a triangle
            public readonly double MaxBand()
            {
                if (count == 4)
                    return 0.5 * math.min(math.length(corner[1] - corner[0]), math.length(corner[3] - corner[0]));
                var perimeter = math.length(corner[1] - corner[0]) + math.length(corner[2] - corner[1]) + math.length(corner[0] - corner[2]);
                return perimeter > 0 ? math.abs(Cross(corner[1] - corner[0], corner[2] - corner[0])) / perimeter : 0;
            }

            // Whether the strips along edge i, band wide on both sides, meet: a triangle's at its inradius, a
            // rectangle's on its shorter sides
            public readonly bool Closed(int i, double band)
            {
                if (band < MaxBand())
                    return false;
                return count == 3 || math.length(corner[(i + 1) % count] - corner[i]) <= 2 * band * (1 + 1e-9);
            }

            // open: the edges (a bit each) not inset
            public void Inset(double band, int open = 0)
            {
                for (int i = 0; i < count; i++)
                {
                    var previous = (i + count - 1) % count;
                    var bandPrevious = (open & (1 << previous)) != 0 ? 0 : band;
                    var bandHere     = (open & (1 << i)) != 0 ? 0 : band;
                    inner[i] = Intersect(inward[previous], math.dot(inward[previous], corner[previous]) + bandPrevious,
                                         inward[i],        math.dot(inward[i], corner[i]) + bandHere);
                    footNext[i]     = corner[i] + along[i] * math.dot(inner[i] - corner[i], along[i]);
                    footPrevious[i] = corner[i] + along[previous] * math.dot(inner[i] - corner[i], along[previous]);
                }
            }

            // Where n0.p = c0 and n1.p = c1
            static double2 Intersect(double2 n0, double c0, double2 n1, double c1)
            {
                var determinant = Cross(n0, n1);
                return new double2((c0 * n1.y - n0.y * c1) / determinant, (n0.x * c1 - c0 * n1.x) / determinant);
            }
        }

        // The band in the surface (units) and in the tile (pixels): the border's width at the tile's density, at most
        // where the surface's strips meet; false when there is none
        static bool Bands(in HotspotSectionInput input, in Outline s, in Outline t, out double surfaceBand, out double tileBand)
        {
            surfaceBand = tileBand = 0;
            if (!(input.border > 0) || !(input.pixelsPerUnit > 0))
                return false;
            var full = (double)input.border / input.pixelsPerUnit;
            surfaceBand = math.min(full, s.MaxBand());
            tileBand    = math.min(input.border * (surfaceBand / full), t.MaxBand());
            return surfaceBand > 0 && tileBand > 0;
        }

        // Whether a surface is all border: too small for its border to leave an interior
        public static bool IsAllBorder(in HotspotSectionInput input)
        {
            if ((input.count != 3 && input.count != 4) || input.Repeated)
                return false;
            var s = new Outline(input.count, input.f0, input.f1, input.f2);
            var t = new Outline(input.count, input.t0, input.t1, input.t2);
            return Bands(input, s, t, out var surfaceBand, out _) && surfaceBand >= s.MaxBand();
        }

        int AddLine(double2 normal, double2 through)
        {
            lines.Add(new HotspotSectionLine { normal = normal, offset = -math.dot(normal, through) });
            return lines.Length - 1;
        }

        void AddCell(double2 a, double2 b, double2 c, double2 ta, double2 tb, double2 tc, double2 textureSize,
                     int line0, int side0, int line1, int side1, int line2 = -1, int side2 = 0, int line3 = -1, int side3 = 0)
        {
            var start = halfPlanes.Length;
            Half(line0, side0);
            Half(line1, side1);
            Half(line2, side2);
            Half(line3, side3);
            AddCell(start, a, b, c, ta, tb, tc, textureSize);
        }

        void Half(int line, int side)
        {
            if (line >= 0)
                halfPlanes.Add(new HotspotSectionHalfPlane { line = line, side = side });
        }

        // A cell of the half planes added since start, (a, b, c) to (ta, tb, tc)
        void AddCell(int start, double2 a, double2 b, double2 c, double2 ta, double2 tb, double2 tc, double2 textureSize)
        {
            var surface = new double2x2(b - a, c - a);
            var tile    = new double2x2((tb - ta) / textureSize, (tc - ta) / textureSize);
            cells.Add(new HotspotSectionCell
            {
                halfPlaneStart = start,
                halfPlaneCount = halfPlanes.Length - start,
                origin         = a,
                uvOrigin       = ta / textureSize,
                map            = math.mul(tile, math.inverse(surface))
            });
        }

        // Repeats too many to cut a surface into: a sliver, stretched instead
        public const int kMaxRepeats = 4096;

        // How many times a length takes a period, at least once; 0 when that is more than kMaxRepeats
        public static int Repeats(double length, double period)
        {
            if (!(period > 0))
                return 1;
            var count = math.max(1.0, math.round(length / period));
            return count > kMaxRepeats ? 0 : (int)count;
        }

        // How a border strip along an edge of the given length takes its tile side's strip (period, in the same units), by
        // that side's rule (HotspotBorderMode) or the tile's flags: how many repeats, each step long (with partial
        // repeats the last one cut off where the edge ends). A stretching side with a limit repeats, the fewest times
        // that keep within it, when it would stretch further.
        internal static int StripRepeats(in HotspotSectionInput input, int side, double length, double period, out double step)
        {
            step = length;
            if (!(length > 0) || !(period > 0))
                return 1;
            var mode    = side >= 0 && side < 4 ? (HotspotBorderMode)input.sideMode[side] : HotspotBorderMode.Tile;
            var repeat  = mode == HotspotBorderMode.Repeat || mode == HotspotBorderMode.RepeatPartial ||
                          (mode == HotspotBorderMode.Tile && (input.flags & HotspotTileFlags.RepeatBorder) != 0);
            var partial = mode == HotspotBorderMode.RepeatPartial ||
                          (mode == HotspotBorderMode.Tile && (input.flags & HotspotTileFlags.PartialRepeats) != 0);
            if (repeat)
            {
                if (partial)
                {
                    var count = math.max(1, PartialRepeats(length, period, out var last));
                    if (count > input.MaxRepeats)
                        return 1;
                    if (count > 1 || last < 1) step = period;
                    return count;
                }
                var whole = math.max(1, Repeats(length, period));
                if (whole > input.MaxRepeats)
                    return 1;
                step = length / whole;
                return whole;
            }
            var limit = side >= 0 && side < 4 ? (double)input.sideStretch[side] : 0;
            if (limit > 1 && length / period > limit)
            {
                var count = (int)math.ceil(length / period / limit - 1e-9);
                if (count > 1 && count <= input.MaxRepeats)
                {
                    step = length / count;
                    return count;
                }
            }
            return 1;
        }

        // How many repeats of a period it takes to cover a length, the last cut off (last: how much of it is used, 1 for
        // all); 0 when that is more than kMaxRepeats. A length within a millionth of a whole number of periods is that.
        public static int PartialRepeats(double length, double period, out double last)
        {
            last = 1;
            if (!(period > 0))
                return 1;
            var periods = length / period;
            var whole = math.round(periods);
            var count = math.abs(periods - whole) <= 1e-6 * math.max(1, whole) ? whole : math.ceil(periods);
            count = math.max(1, count);
            if (count > kMaxRepeats)
                return 0;
            last = math.clamp(periods - (count - 1), 0, 1);
            return (int)count;
        }

        // The sections of a surface on its tile; false when it is one section: the tile neither repeats nor has a
        // border, or no band fits
        public bool Build(in HotspotTileData tile, in HotspotLayoutData layout, in HotspotShape shape, in HotspotChoice choice)
        {
            return Build(Input(tile, layout, shape, choice, new float3(0, 0, -1)));
        }

        // The sections from their input, around the surface's bounds (or its triangle)
        public bool Build(in HotspotSectionInput input)
        {
            lines.Clear();
            halfPlanes.Clear();
            cells.Clear();
            interiorCell = -1;
            interiorToEdges = false;
            openSides = 0;
            var count = input.count;
            if (count != 3 && count != 4)
                return false;
            float2 f0 = input.f0, f1 = input.f1, f2 = input.f2, t0 = input.t0, t1 = input.t1, t2 = input.t2;
            if (input.Repeated)
                return BuildRepeats(math.max(1, input.repeatX), math.max(1, input.repeatY),
                                    input.lastRepeat.x > 0 ? input.lastRepeat.x : 1, input.lastRepeat.y > 0 ? input.lastRepeat.y : 1,
                                    input.offset, f0, f1, f2, t0, t1, t2, input.textureSize, input.MaxRepeats);
            var s = new Outline(count, f0, f1, f2);
            var t = new Outline(count, t0, t1, t2);
            if (!Bands(input, s, t, out var surfaceBand, out var tileBand))
                return false;
            s.Inset(surfaceBand);
            t.Inset(tileBand);
            var size = (double2)input.textureSize;

            // inset[i] parallel to edge i, positive inside; squareNext[i] and squarePrevious[i] square to the edges
            // after and before corner i through its inner corner, negative on the corner's side
            var inset = new int4(-1); var squareNext = new int4(-1); var squarePrevious = new int4(-1); var diagonal = new int4(-1);
            for (int i = 0; i < count; i++)
                inset[i] = AddLine(s.inward[i], s.corner[i] + s.inward[i] * surfaceBand);
            for (int i = 0; i < count; i++)
            {
                var previous = (i + count - 1) % count;
                if (count == 4)
                {
                    // a rectangle's square lines are the insets of the other edges
                    squareNext[i]     = inset[previous];
                    squarePrevious[i] = inset[i];
                } else
                {
                    squareNext[i]     = AddLine(s.along[i], s.inner[i]);
                    squarePrevious[i] = AddLine(-s.along[previous], s.inner[i]);
                    var toInner = math.normalize(s.inner[i] - s.corner[i]);
                    diagonal[i] = AddLine(new double2(-toInner.y, toInner.x), s.corner[i]);
                }
            }

            // all border: no interior, and no strip where the strips across meet
            if (surfaceBand < s.MaxBand())
                AddCell(s.inner[0], s.inner[1], s.inner[count - 1], t.inner[0], t.inner[1], t.inner[count - 1], size,
                        inset[0], 1, inset[1], 1, inset[2], 1, count == 4 ? inset[3] : -1, 1);
            for (int i = 0; i < count; i++)
            {
                var next = (i + 1) % count;
                if (s.Closed(i, surfaceBand))
                {
                    AddCorner(i, count, s, t, size, squareNext, squarePrevious, diagonal);
                    continue;
                }
                var length = math.length(s.footPrevious[next] - s.footNext[i]);
                var period = math.length(t.footPrevious[next] - t.footNext[i]) * surfaceBand / tileBand;
                var repeats = StripRepeats(input, i, length, period, out var step);
                // the strip, or its repeats: each from the tile's whole strip, cut square to the edge
                var previousCut = squareNext[i];
                for (int r = 0; r < repeats; r++)
                {
                    var a = s.footNext[i] + s.along[i] * (step * r);
                    var cut = r + 1 < repeats ? AddLine(s.along[i], s.footNext[i] + s.along[i] * (step * (r + 1))) : -1;
                    var start = halfPlanes.Length;
                    Half(inset[i], -1);
                    Half(previousCut, 1);
                    if (cut >= 0) Half(cut, -1); else Half(squarePrevious[next], 1);
                    AddCell(start, a, a + s.along[i] * step, a + s.inward[i] * surfaceBand,
                            t.footNext[i], t.footPrevious[next], t.inner[i], size);
                    previousCut = cut;
                }
                AddCorner(i, count, s, t, size, squareNext, squarePrevious, diagonal);
            }
            return true;
        }

        void AddCorner(int i, int count, in Outline s, in Outline t, double2 size, int4 squareNext, int4 squarePrevious, int4 diagonal)
        {
            if (count == 4)
            {
                AddCell(s.corner[i], s.footNext[i], s.footPrevious[i], t.corner[i], t.footNext[i], t.footPrevious[i], size,
                        squareNext[i], -1, squarePrevious[i], -1);
                return;
            }
            // a triangle's corners differ from its tile's, so each is two triangles
            var side = lines[diagonal[i]].Value(s.footNext[i]) > 0 ? 1 : -1;
            AddCell(s.corner[i], s.footNext[i], s.inner[i], t.corner[i], t.footNext[i], t.inner[i], size,
                    squareNext[i], -1, squarePrevious[i], -1, diagonal[i], side);
            AddCell(s.corner[i], s.inner[i], s.footPrevious[i], t.corner[i], t.inner[i], t.footPrevious[i], size,
                    squareNext[i], -1, squarePrevious[i], -1, diagonal[i], -side);
        }

        // A rectangle repeatX by repeatY times the tile, each repeat (f0', f1', f2') to (t0, t1, t2); of the last repeat
        // across and up only lastX and lastY are used. Moved along by shift (a part of a repeat), the first repeat is cut
        // off and the rest reach one further.
        bool BuildRepeats(int repeatX, int repeatY, double lastX, double lastY, float2 shift, float2 f0, float2 f1, float2 f2, float2 t0, float2 t1, float2 t2, float2 textureSize, int maxRepeats)
        {
            var across = ((double2)f1 - (double2)f0) / (repeatX - 1 + lastX);
            var up     = ((double2)f2 - (double2)f0) / (repeatY - 1 + lastY);
            if (!(math.lengthsq(across) > 0) || !(math.lengthsq(up) > 0))
                return false;
            var origin = (double2)f0 - across * shift.x - up * shift.y;
            repeatX = (int)math.ceil(repeatX - 1 + lastX + shift.x - 1e-9);
            repeatY = (int)math.ceil(repeatY - 1 + lastY + shift.y - 1e-9);
            if (repeatX > kMaxRepeats + 1 || repeatY > kMaxRepeats + 1 || (long)repeatX * repeatY > maxRepeats)
                return false;
            var linesX = lines.Length;
            for (int i = 1; i < repeatX; i++)
                AddLine(math.normalize(across), origin + across * i);
            var linesY = lines.Length;
            for (int j = 1; j < repeatY; j++)
                AddLine(math.normalize(up), origin + up * j);
            for (int j = 0; j < repeatY; j++)
            {
                for (int i = 0; i < repeatX; i++)
                {
                    var start = halfPlanes.Length;
                    if (i > 0)           Half(linesX + i - 1, 1);
                    if (i + 1 < repeatX) Half(linesX + i, -1);
                    if (j > 0)           Half(linesY + j - 1, 1);
                    if (j + 1 < repeatY) Half(linesY + j, -1);
                    var a = origin + across * i + up * j;
                    AddCell(start, a, a + across, a + up, t0, t1, t2, textureSize);
                }
            }
            return true;
        }

        // The section a point of the frame is in, or the nearest one for a point just outside them all; the interior
        // (a cell without half planes) when it is in none
        public readonly int Locate(double2 p)
        {
            var best = 0;
            var bestMargin = double.NegativeInfinity;
            var interior = -1;
            for (int c = 0; c < cells.Length; c++)
            {
                var cell = cells[c];
                if (cell.halfPlaneCount == 0)
                {
                    interior = c;
                    continue;
                }
                var margin = double.PositiveInfinity;
                for (int h = cell.halfPlaneStart; h < cell.halfPlaneStart + cell.halfPlaneCount; h++)
                    margin = math.min(margin, halfPlanes[h].side * lines[halfPlanes[h].line].Value(p));
                if (margin > bestMargin)
                {
                    bestMargin = margin;
                    best = c;
                }
            }
            if (interior >= 0 && !(bestMargin >= 0))
                return interior;
            return best;
        }

        public readonly float2 UV(double2 p) => UV(Locate(p), p);

        public readonly float2 UV(int cell, double2 p)
        {
            var found = cells[cell];
            return (float2)(found.uvOrigin + math.mul(found.map, p - found.origin));
        }

        // How far apart two uvs of one vertex may be and still be one vertex
        const float kSameUV = 1e-5f;

        // Each triangle's uvs from the section it is in, the interior's from its repeats when there are any
        // (BuildInteriorRepeats). A
        // vertex on a seam, where repeats meet, gets a vertex of its own for each side.
        public readonly void AssignUVs(NativeList<int> indices, NativeList<float3> colliderVertices, NativeList<SelectVertex> selectVertices,
                                       NativeList<RenderVertex> renderVertices, float3 right, float3 up)
        {
            AssignUVs(indices, colliderVertices, selectVertices, renderVertices, right, up, default);
        }

        public readonly void AssignUVs(NativeList<int> indices, NativeList<float3> colliderVertices, NativeList<SelectVertex> selectVertices,
                                       NativeList<RenderVertex> renderVertices, float3 right, float3 up, in HotspotSections repeats)
        {
            var repeated = interiorCell >= 0 && repeats.cells.IsCreated && repeats.cells.Length > 0;
            var r = (double3)right; var u = (double3)up;
            var assigned = new NativeList<bool>(renderVertices.Length, Allocator.Temp);
            assigned.Resize(renderVertices.Length, NativeArrayOptions.ClearMemory);
            var copies = new UnsafeHashMap<int2, int>(renderVertices.Length, Allocator.Temp);
            for (int t = 0; t + 2 < indices.Length; t += 3)
            {
                var centroid = double2.zero;
                for (int k = 0; k < 3; k++)
                {
                    var p = (double3)renderVertices[indices[t + k]].position;
                    centroid += new double2(math.dot(r, p), math.dot(u, p));
                }
                var cell = Locate(centroid / 3);
                // a repeat's cell of the interior counts after the sections' own
                var repeat = -1;
                if (repeated && cell == interiorCell)
                {
                    repeat = repeats.Locate(centroid / 3);
                    cell = cells.Length + repeat;
                }
                for (int k = 0; k < 3; k++)
                {
                    var index = indices[t + k];
                    if (copies.TryGetValue(new int2(index, cell), out var copy))
                    {
                        indices[t + k] = copy;
                        continue;
                    }
                    var vertex = renderVertices[index];
                    var p = (double3)vertex.position;
                    var uv = repeat >= 0 ? repeats.UV(repeat, new double2(math.dot(r, p), math.dot(u, p)))
                                         : UV(cell, new double2(math.dot(r, p), math.dot(u, p)));
                    if (!assigned[index])
                    {
                        vertex.uv0 = uv;
                        renderVertices[index] = vertex;
                        assigned[index] = true;
                        copies.Add(new int2(index, cell), index);
                    } else if (math.distancesq(vertex.uv0, uv) <= kSameUV * kSameUV)
                    {
                        copies.Add(new int2(index, cell), index);
                    } else
                    {
                        vertex.uv0 = uv;
                        renderVertices.Add(vertex);
                        colliderVertices.Add(colliderVertices[index]);
                        selectVertices.Add(selectVertices[index]);
                        assigned.Add(true);
                        copy = renderVertices.Length - 1;
                        copies.Add(new int2(index, cell), copy);
                        indices[t + k] = copy;
                    }
                }
            }
            assigned.Dispose();
            copies.Dispose();
        }

        // A point of a piece of a surface triangle. kind 0: corner a of the triangle; 1: where triangle edge a crosses
        // line b; 2: where lines a and b cross. support is the edge to the next point: a line, or -1 - a triangle edge.
        struct PiecePoint
        {
            public double2 p2;
            public double3 p3;
            public double3 weights;
            public int     kind, a, b;
            public int     support;

            // on the line, or on one that is the same line (same: the first of the lines it coincides with)
            public readonly bool OnLine(int line, NativeArray<int> same) =>
                (kind == 1 && same[b] == same[line]) || (kind == 2 && (same[a] == same[line] || same[b] == same[line]));
        }

        static bool Before(double3 p, double3 q)
        {
            if (p.x != q.x) return p.x < q.x;
            if (p.y != q.y) return p.y < q.y;
            return p.z < q.z;
        }

        // Cuts the surface's triangles along the lines between the sections, so each piece lies in one. A point where a
        // line crosses a triangle edge is computed from the edge's ends in a fixed order, and one where two lines cross
        // from the lines alone, so the triangles on either side of an edge get the very same points and share them.
        public readonly void Split(float3 right, float3 up, float3 normal, NativeList<int> indices,
                                   NativeList<float3> colliderVertices, NativeList<SelectVertex> selectVertices, NativeList<RenderVertex> renderVertices)
        {
            if (lines.Length == 0 || indices.Length == 0)
                return;
            var r = (double3)right; var u = (double3)up; var n = (double3)normal;
            var planeDistance = math.dot(n, (double3)colliderVertices[indices[0]]);
            // a point this close to a line is on it, as in CutOnce: a few float steps of the positions
            var extent = 0.0; var centre = double2.zero;
            for (int i = 0; i < indices.Length; i++)
            {
                var p = new double2(math.dot(r, (double3)colliderVertices[indices[i]]), math.dot(u, (double3)colliderVertices[indices[i]]));
                extent = math.max(extent, math.cmax(math.abs(p)));
                centre += p;
            }
            centre /= indices.Length;
            var size = 0.0;
            for (int i = 0; i < indices.Length; i++)
                size = math.max(size, math.distance(new double2(math.dot(r, (double3)colliderVertices[indices[i]]), math.dot(u, (double3)colliderVertices[indices[i]])), centre));
            var onLine = 1e-6 * math.max(1, extent);
            // lines that coincide (strips meeting in the middle of a surface that is all border) are one line: a point on
            // one is on the other, and they never cross
            var same = SameLines(Allocator.Temp, centre, size, onLine);

            using var newIndices  = new NativeList<int>(indices.Length * 4, Allocator.Temp);
            using var newCollider = new NativeList<float3>(colliderVertices.Length * 2, Allocator.Temp);
            using var newSelect   = new NativeList<SelectVertex>(colliderVertices.Length * 2, Allocator.Temp);
            using var newRender   = new NativeList<RenderVertex>(colliderVertices.Length * 2, Allocator.Temp);
            using var lookup      = new UnsafeHashMap<float3, int>(colliderVertices.Length * 2, Allocator.Temp);
            using var values      = new NativeList<double>(16, Allocator.Temp);
            var points     = new NativeList<PiecePoint>(64, Allocator.Temp);
            var ranges     = new NativeList<int2>(16, Allocator.Temp);
            var nextPoints = new NativeList<PiecePoint>(64, Allocator.Temp);
            var nextRanges = new NativeList<int2>(16, Allocator.Temp);

            var q3 = new NativeArray<double3>(3, Allocator.Temp);
            var q2 = new NativeArray<double2>(3, Allocator.Temp);
            for (int t = 0; t + 2 < indices.Length; t += 3)
            {
                for (int k = 0; k < 3; k++)
                {
                    q3[k] = (double3)colliderVertices[indices[t + k]];
                    q2[k] = new double2(math.dot(r, q3[k]), math.dot(u, q3[k]));
                }
                points.Clear();
                ranges.Clear();
                for (int k = 0; k < 3; k++)
                {
                    var weights = double3.zero;
                    weights[k] = 1;
                    points.Add(new PiecePoint { p2 = q2[k], p3 = q3[k], weights = weights, kind = 0, a = k, b = -1, support = -1 - k });
                }
                ranges.Add(new int2(0, 3));

                for (int line = 0; line < lines.Length; line++)
                {
                    nextPoints.Clear();
                    nextRanges.Clear();
                    for (int piece = 0; piece < ranges.Length; piece++)
                    {
                        var range = ranges[piece];
                        values.Clear();
                        bool above = false, below = false;
                        for (int i = 0; i < range.y; i++)
                        {
                            var point = points[range.x + i];
                            var value = point.OnLine(line, same) ? 0 : lines[line].Value(point.p2);
                            if (math.abs(value) <= onLine) value = 0;
                            values.Add(value);
                            above |= value > 0;
                            below |= value < 0;
                        }
                        if (!(above && below))
                        {
                            var start = nextPoints.Length;
                            for (int i = 0; i < range.y; i++)
                                nextPoints.Add(points[range.x + i]);
                            nextRanges.Add(new int2(start, range.y));
                            continue;
                        }
                        for (int side = 1; side >= -1; side -= 2)
                        {
                            var start = nextPoints.Length;
                            for (int i = 0; i < range.y; i++)
                            {
                                var from = points[range.x + i];
                                var to   = points[range.x + (i + 1) % range.y];
                                var sFrom = values[i] * side;
                                var sTo   = values[(i + 1) % range.y] * side;
                                if (sFrom >= 0)
                                {
                                    var kept = from;
                                    kept.support = (sFrom == 0 && sTo < 0) ? line : from.support;
                                    nextPoints.Add(kept);
                                }
                                if ((sFrom > 0 && sTo < 0) || (sFrom < 0 && sTo > 0))
                                {
                                    var crossing = Crossing(from.support, line, q3, q2, r, u, n, planeDistance);
                                    crossing.support = sFrom > 0 ? line : from.support;
                                    nextPoints.Add(crossing);
                                }
                            }
                            var count = nextPoints.Length - start;
                            if (count >= 3)
                                nextRanges.Add(new int2(start, count));
                            else
                                nextPoints.Length = start;
                        }
                    }
                    (points, nextPoints) = (nextPoints, points);
                    (ranges, nextRanges) = (nextRanges, ranges);
                }

                var render0 = renderVertices[indices[t]]; var render1 = renderVertices[indices[t + 1]]; var render2 = renderVertices[indices[t + 2]];
                var entityID = selectVertices[indices[t]].entityID;
                for (int piece = 0; piece < ranges.Length; piece++)
                {
                    var range = ranges[piece];
                    var first = points[range.x];
                    for (int i = 1; i + 1 < range.y; i++)
                    {
                        var second = points[range.x + i];
                        var third  = points[range.x + i + 1];
                        // flat as far as the positions tell: no triangle
                        var longest = math.sqrt(math.max(math.distancesq(first.p2, second.p2), math.max(math.distancesq(second.p2, third.p2), math.distancesq(third.p2, first.p2))));
                        if (!(math.abs(Cross(second.p2 - first.p2, third.p2 - first.p2)) > onLine * longest))
                            continue;
                        // points that round to one float position are one vertex (lines crossing at one point make it
                        // from different pairs of them); a triangle that loses a corner to that is left out
                        var i0 = Vertex(first, indices, t); var i1 = Vertex(second, indices, t); var i2 = Vertex(third, indices, t);
                        if (i0 == i1 || i1 == i2 || i2 == i0)
                            continue;
                        newIndices.Add(i0);
                        newIndices.Add(i1);
                        newIndices.Add(i2);
                    }
                }

                int Vertex(in PiecePoint point, NativeList<int> sourceIndices, int triangle)
                {
                    var key = (point.kind == 0 ? colliderVertices[sourceIndices[triangle + point.a]] : (float3)point.p3) + 0.0f;
                    if (lookup.TryGetValue(key, out var index))
                        return index;
                    index = newCollider.Length;
                    lookup.Add(key, index);
                    if (point.kind == 0)
                    {
                        var source = sourceIndices[triangle + point.a];
                        newCollider.Add(colliderVertices[source]);
                        newSelect.Add(selectVertices[source]);
                        newRender.Add(renderVertices[source]);
                        return index;
                    }
                    var position = (float3)point.p3;
                    var w = (float3)point.weights;
                    newCollider.Add(position);
                    newSelect.Add(new SelectVertex { position = position, entityID = entityID });
                    newRender.Add(new RenderVertex
                    {
                        position = position,
                        normal   = math.normalizesafe(w.x * render0.normal + w.y * render1.normal + w.z * render2.normal, normal),
                        tangent  = w.x * render0.tangent + w.y * render1.tangent + w.z * render2.tangent,
                        uv1      = w.x * render0.uv1 + w.y * render1.uv1 + w.z * render2.uv1
                    });
                    return index;
                }
            }
            q3.Dispose();
            q2.Dispose();
            same.Dispose();
            points.Dispose();
            ranges.Dispose();
            nextPoints.Dispose();
            nextRanges.Dispose();

            indices.Clear();          indices.AddRange(newIndices.AsArray());
            colliderVertices.Clear(); colliderVertices.AddRange(newCollider.AsArray());
            selectVertices.Clear();   selectVertices.AddRange(newSelect.AsArray());
            renderVertices.Clear();   renderVertices.AddRange(newRender.AsArray());
        }

        // Where the edge or line a piece's edge lies on (support) crosses a line
        readonly PiecePoint Crossing(int support, int line, NativeArray<double3> q3, NativeArray<double2> q2, double3 r, double3 u, double3 n, double planeDistance)
        {
            if (support < 0)
            {
                var edge = -1 - support;
                int from = edge, to = (edge + 1) % 3;
                if (Before(q3[to], q3[from]))
                    (from, to) = (to, from);
                var vFrom = lines[line].Value(q2[from]);
                var vTo   = lines[line].Value(q2[to]);
                var t = vFrom != vTo ? vFrom / (vFrom - vTo) : 0.5;
                var weights = double3.zero;
                weights[from] = 1 - t;
                weights[to]   = t;
                return new PiecePoint
                {
                    p2 = q2[from] + (q2[to] - q2[from]) * t,
                    p3 = q3[from] + (q3[to] - q3[from]) * t,
                    weights = weights, kind = 1, a = edge, b = line
                };
            }
            int first = math.min(support, line), second = math.max(support, line);
            var l0 = lines[first]; var l1 = lines[second];
            var determinant = Cross(l0.normal, l1.normal);
            var p2 = new double2((-l0.offset * l1.normal.y + l0.normal.y * l1.offset) / determinant,
                                 (-l0.normal.x * l1.offset + l0.offset * l1.normal.x) / determinant);
            // its weights in the triangle, for what is interpolated
            var e1 = q2[1] - q2[0]; var e2 = q2[2] - q2[0]; var d = p2 - q2[0];
            var area = Cross(e1, e2);
            var w1 = area != 0 ? Cross(d, e2) / area : 0;
            var w2 = area != 0 ? Cross(e1, d) / area : 0;
            return new PiecePoint
            {
                p2 = p2,
                p3 = r * p2.x + u * p2.y + n * planeDistance,
                weights = new double3(1 - w1 - w2, w1, w2), kind = 2, a = first, b = second
            };
        }
    }
}
