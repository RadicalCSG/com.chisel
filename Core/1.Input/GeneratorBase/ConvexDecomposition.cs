using System.Runtime.CompilerServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;

namespace Chisel.Core
{
    // Splits a closed outline into convex pieces whose corners are corners of the outline: it adds none. Each piece is
    // counter-clockwise, and each of its edges keeps the segment of the corner it starts from (SegmentVertex), which
    // also tells whether the edge is a cut between pieces rather than a side along the outline. A brush
    // has one face per plane, so the sides of a piece on one line are drawn with one surface: a piece never goes straight
    // on at a corner where the segment changes, the sides it has on one line that show have one segment, and a cut on
    // such a line has that segment too (it doesn't show). Any other cut takes the segment of the corner it starts from.
    //
    // The pieces are Hertel and Mehlhorn's (Merger): a triangulation by ear clipping, its cuts taken out again, in one
    // pass, wherever the two pieces on either side make one. Without the surface rules that is never more than four
    // times the fewest pieces; a cut kept for those rules can add to it. A convex piece turns left at each of its
    // corners, or goes straight on. Every decision is exact (ExactFloat2): there is no tolerance anywhere.
    //
    // The outline is made simple first: corners repeated one after the other become one, spikes of no width go, a corner
    // on another edge is put into that edge, and where the outline passes one position twice it becomes two outlines,
    // each filled on its own (as ChiselCurve2DBlob.SplitAtCrossings does where it crosses itself). One that still
    // crosses itself is refused.
    static class ConvexDecomposition
    {
        // Appends the pieces of the outline to pieceVertices, each piece's end to pieceEnds. False when the outline
        // crosses itself or has a coordinate that is not finite; an outline without area has no pieces.
        public static bool Decompose(NativeList<SegmentVertex> outline, ref UnsafeList<SegmentVertex> pieceVertices, ref UnsafeList<int> pieceEnds)
        {
            for (int i = 0; i < outline.Length; i++)
            {
                if (!math.all(math.isfinite(outline[i].position)))
                    return false;
            }
            var pending     = new NativeList<SegmentVertex>(outline.Length, Allocator.Temp);
            var pendingEnds = new NativeList<int>(4, Allocator.Temp);
            var loop        = new NativeList<SegmentVertex>(outline.Length, Allocator.Temp);
            try
            {
                pending.AddRange(outline.AsArray());
                pendingEnds.Add(pending.Length);
                while (pendingEnds.Length > 0)
                {
                    var end   = pendingEnds[pendingEnds.Length - 1];
                    var start = pendingEnds.Length > 1 ? pendingEnds[pendingEnds.Length - 2] : 0;
                    loop.Clear();
                    for (int n = start; n < end; n++)
                        loop.Add(pending[n]);
                    pending.ResizeUninitialized(start);
                    pendingEnds.RemoveAt(pendingEnds.Length - 1);

                    if (!Simplify(loop, pending, pendingEnds, out var split))
                        return false;
                    if (split || loop.Length < 3)
                        continue;
                    if (!DecomposeSimple(loop, ref pieceVertices, ref pieceEnds))
                        return false;
                }
                return true;
            }
            finally
            {
                pending.Dispose();
                pendingEnds.Dispose();
                loop.Dispose();
            }
        }

        // ---- making the outline simple ------------------------------------------------------------------------------

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static bool Same(float2 a, float2 b) => a.x == b.x && a.y == b.y;

        // Turning left at b, or going straight on (not turning back)
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static bool Convex(float2 a, float2 b, float2 c)
        {
            var turn = ExactFloat2.Orientation(a, b, c);
            return turn > 0 || (turn == 0 && ExactFloat2.DotSign(a, b, b, c) > 0);
        }

        // p strictly between a and b, on the segment
        static bool InsideSegment(float2 p, float2 a, float2 b)
        {
            return ExactFloat2.Orientation(a, b, p) == 0 && ExactFloat2.DotSign(a, p, a, b) > 0 && ExactFloat2.DotSign(b, p, b, a) > 0;
        }

