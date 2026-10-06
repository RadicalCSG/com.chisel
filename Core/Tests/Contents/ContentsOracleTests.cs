using NUnit.Framework;
using Unity.Mathematics;

namespace Chisel.Core.Tests
{
	// The oracle is the reference the whole contents suite is judged against, so it gets its own
	// tests: every rule from the design, stated as a scene whose answer can be read off by hand.
	[TestFixture]
	[Category("Contents")]
	public class ContentsOracleTests
	{
		const int kSolid = 0;
		const int kGlass = 1;
		const int kWater = 2;

		// Well above the 12.5 mm weld distance, so no probe lands in the band where welding decides.
		const float kOffset = 0.05f;

		static ContentsSceneNode Box(float3 min, float3 max, int contents = kSolid,
									 CSGOperationType operation = CSGOperationType.Additive, string name = null)
		{
			return ContentsSceneNode.Brush(ContentsScene.BoxPlanes(min, max), contents, operation, name: name);
		}

		// Index of the +y plane in ContentsScene.BoxPlanes.
		const int kPlaneMaxY = 3;
		const int kPlaneMinY = 2;

		[Test]
		public void SolidFaceAgainstGlass_IsDrawn()
		{
			// The bm_c0a0a case: a metal post with a glass pane ending flush against it.
			var post = Box(new float3(-1, -1, 0), new float3(1, 1, 4), kSolid, name: "post");
			var pane = Box(new float3(-0.5f, 1, 0), new float3(0.5f, 5, 4), kGlass, name: "pane");
			var oracle = new ContentsOracle(new ContentsScene().Add(post).Add(pane));

			// Right where the pane covers the post's face, which is exactly what Chisel drops today.
			var onFace = new float3(0, 1, 2);
			Assert.That(oracle.JudgeFace(post, kPlaneMaxY, onFace, kOffset), Is.EqualTo(ContentsFaceVerdict.FacingOut));
		}

		[Test]
		public void GlassFaceAgainstSolid_IsRemoved()
		{
			var post = Box(new float3(-1, -1, 0), new float3(1, 1, 4), kSolid, name: "post");
			var pane = Box(new float3(-0.5f, 1, 0), new float3(0.5f, 5, 4), kGlass, name: "pane");
			var oracle = new ContentsOracle(new ContentsScene().Add(post).Add(pane));

			// The pane's end face sits inside the post's half space: Solid removes every type.
			var onFace = new float3(0, 1, 2);
			Assert.That(oracle.JudgeFace(pane, kPlaneMinY, onFace, kOffset), Is.EqualTo(ContentsFaceVerdict.None));
		}

		[Test]
		public void FaceInsideItsOwnType_IsRemoved()
		{
			var left  = Box(new float3(-2, -1, 0), new float3(0, 1, 4), kGlass, name: "left");
			var right = Box(new float3(0, -1, 0), new float3(2, 1, 4), kGlass, name: "right");
			var oracle = new ContentsOracle(new ContentsScene().Add(left).Add(right));

			// Glass in glass gets removed, exactly as solid in solid does today.
			var onFace = new float3(0, 0, 2);
			Assert.That(oracle.JudgeFace(left, 1, onFace, kOffset), Is.EqualTo(ContentsFaceVerdict.None));
		}

		[Test]
		public void SolidWallInsideWater_StaysVisible()
		{
			var water = Box(new float3(-5, -5, 0), new float3(5, 5, 4), kWater, name: "water");
			var wall  = Box(new float3(-1, -1, 0), new float3(1, 1, 4), kSolid, name: "wall");
			var oracle = new ContentsOracle(new ContentsScene().Add(water).Add(wall));

			var onFace = new float3(0, 1, 2);
			Assert.That(oracle.JudgeFace(wall, kPlaneMaxY, onFace, kOffset), Is.EqualTo(ContentsFaceVerdict.FacingOut));
		}

		[Test]
		public void GlassAgainstWater_KeepsBothFaces()
		{
			var glass = Box(new float3(-2, -1, 0), new float3(0, 1, 4), kGlass, name: "glass");
			var water = Box(new float3(0, -1, 0), new float3(2, 1, 4), kWater, name: "water");
			var oracle = new ContentsOracle(new ContentsScene().Add(glass).Add(water));

			var onFace = new float3(0, 0, 2);
			Assert.That(oracle.JudgeFace(glass, 1, onFace, kOffset), Is.EqualTo(ContentsFaceVerdict.FacingOut), "glass side");
			Assert.That(oracle.JudgeFace(water, 0, onFace, kOffset), Is.EqualTo(ContentsFaceVerdict.FacingOut), "water side");
		}

