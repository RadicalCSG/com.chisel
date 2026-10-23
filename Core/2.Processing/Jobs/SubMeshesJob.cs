using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Debug = UnityEngine.Debug;
using ReadOnlyAttribute = Unity.Collections.ReadOnlyAttribute;
using WriteOnlyAttribute = Unity.Collections.WriteOnlyAttribute;

namespace Chisel.Core
{
    struct SubMeshDescriptions
    {
        public MeshQuery meshQuery;
        public int meshQueryIndex;
        public int subMeshQueryIndex;
        public ulong surfaceParameter;
        public uint  chunk;
            
        public int surfacesOffset;
        public int surfacesCount;
            
        public int vertexCount;
        public int indexCount;
            
        public uint geometryHashValue;  // used to detect changes in vertex positions  
        public uint surfaceHashValue;   // used to detect changes in color, normal, tangent or uv (doesn't effect lighting)
    }

	struct SubMeshSurface
	{
		public CompactNodeID brushNodeID;
		public int surfaceIndex;
		public ulong surfaceParameter;
		public uint  chunk;

		public int vertexCount;
		public int indexCount;

		public uint geometryHashValue;
		public uint surfaceHashValue;
		public BlobAssetReference<ChiselBrushRenderBuffer> brushRenderBuffer;
	}

	// What a query's surfaces are grouped by: their parameter, and for a collider the chunk of space its brush is in
	struct SurfaceGroupKey : IComparable<SurfaceGroupKey>, IEquatable<SurfaceGroupKey>
	{
		public ulong parameter;
		public uint  chunk;
		public int CompareTo(SurfaceGroupKey other)
		{
			if (parameter != other.parameter) return parameter < other.parameter ? -1 : 1;
			if (chunk != other.chunk) return chunk < other.chunk ? -1 : 1;
			return 0;
		}
		public bool Equals(SurfaceGroupKey other) => parameter == other.parameter && chunk == other.chunk;
		public override int GetHashCode() => (int)math.hash(new uint3((uint)parameter, (uint)(parameter >> 32), chunk));
	}

	struct BrushData
	{
		public IndexOrder brushIndexOrder; //<- TODO: if we use NodeOrder maybe this could be explicit based on the order in array?
		public int brushSurfaceOffset;
		public int brushSurfaceCount;

		public BlobAssetReference<ChiselBrushRenderBuffer> brushRenderBuffer;
	}


	[BurstCompile(CompileSynchronously = true)]
    struct AllocateSubMeshesJob : IJob
    {
        // Read
        [NoAlias, ReadOnly] public int                  meshQueryLength;
        [NoAlias, ReadOnly] public NativeReference<int> surfaceCountRef;

        // Read/Write
        [NoAlias] public NativeList<SubMeshDescriptions> subMeshDescriptions;
        [NoAlias] public NativeList<SubMeshSection>      subMeshSections;

        public void Execute()
        {
            var subMeshCapacity = surfaceCountRef.Value * meshQueryLength;
            if (subMeshDescriptions.Capacity < subMeshCapacity)
                subMeshDescriptions.Capacity = math.max(4, subMeshCapacity);
            
            if (subMeshSections.Capacity < subMeshCapacity)
                subMeshSections.Capacity = math.max(4, subMeshCapacity);
        }
    }

    [BurstCompile(CompileSynchronously = true)]
    struct FindBrushRenderBuffersJob : IJob
    {
        // Read
        [NoAlias, ReadOnly] public int meshQueryLength;
        [NoAlias, ReadOnly] public NativeList<IndexOrder>                                  allTreeBrushIndexOrders;
        [NoAlias, ReadOnly] public NativeList<BlobAssetReference<ChiselBrushRenderBuffer>> brushRenderBufferCache;
        // the brushes whose buffers this update made again
        [NoAlias, ReadOnly] public NativeList<IndexOrder>                                  allUpdateBrushIndexOrders;

        // Write
        [NoAlias] public NativeList<BrushData> brushRenderData;

        [NoAlias] public NativeReference<int> surfaceCountRef;
        // what the weld did at the last update, kept with the tree
        [NoAlias] public WeldMemory weldMemory;
        // where the weld's last step moved the corners it moved (OutputModelWeld.WeldModel), kept with the tree
        [NoAlias] public NativeList<float3x2> collapseMoves;
        // the surfaces the weld changed and the brushes to store again (CopyWeldedBrushesJob)
        [NoAlias] public WeldState weld;

        public void Execute()
        {
            var totalSurfaceCount = 0;
            for (int b = 0, count_b = allTreeBrushIndexOrders.Length; b < count_b; b++)
            {
                var brushIndexOrder     = allTreeBrushIndexOrders[b];
                var brushNodeOrder      = allTreeBrushIndexOrders[b].nodeOrder;
                var brushRenderBuffer   = brushRenderBufferCache[brushNodeOrder];
                if (!brushRenderBuffer.IsCreated)
                    continue;

                ref var brushRenderBufferRef = ref brushRenderBuffer.Value;

                var brushSurfaceCount = brushRenderBufferRef.surfaceCount;
                if (brushSurfaceCount == 0)
                    continue;

                var brushSurfaceOffset = brushRenderBufferRef.surfaceOffset;
                totalSurfaceCount += brushSurfaceCount;
                brushRenderData.AddNoResize(new BrushData{
                    brushIndexOrder     = brushIndexOrder,
                    brushSurfaceOffset  = brushSurfaceOffset,
                    brushSurfaceCount   = brushSurfaceCount,
                    brushRenderBuffer   = brushRenderBuffer
                });
            }
            if (surfaceCountRef.Value < totalSurfaceCount)
                surfaceCountRef.Value = totalSurfaceCount;

            // The weld at the very end across the whole model: needles that close a seam go with the triangle across them
            // (its collapse runs brush by brush in the jobs after this one; the brushes it changed are stored again by
            // CopyWeldedBrushesJob)
            OutputModelWeld.PrepareIncremental(brushRenderData, allUpdateBrushIndexOrders.AsArray(), ref weldMemory, ref weld);
            do
                OutputModelWeld.PlanWeldBeforeCollapse(brushRenderData, ref weld, out _, out _, out _, collapseMoves);
            while (OutputModelWeld.TakeInSplitSurfaces(brushRenderData, ref weldMemory, ref weld));
            OutputModelWeld.RecordRounds(brushRenderData, ref weldMemory, ref weld);
        }
    }

    // The collapse of the weld, brush by brush: each brush's short edges counted (write false), then written where
    // OffsetShortEdges puts them
    [BurstCompile(CompileSynchronously = true)]
    unsafe struct ShortEdgesJob : IJobParallelForDefer
    {
        public bool write;
        [NoAlias, ReadOnly] public NativeArray<BrushData>              brushRenderData;
        [NoAlias, ReadOnly] public NativeList<OutputModelWeld.Working> work;
        [NoAlias, ReadOnly] public NativeParallelHashMap<long, int>    workIndex;
        [NoAlias, ReadOnly] public NativeArray<int2>                   edgeOffsets;
        [NoAlias, ReadOnly] public WeldScope                           scope;

        [NativeDisableParallelForRestriction] [NoAlias] public NativeArray<int2>     edgeCounts;
        [NativeDisableParallelForRestriction] [NoAlias] public NativeArray<float3x2> shortPairs;
        [NativeDisableParallelForRestriction] [NoAlias] public NativeArray<float3>   keptCorners;

        public void Execute(int b)
        {
            if (!write)
            {
                edgeCounts[b] = OutputModelWeld.ShortEdgesOf(b, brushRenderData, work, workIndex, scope, null, null);
                return;
            }
            OutputModelWeld.ShortEdgesOf(b, brushRenderData, work, workIndex, scope,
                                         (float3x2*)shortPairs.GetUnsafePtr() + edgeOffsets[b].x, (float3*)keptCorners.GetUnsafePtr() + edgeOffsets[b].y);
        }
    }

