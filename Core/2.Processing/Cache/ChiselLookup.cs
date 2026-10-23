using System.Collections.Generic;
using UnityEngine;
using Unity.Jobs;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Mathematics;

namespace Chisel.Core
{
    internal sealed class ChiselTreeLookup : ScriptableObject
    {
        public class Data
        {
            public JobHandle                            lastJobHandle;

            public NativeList<CompactNodeID>            brushIDValues;

            public NativeList<BlobAssetReference<BasePolygonsBlob>>             basePolygonCache;
            public NativeList<BlobAssetReference<RoutingTable>>                 routingTableCache;
            public NativeList<NodeTransformations>                              transformationCache;

            public NativeList<BlobAssetReference<BrushTreeSpaceVerticesBlob>>   treeSpaceVerticesCache;
            public NativeList<BlobAssetReference<ChiselBrushRenderBuffer>>      brushRenderBufferCache;

            public NativeList<MinMaxAABB>                                       brushTreeSpaceBoundCache;
            public NativeList<BlobAssetReference<BrushTreeSpacePlanes>>         brushTreeSpacePlaneCache;
            public NativeList<BlobAssetReference<BrushesTouchedByBrush>>        brushesTouchedByBrushCache;
            public NativeList<UnsafeList<float3>>                               loopVerticesCache;
            // Each brush as the exact CSG sees it (ExactBrush: exact planes, corners, bounds), rebuilt when the brush
            // changes (ExactInputJob). Only kept up to date while the tree is built exactly (builtExact).
            public NativeList<BlobAssetReference<ExactBrush>>                   exactBrushCache;

            public NativeList<ChiselDecalInstance>                              decalInstances;
            // where each decal was in the list SetDecals was given (ChiselDecalStore)
            public NativeList<int>                                              decalSequences;
            public NativeList<MinMaxAABB>                                       decalBounds;
            public NativeList<ChiselDecalTarget>                                decalTargets;
            public NativeList<DecalVolume>                                      decalVolumes;
            public NativeList<ChiselDecalTarget>                                decalVolumeTargets;
            public NativeList<MinMaxAABB>                                       changedDecalBounds;
            // The surfaces connected or disconnected by hand (ChiselHotspotLinkStore)
            public NativeList<HotspotLink>                                      hotspotLinks;

            public NativeParallelHashMap<CompactNodeID, BlobAssetReference<ChiselBrushRenderBuffer>> brushRenderBufferLookup;

            // Whether the tree's output was last built by the exact CSG (TreeUpdate.kExactCSG). An update by the other
            // algorithm rebuilds every brush of the tree, so the two never mix in one tree's output.
            public bool                                                         builtExact;
            // What each hotspot layout was at the last update (ChiselHotspotStore.LayoutHashes, by id - 1)
            public NativeList<uint>                                             hotspotLayoutHashes;
            // Where the weld's last step moved the corners it moved at the last update (from, to; OutputModelWeld)
            public NativeList<float3x2>                                         collapseMoves;
            // What the hotspot cut made of each brush's surfaces, kept while its inputs stay the same (OutputHotspotSections.CutModel)
            public NativeParallelHashMap<CompactNodeID, HotspotCutEntry>        hotspotCutCache;
            // what the weld did at the last update (OutputModelWeld.PrepareIncremental)
            public WeldMemory                                                   weldMemory;
            // what the hotspot stage found for each brush (HotspotStageMemory.Prepare)
            public HotspotStageMemory                                           hotspotStageMemory;
            // what the last update's hotspot grouping found (HotspotGroups.PrepareIncremental)
            public HotspotGroupMemory                                          hotspotGroupMemory;
            // What each group of hotspot surfaces was matched with, by what it was made from (HotspotGroups.Match)
            public NativeParallelHashMap<uint4, GroupMatchEntry>               hotspotMatchCache;
            // What each render mesh was last made from, by (query, material): a mesh made from the same is kept
            public NativeParallelHashMap<RenderMeshKey, uint4>                 renderMeshHashes;

