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
            public NativeArray<ChiselLayerParameters>   parameters; 
            public NativeParallelHashSet<int>           allKnownBrushMeshIndices;

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
            public NativeList<MinMaxAABB>                                       decalBounds;
            public NativeList<ChiselDecalTarget>                                decalTargets;
            public NativeList<DecalVolume>                                      decalVolumes;
            public NativeList<ChiselDecalTarget>                                decalVolumeTargets;
            public NativeList<MinMaxAABB>                                       changedDecalBounds;

            public NativeParallelHashMap<CompactNodeID, BlobAssetReference<ChiselBrushRenderBuffer>> brushRenderBufferLookup;

            // Whether the tree's output was last built by the exact CSG (TreeUpdate.kExactCSG). An update by the other
            // algorithm rebuilds every brush of the tree, so the two never mix in one tree's output.
            public bool                                                         builtExact;

			internal void Initialize()
            {
                brushIDValues               = new NativeList<CompactNodeID>(1000, Allocator.Persistent); // Confirmed to get disposed
				allKnownBrushMeshIndices    = new NativeParallelHashSet<int>(1000, Allocator.Persistent); // Confirmed to get disposed

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
				decalBounds                 = new NativeList<MinMaxAABB>(16, Allocator.Persistent);
				decalVolumes                = new NativeList<DecalVolume>(16, Allocator.Persistent);
				decalTargets                = new NativeList<ChiselDecalTarget>(16, Allocator.Persistent);
				decalVolumeTargets          = new NativeList<ChiselDecalTarget>(16, Allocator.Persistent);
				changedDecalBounds          = new NativeList<MinMaxAABB>(16, Allocator.Persistent);

				parameters = new NativeArray<ChiselLayerParameters>(SurfaceDestinationParameters.ParameterCount, Allocator.Persistent); // Confirmed to get disposed
				for (int i = 0; i < parameters.Length; i++)
                {
                    var parameter = parameters[i];
                    parameter.Initialize(); 
                    Debug.Assert(parameter.IsCreated);
                    parameters[i] = parameter;
                }
            }

            internal void EnsureCapacity(int brushCount)
            {
                if (brushRenderBufferLookup.Capacity < brushCount)
                    brushRenderBufferLookup.Capacity = brushCount;

                if (brushIDValues.Capacity < brushCount)
                    brushIDValues.Capacity = brushCount;

                if (allKnownBrushMeshIndices.Capacity < brushCount)
                    allKnownBrushMeshIndices.Capacity = brushCount;

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
                if (allKnownBrushMeshIndices.IsCreated)
                {
                    allKnownBrushMeshIndices.Clear();
                    allKnownBrushMeshIndices.Dispose();
                }
                allKnownBrushMeshIndices = default;
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
                if (decalBounds.IsCreated)        decalBounds.Dispose();
                if (decalVolumes.IsCreated)       decalVolumes.Dispose();
                if (decalTargets.IsCreated)       decalTargets.Dispose();
                if (decalVolumeTargets.IsCreated) decalVolumeTargets.Dispose();
                decalTargets = default;
                decalVolumeTargets = default;
                if (changedDecalBounds.IsCreated) changedDecalBounds.Dispose();
                decalInstances = default;
                decalBounds = default;
                decalVolumes = default;
                changedDecalBounds = default;
                if (brushRenderBufferLookup.IsCreated)
                    brushRenderBufferLookup.Dispose();
                brushRenderBufferLookup = default;
                builtExact = false;
                if (parameters.IsCreated)
				{
					// Confirmed to be called
					for (int i = 0; i < parameters.Length; i++)
                    {
                        if (parameters[i].IsCreated)
							parameters[i].Dispose();
                        parameters[i] = default;
                    }
                    parameters.Dispose();
                }
                parameters = default;
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
