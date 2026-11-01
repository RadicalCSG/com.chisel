using Unity.Collections;
using Unity.Mathematics;

namespace Chisel.Core
{
    /// <summary>The outline of a <see cref="ChiselDecalProjection.Polygon"/> decal: a polygon that doesn't cross itself, without holes.</summary>
    public static class ChiselDecalOutline
    {
        // Corners closer to a line than this turn neither way
        const double kTurnEpsilon = 1e-12;

        /// <summary>
        /// Splits an outline (either way round) into counter-clockwise triangles: three indices into it each. False when it
        /// can't, for an outline that crosses itself or has fewer than three corners.
        /// </summary>
        public static bool Triangulate(NativeArray<float2> outline, NativeList<int> triangles)
        {
            triangles.Clear();
            var count = outline.IsCreated ? outline.Length : 0;
            if (count < 3)
                return false;
            for (int i = 0; i < count; i++)
            {
                if (!math.all(math.isfinite(outline[i])))
                    return false;
            }

            var twiceArea = 0.0;
            for (int i = 0; i < count; i++)
            {
                var a = (double2)outline[i];
                var b = (double2)outline[(i + 1) % count];
                twiceArea += a.x * b.y - b.x * a.y;
            }
            if (!(math.abs(twiceArea) > kTurnEpsilon))
                return false;

            using var remaining = new NativeList<int>(count, Allocator.Temp);
            for (int i = 0; i < count; i++)
                remaining.Add(twiceArea > 0 ? i : count - 1 - i);

            while (remaining.Length > 3)
            {
                var clipped = false;
                for (int i = 0; i < remaining.Length; i++)
                {
                    var prev = remaining[(i + remaining.Length - 1) % remaining.Length];
                    var curr = remaining[i];
                    var next = remaining[(i + 1) % remaining.Length];
                    if (!IsEar(outline, remaining, prev, curr, next))
                        continue;
                    triangles.Add(prev);
                    triangles.Add(curr);
                    triangles.Add(next);
                    remaining.RemoveAt(i);
                    clipped = true;
                    break;
                }
                if (!clipped)
                {
                    triangles.Clear();
                    return false;
                }
            }
            if (Cross(outline[remaining[0]], outline[remaining[1]], outline[remaining[2]]) > kTurnEpsilon)
            {
                triangles.Add(remaining[0]);
                triangles.Add(remaining[1]);
                triangles.Add(remaining[2]);
            }
            return triangles.Length > 0;
        }

        /// <summary>
        /// Splits an outline into convex counter-clockwise parts of three or four corners (w is -1 for three): its
        /// triangles, joined where two make a convex part. False when it can't be triangulated.
        /// </summary>
        public static bool ConvexParts(NativeArray<float2> outline, NativeList<int4> parts)
        {
            parts.Clear();
            using var triangles = new NativeList<int>(math.max(3, outline.Length * 3), Allocator.Temp);
            if (!Triangulate(outline, triangles))
                return false;
            for (int t = 0; t + 2 < triangles.Length; t += 3)
                parts.Add(new int4(triangles[t], triangles[t + 1], triangles[t + 2], -1));

            var joined = true;
            while (joined)
            {
                joined = false;
                for (int i = 0; i < parts.Length && !joined; i++)
                {
                    for (int j = i + 1; j < parts.Length && !joined; j++)
                    {
                        if (!TryJoin(outline, parts[i], parts[j], out var part))
                            continue;
                        parts[i] = part;
                        parts.RemoveAtSwapBack(j);
                        joined = true;
                    }
                }
            }
            return true;
        }

        static int CornerCount(int4 part) => part.w < 0 ? 3 : 4;

        // Two triangles sharing an edge, as one convex part of four corners
        static bool TryJoin(NativeArray<float2> outline, int4 a, int4 b, out int4 joined)
        {
            joined = default;
            if (CornerCount(a) != 3 || CornerCount(b) != 3)
                return false;
            for (int i = 0; i < 3; i++)
            {
                var from = a[i];
                var to   = a[(i + 1) % 3];
                for (int k = 0; k < 3; k++)
                {
                    if (b[k] != to || b[(k + 1) % 3] != from)
                        continue;
                    // a from 'to' round to 'from', then b's corner opposite the edge
                    joined = new int4(to, a[(i + 2) % 3], from, b[(k + 2) % 3]);
                    for (int c = 0; c < 4; c++)
                    {
                        if (Cross(outline[joined[c]], outline[joined[(c + 1) % 4]], outline[joined[(c + 2) % 4]]) <= kTurnEpsilon)
                            return false;
                    }
                    return true;
                }
            }
            return false;
        }

        static double Cross(float2 a, float2 b, float2 c)
        {
            return ((double)b.x - a.x) * ((double)c.y - a.y) - ((double)b.y - a.y) * ((double)c.x - a.x);
        }

        // A corner that turns left, with no other corner inside or on its triangle
        static bool IsEar(NativeArray<float2> outline, NativeList<int> remaining, int prev, int curr, int next)
        {
            var a = outline[prev];
            var b = outline[curr];
            var c = outline[next];
            if (Cross(a, b, c) <= kTurnEpsilon)
                return false;
            for (int r = 0; r < remaining.Length; r++)
            {
                var index = remaining[r];
                if (index == prev || index == curr || index == next)
                    continue;
                var p = outline[index];
                if (math.all(p == a) || math.all(p == b) || math.all(p == c))
                    continue;
                if (Cross(a, b, p) >= 0 && Cross(b, c, p) >= 0 && Cross(c, a, p) >= 0)
                    return false;
            }
            return true;
        }
    }
}
