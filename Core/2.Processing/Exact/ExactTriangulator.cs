using System;
using System.Runtime.CompilerServices;

namespace Chisel.Core
{
    enum ExactTriangulationFailure : byte
    {
        None,
        UnbalancedVertex,   // a vertex with more edges in than out: the boundary is not closed (a bug upstream)
        UnpairedEdge,       // at a vertex where several boundaries meet, an edge had no partner in angular order
        NoBridge,           // a hole could not be connected to the boundary around it
        NoEar,              // ear clipping ran out of ears before the polygon was done
    }

    struct ExactTriangulator : IDisposable
    {
        struct Node
        {
            public int vertex;
            public int prev, next;
            public int loop;
            public int edgeIn, edgeOut;     // the input edges this node sits between
            public bool removed;
        }

        struct Ray
        {
            public int  edge;
            public bool outgoing;
        }

        ExactList<Node>   nodes;
        ExactList<int>    outCount, inCount, firstOut;
        ExactList<int>    outList;
        ExactList<int>    nextEdge;         // per input edge: the edge that follows it on its loop
        ExactList<int>    edgeNode;         // per input edge: the node at its start vertex
        ExactList<Ray>    rays;
        ExactList<int>    loopStart;        // a node on each loop
        ExactList<byte>   loopState;        // 0 outer, 1 hole waiting for a bridge, 2 hole joined
        ExactList<int>    loopLeftmost;     // node of each loop's lexicographically smallest vertex
        ExactList<int>    holes;
        ExactList<int>    candidates;
        ExactList<double> nodeBounds;       // approximate (u, v) per vertex, for conservative culling (FilterSlack)
        ExactList<int>    twins;            // per triangle corner slot (the edge from that corner on): the slot of the same
                                            // edge in the triangle across it, -1 where it may not be flipped
        ExactList<int>    edgeRecords, edgeRecordsSorted, vertexCounts;
        ExactList<int>    flipStack;

        int axisU, axisV;
        ExactPlane plane;

        public static ExactTriangulator Create()
        {
            return new ExactTriangulator
            {
                nodes             = new ExactList<Node>(64),
                outCount          = new ExactList<int>(64),
                inCount           = new ExactList<int>(64),
                firstOut          = new ExactList<int>(64),
                outList           = new ExactList<int>(64),
                nextEdge          = new ExactList<int>(64),
                edgeNode          = new ExactList<int>(64),
                rays              = new ExactList<Ray>(16),
                loopStart         = new ExactList<int>(8),
                loopState         = new ExactList<byte>(8),
                loopLeftmost      = new ExactList<int>(8),
                holes             = new ExactList<int>(8),
                candidates        = new ExactList<int>(64),
                nodeBounds        = new ExactList<double>(128),
                twins             = new ExactList<int>(64),
                edgeRecords       = new ExactList<int>(64),
                edgeRecordsSorted = new ExactList<int>(64),
                vertexCounts      = new ExactList<int>(64),
                flipStack         = new ExactList<int>(64),
            };
        }

        public void Dispose()
        {
            nodes.Dispose(); outCount.Dispose(); inCount.Dispose(); firstOut.Dispose(); outList.Dispose(); nextEdge.Dispose();
            edgeNode.Dispose(); rays.Dispose(); loopStart.Dispose(); loopState.Dispose(); loopLeftmost.Dispose(); holes.Dispose();
            candidates.Dispose(); nodeBounds.Dispose(); twins.Dispose(); edgeRecords.Dispose(); edgeRecordsSorted.Dispose();
            vertexCounts.Dispose(); flipStack.Dispose();
        }

        // ---- exact helpers ----------------------------------------------------------------------------------------

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        int Orientation(in ExactOutputVertex a, in ExactOutputVertex b, in ExactOutputVertex c)
        {
            return ExactPredicates.Orientation(a.vertex, b.vertex, c.vertex, axisU, axisV);
        }

        // Direction of an edge's ray in the projection, exactly: n(plane) x n(line), negated when `forward` is false.
        void RayDirection(in ExactPlane linePlane, bool forward, out Int128 du, out Int128 dv)
        {
            var dx = Int128.DifferenceOfProducts(plane.b, linePlane.c, plane.c, linePlane.b);
            var dy = Int128.DifferenceOfProducts(plane.c, linePlane.a, plane.a, linePlane.c);
            var dz = Int128.DifferenceOfProducts(plane.a, linePlane.b, plane.b, linePlane.a);
            du = axisU == 0 ? dx : (axisU == 1 ? dy : dz);
            dv = axisV == 0 ? dx : (axisV == 1 ? dy : dz);
            if (!forward)
            {
                du = Int128.Negate(du);
                dv = Int128.Negate(dv);
            }
        }

