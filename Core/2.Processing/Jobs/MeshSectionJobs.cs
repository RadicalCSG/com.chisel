using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Debug = UnityEngine.Debug;
using ReadOnlyAttribute = Unity.Collections.ReadOnlyAttribute;

namespace Chisel.Core
{
    // The jobs that group the queries' surfaces with the tree's memory (MeshSections); with MeshSectionMemory.Verify also
    // the jobs that group every brush as before, and the two compared
    static class MeshSectionJobs
    {
        // Disposes what it made itself with the handle it returns
        public static JobHandle Schedule(bool runInParallel, NativeArray<MeshQuery> meshQueries, NativeList<BrushData> brushRenderData, float lightmapAreaBudget,
                                         MeshSectionMemory memory, NativeArray<UnsafeList<SubMeshSurface>> subMeshSurfaces, Allocator allocator, JobHandle dependsOn)
        {
            var queryCount  = meshQueries.Length;
            var slotOfBrush = new NativeList<int>(Allocator.TempJob);
            var changed     = new NativeList<int>(Allocator.TempJob);
            var touched     = new NativeList<int>(Allocator.TempJob);
            var touchedSlot = new NativeList<bool>(Allocator.TempJob);
            var keyed       = new NativeList<KeyedSurface>(Allocator.TempJob);
            var keyedStart  = new NativeList<int>(Allocator.TempJob);
            var handle = new PrepareMeshSectionsJob
            {
                brushRenderData    = brushRenderData,
                memory             = memory,
                queryCount         = queryCount,
                lightmapAreaBudget = lightmapAreaBudget,
                slotOfBrush        = slotOfBrush,
                changed            = changed,
                touched            = touched,
                touchedSlot        = touchedSlot
            }.Schedule(runInParallel, dependsOn);
            handle = NativeCollection.ScheduleConstruct(runInParallel, out var keyedStream, changed, Allocator.TempJob, handle);
            handle = new MeshSectionKeysJob
            {
                brushRenderData    = brushRenderData.AsDeferredJobArray(),
                meshQueries        = meshQueries,
                lightmapAreaBudget = lightmapAreaBudget,
                changed            = changed.AsDeferredJobArray(),
                keyed              = keyedStream.AsWriter()
            }.Schedule(runInParallel, changed, 4, handle);
            handle = new GatherMeshSectionKeysJob
            {
                keyedStream = keyedStream.AsReader(),
                changed     = changed,
                queryCount  = queryCount,
                keyed       = keyed,
                keyedStart  = keyedStart
            }.Schedule(runInParallel, handle);
            handle = new ApplyMeshSectionsJob
            {
                queries            = memory.queries.AsDeferredJobArray(),
                meshQueries        = meshQueries,
                lightmapAreaBudget = lightmapAreaBudget,
                stamp              = memory.stamp,
                slots              = memory.slots.AsDeferredJobArray(),
                touchedSlot        = touchedSlot.AsDeferredJobArray(),
                touched            = touched.AsDeferredJobArray(),
                changed            = changed.AsDeferredJobArray(),
                slotOfBrush        = slotOfBrush.AsDeferredJobArray(),
                keyed              = keyed.AsDeferredJobArray(),
                keyedStart         = keyedStart.AsDeferredJobArray()
            }.Schedule(runInParallel, queryCount, 1, handle);
            handle = new AssembleMeshSectionsJob
            {
                queries         = memory.queries.AsDeferredJobArray(),
                allocator       = allocator,
                subMeshSurfaces = subMeshSurfaces
            }.Schedule(runInParallel, queryCount, 1, handle);
            handle = new FinishMeshSectionsJob { memory = memory, touched = touched }.Schedule(runInParallel, handle);

            if (MeshSectionMemory.Verify)
            {
                var reference   = new NativeArray<UnsafeList<SubMeshSurface>>(queryCount, Allocator.TempJob);
                var rangeGroups = new NativeArray<PrepareSubSections.RangeGroups>(PrepareSubSections.kRangeCount * queryCount, Allocator.TempJob);
                var rangeChunks = new NativeArray<UnsafeList<uint>>(PrepareSubSections.kRangeCount, Allocator.TempJob);
                handle = new PrepareSubSectionKeysJob
                {
                    meshQueries        = meshQueries,
                    brushRenderData    = brushRenderData,
                    allocator          = Allocator.TempJob,
                    ranges             = rangeGroups,
                    rangeChunks        = rangeChunks,
                    lightmapAreaBudget = lightmapAreaBudget
                }.Schedule(runInParallel, PrepareSubSections.kRangeCount, 1, handle);
                handle = new PrepareSubSectionsJob
                {
                    meshQueries        = meshQueries,
                    lightmapAreaBudget = lightmapAreaBudget,
                    allocator          = Allocator.TempJob,
                    ranges             = rangeGroups,
                    subMeshSurfaces    = reference
                }.Schedule(runInParallel, queryCount, 1, handle);
                handle = new ScatterSubSectionsJob
                {
                    meshQueries     = meshQueries,
                    brushRenderData = brushRenderData,
                    subMeshSurfaces = reference,
                    ranges          = rangeGroups,
                    rangeChunks     = rangeChunks
                }.Schedule(runInParallel, PrepareSubSections.kRangeCount, 1, handle);
                handle = new CompareMeshSectionsJob { made = subMeshSurfaces, reference = reference }.Schedule(runInParallel, handle);
                handle = JobHandle.CombineDependencies(reference.Dispose(handle), rangeGroups.Dispose(handle), rangeChunks.Dispose(handle));
            }

            var disposed = new NativeArray<JobHandle>(7, Allocator.Temp);
            disposed[0] = slotOfBrush.Dispose(handle);
            disposed[1] = changed.Dispose(handle);
            disposed[2] = touched.Dispose(handle);
            disposed[3] = touchedSlot.Dispose(handle);
            disposed[4] = keyed.Dispose(handle);
            disposed[5] = keyedStart.Dispose(handle);
            disposed[6] = keyedStream.Dispose(handle);
            var all = JobHandle.CombineDependencies(disposed);
            disposed.Dispose();
            return all;
        }
    }

