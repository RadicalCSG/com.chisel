using System;
using System.Text;
using NUnit.Framework;

namespace Chisel.Core.Tests
{
	[TestFixture]
	[Category("Probe")]
	[Explicit("writes traces of the exact CSG to Library/ChiselHarvest/traces; run it deliberately")]
	public class ExactCSGProbeTests
	{
		[Test]
		public void NestedSubtractionDepths()
		{
			var report = new StringBuilder();
			foreach (int depth in new[] { 4, 8, 10, 12, 13, 14, 15, 16 })
			{
				using (var harness = ContentsTreeHarness.Build(NestedSubtractions(depth)))
				{
					harness.JudgeEachUpdate = false;
					harness.Update();
					var mismatches = ContentsComparison.FindMismatchRecords(harness, out int judged, out int skipped, out _, 64f, 3);
					var data = ChiselTreeLookup.Value[harness.Tree];
					int maxRows = 0, maxLookupRows = 0, maxDestination = 0, tables = 0;
					for (int i = 0; i < data.routingTableCache.Length; i++)
					{
						var table = data.routingTableCache[i];
						if (!table.IsCreated) continue;
						tables++;
						ref var rows = ref table.Value.routingRows;
						maxRows = Math.Max(maxRows, rows.Length);
						ref var lookups = ref table.Value.routingLookups;
						for (int k = 0; k < lookups.Length; k++)
							maxLookupRows = Math.Max(maxLookupRows, lookups[k].endIndex - lookups[k].startIndex);
						for (int r = 0; r < rows.Length; r++)
						{
							var row = rows[r];
							foreach (var destination in new[] { (int)row.inside, (int)row.aligned, (int)row.selfAligned,
																(int)row.selfReverseAligned, (int)row.reverseAligned, (int)row.outside })
								maxDestination = Math.Max(maxDestination, destination);
						}
					}
					report.Append($"depth {depth}: {mismatches.Count} mismatch(es) over {judged} judged ({skipped} skipped); ")
						  .Append($"{tables} tables, rows max {maxRows}, rows per lookup max {maxLookupRows}, destination max {maxDestination}");
					if (mismatches.Count > 0)
						report.Append("; first: ").Append(mismatches[0].Format("n"));
					report.AppendLine();
				}
			}
			// Also to the log: a script that runs this reads the numbers there, where a passed result carries no message
			UnityEngine.Debug.Log("ExactCSGProbeTests.NestedSubtractionDepths\n" + report);
			Assert.Pass(report.ToString());
		}

		static ContentsScene NestedSubtractions(int depth)
		{
			var block = ContentsSceneNode.Brush(ContentsScene.BoxPlanes(new Unity.Mathematics.float3(-10), new Unity.Mathematics.float3(10)), name: "block");
			ContentsSceneNode inner = null;
			for (int i = depth - 1; i >= 0; i--)
			{
				var offset = i * 0.5f;
				var bite = ContentsSceneNode.Brush(ContentsScene.BoxPlanes(new Unity.Mathematics.float3(-5 + offset, -12, -5 - offset),
																		   new Unity.Mathematics.float3( 5 + offset,  12,  5 - offset)), name: "bite" + i);
				inner = inner == null ? ContentsSceneNode.Composite(CSGOperationType.Subtractive, bite)
									  : ContentsSceneNode.Composite(CSGOperationType.Subtractive, bite, inner);
			}
			return new ContentsScene().Add(block).Add(inner);
		}

