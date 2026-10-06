using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using Unity.Mathematics;

namespace Chisel.Core.Tests
{
	public static class ContentsHarvest
	{
		/// <summary>One failing spot: sample points on one brush face that lie together and fail.</summary>
		public sealed class Site
		{
			public string brushName;
			public int    face;
			public readonly List<float3> points   = new List<float3>();
			public readonly List<string> kinds    = new List<string>();
			public readonly List<string> messages = new List<string>();
			public float3 min, max;

			public string Key => $"{brushName} face {face} near ({F((min.x + max.x) * 0.5f)}, {F((min.y + max.y) * 0.5f)}, {F((min.z + max.z) * 0.5f)})";

			public string Kind
			{
				get
				{
					var counts = new Dictionary<string, int>();
					string best = null;
					foreach (var kind in kinds)
					{
						counts.TryGetValue(kind, out var count);
						counts[kind] = ++count;
						if (best == null || count > counts[best])
							best = kind;
					}
					return best;
				}
			}
		}

		/// <summary>One shrunk, reproducible disagreement.</summary>
		public sealed class Finding
		{
			public int          number;
			public string       label;
			public List<Site>   sites      = new List<Site>();
			public ContentsScene minimal;
			public List<string> mismatches = new List<string>();   // every disagreement in the minimal scene
			public List<string> unexamined = new List<string>();
			public float        extent;
			public int          brushesStart;
			public int          brushesAfter;
			public string       origin;                            // which neighbourhood it was shrunk from
			public bool         complete;                          // false when the time budget stopped the shrink
			public int          runs;
			public double       seconds;
			public string CSharp => ContentsSceneShrinker.ToCSharp(minimal, "scene");
		}

		/// <summary>What a sweep saw, including what it could NOT see.</summary>
		public sealed class Sweep
		{
			public List<ContentsComparison.Mismatch> mismatches = new List<ContentsComparison.Mismatch>();
			public List<string> unexamined = new List<string>();
			// Brushes replayed from their own mesh whose registered planes are not the scene's (ContentsTreeHarness.PlaneMismatches)
			public List<string> planeMismatches = new List<string>();
			public int  checkedSamples;
			public int  skipped;
			public bool updated;
			public bool Disagrees => mismatches.Count > 0;
			public bool Complete  => unexamined.Count == 0;
		}

		// CSG runs and what went wrong in them. An exception inside the pipeline is a finding in its own right, so it
		// is kept, not swallowed.
		sealed class Stats
		{
			public int runs;
			public readonly List<string> exceptions = new List<string>();
			public readonly HashSet<string> exceptionKinds = new HashSet<string>();
		}

		static string F(float value) => value.ToString("0.###", CultureInfo.InvariantCulture);

		public static float RequiredExtent(ContentsScene scene)
		{
			float furthest = 0f;
			foreach (var brush in AllBrushes(scene))
			{
				var planes = brush.TreePlanes();
				for (int i = 0; i < planes.Length; i++)
					furthest = math.max(furthest, math.abs(planes[i].w));
			}
			return math.max(ContentsComparison.kDefaultExtent, (furthest * 2f) + 16f);
		}

		public static IEnumerable<ContentsSceneNode> AllBrushes(ContentsScene scene)
		{
			var stack = new Stack<ContentsSceneNode>();
			for (int i = scene.roots.Count - 1; i >= 0; i--)
				stack.Push(scene.roots[i]);
			while (stack.Count > 0)
			{
				var node = stack.Pop();
				if (node.IsBrush) { yield return node; continue; }
				if (node.children == null) continue;
				for (int i = node.children.Count - 1; i >= 0; i--)
					stack.Push(node.children[i]);
			}
		}

		public static int BrushCount(ContentsScene scene)
		{
			int count = 0;
			foreach (var _ in AllBrushes(scene)) count++;
			return count;
		}

