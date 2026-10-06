using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using NUnit.Framework;
using Unity.Mathematics;

namespace Chisel.Core.Tests
{
	[TestFixture]
	[Category("Contents")]
	public class SharedCornerSurveyTests
	{
		static readonly float3 kCorner = new float3(1, 2, 3);
		static readonly float3 kTwoMillimetres = new float3(0.002f, 0, 0);

		#region Classifier
		[Test]
		public void Classify_BothAtTheCorner_IsSame()
		{
			var result = SharedCornerSurvey.Classify(kCorner, new[] { kCorner }, new[] { kCorner + new float3(0.000001f) });
			Assert.That(result.verdict, Is.EqualTo(SharedCornerSurvey.Verdict.Same));
		}

		[Test]
		public void Classify_OneEachTwoMillimetresApart_IsApart()
		{
			var result = SharedCornerSurvey.Classify(kCorner, new[] { kCorner }, new[] { kCorner + kTwoMillimetres });
			Assert.That(result.verdict,  Is.EqualTo(SharedCornerSurvey.Verdict.Apart));
			Assert.That(result.distance, Is.EqualTo(0.002f).Within(0.000001f));
		}

		[Test]
		public void Classify_OneAgainstTwo_IsOneVsTwo()
		{
			// One side welded its two nearby vertices into one that sits between them
			var result = SharedCornerSurvey.Classify(kCorner, new[] { kCorner + (kTwoMillimetres * 0.5f) },
															  new[] { kCorner, kCorner + kTwoMillimetres });
			Assert.That(result.verdict, Is.EqualTo(SharedCornerSurvey.Verdict.OneVsTwo));
			Assert.That(result.countA,  Is.EqualTo(1));
			Assert.That(result.countB,  Is.EqualTo(2));
		}

		[Test]
		public void Classify_OneSideDrawsNothingNearIt_IsMissing()
		{
			var result = SharedCornerSurvey.Classify(kCorner, new[] { kCorner }, new[] { kCorner + new float3(1, 0, 0) });
			Assert.That(result.verdict, Is.EqualTo(SharedCornerSurvey.Verdict.Missing));
			Assert.That(result.isDisagreement, Is.True);
		}

		[Test]
		public void Classify_NeitherSideDrawsNearIt_IsHidden()
		{
			var result = SharedCornerSurvey.Classify(kCorner, new float3[0], new[] { kCorner + new float3(1, 0, 0) });
			Assert.That(result.verdict, Is.EqualTo(SharedCornerSurvey.Verdict.Hidden));
			Assert.That(result.isDisagreement, Is.False);
		}

		// A brush keeping a vertex of its own next to the shared corner is not a disagreement about
		// the corner.
		[Test]
		public void Classify_SameCornerWithAnOwnVertexNearby_IsSame()
		{
			var result = SharedCornerSurvey.Classify(kCorner, new[] { kCorner, kCorner + kTwoMillimetres }, new[] { kCorner });
			Assert.That(result.verdict, Is.EqualTo(SharedCornerSurvey.Verdict.Same));
		}

		[Test]
		public void ClassifyCorner_OnlyComparesFacesThroughTheCorner()
		{
			var corner = new SharedCornerSurvey.Corner { position = kCorner, planesA = new[] { 0 }, planesB = new[] { 0 } };
			var verticesA = new List<List<float3>> { new List<float3> { kCorner } };
			var verticesB = new List<List<float3>>
			{
				new List<float3> { kCorner + new float3(0.0019f, 0, 0) },
				new List<float3> { kCorner }
			};
			var result = SharedCornerSurvey.ClassifyCorner(corner, verticesA, verticesB);
			Assert.That(result.verdict,  Is.EqualTo(SharedCornerSurvey.Verdict.Apart));
			Assert.That(result.distance, Is.EqualTo(0.0019f).Within(0.000001f));
		}

		[Test]
		public void ClassifyCorner_TakesTheWorstFacePair()
		{
			var corner = new SharedCornerSurvey.Corner { position = kCorner, planesA = new[] { 0, 1 }, planesB = new[] { 0 } };
			var verticesA = new List<List<float3>>
			{
				new List<float3> { kCorner },
				new List<float3> { kCorner + kTwoMillimetres }
			};
			var verticesB = new List<List<float3>> { new List<float3> { kCorner } };
			var result = SharedCornerSurvey.ClassifyCorner(corner, verticesA, verticesB);
			Assert.That(result.verdict,  Is.EqualTo(SharedCornerSurvey.Verdict.Apart));
			Assert.That(result.distance, Is.EqualTo(0.002f).Within(0.000001f));
		}

