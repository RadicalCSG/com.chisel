using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using Debug = UnityEngine.Debug;
using ReadOnlyAttribute = Unity.Collections.ReadOnlyAttribute;

namespace Chisel.Core
{
    // What the last update grouped each mesh query's surfaces into (by SurfaceGroupKey), kept with the tree, so the next
    // update groups again only the surfaces of the brushes whose buffers changed (ChiselBrushRenderBuffer.serial). The lists
    // it makes are the ones PrepareSubSectionKeysJob, PrepareSubSectionsJob and ScatterSubSectionsJob make of every brush:
    // per query its groups in key order, each in brush order (Verify compares the two).
    internal struct MeshSectionMemory
    {
        public struct BrushSlot
        {
            public CompactNodeID id;
            // the serial of the buffer the slot's surfaces were grouped from
            public ulong serial;
            // where the brush is in this update's brushes, and the update that saw it last
            public int   brush;
            public int   stamp;
            public bool  alive;
        }

        // A surface in a group: its brush's slot, where it is in its brush's surfaces of the query, its lightmap area
        public struct Entry
        {
            public int   slot;
            public int   seq;
            public float area;
        }

        // The surfaces of a query with one key; a lightmap split query's go on to the final group of their cell (final)
        public struct FineGroup
        {
            public SurfaceGroupKey key;
            public int3  cell;
            public int   final;
            public bool  alive;
            public bool  dirty;
            // in brush order, with what their final group gives each (parameter, chunk, layer, decal stack) given
            public UnsafeList<SubMeshSurface> surfaces;
            public UnsafeList<Entry>          entries;
        }

        // The surfaces that go in one place of the query's list: one fine group's, or several merged in brush order
        public struct FinalGroup
        {
            public SurfaceGroupKey key;
            public bool  alive;
            public bool  dirty;
            // the stamp of the update that last changed its surfaces: the same version, the same surfaces
            public int   version;
            public UnsafeList<int>            fines;
            public UnsafeList<SubMeshSurface> merged;
        }

        public struct Query
        {
            public UnsafeHashMap<SurfaceGroupKey, int>          fineOf;
            public UnsafeList<FineGroup>                        fines;
            public UnsafeList<int>                              freeFines;
            // the fine groups each brush slot has surfaces in
            public UnsafeParallelMultiHashMap<int, int>         finesOfSlot;
            public UnsafeHashMap<SurfaceGroupKey, int>          finalOf;
            public UnsafeList<FinalGroup>                       finals;
            public UnsafeList<int>                              freeFinals;
            // a lightmap split query's fine groups by their 32 m cell (its key at level 0)
            public UnsafeParallelMultiHashMap<SurfaceGroupKey, int> finesOfCell;
            // the last list's final groups in their order: where each starts, which it is, its version
            public UnsafeList<int3>                             assembled;

            public static Query Create(Allocator allocator)
            {
                return new Query
                {
                    assembled   = new UnsafeList<int3>(256, allocator),
                    fineOf      = new UnsafeHashMap<SurfaceGroupKey, int>(256, allocator),
                    fines       = new UnsafeList<FineGroup>(256, allocator),
                    freeFines   = new UnsafeList<int>(16, allocator),
                    finesOfSlot = new UnsafeParallelMultiHashMap<int, int>(1024, allocator),
                    finalOf     = new UnsafeHashMap<SurfaceGroupKey, int>(256, allocator),
                    finals      = new UnsafeList<FinalGroup>(256, allocator),
                    freeFinals  = new UnsafeList<int>(16, allocator),
                    finesOfCell = new UnsafeParallelMultiHashMap<SurfaceGroupKey, int>(256, allocator)
                };
            }

            public void Dispose()
            {
                for (int f = 0; f < fines.Length; f++)
                {
                    if (fines[f].surfaces.IsCreated) fines[f].surfaces.Dispose();
                    if (fines[f].entries.IsCreated)  fines[f].entries.Dispose();
                }
                for (int f = 0; f < finals.Length; f++)
                {
                    if (finals[f].fines.IsCreated)  finals[f].fines.Dispose();
                    if (finals[f].merged.IsCreated) finals[f].merged.Dispose();
                }
                if (fineOf.IsCreated)      fineOf.Dispose();
                if (fines.IsCreated)       fines.Dispose();
                if (freeFines.IsCreated)   freeFines.Dispose();
                if (finesOfSlot.IsCreated) finesOfSlot.Dispose();
                if (finalOf.IsCreated)     finalOf.Dispose();
                if (finals.IsCreated)      finals.Dispose();
                if (freeFinals.IsCreated)  freeFinals.Dispose();
                if (finesOfCell.IsCreated) finesOfCell.Dispose();
                if (assembled.IsCreated)   assembled.Dispose();
            }

