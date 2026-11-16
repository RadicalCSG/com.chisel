using NUnit.Framework;
using Unity.Mathematics;

namespace Chisel.Core.Tests
{
	// A pool: a floor, a subtractive brush carving the pool out of it, and the same brush again, additive with water
	// contents, filling it. The water's top is where water meets air, so it is drawn.
	[TestFixture]
	[Category("Contents")]
	public class WaterInCarvedSpaceTests
	{
		const int kPlaneMaxY = 3;

		static (ContentsScene scene, ContentsSceneNode water) Pool()
		{
			var floor = ContentsSceneNode.Brush(ContentsScene.BoxPlanes(new float3(-4, -4, -4), new float3(4, 0, 4)), name: "floor");
			var carve = ContentsSceneNode.Brush(ContentsScene.BoxPlanes(new float3(-2, -2, -2), new float3(2, 0, 2)),
												operation: CSGOperationType.Subtractive, name: "carve");
			var water = ContentsSceneNode.Brush(ContentsScene.BoxPlanes(new float3(-2, -2, -2), new float3(2, 0, 2)),
												ContentsListScope.kWater, name: "water");
			return (new ContentsScene().Add(floor).Add(carve).Add(water), water);
		}

		[Test]
		public void TheOracle_DrawsTheWatersTop()
		{
			using var scope = new ContentsListScope();
			var (scene, water) = Pool();
			var oracle = new ContentsOracle(scene);
			Assert.That(oracle.JudgeFace(water, kPlaneMaxY, new float3(0.5f, 0, 0.5f), 0.05f), Is.EqualTo(ContentsFaceVerdict.FacingOut));
		}

		// Without the carve the floor and the water have a face in the same place: the floor's is drawn, as Solid wins
		// every tie, though the water comes later
		[Test]
		public void WaterFlushWithTheFloor_DrawsTheFloorsFace()
		{
			using var scope = new ContentsListScope();
			var floor = ContentsSceneNode.Brush(ContentsScene.BoxPlanes(new float3(-4, -4, -4), new float3(4, 0, 4)), name: "floor");
			var water = ContentsSceneNode.Brush(ContentsScene.BoxPlanes(new float3(-2, -2, -2), new float3(2, 0, 2)),
												ContentsListScope.kWater, name: "water");
			var scene = new ContentsScene().Add(floor).Add(water);
			ContentsComparison.BuildAndAssertMatchesOracle(scene, "water flush with the floor");

			using var harness = ContentsTreeHarness.Build(scene);
			Assert.That(harness.Update(), Is.True);
			int onTop = 0;
			foreach (var triangle in harness.Triangles)
			{
				var center = triangle.Center;
				if (math.abs(center.y) > 1e-4f || math.abs(center.x) >= 2 || math.abs(center.z) >= 2 || triangle.Normal.y < 0.5f)
					continue;
				onTop++;
				Assert.That(harness.NodeOf(triangle.brushID), Is.SameAs(floor), "the top over the water is drawn by " + harness.NodeOf(triangle.brushID)?.name);
			}
			Assert.That(onTop, Is.GreaterThan(0), "nothing drawn on the top over the water");
		}

		[Test]
		public void WaterFillingWhatASubtractiveBrushCarved_MatchesTheOracle()
		{
			using var scope = new ContentsListScope();
			var (scene, _) = Pool();
			ContentsComparison.BuildAndAssertMatchesOracle(scene, "water filling a carved pool");
		}
	}
}
