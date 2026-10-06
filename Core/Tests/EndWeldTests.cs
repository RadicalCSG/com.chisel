using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Unity.Mathematics;

namespace Chisel.Core.Tests
{
	[TestFixture]
	public class EndWeldTests
	{
		internal static readonly uint[] kDrainCapC3a2c =
		{
			0x00000000,0x3F800000,0x00000000,0xBFFB2847, 0x00000000,0xBF800000,0x00000000,0xBFD9529A, 0x00000000,0x00000000,0xBF800000,0xBF930CF7,
			0x00000000,0x00000000,0x3F800000,0xBFA544F4, 0xBF800000,0x00000000,0x00000000,0xBE1C291A, 0x3F800000,0x00000000,0x00000000,0xBE1C28D2,
			0xBF3504F3,0x3F3504F3,0x00000000,0xBFB19859, 0xBF3504F3,0xBF3504F3,0x00000000,0xBF99AB9E, 0xBF3504F3,0x00000000,0xBF3504F3,0xBF4FF60B,
			0xBF3504F3,0x00000000,0x3F3504F3,0xBF69B9EC,
		};
		internal static readonly uint[] kDrainCapC3a2cPivot = { 0xC1A37AE1, 0xC173ACB7, 0x42099349 };

		// bm_c3a2d's func_door 286301 'drain_cap_&i1' (solid 286302): the same instance, placed and sized a little
		// differently. Its output held 2 such triangles as well.
		static readonly uint[] kDrainCapC3a2d =
		{
			0x00000000,0x3F800000,0x00000000,0xBFA7DF3B, 0x00000000,0xBF800000,0x00000000,0xBF9072B0, 0x00000000,0x00000000,0xBF800000,0xBF88A3D0,
			0x00000000,0x00000000,0x3F800000,0xBFAFAE1C, 0xBF800000,0x00000000,0x00000000,0xBE215DC3, 0x3F800000,0x00000000,0x00000000,0xBE16F429,
			0xBF3504F3,0x3F3504F3,0x00000000,0xBF6E53C4, 0xBF3504F3,0xBF3504F3,0x00000000,0xBF4D335F, 0xBF3504F3,0x00000000,0xBF3504F3,0xBF422889,
			0xBF3504F3,0x00000000,0x3F3504F3,0xBF795E9A,
		};
		static readonly uint[] kDrainCapC3a2dPivot = { 0x41B0F0D0, 0xC0490E56, 0x41828A3D };

		static float4[] Planes(uint[] bits)
		{
			var planes = new float4[bits.Length / 4];
			for (int p = 0; p < planes.Length; p++)
				planes[p] = math.asfloat(new uint4(bits[p * 4], bits[p * 4 + 1], bits[p * 4 + 2], bits[p * 4 + 3]));
			return planes;
		}

		static float3 Pivot(uint[] bits) => math.asfloat(new uint3(bits[0], bits[1], bits[2]));

		static string Describe(ContentsTreeHarness.CapturedTriangle triangle)
		{
			return $"({triangle.a.x:R}, {triangle.a.y:R}, {triangle.a.z:R}) ({triangle.b.x:R}, {triangle.b.y:R}, {triangle.b.z:R}) ({triangle.c.x:R}, {triangle.c.y:R}, {triangle.c.z:R})";
		}

		static void AssertDrawsNoTriangleWithTwoCornersAtOnePosition(uint[] planeBits, uint[] pivotBits, string what)
		{
			var node = ContentsSceneNode.Brush(Planes(planeBits), localToTree: float4x4.Translate(Pivot(pivotBits)), name: what);
			using var harness = ContentsTreeHarness.Build(new ContentsScene().Add(node));
			harness.CheckWeldEachUpdate = false;
			Assert.That(harness.Update(), Is.True, what + ": the CSG update did not run");
			Assert.That(harness.TriangleCountOf(node), Is.GreaterThan(0), what + " draws nothing: " + harness.LastUpdateReport);

			var degenerate = harness.Triangles.Where(t => ContentsTreeHarness.TwoCornersAtOnePosition(t.a, t.b, t.c)).ToList();
			Assert.That(degenerate.Count, Is.EqualTo(0),
						$"{what}: {degenerate.Count} of its {harness.Triangles.Count} triangles have two corners at one float position, " +
						"e.g. " + string.Join("; ", degenerate.Take(3).Select(Describe)));
			Assert.That(harness.DegenerateTriangleCount, Is.EqualTo(0), what + ": the render meshes hold triangles with two corners at one float position");
			Assert.That(harness.LookupMismatches, Is.Null, what + ": the picking lookup is out of step with the triangles: " + harness.LookupMismatches);
		}

		[Test]
		public void TheDrainCapOfBmC3a2c_DrawsNoTriangleWithTwoCornersAtOneFloatPosition()
		{
			AssertDrawsNoTriangleWithTwoCornersAtOnePosition(kDrainCapC3a2c, kDrainCapC3a2cPivot, "the drain cap of bm_c3a2c");
		}

		[Test]
		public void TheDrainCapOfBmC3a2d_DrawsNoTriangleWithTwoCornersAtOneFloatPosition()
		{
			AssertDrawsNoTriangleWithTwoCornersAtOnePosition(kDrainCapC3a2d, kDrainCapC3a2dPivot, "the drain cap of bm_c3a2d");
		}

