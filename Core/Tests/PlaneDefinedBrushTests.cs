using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace Chisel.Core.Tests
{
	[TestFixture]
	public class PlaneDefinedBrushTests
	{
		static readonly uint[] kCone =
		{
		    0x00000000, 0xBF800000, 0x80000000, 0x41AFAE14,  0x3E0C0DE9, 0x3E8234EE, 0x3F751857, 0x4202885F,
		    0x3EBBA91A, 0x80000000, 0x3F6E2F3F, 0x424041DC,  0x3F18F346, 0x3E825AF9, 0x3F42A9FD, 0x422BAA45,
		    0x3F494C29, 0x80000000, 0x3F1E298E, 0x4250FE00,  0x3F665850, 0x3E824729, 0x3EB57BE2, 0x421B81E5,
		    0x3F7D6D54, 0x80000000, 0x3E10D0C3, 0x422668BA,  0x3F751857, 0x3E8234EE, 0xBE0C0DE9, 0x41AFA1DC,
		    0x3F6E2F3F, 0x00000000, 0xBEBBA91A, 0x419B9193,  0x3F42A9EC, 0x3E825B95, 0xBF18F33A, 0xC06EF5D1,
		    0x3F1E298E, 0x00000000, 0xBF494C29, 0xC1125CF1,  0x3EB57BE2, 0x3E824729, 0xBF665850, 0xC1FB06E7,
		    0x3E10D0C3, 0x00000000, 0xBF7D6D54, 0xC20EC59D,  0xBE0C0DE9, 0x3E8234EE, 0xBF751857, 0xC253332A,
		    0xBEBBA91A, 0x80000000, 0xBF6E2F3F, 0xC254CD03,  0xBF18F346, 0x3E825AF9, 0xBF42A9FD, 0xC27C6CA1,
		    0xBF494C29, 0x80000000, 0xBF1E298E, 0xC2658C7D,  0xBF665850, 0x3E824729, 0xBEB57BE2, 0xC26C37FB,
		    0xBF7D6D54, 0x80000000, 0xBE10D0C3, 0xC23AF0CE,  0xBF751857, 0x3E8234EE, 0x3E0C0DE9, 0xC2287BB9,
		    0xBF6E2F3F, 0x00000000, 0x3EBBA91A, 0xC1C4A7E1,  0xBF42A9EC, 0x3E825B95, 0x3F18F33A, 0xC183A6A7,
		    0xBF1E298E, 0x00000000, 0x3F494C29, 0x408045F3,  0xBEB57BE2, 0x3E824729, 0x3F665850, 0x41333575,
		    0xBE10D0C3, 0x00000000, 0x3F7D6D54, 0x41F47B13,  0xBF6E2F3F, 0x80000000, 0xBEBBA91A, 0xC254CD03,
		    0xBF1E298E, 0x80000000, 0xBF494C29, 0xC2658C7D,  0xBE10D0C3, 0x80000000, 0xBF7D6D54, 0xC23AF0CE,
		    0x3EBBA91A, 0x00000000, 0xBF6E2F3F, 0xC1C4A7E1,  0x3F494C29, 0x00000000, 0xBF1E298E, 0x408045F3,
		    0x3F7D6D54, 0x00000000, 0xBE10D0C3, 0x41F47B13,  0x3F6E2F3F, 0x80000000, 0x3EBBA91A, 0x424041DC,
		    0x3F1E298E, 0x80000000, 0x3F494C29, 0x4250FE00,  0x3E10D0C3, 0x80000000, 0x3F7D6D54, 0x422668BA,
		    0xBEBBA91A, 0x00000000, 0x3F6E2F3F, 0x419B9193,  0xBF494C29, 0x00000000, 0x3F1E298E, 0xC1125CF1,
		    0xBF7D6D54, 0x00000000, 0x3E10D0C3, 0xC20EC59D,  0xBF751857, 0x3E8234EE, 0xBE0C0DE9, 0xC253332A,
		    0xBF665850, 0x3E824729, 0x3EB57BE2, 0xC1FB06E7,  0xBF18F346, 0x3E825AF9, 0x3F42A9FD, 0xC06EF375,
		    0xBE0C0DE9, 0x3E8234EE, 0x3F751857, 0x41AFA1DC,  0x3EB57BEF, 0x3E8246A3, 0x3F665861, 0x421B8211,
		    0x3F42A9F7, 0x3E825B2B, 0x3F18F342, 0x422BAA34,  0x3F75182F, 0x3E823626, 0x3E0C0DD2, 0x420287FC,
		    0x3F665850, 0x3E824729, 0xBEB57BE2, 0x41333575,  0x3F18F346, 0x3E825AF9, 0xBF42A9FD, 0xC183A664,
		    0x3E0C0DE9, 0x3E8234EE, 0xBF751857, 0xC2287BB9,  0xBEB57BE2, 0x3E824729, 0xBF665850, 0xC26C37FB,
		    0xBF42A9FD, 0x3E825AF9, 0xBF18F346, 0xC27C6CA1,
		};


		static readonly uint[] kLightBlocker1010176 =
		{
			0x80000000,0x3F800000,0x00000000,0xBF6A3D6E, 0x00000000,0xBF800000,0x00000000,0xBF6A3D73, 0xBF800000,0x00000000,0x80000000,0xBB9C2CCD,
			0x3F800000,0x00000000,0x00000000,0xBB9C251F, 0x00000000,0x00000000,0x3F800000,0xBF17D280, 0x00000000,0x00000000,0xBF800000,0xBEF2EA5C,
		};
		static readonly uint[] kLightBlocker1010176Pivot = { 0xC25B4B85, 0x41B83852, 0x414C02D8 };
		// bm_c1a3a 'Light Blocker 2244158', a slat 9.5 mm across: registration's old weld collapsed it into NaN planes
		static readonly uint[] kLightBlocker2244158 =
		{
			0x00000000,0xBF800000,0x00000000,0xBB26C429, 0x3F800000,0x00000000,0x00000000,0xBB4F347B, 0x00000000,0x80000000,0xBF800000,0xBE91BFD1,
			0x00000000,0x00000000,0x3F800000,0xBE7F0FBB, 0xBF3504F3,0x3F3504F3,0x00000000,0xBB316CC0,
		};
		static readonly uint[] kLightBlocker2244158Pivot = { 0x41815EF7, 0xBFA1EF4D, 0xC0420FB4 };
		// bm_c1a4b's cone (func_brush 3926590, solid 3926591) as the importer gives it now
		static readonly uint[] kConeAsImported =
		{
			0x00000000,0xBF800000,0x00000000,0xC0D8B5D2, 0x3E0C0DE9,0x3E8234EE,0x3F751857,0xC02D7501, 0x3EBBA91A,0x00000000,0x3F6E2F3F,0xC01FFDEE,
			0x3F18F346,0x3E825AF9,0x3F42A9FD,0xC02E097B, 0x3F494C29,0x00000000,0x3F1E298E,0xC0210292, 0x3F665850,0x3E824729,0x3EB57BE2,0xC02F5D30,
			0x3F7D6D54,0x00000000,0x3E10D0C3,0xC022A4DA, 0x3F751857,0x3E8234EE,0xBE0C0DE9,0xC0315008, 0x3F6E2F3F,0x00000000,0xBEBBA91A,0xC024F9DE,
			0x3F42A9EC,0x3E825B95,0xBF18F33A,0xC033B107, 0x3F1E298E,0x00000000,0xBF494C29,0xC02731F8, 0x3EB57BE2,0x3E824729,0xBF665850,0xC03549EE,
			0x3E10D0C3,0x00000000,0xBF7D6D54,0xC028597A, 0xBE0C0DE9,0x3E8234EE,0xBF751857,0xC035EF9C, 0xBEBBA91A,0x00000000,0xBF6E2F3F,0xC028B47C,
			0xBF18F346,0x3E825AF9,0xBF42A9FD,0xC035C2F7, 0xBF494C29,0x00000000,0xBF1E298E,0xC027E54B, 0xBF665850,0x3E824729,0xBEB57BE2,0xC034392E,
			0xBF7D6D54,0x00000000,0xBE10D0C3,0xC025DC63, 0xBF751857,0x3E8234EE,0x3E0C0DE9,0xC0321495, 0xBF6E2F3F,0x00000000,0x3EBBA91A,0xC023B88C,
			0xBF42A9EC,0x3E825B95,0x3F18F33A,0xC0301C49, 0xBF1E298E,0x00000000,0x3F494C29,0xC021B5E6, 0xBEB57BE2,0x3E824729,0x3F665850,0xC02E4C70,
			0xBE10D0C3,0x00000000,0x3F7D6D54,0xC02027C3, 0xBF6E2F3F,0x00000000,0xBEBBA91A,0xC026DC5F, 0xBF1E298E,0x00000000,0xBF494C29,0xC028725E,
			0xBE10D0C3,0x00000000,0xBF7D6D54,0xC028A2D1, 0x3EBBA91A,0x00000000,0xBF6E2F3F,0xC027F668, 0x3F494C29,0x00000000,0xBF1E298E,0xC0264D83,
			0x3F7D6D54,0x00000000,0xBE10D0C3,0xC023DB01, 0x3F6E2F3F,0x00000000,0x3EBBA91A,0xC021D60B, 0x3F1E298E,0x00000000,0x3F494C29,0xC0207580,
			0x3E10D0C3,0x00000000,0x3F7D6D54,0xC01FDE6C, 0xBEBBA91A,0x00000000,0x3F6E2F3F,0xC020BC02, 0xBF494C29,0x00000000,0x3F1E298E,0xC0229A5A,
			0xBF7D6D54,0x00000000,0x3E10D0C3,0xC024A63C, 0xBF751857,0x3E8234EE,0xBE0C0DE9,0xC0334089, 0xBF665850,0x3E824729,0x3EB57BE2,0xC0312FD0,
			0xBF18F346,0x3E825AF9,0x3F42A9FD,0xC02F401E, 0xBE0C0DE9,0x3E8234EE,0x3F751857,0xC02DBBEF, 0x3EB57BEF,0x3E8246A5,0x3F665861,0xC02D9496,
			0x3F42A9F7,0x3E825B2B,0x3F18F342,0xC02E91EB, 0x3F75182F,0x3E823626,0x3E0C0DD1,0xC0302427, 0x3F665850,0x3E824729,0xBEB57BE2,0xC032668E,
			0x3F18F346,0x3E825AF9,0xBF42A9FD,0xC0348D20, 0x3E0C0DE9,0x3E8234EE,0xBF751857,0xC035A8AE, 0xBEB57BE2,0x3E824729,0xBF665850,0xC03601C1,
			0xBF42A9FD,0x3E825AF9,0xBF18F346,0xC0353A8A,
		};
		static readonly uint[] kConeAsImportedPivot = { 0xC21C18C1, 0x41E5DB89, 0xC21BE46D };

		static float4[] Planes(uint[] bits)
		{
			var planes = new float4[bits.Length / 4];
			for (int p = 0; p < planes.Length; p++)
				planes[p] = math.asfloat(new uint4(bits[p * 4], bits[p * 4 + 1], bits[p * 4 + 2], bits[p * 4 + 3]));
			return planes;
		}

		static float4[] BoxPlanes(float3 min, float3 max)
		{
			return new[]
			{
				new float4(-1, 0, 0,  min.x), new float4(1, 0, 0, -max.x),
				new float4( 0,-1, 0,  min.y), new float4(0, 1, 0, -max.y),
				new float4( 0, 0,-1,  min.z), new float4(0, 0, 1, -max.z)
			};
		}

		static ChiselSurfaceArray SurfacesFor(int count)
		{
			var surfaceArray = new ChiselSurfaceArray();
			surfaceArray.EnsureSize(count);
			for (int i = 0; i < surfaceArray.surfaces.Length; i++)
				surfaceArray.surfaces[i] = new ChiselSurface();
			return surfaceArray;
		}

		static bool SameBits(float4 a, float4 b) => math.all(math.asuint(a) == math.asuint(b));

		// Given these planes, the definition is a valid brush of exactly expectedFaces of them, and a scene registers it
		// with exactly their planes
		static void AssertIsABrushOf(float4[] planes, int expectedFaces, string what)
		{
			var definition = new ChiselBrushDefinition();
			definition.SetPlanes(planes);
			Assert.That(definition.Validate(), Is.True, what + ": not a valid brush");
			var outline = definition.BrushOutline;
			Assert.That(outline, Is.Not.Null, what + ": no outline");
			Assert.That(outline.ValidateData(out var dataProblem), Is.True, what + ": its outline is broken: " + dataProblem);
			Assert.That(outline.polygons.Length, Is.EqualTo(expectedFaces), what + ": another number of faces");
			var seen = new HashSet<int>();
			for (int f = 0; f < outline.polygons.Length; f++)
			{
				var polygon = outline.polygons[f];
				int p = polygon.descriptionIndex;
				Assert.That(p >= 0 && p < planes.Length, Is.True, $"{what}: face {f} has no plane of the brush ({p})");
				Assert.That(seen.Add(p), Is.True, $"{what}: plane {p} has two faces");
				Assert.That(polygon.edgeCount, Is.GreaterThanOrEqualTo(3), $"{what}: face {f} (plane {p}) has {polygon.edgeCount} edges");
				Assert.That(SameBits(outline.planes[f], planes[p]), Is.True, $"{what}: face {f} has plane {outline.planes[f]}, it was given {planes[p]}");
			}
			Assert.That(definition.RequiredSurfaceCount, Is.EqualTo(planes.Length), what + ": surfaces are one per given plane");

			var brush = CSGTreeBrush.Create();
			try
			{
				BrushMeshManager.ConvertBrushMeshesToBrushMeshInstances(new List<CSGTreeBrush> { brush }, new List<BrushMesh> { outline },
																		new List<ChiselSurfaceArray> { SurfacesFor(planes.Length) });
				var blobRef = BrushMeshManager.GetBrushMeshBlob(brush.BrushMesh);
				Assert.That(blobRef.IsCreated, Is.True, what + ": a scene registers it as nothing");
				ref var blob = ref blobRef.Value;
				Assert.That(blob.localPlaneCount, Is.EqualTo(outline.polygons.Length), what + ": registered with another number of planes");
				for (int f = 0; f < outline.polygons.Length; f++)
					Assert.That(SameBits(blob.localPlanes[f], outline.planes[f]), Is.True, $"{what}: registered plane {f} is {blob.localPlanes[f]}, not {outline.planes[f]}");
			}
			finally
			{
				var registered = brush.BrushMesh;
				brush.Destroy();
				if (registered.Valid)
					registered.Destroy();
			}
		}

		[Test]
		public void TheConeOfBmC1a4b_IsABrushOfItsFortyNinePlanes()
		{
			AssertIsABrushOf(Planes(kCone), 49, "the cone of bm_c1a4b");
		}

		[Test]
		public void TheContentsHarness_BuildsABrushOfPlanesAsAScene()
		{
			var planes = Planes(kCone);
			var definition = new ChiselBrushDefinition();
			definition.SetPlanes(planes);
			Assert.That(definition.Validate(), Is.True, "the cone of bm_c1a4b is no brush");
			var outline = definition.BrushOutline;

			var cone = ContentsSceneNode.Brush(planes, name: "cone");
			using (var harness = ContentsTreeHarness.Build(new ContentsScene().Add(cone)))
			{
				var blobRef = BrushMeshManager.GetBrushMeshBlob(harness.TreeBrushOf(cone).BrushMesh);
				Assert.That(blobRef.IsCreated, Is.True, "the harness made nothing of the cone's planes");
				ref var blob = ref blobRef.Value;
				Assert.That(blob.localPlaneCount, Is.EqualTo(outline.planes.Length), "the harness made another brush of the cone's planes: faces");
				for (int f = 0; f < outline.planes.Length; f++)
					Assert.That(SameBits(blob.localPlanes[f], outline.planes[f]), Is.True, $"the harness made another brush of the cone's planes: plane {f} is {blob.localPlanes[f]}, not {outline.planes[f]}");
				Assert.That(blob.localVertices.Length, Is.EqualTo(outline.vertices.Length), "the harness made another brush of the cone's planes: vertices");
				for (int v = 0; v < outline.vertices.Length; v++)
					Assert.That(math.all(math.asuint(blob.localVertices[v]) == math.asuint(outline.vertices[v])), Is.True,
								$"the harness made another brush of the cone's planes: vertex {v} is {blob.localVertices[v]}, not {outline.vertices[v]}");

				Assert.That(harness.Update(), Is.True, "the CSG update did not run");
				Assert.That(harness.TriangleCountOf(cone), Is.GreaterThan(0), "the cone draws nothing: " + harness.LastUpdateReport);
			}
		}

		static float3 Pivot(uint[] bits) => math.asfloat(new uint3(bits[0], bits[1], bits[2]));

		// The brush a scene makes of these planes reaches the CSG where the importer puts it, and draws
		static void AssertIsABrushThatDraws(uint[] planeBits, uint[] pivotBits, int expectedFaces, string what)
		{
			var planes = Planes(planeBits);
			AssertIsABrushOf(planes, expectedFaces, what);
			var node = ContentsSceneNode.Brush(planes, localToTree: float4x4.Translate(Pivot(pivotBits)), name: what);
			using (var harness = ContentsTreeHarness.Build(new ContentsScene().Add(node)))
			{
				Assert.That(harness.Update(), Is.True, what + ": the CSG update did not run");
				Assert.That(harness.TriangleCountOf(node), Is.GreaterThan(0), what + " draws nothing: " + harness.LastUpdateReport);
			}
		}

		[Test]
		public void TheLightBlocker1010176OfBmC1a4c_IsABrushThatDraws()
		{
			AssertIsABrushThatDraws(kLightBlocker1010176, kLightBlocker1010176Pivot, 6, "Light Blocker 1010176 of bm_c1a4c");
		}

		[Test]
		public void TheLightBlocker2244158OfBmC1a3a_IsABrushThatDraws()
		{
			AssertIsABrushThatDraws(kLightBlocker2244158, kLightBlocker2244158Pivot, 5, "Light Blocker 2244158 of bm_c1a3a");
		}

		[Test]
		public void TheConeOfBmC1a4b_AsTheImporterGivesIt_IsABrushThatDraws()
		{
			AssertIsABrushThatDraws(kConeAsImported, kConeAsImportedPivot, 49, "the cone of bm_c1a4b as imported");
		}

		[Test]
		public void ABox_IsABrushOfItsSixPlanes()
		{
			AssertIsABrushOf(BoxPlanes(new float3(-1, -2, -3), new float3(4, 5, 6)), 6, "a box");
		}

		// A thin slab, 1/64 of a unit: the float cut of the importer's box loses the sides of such brushes
		[Test]
		public void AThinSlab_IsABrushOfItsSixPlanes()
		{
			AssertIsABrushOf(BoxPlanes(new float3(-40, 0, -30), new float3(40, 1f / 64, 30)), 6, "a slab 1/64 thick");
		}

		// A plane that only touches the brush (at an edge) makes no face, and its surface is not used
		[Test]
		public void APlaneThatOnlyTouches_MakesNoFace()
		{
			var planes = BoxPlanes(new float3(0, 0, 0), new float3(1, 1, 1)).ToList();
			var diagonal = math.normalize(new float3(1, 1, 0));
			planes.Add(new float4(diagonal, -math.dot(diagonal, new float3(1, 1, 0))));   // through the edge x = 1, y = 1
			var definition = new ChiselBrushDefinition();
			definition.SetPlanes(planes.ToArray());
			Assert.That(definition.Validate(), Is.True);
			Assert.That(definition.BrushOutline.polygons.Any(p => p.descriptionIndex == 6), Is.False, "the touching plane has a face");
			Assert.That(definition.BrushOutline.polygons.Length, Is.EqualTo(6));
		}

		[Test]
		public void PlanesThatDoNotCloseIt_AreNoBrush()
		{
			var definition = new ChiselBrushDefinition();
			definition.SetPlanes(BoxPlanes(new float3(0, 0, 0), new float3(1, 1, 1)).Take(5).ToArray());
			UnityEngine.TestTools.LogAssert.ignoreFailingMessages = true;
			try { Assert.That(definition.Validate(), Is.False, "five planes of a box made a brush"); }
			finally { UnityEngine.TestTools.LogAssert.ignoreFailingMessages = false; }
		}

		[Test]
		public void PlanesThatEncloseNothing_AreNoBrush()
		{
			var definition = new ChiselBrushDefinition();
			var planes = BoxPlanes(new float3(0, 0, 0), new float3(1, 1, 1));
			planes[1] = new float4(1, 0, 0, 1);     // x <= -1, while x >= 0
			definition.SetPlanes(planes);
			UnityEngine.TestTools.LogAssert.ignoreFailingMessages = true;
			try { Assert.That(definition.Validate(), Is.False, "planes that enclose nothing made a brush"); }
			finally { UnityEngine.TestTools.LogAssert.ignoreFailingMessages = false; }
		}

		// Editing the outline (the editor assigns it) makes the brush its outline from then on
		[Test]
		public void AnOutlineGivenFromOutside_EndsThePlanes()
		{
			var definition = new ChiselBrushDefinition();
			definition.SetPlanes(BoxPlanes(new float3(0, 0, 0), new float3(1, 1, 1)));
			Assert.That(definition.Validate(), Is.True);
			definition.BrushOutline = new BrushMesh(definition.BrushOutline);
			Assert.That(definition.IsGivenAsPlanes, Is.False);
			Assert.That(definition.Validate(), Is.True, "the edited outline is a valid brush");
		}

		// The same planes derive their outline once; a changed plane derives it again
		[Test]
		public void TheOutline_IsDerivedAgainOnlyWhenThePlanesChange()
		{
			var definition = new ChiselBrushDefinition();
			var planes = BoxPlanes(new float3(0, 0, 0), new float3(1, 1, 1));
			definition.SetPlanes(planes);
			Assert.That(definition.Validate(), Is.True);
			var first = definition.BrushOutline;
			Assert.That(definition.Validate(), Is.True);
			Assert.That(definition.BrushOutline, Is.SameAs(first), "unchanged planes derived their outline again");
			definition.inputPlanes[1] = new float4(1, 0, 0, -2);
			Assert.That(definition.Validate(), Is.True);
			Assert.That(definition.BrushOutline, Is.Not.SameAs(first), "a changed plane kept the old outline");
		}
	}
}
