using System;
using System.Collections.Generic;
using NUnit.Framework;
using Unity.Mathematics;

namespace Chisel.Core.Tests
{
	// A brush built again only because a brush it touches changed keeps its routing table when the brushes it touches, how
	// it touches them and the tree are the same as when the table was built (CreateRoutingTableJob): what it keeps must be
	// what building the table gives
	[TestFixture]
	[Category("Contents")]
	public class RoutingTableKeepTests
	{
		const int kWater = 3;

		static ContentsSceneNode Box(float3 min, float3 max, string name, CSGOperationType operation = CSGOperationType.Additive, int contents = 0)
			=> ContentsSceneNode.Brush(ContentsScene.BoxPlanes(min, max), contents, operation, name: name);

		// A floor with posts standing on it and touching each other, walls along two sides, a hole cut in the floor, water in
		// it, a composite that keeps the part of a block inside another one, and one whose intersecting brush is away from
		// the shelf it would keep, so it keeps none of it
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
			scene.Add(ContentsSceneNode.Composite(CSGOperationType.Additive,
				Box(new float3(5, 1.5f, 0), new float3(8, 1.75f, 4), "shelf"),
				Box(new float3(5, 4, 0), new float3(8, 5, 4), "away", CSGOperationType.Intersecting)));
			return scene;
		}

		// A floor with a row of boxes on it, each cutting into the next, the last in a composite, and a small box sticking
		// out of a big one
		static ContentsScene Row()
		{
			var scene = new ContentsScene().WithContentsList("Solid", "Glass", "Grate", "Water");
			scene.Add(Box(new float3(-1, -1, -1), new float3(9, 0, 5), "floor"));
			scene.Add(Box(new float3(0, 0, 0), new float3(1.5f, 2, 2), "a"));
			scene.Add(Box(new float3(1, 0, 0), new float3(3, 2, 2), "b"));
			scene.Add(Box(new float3(2.5f, 0, 0), new float3(4, 2, 2), "c"));
			var group = ContentsSceneNode.Composite(CSGOperationType.Additive, Box(new float3(3.5f, 0, 0), new float3(4.5f, 2, 2), "d"));
			group.name = "group";
			scene.Add(group);
			scene.Add(Box(new float3(5, 0, 0), new float3(8, 3, 3), "big"));
			scene.Add(Box(new float3(7.5f, 1, 1), new float3(8.5f, 2, 2), "small"));
			return scene;
		}

		static int Stat(ExactCSGStat stat) => CompactHierarchyManager.TreeUpdate.s_LastExactCSGStats[(int)stat];

		// The same scene twice: one tree updated with routing tables kept (and every kept table built as well and compared,
		// which counts RoutingTableMismatch, a failure the harness throws on), one with every table built
		sealed class Pair : IDisposable
		{
			public ContentsTreeHarness kept;
			public ContentsTreeHarness built;
			public int keptTables;

			// One after the other: an update builds every tree that changed, and a harness only sees its own tree's meshes
			public Pair(Func<ContentsScene> scene = null)
			{
				scene ??= Scene;
				try
				{
					kept = ContentsTreeHarness.Build(scene(), captureExact: false);
					Update(kept, true, "first update");
					built = ContentsTreeHarness.Build(scene(), captureExact: false);
					Update(built, false, "first update");
					AssertSameTriangles("first update");
				}
				catch
				{
					Dispose();
					throw;
				}
			}

			// Makes the same edit to both and updates them, each on its own
			public void Edit(Action<ContentsTreeHarness> edit, string what)
			{
				edit(kept);
				Update(kept, true, what);
				edit(built);
				Update(built, false, what);
				AssertSameTriangles(what);
			}

			public void Update(ContentsTreeHarness harness, bool keep, string what)
			{
				var keepTables = CompactHierarchyManager.KeepRoutingTables;
				var verify     = CompactHierarchyManager.VerifyKeptRoutingTables;
				try
				{
					CompactHierarchyManager.KeepRoutingTables       = keep;
					CompactHierarchyManager.VerifyKeptRoutingTables = keep;
					harness.Update();
					if (keep)
						keptTables += Stat(ExactCSGStat.RoutingTablesKept);
					else
						Assert.That(Stat(ExactCSGStat.RoutingTablesKept), Is.Zero, $"{what}: kept while switched off");
				}
				finally
				{
					CompactHierarchyManager.KeepRoutingTables       = keepTables;
					CompactHierarchyManager.VerifyKeptRoutingTables = verify;
				}
			}

			public void AssertSameTriangles(string what)
			{
				var a = kept.Triangles;
				var b = built.Triangles;
				Assert.That(a.Count, Is.EqualTo(b.Count), $"{what}: triangle count");
				for (int i = 0; i < a.Count; i++)
					Assert.That(Same(a[i], b[i]), Is.True, $"{what}: triangle {i} differs");
			}

			public void Dispose()
			{
				kept?.Dispose();
				built?.Dispose();
			}
		}

