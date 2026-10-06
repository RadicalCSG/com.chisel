using NUnit.Framework;
using Unity.Mathematics;

namespace Chisel.Core.Tests
{
	[TestFixture]
	[Category("Contents")]
	public class SampleSceneExtrudedShapeTests
	{
		const float kSqrtHalf = 0.70710678f;

		static float4[] OctagonalPrism(float flat, float corner, float yMin, float yMax)
		{
			var diagonal = (flat + corner) * kSqrtHalf;
			return new float4[]
			{
				new float4( 0f, -1f,  0f,  yMin),   // bottom cap: y >= yMin
				new float4( 0f, +1f,  0f, -yMax),   // top cap:    y <= yMax
				new float4(+1f,  0f,  0f, -flat),
				new float4(-1f,  0f,  0f, -flat),
				new float4( 0f,  0f, +1f, -flat),
				new float4( 0f,  0f, -1f, -flat),
				new float4(+kSqrtHalf, 0f, +kSqrtHalf, -diagonal),
				new float4(+kSqrtHalf, 0f, -kSqrtHalf, -diagonal),
				new float4(-kSqrtHalf, 0f, +kSqrtHalf, -diagonal),
				new float4(-kSqrtHalf, 0f, -kSqrtHalf, -diagonal),
			};
		}

		// The scene's two shapes, in the generator's own local space (before pivotOffset and the transform).
		static float4[] AdditivePrism() { return OctagonalPrism(5.5f, 2.5f, -7f, -6f); }
		static float4[] RemovePrism()   { return OctagonalPrism(5.0f, 2.0f, -7f, -6f); }

		// Both generators sit at exactly this transform in the scene; pivotOffset is a plain translation applied
		// between the node and the generated geometry (ChiselGeneratorComponent.LocalTransformation).
		static float4x4 SceneTransform()
		{
			var nodeToTree = float4x4.TRS(new float3(0f, -1.5f, -30f),
										  quaternion.AxisAngle(new float3(0f, 1f, 0f), math.PI), // 180 about Y
										  new float3(1f, 1f, 1f));
			return math.mul(nodeToTree, float4x4.Translate(new float3(0f, 6.5f, 0f)));
		}

		// Local y = -6 is the top cap. Through pivotOffset (+6.5) it becomes y = 0.5, the 180 degree turn about Y
		// leaves Y alone, and the node sits at y = -1.5, so the cap lands on y = -1 facing +Y.
		static readonly float4 kTopCap = new float4(0f, 1f, 0f, 1f);

		static ContentsScene ScenePrisms(bool withRemove)
		{
			var transform = SceneTransform();
			var scene = new ContentsScene()
				.Add(ContentsSceneNode.Brush(AdditivePrism(), localToTree: transform,
											 name: "Extruded Shape (Additive)"));
			if (withRemove)
				scene.Add(ContentsSceneNode.Brush(RemovePrism(), operation: CSGOperationType.Subtractive,
												  localToTree: transform, name: "Extruded Shape (Remove)"));
			return scene;
		}

		// Area of the output triangles lying on the top cap and facing the same way. Measured on the triangles
		// rather than with the oracle's face sampler, matching SourceConcretePadTests.
		static double TopCapArea(ContentsTreeHarness harness)
		{
			double area = 0;
			foreach (var triangle in harness.Triangles)
			{
				if (math.dot(triangle.Normal, kTopCap.xyz) < 0.999f)
					continue;
				if (math.abs(math.dot(kTopCap.xyz, triangle.Center) + kTopCap.w) > 1e-3f)
					continue;
				area += 0.5 * math.length(math.cross(triangle.b - triangle.a, triangle.c - triangle.a));
			}
			return area;
		}

		static double MeasureTopCap(ContentsScene scene, string what)
		{
			using (var harness = ContentsTreeHarness.Build(scene))
			{
				Assert.That(harness.Update(), Is.True, what + ": the CSG update did not run");
				return TopCapArea(harness);
			}
		}

		// THE CONTROL. The larger prism alone must show its whole cap: 11 x 11 with four 3x3 corners cut off.
		// This pins the hand-derived plane set. If it fails, the fault is in this fixture, not in the CSG.
		[Test]
		public void ThePrismOnItsOwn_ShowsItsWholeTopCap()
		{
			var area = MeasureTopCap(ScenePrisms(withRemove: false), "the additive prism alone");
			Assert.That(area, Is.EqualTo(103.0).Within(0.5), $"the prism's top cap is {area:0.00}");
		}

		[Test]
		public void APrismWithACoaxialPrismSubtracted_LeavesARingOnItsTopCap()
		{
			var area = MeasureTopCap(ScenePrisms(withRemove: true), "the additive prism with the remove prism");
			Assert.That(area, Is.EqualTo(21.0).Within(0.5),
						$"the cap should be a ring of 21 but is {area:0.00} " +
						"(103 means the subtraction did nothing to it)");
		}

		// The same subtraction seen from the other end. Both caps are coincident, so both must become rings;
		// if only one survives, the two ends are being treated differently and that narrows the cause.
		[Test]
		public void APrismWithACoaxialPrismSubtracted_LeavesARingOnItsBottomCap()
		{
			// local y = -7 -> +6.5 -> -0.5, node at -1.5, so the bottom cap lands on y = -2 facing -Y.
			var bottomCap = new float4(0f, -1f, 0f, -2f);
			using (var harness = ContentsTreeHarness.Build(ScenePrisms(withRemove: true)))
			{
				Assert.That(harness.Update(), Is.True, "the CSG update did not run");
				double area = 0;
				foreach (var triangle in harness.Triangles)
				{
					if (math.dot(triangle.Normal, bottomCap.xyz) < 0.999f)
						continue;
					if (math.abs(math.dot(bottomCap.xyz, triangle.Center) + bottomCap.w) > 1e-3f)
						continue;
					area += 0.5 * math.length(math.cross(triangle.b - triangle.a, triangle.c - triangle.a));
				}
				Assert.That(area, Is.EqualTo(21.0).Within(0.5),
							$"the bottom cap should be a ring of 21 but is {area:0.00}");
			}
		}
	}
}
