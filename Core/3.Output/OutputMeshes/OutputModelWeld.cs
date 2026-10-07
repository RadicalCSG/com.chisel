using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Mathematics;

namespace Chisel.Core
{
    internal static class OutputModelWeld
    {
        // A surface this weld changes: the triangles of one dropped are -1
        struct Working
        {
            public int                      brush;
            public int                      surface;
            public UnsafeList<int>          indices;
            public UnsafeList<float3>       collider;
            public UnsafeList<SelectVertex> select;
            public UnsafeList<RenderVertex> render;
        }

        struct Needle
        {
            public int brush;
            public int surface;
            public int triangle;
        }

        public static void WeldModel(NativeList<BrushData> brushes, NativeList<BlobAssetReference<ChiselBrushRenderBuffer>> patched)
        {
            WeldModel(brushes, patched, out _, out _, out _);
        }

        public static void WeldModel(NativeList<BrushData> brushes, NativeList<BlobAssetReference<ChiselBrushRenderBuffer>> patched,
                                     out int split, out int paired, out int cancelled)
        {
            split = 0;
            paired = 0;
            cancelled = 0;
            var needles = new NativeList<Needle>(Allocator.Temp);
            for (int b = 0; b < brushes.Length; b++)
            {
                if (!brushes[b].brushRenderBuffer.IsCreated)
                    continue;
                ref var surfaces = ref brushes[b].brushRenderBuffer.Value.surfaces;
                for (int s = 0; s < surfaces.Length; s++)
                {
                    ref var list = ref surfaces[s].needles;
                    for (int n = 0; n < list.Length; n++)
                        needles.Add(new Needle { brush = b, surface = s, triangle = list[n] });
                }
            }

            // What each brush's output spans, to find the brushes a long edge can lie on
            var bounds = new NativeArray<MinMaxAABB>(brushes.Length, Allocator.Temp);
            for (int b = 0; b < brushes.Length; b++)
            {
                var box = new MinMaxAABB { Min = new float3(float.PositiveInfinity), Max = new float3(float.NegativeInfinity) };
                if (brushes[b].brushRenderBuffer.IsCreated)
                {
                    ref var surfaces = ref brushes[b].brushRenderBuffer.Value.surfaces;
                    for (int s = 0; s < surfaces.Length; s++)
                    {
                        if (surfaces[s].indexCount == 0)
                            continue;
                        box.Min = math.min(box.Min, surfaces[s].aabb.Min);
                        box.Max = math.max(box.Max, surfaces[s].aabb.Max);
                    }
                }
                bounds[b] = box;
            }

            var workIndex = new NativeParallelHashMap<long, int>(needles.Length * 2 + 16, Allocator.Temp);
            var work      = new NativeList<Working>(Allocator.Temp);
            var opposites = new Opposites(TriangleCount(brushes), Allocator.Temp);
            var fresh     = new NativeList<int3>(Allocator.Temp);
            try
            {
                KeyAll(brushes, work, workIndex, ref opposites);
                for (bool changed = true, first = true; changed; first = false)
                {
                    // after the first turn only the triangles the splits made can make a new pair
                    var from = first ? 0 : opposites.found.Length;
                    KeyFresh(brushes, work, workIndex, fresh, ref opposites);
                    fresh.Clear();
                    changed  = CancelOpposites(brushes, work, workIndex, ref opposites, from, ref cancelled);
                    changed |= WeldNeedles(brushes, bounds, needles, work, workIndex, fresh, ref split, ref paired);
                }

                // The copies of the brushes it changed
                for (int k = 0; k < work.Length; k++)
                {
                    var brush = work[k].brush;
                    var first = true;
                    for (int j = 0; j < k; j++)
                    {
                        if (work[j].brush == brush) { first = false; break; }
                    }
                    if (!first)
                        continue;
                    var copy = Copy(brushes[brush].brushRenderBuffer, brush, work, workIndex);
                    patched.Add(copy);
                    var data = brushes[brush];
                    data.brushRenderBuffer = copy;
                    brushes[brush] = data;
                }
            }
            finally
            {
                for (int k = 0; k < work.Length; k++)
                {
                    var w = work[k];
                    w.indices.Dispose();
                    w.collider.Dispose();
                    w.select.Dispose();
                    w.render.Dispose();
                }
                work.Dispose();
                workIndex.Dispose();
                opposites.Dispose();
                fresh.Dispose();
                bounds.Dispose();
                needles.Dispose();
            }
        }

