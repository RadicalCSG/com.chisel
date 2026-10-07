using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;

namespace Chisel.Core
{
    // A surface whose outline is one convex loop is cut once: its outline is clipped to each section, and each section
    // is triangulated once. A line only cuts where it bounds a section, unlike Split, which cuts every triangle by every
    // line across the whole surface. Points on an edge two sections share are put in both, so nothing is left with a
    // T-junction; the outline keeps every point it had (the weld's, where other surfaces meet it).
    public partial struct HotspotSections
    {
        // A point of a section's polygon. kind 0: outline point a; 1: where outline edge a (from point a to a + 1) crosses
        // line b; 2: where lines a and b cross. support: what the edge to the next point lies on, a line, or -1 - an
        // outline edge.
        struct CutPoint
        {
            public double2 p2;
            public double3 p3;
            public int     kind, a, b;
            public int     support;

            public readonly bool OnLine(int line, NativeArray<int> same) =>
                (kind == 1 && same[b] == same[line]) || (kind == 2 && (same[a] == same[line] || same[b] == same[line]));
        }

        // The first of the lines each line coincides with (strips that meet in the middle of a surface that is all border)
        readonly NativeArray<int> SameLines(Allocator allocator)
        {
            var same = new NativeArray<int>(lines.Length, allocator);
            for (int l = 0; l < lines.Length; l++)
            {
                same[l] = l;
                for (int m = 0; m < l; m++)
                {
                    var a = lines[l]; var b = lines[m];
                    var direction = math.dot(a.normal, b.normal) >= 0 ? 1.0 : -1.0;
                    var tolerance = 1e-9 * math.max(1, math.abs(a.offset));
                    if (math.abs(Cross(a.normal, b.normal)) <= 1e-12 && math.abs(a.offset - direction * b.offset) <= tolerance)
                    {
                        same[l] = same[m];
                        break;
                    }
                }
            }
            return same;
        }

        // The outline of the triangles as one loop of vertex indices, every point on it kept, the surface on its left in
        // the frame; false when it is not one convex loop
        static bool ConvexLoop(NativeList<int> indices, NativeArray<double2> frame, NativeList<float3> positions, NativeList<int> loop)
        {
            loop.Clear();
            var ids   = new UnsafeHashMap<float3, int>(positions.Length, Allocator.Temp);
            var first = new NativeArray<int>(positions.Length, Allocator.Temp);
            for (int v = 0; v < positions.Length; v++)
            {
                var key = positions[v] + 0.0f;
                if (!ids.TryGetValue(key, out var id)) { id = v; ids.Add(key, v); }
                first[v] = id;
            }
            var area = 0.0;
            for (int t = 0; t + 2 < indices.Length; t += 3)
                area += Cross(frame[indices[t + 1]] - frame[indices[t]], frame[indices[t + 2]] - frame[indices[t]]);
            var flip = area < 0;
            var edges = new UnsafeHashMap<int2, int>(indices.Length, Allocator.Temp);
            for (int t = 0; t + 2 < indices.Length; t += 3)
            {
                for (int k = 0; k < 3; k++)
                {
                    var a = first[indices[t + k]]; var b = first[indices[t + (k + 1) % 3]];
                    if (a == b) continue;
                    var edge = flip ? new int2(b, a) : new int2(a, b);
                    edges.TryGetValue(edge, out var count);
                    edges.Remove(edge);
                    edges.Add(edge, count + 1);
                }
            }
            var next = new UnsafeHashMap<int, int>(indices.Length, Allocator.Temp);
            var ok = true;
            using (var all = edges.GetKeyArray(Allocator.Temp))
            {
                for (int e = 0; e < all.Length && ok; e++)
                {
                    if (edges.ContainsKey(all[e].yx))
                        continue;
                    ok = edges[all[e]] == 1 && next.TryAdd(all[e].x, all[e].y);
                }
            }
            if (ok && next.Count >= 3)
            {
                using var starts = next.GetKeyArray(Allocator.Temp);
                var current = starts[0];
                for (int guard = 0; guard <= next.Count; guard++)
                {
                    loop.Add(current);
                    if (!next.TryGetValue(current, out current)) { ok = false; break; }
                    if (current == starts[0]) break;
                }
                ok &= loop.Length == next.Count;
                // convex: no turn to the right (points on a straight edge allowed)
                for (int k = 0; k < loop.Length && ok; k++)
                {
                    var a = frame[loop[(k + loop.Length - 1) % loop.Length]]; var b = frame[loop[k]]; var c = frame[loop[(k + 1) % loop.Length]];
                    var scale = math.max(math.lengthsq(b - a), math.lengthsq(c - b));
                    ok = Cross(b - a, c - b) >= -1e-12 * scale;
                }
            } else
                ok = false;
            next.Dispose();
            edges.Dispose();
            first.Dispose();
            ids.Dispose();
            if (!ok) loop.Clear();
            return ok;
        }

