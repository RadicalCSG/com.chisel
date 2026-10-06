using System.Collections.Generic;
using NUnit.Framework;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace Chisel.Core.Tests
{
	[TestFixture]
	public class OutputWeldTests
	{
		struct Corner
		{
			public float3       collider;
			public SelectVertex select;
			public RenderVertex render;
		}

		static Corner V(float3 position, float2 uv = default, float entity = 1)
		{
			return new Corner
			{
				collider = position,
				select   = new SelectVertex { position = position, entityID = new Vector4(entity, 0, 0, 0) },
				render   = new RenderVertex { position = position, normal = new float3(0, 0, 1), tangent = new float4(1, 0, 0, 1), uv0 = uv, uv1 = uv * 0.5f }
			};
		}

		static bool SameBits(Corner a, Corner b)
		{
			return math.all(math.asuint(a.collider) == math.asuint(b.collider)) &&
				   math.all(math.asuint(a.select.position) == math.asuint(b.select.position)) &&
				   math.all(math.asuint((float4)a.select.entityID) == math.asuint((float4)b.select.entityID)) &&
				   math.all(math.asuint(a.render.position) == math.asuint(b.render.position)) &&
				   math.all(math.asuint(a.render.normal) == math.asuint(b.render.normal)) &&
				   math.all(math.asuint(a.render.tangent) == math.asuint(b.render.tangent)) &&
				   math.all(math.asuint(a.render.uv0) == math.asuint(b.render.uv0)) &&
				   math.all(math.asuint(a.render.uv1) == math.asuint(b.render.uv1));
		}

		static bool SamePosition(float3 a, float3 b) => a.x == b.x && a.y == b.y && a.z == b.z;

		// The surface after the weld: its indices, its vertices as corners, and how many triangles the weld says it dropped
		static (int[] indices, Corner[] vertices, int dropped, int flipped) Weld(int[] indices, Corner[] vertices)
		{
			var indexList    = new NativeList<int>(indices.Length, Allocator.Temp);
			var colliderList = new NativeList<float3>(vertices.Length, Allocator.Temp);
			var selectList   = new NativeList<SelectVertex>(vertices.Length, Allocator.Temp);
			var renderList   = new NativeList<RenderVertex>(vertices.Length, Allocator.Temp);
			try
			{
				foreach (var index in indices)
					indexList.Add(index);
				foreach (var vertex in vertices)
				{
					colliderList.Add(vertex.collider);
					selectList.Add(vertex.select);
					renderList.Add(vertex.render);
				}
				var dropped = OutputWeld.WeldSurface(indexList, colliderList, selectList, renderList, out var flipped);
				Assert.That(selectList.Length, Is.EqualTo(colliderList.Length), "the select stream has another length than the collider stream");
				Assert.That(renderList.Length, Is.EqualTo(colliderList.Length), "the render stream has another length than the collider stream");
				var outVertices = new Corner[colliderList.Length];
				for (int v = 0; v < outVertices.Length; v++)
					outVertices[v] = new Corner { collider = colliderList[v], select = selectList[v], render = renderList[v] };
				return (indexList.AsArray().ToArray(), outVertices, dropped, flipped);
			}
			finally
			{
				indexList.Dispose();
				colliderList.Dispose();
				selectList.Dispose();
				renderList.Dispose();
			}
		}

		static void AssertSameSurface((int[] indices, Corner[] vertices, int dropped, int flipped) result, int[] indices, Corner[] vertices, string what)
		{
			Assert.That(result.flipped, Is.EqualTo(0), what + ": a needle was flipped");
			Assert.That(result.indices, Is.EqualTo(indices), what + ": the indices changed");
			Assert.That(result.vertices.Length, Is.EqualTo(vertices.Length), what + ": the vertex count changed");
			for (int v = 0; v < vertices.Length; v++)
				Assert.That(SameBits(result.vertices[v], vertices[v]), Is.True, $"{what}: vertex {v} changed");
		}

		[Test]
		public void ACleanSurface_IsLeftExactlyAsItWas()
		{
			var vertices = new[] { V(new float3(0, 0, 0)), V(new float3(1, 0, 0), new float2(1, 0)), V(new float3(1, 1, 0), new float2(1, 1)), V(new float3(0, 1, 0), new float2(0, 1)) };
			var indices  = new[] { 0, 1, 2, 0, 2, 3 };
			var result   = Weld(indices, vertices);
			Assert.That(result.dropped, Is.EqualTo(0));
			AssertSameSurface(result, indices, vertices, "a quad");
		}

		[Test]
		public void TwoCornersThatRoundToOneFloat_AreOneVertex_AndTheirTriangleIsDropped()
		{
			var vertices = new[] { V(new float3(0, 0, 0)), V(new float3(2, 0, 0)), V(new float3(2, 1, 0)), V(new float3(2, 1, 0)), V(new float3(0, 1, 0)) };
			var result   = Weld(new[] { 0, 1, 2, 0, 2, 3, 0, 3, 4 }, vertices);
			Assert.That(result.dropped, Is.EqualTo(1), "triangles dropped");
			Assert.That(result.indices, Is.EqualTo(new[] { 0, 1, 2, 0, 2, 3 }), "the two triangles left, corner 3 shared with 2 and 4 now 3");
			Assert.That(result.vertices.Length, Is.EqualTo(4), "one vertex per record");
			Assert.That(SameBits(result.vertices[3], vertices[4]), Is.True, "the last vertex is the pentagon's corner 4");
		}

		[Test]
		public void TwoCornersAtOnePositionWithOtherAttributes_TheirTriangleIsDropped_TheVerticesStayApart()
		{
			var vertices = new[] { V(new float3(0, 0, 0)), V(new float3(2, 0, 0)), V(new float3(2, 1, 0), new float2(1, 1)), V(new float3(2, 1, 0), new float2(1, 2)), V(new float3(0, 1, 0)) };
			var result   = Weld(new[] { 0, 1, 2, 0, 2, 3, 0, 3, 4 }, vertices);
			Assert.That(result.dropped, Is.EqualTo(1), "triangles dropped");
			Assert.That(result.indices, Is.EqualTo(new[] { 0, 1, 2, 0, 3, 4 }), "the two triangles left, as they were");
			Assert.That(result.vertices.Length, Is.EqualTo(5), "the vertices with other attributes stay");
			for (int v = 0; v < 5; v++)
				Assert.That(SameBits(result.vertices[v], vertices[v]), Is.True, $"vertex {v} changed");
		}

		[Test]
		public void ANeedleWithANeighbourAcrossItsLongEdge_IsFlippedAway()
		{
			var vertices = new[] { V(new float3(0, 0, 0)), V(new float3(1, 0, 0)), V(new float3(2, 0, 0)), V(new float3(1, 1, 0)) };
			var result   = Weld(new[] { 0, 1, 2, 0, 2, 3 }, vertices);
			Assert.That(result.dropped, Is.EqualTo(0), "triangles dropped");
			Assert.That(result.flipped, Is.EqualTo(1), "needles flipped");
			Assert.That(result.indices, Is.EqualTo(new[] { 0, 1, 3, 1, 2, 3 }), "u-m-d and m-w-d, in the needle's and the neighbour's places");
			Assert.That(result.vertices.Length, Is.EqualTo(4), "the vertices stay");
			for (int v = 0; v < 4; v++)
				Assert.That(SameBits(result.vertices[v], vertices[v]), Is.True, $"vertex {v} changed");
		}

		// The same needle read from another corner: wherever its winding starts, it is the same needle
		[Test]
		public void ANeedleReadFromAnyCorner_IsFlippedAlike()
		{
			var vertices = new[] { V(new float3(0, 0, 0)), V(new float3(1, 0, 0)), V(new float3(2, 0, 0)), V(new float3(1, 1, 0)) };
			var result   = Weld(new[] { 2, 0, 1, 2, 3, 0 }, vertices);
			Assert.That(result.flipped, Is.EqualTo(1), "needles flipped");
			Assert.That(result.indices, Is.EqualTo(new[] { 0, 1, 3, 1, 2, 3 }), "u-m-d and m-w-d");
		}

		// On the surface's border nothing runs the long edge the other way: the needle stays, filling its hairline
		[Test]
		public void ANeedleOnTheBorder_StaysAsItIs()
		{
			var vertices = new[] { V(new float3(0, 0, 0)), V(new float3(1, 0, 0)), V(new float3(2, 0, 0)), V(new float3(1, -1, 0)) };
			var indices  = new[] { 0, 1, 2, 1, 0, 3 };                       // a neighbour on a short edge only
			AssertSameSurface(Weld(indices, vertices), indices, vertices, "a needle on the border");
		}

		// A neighbour whose third corner lies on the same line would turn into two needles: both stay
		[Test]
		public void ANeedleWhoseNeighbourIsOnTheSameLine_StaysAsItIs()
		{
			var vertices = new[] { V(new float3(0, 0, 0)), V(new float3(1, 0, 0)), V(new float3(2, 0, 0)), V(new float3(3, 0, 0)) };
			var indices  = new[] { 0, 1, 2, 0, 2, 3 };
			AssertSameSurface(Weld(indices, vertices), indices, vertices, "a needle beside a needle");
		}

		// Off the line by the smallest float there is: no needle, decided exactly (coordinates 127 powers of two apart, so the
		// 512 bit path), and nothing is flipped
		[Test]
		public void AnAlmostStraightTriangle_IsNoNeedle()
		{
			var vertices = new[] { V(new float3(0, 0, 0)), V(new float3(1, math.asfloat(1u), 0)), V(new float3(2, 0, 0)), V(new float3(1, 1, 0)) };
			var indices  = new[] { 0, 1, 2, 0, 2, 3 };
			Assert.That(OutputWeld.OnOneLine(vertices[0].collider, vertices[1].collider, vertices[2].collider), Is.False, "one denormal off the line is on it");
			AssertSameSurface(Weld(indices, vertices), indices, vertices, "an almost straight triangle");
		}

		// bm_c2a5g's needle (EndWeldTests): x from -39.04 through 0 to 1.37e-8, 55 powers of two apart. On one line exactly,
		// decided in 512 bits, and flipped
		[Test]
		public void ANeedleFiftyFivePowersOfTwoWide_IsDecidedExactly_AndFlipped()
		{
			var u = new float3(-39.04000473022461f, 5.032470703125f, 2.44000244140625f);
			var m = new float3(0.0f, 5.032470703125f, 2.44000244140625f);
			var w = new float3(1.3747119886886594e-08f, 5.032470703125f, 2.44000244140625f);
			var d = new float3(1.3747119886886594e-08f, 5.3375244140625f, 2.44000244140625f);
			Assert.That(OutputWeld.OnOneLine(u, m, w), Is.True, "the needle is not on one line");
			var result = Weld(new[] { 0, 1, 2, 0, 2, 3 }, new[] { V(u), V(m), V(w), V(d) });
			Assert.That(result.flipped, Is.EqualTo(1), "needles flipped");
			Assert.That(result.indices, Is.EqualTo(new[] { 0, 1, 3, 1, 2, 3 }), "u-m-d and m-w-d");
		}

		// NaN and infinity are never on a line: nothing is decided about them, nothing is flipped
		[Test]
		public void NaNAndInfinity_AreOnNoLine()
		{
			Assert.That(OutputWeld.OnOneLine(new float3(0, 0, 0), new float3(float.NaN, 0, 0), new float3(2, 0, 0)), Is.False);
			Assert.That(OutputWeld.OnOneLine(new float3(0, 0, 0), new float3(1, 0, 0), new float3(float.PositiveInfinity, 0, 0)), Is.False);
			Assert.That(OutputWeld.OnOneLine(new float3(0, 0, 0), new float3(0, 0, 0), new float3(0, 0, 0)), Is.True, "three times the origin");
		}

		[Test]
		public void OnOneLine_AgreesWithAnExactOracle([NUnit.Framework.Range(1, 40)] int seed)
		{
			var random = new Unity.Mathematics.Random((uint)seed * 747796405u + 7);
			for (int i = 0; i < 500; i++)
			{
				float3 a, b, c;
				var kind = random.NextInt(6);
				var scale = math.asfloat((uint)random.NextInt(1, 254) << 23);          // a power of two, 2^-126 .. 2^126
				a = RandomPoint(ref random, scale);
				var direction = new float3(random.NextInt(-4, 5), random.NextInt(-4, 5), random.NextInt(-4, 5)) * scale;
				switch (kind)
				{
					case 0:  b = RandomPoint(ref random, scale); c = RandomPoint(ref random, scale); break;              // anything
					case 1:  b = a + direction; c = a + direction * 2; break;                                            // on a line (when exact)
					case 2:  b = a + direction; c = a + direction * 3; c.y = NextAfter(c.y); break;                      // a float step off
					case 3:  b = new float3(a.x * 2, a.y, a.z); c = new float3(-a.x, a.y, a.z); break;                   // along x
					case 4:  b = new float3(math.asfloat(1u), a.y, a.z); c = new float3(-0.0f, a.y, a.z); break;         // denormal and -0
					default: b = a * 0.5f; c = a * math.asfloat((uint)random.NextInt(1, 254) << 23); break;              // through the origin, far apart
				}
				var expected = OracleOnOneLine(a, b, c, out var span);
				if (span > 228)
				{
					Assert.That(OutputWeld.OnOneLine(a, b, c), Is.False, $"seed {seed}, triple {i}: wider than the 512 bit path decides, yet decided");
					continue;
				}
				Assert.That(OutputWeld.OnOneLine(a, b, c), Is.EqualTo(expected), $"seed {seed}, triple {i} (kind {kind}): {a} {b} {c}");
			}
		}

		static float3 RandomPoint(ref Unity.Mathematics.Random random, float scale)
		{
			return new float3(random.NextInt(-1000, 1001), random.NextInt(-1000, 1001), random.NextInt(-1000, 1001)) * (scale / 512.0f);
		}

		static float NextAfter(float value)
		{
			var bits = math.asuint(value);
			if (value == 0) return math.asfloat(1u);
			return math.asfloat(value > 0 ? bits + 1 : bits - 1);
		}

		static System.Numerics.BigInteger Exact(float value)
		{
			var bits  = math.asuint(value);
			var field = (int)((bits >> 23) & 0xFF);
			long whole = field == 0 ? (bits & 0x7FFFFF) : ((bits & 0x7FFFFF) | 0x800000);
			var result = new System.Numerics.BigInteger(whole) << (field == 0 ? 0 : field - 1);
			return (bits & 0x80000000u) != 0 ? -result : result;
		}

		// The exact answer, and how many powers of two the nonzero coordinates span (what OnOneLine's integer grid needs)
		static bool OracleOnOneLine(float3 a, float3 b, float3 c, out int span)
		{
			span = 0;
			if (!math.all(math.isfinite(a)) || !math.all(math.isfinite(b)) || !math.all(math.isfinite(c)))
				return false;
			int lowest = int.MaxValue, highest = int.MinValue;
			foreach (var v in new[] { a.x, a.y, a.z, b.x, b.y, b.z, c.x, c.y, c.z })
			{
				if (v == 0) continue;
				var field = (int)((math.asuint(v) >> 23) & 0xFF);
				var e = field == 0 ? -149 : field - 150;
				lowest = math.min(lowest, e); highest = math.max(highest, e);
			}
			span = lowest == int.MaxValue ? 0 : highest - lowest;
			var ux = Exact(b.x) - Exact(a.x); var uy = Exact(b.y) - Exact(a.y); var uz = Exact(b.z) - Exact(a.z);
			var wx = Exact(c.x) - Exact(a.x); var wy = Exact(c.y) - Exact(a.y); var wz = Exact(c.z) - Exact(a.z);
			return (uy * wz - uz * wy).IsZero && (uz * wx - ux * wz).IsZero && (ux * wy - uy * wx).IsZero;
		}

		// 0 and -0 are one position, so a triangle with corners at both draws nothing and goes; the two are other bits, so they
		// are not one vertex, and the one no triangle uses any more goes
		[Test]
		public void ZeroAndMinusZero_AreOnePosition_NotOneVertex()
		{
			var vertices = new[] { V(new float3(0, 0, 0)), V(new float3(-0.0f, 0, 0)), V(new float3(0, 1, 0)), V(new float3(1, 0, 0)) };
			var result   = Weld(new[] { 0, 1, 2, 0, 3, 2 }, vertices);
			Assert.That(result.dropped, Is.EqualTo(1), "triangles dropped");
			Assert.That(result.indices, Is.EqualTo(new[] { 0, 2, 1 }), "the triangle left, renumbered");
			Assert.That(result.vertices.Length, Is.EqualTo(3), "the -0 vertex nothing uses is gone");
			Assert.That(math.asuint(result.vertices[0].collider.x), Is.EqualTo(0u), "the vertex at 0 kept its bits");
		}

		// A NaN position is never equal to anything, so a broken vertex is never hidden by dropping its triangle
		[Test]
		public void ANaNCorner_IsNeverAtThePositionOfAnother_SoItsTriangleStays()
		{
			var nan      = new float3(float.NaN, 0, 0);
			var vertices = new[] { V(nan), V(nan), V(new float3(0, 1, 0)) };
			var result   = Weld(new[] { 0, 1, 2 }, vertices);
			Assert.That(result.dropped, Is.EqualTo(0), "a triangle with NaN corners was dropped");
			Assert.That(result.indices.Length, Is.EqualTo(3), "the triangle is gone");
		}

		[Test]
		public void ASurfaceOfOnlyCollapsedTriangles_IsLeftEmpty()
		{
			var vertices = new[] { V(new float3(0, 0, 0)), V(new float3(0, 0, 0)), V(new float3(1, 0, 0)), V(new float3(1, 0, 0)) };
			var result   = Weld(new[] { 0, 1, 2, 2, 3, 0 }, vertices);
			Assert.That(result.dropped, Is.EqualTo(2));
			Assert.That(result.indices, Is.Empty);
			Assert.That(result.vertices, Is.Empty);
		}

		// Nothing here may make a broken surface worse: an index out of range, or a count that is no whole triangle, leaves it as it is
		[Test]
		public void ABrokenSurface_IsLeftAsItIs()
		{
			var vertices = new[] { V(new float3(0, 0, 0)), V(new float3(0, 0, 0)), V(new float3(1, 0, 0)) };
			var outOfRange = new[] { 0, 1, 2, 0, 1, 7 };
			AssertSameSurface(Weld(outOfRange, vertices), outOfRange, vertices, "an index out of range");
			var negative = new[] { 0, 1, 2, -1, 1, 2 };
			AssertSameSurface(Weld(negative, vertices), negative, vertices, "a negative index");
			var partial = new[] { 0, 1, 2, 0 };
			AssertSameSurface(Weld(partial, vertices), partial, vertices, "an index count that is no whole triangle");
		}

		[Test]
		public void RandomSurfaces_KeepEveryTriangleThatHasArea_CornerForCorner([NUnit.Framework.Range(1, 300)] int seed)
		{
			var random   = new Unity.Mathematics.Random((uint)seed * 2654435761u + 1);
			var vertices = new List<Corner>();
			var count    = random.NextInt(3, 24);
			for (int v = 0; v < count; v++)
			{
				if (vertices.Count > 0 && random.NextInt(4) == 0)
				{
					vertices.Add(vertices[random.NextInt(vertices.Count)]);        // a copy, bit for bit
					continue;
				}
				var position = new float3(random.NextInt(3), random.NextInt(3), random.NextInt(2));
				if (position.x == 0 && random.NextInt(4) == 0)
					position.x = -0.0f;                                          // sometimes -0 where it is 0
				vertices.Add(V(position, new float2(random.NextInt(2), 0), random.NextInt(1, 3)));
			}
			var indices = new List<int>();
			var triangles = random.NextInt(1, 20);
			for (int t = 0; t < triangles * 3; t++)
				indices.Add(random.NextInt(vertices.Count));

			var input  = vertices.ToArray();
			var result = Weld(indices.ToArray(), input);
			AssertWeldInvariants(indices.ToArray(), input, result, $"seed {seed}");
			if (result.flipped > 0)
				return;     // the flip replaced needles and neighbours: the invariants above are what holds, corner for corner no longer

			var expected = new List<Corner>();
			var expectedDropped = 0;
			for (int t = 0; t < indices.Count; t += 3)
			{
				var a = input[indices[t]]; var b = input[indices[t + 1]]; var c = input[indices[t + 2]];
				if (SamePosition(a.collider, b.collider) || SamePosition(b.collider, c.collider) || SamePosition(c.collider, a.collider))
				{
					expectedDropped++;
					continue;
				}
				expected.Add(a); expected.Add(b); expected.Add(c);
			}
			Assert.That(result.dropped, Is.EqualTo(expectedDropped), $"seed {seed}: triangles dropped");
			Assert.That(result.indices.Length, Is.EqualTo(expected.Count), $"seed {seed}: corners kept");
			for (int i = 0; i < expected.Count; i++)
				Assert.That(SameBits(result.vertices[result.indices[i]], expected[i]), Is.True, $"seed {seed}: corner {i} is not the corner it was");
			var used = new bool[result.vertices.Length];
			foreach (var index in result.indices)
				used[index] = true;
			for (int v = 0; v < result.vertices.Length; v++)
			{
				Assert.That(used[v], Is.True, $"seed {seed}: vertex {v} is used by no triangle");
				for (int w = v + 1; w < result.vertices.Length; w++)
					Assert.That(SameBits(result.vertices[v], result.vertices[w]), Is.False, $"seed {seed}: vertices {v} and {w} are the same");
			}
		}

		static void AssertWeldInvariants(int[] inputIndices, Corner[] input, (int[] indices, Corner[] vertices, int dropped, int flipped) result, string what)
		{
			Assert.That(result.indices.Length, Is.EqualTo(inputIndices.Length - 3 * result.dropped), what + ": triangles other than the dropped ones went");
			int needlesBefore = 0, needlesAfter = 0;
			for (int t = 0; t < inputIndices.Length; t += 3)
			{
				var a = input[inputIndices[t]].collider; var b = input[inputIndices[t + 1]].collider; var c = input[inputIndices[t + 2]].collider;
				if (!SamePosition(a, b) && !SamePosition(b, c) && !SamePosition(c, a) && OutputWeld.OnOneLine(a, b, c))
					needlesBefore++;
			}
			for (int t = 0; t < result.indices.Length; t += 3)
			{
				var a = result.vertices[result.indices[t]].collider; var b = result.vertices[result.indices[t + 1]].collider; var c = result.vertices[result.indices[t + 2]].collider;
				Assert.That(SamePosition(a, b) || SamePosition(b, c) || SamePosition(c, a), Is.False, what + ": a triangle with two corners at one position is left");
				if (OutputWeld.OnOneLine(a, b, c))
					needlesAfter++;
			}
			Assert.That(needlesAfter, Is.EqualTo(needlesBefore - result.flipped), what + ": needles left");
			foreach (var corner in result.vertices)
			{
				var known = false;
				foreach (var original in input)
					known |= SameBits(corner, original);
				Assert.That(known, Is.True, what + ": a vertex the input does not have");
			}
			Assert.That(NetEdges(result.indices, result.vertices), Is.EquivalentTo(NetEdges(inputIndices, input)), what + ": the net edges changed");
		}

		// Per pair of positions, how often it is run one way more than the other (0 left out). -0 is 0 here, as everywhere.
		static Dictionary<(float3, float3), int> NetEdges(int[] indices, Corner[] vertices)
		{
			var net = new Dictionary<(float3, float3), int>();
			for (int t = 0; t + 2 < indices.Length; t += 3)
			{
				for (int e = 0; e < 3; e++)
				{
					var from = vertices[indices[t + e]].collider + 0.0f;
					var to   = vertices[indices[t + (e + 1) % 3]].collider + 0.0f;
					if (SamePosition(from, to))
						continue;
					var forward = math.asuint(from).x < math.asuint(to).x || (math.asuint(from).x == math.asuint(to).x &&
								  (math.asuint(from).y < math.asuint(to).y || (math.asuint(from).y == math.asuint(to).y && math.asuint(from).z < math.asuint(to).z)));
					var key = forward ? (from, to) : (to, from);
					net.TryGetValue(key, out var n);
					net[key] = n + (forward ? 1 : -1);
				}
			}
			foreach (var key in new List<(float3, float3)>(net.Keys))
			{
				if (net[key] == 0)
					net.Remove(key);
			}
			return net;
		}

		[Test]
		public void AClosedSolidWithCopiedCornersAndSpikes_StaysClosed()
		{
			// an octahedron
			var p = new[] { new float3(1, 0, 0), new float3(-1, 0, 0), new float3(0, 1, 0), new float3(0, -1, 0), new float3(0, 0, 1), new float3(0, 0, -1) };
			var faces = new[] { 0, 2, 4, 2, 1, 4, 1, 3, 4, 3, 0, 4, 2, 0, 5, 1, 2, 5, 3, 1, 5, 0, 3, 5 };
			var vertices = new List<Corner>();
			foreach (var position in p)
				vertices.Add(V(position));
			// each face uses its own copy of its first corner
			var indices = new List<int>();
			for (int f = 0; f < faces.Length; f += 3)
			{
				vertices.Add(V(p[faces[f]]));
				indices.Add(vertices.Count - 1); indices.Add(faces[f + 1]); indices.Add(faces[f + 2]);
			}
			// and spikes: a triangle from a corner to its copy and a neighbour, whose two long edges cancel
			indices.Add(0); indices.Add(6); indices.Add(2);
			indices.Add(4); indices.Add(4); indices.Add(1);

			Assert.That(IsClosed(indices.ToArray(), vertices.ToArray()), Is.True, "the octahedron is not closed to begin with");
			var result = Weld(indices.ToArray(), vertices.ToArray());
			Assert.That(result.dropped, Is.EqualTo(2), "the two spikes");
			Assert.That(result.indices.Length, Is.EqualTo(24), "the octahedron's 8 faces");
			Assert.That(result.vertices.Length, Is.EqualTo(6), "one vertex per corner: the copies are the same records");
			Assert.That(IsClosed(result.indices, result.vertices), Is.True, "the welded octahedron is open");
		}

		static bool IsClosed(int[] indices, Corner[] vertices)
		{
			var edges = new Dictionary<(float3, float3), int>();
			for (int t = 0; t < indices.Length; t += 3)
			{
				for (int e = 0; e < 3; e++)
				{
					var from = vertices[indices[t + e]].collider;
					var to   = vertices[indices[t + (e + 1) % 3]].collider;
					if (SamePosition(from, to))
						continue;
					edges.TryGetValue((from, to), out var n);
					edges[(from, to)] = n + 1;
				}
			}
			foreach (var edge in edges)
			{
				edges.TryGetValue((edge.Key.Item2, edge.Key.Item1), out var back);
				if (back != edge.Value)
					return false;
			}
			return true;
		}

		// The exact judge counts the rounded exact triangles it does not look for in the mesh, because the weld drops them:
		// exactly the drain cap's two (EndWeldTests), and none of a box's
		[Test]
		public void TheExactJudge_CountsTheTrianglesTheWeldDrops()
		{
			var planes = new float4[EndWeldTests.kDrainCapC3a2c.Length / 4];
			for (int p = 0; p < planes.Length; p++)
				planes[p] = math.asfloat(new uint4(EndWeldTests.kDrainCapC3a2c[p * 4], EndWeldTests.kDrainCapC3a2c[p * 4 + 1], EndWeldTests.kDrainCapC3a2c[p * 4 + 2], EndWeldTests.kDrainCapC3a2c[p * 4 + 3]));
			var pivot = math.asfloat(new uint3(EndWeldTests.kDrainCapC3a2cPivot[0], EndWeldTests.kDrainCapC3a2cPivot[1], EndWeldTests.kDrainCapC3a2cPivot[2]));
			var cap = ContentsSceneNode.Brush(planes, localToTree: float4x4.Translate(pivot), name: "drain cap");
			using (var harness = ContentsTreeHarness.Build(new ContentsScene().Add(cap)))
			{
				Assert.That(harness.Update(), Is.True, "the CSG update did not run");
				var mismatches = ExactJudge.FindMismatches(harness, 8, out _, checkMesh: true);
				Assert.That(mismatches, Is.Empty, "the drain cap's mesh is judged wrong");
				Assert.That(ExactJudge.LastDroppedByTheWeld, Is.EqualTo(2), "the exact triangles the weld drops from the drain cap");
			}
			var box = ContentsSceneNode.Brush(ContentsScene.BoxPlanes(new float3(0, 0, 0), new float3(1, 1, 1)), name: "box");
			using (var harness = ContentsTreeHarness.Build(new ContentsScene().Add(box)))
			{
				Assert.That(harness.Update(), Is.True, "the CSG update did not run");
				Assert.That(ExactJudge.FindMismatches(harness, 8, out _, checkMesh: true), Is.Empty, "the box's mesh is judged wrong");
				Assert.That(ExactJudge.LastDroppedByTheWeld, Is.EqualTo(0), "the weld drops nothing of a box");
			}
		}

		// The exact judge recognises the needles the weld flipped away (ExactJudge.CheckMesh): the wedges of bm_c0a0a
		// (EndWeldTests) have one, and are judged right, mesh included
		[Test]
		public void TheExactJudge_CountsTheNeedlesTheWeldFlips()
		{
			using var harness = ContentsTreeHarness.Build(EndWeldTests.SceneOf(EndWeldTests.kWedgesC0a0a, EndWeldTests.kWedgesC0a0aPivots, "wedges"));
			Assert.That(harness.Update(), Is.True, "the CSG update did not run");
			var mismatches = ExactJudge.FindMismatches(harness, 8, out _, checkMesh: true);
			Assert.That(mismatches, Is.Empty, "the wedges' mesh is judged wrong");
			Assert.That(ExactJudge.LastFlippedByTheWeld, Is.EqualTo(1), "needles the weld flipped away");
		}

		// Every surface buffer is welded as it is made: what a buffer counts, hashes and holds is the welded surface
		[Test]
		public void ASurfaceBuffer_IsMadeOfTheWeldedSurface()
		{
			var vertices = new[] { V(new float3(0, 0, 0)), V(new float3(2, 0, 0)), V(new float3(2, 1, 0)), V(new float3(2, 1, 0)), V(new float3(0, 1, 0)) };
			var indices  = new[] { 0, 1, 2, 0, 2, 3, 0, 3, 4 };
			var indexList    = new NativeList<int>(Allocator.Temp);
			var colliderList = new NativeList<float3>(Allocator.Temp);
			var selectList   = new NativeList<SelectVertex>(Allocator.Temp);
			var renderList   = new NativeList<RenderVertex>(Allocator.Temp);
			try
			{
				foreach (var index in indices)
					indexList.Add(index);
				foreach (var vertex in vertices)
				{
					colliderList.Add(vertex.collider);
					selectList.Add(vertex.select);
					renderList.Add(vertex.render);
				}
				using var builder = new BlobBuilder(Allocator.Temp);
				ref var root = ref builder.ConstructRoot<ChiselSurfaceRenderBuffer>();
				root.Construct(builder, indexList, colliderList, selectList, renderList, 0, 0,
							   SurfaceDestinationFlags.Renderable | SurfaceDestinationFlags.Collidable, default, default);
				using var blob = builder.CreateBlobAssetReference<ChiselSurfaceRenderBuffer>(Allocator.Temp);
				ref var buffer = ref blob.Value;
				Assert.That(buffer.indexCount, Is.EqualTo(6), "the buffer counts the collapsed triangle");
				Assert.That(buffer.vertexCount, Is.EqualTo(4), "the buffer counts the copied corner");
				Assert.That(buffer.indices.Length, Is.EqualTo(6), "the buffer holds the collapsed triangle");
				Assert.That(buffer.renderVertices.Length, Is.EqualTo(4), "the buffer holds the copied corner");
				for (int i = 0; i < buffer.indices.Length; i += 3)
				{
					var a = buffer.colliderVertices[buffer.indices[i]];
					var b = buffer.colliderVertices[buffer.indices[i + 1]];
					var c = buffer.colliderVertices[buffer.indices[i + 2]];
					Assert.That(SamePosition(a, b) || SamePosition(b, c) || SamePosition(c, a), Is.False, "the buffer holds a triangle with two corners at one position");
				}
			}
			finally
			{
				indexList.Dispose();
				colliderList.Dispose();
				selectList.Dispose();
				renderList.Dispose();
			}
		}
	}
}