    // The collapse's steps that see the whole model: 0 where each brush's short edges go, 1 the clusters, 2 where each
    // brush's decisions go, 3 the decisions carried out, and what the weld did kept (OutputModelWeld.RecordIncremental)
    [BurstCompile(CompileSynchronously = true)]
    struct CollapseStepJob : IJob
    {
        public int step;
        [NoAlias] public NativeList<BrushData>  brushRenderData;
        [NoAlias] public NativeList<float3x2>   collapseMoves;
        [NoAlias] public WeldState              weld;
        [NoAlias] public WeldMemory             weldMemory;

        public void Execute()
        {
            switch (step)
            {
                case 0: OutputModelWeld.OffsetShortEdges(ref weld); break;
                case 1: OutputModelWeld.ClusterShortEdges(ref weld, collapseMoves); break;
                case 2: OutputModelWeld.OffsetDecisions(ref weld); break;
                case 3:
                    OutputModelWeld.ApplyCollapse(brushRenderData, ref weld);
                    OutputModelWeld.RecordIncremental(brushRenderData, ref weldMemory, ref weld, collapseMoves);
                    break;
            }
        }
    }

    // What the collapse does to each brush's surfaces, counted (write false), then written where OffsetDecisions puts it
    [BurstCompile(CompileSynchronously = true)]
    unsafe struct DecideCollapseJob : IJobParallelForDefer
    {
        public bool write;
        [NoAlias, ReadOnly] public NativeArray<BrushData>                 brushRenderData;
        [NoAlias, ReadOnly] public NativeList<OutputModelWeld.Working>    work;
        [NoAlias, ReadOnly] public NativeParallelHashMap<long, int>       workIndex;
        [NoAlias, ReadOnly] public NativeParallelHashMap<float3, float3>  movedTo;
        [NoAlias, ReadOnly] public NativeArray<int>                       decisionOffsets;
        [NoAlias, ReadOnly] public WeldScope                              scope;

        [NativeDisableParallelForRestriction] [NoAlias] public NativeArray<int>                              decisionCounts;
        [NativeDisableParallelForRestriction] [NoAlias] public NativeArray<OutputModelWeld.CollapseDecision> decisions;

        public void Execute(int b)
        {
            if (!write)
            {
                decisionCounts[b] = OutputModelWeld.DecideCollapse(b, brushRenderData, work, workIndex, scope, movedTo, null);
                return;
            }
            OutputModelWeld.DecideCollapse(b, brushRenderData, work, workIndex, scope, movedTo,
                                           (OutputModelWeld.CollapseDecision*)decisions.GetUnsafePtr() + decisionOffsets[b]);
        }
    }

    // Marks the sections whose mesh is made from the same as last time (keptSections), remembers what each was made from,
    // and gives each render mesh the hash of what its lightmap layout is made from (sectionLightmapHashes); kept: how
    // many are kept. Render, shadow-only, collider and shown debug visualization meshes are kept. Section by section, in
    // parallel, what each is made from (sectionHashes, FindKeptSectionHashesJob), then FindKeptSectionsJob
    [BurstCompile(CompileSynchronously = true)]
    unsafe struct FindKeptSectionHashesJob : IJobParallelFor
    {
        [NoAlias, ReadOnly] public NativeList<SubMeshSection>                sections;
        [NoAlias, ReadOnly] public NativeList<SubMeshDescriptions>           descriptions;
        [NoAlias, ReadOnly] public NativeArray<UnsafeList<SubMeshSurface>>   subMeshSurfaces;
        public LightmapUVSettings lightmapUVSettings;
        public bool keeping;
        // the debug visualizations made (AssignMeshesJob.debugVisualizations): one that isn't leaves no hash behind
        public int debugVisualizations;

        [NoAlias] public NativeArray<uint>   sectionLightmapHashes;
        // what a section that can be kept is made from (hashed: true)
        [NoAlias] public NativeArray<uint4>  sectionHashes;
        [NoAlias] public NativeArray<bool>   hashed;

        public void Execute(int i)
        {
            var buffer = new UnsafeAppendBuffer(256, 16, Allocator.Temp);
            try
            {
                var section  = sections[i];
                var collider = section.meshQuery.LayerParameterIndex == SurfaceParameterIndex.PhysicsMaterial;
                var render   = section.meshQuery.LayerParameterIndex == SurfaceParameterIndex.RenderMaterial;
                var debug    = section.meshQuery.LayerParameterIndex == SurfaceParameterIndex.None;
                if (render)
                    sectionLightmapHashes[i] = LightmapHash(section, ref buffer);
                if (!keeping ||
                    !(collider || debug || GatherSurfacesJob.OneMeshPerMaterial(section.meshQuery) || PrepareSubSections.OneMaterial(section.meshQuery)) ||
                    ((render || debug) && section.endIndex - section.startIndex != 1) ||
                    (debug && AssignMeshesJob.HiddenDebugVisualization(section.meshQuery, debugVisualizations)))
                    return;
                // a collider's section is the one description it starts at
                var description = descriptions[section.startIndex];
                buffer.Reset();
                if (render || debug)
                {
                    buffer.Add(description.geometryHashValue);
                    buffer.Add(description.surfaceHashValue);
                    buffer.Add(description.vertexCount);
                    buffer.Add(description.indexCount);
                    buffer.Add(lightmapUVSettings);
                }
                var surfaces = subMeshSurfaces[description.meshQueryIndex];
                for (int s = description.surfacesOffset; s < description.surfacesOffset + description.surfacesCount && s < surfaces.Length; s++)
                {
                    buffer.Add(surfaces[s].brushNodeID);
                    buffer.Add(surfaces[s].surfaceIndex);
                    // a collider is the shape as the weld left it, before any hotspot cut
                    if (collider && surfaces[s].brushRenderBuffer.IsCreated)
                        buffer.Add(surfaces[s].brushRenderBuffer.Value.surfaces[surfaces[s].surfaceIndex].shapeHashValue);
                }
                sectionHashes[i] = xxHash3.Hash128(buffer.Ptr, buffer.Length);
                hashed[i] = true;
            }
            finally
            {
                buffer.Dispose();
            }
        }

        // What a render mesh's lightmap layout is made from (ChiselOutputRenderable): each surface's chart, which the
        // CSG made before any hotspot cut, and the layout settings
        uint LightmapHash(SubMeshSection section, ref UnsafeAppendBuffer buffer)
        {
            buffer.Reset();
            buffer.Add(lightmapUVSettings);
            for (int d = section.startIndex; d < section.endIndex; d++)
            {
                var description = descriptions[d];
                var surfaces    = subMeshSurfaces[description.meshQueryIndex];
                for (int s = description.surfacesOffset; s < description.surfacesOffset + description.surfacesCount && s < surfaces.Length; s++)
                {
                    if (!surfaces[s].brushRenderBuffer.IsCreated)
                        continue;
                    ref var surface = ref surfaces[s].brushRenderBuffer.Value.surfaces[surfaces[s].surfaceIndex];
                    var drawn = surface.indices.Length > 0 && surface.renderVertices.Length > 0;
                    buffer.Add(surface.lightmapChart);
                    buffer.Add(drawn ? (int)LightmapUVLayout.ModeOf(surface.outputFlags) : -1);
                    buffer.Add(surface.shapeHashValue);
                    buffer.Add(surfaces[s].surfaceIndex);
                }
            }
            return xxHash3.Hash64(buffer.Ptr, buffer.Length).x;
        }
    }

    [BurstCompile(CompileSynchronously = true)]
    struct FindKeptSectionsJob : IJob
    {
        [NoAlias, ReadOnly] public NativeList<SubMeshSection>                sections;
        [NoAlias, ReadOnly] public NativeList<SubMeshDescriptions>           descriptions;
        [NoAlias, ReadOnly] public NativeArray<uint4>  sectionHashes;
        [NoAlias, ReadOnly] public NativeArray<bool>   hashed;