			internal void Initialize()
            {
                brushIDValues               = new NativeList<CompactNodeID>(1000, Allocator.Persistent); // Confirmed to get disposed

				brushRenderBufferLookup     = new NativeParallelHashMap<CompactNodeID, BlobAssetReference<ChiselBrushRenderBuffer>>(1000, Allocator.Persistent); // Confirmed to get disposed

				// brushIndex
				brushTreeSpaceBoundCache    = new NativeList<MinMaxAABB>(1000, Allocator.Persistent); // Confirmed to get disposed
				transformationCache         = new NativeList<NodeTransformations>(1000, Allocator.Persistent); // Confirmed to get disposed
				basePolygonCache            = new NativeList<BlobAssetReference<BasePolygonsBlob>>(1000, Allocator.Persistent); // Confirmed to get disposed
				treeSpaceVerticesCache      = new NativeList<BlobAssetReference<BrushTreeSpaceVerticesBlob>>(1000, Allocator.Persistent); // Confirmed to get disposed
				routingTableCache           = new NativeList<BlobAssetReference<RoutingTable>>(1000, Allocator.Persistent); // Confirmed to get disposed
				brushTreeSpacePlaneCache    = new NativeList<BlobAssetReference<BrushTreeSpacePlanes>>(1000, Allocator.Persistent); // Confirmed to get disposed
				brushesTouchedByBrushCache  = new NativeList<BlobAssetReference<BrushesTouchedByBrush>>(1000, Allocator.Persistent); // Confirmed to get disposed
				brushRenderBufferCache      = new NativeList<BlobAssetReference<ChiselBrushRenderBuffer>>(1000, Allocator.Persistent); // Confirmed to get disposed
				loopVerticesCache           = new NativeList<UnsafeList<float3>>(1000, Allocator.Persistent); // Confirmed to get disposed
				exactBrushCache             = new NativeList<BlobAssetReference<ExactBrush>>(1000, Allocator.Persistent);

				decalInstances              = new NativeList<ChiselDecalInstance>(16, Allocator.Persistent);
				decalSequences              = new NativeList<int>(16, Allocator.Persistent);
				decalBounds                 = new NativeList<MinMaxAABB>(16, Allocator.Persistent);
				decalVolumes                = new NativeList<DecalVolume>(16, Allocator.Persistent);
				hotspotLinks                = new NativeList<HotspotLink>(4, Allocator.Persistent);
				hotspotLayoutHashes         = new NativeList<uint>(4, Allocator.Persistent);
				collapseMoves               = new NativeList<float3x2>(4, Allocator.Persistent);
				hotspotCutCache             = new NativeParallelHashMap<CompactNodeID, HotspotCutEntry>(16, Allocator.Persistent);
				weldMemory                  = WeldMemory.Create(Allocator.Persistent);
				hotspotStageMemory          = HotspotStageMemory.Create(Allocator.Persistent);
				hotspotGroupMemory          = HotspotGroupMemory.Create(Allocator.Persistent);
				hotspotMatchCache           = new NativeParallelHashMap<uint4, GroupMatchEntry>(16, Allocator.Persistent);
				renderMeshHashes            = new NativeParallelHashMap<RenderMeshKey, uint4>(16, Allocator.Persistent);
				decalTargets                = new NativeList<ChiselDecalTarget>(16, Allocator.Persistent);
				decalVolumeTargets          = new NativeList<ChiselDecalTarget>(16, Allocator.Persistent);
				changedDecalBounds          = new NativeList<MinMaxAABB>(16, Allocator.Persistent);
            }

            internal void EnsureCapacity(int brushCount)
            {
                if (brushRenderBufferLookup.Capacity < brushCount)
                    brushRenderBufferLookup.Capacity = brushCount;

                if (brushIDValues.Capacity < brushCount)
                    brushIDValues.Capacity = brushCount;

                if (basePolygonCache.Capacity < brushCount)
                    basePolygonCache.Capacity = brushCount;

                if (brushTreeSpaceBoundCache.Capacity < brushCount)
                    brushTreeSpaceBoundCache.Capacity = brushCount;

                if (treeSpaceVerticesCache.Capacity < brushCount)
                    treeSpaceVerticesCache.Capacity = brushCount;

                if (routingTableCache.Capacity < brushCount)
                    routingTableCache.Capacity = brushCount;

                if (brushTreeSpacePlaneCache.Capacity < brushCount)
                    brushTreeSpacePlaneCache.Capacity = brushCount;

                if (brushesTouchedByBrushCache.Capacity < brushCount)
                    brushesTouchedByBrushCache.Capacity = brushCount;

                if (transformationCache.Capacity < brushCount)
                    transformationCache.Capacity = brushCount;

                if (brushRenderBufferCache.Capacity < brushCount)
                    brushRenderBufferCache.Capacity = brushCount;

                if (loopVerticesCache.Capacity < brushCount)
                    loopVerticesCache.Capacity = brushCount;

                if (exactBrushCache.Capacity < brushCount)
                    exactBrushCache.Capacity = brushCount;
            }