		internal static readonly uint[][] kWedgesC0a0a =
		{
			new uint[] { 0x00000000,0x3F800000,0x00000000,0xBD6A3D6D, 0x3E50C259,0xBF34D288,0x3F2D89C4,0xBD258553, 0xBF690DC7,0x00000000,0x3ED3DDFB,0xBF1B4C98,
						 0xBE4BD65D,0x00000000,0xBF7AE073,0xBD65E81F },
			new uint[] { 0x00000000,0x3F800000,0x00000000,0xBDBB6456, 0x3E507E7A,0xBF358491,0x3F2CD4A2,0xBD5D6AA7, 0x3F7C8450,0x00000000,0xBE285835,0xBF7ECB56,
						 0xBE93D5F7,0x00000000,0xBF7518A0,0xBD7B49B0, 0x3E4BDA49,0x00000000,0x3F7AE040,0xBD9E39CF },
		};
		internal static readonly uint[][] kWedgesC0a0aPivots = { new uint[] { 0xC21C1083, 0x3F8B147B, 0x4317A59F }, new uint[] { 0xC218CB74, 0x3F86B021, 0x43175846 } };
		// bm_c4a3c's Details, three brushes (solids 6202567, 6202616, 6202670): a needle 0.00004 units long
		static readonly uint[][] kDetailsC4a3c =
		{
			new uint[] { 0xBF77E394,0x379A2F2D,0xBE7FB863,0xBF0B2C7F, 0x3EC1B800,0x384AB406,0xBF6CF80A,0xBE9B303B, 0xBEC2ED09,0x00000000,0x3F6CB8A7,0xBE9B36CA,
						 0x3F3504D6,0x00000000,0x3F350511,0xBF220074, 0x00000000,0xBF800000,0x00000000,0xC0010C85, 0x00000000,0x3F800000,0x00000000,0xC04ACA85 },
			new uint[] { 0x00000000,0xBF800000,0x00000000,0xC0DCAF2F, 0xBF800000,0x00000000,0x00000000,0xBF7B9752, 0x3F76E32E,0x00000000,0x3E8763CF,0xBF9D0622,
						 0x3E01D4B8,0x00000000,0xBF7DEF1D,0xBF9B15FB, 0xBE0551F4,0x00000000,0x3F7DD22E,0xBF9B149E, 0x00000000,0x3F800000,0x00000000,0xC08C6F89 },
			new uint[] { 0xBF76E32E,0x00000000,0xBE8763CF,0xBFA7DC84, 0x3F5D4971,0xB6392C10,0x3F00B79B,0xBF705887, 0x3EC75EDF,0x00000000,0xBF6BCB48,0xBF8797BD,
						 0xBEC1AF20,0x00000000,0x3F6CF9DA,0xBFAD9F3D, 0x3F3503E9,0x00000000,0x3F3505FD,0xBF4095CB, 0x00000000,0x3F800000,0x00000000,0xC0D3EE69,
						 0x00000000,0xBF800000,0x00000000,0xC12C31C1 },
		};
		static readonly uint[][] kDetailsC4a3cPivots =
		{
			new uint[] { 0xC3114D22, 0xC370C37C, 0x41A22BBE }, new uint[] { 0xC312A321, 0xC36D689B, 0x41936AAB }, new uint[] { 0xC3102612, 0xC36FA492, 0x4197994D },
		};

