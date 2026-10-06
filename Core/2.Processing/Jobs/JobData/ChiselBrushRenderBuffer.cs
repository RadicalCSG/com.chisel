using System;

using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Burst;

using Debug = UnityEngine.Debug;

namespace Chisel.Core
{
    internal struct ChiselSurfaceRenderBuffer
    {
        public int                          surfaceIndex;
        public int                          baseSurfaceIndex;
        // The decal drawn in this surface, which clicking it selects; 0 for the brush's own surfaces
        public ulong                        decalEntityID;
		public SurfaceDestinationFlags      destinationFlags;
		public SurfaceDestinationParameters destinationParameters;
		// How many lightmap texels the surface gets (SurfaceOutputFlags.NoLightmap, SingleLightmapTexel)
		public SurfaceOutputFlags           outputFlags;

        public int    vertexCount;
        public int    indexCount;

        public uint   geometryHashValue;
        public uint   surfaceHashValue;

        public MinMaxAABB aabb;  
        // The rectangle the surface's lightmap coordinates (RenderVertex.uv1) span in its plane: the lowest in xy, the highest in zw
        public float4 lightmapChart;

        public BlobArray<Int32>		   indices;
        public BlobArray<RenderVertex> renderVertices;
		public BlobArray<SelectVertex> selectVertices;
		public BlobArray<float3>	   colliderVertices;
		// The triangles of three distinct float positions on one line the weld left (OutputWeld.FindNeedles), by index:
		// what the weld across a model starts from (OutputModelWeld)
		public BlobArray<Int32>		   needles;
		
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

		[BurstDiscard]
		static void LogInconsistentVertexStreams(int surfaceIndex, int colliderCount, int selectCount, int renderCount, int indexCount)
		{
			Debug.LogError($"Chisel surface {surfaceIndex} produced mismatched vertex streams " +
						   $"(collider={colliderCount}, select={selectCount}, render={renderCount}, indices={indexCount}). " +
						   $"Dropping the surface to avoid generating out-of-range mesh indices.");
		}

		[GenerateTestsForBurstCompatibility]
        public void Construct(BlobBuilder					builder,
							  NativeList<Int32>				indices,
							  NativeList<float3>			colliderVertices,
							  NativeList<SelectVertex>		selectVertices,
							  NativeList<RenderVertex>		renderVertices,
							  int							surfaceIndex,
							  int							baseSurfaceIndex,
                              SurfaceDestinationFlags		destinationFlags,
							  SurfaceDestinationParameters	destinationParameters,
							  SurfaceOutputFlags				outputFlags)
		{
			if (colliderVertices.Length != renderVertices.Length ||
				colliderVertices.Length != selectVertices.Length)
			{
				LogInconsistentVertexStreams(surfaceIndex, colliderVertices.Length, selectVertices.Length, renderVertices.Length, indices.Length);

				this.surfaceIndex = surfaceIndex;
				this.baseSurfaceIndex = baseSurfaceIndex;
				this.decalEntityID = 0;
				this.destinationFlags = destinationFlags;
				this.destinationParameters = destinationParameters;
				this.outputFlags = outputFlags;
				this.lightmapChart = float4.zero;
				this.vertexCount = 0;
				this.indexCount = 0;
				this.surfaceHashValue = 0;
				this.geometryHashValue = 0;
				this.aabb = default;
				builder.Allocate(ref this.indices, 0);
				builder.Allocate(ref this.colliderVertices, 0);
				builder.Allocate(ref this.renderVertices, 0);
				builder.Allocate(ref this.selectVertices, 0);
				builder.Allocate(ref this.needles, 0);
				return;
			}

			OutputWeld.WeldSurface(indices, colliderVertices, selectVertices, renderVertices);

			FixUpOrdering(indices, colliderVertices, selectVertices, renderVertices);

			this.surfaceIndex = surfaceIndex;
			this.baseSurfaceIndex = baseSurfaceIndex;
			this.decalEntityID = 0;

			this.destinationFlags = destinationFlags;
			this.destinationParameters = destinationParameters;
			this.outputFlags = outputFlags;

			Store(builder, indices, colliderVertices, selectVertices, renderVertices);
		}

		[GenerateTestsForBurstCompatibility]
		public void Store(BlobBuilder					builder,
						  NativeList<Int32>				indices,
						  NativeList<float3>			colliderVertices,
						  NativeList<SelectVertex>		selectVertices,
						  NativeList<RenderVertex>		renderVertices)
		{
			var vertexHashValue   = colliderVertices.Hash();
			var indicesHashValue  = indices.Hash();
			var geometryHashValue = math.hash(new uint2(vertexHashValue, indicesHashValue));

			this.vertexCount = colliderVertices.Length;
			this.indexCount = indices.Length;

			// The lightmap texels it gets are part of how it is drawn
			uint surfaceHash = (uint)LightmapUVLayout.ModeOf(outputFlags);
			var chartMin = new float2(float.PositiveInfinity);
			var chartMax = new float2(float.NegativeInfinity);
			for (int i = 0; i < renderVertices.Length; i++)
			{
				var renderVertex = renderVertices[i];
				surfaceHash = math.hash(new uint2(surfaceHash, math.hash(renderVertex.normal)));
				surfaceHash = math.hash(new uint2(surfaceHash, math.hash(renderVertex.tangent)));
				surfaceHash = math.hash(new uint2(surfaceHash, math.hash(renderVertex.uv0)));
				chartMin = math.min(chartMin, renderVertex.uv1);
				chartMax = math.max(chartMax, renderVertex.uv1);
			}

			this.surfaceHashValue = surfaceHash;
			this.lightmapChart = (renderVertices.Length > 0) ? new float4(chartMin, chartMax) : float4.zero;
			this.geometryHashValue = geometryHashValue;

			this.aabb = colliderVertices.GetMinMax();

			var outputIndices			= builder.Construct(ref this.indices, indices);
			var outputColliderVertices	= builder.Construct(ref this.colliderVertices, colliderVertices);
			var outputRenderVertices	= builder.Construct(ref this.renderVertices, renderVertices);
			var outputSelectVertices	= builder.Construct(ref this.selectVertices, selectVertices);
			using (var needles = new NativeList<Int32>(Allocator.Temp))
			{
				OutputWeld.FindNeedles(indices, colliderVertices, needles);
				builder.Construct(ref this.needles, needles);
			}

			UnityEngine.Debug.Assert(outputColliderVertices.Length == this.vertexCount);
			UnityEngine.Debug.Assert(outputRenderVertices.Length == this.vertexCount);
			UnityEngine.Debug.Assert(outputSelectVertices.Length == this.vertexCount);
			Debug.Assert(outputIndices.Length == this.indexCount);
		}
    };

	internal struct ChiselQuerySurface
    {
        public int	surfaceIndex;
        public ulong	surfaceParameter;

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
