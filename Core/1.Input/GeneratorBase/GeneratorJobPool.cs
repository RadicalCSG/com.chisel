using System;
using System.Linq;
using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;
using Unity.Jobs;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;
using UnityEngine.Profiling;
using ReadOnlyAttribute = Unity.Collections.ReadOnlyAttribute;
using WriteOnlyAttribute = Unity.Collections.WriteOnlyAttribute;
using Unity.Entities;

namespace Chisel.Core
{
    public struct GeneratedNodeDefinition
    {
        public int              hierarchyIndex;         // index to the hierarchy (model) our node lives on
        public CompactNodeID    parentCompactNodeID;    // node ID of the parent of this node
        public int              siblingIndex;           // the sibling index of this node, relative to other children of our parent
        public CompactNodeID    compactNodeID;          // the node ID of this node

        public CSGOperationType operation;              // the type of CSG operation of this node
        public float4x4         transformation;         // the transformation of this node
        public int              brushMeshHash;          // the hash of the brush-mesh (which is also the ID we lookup meshes with) this node uses (if any)
    }

    // TODO: move to core
    [BurstCompile(CompileSynchronously = true)]
    public class GeneratorJobPoolManager : System.IDisposable
    {
        System.Collections.Generic.HashSet<IGeneratorJobPool> generatorPools = new();

        static GeneratorJobPoolManager s_Instance;
        public static GeneratorJobPoolManager Instance => (s_Instance ??= new GeneratorJobPoolManager());

        public static bool Register  (IGeneratorJobPool pool) { return Instance.generatorPools.Add(pool); }
        public static bool Unregister(IGeneratorJobPool pool) { return Instance.generatorPools.Remove(pool); }

#if UNITY_EDITOR
        [UnityEditor.InitializeOnLoadMethod]
        public static void Init()
        {
            UnityEditor.AssemblyReloadEvents.beforeAssemblyReload -= OnAssemblyReload;
            UnityEditor.AssemblyReloadEvents.beforeAssemblyReload += OnAssemblyReload;
        }
         
        private static void OnAssemblyReload()
        {
            s_Instance?.Dispose();
            s_Instance = null;
        }
#endif

        static JobHandle s_PreviousJobHandle = default;

        public static void Clear() 
        {
            s_PreviousJobHandle.Complete();
            var allGeneratorPools = Instance.generatorPools;
            foreach (var pool in allGeneratorPools)
                pool.AllocateOrClear();
        }

        [BurstCompile(CompileSynchronously = true)]
        struct ResizeTempListsJob : IJob
        {
            [NoAlias, ReadOnly] public NativeArray<int> totalCounts;

            [NoAlias, WriteOnly] public NativeList<GeneratedNodeDefinition>             generatedNodeDefinitions;
            [NoAlias, WriteOnly] public NativeList<BlobAssetReference<BrushMeshBlob>>   brushMeshBlobs;

            public void Execute()
            {
                var totalCount = 0;
                for (int i = 0; i < totalCounts.Length; i++)
                    totalCount += totalCounts[i];
                generatedNodeDefinitions.Capacity = totalCount;
                brushMeshBlobs.Capacity = totalCount;
            }
        }

        const Allocator defaultAllocator = Allocator.TempJob;

		readonly static List<IGeneratorJobPool> s_GeneratorJobs = new List<IGeneratorJobPool>();

        // TODO: Optimize this
        public static JobHandle ScheduleJobs(bool runInParallel, JobHandle dependsOn = default)
        {
            var hierarchyList = CompactHierarchyManager.HierarchyList;

            var combinedJobHandle = (JobHandle)default;
            var allGeneratorPools = Instance.generatorPools;
            Profiler.BeginSample("GenPool_Generate");
            s_GeneratorJobs.Clear();
            foreach (var pool in allGeneratorPools)
            {
                if (pool.HasJobs)
                    s_GeneratorJobs.Add(pool);
            }
            foreach (var pool in s_GeneratorJobs)
                combinedJobHandle = JobHandle.CombineDependencies(combinedJobHandle, pool.ScheduleGenerateJob(runInParallel, dependsOn));
            Profiler.EndSample();

            NativeList<GeneratedNodeDefinition> generatedNodeDefinitions = default;
            NativeList<BlobAssetReference<BrushMeshBlob>> brushMeshBlobs = default;
            NativeArray<int> totalCounts = default;
            var lastJobHandle = combinedJobHandle;
            var allocateJobHandle = default(JobHandle);
            try
            {
                Profiler.BeginSample("GenPool_UpdateHierarchy");
                totalCounts = new NativeArray<int>(s_GeneratorJobs.Count, defaultAllocator);
                int index = 0;
                foreach (var pool in s_GeneratorJobs)
                {
                    lastJobHandle = pool.ScheduleUpdateHierarchyJob(runInParallel, lastJobHandle, index, totalCounts);
                    index++;
                }
                Profiler.EndSample();

                Profiler.BeginSample("GenPool_Allocator"); 
                generatedNodeDefinitions    = new NativeList<GeneratedNodeDefinition>(defaultAllocator);
                brushMeshBlobs              = new NativeList<BlobAssetReference<BrushMeshBlob>>(defaultAllocator);
                var resizeTempLists = new ResizeTempListsJob
                {
                    // Read
                    totalCounts                 = totalCounts,

                    // Read / Write
                    generatedNodeDefinitions    = generatedNodeDefinitions,
                    brushMeshBlobs              = brushMeshBlobs
                };
                allocateJobHandle = resizeTempLists.Schedule(runInParallel, lastJobHandle);
                Profiler.EndSample();
            
                Profiler.BeginSample("GenPool_Complete");
                allocateJobHandle.Complete(); // TODO: get rid of this somehow
                Profiler.EndSample();

                Profiler.BeginSample("GenPool_Schedule");
                lastJobHandle = default;
                combinedJobHandle = allocateJobHandle;
                foreach (var pool in s_GeneratorJobs)
                {
                    var scheduleJobHandle = pool.ScheduleInitializeArraysJob(runInParallel, 
                                                                             // Read
                                                                             hierarchyList, 
                                                                             // Write
                                                                             generatedNodeDefinitions,
                                                                             brushMeshBlobs,
                                                                             // Dependency
                                                                             JobHandle.CombineDependencies(allocateJobHandle, lastJobHandle));
                    lastJobHandle = scheduleJobHandle;
                    combinedJobHandle = JobHandle.CombineDependencies(combinedJobHandle, scheduleJobHandle);
                }

                lastJobHandle = JobHandle.CombineDependencies(allocateJobHandle, combinedJobHandle);
                lastJobHandle = BrushMeshManager.ScheduleBrushRegistration(runInParallel, brushMeshBlobs, generatedNodeDefinitions, lastJobHandle);
                lastJobHandle = ScheduleAssignMeshesJob(runInParallel, hierarchyList, generatedNodeDefinitions, lastJobHandle);
                s_PreviousJobHandle = lastJobHandle;
                Profiler.EndSample();
            }
            finally
            {
                Profiler.BeginSample("GenPool_Dispose");
                var totalCountsDisposeJobHandle                 = totalCounts.Dispose(allocateJobHandle);
                totalCounts = default;
                
                var generatedNodeDefinitionsDisposeJobHandle    = generatedNodeDefinitions.Dispose(lastJobHandle);
                generatedNodeDefinitions = default;
                
                var brushMeshBlobsDisposeJobHandle              = brushMeshBlobs.Dispose(lastJobHandle);
                brushMeshBlobs = default;
                
                var allDisposes = JobHandle.CombineDependencies(totalCountsDisposeJobHandle,
                                                                generatedNodeDefinitionsDisposeJobHandle,
                                                                brushMeshBlobsDisposeJobHandle);
				lastJobHandle = JobHandle.CombineDependencies(allDisposes, lastJobHandle);
				s_PreviousJobHandle = lastJobHandle;

				Profiler.EndSample();
            }

            return lastJobHandle;
        }
        
