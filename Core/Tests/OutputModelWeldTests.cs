using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.TestTools;

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
			public int split, paired;
			public readonly Dictionary<(float3, float3), int> boundaryBefore;

			public Model(params BlobAssetReference<ChiselBrushRenderBuffer>[] buffers)
			{
				originals = buffers;
				foreach (var buffer in buffers)
					brushes.Add(new BrushData { brushRenderBuffer = buffer, brushSurfaceCount = buffer.Value.surfaceCount });
				boundaryBefore = Boundary(buffers);
				OutputModelWeld.WeldModel(brushes, patched, out split, out paired);
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

		// Two triangles at the same three positions, bit for bit, wound the other way round, of two brushes: the weld only
		// welds, so both stay, as an interface between two contents is drawn from each side
		[Test]
		public void TwoIdenticalTrianglesFacingOppositeWays_BothStay()
		{
			var Q = new float3(2, 1, 0);
			using var model = new Model(Brush(new[] { U, W, P,  W, Q, P }), Brush(new[] { U, P, W }));
			Assert.That((model.split, model.paired, model.patched.Length), Is.EqualTo((0, 0, 0)), "split, paired, brushes copied");
			Assert.That(model[0], Is.EqualTo(model.originals[0]), "the first brush's buffer was replaced");
			Assert.That(model[1], Is.EqualTo(model.originals[1]), "the second brush's buffer was replaced");
		}

		// A split makes a triangle at the same positions as a third brush's, facing the other way: the split is made and the
		// third brush's triangle stays
		[Test]
		public void AHalfOfASplitAtAnotherBrushsTriangleFacingTheOtherWay_BothStay()
		{
			using var model = new Model(Brush(kNeedleSurface), Brush(kSurfaceAcross), Brush(new[] { D, U, M }));
			Assert.That((model.split, model.paired, model.patched.Length), Is.EqualTo((1, 0, 2)), "split, paired, brushes copied");
			var across = Triangles(model[1], 0);
			Assert.That(across.Count == 2 && HasTriangle(across, W, M, D) && HasTriangle(across, M, U, D), Is.True, "the surface across: " + string.Join("; ", across));
			Assert.That(model[2], Is.EqualTo(model.originals[2]), "the third brush's buffer was replaced");
			Assert.That(Boundary(model.Buffers()), Is.EquivalentTo(model.boundaryBefore), "what the surfaces close against changed");
			for (int b = 0; b < 2; b++)
				AssertStoredAsTheCSGStoresIt(model[b], "brush " + b);
		}

		// The second split of a chain, in the second turn, makes k-m-d at the positions of a fourth brush's d-m-k: both stay
		[Test]
		public void TheSecondSplitOfAChainAtAnotherBrushsTriangle_BothStay()
		{
			var K = new float3(1.5f, 0, 0);
			using var model = new Model(Brush(new[] { M, W, K }), Brush(new[] { U, W, M,  W, P, K,  K, P, M,  P, U, M }), Brush(kSurfaceAcross),
										Brush(new[] { D, M, K }));
			Assert.That((model.split, model.paired, model.patched.Length), Is.EqualTo((2, 0, 3)), "split, paired, brushes copied");
			var across = Triangles(model[2], 0);
			Assert.That(across.Count == 3 && HasTriangle(across, W, K, D) && HasTriangle(across, K, M, D) && HasTriangle(across, M, U, D), Is.True,
						"the triangle across, split twice: " + string.Join("; ", across));
			Assert.That(model[3], Is.EqualTo(model.originals[3]), "the fourth brush's buffer was replaced");
			Assert.That(Boundary(model.Buffers()), Is.EquivalentTo(model.boundaryBefore), "what the surfaces close against changed");
			for (int b = 0; b < 3; b++)
				AssertStoredAsTheCSGStoresIt(model[b], "brush " + b);
		}

		// Two corners half a millimetre apart: the edge between them is shorter than kCollapseLength
		static readonly float3 ShortB = new float3(1, 0, 0), ShortC = new float3(1, 0.0005f, 0);
		static readonly float3 ShortD = new float3(0, 0, 1), ShortE = new float3(3, 2, 0), ShortF = new float3(2, 3, 0);

		static void MakeDecal(BlobAssetReference<ChiselBrushRenderBuffer> buffer, int surface)
		{
			buffer.Value.surfaces[surface].decalEntityID = 1;
		}

		// A corner where a side meets a top, and a point on the top half a millimetre off the side's plane, before the corner
		// by x, y, z: the collapse keeps the corner, so the side stays where it is, and moves the point on the top onto it
		[Test]
		public void AShortEdgeFromACorner_KeepsTheCornerOnItsFaces()
		{
			var corner = new float3(0, 1, 0);
			var onTop  = new float3(-0.0005f, 1, 0.0002f);
			float3 s0 = new float3(0, 0, 0), s1 = new float3(0, 0, 1), s2 = new float3(0, 1, 1);
			float3 t0 = new float3(-1, 1, 1), t1 = new float3(-1, 1, 0);
			var side = new[] { corner, s0, s1,  corner, s1, s2 };
			var top  = new[] { corner, s2, onTop,  onTop, s2, t0,  onTop, t0, t1 };
			using var model = new Model(Brush(side, top));

			var sideCorners = new List<float3>();
			foreach (var (a, b, c) in Triangles(model[0], 0))
				sideCorners.AddRange(new[] { a, b, c });
			Assert.That(sideCorners, Has.Member(corner), "the side's corner moved");
			foreach (var p in sideCorners)
				Assert.That(p.x, Is.EqualTo(0), $"the side is moved off its plane at {p}");
			foreach (var (a, b, c) in Triangles(model[0], 1))
			{
				foreach (var p in new[] { a, b, c })
					Assert.That(p.Equals(onTop), Is.False, "the point on the top stayed");
			}
		}

		[Test]
		public void ASurfaceWithAShortEdge_IsCollapsed()
		{
			using var model = new Model(Brush(new[] { ShortB, ShortC, ShortD }));
			Assert.That(Triangles(model[0], 0).Count, Is.EqualTo(0), "the triangle across the short edge is left without area");
		}

		// A closed surface in which the collapse of a short edge (b0 onto b) leaves a triangle with its three corners on one
		// line, a - c - b: it stays, as without it the surface is open along a - b, which the triangle across it has, while
		// the triangles across a - c and c - b have those
		[Test]
		public void ATriangleTheCollapseLeavesOnOneLine_Stays_AndTheSurfaceStaysClosed()
		{
			var a  = new float3(0, 0, 0); var c = new float3(1, 0, 0); var b = new float3(2, 0, 0);
			var b0 = new float3(2, 0.0005f, 0); var e = new float3(1, 1, 0); var f = new float3(1, 0.3f, -1);
			var top    = new[] { a, c, b0,  c, b, b0,  a, b0, e };
			var bottom = new[] { c, a, f,  b, c, f,  b0, b, f,  e, b0, f,  a, e, f };
			using var model = new Model(Brush(top, bottom));
			Assert.That(model.boundaryBefore, Is.Empty, "closed to begin with");
			Assert.That(Triangles(model[0], 0).Any(t => t.a.Equals(b0) || t.b.Equals(b0) || t.c.Equals(b0)), Is.False, "b0 was collapsed onto b");
			Assert.That(HasTriangle(Triangles(model[0], 0), a, c, b), Is.True, "the triangle the collapse left on one line");
			Assert.That(Boundary(model.Buffers()), Is.Empty, "the surface is open");
			AssertStoredAsTheCSGStoresIt(model[0], "the welded brush");
		}

		[Test]
		public void ADecalSurfaceWithAShortEdge_IsLeftAlone()
		{
			var buffer = Brush(new[] { ShortB, ShortC, ShortD });
			MakeDecal(buffer, 0);
			using var model = new Model(buffer);
			var triangles = Triangles(model[0], 0);
			Assert.That(triangles.Count, Is.EqualTo(1), "a decal's short edges collapse nothing");
			var corners = new[] { triangles[0].a, triangles[0].b, triangles[0].c };
			Assert.That(corners, Is.EquivalentTo(new[] { ShortB, ShortC, ShortD }), "and move nothing");
		}

		[Test]
		public void ADecalSurface_FollowsTheCornersTheCollapseMoves()
		{
			// the decal has one triangle at each end of the short edge; where they meet, the collapse puts both corners on one point
			var decal = Brush(new[] { ShortB, ShortE, ShortF }, new[] { ShortC, ShortF, ShortE });
			MakeDecal(decal, 0);
			MakeDecal(decal, 1);
			var solid = Brush(new[] { ShortB, ShortC, ShortD });
			using var brushes = new NativeList<BrushData>(Allocator.Temp);
			using var patched = new NativeList<BlobAssetReference<ChiselBrushRenderBuffer>>(Allocator.Temp);
			using var moves = new NativeList<float3x2>(Allocator.Temp);
			brushes.Add(new BrushData { brushRenderBuffer = solid, brushSurfaceCount = 1 });
			brushes.Add(new BrushData { brushRenderBuffer = decal, brushSurfaceCount = 2 });
			try
			{
				OutputModelWeld.WeldModel(brushes, patched, moves);
				Assert.That(moves.Length, Is.GreaterThan(0), "the solid triangle's short edge was collapsed");
				var first = Triangles(brushes[1].brushRenderBuffer, 0);
				var second = Triangles(brushes[1].brushRenderBuffer, 1);
				Assert.That(first.Count, Is.EqualTo(1));
				Assert.That(second.Count, Is.EqualTo(1));
				float3 Corner(float3 a, float3 b, float3 c) => a.Equals(ShortE) || a.Equals(ShortF) ? (b.Equals(ShortE) || b.Equals(ShortF) ? c : b) : a;
				var atB = Corner(first[0].a, first[0].b, first[0].c);
				var atC = Corner(second[0].a, second[0].b, second[0].c);
				Assert.That(atB.Equals(atC), Is.True, "the corners of the decal's pieces share a point: " + atB + " / " + atC);
			}
			finally
			{
				foreach (var copy in patched) copy.Dispose();
				solid.Dispose();
				decal.Dispose();
			}
		}

		// Three brushes of an imported map (bm_c0a0a, Details), where the weld takes out two needles that face each other
		internal static ContentsScene FacingNeedlesOfAMap()
		{
			return new ContentsScene()
				.WithContentsList("Solid", "Water", "Glass", "Grate")
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(-1f, 0f, 0f, -0.159761965f),
					new float4(1f, 0f, 0f, -0.145238042f),
					new float4(0f, -0.9805807f, 0.196116135f, -0.185143262f),
					new float4(0f, 0.196116135f, -0.9805807f, -0.19795908f),
					new float4(0f, 1f, 0f, -0.23782739f),
					new float4(0f, -0.707106769f, -0.707106769f, -0.102697767f),
					new float4(0f, 0f, 1f, -0.226936042f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(55.974762f, 0.9821726f, 108.734314f, 1f)), name: "Solid 1317905 #2356")
					.WithMesh(new float3[] { new float3(-0.159761965f, -0.143422619f, 0.226936042f), new float3(-0.159761965f, 0.23782739f, 0.226936042f), new float3(-0.159761965f, 0.23782739f, -0.154313967f), new float3(-0.159761965f, 0.047202386f, -0.19243896f), new float3(-0.159761965f, -0.181547612f, 0.0363110341f), new float3(0.145238042f, 0.23782739f, 0.226936042f), new float3(0.145238042f, -0.143422619f, 0.226936042f), new float3(0.145238042f, -0.181547612f, 0.0363110341f), new float3(0.145238042f, 0.047202386f, -0.19243896f), new float3(0.145238042f, 0.23782739f, -0.154313967f) }, new int[] { 0, 1, 2, 3, 4, -1, 5, 6, 7, 8, 9, -1, 6, 0, 4, 7, -1, 8, 3, 2, 9, -1, 1, 5, 9, 2, -1, 3, 8, 7, 4, -1, 5, 1, 0, 6, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 1f, 0f, -0.0571874864f),
					new float4(-0.07102497f, -0.7112149f, -0.6993774f, -0.04067172f),
					new float4(0.9863939f, 0f, -0.164398983f, -0.611914635f),
					new float4(0.00769208f, 0f, 0.999970436f, -0.0572624058f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(57.9690323f, 1.08656251f, 108.584763f, 1f)), name: "Solid 506451 #4064")
					.WithMesh(new float3[] { new float3(-1.84923863f, 0.0571874864f, 0.0714890137f), new float3(0.6290927f, 0.0571874864f, 0.0524249226f), new float3(0.5909677f, 0.0571874864f, -0.176325083f), new float3(0.6290927f, -0.171562523f, 0.0524249226f) }, new int[] { 0, 1, 2, -1, 3, 0, 2, -1, 1, 3, 2, -1, 3, 1, 0, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 1f, 0f, -0.0465972126f),
					new float4(0f, -1f, 0f, -0.0296527855f),
					new float4(-1f, 0f, 0f, -1.2327069f),
					new float4(0.9828722f, 0f, -0.184288532f, -1.23443079f),
					new float4(0.09875158f, 0f, 0.9951121f, -0.1701843f),
					new float4(-0.10104271f, 0f, -0.9948821f, -0.136146516f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(57.3527069f, 1.17340279f, 108.6679f, 1f)), name: "Solid 506454 #4066")
					.WithMesh(new float3[] { new float3(1.20729315f, 0.0465972126f, -0.2594626f), new float3(-1.2327069f, 0.0465972126f, -0.0116500929f), new float3(-1.2327069f, 0.0465972126f, 0.293349922f), new float3(1.26448059f, 0.0465972126f, 0.04553742f), new float3(-1.2327069f, -0.0296527855f, -0.0116500929f), new float3(1.20729315f, -0.0296527855f, -0.2594626f), new float3(1.26448059f, -0.0296527855f, 0.04553742f), new float3(-1.2327069f, -0.0296527855f, 0.293349922f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 1, 4, 7, 2, -1, 5, 0, 3, 6, -1, 6, 3, 2, 7, -1, 0, 5, 4, 1, -1 }))
				;
		}

		// A box and a tilted box crossing its edge, a kilometre and a half from the origin, where a corner of the exact CSG
		// rounds onto one line with two others: the weld flips the needle that leaves
		internal static ContentsScene NeedleOnAnEdgeFarAway()
		{
			return new ContentsScene()
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(-1f, 0f, 0f, 0f),
					new float4(1f, 0f, 0f, -3.1875f),
					new float4(0f, -1f, 0f, 0f),
					new float4(0f, 1f, 0f, -3.0625f),
					new float4(0f, 0f, -1f, 0f),
					new float4(0f, 0f, 1f, -0.9375f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(1352f, 1681f, 2700f, 1f)), name: "box"))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(-1f, 0f, 0f, -0.8772203f),
					new float4(1f, 0f, 0f, -0.8772203f),
					new float4(0f, -1f, 0f, -0.780715764f),
					new float4(0f, 1f, 0f, -0.780715764f),
					new float4(0f, 0f, -1f, -0.249651611f),
					new float4(0f, 0f, 1f, -0.249651611f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(0.9707907f, -0.231773347f, 0.0620208047f, 0f), new float4(0.2214925f, 0.9651064f, 0.139680624f, 0f), new float4(-0.09223092f, -0.121863514f, 0.988252342f, 0f), new float4(1353.72424f, 1684.14575f, 2700.78223f, 1f)), name: "tilted 0"))
				;
		}

		[Test]
		public void TheExactJudge_CountsWhatTheWeldAcrossTheModelTakesAway()
		{
			foreach (var (scene, what, flipped, split, paired) in new[]
			{
				// a seam of the sample scene, where the weld splits two triangles of other brushes
				(SampleSceneCaseTests.Model1Site4(), "the sample scene's seam", 0, 2, 0),
				(NeedleOnAnEdgeFarAway(), "the needle on an edge far away", 1, 0, 0),
				(FacingNeedlesOfAMap(), "the facing needles of a map", 0, 0, 1),
			})
			{
				using var harness = ContentsTreeHarness.Build(scene);
				Assert.That(harness.Update(), Is.True, what + ": the CSG update did not run");
				var mismatches = ExactJudge.FindMismatches(harness, 8, out var judged, checkMesh: true);
				Assert.That(judged, Is.GreaterThan(0), what + ": nothing was judged");
				Assert.That(mismatches.Count, Is.EqualTo(0), what + ": " + string.Join("; ", mismatches.Select(x => x.Format("exact"))));
				Assert.That((ExactJudge.LastFlippedByTheWeld, ExactJudge.LastSplitByTheWeld, ExactJudge.LastPairedByTheWeld),
							Is.EqualTo((flipped, split, paired)), what + ": flipped (or split within a brush), split into another brush, paired");
			}
		}

		// The weld made incrementally over a run of models, each the one before with some brushes' buffers made again;
		// after each, every brush's buffer and every move is what the full weld makes of that model
		sealed class Incremental : System.IDisposable
		{
			WeldMemory memory = WeldMemory.Create(Allocator.Persistent);
			readonly List<BlobAssetReference<ChiselBrushRenderBuffer>> owned = new();
			public int LastScope => memory.lastScope.Value;
			public ref WeldMemory Memory => ref memory;

			public BlobAssetReference<ChiselBrushRenderBuffer> Own(BlobAssetReference<ChiselBrushRenderBuffer> buffer) { owned.Add(buffer); return buffer; }

			// id + k * kGeneration: the node in id's slot k generations later (BrushSlots)
			public const int kGeneration = 1000;
			public static CompactNodeID Id(int id) => new CompactNodeID(default, new SlotIndex { index = id % kGeneration + 1, generation = id / kGeneration + 1 });

			// model: (node id, buffer) in tree order; updated: the ids whose buffers this update made again
			public void Weld(string what, (int id, BlobAssetReference<ChiselBrushRenderBuffer> buffer)[] model, params int[] updated) => Weld(what, true, model, updated);

			public void Weld(string what, bool check, (int id, BlobAssetReference<ChiselBrushRenderBuffer> buffer)[] model, params int[] updated)
			{
				using var brushes = new NativeList<BrushData>(Allocator.Temp);
				using var full    = new NativeList<BrushData>(Allocator.Temp);
				foreach (var (id, buffer) in model)
				{
					var data = new BrushData { brushIndexOrder = new IndexOrder { compactNodeID = Id(id), nodeOrder = id }, brushRenderBuffer = buffer, brushSurfaceCount = buffer.Value.surfaceCount };
					brushes.Add(data);
					full.Add(data);
				}
				var orderList = new IndexOrder[updated.Length];
				for (int i = 0; i < updated.Length; i++)
					orderList[i] = new IndexOrder { compactNodeID = Id(updated[i]), nodeOrder = updated[i] };
				using var orders = new NativeArray<IndexOrder>(orderList, Allocator.Temp);
				using var moves     = new NativeList<float3x2>(Allocator.Temp);
				using var fullMoves = new NativeList<float3x2>(Allocator.Temp);
				using var patched   = new NativeList<BlobAssetReference<ChiselBrushRenderBuffer>>(Allocator.Temp);
				OutputModelWeld.WeldIncremental(brushes, orders, ref memory, moves);
				OutputModelWeld.WeldModel(full, patched, out _, out _, out _, fullMoves);
				try
				{
					if (!check)
						return;
					for (int b = 0; b < brushes.Length; b++)
						Assert.That(OutputModelWeld.SameBuffer(brushes[b].brushRenderBuffer, full[b].brushRenderBuffer), Is.True, $"{what}: brush {model[b].id}");
					var a = new HashSet<(float3, float3)>(); foreach (var m in moves) a.Add((m.c0, m.c1));
					var c = new HashSet<(float3, float3)>(); foreach (var m in fullMoves) c.Add((m.c0, m.c1));
					Assert.That(a.SetEquals(c), Is.True, $"{what}: the moves ({moves.Length} and {fullMoves.Length})");
				}
				finally
				{
					foreach (var copy in patched) copy.Dispose();
				}
			}

			public void Dispose()
			{
				memory.Dispose();
				foreach (var buffer in owned) buffer.Dispose();
			}
		}

		static float3[] Shifted(float3[] corners, float3 by) => corners.Select(p => p + by).ToArray();

		static readonly float3 kFar = new float3(50, 0, 0);
		static float3[] FarTriangle(float y) => new[] { kFar, kFar + new float3(1, y, 0), kFar + new float3(0, 1, 0) };

		[Test]
		public void AroundANeedle_TheIncrementalWeld_IsTheFullWeld()
		{
			using var weld = new Incremental();
			var needle = weld.Own(Brush(kNeedleSurface));
			var across = weld.Own(Brush(kSurfaceAcross));
			var far = weld.Own(Brush(FarTriangle(1)));
			weld.Weld("the first update", new[] { (0, needle), (1, across), (2, far) });

			var far2 = weld.Own(Brush(FarTriangle(2)));
			weld.Weld("a triangle apart changed", new[] { (0, needle), (1, across), (2, far2) }, 2);
			Assert.That(weld.LastScope, Is.EqualTo(1), "only the triangle apart");

			var acrossMoved = weld.Own(Brush(new[] { W, U, D + new float3(0, 0, -1) }));
			weld.Weld("the surface across changed", new[] { (0, needle), (1, acrossMoved), (2, far2) }, 1);
			Assert.That(weld.LastScope, Is.EqualTo(4), "the needle's surface and the triangle across");

			var away = weld.Own(Brush(new[] { W + new float3(0, 0, 3), U + new float3(0, 0, 3), D }));
			weld.Weld("the surface across moved off the seam", new[] { (0, needle), (1, away), (2, far2) }, 1);
			weld.Weld("and back", new[] { (0, needle), (1, across), (2, far2) }, 1);
		}

		// A brush gone and a new one in its slot, put last: the new one is no brush that moved, so the weld stays with the
		// pieces of the two and the model is not welded again as a whole
		[Test]
		public void ANewBrushInTheSlotOfOneGone_IsWeldedWithoutTheWholeModel()
		{
			using var weld = new Incremental();
			var needle = weld.Own(Brush(kNeedleSurface));
			var across = weld.Own(Brush(kSurfaceAcross));
			var far    = weld.Own(Brush(FarTriangle(1)));
			var other  = weld.Own(Brush(FarTriangle(2)));
			weld.Weld("the first update", new[] { (0, needle), (1, across), (2, far) });
			var replacement = Incremental.kGeneration;
			weld.Weld("the needle gone, a new brush in its slot put last", new[] { (1, across), (2, far), (replacement, other) }, replacement);
			Assert.That(weld.LastScope, Is.Not.EqualTo(-1), "not the whole model");
		}

		[Test]
		public void AroundAShortEdge_TheIncrementalWeld_IsTheFullWeld()
		{
			using var weld = new Incremental();
			var solid = weld.Own(Brush(new[] { ShortB, ShortC, ShortD }));
			var next  = weld.Own(Brush(new[] { ShortC, ShortB, new float3(1, 0, -1) }));
			var far   = weld.Own(Brush(FarTriangle(1)));
			weld.Weld("the first update", new[] { (0, solid), (1, next), (2, far) });

			var nextMoved = weld.Own(Brush(new[] { ShortC, ShortB, new float3(1, 1, -1) }));
			weld.Weld("the brush with the other side of the short edge changed", new[] { (0, solid), (1, nextMoved), (2, far) }, 1);
			Assert.That(weld.LastScope, Is.EqualTo(1), "the changed triangle: the short edge's point group is clustered again apart");

			var nextAway = weld.Own(Brush(new[] { ShortC + new float3(0, 0, -2), ShortB + new float3(0, 0, -2), new float3(1, 1, -3) }));
			weld.Weld("moved away from the short edge", new[] { (0, solid), (1, nextAway), (2, far) }, 1);
			weld.Weld("and back", new[] { (0, solid), (1, next), (2, far) }, 1);
		}

		// A short edge of another brush joins a point group (a chain of short edges over three brushes): the cluster's corner
		// changes, so the brushes with corners in it are stored again though no triangle of theirs is welded again
		[Test]
		public void AShortEdgeJoiningAPointGroup_MovesTheCornersOfBrushesOutOfTheScope()
		{
			float3 a = new float3(1, -0.0003f, 0), b = new float3(1, 0, 0), c = new float3(1, 0.0003f, 0), e = new float3(1, 0.0006f, 0);
			using var weld = new Incremental();
			var solid = weld.Own(Brush(new[] { b, c, ShortD }, new[] { b, ShortD, new float3(0, -1, 0) }));
			var chain = weld.Own(Brush(new[] { c, e, new float3(2, 0, 1) }, new[] { e, new float3(0, 2, 0), new float3(2, 0, 1) }));
			var far   = weld.Own(Brush(FarTriangle(1)));
			var away  = weld.Own(Brush(new[] { a + new float3(0, 0, -2), b + new float3(0, 0, -2), new float3(2, -1, -1) }));
			var near  = weld.Own(Brush(new[] { a, b, new float3(2, -1, 1) }));
			weld.Weld("the first update", new[] { (0, solid), (1, chain), (2, away), (3, far) });
			weld.Weld("the short edge moved onto the group", new[] { (0, solid), (1, chain), (2, near), (3, far) }, 2);
			Assert.That(weld.LastScope, Is.EqualTo(1), "only the moved triangle");
			weld.Weld("a brush apart changed", new[] { (0, solid), (1, chain), (2, near), (3, weld.Own(Brush(FarTriangle(2)))) }, 3);
			weld.Weld("and away again", new[] { (0, solid), (1, chain), (2, away), (3, far) }, 2, 3);
		}

		// Buffers are compared byte for byte; padding in a query's entry is left as whatever was there
		[Test]
		public void AQuerySurface_HasNoPadding()
		{
			var fields = typeof(ChiselQuerySurface).GetFields(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public);
			var size = fields.Sum(f => System.Runtime.InteropServices.Marshal.SizeOf(f.FieldType));
			Assert.That(Unity.Collections.LowLevel.Unsafe.UnsafeUtility.SizeOf<ChiselQuerySurface>(), Is.EqualTo(size));
		}

		[Test]
		public void AddingRemovingAndReorderingBrushes_TheIncrementalWeld_IsTheFullWeld()
		{
			using var weld = new Incremental();
			var a = weld.Own(Brush(new[] { U, W, P }));
			var b = weld.Own(Brush(new[] { U, P, W }));
			var needle = weld.Own(Brush(Shifted(kNeedleSurface, new float3(0, 0, 10))));
			var across = weld.Own(Brush(Shifted(kSurfaceAcross, new float3(0, 0, 10))));
			weld.Weld("the first update", new[] { (0, a), (2, needle), (3, across) });
			weld.Weld("a brush added", new[] { (0, a), (1, b), (2, needle), (3, across) }, 1);
			Assert.That(weld.LastScope, Is.EqualTo(1), "only the added triangle: the one at its positions facing the other way is not welded with it");
			weld.Weld("a brush removed", new[] { (1, b), (2, needle), (3, across) });
			Assert.That(weld.LastScope, Is.EqualTo(0), "nothing: the removed triangle was welded with no other");
			weld.Weld("the needle's brush removed", new[] { (1, b), (3, across) });
			weld.Weld("and added again", new[] { (1, b), (2, needle), (3, across) }, 2);
			weld.Weld("two brushes swapped", new[] { (1, b), (3, across), (2, needle) });
			Assert.That(weld.LastScope, Is.EqualTo(-1), "another order welds everything");
		}

		// One surface with two triangles apart, each split by a needle of another brush: two pieces that both add to the
		// surface, so welded together
		[Test]
		public void TwoPiecesSplittingOneSurface_AreWeldedTogether()
		{
			var by = new float3(10, 0, 0);
			using var weld = new Incremental();
			var first  = weld.Own(Brush(kNeedleSurface));
			var second = weld.Own(Brush(Shifted(kNeedleSurface, by)));
			var across = weld.Own(Brush(kSurfaceAcross.Concat(Shifted(kSurfaceAcross, by)).ToArray()));
			weld.Weld("the first update", new[] { (0, first), (1, second), (2, across) });

			var secondAway = weld.Own(Brush(Shifted(kNeedleSurface, by + new float3(0, 0, 0.5f))));
			weld.Weld("the second needle moved off its seam", new[] { (0, first), (1, secondAway), (2, across) }, 1);
			Assert.That(weld.LastScope, Is.EqualTo(3 + 3 + 2), "both needles' surfaces and the triangles across");

			weld.Weld("and back", new[] { (0, first), (1, second), (2, across) }, 1);
			Assert.That(weld.LastScope, Is.EqualTo(3 + 3 + 2), "the first needle's piece taken in, the surface split by both");
		}

		// The hotspot cut writes each surface's group into the brush's buffer in place (OutputHotspotSections.StartCutKeys): a buffer the weld keeps carries it too
		[Test]
		public void AGroupWrittenIntoABrushsOwnBuffer_ReachesTheBufferTheWeldKept()
		{
			using var weld = new Incremental();
			var needle = weld.Own(Brush(kNeedleSurface));
			var across = weld.Own(Brush(kSurfaceAcross));
			var far = weld.Own(Brush(FarTriangle(1)));
			weld.Weld("the first update", new[] { (0, needle), (1, across), (2, far) });
			across.Value.surfaces[0].hotspot.group = 7;
			across.Value.surfaces[0].hotspot.groupVariations = 3;
			weld.Weld("the group written in place", new[] { (0, needle), (1, across), (2, far) });
		}

		// Planted: a brush's kept buffer forgotten; the verify has to see it
		[Test]
		public void AWrongKeptBuffer_IsReportedByTheVerify()
		{
			var verify = OutputModelWeld.VerifyIncremental;
			try
			{
				using var weld = new Incremental();
				var needle = weld.Own(Brush(kNeedleSurface));
				var across = weld.Own(Brush(kSurfaceAcross));
				var far = weld.Own(Brush(FarTriangle(1)));
				weld.Weld("the first update", new[] { (0, needle), (1, across), (2, far) });
				ref var memory = ref weld.Memory;
				ref var kept = ref memory.brushes.ElementAt(BrushSlots.Of(Incremental.Id(1)));
				Assert.That(kept.output.IsCreated, Is.True, "the split triangle's brush was stored again");
				kept.output.Dispose();
				kept.output = default;
				OutputModelWeld.VerifyIncremental = true;
				LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("The incremental weld differs from the full weld: 1 of 3"));
				var far2 = weld.Own(Brush(FarTriangle(2)));
				weld.Weld("a triangle apart changed", false, new[] { (0, needle), (1, across), (2, far2) }, 2);
			}
			finally
			{
				OutputModelWeld.VerifyIncremental = verify;
			}
		}
	}
}
