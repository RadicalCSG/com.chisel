using NUnit.Framework;
using Unity.Mathematics;
using Random = Unity.Mathematics.Random;

namespace Chisel.Core.Tests
{
	[TestFixture]
	[Category("Contents")]
	public class IntersectionLoopCapacityTests
	{
		// Unit boxes, each with its own rotation and scale, packed close enough that every one overlaps every
		// other. Deterministic: the same seed builds the same scene every run.
		static ContentsScene MutuallyOverlappingBrushes(int count)
		{
			var random = new Random(0x9E3779B9);
			var scene  = new ContentsScene();
			for (int i = 0; i < count; i++)
			{
				var node = ContentsSceneNode.Brush(ContentsScene.BoxPlanes(new float3(-0.5f), new float3(0.5f)),
												   name: "b" + i);
				node.localToTree = float4x4.TRS(random.NextFloat3(-0.35f, 0.35f),
												quaternion.Euler(random.NextFloat3(-math.PI, math.PI)),
												random.NextFloat3(0.9f, 1.6f));
				scene.Add(node);
			}
			return scene;
		}

		[Test]
		public void AFewOverlappingBrushes_MatchTheOracle()
		{
			ContentsComparison.BuildAndAssertMatchesOracle(MutuallyOverlappingBrushes(4), "four overlapping brushes");
		}

		// 6N(N-1) loops against 32N reserved: at ten brushes that is 540 against 320, exceeded by 1.7x with ten
		// brushes rather than the 256 it took to find on a map.
		[Test]
		public void EnoughOverlappingBrushesToOutgrowTheGuessedCapacity_StillMatchTheOracle()
		{
			ContentsComparison.BuildAndAssertMatchesOracle(MutuallyOverlappingBrushes(10), "ten overlapping brushes");
		}

		// And past it by a margin no constant-per-brush reservation could cover: 1440 against 512.
		[Test]
		public void FarPastTheGuessedCapacity_StillMatchesTheOracle()
		{
			ContentsComparison.BuildAndAssertMatchesOracle(MutuallyOverlappingBrushes(16), "sixteen overlapping brushes");
		}
	}
}
