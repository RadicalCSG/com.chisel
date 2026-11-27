using System.Collections.Generic;
using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;

namespace Chisel.Core.Tests
{
	// Positions on a grid as hash map keys (MixedKey): a map picks a bucket by the low bits of a key's hash
	[TestFixture]
	public class MixedKeyTests
	{
		const int kBuckets = 1024;

		static List<float3> Grid()
		{
			var points = new List<float3>();
			for (int x = 0; x < 32; x++)
				for (int z = 0; z < 32; z++)
					points.Add(new float3(x * 0.25f, 1.5f, z * 0.25f));
			return points;
		}

		static int Buckets(IEnumerable<int> hashes)
		{
			var used = new HashSet<int>();
			foreach (var hash in hashes)
				used.Add(hash & (kBuckets - 1));
			return used.Count;
		}

		// A grid position's float bits end in zeros, and so does their hash by multiplying and adding them: a thousand
		// positions fall into a few buckets as they are, and into most of them mixed
		[Test]
		public void PositionsOnAGrid_FallIntoMostBuckets()
		{
			var grid = Grid();
			var raw = new List<int>(); var mixed = new List<int>();
			foreach (var p in grid)
			{
				raw.Add(p.GetHashCode());
				mixed.Add(new MixedKey<float3>(p).GetHashCode());
			}
			Assert.That(Buckets(raw), Is.LessThan(kBuckets / 16), "the positions' own hashes were expected to cluster");
			Assert.That(Buckets(mixed), Is.GreaterThan(kBuckets / 2), "mixed, the positions fall into a few buckets");
		}

		[Test]
		public void AMixedKey_FindsWhatItsValueFinds()
		{
			NativeParallelHashMap<MixedKey<float3x2>, int> map;
			using var _map = map = new NativeParallelHashMap<MixedKey<float3x2>, int>(16, Allocator.Temp);
			var edge = new float3x2(new float3(0.25f, 0, 1), new float3(0.5f, 0, 1));
			map[edge] = 3;
			Assert.That(map.TryGetValue(new float3x2(new float3(0.25f, 0, 1), new float3(0.5f, 0, 1)), out var value) && value == 3, Is.True);
			Assert.That(map.ContainsKey(new float3x2(new float3(0.5f, 0, 1), new float3(0.25f, 0, 1))), Is.False, "the other way round is another edge");
		}
	}
}