            /// <summary>The final group that starts at offset in the last list, or -1</summary>
            public int AssembledAt(int offset)
            {
                var lo = 0;
                var hi = assembled.Length - 1;
                while (lo <= hi)
                {
                    var mid = (lo + hi) >> 1;
                    var at  = assembled[mid].x;
                    if (at == offset)
                        return mid;
                    if (at < offset) lo = mid + 1; else hi = mid - 1;
                }
                return -1;
            }
        }

        // What a section's mesh was found to be made from (FindKeptSectionHashesJob), by what its surfaces are
        public struct SectionHashes
        {
            public uint4 section;
            public uint  lightmap;
            public bool  hashed;
        }

        /// <summary>Each update's lists are also made of every brush the way they were before, and any difference is
        /// logged as an error</summary>
        public static bool Verify;

        public NativeParallelHashMap<CompactNodeID, int> slotOf;
        public NativeList<BrushSlot>                     slots;
        public NativeList<int>                           freeSlots;
        public NativeList<Query>                         queries;
        public NativeReference<int>                      stamp;
        public NativeReference<bool>                     valid;
        // what the groups were made with: with another area budget everything is grouped again
        public NativeReference<float>                    lightmapAreaBudget;
        // whether the last update grouped every brush again, and how many brushes it grouped again
        public NativeReference<bool>                     lastFull;
        public NativeReference<int>                      lastChanged;
        // the last update's section hashes, by what each section's surfaces are (the queries, final groups and versions
        // its descriptions start at, and the settings the hashes are made with)
        public NativeParallelHashMap<uint4, SectionHashes> sectionHashes;
        Allocator allocator;

        public static MeshSectionMemory Create(Allocator allocator)
        {
            return new MeshSectionMemory
            {
                slotOf             = new NativeParallelHashMap<CompactNodeID, int>(1024, allocator),
                slots              = new NativeList<BrushSlot>(1024, allocator),
                freeSlots          = new NativeList<int>(64, allocator),
                queries            = new NativeList<Query>(16, allocator),
                stamp              = new NativeReference<int>(allocator),
                valid              = new NativeReference<bool>(allocator),
                lightmapAreaBudget = new NativeReference<float>(allocator),
                lastFull           = new NativeReference<bool>(allocator),
                lastChanged        = new NativeReference<int>(allocator),
                sectionHashes      = new NativeParallelHashMap<uint4, SectionHashes>(256, allocator),
                allocator          = allocator
            };
        }

        public bool IsCreated => slots.IsCreated;

        /// <summary>Forgets every group, for queryCount queries</summary>
        public void Reset(int queryCount)
        {
            for (int q = 0; q < queries.Length; q++)
                queries[q].Dispose();
            queries.Clear();
            for (int q = 0; q < queryCount; q++)
                queries.Add(Query.Create(allocator));
            slotOf.Clear();
            slots.Clear();
            freeSlots.Clear();
            valid.Value = false;
        }

        public void Dispose()
        {
            if (queries.IsCreated)
            {
                for (int q = 0; q < queries.Length; q++)
                    queries[q].Dispose();
                queries.Dispose();
            }
            if (slotOf.IsCreated)             slotOf.Dispose();
            if (slots.IsCreated)              slots.Dispose();
            if (freeSlots.IsCreated)          freeSlots.Dispose();
            if (stamp.IsCreated)              stamp.Dispose();
            if (valid.IsCreated)              valid.Dispose();
            if (lightmapAreaBudget.IsCreated) lightmapAreaBudget.Dispose();
            if (lastFull.IsCreated)           lastFull.Dispose();
            if (lastChanged.IsCreated)        lastChanged.Dispose();
            if (sectionHashes.IsCreated)      sectionHashes.Dispose();
        }
    }