        static int Cross(Int128 au, Int128 av, Int128 bu, Int128 bv)
        {
            return BigInt.Sub(BigInt.Mul(BigInt.FromInt128(au), BigInt.FromInt128(bv)),
                              BigInt.Mul(BigInt.FromInt128(av), BigInt.FromInt128(bu))).Sign();
        }

        // Counter-clockwise order of two directions by angle from +u: -1 when a comes first, 1 when b, 0 when equal.
        static int CompareAngle(Int128 au, Int128 av, Int128 bu, Int128 bv)
        {
            int halfA = (av.Sign() > 0 || (av.IsZero && au.Sign() > 0)) ? 0 : 1;
            int halfB = (bv.Sign() > 0 || (bv.IsZero && bu.Sign() > 0)) ? 0 : 1;
            if (halfA != halfB)
                return halfA < halfB ? -1 : 1;
            return -Cross(au, av, bu, bv);   // a first when b is to its left
        }

        // Whether direction t lies strictly inside the sector swept counter-clockwise from direction a to direction b.
        static bool DirectionInSector(Int128 au, Int128 av, Int128 bu, Int128 bv, Int128 tu, Int128 tv)
        {
            int ab = Cross(au, av, bu, bv), at = Cross(au, av, tu, tv), tb = Cross(tu, tv, bu, bv);
            if (ab > 0) return at > 0 && tb > 0;
            if (ab < 0) return at > 0 || tb > 0;
            // a and b on one line: opposite (a half-plane) or the same (all around but the ray itself)
            bool opposite = BigInt.Add(BigInt.Mul(BigInt.FromInt128(au), BigInt.FromInt128(bu)),
                                       BigInt.Mul(BigInt.FromInt128(av), BigInt.FromInt128(bv))).Sign() < 0;
            if (opposite) return at > 0;
            return at != 0 || BigInt.Add(BigInt.Mul(BigInt.FromInt128(au), BigInt.FromInt128(tu)),
                                         BigInt.Mul(BigInt.FromInt128(av), BigInt.FromInt128(tv))).Sign() < 0;
        }

        // ---- the triangulation ------------------------------------------------------------------------------------

        // edges: the region is on their left. lines: the planes the edges lie on. Appends triangles (three vertex indices,
        // counter-clockwise).
        public ExactTriangulationFailure Triangulate(in ExactPlane facePlane,
                                                     ref ExactList<ExactOutputVertex> vertices,
                                                     ref ExactList<ExactOutputEdge> edges,
                                                     ref ExactList<ExactLine> lines,
                                                     ref ExactList<int> triangles)
        {
            plane = facePlane;
            int dominant = plane.DominantAxis();
            long dominantComponent = dominant == 0 ? plane.a : (dominant == 1 ? plane.b : plane.c);
            ExactPredicates.ProjectionAxes(dominant, dominantComponent < 0, out axisU, out axisV);
            if (edges.Length == 0)
                return ExactTriangulationFailure.None;
            int firstTriangle = triangles.Length;

            nodeBounds.Clear();
            for (int v = 0; v < vertices.Length; v++)
            {
                ref var x = ref vertices[v].vertex;
                nodeBounds.Add(x.CoordinateD(axisU) / x.Wd);
                nodeBounds.Add(x.CoordinateD(axisV) / x.Wd);
            }

            var failure = BuildLoops(ref vertices, ref edges, ref lines);
            if (failure != ExactTriangulationFailure.None)
                return failure;
            ClassifyLoops(ref vertices, ref edges, ref lines);
            failure = BridgeHoles(ref vertices);
            if (failure != ExactTriangulationFailure.None)
                return failure;
            failure = ClipEars(ref vertices, ref triangles);
            if (failure != ExactTriangulationFailure.None)
                return failure;
            MakeDelaunay(ref vertices, ref edges, ref triangles, firstTriangle);
            return ExactTriangulationFailure.None;
        }

