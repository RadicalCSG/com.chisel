using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Mathematics;

namespace Chisel.Core
{
    // A brush the weld changed (OutputModelWeld.PlanWeld), and its buffer stored again
    internal struct WeldCopy
    {
        public int brush;
        public uint4 key;
        public bool keyed;
        public bool reused;
        public BlobAssetReference<ChiselBrushRenderBuffer> result;
    }

    // A brush's welded buffer, with what it was made from
    internal struct WeldCopyEntry
    {
        public uint4 key;
        public BlobAssetReference<ChiselBrushRenderBuffer> result;
        public bool used;
    }

    // The surfaces the weld changed, kept until their brushes are stored again
    internal struct WeldState
    {
        public NativeList<OutputModelWeld.Working> work;
        public NativeParallelHashMap<long, int> workIndex;
        public NativeList<WeldCopy> copies;
        // the collapse, brush by brush: each brush's short edges and the corners that stay (counts, then where they start),
        // where the corners that move go, and what it does to each brush's surfaces
        public NativeList<int2>     edgeCounts, edgeOffsets;
        public NativeList<float3x2> shortPairs;
        public NativeList<float3>   keptCorners;
        public NativeParallelHashMap<float3, float3> movedTo;
        public NativeList<int>      decisionCounts, decisionOffsets;
        public NativeList<OutputModelWeld.CollapseDecision> decisions;
        // the triangles this update welds (OutputModelWeld.PrepareIncremental); all of them unless it says otherwise
        public WeldScope scope;

        public static WeldState Create(Allocator allocator)
        {
            return new WeldState
            {
                work      = new NativeList<OutputModelWeld.Working>(16, allocator),
                workIndex = new NativeParallelHashMap<long, int>(16, allocator),
                copies    = new NativeList<WeldCopy>(16, allocator),
                edgeCounts      = new NativeList<int2>(16, allocator),
                edgeOffsets     = new NativeList<int2>(16, allocator),
                shortPairs      = new NativeList<float3x2>(16, allocator),
                keptCorners     = new NativeList<float3>(16, allocator),
                movedTo         = new NativeParallelHashMap<float3, float3>(16, allocator),
                decisionCounts  = new NativeList<int>(16, allocator),
                decisionOffsets = new NativeList<int>(16, allocator),
                decisions       = new NativeList<OutputModelWeld.CollapseDecision>(16, allocator),
                scope           = WeldScope.Create(allocator)
            };
        }

        public void Clear()
        {
            FreeWork(work);
            work.Clear();
            workIndex.Clear();
            copies.Clear();
            edgeCounts.Clear(); edgeOffsets.Clear(); shortPairs.Clear(); keptCorners.Clear(); movedTo.Clear();
            decisionCounts.Clear(); decisionOffsets.Clear(); decisions.Clear();
        }

        internal static void FreeWork(NativeList<OutputModelWeld.Working> work)
        {
            for (int k = 0; k < work.Length; k++)
            {
                var w = work[k];
                if (w.indices.IsCreated)  w.indices.Dispose();
                if (w.collider.IsCreated) w.collider.Dispose();
                if (w.select.IsCreated)   w.select.Dispose();
                if (w.render.IsCreated)   w.render.Dispose();
                if (w.splitRoots.IsCreated) w.splitRoots.Dispose();
            }
        }

        public void Dispose()
        {
            if (work.IsCreated)
            {
                FreeWork(work);
                work.Dispose();
            }
            if (workIndex.IsCreated) workIndex.Dispose();
            if (copies.IsCreated)    copies.Dispose();
            if (edgeCounts.IsCreated)      edgeCounts.Dispose();
            if (edgeOffsets.IsCreated)     edgeOffsets.Dispose();
            if (shortPairs.IsCreated)      shortPairs.Dispose();
            if (keptCorners.IsCreated)     keptCorners.Dispose();
            if (movedTo.IsCreated)         movedTo.Dispose();
            if (decisionCounts.IsCreated)  decisionCounts.Dispose();
            if (decisionOffsets.IsCreated) decisionOffsets.Dispose();
            if (decisions.IsCreated)       decisions.Dispose();
            scope.Dispose();
        }

        public Unity.Jobs.JobHandle Dispose(Unity.Jobs.JobHandle dependency)
        {
            if (work.IsCreated)
                dependency = Unity.Jobs.IJobExtensions.Schedule(new FreeWeldWorkJob { work = work }, dependency);
            var a = work.IsCreated      ? work.Dispose(dependency)      : dependency;
            var b = workIndex.IsCreated ? workIndex.Dispose(dependency) : dependency;
            var c = copies.IsCreated    ? copies.Dispose(dependency)    : dependency;
            var d = dependency;
            var handles = new NativeArray<Unity.Jobs.JobHandle>(11, Allocator.Temp);
            handles[0] = Unity.Jobs.JobHandle.CombineDependencies(a, b, c);
            handles[1] = d;
            handles[2] = edgeCounts.IsCreated      ? edgeCounts.Dispose(dependency)      : dependency;
            handles[3] = edgeOffsets.IsCreated     ? edgeOffsets.Dispose(dependency)     : dependency;
            handles[4] = shortPairs.IsCreated      ? shortPairs.Dispose(dependency)      : dependency;
            handles[5] = keptCorners.IsCreated     ? keptCorners.Dispose(dependency)     : dependency;
            handles[6] = movedTo.IsCreated         ? movedTo.Dispose(dependency)         : dependency;
            handles[7] = decisionCounts.IsCreated  ? decisionCounts.Dispose(dependency)  : dependency;
            handles[8] = decisionOffsets.IsCreated ? decisionOffsets.Dispose(dependency) : dependency;
            handles[9] = decisions.IsCreated       ? decisions.Dispose(dependency)       : dependency;
            handles[10] = scope.Dispose(dependency);
            var all = Unity.Jobs.JobHandle.CombineDependencies(handles);
            handles.Dispose();
            return all;
        }
    }