		[Test]
		public void ClassifyCorner_OneSideWithNoFaceNearIt_IsMissing()
		{
			var corner = new SharedCornerSurvey.Corner { position = kCorner, planesA = new[] { 0 }, planesB = new[] { 0 } };
			var verticesA = new List<List<float3>> { new List<float3> { kCorner } };
			var verticesB = new List<List<float3>> { new List<float3>() };
			var result = SharedCornerSurvey.ClassifyCorner(corner, verticesA, verticesB);
			Assert.That(result.verdict, Is.EqualTo(SharedCornerSurvey.Verdict.Missing));
		}

		[Test]
		public void PlaneOf_FindsTheFaceATriangleLiesOn_InEitherOrientation()
		{
			var planes = ContentsScene.BoxPlanes(new float3(0), new float3(2));
			Assert.That(SharedCornerSurvey.PlaneOf(new float3(1, 0, 0),  new float3(2.01f, 1, 1), planes), Is.EqualTo(1));
			Assert.That(SharedCornerSurvey.PlaneOf(new float3(-1, 0, 0), new float3(2, 1, 1),     planes), Is.EqualTo(1), "a carved wall faces inwards");
			Assert.That(SharedCornerSurvey.PlaneOf(new float3(1, 0, 0),  new float3(1, 1, 1),     planes), Is.EqualTo(-1), "not on any face");
		}
		#endregion

		#region Corners
		// Two unit-offset cubes: their surfaces cross on a skew hexagon, whose six corners are the
		// corners of the overlap that lie on both surfaces - all of them except (1,1,1) and (2,2,2).
		[Test]
		public void SharedCorners_OfTwoOverlappingBoxes_AreTheCornersOfTheirCrossing()
		{
			var planes = new List<float4[]>
			{
				ContentsScene.BoxPlanes(new float3(0), new float3(2)),
				ContentsScene.BoxPlanes(new float3(1), new float3(3))
			};
			var corners = SharedCornerSurvey.SharedCorners(planes);

			var expected = new[]
			{
				new float3(2, 1, 1), new float3(1, 2, 1), new float3(1, 1, 2),
				new float3(1, 2, 2), new float3(2, 1, 2), new float3(2, 2, 1)
			};
			Assert.That(corners.Count, Is.EqualTo(expected.Length), string.Join(", ", corners.Select(c => c.position)));
			foreach (var position in expected)
				Assert.That(corners.Any(c => math.distance(c.position, position) < 0.00001f), Is.True, "missing " + position);
			Assert.That(corners.All(c => c.isolated), Is.True, "nothing else comes near these corners");

			// (2, 1, 1) lies on A's +x face, and on B's -y and -z faces
			var corner = corners.First(c => math.distance(c.position, new float3(2, 1, 1)) < 0.00001f);
			Assert.That(corner.planesA, Is.EqualTo(new[] { 1 }));
			Assert.That(corner.planesB, Is.EqualTo(new[] { 2, 4 }));
		}

		[Test]
		public void SharedCorners_WithAnotherFaceWithinTheWeldRadius_AreNotIsolated()
		{
			var planes = new List<float4[]>
			{
				ContentsScene.BoxPlanes(new float3(0), new float3(2)),
				ContentsScene.BoxPlanes(new float3(1), new float3(3)),
				// Its -x face lies 2 mm beyond the corner (2, 1, 1)
				ContentsScene.BoxPlanes(new float3(2.002f, 0.5f, 0.5f), new float3(3, 1.5f, 1.5f))
			};
			var corners = SharedCornerSurvey.SharedCorners(planes);
			var corner = corners.First(c => c.brushA == 0 && c.brushB == 1 && math.distance(c.position, new float3(2, 1, 1)) < 0.00001f);
			Assert.That(corner.isolated, Is.False);
		}

