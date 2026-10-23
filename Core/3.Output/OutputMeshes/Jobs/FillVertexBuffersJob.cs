using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
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
        public ulong objectIndex;
        // a render mesh's material (each material is a mesh of its own)
        public ulong surfaceParameter;
        // the chunk of space of a collider or a render mesh
        public uint  chunk;
        // what a render mesh's lightmap layout is made from: its baked lightmap stays valid while this does
        public uint  lightmapHashValue;
        public ChiselMeshType type;
        public SubMeshSection subMeshSection;
	}
    
    // A mesh of a tree: its query, its material and, for a collider, its chunk
    public struct RenderMeshKey : System.IEquatable<RenderMeshKey>
    {
        public int   query;
        public ulong surfaceParameter;
        public uint  chunk;
        public bool Equals(RenderMeshKey other) => query == other.query && surfaceParameter == other.surfaceParameter && chunk == other.chunk;

        // a debug visualization's query can be a render or collider query too: it is told apart by this bit
        const int kDebugVisualization = 1 << 30;

        internal static RenderMeshKey Of(MeshQuery meshQuery, SubMeshDescriptions description)
        {
            var debug = meshQuery.LayerParameterIndex == SurfaceParameterIndex.None;
            return new RenderMeshKey
            {
                query            = (int)meshQuery.LayerQuery | (debug ? kDebugVisualization : 0),
                surfaceParameter = description.surfaceParameter,
                chunk            = description.chunk
            };
        }
        public override int GetHashCode() => (int)Unity.Mathematics.math.hash(new Unity.Mathematics.uint4((uint)query, (uint)surfaceParameter, (uint)(surfaceParameter >> 32), chunk));
    }

    [BurstCompile(CompileSynchronously = true)]
    public struct AssignMeshesJob : IJob
    {
        public const int kDebugVisualizationModeCount = 6;
        public struct DebugRenderFlags { public SurfaceDestinationFlags Item1; public SurfaceDestinationFlags Item2; };
        public readonly static DebugRenderFlags[] kGeneratedDebugRendererFlags = new DebugRenderFlags[kDebugVisualizationModeCount]
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
        // the sections whose render mesh is the same as last time (no mesh is filled for them)
        [NoAlias, ReadOnly] public NativeArray<bool> keptSections;
        [NoAlias, ReadOnly] public NativeArray<uint> sectionLightmapHashes;
        // the debug visualizations to make (bit i: kGeneratedDebugRendererFlags[i])
        public int debugVisualizations;
        
		// Write
		[NoAlias, WriteOnly] public NativeList<Mesh.MeshData> meshes;

		// Write (allocate)
        [NoAlias] public NativeList<ChiselMeshUpdate> meshUpdatesRenderable;
        [NoAlias] public NativeList<ChiselMeshUpdate> meshUpdatesColliders;
        [NoAlias] public NativeList<ChiselMeshUpdate> meshUpdatesDebugVisualization;
        [NoAlias] public NativeList<ChiselMeshUpdate> meshUpdatesKept;
        [NoAlias] public NativeList<ChiselMeshUpdate> meshUpdatesKeptColliders;
        [NoAlias] public NativeList<ChiselMeshUpdate> meshUpdatesKeptDebugVisualization;


        // A debug visualization's query that debugVisualizations leaves out
        public static bool HiddenDebugVisualization(MeshQuery meshQuery, int debugVisualizations)
        {
            if (meshQuery.LayerParameterIndex != SurfaceParameterIndex.None)
                return false;
            var index = DebugVisualizationIndex(meshQuery.LayerQuery, meshQuery.LayerQueryMask);
            return index >= 0 && (debugVisualizations & (1 << index)) == 0;
        }

        static int DebugVisualizationIndex(SurfaceDestinationFlags query, SurfaceDestinationFlags mask)
        {
            for (int f = 0; f < kGeneratedDebugRendererFlags.Length; f++)
            {
                if (kGeneratedDebugRendererFlags[f].Item1 == query && kGeneratedDebugRendererFlags[f].Item2 == mask)
                    return f;
            }
            return -1;
        }

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
                        var query   = subMeshSection.meshQuery.LayerQuery;
                        var mask    = subMeshSection.meshQuery.LayerQueryMask;
                        var debugVisualizationIndex = DebugVisualizationIndex(query, mask);
                        if (debugVisualizationIndex == -1)
                        {
                            InvalidQuery(query, mask);
                            continue;
                        }
                        if ((debugVisualizations & (1 << debugVisualizationIndex)) == 0)
                            continue;

                        var debugChunk = meshDescriptions[subMeshSection.startIndex].chunk;
                        if (keptSections.IsCreated && i < keptSections.Length && keptSections[i])
                        {
                            meshUpdatesKeptDebugVisualization.Add(new ChiselMeshUpdate
                            {
                                contentsIndex  = i,
                                meshIndex      = -1,
                                objectIndex    = (ulong)debugVisualizationIndex,
                                chunk          = debugChunk,
                                type           = ChiselMeshType.DebugVisualization,
                                subMeshSection = subMeshSection
                            });
                            continue;
                        }
                        var meshUpdate = new ChiselMeshUpdate
                        {
                            contentsIndex  = i,
                            meshIndex      = meshIndex,
                            objectIndex    = (ulong)debugVisualizationIndex,
                            chunk          = debugChunk,
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
                        var renderIndex = subMeshSection.meshQuery.LayerQuery.RendererIndex();
                        if (keptSections.IsCreated && i < keptSections.Length && keptSections[i])
                        {
                            meshUpdatesKept.Add(new ChiselMeshUpdate
                            {
                                contentsIndex    = i,
                                meshIndex        = -1,
                                objectIndex      = (ulong)renderIndex,
                                surfaceParameter = meshDescriptions[subMeshSection.startIndex].surfaceParameter,
                                chunk            = meshDescriptions[subMeshSection.startIndex].chunk,
                                type             = ChiselMeshType.Render,
                                subMeshSection   = subMeshSection
                            });
                            continue;
                        }
                        var meshUpdate = new ChiselMeshUpdate
                        {
                            contentsIndex    = i,
                            meshIndex        = meshIndex,
                            objectIndex      = (ulong)renderIndex,
                            surfaceParameter = GatherSurfacesJob.OneMeshPerMaterial(subMeshSection.meshQuery) ? meshDescriptions[subMeshSection.startIndex].surfaceParameter : 0,
                            chunk            = meshDescriptions[subMeshSection.startIndex].chunk,
                            lightmapHashValue = sectionLightmapHashes.IsCreated && i < sectionLightmapHashes.Length ? sectionLightmapHashes[i] : 0,
                            type             = ChiselMeshType.Render,
						    subMeshSection   = subMeshSection
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
                    var chunk            = meshDescriptions[subMeshSection.startIndex].chunk;
                    if (keptSections.IsCreated && i < keptSections.Length && keptSections[i])
                    {
                        meshUpdatesKeptColliders.Add(new ChiselMeshUpdate
                        {
                            contentsIndex  = i,
                            colliderIndex  = -1,
                            meshIndex      = -1,
                            objectIndex    = surfaceParameter,
                            chunk          = chunk,
                            type           = ChiselMeshType.Collider,
                            subMeshSection = subMeshSection
                        });
                        continue;
                    }
                    var meshUpdate = new ChiselMeshUpdate
                    {
                        contentsIndex  = i,
                        colliderIndex  = colliderIndex,
                        meshIndex      = meshIndex,
                        objectIndex    = surfaceParameter,
                        chunk          = chunk,
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
		[NoAlias, ReadOnly] public NativeArray<VertexAttributeDescriptor> descriptors;
        [NoAlias, ReadOnly] public NativeList<ChiselMeshUpdate> meshUpdates;
		[NoAlias, ReadOnly] public SubMeshSource subMeshSource;

		// Read / Write
		[NativeDisableContainerSafetyRestriction] public Mesh.MeshDataArray meshDataArray;

        public void Execute(int inputMeshIndex)
        {
            var update   = meshUpdates[inputMeshIndex];
            var meshData = meshDataArray[update.meshIndex];
            new Worker().CopyMesh(descriptors, update.subMeshSection, subMeshSource, ref meshData);
		}
    }
    
    // The render meshes made ready, one after another, and where each surface goes (ChiselOutputRenderable.Prepare)
    [BurstCompile(CompileSynchronously = true, OptimizeFor = OptimizeFor.Performance)]
    struct PrepareRenderCopiesJob : IJob
	{
        // Read
		[NoAlias, ReadOnly] public NativeArray<VertexAttributeDescriptor> descriptors;
        [NoAlias, ReadOnly] public NativeList<ChiselMeshUpdate> meshUpdates;
		[NoAlias, ReadOnly] public SubMeshSource subMeshSource;

		// Write
		[NativeDisableContainerSafetyRestriction] public Mesh.MeshDataArray meshDataArray;
        [NoAlias] public NativeList<RenderCopyRange> ranges;
        [NoAlias] public NativeList<int2> chunks;

        public void Execute()
        {
            ranges.Clear();
            chunks.Clear();
            for (int i = 0; i < meshUpdates.Length; i++)
            {
                var update   = meshUpdates[i];
                var meshData = meshDataArray[update.meshIndex];
                ChiselOutputRenderable.Prepare(descriptors, update.subMeshSection, subMeshSource, ref meshData, update.meshIndex, ranges, chunks);
            }
		}
    }

    // The render meshes' surfaces copied, a chunk of them at a time
    [BurstCompile(CompileSynchronously = true, OptimizeFor = OptimizeFor.Performance)]
    struct RenderCopyRangesJob : IJobParallelForDefer
	{
        [NoAlias, ReadOnly] public NativeArray<RenderCopyRange> ranges;
        [NoAlias, ReadOnly] public NativeArray<int2> chunks;
		// what the ranges write into
		[NativeDisableContainerSafetyRestriction] public Mesh.MeshDataArray meshDataArray;

        public void Execute(int index)
        {
            for (int r = chunks[index].x; r < chunks[index].y; r++)
                ChiselOutputRenderable.CopyRange(ranges[r]);
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

	// TODO: use entityIDs so we can also use this for picking
	[BurstCompile(CompileSynchronously = true, OptimizeFor = OptimizeFor.Performance)]
    struct FindTriangleBrushIndicesJob : IJobParallelForDefer
    {
        // Read
        [NoAlias, ReadOnly] public NativeList<ChiselMeshUpdate> meshUpdates;
        [NoAlias, ReadOnly] public NativeList<SubMeshDescriptions> subMeshDescriptions;
        [NoAlias, ReadOnly] public NativeArray<UnsafeList<SubMeshSurface>> subMeshSurfaces;
		[NoAlias, ReadOnly] public CompactHierarchyManagerInstance.ReadOnlyEntityIDLookup entityIDLookup;

		// Read / Write
		[NoAlias, NativeDisableContainerSafetyRestriction] public NativeList<BlobAssetReference<SubMeshTriangleLookup>> subMeshTriangleLookups;
		
		public void Execute(int renderIndex)
        {
            var update = meshUpdates[renderIndex];
			subMeshTriangleLookups[update.contentsIndex] =
				SubMeshTriangleLookup.Create(
                    update.subMeshSection, subMeshDescriptions, subMeshSurfaces, 
                    entityIDLookup);
		}
    }        
}
