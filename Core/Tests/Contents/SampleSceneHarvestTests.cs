using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;

namespace Chisel.Core.Tests
{
	[TestFixture]
	[Category("Harvest")]
	[Explicit("reads a dump written by an editor snippet; run it deliberately")]
	public class SampleSceneHarvestTests
	{
		// Assigned by JsonUtility through reflection, which the compiler cannot see.
#pragma warning disable CS0649
		[Serializable]
		class DumpEntry
		{
			public int     model;
			public int     parent;
			public int     op;
			public string  name;
			public int     kind;        // 0 branch, 1 brush
			public int     contents;
			public float[] matrix;      // 16 floats, float4x4 column by column
			public float[] planes;      // face planes, 4 floats each
			public float[] vertices;    // 3 floats each, the brush mesh's own
			public int[]   loops;       // vertex indices per polygon, -1 after each
			public int     missingMesh;
		}

		[Serializable]
		class Dump
		{
			public string[]    models;
			public DumpEntry[] entries;
		}
#pragma warning restore CS0649

		static string HarvestDirectory => Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Library", "ChiselHarvest"));

		// The sample scene's dump; another scene's is named after that scene.
		const string kSampleScene = "sample_scene";

		// Null, with the reason, when there is no usable dump.
		static Dump Load(string dumpName, out string problem)
		{
			var path = Path.Combine(HarvestDirectory, dumpName + ".json");
			problem = null;
			if (!File.Exists(path))
			{
				problem = "no dump at " + path + " - run the dump snippet first";
				return null;
			}
			var dump = JsonUtility.FromJson<Dump>(File.ReadAllText(path));
			if (dump == null || dump.entries == null || dump.entries.Length == 0)
			{
				problem = "the dump at " + path + " did not parse or has no entries";
				return null;
			}
			return dump;
		}

		static float4x4 Matrix(float[] m)
		{
			return new float4x4(new float4(m[0],  m[1],  m[2],  m[3]),
								new float4(m[4],  m[5],  m[6],  m[7]),
								new float4(m[8],  m[9],  m[10], m[11]),
								new float4(m[12], m[13], m[14], m[15]));
		}

		static float3[] Vertices(float[] v)
		{
			var result = new float3[v.Length / 3];
			for (int i = 0; i < result.Length; i++)
				result[i] = new float3(v[i * 3], v[i * 3 + 1], v[i * 3 + 2]);
			return result;
		}

		static float4[] Planes(float[] p)
		{
			var result = new float4[p.Length / 4];
			for (int i = 0; i < result.Length; i++)
				result[i] = new float4(p[i * 4], p[i * 4 + 1], p[i * 4 + 2], p[i * 4 + 3]);
			return result;
		}

		static ContentsScene SceneOf(Dump dump, int modelIndex, StringBuilder skipped, out int brushes)
		{
			var scene = new ContentsScene();
			var nodes = new Dictionary<int, ContentsSceneNode>();
			brushes = 0;
			for (int i = 0; i < dump.entries.Length; i++)
			{
				var e = dump.entries[i];
				if (e.model != modelIndex)
					continue;

				ContentsSceneNode node;
				if (e.kind == 1)
				{
					if (e.missingMesh != 0 || e.planes == null || e.planes.Length < 16)
					{
						skipped.AppendLine($"    brush '{e.name}' (entry {i}) skipped: " +
										   (e.missingMesh != 0 ? "no brush mesh" : $"{(e.planes?.Length ?? 0) / 4} planes"));
						continue;
					}
					node = ContentsSceneNode.Brush(Planes(e.planes), e.contents, (CSGOperationType)e.op,
												   Matrix(e.matrix), $"{e.name} #{i}");
					// its own mesh too, so the harness derives the very planes the scene's CSG was given
					if (e.vertices != null && e.vertices.Length >= 12 && e.loops != null && e.loops.Length > 0)
						node.WithMesh(Vertices(e.vertices), e.loops);
					brushes++;
				}
				else
				{
					node = ContentsSceneNode.Composite((CSGOperationType)e.op);
					node.name = $"{e.name} #{i}";
				}

				nodes[i] = node;
				if (e.parent < 0)
					scene.Add(node);
				else if (nodes.TryGetValue(e.parent, out var parent))
					parent.children.Add(node);
				else
					skipped.AppendLine($"    '{e.name}' (entry {i}) skipped: its parent {e.parent} was skipped");
			}
			return scene;
		}