    [Unity.Burst.BurstCompile]
    struct FreeWeldWorkJob : Unity.Jobs.IJob
    {
        public NativeList<OutputModelWeld.Working> work;
        public void Execute() { WeldState.FreeWork(work); }
    }

    internal static partial class OutputModelWeld
    {
        // Edges shorter than this are collapsed, the last step across the model: where many planes
        // meet (a sphere's pole) planes snapped one by one meet a fraction of a millimetre apart, which only slivers show
        public const float kCollapseLength = 1e-3f;

        // A surface this weld changes: the triangles of one dropped are -1. One only the collapse changed has no copy, only
        // what the collapse does to it (WeldState.decisions from decisionStart): nothing reads it before Copy
        internal struct Working
        {
            public int                      brush;
            public int                      surface;
            public UnsafeList<int>          indices;
            public UnsafeList<float3>       collider;
            public UnsafeList<SelectVertex> select;
            public UnsafeList<RenderVertex> render;
            public int                      decisionStart, decisionCount;
            // the input triangles its splits started from (WeldMemory)
            public UnsafeList<int>          splitRoots;
            public readonly bool CollapseOnly => !indices.IsCreated;
        }

        // What the collapse does to a surface: drops a triangle (index), or moves a corner (index) to
        internal struct CollapseDecision
        {
            public int    surface;
            public int    index;
            public bool   move;
            public float3 to;
        }

        struct Needle
        {
            public int brush;
            public int surface;
            public int triangle;
        }

        public static void WeldModel(NativeList<BrushData> brushes, NativeList<BlobAssetReference<ChiselBrushRenderBuffer>> patched)
        {
            WeldModel(brushes, patched, out _, out _, out _, out _);
        }

        // The same; moves: where the last step moved each corner it moved (from, to), what ExactJudge checks it by
        public static void WeldModel(NativeList<BrushData> brushes, NativeList<BlobAssetReference<ChiselBrushRenderBuffer>> patched, NativeList<float3x2> moves)
        {
            WeldModel(brushes, patched, out _, out _, out _, out _, moves);
        }

        public static void WeldModel(NativeList<BrushData> brushes, NativeList<BlobAssetReference<ChiselBrushRenderBuffer>> patched,
                                     out int split, out int paired, out int cancelled)
        {
            WeldModel(brushes, patched, out split, out paired, out cancelled, out _);
        }

        // The same; collapsed: the corners moved onto another by CollapseShortEdges
        public static void WeldModel(NativeList<BrushData> brushes, NativeList<BlobAssetReference<ChiselBrushRenderBuffer>> patched,
                                     out int split, out int paired, out int cancelled, out int collapsed, NativeList<float3x2> moves = default)
        {
            var state = WeldState.Create(Allocator.Temp);
            try
            {
                PlanWeld(brushes, ref state, out split, out paired, out cancelled, out collapsed, moves);
                var copies = state.copies.AsArray();
                for (int i = 0; i < copies.Length; i++)
                    copies[i] = CopyPlanned(copies[i], brushes.AsArray(), state.work, state.workIndex, state.decisions, default);
                FinishWeld(brushes, patched, state.copies, default);
            }
            finally
            {
                state.Dispose();
            }
        }

        // Every step of the weld but the copies of the brushes it changed (state.copies, CopyPlanned), one after another
        public static unsafe void PlanWeld(NativeList<BrushData> brushes, ref WeldState state, out int split, out int paired, out int cancelled, out int collapsed,
                                           NativeList<float3x2> moves = default)
        {
            PlanWeldBeforeCollapse(brushes, ref state, out split, out paired, out cancelled, moves);
            collapsed = Collapse(brushes, ref state, moves);
        }

        // The collapse, its steps one after another
        static unsafe int Collapse(NativeList<BrushData> brushes, ref WeldState state, NativeList<float3x2> moves)
        {
            var counts = state.edgeCounts.AsArray();
            for (int b = 0; b < brushes.Length; b++)
                counts[b] = ShortEdgesOf(b, brushes.AsArray(), state.work, state.workIndex, state.scope, null, null);
            OffsetShortEdges(ref state);
            for (int b = 0; b < brushes.Length; b++)
                ShortEdgesOf(b, brushes.AsArray(), state.work, state.workIndex, state.scope,
                             (float3x2*)state.shortPairs.GetUnsafePtr() + state.edgeOffsets[b].x, (float3*)state.keptCorners.GetUnsafePtr() + state.edgeOffsets[b].y);
            var collapsed = ClusterShortEdges(ref state, moves);
            var decisionCounts = state.decisionCounts.AsArray();
            for (int b = 0; b < brushes.Length; b++)
                decisionCounts[b] = DecideCollapse(b, brushes.AsArray(), state.work, state.workIndex, state.scope, state.movedTo, null);
            OffsetDecisions(ref state);
            for (int b = 0; b < brushes.Length; b++)
                DecideCollapse(b, brushes.AsArray(), state.work, state.workIndex, state.scope, state.movedTo,
                               (CollapseDecision*)state.decisions.GetUnsafePtr() + state.decisionOffsets[b]);
            ApplyCollapse(brushes, ref state);
            return collapsed;
        }

