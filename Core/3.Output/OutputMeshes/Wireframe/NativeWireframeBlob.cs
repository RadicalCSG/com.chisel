using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Mathematics;

namespace Chisel.Core
{
	public struct NativeWireframeBlob
    {
        public BlobArray<float3> vertices;
        public BlobArray<int> visibleOuterLines;
        public BlobArray<int> surfaceVisibleOuterLines;
        public BlobArray<int> surfaceVisibleOuterLineRanges;
        public uint hash;

        public static BlobAssetReference<NativeWireframeBlob> Create(ref BrushMeshBlob brushMesh, Allocator allocator)
		{
			using var builder = new BlobBuilder(Allocator.Temp);
			ref var root = ref builder.ConstructRoot<NativeWireframeBlob>();

			ref var polygons = ref brushMesh.polygons;
			ref var halfEdges = ref brushMesh.halfEdges;
			ref var localVertices = ref brushMesh.localVertices;

			builder.Construct(ref root.vertices, ref localVertices);

			using var visibleOuterLines = new UnsafeList<int>(halfEdges.Length * 2, Allocator.Persistent);
			using var surfaceVisibleOuterLines = new UnsafeList<int>(halfEdges.Length * 2, Allocator.Persistent);
			
			var surfaceVisibleOuterLineRanges = builder.Allocate(ref root.surfaceVisibleOuterLineRanges, polygons.Length);
			for (int p = 0; p < polygons.Length; p++)
			{
				var firstEdge = polygons[p].firstEdge;
				var edgeCount = polygons[p].edgeCount;
				var lastEdge = firstEdge + edgeCount;

				var vertexIndex0 = halfEdges[lastEdge - 1].vertexIndex;
				int vertexIndex1;
				for (int h = firstEdge; h < lastEdge; vertexIndex0 = vertexIndex1, h++)
				{
					vertexIndex1 = halfEdges[h].vertexIndex;
					if (vertexIndex0 > vertexIndex1) // avoid duplicate edges
					{
						visibleOuterLines.Add(vertexIndex0);
						visibleOuterLines.Add(vertexIndex1);
					}
					surfaceVisibleOuterLines.Add(vertexIndex0); 
					surfaceVisibleOuterLines.Add(vertexIndex1);
				}
				surfaceVisibleOuterLineRanges[p] = surfaceVisibleOuterLines.Length;
			}

			builder.Construct(ref root.visibleOuterLines, visibleOuterLines);
			builder.Construct(ref root.surfaceVisibleOuterLines, surfaceVisibleOuterLines);
			root.hash = math.hash(new uint4(localVertices.Hash(),
									   visibleOuterLines.Hash(),
									   surfaceVisibleOuterLines.Hash(),
									   root.surfaceVisibleOuterLineRanges.Hash()));
			return builder.CreateBlobAssetReference<NativeWireframeBlob>(allocator);
		}
    }
}