    // A surface of a changed brush, keyed (MeshSections.KeysOf)
    struct KeyedSurface
    {
        public int             query;
        public SurfaceGroupKey key;
        public int3            cell;
        public float           area;
        public int             seq;
        public SubMeshSurface  surface;
    }

    static class MeshSections
    {
        /// <summary>
        /// The brushes to group again: those whose buffers are new or changed (changed, by brush), with the slots of those
        /// and of the brushes no longer there (touched, flagged in touchedSlot). Everything is grouped again when there are
        /// no groups yet, the queries or the area budget changed, brushes were reordered, or more than an eighth changed.
        /// </summary>
        public static void Prepare(NativeList<BrushData> brushes, ref MeshSectionMemory memory, int queryCount, float lightmapAreaBudget,
                                   NativeList<int> slotOfBrush, NativeList<int> changed, NativeList<int> touched, NativeList<bool> touchedSlot)
        {
            var count = brushes.Length;
            var full  = !memory.valid.Value || memory.queries.Length != queryCount || memory.lightmapAreaBudget.Value != lightmapAreaBudget;
            for (int pass = 0; pass < 2; pass++)
            {
                if (full)
                {
                    memory.Reset(queryCount);
                    memory.lightmapAreaBudget.Value = lightmapAreaBudget;
                }
                memory.valid.Value = false;
                var stamp = ++memory.stamp.Value;
                slotOfBrush.Resize(count, NativeArrayOptions.UninitializedMemory);
                changed.Clear();
                touched.Clear();
                var lastBrush  = -1;
                var reordered  = false;
                for (int b = 0; b < count; b++)
                {
                    var data   = brushes[b];
                    var id     = data.brushIndexOrder.compactNodeID;
                    var serial = data.brushRenderBuffer.IsCreated ? data.brushRenderBuffer.Value.serial : 0ul;
                    var isNew  = !memory.slotOf.TryGetValue(id, out var s);
                    if (isNew)
                    {
                        if (memory.freeSlots.Length > 0)
                        {
                            s = memory.freeSlots[memory.freeSlots.Length - 1];
                            memory.freeSlots.RemoveAt(memory.freeSlots.Length - 1);
                        } else
                        {
                            s = memory.slots.Length;
                            memory.slots.Add(default);
                        }
                        memory.slotOf.Add(id, s);
                        memory.slots[s] = new MeshSectionMemory.BrushSlot { id = id, alive = true, brush = -1 };
                    }
                    var slot = memory.slots[s];
                    if (!isNew)
                    {
                        // the groups keep their brushes in the order they had: another order groups everything again
                        reordered |= slot.brush < lastBrush;
                        lastBrush  = slot.brush;
                    }
                    var isChanged = isNew || serial == 0 || serial != slot.serial;
                    slot.serial = serial;
                    slot.brush  = b;
                    slot.stamp  = stamp;
                    memory.slots[s] = slot;
                    slotOfBrush[b]  = s;
                    if (isChanged)
                    {
                        changed.Add(b);
                        touched.Add(s);
                    }
                }
                if (!full && (reordered || changed.Length * 8 > count + 64))
                {
                    full = true;
                    continue;
                }
                // the brushes no longer there: their surfaces are taken out, their slots freed once that is done
                for (int s = 0; s < memory.slots.Length; s++)
                {
                    var slot = memory.slots[s];
                    if (slot.alive && slot.stamp != stamp)
                        touched.Add(s);
                }
                break;
            }
            touchedSlot.Resize(memory.slots.Length, NativeArrayOptions.ClearMemory);
            for (int i = 0; i < touched.Length; i++)
                touchedSlot[touched[i]] = true;
            memory.lastFull.Value    = full;
            memory.lastChanged.Value = changed.Length;
        }

