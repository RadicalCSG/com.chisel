using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;

namespace Chisel.Core
{
    // Flip-safe refinement of the conforming interior (low distortion
    // optimization): the mean value map (SolveInterior) is valid but crowds the texture near some corners. Each inside
    // vertex in turn moves down the symmetric Dirichlet energy of its triangles, measured against the tile's density, as
    // far as that lowers it without turning a triangle over; the outline stays where it is, so the map stays one to one.
    public partial struct HotspotSections
    {
        public const int kRefineSweeps    = 64;
        // stop once a sweep lowers the energy by less than this part of it
        const double     kRefineTolerance = 1e-7;
        const int        kRefineHalvings  = 16;

        // The interior's triangles (by vertex), and which of their vertices are inside it, not on its outline
        readonly void InteriorOf(NativeList<int> indices, NativeArray<double2> frame, NativeList<int> triangles, NativeArray<bool> inside)
        {
            var edgeUse = new UnsafeHashMap<int2, int>(indices.Length, Allocator.Temp);
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
            for (int i = 0; i < triangles.Length; i++)
                inside[triangles[i]] = true;
            using (var keys = edgeUse.GetKeyArray(Allocator.Temp))
            {
                for (int e = 0; e < keys.Length; e++)
                {
                    if (edgeUse[keys[e]] != 1)
                        continue;
                    inside[keys[e].x] = false;
                    inside[keys[e].y] = false;
                }
            }
            edgeUse.Dispose();
        }

        // Symmetric Dirichlet energy of a map, (|J|^2 + |J^-1|^2) / 2: 2 for a rotation, without bound as it flattens
        static double SymmetricDirichlet(double2x2 j, out double determinant)
        {
            determinant = math.determinant(j);
            if (determinant == 0)
                return double.PositiveInfinity;
            var frobenius = math.lengthsq(j.c0) + math.lengthsq(j.c1);
            // |J^-1|^2 = |J|^2 / det^2 for a 2x2 matrix
            return 0.5 * frobenius * (1 + 1 / (determinant * determinant));
        }

