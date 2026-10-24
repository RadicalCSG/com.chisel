using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Unity.Mathematics;

namespace Chisel.Core.Tests
{
	public static class LiveOutputCheck
	{
		public static string Run(ContentsScene scene, float[] triangleData, string what, int examplesPerFace = 3)
		{
			var triangles = new List<float3x3>(triangleData.Length / 9);
			for (int t = 0; t + 8 < triangleData.Length; t += 9)
				triangles.Add(new float3x3(new float3(triangleData[t], triangleData[t + 1], triangleData[t + 2]),
										   new float3(triangleData[t + 3], triangleData[t + 4], triangleData[t + 5]),
										   new float3(triangleData[t + 6], triangleData[t + 7], triangleData[t + 8])));

			var oracle  = new ContentsOracle(scene);
			var brushes = oracle.Brushes;
			var extent  = ContentsHarvest.RequiredExtent(scene);
			int checkedSamples = 0, skipped = 0, holes = 0, extras = 0, unexamined = 0;
			var perFace = new StringBuilder();

			var allBounds = new List<(float3 min, float3 max)>(brushes.Count);
			foreach (var other in brushes)
				allBounds.Add(ContentsFaceSampler.BrushBounds(oracle.TreePlanesOf(other), extent));

			for (int b = 0; b < brushes.Count; b++)
			{
				var planes = oracle.TreePlanesOf(brushes[b]);
				var otherPlanes = new List<float4[]>();
				var otherBounds = new List<(float3 min, float3 max)>();
				for (int o = 0; o < brushes.Count; o++)
				{
					if (o == b) continue;
					otherPlanes.Add(oracle.TreePlanesOf(brushes[o]));
					otherBounds.Add(allBounds[o]);
				}

				for (int p = 0; p < planes.Length; p++)
				{
					var plane   = planes[p];
					var samples = ContentsFaceSampler.FaceSamples(planes, p, otherPlanes, otherBounds, extent,
																  ContentsComparison.kSampleInset, ContentsComparison.kSampleSpacing,
																  ContentsComparison.kCellClearance);
					if (samples.Count == 0) { unexamined++; continue; }

					// only the triangles lying on this plane can cover a sample on it
					var onPlane = new List<float3x3>();
					foreach (var triangle in triangles)
						if (ContentsComparison.OnPlane(triangle.c0, plane) && ContentsComparison.OnPlane(triangle.c1, plane) &&
							ContentsComparison.OnPlane(triangle.c2, plane))
							onPlane.Add(triangle);

					int faceHoles = 0, faceExtras = 0;
					var examples = new StringBuilder();
					foreach (var point in samples)
					{
						if (ContentsFaceSampler.NearACrossingPlane(otherPlanes, plane, point, ContentsComparison.kSampleCrossingBand)) { skipped++; continue; }
						checkedSamples++;
						ContentsComparison.ExpectedFacings(oracle, plane, point, out var expectAlong, out var expectAgainst);
						bool foundAlong = false, foundAgainst = false;
						foreach (var triangle in onPlane)
						{
							if (!ContentsComparison.Covers(triangle.c0, triangle.c1, triangle.c2, plane, point, out var facing))
								continue;
							if (facing > 0) foundAlong = true; else foundAgainst = true;
						}
						var hole  = (expectAlong && !foundAlong) || (expectAgainst && !foundAgainst);
						var extra = (!expectAlong && foundAlong) || (!expectAgainst && foundAgainst);
						if (hole)  faceHoles++;
						if (extra) faceExtras++;
						if ((hole || extra) && faceHoles + faceExtras <= examplesPerFace)
							examples.Append($" ({F(point.x)}, {F(point.y)}, {F(point.z)}) {(hole ? "HOLE" : "EXTRA")}" +
											$" expect{(expectAlong ? "+" : "")}{(expectAgainst ? "-" : "")} found{(foundAlong ? "+" : "")}{(foundAgainst ? "-" : "")};");
					}
					holes  += faceHoles;
					extras += faceExtras;
					if (faceHoles + faceExtras > 0)
						perFace.AppendLine($"  {brushes[b].name} face {p}: {faceHoles} hole(s), {faceExtras} extra(s) of {samples.Count} samples:{examples}");
				}
			}

			var text = new StringBuilder();
			text.AppendLine($"# {what}: {holes} hole sample(s), {extras} extra sample(s) over {checkedSamples} checked, {skipped} skipped near crossing planes, " +
							$"{unexamined} face(s) unexamined; {triangles.Count} live triangles, extent {F(extent)}");
			text.Append(perFace);
			return text.ToString();
		}

		static string F(float value) => value.ToString("0.###", CultureInfo.InvariantCulture);
	}
}