        /// <summary>The keys of a changed brush's surfaces, query after query, written at changedIndex, as
        /// PrepareSubSectionKeysJob makes them</summary>
        public static void KeysOf(NativeArray<BrushData> brushes, NativeArray<MeshQuery> meshQueries, float lightmapAreaBudget, int brush, int changedIndex,
                                  ref NativeStream.Writer keyed)
        {
            var queryCount = meshQueries.Length;
            var data       = brushes[brush];
            var bufferRef  = data.brushRenderBuffer;
            ref var buffer = ref bufferRef.Value;
            var anyChunked = false;
            for (int t = 0; t < queryCount; t++)
                anyChunked |= PrepareSubSections.Chunked(meshQueries[t]);
            var chunk = anyChunked ? PrepareSubSections.ChunkOf(ref buffer) : 0;
            ref var querySurfaces = ref buffer.querySurfaces;
            keyed.BeginForEachIndex(changedIndex);
            for (int t = 0; t < queryCount; t++)
            {
                var query          = meshQueries[t];
                var chunked        = PrepareSubSections.Chunked(query);
                var surfaceChunked = PrepareSubSections.SurfaceChunked(query);
                var oneMaterial    = PrepareSubSections.OneMaterial(query);
                var split          = PrepareSubSections.LightmapSplit(query, lightmapAreaBudget);
                ref var surfaces   = ref querySurfaces[t].surfaces;
                var brushNodeID    = querySurfaces[t].brushNodeID;
                for (int s = 0; s < surfaces.Length; s++)
                {
                    SurfaceGroupKey key;
                    var cell = int3.zero;
                    ref var bufferSurface = ref buffer.surfaces[surfaces[s].surfaceIndex];
                    if (split && bufferSurface.decalStack == 0)
                    {
                        cell = PrepareSubSections.FineCellOf(bufferSurface.aabb);
                        key  = new SurfaceGroupKey { parameter = surfaces[s].surfaceParameter, layer = (byte)surfaces[s].colliderLayer, chunk = PrepareSubSections.ChunkOfCell(cell, 2) };
                    } else
                        key = PrepareSubSections.KeyOf(ref buffer, surfaces[s], chunk, chunked, surfaceChunked, oneMaterial);
                    keyed.Write(new KeyedSurface
                    {
                        query = t,
                        key  = key,
                        cell = cell,
                        area = split ? PrepareSubSections.LightmapArea(ref buffer, surfaces[s]) : 0,
                        seq  = s,
                        surface = new SubMeshSurface
                        {
                            brushNodeID       = brushNodeID,
                            surfaceIndex      = surfaces[s].surfaceIndex,
                            surfaceParameter  = surfaces[s].surfaceParameter,
                            decalStack        = bufferSurface.decalStack,
                            vertexCount       = surfaces[s].vertexCount,
                            indexCount        = surfaces[s].indexCount,
                            surfaceHashValue  = surfaces[s].surfaceHashValue,
                            geometryHashValue = surfaces[s].geometryHashValue,
                            brushRenderBuffer = bufferRef
                        }
                    });
                }
            }
            keyed.EndForEachIndex();
        }

