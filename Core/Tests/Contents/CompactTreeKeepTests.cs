using System;
using System.Collections.Generic;
using NUnit.Framework;
using Unity.Mathematics;

namespace Chisel.Core.Tests
{
	// An update keeps the last update's compact tree while the hierarchy's structure and the number of contents are the
	// same (BuildCompactTreeJob): the tree it keeps must be what building it gives
	[TestFixture]
	[Category("Contents")]
	public class CompactTreeKeepTests
	{
		const int kWater = 3;

		static ContentsSceneNode Box(float3 min, float3 max, string name, CSGOperationType operation = CSGOperationType.Additive, int contents = 0)
			=> ContentsSceneNode.Brush(ContentsScene.BoxPlanes(min, max), contents, operation, name: name);

		static ContentsSceneNode Composite(string name, CSGOperationType operation, params ContentsSceneNode[] children)
		{
			var composite = ContentsSceneNode.Composite(operation, children);
			composite.name = name;
			return composite;
		}

		// A floor with posts, a hole with water in it, a composite that keeps the part of a block inside another one, and an
		// empty composite to move brushes into
		static ContentsScene Scene()
		{
			var scene = new ContentsScene().WithContentsList("Solid", "Glass", "Grate", "Water");
			scene.Add(Box(new float3(-1, -1, -1), new float3(9, 0, 5), "floor"));
			for (int x = 0; x < 4; x++)
				scene.Add(Box(new float3(x * 2, 0, 0), new float3(x * 2 + 1.5f, 2, 2), $"post {x}"));
			scene.Add(Box(new float3(3, -0.5f, 1), new float3(5, 0.5f, 3), "hole", CSGOperationType.Subtractive));
			scene.Add(Box(new float3(3, -0.5f, 1), new float3(5, -0.25f, 3), "water", contents: kWater));
			scene.Add(Composite("keeper", CSGOperationType.Additive,
				Box(new float3(1, 1, 3.5f), new float3(4, 2.5f, 4.5f), "block"),
				Box(new float3(2, 0.5f, 3), new float3(6, 2, 5), "keeps", CSGOperationType.Intersecting)));
			scene.Add(Composite("group", CSGOperationType.Additive));
			return scene;
		}

		static int Stat(ExactCSGStat stat) => CompactHierarchyManager.TreeUpdate.s_LastExactCSGStats[(int)stat];

		// The same scene twice: one tree updated with its compact tree kept (and built as well and compared, which counts
		// CompactTreeMismatch, a failure the harness throws on), one with the tree built every update
		sealed class Pair : IDisposable
		{
			public ContentsTreeHarness kept;
			public ContentsTreeHarness built;

			public Pair()
			{
				try
				{
					kept = ContentsTreeHarness.Build(Scene(), captureExact: false);
					Update(kept, true, "first update");
					built = ContentsTreeHarness.Build(Scene(), captureExact: false);
					Update(built, false, "first update");
					AssertSameTriangles("first update");
				}
				catch
				{
					Dispose();
					throw;
				}
			}

			// Makes the same edit to both and updates them, each on its own; whether the kept one kept its tree
			public bool Edit(Action<ContentsTreeHarness> edit, string what)
			{
				edit(kept);
				bool keptTree = Update(kept, true, what);
				edit(built);
				Update(built, false, what);
				AssertSameTriangles(what);
				return keptTree;
			}

			static bool Update(ContentsTreeHarness harness, bool keep, string what)
			{
				var keepTree = CompactHierarchyManager.KeepCompactTree;
				var verify   = CompactHierarchyManager.VerifyKeptCompactTree;
				try
				{
					CompactHierarchyManager.KeepCompactTree       = keep;
					CompactHierarchyManager.VerifyKeptCompactTree = keep;
					harness.Update();
					if (!keep)
						Assert.That(Stat(ExactCSGStat.CompactTreesKept), Is.Zero, $"{what}: kept while switched off");
					return Stat(ExactCSGStat.CompactTreesKept) > 0;
				}
				finally
				{
					CompactHierarchyManager.KeepCompactTree       = keepTree;
					CompactHierarchyManager.VerifyKeptCompactTree = verify;
				}
			}

			void AssertSameTriangles(string what)
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

		static void Moved(Pair pair, string what)
		{
			Assert.That(pair.Edit(h => Shift(h, "post 1", new float3(0, 0.25f, 0)), $"{what}, then post 1 up"), Is.True, $"{what}, then post 1 up: the tree was not kept");
			Assert.That(pair.Edit(h => Shift(h, "post 1", new float3(0, -0.25f, 0)), $"{what}, then post 1 down"), Is.True, $"{what}, then post 1 down: the tree was not kept");
		}

		[Test]
		public void MovingBrushes_KeepsTheTree()
		{
			using var pair = new Pair();
			Moved(pair, "nothing changed");
			Assert.That(pair.Edit(h => Shift(h, "block", new float3(0.25f, 0, 0)), "block moved"), Is.True, "block moved: the tree was not kept");
		}

