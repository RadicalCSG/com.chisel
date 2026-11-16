using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Unity.Mathematics;

namespace Chisel.Core.Tests
{
	[TestFixture]
	[Category("Contents")]
	public class ExactJudgeTests
	{
		const float kGap = 1f / (1 << 12);

		static ContentsScene GapScene()
		{
			return new ContentsScene()
				.Add(ContentsSceneNode.Brush(ContentsScene.BoxPlanes(new float3(0, 0, 0), new float3(1, 1, 1)), name: "A"))
				.Add(ContentsSceneNode.Brush(ContentsScene.BoxPlanes(new float3(1 + kGap, 0, 0), new float3(2, 1, 1)), name: "B"));
		}

		static ContentsTreeHarness BuildAndUpdate(ContentsScene scene)
		{
			var harness = ContentsTreeHarness.Build(scene);
			Assert.That(harness.Update(), Is.True, "the CSG update did not run");
			return harness;
		}

		static List<ContentsComparison.Mismatch> Judge(ContentsTreeHarness harness)
		{
			var mismatches = ExactJudge.FindMismatches(harness, 100, out var judged);
			Assert.That(judged, Is.GreaterThan(0), "the judge judged nothing");
			return mismatches;
		}

		static string Describe(List<ContentsComparison.Mismatch> mismatches)
		{
			return mismatches.Count == 0 ? "nothing" : string.Join("; ", mismatches.Select(m => m.Format("")));
		}

		static void AssertReports(List<ContentsComparison.Mismatch> mismatches, string fragment, string planted)
		{
			Assert.That(mismatches.Any(m => m.problem.Contains(fragment)), Is.True,
						$"planted {planted}, and the judge did not report \"{fragment}\"; it reported {Describe(mismatches)}");
		}

		static ExactCSGCapture.Brush CaptureOf(ContentsTreeHarness harness, string name)
		{
			var node = harness.Scene.roots.First(n => n.name == name);
			Assert.That(ExactCSGCapture.Brushes.TryGetValue(harness.NodeIDOf(node), out var brush), Is.True, "nothing captured for " + name);
			Assert.That(brush.parts, Is.Not.Null, "no triangles captured for " + name);
			return brush;
		}

		// The part a brush drew on its face whose normal points along `sign` times the axis.
		static ExactCSGCapture.Part PartFacing(ExactCSGCapture.Brush brush, int axis, int sign)
		{
			foreach (var part in brush.parts)
			{
				var plane = brush.planes[part.surface];
				var normal = new[] { plane.a, plane.b, plane.c };
				bool alongAxis = true;
				for (int i = 0; i < 3; i++)
					alongAxis &= i == axis ? Math.Sign(normal[i]) == sign : normal[i] == 0;
				if (alongAxis)
					return part;
			}
			Assert.Fail($"no part drawn on the face facing {(sign > 0 ? "+" : "-")}{"xyz"[axis]}");
			return null;
		}

		[Test]
		public void TheControl_TwoBoxesAHairApart_Passes()
		{
			using (var harness = BuildAndUpdate(GapScene()))
			{
				var mismatches = Judge(harness);
				Assert.That(mismatches, Is.Empty, Describe(mismatches));
			}
		}

		[Test]
		public void ATriangleMissingFromAFaceAcrossTheGap_IsAHole()
		{
			using (var harness = BuildAndUpdate(GapScene()))
			{
				var part = PartFacing(CaptureOf(harness, "B"), 0, -1);
				part.triangles = part.triangles.Skip(3).ToArray();
				AssertReports(Judge(harness), "the output draws nothing", "a missing triangle on B's face across the gap");
			}
		}