		// On a seam: bm_c4a3b1's func_breakable 7273 'agrunt_elev_02a_aneurysm_break' (solids 7274, 7275, 7276)
		internal static readonly uint[][] kBreakableC4a3b1 =
		{
			new uint[] { 0x00000000,0x3F800000,0x00000000,0xBEF2EA7B, 0xBF6FB345,0x3EB3C674,0x00000000,0xBF26868C, 0x3F6FB345,0x3EB3C674,0x00000000,0xBF5333C5,
						 0x00000000,0x3EB3C674,0xBF6FB345,0xBF36C595, 0x00000000,0x3EB3C674,0x3F6FB345,0xBF42F4BD, 0x00000000,0xBF800000,0x00000000,0xBF3EDCAE },
			new uint[] { 0x00000000,0xBF800000,0x00000000,0xBEF2EA61, 0xBF6FB345,0xBEB3C674,0x00000000,0xBF268688, 0x3F6FB345,0xBEB3C674,0x00000000,0xBF5333C1,
						 0x00000000,0xBEB3C674,0xBF6FB345,0xBF42F4B6, 0x00000000,0xBEB3C674,0x3F6FB345,0xBF36C593, 0x00000000,0x3F800000,0x00000000,0xBF3EDCBB },
			new uint[] { 0xBF800000,0x00000000,0x00000000,0xBF72EA33, 0x3F800000,0x00000000,0x00000000,0xBF97D294, 0x00000000,0x00000000,0xBF800000,0xBF88A3E6,
						 0x00000000,0x00000000,0x3F800000,0xBF88A3C8, 0x00000000,0xBF800000,0x00000000,0xBF72EA45, 0x00000000,0x3F800000,0x00000000,0xBFBEDCC9 },
		};
		internal static readonly uint[][] kBreakableC4a3b1Pivots =
		{
			new uint[] { 0x4298EC72, 0x4283D72A, 0x423E37E5 }, new uint[] { 0x4298EC72, 0x4277F4DF, 0x423E6BF2 }, new uint[] { 0x4298DF6E, 0x427EBBFB, 0x423E51EC },
		};
		// On a seam: bm_c2a5g's func_occluder 5748180 (solids 5748175, 5748167, 5748166, 5748156); its needle runs from
		// x = -39.04 through x = 0 to x = 1.37e-8, coordinates 55 powers of two apart
		internal static readonly uint[][] kOccluderC2a5g =
		{
			new uint[] { 0x00000000,0x3F800000,0x00000000,0xBE3EDCBA, 0x00000000,0xBF800000,0x00000000,0xBDF2EA64, 0xBF800000,0x00000000,0x00000000,0xC00ACF14,
						 0x3F800000,0x00000000,0x00000000,0xC02D82D8, 0xBF3504F3,0x00000000,0x3F3504F3,0xC0ABC46A, 0xBF550140,0x00000000,0xBF0E00D5,0xC086BECD },
			new uint[] { 0x00000000,0x3F800000,0x00000000,0xBDF2EA73, 0x00000000,0xBF800000,0x00000000,0xBE3EDCB2, 0x3F3504F3,0x00000000,0xBF3504F3,0xC110296B,
						 0x00000000,0x00000000,0x3F800000,0xC02D82D8, 0xBE785B42,0x00000000,0xBF785B42,0xC045C9D1, 0x00000000,0x00000000,0xBF800000,0xC00ACF13 },
			new uint[] { 0x00000000,0x3F800000,0x00000000,0xBDF2EA73, 0x00000000,0xBF800000,0x00000000,0xBE3EDCB2, 0x3F550140,0x00000000,0x3F0E00D5,0xC13BAE53,
						 0x00000000,0x00000000,0xBF800000,0xC050369E, 0xBEB3C674,0x00000000,0x3F6FB345,0xC09E66E4, 0x00000000,0x00000000,0x3F800000,0xC0822222 },
			new uint[] { 0x00000000,0x3F800000,0x00000000,0xBE3EDCBA, 0x00000000,0xBF800000,0x00000000,0xBDF2EA64, 0xBF800000,0x00000000,0x00000000,0xC12D82D9,
						 0x3E785B42,0x00000000,0x3F785B42,0xC0EBA9A7, 0x3EB3C674,0x00000000,0xBF6FB345,0xC0E372EC, 0x3F800000,0x00000000,0x00000000,0xC10ACF12 },
		};
		internal static readonly uint[][] kOccluderC2a5gPivots =
		{
			new uint[] { 0xC02D82D8, 0x40A4D5E7, 0xC0FB9753 }, new uint[] { 0xC17740DA, 0x40A70123, 0xBE8ACF13 },
			new uint[] { 0xC1822222, 0x40A70123, 0xC1822222 }, new uint[] { 0xC1E1907F, 0x40A4D5E7, 0xC0FB9753 },
		};
		// On a seam between two brushes, reduced to the fewest brushes of their model that still make it: bm_c0a0a's Details,
		// solids 126268, 126269, 126303 (2 October 2026)
		internal static readonly uint[][] kSeamC0a0a =
		{
			new uint[] { 0x00000000,0x3F3504F3,0x3F3504F3,0xBF42C59C, 0x00000000,0xBF800000,0x00000000,0xBF5C2465, 0x00000000,0x00000000,0xBF800000,0xBD3EDC29,
						 0x00000000,0x00000000,0x3F800000,0xBCF2EB85, 0xBF800000,0x00000000,0x00000000,0xBE1C2885, 0x3F800000,0x00000000,0x00000000,0xBE1C2966 },
			new uint[] { 0x3F800000,0x00000000,0x00000000,0xBE2D8266, 0xBF800000,0x00000000,0x00000000,0xBE0ACF85, 0x00000000,0xBF3504F3,0xBF3504F3,0xBECD81C4,
						 0x00000000,0xBF3504F3,0x3F3504F3,0xBECA70BD, 0x00000000,0x3F800000,0x00000000,0xBD3EDC9A, 0x00000000,0xBF800000,0x00000000,0xBCF2EAA4 },
			new uint[] { 0x3F800000,0x00000000,0x00000000,0xBD9C28CD, 0xBF800000,0x00000000,0x00000000,0xBD9C291F, 0x00000000,0x3F800000,0x00000000,0xBE8F2594,
						 0x00000000,0xBF800000,0x00000000,0xBE362FBA, 0x00000000,0x00000000,0xBF800000,0xBF0ACF03, 0x00000000,0x00000000,0x3F800000,0xBF2D82E9 },
		};
		internal static readonly uint[][] kSeamC0a0aPivots =
		{
			new uint[] { 0x42697A3D, 0xC0CEB8E4, 0x4220EBE0 }, new uint[] { 0x426968E4, 0xC0AC4A86, 0x42233123 }, new uint[] { 0x4269C852, 0xC0A51B4F, 0x4222E765 },
		};
		// Two facing needles, each with the other across its long edge: bm_c4a3c's world, solids 5814417, 5814409, 5814992
		// (2 October 2026)
		internal static readonly uint[][] kFacingC4a3c =
		{
			new uint[] { 0x00000000,0x00000000,0xBF800000,0xBF8F2588, 0xBF3504F3,0x00000000,0xBF3504F3,0xBD755D16, 0x3F3504F3,0x00000000,0x3F3504F3,0xBD4452D5,
						 0x00000000,0xBF800000,0x00000000,0xC06E93F8, 0x00000000,0x3F800000,0x00000000,0xC017D26E, 0x00000000,0x00000000,0x3F800000,0xBF362FD1 },
			new uint[] { 0xBF800000,0x00000000,0x00000000,0xBFEFA952, 0xBF3504F3,0x00000000,0x3F3504F3,0xBD5CD318, 0x3F3504F3,0x00000000,0xBF3504F3,0xBD5CDCD3,
						 0x00000000,0xBF800000,0x00000000,0xC058E378, 0x00000000,0x3F800000,0x00000000,0xC02D82EE, 0x00000000,0x00000000,0x3F800000,0xBFEE93E8 },
			new uint[] { 0x00000000,0xBF800000,0x00000000,0xBDA68F0A, 0xBF800000,0x00000000,0x00000000,0xBF1C28C3, 0x00000000,0x3F800000,0x00000000,0xBD91C2E1,
						 0x3F3504F3,0x00000000,0x3F3504F3,0xBF5CD825, 0x3F3504F3,0x00000000,0xBF3504F3,0xBF5CD80F },
		};
		internal static readonly uint[][] kFacingC4a3cPivots =
		{
			new uint[] { 0xC30C1D16, 0xC3721A2B, 0x41B8A06D }, new uint[] { 0xC30CDE1E, 0xC37270ED, 0x41A0C4D6 }, new uint[] { 0xC30C4CCD, 0xC375BFA9, 0x41AFAE14 },
		};
		static readonly uint[][] kSplitTwiceC0a0a =
		{
			new uint[] { 0x00000000,0xBF800000,0x00000000,0xBD6A3D76,0xBF330AA2,0x3F361247,0x3D915B93,0xBD26966E,0xBE285835,0xB8B2DDB9,0xBF7C8450,0xBF1CC351,
						 0x3F7FFE10,0xB53266B0,0xBBFBDC43,0xBD6A8ABB },
			new uint[] { 0x00000000,0xBF800000,0x00000000,0xBE3EDCB8,0x00000000,0x3F800000,0x00000000,0xBDF2EA66,0xBE785B42,0x00000000,0xBF785B42,0xBFA05F89,
						 0x3E785B42,0x00000000,0x3F785B42,0xBFA05F7E,0x3F751896,0x00000000,0xBE93D63C,0xBD32A1EC,0xBF751896,0x00000000,0x3E93D63C,0xBD0EEE06 },
			new uint[] { 0x00000000,0xBF800000,0x00000000,0xBE3EDCB8,0x00000000,0x3F800000,0x00000000,0xBDF2EA66,0xBE785B42,0x00000000,0xBF785B42,0xBF9E4690,
						 0x00000000,0x3BCCBEFA,0x3F7FFEB8,0xBF9F312C,0x3F7EBFAA,0x00000000,0xBDCA3EA7,0xBD2EEDBC,0xBF7EBABD,0x3A22FCFF,0x3DCBC8FC,0xBD0C0614 },
		};
		static readonly uint[][] kSplitTwiceC0a0aPivots =
		{
			new uint[] { 0x428907E1, 0xC097E3D7, 0x417273BA }, new uint[] { 0x42887AA2, 0xC0963210, 0x41550773 }, new uint[] { 0x42897383, 0xC0963210, 0x417C2458 },
		};
		// bm_c0a0a's Details, solids 716835, 716836, 716837 (2 October 2026)
		static readonly uint[][] kSplitTwiceOnOneBrushC0a0a =
		{
			new uint[] { 0xBF800000,0x00000000,0x00000000,0xBD0AD0F6,0x00000000,0x00000000,0x3F800000,0xBE1C28E6,0x00000000,0x00000000,0xBF800000,0xBE1C2905,
						 0x3F800000,0x00000000,0x00000000,0xBD2D80F6,0xBF3504F3,0xBF3504F3,0x00000000,0xBE691D2E,0xBF3504F3,0x3F3504F3,0x00000000,0xBEB80977 },
			new uint[] { 0x00000000,0xBF800000,0x00000000,0xBD1C28F6,0x00000000,0x00000000,0x3F800000,0xBE1C28E6,0x00000000,0x00000000,0xBF800000,0xBE1C2905,
						 0x00000000,0x3F800000,0x00000000,0xBD1C28F5,0x3F3504F3,0xBF3504F3,0x00000000,0xBF881C30,0xBF800000,0x00000000,0x00000000,0xBF99FDC1 },
			new uint[] { 0x00000000,0x3F800000,0x00000000,0xBCF2EA5D,0x00000000,0x00000000,0x3F800000,0xBE1C28E6,0x00000000,0x00000000,0xBF800000,0xBE1C2905,
						 0x00000000,0xBF800000,0x00000000,0xBD3EDCBD,0xBF3504F3,0x3F3504F3,0x00000000,0xC0A7BDCE,0x3F3504F3,0x3F3504F3,0x00000000,0xC0D1B989 },
		};
		static readonly uint[][] kSplitTwiceOnOneBrushC0a0aPivots =
		{
			new uint[] { 0x424DDBAA, 0xBDBEDCBB, 0x408151EC }, new uint[] { 0x4247DC17, 0x3ED6B852, 0x408151EC }, new uint[] { 0x4228C444, 0xBED261D9, 0x408151EC },
		};
		static readonly uint[][] kClipsC4a3c =
		{
			new uint[] { 0x00000000,0x3F7F5974,0x3D91E9F9,0xBEBE61C2, 0x00000000,0xBF7F5974,0xBD91E9F9,0xBE7249F4, 0x00000000,0x00000000,0xBF800000,0xC088A3D5,
						 0x00000000,0x00000000,0x3F800000,0xC088A3D9, 0xBF800000,0x00000000,0x00000000,0xBF8ACF26, 0x3F800000,0x00000000,0x00000000,0xBFAD82C5 },
			new uint[] { 0x00000000,0x3F800000,0x00000000,0xBE72EDC3, 0x00000000,0xBF800000,0x00000000,0xBEBEDB0A, 0x3EA167AF,0xBF3594A5,0xBF2167AF,0xC108BEE9,
						 0x00000000,0x00000000,0x3F800000,0xC12D82DA, 0xBECFF162,0x3F69EF8E,0x00000000,0xBEFDB272, 0x3ECFF162,0xBF69EF8E,0x00000000,0xBEFDAF01 },
		};
		static readonly uint[][] kClipsC4a3cPivots =
		{
			new uint[] { 0xC2F7EE5D, 0xC38ACCA3, 0xC20D851F }, new uint[] { 0xC2F8BA3D, 0xC38A943F, 0xC249FA50 },
		};
		// solids 2695878, 2695868, 4697015 of one model: across another brush
		static readonly uint[][] kSolidsC4a3c =
		{
			new uint[] { 0x00000000,0x3F800000,0x00000000,0xBF3EDC48, 0x00000000,0xBF800000,0x00000000,0xBEF2EB48, 0xBF64F92E,0x00000000,0x3EE4F92E,0xBF2AB65E,
						 0x3F64F92E,0x00000000,0xBEE4F92E,0xBF5563F4, 0x00000000,0x00000000,0x3F800000,0xC01C28F2, 0x00000000,0x00000000,0xBF800000,0xC01C28FA },
			new uint[] { 0x00000000,0x3F800000,0x00000000,0xBF3EDC48, 0x00000000,0xBF800000,0x00000000,0xBEF2EB48, 0xBF800000,0x00000000,0x00000000,0xBF24D5AE,
						 0x3F6FB345,0x00000000,0xBEB3C674,0xBF40ED28, 0x00000000,0x00000000,0x3F800000,0xBF1C28DA, 0x00000000,0x00000000,0xBF800000,0xBF1C2912 },
			new uint[] { 0x00000000,0x3F800000,0x00000000,0xBEBEDC8F, 0x00000000,0xBF800000,0x00000000,0xBE72EAB8, 0x00000000,0x3EE4F92E,0x3F64F92E,0xBF0F8DC5,
						 0x3F800000,0x00000000,0x00000000,0xC077CBB4, 0xBF64F92E,0xA795FE1D,0x3EE4F92E,0xC02F13BA, 0x00000000,0x00000000,0xBF800000,0xBF478992 },
		};
		static readonly uint[][] kSolidsC4a3cPivots =
		{
			new uint[] { 0xC2BCD432, 0xC37903FB, 0xC251D70A }, new uint[] { 0xC2BF78E4, 0xC37903FB, 0xC25E0A3D }, new uint[] { 0xC2C880ED, 0xC378A48D, 0xC25D5CBB },
		};
		// solids 6371133, 6371134, 6371135 of one model: across another face of its own brush
		static readonly uint[][] kOneBrushC4a3c =
		{
			new uint[] { 0x00000000,0x3F800000,0x00000000,0xBE72E91F, 0x00000000,0xBF800000,0x00000000,0xBEBEDD5C, 0xBF800000,0x00000000,0x00000000,0xC0903B28,
						 0x3F230AAA,0x00000000,0xBF455DC0,0xC084D34B, 0x3EAD6CA3,0x00000000,0x3F70DDFF,0xC0DD8947, 0xBEE4F92E,0x00000000,0xBF64F92E,0xC10B6E67 },
			new uint[] { 0x3DF68C00,0xBF768C00,0x3E769ACF,0xC08E408C, 0xBF800000,0x00000000,0x00000000,0xBEB4747B, 0x3EE32B61,0x3E0048B0,0x3F632B61,0xBE8FE27F,
						 0xBEE36364,0xBDED506C,0xBF63710C,0xBE720AFB, 0x3F3504F3,0x00000000,0xBF3504F3,0xBE8481D0 },
			new uint[] { 0x3DA582CA,0x3F7F29A3,0x00000000,0xBE72201E, 0xBDA582CA,0xBF7F29A3,0x00000000,0xBEBE3C56, 0xBF800000,0x00000000,0x00000000,0xC1489F4C,
						 0x3F800000,0x00000000,0x00000000,0xC1207F6C, 0x3EE9E24E,0x00000000,0x3F63BAAA,0xC0E68F58, 0xBEF376F8,0x00000000,0xBF613472,0xC0E4011A },
		};
		static readonly uint[][] kOneBrushC4a3cPivots =
		{
			new uint[] { 0xC2A6AA62, 0xC37881D9, 0xC1B33456 }, new uint[] { 0xC2AEF9A0, 0xC3745875, 0xC1F321E3 }, new uint[] { 0xC2C3BE02, 0xC377B1A3, 0xC182F259 },
		};
		static readonly uint[][] kBevelC4a3c =
		{
			new uint[] { 0x00000000,0x3F800000,0x00000000,0xC1126668, 0x00000000,0xBF800000,0x00000000,0xC1126665, 0xBF800000,0x00000000,0x00000000,0xBE9C287B,
						 0x3F800000,0x00000000,0x00000000,0xBE9C2971, 0x00000000,0x00000000,0x3F800000,0xC1151C71, 0x3F3504F3,0x00000000,0xBF3504F3,0xC0A8B337 },
			new uint[] { 0x00000000,0x3F800000,0x00000000,0xBE9C2AE1, 0x00000000,0xBF800000,0x00000000,0xBE9C270A, 0xBF3504F3,0x00000000,0x3F3504F3,0xC0DCD800,
						 0x3F3504F3,0x00000000,0xBF3504F3,0xC0DCD7EB, 0x3F3504F3,0x00000000,0x3F3504F3,0xC0D6B579, 0xBF3504F3,0x00000000,0xBF3504F3,0xC0ABC474 },
		};
		static readonly uint[][] kBevelC4a3cPivots =
		{
			new uint[] { 0xC34248F6, 0xC3917C29, 0x41910B61 }, new uint[] { 0xC3427456, 0xC38CC1EC, 0x3F3EDCBB },
		};
		// bm_c2a5a's model 24, solids 11807, 11808, 11810: two brushes' sloped faces
		static readonly uint[][] kSlopeC2a5a =
		{
			new uint[] { 0x00000000,0x3F800000,0x00000000,0xBD26930A, 0x3EC8D3F2,0xBF5500AA,0x3EC8D3DF,0xBD2D3ADD, 0x3F351F9C,0x00000000,0xBF34EA46,0xBF9C2A92,
						 0xBF34E99C,0x00000000,0x3F352047,0xBF88A501, 0xBF3504EB,0x00000000,0xBF3504FC,0xBDBB63F8 },
			new uint[] { 0x3EC8D4C3,0xBF5500C9,0x3EC8D28C,0xBD4A208A, 0x3F351F98,0xBA5F7354,0xBF34EA42,0xBD1C387D, 0xBF3520EC,0x3A6AC1F2,0x3F34E8EC,0xBD1C43A6,
						 0xBEC8D0D7,0x3F55008B,0xBEC8D77E,0xBD9ECDA0, 0x00000000,0xBF800000,0x00000000,0xBE0ACF33, 0x00000000,0x3F800000,0x00000000,0xBE2D82B8 },
			new uint[] { 0x3F34FB9D,0x3998BC14,0xBF350E48,0xBF9C28D7, 0xBF34FC0F,0xB99C81B4,0x3F350DD6,0xBF9C296C, 0xBEC8D419,0x3F55008B,0xBEC8D43C,0xBDF56EC7,
						 0x00000000,0xBF800000,0x00000000,0xBDF2EA66, 0x00000000,0x3F800000,0x00000000,0xBE3EDCB8, 0x3F350503,0x00000000,0x3F3504E3,0xBE95A57B },
		};
		static readonly uint[][] kSlopeC2a5aPivots =
		{
			new uint[] { 0x420C22F0, 0x41DC7EA2, 0x430C2594 }, new uint[] { 0x420912E6, 0x41DCAF38, 0x430CF1DE }, new uint[] { 0x420D3517, 0x41DC8C84, 0x430C4E8A },
		};
		// bm_c0a0a's world, solids 1137293, 1137291, 1137292
		static readonly uint[][] kWorldC0a0a =
		{
			new uint[] { 0x00000000,0x3F800000,0x00000000,0xBF72EA61, 0x00000000,0xBF800000,0x00000000,0xBFBEDCBB, 0x00000000,0x00000000,0x3F800000,0xC0D1D70A,
						 0xBF800000,0x00000000,0x00000000,0xBF2ACCD4, 0x3F800000,0x00000000,0x00000000,0xBF2ACCC5, 0x00000000,0x00000000,0xBF800000,0xC0D1D70A },
			new uint[] { 0x00000000,0x3F800000,0x00000000,0xBF72EA61, 0x00000000,0xBF800000,0x00000000,0xBFBEDCBB, 0xBF3504F3,0x00000000,0x3F3504F3,0xBEFDD0C3,
						 0x3F3504F3,0x00000000,0x3F3504F3,0xBEFDD0AE, 0x00000000,0x00000000,0x3F800000,0xBD0ACAE1, 0x00000000,0x00000000,0xBF800000,0xBD2D870A },
			new uint[] { 0x00000000,0x3F800000,0x00000000,0xBF72EA61, 0x00000000,0xBF800000,0x00000000,0xBFBEDCBB, 0x00000000,0x00000000,0x3F800000,0xC0D261CA,
						 0xBF3504F3,0x00000000,0xBF3504F3,0xC094C34A, 0x3F800000,0x00000000,0x00000000,0xBD2D833D, 0xBF800000,0x00000000,0x00000000,0xBD0ACEAE },
		};
		static readonly uint[][] kWorldC0a0aPivots =
		{
			new uint[] { 0xC14E2E14, 0xC058E38E, 0x432B9000 }, new uint[] { 0xC14E2E14, 0xC058E38E, 0x4324F89B }, new uint[] { 0xC142F679, 0xC058E38E, 0x432B8BAA },
		};
		// bm_c2a5a's model 24, solids 4608, 4609, 4621, 4624
		static readonly uint[][] kFourC2a5a =
		{
			new uint[] { 0x00000000,0x3F800000,0x00000000,0xBEF2EA65, 0x00000000,0xBF800000,0x00000000,0xBF3EDCB9, 0xBF800000,0x00000000,0x00000000,0xBFAD82F3,
						 0x3F800000,0x00000000,0x00000000,0xBF8ACEF8, 0x3F3504F3,0x00000000,0x3F3504F3,0xC03B1A7B, 0x3F3504F3,0x00000000,0xBF3504F3,0xC03B1A7C },
			new uint[] { 0x00000000,0x3F800000,0x00000000,0xBEF2EA65, 0x00000000,0xBF800000,0x00000000,0xBF3EDCB9, 0x00000000,0x00000000,0x3F800000,0xBF9C28F6,
						 0x00000000,0x00000000,0xBF800000,0xBF9C28F6, 0xBF3504F3,0x00000000,0xBF3504F3,0xC02BC467, 0x3F3504F3,0x00000000,0xBF3504F3,0xC056B586 },
			new uint[] { 0x00000000,0x3F800000,0x00000000,0xBE79DB2A, 0x00000000,0xBF800000,0x00000000,0xBEBB6456, 0xBF800000,0x00000000,0x00000000,0xBD5038F6,
						 0x3F7F5673,0x00000000,0x3D9338C4,0xBD95ABBE, 0x3F7CA7A2,0x00000000,0xBE24FFC3,0xBE27BF2D },
			new uint[] { 0x00000000,0x3F800000,0x00000000,0xBE79DB2A, 0x00000000,0xBF800000,0x00000000,0xBEBB6456, 0x3D9338C4,0x00000000,0x3F7F5673,0xBE2A5076,
						 0xBED9AB7D,0x00000000,0xBF67B68D,0xBE81F7C7, 0x3EE90451,0x00000000,0xBF63F387,0xBEBB864B },
		};
		static readonly uint[][] kFourC2a5aPivots =
		{
			new uint[] { 0xC2A816C1, 0x404BE024, 0xC0AFAE14 }, new uint[] { 0xC2A0C4D6, 0x404BE024, 0xBF9C28F6 }, new uint[] { 0xC2A5D17E, 0x405A9FBE, 0xC0852EEF }, new uint[] { 0xC2A3ABAE, 0x405A9FBE, 0xC02C051F },
		};

