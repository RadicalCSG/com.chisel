using System.Collections.Generic;
using Chisel.Core;
using Unity.Mathematics;
using UnityEngine;

namespace Chisel.Components
{
    /// <summary>One brush a displaced surface adds to its generator's: a column standing on the surface, adding or cutting</summary>
    public struct ChiselDisplacementColumn
    {
        public bool     additive;
        /// <summary>The one face of the column that can be seen, of its own planes (its top or its bottom). Only that one
        /// is smoothed with the rest of the surface; the others are inside what the columns make.</summary>
        public int      seenFace;
        /// <summary>The column's planes in the generator's space, pointing out of it: its own (its walls, then its top and
        /// its bottom), then the brush's sides it is cut to</summary>
        public float4[] planes;
        /// <summary>How many of the planes are the column's own</summary>
        public int      ownPlanes;
        /// <summary>The brush's sides the column is cut to, after its own planes: the index of each in brushPlanes</summary>
        public int[]    sides;
        public Vector3  boundsMin;
        public Vector3  boundsMax;
        /// <summary>Which of the generator's groups of columns it is in: <see cref="ChiselDisplacement.kRaised"/>,
        /// kLowered or kPushed</summary>
        public int      group;
        /// <summary>What its brush is made from: its planes, its face that is seen, its group and the heights round it
        /// (which its normals come from). A column with the key of one already made is the same brush.</summary>
        public uint     key;
        /// <summary>The corners of a column over a triangle: under its three surface points, then over them; null for
        /// any other column</summary>
        public float3[] corners;
    }

    /// <summary>
    /// A surface displaced by a grid of heights (<see cref="ChiselSurfacePaint"/>, its X along the surface's normal): every
    /// cell is two triangles, and every triangle a column along the normal between two planes. One not lowered adds from
    /// just under the surface (the generator's own brush has its displaced surface moved in by <see cref="kOverlap"/>) up
    /// to its heights, a lowered one cuts from its depths up to the surface, and where a depth comes closer than
    /// <see cref="kMinThickness"/> to the brush's opposite side it pushes that side out. Flat cells with nothing displaced
    /// round them are one column together, a rectangle of them. All are convex.
    /// </summary>
    /// <summary>The columns of a displaced surface's cells, kept between calls of <see cref="ChiselDisplacement.Columns"/></summary>
    public sealed class ChiselDisplacementCache
    {
        // everything the columns are made from besides the heights, exactly; a change to it forgets every cell
        float[] made;
        readonly Dictionary<int2, Cell> cells = new();

        sealed class Cell
        {
            public float[] heights;
            public ChiselDisplacementColumn[] raised, cut, pushed;
        }

        internal void Begin(ChiselSurfacePaint grid, float3 n, IReadOnlyList<float4> brushPlanes, int[] ends)
        {
            var values = new List<float>
            {
                grid.origin.x, grid.origin.y, grid.origin.z, grid.cellU.x, grid.cellU.y, grid.cellU.z,
                grid.cellV.x, grid.cellV.y, grid.cellV.z, grid.resolution.x, grid.resolution.y, n.x, n.y, n.z
            };
            foreach (var plane in brushPlanes)
            {
                values.Add(plane.x); values.Add(plane.y); values.Add(plane.z); values.Add(plane.w);
            }
            values.Add(-1);
            foreach (var end in ends)
                values.Add(end);
            var now = values.ToArray();
            if (made != null && SameBits(made, now))
                return;
            made = now;
            cells.Clear();
        }

        // the heights of the 4 by 4 grid points round a cell (from its lowest corner), which its columns come from
        static float[] Round(ChiselSurfacePaint grid, int2 cell)
        {
            var size = grid.resolution;
            var heights = new float[16];
            for (int j = 0, k = 0; j < 4; j++)
            {
                for (int i = 0; i < 4; i++, k++)
                    heights[k] = grid[math.clamp(cell.x - 1 + i, 0, size.x - 1), math.clamp(cell.y - 1 + j, 0, size.y - 1)].x;
            }
            return heights;
        }

        static bool SameBits(float[] a, float[] b)
        {
            if (a.Length != b.Length)
                return false;
            for (int i = 0; i < a.Length; i++)
            {
                if (math.asuint(a[i]) != math.asuint(b[i]))
                    return false;
            }
            return true;
        }

        internal bool TryGet(ChiselSurfacePaint grid, int2 cell, List<ChiselDisplacementColumn> raised, List<ChiselDisplacementColumn> cut,
                             List<ChiselDisplacementColumn> pushed)
        {
            if (!cells.TryGetValue(cell, out var known) || !SameBits(known.heights, Round(grid, cell)))
                return false;
            raised.AddRange(known.raised);
            cut.AddRange(known.cut);
            pushed.AddRange(known.pushed);
            return true;
        }

