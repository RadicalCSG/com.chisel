using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace Chisel.Core.Tests
{
	[TestFixture]
	public class OutputModelWeldTests
	{
		// A seam along x. The needle's surface lies in z = 0: u-w-p, with the needle u-w-m on its edge u-w. The surface
		// across lies in y = 0: w-u-d.
		static readonly float3 U = new float3(0, 0, 0), M = new float3(1, 0, 0), W = new float3(2, 0, 0);
		static readonly float3 P = new float3(1, 1, 0), D = new float3(1, 0, -1);
		static readonly float3[] kNeedleSurface = { U, W, M,  W, P, M,  P, U, M };
		static readonly float3[] kSurfaceAcross = { W, U, D };

		// What a vertex carries follows from its position, as a surface's attributes follow from its plane
		static RenderVertex Render(float3 p)
		{
			return new RenderVertex { position = p, normal = new float3(0, 0, 1), tangent = new float4(1, 0, 0, 1),
									  uv0 = new float2(p.x * 0.5f, p.y + p.z), uv1 = new float2(p.x, p.y - p.z) * 0.25f };
		}

		static BlobAssetReference<ChiselBrushRenderBuffer> Brush(params float3[][] surfaces)
		{
			return Brush(SurfaceDestinationFlags.Renderable | SurfaceDestinationFlags.Collidable, surfaces);
		}

		// The same, its surfaces going into the meshes `flags` say
		static BlobAssetReference<ChiselBrushRenderBuffer> Brush(SurfaceDestinationFlags flags, params float3[][] surfaces)
		{
			var builder = new BlobBuilder(Allocator.Temp);
			try
			{
				ref var root = ref builder.ConstructRoot<ChiselBrushRenderBuffer>();
				var buffers = builder.Allocate(ref root.surfaces, surfaces.Length);
				for (int s = 0; s < surfaces.Length; s++)
				{
					using var indices  = new NativeList<int>(Allocator.Temp);
					using var collider = new NativeList<float3>(Allocator.Temp);
					using var select   = new NativeList<SelectVertex>(Allocator.Temp);
					using var render   = new NativeList<RenderVertex>(Allocator.Temp);
					foreach (var p in surfaces[s])
					{
						indices.Add(collider.Length);
						collider.Add(p);
						select.Add(new SelectVertex { position = p, entityID = new Vector4(1, 0, 0, 0) });
						render.Add(Render(p));
					}
					buffers[s].Construct(builder, indices, collider, select, render, s, s, flags, default, default);
				}
				var queries = builder.Allocate(ref root.querySurfaces, 1);
				using var list = new NativeList<ChiselQuerySurface>(Allocator.Temp);
				for (int s = 0; s < surfaces.Length; s++)
				{
					if (buffers[s].indexCount == 0)
						continue;
					list.Add(new ChiselQuerySurface { surfaceIndex = s, vertexCount = buffers[s].vertexCount, indexCount = buffers[s].indexCount,
													  geometryHashValue = buffers[s].geometryHashValue, surfaceHashValue = buffers[s].surfaceHashValue });
				}
				builder.Construct(ref queries[0].surfaces, list);
				root.surfaceOffset = 0;
				root.surfaceCount  = surfaces.Length;
				return builder.CreateBlobAssetReference<ChiselBrushRenderBuffer>(Allocator.Persistent);
			}
			finally
			{
				builder.Dispose();
			}
		}

		// The brushes of a model as the meshes gather them, welded across the model; disposes every buffer it saw
		sealed class Model : System.IDisposable
		{
			public readonly BlobAssetReference<ChiselBrushRenderBuffer>[] originals;
			public NativeList<BrushData> brushes = new NativeList<BrushData>(Allocator.Temp);
			public NativeList<BlobAssetReference<ChiselBrushRenderBuffer>> patched = new NativeList<BlobAssetReference<ChiselBrushRenderBuffer>>(Allocator.Temp);
			public int split, paired, cancelled;
			public readonly Dictionary<(float3, float3), int> boundaryBefore;

			public Model(params BlobAssetReference<ChiselBrushRenderBuffer>[] buffers)
			{
				originals = buffers;
				foreach (var buffer in buffers)
					brushes.Add(new BrushData { brushRenderBuffer = buffer, brushSurfaceCount = buffer.Value.surfaceCount });
				boundaryBefore = Boundary(buffers);
				OutputModelWeld.WeldModel(brushes, patched, out split, out paired, out cancelled);
			}

			public BlobAssetReference<ChiselBrushRenderBuffer> this[int brush] => brushes[brush].brushRenderBuffer;

			public BlobAssetReference<ChiselBrushRenderBuffer>[] Buffers()
			{
				var result = new BlobAssetReference<ChiselBrushRenderBuffer>[brushes.Length];
				for (int b = 0; b < brushes.Length; b++)
					result[b] = brushes[b].brushRenderBuffer;
				return result;
			}

			public void Dispose()
			{
				foreach (var copy in patched) copy.Dispose();
				foreach (var original in originals) original.Dispose();
				brushes.Dispose();
				patched.Dispose();
			}
		}

		static List<(float3 a, float3 b, float3 c)> Triangles(BlobAssetReference<ChiselBrushRenderBuffer> buffer, int surface)
		{
			ref var s = ref buffer.Value.surfaces[surface];
			var result = new List<(float3, float3, float3)>();
			for (int i = 0; i + 2 < s.indices.Length; i += 3)
				result.Add((s.colliderVertices[s.indices[i]], s.colliderVertices[s.indices[i + 1]], s.colliderVertices[s.indices[i + 2]]));
			return result;
		}

		// The directed edges no other edge runs the other way, over every surface of every brush: what a model's surface
		// closes against
		static Dictionary<(float3, float3), int> Boundary(IEnumerable<BlobAssetReference<ChiselBrushRenderBuffer>> buffers)
		{
			var count = new Dictionary<(float3, float3), int>();
			foreach (var buffer in buffers)
			{
				for (int s = 0; s < buffer.Value.surfaces.Length; s++)
				{
					foreach (var (a, b, c) in Triangles(buffer, s))
					{
						foreach (var (from, to) in new[] { (a, b), (b, c), (c, a) })
						{
							count.TryGetValue((from, to), out var n);
							count[(from, to)] = n + 1;
						}
					}
				}
			}
			var net = new Dictionary<(float3, float3), int>();
			foreach (var pair in count)
			{
				count.TryGetValue((pair.Key.Item2, pair.Key.Item1), out var back);
				if (pair.Value > back)
					net[pair.Key] = pair.Value - back;
			}
			return net;
		}

		static bool HasTriangle(List<(float3 a, float3 b, float3 c)> triangles, float3 a, float3 b, float3 c)
		{
			foreach (var t in triangles)
			{
				if ((t.a.Equals(a) && t.b.Equals(b) && t.c.Equals(c)) || (t.a.Equals(b) && t.b.Equals(c) && t.c.Equals(a)) ||
					(t.a.Equals(c) && t.b.Equals(a) && t.c.Equals(b)))
					return true;
			}
			return false;
		}

		static void AssertStoredAsTheCSGStoresIt(BlobAssetReference<ChiselBrushRenderBuffer> buffer, string what)
		{
			ref var brush = ref buffer.Value;
			for (int s = 0; s < brush.surfaces.Length; s++)
			{
				ref var surface = ref brush.surfaces[s];
				Assert.That(surface.indexCount, Is.EqualTo(surface.indices.Length), what + ": index count");
				Assert.That(surface.vertexCount, Is.EqualTo(surface.colliderVertices.Length), what + ": vertex count");
				Assert.That(surface.renderVertices.Length, Is.EqualTo(surface.vertexCount), what + ": render vertices");
				Assert.That(surface.selectVertices.Length, Is.EqualTo(surface.vertexCount), what + ": select vertices");
				var used = new bool[surface.vertexCount];
				for (int i = 0; i < surface.indices.Length; i++)
				{
					Assert.That(surface.indices[i], Is.InRange(0, surface.vertexCount - 1), what + ": an index out of range");
					used[surface.indices[i]] = true;
				}
				Assert.That(used.All(u => u), Is.True, what + ": a vertex no triangle uses");
				foreach (var (a, b, c) in Triangles(buffer, s))
					Assert.That(OutputWeld.HasTwoCornersAtOnePosition(a, b, c), Is.False, what + ": a triangle with two corners at one position");
				var needles = 0;
				foreach (var (a, b, c) in Triangles(buffer, s))
					if (ExactFloatLine.AsNeedle(a, b, c, out _, out _, out _)) needles++;
				Assert.That(surface.needles.Length, Is.EqualTo(needles), what + ": the needles it records");
				for (int v = 0; v < surface.vertexCount; v++)
				{
					Assert.That(math.asuint(surface.renderVertices[v].position), Is.EqualTo(math.asuint(surface.colliderVertices[v])), what + ": render position");
					Assert.That(math.asuint(surface.selectVertices[v].position), Is.EqualTo(math.asuint(surface.colliderVertices[v])), what + ": select position");
				}
			}
			ref var query = ref brush.querySurfaces[0].surfaces;
			for (int e = 0; e < query.Length; e++)
			{
				ref var surface = ref brush.surfaces[query[e].surfaceIndex];
				Assert.That(query[e].indexCount, Is.EqualTo(surface.indexCount), what + ": the mesh query's index count");
				Assert.That(query[e].vertexCount, Is.EqualTo(surface.vertexCount), what + ": the mesh query's vertex count");
				Assert.That(query[e].geometryHashValue, Is.EqualTo(surface.geometryHashValue), what + ": the mesh query's geometry hash");
				Assert.That(surface.indexCount, Is.GreaterThan(0), what + ": the mesh query lists a surface without triangles");
			}
		}

		[Test]
		public void ANeedleOnASeamBetweenTwoBrushes_Goes_AndTheTriangleAcrossIsSplitAtItsMiddleCorner()
		{
			using var model = new Model(Brush(kNeedleSurface), Brush(kSurfaceAcross));
			Assert.That((model.split, model.paired, model.patched.Length), Is.EqualTo((1, 0, 2)), "split, paired, brushes copied");

			var needleSide = Triangles(model[0], 0);
			Assert.That(needleSide.Count, Is.EqualTo(2), "the needle's surface");
			Assert.That(HasTriangle(needleSide, U, W, M), Is.False, "the needle is still there");
			var across = Triangles(model[1], 0);
			Assert.That(across.Count, Is.EqualTo(2), "the surface across");
			Assert.That(HasTriangle(across, W, M, D) && HasTriangle(across, M, U, D), Is.True, "the triangle across is not split at m: " + string.Join("; ", across));
			Assert.That(Boundary(model.Buffers()), Is.EquivalentTo(model.boundaryBefore), "what the surfaces close against changed");
			AssertStoredAsTheCSGStoresIt(model[0], "the needle's brush");
			AssertStoredAsTheCSGStoresIt(model[1], "the brush across");

			// the new corner is m, bit for bit, with what the surface across carries there
			ref var surface = ref model[1].Value.surfaces[0];
			var found = false;
			for (int v = 0; v < surface.vertexCount; v++)
			{
				if (!surface.colliderVertices[v].Equals(M))
					continue;
				found = true;
				var expected = Render(M);
				var actual = surface.renderVertices[v];
				Assert.That(math.asuint(actual.position), Is.EqualTo(math.asuint(M)), "its position");
				Assert.That(actual.uv0, Is.EqualTo(expected.uv0), "its texture coordinate");
				Assert.That(actual.uv1, Is.EqualTo(expected.uv1), "its lightmap coordinate");
				Assert.That(actual.normal, Is.EqualTo(expected.normal), "its normal");
				Assert.That(actual.tangent, Is.EqualTo(expected.tangent), "its tangent");
			}
			Assert.That(found, Is.True, "the surface across has no corner at m");

			// and the buffers the CSG made, which the cache keeps, are as they were
			Assert.That(model.originals[0].Value.surfaces[0].indexCount, Is.EqualTo(9), "the cached needle's surface changed");
			Assert.That(model.originals[0].Value.surfaces[0].needles.Length, Is.EqualTo(1), "the cached needle's surface lost its needle");
			Assert.That(model.originals[1].Value.surfaces[0].indexCount, Is.EqualTo(3), "the cached surface across changed");
		}

		[Test]
		public void ANeedleOnTheEdgeBetweenTwoFacesOfOneBrush_GoesTheSameWay()
		{
			using var model = new Model(Brush(kNeedleSurface, kSurfaceAcross));
			Assert.That((model.split, model.paired, model.patched.Length), Is.EqualTo((1, 0, 1)), "split, paired, brushes copied");
			Assert.That(Triangles(model[0], 0).Count, Is.EqualTo(2), "the needle's face");
			Assert.That(Triangles(model[0], 1).Count, Is.EqualTo(2), "the face across");
			Assert.That(Boundary(model.Buffers()), Is.EquivalentTo(model.boundaryBefore), "what the faces close against changed");
			AssertStoredAsTheCSGStoresIt(model[0], "the brush");
		}

		[Test]
		public void TwoFacingNeedles_GoTogether()
		{
			var Q = new float3(1, 0, -1);
			using var model = new Model(Brush(kNeedleSurface), Brush(new[] { W, U, M,  M, U, Q,  W, M, Q }));
			Assert.That((model.split, model.paired, model.patched.Length), Is.EqualTo((0, 1, 2)), "split, paired, brushes copied");
			Assert.That(Triangles(model[0], 0).Count, Is.EqualTo(2), "the first needle's surface");
			Assert.That(Triangles(model[1], 0).Count, Is.EqualTo(2), "the second needle's surface");
			Assert.That(Boundary(model.Buffers()), Is.EquivalentTo(model.boundaryBefore), "what the surfaces close against changed");
			AssertStoredAsTheCSGStoresIt(model[0], "the first brush");
			AssertStoredAsTheCSGStoresIt(model[1], "the second brush");
		}

		[Test]
		public void ANeedleAcrossWhichLiesANeedleWithAnotherMiddleCorner_Stays()
		{
			using var model = new Model(Brush(kNeedleSurface), Brush(new[] { W, U, new float3(0.5f, 0, 0) }));
			Assert.That((model.split, model.paired, model.patched.Length), Is.EqualTo((0, 0, 0)), "split, paired, brushes copied");
			Assert.That(model[0], Is.EqualTo(model.originals[0]), "the first brush's buffer was replaced");
		}

		[Test]
		public void ANeedleWithNothingAcross_Stays()
		{
			using var model = new Model(Brush(kNeedleSurface));
			Assert.That((model.split, model.paired, model.patched.Length), Is.EqualTo((0, 0, 0)), "split, paired, brushes copied");
		}

		[Test]
		public void AModelWithoutNeedles_IsLeftAlone()
		{
			using var model = new Model(Brush(new[] { U, W, P }), Brush(kSurfaceAcross));
			Assert.That((model.split, model.paired, model.patched.Length), Is.EqualTo((0, 0, 0)), "split, paired, brushes copied");
		}

		[Test]
		public void TwoNeedlesAcrossTwoEdgesOfOneTriangle_BothSplitIt()
		{
			var N = new float3(1.5f, 0, -0.5f);
			using var model = new Model(Brush(kNeedleSurface), Brush(kSurfaceAcross), Brush(new[] { W, D, N }));
			Assert.That((model.split, model.paired, model.patched.Length), Is.EqualTo((2, 0, 3)), "split, paired, brushes copied");
			var across = Triangles(model[1], 0);
			Assert.That(across.Count, Is.EqualTo(3), "the triangle across both: " + string.Join("; ", across));
			Assert.That(HasTriangle(across, M, U, D) && HasTriangle(across, D, N, M) && HasTriangle(across, N, W, M), Is.True,
						"the triangle across both is not split at both middle corners: " + string.Join("; ", across));
			Assert.That(Triangles(model[2], 0).Count, Is.EqualTo(0), "the third brush's needle");
			Assert.That(model[2].Value.querySurfaces[0].surfaces.Length, Is.EqualTo(0), "the mesh query lists a surface left without triangles");
			Assert.That(Boundary(model.Buffers()), Is.EquivalentTo(model.boundaryBefore), "what the surfaces close against changed");
			for (int b = 0; b < 3; b++)
				AssertStoredAsTheCSGStoresIt(model[b], "brush " + b);
		}

		[Test]
		public void ANeedleAcrossTheShortEdgeOfAnother_GoesWhenTheOtherHasGone()
		{
			var K = new float3(1.5f, 0, 0);
			using var model = new Model(Brush(new[] { M, W, K }), Brush(new[] { U, W, M,  W, P, K,  K, P, M,  P, U, M }), Brush(kSurfaceAcross));
			Assert.That((model.split, model.paired, model.patched.Length), Is.EqualTo((2, 0, 3)), "split, paired, brushes copied");
			Assert.That(Triangles(model[0], 0).Count, Is.EqualTo(0), "the needle visited first is still there");
			Assert.That(Triangles(model[1], 0).Count, Is.EqualTo(3), "the second needle's surface");
			var across = Triangles(model[2], 0);
			Assert.That(across.Count, Is.EqualTo(3), "the triangle across both: " + string.Join("; ", across));
			Assert.That(HasTriangle(across, W, K, D) && HasTriangle(across, K, M, D) && HasTriangle(across, M, U, D), Is.True,
						"the triangle across is not split at both middle corners: " + string.Join("; ", across));
			Assert.That(Boundary(model.Buffers()), Is.EquivalentTo(model.boundaryBefore), "what the surfaces close against changed");
			for (int b = 0; b < 3; b++)
				AssertStoredAsTheCSGStoresIt(model[b], "brush " + b);
		}

		// Two triangles at the same three positions, bit for bit, wound the other way round, of two brushes: both go, the
		// triangle beside one of them stays, and what the surfaces close against does not change
		[Test]
		public void TwoIdenticalTrianglesFacingOppositeWays_GoTogether()
		{
			var Q = new float3(2, 1, 0);
			using var model = new Model(Brush(new[] { U, W, P,  W, Q, P }), Brush(new[] { U, P, W }));
			Assert.That((model.split, model.paired, model.cancelled, model.patched.Length), Is.EqualTo((0, 0, 1, 2)), "split, paired, cancelled, brushes copied");
			var first = Triangles(model[0], 0);
			Assert.That(first.Count == 1 && HasTriangle(first, W, Q, P), Is.True, "the first brush's surface: " + string.Join("; ", first));
			Assert.That(Triangles(model[1], 0).Count, Is.EqualTo(0), "the second brush's triangle");
			Assert.That(model[1].Value.querySurfaces[0].surfaces.Length, Is.EqualTo(0), "the mesh query lists a surface left without triangles");
			Assert.That(Boundary(model.Buffers()), Is.EquivalentTo(model.boundaryBefore), "what the surfaces close against changed");
			AssertStoredAsTheCSGStoresIt(model[0], "the first brush");
			AssertStoredAsTheCSGStoresIt(model[1], "the second brush");
			Assert.That(model.originals[1].Value.surfaces[0].indexCount, Is.EqualTo(3), "the cached buffer changed");
		}

		// The same two, where one surface goes into other meshes than the other: each mesh would lose one of them, so both stay
		[Test]
		public void TwoIdenticalTrianglesFacingOppositeWays_OfSurfacesForOtherMeshes_Stay()
		{
			using var model = new Model(Brush(new[] { U, W, P }), Brush(SurfaceDestinationFlags.Renderable, new[] { U, P, W }));
			Assert.That((model.split, model.paired, model.cancelled, model.patched.Length), Is.EqualTo((0, 0, 0, 0)), "split, paired, cancelled, brushes copied");
		}

		// A split makes a triangle the identical twin, facing the other way, of a third brush's: the two go in the same weld
		[Test]
		public void AHalfOfASplitAndItsTwinFacingTheOtherWay_GoTogether()
		{
			using var model = new Model(Brush(kNeedleSurface), Brush(kSurfaceAcross), Brush(new[] { D, U, M }));
			Assert.That((model.split, model.paired, model.cancelled, model.patched.Length), Is.EqualTo((1, 0, 1, 3)), "split, paired, cancelled, brushes copied");
			var across = Triangles(model[1], 0);
			Assert.That(across.Count == 1 && HasTriangle(across, W, M, D), Is.True, "the surface across: " + string.Join("; ", across));
			Assert.That(Triangles(model[2], 0).Count, Is.EqualTo(0), "the third brush's triangle");
			Assert.That(Boundary(model.Buffers()), Is.EquivalentTo(model.boundaryBefore), "what the surfaces close against changed");
			for (int b = 0; b < 3; b++)
				AssertStoredAsTheCSGStoresIt(model[b], "brush " + b);
		}

		// The exact judge recognises what the weld across the model took away (ExactJudge.CheckMesh) and counts it: on the
		// needle scenes of EndWeldTests, from the original data
		[Test]
		public void TheExactJudge_CountsWhatTheWeldAcrossTheModelTakesAway()
		{
			foreach (var (brushes, pivots, what, flipped, split, paired, cancelled) in new[]
			{
				(EndWeldTests.kSeamC0a0a,       EndWeldTests.kSeamC0a0aPivots,       "the seam of bm_c0a0a",       0, 1, 0, 0),
				(EndWeldTests.kFacingC4a3c,     EndWeldTests.kFacingC4a3cPivots,     "the facing needles of bm_c4a3c", 0, 0, 1, 0),
				(EndWeldTests.kBreakableC4a3b1, EndWeldTests.kBreakableC4a3b1Pivots, "the breakable of bm_c4a3b1", 1, 0, 0, 0),
				(EndWeldTests.kOccluderC2a5g,   EndWeldTests.kOccluderC2a5gPivots,   "the occluder of bm_c2a5g",   0, 1, 0, 1),
			})
			{
				using var harness = ContentsTreeHarness.Build(EndWeldTests.SceneOf(brushes, pivots, what));
				Assert.That(harness.Update(), Is.True, what + ": the CSG update did not run");
				var mismatches = ExactJudge.FindMismatches(harness, 8, out var judged, checkMesh: true);
				Assert.That(judged, Is.GreaterThan(0), what + ": nothing was judged");
				Assert.That(mismatches.Count, Is.EqualTo(0), what + ": " + string.Join("; ", mismatches.Select(x => x.Format("exact"))));
				Assert.That((ExactJudge.LastFlippedByTheWeld, ExactJudge.LastSplitByTheWeld, ExactJudge.LastPairedByTheWeld, ExactJudge.LastCancelledByTheWeld),
							Is.EqualTo((flipped, split, paired, cancelled)), what + ": flipped (or split within a brush), split into another brush, paired, cancelled");
			}
		}
	}
}