		// Everything but the brush's node id, which is another in each tree
		static bool Same(ContentsTreeHarness.CapturedTriangle p, ContentsTreeHarness.CapturedTriangle q)
		{
			return math.all(p.a == q.a) && math.all(p.b == q.b) && math.all(p.c == q.c) &&
				   math.all(p.surfaceNormal == q.surfaceNormal) && p.material == q.material &&
				   math.all(p.uvA == q.uvA) && math.all(p.uvB == q.uvB) && math.all(p.uvC == q.uvC) &&
				   math.all(p.lightmapA == q.lightmapA) && math.all(p.lightmapB == q.lightmapB) && math.all(p.lightmapC == q.lightmapC) &&
				   math.all(p.normalA == q.normalA) && math.all(p.normalB == q.normalB) && math.all(p.normalC == q.normalC) &&
				   math.all(p.tangentA == q.tangentA) && math.all(p.tangentB == q.tangentB) && math.all(p.tangentC == q.tangentC) &&
				   p.section == q.section && p.selectedEntityID == q.selectedEntityID;
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

		[Test]
		public void MovingAPost_KeepsTheTablesOfTheBrushesAroundIt()
		{
			using var pair = new Pair();
			pair.keptTables = 0;
			pair.Edit(h => Shift(h, "post 1 0", new float3(0, 0.25f, 0)), "post 1 0 up");
			Assert.That(pair.keptTables, Is.GreaterThan(0), "no table of the posts and floor around it was kept");
		}

		[Test]
		public void APostMovedThroughItsNeighbourAndBack_GivesWhatBuildingGives()
		{
			using var pair = new Pair();
			for (int step = 0; step < 8; step++)
				pair.Edit(h => Shift(h, "post 1 0", new float3(0.25f, 0, 0)), $"step {step} east");
			for (int step = 0; step < 8; step++)
				pair.Edit(h => Shift(h, "post 1 0", new float3(-0.25f, 0, 0)), $"step {step} back");
			Assert.That(pair.keptTables, Is.GreaterThan(0));
		}

		// The intersecting brush of a composite decides what is left of the brushes before it, touching them or not
		[Test]
		public void AnIntersectingBrushMovedOntoTheShelfAndAway_GivesWhatBuildingGives()
		{
			using var pair = new Pair();
			pair.Edit(h => Shift(h, "away", new float3(0, -3, 0)), "away moved onto the shelf");
			pair.Edit(h => Shift(h, "post 3 0", new float3(0, 0.25f, 0)), "post 3 0 up");
			pair.Edit(h => Shift(h, "away", new float3(0, 3, 0)), "away moved off the shelf");
			pair.Edit(h => Shift(h, "post 3 0", new float3(0, -0.25f, 0)), "post 3 0 down");
		}

		// The brushes around one whose operation or contents changed touch it as they did, in a tree that is otherwise the same
		[TestCase(CSGOperationType.Subtractive)]
		[TestCase(CSGOperationType.Intersecting)]
		public void AnOperationChangedInARow_GivesWhatBuildingGives(CSGOperationType operation)
		{
			using var pair = new Pair(Row);
			pair.Edit(h => Shift(h, "a", new float3(0, 0.25f, 0)), "a up");
			pair.Edit(h => h.SetOperation(Named(h, "b"), operation), $"b made {operation}");
			pair.Edit(h => Shift(h, "c", new float3(0, 0.25f, 0)), "c up");
		}

		// Only the composite changed: the brushes around the one in it touch what they touched, in a tree whose structure
		// differs only in the composite's operation
		[TestCase(CSGOperationType.Subtractive)]
		[TestCase(CSGOperationType.Intersecting)]
		[TestCase(CSGOperationType.Copy)]
		public void ACompositesOperationChanged_GivesWhatBuildingGives(CSGOperationType operation)
		{
			using var pair = new Pair(Row);
			pair.Edit(h => Shift(h, "a", new float3(0, 0.25f, 0)), "a up");
			pair.Edit(h => h.SetOperation(Named(h, "group"), operation), $"group made {operation}");
			pair.Edit(h => Shift(h, "a", new float3(0, -0.25f, 0)), "a down");
		}

		[Test]
		public void ContentsChangedInARow_GiveWhatBuildingGives()
		{
			using var pair = new Pair(Row);
			pair.Edit(h => Shift(h, "a", new float3(0, 0.25f, 0)), "a up");
			pair.Edit(h => h.SetContents(Named(h, "b"), kWater), "b made water");
			pair.Edit(h => h.SetContents(Named(h, "a"), kWater), "a made water");
			pair.Edit(h => Shift(h, "c", new float3(0, 0.25f, 0)), "c up");
		}

		// The big box touches the small one all along, first cut into by it, then with it wholly inside
		[Test]
		public void ABoxMovedWhollyInsideAnotherAndOut_GivesWhatBuildingGives()
		{
			using var pair = new Pair(Row);
			pair.keptTables = 0;
			pair.Edit(h => Shift(h, "small", new float3(-1, 0, 0)), "small moved inside big");
			pair.Edit(h => Shift(h, "small", new float3(1, 0, 0)), "small moved out again");
			Assert.That(pair.keptTables, Is.GreaterThan(0), "no table was kept");
		}

		[TestCase("hole", CSGOperationType.Additive)]
		[TestCase("post 2 0", CSGOperationType.Subtractive)]
		[TestCase("post 2 0", CSGOperationType.Intersecting)]
		[TestCase("keeps", CSGOperationType.Additive)]
		[TestCase("away", CSGOperationType.Additive)]
		[TestCase("block", CSGOperationType.Subtractive)]
		public void AnOperationChanged_GivesWhatBuildingGives(string name, CSGOperationType operation)
		{
			using var pair = new Pair();
			pair.Edit(h => h.SetOperation(Named(h, name), operation), $"{name} made {operation}");
			pair.Edit(h => Shift(h, "post 1 1", new float3(0, 0.25f, 0)), "post 1 1 up");
		}

		[Test]
		public void ContentsChanged_GiveWhatBuildingGives()
		{
			using var pair = new Pair();
			pair.Edit(h => h.SetContents(Named(h, "water"), 0), "water made solid");
			pair.Edit(h => h.SetContents(Named(h, "post 2 1"), kWater), "post 2 1 made water");
			pair.Edit(h => Shift(h, "post 2 0", new float3(0, 0.25f, 0)), "post 2 0 up");
		}

		[Test]
		public void BrushesReorderedRemovedAndAdded_GiveWhatBuildingGives()
		{
			using var pair = new Pair();
			pair.Edit(h => h.Move(Named(h, "post 1 0"), null, h.Scene.roots.Count - 1), "post 1 0 moved last");
			pair.Edit(h => Shift(h, "post 1 1", new float3(0, 0.25f, 0)), "post 1 1 up");
			pair.Edit(h => h.Move(Named(h, "hole"), null, 0), "hole moved first");
			pair.Edit(h => h.Remove(Named(h, "post 2 1"), destroy: true), "post 2 1 removed");
			pair.Edit(h => h.Insert(null, 3, Box(new float3(2.5f, 0, 1), new float3(4.5f, 1, 2.5f), "added")), "a box added");
			pair.Edit(h => Shift(h, "added", new float3(0, 0, 0.25f)), "the added box moved");
		}

		[Test]
		public void RandomEdits_GiveWhatBuildingGives([Values(1, 2, 3)] int seed)
		{
			var random = new Unity.Mathematics.Random((uint)seed * 7919u);
			using var pair = new Pair();
			var names = new List<string>();
			foreach (var root in pair.kept.Scene.roots)
				if (root.IsBrush)
					names.Add(root.name);
			for (int step = 0; step < 24; step++)
			{
				var name = names[random.NextInt(names.Count)];
				switch (random.NextInt(5))
				{
					case 0:
					case 1:
					{
						var offset = random.NextInt(3) switch { 0 => new float3(0.25f, 0, 0), 1 => new float3(0, -0.25f, 0), _ => new float3(0, 0, 0.5f) };
						if (random.NextBool()) offset = -offset;
						pair.Edit(h => Shift(h, name, offset), $"step {step}: {name} moved by {offset}");
						break;
					}
					case 2:
					{
						var operation = (CSGOperationType)random.NextInt(3);
						pair.Edit(h => h.SetOperation(Named(h, name), operation), $"step {step}: {name} made {operation}");
						break;
					}
					case 3:
					{
						int index = random.NextInt(pair.kept.Scene.roots.Count);
						pair.Edit(h => h.Move(Named(h, name), null, index), $"step {step}: {name} moved to {index}");
						break;
					}
					default:
					{
						var contents = random.NextBool() ? kWater : 0;
						pair.Edit(h => h.SetContents(Named(h, name), contents), $"step {step}: {name} given contents {contents}");
						break;
					}
				}
			}
			Assert.That(pair.keptTables, Is.GreaterThan(0));
		}

		// Trees whose brushes kept their tables and built new ones: every table goes with the tree
		[Test]
		public void TreesThatKeptTheirTables_LeaveNothingAllocatedWhenDestroyed()
		{
			void Round()
			{
				var keepTables = CompactHierarchyManager.KeepRoutingTables;
				try
				{
					CompactHierarchyManager.KeepRoutingTables = true;
					using var harness = ContentsTreeHarness.Build(Scene(), captureExact: false);
					harness.Update();
					int kept = 0;
					for (int step = 0; step < 4; step++)
					{
						Shift(harness, step % 2 == 0 ? "post 1 0" : "away", new float3(0, step % 2 == 0 ? 0.25f : -3, 0));
						harness.Update();
						kept += Stat(ExactCSGStat.RoutingTablesKept);
					}
					Assert.That(kept, Is.GreaterThan(0), "no table was kept");
				}
				finally
				{
					CompactHierarchyManager.KeepRoutingTables = keepTables;
				}
			}

			// the caches that grow once are made first
			Round();
			Round();
			Unity.Collections.LowLevel.Unsafe.UnsafeUtility.CheckForLeaks();
			Round();
			Assert.That(Unity.Collections.LowLevel.Unsafe.UnsafeUtility.CheckForLeaks(), Is.Zero, "a destroyed tree left allocations behind");
		}
	}
}
