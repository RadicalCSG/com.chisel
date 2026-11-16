using NUnit.Framework;
using Unity.Mathematics;

namespace Chisel.Core.Tests
{
	// Faces of brushes of different types in one plane, facing the same way: the type that wins the tie draws it (Solid,
	// then the earlier entry in the list), whichever brush comes later; and where the winner's face is not drawn there (a
	// carve took it, or a brush of its own type lies across it) the other face is decided as if the winner were not there
	[TestFixture]
	[Category("Contents")]
	public class CoplanarTieTests
	{
		static ContentsSceneNode Box(float3 min, float3 max, string name, int contents = ContentsSceneNode.kSolidContents,
									 CSGOperationType operation = CSGOperationType.Additive)
		{
			return ContentsSceneNode.Brush(ContentsScene.BoxPlanes(min, max), contents: contents, operation: operation, name: name);
		}

		// A Solid box whose side lies on the seam of two Water boxes: across that side is Water, so the side is drawn,
		// though the later Water box has a face there too (which the Water behind it takes away)
		[Test]
		public void ASolidSideOnTheSeamOfTwoWaters_IsDrawn()
		{
			using var scope = new ContentsListScope();
			var scene = new ContentsScene()
				.Add(Box(new float3(0, -1, -1), new float3(1, 1, 1), "solid"))
				.Add(Box(new float3(-4, -2, -2), new float3(0, 2, 2), "water left", ContentsListScope.kWater))
				.Add(Box(new float3(0, -2, -2), new float3(4, 2, 2), "water right", ContentsListScope.kWater));
			ContentsComparison.BuildAndAssertMatchesOracle(scene, "a Solid side on the seam of two Waters");
		}

		// A Water top in the plane of a Glass top, which wins the tie, but whose face is not drawn there: another Glass box
		// lies on it. The Water top is drawn, against the Glass above.
		[Test]
		public void AWaterTopUnderTheSeamOfTwoGlasses_IsDrawn([Values] bool waterFirst)
		{
			using var scope = new ContentsListScope();
			var water = Box(new float3(-1, -2, -1), new float3(1, 0, 1), "water", ContentsListScope.kWater);
			var below = Box(new float3(-2, -2, -2), new float3(2, 0, 2), "glass below", ContentsListScope.kGlass);
			var above = Box(new float3(-2, 0, -2), new float3(2, 2, 2), "glass above", ContentsListScope.kGlass);
			var scene = waterFirst ? new ContentsScene().Add(water).Add(below).Add(above)
								   : new ContentsScene().Add(below).Add(above).Add(water);
			ContentsComparison.BuildAndAssertMatchesOracle(scene, "a Water top under the seam of two Glasses");
		}

		// A Water top in the plane of a Solid floor's top, which wins the tie, but which a carve took away over the Water:
		// the Water top is drawn there, and the floor's top around it
		[Test]
		public void AWaterTopWhereACarveTookTheFloorsTop_IsDrawn()
		{
			using var scope = new ContentsListScope();
			var scene = new ContentsScene()
				.Add(Box(new float3(-4, -4, -4), new float3(4, 0, 4), "floor"))
				.Add(Box(new float3(-2, -2, -2), new float3(2, 0, 2), "carve", operation: CSGOperationType.Subtractive))
				.Add(Box(new float3(-1, -2, -1), new float3(1, 0, 1), "water", ContentsListScope.kWater));
			ContentsComparison.BuildAndAssertMatchesOracle(scene, "a Water top where a carve took the floor's top");
		}
	}
}
