using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;

namespace Chisel.Core
{
    static class LoopEdgeSplitter
    {
        public static void SplitEdgesAtVertices(ref UnsafeList<Edge>       edges,
                                                in  NativeArray<float3>    vertexPositions,
                                                in  NativeArray<ushort>    candidateVertices,
                                                int                        candidateCount,
                                                float                      sqrEdgeEpsilon)
        {
            if (edges.Length < 1 || candidateCount < 1)
                return;

            var inputLength = edges.Length;
            var src = new NativeArray<Edge>(inputLength, Allocator.Temp);
            for (int i = 0; i < inputLength; i++)
                src[i] = edges[i];
            edges.Clear();

            // On-segment vertices for the current edge, sorted along it.
            var onEdge = new NativeList<ushort>(candidateCount, Allocator.Temp);

            for (int e = 0; e < inputLength; e++)
            {
                var vertexIndex0 = src[e].index1;
                var vertexIndex1 = src[e].index2;
                var vertex0 = vertexPositions[vertexIndex0];
                var vertex1 = vertexPositions[vertexIndex1];

                onEdge.Clear();

                var direction = vertex1 - vertex0;
                var length    = math.length(direction);
                if (length > math.EPSILON)
                {
                    var delta = direction / length;
                    for (int c = 0; c < candidateCount; c++)
                    {
                        var candidateIndex = candidateVertices[c];
                        if (candidateIndex == vertexIndex0 ||
                            candidateIndex == vertexIndex1)
                            continue;

                        var candidate = vertexPositions[candidateIndex];
                        var dot = math.dot(candidate - vertex0, delta);
                        if (dot <= 0 || dot >= length)
                            continue;
                        if (!MathExtensions.IsPointOnLineSegmentButNotOnVertex(candidate, vertex0, vertex1, sqrEdgeEpsilon))
                            continue;
                        onEdge.AddNoResize(candidateIndex);
                    }
                }

                if (onEdge.Length == 0)
                {
                    edges.Add(src[e]);
                    continue;
                }

                // Sort the inserted vertices by distance along the edge (small lists; same approach
                // as FindLoopVertexOverlaps). Equal-distance / duplicate indices collapse below.
                {
                    var delta = math.normalizesafe(direction);
                    for (int a = 0; a < onEdge.Length - 1; a++)
                    {
                        for (int b = a + 1; b < onEdge.Length; b++)
                        {
                            var da = math.dot(vertexPositions[onEdge[a]] - vertex0, delta);
                            var db = math.dot(vertexPositions[onEdge[b]] - vertex0, delta);
                            if (da > db)
                            {
                                var tmp = onEdge[a];
                                onEdge[a] = onEdge[b];
                                onEdge[b] = tmp;
                            }
                        }
                    }
                }

                if (vertexIndex0 != onEdge[0])
                    edges.Add(new Edge { index1 = vertexIndex0, index2 = onEdge[0] });
                for (int i = 1; i < onEdge.Length; i++)
                {
                    if (onEdge[i - 1] != onEdge[i])
                        edges.Add(new Edge { index1 = onEdge[i - 1], index2 = onEdge[i] });
                }
                if (onEdge[onEdge.Length - 1] != vertexIndex1)
                    edges.Add(new Edge { index1 = onEdge[onEdge.Length - 1], index2 = vertexIndex1 });
            }

            onEdge.Dispose();
            src.Dispose();
        }

        public static void RemoveAntiparallelEdgePairs(ref UnsafeList<Edge> edges)
        {
            var removed = true;
            while (removed)
            {
                removed = false;
                for (int a = 0; a < edges.Length && !removed; a++)
                {
                    for (int b = a + 1; b < edges.Length; b++)
                    {
                        if (edges[a].index1 == edges[b].index2 &&
                            edges[a].index2 == edges[b].index1)
                        {
                            // b > a, so removing b first leaves index a valid.
                            edges.RemoveAtSwapBack(b);
                            edges.RemoveAtSwapBack(a);
                            removed = true;
                            break;
                        }
                    }
                }
            }
        }

