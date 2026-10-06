using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Unity.Mathematics;
using static Chisel.Core.Tests.ContentsListScope;

namespace Chisel.Core.Tests
{
	[TestFixture]
	[Category("Contents")]
	public class ContentsSceneTests
	{
		const int kSolid = ContentsSceneNode.kSolidContents;

		ContentsListScope list;

		[SetUp]    public void InstallList()   { list = new ContentsListScope(); }
		[TearDown] public void RestoreList()   { list.Dispose(); list = null; }

		static ContentsSceneNode Box(string name, float3 min, float3 max, int contents = kSolid,
									 CSGOperationType operation = CSGOperationType.Additive)
		{
			return ContentsSceneNode.Brush(ContentsScene.BoxPlanes(min, max), contents, operation, name: name);
		}

		static ContentsScene Scene(params ContentsSceneNode[] roots)
		{
			var scene = new ContentsScene();
			foreach (var root in roots)
				scene.Add(root);
			return scene;
		}

		static ContentsScene Reversed(ContentsScene scene)
		{
			var result = new ContentsScene();
			for (int i = scene.roots.Count - 1; i >= 0; i--)
				result.Add(scene.roots[i]);
			return result;
		}

		#region Fixed scenes
		// bm_c0a0a's frame and pane: the post's face behind the pane's end is drawn once the pane is glass,
		// and the pane's end against the post is still removed.
		[Test]
		public void PostAndGlassPane_KeepThePostFace([Values] bool paneFirst)
		{
			var post = Box("post", new float3(-1, -1, 0), new float3(1, 1, 4));
			var pane = Box("pane", new float3(-0.5f, 1, 0), new float3(0.5f, 5, 4), kGlass);
			var scene = paneFirst ? Scene(pane, post) : Scene(post, pane);
			ContentsComparison.BuildAndAssertMatchesOracle(scene, "post and glass pane");
		}

		// Hook 2: a brush wholly inside another. Solid inside water keeps its faces, water inside solid loses
		// them, and inside its own type a brush loses them as it always has.
		[Test]
		public void BrushWhollyInsideAnother_FollowsTheRules([Values(kSolid, kGlass, kWater)] int inner,
															  [Values(kSolid, kGlass, kWater)] int outer,
															  [Values] bool innerFirst)
		{
			var small = Box("inner", new float3(1), new float3(2), inner);
			var large = Box("outer", new float3(0), new float3(4), outer);
			var scene = innerFirst ? Scene(small, large) : Scene(large, small);
			ContentsComparison.BuildAndAssertMatchesOracle(scene, $"type {inner} inside type {outer}");
		}

		// Two types overlapping: each keeps its faces inside the other.
		[Test]
		public void GlassOverlappingWater_KeepsBothInteriors([Values] bool glassFirst)
		{
			var glass = Box("glass", new float3(0), new float3(2), kGlass);
			var water = Box("water", new float3(1), new float3(3), kWater);
			var scene = glassFirst ? Scene(glass, water) : Scene(water, glass);
			ContentsComparison.BuildAndAssertMatchesOracle(scene, "glass overlapping water");
		}

		// Coplanar faces facing the same way: the tie goes to the earlier type, whichever brush comes first,
		// and to Solid over any other type.
		[Test]
		public void CoplanarTie_GoesToTheEarlierType([Values(kSolid, kGlass)] int winner, [Values] bool winnerFirst)
		{
			var large = Box("large", new float3(0), new float3(2), winner);
			var half  = Box("half",  new float3(0), new float3(2, 2, 1), kWater);
			var scene = winnerFirst ? Scene(large, half) : Scene(half, large);
			ContentsComparison.BuildAndAssertMatchesOracle(scene, $"type {winner} tied with water");
		}

