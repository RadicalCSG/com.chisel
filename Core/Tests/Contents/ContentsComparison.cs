using System.Collections.Generic;
using System.Text;
using NUnit.Framework;
using Unity.Mathematics;

namespace Chisel.Core.Tests
{
	// Compares what a harness captured against what the oracle says its scene should draw, face by
	// face, on a grid of sample points.
	public static class ContentsComparison
	{
		const float kProbeOffset  = 0.05f;
		const float kInset        = 0.2f;
		const float kSpacing      = 0.75f;
		const float kCrossingBand = 0.05f;
		// A vertex may sit up to the 12.5 mm weld distance off the analytic plane of the face it belongs
		// to, so "on this plane" has to be at least that loose or real triangles get rejected.
		const float kOnPlane      = 0.02f;
		const float kInside       = 0.001f;
		const float kCoplanar     = 0.001f;
		const int   kMaxReported  = 8;

		internal struct Face : System.IEquatable<Face>
		{
			public ContentsSceneNode brush;
			public int    facing;     // +1 drawn along the sampled plane's normal, -1 against it
			public bool Equals(Face other) => brush == other.brush && facing == other.facing;
			public override bool Equals(object obj) => obj is Face other && Equals(other);
			public override int GetHashCode() => ((brush?.GetHashCode() ?? 0) * 3) + facing;
			public override string ToString() => (brush?.name ?? "?") + (brush != null && brush.contents != ContentsSceneNode.kSolidContents ? "(c" + brush.contents + ")" : "") + (facing > 0 ? "+" : "-");
		}

		// One place where the output and the oracle disagree, kept as data so a caller can come back to the same
		// point later. The text form is what FindMismatches has always reported.
		public sealed class Mismatch
		{
			public ContentsSceneNode brush;
			public int    brushIndex;
			public int    face;
			public float3 point;
			public string problem;
			public float  nearest;

			public string Format(string what)
			{
				return $"{what}: {brush.name ?? ("brush" + brushIndex)} face {face} at " +
					   $"({point.x:0.###}, {point.y:0.###}, {point.z:0.###}): {problem}" +
					   $" [nearest other plane {nearest:0.####}]";
			}
		}

		// Builds the scene, runs one full update and compares.
		public static void BuildAndAssertMatchesOracle(ContentsScene scene, string what)
		{
			using (var harness = ContentsTreeHarness.Build(scene))
			{
				Assert.That(harness.Update(), Is.True, what + ": the CSG update did not run");
				AssertMatchesOracle(harness, what);
			}
		}

		// As above, sampling with the given quad extent. A scene that sits further from the origin than the
		// default extent needs this, or its far faces are never sampled and pass unexamined.
		public static void BuildAndAssertMatchesOracle(ContentsScene scene, string what, float extent)
		{
			using (var harness = ContentsTreeHarness.Build(scene))
			{
				Assert.That(harness.Update(), Is.True, what + ": the CSG update did not run");
				AssertMatchesOracle(harness, what, extent);
			}
		}

		public static void AssertMatchesOracle(ContentsTreeHarness harness, string what, float extent)
		{
			var failures = FindMismatches(harness, what, out var checkedSamples, out var skipped,
										  out var facesWithoutSamples, extent, kMaxReported);
			Assert.That(checkedSamples, Is.GreaterThan(0), what + ": every sample was filtered out");
			if (failures.Count > 0)
			{
				var report = new StringBuilder();
				report.AppendLine($"{what}: {failures.Count} mismatch(es) over {checkedSamples} samples, {skipped} skipped");
				report.AppendLine("  scene: " + DescribeScene(harness.Scene));
				report.AppendLine("  update: " + harness.LastUpdateReport);
				foreach (var failure in failures)
					report.AppendLine("  " + failure);
				if (facesWithoutSamples.Count > 0)
					report.AppendLine($"  ({facesWithoutSamples.Count} face(s) produced no samples and were not judged)");
				Assert.Fail(report.ToString());
			}
		}

