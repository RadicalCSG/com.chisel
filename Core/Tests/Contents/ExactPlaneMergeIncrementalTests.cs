using System.Collections.Generic;
using NUnit.Framework;
using Unity.Mathematics;

namespace Chisel.Core.Tests
{
	// The plane merge only merges again the groups an update's edits can have changed (ExactPlaneMerge.MergeAgain): what it
	// keeps must be what merging every face again would give
	[TestFixture]
	[Category("Contents")]
	public class ExactPlaneMergeIncrementalTests
	{
		const float s = 0.707106769f;

		// A prism under a slope at 45 degrees and a wedge on it whose slanted face is that slope (ExactPlaneMergeTests), at x
		static ContentsSceneNode Slope(float x, string name) => ContentsSceneNode.Brush(new[]
		{
			new float4( 0, -1, 0, 0),
			new float4(-1,  0, 0, 1),
			new float4( s,  s, 0, -1.98f * s),
			new float4( 0,  0, 1, -2),
			new float4(-s,  0, -s, 0),
		}, localToTree: float4x4.Translate(new float3(x, 0, 0)), name: name);

		static ContentsSceneNode Wedge(float x, string name) => ContentsSceneNode.Brush(new[]
		{
			new float4( 0,  1, 0, -0.98f),
			new float4(-s, -s, 0, 1.98f * s),
			new float4( 1,  0, 0, -1.2f),
			new float4( 0,  0, 1, -2),
			new float4(-s,  0, -s, 0),
		}, localToTree: float4x4.Translate(new float3(x, 0, 0)), name: name);

		static ContentsSceneNode Box(float3 min, float3 max, string name) => ContentsSceneNode.Brush(ContentsScene.BoxPlanes(min, max), name: name);

		// Rows of boxes that touch, whose tops make one plane, and slopes with wedges on them
		static ContentsScene MergedScene()
		{
			var scene = new ContentsScene();
			for (int row = 0; row < 3; row++)
				for (int i = 0; i < 6; i++)
					scene.Add(Box(new float3(i, 0, row * 3), new float3(i + 1, 1, row * 3 + 1), $"box {row} {i}"));
			for (int i = 0; i < 4; i++)
			{
				scene.Add(Slope(20 + i * 5, $"slope {i}"));
				scene.Add(Wedge(20 + i * 5, $"wedge {i}"));
			}
			return scene;
		}

		static List<ExactPlane[]> PlanesOf(ContentsTreeHarness harness)
		{
			var planes = new List<ExactPlane[]>();
			foreach (var node in BrushesOf(harness.Scene))
			{
				Assert.That(ExactCSGCapture.Brushes.TryGetValue(harness.NodeIDOf(node), out var brush), Is.True, $"nothing captured for {node.name}");
				planes.Add(brush.planes);
			}
			return planes;
		}

		static List<ContentsSceneNode> BrushesOf(ContentsScene scene)
		{
			var brushes = new List<ContentsSceneNode>();
			foreach (var root in scene.roots)
				if (root.IsBrush)
					brushes.Add(root);
			return brushes;
		}

		static bool Same(ExactPlane p, ExactPlane q) => p.a == q.a && p.b == q.b && p.c == q.c && Int128.Equals(p.w, q.w);

		[Test]
		public void RandomEdits_KeepThePlanesOfMergingEverythingAgain([Values(1, 2)] int seed)
		{
			RandomEdits(seed, movesOnly: false);
		}

		// Only moves: the brush list stays the same, so the merge looks only at the brushes built again
		[Test]
		public void RandomMoves_KeepThePlanesOfMergingEverythingAgain([Values(1, 2, 3)] int seed)
		{
			RandomEdits(seed, movesOnly: true);
		}

