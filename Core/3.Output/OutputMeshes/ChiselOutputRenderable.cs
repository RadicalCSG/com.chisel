using Unity.Jobs;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using Unity.Mathematics;
using Unity.Burst;

namespace Chisel.Core
{
    public struct ChiselMeshUpdates
	{
        [ReadOnly] public VertexBufferContents          vertexBufferContents;
		[ReadOnly] public NativeList<ChiselMeshUpdate>  meshUpdatesRenderables;
		[ReadOnly] public NativeList<ChiselMeshUpdate>  meshUpdatesColliders;
		[ReadOnly] public NativeList<ChiselMeshUpdate>  meshUpdatesDebugVisualizations;
        
		public Mesh.MeshDataArray meshDataArray;
    }

	internal struct ChiselOutputRenderable : IChiselOutputMeshCopier
	{
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
				Debug.Assert((meshQuery.LayerQuery & SurfaceDestinationFlags.Renderable) != 0);

				// Each Material is stored as a submesh in the same mesh
				meshAllocations += 1;
			}
			return meshAllocations;
		}

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

                    for (int i = 0; i < sourceIndexCount; i++)
                        indices[i + indexOffset] = (int)(sourceIndices[i] + indexVertexOffset);
                    indexOffset += sourceIndexCount;

                    vertices.CopyFrom(currentBaseVertex + indexVertexOffset, ref sourceVertices, 0, sourceVertexCount);

					aabb.Min = math.min(aabb.Min, sourceBuffer.aabb.Min);
					aabb.Max = math.max(aabb.Max, sourceBuffer.aabb.Max);

                    indexVertexOffset += sourceVertexCount;
                }
                
                meshData.SetSubMesh(subMeshIndex, new SubMeshDescriptor
                {
                    baseVertex  = currentBaseVertex,
                    firstVertex = 0,
                    vertexCount = vertexCount,
                    indexStart  = currentBaseIndex,
                    indexCount  = indexCount,
                    bounds      = aabb.ToBounds(),
                    topology    = UnityEngine.MeshTopology.Triangles,
                }, MeshUpdateFlags.DontRecalculateBounds | MeshUpdateFlags.DontValidateIndices);

                currentBaseVertex += vertexCount;
                currentBaseIndex += indexCount;
            }
        }
	}


	internal struct ChiselOutputCollidable : IChiselOutputMeshCopier
	{
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

            meshData.SetVertexBufferParams(totalVertexCount, descriptors);
            meshData.SetIndexBufferParams(totalIndexCount, IndexFormat.UInt32);

            var vertices            = meshData.GetVertexData<float3>(stream: 0);
            var indices             = meshData.GetIndexData<int>();

            var subMeshCount        = subMeshSource.subMeshDescriptions[startIndex];
            var meshQueryIndex		= subMeshCount.meshQueryIndex;

            var surfacesOffset      = subMeshCount.surfacesOffset;
            var surfacesCount       = subMeshCount.surfacesCount;
            var vertexCount		    = subMeshCount.vertexCount;
            var indexCount		    = subMeshCount.indexCount;
            var subMeshSurfaceArray = subMeshSource.subMeshSurfaces[meshQueryIndex];

            var aabb = new MinMaxAABB()
            {
                Min = new float3(float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity),
                Max = new float3(float.NegativeInfinity, float.NegativeInfinity, float.NegativeInfinity)
            };

            // copy all the vertices & indices to a mesh for the collider
            int indexOffset = 0, vertexOffset = 0;
            for (int surfaceIndex = surfacesOffset, brushIDIndexOffset = 0, lastSurfaceIndex = surfacesCount + surfacesOffset;
                    surfaceIndex < lastSurfaceIndex;
                    ++surfaceIndex)
            {
                var subMeshSurface      = subMeshSurfaceArray[surfaceIndex];
                ref var sourceBuffer    = ref subMeshSurface.brushRenderBuffer.Value.surfaces[subMeshSurface.surfaceIndex];
                ref var sourceIndices   = ref sourceBuffer.indices;
                ref var sourceVertices  = ref sourceBuffer.colliderVertices;

                var sourceIndexCount    = sourceIndices.Length;
                var sourceVertexCount   = sourceVertices.Length;

                if (sourceIndexCount == 0 ||
                    sourceVertexCount == 0)
                    continue;

                var sourceBrushCount    = sourceIndexCount / 3;
                brushIDIndexOffset += sourceBrushCount;

                for (int i = 0; i < sourceIndexCount; i++)
                    indices[i + indexOffset] = (int)(sourceIndices[i] + vertexOffset);
                indexOffset += sourceIndexCount;

                vertices.CopyFrom(vertexOffset, ref sourceVertices, 0, sourceVertexCount);

				aabb.Min = math.min(aabb.Min, sourceBuffer.aabb.Min);
				aabb.Max = math.max(aabb.Max, sourceBuffer.aabb.Max);

                vertexOffset += sourceVertexCount;
            }
            Debug.Assert(indexOffset == totalIndexCount);
            Debug.Assert(vertexOffset == totalVertexCount);

            meshData.subMeshCount = 1;
            meshData.SetSubMesh(0, new SubMeshDescriptor
            {
                baseVertex  = 0,
                firstVertex = 0,
                vertexCount = vertexCount,
                indexStart  = 0,
                indexCount  = indexCount,
                bounds      = aabb.ToBounds(),
                topology    = UnityEngine.MeshTopology.Triangles,
            }, MeshUpdateFlags.DontRecalculateBounds | MeshUpdateFlags.DontValidateIndices);
        }
	}


	internal struct ChiselOutputDebugVisualizer : IChiselOutputMeshCopier
	{
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

                    for (int i = 0; i < sourceIndexCount; i++)
                        indices[i + indexOffset] = (int)(sourceIndices[i] + indexVertexOffset);
                    indexOffset += sourceIndexCount;

                    vertices.CopyFrom(currentBaseVertex + indexVertexOffset, ref sourceVertices, 0, sourceVertexCount);

					aabb.Min = math.min(aabb.Min, sourceBuffer.aabb.Min);
					aabb.Max = math.max(aabb.Max, sourceBuffer.aabb.Max);

                    indexVertexOffset += sourceVertexCount;
                }
                
                meshData.SetSubMesh(subMeshIndex, new SubMeshDescriptor
                {
                    baseVertex  = currentBaseVertex,
                    firstVertex = 0,
                    vertexCount = vertexCount,
                    indexStart  = currentBaseIndex,
                    indexCount  = indexCount,
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
				Debug.Assert((meshQuery.LayerQuery & SurfaceDestinationFlags.Renderable) != 0);

				// Each Material is stored as a submesh in the same mesh
				meshAllocations += 1;
			}
			return meshAllocations;
		}

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

                    for (int i = 0; i < sourceIndexCount; i++)
                        indices[i + indexOffset] = (int)(sourceIndices[i] + indexVertexOffset);
                    indexOffset += sourceIndexCount;

                    vertices.CopyFrom(currentBaseVertex + indexVertexOffset, ref sourceVertices, 0, sourceVertexCount);

					aabb.Min = math.min(aabb.Min, sourceBuffer.aabb.Min);
					aabb.Max = math.max(aabb.Max, sourceBuffer.aabb.Max);

                    indexVertexOffset += sourceVertexCount;
                }
                
                meshData.SetSubMesh(subMeshIndex, new SubMeshDescriptor
                {
                    baseVertex  = currentBaseVertex,
                    firstVertex = 0,
                    vertexCount = vertexCount,
                    indexStart  = currentBaseIndex,
                    indexCount  = indexCount,
                    bounds      = aabb.ToBounds(),
                    topology    = UnityEngine.MeshTopology.Triangles,
                }, MeshUpdateFlags.DontRecalculateBounds | MeshUpdateFlags.DontValidateIndices);

                currentBaseVertex += vertexCount;
                currentBaseIndex += indexCount;
            }
        }
	}
}