using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace Chisel.Core.Tests
{
	public sealed class ContentsTreeHarness : IDisposable
	{
		public struct CapturedTriangle
		{
			public float3 a, b, c;
			public CompactNodeID brushID;
			// The normal the pipeline gave the surface. Unlike Normal it is exact on a sliver a fraction
			// of a millimetre wide, where the cross product of the edges is mostly rounding.
			public float3 surfaceNormal;
			// The material the triangle is drawn with (its sub-mesh's surface parameter), and its texture
			// coordinates. Decals draw with their own material (Documentation~/Design/Decals.md).
			public ulong  material;
			public float2 uvA, uvB, uvC;
			// Its lightmap coordinates (UV1), and the renderable section (mesh) it came from: each has a lightmap of its own
			public float2 lightmapA, lightmapB, lightmapC;
			// Each vertex's normal and tangent as the mesh holds them, not made safe: a NaN here lights the surface NaN
			public float3 normalA, normalB, normalC;
			public float4 tangentA, tangentB, tangentC;
			public int    section;
			// The entity a click on the triangle selects: its brush's, or its decal's
			public ulong  selectedEntityID;

			public readonly float3 Center => (a + b + c) / 3.0f;
			// Geometric normal from the winding, which is what tells a face drawn outwards from the
			// wall a carve leaves behind.
			public readonly float3 Normal => math.normalizesafe(math.cross(b - a, c - a));
		}

		readonly List<BrushMeshInstance>  instances = new List<BrushMeshInstance>();
		readonly List<ContentsSceneNode>  nodes     = new List<ContentsSceneNode>();
		readonly List<CSGTreeBrush>       brushes   = new List<CSGTreeBrush>();
		readonly List<CapturedTriangle>   triangles = new List<CapturedTriangle>();
		// Every node this harness created, brushes and composites, attached or not, so edits can find
		// them and Dispose can destroy all of them.
		readonly Dictionary<ContentsSceneNode, CSGTreeNode> treeNodes = new Dictionary<ContentsSceneNode, CSGTreeNode>();

		ContentsScene scene;
		CSGTree tree;
		bool capturing;

		public IReadOnlyList<CapturedTriangle> Triangles => triangles;
		// The total area of the collider triangles the last delivered update made
		public double ColliderArea { get; private set; }
		public int ColliderVertexCount { get; private set; }
		public int ColliderPositionCount { get; private set; }
		public int DegenerateTriangleCount { get; private set; }
		public int ColliderDegenerateTriangleCount { get; private set; }
		public string LookupMismatches { get; private set; }
		public CSGTree Tree => tree;
		public ContentsScene Scene => scene;

		// Whether the last update delivered meshes for this tree. When it didn't, Triangles still holds
		// the previous output, which is exactly what the editor would keep showing.
		public bool Delivered { get; private set; }

		public string LastUpdateReport { get; private set; } = "(no update yet)";
		int callbacksForThisTree, callbacksForOtherTrees, renderableSections, sectionsWithLookup;

		public static ContentsTreeHarness Build(ContentsScene scene)
		{
			PrepareForUpdate();

			var harness  = new ContentsTreeHarness { scene = scene };
			var children = new CSGTreeNode[scene.roots.Count];
			for (int i = 0; i < scene.roots.Count; i++)
				children[i] = harness.CreateNode(scene.roots[i]);
			harness.tree = CSGTree.Create(default(UnityEngine.EntityId), children);

			// The exact CSG's planes and triangles, for ExactJudge; only captured while the exact CSG runs. Held until
			// Dispose, so it covers every update this harness makes.
			ExactCSGCapture.Acquire();
			harness.capturing = true;
			return harness;
		}

		CSGTreeNode CreateNode(ContentsSceneNode node)
		{
			if (node.IsBrush)
			{
				var instance = node.localVertices != null ? CreateBrushMesh(node) : CreateBrushMesh(node.localPlanes);
				instances.Add(instance);

				var brush = CSGTreeBrush.Create(UnityEngine.EntityId.FromULong(node.entityID), node.localToTree, instance, node.operation);
				brush.Contents = node.contents;
				nodes.Add(node);
				brushes.Add(brush);
				treeNodes[node] = brush;
				return brush;
			}

			var children = new CSGTreeNode[node.children.Count];
			for (int i = 0; i < node.children.Count; i++)
				children[i] = CreateNode(node.children[i]);
			var branch = CSGTreeBranch.Create(default(UnityEngine.EntityId), node.operation, children);
			treeNodes[node] = branch;
			return branch;
		}

		static BrushMeshInstance CreateBrushMesh(float4[] planes)
		{
			var surfaceArray = new ChiselSurfaceArray();
			surfaceArray.EnsureSize(planes.Length);
			for (int i = 0; i < surfaceArray.surfaces.Length; i++)
				surfaceArray.surfaces[i] = new ChiselSurface();

			if (!ExactBrushOutline.FromPlanes(planes, out var brushMesh, out _))
				return BrushMeshInstance.InvalidInstance;
			return BrushMeshInstance.Create(brushMesh, in surfaceArray);
		}

		BrushMeshInstance CreateBrushMesh(ContentsSceneNode node)
		{
			var vertices  = node.localVertices;
			var loops     = node.polygonLoops;
			var polygons  = new List<BrushMesh.Polygon>();
			var halfEdges = new List<BrushMesh.HalfEdge>();
			int start = 0;
			for (int i = 0; i < loops.Length; i++)
			{
				if (loops[i] >= 0)
					continue;
				polygons.Add(new BrushMesh.Polygon { firstEdge = halfEdges.Count, edgeCount = i - start, descriptionIndex = polygons.Count });
				for (int k = start; k < i; k++)
					halfEdges.Add(new BrushMesh.HalfEdge { vertexIndex = loops[k], twinIndex = -1 });
				start = i + 1;
			}

			// A half edge joins its own vertex to the one before it in its polygon; its twin joins the same two the other way
			var edges = halfEdges.ToArray();
			var byEnds = new Dictionary<(int, int), int>();
			foreach (var polygon in polygons)
			{
				for (int e = polygon.firstEdge; e < polygon.firstEdge + polygon.edgeCount; e++)
				{
					int previous = e == polygon.firstEdge ? polygon.firstEdge + polygon.edgeCount - 1 : e - 1;
					byEnds[(edges[e].vertexIndex, edges[previous].vertexIndex)] = e;
				}
			}
			foreach (var polygon in polygons)
			{
				for (int e = polygon.firstEdge; e < polygon.firstEdge + polygon.edgeCount; e++)
				{
					int previous = e == polygon.firstEdge ? polygon.firstEdge + polygon.edgeCount - 1 : e - 1;
					if (!byEnds.TryGetValue((edges[previous].vertexIndex, edges[e].vertexIndex), out var twin))
						throw new InvalidOperationException($"{node.name}: the edge {edges[previous].vertexIndex}-{edges[e].vertexIndex} of its mesh has no twin");
					edges[e].twinIndex = twin;
				}
			}

			var surfaceArray = new ChiselSurfaceArray();
			surfaceArray.EnsureSize(polygons.Count);
			for (int i = 0; i < surfaceArray.surfaces.Length; i++)
				surfaceArray.surfaces[i] = new ChiselSurface();
			var brushMesh = new BrushMesh { vertices = (float3[])vertices.Clone(), halfEdges = edges, polygons = polygons.ToArray(),
											planes = node.localPlanes != null && node.localPlanes.Length == polygons.Count ? (float4[])node.localPlanes.Clone() : null };
			var instance  = BrushMeshInstance.Create(brushMesh, in surfaceArray);

			var blob = BrushMeshManager.GetBrushMeshBlob(instance);
			if (!blob.IsCreated)
				planeMismatches.Add($"{node.name}: its mesh registered as nothing");
			else
			{
				ref var mesh = ref blob.Value;
				if (mesh.localPlaneCount != node.localPlanes.Length)
					planeMismatches.Add($"{node.name}: its mesh has {mesh.localPlaneCount} planes, the description {node.localPlanes.Length}");
				else
				{
					for (int p = 0; p < mesh.localPlaneCount; p++)
					{
						if (math.all(math.asint(mesh.localPlanes[p]) == math.asint(node.localPlanes[p])))
							continue;
						planeMismatches.Add($"{node.name}: plane {p} registered as {mesh.localPlanes[p]}, the description has {node.localPlanes[p]}");
						break;
					}
				}
			}
			return instance;
		}

		readonly List<string> planeMismatches = new List<string>();

		// Brushes built from their own mesh whose registered planes are not the planes their description gives: a replay
		// that is not the scene it replays. Empty when every brush came back exactly.
		public IReadOnlyList<string> PlaneMismatches => planeMismatches;

		#region Edits

		// Adds a new node under parent (null for the top level) at index.
		public void Insert(ContentsSceneNode parent, int index, ContentsSceneNode node)
		{
			var created = CreateNode(node);
			if (!InsertIntoTree(parent, index, created))
				throw new InvalidOperationException($"Could not insert {NameOf(node)} at {index} under {NameOf(parent)}");
			ChildrenOf(parent).Insert(index, node);
		}

		// Takes a node out of the tree. Destroying it is what deleting its GameObject does; detaching
		// keeps it alive, so Move can put it somewhere else.
		public void Remove(ContentsSceneNode node, bool destroy)
		{
			var parent = ParentOf(node);
			if (destroy)
				Destroy(node);
			else if (!RemoveFromTree(parent, treeNodes[node]))
				throw new InvalidOperationException($"Could not detach {NameOf(node)} from {NameOf(parent)}");
			ChildrenOf(parent).Remove(node);
		}

		public void Move(ContentsSceneNode node, ContentsSceneNode newParent, int index)
		{
			Remove(node, destroy: false);
			if (!InsertIntoTree(newParent, index, treeNodes[node]))
				throw new InvalidOperationException($"Could not move {NameOf(node)} to {index} under {NameOf(newParent)}");
			ChildrenOf(newParent).Insert(index, node);
		}

		public void SetOperation(ContentsSceneNode node, CSGOperationType operation)
		{
			var treeNode = treeNodes[node];
			treeNode.Operation = operation;
			node.operation = operation;
		}

		// Moves a brush: its transform into the tree, as a user dragging it does.
		public void SetLocalToTree(ContentsSceneNode brush, float4x4 localToTree)
		{
			if (!brush.IsBrush)
				throw new ArgumentException($"{NameOf(brush)} is a composite; only brushes carry a transform here");
			var treeBrush = (CSGTreeBrush)treeNodes[brush];
			treeBrush.LocalTransformation = localToTree;
			brush.localToTree = localToTree;
		}

		public void SetContents(ContentsSceneNode brush, int contents)
		{
			if (!brush.IsBrush)
				throw new ArgumentException($"{NameOf(brush)} is a composite, and composites don't carry contents");
			var treeBrush = (CSGTreeBrush)treeNodes[brush];
			treeBrush.Contents = contents;
			brush.contents = contents;
		}

		bool InsertIntoTree(ContentsSceneNode parent, int index, CSGTreeNode child)
		{
			return parent == null ? tree.Insert(index, child)
								  : ((CSGTreeBranch)treeNodes[parent]).Insert(index, child);
		}

		bool RemoveFromTree(ContentsSceneNode parent, CSGTreeNode child)
		{
			return parent == null ? tree.Remove(child)
								  : ((CSGTreeBranch)treeNodes[parent]).Remove(child);
		}

		void Destroy(ContentsSceneNode node)
		{
			if (!node.IsBrush)
			{
				foreach (var child in node.children)
					Destroy(child);
			}

			DestroyTreeNode(node, treeNodes[node]);
			treeNodes.Remove(node);
			if (node.IsBrush)
			{
				var index = nodes.IndexOf(node);
				nodes.RemoveAt(index);
				brushes.RemoveAt(index);
			}
		}

		static void DestroyTreeNode(ContentsSceneNode node, CSGTreeNode treeNode)
		{
			// Destroying an invalid node logs an error, which fails the test that is cleaning up
			if (!treeNode.Valid)
				return;
			if (node.IsBrush)
				((CSGTreeBrush)treeNode).Destroy();
			else
				((CSGTreeBranch)treeNode).Destroy();
		}

		List<ContentsSceneNode> ChildrenOf(ContentsSceneNode parent) => parent == null ? scene.roots : parent.children;

		ContentsSceneNode ParentOf(ContentsSceneNode node)
		{
			if (scene.roots.Contains(node))
				return null;
			return FindParent(scene.roots, node) ?? throw new ArgumentException($"{NameOf(node)} is not in the scene");
		}

		static ContentsSceneNode FindParent(List<ContentsSceneNode> candidates, ContentsSceneNode node)
		{
			foreach (var candidate in candidates)
			{
				if (candidate.IsBrush)
					continue;
				if (candidate.children.Contains(node))
					return candidate;
				var found = FindParent(candidate.children, node);
				if (found != null)
					return found;
			}
			return null;
		}

		static string NameOf(ContentsSceneNode node) => node == null ? "the tree" : (node.name ?? (node.IsBrush ? "a brush" : "a composite"));
		#endregion

		// Runs the CSG update and fills Triangles. Returns false when nothing was dirty.
		public bool Update()
		{
			var foreign = CountForeignDirtyTrees(tree);
			if (foreign > 0 && AnyChiselModelInScene())
			{
				throw new InvalidOperationException(
					$"{foreign} other Chisel tree(s) need an update ({ForeignDirtyTreeOwners(tree)}) and a Chisel model is in the scene. " +
					"Run this suite in a scene with no model, opened AFTER the last recompile.");
			}

			Delivered = false;
			callbacksForThisTree = callbacksForOtherTrees = renderableSections = sectionsWithLookup = 0;
			var updated = CompactHierarchyManager.Flush(FinishMeshUpdates);
			LastUpdateReport = $"updated={updated}, rounds={CompactHierarchyManager.LastUpdateRounds}, " +
							   $"brushes flagged={CompactHierarchyManager.LastUpdateModifiedBrushCount}; " +
							   $"callbacks: mine={callbacksForThisTree}, other trees={callbacksForOtherTrees}; " +
							   $"renderable sections={renderableSections}, of which with a lookup={sectionsWithLookup}" +
							   (Delivered ? "" : "; nothing delivered, the previous triangles are kept");

			// The exact CSG reports what it could not build instead of repairing it; under the harness any such report is
			// an error of the CSG, never something to average away in a comparison.
			if (CompactHierarchyManager.TreeUpdate.kExactCSG)
			{
				var failures = ExactCSGFailures();
				LastUpdateReport += "; exact CSG: " + (failures ?? "no failures");
				if (failures != null)
					throw new InvalidOperationException("The exact CSG reported failures: " + failures + " (" + LastUpdateReport + ")");

				if (updated && JudgeEachUpdate)
				{
					var mismatches = ExactJudge.FindMismatches(this, kMaxJudgeReports, out _, checkMesh: false);
					if (mismatches.Count > 0)
					{
						var report = new System.Text.StringBuilder("The exact judge disagrees with this update's output:");
						foreach (var mismatch in mismatches)
							report.Append(Environment.NewLine).Append("  ").Append(mismatch.Format("exact"));
						report.Append(Environment.NewLine).Append("  (").Append(LastUpdateReport).Append(')');
						throw new InvalidOperationException(report.ToString());
					}
				}
			}

			// And every delivered update is checked for what the weld at the very end leaves (OutputWeld, ExactCSG.md step 6),
			// whatever else the test checks, in both pipelines
			if (updated && Delivered && CheckWeldEachUpdate)
			{
				var weld = WeldProblems();
				if (weld != null)
					throw new InvalidOperationException("The weld at the very end left: " + weld + " (" + LastUpdateReport + ")");
			}
			return updated;
		}

		// Whether Update checks the delivered meshes for what the weld leaves. Off only in a test that reads the counts itself.
		internal bool CheckWeldEachUpdate { get; set; } = true;

		internal string WeldProblems()
		{
			var text = new System.Text.StringBuilder();
			if (DegenerateTriangleCount > 0)
				text.Append(DegenerateTriangleCount).Append(" render triangle(s) with two corners at one position; ");
			if (ColliderDegenerateTriangleCount > 0)
				text.Append(ColliderDegenerateTriangleCount).Append(" collider triangle(s) with two corners at one position; ");
			if (ColliderVertexCount != ColliderPositionCount)
				text.Append("the collider has ").Append(ColliderVertexCount).Append(" vertices at ").Append(ColliderPositionCount).Append(" positions; ");
			if (LookupMismatches != null)
				text.Append("picking lookups out of step: ").Append(LookupMismatches).Append("; ");
			return text.Length == 0 ? null : text.ToString(0, text.Length - 2);
		}

		const int kMaxJudgeReports = 8;

		internal bool JudgeEachUpdate { get; set; } = true;

		// The failure counters of the last update, or null when there were none.
		internal static string ExactCSGFailures()
		{
			var stats = CompactHierarchyManager.TreeUpdate.s_LastExactCSGStats;
			System.Text.StringBuilder text = null;
			for (int i = (int)ExactCSGStat.InvalidPlane; i < (int)ExactCSGStat.Count; i++)
			{
				if (stats[i] == 0)
					continue;
				text ??= new System.Text.StringBuilder();
				if (text.Length > 0) text.Append(", ");
				text.Append((ExactCSGStat)i).Append('=').Append(stats[i]);
			}
			return text?.ToString();
		}

		internal static bool TwoCornersAtOnePosition(float3 a, float3 b, float3 c)
		{
			return (a.x == b.x && a.y == b.y && a.z == b.z) || (b.x == c.x && b.y == c.y && b.z == c.z) || (c.x == a.x && c.y == a.y && c.z == a.z);
		}

		int FinishMeshUpdates(CSGTree updatedTree, ChiselMeshUpdates meshUpdates, JobHandle dependencies)
		{
			dependencies.Complete();
			try
			{
				if (updatedTree != tree)
				{
					callbacksForOtherTrees++;
					return 0;
				}
				callbacksForThisTree++;
				Delivered = true;
				triangles.Clear();

				ColliderArea = 0;
				ColliderVertexCount = ColliderPositionCount = DegenerateTriangleCount = ColliderDegenerateTriangleCount = 0;
				LookupMismatches = null;
				foreach (var meshUpdate in meshUpdates.meshUpdatesColliders)
				{
					var colliderData     = meshUpdates.meshDataArray[meshUpdate.meshIndex];
					var colliderIndices  = colliderData.GetIndexData<int>();
					var colliderVertices = colliderData.GetVertexData<float3>(0);
					ColliderVertexCount += colliderVertices.Length;
					var positions = new HashSet<float3>();
					for (int v = 0; v < colliderVertices.Length; v++)
						positions.Add(colliderVertices[v] + 0.0f);
					ColliderPositionCount += positions.Count;
					// only the declared triangles: a sub-mesh may write fewer indices than the buffer holds
					var declaredIndices = colliderData.subMeshCount > 0 ? colliderData.GetSubMesh(0).indexCount : 0;
					for (int i = 0; i + 2 < declaredIndices; i += 3)
					{
						var a = colliderVertices[colliderIndices[i]];
						var b = colliderVertices[colliderIndices[i + 1]];
						var c = colliderVertices[colliderIndices[i + 2]];
						ColliderArea += math.length(math.cross((double3)b - a, (double3)c - a)) * 0.5;
						if (TwoCornersAtOnePosition(a, b, c))
							ColliderDegenerateTriangleCount++;
					}
				}

				foreach (var meshUpdate in meshUpdates.meshUpdatesRenderables)
				{
					renderableSections++;
					var lookupRef = meshUpdates.vertexBufferContents.subMeshTriangleLookups[meshUpdate.contentsIndex];
					if (!lookupRef.IsCreated)
						continue;
					sectionsWithLookup++;
					ref var lookup = ref lookupRef.Value;

					var meshData = meshUpdates.meshDataArray[meshUpdate.meshIndex];
					var indices  = meshData.GetIndexData<int>();
					var vertices = meshData.GetVertexData<RenderVertex>(0);

					// The weld drops triangles, so the picking lookup has to cover exactly the triangles the sub-meshes declare,
					// and none of those may have two corners at one position
					var declaredTriangles = 0;
					for (int s = 0; s < meshData.subMeshCount; s++)
					{
						var subMesh = meshData.GetSubMesh(s);
						declaredTriangles += subMesh.indexCount / 3;
						for (int i = subMesh.indexStart; i + 2 < subMesh.indexStart + subMesh.indexCount; i += 3)
						{
							if (TwoCornersAtOnePosition(vertices[indices[i]].position, vertices[indices[i + 1]].position, vertices[indices[i + 2]].position))
								DegenerateTriangleCount++;
						}
					}
					if (lookup.perTriangleNodeIDLookup.Length != declaredTriangles)
						LookupMismatches = (LookupMismatches == null ? "" : LookupMismatches + "; ") +
										   $"section {renderableSections}: lookup {lookup.perTriangleNodeIDLookup.Length} triangles, sub-meshes {declaredTriangles}";

					var count = math.min(lookup.perTriangleNodeIDLookup.Length, indices.Length / 3);
					// The sub-mesh (and so the material) of each triangle, counted the same way
					var descriptions = meshUpdates.vertexBufferContents.meshDescriptions;
					var section      = meshUpdate.subMeshSection;
					var description  = section.startIndex;
					var descriptionEnd = description < section.endIndex ? descriptions[description].indexCount / 3 : int.MaxValue;
					for (int t = 0; t < count; t++)
					{
						while (t >= descriptionEnd && description + 1 < section.endIndex)
						{
							description++;
							descriptionEnd += descriptions[description].indexCount / 3;
						}
						var i0 = indices[(t * 3) + 0];
						var i1 = indices[(t * 3) + 1];
						var i2 = indices[(t * 3) + 2];
						triangles.Add(new CapturedTriangle
						{
							a             = vertices[i0].position,
							b             = vertices[i1].position,
							c             = vertices[i2].position,
							brushID       = lookup.perTriangleNodeIDLookup[t],
							surfaceNormal = math.normalizesafe(vertices[i0].normal),
							material      = description < section.endIndex ? descriptions[description].surfaceParameter : 0,
							uvA           = vertices[i0].uv0,
							uvB           = vertices[i1].uv0,
							uvC           = vertices[i2].uv0,
							lightmapA     = vertices[i0].uv1,
							lightmapB     = vertices[i1].uv1,
							lightmapC     = vertices[i2].uv1,
							normalA       = vertices[i0].normal,
							normalB       = vertices[i1].normal,
							normalC       = vertices[i2].normal,
							tangentA      = vertices[i0].tangent,
							tangentB      = vertices[i1].tangent,
							tangentC      = vertices[i2].tangent,
							section       = renderableSections,
							selectedEntityID = lookup.selectionIndexDescriptions[lookup.perTriangleSelectionIDLookup[t]].entityID
						});
					}
				}
			}
			finally
			{
				// Ownership of the mesh data transfers to this callback, including for trees that are
				// not ours.
				if (meshUpdates.meshDataArray.Length > 0)
					meshUpdates.meshDataArray.Dispose();
			}
			return 0;
		}

		// Which described brush produced this triangle, or null when the triangle came from a tree or
		// a brush this harness did not build.
		public ContentsSceneNode NodeOf(CompactNodeID brushID)
		{
			for (int i = 0; i < brushes.Count; i++)
			{
				if (brushes[i].Valid && brushes[i].CompactNodeID == brushID)
					return nodes[i];
			}
			return null;
		}

		// Plant errors in the captured output, for tests that show a check notices one (ExactJudgeTests).
		internal void RemoveTriangleForTest(int index) => triangles.RemoveAt(index);
		internal void ReplaceTriangleForTest(int index, CapturedTriangle triangle) => triangles[index] = triangle;

		// The stable id of the tree brush built for this described brush (what ExactCSGCapture is keyed by), or
		// NodeID.Invalid when the brush is not in this harness.
		internal NodeID NodeIDOf(ContentsSceneNode brush)
		{
			var index = nodes.IndexOf(brush);
			return index < 0 ? NodeID.Invalid : brushes[index].NodeID;
		}

		// The tree brush built for this described brush; invalid when the brush is not in this harness.
		internal CSGTreeBrush TreeBrushOf(ContentsSceneNode brush)
		{
			var index = nodes.IndexOf(brush);
			return index < 0 ? default : brushes[index];
		}

		// How many of the captured triangles this brush drew.
		public int TriangleCountOf(ContentsSceneNode brush)
		{
			var count = 0;
			foreach (var triangle in triangles)
			{
				if (NodeOf(triangle.brushID) == brush)
					count++;
			}
			return count;
		}

		// Leave the editor with no pending CSG work, so that the flush this harness runs later sees only
		// its own tree. Refuses when a model is in the scene, because draining would take its meshes.
		public static void PrepareForUpdate()
		{
			if (CountForeignDirtyTrees(default) == 0)
				return;
			if (AnyChiselModelInScene())
			{
				throw new InvalidOperationException(
					$"A Chisel model in the open scene is waiting for an update ({ForeignDirtyTreeOwners(default)}). Run this suite in a scene " +
					"with no model, opened AFTER the last recompile - a domain reload restores the previous " +
					"scene, so an empty scene opened before a compile does not survive it.");
			}
			DrainForeignUpdates();
		}

		// Is a Chisel model open in the scene? Found by type name, so this test assembly does not have
		// to depend on Chisel.Components.
		public static bool AnyChiselModelInScene()
		{
			var behaviours = UnityEngine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include);
			foreach (var behaviour in behaviours)
			{
				if (behaviour == null)
					continue;
				for (var type = behaviour.GetType(); type != null; type = type.BaseType)
				{
					if (type.Name == "ChiselModelComponent")
						return true;
				}
			}
			return false;
		}

		public static void DrainForeignUpdates()
		{
			CompactHierarchyManager.Flush((tree, meshUpdates, dependencies) =>
			{
				dependencies.Complete();
				if (meshUpdates.meshDataArray.Length > 0)
					meshUpdates.meshDataArray.Dispose();
				return 0;
			});
		}

		static string ForeignDirtyTreeOwners(CSGTree mine)
		{
			var all = new NativeList<CSGTree>(Allocator.Temp);
			try
			{
				CompactHierarchyManager.GetAllTrees(all);
				var names = new System.Text.StringBuilder();
				for (int i = 0; i < all.Length; i++)
				{
					var other = all[i];
					if (!other.Valid || other == mine || !other.IsStatusFlagSet(NodeStatusFlags.TreeNeedsUpdate))
						continue;
					var owner = UnityEngine.Resources.EntityIdToObject(other.EntityId);
					if (names.Length > 0)
						names.Append(", ");
					names.Append(owner != null ? $"'{owner.name}'" : "a tree without an owner").Append($" with {other.Count} children");
				}
				return names.ToString();
			}
			finally
			{
				all.Dispose();
			}
		}

		public static int CountForeignDirtyTrees(CSGTree mine)
		{
			var all = new NativeList<CSGTree>(Allocator.Temp);
			try
			{
				CompactHierarchyManager.GetAllTrees(all);
				var count = 0;
				for (int i = 0; i < all.Length; i++)
				{
					var other = all[i];
					if (!other.Valid || other == mine)
						continue;
					if (other.IsStatusFlagSet(NodeStatusFlags.TreeNeedsUpdate))
						count++;
				}
				return count;
			}
			finally
			{
				all.Dispose();
			}
		}

		public void Dispose()
		{
			foreach (var pair in treeNodes)
			{
				if (pair.Key.IsBrush)
					DestroyTreeNode(pair.Key, pair.Value);
			}
			foreach (var pair in treeNodes)
			{
				if (!pair.Key.IsBrush)
					DestroyTreeNode(pair.Key, pair.Value);
			}
			treeNodes.Clear();
			if (tree.Valid)
				tree.Destroy();
			foreach (var instance in instances)
				instance.Destroy();
			instances.Clear();
			nodes.Clear();
			brushes.Clear();
			triangles.Clear();
			if (capturing)
			{
				capturing = false;
				ExactCSGCapture.Release();
			}
		}
	}
}