        // The weld up to the collapse (the steps a job takes before ShortEdgesOf runs brush by brush)
        public static void PlanWeldBeforeCollapse(NativeList<BrushData> brushes, ref WeldState state, out int split, out int paired, out int cancelled,
                                                  NativeList<float3x2> moves = default)
        {
            if (moves.IsCreated)
                moves.Clear();
            split = 0;
            paired = 0;
            cancelled = 0;
            state.Clear();
            state.edgeCounts.Resize(brushes.Length, NativeArrayOptions.ClearMemory);
            var needles = new NativeList<Needle>(Allocator.Temp);
            for (int b = 0; b < brushes.Length; b++)
            {
                if (!brushes[b].brushRenderBuffer.IsCreated || !state.scope.Touches(b))
                    continue;
                ref var surfaces = ref brushes[b].brushRenderBuffer.Value.surfaces;
                for (int s = 0; s < surfaces.Length; s++)
                {
                    ref var list = ref surfaces[s].needles;
                    for (int n = 0; n < list.Length; n++)
                    {
                        if (state.scope.Contains(b, s, list[n]))
                            needles.Add(new Needle { brush = b, surface = s, triangle = list[n] });
                    }
                }
            }

            // What each brush's output spans, to find the brushes a long edge can lie on (FindAcross looks at no other)
            var bounds = new NativeArray<MinMaxAABB>(brushes.Length, Allocator.Temp);
            for (int b = 0; b < brushes.Length; b++)
            {
                var box = new MinMaxAABB { Min = new float3(float.PositiveInfinity), Max = new float3(float.NegativeInfinity) };
                if (brushes[b].brushRenderBuffer.IsCreated && state.scope.Touches(b))
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

            var workIndex = state.workIndex;
            var work      = state.work;
            if (workIndex.Capacity < needles.Length * 2 + 16)
                workIndex.Capacity = needles.Length * 2 + 16;
            var opposites = new Opposites(state.scope.all.Value ? TriangleCount(brushes) : state.scope.inside.Length, Allocator.Temp);
            var fresh     = new NativeList<int3>(Allocator.Temp);
            try
            {
                KeyAll(brushes, work, workIndex, state.scope, ref opposites);
                for (bool changed = true, first = true; changed; first = false)
                {
                    // after the first turn only the triangles the splits made can make a new pair
                    var from = first ? 0 : opposites.found.Length;
                    KeyFresh(brushes, work, workIndex, fresh, ref opposites);
                    fresh.Clear();
                    changed  = CancelOpposites(brushes, work, workIndex, ref opposites, from, ref cancelled);
                    changed |= WeldNeedles(brushes, bounds, needles, work, workIndex, state.scope, fresh, ref split, ref paired);
                }
            }
            finally
            {
                opposites.Dispose();
                fresh.Dispose();
                bounds.Dispose();
                needles.Dispose();
            }
        }

        // One changed brush's buffer stored again, or the one the cache kept when it was made from the same; each is
        // independent of the others
        public static WeldCopy CopyPlanned(WeldCopy copy, NativeArray<BrushData> brushes, NativeList<Working> work, NativeParallelHashMap<long, int> workIndex,
                                           NativeList<CollapseDecision> collapseDecisions, NativeParallelHashMap<CompactNodeID, WeldCopyEntry> cache)
        {
            var source = brushes[copy.brush].brushRenderBuffer;
            if (cache.IsCreated)
            {
                copy.keyed = CopyKey(source, copy.brush, work, workIndex, collapseDecisions, out copy.key);
                if (copy.keyed && cache.TryGetValue(brushes[copy.brush].brushIndexOrder.compactNodeID, out var entry) &&
                    entry.result.IsCreated && entry.key.Equals(copy.key))
                {
                    copy.result = entry.result;
                    copy.reused = true;
                    return copy;
                }
            }
            copy.result = Copy(source, copy.brush, work, workIndex, collapseDecisions);
            return copy;
        }

        // The copies handed to their brushes: kept by the cache, or by patched to be freed after the meshes are made; then
        // the cache forgets the brushes this update did not weld
        public static void FinishWeld(NativeList<BrushData> brushes, NativeList<BlobAssetReference<ChiselBrushRenderBuffer>> patched, NativeList<WeldCopy> copies,
                                      NativeParallelHashMap<CompactNodeID, WeldCopyEntry> cache)
        {
            var cached = cache.IsCreated;
            for (int i = 0; i < copies.Length; i++)
            {
                var copy = copies[i];
                if (cached && copy.keyed)
                {
                    var nodeID = brushes[copy.brush].brushIndexOrder.compactNodeID;
                    if (!copy.reused && cache.TryGetValue(nodeID, out var old) && old.result.IsCreated)
                        old.result.Dispose();
                    cache[nodeID] = new WeldCopyEntry { key = copy.key, result = copy.result, used = true };
                }
                else
                    patched.Add(copy.result);
                var data = brushes[copy.brush];
                data.brushRenderBuffer = copy.result;
                brushes[copy.brush] = data;
            }
            if (!cached)
                return;
            using var gone = new NativeList<CompactNodeID>(Allocator.Temp);
            foreach (var pair in cache)
            {
                if (!pair.Value.used)
                    gone.Add(pair.Key);
            }
            foreach (var id in gone)
            {
                if (cache.TryGetValue(id, out var dead) && dead.result.IsCreated)
                    dead.result.Dispose();
                cache.Remove(id);
            }
            using var left = cache.GetKeyArray(Allocator.Temp);
            foreach (var id in left)
            {
                var entry = cache[id];
                entry.used = false;
                cache[id] = entry;
            }
        }

        // What Copy reads for a brush: its buffer and the weld's working copies of its surfaces
        static unsafe bool CopyKey(BlobAssetReference<ChiselBrushRenderBuffer> source, int brush, NativeList<Working> work,
                                   NativeParallelHashMap<long, int> workIndex, NativeList<CollapseDecision> collapseDecisions, out uint4 key)
        {
            key = default;
            if (!source.IsCreated)
                return false;
            var data   = (byte*)source.GetUnsafePtr();
            var length = *(int*)(data - 32 + 8);
            if (length <= 0 || length >= (1 << 30))
                return false;
            var buffer = new UnsafeAppendBuffer(256, 16, Allocator.Temp);
            try
            {
                buffer.Add(xxHash3.Hash128(data, length));
                ref var surfaces = ref source.Value.surfaces;
                for (int s = 0; s < surfaces.Length; s++)
                {
                    var w = Find(workIndex, brush, s);
                    if (w < 0)
                        continue;
                    var item = work[w];
                    buffer.Add(s);
                    if (item.CollapseOnly)
                    {
                        buffer.Add(-1);
                        buffer.Add(HashOf((byte*)collapseDecisions.GetUnsafeReadOnlyPtr() + (long)item.decisionStart * UnsafeUtility.SizeOf<CollapseDecision>(),
                                          (long)item.decisionCount * UnsafeUtility.SizeOf<CollapseDecision>()));
                        continue;
                    }
                    buffer.Add(item.indices.Length);
                    buffer.Add(item.collider.Length);
                    buffer.Add(HashOf(item.indices.Ptr,  (long)item.indices.Length  * sizeof(int)));
                    buffer.Add(HashOf(item.collider.Ptr, (long)item.collider.Length * sizeof(float3)));
                    buffer.Add(HashOf(item.select.Ptr,   (long)item.select.Length   * UnsafeUtility.SizeOf<SelectVertex>()));
                    buffer.Add(HashOf(item.render.Ptr,   (long)item.render.Length   * UnsafeUtility.SizeOf<RenderVertex>()));
                }
                key = xxHash3.Hash128(buffer.Ptr, buffer.Length);
                return true;
            }
            finally
            {
                buffer.Dispose();
            }
        }

        static unsafe uint4 HashOf(void* data, long length) { return length == 0 ? default : xxHash3.Hash128(data, length); }

        // One pass over the needles; true when it dropped one
        static bool WeldNeedles(NativeList<BrushData> brushes, NativeArray<MinMaxAABB> bounds, NativeList<Needle> needles,
                                NativeList<Working> work, NativeParallelHashMap<long, int> workIndex, WeldScope scope, NativeList<int3> fresh,
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
                if (!FindAcross(brushes, bounds, work, workIndex, scope, needle, pu, pw,
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

        internal struct TriangleKey : System.IEquatable<TriangleKey>
        {
            public int3 a, b, c;
            public bool Equals(TriangleKey other) => math.all(a == other.a) && math.all(b == other.b) && math.all(c == other.c);
            public override int GetHashCode() => (int)math.hash(new int3x3(a, b, c));
        }

        internal static int3 Bits(float3 p)
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

        static void KeyAll(NativeList<BrushData> brushes, NativeList<Working> work, NativeParallelHashMap<long, int> workIndex, WeldScope scope, ref Opposites opposites)
        {
            for (int b = 0; b < brushes.Length; b++)
            {
                if (!brushes[b].brushRenderBuffer.IsCreated || !scope.Touches(b))
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
                            if (copy.indices[t] < 0 || !scope.Contains(b, s, t / 3))
                                continue;
                            opposites.Add(new int3(b, s, t / 3), copy.collider[copy.indices[t]], copy.collider[copy.indices[t + 1]], copy.collider[copy.indices[t + 2]]);
                        }
                        continue;
                    }
                    ref var indices   = ref surfaces[s].indices;
                    ref var positions = ref surfaces[s].colliderVertices;
                    for (int t = 0; t + 2 < indices.Length; t += 3)
                    {
                        if (scope.Contains(b, s, t / 3))
                            opposites.Add(new int3(b, s, t / 3), positions[indices[t]], positions[indices[t + 1]], positions[indices[t + 2]]);
                    }
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
                // of the triangles facing it, the one keyed last: the choice depends on the triangles, not on the map
                var best = -1;
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
                    best = math.max(best, j);
                } while (opposites.byKey.TryGetNextValue(out j, ref iterator));
                // two facing needles are WeldNeedles' to drop
                if (best < 0 || OutputWeld.OnOneLine(math.asfloat(key.a), math.asfloat(key.b), math.asfloat(key.c)))
                    continue;
                var facing = opposites.found[best];
                Drop(brushes, work, workIndex, mine.x, mine.y, mine.z);
                Drop(brushes, work, workIndex, facing.x, facing.y, facing.z);
                opposites.gone[i] = true;
                opposites.gone[best] = true;
                cancelled++;
                changed = true;
            }
            return changed;
        }

        internal struct ShortEdge
        {
            public int   a, b;
            public float length;
        }

        // Shortest first, then by the corners' positions, so the order is the same whatever order they were found in
        struct ShortestFirst : System.Collections.Generic.IComparer<ShortEdge>
        {
            public NativeArray<float3> points;
            public readonly int Compare(ShortEdge x, ShortEdge y)
            {
                if (x.length != y.length) return x.length.CompareTo(y.length);
                var c = Less(points[x.a], points[y.a]); if (c != 0) return c;
                return Less(points[x.b], points[y.b]);
            }
        }

        static int Less(float3 x, float3 y) => x.x != y.x ? x.x.CompareTo(y.x) : (x.y != y.y ? x.y.CompareTo(y.y) : x.z.CompareTo(y.z));

        // The short edges' corners made one, shortest edge first, as long as what becomes one fits within kCollapseLength
        // (a chain of short edges never drags a corner further): where each point goes, its cluster's least corner by x, y,
        // z. Edges of a and b in either order, a and b their points' indices. The same for the same edges in any order:
        // what ExactJudge accounts for with it.
        internal static void Cluster(NativeArray<float3> points, NativeList<ShortEdge> edges, NativeArray<float3> to)
        {
            for (int e = 0; e < edges.Length; e++)
            {
                var edge = edges[e];
                if (Less(points[edge.a], points[edge.b]) > 0)
                    (edge.a, edge.b) = (edge.b, edge.a);
                edges[e] = edge;
            }
            edges.Sort(new ShortestFirst { points = points });
            var parent = new NativeArray<int>(points.Length, Allocator.Temp);
            var lo     = new NativeArray<float3>(points.Length, Allocator.Temp);
            var hi     = new NativeArray<float3>(points.Length, Allocator.Temp);
            var least  = new NativeArray<float3>(points.Length, Allocator.Temp);
            for (int i = 0; i < points.Length; i++) { parent[i] = i; lo[i] = points[i]; hi[i] = points[i]; least[i] = points[i]; }
            int Root(int i)
            {
                while (parent[i] != i) { parent[i] = parent[parent[i]]; i = parent[i]; }
                return i;
            }
            for (int e = 0; e < edges.Length; e++)
            {
                int ra = Root(edges[e].a), rb = Root(edges[e].b);
                if (ra == rb)
                    continue;
                var min = math.min(lo[ra], lo[rb]); var max = math.max(hi[ra], hi[rb]);
                if (!(math.length(max - min) <= kCollapseLength))
                    continue;
                parent[rb] = ra;
                lo[ra] = min; hi[ra] = max;
                least[ra] = Less(least[ra], least[rb]) <= 0 ? least[ra] : least[rb];
            }
            for (int i = 0; i < points.Length; i++)
                to[i] = least[Root(i)];
            parent.Dispose(); lo.Dispose(); hi.Dispose(); least.Dispose();
        }

        // The collapse of the model's edges shorter than kCollapseLength (Cluster), last, away from what the steps before
        // changed: every surface of every brush with a corner there moves it, so the model stays closed, and a triangle
        // left without area is dropped. Brush by brush where it can be: ShortEdgesOf, ClusterShortEdges, DecideCollapse,
        // ApplyCollapse; each brush's part written where the counts put it, so the result is the same in any order.

        // A brush's short edges (pairs) and the corners that stay (kept: a needle's, and those of what the steps before
        // changed); counts them when pairs is null
        public static unsafe int2 ShortEdgesOf(int b, NativeArray<BrushData> brushes, NativeList<Working> work, NativeParallelHashMap<long, int> workIndex,
                                               WeldScope scope, float3x2* pairs, float3* kept)
        {
            var count = int2.zero;
            // an incremental update collapses by point group (OutputModelWeld.RecordIncremental)
            if (!brushes[b].brushRenderBuffer.IsCreated || (scope.all.IsCreated && !scope.all.Value))
                return count;
            var limitSq = kCollapseLength * kCollapseLength;
            var surfaceCount = brushes[b].brushRenderBuffer.Value.surfaces.Length;
            for (int s = 0; s < surfaceCount; s++)
            {
                // a decal's pieces follow the corners that move, but their short edges do not move anything
                if (brushes[b].brushRenderBuffer.Value.surfaces[s].decalEntityID != 0)
                    continue;
                var view = new SurfaceView(brushes, work, workIndex, b, s);
                for (int t = 0; t < view.TriangleCount; t++)
                {
                    if (!view.Corners(t, out var c0, out var c1, out var c2) || !scope.Contains(b, s, t))
                        continue;
                    var q0 = view.Position(c0) + 0.0f;
                    var q1 = view.Position(c1) + 0.0f;
                    var q2 = view.Position(c2) + 0.0f;
                    if (IsNeedle(q0, q1, q2) || view.Changed(t))
                    {
                        if (kept != null) { kept[count.y] = q0; kept[count.y + 1] = q1; kept[count.y + 2] = q2; }
                        count.y += 3;
                        continue;
                    }
                    AddShort(q0, q1); AddShort(q1, q2); AddShort(q2, q0);
                }
            }
            return count;

            void AddShort(float3 x, float3 y)
            {
                var d = math.distancesq(x, y);
                if (!(d > 0) || !(d < limitSq))
                    return;
                if (pairs != null)
                    pairs[count.x] = new float3x2(x, y);
                count.x++;
            }
        }

        // Where each brush's short edges and kept corners start, and room for them
        public static void OffsetShortEdges(ref WeldState state)
        {
            var counts = state.edgeCounts;
            state.edgeOffsets.ResizeUninitialized(counts.Length);
            var total = int2.zero;
            for (int b = 0; b < counts.Length; b++)
            {
                state.edgeOffsets[b] = total;
                total += counts[b];
            }
            state.shortPairs.ResizeUninitialized(total.x);
            state.keptCorners.ResizeUninitialized(total.y);
        }

        // Where the corners that move go (movedTo); the number of them. moves: (from, to) of each
        public static int ClusterShortEdges(ref WeldState state, NativeList<float3x2> moves)
        {
            var collapsed = 0;
            state.movedTo.Clear();
            state.decisionCounts.Resize(state.edgeCounts.Length, NativeArrayOptions.ClearMemory);
            var ids    = new UnsafeHashMap<float3, int>(64, Allocator.Temp);
            var points = new NativeList<float3>(64, Allocator.Temp);
            var edges  = new NativeList<ShortEdge>(64, Allocator.Temp);
            var keptPoints = new UnsafeHashSet<float3>(math.max(16, state.keptCorners.Length), Allocator.Temp);
            int IdOf(float3 p)
            {
                if (!ids.TryGetValue(p, out var id)) { id = points.Length; ids.Add(p, id); points.Add(p); }
                return id;
            }
            try
            {
                var kept = state.keptCorners;
                for (int i = 0; i < kept.Length; i++)
                    keptPoints.Add(kept[i]);
                // a needle's corners stay, and those of what the steps before changed: those are theirs
                var pairs = state.shortPairs;
                for (int e = 0; e < pairs.Length; e++)
                {
                    var x = pairs[e].c0; var y = pairs[e].c1;
                    if (keptPoints.Contains(x) || keptPoints.Contains(y))
                        continue;
                    int a = IdOf(x), c = IdOf(y);
                    edges.Add(new ShortEdge { a = math.min(a, c), b = math.max(a, c), length = math.distance(x, y) });
                }
                if (edges.Length == 0)
                    return 0;

                var to = new NativeArray<float3>(points.Length, Allocator.Temp);
                Cluster(points.AsArray(), edges, to);
                for (int i = 0; i < points.Length; i++)
                {
                    if (to[i].Equals(points[i]))
                        continue;
                    collapsed++;
                    state.movedTo.TryAdd(points[i], to[i]);
                    if (moves.IsCreated)
                        moves.Add(new float3x2(points[i], to[i]));
                }
                to.Dispose();
                return collapsed;
            }
            finally
            {
                edges.Dispose(); points.Dispose(); ids.Dispose(); keptPoints.Dispose();
            }
        }

        // What the collapse does to a brush's surfaces (counts it when decisions is null): surface by surface, a triangle the
        // collapse leaves without area is dropped, judged from the corners as they were (one the collapse did not touch stays
        // as it is), and every corner of a triangle left at a position that moves takes its cluster's
        public static unsafe int DecideCollapse(int b, NativeArray<BrushData> brushes, NativeList<Working> work, NativeParallelHashMap<long, int> workIndex,
                                                WeldScope scope, NativeParallelHashMap<float3, float3> movedTo, CollapseDecision* decisions)
        {
            var count = 0;
            // a triangle out of the scope has no corner at a position that moves
            if (!brushes[b].brushRenderBuffer.IsCreated || !scope.Touches(b) || movedTo.Count() == 0)
                return 0;
            var targets = new NativeList<float3>(64, Allocator.Temp);
            var movedAt = new NativeList<bool>(64, Allocator.Temp);
            var dropped = new NativeList<bool>(64, Allocator.Temp);
            var surfaceCount = brushes[b].brushRenderBuffer.Value.surfaces.Length;
            for (int s = 0; s < surfaceCount; s++)
            {
                var view = new SurfaceView(brushes, work, workIndex, b, s);
                var vertexCount = view.VertexCount;
                targets.ResizeUninitialized(vertexCount);
                movedAt.Resize(vertexCount, NativeArrayOptions.ClearMemory);
                var any = false;
                for (int v = 0; v < vertexCount; v++)
                {
                    movedAt[v] = false;
                    if (!movedTo.TryGetValue(view.Position(v) + 0.0f, out var at))
                        continue;
                    targets[v] = at;
                    movedAt[v] = true;
                    any = true;
                }
                if (!any)
                    continue;
                dropped.Resize(view.TriangleCount, NativeArrayOptions.ClearMemory);
                for (int t = 0; t < view.TriangleCount; t++)
                {
                    dropped[t] = false;
                    if (!view.Corners(t, out var c0, out var c1, out var c2))
                        continue;
                    var m0 = movedAt[c0]; var m1 = movedAt[c1]; var m2 = movedAt[c2];
                    if (!m0 && !m1 && !m2)
                        continue;
                    var p0 = m0 ? targets[c0] : view.Position(c0);
                    var p1 = m1 ? targets[c1] : view.Position(c1);
                    var p2 = m2 ? targets[c2] : view.Position(c2);
                    if (!OutputWeld.WithoutArea(p0, p1, p2))
                        continue;
                    dropped[t] = true;
                    if (decisions != null)
                        decisions[count] = new CollapseDecision { surface = s, index = t };
                    count++;
                }
                // the corners of the triangles left
                var used = new NativeArray<bool>(vertexCount, Allocator.Temp);
                for (int t = 0; t < view.TriangleCount; t++)
                {
                    if (dropped[t] || !view.Corners(t, out var c0, out var c1, out var c2))
                        continue;
                    used[c0] = used[c1] = used[c2] = true;
                }
                for (int v = 0; v < vertexCount; v++)
                {
                    if (!used[v] || !movedAt[v])
                        continue;
                    if (decisions != null)
                        decisions[count] = new CollapseDecision { surface = s, index = v, move = true, to = targets[v] };
                    count++;
                }
                used.Dispose();
            }
            targets.Dispose(); movedAt.Dispose(); dropped.Dispose();
            return count;
        }

        // Where each brush's decisions start, and room for them
        public static void OffsetDecisions(ref WeldState state)
        {
            var counts = state.decisionCounts;
            state.decisionOffsets.ResizeUninitialized(counts.Length);
            var total = 0;
            for (int b = 0; b < counts.Length; b++)
            {
                state.decisionOffsets[b] = total;
                total += counts[b];
            }
            state.decisions.ResizeUninitialized(total);
        }

        // The decisions carried out, surface by surface (a surface nothing else of the weld changed keeps only its
        // decisions); then the brushes the weld changed, each once
        public static void ApplyCollapse(NativeList<BrushData> brushes, ref WeldState state)
        {
            var work      = state.work;
            var workIndex = state.workIndex;
            var decisions = state.decisions;
            for (int b = 0; b < state.decisionCounts.Length && b < brushes.Length; b++)
            {
                var start = state.decisionOffsets[b];
                var end   = start + state.decisionCounts[b];
                for (int i = start; i < end;)
                {
                    var s = decisions[i].surface;
                    var last = i;
                    while (last < end && decisions[last].surface == s)
                        last++;
                    if (Find(workIndex, b, s) < 0)
                    {
                        workIndex.TryAdd(Key(b, s), work.Length);
                        work.Add(new Working { brush = b, surface = s, decisionStart = i, decisionCount = last - i });
                    } else
                    {
                        for (int k = i; k < last; k++)
                        {
                            if (!decisions[k].move)
                                Drop(brushes, work, workIndex, b, s, decisions[k].index);
                        }
                        var w = WorkOn(brushes, work, workIndex, b, s);
                        ref var copy = ref work.ElementAt(w);
                        for (int k = i; k < last; k++)
                        {
                            if (decisions[k].move)
                                MoveCorner(ref copy, decisions[k].index, decisions[k].to);
                        }
                    }
                    i = last;
                }
            }

            var copied = new NativeArray<bool>(brushes.Length, Allocator.Temp);
            for (int k = 0; k < work.Length; k++)
            {
                var brush = work[k].brush;
                if (copied[brush])
                    continue;
                copied[brush] = true;
                state.copies.Add(new WeldCopy { brush = brush });
            }
            copied.Dispose();
        }

        // A surface as the weld has it now (its working copy, or its brush's buffer), looked up once
        unsafe struct SurfaceView
        {
            int*    indices;
            float3* positions;
            int*    source;
            int     indexCount, sourceCount, vertexCount;
            bool    worked;

            public SurfaceView(NativeArray<BrushData> brushes, NativeList<Working> work, NativeParallelHashMap<long, int> workIndex, int brush, int surface)
            {
                ref var original = ref brushes[brush].brushRenderBuffer.Value.surfaces[surface];
                source      = (int*)original.indices.GetUnsafePtr();
                sourceCount = original.indices.Length;
                var w = Find(workIndex, brush, surface);
                worked = w >= 0;
                if (worked)
                {
                    var copy   = work[w];
                    indices    = copy.indices.Ptr;
                    indexCount = copy.indices.Length;
                    positions  = copy.collider.Ptr;
                    vertexCount = copy.collider.Length;
                } else
                {
                    indices    = source;
                    indexCount = sourceCount;
                    positions  = (float3*)original.colliderVertices.GetUnsafePtr();
                    vertexCount = original.colliderVertices.Length;
                }
            }

            public readonly int TriangleCount => indexCount / 3;
            public readonly int VertexCount => vertexCount;

            public readonly bool Corners(int triangle, out int i0, out int i1, out int i2)
            {
                i0 = indices[triangle * 3]; i1 = indices[triangle * 3 + 1]; i2 = indices[triangle * 3 + 2];
                return i0 >= 0;
            }

            public readonly float3 Position(int vertex) => positions[vertex];

            // as Changed
            public readonly bool Changed(int triangle)
            {
                if (!worked)
                    return false;
                if (triangle * 3 + 2 >= sourceCount)
                    return true;
                for (int k = 0; k < 3; k++)
                {
                    if (indices[triangle * 3 + k] != source[triangle * 3 + k])
                        return true;
                }
                return false;
            }
        }

        // Whether the steps before the collapse changed a triangle: added it, or rewrote it (a dropped one is not there)
        static bool Changed(NativeList<BrushData> brushes, NativeList<Working> work, NativeParallelHashMap<long, int> workIndex, int brush, int surface, int triangle)
        {
            var w = Find(workIndex, brush, surface);
            if (w < 0)
                return false;
            ref var source = ref brushes[brush].brushRenderBuffer.Value.surfaces[surface];
            if (triangle * 3 + 2 >= source.indices.Length)
                return true;
            var indices = work[w].indices;
            for (int k = 0; k < 3; k++)
            {
                if (indices[triangle * 3 + k] != source.indices[triangle * 3 + k])
                    return true;
            }
            return false;
        }

        // A triangle whose corners lie on one line in float, none two at one position (OutputWeld.FindNeedles)
        internal static bool IsNeedle(float3 a, float3 b, float3 c) => !OutputWeld.HasTwoCornersAtOnePosition(a, b, c) && OutputWeld.OnOneLine(a, b, c);

        static void MoveCorner(ref Working copy, int vertex, float3 to)
        {
            copy.collider[vertex] = to;
            var select = copy.select[vertex]; select.position = to; copy.select[vertex] = select;
            var render = copy.render[vertex]; render.position = to; copy.render[vertex] = render;
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
                               NativeParallelHashMap<long, int> workIndex, WeldScope scope, Needle needle, float3 pu, float3 pw,
                               out int acrossBrush, out int acrossSurface, out int acrossTriangle, out int au, out int aw, out int ad)
        {
            for (int b = 0; b < brushes.Length; b++)
            {
                if (!scope.Touches(b) || !Contains(bounds[b], pu) || !Contains(bounds[b], pw))
                    continue;
                ref var surfaces = ref brushes[b].brushRenderBuffer.Value.surfaces;
                for (int s = 0; s < surfaces.Length; s++)
                {
                    if (surfaces[s].indexCount == 0 || !Contains(surfaces[s].aabb, pu) || !Contains(surfaces[s].aabb, pw))
                        continue;
                    var count = TriangleCount(brushes, work, workIndex, b, s);
                    for (int t = 0; t < count; t++)
                    {
                        if ((b == needle.brush && s == needle.surface && t == needle.triangle) || !scope.Contains(b, s, t))
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
                indices  = new UnsafeList<int>(source.indices.Length + 3, Allocator.Persistent),
                collider = new UnsafeList<float3>(source.colliderVertices.Length + 1, Allocator.Persistent),
                select   = new UnsafeList<SelectVertex>(source.selectVertices.Length + 1, Allocator.Persistent),
                render   = new UnsafeList<RenderVertex>(source.renderVertices.Length + 1, Allocator.Persistent),
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
            if (triangle < brushes[brush].brushRenderBuffer.Value.surfaces[surface].indices.Length / 3)
            {
                if (!copy.splitRoots.IsCreated)
                    copy.splitRoots = new UnsafeList<int>(1, Allocator.Persistent);
                copy.splitRoots.Add(triangle);
            }
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
        static unsafe BlobAssetReference<ChiselBrushRenderBuffer> Copy(BlobAssetReference<ChiselBrushRenderBuffer> source, int brush,
                                                                       NativeList<Working> work, NativeParallelHashMap<long, int> workIndex,
                                                                       NativeList<CollapseDecision> collapseDecisions)
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
                    var materialized = copy.CollapseOnly;
                    if (materialized)
                    {
                        // the surface as it was with what the collapse did to it
                        copy.indices  = new UnsafeList<int>(from.indices.Length, Allocator.Temp);
                        copy.collider = new UnsafeList<float3>(from.colliderVertices.Length, Allocator.Temp);
                        copy.select   = new UnsafeList<SelectVertex>(from.selectVertices.Length, Allocator.Temp);
                        copy.render   = new UnsafeList<RenderVertex>(from.renderVertices.Length, Allocator.Temp);
                        copy.indices .AddRange(from.indices.GetUnsafePtr(),          from.indices.Length);
                        copy.collider.AddRange(from.colliderVertices.GetUnsafePtr(), from.colliderVertices.Length);
                        copy.select  .AddRange(from.selectVertices.GetUnsafePtr(),   from.selectVertices.Length);
                        copy.render  .AddRange(from.renderVertices.GetUnsafePtr(),   from.renderVertices.Length);
                        for (int m = copy.decisionStart; m < copy.decisionStart + copy.decisionCount; m++)
                        {
                            var decision = collapseDecisions[m];
                            if (decision.move)
                                MoveCorner(ref copy, decision.index, decision.to);
                            else
                                copy.indices[decision.index * 3] = copy.indices[decision.index * 3 + 1] = copy.indices[decision.index * 3 + 2] = -1;
                        }
                    }
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
                    if (materialized)
                    {
                        copy.indices.Dispose(); copy.collider.Dispose(); copy.select.Dispose(); copy.render.Dispose();
                    }
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