		// Compares the harness's last captured output with its scene as it is now, so after an edit
		// this judges the incremental update.
		public static void AssertMatchesOracle(ContentsTreeHarness harness, string what)
		{
			var failures = FindMismatches(harness, what, out var checkedSamples, out var skipped);
			Assert.That(checkedSamples, Is.GreaterThan(0), what + ": every sample was filtered out");
			if (failures.Count > 0)
			{
				var report = new StringBuilder();
				report.AppendLine($"{what}: {failures.Count} mismatch(es) over {checkedSamples} samples, {skipped} skipped");
				report.AppendLine("  scene: " + DescribeScene(harness.Scene));
				report.AppendLine("  update: " + harness.LastUpdateReport);
				foreach (var failure in failures)
					report.AppendLine("  " + failure);
				Assert.Fail(report.ToString());
			}
		}

		// The first few places where the output and the oracle disagree, without failing: a control that has to
		// show a disagreement uses this.
		public static List<string> FindMismatches(ContentsTreeHarness harness, string what, out int checkedSamples, out int skipped)
		{
			return FindMismatches(harness, what, out checkedSamples, out skipped, out _, kDefaultExtent, kMaxReported);
		}

		// The extent ContentsFaceSampler builds its quad with, when a caller does not say otherwise.
		public const float kDefaultExtent = 64f;

		public static List<string> FindMismatches(ContentsTreeHarness harness, string what,
												  out int checkedSamples, out int skipped,
												  out List<string> facesWithoutSamples,
												  float extent, int maxReported)
		{
			var records  = FindMismatchRecords(harness, out checkedSamples, out skipped, out facesWithoutSamples,
											   extent, maxReported);
			var failures = new List<string>(records.Count);
			foreach (var record in records)
				failures.Add(record.Format(what));
			return failures;
		}

		public static List<Mismatch> FindMismatchRecords(ContentsTreeHarness harness,
														 out int checkedSamples, out int skipped,
														 out List<string> facesWithoutSamples,
														 float extent, int maxReported, bool budgeted = false)
		{
			if (CompactHierarchyManager.TreeUpdate.kExactCSG)
			{
				skipped = 0;
				return ExactJudge.FindMismatches(harness, maxReported, out checkedSamples, out facesWithoutSamples,
												 budgeted ? ExactJudge.SweepBudget : ExactJudge.WorkBudget.Unlimited);
			}

			var scene   = harness.Scene;
			var oracle  = new ContentsOracle(scene);
			var brushes = oracle.Brushes;

			var failures = new List<Mismatch>();
			facesWithoutSamples = new List<string>();
			checkedSamples = 0;
			skipped = 0;

			var allBounds = new List<(float3 min, float3 max)>(brushes.Count);
			foreach (var other in brushes)
				allBounds.Add(ContentsFaceSampler.BrushBounds(oracle.TreePlanesOf(other), extent));

			for (int b = 0; b < brushes.Count && failures.Count < maxReported; b++)
			{
				var brush  = brushes[b];
				var planes = oracle.TreePlanesOf(brush);

				var otherPlanes = new List<float4[]>();
				var otherBounds = new List<(float3 min, float3 max)>();
				for (int o = 0; o < brushes.Count; o++)
				{
					if (o == b)
						continue;
					otherPlanes.Add(oracle.TreePlanesOf(brushes[o]));
					otherBounds.Add(allBounds[o]);
				}

				for (int p = 0; p < planes.Length && failures.Count < maxReported; p++)
				{
					// the grid, plus a point in every region the brushes reaching this face cut it into
					var samples = ContentsFaceSampler.FaceSamples(planes, p, otherPlanes, otherBounds, extent, kInset, kSpacing,
																  kCellClearance);

					// A face nothing sampled has not passed - it has not been examined. Recorded so a sweep
					// can tell the two apart instead of counting silence as agreement.
					if (samples.Count == 0)
						facesWithoutSamples.Add($"{brush.name ?? ("brush" + b)} face {p}");

					foreach (var point in samples)
					{
						if (ContentsFaceSampler.NearACrossingPlane(otherPlanes, planes[p], point, kCrossingBand))
						{
							skipped++;
							continue;
						}
						checkedSamples++;

						var expected = ExpectedFaces(oracle, brushes, planes[p], point);
						var found    = FoundFaces(harness, planes[p], point);

						var problem = Compare(expected, found);
						if (problem != null)
						{
							var nearest = NearestOtherPlaneDistance(otherPlanes, point);
							failures.Add(new Mismatch
							{
								brush      = brush,
								brushIndex = b,
								face       = p,
								point      = point,
								problem    = problem,
								nearest    = nearest
							});
							if (failures.Count >= maxReported)
								break;
						}
					}
				}
			}

			return failures;
		}