		[Test]
		public void AFaceDrawnTheWrongWay_IsReported()
		{
			using (var harness = BuildAndUpdate(GapScene()))
			{
				var part = PartFacing(CaptureOf(harness, "A"), 1, 1);
				part.category = part.category == CategoryIndex.SelfAligned ? CategoryIndex.SelfReverseAligned : CategoryIndex.SelfAligned;
				AssertReports(Judge(harness), "expects one of A+", "A's top drawn facing down");
			}
		}

		[Test]
		public void ATriangleDrawnTwice_IsReported()
		{
			using (var harness = BuildAndUpdate(GapScene()))
			{
				var part = PartFacing(CaptureOf(harness, "A"), 1, 1);
				part.triangles = part.triangles.Concat(part.triangles.Take(3)).ToArray();
				AssertReports(Judge(harness), "more than once", "one of A's top triangles twice");
			}
		}

		// The strip of floor between the boxes is 2^-12 wide. Stretching A's bottom over it has to be seen.
		[Test]
		public void AFaceReachingIntoTheGap_IsAnExtraFace()
		{
			using (var harness = BuildAndUpdate(GapScene()))
			{
				var part = PartFacing(CaptureOf(harness, "A"), 1, -1);
				var gapPlane = ExactPlane.Quantize(1, 0, 0, -(1.0 + kGap));
				int moved = 0;
				for (int i = 0; i < part.vertices.Length; i++)
				{
					var position = part.positions[i];
					if (position.x != 1f)
						continue;
					part.vertices[i]  = ExactVertex.Intersect(gapPlane, ExactPlane.Quantize(0, 1, 0, -position.y),
															  ExactPlane.Quantize(0, 0, 1, -position.z));
					part.positions[i] = new float3(1 + kGap, position.y, position.z);
					moved++;
				}
				Assert.That(moved, Is.GreaterThan(0), "no vertex of A's bottom lies at x = 1");
				AssertReports(Judge(harness), "the oracle expects nothing here", "A's bottom stretched over the gap");
			}
		}

		[Test]
		public void AVertexOffItsFacesPlane_IsReported()
		{
			using (var harness = BuildAndUpdate(GapScene()))
			{
				var brush = CaptureOf(harness, "A");
				var part  = PartFacing(brush, 0, 1);
				brush.planes[part.surface].w = Int128.Add(brush.planes[part.surface].w, Int128.FromLong(1));
				AssertReports(Judge(harness), "does not lie on its face's plane", "A's +x plane moved by 2^-20");
			}
		}

		[Test]
		public void AVertexRoundedToTheWrongFloat_IsReported()
		{
			using (var harness = BuildAndUpdate(GapScene()))
			{
				var part = PartFacing(CaptureOf(harness, "A"), 1, 1);
				var position = part.positions[0];
				position.x = math.asfloat(math.asint(position.x) + 1);
				part.positions[0] = position;
				AssertReports(Judge(harness), "the nearest float is", "a vertex of A's top one float off");
			}
		}

		// The concrete pad among its 65 neighbours, 99 m from the origin (ConcretePadTests): the judge has to see its top
		// face there too, where brushes rest on it within a float's noise.
		[Test]
		public void ATriangleMissingFromThePadsTopAmongItsNeighbours_IsAHole()
		{
			using (var harness = BuildAndUpdate(ConcretePadTests.BuildPadWith(ConcretePadTests.kNeighbourhood)))
			{
				var control = Judge(harness);
				Assert.That(control, Is.Empty, "before anything is planted: " + Describe(control));
				var part = PartFacing(CaptureOf(harness, "pad 4838"), 1, 1);
				Assert.That(part.triangles.Length, Is.GreaterThan(3), "the pad's top has one triangle or none");
				part.triangles = part.triangles.Skip(3).ToArray();
				AssertReports(Judge(harness), "the output draws nothing", "a missing triangle on the pad's top");
			}
		}

