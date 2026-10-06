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
            try
            {
                for (var changed = true; changed; )
                {
                    changed  = CancelOpposites(brushes, work, workIndex, ref cancelled);
                    changed |= WeldNeedles(brushes, bounds, needles, work, workIndex, ref split, ref paired);
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
                bounds.Dispose();
                needles.Dispose();
            }
        }

        // One pass over the needles; true when it dropped one
        static bool WeldNeedles(NativeList<BrushData> brushes, NativeArray<MinMaxAABB> bounds, NativeList<Needle> needles,
                                NativeList<Working> work, NativeParallelHashMap<long, int> workIndex, ref int split, ref int paired)
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

        static bool CancelOpposites(NativeList<BrushData> brushes, NativeList<Working> work, NativeParallelHashMap<long, int> workIndex, ref int cancelled)
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
                        count += TriangleCount(brushes, work, workIndex, b, s);
                }
            }
            if (count < 2)
                return false;

            NativeList<int3> found;         // brush, surface, triangle
            using var _found = found = new NativeList<int3>(count, Allocator.Temp);
            NativeList<TriangleKey> keys;
            using var _keys = keys = new NativeList<TriangleKey>(count, Allocator.Temp);
            NativeParallelMultiHashMap<TriangleKey, int> byKey;
            using var _byKey = byKey = new NativeParallelMultiHashMap<TriangleKey, int>(count, Allocator.Temp);
            for (int b = 0; b < brushes.Length; b++)
            {
                if (!brushes[b].brushRenderBuffer.IsCreated)
                    continue;
                ref var surfaces = ref brushes[b].brushRenderBuffer.Value.surfaces;
                for (int s = 0; s < surfaces.Length; s++)
                {
                    if (surfaces[s].decalEntityID != 0)
                        continue;
                    var triangles = TriangleCount(brushes, work, workIndex, b, s);
                    for (int t = 0; t < triangles; t++)
                    {
                        if (!Corners(brushes, work, workIndex, b, s, t, out var i0, out var i1, out var i2))
                            continue;
                        var p0 = Position(brushes, work, workIndex, b, s, i0);
                        var p1 = Position(brushes, work, workIndex, b, s, i1);
                        var p2 = Position(brushes, work, workIndex, b, s, i2);
                        // NaN is never one position
                        if (!math.all(math.isfinite(p0) & math.isfinite(p1) & math.isfinite(p2)))
                            continue;
                        var key = KeyOf(Bits(p0), Bits(p1), Bits(p2));
                        byKey.Add(key, found.Length);
                        found.Add(new int3(b, s, t));
                        keys.Add(key);
                    }
                }
            }

            NativeArray<bool> gone;
            using var _gone = gone = new NativeArray<bool>(found.Length, Allocator.Temp);
            var changed = false;
            for (int i = 0; i < found.Length; i++)
            {
                if (gone[i])
                    continue;
                var key = keys[i];
                if (!byKey.TryGetFirstValue(KeyOf(key.a, key.c, key.b), out var j, out var iterator))
                    continue;
                var mine = found[i];
                var flags = brushes[mine.x].brushRenderBuffer.Value.surfaces[mine.y].destinationFlags;
                do
                {
                    if (j == i || gone[j])
                        continue;
                    var other = found[j];
                    if (brushes[other.x].brushRenderBuffer.Value.surfaces[other.y].destinationFlags != flags)
                        continue;
                    // two facing needles are WeldNeedles' to drop, and counted there
                    if (OutputWeld.OnOneLine(math.asfloat(key.a), math.asfloat(key.b), math.asfloat(key.c)))
                        break;
                    Drop(brushes, work, workIndex, mine.x, mine.y, mine.z);
                    Drop(brushes, work, workIndex, other.x, other.y, other.z);
                    gone[i] = true;
                    gone[j] = true;
                    cancelled++;
                    changed = true;
                    break;
                } while (byKey.TryGetNextValue(out j, ref iterator));
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
                    var w = Find(workIndex, brush, s);
                    if (w < 0)
                    {
                        to.vertexCount       = from.vertexCount;
                        to.indexCount        = from.indexCount;
                        to.geometryHashValue = from.geometryHashValue;
                        to.surfaceHashValue  = from.surfaceHashValue;
                        to.aabb              = from.aabb;
                        to.lightmapChart     = from.lightmapChart;
                        builder.Construct(ref to.indices,          ref from.indices);
                        builder.Construct(ref to.renderVertices,   ref from.renderVertices);
                        builder.Construct(ref to.selectVertices,   ref from.selectVertices);
                        builder.Construct(ref to.colliderVertices, ref from.colliderVertices);
                        builder.Construct(ref to.needles,          ref from.needles);
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