		public static string MismatchAt(ContentsTreeHarness harness, ContentsOracle oracle, float4 plane, float3 point)
		{
			return Compare(ExpectedFaces(oracle, oracle.Brushes, plane, point), FoundFaces(harness, plane, point));
		}

		// Whether the oracle has a face at this point on this plane, per facing - all that can be asked of an output that
		// carries no brush identity, such as a live scene's meshes.
		public static void ExpectedFacings(ContentsOracle oracle, float4 plane, float3 point, out bool along, out bool against)
		{
			along = against = false;
			foreach (var face in ExpectedFaces(oracle, oracle.Brushes, plane, point))
			{
				if (face.facing > 0) along   = true;
				else                 against = true;
			}
		}

		// Whether this triangle lies on the plane and covers the point, with the same tolerances the harness comparison
		// uses; facing is +1 when it is drawn along the plane's normal.
		public static bool Covers(float3 a, float3 b, float3 c, float4 plane, float3 point, out int facing)
		{
			facing = math.dot(math.cross(b - a, c - a), plane.xyz) > 0 ? 1 : -1;
			return ContentsFaceSampler.CoversPoint(a, b, c, plane, point, kOnPlane, kInside);
		}

		public static bool OnPlane(float3 point, float4 plane) => math.abs(math.dot(plane.xyz, point) + plane.w) <= kOnPlane;

		public const float kSampleInset = kInset, kSampleSpacing = kSpacing, kSampleCrossingBand = kCrossingBand;

		public const float kCellClearance = kCrossingBand * 2;

		public static string KindOf(string problem)
		{
			if (problem == null)
				return null;
			if (problem.StartsWith("the output draws nothing", System.StringComparison.Ordinal))
				return "hole";
			const string kExtra = "the oracle expects nothing here, the output draws ";
			if (problem.StartsWith(kExtra, System.StringComparison.Ordinal))
				return "extra: " + problem.Substring(kExtra.Length);
			var disallowed = problem.IndexOf(", which the oracle does not allow", System.StringComparison.Ordinal);
			if (disallowed > 0)
				return "wrong: " + problem.Substring(0, disallowed);
			if (problem.EndsWith("on top of each other", System.StringComparison.Ordinal))
				return "overlap";
			return problem;
		}

		// Every face the oracle finds at this point on this plane. More than one brush can qualify where
		// faces are coplanar; Compare decides which of them may draw.
		static List<Face> ExpectedFaces(ContentsOracle oracle, IReadOnlyList<ContentsSceneNode> brushes,
										float4 plane, float3 point)
		{
			var expected = new List<Face>();
			foreach (var brush in brushes)
			{
				var planes = oracle.TreePlanesOf(brush);
				for (int p = 0; p < planes.Length; p++)
				{
					var sameWay = math.dot(planes[p].xyz, plane.xyz) > 0;
					var aligned = math.abs(math.dot(planes[p].xyz, plane.xyz)) > 0.9999f &&
								  math.abs(sameWay ? planes[p].w - plane.w : planes[p].w + plane.w) < kCoplanar;
					if (!aligned)
						continue;
					// On that brush's face, not just its plane: a coplanar face elsewhere can't draw here, and in a
					// tie it would take the point from the brush that does
					if (!OnFace(planes, p, point))
						continue;

					var verdict = oracle.JudgeFace(brush, p, point, kProbeOffset);
					if (verdict == ContentsFaceVerdict.None)
						continue;

					// A face drawn out of its own brush points along its own plane's normal; express
					// that in the sampled plane's frame so both orientations can be compared.
					var facing = verdict == ContentsFaceVerdict.FacingOut ? 1 : -1;
					if (!sameWay)
						facing = -facing;
					expected.Add(new Face { brush = brush, facing = facing });
				}
			}
			return expected;
		}