        // Moves the inside vertices of the conforming interior to lower its distortion; uvs as SolveInterior gives them,
        // unitsPerUV: the texture's size over the tile's pixels a unit (a uv in units at the tile's density)
        public readonly void RefineInterior(NativeList<int> indices, NativeList<float3> positions, float3 right, float3 up,
                                            NativeList<float2> uvs, double2 unitsPerUV, int sweeps = kRefineSweeps)
        {
            if (!Conforms || sweeps <= 0 || !(unitsPerUV.x > 0) || !(unitsPerUV.y > 0))
                return;
            var r = (double3)right; var u = (double3)up;
            var frame = new NativeArray<double2>(positions.Length, Allocator.Temp);
            for (int v = 0; v < positions.Length; v++)
                frame[v] = FrameOf(positions[v], r, u);
            var triangles = new NativeList<int>(indices.Length, Allocator.Temp);
            var inside    = new NativeArray<bool>(positions.Length, Allocator.Temp);
            InteriorOf(indices, frame, triangles, inside);
            var triangleCount = triangles.Length / 3;

            // in units at the tile's density
            var work = new NativeArray<double2>(positions.Length, Allocator.Temp);
            for (int v = 0; v < positions.Length; v++)
                work[v] = (double2)uvs[v] * unitsPerUV;

            // each triangle's corners in its frame, as an inverse edge matrix, and its area; the way round the uvs go
            var restInverse = new NativeArray<double2x2>(triangleCount, Allocator.Temp);
            var area        = new NativeArray<double>(triangleCount, Allocator.Temp);
            var orientation = 0.0;
            for (int t = 0; t < triangleCount; t++)
            {
                var a = triangles[t * 3]; var b = triangles[t * 3 + 1]; var c = triangles[t * 3 + 2];
                var rest = new double2x2(frame[b] - frame[a], frame[c] - frame[a]);
                var determinant = math.determinant(rest);
                area[t] = 0.5 * math.abs(determinant);
                restInverse[t] = determinant != 0 ? math.inverse(rest) : default;
                var j = math.mul(new double2x2(work[b] - work[a], work[c] - work[a]), restInverse[t]);
                orientation += area[t] * math.determinant(j);
            }
            var sign = orientation >= 0 ? 1.0 : -1.0;

            // the triangles round each vertex
            var firsts = new NativeArray<int>(positions.Length + 1, Allocator.Temp);
            for (int i = 0; i < triangles.Length; i++)
                firsts[triangles[i] + 1]++;
            for (int v = 0; v < positions.Length; v++)
                firsts[v + 1] += firsts[v];
            var around = new NativeArray<int>(triangles.Length, Allocator.Temp);
            var filled = new NativeArray<int>(positions.Length, Allocator.Temp);
            for (int i = 0; i < triangles.Length; i++)
            {
                var v = triangles[i];
                around[firsts[v] + filled[v]++] = i / 3;
            }

            double TriangleEnergy(int t, out double determinant)
            {
                var a = triangles[t * 3]; var b = triangles[t * 3 + 1]; var c = triangles[t * 3 + 2];
                var j = math.mul(new double2x2(work[b] - work[a], work[c] - work[a]), restInverse[t]);
                var energy = SymmetricDirichlet(j, out determinant);
                determinant *= sign;
                return area[t] * energy;
            }

            var total = 0.0;
            for (int t = 0; t < triangleCount; t++)
                total += TriangleEnergy(t, out _);
            for (int sweep = 0; sweep < sweeps && total < double.PositiveInfinity; sweep++)
            {
                var lowered = 0.0;
                for (int v = 0; v < positions.Length; v++)
                {
                    if (!inside[v] || firsts[v + 1] == firsts[v])
                        continue;
                    // the energy round it, its gradient, and a step scaled by how stiff it is
                    var before = 0.0;
                    var gradient = double2.zero;
                    var stiffness = 0.0;
                    var valid = true;
                    for (int n = firsts[v]; n < firsts[v + 1]; n++)
                    {
                        var t = around[n];
                        before += TriangleEnergy(t, out var determinant);
                        valid &= determinant > 0;
                        var a = triangles[t * 3]; var b = triangles[t * 3 + 1]; var c = triangles[t * 3 + 2];
                        var j = math.mul(new double2x2(work[b] - work[a], work[c] - work[a]), restInverse[t]);
                        var det = math.determinant(j);
                        var frobenius = math.lengthsq(j.c0) + math.lengthsq(j.c1);
                        // d/dJ of (|J|^2 (1 + 1/det^2)) / 2: J (1 + 1/det^2) - |J|^2 cof(J) / det^3
                        var cofactor = new double2x2(new double2(j.c1.y, -j.c1.x), new double2(-j.c0.y, j.c0.x));
                        var dj = j * (1 + 1 / (det * det)) - cofactor * (frobenius / (det * det * det));
                        var g = math.mul(dj, math.transpose(restInverse[t])) * area[t];
                        // the barycentric gradient of this corner
                        var lambda = math.transpose(restInverse[t]);
                        double2 corner;
                        if (v == b)      { gradient += g.c0; corner = lambda.c0; }
                        else if (v == c) { gradient += g.c1; corner = lambda.c1; }
                        else             { gradient -= g.c0 + g.c1; corner = -(lambda.c0 + lambda.c1); }
                        stiffness += area[t] * math.lengthsq(corner) * (1 + 3 / (det * det));
                    }
                    if (!valid || !(stiffness > 0) || !(math.lengthsq(gradient) > 0))
                        continue;
                    var start = work[v];
                    var step = -gradient / stiffness;
                    var accepted = false;
                    for (int halving = 0; halving < kRefineHalvings && !accepted; halving++, step *= 0.5)
                    {
                        work[v] = start + step;
                        var after = 0.0;
                        var flipped = false;
                        for (int n = firsts[v]; n < firsts[v + 1] && !flipped; n++)
                        {
                            after += TriangleEnergy(around[n], out var determinant);
                            flipped |= !(determinant > 0);
                        }
                        if (!flipped && after < before)
                        {
                            accepted = true;
                            lowered += before - after;
                        }
                    }
                    if (!accepted)
                        work[v] = start;
                }
                total -= lowered;
                if (lowered <= total * kRefineTolerance)
                    break;
            }

            for (int v = 0; v < positions.Length; v++)
            {
                if (inside[v])
                    uvs[v] = (float2)(work[v] / unitsPerUV);
            }
            filled.Dispose();
            around.Dispose();
            firsts.Dispose();
            area.Dispose();
            restInverse.Dispose();
            work.Dispose();
            inside.Dispose();
            triangles.Dispose();
            frame.Dispose();
        }
    }
}
