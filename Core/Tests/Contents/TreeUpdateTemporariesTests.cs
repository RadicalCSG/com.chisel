using System.Collections.Generic;
using NUnit.Framework;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEngine;

namespace Chisel.Core.Tests
{
	// The containers a tree's update works in are kept with the tree for its next update, and freed with the tree
	[TestFixture]
	[Category("Contents")]
	public class TreeUpdateTemporariesTests
	{
		const int kWater = 3;

		static ContentsSceneNode Box(float3 min, float3 max, string name, CSGOperationType operation = CSGOperationType.Additive, int contents = 0)
			=> ContentsSceneNode.Brush(ContentsScene.BoxPlanes(min, max), contents, operation, name: name);

		static ContentsScene Scene()
		{
			var scene = new ContentsScene().WithContentsList("Solid", "Glass", "Grate", "Water");
			scene.Add(Box(new float3(-1, -1, -1), new float3(9, 0, 5), "floor"));
			for (int x = 0; x < 4; x++)
				for (int z = 0; z < 2; z++)
					scene.Add(Box(new float3(x * 2, 0, z * 2), new float3(x * 2 + 1.5f, 2, z * 2 + 2), $"post {x} {z}"));
			scene.Add(Box(new float3(-1, 0, -1), new float3(9, 3, -0.5f), "wall south"));
			scene.Add(Box(new float3(3, -0.5f, 1), new float3(5, 0.5f, 3), "hole", CSGOperationType.Subtractive));
			scene.Add(Box(new float3(3, -0.5f, 1), new float3(5, -0.25f, 3), "water", contents: kWater));
			return scene;
		}

		static ContentsSceneNode Named(ContentsTreeHarness harness, string name)
		{
			foreach (var node in harness.Scene.roots)
				if (node.name == name)
					return node;
			Assert.Fail(name);
			return null;
		}

		static void Shift(ContentsTreeHarness harness, string name, float3 offset)
		{
			var node = Named(harness, name);
			harness.SetLocalToTree(node, math.mul(float4x4.Translate(offset), node.localToTree));
		}

		static unsafe long Of<T>(NativeList<T> list) where T : unmanaged
			=> list.IsCreated ? (long)list.GetUnsafeList() : 0;

		static unsafe long Of<T>(NativeArray<T> array) where T : struct
			=> array.IsCreated ? (long)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(array) : 0;

		// where some of the tree's kept containers are: lists by their list (which stays where it is as it grows),
		// arrays by their memory
		static Dictionary<string, long> Containers(ContentsTreeHarness harness)
		{
			var t = ChiselTreeLookup.Value[harness.Tree].updateTemporaries;
			return new Dictionary<string, long>
			{
				{ "brushes",                    Of(t.brushes) },
				{ "nodes",                      Of(t.nodes) },
				{ "allTreeBrushIndexOrders",    Of(t.allTreeBrushIndexOrders) },
				{ "brushRenderData",            Of(t.brushRenderData) },
				{ "brushIntersectionsWith",     Of(t.brushIntersectionsWith) },
				{ "allBrushMeshIDs",            Of(t.allBrushMeshIDs) },
				{ "brushMeshLookup",            Of(t.brushMeshLookup) },
				{ "brushBrushIntersections",    Of(t.brushBrushIntersections) },
				{ "exactCSGStats",              Of(t.exactCSGStats) },
				{ "mergedPlanes",               Of(t.mergedPlanes) },
				{ "basePolygonDisposeList",     Of(t.basePolygonDisposeList) },
				{ "exactBrushDisposeList",      Of(t.exactBrushDisposeList) },
				{ "subMeshSurfaces",            Of(t.subMeshSurfaces) },
				{ "meshUpdatesRenderables",     Of(t.meshUpdatesRenderables) },
				{ "meshCleanup",                Of(t.meshCleanup) },
				{ "meshCleanupOutlines",        Of(t.meshCleanupOutlines) },
				{ "contentsVolumeNodes",        Of(t.contentsVolumeNodes) },
				{ "weld.work",                  Of(t.weld.work) },
				{ "hotspotCut.groups",          Of(t.hotspotCut.groups) },
				{ "hotspotFind.candidates",     Of(t.hotspotFind.candidates) },
			};
		}

		[Test]
		public void ASecondUpdate_WorksInTheContainersOfTheFirst()
		{
			using var harness = ContentsTreeHarness.Build(Scene(), captureExact: false);
			harness.Update();
			var first = Containers(harness);
			foreach (var container in first)
				Assert.That(container.Value, Is.Not.Zero, $"{container.Key} is not kept after the first update");

			Shift(harness, "post 1 0", new float3(0, 0.25f, 0));
			harness.Update();
			var second = Containers(harness);
			foreach (var container in first)
				Assert.That(second[container.Key], Is.EqualTo(container.Value), $"the second update made {container.Key} again");
		}