		internal static ContentsScene SceneOf(uint[][] brushes, uint[][] pivots, string what)
		{
			var scene = new ContentsScene();
			for (int b = 0; b < brushes.Length; b++)
				scene.Add(ContentsSceneNode.Brush(Planes(brushes[b]), localToTree: float4x4.Translate(Pivot(pivots[b])), name: what + " " + b));
			return scene;
		}

		internal static List<string> FlippableNeedles(ContentsTreeHarness harness)
		{
			var found = new List<string>();
			var surfaces = harness.Triangles.GroupBy(t => (t.brushID, math.asuint(t.surfaceNormal).x, math.asuint(t.surfaceNormal).y, math.asuint(t.surfaceNormal).z, t.material, t.section));
			foreach (var surface in surfaces)
			{
				var byEdge = new Dictionary<(float3, float3), List<ContentsTreeHarness.CapturedTriangle>>();
				foreach (var t in surface)
				{
					foreach (var (from, to) in new[] { (t.a, t.b), (t.b, t.c), (t.c, t.a) })
					{
						if (!byEdge.TryGetValue((from, to), out var list))
							byEdge[(from, to)] = list = new List<ContentsTreeHarness.CapturedTriangle>();
						list.Add(t);
					}
				}
				foreach (var t in surface)
				{
					if (!ExactFloatLine.AsNeedle(t.a, t.b, t.c, out var u, out var m, out var w))
						continue;
					if (!byEdge.TryGetValue((u, w), out var neighbours))
						continue;
					foreach (var n in neighbours)
					{
						var d = !n.a.Equals(u) && !n.a.Equals(w) ? n.a : (!n.b.Equals(u) && !n.b.Equals(w) ? n.b : n.c);
						if (!ExactFloatLine.OnOneLine(u, w, d))
							found.Add(Describe(t) + " beside " + Describe(n));
					}
				}
			}
			return found;
		}

