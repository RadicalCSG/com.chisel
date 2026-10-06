using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Unity.Mathematics;

namespace Chisel.Core.Tests
{
	[TestFixture]
	[Category("Contents")]
	public class CanonicalVertexEditTests
	{
		int savedStage;

		[SetUp]
		public void SaveStage() => savedStage = CompactHierarchyManager.TreeUpdate.kCanonicalVertexStage;

		[TearDown]
		public void RestoreStage() => CompactHierarchyManager.TreeUpdate.kCanonicalVertexStage = savedStage;

		static ContentsSceneNode Box(string name, float3 min, float3 max)
		{
			return ContentsSceneNode.Brush(ContentsScene.BoxPlanes(min, max), name: name);
		}

		static List<float3> VerticesOf(ContentsTreeHarness harness, ContentsSceneNode brush)
		{
			var vertices = new List<float3>();
			foreach (var triangle in harness.Triangles)
			{
				if (harness.NodeOf(triangle.brushID) != brush)
					continue;
				vertices.Add(triangle.a);
				vertices.Add(triangle.b);
				vertices.Add(triangle.c);
			}
			return vertices.Distinct().ToList();
		}

		// Every vertex either brush draws on the plane x = seamX is one the other brush draws too.
		static void AssertSeamMatches(ContentsTreeHarness harness, ContentsSceneNode a, ContentsSceneNode b, float seamX, string when)
		{
			var verticesA = VerticesOf(harness, a);
			var verticesB = VerticesOf(harness, b);
			var seamA = verticesA.Where(v => math.abs(v.x - seamX) < 0.001f).ToList();
			var seamB = verticesB.Where(v => math.abs(v.x - seamX) < 0.001f).ToList();
			Assert.That(seamA, Is.Not.Empty, $"{when}: {a.name} draws nothing on the seam");
			Assert.That(seamB, Is.Not.Empty, $"{when}: {b.name} draws nothing on the seam");
			foreach (var vertex in seamA)
				Assert.That(seamB.Any(v => math.distance(v, vertex) <= CanonicalVertices.kSameVertex), Is.True,
							$"{when}: {a.name} has {vertex} on the seam and {b.name} doesn't; {b.name} has {string.Join(" ", seamB)}");
			foreach (var vertex in seamB)
				Assert.That(seamA.Any(v => math.distance(v, vertex) <= CanonicalVertices.kSameVertex), Is.True,
							$"{when}: {b.name} has {vertex} on the seam and {a.name} doesn't; {a.name} has {string.Join(" ", seamA)}");
		}

		[Test]
		public void AddingABrush_KeepsTheSeamWithABrushThatWasNotRebuilt([Values(0, 2, 3, 4)] int stageValue)
		{
			var stage = (CanonicalVertexStage)stageValue;
			CompactHierarchyManager.TreeUpdate.kCanonicalVertexStage = stageValue;

			var left   = Box("left",   new float3(0, 0, 0), new float3(1, 1, 1));
			var middle = Box("middle", new float3(1, 0, 0), new float3(2, 1, 1));
			using var harness = ContentsTreeHarness.Build(new ContentsScene().Add(left).Add(middle));
			Assert.That(harness.Update(), Is.True, "the first update did not run");
			AssertSeamMatches(harness, left, middle, 1, "before the edit");
			var leftBefore = VerticesOf(harness, left);

			var right = Box("right", new float3(2, 0, 0), new float3(3, 1.0003f, 1));
			harness.Insert(null, 2, right);
			Assert.That(harness.Update(), Is.True, "the update after the edit did not run");
			Assert.That(harness.Delivered, Is.True, harness.LastUpdateReport);

			// Only a brush that wasn't rebuilt makes this a test of the rule
			CollectionAssert.AreEquivalent(leftBefore, VerticesOf(harness, left),
										   "the left box changed, so it was rebuilt: " + harness.LastUpdateReport);
			AssertSeamMatches(harness, left, middle, 1, "after the edit");

			if (CompactHierarchyManager.TreeUpdate.kExactCSG)
				AssertStepIsClosed(harness, middle, right, 2);
			else if (stage >= CanonicalVertexStage.Positions)
				AssertSeamMatches(harness, middle, right, 2, "after the edit");
		}

		static void AssertStepIsClosed(ContentsTreeHarness harness, ContentsSceneNode lower, ContentsSceneNode taller, float seamX)
		{
			Assert.That(ExactCSGCapture.Brushes.TryGetValue(harness.NodeIDOf(taller), out var capture) && capture.planes != null,
						Is.True, $"exact CSG: no planes were captured for {taller.name}");
			var tops = capture.planes.Where(p => p.a == 0 && p.c == 0 && p.b > 0).ToList();
			Assert.That(tops.Count, Is.EqualTo(1), $"exact CSG: {taller.name} does not have one plane facing straight up");
			// a*x + b*y + c*z + w = 0 with a = c = 0: every point of the top has y = -w / b
			var stepTop = ExactPredicates.RoundToFloat(Int128.FromLong(-tops[0].w), Int128.FromLong(tops[0].b));

			var seamLower  = VerticesOf(harness, lower).Where(v => math.abs(v.x - seamX) < 0.001f).ToList();
			var seamTaller = VerticesOf(harness, taller).Where(v => math.abs(v.x - seamX) < 0.001f).ToList();
			Assert.That(seamLower, Is.Not.Empty, $"exact CSG: {lower.name} draws nothing on the seam");
			foreach (var vertex in seamLower)
				Assert.That(seamTaller.Contains(vertex), Is.True,
							$"exact CSG: {lower.name} has {vertex} on the seam and {taller.name} doesn't; {taller.name} has {string.Join(" ", seamTaller)}");
			var stepCorners = seamTaller.Where(v => !seamLower.Contains(v)).ToList();
			foreach (var vertex in stepCorners)
				Assert.That(vertex.y, Is.EqualTo(stepTop),
							$"exact CSG: {taller.name} has {vertex} on the seam, which is neither a corner of {lower.name} nor on the top of the step " +
							$"(y = {stepTop:R}); {lower.name} has {string.Join(" ", seamLower)}");
			Assert.That(stepCorners.Count, Is.EqualTo(2), $"exact CSG: the step above {lower.name} is not drawn as one strip; {taller.name} has {string.Join(" ", seamTaller)}");
			Assert.That(stepCorners[0].y, Is.EqualTo(stepCorners[1].y), "exact CSG: the top of the step is not one plane");
		}
	}
}