        /// <summary>One query's groups made again where the touched brushes were and are: their surfaces taken out and the
        /// changed brushes' put in (keyed), each group changed put back in brush order, the final groups of the cells
        /// touched made again</summary>
        public static unsafe void Apply(ref MeshSectionMemory.Query q, int t, int queryCount, bool split, float lightmapAreaBudget, int stamp,
                                        NativeArray<MeshSectionMemory.BrushSlot> slots, NativeArray<bool> touchedSlot, NativeArray<int> touched,
                                        NativeArray<int> changed, NativeArray<int> slotOfBrush, NativeArray<KeyedSurface> keyed, NativeArray<int> keyedStart)
        {
            using var dirtyFines = new NativeList<int>(64, Allocator.Temp);

            // out: what the touched brushes had in this query
            for (int i = 0; i < touched.Length; i++)
            {
                var slot = touched[i];
                if (!q.finesOfSlot.TryGetFirstValue(slot, out var f, out var iterator))
                    continue;
                do MarkFine(ref q, f, dirtyFines); while (q.finesOfSlot.TryGetNextValue(out f, ref iterator));
                q.finesOfSlot.Remove(slot);
            }
            for (int d = 0; d < dirtyFines.Length; d++)
            {
                ref var fine = ref q.fines.ElementAt(dirtyFines[d]);
                var kept = 0;
                for (int e = 0; e < fine.entries.Length; e++)
                {
                    if (touchedSlot[fine.entries[e].slot])
                        continue;
                    fine.entries[kept]  = fine.entries[e];
                    fine.surfaces[kept] = fine.surfaces[e];
                    kept++;
                }
                fine.entries.Resize(kept);
                fine.surfaces.Resize(kept);
            }

            // in: the changed brushes' surfaces, in brush order
            using var finesOfBrush = new NativeList<int>(16, Allocator.Temp);
            for (int c = 0; c < changed.Length; c++)
            {
                var slot  = slotOfBrush[changed[c]];
                var range = c * queryCount + t;
                finesOfBrush.Clear();
                for (int n = keyedStart[range]; n < keyedStart[range + 1]; n++)
                {
                    var surface = keyed[n];
                    var f = FineOf(ref q, surface.key, surface.cell, split);
                    MarkFine(ref q, f, dirtyFines);
                    ref var fine = ref q.fines.ElementAt(f);
                    fine.surfaces.Add(surface.surface);
                    fine.entries.Add(new MeshSectionMemory.Entry { slot = slot, seq = surface.seq, area = surface.area });
                    if (!finesOfBrush.Contains(f))
                    {
                        finesOfBrush.Add(f);
                        q.finesOfSlot.Add(slot, f);
                    }
                }
            }

            // each group changed in brush order again; an empty one goes
            using var touchedFinals = new NativeList<int>(64, Allocator.Temp);
            using var touchedCells  = new NativeList<SurfaceGroupKey>(16, Allocator.Temp);
            for (int d = 0; d < dirtyFines.Length; d++)
            {
                var f = dirtyFines[d];
                ref var fine = ref q.fines.ElementAt(f);
                fine.dirty = false;
                if (fine.final >= 0)
                    MarkFinal(ref q, fine.final, touchedFinals);
                if (split)
                {
                    var cellKey = CellKey(fine.key, fine.cell, 0);
                    if (!touchedCells.Contains(cellKey))
                        touchedCells.Add(cellKey);
                }
                if (fine.entries.Length == 0)
                {
                    RemoveFine(ref q, f, split);
                    continue;
                }
                SortByBrush(ref fine.surfaces, ref fine.entries, slots);
            }

            // the final group of each fine group changed, or in a cell whose lightmap area changed
            if (!split)
            {
                for (int d = 0; d < dirtyFines.Length; d++)
                {
                    ref var fine = ref q.fines.ElementAt(dirtyFines[d]);
                    if (fine.alive && fine.final < 0)
                        SetFinal(ref q, dirtyFines[d], fine.key, touchedFinals);
                }
            } else
            {
                using var inCell = new NativeList<int>(64, Allocator.Temp);
                NativeHashMap<SurfaceGroupKey, float2> sums;
                using var _sums  = sums = new NativeHashMap<SurfaceGroupKey, float2>(16, Allocator.Temp);
                for (int c = 0; c < touchedCells.Length; c++)
                {
                    inCell.Clear();
                    sums.Clear();
                    if (q.finesOfCell.TryGetFirstValue(touchedCells[c], out var f, out var iterator))
                    {
                        do inCell.Add(f); while (q.finesOfCell.TryGetNextValue(out f, ref iterator));
                    }
                    // the fine groups in index order: the sums don't depend on the order the groups were made in
                    inCell.Sort();
                    for (int i = 0; i < inCell.Length; i++)
                    {
                        var fine = q.fines[inCell[i]];
                        for (int level = 0; level < 2; level++)
                        {
                            var key = CellKey(fine.key, fine.cell, level);
                            sums.TryGetValue(key, out var sum);
                            sums[key] = sum + new float2(AreaOf(fine), fine.entries.Length);
                        }
                    }
                    for (int i = 0; i < inCell.Length; i++)
                    {
                        var fine  = q.fines[inCell[i]];
                        var level = 2;
                        for (int coarse = 0; coarse < 2; coarse++)
                        {
                            var sum = sums[CellKey(fine.key, fine.cell, coarse)];
                            if (sum.x <= lightmapAreaBudget || sum.y <= 1)
                            {
                                level = coarse;
                                break;
                            }
                        }
                        SetFinal(ref q, inCell[i], CellKey(fine.key, fine.cell, level), touchedFinals);
                    }
                }
            }

            // each final group touched: its fine groups' surfaces given what it gives them, merged when there are several
            for (int i = 0; i < touchedFinals.Length; i++)
            {
                var g = touchedFinals[i];
                ref var final = ref q.finals.ElementAt(g);
                final.dirty   = false;
                final.version = stamp;
                if (final.fines.Length == 0)
                {
                    if (final.merged.IsCreated) final.merged.Dispose();
                    final.fines.Dispose();
                    final.alive = false;
                    q.finalOf.Remove(final.key);
                    q.freeFinals.Add(g);
                    continue;
                }
                var decals = final.key.parameter == ChiselDecalMeshes.kTransparentDecals;
                for (int m = 0; m < final.fines.Length; m++)
                {
                    ref var fine = ref q.fines.ElementAt(final.fines[m]);
                    for (int s = 0; s < fine.surfaces.Length; s++)
                    {
                        ref var surface = ref fine.surfaces.ElementAt(s);
                        surface.surfaceParameter = decals ? surface.surfaceParameter : final.key.parameter;
                        surface.chunk            = final.key.chunk;
                        surface.colliderLayer    = final.key.layer;
                        if (!decals)
                            surface.decalStack = 0;
                    }
                }
                if (final.fines.Length == 1)
                {
                    if (final.merged.IsCreated) final.merged.Dispose();
                    final.merged = default;
                    continue;
                }
                if (!final.merged.IsCreated)
                    final.merged = new UnsafeList<SubMeshSurface>(64, Allocator.Persistent);
                final.merged.Clear();
                using var entries = new UnsafeList<MeshSectionMemory.Entry>(64, Allocator.Temp);
                for (int m = 0; m < final.fines.Length; m++)
                {
                    var fine = q.fines[final.fines[m]];
                    final.merged.AddRange(fine.surfaces.Ptr, fine.surfaces.Length);
                    entries.AddRange(fine.entries.Ptr, fine.entries.Length);
                }
                var mergedEntries = entries;
                SortByBrush(ref final.merged, ref mergedEntries, slots);
            }
        }

