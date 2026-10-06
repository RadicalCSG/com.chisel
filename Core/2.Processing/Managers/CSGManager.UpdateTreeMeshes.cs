using Unity.Jobs;
using Unity.Collections;
using Unity.Mathematics;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Profiling;
using Unity.Entities;
using System.Buffers;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace Chisel.Core
{
    public struct ModelSettings
    {
        public bool SubtractiveWorkflow;
        public bool NormalSmoothing;
        public float NormalSmoothingAngle;
        /// <summary>The lightmap texels a unit of the model gets (LightmapUVSettings.texelsPerUnit); 0 for the default</summary>
        public float LightmapTexelsPerUnit;
        /// <summary>The lightmap texels between two charts (LightmapUVSettings.paddingTexels)</summary>
        public float LightmapPaddingTexels;
    }
     
    public static class ModelSettingsStore
    {
        static readonly Dictionary<ulong, ModelSettings> s_Settings = new();
        static readonly object s_Lock = new();

        public static void Set(ulong entityID, ModelSettings settings)
        {
            lock (s_Lock)
            {
                s_Settings[entityID] = settings;
            }
        }

        public static bool TryGet(ulong entityID, out ModelSettings settings)
        {
            lock (s_Lock)
            {
                return s_Settings.TryGetValue(entityID, out settings);
            }
        }

        public static void Remove(ulong entityID)
        {
            lock (s_Lock)
            {
                s_Settings.Remove(entityID);
            }
        }
    }

	static partial class CompactHierarchyManager
	{
		const bool runInParallelDefault = true;

        const int kMergeIterations = 30;

        const int kMaxPropagationRounds = 8;

        public static int LastUpdateRounds             { get; private set; }
        public static int LastUpdateModifiedBrushCount { get; private set; }
        public static int LastUpdateStaleBrushCount    { get; private set; }
        // One line per round and tree: what was modified, how many brushes were updated in total
        // (modified + touching) and how many the propagation dirtied for the next round.
        public static string LastUpdateLog             { get; private set; } = "";

        #region Update / Rebuild

        static readonly ProfilerMarker kUpdateTreeMeshesProfilerMarker = new("UpdateTreeMeshes");

		internal static bool UpdateAllTreeMeshes(FinishMeshUpdate finishMeshUpdates, out JobHandle allTrees)
        {
            return UpdateAllTreeMeshes(finishMeshUpdates, null, out allTrees);
        }

		internal static bool UpdateAllTreeMeshes(FinishMeshUpdate finishMeshUpdates, CanSkipTreeUpdate canSkipTreeUpdate, out JobHandle allTrees)
        {
            allTrees = default;
            bool needUpdate = false;

			if (instance.defaultHierarchyID == CompactHierarchyID.Invalid)
				instance.Initialize();

			CompactHierarchyManager.GetAllTrees(instance.allTrees);
            // Check if we have a tree that needs updates
            instance.updatedTrees.Clear();
            for (int t = 0; t < instance.allTrees.Length; t++)
            {
                var tree = instance.allTrees[t];
                if (tree.Valid &&
                    tree.IsStatusFlagSet(NodeStatusFlags.TreeNeedsUpdate))
                {
                    instance.updatedTrees.Add(tree);
                    needUpdate = true;
                }
            }

            if (!needUpdate)
                return false;

            using (kUpdateTreeMeshesProfilerMarker.Auto())
            {
                TreeUpdate.s_PropagationRound     = 0;
                TreeUpdate.s_PropagationRequested = false;
                TreeUpdate.s_ModifiedBrushCount   = 0;
                TreeUpdate.s_StaleBrushCount      = 0;
                TreeUpdate.s_Log.Clear();
                System.Array.Clear(TreeUpdate.s_LastExactCSGStats, 0, TreeUpdate.s_LastExactCSGStats.Length);
				allTrees = TreeUpdate.ScheduleTreeMeshJobs(finishMeshUpdates, instance.updatedTrees, canSkipTreeUpdate);

                // Further rounds for the brushes the welding/T-junction propagation dirtied (see
                // kMaxPropagationRounds), until nothing moves any more.
                while (TreeUpdate.s_PropagationRequested &&
                       TreeUpdate.s_PropagationRound + 1 < kMaxPropagationRounds)
                {
                    TreeUpdate.s_PropagationRound++;
                    TreeUpdate.s_PropagationRequested = false;
                    instance.updatedTrees.Clear();
                    for (int t = 0; t < instance.allTrees.Length; t++)
                    {
                        var tree = instance.allTrees[t];
                        if (tree.Valid &&
                            tree.IsStatusFlagSet(NodeStatusFlags.TreeNeedsUpdate))
                            instance.updatedTrees.Add(tree);
                    }
                    TreeUpdate.s_Log.Append("round ").Append(TreeUpdate.s_PropagationRound)
                                    .Append(": trees=").Append(instance.allTrees.Length)
                                    .Append(" dirty=").Append(instance.updatedTrees.Length).Append('\n');
                    if (instance.updatedTrees.Length == 0)
                        break;
                    allTrees = JobHandle.CombineDependencies(allTrees, TreeUpdate.ScheduleTreeMeshJobs(finishMeshUpdates, instance.updatedTrees));
                }
                LastUpdateRounds             = TreeUpdate.s_PropagationRound + 1;
                LastUpdateModifiedBrushCount = TreeUpdate.s_ModifiedBrushCount;
                LastUpdateStaleBrushCount    = TreeUpdate.s_StaleBrushCount;
                LastUpdateLog                = TreeUpdate.s_Log.ToString();
                ReportExactCSGFailures();
				return true;
            }
        }
        #endregion

        static string s_LastExactCSGFailureReport;

        static void ReportExactCSGFailures()
        {
            string report = null;
            var stats = TreeUpdate.s_LastExactCSGStats;
            for (int i = (int)ExactCSGStat.InvalidPlane; i < (int)ExactCSGStat.Count; i++)
            {
                if (stats[i] == 0)
                    continue;
                report = (report == null ? string.Empty : report + ", ") + (ExactCSGStat)i + " " + stats[i];
            }
            if (report == s_LastExactCSGFailureReport)
                return;
            s_LastExactCSGFailureReport = report;
            if (report != null)
                UnityEngine.Debug.LogFormat(UnityEngine.LogType.Warning, UnityEngine.LogOption.NoStacktrace, null,
                                            "Chisel's exact CSG left out what it could not build: {0}", report);
        }

        const Allocator defaultAllocator = Allocator.TempJob;

        internal struct TreeUpdate
        {
            // Propagation-round bookkeeping shared between UpdateAllTreeMeshes (which loops) and
            // ScheduleTreeMeshJobs (which dirties the stale brushes); see kMaxPropagationRounds.
            internal static int  s_PropagationRound;
            internal static bool s_PropagationRequested;
            internal static int  s_ModifiedBrushCount;
            internal static int  s_StaleBrushCount;
            internal static readonly System.Text.StringBuilder s_Log = new();

            internal static readonly bool kInternBrushPlanes = false;

            internal static readonly bool kUsePlaneIdsForAlignment = false;

            internal static bool kUseIncidenceWeld = false;

            internal static int kCanonicalVertexStage = 0;

            internal static bool kCanonicalAlignment = true;

            internal static bool kExactCSG = true;

            // What the exact CSG counted in the last UpdateAllTreeMeshes call (indexed by ExactCSGStat), summed over trees.
            internal static readonly int[] s_LastExactCSGStats = new int[(int)ExactCSGStat.Count];

            public CanonicalVertexStage canonicalVertexStage;
            public bool          exactCSG;
            // ExactCSGCapture is on for this update: ExactCSGJob writes its exact output into Temporaries.exactCapture, and
            // PreMeshUpdateDispose stores it with every brush's exact planes.
            public bool          captureExact;
            // The tree's output was built by the other algorithm (ChiselTreeLookup.Data.builtExact): every brush is rebuilt,
            // so an incremental update never leaves one algorithm's output next to the other's.
            public bool          rebuildAllBrushes;

            public CSGTree       tree;
            public CompactNodeID treeCompactNodeID;
            public int           brushCount;
            public int           maxNodeOrder;
            public int           updateCount;
            public bool          brushListChanged;
            // Set when this update dirtied brushes of this tree for another propagation round, so the
            // tree's "needs update" flag survives the clean-up that otherwise clears it.
            public bool          propagationRequested;
            // Set when RunMeshUpdateJobs left the meshes for the next propagation round to build.
            public bool          skipMeshGeneration;
            public bool          subtractiveWorkflow;
            public float         normalSmoothingAngle;
            public LightmapUVSettings lightmapUVSettings;

            public JobHandle     dependencies;

            #region All Native Collection Temporaries
            internal struct TemporariesStruct
            { 
                public UnityEngine.Mesh.MeshDataArray       meshDataArray;
                public NativeList<UnityEngine.Mesh.MeshData> meshDatas;

                public NativeArray<int>                     parameterCounts;
                public NativeList<NodeOrderNodeID>          transformTreeBrushIndicesList;

                public NativeList<CompactNodeID>            brushes;
                public NativeList<CompactNodeID>            nodes;

                public NativeList<IndexOrder>               allTreeBrushIndexOrders;
                public NativeList<IndexOrder>               rebuildTreeBrushIndexOrders;
                public NativeList<IndexOrder>               allUpdateBrushIndexOrders;
                public NativeArray<int>                     allBrushMeshIDs;
            
                public NativeArray<MeshQuery>               meshQueries;
                public int                                  meshQueriesLength;

                public NativeArray<UnsafeList<BrushIntersectWith>> brushBrushIntersections;
                public NativeList<BrushBoundsSweepEntry>    brushBoundsSweep;
                // Shared plane identity (see InternedPlanes / InternBrushPlanesJob). Only built when
                // kInternBrushPlanes is on; nothing consumes it yet.
                public InternedPlanes                       internedPlanes;
                public NativeList<int>                      brushPlaneIds;      // flat, (id+1), negated when flipped
                public NativeArray<int2>                    brushPlaneIdRange;  // by nodeOrder: (offset, count)
                // Brushes this update left with stale welding/T-junction inputs (StoreLoopVerticesJob);
                // read back on the main thread to dirty them for another round.
                public NativeList<CompactNodeID>            staleLoopBrushes;
                // Diagnostics written by StoreLoopVerticesJob (see its kStats* indices); logged per round.
                public NativeArray<int>                     propagationStats;
                // Counters written by ExactCSGJob (ExactCSGStat); always allocated, a job cannot be scheduled without it.
                public NativeArray<int>                     exactCSGStats;
                // Every brush's bounds as its exact planes make it (ExactBrush.bounds), by node order: what the exact CSG's
                // broad phase sweeps instead of the brush meshes' bounds. Minimal when the exact CSG is off.
                public NativeList<MinMaxAABB>               exactBounds;
                // The ExactBrush entries this update replaced (ExactInputJob), disposed once the update is done
                public NativeList<BlobAssetReference<ExactBrush>> exactBrushDisposeList;
                // ExactCSGJob's exact output when captureExact is set (see ExactCSGCapture); minimal otherwise, since a job
                // cannot be scheduled without it. Only created when ExactCSGJob is scheduled.
                public NativeStream                         exactCapture;
                public NativeList<BrushIntersectWith>       brushIntersectionsWith;
                public NativeArray<int2>                    brushIntersectionsWithRange;
                public NativeList<IndexOrder>               brushesThatNeedIndirectUpdate;
                public NativeParallelHashSet<IndexOrder>    brushesThatNeedIndirectUpdateHashMap;

                public NativeList<BrushPair2>               uniqueBrushPairs;

                public NativeList<float3>                   outputSurfaceVertices;
                public NativeList<BrushIntersectionLoop>    outputSurfaces;
                public NativeArray<int2>                    outputSurfacesRange;

                public NativeArray<BlobAssetReference<BrushMeshBlob>> brushMeshLookup;
                public NativeArray<UnsafeList<float3>>      loopVerticesLookup;
                public NativeArray<UnsafeList<float3>>      loopVerticesLookupOut;
                public NativeArray<int>                     mergeBrushState;

                public NativeReference<int>                 surfaceCountRef;
                // How many intersection loops CreateIntersectionLoopsJob will write, counted by
                // CountIntersectionLoopsJob so its output list is reserved rather than guessed at.
                public NativeReference<int>                 intersectionLoopCountRef;
                public NativeReference<BlobAssetReference<CompactTree>> compactTreeRef;
                public NativeReference<bool>                needRemappingRef;

                public VertexBufferContents                 vertexBufferContents;

                public NativeList<int>                      nodeIDValueToNodeOrder;
                public NativeReference<int>                 nodeIDValueToNodeOrderOffsetRef;

                public NativeList<BrushData>                brushRenderData;
                // The copies of brush buffers the weld across the model changed (OutputModelWeld), written with brushRenderData
                public NativeList<BlobAssetReference<ChiselBrushRenderBuffer>> patchedRenderBuffers;
                public NativeList<SubMeshDescriptions>      subMeshDescriptions;
                public NativeArray<UnsafeList<SubMeshSurface>> subMeshSurfaces;

                public NativeList<ChiselMeshUpdate>         meshUpdatesColliders;
                public NativeList<ChiselMeshUpdate>         meshUpdatesRenderables;
                public NativeList<ChiselMeshUpdate>         meshUpdatesDebugVisualizations;

                public NativeList<BlobAssetReference<BasePolygonsBlob>>           basePolygonDisposeList;
                public NativeList<BlobAssetReference<BrushTreeSpaceVerticesBlob>> treeSpaceVerticesDisposeList;
                public NativeList<BlobAssetReference<BrushesTouchedByBrush>>      brushesTouchedByBrushDisposeList;
                public NativeList<BlobAssetReference<RoutingTable>>               routingTableDisposeList;
                public NativeList<BlobAssetReference<BrushTreeSpacePlanes>>       brushTreeSpacePlaneDisposeList;
                public NativeList<BlobAssetReference<ChiselBrushRenderBuffer>>    brushRenderBufferDisposeList;

                // Decals (ChiselDecalStore): this update's copy of the tree's decals, and where they changed
                public NativeArray<DecalVolume>             decalVolumes;
                public NativeArray<ChiselDecalTarget>       decalTargets;
                public NativeArray<MinMaxAABB>              changedDecalBounds;
            } 
            internal TemporariesStruct Temporaries;
            #endregion

            #region Sub tasks JobHandles
            internal enum JobHandleType
            {
                transformTreeBrushIndicesListJobHandle,
                brushesJobHandle,
                nodesJobHandle,
                parametersJobHandle,
                allKnownBrushMeshIndicesJobHandle,
                parameterCountsJobHandle,
                allBrushMeshIDsJobHandle,
                allTreeBrushIndexOrdersJobHandle,
                allUpdateBrushIndexOrdersJobHandle,
                brushIDValuesJobHandle,
                basePolygonCacheJobHandle,
                brushBrushIntersectionsJobHandle,
                brushBoundsSweepJobHandle,
                internedPlanesJobHandle,
                loopVerticesCacheJobHandle,
                staleLoopBrushesJobHandle,
                brushesTouchedByBrushCacheJobHandle,
                brushRenderBufferCacheJobHandle,
                brushRenderDataJobHandle,
                brushTreeSpacePlaneCacheJobHandle,
                brushMeshBlobsLookupJobHandle,
                hierarchyIDJobHandle,
                hierarchyListJobHandle,
                brushMeshLookupJobHandle,
                brushIntersectionsWithJobHandle,
                brushIntersectionsWithRangeJobHandle,
                brushesThatNeedIndirectUpdateHashMapJobHandle,
                brushesThatNeedIndirectUpdateJobHandle,
                brushTreeSpaceBoundCacheJobHandle,
                dataStream1JobHandle,
                dataStream2JobHandle,
                intersectingBrushesStreamJobHandle,
                loopVerticesLookupJobHandle,
                loopVerticesLookupOutJobHandle,
                mergeBrushStateJobHandle,
                meshQueriesJobHandle,
                nodeIDValueToNodeOrderArrayJobHandle,
                outputSurfaceVerticesJobHandle,
                outputSurfacesJobHandle,
                outputSurfacesRangeJobHandle,
                routingTableCacheJobHandle,
                rebuildTreeBrushIndexOrdersJobHandle,
                sectionsJobHandle,
                surfaceCountRefJobHandle,
                intersectionLoopCountRefJobHandle,
                compactTreeRefJobHandle,
                compactHierarchyJobHandle,
                needRemappingRefJobHandle,
                nodeIDValueToNodeOrderOffsetRefJobHandle,
                subMeshSurfacesJobHandle,
                subMeshDescriptionsJobHandle,
                treeSpaceVerticesCacheJobHandle,
                transformationCacheJobHandle,
                uniqueBrushPairsJobHandle,
                vertexBufferContents_renderDescriptorsJobHandle,
                vertexBufferContents_colliderDescriptorsJobHandle,
                vertexBufferContents_subMeshSectionsJobHandle,
                vertexBufferContents_meshesJobHandle,
                meshUpdatesJobHandle,
                colliderMeshUpdatesJobHandle,
                debugHelperMeshesJobHandle,
                renderMeshesJobHandle,
                vertexBufferContents_triangleBrushIndicesJobHandle,
                vertexBufferContents_meshDescriptionsJobHandle,
                meshDatasJobHandle,
                storeToCacheJobHandle,
                preMeshUpdateCombinedJobHandle,
                brushOutlineManagerJobHandle,
                decalVolumesJobHandle,
                exactBrushCacheJobHandle,
                Count
            }

            internal struct JobHandlesStruct
            {
                DualJobHandle[] m_Handles;
                // node id -> scheduled job handle, for the sparse dependency graph (see DualJobHandle / JobExtensions).
                System.Collections.Generic.List<JobHandle> m_NodeHandles;

                // Allocated once per TreeUpdate (pooled) and cleared on reuse.
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                public void Reset()
                {
                    if (m_Handles == null || m_Handles.Length != (int)JobHandleType.Count)
                        m_Handles = new DualJobHandle[(int)JobHandleType.Count];
                    else
                        System.Array.Clear(m_Handles, 0, m_Handles.Length);
                    if (m_NodeHandles == null)
                        m_NodeHandles = new System.Collections.Generic.List<JobHandle>(256);
                    else
                        m_NodeHandles.Clear();
                }

                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                public void SeedResourceWriter(JobHandleType type, JobHandle handle)
                {
                    m_NodeHandles.Add(handle);
                    m_Handles[(int)type].MergeExternalWriter(m_NodeHandles.Count - 1, handle);
                }

                public readonly ref DualJobHandle this[JobHandleType type]
                {
                    [MethodImpl(MethodImplOptions.AggressiveInlining)]
					get => ref m_Handles[(int)type];
                }

                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                public readonly ReadJobHandles Read(JobHandleType t0) { var r = ReadJobHandles.Create(m_Handles, m_NodeHandles); r.Add((int)t0); return r; }
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                public readonly ReadJobHandles Read(JobHandleType t0, JobHandleType t1) { var r = ReadJobHandles.Create(m_Handles, m_NodeHandles); r.Add((int)t0); r.Add((int)t1); return r; }
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                public readonly ReadJobHandles Read(JobHandleType t0, JobHandleType t1, JobHandleType t2) { var r = ReadJobHandles.Create(m_Handles, m_NodeHandles); r.Add((int)t0); r.Add((int)t1); r.Add((int)t2); return r; }
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                public readonly ReadJobHandles Read(JobHandleType t0, JobHandleType t1, JobHandleType t2, JobHandleType t3) { var r = ReadJobHandles.Create(m_Handles, m_NodeHandles); r.Add((int)t0); r.Add((int)t1); r.Add((int)t2); r.Add((int)t3); return r; }
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                public readonly ReadJobHandles Read(JobHandleType t0, JobHandleType t1, JobHandleType t2, JobHandleType t3, JobHandleType t4) { var r = ReadJobHandles.Create(m_Handles, m_NodeHandles); r.Add((int)t0); r.Add((int)t1); r.Add((int)t2); r.Add((int)t3); r.Add((int)t4); return r; }
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                public readonly ReadJobHandles Read(JobHandleType t0, JobHandleType t1, JobHandleType t2, JobHandleType t3, JobHandleType t4, JobHandleType t5) { var r = ReadJobHandles.Create(m_Handles, m_NodeHandles); r.Add((int)t0); r.Add((int)t1); r.Add((int)t2); r.Add((int)t3); r.Add((int)t4); r.Add((int)t5); return r; }
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                public readonly ReadJobHandles Read(JobHandleType t0, JobHandleType t1, JobHandleType t2, JobHandleType t3, JobHandleType t4, JobHandleType t5, JobHandleType t6) { var r = ReadJobHandles.Create(m_Handles, m_NodeHandles); r.Add((int)t0); r.Add((int)t1); r.Add((int)t2); r.Add((int)t3); r.Add((int)t4); r.Add((int)t5); r.Add((int)t6); return r; }
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                public readonly ReadJobHandles Read(JobHandleType t0, JobHandleType t1, JobHandleType t2, JobHandleType t3, JobHandleType t4, JobHandleType t5, JobHandleType t6, JobHandleType t7) { var r = ReadJobHandles.Create(m_Handles, m_NodeHandles); r.Add((int)t0); r.Add((int)t1); r.Add((int)t2); r.Add((int)t3); r.Add((int)t4); r.Add((int)t5); r.Add((int)t6); r.Add((int)t7); return r; }
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                public readonly ReadJobHandles Read(JobHandleType t0, JobHandleType t1, JobHandleType t2, JobHandleType t3, JobHandleType t4, JobHandleType t5, JobHandleType t6, JobHandleType t7, JobHandleType t8) { var r = ReadJobHandles.Create(m_Handles, m_NodeHandles); r.Add((int)t0); r.Add((int)t1); r.Add((int)t2); r.Add((int)t3); r.Add((int)t4); r.Add((int)t5); r.Add((int)t6); r.Add((int)t7); r.Add((int)t8); return r; }
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                public readonly ReadJobHandles Read(JobHandleType t0, JobHandleType t1, JobHandleType t2, JobHandleType t3, JobHandleType t4, JobHandleType t5, JobHandleType t6, JobHandleType t7, JobHandleType t8, JobHandleType t9) { var r = ReadJobHandles.Create(m_Handles, m_NodeHandles); r.Add((int)t0); r.Add((int)t1); r.Add((int)t2); r.Add((int)t3); r.Add((int)t4); r.Add((int)t5); r.Add((int)t6); r.Add((int)t7); r.Add((int)t8); r.Add((int)t9); return r; }
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                public readonly ReadJobHandles Read(JobHandleType t0, JobHandleType t1, JobHandleType t2, JobHandleType t3, JobHandleType t4, JobHandleType t5, JobHandleType t6, JobHandleType t7, JobHandleType t8, JobHandleType t9, JobHandleType t10) { var r = ReadJobHandles.Create(m_Handles, m_NodeHandles); r.Add((int)t0); r.Add((int)t1); r.Add((int)t2); r.Add((int)t3); r.Add((int)t4); r.Add((int)t5); r.Add((int)t6); r.Add((int)t7); r.Add((int)t8); r.Add((int)t9); r.Add((int)t10); return r; }
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                public readonly ReadJobHandles Read(JobHandleType t0, JobHandleType t1, JobHandleType t2, JobHandleType t3, JobHandleType t4, JobHandleType t5, JobHandleType t6, JobHandleType t7, JobHandleType t8, JobHandleType t9, JobHandleType t10, JobHandleType t11) { var r = ReadJobHandles.Create(m_Handles, m_NodeHandles); r.Add((int)t0); r.Add((int)t1); r.Add((int)t2); r.Add((int)t3); r.Add((int)t4); r.Add((int)t5); r.Add((int)t6); r.Add((int)t7); r.Add((int)t8); r.Add((int)t9); r.Add((int)t10); r.Add((int)t11); return r; }

                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                public readonly WriteJobHandles Write(JobHandleType t0) { var r = WriteJobHandles.Create(m_Handles, m_NodeHandles); r.Add((int)t0); return r; }
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                public readonly WriteJobHandles Write(JobHandleType t0, JobHandleType t1) { var r = WriteJobHandles.Create(m_Handles, m_NodeHandles); r.Add((int)t0); r.Add((int)t1); return r; }
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                public readonly WriteJobHandles Write(JobHandleType t0, JobHandleType t1, JobHandleType t2) { var r = WriteJobHandles.Create(m_Handles, m_NodeHandles); r.Add((int)t0); r.Add((int)t1); r.Add((int)t2); return r; }
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                public readonly WriteJobHandles Write(JobHandleType t0, JobHandleType t1, JobHandleType t2, JobHandleType t3) { var r = WriteJobHandles.Create(m_Handles, m_NodeHandles); r.Add((int)t0); r.Add((int)t1); r.Add((int)t2); r.Add((int)t3); return r; }
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                public readonly WriteJobHandles Write(JobHandleType t0, JobHandleType t1, JobHandleType t2, JobHandleType t3, JobHandleType t4) { var r = WriteJobHandles.Create(m_Handles, m_NodeHandles); r.Add((int)t0); r.Add((int)t1); r.Add((int)t2); r.Add((int)t3); r.Add((int)t4); return r; }
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                public readonly WriteJobHandles Write(JobHandleType t0, JobHandleType t1, JobHandleType t2, JobHandleType t3, JobHandleType t4, JobHandleType t5) { var r = WriteJobHandles.Create(m_Handles, m_NodeHandles); r.Add((int)t0); r.Add((int)t1); r.Add((int)t2); r.Add((int)t3); r.Add((int)t4); r.Add((int)t5); return r; }
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                public readonly WriteJobHandles Write(JobHandleType t0, JobHandleType t1, JobHandleType t2, JobHandleType t3, JobHandleType t4, JobHandleType t5, JobHandleType t6) { var r = WriteJobHandles.Create(m_Handles, m_NodeHandles); r.Add((int)t0); r.Add((int)t1); r.Add((int)t2); r.Add((int)t3); r.Add((int)t4); r.Add((int)t5); r.Add((int)t6); return r; }
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                public readonly WriteJobHandles Write(JobHandleType t0, JobHandleType t1, JobHandleType t2, JobHandleType t3, JobHandleType t4, JobHandleType t5, JobHandleType t6, JobHandleType t7) { var r = WriteJobHandles.Create(m_Handles, m_NodeHandles); r.Add((int)t0); r.Add((int)t1); r.Add((int)t2); r.Add((int)t3); r.Add((int)t4); r.Add((int)t5); r.Add((int)t6); r.Add((int)t7); return r; }
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                public readonly WriteJobHandles Write(JobHandleType t0, JobHandleType t1, JobHandleType t2, JobHandleType t3, JobHandleType t4, JobHandleType t5, JobHandleType t6, JobHandleType t7, JobHandleType t8) { var r = WriteJobHandles.Create(m_Handles, m_NodeHandles); r.Add((int)t0); r.Add((int)t1); r.Add((int)t2); r.Add((int)t3); r.Add((int)t4); r.Add((int)t5); r.Add((int)t6); r.Add((int)t7); r.Add((int)t8); return r; }
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                public readonly WriteJobHandles Write(JobHandleType t0, JobHandleType t1, JobHandleType t2, JobHandleType t3, JobHandleType t4, JobHandleType t5, JobHandleType t6, JobHandleType t7, JobHandleType t8, JobHandleType t9) { var r = WriteJobHandles.Create(m_Handles, m_NodeHandles); r.Add((int)t0); r.Add((int)t1); r.Add((int)t2); r.Add((int)t3); r.Add((int)t4); r.Add((int)t5); r.Add((int)t6); r.Add((int)t7); r.Add((int)t8); r.Add((int)t9); return r; }
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                public readonly WriteJobHandles Write(JobHandleType t0, JobHandleType t1, JobHandleType t2, JobHandleType t3, JobHandleType t4, JobHandleType t5, JobHandleType t6, JobHandleType t7, JobHandleType t8, JobHandleType t9, JobHandleType t10) { var r = WriteJobHandles.Create(m_Handles, m_NodeHandles); r.Add((int)t0); r.Add((int)t1); r.Add((int)t2); r.Add((int)t3); r.Add((int)t4); r.Add((int)t5); r.Add((int)t6); r.Add((int)t7); r.Add((int)t8); r.Add((int)t9); r.Add((int)t10); return r; }
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                public readonly WriteJobHandles Write(JobHandleType t0, JobHandleType t1, JobHandleType t2, JobHandleType t3, JobHandleType t4, JobHandleType t5, JobHandleType t6, JobHandleType t7, JobHandleType t8, JobHandleType t9, JobHandleType t10, JobHandleType t11) { var r = WriteJobHandles.Create(m_Handles, m_NodeHandles); r.Add((int)t0); r.Add((int)t1); r.Add((int)t2); r.Add((int)t3); r.Add((int)t4); r.Add((int)t5); r.Add((int)t6); r.Add((int)t7); r.Add((int)t8); r.Add((int)t9); r.Add((int)t10); r.Add((int)t11); return r; }
            }
            
            internal JobHandlesStruct JobHandles;
            #endregion
            
            #region MeshQueryComparer - Sort mesh mesh queries to help ensure consistency
            struct MeshQueryComparer : System.Collections.Generic.IComparer<MeshQuery>
            {
                public int Compare(MeshQuery x, MeshQuery y)
                {
                    if (x.LayerParameterIndex != y.LayerParameterIndex) return ((int)x.LayerParameterIndex) - ((int)y.LayerParameterIndex);
                    if (x.LayerQuery != y.LayerQuery) return ((int)x.LayerQuery) - ((int)y.LayerQuery);
                    return 0;
                }
            }

			readonly static MeshQueryComparer kMeshQueryComparer = new();
			#endregion

			#region Initialize Temporaries
			public void Initialize()
            {
                var chiselLookupValues = ChiselTreeLookup.Value[this.tree];

                // Make sure that if we, somehow, run this while parts of the previous update is still running, we wait for the previous run to complete
                chiselLookupValues.lastJobHandle.Complete();
                chiselLookupValues.lastJobHandle = default;

                // Reset everything
                JobHandles.Reset();
                Temporaries = default;
                subtractiveWorkflow = false;
                normalSmoothingAngle = -1f; // -1 means no smoothing
                lightmapUVSettings = LightmapUVSettings.Default;

                if (ModelSettingsStore.TryGet(UnityEngine.EntityId.ToULong(tree.EntityId), out var modelSettings))
                {
                    subtractiveWorkflow = modelSettings.SubtractiveWorkflow;
                    normalSmoothingAngle = modelSettings.NormalSmoothing ? math.clamp(modelSettings.NormalSmoothingAngle, 0.0f, 180.0f) : 0.0f;
                    lightmapUVSettings = new LightmapUVSettings { texelsPerUnit = modelSettings.LightmapTexelsPerUnit, paddingTexels = modelSettings.LightmapPaddingTexels }.Usable;
                }

                ref var compactHierarchy = ref CompactHierarchyManager.GetHierarchy(this.treeCompactNodeID);

                Temporaries.parameterCounts                = new NativeArray<int>(chiselLookupValues.parameters.Length, defaultAllocator);
                Temporaries.transformTreeBrushIndicesList  = new NativeList<NodeOrderNodeID>(defaultAllocator);
                Temporaries.nodes                          = new NativeList<CompactNodeID>(defaultAllocator);
                Temporaries.brushes                        = new NativeList<CompactNodeID>(defaultAllocator);

                compactHierarchy.GetTreeNodes(Temporaries.nodes, Temporaries.brushes);
                var newBrushCount = Temporaries.brushes.Length;
                this.brushCount   = newBrushCount;

                #region Allocations/Resize
                chiselLookupValues.EnsureCapacity(newBrushCount);

                this.maxNodeOrder = this.brushCount;
                this.canonicalVertexStage = (CanonicalVertexStage)math.clamp(kCanonicalVertexStage,
                                                                              (int)CanonicalVertexStage.Off,
                                                                              (int)CanonicalVertexStage.Everywhere);

                Temporaries.meshDataArray   = default;
                Temporaries.meshDatas       = new NativeList<UnityEngine.Mesh.MeshData>(defaultAllocator);

                Temporaries.brushesThatNeedIndirectUpdateHashMap = new NativeParallelHashSet<IndexOrder>(brushCount, defaultAllocator);
                Temporaries.brushesThatNeedIndirectUpdate   = new NativeList<IndexOrder>(brushCount, defaultAllocator);

                // TODO: find actual vertex count
                Temporaries.outputSurfaceVertices           = new NativeList<float3>(65535 * 10, defaultAllocator);

                Temporaries.outputSurfaces                  = new NativeList<BrushIntersectionLoop>(brushCount * 32, defaultAllocator);
                Temporaries.brushIntersectionsWith          = new NativeList<BrushIntersectWith>(brushCount, defaultAllocator);

                Temporaries.nodeIDValueToNodeOrderOffsetRef = new NativeReference<int>(defaultAllocator);
                Temporaries.surfaceCountRef                 = new NativeReference<int>(defaultAllocator);
                Temporaries.intersectionLoopCountRef        = new NativeReference<int>(defaultAllocator);
                Temporaries.compactTreeRef                  = new NativeReference<BlobAssetReference<CompactTree>>(defaultAllocator);
                Temporaries.needRemappingRef                = new NativeReference<bool>(defaultAllocator);

                Temporaries.uniqueBrushPairs                = new NativeList<BrushPair2>(brushCount * 256, defaultAllocator);

                Temporaries.rebuildTreeBrushIndexOrders     = new NativeList<IndexOrder>(brushCount, defaultAllocator);
                Temporaries.allUpdateBrushIndexOrders       = new NativeList<IndexOrder>(brushCount, defaultAllocator);
                Temporaries.allBrushMeshIDs                 = new NativeArray<int>(brushCount, defaultAllocator);
                Temporaries.brushRenderData                 = new NativeList<BrushData>(brushCount, defaultAllocator);
                Temporaries.allTreeBrushIndexOrders         = new NativeList<IndexOrder>(brushCount, defaultAllocator);
                Temporaries.allTreeBrushIndexOrders.Clear();
                Temporaries.allTreeBrushIndexOrders.Resize(brushCount, NativeArrayOptions.ClearMemory);

                Temporaries.outputSurfacesRange             = new NativeArray<int2>(brushCount, defaultAllocator);
                Temporaries.brushIntersectionsWithRange     = new NativeArray<int2>(brushCount, defaultAllocator);
                Temporaries.nodeIDValueToNodeOrder          = new NativeList<int>(brushCount, defaultAllocator);
                Temporaries.brushMeshLookup                 = new NativeArray<BlobAssetReference<BrushMeshBlob>>(brushCount, defaultAllocator);

                Temporaries.brushBrushIntersections         = new NativeArray<UnsafeList<BrushIntersectWith>>(brushCount, defaultAllocator);
                Temporaries.brushBoundsSweep                = new NativeList<BrushBoundsSweepEntry>(brushCount, defaultAllocator);
                Temporaries.brushPlaneIds                  = new NativeList<int>(kInternBrushPlanes ? brushCount * 6 : 1, defaultAllocator);
                Temporaries.brushPlaneIdRange              = new NativeArray<int2>(kInternBrushPlanes ? brushCount : 1, defaultAllocator);
                if (kInternBrushPlanes)
                    Temporaries.internedPlanes             = new InternedPlanes(math.max(1024, brushCount), defaultAllocator);
                Temporaries.staleLoopBrushes                = new NativeList<CompactNodeID>(brushCount, defaultAllocator);
                Temporaries.propagationStats                = new NativeArray<int>(StoreLoopVerticesJob.kStatsCount(kMergeIterations), defaultAllocator);
                Temporaries.exactCSGStats                   = new NativeArray<int>((int)ExactCSGStat.Count, defaultAllocator);
                this.exactCSG                               = kExactCSG;
                this.captureExact                           = kExactCSG && ExactCSGCapture.Enabled;
                this.rebuildAllBrushes                      = chiselLookupValues.builtExact != exactCSG;
                chiselLookupValues.builtExact               = exactCSG;
                Temporaries.exactBounds                     = new NativeList<MinMaxAABB>(exactCSG ? math.max(1, brushCount) : 1, defaultAllocator);

                // The decals, and where they changed since the last update. The next update starts from here.
                Temporaries.decalVolumes                    = new NativeArray<DecalVolume>(chiselLookupValues.decalVolumes.AsArray(), defaultAllocator);
                Temporaries.decalTargets                    = new NativeArray<ChiselDecalTarget>(chiselLookupValues.decalVolumeTargets.AsArray(), defaultAllocator);
                Temporaries.changedDecalBounds              = new NativeArray<MinMaxAABB>(chiselLookupValues.changedDecalBounds.AsArray(), defaultAllocator);
                chiselLookupValues.changedDecalBounds.Clear();

                Temporaries.subMeshDescriptions             = new NativeList<SubMeshDescriptions>(defaultAllocator);

                Temporaries.meshUpdatesColliders            = new NativeList<ChiselMeshUpdate>(defaultAllocator);
                Temporaries.meshUpdatesRenderables          = new NativeList<ChiselMeshUpdate>(defaultAllocator);
                Temporaries.meshUpdatesDebugVisualizations  = new NativeList<ChiselMeshUpdate>(defaultAllocator);


                Temporaries.loopVerticesLookup              = new NativeArray<UnsafeList<float3>>(this.brushCount, defaultAllocator);
                Temporaries.loopVerticesLookupOut           = new NativeArray<UnsafeList<float3>>(this.brushCount, defaultAllocator);
                // Merge fixpoint worklist: one row of brushCount ran/changed flags per merge pass.
                // Pass 0 merges everything regardless, so this starts out cleared.
                Temporaries.mergeBrushState                = new NativeArray<int>(kMergeIterations * this.brushCount, defaultAllocator);

                Temporaries.vertexBufferContents.EnsureInitialized();

                // Regular index operator will return a copy instead of a reference *sigh*
                for (int l = 0; l < SurfaceDestinationParameters.ParameterCount; l++)
                {
                    var parameter = chiselLookupValues.parameters[l];
                    parameter.Clear();
                    chiselLookupValues.parameters[l] = parameter;
                }

                #region MeshQueries
                // TODO: have more control over the queries
                Temporaries.meshQueries         = MeshQuery.DefaultQueries.ToNativeArray(defaultAllocator);
                Temporaries.meshQueriesLength   = Temporaries.meshQueries.Length;
                Temporaries.meshQueries.Sort(kMeshQueryComparer);
                #endregion

                Temporaries.subMeshSurfaces = new NativeArray<UnsafeList<SubMeshSurface>>(Temporaries.meshQueriesLength, defaultAllocator);
                Temporaries.patchedRenderBuffers = new NativeList<BlobAssetReference<ChiselBrushRenderBuffer>>(defaultAllocator);
                
                Temporaries.subMeshDescriptions.Clear();

                Temporaries.allUpdateBrushIndexOrders.Clear();
                if (Temporaries.allUpdateBrushIndexOrders.Capacity < this.brushCount)
                    Temporaries.allUpdateBrushIndexOrders.Capacity = this.brushCount;


                Temporaries.brushesThatNeedIndirectUpdateHashMap.Clear();
                Temporaries.brushesThatNeedIndirectUpdate.Clear();

                if (chiselLookupValues.basePolygonCache.Length < newBrushCount)
                    chiselLookupValues.basePolygonCache.Resize(newBrushCount, NativeArrayOptions.ClearMemory);
                if (chiselLookupValues.routingTableCache.Length < newBrushCount)
                    chiselLookupValues.routingTableCache.Resize(newBrushCount, NativeArrayOptions.ClearMemory);
                if (chiselLookupValues.transformationCache.Length < newBrushCount)
                    chiselLookupValues.transformationCache.Resize(newBrushCount, NativeArrayOptions.ClearMemory);
                if (chiselLookupValues.brushRenderBufferCache.Length < newBrushCount)
                    chiselLookupValues.brushRenderBufferCache.Resize(newBrushCount, NativeArrayOptions.ClearMemory);
                if (chiselLookupValues.treeSpaceVerticesCache.Length < newBrushCount)
                    chiselLookupValues.treeSpaceVerticesCache.Resize(newBrushCount, NativeArrayOptions.ClearMemory);
                if (chiselLookupValues.brushTreeSpacePlaneCache.Length < newBrushCount)
                    chiselLookupValues.brushTreeSpacePlaneCache.Resize(newBrushCount, NativeArrayOptions.ClearMemory);
                if (chiselLookupValues.brushTreeSpaceBoundCache.Length < newBrushCount)
                    chiselLookupValues.brushTreeSpaceBoundCache.Resize(newBrushCount, NativeArrayOptions.ClearMemory);
                if (chiselLookupValues.brushesTouchedByBrushCache.Length < newBrushCount)
                    chiselLookupValues.brushesTouchedByBrushCache.Resize(newBrushCount, NativeArrayOptions.ClearMemory);
                if (chiselLookupValues.loopVerticesCache.Length < newBrushCount)
                    chiselLookupValues.loopVerticesCache.Resize(newBrushCount, NativeArrayOptions.ClearMemory);
                if (chiselLookupValues.exactBrushCache.Length < newBrushCount)
                    chiselLookupValues.exactBrushCache.Resize(newBrushCount, NativeArrayOptions.ClearMemory);

                Temporaries.basePolygonDisposeList           = new NativeList<BlobAssetReference<BasePolygonsBlob>>(chiselLookupValues.basePolygonCache.Length, defaultAllocator);
                Temporaries.treeSpaceVerticesDisposeList     = new NativeList<BlobAssetReference<BrushTreeSpaceVerticesBlob>>(chiselLookupValues.treeSpaceVerticesCache.Length, defaultAllocator);
                Temporaries.brushesTouchedByBrushDisposeList = new NativeList<BlobAssetReference<BrushesTouchedByBrush>>(chiselLookupValues.brushesTouchedByBrushCache.Length, defaultAllocator);
                Temporaries.routingTableDisposeList          = new NativeList<BlobAssetReference<RoutingTable>>(chiselLookupValues.routingTableCache.Length, defaultAllocator);
                Temporaries.brushTreeSpacePlaneDisposeList   = new NativeList<BlobAssetReference<BrushTreeSpacePlanes>>(chiselLookupValues.brushTreeSpacePlaneCache.Length, defaultAllocator);
                Temporaries.brushRenderBufferDisposeList     = new NativeList<BlobAssetReference<ChiselBrushRenderBuffer>>(chiselLookupValues.brushRenderBufferCache.Length, defaultAllocator);
                Temporaries.exactBrushDisposeList            = new NativeList<BlobAssetReference<ExactBrush>>(exactCSG ? math.max(1, chiselLookupValues.exactBrushCache.Length) : 1, defaultAllocator);

                #endregion
            }
            #endregion


            readonly static ProfilerMarker kScheduleGeneratorJobPoolProfilerMarker = new("CSG_ScheduleGeneratorJobPool");
			readonly static ProfilerMarker kTreeUpdateAllocateProfilerMarker = new("CSG_TreeUpdate_Allocate");
			readonly static ProfilerMarker kTreeUpdateInitializeProfilerMarker = new("CSG_TreeUpdate_Initialize");
			readonly static ProfilerMarker kRunMeshInitJobsProfilerMarker = new("CSG_RunMeshInitJobs");
			readonly static ProfilerMarker kRunMeshUpdateJobsProfilerMarker = new("CSG_RunMeshUpdateJobs");
			readonly static ProfilerMarker kClearFlagsProfilerMarker = new("CSG_ClearFlags");
			readonly static ProfilerMarker kFinishMeshUpdatesProfilerMarker = new("CSG_FinishMeshUpdates");
			readonly static ProfilerMarker kFreeTemporariesProfilerMarker = new("CSG_FreeTemporaries");
			readonly static ProfilerMarker kJobBuildLookupTablesJobProfilerMarker = new("Job_BuildLookupTablesJob");
			readonly static ProfilerMarker kJobCacheRemappingJobProfilerMarker = new("Job_CacheRemappingJob");
			readonly static ProfilerMarker kJobUpdateBrushIDValuesJobProfilerMarker = new("Job_UpdateBrushIDValuesJob");
			readonly static ProfilerMarker kJobFindModifiedBrushesJobProfilerMarker = new("Job_FindModifiedBrushesJob");
			readonly static ProfilerMarker kJobInvalidateBrushesJobProfilerMarker = new("Job_InvalidateBrushesJob");
			readonly static ProfilerMarker kJobFindDecalAffectedBrushesJobProfilerMarker = new("Job_FindDecalAffectedBrushesJob");
			readonly static ProfilerMarker kJobUpdateBrushMeshIDsJobProfilerMarker = new("Job_UpdateBrushMeshIDsJob");
			readonly static ProfilerMarker kJob_UpdateTransformationsJobProfilerMarker = new("Job_UpdateTransformationsJob");
			readonly static ProfilerMarker kJob_BuildCompactTreeJobProfilerMarker = new("Job_BuildCompactTreeJob");
			readonly static ProfilerMarker kJob_FillBrushMeshBlobLookupJobProfilerMarker = new("Job_FillBrushMeshBlobLookupJob");
			readonly static ProfilerMarker kJob_InvalidateBrushCacheJobProfilerMarker = new("Job_InvalidateBrushCacheJob");
			readonly static ProfilerMarker kJob_FixupBrushCacheIndicesJobProfilerMarker = new("Job_FixupBrushCacheIndicesJob");
			readonly static ProfilerMarker kJob_CreateTreeSpaceVerticesAndBoundsJobProfilerMarker = new("Job_CreateTreeSpaceVerticesAndBoundsJob");
			readonly static ProfilerMarker kJob_BuildBrushBoundsSweepProfilerMarker = new("Job_BuildBrushBoundsSweep");
			readonly static ProfilerMarker kJob_SeedLoopVerticesProfilerMarker = new("Job_SeedLoopVerticesFromCache");
			readonly static ProfilerMarker kJob_StoreLoopVerticesProfilerMarker = new("Job_StoreLoopVertices");
			readonly static ProfilerMarker kJob_FindAllBrushIntersectionPairsProfilerMarker = new("Job_FindAllBrushIntersectionPairs");
			readonly static ProfilerMarker kJob_FindUniqueIndirectBrushIntersectionsProfilerMarker = new("Job_FindUniqueIndirectBrushIntersections");
			readonly static ProfilerMarker kJob_InvalidateBrushCache_IndirectProfilerMarker = new("Job_InvalidateBrushCache_Indirect");
			readonly static ProfilerMarker kJob_CreateTreeSpaceVerticesAndBounds_IndirectProfilerMarker = new("Job_CreateTreeSpaceVerticesAndBounds_Indirect");
			readonly static ProfilerMarker kJob_FindAllBrushIntersectionPairs_IndirectProfilerMarker = new("Job_FindAllBrushIntersectionPairs_Indirect");
			readonly static ProfilerMarker kJob_AddIndirectUpdatedBrushesToListAndSortProfilerMarker = new("Job_AddIndirectUpdatedBrushesToListAndSort");
			readonly static ProfilerMarker kJob_GatherAndStoreBrushIntersectionsProfilerMarker = new("Job_GatherAndStoreBrushIntersections");
			readonly static ProfilerMarker kJob_PrepareBrushPairIntersectionsProfilerMarker = new("Job_PrepareBrushPairIntersections");
			readonly static ProfilerMarker kJob_GenerateBasePolygonLoopsProfilerMarker = new("Job_GenerateBasePolygonLoops");
			readonly static ProfilerMarker kJob_UpdateBrushTreeSpacePlanesProfilerMarker = new("Job_UpdateBrushTreeSpacePlanes");
			readonly static ProfilerMarker kJob_CreateIntersectionLoopsProfilerMarker = new("Job_CreateIntersectionLoops");
			readonly static ProfilerMarker kJob_GatherOutputSurfacesProfilerMarker = new("Job_GatherOutputSurfaces");
			readonly static ProfilerMarker kJob_FindLoopOverlapIntersectionsProfilerMarker = new("Job_FindLoopOverlapIntersections");
			readonly static ProfilerMarker kJob_MergeTouchingBrushVerticesIndirectProfilerMarker = new("Job_MergeTouchingBrushVerticesIndirect");
			readonly static ProfilerMarker kJob_UpdateBrushCategorizationTablesProfilerMarker = new("Job_UpdateBrushCategorizationTables");
			readonly static ProfilerMarker kJob_PerformCSGProfilerMarker = new("Job_PerformCSG");
			readonly static ProfilerMarker kJob_GenerateSurfaceTrianglesProfilerMarker = new("Job_GenerateSurfaceTriangles");
			readonly static ProfilerMarker kJob_FindBrushRenderBuffersProfilerMarker = new("Job_FindBrushRenderBuffers");
			readonly static ProfilerMarker kJob_AllocateSubMeshesProfilerMarker = new("Job_AllocateSubMeshes");
			readonly static ProfilerMarker kJob_PrepareSubSectionsProfilerMarker = new("Job_PrepareSubSections");
			readonly static ProfilerMarker kJob_SortSurfacesProfilerMarker = new("Job_SortSurfaces");
			readonly static ProfilerMarker kJob_GenerateMeshDescriptionProfilerMarker = new("Job_GenerateMeshDescription");
			readonly static ProfilerMarker kMesh_AllocateWritableMeshDataProfilerMarker = new("Mesh.AllocateWritableMeshData");
			readonly static ProfilerMarker kJob_CopyToMeshesProfilerMarker = new("Job_CopyToMeshes");
			readonly static ProfilerMarker kJob_StoreToCacheProfilerMarker = new("Job_StoreToCache");


			public static JobHandle ScheduleTreeMeshJobs(FinishMeshUpdate finishMeshUpdates, NativeList<CSGTree> trees, CanSkipTreeUpdate canSkipTreeUpdate = null)
            {
                var finalJobHandle = default(JobHandle);

                //
                // Schedule all the jobs that create new meshes based on our CSG trees
                //
                #region Schedule Generator Jobs
                var generatorPoolJobHandle = default(JobHandle);
                using (kScheduleGeneratorJobPoolProfilerMarker.Auto())
                { 
                    var runInParallel = runInParallelDefault;
                    generatorPoolJobHandle = GeneratorJobPoolManager.ScheduleJobs(runInParallel);
                }
                #endregion

                // TODO: make this unnecessary
                generatorPoolJobHandle.Complete();

				var treeUpdateLength = 0;
				var treeUpdates = ArrayPool<TreeUpdate>.Shared.Rent(trees.Length);
				try
                { 

				    //
				    // Make a list of valid modified trees
				    //
				    #region Find all modified, valid trees
                    using (kTreeUpdateAllocateProfilerMarker.Auto())
                    {
                        if (treeUpdates == null || treeUpdates.Length < trees.Length)
                            treeUpdates = new TreeUpdate[trees.Length];
                        for (int t = 0; t < trees.Length; t++)
                        {
                            var tree = trees[t];
                            var treeCompactNodeID = CompactHierarchyManager.GetCompactNodeID(tree);
                            ref var compactHierarchy = ref CompactHierarchyManager.GetHierarchy(treeCompactNodeID);

                            // Skip invalid trees since they wouldn't work anyway
                            if (!compactHierarchy.IsValidCompactNodeID(treeCompactNodeID))
                                continue;

                            if (!compactHierarchy.IsNodeDirty(treeCompactNodeID))
                                continue;

                            // Asked after the generators ran, so every brush has its mesh. A tree whose output is still
                            // what its input builds isn't built (see Flush(FinishMeshUpdate, CanSkipTreeUpdate)).
                            if (canSkipTreeUpdate != null && canSkipTreeUpdate(tree))
                            {
                                SkipTreeUpdate(tree, treeCompactNodeID);
                                continue;
                            }
                            s_SkippedTrees.Remove(tree.NodeID);

                            ref var treeUpdate = ref treeUpdates[treeUpdateLength];
                            treeUpdate.tree = tree;
                            treeUpdate.treeCompactNodeID = treeCompactNodeID;
                            treeUpdate.propagationRequested = false;
                            treeUpdate.skipMeshGeneration = false;
                            treeUpdate.brushListChanged = false;
                            treeUpdateLength++;
                        }

                        if (treeUpdateLength == 0)
                        {
                            s_Log.Append("round ").Append(s_PropagationRound).Append(": no valid dirty trees (of ").Append(trees.Length).Append(")\n");
                            return finalJobHandle;
                        }
                    }
                    #endregion

                    //
                    // Initialize our data structures
                    //
                    #region Initialize temporaries
                    using (kTreeUpdateInitializeProfilerMarker.Auto())
                    {
                        for (int t = 0; t < treeUpdateLength; t++)
                        {
                            treeUpdates[t].Initialize();
                        }
                    }
                    #endregion

					try
                    {
                        try
                        {
                            //
                            // Preprocess the data we need to perform CSG, figure what needs to be updated in the tree (might be nothing)
                            //
                            #region Schedule cache update jobs
                            using (kRunMeshInitJobsProfilerMarker.Auto())
                            {
                                for (int t = 0; t < treeUpdateLength; t++)
                                {
                                    treeUpdates[t].RunMeshInitJobs(generatorPoolJobHandle);
                                }
                            }
                            #endregion

                            //
                            // Schedule chain of jobs that will generate our surface meshes 
                            // At this point we need previously scheduled jobs to be completed so we know what actually needs to be updated, if anything
                            //
                            #region Schedule CSG jobs
                            using (kRunMeshUpdateJobsProfilerMarker.Auto())
                            {
                                // Reverse order since we sorted the trees from big to small & small trees are more likely to have already completed

                                JobHandle sharedCompactHierarchy = default;
                                for (int t = 0; t < treeUpdateLength; t++)
                                {
                                    ref var initialised = ref treeUpdates[t];
                                    sharedCompactHierarchy = JobHandle.CombineDependencies(
                                        sharedCompactHierarchy,
                                        initialised.JobHandles[JobHandleType.compactHierarchyJobHandle].readWriteBarrier);
                                }
                                for (int t = treeUpdateLength - 1; t >= 0; t--)
                                {
                                    ref var treeUpdate = ref treeUpdates[t];
                                    // TODO: figure out if there's a way around this ....
                                    treeUpdate.JobHandles[JobHandleType.transformTreeBrushIndicesListJobHandle].readWriteBarrier.Complete();
                                    treeUpdate.JobHandles[JobHandleType.rebuildTreeBrushIndexOrdersJobHandle].writeBarrier.Complete();
                                    treeUpdate.JobHandles[JobHandleType.needRemappingRefJobHandle].writeBarrier.Complete();
                                    treeUpdate.updateCount = treeUpdate.Temporaries.rebuildTreeBrushIndexOrders.Length;
                                    // An empty tree has nothing to rebuild, and gets its meshes cleared below
                                    treeUpdate.brushListChanged = treeUpdate.brushCount > 0 && treeUpdate.Temporaries.needRemappingRef.Value;

                                    if (treeUpdate.updateCount <= 0 &&
                                        !treeUpdate.brushListChanged)
                                        continue;

                                    treeUpdate.JobHandles.SeedResourceWriter(JobHandleType.compactHierarchyJobHandle, sharedCompactHierarchy);

                                    treeUpdate.RunMeshUpdateJobs();

                                    // Carry this tree's accumulated hierarchy access forward so the next tree depends on it.
                                    sharedCompactHierarchy = treeUpdate.JobHandles[JobHandleType.compactHierarchyJobHandle].readWriteBarrier;
                                }
                            }
                            #endregion
                        }
                        finally
                        {
						    //
						    // Dispose temporaries that we don't need anymore
						    //
						    #region Cleanup
						    for (int t = 0; t < treeUpdateLength; t++)
                            {
                                ref var treeUpdate = ref treeUpdates[t];
                                treeUpdate.PreMeshUpdateDispose();
                            }
						    #endregion
					    }

					    //
					    // Wait for our scheduled mesh update jobs to finish, ensure our components are setup correctly, and upload our mesh data to the meshes
					    //

					    for (int t = 0; t < treeUpdateLength; t++)
                        {
                            ref var treeUpdate = ref treeUpdates[t];
                            treeUpdate.dependencies = JobHandleExtensions.CombineDependencies(treeUpdate.JobHandles[JobHandleType.compactHierarchyJobHandle].readWriteBarrier,
																						      treeUpdate.JobHandles[JobHandleType.meshDatasJobHandle].writeBarrier,
                                                                                              treeUpdate.JobHandles[JobHandleType.meshUpdatesJobHandle].writeBarrier,
                                                                                              treeUpdate.JobHandles[JobHandleType.colliderMeshUpdatesJobHandle].writeBarrier,
                                                                                              treeUpdate.JobHandles[JobHandleType.debugHelperMeshesJobHandle].writeBarrier,
                                                                                              treeUpdate.JobHandles[JobHandleType.renderMeshesJobHandle].writeBarrier,
                                                                                              treeUpdate.JobHandles[JobHandleType.vertexBufferContents_triangleBrushIndicesJobHandle].writeBarrier,
                                                                                              treeUpdate.JobHandles[JobHandleType.vertexBufferContents_meshesJobHandle].writeBarrier);

                            // TODO: get rid of these crazy legacy flags
                            #region Clear tree/brush status flags 
                            ref var compactHierarchy = ref CompactHierarchyManager.GetHierarchy(treeUpdate.treeCompactNodeID);
                            using (kClearFlagsProfilerMarker.Auto())
                            {
                                compactHierarchy.ClearAllStatusFlags(treeUpdate.treeCompactNodeID);
                                for (int b = 0; b < treeUpdate.brushCount; b++)
                                {
                                    var brushIndexOrder = treeUpdate.Temporaries.allTreeBrushIndexOrders[b];
                                    compactHierarchy.ClearAllStatusFlags(brushIndexOrder.compactNodeID);
                                }
                            }
                            #endregion

                            if (treeUpdate.updateCount <= 0 &&
                                !treeUpdate.brushListChanged &&
                                treeUpdate.brushCount > 0)
                                continue;

                            //
                            // Call delegate to convert the generated meshes in whatever we need 
                            //  For example, it could create/update Meshes/MeshRenderers/MeshFilters/Gameobjects etc.
                            //  But it could eventually, optionally, output entities instead at some point
                            //
                            #region Finish Mesh Updates
                            if (finishMeshUpdates != null && !treeUpdate.skipMeshGeneration)
                            {
                                using (kFinishMeshUpdatesProfilerMarker.Auto())
                                {
                                    var meshUpdates = new ChiselMeshUpdates()
                                    {
									    vertexBufferContents            = treeUpdate.Temporaries.vertexBufferContents,
									    meshDataArray                   = treeUpdate.Temporaries.meshDataArray,
									    meshUpdatesColliders            = treeUpdate.Temporaries.meshUpdatesColliders,
									    meshUpdatesRenderables          = treeUpdate.Temporaries.meshUpdatesRenderables,
									    meshUpdatesDebugVisualizations  = treeUpdate.Temporaries.meshUpdatesDebugVisualizations
                                    };
								    var usedMeshCount = finishMeshUpdates(treeUpdate.tree, meshUpdates, treeUpdate.dependencies);
                                    treeUpdate.Temporaries.meshDataArray = default;
							    }
                            }
                            #endregion
                        }
                    }
                    finally
                    {
                        #region Ensure meshes are cleaned up
                        for (int t = 0; t < treeUpdateLength; t++)
                        {
                            ref var treeUpdate = ref treeUpdates[t];

                            // Error or not, our jobs need to be completed at this point
                            treeUpdate.dependencies.Complete();

                            treeUpdate.JobHandles[JobHandleType.staleLoopBrushesJobHandle].readWriteBarrier.Complete();
                            s_ModifiedBrushCount += treeUpdate.updateCount;
                            var staleCount = 0;
                            var dirtiedCount = 0;
                            if (s_PropagationRound + 1 < kMaxPropagationRounds &&
                                treeUpdate.Temporaries.staleLoopBrushes.IsCreated)
                            {
                                var staleLoopBrushes = treeUpdate.Temporaries.staleLoopBrushes;
                                staleCount = staleLoopBrushes.Length;
                                for (int i = 0; i < staleLoopBrushes.Length; i++)
                                {
                                    if (SetDirty(staleLoopBrushes[i]))
                                    {
                                        s_PropagationRequested = true;
                                        treeUpdate.propagationRequested = true;
                                        s_StaleBrushCount++;
                                        dirtiedCount++;
                                    }
                                }
                            }
                            if (treeUpdate.Temporaries.propagationStats.IsCreated)
                            {
                                var stats = treeUpdate.Temporaries.propagationStats;
                                s_Log.Append("    merge changed/pass:");
                                for (int p = 0; p < kMergeIterations; p++) s_Log.Append(' ').Append(stats[p]);
                                s_Log.Append("  brushesWithChangedLoops=").Append(stats[kMergeIterations])
                                     .Append(" changedVertices=").Append(stats[kMergeIterations + 1])
                                     .Append(" marksByChange=").Append(stats[kMergeIterations + 2])
                                     .Append(" marksByStillMoving=").Append(stats[kMergeIterations + 3])
                                     .Append('\n');
                            }
                            s_Log.Append("round ").Append(s_PropagationRound)
                                 .Append(": tree ").Append(t)
                                 .Append(" brushes=").Append(treeUpdate.brushCount)
                                 .Append(" modified=").Append(treeUpdate.updateCount)
                                 .Append(" stale=").Append(staleCount)
                                 .Append(" dirtied=").Append(dirtiedCount)
                                 .Append('\n');

                            // Ensure our meshDataArray ends up being disposed, even if we had errors
                            if (treeUpdate.Temporaries.meshDataArray.Length > 0)
                            {
                                try { treeUpdate.Temporaries.meshDataArray.Dispose(); } catch { }
                            }
                            treeUpdate.Temporaries.meshDataArray = default;
                        }
                        #endregion

                        #region Free temporaries
                        // TODO: most of these disposes can be scheduled before we complete and write to the meshes, 
                        // so that these disposes can happen at the same time as the mesh updates in finishMeshUpdates
                        using (kFreeTemporariesProfilerMarker.Auto())
                        {
                            using var freeJobs = new JobHandleAccumulator(treeUpdateLength, Allocator.Temp);
                            for (int t = 0; t < treeUpdateLength; t++)
                            {
                                ref var treeUpdate = ref treeUpdates[t];
								freeJobs.Add(treeUpdate.FreeTemporaries(ref finalJobHandle));
                                treeUpdate.Temporaries = default;
                            }
                            GeneratorJobPoolManager.Clear();
                            freeJobs.Combine().Complete();
						}
                        #endregion
                    }
                    return finalJobHandle;
				}
				catch (System.Exception exception)
				{
					// The update failed partway through. The outer finally marks the affected tree(s) clean so we
					// don't retry the same failure every frame; surface the cause here instead of silently dropping it.
					UnityEngine.Debug.LogException(exception);
					UnityEngine.Debug.LogError($"CSG mesh update failed for {treeUpdateLength} tree(s); they were marked as no longer needing an update to avoid retrying the same failure every frame.");
					return finalJobHandle;
				}
				finally
				{
					for (int t = 0; t < treeUpdateLength; t++)
					{
					    try
					    {
					        var treeCompactNodeID = treeUpdates[t].treeCompactNodeID;
					        ref var compactHierarchy = ref CompactHierarchyManager.GetHierarchy(treeCompactNodeID);
					        compactHierarchy.ClearAllStatusFlags(treeCompactNodeID);
					        // ... except when this update dirtied brushes of this tree for another propagation round
					        // (see kMaxPropagationRounds): keep it flagged so UpdateAllTreeMeshes picks it up again.
					        if (treeUpdates[t].propagationRequested)
					            compactHierarchy.SetStatusFlag(treeCompactNodeID, NodeStatusFlags.TreeNeedsUpdate);
					    }
					    catch { } // Preserve the original exception.
					}

					ArrayPool<TreeUpdate>.Shared.Return(treeUpdates);
				}
			}

            public void RunMeshInitJobs(JobHandle dependsOn)
			{
				// TODO: Try to get rid of this Complete
				dependsOn.Complete(); // <-- Initialize has code that depends on the current state of the tree
				dependsOn = default;

				var chiselLookupValues = ChiselTreeLookup.Value[this.tree];
                ref var brushMeshBlobs = ref ChiselMeshLookup.Value.brushMeshBlobCache;
                {
                    #region Build Lookup Tables
                    using (kJobBuildLookupTablesJobProfilerMarker.Auto())
                    {
                        const bool runInParallel = runInParallelDefault;
                        var buildLookupTablesJob = new BuildLookupTablesJob
                        {
                            // Read
                            brushes                         = Temporaries.brushes,
                            brushCount                      = this.brushCount,

                            // Read/Write
                            nodeIDValueToNodeOrder          = Temporaries.nodeIDValueToNodeOrder,

                            // Write
                            nodeIDValueToNodeOrderOffsetRef = Temporaries.nodeIDValueToNodeOrderOffsetRef,
                            allTreeBrushIndexOrders         = Temporaries.allTreeBrushIndexOrders
                        };
                        buildLookupTablesJob.Schedule(runInParallel,
                            JobHandles.Read(
                                JobHandleType.brushesJobHandle),
                            JobHandles.Write(
                                JobHandleType.nodeIDValueToNodeOrderArrayJobHandle,
                                JobHandleType.nodeIDValueToNodeOrderOffsetRefJobHandle,
                                JobHandleType.allTreeBrushIndexOrdersJobHandle));
                    }
                    #endregion

                    #region CacheRemapping
                    using (kJobCacheRemappingJobProfilerMarker.Auto())
                    {
                        const bool runInParallel = false;// runInParallelDefault;
                        // TODO: update "previous siblings" when something with an intersection operation has been modified
                        var cacheRemappingJob = new CacheRemappingJob
                        {
                            // Read
                            nodeIDValueToNodeOrder          = Temporaries.nodeIDValueToNodeOrder,
                            nodeIDValueToNodeOrderOffsetRef = Temporaries.nodeIDValueToNodeOrderOffsetRef,
                            brushes                         = Temporaries.brushes,
                            brushCount                      = this.brushCount,
                            allTreeBrushIndexOrders         = Temporaries.allTreeBrushIndexOrders,
                            brushIDValues                   = chiselLookupValues.brushIDValues,
							compactHierarchy                = CompactHierarchyManager.GetReadOnlyHierarchy(treeCompactNodeID),
							
							// Read/Write
							basePolygonCache                = chiselLookupValues.basePolygonCache,
                            routingTableCache               = chiselLookupValues.routingTableCache,
                            transformationCache             = chiselLookupValues.transformationCache,
                            brushRenderBufferCache          = chiselLookupValues.brushRenderBufferCache,
                            treeSpaceVerticesCache          = chiselLookupValues.treeSpaceVerticesCache,
                            brushTreeSpacePlaneCache        = chiselLookupValues.brushTreeSpacePlaneCache,
                            brushTreeSpaceBoundCache        = chiselLookupValues.brushTreeSpaceBoundCache,
                            brushesTouchedByBrushCache      = chiselLookupValues.brushesTouchedByBrushCache,
                            loopVerticesCache               = chiselLookupValues.loopVerticesCache,
                            exactBrushCache                 = chiselLookupValues.exactBrushCache,

                            // Write
                            brushesThatNeedIndirectUpdateHashMap    = Temporaries.brushesThatNeedIndirectUpdateHashMap,
                            needRemappingRef                        = Temporaries.needRemappingRef
                        };
                        cacheRemappingJob.Schedule(runInParallel,
                            JobHandles.Read(
                                JobHandleType.nodeIDValueToNodeOrderArrayJobHandle,
                                JobHandleType.nodeIDValueToNodeOrderOffsetRefJobHandle,
                                JobHandleType.brushesJobHandle,
                                JobHandleType.allTreeBrushIndexOrdersJobHandle,
                                JobHandleType.brushIDValuesJobHandle),
                            JobHandles.Write(
                                JobHandleType.basePolygonCacheJobHandle,
                                JobHandleType.routingTableCacheJobHandle,
                                JobHandleType.transformationCacheJobHandle,
                                JobHandleType.brushRenderBufferCacheJobHandle,
                                JobHandleType.treeSpaceVerticesCacheJobHandle,
                                JobHandleType.brushTreeSpacePlaneCacheJobHandle,
                                JobHandleType.brushTreeSpaceBoundCacheJobHandle,
                                JobHandleType.brushesTouchedByBrushCacheJobHandle,
                                JobHandleType.loopVerticesCacheJobHandle,
                                JobHandleType.exactBrushCacheJobHandle,
                                JobHandleType.brushesThatNeedIndirectUpdateHashMapJobHandle,
                                JobHandleType.needRemappingRefJobHandle));
                    }
                    #endregion

                    #region Update BrushID Values
                    using (kJobUpdateBrushIDValuesJobProfilerMarker.Auto())
                    {
                        const bool runInParallel = runInParallelDefault;
                        var updateBrushIDValuesJob = new UpdateBrushIDValuesJob
                        {
                            // Read
                            brushes         = Temporaries.brushes,
                            brushCount      = this.brushCount,

                            // Read/Write
                            brushIDValues   = chiselLookupValues.brushIDValues
                        };
                        updateBrushIDValuesJob.Schedule(runInParallel,
                            JobHandles.Read(
                                JobHandleType.brushesJobHandle),
                            JobHandles.Write(
                                JobHandleType.brushIDValuesJobHandle));
                    }
                    #endregion

                    #region Find Modified Brushes
                    using (kJobFindModifiedBrushesJobProfilerMarker.Auto())
                    {
                        const bool runInParallel = runInParallelDefault;
                        Temporaries.transformTreeBrushIndicesList.Clear();
                        if (Temporaries.transformTreeBrushIndicesList.Capacity < this.brushCount)
                            Temporaries.transformTreeBrushIndicesList.SetCapacity(this.brushCount);
                        var findModifiedBrushesJob = new FindModifiedBrushesJob
                        {
                            // Read
                            brushes                       = Temporaries.brushes,
                            brushCount                    = this.brushCount,
                            allTreeBrushIndexOrders       = Temporaries.allTreeBrushIndexOrders,
                            compactHierarchy              = CompactHierarchyManager.GetReadOnlyHierarchy(treeCompactNodeID),
                            rebuildAll                    = this.rebuildAllBrushes,

                            // Read/Write
                            rebuildTreeBrushIndexOrders   = Temporaries.rebuildTreeBrushIndexOrders,

                            // Write
                            transformTreeBrushIndicesList = Temporaries.transformTreeBrushIndicesList.AsParallelWriter()
                        };
                        var handle = findModifiedBrushesJob.Schedule(runInParallel,
                            JobHandles.Read(
                                JobHandleType.brushesJobHandle,
                                JobHandleType.allTreeBrushIndexOrdersJobHandle),
                            JobHandles.Write(
                                JobHandleType.compactHierarchyJobHandle,
                                JobHandleType.rebuildTreeBrushIndexOrdersJobHandle,
                                JobHandleType.transformTreeBrushIndicesListJobHandle));
                        //handle.Complete();
                    }
                    #endregion

                    #region Find Decal Affected Brushes
                    using (kJobFindDecalAffectedBrushesJobProfilerMarker.Auto())
                    {
                        const bool runInParallel = runInParallelDefault;
                        var findDecalAffectedBrushesJob = new FindDecalAffectedBrushesJob
                        {
                            // Read
                            changedDecalBounds          = Temporaries.changedDecalBounds,
                            brushTreeSpaceBoundCache    = chiselLookupValues.brushTreeSpaceBoundCache,
                            allTreeBrushIndexOrders     = Temporaries.allTreeBrushIndexOrders,
                            brushCount                  = this.brushCount,

                            // Read/Write
                            rebuildTreeBrushIndexOrders = Temporaries.rebuildTreeBrushIndexOrders
                        };
                        findDecalAffectedBrushesJob.Schedule(runInParallel,
                            JobHandles.Read(
                                JobHandleType.decalVolumesJobHandle,
                                JobHandleType.brushTreeSpaceBoundCacheJobHandle,
                                JobHandleType.allTreeBrushIndexOrdersJobHandle),
                            JobHandles.Write(
                                JobHandleType.rebuildTreeBrushIndexOrdersJobHandle));
                    }
                    #endregion

                    #region Invalidate Brushes
                    using (kJobInvalidateBrushesJobProfilerMarker.Auto())
                    {
                        const bool runInParallel = runInParallelDefault;
                        var invalidateBrushesJob = new InvalidateBrushesJob
                        {
                            // Read
                            needRemappingRef                = Temporaries.needRemappingRef,
                            rebuildTreeBrushIndexOrders     = Temporaries.rebuildTreeBrushIndexOrders,
                            brushesTouchedByBrushCache      = chiselLookupValues.brushesTouchedByBrushCache,
                            brushes                         = Temporaries.brushes,
                            brushCount                      = this.brushCount,
                            nodeIDValueToNodeOrder          = Temporaries.nodeIDValueToNodeOrder,
                            nodeIDValueToNodeOrderOffsetRef = Temporaries.nodeIDValueToNodeOrderOffsetRef,
                            compactHierarchy                = CompactHierarchyManager.GetReadOnlyHierarchy(treeCompactNodeID),

                            // Write
                            brushesThatNeedIndirectUpdateHashMap = Temporaries.brushesThatNeedIndirectUpdateHashMap
                        };
                        var jobHandle = invalidateBrushesJob.Schedule(runInParallel,
                            JobHandles.Read(
                                JobHandleType.needRemappingRefJobHandle,
                                JobHandleType.rebuildTreeBrushIndexOrdersJobHandle,
                                JobHandleType.brushesTouchedByBrushCacheJobHandle,
                                JobHandleType.brushesJobHandle,
                                JobHandleType.nodeIDValueToNodeOrderArrayJobHandle,
                                JobHandleType.nodeIDValueToNodeOrderOffsetRefJobHandle,
								JobHandleType.compactHierarchyJobHandle),
                            JobHandles.Write(
                                JobHandleType.brushesThatNeedIndirectUpdateHashMapJobHandle));
                        jobHandle.Complete(); // Required because unity is a buggy mess

					}
                    #endregion

                    #region Update BrushMesh IDs
                    using (kJobUpdateBrushMeshIDsJobProfilerMarker.Auto())
                    {
                        const bool runInParallel = runInParallelDefault;
                        var updateBrushMeshIDsJob = new UpdateBrushMeshIDsJob
                        {
                            // Read
                            brushMeshBlobs           = brushMeshBlobs,
                            brushCount               = this.brushCount,
                            brushes                  = Temporaries.brushes,
                            compactHierarchy         = CompactHierarchyManager.GetReadOnlyHierarchy(treeCompactNodeID),

                            // Read / Write
                            allKnownBrushMeshIndices = chiselLookupValues.allKnownBrushMeshIndices,
                            parameters               = chiselLookupValues.parameters,
                            parameterCounts          = Temporaries.parameterCounts,

                            // Write
                            allBrushMeshIDs          = Temporaries.allBrushMeshIDs
                        };
                        var jobHandle = updateBrushMeshIDsJob.Schedule(runInParallel,
                            JobHandles.Read(
                                JobHandleType.brushMeshBlobsLookupJobHandle,
                                JobHandleType.brushesJobHandle,
								JobHandleType.compactHierarchyJobHandle),
                            JobHandles.Write(
                                JobHandleType.allKnownBrushMeshIndicesJobHandle,
                                JobHandleType.parametersJobHandle,
                                JobHandleType.parameterCountsJobHandle,
                                JobHandleType.allBrushMeshIDsJobHandle));
                        //jobHandle.Complete();
					}
                    #endregion
                }
            }

            public void RunMeshUpdateJobs()
			{
				var chiselLookupValues = ChiselTreeLookup.Value[this.tree];
                ref var brushMeshBlobs = ref ChiselMeshLookup.Value.brushMeshBlobCache;

                #region Perform CSG

                #region Prepare

                #region Update Transformations
                using (kJob_UpdateTransformationsJobProfilerMarker.Auto())
                {
                    const bool runInParallel = runInParallelDefault;
                    var updateTransformationsJob = new UpdateTransformationsJob
                    {
                        // Read
                        transformTreeBrushIndicesList   = Temporaries.transformTreeBrushIndicesList,
                        compactHierarchy                = CompactHierarchyManager.GetReadOnlyHierarchy(treeCompactNodeID),

                        // Write
                        transformationCache             = chiselLookupValues.transformationCache
                    };
                    updateTransformationsJob.Schedule(runInParallel, Temporaries.transformTreeBrushIndicesList, 8,
                        JobHandles.Read(
                            JobHandleType.transformTreeBrushIndicesListJobHandle,
							JobHandleType.compactHierarchyJobHandle),
                        JobHandles.Write(
                            JobHandleType.transformationCacheJobHandle));
                }
                #endregion

                #region Build CSG Tree
                using (kJob_BuildCompactTreeJobProfilerMarker.Auto())
                {
                    const bool runInParallel = runInParallelDefault;
                    var buildCompactTreeJob = new BuildCompactTreeJob
                    {
                        // Read
                        treeCompactNodeID   = this.treeCompactNodeID,
                        contentsCount       = CompactHierarchyManager.ContentsCount,
                        brushes             = Temporaries.brushes,
                        nodes               = Temporaries.nodes,
                        compactHierarchy    = CompactHierarchyManager.GetReadOnlyHierarchy(treeCompactNodeID),

                        // Write
                        compactTreeRef      = Temporaries.compactTreeRef
                    };
                    var jobHandle = buildCompactTreeJob.Schedule(runInParallel,
                        JobHandles.Read(
                            JobHandleType.brushesJobHandle,
                            JobHandleType.nodesJobHandle,
                            JobHandleType.compactHierarchyJobHandle),
                        JobHandles.Write(
                            JobHandleType.compactTreeRefJobHandle));
                    //jobHandle.Complete();
				}
                #endregion

                #region Update BrushMeshBlob Lookup table
                // Create lookup table for all brushMeshBlobs, based on the node order in the tree
                using (kJob_FillBrushMeshBlobLookupJobProfilerMarker.Auto())
                {
                    const bool runInParallel = runInParallelDefault;
                    var fillBrushMeshBlobLookupJob = new FillBrushMeshBlobLookupJob
                    {
                        // Read
                        brushMeshBlobs          = brushMeshBlobs,
                        allTreeBrushIndexOrders = Temporaries.allTreeBrushIndexOrders,
                        allBrushMeshIDs         = Temporaries.allBrushMeshIDs,

                        // Write
                        brushMeshLookup = Temporaries.brushMeshLookup,
                        surfaceCountRef = Temporaries.surfaceCountRef
                    };
                    fillBrushMeshBlobLookupJob.Schedule(runInParallel,
                        JobHandles.Read(
                            JobHandleType.brushMeshBlobsLookupJobHandle,
                            JobHandleType.allTreeBrushIndexOrdersJobHandle,
                            JobHandleType.allBrushMeshIDsJobHandle),
                        JobHandles.Write(
                            JobHandleType.brushMeshLookupJobHandle,
                            JobHandleType.surfaceCountRefJobHandle));
                }
                #endregion

                #region Exact input
                if (exactCSG)
                {
                    const bool runInParallel = runInParallelDefault;
                    var exactInputJob = new ExactInputJob
                    {
                        // Read
                        rebuildTreeBrushIndexOrders = Temporaries.rebuildTreeBrushIndexOrders,
                        brushMeshLookup             = Temporaries.brushMeshLookup,
                        transformationCache         = chiselLookupValues.transformationCache,

                        // Read / Write
                        stats                       = Temporaries.exactCSGStats,
                        exactBrushCache             = chiselLookupValues.exactBrushCache,

                        // Write
                        disposeList                 = Temporaries.exactBrushDisposeList.AsParallelWriter()
                    };
                    exactInputJob.Schedule(runInParallel, Temporaries.rebuildTreeBrushIndexOrders, 1,
                        JobHandles.Read(
                            JobHandleType.rebuildTreeBrushIndexOrdersJobHandle,
                            JobHandleType.brushMeshLookupJobHandle,
                            JobHandleType.transformationCacheJobHandle),
                        JobHandles.Write(
                            JobHandleType.exactBrushCacheJobHandle));

                    var exactBoundsJob = new ExactBoundsJob
                    {
                        // Read
                        allTreeBrushIndexOrders     = Temporaries.allTreeBrushIndexOrders,
                        brushMeshLookup             = Temporaries.brushMeshLookup,
                        transformationCache         = chiselLookupValues.transformationCache,

                        // Read / Write
                        stats                       = Temporaries.exactCSGStats,
                        exactBrushCache             = chiselLookupValues.exactBrushCache,

                        // Write
                        bounds                      = Temporaries.exactBounds
                    };
                    exactBoundsJob.Schedule(false,
                        JobHandles.Read(
                            JobHandleType.allTreeBrushIndexOrdersJobHandle,
                            JobHandleType.brushMeshLookupJobHandle,
                            JobHandleType.transformationCacheJobHandle),
                        JobHandles.Write(
                            JobHandleType.exactBrushCacheJobHandle));
                }
                #endregion

                #region Invalidate outdated caches
                // Invalidate outdated caches for all modified brushes
                using (kJob_InvalidateBrushCacheJobProfilerMarker.Auto())
                {
                    const bool runInParallel = runInParallelDefault;
                    var invalidateBrushCacheJob = new InvalidateBrushCacheJob
                    {
                        // Read
                        rebuildTreeBrushIndexOrders = Temporaries.rebuildTreeBrushIndexOrders,

                        // Read/Write
                        basePolygonCache            = chiselLookupValues.basePolygonCache,
                        treeSpaceVerticesCache      = chiselLookupValues.treeSpaceVerticesCache,
                        brushesTouchedByBrushCache  = chiselLookupValues.brushesTouchedByBrushCache,
                        routingTableCache           = chiselLookupValues.routingTableCache,
                        brushTreeSpacePlaneCache    = chiselLookupValues.brushTreeSpacePlaneCache,
                        brushRenderBufferCache      = chiselLookupValues.brushRenderBufferCache,

                        // Write
                        basePolygonDisposeList           = Temporaries.basePolygonDisposeList.AsParallelWriter(),
                        routingTableDisposeList          = Temporaries.routingTableDisposeList.AsParallelWriter(),
                        brushRenderBufferDisposeList     = Temporaries.brushRenderBufferDisposeList.AsParallelWriter(),
                        treeSpaceVerticesDisposeList     = Temporaries.treeSpaceVerticesDisposeList.AsParallelWriter(),
                        brushTreeSpacePlaneDisposeList   = Temporaries.brushTreeSpacePlaneDisposeList.AsParallelWriter(),
                        brushesTouchedByBrushDisposeList = Temporaries.brushesTouchedByBrushDisposeList.AsParallelWriter()
                    };
                    invalidateBrushCacheJob.Schedule(runInParallel, Temporaries.rebuildTreeBrushIndexOrders, 16,
                        JobHandles.Read(
                            JobHandleType.rebuildTreeBrushIndexOrdersJobHandle),
                        JobHandles.Write(
                            JobHandleType.basePolygonCacheJobHandle,
                            JobHandleType.treeSpaceVerticesCacheJobHandle,
                            JobHandleType.brushesTouchedByBrushCacheJobHandle,
                            JobHandleType.routingTableCacheJobHandle,
                            JobHandleType.brushTreeSpacePlaneCacheJobHandle,
                            JobHandleType.brushRenderBufferCacheJobHandle));
				}
                #endregion

                #region Fixup brush cache data order
                // Fix up brush order index in cache data (ordering of brushes may have changed)
                using (kJob_FixupBrushCacheIndicesJobProfilerMarker.Auto())
                {
                    const bool runInParallel = runInParallelDefault;
                    var fixupBrushCacheIndicesJob = new FixupBrushCacheIndicesJob
                    {
                        // Read
                        allTreeBrushIndexOrders         = Temporaries.allTreeBrushIndexOrders,
                        nodeIDValueToNodeOrder          = Temporaries.nodeIDValueToNodeOrder,
                        nodeIDValueToNodeOrderOffsetRef = Temporaries.nodeIDValueToNodeOrderOffsetRef,

                        // Read Write
                        basePolygonCache                = chiselLookupValues.basePolygonCache,
                        brushesTouchedByBrushCache      = chiselLookupValues.brushesTouchedByBrushCache
                    };
                    fixupBrushCacheIndicesJob.Schedule(runInParallel, Temporaries.allTreeBrushIndexOrders, 16,
                        JobHandles.Read(
                            JobHandleType.allTreeBrushIndexOrdersJobHandle,
                            JobHandleType.nodeIDValueToNodeOrderArrayJobHandle,
                            JobHandleType.nodeIDValueToNodeOrderOffsetRefJobHandle),
                        JobHandles.Write(
                            JobHandleType.basePolygonCacheJobHandle,
                            JobHandleType.brushesTouchedByBrushCacheJobHandle));
                }
                #endregion

                #region Update brush tree space vertices and bounds
                // Create tree space vertices from local vertices + transformations & an AABB for each brush that has been modified
                using (kJob_CreateTreeSpaceVerticesAndBoundsJobProfilerMarker.Auto())
                {
                    const bool runInParallel = runInParallelDefault;
                    // TODO: should only do this once at creation time, part of brushMeshBlob? store with brush component itself
                    var createTreeSpaceVerticesAndBoundsJob = new CreateTreeSpaceVerticesAndBoundsJob
                    {
                        // Read
                        rebuildTreeBrushIndexOrders     = Temporaries.rebuildTreeBrushIndexOrders,
                        transformationCache             = chiselLookupValues.transformationCache,
                        brushMeshLookup                 = Temporaries.brushMeshLookup,

						// Read / Write
						compactHierarchyManager         = CompactHierarchyManager.AsReadWrite(),

                        // Write
                        brushTreeSpaceBounds            = chiselLookupValues.brushTreeSpaceBoundCache,
                        treeSpaceVerticesCache          = chiselLookupValues.treeSpaceVerticesCache
                    };
                    var jobHandle = createTreeSpaceVerticesAndBoundsJob.Schedule(runInParallel, Temporaries.rebuildTreeBrushIndexOrders, 16,
                        JobHandles.Read(
                            JobHandleType.rebuildTreeBrushIndexOrdersJobHandle,
                            JobHandleType.transformationCacheJobHandle,
							JobHandleType.brushMeshLookupJobHandle,
							//JobHandleType.brushMeshBlobsLookupJobHandle,
							JobHandleType.compactHierarchyJobHandle),
                        JobHandles.Write(
							JobHandleType.compactHierarchyJobHandle,
							JobHandleType.brushTreeSpaceBoundCacheJobHandle,
                            JobHandleType.treeSpaceVerticesCacheJobHandle));
					//jobHandle.Complete();
				}
                #endregion

                #region Build the bounds broad-phase
                // Sort-and-sweep over every brush's tree-space bounds, so the intersection-pair jobs below
                // only test the brushes whose bounds can actually overlap instead of every brush in the tree.
                using (kJob_BuildBrushBoundsSweepProfilerMarker.Auto())
                {
                    const bool runInParallel = runInParallelDefault;
                    var buildBrushBoundsSweepJob = new BuildBrushBoundsSweepJob
                    {
                        // Read (the exact CSG sweeps the bounds of the brushes as their exact planes make them)
                        allTreeBrushIndexOrders         = Temporaries.allTreeBrushIndexOrders,
                        brushTreeSpaceBounds            = exactCSG ? Temporaries.exactBounds : chiselLookupValues.brushTreeSpaceBoundCache,

                        // Write
                        sweepEntries                    = Temporaries.brushBoundsSweep
                    };
                    buildBrushBoundsSweepJob.Schedule(runInParallel,
                        JobHandles.Read(
                            JobHandleType.allTreeBrushIndexOrdersJobHandle,
                            JobHandleType.brushTreeSpaceBoundCacheJobHandle,
                            JobHandleType.exactBrushCacheJobHandle),
                        JobHandles.Write(
                            JobHandleType.brushBoundsSweepJobHandle));
                }
                #endregion

                #region Update intersection pairs
                // Find all pairs of brushes that intersect, for those brushes that have been modified
                using (kJob_FindAllBrushIntersectionPairsProfilerMarker.Auto())
                {
                    const bool runInParallel = runInParallelDefault;
                    // TODO: only change when brush or any touching brush has been added/removed or changes operation/order
                    var findAllBrushIntersectionPairsJob = new FindAllBrushIntersectionPairsJob
                    {
                        // Read
                        allTreeBrushIndexOrders         = Temporaries.allTreeBrushIndexOrders,
                        transformationCache             = chiselLookupValues.transformationCache,
                        brushMeshLookup                 = Temporaries.brushMeshLookup,
                        brushTreeSpaceBounds            = exactCSG ? Temporaries.exactBounds : chiselLookupValues.brushTreeSpaceBoundCache,
                        rebuildTreeBrushIndexOrders     = Temporaries.rebuildTreeBrushIndexOrders,
                        brushBoundsSweep                = Temporaries.brushBoundsSweep,
                        exactCSG                        = exactCSG,
                        exactBrushCache                 = chiselLookupValues.exactBrushCache,

                        // Read / Write
                        allocator                       = defaultAllocator,
                        brushBrushIntersections         = Temporaries.brushBrushIntersections,

                        // Write
                        brushesThatNeedIndirectUpdateHashMap = Temporaries.brushesThatNeedIndirectUpdateHashMap.AsParallelWriter()
                    };
                    findAllBrushIntersectionPairsJob.Schedule(runInParallel, Temporaries.rebuildTreeBrushIndexOrders, 16,
                        JobHandles.Read(
                            JobHandleType.allTreeBrushIndexOrdersJobHandle,
                            JobHandleType.transformationCacheJobHandle,
                            JobHandleType.brushMeshLookupJobHandle,
                            JobHandleType.brushTreeSpaceBoundCacheJobHandle,
                            JobHandleType.rebuildTreeBrushIndexOrdersJobHandle,
                            JobHandleType.brushBoundsSweepJobHandle,
                            JobHandleType.exactBrushCacheJobHandle),
                        JobHandles.Write(
                            JobHandleType.brushBrushIntersectionsJobHandle,
                            JobHandleType.brushesThatNeedIndirectUpdateHashMapJobHandle));
                }
                #endregion

                #region Update list of brushes that touch brushes
                // Find all brushes that touch the brushes that have been modified
                using (kJob_FindUniqueIndirectBrushIntersectionsProfilerMarker.Auto())
                {
                    const bool runInParallel = runInParallelDefault;
                    // TODO: optimize, use hashed grid
                    var findUniqueIndirectBrushIntersectionsJob = new FindUniqueIndirectBrushIntersectionsJob
                    {
                        // Read
                        brushesThatNeedIndirectUpdateHashMap = Temporaries.brushesThatNeedIndirectUpdateHashMap,

                        // Read / Write
                        brushesThatNeedIndirectUpdate = Temporaries.brushesThatNeedIndirectUpdate
                    };
                    findUniqueIndirectBrushIntersectionsJob.Schedule(runInParallel,
                        JobHandles.Read(
                            JobHandleType.brushesThatNeedIndirectUpdateHashMapJobHandle,
                            JobHandleType.brushesThatNeedIndirectUpdateJobHandle),
                        JobHandles.Write(
                            JobHandleType.brushesThatNeedIndirectUpdateJobHandle));
                }
                #endregion

                #region Invalidate indirectly outdated caches (when brush touches a brush that has changed)
                // Invalidate the cache for the brushes that have been indirectly modified (touch a brush that has changed)
                using (kJob_InvalidateBrushCache_IndirectProfilerMarker.Auto())
                {
                    const bool runInParallel = runInParallelDefault;
                    var invalidateIndirectBrushCacheJob = new InvalidateIndirectBrushCacheJob
                    {
                        // Read
                        brushesThatNeedIndirectUpdate = Temporaries.brushesThatNeedIndirectUpdate,

                        // Read/Write
                        basePolygonCache            = chiselLookupValues.basePolygonCache,
                        treeSpaceVerticesCache      = chiselLookupValues.treeSpaceVerticesCache,
                        brushesTouchedByBrushCache  = chiselLookupValues.brushesTouchedByBrushCache,
                        routingTableCache           = chiselLookupValues.routingTableCache,
                        brushTreeSpacePlaneCache    = chiselLookupValues.brushTreeSpacePlaneCache,
                        brushRenderBufferCache      = chiselLookupValues.brushRenderBufferCache
                    };
                    invalidateIndirectBrushCacheJob.Schedule(runInParallel, Temporaries.brushesThatNeedIndirectUpdate, 16,
                        JobHandles.Read(
                            JobHandleType.brushesThatNeedIndirectUpdateJobHandle),
                        JobHandles.Write(
                            JobHandleType.basePolygonCacheJobHandle,
                            JobHandleType.treeSpaceVerticesCacheJobHandle,
                            JobHandleType.brushesTouchedByBrushCacheJobHandle,
                            JobHandleType.routingTableCacheJobHandle,
                            JobHandleType.brushTreeSpacePlaneCacheJobHandle,
                            JobHandleType.brushRenderBufferCacheJobHandle));
                }
                #endregion

                #region Fixup indirectly brush cache data order (when brush touches a brush that has changed)
                // Create tree space vertices from local vertices + transformations & an AABB for each brush that has been indirectly modified
                using (kJob_CreateTreeSpaceVerticesAndBounds_IndirectProfilerMarker.Auto())
                {
                    const bool runInParallel = runInParallelDefault;
                    var createTreeSpaceVerticesAndBoundsJob = new CreateTreeSpaceVerticesAndBoundsJob
                    {
                        // Read
                        rebuildTreeBrushIndexOrders = Temporaries.brushesThatNeedIndirectUpdate,
                        transformationCache         = chiselLookupValues.transformationCache,
                        brushMeshLookup             = Temporaries.brushMeshLookup,

                        // Read / Write
						compactHierarchyManager     = CompactHierarchyManager.AsReadWrite(),

                        // Write
                        brushTreeSpaceBounds        = chiselLookupValues.brushTreeSpaceBoundCache,
                        treeSpaceVerticesCache      = chiselLookupValues.treeSpaceVerticesCache,
                    };
                    var jobHandle = createTreeSpaceVerticesAndBoundsJob.Schedule(runInParallel, Temporaries.brushesThatNeedIndirectUpdate, 16,
                        JobHandles.Read(                            
                            JobHandleType.brushesThatNeedIndirectUpdateJobHandle, //JobHandleType.rebuildTreeBrushIndexOrdersJobHandle,
                            JobHandleType.transformationCacheJobHandle,
							JobHandleType.brushMeshLookupJobHandle,
							//JobHandleType.brushMeshBlobsLookupJobHandle,
							JobHandleType.compactHierarchyJobHandle),
                        JobHandles.Write(
                            JobHandleType.compactHierarchyJobHandle,
							JobHandleType.brushTreeSpaceBoundCacheJobHandle,
                            JobHandleType.treeSpaceVerticesCacheJobHandle));
                    //jobHandle.Complete(); 
				} 
                #endregion

                #region Update intersection pairs (when brush touches a brush that has changed)
                // Find all pairs of brushes that intersect, for those brushes that have been indirectly modified
                using (kJob_FindAllBrushIntersectionPairs_IndirectProfilerMarker.Auto())
                {
                    const bool runInParallel = runInParallelDefault;
                    var findAllIndirectBrushIntersectionPairsJob = new FindAllIndirectBrushIntersectionPairsJob
                    {
                        // Read
                        allTreeBrushIndexOrders         = Temporaries.allTreeBrushIndexOrders,
                        transformationCache             = chiselLookupValues.transformationCache,
                        brushMeshLookup                 = Temporaries.brushMeshLookup,
                        brushTreeSpaceBounds            = exactCSG ? Temporaries.exactBounds : chiselLookupValues.brushTreeSpaceBoundCache,
                        brushesThatNeedIndirectUpdate   = Temporaries.brushesThatNeedIndirectUpdate,
                        brushBoundsSweep                = Temporaries.brushBoundsSweep,
                        exactCSG                        = exactCSG,
                        exactBrushCache                 = chiselLookupValues.exactBrushCache,

                        // Read / Write
                        allocator                       = defaultAllocator,
                        brushBrushIntersections         = Temporaries.brushBrushIntersections
                    };
                    findAllIndirectBrushIntersectionPairsJob.Schedule(runInParallel, Temporaries.brushesThatNeedIndirectUpdate, 1,
                        JobHandles.Read(
                            JobHandleType.allTreeBrushIndexOrdersJobHandle,
                            JobHandleType.transformationCacheJobHandle,
                            JobHandleType.brushMeshLookupJobHandle,
                            JobHandleType.brushTreeSpaceBoundCacheJobHandle,
                            JobHandleType.brushesThatNeedIndirectUpdateJobHandle,
                            JobHandleType.brushBoundsSweepJobHandle,
                            JobHandleType.exactBrushCacheJobHandle),
                        JobHandles.Write(
                            JobHandleType.brushBrushIntersectionsJobHandle));
                }
                #endregion

                #region Update list of brushes that touch brushes (when brush touches a brush that has changed)
                // Add brushes that need to be indirectly updated to our list of brushes that need updates
                using (kJob_AddIndirectUpdatedBrushesToListAndSortProfilerMarker.Auto())
                {
                    const bool runInParallel = runInParallelDefault;
                    var addIndirectUpdatedBrushesToListAndSortJob = new AddIndirectUpdatedBrushesToListAndSortJob
                    {
                        // Read
                        allTreeBrushIndexOrders         = Temporaries.allTreeBrushIndexOrders,
                        brushesThatNeedIndirectUpdate   = Temporaries.brushesThatNeedIndirectUpdate,
                        rebuildTreeBrushIndexOrders     = Temporaries.rebuildTreeBrushIndexOrders,

                        // Write
                        allUpdateBrushIndexOrders       = Temporaries.allUpdateBrushIndexOrders.AsParallelWriter(),
                    };
                    addIndirectUpdatedBrushesToListAndSortJob.Schedule(runInParallel,
                        JobHandles.Read(
                            JobHandleType.allTreeBrushIndexOrdersJobHandle,
                            JobHandleType.brushesThatNeedIndirectUpdateJobHandle,
                            JobHandleType.rebuildTreeBrushIndexOrdersJobHandle),
                        JobHandles.Write(
                            JobHandleType.allUpdateBrushIndexOrdersJobHandle));
                }
                #endregion

                #region Gather all brush intersections
                // Gather all found pairs of brushes that intersect with each other and cache them
                using (kJob_GatherAndStoreBrushIntersectionsProfilerMarker.Auto())
                {
                    const bool runInParallel = runInParallelDefault;
                    var gatherBrushIntersectionsJob = new GatherBrushIntersectionPairsJob
                    {
                        // Read
                        brushBrushIntersections     = Temporaries.brushBrushIntersections,

                        // Write
                        brushIntersectionsWithRange = Temporaries.brushIntersectionsWithRange,

                        // Read / Write
                        brushIntersectionsWith      = Temporaries.brushIntersectionsWith
                    };
                    gatherBrushIntersectionsJob.Schedule(runInParallel,
                        JobHandles.Read(
                            JobHandleType.brushBrushIntersectionsJobHandle,
                            JobHandleType.brushIntersectionsWithJobHandle),
                        JobHandles.Write(
                            JobHandleType.brushIntersectionsWithJobHandle,
                            JobHandleType.brushIntersectionsWithRangeJobHandle));

                    var storeBrushIntersectionsJob = new StoreBrushIntersectionsJob
                    {
                        // Read
                        treeCompactNodeID           = treeCompactNodeID,
                        compactTreeRef              = Temporaries.compactTreeRef,
                        allTreeBrushIndexOrders     = Temporaries.allTreeBrushIndexOrders,
                        allUpdateBrushIndexOrders   = Temporaries.allUpdateBrushIndexOrders,

                        brushIntersectionsWith      = Temporaries.brushIntersectionsWith,
                        brushIntersectionsWithRange = Temporaries.brushIntersectionsWithRange,

                        // Write
                        brushesTouchedByBrushCache = chiselLookupValues.brushesTouchedByBrushCache
                    };
                    storeBrushIntersectionsJob.Schedule(runInParallel, Temporaries.allUpdateBrushIndexOrders, 16,
                        JobHandles.Read(
                            JobHandleType.compactTreeRefJobHandle,
                            JobHandleType.allTreeBrushIndexOrdersJobHandle,
                            JobHandleType.allUpdateBrushIndexOrdersJobHandle,
                            JobHandleType.brushIntersectionsWithJobHandle,
                            JobHandleType.brushIntersectionsWithRangeJobHandle),
                        JobHandles.Write(
                            JobHandleType.brushesTouchedByBrushCacheJobHandle));
                }
                #endregion

                #endregion

                //
                // Determine all surfaces and intersections
                //

                NativeStream intersectingBrushesStream = default;
				#region Determine Intersection Surfaces
				// Find all pairs of brush intersections for each brush
				if (!exactCSG)
				using (kJob_PrepareBrushPairIntersectionsProfilerMarker.Auto())
                {
                    const bool runInParallel = runInParallelDefault;
                    var findBrushPairsJob = new FindBrushPairsJob
                    {
                        // Read
                        maxOrder                    = brushCount,
                        allUpdateBrushIndexOrders   = Temporaries.allUpdateBrushIndexOrders,
                        brushesTouchedByBrushes     = chiselLookupValues.brushesTouchedByBrushCache,

                        // Read (Re-allocate) / Write
                        uniqueBrushPairs            = Temporaries.uniqueBrushPairs
                    };
                    findBrushPairsJob.Schedule(runInParallel,
                        JobHandles.Read(
                            JobHandleType.allUpdateBrushIndexOrdersJobHandle,
                            JobHandleType.brushesTouchedByBrushCacheJobHandle),
                        JobHandles.Write(JobHandleType.uniqueBrushPairsJobHandle));

                    // The pairs are known now, and the brush meshes have been looked up since well
                    // before this, so what CreateIntersectionLoopsJob will write can be counted here.
                    var countIntersectionLoopsJob = new CountIntersectionLoopsJob
                    {
                        // Read
                        uniqueBrushPairs            = Temporaries.uniqueBrushPairs,
                        brushMeshLookup             = Temporaries.brushMeshLookup,

                        // Write
                        intersectionLoopCountRef    = Temporaries.intersectionLoopCountRef
                    };
                    countIntersectionLoopsJob.Schedule(runInParallel,
                        JobHandles.Read(
                            JobHandleType.uniqueBrushPairsJobHandle,
                            JobHandleType.brushMeshLookupJobHandle),
                        JobHandles.Write(JobHandleType.intersectionLoopCountRefJobHandle));

                    NativeCollection.ScheduleConstruct(runInParallel, out intersectingBrushesStream, Temporaries.uniqueBrushPairs,
                                                        JobHandles.Read(
                                                            JobHandleType.uniqueBrushPairsJobHandle
                                                            ),
                                                        JobHandles.Write(
                                                            JobHandleType.intersectingBrushesStreamJobHandle
                                                            ),
                                                        defaultAllocator);

                    if (kInternBrushPlanes)
                    {
                        var internBrushPlanesJob = new InternBrushPlanesJob
                        {
                            // Read
                            allUpdateBrushIndexOrders = Temporaries.allUpdateBrushIndexOrders,
                            brushMeshLookup           = Temporaries.brushMeshLookup,
                            transformationCache       = chiselLookupValues.transformationCache,

                            // Write
                            internedPlanes            = Temporaries.internedPlanes,
                            brushPlaneIds             = Temporaries.brushPlaneIds,
                            brushPlaneIdRange         = Temporaries.brushPlaneIdRange
                        };
                        internBrushPlanesJob.Schedule(runInParallel,
                            JobHandles.Read(
                                JobHandleType.allUpdateBrushIndexOrdersJobHandle,
                                JobHandleType.brushMeshLookupJobHandle,
                                JobHandleType.transformationCacheJobHandle),
                            JobHandles.Write(
                                JobHandleType.internedPlanesJobHandle));
                    }

                    var prepareBrushPairIntersectionsJob = new PrepareBrushPairIntersectionsJob
                    {
                        // Read
                        uniqueBrushPairs        = Temporaries.uniqueBrushPairs,
                        transformationCache     = chiselLookupValues.transformationCache,
                        brushMeshLookup         = Temporaries.brushMeshLookup,
                        // Shared plane identity; empty when kInternBrushPlanes is off, in which case
                        // the job falls back to comparing plane equations.
                        usePlaneIds             = kInternBrushPlanes && kUsePlaneIdsForAlignment,
                        brushPlaneIds           = Temporaries.brushPlaneIds,
                        brushPlaneIdRange       = Temporaries.brushPlaneIdRange,
                        canonicalAlignment      = kCanonicalAlignment && canonicalVertexStage >= CanonicalVertexStage.LoopIdentity,

                        // Write
                        intersectingBrushesStream = intersectingBrushesStream.AsWriter()
                    };
                    prepareBrushPairIntersectionsJob.Schedule(runInParallel, Temporaries.uniqueBrushPairs, 1,
                        JobHandles.Read(
                            JobHandleType.uniqueBrushPairsJobHandle,
                            JobHandleType.transformationCacheJobHandle,
                            JobHandleType.brushMeshLookupJobHandle,
                            JobHandleType.internedPlanesJobHandle),
                        JobHandles.Write(JobHandleType.intersectingBrushesStreamJobHandle));
                }

                using (kJob_UpdateBrushTreeSpacePlanesProfilerMarker.Auto())
                {
                    const bool runInParallel = runInParallelDefault;
                    // TODO: should only do this at creation time + when moved / store with brush component itself
                    var createBrushTreeSpacePlanesJob = new CreateBrushTreeSpacePlanesJob
                    {
                        // Read
                        allUpdateBrushIndexOrders   = Temporaries.allUpdateBrushIndexOrders,
                        brushMeshLookup             = Temporaries.brushMeshLookup,
                        transformationCache         = chiselLookupValues.transformationCache,

                        // Write
                        brushTreeSpacePlanes        = chiselLookupValues.brushTreeSpacePlaneCache
                    };
                    createBrushTreeSpacePlanesJob.Schedule(runInParallel, Temporaries.allUpdateBrushIndexOrders, 16,
                        JobHandles.Read(
                            JobHandleType.allUpdateBrushIndexOrdersJobHandle,
                            JobHandleType.brushMeshLookupJobHandle,
                            JobHandleType.transformationCacheJobHandle),
                        JobHandles.Write(
                            JobHandleType.brushTreeSpacePlaneCacheJobHandle));

                }

                // After the tree-space planes: canonical vertices place the brush corners with them.
                using (kJob_GenerateBasePolygonLoopsProfilerMarker.Auto())
                {
                    const bool runInParallel = runInParallelDefault;
                    // TODO: should only do this once at creation time, part of brushMeshBlob? store with brush component itself
                    var createBlobPolygonsBlobs = new CreateBlobPolygonsBlobsJob
                    {
                        // Read
                        allUpdateBrushIndexOrders   = Temporaries.allUpdateBrushIndexOrders,
                        brushesTouchedByBrushCache  = chiselLookupValues.brushesTouchedByBrushCache,
                        brushMeshLookup             = Temporaries.brushMeshLookup,
                        treeSpaceVerticesCache      = chiselLookupValues.treeSpaceVerticesCache,
                        canonicalVertexStage        = canonicalVertexStage,
                        brushTreeSpacePlaneCache    = chiselLookupValues.brushTreeSpacePlaneCache,

                        // Write
                        basePolygonCache            = chiselLookupValues.basePolygonCache
                    };
                    createBlobPolygonsBlobs.Schedule(runInParallel, Temporaries.allUpdateBrushIndexOrders, 16,
                        JobHandles.Read(
                            JobHandleType.allUpdateBrushIndexOrdersJobHandle,
                            JobHandleType.brushesTouchedByBrushCacheJobHandle,
                            JobHandleType.brushMeshLookupJobHandle,
                            JobHandleType.treeSpaceVerticesCacheJobHandle,
                            JobHandleType.brushTreeSpacePlaneCacheJobHandle),
                        JobHandles.Write(
                            JobHandleType.basePolygonCacheJobHandle));
                }

                if (!exactCSG)
                using (kJob_CreateIntersectionLoopsProfilerMarker.Auto())
                {
                    const bool runInParallel = runInParallelDefault;
                    NativeCollection.ScheduleEnsureCapacity(runInParallel, ref Temporaries.outputSurfaces, Temporaries.intersectionLoopCountRef,
                                                        JobHandles.Read(
                                                            JobHandleType.intersectionLoopCountRefJobHandle),
                                                        JobHandles.Write(
                                                            JobHandleType.outputSurfacesJobHandle),
                                                        defaultAllocator);

                    var createIntersectionLoopsJob = new CreateIntersectionLoopsJob
                    {
                        useIncidenceWeld            = kUseIncidenceWeld,
                        // Needed for count (forced & unused)
                        uniqueBrushPairs            = Temporaries.uniqueBrushPairs,

                        // Read
                        brushTreeSpacePlaneCache    = chiselLookupValues.brushTreeSpacePlaneCache,
                        treeSpaceVerticesCache      = chiselLookupValues.treeSpaceVerticesCache,
                        intersectingBrushesStream   = intersectingBrushesStream.AsReader(),
                        canonicalVertexStage        = canonicalVertexStage,
                        brushesTouchedByBrushCache  = chiselLookupValues.brushesTouchedByBrushCache,

                        // Write
                        outputSurfaceVertices       = Temporaries.outputSurfaceVertices.AsParallelWriterExt(),
                        outputSurfaces              = Temporaries.outputSurfaces.AsParallelWriter()
                    };
                    var currentJobHandle = createIntersectionLoopsJob.Schedule(runInParallel, Temporaries.uniqueBrushPairs, 8,
                        JobHandles.Read(
                            JobHandleType.uniqueBrushPairsJobHandle,
                            JobHandleType.brushTreeSpacePlaneCacheJobHandle,
                            JobHandleType.treeSpaceVerticesCacheJobHandle,
                            JobHandleType.intersectingBrushesStreamJobHandle,
                            JobHandleType.brushesTouchedByBrushCacheJobHandle),
                        JobHandles.Write(
                            JobHandleType.outputSurfaceVerticesJobHandle,
                            JobHandleType.outputSurfacesJobHandle));

                    NativeCollection.ScheduleDispose(runInParallel, ref intersectingBrushesStream, currentJobHandle);
				}

                if (!exactCSG)
                using (kJob_GatherOutputSurfacesProfilerMarker.Auto())
                {
                    const bool runInParallel = runInParallelDefault;
                    var gatherOutputSurfacesJob = new GatherOutputSurfacesJob
                    {
                        // Read / Write (Sort)
                        outputSurfaces      = Temporaries.outputSurfaces,

                        // Write
                        outputSurfacesRange = Temporaries.outputSurfacesRange
                    };
                    gatherOutputSurfacesJob.Schedule(runInParallel,
                        JobHandles.Read(
                            JobHandleType.outputSurfacesJobHandle), // TODO: support not having any read-handles
                        JobHandles.Write(
                            JobHandleType.outputSurfacesJobHandle,
                            JobHandleType.outputSurfacesRangeJobHandle));
                }
                
                NativeStream dataStream1 = default;
                if (!exactCSG)
                using (kJob_FindLoopOverlapIntersectionsProfilerMarker.Auto())
                {
                    const bool runInParallel = runInParallelDefault;
                    NativeCollection.ScheduleConstruct(runInParallel, out dataStream1, Temporaries.allUpdateBrushIndexOrders,
                                                        JobHandles.Read(
                                                            JobHandleType.allUpdateBrushIndexOrdersJobHandle
                                                            ),
                                                        JobHandles.Write(
                                                            JobHandleType.dataStream1JobHandle
                                                            ),
                                                        defaultAllocator);

                    using (kJob_SeedLoopVerticesProfilerMarker.Auto())
                    {
                        var seedLoopVerticesFromCacheJob = new SeedLoopVerticesFromCacheJob
                        {
                            // Read
                            allTreeBrushIndexOrders   = Temporaries.allTreeBrushIndexOrders,
                            allUpdateBrushIndexOrders = Temporaries.allUpdateBrushIndexOrders,
                            loopVerticesCache         = chiselLookupValues.loopVerticesCache,
                            allocator                 = defaultAllocator,

                            // Write
                            loopVerticesLookup        = Temporaries.loopVerticesLookup
                        };
                        seedLoopVerticesFromCacheJob.Schedule(runInParallel,
                            JobHandles.Read(
                                JobHandleType.allTreeBrushIndexOrdersJobHandle,
                                JobHandleType.allUpdateBrushIndexOrdersJobHandle,
                                JobHandleType.loopVerticesCacheJobHandle),
                            JobHandles.Write(
                                JobHandleType.loopVerticesLookupJobHandle));
                    }

                    var findLoopOverlapIntersectionsJob = new FindLoopOverlapIntersectionsJob
                    {
                        useIncidenceWeld          = kUseIncidenceWeld,
                        // Read
                        allUpdateBrushIndexOrders = Temporaries.allUpdateBrushIndexOrders,
                        outputSurfaceVertices     = Temporaries.outputSurfaceVertices,
                        outputSurfaces            = Temporaries.outputSurfaces,
                        outputSurfacesRange       = Temporaries.outputSurfacesRange,
                        maxNodeOrder              = maxNodeOrder,
                        brushTreeSpacePlaneCache  = chiselLookupValues.brushTreeSpacePlaneCache,
                        basePolygonCache          = chiselLookupValues.basePolygonCache,
                        canonicalVertexStage      = canonicalVertexStage,
                        brushesTouchedByBrushCache = chiselLookupValues.brushesTouchedByBrushCache,

                        // Read Write
                        allocator          = defaultAllocator,
                        loopVerticesLookup = Temporaries.loopVerticesLookup,

                        // Write
                        output = dataStream1.AsWriter()
                    };
                    findLoopOverlapIntersectionsJob.Schedule(runInParallel, Temporaries.allUpdateBrushIndexOrders, 1,
                        JobHandles.Read(
                            JobHandleType.allUpdateBrushIndexOrdersJobHandle,
                            JobHandleType.outputSurfaceVerticesJobHandle,
                            JobHandleType.outputSurfacesJobHandle,
                            JobHandleType.outputSurfacesRangeJobHandle,
                            JobHandleType.brushTreeSpacePlaneCacheJobHandle,
                            JobHandleType.basePolygonCacheJobHandle,
                            JobHandleType.brushesTouchedByBrushCacheJobHandle),
                        JobHandles.Write(
                            JobHandleType.loopVerticesLookupJobHandle,
                            JobHandleType.dataStream1JobHandle));
                }
                #endregion

                //
                // Ensure vertices that should be identical on different brushes, ARE actually identical
                //

                #region Merge vertices
                if (!exactCSG)
                using (kJob_MergeTouchingBrushVerticesIndirectProfilerMarker.Auto())
                {
                    const bool runInParallel = runInParallelDefault;
                    for (int mergeIteration = 0; mergeIteration < kMergeIterations; mergeIteration++)
                    {
                        var mergeTouchingBrushVerticesIndirectJob = new MergeTouchingBrushVerticesIndirectJob
                        {
                            useIncidenceWeld           = kUseIncidenceWeld,
                            canonicalVertexStage       = canonicalVertexStage,
                            brushTreeSpacePlaneCache   = chiselLookupValues.brushTreeSpacePlaneCache,
                            basePolygonCache           = chiselLookupValues.basePolygonCache,
                            // Read
                            allUpdateBrushIndexOrders  = Temporaries.allUpdateBrushIndexOrders,
                            brushesTouchedByBrushCache = chiselLookupValues.brushesTouchedByBrushCache,
                            treeSpaceVerticesArray     = chiselLookupValues.treeSpaceVerticesCache,
                            loopVerticesLookup         = Temporaries.loopVerticesLookup,
                            iterationIndex             = mergeIteration,

                            // Read Write
                            brushState                 = Temporaries.mergeBrushState,

                            // Write
                            loopVerticesLookupOut      = Temporaries.loopVerticesLookupOut,
                        };
                        mergeTouchingBrushVerticesIndirectJob.Schedule(runInParallel, Temporaries.allUpdateBrushIndexOrders, 1,
                            JobHandles.Read(
                                JobHandleType.brushTreeSpacePlaneCacheJobHandle,
                                JobHandleType.basePolygonCacheJobHandle,
                                JobHandleType.allUpdateBrushIndexOrdersJobHandle,
                                JobHandleType.treeSpaceVerticesCacheJobHandle,
                                JobHandleType.brushesTouchedByBrushCacheJobHandle,
                                JobHandleType.loopVerticesLookupJobHandle),
                            JobHandles.Write(
                                JobHandleType.loopVerticesLookupOutJobHandle,
                                JobHandleType.mergeBrushStateJobHandle));

                        var copyBackLoopVerticesJob = new CopyBackLoopVerticesJob
                        {
                            allUpdateBrushIndexOrders = Temporaries.allUpdateBrushIndexOrders,
                            loopVerticesLookupOut     = Temporaries.loopVerticesLookupOut,
                            loopVerticesLookup        = Temporaries.loopVerticesLookup,
                            brushState                = Temporaries.mergeBrushState,
                            iterationIndex            = mergeIteration,
                        };
                        copyBackLoopVerticesJob.Schedule(runInParallel, Temporaries.allUpdateBrushIndexOrders, 1,
                            JobHandles.Read(
                                JobHandleType.allUpdateBrushIndexOrdersJobHandle,
                                JobHandleType.loopVerticesLookupOutJobHandle,
                                JobHandleType.mergeBrushStateJobHandle),
                            JobHandles.Write(JobHandleType.loopVerticesLookupJobHandle));
                    }
                }
                #endregion

                #region Persist merged loop vertices, find the brushes this update left stale
                if (!exactCSG)
                using (kJob_StoreLoopVerticesProfilerMarker.Auto())
                {
                    const bool runInParallel = runInParallelDefault;
                    var storeLoopVerticesJob = new StoreLoopVerticesJob
                    {
                        // Read
                        allUpdateBrushIndexOrders  = Temporaries.allUpdateBrushIndexOrders,
                        allTreeBrushIndexOrders    = Temporaries.allTreeBrushIndexOrders,
                        loopVerticesLookup         = Temporaries.loopVerticesLookup,
                        brushesTouchedByBrushCache = chiselLookupValues.brushesTouchedByBrushCache,
                        brushTreeSpaceBounds       = chiselLookupValues.brushTreeSpaceBoundCache,
                        mergeBrushState            = Temporaries.mergeBrushState,
                        lastMergeIteration         = kMergeIterations - 1,

                        // Read / Write
                        loopVerticesCache          = chiselLookupValues.loopVerticesCache,

                        // Write
                        staleBrushes               = Temporaries.staleLoopBrushes,
                        stats                      = Temporaries.propagationStats
                    };
                    storeLoopVerticesJob.Schedule(runInParallel,
                        JobHandles.Read(
                            JobHandleType.allUpdateBrushIndexOrdersJobHandle,
                            JobHandleType.allTreeBrushIndexOrdersJobHandle,
                            JobHandleType.loopVerticesLookupJobHandle,
                            JobHandleType.brushesTouchedByBrushCacheJobHandle,
                            JobHandleType.brushTreeSpaceBoundCacheJobHandle,
                            JobHandleType.mergeBrushStateJobHandle),
                        JobHandles.Write(
                            JobHandleType.loopVerticesCacheJobHandle,
                            JobHandleType.staleLoopBrushesJobHandle));
                }
                #endregion

                //
                // Perform CSG on prepared surfaces, giving each surface a categorization
                //

                #region Perform CSG 
                using (kJob_UpdateBrushCategorizationTablesProfilerMarker.Auto())
                {
                    const bool runInParallel = runInParallelDefault;
                    // TODO: only update when brush or any touching brush has been added/removed or changes operation/order                    
                    // TODO: determine when a brush is completely inside another brush (might not have *any* intersection loops)
                    var createRoutingTableJob = new CreateRoutingTableJob // Build categorization trees for brushes
                    {
                        // Read
                        allUpdateBrushIndexOrders = Temporaries.allUpdateBrushIndexOrders,
                        brushesTouchedByBrushes   = chiselLookupValues.brushesTouchedByBrushCache,
                        compactTreeRef            = Temporaries.compactTreeRef,

                        // Write
                        routingTableLookup        = chiselLookupValues.routingTableCache
                    };
                    createRoutingTableJob.Schedule(runInParallel, Temporaries.allUpdateBrushIndexOrders, 1,
                        JobHandles.Read(
                            JobHandleType.allUpdateBrushIndexOrdersJobHandle,
                            JobHandleType.brushesTouchedByBrushCacheJobHandle,
                            JobHandleType.compactTreeRefJobHandle),
                        JobHandles.Write(JobHandleType.routingTableCacheJobHandle));
                }


				NativeStream dataStream2;
                using (kJob_PerformCSGProfilerMarker.Auto())
                {
                    const bool runInParallel = runInParallelDefault;
                    NativeCollection.ScheduleConstruct(runInParallel, out dataStream2, Temporaries.allUpdateBrushIndexOrders,
                                                        JobHandles.Read(
                                                            JobHandleType.allUpdateBrushIndexOrdersJobHandle
                                                            ),
                                                        JobHandles.Write(
                                                            JobHandleType.dataStream2JobHandle
                                                            ),
                                                        defaultAllocator);

                    if (exactCSG)
                    {
                        // What a test judges exactly (ExactCSGCapture), written beside the output
                        if (captureExact)
                            NativeCollection.ScheduleConstruct(runInParallel, out Temporaries.exactCapture, Temporaries.allUpdateBrushIndexOrders,
                                                                JobHandles.Read(
                                                                    JobHandleType.allUpdateBrushIndexOrdersJobHandle
                                                                    ),
                                                                JobHandles.Write(
                                                                    JobHandleType.dataStream2JobHandle
                                                                    ),
                                                                defaultAllocator);
                        else
                            Temporaries.exactCapture = new NativeStream(1, defaultAllocator);

                        // Every face decided exactly, from the brushes' planes and the routing table, and triangulated
                        var exactCSGJob = new ExactCSGJob
                        {
                            // Read
                            allUpdateBrushIndexOrders   = Temporaries.allUpdateBrushIndexOrders,
                            brushMeshLookup             = Temporaries.brushMeshLookup,
                            exactBrushCache             = chiselLookupValues.exactBrushCache,
                            brushesTouchedByBrushCache  = chiselLookupValues.brushesTouchedByBrushCache,
                            routingTableCache           = chiselLookupValues.routingTableCache,
                            compactTreeRef              = Temporaries.compactTreeRef,
                            captureExact                = captureExact,

                            // Write
                            stats                       = Temporaries.exactCSGStats,
                            output                      = dataStream2.AsWriter(),
                            capture                     = Temporaries.exactCapture.AsWriter(),
                        };
                        exactCSGJob.Schedule(runInParallel, Temporaries.allUpdateBrushIndexOrders, 1,
                            JobHandles.Read(
                                JobHandleType.compactTreeRefJobHandle,
                                JobHandleType.allUpdateBrushIndexOrdersJobHandle,
                                JobHandleType.brushMeshLookupJobHandle,
                                JobHandleType.exactBrushCacheJobHandle,
                                JobHandleType.brushesTouchedByBrushCacheJobHandle,
                                JobHandleType.routingTableCacheJobHandle),
                            JobHandles.Write(
                                JobHandleType.dataStream2JobHandle));
                    } else
                    {
                    // Perform CSG
                    var performCSGJob = new PerformCSGJob
                    {
                        useIncidenceWeld            = kUseIncidenceWeld,
                        canonicalVertexStage        = canonicalVertexStage,
                        // Read
                        allUpdateBrushIndexOrders   = Temporaries.allUpdateBrushIndexOrders,
                        routingTableCache           = chiselLookupValues.routingTableCache,
                        brushTreeSpacePlaneCache    = chiselLookupValues.brushTreeSpacePlaneCache,
                        brushesTouchedByBrushCache  = chiselLookupValues.brushesTouchedByBrushCache,
                        loopVerticesLookup          = Temporaries.loopVerticesLookup,
                        compactTreeRef              = Temporaries.compactTreeRef,
                        input                       = dataStream1.AsReader(),

                        // Write
                        output                      = dataStream2.AsWriter(),
                    };
                    var currentJobHandle = performCSGJob.Schedule(runInParallel, Temporaries.allUpdateBrushIndexOrders, 1,
                        JobHandles.Read(
                            JobHandleType.compactTreeRefJobHandle,
                            JobHandleType.allUpdateBrushIndexOrdersJobHandle,
                            JobHandleType.routingTableCacheJobHandle,
                            JobHandleType.brushTreeSpacePlaneCacheJobHandle,
                            JobHandleType.brushesTouchedByBrushCacheJobHandle,
                            JobHandleType.dataStream1JobHandle,
                            JobHandleType.loopVerticesLookupJobHandle),
                        JobHandles.Write(
                            JobHandleType.dataStream2JobHandle));

                    NativeCollection.ScheduleDispose(runInParallel, ref dataStream1, currentJobHandle);
                    }
                }
				#endregion

				//
				// Triangulate the surfaces and update the geometry cache
				//

				#region Triangulate Surfaces 
				using (kJob_GenerateSurfaceTrianglesProfilerMarker.Auto())
                {
                    const bool runInParallel = runInParallelDefault;
                    var generateSurfaceTrianglesJob = new GenerateSurfaceTrianglesJob
                    {
                        exactInput                = exactCSG,
                        useIncidenceWeld          = kUseIncidenceWeld,
                        canonicalVertexStage      = canonicalVertexStage,
                        brushTreeSpacePlaneCache  = chiselLookupValues.brushTreeSpacePlaneCache,
                        // Read
                        allUpdateBrushIndexOrders = Temporaries.allUpdateBrushIndexOrders,
                        basePolygonCache          = chiselLookupValues.basePolygonCache,
                        transformationCache       = chiselLookupValues.transformationCache,
                        loopVerticesLookup        = Temporaries.loopVerticesLookup,
                        brushesTouchedByBrushCache = chiselLookupValues.brushesTouchedByBrushCache,
                        input                     = dataStream2.AsReader(),
                        meshQueries               = Temporaries.meshQueries,
						entityIDLookup            = GetReadOnlyEntityIDLookup(),
                        subtractiveWorkflow       = subtractiveWorkflow,
                        normalSmoothingAngle      = normalSmoothingAngle,
                        decalVolumes              = Temporaries.decalVolumes,
                        decalTargets              = Temporaries.decalTargets,

						// Write
						brushRenderBufferCache    = chiselLookupValues.brushRenderBufferCache
                    };
                    var currentJobHandle = generateSurfaceTrianglesJob.Schedule(runInParallel, Temporaries.allUpdateBrushIndexOrders, 1,
                        JobHandles.Read(
                            JobHandleType.brushTreeSpacePlaneCacheJobHandle,
                            JobHandleType.allUpdateBrushIndexOrdersJobHandle,
                            JobHandleType.basePolygonCacheJobHandle,
                            JobHandleType.transformationCacheJobHandle,
                            JobHandleType.loopVerticesLookupJobHandle,
                            JobHandleType.brushesTouchedByBrushCacheJobHandle,
                            JobHandleType.dataStream2JobHandle,
                            JobHandleType.meshQueriesJobHandle,
                            JobHandleType.decalVolumesJobHandle),
                        JobHandles.Write(JobHandleType.brushRenderBufferCacheJobHandle));

					NativeCollection.ScheduleDispose(runInParallel, ref dataStream2, currentJobHandle);
                }
                #endregion

				#endregion


				// TODO: store parameterCounts per brush (precalculated), manage these counts in the hierarchy when brushes are added/removed/modified
				//       then we don't need to count them here & don't need to do a "complete" here
				JobHandles[JobHandleType.parameterCountsJobHandle].Complete();
                JobHandles[JobHandleType.parameterCountsJobHandle] = default;

                JobHandles[JobHandleType.staleLoopBrushesJobHandle].readWriteBarrier.Complete();
                skipMeshGeneration = Temporaries.staleLoopBrushes.IsCreated &&
                                     Temporaries.staleLoopBrushes.Length > 0 &&
                                     s_PropagationRound + 1 < kMaxPropagationRounds;
                if (skipMeshGeneration)
                    return;

				#region Store Results

				// TODO: move this out of this method, make it ON DEMAND

				//
				// Create meshes from all cached surfaces (which already contains the updated surfaces)
				//

				#region Find all generated brush specific geometry
				using (kJob_FindBrushRenderBuffersProfilerMarker.Auto())
                {
                    const bool runInParallel = runInParallelDefault;
                    NativeCollection.ScheduleEnsureCapacity(runInParallel, ref Temporaries.brushRenderData, Temporaries.allTreeBrushIndexOrders,
                                                        JobHandles.Read(JobHandleType.allTreeBrushIndexOrdersJobHandle),
                                                        JobHandles.Write(JobHandleType.brushRenderDataJobHandle),
                                                        defaultAllocator);

                    var findBrushRenderBuffersJob = new FindBrushRenderBuffersJob
                    {
                        // Read
                        meshQueryLength         = Temporaries.meshQueriesLength,
                        allTreeBrushIndexOrders = Temporaries.allTreeBrushIndexOrders,
                        brushRenderBufferCache  = chiselLookupValues.brushRenderBufferCache,

                        // Write
                        brushRenderData      = Temporaries.brushRenderData,
                        patchedRenderBuffers = Temporaries.patchedRenderBuffers,

                        // Read/Write
                        surfaceCountRef = Temporaries.surfaceCountRef
                    };
                    findBrushRenderBuffersJob.Schedule(runInParallel,
                        JobHandles.Read(
                            JobHandleType.meshQueriesJobHandle,
                            JobHandleType.allTreeBrushIndexOrdersJobHandle,
                            JobHandleType.brushRenderBufferCacheJobHandle),
                        JobHandles.Write(
                            JobHandleType.brushRenderDataJobHandle,
                            JobHandleType.surfaceCountRefJobHandle));
                }
                #endregion

                #region Allocate sub meshes
                using (kJob_AllocateSubMeshesProfilerMarker.Auto())
                {
                    const bool runInParallel = runInParallelDefault;
                    var allocateSubMeshesJob = new AllocateSubMeshesJob
                    {
                        // Read
                        meshQueryLength = Temporaries.meshQueriesLength,
                        surfaceCountRef = Temporaries.surfaceCountRef,

                        // Read/Write
                        subMeshDescriptions = Temporaries.subMeshDescriptions,
                        subMeshSections     = Temporaries.vertexBufferContents.subMeshSections,
                    };
                    allocateSubMeshesJob.Schedule(runInParallel,
                        JobHandles.Read(
                            JobHandleType.meshQueriesJobHandle,
                            JobHandleType.surfaceCountRefJobHandle),
                        JobHandles.Write(
                            JobHandleType.subMeshDescriptionsJobHandle,
                            JobHandleType.vertexBufferContents_subMeshSectionsJobHandle));
                }
                #endregion

                #region Prepare sub sections
                using (kJob_PrepareSubSectionsProfilerMarker.Auto())
                {
                    const bool runInParallel = runInParallelDefault;
                    var prepareSubSectionsJob = new PrepareSubSectionsJob
                    {
                        // Read
                        meshQueries     = Temporaries.meshQueries,
                        brushRenderData = Temporaries.brushRenderData,

                        // Write
                        allocator       = defaultAllocator,
                        subMeshSurfaces = Temporaries.subMeshSurfaces,
                    };
                    prepareSubSectionsJob.Schedule(runInParallel, Temporaries.meshQueriesLength, 1,
                        JobHandles.Read(
                            JobHandleType.meshQueriesJobHandle,
                            JobHandleType.brushRenderDataJobHandle),
                        JobHandles.Write(JobHandleType.subMeshSurfacesJobHandle));
                }
                #endregion

                #region Sort surfaces
                using (kJob_SortSurfacesProfilerMarker.Auto())
                {
                    const bool runInParallel = runInParallelDefault;
                    var sortSurfacesParallelJob = new SortSurfacesParallelJob
                    {
                        // Read
                        meshQueries     = Temporaries.meshQueries,
                        subMeshSurfaces = Temporaries.subMeshSurfaces,

                        // Write
                        subMeshDescriptions = Temporaries.subMeshDescriptions
                    };
                    sortSurfacesParallelJob.Schedule(runInParallel,
                        JobHandles.Read(
                            JobHandleType.meshQueriesJobHandle,
                            JobHandleType.subMeshSurfacesJobHandle),
                        JobHandles.Write(JobHandleType.subMeshDescriptionsJobHandle));

                    var gatherSurfacesJob = new GatherSurfacesJob
                    {
                        // Read / Write
                        subMeshDescriptions = Temporaries.subMeshDescriptions,

                        // Write
                        subMeshSections = Temporaries.vertexBufferContents.subMeshSections,
                    };
                    gatherSurfacesJob.Schedule(runInParallel,
                        JobHandles.Read(
                            JobHandleType.subMeshDescriptionsJobHandle), // TODO: Can't do empty ReadJobHandles, fix this
                        JobHandles.Write(
                            JobHandleType.subMeshDescriptionsJobHandle,
                            JobHandleType.vertexBufferContents_subMeshSectionsJobHandle));
                }
                #endregion
                
                #region Generate mesh descriptions
                using (kJob_GenerateMeshDescriptionProfilerMarker.Auto())
                {
                    const bool runInParallel = runInParallelDefault;
                    var generateMeshDescriptionJob = new GenerateMeshDescriptionJob
                    {
                        // Read
                        subMeshDescriptions = Temporaries.subMeshDescriptions,

                        // Read Write
                        meshDescriptions    = Temporaries.vertexBufferContents.meshDescriptions
                    };
                    generateMeshDescriptionJob.Schedule(runInParallel,
                        JobHandles.Read(JobHandleType.subMeshDescriptionsJobHandle),
                        JobHandles.Write(JobHandleType.vertexBufferContents_meshDescriptionsJobHandle));
                }
				#endregion


				// TODO: Make creation of different kinds of 'meshes' consistent
				#region Create Meshes
				using (kMesh_AllocateWritableMeshDataProfilerMarker.Auto())
                {
                    var meshAllocations = 0;
                    for (int m = 0; m < Temporaries.meshQueries.Length; m++)
                    {
                        var meshQuery = Temporaries.meshQueries[m];
                        var surfaceParameterIndex = (meshQuery.LayerParameterIndex >= SurfaceParameterIndex.Parameter1 &&
                                                        meshQuery.LayerParameterIndex <= SurfaceParameterIndex.MaxParameterIndex) ?
                                                        (int)meshQuery.LayerParameterIndex : 0;

                        // Query uses Material
                        if ((meshQuery.LayerQuery & SurfaceDestinationFlags.Renderable) != 0 && surfaceParameterIndex == 1)
                        {
                            // Each Material is stored as a submesh in the same mesh
                            meshAllocations += 1;
                        }
                        // Query uses PhysicMaterial
                        else if ((meshQuery.LayerQuery & SurfaceDestinationFlags.Collidable) != 0 && surfaceParameterIndex == 2)
                        {
                            // Each PhysicMaterial is stored in its own separate mesh
                            meshAllocations += Temporaries.parameterCounts[SurfaceDestinationParameters.kColliderLayer];
                        } else
                            meshAllocations++;
                    }

                    Temporaries.meshDataArray = UnityEngine.Mesh.AllocateWritableMeshData(meshAllocations);

                    for (int i = 0; i < meshAllocations; i++)
                        Temporaries.meshDatas.Add(Temporaries.meshDataArray[i]);
                }

				using (kJob_CopyToMeshesProfilerMarker.Auto())
                {
                    const bool runInParallel = runInParallelDefault;
                    var assignMeshesJob = new AssignMeshesJob
                    {
                        // Read
                        meshDescriptions = Temporaries.vertexBufferContents.meshDescriptions,
                        subMeshSections  = Temporaries.vertexBufferContents.subMeshSections,
                        meshDatas        = Temporaries.meshDatas,

                        // Write
                        meshes           = Temporaries.vertexBufferContents.meshes,

						// Read / Write (allocate)
						meshUpdatesColliders          = Temporaries.meshUpdatesColliders,
						meshUpdatesRenderable         = Temporaries.meshUpdatesRenderables,
						meshUpdatesDebugVisualization = Temporaries.meshUpdatesDebugVisualizations,
                    };
                    assignMeshesJob.Schedule(runInParallel,
                        JobHandles.Read(
                            JobHandleType.vertexBufferContents_meshDescriptionsJobHandle,
                            JobHandleType.vertexBufferContents_subMeshSectionsJobHandle,
                            JobHandleType.meshDatasJobHandle),
                        JobHandles.Write(
                            JobHandleType.vertexBufferContents_meshesJobHandle,
                            JobHandleType.debugHelperMeshesJobHandle,
                            JobHandleType.renderMeshesJobHandle,
                            JobHandleType.meshUpdatesJobHandle,
                            JobHandleType.colliderMeshUpdatesJobHandle));
                    
					var subMeshSource = new SubMeshSource
                    { 
						subMeshSurfaces     = Temporaries.subMeshSurfaces,    // PrepareSubSectionsJob   -> meshQueries / brushRenderData (FindBrushRenderBuffersJob)
						subMeshDescriptions = Temporaries.subMeshDescriptions, // SortSurfacesParallelJob -> meshQueries / subMeshSurfaces (PrepareSubSectionsJob)
						lightmapUVSettings  = lightmapUVSettings
					};

                    var copyRenderablesJob = new OutputCopyJob<ChiselOutputRenderable>
					{
                        // Read
                        subMeshSource = subMeshSource,
                        descriptors   = Temporaries.vertexBufferContents.renderDescriptors,
						meshUpdates   = Temporaries.meshUpdatesRenderables,

                        // Read/Write
                        meshDataArray = Temporaries.meshDataArray,
                    };
                    copyRenderablesJob.Schedule(runInParallel, Temporaries.meshUpdatesRenderables, 1,
                        JobHandles.Read(
                            JobHandleType.vertexBufferContents_subMeshSectionsJobHandle,
                            JobHandleType.subMeshDescriptionsJobHandle,
                            JobHandleType.subMeshSurfacesJobHandle,
                            JobHandleType.vertexBufferContents_renderDescriptorsJobHandle,
                            JobHandleType.vertexBufferContents_colliderDescriptorsJobHandle,
                            JobHandleType.meshUpdatesJobHandle),
                        JobHandles.Write(JobHandleType.vertexBufferContents_meshesJobHandle));

                    var copyCollidersJob = new OutputCopyJob<ChiselOutputCollidable>
					{
                        // Read
                        subMeshSource = subMeshSource,
						descriptors   = Temporaries.vertexBufferContents.colliderDescriptors,
						meshUpdates   = Temporaries.meshUpdatesColliders,

                        // Read/Write
                        meshDataArray = Temporaries.meshDataArray,
                    };
                    copyCollidersJob.Schedule(runInParallel, Temporaries.meshUpdatesColliders, 1,
                        JobHandles.Read(
                            JobHandleType.vertexBufferContents_subMeshSectionsJobHandle,
                            JobHandleType.subMeshDescriptionsJobHandle,
                            JobHandleType.subMeshSurfacesJobHandle,
                            JobHandleType.vertexBufferContents_renderDescriptorsJobHandle,
                            JobHandleType.vertexBufferContents_colliderDescriptorsJobHandle,
                            JobHandleType.meshUpdatesJobHandle),
                        JobHandles.Write(JobHandleType.vertexBufferContents_meshesJobHandle));

                    var copyDebugVisualizationJob = new OutputCopyJob<ChiselOutputDebugVisualizer>
					{
                        // Read
                        subMeshSource = subMeshSource,
                        descriptors   = Temporaries.vertexBufferContents.renderDescriptors,
						meshUpdates   = Temporaries.meshUpdatesDebugVisualizations,

                        // Read / Write
                        meshDataArray = Temporaries.meshDataArray,
                    };
                    copyDebugVisualizationJob.Schedule(runInParallel, Temporaries.meshUpdatesDebugVisualizations, 1,
                        JobHandles.Read(
                            JobHandleType.vertexBufferContents_subMeshSectionsJobHandle,
                            JobHandleType.subMeshDescriptionsJobHandle,
                            JobHandleType.subMeshSurfacesJobHandle,
                            JobHandleType.vertexBufferContents_renderDescriptorsJobHandle,
                            JobHandleType.vertexBufferContents_colliderDescriptorsJobHandle,
                            JobHandleType.meshUpdatesJobHandle),
                        JobHandles.Write(JobHandleType.vertexBufferContents_meshesJobHandle));


					// Wireframe rendering

					JobHandle jobHandle3;
					{
						//using var requiredAllocatedNodes = new NativeList<CompactNodeID>(Allocator.TempJob);
						//using var outlineCount = new NativeReference<int>(Allocator.TempJob);

						var updateBrushOutlineJob = new UpdateBrushWireframeJob
						{
							// Read
							allUpdateBrushIndexOrders = Temporaries.allUpdateBrushIndexOrders.AsDeferredJobArray(),
							compactHierarchy          = CompactHierarchyManager.GetReadOnlyHierarchy(treeCompactNodeID),
							brushMeshBlobs            = brushMeshBlobs,

							// Write
							brushWireframeManager     = CompactHierarchyManager.BrushOutlineManager
						};
						jobHandle3 = updateBrushOutlineJob.Schedule(runInParallel,
							JobHandles.Read(
								JobHandleType.compactHierarchyJobHandle,
								JobHandleType.allUpdateBrushIndexOrdersJobHandle,
								JobHandleType.brushMeshBlobsLookupJobHandle),
							JobHandles.Write(
								JobHandleType.brushOutlineManagerJobHandle));
					}

					// Triangle lookups / selection

					// TODO: Create selection meshes that use entityID colors

					JobHandle jobHandle1, jobHandle2;
					{
						var allocateVertexBuffersJob = new AllocateVertexBuffersJob
						{
							// Read
							subMeshSections = Temporaries.vertexBufferContents.subMeshSections,

							// Read / Write (allocate)
							subMeshTriangleLookups = Temporaries.vertexBufferContents.subMeshTriangleLookups
						};
						allocateVertexBuffersJob.Schedule(runInParallel,
							JobHandles.Read(
								JobHandleType.vertexBufferContents_subMeshSectionsJobHandle),
							JobHandles.Write(
								JobHandleType.vertexBufferContents_triangleBrushIndicesJobHandle));

						var renderTriangleBrushIndicesJob1 = new FindTriangleBrushIndicesJob
                        {
                            // Read
                            subMeshDescriptions = Temporaries.subMeshDescriptions,
                            subMeshSurfaces     = Temporaries.subMeshSurfaces,
                            meshUpdates         = Temporaries.meshUpdatesRenderables,
						    entityIDLookup      = CompactHierarchyManager.GetReadOnlyEntityIDLookup(),

						    // Read / Write
						    subMeshTriangleLookups = Temporaries.vertexBufferContents.subMeshTriangleLookups
					    };
                        jobHandle1 = renderTriangleBrushIndicesJob1.Schedule(runInParallel, Temporaries.meshUpdatesRenderables, 1,
                        JobHandles.Read(
                            JobHandleType.vertexBufferContents_subMeshSectionsJobHandle,
                            JobHandleType.subMeshDescriptionsJobHandle,
                            JobHandleType.subMeshSurfacesJobHandle,
                            JobHandleType.renderMeshesJobHandle),
                        JobHandles.Write(JobHandleType.vertexBufferContents_triangleBrushIndicesJobHandle));
                    
					    var renderTriangleBrushIndicesJob2 = new FindTriangleBrushIndicesJob
                        {
                            // Read
                            subMeshDescriptions = Temporaries.subMeshDescriptions,
                            subMeshSurfaces     = Temporaries.subMeshSurfaces,
                            meshUpdates         = Temporaries.meshUpdatesDebugVisualizations,
						    entityIDLookup    = CompactHierarchyManager.GetReadOnlyEntityIDLookup(),

						    // Read / Write
						    subMeshTriangleLookups = Temporaries.vertexBufferContents.subMeshTriangleLookups
					    };
                        jobHandle2 = renderTriangleBrushIndicesJob2.Schedule(runInParallel, Temporaries.meshUpdatesDebugVisualizations, 1,
                        JobHandles.Read(
                            JobHandleType.vertexBufferContents_subMeshSectionsJobHandle,
                            JobHandleType.subMeshDescriptionsJobHandle,
                            JobHandleType.subMeshSurfacesJobHandle,
                            JobHandleType.renderMeshesJobHandle),
                        JobHandles.Write(JobHandleType.vertexBufferContents_triangleBrushIndicesJobHandle));
                    }
					JobHandle.CombineDependencies(jobHandle1, jobHandle2, jobHandle3).Complete();
				}
				#endregion


				// TODO: Create selection meshes that use entityID colors
				// TODO: -> then we can get rid of this
				#region Store cached values back into cache (by node Index)
				using (kJob_StoreToCacheProfilerMarker.Auto())//*
                {
                    const bool runInParallel = runInParallelDefault;
                    var storeToCacheJob = new StoreToCacheJob
                    {
                        // Read
                        allTreeBrushIndexOrders   = Temporaries.allTreeBrushIndexOrders,
                        brushTreeSpaceBoundCache  = chiselLookupValues.brushTreeSpaceBoundCache,
                        brushRenderBufferCache    = chiselLookupValues.brushRenderBufferCache,

                        // Read / Write
                        brushRenderBufferLookup   = chiselLookupValues.brushRenderBufferLookup
                    };
                    storeToCacheJob.Schedule(runInParallel,
                        JobHandles.Read(
                            JobHandleType.allTreeBrushIndexOrdersJobHandle,
                            JobHandleType.brushTreeSpaceBoundCacheJobHandle,
                            JobHandleType.brushRenderBufferCacheJobHandle),
                        JobHandles.Write(JobHandleType.storeToCacheJobHandle));
                }
                #endregion

                #endregion
            }
             

            // Test instrumentation (ExactCSGCapture): waits for the exact CSG of this update, then keeps every brush's exact
            // planes and the exact output of the brushes it rebuilt.
            void StoreExactCapture()
            {
                JobHandles[JobHandleType.exactBrushCacheJobHandle].readWriteBarrier.Complete();
                JobHandles[JobHandleType.dataStream2JobHandle].readWriteBarrier.Complete();
                JobHandles[JobHandleType.allTreeBrushIndexOrdersJobHandle].readWriteBarrier.Complete();
                JobHandles[JobHandleType.compactHierarchyJobHandle].readWriteBarrier.Complete();

                ref var compactHierarchy = ref CompactHierarchyManager.GetHierarchy(treeCompactNodeID);
                var allTreeBrushIndexOrders = Temporaries.allTreeBrushIndexOrders;
                var nodeIDs = new NodeID[allTreeBrushIndexOrders.Length];
                for (int b = 0; b < nodeIDs.Length; b++)
                    nodeIDs[b] = compactHierarchy.GetNodeID(allTreeBrushIndexOrders[b].compactNodeID);
                ExactCSGCapture.Store(allTreeBrushIndexOrders, nodeIDs, ChiselTreeLookup.Value[this.tree].exactBrushCache,
                                      ExactCSGCapture.ReadParts(Temporaries.exactCapture));
            }

            public JobHandle PreMeshUpdateDispose()
            {
                var dependencies = JobHandleExtensions.CombineDependencies(
                                                JobHandleExtensions.CombineDependencies(
                                                    JobHandles[JobHandleType.allBrushMeshIDsJobHandle].writeBarrier,
                                                    JobHandles[JobHandleType.allUpdateBrushIndexOrdersJobHandle].writeBarrier,
                                                    JobHandles[JobHandleType.brushIDValuesJobHandle].writeBarrier,
                                                    JobHandles[JobHandleType.basePolygonCacheJobHandle].writeBarrier,
                                                    JobHandles[JobHandleType.brushBrushIntersectionsJobHandle].writeBarrier,
                                                    JobHandles[JobHandleType.brushesTouchedByBrushCacheJobHandle].writeBarrier,
                                                    JobHandles[JobHandleType.brushRenderBufferCacheJobHandle].writeBarrier,
                                                    JobHandles[JobHandleType.brushRenderDataJobHandle].writeBarrier,
                                                    JobHandles[JobHandleType.brushTreeSpacePlaneCacheJobHandle].writeBarrier),
                                                JobHandleExtensions.CombineDependencies(
                                                    JobHandles[JobHandleType.brushMeshBlobsLookupJobHandle].writeBarrier,
                                                    JobHandles[JobHandleType.hierarchyIDJobHandle].writeBarrier,
                                                    JobHandles[JobHandleType.hierarchyListJobHandle].writeBarrier,
                                                    JobHandles[JobHandleType.brushMeshLookupJobHandle].writeBarrier,
                                                    JobHandles[JobHandleType.brushIntersectionsWithJobHandle].writeBarrier,
                                                    JobHandles[JobHandleType.brushIntersectionsWithRangeJobHandle].writeBarrier,
                                                    JobHandles[JobHandleType.brushesThatNeedIndirectUpdateHashMapJobHandle].writeBarrier,
                                                    JobHandles[JobHandleType.brushesThatNeedIndirectUpdateJobHandle].writeBarrier,
                                                    JobHandles[JobHandleType.brushTreeSpaceBoundCacheJobHandle].writeBarrier),
                                                JobHandleExtensions.CombineDependencies(
                                                    JobHandles[JobHandleType.dataStream1JobHandle].writeBarrier,
                                                    JobHandles[JobHandleType.dataStream2JobHandle].writeBarrier,
                                                    JobHandles[JobHandleType.intersectingBrushesStreamJobHandle].writeBarrier,
                                                    JobHandles[JobHandleType.loopVerticesLookupJobHandle].writeBarrier,
                                                    JobHandles[JobHandleType.meshQueriesJobHandle].writeBarrier,
                                                    JobHandles[JobHandleType.nodeIDValueToNodeOrderArrayJobHandle].writeBarrier,
                                                    JobHandles[JobHandleType.outputSurfaceVerticesJobHandle].writeBarrier,
                                                    JobHandles[JobHandleType.outputSurfacesJobHandle].writeBarrier,
                                                    JobHandles[JobHandleType.outputSurfacesRangeJobHandle].writeBarrier),
                                                JobHandleExtensions.CombineDependencies(
                                                    JobHandles[JobHandleType.routingTableCacheJobHandle].writeBarrier,
                                                    JobHandles[JobHandleType.rebuildTreeBrushIndexOrdersJobHandle].writeBarrier,
                                                    JobHandles[JobHandleType.sectionsJobHandle].writeBarrier,
                                                    JobHandles[JobHandleType.subMeshSurfacesJobHandle].writeBarrier,
                                                    JobHandles[JobHandleType.subMeshDescriptionsJobHandle].writeBarrier,
                                                    JobHandles[JobHandleType.treeSpaceVerticesCacheJobHandle].writeBarrier,
                                                    JobHandles[JobHandleType.transformationCacheJobHandle].writeBarrier,
                                                    JobHandles[JobHandleType.uniqueBrushPairsJobHandle].writeBarrier),
                                                JobHandleExtensions.CombineDependencies(
                                                    JobHandles[JobHandleType.brushesJobHandle].writeBarrier,
                                                    JobHandles[JobHandleType.nodesJobHandle].writeBarrier, 
                                                    JobHandles[JobHandleType.parametersJobHandle].writeBarrier,
                                                    JobHandles[JobHandleType.allKnownBrushMeshIndicesJobHandle].writeBarrier,
                                                    JobHandles[JobHandleType.parameterCountsJobHandle].writeBarrier,
                                                    JobHandles[JobHandleType.storeToCacheJobHandle].writeBarrier),
                                                JobHandleExtensions.CombineDependencies(
                                                    JobHandles[JobHandleType.surfaceCountRefJobHandle].writeBarrier,
                                                    JobHandles[JobHandleType.compactTreeRefJobHandle].writeBarrier,
                                                    JobHandles[JobHandleType.needRemappingRefJobHandle].writeBarrier,
                                                    JobHandles[JobHandleType.nodeIDValueToNodeOrderOffsetRefJobHandle].writeBarrier,
                                                    JobHandles[JobHandleType.transformTreeBrushIndicesListJobHandle].writeBarrier)
                                            );

                var chiselLookupValues = ChiselTreeLookup.Value[this.tree];
                // Accumulate the dispose handles into a flat list and combine once (depth 1) instead of the
                // depth-~25 linear chain that repeated lastJobHandle.AddDependency(...) used to build.
                using var lastJobHandle = new JobHandleAccumulator(48, Allocator.Temp);
                lastJobHandle.Add(dependencies);

                if (Temporaries.exactCapture.IsCreated)
                {
                    if (captureExact)
                        StoreExactCapture();
                    lastJobHandle.AddDependency(Temporaries.exactCapture.Dispose(JobHandles[JobHandleType.dataStream2JobHandle].readWriteBarrier));
                    Temporaries.exactCapture = default;
                }

                lastJobHandle.AddDependency(Temporaries.brushIntersectionsWithRange  .SafeDispose(JobHandles[JobHandleType.brushIntersectionsWithRangeJobHandle].readWriteBarrier));
                lastJobHandle.AddDependency(Temporaries.brushIntersectionsWith       .SafeDispose(JobHandles[JobHandleType.brushIntersectionsWithJobHandle].readWriteBarrier));
                lastJobHandle.AddDependency(Temporaries.outputSurfaceVertices        .SafeDispose(JobHandles[JobHandleType.outputSurfaceVerticesJobHandle].readWriteBarrier));
                lastJobHandle.AddDependency(Temporaries.outputSurfacesRange          .SafeDispose(JobHandles[JobHandleType.outputSurfacesRangeJobHandle].readWriteBarrier));
                lastJobHandle.AddDependency(Temporaries.parameterCounts              .SafeDispose(JobHandles[JobHandleType.parameterCountsJobHandle].readWriteBarrier));
                lastJobHandle.AddDependency(Temporaries.brushMeshLookup              .SafeDispose(JobHandles[JobHandleType.brushMeshLookupJobHandle].readWriteBarrier));
                lastJobHandle.AddDependency(Temporaries.outputSurfaces               .SafeDispose(JobHandles[JobHandleType.outputSurfacesJobHandle].readWriteBarrier));
                
                lastJobHandle.AddDependency(Temporaries.nodes                        .SafeDispose(JobHandles[JobHandleType.nodesJobHandle].readWriteBarrier));
                lastJobHandle.AddDependency(Temporaries.brushes                      .SafeDispose(JobHandles[JobHandleType.brushesJobHandle].readWriteBarrier));
                lastJobHandle.AddDependency(Temporaries.allBrushMeshIDs              .SafeDispose(JobHandles[JobHandleType.allBrushMeshIDsJobHandle].readWriteBarrier));
                lastJobHandle.AddDependency(Temporaries.brushRenderData              .SafeDispose(JobHandles[JobHandleType.brushRenderDataJobHandle].readWriteBarrier));
                lastJobHandle.AddDependency(Temporaries.uniqueBrushPairs             .SafeDispose(JobHandles[JobHandleType.uniqueBrushPairsJobHandle].readWriteBarrier));
                lastJobHandle.AddDependency(Temporaries.nodeIDValueToNodeOrder       .SafeDispose(JobHandles[JobHandleType.nodeIDValueToNodeOrderArrayJobHandle].readWriteBarrier));
                lastJobHandle.AddDependency(Temporaries.allUpdateBrushIndexOrders    .SafeDispose(JobHandles[JobHandleType.allUpdateBrushIndexOrdersJobHandle].readWriteBarrier));
                lastJobHandle.AddDependency(Temporaries.rebuildTreeBrushIndexOrders  .SafeDispose(JobHandles[JobHandleType.rebuildTreeBrushIndexOrdersJobHandle].readWriteBarrier));
                lastJobHandle.AddDependency(Temporaries.brushesThatNeedIndirectUpdate.SafeDispose(JobHandles[JobHandleType.brushesThatNeedIndirectUpdateJobHandle].readWriteBarrier));
                lastJobHandle.AddDependency(Temporaries.transformTreeBrushIndicesList.SafeDispose(JobHandles[JobHandleType.transformTreeBrushIndicesListJobHandle].readWriteBarrier));
                
                lastJobHandle.AddDependency(Temporaries.brushesThatNeedIndirectUpdateHashMap.Dispose(JobHandles[JobHandleType.brushesThatNeedIndirectUpdateHashMapJobHandle].readWriteBarrier));
                lastJobHandle.AddDependency(Temporaries.decalVolumes                 .SafeDispose(JobHandles[JobHandleType.decalVolumesJobHandle].readWriteBarrier));
                lastJobHandle.AddDependency(Temporaries.changedDecalBounds           .SafeDispose(JobHandles[JobHandleType.decalVolumesJobHandle].readWriteBarrier));
                lastJobHandle.AddDependency(Temporaries.decalTargets                 .SafeDispose(JobHandles[JobHandleType.decalVolumesJobHandle].readWriteBarrier));
                
                
                // Note: cannot use "IsCreated" on this job, for some reason it won't be scheduled and then complain that it's leaking? Bug in IsCreated?
                lastJobHandle.AddDependency(Temporaries.meshQueries                     .SafeDispose(JobHandles[JobHandleType.meshQueriesJobHandle].readWriteBarrier));


                lastJobHandle.AddDependency(Temporaries.loopVerticesLookupOut           .DisposeDeep(JobHandles[JobHandleType.loopVerticesLookupOutJobHandle].readWriteBarrier));
                lastJobHandle.AddDependency(Temporaries.mergeBrushState                 .SafeDispose(JobHandles[JobHandleType.mergeBrushStateJobHandle].readWriteBarrier));
                lastJobHandle.AddDependency(Temporaries.brushBoundsSweep                .SafeDispose(JobHandles[JobHandleType.brushBoundsSweepJobHandle].readWriteBarrier));
                {
                    var internedPlanesBarrier = JobHandles[JobHandleType.internedPlanesJobHandle].readWriteBarrier;
                    if (Temporaries.internedPlanes.IsCreated)
                        lastJobHandle.AddDependency(Temporaries.internedPlanes.Dispose(internedPlanesBarrier));
                    lastJobHandle.AddDependency(Temporaries.brushPlaneIds    .SafeDispose(internedPlanesBarrier));
                    lastJobHandle.AddDependency(Temporaries.brushPlaneIdRange.SafeDispose(internedPlanesBarrier));
                }
                {
                    var exactBrushCacheBarrier = JobHandles[JobHandleType.exactBrushCacheJobHandle].readWriteBarrier;
                    lastJobHandle.AddDependency(Temporaries.exactBounds          .SafeDispose(exactBrushCacheBarrier));
                    lastJobHandle.AddDependency(Temporaries.exactBrushDisposeList.DisposeDeep(exactBrushCacheBarrier));
                }
                lastJobHandle.AddDependency(Temporaries.loopVerticesLookup              .DisposeDeep(JobHandles[JobHandleType.loopVerticesLookupJobHandle].readWriteBarrier),
                                            Temporaries.brushBrushIntersections         .DisposeDeep(JobHandles[JobHandleType.brushBrushIntersectionsJobHandle].readWriteBarrier),
                                            
                                            Temporaries.basePolygonDisposeList          .DisposeDeep(JobHandles[JobHandleType.basePolygonCacheJobHandle].readWriteBarrier),
                                            Temporaries.routingTableDisposeList         .DisposeDeep(JobHandles[JobHandleType.routingTableCacheJobHandle].readWriteBarrier),
                                            Temporaries.brushRenderBufferDisposeList    .DisposeDeep(JobHandles[JobHandleType.brushRenderBufferCacheJobHandle].readWriteBarrier),
                                            Temporaries.treeSpaceVerticesDisposeList    .DisposeDeep(JobHandles[JobHandleType.treeSpaceVerticesCacheJobHandle].readWriteBarrier),
                                            Temporaries.brushTreeSpacePlaneDisposeList  .DisposeDeep(JobHandles[JobHandleType.brushTreeSpacePlaneCacheJobHandle].readWriteBarrier),
                                            Temporaries.brushesTouchedByBrushDisposeList.DisposeDeep(JobHandles[JobHandleType.brushesTouchedByBrushCacheJobHandle].readWriteBarrier));
                

                lastJobHandle.AddDependency(Temporaries.compactTreeRef                  .DisposeBlobDeep(JobHandles[JobHandleType.compactTreeRefJobHandle].readWriteBarrier));
                lastJobHandle.AddDependency(Temporaries.surfaceCountRef                 .Dispose(JobHandles[JobHandleType.surfaceCountRefJobHandle].readWriteBarrier));
                lastJobHandle.AddDependency(Temporaries.intersectionLoopCountRef        .Dispose(JobHandles[JobHandleType.intersectionLoopCountRefJobHandle].readWriteBarrier));
                lastJobHandle.AddDependency(Temporaries.needRemappingRef                .Dispose(JobHandles[JobHandleType.needRemappingRefJobHandle].readWriteBarrier));
                lastJobHandle.AddDependency(Temporaries.nodeIDValueToNodeOrderOffsetRef .Dispose(JobHandles[JobHandleType.nodeIDValueToNodeOrderOffsetRefJobHandle].readWriteBarrier));

                lastJobHandle.Combine().Complete();

                if (Temporaries.exactCSGStats.IsCreated)
                {
                    for (int i = 0; i < Temporaries.exactCSGStats.Length && i < s_LastExactCSGStats.Length; i++)
                        s_LastExactCSGStats[i] += Temporaries.exactCSGStats[i];
                    Temporaries.exactCSGStats.Dispose();
                    Temporaries.exactCSGStats = default;
                }

				chiselLookupValues.lastJobHandle = default;
                return default;
            }

			public JobHandle FreeTemporaries(ref JobHandle finalJobHandle)
            {
                // Combine all JobHandles of all jobs to ensure that we wait for ALL of them to finish 
                // before we dispose of our temporaries.
                // Eventually we might want to put this in between other jobs, but for now this is safer
                // to work with while things are still being re-arranged.
                var dependencies = JobHandleExtensions.CombineDependencies(
                                                JobHandleExtensions.CombineDependencies(
                                                    JobHandles[JobHandleType.allBrushMeshIDsJobHandle].writeBarrier,
                                                    JobHandles[JobHandleType.allUpdateBrushIndexOrdersJobHandle].writeBarrier,
                                                    JobHandles[JobHandleType.brushIDValuesJobHandle].writeBarrier,
                                                    JobHandles[JobHandleType.basePolygonCacheJobHandle].writeBarrier,
                                                    JobHandles[JobHandleType.brushBrushIntersectionsJobHandle].writeBarrier,
                                                    JobHandles[JobHandleType.brushesTouchedByBrushCacheJobHandle].writeBarrier,
                                                    JobHandles[JobHandleType.brushRenderBufferCacheJobHandle].writeBarrier,
                                                    JobHandles[JobHandleType.brushRenderDataJobHandle].writeBarrier,
                                                    JobHandles[JobHandleType.brushTreeSpacePlaneCacheJobHandle].writeBarrier),
                                                JobHandleExtensions.CombineDependencies(
                                                    JobHandles[JobHandleType.brushMeshBlobsLookupJobHandle].writeBarrier,
                                                    JobHandles[JobHandleType.hierarchyIDJobHandle].writeBarrier,
                                                    JobHandles[JobHandleType.hierarchyListJobHandle].writeBarrier,
                                                    JobHandles[JobHandleType.brushMeshLookupJobHandle].writeBarrier,
                                                    JobHandles[JobHandleType.brushIntersectionsWithJobHandle].writeBarrier,
                                                    JobHandles[JobHandleType.brushIntersectionsWithRangeJobHandle].writeBarrier,
                                                    JobHandles[JobHandleType.brushesThatNeedIndirectUpdateHashMapJobHandle].writeBarrier,
                                                    JobHandles[JobHandleType.brushesThatNeedIndirectUpdateJobHandle].writeBarrier,
                                                    JobHandles[JobHandleType.brushTreeSpaceBoundCacheJobHandle].writeBarrier),
                                                JobHandleExtensions.CombineDependencies(
                                                    JobHandles[JobHandleType.dataStream1JobHandle].writeBarrier,
                                                    JobHandles[JobHandleType.dataStream2JobHandle].writeBarrier,
                                                    JobHandles[JobHandleType.intersectingBrushesStreamJobHandle].writeBarrier,
                                                    JobHandles[JobHandleType.loopVerticesLookupJobHandle].writeBarrier,
                                                    JobHandles[JobHandleType.meshQueriesJobHandle].writeBarrier,
                                                    JobHandles[JobHandleType.nodeIDValueToNodeOrderArrayJobHandle].writeBarrier,
                                                    JobHandles[JobHandleType.outputSurfaceVerticesJobHandle].writeBarrier,
                                                    JobHandles[JobHandleType.outputSurfacesJobHandle].writeBarrier,
                                                    JobHandles[JobHandleType.outputSurfacesRangeJobHandle].writeBarrier),
                                                JobHandleExtensions.CombineDependencies(
                                                    JobHandles[JobHandleType.routingTableCacheJobHandle].writeBarrier,
                                                    JobHandles[JobHandleType.rebuildTreeBrushIndexOrdersJobHandle].writeBarrier,
                                                    JobHandles[JobHandleType.sectionsJobHandle].writeBarrier,
                                                    JobHandles[JobHandleType.subMeshSurfacesJobHandle].writeBarrier,
                                                    JobHandles[JobHandleType.subMeshDescriptionsJobHandle].writeBarrier,
                                                    JobHandles[JobHandleType.treeSpaceVerticesCacheJobHandle].writeBarrier,
                                                    JobHandles[JobHandleType.transformationCacheJobHandle].writeBarrier,
                                                    JobHandles[JobHandleType.uniqueBrushPairsJobHandle].writeBarrier),
                                                JobHandleExtensions.CombineDependencies(
                                                    JobHandles[JobHandleType.transformTreeBrushIndicesListJobHandle].writeBarrier,
                                                    JobHandles[JobHandleType.nodesJobHandle].writeBarrier,
                                                    JobHandles[JobHandleType.parametersJobHandle].writeBarrier,
                                                    JobHandles[JobHandleType.allKnownBrushMeshIndicesJobHandle].writeBarrier,
                                                    JobHandles[JobHandleType.parameterCountsJobHandle].writeBarrier),
                                                JobHandleExtensions.CombineDependencies(
                                                    JobHandles[JobHandleType.storeToCacheJobHandle].writeBarrier,

                                                    JobHandles[JobHandleType.allTreeBrushIndexOrdersJobHandle].writeBarrier,
                                                    JobHandles[JobHandleType.meshUpdatesJobHandle].writeBarrier,
                                                    JobHandles[JobHandleType.colliderMeshUpdatesJobHandle].writeBarrier,
                                                    JobHandles[JobHandleType.debugHelperMeshesJobHandle].writeBarrier,
                                                    JobHandles[JobHandleType.renderMeshesJobHandle].writeBarrier,
                                                    JobHandles[JobHandleType.surfaceCountRefJobHandle].writeBarrier,
                                                    JobHandles[JobHandleType.nodeIDValueToNodeOrderOffsetRefJobHandle].writeBarrier),
                                                JobHandleExtensions.CombineDependencies(
                                                    JobHandles[JobHandleType.vertexBufferContents_renderDescriptorsJobHandle].writeBarrier,
                                                    JobHandles[JobHandleType.vertexBufferContents_colliderDescriptorsJobHandle].writeBarrier,
                                                    JobHandles[JobHandleType.vertexBufferContents_subMeshSectionsJobHandle].writeBarrier,
                                                    JobHandles[JobHandleType.vertexBufferContents_triangleBrushIndicesJobHandle].writeBarrier,
                                                    JobHandles[JobHandleType.vertexBufferContents_meshDescriptionsJobHandle].writeBarrier,
                                                    JobHandles[JobHandleType.vertexBufferContents_meshesJobHandle].writeBarrier,
                                                    JobHandles[JobHandleType.compactTreeRefJobHandle].writeBarrier,
                                                    JobHandles[JobHandleType.needRemappingRefJobHandle].writeBarrier,
                                                    JobHandles[JobHandleType.meshDatasJobHandle].writeBarrier)
                                        );

                // Technically not necessary, but Unity will complain about memory leaks that aren't there (jobs just haven't finished yet)
                // TODO: see if we can use domain reload events to ensure this job is completed before a domain reload occurs
                dependencies.Complete(); 
                                            

                // We let the final JobHandle dependend on the dependencies, but not on the disposal, 
                // because we do not need to wait for the disposal of native collections do use our generated data
                finalJobHandle.AddDependency(dependencies);


                var chiselLookupValues = ChiselTreeLookup.Value[this.tree];
                var lastJobHandle = chiselLookupValues.lastJobHandle;
                lastJobHandle.AddDependency(Temporaries.subMeshSurfaces         .DisposeDeep(JobHandles[JobHandleType.subMeshSurfacesJobHandle].readWriteBarrier));
                // The meshes read the weld's copies through subMeshSurfaces (and brushRenderData before it)
                lastJobHandle.AddDependency(Temporaries.patchedRenderBuffers    .DisposeDeep(JobHandle.CombineDependencies(
                                                                                    JobHandles[JobHandleType.subMeshSurfacesJobHandle].readWriteBarrier,
                                                                                    JobHandles[JobHandleType.brushRenderDataJobHandle].readWriteBarrier)));
                lastJobHandle.AddDependency(Temporaries.allTreeBrushIndexOrders .SafeDispose(JobHandles[JobHandleType.allTreeBrushIndexOrdersJobHandle].readWriteBarrier));
                lastJobHandle.AddDependency(Temporaries.staleLoopBrushes        .SafeDispose(JobHandles[JobHandleType.staleLoopBrushesJobHandle].readWriteBarrier));
                lastJobHandle.AddDependency(Temporaries.propagationStats        .SafeDispose(JobHandles[JobHandleType.staleLoopBrushesJobHandle].readWriteBarrier));
                lastJobHandle.AddDependency(Temporaries.exactCSGStats           .SafeDispose(JobHandles[JobHandleType.dataStream2JobHandle].readWriteBarrier));
                lastJobHandle.AddDependency(Temporaries.meshUpdatesColliders    .SafeDispose(JobHandles[JobHandleType.colliderMeshUpdatesJobHandle].readWriteBarrier));
                lastJobHandle.AddDependency(Temporaries.meshUpdatesDebugVisualizations.SafeDispose(JobHandles[JobHandleType.debugHelperMeshesJobHandle].readWriteBarrier));
                lastJobHandle.AddDependency(Temporaries.meshUpdatesRenderables  .SafeDispose(JobHandles[JobHandleType.renderMeshesJobHandle].readWriteBarrier));
                lastJobHandle.AddDependency(Temporaries.meshDatas               .SafeDispose(JobHandles[JobHandleType.meshDatasJobHandle].readWriteBarrier));                
                lastJobHandle.AddDependency(Temporaries.subMeshDescriptions     .SafeDispose(JobHandles[JobHandleType.subMeshDescriptionsJobHandle].readWriteBarrier));
                
                var vertexbufferContentsJobHandle = JobHandleExtensions.CombineDependencies(
                                                            JobHandles[JobHandleType.vertexBufferContents_renderDescriptorsJobHandle].readWriteBarrier,
                                                            JobHandles[JobHandleType.vertexBufferContents_colliderDescriptorsJobHandle].readWriteBarrier,
                                                            JobHandles[JobHandleType.vertexBufferContents_subMeshSectionsJobHandle].readWriteBarrier,
                                                            JobHandles[JobHandleType.vertexBufferContents_triangleBrushIndicesJobHandle].readWriteBarrier,
                                                            JobHandles[JobHandleType.vertexBufferContents_meshDescriptionsJobHandle].readWriteBarrier,
                                                            JobHandles[JobHandleType.vertexBufferContents_meshesJobHandle].readWriteBarrier);

                lastJobHandle.AddDependency(Temporaries.vertexBufferContents    .Dispose(vertexbufferContentsJobHandle));

                lastJobHandle.AddDependency(dependencies);
                
                lastJobHandle.Complete();
				lastJobHandle = default;

				chiselLookupValues.lastJobHandle = lastJobHandle;
				return lastJobHandle;
            }
        }
    }
}