		[Test]
		public void NearMissPopulations_HaveCornersThatAreNotIsolated([Values] bool slivers)
		{
			var notIsolated = 0;
			var pairsCrossingTheirBox = 0;
			var pairs = 0;
			for (int seed = 1; seed <= 20; seed++)
			{
				var scene   = slivers ? ContentsSceneGenerators.NearMissSlivers(seed, rotated: false)
									  : ContentsSceneGenerators.NearMissLedges(seed, rotated: false);
				var oracle  = new ContentsOracle(scene);
				var brushes = oracle.Brushes;
				var planes  = brushes.Select(oracle.TreePlanesOf).ToList();
				var corners = SharedCornerSurvey.SharedCorners(planes);
				notIsolated += corners.Count(c => !c.isolated);
				for (int w = 0; w + 1 < brushes.Count; w += 2)
				{
					pairs++;
					if (corners.Count(c => c.brushA == w && c.brushB == w + 1 && !c.isolated) >= 2)
						pairsCrossingTheirBox++;
				}
			}
			Assert.That(notIsolated, Is.GreaterThan(0));
			if (slivers)
				Assert.That(pairsCrossingTheirBox, Is.EqualTo(pairs), "every sliver pair has its two near crossings");
		}
		#endregion

		#region Survey
		const int kSeeds = 50;

		static readonly (string name, Func<int, ContentsScene> make)[] kPopulations =
		{
			("near-miss slivers",        seed => ContentsSceneGenerators.NearMissSlivers(seed, rotated: false)),
			("near-miss slivers, turned",seed => ContentsSceneGenerators.NearMissSlivers(seed, rotated: true)),
			("near-miss ledges",         seed => ContentsSceneGenerators.NearMissLedges(seed, rotated: false)),
			("near-miss boxes",          seed => ContentsSceneGenerators.NearMissBoxes(seed, rotated: false)),
			("near-miss boxes, turned", seed => ContentsSceneGenerators.NearMissBoxes(seed, rotated: true)),
			("grid boxes",              seed => ContentsSceneGenerators.AllSolid(ContentsSceneGenerators.GridBoxes(seed))),
			("cut brushes",             seed => ContentsSceneGenerators.AllSolid(ContentsSceneGenerators.CutBrushes(seed))),
		};

		static readonly SharedCornerSurvey.Verdict[] kReported =
		{
			SharedCornerSurvey.Verdict.Same, SharedCornerSurvey.Verdict.Apart, SharedCornerSurvey.Verdict.OneVsTwo,
			SharedCornerSurvey.Verdict.Other, SharedCornerSurvey.Verdict.Missing
		};

		sealed class Tally
		{
			public int scenes, corners, hidden;
			// Every isolated corner, hidden or not, and how many of them the output gets wrong by the oracle
			public int isolatedCorners, isolatedWrong;
			public readonly int[] isolated    = new int[Enum.GetValues(typeof(SharedCornerSurvey.Verdict)).Length];
			public readonly int[] notIsolated = new int[Enum.GetValues(typeof(SharedCornerSurvey.Verdict)).Length];
			public readonly List<float> distances = new List<float>();
		}

		sealed class Case
		{
			public int setting;
			public string population;
			public int seed;
			public string pair;
			public float3 position;
			public SharedCornerSurvey.Result result;
			public bool isolated;
			// Isolated corners only: which one, and what the oracle says is wrong with the output there
			public int isolatedIndex;
			public string wrong;
		}

		// An isolated corner as one setting drew it
		readonly struct IsolatedOutcome
		{
			public readonly SharedCornerSurvey.Verdict verdict;
			// Null where the output agrees with the oracle
			public readonly string wrong;
			public IsolatedOutcome(SharedCornerSurvey.Verdict verdict, string wrong) { this.verdict = verdict; this.wrong = wrong; }
			public string Describe() => $"{verdict} ({wrong ?? "right"})";
		}

		readonly struct Setting
		{
			public readonly string title;
			public readonly Action apply;
			public Setting(string title, Action apply) { this.title = title; this.apply = apply; }
		}

		[Test, Explicit]
		public void Survey_GateOffThenOn()
		{
			var original = CompactHierarchyManager.TreeUpdate.kUseIncidenceWeld;
			RunSurvey("SharedCornerSurvey", "the gate",
					  new[]
					  {
						  new Setting("GATE OFF (shipped; the control)",    () => CompactHierarchyManager.TreeUpdate.kUseIncidenceWeld = false),
						  new Setting("GATE ON (kUseIncidenceWeld = true)", () => CompactHierarchyManager.TreeUpdate.kUseIncidenceWeld = true),
					  },
					  () => CompactHierarchyManager.TreeUpdate.kUseIncidenceWeld = original);
		}