        [NoAlias] public NativeParallelHashMap<RenderMeshKey, uint4> hashes;
        [NoAlias] public NativeArray<bool>   keptSections;
        [NoAlias] public NativeReference<int> kept;

        public void Execute()
        {
            var seen  = new NativeParallelHashSet<RenderMeshKey>(math.max(1, sections.Length), Allocator.Temp);
            var count = 0;
            for (int i = 0; i < sections.Length; i++)
            {
                if (!hashed[i])
                    continue;
                var section     = sections[i];
                var description = descriptions[section.startIndex];
                var hash = sectionHashes[i];
                var key  = RenderMeshKey.Of(section.meshQuery, description);
                if (!seen.Add(key))
                    continue;
                if (hashes.TryGetValue(key, out var last) && last.Equals(hash))
                {
                    keptSections[i] = true;
                    count++;
                }
                hashes[key] = hash;
            }
            if (hashes.IsCreated)
            {
                // the meshes this update does not have any more
                using var keys = hashes.GetKeyArray(Allocator.Temp);
                for (int k = 0; k < keys.Length; k++)
                {
                    if (!seen.Contains(keys[k]))
                        hashes.Remove(keys[k]);
                }
            }
            seen.Dispose();
            kept.Value = count;
        }
    }

    // Each brush the weld changed, stored again on its own
    [BurstCompile(CompileSynchronously = true)]
    struct CopyWeldedBrushesJob : IJobParallelForDefer
    {
        [NoAlias, ReadOnly] public NativeArray<BrushData>              brushRenderData;
        [NoAlias, ReadOnly] public NativeList<OutputModelWeld.Working> work;
        [NoAlias, ReadOnly] public NativeParallelHashMap<long, int>    workIndex;
        [NoAlias, ReadOnly] public NativeList<OutputModelWeld.CollapseDecision> collapseDecisions;

        [NoAlias] public NativeArray<WeldCopy> copies;

        public void Execute(int index)
        {
            copies[index] = OutputModelWeld.CopyPlanned(copies[index], brushRenderData, work, workIndex, collapseDecisions, default);
        }
    }

    // Who is grouped with whom (MatchHotspotGroupsJob), once the weld's copies are handed out (FinishWeldJob)
    [BurstCompile(CompileSynchronously = true)]
    struct PrepareHotspotGroupsJob : IJob
    {
        // Read
        [NoAlias, ReadOnly] public NativeArray<HotspotTileData>   hotspotTiles;
        [NoAlias, ReadOnly] public NativeArray<HotspotLayoutData> hotspotLayouts;
        [NoAlias, ReadOnly] public NativeArray<HotspotLink>       hotspotLinks;
        [NoAlias, ReadOnly] public NativeArray<float4>            hotspotTileSides;

        // Write
        [NoAlias] public NativeList<BrushData> brushRenderData;
        [NoAlias] public HotspotCutState hotspotCut;
        [NoAlias] public FindState       hotspotFind;
        // what the last update's grouping found, kept with the tree
        [NoAlias] public HotspotGroupMemory hotspotGroupMemory;
        // the brushes whose kinds were kept (HotspotStageMemory.keptKinds)
        [NoAlias, ReadOnly] public NativeList<bool> keptBrushes;
        public bool verify;

        public void Execute()
        {
            HotspotGroups.Borders(brushRenderData, hotspotLinks, ref hotspotCut.bordersOf);
            HotspotGroups.PrepareIncremental(brushRenderData, hotspotTiles, hotspotLayouts, hotspotLinks, ref hotspotFind, ref hotspotGroupMemory, keptBrushes.AsArray());
            if (verify)
            {
                var full = FindState.Create(Allocator.Temp);
                HotspotGroups.Prepare(brushRenderData, hotspotTiles, hotspotLayouts, hotspotLinks, ref full);
                if (!HotspotGroups.SameAs(hotspotFind, full, out var difference))
                    Debug.LogError($"Incremental hotspot groups differ from the full grouping ({hotspotGroupMemory.lastDirty.Value} dirty, full: {hotspotGroupMemory.lastFull.Value}): {difference}");
                full.Dispose();
            }
            hotspotFind.inputs.Value = HotspotGroups.InputsKey(hotspotTiles, hotspotLayouts, hotspotTileSides);
        }
    }

    // The weld's copies handed to their brushes (each brush's own buffer kept first), and room for the surfaces' kinds
    [BurstCompile(CompileSynchronously = true)]
    struct FinishWeldJob : IJob
    {
        [NoAlias, ReadOnly] public NativeList<WeldCopy> weldCopies;

        [NoAlias] public NativeList<BrushData> brushRenderData;
        [NoAlias] public NativeList<BlobAssetReference<ChiselBrushRenderBuffer>> patchedRenderBuffers;
        [NoAlias] public HotspotCutState hotspotCut;
        [NoAlias] public FindState       hotspotFind;
        [NoAlias] public WeldMemory      weldMemory;
        [NoAlias, ReadOnly] public NativeList<float3x2> collapseMoves;
        public bool verify, report;
        [NoAlias, ReadOnly] public NativeArray<HotspotLayoutData> hotspotLayouts;
        [NoAlias] public HotspotStageMemory hotspotStageMemory;

        public void Execute()
        {
            // each brush's own buffer, before the weld's copies: told the hotspot group each surface was mapped with
            hotspotCut.kept.Clear();
            for (int i = 0; i < brushRenderData.Length; i++)
                hotspotCut.kept.Add(brushRenderData[i].brushRenderBuffer);
            OutputModelWeld.FinishIncremental(brushRenderData, weldCopies, ref weldMemory, verify, report, collapseMoves);
            HotspotGroups.StartKinds(brushRenderData, ref hotspotFind);
            hotspotStageMemory.Prepare(brushRenderData, hotspotLayouts, weldMemory.sameBuffer);
        }
    }

    // Each brush's surfaces' part in the hotspot groups (HotspotGroups.KindsOf), brush by brush
    [BurstCompile(CompileSynchronously = true)]
    struct SurfaceKindsJob : IJobParallelForDefer
    {
        [NoAlias, ReadOnly] public NativeArray<BrushData>         brushRenderData;
        [NoAlias, ReadOnly] public NativeArray<HotspotLayoutData> hotspotLayouts;
        [NoAlias, ReadOnly] public NativeArray<int>               kindStart;
        [NativeDisableParallelForRestriction] [NoAlias] public NativeArray<byte>  kinds;
        [NativeDisableParallelForRestriction] [NoAlias] public NativeArray<uint4> hashes;
        // what was kept of each brush (HotspotStageMemory): its slot, whether its kinds are kept, the slots
        [NoAlias, ReadOnly] public NativeArray<int>  slotOfBrush;
        [NoAlias, ReadOnly] public NativeArray<bool> keptKinds;
        [NoAlias, ReadOnly] public NativeArray<HotspotStageBrush> stageBrushes;
        public bool verify;

        public void Execute(int b)
        {
            if (!brushRenderData[b].brushRenderBuffer.IsCreated)
                return;
            var kept  = stageBrushes[slotOfBrush[b]];
            var start = kindStart[b];
            var count = kindStart[b + 1] - start;
            if (keptKinds[b])
            {
                for (int s = 0; s < count; s++)
                {
                    kinds[start + s]  = kept.kinds[s];
                    hashes[start + s] = kept.hashes[s];
                }
                if (verify)
                    HotspotGroups.VerifyKeptKinds(b, brushRenderData, hotspotLayouts, kindStart, kinds, hashes);
                return;
            }
            HotspotGroups.KindsOf(b, brushRenderData, hotspotLayouts, kindStart, kinds, hashes);
            for (int s = 0; s < count; s++)
            {
                kept.kinds[s]  = kinds[start + s];
                kept.hashes[s] = hashes[start + s];
            }
        }
    }

