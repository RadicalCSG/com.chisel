using Unity.Collections;
using UnityEngine;
using Unity.Burst;
using UnityEngine.Rendering;
using Unity.Collections.LowLevel.Unsafe;

namespace Chisel.Core
{
    struct SubMeshSource
    {
		[NoAlias, ReadOnly] public NativeList<SubMeshDescriptions>         subMeshDescriptions;
		[NoAlias, ReadOnly] public NativeArray<UnsafeList<SubMeshSurface>> subMeshSurfaces;
		// How the meshes' lightmap coordinates are laid out: the model's (LightmapUVLayout)
		public LightmapUVSettings                                   lightmapUVSettings;
		// The tree's paint: a render mesh with painted surfaces takes it in a second vertex stream (ChiselOutputRenderable)
		public ChiselPaintLookup                                    paint;
    }

	internal interface IChiselOutputMeshCopier
	{
		// Returns the desired Mesh.MeshData's we need, 0 if we don't need any.
		// meshOffset is the index into the array of Mesh.MeshData's

		void CopyMesh([NoAlias, ReadOnly] NativeArray<VertexAttributeDescriptor> descriptors,
					  [NoAlias, ReadOnly] SubMeshSection vertexBufferInit,
					  [NoAlias, ReadOnly] SubMeshSource inputMeshSource,
					  [NoAlias] ref Mesh.MeshData meshData);
	}
}