		// The shipped state against itself. Every control above assumes the pipeline gives the same output for the same
		// scene twice; any difference reported here is nondeterminism, not a setting.
		[Test, Explicit]
		public void Survey_ControlAgainstItself()
		{
			var originalGate  = CompactHierarchyManager.TreeUpdate.kUseIncidenceWeld;
			var originalStage = CompactHierarchyManager.TreeUpdate.kCanonicalVertexStage;
			Action shipped = () =>
			{
				CompactHierarchyManager.TreeUpdate.kUseIncidenceWeld     = false;
				CompactHierarchyManager.TreeUpdate.kCanonicalVertexStage = (int)CanonicalVertexStage.Off;
			};
			RunSurvey("SharedCornerSurveyDeterminism", "a second run of the same build",
					  new[] { new Setting("SHIPPED (first run)", shipped), new Setting("SHIPPED (second run)", shipped) },
					  () =>
					  {
						  CompactHierarchyManager.TreeUpdate.kUseIncidenceWeld     = originalGate;
						  CompactHierarchyManager.TreeUpdate.kCanonicalVertexStage = originalStage;
					  });
		}

		// Canonical vertices (Documentation~/Design/CanonicalVertices.md): every stage against the shipped state. The bar
		// is two-sided splits at the control's level and one-sided misses down by at least 90%.
		[Test, Explicit]
		public void Survey_CanonicalVertexStages()
		{
			var original = CompactHierarchyManager.TreeUpdate.kCanonicalVertexStage;
			Action Stage(CanonicalVertexStage value) => () => CompactHierarchyManager.TreeUpdate.kCanonicalVertexStage = (int)value;
			RunSurvey("CanonicalVertexSurvey", "canonical vertices",
					  new[]
					  {
						  new Setting("CANONICAL OFF (shipped; the control)", Stage(CanonicalVertexStage.Off)),
						  new Setting("CANONICAL POSITIONS (stage 2)",        Stage(CanonicalVertexStage.Positions)),
						  new Setting("CANONICAL LOOP IDENTITY (stage 3)",    Stage(CanonicalVertexStage.LoopIdentity)),
						  new Setting("CANONICAL EVERYWHERE (stage 4)",       Stage(CanonicalVertexStage.Everywhere)),
					  },
					  () => CompactHierarchyManager.TreeUpdate.kCanonicalVertexStage = original);
		}