        // One pass over the needles; true when it dropped one
        static bool WeldNeedles(NativeList<BrushData> brushes, NativeArray<MinMaxAABB> bounds, NativeList<Needle> needles,
                                NativeList<Working> work, NativeParallelHashMap<long, int> workIndex, NativeList<int3> fresh,
                                ref int split, ref int paired)
        {
            var changed = false;
            for (int i = 0; i < needles.Length; i++)
            {
                var needle = needles[i];
                // its corners as they are now: one an earlier step dropped is gone
                if (!Corners(brushes, work, workIndex, needle.brush, needle.surface, needle.triangle, out var i0, out var i1, out var i2))
                    continue;
                var p0 = Position(brushes, work, workIndex, needle.brush, needle.surface, i0);
                var p1 = Position(brushes, work, workIndex, needle.brush, needle.surface, i1);
                var p2 = Position(brushes, work, workIndex, needle.brush, needle.surface, i2);
                // the corner strictly between the other two, and the needle read from it: u, m, w; its long edge runs w -> u
                float3 pu, pm, pw;
                if      (OutputWeld.StrictlyBetween(p2, p0, p1)) { pu = p2; pm = p0; pw = p1; }
                else if (OutputWeld.StrictlyBetween(p0, p1, p2)) { pu = p0; pm = p1; pw = p2; }
                else if (OutputWeld.StrictlyBetween(p1, p2, p0)) { pu = p1; pm = p2; pw = p0; }
                else continue;

                // the triangle across it runs u -> w
                if (!FindAcross(brushes, bounds, work, workIndex, needle, pu, pw,
                                out var acrossBrush, out var acrossSurface, out var acrossTriangle, out var au, out var aw, out var ad))
                    continue;
                var pd = Position(brushes, work, workIndex, acrossBrush, acrossSurface, ad);
                if (!OutputWeld.OnOneLine(pu, pw, pd))
                {
                    Drop(brushes, work, workIndex, needle.brush, needle.surface, needle.triangle);
                    Split(brushes, work, workIndex, acrossBrush, acrossSurface, acrossTriangle, au, aw, ad, pm);
                    fresh.Add(new int3(acrossBrush, acrossSurface, acrossTriangle));
                    fresh.Add(new int3(acrossBrush, acrossSurface, TriangleCount(brushes, work, workIndex, acrossBrush, acrossSurface) - 1));
                    split++;
                    changed = true;
                } else
                if (OutputWeld.SamePosition(pd, pm))
                {
                    Drop(brushes, work, workIndex, needle.brush, needle.surface, needle.triangle);
                    Drop(brushes, work, workIndex, acrossBrush, acrossSurface, acrossTriangle);
                    paired++;
                    changed = true;
                }
            }
            return changed;
        }

        struct TriangleKey : System.IEquatable<TriangleKey>
        {
            public int3 a, b, c;
            public bool Equals(TriangleKey other) => math.all(a == other.a) && math.all(b == other.b) && math.all(c == other.c);
            public override int GetHashCode() => (int)math.hash(new int3x3(a, b, c));
        }

        static int3 Bits(float3 p)
        {
            var bits = math.asint(p);
            return math.select(bits, int3.zero, bits == new int3(int.MinValue));
        }

        static bool Less(int3 p, int3 q) => p.x != q.x ? p.x < q.x : (p.y != q.y ? p.y < q.y : p.z < q.z);

        static TriangleKey KeyOf(int3 a, int3 b, int3 c)
        {
            if (Less(b, a) && Less(b, c)) return new TriangleKey { a = b, b = c, c = a };
            if (Less(c, a) && Less(c, b)) return new TriangleKey { a = c, b = a, c = b };
            return new TriangleKey { a = a, b = b, c = c };
        }

        // Every keyed triangle; a key goes stale when a split rewrites its triangle or it is dropped, so a match is checked
        struct Opposites
        {
            public NativeList<int3>        found;      // brush, surface, triangle
            public NativeList<TriangleKey> keys;
            public NativeList<bool>        gone;
            public NativeParallelMultiHashMap<TriangleKey, int> byKey;

            public Opposites(int capacity, Allocator allocator)
            {
                capacity = math.max(capacity, 16);
                found = new NativeList<int3>(capacity, allocator);
                keys  = new NativeList<TriangleKey>(capacity, allocator);
                gone  = new NativeList<bool>(capacity, allocator);
                byKey = new NativeParallelMultiHashMap<TriangleKey, int>(capacity, allocator);
            }

