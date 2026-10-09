using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;

namespace Chisel.Core
{
    // A border that follows what a surface draws: a strip along every edge of its outline (its holes too), each mapped to
    // a side of the tile, as deep into the tile as it is into the surface; a corner where two strips meet at a convex
    // corner; at a reflex corner the strips meet on the line through it and its inner corner. The rest is the interior;
    // a surface whose bounds are too small for the border to leave one is all border. The interior maps as the surface's
    // bounds do, undistorted, whatever its outline.
    public partial struct HotspotSections
    {
        // The loops of a surface's outline in its frame, the surface on their left: corners, and (start, count) per loop.
        // Points on a straight edge are no corners. An edge with another's corner on it (where surfaces of two brushes meet)
        // is taken in parts, so the edges both have cancel.
        public static void OutlineOf(NativeList<int> indices, NativeList<float3> positions, float3 right, float3 up,
                                     NativeList<double2> corners, NativeList<int2> loops)
        {
            OutlineOf(indices, positions, right, up, default, corners, loops, default);
        }

        // The same, with the edges given a border or none by hand (rules): an edge both have (where surfaces mapped as one
        // meet) is on the outline when a rule puts a border along it; bordered, when created, tells for each corner
        // whether the edge from it to the next has a border. A corner where that changes stays, straight or not. A corner
        // closer than tolerance to the line between its neighbours is none either (the CSG can leave a corner cut off by a
        // tenth of a millimetre, as where a capsule's side meets its cap: a border along it would be micro triangles).
        public static void OutlineOf(NativeList<int> indices, NativeList<float3> positions, float3 right, float3 up, NativeArray<HotspotBorderCorner> rules,
                                     NativeList<double2> corners, NativeList<int2> loops, NativeList<bool> bordered, double tolerance = 0)
        {
            corners.Clear();
            loops.Clear();
            if (bordered.IsCreated)
                bordered.Clear();
            var r = (double3)right; var u = (double3)up;
            var ids      = new UnsafeHashMap<int3, int>(positions.Length, Allocator.Temp);
            var where    = new NativeList<float3>(positions.Length, Allocator.Temp);
            var vertexId = new NativeList<int>(positions.Length, Allocator.Temp);
            for (int v = 0; v < positions.Length; v++)
            {
                var p = positions[v] + 0.0f;
                var key = math.asint(p);
                if (!ids.TryGetValue(key, out var id))
                {
                    id = where.Length;
                    ids.Add(key, id);
                    where.Add(p);
                }
                vertexId.Add(id);
            }
            double2 Frame(float3 p) => new double2(math.dot(r, (double3)p), math.dot(u, (double3)p));

            // (corner, rule) -> border, the edges of single edge rules (from, to in the frame; rule, border), and the rules
            // newest first
            var ruled    = new UnsafeHashMap<int2, int>(rules.IsCreated ? math.max(1, rules.Length) : 1, Allocator.Temp);
            var segments = new NativeList<double4>(rules.IsCreated ? math.max(1, rules.Length) : 1, Allocator.Temp);
            var segmentOf = new NativeList<int2>(rules.IsCreated ? math.max(1, rules.Length) : 1, Allocator.Temp);
            var newest   = new NativeList<int>(4, Allocator.Temp);
            for (int i = 0; rules.IsCreated && i < rules.Length; i++)
            {
                if (rules[i].segment)
                {
                    segments.Add(new double4(Frame(rules[i].position), Frame(rules[i].end)));
                    segmentOf.Add(new int2(rules[i].rule, rules[i].border));
                } else
                {
                    if (!ids.TryGetValue(math.asint(rules[i].position + 0.0f), out var id))
                        continue;
                    ruled.TryAdd(new int2(id, rules[i].rule), rules[i].border);
                }
                if (!newest.Contains(rules[i].rule))
                    newest.Add(rules[i].rule);
            }
            newest.Sort();
            // a point of the outline this close to a single edge rule's edge is on it (the edge comes from the brush, the
            // outline from the CSG): the larger of tolerance and a few float steps
            var extent = 0.0;
            for (int i = 0; i < where.Length; i++)
                extent = math.max(extent, math.cmax(math.abs(Frame(where[i]))));
            var alongEdge = math.max(tolerance, 1e-6 * math.max(1, extent));
            int BorderOf(int a, int b)
            {
                for (int i = newest.Length - 1; i >= 0; i--)
                {
                    if (ruled.TryGetValue(new int2(a, newest[i]), out var border) && ruled.ContainsKey(new int2(b, newest[i])))
                        return border;
                    for (int k = 0; k < segments.Length; k++)
                    {
                        if (segmentOf[k].x == newest[i] &&
                            Deviation(segments[k].xy, Frame(where[a]), segments[k].zw) <= alongEdge &&
                            Deviation(segments[k].xy, Frame(where[b]), segments[k].zw) <= alongEdge)
                            return segmentOf[k].y;
                    }
                }
                return 0;
            }

            // the triangles' winding in the frame
            var area = 0.0;
            for (int t = 0; t + 2 < indices.Length; t += 3)
            {
                var a = Frame(positions[indices[t]]); var b = Frame(positions[indices[t + 1]]); var c = Frame(positions[indices[t + 2]]);
                area += Cross(b - a, c - a);
            }
            var flip = area < 0;

            var edges = new UnsafeHashMap<int2, int>(indices.Length, Allocator.Temp);
            var on    = new NativeList<double2>(4, Allocator.Temp);   // (where along, corner)
            void Add(int a, int b)
            {
                var edge = flip ? new int2(b, a) : new int2(a, b);
                edges.TryGetValue(edge, out var count);
                edges.Remove(edge);
                edges.Add(edge, count + 1);
            }
            for (int t = 0; t + 2 < indices.Length; t += 3)
            {
                for (int k = 0; k < 3; k++)
                {
                    var a = vertexId[indices[t + k]]; var b = vertexId[indices[t + (k + 1) % 3]];
                    if (a == b)
                        continue;
                    // the corners on it, in order
                    var pa = Frame(where[a]); var ab = Frame(where[b]) - pa;
                    var length = math.lengthsq(ab);
                    on.Clear();
                    for (int c = 0; c < where.Length && length > 0; c++)
                    {
                        if (c == a || c == b)
                            continue;
                        var ac = Frame(where[c]) - pa;
                        var along = math.dot(ac, ab) / length;
                        var off = Cross(ab, ac);
                        if (along > 1e-9 && along < 1 - 1e-9 && off * off <= 1e-12 * length * length)
                            on.Add(new double2(along, c));
                    }
                    for (int i = 1; i < on.Length; i++)
                    {
                        var item = on[i]; var j = i - 1;
                        while (j >= 0 && on[j].x > item.x) { on[j + 1] = on[j]; j--; }
                        on[j + 1] = item;
                    }
                    var from = a;
                    for (int i = 0; i < on.Length; i++)
                    {
                        Add(from, (int)on[i].y);
                        from = (int)on[i].y;
                    }
                    Add(from, b);
                }
            }
            on.Dispose();
            // an edge without its reverse is on the outline; where two loops touch at a corner (the steps of a spiral
            // staircase) that corner has two ways on
            var next    = new UnsafeParallelMultiHashMap<int, int>(indices.Length, Allocator.Temp);
            var outline = new NativeList<int2>(indices.Length, Allocator.Temp);
            using (var all = edges.GetKeyArray(Allocator.Temp))
            {
                for (int e = 0; e < all.Length; e++)
                {
                    var edge = all[e];
                    if (edges.ContainsKey(edge.yx) && !(BorderOf(edge.x, edge.y) > 0))
                        continue;
                    next.Add(edge.x, edge.y);
                    outline.Add(edge);
                }
            }
            outline.Sort(new EdgeOrder());
            var loop  = new NativeList<int>(16, Allocator.Temp);
            var state = new NativeList<int>(16, Allocator.Temp);
            var used  = new UnsafeHashSet<int2>(outline.Length, Allocator.Temp);
            {
                for (int s = 0; s < outline.Length; s++)
                {
                    var first = outline[s];
                    if (used.Contains(first))
                        continue;
                    loop.Clear();
                    used.Add(first);
                    loop.Add(first.x);
                    var from = first.x; var current = first.y;
                    var closed = false;
                    for (int guard = 0; guard < outline.Length; guard++)
                    {
                        if (current == first.x) { closed = true; break; }
                        loop.Add(current);
                        // of the ways on, the one that turns furthest left keeps to this loop (the surface is on its left)
                        var incoming = Frame(where[current]) - Frame(where[from]);
                        var best = -1; var bestTurn = double.NegativeInfinity;
                        if (next.TryGetFirstValue(current, out var to, out var iterator))
                        {
                            do
                            {
                                // never straight back along an edge both surfaces keep
                                if (to == from || used.Contains(new int2(current, to)))
                                    continue;
                                var outgoing = Frame(where[to]) - Frame(where[current]);
                                var turn = math.atan2(Cross(incoming, outgoing), math.dot(incoming, outgoing));
                                if (turn > bestTurn) { bestTurn = turn; best = to; }
                            } while (next.TryGetNextValue(out to, ref iterator));
                        }
                        if (best < 0)
                            break;
                        used.Add(new int2(current, best));
                        from = current;
                        current = best;
                    }
                    if (!closed)
                        continue;
                    // each edge's border by hand, from each corner to the next
                    state.Clear();
                    for (int k = 0; k < loop.Length; k++)
                        state.Add(BorderOf(loop[k], loop[(k + 1) % loop.Length]));
                    // no corner where it goes straight on, unless the border changes there
                    for (bool removed = true; removed && loop.Length > 3;)
                    {
                        removed = false;
                        for (int k = 0; k < loop.Length && loop.Length > 3; k++)
                        {
                            var h = (k + loop.Length - 1) % loop.Length;
                            var previous = where[loop[h]];
                            var here     = where[loop[k]];
                            var after    = where[loop[(k + 1) % loop.Length]];
                            if (state[h] != state[k] ||
                                !(OutputWeld.OnOneLine(previous, here, after) || Deviation(Frame(previous), Frame(here), Frame(after)) <= tolerance))
                                continue;
                            loop.RemoveAt(k);
                            state.RemoveAt(k);
                            removed = true;
                            k--;
                        }
                    }
                    if (loop.Length < 3)
                        continue;
                    loops.Add(new int2(corners.Length, loop.Length));
                    for (int k = 0; k < loop.Length; k++)
                    {
                        corners.Add(Frame(where[loop[k]]));
                        if (bordered.IsCreated)
                            bordered.Add(state[k] >= 0);
                    }
                }
            }
            loop.Dispose();
            state.Dispose();
            used.Dispose();
            outline.Dispose();
            newest.Dispose();
            segments.Dispose();
            segmentOf.Dispose();
            ruled.Dispose();
            next.Dispose();
            edges.Dispose();
            vertexId.Dispose();
            where.Dispose();
            ids.Dispose();
        }