		/// <summary>Build the scene, run one update, and report every disagreement and every unexamined face.</summary>
		public static Sweep Run(ContentsScene scene, float extent, int maxReported = int.MaxValue)
		{
			var result = new Sweep();
			using (var harness = ContentsTreeHarness.Build(scene))
			{
				harness.JudgeEachUpdate = false;    // the disagreements are what a sweep is after, not a reason to throw
				result.planeMismatches.AddRange(harness.PlaneMismatches);
				result.updated = harness.Update();
				if (!result.updated)
					return result;
				result.mismatches = ContentsComparison.FindMismatchRecords(harness,
																		   out result.checkedSamples,
																		   out result.skipped,
																		   out result.unexamined,
																		   extent, maxReported);
			}
			return result;
		}

		/// <summary>
		/// Build the scene, run one update and assert it matches the oracle on every face, sampled with an extent
		/// that reaches all of them. This is what the harvested fixtures assert: the scenes sit where the level put
		/// them, often further from the origin than the sampler's default reach.
		/// </summary>
		public static void AssertMatchesOracle(ContentsScene scene, string what)
		{
			// A probe sets this to get a CSG trace of every scene a fixture asserts on (ContentsTrace, Library/ChiselHarvest/traces).
			if (TraceTo != null)
				ContentsTrace.RunToFile(scene, what, TraceTo + "_" + System.Text.RegularExpressions.Regex.Replace(what, "[^A-Za-z0-9]+", "_"));
			ContentsComparison.BuildAndAssertMatchesOracle(scene, what, RequiredExtent(scene));
		}

		internal static string TraceTo;

		// --- sites ------------------------------------------------------------------------------------------------

		const float kClusterDistance = 1.6f;

		// Neighbourhoods tried around a site's points before falling back to its whole face, then the whole scene.
		static readonly float[] kPointMargins = { 0.5f, 2f, 8f };
		const float kFaceMargin = 1f;

		static List<Site> SitesOf(List<ContentsComparison.Mismatch> mismatches, string label)
		{
			// group by brush face, in the order the sweep met them
			var byFace = new Dictionary<(ContentsSceneNode, int), List<ContentsComparison.Mismatch>>();
			var order  = new List<(ContentsSceneNode, int)>();
			foreach (var mismatch in mismatches)
			{
				var key = (mismatch.brush, mismatch.face);
				if (!byFace.TryGetValue(key, out var list))
				{
					byFace[key] = list = new List<ContentsComparison.Mismatch>();
					order.Add(key);
				}
				list.Add(mismatch);
			}

			var sites = new List<Site>();
			foreach (var key in order)
			{
				var list = byFace[key];
				// single-linkage clusters of the failing points
				var parent = new int[list.Count];
				for (int i = 0; i < parent.Length; i++) parent[i] = i;
				int Root(int i) { while (parent[i] != i) i = parent[i] = parent[parent[i]]; return i; }
				for (int i = 0; i < list.Count; i++)
					for (int j = i + 1; j < list.Count; j++)
						if (math.distance(list[i].point, list[j].point) <= kClusterDistance)
							parent[Root(i)] = Root(j);

				var clusters = new Dictionary<int, Site>();
				for (int i = 0; i < list.Count; i++)
				{
					var root = Root(i);
					if (!clusters.TryGetValue(root, out var site))
					{
						clusters[root] = site = new Site
						{
							brushName = list[i].brush.name,
							face      = list[i].face,
							min       = list[i].point,
							max       = list[i].point
						};
						sites.Add(site);
					}
					site.points.Add(list[i].point);
					site.kinds.Add(ContentsComparison.KindOf(list[i].problem));
					site.messages.Add(list[i].Format(label));
					site.min = math.min(site.min, list[i].point);
					site.max = math.max(site.max, list[i].point);
				}
			}
			return sites;
		}

		static ContentsSceneNode FindBrush(ContentsScene scene, string name)
		{
			foreach (var brush in AllBrushes(scene))
				if (brush.name == name) return brush;
			return null;
		}

		// Tree-space bounds of a brush, from its faces as the sampler clips them. False when nothing is left of it.
		static bool BoundsOf(ContentsSceneNode brush, float extent, out float3 min, out float3 max)
		{
			min = new float3(float.PositiveInfinity);
			max = new float3(float.NegativeInfinity);
			var planes = brush.TreePlanes();
			bool any = false;
			for (int p = 0; p < planes.Length; p++)
			{
				foreach (var v in ContentsFaceSampler.FacePolygon(planes, p, extent))
				{
					min = math.min(min, v);
					max = math.max(max, v);
					any = true;
				}
			}
			return any;
		}