        [BurstCompile(CompileSynchronously = true)]
        struct HierarchySortJob : IJob
        {
            [NoAlias] public NativeList<GeneratedNodeDefinition>    generatedNodeDefinitions;

            struct GeneratedNodeDefinitionSorter : IComparer<GeneratedNodeDefinition>
            {
                public readonly int Compare(GeneratedNodeDefinition x, GeneratedNodeDefinition y)
                {
                    var x_hierarchyID = x.hierarchyIndex;
                    var y_hierarchyID = y.hierarchyIndex;

                    if (x_hierarchyID != y_hierarchyID)
                        return x_hierarchyID - y_hierarchyID;
                    
                    var x_parentCompactNodeID = x.parentCompactNodeID.slotIndex.index;
                    var y_parentCompactNodeID = y.parentCompactNodeID.slotIndex.index;
                    if (x_parentCompactNodeID != y_parentCompactNodeID)
                        return x_parentCompactNodeID - y_parentCompactNodeID;

                    var x_siblingIndex = x.siblingIndex;
                    var y_siblingIndex = y.siblingIndex;
                    if (x_siblingIndex != y_siblingIndex)
                        return x_siblingIndex - y_siblingIndex;

                    var x_compactNodeID = x.compactNodeID.slotIndex.index;
                    var y_compactNodeID = y.compactNodeID.slotIndex.index;
                    if (x_compactNodeID != y_compactNodeID)
                        return x_compactNodeID - y_compactNodeID;
                    return 0;
                }
            }

			readonly static GeneratedNodeDefinitionSorter kGeneratedNodeDefinitionSorter = new();

            public void Execute()
            {
                generatedNodeDefinitions.Sort(kGeneratedNodeDefinitionSorter);
            }
        }


        [BurstCompile(CompileSynchronously = true)]
        unsafe struct AssignMeshesJob : IJob
        {          
            // TODO: get rid of this
            public void InitializeLookups()
            {
                hierarchyIDLookupPtr = (SlotIndexMap*)UnsafeUtility.AddressOf(ref CompactHierarchyManager.HierarchyIDLookup);
                nodeIDLookupPtr = (SlotIndexMap*)UnsafeUtility.AddressOf(ref CompactHierarchyManager.NodeIDLookup);
                nodesLookup = CompactHierarchyManager.Nodes;
            }

            // Read
            [NoAlias, ReadOnly] public NativeList<GeneratedNodeDefinition>      generatedNodeDefinitions;

            // Read/Write
            [NoAlias] public PointerReference<SlotIndexMap>                        hierarchyIDLookupRef;
            [NativeDisableUnsafePtrRestriction, NoAlias] public SlotIndexMap*      hierarchyIDLookupPtr;
            [NativeDisableUnsafePtrRestriction, NoAlias] public SlotIndexMap*      nodeIDLookupPtr;
            [NoAlias] public NativeList<CompactNodeID>                          nodesLookup;
            [NoAlias] public NativeList<CompactHierarchy>                       hierarchyList;
            [NativeDisableContainerSafetyRestriction]
            [NoAlias] public NativeParallelHashMap<int, RefCountedBrushMeshBlob>        brushMeshBlobCache;
             
