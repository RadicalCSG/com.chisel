using System;

using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

using Debug = UnityEngine.Debug;

namespace Chisel.Core
{
    internal struct ChiselSurfaceRenderBuffer
    {
        public int                          surfaceIndex;
		public SurfaceDestinationFlags      destinationFlags;
		public SurfaceDestinationParameters destinationParameters;

        public int    vertexCount;
        public int    indexCount;

        public uint   geometryHashValue;
        public uint   surfaceHashValue;

        public MinMaxAABB aabb;  

        public BlobArray<Int32>		   indices;
        public BlobArray<RenderVertex> renderVertices;
		public BlobArray<SelectVertex> selectVertices;
		public BlobArray<float3>	   colliderVertices;
		
		public readonly void FixUpOrdering(NativeList<Int32>		indices,
										   NativeList<float3>		colliderVertices,
										   NativeList<SelectVertex>	selectVertices,
										   NativeList<RenderVertex>	renderVertices)
		{
			UnityEngine.Debug.Assert(colliderVertices.Length == selectVertices.Length);
			UnityEngine.Debug.Assert(renderVertices.Length == selectVertices.Length);
			
			var indexRemap = new NativeList<int>(colliderVertices.Length, Allocator.TempJob);
			indexRemap.Resize(colliderVertices.Length, NativeArrayOptions.ClearMemory);
			for (int i = 0; i < colliderVertices.Length; i++)
			{
				indexRemap[i] = i;
			}

			for (int i = 0; i < colliderVertices.Length - 1; i++)
			{
				uint i_hash = math.hash(colliderVertices[i]);
				for (int j = i + 1; j < colliderVertices.Length; j++)
				{
					uint j_hash = math.hash(colliderVertices[j]);
					if (i_hash >= j_hash)
						continue;

					var index_i = indexRemap[i];
					var index_j = indexRemap[j];

					(colliderVertices[index_i], colliderVertices[index_j]) = (colliderVertices[index_j], colliderVertices[index_i]);
					(selectVertices[index_i], selectVertices[index_j]) = (selectVertices[index_j], selectVertices[index_i]);
					(renderVertices[index_i], renderVertices[index_j]) = (renderVertices[index_j], renderVertices[index_i]);
					(indexRemap[i], indexRemap[j]) = (indexRemap[j], indexRemap[i]);
					(i_hash, _) = (j_hash, i_hash);

					Debug.Assert(indexRemap[i] >= 0 && indexRemap[i] < colliderVertices.Length);
					Debug.Assert(indexRemap[j] >= 0 && indexRemap[j] < colliderVertices.Length);
				}
			}
			
			for (int i = 0; i < indices.Length; i+=3)
			{
				var a = indexRemap[indices[i + 0]];
				var b = indexRemap[indices[i + 1]];
				var c = indexRemap[indices[i + 2]];
				if (a < b && a < c)
				{
					indices[i + 0] = a;
					indices[i + 1] = b;
					indices[i + 2] = c;
				} else
				if (b < a && b < c)
				{
					indices[i + 0] = b;
					indices[i + 1] = c;
					indices[i + 2] = a;
				} else
				{
					indices[i + 0] = c;
					indices[i + 1] = a;
					indices[i + 2] = b;
				}
			}

			indexRemap.Dispose();


			for (int i = 0; i < indices.Length - 3; i += 3)
			{
				uint i_hash = math.hash(new int3(indices[i+0], indices[i + 1], indices[i + 2]));
				for (int j = i + 3; j < indices.Length; j += 3)
				{
					uint j_hash = math.hash(new int3(indices[j + 0], indices[j + 1], indices[j + 2]));
					if (i_hash >= j_hash)
						continue;

					(indices[i + 0], indices[j + 0]) = (indices[j + 0], indices[i + 0]);
					(indices[i + 1], indices[j + 1]) = (indices[j + 1], indices[i + 1]);
					(indices[i + 2], indices[j + 2]) = (indices[j + 2], indices[i + 2]);
					(i_hash, _) = (j_hash, i_hash);
				}
			}
		}

		[GenerateTestsForBurstCompatibility]
        public void Construct(BlobBuilder					builder,
							  NativeList<Int32>				indices,
							  NativeList<float3>			colliderVertices,
							  NativeList<SelectVertex>		selectVertices,
							  NativeList<RenderVertex>		renderVertices,
							  int							surfaceIndex,
                              SurfaceDestinationFlags		destinationFlags,
							  SurfaceDestinationParameters	destinationParameters)
		{
			FixUpOrdering(indices, colliderVertices, selectVertices, renderVertices);


			var vertexHashValue   = colliderVertices.Hash();
			var indicesHashValue  = indices.Hash();
			var geometryHashValue = math.hash(new uint2(vertexHashValue, indicesHashValue));

			this.surfaceIndex = surfaceIndex;

			this.destinationFlags = destinationFlags;
			this.destinationParameters = destinationParameters;

			this.vertexCount = colliderVertices.Length;
			this.indexCount = indices.Length;

			// TODO: properly compute hash again, AND USE IT
			this.surfaceHashValue = 0;// math.hash(new uint3(normalHash, tangentHash, uv0Hash));
			this.geometryHashValue = geometryHashValue;

			this.aabb = colliderVertices.GetMinMax();

			var outputIndices			= builder.Construct(ref this.indices, indices);
			var outputColliderVertices	= builder.Construct(ref this.colliderVertices, colliderVertices);
			var outputRenderVertices	= builder.Construct(ref this.renderVertices, renderVertices);
			var outputSelectVertices	= builder.Construct(ref this.selectVertices, selectVertices);

			UnityEngine.Debug.Assert(outputColliderVertices.Length == this.vertexCount);
			UnityEngine.Debug.Assert(outputRenderVertices.Length == this.vertexCount);
			UnityEngine.Debug.Assert(outputSelectVertices.Length == this.vertexCount);
			Debug.Assert(outputIndices.Length == this.indexCount);
		}
    };

	internal struct ChiselQuerySurface
    {
        public int	surfaceIndex;
        public int	surfaceParameter;

        public int	vertexCount;
        public int	indexCount;

        public uint	geometryHashValue;
        public uint	surfaceHashValue;
    }

    internal struct ChiselQuerySurfaces
    {
        public CompactNodeID                    brushNodeID;
		public BlobArray<ChiselQuerySurface>    surfaces;
    }

    internal struct ChiselBrushRenderBuffer
    {
        public BlobArray<ChiselSurfaceRenderBuffer> surfaces;
        public BlobArray<ChiselQuerySurfaces>       querySurfaces;
        public int surfaceOffset;
        public int surfaceCount;
    };

}