		static void AssertNoFlippableNeedle(uint[][] brushes, uint[][] pivots, string what)
		{
			using var harness = ContentsTreeHarness.Build(SceneOf(brushes, pivots, what));
			Assert.That(harness.Update(), Is.True, what + ": the CSG update did not run");
			Assert.That(harness.Triangles.Count, Is.GreaterThan(0), what + " draws nothing: " + harness.LastUpdateReport);
			var needles = FlippableNeedles(harness);
			Assert.That(needles, Is.Empty, $"{what}: {needles.Count} needle(s) with a neighbour across their long edge on their own face, " +
										   "e.g. " + string.Join("; ", needles.Take(2)));
		}

		// Every needle of the delivered meshes, however it lies
		static List<string> Needles(ContentsTreeHarness harness)
		{
			var found = new List<string>();
			foreach (var t in harness.Triangles)
			{
				if (ExactFloatLine.AsNeedle(t.a, t.b, t.c, out _, out _, out _))
					found.Add(Describe(t));
			}
			return found;
		}

		static void AssertNoNeedle(uint[][] brushes, uint[][] pivots, string what)
		{
			using var harness = ContentsTreeHarness.Build(SceneOf(brushes, pivots, what));
			Assert.That(harness.Update(), Is.True, what + ": the CSG update did not run");
			Assert.That(harness.Triangles.Count, Is.GreaterThan(0), what + " draws nothing: " + harness.LastUpdateReport);
			var needles = Needles(harness);
			Assert.That(needles, Is.Empty, $"{what}: {needles.Count} needle(s) left, e.g. " + string.Join("; ", needles.Take(2)));
		}