            public void Execute()
            {
                if (!generatedNodeDefinitions.IsCreated)
                    return;

                ref var hierarchyIDLookup   = ref hierarchyIDLookupRef.Value;// UnsafeUtility.AsRef<IDManager>(hierarchyIDLookupPtr);
                //ref var hierarchyIDLookup   = ref UnsafeUtility.AsRef<IDManager>(hierarchyIDLookupPtr);
                ref var nodeIDLookup        = ref UnsafeUtility.AsRef<SlotIndexMap>(nodeIDLookupPtr);

                // TODO: set all unique hierarchies dirty separately, somehow. Make this job parallel
                var hierarchyListPtr = (CompactHierarchy*)hierarchyList.GetUnsafePtr();
                for (int index = 0; index < generatedNodeDefinitions.Length; index++)
                {
                    if (index >= generatedNodeDefinitions.Length)
                        throw new Exception($"index {index} >= generatedNodeDefinitions.Length {generatedNodeDefinitions.Length}");

                    var hierarchyIndex  = generatedNodeDefinitions[index].hierarchyIndex;
                    var compactNodeID   = generatedNodeDefinitions[index].compactNodeID;
                    var transformation  = generatedNodeDefinitions[index].transformation;
                    var operation       = generatedNodeDefinitions[index].operation;
                    var brushMeshHash   = generatedNodeDefinitions[index].brushMeshHash;

                    ref var compactHierarchy = ref hierarchyListPtr[hierarchyIndex];
                    compactHierarchy.SetState(compactNodeID, brushMeshBlobCache, brushMeshHash, operation, transformation);
                    compactHierarchy.SetTreeDirty(); 
                } 

                // Reverse order so that we don't move nodes around when they're already in order (which is the fast path)
                for (int index = generatedNodeDefinitions.Length - 1; index >= 0; index--)
                {
                    if (index >= generatedNodeDefinitions.Length)
                        throw new Exception($"index {index} >= generatedNodeDefinitions.Length {generatedNodeDefinitions.Length}");

                    var hierarchyIndex      = generatedNodeDefinitions[index].hierarchyIndex;
                    var parentCompactNodeID = generatedNodeDefinitions[index].parentCompactNodeID; 
                    var compactNodeID       = generatedNodeDefinitions[index].compactNodeID;
                    var siblingIndex        = generatedNodeDefinitions[index].siblingIndex;
                     
                    ref var compactHierarchy = ref hierarchyListPtr[hierarchyIndex];
                    compactHierarchy.AttachToParentAt(ref hierarchyIDLookup, hierarchyList, ref nodeIDLookup, nodesLookup, parentCompactNodeID, siblingIndex, compactNodeID); // TODO: need to be able to do this
                }
            }
        }

        static JobHandle ScheduleAssignMeshesJob(bool                                runInParallel, 
                                                 NativeList<CompactHierarchy>        hierarchyList,
                                                 NativeList<GeneratedNodeDefinition> generatedNodeDefinitions,
                                                 JobHandle                           dependsOn)
        {
            var sortJob             = new HierarchySortJob { generatedNodeDefinitions = generatedNodeDefinitions };
            var sortJobHandle       = sortJob.Schedule(runInParallel, dependsOn);
            var brushMeshBlobCache  = ChiselMeshLookup.Value.brushMeshBlobCache;
            var assignJob = new AssignMeshesJob
            {
                // Read
                generatedNodeDefinitions = generatedNodeDefinitions,

                // Read/Write
                hierarchyIDLookupRef     = new PointerReference<SlotIndexMap>(ref CompactHierarchyManager.HierarchyIDLookup),
                hierarchyList            = hierarchyList,
                brushMeshBlobCache       = brushMeshBlobCache
            };
            assignJob.InitializeLookups(); 
            return assignJob.Schedule(runInParallel, sortJobHandle);
        }

        public void Dispose()
		{
			// Confirmed to be called
			if (generatorPools == null)
                return;

            var allGeneratorPools = generatorPools.ToArray();
            for (int i = allGeneratorPools.Length - 1; i >= 0; i--)
            { 
                try 
                { 
                    allGeneratorPools[i].Dispose(); 
                    allGeneratorPools[i] = default; 
                }
                catch (System.Exception ex)
                {
                    Debug.LogException(ex);
                }
            }
             
			generatorPools.Clear();
            generatorPools = null;
        }
    }

    // TODO: move to core
    public interface IGeneratorJobPool : System.IDisposable
    {
        void AllocateOrClear();
        bool HasJobs { get; }
        JobHandle ScheduleGenerateJob(bool runInParallel, JobHandle dependsOn);
        JobHandle ScheduleUpdateHierarchyJob(bool runInParallel, JobHandle dependsOn, int index, NativeArray<int> totalCounts);
        JobHandle ScheduleInitializeArraysJob(bool runInParallel, 
                                              [NoAlias, ReadOnly] NativeList<CompactHierarchy>                    inHierarchyList, 
                                              [NoAlias, WriteOnly] NativeList<GeneratedNodeDefinition>            outGeneratedNodeDefinitions,
                                              [NoAlias, WriteOnly] NativeList<BlobAssetReference<BrushMeshBlob>>  outBrushMeshBlobs,
                                              JobHandle dependsOn);
    }

    [BurstCompile(CompileSynchronously = true)]
    struct CreateBrushesJob<Generator> : IJobParallelForDefer
        where Generator : unmanaged, IBrushGenerator
    {
        [NoAlias, ReadOnly] public NativeList<Generator> generators;
        [NoAlias, ReadOnly] public NativeList<BlobAssetReference<InternalChiselSurfaceArray>> surfaceArrays;

        [NativeDisableParallelForRestriction]
        [NoAlias, WriteOnly] public NativeList<BlobAssetReference<BrushMeshBlob>> brushMeshes;

        public void Execute(int index)
        {
            if (!surfaceArrays.IsCreated ||
                !surfaceArrays[index].IsCreated)
            {
                brushMeshes[index] = default;
                return;
            }

            brushMeshes[index] = generators[index].GenerateMesh(surfaceArrays[index]);
        }
    }

    [BurstCompile(CompileSynchronously = true)]
    struct UpdateHierarchyJob : IJob
    {
        [NoAlias, ReadOnly] public NativeList<NodeID> generatorNodes;
        [NoAlias, ReadOnly] public int index;
        [NoAlias, WriteOnly] public NativeArray<int> totalCounts;

        public void Execute()
        {
            totalCounts[index] = generatorNodes.Length;
        }
    }

