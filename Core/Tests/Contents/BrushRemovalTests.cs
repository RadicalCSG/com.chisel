using System;
using NUnit.Framework;
using Unity.Mathematics;

namespace Chisel.Core.Tests
{
	[TestFixture]
	[Category("Contents")]
	public class BrushRemovalTests
	{
		static ContentsSceneNode Box(string name, float3 min, float3 max)
		{
			return ContentsSceneNode.Brush(ContentsScene.BoxPlanes(min, max), name: name);
		}

		static ContentsTreeHarness BuildAndUpdate(ContentsScene scene, string what)
		{
			var harness = ContentsTreeHarness.Build(scene);
			Assert.That(harness.Update(), Is.True, what + ": the CSG update did not run");
			return harness;
		}

		static void UpdateAndAssert(ContentsTreeHarness harness, string what, params ContentsSceneNode[] removed)
		{
			Assert.That(harness.Update(), Is.True, what + ": the CSG update did not run");
			Assert.That(harness.Delivered, Is.True, what + ": no meshes were delivered (" + harness.LastUpdateReport + ")");
			ContentsComparison.AssertMatchesOracle(harness, what);
			Assert.That(RemovedBrushTriangles(harness, removed), Is.Zero, what + ": triangles of removed brushes are still drawn");
		}

		// A destroyed brush is gone from the harness, so its triangles have no node; a detached one is
		// still known.
		static int RemovedBrushTriangles(ContentsTreeHarness harness, ContentsSceneNode[] removed)
		{
			var count = 0;
			foreach (var triangle in harness.Triangles)
			{
				var node = harness.NodeOf(triangle.brushID);
				if (node == null || Array.IndexOf(removed, node) >= 0)
					count++;
			}
			return count;
		}

		static string Removed(bool destroy) => destroy ? "destroyed" : "detached";

		[Test]
		public void RemovingABrush_RebuildsTheBrushesItTouched([Values] bool destroy)
		{
			var a = Box("a", new float3(0), new float3(2));
			var b = Box("b", new float3(1), new float3(3));
			var scene = new ContentsScene().Add(a).Add(b);
			using (var harness = BuildAndUpdate(scene, "a, b"))
			{
				harness.Remove(b, destroy);
				UpdateAndAssert(harness, "b " + Removed(destroy), b);
			}
		}

		[Test]
		public void RemovingABrushThatTouchesNothing_RemovesItsTriangles([Values] bool destroy)
		{
			var a   = Box("a",   new float3(0),        new float3(2));
			var far = Box("far", new float3(10, 0, 0), new float3(12, 2, 2));
			var scene = new ContentsScene().Add(a).Add(far);
			using (var harness = BuildAndUpdate(scene, "a, far"))
			{
				harness.Remove(far, destroy);
				UpdateAndAssert(harness, "far " + Removed(destroy), far);
			}
		}

		// What a generator does when it is left with no brushes: each one is removed and the branch
		// stays. The wall is outside the branch, and nothing flags it.
		[Test]
		public void RemovingEveryBrushOfAComposite_RebuildsTheBrushesTheyTouched([Values] bool destroy)
		{
			var wall  = Box("wall", new float3(0),          new float3(4, 2, 2));
			var s1    = Box("s1",   new float3(0.5f, 1, 1), new float3(1.5f, 3, 3));
			var s2    = Box("s2",   new float3(2.5f, 1, 1), new float3(3.5f, 3, 3));
			var steps = ContentsSceneNode.Composite(CSGOperationType.Additive, s1, s2);
			var scene = new ContentsScene().Add(wall).Add(steps);
			using (var harness = BuildAndUpdate(scene, "wall, [s1, s2]"))
			{
				harness.Remove(s1, destroy);
				harness.Remove(s2, destroy);
				UpdateAndAssert(harness, "s1 and s2 " + Removed(destroy), s1, s2);
			}
		}

		// Half of a row of overlapping boxes removed in one update: every box that is left touched two
		// of them.
		[Test]
		public void RemovingManyBrushesAtOnce_RebuildsEveryBrushTheyTouched([Values] bool destroy)
		{
			const int kCount = 16;
			var boxes = new ContentsSceneNode[kCount];
			var scene = new ContentsScene();
			for (int i = 0; i < kCount; i++)
			{
				boxes[i] = Box("box" + i, new float3(i * 1.5f, 0, 0), new float3((i * 1.5f) + 2, 2, 2));
				scene.Add(boxes[i]);
			}
			using (var harness = BuildAndUpdate(scene, kCount + " overlapping boxes"))
			{
				var removed = new ContentsSceneNode[kCount / 2];
				for (int i = 1; i < kCount; i += 2)
				{
					harness.Remove(boxes[i], destroy);
					removed[i / 2] = boxes[i];
				}
				UpdateAndAssert(harness, "every other box " + Removed(destroy), removed);
			}
		}
	}
}
