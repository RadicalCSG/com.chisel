using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;
using Unity.Entities;
using UnityEditor;
using UnityEngine.Pool;
using Unity.Mathematics;

namespace Chisel.Core
{
	// TODO: actually use this
	public struct SubMeshTriangleLookup
	{
		public BlobArray<int> perTriangleSelectionIDLookup;
		public BlobArray<int> selectionToInstanceIDs;

		// TODO: use this for surface selection
		public BlobArray<int> perTriangleSurfaceIndexLookup;
		public BlobArray<CompactNodeID> perTriangleNodeIDLookup;

		public int hashCode;

		internal static BlobAssetReference<SubMeshTriangleLookup> Create(
							SubMeshSection subMeshSection,
							NativeList<SubMeshDescriptions> subMeshDescriptions,
							NativeArray<UnsafeList<SubMeshSurface>> subMeshSurfaces,
							CompactHierarchyManagerInstance.ReadOnlyInstanceIDLookup instanceIDLookup,
							Allocator allocator)
        {
            var totalVertexCount    = subMeshSection.totalVertexCount;
            var totalIndexCount     = subMeshSection.totalIndexCount;
            var startIndex          = subMeshSection.startIndex;
            var endIndex            = subMeshSection.endIndex;
            var subMeshCount		= endIndex - startIndex;
			if (totalVertexCount == 0 || totalIndexCount < 3 || subMeshCount == 0)
                return BlobAssetReference<SubMeshTriangleLookup>.Null;

			var triangleCount = totalIndexCount / 3;

			using var builder = new BlobBuilder(Allocator.Temp);
			ref var root = ref builder.ConstructRoot<SubMeshTriangleLookup>();

			var perTriangleSurfaceIndexLookup = builder.Allocate(ref root.perTriangleSurfaceIndexLookup, triangleCount);
			var perTriangleSelectionIDLookup = builder.Allocate(ref root.perTriangleSelectionIDLookup, triangleCount);
			var perTriangleNodeIDLookup = builder.Allocate(ref root.perTriangleNodeIDLookup, triangleCount);

			using var uniqueInstanceIDs = new NativeHashMap<int, int>(triangleCount, Allocator.Temp);
			using var selectionToInstanceIDs = new NativeList<int>(triangleCount, Allocator.Temp);

			int currentBaseIndex = 0;
			for (int subMeshIndex = 0, d = startIndex; d < endIndex; d++, subMeshIndex++)
			{
				var subMeshDescription  = subMeshDescriptions[d];
				var indexCount			= subMeshDescription.indexCount;
				var surfacesOffset		= subMeshDescription.surfacesOffset;
				var surfacesCount		= subMeshDescription.surfacesCount;
				var meshQueryIndex		= subMeshDescription.meshQueryIndex;
				var subMeshSurfaceArray = subMeshSurfaces[meshQueryIndex];

				for (int brushIDIndexOffset = currentBaseIndex / 3,
						 lastSurfaceIndex = surfacesCount + surfacesOffset,
						 surfaceIndex = surfacesOffset;
						 surfaceIndex < lastSurfaceIndex; surfaceIndex++)
				{
					var subMeshSurface	= subMeshSurfaceArray[surfaceIndex];
					ref var brushRenderBuffer = ref subMeshSurface.brushRenderBuffer.Value;
					ref var surface = ref brushRenderBuffer.surfaces[subMeshSurface.surfaceIndex];
					ref var indices = ref surface.indices;
					var brushIndexCount = indices.Length;
					if (brushIndexCount == 0)
						continue;

					var brushNodeID	= subMeshSurface.brushNodeID;
					var instanceID	= instanceIDLookup.SafeGetNodeInstanceID(brushNodeID);

					if (!uniqueInstanceIDs.TryGetValue(instanceID, out int selectionID))
					{
						selectionID = selectionToInstanceIDs.Length;
						uniqueInstanceIDs.Add(instanceID, selectionID);
						selectionToInstanceIDs.Add(instanceID);
					}

					var brushTriangleCount = brushIndexCount / 3;
					for (int n = 0; n < brushTriangleCount; n++)
					{
						perTriangleNodeIDLookup[n + brushIDIndexOffset] = brushNodeID;
						perTriangleSurfaceIndexLookup[n + brushIDIndexOffset] = surfaceIndex;
						perTriangleSelectionIDLookup[n + brushIDIndexOffset] = selectionID;
					}
					brushIDIndexOffset += brushTriangleCount;
				}
				currentBaseIndex += indexCount;
			}

			var hashCode = selectionToInstanceIDs.Hash();

			root.hashCode = (int)hashCode;
			builder.Construct(ref root.selectionToInstanceIDs, selectionToInstanceIDs);

			return builder.CreateBlobAssetReference<SubMeshTriangleLookup>(allocator);
		}