        /// <summary>The keyed surfaces in one list, with where each changed brush's of each query start (keyedStart, at
        /// changedIndex * queryCount + query, and one more at the end)</summary>
        public static void Gather(NativeStream.Reader keyedStream, int changedCount, int queryCount, NativeList<KeyedSurface> keyed, NativeList<int> keyedStart)
        {
            keyed.Clear();
            keyedStart.Clear();
            for (int c = 0; c < changedCount; c++)
            {
                var found = keyedStream.BeginForEachIndex(c);
                var t = 0;
                for (int n = 0; n < found; n++)
                {
                    var surface = keyedStream.Read<KeyedSurface>();
                    for (; t <= surface.query; t++)
                        keyedStart.Add(keyed.Length);
                    keyed.Add(surface);
                }
                for (; t < queryCount; t++)
                    keyedStart.Add(keyed.Length);
                keyedStream.EndForEachIndex();
            }
            keyedStart.Add(keyed.Length);
        }

        /// <summary>One query's list: its final groups in key order, each in brush order</summary>
        public static unsafe UnsafeList<SubMeshSurface> Assemble(ref MeshSectionMemory.Query q, Allocator allocator)
        {
            using var order = new NativeList<int>(q.finals.Length, Allocator.Temp);
            var total = 0;
            for (int g = 0; g < q.finals.Length; g++)
            {
                var final = q.finals[g];
                if (!final.alive)
                    continue;
                order.Add(g);
                total += final.fines.Length == 1 ? q.fines[final.fines[0]].surfaces.Length : final.merged.Length;
            }
            order.Sort(new ByFinalKey { finals = q.finals.Ptr });
            var list = new UnsafeList<SubMeshSurface>(math.max(1, total), allocator);
            q.assembled.Clear();
            for (int i = 0; i < order.Length; i++)
            {
                var final = q.finals[order[i]];
                var from  = final.fines.Length == 1 ? q.fines[final.fines[0]].surfaces : final.merged;
                q.assembled.Add(new int3(list.Length, order[i], final.version));
                list.AddRange(from.Ptr, from.Length);
            }
            return list;
        }

        /// <summary>The slots of the brushes no longer there freed, and the memory taken as it is</summary>
        public static void Finish(ref MeshSectionMemory memory, NativeArray<int> touched)
        {
            var stamp = memory.stamp.Value;
            for (int i = 0; i < touched.Length; i++)
            {
                var s    = touched[i];
                var slot = memory.slots[s];
                if (!slot.alive || slot.stamp == stamp)
                    continue;
                memory.slotOf.Remove(slot.id);
                slot.alive = false;
                memory.slots[s] = slot;
                memory.freeSlots.Add(s);
            }
            memory.valid.Value = true;
        }