        // Cuts a surface whose outline is one convex loop along the lines between the sections only where they bound
        // them; false (nothing changed) when it is not such a surface, or the sections do not cover it once
        public readonly bool CutConvex(float3 right, float3 up, float3 normal, NativeList<int> indices,
                                       NativeList<float3> colliderVertices, NativeList<SelectVertex> selectVertices, NativeList<RenderVertex> renderVertices)
        {
            if (cells.Length == 0 || indices.Length < 3)
                return false;
            // every section bounded, unless it is the only one
            for (int c = 0; c < cells.Length && cells.Length > 1; c++)
            {
                if (cells[c].halfPlaneCount == 0)
                    return false;
            }
            var r = (double3)right; var u = (double3)up; var n = (double3)normal;
            var frame = new NativeArray<double2>(colliderVertices.Length, Allocator.Temp);
            for (int v = 0; v < colliderVertices.Length; v++)
                frame[v] = FrameOf(colliderVertices[v], r, u);
            var loop = new NativeList<int>(16, Allocator.Temp);
            var orientation = 0.0;
            for (int t = 0; t + 2 < indices.Length; t += 3)
                orientation += Cross(frame[indices[t + 1]] - frame[indices[t]], frame[indices[t + 2]] - frame[indices[t]]);
            if (!ConvexLoop(indices, frame, colliderVertices, loop))
            {
                loop.Dispose();
                frame.Dispose();
                return false;
            }
            var planeDistance = math.dot(n, (double3)colliderVertices[indices[0]]);
            var same = SameLines(Allocator.Temp);

            // each section's polygon, clipped from the outline
            var polygons = new NativeList<CutPoint>(loop.Length * cells.Length, Allocator.Temp);
            var ranges   = new NativeList<int3>(cells.Length, Allocator.Temp);   // start, count, cell
            var current  = new NativeList<CutPoint>(32, Allocator.Temp);
            var clipped  = new NativeList<CutPoint>(32, Allocator.Temp);
            var values   = new NativeList<double>(32, Allocator.Temp);
            var outlineArea = 0.0;
            for (int k = 0; k < loop.Length; k++)
                outlineArea += Cross(frame[loop[k]], frame[loop[(k + 1) % loop.Length]]);
            outlineArea *= 0.5;
            var cutArea = 0.0;
            // a point this close to a line is on it (its size: the outline's extent)
            var extent = 0.0;
            for (int k = 0; k < loop.Length; k++)
                extent = math.max(extent, math.cmax(math.abs(frame[loop[k]])));
            var onLine = 1e-12 * math.max(1, extent);
            for (int c = 0; c < cells.Length; c++)
            {
                current.Clear();
                for (int k = 0; k < loop.Length; k++)
                    current.Add(new CutPoint { p2 = frame[loop[k]], p3 = colliderVertices[loop[k]], kind = 0, a = k, b = -1, support = -1 - k });
                var cell = cells[c];
                for (int h = cell.halfPlaneStart; h < cell.halfPlaneStart + cell.halfPlaneCount && current.Length >= 3; h++)
                {
                    var line = halfPlanes[h].line; var side = halfPlanes[h].side;
                    values.Clear();
                    bool inside = false, outside = false;
                    for (int i = 0; i < current.Length; i++)
                    {
                        var value = current[i].OnLine(line, same) ? 0 : side * lines[line].Value(current[i].p2);
                        if (math.abs(value) <= onLine) value = 0;
                        values.Add(value);
                        inside  |= value > 0;
                        outside |= value < 0;
                    }
                    if (!outside)
                        continue;
                    clipped.Clear();
                    if (inside)
                    {
                        for (int i = 0; i < current.Length; i++)
                        {
                            var from = current[i]; var to = current[(i + 1) % current.Length];
                            var vFrom = values[i]; var vTo = values[(i + 1) % current.Length];
                            if (vFrom >= 0)
                            {
                                var kept = from;
                                // leaving the half plane here: the edge to the next point is the line
                                if (vFrom == 0 && vTo < 0) kept.support = line;
                                clipped.Add(kept);
                            }
                            if ((vFrom > 0 && vTo < 0) || (vFrom < 0 && vTo > 0))
                            {
                                var crossing = CutCrossing(from.support, line, loop, frame, colliderVertices, r, u, n, planeDistance);
                                crossing.support = vFrom > 0 ? line : from.support;
                                clipped.Add(crossing);
                            }
                        }
                    }
                    (current, clipped) = (clipped, current);
                }
                if (current.Length < 3)
                    continue;
                var area = 0.0;
                for (int i = 0; i < current.Length; i++)
                    area += Cross(current[i].p2, current[(i + 1) % current.Length].p2);
                area *= 0.5;
                if (!(area > 0))
                    continue;
                cutArea += area;
                ranges.Add(new int3(polygons.Length, current.Length, c));
                polygons.AddRange(current.AsArray());
            }
            current.Dispose(); clipped.Dispose(); values.Dispose();

            // the sections cover the surface once, or it is cut the old way
            var covered = math.abs(cutArea - outlineArea) <= 1e-6 * math.max(1, math.abs(outlineArea));
            if (!covered)
            {
                ranges.Dispose(); polygons.Dispose(); same.Dispose(); loop.Dispose(); frame.Dispose();
                return false;
            }

            // the vertices, one for each place: the outline's as they are, new ones taking what they lie on
            var newCollider = new NativeList<float3>(colliderVertices.Length * 2, Allocator.Temp);
            var newSelect   = new NativeList<SelectVertex>(colliderVertices.Length * 2, Allocator.Temp);
            var newRender   = new NativeList<RenderVertex>(colliderVertices.Length * 2, Allocator.Temp);
            var lookup      = new UnsafeHashMap<float3, int>(polygons.Length, Allocator.Temp);
            var vertexOf    = new NativeArray<int>(polygons.Length, Allocator.Temp);
            var entityID    = selectVertices[indices[0]].entityID;
            for (int i = 0; i < polygons.Length; i++)
            {
                var point = polygons[i];
                var key = (point.kind == 0 ? colliderVertices[loop[point.a]] : (float3)point.p3) + 0.0f;
                if (lookup.TryGetValue(key, out var index)) { vertexOf[i] = index; continue; }
                index = newCollider.Length;
                lookup.Add(key, index);
                vertexOf[i] = index;
                if (point.kind == 0)
                {
                    var source = loop[point.a];
                    newCollider.Add(colliderVertices[source]);
                    newSelect.Add(selectVertices[source]);
                    newRender.Add(renderVertices[source]);
                    continue;
                }
                var position = (float3)point.p3;
                newCollider.Add(position);
                newSelect.Add(new SelectVertex { position = position, entityID = entityID });
                newRender.Add(Interpolated(point, position, loop, frame, indices, renderVertices, normal));
            }

            // the points on each line and outline edge, in order along it, so an edge two sections share gets them all
            var onSupport = new UnsafeParallelMultiHashMap<int, int2>(polygons.Length * 2, Allocator.Temp);   // support -> (vertex, point)
            for (int i = 0; i < polygons.Length; i++)
            {
                var point = polygons[i];
                if (point.kind == 0)
                {
                    onSupport.Add(-1 - point.a, new int2(vertexOf[i], i));
                    onSupport.Add(-1 - (point.a + loop.Length - 1) % loop.Length, new int2(vertexOf[i], i));
                } else if (point.kind == 1)
                {
                    onSupport.Add(-1 - point.a, new int2(vertexOf[i], i));
                    onSupport.Add(same[point.b], new int2(vertexOf[i], i));
                } else
                {
                    onSupport.Add(same[point.a], new int2(vertexOf[i], i));
                    onSupport.Add(same[point.b], new int2(vertexOf[i], i));
                }
            }

            var newIndices = new NativeList<int>(polygons.Length * 3, Allocator.Temp);
            var ring       = new NativeList<int>(32, Allocator.Temp);
            var ringP      = new NativeList<double2>(32, Allocator.Temp);
            var between    = new NativeList<double3>(8, Allocator.Temp);   // parameter, vertex, -
            var seen       = new UnsafeHashSet<int>(8, Allocator.Temp);
            var complete   = true;
            for (int g = 0; g < ranges.Length && complete; g++)
            {
                var range = ranges[g];
                ring.Clear(); ringP.Clear();
                for (int i = 0; i < range.y; i++)
                {
                    var from = polygons[range.x + i]; var to = polygons[range.x + (i + 1) % range.y];
                    var vFrom = vertexOf[range.x + i]; var vTo = vertexOf[range.x + (i + 1) % range.y];
                    if (ring.Length == 0 || ring[ring.Length - 1] != vFrom) { ring.Add(vFrom); ringP.Add(from.p2); }
                    // the other points on this edge, in order from its start
                    var support = from.support >= 0 ? same[from.support] : from.support;
                    var direction = to.p2 - from.p2;
                    var length = math.lengthsq(direction);
                    if (!(length > 0)) continue;
                    between.Clear(); seen.Clear();
                    if (onSupport.TryGetFirstValue(support, out var item, out var iterator))
                    {
                        do
                        {
                            if (item.x == vFrom || item.x == vTo || !seen.Add(item.x)) continue;
                            var t = math.dot(polygons[item.y].p2 - from.p2, direction) / length;
                            if (t > 1e-9 && t < 1 - 1e-9) between.Add(new double3(t, item.x, item.y));
                        } while (onSupport.TryGetNextValue(out item, ref iterator));
                    }
                    for (int a = 1; a < between.Length; a++)
                    {
                        var key = between[a]; var b = a - 1;
                        while (b >= 0 && between[b].x > key.x) { between[b + 1] = between[b]; b--; }
                        between[b + 1] = key;
                    }
                    for (int a = 0; a < between.Length; a++)
                    {
                        var v = (int)between[a].y;
                        if (ring[ring.Length - 1] != v) { ring.Add(v); ringP.Add(polygons[(int)between[a].z].p2); }
                    }
                }
                if (ring.Length > 1 && ring[0] == ring[ring.Length - 1]) { ring.RemoveAt(ring.Length - 1); ringP.RemoveAt(ringP.Length - 1); }
                complete &= EarClip(ring, ringP, orientation < 0, newIndices);
            }
            between.Dispose(); seen.Dispose(); ring.Dispose(); ringP.Dispose();
            onSupport.Dispose(); vertexOf.Dispose(); lookup.Dispose();
            ranges.Dispose(); polygons.Dispose(); same.Dispose(); loop.Dispose(); frame.Dispose();
            if (!complete)
            {
                newIndices.Dispose(); newCollider.Dispose(); newSelect.Dispose(); newRender.Dispose();
                return false;
            }

            indices.Clear();          indices.AddRange(newIndices.AsArray());
            colliderVertices.Clear(); colliderVertices.AddRange(newCollider.AsArray());
            selectVertices.Clear();   selectVertices.AddRange(newSelect.AsArray());
            renderVertices.Clear();   renderVertices.AddRange(newRender.AsArray());
            newIndices.Dispose(); newCollider.Dispose(); newSelect.Dispose(); newRender.Dispose();
            return true;
        }