		public void CopyTo(ManagedSubMeshTriangleLookup managedSubMeshTriangleLookup)
		{
			if (managedSubMeshTriangleLookup.perTriangleSurfaceIndexLookup == null ||
				managedSubMeshTriangleLookup.perTriangleSurfaceIndexLookup.Length < perTriangleSurfaceIndexLookup.Length)
				managedSubMeshTriangleLookup.perTriangleSurfaceIndexLookup = new int[perTriangleSurfaceIndexLookup.Length];
			if (managedSubMeshTriangleLookup.perTriangleSurfaceIndexLookup.Length > 0)
				perTriangleSurfaceIndexLookup.CopyTo(managedSubMeshTriangleLookup.perTriangleSurfaceIndexLookup, perTriangleSurfaceIndexLookup.Length);
			else
				managedSubMeshTriangleLookup.perTriangleSurfaceIndexLookup = Array.Empty<int>();

			if (managedSubMeshTriangleLookup.perTriangleSelectionIDLookup == null ||
				managedSubMeshTriangleLookup.perTriangleSelectionIDLookup.Length < perTriangleSelectionIDLookup.Length)
				managedSubMeshTriangleLookup.perTriangleSelectionIDLookup = new int[perTriangleSelectionIDLookup.Length];
			if (managedSubMeshTriangleLookup.perTriangleSelectionIDLookup.Length > 0)
				perTriangleSelectionIDLookup.CopyTo(managedSubMeshTriangleLookup.perTriangleSelectionIDLookup, perTriangleSelectionIDLookup.Length);
			else
				managedSubMeshTriangleLookup.perTriangleSelectionIDLookup = Array.Empty<int>();

			if (managedSubMeshTriangleLookup.selectionToInstanceIDs == null ||
				managedSubMeshTriangleLookup.selectionToInstanceIDs.Length < selectionToInstanceIDs.Length)
				managedSubMeshTriangleLookup.selectionToInstanceIDs = new int[selectionToInstanceIDs.Length];
			if (managedSubMeshTriangleLookup.selectionToInstanceIDs.Length > 0)
				selectionToInstanceIDs.CopyTo(managedSubMeshTriangleLookup.selectionToInstanceIDs, selectionToInstanceIDs.Length);
			else
				managedSubMeshTriangleLookup.selectionToInstanceIDs = Array.Empty<int>();

			if (managedSubMeshTriangleLookup.perTriangleNodeIDLookup == null ||
				managedSubMeshTriangleLookup.perTriangleNodeIDLookup.Length < perTriangleNodeIDLookup.Length)
				managedSubMeshTriangleLookup.perTriangleNodeIDLookup = new CompactNodeID[perTriangleNodeIDLookup.Length];
			if (managedSubMeshTriangleLookup.perTriangleNodeIDLookup.Length > 0)
				perTriangleNodeIDLookup.CopyTo(managedSubMeshTriangleLookup.perTriangleNodeIDLookup, perTriangleNodeIDLookup.Length);
			else
				managedSubMeshTriangleLookup.perTriangleNodeIDLookup = Array.Empty<CompactNodeID>();
			managedSubMeshTriangleLookup.hashCode = hashCode;
		} 
	}