        internal void Set(ChiselSurfacePaint grid, int2 cell, ChiselDisplacementColumn[] raised, ChiselDisplacementColumn[] cut,
                          ChiselDisplacementColumn[] pushed)
        {
            cells[cell] = new Cell { heights = Round(grid, cell), raised = raised, cut = cut, pushed = pushed };
        }
    }

    public static class ChiselDisplacement
    {
        /// <summary>Heights closer to 0 than this leave the surface as it is: a column's top tilted less than that over a
        /// cell is too close to the flat surface for the CSG to cut them apart cleanly</summary>
        public const float kFlat = 0.005f;
        /// <summary>How thin a displaced brush can get before its opposite side is pushed out</summary>
        public const float kMinThickness = 0.01f;
        /// <summary>The groups of columns, in the order the generator's branch takes them: what is raised (or flat), what
        /// is cut, what pushes the opposite side out</summary>
        public const int kRaised = 0, kLowered = 1, kPushed = 2, kGroupCount = 3;
        /// <summary>How far a column reaches past the surface into what it adds to or cuts from, so they meet without a
        /// gap; the generator's own brush has a displaced surface moved in by as much</summary>
        public const float kOverlap = 1e-3f;

        /// <summary>
        /// The columns of a displaced surface, in the order its generator's branch takes them: what is raised (or flat),
        /// then what is cut, then what pushes the opposite side out. normal points out of the surface; brushPlanes are the
        /// brush's own (its thickness under a point is how far along -normal its other planes are). The raised and the cut
        /// columns are prisms over the grid's triangles that nothing cuts: whatever bounds them (the brush's sides, the
        /// other displaced surfaces) is intersected with them in the generator's branch. Columns that share a face share
        /// its plane exactly, so the exact CSG sees one plane there. A triangle wholly beyond a side in endsAt (by plane,
        /// all sides when null) has nothing to displace; the columns that push out are cut to the sides. cache: the columns
        /// of the cells made before, kept for the next call (a cell is made again only when the heights round it changed).
        /// </summary>
        public static void Columns(ChiselSurfacePaint heights, Vector3 normal, IReadOnlyList<float4> brushPlanes, List<ChiselDisplacementColumn> columns,
                                   bool prepared = false, IReadOnlyList<bool> endsAt = null, ChiselDisplacementCache cache = null)
        {
            columns.Clear();
            if (heights == null || !heights.IsValid || heights.resolution.x < 2 || heights.resolution.y < 2)
                return;
            var n = (float3)normal.normalized;
            var sides = new List<int>();
            for (int i = 0; i < brushPlanes.Count; i++)
            {
                if (math.abs(math.dot(brushPlanes[i].xyz, n)) < 0.999f)
                    sides.Add(i);
            }
            if (!prepared)
                heights = ContinuedPastTheEdges(heights, brushPlanes, sides);
            var origin = PointOf(heights, int2.zero);
            var ends   = new List<int>();
            foreach (var side in sides)
            {
                if (endsAt == null || (side < endsAt.Count && endsAt[side]))
                    ends.Add(side);
            }
            var context = new Context
            {
                grid        = heights,
                n           = n,
                brushPlanes = brushPlanes,
                sides       = sides.ToArray(),
                ends        = ends.ToArray(),
                // the surface, where the raised columns start (just under it) and where the cut ones end (just over it)
                surface     = new float4( n, -math.dot(n, origin)),
                under       = new float4(-n,  math.dot(n, origin - n * kOverlap)),
                over        = new float4( n, -math.dot(n, origin + n * kOverlap)),
                acrossU     = math.normalize(math.cross((float3)heights.cellU, n)),
                acrossV     = math.normalize(math.cross((float3)heights.cellV, n)),
                acrossUV    = math.normalize(math.cross((float3)heights.cellU + (float3)heights.cellV, n))
            };
            var raised  = new List<ChiselDisplacementColumn>();
            var cut     = new List<ChiselDisplacementColumn>();
            var pushed  = new List<ChiselDisplacementColumn>();
            var size    = heights.resolution;
            var merged  = FlatRectangles(context, raised);
            cache?.Begin(context.grid, n, brushPlanes, context.ends);
            for (int j = 0; j + 1 < size.y; j++)
            {
                for (int i = 0; i + 1 < size.x; i++)
                {
                    if (merged[j * size.x + i])
                        continue;
                    var cell = new int2(i, j);
                    if (cache != null && cache.TryGet(heights, cell, raised, cut, pushed))
                        continue;
                    int r = raised.Count, c = cut.Count, u = pushed.Count;
                    Triangle(context, new int2(i, j), new int2(i + 1, j), new int2(i + 1, j + 1), raised, cut, pushed);
                    Triangle(context, new int2(i, j), new int2(i + 1, j + 1), new int2(i, j + 1), raised, cut, pushed);
                    cache?.Set(heights, cell, raised.GetRange(r, raised.Count - r).ToArray(), cut.GetRange(c, cut.Count - c).ToArray(),
                               pushed.GetRange(u, pushed.Count - u).ToArray());
                }
            }
            columns.AddRange(raised);
            columns.AddRange(cut);
            columns.AddRange(pushed);
        }