		[Test]
		public void CarvedWall_FacesIntoTheCarve()
		{
			var solid = Box(new float3(-2, -2, 0), new float3(2, 2, 4), kSolid, name: "solid");
			var notch = Box(new float3(-1, 1, 0), new float3(1, 3, 4), kSolid, CSGOperationType.Subtractive, "notch");
			var oracle = new ContentsOracle(new ContentsScene().Add(solid).Add(notch));

			// The notch's -y face at y = 1 is the wall the carve leaves: empty behind it, filled ahead.
			var onFace = new float3(0, 1, 2);
			Assert.That(oracle.JudgeFace(notch, kPlaneMinY, onFace, kOffset), Is.EqualTo(ContentsFaceVerdict.FacingIn));
		}

		[Test]
		public void CarvingBrushOfAnotherType_StillCarvesSolid()
		{
			var solid = Box(new float3(-2, -2, 0), new float3(2, 2, 4), kSolid, name: "solid");
			var notch = Box(new float3(-1, 1, 0), new float3(1, 3, 4), kWater, CSGOperationType.Subtractive, "notch");
			var oracle = new ContentsOracle(new ContentsScene().Add(solid).Add(notch));

			// A carving brush carves every type, and its wall belongs to its own type - so a Water
			// carve leaves walls where it cuts solid, which is a known limit of the design.
			var onFace = new float3(0, 1, 2);
			Assert.That(oracle.JudgeFace(notch, kPlaneMinY, onFace, kOffset), Is.EqualTo(ContentsFaceVerdict.FacingIn));
			Assert.That(oracle.IsCarving(notch), Is.True);
		}

		[Test]
		public void BrushInsideSubtractiveComposite_Carves()
		{
			var solid = Box(new float3(-2, -2, 0), new float3(2, 2, 4), kSolid, name: "solid");
			var inner = Box(new float3(-1, 1, 0), new float3(1, 3, 4), kGlass, name: "inner");
			var cut   = ContentsSceneNode.Composite(CSGOperationType.Subtractive, inner);
			var oracle = new ContentsOracle(new ContentsScene().Add(solid).Add(cut));

			Assert.That(oracle.IsCarving(inner), Is.True, "a brush inside a subtractive composite carves");
			var onFace = new float3(0, 1, 2);
			Assert.That(oracle.JudgeFace(inner, kPlaneMinY, onFace, kOffset), Is.EqualTo(ContentsFaceVerdict.FacingIn));
		}

		[Test]
		public void EverythingSolid_MatchesTodaysAnswers()
		{
			// The control every generated scene also runs: with one type everywhere, the oracle must
			// give the plain CSG answer - here, no face at a contact between two brushes.
			var left  = Box(new float3(-2, -1, 0), new float3(0, 1, 4), kSolid, name: "left");
			var right = Box(new float3(0, -1, 0), new float3(2, 1, 4), kSolid, name: "right");
			var oracle = new ContentsOracle(new ContentsScene().Add(left).Add(right));

			var onFace = new float3(0, 0, 2);
			Assert.That(oracle.JudgeFace(left, 1, onFace, kOffset), Is.EqualTo(ContentsFaceVerdict.None));
			Assert.That(oracle.JudgeFace(right, 0, onFace, kOffset), Is.EqualTo(ContentsFaceVerdict.None));
		}

		[Test]
		public void OuterFacesAreUnaffectedByOtherTypes()
		{
			// Separation, stated on one face: a brush of another type touching nothing changes nothing.
			var solid = Box(new float3(-1, -1, 0), new float3(1, 1, 4), kSolid, name: "solid");
			var far   = Box(new float3(10, 10, 0), new float3(12, 12, 4), kWater, name: "far");
			var alone = new ContentsOracle(new ContentsScene().Add(solid));
			var both  = new ContentsOracle(new ContentsScene().Add(solid).Add(far));

			var onFace = new float3(0, 1, 2);
			Assert.That(both.JudgeFace(solid, kPlaneMaxY, onFace, kOffset),
						Is.EqualTo(alone.JudgeFace(solid, kPlaneMaxY, onFace, kOffset)));
		}

		[Test]
		public void TieOrder_SolidFirstThenListOrder()
		{
			Assert.That(ContentsOracle.WinsTie(kSolid, kGlass), Is.True,  "Solid wins every tie");
			Assert.That(ContentsOracle.WinsTie(kGlass, kSolid), Is.False);
			Assert.That(ContentsOracle.WinsTie(kGlass, kWater), Is.True,  "the earlier list entry wins");
			Assert.That(ContentsOracle.WinsTie(kWater, kGlass), Is.False);
			Assert.That(ContentsOracle.WinsTie(kGlass, kGlass), Is.False, "same type: either may draw");
		}
	}
}
