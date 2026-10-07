using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;

namespace Chisel.Core
{
    // A border that follows what a surface draws: a strip along every edge of its outline (its holes too), each mapped to
    // the side of the tile its outside faces, as deep into the tile as it is into the surface; a corner where two strips
    // meet at a convex corner; at a reflex corner the strips meet on the line that halves it. The rest is the interior,
    // mapped as the surface's bounds are.
    public partial struct HotspotSections
    {
        // The loops of a surface's outline in its frame, the surface on their left: corners, and (start, count) per loop.
        // Points on a straight edge are no corners.
        public static void OutlineOf(NativeList<int> indices, NativeList<float3> positions, float3 right, float3 up,
                                     NativeList<double2> corners, NativeList<int2> loops)
        {
            corners.Clear();
            loops.Clear();
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

            // the triangles' winding in the frame
            var area = 0.0;
            for (int t = 0; t + 2 < indices.Length; t += 3)
            {
                var a = Frame(positions[indices[t]]); var b = Frame(positions[indices[t + 1]]); var c = Frame(positions[indices[t + 2]]);
                area += Cross(b - a, c - a);
            }
            var flip = area < 0;

            var edges = new UnsafeHashMap<int2, int>(indices.Length, Allocator.Temp);
            for (int t = 0; t + 2 < indices.Length; t += 3)
            {
                for (int k = 0; k < 3; k++)
                {
                    var a = vertexId[indices[t + k]]; var b = vertexId[indices[t + (k + 1) % 3]];
                    if (a == b)
                        continue;
                    var edge = flip ? new int2(b, a) : new int2(a, b);
                    edges.TryGetValue(edge, out var count);
                    edges.Remove(edge);
                    edges.Add(edge, count + 1);
                }
            }
            // an edge without its reverse is on the outline
            var next = new UnsafeHashMap<int, int>(indices.Length, Allocator.Temp);
            using (var all = edges.GetKeyArray(Allocator.Temp))
            {
                for (int e = 0; e < all.Length; e++)
                {
                    var edge = all[e];
                    if (!edges.ContainsKey(edge.yx))
                        next.TryAdd(edge.x, edge.y);
                }
            }
            var loop  = new NativeList<int>(16, Allocator.Temp);
            var taken = new UnsafeHashSet<int>(16, Allocator.Temp);
            using (var starts = next.GetKeyArray(Allocator.Temp))
            {
                starts.Sort();
                for (int s = 0; s < starts.Length; s++)
                {
                    if (taken.Contains(starts[s]))
                        continue;
                    loop.Clear();
                    var current = starts[s];
                    var closed = false;
                    while (!taken.Contains(current) && next.TryGetValue(current, out var following))
                    {
                        taken.Add(current);
                        loop.Add(current);
                        current = following;
                        if (current == starts[s]) { closed = true; break; }
                    }
                    if (!closed)
                        continue;
                    // no corner where it goes straight on
                    for (bool removed = true; removed && loop.Length > 3;)
                    {
                        removed = false;
                        for (int k = 0; k < loop.Length && loop.Length > 3; k++)
                        {
                            var previous = where[loop[(k + loop.Length - 1) % loop.Length]];
                            var here     = where[loop[k]];
                            var after    = where[loop[(k + 1) % loop.Length]];
                            if (!OutputWeld.OnOneLine(previous, here, after))
                                continue;
                            loop.RemoveAt(k);
                            removed = true;
                            k--;
                        }
                    }
                    if (loop.Length < 3)
                        continue;
                    loops.Add(new int2(corners.Length, loop.Length));
                    for (int k = 0; k < loop.Length; k++)
                        corners.Add(Frame(where[loop[k]]));
                }
            }
            loop.Dispose();
            taken.Dispose();
            next.Dispose();
            edges.Dispose();
            vertexId.Dispose();
            where.Dispose();
            ids.Dispose();
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

        // The border along the outline of what the surface draws (indices, positions); false when the tile has no border
        // or no band fits
        public bool BuildConforming(in HotspotSectionInput input, NativeList<int> indices, NativeList<float3> positions)
        {
            lines.Clear();
            halfPlanes.Clear();
            cells.Clear();
            if (input.count != 4 || !(input.border > 0) || !(input.pixelsPerUnit > 0) || input.Repeated)
                return false;
            var s = new Outline(4, input.f0, input.f1, input.f2);
            var t = new Outline(4, input.t0, input.t1, input.t2);
            var surfaceBand = math.min((double)input.border / input.pixelsPerUnit, 0.5 * s.MaxBand());
            var tileBand    = math.min((double)input.border, 0.5 * t.MaxBand());
            if (!(surfaceBand > 0) || !(tileBand > 0))
                return false;
            s.Inset(surfaceBand);
            t.Inset(tileBand);
            var size = (double2)input.textureSize;
            var depthScale = tileBand / surfaceBand;

            // the interior: the surface's inset bounds onto the tile's, in pixels
            var origin = s.inner[0];
            var linear = math.mul(new double2x2(t.inner[1] - t.inner[0], t.inner[3] - t.inner[0]),
                                  math.inverse(new double2x2(s.inner[1] - origin, s.inner[3] - origin)));
            var pixelOrigin = t.inner[0];
            double2 Interior(double2 p) => pixelOrigin + math.mul(linear, p - origin);

            using var corners = new NativeList<double2>(32, Allocator.Temp);
            using var loops   = new NativeList<int2>(4, Allocator.Temp);
            OutlineOf(indices, positions, input.right, input.up, corners, loops);
            if (loops.Length == 0)
                return false;

            using var along    = new NativeList<double2>(corners.Length, Allocator.Temp);
            using var inward   = new NativeList<double2>(corners.Length, Allocator.Temp);
            using var inner    = new NativeList<double2>(corners.Length, Allocator.Temp);
            using var convex   = new NativeList<bool>(corners.Length, Allocator.Temp);
            using var outer    = new NativeList<int>(corners.Length, Allocator.Temp);
            using var inset    = new NativeList<int>(corners.Length, Allocator.Temp);
            using var side     = new NativeList<int>(corners.Length, Allocator.Temp);
            var starts         = new NativeList<int2>(corners.Length, Allocator.Temp);   // (line, side) where a strip starts
            var ends           = new NativeList<int2>(corners.Length, Allocator.Temp);   // and where it ends
            for (int l = 0; l < loops.Length; l++)
            {
                var first = loops[l].x; var count = loops[l].y;
                int At(int k) => first + (k % count + count) % count;
                along.Clear(); inward.Clear(); inner.Clear(); convex.Clear(); outer.Clear(); inset.Clear(); side.Clear(); starts.Clear(); ends.Clear();
                for (int k = 0; k < count; k++)
                {
                    var d = math.normalize(corners[At(k + 1)] - corners[At(k)]);
                    var n = new double2(-d.y, d.x);
                    along.Add(d);
                    inward.Add(n);
                    outer.Add(AddLine(n, corners[At(k)]));
                    inset.Add(AddLine(n, corners[At(k)] + n * surfaceBand));
                    side.Add(SideFacing(t, math.mul(linear, -n)));
                    starts.Add(default);
                    ends.Add(default);
                }
                // each corner k, between edge k - 1 and edge k
                for (int k = 0; k < count; k++)
                {
                    var h = (k + count - 1) % count;
                    var c = corners[At(k)];
                    var turn = Cross(along[h], along[k]);
                    var nh = inward[h]; var nk = inward[k];
                    var determinant = Cross(nh, nk);
                    var point = math.abs(determinant) > 0
                              ? new double2(((math.dot(nh, c) + surfaceBand) * nk.y - nh.y * (math.dot(nk, c) + surfaceBand)) / determinant,
                                            (nh.x * (math.dot(nk, c) + surfaceBand) - (math.dot(nh, c) + surfaceBand) * nk.x) / determinant)
                              : c + nk * surfaceBand;
                    inner.Add(point);
                    convex.Add(turn > 0);
                    if (turn > 0)
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
                    var a = corners[At(k)];
                    var b = corners[At(k + 1)];
                    var d = along[k]; var n = inward[k]; var j = side[k];
                    var depthEdge = k;
                    double2 Strip(double2 p) => WithDepth(Interior(p), t, side[depthEdge], math.dot(inward[depthEdge], p - corners[At(depthEdge)]) * depthScale);

                    // where along the edge the strip runs, between its corners' sections
                    var from = convex[k] ? math.dot(d, inner[k] - a) : 0;
                    var to   = convex[(k + 1) % count] ? math.dot(d, inner[(k + 1) % count] - a) : math.length(b - a);
                    var repeats = 1;
                    var step = to - from;
                    if ((input.flags & HotspotTileFlags.RepeatBorder) != 0 && step > 0)
                    {
                        var next = (j + 1) % t.count;
                        var period = math.length(t.footPrevious[next] - t.footNext[j]) / depthScale;
                        if ((input.flags & HotspotTileFlags.PartialRepeats) != 0)
                        {
                            repeats = math.max(1, PartialRepeats(step, period, out var last));
                            if (repeats > 1 || last < 1) step = period;
                        } else
                        {
                            repeats = math.max(1, Repeats(step, period));
                            step /= repeats;
                        }
                    }
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
                            var next = (j + 1) % t.count;
                            var forward = math.dot(math.mul(linear, d), t.along[j]) >= 0;
                            var p0 = a + d * (from + step * rep);
                            AddCell(start, p0, p0 + d * step, p0 + n * surfaceBand,
                                    forward ? t.footNext[j] : t.footPrevious[next], forward ? t.footPrevious[next] : t.footNext[j],
                                    forward ? t.inner[j] : t.inner[next], size);
                        }
                        previousCut = new int2(cut.x, -cut.y);
                    }
                }

                // the corners
                for (int k = 0; k < count; k++)
                {
                    if (!convex[k])
                        continue;
                    var h = (k + count - 1) % count;
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
                        double2 StripOf(double2 p, int edge) => WithDepth(Interior(p), t, side[edge], Depth(p, edge));
                        var start = halfPlanes.Length;
                        Half(outer[h], 1); Half(outer[k], 1); Half(ends[h].x, -ends[h].y); Half(starts[k].x, -starts[k].y); Half(diagonal, towardK);
                        AddCell(start, c, c + along[k], c + inward[k], StripOf(c, kEdge), StripOf(c + along[k], kEdge), StripOf(c + inward[k], kEdge), size);
                        start = halfPlanes.Length;
                        Half(outer[h], 1); Half(outer[k], 1); Half(ends[h].x, -ends[h].y); Half(starts[k].x, -starts[k].y); Half(diagonal, -towardK);
                        AddCell(start, c, c - along[h], c + inward[h], StripOf(c, hEdge), StripOf(c - along[h], hEdge), StripOf(c + inward[h], hEdge), size);
                    }
                }
            }

            starts.Dispose();
            ends.Dispose();

            // the interior: wherever no band section is
            var interiorStart = halfPlanes.Length;
            AddCell(interiorStart, s.inner[0], s.inner[1], s.inner[3], t.inner[0], t.inner[1], t.inner[3], size);
            return true;
        }
    }
}