		[TestCase("hole", CSGOperationType.Additive)]
		[TestCase("post 2", CSGOperationType.Intersecting)]
		[TestCase("keeps", CSGOperationType.Subtractive)]
		[TestCase("keeper", CSGOperationType.Subtractive)]
		[TestCase("keeper", CSGOperationType.Copy)]
		public void AnOperationChanged_BuildsTheTreeAgain(string name, CSGOperationType operation)
		{
			using var pair = new Pair();
			Moved(pair, "before");
			Assert.That(pair.Edit(h => h.SetOperation(Named(h, name), operation), $"{name} made {operation}"), Is.False, $"{name} made {operation}: the tree was kept");
			Moved(pair, $"{name} made {operation}");
		}

		[Test]
		public void ContentsChanged_BuildsTheTreeAgain()
		{
			using var pair = new Pair();
			Assert.That(pair.Edit(h => h.SetContents(Named(h, "post 2"), kWater), "post 2 made water"), Is.False, "post 2 made water: the tree was kept");
			Moved(pair, "post 2 made water");
			Assert.That(pair.Edit(h => h.SetContents(Named(h, "water"), 0), "water made solid"), Is.False, "water made solid: the tree was kept");
		}

		[Test]
		public void BrushesReordered_BuildsTheTreeAgain()
		{
			using var pair = new Pair();
			Assert.That(pair.Edit(h => h.Move(Named(h, "hole"), null, 0), "hole moved first"), Is.False, "hole moved first: the tree was kept");
			Moved(pair, "hole moved first");
			Assert.That(pair.Edit(h => h.Move(Named(h, "post 3"), null, h.Scene.roots.Count - 1), "post 3 moved last"), Is.False, "post 3 moved last: the tree was kept");
		}

		[Test]
		public void BrushesMovedIntoAndOutOfComposites_BuildsTheTreeAgain()
		{
			using var pair = new Pair();
			Assert.That(pair.Edit(h => h.Move(Named(h, "post 2"), Named(h, "group"), 0), "post 2 moved into group"), Is.False, "post 2 moved into group: the tree was kept");
			Moved(pair, "post 2 moved into group");
			Assert.That(pair.Edit(h => h.Move(Named(h, "block"), Named(h, "group"), 1), "block moved into group"), Is.False, "block moved into group: the tree was kept");
			Assert.That(pair.Edit(h => h.Move(Named(h, "post 2"), null, 2), "post 2 moved out of group"), Is.False, "post 2 moved out of group: the tree was kept");
			Moved(pair, "post 2 moved out of group");
		}

		[Test]
		public void BrushesAndCompositesAddedAndRemoved_BuildsTheTreeAgain()
		{
			using var pair = new Pair();
			Assert.That(pair.Edit(h => h.Remove(Named(h, "post 3"), destroy: true), "post 3 removed"), Is.False, "post 3 removed: the tree was kept");
			Moved(pair, "post 3 removed");
			Assert.That(pair.Edit(h => h.Insert(null, 2, Box(new float3(2.5f, 0, 1), new float3(4.5f, 1, 2.5f), "added")), "a box added"), Is.False, "a box added: the tree was kept");
			Moved(pair, "a box added");
			Assert.That(pair.Edit(h => h.Insert(Named(h, "group"), 0, Box(new float3(6, 0, 3), new float3(7, 1, 4), "inside")), "a box added to group"), Is.False, "a box added to group: the tree was kept");
			Assert.That(pair.Edit(h => h.Remove(Named(h, "keeps"), destroy: false), "keeps detached"), Is.False, "keeps detached: the tree was kept");
			Moved(pair, "keeps detached");
			Assert.That(pair.Edit(h => h.Remove(Named(h, "keeper"), destroy: true), "keeper removed"), Is.False, "keeper removed: the tree was kept");
			Moved(pair, "keeper removed");
		}

		// What the generators' job pool does each time a generator updates its brushes (GeneratorJobPool.AssignMeshesJob):
		// each node's state set, then each node attached where it belongs. A brush is moved along with the ones that change
		// nothing, so the update builds something.
		static void SetState(ContentsTreeHarness harness, string name, CSGOperationType operation, int contents)
		{
			var node    = Named(harness, name);
			var brush   = harness.TreeBrushOf(node);
			var id      = CompactHierarchyManager.GetCompactNodeID(brush);
			ref var hierarchy = ref CompactHierarchyManager.GetHierarchy(id);
			hierarchy.SetState(id, ChiselMeshLookup.Value.brushMeshBlobCache, CompactHierarchyManager.GetBrushMeshID(brush.NodeID),
							   operation, contents, CompactHierarchyManager.GetNodeLocalTransformation(brush.NodeID));
			hierarchy.SetTreeDirty();
			node.operation = operation;
			node.contents  = contents;
		}