        // How far a point is from the segment between two others
        static double Deviation(double2 a, double2 p, double2 b)
        {
            var ab = b - a;
            var length = math.lengthsq(ab);
            var t = length > 0 ? math.saturate(math.dot(p - a, ab) / length) : 0;
            return math.distance(p, a + ab * t);
        }

        struct EdgeOrder : System.Collections.Generic.IComparer<int2>
        {
            public readonly int Compare(int2 a, int2 b) => a.x != b.x ? a.x.CompareTo(b.x) : a.y.CompareTo(b.y);
        }

        // A tile's side (of t) whose outside faces this way in the tile
        static int SideFacing(in Outline t, double2 direction)
        {
            var best = 0; var bestDot = double.NegativeInfinity;
            direction = math.normalizesafe(direction);
            for (int j = 0; j < t.count; j++)
            {
                var facing = -math.dot(direction, t.inward[j]);
                if (facing > bestDot) { bestDot = facing; best = j; }
            }
            return best;
        }

        // The pixel a point of the band takes: the interior's, with its depth below side j of the tile replaced by depth
        static double2 WithDepth(double2 pixel, in Outline t, int j, double depth)
        {
            return pixel - t.inward[j] * math.dot(t.inward[j], pixel - t.corner[j]) + t.inward[j] * depth;
        }