		// Whether the point lies within the face on the given plane: inside or on every other plane of the brush
		public static bool OnFace(float4[] planes, int planeIndex, float3 point)
		{
			for (int q = 0; q < planes.Length; q++)
			{
				if (q != planeIndex && math.dot(planes[q].xyz, point) + planes[q].w > kCoplanar)
					return false;
			}
			return true;
		}

		// The scene's shape in one line: which brush carries which operation, and how the composites
		// nest. A mismatch is nearly always about an operation, so this is the first thing to read.
		public static string DescribeScene(ContentsScene scene)
		{
			var text = new StringBuilder();
			foreach (var node in scene.roots)
				DescribeNode(text, node);
			return text.ToString();
		}

		static void DescribeNode(StringBuilder text, ContentsSceneNode node)
		{
			if (text.Length > 0)
				text.Append(' ');
			if (node.IsBrush)
			{
				text.Append($"{node.name}({node.operation.ToString().Substring(0, 3)},c{node.contents},{node.localPlanes.Length}p)");
				return;
			}
			text.Append($"[{node.operation.ToString().Substring(0, 3)}:");
			foreach (var child in node.children)
				DescribeNode(text, child);
			text.Append(']');
		}

		static float NearestOtherPlaneDistance(List<float4[]> otherBrushPlanes, float3 point)
		{
			var nearest = float.MaxValue;
			foreach (var planes in otherBrushPlanes)
			{
				for (int p = 0; p < planes.Length; p++)
					nearest = math.min(nearest, math.abs(math.dot(planes[p].xyz, point) + planes[p].w));
			}
			return nearest;
		}

		static List<Face> FoundFaces(ContentsTreeHarness harness, float4 plane, float3 point)
		{
			var found = new List<Face>();
			foreach (var triangle in harness.Triangles)
			{
				if (!ContentsFaceSampler.CoversPoint(triangle.a, triangle.b, triangle.c, plane, point, kOnPlane, kInside))
					continue;
				var node = harness.NodeOf(triangle.brushID);
				if (node == null)
					continue;
				found.Add(new Face { brush = node, facing = math.dot(triangle.Normal, plane.xyz) > 0 ? 1 : -1 });
			}
			return found;
		}

		internal static string Compare(List<Face> expected, List<Face> found)
		{
			foreach (var facing in new[] { 1, -1 })
			{
				var allowed = Allowed(expected, facing);
				var drawn   = Distinct(found, facing);
				if (allowed.Count == 0 && drawn.Count == 0)
					continue;
				if (allowed.Count == 0)
					return "the oracle expects nothing here, the output draws " + Describe(drawn);
				if (drawn.Count == 0)
					return "the output draws nothing, the oracle expects one of " + Describe(allowed);
				foreach (var face in drawn)
				{
					if (!allowed.Contains(face))
						return $"the output draws {face}, which the oracle does not allow (it expects one of {Describe(allowed)})";
				}
				if (drawn.Count > 1)
					return $"the output draws {Describe(drawn)} on top of each other";
			}
			return null;
		}

		static List<Face> Allowed(List<Face> expected, int facing)
		{
			var candidates = Distinct(expected, facing);
			if (candidates.Count < 2)
				return candidates;
			var winner = candidates[0].brush.contents;
			foreach (var candidate in candidates)
			{
				if (ContentsOracle.WinsTie(candidate.brush.contents, winner))
					winner = candidate.brush.contents;
			}
			return candidates.FindAll(candidate => candidate.brush.contents == winner);
		}

		static List<Face> Distinct(List<Face> faces, int facing)
		{
			var result = new List<Face>();
			foreach (var face in faces)
			{
				if (face.facing == facing && !result.Contains(face))
					result.Add(face);
			}
			return result;
		}

		static string Describe(List<Face> faces)
		{
			var text = new StringBuilder();
			foreach (var face in faces)
			{
				if (text.Length > 0)
					text.Append(", ");
				text.Append(face.ToString());
			}
			return text.Length == 0 ? "nothing" : text.ToString();
		}
	}
}