		static bool FaceBounds(ContentsSceneNode brush, int face, float extent, out float3 min, out float3 max)
		{
			min = new float3(float.PositiveInfinity);
			max = new float3(float.NegativeInfinity);
			var planes = brush.TreePlanes();
			if (face < 0 || face >= planes.Length) return false;
			bool any = false;
			foreach (var v in ContentsFaceSampler.FacePolygon(planes, face, extent))
			{
				min = math.min(min, v);
				max = math.max(max, v);
				any = true;
			}
			return any;
		}

		static ContentsScene Localise(ContentsScene scene, float3 min, float3 max, float extent, string keep,
									  Dictionary<ContentsSceneNode, (float3, float3)> bounds)
		{
			var result = new ContentsScene();
			foreach (var root in scene.roots)
			{
				var kept = LocaliseNode(root, min, max, extent, keep, bounds);
				if (kept != null) result.Add(kept);
			}
			return result;
		}

		static ContentsSceneNode LocaliseNode(ContentsSceneNode node, float3 min, float3 max, float extent, string keep,
											  Dictionary<ContentsSceneNode, (float3, float3)> bounds)
		{
			if (node.IsBrush)
			{
				if (node.operation == CSGOperationType.Intersecting || node.name == keep)
					return node;
				if (!bounds.TryGetValue(node, out var box))
				{
					if (!BoundsOf(node, extent, out var bmin, out var bmax))
						return null;
					bounds[node] = box = (bmin, bmax);
				}
				bool overlaps = math.all(box.Item1 <= max) && math.all(box.Item2 >= min);
				return overlaps ? node : null;
			}
			var copy = ContentsSceneNode.Composite(node.operation);
			copy.name = node.name;
			if (node.children != null)
			{
				foreach (var child in node.children)
				{
					var kept = LocaliseNode(child, min, max, extent, keep, bounds);
					if (kept != null) copy.children.Add(kept);
				}
			}
			return copy.children.Count == 0 ? null : copy;
		}

		static ContentsScene Without(ContentsScene scene, HashSet<ContentsSceneNode> remove)
		{
			var result = new ContentsScene();
			foreach (var root in scene.roots)
			{
				var kept = WithoutNode(root, remove);
				if (kept != null) result.Add(kept);
			}
			return result;
		}

		static ContentsSceneNode WithoutNode(ContentsSceneNode node, HashSet<ContentsSceneNode> remove)
		{
			if (node.IsBrush)
				return remove.Contains(node) ? null : node;
			var copy = ContentsSceneNode.Composite(node.operation);
			copy.name = node.name;
			foreach (var child in node.children)
			{
				var kept = WithoutNode(child, remove);
				if (kept != null) copy.children.Add(kept);
			}
			return copy.children.Count == 0 ? null : copy;
		}

		static bool FailsAt(ContentsScene scene, Site site, Stats stats)
		{
			var brush = FindBrush(scene, site.brushName);
			if (brush == null)
				return false;
			stats.runs++;
			try
			{
				using (var harness = ContentsTreeHarness.Build(scene))
				{
					harness.JudgeEachUpdate = false;
					if (!harness.Update())
						return false;
					if (CompactHierarchyManager.TreeUpdate.kExactCSG)
						return FailsExactlyAt(harness, site);
					var oracle = new ContentsOracle(harness.Scene);
					var plane  = oracle.TreePlanesOf(brush)[site.face];
					for (int i = 0; i < site.points.Count; i++)
					{
						var problem = ContentsComparison.MismatchAt(harness, oracle, plane, site.points[i]);
						if (problem != null && ContentsComparison.KindOf(problem) == site.kinds[i])
							return true;
					}
					return false;
				}
			}
			catch (Exception e) when (!IsHarnessRefusal(e))
			{
				RecordException(scene, e, stats);
				return false;
			}
		}

		const float kExactSiteMargin = 0.5f;

