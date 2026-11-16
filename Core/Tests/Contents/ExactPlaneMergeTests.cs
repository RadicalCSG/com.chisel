using NUnit.Framework;
using Unity.Mathematics;

namespace Chisel.Core.Tests
{
	// Faces of different brushes that lie in one plane in float keep one exact plane, although each brush makes its planes
	// from its own corners snapped to the grid, and on a slanted plane different corners snap off one plane
	[TestFixture]
	[Category("Contents")]
	public class ExactPlaneMergeTests
	{
		const float s = 0.707106769f;

		// A prism under a slope at 45 degrees through (1, 0.98) and (1.98, 0), mitred at x + z = 0, as pathed stairs fill a
		// step down
		static ContentsSceneNode Slope() => ContentsSceneNode.Brush(new[]
		{
			new float4( 0, -1, 0, 0),
			new float4(-1,  0, 0, 1),
			new float4( s,  s, 0, -1.98f * s),
			new float4( 0,  0, 1, -2),
			new float4(-s,  0, -s, 0),
		}, name: "slope");

		// The wedge the stairs put on that slope under a step: its slanted face lies on the slope, its corner (1.2, 0.78)
		// snaps off the line the slope's corners (1, 0.98) and (1.98, 0) snap to
		static ContentsSceneNode Wedge() => ContentsSceneNode.Brush(new[]
		{
			new float4( 0,  1, 0, -0.98f),
			new float4(-s, -s, 0, 1.98f * s),
			new float4( 1,  0, 0, -1.2f),
			new float4( 0,  0, 1, -2),
			new float4(-s,  0, -s, 0),
		}, name: "wedge");

		static ContentsScene WedgeOnASlope() => new ContentsScene().Add(Slope()).Add(Wedge());

		// The exact plane of the brush's face whose normal is closest to the direction given
		static ExactPlane PlaneFacing(ContentsTreeHarness harness, string name, double3 direction)
		{
			var node = harness.Scene.roots.Find(n => n.name == name);
			Assert.That(ExactCSGCapture.Brushes.TryGetValue(harness.NodeIDOf(node), out var brush), Is.True, "nothing captured for " + name);
			ExactPlane best = default; double bestDot = double.NegativeInfinity;
			foreach (var plane in brush.planes)
			{
				var dot = math.dot(math.normalize(new double3(plane.a, plane.b, plane.c)), math.normalize(direction));
				if (dot > bestDot) { bestDot = dot; best = plane; }
			}
			return best;
		}

		[Test]
		public void AWedgeOnASlope_SharesTheSlopesPlane()
		{
			using var harness = ContentsTreeHarness.Build(WedgeOnASlope());
			harness.JudgeEachUpdate = false;
			Assert.That(harness.Update(), Is.True);
			var slope = PlaneFacing(harness, "slope", new double3(1, 1, 0));
			var wedge = PlaneFacing(harness, "wedge", new double3(-1, -1, 0));
			Assert.That(ExactPredicates.SamePlane(slope, wedge), Is.EqualTo(-1),
						$"slope ({slope.a}, {slope.b}, {slope.c}, {slope.w.ToDouble()}) and wedge ({wedge.a}, {wedge.b}, {wedge.c}, {wedge.w.ToDouble()}) are not one plane facing both ways");
		}

		// Only the slope moves, away and back: the wedge, untouched, takes its own plane again and then the slope's
		[Test]
		public void TheSlopeMovedAwayAndBack_TheWedgeFollowsWithoutBeingTouched()
		{
			var slope = Slope();
			using var harness = ContentsTreeHarness.Build(new ContentsScene().Add(slope).Add(Wedge()));
			Assert.That(harness.Update(), Is.True);
			Assert.That(SlantedPlanes(harness), Is.EqualTo(-1), "one plane before the slope moved");

			harness.SetLocalToTree(slope, float4x4.Translate(new float3(0, -0.5f, 0)));
			Assert.That(harness.Update(), Is.True);
			Assert.That(SlantedPlanes(harness), Is.EqualTo(0), "still one plane with the slope moved away");

			harness.SetLocalToTree(slope, float4x4.identity);
			Assert.That(harness.Update(), Is.True);
			Assert.That(SlantedPlanes(harness), Is.EqualTo(-1), "not one plane again with the slope back");
		}