        // Where what a polygon edge lies on (an outline edge, or a line) crosses a line: from the outline edge's ends in a
        // fixed order, or from the two lines, so neighbouring sections get the very same point
        readonly CutPoint CutCrossing(int support, int line, NativeList<int> loop, NativeArray<double2> frame, NativeList<float3> positions,
                                      double3 r, double3 u, double3 n, double planeDistance)
        {
            if (support < 0)
            {
                var edge = -1 - support;
                int from = loop[edge], to = loop[(edge + 1) % loop.Length];
                var p3From = (double3)positions[from]; var p3To = (double3)positions[to];
                var flipped = Before(p3To, p3From);
                if (flipped) { (from, to) = (to, from); (p3From, p3To) = (p3To, p3From); }
                var vFrom = lines[line].Value(frame[from]);
                var vTo   = lines[line].Value(frame[to]);
                var t = vFrom != vTo ? vFrom / (vFrom - vTo) : 0.5;
                return new CutPoint
                {
                    p2 = frame[from] + (frame[to] - frame[from]) * t,
                    p3 = p3From + (p3To - p3From) * t,
                    kind = 1, a = edge, b = line
                };
            }
            int first = math.min(support, line), second = math.max(support, line);
            var l0 = lines[first]; var l1 = lines[second];
            var determinant = Cross(l0.normal, l1.normal);
            var p2 = new double2((-l0.offset * l1.normal.y + l0.normal.y * l1.offset) / determinant,
                                 (-l0.normal.x * l1.offset + l0.offset * l1.normal.x) / determinant);
            return new CutPoint { p2 = p2, p3 = r * p2.x + u * p2.y + n * planeDistance, kind = 2, a = first, b = second };
        }