		// Needles in the delivered meshes with a triangle across their long edge - of any brush, its third corner off that
		// line: what the weld across the model takes away (OutputModelWeld)
		static List<string> NeedlesWithATriangleAcross(ContentsTreeHarness harness)
		{
			var byEdge = new Dictionary<(float3, float3), List<ContentsTreeHarness.CapturedTriangle>>();
			foreach (var t in harness.Triangles)
			{
				foreach (var (from, to) in new[] { (t.a, t.b), (t.b, t.c), (t.c, t.a) })
				{
					if (!byEdge.TryGetValue((from, to), out var list))
						byEdge[(from, to)] = list = new List<ContentsTreeHarness.CapturedTriangle>();
					list.Add(t);
				}
			}
			var found = new List<string>();
			foreach (var t in harness.Triangles)
			{
				if (!ExactFloatLine.AsNeedle(t.a, t.b, t.c, out var u, out var m, out var w))
					continue;
				if (!byEdge.TryGetValue((u, w), out var across))
					continue;
				foreach (var n in across)
				{
					var d = !n.a.Equals(u) && !n.a.Equals(w) ? n.a : (!n.b.Equals(u) && !n.b.Equals(w) ? n.b : n.c);
					if (!ExactFloatLine.OnOneLine(u, w, d))
						found.Add(Describe(t) + " beside " + Describe(n));
				}
			}
			return found;
		}