		// How the slope's and the wedge's slanted planes compare (ExactPredicates.SamePlane)
		static int SlantedPlanes(ContentsTreeHarness harness)
		{
			return ExactPredicates.SamePlane(PlaneFacing(harness, "slope", new double3(1, 1, 0)), PlaneFacing(harness, "wedge", new double3(-1, -1, 0)));
		}

		[Test]
		public void AWedgeOnASlope_TheMeshIsWhatTheExactCSGMade()
		{
			ExactJudgeCaseTests.AssertTheMeshIsWhatTheExactCSGMade(WedgeOnASlope());
		}

		// Two boxes that touch in float, their shared face one ulp either side of a rounding step: one face's float plane
		// rounds (ExactPlane.Quantize) and its corners snap down a step, the other's up, so without a tolerance of a step
		// they would be a step apart and both faces drawn
		[Test]
		public void TwoBoxesTouchingAcrossARoundingStep_TouchAsOne()
		{
			const float step = 1f / 32768;      // half a grid step: the rounding step lies at 1 + step
			var left  = ContentsSceneNode.Brush(ContentsScene.BoxPlanes(new float3(0, 0, 0), new float3(math.asfloat(math.asint(1 + step) - 1), 1, 1)), name: "left");
			var right = ContentsSceneNode.Brush(ContentsScene.BoxPlanes(new float3(1 + step, 0, 0), new float3(2, 1, 1)), name: "right");
			using var harness = ContentsTreeHarness.Build(new ContentsScene().Add(left).Add(right));
			Assert.That(harness.Update(), Is.True);
			var a = PlaneFacing(harness, "left",  new double3(1, 0, 0));
			var b = PlaneFacing(harness, "right", new double3(-1, 0, 0));
			Assert.That(ExactPredicates.SamePlane(a, b), Is.EqualTo(-1),
						$"left ({a.a}, {a.b}, {a.c}, {a.w.ToDouble()}) and right ({b.a}, {b.b}, {b.c}, {b.w.ToDouble()}) are not one plane facing both ways");
		}

