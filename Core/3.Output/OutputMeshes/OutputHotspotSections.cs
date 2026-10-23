using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Mathematics;

namespace Chisel.Core
{
    // After the model's weld: the surfaces with hotspot sections are cut where their sections meet and given their uvs.
    // Only what is drawn is cut; the collider keeps the welded triangles (ChiselSurfaceRenderBuffer.physicsIndices).
    // A brush's cut surfaces, with what they were made from
    internal struct HotspotCutEntry
    {
        public uint4 key;
        public BlobAssetReference<ChiselBrushRenderBuffer> result;
        public bool used;
    }

    // A brush to cut (OutputHotspotSections.PlanCut), and what the cut made of it
    internal struct HotspotCutWork
    {
        public int brush;
        public uint4 key;
        public bool keyed;
        public BlobAssetReference<ChiselBrushRenderBuffer> result;
    }

    // What planning the cut finds for the model, read by every brush's cut
    internal struct HotspotCutState
    {
        // the maps behind a reference: an UnsafeHashMap copied into a job keeps its storage from before it grew
        public NativeReference<UnsafeHashMap<int2, int>> groupOfRef;
        public NativeReference<UnsafeHashMap<int2, float4x4>> unfoldOfRef;
        public UnsafeParallelMultiHashMap<int2, HotspotBorderCorner> bordersOf;
        public NativeList<HotspotGroups.Group> groups;
        public NativeList<HotspotSectionLine> lines;
        public NativeList<HotspotSectionHalfPlane> halfPlanes;
        public NativeList<HotspotSectionCell> cells;
        public NativeList<HotspotCutWork> work;
        // each brush's own buffer, before the weld's copies (PlanCut's kept)
        public NativeList<BlobAssetReference<ChiselBrushRenderBuffer>> kept;
        // what the cut reads, worked out before the cache is asked (StartCutKeys, BrushCutKey): each group's key, what
        // every brush reads, and each brush's key; brushCuts: 0 not cut, 1 cut and keyed, 2 cut every time
        public NativeList<uint4> groupKeys;
        public NativeReference<uint4> globalKey;
        public NativeList<uint4> brushKeys;
        public NativeList<byte>  brushCuts;

        public static HotspotCutState Create(Allocator allocator)
        {
            return new HotspotCutState
            {
                groupOfRef  = new NativeReference<UnsafeHashMap<int2, int>>(new UnsafeHashMap<int2, int>(64, allocator), allocator),
                unfoldOfRef = new NativeReference<UnsafeHashMap<int2, float4x4>>(new UnsafeHashMap<int2, float4x4>(8, allocator), allocator),
                bordersOf  = new UnsafeParallelMultiHashMap<int2, HotspotBorderCorner>(8, allocator),
                groups     = new NativeList<HotspotGroups.Group>(8, allocator),
                lines      = new NativeList<HotspotSectionLine>(32, allocator),
                halfPlanes = new NativeList<HotspotSectionHalfPlane>(64, allocator),
                cells      = new NativeList<HotspotSectionCell>(32, allocator),
                work       = new NativeList<HotspotCutWork>(16, allocator),
                kept       = new NativeList<BlobAssetReference<ChiselBrushRenderBuffer>>(16, allocator),
                groupKeys  = new NativeList<uint4>(16, allocator),
                globalKey  = new NativeReference<uint4>(allocator),
                brushKeys  = new NativeList<uint4>(16, allocator),
                brushCuts  = new NativeList<byte>(16, allocator)
            };
        }

        public readonly unsafe ref UnsafeHashMap<int2, int> GroupOf => ref UnsafeUtility.AsRef<UnsafeHashMap<int2, int>>(groupOfRef.GetUnsafePtr());
        public readonly unsafe ref UnsafeHashMap<int2, float4x4> UnfoldOf => ref UnsafeUtility.AsRef<UnsafeHashMap<int2, float4x4>>(unfoldOfRef.GetUnsafePtr());

        public void Dispose()
        {
            if (groupOfRef.IsCreated)  { if (GroupOf.IsCreated) GroupOf.Dispose(); groupOfRef.Dispose(); }
            if (unfoldOfRef.IsCreated) { if (UnfoldOf.IsCreated) UnfoldOf.Dispose(); unfoldOfRef.Dispose(); }
            if (bordersOf.IsCreated)  bordersOf.Dispose();
            if (groups.IsCreated)     groups.Dispose();
            if (lines.IsCreated)      lines.Dispose();
            if (halfPlanes.IsCreated) halfPlanes.Dispose();
            if (cells.IsCreated)      cells.Dispose();
            if (work.IsCreated)       work.Dispose();
            if (kept.IsCreated)       kept.Dispose();
            if (groupKeys.IsCreated)  groupKeys.Dispose();
            if (globalKey.IsCreated)  globalKey.Dispose();
            if (brushKeys.IsCreated)  brushKeys.Dispose();
            if (brushCuts.IsCreated)  brushCuts.Dispose();
        }