        // A new vertex's normal, tangent and lightmap uv: along its outline edge, or from the triangle it is in
        static RenderVertex Interpolated(in CutPoint point, float3 position, NativeList<int> loop, NativeArray<double2> frame,
                                         NativeList<int> indices, NativeList<RenderVertex> render, float3 normal)
        {
            if (point.kind == 1)
            {
                var from = loop[point.a]; var to = loop[(point.a + 1) % loop.Length];
                var direction = frame[to] - frame[from];
                var t = (float)math.saturate(math.dot(point.p2 - frame[from], direction) / math.max(math.lengthsq(direction), 1e-300));
                var a = render[from]; var b = render[to];
                return new RenderVertex
                {
                    position = position,
                    normal   = math.normalizesafe(math.lerp(a.normal, b.normal, t), normal),
                    tangent  = math.lerp(a.tangent, b.tangent, t),
                    uv1      = math.lerp(a.uv1, b.uv1, t)
                };
            }
            // the triangle that holds it best
            var best = 0; var bestWeights = new double3(1, 0, 0); var bestMargin = double.NegativeInfinity;
            for (int t = 0; t + 2 < indices.Length; t += 3)
            {
                var p0 = frame[indices[t]]; var e1 = frame[indices[t + 1]] - p0; var e2 = frame[indices[t + 2]] - p0; var d = point.p2 - p0;
                var area = Cross(e1, e2);
                if (area == 0) continue;
                var w1 = Cross(d, e2) / area; var w2 = Cross(e1, d) / area;
                var w = new double3(1 - w1 - w2, w1, w2);
                var margin = math.cmin(w);
                if (margin > bestMargin) { bestMargin = margin; best = t; bestWeights = w; }
            }
            var r0 = render[indices[best]]; var r1 = render[indices[best + 1]]; var r2 = render[indices[best + 2]];
            var weights = (float3)bestWeights;
            return new RenderVertex
            {
                position = position,
                normal   = math.normalizesafe(weights.x * r0.normal + weights.y * r1.normal + weights.z * r2.normal, normal),
                tangent  = weights.x * r0.tangent + weights.y * r1.tangent + weights.z * r2.tangent,
                uv1      = weights.x * r0.uv1 + weights.y * r1.uv1 + weights.z * r2.uv1
            };
        }