		static void RunSurvey(string reportName, string changedBy, Setting[] settings, Action restore)
		{
			var cases    = new List<Case>();
			// Isolated corners where the output contradicts the oracle, and where the two sides put the corner in
			// different places
			var wrongCases    = new List<Case>();
			var positionLeaks = new List<string>();
			var outputs  = new Dictionary<(int, string, int), List<float3>>();
			var tallies  = new Dictionary<(int, string), Tally>();
			var stats    = new string[settings.Length];
			// Every isolated corner's outcome, per setting, in corner order
			var isolatedOutcomes = new Dictionary<(int, string, int), List<IsolatedOutcome>>();
			// What the oracle expects of each side of every isolated corner, in corner order; the same for every setting
			var expectations = new Dictionary<(string, int), List<(SharedCornerSurvey.Expectation a, SharedCornerSurvey.Expectation b)>>();
			(SharedCornerSurvey.Corner corner, List<List<float3>> a, List<List<float3>> b)? isolatedSample = null;

			var wasExactCSG = CompactHierarchyManager.TreeUpdate.kExactCSG;
			try
			{
				CompactHierarchyManager.TreeUpdate.kExactCSG = false;
				for (int setting = 0; setting < settings.Length; setting++)
				{
					settings[setting].apply();
					CanonicalVertexStats.Reset();
					foreach (var (name, make) in kPopulations)
					{
						var tally = new Tally();
						tallies[(setting, name)] = tally;
						for (int seed = 1; seed <= kSeeds; seed++)
						{
							var scene   = make(seed);
							var oracle  = new ContentsOracle(scene);
							var brushes = oracle.Brushes;
							var planes  = brushes.Select(oracle.TreePlanesOf).ToList();

							// Per brush, per plane: the distinct vertices it drew on that face
							var byPlane = planes.Select(p => p.Select(_ => new List<float3>()).ToList()).ToList();
							var all     = new List<float3>();
							using (var harness = ContentsTreeHarness.Build(scene))
							{
								Assert.That(harness.Update(), Is.True, $"{name} seed {seed}: the CSG update did not run");
								var index = new Dictionary<ContentsSceneNode, int>();
								for (int i = 0; i < brushes.Count; i++)
									index[brushes[i]] = i;
								foreach (var triangle in harness.Triangles)
								{
									var node = harness.NodeOf(triangle.brushID);
									if (node == null)
										continue;
									var brush = index[node];
									all.Add(triangle.a); all.Add(triangle.b); all.Add(triangle.c);
									var plane = SharedCornerSurvey.PlaneOf(triangle.surfaceNormal, triangle.Center, planes[brush]);
									if (plane < 0)
										continue;
									byPlane[brush][plane].Add(triangle.a);
									byPlane[brush][plane].Add(triangle.b);
									byPlane[brush][plane].Add(triangle.c);
								}
							}
							byPlane = byPlane.Select(faces => faces.Select(SharedCornerSurvey.Distinct).ToList()).ToList();
							outputs[(setting, name, seed)] = SharedCornerSurvey.Distinct(all);
							tally.scenes++;

							var corners = SharedCornerSurvey.SharedCorners(planes);
							if (!expectations.TryGetValue((name, seed), out var expected))
							{
								expected = corners.Where(c => c.isolated)
												  .Select(c => (SharedCornerSurvey.Expect(oracle, brushes[c.brushA], c.planesA, c.position),
																SharedCornerSurvey.Expect(oracle, brushes[c.brushB], c.planesB, c.position)))
												  .ToList();
								expectations[(name, seed)] = expected;
							}

							var outcomes = new List<IsolatedOutcome>();
							isolatedOutcomes[(setting, name, seed)] = outcomes;
							foreach (var corner in corners)
							{
								var result = SharedCornerSurvey.ClassifyCorner(corner, byPlane[corner.brushA], byPlane[corner.brushB]);
								var pair   = brushes[corner.brushA].name + "/" + brushes[corner.brushB].name;
								if (corner.isolated)
								{
									var isolatedIndex = outcomes.Count;
									var wrong = Judge(expected[isolatedIndex], result);
									outcomes.Add(new IsolatedOutcome(result.verdict, wrong));
									tally.isolatedCorners++;
									if (wrong != null)
									{
										tally.isolatedWrong++;
										wrongCases.Add(new Case
										{
											setting = setting, population = name, seed = seed, position = corner.position, pair = pair,
											result = result, isolated = true, isolatedIndex = isolatedIndex, wrong = wrong
										});
									}
									if (result.isPositionDisagreement)
									{
										positionLeaks.Add($"[{setting}] {name} seed {seed}, isolated corner {isolatedIndex}, {pair} at {Format(corner.position)}: " +
														  $"{result.verdict}, {result.distance * 1000:0.###} mm");
									}
								}
								if (result.verdict == SharedCornerSurvey.Verdict.Hidden)
								{
									tally.hidden++;
									continue;
								}
								tally.corners++;
								(corner.isolated ? tally.isolated : tally.notIsolated)[(int)result.verdict]++;
								if (result.isDisagreement)
								{
									if (!float.IsNaN(result.distance))
										tally.distances.Add(result.distance);
									cases.Add(new Case
									{
										setting = setting, population = name, seed = seed, position = corner.position,
										pair = pair, result = result, isolated = corner.isolated
									});
								}
								else if (setting == 0 && corner.isolated && isolatedSample == null &&
										 result.verdict == SharedCornerSurvey.Verdict.Same)
								{
									isolatedSample = (corner, byPlane[corner.brushA], byPlane[corner.brushB]);
								}
							}
						}
					}
					stats[setting] = CanonicalVertexStats.Describe();
				}
			}
			finally
			{
				restore();
				CompactHierarchyManager.TreeUpdate.kExactCSG = wasExactCSG;
			}

			var controlPassed = false;
			var controlText = "no isolated corner where both sides agree was found";
			if (isolatedSample.HasValue)
			{
				var (corner, a, b) = isolatedSample.Value;
				var moved = a.Select(face => face.Select(v => math.distance(v, corner.position) <= SharedCornerSurvey.kWeldRadius ? v + kTwoMillimetres : v).ToList()).ToList();
				var control = SharedCornerSurvey.ClassifyCorner(corner, moved, b);
				controlPassed = control.isDisagreement && math.abs(control.distance - 0.002f) < 0.00005f;
				controlText = $"moving one side's vertices 2 mm at the isolated corner {Format(corner.position)} gives {control.verdict} " +
							  $"at {control.distance * 1000:0.###} mm: {(controlPassed ? "PASS" : "FAIL")}";
			}

			var changes = new List<string>();
			var changedVerdicts = new int[settings.Length];
			var fixes   = new int[settings.Length];
			var breaks  = new int[settings.Length];
			for (int setting = 1; setting < settings.Length; setting++)
			{
				foreach (var (name, _) in kPopulations)
				{
					for (int seed = 1; seed <= kSeeds; seed++)
					{
						var control = isolatedOutcomes[(0, name, seed)];
						var other   = isolatedOutcomes[(setting, name, seed)];
						for (int i = 0; i < control.Count && i < other.Count; i++)
						{
							if (control[i].verdict == other[i].verdict)
								continue;
							changedVerdicts[setting]++;
							if (control[i].wrong != null && other[i].wrong == null) fixes[setting]++;
							if (control[i].wrong == null && other[i].wrong != null) breaks[setting]++;
							changes.Add($"[{setting}] {name} seed {seed}, isolated corner {i}: {control[i].Describe()} in the control, {other[i].Describe()} here");
						}
					}
				}
			}

			var report = new StringBuilder();
			report.AppendLine($"Shared-corner survey of {changedBy}, {DateTime.Now:yyyy-MM-dd HH:mm}, seeds 1..{kSeeds} per population");
			report.AppendLine("A corner is a point on the surfaces of two brushes. Per pair of faces through it (one of each brush), the");
			report.AppendLine($"vertices each face drew within {SharedCornerSurvey.kWeldRadius * 1000:0.#} mm are compared by their nearest one: 'same' within {SharedCornerSurvey.kSamePosition * 1000:0.##} mm.");
			report.AppendLine("A corner takes its worst face pair. 'missing' = one brush drew nothing near the corner on its faces through it.");
			report.AppendLine("Isolated corners have no other face within the weld radius, so the two sides can't put them in different");
			report.AppendLine("places (negative control). Whether each side draws there is judged against the oracle instead: a face lost or");
			report.AppendLine("kept anywhere along its outline shows up at its isolated corners too, whatever the weld did there.");
			report.AppendLine();
			for (int setting = 0; setting < settings.Length; setting++)
			{
				report.AppendLine($"[{setting}] {settings[setting].title}");
				foreach (var (name, _) in kPopulations)
				{
					var tally = tallies[(setting, name)];
					report.AppendLine($"  {name}: {tally.scenes} scenes, {tally.corners} corners drawn, {tally.hidden} hidden");
					report.AppendLine($"    isolated:     {Counts(tally.isolated)}; the oracle contradicts {tally.isolatedWrong} of {tally.isolatedCorners}");
					report.AppendLine($"    not isolated: {Counts(tally.notIsolated)}");
					report.AppendLine($"    disagreement distances: {Distances(tally.distances)}");
				}
				report.AppendLine($"  canonical vertices: {stats[setting]}");
				report.AppendLine();
			}

			report.AppendLine("Scenes whose output a setting changed from the control's (any vertex added, removed or moved):");
			for (int setting = 1; setting < settings.Length; setting++)
			{
				report.AppendLine($"  [{setting}] {settings[setting].title}");
				foreach (var (name, _) in kPopulations)
				{
					var changed = 0;
					for (int seed = 1; seed <= kSeeds; seed++)
					{
						if (!SamePositions(outputs[(0, name, seed)], outputs[(setting, name, seed)]))
							changed++;
					}
					report.AppendLine($"    {name}: {changed} of {kSeeds}");
				}
			}
			report.AppendLine();
			report.AppendLine("Positive control: " + controlText);
			report.AppendLine($"Negative control, isolated corners where the two sides put the corner in different places: {positionLeaks.Count}");
			foreach (var leak in positionLeaks.Take(10))
				report.AppendLine("  " + leak);
			report.AppendLine();
			report.AppendLine("Isolated corners where the output contradicts the oracle, first 10 per setting:");
			for (int setting = 0; setting < settings.Length; setting++)
			{
				foreach (var item in wrongCases.Where(c => c.setting == setting).Take(10))
				{
					report.AppendLine($"  [{setting}] {item.population} seed {item.seed}, isolated corner {item.isolatedIndex}, {item.pair} (A/B) " +
									  $"at {Format(item.position)}: {item.wrong} ({item.result.verdict})");
				}
			}
			report.AppendLine();
			report.AppendLine("Isolated corners whose verdict a setting changed from the control's (information, judged by the oracle):");
			for (int setting = 1; setting < settings.Length; setting++)
				report.AppendLine($"  [{setting}] {changedVerdicts[setting]} changed: {fixes[setting]} were wrong and are now right, {breaks[setting]} were right and are now wrong");
			foreach (var change in changes.Take(25))
				report.AppendLine("  " + change);
			report.AppendLine();
			report.AppendLine("Disagreements, first 25 per setting:");
			for (int setting = 0; setting < settings.Length; setting++)
			{
				foreach (var item in cases.Where(c => c.setting == setting).Take(25))
				{
					report.AppendLine($"  [{setting}] {item.population} seed {item.seed}, {item.pair} at {Format(item.position)}" +
									  $"{(item.isolated ? " (isolated)" : "")}: {item.result.verdict}, " +
									  $"{(float.IsNaN(item.result.distance) ? "nothing to measure" : $"{item.result.distance * 1000:0.###} mm")}" +
									  $" ({item.result.countA} vs {item.result.countB} vertices)");
				}
			}

			var text = report.ToString();
			var path = Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath, "..", "Logs", reportName + ".txt"));
			File.WriteAllText(path, text);
			UnityEngine.Debug.Log($"Shared-corner survey written to {path}\n{text}");