        public Unity.Jobs.JobHandle Dispose(Unity.Jobs.JobHandle dependency)
        {
            // the maps as the jobs left them, then their references
            if (groupOfRef.IsCreated && unfoldOfRef.IsCreated)
                dependency = Unity.Jobs.IJobExtensions.Schedule(new DisposeHotspotCutMapsJob { groupOf = groupOfRef, unfoldOf = unfoldOfRef }, dependency);
            var handles = new NativeArray<Unity.Jobs.JobHandle>(13, Allocator.Temp);
            for (int i = 0; i < handles.Length; i++)
                handles[i] = dependency;
            if (groupOfRef.IsCreated)  handles[0] = groupOfRef.Dispose(dependency);
            if (unfoldOfRef.IsCreated) handles[1] = unfoldOfRef.Dispose(dependency);
            if (bordersOf.IsCreated)  handles[2] = bordersOf.Dispose(dependency);
            if (groups.IsCreated)     handles[3] = groups.Dispose(dependency);
            if (lines.IsCreated)      handles[4] = lines.Dispose(dependency);
            if (halfPlanes.IsCreated) handles[5] = halfPlanes.Dispose(dependency);
            if (cells.IsCreated)      handles[6] = cells.Dispose(dependency);
            if (work.IsCreated)       handles[7] = work.Dispose(dependency);
            if (kept.IsCreated)       handles[8] = kept.Dispose(dependency);
            if (groupKeys.IsCreated)  handles[9] = groupKeys.Dispose(dependency);
            if (globalKey.IsCreated)  handles[10] = globalKey.Dispose(dependency);
            if (brushKeys.IsCreated)  handles[11] = brushKeys.Dispose(dependency);
            if (brushCuts.IsCreated)  handles[12] = brushCuts.Dispose(dependency);
            var all = Unity.Jobs.JobHandle.CombineDependencies(handles);
            handles.Dispose();
            return all;
        }
    }

    [Unity.Burst.BurstCompile]
    struct DisposeHotspotCutMapsJob : Unity.Jobs.IJob
    {
        public NativeReference<UnsafeHashMap<int2, int>> groupOf;
        public NativeReference<UnsafeHashMap<int2, float4x4>> unfoldOf;

        public void Execute()
        {
            var a = groupOf.Value;  if (a.IsCreated) a.Dispose();
            var b = unfoldOf.Value; if (b.IsCreated) b.Dispose();
        }
    }

    internal static class OutputHotspotSections
    {
        // kept: each brush's own buffer, as the tree keeps it (before the weld patched any); it is told the group each of its
        // surfaces was mapped with, which the editor reads (the patched copies are gone after the meshes are made)
        public static void CutModel(NativeList<BrushData> brushes, NativeList<BlobAssetReference<ChiselBrushRenderBuffer>> patched,
                                    NativeArray<HotspotTileData> tiles, NativeArray<HotspotLayoutData> layouts, NativeArray<HotspotLink> links,
                                    NativeArray<float4> tileSides = default, NativeArray<BlobAssetReference<ChiselBrushRenderBuffer>> kept = default,
                                    NativeArray<ulong> variants = default, NativeParallelHashMap<CompactNodeID, HotspotCutEntry> cache = default)
        {
            var state = HotspotCutState.Create(Allocator.Temp);
            try
            {
                PlanCut(brushes, tiles, layouts, links, tileSides, kept, variants, cache, ref state);
                var work = state.work.AsArray();
                for (int i = 0; i < work.Length; i++)
                    work[i] = CutPlanned(work[i], brushes.AsArray(), state.GroupOf, state.UnfoldOf, state.bordersOf, state.groups, state.lines,
                                         state.halfPlanes, state.cells, tileSides, layouts, variants);
                FinishCut(brushes, patched, state.work, cache);
            }
            finally
            {
                state.Dispose();
            }
        }

        // The model's groups (HotspotGroups), and the brushes to cut (work): a brush whose inputs are what they were when
        // the cache kept its cut is given that cut here
        public static void PlanCut(NativeList<BrushData> brushes, NativeArray<HotspotTileData> tiles, NativeArray<HotspotLayoutData> layouts,
                                   NativeArray<HotspotLink> links, NativeArray<float4> tileSides, NativeArray<BlobAssetReference<ChiselBrushRenderBuffer>> kept,
                                   NativeArray<ulong> variants, NativeParallelHashMap<CompactNodeID, HotspotCutEntry> cache, ref HotspotCutState state)
        {
            state.work.Clear();
            HotspotGroups.Borders(brushes, links, ref state.bordersOf);
            HotspotGroups.Find(brushes, tiles, layouts, links, state.bordersOf, ref state.GroupOf, ref state.UnfoldOf, state.groups, state.lines,
                               state.halfPlanes, state.cells, tileSides);
            PlanCutAfterGroups(brushes, layouts, tileSides, kept, variants, cache, ref state);
        }