		static bool FailsExactlyAt(ContentsTreeHarness harness, Site site)
		{
			var mismatches = ExactJudge.FindMismatches(harness, int.MaxValue, out _);
			var min   = site.min - kExactSiteMargin;
			var max   = site.max + kExactSiteMargin;
			var kinds = new HashSet<string>(site.kinds);
			foreach (var mismatch in mismatches)
			{
				if (mismatch.brush == null || mismatch.brush.name != site.brushName || mismatch.face != site.face)
					continue;
				if (!math.all(mismatch.point >= min) || !math.all(mismatch.point <= max))
					continue;
				if (kinds.Contains(ContentsComparison.KindOf(mismatch.problem)))
					return true;
			}
			return false;
		}

		// The harness refuses to flush while a model in the open scene has pending work. That is a setup problem,
		// not a finding, and swallowing it would make every site look fixed.
		static bool IsHarnessRefusal(Exception e) => e is InvalidOperationException && e.Message.Contains("Chisel model");

		static void RecordException(ContentsScene scene, Exception e, Stats stats)
		{
			var kind = e.GetType().Name + ": " + e.Message;
			if (!stats.exceptionKinds.Add(kind) && stats.exceptions.Count > 20)
				return;
			var text = new StringBuilder();
			text.AppendLine($"EXCEPTION in a CSG run over {BrushCount(scene)} brush(es): {kind}");
			text.AppendLine(e.StackTrace);
			if (BrushCount(scene) <= 12)
				text.AppendLine(ContentsSceneShrinker.ToCSharp(scene, "threw"));
			stats.exceptions.Add(text.ToString());
		}

		static ContentsScene ShrinkSite(ContentsScene start, Site site, Stats stats, Stopwatch clock, double deadline,
										out bool complete)
		{
			complete = false;
			var current    = start;
			var candidates = new List<ContentsSceneNode>();
			foreach (var brush in AllBrushes(current))
				if (brush.name != site.brushName)
					candidates.Add(brush);

			int chunk = math.max(1, candidates.Count / 2);
			while (candidates.Count > 0)
			{
				bool removed = false;
				for (int i = 0; i < candidates.Count;)
				{
					if (clock.Elapsed.TotalSeconds > deadline)
						return current;
					var count     = math.min(chunk, candidates.Count - i);
					var candidate = Without(current, new HashSet<ContentsSceneNode>(candidates.GetRange(i, count)));
					if (FailsAt(candidate, site, stats))
					{
						current = candidate;
						candidates.RemoveRange(i, count);
						removed = true;
					}
					else
						i += count;
				}
				if (chunk > 1)
					chunk = math.max(1, chunk / 2);
				else if (!removed)
					break;
			}

			current  = Simplify(current, site, stats, clock, deadline);
			complete = clock.Elapsed.TotalSeconds <= deadline;
			return current;
		}

		static ContentsScene Simplify(ContentsScene scene, Site site, Stats stats, Stopwatch clock, double deadline)
		{
			bool changed = true;
			while (changed && clock.Elapsed.TotalSeconds <= deadline)
			{
				changed = false;
				for (int index = 0; ; index++)
				{
					var candidate = Dissolve(scene, index, out var exists);
					if (!exists)
						break;
					if (candidate != null && FailsAt(candidate, site, stats))
					{
						scene   = candidate;
						changed = true;
						break;
					}
				}
			}
			return scene;
		}

		static ContentsScene Dissolve(ContentsScene scene, int index, out bool exists)
		{
			int running = 0;
			var state   = 0;   // 0 not reached, 1 dissolved, 2 reached but not dissolvable
			var roots   = DissolveIn(scene.roots, index, ref running, ref state);
			exists = state != 0;
			if (state != 1)
				return null;
			var result = new ContentsScene();
			foreach (var root in roots)
				result.Add(root);
			return result;
		}

