using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;

namespace Chisel.Core
{
    // The conforming interior: the surface is subdivided, its interior's
    // outline goes round the tile's inner rectangle (BuildConforming), and every vertex inside is the mean value weighted
    // average of its neighbours. With the outline convex and the weights positive the map is one to one (Floater), and
    // mean value weights reproduce an affine map exactly.
    public partial struct HotspotSections
    {
        // The interior's subdivision: until its longest edge is at most this part of the bounds' larger side, at most
        // kMaxSubdivisions times
        public const double kSubdivisionPart  = 0.125;
        public const int    kMaxSubdivisions  = 3;
        // The solve: over-relaxed Gauss-Seidel, until no uv moves more than this, at most this many sweeps
        const double kSolveRelaxation = 1.6;
        const double kSolveTolerance  = 1e-10;
        const int    kSolveSweeps     = 4000;

        public readonly bool Conforms => interiorCell >= 0 && conformLoop.IsCreated && conformLoop.Length >= 3;

        static double2 FrameOf(float3 p, double3 r, double3 u) => new double2(math.dot(r, (double3)p), math.dot(u, (double3)p));

        // The uv on the conforming interior's outline nearest p
        readonly float2 OutlineUV(double2 p)
        {
            var best = double.PositiveInfinity;
            var uv = double2.zero;
            for (int k = 0; k < conformLoop.Length; k++)
            {
                var a = conformLoop[k]; var b = conformLoop[(k + 1) % conformLoop.Length];
                var ab = b - a;
                var t = math.lengthsq(ab) > 0 ? math.saturate(math.dot(p - a, ab) / math.lengthsq(ab)) : 0;
                var distance = math.lengthsq(a + ab * t - p);
                if (distance < best)
                {
                    best = distance;
                    uv = math.lerp(conformImage[k], conformImage[(k + 1) % conformLoop.Length], t);
                }
            }
            return (float2)(uv / conformSize);
        }

        // How many times to subdivide for the interior's triangles
        public readonly int SubdivisionLevels(NativeList<int> indices, NativeList<float3> positions, float3 right, float3 up)
        {
            if (!Conforms)
                return 0;
            var r = (double3)right; var u = (double3)up;
            var min = new double2(double.PositiveInfinity); var max = new double2(double.NegativeInfinity);
            var longest = 0.0;
            for (int t = 0; t + 2 < indices.Length; t += 3)
            {
                var a = FrameOf(positions[indices[t]], r, u); var b = FrameOf(positions[indices[t + 1]], r, u); var c = FrameOf(positions[indices[t + 2]], r, u);
                min = math.min(min, math.min(a, math.min(b, c)));
                max = math.max(max, math.max(a, math.max(b, c)));
                if (Locate((a + b + c) / 3) != interiorCell)
                    continue;
                longest = math.max(longest, math.max(math.length(b - a), math.max(math.length(c - b), math.length(a - c))));
            }
            var target = math.cmax(max - min) * kSubdivisionPart;
            var levels = 0;
            while (longest > target && levels < kMaxSubdivisions)
            {
                longest *= 0.5;
                levels++;
            }
            return levels;
        }