        ExactTriangulationFailure BuildLoops(ref ExactList<ExactOutputVertex> vertices, ref ExactList<ExactOutputEdge> edges,
                                            ref ExactList<ExactLine> lines)
        {
            int vertexCount = vertices.Length;
            outCount.Clear(); inCount.Clear(); firstOut.Clear();
            outCount.Resize(vertexCount); inCount.Resize(vertexCount); firstOut.Resize(vertexCount + 1);
            for (int e = 0; e < edges.Length; e++)
            {
                outCount[edges[e].from]++;
                inCount[edges[e].to]++;
            }
            for (int v = 0; v < vertexCount; v++)
                if (outCount[v] != inCount[v])
                    return ExactTriangulationFailure.UnbalancedVertex;
            int running = 0;
            for (int v = 0; v < vertexCount; v++) { firstOut[v] = running; running += outCount[v]; }
            firstOut[vertexCount] = running;
            outList.Clear(); outList.Resize(edges.Length);
            for (int v = 0; v < vertexCount; v++) outCount[v] = 0;
            for (int e = 0; e < edges.Length; e++)
            {
                int from = edges[e].from;
                outList[firstOut[from] + outCount[from]] = e;
                outCount[from]++;
            }

            nextEdge.Clear(); nextEdge.Resize(edges.Length);
            for (int e = 0; e < edges.Length; e++)
            {
                int to = edges[e].to;
                nextEdge[e] = outCount[to] == 1 ? outList[firstOut[to]] : -1;
            }
            for (int v = 0; v < vertexCount; v++)
            {
                if (outCount[v] < 2)
                    continue;
                // Several boundaries meet here: order every edge's ray around the vertex and pair each incoming edge with
                // the ray next to it clockwise, which has to be an outgoing edge.
                rays.Clear();
                for (int e = 0; e < edges.Length; e++)
                {
                    if (edges[e].from == v) rays.Add(new Ray { edge = e, outgoing = true });
                    if (edges[e].to == v)   rays.Add(new Ray { edge = e, outgoing = false });
                }
                for (int i = 1; i < rays.Length; i++)
                {
                    var item = rays[i];
                    int j = i - 1;
                    while (j >= 0 && CompareRays(item, rays[j], ref edges, ref lines) < 0)
                    {
                        rays[j + 1] = rays[j];
                        j--;
                    }
                    rays[j + 1] = item;
                }
                for (int i = 0; i < rays.Length; i++)
                {
                    if (rays[i].outgoing)
                        continue;
                    var clockwise = rays[(i + rays.Length - 1) % rays.Length];
                    if (!clockwise.outgoing)
                        return ExactTriangulationFailure.UnpairedEdge;
                    nextEdge[rays[i].edge] = clockwise.edge;
                }
            }

            nodes.Clear();
            loopStart.Clear();
            edgeNode.Clear(); edgeNode.Resize(edges.Length);
            for (int e = 0; e < edges.Length; e++) edgeNode[e] = -1;
            for (int e = 0; e < edges.Length; e++)
            {
                if (edgeNode[e] >= 0)
                    continue;
                int loop = loopStart.Length;
                int first = nodes.Length;
                int current = e;
                while (edgeNode[current] < 0)
                {
                    edgeNode[current] = nodes.Length;
                    nodes.Add(new Node { vertex = edges[current].from, edgeOut = current, loop = loop });
                    current = nextEdge[current];
                    if (current < 0)
                        return ExactTriangulationFailure.UnpairedEdge;
                }
                if (current != e)
                    return ExactTriangulationFailure.UnpairedEdge;
                int last = nodes.Length - 1;
                for (int n = first; n <= last; n++)
                {
                    ref var node = ref nodes[n];
                    node.prev = n == first ? last : n - 1;
                    node.next = n == last ? first : n + 1;
                }
                for (int n = first; n <= last; n++)
                    nodes[n].edgeIn = nodes[nodes[n].prev].edgeOut;
                loopStart.Add(first);
            }
            return ExactTriangulationFailure.None;
        }

        int CompareRays(in Ray a, in Ray b, ref ExactList<ExactOutputEdge> edges, ref ExactList<ExactLine> lines)
        {
            // an outgoing edge's ray runs along the edge, an incoming edge's ray back along it
            RayDirection(lines[edges[a.edge].line].plane, (edges[a.edge].forward != 0) == a.outgoing, out var au, out var av);
            RayDirection(lines[edges[b.edge].line].plane, (edges[b.edge].forward != 0) == b.outgoing, out var bu, out var bv);
            return CompareAngle(au, av, bu, bv);
        }

        void ClassifyLoops(ref ExactList<ExactOutputVertex> vertices, ref ExactList<ExactOutputEdge> edges,
                           ref ExactList<ExactLine> lines)
        {
            loopState.Clear();
            loopLeftmost.Clear();
            var left = Int128.FromLong(-1);
            var zero = Int128.FromLong(0);
            for (int l = 0; l < loopStart.Length; l++)
            {
                int start = loopStart[l];
                int best = start;
                for (int n = nodes[start].next; n != start; n = nodes[n].next)
                {
                    if (CompareLexicographic(ref vertices, nodes[n].vertex, nodes[best].vertex) < 0)
                        best = n;
                }
                // Every visit of the loop to that point: does the region's sector there reach to the left (-u)?
                bool hole = false;
                int leftmostVertex = nodes[best].vertex;
                int m = start;
                do
                {
                    ref var node = ref nodes[m];
                    if (node.vertex == leftmostVertex)
                    {
                        var outEdge = edges[node.edgeOut];
                        var inEdge  = edges[node.edgeIn];
                        RayDirection(lines[outEdge.line].plane, outEdge.forward != 0, out var ou, out var ov);
                        RayDirection(lines[inEdge.line].plane, inEdge.forward == 0, out var iu, out var iv);
                        if (DirectionInSector(ou, ov, iu, iv, left, zero))
                            hole = true;
                    }
                    m = node.next;
                } while (m != start);
                loopState.Add((byte)(hole ? 1 : 0));
                loopLeftmost.Add(best);
            }
        }