	public interface IBrushVisibilityLookup
    {
        bool IsBrushVisible(CompactNodeID brushID);
		bool IsBrushVisible(int instanceID);
	}

	// TODO: use the native blob instead
	[Serializable]
	public class ManagedSubMeshTriangleLookup
	{
		public CompactNodeID[] perTriangleNodeIDLookup = Array.Empty<CompactNodeID>();
		public int[] perTriangleSelectionIDLookup = Array.Empty<int>();
		public int[] selectionToInstanceIDs = Array.Empty<int>();
		public int[] perTriangleSurfaceIndexLookup = Array.Empty<int>();
		public int hashCode = 0;

		public void Clear()
		{
			perTriangleNodeIDLookup = Array.Empty<CompactNodeID>();
			perTriangleSurfaceIndexLookup = Array.Empty<int>();
			hashCode = 0;
		}

		// TODO: put this in a job so we can optimize this
		// TODO: refactor this so we work on groups of meshes instead
		public void GenerateSubMesh<BrushVisibilityLookup>(BrushVisibilityLookup visibilityLookup, Mesh srcMesh, Mesh dstMesh)
			where BrushVisibilityLookup : unmanaged, IBrushVisibilityLookup
		{
			dstMesh.Clear(keepVertexLayout: true);
			if (perTriangleNodeIDLookup.Length == 0)
				return;

			List<Vector3> sVertices = new();
			List<Vector3> sNormals = new();
			List<Vector4> sTangents = new();
			List<Vector2> sUV0 = new();
			List<int> sSrcTriangles = new();
			List<int> sDstTriangles = new();

			srcMesh.GetVertices(sVertices);
			dstMesh.SetVertices(sVertices);

			srcMesh.GetNormals(sNormals);
			dstMesh.SetNormals(sNormals);

			srcMesh.GetTangents(sTangents);
			dstMesh.SetTangents(sTangents);

			srcMesh.GetUVs(0, sUV0);
			dstMesh.SetUVs(0, sUV0);

			dstMesh.subMeshCount = srcMesh.subMeshCount;
			for (int subMesh = 0, n = 0; subMesh < srcMesh.subMeshCount; subMesh++)
			{
				bool calculateBounds = false;
				int baseVertex = (int)srcMesh.GetBaseVertex(subMesh);
				srcMesh.GetTriangles(sSrcTriangles, subMesh, applyBaseVertex: false);
				sDstTriangles.Clear();
				var prevBrushID = CompactNodeID.Invalid;
				var isBrushVisible = true;
				for (int i = 0; i < sSrcTriangles.Count; i += 3, n++)
				{
					if (n < perTriangleNodeIDLookup.Length)
					{
						var brushID = perTriangleNodeIDLookup[n];
						if (prevBrushID != brushID)
						{
							isBrushVisible = visibilityLookup.IsBrushVisible(brushID);
							prevBrushID = brushID;
						}
						if (!isBrushVisible)
							continue;
					}
					sDstTriangles.Add(sSrcTriangles[i + 0]);
					sDstTriangles.Add(sSrcTriangles[i + 1]);
					sDstTriangles.Add(sSrcTriangles[i + 2]);
				}
				dstMesh.SetTriangles(sDstTriangles, subMesh, calculateBounds, baseVertex);
			}
			dstMesh.RecalculateBounds();
		}

		public delegate bool NeedToRenderForPicking(GameObject go);