        /// <summary>The heights the columns are made from: those within kFlat of 0 are 0, and the points beyond the
        /// surface's edges have the height of the nearest point on it</summary>
        public static ChiselSurfacePaint Prepared(ChiselSurfacePaint heights, Vector3 normal, IReadOnlyList<float4> brushPlanes)
        {
            if (heights == null || !heights.IsValid)
                return heights;
            var n = (float3)normal.normalized;
            var sides = new List<int>();
            for (int i = 0; i < brushPlanes.Count; i++)
            {
                if (math.abs(math.dot(brushPlanes[i].xyz, n)) < 0.999f)
                    sides.Add(i);
            }
            return ContinuedPastTheEdges(heights, brushPlanes, sides);
        }

        /// <summary>
        /// The displaced surface's normal at a point (its position on the flat surface, along n), interpolated between
        /// the normals at the grid's points round it. Each of those is made from the slopes to the points next to it on
        /// either side, so none of the directions the cells are cut in is favoured. prepared: <see cref="Prepared"/>
        /// </summary>
        public static float3 NormalAt(ChiselSurfacePaint prepared, float3 n, float3 point)
        {
            var size = prepared.resolution;
            float3 u = prepared.cellU, v = prepared.cellV;
            var uu = math.dot(u, u); var uv = math.dot(u, v); var vv = math.dot(v, v);
            var determinant = uu * vv - uv * uv;
            if (!(math.abs(determinant) > 1e-12f) || size.x < 2 || size.y < 2)
                return n;
            var offset = point - (float3)prepared.origin;
            var du = math.dot(offset, u); var dv = math.dot(offset, v);
            var x = math.clamp((vv * du - uv * dv) / determinant, 0, size.x - 1);
            var y = math.clamp((uu * dv - uv * du) / determinant, 0, size.y - 1);
            var i = math.min((int)x, size.x - 2);
            var j = math.min((int)y, size.y - 2);
            var fx = x - i; var fy = y - j;
            var bottom = math.lerp(PointNormal(prepared, n, i, j),     PointNormal(prepared, n, i + 1, j),     fx);
            var top    = math.lerp(PointNormal(prepared, n, i, j + 1), PointNormal(prepared, n, i + 1, j + 1), fx);
            return math.normalizesafe(math.lerp(bottom, top, fy), n);
        }

        // The normal at a grid point, from the heights of the points on either side of it (or the one there is, at an edge)
        static float3 PointNormal(ChiselSurfacePaint grid, float3 n, int i, int j)
        {
            var size = grid.resolution;
            int i0 = math.max(i - 1, 0), i1 = math.min(i + 1, size.x - 1);
            int j0 = math.max(j - 1, 0), j1 = math.min(j + 1, size.y - 1);
            var alongU = (float3)grid.cellU * (i1 - i0) + n * (grid[i1, j].x - grid[i0, j].x);
            var alongV = (float3)grid.cellV * (j1 - j0) + n * (grid[i, j1].x - grid[i, j0].x);
            var normal = math.normalizesafe(math.cross(alongU, alongV), n);
            return math.dot(normal, n) < 0 ? -normal : normal;
        }

        struct Context
        {
            public ChiselSurfacePaint               grid;
            public float3                           n;
            public IReadOnlyList<float4>            brushPlanes;
            public int[]                            sides;
            // the sides beyond which a triangle has nothing to displace
            public int[]                            ends;
            public float4                           surface, under, over;
            // the directions out of the walls along the grid's lines and the cells' diagonals
            public float3                           acrossU, acrossV, acrossUV;
        }

        // The plane of a wall along the grid's line from a to b (neighbouring points, or a cell's diagonal), the same
        // for every column on that line: made from the line's first point, pointing either way
        static float4 LinePlane(in Context context, int2 a, int2 b)
        {
            var grid = context.grid;
            if (a.y == b.y)
                return Through(context.acrossU, PointOf(grid, new int2(0, a.y)));
            if (a.x == b.x)
                return Through(context.acrossV, PointOf(grid, new int2(a.x, 0)));
            // a diagonal from its first point on the grid's edge
            var start = math.min(a, b);
            return Through(context.acrossUV, PointOf(grid, start - math.min(start.x, start.y)));
        }

        static float4 Through(float3 normal, float3 point) => new float4(normal, -math.dot(normal, point));