        // The border along the outline of what the surface draws (indices, positions); false when it has none
        public bool BuildConforming(in HotspotSectionInput input, NativeList<int> indices, NativeList<float3> positions)
        {
            return BuildConforming(input, indices, positions, default);
        }

        // The same, with the border put along an edge or left out by hand (rules, OutlineOf): where an edge has none the
        // strips beside it run on to it, and the interior reaches it
        public bool BuildConforming(in HotspotSectionInput input, NativeList<int> indices, NativeList<float3> positions, NativeArray<HotspotBorderCorner> rules)
        {
            lines.Clear();
            halfPlanes.Clear();
            cells.Clear();
            interiorCell = -1;
            interiorToEdges = false;
            openSides = 0;
            if (input.count != 4 || !(input.pixelsPerUnit > 0) || input.Repeated)
                return false;
            var s = new Outline(4, input.f0, input.f1, input.f2);
            var t = new Outline(4, input.t0, input.t1, input.t2);
            if (!Bands(input, s, t, out var surfaceBand, out var tileBand))
                return false;
            var allBorder = surfaceBand >= s.MaxBand();

            using var corners  = new NativeList<double2>(32, Allocator.Temp);
            using var loops    = new NativeList<int2>(4, Allocator.Temp);
            using var bordered = new NativeList<bool>(32, Allocator.Temp);
            // a corner less than a quarter of a pixel off the line through its neighbours makes no difference to be seen
            OutlineOf(indices, positions, input.right, input.up, rules, corners, loops, bordered, 0.25 / input.pixelsPerUnit);
            if (loops.Length == 0)
                return false;
            if (!allBorder)
                openSides = OpenSides(s, corners, loops, bordered, out interiorToEdges);

            s.Inset(surfaceBand, openSides);
            t.Inset(tileBand);
            var size = (double2)input.textureSize;
            var depthScale = tileBand / surfaceBand;

            // the interior as the bounds map it: the surface's inset bounds (out to the bounds where they have no border)
            // onto the tile's, in pixels; across a surface that is all border, where the inset bounds have no width, at the
            // border's density
            var origin = s.inner[0];
            var acrossS = s.inner[1] - origin; var acrossT = t.inner[1] - t.inner[0];
            var upS     = s.inner[3] - origin; var upT     = t.inner[3] - t.inner[0];
            if (allBorder && s.Closed(0, surfaceBand)) { acrossS = s.along[0];  acrossT = t.along[0] * depthScale; }
            if (allBorder && s.Closed(3, surfaceBand)) { upS     = -s.along[3]; upT     = -t.along[3] * depthScale; }
            var linear = math.mul(new double2x2(acrossT, upT), math.inverse(new double2x2(acrossS, upS)));
            var pixelOrigin = t.inner[0];
            double2 Interior(double2 p) => pixelOrigin + math.mul(linear, p - origin);

            using var along    = new NativeList<double2>(corners.Length, Allocator.Temp);
            using var inward   = new NativeList<double2>(corners.Length, Allocator.Temp);
            using var inner    = new NativeList<double2>(corners.Length, Allocator.Temp);
            using var convex   = new NativeList<bool>(corners.Length, Allocator.Temp);
            using var outer    = new NativeList<int>(corners.Length, Allocator.Temp);
            using var inset    = new NativeList<int>(corners.Length, Allocator.Temp);
            var side           = new NativeList<int>(corners.Length, Allocator.Temp);
            var starts         = new NativeList<int2>(corners.Length, Allocator.Temp);   // (line, side) where a strip starts
            var ends           = new NativeList<int2>(corners.Length, Allocator.Temp);   // and where it ends
            for (int l = 0; l < loops.Length; l++)
            {
                var first = loops[l].x; var count = loops[l].y;
                int At(int k) => first + (k % count + count) % count;
                double Band(int k) => bordered[At(k)] ? surfaceBand : 0;
                along.Clear(); inward.Clear(); inner.Clear(); convex.Clear(); outer.Clear(); inset.Clear(); side.Clear(); starts.Clear(); ends.Clear();
                for (int k = 0; k < count; k++)
                {
                    var d = math.normalize(corners[At(k + 1)] - corners[At(k)]);
                    var n = new double2(-d.y, d.x);
                    along.Add(d);
                    inward.Add(n);
                    outer.Add(AddLine(n, corners[At(k)]));
                    inset.Add(AddLine(n, corners[At(k)] + n * Band(k)));
                    side.Add(SideFacing(t, math.mul(linear, -n)));
                    starts.Add(default);
                    ends.Add(default);
                }
                // each corner k, between edge k - 1 and edge k: its inner corner
                for (int k = 0; k < count; k++)
                {
                    var h = (k + count - 1) % count;
                    var c = corners[At(k)];
                    var nh = inward[h]; var nk = inward[k];
                    var bh = Band(h); var bk = Band(k);
                    var determinant = Cross(nh, nk);
                    var point = math.abs(determinant) > 0
                              ? new double2(((math.dot(nh, c) + bh) * nk.y - nh.y * (math.dot(nk, c) + bk)) / determinant,
                                            (nh.x * (math.dot(nk, c) + bk) - (math.dot(nh, c) + bh) * nk.x) / determinant)
                              : c + nk * bk;
                    inner.Add(point);
                    convex.Add(Cross(along[h], along[k]) > 0);
                }

                for (int k = 0; k < count; k++)
                {
                    var h = (k + count - 1) % count;
                    var c = corners[At(k)];
                    var point = inner[k];
                    var nk = inward[k];
                    if (math.abs(Cross(along[h], along[k])) < 1e-9 && math.dot(along[h], along[k]) > 0)
                    {
                        // straight on, where the border starts or stops: square to the edge
                        var square = AddLine(along[k], c);
                        ends[h]   = new int2(square, -1);
                        starts[k] = new int2(square, 1);
                    } else if (!bordered[At(h)] || !bordered[At(k)])
                    {
                        // a strip beside an edge without a border runs on to that edge (inside it at a convex corner,
                        // outside it at a reflex one)
                        var inside = convex[k] ? 1 : -1;
                        ends[h]   = new int2(outer[k], inside);
                        starts[k] = new int2(outer[h], inside);
                    } else if (convex[k])
                    {
                        // square to each edge through the inner corner, the corner's side negative
                        ends[h]   = new int2(AddLine(-along[h], point), 1);
                        starts[k] = new int2(AddLine(along[k], point), 1);
                    } else
                    {
                        // on the line from the corner to its inner corner, the next edge's side positive
                        var m = math.normalizesafe(point - c, nk);
                        var normal = new double2(-m.y, m.x);
                        if (math.dot(normal, along[k]) < 0)
                            normal = -normal;
                        var miter = AddLine(normal, c);
                        ends[h]   = new int2(miter, -1);
                        starts[k] = new int2(miter, 1);
                    }
                }

                for (int k = 0; k < count; k++)
                {
                    if (!bordered[At(k)])
                        continue;
                    var a = corners[At(k)];
                    var b = corners[At(k + 1)];
                    var d = along[k]; var n = inward[k]; var j = side[k];
                    var depthEdge = k;
                    double2 Strip(double2 p) => WithDepth(Interior(p), t, side[depthEdge], math.dot(inward[depthEdge], p - corners[At(depthEdge)]) * depthScale);

                    // where along the edge the strip runs, between its corners' sections
                    var from = convex[k] ? math.dot(d, inner[k] - a) : 0;
                    var to   = convex[(k + 1) % count] ? math.dot(d, inner[(k + 1) % count] - a) : math.length(b - a);
                    var period  = math.length(t.footPrevious[(j + 1) % t.count] - t.footNext[j]) / depthScale;
                    var repeats = StripRepeats(input, j, to - from, period, out var step);
                    var previousCut = starts[k];
                    for (int rep = 0; rep < repeats; rep++)
                    {
                        var cut = rep + 1 < repeats ? new int2(AddLine(d, a + d * (from + step * (rep + 1))), -1) : ends[k];
                        var start = halfPlanes.Length;
                        Half(outer[k], 1);
                        Half(inset[k], -1);
                        Half(previousCut.x, previousCut.y);
                        Half(cut.x, cut.y);
                        if (repeats == 1)
                            AddCell(start, a, a + d, a + n * surfaceBand, Strip(a), Strip(a + d), Strip(a + n * surfaceBand), size);
                        else
                        {
                            // each repeat is the tile's whole strip on that side, which way round its edge runs
                            var nextSide = (j + 1) % t.count;
                            var forward = math.dot(math.mul(linear, d), t.along[j]) >= 0;
                            var p0 = a + d * (from + step * rep);
                            AddCell(start, p0, p0 + d * step, p0 + n * surfaceBand,
                                    forward ? t.footNext[j] : t.footPrevious[nextSide], forward ? t.footPrevious[nextSide] : t.footNext[j],
                                    forward ? t.inner[j] : t.inner[nextSide], size);
                        }
                        previousCut = new int2(cut.x, -cut.y);
                    }
                }

                // the corners
                for (int k = 0; k < count; k++)
                {
                    var h = (k + count - 1) % count;
                    if (!convex[k] || !bordered[At(h)] || !bordered[At(k)])
                        continue;
                    var c = corners[At(k)];
                    var hEdge = h; var kEdge = k;
                    double Depth(double2 p, int edge) => math.dot(inward[edge], p - corners[At(edge)]) * depthScale;
                    var jh = side[h]; var jk = side[k];
                    if (jh != jk && math.abs(math.dot(t.inward[jh], t.inward[jk])) < 1e-9)
                    {
                        double2 Corner(double2 p) => WithDepth(WithDepth(Interior(p), t, jh, Depth(p, hEdge)), t, jk, Depth(p, kEdge));
                        var start = halfPlanes.Length;
                        Half(outer[h], 1);
                        Half(outer[k], 1);
                        Half(ends[h].x, -ends[h].y);
                        Half(starts[k].x, -starts[k].y);
                        AddCell(start, c, c + along[k], c - along[h], Corner(c), Corner(c + along[k]), Corner(c - along[h]), size);
                    } else
                    {
                        // both edges face the same side (or opposite ones): each half of the corner as its edge's strip
                        var toInner = math.normalizesafe(inner[k] - c, inward[k]);
                        var diagonal = AddLine(new double2(-toInner.y, toInner.x), c);
                        var towardK = lines[diagonal].Value(c + along[k]) > 0 ? 1 : -1;
                        var start = halfPlanes.Length;
                        Half(outer[h], 1); Half(outer[k], 1); Half(ends[h].x, -ends[h].y); Half(starts[k].x, -starts[k].y); Half(diagonal, towardK);
                        double2 StripOf(double2 p, int edge) => WithDepth(Interior(p), t, side[edge], Depth(p, edge));
                        AddCell(start, c, c + along[k], c + inward[k], StripOf(c, kEdge), StripOf(c + along[k], kEdge), StripOf(c + inward[k], kEdge), size);
                        start = halfPlanes.Length;
                        Half(outer[h], 1); Half(outer[k], 1); Half(ends[h].x, -ends[h].y); Half(starts[k].x, -starts[k].y); Half(diagonal, -towardK);
                        AddCell(start, c, c - along[h], c + inward[h], StripOf(c, hEdge), StripOf(c - along[h], hEdge), StripOf(c + inward[h], hEdge), size);
                    }
                }
            }
            starts.Dispose();
            ends.Dispose();
            side.Dispose();

            // the interior: wherever no band section is, unless it is all border; inside every inset of a convex outline
            if (!allBorder)
            {
                var start = halfPlanes.Length;
                if (loops.Length == 1 && Convex(corners, loops[0]))
                {
                    for (int k = 0; k < inset.Length; k++)
                        Half(inset[k], 1);
                }
                interiorCell = cells.Length;
                AddCell(start, s.inner[0], s.inner[1], s.inner[3], t.inner[0], t.inner[1], t.inner[3], size);
            }
            return true;
        }