		// TODO: put this in a job so we can optimize this
		// TODO: refactor this so we work on groups of meshes instead
		public void GenerateSelectionSubMesh(HashSet<int> skipSelectionID, Mesh srcMesh, Mesh dstMesh)
		{
			dstMesh.Clear(keepVertexLayout: true);
			if (perTriangleNodeIDLookup.Length == 0)
			{
				Debug.Log("perTriangleNodeIDLookup.Length == 0");
				return;
			}

			var sSrcVertices = ListPool<Vector3>.Get();
			var sSrcTriangles = ListPool<int>.Get();
			var sDstSubMeshes = ListPool<List<int>>.Get();

			srcMesh.GetVertices(sSrcVertices);

			var vertexLookup = DictionaryPool<(int, Vector3), int>.Get();
			var sInstanceIDs = ListPool<Vector4>.Get();
			var sVertices = ListPool<Vector3>.Get();

			dstMesh.subMeshCount = srcMesh.subMeshCount;
			for (int subMesh = 0, n = 0; subMesh < srcMesh.subMeshCount; subMesh++)
			{
				srcMesh.GetTriangles(sSrcTriangles, subMesh, applyBaseVertex: true);
				
				var sDstTriangles = ListPool<int>.Get();
				sDstSubMeshes.Add(sDstTriangles);

				for (int i = 0; i < sSrcTriangles.Count; i += 3, n++)
				{
					var selectionID = perTriangleSelectionIDLookup[n];
					if (skipSelectionID.Contains(selectionID))
						continue;

					var selectionIDVec = HandleUtility.EncodeSelectionId(selectionID);

					{
						var srcIndex = sSrcTriangles[i + 0];
						var srcVertex = sSrcVertices[srcIndex];
						if (!vertexLookup.TryGetValue((selectionID, srcVertex), out int dstIndex))
						{
							dstIndex = sVertices.Count;
							vertexLookup[(selectionID, srcVertex)] = dstIndex;
							sVertices.Add(srcVertex);
							sInstanceIDs.Add(selectionIDVec);
						}
						sDstTriangles.Add(dstIndex);
					}

					{
						var srcIndex = sSrcTriangles[i + 1];
						var srcVertex = sSrcVertices[srcIndex];
						if (!vertexLookup.TryGetValue((selectionID, srcVertex), out int dstIndex))
						{
							dstIndex = sVertices.Count;
							vertexLookup[(selectionID, srcVertex)] = dstIndex;
							sVertices.Add(srcVertex);
							sInstanceIDs.Add(selectionIDVec);
						}
						sDstTriangles.Add(dstIndex);
					}

					{
						var srcIndex = sSrcTriangles[i + 2];
						var srcVertex = sSrcVertices[srcIndex];
						if (!vertexLookup.TryGetValue((selectionID, srcVertex), out int dstIndex))
						{
							dstIndex = sVertices.Count;
							vertexLookup[(selectionID, srcVertex)] = dstIndex;
							sVertices.Add(srcVertex);
							sInstanceIDs.Add(selectionIDVec);
						}
						sDstTriangles.Add(dstIndex);
					}
				}
			}

			dstMesh.SetVertices(sVertices);
			dstMesh.SetUVs(0, sInstanceIDs);

			for (int subMesh = 0; subMesh < srcMesh.subMeshCount; subMesh++)
			{
				bool calculateBounds = false;
				var sDstTriangles = sDstSubMeshes[subMesh];
				dstMesh.SetTriangles(sDstTriangles, subMesh, calculateBounds, 0);
			}
			dstMesh.RecalculateBounds();

			ListPool<Vector3>.Release(sSrcVertices);
			ListPool<int>.Release(sSrcTriangles);

			for (int i = 0; i < sDstSubMeshes.Count; i++)
			{
				ListPool<int>.Release(sDstSubMeshes[i]);
			}

			ListPool<List<int>>.Release(sDstSubMeshes);

			DictionaryPool<(int, Vector3), int>.Release(vertexLookup);

			ListPool<Vector3>.Release(sVertices);
			ListPool<Vector4>.Release(sInstanceIDs);
		}
	}
}