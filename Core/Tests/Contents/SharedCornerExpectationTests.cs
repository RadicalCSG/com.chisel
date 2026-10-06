using System.Linq;
using NUnit.Framework;
using Unity.Mathematics;

namespace Chisel.Core.Tests
{
	[TestFixture]
	[Category("Contents")]
	public class SharedCornerExpectationTests
	{
		const int kSectors = 720;

		static bool?[] Pattern(System.Func<int, bool?> sector) => Enumerable.Range(0, kSectors).Select(sector).ToArray();

		#region Sector patterns
		[Test]
		public void ExpectFace_NothingDrawn_IsNoFace()
		{
			Assert.That(SharedCornerSurvey.ExpectFace(Pattern(k => false)), Is.EqualTo(SharedCornerSurvey.Expectation.NoFace));
			Assert.That(SharedCornerSurvey.ExpectFace(Pattern(k => k < 360 ? (bool?)null : false)), Is.EqualTo(SharedCornerSurvey.Expectation.NoFace));
		}

		[Test]
		public void ExpectFace_DrawnAllAround_IsDrawnThrough()
		{
			Assert.That(SharedCornerSurvey.ExpectFace(Pattern(k => true)), Is.EqualTo(SharedCornerSurvey.Expectation.DrawnThrough));
			// The edge brush's face only covers half the circle
			Assert.That(SharedCornerSurvey.ExpectFace(Pattern(k => k < 360 ? (bool?)null : true)), Is.EqualTo(SharedCornerSurvey.Expectation.DrawnThrough));
		}

		[Test]
		public void ExpectFace_CutByAStraightLine_IsAStraightBoundary()
		{
			Assert.That(SharedCornerSurvey.ExpectFace(Pattern(k => k < 360)), Is.EqualTo(SharedCornerSurvey.Expectation.StraightBoundary));
			Assert.That(SharedCornerSurvey.ExpectFace(Pattern(k => k >= 100 && k < 460)), Is.EqualTo(SharedCornerSurvey.Expectation.StraightBoundary));
			// One sector either way is sampling, not a bend
			Assert.That(SharedCornerSurvey.ExpectFace(Pattern(k => k < 361)), Is.EqualTo(SharedCornerSurvey.Expectation.StraightBoundary));
		}

		[Test]
		public void ExpectFace_BentOutline_IsABoundary()
		{
			// A quarter cut out, and a 9 degree bend
			Assert.That(SharedCornerSurvey.ExpectFace(Pattern(k => k >= 180)), Is.EqualTo(SharedCornerSurvey.Expectation.Boundary));
			Assert.That(SharedCornerSurvey.ExpectFace(Pattern(k => k < 378)), Is.EqualTo(SharedCornerSurvey.Expectation.Boundary));
			// Two separate wedges
			Assert.That(SharedCornerSurvey.ExpectFace(Pattern(k => (k / 180) % 2 == 0)), Is.EqualTo(SharedCornerSurvey.Expectation.Boundary));
		}

		[Test]
		public void ExpectFace_OutlineEndingOnTheEdge_IsABoundary()
		{
			// Half the circle is off the face; the drawn part ends on the edge at the corner, even when the
			// line it ends along is straight
			Assert.That(SharedCornerSurvey.ExpectFace(Pattern(k => k < 360 ? (bool?)null : k < 540)), Is.EqualTo(SharedCornerSurvey.Expectation.Boundary));
		}
		#endregion

		#region Judging a side
		[Test]
		public void JudgeSide_OnlyFlagsAFaceThatShouldBeGone_OrACornerThatShouldBeThere()
		{
			Assert.That(SharedCornerSurvey.JudgeSide(SharedCornerSurvey.Expectation.NoFace, drewNearCorner: false), Is.Null);
			Assert.That(SharedCornerSurvey.JudgeSide(SharedCornerSurvey.Expectation.NoFace, drewNearCorner: true), Is.Not.Null);
			Assert.That(SharedCornerSurvey.JudgeSide(SharedCornerSurvey.Expectation.Boundary, drewNearCorner: true), Is.Null);
			Assert.That(SharedCornerSurvey.JudgeSide(SharedCornerSurvey.Expectation.Boundary, drewNearCorner: false), Is.Not.Null);
			// Either is fine where a vertex is allowed but not needed
			foreach (var optional in new[] { SharedCornerSurvey.Expectation.DrawnThrough, SharedCornerSurvey.Expectation.StraightBoundary })
			{
				Assert.That(SharedCornerSurvey.JudgeSide(optional, drewNearCorner: false), Is.Null, optional.ToString());
				Assert.That(SharedCornerSurvey.JudgeSide(optional, drewNearCorner: true), Is.Null, optional.ToString());
			}
		}
		#endregion

