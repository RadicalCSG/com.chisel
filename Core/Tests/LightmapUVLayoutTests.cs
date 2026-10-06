using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;

namespace Chisel.Core.Tests
{
	[TestFixture]
	public class LightmapUVLayoutTests
	{
		static readonly LightmapUVSettings kSettings = new() { texelsPerUnit = 10, paddingTexels = 2 };

		static LightmapChart Chart(float2 min, float2 size, ulong key, LightmapChartMode mode = LightmapChartMode.Texels)
		{
			return new LightmapChart { rect = new float4(min, min + size), mode = mode, key = key };
		}

		static int Layout(LightmapChart[] charts, LightmapUVSettings settings, out LightmapChartPlacement[] placements)
		{
			using var chartArray     = new NativeArray<LightmapChart>(charts, Allocator.Temp);
			using var placementArray = new NativeArray<LightmapChartPlacement>(charts.Length, Allocator.Temp);
			var side = LightmapUVLayout.Layout(chartArray, settings, placementArray);
			placements = placementArray.ToArray();
			return side;
		}

		// The texels a chart covers in the layout: the rectangle its corners go to
		static float4 Covered(LightmapChart chart, LightmapChartPlacement placement)
		{
			var a = placement.Place(chart.rect.xy, chart.rect);
			var b = placement.Place(chart.rect.zw, chart.rect);
			var c = placement.Place(chart.rect.xw, chart.rect);
			var d = placement.Place(chart.rect.zy, chart.rect);
			return new float4(math.min(math.min(a, b), math.min(c, d)), math.max(math.max(a, b), math.max(c, d)));
		}

		static LightmapChart[] RandomCharts(uint seed, int count, bool outliers = true)
		{
			var random = new Random(seed);
			var charts = new LightmapChart[count];
			for (int i = 0; i < count; i++)
			{
				// Mostly small surfaces, some long thin ones and a few large ones, as a level has
				var size = random.NextFloat2(new float2(0.01f), new float2(2));
				if (outliers && random.NextInt(8) == 0) size.x *= 10;
				if (outliers && random.NextInt(20) == 0) size *= 5;
				charts[i] = Chart(random.NextFloat2(new float2(-50), new float2(50)), size, (ulong)i + 1);
			}
			return charts;
		}

		[Test]
		public void Charts_StayInsideTheSquare_AndKeepThePaddingBetweenThem([Values(1u, 2u, 3u, 4u, 5u)] uint seed)
		{
			var charts = RandomCharts(seed, 300);
			var side = Layout(charts, kSettings, out var placements);
			Assert.That(side, Is.GreaterThan(0));

			var covered = new float4[charts.Length];
			for (int i = 0; i < charts.Length; i++)
			{
				covered[i] = Covered(charts[i], placements[i]);
				Assert.That(math.all(covered[i].xy >= -1e-3f) && math.all(covered[i].zw <= side + 1e-3f), Is.True,
							$"chart {i} at {covered[i]} is outside the {side} texel square");
			}
			for (int i = 0; i < charts.Length; i++)
			{
				for (int j = i + 1; j < charts.Length; j++)
				{
					// Apart by at least the padding along one axis
					var gap = math.max(covered[j].xy - covered[i].zw, covered[i].xy - covered[j].zw);
					Assert.That(math.cmax(gap), Is.GreaterThanOrEqualTo(kSettings.paddingTexels - 1e-3f),
								$"charts {i} ({covered[i]}) and {j} ({covered[j]}) are closer than the padding");
				}
			}
		}

		// A texel per tenth of a unit: a 2 by 1 chart spans 20 by 10 texels; one standing up is laid on its long side
		[Test]
		public void TexelsFollowTheSize_AndATallChartLiesOnItsLongSide()
		{
			var charts = new[] { Chart(new float2(3, 4), new float2(2, 1), 1), Chart(new float2(-7, 1), new float2(0.5f, 3), 2) };
			Layout(charts, kSettings, out var placements);

			var wide = Covered(charts[0], placements[0]);
			Assert.That(wide.zw - wide.xy, Is.EqualTo(new float2(20, 10)).Using(new Float2Comparer(1e-3f)));
			Assert.IsFalse(placements[0].rotated);

			var tall = Covered(charts[1], placements[1]);
			Assert.IsTrue(placements[1].rotated);
			Assert.That(tall.zw - tall.xy, Is.EqualTo(new float2(30, 5)).Using(new Float2Comparer(1e-3f)));
		}

		// A surface that gives off light gets one texel, however large it is
		[Test]
		public void ASingleTexelChart_FitsInOneTexel()
		{
			var charts = new[] { Chart(new float2(10, 20), new float2(8, 3), 1, LightmapChartMode.SingleTexel), Chart(new float2(0, 0), new float2(1, 1), 2) };
			Layout(charts, kSettings, out var placements);

			var single = Covered(charts[0], placements[0]);
			Assert.That(math.cmax(single.zw - single.xy), Is.EqualTo(1).Within(1e-4f), "it spans exactly one texel along its long side");
			Assert.That(math.floor(single.xy), Is.EqualTo(math.floor(single.zw - 1e-4f)), "within one texel of the grid");
		}

