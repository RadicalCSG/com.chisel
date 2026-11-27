using System.Linq;
using NUnit.Framework;
using Unity.Mathematics;

namespace Chisel.Core.Tests
{
	[TestFixture]
	[Category("Contents")]
	public class ExactDelaunayTests
	{
		static ContentsScene StripScene()
		{
			var scene = new ContentsScene()
				.Add(ContentsSceneNode.Brush(ContentsScene.BoxPlanes(new float3(0, 0, 0), new float3(16, 1, 0.25f)), name: "strip"));
			int index = 0;
			for (float x = 0.5f; x + 0.375f <= 16; x += 1.25f)
				scene.Add(ContentsSceneNode.Brush(ContentsScene.BoxPlanes(new float3(x, 0, -0.5f), new float3(x + 0.375f, 1, 0)), name: "near " + index++));
			for (float x = 1.0625f; x + 0.5f <= 16; x += 1.4375f)
				scene.Add(ContentsSceneNode.Brush(ContentsScene.BoxPlanes(new float3(x, 0, 0.25f), new float3(x + 0.5f, 1, 0.75f)), name: "far " + index++));
			return scene;
		}

		static ContentsTreeHarness BuildAndUpdate(ContentsScene scene)
		{
			var harness = ContentsTreeHarness.Build(scene);
			Assert.That(harness.Update(), Is.True, "the CSG update did not run");
			return harness;
		}

		static ExactDelaunayCheck.Result CheckPart(ExactCSGCapture.Brush brush, ExactCSGCapture.Part part)
		{
			var plane = brush.planes[part.surface];
			int dominant = plane.DominantAxis();
			long component = dominant == 0 ? plane.a : (dominant == 1 ? plane.b : plane.c);
			ExactPredicates.ProjectionAxes(dominant, component < 0, out int axisU, out int axisV);
			var positions = new float[part.positions.Length * 3];
			for (int v = 0; v < part.positions.Length; v++)
			{
				positions[v * 3]     = part.positions[v].x;
				positions[v * 3 + 1] = part.positions[v].y;
				positions[v * 3 + 2] = part.positions[v].z;
			}
			return ExactDelaunayCheck.Check(part.vertices, positions, part.triangles, axisU, axisV);
		}

		[Test]
		public void AStripWithVerticesAlongBothSides_IsTriangulatedLocallyDelaunay()
		{
			using (var harness = BuildAndUpdate(StripScene()))
			{
				int innerEdges = 0, violations = 0, stripTopEdges = 0;
				string first = null;
				foreach (var node in harness.Scene.roots)
				{
					Assert.That(ExactCSGCapture.Brushes.TryGetValue(harness.NodeIDOf(node), out var brush) && brush.parts != null, Is.True,
								"no triangles captured for " + node.name);
					foreach (var part in brush.parts)
					{
						var result = CheckPart(brush, part);
						innerEdges += result.innerEdges;
						violations += result.violations;
						if (first == null && result.first != null)
							first = $"{node.name} face {part.surface}: {result.first}";
						var plane = brush.planes[part.surface];
						if (node.name == "strip" && plane.a == 0 && plane.c == 0 && plane.b > 0)
							stripTopEdges += result.innerEdges;
					}
				}
				// the strip's top has a vertex at every box's end on both sides: dozens of inner edges, or it tested nothing
				Assert.That(stripTopEdges, Is.GreaterThan(40), "the strip's top has too few inner edges to test anything");
				Assert.That(violations, Is.Zero, $"{violations} of {innerEdges} inner edges are not locally Delaunay; first: {first}");

				var mismatches = ExactJudge.FindMismatches(harness, 100, out var judged);
				Assert.That(judged, Is.GreaterThan(0), "the judge judged nothing");
				Assert.That(mismatches, Is.Empty, string.Join("; ", mismatches.Select(m => m.Format(""))));
			}
		}

		// A floor with a grid of boxes standing in it: its top is one face with a hole for every box, each bridged to the
		// boundary around it before the face is cut into ears
		[Test]
		public void AFloorWithAHoleForEveryBoxStandingInIt_IsTriangulatedAsTheSceneIs()
		{
			var scene = new ContentsScene()
				.Add(ContentsSceneNode.Brush(ContentsScene.BoxPlanes(new float3(-1, -1, -1), new float3(31, 0.25f, 31)), name: "floor"));
			for (int x = 0; x < 10; x++)
				for (int z = 0; z < 10; z++)
					scene.Add(ContentsSceneNode.Brush(ContentsScene.BoxPlanes(new float3(x * 3, 0, z * 3), new float3(x * 3 + 1.5f, 1, z * 3 + 1.25f)),
													  name: $"box {x} {z}"));
			using (var harness = BuildAndUpdate(scene))
			{
				var floor = harness.Scene.roots[0];
				Assert.That(ExactCSGCapture.Brushes.TryGetValue(harness.NodeIDOf(floor), out var brush) && brush.parts != null, Is.True,
							"no triangles captured for the floor");
				int topTriangles = 0, violations = 0, innerEdges = 0;
				string first = null;
				foreach (var part in brush.parts)
				{
					var plane = brush.planes[part.surface];
					if (!(plane.a == 0 && plane.c == 0 && plane.b > 0))
						continue;
					topTriangles += part.triangles.Length / 3;
					var result = CheckPart(brush, part);
					innerEdges += result.innerEdges;
					violations += result.violations;
					first ??= result.first;
				}
				// a hundred holes of four corners each take more than 400 triangles to fill around
				Assert.That(topTriangles, Is.GreaterThan(400), "the floor's top was not cut around every box");
				Assert.That(violations, Is.Zero, $"{violations} of {innerEdges} inner edges are not locally Delaunay; first: {first}");

				var mismatches = ExactJudge.FindMismatches(harness, 100, out var judged);
				Assert.That(judged, Is.GreaterThan(0), "the judge judged nothing");
				Assert.That(mismatches, Is.Empty, string.Join("; ", mismatches.Select(m => m.Format(""))));
				ContentsComparison.AssertMatchesOracle(harness, "the floor and its boxes");
			}
		}

		[Test]
		public void TheCheck_ReportsTheDiagonalThatShouldHaveBeenFlipped()
		{
			ExactVertex Point(double x, double y) => ExactVertex.Intersect(ExactPlane.Quantize(1, 0, 0, -x), ExactPlane.Quantize(0, 1, 0, -y),
																		   ExactPlane.Quantize(0, 0, 1, 0));
			var vertices  = new[] { Point(0, 0), Point(4, 0), Point(4, 1), Point(1, 1) };
			var positions = new float[] { 0, 0, 0, 4, 0, 0, 4, 1, 0, 1, 1, 0 };
			var floor     = ExactPlane.Quantize(0, 0, 1, 0);
			ExactPredicates.ProjectionAxes(floor.DominantAxis(), floor.c < 0, out int u, out int v);

			var wrong = ExactDelaunayCheck.Check(vertices, positions, new[] { 0, 1, 2, 0, 2, 3 }, u, v);
			Assert.That((wrong.innerEdges, wrong.violations), Is.EqualTo((1, 1)), "the diagonal that should have been flipped");
			var right = ExactDelaunayCheck.Check(vertices, positions, new[] { 0, 1, 3, 1, 2, 3 }, u, v);
			Assert.That((right.innerEdges, right.violations), Is.EqualTo((1, 0)), "the Delaunay diagonal: " + right.first);
		}
	}
}