		static void RandomEdits(int seed, bool movesOnly)
		{
			var random = new Random((uint)seed * 7907u);
			using var harness = ContentsTreeHarness.Build(MergedScene());
			harness.JudgeEachUpdate = false;
			Assert.That(harness.Update(), Is.True);
			var memory = ChiselTreeLookup.Value[harness.Tree].planeMergeMemory;
			int added = 0, lookedAtSome = 0, rebuiltByMerge = 0;
			for (int step = 0; step < 30; step++)
			{
				// one to three edits an update, as faces of one group can change together
				string edit = "";
				int edits = random.NextInt(1, 4);
				for (int e = 0; e < edits; e++)
					edit += (e > 0 ? ", " : "") + RandomEdit(harness, ref random, ref added, movesOnly);
				bool afterAMergeRebuilt = memory.mergeRebuilt.Length > 0;
				Assert.That(harness.Update(), Is.True, edit);
				if (memory.lastVisited.Value < BrushesOf(harness.Scene).Count)
				{
					lookedAtSome++;
					if (afterAMergeRebuilt)
						rebuiltByMerge++;
				}
				var kept = PlanesOf(harness);

				using var fresh = ContentsTreeHarness.Build(harness.Scene);
				fresh.JudgeEachUpdate = false;
				Assert.That(fresh.Update(), Is.True);
				var merged = PlanesOf(fresh);
				var names  = BrushesOf(harness.Scene);
				for (int b = 0; b < kept.Count; b++)
				{
					Assert.That(kept[b].Length, Is.EqualTo(merged[b].Length), $"step {step}, {edit}: {names[b].name}");
					for (int p = 0; p < kept[b].Length; p++)
						Assert.That(Same(kept[b][p], merged[b][p]), Is.True, $"step {step}, {edit}: plane {p} of {names[b].name} is not what merging everything gives");
				}
			}
			if (movesOnly)
			{
				Assert.That(lookedAtSome, Is.GreaterThan(0), "no merge looked at only some of the brushes");
				Assert.That(rebuiltByMerge, Is.GreaterThan(0), "no merge looked at only some brushes after one that built brushes again");
			}
		}

		// Moves, removes, adds or reorders a brush at random; says what it did
		static string RandomEdit(ContentsTreeHarness harness, ref Random random, ref int added, bool movesOnly)
		{
			var brushes = BrushesOf(harness.Scene);
			int kind    = movesOnly ? random.NextInt(2) : brushes.Count < 2 ? 3 : random.NextInt(5);
			var brush   = brushes.Count > 0 ? brushes[random.NextInt(brushes.Count)] : null;
			switch (kind)
			{
				case 0:
				case 1:
				{
					// moved a whole step, or across a rounding step, as touching brushes are
					var offset = random.NextBool() ? new float3(0, random.NextBool() ? 0.25f : -0.25f, 0) : new float3(1f / 32768, 0, 0);
					harness.SetLocalToTree(brush, math.mul(float4x4.Translate(offset), brush.localToTree));
					return $"moved {brush.name} by {offset}";
				}
				case 2:
					harness.Remove(brush, destroy: true);
					return $"removed {brush.name}";
				case 3:
				{
					int i = random.NextInt(0, 7), row = random.NextInt(0, 3);
					var box = Box(new float3(i, 1, row * 3), new float3(i + 1, 2, row * 3 + 1), $"added {added++}");
					harness.Insert(null, random.NextInt(0, harness.Scene.roots.Count + 1), box);
					return $"added {box.name}";
				}
				default:
					harness.Move(brush, null, random.NextInt(0, harness.Scene.roots.Count));
					return $"moved {brush.name} in the tree";
			}
		}

		// Both boxes of a pair touching in a face changed in one update, or one removed and the other moved: the faces of
		// one group leave the memory one brush after the other
		[Test]
		public void TwoBrushesOfOneGroupChanged_KeepThePlanesOfMergingEverythingAgain([Values(true, false)] bool removeOne)
		{
			var scene = new ContentsScene();
			for (int pair = 0; pair < 6; pair++)
			{
				var at = new float3(pair * 4, 0, 0);
				scene.Add(Box(at, at + new float3(1, 1, 1), $"left {pair}"));
				scene.Add(Box(at + new float3(1, 0, 0), at + new float3(2, 1, 1), $"right {pair}"));
			}
			using var harness = ContentsTreeHarness.Build(scene);
			harness.JudgeEachUpdate = false;
			Assert.That(harness.Update(), Is.True);
			var memory = ChiselTreeLookup.Value[harness.Tree].planeMergeMemory;

			var left  = scene.roots[2];
			var right = scene.roots[3];
			var up    = float4x4.Translate(new float3(0, 0.25f, 0));
			if (removeOne)
				harness.Remove(left, destroy: true);
			else
				harness.SetLocalToTree(left, math.mul(up, left.localToTree));
			harness.SetLocalToTree(right, math.mul(up, right.localToTree));
			Assert.That(harness.Update(), Is.True);
			Assert.That(memory.lastFull.Value, Is.False, "two brushes of twelve changed");

			var kept = PlanesOf(harness);
			using var fresh = ContentsTreeHarness.Build(harness.Scene);
			fresh.JudgeEachUpdate = false;
			Assert.That(fresh.Update(), Is.True);
			var merged = PlanesOf(fresh);
			for (int b = 0; b < kept.Count; b++)
			{
				Assert.That(kept[b].Length, Is.EqualTo(merged[b].Length));
				for (int p = 0; p < kept[b].Length; p++)
					Assert.That(Same(kept[b][p], merged[b][p]), Is.True, $"plane {p} of brush {b}");
			}
		}