		[Test]
		public void TheNeedlesOfBmC4a3cAcrossALaterHalf_LeaveNoNeedle()
		{
			foreach (var (brushes, pivots, what) in new[] { (kClipsC4a3c, kClipsC4a3cPivots, "the NPC clips of bm_c4a3c"),
															 (kSolidsC4a3c, kSolidsC4a3cPivots, "the solids of bm_c4a3c"),
															 (kOneBrushC4a3c, kOneBrushC4a3cPivots, "the solids of one brush's faces of bm_c4a3c") })
			{
				using var harness = ContentsTreeHarness.Build(SceneOf(brushes, pivots, what));
				Assert.That(harness.Update(), Is.True, what + ": the CSG update did not run");
				Assert.That(harness.Triangles.Count, Is.GreaterThan(0), what + " draws nothing: " + harness.LastUpdateReport);
				var needles = NeedlesWithATriangleAcross(harness);
				Assert.That(needles, Is.Empty, $"{what}: {needles.Count} needle(s) left with a triangle across, e.g. " + string.Join("; ", needles.Take(2)));
			}
		}

		// Pairs of triangles in the delivered meshes at the same three positions, bit for bit (0 and -0 one position), wound
		// the other way round
		static List<string> OppositeTwins(ContentsTreeHarness harness)
		{
			int3 Bits(float3 p) { var i = math.asint(p); return math.select(i, int3.zero, i == new int3(int.MinValue)); }
			bool Less(int3 p, int3 q) => p.x != q.x ? p.x < q.x : (p.y != q.y ? p.y < q.y : p.z < q.z);
			(int3, int3, int3) Key(float3 a, float3 b, float3 c)
			{
				int3 A = Bits(a), B = Bits(b), C = Bits(c);
				if (Less(B, A) && Less(B, C)) return (B, C, A);
				if (Less(C, A) && Less(C, B)) return (C, A, B);
				return (A, B, C);
			}
			var keys = new HashSet<(int3, int3, int3)>();
			foreach (var t in harness.Triangles)
				keys.Add(Key(t.a, t.b, t.c));
			var found = new List<string>();
			foreach (var t in harness.Triangles)
			{
				if (keys.Contains(Key(t.a, t.c, t.b)))
					found.Add(Describe(t));
			}
			return found;
		}

		// The weld across the model drops both triangles of each such pair
		[Test]
		public void TheOppositeTwinsOfFourRealScenes_GoTogether()
		{
			foreach (var (brushes, pivots, what) in new[] {
															 (kBevelC4a3c, kBevelC4a3cPivots, "the bevel of bm_c4a3c"),
															 (kSlopeC2a5a, kSlopeC2a5aPivots, "the sloped faces of bm_c2a5a"),
															 (kWorldC0a0a, kWorldC0a0aPivots, "the world solids of bm_c0a0a"),
															 (kFourC2a5a, kFourC2a5aPivots, "the four solids of bm_c2a5a") })
			{
				using var harness = ContentsTreeHarness.Build(SceneOf(brushes, pivots, what));
				Assert.That(harness.Update(), Is.True, what + ": the CSG update did not run");
				Assert.That(harness.Triangles.Count, Is.GreaterThan(0), what + " draws nothing: " + harness.LastUpdateReport);
				var twins = OppositeTwins(harness);
				Assert.That(twins, Is.Empty, $"{what}: {twins.Count} triangle(s) left with an identical twin facing the other way, e.g. " + string.Join("; ", twins.Take(2)));
			}
		}

		[Test]
		public void TheExactJudge_FindsTheNeedleScenesRight_MeshIncluded()
		{
			foreach (var (brushes, pivots, what) in new[] { (kWedgesC0a0a, kWedgesC0a0aPivots, "the wedges of bm_c0a0a"),
															 (kDetailsC4a3c, kDetailsC4a3cPivots, "the details of bm_c4a3c"),
															 (kBreakableC4a3b1, kBreakableC4a3b1Pivots, "the breakable of bm_c4a3b1"),
															 (kOccluderC2a5g, kOccluderC2a5gPivots, "the occluder of bm_c2a5g"),
															 (kSeamC0a0a, kSeamC0a0aPivots, "the seam of bm_c0a0a"),
															 (kFacingC4a3c, kFacingC4a3cPivots, "the facing needles of bm_c4a3c"),
															 (kClipsC4a3c, kClipsC4a3cPivots, "the NPC clips of bm_c4a3c"),
															 (kSolidsC4a3c, kSolidsC4a3cPivots, "the solids of bm_c4a3c"),
															 (kOneBrushC4a3c, kOneBrushC4a3cPivots, "the solids of one brush's faces of bm_c4a3c"),
															 (kBevelC4a3c, kBevelC4a3cPivots, "the bevel of bm_c4a3c"),
															 (kSlopeC2a5a, kSlopeC2a5aPivots, "the sloped faces of bm_c2a5a"),
															 (kWorldC0a0a, kWorldC0a0aPivots, "the world solids of bm_c0a0a"),
															 (kFourC2a5a, kFourC2a5aPivots, "the four solids of bm_c2a5a") })
			{
				using var harness = ContentsTreeHarness.Build(SceneOf(brushes, pivots, what));
				Assert.That(harness.Update(), Is.True, what + ": the CSG update did not run");
				var mismatches = ExactJudge.FindMismatches(harness, 8, out var judged, checkMesh: true);
				Assert.That(judged, Is.GreaterThan(0), what + ": nothing was judged");
				Assert.That(mismatches.Count, Is.EqualTo(0), what + ": " + string.Join("; ", mismatches.Select(x => x.Format("exact"))));
			}
		}

		[Test]
		public void TheWedgesOfBmC0a0a_LeaveNoNeedleTheFlipCouldTakeAway()
		{
			AssertNoFlippableNeedle(kWedgesC0a0a, kWedgesC0a0aPivots, "the wedges of bm_c0a0a");
		}