        int CompareLexicographic(ref ExactList<ExactOutputVertex> vertices, int a, int b)
        {
            if (a == b) return 0;
            int c = ExactPredicates.CompareCoordinate(vertices[a].vertex, vertices[b].vertex, axisU);
            if (c == 0) c = ExactPredicates.CompareCoordinate(vertices[a].vertex, vertices[b].vertex, axisV);
            return c;
        }

        // Whether `target` lies strictly inside the region's sector at node n: counter-clockwise from the direction to its
        // next vertex to the direction to its previous one.
        bool InSector(ref ExactList<ExactOutputVertex> vertices, int n, in ExactOutputVertex target)
        {
            ref var node = ref nodes[n];
            ref var v    = ref vertices[node.vertex];
            ref var next = ref vertices[nodes[node.next].vertex];
            ref var prev = ref vertices[nodes[node.prev].vertex];
            int turn      = Orientation(prev, v, next);
            int leftOfOut = Orientation(v, next, target);   // > 0: left of v -> next
            int leftOfIn  = Orientation(v, prev, target);   // < 0: right of v -> prev
            if (turn > 0) return leftOfOut > 0 && leftOfIn < 0;
            if (turn < 0) return leftOfOut > 0 || leftOfIn < 0;
            return leftOfOut > 0;
        }

        const double kUnitRoundoff = 1.1102230246251565e-16;   // 2^-53

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static double FilterSlack(double largestMagnitude) => largestMagnitude * (16 * kUnitRoundoff);

        // Conservative approximate test: false only when the boxes of (a, b) and (c, d) are apart (see FilterSlack).
        bool BoxesMayMeet(int a, int b, int c, int d)
        {
            double au = nodeBounds[a * 2], av = nodeBounds[a * 2 + 1], bu = nodeBounds[b * 2], bv = nodeBounds[b * 2 + 1];
            double cu = nodeBounds[c * 2], cv = nodeBounds[c * 2 + 1], du = nodeBounds[d * 2], dv = nodeBounds[d * 2 + 1];
            double scale = Math.Max(Math.Max(Math.Max(Math.Abs(au), Math.Abs(av)), Math.Max(Math.Abs(bu), Math.Abs(bv))),
                                    Math.Max(Math.Max(Math.Abs(cu), Math.Abs(cv)), Math.Max(Math.Abs(du), Math.Abs(dv))));
            double slack = FilterSlack(scale);
            if (Math.Max(au, bu) + slack < Math.Min(cu, du) || Math.Max(cu, du) + slack < Math.Min(au, bu)) return false;
            if (Math.Max(av, bv) + slack < Math.Min(cv, dv) || Math.Max(cv, dv) + slack < Math.Min(av, bv)) return false;
            return true;
        }

        // Whether the segments (p, q) and (r, s) have a point in common other than an endpoint they share by vertex.
        bool SegmentsMeet(ref ExactList<ExactOutputVertex> vertices, int p, int q, int r, int s)
        {
            if (!BoxesMayMeet(p, q, r, s))
                return false;
            bool shareP = r == p || s == p, shareQ = r == q || s == q;
            ref var vp = ref vertices[p]; ref var vq = ref vertices[q];
            ref var vr = ref vertices[r]; ref var vs = ref vertices[s];
            if (shareP && shareQ)
                return true;    // the same segment
            if (shareP || shareQ)
            {
                // sharing one endpoint, they meet elsewhere only when collinear and pointing the same way from it
                int other  = (r == p || r == q) ? s : r;
                if (Orientation(vp, vq, vertices[other]) != 0)
                    return false;
                int shared = shareP ? p : q;
                int far    = shareP ? q : p;
                return SameDirection(vertices[shared], vertices[far], vertices[other]);
            }
            int o1 = Orientation(vp, vq, vr);
            int o2 = Orientation(vp, vq, vs);
            if (o1 != 0 && o1 == o2) return false;
            int o3 = Orientation(vr, vs, vp);
            int o4 = Orientation(vr, vs, vq);
            if (o3 != 0 && o3 == o4) return false;
            if (o1 == 0 && o2 == 0)
                return OnSegment(vertices[p], vertices[q], vertices[r]) || OnSegment(vertices[p], vertices[q], vertices[s]) ||
                       OnSegment(vertices[r], vertices[s], vertices[p]);
            return true;
        }

