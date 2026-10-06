using NUnit.Framework;
using Unity.Mathematics;

namespace Chisel.Core.Tests
{
	[TestFixture]
	[Category("Contents")]
	public class FlushContactFaceTests
	{
		static readonly float4[] kSlab = {
			new(-0.000000000f, +1.000000000f, +0.000000000f, -20.358750000f),
			new(+0.000000000f, -1.000000000f, -0.000000000f, +19.596250000f),
			new(-0.866081530f, +0.000124976f, +0.499902759f, -101.658394691f),
			new(+0.866003425f, +0.000000000f, -0.500038066f, +100.398516838f),
			new(-0.500290606f, -0.000216464f, -0.865857530f, -242.318658487f),
			new(+0.500173430f, +0.000000000f, +0.865925251f, +241.699207126f),
		};
		static readonly float4[] kBrush7339 = {
			new(+0.499722453f, +0.000000000f, +0.866185586f, +241.608352902f),
			new(+0.865768711f, +0.000000000f, -0.500444342f, +100.646395536f),
			new(-0.865768711f, +0.000000000f, +0.500444342f, -100.760834020f),
			new(-0.000000000f, +1.000000000f, +0.000000000f, -20.358750000f),
			new(+0.000000000f, -1.000000000f, +0.000000000f, +19.748750000f),
			new(-0.499722453f, -0.000000000f, -0.866185586f, -241.646444038f),
		};
		static readonly float4[] kBrush7340 = {
			new(+0.503088273f, -0.124296091f, +0.855250064f, +243.063754992f),
			new(-0.867013944f, +0.000000000f, +0.498283876f, -101.248869699f),
			new(+0.864507187f, +0.000000000f, -0.502620457f, +99.962850708f),
			new(-0.000000000f, +1.000000000f, +0.000000000f, -19.901250000f),
			new(-0.507020127f, -0.000000000f, -0.861934215f, -242.489550973f),
		};
		static readonly float4[] kBrush7341 = {
			new(+0.507020127f, -0.000000000f, +0.861934215f, +242.470423797f),
			new(-0.000000000f, +1.000000000f, +0.000000000f, -20.320625000f),
			new(+0.867013944f, +0.000000000f, -0.498283876f, +101.173679285f),
			new(-0.867013944f, +0.000000000f, +0.498283876f, -101.248869699f),
			new(+0.000000000f, -1.000000000f, +0.000000000f, +19.901250000f),
			new(-0.507020127f, +0.000000000f, -0.861934215f, -242.489550973f),
		};
		static readonly float4[] kBrush7342 = {
			new(+0.499722453f, +0.000000000f, +0.866185586f, +241.608670434f),
			new(+0.865768711f, +0.000000000f, -0.500444342f, +100.837676467f),
			new(-0.865768711f, +0.000000000f, +0.500444342f, -100.952114951f),
			new(-0.000000000f, +1.000000000f, +0.000000000f, -20.358750000f),
			new(+0.000000000f, -1.000000000f, +0.000000000f, +19.748750000f),
			new(-0.499722453f, -0.000000000f, -0.866185586f, -241.646761570f),
		};
		static readonly float4[] kBrush7343 = {
			new(+0.492291101f, -0.124301967f, +0.861509427f, +241.811116845f),
			new(-0.867013944f, +0.000000000f, +0.498283876f, -101.440151225f),
			new(+0.867013944f, +0.000000000f, -0.498283876f, +101.363308066f),
			new(-0.000000000f, +1.000000000f, +0.000000000f, -19.901250000f),
			new(-0.496138938f, -0.000000000f, -0.868243142f, -241.227184908f),
		};
		static readonly float4[] kBrush7344 = {
			new(+0.496138938f, -0.000000000f, +0.868243142f, +241.208056814f),
			new(-0.000000000f, +1.000000000f, +0.000000000f, -20.320625000f),
			new(+0.867013944f, +0.000000000f, -0.498283876f, +101.363308066f),
			new(-0.867013944f, +0.000000000f, +0.498283876f, -101.440151225f),
			new(+0.000000000f, -1.000000000f, +0.000000000f, +19.901250000f),
			new(-0.496138938f, +0.000000000f, -0.868243142f, -241.227184908f),
		};
		static readonly float4[] kBrush7345 = {
			new(+0.499722453f, +0.000000000f, +0.866185586f, +241.608987966f),
			new(+0.865768711f, +0.000000000f, -0.500444342f, +101.028957398f),
			new(-0.865768711f, +0.000000000f, +0.500444342f, -101.143395882f),
			new(-0.000000000f, +1.000000000f, +0.000000000f, -20.358750000f),
			new(+0.000000000f, -1.000000000f, +0.000000000f, +19.748750000f),
			new(-0.499722453f, -0.000000000f, -0.866185586f, -241.647079102f),
		};
		static readonly float4[] kBrush7346 = {
			new(+0.492817596f, -0.124455104f, +0.861186242f, +241.872141459f),
			new(-0.867013944f, +0.000000000f, +0.498283876f, -101.631432751f),
			new(+0.867013944f, +0.000000000f, -0.498283876f, +101.554589591f),
			new(-0.000000000f, +1.000000000f, +0.000000000f, -19.901250000f),
			new(-0.496138938f, -0.000000000f, -0.868243142f, -241.226712026f),
		};
		static readonly float4[] kBrush7347 = {
			new(+0.496138938f, -0.000000000f, +0.868243142f, +241.207583931f),
			new(-0.000000000f, +1.000000000f, +0.000000000f, -20.320625000f),
			new(+0.867013944f, +0.000000000f, -0.498283876f, +101.554589591f),
			new(-0.867013944f, +0.000000000f, +0.498283876f, -101.631432751f),
			new(+0.000000000f, -1.000000000f, +0.000000000f, +19.901250000f),
			new(-0.496138938f, +0.000000000f, -0.868243142f, -241.226712026f),
		};
		static readonly float4[] kBrush7348 = {
			new(+0.499722453f, +0.000000000f, +0.866185586f, +241.608352902f),
			new(+0.865768711f, +0.000000000f, -0.500444342f, +101.218587958f),
			new(-0.865768711f, +0.000000000f, +0.500444342f, -101.333026442f),
			new(-0.000000000f, +1.000000000f, +0.000000000f, -20.358750000f),
			new(+0.000000000f, -1.000000000f, +0.000000000f, +19.748750000f),
			new(-0.499722453f, -0.000000000f, -0.866185586f, -241.646444038f),
		};
		static readonly float4[] kBrush7349 = {
			new(+0.503078551f, -0.124449057f, +0.855233537f, +243.066945139f),
			new(-0.867013944f, +0.000000000f, +0.498283876f, -101.821061532f),
			new(+0.867013944f, +0.000000000f, -0.498283876f, +101.745871117f),
			new(-0.000000000f, +1.000000000f, +0.000000000f, -19.901250000f),
			new(-0.507020127f, -0.000000000f, -0.861934215f, -242.494383508f),
		};
		static readonly float4[] kBrush7350 = {
			new(+0.507020127f, -0.000000000f, +0.861934215f, +242.475256333f),
			new(-0.000000000f, +1.000000000f, +0.000000000f, -20.320625000f),
			new(+0.867013944f, +0.000000000f, -0.498283876f, +101.745871117f),
			new(-0.867013944f, +0.000000000f, +0.498283876f, -101.821061532f),
			new(+0.000000000f, -1.000000000f, +0.000000000f, +19.901250000f),
			new(-0.507020127f, +0.000000000f, -0.861934215f, -242.494383508f),
		};
		static readonly float4[] kBrush7351 = {
			new(+0.499722453f, +0.000000000f, +0.866185586f, +241.608987966f),
			new(+0.865768711f, +0.000000000f, -0.500444342f, +100.456764977f),
			new(-0.865768711f, +0.000000000f, +0.500444342f, -100.571203461f),
			new(-0.000000000f, +1.000000000f, +0.000000000f, -20.358750000f),
			new(+0.000000000f, -1.000000000f, +0.000000000f, +19.748750000f),
			new(-0.499722453f, -0.000000000f, -0.866185586f, -241.647079102f),
		};
		static readonly float4[][] kNeighbours =
		{
			kBrush7339, kBrush7340, kBrush7341, kBrush7342, kBrush7343, kBrush7344, kBrush7345, kBrush7346, kBrush7347, kBrush7348, kBrush7349, kBrush7350, kBrush7351
		};

