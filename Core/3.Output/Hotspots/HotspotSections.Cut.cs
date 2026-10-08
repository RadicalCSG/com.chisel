using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;

namespace Chisel.Core
{
    // A surface is cut once where its sections meet: what it draws is clipped to each section and each piece is
    // triangulated once, so a line only cuts where it bounds a section. It starts from the outline when that is one
    // convex loop, otherwise from each triangle. A section without bounds (the interior of a surface that is not one
    // convex loop) takes what is left. Points on an edge two pieces share are put in both, so nothing is left with a
    // T-junction; the outline keeps every point it had (the weld's, where other surfaces meet it).
    public partial struct HotspotSections
    {
        // A point of a piece. kind 0: vertex a; 1: where edge a (two vertices) crosses line b; 2: where lines a and b
        // cross. support: what the edge to the next point lies on, a line, or -1 - an edge.
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

        // The outline of the triangles (vertices by representative) as one loop, every point on it kept, the surface on
        // its left in the frame; false when it is not one convex loop
        static bool ConvexLoop(NativeList<int> indices, NativeArray<double2> frame, NativeArray<int> rep, NativeList<int> loop)
        {
            loop.Clear();
            var area = 0.0;
            for (int t = 0; t + 2 < indices.Length; t += 3)
                area += Cross(frame[indices[t + 1]] - frame[indices[t]], frame[indices[t + 2]] - frame[indices[t]]);
            var flip = area < 0;
            var edges = new UnsafeHashMap<int2, int>(indices.Length, Allocator.Temp);
            for (int t = 0; t + 2 < indices.Length; t += 3)
            {
                for (int k = 0; k < 3; k++)
                {
                    var a = rep[indices[t + k]]; var b = rep[indices[t + (k + 1) % 3]];
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
            if (!ok) loop.Clear();
            return ok;
        }

        // Cuts the surface where its sections meet and nowhere else; false (nothing changed) when its pieces would not
        // cover it once (then Split cuts it)
        public readonly bool CutOnce(float3 right, float3 up, float3 normal, NativeList<int> indices,
                                     NativeList<float3> colliderVertices, NativeList<SelectVertex> selectVertices, NativeList<RenderVertex> renderVertices)
        {
            if (cells.Length == 0 || indices.Length < 3)
                return false;
            var unbounded = -1;
            for (int c = 0; c < cells.Length; c++)
            {
                if (cells[c].halfPlaneCount != 0) continue;
                if (unbounded >= 0) return false;
                unbounded = c;
            }
            var r = (double3)right; var u = (double3)up; var n = (double3)normal;
            var frame = new NativeArray<double2>(colliderVertices.Length, Allocator.Temp);
            var rep   = new NativeArray<int>(colliderVertices.Length, Allocator.Temp);
            using (var ids = new UnsafeHashMap<float3, int>(colliderVertices.Length, Allocator.Temp))
            {
                for (int v = 0; v < colliderVertices.Length; v++)
                {
                    frame[v] = FrameOf(colliderVertices[v], r, u);
                    var key = colliderVertices[v] + 0.0f;
                    if (!ids.TryGetValue(key, out var id)) { id = v; ids.Add(key, v); }
                    rep[v] = id;
                }
            }
            var orientation = 0.0;
            for (int t = 0; t + 2 < indices.Length; t += 3)
                orientation += Cross(frame[indices[t + 1]] - frame[indices[t]], frame[indices[t + 2]] - frame[indices[t]]);
            var planeDistance = math.dot(n, (double3)colliderVertices[indices[0]]);
            var same = SameLines(Allocator.Temp);

            // the polygons to start from, counter-clockwise in the frame: the outline when it is one convex loop, otherwise
            // each triangle (the triangle a start is, for what a point inside it takes from its corners; -1: none)
            var starts      = new NativeList<int>(indices.Length, Allocator.Temp);
            var startRanges = new NativeList<int3>(indices.Length / 3, Allocator.Temp);   // start, count, triangle
            using (var loop = new NativeList<int>(16, Allocator.Temp))
            {
                if (ConvexLoop(indices, frame, rep, loop))
                {
                    startRanges.Add(new int3(0, loop.Length, -1));
                    starts.AddRange(loop.AsArray());
                } else
                {
                    for (int t = 0; t + 2 < indices.Length; t += 3)
                    {
                        int a = rep[indices[t]], b = rep[indices[t + 1]], c = rep[indices[t + 2]];
                        var area = Cross(frame[b] - frame[a], frame[c] - frame[a]);
                        if (area == 0) continue;
                        if (area < 0) (b, c) = (c, b);
                        startRanges.Add(new int3(starts.Length, 3, t));
                        starts.Add(a); starts.Add(b); starts.Add(c);
                    }
                }
            }

            // edges between vertices, one id for both ways round
            var edgeIds = new UnsafeHashMap<int2, int>(starts.Length, Allocator.Temp);
            var edges   = new NativeList<int2>(starts.Length, Allocator.Temp);
            int EdgeSupport(int a, int b)
            {
                var key = new int2(math.min(a, b), math.max(a, b));
                if (!edgeIds.TryGetValue(key, out var id)) { id = edges.Length; edges.Add(key); edgeIds.Add(key, id); }
                return -1 - id;
            }

            // a point this close to a line is on it (its size: the surface's extent)
            var extent = 0.0;
            for (int i = 0; i < starts.Length; i++)
                extent = math.max(extent, math.cmax(math.abs(frame[starts[i]])));
            var onLine = 1e-12 * math.max(1, extent);

            // each bounded section's piece of each start
            var points  = new NativeList<CutPoint>(starts.Length * cells.Length, Allocator.Temp);
            var pieces  = new NativeList<int4>(cells.Length, Allocator.Temp);   // start, count, cell, start polygon
            var current = new NativeList<CutPoint>(32, Allocator.Temp);
            var clipped = new NativeList<CutPoint>(32, Allocator.Temp);
            var values  = new NativeList<double>(32, Allocator.Temp);
            var startArea = 0.0;
            for (int s = 0; s < startRanges.Length; s++)
            {
                var range = startRanges[s];
                for (int k = 0; k < range.y; k++)
                    startArea += 0.5 * Cross(frame[starts[range.x + k]], frame[starts[range.x + (k + 1) % range.y]]);
                for (int c = 0; c < cells.Length; c++)
                {
                    var cell = cells[c];
                    if (cell.halfPlaneCount == 0)
                        continue;
                    current.Clear();
                    for (int k = 0; k < range.y; k++)
                    {
                        var v = starts[range.x + k];
                        current.Add(new CutPoint { p2 = frame[v], p3 = colliderVertices[v], kind = 0, a = v, b = -1,
                                                   support = EdgeSupport(v, starts[range.x + (k + 1) % range.y]) });
                    }
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
                                var from = current[i];
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
                                    var crossing = CutCrossing(from.support, line, edges, frame, colliderVertices, r, u, n, planeDistance);
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
                    if (!(area > 0))
                        continue;
                    pieces.Add(new int4(points.Length, current.Length, c, s));
                    points.AddRange(current.AsArray());
                }
            }
            current.Dispose(); clipped.Dispose(); values.Dispose();

            // the vertices, one for each place: the surface's as they are, new ones taking what they lie on; and every
            // point under what it lies on (its edges in and out), so an edge gets every point on it
            var newCollider = new NativeList<float3>(colliderVertices.Length * 2, Allocator.Temp);
            var newSelect   = new NativeList<SelectVertex>(colliderVertices.Length * 2, Allocator.Temp);
            var newRender   = new NativeList<RenderVertex>(colliderVertices.Length * 2, Allocator.Temp);
            var where       = new NativeList<double2>(colliderVertices.Length * 2, Allocator.Temp);
            var lookup      = new UnsafeHashMap<float3, int>(points.Length + starts.Length, Allocator.Temp);
            var onSupport   = new UnsafeParallelMultiHashMap<int, int>(points.Length * 2 + starts.Length * 2, Allocator.Temp);
            var entityID    = selectVertices[indices[0]].entityID;
            int VertexOf(in CutPoint point, int startTriangle)
            {
                var key = (point.kind == 0 ? colliderVertices[point.a] : (float3)point.p3) + 0.0f;
                if (lookup.TryGetValue(key, out var index))
                    return index;
                index = newCollider.Length;
                lookup.Add(key, index);
                where.Add(point.kind == 0 ? frame[point.a] : point.p2);
                if (point.kind == 0)
                {
                    newCollider.Add(colliderVertices[point.a]);
                    newSelect.Add(selectVertices[point.a]);
                    newRender.Add(renderVertices[point.a]);
                    return index;
                }
                var position = (float3)point.p3;
                newCollider.Add(position);
                newSelect.Add(new SelectVertex { position = position, entityID = entityID });
                newRender.Add(Interpolated(point, position, edges, frame, indices, startTriangle, renderVertices, normal));
                return index;
            }
            int Key(int support) => support >= 0 ? same[support] : support;
            void Register(int support, int vertex)
            {
                var key = Key(support);
                if (onSupport.TryGetFirstValue(key, out var existing, out var iterator))
                {
                    do { if (existing == vertex) return; } while (onSupport.TryGetNextValue(out existing, ref iterator));
                }
                onSupport.Add(key, vertex);
            }
            var pointVertex = new NativeArray<int>(points.Length, Allocator.Temp);
            for (int g = 0; g < pieces.Length; g++)
            {
                var piece = pieces[g];
                var triangle = startRanges[piece.w].z;
                for (int i = 0; i < piece.y; i++)
                    pointVertex[piece.x + i] = VertexOf(points[piece.x + i], triangle);
                for (int i = 0; i < piece.y; i++)
                {
                    var point = points[piece.x + i];
                    var previous = points[piece.x + (i + piece.y - 1) % piece.y];
                    Register(point.support, pointVertex[piece.x + i]);
                    Register(previous.support, pointVertex[piece.x + i]);
                }
            }
            var startVertex = new NativeArray<int>(starts.Length, Allocator.Temp);
            for (int s = 0; s < startRanges.Length; s++)
            {
                var range = startRanges[s];
                for (int k = 0; k < range.y; k++)
                {
                    var v = starts[range.x + k];
                    startVertex[range.x + k] = VertexOf(new CutPoint { kind = 0, a = v, p2 = frame[v] }, range.z);
                }
                for (int k = 0; k < range.y; k++)
                {
                    var here = starts[range.x + k];
                    var next = starts[range.x + (k + 1) % range.y];
                    var previous = starts[range.x + (k + range.y - 1) % range.y];
                    Register(EdgeSupport(here, next), startVertex[range.x + k]);
                    Register(EdgeSupport(previous, here), startVertex[range.x + k]);
                }
            }

            var newIndices = new NativeList<int>(points.Length * 3, Allocator.Temp);
            var ring       = new NativeList<int>(32, Allocator.Temp);
            var ringP      = new NativeList<double2>(32, Allocator.Temp);
            var between    = new NativeList<double2>(8, Allocator.Temp);   // parameter, vertex
            var complete   = true;
            // the vertices from a to b along what they lie on, a included, b not
            void Along(int a, int b, int support)
            {
                if (ring.Length == 0 || ring[ring.Length - 1] != a) { ring.Add(a); ringP.Add(where[a]); }
                var direction = where[b] - where[a];
                var length = math.lengthsq(direction);
                if (!(length > 0)) return;
                between.Clear();
                if (onSupport.TryGetFirstValue(Key(support), out var vertex, out var iterator))
                {
                    do
                    {
                        if (vertex == a || vertex == b) continue;
                        var t = math.dot(where[vertex] - where[a], direction) / length;
                        if (t > 1e-9 && t < 1 - 1e-9) between.Add(new double2(t, vertex));
                    } while (onSupport.TryGetNextValue(out vertex, ref iterator));
                }
                for (int i = 1; i < between.Length; i++)
                {
                    var item = between[i]; var j = i - 1;
                    while (j >= 0 && between[j].x > item.x) { between[j + 1] = between[j]; j--; }
                    between[j + 1] = item;
                }
                for (int i = 0; i < between.Length; i++)
                {
                    var v = (int)between[i].y;
                    if (ring[ring.Length - 1] != v) { ring.Add(v); ringP.Add(where[v]); }
                }
            }
            void CloseRing()
            {
                while (ring.Length > 1 && ring[0] == ring[ring.Length - 1]) { ring.RemoveAt(ring.Length - 1); ringP.RemoveAt(ringP.Length - 1); }
            }

            // the conforming interior's piece: in the triangles that distort least, with as few points inside it as
            // bring its distortion down (InteriorTriangles)
            var insidePoints   = new NativeList<double2>(8, Allocator.Temp);
            var self           = this;
            var insideVertex   = new NativeList<int>(8, Allocator.Temp);
            var insideTriangle = new NativeList<int3>(16, Allocator.Temp);
            bool Interior()
            {
                if (!self.InteriorTriangles(ringP, insidePoints, insideTriangle))
                    return EarClip(ring, ringP, orientation < 0, newIndices);
                insideVertex.Clear();
                for (int i = 0; i < insidePoints.Length; i++)
                {
                    var p = insidePoints[i];
                    insideVertex.Add(VertexOf(new CutPoint { p2 = p, p3 = r * p.x + u * p.y + n * planeDistance, kind = 3, a = -1, b = -1 }, -1));
                }
                int Vertex(int k) => k < ring.Length ? ring[k] : insideVertex[k - ring.Length];
                for (int i = 0; i < insideTriangle.Length; i++)
                {
                    var triangle = insideTriangle[i];
                    newIndices.Add(Vertex(triangle.x));
                    if (orientation < 0) { newIndices.Add(Vertex(triangle.z)); newIndices.Add(Vertex(triangle.y)); }
                    else                 { newIndices.Add(Vertex(triangle.y)); newIndices.Add(Vertex(triangle.z)); }
                }
                return true;
            }

            for (int g = 0; g < pieces.Length && complete; g++)
            {
                var piece = pieces[g];
                ring.Clear(); ringP.Clear();
                for (int i = 0; i < piece.y; i++)
                    Along(pointVertex[piece.x + i], pointVertex[piece.x + (i + 1) % piece.y], points[piece.x + i].support);
                CloseRing();
                complete &= piece.z == interiorCell && Conforms ? Interior() : EarClip(ring, ringP, orientation < 0, newIndices);
            }

            // what no bounded section took: each start's outline less its pieces, edges both ways round cancelling
            if (unbounded >= 0 && complete)
            {
                var directed = new UnsafeHashSet<int2>(64, Allocator.Temp);
                var next     = new UnsafeHashMap<int, int>(64, Allocator.Temp);
                var traced   = new NativeList<int>(32, Allocator.Temp);
                void Edge(int a, int b)
                {
                    if (a == b) return;
                    if (directed.Remove(new int2(b, a))) return;
                    complete &= directed.Add(new int2(a, b));
                }
                for (int s = 0; s < startRanges.Length && complete; s++)
                {
                    var range = startRanges[s];
                    directed.Clear();
                    ring.Clear(); ringP.Clear();
                    for (int k = 0; k < range.y; k++)
                        Along(startVertex[range.x + k], startVertex[range.x + (k + 1) % range.y],
                              EdgeSupport(starts[range.x + k], starts[range.x + (k + 1) % range.y]));
                    CloseRing();
                    for (int i = 0; i < ring.Length; i++)
                        Edge(ring[i], ring[(i + 1) % ring.Length]);
                    for (int g = 0; g < pieces.Length; g++)
                    {
                        var piece = pieces[g];
                        if (piece.w != s) continue;
                        ring.Clear(); ringP.Clear();
                        for (int i = 0; i < piece.y; i++)
                            Along(pointVertex[piece.x + i], pointVertex[piece.x + (i + 1) % piece.y], points[piece.x + i].support);
                        CloseRing();
                        for (int i = 0; i < ring.Length; i++)
                            Edge(ring[(i + 1) % ring.Length], ring[i]);
                    }
                    if (!complete || directed.Count == 0)
                        continue;
                    next.Clear();
                    using (var left = directed.ToNativeArray(Allocator.Temp))
                    {
                        for (int e = 0; e < left.Length && complete; e++)
                            complete &= next.TryAdd(left[e].x, left[e].y);
                    }
                    while (complete && next.Count > 0)
                    {
                        using var keys = next.GetKeyArray(Allocator.Temp);
                        var first = keys[0];
                        traced.Clear();
                        var at = first;
                        for (int guard = 0; guard <= keys.Length; guard++)
                        {
                            traced.Add(at);
                            if (!next.TryGetValue(at, out var to)) { complete = false; break; }
                            next.Remove(at);
                            at = to;
                            if (at == first) break;
                        }
                        if (!complete || at != first) { complete = false; break; }
                        ring.Clear(); ringP.Clear();
                        for (int i = 0; i < traced.Length; i++) { ring.Add(traced[i]); ringP.Add(where[traced[i]]); }
                        var area = 0.0;
                        for (int i = 0; i < ringP.Length; i++)
                            area += Cross(ringP[i], ringP[(i + 1) % ringP.Length]);
                        // a hole in what is left: cut triangle by triangle instead
                        if (!(area > 0)) { complete = false; break; }
                        complete &= unbounded == interiorCell && Conforms ? Interior() : EarClip(ring, ringP, orientation < 0, newIndices);
                    }
                }
                traced.Dispose(); next.Dispose(); directed.Dispose();
            }

            // the pieces cover the surface once
            if (complete)
            {
                var cutArea = 0.0;
                for (int t = 0; t + 2 < newIndices.Length; t += 3)
                    cutArea += 0.5 * math.abs(Cross(where[newIndices[t + 1]] - where[newIndices[t]], where[newIndices[t + 2]] - where[newIndices[t]]));
                complete = math.abs(cutArea - startArea) <= 1e-6 * math.max(1, math.abs(startArea));
            }

            insidePoints.Dispose(); insideVertex.Dispose(); insideTriangle.Dispose();
            between.Dispose(); ring.Dispose(); ringP.Dispose();
            startVertex.Dispose(); pointVertex.Dispose(); onSupport.Dispose(); lookup.Dispose(); where.Dispose();
            pieces.Dispose(); points.Dispose(); edges.Dispose(); edgeIds.Dispose();
            startRanges.Dispose(); starts.Dispose(); same.Dispose(); rep.Dispose(); frame.Dispose();
            if (complete)
            {
                indices.Clear();          indices.AddRange(newIndices.AsArray());
                colliderVertices.Clear(); colliderVertices.AddRange(newCollider.AsArray());
                selectVertices.Clear();   selectVertices.AddRange(newSelect.AsArray());
                renderVertices.Clear();   renderVertices.AddRange(newRender.AsArray());
            }
            newIndices.Dispose(); newCollider.Dispose(); newSelect.Dispose(); newRender.Dispose();
            return complete;
        }

        // Where what a piece's edge lies on (an edge between two vertices, or a line) crosses a line: from the edge's
        // ends in a fixed order, or from the two lines, so neighbouring pieces get the very same point
        readonly CutPoint CutCrossing(int support, int line, NativeList<int2> edges, NativeArray<double2> frame, NativeList<float3> positions,
                                      double3 r, double3 u, double3 n, double planeDistance)
        {
            if (support < 0)
            {
                var edge = -1 - support;
                int from = edges[edge].x, to = edges[edge].y;
                var p3From = (double3)positions[from]; var p3To = (double3)positions[to];
                if (Before(p3To, p3From)) { (from, to) = (to, from); (p3From, p3To) = (p3To, p3From); }
                var vFrom = lines[line].Value(frame[from]);
                var vTo   = lines[line].Value(frame[to]);
                var t = vFrom != vTo ? vFrom / (vFrom - vTo) : 0.5;
                return new CutPoint { p2 = frame[from] + (frame[to] - frame[from]) * t, p3 = p3From + (p3To - p3From) * t, kind = 1, a = edge, b = line };
            }
            int first = math.min(support, line), second = math.max(support, line);
            var l0 = lines[first]; var l1 = lines[second];
            var determinant = Cross(l0.normal, l1.normal);
            var p2 = new double2((-l0.offset * l1.normal.y + l0.normal.y * l1.offset) / determinant,
                                 (-l0.normal.x * l1.offset + l0.offset * l1.normal.x) / determinant);
            return new CutPoint { p2 = p2, p3 = r * p2.x + u * p2.y + n * planeDistance, kind = 2, a = first, b = second };
        }

        // A new vertex's normal, tangent and lightmap uv: along its edge, or from the triangle it is in
        static RenderVertex Interpolated(in CutPoint point, float3 position, NativeList<int2> edges, NativeArray<double2> frame,
                                         NativeList<int> indices, int triangle, NativeList<RenderVertex> render, float3 normal)
        {
            if (point.kind == 1)
            {
                var from = edges[point.a].x; var to = edges[point.a].y;
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
            // the triangle it came from, or the one that holds it best
            var best = triangle >= 0 ? triangle : 0; var bestWeights = new double3(1, 0, 0); var bestMargin = double.NegativeInfinity;
            for (int t = triangle >= 0 ? triangle : 0; t + 2 < indices.Length; t += 3)
            {
                var p0 = frame[indices[t]]; var e1 = frame[indices[t + 1]] - p0; var e2 = frame[indices[t + 2]] - p0; var d = point.p2 - p0;
                var area = Cross(e1, e2);
                if (area != 0)
                {
                    var w1 = Cross(d, e2) / area; var w2 = Cross(e1, d) / area;
                    var w = new double3(1 - w1 - w2, w1, w2);
                    var margin = math.cmin(w);
                    if (margin > bestMargin) { bestMargin = margin; best = t; bestWeights = w; }
                }
                if (triangle >= 0) break;
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

        // The conforming interior's map kinks where its triangles meet: there its slope (J) jumps. A piece whose worst jump
        // (|J1 - J2| / |J1|) is above kInteriorKink gets one point in its middle; when that still leaves one above
        // kInteriorRingKink, also a ring of points halfway out, if that takes away a fifth of it (// subdivide only to reduce the distortion, to a minimum)
        const double kInteriorKink     = 0.25;
        const double kInteriorRingKink = 0.6;
        const double kInteriorRing     = 0.5;
        const double kWorthTheRing     = 0.8;

        // A convex piece of the conforming interior (points, counter-clockwise) into the triangles whose map round the
        // tile's inner rectangle distorts least, none flat along one side of the tile (LeastDistorted), with as few
        // points inside it (inside) as smooth its kinks. A triangle's corners below points.Length are its points, the
        // others the points inside. False when it is not convex or every triangulation has a flat triangle (then it is
        // ear clipped).
        readonly bool InteriorTriangles(NativeList<double2> points, NativeList<double2> inside, NativeList<int3> triangles)
        {
            inside.Clear();
            triangles.Clear();
            var count = points.Length;
            var convex = count >= 3;
            for (int i = 0; i < count && convex; i++)
            {
                var a = points[(i + count - 1) % count]; var b = points[i]; var c = points[(i + 1) % count];
                convex = Cross(b - a, c - b) >= -1e-12 * math.max(math.lengthsq(b - a), math.lengthsq(c - b));
            }
            if (!convex)
                return false;
            var images = new NativeArray<double2>(count + count + 1, Allocator.Temp);
            for (int i = 0; i < count; i++)
                images[i] = OutlinePixels(points[i]);
            var least = LeastDistorted(points.AsArray(), images.GetSubArray(0, count), triangles);
            if (double.IsPositiveInfinity(least))
            {
                images.Dispose();
                return false;
            }
            var all = new NativeList<double2>(count * 2 + 1, Allocator.Temp);
            all.AddRange(points.AsArray());
            var kink = Kink(all, triangles, images);
            if (kink > kInteriorKink)
            {
                var middle = double2.zero;
                for (int i = 0; i < count; i++)
                    middle += points[i];
                middle /= count;
                var fan = new NativeList<int3>(count, Allocator.Temp);
                for (int i = 0; i < count; i++)
                    fan.Add(new int3(i, (i + 1) % count, count));
                all.Add(middle);
                if (SolveInside(all, count, fan, images))
                {
                    var fanKink = Kink(all, fan, images);
                    if (fanKink < kink)
                    {
                        kink = fanKink;
                        inside.Clear(); inside.Add(middle);
                        triangles.Clear(); triangles.AddRange(fan.AsArray());
                    }
                }
                fan.Dispose();
                if (inside.Length > 0 && kink > kInteriorRingKink)
                {
                    all.Clear();
                    all.AddRange(points.AsArray());
                    for (int i = 0; i < count; i++)
                        all.Add(middle + (points[i] - middle) * kInteriorRing);
                    all.Add(middle);
                    var ring = new NativeList<int3>(count * 3, Allocator.Temp);
                    for (int i = 0; i < count; i++)
                    {
                        var j = (i + 1) % count;
                        ring.Add(new int3(i, j, count + j));
                        ring.Add(new int3(i, count + j, count + i));
                        ring.Add(new int3(count + i, count + j, count + count));
                    }
                    if (SolveInside(all, count, ring, images) && Kink(all, ring, images) < kink * kWorthTheRing)
                    {
                        inside.Clear();
                        for (int i = count; i < all.Length; i++)
                            inside.Add(all[i]);
                        triangles.Clear(); triangles.AddRange(ring.AsArray());
                    }
                    ring.Dispose();
                }
            }
            all.Dispose();
            images.Dispose();
            return true;
        }

        // The images of the points after the first count (whose images are given): each the mean value weighted average
        // of its neighbours, as SolveInterior gives them; false when a triangle has a point twice
        static bool SolveInside(NativeList<double2> points, int count, NativeList<int3> triangles, NativeArray<double2> images)
        {
            for (int i = count; i < points.Length; i++)
                images[i] = double2.zero;
            for (int sweep = 0; sweep < 500; sweep++)
            {
                var moved = 0.0;
                for (int v = count; v < points.Length; v++)
                {
                    var sum = double2.zero; var weights = 0.0;
                    for (int t = 0; t < triangles.Length; t++)
                    {
                        var triangle = triangles[t];
                        int a, b;
                        if (triangle.x == v)      { a = triangle.y; b = triangle.z; }
                        else if (triangle.y == v) { a = triangle.z; b = triangle.x; }
                        else if (triangle.z == v) { a = triangle.x; b = triangle.y; }
                        else continue;
                        var da = points[a] - points[v]; var db = points[b] - points[v];
                        var la = math.length(da); var lb = math.length(db);
                        if (!(la > 0) || !(lb > 0))
                            return false;
                        var half = math.tan(0.5 * math.acos(math.clamp(math.dot(da, db) / (la * lb), -1, 1)));
                        sum += images[a] * (half / la) + images[b] * (half / lb);
                        weights += half / la + half / lb;
                    }
                    if (!(weights > 0))
                        return false;
                    var next = sum / weights;
                    moved = math.max(moved, math.lengthsq(next - images[v]));
                    images[v] = next;
                }
                if (moved < 1e-14)
                    break;
            }
            return true;
        }

        // The worst kink of a map: of every two triangles sharing an edge, how far their slopes differ (|J1 - J2| / |J1|);
        // infinity when a triangle is flat or turned over in either
        static double Kink(NativeList<double2> points, NativeList<int3> triangles, NativeArray<double2> images)
        {
            var slopes = new NativeArray<double2x2>(triangles.Length, Allocator.Temp);
            var worst = 0.0;
            for (int t = 0; t < triangles.Length && worst < double.PositiveInfinity; t++)
            {
                var triangle = triangles[t];
                var e1 = points[triangle.y] - points[triangle.x]; var e2 = points[triangle.z] - points[triangle.x];
                var f1 = images[triangle.y] - images[triangle.x]; var f2 = images[triangle.z] - images[triangle.x];
                if (!(Cross(e1, e2) > 0) || !(Cross(f1, f2) * Cross(e1, e2) > 0))
                    worst = double.PositiveInfinity;
                else
                    slopes[t] = math.mul(new double2x2(f1, f2), math.inverse(new double2x2(e1, e2)));
            }
            for (int t = 0; t < triangles.Length && worst < double.PositiveInfinity; t++)
            {
                for (int o = t + 1; o < triangles.Length; o++)
                {
                    var a = triangles[t]; var b = triangles[o];
                    var shared = (a.x == b.x || a.x == b.y || a.x == b.z ? 1 : 0) + (a.y == b.x || a.y == b.y || a.y == b.z ? 1 : 0)
                               + (a.z == b.x || a.z == b.y || a.z == b.z ? 1 : 0);
                    if (shared != 2)
                        continue;
                    var difference = slopes[t] - slopes[o];
                    var size = math.min(math.lengthsq(slopes[t].c0) + math.lengthsq(slopes[t].c1), math.lengthsq(slopes[o].c0) + math.lengthsq(slopes[o].c1));
                    worst = math.max(worst, math.sqrt((math.lengthsq(difference.c0) + math.lengthsq(difference.c1)) / size));
                }
            }
            slopes.Dispose();
            return worst;
        }

        // The triangulation of a convex polygon (counter-clockwise, points on its straight edges included) whose map onto
        // images distorts least: the least sum over its triangles of their area times |J|^2 / det J, the map's conformal
        // distortion (2 where it only scales and turns). A triangle without area, or turned over, in either is never taken.
        // Infinity when every triangulation has one; triangles (when created) gets the corners of each.
        static double LeastDistorted(NativeArray<double2> points, NativeArray<double2> images, NativeList<int3> triangles)
        {
            var count = points.Length;
            if (count < 3)
                return double.PositiveInfinity;
            // mirrored images keep their distortion
            var imageArea = 0.0;
            for (int i = 0; i < count; i++)
                imageArea += Cross(images[i], images[(i + 1) % count]);
            var flip = imageArea < 0 ? new double2(1, -1) : new double2(1, 1);
            var cost  = new NativeArray<double>(count * count, Allocator.Temp);
            var split = new NativeArray<int>(count * count, Allocator.Temp);
            for (int gap = 2; gap < count; gap++)
            {
                for (int i = 0; i + gap < count; i++)
                {
                    var j = i + gap;
                    var best = double.PositiveInfinity; var at = -1;
                    for (int k = i + 1; k < j; k++)
                    {
                        var c = cost[i * count + k] + cost[k * count + j]
                              + Distortion(points[i], points[k], points[j], images[i] * flip, images[k] * flip, images[j] * flip);
                        if (c < best) { best = c; at = k; }
                    }
                    cost[i * count + j] = best;
                    split[i * count + j] = at;
                }
            }
            var least = cost[count - 1];
            if (!double.IsPositiveInfinity(least) && triangles.IsCreated)
            {
                var spans = new NativeList<int2>(count, Allocator.Temp);
                spans.Add(new int2(0, count - 1));
                while (spans.Length > 0)
                {
                    var span = spans[spans.Length - 1];
                    spans.RemoveAt(spans.Length - 1);
                    if (span.y - span.x < 2)
                        continue;
                    var k = split[span.x * count + span.y];
                    triangles.Add(new int3(span.x, k, span.y));
                    spans.Add(new int2(span.x, k));
                    spans.Add(new int2(k, span.y));
                }
                spans.Dispose();
            }
            cost.Dispose();
            split.Dispose();
            return least;
        }

        static double Distortion(double2 p0, double2 p1, double2 p2, double2 q0, double2 q1, double2 q2)
        {
            var e1 = p1 - p0; var e2 = p2 - p0;
            var area = Cross(e1, e2);
            if (!(area > 1e-12 * math.max(math.lengthsq(e1), math.lengthsq(e2))))
                return double.PositiveInfinity;
            var f1 = q1 - q0; var f2 = q2 - q0;
            var imageArea = Cross(f1, f2);
            if (!(imageArea > 1e-12 * math.max(math.lengthsq(f1), math.lengthsq(f2))))
                return double.PositiveInfinity;
            var map = math.mul(new double2x2(f1, f2), math.inverse(new double2x2(e1, e2)));
            return 0.5 * area * (math.lengthsq(map.c0) + math.lengthsq(map.c1)) / math.determinant(map);
        }

        // A simple polygon, counter-clockwise (points on its straight edges included), into triangles that use every
        // point: an ear is a corner with area whose triangle holds no other point. Wound as the surface's triangles were
        // (reversed: they were clockwise in the frame). False when the triangles do not cover it.
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
