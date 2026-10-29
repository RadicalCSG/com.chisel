using System;

using Unity.Entities;
using Unity.Mathematics;

namespace Chisel.Core
{
    struct Edge : IEquatable<Edge>
    {
        public ushort index1;
        public ushort index2;

        public bool Equals(Edge other) => (index1 == other.index1 && index2 == other.index2);
        public override int GetHashCode() => (int)math.hash(new int2(index1, index2));
        public override string ToString() => $"({index1}, {index2})";
    }

    // Note: Stored in BlobAsset at runtime/editor-time
    struct BasePolygon
    {
        public IndexOrder  nodeIndexOrder;
        public SurfaceInfo surfaceInfo;
        public int         startEdgeIndex;
        public int         endEdgeIndex;
    }

    // Note: Stored in BlobAsset at runtime/editor-time
    struct BaseSurface // TODO: what is the difference with InternalChiselSurface?
	{
		public SurfaceDestinationFlags      destinationFlags;
		public SurfaceDestinationParameters destinationParameters;
		public SurfaceOutputFlags           outputFlags;
        public UVMatrix UV0;
        public float4   localPlane;
        // The brush's surface this polygon shows (its index in the generator's surface array)
        public int      descriptionIndex;
        // The surfaces in the same group (not 0) of the same brush, or generator, share their normals where they meet
        public uint     smoothingGroup;
        // Hotspot mapping: the layout (ChiselHotspotStore, 0 for none), the pinned tile and the seed
        public int      hotspotLayout;
        public int      hotspotPinned, hotspotPinnedSheet;
        public uint     hotspotSeed;
        // and how the texture lies on it: turned (degrees), its density, mirrored (HotspotSurface)
        public float    hotspotRotation;
        public float    hotspotDensity;
        public bool     hotspotMirror;
        public float2   hotspotOffset;
        // the texture is not locked to the object (SurfaceDetailFlags.TextureIsInWorldSpace): a hotspot surface's frame
        // comes from the tree's axes and its repeats lie on a grid fixed in the tree
        public bool     textureInWorld;
        // its uvs are interpolated from the ones its generator gave its corners (BasePolygonsBlob.edgeUVs)
        public bool     cornerUVs;
    }

    struct BasePolygonsBlob
    {
        public BlobArray<BasePolygon> polygons;
        public BlobArray<Edge>        edges;
        public BlobArray<float3>      vertices;
        public BlobArray<BaseSurface> surfaces;
        // By edge, the uv its polygon has at the edge's first vertex, when the brush's generator gave them (empty otherwise)
        public BlobArray<float2>      edgeUVs;
    }
}