            public void Add(int3 triangle, float3 p0, float3 p1, float3 p2)
            {
                // NaN is never one position
                if (!math.all(math.isfinite(p0) & math.isfinite(p1) & math.isfinite(p2)))
                    return;
                var key = KeyOf(Bits(p0), Bits(p1), Bits(p2));
                byKey.Add(key, found.Length);
                found.Add(triangle);
                keys.Add(key);
                gone.Add(false);
            }

            public void Dispose()
            {
                found.Dispose();
                keys.Dispose();
                gone.Dispose();
                byKey.Dispose();
            }
        }

        static int TriangleCount(NativeList<BrushData> brushes)
        {
            var count = 0;
            for (int b = 0; b < brushes.Length; b++)
            {
                if (!brushes[b].brushRenderBuffer.IsCreated)
                    continue;
                ref var surfaces = ref brushes[b].brushRenderBuffer.Value.surfaces;
                for (int s = 0; s < surfaces.Length; s++)
                {
                    if (surfaces[s].decalEntityID == 0)
                        count += surfaces[s].indices.Length / 3;
                }
            }
            return count;
        }

        static void KeyAll(NativeList<BrushData> brushes, NativeList<Working> work, NativeParallelHashMap<long, int> workIndex, ref Opposites opposites)
        {
            for (int b = 0; b < brushes.Length; b++)
            {
                if (!brushes[b].brushRenderBuffer.IsCreated)
                    continue;
                ref var surfaces = ref brushes[b].brushRenderBuffer.Value.surfaces;
                for (int s = 0; s < surfaces.Length; s++)
                {
                    if (surfaces[s].decalEntityID != 0)
                        continue;
                    var w = Find(workIndex, b, s);
                    if (w >= 0)
                    {
                        var copy = work[w];
                        for (int t = 0; t + 2 < copy.indices.Length; t += 3)
                        {
                            if (copy.indices[t] < 0)
                                continue;
                            opposites.Add(new int3(b, s, t / 3), copy.collider[copy.indices[t]], copy.collider[copy.indices[t + 1]], copy.collider[copy.indices[t + 2]]);
                        }
                        continue;
                    }
                    ref var indices   = ref surfaces[s].indices;
                    ref var positions = ref surfaces[s].colliderVertices;
                    for (int t = 0; t + 2 < indices.Length; t += 3)
                        opposites.Add(new int3(b, s, t / 3), positions[indices[t]], positions[indices[t + 1]], positions[indices[t + 2]]);
                }
            }
        }

        static void KeyFresh(NativeList<BrushData> brushes, NativeList<Working> work, NativeParallelHashMap<long, int> workIndex,
                             NativeList<int3> fresh, ref Opposites opposites)
        {
            for (int f = 0; f < fresh.Length; f++)
            {
                var triangle = fresh[f];
                if (brushes[triangle.x].brushRenderBuffer.Value.surfaces[triangle.y].decalEntityID != 0)
                    continue;
                if (!Corners(brushes, work, workIndex, triangle.x, triangle.y, triangle.z, out var i0, out var i1, out var i2))
                    continue;
                opposites.Add(triangle, Position(brushes, work, workIndex, triangle.x, triangle.y, i0),
                                        Position(brushes, work, workIndex, triangle.x, triangle.y, i1),
                                        Position(brushes, work, workIndex, triangle.x, triangle.y, i2));
            }
        }

        // Whether a keyed triangle is still there with that key
        static bool Current(NativeList<BrushData> brushes, NativeList<Working> work, NativeParallelHashMap<long, int> workIndex, int3 triangle, TriangleKey key)
        {
            if (!Corners(brushes, work, workIndex, triangle.x, triangle.y, triangle.z, out var i0, out var i1, out var i2))
                return false;
            var p0 = Position(brushes, work, workIndex, triangle.x, triangle.y, i0);
            var p1 = Position(brushes, work, workIndex, triangle.x, triangle.y, i1);
            var p2 = Position(brushes, work, workIndex, triangle.x, triangle.y, i2);
            return KeyOf(Bits(p0), Bits(p1), Bits(p2)).Equals(key);
        }