		// Faces touching back to back across three types: solid against glass, glass against water.
		[Test]
		public void BackToBack_AcrossTypes()
		{
			var scene = Scene(Box("solid", new float3(0, 0, 0), new float3(1, 2, 2)),
							  Box("glass", new float3(1, 0, 0), new float3(2, 2, 2), kGlass),
							  Box("water", new float3(2, 0, 0), new float3(3, 2, 2), kWater));
			ContentsComparison.BuildAndAssertMatchesOracle(scene, "solid, glass, water in a row");
			ContentsComparison.BuildAndAssertMatchesOracle(Reversed(scene), "water, glass, solid in a row");
		}

		// A carve removes every type, and its walls are Solid: they show where it cut the floor, not where it
		// cut the glass.
		[Test]
		public void CarveThroughSolidAndGlass()
		{
			var scene = Scene(Box("floor", new float3(0, 0, 0), new float3(6, 6, 1)),
							  Box("pane",  new float3(2, 0, 1), new float3(2.5f, 6, 3), kGlass),
							  Box("carve", new float3(1, 1, 0.5f), new float3(5, 5, 4), operation: CSGOperationType.Subtractive));
			ContentsComparison.BuildAndAssertMatchesOracle(scene, "carve through solid and glass");
		}

		// The design's pool: carved into the floor and then filled with water. The pool walls stay visible
		// below the water surface.
		[Test]
		public void FloorPoolWater_KeepsThePoolWalls()
		{
			var scene = Scene(Box("floor", new float3(0, 0, 0), new float3(6, 6, 2)),
							  Box("pool",  new float3(1, 1, 1), new float3(5, 5, 3), operation: CSGOperationType.Subtractive),
							  Box("water", new float3(1, 1, 1), new float3(5, 5, 1.75f), kWater));
			ContentsComparison.BuildAndAssertMatchesOracle(scene, "floor, pool and water");
		}

		// A brush inside a subtractive composite carves, whatever its type, and its walls take its own type.
		[Test]
		public void GlassInsideACarvingComposite_Carves([Values(CSGOperationType.Subtractive, CSGOperationType.Intersecting)] CSGOperationType operation)
		{
			var scene = Scene(Box("floor", new float3(0, 0, 0), new float3(6, 6, 2)),
							  Box("water", new float3(0, 0, 2), new float3(6, 3, 3), kWater),
							  ContentsSceneNode.Composite(operation,
								  Box("cut", new float3(1, 1, 1), new float3(5, 5, 4), kGlass)));
			ContentsComparison.BuildAndAssertMatchesOracle(scene, $"glass in a {operation} composite");
		}

		static IEnumerable<TestCaseData> CarvingOnAnotherType()
		{
			foreach (var baseType in new[] { kSolid, kGlass, kWater })
			foreach (var cutType in new[] { kSolid, kGlass, kGrate })
			{
				if (baseType == cutType)
					continue;
				foreach (var operation in new[] { CSGOperationType.Intersecting, CSGOperationType.Subtractive })
				foreach (var inComposite in new[] { false, true })
					yield return new TestCaseData(baseType, cutType, operation, inComposite);
			}
		}

		[TestCaseSource(nameof(CarvingOnAnotherType))]
		public void CarvingBrush_OnTheFacesOfAnotherType(int baseType, int cutType, CSGOperationType operation, bool inComposite)
		{
			var block = Box("block", new float3(0), new float3(2), baseType);
			var cut   = Box("cut",   new float3(0), new float3(2, 2, 1), cutType, inComposite ? CSGOperationType.Additive : operation);
			var scene = Scene(block, inComposite ? ContentsSceneNode.Composite(operation, cut) : cut);
			ContentsComparison.BuildAndAssertMatchesOracle(scene, $"{operation} type {cutType} on the faces of type {baseType}{(inComposite ? ", in a composite" : "")}");
		}