		// A surface without a lightmap gets no cell: nothing is laid out for it
		[Test]
		public void AChartWithoutLightmap_GetsNoTexels()
		{
			var none = new[] { Chart(new float2(1, 2), new float2(3, 4), 1, LightmapChartMode.None) };
			Assert.AreEqual(0, Layout(none, kSettings, out var placements));
			Assert.That(placements[0].Place(new float2(2, 3), none[0].rect), Is.EqualTo(float2.zero));

			var withOther = new[] { none[0], Chart(new float2(0, 0), new float2(1, 1), 2) };
			var side = Layout(withOther, kSettings, out _);
			Assert.AreEqual(10 + 2, side, "only the other chart's 10 texels and its padding");
		}

		// The layout depends on the charts, not on the order they come in: a bake stays valid when the mesh is built again
		[Test]
		public void TheSameCharts_GetTheSamePlaces_InAnyOrder()
		{
			var charts = RandomCharts(7, 200);
			// Many of the same size, which only their keys can order
			for (int i = 0; i < charts.Length; i += 3)
				charts[i].rect = new float4(charts[i].rect.xy, charts[i].rect.xy + new float2(0.5f, 0.5f));
			var side = Layout(charts, kSettings, out var placements);

			var shuffled = (LightmapChart[])charts.Clone();
			var random = new Random(99);
			for (int i = shuffled.Length - 1; i > 0; i--)
			{
				var j = random.NextInt(i + 1);
				(shuffled[i], shuffled[j]) = (shuffled[j], shuffled[i]);
			}
			var shuffledSide = Layout(shuffled, kSettings, out var shuffledPlacements);

			Assert.AreEqual(side, shuffledSide);
			for (int i = 0; i < shuffled.Length; i++)
			{
				var original = (int)shuffled[i].key - 1;
				Assert.That(shuffledPlacements[i].origin, Is.EqualTo(placements[original].origin), $"chart with key {shuffled[i].key}");
			}
		}

		// Many small charts fill most of the square they get (one very long chart would make the square at least that wide)
		[Test]
		public void TheSquare_IsMostlyFilled()
		{
			var charts = RandomCharts(11, 2000, outliers: false);
			var side = Layout(charts, kSettings, out var placements);
			var used = 0.0;
			for (int i = 0; i < charts.Length; i++)
			{
				var covered = Covered(charts[i], placements[i]);
				used += (math.ceil(covered.z - covered.x) + kSettings.paddingTexels) * (math.ceil(covered.w - covered.y) + kSettings.paddingTexels);
			}
			Assert.That(used / ((double)side * side), Is.GreaterThan(0.6), $"cells use {used} of {side}x{side} texels");
		}

		// A chart longer than any lightmap could hold still stays inside its own cell, with fewer texels per unit
		[Test]
		public void AHugeChart_StillFitsItsCell()
		{
			var charts = new[] { Chart(new float2(0, 0), new float2(10000, 2), 1), Chart(new float2(5, 5), new float2(1, 1), 2) };
			var side = Layout(charts, kSettings, out var placements);
			var huge  = Covered(charts[0], placements[0]);
			var small = Covered(charts[1], placements[1]);
			Assert.That(huge.z - huge.x, Is.LessThanOrEqualTo(LightmapUVLayout.kMaxChartTexels + 1e-2f));
			Assert.That(math.all(huge.zw <= side) && math.all(small.zw <= side), Is.True);
			var gap = math.max(small.xy - huge.zw, huge.xy - small.zw);
			Assert.That(math.cmax(gap), Is.GreaterThanOrEqualTo(kSettings.paddingTexels - 1e-2f), "the charts stay apart");
		}

		[Test]
		public void OutputFlags_SayHowManyTexelsASurfaceGets()
		{
			Assert.AreEqual(LightmapChartMode.Texels,      LightmapUVLayout.ModeOf(SurfaceOutputFlags.Default));
			Assert.AreEqual(LightmapChartMode.None,        LightmapUVLayout.ModeOf(SurfaceOutputFlags.NoLightmap));
			Assert.AreEqual(LightmapChartMode.SingleTexel, LightmapUVLayout.ModeOf(SurfaceOutputFlags.SingleLightmapTexel));
			Assert.AreEqual(LightmapChartMode.None,        LightmapUVLayout.ModeOf(SurfaceOutputFlags.NoLightmap | SurfaceOutputFlags.SingleLightmapTexel), "no lightmap wins");
		}

		// The settings a model can't use fall back to Unity's defaults
		[Test]
		public void UnusableSettings_FallBackToTheDefaults()
		{
			var usable = new LightmapUVSettings { texelsPerUnit = 0, paddingTexels = float.NaN }.Usable;
			Assert.AreEqual(LightmapUVSettings.kDefaultTexelsPerUnit, usable.texelsPerUnit);
			Assert.AreEqual(LightmapUVSettings.kDefaultPaddingTexels, usable.paddingTexels);
		}

		sealed class Float2Comparer : System.Collections.Generic.IEqualityComparer<float2>
		{
			readonly float tolerance;
			public Float2Comparer(float tolerance) { this.tolerance = tolerance; }
			public bool Equals(float2 a, float2 b) => math.all(math.abs(a - b) <= tolerance);
			public int GetHashCode(float2 value) => 0;
		}
	}
}