        static bool CancelOpposites(NativeList<BrushData> brushes, NativeList<Working> work, NativeParallelHashMap<long, int> workIndex,
                                    ref Opposites opposites, int from, ref int cancelled)
        {
            var changed = false;
            for (int i = from; i < opposites.found.Length; i++)
            {
                if (opposites.gone[i])
                    continue;
                var key = opposites.keys[i];
                if (!opposites.byKey.TryGetFirstValue(KeyOf(key.a, key.c, key.b), out var j, out var iterator))
                    continue;
                var mine = opposites.found[i];
                if (!Current(brushes, work, workIndex, mine, key))
                {
                    opposites.gone[i] = true;
                    continue;
                }
                var flags = brushes[mine.x].brushRenderBuffer.Value.surfaces[mine.y].destinationFlags;
                do
                {
                    if (j == i || opposites.gone[j])
                        continue;
                    var other = opposites.found[j];
                    if (brushes[other.x].brushRenderBuffer.Value.surfaces[other.y].destinationFlags != flags)
                        continue;
                    if (!Current(brushes, work, workIndex, other, opposites.keys[j]))
                    {
                        opposites.gone[j] = true;
                        continue;
                    }
                    // two facing needles are WeldNeedles' to drop
                    if (OutputWeld.OnOneLine(math.asfloat(key.a), math.asfloat(key.b), math.asfloat(key.c)))
                        break;
                    Drop(brushes, work, workIndex, mine.x, mine.y, mine.z);
                    Drop(brushes, work, workIndex, other.x, other.y, other.z);
                    opposites.gone[i] = true;
                    opposites.gone[j] = true;
                    cancelled++;
                    changed = true;
                    break;
                } while (opposites.byKey.TryGetNextValue(out j, ref iterator));
            }
            return changed;
        }

        static long Key(int brush, int surface) => ((long)brush << 32) | (uint)surface;

        static int Find(NativeParallelHashMap<long, int> workIndex, int brush, int surface)
        {
            return workIndex.TryGetValue(Key(brush, surface), out var w) ? w : -1;
        }

        static int TriangleCount(NativeList<BrushData> brushes, NativeList<Working> work, NativeParallelHashMap<long, int> workIndex, int brush, int surface)
        {
            var w = Find(workIndex, brush, surface);
            if (w >= 0)
                return work[w].indices.Length / 3;
            return brushes[brush].brushRenderBuffer.Value.surfaces[surface].indices.Length / 3;
        }

        // The vertex indices of a triangle; false when it was dropped
        static bool Corners(NativeList<BrushData> brushes, NativeList<Working> work, NativeParallelHashMap<long, int> workIndex,
                            int brush, int surface, int triangle, out int i0, out int i1, out int i2)
        {
            var w = Find(workIndex, brush, surface);
            if (w >= 0)
            {
                var indices = work[w].indices;
                i0 = indices[triangle * 3]; i1 = indices[triangle * 3 + 1]; i2 = indices[triangle * 3 + 2];
            } else
            {
                ref var indices = ref brushes[brush].brushRenderBuffer.Value.surfaces[surface].indices;
                i0 = indices[triangle * 3]; i1 = indices[triangle * 3 + 1]; i2 = indices[triangle * 3 + 2];
            }
            return i0 >= 0;
        }

        static float3 Position(NativeList<BrushData> brushes, NativeList<Working> work, NativeParallelHashMap<long, int> workIndex,
                               int brush, int surface, int vertex)
        {
            var w = Find(workIndex, brush, surface);
            if (w >= 0)
                return work[w].collider[vertex];
            return brushes[brush].brushRenderBuffer.Value.surfaces[surface].colliderVertices[vertex];
        }

        static bool Contains(MinMaxAABB box, float3 p)
        {
            return math.all(p >= box.Min) && math.all(p <= box.Max);
        }