        // The repeats of a bordered tile's interior (the interior repeats at the tile's density, the last
        // repeat along each axis cut off where the surface ends): cells over the whole plane between the repeats' seams,
        // from the inset bounds' first corner, each repeat onto the tile's inner rectangle. Where edges have no border
        // (openSides, interiorToEdges) the inset bounds reach the bounds on those sides, and the repeats go on over the
        // border's width too. The surface's Offset moves the repeats along (interiorOffset), as it moves a repeating
        // tile's. False when the surface is all border or would take more than kMaxRepeats.
        public bool BuildInteriorRepeats(in HotspotSectionInput input, int openSides = 0, bool toEdges = false)
        {
            lines.Clear();
            halfPlanes.Clear();
            cells.Clear();
            interiorCell = -1;
            if (input.count != 4 || !(input.pixelsPerUnit > 0) || input.Repeated)
                return false;
            var s = new Outline(4, input.f0, input.f1, input.f2);
            var t = new Outline(4, input.t0, input.t1, input.t2);
            if (!Bands(input, s, t, out var surfaceBand, out var tileBand) || surfaceBand >= s.MaxBand())
                return false;
            s.Inset(surfaceBand, openSides);
            t.Inset(tileBand);
            var period = new double2(math.length(t.inner[1] - t.inner[0]), math.length(t.inner[3] - t.inner[0])) / input.pixelsPerUnit;
            if (!(period.x > 0) || !(period.y > 0))
                return false;
            var shift  = period * (double2)input.interiorOffset;
            var origin = s.inner[0] - shift;
            var extent = new double2(math.length(s.inner[1] - s.inner[0]), math.length(s.inner[3] - s.inner[0]));
            var band   = toEdges ? (double)surfaceBand : 0;
            // the repeats' cells, i from x0 up to x1 and j from y0 up to y1, from where the band starts to where it ends
            var x0 = (int)math.floor((shift.x - band) / period.x + 1e-9);
            var y0 = (int)math.floor((shift.y - band) / period.y + 1e-9);
            var x1 = math.max(x0 + 1, (int)math.ceil((extent.x + band + shift.x) / period.x - 1e-9));
            var y1 = math.max(y0 + 1, (int)math.ceil((extent.y + band + shift.y) / period.y - 1e-9));
            if ((long)(x1 - x0) * (y1 - y0) > input.MaxRepeats)
                return false;
            var firstX = lines.Length - x0 - 1;
            for (int i = x0 + 1; i < x1; i++)
                AddLine(new double2(1, 0), origin + new double2(i * period.x, 0));
            var firstY = lines.Length - y0 - 1;
            for (int j = y0 + 1; j < y1; j++)
                AddLine(new double2(0, 1), origin + new double2(0, j * period.y));
            var size = (double2)input.textureSize;
            for (int j = y0; j < y1; j++)
            {
                for (int i = x0; i < x1; i++)
                {
                    var start = halfPlanes.Length;
                    if (i > x0)     Half(firstX + i, 1);
                    if (i < x1 - 1) Half(firstX + i + 1, -1);
                    if (j > y0)     Half(firstY + j, 1);
                    if (j < y1 - 1) Half(firstY + j + 1, -1);
                    var corner = origin + new double2(i * period.x, j * period.y);
                    AddCell(start, corner, corner + new double2(period.x, 0), corner + new double2(0, period.y), t.inner[0], t.inner[1], t.inner[3], size);
                }
            }
            return true;
        }

