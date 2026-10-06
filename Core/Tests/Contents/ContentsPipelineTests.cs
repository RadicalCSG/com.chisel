using NUnit.Framework;
using Unity.Mathematics;

namespace Chisel.Core.Tests
{
	[TestFixture]
	[Category("Contents")]
	public class ContentsPipelineTests
	{
		[Test]
		public void PostAndPane_AllSolid_MatchesTheOracle()
		{
			// The bm_c0a0a shape, with both brushes Solid: today's pipeline drops the post's face
			// where the pane covers it, and so does the oracle, because Solid removes Solid.
			var scene = new ContentsScene()
				.Add(ContentsSceneNode.Brush(ContentsScene.BoxPlanes(new float3(-1, -1, 0), new float3(1, 1, 4)),
											 name: "post"))
				.Add(ContentsSceneNode.Brush(ContentsScene.BoxPlanes(new float3(-0.5f, 1, 0), new float3(0.5f, 5, 4)),
											 name: "pane"));
			ContentsComparison.BuildAndAssertMatchesOracle(scene, "post and pane");
		}

		[Test]
		public void GridBoxes_AllSolid_MatchTheOracle([Values(1, 2, 3, 4, 5, 6, 7, 8)] int seed)
		{
			var scene = ContentsSceneGenerators.AllSolid(ContentsSceneGenerators.GridBoxes(seed));
			ContentsComparison.BuildAndAssertMatchesOracle(scene, "grid boxes seed " + seed);
		}

		[Test]
		public void RotatedGridBoxes_AllSolid_MatchTheOracle([Values(1, 2, 3, 4)] int seed)
		{
			var scene = ContentsSceneGenerators.AllSolid(ContentsSceneGenerators.RotatedGridBoxes(seed));
			ContentsComparison.BuildAndAssertMatchesOracle(scene, "rotated grid boxes seed " + seed);
		}

		[Test]
		public void FreeBrushes_AllSolid_MatchTheOracle([Values(1, 2, 3, 4)] int seed)
		{
			var scene = ContentsSceneGenerators.AllSolid(ContentsSceneGenerators.FreeBrushes(seed));
			ContentsComparison.BuildAndAssertMatchesOracle(scene, "free brushes seed " + seed);
		}

		[Test]
		public void CutBrushes_AllSolid_MatchTheOracle([Values(1, 2, 3, 4)] int seed)
		{
			var scene = ContentsSceneGenerators.AllSolid(ContentsSceneGenerators.CutBrushes(seed));
			ContentsComparison.BuildAndAssertMatchesOracle(scene, "cut brushes seed " + seed);
		}
	}
}