		[Test]
		public void ANaNTangentInTheMesh_IsReported()
		{
			using (var harness = BuildAndUpdate(GapScene()))
			{
				Assert.That(harness.Triangles.Count, Is.GreaterThan(0), "the mesh is empty");
				var triangle = harness.Triangles[0];
				triangle.tangentB = new float4(float.NaN, float.NaN, float.NaN, -1);
				harness.ReplaceTriangleForTest(0, triangle);
				AssertReports(Judge(harness), "tangent is not a number", "a NaN tangent in the mesh");
			}
		}

		[Test]
		public void ATriangleMissingFromTheMesh_IsReported()
		{
			using (var harness = BuildAndUpdate(GapScene()))
			{
				Assert.That(harness.Triangles.Count, Is.GreaterThan(0), "the mesh is empty");
				harness.RemoveTriangleForTest(0);
				AssertReports(Judge(harness), "the mesh does not have", "a triangle taken out of the mesh");
			}
		}

		// The cleanup puts a corner that lies on a surface's outline edge into that edge (OutputMeshCleanup.SplitOutline),
		// where the surface across has it: the triangles on both sides of the edge become two each, that cover the same,
		// and it records what it took out and put in
		[Test]
		public void ATriangleSplitAtAPointOnItsEdge_IsTheSameTriangle()
		{
			using (var harness = BuildAndUpdate(GapScene()))
			{
				SplitTriangle(harness, 0, offTheEdge: 0, bothSides: true, recorded: true);
				var mismatches = Judge(harness);
				Assert.That(mismatches, Is.Empty, Describe(mismatches));
			}
		}

		// The same with the point a centimetre off the edge covers something else: what the cleanup recorded is reported
		[Test]
		public void ATriangleSplitAtAPointOffItsEdge_IsReported()
		{
			using (var harness = BuildAndUpdate(GapScene()))
			{
				SplitTriangle(harness, 0, offTheEdge: 0.01f, bothSides: true, recorded: true);
				AssertReports(Judge(harness), "cover something else", "a triangle split at a point off its edge");
			}
		}

		// A split the cleanup did not record is no change of the cleanup's, and none the weld makes
		[Test]
		public void ATriangleSplitTheCleanupDidNotRecord_IsReported()
		{
			using (var harness = BuildAndUpdate(GapScene()))
			{
				SplitTriangle(harness, 0, offTheEdge: 0, bothSides: true, recorded: false);
				AssertReports(Judge(harness), "the mesh has a triangle the exact CSG did not make", "a split no step recorded");
			}
		}

		// Split on one side only, the edge across runs past the point: a crack, where the exact CSG's output is closed
		[Test]
		public void ATriangleSplitOnOneSideOfItsEdge_IsReportedOpen()
		{
			using (var harness = BuildAndUpdate(GapScene()))
			{
				SplitTriangle(harness, 0, offTheEdge: 0, bothSides: false, recorded: true);
				AssertReports(Judge(harness), "the mesh is open along", "a triangle split on one side of its edge");
			}
		}

		// Mesh triangle `index` as two, split at the middle of an edge where its face meets another (on its surface's
		// outline, where the cleanup puts corners), moved `offTheEdge` into its plane, away from its third corner; with
		// bothSides, the triangle across the edge too, at the same point. recorded: as the cleanup records what it changed
		static void SplitTriangle(ContentsTreeHarness harness, int index, float offTheEdge, bool bothSides, bool recorded)
		{
			Assert.That(harness.Triangles.Count, Is.GreaterThan(index), "the mesh is empty");
			var triangle = harness.Triangles[index];
			var corners = new[] { triangle.a, triangle.b, triangle.c };
			for (int e = 0; e < 3; e++)
			{
				var from = corners[e]; var to = corners[(e + 1) % 3]; var third = corners[(e + 2) % 3];
				var across = -1;
				for (int t = 0; t < harness.Triangles.Count && across < 0; t++)
				{
					var other = harness.Triangles[t];
					if ((other.a.Equals(to) && other.b.Equals(from)) || (other.b.Equals(to) && other.c.Equals(from)) || (other.c.Equals(to) && other.a.Equals(from)))
						across = t;
				}
				if (across < 0 || math.dot(math.normalize(harness.Triangles[across].Normal), math.normalize(triangle.Normal)) > 0.5f)
					continue;
				var middle = (from + to) * 0.5f;
				var inPlane = math.normalize(math.cross(triangle.Normal, to - from));
				if (math.dot(inPlane, third - middle) > 0)
					inPlane = -inPlane;
				middle += inPlane * offTheEdge;
				Split(harness, index, from, to, middle, recorded ? kPlantedSurface : -1);
				if (bothSides)
					Split(harness, across, to, from, middle, recorded ? kPlantedSurface + 1 : -1);
				return;
			}
			Assert.Fail("the triangle has no edge where its face meets another");
		}