    [BurstCompile(CompileSynchronously = true)] 
    unsafe struct InitializeArraysJob : IJob
    {
        // TODO: get rid of this
        public void InitializeLookups()
        {
            hierarchyIDLookupPtr = (SlotIndexMap*)UnsafeUtility.AddressOf(ref CompactHierarchyManager.HierarchyIDLookup);
            nodeIDLookupPtr = (SlotIndexMap*)UnsafeUtility.AddressOf(ref CompactHierarchyManager.NodeIDLookup);
            nodesLookup = CompactHierarchyManager.Nodes;
        }

        // Read
        [NativeDisableUnsafePtrRestriction, NoAlias, ReadOnly] public SlotIndexMap*    hierarchyIDLookupPtr;
        [NativeDisableUnsafePtrRestriction, NoAlias, ReadOnly] public SlotIndexMap*    nodeIDLookupPtr;
        [NoAlias, ReadOnly] public NativeList<CompactNodeID>                        nodesLookup;

        [NoAlias, ReadOnly] public NativeList<NodeID>                             nodes;
        [NoAlias, ReadOnly] public NativeList<CompactHierarchy>                   hierarchyList;
        [NoAlias, ReadOnly] public NativeList<BlobAssetReference<BrushMeshBlob>>  brushMeshes;

        // Write
        [NoAlias, WriteOnly] public NativeList<GeneratedNodeDefinition>.ParallelWriter            generatedNodeDefinitions;
        [NoAlias, WriteOnly] public NativeList<BlobAssetReference<BrushMeshBlob>>.ParallelWriter  brushMeshBlobs;

        public void Execute()
        {
            ref var hierarchyIDLookup = ref UnsafeUtility.AsRef<SlotIndexMap>(hierarchyIDLookupPtr);
            ref var nodeIDLookup = ref UnsafeUtility.AsRef<SlotIndexMap>(nodeIDLookupPtr);
            var hierarchyListPtr = (CompactHierarchy*)hierarchyList.GetUnsafeReadOnlyPtr();
            for (int i = 0; i < nodes.Length; i++) 
            {
                var nodeID              = nodes[i];
                var compactNodeID       = CompactHierarchyManager.GetCompactNodeIDNoError(ref nodeIDLookup, nodesLookup, nodeID);
                var hierarchyIndex      = CompactHierarchyManager.GetHierarchyIndexUnsafe(ref hierarchyIDLookup, compactNodeID);
                var transformation      = hierarchyListPtr[hierarchyIndex].GetLocalTransformation(compactNodeID);
                var operation           = hierarchyListPtr[hierarchyIndex].GetOperation(compactNodeID);
                var parentCompactNodeID = hierarchyListPtr[hierarchyIndex].ParentOf(compactNodeID);
                var siblingIndex        = hierarchyListPtr[hierarchyIndex].SiblingIndexOf(compactNodeID);
                var brushMeshBlob       = brushMeshes[i];

                generatedNodeDefinitions.AddNoResize(new GeneratedNodeDefinition
                {
                    parentCompactNodeID = parentCompactNodeID,
                    compactNodeID       = compactNodeID,
                    hierarchyIndex      = hierarchyIndex,
                    siblingIndex        = siblingIndex,
                    operation           = operation,
                    transformation      = transformation
                });
                brushMeshBlobs.AddNoResize(brushMeshBlob);
            }
        }
    }