		static List<ContentsSceneNode> DissolveIn(List<ContentsSceneNode> nodes, int index, ref int running, ref int state)
		{
			var result = new List<ContentsSceneNode>(nodes.Count);
			foreach (var node in nodes)
			{
				if (node.IsBrush || state != 0)
				{
					result.Add(node);
					continue;
				}
				if (running++ == index)
				{
					var lifted = Lift(node);
					if (lifted != null) { result.AddRange(lifted); state = 1; }
					else                { result.Add(node);        state = 2; }
					continue;
				}
				var copy = ContentsSceneNode.Composite(node.operation);
				copy.name = node.name;
				copy.children.AddRange(DissolveIn(node.children, index, ref running, ref state));
				result.Add(copy);
			}
			return result;
		}

		// What a composite can be replaced by in its parent without changing what the oracle says, or null.
		static List<ContentsSceneNode> Lift(ContentsSceneNode composite)
		{
			var children = composite.children;
			if (children.Count == 0 || !children.TrueForAll(child => child.operation == CSGOperationType.Additive))
				return null;
			if (composite.operation == CSGOperationType.Additive)
				return new List<ContentsSceneNode>(children);
			if (children.Count == 1)
				return new List<ContentsSceneNode> { WithOperation(children[0], composite.operation) };
			return null;
		}

		static ContentsSceneNode WithOperation(ContentsSceneNode node, CSGOperationType operation)
		{
			if (node.IsBrush)
			{
				var brush = ContentsSceneNode.Brush(node.localPlanes, node.contents, operation, node.localToTree, node.name);
				brush.entityID = node.entityID;
				brush.WithMesh(node.localVertices, node.polygonLoops);
				return brush;
			}
			var copy = ContentsSceneNode.Composite(operation, node.children.ToArray());
			copy.name = node.name;
			return copy;
		}

		static string Signature(ContentsScene scene)
		{
			var names = new List<string>();
			foreach (var brush in AllBrushes(scene))
				names.Add(brush.name ?? "?");
			names.Sort(StringComparer.Ordinal);
			return string.Join("|", names);
		}

