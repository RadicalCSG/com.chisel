using System.Collections.Generic;
using NUnit.Framework;
using Unity.Mathematics;

namespace Chisel.Core.Tests
{
	[TestFixture]
	[Category("Contents")]
	public class LightmapUVPipelineTests
	{
		static ContentsSceneNode Box(string name, float3 min, float3 max, CSGOperationType operation = CSGOperationType.Additive)
		{
			return ContentsSceneNode.Brush(ContentsScene.BoxPlanes(min, max), operation: operation, name: name);
		}

		// A floor with a wall standing on it, a window cut through the wall, and a wedge: surfaces cut into pieces, one with a
		// hole, and one in no axis' plane
		static ContentsScene Room()
		{
			return new ContentsScene()
				.Add(Box("floor", new float3(-4, -1, -4), new float3(4, 0, 4)))
				.Add(Box("wall", new float3(-3, 0, -0.25f), new float3(3, 3, 0.25f)))
				.Add(Box("window", new float3(-1, 1, -1), new float3(1, 2, 1), CSGOperationType.Subtractive))
				.Add(ContentsSceneNode.Brush(ContentsScene.WedgePlanes(new float3(1.5f, 0, 2), new float3(3.5f, 2, 3.5f)), name: "wedge"));
		}

		static ContentsTreeHarness BuildAndUpdate(ContentsScene scene)
		{
			var harness = ContentsTreeHarness.Build(scene);
			Assert.That(harness.Update(), Is.True, "the CSG update did not run");
			Assert.That(harness.Delivered, Is.True, "no meshes were delivered: " + harness.LastUpdateReport);
			Assert.That(harness.Triangles.Count, Is.GreaterThan(0), "nothing was drawn");
			return harness;
		}

		static double Area(float3 a, float3 b, float3 c) => math.length(math.cross((double3)b - a, (double3)c - a)) * 0.5;
		static double Area(float2 a, float2 b, float2 c) => math.abs(((double)b.x - a.x) * ((double)c.y - a.y) - ((double)c.x - a.x) * ((double)b.y - a.y)) * 0.5;

		// The surface a triangle belongs to: its brush and its plane (the normal the pipeline gave it is exact even on a sliver)
		static (CompactNodeID, int3, int) SurfaceOf(in ContentsTreeHarness.CapturedTriangle triangle)
		{
			var normal = triangle.surfaceNormal;
			return (triangle.brushID, (int3)math.round(normal * 1000), (int)math.round(math.dot(normal, triangle.a) * 1000));
		}

		[Test]
		public void EveryTriangle_HasLightmapCoordinatesInsideTheUnitSquare()
		{
			using var harness = BuildAndUpdate(Room());
			foreach (var triangle in harness.Triangles)
			{
				foreach (var uv in new[] { triangle.lightmapA, triangle.lightmapB, triangle.lightmapC })
					Assert.That(math.all(uv >= 0) && math.all(uv <= 1), Is.True, $"lightmap coordinate {uv} of a triangle at {triangle.Center}");
			}
		}

		// Two surfaces never share texels: each lies in a cell of its own, so their triangles' boxes in the lightmap are apart
		[Test]
		public void DifferentSurfaces_DoNotOverlapInTheLightmap()
		{
			using var harness = BuildAndUpdate(Room());
			var triangles = harness.Triangles;
			for (int i = 0; i < triangles.Count; i++)
			{
				var a = triangles[i];
				var boxA = new float4(math.min(math.min(a.lightmapA, a.lightmapB), a.lightmapC), math.max(math.max(a.lightmapA, a.lightmapB), a.lightmapC));
				for (int j = i + 1; j < triangles.Count; j++)
				{
					var b = triangles[j];
					if (a.section != b.section || SurfaceOf(a).Equals(SurfaceOf(b)))
						continue;
					var boxB = new float4(math.min(math.min(b.lightmapA, b.lightmapB), b.lightmapC), math.max(math.max(b.lightmapA, b.lightmapB), b.lightmapC));
					var apart = math.any(boxA.zw <= boxB.xy) || math.any(boxB.zw <= boxA.xy);
					Assert.That(apart, Is.True, $"triangles at {a.Center} and {b.Center} share lightmap texels ({boxA} and {boxB})");
				}
			}
		}

		// Every surface gets the same texels per unit of area, the sloped one included: charts are the surfaces laid flat,
		// neither stretched nor scaled apart from each other
		[Test]
		public void EverySurface_GetsTheSameTexelDensity()
		{
			using var harness = BuildAndUpdate(Room());
			var densities = new Dictionary<int, double>();
			foreach (var triangle in harness.Triangles)
			{
				var area = Area(triangle.a, triangle.b, triangle.c);
				if (area < 1e-3)
					continue;
				var density = Area(triangle.lightmapA, triangle.lightmapB, triangle.lightmapC) / area;
				if (!densities.TryGetValue(triangle.section, out var expected))
				{
					densities[triangle.section] = density;
					continue;
				}
				Assert.That(density, Is.EqualTo(expected).Within(0.5).Percent, $"the triangle at {triangle.Center}");
			}
		}

		// The same brushes get the same lightmap coordinates every time they are built, so a bake stays valid after a reload
		[Test]
		public void TheSameScene_GetsTheSameLightmapCoordinates()
		{
			var first = new Dictionary<(int3, int3, int3), (float2, float2, float2)>();
			int firstCount;
			using (var harness = BuildAndUpdate(Room()))
			{
				foreach (var triangle in harness.Triangles)
					first[KeyOf(triangle)] = (triangle.lightmapA, triangle.lightmapB, triangle.lightmapC);
				firstCount = harness.Triangles.Count;
			}

			using var again = BuildAndUpdate(Room());
			Assert.AreEqual(firstCount, again.Triangles.Count, "the same triangles");
			foreach (var triangle in again.Triangles)
			{
				Assert.That(first.TryGetValue(KeyOf(triangle), out var expected), Is.True, $"the triangle at {triangle.Center} is new");
				Assert.That(expected, Is.EqualTo((triangle.lightmapA, triangle.lightmapB, triangle.lightmapC)), $"the triangle at {triangle.Center}");
			}
		}

		static (int3, int3, int3) KeyOf(in ContentsTreeHarness.CapturedTriangle triangle)
		{
			return ((int3)math.round(triangle.a * 1000), (int3)math.round(triangle.b * 1000), (int3)math.round(triangle.c * 1000));
		}
	}
}