        unsafe struct ByFinalKey : System.Collections.Generic.IComparer<int>
        {
            [NativeDisableUnsafePtrRestriction] public MeshSectionMemory.FinalGroup* finals;
            public readonly int Compare(int x, int y) => finals[x].key.CompareTo(finals[y].key);
        }

        // A fine group's key at a level of its cell, as PrepareSubSectionsJob.SplitForLightmaps sums them
        static SurfaceGroupKey CellKey(SurfaceGroupKey key, int3 cell, int level)
        {
            return new SurfaceGroupKey { parameter = key.parameter, layer = key.layer, chunk = PrepareSubSections.ChunkOfCell(cell, level) };
        }

        static float AreaOf(MeshSectionMemory.FineGroup fine)
        {
            var area = 0f;
            for (int e = 0; e < fine.entries.Length; e++)
                area += fine.entries[e].area;
            return area;
        }

        static int FineOf(ref MeshSectionMemory.Query q, SurfaceGroupKey key, int3 cell, bool split)
        {
            if (q.fineOf.TryGetValue(key, out var f))
                return f;
            var fine = new MeshSectionMemory.FineGroup
            {
                key      = key,
                cell     = cell,
                final    = -1,
                alive    = true,
                surfaces = new UnsafeList<SubMeshSurface>(16, Allocator.Persistent),
                entries  = new UnsafeList<MeshSectionMemory.Entry>(16, Allocator.Persistent)
            };
            if (q.freeFines.Length > 0)
            {
                f = q.freeFines[q.freeFines.Length - 1];
                q.freeFines.RemoveAt(q.freeFines.Length - 1);
                q.fines[f] = fine;
            } else
            {
                f = q.fines.Length;
                q.fines.Add(fine);
            }
            q.fineOf.Add(key, f);
            if (split)
                q.finesOfCell.Add(CellKey(key, cell, 0), f);
            return f;
        }

        static void RemoveFine(ref MeshSectionMemory.Query q, int f, bool split)
        {
            ref var fine = ref q.fines.ElementAt(f);
            if (fine.final >= 0)
            {
                ref var final = ref q.finals.ElementAt(fine.final);
                var at = final.fines.IndexOf(f);
                if (at >= 0)
                    final.fines.RemoveAt(at);
            }
            if (split)
            {
                var cellKey = CellKey(fine.key, fine.cell, 0);
                if (q.finesOfCell.TryGetFirstValue(cellKey, out var other, out var iterator))
                {
                    do
                    {
                        if (other == f)
                        {
                            q.finesOfCell.Remove(iterator);
                            break;
                        }
                    } while (q.finesOfCell.TryGetNextValue(out other, ref iterator));
                }
            }
            q.fineOf.Remove(fine.key);
            fine.surfaces.Dispose();
            fine.entries.Dispose();
            fine.alive = false;
            fine.final = -1;
            q.freeFines.Add(f);
        }

        static void MarkFine(ref MeshSectionMemory.Query q, int f, NativeList<int> dirtyFines)
        {
            ref var fine = ref q.fines.ElementAt(f);
            if (fine.dirty)
                return;
            fine.dirty = true;
            dirtyFines.Add(f);
        }

        static void MarkFinal(ref MeshSectionMemory.Query q, int g, NativeList<int> touchedFinals)
        {
            ref var final = ref q.finals.ElementAt(g);
            if (final.dirty)
                return;
            final.dirty = true;
            touchedFinals.Add(g);
        }

        // puts a fine group in the final group of a key, taking it out of the one it was in
        static void SetFinal(ref MeshSectionMemory.Query q, int f, SurfaceGroupKey key, NativeList<int> touchedFinals)
        {
            ref var fine = ref q.fines.ElementAt(f);
            if (fine.final >= 0)
            {
                if (q.finals[fine.final].key.Equals(key))
                    return;
                ref var old = ref q.finals.ElementAt(fine.final);
                var at = old.fines.IndexOf(f);
                if (at >= 0)
                    old.fines.RemoveAt(at);
                MarkFinal(ref q, fine.final, touchedFinals);
            }
            if (!q.finalOf.TryGetValue(key, out var g))
            {
                var final = new MeshSectionMemory.FinalGroup { key = key, alive = true, fines = new UnsafeList<int>(2, Allocator.Persistent) };
                if (q.freeFinals.Length > 0)
                {
                    g = q.freeFinals[q.freeFinals.Length - 1];
                    q.freeFinals.RemoveAt(q.freeFinals.Length - 1);
                    q.finals[g] = final;
                } else
                {
                    g = q.finals.Length;
                    q.finals.Add(final);
                }
                q.finalOf.Add(key, g);
            }
            q.finals.ElementAt(g).fines.Add(f);
            fine.final = g;
            MarkFinal(ref q, g, touchedFinals);
        }