		// A carving brush touching a brush of another type from outside, back to back: that brush keeps its face,
		// and the carving brush leaves no wall on it. Generated grid seeds 18 and 22 found this.
		[Test]
		public void CarvingBrush_BackToBackWithAnotherType([Values(kSolid, kWater)] int baseType, [Values(kGlass, kGrate)] int cutType,
														   [Values(CSGOperationType.Subtractive, CSGOperationType.Intersecting)] CSGOperationType operation,
														   [Values] bool inComposite)
		{
			var block = Box("block", new float3(0), new float3(4, 4, 2), baseType);
			var cut   = Box("cut",   new float3(1, 1, 2), new float3(3, 3, 3), cutType, inComposite ? CSGOperationType.Additive : operation);
			var scene = Scene(block, inComposite ? ContentsSceneNode.Composite(operation, cut) : cut);
			ContentsComparison.BuildAndAssertMatchesOracle(scene, $"{operation} type {cutType} on top of type {baseType}{(inComposite ? ", in a composite" : "")}");
		}

		// An additive composite doesn't make its brushes carve: water in one is looked through like any water.
		[Test]
		public void WaterInsideAnAdditiveComposite_IsLookedThrough()
		{
			var scene = Scene(Box("post", new float3(-1, -1, 0), new float3(1, 1, 4)),
							  ContentsSceneNode.Composite(CSGOperationType.Additive,
								  Box("pool",  new float3(-3, -3, 0), new float3(3, 3, 2), kWater),
								  Box("spill", new float3(-0.5f, 1, 0), new float3(0.5f, 5, 3), kWater)));
			ContentsComparison.BuildAndAssertMatchesOracle(scene, "water in an additive composite");
		}
		#endregion

		#region Generated scenes
		static readonly int[] kGridDefects    = { 11, 37 };
		static readonly int[] kRotatedDefects = { 11 };
		static readonly int[] kFreeDefects    = { 14, 21, 23, 25, 26 };
		const int kCarvingTieLimit = 7;

		static IEnumerable<int> Seeds(int count, params int[] skip) => Enumerable.Range(1, count).Where(seed => !skip.Contains(seed));
		static IEnumerable<int> GridSeeds()    => Seeds(80, kGridDefects.Append(kCarvingTieLimit).ToArray());
		static IEnumerable<int> RotatedSeeds() => Seeds(30, kRotatedDefects.Append(kCarvingTieLimit).ToArray());
		static IEnumerable<int> FreeSeeds()    => Seeds(30, kFreeDefects);
		static IEnumerable<int> CutSeeds()     => Seeds(30);

		[Test]
		public void GridBoxes_WithTypes_MatchTheOracle([ValueSource(nameof(GridSeeds))] int seed)
		{
			ContentsComparison.BuildAndAssertMatchesOracle(ContentsSceneGenerators.GridBoxes(seed), "grid boxes with types, seed " + seed);
		}

		[Test]
		public void RotatedGridBoxes_WithTypes_MatchTheOracle([ValueSource(nameof(RotatedSeeds))] int seed)
		{
			ContentsComparison.BuildAndAssertMatchesOracle(ContentsSceneGenerators.RotatedGridBoxes(seed), "rotated grid boxes with types, seed " + seed);
		}

		[Test]
		public void FreeBrushes_WithTypes_MatchTheOracle([ValueSource(nameof(FreeSeeds))] int seed)
		{
			ContentsComparison.BuildAndAssertMatchesOracle(ContentsSceneGenerators.FreeBrushes(seed), "free brushes with types, seed " + seed);
		}

		[Test]
		public void CutBrushes_WithTypes_MatchTheOracle([ValueSource(nameof(CutSeeds))] int seed)
		{
			ContentsComparison.BuildAndAssertMatchesOracle(ContentsSceneGenerators.CutBrushes(seed), "cut brushes with types, seed " + seed);
		}
		#endregion