    // TODO: move to core, call ScheduleUpdate when hash of definition changes (no more manual calls)
    [BurstCompile(CompileSynchronously = true)]
    public class GeneratorBrushJobPool<Generator> : IGeneratorJobPool
        where Generator : unmanaged, IBrushGenerator
    {
        NativeList<Generator>                                      generators;
        NativeList<BlobAssetReference<BrushMeshBlob>>              brushMeshes;
        NativeList<BlobAssetReference<InternalChiselSurfaceArray>> surfaceArrays;
        NativeList<NodeID>                                         generatorNodes;

        JobHandle updateHierarchyJobHandle = default;
        JobHandle initializeArraysJobHandle = default;
        JobHandle previousJobHandle = default;
        
        public GeneratorBrushJobPool() { GeneratorJobPoolManager.Register(this); }

        public void AllocateOrClear()
        {
            updateHierarchyJobHandle.Complete();
            updateHierarchyJobHandle = default;
            initializeArraysJobHandle.Complete();
            initializeArraysJobHandle = default;
            previousJobHandle.Complete(); // <- make sure we've completed the previous schedule
            previousJobHandle = default;
            
            if (generators    .IsCreated) generators    .Clear(); else generators     = new NativeList<Generator>(Allocator.Persistent); // Confirmed to be disposed
			if (brushMeshes   .IsCreated) brushMeshes   .Clear(); else brushMeshes    = new NativeList<BlobAssetReference<BrushMeshBlob>>(Allocator.Persistent); // Confirmed to be disposed
			if (surfaceArrays .IsCreated) surfaceArrays .Clear(); else surfaceArrays  = new NativeList<BlobAssetReference<InternalChiselSurfaceArray>>(Allocator.Persistent); // Confirmed to be disposed
			if (generatorNodes.IsCreated) generatorNodes.Clear(); else generatorNodes = new NativeList<NodeID>(Allocator.Persistent); // Confirmed to be disposed
		}

		public void Dispose()
		{
			// Confirmed to be called ChiselBox/ChiselCylinder
			GeneratorJobPoolManager.Unregister(this);
            if (generators    .IsCreated) generators    .Dispose();
            if (brushMeshes   .IsCreated) brushMeshes   .Dispose();
            if (surfaceArrays .IsCreated) surfaceArrays .DisposeDeep();
            if (generatorNodes.IsCreated) generatorNodes.Dispose();

			generators = default;
			brushMeshes = default;
			surfaceArrays = default;
            generatorNodes = default;
        }

        public bool HasJobs
        {
            get
            {
                previousJobHandle.Complete(); // <- make sure we've completed the previous schedule
                previousJobHandle = default;

                return generators.IsCreated && generators.Length > 0;
            }
        }

        public void ScheduleUpdate(CSGTreeNode generatorNode, Generator settings, BlobAssetReference<InternalChiselSurfaceArray> surfaceArray)
        {
            if (!generatorNodes.IsCreated)
                AllocateOrClear();

            if (!surfaceArray.IsCreated)
                return;

            var nodeID = generatorNode.nodeID;
            var index = generatorNodes.IndexOf(nodeID);
            if (index != -1)
            {
                // TODO: get rid of this
                if (surfaceArrays[index].IsCreated)
                    surfaceArrays[index].Dispose();

                surfaceArrays [index] = surfaceArray;
                generators    [index] = settings;
                generatorNodes[index] = nodeID;
            } else
            { 
                surfaceArrays .Add(surfaceArray);
                generators    .Add(settings);
                generatorNodes.Add(nodeID); 
            }
        }

        public JobHandle ScheduleGenerateJob(bool runInParallel, JobHandle dependsOn = default)
        {
            previousJobHandle.Complete(); // <- make sure we've completed the previous schedule
            previousJobHandle = default;

            if (!generatorNodes.IsCreated)
                return dependsOn;
            
            for (int i = generatorNodes.Length - 1; i >= 0; i--)
            {
                var nodeID = generatorNodes[i];
                if (surfaceArrays[i].IsCreated &&
                    CompactHierarchyManager.IsValidNodeID(nodeID) &&
                    CompactHierarchyManager.GetTypeOfNode(nodeID) == CSGNodeType.Brush)
                    continue;

                if (surfaceArrays[i].IsCreated)
                    surfaceArrays[i].Dispose();
                surfaceArrays.RemoveAt(i);
                generators        .RemoveAt(i);
                generatorNodes    .RemoveAt(i);
            }

            if (generatorNodes.Length == 0)
                return dependsOn;

            brushMeshes.Clear();
            brushMeshes.Resize(generators.Length, NativeArrayOptions.ClearMemory);
            var job = new CreateBrushesJob<Generator>
            {
                // read
                generators    = generators,
                surfaceArrays = surfaceArrays,

                // write
                brushMeshes   = brushMeshes
            };
            var createJobHandle = job.Schedule(runInParallel, generators, 8, dependsOn);
            var surfaceDeepDisposeJobHandle = surfaceArrays.DisposeDeep(createJobHandle);
            var generatorsDisposeJobHandle = generators.Dispose(createJobHandle);
            generators = default;
            surfaceArrays = default;
            return JobHandleExtensions.CombineDependencies(createJobHandle, surfaceDeepDisposeJobHandle, generatorsDisposeJobHandle);
        }
         
        public JobHandle ScheduleUpdateHierarchyJob(bool runInParallel, JobHandle dependsOn, int index, NativeArray<int> totalCounts)
        {
            var updateHierarchyJob = new UpdateHierarchyJob
            {
                generatorNodes  = generatorNodes,
                index           = index,
                totalCounts     = totalCounts
            };
            updateHierarchyJobHandle = updateHierarchyJob.Schedule(runInParallel, dependsOn);
            return updateHierarchyJobHandle;
        }

        public JobHandle ScheduleInitializeArraysJob(bool runInParallel, 
                                                     [NoAlias, ReadOnly] NativeList<CompactHierarchy> hierarchyList, 
                                                     [NoAlias, WriteOnly] NativeList<GeneratedNodeDefinition> generatedNodeDefinitions,
                                                     [NoAlias, WriteOnly] NativeList<BlobAssetReference<BrushMeshBlob>> brushMeshBlobs,
                                                     JobHandle dependsOn)
        {
            var initializeArraysJob = new InitializeArraysJob
            {
                // Read
                nodes                    = generatorNodes,
                brushMeshes              = brushMeshes,
                hierarchyList            = hierarchyList,

                // Write
                generatedNodeDefinitions = generatedNodeDefinitions.AsParallelWriter(),
                brushMeshBlobs           = brushMeshBlobs.AsParallelWriter()
            };
            initializeArraysJob.InitializeLookups();
            initializeArraysJobHandle = initializeArraysJob.Schedule(runInParallel, dependsOn);
            var combinedJobHandle = JobHandleExtensions.CombineDependencies(
                                        initializeArraysJobHandle,
                                        generatorNodes.Dispose(initializeArraysJobHandle),
                                        brushMeshes.Dispose(initializeArraysJobHandle));
            generatorNodes = default;
            brushMeshes = default;
            return combinedJobHandle;
        }
    }
    
    [BurstCompile(CompileSynchronously = true)]
    public unsafe struct BranchPrepareAndCountBrushesJob<Generator> : IJobParallelForDefer
        where Generator : unmanaged, IBranchGenerator
    {
        [NativeDisableUnsafePtrRestriction]
        [NoAlias] public UnsafeList<Generator>*     settings;
        [NoAlias, ReadOnly] public NativeList<Generator>      generators; // required because it's used for the count of IJobParallelForDefer
        [NativeDisableParallelForRestriction]
        [NoAlias] public NativeArray<int>           brushCounts;

        public void Execute(int index)
        {
            ref var setting = ref settings->ElementAt(index);
            brushCounts[index] = setting.PrepareAndCountRequiredBrushMeshes();
        }
    }
         
    [BurstCompile(CompileSynchronously = true)]
    public unsafe struct BranchAllocateBrushesJob<Generator> : IJob
        where Generator : unmanaged, IBranchGenerator
    {
        [NoAlias, ReadOnly] public NativeArray<int>     brushCounts;
        [NativeDisableUnsafePtrRestriction]
        [NoAlias, WriteOnly] public UnsafeList<Range>*  ranges;
        [NoAlias] public NativeList<GeneratedNode>      generatedNodes;

        public void Execute()
        {
            var totalRequiredBrushCount = 0;
            for (int i = 0; i < brushCounts.Length; i++)
            {
                var length = brushCounts[i];
                var start = totalRequiredBrushCount;
                var end = start + length;
                (*ranges)[i] = new Range { start = start, end = end };
                totalRequiredBrushCount += length;
            }
            generatedNodes.Clear();
            generatedNodes.Resize(totalRequiredBrushCount, NativeArrayOptions.ClearMemory);
        }
    }