        // The rest of PlanCut, once the model's groups are in state (HotspotGroups.Find, or Prepare, Match and Assemble)
        public static void PlanCutAfterGroups(NativeList<BrushData> brushes, NativeArray<HotspotLayoutData> layouts, NativeArray<float4> tileSides,
                                              NativeArray<BlobAssetReference<ChiselBrushRenderBuffer>> kept, NativeArray<ulong> variants,
                                              NativeParallelHashMap<CompactNodeID, HotspotCutEntry> cache, ref HotspotCutState state)
        {
            StartCutKeys(brushes, layouts, tileSides, kept, variants, cache.IsCreated, ref state);
            var keys = state.brushKeys.AsArray();
            var cuts = state.brushCuts.AsArray();
            for (int b = 0; b < brushes.Length; b++)
            {
                var content = default(uint4);
                cuts[b] = BrushCutKey(b, brushes.AsArray(), state.GroupOf, state.UnfoldOf, state.bordersOf, state.groupKeys.AsArray(), state.globalKey.Value,
                                      cache.IsCreated, false, ref content, out var key, out _);
                keys[b] = key;
            }
            FinishCutKeys(brushes, cache, ref state);
        }

        // The groups' ids and keys, told to each brush's own buffer, and room for each brush's key (BrushCutKey)
        public static void StartCutKeys(NativeList<BrushData> brushes, NativeArray<HotspotLayoutData> layouts, NativeArray<float4> tileSides,
                                        NativeArray<BlobAssetReference<ChiselBrushRenderBuffer>> kept, NativeArray<ulong> variants, bool cached,
                                        ref HotspotCutState state)
        {
            state.work.Clear();
            var groupOf = state.GroupOf;
            var groups  = state.groups;
            StableGroupIds(brushes, groupOf, groups);
            for (int b = 0; kept.IsCreated && b < kept.Length && b < brushes.Length; b++)
            {
                if (!kept[b].IsCreated)
                    continue;
                ref var surfaces = ref kept[b].Value.surfaces;
                for (int s = 0; s < surfaces.Length; s++)
                {
                    var grouped = groupOf.TryGetValue(new int2(b, s), out var g);
                    surfaces[s].hotspot.group           = grouped ? groups[g].info.group : 0;
                    surfaces[s].hotspot.groupVariations = grouped ? groups[g].info.variations : 0;
                }
            }
            state.groupKeys.ResizeUninitialized(cached ? groups.Length : 0);
            state.globalKey.Value = default;
            if (cached)
            {
                for (int g = 0; g < groups.Length; g++)
                    state.groupKeys[g] = HashGroup(groups[g], state.lines, state.halfPlanes, state.cells);
                state.globalKey.Value = HashInputs(layouts, tileSides, variants);
            }
            state.brushKeys.ResizeUninitialized(brushes.Length);
            state.brushCuts.ResizeUninitialized(brushes.Length);
        }

        // Whether a brush is cut (0 not, 1 keyed, 2 every time) and its key: what the cut reads, the brush as the weld left
        // it, its groups, the borders given by hand, the layouts. Each brush is independent of the others.
        // content: the brush's ContentHash, given (contentKept) or made here (made)
        public static byte BrushCutKey(int b, NativeArray<BrushData> brushes, UnsafeHashMap<int2, int> groupOf, UnsafeHashMap<int2, float4x4> unfoldOf,
                                       in UnsafeParallelMultiHashMap<int2, HotspotBorderCorner> bordersOf, NativeArray<uint4> groupKeys, uint4 globalKey,
                                       bool cached, bool contentKept, ref uint4 content, out uint4 key, out bool made)
        {
            key = default;
            made = false;
            if (!brushes[b].brushRenderBuffer.IsCreated ||
                (!HasSections(ref brushes[b].brushRenderBuffer.Value) && !InGroup(ref brushes[b].brushRenderBuffer.Value, b, groupOf)))
                return 0;
            if (!cached)
                return 2;
            if (!contentKept)
            {
                if (!ContentHash(brushes[b].brushRenderBuffer, out content))
                    return 2;
                made = true;
            }
            var rules = new NativeList<HotspotBorderCorner>(4, Allocator.Temp);
            BrushKey(brushes[b].brushRenderBuffer, b, content, groupOf, unfoldOf, bordersOf, groupKeys, globalKey, rules, out key);
            rules.Dispose();
            return 1;
        }

