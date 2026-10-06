using NUnit.Framework;
using Unity.Mathematics;

namespace Chisel.Core.Tests
{
	[TestFixture]
	[Category("Contents")]
	public class NestedSubtractionRoutingTests
	{
		static ContentsSceneNode Box(string name, float3 min, float3 max, CSGOperationType operation = CSGOperationType.Additive)
		{
			return ContentsSceneNode.Brush(ContentsScene.BoxPlanes(min, max), operation: operation, name: name);
		}

		/// <summary>
		/// One solid block, then <paramref name="depth"/> subtract groups nested inside one another, each
		/// taking a bite that overlaps the block and every other bite - so every brush touches every other
		/// and none of them can be culled out of the routing.
		/// </summary>
		static ContentsScene NestedSubtractions(int depth)
		{
			var block = Box("block", new float3(-10, -10, -10), new float3(10, 10, 10));

			// Build from the inside out, so each group ends up holding the next one.
			ContentsSceneNode inner = null;
			for (int i = depth - 1; i >= 0; i--)
			{
				// Bites all cross the middle of the block and each other, staggered so no two coincide.
				var offset = i * 0.5f;
				var bite = Box("bite" + i,
							   new float3(-5 + offset, -12, -5 - offset),
							   new float3( 5 + offset,  12,  5 - offset));
				inner = inner == null
					  ? ContentsSceneNode.Composite(CSGOperationType.Subtractive, bite)
					  : ContentsSceneNode.Composite(CSGOperationType.Subtractive, bite, inner);
			}

			return new ContentsScene().Add(block).Add(inner);
		}

		[TestCase(2)]
		[TestCase(4)]
		[TestCase(8)]
		[TestCase(16)]
		public void NestedSubtractGroups_Route(int depth)
		{
			using var harness = ContentsTreeHarness.Build(NestedSubtractions(depth));
			Assert.That(harness.Update(), Is.True, depth + " levels: the CSG update did not run");
			Assert.That(harness.Delivered, Is.True,
						depth + " levels: no meshes were delivered (" + harness.LastUpdateReport + ")");
		}
	}
}