		// Many pairs of boxes, each pair touching in a face: moving one box merges again only its own faces and its pair's
		[Test]
		public void MovingOneBrush_MergesAgainOnlyTheGroupsItIsIn()
		{
			var scene = new ContentsScene();
			for (int pair = 0; pair < 150; pair++)
			{
				var at = new float3(pair % 15 * 4, 0, pair / 15 * 4);
				scene.Add(Box(at, at + new float3(1, 1, 1), $"left {pair}"));
				scene.Add(Box(at + new float3(1, 0, 0), at + new float3(2, 1, 1), $"right {pair}"));
			}
			using var harness = ContentsTreeHarness.Build(scene);
			harness.JudgeEachUpdate = false;
			Assert.That(harness.Update(), Is.True);
			var memory = ChiselTreeLookup.Value[harness.Tree].planeMergeMemory;
			Assert.That(memory.lastFull.Value, Is.True, "the first merge merges everything");

			var moved = scene.roots[2 * 70];
			harness.SetLocalToTree(moved, math.mul(float4x4.Translate(new float3(0, 0.25f, 0)), moved.localToTree));
			Assert.That(harness.Update(), Is.True);
			Assert.That(memory.lastFull.Value, Is.False, "one brush moved");
			Assert.That(memory.lastMerged.Value, Is.LessThanOrEqualTo(20), "faces merged again, of 1,800");
			Assert.That(memory.lastVisited.Value, Is.LessThanOrEqualTo(10), "brushes looked at, of 300");

			// and again: the brushes the last merge built again are looked at too
			harness.SetLocalToTree(moved, math.mul(float4x4.Translate(new float3(0, 0.25f, 0)), moved.localToTree));
			Assert.That(harness.Update(), Is.True);
			Assert.That(memory.lastVisited.Value, Is.LessThanOrEqualTo(10), "brushes looked at the second time, of 300");
		}

		// A ring of faces that no longer leads back to where it started (looping, or ending) must not hang the update or
		// lose planes: everything is merged again
		[Test]
		public void BrokenRingsInTheMemory_MergeEverythingAgain([Values(true, false)] bool looping)
		{
			var scene = new ContentsScene();
			for (int pair = 0; pair < 4; pair++)
			{
				var at = new float3(pair * 4, 0, 0);
				scene.Add(Box(at, at + new float3(1, 1, 1), $"left {pair}"));
				scene.Add(Box(at + new float3(1, 0, 0), at + new float3(2, 1, 1), $"right {pair}"));
			}
			using var harness = ContentsTreeHarness.Build(scene);
			harness.JudgeEachUpdate = false;
			Assert.That(harness.Update(), Is.True);
			var memory = ChiselTreeLookup.Value[harness.Tree].planeMergeMemory;

			int broken = 0;
			for (int slot = 0; slot < memory.next.Length; slot++)
			{
				int mate = memory.next[slot];
				if (mate < 0 || mate == slot || memory.next[mate] != slot)
					continue;
				memory.next[mate] = looping ? mate : -1;
				broken++;
			}
			Assert.That(broken, Is.GreaterThan(0), "rings to break");

			foreach (var moved in new[] { scene.roots[2], scene.roots[3] })
				harness.SetLocalToTree(moved, math.mul(float4x4.Translate(new float3(0, 0.25f, 0)), moved.localToTree));
			UnityEngine.TestTools.LogAssert.Expect(UnityEngine.LogType.Error, new System.Text.RegularExpressions.Regex("plane merge memory lost track"));
			Assert.That(harness.Update(), Is.True);
			Assert.That(memory.lastFull.Value, Is.True, "merged everything again");

			var kept = PlanesOf(harness);
			using var fresh = ContentsTreeHarness.Build(harness.Scene);
			fresh.JudgeEachUpdate = false;
			Assert.That(fresh.Update(), Is.True);
			var merged = PlanesOf(fresh);
			for (int b = 0; b < kept.Count; b++)
			{
				Assert.That(kept[b].Length, Is.EqualTo(merged[b].Length));
				for (int p = 0; p < kept[b].Length; p++)
					Assert.That(Same(kept[b][p], merged[b][p]), Is.True, $"plane {p} of brush {b}");
			}
		}
	}
}