        // For collinear points: whether b and c lie on the same side of a.
        static bool SameDirection(in ExactOutputVertex a, in ExactOutputVertex b, in ExactOutputVertex c)
        {
            for (int axis = 0; axis < 3; axis++)
            {
                int sb = ExactPredicates.CompareCoordinate(b.vertex, a.vertex, axis);
                int sc = ExactPredicates.CompareCoordinate(c.vertex, a.vertex, axis);
                if (sb != 0 || sc != 0)
                    return sb == sc;
            }
            return true;
        }

        // For collinear points: whether x lies on the closed segment (a, b).
        static bool OnSegment(in ExactOutputVertex a, in ExactOutputVertex b, in ExactOutputVertex x)
        {
            for (int axis = 0; axis < 3; axis++)
            {
                int sa = ExactPredicates.CompareCoordinate(x.vertex, a.vertex, axis);
                int sb = ExactPredicates.CompareCoordinate(x.vertex, b.vertex, axis);
                if ((sa > 0 && sb > 0) || (sa < 0 && sb < 0))
                    return false;
            }
            return true;
        }

        ExactTriangulationFailure BridgeHoles(ref ExactList<ExactOutputVertex> vertices)
        {
            holes.Clear();
            for (int l = 0; l < loopStart.Length; l++)
                if (loopState[l] == 1)
                    holes.Add(l);
            // left to right by leftmost vertex, so every hole's left is joined up by the time it is bridged
            for (int i = 1; i < holes.Length; i++)
            {
                int item = holes[i];
                int j = i - 1;
                while (j >= 0 && CompareLexicographic(ref vertices, nodes[loopLeftmost[item]].vertex, nodes[loopLeftmost[holes[j]]].vertex) < 0)
                {
                    holes[j + 1] = holes[j];
                    j--;
                }
                holes[j + 1] = item;
            }
            for (int h = 0; h < holes.Length; h++)
            {
                var failure = Bridge(ref vertices, holes[h], loopLeftmost[holes[h]]);
                if (failure != ExactTriangulationFailure.None)
                    return failure;
            }
            return ExactTriangulationFailure.None;
        }

        ExactTriangulationFailure Bridge(ref ExactList<ExactOutputVertex> vertices, int hole, int from)
        {
            candidates.Clear();
            int fromVertex = nodes[from].vertex;
            for (int n = 0; n < nodes.Length; n++)
            {
                ref var node = ref nodes[n];
                if (node.removed || node.loop == hole || loopState[node.loop] == 1 || node.vertex == fromVertex)
                    continue;
                if (ExactPredicates.CompareCoordinate(vertices[node.vertex].vertex, vertices[fromVertex].vertex, axisU) > 0)
                    continue;
                candidates.Add(n);
            }
            for (int i = 1; i < candidates.Length; i++)
            {
                int item = candidates[i];
                int j = i - 1;
                while (j >= 0 && ExactPredicates.CompareCoordinate(vertices[nodes[candidates[j]].vertex].vertex,
                                                                   vertices[nodes[item].vertex].vertex, axisU) < 0)
                {
                    candidates[j + 1] = candidates[j];
                    j--;
                }
                candidates[j + 1] = item;
            }
            for (int c = 0; c < candidates.Length; c++)
            {
                int target = candidates[c];
                if (!BridgeVisible(ref vertices, from, target))
                    continue;
                Splice(from, target, nodes[target].loop);
                loopState[hole] = 2;
                return ExactTriangulationFailure.None;
            }
            return ExactTriangulationFailure.NoBridge;
        }

        // A bridge from node `from` to node `to` is usable when it leaves and arrives inside the region's sectors and meets
        // no edge of any loop anywhere but at its own two endpoints.
        bool BridgeVisible(ref ExactList<ExactOutputVertex> vertices, int from, int to)
        {
            int a = nodes[from].vertex, b = nodes[to].vertex;
            if (!InSector(ref vertices, from, vertices[b]) || !InSector(ref vertices, to, vertices[a]))
                return false;
            for (int n = 0; n < nodes.Length; n++)
            {
                if (nodes[n].removed)
                    continue;
                if (SegmentsMeet(ref vertices, a, b, nodes[n].vertex, nodes[nodes[n].next].vertex))
                    return false;
            }
            return true;
        }

        // Earcut's splice: the loop through `to` continues into the hole at `from`, around it, and back:
        //   to -> from -> (around the hole) -> from' -> to' -> (the rest of to's loop)
        void Splice(int from, int to, int loop)
        {
            int toNext   = nodes[to].next;
            int fromPrev = nodes[from].prev;
            int from2 = nodes.Length;
            nodes.Add(new Node { vertex = nodes[from].vertex, edgeIn = -1, edgeOut = -1, loop = loop });
            int to2 = nodes.Length;
            nodes.Add(new Node { vertex = nodes[to].vertex, edgeIn = -1, edgeOut = -1, loop = loop });

            // the hole's nodes join the loop
            int n = from;
            do { nodes[n].loop = loop; n = nodes[n].next; } while (n != from);

            nodes[to].next = from;          nodes[from].prev = to;
            nodes[fromPrev].next = from2;   nodes[from2].prev = fromPrev;
            nodes[from2].next = to2;        nodes[to2].prev = from2;
            nodes[to2].next = toNext;       nodes[toNext].prev = to2;
        }