        // A brush's buffer, hashed without what the cut writes into it in place (each surface's group and its variations,
        // StartCutKeys): the same while the buffer is; false when it cannot be hashed
        public static unsafe bool ContentHash(BlobAssetReference<ChiselBrushRenderBuffer> source, out uint4 hash)
        {
            hash = default;
            if (!BlobBytes(source, out var data, out var length))
                return false;
            var state = new xxHash3.StreamingState(false);
            var zero  = 0;
            long at = 0;
            ref var surfaces = ref source.Value.surfaces;
            for (int s = 0; s < surfaces.Length; s++)
            {
                var group      = (byte*)UnsafeUtility.AddressOf(ref surfaces[s].hotspot.group) - data;
                var variations = (byte*)UnsafeUtility.AddressOf(ref surfaces[s].hotspot.groupVariations) - data;
                state.Update(data + at, (int)(group - at));
                state.Update(&zero, sizeof(int));
                at = group + sizeof(int);
                state.Update(data + at, (int)(variations - at));
                state.Update(&zero, sizeof(int));
                at = variations + sizeof(int);
            }
            state.Update(data + at, (int)(length - at));
            hash = state.DigestHash128();
            return true;
        }

        // HotspotGroups.VerifyIncremental: a kept content hash is the one the buffer has now
        public static void VerifyKeptContent(int b, NativeArray<BrushData> brushes, uint4 content)
        {
            if (!ContentHash(brushes[b].brushRenderBuffer, out var now) || !now.Equals(content))
                UnityEngine.Debug.LogError($"Kept hotspot content hash of brush {b} differs from its buffer's");
        }

        // The brushes to cut (work): a brush whose key is what it was when the cache kept its cut is given that cut here
        public static void FinishCutKeys(NativeList<BrushData> brushes, NativeParallelHashMap<CompactNodeID, HotspotCutEntry> cache, ref HotspotCutState state)
        {
            var cached = cache.IsCreated;
            for (int b = 0; b < brushes.Length; b++)
            {
                var nodeID = brushes[b].brushIndexOrder.compactNodeID;
                var cut    = state.brushCuts[b];
                if (cut == 0)
                {
                    if (cached && cache.TryGetValue(nodeID, out var stale))
                    {
                        if (stale.result.IsCreated)
                            stale.result.Dispose();
                        cache.Remove(nodeID);
                    }
                    continue;
                }
                var keyed = cut == 1;
                var key   = state.brushKeys[b];
                if (keyed && cache.TryGetValue(nodeID, out var entry) && entry.result.IsCreated && entry.key.Equals(key))
                {
                    entry.used = true;
                    cache[nodeID] = entry;
                    var data = brushes[b];
                    data.brushRenderBuffer = entry.result;
                    brushes[b] = data;
                    continue;
                }
                state.work.Add(new HotspotCutWork { brush = b, key = key, keyed = keyed });
            }
        }

        // Each group's id (HotspotSurfaceInfo.group): from its first member, so it stays while the group does and what was
        // made from the group is the same again
        static void StableGroupIds(NativeList<BrushData> brushes, UnsafeHashMap<int2, int> groupOf, NativeList<HotspotGroups.Group> groups)
        {
            var lowest = new NativeArray<uint>(groups.Length, Allocator.Temp);
            for (int g = 0; g < lowest.Length; g++)
                lowest[g] = uint.MaxValue;
            foreach (var pair in groupOf)
            {
                var b = pair.Key.x;
                if (b < 0 || b >= brushes.Length || pair.Value < 0 || pair.Value >= groups.Length)
                    continue;
                var id = math.hash(new int2(brushes[b].brushIndexOrder.compactNodeID.GetHashCode(), pair.Key.y));
                lowest[pair.Value] = math.min(lowest[pair.Value], id);
            }
            for (int g = 0; g < groups.Length; g++)
            {
                var group = groups[g];
                group.info.group = (int)((lowest[g] >> 1) | 1);
                groups[g] = group;
            }
            lowest.Dispose();
        }

        // One planned brush cut; each is independent of the others, so they can be cut at the same time
        public static HotspotCutWork CutPlanned(HotspotCutWork work, NativeArray<BrushData> brushes, UnsafeHashMap<int2, int> groupOf,
                                                UnsafeHashMap<int2, float4x4> unfoldOf, in UnsafeParallelMultiHashMap<int2, HotspotBorderCorner> bordersOf,
                                                NativeList<HotspotGroups.Group> groups, NativeList<HotspotSectionLine> lines,
                                                NativeList<HotspotSectionHalfPlane> halfPlanes, NativeList<HotspotSectionCell> cells,
                                                NativeArray<float4> tileSides, NativeArray<HotspotLayoutData> layouts, NativeArray<ulong> variants)
        {
            work.result = Cut(brushes[work.brush].brushRenderBuffer, work.brush, groupOf, unfoldOf, bordersOf, groups, lines, halfPlanes, cells,
                              tileSides, layouts, variants);
            return work;
        }