        // The loop as a simple outline. When it passes one position twice it is split into two loops there, pushed onto
        // pending (split); false when what is left of it crosses itself
        static bool Simplify(NativeList<SegmentVertex> loop, NativeList<SegmentVertex> pending, NativeList<int> pendingEnds, out bool split)
        {
            split = false;
            while (true)
            {
                RemoveRepeatsAndSpikes(loop);
                var n = loop.Length;
                if (n < 3)
                    return true;
                if (PutCornerIntoEdge(loop))
                    continue;

                // a position passed twice: the loop from there round to there, and the rest
                for (int p = 0; p < n; p++)
                {
                    for (int q = p + 2; q < n; q++)
                    {
                        if ((p == 0 && q == n - 1) || !Same(loop[p].position, loop[q].position))
                            continue;
                        for (int v = p; v < q; v++)
                            pending.Add(loop[v]);
                        pendingEnds.Add(pending.Length);
                        for (int v = q; v < n; v++)
                            pending.Add(loop[v]);
                        for (int v = 0; v < p; v++)
                            pending.Add(loop[v]);
                        pendingEnds.Add(pending.Length);
                        split = true;
                        return true;
                    }
                }

                // no corner lies on another edge, and no two corners are one: edges that meet now cross
                for (int e = 0; e < n; e++)
                {
                    float2 a = loop[e].position, b = loop[(e + 1) % n].position;
                    float2 low = math.min(a, b), high = math.max(a, b);
                    for (int f = e + 2; f < n; f++)
                    {
                        if (e == 0 && f == n - 1)
                            continue;
                        float2 c = loop[f].position, d = loop[(f + 1) % n].position;
                        // apart in x or y: exact, as floats compare exactly
                        if (math.any(math.max(c, d) < low) || math.any(math.min(c, d) > high))
                            continue;
                        if (ExactFloat2.Orientation(a, b, c) * ExactFloat2.Orientation(a, b, d) < 0 &&
                            ExactFloat2.Orientation(c, d, a) * ExactFloat2.Orientation(c, d, b) < 0)
                            return false;
                    }
                }
                return true;
            }
        }

        // Corners at the position of the next go: the next has the edge on from there. A spike of no width (the edge out
        // turning back along the edge in) goes too: the edge from the corner before it to the one after runs along the
        // longer of the two, and takes its segment.
        static void RemoveRepeatsAndSpikes(NativeList<SegmentVertex> loop)
        {
            var changed = true;
            while (changed && loop.Length >= 3)
            {
                changed = false;
                for (int i = 0; i < loop.Length && loop.Length >= 3; i++)
                {
                    var n = loop.Length;
                    int previous = (i + n - 1) % n, next = (i + 1) % n;
                    float2 a = loop[previous].position, b = loop[i].position, c = loop[next].position;
                    if (Same(b, c))
                    {
                        loop.RemoveAt(i);
                        changed = true;
                        i--;
                        continue;
                    }
                    if (ExactFloat2.Orientation(a, b, c) != 0 || ExactFloat2.DotSign(a, b, b, c) >= 0)
                        continue;
                    if (ExactFloat2.DotSign(a, c, a, b) < 0)
                    {
                        var corner = loop[previous];
                        corner.segmentIndex = loop[i].segmentIndex;
                        loop[previous] = corner;
                    }
                    loop.RemoveAt(i);
                    changed = true;
                    i--;
                }
            }
        }

        // A corner strictly inside an edge it is not on the end of: put into that edge, the two halves keeping its
        // segment. True when one was.
        static bool PutCornerIntoEdge(NativeList<SegmentVertex> loop)
        {
            var n = loop.Length;
            for (int e = 0; e < n; e++)
            {
                var a = loop[e].position;
                var b = loop[(e + 1) % n].position;
                float2 low = math.min(a, b), high = math.max(a, b);
                for (int m = 0; m < n; m++)
                {
                    var q = loop[m].position;
                    if (math.any(q < low) || math.any(q > high) || m == e || m == (e + 1) % n || !InsideSegment(q, a, b))
                        continue;
                    var corner = new SegmentVertex { position = loop[m].position, segmentIndex = loop[e].segmentIndex };
                    loop.Add(corner);
                    for (int v = n; v > e + 1; v--)
                        loop[v] = loop[v - 1];
                    loop[e + 1] = corner;
                    return true;
                }
            }
            return false;
        }