        public static void RemoveSimpleChords(ref UnsafeList<Edge> edges)
        {
            var removed = true;
            while (removed)
            {
                removed = false;
                for (int i = 0; i < edges.Length; i++)
                {
                    int a = edges[i].index1;
                    int b = edges[i].index2;
                    int outA = 0, inA = 0, outB = 0, inB = 0;
                    for (int k = 0; k < edges.Length; k++)
                    {
                        if (edges[k].index1 == a) outA++;
                        if (edges[k].index2 == a) inA++;
                        if (edges[k].index1 == b) outB++;
                        if (edges[k].index2 == b) inB++;
                    }
                    if (outA == 2 && inA == 1 && inB == 2 && outB == 1)
                    {
                        edges.RemoveAtSwapBack(i);
                        removed = true;
                        break;
                    }
                }
            }
        }

        public static bool RewindClosedLoop(ref UnsafeList<Edge> edges)
        {
            if (edges.Length < 3)
                return false;

            int maxIndex = 0;
            for (int e = 0; e < edges.Length; e++)
            {
                if (edges[e].index1 > maxIndex) maxIndex = edges[e].index1;
                if (edges[e].index2 > maxIndex) maxIndex = edges[e].index2;
            }
            int n = maxIndex + 1;

            var degree = new NativeArray<int>(n, Allocator.Temp);
            var parent = new NativeArray<int>(n, Allocator.Temp);
            for (int v = 0; v < n; v++) parent[v] = v;

            for (int e = 0; e < edges.Length; e++)
            {
                int a = edges[e].index1, b = edges[e].index2;
                if (a == b) { degree.Dispose(); parent.Dispose(); return false; }
                degree[a] = degree[a] + 1;
                degree[b] = degree[b] + 1;
                int ra = a; while (parent[ra] != ra) ra = parent[ra];
                int rb = b; while (parent[rb] != rb) rb = parent[rb];
                if (ra != rb) parent[ra] = rb;
            }

            // One component, every used vertex at degree 2, and as many vertices as edges: a cycle.
            int usedVertices = 0, root = -1, start = -1;
            bool ok = true;
            for (int v = 0; v < n && ok; v++)
            {
                int d = degree[v];
                if (d == 0) continue;
                usedVertices++;
                if (start == -1) start = v;
                int r = v; while (parent[r] != r) r = parent[r];
                if (root == -1) root = r;
                else if (r != root) ok = false;
                if (d != 2) ok = false;
            }
            if (!ok || usedVertices != edges.Length)
            {
                degree.Dispose();
                parent.Dispose();
                return false;
            }

            var consumed = new NativeArray<bool>(edges.Length, Allocator.Temp);
            var seq      = new NativeList<int>(edges.Length + 1, Allocator.Temp);
            int cur = start;
            seq.Add(cur);
            for (int step = 0; step < edges.Length; step++)
            {
                int nextV = -1, nextE = -1;
                for (int e = 0; e < edges.Length; e++)
                {
                    if (consumed[e]) continue;
                    if (edges[e].index1 == cur) { nextV = edges[e].index2; nextE = e; break; }
                    if (edges[e].index2 == cur) { nextV = edges[e].index1; nextE = e; break; }
                }
                if (nextE == -1) break;
                consumed[nextE] = true;
                seq.Add(nextV);
                cur = nextV;
            }

            bool allConsumed = true;
            for (int e = 0; e < edges.Length; e++) if (!consumed[e]) { allConsumed = false; break; }

            var rewound = false;
            if (allConsumed && cur == start && seq.Length == edges.Length + 1)
            {
                edges.Clear();
                for (int i = 0; i < seq.Length - 1; i++)
                    edges.Add(new Edge { index1 = (ushort)seq[i], index2 = (ushort)seq[i + 1] });
                rewound = true;
            }

            consumed.Dispose();
            seq.Dispose();
            degree.Dispose();
            parent.Dispose();
            return rewound;
        }