        // The plane pointing away from a point inside: the plane as it is, or exactly reversed
        static float4 Outwards(float4 plane, float3 inside) => math.dot(plane.xyz, inside) + plane.w > 0 ? -plane : plane;

        // The heights the columns are made from: those within kFlat of 0 are 0, and the grid's points beyond the surface's
        // edges take the height of the nearest point on it, so the surface goes on past an edge as it is there, instead
        // of bending towards heights nothing was painted at
        static ChiselSurfacePaint ContinuedPastTheEdges(ChiselSurfacePaint grid, IReadOnlyList<float4> brushPlanes, List<int> sides)
        {
            const float kOnTheSide = 1e-4f;
            var size    = grid.resolution;
            var count   = size.x * size.y;
            var height  = new float[count];
            var known   = new bool[count];
            var queue   = new Queue<int>();
            for (int j = 0; j < size.y; j++)
            {
                for (int i = 0; i < size.x; i++)
                {
                    var at    = j * size.x + i;
                    var point = PointOf(grid, new int2(i, j));
                    var on    = true;
                    foreach (var side in sides)
                    {
                        var plane = brushPlanes[side];
                        if (math.dot(plane.xyz, point) + plane.w > kOnTheSide)
                        {
                            on = false;
                            break;
                        }
                    }
                    if (!on)
                        continue;
                    var h = grid[i, j].x;
                    height[at] = math.abs(h) <= kFlat ? 0 : h;
                    known[at]  = true;
                    queue.Enqueue(at);
                }
            }
            if (queue.Count == 0)
                return grid;

            // outwards from the surface, a ring of points at a time
            while (queue.Count > 0)
            {
                var at = queue.Dequeue();
                int i = at % size.x, j = at / size.x;
                for (int dj = -1; dj <= 1; dj++)
                {
                    for (int di = -1; di <= 1; di++)
                    {
                        int x = i + di, y = j + dj;
                        if (x < 0 || y < 0 || x >= size.x || y >= size.y || known[y * size.x + x])
                            continue;
                        known[y * size.x + x]  = true;
                        height[y * size.x + x] = height[at];
                        queue.Enqueue(y * size.x + x);
                    }
                }
            }
            var continued = ChiselSurfacePaint.Create(grid.surfaceIndex, grid.attribute, Core.ChiselPaintComponents.X, Vector4.zero,
                                                      grid.origin, grid.cellU, grid.cellV, size);
            for (int p = 0; p < count; p++)
                continued[p] = new Vector4(height[p], 0, 0, 0);
            return continued;
        }

        // The cells that are flat with nothing displaced round them (so their normals are the surface's) and well inside
        // the brush's sides, as rectangles of them, each one column (in raised); true by cell for those it took
        static bool[] FlatRectangles(in Context context, List<ChiselDisplacementColumn> raised)
        {
            var grid  = context.grid;
            var size  = grid.resolution;
            var cells = new int2(size.x - 1, size.y - 1);
            var flat  = new bool[size.x * size.y];
            var taken = new bool[size.x * size.y];
            for (int j = 0; j < cells.y; j++)
            {
                for (int i = 0; i < cells.x; i++)
                    flat[j * size.x + i] = Mergeable(context, i, j);
            }
            for (int j = 0; j < cells.y; j++)
            {
                for (int i = 0; i < cells.x; i++)
                {
                    if (!flat[j * size.x + i] || taken[j * size.x + i])
                        continue;
                    var right = i;
                    while (right + 1 < cells.x && flat[j * size.x + right + 1] && !taken[j * size.x + right + 1])
                        right++;
                    var top = j;
                    while (top + 1 < cells.y)
                    {
                        var row = true;
                        for (int x = i; x <= right && row; x++)
                            row = flat[(top + 1) * size.x + x] && !taken[(top + 1) * size.x + x];
                        if (!row)
                            break;
                        top++;
                    }
                    for (int y = j; y <= top; y++)
                        for (int x = i; x <= right; x++)
                            taken[y * size.x + x] = true;
                    var corners = new[] { new int2(i, j), new int2(right + 1, j), new int2(right + 1, top + 1), new int2(i, top + 1) };
                    var rectangle = FlatColumn(context, corners);
                    var points = new float3[8];
                    for (int c = 0; c < 4; c++)
                    {
                        points[c]     = PointOf(grid, corners[c]);
                        points[4 + c] = points[c] - context.n * kOverlap;
                    }
                    raised.Add(Keyed(CutToEnds(context, rectangle, points), 4, kRaised, 0));
                }
            }
            return taken;
        }