    [BurstCompile(CompileSynchronously = true)]
    struct PrepareMeshSectionsJob : IJob
    {
        [NoAlias, ReadOnly] public NativeList<BrushData> brushRenderData;
        public MeshSectionMemory memory;
        public int   queryCount;
        public float lightmapAreaBudget;
        [NoAlias] public NativeList<int>  slotOfBrush;
        [NoAlias] public NativeList<int>  changed;
        [NoAlias] public NativeList<int>  touched;
        [NoAlias] public NativeList<bool> touchedSlot;

        public void Execute()
        {
            MeshSections.Prepare(brushRenderData, ref memory, queryCount, lightmapAreaBudget, slotOfBrush, changed, touched, touchedSlot);
        }
    }

    [BurstCompile(CompileSynchronously = true)]
    struct MeshSectionKeysJob : IJobParallelForDefer
    {
        [NoAlias, ReadOnly] public NativeArray<BrushData> brushRenderData;
        [NoAlias, ReadOnly] public NativeArray<MeshQuery> meshQueries;
        public float lightmapAreaBudget;
        [NoAlias, ReadOnly] public NativeArray<int> changed;
        [NoAlias] public NativeStream.Writer keyed;

        public void Execute(int index)
        {
            MeshSections.KeysOf(brushRenderData, meshQueries, lightmapAreaBudget, changed[index], index, ref keyed);
        }
    }

    [BurstCompile(CompileSynchronously = true)]
    struct GatherMeshSectionKeysJob : IJob
    {
        [NoAlias, ReadOnly] public NativeStream.Reader keyedStream;
        [NoAlias, ReadOnly] public NativeList<int> changed;
        public int queryCount;
        [NoAlias] public NativeList<KeyedSurface> keyed;
        [NoAlias] public NativeList<int> keyedStart;

        public void Execute()
        {
            MeshSections.Gather(keyedStream, changed.Length, queryCount, keyed, keyedStart);
        }
    }

    // Each query on its own
    [BurstCompile(CompileSynchronously = true)]
    unsafe struct ApplyMeshSectionsJob : IJobParallelFor
    {
        [NativeDisableParallelForRestriction] [NoAlias] public NativeArray<MeshSectionMemory.Query> queries;
        [NoAlias, ReadOnly] public NativeArray<MeshQuery> meshQueries;
        public float lightmapAreaBudget;
        [NoAlias, ReadOnly] public NativeReference<int> stamp;
        [NoAlias, ReadOnly] public NativeArray<MeshSectionMemory.BrushSlot> slots;
        [NoAlias, ReadOnly] public NativeArray<bool> touchedSlot;
        [NoAlias, ReadOnly] public NativeArray<int>  touched;
        [NoAlias, ReadOnly] public NativeArray<int>  changed;
        [NoAlias, ReadOnly] public NativeArray<int>  slotOfBrush;
        [NoAlias, ReadOnly] public NativeArray<KeyedSurface> keyed;
        [NoAlias, ReadOnly] public NativeArray<int>  keyedStart;

        public void Execute(int t)
        {
            ref var query = ref UnsafeUtility.ArrayElementAsRef<MeshSectionMemory.Query>(queries.GetUnsafePtr(), t);
            MeshSections.Apply(ref query, t, meshQueries.Length, PrepareSubSections.LightmapSplit(meshQueries[t], lightmapAreaBudget), lightmapAreaBudget,
                               stamp.Value, slots, touchedSlot, touched, changed, slotOfBrush, keyed, keyedStart);
        }
    }

    // Each query's list on its own
    [BurstCompile(CompileSynchronously = true)]
    unsafe struct AssembleMeshSectionsJob : IJobParallelFor
    {
        [NativeDisableParallelForRestriction] [NoAlias] public NativeArray<MeshSectionMemory.Query> queries;
        public Allocator allocator;
        [NoAlias] public NativeArray<UnsafeList<SubMeshSurface>> subMeshSurfaces;

        public void Execute(int t)
        {
            if (subMeshSurfaces[t].IsCreated)
                subMeshSurfaces[t].Dispose();
            ref var query = ref UnsafeUtility.ArrayElementAsRef<MeshSectionMemory.Query>(queries.GetUnsafePtr(), t);
            subMeshSurfaces[t] = MeshSections.Assemble(ref query, allocator);
        }
    }

    [BurstCompile(CompileSynchronously = true)]
    struct FinishMeshSectionsJob : IJob
    {
        public MeshSectionMemory memory;
        [NoAlias, ReadOnly] public NativeList<int> touched;

        public void Execute()
        {
            MeshSections.Finish(ref memory, touched.AsArray());
        }
    }

    // The lists made with the memory against the ones made of every brush; the latter freed
    [BurstCompile(CompileSynchronously = true)]
    struct CompareMeshSectionsJob : IJob
    {
        [NoAlias, ReadOnly] public NativeArray<UnsafeList<SubMeshSurface>> made;
        [NoAlias] public NativeArray<UnsafeList<SubMeshSurface>> reference;

        public void Execute()
        {
            if (!MeshSections.Same(made, reference, out var query, out var index))
                Debug.LogError($"The mesh sections made with the memory differ from those made of every brush: query {query}, surface {index} ({made[query].Length} and {reference[query].Length})");
            for (int t = 0; t < reference.Length; t++)
            {
                if (reference[t].IsCreated)
                    reference[t].Dispose();
            }
        }
    }
}
