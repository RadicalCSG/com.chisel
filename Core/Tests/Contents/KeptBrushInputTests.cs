using System.Collections.Generic;
using NUnit.Framework;
using Unity.Mathematics;

namespace Chisel.Core.Tests
{
	// Each brush's mesh (FillBrushMeshBlobLookupJob) and exact bounds (ExactBoundsJob) are kept from one update to the next,
	// and taken again only for the brushes that changed: what is kept must be what taking it again gives
	[TestFixture]
	[Category("Contents")]
	public class KeptBrushInputTests
	{
		const int kWater = 3;

		static ContentsSceneNode Box(float3 min, float3 max, string name, CSGOperationType operation = CSGOperationType.Additive, int contents = 0)
			=> ContentsSceneNode.Brush(ContentsScene.BoxPlanes(min, max), contents, operation, name: name);

		// A floor with posts on it, walls, a hole with water in it, and a composite; several posts share a mesh
		static ContentsScene Scene()
		{
			var scene = new ContentsScene().WithContentsList("Solid", "Glass", "Grate", "Water");
			scene.Add(Box(new float3(-1, -1, -1), new float3(9, 0, 5), "floor"));
			for (int x = 0; x < 4; x++)
				for (int z = 0; z < 2; z++)
					scene.Add(Box(new float3(x * 2, 0, z * 2), new float3(x * 2 + 1.5f, 2, z * 2 + 2), $"post {x} {z}"));
			scene.Add(Box(new float3(-1, 0, -1), new float3(9, 3, -0.5f), "wall south"));
			scene.Add(Box(new float3(8.5f, 0, -1), new float3(9, 3, 5), "wall east"));
			scene.Add(Box(new float3(3, -0.5f, 1), new float3(5, 0.5f, 3), "hole", CSGOperationType.Subtractive));
			scene.Add(Box(new float3(3, -0.5f, 1), new float3(5, -0.25f, 3), "water", contents: kWater));
			scene.Add(ContentsSceneNode.Composite(CSGOperationType.Additive,
				Box(new float3(1, 1, 3.5f), new float3(4, 2.5f, 4.5f), "block"),
				Box(new float3(2, 0.5f, 3), new float3(6, 2, 5), "keeps", CSGOperationType.Intersecting)));
			return scene;
		}

		static int Stat(ExactCSGStat stat) => CompactHierarchyManager.TreeUpdate.s_LastExactCSGStats[(int)stat];

		static void Update(ContentsTreeHarness harness, string what)
		{
			// an edit that changes nothing (a brush moved to where it is) runs no update
			harness.Update();
			AssertKeptInputIsCurrent(harness, what);
		}

		// Every brush's kept mesh is the one its mesh id is registered with now, and its kept bounds are its exact brush's
		static void AssertKeptInputIsCurrent(ContentsTreeHarness harness, string what)
		{
			var data     = ChiselTreeLookup.Value[harness.Tree];
			var registry = ChiselMeshLookup.Value.brushMeshBlobCache;
			int count    = data.brushIDValues.Length;
			Assert.That(data.brushMeshLookupCache.Length, Is.GreaterThanOrEqualTo(count), $"{what}: kept meshes");
			Assert.That(data.exactBoundsCache.Length, Is.GreaterThanOrEqualTo(count), $"{what}: kept bounds");
			for (int k = 0; k < count; k++)
			{
				var brushID = data.brushIDValues[k];
				ref var hierarchy = ref CompactHierarchyManager.GetHierarchy(brushID);
				var meshID = hierarchy.GetBrushMeshID(brushID);
				var expected = registry.TryGetValue(meshID, out var registered) ? registered.brushMeshBlob : default;
				var kept = data.brushMeshLookupCache[k];
				Assert.That(kept.blob == expected, Is.True, $"{what}: brush {k} kept another mesh than its id {meshID} has");
				Assert.That(kept.polygonCount, Is.EqualTo(expected.IsCreated ? expected.Value.polygons.Length : 0), $"{what}: brush {k}'s polygon count");

				var exact = data.exactBrushCache[k];
				Assert.That(exact.IsCreated, Is.True, $"{what}: brush {k} has no exact brush");
				var bounds = data.exactBoundsCache[k];
				Assert.That(math.all(math.asint(bounds.Min) == math.asint(exact.Value.bounds.Min)) &&
							math.all(math.asint(bounds.Max) == math.asint(exact.Value.bounds.Max)), Is.True,
							$"{what}: brush {k} kept bounds {bounds.Min} {bounds.Max}, its exact brush has {exact.Value.bounds.Min} {exact.Value.bounds.Max}");
			}
		}

		static ContentsSceneNode Named(ContentsTreeHarness harness, string name)
		{
			var found = Find(harness.Scene.roots, name);
			Assert.That(found, Is.Not.Null, name);
			return found;
		}

		static ContentsSceneNode Find(List<ContentsSceneNode> nodes, string name)
		{
			foreach (var node in nodes)
			{
				if (node.name == name)
					return node;
				if (node.children != null && Find(node.children, name) is ContentsSceneNode child)
					return child;
			}
			return null;
		}

		static void Shift(ContentsTreeHarness harness, string name, float3 offset)
		{
			var node = Named(harness, name);
			harness.SetLocalToTree(node, math.mul(float4x4.Translate(offset), node.localToTree));
		}

