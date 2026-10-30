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

		// Without the carve the floor and the water have a face in the same place: the later brush's is drawn, as
		// between brushes of one type
		[Test]
		public void WaterFlushWithTheFloor_DrawsTheLaterBrushsFace()
		{
			using var scope = new ContentsListScope();
			var floor = ContentsSceneNode.Brush(ContentsScene.BoxPlanes(new float3(-4, -4, -4), new float3(4, 0, 4)), name: "floor");
			var water = ContentsSceneNode.Brush(ContentsScene.BoxPlanes(new float3(-2, -2, -2), new float3(2, 0, 2)),
												ContentsListScope.kWater, name: "water");
			ContentsComparison.BuildAndAssertMatchesOracle(new ContentsScene().Add(floor).Add(water), "water flush with the floor");
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