        ExactTriangulationFailure ClipEars(ref ExactList<ExactOutputVertex> vertices, ref ExactList<int> triangles)
        {
            for (int l = 0; l < loopStart.Length; l++)
            {
                if (loopState[l] != 0)
                    continue;
                int start = loopStart[l];
                int remaining = 0;
                int n = start;
                do { remaining++; n = nodes[n].next; } while (n != start);

                int current = start;
                int sinceLastEar = 0;
                while (remaining > 3)
                {
                    if (IsEar(ref vertices, current))
                    {
                        ref var node = ref nodes[current];
                        triangles.Add(nodes[node.prev].vertex);
                        triangles.Add(node.vertex);
                        triangles.Add(nodes[node.next].vertex);
                        int prev = node.prev, next = node.next;
                        nodes[prev].next = next;
                        nodes[next].prev = prev;
                        node.removed = true;
                        remaining--;
                        current = prev;         // the corner before may have become an ear
                        sinceLastEar = 0;
                        continue;
                    }
                    current = nodes[current].next;
                    if (++sinceLastEar > remaining)
                        return ExactTriangulationFailure.NoEar;
                }
                {
                    ref var node = ref nodes[current];
                    int a = nodes[node.prev].vertex, b = node.vertex, c = nodes[node.next].vertex;
                    int turn = Orientation(vertices[a], vertices[b], vertices[c]);
                    if (turn > 0)
                    {
                        triangles.Add(a); triangles.Add(b); triangles.Add(c);
                    }
                    else if (turn < 0)
                        return ExactTriangulationFailure.NoEar;
                    // collinear: the last three enclose nothing
                }
            }
            return ExactTriangulationFailure.None;
        }

        bool IsEar(ref ExactList<ExactOutputVertex> vertices, int n)
        {
            ref var node = ref nodes[n];
            int ia = nodes[node.prev].vertex, ib = node.vertex, ic = nodes[node.next].vertex;
            if (ia == ib || ib == ic || ia == ic)
                return false;
            ref var a = ref vertices[ia]; ref var b = ref vertices[ib]; ref var c = ref vertices[ic];
            if (Orientation(a, b, c) <= 0)
                return false;
            // the diagonal a -> c has to leave a inside the region and arrive at c from inside it
            if (!InSector(ref vertices, node.prev, c) || !InSector(ref vertices, node.next, a))
                return false;
            // and no other vertex may lie in the triangle, its edges included
            for (int m = nodes[node.next].next; m != node.prev; m = nodes[m].next)
            {
                int ip = nodes[m].vertex;
                if (ip == ia || ip == ib || ip == ic)
                    continue;
                if (!PointMayBeInTriangle(ia, ib, ic, ip))
                    continue;
                ref var p = ref vertices[ip];
                if (Orientation(a, b, p) >= 0 && Orientation(b, c, p) >= 0 && Orientation(c, a, p) >= 0)
                    return false;
            }
            return true;
        }

        // ---- Lawson's flips (step 4) ------------------------------------------------------------------------------

