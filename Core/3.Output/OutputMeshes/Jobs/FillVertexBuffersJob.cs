using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Jobs;
using UnityEngine;
using UnityEngine.Rendering;
using Debug = UnityEngine.Debug;
using ReadOnlyAttribute = Unity.Collections.ReadOnlyAttribute;
using WriteOnlyAttribute = Unity.Collections.WriteOnlyAttribute;

namespace Chisel.Core
{
    public enum ChiselMeshType
    {
        Render,
        Collider,
        DebugVisualization
    }

    public struct ChiselMeshUpdate
    {
        public int contentsIndex;
        public int colliderIndex;
        public int meshIndex;
        public int objectIndex;
        public ChiselMeshType type;
        public SubMeshSection subMeshSection;
	}
    
    [BurstCompile(CompileSynchronously = true)]
    public struct AssignMeshesJob : IJob
    {
        public const int kDebugVisualizationModeCount = 6;
        public struct DebugRenderFlags { public SurfaceDestinationFlags Item1; public SurfaceDestinationFlags Item2; };
        public static readonly DebugRenderFlags[] kGeneratedDebugRendererFlags = new DebugRenderFlags[kDebugVisualizationModeCount]
        {
            new(){ Item1 = SurfaceDestinationFlags.None                  , Item2 = SurfaceDestinationFlags.Renderable },              // is explicitly set to "not visible"
            new(){ Item1 = SurfaceDestinationFlags.RenderShadowsCasting  , Item2 = SurfaceDestinationFlags.RenderShadowsCasting },    // casts Shadows and is renderered
            new(){ Item1 = SurfaceDestinationFlags.ShadowCasting         , Item2 = SurfaceDestinationFlags.RenderShadowsCasting },    // casts Shadows and is NOT renderered (shadowOnly)
            new(){ Item1 = SurfaceDestinationFlags.RenderShadowsReceiving, Item2 = SurfaceDestinationFlags.RenderShadowsReceiving },  // any surface that receives shadows (must be rendered)
            new(){ Item1 = SurfaceDestinationFlags.Collidable            , Item2 = SurfaceDestinationFlags.Collidable },              // collider surfaces
            new(){ Item1 = SurfaceDestinationFlags.Discarded             , Item2 = SurfaceDestinationFlags.Discarded }                // all surfaces removed by the CSG algorithm
        };

        // Read
        [NoAlias, ReadOnly] public NativeList<GeneratedMeshDescription> meshDescriptions;
        [NoAlias, ReadOnly] public NativeList<SubMeshSection> subMeshSections;
        [NoAlias, ReadOnly] public NativeList<Mesh.MeshData>  meshDatas;
        
		// Write
		[NoAlias, WriteOnly] public NativeList<Mesh.MeshData> meshes;

		// Write (allocate)
        [NoAlias] public NativeList<ChiselMeshUpdate> meshUpdatesRenderable;
        [NoAlias] public NativeList<ChiselMeshUpdate> meshUpdatesColliders;
        [NoAlias] public NativeList<ChiselMeshUpdate> meshUpdatesDebugVisualization;


        [BurstDiscard]
        public static void InvalidQuery(SurfaceDestinationFlags query, SurfaceDestinationFlags mask)
        {
            Debug.Assert(false, $"Invalid debug visualization query used (query: {query}, mask: {mask})");
        }

