using System;

using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

using Debug = UnityEngine.Debug;

namespace Chisel.Core
{
    struct ChiselSurfaceRenderBuffer
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
        public BlobArray<float3>	   colliderVertices;

        public void Construct(BlobBuilder builder,
							  NativeList<Int32> indices,
							  NativeList<RenderVertex> renderVertices,
							  NativeList<float3> colliderVertices, 
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
			var outputVertices = builder.Construct(ref this.colliderVertices, colliderVertices);
			builder.Construct(ref this.renderVertices, renderVertices);

			UnityEngine.Debug.Assert(outputVertices.Length == this.vertexCount);
			Debug.Assert(outputIndices.Length == this.indexCount);
		}
    };

    struct ChiselQuerySurface
    {
        public int      surfaceIndex;
        public int      surfaceParameter;

        public int      vertexCount;
        public int      indexCount;

        public uint     geometryHash;
        public uint     surfaceHash;
    }

    struct ChiselQuerySurfaces
    {
        public CompactNodeID                    brushNodeID;
        public BlobArray<ChiselQuerySurface>    surfaces;
    }

    struct ChiselBrushRenderBuffer
    {
        public BlobArray<ChiselSurfaceRenderBuffer> surfaces;
        public BlobArray<ChiselQuerySurfaces>       querySurfaces;
        public int surfaceOffset;
        public int surfaceCount;
    };

}