		// Per model. Sites are shrunk cheapest first, and whatever the budget does not reach is listed in the report
		// as NOT REACHED rather than dropped, so a second run can be aimed at it.
		const double kBudgetSeconds = 600;

		[TestCase(0)]
		[TestCase(1)]
		[TestCase(2)]
		[TestCase(3)]
		public void HarvestModel(int modelIndex)
		{
			var result = Harvest(modelIndex, kBudgetSeconds);
			if (result.StartsWith(kInconclusive, StringComparison.Ordinal))
				Assert.Inconclusive(result);
			// A harvester, not an assertion: it passes with a count. The report file is where the cases are.
			Assert.Pass(result);
		}

		const string kInconclusive = "INCONCLUSIVE: ";

		/// <summary>One model of the dump as a scene description, or null with the reason.</summary>
		public static ContentsScene LoadModel(int modelIndex, out string modelName, out string problem)
		{
			return LoadModel(kSampleScene, modelIndex, out modelName, out problem);
		}

		/// <summary>One model of a scene's dump as a scene description, or null with the reason.</summary>
		public static ContentsScene LoadModel(string dumpName, int modelIndex, out string modelName, out string problem)
		{
			modelName = null;
			var dump = Load(dumpName, out problem);
			if (dump == null)
				return null;
			return LoadModel(dump, modelIndex, out modelName, out problem);
		}

		// The same from a dump already read, so a caller that goes through many models parses the file once
		static ContentsScene LoadModel(Dump dump, int modelIndex, out string modelName, out string problem)
		{
			modelName = null;
			problem   = null;
			if (modelIndex >= dump.models.Length)
			{
				problem = $"the dump has {dump.models.Length} model(s)";
				return null;
			}
			modelName = dump.models[modelIndex];
			var scene = SceneOf(dump, modelIndex, new StringBuilder(), out var brushCount);
			if (brushCount == 0)
			{
				problem = $"model '{modelName}' has no brushes in the dump - UNBUILT";
				return null;
			}
			return scene;
		}

		/// <summary>
		/// Judges a LIVE model's meshes - the scene's own output, built from the generators' own vertices by the editor's
		/// own update path - against the oracle for the dumped description of the same model. The harness rebuilds brushes
		/// from their planes, so a failure that needs the real vertices, or the editor's incremental updates, shows here
		/// and nowhere else. `triangles` is 9 floats per triangle in the model's space. Report in
		/// Library/ChiselHarvest/live_check_&lt;model&gt;.txt.
		/// </summary>
		public static string CheckLiveModel(int modelIndex, float[] triangles)
		{
			var scene = LoadModel(modelIndex, out var modelName, out var problem);
			if (scene == null)
				return kInconclusive + problem;
			var report = LiveOutputCheck.Run(scene, triangles, $"live {modelName}");
			Directory.CreateDirectory(HarvestDirectory);
			var outPath = Path.Combine(HarvestDirectory, $"live_check_{modelIndex}.txt");
			File.WriteAllText(outPath, report);
			var firstLine = report.Split('\n')[0];
			return $"{firstLine} -> {outPath}";
		}

		/// <summary>
		/// Compares a live model's triangles with what the harness builds from the dump of the same model: the same CSG, from
		/// the same planes and transforms. An editor snippet writes the live triangles to
		/// Library/ChiselHarvest/&lt;dumpName&gt;_live_&lt;model&gt;.bin, 9 little-endian floats per triangle in the model's space,
		/// as its meshes wind them. Triangles are compared bit for bit, so any difference is the editor's own path making
		/// something the planes do not: its brushes, its incremental updates, or what happens to the meshes after the CSG.
		/// Report in &lt;dumpName&gt;_live_&lt;model&gt;.txt.
		/// </summary>
		public static string CompareLiveModel(string dumpName, int modelIndex)
		{
			var scene = LoadModel(dumpName, modelIndex, out var modelName, out var problem);
			if (scene == null)
				return kInconclusive + problem;
			using (var harness = ContentsTreeHarness.Build(scene))
			{
				harness.JudgeEachUpdate = false;
				if (!harness.Update())
					return kInconclusive + "the harness's CSG update did not run";
				return CompareWithLive(harness, scene, dumpName, modelIndex, modelName);
			}
		}