        // A convex polygon (points on its straight edges included) into triangles that use every point: an ear is a
        // corner with area whose diagonal passes no other point. Wound as the surface's triangles were (reversed: they
        // were clockwise in the frame). False when the triangles do not cover it.
        static bool EarClip(NativeList<int> ring, NativeList<double2> points, bool reversed, NativeList<int> output)
        {
            var polygonArea = 0.0;
            for (int i = 0; i < points.Length; i++)
                polygonArea += Cross(points[i], points[(i + 1) % points.Length]);
            polygonArea *= 0.5;
            var triangleArea = 0.0;
            var left = new NativeList<int>(ring.Length, Allocator.Temp);
            for (int i = 0; i < ring.Length; i++)
                left.Add(i);
            var guard = ring.Length * ring.Length + 8;
            while (left.Length > 3 && guard-- > 0)
            {
                var clippedOne = false;
                for (int i = 0; i < left.Length && !clippedOne; i++)
                {
                    var prev = left[(i + left.Length - 1) % left.Length]; var here = left[i]; var next = left[(i + 1) % left.Length];
                    var a = points[prev]; var b = points[here]; var c = points[next];
                    var area = Cross(b - a, c - a);
                    var scale = math.max(math.lengthsq(b - a), math.lengthsq(c - a));
                    if (!(area > 1e-12 * scale))
                        continue;
                    // no other point inside the ear or on its diagonal
                    var clear = true;
                    for (int j = 0; j < left.Length && clear; j++)
                    {
                        var o = left[j];
                        if (o == prev || o == here || o == next) continue;
                        var p = points[o];
                        clear = !(Cross(b - a, p - a) >= 0 && Cross(c - b, p - b) >= 0 && Cross(a - c, p - c) >= 0);
                    }
                    if (!clear)
                        continue;
                    Emit(ring[prev], ring[here], ring[next]);
                    triangleArea += area * 0.5;
                    left.RemoveAt(i);
                    clippedOne = true;
                }
                if (!clippedOne)
                    break;
            }
            if (left.Length == 3)
            {
                var a = points[left[0]]; var b = points[left[1]]; var c = points[left[2]];
                if (Cross(b - a, c - a) > 0)
                {
                    Emit(ring[left[0]], ring[left[1]], ring[left[2]]);
                    triangleArea += Cross(b - a, c - a) * 0.5;
                }
            }
            left.Dispose();
            return math.abs(triangleArea - polygonArea) <= 1e-9 * math.max(1, math.abs(polygonArea));

            void Emit(int i0, int i1, int i2)
            {
                output.Add(i0);
                if (reversed) { output.Add(i2); output.Add(i1); }
                else          { output.Add(i1); output.Add(i2); }
            }
        }
    }
}