            internal void Dispose()
            {
                // Confirmed to be called
                if (brushIDValues.IsCreated)
                    brushIDValues.Dispose();
                brushIDValues = default;
                if (basePolygonCache.IsCreated)
                {
                    // Confirmed to be called
                    for (int i = 0; i < basePolygonCache.Length; i++)
					{
						if (basePolygonCache[i].IsCreated)
							basePolygonCache[i].Dispose();
                        basePolygonCache[i] = default;
                    }
                    basePolygonCache.Clear(); 
                    basePolygonCache.Dispose();
                }
                basePolygonCache = default;
                if (brushTreeSpaceBoundCache.IsCreated)
                {
                    brushTreeSpaceBoundCache.Clear();
                    brushTreeSpaceBoundCache.Dispose();
                }
                brushTreeSpaceBoundCache = default;
                if (treeSpaceVerticesCache.IsCreated)
                {
					// Confirmed to be called
                    for (int i = 0; i < treeSpaceVerticesCache.Length; i++)
					{
						if (treeSpaceVerticesCache[i].IsCreated)
							treeSpaceVerticesCache[i].Dispose();
                        treeSpaceVerticesCache[i] = default;
                    }
                    treeSpaceVerticesCache.Clear();
                    treeSpaceVerticesCache.Dispose();
                }
                treeSpaceVerticesCache = default;
                if (routingTableCache.IsCreated)
				{
					// Confirmed to be called
					for (int i = 0; i < routingTableCache.Length; i++)
                    {
                        if (routingTableCache[i].IsCreated)
							routingTableCache[i].Dispose();
                        routingTableCache[i] = default;
                    }
                    routingTableCache.Clear();
                    routingTableCache.Dispose();
                }
                routingTableCache = default;
                if (brushTreeSpacePlaneCache.IsCreated)
				{
                    // Confirmed to be called
					for (int i = 0; i < brushTreeSpacePlaneCache.Length; i++)
                    {
                        if (brushTreeSpacePlaneCache[i].IsCreated)
							brushTreeSpacePlaneCache[i].Dispose();
                        brushTreeSpacePlaneCache[i] = default;
                    }
                    brushTreeSpacePlaneCache.Clear();
                    brushTreeSpacePlaneCache.Dispose();
                }
                brushTreeSpacePlaneCache = default;
                if (brushesTouchedByBrushCache.IsCreated)
				{
                    // Confirmed to be called
					for (int i = 0; i < brushesTouchedByBrushCache.Length; i++)
                    {
                        if (brushesTouchedByBrushCache[i].IsCreated)
                            brushesTouchedByBrushCache[i].Dispose();
                        brushesTouchedByBrushCache[i] = default;
                    }
                    brushesTouchedByBrushCache.Clear();
                    brushesTouchedByBrushCache.Dispose();
                }
                brushesTouchedByBrushCache = default;
                if (transformationCache.IsCreated)
                {
                    transformationCache.Clear();
                    transformationCache.Dispose();
                }
                transformationCache = default;
                if (brushRenderBufferCache.IsCreated)
                {
					// Confirmed to be called
					for (int i = 0; i < brushRenderBufferCache.Length; i++)
                    {
                        if (brushRenderBufferCache[i].IsCreated)
                            brushRenderBufferCache[i].Dispose();
                        brushRenderBufferCache[i] = default;
                    }
                    brushRenderBufferCache.Clear();
                    brushRenderBufferCache.Dispose();
                }
                brushRenderBufferCache = default;
                if (loopVerticesCache.IsCreated)
                {
                    for (int i = 0; i < loopVerticesCache.Length; i++)
                    {
                        if (loopVerticesCache[i].IsCreated)
                            loopVerticesCache[i].Dispose();
                        loopVerticesCache[i] = default;
                    }
                    loopVerticesCache.Clear();
                    loopVerticesCache.Dispose();
                }
                loopVerticesCache = default;
                if (exactBrushCache.IsCreated)
                {
                    for (int i = 0; i < exactBrushCache.Length; i++)
                    {
                        if (exactBrushCache[i].IsCreated)
                            exactBrushCache[i].Dispose();
                        exactBrushCache[i] = default;
                    }
                    exactBrushCache.Clear();
                    exactBrushCache.Dispose();
                }
                exactBrushCache = default;
                if (decalInstances.IsCreated)     decalInstances.Dispose();
                if (decalSequences.IsCreated)     decalSequences.Dispose();
                if (decalBounds.IsCreated)        decalBounds.Dispose();
                if (decalVolumes.IsCreated)       decalVolumes.Dispose();
                if (decalTargets.IsCreated)       decalTargets.Dispose();
                if (decalVolumeTargets.IsCreated) decalVolumeTargets.Dispose();
                decalTargets = default;
                decalVolumeTargets = default;
                if (changedDecalBounds.IsCreated) changedDecalBounds.Dispose();
                if (hotspotLinks.IsCreated)       hotspotLinks.Dispose();
                hotspotLinks = default;
                if (hotspotLayoutHashes.IsCreated) hotspotLayoutHashes.Dispose();
                hotspotLayoutHashes = default;
                if (collapseMoves.IsCreated) collapseMoves.Dispose();
                collapseMoves = default;
                if (hotspotCutCache.IsCreated)
                {
                    foreach (var entry in hotspotCutCache)
                    {
                        if (entry.Value.result.IsCreated)
                            entry.Value.result.Dispose();
                    }
                    hotspotCutCache.Dispose();
                }
                hotspotCutCache = default;
                weldMemory.Dispose();
                weldMemory = default;
                hotspotStageMemory.Dispose();
                hotspotStageMemory = default;
                hotspotGroupMemory.Dispose();
                hotspotGroupMemory = default;
                if (hotspotMatchCache.IsCreated)
                {
                    foreach (var entry in hotspotMatchCache)
                        entry.Value.match.Dispose();
                    hotspotMatchCache.Dispose();
                }
                hotspotMatchCache = default;
                if (renderMeshHashes.IsCreated) renderMeshHashes.Dispose();
                renderMeshHashes = default;
                decalInstances = default;
                decalSequences = default;
                decalBounds = default;
                decalVolumes = default;
                changedDecalBounds = default;
                if (brushRenderBufferLookup.IsCreated)
                    brushRenderBufferLookup.Dispose();
                brushRenderBufferLookup = default;
                builtExact = false;
            }
        }