        // A cell is merged when the heights it and the cells round it are made from (its 4 by 4 points) are all 0, and
        // it is not wholly beyond a side it ends at
        static bool Mergeable(in Context context, int i, int j)
        {
            var grid = context.grid;
            var size = grid.resolution;
            for (int y = j - 1; y <= j + 2; y++)
            {
                for (int x = i - 1; x <= i + 2; x++)
                {
                    if (grid[math.clamp(x, 0, size.x - 1), math.clamp(y, 0, size.y - 1)].x != 0)
                        return false;
                }
            }
            return !Beyond(context, PointOf(grid, new int2(i, j)), PointOf(grid, new int2(i + 1, j)), PointOf(grid, new int2(i + 1, j + 1))) ||
                   !Beyond(context, PointOf(grid, new int2(i, j)), PointOf(grid, new int2(i + 1, j + 1)), PointOf(grid, new int2(i, j + 1)));
        }

        // Whether a triangle on the surface is wholly beyond a side it ends at
        static bool Beyond(in Context context, float3 a, float3 b, float3 c)
        {
            foreach (var side in context.ends)
            {
                var plane = context.brushPlanes[side];
                if (math.all(new float3(math.dot(plane.xyz, a), math.dot(plane.xyz, b), math.dot(plane.xyz, c)) + plane.w > 1e-4f))
                    return true;
            }
            return false;
        }

        // The flat column over a rectangle of cells (its corners in order round it), from just under the surface up to
        // it: its walls, then its top and its bottom
        static ChiselDisplacementColumn FlatColumn(in Context context, int2[] corners)
        {
            var grid   = context.grid;
            var count  = corners.Length;
            var planes = new float4[count + 2];
            var points = new float3[count];
            var centre = float3.zero;
            for (int e = 0; e < count; e++)
            {
                points[e] = PointOf(grid, corners[e]);
                centre   += points[e];
            }
            centre /= count;
            var min = points[0]; var max = points[0];
            for (int e = 0; e < count; e++)
            {
                planes[e] = Outwards(LinePlane(context, corners[e], corners[(e + 1) % count]), centre);
                min = math.min(min, math.min(points[e], points[e] - context.n * kOverlap));
                max = math.max(max, points[e]);
            }
            planes[count]     = context.surface;
            planes[count + 1] = context.under;
            return new ChiselDisplacementColumn { additive = true, planes = planes, ownPlanes = count + 2, boundsMin = min - 1e-3f, boundsMax = max + 1e-3f };
        }

        static float3 PointOf(ChiselSurfacePaint grid, int2 at)
        {
            return (float3)(grid.origin + at.x * grid.cellU + at.y * grid.cellV);
        }

        static float HeightOf(ChiselSurfacePaint grid, int2 at) => grid[at.x, at.y].x;

        // A corner of a part of a triangle: at a corner of the triangle (edge = -1), or on one of its edges
        struct PartCorner
        {
            public float3 point;
            public float  height;
            public int    corner, edge;
            // whether it is on the triangle's edge e, from its corner e to e + 1
            public readonly bool On(int e) => edge == e || (edge < 0 && (corner == e || (corner + 2) % 3 == e));
        }