		// CompareLiveModel's comparison, on a harness whose update has run
		static string CompareWithLive(ContentsTreeHarness harness, ContentsScene scene, string dumpName, int modelIndex, string modelName)
		{
			var livePath = Path.Combine(HarvestDirectory, $"{dumpName}_live_{modelIndex}.bin");
			if (!File.Exists(livePath))
				return kInconclusive + "no live triangles at " + livePath;
			var bytes = File.ReadAllBytes(livePath);
			var live  = new float[bytes.Length / 4];
			Buffer.BlockCopy(bytes, 0, live, 0, live.Length * 4);

			var liveCounts = new Dictionary<TriangleKey, int>();
			for (int t = 0; t + 8 < live.Length; t += 9)
			{
				var key = TriangleKey.Of(new float3(live[t], live[t + 1], live[t + 2]), new float3(live[t + 3], live[t + 4], live[t + 5]),
										 new float3(live[t + 6], live[t + 7], live[t + 8]));
				liveCounts.TryGetValue(key, out var count);
				liveCounts[key] = count + 1;
			}

			var report = new StringBuilder();
			{
				var built    = new Dictionary<TriangleKey, int>();
				var brushOf  = new Dictionary<TriangleKey, string>();
				// The harness's triangles too, for an offline comparison that does not depend on how a face is cut into
				// triangles: 9 floats, then the brush's index in the dump's order, per triangle.
				var brushIndex = new Dictionary<ContentsSceneNode, int>();
				foreach (var node in ContentsHarvest.AllBrushes(scene))
					brushIndex[node] = brushIndex.Count;
				var harnessData = new List<float>(harness.Triangles.Count * 10);
				foreach (var triangle in harness.Triangles)
				{
					var key = TriangleKey.Of(triangle.a, triangle.b, triangle.c);
					built.TryGetValue(key, out var count);
					built[key] = count + 1;
					var node = harness.NodeOf(triangle.brushID);
					brushOf[key] = node?.name ?? "?";
					harnessData.Add(triangle.a.x); harnessData.Add(triangle.a.y); harnessData.Add(triangle.a.z);
					harnessData.Add(triangle.b.x); harnessData.Add(triangle.b.y); harnessData.Add(triangle.b.z);
					harnessData.Add(triangle.c.x); harnessData.Add(triangle.c.y); harnessData.Add(triangle.c.z);
					harnessData.Add(node != null && brushIndex.TryGetValue(node, out var index) ? index : -1);
				}
				var harnessBytes = new byte[harnessData.Count * 4];
				Buffer.BlockCopy(harnessData.ToArray(), 0, harnessBytes, 0, harnessBytes.Length);
				Directory.CreateDirectory(HarvestDirectory);
				File.WriteAllBytes(Path.Combine(HarvestDirectory, $"{dumpName}_harness_{modelIndex}.bin"), harnessBytes);

				var missing = new List<(TriangleKey key, int count)>();
				var extra   = new List<(TriangleKey key, int count)>();
				foreach (var pair in built)
				{
					liveCounts.TryGetValue(pair.Key, out var liveCount);
					if (liveCount < pair.Value)
						missing.Add((pair.Key, pair.Value - liveCount));
				}
				foreach (var pair in liveCounts)
				{
					built.TryGetValue(pair.Key, out var builtCount);
					if (builtCount < pair.Value)
						extra.Add((pair.Key, pair.Value - builtCount));
				}
				double missingArea = 0, extraArea = 0;
				foreach (var (key, count) in missing) missingArea += key.Area * count;
				foreach (var (key, count) in extra)   extraArea   += key.Area * count;
				missing.Sort((x, y) => y.key.Area.CompareTo(x.key.Area));
				extra.Sort((x, y) => y.key.Area.CompareTo(x.key.Area));

				report.AppendLine($"# {dumpName} / {modelName}: live {live.Length / 9} triangles, harness {harness.Triangles.Count}; " +
								  $"{missing.Count} harness triangle(s) missing from the live meshes (area {missingArea:0.####}), " +
								  $"{extra.Count} live triangle(s) the harness does not make (area {extraArea:0.####}); " +
								  (harness.PlaneMismatches.Count == 0 ? "every brush replayed with exactly the scene's planes"
																	  : $"{harness.PlaneMismatches.Count} brush(es) replayed with OTHER planes, e.g. {harness.PlaneMismatches[0]}"));
				report.AppendLine("## missing from the live meshes, largest first:");
				for (int i = 0; i < missing.Count && i < 40; i++)
					report.AppendLine($"  {brushOf[missing[i].key]}: {missing[i].key} area {missing[i].key.Area:0.######}" +
									  (missing[i].count > 1 ? $" x{missing[i].count}" : ""));
				report.AppendLine("## in the live meshes only, largest first:");
				for (int i = 0; i < extra.Count && i < 40; i++)
					report.AppendLine($"  {extra[i].key} area {extra[i].key.Area:0.######}" + (extra[i].count > 1 ? $" x{extra[i].count}" : ""));
			}
			Directory.CreateDirectory(HarvestDirectory);
			var outPath = Path.Combine(HarvestDirectory, $"{dumpName}_live_{modelIndex}.txt");
			File.WriteAllText(outPath, report.ToString());
			return report.ToString().Split('\n')[0] + " -> " + outPath;
		}