		/// <summary>
		/// One minimal scene per distinct failing spot, reported through <paramref name="emit"/> as each is found,
		/// so a run that is stopped still leaves everything it had. Each site is localised to the brushes near its
		/// points (growing the margin, then its whole face, then the whole scene - a failure that needs a distant
		/// brush is worth knowing about in itself) and shrunk with its own points still failing. A site an earlier
		/// minimal scene already reproduces is credited to it instead of being shrunk again. Cheapest sites go
		/// first; whatever the budget does not reach is listed, never dropped.
		/// </summary>
		public static List<Finding> HarvestSites(ContentsScene scene, string label, double budgetSeconds, Action<string> emit,
												 int maxWholeSceneShrinks = 2)
		{
			var clock    = Stopwatch.StartNew();
			var stats    = new Stats();
			var findings = new List<Finding>();

			var names = new HashSet<string>();
			foreach (var brush in AllBrushes(scene))
			{
				if (brush.name == null || !names.Add(brush.name))
				{
					emit($"  REFUSED: brush names must be unique and non-null to follow a site through shrinking ('{brush.name}')\n");
					return findings;
				}
			}

			var extent = RequiredExtent(scene);
			var sweep  = Run(scene, extent);
			stats.runs++;
			if (!sweep.updated)
			{
				emit("  THE CSG UPDATE DID NOT RUN - nothing was examined\n");
				return findings;
			}
			var text = new StringBuilder();
			int withMesh = 0;
			foreach (var brush in AllBrushes(scene))
				if (brush.localVertices != null) withMesh++;
			text.AppendLine(sweep.planeMismatches.Count == 0
				? $"  replay: {withMesh} brush(es) rebuilt from their own mesh, every one with exactly the scene's planes"
				: $"  REPLAY IS NOT THE SCENE: {sweep.planeMismatches.Count} of {withMesh} brush(es) rebuilt from their own mesh " +
				  $"registered other planes, e.g. {string.Join("; ", sweep.planeMismatches.GetRange(0, math.min(3, sweep.planeMismatches.Count)))}");
			if (!sweep.Complete)
			{
				text.AppendLine($"  WARNING: {sweep.unexamined.Count} face(s) produced NO samples - not examined, NOT passed:");
				foreach (var face in sweep.unexamined)
					text.AppendLine("      " + face);
			}
			if (!sweep.Disagrees)
			{
				text.AppendLine($"  agrees with the oracle everywhere it was examined ({sweep.checkedSamples} samples, " +
								$"{sweep.skipped} skipped near crossing planes, extent {F(extent)})");
				emit(text.ToString());
				return findings;
			}

			var sites = SitesOf(sweep.mismatches, label);
			var faces = new HashSet<(ContentsSceneNode, int)>();
			foreach (var mismatch in sweep.mismatches)
				faces.Add((mismatch.brush, mismatch.face));
			text.AppendLine($"  {sweep.mismatches.Count} mismatch(es) over {sweep.checkedSamples} samples ({sweep.skipped} skipped " +
							$"near crossing planes, extent {F(extent)}), on {faces.Count} brush face(s), in {sites.Count} site(s); " +
							$"sweep took {clock.Elapsed.TotalSeconds.ToString("0.0", CultureInfo.InvariantCulture)} s");
			emit(text.ToString());

			// cheapest first: the size of each site's closest neighbourhood
			var bounds = new Dictionary<ContentsSceneNode, (float3, float3)>();
			var cost   = new Dictionary<Site, int>();
			foreach (var site in sites)
				cost[site] = BrushCount(Localise(scene, site.min - kPointMargins[0], site.max + kPointMargins[0], extent, site.brushName, bounds));
			var ordered = new List<Site>(sites);
			ordered.Sort((a, b) => cost[a] != cost[b] ? cost[a].CompareTo(cost[b]) : string.CompareOrdinal(a.Key, b.Key));

			var bySignature      = new Dictionary<string, Finding>();
			int wholeSceneShrinks = 0, credited = 0, unreached = 0, lost = 0;
			for (int s = 0; s < ordered.Count; s++)
			{
				var site   = ordered[s];
				var prefix = $"  site {s + 1}/{ordered.Count} {site.Key} ({site.points.Count} point(s), {site.Kind}): ";
				if (clock.Elapsed.TotalSeconds > budgetSeconds)
				{
					emit(prefix + "NOT REACHED - time budget spent\n");
					unreached++;
					continue;
				}

				Finding creditedTo = null;
				foreach (var finding in findings)
				{
					if (FindBrush(finding.minimal, site.brushName) != null && FailsAt(finding.minimal, site, stats))
					{
						creditedTo = finding;
						break;
					}
				}
				if (creditedTo != null)
				{
					creditedTo.sites.Add(site);
					credited++;
					emit(prefix + $"reproduced by FINDING {creditedTo.number}\n");
					continue;
				}

				var siteClock = Stopwatch.StartNew();
				int runsBefore = stats.runs;
				ContentsScene start = null;
				string origin = null;
				foreach (var margin in kPointMargins)
				{
					var local = Localise(scene, site.min - margin, site.max + margin, extent, site.brushName, bounds);
					if (FailsAt(local, site, stats))
					{
						start  = local;
						origin = $"points +{F(margin)}";
						break;
					}
				}
				if (start == null)
				{
					var brush = FindBrush(scene, site.brushName);
					if (FaceBounds(brush, site.face, extent, out var fmin, out var fmax))
					{
						var local = Localise(scene, fmin - kFaceMargin, fmax + kFaceMargin, extent, site.brushName, bounds);
						if (FailsAt(local, site, stats))
						{
							start  = local;
							origin = $"face +{F(kFaceMargin)}";
						}
					}
				}
				if (start == null)
				{
					if (wholeSceneShrinks >= maxWholeSceneShrinks || clock.Elapsed.TotalSeconds > budgetSeconds * 0.5)
					{
						emit(prefix + "needs more than its neighbourhood; NOT SHRUNK (whole-scene shrinks are capped at " +
							 $"{maxWholeSceneShrinks} per scene and the first half of the budget)\n");
						unreached++;
						continue;
					}
					if (!FailsAt(scene, site, stats))
					{
						emit(prefix + "does not fail when the scene is rebuilt - NOT REPRODUCIBLE, not recorded\n");
						lost++;
						continue;
					}
					start  = scene;
					origin = "whole scene";
					wholeSceneShrinks++;
				}

				var deadline = math.min(budgetSeconds, clock.Elapsed.TotalSeconds + 300.0);
				var minimal  = ShrinkSite(start, site, stats, clock, deadline, out var complete);
				// Every step was checked, but check the result once more: this is what the fixture will hold.
				if (!FailsAt(minimal, site, stats))
				{
					emit(prefix + "the shrunk scene no longer fails - NOT RECORDED (a harvester bug)\n");
					lost++;
					continue;
				}

				var signature = Signature(minimal);
				if (bySignature.TryGetValue(signature, out var existing))
				{
					existing.sites.Add(site);
					credited++;
					emit(prefix + $"shrank to the same brushes as FINDING {existing.number}\n");
					continue;
				}

				var minimalExtent = RequiredExtent(minimal);
				var confirmed     = Run(minimal, minimalExtent);
				stats.runs++;
				var result = new Finding
				{
					number       = findings.Count + 1,
					label        = label,
					minimal      = minimal,
					extent       = minimalExtent,
					unexamined   = confirmed.unexamined,
					brushesStart = BrushCount(start),
					brushesAfter = BrushCount(minimal),
					origin       = origin,
					complete     = complete,
					runs         = stats.runs - runsBefore,
					seconds      = siteClock.Elapsed.TotalSeconds
				};
				foreach (var mismatch in confirmed.mismatches)
					result.mismatches.Add(mismatch.Format(label));
				result.sites.Add(site);
				bySignature[signature] = result;
				findings.Add(result);
				emit(prefix + $"-> FINDING {result.number}\n" + FindingText(result));
			}

			var summary = new StringBuilder();
			summary.AppendLine();
			summary.AppendLine($"# SUMMARY {label}: {findings.Count} distinct minimal failing scene(s) from {sites.Count} site(s); " +
							   $"{credited} site(s) credited to an earlier finding, {unreached} not reached or not shrunk, " +
							   $"{lost} not reproducible; {stats.runs} CSG run(s) in " +
							   $"{clock.Elapsed.TotalSeconds.ToString("0.0", CultureInfo.InvariantCulture)} s");
			if (stats.exceptions.Count > 0)
			{
				summary.AppendLine($"# {stats.exceptions.Count} EXCEPTION(S) inside CSG runs (findings in their own right):");
				foreach (var exception in stats.exceptions)
					summary.AppendLine(exception);
			}
			else
				summary.AppendLine("# no exception was thrown by any CSG run");
			emit(summary.ToString());
			return findings;
		}