        // The first triangle, in brush, surface and triangle order, that runs u -> w; its corners at u and w and the third, d
        static bool FindAcross(NativeList<BrushData> brushes, NativeArray<MinMaxAABB> bounds, NativeList<Working> work,
                               NativeParallelHashMap<long, int> workIndex, Needle needle, float3 pu, float3 pw,
                               out int acrossBrush, out int acrossSurface, out int acrossTriangle, out int au, out int aw, out int ad)
        {
            for (int b = 0; b < brushes.Length; b++)
            {
                if (!Contains(bounds[b], pu) || !Contains(bounds[b], pw))
                    continue;
                ref var surfaces = ref brushes[b].brushRenderBuffer.Value.surfaces;
                for (int s = 0; s < surfaces.Length; s++)
                {
                    if (surfaces[s].indexCount == 0 || !Contains(surfaces[s].aabb, pu) || !Contains(surfaces[s].aabb, pw))
                        continue;
                    var count = TriangleCount(brushes, work, workIndex, b, s);
                    for (int t = 0; t < count; t++)
                    {
                        if (b == needle.brush && s == needle.surface && t == needle.triangle)
                            continue;
                        if (!Corners(brushes, work, workIndex, b, s, t, out var c0, out var c1, out var c2))
                            continue;
                        var q0 = Position(brushes, work, workIndex, b, s, c0);
                        var q1 = Position(brushes, work, workIndex, b, s, c1);
                        var q2 = Position(brushes, work, workIndex, b, s, c2);
                        int eu = -1, ew = -1, ed = -1;
                        if      (OutputWeld.SamePosition(q0, pu) && OutputWeld.SamePosition(q1, pw)) { eu = c0; ew = c1; ed = c2; }
                        else if (OutputWeld.SamePosition(q1, pu) && OutputWeld.SamePosition(q2, pw)) { eu = c1; ew = c2; ed = c0; }
                        else if (OutputWeld.SamePosition(q2, pu) && OutputWeld.SamePosition(q0, pw)) { eu = c2; ew = c0; ed = c1; }
                        if (eu < 0)
                            continue;
                        acrossBrush = b; acrossSurface = s; acrossTriangle = t;
                        au = eu; aw = ew; ad = ed;
                        return true;
                    }
                }
            }
            acrossBrush = acrossSurface = acrossTriangle = au = aw = ad = -1;
            return false;
        }

        // The working copy of a surface, made from its buffer the first time
        unsafe static int WorkOn(NativeList<BrushData> brushes, NativeList<Working> work, NativeParallelHashMap<long, int> workIndex, int brush, int surface)
        {
            var w = Find(workIndex, brush, surface);
            if (w >= 0)
                return w;
            ref var source = ref brushes[brush].brushRenderBuffer.Value.surfaces[surface];
            var copy = new Working
            {
                brush    = brush,
                surface  = surface,
                indices  = new UnsafeList<int>(source.indices.Length + 3, Allocator.Temp),
                collider = new UnsafeList<float3>(source.colliderVertices.Length + 1, Allocator.Temp),
                select   = new UnsafeList<SelectVertex>(source.selectVertices.Length + 1, Allocator.Temp),
                render   = new UnsafeList<RenderVertex>(source.renderVertices.Length + 1, Allocator.Temp),
            };
            copy.indices .AddRange(source.indices.GetUnsafePtr(),          source.indices.Length);
            copy.collider.AddRange(source.colliderVertices.GetUnsafePtr(), source.colliderVertices.Length);
            copy.select  .AddRange(source.selectVertices.GetUnsafePtr(),   source.selectVertices.Length);
            copy.render  .AddRange(source.renderVertices.GetUnsafePtr(),   source.renderVertices.Length);
            w = work.Length;
            work.Add(copy);
            workIndex.TryAdd(Key(brush, surface), w);
            return w;
        }

        static void Drop(NativeList<BrushData> brushes, NativeList<Working> work, NativeParallelHashMap<long, int> workIndex, int brush, int surface, int triangle)
        {
            var w = WorkOn(brushes, work, workIndex, brush, surface);
            ref var copy = ref work.ElementAt(w);
            copy.indices[triangle * 3]     = -1;
            copy.indices[triangle * 3 + 1] = -1;
            copy.indices[triangle * 3 + 2] = -1;
        }

        // u-w-d becomes u-m-d (in its place) and m-w-d (added), m a new corner of the surface at pm
        static void Split(NativeList<BrushData> brushes, NativeList<Working> work, NativeParallelHashMap<long, int> workIndex,
                          int brush, int surface, int triangle, int u, int w, int d, float3 pm)
        {
            var c = WorkOn(brushes, work, workIndex, brush, surface);
            ref var copy = ref work.ElementAt(c);
            var pu = copy.collider[u];
            var pw = copy.collider[w];
            var along = pw - pu;
            var t = math.dot(pm - pu, along) / math.dot(along, along);
            var ru = copy.render[u];
            var rw = copy.render[w];
            var m = copy.collider.Length;
            copy.collider.Add(pm);
            var select = copy.select[u];
            select.position = pm;
            copy.select.Add(select);
            copy.render.Add(new RenderVertex
            {
                position = pm,
                normal   = math.lerp(ru.normal,  rw.normal,  t),
                tangent  = math.lerp(ru.tangent, rw.tangent, t),
                uv0      = math.lerp(ru.uv0,     rw.uv0,     t),
                uv1      = math.lerp(ru.uv1,     rw.uv1,     t)
            });
            copy.indices[triangle * 3]     = u;
            copy.indices[triangle * 3 + 1] = m;
            copy.indices[triangle * 3 + 2] = d;
            copy.indices.Add(m);
            copy.indices.Add(w);
            copy.indices.Add(d);
        }

