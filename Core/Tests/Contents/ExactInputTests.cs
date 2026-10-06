using System.Linq;
using NUnit.Framework;
using Unity.Mathematics;

namespace Chisel.Core.Tests
{
	[TestFixture]
	[Category("Contents")]
	public class ExactInputTests
	{
		static int InputBrushes => CompactHierarchyManager.TreeUpdate.s_LastExactCSGStats[(int)ExactCSGStat.InputBrushes];

		// Every counter of the last update, for the failure message
		static string Counters()
		{
			var stats = CompactHierarchyManager.TreeUpdate.s_LastExactCSGStats;
			return string.Join(", ", Enumerable.Range(0, stats.Length).Select(i => $"{(ExactCSGStat)i}={stats[i]}"));
		}

		// Boxes in a row, each overlapping the next, so every brush touches its neighbours
		static ContentsScene Row(int count)
		{
			var scene = new ContentsScene();
			for (int i = 0; i < count; i++)
				scene.Add(ContentsSceneNode.Brush(ContentsScene.BoxPlanes(new float3(i, 0, 0), new float3(i + 1.5f, 1, 1)), name: "box" + i));
			return scene;
		}

		static ContentsTreeHarness BuildAndUpdate(ContentsScene scene)
		{
			if (!CompactHierarchyManager.TreeUpdate.kExactCSG)
				Assert.Ignore("tests the exact CSG's input, which is off");
			var harness = ContentsTreeHarness.Build(scene);
			Assert.That(harness.Update(), Is.True, "the first update did not run");
			return harness;
		}

		static ContentsSceneNode Brush(ContentsTreeHarness harness, string name) => harness.Scene.roots.First(n => n.name == name);

		[Test]
		public void TheFirstUpdate_BuildsEveryBrushesInput()
		{
			using (var harness = BuildAndUpdate(Row(4)))
				Assert.That(InputBrushes, Is.EqualTo(4), Counters());
		}

		[Test]
		public void MovingOneBrush_BuildsOnlyItsInput()
		{
			using (var harness = BuildAndUpdate(Row(4)))
			{
				harness.SetLocalToTree(Brush(harness, "box2"), float4x4.Translate(new float3(0, 0.25f, 0)));
				Assert.That(harness.Update(), Is.True, "moving a brush updated nothing");
				Assert.That(InputBrushes, Is.EqualTo(1), "brushes whose exact input was built after one brush moved; " + Counters());
				ContentsComparison.AssertMatchesOracle(harness, "box2 moved up by a quarter");
			}
		}

		// Removing a brush changes no other brush, but moves the ones after it to other node orders: their inputs have to
		// move with them (CacheRemappingJob), not be rebuilt.
		[Test]
		public void RemovingABrush_KeepsTheOthersInputsWhereTheyGo()
		{
			using (var harness = BuildAndUpdate(Row(4)))
			{
				harness.Remove(Brush(harness, "box1"), destroy: true);
				Assert.That(harness.Update(), Is.True, "removing a brush updated nothing");
				Assert.That(InputBrushes, Is.EqualTo(0), "brushes whose exact input was built after a brush was removed; " + Counters());
				ContentsComparison.AssertMatchesOracle(harness, "box1 removed");

				harness.SetLocalToTree(Brush(harness, "box3"), float4x4.Translate(new float3(-0.5f, 0, 0)));
				Assert.That(harness.Update(), Is.True, "moving a brush updated nothing");
				Assert.That(InputBrushes, Is.EqualTo(1), "brushes whose exact input was built after one brush moved; " + Counters());
				ContentsComparison.AssertMatchesOracle(harness, "box3 moved back by half");
			}
		}

		// Planted: a brush moves and its exact input is not rebuilt. The judge has to see that the planes the CSG used are not
		// the brush's planes any more.
		[Test]
		public void AStaleInput_IsReported()
		{
			using (var harness = BuildAndUpdate(Row(3)))
			{
				harness.SetLocalToTree(Brush(harness, "box1"), float4x4.Translate(new float3(0, 0.25f, 0)));
				var mismatches = ExactJudge.FindMismatches(harness, 100, out _);
				Assert.That(mismatches.Any(m => m.problem.Contains("not the brush's current plane")), Is.True,
							"a brush moved without an update, and the judge did not notice: " +
							(mismatches.Count == 0 ? "nothing reported" : string.Join("; ", mismatches.Select(m => m.Format("")))));
			}
		}
	}
}