		// Surfaces of the changes planted as the cleanup's, past any a brush here has, so none mixes with one it recorded
		const int kPlantedSurface = 1000;

		// Mesh triangle `index`, which runs from -> to, as two, split at point; recorded as a change of the cleanup's to
		// `surface` when that is not negative
		static void Split(ContentsTreeHarness harness, int index, float3 from, float3 to, float3 point, int surface)
		{
			var triangle = harness.Triangles[index];
			var corners = new[] { triangle.a, triangle.b, triangle.c };
			var start = corners[0].Equals(from) && corners[1].Equals(to) ? 0 : (corners[1].Equals(from) && corners[2].Equals(to) ? 1 : 2);
			var third = corners[(start + 2) % 3];
			var first = triangle;  first.a  = from;  first.b  = point; first.c  = third;
			var second = triangle; second.a = point; second.b = to;    second.c = third;
			harness.ReplaceTriangleForTest(index, first);
			harness.AddTriangleForTest(second);
			if (surface < 0)
				return;
			if (!harness.CleanupChanges.TryGetValue(triangle.brushID, out var changes))
				harness.CleanupChanges[triangle.brushID] = changes = new List<MeshCleanupChange>();
			changes.Add(new MeshCleanupChange { a = triangle.a, b = triangle.b, c = triangle.c, surface = surface });
			changes.Add(new MeshCleanupChange { a = first.a,  b = first.b,  c = first.c,  surface = surface, added = 1 });
			changes.Add(new MeshCleanupChange { a = second.a, b = second.b, c = second.c, surface = surface, added = 1 });
		}

		[Test]
		public void APlaneOverTheWorkBudget_IsListedAsNotJudged([Values("edge pairs", "slab crossings", "judgement's edge pairs",
																		 "judgement's slab crossings")] string kind)
		{
			using (var harness = BuildAndUpdate(GapScene()))
			{
				ExactJudge.FindMismatches(harness, 100, out int judgedAll, out var none, ExactJudge.WorkBudget.Unlimited);
				Assert.That(none, Is.Empty, "no budget, and still planes listed: " + string.Join("; ", none));

				var budget = ExactJudge.WorkBudget.Unlimited;
				switch (kind)
				{
					case "edge pairs":                 budget.edgePairs = 0; break;
					case "slab crossings":             budget.slabCrossings = 0; break;
					case "judgement's edge pairs":     budget.totalEdgePairs = 0; break;
					case "judgement's slab crossings": budget.totalSlabCrossings = 0; break;
				}
				var mismatches = ExactJudge.FindMismatches(harness, 100, out int judged, out var unexamined, budget);
				Assert.That(unexamined, Is.Not.Empty, $"no room for {kind}, and every plane was judged anyway");
				Assert.That(unexamined.All(line => line.Contains("NOT judged") && line.Contains(kind)), Is.True, string.Join("; ", unexamined));
				Assert.That(judged, Is.LessThan(judgedAll), "the planes listed were judged all the same");
				Assert.That(mismatches, Is.Empty, Describe(mismatches));
			}
		}
	}
}