        static void Triangle(in Context context, int2 ia, int2 ib, int2 ic,
                             List<ChiselDisplacementColumn> raised, List<ChiselDisplacementColumn> cut, List<ChiselDisplacementColumn> pushed)
        {
            var grid = context.grid;
            var p = new float3x3(PointOf(grid, ia), PointOf(grid, ib), PointOf(grid, ic));
            if (Beyond(context, p.c0, p.c1, p.c2))
                return;
            var index = new[] { ia, ib, ic };
            // the planes of its edges, from corner e to e + 1
            var edges = new float4[3];
            for (int e = 0; e < 3; e++)
                edges[e] = LinePlane(context, index[e], index[(e + 1) % 3]);
            var h = new float3(HeightOf(grid, ia), HeightOf(grid, ib), HeightOf(grid, ic));
            var around = Around(grid, math.min(ia, math.min(ib, ic)));
            if (!(math.any(h > kFlat) && math.any(h < -kFlat)))
            {
                Piece(context, p, h, edges, around, raised, cut, pushed);
                return;
            }

            // raised at a corner and lowered at another: the part raised and the part lowered, apart along where it is
            // at the surface, meet there (each clamped to the surface over the other part would leave a step between them)
            var all = new List<PartCorner>(5);
            for (int c = 0; c < 3; c++)
            {
                var d = (c + 1) % 3;
                all.Add(new PartCorner { point = p[c], height = h[c], corner = c, edge = -1 });
                if ((h[c] > 0 && h[d] < 0) || (h[c] < 0 && h[d] > 0))
                {
                    // from the edge's first grid point, so the triangle on its other side has the same point
                    int from = c, to = d;
                    if (index[d].y < index[c].y || (index[d].y == index[c].y && index[d].x < index[c].x))
                        (from, to) = (d, c);
                    all.Add(new PartCorner { point = math.lerp(p[from], p[to], h[from] / (h[from] - h[to])), height = 0, corner = -1, edge = c });
                }
            }
            // the line the two parts meet along, through the two points at the surface that aren't on one edge: one plane,
            // the one both parts' walls there are on
            var meet = float4.zero;
            for (int a = 0; a < all.Count && meet.Equals(float4.zero); a++)
            {
                for (int b = a + 1; b < all.Count && meet.Equals(float4.zero); b++)
                {
                    if (all[a].height != 0 || all[b].height != 0 || SharedEdge(all[a], all[b]) >= 0)
                        continue;
                    meet = Through(math.normalize(math.cross(all[b].point - all[a].point, context.n)), all[a].point);
                }
            }
            var part = new List<int>(4);
            for (int sign = 1; sign >= -1; sign -= 2)
            {
                part.Clear();
                for (int c = 0; c < all.Count; c++)
                {
                    if (all[c].height * sign >= 0)
                        part.Add(c);
                }
                // a triangle, or a four sided part as two (its displaced top is flat across both) apart along one diagonal
                var diagonal = part.Count == 4 ? Through(math.normalize(math.cross(all[part[2]].point - all[part[0]].point, context.n)), all[part[0]].point) : float4.zero;
                for (int k = 1; k + 1 < part.Count; k++)
                {
                    var corners = new[] { part[0], part[k], part[k + 1] };
                    var walls   = new float4[3];
                    for (int e = 0; e < 3; e++)
                    {
                        int from = corners[e], to = corners[(e + 1) % 3];
                        var on   = SharedEdge(all[from], all[to]);
                        if (on >= 0)
                            walls[e] = edges[on];
                        else if (part.Count == 4 && ((from == part[0] && to == part[2]) || (from == part[2] && to == part[0])))
                            walls[e] = diagonal;
                        else
                            walls[e] = meet;
                    }
                    Piece(context, new float3x3(all[corners[0]].point, all[corners[1]].point, all[corners[2]].point),
                          new float3(all[corners[0]].height, all[corners[1]].height, all[corners[2]].height), walls, around, raised, cut, pushed);
                }
            }
        }

        // The edge of the triangle both corners are on, or -1
        static int SharedEdge(in PartCorner a, in PartCorner b)
        {
            for (int e = 0; e < 3; e++)
            {
                if (a.On(e) && b.On(e))
                    return e;
            }
            return -1;
        }

        // The columns over a triangle on the surface, its heights at its corners all raised (or flat), or all lowered;
        // edges: the planes its walls are on, from corner e to e + 1, either way round
        static void Piece(in Context context, float3x3 p, float3 h, float4[] edges, uint around,
                          List<ChiselDisplacementColumn> raised, List<ChiselDisplacementColumn> cut, List<ChiselDisplacementColumn> pushed)
        {
            var n    = context.n;
            // a part of no area, where it touches the surface at a corner
            if (math.lengthsq(math.cross(p.c1 - p.c0, p.c2 - p.c0)) < 1e-12f)
                return;
            var centre = (p.c0 + p.c1 + p.c2) / 3;
            var walls  = new float4[3];
            for (int e = 0; e < 3; e++)
                walls[e] = Outwards(edges[e], centre);
            var up   = math.max(h, 0);
            var down = math.max(-h, 0);
            // the surface itself, from just under it: raised, or where it is lowered up to the surface, which what is cut
            // takes away from again (else where the lowered surface comes up to the surface the brush's own face, just
            // under it, would show)
            var top = math.all(up == 0) ? context.surface : Cap(p, n, up, true);
            var column = Column(p, n, -kOverlap, up, true, walls, top, context.under);
            raised.Add(Keyed(CutToEnds(context, column, column.corners), kTop, kRaised, around));
            if (!math.any(down > kFlat))
                return;
            // what is cut ends where what pushes out starts: one plane, either way round
            var bottom = Cap(p, n, -down, false);
            cut.Add(Keyed(Column(p, n, -down, kOverlap, false, walls, context.over, bottom), kBottom, kLowered, around));
            var brushPlanes = context.brushPlanes;
            var thickness = new float3(Thickness(p.c0, n, brushPlanes), Thickness(p.c1, n, brushPlanes), Thickness(p.c2, n, brushPlanes));
            if (!math.any(down + kMinThickness > thickness))
                return;
            var sidePlanes = new float4[context.sides.Length];
            for (int s = 0; s < sidePlanes.Length; s++)
                sidePlanes[s] = brushPlanes[context.sides[s]];
            pushed.Add(Keyed(Cut(Column(p, n, -(down + kMinThickness), -down, true, walls, -bottom, Cap(p, n, -(down + kMinThickness), false)),
                                 sidePlanes, context.sides), kBottom, kPushed, around));
        }

        const int kTop    = 3;
        const int kBottom = 4;

