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
	public struct SelectionDescription : IEquatable<SelectionDescription>
	{
		public ulong entityID;
		public int surfaceIndex;
		public CompactNodeID brushNodeID;

		public override readonly int GetHashCode() { return (int)Hash(); }
		public readonly uint Hash() { unchecked { return math.hash(new int4((int)entityID, (int)(entityID >> 32), surfaceIndex, (int)brushNodeID.Hash())); } }
		public override readonly bool Equals(object obj)
		{
			if (obj is SelectionDescription selectionDescription) return Equals(selectionDescription);
			return false;
		}
		public readonly bool Equals(SelectionDescription other)
		{
			return entityID == other.entityID && surfaceIndex == other.surfaceIndex && brushNodeID == other.brushNodeID;
		}
	} 

	// The triangles of one surface in a mesh: what each of them is (SubMeshTriangleLookup)
	public struct SubMeshTriangleRun
	{
		public int start, count;
		public CompactNodeID nodeID;
		public int surfaceIndex;
		public int selectionID;
	}

	// What each triangle of a mesh is, a run per surface (CopyTo spells it out per triangle)
	public struct SubMeshTriangleLookup
	{
		public BlobArray<SubMeshTriangleRun> runs;
		public BlobArray<SelectionDescription> selectionIndexDescriptions;
		public int triangleCount;

		public int hashCode;

		internal static BlobAssetReference<SubMeshTriangleLookup> Create(
							SubMeshSection subMeshSection,
							NativeList<SubMeshDescriptions> subMeshDescriptions,
							NativeArray<UnsafeList<SubMeshSurface>> subMeshSurfaces,
							CompactHierarchyManagerInstance.ReadOnlyEntityIDLookup entityIDLookup,
							Allocator allocator = Allocator.Persistent)// Indirect
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
			root.triangleCount = triangleCount;

			using var runs = new NativeList<SubMeshTriangleRun>(Allocator.Temp);
			using var selectionIndexDescriptions = new NativeList<SelectionDescription>(triangleCount, Allocator.Temp);

			var lastBrushNodeID = CompactNodeID.Invalid;
			ulong lastEntityID = 0;

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
					if (brushNodeID != lastBrushNodeID)
					{
						lastBrushNodeID = brushNodeID;
						lastEntityID    = entityIDLookup.SafeGetNodeEntityID(brushNodeID);
					}

					var selectionID = selectionIndexDescriptions.Length;
					selectionIndexDescriptions.Add(new SelectionDescription()
					{
						// A decal's surface selects the decal, and the brush surface it lies on
						entityID = surface.decalEntityID != 0 ? surface.decalEntityID : lastEntityID,
						surfaceIndex = surface.baseSurfaceIndex,
						brushNodeID = brushNodeID
					});

					var brushTriangleCount = brushIndexCount / 3;
					if (brushIDIndexOffset + brushTriangleCount > triangleCount)
						brushTriangleCount = triangleCount - brushIDIndexOffset;
					if (brushTriangleCount <= 0)
						break;
					runs.Add(new SubMeshTriangleRun { start = brushIDIndexOffset, count = brushTriangleCount, nodeID = brushNodeID, surfaceIndex = surfaceIndex, selectionID = selectionID });
					brushIDIndexOffset += brushTriangleCount;
				}
				currentBaseIndex += indexCount;
			}

			var hashCode = selectionIndexDescriptions.Hash();

			root.hashCode = (int)hashCode;
			builder.Construct(ref root.selectionIndexDescriptions, selectionIndexDescriptions);
			builder.Construct(ref root.runs, runs);

			return builder.CreateBlobAssetReference<SubMeshTriangleLookup>(allocator);//Allocator.Persistent / Confirmed to be disposed
		}

		public void CopyTo(ManagedSubMeshTriangleLookup managedSubMeshTriangleLookup)
		{
			if (managedSubMeshTriangleLookup.perTriangleSurfaceIndexLookup == null ||
				managedSubMeshTriangleLookup.perTriangleSurfaceIndexLookup.Length < triangleCount)
				managedSubMeshTriangleLookup.perTriangleSurfaceIndexLookup = new int[triangleCount];
			if (managedSubMeshTriangleLookup.perTriangleSurfaceIndexLookup.Length == 0)
				managedSubMeshTriangleLookup.perTriangleSurfaceIndexLookup = Array.Empty<int>();

			if (managedSubMeshTriangleLookup.perTriangleSelectionIDLookup == null ||
				managedSubMeshTriangleLookup.perTriangleSelectionIDLookup.Length < triangleCount)
				managedSubMeshTriangleLookup.perTriangleSelectionIDLookup = new int[triangleCount];
			if (managedSubMeshTriangleLookup.perTriangleSelectionIDLookup.Length == 0)
				managedSubMeshTriangleLookup.perTriangleSelectionIDLookup = Array.Empty<int>();

			if (managedSubMeshTriangleLookup.selectionIndexDescriptions == null ||
				managedSubMeshTriangleLookup.selectionIndexDescriptions.Length < selectionIndexDescriptions.Length)
				managedSubMeshTriangleLookup.selectionIndexDescriptions = new SelectionDescription[selectionIndexDescriptions.Length];
			if (managedSubMeshTriangleLookup.selectionIndexDescriptions.Length > 0)
				selectionIndexDescriptions.CopyTo(managedSubMeshTriangleLookup.selectionIndexDescriptions, selectionIndexDescriptions.Length);
			else
				managedSubMeshTriangleLookup.selectionIndexDescriptions = Array.Empty<SelectionDescription>();

			if (managedSubMeshTriangleLookup.perTriangleNodeIDLookup == null ||
				managedSubMeshTriangleLookup.perTriangleNodeIDLookup.Length < triangleCount)
				managedSubMeshTriangleLookup.perTriangleNodeIDLookup = new CompactNodeID[triangleCount];
			if (managedSubMeshTriangleLookup.perTriangleNodeIDLookup.Length == 0)
				managedSubMeshTriangleLookup.perTriangleNodeIDLookup = Array.Empty<CompactNodeID>();
			if (triangleCount > 0)
			{
				unsafe
				{
					fixed (int* surfaces = managedSubMeshTriangleLookup.perTriangleSurfaceIndexLookup, selections = managedSubMeshTriangleLookup.perTriangleSelectionIDLookup)
					fixed (CompactNodeID* nodes = managedSubMeshTriangleLookup.perTriangleNodeIDLookup)
						SubMeshTriangleRuns.Spell((SubMeshTriangleRun*)runs.GetUnsafePtr(), runs.Length, triangleCount, surfaces, selections, nodes);
				}
			}
			managedSubMeshTriangleLookup.hashCode = hashCode;
			managedSubMeshTriangleLookup.validTriangleCount = triangleCount;
		}

		// The run that holds a triangle (default: one no run holds)
		public SubMeshTriangleRun RunAt(int triangle)
		{
			int lo = 0, hi = runs.Length - 1;
			while (lo <= hi)
			{
				var mid = (lo + hi) >> 1;
				var run = runs[mid];
				if (triangle < run.start) hi = mid - 1;
				else if (triangle >= run.start + run.count) lo = mid + 1;
				else return run;
			}
			return default;
		}
	}

	// SubMeshTriangleLookup.CopyTo, compiled by Burst: the first triangleCount entries of each lookup, run by run; a
	// triangle no run holds is 0
	[Unity.Burst.BurstCompile]
	internal static class SubMeshTriangleRuns
	{
		[Unity.Burst.BurstCompile]
		public static unsafe void Spell(SubMeshTriangleRun* runs, int runCount, int triangleCount, int* surfaces, int* selections, CompactNodeID* nodes)
		{
			var at = 0;
			for (int r = 0; r <= runCount; r++)
			{
				var start = r < runCount ? runs[r].start : triangleCount;
				for (int t = at; t < start; t++)
				{
					surfaces[t] = 0;
					selections[t] = 0;
					nodes[t] = default;
				}
				if (r == runCount)
					break;
				var run = runs[r];
				for (int t = run.start, end = run.start + run.count; t < end; t++)
				{
					surfaces[t] = run.surfaceIndex;
					selections[t] = run.selectionID;
					nodes[t] = run.nodeID;
				}
				at = run.start + run.count;
			}
		}
	}

	public interface IBrushVisibilityLookup
    {
        bool IsBrushVisible(CompactNodeID brushID);
		bool IsBrushVisible(ulong entityID);
	}

	// TODO: use the native blob instead
	[Serializable]
	public class ManagedSubMeshTriangleLookup
	{
		[System.NonSerialized] public CompactNodeID[] perTriangleNodeIDLookup = Array.Empty<CompactNodeID>();
		public int[] perTriangleSelectionIDLookup = Array.Empty<int>();
		[System.NonSerialized] public SelectionDescription[] selectionIndexDescriptions = Array.Empty<SelectionDescription>();
		public int[] perTriangleSurfaceIndexLookup = Array.Empty<int>();
		public int hashCode = 0;

		[System.NonSerialized] public int validTriangleCount = 0;

		public void Clear()
		{
			perTriangleNodeIDLookup = Array.Empty<CompactNodeID>();
			perTriangleSurfaceIndexLookup = Array.Empty<int>();
			validTriangleCount = 0;
			hashCode = 0;
		}

		// TODO: put this in a job so we can optimize this
		// TODO: refactor this so we work on groups of meshes instead
		public void GenerateSubMesh<BrushVisibilityLookup>(BrushVisibilityLookup visibilityLookup, Mesh srcMesh, Mesh dstMesh)
			where BrushVisibilityLookup : unmanaged, IBrushVisibilityLookup
		{
			dstMesh.Clear(keepVertexLayout: true);
			dstMesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
			if (perTriangleNodeIDLookup.Length == 0)
				return;

			List<Vector3> sVertices = new();
			List<Vector3> sNormals = new();
			List<Vector4> sTangents = new();
			List<Vector2> sUV0 = new();
			List<int> sSrcTriangles = new();
			List<int> sDstTriangles = new();

			srcMesh.GetVertices(sVertices);
			if (sVertices.Count == 0)
				return;
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
					var i0 = sSrcTriangles[i + 0];
					var i1 = sSrcTriangles[i + 1];
					var i2 = sSrcTriangles[i + 2];
					var maxIndex = sVertices.Count - baseVertex;
					if (i0 < 0 || i0 >= maxIndex || i1 < 0 || i1 >= maxIndex || i2 < 0 || i2 >= maxIndex)
						continue;
					sDstTriangles.Add(i0);
					sDstTriangles.Add(i1);
					sDstTriangles.Add(i2);
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
			dstMesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
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
			var sEntityIDs = ListPool<Vector4>.Get();
			var sVertices = ListPool<Vector3>.Get();


			// Only the first validTriangleCount entries were written by the last CopyTo; the rest of the
			// array is capacity left over from a previous, larger build.
			var validCount = validTriangleCount > 0 ? validTriangleCount : perTriangleSelectionIDLookup.Length;
			int overrun = 0;

			dstMesh.subMeshCount = srcMesh.subMeshCount;
			for (int subMesh = 0, n = 0; subMesh < srcMesh.subMeshCount; subMesh++)
			{
				srcMesh.GetTriangles(sSrcTriangles, subMesh, applyBaseVertex: true);
				
				var sDstTriangles = ListPool<int>.Get();
				sDstSubMeshes.Add(sDstTriangles);

				for (int i = 0; i < sSrcTriangles.Count; i += 3, n++)
				{
					if (n >= validCount)
					{
						overrun++;
						continue;
					}
					var selectionID = perTriangleSelectionIDLookup[n];
					if (skipSelectionID != null && skipSelectionID.Contains(selectionID))
						continue;

					//var selectionIDVec = HandleUtility.EncodeSelectionId(selectionID);
					var selectionIDVec = new Vector4((int)(byte)(selectionID & 0xFF), (int)(byte)((selectionID >> 8) & 0xFF), (int)(byte)((selectionID >> 16) & 0xFF), (int)(byte)((selectionID >> 24) & 0xFF)) / 255f;

					{
						var srcIndex = sSrcTriangles[i + 0];
						var srcVertex = sSrcVertices[srcIndex];
						if (!vertexLookup.TryGetValue((selectionID, srcVertex), out int dstIndex))
						{
							dstIndex = sVertices.Count;
							vertexLookup[(selectionID, srcVertex)] = dstIndex;
							sVertices.Add(srcVertex);
							sEntityIDs.Add(selectionIDVec);
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
							sEntityIDs.Add(selectionIDVec);
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
							sEntityIDs.Add(selectionIDVec);
						}
						sDstTriangles.Add(dstIndex);
					}
				}
			}

			if (overrun > 0)
				Debug.LogError($"Picking selection mesh: the rendered mesh has {overrun} more triangles than the " +
							   $"per-triangle brush-id lookup covers ({validCount}). Those triangles are not pickable; " +
							   $"the lookup and the mesh were built from different data.");

			dstMesh.SetVertices(sVertices);
			dstMesh.SetUVs(0, sEntityIDs);

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
			ListPool<Vector4>.Release(sEntityIDs);
		}
	}
}