		static void AttachRootChildrenAt(ContentsTreeHarness harness, int from)
		{
			var rootID = CompactHierarchyManager.GetCompactNodeID(harness.Tree);
			ref var hierarchy = ref CompactHierarchyManager.GetHierarchy(rootID);
			int count = hierarchy.ChildCount(rootID);
			var children = new List<CompactNodeID>();
			for (int i = 0; i < count; i++)
				children.Add(hierarchy.GetChildCompactNodeIDAt(rootID, i));
			// from 0 puts every child back where it is; otherwise the child at from is put first
			if (from == 0)
			{
				for (int i = 0; i < count; i++)
					hierarchy.AttachToParentAt(rootID, i, children[i]);
			} else
				hierarchy.AttachToParentAt(rootID, 0, children[from]);
			hierarchy.SetTreeDirty();
			if (from != 0)
			{
				var moved = harness.Scene.roots[from];
				harness.Scene.roots.RemoveAt(from);
				harness.Scene.roots.Insert(0, moved);
			}
		}

		[Test]
		public void StatesSetAsGeneratorsSetThem_KeepTheTreeOnlyWhenTheyAreTheSame()
		{
			using var pair = new Pair();
			Assert.That(pair.Edit(h => { SetState(h, "post 1", CSGOperationType.Additive, 0); Shift(h, "post 2", new float3(0, 0.25f, 0)); }, "post 1 given the state it has"), Is.True, "post 1 given the state it has: the tree was not kept");
			Assert.That(pair.Edit(h => SetState(h, "post 1", CSGOperationType.Subtractive, 0), "post 1 made subtractive"), Is.False, "post 1 made subtractive: the tree was kept");
			Moved(pair, "post 1 made subtractive");
			Assert.That(pair.Edit(h => SetState(h, "post 1", CSGOperationType.Subtractive, kWater), "post 1 made water"), Is.False, "post 1 made water: the tree was kept");
			Moved(pair, "post 1 made water");
		}

		[Test]
		public void NodesAttachedAsGeneratorsAttachThem_KeepTheTreeOnlyWhenTheyMoved()
		{
			using var pair = new Pair();
			Assert.That(pair.Edit(h => { AttachRootChildrenAt(h, 0); Shift(h, "post 2", new float3(0, 0.25f, 0)); }, "every node attached where it is"), Is.True, "every node attached where it is: the tree was not kept");
			Assert.That(pair.Edit(h => AttachRootChildrenAt(h, 5), "the hole attached first"), Is.False, "the hole attached first: the tree was kept");
			Moved(pair, "the hole attached first");
		}

		// A node detached in one update and attached in a later one: only the attach changes the tree in the second
		[Test]
		public void ABrushDetachedAndAttachedLater_BuildsTheTreeAgain()
		{
			using var pair = new Pair();
			var detached = new Dictionary<ContentsTreeHarness, ContentsSceneNode>();
			Assert.That(pair.Edit(h => { detached[h] = Named(h, "post 3"); h.Remove(detached[h], destroy: false); }, "post 3 detached"), Is.False, "post 3 detached: the tree was kept");
			Moved(pair, "post 3 detached");
			Assert.That(pair.Edit(h =>
			{
				var group = Named(h, "group");
				((CSGTreeBranch)h.TreeNodeOf(group)).Insert(0, h.TreeNodeOf(detached[h]));
				group.children.Insert(0, detached[h]);
			}, "post 3 attached to group"), Is.False, "post 3 attached to group: the tree was kept");
			Moved(pair, "post 3 attached to group");
		}

		[Test]
		public void RandomEdits_GiveWhatBuildingGives([Values(1, 2, 3)] int seed)
		{
			var random = new Unity.Mathematics.Random((uint)seed * 7919u);
			using var pair = new Pair();
			var names = new List<string> { "floor", "post 0", "post 1", "post 2", "post 3", "hole", "water", "block", "keeps" };
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
						bool intoGroup = random.NextBool();
						pair.Edit(h => h.Move(Named(h, name), intoGroup ? Named(h, "group") : null, 0), $"step {step}: {name} moved {(intoGroup ? "into group" : "first")}");
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
		}

		// Trees that kept their compact tree and built new ones: every tree goes with them
		[Test]
		public void TreesThatKeptTheirCompactTree_LeaveNothingAllocatedWhenDestroyed()
		{
			void Round()
			{
				using var harness = ContentsTreeHarness.Build(Scene(), captureExact: false);
				harness.Update();
				int kept = 0;
				for (int step = 0; step < 4; step++)
				{
					if (step == 2)
						harness.SetOperation(Named(harness, "post 2"), CSGOperationType.Subtractive);
					Shift(harness, "post 1", new float3(0, step % 2 == 0 ? 0.25f : -0.25f, 0));
					harness.Update();
					kept += Stat(ExactCSGStat.CompactTreesKept);
				}
				Assert.That(kept, Is.GreaterThan(0), "no compact tree was kept");
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
