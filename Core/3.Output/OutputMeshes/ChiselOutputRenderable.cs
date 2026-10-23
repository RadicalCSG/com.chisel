using Unity.Jobs;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;
using UnityEngine.Rendering;
using Unity.Mathematics;
using Unity.Burst;
using Unity.Entities;
using System.Runtime.CompilerServices;

namespace Chisel.Core
{
    public struct ChiselMeshUpdates
	{
        [ReadOnly] public VertexBufferContents          vertexBufferContents;
		[ReadOnly] public NativeList<ChiselMeshUpdate>  meshUpdatesRenderables;
		[ReadOnly] public NativeList<ChiselMeshUpdate>  meshUpdatesColliders;
		[ReadOnly] public NativeList<ChiselMeshUpdate>  meshUpdatesDebugVisualizations;
		// The render meshes that are the same as last time: their renderers keep what they have
		[ReadOnly] public NativeList<ChiselMeshUpdate>  meshUpdatesKept;
		// The same for the colliders: they keep their mesh and their bake
		[ReadOnly] public NativeList<ChiselMeshUpdate>  meshUpdatesKeptColliders;
		// and for the debug visualizations
		[ReadOnly] public NativeList<ChiselMeshUpdate>  meshUpdatesKeptDebugVisualizations;
        
		public Mesh.MeshDataArray meshDataArray;
    }

	internal static class ChiselOutputMeshValidation
	{
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool FitsInBuffer(int start, int count, int bufferLength)
		{
			return start >= 0 && count >= 0 && start + count <= bufferLength;
		}

		[BurstDiscard]
		public static void ReportCopyOverflow(int subMeshIndex, int start, int count, int bufferLength)
		{
			Debug.LogWarning($"Chisel mesh generation: sub-mesh {subMeshIndex} tried to copy {count} entries at {start} " +
						   $"into a {bufferLength}-entry buffer. The sub-mesh counts are out of sync with the surfaces " +
						   $"they were built from; the sub-mesh has been truncated to what actually fit. This normally " +
						   $"means a MeshQuery change altered which surfaces land in a section without the section's " +
						   $"totals following (see GatherSurfacesJob / SortSurfacesParallelJob).");
		}

		[BurstDiscard]
		public static void ValidateSubMeshIndices(NativeArray<int> indices, int indexStart, int indexCount, int vertexCount, int subMeshIndex)
		{
			var lastIndex = indexStart + indexCount;
			if (indexStart < 0 || lastIndex > indices.Length)
			{
				Debug.LogError($"Chisel mesh generation: sub-mesh {subMeshIndex} index range [{indexStart}, {lastIndex}) " +
							   $"is outside the {indices.Length}-entry index buffer. Sub-mesh index counts are out of sync " +
							   $"with the copied geometry (see ChiselOutputRenderable.CopyMesh).");
				return;
			}
			for (int i = indexStart; i < lastIndex; i++)
			{
				var index = indices[i];
				if (index >= 0 && index < vertexCount)
					continue;
				Debug.LogError($"Chisel mesh generation: sub-mesh {subMeshIndex} references vertex {index} at index-buffer " +
							   $"position {i}, but the mesh only has {vertexCount} vertices. The sub-mesh vertex/index counts " +
							   $"are out of sync with the copied geometry (see ChiselOutputRenderable.CopyMesh).");
				return;
			}
		}
	}

	internal struct ChiselOutputRenderable : IChiselOutputMeshCopier
	{
#if false
        [GenerateTestsForBurstCompatibility]
		public readonly int GetOutputMeshCount([ReadOnly] NativeArray<MeshQuery> meshQueries,
									           [ReadOnly] NativeArray<int>       parameterCounts)
		{
			var meshAllocations = 0;
			for (int m = 0; m < meshQueries.Length; m++)
			{
				var meshQuery = meshQueries[m];
				// Query must use Material
				if (meshQuery.LayerParameterIndex != SurfaceParameterIndex.Parameter1)
					continue;

				// Each Material is stored as a submesh in the same mesh
				meshAllocations += 1;
			}
			return meshAllocations;
		}
#endif