		// Does every output triangle lie on a face plane of the brush it is attributed to? Written to
		// Library/ChiselHarvest/traces/attribution_model2f04.txt.
		[Test]
		public void CheckTriangleAttribution()
		{
			var report = new StringBuilder();
			var text = new StringBuilder();
			var scene = Model2F04Scene();
			using (var harness = ContentsTreeHarness.Build(scene))
			{
				harness.Update();
				var brushes = new System.Collections.Generic.List<ContentsSceneNode>();
				foreach (var root in scene.roots) AddBrushes(root, brushes);
				int wrong = 0, total = 0;
				for (int t = 0; t < harness.Triangles.Count; t++)
				{
					var triangle = harness.Triangles[t];
					var node = harness.NodeOf(triangle.brushID);
					total++;
					bool onOwn = node != null && OnAPlaneOf(node, triangle);
					if (onOwn)
						continue;
					wrong++;
					var owners = new StringBuilder();
					foreach (var other in brushes)
						if (OnAPlaneOf(other, triangle)) owners.Append(other.name).Append(' ');
					text.AppendLine($"triangle {t} section {triangle.section} material {triangle.material} attributed to {node?.name ?? "nothing"} " +
									$"({triangle.a}, {triangle.b}, {triangle.c}) lies on a plane of: {owners}");
				}
				// The failing sample point on Extruded Shape #172's bottom, and every triangle of Box (15)
				var point = new Unity.Mathematics.float3(-11.988f, -1f, -13.033f);
				var bottom = new Unity.Mathematics.float4(0, -1, 0, -1);
				for (int t = 0; t < harness.Triangles.Count; t++)
				{
					var triangle = harness.Triangles[t];
					var node = harness.NodeOf(triangle.brushID);
					bool covers = ContentsComparison.Covers(triangle.a, triangle.b, triangle.c, bottom, point, out int facing);
					if (covers || (node != null && node.name.StartsWith("Box (15)")))
						text.AppendLine($"  {(covers ? "COVERS P " : "")}triangle {t} of {node?.name ?? "nothing"} facing {(covers ? facing : 0)}: {triangle.a} {triangle.b} {triangle.c}");
				}
				text.Insert(0, $"{wrong} of {total} triangles are not on a plane of the brush they are attributed to\n");
				report.AppendLine($"{wrong} of {total} misattributed");
			}
			var directory = System.IO.Path.Combine("Library", "ChiselHarvest", "traces");
			System.IO.Directory.CreateDirectory(directory);
			System.IO.File.WriteAllText(System.IO.Path.Combine(directory, "attribution_model2f04.txt"), text.ToString());
			Assert.Pass(report.ToString());
		}

		static void AddBrushes(ContentsSceneNode node, System.Collections.Generic.List<ContentsSceneNode> brushes)
		{
			if (node.IsBrush) { brushes.Add(node); return; }
			foreach (var child in node.children) AddBrushes(child, brushes);
		}