    // Each group of hotspot surfaces matched on its own
    [BurstCompile(CompileSynchronously = true)]
    struct MatchHotspotGroupsJob : IJobParallelForDefer
    {
        [NoAlias, ReadOnly] public NativeArray<BrushData>         brushRenderData;
        [NoAlias, ReadOnly] public NativeArray<HotspotTileData>   hotspotTiles;
        [NoAlias, ReadOnly] public NativeArray<HotspotLayoutData> hotspotLayouts;
        [NoAlias, ReadOnly] public NativeArray<float4>            hotspotTileSides;
        [NoAlias, ReadOnly] public UnsafeParallelMultiHashMap<int2, HotspotBorderCorner> bordersOf;
        [NoAlias, ReadOnly] public NativeList<int2>     candidates;
        [NoAlias, ReadOnly] public NativeList<float4x4> unfold;
        [NoAlias, ReadOnly] public NativeList<int>      memberStart;
        [NoAlias, ReadOnly] public NativeList<int>      memberOf;
        [NoAlias, ReadOnly] public NativeList<int>      leads;
        [NoAlias, ReadOnly] public NativeReference<uint4> inputs;
        // what each group was matched with before, kept with the tree
        [NoAlias, ReadOnly] public NativeParallelHashMap<uint4, GroupMatchEntry> hotspotMatchCache;

        [NoAlias] public NativeArray<GroupMatch> matches;

        public void Execute(int index)
        {
            var state = new FindState { candidates = candidates, unfold = unfold, memberStart = memberStart, memberOf = memberOf, leads = leads };
            matches[index] = HotspotGroups.Match(leads[index], brushRenderData, hotspotTiles, hotspotLayouts, hotspotTileSides, bordersOf, state,
                                                 hotspotMatchCache, inputs.Value);
        }
    }

    // The groups put together in the model's order, then the hotspot cut planned (CutHotspotBrushesJob)
    [BurstCompile(CompileSynchronously = true)]
    struct PlanHotspotCutJob : IJob
    {
        // Read
        [NoAlias, ReadOnly] public NativeArray<HotspotLayoutData> hotspotLayouts;
        [NoAlias, ReadOnly] public NativeArray<float4>            hotspotTileSides;
        [NoAlias, ReadOnly] public NativeArray<ulong>             hotspotVariants;

        // Write
        [NoAlias] public NativeList<BrushData> brushRenderData;
        // what the hotspot cut made of each brush, kept with the tree
        [NoAlias] public NativeParallelHashMap<CompactNodeID, HotspotCutEntry> hotspotCutCache;
        // what each group was matched with, kept with the tree
        [NoAlias] public NativeParallelHashMap<uint4, GroupMatchEntry> hotspotMatchCache;
        // the model's hotspot groups and the brushes left to cut (CutHotspotBrushesJob)
        [NoAlias] public HotspotCutState hotspotCut;
        [NoAlias] public FindState       hotspotFind;

        public void Execute()
        {
            HotspotGroups.Assemble(ref hotspotFind, ref hotspotCut.GroupOf, ref hotspotCut.UnfoldOf, hotspotCut.groups, hotspotCut.lines, hotspotCut.halfPlanes, hotspotCut.cells,
                                   hotspotMatchCache);
            // Then what is drawn of hotspot surfaces is cut into its sections; the collider stays as welded (each brush's key
            // in HotspotCutKeysJob, the brushes to cut in FinishHotspotCutKeysJob)
            OutputHotspotSections.StartCutKeys(brushRenderData, hotspotLayouts, hotspotTileSides, hotspotCut.kept.AsArray(), hotspotVariants, hotspotCutCache.IsCreated, ref hotspotCut);
        }
    }

    // What the hotspot cut reads of each brush (OutputHotspotSections.BrushCutKey), brush by brush
    [BurstCompile(CompileSynchronously = true)]
    struct HotspotCutKeysJob : IJobParallelForDefer
    {
        [NoAlias, ReadOnly] public NativeArray<BrushData> brushRenderData;
        [NoAlias, ReadOnly] public NativeReference<UnsafeHashMap<int2, int>>      groupOf;
        [NoAlias, ReadOnly] public NativeReference<UnsafeHashMap<int2, float4x4>> unfoldOf;
        [NoAlias, ReadOnly] public UnsafeParallelMultiHashMap<int2, HotspotBorderCorner> bordersOf;
        [NoAlias, ReadOnly] public NativeArray<uint4> groupKeys;
        [NoAlias, ReadOnly] public NativeReference<uint4> globalKey;
        public bool cached;

        [NativeDisableParallelForRestriction] [NoAlias] public NativeArray<uint4> brushKeys;
        [NativeDisableParallelForRestriction] [NoAlias] public NativeArray<byte>  brushCuts;
        // each brush's content hash, kept while its buffer stays (HotspotStageMemory)
        [NoAlias, ReadOnly] public NativeArray<int>  slotOfBrush;
        [NoAlias, ReadOnly] public NativeArray<bool> keptContent;
        [NativeDisableParallelForRestriction] [NoAlias] public NativeArray<HotspotStageBrush> stageBrushes;
        public bool verify;

        public void Execute(int b)
        {
            var slot    = slotOfBrush[b];
            var kept    = stageBrushes[slot];
            var content = kept.content;
            brushCuts[b] = OutputHotspotSections.BrushCutKey(b, brushRenderData, groupOf.Value, unfoldOf.Value, bordersOf, groupKeys, globalKey.Value, cached,
                                                             keptContent[b], ref content, out var key, out var made);
            brushKeys[b] = key;
            if (made)
            {
                kept.content    = content;
                kept.hasContent = true;
                stageBrushes[slot] = kept;
            } else
            if (verify && keptContent[b])
                OutputHotspotSections.VerifyKeptContent(b, brushRenderData, content);
        }
    }

    // The brushes left to cut, and the cache's cuts handed to the others (OutputHotspotSections.FinishCutKeys)
    [BurstCompile(CompileSynchronously = true)]
    struct FinishHotspotCutKeysJob : IJob
    {
        [NoAlias] public NativeList<BrushData> brushRenderData;
        [NoAlias] public NativeParallelHashMap<CompactNodeID, HotspotCutEntry> hotspotCutCache;
        [NoAlias] public HotspotCutState hotspotCut;

        public void Execute()
        {
            OutputHotspotSections.FinishCutKeys(brushRenderData, hotspotCutCache, ref hotspotCut);
        }
    }

    // Each brush FindBrushRenderBuffersJob left to cut, cut on its own
    [BurstCompile(CompileSynchronously = true)]
    struct CutHotspotBrushesJob : IJobParallelForDefer
    {
        [NoAlias, ReadOnly] public NativeArray<BrushData>         brushRenderData;
        [NoAlias, ReadOnly] public NativeArray<HotspotLayoutData> hotspotLayouts;
        [NoAlias, ReadOnly] public NativeArray<float4>            hotspotTileSides;
        [NoAlias, ReadOnly] public NativeArray<ulong>             hotspotVariants;
        [NoAlias, ReadOnly] public NativeReference<UnsafeHashMap<int2, int>>      groupOf;
        [NoAlias, ReadOnly] public NativeReference<UnsafeHashMap<int2, float4x4>> unfoldOf;
        [NoAlias, ReadOnly] public UnsafeParallelMultiHashMap<int2, HotspotBorderCorner> bordersOf;
        [NoAlias, ReadOnly] public NativeList<HotspotGroups.Group>      groups;
        [NoAlias, ReadOnly] public NativeList<HotspotSectionLine>      lines;
        [NoAlias, ReadOnly] public NativeList<HotspotSectionHalfPlane> halfPlanes;
        [NoAlias, ReadOnly] public NativeList<HotspotSectionCell>      cells;