        // Flips the inner edges of the triangles from `first` on until every one that may be flipped is locally Delaunay in
        // the drawn floats (see the top).
        void MakeDelaunay(ref ExactList<ExactOutputVertex> vertices, ref ExactList<ExactOutputEdge> edges,
                          ref ExactList<int> triangles, int first)
        {
            int slotCount = triangles.Length - first;
            if (slotCount < 6)
                return;     // one triangle has no inner edge
            FindTwins(vertices.Length, ref triangles, first, ref edges);
            flipStack.Clear();
            for (int slot = 0; slot < slotCount; slot++)
                if (twins[slot] > slot)
                    flipStack.Add(slot);
            while (flipStack.Length > 0)
            {
                int slot = flipStack[flipStack.Length - 1];
                flipStack.RemoveAtSwapBack(flipStack.Length - 1);
                if (twins[slot] >= 0)
                    TryFlip(ref vertices, ref triangles, first, slot);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static int NextCorner(int k) => k == 2 ? 0 : k + 1;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static int PreviousCorner(int k) => k == 0 ? 2 : k - 1;

        // An edge record: below slotCount the edge of a triangle from its corner slot to the next corner, from there on an
        // input edge.
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static void RecordEnds(ref ExactList<int> triangles, int first, int slotCount, ref ExactList<ExactOutputEdge> edges,
                               int record, out int from, out int to)
        {
            if (record < slotCount)
            {
                int triangle = record / 3;
                from = triangles[first + record];
                to   = triangles[first + triangle * 3 + NextCorner(record - triangle * 3)];
            }
            else
            {
                ref var edge = ref edges[record - slotCount];
                from = edge.from;
                to   = edge.to;
            }
        }

        void FindTwins(int vertexCount, ref ExactList<int> triangles, int first, ref ExactList<ExactOutputEdge> edges)
        {
            int slotCount   = triangles.Length - first;
            int recordCount = slotCount + edges.Length;
            twins.Clear(); twins.Resize(slotCount);
            edgeRecords.Clear(); edgeRecords.Resize(recordCount);
            edgeRecordsSorted.Clear(); edgeRecordsSorted.Resize(recordCount);
            for (int r = 0; r < recordCount; r++)
                edgeRecords[r] = r;
            CountingSort(ref edgeRecords, ref edgeRecordsSorted, vertexCount, ref triangles, first, slotCount, ref edges, byLarger: true);
            CountingSort(ref edgeRecordsSorted, ref edgeRecords, vertexCount, ref triangles, first, slotCount, ref edges, byLarger: false);

            for (int i = 0; i < recordCount;)
            {
                RecordEnds(ref triangles, first, slotCount, ref edges, edgeRecords[i], out int from, out int to);
                int low = Math.Min(from, to), high = Math.Max(from, to);
                int end = i + 1;
                while (end < recordCount)
                {
                    RecordEnds(ref triangles, first, slotCount, ref edges, edgeRecords[end], out int nextFrom, out int nextTo);
                    if (Math.Min(nextFrom, nextTo) != low || Math.Max(nextFrom, nextTo) != high)
                        break;
                    end++;
                }
                for (int k = i; k < end; k++)
                    if (edgeRecords[k] < slotCount)
                        twins[edgeRecords[k]] = -1;
                if (end - i == 2)
                {
                    int one = edgeRecords[i], other = edgeRecords[i + 1];
                    if (one < slotCount && other < slotCount)
                    {
                        RecordEnds(ref triangles, first, slotCount, ref edges, other, out int otherFrom, out _);
                        if (otherFrom != from)
                        {
                            twins[one]   = other;
                            twins[other] = one;
                        }
                    }
                }
                i = end;
            }
        }

        // Stable counting sort of the edge records by their larger or smaller vertex.
        void CountingSort(ref ExactList<int> source, ref ExactList<int> target, int vertexCount, ref ExactList<int> triangles,
                          int first, int slotCount, ref ExactList<ExactOutputEdge> edges, bool byLarger)
        {
            vertexCounts.Clear();
            vertexCounts.Resize(vertexCount + 1);
            for (int i = 0; i < source.Length; i++)
            {
                RecordEnds(ref triangles, first, slotCount, ref edges, source[i], out int from, out int to);
                vertexCounts[(byLarger ? Math.Max(from, to) : Math.Min(from, to)) + 1]++;
            }
            for (int v = 0; v < vertexCount; v++)
                vertexCounts[v + 1] += vertexCounts[v];
            for (int i = 0; i < source.Length; i++)
            {
                RecordEnds(ref triangles, first, slotCount, ref edges, source[i], out int from, out int to);
                int key = byLarger ? Math.Max(from, to) : Math.Min(from, to);
                target[vertexCounts[key]] = source[i];
                vertexCounts[key]++;
            }
        }

        void TryFlip(ref ExactList<ExactOutputVertex> vertices, ref ExactList<int> triangles, int first, int slot)
        {
            int twin = twins[slot];
            int t = slot / 3, k = slot - t * 3;
            int s = twin / 3, j = twin - s * 3;
            int a = triangles[first + slot];
            int b = triangles[first + t * 3 + NextCorner(k)];
            int c = triangles[first + t * 3 + PreviousCorner(k)];
            int d = triangles[first + s * 3 + PreviousCorner(j)];
            ref var va = ref vertices[a]; ref var vb = ref vertices[b];
            ref var vc = ref vertices[c]; ref var vd = ref vertices[d];
            if (!FloatInCircle(va, vb, vc, vd))
                return;
            if (!FloatTurnsLeft(va, vd, vc) || !FloatTurnsLeft(vd, vb, vc))
                return;
            if (Orientation(va, vd, vc) <= 0 || Orientation(vd, vb, vc) <= 0)
                return;

            int outerBC = twins[t * 3 + NextCorner(k)], outerCA = twins[t * 3 + PreviousCorner(k)];
            int outerAD = twins[s * 3 + NextCorner(j)], outerDB = twins[s * 3 + PreviousCorner(j)];
            // (c, a, d) and (d, b, c), each with its new edge d - c last
            triangles[first + t * 3] = c; triangles[first + t * 3 + 1] = a; triangles[first + t * 3 + 2] = d;
            triangles[first + s * 3] = d; triangles[first + s * 3 + 1] = b; triangles[first + s * 3 + 2] = c;
            twins[t * 3] = outerCA; twins[t * 3 + 1] = outerAD; twins[t * 3 + 2] = s * 3 + 2;
            twins[s * 3] = outerDB; twins[s * 3 + 1] = outerBC; twins[s * 3 + 2] = t * 3 + 2;
            if (outerCA >= 0) { twins[outerCA] = t * 3;     flipStack.Add(t * 3); }
            if (outerAD >= 0) { twins[outerAD] = t * 3 + 1; flipStack.Add(t * 3 + 1); }
            if (outerDB >= 0) { twins[outerDB] = s * 3;     flipStack.Add(s * 3); }
            if (outerBC >= 0) { twins[outerBC] = s * 3 + 1; flipStack.Add(s * 3 + 1); }
        }

        // The float positions in the plane's projection, where counter-clockwise is counter-clockwise seen from its normal.
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        double FloatU(in ExactOutputVertex v) => axisU == 0 ? v.x : (axisU == 1 ? v.y : v.z);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        double FloatV(in ExactOutputVertex v) => axisV == 0 ? v.x : (axisV == 1 ? v.y : v.z);

        const double kOrientErrorBound   = (3.0 + 16.0 * kUnitRoundoff) * kUnitRoundoff;
        const double kInCircleErrorBound = (10.0 + 96.0 * kUnitRoundoff) * kUnitRoundoff;

        // Whether (a, b, c) certainly turns left in the float positions.
        bool FloatTurnsLeft(in ExactOutputVertex a, in ExactOutputVertex b, in ExactOutputVertex c)
        {
            double acu = FloatU(a) - FloatU(c), bcu = FloatU(b) - FloatU(c);
            double acv = FloatV(a) - FloatV(c), bcv = FloatV(b) - FloatV(c);
            double left = acu * bcv, right = acv * bcu;
            return left - right > kOrientErrorBound * (Math.Abs(left) + Math.Abs(right));
        }

        // Whether d certainly lies inside the circle through a, b, c (counter-clockwise) in the float positions.
        bool FloatInCircle(in ExactOutputVertex a, in ExactOutputVertex b, in ExactOutputVertex c, in ExactOutputVertex d)
        {
            double du = FloatU(d), dv = FloatV(d);
            double adu = FloatU(a) - du, adv = FloatV(a) - dv;
            double bdu = FloatU(b) - du, bdv = FloatV(b) - dv;
            double cdu = FloatU(c) - du, cdv = FloatV(c) - dv;
            double bducdv = bdu * cdv, cdubdv = cdu * bdv, aLift = adu * adu + adv * adv;
            double cduadv = cdu * adv, aducdv = adu * cdv, bLift = bdu * bdu + bdv * bdv;
            double adubdv = adu * bdv, bduadv = bdu * adv, cLift = cdu * cdu + cdv * cdv;
            double determinant = aLift * (bducdv - cdubdv) + bLift * (cduadv - aducdv) + cLift * (adubdv - bduadv);
            double permanent   = (Math.Abs(bducdv) + Math.Abs(cdubdv)) * aLift
                               + (Math.Abs(cduadv) + Math.Abs(aducdv)) * bLift
                               + (Math.Abs(adubdv) + Math.Abs(bduadv)) * cLift;
            return determinant > kInCircleErrorBound * permanent;
        }

        // Conservative approximate test: false only when p lies outside the box of the triangle (a, b, c), exactly (see
        // FilterSlack; a point inside the box is no larger than the box's largest coordinate).
        bool PointMayBeInTriangle(int a, int b, int c, int p)
        {
            double pu = nodeBounds[p * 2], pv = nodeBounds[p * 2 + 1];
            double minU = Math.Min(nodeBounds[a * 2], Math.Min(nodeBounds[b * 2], nodeBounds[c * 2]));
            double maxU = Math.Max(nodeBounds[a * 2], Math.Max(nodeBounds[b * 2], nodeBounds[c * 2]));
            double minV = Math.Min(nodeBounds[a * 2 + 1], Math.Min(nodeBounds[b * 2 + 1], nodeBounds[c * 2 + 1]));
            double maxV = Math.Max(nodeBounds[a * 2 + 1], Math.Max(nodeBounds[b * 2 + 1], nodeBounds[c * 2 + 1]));
            double scale = Math.Max(Math.Max(Math.Abs(minU), Math.Abs(maxU)), Math.Max(Math.Abs(minV), Math.Abs(maxV)));
            double slack = FilterSlack(scale);
            return pu >= minU - slack && pu <= maxU + slack && pv >= minV - slack && pv <= maxV + slack;
        }
    }
}