		#region From the oracle
		// Two unit-offset cubes: at every corner where their surfaces cross, both outlines bend.
		[Test]
		public void Expect_OverlappingBoxes_HaveABoundaryOnBothSides()
		{
			var scene = new ContentsScene()
				.Add(ContentsSceneNode.Brush(ContentsScene.BoxPlanes(new float3(0), new float3(2)), name: "a"))
				.Add(ContentsSceneNode.Brush(ContentsScene.BoxPlanes(new float3(1), new float3(3)), name: "b"));
			var oracle  = new ContentsOracle(scene);
			var brushes = oracle.Brushes;
			var corners = SharedCornerSurvey.SharedCorners(brushes.Select(oracle.TreePlanesOf).ToList());
			Assert.That(corners.Count, Is.EqualTo(6));
			foreach (var corner in corners)
			{
				Assert.That(SharedCornerSurvey.Expect(oracle, brushes[corner.brushA], corner.planesA, corner.position),
							Is.EqualTo(SharedCornerSurvey.Expectation.Boundary), "a at " + corner.position);
				Assert.That(SharedCornerSurvey.Expect(oracle, brushes[corner.brushB], corner.planesB, corner.position),
							Is.EqualTo(SharedCornerSurvey.Expectation.Boundary), "b at " + corner.position);
			}
		}

		// The same crossing buried deep inside a third solid brush: nothing may be drawn there.
		[Test]
		public void Expect_CrossingInsideASolid_HasNoFaceOnEitherSide()
		{
			var scene = new ContentsScene()
				.Add(ContentsSceneNode.Brush(ContentsScene.BoxPlanes(new float3(0), new float3(2)), name: "a"))
				.Add(ContentsSceneNode.Brush(ContentsScene.BoxPlanes(new float3(1), new float3(3)), name: "b"))
				.Add(ContentsSceneNode.Brush(ContentsScene.BoxPlanes(new float3(1.5f, 0.5f, 0.5f), new float3(2.5f, 1.5f, 1.5f)), name: "cover"));
			var oracle  = new ContentsOracle(scene);
			var brushes = oracle.Brushes;
			var corners = SharedCornerSurvey.SharedCorners(brushes.Select(oracle.TreePlanesOf).ToList());
			var corner  = corners.First(c => c.brushA == 0 && c.brushB == 1 && math.distance(c.position, new float3(2, 1, 1)) < 0.00001f);
			Assert.That(corner.isolated, Is.True, "the cover's faces are half a unit away");
			Assert.That(SharedCornerSurvey.Expect(oracle, brushes[0], corner.planesA, corner.position), Is.EqualTo(SharedCornerSurvey.Expectation.NoFace));
			Assert.That(SharedCornerSurvey.Expect(oracle, brushes[1], corner.planesB, corner.position), Is.EqualTo(SharedCornerSurvey.Expectation.NoFace));
		}

		// A carve that comes before the brush it crosses carves nothing of it: the later brush's face is
		// drawn straight through the crossing, and the carve leaves no wall where nothing was filled.
		[Test]
		public void Expect_CarveBeforeTheBrushItCrosses_LeavesTheLaterFaceDrawnThrough()
		{
			var scene = new ContentsScene()
				.Add(ContentsSceneNode.Brush(ContentsScene.BoxPlanes(new float3(-5), new float3(-4)), name: "base"))
				.Add(ContentsSceneNode.Brush(ContentsScene.BoxPlanes(new float3(0), new float3(2)), operation: CSGOperationType.Subtractive, name: "carve"))
				.Add(ContentsSceneNode.Brush(ContentsScene.BoxPlanes(new float3(1), new float3(3)), name: "fill"));
			var oracle  = new ContentsOracle(scene);
			var brushes = oracle.Brushes;
			var corners = SharedCornerSurvey.SharedCorners(brushes.Select(oracle.TreePlanesOf).ToList());
			// (1, 1, 2): the carve's top face against the fill's -x and -y faces, where the fill's edge
			// crosses the carve's face
			var corner  = corners.First(c => c.brushA == 1 && c.brushB == 2 && math.distance(c.position, new float3(1, 1, 2)) < 0.00001f);
			Assert.That(SharedCornerSurvey.Expect(oracle, brushes[1], corner.planesA, corner.position), Is.EqualTo(SharedCornerSurvey.Expectation.NoFace), "carve");
			Assert.That(SharedCornerSurvey.Expect(oracle, brushes[2], corner.planesB, corner.position), Is.EqualTo(SharedCornerSurvey.Expectation.DrawnThrough), "fill");
		}
		#endregion
	}
}