		[Test]
		public void TheDetailsOfBmC4a3c_LeaveNoNeedleTheFlipCouldTakeAway()
		{
			AssertNoFlippableNeedle(kDetailsC4a3c, kDetailsC4a3cPivots, "the details of bm_c4a3c");
		}

		// A needle on the edge between two faces of one brush: the triangle across it, of the other face, is split at its
		// middle corner, and it goes (OutputModelWeld)
		[Test]
		public void TheBreakableOfBmC4a3b1_LeavesNoNeedle()
		{
			AssertNoNeedle(kBreakableC4a3b1, kBreakableC4a3b1Pivots, "the breakable of bm_c4a3b1");
		}

		// The same, with coordinates 55 powers of two apart
		[Test]
		public void TheOccluderOfBmC2a5g_LeavesNoNeedle()
		{
			AssertNoNeedle(kOccluderC2a5g, kOccluderC2a5gPivots, "the occluder of bm_c2a5g");
		}

		// A needle on the seam between two brushes: the other brush's triangle across it is split
		[Test]
		public void TheSeamBetweenTwoDetailBrushesOfBmC0a0a_LeavesNoNeedle()
		{
			AssertNoNeedle(kSeamC0a0a, kSeamC0a0aPivots, "the seam of bm_c0a0a");
		}

		// Two facing needles go together
		[Test]
		public void TheFacingNeedlesOfBmC4a3c_LeaveNoNeedle()
		{
			AssertNoNeedle(kFacingC4a3c, kFacingC4a3cPivots, "the facing needles of bm_c4a3c");
		}

		[Test]
		public void TheExactJudge_FollowsAHalfThatIsSplitAgain_MeshIncluded()
		{
			foreach (var (brushes, pivots, what) in new[] { (kSplitTwiceC0a0a, kSplitTwiceC0a0aPivots, "the needles of solids 716300 and 716301 of bm_c0a0a"),
															 (kSplitTwiceOnOneBrushC0a0a, kSplitTwiceOnOneBrushC0a0aPivots, "the needles of solid 716835 of bm_c0a0a") })
			{
				using var harness = ContentsTreeHarness.Build(SceneOf(brushes, pivots, what));
				Assert.That(harness.Update(), Is.True, what + ": the CSG update did not run");
				var mismatches = ExactJudge.FindMismatches(harness, 8, out var judged, checkMesh: true);
				Assert.That(judged, Is.GreaterThan(0), what + ": nothing was judged");
				Assert.That(mismatches.Count, Is.EqualTo(0), what + ": " + string.Join("; ", mismatches.Select(x => x.Format("exact"))));
			}
		}

		[Test]
		public void TheExactJudge_DoesNotLookForTheTrianglesTheWeldDrops()
		{
			var node = ContentsSceneNode.Brush(Planes(kDrainCapC3a2c), localToTree: float4x4.Translate(Pivot(kDrainCapC3a2cPivot)), name: "drain cap");
			using var harness = ContentsTreeHarness.Build(new ContentsScene().Add(node));
			Assert.That(harness.Update(), Is.True, "the CSG update did not run");
			var mismatches = ExactJudge.FindMismatches(harness, 8, out var judged, checkMesh: true);
			Assert.That(judged, Is.GreaterThan(0), "nothing was judged");
			Assert.That(mismatches.Count, Is.EqualTo(0), "the exact judge disagrees with the drain cap's mesh: " +
						string.Join("; ", mismatches.Select(m => m.Format("exact"))));
		}

		[Test]
		public void TwoBoxesSideBySide_TheColliderHasOneVertexPerPosition()
		{
			var left  = ContentsSceneNode.Brush(ContentsScene.BoxPlanes(new float3(0, 0, 0), new float3(1, 1, 1)), name: "left");
			var right = ContentsSceneNode.Brush(ContentsScene.BoxPlanes(new float3(1, 0, 0), new float3(2, 1, 1)), name: "right");
			using var harness = ContentsTreeHarness.Build(new ContentsScene().Add(left).Add(right));
			harness.CheckWeldEachUpdate = false;
			Assert.That(harness.Update(), Is.True, "the CSG update did not run");
			Assert.That(harness.ColliderArea, Is.EqualTo(10.0), "the collider is not the 2 x 1 x 1 box's surface: " + harness.LastUpdateReport);
			Assert.That(harness.ColliderPositionCount, Is.EqualTo(12), "the collider's triangles are not at the box's 12 corner positions");
			Assert.That(harness.ColliderVertexCount, Is.EqualTo(harness.ColliderPositionCount),
						$"the collider has {harness.ColliderVertexCount} vertices at {harness.ColliderPositionCount} positions");
			Assert.That(harness.ColliderDegenerateTriangleCount, Is.EqualTo(0), "the collider holds triangles with two corners at one position");
		}

		[Test]
		public void ADrainCapAndABox_EveryTriangleIsPickedAsTheBrushThatDrewIt()
		{
			var pivot = Pivot(kDrainCapC3a2cPivot);
			var cap   = ContentsSceneNode.Brush(Planes(kDrainCapC3a2c), localToTree: float4x4.Translate(pivot), name: "drain cap");
			var box   = ContentsSceneNode.Brush(ContentsScene.BoxPlanes(pivot + new float3(10, 0, 0), pivot + new float3(11, 1, 1)), name: "box");
			using var harness = ContentsTreeHarness.Build(new ContentsScene().Add(cap).Add(box));
			harness.CheckWeldEachUpdate = false;
			Assert.That(harness.Update(), Is.True, "the CSG update did not run");
			Assert.That(harness.LookupMismatches, Is.Null, "the picking lookup is out of step with the triangles: " + harness.LookupMismatches);
			Assert.That(harness.TriangleCountOf(cap), Is.GreaterThan(0), "the drain cap draws nothing: " + harness.LastUpdateReport);
			Assert.That(harness.TriangleCountOf(box), Is.EqualTo(12), "the box is not drawn as 12 triangles: " + harness.LastUpdateReport);
			var wrong = new List<string>();
			foreach (var triangle in harness.Triangles)
			{
				var drawnBy  = harness.NodeOf(triangle.brushID);
				var expected = triangle.Center.x > pivot.x + 5 ? box : cap;
				if (drawnBy != expected)
					wrong.Add($"{Describe(triangle)} is picked as {drawnBy?.name ?? "nothing"}, it lies in the {expected.name}");
			}
			Assert.That(wrong, Is.Empty, string.Join("; ", wrong.Take(5)));
		}
	}
}