        static ChiselTreeLookup _singleton;

        static void UpdateValue()
        {
            if (_singleton == null)
            {
                _singleton = ScriptableObject.CreateInstance<ChiselTreeLookup>();
                _singleton.hideFlags = HideFlags.HideAndDontSave;
            }
        }

        public static ChiselTreeLookup Value
        {
            get
            {
                if (_singleton == null)
                    UpdateValue();
                return _singleton;
            }
        }

        public Data this[CSGTree treeNode]
        {
            get
            {
                if (!chiselTreeData.TryGetValue(treeNode, out var data))
                {
                    data = new Data();
                    data.Initialize();
                    chiselTreeData[treeNode] = data;
                }
                return data;
            }
        }

        readonly Dictionary<CSGTree, Data> chiselTreeData = new();

        internal int  TreeCount                 => chiselTreeData.Count;
        internal bool HasTree(CSGTree treeNode) => chiselTreeData.ContainsKey(treeNode);

        /// <summary>Releases the caches of a tree that is being destroyed.</summary>
        internal static void ReleaseTree(CSGTree treeNode)
        {
            if (_singleton == null || !_singleton.chiselTreeData.TryGetValue(treeNode, out var data))
                return;
            _singleton.chiselTreeData.Remove(treeNode);
            data.lastJobHandle.Complete();
            data.Dispose();
        }

        /// <summary>Releases the caches of every tree, for when all trees go at once.</summary>
        internal static void ReleaseAllTrees()
        {
            if (_singleton != null)
                _singleton.DisposeAllTrees();
        }

        void DisposeAllTrees()
        {
            foreach (var data in chiselTreeData.Values)
            {
                if (data == null)
                    continue;
                data.lastJobHandle.Complete();
                data.Dispose();
            }
            chiselTreeData.Clear();
        }

        void Dispose()
		{
			// Confirmed to be called
			DisposeAllTrees();
			if (_singleton == this)
				_singleton = null;
		}

        internal void OnDisable()
        {
			Dispose();
        }

		private void OnDestroy()
		{
			Dispose();
		}
	}
}