        // A simple loop counter-clockwise: reversed when clockwise, each edge keeping its segment (which moves to the
        // corner it now starts from)
        static bool MakeCounterClockwise(NativeList<SegmentVertex> loop)
        {
            var n = loop.Length;
            // at its lowest corner (by x, then y) a simple outline turns its own way
            var lowest = 0;
            for (int i = 1; i < n; i++)
            {
                var p = loop[i].position;
                var q = loop[lowest].position;
                if (p.x < q.x || (p.x == q.x && p.y < q.y))
                    lowest = i;
            }
            var turn = ExactFloat2.Orientation(loop[(lowest + n - 1) % n].position, loop[lowest].position, loop[(lowest + 1) % n].position);
            if (turn == 0)
                return false;
            if (turn > 0)
                return true;
            var reversed = new NativeArray<SegmentVertex>(n, Allocator.Temp);
            for (int k = 0; k < n; k++)
                reversed[k] = new SegmentVertex { position = loop[n - 1 - k].position, segmentIndex = loop[(2 * n - 2 - k) % n].segmentIndex };
            for (int k = 0; k < n; k++)
                loop[k] = reversed[k];
            reversed.Dispose();
            return true;
        }

        // ---- the decomposition of a simple outline ------------------------------------------------------------------

        // Whether a cut must end at corner c of a counter-clockwise loop: where it is reflex, and where it goes straight on
        // but its segment changes. A brush has one face per plane, so a piece straight there would draw the sides on both
        // edges with one surface.
        static bool NeedsCut(NativeList<SegmentVertex> loop, int c)
        {
            var n = loop.Length;
            float2 a = loop[(c + n - 1) % n].position, b = loop[c].position, d = loop[(c + 1) % n].position;
            if (!Convex(a, b, d))
                return true;
            return ExactFloat2.Orientation(a, b, d) == 0 && loop[(c + n - 1) % n].segmentIndex != loop[c].segmentIndex;
        }

        static bool DecomposeSimple(NativeList<SegmentVertex> loop, ref UnsafeList<SegmentVertex> pieceVertices, ref UnsafeList<int> pieceEnds)
        {
            // without a turn at its lowest corner it has no area: no pieces
            if (!MakeCounterClockwise(loop))
                return true;
            var n = loop.Length;
            var cutAnywhere = false;
            for (int i = 0; i < n && !cutAnywhere; i++)
                cutAnywhere = NeedsCut(loop, i);
            if (!cutAnywhere)
            {
                for (int i = 0; i < n; i++)
                    pieceVertices.Add(loop[i]);
                pieceEnds.Add(pieceVertices.Length);
                return true;
            }
            var merger = Merger.Create(loop);
            try
            {
                return merger.Run(ref pieceVertices, ref pieceEnds);
            }
            finally
            {
                merger.Dispose();
            }
        }

