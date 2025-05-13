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
    }

	internal interface IChiselOutputMeshCopier
	{
		// Returns the desired Mesh.MeshData's we need, 0 if we don't need any.
		// meshOffset is the index into the array of Mesh.MeshData's
#if false
		int GetOutputMeshCount([ReadOnly] NativeArray<MeshQuery> meshQueries,
							   [ReadOnly] NativeArray<int>		 parameterCounts);
#endif

		void CopyMesh([NoAlias, ReadOnly] NativeArray<VertexAttributeDescriptor> descriptors,
					  [NoAlias, ReadOnly] SubMeshSection vertexBufferInit,
					  [NoAlias, ReadOnly] SubMeshSource inputMeshSource,
					  [NoAlias] ref Mesh.MeshData meshData);
	}
}