        // Every triangle into four at its edges' middles, levels times; a middle is made once for both triangles of an edge
        public static void Subdivide(int levels, NativeList<int> indices, NativeList<float3> positions,
                                     NativeList<SelectVertex> selectVertices, NativeList<RenderVertex> renderVertices)
        {
            for (int level = 0; level < levels; level++)
            {
                var middles = new UnsafeHashMap<int2, int>(indices.Length, Allocator.Temp);
                var result  = new NativeList<int>(indices.Length * 4, Allocator.Temp);
                int Middle(int a, int b)
                {
                    var key = new int2(math.min(a, b), math.max(a, b));
                    if (middles.TryGetValue(key, out var m))
                        return m;
                    m = positions.Length;
                    var p = (float3)(((double3)positions[key.x] + (double3)positions[key.y]) * 0.5);
                    positions.Add(p);
                    var select = selectVertices[a];
                    select.position = p;
                    selectVertices.Add(select);
                    var ra = renderVertices[key.x]; var rb = renderVertices[key.y];
                    renderVertices.Add(new RenderVertex
                    {
                        position = p,
                        normal   = math.normalizesafe(ra.normal + rb.normal, ra.normal),
                        tangent  = (ra.tangent + rb.tangent) * 0.5f,
                        uv0      = (ra.uv0 + rb.uv0) * 0.5f,
                        uv1      = (ra.uv1 + rb.uv1) * 0.5f
                    });
                    middles.Add(key, m);
                    return m;
                }
                for (int t = 0; t + 2 < indices.Length; t += 3)
                {
                    var a = indices[t]; var b = indices[t + 1]; var c = indices[t + 2];
                    var ab = Middle(a, b); var bc = Middle(b, c); var ca = Middle(c, a);
                    result.Add(a);  result.Add(ab); result.Add(ca);
                    result.Add(ab); result.Add(b);  result.Add(bc);
                    result.Add(ca); result.Add(bc); result.Add(c);
                    result.Add(ab); result.Add(bc); result.Add(ca);
                }
                indices.Clear();
                indices.AddRange(result.AsArray());
                result.Dispose();
                middles.Dispose();
            }
        }