		static ContentsTreeHarness Start()
		{
			var harness = ContentsTreeHarness.Build(Scene(), captureExact: false);
			Update(harness, "first update");
			return harness;
		}

		[Test]
		public void MovingABrush_TakesTheMeshAndBoundsOfOnlyTheBrushesThatChanged()
		{
			using var harness = Start();
			int brushCount = ChiselTreeLookup.Value[harness.Tree].brushIDValues.Length;
			Shift(harness, "post 1 0", new float3(0, 0.25f, 0));
			Update(harness, "post 1 0 up");
			Assert.That(Stat(ExactCSGStat.MeshesLookedUp), Is.InRange(1, brushCount / 2), "meshes looked up");
			Assert.That(Stat(ExactCSGStat.BoundsTaken), Is.InRange(1, brushCount / 2), "bounds taken from the exact brushes");
		}

		[Test]
		public void BrushesMovedAndChanged_KeepWhatTakingItAgainGives()
		{
			using var harness = Start();
			Shift(harness, "post 1 0", new float3(0, 0.25f, 0));
			Update(harness, "post 1 0 up");
			harness.SetOperation(Named(harness, "post 2 0"), CSGOperationType.Subtractive);
			Update(harness, "post 2 0 made subtractive");
			harness.SetContents(Named(harness, "post 2 1"), kWater);
			Update(harness, "post 2 1 made water");
			harness.SetPlanes(Named(harness, "post 3 0"), ContentsScene.BoxPlanes(new float3(6, 0, 0), new float3(7.5f, 2.5f, 2)));
			Update(harness, "post 3 0 made taller");
		}

		// A brush given another mesh and its own back between two updates: the mesh it has now is registered again
		[Test]
		public void AMeshChangedAndBackBetweenUpdates_IsLookedUpAgain()
		{
			using var harness = Start();
			var post  = Named(harness, "post 3 1");
			var own   = post.localPlanes;
			harness.SetPlanes(post, ContentsScene.BoxPlanes(new float3(6, 0, 2), new float3(7.5f, 2.5f, 4)));
			harness.SetPlanes(post, own);
			Update(harness, "post 3 1 given another mesh and its own back");
			harness.SetPlanes(post, ContentsScene.BoxPlanes(new float3(6, 0, 2), new float3(7.5f, 2.5f, 4)));
			Update(harness, "post 3 1 made taller");
			harness.SetPlanes(post, own);
			Update(harness, "post 3 1 back");
		}

		[Test]
		public void BrushesReorderedRemovedAndAdded_KeepWhatTakingItAgainGives()
		{
			using var harness = Start();
			harness.Move(Named(harness, "post 1 0"), null, harness.Scene.roots.Count - 1);
			Update(harness, "post 1 0 moved last");
			harness.Move(Named(harness, "hole"), null, 0);
			Update(harness, "hole moved first");
			harness.Remove(Named(harness, "post 2 1"), destroy: true);
			Update(harness, "post 2 1 removed");
			harness.Insert(null, 3, Box(new float3(2.5f, 0, 1), new float3(4.5f, 1, 2.5f), "added"));
			Update(harness, "a box added");
			harness.Insert(null, harness.Scene.roots.Count, Box(new float3(0, 2, 0), new float3(1.5f, 2.5f, 2), "added last"));
			Update(harness, "a box added last");
			Shift(harness, "added last", new float3(0, 0, 0.25f));
			Update(harness, "the box added last moved");
		}

		[Test]
		public void RandomEdits_KeepWhatTakingItAgainGives([Values(1, 2, 3)] int seed)
		{
			var random = new Unity.Mathematics.Random((uint)seed * 7919u);
			using var harness = Start();
			var names = new List<string>();
			foreach (var root in harness.Scene.roots)
				if (root.IsBrush)
					names.Add(root.name);
			for (int step = 0; step < 24; step++)
			{
				var name = names[random.NextInt(names.Count)];
				var node = Named(harness, name);
				switch (random.NextInt(5))
				{
					case 0:
					case 1:
					{
						var offset = random.NextInt(3) switch { 0 => new float3(0.25f, 0, 0), 1 => new float3(0, -0.25f, 0), _ => new float3(0, 0, 0.5f) };
						if (random.NextBool()) offset = -offset;
						Shift(harness, name, offset);
						Update(harness, $"step {step}: {name} moved by {offset}");
						break;
					}
					case 2:
					{
						var min = node.localPlanes == null ? float3.zero : new float3(random.NextInt(8), 0, random.NextInt(4));
						harness.SetPlanes(node, ContentsScene.BoxPlanes(min, min + new float3(1, 1 + random.NextInt(3), 1)));
						Update(harness, $"step {step}: {name} given another mesh");
						break;
					}
					case 3:
					{
						int index = random.NextInt(harness.Scene.roots.Count);
						harness.Move(node, null, index);
						Update(harness, $"step {step}: {name} moved to {index}");
						break;
					}
					default:
					{
						var operation = (CSGOperationType)random.NextInt(3);
						harness.SetOperation(node, operation);
						Update(harness, $"step {step}: {name} made {operation}");
						break;
					}
				}
			}
		}
	}
}