		// Surface 6 is the slab's sixth side, in the VMF's own order
		static float3 FaceNormal => kSlab[5].xyz;

		static ContentsSceneNode Brush(string name, float4[] planes) => ContentsSceneNode.Brush(planes, name: name);

		static ContentsTreeHarness BuildAndUpdate(ContentsScene scene)
		{
			var harness = ContentsTreeHarness.Build(scene);
			Assert.That(harness.Update(), Is.True, "the CSG update did not run");
			Assert.That(harness.Delivered, Is.True, "no meshes were delivered: " + harness.LastUpdateReport);
			return harness;
		}

		// How much of surface 6 the slab draws, in square metres
		static double FaceArea(ContentsTreeHarness harness, ContentsSceneNode slab)
		{
			double area = 0;
			foreach (var triangle in harness.Triangles)
			{
				if (harness.NodeOf(triangle.brushID) != slab)
					continue;
				if (math.dot(triangle.surfaceNormal, FaceNormal) < 0.999f)
					continue;
				area += math.length(math.cross((double3)triangle.b - triangle.a, (double3)triangle.c - triangle.a)) * 0.5;
			}
			return area;
		}

		// On its own the slab draws the whole wall: 40 units tall by about 64 long, so roughly 0.76 x 1.22 m
		[Test]
		public void TheSlabAlone_DrawsSurface6()
		{
			var slab = Brush("slab", kSlab);
			using var harness = BuildAndUpdate(new ContentsScene().Add(slab));
			Assert.That(FaceArea(harness, slab), Is.GreaterThan(0.5), "the slab does not draw surface 6 even by itself");
		}