        static ChiselDisplacementColumn Keyed(ChiselDisplacementColumn column, int face, int group, uint around)
        {
            column.seenFace = face;
            column.group    = group;
            var key = math.hash(new uint3(around, (uint)face, (uint)group));
            foreach (var plane in column.planes)
                key = math.hash(new uint2(key, math.hash(plane)));
            column.key = key;
            return column;
        }

        // The heights of the 4 by 4 grid points round a cell (from its lowest corner), which the normals in it come from
        static uint Around(ChiselSurfacePaint grid, int2 cell)
        {
            var size = grid.resolution;
            var hash = 0u;
            for (int j = cell.y - 1; j <= cell.y + 2; j++)
            {
                for (int i = cell.x - 1; i <= cell.x + 2; i++)
                {
                    var height = grid[math.clamp(i, 0, size.x - 1), math.clamp(j, 0, size.y - 1)].x;
                    hash = math.hash(new uint2(hash, math.asuint(height)));
                }
            }
            return hash;
        }

        // A raised column cut to the brush's sides it ends at that it reaches past (points: its corners): past them there
        // is nothing. Those are the brush's own planes, so the column ends exactly where the brush does.
        static ChiselDisplacementColumn CutToEnds(in Context context, ChiselDisplacementColumn column, float3[] points)
        {
            List<int> crossing = null;
            foreach (var side in context.ends)
            {
                var plane  = context.brushPlanes[side];
                var length = math.length(plane.xyz);
                var inside = true;
                foreach (var point in points)
                    inside &= math.dot(plane.xyz, point) + plane.w < -kSideMargin * length;
                if (!inside)
                    (crossing ??= new List<int>()).Add(side);
            }
            if (crossing == null)
                return column;
            var sidePlanes = new float4[crossing.Count];
            for (int s = 0; s < sidePlanes.Length; s++)
                sidePlanes[s] = context.brushPlanes[crossing[s]];
            return Cut(column, sidePlanes, crossing.ToArray());
        }

        static ChiselDisplacementColumn Cut(ChiselDisplacementColumn column, float4[] sidePlanes, int[] sides)
        {
            var planes = new float4[column.planes.Length + sidePlanes.Length];
            column.planes.CopyTo(planes, 0);
            sidePlanes.CopyTo(planes, column.planes.Length);
            column.planes = planes;
            column.sides  = sides;
            return column;
        }

        static ChiselDisplacementColumn Column(float3x3 p, float3 n, float lower, float3 upper, bool additive, float4[] walls, float4 top, float4 bottom)
            => Column(p, n, new float3(lower), upper, additive, walls, top, bottom);

        static ChiselDisplacementColumn Column(float3x3 p, float3 n, float3 lower, float upper, bool additive, float4[] walls, float4 top, float4 bottom)
            => Column(p, n, lower, new float3(upper), additive, walls, top, bottom);

        // The column over triangle p (on the surface) along n, from lower to upper at each corner: its walls (from corner
        // e to e + 1), its top and its bottom, all pointing out of it
        static ChiselDisplacementColumn Column(float3x3 p, float3 n, float3 lower, float3 upper, bool additive, float4[] walls, float4 top, float4 bottom)
        {
            var planes = new[] { walls[0], walls[1], walls[2], top, bottom };
            var min = math.min(math.min(p.c0, p.c1), p.c2);
            var max = math.max(math.max(p.c0, p.c1), p.c2);
            for (int k = 0; k < 3; k++)
            {
                min = math.min(min, math.min(p[k] + n * lower[k], p[k] + n * upper[k]));
                max = math.max(max, math.max(p[k] + n * lower[k], p[k] + n * upper[k]));
            }
            var corners = new[] { p.c0 + n * lower.x, p.c1 + n * lower.y, p.c2 + n * lower.z,
                                  p.c0 + n * upper.x, p.c1 + n * upper.y, p.c2 + n * upper.z };
            return new ChiselDisplacementColumn { additive = additive, planes = planes, ownPlanes = 5, boundsMin = min - 1e-3f, boundsMax = max + 1e-3f, corners = corners };
        }

        /// <summary>The brush mesh of a column, its faces numbered by plane: made directly where it is the prism over its
        /// triangle with every corner well inside the brush's sides (most columns), else cut from its planes</summary>
        public static BrushMesh ColumnMesh(in ChiselDisplacementColumn column)
        {
            if (column.corners != null && column.ownPlanes == 5 && InsideSides(column))
                return Prism(column);
            var bounds = new Bounds();
            bounds.SetMinMax(column.boundsMin, column.boundsMax);
            var surfaces = new ChiselSurfaceArray();
            BrushMeshFactory.CreateFromPlanes(column.planes, bounds, ref surfaces, out var mesh);
            return mesh;
        }