        [GenerateTestsForBurstCompatibility]
		public void CopyMesh([NoAlias, ReadOnly] NativeArray<VertexAttributeDescriptor> descriptors,
					         [NoAlias, ReadOnly] SubMeshSection subMeshSection,
					         [NoAlias, ReadOnly] SubMeshSource subMeshSource,
                             [NoAlias] ref Mesh.MeshData meshData)
        {
            var ranges = new NativeList<RenderCopyRange>(Allocator.Temp);
            var chunks = new NativeList<int2>(Allocator.Temp);
            Prepare(descriptors, subMeshSection, subMeshSource, ref meshData, 0, ranges, chunks);
            chunks.Dispose();
            for (int r = 0; r < ranges.Length; r++)
                CopyRange(ranges[r]);
            ranges.Dispose();
        }

        // The mesh made ready to be copied into: its buffers, its sub-meshes and its lightmap layout, and a range per
        // surface (CopyRange, each independent of the others), in chunks of about kChunkVertices (start, end of ranges):
        // a thread a chunk, as more threads only fight over the mesh's fresh memory
        public const int kChunkVertices = 32768;
        public static unsafe void Prepare([NoAlias, ReadOnly] NativeArray<VertexAttributeDescriptor> descriptors,
					               [NoAlias, ReadOnly] SubMeshSection subMeshSection,
					               [NoAlias, ReadOnly] SubMeshSource subMeshSource,
                                   [NoAlias] ref Mesh.MeshData meshData, int meshIndex, NativeList<RenderCopyRange> ranges, NativeList<int2> chunks)
        {
            var startIndex          = subMeshSection.startIndex;
            var endIndex            = subMeshSection.endIndex;
            var numberOfSubMeshes   = endIndex - startIndex;
            var totalVertexCount    = subMeshSection.totalVertexCount;
            var totalIndexCount     = subMeshSection.totalIndexCount;
            if (numberOfSubMeshes == 0 ||
                totalVertexCount == 0 ||
                totalIndexCount == 0)
            {
                meshData.SetVertexBufferParams(0, descriptors);
                meshData.SetIndexBufferParams(0, IndexFormat.UInt32);
                meshData.subMeshCount = 0;
                return;
            }

            meshData.SetVertexBufferParams(totalVertexCount, descriptors);
            meshData.SetIndexBufferParams(totalIndexCount, IndexFormat.UInt32);
            meshData.subMeshCount = numberOfSubMeshes;

            var indices     = meshData.GetIndexData<int>();
            var vertices    = meshData.GetVertexData<RenderVertex>(stream: 0);
            var indexBufferLength  = indices.Length;
            var vertexBufferLength = vertices.Length;
            // the ranges write through these, so no copy asks the mesh for its buffers again
            var indexPointer  = (int*)NativeArrayUnsafeUtility.GetUnsafePtr(indices);
            var vertexPointer = (RenderVertex*)NativeArrayUnsafeUtility.GetUnsafePtr(vertices);
            // The lightmap layout of this mesh: a chart per surface, in the order they are copied below (LightmapUVLayout)
            var chartCount = 0;
            for (int d = startIndex; d < endIndex; d++)
                chartCount += subMeshSource.subMeshDescriptions[d].surfacesCount;
            var charts     = new NativeArray<LightmapChart>(chartCount, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
            var placements = new NativeArray<LightmapChartPlacement>(chartCount, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
            for (int d = startIndex, chart = 0; d < endIndex; d++)
            {
                var description  = subMeshSource.subMeshDescriptions[d];
                var surfaceArray = subMeshSource.subMeshSurfaces[description.meshQueryIndex];
                for (int s = description.surfacesOffset, last = s + description.surfacesCount; s < last; s++, chart++)
                {
                    var chartSurface = surfaceArray[s];
                    ref var surface  = ref chartSurface.brushRenderBuffer.Value.surfaces[chartSurface.surfaceIndex];
                    var drawn = surface.indices.Length > 0 && surface.renderVertices.Length > 0;
                    charts[chart] = new LightmapChart
                    {
                        rect = surface.lightmapChart,
                        mode = drawn ? LightmapUVLayout.ModeOf(surface.outputFlags) : LightmapChartMode.None,
                        key  = ((ulong)surface.shapeHashValue << 32) | (uint)chartSurface.surfaceIndex
                    };
                }
            }
            var lightmapSide  = LightmapUVLayout.Layout(charts, subMeshSource.lightmapUVSettings, placements);
            var lightmapScale = (lightmapSide > 0) ? 1.0f / lightmapSide : 0.0f;
            var chartBase     = 0;

            int currentBaseVertex   = 0;
            int currentBaseIndex    = 0;
            var chunkStart          = ranges.Length;
            var chunkVertices       = 0;

            for (int subMeshIndex = 0, d = startIndex; d < endIndex; d++, subMeshIndex++)
            {
                var subMeshCount        = subMeshSource.subMeshDescriptions[d];
                var vertexCount		    = subMeshCount.vertexCount;
                var indexCount		    = subMeshCount.indexCount;
                var surfacesOffset      = subMeshCount.surfacesOffset;
                var surfacesCount       = subMeshCount.surfacesCount;
                var meshQueryIndex      = subMeshCount.meshQueryIndex;
                var subMeshSurfaceArray = subMeshSource.subMeshSurfaces[meshQueryIndex];

                var aabb = new MinMaxAABB()
                {
                    Min = new float3(float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity),
                    Max = new float3(float.NegativeInfinity, float.NegativeInfinity, float.NegativeInfinity)
                };

                // where each surface's vertices & indices go in the sub-meshes, one sub-mesh per material
                int writtenIndexCount = 0;
                for (int surfaceIndex       = surfacesOffset,
                         indexOffset        = currentBaseIndex,
                         indexVertexOffset  = 0,
                         lastSurfaceIndex   = surfacesCount + surfacesOffset;

                         surfaceIndex < lastSurfaceIndex;

                         ++surfaceIndex)
                {
                    var subMeshSurface      = subMeshSurfaceArray[surfaceIndex];
                    ref var sourceBuffer    = ref subMeshSurface.brushRenderBuffer.Value.surfaces[subMeshSurface.surfaceIndex];

                    var sourceIndexCount    = sourceBuffer.indices.Length;
                    var sourceVertexCount   = sourceBuffer.renderVertices.Length;

                    if (sourceIndexCount == 0 ||
                        sourceVertexCount == 0)
                        continue;

                    // Ask BEFORE writing - the buffer would be overrun
                    if (!ChiselOutputMeshValidation.FitsInBuffer(indexOffset, sourceIndexCount, indexBufferLength) ||
                        !ChiselOutputMeshValidation.FitsInBuffer(currentBaseVertex + indexVertexOffset, sourceVertexCount, vertexBufferLength))
                    {
                        ChiselOutputMeshValidation.ReportCopyOverflow(subMeshIndex, indexOffset, sourceIndexCount, indexBufferLength);
                        break;
                    }

                    // Where its lightmap coordinates go in this mesh's layout
                    var chartIndex = chartBase + (surfaceIndex - surfacesOffset);
                    ranges.Add(new RenderCopyRange
                    {
                        meshIndex     = meshIndex,
                        indices       = indexPointer,
                        vertices      = vertexPointer,
                        buffer        = subMeshSurface.brushRenderBuffer,
                        surfaceIndex  = subMeshSurface.surfaceIndex,
                        baseVertex    = currentBaseVertex + indexVertexOffset,
                        baseIndex     = indexOffset,
                        placement     = placements[chartIndex],
                        chartRect     = charts[chartIndex].rect,
                        lightmapScale = lightmapScale
                    });
                    chunkVertices += sourceVertexCount;
                    if (chunkVertices >= kChunkVertices)
                    {
                        chunks.Add(new int2(chunkStart, ranges.Length));
                        chunkStart = ranges.Length;
                        chunkVertices = 0;
                    }
                    indexOffset += sourceIndexCount;
                    writtenIndexCount += sourceIndexCount;

					aabb.Min = math.min(aabb.Min, sourceBuffer.aabb.Min);
					aabb.Max = math.max(aabb.Max, sourceBuffer.aabb.Max);

                    indexVertexOffset += sourceVertexCount;
                }

                for (int z = currentBaseIndex + writtenIndexCount; z < currentBaseIndex + indexCount && z < indexBufferLength; z++)
                    indices[z] = 0;

                meshData.SetSubMesh(subMeshIndex, new SubMeshDescriptor
                {
                    baseVertex  = 0,//currentBaseVertex,
                    indexStart  = currentBaseIndex,
                    indexCount  = writtenIndexCount,
					//firstVertex = 0,
					//vertexCount = vertexCount,
					bounds      = aabb.ToBounds(),
					topology    = UnityEngine.MeshTopology.Triangles,
                }, MeshUpdateFlags.DontRecalculateBounds | MeshUpdateFlags.DontValidateIndices);

                currentBaseVertex += vertexCount;
                currentBaseIndex += indexCount;
                chartBase += surfacesCount;
            }
            if (ranges.Length > chunkStart)
                chunks.Add(new int2(chunkStart, ranges.Length));
            charts.Dispose();
            placements.Dispose();
        }

        // One surface's indices and vertices copied to where Prepare put them, its lightmap coordinates placed
        public static unsafe void CopyRange(RenderCopyRange range)
        {
            var vertices = range.vertices;
            var indices  = range.indices;
            ref var source = ref range.buffer.Value.surfaces[range.surfaceIndex];
            ref var sourceIndices  = ref source.indices;
            ref var sourceVertices = ref source.renderVertices;
            for (int i = 0; i < sourceIndices.Length; i++)
                indices[range.baseIndex + i] = sourceIndices[i] + range.baseVertex;
            for (int v = 0; v < sourceVertices.Length; v++)
            {
                var vertex = sourceVertices[v];
                vertex.uv1 = range.placement.Place(vertex.uv1, range.chartRect) * range.lightmapScale;
                vertices[range.baseVertex + v] = vertex;
            }
        }
	}

    // Where one surface of a render mesh goes, and how its lightmap coordinates are placed (ChiselOutputRenderable.Prepare)
    internal unsafe struct RenderCopyRange
    {
        public int    meshIndex;
        [NativeDisableUnsafePtrRestriction] public int*          indices;
        [NativeDisableUnsafePtrRestriction] public RenderVertex* vertices;
        public BlobAssetReference<ChiselBrushRenderBuffer> buffer;
        public int    surfaceIndex;
        public int    baseVertex, baseIndex;
        public LightmapChartPlacement placement;
        public float4 chartRect;
        public float  lightmapScale;
    }


	internal struct ChiselOutputCollidable : IChiselOutputMeshCopier
	{
#if false
        [GenerateTestsForBurstCompatibility]
		public readonly int GetOutputMeshCount([ReadOnly] NativeArray<MeshQuery> meshQueries,
									           [ReadOnly] NativeArray<int>       parameterCounts)
		{
			var meshAllocations = 0;
			for (int m = 0; m < meshQueries.Length; m++)
			{
				var meshQuery = meshQueries[m];
				// Query must use PhysicMaterial
				if (meshQuery.LayerParameterIndex != SurfaceParameterIndex.Parameter2)
					continue;
				Debug.Assert((meshQuery.LayerQuery & SurfaceDestinationFlags.Collidable) != 0);
				
				// Each PhysicMaterial is stored in its own separate mesh
				meshAllocations += parameterCounts[SurfaceDestinationParameters.kColliderLayer];
			}
			return meshAllocations;
		}
#endif

        [GenerateTestsForBurstCompatibility]
		public void CopyMesh([NoAlias, ReadOnly] NativeArray<VertexAttributeDescriptor> descriptors,
					         [NoAlias, ReadOnly] SubMeshSection subMeshSection, 
					         [NoAlias, ReadOnly] SubMeshSource subMeshSource,
                             [NoAlias] ref Mesh.MeshData meshData)
        {
            var totalVertexCount    = subMeshSection.totalVertexCount;
            var totalIndexCount     = subMeshSection.totalIndexCount;

            if (totalVertexCount == 0 ||
                totalIndexCount == 0)
            {
                meshData.SetVertexBufferParams(0, descriptors);
                meshData.SetIndexBufferParams(0, IndexFormat.UInt32);
                meshData.subMeshCount = 0;
                return;
            }
            var startIndex          = subMeshSection.startIndex;

            var subMeshCount        = subMeshSource.subMeshDescriptions[startIndex];
            var meshQueryIndex		= subMeshCount.meshQueryIndex;

            var surfacesOffset      = subMeshCount.surfacesOffset;
            var surfacesCount       = subMeshCount.surfacesCount;
            var vertexCount		    = subMeshCount.vertexCount;
            var indexCount		    = subMeshCount.indexCount;
            var subMeshSurfaceArray = subMeshSource.subMeshSurfaces[meshQueryIndex];

            // A surface whose drawn triangles are cut (hotspot sections) gives the collider its uncut ones
            using var colliderPositions = new NativeList<float3>(totalVertexCount, Allocator.Temp);
            using var positionIndex     = new NativeParallelHashMap<float3, int>(totalVertexCount, Allocator.Temp);
            using var vertexRemap       = new NativeList<int>(totalVertexCount, Allocator.Temp);
            var colliderIndexCount = 0;
            for (int surfaceIndex = surfacesOffset, lastSurfaceIndex = surfacesCount + surfacesOffset;
                    surfaceIndex < lastSurfaceIndex;
                    ++surfaceIndex)
            {
                var subMeshSurface      = subMeshSurfaceArray[surfaceIndex];
                ref var sourceBuffer    = ref subMeshSurface.brushRenderBuffer.Value.surfaces[subMeshSurface.surfaceIndex];
                var physics             = sourceBuffer.physicsIndices.Length > 0;
                ref var sourceIndices   = ref (physics ? ref sourceBuffer.physicsIndices : ref sourceBuffer.indices);
                ref var sourceVertices  = ref (physics ? ref sourceBuffer.physicsVertices : ref sourceBuffer.colliderVertices);
                if (sourceIndices.Length == 0 ||
                    sourceVertices.Length == 0)
                    continue;
                colliderIndexCount += sourceIndices.Length;
                for (int v = 0; v < sourceVertices.Length; v++)
                {
                    var position = sourceVertices[v] + 0.0f;    // -0 becomes 0, so equal positions hash alike
                    if (!positionIndex.TryGetValue(position, out var unique))
                    {
                        unique = colliderPositions.Length;
                        colliderPositions.Add(position);
                        positionIndex.TryAdd(position, unique);
                    }
                    vertexRemap.Add(unique);
                }
            }

            meshData.SetVertexBufferParams(colliderPositions.Length, descriptors);
            meshData.SetIndexBufferParams(colliderIndexCount, IndexFormat.UInt32);

            var vertices            = meshData.GetVertexData<float3>(stream: 0);
            var indices             = meshData.GetIndexData<int>();
            var indexBufferLength  = indices.Length;
            var vertexBufferLength = vertexRemap.Length;
            for (int v = 0; v < colliderPositions.Length; v++)
                vertices[v] = colliderPositions[v];

            var aabb = new MinMaxAABB()
            {
                Min = new float3(float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity),
                Max = new float3(float.NegativeInfinity, float.NegativeInfinity, float.NegativeInfinity)
            };

            // copy all the indices to a mesh for the collider, each pointing at its position's vertex
            int indexOffset = 0, vertexOffset = 0;
            for (int surfaceIndex = surfacesOffset, lastSurfaceIndex = surfacesCount + surfacesOffset;
                    surfaceIndex < lastSurfaceIndex;
                    ++surfaceIndex)
            {
                var subMeshSurface      = subMeshSurfaceArray[surfaceIndex];
                ref var sourceBuffer    = ref subMeshSurface.brushRenderBuffer.Value.surfaces[subMeshSurface.surfaceIndex];
                var physics             = sourceBuffer.physicsIndices.Length > 0;
                ref var sourceIndices   = ref (physics ? ref sourceBuffer.physicsIndices : ref sourceBuffer.indices);
                ref var sourceVertices  = ref (physics ? ref sourceBuffer.physicsVertices : ref sourceBuffer.colliderVertices);

                var sourceIndexCount    = sourceIndices.Length;
                var sourceVertexCount   = sourceVertices.Length;

                if (sourceIndexCount == 0 ||
                    sourceVertexCount == 0)
                    continue;

                // Ask BEFORE writing - the Debug.Asserts below only fire in development builds, and
                // only after the buffer has already been overrun.
                if (!ChiselOutputMeshValidation.FitsInBuffer(indexOffset, sourceIndexCount, indexBufferLength) ||
                    !ChiselOutputMeshValidation.FitsInBuffer(vertexOffset, sourceVertexCount, vertexBufferLength))
                {
                    ChiselOutputMeshValidation.ReportCopyOverflow(0, indexOffset, sourceIndexCount, indexBufferLength);
                    break;
                }

                for (int i = 0; i < sourceIndexCount; i++)
                {
                    // An index outside its surface stays outside the mesh, so the validation below still reports it
                    var source = sourceIndices[i];
                    indices[i + indexOffset] = (uint)source < (uint)sourceVertexCount ? vertexRemap[source + vertexOffset]
                                                                                      : colliderPositions.Length;
                }
                indexOffset += sourceIndexCount;

				aabb.Min = math.min(aabb.Min, sourceBuffer.aabb.Min);
				aabb.Max = math.max(aabb.Max, sourceBuffer.aabb.Max);

                vertexOffset += sourceVertexCount;
            }
            // Same reasoning as above: never leave an uninitialized tail in the index buffer.
            for (int z = indexOffset; z < colliderIndexCount && z < indexBufferLength; z++)
                indices[z] = 0;

            Debug.Assert(indexOffset == colliderIndexCount);
            Debug.Assert(vertexOffset == vertexRemap.Length);

            meshData.subMeshCount = 1;
            // read here: an argument computed in the call is discarded with the call under Burst (BC1371)
            var colliderVertexCount = colliderPositions.Length;
            ChiselOutputMeshValidation.ValidateSubMeshIndices(indices, 0, colliderIndexCount, colliderVertexCount, 0);
            meshData.SetSubMesh(0, new SubMeshDescriptor
            {
                baseVertex  = 0,
                indexStart  = 0,
                indexCount  = colliderIndexCount,
				//firstVertex = 0,
				//vertexCount = vertexCount,
				bounds      = aabb.ToBounds(),
                topology    = UnityEngine.MeshTopology.Triangles,
            }, MeshUpdateFlags.DontRecalculateBounds | MeshUpdateFlags.DontValidateIndices);
        }
	}


	internal struct ChiselOutputDebugVisualizer : IChiselOutputMeshCopier
	{
#if false
        [GenerateTestsForBurstCompatibility]
		public readonly int GetOutputMeshCount([ReadOnly] NativeArray<MeshQuery> meshQueries,
									  [ReadOnly] NativeArray<int>		parameterCounts)
		{
			var meshAllocations = 0;
			for (int m = 0; m < meshQueries.Length; m++)
			{
				var meshQuery = meshQueries[m];
				// Query doesn't use Material or PhysicMaterial
				if (meshQuery.LayerParameterIndex == SurfaceParameterIndex.None)
					continue;
				meshAllocations++;
			}
			return meshAllocations;
		}
#endif

        [GenerateTestsForBurstCompatibility]
		public void CopyMesh([NoAlias, ReadOnly] NativeArray<VertexAttributeDescriptor> descriptors,
					         [NoAlias, ReadOnly] SubMeshSection subMeshSection, 
					         [NoAlias, ReadOnly] SubMeshSource subMeshSource,
                             [NoAlias] ref Mesh.MeshData meshData)
        {
            var startIndex          = subMeshSection.startIndex;
            var endIndex            = subMeshSection.endIndex;
            var numberOfSubMeshes   = endIndex - startIndex;
            var totalVertexCount    = subMeshSection.totalVertexCount;
            var totalIndexCount     = subMeshSection.totalIndexCount;            
            if (numberOfSubMeshes == 0 ||
                totalVertexCount == 0 ||
                totalIndexCount == 0)
            {
                meshData.SetVertexBufferParams(0, descriptors);
                meshData.SetIndexBufferParams(0, IndexFormat.UInt32);
                meshData.subMeshCount = 0;
                return;
            }

            meshData.SetVertexBufferParams(totalVertexCount, descriptors);
            meshData.SetIndexBufferParams(totalIndexCount, IndexFormat.UInt32);
            meshData.subMeshCount = numberOfSubMeshes;

            var vertices    = meshData.GetVertexData<RenderVertex>(stream: 0);
            var indices     = meshData.GetIndexData<int>();
            var indexBufferLength  = indices.Length;
            var vertexBufferLength = vertices.Length;

            int currentBaseVertex   = 0;
            int currentBaseIndex    = 0;

            for (int subMeshIndex = 0, d = startIndex; d < endIndex; d++, subMeshIndex++)
            {
                var subMeshCount        = subMeshSource.subMeshDescriptions[d];
                var vertexCount		    = subMeshCount.vertexCount;
                var indexCount		    = subMeshCount.indexCount;
                var surfacesOffset      = subMeshCount.surfacesOffset;
                var surfacesCount       = subMeshCount.surfacesCount;
                var meshQueryIndex      = subMeshCount.meshQueryIndex;
                var subMeshSurfaceArray = subMeshSource.subMeshSurfaces[meshQueryIndex];

                var aabb = new MinMaxAABB()
                {
                    Min = new float3(float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity),
                    Max = new float3(float.NegativeInfinity, float.NegativeInfinity, float.NegativeInfinity)
                };

                // copy all the vertices & indices to the sub-meshes, one sub-mesh per material
                int writtenIndexCount = 0;
                for (int surfaceIndex       = surfacesOffset, 
                         indexOffset        = currentBaseIndex, 
                         indexVertexOffset  = 0, 
                         lastSurfaceIndex   = surfacesCount + surfacesOffset;

                         surfaceIndex < lastSurfaceIndex;

                         ++surfaceIndex)
                {
                    var subMeshSurface      = subMeshSurfaceArray[surfaceIndex];
                    ref var sourceBuffer    = ref subMeshSurface.brushRenderBuffer.Value.surfaces[subMeshSurface.surfaceIndex];
                            
                    ref var sourceIndices   = ref sourceBuffer.indices;
                    ref var sourceVertices  = ref sourceBuffer.renderVertices;

                    var sourceIndexCount    = sourceIndices.Length;
                    var sourceVertexCount   = sourceVertices.Length;

                    if (sourceIndexCount == 0 ||
                        sourceVertexCount == 0)
                        continue;

                    // Ask BEFORE writing - the [BurstDiscard] validation below never runs inside the
                    // job, and by the time it would, the buffer has already been overrun.
                    if (!ChiselOutputMeshValidation.FitsInBuffer(indexOffset, sourceIndexCount, indexBufferLength) ||
                        !ChiselOutputMeshValidation.FitsInBuffer(currentBaseVertex + indexVertexOffset, sourceVertexCount, vertexBufferLength))
                    {
                        ChiselOutputMeshValidation.ReportCopyOverflow(subMeshIndex, indexOffset, sourceIndexCount, indexBufferLength);
                        break;
                    }

                    for (int i = 0; i < sourceIndexCount; i++)
                        indices[i + indexOffset] = (int)(sourceIndices[i] + indexVertexOffset) + currentBaseVertex;
                    indexOffset += sourceIndexCount;
                    writtenIndexCount += sourceIndexCount;

                    vertices.CopyFrom(currentBaseVertex + indexVertexOffset, ref sourceVertices, 0, sourceVertexCount);

					aabb.Min = math.min(aabb.Min, sourceBuffer.aabb.Min);
					aabb.Max = math.max(aabb.Max, sourceBuffer.aabb.Max);

                    indexVertexOffset += sourceVertexCount;
                }
                
                for (int z = currentBaseIndex + writtenIndexCount; z < currentBaseIndex + indexCount && z < indexBufferLength; z++)
                    indices[z] = 0;

                ChiselOutputMeshValidation.ValidateSubMeshIndices(indices, currentBaseIndex, indexCount, totalVertexCount, subMeshIndex);
                meshData.SetSubMesh(subMeshIndex, new SubMeshDescriptor
                {
                    baseVertex  = 0,//currentBaseVertex,
                    indexStart  = currentBaseIndex,
                    indexCount  = writtenIndexCount,
					//firstVertex = 0,
					//vertexCount = vertexCount,
					bounds      = aabb.ToBounds(),
                    topology    = UnityEngine.MeshTopology.Triangles,
                }, MeshUpdateFlags.DontRecalculateBounds | MeshUpdateFlags.DontValidateIndices);

                currentBaseVertex += vertexCount;
                currentBaseIndex += indexCount;
            }
        }
	}

    

    // TODO: this doesn't make sense, since we'd want to create a selection mesh, 
    //          for EACH renderable, debug-visualizer AND collider mesh that we create ..
	internal struct ChiselOutputSelectionMesh : IChiselOutputMeshCopier
	{
#if false
		public readonly int GetOutputMeshCount([ReadOnly] NativeArray<MeshQuery> meshQueries,
									           [ReadOnly] NativeArray<int>       parameterCounts)
		{
			var meshAllocations = 0;
			for (int m = 0; m < meshQueries.Length; m++)
			{
				var meshQuery = meshQueries[m];
				// Query must use Material
				if (meshQuery.LayerParameterIndex != SurfaceParameterIndex.Parameter1)
					continue;

				// Each Material is stored as a submesh in the same mesh
				meshAllocations += 1;
			}
			return meshAllocations;
		}
#endif

		public void CopyMesh([NoAlias, ReadOnly] NativeArray<VertexAttributeDescriptor> descriptors,
					         [NoAlias, ReadOnly] SubMeshSection subMeshSection, 
					         [NoAlias, ReadOnly] SubMeshSource subMeshSource,
                             [NoAlias] ref Mesh.MeshData meshData)
        {
            var startIndex          = subMeshSection.startIndex;
            var endIndex            = subMeshSection.endIndex;
            var numberOfSubMeshes   = endIndex - startIndex;
            var totalVertexCount    = subMeshSection.totalVertexCount;
            var totalIndexCount     = subMeshSection.totalIndexCount;            
            if (numberOfSubMeshes == 0 ||
                totalVertexCount == 0 ||
                totalIndexCount == 0)
            {
                meshData.SetVertexBufferParams(0, descriptors);
                meshData.SetIndexBufferParams(0, IndexFormat.UInt32);
                meshData.subMeshCount = 0;
                return;
            }

            meshData.SetVertexBufferParams(totalVertexCount, descriptors);
            meshData.SetIndexBufferParams(totalIndexCount, IndexFormat.UInt32);
            meshData.subMeshCount = numberOfSubMeshes;

            var vertices    = meshData.GetVertexData<SelectVertex>(stream: 0);
            var indices     = meshData.GetIndexData<int>();
            var indexBufferLength  = indices.Length;
            var vertexBufferLength = vertices.Length;

            int currentBaseVertex   = 0;
            int currentBaseIndex    = 0;

            for (int subMeshIndex = 0, d = startIndex; d < endIndex; d++, subMeshIndex++)
            {
                var subMeshCount        = subMeshSource.subMeshDescriptions[d];
                var vertexCount		    = subMeshCount.vertexCount;
                var indexCount		    = subMeshCount.indexCount;
                var surfacesOffset      = subMeshCount.surfacesOffset;
                var surfacesCount       = subMeshCount.surfacesCount;
                var meshQueryIndex      = subMeshCount.meshQueryIndex;
                var subMeshSurfaceArray = subMeshSource.subMeshSurfaces[meshQueryIndex];

                var aabb = new MinMaxAABB()
                {
                    Min = new float3(float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity),
                    Max = new float3(float.NegativeInfinity, float.NegativeInfinity, float.NegativeInfinity)
                };

                // copy all the vertices & indices to the sub-meshes, one sub-mesh per material
                int writtenIndexCount = 0;
                for (int surfaceIndex       = surfacesOffset, 
                         indexOffset        = currentBaseIndex, 
                         indexVertexOffset  = 0, 
                         lastSurfaceIndex   = surfacesCount + surfacesOffset;

                         surfaceIndex < lastSurfaceIndex;

                         ++surfaceIndex)
                {
                    var subMeshSurface      = subMeshSurfaceArray[surfaceIndex];
                    ref var sourceBuffer    = ref subMeshSurface.brushRenderBuffer.Value.surfaces[subMeshSurface.surfaceIndex];
                            
                    ref var sourceIndices   = ref sourceBuffer.indices;
                    ref var sourceVertices  = ref sourceBuffer.selectVertices;

                    var sourceIndexCount    = sourceIndices.Length;
                    var sourceVertexCount   = sourceVertices.Length;

                    if (sourceIndexCount == 0 ||
                        sourceVertexCount == 0)
                        continue;

                    // Ask BEFORE writing - the [BurstDiscard] validation below never runs inside the
                    // job, and by the time it would, the buffer has already been overrun.
                    if (!ChiselOutputMeshValidation.FitsInBuffer(indexOffset, sourceIndexCount, indexBufferLength) ||
                        !ChiselOutputMeshValidation.FitsInBuffer(currentBaseVertex + indexVertexOffset, sourceVertexCount, vertexBufferLength))
                    {
                        ChiselOutputMeshValidation.ReportCopyOverflow(subMeshIndex, indexOffset, sourceIndexCount, indexBufferLength);
                        break;
                    }

                    for (int i = 0; i < sourceIndexCount; i++)
                        indices[i + indexOffset] = (int)(sourceIndices[i] + indexVertexOffset) + currentBaseVertex;
                    indexOffset += sourceIndexCount;
                    writtenIndexCount += sourceIndexCount;

                    vertices.CopyFrom(currentBaseVertex + indexVertexOffset, ref sourceVertices, 0, sourceVertexCount);

					aabb.Min = math.min(aabb.Min, sourceBuffer.aabb.Min);
					aabb.Max = math.max(aabb.Max, sourceBuffer.aabb.Max);

                    indexVertexOffset += sourceVertexCount;
                }
                
                for (int z = currentBaseIndex + writtenIndexCount; z < currentBaseIndex + indexCount && z < indexBufferLength; z++)
                    indices[z] = 0;

                ChiselOutputMeshValidation.ValidateSubMeshIndices(indices, currentBaseIndex, indexCount, totalVertexCount, subMeshIndex);
                meshData.SetSubMesh(subMeshIndex, new SubMeshDescriptor
                {
                    baseVertex  = 0,//currentBaseVertex,
                    indexStart  = currentBaseIndex,
                    indexCount  = writtenIndexCount,
					//firstVertex = 0,
					//vertexCount = vertexCount,
					bounds      = aabb.ToBounds(),
                    topology    = UnityEngine.MeshTopology.Triangles,
                }, MeshUpdateFlags.DontRecalculateBounds | MeshUpdateFlags.DontValidateIndices);

                currentBaseVertex += vertexCount;
                currentBaseIndex += indexCount;
            }
        }
	}
}