        // A convex piece of the loop, its corners counted from loop corner `first` on, appended with each corner having
        // the segment of the side it starts, and whether that side is a cut. A side along the outline has the segment of
        // its edge, and shows. A cut
        // doesn't show, but where the piece goes straight on from a side that does, the brush has one face for both,
        // drawn with one surface: there the cut has the segment of that side. False when one line has sides of two
        // segments that show, which no brush can draw.
        static bool AddPiece(NativeList<SegmentVertex> loop, int first, NativeList<int> corners, ref UnsafeList<SegmentVertex> pieceVertices)
        {
            var n = loop.Length;
            var count = corners.Length;
            var start = pieceVertices.Length;
            for (int v = 0; v < count; v++)
                pieceVertices.Add(loop[(corners[v] + first) % n]);

            // a corner where the piece turns, to start the runs of sides on one line from
            var turn = -1;
            for (int v = 0; v < count && turn < 0; v++)
            {
                if (ExactFloat2.Orientation(pieceVertices[start + (v + count - 1) % count].position, pieceVertices[start + v].position,
                                            pieceVertices[start + (v + 1) % count].position) != 0)
                    turn = v;
            }
            if (turn < 0)
                return false;
            for (int runStart = 0; runStart < count; )
            {
                // the sides from corner turn + runStart on, up to the next corner where it turns
                var runEnd = runStart + 1;
                while (runEnd < count && ExactFloat2.Orientation(pieceVertices[start + (turn + runEnd - 1) % count].position,
                                                                 pieceVertices[start + (turn + runEnd) % count].position,
                                                                 pieceVertices[start + (turn + runEnd + 1) % count].position) == 0)
                    runEnd++;
                var shown = -1;
                for (int r = runStart; r < runEnd; r++)
                {
                    var v = (turn + r) % count;
                    if (corners[(v + 1) % count] != (corners[v] + 1) % n)
                        continue;
                    if (shown >= 0 && pieceVertices[start + shown].segmentIndex != pieceVertices[start + v].segmentIndex)
                        return false;
                    shown = v;
                }
                for (int r = runStart; r < runEnd && shown >= 0; r++)
                {
                    var v = (turn + r) % count;
                    var corner = pieceVertices[start + v];
                    corner.segmentIndex = pieceVertices[start + shown].segmentIndex;
                    pieceVertices[start + v] = corner;
                }
                runStart = runEnd;
            }
            for (int v = 0; v < count; v++)
            {
                var corner = pieceVertices[start + v];
                corner.cut = corners[(v + 1) % count] != (corners[v] + 1) % n;
                pieceVertices[start + v] = corner;
            }
            return true;
        }

        // ---- Hertel and Mehlhorn --------------------------------------------------------------------------------------

        // A triangulation of a simple counter-clockwise loop by ear clipping, then each of its cuts taken out again where
        // the two pieces on either side make one convex piece (Hertel and Mehlhorn, "Fast triangulation of simple
        // polygons", 1983). A piece may go straight on only where NeedsCut allows, and the sides it has on one line that
        // show have one segment. The triangles and pieces are kept as half-edges: three per triangle, an outline edge
        // without a twin.
        struct Merger
        {
            NativeList<SegmentVertex> loop;
            int n;
            NativeList<int>   origin;       // by half-edge: the corner it starts at
            NativeList<int>   next, prev;   // by half-edge: the next and previous of its piece
            NativeList<int>   twin;         // by half-edge: the same cut the other way, -1 along the outline
            NativeList<bool>  removed;      // by half-edge: a cut taken out

            public static Merger Create(NativeList<SegmentVertex> loop)
            {
                var n = loop.Length;
                var merger = new Merger
                {
                    loop        = loop,
                    n           = n,
                    origin      = new NativeList<int>((n - 2) * 3, Allocator.Temp),
                    next        = new NativeList<int>((n - 2) * 3, Allocator.Temp),
                    prev        = new NativeList<int>((n - 2) * 3, Allocator.Temp),
                    twin        = new NativeList<int>((n - 2) * 3, Allocator.Temp),
                    removed     = new NativeList<bool>((n - 2) * 3, Allocator.Temp)
                };
                return merger;
            }

            public void Dispose()
            {
                origin.Dispose(); next.Dispose(); prev.Dispose(); twin.Dispose(); removed.Dispose();
            }

            float2 P(int c) => loop[c].position;

            public bool Run(ref UnsafeList<SegmentVertex> pieceVertices, ref UnsafeList<int> pieceEnds)
            {
                if (!Triangulate())
                    return false;
                for (int h = 0; h < origin.Length; h++)
                {
                    if (twin[h] > h)
                        TryRemove(h);
                }
                return EmitPieces(ref pieceVertices, ref pieceEnds);
            }

