using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Unity.Jobs;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

namespace Chisel.Core
{
    static class JobExtensions
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Run<T, U>(this T jobData, NativeList<U> list)
            where T : struct, IJobParallelForDefer
            where U : unmanaged
        {
            for (int index = 0; index < list.Length; index++)
                jobData.Execute(index);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static JobHandle Schedule<T, U>(this T jobData, bool runInParallel, NativeList<U> list, int innerloopBatchCount, JobHandle dependsOn = default)
            where T : struct, IJobParallelForDefer
            where U : unmanaged
        {
            CheckDependencies(runInParallel, dependsOn);
            if (runInParallel)
                return jobData.Schedule(list, innerloopBatchCount, dependsOn);

            dependsOn.Complete();
            jobData.Run(list);
            return default;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static JobHandle Schedule<T>(this T jobData, bool runInParallel, int arrayLength, int innerloopBatchCount, JobHandle dependsOn = default)
            where T : struct, IJobParallelFor
        {
            CheckDependencies(runInParallel, dependsOn);
            if (runInParallel)
                return jobData.Schedule(arrayLength, innerloopBatchCount, dependsOn);

            dependsOn.Complete();
            jobData.Run(arrayLength);
            return default;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static JobHandle Schedule<T>(this T jobData, bool runInParallel, JobHandle dependsOn = default)
            where T : struct, IJob
        {
            CheckDependencies(runInParallel, dependsOn);
            if (runInParallel)
                return jobData.Schedule(dependsOn);

            dependsOn.Complete();
            jobData.Run();
            return default;
        }

        #region CheckDependencies
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void CheckDependencies(bool runInParallel, JobHandle dependencies) { if (!runInParallel) dependencies.Complete(); }
        #endregion


        // Gather the deduplicated predecessor job-node ids and combine just their handles.
        internal static JobHandle ResolveDependencies(ref ReadJobHandles readDependencies, ref WriteJobHandles writeDependencies)
        {
            var predecessors = new FixedList512Bytes<int>();
            readDependencies.GatherPredecessors(ref predecessors);
            writeDependencies.GatherPredecessors(ref predecessors);
            return readDependencies.CombineNodes(in predecessors);
        }

        // Add this job as a graph node and record it as the last writer / a reader of its resources.
        internal static void RegisterNode(ref ReadJobHandles readDependencies, ref WriteJobHandles writeDependencies, JobHandle handle)
        {
            var node = readDependencies.AddNode(handle);
            writeDependencies.Register(node, handle);
            readDependencies.Register(node, handle);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static JobHandle Schedule<T>(this T jobData, bool runInParallel, ReadJobHandles readDependencies, WriteJobHandles writeDependencies)
            where T : struct, IJob
        {
            var dependencies = ResolveDependencies(ref readDependencies, ref writeDependencies);
            CheckDependencies(runInParallel, dependencies);
            var currentJobHandle = jobData.Schedule(runInParallel, dependencies);
            RegisterNode(ref readDependencies, ref writeDependencies, currentJobHandle);
            return currentJobHandle;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static JobHandle Schedule<T>(this T jobData, bool runInParallel, int arrayLength, int innerloopBatchCount, ReadJobHandles readDependencies, WriteJobHandles writeDependencies)
            where T : struct, IJobParallelFor
        {
            var dependencies = ResolveDependencies(ref readDependencies, ref writeDependencies);
            CheckDependencies(runInParallel, dependencies);
            var currentJobHandle = jobData.Schedule(runInParallel, arrayLength, innerloopBatchCount, dependencies);
            RegisterNode(ref readDependencies, ref writeDependencies, currentJobHandle);
            return currentJobHandle;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static JobHandle Schedule<T, U>(this T jobData, bool runInParallel, NativeList<U> list, int innerloopBatchCount, ReadJobHandles readDependencies, WriteJobHandles writeDependencies)
            where T : struct, IJobParallelForDefer
            where U : unmanaged
        {
            var dependencies = ResolveDependencies(ref readDependencies, ref writeDependencies);
            CheckDependencies(runInParallel, dependencies);
            var currentJobHandle = jobData.Schedule(runInParallel, list, innerloopBatchCount, dependencies);
            RegisterNode(ref readDependencies, ref writeDependencies, currentJobHandle);
            return currentJobHandle;
        }
    }

    public struct DualJobHandle
    {
        // The last writer's handle. A consumer that just wants "wait for writes to this resource" reads this.
        public JobHandle writeBarrier;

        // Reads since the last write, combined. Reset to default when a new write lands.
        public JobHandle reads;

        // "wait for all access to this resource" - last write + reads since. Used by dispose/finalize.
        public readonly JobHandle readWriteBarrier
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => JobHandle.CombineDependencies(writeBarrier, reads);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Complete() => readWriteBarrier.Complete();

        // Graph node id of the last writer, stored +1 so a zero-initialized slot means "no writer".
        int m_LastWriterPlus1;
        // Graph node ids of the readers since the last write (bounded by reads-between-writes).
        public FixedList512Bytes<int> readerNodes;

        public readonly bool HasWriter { [MethodImpl(MethodImplOptions.AggressiveInlining)] get => m_LastWriterPlus1 != 0; }
        public readonly int  LastWriter { [MethodImpl(MethodImplOptions.AggressiveInlining)] get => m_LastWriterPlus1 - 1; }

        // Record a read: a later writer must wait for it (node + handle), a later reader need not.
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AddReader(int node, JobHandle handle)
        {
            reads = JobHandle.CombineDependencies(reads, handle);
            readerNodes.Add(node);
        }

        // Record a write: it subsumes the resource's prior reads+writes, so collapse the node state to it.
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AddWriter(int node, JobHandle handle)
        {
            writeBarrier = handle;
            reads = default;
            m_LastWriterPlus1 = node + 1;
            readerNodes.Clear();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void MergeExternalWriter(int node, JobHandle handle)
        {
            // Demote the previous writer to a reader node so overwriting LastWriter does not drop the
            // edge to it - a later writer gathers LastWriter plus every reader node.
            if (HasWriter && !readerNodes.Contains(LastWriter))
                readerNodes.Add(LastWriter);
            writeBarrier = JobHandle.CombineDependencies(writeBarrier, handle);
            m_LastWriterPlus1 = node + 1;
            // reads / the remaining readerNodes are deliberately preserved.
        }
    }

    public struct ReadJobHandles
    {
        DualJobHandle[]        m_Array;
        List<JobHandle>        m_Nodes;
        FixedList512Bytes<int> m_Indices;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static ReadJobHandles Create(DualJobHandle[] array, List<JobHandle> nodes)
        {
            ReadJobHandles result;
            result.m_Array   = array;
            result.m_Nodes   = nodes;
            result.m_Indices = default;
            return result;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void Add(int index0) { m_Indices.Add(index0); }

        // Append a new job node (its handle) to the graph and return its id.
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal readonly int AddNode(JobHandle handle) { m_Nodes.Add(handle); return m_Nodes.Count - 1; }

        // A reader depends on the LAST WRITER of each resource it reads. Collect those node ids, deduped.
        internal readonly void GatherPredecessors(ref FixedList512Bytes<int> predecessors)
        {
            for (int i = 0; i < m_Indices.Length; i++)
            {
                ref readonly var res = ref m_Array[m_Indices[i]];
                if (res.HasWriter && !predecessors.Contains(res.LastWriter))
                    predecessors.Add(res.LastWriter);
            }
        }

        // Combine the handles of the (already deduplicated) predecessor nodes - one flat depth-1 combine.
        internal readonly JobHandle CombineNodes(in FixedList512Bytes<int> predecessors)
        {
            if (predecessors.Length == 0) return default;
            var acc = new JobHandleAccumulator(predecessors.Length, Allocator.Temp);
            for (int i = 0; i < predecessors.Length; i++)
                acc.Add(m_Nodes[predecessors[i]]);
            var handle = acc.Combine();
            acc.Dispose();
            return handle;
        }

        // Register this job's node as a reader of each resource it reads.
        internal readonly void Register(int node, JobHandle handle)
        {
            for (int i = 0; i < m_Indices.Length; i++)
                m_Array[m_Indices[i]].AddReader(node, handle);
        }
    }

    public struct WriteJobHandles
    {
        DualJobHandle[]        m_Array;
        List<JobHandle>        m_Nodes;
        FixedList512Bytes<int> m_Indices;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static WriteJobHandles Create(DualJobHandle[] array, List<JobHandle> nodes)
        {
            WriteJobHandles result;
            result.m_Array   = array;
            result.m_Nodes   = nodes;
            result.m_Indices = default;
            return result;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void Add(int index) { m_Indices.Add(index); }

        // A writer depends on the LAST WRITER and ALL READERS-SINCE of each resource it writes. Deduped.
        internal readonly void GatherPredecessors(ref FixedList512Bytes<int> predecessors)
        {
            for (int i = 0; i < m_Indices.Length; i++)
            {
                ref readonly var res = ref m_Array[m_Indices[i]];
                if (res.HasWriter && !predecessors.Contains(res.LastWriter))
                    predecessors.Add(res.LastWriter);
                for (int r = 0; r < res.readerNodes.Length; r++)
                    if (!predecessors.Contains(res.readerNodes[r]))
                        predecessors.Add(res.readerNodes[r]);
            }
        }

        // Register this job's node as the (new) last writer of each resource it writes.
        internal readonly void Register(int node, JobHandle handle)
        {
            for (int i = 0; i < m_Indices.Length; i++)
                m_Array[m_Indices[i]].AddWriter(node, handle);
        }
    }

    public struct JobHandleAccumulator : System.IDisposable
    {
        NativeList<JobHandle> m_Handles;

        public JobHandleAccumulator(int capacity, Allocator allocator) { m_Handles = new NativeList<JobHandle>(capacity, allocator); }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Add(JobHandle handle) { m_Handles.Add(handle); }

        public void AddDependency(JobHandle h0) { m_Handles.Add(h0); }
        public void AddDependency(JobHandle h0, JobHandle h1, JobHandle h2, JobHandle h3, JobHandle h4, JobHandle h5, JobHandle h6, JobHandle h7) { m_Handles.Add(h0); m_Handles.Add(h1); m_Handles.Add(h2); m_Handles.Add(h3); m_Handles.Add(h4); m_Handles.Add(h5); m_Handles.Add(h6); m_Handles.Add(h7); }

        // Flat depth-1 combine of everything collected so far.
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly JobHandle Combine() => m_Handles.Length == 0 ? default : JobHandle.CombineDependencies(m_Handles.AsArray());

        public void Dispose() { if (m_Handles.IsCreated) m_Handles.Dispose(); }
    }
}