    [BurstCompile(CompileSynchronously = true)]
    unsafe struct BranchCreateBrushesJob<Generator> : IJobParallelForDefer
        where Generator : unmanaged, IBranchGenerator
    {
        [NoAlias, ReadOnly] public NativeList<BlobAssetReference<InternalChiselSurfaceArray>> surfaceArrays;
        [NoAlias, ReadOnly] public NativeList<Generator> generators; // required because it's used for the count of IJobParallelForDefer

        [NativeDisableUnsafePtrRestriction]
        [NoAlias] public UnsafeList<Generator>* settings;
            
        [NativeDisableUnsafePtrRestriction]
        [NoAlias] public UnsafeList<Range>*     ranges;

        [NativeDisableParallelForRestriction]
        [NoAlias, WriteOnly] public NativeList<GeneratedNode>   generatedNodes;

        public void Execute(int index)
        {
            try
            {
                ref var range = ref ranges->ElementAt(index);
                var requiredSubMeshCount = range.Length;
                if (requiredSubMeshCount != 0)
                {
                    using var nodes = new NativeList<GeneratedNode>(requiredSubMeshCount, Allocator.Temp);
                    nodes.Resize(requiredSubMeshCount, NativeArrayOptions.ClearMemory); //<- get rid of resize, use add below

                    ref var setting = ref settings->ElementAt(index);
                    if (!surfaceArrays[index].IsCreated ||
                        !setting.GenerateNodes(surfaceArrays[index], nodes))
                    {
                        range = new Range { start = 0, end = 0 };
                        return;
                    }

                    Debug.Assert(requiredSubMeshCount == nodes.Length);
                    if (requiredSubMeshCount != nodes.Length)
                        throw new InvalidOperationException();
                    for (int i = range.start, m = 0; i < range.end; i++, m++)
                        generatedNodes[i] = nodes[m];
                }
            }
            finally
            {
                ref var setting = ref settings->ElementAt(index);
                setting.Dispose();
            }
        }
    }

