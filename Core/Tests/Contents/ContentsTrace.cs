using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Unity.Mathematics;

namespace Chisel.Core.Tests
{
	public static class ContentsTrace
	{
		// Library/ChiselHarvest/traces/<name>.txt, so an editor snippet can ask for a trace without file access of its own.
		public static string RunToFile(ContentsScene scene, string what, string name)
		{
			var directory = System.IO.Path.GetFullPath(System.IO.Path.Combine(UnityEngine.Application.dataPath, "..", "Library", "ChiselHarvest", "traces"));
			System.IO.Directory.CreateDirectory(directory);
			var path = System.IO.Path.Combine(directory, name + ".txt");
			var text = Run(scene, what);
			System.IO.File.WriteAllText(path, text);
			return $"{text.Length} chars -> {path}";
		}

		static bool s_LiveBurst, s_LiveJobCompiler;

		public static string BeginLive()
		{
			s_LiveBurst       = Unity.Burst.BurstCompiler.Options.EnableBurstCompilation;
			s_LiveJobCompiler = Unity.Jobs.LowLevel.Unsafe.JobsUtility.JobCompilerEnabled;
			Unity.Burst.BurstCompiler.Options.EnableBurstCompilation = false;
			Unity.Jobs.LowLevel.Unsafe.JobsUtility.JobCompilerEnabled = false;
			CSGTrace.Sink = new StringBuilder();
			return "tracing";
		}

		public static string EndLive(string name)
		{
			var sink = CSGTrace.Sink;
			CSGTrace.Sink = null;
			Unity.Jobs.LowLevel.Unsafe.JobsUtility.JobCompilerEnabled = s_LiveJobCompiler;
			Unity.Burst.BurstCompiler.Options.EnableBurstCompilation = s_LiveBurst;
			if (sink == null)
				return "no trace was running";
			var directory = System.IO.Path.GetFullPath(System.IO.Path.Combine(UnityEngine.Application.dataPath, "..", "Library", "ChiselHarvest", "traces"));
			System.IO.Directory.CreateDirectory(directory);
			var path = System.IO.Path.Combine(directory, name + ".txt");
			System.IO.File.WriteAllText(path, sink.ToString());
			return $"{sink.Length} chars -> {path}";
		}

		public static string Run(ContentsScene scene, string what)
		{
			var text   = new StringBuilder();
			var brushes = new List<ContentsSceneNode>(ContentsHarvest.AllBrushes(scene));
			text.AppendLine($"# {what}: {brushes.Count} brush(es) - " + ContentsComparison.DescribeScene(scene));
			for (int b = 0; b < brushes.Count; b++)
				text.AppendLine($"#   b{b} = {brushes[b].name} {brushes[b].operation}");

			var burst = Unity.Burst.BurstCompiler.Options.EnableBurstCompilation;
			var jobCompiler = Unity.Jobs.LowLevel.Unsafe.JobsUtility.JobCompilerEnabled;
			var sink  = new StringBuilder();
			try
			{
				Unity.Burst.BurstCompiler.Options.EnableBurstCompilation = false;
				Unity.Jobs.LowLevel.Unsafe.JobsUtility.JobCompilerEnabled = false;
				CSGTrace.Sink = sink;
				using (var harness = ContentsTreeHarness.Build(scene))
				{
					var updated = harness.Update();
					CSGTrace.Sink = null;
					text.Append(sink);
					text.AppendLine($"# update: {harness.LastUpdateReport}");
					if (!updated)
						return text.ToString();

					var drawn = new Dictionary<ContentsSceneNode, (int count, double area)>();
					foreach (var triangle in harness.Triangles)
					{
						var node = harness.NodeOf(triangle.brushID);
						if (node == null) continue;
						drawn.TryGetValue(node, out var sum);
						drawn[node] = (sum.count + 1, sum.area + (0.5 * math.length(math.cross((double3)triangle.b - triangle.a, (double3)triangle.c - triangle.a))));
					}
					for (int b = 0; b < brushes.Count; b++)
					{
						drawn.TryGetValue(brushes[b], out var sum);
						text.AppendLine($"# drawn b{b}: {sum.count} triangle(s), area {sum.area.ToString("0.####", CultureInfo.InvariantCulture)}");
					}

					var mismatches = ContentsComparison.FindMismatchRecords(harness, out var checkedSamples, out _, out _,
																			ContentsHarvest.RequiredExtent(scene), int.MaxValue);
					var byFace = new Dictionary<string, int>();
					var first  = new Dictionary<string, string>();
					foreach (var mismatch in mismatches)
					{
						var key = $"b{brushes.IndexOf(mismatch.brush)} face {mismatch.face}: {ContentsComparison.KindOf(mismatch.problem)}";
						byFace.TryGetValue(key, out var count);
						byFace[key] = count + 1;
						if (!first.ContainsKey(key))
							first[key] = mismatch.Format(what);
					}
					text.AppendLine($"# oracle: {mismatches.Count} mismatch(es) over {checkedSamples} samples");
					foreach (var pair in byFace)
						text.AppendLine($"#   {pair.Key} x{pair.Value}   e.g. {first[pair.Key]}");
				}
			}
			finally
			{
				CSGTrace.Sink = null;
				Unity.Jobs.LowLevel.Unsafe.JobsUtility.JobCompilerEnabled = jobCompiler;
				Unity.Burst.BurstCompiler.Options.EnableBurstCompilation = burst;
			}
			return text.ToString();
		}
	}
}