        // The sides of the bounds (s, a bit each) the outline runs along only where it has no border; toEdges: some edge
        // of it has none
        static int OpenSides(in Outline s, NativeList<double2> corners, NativeList<int2> loops, NativeList<bool> bordered, out bool toEdges)
        {
            toEdges = false;
            var open = 0; var closed = 0;
            var tolerance = 1e-5 * math.max(1, math.length(s.corner[2] - s.corner[0]));
            for (int l = 0; l < loops.Length; l++)
            {
                var first = loops[l].x; var count = loops[l].y;
                for (int k = 0; k < count; k++)
                {
                    var a = corners[first + k]; var b = corners[first + (k + 1) % count];
                    toEdges |= !bordered[first + k];
                    for (int j = 0; j < 4; j++)
                    {
                        if (math.abs(math.dot(s.inward[j], a - s.corner[j])) > tolerance || math.abs(math.dot(s.inward[j], b - s.corner[j])) > tolerance)
                            continue;
                        if (bordered[first + k]) closed |= 1 << j;
                        else                     open   |= 1 << j;
                    }
                }
            }
            return open & ~closed;
        }

        static bool Convex(NativeList<double2> corners, int2 loop)
        {
            for (int k = 0; k < loop.y; k++)
            {
                var a = corners[loop.x + (k + loop.y - 1) % loop.y];
                var b = corners[loop.x + k];
                var c = corners[loop.x + (k + 1) % loop.y];
                if (!(Cross(b - a, c - b) > 0))
                    return false;
            }
            return true;
        }

    }
}