        [NoAlias] public NativeArray<HotspotCutWork> work;

        public void Execute(int index)
        {
            work[index] = OutputHotspotSections.CutPlanned(work[index], brushRenderData, groupOf.Value, unfoldOf.Value, bordersOf, groups, lines, halfPlanes, cells,
                                                           hotspotTileSides, hotspotLayouts, hotspotVariants);
        }
    }

    // The cuts handed to the brushes and the cache
    [BurstCompile(CompileSynchronously = true)]
    struct FinishHotspotCutJob : IJob
    {
        [NoAlias, ReadOnly] public NativeList<HotspotCutWork> work;

        [NoAlias] public NativeList<BrushData> brushRenderData;
        [NoAlias] public NativeList<BlobAssetReference<ChiselBrushRenderBuffer>> patchedRenderBuffers;
        [NoAlias] public NativeParallelHashMap<CompactNodeID, HotspotCutEntry> hotspotCutCache;

        public void Execute()
        {
            OutputHotspotSections.FinishCut(brushRenderData, patchedRenderBuffers, work, hotspotCutCache);
        }
    }

    // Every query's surfaces grouped by key (SurfaceGroupKey): the groups in key order, brush order within a group. The
    // brushes in ranges (kRangeCount, in brush order), each range's buffers read once for all queries: its keys
    // (PrepareSubSectionKeysJob), where each range's part of a group goes (PrepareSubSectionsJob), its surfaces written there
    // (ScatterSubSectionsJob)
    static class PrepareSubSections
    {
        public const int kRangeCount = 8;

        // One range's surfaces of one query: its distinct keys, per key its surface count (then where its next surface
        // goes), per surface in brush order its key
        public struct RangeGroups
        {
            public UnsafeList<SurfaceGroupKey> keys;
            public UnsafeList<int> counts;
            public UnsafeList<int> groupOf;
            // A lightmap split query's keys are 8 m cells: each one's cell and lightmap area, and the group it goes to
            // (finalOf, into finalKeys and finalCounts, from PrepareSubSectionsJob). Otherwise a key is its own group,
            // and finalKeys and finalCounts are keys and counts.
            public UnsafeList<int3>  cells;
            public UnsafeList<float> areas;
            public UnsafeList<int>   finalOf;
            public UnsafeList<SurfaceGroupKey> finalKeys;
            public UnsafeList<int>   finalCounts;
        }

        public static void Range(int range, int brushCount, out int first, out int end)
        {
            first = (int)((long)range * brushCount / kRangeCount);
            end   = (int)((long)(range + 1) * brushCount / kRangeCount);
        }

        public static bool Chunked(MeshQuery query) => query.LayerParameterIndex == SurfaceParameterIndex.PhysicsMaterial;

        // A render query's meshes are chunked by surface: each surface is in the 32 m cell its centre lies in
        // (Documentation~/Design/Chunking.md)
        public const float kSurfaceChunkSize = 32.0f;

        public static bool SurfaceChunked(MeshQuery query) => query.LayerParameterIndex == SurfaceParameterIndex.RenderMaterial ||
                                                              query.LayerParameterIndex == SurfaceParameterIndex.None;

        // A query drawn with one material of its own (shadow-only, a debug visualization): its surfaces' materials don't
        // split its meshes
        public static bool OneMaterial(MeshQuery query) => SurfaceChunked(query) && !GatherSurfacesJob.OneMeshPerMaterial(query);

        public static uint CellOf(MinMaxAABB aabb)
        {
            var cell = (int3)math.floor((aabb.Min + aabb.Max) * (0.5f / kSurfaceChunkSize));
            return math.hash(cell);
        }

        // The key a surface of a query is grouped by: its parameter, and its chunk
        public static SurfaceGroupKey KeyOf(ref ChiselBrushRenderBuffer buffer, ChiselQuerySurface surface, uint brushChunk, bool chunked, bool surfaceChunked, bool oneMaterial)
        {
            return new SurfaceGroupKey
            {
                parameter = oneMaterial ? 0 : surface.surfaceParameter,
                chunk     = chunked ? brushChunk : surfaceChunked ? CellOf(buffer.surfaces[surface.surfaceIndex].aabb) : 0
            };
        }

        // A model that bakes lightmaps splits a render chunk whose lightmap would be larger than the lighting allows
        // (lightmapAreaBudget, in square units) into its 16 m cells, and those into 8 m ones (Chunking.md)
        public static bool LightmapSplit(MeshQuery query, float lightmapAreaBudget)
        {
            return lightmapAreaBudget > 0 && GatherSurfacesJob.OneMeshPerMaterial(query) &&
                   (query.LayerQuery & SurfaceDestinationFlags.ExcludedFromGlobalIllumination) == 0;
        }

        const float kFineChunkSize = kSurfaceChunkSize / 4;

        public static int3 FineCellOf(MinMaxAABB aabb)
        {
            return (int3)math.floor((aabb.Min + aabb.Max) * (0.5f / kFineChunkSize));
        }

        // The chunk of an 8 m cell's 32 m (level 0, the same as CellOf), 16 m (1) or 8 m (2) cell
        public static uint ChunkOfCell(int3 fineCell, int level)
        {
            if (level == 0)
                return math.hash(FloorDivide(fineCell, 4));
            if (level == 1)
                return math.hash(new int4(FloorDivide(fineCell, 2), 1));
            return math.hash(new int4(fineCell, 2));
        }

        static int3 FloorDivide(int3 value, int divisor)
        {
            var quotient = value / divisor;
            return quotient - math.select(int3.zero, new int3(1), (value % divisor != 0) & (value < 0));
        }

        // What a surface asks of the lightmap: its chart's rectangle
        public static float LightmapArea(ref ChiselBrushRenderBuffer buffer, ChiselQuerySurface surface)
        {
            var chart = buffer.surfaces[surface.surfaceIndex].lightmapChart;
            var size  = math.max(chart.zw - chart.xy, float2.zero);
            return size.x * size.y;
        }

        const float kChunkSize = 16.0f;

        // The cell of space a brush's output is centred in: a collider is a mesh per physics material and chunk, so a
        // change rebakes only the chunks it touched
        public static uint ChunkOf(ref ChiselBrushRenderBuffer brushRenderBuffer)
        {
            ref var surfaces = ref brushRenderBuffer.surfaces;
            var min = new float3(float.PositiveInfinity);
            var max = new float3(float.NegativeInfinity);
            for (int s = 0; s < surfaces.Length; s++)
            {
                if (surfaces[s].indexCount == 0)
                    continue;
                min = math.min(min, surfaces[s].aabb.Min);
                max = math.max(max, surfaces[s].aabb.Max);
            }
            if (math.any(min > max))
                return 0;
            var cell = (int3)math.floor((min + max) * (0.5f / kChunkSize));
            return math.hash(cell);
        }

        public unsafe struct ByGroupKey : System.Collections.Generic.IComparer<int>
        {
            [NativeDisableUnsafePtrRestriction] public SurfaceGroupKey* keys;
            public readonly int Compare(int x, int y) => keys[x].CompareTo(keys[y]);
        }
    }

    [BurstCompile(CompileSynchronously = true)]
    struct PrepareSubSectionKeysJob : IJobParallelFor
    {
        // Read
        [NoAlias, ReadOnly] public NativeArray<MeshQuery>   meshQueries;
        [NoAlias, ReadOnly] public NativeList<BrushData>    brushRenderData;

        // Write
        public Allocator allocator;
        // per range and query (range * queries + query)
        [NativeDisableParallelForRestriction] [NoAlias] public NativeArray<PrepareSubSections.RangeGroups> ranges;
        // per range: its brushes' chunks
        [NativeDisableParallelForRestriction] [NoAlias] public NativeArray<UnsafeList<uint>> rangeChunks;
        public float lightmapAreaBudget;