		// Three brushes of raised spiral stairs with a face in the radial plane through the axis: step 32's reaches from the
		// axis outwards, step 6's lies on part of it facing the other way, step 41's is a tall narrow strip further out. Their
		// snapped corners don't lie in one plane, so each makes a different one; all three must be cut with one plane, or
		// step 6 and step 32 meet a hair apart.
		static ContentsScene RadialFacesOfSpiralStairs()
		{
			return new ContentsScene()
				.Add(ContentsSceneNode.Brush(new float4[] { new float4(-0.809016943f, 0f, 0.5877853f, -2.54288981E-08f), new float4(0.809016943f, 0f, 0.5877853f, -1.39617765f), new float4(0f, 1f, 0f, -0.88f), new float4(-0.158255443f, 0f, -0.9873982f, 0.6688816f), new float4(0.18267177f, -0.9741749f, -0.132718831f, 0.6624389f), }, contents: 0, operation: CSGOperationType.Additive, localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(0f, 0f, 0f, 1f)), name: "step 6").WithMesh(new float3[] { new float3(0.440838933f, 0.88f, 0.6067627f), new float3(0.440838933f, 0.68f, 0.6067627f), new float3(0.8628853f, 0.68f, 1.18765962f), new float3(0.8628853f, 0.88f, 1.18765962f), new float3(1.39617777f, 0.88f, 0.4536456f), }, new int[] { 0, 1, 2, 3, -1, 3, 2, 4, -1, 0, 3, 4, -1, 1, 0, 4, -1, 2, 1, 4, -1, }))
				.Add(ContentsSceneNode.Brush(new float4[] { new float4(-0.309017062f, 0f, -0.9510565f, 0f), new float4(0f, 1f, 0f, -0.7f), new float4(-1f, 0f, 0f, -0.0190211311f), new float4(0.309017062f, 0f, 0.9510565f, -1.39617765f), new float4(0f, -1f, 0f, 0.68f), new float4(0.809016943f, 0f, -0.5877853f, 5.551115E-17f), }, contents: 0, operation: CSGOperationType.Additive, localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(0f, 0f, 0f, 1f)), name: "step 32").WithMesh(new float3[] { new float3(0f, 0.7f, 0f), new float3(-0.0190211311f, 0.7f, 0.006180342f), new float3(-0.0190211311f, 0.7f, 1.47420847f), new float3(0.8628853f, 0.7f, 1.18765962f), new float3(0f, 0.68f, 0f), new float3(-0.0190211311f, 0.68f, 0.006180342f), new float3(-0.0190211311f, 0.68f, 1.47420847f), new float3(0.8628853f, 0.68f, 1.18765962f), }, new int[] { 1, 0, 4, 5, -1, 3, 0, 1, 2, -1, 6, 2, 1, 5, -1, 3, 2, 6, 7, -1, 4, 7, 6, 5, -1, 4, 0, 3, 7, -1, }))
				.Add(ContentsSceneNode.Brush(new float4[] { new float4(-0.707106769f, 0f, -0.7071068f, 1.32117784f), new float4(0.707106769f, 0f, 0.7071068f, -1.47117782f), new float4(-0.8314696f, 0f, 0.555570245f, 0f), new float4(0.809016943f, 0f, -0.587785244f, 0f), new float4(-0.192529663f, 0.9728222f, 0.128644258f, -1.83620191f), new float4(-0.167979434f, 0.9782066f, 0.122044258f, -1.85859251f), new float4(0f, -1f, 0f, 0f), }, contents: 0, operation: CSGOperationType.Additive, localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(0f, 0f, 0f, 1f)), name: "step 41").WithMesh(new float3[] { new float3(0.748387158f, 0f, 1.12004054f), new float3(0.8333553f, 0f, 1.2472043f), new float3(0.8755157f, 0f, 1.20504391f), new float3(0.786248863f, 0f, 1.08217871f), new float3(0.748387158f, 1.8875f, 1.12004054f), new float3(0.8333553f, 1.8875f, 1.2472043f), new float3(0.8755157f, 1.9f, 1.20504391f), new float3(0.786248863f, 1.9f, 1.08217871f), }, new int[] { 4, 7, 3, 0, -1, 1, 2, 6, 5, -1, 0, 1, 5, 4, -1, 7, 6, 2, 3, -1, 4, 5, 7, -1, 5, 6, 7, -1, 3, 2, 1, 0, -1, }));
		}

		static readonly double3 kRadial = new double3(0.809016943, 0, -0.587785244);

		[Test]
		public void RadialFacesOfSpiralStairs_TakeOnePlane()
		{
			using var harness = ContentsTreeHarness.Build(RadialFacesOfSpiralStairs());
			harness.JudgeEachUpdate = false;
			Assert.That(harness.Update(), Is.True);
			var step32 = PlaneFacing(harness, "step 32", kRadial);
			var step6  = PlaneFacing(harness, "step 6", -kRadial);
			var step41 = PlaneFacing(harness, "step 41", kRadial);
			Assert.That(ExactPredicates.SamePlane(step32, step6), Is.EqualTo(-1),
						$"step 32 ({step32.a}, {step32.b}, {step32.c}, {step32.w.ToDouble()}) and step 6 ({step6.a}, {step6.b}, {step6.c}, {step6.w.ToDouble()}) are not one plane facing both ways");
			Assert.That(ExactPredicates.SamePlane(step32, step41), Is.EqualTo(1),
						$"step 32 ({step32.a}, {step32.b}, {step32.c}, {step32.w.ToDouble()}) and step 41 ({step41.a}, {step41.b}, {step41.c}, {step41.w.ToDouble()}) are not one plane");
		}

		[Test]
		public void RadialFacesOfSpiralStairs_TheMeshIsWhatTheExactCSGMade()
		{
			ExactJudgeCaseTests.AssertTheMeshIsWhatTheExactCSGMade(RadialFacesOfSpiralStairs());
		}
	}
}