            // Ear clipping: a corner that turns left, whose triangle with its neighbours holds no other corner, is cut
            // off, until a triangle is left. A triangle that holds a corner holds one that doesn't turn left (the one
            // nearest the ear's tip has the inside of the triangle on its side), so only those are tested.
            bool Triangulate()
            {
                var before = new NativeArray<int>(n, Allocator.Temp);
                var after  = new NativeArray<int>(n, Allocator.Temp);
                var ear    = new NativeArray<bool>(n, Allocator.Temp);
                var bent   = new NativeList<int>(n, Allocator.Temp);
                var bentAt = new NativeArray<int>(n, Allocator.Temp);
                var bentPosition = new NativeList<float2>(n, Allocator.Temp);
                var edges  = new NativeHashMap<int, int>(n * 2, Allocator.Temp);
                try
                {
                    for (int c = 0; c < n; c++)
                    {
                        before[c] = (c + n - 1) % n;
                        after[c]  = (c + 1) % n;
                        bentAt[c] = -1;
                    }
                    for (int c = 0; c < n; c++)
                    {
                        if (ExactFloat2.Orientation(P(before[c]), P(c), P(after[c])) <= 0)
                        {
                            bentAt[c] = bent.Length;
                            bent.Add(c);
                            bentPosition.Add(P(c));
                        }
                    }
                    for (int c = 0; c < n; c++)
                        ear[c] = IsEar(c, before, after, bent, bentPosition);

                    var left = n;
                    for (int c = 0, tries = 0; left > 3; )
                    {
                        if (!ear[c])
                        {
                            c = after[c];
                            if (++tries > left)
                                return false;
                            continue;
                        }
                        tries = 0;
                        int a = before[c], b = after[c];
                        AddTriangle(a, c, b, edges);
                        after[a]  = b;
                        before[b] = a;
                        left--;
                        Unbend(a, before, after, bent, bentAt, bentPosition);
                        Unbend(b, before, after, bent, bentAt, bentPosition);
                        ear[a] = IsEar(a, before, after, bent, bentPosition);
                        ear[b] = IsEar(b, before, after, bent, bentPosition);
                        c = b;
                    }
                    for (int c = 0; c < n; c++)
                    {
                        if (after[before[c]] != c)
                            continue;
                        // the last triangle: a corner still in the loop
                        AddTriangle(before[c], c, after[c], edges);
                        break;
                    }
                    return true;
                }
                finally
                {
                    before.Dispose(); after.Dispose(); ear.Dispose(); bent.Dispose(); bentAt.Dispose(); bentPosition.Dispose(); edges.Dispose();
                }
            }

            // Corner c once it turns left, after a neighbour was cut off: no longer one an ear's triangle can hold
            void Unbend(int c, NativeArray<int> before, NativeArray<int> after, NativeList<int> bent, NativeArray<int> bentAt, NativeList<float2> bentPosition)
            {
                if (bentAt[c] < 0 || ExactFloat2.Orientation(P(before[c]), P(c), P(after[c])) <= 0)
                    return;
                var at = bentAt[c];
                var last = bent.Length - 1;
                bent[at] = bent[last];
                bentPosition[at] = bentPosition[last];
                bentAt[bent[at]] = at;
                bent.RemoveAt(last);
                bentPosition.RemoveAt(last);
                bentAt[c] = -1;
            }

            // The triangle's bounds rule out most corners with float comparisons alone, which are exact
            bool IsEar(int c, NativeArray<int> before, NativeArray<int> after, NativeList<int> bent, NativeList<float2> bentPosition)
            {
                int a = before[c], b = after[c];
                float2 pa = P(a), pc = P(c), pb = P(b);
                if (ExactFloat2.Orientation(pa, pc, pb) <= 0)
                    return false;
                var low  = math.min(pa, math.min(pc, pb));
                var high = math.max(pa, math.max(pc, pb));
                for (int i = 0; i < bentPosition.Length; i++)
                {
                    var q = bentPosition[i];
                    if (math.any(q < low) || math.any(q > high))
                        continue;
                    var w = bent[i];
                    if (w == a || w == b || w == c)
                        continue;
                    if (ExactFloat2.Orientation(pa, pc, q) >= 0 && ExactFloat2.Orientation(pc, pb, q) >= 0 && ExactFloat2.Orientation(pb, pa, q) >= 0)
                        return false;
                }
                return true;
            }