		// SampleSceneHoleTests.HoleOnExtrudedShapeBottom_WithAddBoxAndAddBox15AndAddExtrudedShape2_Model2F04's scene
		static ContentsScene Model2F04Scene()
		{
			return new ContentsScene()
				.Add(ContentsSceneNode.Brush(new Unity.Mathematics.float4[]
				{
					new Unity.Mathematics.float4(-1.74902783E-14f, -1f, 5.9604627E-08f, -1f),
					new Unity.Mathematics.float4(1.74902783E-14f, 1f, -5.9604627E-08f, 0.5f),
					new Unity.Mathematics.float4(-0.8944274f, 0f, 0.4472131f, -5.813776f),
					new Unity.Mathematics.float4(-0.5144956f, 0f, -0.857493f, -1.11474156f),
					new Unity.Mathematics.float4(0.8944272f, 0f, -0.44721362f, -5.81377649f),
					new Unity.Mathematics.float4(0.51449573f, 0f, 0.8574929f, -1.11474061f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new Unity.Mathematics.float4x4(new Unity.Mathematics.float4(-1.00000048f, 0f, 0f, 0f), new Unity.Mathematics.float4(0f, 1f, 0f, 0f), new Unity.Mathematics.float4(0f, 0f, -1.00000048f, 0f), new Unity.Mathematics.float4(-7.500004f, 0f, -16.0000076f, 1f)), name: "Extruded Shape #172"))
				.Add(ContentsSceneNode.Brush(new Unity.Mathematics.float4[]
				{
					new Unity.Mathematics.float4(-1.45915264E-15f, 1f, -2.12873918E-09f, 1.06436939E-08f),
					new Unity.Mathematics.float4(0f, -1f, 0f, -1f),
					new Unity.Mathematics.float4(-1f, 4.4822762E-05f, -1.53269343E-06f, -7.99999142f),
					new Unity.Mathematics.float4(1f, -4.43458557E-05f, 4.72003418E-14f, -7.99999952f),
					new Unity.Mathematics.float4(-1.37090876E-06f, 4.386908E-05f, -1f, -8.999989f),
					new Unity.Mathematics.float4(0f, -4.386902E-05f, 1f, -4.999999f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new Unity.Mathematics.float4x4(new Unity.Mathematics.float4(-1.00000048f, 0f, 0f, 0f), new Unity.Mathematics.float4(0f, 1f, 0f, 0f), new Unity.Mathematics.float4(0f, 0f, -1.00000048f, 0f), new Unity.Mathematics.float4(-1.36423772E-12f, -1f, -25f, 1f)), name: "Box #173"))
				.Add(ContentsSceneNode.Brush(new Unity.Mathematics.float4[]
				{
					new Unity.Mathematics.float4(-1f, 0f, 0f, -1f),
					new Unity.Mathematics.float4(0f, -1f, 0f, -3.5f),
					new Unity.Mathematics.float4(0f, 0f, -1f, -1f),
					new Unity.Mathematics.float4(1f, 0f, 0f, -1f),
					new Unity.Mathematics.float4(0f, 1f, 0f, -6.5f),
					new Unity.Mathematics.float4(0f, 0f, 1f, -1f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new Unity.Mathematics.float4x4(new Unity.Mathematics.float4(1.00000048f, 0f, 0f, 0f), new Unity.Mathematics.float4(0f, 1f, 0f, 0f), new Unity.Mathematics.float4(0f, 0f, 1.00000048f, 0f), new Unity.Mathematics.float4(-7.5f, 2.5f, -18f, 1f)), name: "Box (15) #260"))
				.Add(ContentsSceneNode.Brush(new Unity.Mathematics.float4[]
				{
					new Unity.Mathematics.float4(9.169917E-09f, -1f, 7.488786E-08f, -1f),
					new Unity.Mathematics.float4(-5.73119063E-09f, 1f, -6.667312E-08f, 0.5f),
					new Unity.Mathematics.float4(-0.8944274f, 0f, 0.447213173f, -5.813776f),
					new Unity.Mathematics.float4(-0.5144956f, 0f, -0.857493f, -1.11474156f),
					new Unity.Mathematics.float4(0.8944272f, 0f, -0.4472136f, -5.81377649f),
					new Unity.Mathematics.float4(0.51449573f, 0f, 0.8574929f, -0.5573704f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new Unity.Mathematics.float4x4(new Unity.Mathematics.float4(-1.00000048f, 0f, 0f, 0f), new Unity.Mathematics.float4(0f, 1f, 0f, 0f), new Unity.Mathematics.float4(0f, 0f, -1.00000048f, 0f), new Unity.Mathematics.float4(-7.500004f, 0f, -16.0000076f, 1f)), name: "Extruded Shape (2) #319"));
		}

		// On one of the brush's face planes, and inside the brush: coplanar brushes share planes, so the plane alone says
		// nothing about which of them a triangle belongs to.
		static bool OnAPlaneOf(ContentsSceneNode brush, ContentsTreeHarness.CapturedTriangle triangle)
		{
			var toTree = Unity.Mathematics.math.transpose(Unity.Mathematics.math.inverse(brush.localToTree));
			var center = (triangle.a + triangle.b + triangle.c) / 3.0f;
			foreach (var local in brush.localPlanes)
			{
				var plane = Unity.Mathematics.math.mul(toTree, local);
				var length = Unity.Mathematics.math.length(plane.xyz);
				if (length <= 0) continue;
				plane /= length;
				if (Unity.Mathematics.math.dot(plane.xyz, center) + plane.w > 1e-3f)
					return false;
			}
			foreach (var local in brush.localPlanes)
			{
				var plane = Unity.Mathematics.math.mul(toTree, local);
				var length = Unity.Mathematics.math.length(plane.xyz);
				if (length <= 0) continue;
				plane /= length;
				if (Unity.Mathematics.math.abs(Unity.Mathematics.math.dot(plane.xyz, triangle.a) + plane.w) < 1e-3f &&
					Unity.Mathematics.math.abs(Unity.Mathematics.math.dot(plane.xyz, triangle.b) + plane.w) < 1e-3f &&
					Unity.Mathematics.math.abs(Unity.Mathematics.math.dot(plane.xyz, triangle.c) + plane.w) < 1e-3f)
					return true;
			}
			return false;
		}
	}
}