		#region Properties
		// Every brush sharing one type, whichever it is, draws exactly what the all-Solid scene draws.
		[Test]
		public void OneTypeEverywhere_DrawsWhatAllSolidDraws([Values(kGlass, kGrate, kWater)] int type, [Values(1, 2, 3, 4)] int seed)
		{
			var solid = ContentsSceneGenerators.AllSolid(ContentsSceneGenerators.GridBoxes(seed));
			var typed = WithType(ContentsSceneGenerators.GridBoxes(seed), type);
			var expected = Capture(solid);
			var actual   = Capture(typed);
			Assert.That(actual.Count, Is.EqualTo(expected.Count), $"seed {seed}: triangle count");
			for (int i = 0; i < expected.Count; i++)
			{
				if (!expected[i].Equals(actual[i]))
					Assert.Fail($"seed {seed}: triangle {i} differs: {expected[i]} in the all-Solid scene, {actual[i]} with every brush type {type}");
			}
		}

		static ContentsScene WithType(ContentsScene scene, int type)
		{
			void Apply(List<ContentsSceneNode> nodes)
			{
				foreach (var node in nodes)
				{
					if (node.IsBrush) node.contents = type;
					else Apply(node.children);
				}
			}
			Apply(scene.roots);
			return scene;
		}

		// Every triangle as (brush name, corners), in a stable order
		static List<string> Capture(ContentsScene scene)
		{
			using (var harness = ContentsTreeHarness.Build(scene))
			{
				Assert.That(harness.Update(), Is.True, "the CSG update did not run");
				return harness.Triangles
							  .Select(t => $"{harness.NodeOf(t.brushID)?.name}: {Format(t.a)} {Format(t.b)} {Format(t.c)}")
							  .OrderBy(s => s, System.StringComparer.Ordinal)
							  .ToList();
			}
		}

		static string Format(float3 v) => $"({v.x:R}, {v.y:R}, {v.z:R})";
		#endregion

		#region Incremental updates
		// Only the pane changes, but the post's face behind it has to follow: changing a brush's type rebuilds
		// the brushes it touches.
		[Test]
		public void ChangingAType_RebuildsTheBrushesItTouches()
		{
			var post = Box("post", new float3(-1, -1, 0), new float3(1, 1, 4));
			var pane = Box("pane", new float3(-0.5f, 1, 0), new float3(0.5f, 5, 4));
			using (var harness = ContentsTreeHarness.Build(Scene(post, pane)))
			{
				Assert.That(harness.Update(), Is.True);
				ContentsComparison.AssertMatchesOracle(harness, "both solid");

				harness.SetContents(pane, kGlass);
				Assert.That(harness.Update(), Is.True, "changing a type updated nothing");
				ContentsComparison.AssertMatchesOracle(harness, "after the pane became glass");

				harness.SetContents(pane, kSolid);
				Assert.That(harness.Update(), Is.True);
				ContentsComparison.AssertMatchesOracle(harness, "after the pane became solid again");
			}
		}

		// A water brush wholly inside solid: its faces come and go with its type and with the outer brush's.
		[Test]
		public void ChangingATypeInside_RebuildsBothBrushes()
		{
			var outer = Box("outer", new float3(0), new float3(4));
			var inner = Box("inner", new float3(1), new float3(2), kWater);
			using (var harness = ContentsTreeHarness.Build(Scene(outer, inner)))
			{
				Assert.That(harness.Update(), Is.True);
				ContentsComparison.AssertMatchesOracle(harness, "water inside solid");

				harness.SetContents(outer, kGlass);
				Assert.That(harness.Update(), Is.True);
				ContentsComparison.AssertMatchesOracle(harness, "water inside glass");

				harness.SetContents(inner, kGlass);
				Assert.That(harness.Update(), Is.True);
				ContentsComparison.AssertMatchesOracle(harness, "glass inside glass");
			}
		}