        public void Execute() 
        {
            int meshIndex = 0;
            int colliderCount = 0;
            if (meshDescriptions.IsCreated)
			{
				if (meshUpdatesDebugVisualization.Capacity < subMeshSections.Length)
					meshUpdatesDebugVisualization.Capacity = subMeshSections.Length;
				if (meshUpdatesRenderable.Capacity < subMeshSections.Length)
					meshUpdatesRenderable.Capacity = subMeshSections.Length;
				for (int i = 0; i < subMeshSections.Length; i++)
                {
                    var subMeshSection = subMeshSections[i];
                    if (subMeshSection.meshQuery.LayerParameterIndex == SurfaceParameterIndex.None)
                    {
                        int debugVisualizationIndex = -1;
                        var query   = subMeshSection.meshQuery.LayerQuery;
                        var mask    = subMeshSection.meshQuery.LayerQueryMask;
                        for (int f = 0; f < kGeneratedDebugRendererFlags.Length; f++)
                        {
                            if (kGeneratedDebugRendererFlags[f].Item1 != query ||
                                kGeneratedDebugRendererFlags[f].Item2 != mask)
                                continue;

                            debugVisualizationIndex = f;
                            break;
                        }
                        if (debugVisualizationIndex == -1)
                        {
                            InvalidQuery(query, mask);
                            continue;
                        }

                        var meshUpdate = new ChiselMeshUpdate
                        {
                            contentsIndex  = i,
                            meshIndex      = meshIndex,
                            objectIndex    = debugVisualizationIndex,
                            type           = ChiselMeshType.DebugVisualization,
							subMeshSection = subMeshSection
						};

                        meshes.Add(meshDatas[meshIndex]);
                        meshUpdatesDebugVisualization.Add(meshUpdate);
                        //meshUpdates.Add(meshUpdate);
                        meshIndex++; 
                    } else
                    if (subMeshSection.meshQuery.LayerParameterIndex == SurfaceParameterIndex.RenderMaterial)
                    {
                        var renderIndex = (int)(subMeshSection.meshQuery.LayerQuery & SurfaceDestinationFlags.RenderShadowReceiveAndCasting);
                        var meshUpdate = new ChiselMeshUpdate
                        {
                            contentsIndex  = i,
                            meshIndex      = meshIndex,
                            objectIndex    = renderIndex,
                            type           = ChiselMeshType.Render,
						    subMeshSection = subMeshSection
                        };

                        meshes.Add(meshDatas[meshIndex]);
                        meshUpdatesRenderable.Add(meshUpdate);
                        //meshUpdates.Add(meshUpdate);
                        meshIndex++;
                    } else
                    if (subMeshSection.meshQuery.LayerParameterIndex == SurfaceParameterIndex.PhysicsMaterial)
                        colliderCount++;
                }
            }

            if (meshUpdatesColliders.Capacity < colliderCount)
                meshUpdatesColliders.Capacity = colliderCount;
            if (meshDescriptions.IsCreated)
            {
                var colliderIndex = 0;
                for (int i = 0; i < subMeshSections.Length; i++)
                {
                    var subMeshSection = subMeshSections[i];
                    if (subMeshSection.meshQuery.LayerParameterIndex != SurfaceParameterIndex.PhysicsMaterial)
                        continue;

                    var surfaceParameter = meshDescriptions[subMeshSection.startIndex].surfaceParameter;
                    var meshUpdate = new ChiselMeshUpdate
                    {
                        contentsIndex  = i,
                        colliderIndex  = colliderIndex,
                        meshIndex      = meshIndex,
                        objectIndex    = surfaceParameter,
                        type           = ChiselMeshType.Collider,
						subMeshSection = subMeshSection
					};

                    meshes.Add(meshDatas[meshIndex]);
                    meshUpdatesColliders.Add(meshUpdate);
                    //meshUpdates.Add(meshUpdate);
                    colliderIndex++;
                    meshIndex++;
                }
            }
        }
    }
    
    [BurstCompile(CompileSynchronously = true, OptimizeFor = OptimizeFor.Performance)]
    struct OutputCopyJob<Worker> : IJobParallelForDefer
        where Worker : unmanaged, IChiselOutputMeshCopier
	{
        // Read
		[NoAlias, ReadOnly] public SubMeshSource subMeshSource;
        [NoAlias, ReadOnly] public NativeList<ChiselMeshUpdate> meshUpdates;
		[NoAlias, ReadOnly] public NativeArray<VertexAttributeDescriptor> descriptors;

		// Read / Write
		[NativeDisableParallelForRestriction,
			NoAlias, NativeDisableContainerSafetyRestriction] public NativeList<Mesh.MeshData> outputMeshes;

        public void Execute(int inputMeshIndex)
        {
            var update = meshUpdates[inputMeshIndex];
            unsafe
            {
                ref var meshData = ref UnsafeUtility.ArrayElementAsRef<Mesh.MeshData>(outputMeshes.GetUnsafePtr(), update.meshIndex);
                new Worker().CopyMesh(descriptors, update.subMeshSection, subMeshSource, ref meshData);
            }
		} 
    }
    
	[BurstCompile(CompileSynchronously = true)]
	struct AllocateVertexBuffersJob : IJob
	{
		// Read
		[NoAlias, ReadOnly] public NativeList<SubMeshSection> subMeshSections;

		// Read / Write
		[NoAlias, NativeDisableParallelForRestriction] public NativeList<BlobAssetReference<SubMeshTriangleLookup>> subMeshTriangleLookups;

		public void Execute()
		{
			for (int i = 0; i < subMeshTriangleLookups.Length; i++)
			{
				if (subMeshTriangleLookups[i].IsCreated)
					subMeshTriangleLookups[i].Dispose();
			}

			if (subMeshSections.Length == 0)
				return;

			subMeshTriangleLookups.Resize(subMeshSections.Length, NativeArrayOptions.ClearMemory);
		}
	}

	// TODO: use instanceIDs so we can also use this for picking
	[BurstCompile(CompileSynchronously = true, OptimizeFor = OptimizeFor.Performance)]
    struct FindTriangleBrushIndicesJob : IJobParallelForDefer
    {
        // Read
        [NoAlias, ReadOnly] public NativeList<ChiselMeshUpdate> meshUpdates;
        [NoAlias, ReadOnly] public NativeList<SubMeshDescriptions> subMeshDescriptions;
        [NoAlias, ReadOnly] public NativeArray<UnsafeList<SubMeshSurface>> subMeshSurfaces;
		[NoAlias, ReadOnly] public CompactHierarchyManagerInstance.ReadOnlyInstanceIDLookup instanceIDLookup;

		// Read / Write
		[NoAlias, NativeDisableContainerSafetyRestriction] public NativeList<BlobAssetReference<SubMeshTriangleLookup>> subMeshTriangleLookups;
		public Allocator allocator;

		public void Execute(int renderIndex)
        {
            var update = meshUpdates[renderIndex];
			subMeshTriangleLookups[update.contentsIndex] =
				SubMeshTriangleLookup.Create(
                    update.subMeshSection, subMeshDescriptions, subMeshSurfaces, 
                    instanceIDLookup, allocator);
		}
    }        
}