			var examined = tallies.Values.Sum(t => t.corners);
			Assert.That(examined, Is.GreaterThan(0), "the survey found no drawn corners at all");
			Assert.That(controlPassed, Is.True, controlText);
			Assert.That(positionLeaks, Is.Empty, "the two sides put an isolated corner in different places, where nothing near it could move it");
			Assert.Pass($"{examined} corners examined; report at {path}");
		}

		// What is wrong with the output at an isolated corner by the oracle, or null when nothing is
		static string Judge((SharedCornerSurvey.Expectation a, SharedCornerSurvey.Expectation b) expected, SharedCornerSurvey.Result result)
		{
			var wrongA = SharedCornerSurvey.JudgeSide(expected.a, result.drewA);
			var wrongB = SharedCornerSurvey.JudgeSide(expected.b, result.drewB);
			if (wrongA == null && wrongB == null)
				return null;
			if (wrongA != null && wrongB != null)
				return $"A {wrongA}, B {wrongB}";
			return wrongA != null ? "A " + wrongA : "B " + wrongB;
		}

		static string Counts(int[] counts)
		{
			return string.Join(", ", kReported.Select(v => $"{v.ToString().ToLowerInvariant()} {counts[(int)v]}"));
		}

		// The bands of the 13,106 welds the gate refused on bm_c0a0a, so the two can be compared
		static string Distances(List<float> distances)
		{
			if (distances.Count == 0)
				return "none";
			var sorted = distances.Select(d => d * 1000).OrderBy(d => d).ToList();
			var median = sorted[sorted.Count / 2];
			int Bin(float lo, float hi) => sorted.Count(d => d >= lo && d < hi);
			return $"n={sorted.Count}, min {sorted[0]:0.###} mm, median {median:0.###} mm, max {sorted[sorted.Count - 1]:0.###} mm; " +
				   $"0.05-0.6: {Bin(0.05f, 0.6f)}, 0.6-1.2: {Bin(0.6f, 1.2f)}, 1.2-3: {Bin(1.2f, 3)}, 3-6: {Bin(3, 6)}, " +
				   $"6-12.5: {Bin(6, 12.5f)}, 12.5+: {Bin(12.5f, float.MaxValue)}";
		}

		static bool SamePositions(List<float3> a, List<float3> b)
		{
			if (a.Count != b.Count)
				return false;
			var remaining = new List<float3>(b);
			foreach (var position in a)
			{
				var index = remaining.FindIndex(p => math.distance(p, position) <= 0.0000001f);
				if (index < 0)
					return false;
				remaining.RemoveAt(index);
			}
			return true;
		}

		static string Format(float3 position) => $"({position.x:0.####}, {position.y:0.####}, {position.z:0.####})";
		#endregion
	}
}