    [BurstCompile(CompileSynchronously = true)]
    public class GeneratorBranchJobPool<Generator> : IGeneratorJobPool
        where Generator : unmanaged, IBranchGenerator
    {
        NativeList<BlobAssetReference<InternalChiselSurfaceArray>> surfaceArrays;
        NativeList<Generator>       generators;
        NativeList<NodeID>          generatorRootNodeIDs;
        NativeList<Range>           generatorNodeRanges;
        NativeList<GeneratedNode>   generatedNodes;
        
        JobHandle previousJobHandle = default;

        public GeneratorBranchJobPool() { GeneratorJobPoolManager.Register(this); }

		public void AllocateOrClear()
        {
            previousJobHandle.Complete(); // <- make sure we've completed the previous schedule
            previousJobHandle = default;

            if (generatorRootNodeIDs.IsCreated) generatorRootNodeIDs.Clear(); else generatorRootNodeIDs = new NativeList<NodeID>(Allocator.Persistent); // Confirmed to be disposed
			if (generatorNodeRanges .IsCreated) generatorNodeRanges .Clear(); else generatorNodeRanges  = new NativeList<Range>(Allocator.Persistent); // Confirmed to be disposed
			if (surfaceArrays       .IsCreated) surfaceArrays       .Clear(); else surfaceArrays        = new NativeList<BlobAssetReference<InternalChiselSurfaceArray>>(Allocator.Persistent); // Confirmed to be disposed
			if (generatedNodes      .IsCreated) generatedNodes      .Clear(); else generatedNodes       = new NativeList<GeneratedNode>(Allocator.Persistent); // Confirmed to be disposed
			if (generators          .IsCreated) generators          .Clear(); else generators           = new NativeList<Generator>(Allocator.Persistent); // Confirmed to be disposed
		}

		public void Dispose()
        {
            // Confirmed to be called ChiselExtrudedShape
            GeneratorJobPoolManager.Unregister(this);
            if (generatorRootNodeIDs.IsCreated) generatorRootNodeIDs.SafeDispose();
            if (generatorNodeRanges .IsCreated) generatorNodeRanges .SafeDispose();
            if (surfaceArrays       .IsCreated) surfaceArrays       .DisposeDeep(); // Confirmed to be called on children
            if (generatedNodes      .IsCreated) generatedNodes      .SafeDispose();
            if (generators          .IsCreated) generators          .SafeDispose();
            
            generatorRootNodeIDs = default;
            generatorNodeRanges = default;
            surfaceArrays = default;
            generatedNodes = default;
            generators = default;
        }

        public void ScheduleUpdate(CSGTreeNode node, Generator settings, BlobAssetReference<InternalChiselSurfaceArray> surfaceArray)
        {
            if (!generatorRootNodeIDs.IsCreated)
                AllocateOrClear();

            if (!surfaceArray.IsCreated)
                return;

            var nodeID = node.nodeID;
            var index = generatorRootNodeIDs.IndexOf(nodeID);
            if (index != -1)
            {
                // TODO: get rid of this
                if (surfaceArrays[index].IsCreated)
                    surfaceArrays[index].Dispose();

                generatorRootNodeIDs[index] = nodeID;
                surfaceArrays       [index] = surfaceArray;
                generators          [index] = settings;
            } else
            {
                generatorRootNodeIDs.Add(nodeID);
                surfaceArrays       .Add(surfaceArray);
                generators          .Add(settings);
            }
        }

        public bool HasJobs
        {
            get
            {
                previousJobHandle.Complete(); // <- make sure we've completed the previous schedule
                previousJobHandle = default;

                return generators.IsCreated && generators.Length > 0;
            }
        }

        const Allocator defaultAllocator = Allocator.TempJob;

        public unsafe JobHandle ScheduleGenerateJob(bool runInParallel, JobHandle dependsOn = default)
        {
            previousJobHandle.Complete(); // <- make sure we've completed the previous schedule
            previousJobHandle = default;

            if (!generatorRootNodeIDs.IsCreated)
                return dependsOn;

            for (int i = generatorRootNodeIDs.Length - 1; i >= 0; i--)
            {
                var nodeID = generatorRootNodeIDs[i];
                if (surfaceArrays[i].IsCreated &&
                    CompactHierarchyManager.IsValidNodeID(nodeID) &&
                    CompactHierarchyManager.GetTypeOfNode(nodeID) == CSGNodeType.Branch)
                    continue;

                if (surfaceArrays[i].IsCreated)
                    surfaceArrays[i].Dispose();

                generatorRootNodeIDs.RemoveAt(i);
                surfaceArrays  .RemoveAt(i);
                generators          .RemoveAt(i);
            }

            if (generatorRootNodeIDs.Length == 0)
                return dependsOn;

            generatorNodeRanges.Clear();
            generatorNodeRanges.Resize(generators.Length, NativeArrayOptions.ClearMemory);

            var brushCounts = new NativeArray<int>(generators.Length, defaultAllocator);
            var countBrushesJob = new BranchPrepareAndCountBrushesJob<Generator>
            {
                settings       = generators.GetUnsafeList(),
                generators     = generators,// required because it's used for the count of IJobParallelForDefer
                brushCounts    = brushCounts
            };
            var brushCountJobHandle = countBrushesJob.Schedule(runInParallel, generators, 8, dependsOn);
            
            var allocateBrushesJob = new BranchAllocateBrushesJob<Generator>
            {
                brushCounts    = brushCounts,
                ranges         = generatorNodeRanges.GetUnsafeList(),
                generatedNodes = generatedNodes
            };
            var allocateBrushesJobHandle = allocateBrushesJob.Schedule(runInParallel, brushCountJobHandle);

            var createJob = new BranchCreateBrushesJob<Generator>
            {
                settings       = generators.GetUnsafeList(),
                generators     = generators, // required because it's used for the count of IJobParallelForDefer
                ranges         = generatorNodeRanges.GetUnsafeList(),
                generatedNodes = generatedNodes,
                surfaceArrays  = surfaceArrays
            };
            var createJobHandle = createJob.Schedule(runInParallel, generators, 8, allocateBrushesJobHandle);

            var surfaceDeepDisposeJobHandle = surfaceArrays.DisposeDeep(createJobHandle);
            surfaceArrays = default;
            return brushCounts.Dispose(surfaceDeepDisposeJobHandle);
        }
        

        // TODO: implement a way to setup a full hierarchy here, instead of a list of brushes
        // TODO: make this burstable
        [BurstCompile(CompileSynchronously = true)]
        struct UpdateHierarchyJob : IJob
        {
            [NoAlias, ReadOnly] public NativeList<NodeID>   generatorRootNodeIDs;
            [NoAlias, ReadOnly] public NativeList<Range>    generatorNodeRanges;
            
            [NoAlias] public NativeList<Generator>          generators;

            [NoAlias, ReadOnly] public int                  index;
            [NoAlias, WriteOnly] public NativeArray<int>    totalCounts;
            
            void ClearBrushes(CSGTreeBranch branch)
            {
                for (int i = branch.Count - 1; i >= 0; i--)
                    branch[i].Destroy();
                branch.Clear();
            }

            void BuildBrushes(CSGTreeBranch branch, int desiredBrushCount)
            {
                if (branch.Count < desiredBrushCount)
                {
                    var tree = branch.Tree;
                    var newBrushCount = desiredBrushCount - branch.Count;
                    NativeArray<CSGTreeNode> newRange;
					using var _newRange = newRange = new NativeArray<CSGTreeNode>(newBrushCount, Allocator.Temp);

                    var instanceID = branch.InstanceID;
                    for (int i = 0; i < newBrushCount; i++)
                        newRange[i] = tree.CreateBrush(instanceID: instanceID, operation: CSGOperationType.Additive);
                    branch.AddRange(newRange);
                } else
                {
                    for (int i = branch.Count - 1; i >= desiredBrushCount; i--)
                    {
                        var oldBrush = branch[i];
                        branch.RemoveAt(i);
                        oldBrush.Destroy();
                    }
                }
            }
        
            public void Execute()
            {
                int count = 0;
                for (int i = 0; i < generatorRootNodeIDs.Length; i++)
                {
                    var range = generatorNodeRanges[i];
                    var branch = CSGTreeBranch.Find(generatorRootNodeIDs[i]);
                    if (!branch.Valid)
                        continue;

                    if (range.Length == 0)
                    {
                        ClearBrushes(branch);
                        continue;
                    }

                    count += range.Length;

                    if (branch.Count != range.Length)
                        BuildBrushes(branch, range.Length);
                }

                totalCounts[index] = count;
            }
        }

        public JobHandle ScheduleUpdateHierarchyJob(bool runInParallel, JobHandle dependsOn, int index, NativeArray<int> totalCounts)
        {
            var updateHierarchyJob = new UpdateHierarchyJob
            {
                generatorRootNodeIDs = generatorRootNodeIDs,
                generatorNodeRanges  = generatorNodeRanges,
                generators           = generators,
                index                = index,
                totalCounts          = totalCounts
            };
            // TODO: make this work in parallel (create new linear stairs => errors)
#if true
            dependsOn.Complete();
            updateHierarchyJob.Execute();
            return generators.Dispose((JobHandle)default);
#else
            var updateHierarchyJobHandle = updateHierarchyJob.Schedule(runInParallel, dependsOn);
            return generators.Dispose(updateHierarchyJobHandle);
#endif
        }
        
        [BurstCompile(CompileSynchronously = true)]
        internal unsafe struct InitializeArraysJob : IJob
        {
            public void InitializeLookups()
            {
                hierarchyIDLookupPtr = (SlotIndexMap*)UnsafeUtility.AddressOf(ref CompactHierarchyManager.HierarchyIDLookup);
                nodeIDLookupPtr = (SlotIndexMap*)UnsafeUtility.AddressOf(ref CompactHierarchyManager.NodeIDLookup);
                nodesLookup = CompactHierarchyManager.Nodes;
            }

            // Read
            [NativeDisableUnsafePtrRestriction, NoAlias, ReadOnly] public SlotIndexMap*    hierarchyIDLookupPtr;
            [NativeDisableUnsafePtrRestriction, NoAlias, ReadOnly] public SlotIndexMap*    nodeIDLookupPtr;
            [NoAlias, ReadOnly] public NativeList<CompactNodeID>                        nodesLookup;

            [NoAlias, ReadOnly] public NativeList<NodeID>               generatorRootNodeIDs;
            [NoAlias, ReadOnly] public NativeList<Range>                generatorNodeRanges;
            [NoAlias, ReadOnly] public NativeList<CompactHierarchy>     hierarchyList;
            [NoAlias, ReadOnly] public NativeList<GeneratedNode>        generatedNodes;

            // Write
            [NoAlias, WriteOnly] public NativeList<GeneratedNodeDefinition>.ParallelWriter            generatedNodeDefinitions;
            [NoAlias, WriteOnly] public NativeList<BlobAssetReference<BrushMeshBlob>>.ParallelWriter  brushMeshBlobs;

            public void Execute()
            {
                ref var hierarchyIDLookup = ref UnsafeUtility.AsRef<SlotIndexMap>(hierarchyIDLookupPtr);
                ref var nodeIDLookup = ref UnsafeUtility.AsRef<SlotIndexMap>(nodeIDLookupPtr);
                var hierarchyListPtr = (CompactHierarchy*)hierarchyList.GetUnsafeReadOnlyPtr();
                for (int i = 0; i < generatorRootNodeIDs.Length; i++)
                {
                    var range = generatorNodeRanges[i];
                    if (range.Length == 0)
                        continue;
                    
                    var rootNodeID           = generatorRootNodeIDs[i];
                    var rootCompactNodeID    = CompactHierarchyManager.GetCompactNodeIDNoError(ref nodeIDLookup, nodesLookup, rootNodeID);
                    var hierarchyIndex       = CompactHierarchyManager.GetHierarchyIndexUnsafe(ref hierarchyIDLookup, rootCompactNodeID);
                    ref var compactHierarchy = ref hierarchyListPtr[hierarchyIndex];

                    // TODO: just pass an array of compactNodeIDs along from the place were we create these nodes (no lookup needed)
                    // TODO: how can we re-use existing compactNodeIDs instead re-creating them when possible?
                    NativeArray<CompactNodeID> childCompactNodeIDs;
					using var _childCompactNodeIDs = childCompactNodeIDs = new NativeArray<CompactNodeID>(range.Length, Allocator.Temp);
                    for (int b = 0; b < range.Length; b++)
                        childCompactNodeIDs[b] = compactHierarchy.GetChildCompactNodeIDAtNoError(rootCompactNodeID, b);

                    // TODO: sort generatedNodes span, by parentIndex + original index so that all parentIndices are sequential, and in order from small to large

                    int siblingIndex = 0;
                    int prevParentIndex = -1;
                    for (int b = 0, m = range.start; m < range.end; b++, m++)
                    {
                        var compactNodeID = childCompactNodeIDs[b];
                        var operation     = generatedNodes[m].operation;
                        var brushMeshBlob = generatedNodes[m].brushMesh;
                        var parentIndex   = generatedNodes[m].parentIndex;
                        if (parentIndex < prevParentIndex || parentIndex >= b)
                            parentIndex = prevParentIndex;
                        if (prevParentIndex != parentIndex)
                            siblingIndex = 0;
                        var parentCompactNodeID = (parentIndex == -1) ? rootCompactNodeID : childCompactNodeIDs[parentIndex];
                        var transformation      = generatedNodes[m].transformation;
                        generatedNodeDefinitions.AddNoResize(new GeneratedNodeDefinition
                        {
                            parentCompactNodeID = parentCompactNodeID,
                            compactNodeID       = compactNodeID,
                            hierarchyIndex      = hierarchyIndex,
                            siblingIndex        = siblingIndex,
                            operation           = operation,
                            transformation      = transformation
                        });
                        brushMeshBlobs.AddNoResize(brushMeshBlob);
                        siblingIndex++;
                    }
                }
            }
        }

        public JobHandle ScheduleInitializeArraysJob(bool runInParallel, 
                                                     [NoAlias, ReadOnly] NativeList<CompactHierarchy>                   hierarchyList, 
                                                     [NoAlias, WriteOnly] NativeList<GeneratedNodeDefinition>           generatedNodeDefinitions, 
                                                     [NoAlias, WriteOnly] NativeList<BlobAssetReference<BrushMeshBlob>> brushMeshBlobs, 
                                                     JobHandle dependsOn)
        {
            var initializeArraysJob = new InitializeArraysJob
            {
                // Read
                generatorRootNodeIDs    = generatorRootNodeIDs,
                generatorNodeRanges     = generatorNodeRanges,
                generatedNodes          = generatedNodes,
                hierarchyList           = hierarchyList,

                // Write
                generatedNodeDefinitions = generatedNodeDefinitions.AsParallelWriter(),
                brushMeshBlobs           = brushMeshBlobs.AsParallelWriter()
            };
            initializeArraysJob.InitializeLookups();
            var initializeArraysJobHandle = initializeArraysJob.Schedule(runInParallel, dependsOn);
            var combinedJobHandle = JobHandleExtensions.CombineDependencies(
                                        initializeArraysJobHandle,
                                        generatorRootNodeIDs.Dispose(initializeArraysJobHandle),
                                        generatorNodeRanges.Dispose(initializeArraysJobHandle),
                                        generatedNodes.Dispose(initializeArraysJobHandle));

            generatorRootNodeIDs = default;
            generatorNodeRanges = default;
            generatedNodes = default;
            return combinedJobHandle;
        }
	}
}