        // How far inside every side of the brush a corner has to be for that side not to cut the column, nor to lie on
        // a face of it
        const float kSideMargin = 1e-4f;

        internal static bool InsideSides(in ChiselDisplacementColumn column)
        {
            for (int s = column.ownPlanes; s < column.planes.Length; s++)
            {
                var plane  = column.planes[s];
                var length = math.length(plane.xyz);
                if (!(length > 0))
                    return false;
                foreach (var corner in column.corners)
                {
                    if ((math.dot(plane.xyz, corner) + plane.w) / length > -kSideMargin)
                        return false;
                }
            }
            return true;
        }

        // The prism's faces by plane (the walls, from corner e to e + 1, then the top and the bottom), each wound
        // round its plane's normal
        static readonly int[][] kPrismFaces = { new[] { 0, 1, 4, 3 }, new[] { 1, 2, 5, 4 }, new[] { 2, 0, 3, 5 }, new[] { 3, 4, 5 }, new[] { 0, 1, 2 } };

        static BrushMesh Prism(in ChiselDisplacementColumn column)
        {
            var corners   = column.corners;
            var polygons  = new BrushMesh.Polygon[kPrismFaces.Length];
            var planes    = new float4[kPrismFaces.Length];
            var halfEdges = new BrushMesh.HalfEdge[18];
            var polygonOf = new int[18];
            var edge = 0;
            for (int f = 0; f < kPrismFaces.Length; f++)
            {
                var plane = column.planes[f];
                plane /= math.length(plane.xyz);
                planes[f] = plane;
                var face = kPrismFaces[f];
                // Newell's normal of the corners in order: they go round the plane's normal
                var normal = float3.zero;
                for (int k = 0; k < face.Length; k++)
                {
                    var a = corners[face[(k + face.Length - 1) % face.Length]];
                    var b = corners[face[k]];
                    normal += new float3((a.y - b.y) * (a.z + b.z), (a.z - b.z) * (a.x + b.x), (a.x - b.x) * (a.y + b.y));
                }
                var reversed = math.dot(normal, plane.xyz) < 0;
                polygons[f] = new BrushMesh.Polygon { firstEdge = edge, edgeCount = face.Length, descriptionIndex = f };
                for (int k = 0; k < face.Length; k++)
                {
                    halfEdges[edge] = new BrushMesh.HalfEdge { vertexIndex = face[reversed ? face.Length - 1 - k : k], twinIndex = -1 };
                    polygonOf[edge] = f;
                    edge++;
                }
            }
            // a half edge runs from the corner of the one before it to its own; its twin runs back
            for (int e = 0; e < halfEdges.Length; e++)
            {
                var polygon = polygons[polygonOf[e]];
                var from    = halfEdges[polygon.firstEdge + (e - polygon.firstEdge + polygon.edgeCount - 1) % polygon.edgeCount].vertexIndex;
                var to      = halfEdges[e].vertexIndex;
                for (int t = 0; t < halfEdges.Length && halfEdges[e].twinIndex < 0; t++)
                {
                    var other = polygons[polygonOf[t]];
                    var otherFrom = halfEdges[other.firstEdge + (t - other.firstEdge + other.edgeCount - 1) % other.edgeCount].vertexIndex;
                    if (halfEdges[t].vertexIndex == from && otherFrom == to)
                        halfEdges[e].twinIndex = t;
                }
            }
            return new BrushMesh
            {
                vertices               = (float3[])corners.Clone(),
                halfEdges              = halfEdges,
                halfEdgePolygonIndices = polygonOf,
                polygons               = polygons,
                planes                 = planes
            };
        }

        // The plane through the triangle's corners moved along n by offset, pointing along n (top) or against it
        static float4 Cap(float3x3 p, float3 n, float3 offset, bool top)
        {
            var a = p.c0 + n * offset.x;
            var b = p.c1 + n * offset.y;
            var c = p.c2 + n * offset.z;
            var normal = math.normalizesafe(math.cross(b - a, c - a), n);
            if ((math.dot(normal, n) > 0) != top)
                normal = -normal;
            return new float4(normal, -math.dot(normal, a));
        }

        /// <summary>How far along -n from point (inside the brush, on its surface) the brush's other side is</summary>
        public static float Thickness(float3 point, float3 n, IReadOnlyList<float4> brushPlanes)
        {
            var thickness = float.PositiveInfinity;
            for (int i = 0; i < brushPlanes.Count; i++)
            {
                var plane  = brushPlanes[i];
                var facing = math.dot(plane.xyz, n);
                if (!(facing < -1e-6f))
                    continue;
                // dot(plane, point - n s) + w = 0
                var s = (math.dot(plane.xyz, point) + plane.w) / facing;
                thickness = math.min(thickness, math.max(s, 0));
            }
            return thickness;
        }
    }
}