		/// <summary>One finding as report text, with markers a script can cut the C# out by.</summary>
		public static string FindingText(Finding finding)
		{
			var text = new StringBuilder();
			text.AppendLine();
			text.AppendLine($"=== FINDING {finding.number}: {finding.brushesAfter} brush(es); kind: {finding.sites[0].Kind}; " +
							$"shrunk from {finding.origin} ({finding.brushesStart} brushes) in {finding.runs} runs, " +
							$"{finding.seconds.ToString("0.0", CultureInfo.InvariantCulture)} s" +
							(finding.complete ? "" : "; PARTIAL - the budget stopped the shrink, not minimal"));
			text.AppendLine($"  site: {finding.sites[0].Key}");
			for (int i = 0; i < finding.sites[0].messages.Count && i < 3; i++)
				text.AppendLine("    as swept: " + finding.sites[0].messages[i]);
			text.AppendLine($"  the minimal scene disagrees at {finding.mismatches.Count} point(s) (extent {F(finding.extent)}):");
			for (int i = 0; i < finding.mismatches.Count && i < 12; i++)
				text.AppendLine("    " + finding.mismatches[i]);
			if (finding.mismatches.Count > 12)
				text.AppendLine($"    ... and {finding.mismatches.Count - 12} more");
			if (finding.unexamined.Count > 0)
				text.AppendLine($"  {finding.unexamined.Count} face(s) of the minimal scene produced no samples: {string.Join("; ", finding.unexamined)}");
			text.AppendLine("--- C#");
			text.Append(finding.CSharp);
			text.AppendLine("--- END");
			return text.ToString();
		}
	}
}