        struct BrushOrder : System.Collections.Generic.IComparer<int2>
        {
            public readonly int Compare(int2 x, int2 y) => x.x != y.x ? x.x.CompareTo(y.x) : x.y.CompareTo(y.y);
        }

        // surfaces and their entries in brush order, then in each brush's order
        static unsafe void SortByBrush(ref UnsafeList<SubMeshSurface> surfaces, ref UnsafeList<MeshSectionMemory.Entry> entries, NativeArray<MeshSectionMemory.BrushSlot> slots)
        {
            var count = entries.Length;
            if (count < 2)
                return;
            var sorted = true;
            var keys = new NativeArray<int2>(count, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
            for (int i = 0; i < count; i++)
            {
                keys[i] = new int2(slots[entries[i].slot].brush, entries[i].seq);
                if (i > 0 && (keys[i].x < keys[i - 1].x || (keys[i].x == keys[i - 1].x && keys[i].y < keys[i - 1].y)))
                    sorted = false;
            }
            if (!sorted)
            {
                // the positions sorted by their keys: (brush, seq) pairs are distinct, so the order is complete
                var byKey = new NativeArray<int4>(count, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
                for (int i = 0; i < count; i++)
                    byKey[i] = new int4(keys[i].x, keys[i].y, i, 0);
                byKey.Sort(new ByBrushThenSeq());
                var oldSurfaces = new NativeArray<SubMeshSurface>(count, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
                var oldEntries  = new NativeArray<MeshSectionMemory.Entry>(count, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
                UnsafeUtility.MemCpy(oldSurfaces.GetUnsafePtr(), surfaces.Ptr, count * (long)sizeof(SubMeshSurface));
                UnsafeUtility.MemCpy(oldEntries.GetUnsafePtr(), entries.Ptr, count * (long)sizeof(MeshSectionMemory.Entry));
                for (int i = 0; i < count; i++)
                {
                    surfaces[i] = oldSurfaces[byKey[i].z];
                    entries[i]  = oldEntries[byKey[i].z];
                }
                oldEntries.Dispose();
                oldSurfaces.Dispose();
                byKey.Dispose();
            }
            keys.Dispose();
        }

        struct ByBrushThenSeq : System.Collections.Generic.IComparer<int4>
        {
            public readonly int Compare(int4 x, int4 y) => x.x != y.x ? x.x.CompareTo(y.x) : x.y.CompareTo(y.y);
        }

        /// <summary>Whether two updates' lists are the same, surface by surface; the first difference</summary>
        public static bool Same(NativeArray<UnsafeList<SubMeshSurface>> a, NativeArray<UnsafeList<SubMeshSurface>> b, out int query, out int index)
        {
            query = index = -1;
            for (int t = 0; t < a.Length; t++)
            {
                var la = a[t];
                var lb = b[t];
                var length = math.min(la.Length, lb.Length);
                for (int i = 0; i < length; i++)
                {
                    var x = la[i];
                    var y = lb[i];
                    if (x.brushNodeID != y.brushNodeID || x.surfaceIndex != y.surfaceIndex || x.surfaceParameter != y.surfaceParameter ||
                        x.chunk != y.chunk || x.colliderLayer != y.colliderLayer || x.decalStack != y.decalStack ||
                        x.vertexCount != y.vertexCount || x.indexCount != y.indexCount ||
                        x.surfaceHashValue != y.surfaceHashValue || x.geometryHashValue != y.geometryHashValue ||
                        !x.brushRenderBuffer.Equals(y.brushRenderBuffer))
                    {
                        query = t;
                        index = i;
                        return false;
                    }
                }
                if (la.Length != lb.Length)
                {
                    query = t;
                    index = length;
                    return false;
                }
            }
            return true;
        }
    }
}