        public unsafe void Execute(int range)
        {
            var queryCount = meshQueries.Length;
            PrepareSubSections.Range(range, brushRenderData.Length, out var first, out var end);
            var anyChunked = false;
            var chunked = stackalloc bool[queryCount];
            var surfaceChunked = stackalloc bool[queryCount];
            var oneMaterial    = stackalloc bool[queryCount];
            var split          = stackalloc bool[queryCount];
            var ids     = stackalloc UnsafeHashMap<SurfaceGroupKey, int>[queryCount];
            var lastKey = stackalloc SurfaceGroupKey[queryCount];
            var lastId  = stackalloc int[queryCount];
            for (int t = 0; t < queryCount; t++)
            {
                chunked[t] = PrepareSubSections.Chunked(meshQueries[t]);
                surfaceChunked[t] = PrepareSubSections.SurfaceChunked(meshQueries[t]);
                oneMaterial[t]    = PrepareSubSections.OneMaterial(meshQueries[t]);
                split[t]          = PrepareSubSections.LightmapSplit(meshQueries[t], lightmapAreaBudget);
                anyChunked |= chunked[t];
                ids[t] = new UnsafeHashMap<SurfaceGroupKey, int>(16, Allocator.Temp);
                lastId[t] = -1;
                ranges[range * queryCount + t] = new PrepareSubSections.RangeGroups
                {
                    keys    = new UnsafeList<SurfaceGroupKey>(16, allocator),
                    counts  = new UnsafeList<int>(16, allocator),
                    groupOf = new UnsafeList<int>(64, allocator),
                    cells   = split[t] ? new UnsafeList<int3>(16, allocator) : default,
                    areas   = split[t] ? new UnsafeList<float>(16, allocator) : default
                };
            }
            var groups = (PrepareSubSections.RangeGroups*)ranges.GetUnsafePtr() + range * queryCount;
            var chunks = new UnsafeList<uint>(math.max(1, end - first), allocator);
            for (int b = first; b < end; b++)
            {
                ref var buffer = ref brushRenderData[b].brushRenderBuffer.Value;
                var chunk = anyChunked ? PrepareSubSections.ChunkOf(ref buffer) : 0;
                chunks.Add(chunk);
                ref var querySurfaces = ref buffer.querySurfaces;
                for (int t = 0; t < queryCount; t++)
                {
                    ref var surfaces = ref querySurfaces[t].surfaces;
                    if (surfaces.Length == 0)
                        continue;
                    ref var query = ref groups[t];
                    for (int s = 0; s < surfaces.Length; s++)
                    {
                        SurfaceGroupKey key;
                        var cell = int3.zero;
                        if (split[t])
                        {
                            cell = PrepareSubSections.FineCellOf(buffer.surfaces[surfaces[s].surfaceIndex].aabb);
                            key  = new SurfaceGroupKey { parameter = surfaces[s].surfaceParameter, chunk = PrepareSubSections.ChunkOfCell(cell, 2) };
                        } else
                            key = PrepareSubSections.KeyOf(ref buffer, surfaces[s], chunk, chunked[t], surfaceChunked[t], oneMaterial[t]);
                        if (lastId[t] < 0 || !key.Equals(lastKey[t]))
                        {
                            if (!ids[t].TryGetValue(key, out lastId[t]))
                            {
                                lastId[t] = query.keys.Length;
                                ids[t].Add(key, lastId[t]);
                                query.keys.Add(key);
                                query.counts.Add(0);
                                if (split[t])
                                {
                                    query.cells.Add(cell);
                                    query.areas.Add(0);
                                }
                            }
                            lastKey[t] = key;
                        }
                        query.counts[lastId[t]]++;
                        query.groupOf.Add(lastId[t]);
                        if (split[t])
                            query.areas[lastId[t]] += PrepareSubSections.LightmapArea(ref buffer, surfaces[s]);
                    }
                }
            }
            rangeChunks[range] = chunks;
            for (int t = 0; t < queryCount; t++)
                ids[t].Dispose();
        }
    }

    // A query's groups over all ranges: the distinct keys sorted, where each range's part of each group starts; the
    // query's list made
    [BurstCompile(CompileSynchronously = true)]
    struct PrepareSubSectionsJob : IJobParallelFor
    {
        // Read
        [NoAlias, ReadOnly] public NativeArray<MeshQuery>   meshQueries;
        public float lightmapAreaBudget;

        // Read, Write
        public Allocator allocator;
        [NativeDisableParallelForRestriction] [NoAlias] public NativeArray<PrepareSubSections.RangeGroups> ranges;
        [NoAlias] public NativeArray<UnsafeList<SubMeshSurface>> subMeshSurfaces;