            // The triangle a, c, b (counter-clockwise) as three half-edges, each cut paired with its twin
            void AddTriangle(int a, int c, int b, NativeHashMap<int, int> edges)
            {
                var first = origin.Length;
                origin.Add(a); origin.Add(c); origin.Add(b);
                for (int e = 0; e < 3; e++)
                {
                    next.Add(first + (e + 1) % 3);
                    prev.Add(first + (e + 2) % 3);
                    twin.Add(-1);
                    removed.Add(false);
                }
                for (int e = 0; e < 3; e++)
                {
                    int from = origin[first + e], to = origin[first + (e + 1) % 3];
                    if (to == (from + 1) % n)
                        continue;
                    if (edges.TryGetValue(to * n + from, out var other))
                    {
                        twin[first + e] = other;
                        twin[other] = first + e;
                    } else
                        edges.TryAdd(from * n + to, first + e);
                }
            }

            int Destination(int h) => origin[next[h]];

            // A piece's corner at v, from corner a to corner c: turning left, or going straight on. Straight on where the
            // segment changes (NeedsCut) leaves two segments shown on one line, which OneSegment refuses
            bool ConvexAt(int a, int v, int c)
            {
                var turn = ExactFloat2.Orientation(P(a), P(v), P(c));
                return turn > 0 || (turn == 0 && ExactFloat2.DotSign(P(a), P(v), P(v), P(c)) > 0);
            }

            bool Straight(int h)
            {
                return ExactFloat2.Orientation(P(origin[prev[h]]), P(origin[h]), P(Destination(h))) == 0;
            }

            // The cut h and its twin taken out, when the piece they make is convex at both ends and its sides on one
            // line that show have one segment
            void TryRemove(int h)
            {
                var t = twin[h];
                int hp = prev[h], hn = next[h], tp = prev[t], tn = next[t];
                int u = origin[h], v = origin[t];
                if (!ConvexAt(origin[hp], u, Destination(tn)) || !ConvexAt(origin[tp], v, Destination(hn)))
                    return;
                next[hp] = tn; prev[tn] = hp;
                next[tp] = hn; prev[hn] = tp;
                if ((Straight(tn) && !OneSegment(tn)) || (Straight(hn) && !OneSegment(hn)))
                {
                    next[hp] = h; prev[tn] = t;
                    next[tp] = t; prev[hn] = h;
                    return;
                }
                removed[h] = true;
                removed[t] = true;
            }

            // Whether the sides on one line through the start of h, that show, have one segment
            bool OneSegment(int h)
            {
                var side = h;
                for (int steps = 0; Straight(side) && steps < n; steps++)
                    side = prev[side];
                var shown = -1;
                for (int steps = 0; steps < n; steps++)
                {
                    if (twin[side] < 0)
                    {
                        var segment = loop[origin[side]].segmentIndex;
                        if (shown >= 0 && shown != segment)
                            return false;
                        shown = segment;
                    }
                    side = next[side];
                    if (!Straight(side))
                        break;
                }
                return true;
            }

            bool EmitPieces(ref UnsafeList<SegmentVertex> pieceVertices, ref UnsafeList<int> pieceEnds)
            {
                var visited = new NativeArray<bool>(origin.Length, Allocator.Temp);
                var corners = new NativeList<int>(n, Allocator.Temp);
                try
                {
                    for (int h = 0; h < origin.Length; h++)
                    {
                        if (removed[h] || visited[h])
                            continue;
                        corners.Clear();
                        var side = h;
                        for (int steps = 0; !visited[side]; steps++)
                        {
                            if (steps > n)
                                return false;
                            visited[side] = true;
                            corners.Add(origin[side]);
                            side = next[side];
                        }
                        if (side != h || !AddPiece(loop, 0, corners, ref pieceVertices))
                            return false;
                        pieceEnds.Add(pieceVertices.Length);
                    }
                    return true;
                }
                finally
                {
                    visited.Dispose();
                    corners.Dispose();
                }
            }
        }
    }
}