		// The area the slab keeps when it stands with these neighbours, against the area it keeps alone
		static (double kept, double alone) WallAgainst(params int[] neighbours)
		{
			double alone;
			{
				var only = Brush("slab", kSlab);
				using var harness = BuildAndUpdate(new ContentsScene().Add(only));
				alone = FaceArea(harness, only);
			}

			var slab = Brush("slab", kSlab);
			var scene = new ContentsScene().Add(slab);
			foreach (var i in neighbours)
				scene.Add(Brush($"neighbour {i}", kNeighbours[i]));
			using var withNeighbours = BuildAndUpdate(scene);
			return (FaceArea(withNeighbours, slab), alone);
		}

		const double kAreaNoise = 1e-3;
		static void TheyTakeNoMoreThanTheyDoSeparately(params int[] neighbours)
		{
			var report = new System.Text.StringBuilder();
			double alone = 0, budget = 0;
			foreach (var i in neighbours)
			{
				var (keptAlone, aloneArea) = WallAgainst(i);
				alone = aloneArea;
				budget += aloneArea - keptAlone;
				report.AppendLine($"   neighbour {i} alone takes {aloneArea - keptAlone:0.0000} m2");
			}

			var (kept, _) = WallAgainst(neighbours);
			var taken = alone - kept;
			report.AppendLine($"   together they take {taken:0.0000} m2, and may take at most {budget:0.0000}");
			Assert.That(taken, Is.LessThanOrEqualTo(budget + (alone * kAreaNoise)),
				$"surface 6 keeps {kept:0.0000} m2 of {alone:0.0000}: the neighbours took {taken:0.0000} m2 of wall " +
				$"between them, more than the {budget:0.0000} m2 they take one at a time, so the loss is not geometry:"
				+ System.Environment.NewLine + report);
		}

		[Test]
		public void EachNeighbourOnItsOwn_TakesOnlyItsOwnSliver()
		{
			var report = new System.Text.StringBuilder();
			bool failed = false;
			for (int i = 0; i < kNeighbours.Length; i++)
			{
				var (kept, alone) = WallAgainst(i);
				report.AppendLine($"   slab + neighbour {i}: {kept:0.0000} m2 of {alone:0.0000}");
				failed |= kept <= alone * 0.9;
			}
			if (failed)
				Assert.Fail("a single neighbour took more than a tenth of the wall:" + System.Environment.NewLine + report);
		}

		[Test]
		public void TwoOfThemTogether_TakeNoMoreThanTheyDoSeparately()
		{
			TheyTakeNoMoreThanTheyDoSeparately(0, 1);
		}

		// The same pair, with the wedge added before the post. Ordering decides which brush is "later" in the tree, and
		// so which way the contents rules resolve a tie - the defect must not depend on it.
		[Test]
		public void TwoOfThemTheOtherWayAround_TakeNoMoreThanTheyDoSeparately()
		{
			TheyTakeNoMoreThanTheyDoSeparately(1, 0);
		}

		[Test]
		public void TheNeighboursCrossingIt_TakeNoMoreThanTheyDoSeparately()
		{
			var all = new int[kNeighbours.Length];
			for (int i = 0; i < all.Length; i++)
				all[i] = i;
			TheyTakeNoMoreThanTheyDoSeparately(all);
		}

		[Test]
		public void EveryPairOfThem_TakesNoMoreThanItsTwoDoSeparately()
		{
			var alone = new double[kNeighbours.Length];
			double whole = 0;
			for (int i = 0; i < kNeighbours.Length; i++)
			{
				var (kept, aloneArea) = WallAgainst(i);
				alone[i] = aloneArea - kept;
				whole = aloneArea;
			}

			var report = new System.Text.StringBuilder();
			int broken = 0;
			for (int i = 0; i < kNeighbours.Length; i++)
			{
				for (int j = i + 1; j < kNeighbours.Length; j++)
				{
					var (kept, _) = WallAgainst(i, j);
					var taken = whole - kept;
					var budget = alone[i] + alone[j];
					if (taken <= budget + (whole * kAreaNoise))
						continue;
					broken++;
					report.AppendLine($"   {i} and {j} take {taken:0.0000} m2 where {budget:0.0000} is all they account for");
				}
			}
			if (broken > 0)
				Assert.Fail($"{broken} of the 78 pairs took more wall than the two brushes take one at a time:"
					+ System.Environment.NewLine + report);
		}

	}
}