        public unsafe void Execute(int t)
        {
            var queryCount = meshQueries.Length;
            if (subMeshSurfaces[t].IsCreated)
            {
                // should not happen
                subMeshSurfaces[t].Dispose();
                subMeshSurfaces[t] = default;
            }
            var parts = (PrepareSubSections.RangeGroups*)ranges.GetUnsafePtr();
            if (PrepareSubSections.LightmapSplit(meshQueries[t], lightmapAreaBudget))
                SplitForLightmaps(parts, queryCount, t);
            else
            {
                for (int r = 0; r < PrepareSubSections.kRangeCount; r++)
                {
                    ref var part = ref parts[r * queryCount + t];
                    part.finalKeys   = part.keys;
                    part.finalCounts = part.counts;
                }
            }
            var ids        = new UnsafeHashMap<SurfaceGroupKey, int>(16, Allocator.Temp);
            var keys       = new UnsafeList<SurfaceGroupKey>(16, Allocator.Temp);
            var totals     = new UnsafeList<int>(16, Allocator.Temp);
            var globalOf   = new UnsafeList<int>(64, Allocator.Temp);
            var surfaceCount = 0;
            for (int r = 0; r < PrepareSubSections.kRangeCount; r++)
            {
                var part = parts[r * queryCount + t];
                for (int l = 0; l < part.finalKeys.Length; l++)
                {
                    if (!ids.TryGetValue(part.finalKeys[l], out var g))
                    {
                        g = keys.Length;
                        ids.Add(part.finalKeys[l], g);
                        keys.Add(part.finalKeys[l]);
                        totals.Add(0);
                    }
                    totals[g] += part.finalCounts[l];
                    globalOf.Add(g);
                }
                surfaceCount += part.groupOf.Length;
            }

            var list = new UnsafeList<SubMeshSurface>(math.max(1, surfaceCount), allocator);
            list.Resize(surfaceCount, NativeArrayOptions.UninitializedMemory);
            subMeshSurfaces[t] = list;
            if (keys.Length > 0)
            {
                var order = new NativeArray<int>(keys.Length, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
                for (int g = 0; g < order.Length; g++)
                    order[g] = g;
                order.Sort(new PrepareSubSections.ByGroupKey { keys = keys.Ptr });
                var next = new NativeArray<int>(keys.Length, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
                for (int g = 0, offset = 0; g < order.Length; g++)
                {
                    next[order[g]] = offset;
                    offset += totals[order[g]];
                }
                // earlier ranges first: brush order within a group
                for (int r = 0, i = 0; r < PrepareSubSections.kRangeCount; r++)
                {
                    ref var part = ref parts[r * queryCount + t];
                    for (int l = 0; l < part.finalKeys.Length; l++, i++)
                    {
                        var g = globalOf[i];
                        var count = part.finalCounts[l];
                        part.finalCounts[l] = next[g];
                        next[g] += count;
                    }
                }
                next.Dispose();
                order.Dispose();
            }
            globalOf.Dispose(); totals.Dispose(); keys.Dispose(); ids.Dispose();
        }

        // Each 8 m key to its 32 m cell when that cell's lightmap fits, else its 16 m cell when that one fits, else its
        // own; a cell with one surface fits. Depends only on what is in the cell, so an edit changes only its own cells.
        unsafe void SplitForLightmaps(PrepareSubSections.RangeGroups* parts, int queryCount, int t)
        {
            // area and surface count of every 32 m and 16 m cell
            var sums = new UnsafeHashMap<SurfaceGroupKey, float2>(64, Allocator.Temp);
            for (int r = 0; r < PrepareSubSections.kRangeCount; r++)
            {
                ref var part = ref parts[r * queryCount + t];
                for (int l = 0; l < part.keys.Length; l++)
                {
                    for (int level = 0; level < 2; level++)
                    {
                        var key = new SurfaceGroupKey { parameter = part.keys[l].parameter, chunk = PrepareSubSections.ChunkOfCell(part.cells[l], level) };
                        sums.TryGetValue(key, out var sum);
                        sums[key] = sum + new float2(part.areas[l], part.counts[l]);
                    }
                }
            }
            var local = new UnsafeHashMap<SurfaceGroupKey, int>(16, Allocator.Temp);
            for (int r = 0; r < PrepareSubSections.kRangeCount; r++)
            {
                ref var part = ref parts[r * queryCount + t];
                part.finalOf     = new UnsafeList<int>(math.max(1, part.keys.Length), allocator);
                part.finalKeys   = new UnsafeList<SurfaceGroupKey>(math.max(1, part.keys.Length), allocator);
                part.finalCounts = new UnsafeList<int>(math.max(1, part.keys.Length), allocator);
                local.Clear();
                for (int l = 0; l < part.keys.Length; l++)
                {
                    var level = 2;
                    for (int coarse = 0; coarse < 2; coarse++)
                    {
                        var key = new SurfaceGroupKey { parameter = part.keys[l].parameter, chunk = PrepareSubSections.ChunkOfCell(part.cells[l], coarse) };
                        var sum = sums[key];
                        if (sum.x <= lightmapAreaBudget || sum.y <= 1)
                        {
                            level = coarse;
                            break;
                        }
                    }
                    var final = new SurfaceGroupKey { parameter = part.keys[l].parameter, chunk = PrepareSubSections.ChunkOfCell(part.cells[l], level) };
                    if (!local.TryGetValue(final, out var f))
                    {
                        f = part.finalKeys.Length;
                        local.Add(final, f);
                        part.finalKeys.Add(final);
                        part.finalCounts.Add(0);
                    }
                    part.finalCounts[f] += part.counts[l];
                    part.finalOf.Add(f);
                }
            }
            local.Dispose();
            sums.Dispose();
        }
    }

    // A range's surfaces written where PrepareSubSectionsJob put them; its lists freed
    [BurstCompile(CompileSynchronously = true)]
    struct ScatterSubSectionsJob : IJobParallelFor
    {
        // Read
        [NoAlias, ReadOnly] public NativeArray<MeshQuery>   meshQueries;
        [NoAlias, ReadOnly] public NativeList<BrushData>    brushRenderData;
        // the lists' contents are written, each range its own elements
        [NoAlias, ReadOnly] public NativeArray<UnsafeList<SubMeshSurface>> subMeshSurfaces;

        // Read, Write
        [NativeDisableParallelForRestriction] [NoAlias] public NativeArray<PrepareSubSections.RangeGroups> ranges;
        [NativeDisableParallelForRestriction] [NoAlias] public NativeArray<UnsafeList<uint>> rangeChunks;

        public unsafe void Execute(int range)
        {
            var queryCount = meshQueries.Length;
            PrepareSubSections.Range(range, brushRenderData.Length, out var first, out var end);
            var groups = (PrepareSubSections.RangeGroups*)ranges.GetUnsafePtr() + range * queryCount;
            var next    = stackalloc int[queryCount];
            for (int t = 0; t < queryCount; t++)
                next[t] = 0;
            var chunks = rangeChunks[range];
            for (int b = first; b < end; b++)
            {
                var brushRenderBuffer = brushRenderData[b].brushRenderBuffer;
                ref var querySurfaces = ref brushRenderBuffer.Value.querySurfaces;
                for (int t = 0; t < queryCount; t++)
                {
                    ref var surfaces = ref querySurfaces[t].surfaces;
                    if (surfaces.Length == 0)
                        continue;
                    ref var query = ref groups[t];
                    var brushNodeID = querySurfaces[t].brushNodeID;
                    var list = subMeshSurfaces[t];
                    for (int s = 0; s < surfaces.Length; s++)
                    {
                        var localGroup = query.groupOf[next[t]++];
                        var group = query.finalOf.IsCreated ? query.finalOf[localGroup] : localGroup;
                        var key   = query.finalKeys[group];
                        var index = query.finalCounts[group]++;
                        list[index] = new SubMeshSurface
                        {
                            brushNodeID       = brushNodeID,
                            surfaceIndex      = surfaces[s].surfaceIndex,
                            surfaceParameter  = key.parameter,
                            chunk             = key.chunk,
                            vertexCount       = surfaces[s].vertexCount,
                            indexCount        = surfaces[s].indexCount,
                            surfaceHashValue  = surfaces[s].surfaceHashValue,
                            geometryHashValue = surfaces[s].geometryHashValue,
                            brushRenderBuffer = brushRenderBuffer,
                        };
                    }
                }
            }
            for (int t = 0; t < queryCount; t++)
            {
                ref var query = ref groups[t];
                if (query.finalOf.IsCreated)
                {
                    query.finalOf.Dispose(); query.finalKeys.Dispose(); query.finalCounts.Dispose();
                }
                if (query.cells.IsCreated)
                {
                    query.cells.Dispose(); query.areas.Dispose();
                }
                query.keys.Dispose(); query.counts.Dispose(); query.groupOf.Dispose();
            }
            chunks.Dispose();
            rangeChunks[range] = default;
        }
    }

    [BurstCompile(CompileSynchronously = true)]
    struct SortSurfacesParallelJob : IJob
    {
        // Read
        [NoAlias, ReadOnly] public NativeArray<MeshQuery>                  meshQueries;
        [NoAlias, ReadOnly] public NativeArray<UnsafeList<SubMeshSurface>> subMeshSurfaces;

        // Write
        [NoAlias, WriteOnly] public NativeList<SubMeshDescriptions> subMeshDescriptions;

        public void Execute()
        {
            // TODO: figure out why this order matters
            for (int t = 0; t < subMeshSurfaces.Length; t++)
            {
                Execute(t);
            }
        }

        public void Execute(int t)
        {
            var subMeshSurfaceList  = subMeshSurfaces[t];
            var surfaceCount        = subMeshSurfaceList.Length;
            if (surfaceCount == 0)
                return;

            var meshQuery       = meshQueries[t];

			NativeList<SubMeshDescriptions> sectionSubMeshCounts; 
			using var _sectionSubMeshCounts = sectionSubMeshCounts = new NativeList<SubMeshDescriptions>(surfaceCount, Allocator.Temp);
            var currentSubMesh  = new SubMeshDescriptions
            {
                meshQueryIndex      = t,
                subMeshQueryIndex   = 0,
                meshQuery           = meshQuery,
                surfacesOffset      = 0,
                surfaceParameter    = subMeshSurfaceList[0].surfaceParameter,
                chunk               = subMeshSurfaceList[0].chunk
            };
            for (int b = 0; b < surfaceCount; b++)
            {
                var subMeshSurface              = subMeshSurfaceList[b];
                var surfaceParameter            = subMeshSurface.surfaceParameter;
                var surfaceVertexCount          = subMeshSurface.vertexCount;
                var surfaceIndexCount           = subMeshSurface.indexCount;

                if (currentSubMesh.surfaceParameter != surfaceParameter || currentSubMesh.chunk != subMeshSurface.chunk)
                {
                    // Store the previous subMeshCount
                    if (currentSubMesh.indexCount > 0 && currentSubMesh.vertexCount > 0)
                        sectionSubMeshCounts.AddNoResize(currentSubMesh);
                        
                    // Create the new SubMeshCount
                    currentSubMesh.surfaceHashValue = 0;
                    currentSubMesh.geometryHashValue = 0;
                    
                    currentSubMesh.indexCount = 0;
                    currentSubMesh.vertexCount = 0;

					currentSubMesh.surfacesOffset += currentSubMesh.surfacesCount;
					currentSubMesh.surfacesCount = 0;
                    currentSubMesh.surfaceParameter = surfaceParameter;
                    currentSubMesh.chunk = subMeshSurface.chunk;
                    currentSubMesh.subMeshQueryIndex++;
                } 

                currentSubMesh.indexCount += surfaceIndexCount;
                currentSubMesh.vertexCount += surfaceVertexCount;
                currentSubMesh.surfaceHashValue  = math.hash(new uint2(currentSubMesh.surfaceHashValue,  subMeshSurface.surfaceHashValue));
                currentSubMesh.geometryHashValue = math.hash(new uint2(currentSubMesh.geometryHashValue, subMeshSurface.geometryHashValue));
                currentSubMesh.surfacesCount++;
            }

            // Store the last subMeshCount
            if (currentSubMesh.indexCount > 0 && currentSubMesh.vertexCount > 0)
                sectionSubMeshCounts.AddNoResize(currentSubMesh);

            subMeshDescriptions.AddRangeNoResize(sectionSubMeshCounts);
		}
    }
    
    [BurstCompile(CompileSynchronously = true)]
    struct GatherSurfacesJob : IJob
    {
        // Read / Write
        [NoAlias] public NativeList<SubMeshDescriptions>              subMeshDescriptions;

        // Write
        [NoAlias, WriteOnly] public NativeList<SubMeshSection>  subMeshSections;
            
        // A query whose materials each get a mesh of their own: one that draws (a shadow-only one draws everything with
        // one material)
        internal static bool OneMeshPerMaterial(MeshQuery query)
        {
            return query.LayerParameterIndex == SurfaceParameterIndex.RenderMaterial &&
                   (query.LayerQuery & SurfaceDestinationFlags.Renderable) != 0;
        }

        public void Execute()
        {
            if (subMeshDescriptions.Length == 0)
                return;

            int descriptionIndex = 0;
            //var contentsIndex = 0;
            if (subMeshDescriptions[0].meshQuery.LayerParameterIndex == SurfaceParameterIndex.None ||
                subMeshDescriptions[0].meshQuery.LayerParameterIndex == SurfaceParameterIndex.RenderMaterial)
            {
                var prevQuery = subMeshDescriptions[0].meshQuery;
                var prevChunk = subMeshDescriptions[0].chunk;
                var startIndex = 0;
                for (; descriptionIndex < subMeshDescriptions.Length; descriptionIndex++)
                {
                    var subMeshCount = subMeshDescriptions[descriptionIndex];
                    // Exit when layerParameterIndex is no longer LayerParameter1/None
                    if (subMeshCount.meshQuery.LayerParameterIndex != SurfaceParameterIndex.None &&
                        subMeshCount.meshQuery.LayerParameterIndex != SurfaceParameterIndex.RenderMaterial)
                        break;

                    // a render query's materials each get a mesh of their own, so a mesh changes only with its material, and
                    // so does each chunk
                    var currQuery = subMeshCount.meshQuery;
                    if (descriptionIndex == startIndex ||
                        (prevQuery == currQuery && !OneMeshPerMaterial(currQuery) && prevChunk == subMeshCount.chunk))
                    {
                        continue;
                    }

                    int totalVertexCount = 0;
                    int totalIndexCount = 0;
                    for (int i = startIndex; i < descriptionIndex; i++)
                    {
                        totalVertexCount += subMeshDescriptions[i].vertexCount;
                        totalIndexCount += subMeshDescriptions[i].indexCount;
                    }

                    // Group by all subMeshCounts with same query
                    subMeshSections.AddNoResize(new SubMeshSection
                    {
                        meshQuery           = subMeshDescriptions[startIndex].meshQuery,
                        startIndex          = startIndex, 
                        endIndex            = descriptionIndex,
                        totalVertexCount    = totalVertexCount,
                        totalIndexCount     = totalIndexCount,
                    });

                    startIndex = descriptionIndex;
                    prevQuery = currQuery;
                    prevChunk = subMeshCount.chunk;
                }

                {
                    int totalVertexCount = 0;
                    int totalIndexCount = 0;
                    for (int i = startIndex; i < descriptionIndex; i++)
                    {
                        totalVertexCount += subMeshDescriptions[i].vertexCount;
                        totalIndexCount  += subMeshDescriptions[i].indexCount;
                    }

                    subMeshSections.AddNoResize(new SubMeshSection
                    {
                        meshQuery           = subMeshDescriptions[startIndex].meshQuery,
                        startIndex          = startIndex,
                        endIndex            = descriptionIndex,
                        totalVertexCount    = totalVertexCount,
                        totalIndexCount     = totalIndexCount
                    });
                }
            }
                

            if (descriptionIndex < subMeshDescriptions.Length &&
                subMeshDescriptions[descriptionIndex].meshQuery.LayerParameterIndex == SurfaceParameterIndex.PhysicsMaterial)
            {
                Debug.Assert(subMeshDescriptions[subMeshDescriptions.Length - 1].meshQuery.LayerParameterIndex == SurfaceParameterIndex.PhysicsMaterial);

                // Loop through all subMeshCounts with LayerParameter2, and create collider meshes from them
                for (int i = 0; descriptionIndex < subMeshDescriptions.Length; descriptionIndex++, i++)
                {
                    var subMeshCount = subMeshDescriptions[descriptionIndex];

                    // Exit when layerParameterIndex is no longer LayerParameter2
                    if (subMeshCount.meshQuery.LayerParameterIndex != SurfaceParameterIndex.PhysicsMaterial)
                        break;

                    subMeshSections.AddNoResize(new SubMeshSection
                    {
                        meshQuery           = subMeshCount.meshQuery,
                        startIndex          = descriptionIndex,
                        endIndex            = descriptionIndex,
                        totalVertexCount    = subMeshCount.vertexCount,
                        totalIndexCount     = subMeshCount.indexCount
                    });
                }
            }
        }
    }
    
    [BurstCompile(CompileSynchronously = true)]
    struct GenerateMeshDescriptionJob : IJob
    {
        [NoAlias, ReadOnly] public NativeList<SubMeshDescriptions> subMeshDescriptions;

        [NativeDisableParallelForRestriction]
        [NoAlias] public NativeList<GeneratedMeshDescription> meshDescriptions;

        public void Execute()
        {
            if (meshDescriptions.Capacity < subMeshDescriptions.Length)
                meshDescriptions.Capacity = subMeshDescriptions.Length;

            for (int i = 0; i < subMeshDescriptions.Length; i++)
            {
                var subMesh = subMeshDescriptions[i];

                var description = new GeneratedMeshDescription
                {
                    meshQuery           = subMesh.meshQuery,
                    surfaceParameter    = subMesh.surfaceParameter,
                    chunk               = subMesh.chunk,
                    meshQueryIndex      = subMesh.meshQueryIndex,
                    subMeshQueryIndex   = subMesh.subMeshQueryIndex,

                    geometryHashValue   = subMesh.geometryHashValue,
                    surfaceHashValue    = subMesh.surfaceHashValue,

                    vertexCount         = subMesh.vertexCount,
                    indexCount          = subMesh.indexCount
                };

                meshDescriptions.Add(description);
            }
        }
    }    
}
