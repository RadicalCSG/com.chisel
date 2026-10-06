using Unity.Collections;
using Unity.Mathematics;

namespace Chisel.Core
{
    internal static class OutputWeld
    {
        public static bool SamePosition(float3 a, float3 b)
        {
            return a.x == b.x && a.y == b.y && a.z == b.z;
        }

        // Two corners at one float position: no area, under any transform
        public static bool HasTwoCornersAtOnePosition(float3 a, float3 b, float3 c)
        {
            return SamePosition(a, b) || SamePosition(b, c) || SamePosition(c, a);
        }

        public static int WeldSurface(NativeList<int>          indices,
                                      NativeList<float3>       colliderVertices,
                                      NativeList<SelectVertex> selectVertices,
                                      NativeList<RenderVertex> renderVertices)
        {
            return WeldSurface(indices, colliderVertices, selectVertices, renderVertices, out _);
        }

        // The same, and how many needles it flipped away
        public static int WeldSurface(NativeList<int>          indices,
                                      NativeList<float3>       colliderVertices,
                                      NativeList<SelectVertex> selectVertices,
                                      NativeList<RenderVertex> renderVertices,
                                      out int                  flipped)
        {
            flipped = 0;
            var vertexCount = colliderVertices.Length;
            if (selectVertices.Length != vertexCount || renderVertices.Length != vertexCount ||
                indices.Length % 3 != 0)
                return 0;
            for (int i = 0; i < indices.Length; i++)
            {
                if ((uint)indices[i] >= (uint)vertexCount)
                    return 0;
            }
            if (vertexCount == 0)
                return 0;

            // 1. One vertex per record that is the same in every stream, bit for bit
            NativeArray<int> remap;
            using var _remap = remap = new NativeArray<int>(vertexCount, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
            using var byHash = new NativeParallelMultiHashMap<uint, int>(vertexCount, Allocator.Temp);
            var merged = false;
            for (int v = 0; v < vertexCount; v++)
            {
                var hash = HashOf(colliderVertices[v], selectVertices[v], renderVertices[v]);
                remap[v] = v;
                if (byHash.TryGetFirstValue(hash, out var earlier, out var iterator))
                {
                    do
                    {
                        if (SameBits(colliderVertices[earlier], colliderVertices[v]) &&
                            SameBits(selectVertices[earlier],   selectVertices[v]) &&
                            SameBits(renderVertices[earlier],   renderVertices[v]))
                        {
                            remap[v] = earlier;
                            merged = true;
                            break;
                        }
                    } while (byHash.TryGetNextValue(out earlier, ref iterator));
                }
                if (remap[v] == v)
                    byHash.Add(hash, v);
            }

            // 2. No triangle with two corners at one position
            var write = 0;
            var dropped = 0;
            for (int t = 0; t < indices.Length; t += 3)
            {
                int a = remap[indices[t]], b = remap[indices[t + 1]], c = remap[indices[t + 2]];
                if (HasTwoCornersAtOnePosition(colliderVertices[a], colliderVertices[b], colliderVertices[c]))
                {
                    dropped++;
                    continue;
                }
                indices[write]     = a;
                indices[write + 1] = b;
                indices[write + 2] = c;
                write += 3;
            }
            indices.ResizeUninitialized(write);

            // 3. Needles flipped away with the neighbour across their long edge, in place: the triangle count stays
            flipped = FlipNeedles(indices, colliderVertices);

            // 4. Only the vertices a triangle uses, in their order
            NativeArray<int> newIndex;
            using var _newIndex = newIndex = new NativeArray<int>(vertexCount, Allocator.Temp, NativeArrayOptions.ClearMemory);
            for (int i = 0; i < indices.Length; i++)
                newIndex[indices[i]] = 1;
            var kept = 0;
            for (int v = 0; v < vertexCount; v++)
            {
                if (newIndex[v] == 0)
                {
                    newIndex[v] = -1;
                    continue;
                }
                newIndex[v] = kept;
                if (kept != v)
                {
                    colliderVertices[kept] = colliderVertices[v];
                    selectVertices[kept]   = selectVertices[v];
                    renderVertices[kept]   = renderVertices[v];
                }
                kept++;
            }
            if (!merged && dropped == 0 && kept == vertexCount)
                return 0;
            colliderVertices.ResizeUninitialized(kept);
            selectVertices.ResizeUninitialized(kept);
            renderVertices.ResizeUninitialized(kept);
            for (int i = 0; i < indices.Length; i++)
                indices[i] = newIndex[indices[i]];
            return dropped;
        }

        static int FlipNeedles(NativeList<int> indices, NativeList<float3> positions)
        {
            var triangleCount = indices.Length / 3;
            if (triangleCount < 2)
                return 0;
            NativeParallelHashMap<long, int> owner;
            using var _owner = owner = new NativeParallelHashMap<long, int>(indices.Length, Allocator.Temp);
            for (int t = 0; t < triangleCount; t++)
            {
                for (int e = 0; e < 3; e++)
                    owner.TryAdd(EdgeKey(indices[t * 3 + e], indices[t * 3 + (e + 1) % 3]), t);
            }
            var flipped = 0;
            for (int t = 0; t < triangleCount; t++)
            {
                int i0 = indices[t * 3], i1 = indices[t * 3 + 1], i2 = indices[t * 3 + 2];
                float3 p0 = positions[i0], p1 = positions[i1], p2 = positions[i2];
                if (!OnOneLine(p0, p1, p2))
                    continue;
                // the corner strictly between the other two, and the needle read from it: u, m, w; its long edge runs w -> u
                int u, m, w;
                if      (StrictlyBetween(p2, p0, p1)) { u = i2; m = i0; w = i1; }
                else if (StrictlyBetween(p0, p1, p2)) { u = i0; m = i1; w = i2; }
                else if (StrictlyBetween(p1, p2, p0)) { u = i1; m = i2; w = i0; }
                else continue;
                // the neighbour runs u -> w; read it from u: u, w, d
                if (!owner.TryGetValue(EdgeKey(u, w), out var n) || n == t)
                    continue;
                int d;
                if      (indices[n * 3] == u && indices[n * 3 + 1] == w) d = indices[n * 3 + 2];
                else if (indices[n * 3 + 1] == u && indices[n * 3 + 2] == w) d = indices[n * 3];
                else if (indices[n * 3 + 2] == u && indices[n * 3] == w) d = indices[n * 3 + 1];
                else continue;
                if (OnOneLine(positions[u], positions[w], positions[d]))
                    continue;
                Unown(owner, t, u, m, w);
                Unown(owner, n, u, w, d);
                indices[t * 3] = u; indices[t * 3 + 1] = m; indices[t * 3 + 2] = d;
                indices[n * 3] = m; indices[n * 3 + 1] = w; indices[n * 3 + 2] = d;
                Own(owner, t, u, m, d);
                Own(owner, n, m, w, d);
                flipped++;
            }
            return flipped;
        }

        static long EdgeKey(int from, int to) => ((long)from << 32) | (uint)to;

        static void Unown(NativeParallelHashMap<long, int> owner, int triangle, int a, int b, int c)
        {
            Unown(owner, triangle, EdgeKey(a, b));
            Unown(owner, triangle, EdgeKey(b, c));
            Unown(owner, triangle, EdgeKey(c, a));
        }

        static void Unown(NativeParallelHashMap<long, int> owner, int triangle, long key)
        {
            if (owner.TryGetValue(key, out var current) && current == triangle)
                owner.Remove(key);
        }

        static void Own(NativeParallelHashMap<long, int> owner, int triangle, int a, int b, int c)
        {
            owner.TryAdd(EdgeKey(a, b), triangle);
            owner.TryAdd(EdgeKey(b, c), triangle);
            owner.TryAdd(EdgeKey(c, a), triangle);
        }

        // The needles of a surface as it is stored (ChiselSurfaceRenderBuffer.needles): every triangle of three distinct float
        // positions exactly on one line, by its index (triangle t is indices[3t..3t+2])
        public static void FindNeedles(NativeList<int> indices, NativeList<float3> positions, NativeList<int> needles)
        {
            needles.Clear();
            for (int t = 0; t * 3 + 2 < indices.Length; t++)
            {
                float3 p0 = positions[indices[t * 3]], p1 = positions[indices[t * 3 + 1]], p2 = positions[indices[t * 3 + 2]];
                if (HasTwoCornersAtOnePosition(p0, p1, p2) || !OnOneLine(p0, p1, p2))
                    continue;
                needles.Add(t);
            }
        }

        public static bool StrictlyBetween(float3 a, float3 m, float3 b)
        {
            if (a.x != b.x) return (a.x < m.x && m.x < b.x) || (b.x < m.x && m.x < a.x);
            if (a.y != b.y) return (a.y < m.y && m.y < b.y) || (b.y < m.y && m.y < a.y);
            if (a.z != b.z) return (a.z < m.z && m.z < b.z) || (b.z < m.z && m.z < a.z);
            return false;
        }

        public static bool OnOneLine(float3 a, float3 b, float3 c)
        {
            var mantissa = new FixedList128Bytes<long>();
            var exponent = new FixedList64Bytes<int>();
            int lowest = int.MaxValue, highest = int.MinValue;
            for (int k = 0; k < 9; k++)
            {
                var value = k < 3 ? a[k] : (k < 6 ? b[k - 3] : c[k - 6]);
                var bits  = math.asuint(value);
                var field = (int)((bits >> 23) & 0xFF);
                if (field == 0xFF)
                    return false;                                       // NaN or infinity
                long whole = field == 0 ? (bits & 0x7FFFFF) : ((bits & 0x7FFFFF) | 0x800000);
                mantissa.Add((bits & 0x80000000u) != 0 ? -whole : whole);
                exponent.Add(field == 0 ? -149 : field - 150);
                if (whole == 0)
                    continue;
                lowest  = math.min(lowest, exponent[k]);
                highest = math.max(highest, exponent[k]);
            }
            if (lowest == int.MaxValue)
                return true;                                            // all three at the origin
            var span = highest - lowest;
            if (span <= 38)
            {
                // |value| < 2^24 * 2^38 = 2^62, so the differences fit a long and their products Int128
                var v = new FixedList128Bytes<long>();
                for (int k = 0; k < 9; k++)
                    v.Add(mantissa[k] == 0 ? 0 : mantissa[k] << (exponent[k] - lowest));
                long ux = v[3] - v[0], uy = v[4] - v[1], uz = v[5] - v[2];
                long wx = v[6] - v[0], wy = v[7] - v[1], wz = v[8] - v[2];
                return Int128.DifferenceOfProducts(uy, wz, uz, wy).IsZero &&
                       Int128.DifferenceOfProducts(uz, wx, ux, wz).IsZero &&
                       Int128.DifferenceOfProducts(ux, wy, uy, wx).IsZero;
            }
            if (span > 228)
                return false;                                           // wider than the 512 bit products allow: undecided
            // |value| < 2^252, differences < 2^253, products < 2^506, their difference < 2^507: all within 512 bits
            var a0 = Scaled(mantissa[0], exponent[0], lowest); var a1 = Scaled(mantissa[1], exponent[1], lowest); var a2 = Scaled(mantissa[2], exponent[2], lowest);
            var b0 = Scaled(mantissa[3], exponent[3], lowest); var b1 = Scaled(mantissa[4], exponent[4], lowest); var b2 = Scaled(mantissa[5], exponent[5], lowest);
            var c0 = Scaled(mantissa[6], exponent[6], lowest); var c1 = Scaled(mantissa[7], exponent[7], lowest); var c2 = Scaled(mantissa[8], exponent[8], lowest);
            var bx = BigInt.Sub(b0, a0); var by = BigInt.Sub(b1, a1); var bz = BigInt.Sub(b2, a2);
            var cx = BigInt.Sub(c0, a0); var cy = BigInt.Sub(c1, a1); var cz = BigInt.Sub(c2, a2);
            return BigInt.Sub(BigInt.Mul(by, cz), BigInt.Mul(bz, cy)).Sign() == 0 &&
                   BigInt.Sub(BigInt.Mul(bz, cx), BigInt.Mul(bx, cz)).Sign() == 0 &&
                   BigInt.Sub(BigInt.Mul(bx, cy), BigInt.Mul(by, cx)).Sign() == 0;
        }

        static BigInt Scaled(long mantissa, int exponent, int lowest)
        {
            return mantissa == 0 ? BigInt.FromLong(0) : BigInt.ShiftLeft(BigInt.FromLong(mantissa), exponent - lowest);
        }

        static uint HashOf(float3 collider, SelectVertex select, RenderVertex render)
        {
            var hash = math.hash(new uint4(math.hash(collider), math.hash(select.position), math.hash((float4)select.entityID), math.hash(render.position)));
            return math.hash(new uint4(hash, math.hash(render.normal), math.hash(render.tangent), math.hash(new float4(render.uv0, render.uv1))));
        }

        static bool SameBits(float3 a, float3 b) => math.all(math.asuint(a) == math.asuint(b));
        static bool SameBits(float4 a, float4 b) => math.all(math.asuint(a) == math.asuint(b));
        static bool SameBits(float2 a, float2 b) => math.all(math.asuint(a) == math.asuint(b));

        static bool SameBits(SelectVertex a, SelectVertex b)
        {
            return SameBits(a.position, b.position) && SameBits((float4)a.entityID, (float4)b.entityID);
        }

        static bool SameBits(RenderVertex a, RenderVertex b)
        {
            return SameBits(a.position, b.position) && SameBits(a.normal, b.normal) && SameBits(a.tangent, b.tangent) &&
                   SameBits(a.uv0, b.uv0) && SameBits(a.uv1, b.uv1);
        }
    }
}