		// Edits that leave fewer brushes, then more, then move one: after each, the tree gives what a tree made of the same
		// brushes gives at its first update
		[Test]
		public void EditsThatRemoveAndAddBrushes_GiveWhatAFreshTreeGives()
		{
			using var harness = ContentsTreeHarness.Build(Scene(), captureExact: false);
			harness.Update();
			AssertSameAsFresh(harness, "first update");

			harness.Remove(Named(harness, "post 0 0"), destroy: true);
			harness.Remove(Named(harness, "post 2 1"), destroy: true);
			harness.Remove(Named(harness, "wall south"), destroy: true);
			harness.Update();
			AssertSameAsFresh(harness, "three brushes removed");

			for (int i = 0; i < 5; i++)
				harness.Insert(null, harness.Scene.roots.Count, Box(new float3(i * 1.5f, 2, 0), new float3(i * 1.5f + 1, 2.5f, 1), $"added {i}"));
			harness.Update();
			AssertSameAsFresh(harness, "five brushes added");

			Shift(harness, "added 2", new float3(0, 0, 0.5f));
			harness.Update();
			AssertSameAsFresh(harness, "an added brush moved");

			harness.Remove(Named(harness, "added 0"), destroy: true);
			harness.Remove(Named(harness, "hole"), destroy: true);
			harness.Update();
			AssertSameAsFresh(harness, "two more removed");
		}

		static void AssertSameAsFresh(ContentsTreeHarness harness, string what)
		{
			var edited = new List<ContentsTreeHarness.CapturedTriangle>(harness.Triangles);
			using var fresh = ContentsTreeHarness.Build(harness.Scene, captureExact: false);
			fresh.Update();
			var built = new List<ContentsTreeHarness.CapturedTriangle>(fresh.Triangles);
			Assert.That(edited.Count, Is.EqualTo(built.Count), $"{what}: triangle count");
			// a tree keeps its sections from one update to the next, so they, and the order of the triangles in the meshes,
			// can be other than a fresh tree's: the same triangles, in any order
			edited.Sort((p, q) => string.CompareOrdinal(Key(p), Key(q)));
			built.Sort((p, q) => string.CompareOrdinal(Key(p), Key(q)));
			for (int i = 0; i < built.Count; i++)
				Assert.That(Same(edited[i], built[i]), Is.True, $"{what}: triangle {i} differs");
		}

		static string Key(ContentsTreeHarness.CapturedTriangle t)
			=> $"{t.a}{t.b}{t.c}{t.material}{t.uvA}{t.uvB}{t.uvC}{t.lightmapA}{t.lightmapB}{t.lightmapC}{t.normalA}{t.normalB}{t.normalC}";

		// Everything but the brush's node id, which is another in each tree, and the section's number
		static bool Same(ContentsTreeHarness.CapturedTriangle p, ContentsTreeHarness.CapturedTriangle q)
		{
			return math.all(p.a == q.a) && math.all(p.b == q.b) && math.all(p.c == q.c) &&
				   math.all(p.surfaceNormal == q.surfaceNormal) && p.material == q.material &&
				   math.all(p.uvA == q.uvA) && math.all(p.uvB == q.uvB) && math.all(p.uvC == q.uvC) &&
				   math.all(p.lightmapA == q.lightmapA) && math.all(p.lightmapB == q.lightmapB) && math.all(p.lightmapC == q.lightmapC) &&
				   math.all(p.normalA == q.normalA) && math.all(p.normalB == q.normalB) && math.all(p.normalC == q.normalC) &&
				   math.all(p.tangentA == q.tangentA) && math.all(p.tangentB == q.tangentB) && math.all(p.tangentC == q.tangentC);
		}

		// Trees updated a few times and destroyed: what their updates kept goes with them
		[Test]
		public void TreesUpdatedAndDestroyed_LeaveNothingAllocated()
		{
			BrushMeshFactory.CreateBox(new Vector3(0, 0, 0), new Vector3(1.25f, 1.5f, 1.75f), out var box);
			var surfaceArray = new ChiselSurfaceArray();
			surfaceArray.EnsureSize(box.polygons.Length);
			for (int i = 0; i < surfaceArray.surfaces.Length; i++)
				surfaceArray.surfaces[i] = new ChiselSurface();
			var instance = BrushMeshInstance.Create(box, surfaceArray);
			try
			{
				void Round()
				{
					var tree  = CSGTree.Create(default(UnityEngine.EntityId));
					var a = tree.CreateBrush(brushMesh: instance);
					var b = tree.CreateBrush(brushMesh: instance);
					b.LocalTransformation = float4x4.Translate(new float3(0.5f, 0, 0));
					try
					{
						CompactHierarchyManager.Flush(null);
						b.LocalTransformation = float4x4.Translate(new float3(0.75f, 0.25f, 0));
						CompactHierarchyManager.Flush(null);
						var c = tree.CreateBrush(brushMesh: instance);
						c.LocalTransformation = float4x4.Translate(new float3(0, 1, 0));
						CompactHierarchyManager.Flush(null);
						c.Destroy();
					}
					finally
					{
						a.Destroy(); b.Destroy();
						tree.Destroy();
						CompactHierarchyManager.Flush(null);
					}
				}

				// the caches that grow once are made first
				Round();
				Round();
				UnsafeUtility.CheckForLeaks();
				Round();
				Assert.That(UnsafeUtility.CheckForLeaks(), Is.Zero, "a destroyed tree left allocations behind");
			}
			finally
			{
				instance.Destroy();
			}
		}
	}
}