		const char Tab = (char)9;
		static string OneLine(string text) => text.Replace(Tab, ' ').Replace((char)13, ' ').Replace((char)10, ' ');

		/// <summary>
		/// What the map-import series asks of each dumped model, for many models in one call: ONE replay per model, compared
		/// with the live triangles (<see cref="CompareLiveModel"/>, same report file) and swept by the exact judge (what
		/// <see cref="Harvest"/> does with a budget of 0, without listing the failing sites - a model the judge disagrees
		/// with gets a Harvest of its own). A call per model cost two snippet compiles and two replays; for a map's hundred
		/// small brush entities that was most of its checking time. The dump is read once. `modelIndices` is "0,1,2".
		/// One tab-separated line per model: index, name, live comparison, judge, seconds.
		/// </summary>
		public static string CheckModels(string dumpName, string modelIndices)
		{
			var dump = Load(dumpName, out var problem);
			if (dump == null)
				return kInconclusive + problem;
			var lines = new StringBuilder();
			var progressPath = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Library", "ChiselHarvest", $"check_{dumpName}.txt"));
			foreach (var part in modelIndices.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries))
			{
				var clock      = System.Diagnostics.Stopwatch.StartNew();
				var modelIndex = int.Parse(part.Trim());
				string live, judge;
				var scene = LoadModel(dump, modelIndex, out var modelName, out var loadProblem);
				if (scene == null)
				{
					live = judge = kInconclusive + loadProblem;
				} else
				{
					try
					{
						using var harness = ContentsTreeHarness.Build(scene);
						harness.JudgeEachUpdate = false;    // the sweep below reports every disagreement instead of throwing on the first
						if (!harness.Update())
						{
							live = judge = kInconclusive + "the harness's CSG update did not run";
						} else
						{
							live = CompareWithLive(harness, scene, dumpName, modelIndex, modelName);
							var mismatches = ContentsComparison.FindMismatchRecords(harness, out var checkedSamples, out var skipped,
																					 out var unexamined, ContentsHarvest.RequiredExtent(scene), 8,
																					 budgeted: true);
							judge = $"{mismatches.Count} mismatch(es) over {checkedSamples} judged ({skipped} skipped)" +
									(unexamined.Count > 0 ? $"; {unexamined.Count} face(s) NOT examined, first: {unexamined[0]}" : "") +
									(harness.PlaneMismatches.Count > 0 ? $"; REPLAY IS NOT THE SCENE: {harness.PlaneMismatches.Count} brush(es) with other planes" : "") +
									(ExactJudge.LastDroppedByTheWeld > 0 ? $"; {ExactJudge.LastDroppedByTheWeld} exact triangle(s) the weld drops" : "") +
									(ExactJudge.LastFlippedByTheWeld > 0 ? $"; {ExactJudge.LastFlippedByTheWeld} needle(s) the weld flips away" : "") +
									(ExactJudge.LastSplitByTheWeld > 0 ? $"; {ExactJudge.LastSplitByTheWeld} needle(s) split into another brush's triangle" : "") +
									(ExactJudge.LastPairedByTheWeld > 0 ? $"; {ExactJudge.LastPairedByTheWeld} pair(s) of facing needles dropped" : "") +
									(ExactJudge.LastCancelledByTheWeld > 0 ? $"; {ExactJudge.LastCancelledByTheWeld} pair(s) of opposite triangles cancelled" : "") +
									(mismatches.Count > 0 ? "; first: " + mismatches[0].Format("exact") : "");
						}
					}
					catch (Exception exception)
					{
						live = judge = "THREW " + exception.GetType().Name + ": " + exception.Message;
					}
				}
				var line = new StringBuilder();
				line.Append(modelIndex).Append(Tab).Append(modelName).Append(Tab).Append(OneLine(live))
					.Append(Tab).Append(OneLine(judge)).Append(Tab)
					.Append(clock.Elapsed.TotalSeconds.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture)).Append('\n');
				lines.Append(line);
				File.AppendAllText(progressPath, line.ToString());
			}
			return lines.ToString();
		}

		public static string ProfileModel(string dumpName, int modelIndex)
		{
			var path = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Library", "ChiselHarvest",
													 $"profile_{dumpName}_{modelIndex}.txt"));
			var clock = System.Diagnostics.Stopwatch.StartNew();
			using var writer = new StreamWriter(path, false) { AutoFlush = true };
			void Note(string text) => writer.WriteLine(clock.Elapsed.TotalSeconds.ToString("0.000", System.Globalization.CultureInfo.InvariantCulture) + " " + text);

			var dump = Load(dumpName, out var problem);
			if (dump == null)
			{
				Note("no dump: " + problem);
				return kInconclusive + problem;
			}
			var scene = LoadModel(dump, modelIndex, out var modelName, out var loadProblem);
			if (scene == null)
			{
				Note("no model: " + loadProblem);
				return kInconclusive + loadProblem;
			}
			Note($"loaded {modelName}");
			using var harness = ContentsTreeHarness.Build(scene);
			harness.JudgeEachUpdate = false;
			Note("harness built");
			if (!harness.Update())
			{
				Note("the CSG update did not run");
				return kInconclusive + "the harness's CSG update did not run";
			}
			Note("CSG update done");
			var live = CompareWithLive(harness, scene, dumpName, modelIndex, modelName);
			Note("live compared: " + OneLine(live));
			ExactJudge.progress = Note;
			try
			{
				var mismatches = ContentsComparison.FindMismatchRecords(harness, out var checkedSamples, out var skipped,
																		 out var unexamined, ContentsHarvest.RequiredExtent(scene), 8, budgeted: true);
				Note($"judged: {mismatches.Count} mismatch(es) over {checkedSamples}; {unexamined.Count} plane(s) NOT judged" + (unexamined.Count > 0 ? ", first: " + unexamined[0] : ""));
			}
			finally
			{
				ExactJudge.progress = null;
			}
			return path;
		}

		// A triangle as its vertices' float bits, turned to the smallest of its three rotations: the same triangle wound
		// the same way always gives the same key, and the other winding a different one.
		readonly struct TriangleKey : IEquatable<TriangleKey>
		{
			readonly int3 a, b, c;

			TriangleKey(int3 a, int3 b, int3 c) { this.a = a; this.b = b; this.c = c; }

			public static TriangleKey Of(float3 a, float3 b, float3 c)
			{
				var ia = math.asint(a); var ib = math.asint(b); var ic = math.asint(c);
				var best = new TriangleKey(ia, ib, ic);
				var r1   = new TriangleKey(ib, ic, ia);
				var r2   = new TriangleKey(ic, ia, ib);
				if (r1.Less(best)) best = r1;
				if (r2.Less(best)) best = r2;
				return best;
			}

			bool Less(TriangleKey other)
			{
				int order = Order(a, other.a);
				if (order == 0) order = Order(b, other.b);
				if (order == 0) order = Order(c, other.c);
				return order < 0;
			}

			static int Order(int3 p, int3 q) => p.x != q.x ? p.x.CompareTo(q.x) : (p.y != q.y ? p.y.CompareTo(q.y) : p.z.CompareTo(q.z));

			public double Area
			{
				get
				{
					var pa = (double3)math.asfloat(a); var pb = (double3)math.asfloat(b); var pc = (double3)math.asfloat(c);
					return math.length(math.cross(pb - pa, pc - pa)) * 0.5;
				}
			}

			public bool Equals(TriangleKey other) => a.Equals(other.a) && b.Equals(other.b) && c.Equals(other.c);
			public override bool Equals(object obj) => obj is TriangleKey other && Equals(other);
			public override int GetHashCode() => (a.GetHashCode() * 31 + b.GetHashCode()) * 31 + c.GetHashCode();

			public override string ToString()
			{
				string P(int3 p) { var f = math.asfloat(p); return $"({f.x.ToString("R", System.Globalization.CultureInfo.InvariantCulture)}, " +
					$"{f.y.ToString("R", System.Globalization.CultureInfo.InvariantCulture)}, {f.z.ToString("R", System.Globalization.CultureInfo.InvariantCulture)})"; }
				return $"{P(a)} {P(b)} {P(c)}";
			}
		}

		/// <summary>
		/// The harvest of one model as a plain call, so an editor snippet can run it without the test runner. A test
		/// run first saves every modified scene - or asks to, for an untitled one - and then reloads the open scene
		/// from disk, and in the shared editor that open scene is somebody's unsaved work. The harness itself needs
		/// no particular scene: it refuses to flush while a model in the open scene has pending work, and is
		/// otherwise blind to it. Returns a one-line summary; the cases are in the report file.
		/// </summary>
		public static string Harvest(int modelIndex, double budgetSeconds)
		{
			return Harvest(kSampleScene, modelIndex, budgetSeconds);
		}

		/// <summary>
		/// The same for any scene an editor snippet dumped to Library/ChiselHarvest/&lt;dumpName&gt;.json; the report goes
		/// to &lt;dumpName&gt;_harvest_&lt;model&gt;.txt beside it. A budget of 0 only sweeps: every failing spot is listed,
		/// none is shrunk.
		/// </summary>
		public static string Harvest(string dumpName, int modelIndex, double budgetSeconds)
		{
			var dump = Load(dumpName, out var problem);
			if (dump == null)
				return kInconclusive + problem;
			if (modelIndex >= dump.models.Length)
				return kInconclusive + $"the dump has {dump.models.Length} model(s)";

			var modelName = dump.models[modelIndex];
			var skipped   = new StringBuilder();
			var scene     = SceneOf(dump, modelIndex, skipped, out var brushCount);
			if (brushCount == 0)
				return kInconclusive + $"model '{modelName}' has no brushes in the dump - UNBUILT, not clean";

			// The report is written as the harvest goes, so a run that is stopped or times out still leaves every
			// finding it made.
			Directory.CreateDirectory(HarvestDirectory);
			var outPath = Path.Combine(HarvestDirectory, $"{dumpName}_harvest_{modelIndex}.txt");
			var label   = $"{dumpName} / {modelName}";
			var header  = new StringBuilder();
			header.AppendLine($"# {label}: {brushCount} brush(es), budget {budgetSeconds} s");
			if (skipped.Length > 0)
			{
				header.AppendLine("  NOT replayed (so not examined):");
				header.Append(skipped);
			}
			File.WriteAllText(outPath, header.ToString());

			// One minimal scene per failing spot, not one per model: a first run shrank on "any disagreement" and
			// returned a single 166-brush case for a model with 415 mismatches.
			var findings = ContentsHarvest.HarvestSites(scene, label, budgetSeconds, chunk => File.AppendAllText(outPath, chunk));
			return $"{modelName}: {brushCount} brushes, {findings.Count} distinct minimal failing scene(s); report at {outPath}";
		}
	}
}
