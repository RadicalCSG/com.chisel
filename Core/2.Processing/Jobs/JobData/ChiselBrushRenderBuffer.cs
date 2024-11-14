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

        public uint   geometryHash;
        public uint   surfaceHash;

        public MinMaxAABB aabb;  

        public BlobArray<Int32>		   indices;
        public BlobArray<RenderVertex> renderVertices;
		public BlobArray<SelectVertex> selectVertices;
		public BlobArray<float3>	   colliderVertices;

        public void Construct(BlobBuilder builder,
							  NativeList<Int32> indices,
							  NativeList<float3> colliderVertices,
							  NativeList<SelectVertex> selectVertices,
							  NativeList<RenderVertex> renderVertices,
							  int surfaceIndex,
                              SurfaceDestinationFlags destinationFlags,
							  SurfaceDestinationParameters destinationParameters)
		{
			var vertexHash = colliderVertices.Hash();
			var indicesHash = indices.Hash();
			var geometryHash = math.hash(new uint2(vertexHash, indicesHash));

			this.surfaceIndex = surfaceIndex;

			this.destinationFlags = destinationFlags;
			this.destinationParameters = destinationParameters;

			this.vertexCount = colliderVertices.Length;
			this.indexCount = indices.Length;

			// TODO: properly compute hash again, AND USE IT
			this.surfaceHash = 0;// math.hash(new uint3(normalHash, tangentHash, uv0Hash));
			this.geometryHash = geometryHash;

			this.aabb = colliderVertices.GetMinMax();

			var outputIndices = builder.Construct(ref this.indices, indices);
			var outputColliderVertices = builder.Construct(ref this.colliderVertices, colliderVertices);
			var outputRenderVertices = builder.Construct(ref this.renderVertices, renderVertices);
			var outputSelectVertices = builder.Construct(ref this.selectVertices, selectVertices);

			UnityEngine.Debug.Assert(outputColliderVertices.Length == this.vertexCount);
			UnityEngine.Debug.Assert(outputRenderVertices.Length == this.vertexCount);
			UnityEngine.Debug.Assert(outputSelectVertices.Length == this.vertexCount);
			Debug.Assert(outputIndices.Length == this.indexCount);
		}
    };

	internal struct ChiselQuerySurface
    {
        public int      surfaceIndex;
        public int      surfaceParameter;

        public int      vertexCount;
        public int      indexCount;

        public uint     geometryHash;
        public uint     surfaceHash;
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