		// Random type changes on generated scenes: after every one, the incremental update has to draw what the
		// oracle says the scene draws now.
		[Test]
		public void RandomTypeChanges_IncrementalMatchesTheOracle([Values(1, 2, 3, 4)] int seed)
		{
			var scene   = ContentsSceneGenerators.GridBoxes(seed);
			var brushes = new ContentsOracle(scene).Brushes;
			var random  = new Unity.Mathematics.Random((uint)(seed * 2654435761u) | 1u);
			using (var harness = ContentsTreeHarness.Build(scene))
			{
				Assert.That(harness.Update(), Is.True);
				ContentsComparison.AssertMatchesOracle(harness, $"seed {seed} as generated");
				for (int step = 0; step < 6; step++)
				{
					var brush = brushes[random.NextInt(brushes.Count)];
					var type  = (brush.contents + random.NextInt(1, kDefaultNames.Length)) % kDefaultNames.Length;
					harness.SetContents(brush, type);
					Assert.That(harness.Update(), Is.True, $"seed {seed} step {step}: changing {brush.name} to type {type} updated nothing");
					ContentsComparison.AssertMatchesOracle(harness, $"seed {seed} step {step}, {brush.name} now type {type}");
				}
			}
		}

		// An index past the end of the list builds as Solid, and growing the list to include it rebuilds the
		// tree without any brush being touched. Renaming an entry rebuilds nothing.
		[Test]
		public void TheListsLength_DecidesWhatAnIndexBuildsAs()
		{
			list.Install(ChiselContentsList.kSolidName, "Glass", "Grate");
			var post = Box("post", new float3(-1, -1, 0), new float3(1, 1, 4));
			var pane = Box("pane", new float3(-0.5f, 1, 0), new float3(0.5f, 5, 4), kWater);
			using (var harness = ContentsTreeHarness.Build(Scene(post, pane)))
			{
				harness.JudgeEachUpdate = false;    // the pane builds as Solid while the description still says water
				Assert.That(harness.Update(), Is.True);
				pane.contents = kSolid;     // the oracle's view only: this is what the pane builds as
				ContentsComparison.AssertMatchesOracle(harness, "water past the end of the list");
				pane.contents = kWater;

				list.Install(kDefaultNames);
				Assert.That(harness.Update(), Is.True, "growing the list rebuilt nothing");
				ContentsComparison.AssertMatchesOracle(harness, "water once the list has it");

				list.Install(ChiselContentsList.kSolidName, "Glass", "Grate", "Liquid");
				Assert.That(harness.Update(), Is.False, "renaming an entry rebuilt something");
			}
		}
		#endregion

		#region Control
		// With the feature switched off every brush builds as Solid, and the suite has to notice.
		[Test]
		public void WithContentsSwitchedOff_TheOracleDisagrees()
		{
			var original = CompactHierarchyManager.ContentsEnabled;
			try
			{
				CompactHierarchyManager.ContentsEnabled = false;
				var scenes = new List<(string, ContentsScene)>
				{
					("post and glass pane", Scene(Box("post", new float3(-1, -1, 0), new float3(1, 1, 4)),
												  Box("pane", new float3(-0.5f, 1, 0), new float3(0.5f, 5, 4), kGlass))),
					("solid inside water",  Scene(Box("outer", new float3(0), new float3(4), kWater),
												  Box("inner", new float3(1), new float3(2)))),
				};
				foreach (var (what, scene) in scenes)
				{
					using (var harness = ContentsTreeHarness.Build(scene))
					{
						harness.JudgeEachUpdate = false;    // the disagreement is what this control is after
						Assert.That(harness.Update(), Is.True);
						var mismatches = ContentsComparison.FindMismatches(harness, what, out var samples, out _);
						Assert.That(samples, Is.GreaterThan(0), what);
						Assert.That(mismatches, Is.Not.Empty, what + ": the oracle agrees with an output built without contents");
					}
				}
			}
			finally
			{
				CompactHierarchyManager.ContentsEnabled = original;
			}
		}
		#endregion
	}
}