        // The uvs of the interior's vertices (NaN for the others): on its outline round the tile's inner rectangle, inside
        // each the mean value weighted average of its neighbours
        public readonly NativeList<float2> SolveInterior(NativeList<int> indices, NativeList<float3> positions, float3 right, float3 up, Allocator allocator)
        {
            var uvs = new NativeList<float2>(positions.Length, allocator);
            for (int v = 0; v < positions.Length; v++)
                uvs.Add(new float2(float.NaN));
            if (!Conforms)
                return uvs;
            var r = (double3)right; var u = (double3)up;
            var frame = new NativeArray<double2>(positions.Length, Allocator.Temp);
            for (int v = 0; v < positions.Length; v++)
                frame[v] = FrameOf(positions[v], r, u);

            // the interior's triangles, and its outline: the edges only one of them has
            var triangles = new NativeList<int>(indices.Length, Allocator.Temp);
            var edgeUse   = new UnsafeHashMap<int2, int>(indices.Length, Allocator.Temp);
            for (int t = 0; t + 2 < indices.Length; t += 3)
            {
                var a = indices[t]; var b = indices[t + 1]; var c = indices[t + 2];
                if (Locate((frame[a] + frame[b] + frame[c]) / 3) != interiorCell)
                    continue;
                triangles.Add(a); triangles.Add(b); triangles.Add(c);
                for (int k = 0; k < 3; k++)
                {
                    var p = triangles[triangles.Length - 3 + k]; var q = triangles[triangles.Length - 3 + (k + 1) % 3];
                    var key = new int2(math.min(p, q), math.max(p, q));
                    edgeUse.TryGetValue(key, out var used);
                    edgeUse.Remove(key);
                    edgeUse.Add(key, used + 1);
                }
            }
            var onOutline = new NativeArray<bool>(positions.Length, Allocator.Temp);
            using (var keys = edgeUse.GetKeyArray(Allocator.Temp))
            {
                for (int e = 0; e < keys.Length; e++)
                {
                    if (edgeUse[keys[e]] != 1)
                        continue;
                    onOutline[keys[e].x] = true;
                    onOutline[keys[e].y] = true;
                }
            }

            // the outline's uvs; inside, the bounds' map to start from
            var inside = new NativeArray<bool>(positions.Length, Allocator.Temp);
            for (int i = 0; i < triangles.Length; i++)
            {
                var v = triangles[i];
                if (onOutline[v])
                    uvs[v] = OutlineUV(frame[v]);
                else
                {
                    inside[v] = true;
                    uvs[v] = UV(interiorCell, frame[v]);
                }
            }

            // mean value weights: each angle at a vertex weighs both its edges, tan(angle / 2) / length
            var weights = new UnsafeHashMap<int2, double>(triangles.Length * 2, Allocator.Temp);
            void Add(int v, int other, double weight)
            {
                var key = new int2(v, other);
                weights.TryGetValue(key, out var sum);
                weights.Remove(key);
                weights.Add(key, sum + weight);
            }
            for (int t = 0; t + 2 < triangles.Length; t += 3)
            {
                for (int k = 0; k < 3; k++)
                {
                    var v = triangles[t + k];
                    if (!inside[v])
                        continue;
                    var p = triangles[t + (k + 1) % 3]; var q = triangles[t + (k + 2) % 3];
                    var e1 = frame[p] - frame[v]; var e2 = frame[q] - frame[v];
                    var l1 = math.length(e1); var l2 = math.length(e2);
                    if (!(l1 > 0) || !(l2 > 0))
                        continue;
                    var angle = math.acos(math.clamp(math.dot(e1, e2) / (l1 * l2), -1.0, 1.0));
                    var half = math.tan(angle * 0.5);
                    Add(v, p, half / l1);
                    Add(v, q, half / l2);
                }
            }
            // as lists per vertex
            using var pairs = weights.GetKeyValueArrays(Allocator.Temp);
            var order = new NativeArray<int>(pairs.Keys.Length, Allocator.Temp);
            for (int i = 0; i < order.Length; i++)
                order[i] = i;
            order.Sort(new ByVertex { keys = pairs.Keys });
            var vertices = new NativeList<int>(Allocator.Temp);   // per inside vertex: vertex, first, count
            var firsts   = new NativeList<int2>(Allocator.Temp);
            for (int i = 0; i < order.Length;)
            {
                var v = pairs.Keys[order[i]].x;
                var start = i;
                while (i < order.Length && pairs.Keys[order[i]].x == v)
                    i++;
                vertices.Add(v);
                firsts.Add(new int2(start, i - start));
            }

            // solved in double; the outline's stay as they are
            var work = new NativeArray<double2>(positions.Length, Allocator.Temp);
            for (int v = 0; v < positions.Length; v++)
                work[v] = (double2)uvs[v];
            for (int sweep = 0; sweep < kSolveSweeps; sweep++)
            {
                var moved = 0.0;
                for (int i = 0; i < vertices.Length; i++)
                {
                    var v = vertices[i];
                    var sum = double2.zero; var total = 0.0;
                    for (int n = firsts[i].x; n < firsts[i].x + firsts[i].y; n++)
                    {
                        var w = pairs.Values[order[n]];
                        sum   += work[pairs.Keys[order[n]].y] * w;
                        total += w;
                    }
                    if (!(total > 0))
                        continue;
                    var current = work[v];
                    var next = current + (sum / total - current) * kSolveRelaxation;
                    moved = math.max(moved, math.cmax(math.abs(next - current)));
                    work[v] = next;
                }
                if (moved < kSolveTolerance)
                    break;
            }
            for (int i = 0; i < vertices.Length; i++)
                uvs[vertices[i]] = (float2)work[vertices[i]];
            work.Dispose();

            vertices.Dispose();
            firsts.Dispose();
            order.Dispose();
            weights.Dispose();
            inside.Dispose();
            onOutline.Dispose();
            edgeUse.Dispose();
            triangles.Dispose();
            frame.Dispose();
            return uvs;
        }

        struct ByVertex : System.Collections.Generic.IComparer<int>
        {
            public NativeArray<int2> keys;
            public int Compare(int a, int b)
            {
                var p = keys[a]; var q = keys[b];
                if (p.x != q.x) return p.x < q.x ? -1 : 1;
                if (p.y != q.y) return p.y < q.y ? -1 : 1;
                return 0;
            }
        }
    }
}