        // The brush's buffer with its changed surfaces stored again (OutputWeld's rules: only the triangles kept, only the
        // vertices they use, in their order) and the rest copied as they are
        static BlobAssetReference<ChiselBrushRenderBuffer> Copy(BlobAssetReference<ChiselBrushRenderBuffer> source, int brush,
                                                                NativeList<Working> work, NativeParallelHashMap<long, int> workIndex)
        {
            var builder = new BlobBuilder(Allocator.Temp);
            try
            {
                ref var original = ref source.Value;
                ref var root     = ref builder.ConstructRoot<ChiselBrushRenderBuffer>();
                var surfaces     = builder.Allocate(ref root.surfaces, original.surfaces.Length);
                for (int s = 0; s < original.surfaces.Length; s++)
                {
                    ref var from = ref original.surfaces[s];
                    ref var to   = ref surfaces[s];
                    to.surfaceIndex          = from.surfaceIndex;
                    to.baseSurfaceIndex      = from.baseSurfaceIndex;
                    to.decalEntityID         = from.decalEntityID;
                    to.destinationFlags      = from.destinationFlags;
                    to.destinationParameters = from.destinationParameters;
                    to.outputFlags           = from.outputFlags;
                    to.CopyHotspot(builder, ref from);
                    var w = Find(workIndex, brush, s);
                    if (w < 0)
                    {
                        to.CopyGeometry(builder, ref from);
                        continue;
                    }

                    var copy = work[w];
                    NativeArray<int> used;
                    using var _used    = used    = new NativeArray<int>(copy.collider.Length, Allocator.Temp, NativeArrayOptions.ClearMemory);
                    NativeList<int> indices;
                    using var _indices = indices = new NativeList<int>(copy.indices.Length, Allocator.Temp);
                    for (int i = 0; i < copy.indices.Length; i += 3)
                    {
                        if (copy.indices[i] < 0)
                            continue;
                        for (int k = 0; k < 3; k++)
                        {
                            indices.Add(copy.indices[i + k]);
                            used[copy.indices[i + k]] = 1;
                        }
                    }
                    using var collider = new NativeList<float3>(copy.collider.Length, Allocator.Temp);
                    using var select   = new NativeList<SelectVertex>(copy.collider.Length, Allocator.Temp);
                    using var render   = new NativeList<RenderVertex>(copy.collider.Length, Allocator.Temp);
                    for (int v = 0; v < copy.collider.Length; v++)
                    {
                        if (used[v] == 0)
                        {
                            used[v] = -1;
                            continue;
                        }
                        used[v] = collider.Length;
                        collider.Add(copy.collider[v]);
                        select.Add(copy.select[v]);
                        render.Add(copy.render[v]);
                    }
                    for (int i = 0; i < indices.Length; i++)
                        indices[i] = used[indices[i]];
                    to.Store(builder, indices, collider, select, render);
                }

                // The mesh queries' lists of the surfaces, with the changed ones' counts and hashes; a surface left with no
                // triangle is left out, as when the brush's buffer was made
                var queries = builder.Allocate(ref root.querySurfaces, original.querySurfaces.Length);
                using var list = new NativeList<ChiselQuerySurface>(Allocator.Temp);
                for (int q = 0; q < original.querySurfaces.Length; q++)
                {
                    ref var from = ref original.querySurfaces[q];
                    list.Clear();
                    for (int e = 0; e < from.surfaces.Length; e++)
                    {
                        var entry = from.surfaces[e];
                        ref var stored = ref surfaces[entry.surfaceIndex];
                        if (stored.vertexCount == 0 || stored.indexCount == 0)
                            continue;
                        entry.vertexCount       = stored.vertexCount;
                        entry.indexCount        = stored.indexCount;
                        entry.geometryHashValue = stored.geometryHashValue;
                        entry.surfaceHashValue  = stored.surfaceHashValue;
                        list.Add(entry);
                    }
                    queries[q].brushNodeID = from.brushNodeID;
                    builder.Construct(ref queries[q].surfaces, list);
                }
                root.surfaceOffset = original.surfaceOffset;
                root.surfaceCount  = original.surfaceCount;
                return builder.CreateBlobAssetReference<ChiselBrushRenderBuffer>(Allocator.Persistent);
            }
            finally
            {
                builder.Dispose();
            }
        }
    }
}