        public static bool RemoveTinyComponents(ref UnsafeList<Edge> edges)
        {
            if (edges.Length == 0)
                return false;

            int maxIndex = 0;
            for (int e = 0; e < edges.Length; e++)
            {
                if (edges[e].index1 > maxIndex) maxIndex = edges[e].index1;
                if (edges[e].index2 > maxIndex) maxIndex = edges[e].index2;
            }
            int n = maxIndex + 1;

            var parent = new NativeArray<int>(n, Allocator.Temp);
            for (int v = 0; v < n; v++) parent[v] = v;
            for (int e = 0; e < edges.Length; e++)
            {
                int ra = edges[e].index1; while (parent[ra] != ra) ra = parent[ra];
                int rb = edges[e].index2; while (parent[rb] != rb) rb = parent[rb];
                if (ra != rb) parent[ra] = rb;
            }

            var edgeCountPerRoot = new NativeArray<int>(n, Allocator.Temp);
            for (int e = 0; e < edges.Length; e++)
            {
                int r = edges[e].index1; while (parent[r] != r) r = parent[r];
                edgeCountPerRoot[r] = edgeCountPerRoot[r] + 1;
            }

            var removed = false;
            for (int e = edges.Length - 1; e >= 0; e--)
            {
                int r = edges[e].index1; while (parent[r] != r) r = parent[r];
                if (edgeCountPerRoot[r] < 3)
                {
                    edges.RemoveAtSwapBack(e);
                    removed = true;
                }
            }

            parent.Dispose();
            edgeCountPerRoot.Dispose();
            return removed;
        }

        public static bool CloseSingleOpenChain(ref UnsafeList<Edge> edges)
        {
            if (edges.Length < 2)
                return false;

            int maxIndex = 0;
            for (int e = 0; e < edges.Length; e++)
            {
                if (edges[e].index1 > maxIndex) maxIndex = edges[e].index1;
                if (edges[e].index2 > maxIndex) maxIndex = edges[e].index2;
            }
            int n = maxIndex + 1;

            var degree = new NativeArray<int>(n, Allocator.Temp);
            var parent = new NativeArray<int>(n, Allocator.Temp);
            for (int v = 0; v < n; v++) parent[v] = v;

            for (int e = 0; e < edges.Length; e++)
            {
                int a = edges[e].index1, b = edges[e].index2;
                if (a == b) { degree.Dispose(); parent.Dispose(); return false; }
                degree[a] = degree[a] + 1;
                degree[b] = degree[b] + 1;
                int ra = a; while (parent[ra] != ra) ra = parent[ra];
                int rb = b; while (parent[rb] != rb) rb = parent[rb];
                if (ra != rb) parent[ra] = rb;
            }

            // Single connected component, exactly two degree-1 ends, everything else degree-2.
            int ep1 = -1, ep2 = -1, root = -1;
            bool ok = true;
            for (int v = 0; v < n && ok; v++)
            {
                int d = degree[v];
                if (d == 0) continue;
                int r = v; while (parent[r] != r) r = parent[r];
                if (root == -1) root = r;
                else if (r != root) { ok = false; break; } // more than one component
                if (d == 1) { if (ep1 == -1) ep1 = v; else if (ep2 == -1) ep2 = v; else ok = false; }
                else if (d != 2) ok = false; // degree>2 (chord) -> not a clean open chain
            }

            var closed = false;
            if (ok && ep1 != -1 && ep2 != -1)
            {
                // Walk the (undirected) path from ep1 to ep2, consuming each edge once.
                var used = new NativeArray<bool>(edges.Length, Allocator.Temp);
                var seq  = new NativeList<int>(edges.Length + 1, Allocator.Temp);
                int cur  = ep1;
                seq.Add(cur);
                for (int step = 0; step < edges.Length; step++)
                {
                    int nextV = -1, nextE = -1;
                    for (int e = 0; e < edges.Length; e++)
                    {
                        if (used[e]) continue;
                        if (edges[e].index1 == cur) { nextV = edges[e].index2; nextE = e; break; }
                        if (edges[e].index2 == cur) { nextV = edges[e].index1; nextE = e; break; }
                    }
                    if (nextE == -1) break;
                    used[nextE] = true;
                    seq.Add(nextV);
                    cur = nextV;
                }

                bool allUsed = true;
                for (int e = 0; e < edges.Length; e++) if (!used[e]) { allUsed = false; break; }

                if (cur == ep2 && allUsed && seq.Length >= 3)
                {
                    edges.Clear();
                    for (int i = 0; i < seq.Length - 1; i++)
                        edges.Add(new Edge { index1 = (ushort)seq[i], index2 = (ushort)seq[i + 1] });
                    edges.Add(new Edge { index1 = (ushort)seq[seq.Length - 1], index2 = (ushort)seq[0] });
                    closed = true;
                }

                used.Dispose();
                seq.Dispose();
            }

            degree.Dispose();
            parent.Dispose();
            return closed;
        }
    }
}