        // The cuts handed to the brushes: kept by the cache, or by patched to be freed after the meshes are made; then the
        // cache forgets the brushes this update did not cut
        public static void FinishCut(NativeList<BrushData> brushes, NativeList<BlobAssetReference<ChiselBrushRenderBuffer>> patched,
                                     NativeList<HotspotCutWork> work, NativeParallelHashMap<CompactNodeID, HotspotCutEntry> cache)
        {
            var cached = cache.IsCreated;
            for (int i = 0; i < work.Length; i++)
            {
                var item = work[i];
                var b = item.brush;
                if (item.keyed && cached)
                {
                    var nodeID = brushes[b].brushIndexOrder.compactNodeID;
                    if (cache.TryGetValue(nodeID, out var old) && old.result.IsCreated)
                        old.result.Dispose();
                    cache[nodeID] = new HotspotCutEntry { key = item.key, result = item.result, used = true };
                }
                else
                    patched.Add(item.result);
                var data = brushes[b];
                data.brushRenderBuffer = item.result;
                brushes[b] = data;
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

        // The size of a blob's data: it sits in the header in front of it (Unity.Entities BlobAssetHeader, 32 bytes, Length at 8)
        static unsafe bool BlobBytes(BlobAssetReference<ChiselBrushRenderBuffer> blob, out byte* data, out int length)
        {
            data = (byte*)blob.GetUnsafePtr();
            length = *(int*)(data - 32 + 8);
            return length > 0 && length < (1 << 30);
        }

        static unsafe uint4 Hash(void* data, long length) { return length == 0 ? default : xxHash3.Hash128(data, length); }

        // The group as the cut reads it: where its sections start in the model's flat lists changes with the other groups
        static unsafe uint4 HashGroup(HotspotGroups.Group group, NativeList<HotspotSectionLine> lines, NativeList<HotspotSectionHalfPlane> halfPlanes,
                                      NativeList<HotspotSectionCell> cells)
        {
            var start = group.sectionStart;
            var count = group.sectionCount;
            group.sectionStart = int3.zero;
            var parts = stackalloc uint4[4];
            parts[0] = Hash(&group, UnsafeUtility.SizeOf<HotspotGroups.Group>());
            parts[1] = Hash((byte*)lines.GetUnsafePtr()      + (long)start.x * UnsafeUtility.SizeOf<HotspotSectionLine>(),      (long)count.x * UnsafeUtility.SizeOf<HotspotSectionLine>());
            parts[2] = Hash((byte*)halfPlanes.GetUnsafePtr() + (long)start.y * UnsafeUtility.SizeOf<HotspotSectionHalfPlane>(), (long)count.y * UnsafeUtility.SizeOf<HotspotSectionHalfPlane>());
            parts[3] = Hash((byte*)cells.GetUnsafePtr()      + (long)start.z * UnsafeUtility.SizeOf<HotspotSectionCell>(),      (long)count.z * UnsafeUtility.SizeOf<HotspotSectionCell>());
            return Hash(parts, 4 * 16);
        }

        static unsafe uint4 HashInputs(NativeArray<HotspotLayoutData> layouts, NativeArray<float4> tileSides, NativeArray<ulong> variants)
        {
            var parts = stackalloc uint4[3];
            parts[0] = layouts.IsCreated   ? Hash(layouts.GetUnsafeReadOnlyPtr(),   (long)layouts.Length   * UnsafeUtility.SizeOf<HotspotLayoutData>()) : default;
            parts[1] = tileSides.IsCreated ? Hash(tileSides.GetUnsafeReadOnlyPtr(), (long)tileSides.Length * UnsafeUtility.SizeOf<float4>()) : default;
            parts[2] = variants.IsCreated  ? Hash(variants.GetUnsafeReadOnlyPtr(),  (long)variants.Length  * UnsafeUtility.SizeOf<ulong>()) : default;
            return Hash(parts, 3 * 16);
        }

        // One key for everything Cut reads for the brush; false when the brush cannot be keyed (it is cut every time then)
        static unsafe void BrushKey(BlobAssetReference<ChiselBrushRenderBuffer> source, int brush, uint4 content, UnsafeHashMap<int2, int> groupOf, UnsafeHashMap<int2, float4x4> unfoldOf,
                                    in UnsafeParallelMultiHashMap<int2, HotspotBorderCorner> bordersOf, NativeArray<uint4> groupKeys, uint4 globalKey,
                                    NativeList<HotspotBorderCorner> rules, out uint4 key)
        {
            var buffer = new UnsafeAppendBuffer(256, 16, Allocator.Temp);
            try
            {
                buffer.Add(content);
                buffer.Add(globalKey);
                ref var surfaces = ref source.Value.surfaces;
                for (int s = 0; s < surfaces.Length; s++)
                {
                    buffer.Add(surfaces[s].hotspot.group);
                    buffer.Add(surfaces[s].hotspot.groupVariations);
                    if (surfaces[s].indices.Length > 0 && groupOf.TryGetValue(new int2(brush, s), out var g))
                    {
                        buffer.Add(1);
                        buffer.Add(groupKeys[g]);
                        var unfolded = unfoldOf.TryGetValue(new int2(brush, s), out var unfold);
                        buffer.Add(unfolded);
                        if (unfolded)
                            buffer.Add(unfold);
                        continue;
                    }
                    HotspotGroups.BordersOf(new int2(brush, s), bordersOf, rules);
                    buffer.Add(2 + rules.Length);
                    for (int r = 0; r < rules.Length; r++)
                        buffer.Add(rules[r]);
                }
                key = xxHash3.Hash128(buffer.Ptr, buffer.Length);
            }
            finally
            {
                buffer.Dispose();
            }
        }

        static bool InGroup(ref ChiselBrushRenderBuffer buffer, int brush, UnsafeHashMap<int2, int> groupOf)
        {
            for (int s = 0; s < buffer.surfaces.Length; s++)
            {
                if (groupOf.ContainsKey(new int2(brush, s)))
                    return true;
            }
            return false;
        }

        static bool HasSections(ref ChiselBrushRenderBuffer buffer)
        {
            for (int s = 0; s < buffer.surfaces.Length; s++)
            {
                if (buffer.surfaces[s].sectionInput.count > 0 && buffer.surfaces[s].indices.Length > 0)
                    return true;
            }
            return false;
        }

        static BlobAssetReference<ChiselBrushRenderBuffer> Cut(BlobAssetReference<ChiselBrushRenderBuffer> source, int brush, UnsafeHashMap<int2, int> groupOf,
                                                               UnsafeHashMap<int2, float4x4> unfoldOf, in UnsafeParallelMultiHashMap<int2, HotspotBorderCorner> bordersOf,
                                                               NativeList<HotspotGroups.Group> groups, NativeList<HotspotSectionLine> groupLines,
                                                               NativeList<HotspotSectionHalfPlane> groupHalfPlanes, NativeList<HotspotSectionCell> groupCells,
                                                               NativeArray<float4> tileSides, NativeArray<HotspotLayoutData> layouts, NativeArray<ulong> variants)
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
                    if (from.indices.Length > 0 && groupOf.TryGetValue(new int2(brush, s), out var g))
                    {
                        var unfolded = unfoldOf.TryGetValue(new int2(brush, s), out var unfold);
                        // the group's material, when its layout has more than one
                        var group = groups[g];
                        if (group.sheetMaterial != 0)
                            to.destinationParameters.parameter1 = group.sheetMaterial;
                        else if (group.layout >= 0 && group.layout < layouts.Length)
                        {
                            // on the layout's own tiles: one of its materials, or its own back (a member's own match may
                            // have given it another sheet's)
                            var layout = layouts[group.layout];
                            if (variants.IsCreated && layout.variantCount > 1 && layout.variantStart + group.variant < variants.Length)
                                to.destinationParameters.parameter1 = variants[layout.variantStart + group.variant];
                            else if (layout.material != 0)
                                to.destinationParameters.parameter1 = layout.material;
                        }
                        MapInGroup(builder, ref from, ref to, groups[g], g, unfolded ? unfold : float4x4.identity, unfolded, groupLines, groupHalfPlanes, groupCells);
                        continue;
                    }
                    if (from.sectionInput.count == 0 || from.indices.Length == 0)
                    {
                        to.CopyGeometry(builder, ref from);
                        to.CopyHotspot(builder, ref from);
                        continue;
                    }

                    using var indices  = ToList(ref from.indices);
                    using var collider = ToList(ref from.colliderVertices);
                    using var select   = ToList(ref from.selectVertices);
                    using var render   = ToList(ref from.renderVertices);
                    var input    = from.sectionInput;
                    var budget   = input.MaxTriangles;
                    var sections = HotspotSections.Create(Allocator.Temp);
                    using var rules = new NativeList<HotspotBorderCorner>(4, Allocator.Temp);
                    HotspotGroups.BordersOf(new int2(brush, s), bordersOf, rules);
                    // around what the surface draws (its border, its interior), unless it is a triangle (exact already) or
                    // repeats
                    var built = input.count == 4 && !input.Repeated
                              ? sections.BuildConforming(input, indices, collider, rules.AsArray(), tileSides)
                              : sections.Build(input);
                    if (!built)
                    {
                        sections.Dispose();
                        to.CopyGeometry(builder, ref from);
                        to.CopyHotspot(builder, ref from);
                        continue;
                    }
                    var laid = CutAndMap(sections, input, input.right, input.up, true, indices, collider, select, render);
                    // over its budget: without the interior's repeats; still over: drawn as it was, uncut
                    var limited = indices.Length / 3 > budget;
                    if (limited)
                    {
                        Load(ref from, indices, collider, select, render);
                        laid = CutAndMap(sections, input, input.right, input.up, false, indices, collider, select, render);
                    }
                    sections.Dispose();
                    if (indices.Length / 3 > budget)
                    {
                        to.CopyGeometry(builder, ref from);
                        to.CopyHotspot(builder, ref from);
                        to.hotspot.flags |= HotspotSurfaceFlags.BudgetLimited;
                        continue;
                    }
                    // a surface whose cut went wrong is drawn uncut: one bad point would hide the whole model (its
                    // bounds)
                    if (!Finite(collider, render))
                    {
                        to.CopyGeometry(builder, ref from);
                        to.CopyHotspot(builder, ref from);
                        continue;
                    }
                    to.Store(builder, indices, collider, select, render);
                    // the collider and the lightmap keep what the weld left, uncut
                    to.shapeHashValue = from.shapeHashValue;
                    to.lightmapChart  = from.lightmapChart;
                    builder.Construct(ref to.physicsIndices,  ref from.indices);
                    builder.Construct(ref to.physicsVertices, ref from.colliderVertices);
                    to.CopySections(builder, ref from);
                    to.hotspot.repeats = laid;
                    if (limited)
                        to.hotspot.flags |= HotspotSurfaceFlags.BudgetLimited;
                }

                // The mesh queries' lists, with the cut surfaces' counts and hashes
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
                        // the render material the cut gave it (a group's sheet or variant), where the entry had the old one
                        if (entry.surfaceParameter != 0 && entry.surfaceParameter == original.surfaces[entry.surfaceIndex].destinationParameters.parameter1)
                            entry.surfaceParameter = stored.destinationParameters.parameter1;
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

        // A surface mapped with its group: cut with the group's sections (built from what the group draws together), or
        // given the group's tile as it is; one not in the group's first plane is unfolded into it for that (unfold), and
        // folded back, its own corners exactly where they were
        static void MapInGroup(BlobBuilder builder, ref ChiselSurfaceRenderBuffer from, ref ChiselSurfaceRenderBuffer to, in HotspotGroups.Group group, int groupIndex,
                               float4x4 unfold, bool unfolded,
                               NativeList<HotspotSectionLine> lines, NativeList<HotspotSectionHalfPlane> halfPlanes, NativeList<HotspotSectionCell> cells)
        {
            var indices  = ToList(ref from.indices);
            var collider = ToList(ref from.colliderVertices);
            var select   = ToList(ref from.selectVertices);
            var render   = ToList(ref from.renderVertices);
            var original = new UnsafeHashMap<float3, float3>(unfolded ? collider.Length : 1, Allocator.Temp);
            var budget   = group.input.MaxTriangles;
            var laid     = 0;
            var limited  = false;
            // with the interior's repeats; over the budget without them; still over, uncut on the group's bounds map
            for (int attempt = 0; attempt < 3; attempt++)
            {
                if (attempt > 0)
                {
                    Load(ref from, indices, collider, select, render);
                    original.Clear();
                }
                if (unfolded)
                {
                    for (int v = 0; v < collider.Length; v++)
                    {
                        var p = math.mul(unfold, new float4(collider[v], 1)).xyz;
                        original.TryAdd(p, collider[v]);
                        collider[v] = p;
                        var s = select[v]; s.position = p; select[v] = s;
                        var r = render[v]; r.position = p; render[v] = r;
                    }
                }
                if (group.sectionCount.z > 0 && attempt < 2)
                {
                    var sections = HotspotGroups.Sections(group, lines, halfPlanes, cells, Allocator.Temp);
                    laid = CutAndMap(sections, group.input, group.right, group.up, attempt == 0, indices, collider, select, render);
                    sections.Dispose();
                    if (indices.Length / 3 > budget)
                    {
                        limited = true;
                        continue;
                    }
                } else
                {
                    laid = 0;
                    MeshAlgorithms.ComputeUVs(render, group.uvMatrix);
                }
                break;
            }
            if (unfolded)
            {
                var back = math.inverse(unfold);
                for (int v = 0; v < collider.Length; v++)
                {
                    var p = original.TryGetValue(collider[v], out var exact) ? exact : math.mul(back, new float4(collider[v], 1)).xyz;
                    collider[v] = p;
                    var s = select[v]; s.position = p; select[v] = s;
                    var r = render[v]; r.position = p; render[v] = r;
                }
            }
            original.Dispose();
            MeshAlgorithms.ComputeTangents(indices, render);
            if (!Finite(collider, render))
            {
                to.CopyGeometry(builder, ref from);
                to.CopyHotspot(builder, ref from);
            } else
            {
                to.Store(builder, indices, collider, select, render);
                to.shapeHashValue = from.shapeHashValue;
                to.lightmapChart  = from.lightmapChart;
                builder.Construct(ref to.physicsIndices,  ref from.indices);
                builder.Construct(ref to.physicsVertices, ref from.colliderVertices);
                to.CopySections(builder, ref from);
                to.sectionInput = group.input;
                to.hotspot      = group.info;
                to.hotspot.repeats = laid;
                if (limited)
                    to.hotspot.flags |= HotspotSurfaceFlags.BudgetLimited;
            }
            indices.Dispose(); collider.Dispose(); select.Dispose(); render.Dispose();
        }

        // What a surface draws cut where its sections meet (once; triangle by triangle when that would not cover it once),
        // and again where its interior's repeats meet (withRepeats), and given its uvs; how many repeats were laid
        static int CutAndMap(in HotspotSections sections, in HotspotSectionInput input, float3 right, float3 up, bool withRepeats,
                             NativeList<int> indices, NativeList<float3> collider, NativeList<SelectVertex> select, NativeList<RenderVertex> render)
        {
            var normal = math.normalizesafe(math.cross(up, right));
            if (!sections.CutOnce(right, up, normal, indices, collider, select, render))
                sections.Split(right, up, normal, indices, collider, select, render);
            var repeats = withRepeats ? InteriorRepeats(sections, input, right, up, normal, indices, collider, select, render) : HotspotSections.Create(Allocator.Temp);
            sections.AssignUVs(indices, collider, select, render, right, up, repeats);
            var laid = repeats.cells.IsCreated ? repeats.cells.Length : 0;
            repeats.Dispose();
            return laid;
        }

        // A surface's lists as the weld left them, again
        static void Load(ref ChiselSurfaceRenderBuffer from, NativeList<int> indices, NativeList<float3> collider, NativeList<SelectVertex> select, NativeList<RenderVertex> render)
        {
            indices.Clear(); collider.Clear(); select.Clear(); render.Clear();
            for (int i = 0; i < from.indices.Length; i++)          indices.Add(from.indices[i]);
            for (int i = 0; i < from.colliderVertices.Length; i++) collider.Add(from.colliderVertices[i]);
            for (int i = 0; i < from.selectVertices.Length; i++)   select.Add(from.selectVertices[i]);
            for (int i = 0; i < from.renderVertices.Length; i++)   render.Add(from.renderVertices[i]);
        }

        // A bordered tile's interior repeated at the tile's density (BuildInteriorRepeats): what is drawn cut again, triangle
        // by triangle, where the repeats meet (the border's strips too, so no edge has a point on one side only)
        static HotspotSections InteriorRepeats(in HotspotSections sections, in HotspotSectionInput input, float3 right, float3 up, float3 normal,
                                               NativeList<int> indices, NativeList<float3> collider, NativeList<SelectVertex> select, NativeList<RenderVertex> render)
        {
            var repeats = HotspotSections.Create(Allocator.Temp);
            if (sections.interiorCell < 0 || !repeats.BuildInteriorRepeats(input, sections.openSides, sections.interiorToEdges))
            {
                repeats.cells.Clear();
                return repeats;
            }
            if (repeats.cells.Length > 1 && !repeats.CutOnce(right, up, normal, indices, collider, select, render, asTriangles: true))
                repeats.Split(right, up, normal, indices, collider, select, render);
            return repeats;
        }

        static bool Finite(NativeList<float3> positions, NativeList<RenderVertex> render)
        {
            for (int i = 0; i < positions.Length; i++)
            {
                if (!math.all(math.isfinite(positions[i])))
                    return false;
            }
            for (int i = 0; i < render.Length; i++)
            {
                if (!math.all(math.isfinite(render[i].position)) || !math.all(math.isfinite(render[i].uv0)))
                    return false;
            }
            return true;
        }

        static NativeList<T> ToList<T>(ref BlobArray<T> array) where T : unmanaged
        {
            var list = new NativeList<T>(array.Length, Allocator.Temp);
            for (int i = 0; i < array.Length; i++)
                list.Add(array[i]);
            return list;
        }
    }
}
