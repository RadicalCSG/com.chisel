using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace Chisel.Core.Tests
{
	[TestFixture]
	public class BrushMeshRegistrationTests
	{
		static BrushMeshBlob.HalfEdge[] HalfEdgesOf(ref BrushMeshBlob blob)
		{
			var result = new BrushMeshBlob.HalfEdge[blob.halfEdges.Length];
			for (int e = 0; e < result.Length; e++)
				result[e] = blob.halfEdges[e];
			return result;
		}

		static ChiselSurfaceArray SurfacesFor(BrushMesh brushMesh)
		{
			var surfaceArray = new ChiselSurfaceArray();
			surfaceArray.EnsureSize(brushMesh.polygons.Length);
			for (int i = 0; i < surfaceArray.surfaces.Length; i++)
				surfaceArray.surfaces[i] = new ChiselSurface();
			return surfaceArray;
		}

		// The blob has the mesh's own vertices, bit for bit and in its order, and every half edge still points at its own.
		static void AssertAsGiven(BlobAssetReference<BrushMeshBlob> blobRef, float3[] given, BrushMesh.HalfEdge[] givenHalfEdges, string what)
		{
			Assert.That(blobRef.IsCreated, Is.True, what + ": registered as nothing");
			ref var blob = ref blobRef.Value;
			Assert.That(blob.localVertices.Length, Is.EqualTo(given.Length), what + ": the registered mesh has another number of vertices");
			for (int v = 0; v < given.Length; v++)
				Assert.That(math.all(math.asint(blob.localVertices[v]) == math.asint(given[v])), Is.True,
							$"{what}: vertex {v} registered as {blob.localVertices[v]}, given {given[v]}");
			var halfEdges = HalfEdgesOf(ref blob);
			for (int e = 0; e < halfEdges.Length; e++)
				Assert.That(halfEdges[e].vertexIndex, Is.EqualTo(givenHalfEdges[e].vertexIndex), $"{what}: half edge {e} points at another vertex");
		}

		static void AssertRegisteredAsGiven(BrushMesh brushMesh, string what)
		{
			var given = brushMesh.vertices.ToArray();
			var givenHalfEdges = brushMesh.halfEdges.ToArray();
			var instance = BrushMeshInstance.Create(brushMesh, SurfacesFor(brushMesh));
			try
			{
				AssertAsGiven(BrushMeshManager.GetBrushMeshBlob(instance), given, givenHalfEdges, what + ", registered on its own");
			}
			finally
			{
				instance.Destroy();
			}

			var brush = CSGTreeBrush.Create();
			try
			{
				BrushMeshManager.ConvertBrushMeshesToBrushMeshInstances(new List<CSGTreeBrush> { brush }, new List<BrushMesh> { brushMesh },
																		new List<ChiselSurfaceArray> { SurfacesFor(brushMesh) });
				AssertAsGiven(BrushMeshManager.GetBrushMeshBlob(brush.BrushMesh), given, givenHalfEdges, what + ", registered with a scene's brushes");
			}
			finally
			{
				var registered = brush.BrushMesh;
				brush.Destroy();
				if (registered.Valid)
					registered.Destroy();
			}
		}

		static BrushMesh BoxWithGivenPlanes(out float4[] given)
		{
			BrushMeshFactory.CreateBox(new Vector3(0, 0, 0), new Vector3(1, 1, 1), out var box);
			given = box.planes.ToArray();
			for (int v = 0; v < box.vertices.Length; v++)
				box.vertices[v] += new float3(0.001f, 0, 0);
			return box;
		}

		static void AssertPlanesAsGiven(BlobAssetReference<BrushMeshBlob> blobRef, float4[] given, string what)
		{
			Assert.That(blobRef.IsCreated, Is.True, what + ": registered as nothing");
			ref var blob = ref blobRef.Value;
			Assert.That(blob.localPlaneCount, Is.EqualTo(given.Length), what + ": another number of planes");
			for (int p = 0; p < given.Length; p++)
				Assert.That(math.all(math.asint(blob.localPlanes[p]) == math.asint(given[p])), Is.True,
							$"{what}: plane {p} registered as {blob.localPlanes[p]}, the mesh gave {given[p]}");
		}

		[Test]
		public void GivenPlanes_AreThePlanesRegistered_BothWays()
		{
			var box = BoxWithGivenPlanes(out var given);
			// the fit of the shifted outline really is another plane, or this proves nothing
			var fitted = new BrushMesh(box);
			fitted.CalculatePlanes();
			Assert.That(Enumerable.Range(0, given.Length).Any(p => !math.all(math.asint(fitted.planes[p]) == math.asint(given[p]))), Is.True,
						"the shifted outline fits the given planes anyway");

			var instance = BrushMeshInstance.Create(new BrushMesh(box), SurfacesFor(box));
			try
			{
				AssertPlanesAsGiven(BrushMeshManager.GetBrushMeshBlob(instance), given, "registered on its own");
			}
			finally
			{
				instance.Destroy();
			}

			var brush = CSGTreeBrush.Create();
			try
			{
				BrushMeshManager.ConvertBrushMeshesToBrushMeshInstances(new List<CSGTreeBrush> { brush }, new List<BrushMesh> { new BrushMesh(box) },
																		new List<ChiselSurfaceArray> { SurfacesFor(box) });
				AssertPlanesAsGiven(BrushMeshManager.GetBrushMeshBlob(brush.BrushMesh), given, "registered with a scene's brushes");
			}
			finally
			{
				var registered = brush.BrushMesh;
				brush.Destroy();
				if (registered.Valid)
					registered.Destroy();
			}
		}

		// A mesh without planes still gets them: fitted to its outline
		[Test]
		public void AMeshWithoutPlanes_GetsThemFitted()
		{
			var box = BoxWithGivenPlanes(out _);
			box.planes = null;
			var instance = BrushMeshInstance.Create(box, SurfacesFor(box));
			try
			{
				var blobRef = BrushMeshManager.GetBrushMeshBlob(instance);
				Assert.That(blobRef.IsCreated, Is.True);
				Assert.That(blobRef.Value.localPlaneCount, Is.EqualTo(6));
			}
			finally
			{
				instance.Destroy();
			}
		}

		// A ChiselBrushComponent's definition validates its outline without replacing the planes it was given
		[Test]
		public void BrushDefinitionValidate_KeepsTheGivenPlanes()
		{
			var box = BoxWithGivenPlanes(out var given);
			var definition = new ChiselBrushDefinition { brushOutline = box };
			Assert.That(definition.Validate(), Is.True, "the shifted box is a valid brush");
			for (int p = 0; p < given.Length; p++)
				Assert.That(math.all(math.asint(definition.BrushOutline.planes[p]) == math.asint(given[p])), Is.True,
							$"plane {p} became {definition.BrushOutline.planes[p]}, it was given {given[p]}");
		}

		static BrushMesh TheBoxWithAnotherPlane(out float4[] given)
		{
			var box = BoxWithGivenPlanes(out given);
			given[0].w = math.asfloat(math.asuint(given[0].w) + 1u);
			box.planes = given.ToArray();
			return box;
		}

		// Registration shares a mesh only with the same mesh: two brushes whose vertices are the same but whose planes differ
		// each get their own planes, however their hashes compare
		[Test]
		public void MeshesThatDifferOnlyInTheirPlanes_EachKeepTheirOwnPlanes_BothWays()
		{
			var first  = BoxWithGivenPlanes(out var givenFirst);
			first.planes = givenFirst.ToArray();
			var second = TheBoxWithAnotherPlane(out var givenSecond);

			var a = BrushMeshInstance.Create(new BrushMesh(first), SurfacesFor(first));
			var b = BrushMeshInstance.Create(new BrushMesh(second), SurfacesFor(second));
			try
			{
				AssertPlanesAsGiven(BrushMeshManager.GetBrushMeshBlob(a), givenFirst, "the first, registered on its own");
				AssertPlanesAsGiven(BrushMeshManager.GetBrushMeshBlob(b), givenSecond, "the second, registered on its own");
			}
			finally
			{
				a.Destroy();
				b.Destroy();
			}

			var brushes = new List<CSGTreeBrush> { CSGTreeBrush.Create(), CSGTreeBrush.Create() };
			try
			{
				BrushMeshManager.ConvertBrushMeshesToBrushMeshInstances(brushes, new List<BrushMesh> { new BrushMesh(first), new BrushMesh(second) },
																		new List<ChiselSurfaceArray> { SurfacesFor(first), SurfacesFor(second) });
				AssertPlanesAsGiven(BrushMeshManager.GetBrushMeshBlob(brushes[0].BrushMesh), givenFirst, "the first, registered with a scene's brushes");
				AssertPlanesAsGiven(BrushMeshManager.GetBrushMeshBlob(brushes[1].BrushMesh), givenSecond, "the second, registered with a scene's brushes");
			}
			finally
			{
				foreach (var brush in brushes)
				{
					var registered = brush.BrushMesh;
					brush.Destroy();
					if (registered.Valid)
						registered.Destroy();
				}
			}
		}

		// A mesh set again with other planes is registered with those, not kept as it was
		[Test]
		public void AMeshSetAgainWithOtherPlanes_IsRegisteredWithThem()
		{
			var first  = BoxWithGivenPlanes(out var givenFirst);
			first.planes = givenFirst.ToArray();
			var second = TheBoxWithAnotherPlane(out var givenSecond);
			var instance = BrushMeshInstance.Create(new BrushMesh(first), SurfacesFor(first));
			try
			{
				var before = instance.BrushMeshID;
				instance.Set(new BrushMesh(second), SurfacesFor(second));
				AssertPlanesAsGiven(BrushMeshManager.GetBrushMeshBlob(instance), givenSecond, "set again");
				Assert.That(instance.BrushMeshID, Is.Not.EqualTo(before), "the changed mesh kept its registration's id, so nothing sees the change");
			}
			finally
			{
				instance.Destroy();
			}
		}

		// The same mesh twice is still one registration
		[Test]
		public void TheSameMeshTwice_IsOneRegistration()
		{
			var box = BoxWithGivenPlanes(out var given);
			box.planes = given.ToArray();
			var a = BrushMeshInstance.Create(new BrushMesh(box), SurfacesFor(box));
			var b = BrushMeshInstance.Create(new BrushMesh(box), SurfacesFor(box));
			try
			{
				Assert.That(b.BrushMeshID, Is.EqualTo(a.BrushMeshID));
			}
			finally
			{
				a.Destroy();
				b.Destroy();
			}
		}

		[Test]
		public void TheContentsHarness_ReplaysAMeshWithTheScenesPlanes()
		{
			var box = BoxWithGivenPlanes(out var given);
			var loops = new List<int>();
			foreach (var polygon in box.polygons)
			{
				for (int e = 0; e < polygon.edgeCount; e++)
					loops.Add(box.halfEdges[polygon.firstEdge + e].vertexIndex);
				loops.Add(-1);
			}
			var node = ContentsSceneNode.Brush(given, name: "box").WithMesh(box.vertices.ToArray(), loops.ToArray());
			using (var harness = ContentsTreeHarness.Build(new ContentsScene().Add(node)))
			{
				Assert.That(harness.PlaneMismatches, Is.Empty, "the replay registered other planes than the scene's");
				AssertPlanesAsGiven(BrushMeshManager.GetBrushMeshBlob(harness.TreeBrushOf(node).BrushMesh), given, "replayed by the harness");
			}
		}

		// A box 8 mm on each side: every corner is within 1.25 cm of three others
		[Test]
		public void AnEightMillimetreBox_KeepsItsEightCorners()
		{
			BrushMeshFactory.CreateBox(new Vector3(0, 0, 0), new Vector3(0.008f, 0.008f, 0.008f), out var box);
			Assert.That(box, Is.Not.Null);
			AssertRegisteredAsGiven(box, "an 8 mm box");
		}

		// The original data: bm_c2a4g 'Solid 719030' as the scene saved it (ChiselBrushComponent.definition.brushOutline).
		[Test]
		public void BmC2a4gSolid719030_KeepsItsSixteenVertices()
		{
			var vertices = new[]
			{
				new float3(0.018382946f, 0.037841376f, 0.025479233f), new float3(0.011572085f, 0.047798112f, 0.00022935924f),
				new float3(0.018362597f, 0.03767041f, -0.02558761f), new float3(0.0323582f, 0.016956896f, -0.038097184f),
				new float3(0.046823606f, -0.0043485626f, -0.024765162f), new float3(0.05435298f, -0.015367907f, 0.000044269273f),
				new float3(0.04675661f, -0.00405226f, 0.02538101f), new float3(0.032562945f, 0.01695478f, 0.03816659f),
				new float3(-0.046486318f, -0.03621872f, 0.014354243f), new float3(-0.041082278f, -0.04393878f, 0.009641586f),
				new float3(-0.041345753f, -0.043562386f, -0.009550629f), new float3(-0.038241617f, -0.047996864f, 0.00048045514f),
				new float3(-0.051757906f, -0.028687883f, 0.009702293f), new float3(-0.051819477f, -0.028599922f, -0.009569874f),
				new float3(-0.046544276f, -0.036135927f, -0.0142104775f), new float3(-0.05440427f, -0.024907364f, 0.000018829056f)
			};
			var halfEdges = new (int vertex, int twin)[]
			{
				(6, 30), (5, 34), (4, 26), (3, 38), (2, 18), (1, 10), (0, 14), (7, 22), (15, 40), (1, 15), (2, 5), (13, 17),
				(12, 47), (0, 23), (1, 6), (15, 9), (13, 41), (2, 11), (3, 4), (14, 37), (8, 46), (7, 31), (0, 7), (12, 13),
				(10, 43), (4, 39), (5, 2), (11, 33), (9, 45), (6, 35), (7, 0), (8, 21), (11, 44), (5, 27), (6, 1), (9, 29),
				(14, 42), (3, 19), (4, 3), (10, 25), (13, 8), (14, 16), (10, 36), (11, 24), (9, 32), (8, 28), (12, 20), (15, 12)
			};
			var polygons = new (int firstEdge, int edgeCount)[] { (0, 8), (8, 4), (12, 4), (16, 4), (20, 4), (24, 4), (28, 4), (32, 4), (36, 4), (40, 8) };
			var brushMesh = new BrushMesh
			{
				vertices  = vertices,
				halfEdges = halfEdges.Select(h => new BrushMesh.HalfEdge { vertexIndex = h.vertex, twinIndex = h.twin }).ToArray(),
				polygons  = polygons.Select((p, i) => new BrushMesh.Polygon { firstEdge = p.firstEdge, edgeCount = p.edgeCount, descriptionIndex = i }).ToArray()
			};
			// what the scene's component does with its outline before registering it (as BrushMeshFactory.CreateBox does)
			brushMesh.UpdateHalfEdgePolygonIndices();
			brushMesh.CalculatePlanes();
			Assert.That(brushMesh.Validate(logErrors: true), Is.True, "the saved mesh itself is valid");
			AssertRegisteredAsGiven(brushMesh, "bm_c2a4g 'Solid 719030'");
		}
	}
}
