using System;
using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;

namespace Chisel.Core.Tests
{
	// Which faces the plane merge joins into one group (ExactPlaneMerge.Unite), and how many pairs it compares to find them
	[TestFixture]
	public class ExactPlaneMergeUniteTests
	{
		const long kOffsetStep = 1L << 20;     // one offset step of a rounded plane's w (ExactPlane.Quantize)

		static ExactPlaneMerge.Face Face(int brush, long a, long b, long c, long offset, double3 min, double3 max)
		{
			return new ExactPlaneMerge.Face
			{
				brush = brush,
				key   = new ExactPlane(a, b, c, Int128.FromLong(offset * kOffsetStep)),
				min   = min,
				max   = max
			};
		}

		// The faces in the order the merge gives them: by the minimum x of their box
		static NativeList<ExactPlaneMerge.Face> Sorted(ExactPlaneMerge.Face[] faces)
		{
			var sorted = (ExactPlaneMerge.Face[])faces.Clone();
			Array.Sort(sorted, (x, y) => x.min.x.CompareTo(y.min.x));
			var list = new NativeList<ExactPlaneMerge.Face>(sorted.Length, Allocator.Temp);
			foreach (var face in sorted)
				list.Add(face);
			return list;
		}

		static int Find(int[] parent, int i)
		{
			while (parent[i] != i)
				i = parent[i];
			return i;
		}

		// Every pair compared: the groups Unite has to find
		static int[] BruteForceGroups(NativeList<ExactPlaneMerge.Face> faces)
		{
			const double touch = ExactPlaneMerge.kTouch;
			var parent = new int[faces.Length];
			for (int i = 0; i < parent.Length; i++)
				parent[i] = i;
			for (int i = 0; i < faces.Length; i++)
			{
				for (int j = i + 1; j < faces.Length; j++)
				{
					var fi = faces[i]; var fj = faces[j];
					if (fi.brush == fj.brush ||
						math.any(fj.min > fi.max + touch) || math.any(fi.min > fj.max + touch) ||
						!ExactPlaneMerge.OnePlane(fi.key, fj.key))
						continue;
					int ri = Find(parent, i), rj = Find(parent, j);
					if (ri != rj)
						parent[math.max(ri, rj)] = math.min(ri, rj);
				}
			}
			var groups = new int[faces.Length];
			for (int i = 0; i < groups.Length; i++)
				groups[i] = Find(parent, i);
			return groups;
		}

		static int[] UniteGroups(NativeList<ExactPlaneMerge.Face> faces, out long compared)
		{
			var parent = new NativeArray<int>(faces.Length, Allocator.Temp);
			try
			{
				compared = ExactPlaneMerge.Unite(faces, parent);
				var groups = new int[faces.Length];
				for (int i = 0; i < groups.Length; i++)
				{
					int r = i;
					while (parent[r] != r)
						r = parent[r];
					groups[i] = r;
				}
				return groups;
			}
			finally
			{
				parent.Dispose();
			}
		}

		// Planes on both sides of the edges of any bucketing of the plane's values, each face's key one rounding step off its
		// plane or flipped, boxes that overlap often: the groups are those of comparing every pair
		[Test]
		public void RandomFacesAroundEdgesOfPlaneValues_GroupLikeEveryPairCompared([Values(7u, 19u, 101u, 4242u)] uint seed)
		{
			var random = new Unity.Mathematics.Random(seed);
			long[] edges = { 0, 1, -1, 31, 32, 33, -31, -32, -33, 63, 64, 65, -64, -65, 4096, -4096, 1 << 20, -(1 << 20) };
			// round values, values around powers of two, and every small value, so whatever the bucketing's edges are
			// some planes lie on both sides of them
			long Value() => random.NextBool() ? edges[random.NextInt(edges.Length)] + 64L * random.NextInt(-3, 4)
											  : random.NextInt(-160, 161);
			var planes = new long[24, 4];
			for (int p = 0; p < planes.GetLength(0); p++)
				for (int d = 0; d < 4; d++)
					planes[p, d] = Value();

			var faces = new ExactPlaneMerge.Face[900];
			var key = new long[4];
			for (int f = 0; f < faces.Length; f++)
			{
				int plane = random.NextInt(planes.GetLength(0));
				long sign = random.NextBool() ? -1 : 1;
				for (int d = 0; d < 4; d++)
					key[d] = sign * (planes[plane, d] + random.NextInt(-1, 2));
				var min = new double3(random.NextInt(0, 200), random.NextInt(0, 200), random.NextInt(0, 200));
				var max = min + new double3(random.NextInt(0, 60), random.NextInt(0, 60), random.NextInt(0, 60));
				faces[f] = Face(random.NextInt(0, 300), key[0], key[1], key[2], key[3], min, max);
			}

			using var list = Sorted(faces);
			var expected = BruteForceGroups(list);
			var groups   = UniteGroups(list, out _);
			int joined = 0;
			for (int i = 0; i < expected.Length; i++)
				if (expected[i] != i) joined++;
			Assert.That(joined, Is.GreaterThan(100), "too few faces are joined for the test to show anything");
			Assert.That(groups, Is.EqualTo(expected));
		}

		// Floors a millimetre apart that all span the same 100 m in x, as a model's many parallel faces do: none of them are
		// one plane, and finding that must not compare every face with every other one
		[Test]
		public void ParallelFacesOverOneRangeOfX_AreComparedInFewPairs()
		{
			const int count = 2000;
			const double metre = 16384;     // grid steps (ExactPlane.kPointScale)
			var faces = new ExactPlaneMerge.Face[count];
			for (int f = 0; f < count; f++)
			{
				long height = 16L * f;
				faces[f] = Face(f, 0, 1 << 20, 0, -height, new double3(0, height, 0), new double3(100 * metre, height, metre));
			}
			using var list = Sorted(faces);
			var groups = UniteGroups(list, out var compared);
			for (int i = 0; i < groups.Length; i++)
				Assert.That(groups[i], Is.EqualTo(i), "faces a millimetre apart are not one plane");
			Assert.That(compared, Is.LessThan(10L * count));
		}
	}
}
