using NUnit.Framework;
using Unity.Mathematics;

namespace Chisel.Core.Tests
{
	// Spots where the exact judge disagreed with the mesh in an imported map (bm_c0a0a), each shrunk by the harvester to the
	// brushes that still show it, and copied here as the map has them
	[TestFixture]
	[Category("Contents")]
	public partial class ImportedMapCaseTests
	{
		// The contents list of the project the map was imported into: what a brush builds as depends on it
		static readonly string[] kMapContents = { ChiselContentsList.kSolidName, "Water", "Glass", "Grate" };

		// A floor slab between two walls and two thin angled posts: on the top at y -2.44 the mesh has a vertex at z 164.92877 that
		// the exact CSG puts 0.015 mm away, so they split the face differently
		static ContentsScene ModelSite1()
		{
			return new ContentsScene().WithContentsList(kMapContents)
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(1f, 0f, 0f, -1.30472231f),
					new float4(0f, -1f, 0f, -0.8895833f),
					new float4(0f, 1f, 0f, -1.39791667f),
					new float4(0f, 0f, 1f, -4.8800025f),
					new float4(-1f, 0f, 0f, -2.05027771f),
					new float4(0f, 0f, -1f, -4.87999773f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(-4.65972233f, -1.55041671f, 165.31f, 1f)), name: "Solid 293825 #252")
					.WithMesh(new float3[] { new float3(1.30472231f, -0.8895833f, -4.87999773f), new float3(1.30472231f, 1.39791667f, -4.87999773f), new float3(1.30472231f, 1.39791667f, 4.8800025f), new float3(1.30472231f, -0.8895833f, 4.8800025f), new float3(-2.05027771f, -0.8895833f, -4.87999773f), new float3(-2.05027771f, -0.8895833f, 4.8800025f), new float3(-2.05027771f, 1.39791667f, -4.87999773f), new float3(-2.05027771f, 1.39791667f, 4.8800025f) }, new int[] { 0, 1, 2, 3, -1, 4, 0, 3, 5, -1, 1, 6, 7, 2, -1, 7, 5, 3, 2, -1, 6, 4, 5, 7, -1, 4, 6, 1, 0, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 1f, 0f, -1.491111f),
					new float4(0f, -1f, 0f, -0.9488889f),
					new float4(0f, 0f, -1f, -1.52500677f),
					new float4(-1f, 0f, 0f, -10.5563889f),
					new float4(1f, 0f, 0f, -13.195487f),
					new float4(0f, 0f, 1f, -1.90624332f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(-16.5886116f, -3.931111f, 163.0225f, 1f)), name: "Solid 1137239 #627")
					.WithMesh(new float3[] { new float3(-10.5563889f, 1.491111f, 1.90624332f), new float3(13.195487f, 1.491111f, 1.90624332f), new float3(13.195487f, 1.491111f, -1.52500677f), new float3(-10.5563889f, 1.491111f, -1.52500677f), new float3(13.195487f, -0.9488889f, 1.90624332f), new float3(-10.5563889f, -0.9488889f, 1.90624332f), new float3(-10.5563889f, -0.9488889f, -1.52500677f), new float3(13.195487f, -0.9488889f, -1.52500677f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 2, 7, 6, 3, -1, 5, 0, 3, 6, -1, 1, 4, 7, 2, -1, 4, 1, 0, 5, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 1f, 0f, -1.491111f),
					new float4(0f, -1f, 0f, -0.9488889f),
					new float4(0f, 0f, 1f, -5.86278248f),
					new float4(-1f, 0f, 0f, -1.44027781f),
					new float4(1f, 0f, 0f, -1.80034709f),
					new float4(0f, 0f, -1f, -7.32846737f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(-15.4300346f, -3.931111f, 172.257217f, 1f)), name: "Solid 1174287 #830")
					.WithMesh(new float3[] { new float3(1.80034709f, 1.491111f, -7.32846737f), new float3(-1.44027781f, 1.491111f, -7.32846737f), new float3(-1.44027781f, 1.491111f, 5.86278248f), new float3(1.80034709f, 1.491111f, 5.86278248f), new float3(-1.44027781f, -0.9488889f, -7.32846737f), new float3(1.80034709f, -0.9488889f, -7.32846737f), new float3(1.80034709f, -0.9488889f, 5.86278248f), new float3(-1.44027781f, -0.9488889f, 5.86278248f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 6, 3, 2, 7, -1, 1, 4, 7, 2, -1, 5, 0, 3, 6, -1, 0, 5, 4, 1, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 1f, 0f, -0.948888838f),
					new float4(0f, -1f, 0f, -1.49111116f),
					new float4(0f, 0f, 1f, -6.574437f),
					new float4(-0.707106769f, 0f, -0.707106769f, -4.648839f),
					new float4(1f, 0f, 0f, -0.04236145f),
					new float4(-1f, 0f, 0f, -0.03388855f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(-16.912674f, -3.38888884f, 171.545563f, 1f)), name: "Solid 1137266 #1106")
					.WithMesh(new float3[] { new float3(-0.03388855f, 0.948888838f, -6.54056263f), new float3(-0.03388855f, 0.948888838f, 6.574437f), new float3(0.04236145f, 0.948888838f, 6.574437f), new float3(0.04236145f, 0.948888838f, -6.61681271f), new float3(-0.03388855f, -1.49111116f, 6.574437f), new float3(-0.03388855f, -1.49111116f, -6.54056263f), new float3(0.04236145f, -1.49111116f, -6.61681271f), new float3(0.04236145f, -1.49111116f, 6.574437f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 1, 4, 7, 2, -1, 5, 0, 3, 6, -1, 6, 3, 2, 7, -1, 0, 5, 4, 1, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 1f, 0f, -0.948888838f),
					new float4(0f, -1f, 0f, -1.49111116f),
					new float4(0f, 0f, 1f, -6.574437f),
					new float4(-0.707106769f, 0f, -0.707106769f, -4.648839f),
					new float4(1f, 0f, 0f, -0.0423614867f),
					new float4(-1f, 0f, 0f, -0.03388851f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(-12.185174f, -3.38888884f, 171.545563f, 1f)), name: "Solid 1137292 #1109")
					.WithMesh(new float3[] { new float3(-0.03388851f, 0.948888838f, -6.54056263f), new float3(-0.03388851f, 0.948888838f, 6.574437f), new float3(0.0423614867f, 0.948888838f, 6.574437f), new float3(0.0423614867f, 0.948888838f, -6.61681271f), new float3(-0.03388851f, -1.49111116f, 6.574437f), new float3(-0.03388851f, -1.49111116f, -6.54056263f), new float3(0.0423614867f, -1.49111116f, -6.61681271f), new float3(0.0423614867f, -1.49111116f, 6.574437f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 1, 4, 7, 2, -1, 5, 0, 3, 6, -1, 6, 3, 2, 7, -1, 0, 5, 4, 1, -1 }))
				;
		}

		// Two thin angled walls against a post: on the top at y -18.3 the mesh has a vertex at (101.4486, 49.7129), 3 mm from the
		// corner at x 101.45, that the exact CSG does not make
		static ContentsScene ModelSite2()
		{
			return new ContentsScene().WithContentsList(kMapContents)
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 1f, 0f, -8.065555f),
					new float4(0f, -1f, 0f, -12.6744452f),
					new float4(1f, 0f, 0f, -7.79444075f),
					new float4(-1f, 0f, 0f, -9.743059f),
					new float4(0f, 0f, 1f, -0.8387509f),
					new float4(0f, 0f, -1f, -0.8387491f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(98.3074341f, -26.3655548f, 50.55375f, 1f)), name: "Solid 155990 #205")
					.WithMesh(new float3[] { new float3(7.79444075f, 8.065555f, -0.8387491f), new float3(-9.743059f, 8.065555f, -0.8387491f), new float3(-9.743059f, 8.065555f, 0.8387509f), new float3(7.79444075f, 8.065555f, 0.8387509f), new float3(-9.743059f, -12.6744452f, -0.8387491f), new float3(7.79444075f, -12.6744452f, -0.8387491f), new float3(7.79444075f, -12.6744452f, 0.8387509f), new float3(-9.743059f, -12.6744452f, 0.8387509f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 5, 0, 3, 6, -1, 1, 4, 7, 2, -1, 2, 7, 6, 3, -1, 4, 1, 0, 5, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 1f, 0f, -12.6744452f),
					new float4(0f, -1f, 0f, -8.065555f),
					new float4(1f, 0f, 0f, -2.58403f),
					new float4(-1f, 0f, 0f, -2.06722021f),
					new float4(0f, 0f, 1f, -2.21125f),
					new float4(0f, 0f, -1f, -2.21124983f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(103.517845f, -30.9744453f, 47.50375f, 1f)), name: "Solid 156326 #209")
					.WithMesh(new float3[] { new float3(2.58403f, 12.6744452f, -2.21124983f), new float3(-2.06722021f, 12.6744452f, -2.21124983f), new float3(-2.06722021f, 12.6744452f, 2.21125f), new float3(2.58403f, 12.6744452f, 2.21125f), new float3(-2.06722021f, -8.065555f, -2.21124983f), new float3(2.58403f, -8.065555f, -2.21124983f), new float3(2.58403f, -8.065555f, 2.21125f), new float3(-2.06722021f, -8.065555f, 2.21125f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 5, 0, 3, 6, -1, 1, 4, 7, 2, -1, 2, 7, 6, 3, -1, 4, 1, 0, 5, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 1f, 0f, -0.948888f),
					new float4(0f, -1f, 0f, -1.491112f),
					new float4(0f, 0f, 1f, -0.114374086f),
					new float4(0f, 0f, -1f, -0.114375919f),
					new float4(-1f, 0f, 0f, -7.05312538f),
					new float4(0.707106769f, 0f, -0.707106769f, -3.98985052f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(95.6175f, -19.248888f, 49.5243759f, 1f)), name: "Solid 1186632 #1197")
					.WithMesh(new float3[] { new float3(5.756875f, 0.948888f, 0.114374086f), new float3(5.528125f, 0.948888f, -0.114375919f), new float3(-7.05312538f, 0.948888f, -0.114375919f), new float3(-7.05312538f, 0.948888f, 0.114374086f), new float3(5.528125f, -1.491112f, -0.114375919f), new float3(5.756875f, -1.491112f, 0.114374086f), new float3(-7.05312538f, -1.491112f, 0.114374086f), new float3(-7.05312538f, -1.491112f, -0.114375919f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 5, 0, 3, 6, -1, 1, 4, 7, 2, -1, 2, 7, 6, 3, -1, 4, 1, 0, 5, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 1f, 0f, -0.948888f),
					new float4(0f, -1f, 0f, -1.491112f),
					new float4(0f, 0f, 1f, -0.03812393f),
					new float4(0f, 0f, -1f, -0.0381260663f),
					new float4(-1f, 0f, 0f, -5.71027565f),
					new float4(0.707106769f, 0f, -0.707106769f, -5.04722261f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(94.27465f, -19.248888f, 49.6768761f, 1f)), name: "Solid 1186633 #1198")
					.WithMesh(new float3[] { new float3(7.175975f, 0.948888f, 0.03812393f), new float3(7.099725f, 0.948888f, -0.0381260663f), new float3(-5.71027565f, 0.948888f, -0.0381260663f), new float3(-5.71027565f, 0.948888f, 0.03812393f), new float3(7.099725f, -1.491112f, -0.0381260663f), new float3(7.175975f, -1.491112f, 0.03812393f), new float3(-5.71027565f, -1.491112f, 0.03812393f), new float3(-5.71027565f, -1.491112f, -0.0381260663f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 5, 0, 3, 6, -1, 1, 4, 7, 2, -1, 2, 7, 6, 3, -1, 4, 1, 0, 5, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 1f, 0f, -1.51047635f),
					new float4(0f, -1f, 0f, -0.9295237f),
					new float4(1f, 0f, 0f, -0.0367074572f),
					new float4(-1f, 0f, 0f, -0.03954254f),
					new float4(0.9308107f, 0f, 0.365501583f, -0.7247419f),
					new float4(-0.707106769f, 0f, -0.707106769f, -1.75996947f),
					new float4(-0.7070846f, 0f, 0.707128942f, -1.31533766f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(101.413918f, -19.8104763f, 47.81818f, 1f)), name: "Solid 1186449 #1202")
					.WithMesh(new float3[] { new float3(-0.03954254f, 1.51047635f, 1.82057f), new float3(0.03461319f, 1.51047635f, 1.894721f), new float3(0.0367074572f, 1.51047635f, 1.88938773f), new float3(0.0367074572f, 1.51047635f, -2.5256803f), new float3(-0.03954254f, 1.51047635f, -2.44943023f), new float3(0.03461319f, -0.9295237f, 1.894721f), new float3(-0.03954254f, -0.9295237f, 1.82057f), new float3(-0.03954254f, -0.9295237f, -2.44943023f), new float3(0.0367074572f, -0.9295237f, -2.5256803f), new float3(0.0367074572f, -0.9295237f, 1.88938773f) }, new int[] { 0, 1, 2, 3, 4, -1, 5, 6, 7, 8, 9, -1, 8, 3, 2, 9, -1, 6, 0, 4, 7, -1, 1, 5, 9, 2, -1, 7, 4, 3, 8, -1, 5, 1, 0, 6, -1 }))
				;
		}

		// An angled post between two boxes: on its side at x 30.16 the mesh has a vertex at y -0.4575 that the exact CSG does
		// not make
		static ContentsScene ModelSite3()
		{
			return new ContentsScene().WithContentsList(kMapContents)
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 1f, 0f, -0.652361155f),
					new float4(0f, -1f, 0f, -1.02513885f),
					new float4(0f, 0f, 1f, -0.648125052f),
					new float4(-1f, 0f, 0f, -2.03333354f),
					new float4(0f, 0f, -1f, -0.648125f),
					new float4(1f, 0f, 0f, -2.54166651f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(32.19021f, 0.5676389f, 3.545625f, 1f)), name: "Solid 715903 #607")
					.WithMesh(new float3[] { new float3(2.54166651f, 0.652361155f, 0.648125052f), new float3(2.54166651f, 0.652361155f, -0.648125f), new float3(-2.03333354f, 0.652361155f, -0.648125f), new float3(-2.03333354f, 0.652361155f, 0.648125052f), new float3(2.54166651f, -1.02513885f, -0.648125f), new float3(2.54166651f, -1.02513885f, 0.648125052f), new float3(-2.03333354f, -1.02513885f, 0.648125052f), new float3(-2.03333354f, -1.02513885f, -0.648125f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 5, 0, 3, 6, -1, 2, 7, 6, 3, -1, 1, 4, 7, 2, -1, 4, 1, 0, 5, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, -1f, 0f, -1.35555565f),
					new float4(-1f, 0f, 0f, -12.9624987f),
					new float4(0f, 1f, 0f, -1.08444428f),
					new float4(0f, 0f, -1f, -0.648125f),
					new float4(1f, 0f, 0f, -12.9625015f),
					new float4(0f, 0f, 1f, -0.648125052f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(43.1193733f, -3.52444434f, 3.545625f, 1f)), name: "Solid 715904 #608")
					.WithMesh(new float3[] { new float3(12.9625015f, -1.35555565f, 0.648125052f), new float3(-12.9624987f, -1.35555565f, 0.648125052f), new float3(-12.9624987f, -1.35555565f, -0.648125f), new float3(12.9625015f, -1.35555565f, -0.648125f), new float3(-12.9624987f, 1.08444428f, 0.648125052f), new float3(-12.9624987f, 1.08444428f, -0.648125f), new float3(12.9625015f, 1.08444428f, 0.648125052f), new float3(12.9625015f, 1.08444428f, -0.648125f) }, new int[] { 0, 1, 2, 3, -1, 1, 4, 5, 2, -1, 4, 6, 7, 5, -1, 7, 3, 2, 5, -1, 6, 0, 3, 7, -1, 0, 6, 4, 1, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(1f, 0f, 0f, -0.854634643f),
					new float4(-0.9828722f, 0f, -0.184288532f, -0.855806649f),
					new float4(-0.0963242f, 0f, 0.99535f, -0.340373367f),
					new float4(0.102899149f, 0f, -0.9946918f, -0.272287339f),
					new float4(0f, -1f, 0f, -0.948888838f),
					new float4(0f, 1f, 0f, -1.49111116f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(29.30224f, -1.49111116f, 3.76908f, 1f)), name: "Solid 715910 #947")
					.WithMesh(new float3[] { new float3(0.854634643f, 1.49111116f, -0.185329929f), new float3(0.854634643f, 1.49111116f, 0.42467007f), new float3(0.854634643f, -0.948888838f, 0.42467007f), new float3(0.854634643f, -0.948888838f, -0.185329929f), new float3(-0.918177843f, 1.49111116f, 0.253107578f), new float3(-0.803802848f, 1.49111116f, -0.356892437f), new float3(-0.803802848f, -0.948888838f, -0.356892437f), new float3(-0.918177843f, -0.948888838f, 0.253107578f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 1, 4, 7, 2, -1, 5, 0, 3, 6, -1, 6, 3, 2, 7, -1, 0, 5, 4, 1, -1 }))
				;
		}

		// Near (-34.77, 3.152, 150.746): the exact CSG makes a triangle the mesh does not have
		internal static ContentsScene DetailsSite1()
		{
			return new ContentsScene().WithContentsList(kMapContents)
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 1f, 0f, -0.0465972126f),
					new float4(0f, -1f, 0f, -0.0296527855f),
					new float4(1f, 0f, 0f, -1.09715319f),
					new float4(-0.9828722f, 0f, 0.184288532f, -1.37332368f),
					new float4(-0.09875158f, 0f, -0.9951121f, -0.153003111f),
					new float4(0.10104271f, 0f, 0.9948821f, -0.153010055f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(-35.8671532f, 1.17340279f, 150.932632f, 1f)), name: "Solid 504229 #1440")
					.WithMesh(new float3[] { new float3(-1.34284687f, 0.0465972126f, 0.290180057f), new float3(1.09715319f, 0.0465972126f, 0.0423675552f), new float3(1.09715319f, 0.0465972126f, -0.26263243f), new float3(-1.40003431f, 0.0465972126f, -0.0148199424f), new float3(1.09715319f, -0.0296527855f, 0.0423675552f), new float3(-1.34284687f, -0.0296527855f, 0.290180057f), new float3(-1.40003431f, -0.0296527855f, -0.0148199424f), new float3(1.09715319f, -0.0296527855f, -0.26263243f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 1, 4, 7, 2, -1, 5, 0, 3, 6, -1, 6, 3, 2, 7, -1, 0, 5, 4, 1, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 1f, 0f, -0.18638894f),
					new float4(0f, -1f, 0f, -0.11861106f),
					new float4(1f, 0f, 0f, -1.10562563f),
					new float4(-0.9701425f, 0f, 0.242535621f, -1.37415648f),
					new float4(-0.09875158f, 0f, -0.9951121f, -0.0383608267f),
					new float4(0.09950372f, 0f, 0.9950372f, -0.0383534469f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(-35.8756256f, 1.033611f, 150.818268f, 1f)), name: "Solid 504237 #1448")
					.WithMesh(new float3[] { new float3(-1.37249947f, 0.18638894f, 0.175794676f), new float3(1.10562563f, 0.18638894f, -0.0720178261f), new float3(1.10562563f, 0.18638894f, -0.14826782f), new float3(-1.391562f, 0.18638894f, 0.0995446742f), new float3(1.10562563f, -0.11861106f, -0.0720178261f), new float3(-1.37249947f, -0.11861106f, 0.175794676f), new float3(-1.391562f, -0.11861106f, 0.0995446742f), new float3(1.10562563f, -0.11861106f, -0.14826782f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 1, 4, 7, 2, -1, 5, 0, 3, 6, -1, 6, 3, 2, 7, -1, 0, 5, 4, 1, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(1f, 0f, 0f, -0.145237118f),
					new float4(-1f, 0f, 0f, -0.159762874f),
					new float4(0f, 0f, 1f, -0.185174555f),
					new float4(0f, 0f, -1f, -0.119825438f),
					new float4(0.707106769f, 0f, 0.707106769f, -0.07188567f),
					new float4(-0.707106769f, -0.707106769f, 0f, -1.561023f),
					new float4(0.707106769f, 0.707106769f, 0f, -2.53666067f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(-34.6102371f, 3.267857f, 150.789825f, 1f)), name: "Solid 321304 #4244")
					.WithMesh(new float3[] { new float3(0.145237118f, 3.442143f, -0.119825438f), new float3(0.145237118f, 3.442143f, -0.04357543f), new float3(0.145237118f, -2.352857f, -0.04357543f), new float3(0.145237118f, -2.352857f, -0.119825438f), new float3(-0.159762874f, 3.74714279f, 0.185174555f), new float3(-0.159762874f, 3.74714279f, -0.119825438f), new float3(-0.159762874f, -2.047857f, -0.119825438f), new float3(-0.159762874f, -2.047857f, 0.185174555f), new float3(-0.083512865f, 3.670893f, 0.185174555f), new float3(-0.083512865f, -2.12410712f, 0.185174555f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 8, 4, 7, 9, -1, 5, 0, 3, 6, -1, 1, 8, 9, 2, -1, 2, 9, 7, 6, 3, -1, 8, 1, 0, 5, 4, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, -1f, 0f, -0.11861106f),
					new float4(0f, 0f, 1f, -0.182153314f),
					new float4(0f, 0f, -1f, -0.122846678f),
					new float4(0f, -0.707106769f, 0.707106769f, -0.0509218574f),
					new float4(-1f, 0f, 0f, -0.09743073f),
					new float4(0.707106769f, 0.707106769f, 6.526229E-14f, -0.06290296f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(-34.67257f, 1.033611f, 150.792847f, 1f)), name: "Solid 321264 #4245")
					.WithMesh(new float3[] { new float3(0.207569271f, -0.11861106f, -0.122846678f), new float3(0.207569271f, -0.11861106f, -0.0465966761f), new float3(-0.09743073f, -0.11861106f, -0.0465966761f), new float3(-0.09743073f, -0.11861106f, -0.122846678f), new float3(-0.0211807154f, 0.11013893f, 0.182153314f), new float3(-0.09743073f, 0.18638894f, 0.182153314f), new float3(-0.09743073f, 0.11013893f, 0.182153314f), new float3(-0.09743073f, 0.18638894f, -0.122846678f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, -1, 7, 0, 3, -1, 1, 4, 6, 2, -1, 5, 7, 3, 2, 6, -1, 7, 5, 4, 1, 0, -1 }))
				;
		}

		// Near (59.602, -2.364, 83.939): the exact CSG makes a triangle the mesh does not have
		static ContentsScene DetailsSite2()
		{
			return new ContentsScene().WithContentsList(kMapContents)
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 1f, 0f, -0.381249964f),
					new float4(0f, 0f, 1f, -0.042361144f),
					new float4(0f, 0f, -1f, -0.0338888541f),
					new float4(0f, -1f, 0f, -0.381250054f),
					new float4(-1f, 0f, 0f, -3.2787497f),
					new float4(1f, 0f, 0f, -3.27875042f),
				}, contents: 2, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(60.695f, -1.9825f, 83.9470139f, 1f)), name: "Glass 356221 #2890")
					.WithMesh(new float3[] { new float3(3.27875042f, 0.381249964f, 0.042361144f), new float3(3.27875042f, 0.381249964f, -0.0338888541f), new float3(-3.2787497f, 0.381249964f, -0.0338888541f), new float3(-3.2787497f, 0.381249964f, 0.042361144f), new float3(3.27875042f, -0.381250054f, 0.042361144f), new float3(-3.2787497f, -0.381250054f, 0.042361144f), new float3(3.27875042f, -0.381250054f, -0.0338888541f), new float3(-3.2787497f, -0.381250054f, -0.0338888541f) }, new int[] { 0, 1, 2, 3, -1, 4, 0, 3, 5, -1, 1, 6, 7, 2, -1, 6, 4, 5, 7, -1, 7, 5, 3, 2, -1, 4, 6, 1, 0, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(1f, 0f, 0f, -0.0338876359f),
					new float4(0f, 0f, -1f, -0.152498171f),
					new float4(0f, 0f, 1f, -0.152501836f),
					new float4(-1f, 0f, 0f, -0.0423623659f),
					new float4(0.707106769f, -0.707106769f, 0f, -0.647001863f),
					new float4(0.707106769f, 0.707106769f, 0f, -1.01842928f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(57.3823624f, -1.48263884f, 83.95125f, 1f)), name: "Solid 356104 #2894")
					.WithMesh(new float3[] { new float3(0.0338876359f, 1.40638888f, -0.152498171f), new float3(0.0338876359f, 1.40638888f, 0.152501836f), new float3(0.0338876359f, -0.8811112f, 0.152501836f), new float3(0.0338876359f, -0.8811112f, -0.152498171f), new float3(-0.0423623659f, 1.48263884f, -0.152498171f), new float3(-0.0423623659f, -0.957361162f, -0.152498171f), new float3(-0.0423623659f, 1.48263884f, 0.152501836f), new float3(-0.0423623659f, -0.957361162f, 0.152501836f) }, new int[] { 0, 1, 2, 3, -1, 4, 0, 3, 5, -1, 1, 6, 7, 2, -1, 6, 4, 5, 7, -1, 7, 5, 3, 2, -1, 4, 6, 1, 0, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 1f, 0f, -0.03812502f),
					new float4(0f, 0f, 1f, -0.169446722f),
					new float4(0f, 0f, -1f, -0.135553285f),
					new float4(0f, -1f, 0f, -0.0381249823f),
					new float4(0.707106769f, 0.707106769f, 0f, -2.34238815f),
					new float4(-0.707106769f, 0.707106769f, 0f, -2.34838152f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(60.6992378f, -2.401875f, 83.9343f, 1f)), name: "Solid 355954 #2895")
					.WithMesh(new float3[] { new float3(-3.282988f, 0.03812502f, -0.135553285f), new float3(-3.282988f, 0.03812502f, 0.169446722f), new float3(3.274512f, 0.03812502f, 0.169446722f), new float3(3.274512f, 0.03812502f, -0.135553285f), new float3(-3.35923815f, -0.0381249823f, 0.169446722f), new float3(3.35076213f, -0.0381249823f, 0.169446722f), new float3(-3.35923815f, -0.0381249823f, -0.135553285f), new float3(3.35076213f, -0.0381249823f, -0.135553285f) }, new int[] { 0, 1, 2, 3, -1, 1, 4, 5, 2, -1, 6, 0, 3, 7, -1, 4, 6, 7, 5, -1, 5, 7, 3, 2, -1, 6, 4, 1, 0, -1 }))
				;
		}

		// Near (73.67, -10.675, 30.322): the mesh has a triangle the exact CSG does not make
		static ContentsScene DetailsSite3()
		{
			return new ContentsScene().WithContentsList(kMapContents)
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 0f, 1f, -24.73889f),
					new float4(0f, 0f, -1f, -19.791111f),
					new float4(0f, -1f, 0f, -0.118610837f),
					new float4(0f, 1f, 0f, -0.186389163f),
					new float4(0.707106769f, 0.707106769f, 0f, -0.335483849f),
					new float4(-0.707106769f, 0.707106769f, 0f, -0.359445363f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(73.78882f, -10.8613892f, 48.07986f, 1f)), name: "Solid 42396 #1772")
					.WithMesh(new float3[] { new float3(-0.321943343f, 0.186389163f, 24.73889f), new float3(-0.62694335f, -0.118610837f, 24.73889f), new float3(0.5930567f, -0.118610837f, 24.73889f), new float3(0.288056642f, 0.186389163f, 24.73889f), new float3(-0.62694335f, -0.118610837f, -19.791111f), new float3(-0.321943343f, 0.186389163f, -19.791111f), new float3(0.288056642f, 0.186389163f, -19.791111f), new float3(0.5930567f, -0.118610837f, -19.791111f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 1, 4, 7, 2, -1, 5, 0, 3, 6, -1, 6, 3, 2, 7, -1, 0, 5, 4, 1, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 0f, 1f, -0.3388916f),
					new float4(0f, 0f, -1f, -0.2711084f),
					new float4(0f, 1f, 0f, -0.186389163f),
					new float4(0f, -1f, 0f, -0.118610837f),
					new float4(-1f, 0f, 0f, -2.59249949f),
					new float4(1f, 0f, 0f, -2.59250069f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(70.8743744f, -10.8613892f, 72.47986f, 1f)), name: "Solid 399897 #3017")
					.WithMesh(new float3[] { new float3(2.59250069f, -0.118610837f, 0.3388916f), new float3(2.59250069f, 0.186389163f, 0.3388916f), new float3(-2.59249949f, 0.186389163f, 0.3388916f), new float3(-2.59249949f, -0.118610837f, 0.3388916f), new float3(2.59250069f, 0.186389163f, -0.2711084f), new float3(2.59250069f, -0.118610837f, -0.2711084f), new float3(-2.59249949f, -0.118610837f, -0.2711084f), new float3(-2.59249949f, 0.186389163f, -0.2711084f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 1, 4, 7, 2, -1, 5, 0, 3, 6, -1, 6, 3, 2, 7, -1, 0, 5, 4, 1, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 0f, 1f, -0.338888079f),
					new float4(0f, 0f, -1f, -0.2711119f),
					new float4(0f, 1f, 0f, -0.186389163f),
					new float4(0f, -1f, 0f, -0.118610837f),
					new float4(-1f, 0f, 0f, -2.59249949f),
					new float4(1f, 0f, 0f, -2.59250069f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(70.8743744f, -10.8613892f, 34.0498619f, 1f)), name: "Solid 403937 #3277")
					.WithMesh(new float3[] { new float3(2.59250069f, -0.118610837f, 0.338888079f), new float3(2.59250069f, 0.186389163f, 0.338888079f), new float3(-2.59249949f, 0.186389163f, 0.338888079f), new float3(-2.59249949f, -0.118610837f, 0.338888079f), new float3(2.59250069f, 0.186389163f, -0.2711119f), new float3(2.59250069f, -0.118610837f, -0.2711119f), new float3(-2.59249949f, -0.118610837f, -0.2711119f), new float3(-2.59249949f, 0.186389163f, -0.2711119f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 1, 4, 7, 2, -1, 5, 0, 3, 6, -1, 6, 3, 2, 7, -1, 0, 5, 4, 1, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 0f, 1f, -0.338889778f),
					new float4(0f, 0f, -1f, -0.271110237f),
					new float4(0f, 1f, 0f, -0.186389163f),
					new float4(0f, -1f, 0f, -0.118610837f),
					new float4(-1f, 0f, 0f, -2.59249949f),
					new float4(1f, 0f, 0f, -2.59250069f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(70.8743744f, -10.8613892f, 28.55986f, 1f)), name: "Solid 404568 #3341")
					.WithMesh(new float3[] { new float3(2.59250069f, -0.118610837f, 0.338889778f), new float3(2.59250069f, 0.186389163f, 0.338889778f), new float3(-2.59249949f, 0.186389163f, 0.338889778f), new float3(-2.59249949f, -0.118610837f, 0.338889778f), new float3(2.59250069f, 0.186389163f, -0.271110237f), new float3(2.59250069f, -0.118610837f, -0.271110237f), new float3(-2.59249949f, -0.118610837f, -0.271110237f), new float3(-2.59249949f, 0.186389163f, -0.271110237f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 1, 4, 7, 2, -1, 5, 0, 3, 6, -1, 6, 3, 2, 7, -1, 0, 5, 4, 1, -1 }))
				;
		}

		// Near (80.355, -0.381, 37.515): the exact CSG makes a triangle the mesh does not have
		static ContentsScene DetailsSite4()
		{
			return new ContentsScene().WithContentsList(kMapContents)
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(1f, 0f, 0f, -0.07624878f),
					new float4(-1f, 0f, 0f, -0.07625122f),
					new float4(0f, 1f, 0f, -0.038124986f),
					new float4(0f, -0.707106769f, 0.707106769f, -1.485711f),
					new float4(0f, -0.707106769f, -0.707106769f, -1.47971821f),
					new float4(0f, -1f, 0f, -0.0381250158f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(80.32938f, -0.343125f, 36.8245125f, 1f)), name: "Solid 119036 #1830")
					.WithMesh(new float3[] { new float3(0.07624878f, -0.0381250158f, 2.06298757f), new float3(0.07624878f, -0.0381250158f, -2.0545125f), new float3(0.07624878f, 0.038124986f, -2.13076258f), new float3(0.07624878f, 0.038124986f, 2.13923764f), new float3(-0.07625122f, -0.0381250158f, -2.0545125f), new float3(-0.07625122f, -0.0381250158f, 2.06298757f), new float3(-0.07625122f, 0.038124986f, 2.13923764f), new float3(-0.07625122f, 0.038124986f, -2.13076258f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 2, 7, 6, 3, -1, 5, 0, 3, 6, -1, 1, 4, 7, 2, -1, 4, 1, 0, 5, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(1f, 0f, 0f, -0.03812378f),
					new float4(-1f, 0f, 0f, -0.03812622f),
					new float4(0f, 0f, 1f, -2.28750157f),
					new float4(0f, 0f, -1f, -1.82999849f),
					new float4(0f, -1f, 0f, -0.6523611f),
					new float4(0f, 1f, 0f, -1.02513885f),
				}, contents: 2, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(80.32938f, -1.40638888f, 36.6f, 1f)), name: "Glass 119037 #1831")
					.WithMesh(new float3[] { new float3(0.03812378f, 1.02513885f, -1.82999849f), new float3(0.03812378f, 1.02513885f, 2.28750157f), new float3(0.03812378f, -0.6523611f, 2.28750157f), new float3(0.03812378f, -0.6523611f, -1.82999849f), new float3(-0.03812622f, 1.02513885f, 2.28750157f), new float3(-0.03812622f, 1.02513885f, -1.82999849f), new float3(-0.03812622f, -0.6523611f, -1.82999849f), new float3(-0.03812622f, -0.6523611f, 2.28750157f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 1, 4, 7, 2, -1, 5, 0, 3, 6, -1, 6, 3, 2, 7, -1, 0, 5, 4, 1, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(1f, 0f, 0f, -0.06778015f),
					new float4(-1f, 0f, 0f, -0.08471985f),
					new float4(0f, 0f, 1f, -0.033887025f),
					new float4(0f, -0.707106769f, -0.707106769f, -0.485253364f),
					new float4(0f, 0.707106769f, -0.707106769f, -0.760828555f),
					new float4(0f, 0f, -1f, -0.0423629768f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(80.3378448f, -1.41486108f, 38.929863f, 1f)), name: "Solid 119039 #1833")
					.WithMesh(new float3[] { new float3(0.06778015f, -0.64388895f, -0.0423629768f), new float3(0.06778015f, 1.033611f, -0.0423629768f), new float3(0.06778015f, 1.10986114f, 0.033887025f), new float3(0.06778015f, -0.7201389f, 0.033887025f), new float3(-0.08471985f, 1.033611f, -0.0423629768f), new float3(-0.08471985f, -0.64388895f, -0.0423629768f), new float3(-0.08471985f, -0.7201389f, 0.033887025f), new float3(-0.08471985f, 1.10986114f, 0.033887025f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 2, 7, 6, 3, -1, 5, 0, 3, 6, -1, 1, 4, 7, 2, -1, 4, 1, 0, 5, -1 }))
				;
		}

		// Near (58.471, -5.414, 54.29): the exact CSG makes a triangle the mesh does not have
		static ContentsScene DetailsSite5()
		{
			return new ContentsScene().WithContentsList(kMapContents)
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(-1f, 0f, 0f, -0.07625031f),
					new float4(1f, 0f, 0f, -0.0762497f),
					new float4(0f, 1f, 0f, -0.038125135f),
					new float4(0f, -0.707106769f, -0.707106769f, -0.6529944f),
					new float4(0f, -0.707106769f, 0.707106769f, -1.018429f),
					new float4(0f, -1f, 0f, -0.0381248668f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(58.4456253f, -5.375625f, 53.65035f, 1f)), name: "Solid 126334 #3381")
					.WithMesh(new float3[] { new float3(-0.07625031f, -0.0381248668f, -0.8853487f), new float3(-0.07625031f, -0.0381248668f, 1.40215135f), new float3(-0.07625031f, 0.038125135f, 1.4784013f), new float3(-0.07625031f, 0.038125135f, -0.9615987f), new float3(0.0762497f, -0.0381248668f, 1.40215135f), new float3(0.0762497f, -0.0381248668f, -0.8853487f), new float3(0.0762497f, 0.038125135f, -0.9615987f), new float3(0.0762497f, 0.038125135f, 1.4784013f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 2, 7, 6, 3, -1, 5, 0, 3, 6, -1, 1, 4, 7, 2, -1, 4, 1, 0, 5, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(-1f, 0f, 0f, -0.0381253064f),
					new float4(1f, 0f, 0f, -0.0381246954f),
					new float4(0f, 0f, -1f, -1.01666582f),
					new float4(0f, 0f, 1f, -1.27083421f),
					new float4(0f, -1f, 0f, -0.474444419f),
					new float4(0f, 1f, 0f, -0.7455556f),
				}, contents: 2, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(58.4456253f, -6.15930557f, 53.7816658f, 1f)), name: "Glass 126336 #3383")
					.WithMesh(new float3[] { new float3(-0.0381253064f, 0.7455556f, 1.27083421f), new float3(-0.0381253064f, 0.7455556f, -1.01666582f), new float3(-0.0381253064f, -0.474444419f, -1.01666582f), new float3(-0.0381253064f, -0.474444419f, 1.27083421f), new float3(0.0381246954f, 0.7455556f, -1.01666582f), new float3(0.0381246954f, 0.7455556f, 1.27083421f), new float3(0.0381246954f, -0.474444419f, 1.27083421f), new float3(0.0381246954f, -0.474444419f, -1.01666582f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 1, 4, 7, 2, -1, 5, 0, 3, 6, -1, 6, 3, 2, 7, -1, 0, 5, 4, 1, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(-1f, 0f, 0f, -0.08472275f),
					new float4(1f, 0f, 0f, -0.06777725f),
					new float4(0f, 0f, 1f, -0.0338899232f),
					new float4(0f, -0.707106769f, -0.707106769f, -0.359445035f),
					new float4(0f, 0.707106769f, -0.707106769f, -0.5631314f),
					new float4(0f, 0f, -1f, -0.04236008f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(58.4540977f, -6.167778f, 55.09486f, 1f)), name: "Solid 126339 #3386")
					.WithMesh(new float3[] { new float3(-0.08472275f, 0.754027963f, -0.04236008f), new float3(-0.08472275f, -0.465971977f, -0.04236008f), new float3(-0.08472275f, -0.542221963f, 0.0338899232f), new float3(-0.08472275f, 0.830278f, 0.0338899232f), new float3(0.06777725f, -0.465971977f, -0.04236008f), new float3(0.06777725f, 0.754027963f, -0.04236008f), new float3(0.06777725f, 0.830278f, 0.0338899232f), new float3(0.06777725f, -0.542221963f, 0.0338899232f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 6, 3, 2, 7, -1, 1, 4, 7, 2, -1, 5, 0, 3, 6, -1, 0, 5, 4, 1, -1 }))
				;
		}

		// Near (58.458, -5.414, 70.76): the exact CSG makes a triangle the mesh does not have
		static ContentsScene DetailsSite6()
		{
			return new ContentsScene().WithContentsList(kMapContents)
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(-1f, 0f, 0f, -0.07625031f),
					new float4(1f, 0f, 0f, -0.0762497f),
					new float4(0f, 1f, 0f, -0.038125135f),
					new float4(0f, -0.707106769f, -0.707106769f, -0.652992547f),
					new float4(0f, -0.707106769f, 0.707106769f, -1.018431f),
					new float4(0f, -1f, 0f, -0.0381248668f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(58.4456253f, -5.375625f, 70.1203461f, 1f)), name: "Solid 127862 #3374")
					.WithMesh(new float3[] { new float3(-0.07625031f, -0.0381248668f, -0.885346055f), new float3(-0.07625031f, -0.0381248668f, 1.402154f), new float3(-0.07625031f, 0.038125135f, 1.478404f), new float3(-0.07625031f, 0.038125135f, -0.9615961f), new float3(0.0762497f, -0.0381248668f, 1.402154f), new float3(0.0762497f, -0.0381248668f, -0.885346055f), new float3(0.0762497f, 0.038125135f, -0.9615961f), new float3(0.0762497f, 0.038125135f, 1.478404f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 2, 7, 6, 3, -1, 5, 0, 3, 6, -1, 1, 4, 7, 2, -1, 4, 1, 0, 5, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(-1f, 0f, 0f, -0.0381253064f),
					new float4(1f, 0f, 0f, -0.0381246954f),
					new float4(0f, 0f, -1f, -1.01666319f),
					new float4(0f, 0f, 1f, -1.27083683f),
					new float4(0f, -1f, 0f, -0.474444419f),
					new float4(0f, 1f, 0f, -0.7455556f),
				}, contents: 2, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(58.4456253f, -6.15930557f, 70.25166f, 1f)), name: "Glass 127864 #3376")
					.WithMesh(new float3[] { new float3(-0.0381253064f, 0.7455556f, 1.27083683f), new float3(-0.0381253064f, 0.7455556f, -1.01666319f), new float3(-0.0381253064f, -0.474444419f, -1.01666319f), new float3(-0.0381253064f, -0.474444419f, 1.27083683f), new float3(0.0381246954f, 0.7455556f, -1.01666319f), new float3(0.0381246954f, 0.7455556f, 1.27083683f), new float3(0.0381246954f, -0.474444419f, 1.27083683f), new float3(0.0381246954f, -0.474444419f, -1.01666319f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 1, 4, 7, 2, -1, 5, 0, 3, 6, -1, 6, 3, 2, 7, -1, 0, 5, 4, 1, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(-1f, 0f, 0f, -0.08472275f),
					new float4(1f, 0f, 0f, -0.06777725f),
					new float4(0f, 0f, 1f, -0.033892516f),
					new float4(0f, -0.707106769f, -0.707106769f, -0.359443218f),
					new float4(0f, 0.707106769f, -0.707106769f, -0.5631296f),
					new float4(0f, 0f, -1f, -0.042357482f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(58.4540977f, -6.167778f, 71.56486f, 1f)), name: "Solid 127867 #3379")
					.WithMesh(new float3[] { new float3(-0.08472275f, 0.7540281f, -0.042357482f), new float3(-0.08472275f, -0.465972f, -0.042357482f), new float3(-0.08472275f, -0.542222f, 0.033892516f), new float3(-0.08472275f, 0.830278039f, 0.033892516f), new float3(0.06777725f, -0.465972f, -0.042357482f), new float3(0.06777725f, 0.7540281f, -0.042357482f), new float3(0.06777725f, 0.830278039f, 0.033892516f), new float3(0.06777725f, -0.542222f, 0.033892516f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 6, 3, 2, 7, -1, 1, 4, 7, 2, -1, 5, 0, 3, 6, -1, 0, 5, 4, 1, -1 }))
				;
		}

		// Near (89.772, -10.141, 59.475): the exact CSG makes a triangle the mesh does not have
		static ContentsScene DetailsSite7()
		{
			return new ContentsScene().WithContentsList(kMapContents)
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(1f, 0f, 0f, -0.07624695f),
					new float4(-1f, 0f, 0f, -0.07625305f),
					new float4(0f, 1f, 0f, -0.0381246172f),
					new float4(0f, -0.707106769f, 0.707106769f, -1.48570907f),
					new float4(0f, -0.707106769f, -0.707106769f, -1.47972047f),
					new float4(0f, -1f, 0f, -0.03812538f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(89.78438f, -10.1031246f, 58.7845154f, 1f)), name: "Solid 149619 #1866")
					.WithMesh(new float3[] { new float3(0.07624695f, -0.03812538f, 2.06298447f), new float3(0.07624695f, -0.03812538f, -2.05451536f), new float3(0.07624695f, 0.0381246172f, -2.13076544f), new float3(0.07624695f, 0.0381246172f, 2.13923454f), new float3(-0.07625305f, -0.03812538f, -2.05451536f), new float3(-0.07625305f, -0.03812538f, 2.06298447f), new float3(-0.07625305f, 0.0381246172f, 2.13923454f), new float3(-0.07625305f, 0.0381246172f, -2.13076544f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 2, 7, 6, 3, -1, 5, 0, 3, 6, -1, 1, 4, 7, 2, -1, 4, 1, 0, 5, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(1f, 0f, 0f, -0.03812195f),
					new float4(-1f, 0f, 0f, -0.0381280519f),
					new float4(0f, 0f, 1f, -2.28749871f),
					new float4(0f, 0f, -1f, -1.83000135f),
					new float4(0f, -1f, 0f, -0.6523615f),
					new float4(0f, 1f, 0f, -1.0251385f),
				}, contents: 2, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(89.78438f, -11.1663885f, 58.56f, 1f)), name: "Glass 149620 #1867")
					.WithMesh(new float3[] { new float3(0.03812195f, 1.0251385f, -1.83000135f), new float3(0.03812195f, 1.0251385f, 2.28749871f), new float3(0.03812195f, -0.6523615f, 2.28749871f), new float3(0.03812195f, -0.6523615f, -1.83000135f), new float3(-0.0381280519f, 1.0251385f, 2.28749871f), new float3(-0.0381280519f, 1.0251385f, -1.83000135f), new float3(-0.0381280519f, -0.6523615f, -1.83000135f), new float3(-0.0381280519f, -0.6523615f, 2.28749871f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 1, 4, 7, 2, -1, 5, 0, 3, 6, -1, 6, 3, 2, 7, -1, 0, 5, 4, 1, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(1f, 0f, 0f, -0.06777832f),
					new float4(-1f, 0f, 0f, -0.08472168f),
					new float4(0f, 0f, 1f, -0.0338879377f),
					new float4(0f, -0.707106769f, -0.707106769f, -0.4852528f),
					new float4(0f, 0.707106769f, -0.707106769f, -0.760827839f),
					new float4(0f, 0f, -1f, -0.04236206f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(89.79285f, -11.174861f, 60.8898621f, 1f)), name: "Solid 149622 #1869")
					.WithMesh(new float3[] { new float3(0.06777832f, -0.643889f, -0.04236206f), new float3(0.06777832f, 1.033611f, -0.04236206f), new float3(0.06777832f, 1.109861f, 0.0338879377f), new float3(0.06777832f, -0.720139f, 0.0338879377f), new float3(-0.08472168f, 1.033611f, -0.04236206f), new float3(-0.08472168f, -0.643889f, -0.04236206f), new float3(-0.08472168f, -0.720139f, 0.0338879377f), new float3(-0.08472168f, 1.109861f, 0.0338879377f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 2, 7, 6, 3, -1, 5, 0, 3, 6, -1, 1, 4, 7, 2, -1, 4, 1, 0, 5, -1 }))
				;
		}

		// Near (89.772, -10.141, 53.985): the exact CSG makes a triangle the mesh does not have
		static ContentsScene DetailsSite8()
		{
			return new ContentsScene().WithContentsList(kMapContents)
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(1f, 0f, 0f, -0.07624695f),
					new float4(-1f, 0f, 0f, -0.07625305f),
					new float4(0f, 1f, 0f, -0.0381246172f),
					new float4(0f, -0.707106769f, 0.707106769f, -1.48571026f),
					new float4(0f, -0.707106769f, -0.707106769f, -1.47971928f),
					new float4(0f, -1f, 0f, -0.03812538f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(89.78438f, -10.1031246f, 53.2945137f, 1f)), name: "Solid 149639 #1871")
					.WithMesh(new float3[] { new float3(0.07624695f, -0.03812538f, 2.06298637f), new float3(0.07624695f, -0.03812538f, -2.05451369f), new float3(0.07624695f, 0.0381246172f, -2.13076377f), new float3(0.07624695f, 0.0381246172f, 2.13923621f), new float3(-0.07625305f, -0.03812538f, -2.05451369f), new float3(-0.07625305f, -0.03812538f, 2.06298637f), new float3(-0.07625305f, 0.0381246172f, 2.13923621f), new float3(-0.07625305f, 0.0381246172f, -2.13076377f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 2, 7, 6, 3, -1, 5, 0, 3, 6, -1, 1, 4, 7, 2, -1, 4, 1, 0, 5, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(1f, 0f, 0f, -0.03812195f),
					new float4(-1f, 0f, 0f, -0.0381280519f),
					new float4(0f, 0f, 1f, -2.28750038f),
					new float4(0f, 0f, -1f, -1.82999969f),
					new float4(0f, -1f, 0f, -0.6523615f),
					new float4(0f, 1f, 0f, -1.0251385f),
				}, contents: 2, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(89.78438f, -11.1663885f, 53.07f, 1f)), name: "Glass 149640 #1872")
					.WithMesh(new float3[] { new float3(0.03812195f, 1.0251385f, -1.82999969f), new float3(0.03812195f, 1.0251385f, 2.28750038f), new float3(0.03812195f, -0.6523615f, 2.28750038f), new float3(0.03812195f, -0.6523615f, -1.82999969f), new float3(-0.0381280519f, 1.0251385f, 2.28750038f), new float3(-0.0381280519f, 1.0251385f, -1.82999969f), new float3(-0.0381280519f, -0.6523615f, -1.82999969f), new float3(-0.0381280519f, -0.6523615f, 2.28750038f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 1, 4, 7, 2, -1, 5, 0, 3, 6, -1, 6, 3, 2, 7, -1, 0, 5, 4, 1, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(1f, 0f, 0f, -0.06777832f),
					new float4(-1f, 0f, 0f, -0.08472168f),
					new float4(0f, 0f, 1f, -0.0338896178f),
					new float4(0f, -0.707106769f, -0.707106769f, -0.485251635f),
					new float4(0f, 0.707106769f, -0.707106769f, -0.760826647f),
					new float4(0f, 0f, -1f, -0.04236038f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(89.79285f, -11.174861f, 55.39986f, 1f)), name: "Solid 149642 #1874")
					.WithMesh(new float3[] { new float3(0.06777832f, -0.64388907f, -0.04236038f), new float3(0.06777832f, 1.033611f, -0.04236038f), new float3(0.06777832f, 1.109861f, 0.0338896178f), new float3(0.06777832f, -0.7201391f, 0.0338896178f), new float3(-0.08472168f, 1.033611f, -0.04236038f), new float3(-0.08472168f, -0.64388907f, -0.04236038f), new float3(-0.08472168f, -0.7201391f, 0.0338896178f), new float3(-0.08472168f, 1.109861f, 0.0338896178f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 2, 7, 6, 3, -1, 5, 0, 3, 6, -1, 1, 4, 7, 2, -1, 4, 1, 0, 5, -1 }))
				;
		}

		// Near (89.772, -10.141, 48.495): the exact CSG makes a triangle the mesh does not have
		static ContentsScene DetailsSite9()
		{
			return new ContentsScene().WithContentsList(kMapContents)
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(1f, 0f, 0f, -0.07624695f),
					new float4(-1f, 0f, 0f, -0.07625305f),
					new float4(0f, 1f, 0f, -0.0381246172f),
					new float4(0f, -0.707106769f, 0.707106769f, -1.48571146f),
					new float4(0f, -0.707106769f, -0.707106769f, -1.47971809f),
					new float4(0f, -1f, 0f, -0.03812538f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(89.78438f, -10.1031246f, 47.804512f, 1f)), name: "Solid 149649 #1876")
					.WithMesh(new float3[] { new float3(0.07624695f, -0.03812538f, 2.062988f), new float3(0.07624695f, -0.03812538f, -2.054512f), new float3(0.07624695f, 0.0381246172f, -2.130762f), new float3(0.07624695f, 0.0381246172f, 2.13923788f), new float3(-0.07625305f, -0.03812538f, -2.054512f), new float3(-0.07625305f, -0.03812538f, 2.062988f), new float3(-0.07625305f, 0.0381246172f, 2.13923788f), new float3(-0.07625305f, 0.0381246172f, -2.130762f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 2, 7, 6, 3, -1, 5, 0, 3, 6, -1, 1, 4, 7, 2, -1, 4, 1, 0, 5, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(1f, 0f, 0f, -0.03812195f),
					new float4(-1f, 0f, 0f, -0.0381280519f),
					new float4(0f, 0f, 1f, -2.28749824f),
					new float4(0f, 0f, -1f, -1.83000183f),
					new float4(0f, -1f, 0f, -0.6523615f),
					new float4(0f, 1f, 0f, -1.0251385f),
				}, contents: 2, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(89.78438f, -11.1663885f, 47.58f, 1f)), name: "Glass 149650 #1877")
					.WithMesh(new float3[] { new float3(0.03812195f, 1.0251385f, -1.83000183f), new float3(0.03812195f, 1.0251385f, 2.28749824f), new float3(0.03812195f, -0.6523615f, 2.28749824f), new float3(0.03812195f, -0.6523615f, -1.83000183f), new float3(-0.0381280519f, 1.0251385f, 2.28749824f), new float3(-0.0381280519f, 1.0251385f, -1.83000183f), new float3(-0.0381280519f, -0.6523615f, -1.83000183f), new float3(-0.0381280519f, -0.6523615f, 2.28749824f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 1, 4, 7, 2, -1, 5, 0, 3, 6, -1, 6, 3, 2, 7, -1, 0, 5, 4, 1, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(1f, 0f, 0f, -0.06777832f),
					new float4(-1f, 0f, 0f, -0.08472168f),
					new float4(0f, 0f, 1f, -0.0338874832f),
					new float4(0f, -0.707106769f, -0.707106769f, -0.485253125f),
					new float4(0f, 0.707106769f, -0.707106769f, -0.760828137f),
					new float4(0f, 0f, -1f, -0.04236252f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(89.79285f, -11.174861f, 49.9098625f, 1f)), name: "Solid 149652 #1879")
					.WithMesh(new float3[] { new float3(0.06777832f, -0.64388907f, -0.04236252f), new float3(0.06777832f, 1.03361094f, -0.04236252f), new float3(0.06777832f, 1.109861f, 0.0338874832f), new float3(0.06777832f, -0.720139f, 0.0338874832f), new float3(-0.08472168f, 1.03361094f, -0.04236252f), new float3(-0.08472168f, -0.64388907f, -0.04236252f), new float3(-0.08472168f, -0.720139f, 0.0338874832f), new float3(-0.08472168f, 1.109861f, 0.0338874832f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 2, 7, 6, 3, -1, 5, 0, 3, 6, -1, 1, 4, 7, 2, -1, 4, 1, 0, 5, -1 }))
				;
		}

		// Near (89.81, -10.141, 43.005): the exact CSG makes a triangle the mesh does not have
		static ContentsScene DetailsSite10()
		{
			return new ContentsScene().WithContentsList(kMapContents)
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(1f, 0f, 0f, -0.07624695f),
					new float4(-1f, 0f, 0f, -0.07625305f),
					new float4(0f, 1f, 0f, -0.0381246172f),
					new float4(0f, -0.707106769f, 0.707106769f, -1.48571f),
					new float4(0f, -0.707106769f, -0.707106769f, -1.47971964f),
					new float4(0f, -1f, 0f, -0.03812538f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(89.78438f, -10.1031246f, 42.3145142f, 1f)), name: "Solid 154035 #1881")
					.WithMesh(new float3[] { new float3(0.07624695f, -0.03812538f, 2.062986f), new float3(0.07624695f, -0.03812538f, -2.05451417f), new float3(0.07624695f, 0.0381246172f, -2.13076425f), new float3(0.07624695f, 0.0381246172f, 2.139236f), new float3(-0.07625305f, -0.03812538f, -2.05451417f), new float3(-0.07625305f, -0.03812538f, 2.062986f), new float3(-0.07625305f, 0.0381246172f, 2.139236f), new float3(-0.07625305f, 0.0381246172f, -2.13076425f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 2, 7, 6, 3, -1, 5, 0, 3, 6, -1, 1, 4, 7, 2, -1, 4, 1, 0, 5, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(1f, 0f, 0f, -0.03812195f),
					new float4(-1f, 0f, 0f, -0.0381280519f),
					new float4(0f, 0f, 1f, -2.2875f),
					new float4(0f, 0f, -1f, -1.83000016f),
					new float4(0f, -1f, 0f, -0.6523615f),
					new float4(0f, 1f, 0f, -1.0251385f),
				}, contents: 2, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(89.78438f, -11.1663885f, 42.09f, 1f)), name: "Glass 154036 #1882")
					.WithMesh(new float3[] { new float3(0.03812195f, 1.0251385f, -1.83000016f), new float3(0.03812195f, 1.0251385f, 2.2875f), new float3(0.03812195f, -0.6523615f, 2.2875f), new float3(0.03812195f, -0.6523615f, -1.83000016f), new float3(-0.0381280519f, 1.0251385f, 2.2875f), new float3(-0.0381280519f, 1.0251385f, -1.83000016f), new float3(-0.0381280519f, -0.6523615f, -1.83000016f), new float3(-0.0381280519f, -0.6523615f, 2.2875f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 1, 4, 7, 2, -1, 5, 0, 3, 6, -1, 6, 3, 2, 7, -1, 0, 5, 4, 1, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(1f, 0f, 0f, -0.06777832f),
					new float4(-1f, 0f, 0f, -0.08472168f),
					new float4(0f, 0f, 1f, -0.03388916f),
					new float4(0f, -0.707106769f, -0.707106769f, -0.485251933f),
					new float4(0f, 0.707106769f, -0.707106769f, -0.760826945f),
					new float4(0f, 0f, -1f, -0.04236084f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(89.79285f, -11.174861f, 44.41986f, 1f)), name: "Solid 154038 #1884")
					.WithMesh(new float3[] { new float3(0.06777832f, -0.643889f, -0.04236084f), new float3(0.06777832f, 1.03361094f, -0.04236084f), new float3(0.06777832f, 1.109861f, 0.03388916f), new float3(0.06777832f, -0.720139f, 0.03388916f), new float3(-0.08472168f, 1.03361094f, -0.04236084f), new float3(-0.08472168f, -0.643889f, -0.04236084f), new float3(-0.08472168f, -0.720139f, 0.03388916f), new float3(-0.08472168f, 1.109861f, 0.03388916f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 2, 7, 6, 3, -1, 5, 0, 3, 6, -1, 1, 4, 7, 2, -1, 4, 1, 0, 5, -1 }))
				;
		}

		// Near (93.978, -16.394, 36.981): the exact CSG makes a triangle the mesh does not have
		static ContentsScene DetailsSite11()
		{
			return new ContentsScene().WithContentsList(kMapContents)
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(1f, 0f, 0f, -0.0762484744f),
					new float4(-1f, 0f, 0f, -0.07625153f),
					new float4(0f, 1f, 0f, -0.0381251536f),
					new float4(0f, -0.707106769f, 0.707106769f, -0.838708f),
					new float4(0f, -0.707106769f, -0.707106769f, -0.832715452f),
					new float4(0f, -1f, 0f, -0.03812485f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(93.97813f, -16.3556252f, 36.9770126f, 1f)), name: "Solid 189499 #1307")
					.WithMesh(new float3[] { new float3(0.0762484744f, -0.03812485f, 1.14798737f), new float3(0.0762484744f, -0.03812485f, -1.13951266f), new float3(0.0762484744f, 0.0381251536f, -1.21576262f), new float3(0.0762484744f, 0.0381251536f, 1.22423732f), new float3(-0.07625153f, -0.03812485f, -1.13951266f), new float3(-0.07625153f, -0.03812485f, 1.14798737f), new float3(-0.07625153f, 0.0381251536f, 1.22423732f), new float3(-0.07625153f, 0.0381251536f, -1.21576262f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 2, 7, 6, 3, -1, 5, 0, 3, 6, -1, 1, 4, 7, 2, -1, 4, 1, 0, 5, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(1f, 0f, 0f, -0.06777985f),
					new float4(-1f, 0f, 0f, -0.08472016f),
					new float4(0f, 0f, 1f, -0.03388779f),
					new float4(0f, -0.707106769f, -0.707106769f, -0.359446883f),
					new float4(0f, 0.707106769f, -0.707106769f, -0.5631326f),
					new float4(0f, 0f, -1f, -0.0423622131f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(93.9865952f, -17.1477776f, 38.1673622f, 1f)), name: "Solid 189500 #1308")
					.WithMesh(new float3[] { new float3(0.06777985f, -0.465972453f, -0.0423622131f), new float3(0.06777985f, 0.754027545f, -0.0423622131f), new float3(0.06777985f, 0.830277562f, 0.03388779f), new float3(0.06777985f, -0.54222244f, 0.03388779f), new float3(-0.08472016f, 0.754027545f, -0.0423622131f), new float3(-0.08472016f, -0.465972453f, -0.0423622131f), new float3(-0.08472016f, -0.54222244f, 0.03388779f), new float3(-0.08472016f, 0.830277562f, 0.03388779f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 2, 7, 6, 3, -1, 5, 0, 3, 6, -1, 1, 4, 7, 2, -1, 4, 1, 0, 5, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(1f, 0f, 0f, -0.0381234735f),
					new float4(-1f, 0f, 0f, -0.0381265245f),
					new float4(0f, 0f, 1f, -1.27083206f),
					new float4(0f, 0f, -1f, -1.016668f),
					new float4(0f, -1f, 0f, -0.4744449f),
					new float4(0f, 1f, 0f, -0.7455551f),
				}, contents: 2, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(93.97813f, -17.1393051f, 36.8541679f, 1f)), name: "Glass 189501 #1309")
					.WithMesh(new float3[] { new float3(0.0381234735f, 0.7455551f, -1.016668f), new float3(0.0381234735f, 0.7455551f, 1.27083206f), new float3(0.0381234735f, -0.4744449f, 1.27083206f), new float3(0.0381234735f, -0.4744449f, -1.016668f), new float3(-0.0381265245f, 0.7455551f, 1.27083206f), new float3(-0.0381265245f, 0.7455551f, -1.016668f), new float3(-0.0381265245f, -0.4744449f, -1.016668f), new float3(-0.0381265245f, -0.4744449f, 1.27083206f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 1, 4, 7, 2, -1, 5, 0, 3, 6, -1, 6, 3, 2, 7, -1, 0, 5, 4, 1, -1 }))
				;
		}

		// Near (-3.291, 3.126, 164.471): the exact CSG makes a triangle the mesh does not have
		static ContentsScene DetailsSite12()
		{
			return new ContentsScene().WithContentsList(kMapContents)
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(1f, 0f, 0f, -0.07624994f),
					new float4(-1f, 0f, 0f, -0.0762500539f),
					new float4(0f, 1f, 0f, -0.0381249338f),
					new float4(0f, -0.707106769f, 0.707106769f, -0.838703156f),
					new float4(0f, -0.707106769f, -0.707106769f, -0.8327206f),
					new float4(0f, -1f, 0f, -0.0381250679f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(-3.27875f, 3.164375f, 164.08577f, 1f)), name: "Solid 247410 #2010")
					.WithMesh(new float3[] { new float3(0.07624994f, -0.0381250679f, 1.14798033f), new float3(0.07624994f, -0.0381250679f, -1.13951969f), new float3(0.07624994f, 0.0381249338f, -1.21576965f), new float3(0.07624994f, 0.0381249338f, 1.22423029f), new float3(-0.0762500539f, -0.0381250679f, -1.13951969f), new float3(-0.0762500539f, -0.0381250679f, 1.14798033f), new float3(-0.0762500539f, 0.0381249338f, 1.22423029f), new float3(-0.0762500539f, 0.0381249338f, -1.21576965f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 2, 7, 6, 3, -1, 5, 0, 3, 6, -1, 1, 4, 7, 2, -1, 4, 1, 0, 5, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(1f, 0f, 0f, -0.06777774f),
					new float4(-1f, 0f, 0f, -0.08472226f),
					new float4(0f, 0f, 1f, -0.03389221f),
					new float4(0f, -0.707106769f, -0.707106769f, -0.359443575f),
					new float4(0f, 0.707106769f, -0.707106769f, -0.563129663f),
					new float4(0f, 0f, -1f, -0.0423577875f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(-3.27027774f, 2.37222219f, 165.276108f, 1f)), name: "Solid 247411 #2011")
					.WithMesh(new float3[] { new float3(0.06777774f, -0.465972215f, -0.0423577875f), new float3(0.06777774f, 0.754027843f, -0.0423577875f), new float3(0.06777774f, 0.83027786f, 0.03389221f), new float3(0.06777774f, -0.5422222f, 0.03389221f), new float3(-0.08472226f, 0.754027843f, -0.0423577875f), new float3(-0.08472226f, -0.465972215f, -0.0423577875f), new float3(-0.08472226f, -0.5422222f, 0.03389221f), new float3(-0.08472226f, 0.83027786f, 0.03389221f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 2, 7, 6, 3, -1, 5, 0, 3, 6, -1, 1, 4, 7, 2, -1, 4, 1, 0, 5, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(1f, 0f, 0f, -0.03812494f),
					new float4(-1f, 0f, 0f, -0.0381250568f),
					new float4(0f, 0f, 1f, -1.27082884f),
					new float4(0f, 0f, -1f, -1.01667118f),
					new float4(0f, -1f, 0f, -0.4744444f),
					new float4(0f, 1f, 0f, -0.745555639f),
				}, contents: 2, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(-3.27875f, 2.38069439f, 163.962921f, 1f)), name: "Glass 247412 #2012")
					.WithMesh(new float3[] { new float3(0.03812494f, 0.745555639f, -1.01667118f), new float3(0.03812494f, 0.745555639f, 1.27082884f), new float3(0.03812494f, -0.4744444f, 1.27082884f), new float3(0.03812494f, -0.4744444f, -1.01667118f), new float3(-0.0381250568f, 0.745555639f, 1.27082884f), new float3(-0.0381250568f, 0.745555639f, -1.01667118f), new float3(-0.0381250568f, -0.4744444f, -1.01667118f), new float3(-0.0381250568f, -0.4744444f, 1.27082884f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 1, 4, 7, 2, -1, 5, 0, 3, 6, -1, 6, 3, 2, 7, -1, 0, 5, 4, 1, -1 }))
				;
		}

		// Near (-3.291, 3.126, 169.351): the exact CSG makes a triangle the mesh does not have
		static ContentsScene DetailsSite13()
		{
			return new ContentsScene().WithContentsList(kMapContents)
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(1f, 0f, 0f, -0.07624994f),
					new float4(-1f, 0f, 0f, -0.0762500539f),
					new float4(0f, 1f, 0f, -0.0381249338f),
					new float4(0f, -0.707106769f, 0.707106769f, -0.838710546f),
					new float4(0f, -0.707106769f, -0.707106769f, -0.832713246f),
					new float4(0f, -1f, 0f, -0.0381250679f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(-3.27875f, 3.164375f, 168.965759f, 1f)), name: "Solid 247428 #2015")
					.WithMesh(new float3[] { new float3(0.07624994f, -0.0381250679f, 1.14799082f), new float3(0.07624994f, -0.0381250679f, -1.13950932f), new float3(0.07624994f, 0.0381249338f, -1.21575928f), new float3(0.07624994f, 0.0381249338f, 1.22424078f), new float3(-0.0762500539f, -0.0381250679f, -1.13950932f), new float3(-0.0762500539f, -0.0381250679f, 1.14799082f), new float3(-0.0762500539f, 0.0381249338f, 1.22424078f), new float3(-0.0762500539f, 0.0381249338f, -1.21575928f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 2, 7, 6, 3, -1, 5, 0, 3, 6, -1, 1, 4, 7, 2, -1, 4, 1, 0, 5, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(1f, 0f, 0f, -0.06777774f),
					new float4(-1f, 0f, 0f, -0.08472226f),
					new float4(0f, 0f, 1f, -0.03388733f),
					new float4(0f, -0.707106769f, -0.707106769f, -0.359447032f),
					new float4(0f, 0.707106769f, -0.707106769f, -0.5631331f),
					new float4(0f, 0f, -1f, -0.04236267f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(-3.27027774f, 2.37222219f, 170.156113f, 1f)), name: "Solid 247429 #2016")
					.WithMesh(new float3[] { new float3(0.06777774f, -0.465972215f, -0.04236267f), new float3(0.06777774f, 0.754027843f, -0.04236267f), new float3(0.06777774f, 0.83027786f, 0.03388733f), new float3(0.06777774f, -0.5422222f, 0.03388733f), new float3(-0.08472226f, 0.754027843f, -0.04236267f), new float3(-0.08472226f, -0.465972215f, -0.04236267f), new float3(-0.08472226f, -0.5422222f, 0.03388733f), new float3(-0.08472226f, 0.83027786f, 0.03388733f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 2, 7, 6, 3, -1, 5, 0, 3, 6, -1, 1, 4, 7, 2, -1, 4, 1, 0, 5, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(1f, 0f, 0f, -0.03812494f),
					new float4(-1f, 0f, 0f, -0.0381250568f),
					new float4(0f, 0f, 1f, -1.27083921f),
					new float4(0f, 0f, -1f, -1.01666081f),
					new float4(0f, -1f, 0f, -0.4744444f),
					new float4(0f, 1f, 0f, -0.745555639f),
				}, contents: 2, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(-3.27875f, 2.38069439f, 168.842911f, 1f)), name: "Glass 247430 #2017")
					.WithMesh(new float3[] { new float3(0.03812494f, 0.745555639f, -1.01666081f), new float3(0.03812494f, 0.745555639f, 1.27083921f), new float3(0.03812494f, -0.4744444f, 1.27083921f), new float3(0.03812494f, -0.4744444f, -1.01666081f), new float3(-0.0381250568f, 0.745555639f, 1.27083921f), new float3(-0.0381250568f, 0.745555639f, -1.01666081f), new float3(-0.0381250568f, -0.4744444f, -1.01666081f), new float3(-0.0381250568f, -0.4744444f, 1.27083921f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 1, 4, 7, 2, -1, 5, 0, 3, 6, -1, 6, 3, 2, 7, -1, 0, 5, 4, 1, -1 }))
				;
		}

		// Near (-27.196, 3.126, 145.866): the mesh has a triangle the exact CSG does not make
		static ContentsScene DetailsSite14()
		{
			return new ContentsScene().WithContentsList(kMapContents)
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(1f, 0f, 0f, -0.04447983f),
					new float4(0f, 1f, 0f, -0.0381249338f),
					new float4(0f, -0.707106769f, -0.707106769f, -0.652997f),
					new float4(0f, -0.707106769f, 0.707106769f, -1.01842678f),
					new float4(0f, -1f, 0f, -0.0381250679f),
					new float4(-1f, 0f, 0f, -0.06989517f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(-27.18948f, 3.164375f, 146.7516f, 1f)), name: "Solid 296552 #1325")
					.WithMesh(new float3[] { new float3(0.04447983f, -0.0381250679f, 1.40214789f), new float3(0.04447983f, -0.0381250679f, -0.8853522f), new float3(0.04447983f, 0.0381249338f, -0.9616022f), new float3(0.04447983f, 0.0381249338f, 1.478398f), new float3(-0.06989517f, 0.0381249338f, -0.9616022f), new float3(-0.06989517f, 0.0381249338f, 1.478398f), new float3(-0.06989517f, -0.0381250679f, -0.8853522f), new float3(-0.06989517f, -0.0381250679f, 1.40214789f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 3, 2, -1, 6, 4, 2, 1, -1, 5, 7, 0, 3, -1, 7, 6, 1, 0, -1, 6, 7, 5, 4, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(1f, 0f, 0f, -0.04447983f),
					new float4(0f, 0f, -1f, -0.0338830575f),
					new float4(0f, -0.707106769f, 0.707106769f, -0.359450042f),
					new float4(0f, 0.707106769f, 0.707106769f, -0.56313616f),
					new float4(0f, 0f, 1f, -0.0423669443f),
					new float4(-1f, 0f, 0f, -0.06989517f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(-27.18948f, 2.37222219f, 145.823883f, 1f)), name: "Solid 296553 #1326")
					.WithMesh(new float3[] { new float3(0.04447983f, 0.754027843f, 0.0423669443f), new float3(0.04447983f, -0.4659722f, 0.0423669443f), new float3(0.04447983f, -0.5422222f, -0.0338830575f), new float3(0.04447983f, 0.83027786f, -0.0338830575f), new float3(-0.06989517f, -0.5422222f, -0.0338830575f), new float3(-0.06989517f, 0.83027786f, -0.0338830575f), new float3(-0.06989517f, -0.4659722f, 0.0423669443f), new float3(-0.06989517f, 0.754027843f, 0.0423669443f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 3, 2, -1, 6, 4, 2, 1, -1, 5, 7, 0, 3, -1, 7, 6, 1, 0, -1, 6, 7, 5, 4, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(-1f, 0f, 0f, -0.0381244645f),
					new float4(1f, 0f, 0f, -0.0381255336f),
					new float4(0f, 0f, -1f, -1.01666927f),
					new float4(0f, 0f, 1f, -1.27083063f),
					new float4(0f, -1f, 0f, -0.4744444f),
					new float4(0f, 1f, 0f, -0.745555639f),
				}, contents: 2, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(-27.22125f, 2.38069439f, 146.882919f, 1f)), name: "Glass 296554 #1327")
					.WithMesh(new float3[] { new float3(-0.0381244645f, 0.745555639f, 1.27083063f), new float3(-0.0381244645f, 0.745555639f, -1.01666927f), new float3(-0.0381244645f, -0.4744444f, -1.01666927f), new float3(-0.0381244645f, -0.4744444f, 1.27083063f), new float3(0.0381255336f, 0.745555639f, -1.01666927f), new float3(0.0381255336f, 0.745555639f, 1.27083063f), new float3(0.0381255336f, -0.4744444f, 1.27083063f), new float3(0.0381255336f, -0.4744444f, -1.01666927f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 1, 4, 7, 2, -1, 5, 0, 3, 6, -1, 6, 3, 2, 7, -1, 0, 5, 4, 1, -1 }))
				;
		}

		// Near (-3.355, 7.015, 162.057): the mesh has a triangle the exact CSG does not make
		static ContentsScene DetailsSite15()
		{
			return new ContentsScene().WithContentsList(kMapContents)
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, -1f, 0f, -0.130714461f),
					new float4(0f, 1f, 0f, -0.174285546f),
					new float4(0f, 0f, 1f, -7.632268f),
					new float4(-1f, 0f, 0f, -0.185178667f),
					new float4(1f, 0f, 0f, -0.119821332f),
					new float4(-0.707106769f, -0.707106769f, 0f, -0.0616194941f),
					new float4(0.707106769f, 1.770802E-13f, -0.707106769f, -7.196617f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(-3.47482133f, 6.84071445f, 170.487732f, 1f)), name: "Solid 326029 #4250")
					.WithMesh(new float3[] { new float3(0.0435713343f, -0.130714461f, -10.1339827f), new float3(0.119821332f, -0.130714461f, -10.0577326f), new float3(0.119821332f, -0.130714461f, 7.632268f), new float3(0.0435713343f, -0.130714461f, 7.632268f), new float3(0.119821332f, 0.174285546f, -10.0577326f), new float3(-0.185178667f, 0.174285546f, -10.3627319f), new float3(-0.185178667f, 0.174285546f, 7.632268f), new float3(0.119821332f, 0.174285546f, 7.632268f), new float3(-0.185178667f, 0.0980355442f, 7.632268f), new float3(-0.185178667f, 0.0980355442f, -10.3627319f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 8, 3, 2, 7, 6, -1, 5, 9, 8, 6, -1, 1, 4, 7, 2, -1, 9, 0, 3, 8, -1, 0, 9, 5, 4, 1, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 0f, -1f, -0.145241082f),
					new float4(0f, 0f, 1f, -0.159758911f),
					new float4(-0.257662654f, 0.9662349f, 0f, -0.375342429f),
					new float4(0.953582644f, 0.301131368f, 0f, -0.138299763f),
					new float4(-0.952911f, -0.3032501f, 0f, -0.1564752f),
					new float4(0f, -1f, 0f, -0.33041656f),
					new float4(1f, 0f, 0f, -0.134998813f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(-3.48999882f, 7.34541655f, 160.575241f, 1f)), name: "Solid 1138471 #5136")
					.WithMesh(new float3[] { new float3(0.134998813f, 0.0317709781f, -0.145241082f), new float3(0.134998813f, -0.33041656f, -0.145241082f), new float3(-0.05905729f, -0.33041656f, -0.145241082f), new float3(-0.265313685f, 0.317708462f, -0.145241082f), new float3(0.020623818f, 0.393958449f, -0.145241082f), new float3(0.134998813f, -0.33041656f, 0.159758911f), new float3(0.134998813f, 0.0317709781f, 0.159758911f), new float3(0.020623818f, 0.393958449f, 0.159758911f), new float3(-0.265313685f, 0.317708462f, 0.159758911f), new float3(-0.05905729f, -0.33041656f, 0.159758911f) }, new int[] { 0, 1, 2, 3, 4, -1, 5, 6, 7, 8, 9, -1, 3, 8, 7, 4, -1, 6, 0, 4, 7, -1, 2, 9, 8, 3, -1, 1, 5, 9, 2, -1, 5, 1, 0, 6, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 0f, -1f, -0.871430039f),
					new float4(0f, 0f, 1f, -0.958569944f),
					new float4(-0.257662654f, 0.9662349f, 0f, -0.375342458f),
					new float4(0.953582644f, 0.301131368f, 0f, -0.138299644f),
					new float4(-0.952911f, -0.3032501f, 0f, -0.15647532f),
					new float4(0f, -1f, 0f, -0.33041656f),
					new float4(1f, 0f, 0f, -0.1349987f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(-3.490018f, 7.34541655f, 161.60643f, 1f)), name: "Solid 1138495 #5160")
					.WithMesh(new float3[] { new float3(0.1349987f, 0.03177096f, -0.871430039f), new float3(0.1349987f, -0.33041656f, -0.871430039f), new float3(-0.05905742f, -0.33041656f, -0.871430039f), new float3(-0.2653138f, 0.317708462f, -0.871430039f), new float3(0.0206236932f, 0.393958449f, -0.871430039f), new float3(0.1349987f, -0.33041656f, 0.958569944f), new float3(0.1349987f, 0.03177096f, 0.958569944f), new float3(0.0206236932f, 0.393958449f, 0.958569944f), new float3(-0.2653138f, 0.317708462f, 0.958569944f), new float3(-0.05905742f, -0.33041656f, 0.958569944f) }, new int[] { 0, 1, 2, 3, 4, -1, 5, 6, 7, 8, 9, -1, 3, 8, 7, 4, -1, 6, 0, 4, 7, -1, 2, 9, 8, 3, -1, 1, 5, 9, 2, -1, 5, 1, 0, 6, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 0f, -1f, -0.1452356f),
					new float4(0f, 0f, 1f, -0.159764409f),
					new float4(-0.257662654f, 0.9662349f, 0f, -0.375342458f),
					new float4(0.953582644f, 0.301131368f, 0f, -0.138299644f),
					new float4(-0.952911f, -0.3032501f, 0f, -0.15647532f),
					new float4(0f, -1f, 0f, -0.33041656f),
					new float4(1f, 0f, 0f, -0.1349987f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(-3.490018f, 7.34541655f, 162.710236f, 1f)), name: "Solid 1138507 #5172")
					.WithMesh(new float3[] { new float3(0.1349987f, 0.03177096f, -0.1452356f), new float3(0.1349987f, -0.33041656f, -0.1452356f), new float3(-0.05905742f, -0.33041656f, -0.1452356f), new float3(-0.2653138f, 0.317708462f, -0.1452356f), new float3(0.0206236932f, 0.393958449f, -0.1452356f), new float3(0.1349987f, -0.33041656f, 0.159764409f), new float3(0.1349987f, 0.03177096f, 0.159764409f), new float3(0.0206236932f, 0.393958449f, 0.159764409f), new float3(-0.2653138f, 0.317708462f, 0.159764409f), new float3(-0.05905742f, -0.33041656f, 0.159764409f) }, new int[] { 0, 1, 2, 3, 4, -1, 5, 6, 7, 8, 9, -1, 3, 8, 7, 4, -1, 6, 0, 4, 7, -1, 2, 9, 8, 3, -1, 1, 5, 9, 2, -1, 5, 1, 0, 6, -1 }))
				;
		}

		// Near (60.733, -1.754, 95.3): the exact CSG makes a triangle the mesh does not have
		static ContentsScene DetailsSite16()
		{
			return new ContentsScene().WithContentsList(kMapContents)
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 0f, -1f, -0.0677813739f),
					new float4(0f, 0f, 1f, -0.08471863f),
					new float4(0f, -1f, 0f, -0.0296527911f),
					new float4(0.707106769f, 0.707106769f, 0f, -0.748844445f),
					new float4(-0.707106769f, 0.707106769f, 0f, -0.9345607f),
					new float4(0f, 1f, 0f, -0.04659721f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(60.4831963f, -1.80034721f, 95.30403f, 1f)), name: "Solid 346550 #2447")
					.WithMesh(new float3[] { new float3(-1.27507126f, 0.04659721f, -0.0677813739f), new float3(1.01242876f, 0.04659721f, -0.0677813739f), new float3(1.08867884f, -0.0296527911f, -0.0677813739f), new float3(-1.35132122f, -0.0296527911f, -0.0677813739f), new float3(1.01242876f, 0.04659721f, 0.08471863f), new float3(-1.27507126f, 0.04659721f, 0.08471863f), new float3(-1.35132122f, -0.0296527911f, 0.08471863f), new float3(1.08867884f, -0.0296527911f, 0.08471863f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 6, 3, 2, 7, -1, 1, 4, 7, 2, -1, 5, 0, 3, 6, -1, 0, 5, 4, 1, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 0f, -1f, -0.0677813739f),
					new float4(0f, 0f, 1f, -0.08471863f),
					new float4(1f, 0f, 0f, -0.0338882431f),
					new float4(-0.707106769f, -0.707106769f, 0f, -0.3594464f),
					new float4(-0.707106769f, 0.707106769f, 0f, -0.563132465f),
					new float4(-1f, 0f, 0f, -0.0423617549f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(61.5379868f, -1.28777778f, 95.30403f, 1f)), name: "Solid 346551 #2448")
					.WithMesh(new float3[] { new float3(-0.0423617549f, -0.465972245f, -0.0677813739f), new float3(-0.0423617549f, 0.754027843f, -0.0677813739f), new float3(0.0338882431f, 0.8302778f, -0.0677813739f), new float3(0.0338882431f, -0.542222261f, -0.0677813739f), new float3(-0.0423617549f, 0.754027843f, 0.08471863f), new float3(-0.0423617549f, -0.465972245f, 0.08471863f), new float3(0.0338882431f, -0.542222261f, 0.08471863f), new float3(0.0338882431f, 0.8302778f, 0.08471863f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 2, 7, 6, 3, -1, 5, 0, 3, 6, -1, 1, 4, 7, 2, -1, 4, 1, 0, 5, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 1f, 0f, -0.7455556f),
					new float4(0f, -1f, 0f, -0.474444419f),
					new float4(1f, 0f, 0f, -1.14374971f),
					new float4(-1f, 0f, 0f, -1.14375031f),
					new float4(0f, 0f, -1f, -0.0338906869f),
					new float4(0f, 0f, 1f, -0.0423593149f),
				}, contents: 2, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(60.3518753f, -1.27930558f, 95.3082657f, 1f)), name: "Glass 346756 #2824")
					.WithMesh(new float3[] { new float3(-1.14375031f, 0.7455556f, 0.0423593149f), new float3(1.14374971f, 0.7455556f, 0.0423593149f), new float3(1.14374971f, 0.7455556f, -0.0338906869f), new float3(-1.14375031f, 0.7455556f, -0.0338906869f), new float3(1.14374971f, -0.474444419f, 0.0423593149f), new float3(-1.14375031f, -0.474444419f, 0.0423593149f), new float3(-1.14375031f, -0.474444419f, -0.0338906869f), new float3(1.14374971f, -0.474444419f, -0.0338906869f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 1, 4, 7, 2, -1, 5, 0, 3, 6, -1, 6, 3, 2, 7, -1, 0, 5, 4, 1, -1 }))
				;
		}

		// Near (65.105, -0.534, 93.178): the exact CSG makes a triangle the mesh does not have
		static ContentsScene DetailsSite17()
		{
			return new ContentsScene().WithContentsList(kMapContents)
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(1f, 0f, 0f, -0.07624878f),
					new float4(-1f, 0f, 0f, -0.07625122f),
					new float4(0f, 1f, 0f, -0.03812499f),
					new float4(0f, -0.707106769f, 0.707106769f, -0.8387064f),
					new float4(0f, -0.707106769f, -0.707106769f, -0.8327173f),
					new float4(0f, -1f, 0f, -0.0381250121f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(65.07938f, -0.495625f, 92.7920151f, 1f)), name: "Solid 346588 #2457")
					.WithMesh(new float3[] { new float3(0.07624878f, -0.0381250121f, 1.147985f), new float3(0.07624878f, -0.0381250121f, -1.13951516f), new float3(0.07624878f, 0.03812499f, -1.21576512f), new float3(0.07624878f, 0.03812499f, 1.22423494f), new float3(-0.07625122f, -0.0381250121f, -1.13951516f), new float3(-0.07625122f, -0.0381250121f, 1.147985f), new float3(-0.07625122f, 0.03812499f, 1.22423494f), new float3(-0.07625122f, 0.03812499f, -1.21576512f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 2, 7, 6, 3, -1, 5, 0, 3, 6, -1, 1, 4, 7, 2, -1, 4, 1, 0, 5, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(1f, 0f, 0f, -0.06778015f),
					new float4(-1f, 0f, 0f, -0.08471985f),
					new float4(0f, 0f, 1f, -0.03388916f),
					new float4(0f, -0.707106769f, -0.707106769f, -0.359445751f),
					new float4(0f, 0.707106769f, -0.707106769f, -0.5631318f),
					new float4(0f, 0f, -1f, -0.04236084f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(65.0878448f, -1.28777778f, 93.98236f, 1f)), name: "Solid 346591 #2460")
					.WithMesh(new float3[] { new float3(0.06778015f, -0.465972215f, -0.04236084f), new float3(0.06778015f, 0.754027843f, -0.04236084f), new float3(0.06778015f, 0.8302778f, 0.03388916f), new float3(0.06778015f, -0.5422222f, 0.03388916f), new float3(-0.08471985f, 0.754027843f, -0.04236084f), new float3(-0.08471985f, -0.465972215f, -0.04236084f), new float3(-0.08471985f, -0.5422222f, 0.03388916f), new float3(-0.08471985f, 0.8302778f, 0.03388916f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 2, 7, 6, 3, -1, 5, 0, 3, 6, -1, 1, 4, 7, 2, -1, 4, 1, 0, 5, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 1f, 0f, -0.7455556f),
					new float4(0f, -1f, 0f, -0.474444419f),
					new float4(0f, 0f, 1f, -1.27083349f),
					new float4(0f, 0f, -1f, -1.01666653f),
					new float4(1f, 0f, 0f, -0.03812378f),
					new float4(-1f, 0f, 0f, -0.03812622f),
				}, contents: 2, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(65.07938f, -1.27930558f, 92.66917f, 1f)), name: "Glass 346761 #2833")
					.WithMesh(new float3[] { new float3(-0.03812622f, 0.7455556f, -1.01666653f), new float3(-0.03812622f, 0.7455556f, 1.27083349f), new float3(0.03812378f, 0.7455556f, 1.27083349f), new float3(0.03812378f, 0.7455556f, -1.01666653f), new float3(-0.03812622f, -0.474444419f, 1.27083349f), new float3(-0.03812622f, -0.474444419f, -1.01666653f), new float3(0.03812378f, -0.474444419f, -1.01666653f), new float3(0.03812378f, -0.474444419f, 1.27083349f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 1, 4, 7, 2, -1, 5, 0, 3, 6, -1, 6, 3, 2, 7, -1, 0, 5, 4, 1, -1 }))
				;
		}

		// Near (65.092, -1.754, 93.178): the exact CSG makes a triangle the mesh does not have
		static ContentsScene DetailsSite18()
		{
			return new ContentsScene().WithContentsList(kMapContents)
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(1f, 0f, 0f, -0.07624878f),
					new float4(-1f, 0f, 0f, -0.07625122f),
					new float4(0f, -1f, 0f, -0.0296527911f),
					new float4(0f, 0.707106769f, 0.707106769f, -0.8446971f),
					new float4(0f, 0.707106769f, -0.707106769f, -0.838708043f),
					new float4(0f, 1f, 0f, -0.04659721f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(65.07938f, -1.80034721f, 92.7920151f, 1f)), name: "Solid 346590 #2459")
					.WithMesh(new float3[] { new float3(0.07624878f, 0.04659721f, -1.139515f), new float3(0.07624878f, 0.04659721f, 1.147985f), new float3(0.07624878f, -0.0296527911f, 1.22423494f), new float3(0.07624878f, -0.0296527911f, -1.21576512f), new float3(-0.07625122f, 0.04659721f, 1.147985f), new float3(-0.07625122f, 0.04659721f, -1.139515f), new float3(-0.07625122f, -0.0296527911f, -1.21576512f), new float3(-0.07625122f, -0.0296527911f, 1.22423494f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 6, 3, 2, 7, -1, 1, 4, 7, 2, -1, 5, 0, 3, 6, -1, 0, 5, 4, 1, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(1f, 0f, 0f, -0.06778015f),
					new float4(-1f, 0f, 0f, -0.08471985f),
					new float4(0f, 0f, 1f, -0.03388916f),
					new float4(0f, -0.707106769f, -0.707106769f, -0.359445751f),
					new float4(0f, 0.707106769f, -0.707106769f, -0.5631318f),
					new float4(0f, 0f, -1f, -0.04236084f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(65.0878448f, -1.28777778f, 93.98236f, 1f)), name: "Solid 346591 #2460")
					.WithMesh(new float3[] { new float3(0.06778015f, -0.465972215f, -0.04236084f), new float3(0.06778015f, 0.754027843f, -0.04236084f), new float3(0.06778015f, 0.8302778f, 0.03388916f), new float3(0.06778015f, -0.5422222f, 0.03388916f), new float3(-0.08471985f, 0.754027843f, -0.04236084f), new float3(-0.08471985f, -0.465972215f, -0.04236084f), new float3(-0.08471985f, -0.5422222f, 0.03388916f), new float3(-0.08471985f, 0.8302778f, 0.03388916f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 2, 7, 6, 3, -1, 5, 0, 3, 6, -1, 1, 4, 7, 2, -1, 4, 1, 0, 5, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 1f, 0f, -0.7455556f),
					new float4(0f, -1f, 0f, -0.474444419f),
					new float4(0f, 0f, 1f, -1.27083349f),
					new float4(0f, 0f, -1f, -1.01666653f),
					new float4(1f, 0f, 0f, -0.03812378f),
					new float4(-1f, 0f, 0f, -0.03812622f),
				}, contents: 2, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(65.07938f, -1.27930558f, 92.66917f, 1f)), name: "Glass 346761 #2833")
					.WithMesh(new float3[] { new float3(-0.03812622f, 0.7455556f, -1.01666653f), new float3(-0.03812622f, 0.7455556f, 1.27083349f), new float3(0.03812378f, 0.7455556f, 1.27083349f), new float3(0.03812378f, 0.7455556f, -1.01666653f), new float3(-0.03812622f, -0.474444419f, 1.27083349f), new float3(-0.03812622f, -0.474444419f, -1.01666653f), new float3(0.03812378f, -0.474444419f, -1.01666653f), new float3(0.03812378f, -0.474444419f, 1.27083349f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 1, 4, 7, 2, -1, 5, 0, 3, 6, -1, 6, 3, 2, 7, -1, 0, 5, 4, 1, -1 }))
				;
		}

		// Near (-3.291, 3.126, 147.391): the exact CSG makes a triangle the mesh does not have
		static ContentsScene DetailsSite19()
		{
			return new ContentsScene().WithContentsList(kMapContents)
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(1f, 0f, 0f, -0.07624994f),
					new float4(-1f, 0f, 0f, -0.0762500539f),
					new float4(0f, 1f, 0f, -0.0381249338f),
					new float4(0f, -0.707106769f, 0.707106769f, -0.838704467f),
					new float4(0f, -0.707106769f, -0.707106769f, -0.832719266f),
					new float4(0f, -1f, 0f, -0.0381250679f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(-3.27875f, 3.164375f, 147.005768f, 1f)), name: "Solid 3899 #1995")
					.WithMesh(new float3[] { new float3(0.07624994f, -0.0381250679f, 1.14798224f), new float3(0.07624994f, -0.0381250679f, -1.13951778f), new float3(0.07624994f, 0.0381249338f, -1.21576786f), new float3(0.07624994f, 0.0381249338f, 1.2242322f), new float3(-0.0762500539f, -0.0381250679f, -1.13951778f), new float3(-0.0762500539f, -0.0381250679f, 1.14798224f), new float3(-0.0762500539f, 0.0381249338f, 1.2242322f), new float3(-0.0762500539f, 0.0381249338f, -1.21576786f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 2, 7, 6, 3, -1, 5, 0, 3, 6, -1, 1, 4, 7, 2, -1, 4, 1, 0, 5, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(1f, 0f, 0f, -0.06777774f),
					new float4(-1f, 0f, 0f, -0.08472226f),
					new float4(0f, 0f, 1f, -0.0338940434f),
					new float4(0f, -0.707106769f, -0.707106769f, -0.359442264f),
					new float4(0f, 0.707106769f, -0.707106769f, -0.563128352f),
					new float4(0f, 0f, -1f, -0.04235596f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(-3.27027774f, 2.37222219f, 148.1961f, 1f)), name: "Solid 3900 #1996")
					.WithMesh(new float3[] { new float3(0.06777774f, -0.4659722f, -0.04235596f), new float3(0.06777774f, 0.7540278f, -0.04235596f), new float3(0.06777774f, 0.8302778f, 0.0338940434f), new float3(0.06777774f, -0.5422222f, 0.0338940434f), new float3(-0.08472226f, 0.7540278f, -0.04235596f), new float3(-0.08472226f, -0.4659722f, -0.04235596f), new float3(-0.08472226f, -0.5422222f, 0.0338940434f), new float3(-0.08472226f, 0.8302778f, 0.0338940434f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 2, 7, 6, 3, -1, 5, 0, 3, 6, -1, 1, 4, 7, 2, -1, 4, 1, 0, 5, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(1f, 0f, 0f, -0.03812494f),
					new float4(-1f, 0f, 0f, -0.0381250568f),
					new float4(0f, 0f, 1f, -1.27083063f),
					new float4(0f, 0f, -1f, -1.01666927f),
					new float4(0f, -1f, 0f, -0.4744444f),
					new float4(0f, 1f, 0f, -0.745555639f),
				}, contents: 2, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(-3.27875f, 2.38069439f, 146.882919f, 1f)), name: "Glass 3901 #1997")
					.WithMesh(new float3[] { new float3(0.03812494f, 0.745555639f, -1.01666927f), new float3(0.03812494f, 0.745555639f, 1.27083063f), new float3(0.03812494f, -0.4744444f, 1.27083063f), new float3(0.03812494f, -0.4744444f, -1.01666927f), new float3(-0.0381250568f, 0.745555639f, 1.27083063f), new float3(-0.0381250568f, 0.745555639f, -1.01666927f), new float3(-0.0381250568f, -0.4744444f, -1.01666927f), new float3(-0.0381250568f, -0.4744444f, 1.27083063f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 1, 4, 7, 2, -1, 5, 0, 3, 6, -1, 6, 3, 2, 7, -1, 0, 5, 4, 1, -1 }))
				;
		}

		// Near (80.317, -0.381, 59.475): the exact CSG makes a triangle the mesh does not have
		static ContentsScene DetailsSite20()
		{
			return new ContentsScene().WithContentsList(kMapContents)
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(1f, 0f, 0f, -0.07624878f),
					new float4(-1f, 0f, 0f, -0.07625122f),
					new float4(0f, 1f, 0f, -0.038124986f),
					new float4(0f, -0.707106769f, 0.707106769f, -1.48570883f),
					new float4(0f, -0.707106769f, -0.707106769f, -1.47972023f),
					new float4(0f, -1f, 0f, -0.0381250158f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(80.32938f, -0.343125f, 58.7845154f, 1f)), name: "Solid 86213 #1750")
					.WithMesh(new float3[] { new float3(0.07624878f, -0.0381250158f, 2.0629847f), new float3(0.07624878f, -0.0381250158f, -2.05451536f), new float3(0.07624878f, 0.038124986f, -2.13076544f), new float3(0.07624878f, 0.038124986f, 2.13923454f), new float3(-0.07625122f, -0.0381250158f, -2.05451536f), new float3(-0.07625122f, -0.0381250158f, 2.0629847f), new float3(-0.07625122f, 0.038124986f, 2.13923454f), new float3(-0.07625122f, 0.038124986f, -2.13076544f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 2, 7, 6, 3, -1, 5, 0, 3, 6, -1, 1, 4, 7, 2, -1, 4, 1, 0, 5, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(1f, 0f, 0f, -0.03812378f),
					new float4(-1f, 0f, 0f, -0.03812622f),
					new float4(0f, 0f, 1f, -2.28749871f),
					new float4(0f, 0f, -1f, -1.83000135f),
					new float4(0f, -1f, 0f, -0.6523611f),
					new float4(0f, 1f, 0f, -1.02513885f),
				}, contents: 2, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(80.32938f, -1.40638888f, 58.56f, 1f)), name: "Glass 86214 #1751")
					.WithMesh(new float3[] { new float3(0.03812378f, 1.02513885f, -1.83000135f), new float3(0.03812378f, 1.02513885f, 2.28749871f), new float3(0.03812378f, -0.6523611f, 2.28749871f), new float3(0.03812378f, -0.6523611f, -1.83000135f), new float3(-0.03812622f, 1.02513885f, 2.28749871f), new float3(-0.03812622f, 1.02513885f, -1.83000135f), new float3(-0.03812622f, -0.6523611f, -1.83000135f), new float3(-0.03812622f, -0.6523611f, 2.28749871f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 1, 4, 7, 2, -1, 5, 0, 3, 6, -1, 6, 3, 2, 7, -1, 0, 5, 4, 1, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(1f, 0f, 0f, -0.06778015f),
					new float4(-1f, 0f, 0f, -0.08471985f),
					new float4(0f, 0f, 1f, -0.0338879377f),
					new float4(0f, -0.707106769f, -0.707106769f, -0.4852527f),
					new float4(0f, 0.707106769f, -0.707106769f, -0.7608279f),
					new float4(0f, 0f, -1f, -0.04236206f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(80.3378448f, -1.41486108f, 60.8898621f, 1f)), name: "Solid 86216 #1753")
					.WithMesh(new float3[] { new float3(0.06778015f, -0.6438889f, -0.04236206f), new float3(0.06778015f, 1.033611f, -0.04236206f), new float3(0.06778015f, 1.10986114f, 0.0338879377f), new float3(0.06778015f, -0.7201389f, 0.0338879377f), new float3(-0.08471985f, 1.033611f, -0.04236206f), new float3(-0.08471985f, -0.6438889f, -0.04236206f), new float3(-0.08471985f, -0.7201389f, 0.0338879377f), new float3(-0.08471985f, 1.10986114f, 0.0338879377f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 2, 7, 6, 3, -1, 5, 0, 3, 6, -1, 1, 4, 7, 2, -1, 4, 1, 0, 5, -1 }))
				;
		}

		// Near (80.317, -0.381, 48.495): the exact CSG makes a triangle the mesh does not have
		static ContentsScene DetailsSite21()
		{
			return new ContentsScene().WithContentsList(kMapContents)
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(1f, 0f, 0f, -0.07624878f),
					new float4(-1f, 0f, 0f, -0.07625122f),
					new float4(0f, 1f, 0f, -0.038124986f),
					new float4(0f, -0.707106769f, 0.707106769f, -1.48571122f),
					new float4(0f, -0.707106769f, -0.707106769f, -1.47971785f),
					new float4(0f, -1f, 0f, -0.0381250158f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(80.32938f, -0.343125f, 47.804512f, 1f)), name: "Solid 86248 #1760")
					.WithMesh(new float3[] { new float3(0.07624878f, -0.0381250158f, 2.062988f), new float3(0.07624878f, -0.0381250158f, -2.054512f), new float3(0.07624878f, 0.038124986f, -2.130762f), new float3(0.07624878f, 0.038124986f, 2.13923788f), new float3(-0.07625122f, -0.0381250158f, -2.054512f), new float3(-0.07625122f, -0.0381250158f, 2.062988f), new float3(-0.07625122f, 0.038124986f, 2.13923788f), new float3(-0.07625122f, 0.038124986f, -2.130762f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 2, 7, 6, 3, -1, 5, 0, 3, 6, -1, 1, 4, 7, 2, -1, 4, 1, 0, 5, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(1f, 0f, 0f, -0.03812378f),
					new float4(-1f, 0f, 0f, -0.03812622f),
					new float4(0f, 0f, 1f, -2.28749824f),
					new float4(0f, 0f, -1f, -1.83000183f),
					new float4(0f, -1f, 0f, -0.6523611f),
					new float4(0f, 1f, 0f, -1.02513885f),
				}, contents: 2, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(80.32938f, -1.40638888f, 47.58f, 1f)), name: "Glass 86249 #1761")
					.WithMesh(new float3[] { new float3(0.03812378f, 1.02513885f, -1.83000183f), new float3(0.03812378f, 1.02513885f, 2.28749824f), new float3(0.03812378f, -0.6523611f, 2.28749824f), new float3(0.03812378f, -0.6523611f, -1.83000183f), new float3(-0.03812622f, 1.02513885f, 2.28749824f), new float3(-0.03812622f, 1.02513885f, -1.83000183f), new float3(-0.03812622f, -0.6523611f, -1.83000183f), new float3(-0.03812622f, -0.6523611f, 2.28749824f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 1, 4, 7, 2, -1, 5, 0, 3, 6, -1, 6, 3, 2, 7, -1, 0, 5, 4, 1, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(1f, 0f, 0f, -0.06778015f),
					new float4(-1f, 0f, 0f, -0.08471985f),
					new float4(0f, 0f, 1f, -0.0338874832f),
					new float4(0f, -0.707106769f, -0.707106769f, -0.485253036f),
					new float4(0f, 0.707106769f, -0.707106769f, -0.760828257f),
					new float4(0f, 0f, -1f, -0.04236252f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(80.3378448f, -1.41486108f, 49.9098625f, 1f)), name: "Solid 86251 #1763")
					.WithMesh(new float3[] { new float3(0.06778015f, -0.6438889f, -0.04236252f), new float3(0.06778015f, 1.03361118f, -0.04236252f), new float3(0.06778015f, 1.10986114f, 0.0338874832f), new float3(0.06778015f, -0.7201389f, 0.0338874832f), new float3(-0.08471985f, 1.03361118f, -0.04236252f), new float3(-0.08471985f, -0.6438889f, -0.04236252f), new float3(-0.08471985f, -0.7201389f, 0.0338874832f), new float3(-0.08471985f, 1.10986114f, 0.0338874832f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 2, 7, 6, 3, -1, 5, 0, 3, 6, -1, 1, 4, 7, 2, -1, 4, 1, 0, 5, -1 }))
				;
		}

		// Near (80.317, -0.381, 53.985): the exact CSG makes a triangle the mesh does not have
		static ContentsScene DetailsSite22()
		{
			return new ContentsScene().WithContentsList(kMapContents)
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(1f, 0f, 0f, -0.07624878f),
					new float4(-1f, 0f, 0f, -0.07625122f),
					new float4(0f, 1f, 0f, -0.038124986f),
					new float4(0f, -0.707106769f, 0.707106769f, -1.48571f),
					new float4(0f, -0.707106769f, -0.707106769f, -1.479719f),
					new float4(0f, -1f, 0f, -0.0381250158f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(80.32938f, -0.343125f, 53.2945137f, 1f)), name: "Solid 86256 #1755")
					.WithMesh(new float3[] { new float3(0.07624878f, -0.0381250158f, 2.06298637f), new float3(0.07624878f, -0.0381250158f, -2.05451369f), new float3(0.07624878f, 0.038124986f, -2.13076377f), new float3(0.07624878f, 0.038124986f, 2.13923621f), new float3(-0.07625122f, -0.0381250158f, -2.05451369f), new float3(-0.07625122f, -0.0381250158f, 2.06298637f), new float3(-0.07625122f, 0.038124986f, 2.13923621f), new float3(-0.07625122f, 0.038124986f, -2.13076377f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 2, 7, 6, 3, -1, 5, 0, 3, 6, -1, 1, 4, 7, 2, -1, 4, 1, 0, 5, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(1f, 0f, 0f, -0.03812378f),
					new float4(-1f, 0f, 0f, -0.03812622f),
					new float4(0f, 0f, 1f, -2.28750038f),
					new float4(0f, 0f, -1f, -1.82999969f),
					new float4(0f, -1f, 0f, -0.6523611f),
					new float4(0f, 1f, 0f, -1.02513885f),
				}, contents: 2, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(80.32938f, -1.40638888f, 53.07f, 1f)), name: "Glass 86257 #1756")
					.WithMesh(new float3[] { new float3(0.03812378f, 1.02513885f, -1.82999969f), new float3(0.03812378f, 1.02513885f, 2.28750038f), new float3(0.03812378f, -0.6523611f, 2.28750038f), new float3(0.03812378f, -0.6523611f, -1.82999969f), new float3(-0.03812622f, 1.02513885f, 2.28750038f), new float3(-0.03812622f, 1.02513885f, -1.82999969f), new float3(-0.03812622f, -0.6523611f, -1.82999969f), new float3(-0.03812622f, -0.6523611f, 2.28750038f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 1, 4, 7, 2, -1, 5, 0, 3, 6, -1, 6, 3, 2, 7, -1, 0, 5, 4, 1, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(1f, 0f, 0f, -0.06778015f),
					new float4(-1f, 0f, 0f, -0.08471985f),
					new float4(0f, 0f, 1f, -0.0338896178f),
					new float4(0f, -0.707106769f, -0.707106769f, -0.485251546f),
					new float4(0f, 0.707106769f, -0.707106769f, -0.7608267f),
					new float4(0f, 0f, -1f, -0.04236038f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(80.3378448f, -1.41486108f, 55.39986f, 1f)), name: "Solid 86259 #1758")
					.WithMesh(new float3[] { new float3(0.06778015f, -0.64388895f, -0.04236038f), new float3(0.06778015f, 1.033611f, -0.04236038f), new float3(0.06778015f, 1.10986114f, 0.0338896178f), new float3(0.06778015f, -0.720138967f, 0.0338896178f), new float3(-0.08471985f, 1.033611f, -0.04236038f), new float3(-0.08471985f, -0.64388895f, -0.04236038f), new float3(-0.08471985f, -0.720138967f, 0.0338896178f), new float3(-0.08471985f, 1.10986114f, 0.0338896178f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 2, 7, 6, 3, -1, 5, 0, 3, 6, -1, 1, 4, 7, 2, -1, 4, 1, 0, 5, -1 }))
				;
		}

		// Near (80.355, -0.381, 43.005): the exact CSG makes a triangle the mesh does not have
		static ContentsScene DetailsSite23()
		{
			return new ContentsScene().WithContentsList(kMapContents)
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(1f, 0f, 0f, -0.07624878f),
					new float4(-1f, 0f, 0f, -0.07625122f),
					new float4(0f, 1f, 0f, -0.038124986f),
					new float4(0f, -0.707106769f, 0.707106769f, -1.48570979f),
					new float4(0f, -0.707106769f, -0.707106769f, -1.4797194f),
					new float4(0f, -1f, 0f, -0.0381250158f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(80.32938f, -0.343125f, 42.3145142f, 1f)), name: "Solid 86278 #1765")
					.WithMesh(new float3[] { new float3(0.07624878f, -0.0381250158f, 2.062986f), new float3(0.07624878f, -0.0381250158f, -2.05451417f), new float3(0.07624878f, 0.038124986f, -2.13076425f), new float3(0.07624878f, 0.038124986f, 2.139236f), new float3(-0.07625122f, -0.0381250158f, -2.05451417f), new float3(-0.07625122f, -0.0381250158f, 2.062986f), new float3(-0.07625122f, 0.038124986f, 2.139236f), new float3(-0.07625122f, 0.038124986f, -2.13076425f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 2, 7, 6, 3, -1, 5, 0, 3, 6, -1, 1, 4, 7, 2, -1, 4, 1, 0, 5, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(1f, 0f, 0f, -0.03812378f),
					new float4(-1f, 0f, 0f, -0.03812622f),
					new float4(0f, 0f, 1f, -2.2875f),
					new float4(0f, 0f, -1f, -1.83000016f),
					new float4(0f, -1f, 0f, -0.6523611f),
					new float4(0f, 1f, 0f, -1.02513885f),
				}, contents: 2, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(80.32938f, -1.40638888f, 42.09f, 1f)), name: "Glass 86279 #1766")
					.WithMesh(new float3[] { new float3(0.03812378f, 1.02513885f, -1.83000016f), new float3(0.03812378f, 1.02513885f, 2.2875f), new float3(0.03812378f, -0.6523611f, 2.2875f), new float3(0.03812378f, -0.6523611f, -1.83000016f), new float3(-0.03812622f, 1.02513885f, 2.2875f), new float3(-0.03812622f, 1.02513885f, -1.83000016f), new float3(-0.03812622f, -0.6523611f, -1.83000016f), new float3(-0.03812622f, -0.6523611f, 2.2875f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 1, 4, 7, 2, -1, 5, 0, 3, 6, -1, 6, 3, 2, 7, -1, 0, 5, 4, 1, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(1f, 0f, 0f, -0.06778015f),
					new float4(-1f, 0f, 0f, -0.08471985f),
					new float4(0f, 0f, 1f, -0.03388916f),
					new float4(0f, -0.707106769f, -0.707106769f, -0.485251844f),
					new float4(0f, 0.707106769f, -0.707106769f, -0.760827065f),
					new float4(0f, 0f, -1f, -0.04236084f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(80.3378448f, -1.41486108f, 44.41986f, 1f)), name: "Solid 86281 #1768")
					.WithMesh(new float3[] { new float3(0.06778015f, -0.6438889f, -0.04236084f), new float3(0.06778015f, 1.03361118f, -0.04236084f), new float3(0.06778015f, 1.10986114f, 0.03388916f), new float3(0.06778015f, -0.7201389f, 0.03388916f), new float3(-0.08471985f, 1.03361118f, -0.04236084f), new float3(-0.08471985f, -0.6438889f, -0.04236084f), new float3(-0.08471985f, -0.7201389f, 0.03388916f), new float3(-0.08471985f, 1.10986114f, 0.03388916f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 2, 7, 6, 3, -1, 5, 0, 3, 6, -1, 1, 4, 7, 2, -1, 4, 1, 0, 5, -1 }))
				;
		}

		// Near (-23.18, 12.099, 177.51): the exact CSG makes a triangle the mesh does not have
		static ContentsScene DetailsSite24()
		{
			return new ContentsScene().WithContentsList(kMapContents)
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 1f, 0f, -0.190624729f),
					new float4(0.0541373566f, -0.9985335f, 0f, -0.331155449f),
					new float4(1f, 0f, 0f, -4.00736046f),
					new float4(-0.0545040146f, -0.9985136f, 0f, -0.239566639f),
					new float4(0f, 0f, -1f, -1.08444941f),
					new float4(0f, 0f, 1f, -1.35555053f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(-19.25736f, 11.7043753f, 176.154449f, 1f)), name: "Solid 1139864 #5357")
					.WithMesh(new float3[] { new float3(-7.88763952f, 0.190624729f, 1.35555053f), new float3(4.00736046f, 0.190624729f, 1.35555053f), new float3(4.00736046f, 0.190624729f, -1.08444941f), new float3(-7.88763952f, 0.190624729f, -1.08444941f), new float3(4.00736046f, -0.114375278f, 1.35555053f), new float3(0.84298563f, -0.285937756f, 1.35555053f), new float3(0.84298563f, -0.285937756f, -1.08444941f), new float3(4.00736046f, -0.114375278f, -1.08444941f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 1, 4, 7, 2, -1, 5, 0, 3, 6, -1, 6, 3, 2, 7, -1, 0, 5, 4, 1, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0.953582644f, -0.301131368f, 0f, -0.362775326f),
					new float4(0.999621451f, 0.0275125168f, 0f, -0.0497191362f),
					new float4(-1f, 0f, 0f, -0.0381255336f),
					new float4(0f, 0f, -1f, -1.13866389f),
					new float4(0f, 0f, 1f, -1.301336f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(-27.1068745f, 8.702626f, 176.208664f, 1f)), name: "Solid 1139869 #5362")
					.WithMesh(new float3[] { new float3(0.0762494653f, -0.963251233f, 1.301336f), new float3(-0.0381255336f, -1.32543874f, 1.301336f), new float3(-0.0381255336f, -1.32543874f, -1.13866389f), new float3(0.0762494653f, -0.963251233f, -1.13866389f), new float3(-0.0381255336f, 3.19237375f, 1.301336f), new float3(-0.0381255336f, 3.19237375f, -1.13866389f) }, new int[] { 0, 1, 2, 3, -1, 4, 0, 3, 5, -1, 1, 4, 5, 2, -1, 2, 5, 3, -1, 4, 1, 0, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 1f, 0f, -0.474444628f),
					new float4(0f, -1f, 0f, -0.7455554f),
					new float4(-1f, 0f, 0f, -13.2166672f),
					new float4(1f, 0f, 0f, -10.5733337f),
					new float4(0f, 0f, -1f, -1.21999323f),
					new float4(0f, 0f, 1f, -1.2200067f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(-13.9283333f, 12.6405554f, 176.29f, 1f)), name: "Solid 1141629 #5476")
					.WithMesh(new float3[] { new float3(-13.2166672f, 0.474444628f, 1.2200067f), new float3(10.5733337f, 0.474444628f, 1.2200067f), new float3(10.5733337f, 0.474444628f, -1.21999323f), new float3(-13.2166672f, 0.474444628f, -1.21999323f), new float3(10.5733337f, -0.7455554f, 1.2200067f), new float3(-13.2166672f, -0.7455554f, 1.2200067f), new float3(-13.2166672f, -0.7455554f, -1.21999323f), new float3(10.5733337f, -0.7455554f, -1.21999323f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 5, 0, 3, 6, -1, 1, 4, 7, 2, -1, 2, 7, 6, 3, -1, 4, 1, 0, 5, -1 }))
				;
		}

		// Near (80.304, -2.059, 37.515): the exact CSG makes a triangle the mesh does not have
		static ContentsScene DetailsSite25()
		{
			return new ContentsScene().WithContentsList(kMapContents)
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(1f, 0f, 0f, -0.03812378f),
					new float4(-1f, 0f, 0f, -0.03812622f),
					new float4(0f, 0f, 1f, -2.28750157f),
					new float4(0f, 0f, -1f, -1.82999849f),
					new float4(0f, -1f, 0f, -0.6523611f),
					new float4(0f, 1f, 0f, -1.02513885f),
				}, contents: 2, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(80.32938f, -1.40638888f, 36.6f, 1f)), name: "Glass 119037 #1831")
					.WithMesh(new float3[] { new float3(0.03812378f, 1.02513885f, -1.82999849f), new float3(0.03812378f, 1.02513885f, 2.28750157f), new float3(0.03812378f, -0.6523611f, 2.28750157f), new float3(0.03812378f, -0.6523611f, -1.82999849f), new float3(-0.03812622f, 1.02513885f, 2.28750157f), new float3(-0.03812622f, 1.02513885f, -1.82999849f), new float3(-0.03812622f, -0.6523611f, -1.82999849f), new float3(-0.03812622f, -0.6523611f, 2.28750157f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 1, 4, 7, 2, -1, 5, 0, 3, 6, -1, 6, 3, 2, 7, -1, 0, 5, 4, 1, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(1f, 0f, 0f, -0.07624878f),
					new float4(-1f, 0f, 0f, -0.07625122f),
					new float4(0f, -1f, 0f, -0.0296528433f),
					new float4(0f, 0.707106769f, 0.707106769f, -1.4917016f),
					new float4(0f, 0.707106769f, -0.707106769f, -1.48570883f),
					new float4(0f, 1f, 0f, -0.0465971567f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(80.32938f, -2.10534716f, 36.8245125f, 1f)), name: "Solid 119038 #1832")
					.WithMesh(new float3[] { new float3(0.07624878f, 0.0465971567f, -2.0545125f), new float3(0.07624878f, 0.0465971567f, 2.06298757f), new float3(0.07624878f, -0.0296528433f, 2.1392374f), new float3(0.07624878f, -0.0296528433f, -2.13076258f), new float3(-0.07625122f, 0.0465971567f, 2.06298757f), new float3(-0.07625122f, 0.0465971567f, -2.0545125f), new float3(-0.07625122f, -0.0296528433f, -2.13076258f), new float3(-0.07625122f, -0.0296528433f, 2.1392374f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 6, 3, 2, 7, -1, 1, 4, 7, 2, -1, 5, 0, 3, 6, -1, 0, 5, 4, 1, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(1f, 0f, 0f, -0.06778015f),
					new float4(-1f, 0f, 0f, -0.08471985f),
					new float4(0f, 0f, 1f, -0.033887025f),
					new float4(0f, -0.707106769f, -0.707106769f, -0.485253364f),
					new float4(0f, 0.707106769f, -0.707106769f, -0.760828555f),
					new float4(0f, 0f, -1f, -0.0423629768f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(80.3378448f, -1.41486108f, 38.929863f, 1f)), name: "Solid 119039 #1833")
					.WithMesh(new float3[] { new float3(0.06778015f, -0.64388895f, -0.0423629768f), new float3(0.06778015f, 1.033611f, -0.0423629768f), new float3(0.06778015f, 1.10986114f, 0.033887025f), new float3(0.06778015f, -0.7201389f, 0.033887025f), new float3(-0.08471985f, 1.033611f, -0.0423629768f), new float3(-0.08471985f, -0.64388895f, -0.0423629768f), new float3(-0.08471985f, -0.7201389f, 0.033887025f), new float3(-0.08471985f, 1.10986114f, 0.033887025f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 2, 7, 6, 3, -1, 5, 0, 3, 6, -1, 1, 4, 7, 2, -1, 4, 1, 0, 5, -1 }))
				;
		}

		// Near (58.452, -6.634, 55.052): the mesh has a triangle the exact CSG does not make
		static ContentsScene DetailsSite26()
		{
			return new ContentsScene().WithContentsList(kMapContents)
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(-1f, 0f, 0f, -0.0381253064f),
					new float4(1f, 0f, 0f, -0.0381246954f),
					new float4(0f, 0f, -1f, -1.01666582f),
					new float4(0f, 0f, 1f, -1.27083421f),
					new float4(0f, -1f, 0f, -0.474444419f),
					new float4(0f, 1f, 0f, -0.7455556f),
				}, contents: 2, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(58.4456253f, -6.15930557f, 53.7816658f, 1f)), name: "Glass 126336 #3383")
					.WithMesh(new float3[] { new float3(-0.0381253064f, 0.7455556f, 1.27083421f), new float3(-0.0381253064f, 0.7455556f, -1.01666582f), new float3(-0.0381253064f, -0.474444419f, -1.01666582f), new float3(-0.0381253064f, -0.474444419f, 1.27083421f), new float3(0.0381246954f, 0.7455556f, -1.01666582f), new float3(0.0381246954f, 0.7455556f, 1.27083421f), new float3(0.0381246954f, -0.474444419f, 1.27083421f), new float3(0.0381246954f, -0.474444419f, -1.01666582f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 1, 4, 7, 2, -1, 5, 0, 3, 6, -1, 6, 3, 2, 7, -1, 0, 5, 4, 1, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(-1f, 0f, 0f, -0.07625031f),
					new float4(1f, 0f, 0f, -0.0762497f),
					new float4(0f, -1f, 0f, -0.0296525583f),
					new float4(0f, 0.707106769f, -0.707106769f, -0.748846531f),
					new float4(0f, 0.707106769f, 0.707106769f, -0.934559f),
					new float4(0f, 1f, 0f, -0.0465974435f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(58.4456253f, -6.68034744f, 53.77743f, 1f)), name: "Solid 126337 #3384")
					.WithMesh(new float3[] { new float3(-0.07625031f, 0.0465974435f, 1.27506852f), new float3(-0.07625031f, 0.0465974435f, -1.0124315f), new float3(-0.07625031f, -0.0296525583f, -1.08868146f), new float3(-0.07625031f, -0.0296525583f, 1.3513186f), new float3(0.0762497f, 0.0465974435f, -1.0124315f), new float3(0.0762497f, 0.0465974435f, 1.27506852f), new float3(0.0762497f, -0.0296525583f, 1.3513186f), new float3(0.0762497f, -0.0296525583f, -1.08868146f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 6, 3, 2, 7, -1, 1, 4, 7, 2, -1, 5, 0, 3, 6, -1, 0, 5, 4, 1, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(-1f, 0f, 0f, -0.08472275f),
					new float4(1f, 0f, 0f, -0.06777725f),
					new float4(0f, 0f, 1f, -0.0338899232f),
					new float4(0f, -0.707106769f, -0.707106769f, -0.359445035f),
					new float4(0f, 0.707106769f, -0.707106769f, -0.5631314f),
					new float4(0f, 0f, -1f, -0.04236008f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(58.4540977f, -6.167778f, 55.09486f, 1f)), name: "Solid 126339 #3386")
					.WithMesh(new float3[] { new float3(-0.08472275f, 0.754027963f, -0.04236008f), new float3(-0.08472275f, -0.465971977f, -0.04236008f), new float3(-0.08472275f, -0.542221963f, 0.0338899232f), new float3(-0.08472275f, 0.830278f, 0.0338899232f), new float3(0.06777725f, -0.465971977f, -0.04236008f), new float3(0.06777725f, 0.754027963f, -0.04236008f), new float3(0.06777725f, 0.830278f, 0.0338899232f), new float3(0.06777725f, -0.542221963f, 0.0338899232f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 6, 3, 2, 7, -1, 1, 4, 7, 2, -1, 5, 0, 3, 6, -1, 0, 5, 4, 1, -1 }))
				;
		}

		// Near (58.471, -5.82, 46.055): the exact CSG makes a triangle the mesh does not have
		static ContentsScene DetailsSite27()
		{
			return new ContentsScene().WithContentsList(kMapContents)
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(-1f, 0f, 0f, -0.07625031f),
					new float4(1f, 0f, 0f, -0.0762497f),
					new float4(0f, 1f, 0f, -0.038125135f),
					new float4(0f, -0.707106769f, -0.707106769f, -0.652992368f),
					new float4(0f, -0.707106769f, 0.707106769f, -1.01843107f),
					new float4(0f, -1f, 0f, -0.0381248668f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(58.4456253f, -5.375625f, 46.9403458f, 1f)), name: "Solid 126412 #3388")
					.WithMesh(new float3[] { new float3(-0.07625031f, -0.0381248668f, -0.8853458f), new float3(-0.07625031f, -0.0381248668f, 1.40215421f), new float3(-0.07625031f, 0.038125135f, 1.47840416f), new float3(-0.07625031f, 0.038125135f, -0.961595833f), new float3(0.0762497f, -0.0381248668f, 1.40215421f), new float3(0.0762497f, -0.0381248668f, -0.8853458f), new float3(0.0762497f, 0.038125135f, -0.961595833f), new float3(0.0762497f, 0.038125135f, 1.47840416f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 2, 7, 6, 3, -1, 5, 0, 3, 6, -1, 1, 4, 7, 2, -1, 4, 1, 0, 5, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(-1f, 0f, 0f, -0.0677778646f),
					new float4(1f, 0f, 0f, -0.08472214f),
					new float4(0f, 0f, -1f, -0.03388809f),
					new float4(0f, -0.707106769f, 0.707106769f, -0.359446347f),
					new float4(0f, 0.707106769f, 0.707106769f, -0.5631327f),
					new float4(0f, 0f, 1f, -0.0423619077f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(58.4371529f, -6.167778f, 46.0126381f, 1f)), name: "Solid 126413 #3389")
					.WithMesh(new float3[] { new float3(-0.0677778646f, -0.465972f, 0.0423619077f), new float3(-0.0677778646f, 0.754028f, 0.0423619077f), new float3(-0.0677778646f, 0.830278039f, -0.03388809f), new float3(-0.0677778646f, -0.542222f, -0.03388809f), new float3(0.08472214f, 0.754028f, 0.0423619077f), new float3(0.08472214f, -0.465972f, 0.0423619077f), new float3(0.08472214f, -0.542222f, -0.03388809f), new float3(0.08472214f, 0.830278039f, -0.03388809f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 2, 7, 6, 3, -1, 5, 0, 3, 6, -1, 1, 4, 7, 2, -1, 4, 1, 0, 5, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(-1f, 0f, 0f, -0.0381253064f),
					new float4(1f, 0f, 0f, -0.0381246954f),
					new float4(0f, 0f, -1f, -1.01666677f),
					new float4(0f, 0f, 1f, -1.27083325f),
					new float4(0f, -1f, 0f, -0.474444419f),
					new float4(0f, 1f, 0f, -0.7455556f),
				}, contents: 2, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(58.4456253f, -6.15930557f, 47.0716667f, 1f)), name: "Glass 126414 #3390")
					.WithMesh(new float3[] { new float3(-0.0381253064f, 0.7455556f, 1.27083325f), new float3(-0.0381253064f, 0.7455556f, -1.01666677f), new float3(-0.0381253064f, -0.474444419f, -1.01666677f), new float3(-0.0381253064f, -0.474444419f, 1.27083325f), new float3(0.0381246954f, 0.7455556f, -1.01666677f), new float3(0.0381246954f, 0.7455556f, 1.27083325f), new float3(0.0381246954f, -0.474444419f, 1.27083325f), new float3(0.0381246954f, -0.474444419f, -1.01666677f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 1, 4, 7, 2, -1, 5, 0, 3, 6, -1, 6, 3, 2, 7, -1, 0, 5, 4, 1, -1 }))
				;
		}

		// Near (65.066, -1.22, 109.571): the exact CSG makes a triangle the mesh does not have
		static ContentsScene DetailsSite28()
		{
			return new ContentsScene().WithContentsList(kMapContents)
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, -1f, 0f, -0.88958323f),
					new float4(0.9817799f, 0f, -0.1900219f, -1.27514911f),
					new float4(-1f, 0f, 0f, -1.27400863f),
					new float4(0.101660356f, 0f, 0.994819164f, -0.3350967f),
					new float4(-0.09875158f, 0f, -0.9951121f, -0.2680791f),
					new float4(0f, 1f, 0f, -1.39791679f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(57.39401f, -3.99041677f, 109.104218f, 1f)), name: "Solid 1318334 #5661")
					.WithMesh(new float3[] { new float3(-1.27400863f, -0.88958323f, -0.142967537f), new float3(1.22317886f, -0.88958323f, -0.390780032f), new float3(1.33755386f, -0.88958323f, 0.200157478f), new float3(-1.27400863f, -0.88958323f, 0.467032462f), new float3(1.22317886f, 1.39791679f, -0.390780032f), new float3(1.33755386f, 1.39791679f, 0.200157478f), new float3(-1.27400863f, 1.39791679f, 0.467032462f), new float3(-1.27400863f, 1.39791679f, -0.142967537f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 2, 1, -1, 6, 7, 0, 3, -1, 5, 6, 3, 2, -1, 7, 4, 1, 0, -1, 4, 7, 6, 5, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 1f, 0f, -0.9488889f),
					new float4(0.9817799f, 0f, -0.1900219f, -1.27514911f),
					new float4(-1f, 0f, 0f, -1.27400863f),
					new float4(0.101660356f, 0f, 0.994819164f, -0.3350967f),
					new float4(-0.09875158f, 0f, -0.9951121f, -0.2680791f),
					new float4(0f, -1f, 0f, -1.49111116f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(57.39401f, 0.2711111f, 109.104218f, 1f)), name: "Solid 1318491 #5683")
					.WithMesh(new float3[] { new float3(1.22317886f, 0.9488889f, -0.390780032f), new float3(-1.27400863f, 0.9488889f, -0.142967537f), new float3(-1.27400863f, 0.9488889f, 0.467032462f), new float3(1.33755386f, 0.9488889f, 0.200157478f), new float3(1.33755386f, -1.49111116f, 0.200157478f), new float3(1.22317886f, -1.49111116f, -0.390780032f), new float3(-1.27400863f, -1.49111116f, -0.142967537f), new float3(-1.27400863f, -1.49111116f, 0.467032462f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 0, 3, -1, 6, 7, 2, 1, -1, 7, 4, 3, 2, -1, 5, 6, 1, 0, -1, 6, 5, 4, 7, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 1f, 0f, -2.84666657f),
					new float4(0f, -1f, 0f, -3.25333333f),
					new float4(-0.101660356f, 0f, -0.994819164f, -0.363808751f),
					new float4(0f, 0f, 1f, -0.106749572f),
					new float4(0.024683835f, 0f, -0.9996953f, -0.110419489f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(60.74329f, -1.62666667f, 109.4645f, 1f)), name: "Solid 1319242 #5691")
					.WithMesh(new float3[] { new float3(8.79671f, 2.84666657f, 0.106749572f), new float3(-2.01172733f, 2.84666657f, -0.160125419f), new float3(-4.62328959f, 2.84666657f, 0.106749572f), new float3(-2.01172733f, -3.25333333f, -0.160125419f), new float3(8.79671f, -3.25333333f, 0.106749572f), new float3(-4.62328959f, -3.25333333f, 0.106749572f) }, new int[] { 0, 1, 2, -1, 3, 4, 5, -1, 1, 3, 5, 2, -1, 4, 0, 2, 5, -1, 0, 4, 3, 1, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 1f, 0f, -3.25333333f),
					new float4(0f, -1f, 0f, -2.84666681f),
					new float4(-0.6308656f, 0f, -0.775892138f, -1.4731077f),
					new float4(-0.355374336f, 0f, 0.9347241f, -0.9100123f),
					new float4(0.707106769f, 0f, -0.707106769f, -0.871658862f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(65.98039f, -2.0333333f, 107.244354f, 1f)), name: "Solid 1319264 #5694")
					.WithMesh(new float3[] { new float3(3.55960751f, 3.25333333f, 2.32689571f), new float3(-0.36726743f, 3.25333333f, -1.59997928f), new float3(-2.40695477f, 3.25333333f, 0.0584582239f), new float3(-0.36726743f, -2.84666681f, -1.59997928f), new float3(3.55960751f, -2.84666681f, 2.32689571f), new float3(-2.40695477f, -2.84666681f, 0.0584582239f) }, new int[] { 0, 1, 2, -1, 3, 4, 5, -1, 1, 3, 5, 2, -1, 4, 0, 2, 5, -1, 0, 4, 3, 1, -1 }))
				;
		}

		// Near (89.772, -11.819, 53.985): the exact CSG makes a triangle the mesh does not have
		static ContentsScene DetailsSite29()
		{
			return new ContentsScene().WithContentsList(kMapContents)
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(1f, 0f, 0f, -0.03812195f),
					new float4(-1f, 0f, 0f, -0.0381280519f),
					new float4(0f, 0f, 1f, -2.28750038f),
					new float4(0f, 0f, -1f, -1.82999969f),
					new float4(0f, -1f, 0f, -0.6523615f),
					new float4(0f, 1f, 0f, -1.0251385f),
				}, contents: 2, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(89.78438f, -11.1663885f, 53.07f, 1f)), name: "Glass 149640 #1872")
					.WithMesh(new float3[] { new float3(0.03812195f, 1.0251385f, -1.82999969f), new float3(0.03812195f, 1.0251385f, 2.28750038f), new float3(0.03812195f, -0.6523615f, 2.28750038f), new float3(0.03812195f, -0.6523615f, -1.82999969f), new float3(-0.0381280519f, 1.0251385f, 2.28750038f), new float3(-0.0381280519f, 1.0251385f, -1.82999969f), new float3(-0.0381280519f, -0.6523615f, -1.82999969f), new float3(-0.0381280519f, -0.6523615f, 2.28750038f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 1, 4, 7, 2, -1, 5, 0, 3, 6, -1, 6, 3, 2, 7, -1, 0, 5, 4, 1, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(1f, 0f, 0f, -0.07624695f),
					new float4(-1f, 0f, 0f, -0.07625305f),
					new float4(0f, -1f, 0f, -0.029653091f),
					new float4(0f, 0.707106769f, 0.707106769f, -1.49170065f),
					new float4(0f, 0.707106769f, -0.707106769f, -1.48570955f),
					new float4(0f, 1f, 0f, -0.0465969071f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(89.78438f, -11.8653469f, 53.2945137f, 1f)), name: "Solid 149641 #1873")
					.WithMesh(new float3[] { new float3(0.07624695f, 0.0465969071f, -2.05451369f), new float3(0.07624695f, 0.0465969071f, 2.06298637f), new float3(0.07624695f, -0.029653091f, 2.13923645f), new float3(0.07624695f, -0.029653091f, -2.13076377f), new float3(-0.07625305f, 0.0465969071f, 2.06298637f), new float3(-0.07625305f, 0.0465969071f, -2.05451369f), new float3(-0.07625305f, -0.029653091f, -2.13076377f), new float3(-0.07625305f, -0.029653091f, 2.13923645f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 6, 3, 2, 7, -1, 1, 4, 7, 2, -1, 5, 0, 3, 6, -1, 0, 5, 4, 1, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(1f, 0f, 0f, -0.06777832f),
					new float4(-1f, 0f, 0f, -0.08472168f),
					new float4(0f, 0f, 1f, -0.0338896178f),
					new float4(0f, -0.707106769f, -0.707106769f, -0.485251635f),
					new float4(0f, 0.707106769f, -0.707106769f, -0.760826647f),
					new float4(0f, 0f, -1f, -0.04236038f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(89.79285f, -11.174861f, 55.39986f, 1f)), name: "Solid 149642 #1874")
					.WithMesh(new float3[] { new float3(0.06777832f, -0.64388907f, -0.04236038f), new float3(0.06777832f, 1.033611f, -0.04236038f), new float3(0.06777832f, 1.109861f, 0.0338896178f), new float3(0.06777832f, -0.7201391f, 0.0338896178f), new float3(-0.08472168f, 1.033611f, -0.04236038f), new float3(-0.08472168f, -0.64388907f, -0.04236038f), new float3(-0.08472168f, -0.7201391f, 0.0338896178f), new float3(-0.08472168f, 1.109861f, 0.0338896178f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 2, 7, 6, 3, -1, 5, 0, 3, 6, -1, 1, 4, 7, 2, -1, 4, 1, 0, 5, -1 }))
				;
		}

		// Near (89.772, -11.26, 44.378): the exact CSG makes a triangle the mesh does not have
		static ContentsScene DetailsSite30()
		{
			return new ContentsScene().WithContentsList(kMapContents)
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(1f, 0f, 0f, -0.03812195f),
					new float4(-1f, 0f, 0f, -0.0381280519f),
					new float4(0f, 0f, 1f, -2.2875f),
					new float4(0f, 0f, -1f, -1.83000016f),
					new float4(0f, -1f, 0f, -0.6523615f),
					new float4(0f, 1f, 0f, -1.0251385f),
				}, contents: 2, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(89.78438f, -11.1663885f, 42.09f, 1f)), name: "Glass 154036 #1882")
					.WithMesh(new float3[] { new float3(0.03812195f, 1.0251385f, -1.83000016f), new float3(0.03812195f, 1.0251385f, 2.2875f), new float3(0.03812195f, -0.6523615f, 2.2875f), new float3(0.03812195f, -0.6523615f, -1.83000016f), new float3(-0.0381280519f, 1.0251385f, 2.2875f), new float3(-0.0381280519f, 1.0251385f, -1.83000016f), new float3(-0.0381280519f, -0.6523615f, -1.83000016f), new float3(-0.0381280519f, -0.6523615f, 2.2875f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 1, 4, 7, 2, -1, 5, 0, 3, 6, -1, 6, 3, 2, 7, -1, 0, 5, 4, 1, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(1f, 0f, 0f, -0.07624695f),
					new float4(-1f, 0f, 0f, -0.07625305f),
					new float4(0f, -1f, 0f, -0.029653091f),
					new float4(0f, 0.707106769f, 0.707106769f, -1.49170029f),
					new float4(0f, 0.707106769f, -0.707106769f, -1.48570991f),
					new float4(0f, 1f, 0f, -0.0465969071f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(89.78438f, -11.8653469f, 42.3145142f, 1f)), name: "Solid 154037 #1883")
					.WithMesh(new float3[] { new float3(0.07624695f, 0.0465969071f, -2.05451417f), new float3(0.07624695f, 0.0465969071f, 2.062986f), new float3(0.07624695f, -0.029653091f, 2.139236f), new float3(0.07624695f, -0.029653091f, -2.13076425f), new float3(-0.07625305f, 0.0465969071f, 2.062986f), new float3(-0.07625305f, 0.0465969071f, -2.05451417f), new float3(-0.07625305f, -0.029653091f, -2.13076425f), new float3(-0.07625305f, -0.029653091f, 2.139236f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 6, 3, 2, 7, -1, 1, 4, 7, 2, -1, 5, 0, 3, 6, -1, 0, 5, 4, 1, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(1f, 0f, 0f, -0.06777832f),
					new float4(-1f, 0f, 0f, -0.08472168f),
					new float4(0f, 0f, 1f, -0.03388916f),
					new float4(0f, -0.707106769f, -0.707106769f, -0.485251933f),
					new float4(0f, 0.707106769f, -0.707106769f, -0.760826945f),
					new float4(0f, 0f, -1f, -0.04236084f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(89.79285f, -11.174861f, 44.41986f, 1f)), name: "Solid 154038 #1884")
					.WithMesh(new float3[] { new float3(0.06777832f, -0.643889f, -0.04236084f), new float3(0.06777832f, 1.03361094f, -0.04236084f), new float3(0.06777832f, 1.109861f, 0.03388916f), new float3(0.06777832f, -0.720139f, 0.03388916f), new float3(-0.08472168f, 1.03361094f, -0.04236084f), new float3(-0.08472168f, -0.643889f, -0.04236084f), new float3(-0.08472168f, -0.720139f, 0.03388916f), new float3(-0.08472168f, 1.109861f, 0.03388916f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 2, 7, 6, 3, -1, 5, 0, 3, 6, -1, 1, 4, 7, 2, -1, 4, 1, 0, 5, -1 }))
				;
		}

		// Near (59.208, -0.94, 95.287): the exact CSG makes a triangle the mesh does not have
		static ContentsScene DetailsSite31()
		{
			return new ContentsScene().WithContentsList(kMapContents)
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 0f, -1f, -0.0677813739f),
					new float4(0f, 0f, 1f, -0.08471863f),
					new float4(0f, 1f, 0f, -0.03812499f),
					new float4(0.707106769f, -0.707106769f, 0f, -0.7488446f),
					new float4(-0.707106769f, -0.707106769f, 0f, -0.92257905f),
					new float4(0f, -1f, 0f, -0.0381250121f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(60.4747238f, -0.495625f, 95.30403f, 1f)), name: "Solid 346548 #2445")
					.WithMesh(new float3[] { new float3(1.0209012f, -0.0381250121f, -0.0677813739f), new float3(-1.26659882f, -0.0381250121f, -0.0677813739f), new float3(-1.34284878f, 0.03812499f, -0.0677813739f), new float3(1.09715128f, 0.03812499f, -0.0677813739f), new float3(-1.26659882f, -0.0381250121f, 0.08471863f), new float3(1.0209012f, -0.0381250121f, 0.08471863f), new float3(1.09715128f, 0.03812499f, 0.08471863f), new float3(-1.34284878f, 0.03812499f, 0.08471863f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 2, 7, 6, 3, -1, 5, 0, 3, 6, -1, 1, 4, 7, 2, -1, 4, 1, 0, 5, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 0f, -1f, -0.08471863f),
					new float4(0f, 0f, 1f, -0.0677813739f),
					new float4(-1f, 0f, 0f, -0.0338888541f),
					new float4(0.707106769f, -0.707106769f, 0f, -0.359445959f),
					new float4(0.707106769f, 0.707106769f, 0f, -0.563132f),
					new float4(1f, 0f, 0f, -0.042361144f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(59.1657639f, -1.28777778f, 95.32097f, 1f)), name: "Solid 346549 #2446")
					.WithMesh(new float3[] { new float3(0.042361144f, 0.7540278f, -0.08471863f), new float3(0.042361144f, -0.465972215f, -0.08471863f), new float3(-0.0338888541f, -0.5422222f, -0.08471863f), new float3(-0.0338888541f, 0.830277741f, -0.08471863f), new float3(0.042361144f, -0.465972215f, 0.0677813739f), new float3(0.042361144f, 0.7540278f, 0.0677813739f), new float3(-0.0338888541f, 0.830277741f, 0.0677813739f), new float3(-0.0338888541f, -0.5422222f, 0.0677813739f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 6, 3, 2, 7, -1, 1, 4, 7, 2, -1, 5, 0, 3, 6, -1, 0, 5, 4, 1, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 1f, 0f, -0.7455556f),
					new float4(0f, -1f, 0f, -0.474444419f),
					new float4(1f, 0f, 0f, -1.14374971f),
					new float4(-1f, 0f, 0f, -1.14375031f),
					new float4(0f, 0f, -1f, -0.0338906869f),
					new float4(0f, 0f, 1f, -0.0423593149f),
				}, contents: 2, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(60.3518753f, -1.27930558f, 95.3082657f, 1f)), name: "Glass 346756 #2824")
					.WithMesh(new float3[] { new float3(-1.14375031f, 0.7455556f, 0.0423593149f), new float3(1.14374971f, 0.7455556f, 0.0423593149f), new float3(1.14374971f, 0.7455556f, -0.0338906869f), new float3(-1.14375031f, 0.7455556f, -0.0338906869f), new float3(1.14374971f, -0.474444419f, 0.0423593149f), new float3(-1.14375031f, -0.474444419f, 0.0423593149f), new float3(-1.14375031f, -0.474444419f, -0.0338906869f), new float3(1.14374971f, -0.474444419f, -0.0338906869f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 1, 4, 7, 2, -1, 5, 0, 3, 6, -1, 6, 3, 2, 7, -1, 0, 5, 4, 1, -1 }))
				;
		}

		// Near (56.908, 1.22, 4.313): the exact CSG makes a triangle the mesh does not have
		static ContentsScene DetailsSite32()
		{
			return new ContentsScene().WithContentsList(kMapContents)
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 1f, 0f, -0.029652806f),
					new float4(0f, -1f, 0f, -0.0465971939f),
					new float4(0.9828722f, 0f, 0.184288532f, -1.23433781f),
					new float4(-1f, 0f, 0f, -1.23261237f),
					new float4(0.09876663f, 0f, -0.995110631f, -0.170184791f),
					new float4(-0.101058282f, 0f, 0.9948805f, -0.136146f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(57.31468f, 1.19034719f, 4.48520374f, 1f)), name: "Solid 716326 #1543")
					.WithMesh(new float3[] { new float3(-1.23261237f, 0.029652806f, 0.0116398958f), new float3(1.20719683f, 0.029652806f, 0.2594716f), new float3(1.26438427f, 0.029652806f, -0.0455284156f), new float3(-1.23261237f, 0.029652806f, -0.2933601f), new float3(1.20719683f, -0.0465971939f, 0.2594716f), new float3(-1.23261237f, -0.0465971939f, 0.0116398958f), new float3(-1.23261237f, -0.0465971939f, -0.2933601f), new float3(1.26438427f, -0.0465971939f, -0.0455284156f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 1, 4, 7, 2, -1, 5, 0, 3, 6, -1, 6, 3, 2, 7, -1, 0, 5, 4, 1, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 1f, 0f, -0.118611068f),
					new float4(0f, -1f, 0f, -0.186388925f),
					new float4(0.970156848f, 0f, 0.242478162f, -1.11370432f),
					new float4(-0.9701425f, 0f, -0.242535621f, -1.39213789f),
					new float4(0.2887348f, 0f, -0.957409143f, -0.03925811f),
					new float4(-0.2887418f, 0f, 0.957407f, -0.03925854f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(59.9039078f, 1.10138893f, 4.88020658f, 1f)), name: "Solid 716333 #1550")
					.WithMesh(new float3[] { new float3(-1.34390783f, 0.118611068f, -0.36430046f), new float3(1.05796719f, 0.118611068f, 0.36007455f), new float3(1.07702971f, 0.118611068f, 0.283805341f), new float3(-1.32484531f, 0.118611068f, -0.440550476f), new float3(1.05796719f, -0.186388925f, 0.36007455f), new float3(-1.34390783f, -0.186388925f, -0.36430046f), new float3(-1.32484531f, -0.186388925f, -0.440550476f), new float3(1.07702971f, -0.186388925f, 0.283805341f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 1, 4, 7, 2, -1, 5, 0, 3, 6, -1, 6, 3, 2, 7, -1, 0, 5, 4, 1, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 1f, 0f, -0.118611068f),
					new float4(0f, -1f, 0f, -0.186388925f),
					new float4(0.9701425f, 0f, 0.242535621f, -1.23622966f),
					new float4(-1f, 0f, 0f, -1.24329746f),
					new float4(0.09875158f, 0f, -0.9951121f, -0.0426769853f),
					new float4(-0.09950372f, 0f, 0.9950372f, -0.034142185f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(57.3251724f, 1.10138893f, 4.358111f, 1f)), name: "Solid 716334 #1551")
					.WithMesh(new float3[] { new float3(-1.24329746f, 0.118611068f, -0.090017274f), new float3(1.23482764f, 0.118611068f, 0.157795221f), new float3(1.25389f, 0.118611068f, 0.08154523f), new float3(-1.24329746f, 0.118611068f, -0.166267276f), new float3(1.23482764f, -0.186388925f, 0.157795221f), new float3(-1.24329746f, -0.186388925f, -0.090017274f), new float3(-1.24329746f, -0.186388925f, -0.166267276f), new float3(1.25389f, -0.186388925f, 0.08154523f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 1, 4, 7, 2, -1, 5, 0, 3, 6, -1, 6, 3, 2, 7, -1, 0, 5, 4, 1, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, -1f, 0f, -0.1851786f),
					new float4(0f, 1f, 0f, -0.1198214f),
					new float4(-1f, 0f, 0f, -10.9800005f),
					new float4(0f, 0f, 1f, -0.148868829f),
					new float4(0f, 0f, -1f, -0.156131178f),
					new float4(0f, -0.707106769f, 0.707106769f, -0.07445653f),
					new float4(1f, 0f, 0f, -14.6399994f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(41.4418755f, 1.1001786f, 4.349881f, 1f)), name: "Solid 716996 #4603")
					.WithMesh(new float3[] { new float3(14.6399994f, -0.1851786f, -0.156131178f), new float3(14.6399994f, -0.1851786f, -0.07988116f), new float3(-10.9800005f, -0.1851786f, -0.07988116f), new float3(-10.9800005f, -0.1851786f, -0.156131178f), new float3(14.6399994f, 0.1198214f, 0.148868829f), new float3(14.6399994f, 0.1198214f, -0.156131178f), new float3(-10.9800005f, 0.1198214f, -0.156131178f), new float3(-10.9800005f, 0.1198214f, 0.148868829f), new float3(-10.9800005f, 0.0435713939f, 0.148868829f), new float3(14.6399994f, 0.0435713939f, 0.148868829f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 2, 8, 7, 6, 3, -1, 9, 4, 7, 8, -1, 5, 0, 3, 6, -1, 1, 9, 8, 2, -1, 9, 1, 0, 5, 4, -1 }))
				;
		}

		// Near (51.431, 0, 4.041): the exact CSG makes a triangle the mesh does not have
		static ContentsScene DetailsSite33()
		{
			return new ContentsScene().WithContentsList(kMapContents)
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(-1f, 0f, 0f, -0.0338906869f),
					new float4(0f, 0f, 1f, -0.152499765f),
					new float4(0f, 0f, -1f, -0.152500227f),
					new float4(1f, 0f, 0f, -0.0423593149f),
					new float4(-0.707106769f, -0.707106769f, 0f, -0.227650374f),
					new float4(-0.707106769f, 0.707106769f, 0f, -0.3594472f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(51.4645157f, -0.09319445f, 4.04125f, 1f)), name: "Solid 716835 #4450")
					.WithMesh(new float3[] { new float3(-0.0338906869f, 0.474444449f, 0.152499765f), new float3(-0.0338906869f, 0.474444449f, -0.152500227f), new float3(-0.0338906869f, -0.288055569f, -0.152500227f), new float3(-0.0338906869f, -0.288055569f, 0.152499765f), new float3(0.0423593149f, 0.550694466f, 0.152499765f), new float3(0.0423593149f, -0.364305556f, 0.152499765f), new float3(0.0423593149f, 0.550694466f, -0.152500227f), new float3(0.0423593149f, -0.364305556f, -0.152500227f) }, new int[] { 0, 1, 2, 3, -1, 4, 0, 3, 5, -1, 1, 6, 7, 2, -1, 6, 4, 5, 7, -1, 7, 5, 3, 2, -1, 4, 6, 1, 0, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 1f, 0f, -0.0296527687f),
					new float4(0f, 0f, 1f, -0.152499765f),
					new float4(0f, 0f, -1f, -0.152500227f),
					new float4(0f, -1f, 0f, -0.04659723f),
					new float4(-0.707106769f, 0.707106769f, 0f, -5.24191952f),
					new float4(0.707106769f, 0.707106769f, 0f, -6.55389833f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(42.1916656f, -0.410902768f, 4.04125f, 1f)), name: "Solid 716837 #4452")
					.WithMesh(new float3[] { new float3(9.238959f, 0.0296527687f, 0.152499765f), new float3(9.238959f, 0.0296527687f, -0.152500227f), new float3(-7.383541f, 0.0296527687f, -0.152500227f), new float3(-7.383541f, 0.0296527687f, 0.152499765f), new float3(9.315209f, -0.04659723f, 0.152499765f), new float3(-7.459791f, -0.04659723f, 0.152499765f), new float3(9.315209f, -0.04659723f, -0.152500227f), new float3(-7.459791f, -0.04659723f, -0.152500227f) }, new int[] { 0, 1, 2, 3, -1, 4, 0, 3, 5, -1, 1, 6, 7, 2, -1, 6, 4, 5, 7, -1, 7, 5, 3, 2, -1, 4, 6, 1, 0, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 1f, 0f, -0.296527773f),
					new float4(0f, -1f, 0f, -0.465972215f),
					new float4(1f, 0f, 0f, -1.1861105f),
					new float4(0f, 0f, -1f, -0.01906273f),
					new float4(0f, 0f, 1f, -0.0190622713f),
					new float4(-1f, 0f, 0f, -1.48263943f),
				}, contents: 2, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(50.2445145f, 0.08472222f, 4.04125f, 1f)), name: "Glass 716845 #4459")
					.WithMesh(new float3[] { new float3(-1.48263943f, 0.296527773f, -0.01906273f), new float3(-1.48263943f, 0.296527773f, 0.0190622713f), new float3(1.1861105f, 0.296527773f, 0.0190622713f), new float3(1.1861105f, 0.296527773f, -0.01906273f), new float3(-1.48263943f, -0.465972215f, 0.0190622713f), new float3(-1.48263943f, -0.465972215f, -0.01906273f), new float3(1.1861105f, -0.465972215f, -0.01906273f), new float3(1.1861105f, -0.465972215f, 0.0190622713f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 2, 7, 6, 3, -1, 5, 0, 3, 6, -1, 1, 4, 7, 2, -1, 4, 1, 0, 5, -1 }))
				;
		}

		// Near (80.342, -2.059, 59.475): the exact CSG makes a triangle the mesh does not have
		static ContentsScene DetailsSite34()
		{
			return new ContentsScene().WithContentsList(kMapContents)
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(1f, 0f, 0f, -0.03812378f),
					new float4(-1f, 0f, 0f, -0.03812622f),
					new float4(0f, 0f, 1f, -2.28749871f),
					new float4(0f, 0f, -1f, -1.83000135f),
					new float4(0f, -1f, 0f, -0.6523611f),
					new float4(0f, 1f, 0f, -1.02513885f),
				}, contents: 2, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(80.32938f, -1.40638888f, 58.56f, 1f)), name: "Glass 86214 #1751")
					.WithMesh(new float3[] { new float3(0.03812378f, 1.02513885f, -1.83000135f), new float3(0.03812378f, 1.02513885f, 2.28749871f), new float3(0.03812378f, -0.6523611f, 2.28749871f), new float3(0.03812378f, -0.6523611f, -1.83000135f), new float3(-0.03812622f, 1.02513885f, 2.28749871f), new float3(-0.03812622f, 1.02513885f, -1.83000135f), new float3(-0.03812622f, -0.6523611f, -1.83000135f), new float3(-0.03812622f, -0.6523611f, 2.28749871f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 1, 4, 7, 2, -1, 5, 0, 3, 6, -1, 6, 3, 2, 7, -1, 0, 5, 4, 1, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(1f, 0f, 0f, -0.07624878f),
					new float4(-1f, 0f, 0f, -0.07625122f),
					new float4(0f, -1f, 0f, -0.0296528433f),
					new float4(0f, 0.707106769f, 0.707106769f, -1.49169958f),
					new float4(0f, 0.707106769f, -0.707106769f, -1.485711f),
					new float4(0f, 1f, 0f, -0.0465971567f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(80.32938f, -2.10534716f, 58.7845154f, 1f)), name: "Solid 86215 #1752")
					.WithMesh(new float3[] { new float3(0.07624878f, 0.0465971567f, -2.0545156f), new float3(0.07624878f, 0.0465971567f, 2.0629847f), new float3(0.07624878f, -0.0296528433f, 2.13923454f), new float3(0.07624878f, -0.0296528433f, -2.13076544f), new float3(-0.07625122f, 0.0465971567f, 2.0629847f), new float3(-0.07625122f, 0.0465971567f, -2.0545156f), new float3(-0.07625122f, -0.0296528433f, -2.13076544f), new float3(-0.07625122f, -0.0296528433f, 2.13923454f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 6, 3, 2, 7, -1, 1, 4, 7, 2, -1, 5, 0, 3, 6, -1, 0, 5, 4, 1, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(1f, 0f, 0f, -0.06778015f),
					new float4(-1f, 0f, 0f, -0.08471985f),
					new float4(0f, 0f, 1f, -0.0338879377f),
					new float4(0f, -0.707106769f, -0.707106769f, -0.4852527f),
					new float4(0f, 0.707106769f, -0.707106769f, -0.7608279f),
					new float4(0f, 0f, -1f, -0.04236206f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(80.3378448f, -1.41486108f, 60.8898621f, 1f)), name: "Solid 86216 #1753")
					.WithMesh(new float3[] { new float3(0.06778015f, -0.6438889f, -0.04236206f), new float3(0.06778015f, 1.033611f, -0.04236206f), new float3(0.06778015f, 1.10986114f, 0.0338879377f), new float3(0.06778015f, -0.7201389f, 0.0338879377f), new float3(-0.08471985f, 1.033611f, -0.04236206f), new float3(-0.08471985f, -0.6438889f, -0.04236206f), new float3(-0.08471985f, -0.7201389f, 0.0338879377f), new float3(-0.08471985f, 1.10986114f, 0.0338879377f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 2, 7, 6, 3, -1, 5, 0, 3, 6, -1, 1, 4, 7, 2, -1, 4, 1, 0, 5, -1 }))
				;
		}

		// Near (80.342, -2.059, 48.495): the exact CSG makes a triangle the mesh does not have
		static ContentsScene DetailsSite35()
		{
			return new ContentsScene().WithContentsList(kMapContents)
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(1f, 0f, 0f, -0.03812378f),
					new float4(-1f, 0f, 0f, -0.03812622f),
					new float4(0f, 0f, 1f, -2.28749824f),
					new float4(0f, 0f, -1f, -1.83000183f),
					new float4(0f, -1f, 0f, -0.6523611f),
					new float4(0f, 1f, 0f, -1.02513885f),
				}, contents: 2, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(80.32938f, -1.40638888f, 47.58f, 1f)), name: "Glass 86249 #1761")
					.WithMesh(new float3[] { new float3(0.03812378f, 1.02513885f, -1.83000183f), new float3(0.03812378f, 1.02513885f, 2.28749824f), new float3(0.03812378f, -0.6523611f, 2.28749824f), new float3(0.03812378f, -0.6523611f, -1.83000183f), new float3(-0.03812622f, 1.02513885f, 2.28749824f), new float3(-0.03812622f, 1.02513885f, -1.83000183f), new float3(-0.03812622f, -0.6523611f, -1.83000183f), new float3(-0.03812622f, -0.6523611f, 2.28749824f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 1, 4, 7, 2, -1, 5, 0, 3, 6, -1, 6, 3, 2, 7, -1, 0, 5, 4, 1, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(1f, 0f, 0f, -0.07624878f),
					new float4(-1f, 0f, 0f, -0.07625122f),
					new float4(0f, -1f, 0f, -0.0296528433f),
					new float4(0f, 0.707106769f, 0.707106769f, -1.491702f),
					new float4(0f, 0.707106769f, -0.707106769f, -1.48570859f),
					new float4(0f, 1f, 0f, -0.0465971567f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(80.32938f, -2.10534716f, 47.804512f, 1f)), name: "Solid 86250 #1762")
					.WithMesh(new float3[] { new float3(0.07624878f, 0.0465971567f, -2.054512f), new float3(0.07624878f, 0.0465971567f, 2.062988f), new float3(0.07624878f, -0.0296528433f, 2.139238f), new float3(0.07624878f, -0.0296528433f, -2.130762f), new float3(-0.07625122f, 0.0465971567f, 2.062988f), new float3(-0.07625122f, 0.0465971567f, -2.054512f), new float3(-0.07625122f, -0.0296528433f, -2.130762f), new float3(-0.07625122f, -0.0296528433f, 2.139238f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 6, 3, 2, 7, -1, 1, 4, 7, 2, -1, 5, 0, 3, 6, -1, 0, 5, 4, 1, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(1f, 0f, 0f, -0.06778015f),
					new float4(-1f, 0f, 0f, -0.08471985f),
					new float4(0f, 0f, 1f, -0.0338874832f),
					new float4(0f, -0.707106769f, -0.707106769f, -0.485253036f),
					new float4(0f, 0.707106769f, -0.707106769f, -0.760828257f),
					new float4(0f, 0f, -1f, -0.04236252f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(80.3378448f, -1.41486108f, 49.9098625f, 1f)), name: "Solid 86251 #1763")
					.WithMesh(new float3[] { new float3(0.06778015f, -0.6438889f, -0.04236252f), new float3(0.06778015f, 1.03361118f, -0.04236252f), new float3(0.06778015f, 1.10986114f, 0.0338874832f), new float3(0.06778015f, -0.7201389f, 0.0338874832f), new float3(-0.08471985f, 1.03361118f, -0.04236252f), new float3(-0.08471985f, -0.6438889f, -0.04236252f), new float3(-0.08471985f, -0.7201389f, 0.0338874832f), new float3(-0.08471985f, 1.10986114f, 0.0338874832f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 2, 7, 6, 3, -1, 5, 0, 3, 6, -1, 1, 4, 7, 2, -1, 4, 1, 0, 5, -1 }))
				;
		}

		// Near (80.342, -2.059, 53.985): the exact CSG makes a triangle the mesh does not have
		static ContentsScene DetailsSite36()
		{
			return new ContentsScene().WithContentsList(kMapContents)
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(1f, 0f, 0f, -0.03812378f),
					new float4(-1f, 0f, 0f, -0.03812622f),
					new float4(0f, 0f, 1f, -2.28750038f),
					new float4(0f, 0f, -1f, -1.82999969f),
					new float4(0f, -1f, 0f, -0.6523611f),
					new float4(0f, 1f, 0f, -1.02513885f),
				}, contents: 2, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(80.32938f, -1.40638888f, 53.07f, 1f)), name: "Glass 86257 #1756")
					.WithMesh(new float3[] { new float3(0.03812378f, 1.02513885f, -1.82999969f), new float3(0.03812378f, 1.02513885f, 2.28750038f), new float3(0.03812378f, -0.6523611f, 2.28750038f), new float3(0.03812378f, -0.6523611f, -1.82999969f), new float3(-0.03812622f, 1.02513885f, 2.28750038f), new float3(-0.03812622f, 1.02513885f, -1.82999969f), new float3(-0.03812622f, -0.6523611f, -1.82999969f), new float3(-0.03812622f, -0.6523611f, 2.28750038f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 1, 4, 7, 2, -1, 5, 0, 3, 6, -1, 6, 3, 2, 7, -1, 0, 5, 4, 1, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(1f, 0f, 0f, -0.07624878f),
					new float4(-1f, 0f, 0f, -0.07625122f),
					new float4(0f, -1f, 0f, -0.0296528433f),
					new float4(0f, 0.707106769f, 0.707106769f, -1.49170077f),
					new float4(0f, 0.707106769f, -0.707106769f, -1.48570979f),
					new float4(0f, 1f, 0f, -0.0465971567f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(80.32938f, -2.10534716f, 53.2945137f, 1f)), name: "Solid 86258 #1757")
					.WithMesh(new float3[] { new float3(0.07624878f, 0.0465971567f, -2.05451369f), new float3(0.07624878f, 0.0465971567f, 2.06298637f), new float3(0.07624878f, -0.0296528433f, 2.13923645f), new float3(0.07624878f, -0.0296528433f, -2.13076377f), new float3(-0.07625122f, 0.0465971567f, 2.06298637f), new float3(-0.07625122f, 0.0465971567f, -2.05451369f), new float3(-0.07625122f, -0.0296528433f, -2.13076377f), new float3(-0.07625122f, -0.0296528433f, 2.13923645f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 6, 3, 2, 7, -1, 1, 4, 7, 2, -1, 5, 0, 3, 6, -1, 0, 5, 4, 1, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(1f, 0f, 0f, -0.06778015f),
					new float4(-1f, 0f, 0f, -0.08471985f),
					new float4(0f, 0f, 1f, -0.0338896178f),
					new float4(0f, -0.707106769f, -0.707106769f, -0.485251546f),
					new float4(0f, 0.707106769f, -0.707106769f, -0.7608267f),
					new float4(0f, 0f, -1f, -0.04236038f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(80.3378448f, -1.41486108f, 55.39986f, 1f)), name: "Solid 86259 #1758")
					.WithMesh(new float3[] { new float3(0.06778015f, -0.64388895f, -0.04236038f), new float3(0.06778015f, 1.033611f, -0.04236038f), new float3(0.06778015f, 1.10986114f, 0.0338896178f), new float3(0.06778015f, -0.720138967f, 0.0338896178f), new float3(-0.08471985f, 1.033611f, -0.04236038f), new float3(-0.08471985f, -0.64388895f, -0.04236038f), new float3(-0.08471985f, -0.720138967f, 0.0338896178f), new float3(-0.08471985f, 1.10986114f, 0.0338896178f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 2, 7, 6, 3, -1, 5, 0, 3, 6, -1, 1, 4, 7, 2, -1, 4, 1, 0, 5, -1 }))
				;
		}

		// Near (80.304, -2.059, 43.005): the exact CSG makes a triangle the mesh does not have
		static ContentsScene DetailsSite37()
		{
			return new ContentsScene().WithContentsList(kMapContents)
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(1f, 0f, 0f, -0.03812378f),
					new float4(-1f, 0f, 0f, -0.03812622f),
					new float4(0f, 0f, 1f, -2.2875f),
					new float4(0f, 0f, -1f, -1.83000016f),
					new float4(0f, -1f, 0f, -0.6523611f),
					new float4(0f, 1f, 0f, -1.02513885f),
				}, contents: 2, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(80.32938f, -1.40638888f, 42.09f, 1f)), name: "Glass 86279 #1766")
					.WithMesh(new float3[] { new float3(0.03812378f, 1.02513885f, -1.83000016f), new float3(0.03812378f, 1.02513885f, 2.2875f), new float3(0.03812378f, -0.6523611f, 2.2875f), new float3(0.03812378f, -0.6523611f, -1.83000016f), new float3(-0.03812622f, 1.02513885f, 2.2875f), new float3(-0.03812622f, 1.02513885f, -1.83000016f), new float3(-0.03812622f, -0.6523611f, -1.83000016f), new float3(-0.03812622f, -0.6523611f, 2.2875f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 1, 4, 7, 2, -1, 5, 0, 3, 6, -1, 6, 3, 2, 7, -1, 0, 5, 4, 1, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(1f, 0f, 0f, -0.07624878f),
					new float4(-1f, 0f, 0f, -0.07625122f),
					new float4(0f, -1f, 0f, -0.0296528433f),
					new float4(0f, 0.707106769f, 0.707106769f, -1.49170041f),
					new float4(0f, 0.707106769f, -0.707106769f, -1.48571f),
					new float4(0f, 1f, 0f, -0.0465971567f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(80.32938f, -2.10534716f, 42.3145142f, 1f)), name: "Solid 86280 #1767")
					.WithMesh(new float3[] { new float3(0.07624878f, 0.0465971567f, -2.05451417f), new float3(0.07624878f, 0.0465971567f, 2.062986f), new float3(0.07624878f, -0.0296528433f, 2.13923573f), new float3(0.07624878f, -0.0296528433f, -2.13076425f), new float3(-0.07625122f, 0.0465971567f, 2.062986f), new float3(-0.07625122f, 0.0465971567f, -2.05451417f), new float3(-0.07625122f, -0.0296528433f, -2.13076425f), new float3(-0.07625122f, -0.0296528433f, 2.13923573f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 6, 3, 2, 7, -1, 1, 4, 7, 2, -1, 5, 0, 3, 6, -1, 0, 5, 4, 1, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(1f, 0f, 0f, -0.06778015f),
					new float4(-1f, 0f, 0f, -0.08471985f),
					new float4(0f, 0f, 1f, -0.03388916f),
					new float4(0f, -0.707106769f, -0.707106769f, -0.485251844f),
					new float4(0f, 0.707106769f, -0.707106769f, -0.760827065f),
					new float4(0f, 0f, -1f, -0.04236084f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(80.3378448f, -1.41486108f, 44.41986f, 1f)), name: "Solid 86281 #1768")
					.WithMesh(new float3[] { new float3(0.06778015f, -0.6438889f, -0.04236084f), new float3(0.06778015f, 1.03361118f, -0.04236084f), new float3(0.06778015f, 1.10986114f, 0.03388916f), new float3(0.06778015f, -0.7201389f, 0.03388916f), new float3(-0.08471985f, 1.03361118f, -0.04236084f), new float3(-0.08471985f, -0.6438889f, -0.04236084f), new float3(-0.08471985f, -0.7201389f, 0.03388916f), new float3(-0.08471985f, 1.10986114f, 0.03388916f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 2, 7, 6, 3, -1, 5, 0, 3, 6, -1, 1, 4, 7, 2, -1, 4, 1, 0, 5, -1 }))
				;
		}

		// Near (-3.291, 3.126, 142.893): the exact CSG makes a triangle the mesh does not have
		static ContentsScene DetailsSite38()
		{
			return new ContentsScene().WithContentsList(kMapContents)
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(1f, 0f, 0f, -0.07624994f),
					new float4(-1f, 0f, 0f, -0.0762500539f),
					new float4(0f, 1f, 0f, -0.0381249338f),
					new float4(0f, -0.707106769f, 0.707106769f, -0.8387079f),
					new float4(0f, -0.707106769f, -0.707106769f, -0.8327158f),
					new float4(0f, -1f, 0f, -0.0381250679f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(-3.27875f, 3.164375f, 142.125763f, 1f)), name: "Solid 247373 #2000")
					.WithMesh(new float3[] { new float3(0.07624994f, -0.0381250679f, 1.14798713f), new float3(0.07624994f, -0.0381250679f, -1.1395129f), new float3(0.07624994f, 0.0381249338f, -1.215763f), new float3(0.07624994f, 0.0381249338f, 1.22423708f), new float3(-0.0762500539f, -0.0381250679f, -1.1395129f), new float3(-0.0762500539f, -0.0381250679f, 1.14798713f), new float3(-0.0762500539f, 0.0381249338f, 1.22423708f), new float3(-0.0762500539f, 0.0381249338f, -1.215763f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 2, 7, 6, 3, -1, 5, 0, 3, 6, -1, 1, 4, 7, 2, -1, 4, 1, 0, 5, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(1f, 0f, 0f, -0.06777774f),
					new float4(-1f, 0f, 0f, -0.08472226f),
					new float4(0f, 0f, 1f, -0.03388367f),
					new float4(0f, -0.707106769f, -0.707106769f, -0.359449625f),
					new float4(0f, 0.707106769f, -0.707106769f, -0.5631357f),
					new float4(0f, 0f, -1f, -0.0423663333f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(-3.27027774f, 2.37222219f, 143.316116f, 1f)), name: "Solid 247374 #2001")
					.WithMesh(new float3[] { new float3(0.06777774f, -0.465972215f, -0.0423663333f), new float3(0.06777774f, 0.7540278f, -0.0423663333f), new float3(0.06777774f, 0.8302778f, 0.03388367f), new float3(0.06777774f, -0.5422222f, 0.03388367f), new float3(-0.08472226f, 0.7540278f, -0.0423663333f), new float3(-0.08472226f, -0.465972215f, -0.0423663333f), new float3(-0.08472226f, -0.5422222f, 0.03388367f), new float3(-0.08472226f, 0.8302778f, 0.03388367f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 2, 7, 6, 3, -1, 5, 0, 3, 6, -1, 1, 4, 7, 2, -1, 4, 1, 0, 5, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(1f, 0f, 0f, -0.03812494f),
					new float4(-1f, 0f, 0f, -0.0381250568f),
					new float4(0f, 0f, 1f, -1.27083552f),
					new float4(0f, 0f, -1f, -1.01666439f),
					new float4(0f, -1f, 0f, -0.4744444f),
					new float4(0f, 1f, 0f, -0.745555639f),
				}, contents: 2, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(-3.27875f, 2.38069439f, 142.002914f, 1f)), name: "Glass 247375 #2002")
					.WithMesh(new float3[] { new float3(0.03812494f, 0.745555639f, -1.01666439f), new float3(0.03812494f, 0.745555639f, 1.27083552f), new float3(0.03812494f, -0.4744444f, 1.27083552f), new float3(0.03812494f, -0.4744444f, -1.01666439f), new float3(-0.0381250568f, 0.745555639f, 1.27083552f), new float3(-0.0381250568f, 0.745555639f, -1.01666439f), new float3(-0.0381250568f, -0.4744444f, -1.01666439f), new float3(-0.0381250568f, -0.4744444f, 1.27083552f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 1, 4, 7, 2, -1, 5, 0, 3, 6, -1, 6, 3, 2, 7, -1, 0, 5, 4, 1, -1 }))
				;
		}

		// Near (-3.291, 3.126, 138.013): the exact CSG makes a triangle the mesh does not have
		static ContentsScene DetailsSite39()
		{
			return new ContentsScene().WithContentsList(kMapContents)
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(1f, 0f, 0f, -0.07624994f),
					new float4(-1f, 0f, 0f, -0.0762500539f),
					new float4(0f, 1f, 0f, -0.0381249338f),
					new float4(0f, -0.707106769f, 0.707106769f, -0.8387114f),
					new float4(0f, -0.707106769f, -0.707106769f, -0.832712352f),
					new float4(0f, -1f, 0f, -0.0381250679f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(-3.27875f, 3.164375f, 137.245758f, 1f)), name: "Solid 247398 #2005")
					.WithMesh(new float3[] { new float3(0.07624994f, -0.0381250679f, 1.147992f), new float3(0.07624994f, -0.0381250679f, -1.139508f), new float3(0.07624994f, 0.0381249338f, -1.21575809f), new float3(0.07624994f, 0.0381249338f, 1.224242f), new float3(-0.0762500539f, -0.0381250679f, -1.139508f), new float3(-0.0762500539f, -0.0381250679f, 1.147992f), new float3(-0.0762500539f, 0.0381249338f, 1.224242f), new float3(-0.0762500539f, 0.0381249338f, -1.21575809f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 2, 7, 6, 3, -1, 5, 0, 3, 6, -1, 1, 4, 7, 2, -1, 4, 1, 0, 5, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(1f, 0f, 0f, -0.06777774f),
					new float4(-1f, 0f, 0f, -0.08472226f),
					new float4(0f, 0f, 1f, -0.03388855f),
					new float4(0f, -0.707106769f, -0.707106769f, -0.359446168f),
					new float4(0f, 0.707106769f, -0.707106769f, -0.5631322f),
					new float4(0f, 0f, -1f, -0.04236145f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(-3.27027774f, 2.37222219f, 138.436111f, 1f)), name: "Solid 247399 #2006")
					.WithMesh(new float3[] { new float3(0.06777774f, -0.465972215f, -0.04236145f), new float3(0.06777774f, 0.7540278f, -0.04236145f), new float3(0.06777774f, 0.8302778f, 0.03388855f), new float3(0.06777774f, -0.5422222f, 0.03388855f), new float3(-0.08472226f, 0.7540278f, -0.04236145f), new float3(-0.08472226f, -0.465972215f, -0.04236145f), new float3(-0.08472226f, -0.5422222f, 0.03388855f), new float3(-0.08472226f, 0.8302778f, 0.03388855f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 2, 7, 6, 3, -1, 5, 0, 3, 6, -1, 1, 4, 7, 2, -1, 4, 1, 0, 5, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(1f, 0f, 0f, -0.03812494f),
					new float4(-1f, 0f, 0f, -0.0381250568f),
					new float4(0f, 0f, 1f, -1.27084041f),
					new float4(0f, 0f, -1f, -1.0166595f),
					new float4(0f, -1f, 0f, -0.4744444f),
					new float4(0f, 1f, 0f, -0.745555639f),
				}, contents: 2, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(-3.27875f, 2.38069439f, 137.12291f, 1f)), name: "Glass 247400 #2007")
					.WithMesh(new float3[] { new float3(0.03812494f, 0.745555639f, -1.0166595f), new float3(0.03812494f, 0.745555639f, 1.27084041f), new float3(0.03812494f, -0.4744444f, 1.27084041f), new float3(0.03812494f, -0.4744444f, -1.0166595f), new float3(-0.0381250568f, 0.745555639f, 1.27084041f), new float3(-0.0381250568f, 0.745555639f, -1.0166595f), new float3(-0.0381250568f, -0.4744444f, -1.0166595f), new float3(-0.0381250568f, -0.4744444f, 1.27084041f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 1, 4, 7, 2, -1, 5, 0, 3, 6, -1, 6, 3, 2, 7, -1, 0, 5, 4, 1, -1 }))
				;
		}

		// Near (62.258, -0.94, 95.287): the exact CSG makes a triangle the mesh does not have
		static ContentsScene DetailsSite40()
		{
			return new ContentsScene().WithContentsList(kMapContents)
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 0f, -1f, -0.0677813739f),
					new float4(0f, 0f, 1f, -0.08471863f),
					new float4(0f, 1f, 0f, -0.03812499f),
					new float4(0.707106769f, -0.707106769f, 0f, -0.74884516f),
					new float4(-0.707106769f, -0.707106769f, 0f, -0.9225785f),
					new float4(0f, -1f, 0f, -0.0381250121f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(63.5247231f, -0.495625f, 95.30403f, 1f)), name: "Solid 338649 #2441")
					.WithMesh(new float3[] { new float3(1.020902f, -0.0381250121f, -0.0677813739f), new float3(-1.26659811f, -0.0381250121f, -0.0677813739f), new float3(-1.34284806f, 0.03812499f, -0.0677813739f), new float3(1.097152f, 0.03812499f, -0.0677813739f), new float3(-1.26659811f, -0.0381250121f, 0.08471863f), new float3(1.020902f, -0.0381250121f, 0.08471863f), new float3(1.097152f, 0.03812499f, 0.08471863f), new float3(-1.34284806f, 0.03812499f, 0.08471863f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 2, 7, 6, 3, -1, 5, 0, 3, 6, -1, 1, 4, 7, 2, -1, 4, 1, 0, 5, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 0f, -1f, -0.08471863f),
					new float4(0f, 0f, 1f, -0.0677813739f),
					new float4(-1f, 0f, 0f, -0.03388809f),
					new float4(0.707106769f, -0.707106769f, 0f, -0.3594465f),
					new float4(0.707106769f, 0.707106769f, 0f, -0.5631325f),
					new float4(1f, 0f, 0f, -0.0423619077f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(62.2157631f, -1.28777778f, 95.32097f, 1f)), name: "Solid 338652 #2442")
					.WithMesh(new float3[] { new float3(0.0423619077f, 0.7540278f, -0.08471863f), new float3(0.0423619077f, -0.465972215f, -0.08471863f), new float3(-0.03388809f, -0.5422222f, -0.08471863f), new float3(-0.03388809f, 0.830277741f, -0.08471863f), new float3(0.0423619077f, -0.465972215f, 0.0677813739f), new float3(0.0423619077f, 0.7540278f, 0.0677813739f), new float3(-0.03388809f, 0.830277741f, 0.0677813739f), new float3(-0.03388809f, -0.5422222f, 0.0677813739f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 6, 3, 2, 7, -1, 1, 4, 7, 2, -1, 5, 0, 3, 6, -1, 0, 5, 4, 1, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 1f, 0f, -0.7455556f),
					new float4(0f, -1f, 0f, -0.474444419f),
					new float4(1f, 0f, 0f, -1.14375043f),
					new float4(-1f, 0f, 0f, -1.14374959f),
					new float4(0f, 0f, -1f, -0.0338906869f),
					new float4(0f, 0f, 1f, -0.0423593149f),
				}, contents: 2, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(63.4018745f, -1.27930558f, 95.3082657f, 1f)), name: "Glass 346752 #2823")
					.WithMesh(new float3[] { new float3(-1.14374959f, 0.7455556f, 0.0423593149f), new float3(1.14375043f, 0.7455556f, 0.0423593149f), new float3(1.14375043f, 0.7455556f, -0.0338906869f), new float3(-1.14374959f, 0.7455556f, -0.0338906869f), new float3(1.14375043f, -0.474444419f, 0.0423593149f), new float3(-1.14374959f, -0.474444419f, 0.0423593149f), new float3(-1.14374959f, -0.474444419f, -0.0338906869f), new float3(1.14375043f, -0.474444419f, -0.0338906869f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 1, 4, 7, 2, -1, 5, 0, 3, 6, -1, 6, 3, 2, 7, -1, 0, 5, 4, 1, -1 }))
				;
		}

		// Near (61.114, -0.534, 95.313): the exact CSG makes a triangle the mesh does not have
		static ContentsScene DetailsSite41()
		{
			return new ContentsScene().WithContentsList(kMapContents)
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 0f, -1f, -0.0677813739f),
					new float4(0f, 0f, 1f, -0.08471863f),
					new float4(0f, 1f, 0f, -0.03812499f),
					new float4(0.707106769f, -0.707106769f, 0f, -0.7488446f),
					new float4(-0.707106769f, -0.707106769f, 0f, -0.92257905f),
					new float4(0f, -1f, 0f, -0.0381250121f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(60.4747238f, -0.495625f, 95.30403f, 1f)), name: "Solid 346548 #2445")
					.WithMesh(new float3[] { new float3(1.0209012f, -0.0381250121f, -0.0677813739f), new float3(-1.26659882f, -0.0381250121f, -0.0677813739f), new float3(-1.34284878f, 0.03812499f, -0.0677813739f), new float3(1.09715128f, 0.03812499f, -0.0677813739f), new float3(-1.26659882f, -0.0381250121f, 0.08471863f), new float3(1.0209012f, -0.0381250121f, 0.08471863f), new float3(1.09715128f, 0.03812499f, 0.08471863f), new float3(-1.34284878f, 0.03812499f, 0.08471863f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 2, 7, 6, 3, -1, 5, 0, 3, 6, -1, 1, 4, 7, 2, -1, 4, 1, 0, 5, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 0f, -1f, -0.0677813739f),
					new float4(0f, 0f, 1f, -0.08471863f),
					new float4(1f, 0f, 0f, -0.0338882431f),
					new float4(-0.707106769f, -0.707106769f, 0f, -0.3594464f),
					new float4(-0.707106769f, 0.707106769f, 0f, -0.563132465f),
					new float4(-1f, 0f, 0f, -0.0423617549f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(61.5379868f, -1.28777778f, 95.30403f, 1f)), name: "Solid 346551 #2448")
					.WithMesh(new float3[] { new float3(-0.0423617549f, -0.465972245f, -0.0677813739f), new float3(-0.0423617549f, 0.754027843f, -0.0677813739f), new float3(0.0338882431f, 0.8302778f, -0.0677813739f), new float3(0.0338882431f, -0.542222261f, -0.0677813739f), new float3(-0.0423617549f, 0.754027843f, 0.08471863f), new float3(-0.0423617549f, -0.465972245f, 0.08471863f), new float3(0.0338882431f, -0.542222261f, 0.08471863f), new float3(0.0338882431f, 0.8302778f, 0.08471863f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 2, 7, 6, 3, -1, 5, 0, 3, 6, -1, 1, 4, 7, 2, -1, 4, 1, 0, 5, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 1f, 0f, -0.7455556f),
					new float4(0f, -1f, 0f, -0.474444419f),
					new float4(1f, 0f, 0f, -1.14374971f),
					new float4(-1f, 0f, 0f, -1.14375031f),
					new float4(0f, 0f, -1f, -0.0338906869f),
					new float4(0f, 0f, 1f, -0.0423593149f),
				}, contents: 2, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(60.3518753f, -1.27930558f, 95.3082657f, 1f)), name: "Glass 346756 #2824")
					.WithMesh(new float3[] { new float3(-1.14375031f, 0.7455556f, 0.0423593149f), new float3(1.14374971f, 0.7455556f, 0.0423593149f), new float3(1.14374971f, 0.7455556f, -0.0338906869f), new float3(-1.14375031f, 0.7455556f, -0.0338906869f), new float3(1.14374971f, -0.474444419f, 0.0423593149f), new float3(-1.14375031f, -0.474444419f, 0.0423593149f), new float3(-1.14375031f, -0.474444419f, -0.0338906869f), new float3(1.14374971f, -0.474444419f, -0.0338906869f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 1, 4, 7, 2, -1, 5, 0, 3, 6, -1, 6, 3, 2, 7, -1, 0, 5, 4, 1, -1 }))
				;
		}

		// Near (61.114, -0.534, 90.28): the exact CSG makes a triangle the mesh does not have
		static ContentsScene DetailsSite42()
		{
			return new ContentsScene().WithContentsList(kMapContents)
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 0f, -1f, -0.06778015f),
					new float4(0f, 0f, 1f, -0.08471985f),
					new float4(0f, 1f, 0f, -0.03812499f),
					new float4(0.707106769f, -0.707106769f, 0f, -0.7488446f),
					new float4(-0.707106769f, -0.707106769f, 0f, -0.92257905f),
					new float4(0f, -1f, 0f, -0.0381250121f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(60.4747238f, -0.495625f, 90.27153f, 1f)), name: "Solid 346558 #2449")
					.WithMesh(new float3[] { new float3(1.0209012f, -0.0381250121f, -0.06778015f), new float3(-1.26659882f, -0.0381250121f, -0.06778015f), new float3(-1.34284878f, 0.03812499f, -0.06778015f), new float3(1.09715128f, 0.03812499f, -0.06778015f), new float3(-1.26659882f, -0.0381250121f, 0.08471985f), new float3(1.0209012f, -0.0381250121f, 0.08471985f), new float3(1.09715128f, 0.03812499f, 0.08471985f), new float3(-1.34284878f, 0.03812499f, 0.08471985f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 2, 7, 6, 3, -1, 5, 0, 3, 6, -1, 1, 4, 7, 2, -1, 4, 1, 0, 5, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 0f, -1f, -0.06778015f),
					new float4(0f, 0f, 1f, -0.08471985f),
					new float4(1f, 0f, 0f, -0.0338882431f),
					new float4(-0.707106769f, -0.707106769f, 0f, -0.3594464f),
					new float4(-0.707106769f, 0.707106769f, 0f, -0.563132465f),
					new float4(-1f, 0f, 0f, -0.0423617549f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(61.5379868f, -1.28777778f, 90.27153f, 1f)), name: "Solid 346561 #2452")
					.WithMesh(new float3[] { new float3(-0.0423617549f, -0.465972245f, -0.06778015f), new float3(-0.0423617549f, 0.754027843f, -0.06778015f), new float3(0.0338882431f, 0.8302778f, -0.06778015f), new float3(0.0338882431f, -0.542222261f, -0.06778015f), new float3(-0.0423617549f, 0.754027843f, 0.08471985f), new float3(-0.0423617549f, -0.465972245f, 0.08471985f), new float3(0.0338882431f, -0.542222261f, 0.08471985f), new float3(0.0338882431f, 0.8302778f, 0.08471985f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 2, 7, 6, 3, -1, 5, 0, 3, 6, -1, 1, 4, 7, 2, -1, 4, 1, 0, 5, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 1f, 0f, -0.7455556f),
					new float4(0f, -1f, 0f, -0.474444419f),
					new float4(1f, 0f, 0f, -1.14374971f),
					new float4(-1f, 0f, 0f, -1.14375031f),
					new float4(0f, 0f, -1f, -0.033889465f),
					new float4(0f, 0f, 1f, -0.042360533f),
				}, contents: 2, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(60.3518753f, -1.27930558f, 90.2757645f, 1f)), name: "Glass 346758 #2839")
					.WithMesh(new float3[] { new float3(-1.14375031f, 0.7455556f, 0.042360533f), new float3(1.14374971f, 0.7455556f, 0.042360533f), new float3(1.14374971f, 0.7455556f, -0.033889465f), new float3(-1.14375031f, 0.7455556f, -0.033889465f), new float3(1.14374971f, -0.474444419f, 0.042360533f), new float3(-1.14375031f, -0.474444419f, 0.042360533f), new float3(-1.14375031f, -0.474444419f, -0.033889465f), new float3(1.14374971f, -0.474444419f, -0.033889465f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 1, 4, 7, 2, -1, 5, 0, 3, 6, -1, 6, 3, 2, 7, -1, 0, 5, 4, 1, -1 }))
				;
		}

		// Near (59.208, -0.94, 90.255): the exact CSG makes a triangle the mesh does not have
		static ContentsScene DetailsSite43()
		{
			return new ContentsScene().WithContentsList(kMapContents)
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 0f, -1f, -0.06778015f),
					new float4(0f, 0f, 1f, -0.08471985f),
					new float4(0f, 1f, 0f, -0.03812499f),
					new float4(0.707106769f, -0.707106769f, 0f, -0.7488446f),
					new float4(-0.707106769f, -0.707106769f, 0f, -0.92257905f),
					new float4(0f, -1f, 0f, -0.0381250121f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(60.4747238f, -0.495625f, 90.27153f, 1f)), name: "Solid 346558 #2449")
					.WithMesh(new float3[] { new float3(1.0209012f, -0.0381250121f, -0.06778015f), new float3(-1.26659882f, -0.0381250121f, -0.06778015f), new float3(-1.34284878f, 0.03812499f, -0.06778015f), new float3(1.09715128f, 0.03812499f, -0.06778015f), new float3(-1.26659882f, -0.0381250121f, 0.08471985f), new float3(1.0209012f, -0.0381250121f, 0.08471985f), new float3(1.09715128f, 0.03812499f, 0.08471985f), new float3(-1.34284878f, 0.03812499f, 0.08471985f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 2, 7, 6, 3, -1, 5, 0, 3, 6, -1, 1, 4, 7, 2, -1, 4, 1, 0, 5, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 0f, -1f, -0.08472504f),
					new float4(0f, 0f, 1f, -0.06777497f),
					new float4(-1f, 0f, 0f, -0.0338888541f),
					new float4(0.707106769f, -0.707106769f, 0f, -0.359445959f),
					new float4(0.707106769f, 0.707106769f, 0f, -0.563132f),
					new float4(1f, 0f, 0f, -0.042361144f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(59.1657639f, -1.28777778f, 90.288475f, 1f)), name: "Solid 346559 #2450")
					.WithMesh(new float3[] { new float3(0.042361144f, 0.7540278f, -0.08472504f), new float3(0.042361144f, -0.465972215f, -0.08472504f), new float3(-0.0338888541f, -0.5422222f, -0.08472504f), new float3(-0.0338888541f, 0.830277741f, -0.08472504f), new float3(0.042361144f, -0.465972215f, 0.06777497f), new float3(0.042361144f, 0.7540278f, 0.06777497f), new float3(-0.0338888541f, 0.830277741f, 0.06777497f), new float3(-0.0338888541f, -0.5422222f, 0.06777497f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 6, 3, 2, 7, -1, 1, 4, 7, 2, -1, 5, 0, 3, 6, -1, 0, 5, 4, 1, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 1f, 0f, -0.7455556f),
					new float4(0f, -1f, 0f, -0.474444419f),
					new float4(1f, 0f, 0f, -1.14374971f),
					new float4(-1f, 0f, 0f, -1.14375031f),
					new float4(0f, 0f, -1f, -0.033889465f),
					new float4(0f, 0f, 1f, -0.042360533f),
				}, contents: 2, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(60.3518753f, -1.27930558f, 90.2757645f, 1f)), name: "Glass 346758 #2839")
					.WithMesh(new float3[] { new float3(-1.14375031f, 0.7455556f, 0.042360533f), new float3(1.14374971f, 0.7455556f, 0.042360533f), new float3(1.14374971f, 0.7455556f, -0.033889465f), new float3(-1.14375031f, 0.7455556f, -0.033889465f), new float3(1.14374971f, -0.474444419f, 0.042360533f), new float3(-1.14375031f, -0.474444419f, 0.042360533f), new float3(-1.14375031f, -0.474444419f, -0.033889465f), new float3(1.14374971f, -0.474444419f, -0.033889465f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 1, 4, 7, 2, -1, 5, 0, 3, 6, -1, 6, 3, 2, 7, -1, 0, 5, 4, 1, -1 }))
				;
		}

		// Near (61.496, -1.754, 90.274): the mesh has a triangle the exact CSG does not make
		static ContentsScene DetailsSite44()
		{
			return new ContentsScene().WithContentsList(kMapContents)
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 0f, -1f, -0.08472504f),
					new float4(0f, 0f, 1f, -0.06777497f),
					new float4(-1f, 0f, 0f, -0.0338888541f),
					new float4(0.707106769f, -0.707106769f, 0f, -0.359445959f),
					new float4(0.707106769f, 0.707106769f, 0f, -0.563132f),
					new float4(1f, 0f, 0f, -0.042361144f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(59.1657639f, -1.28777778f, 90.288475f, 1f)), name: "Solid 346559 #2450")
					.WithMesh(new float3[] { new float3(0.042361144f, 0.7540278f, -0.08472504f), new float3(0.042361144f, -0.465972215f, -0.08472504f), new float3(-0.0338888541f, -0.5422222f, -0.08472504f), new float3(-0.0338888541f, 0.830277741f, -0.08472504f), new float3(0.042361144f, -0.465972215f, 0.06777497f), new float3(0.042361144f, 0.7540278f, 0.06777497f), new float3(-0.0338888541f, 0.830277741f, 0.06777497f), new float3(-0.0338888541f, -0.5422222f, 0.06777497f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 6, 3, 2, 7, -1, 1, 4, 7, 2, -1, 5, 0, 3, 6, -1, 0, 5, 4, 1, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 0f, -1f, -0.06778015f),
					new float4(0f, 0f, 1f, -0.08471985f),
					new float4(0f, -1f, 0f, -0.0296527911f),
					new float4(0.707106769f, 0.707106769f, 0f, -0.748844445f),
					new float4(-0.707106769f, 0.707106769f, 0f, -0.9345607f),
					new float4(0f, 1f, 0f, -0.04659721f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(60.4831963f, -1.80034721f, 90.27153f, 1f)), name: "Solid 346560 #2451")
					.WithMesh(new float3[] { new float3(-1.27507126f, 0.04659721f, -0.06778015f), new float3(1.01242876f, 0.04659721f, -0.06778015f), new float3(1.08867884f, -0.0296527911f, -0.06778015f), new float3(-1.35132122f, -0.0296527911f, -0.06778015f), new float3(1.01242876f, 0.04659721f, 0.08471985f), new float3(-1.27507126f, 0.04659721f, 0.08471985f), new float3(-1.35132122f, -0.0296527911f, 0.08471985f), new float3(1.08867884f, -0.0296527911f, 0.08471985f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 6, 3, 2, 7, -1, 1, 4, 7, 2, -1, 5, 0, 3, 6, -1, 0, 5, 4, 1, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 0f, -1f, -0.06778015f),
					new float4(0f, 0f, 1f, -0.08471985f),
					new float4(1f, 0f, 0f, -0.0338882431f),
					new float4(-0.707106769f, -0.707106769f, 0f, -0.3594464f),
					new float4(-0.707106769f, 0.707106769f, 0f, -0.563132465f),
					new float4(-1f, 0f, 0f, -0.0423617549f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(61.5379868f, -1.28777778f, 90.27153f, 1f)), name: "Solid 346561 #2452")
					.WithMesh(new float3[] { new float3(-0.0423617549f, -0.465972245f, -0.06778015f), new float3(-0.0423617549f, 0.754027843f, -0.06778015f), new float3(0.0338882431f, 0.8302778f, -0.06778015f), new float3(0.0338882431f, -0.542222261f, -0.06778015f), new float3(-0.0423617549f, 0.754027843f, 0.08471985f), new float3(-0.0423617549f, -0.465972245f, 0.08471985f), new float3(0.0338882431f, -0.542222261f, 0.08471985f), new float3(0.0338882431f, 0.8302778f, 0.08471985f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 2, 7, 6, 3, -1, 5, 0, 3, 6, -1, 1, 4, 7, 2, -1, 4, 1, 0, 5, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 1f, 0f, -0.7455556f),
					new float4(0f, -1f, 0f, -0.474444419f),
					new float4(1f, 0f, 0f, -1.14374971f),
					new float4(-1f, 0f, 0f, -1.14375031f),
					new float4(0f, 0f, -1f, -0.033889465f),
					new float4(0f, 0f, 1f, -0.042360533f),
				}, contents: 2, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(60.3518753f, -1.27930558f, 90.2757645f, 1f)), name: "Glass 346758 #2839")
					.WithMesh(new float3[] { new float3(-1.14375031f, 0.7455556f, 0.042360533f), new float3(1.14374971f, 0.7455556f, 0.042360533f), new float3(1.14374971f, 0.7455556f, -0.033889465f), new float3(-1.14375031f, 0.7455556f, -0.033889465f), new float3(1.14374971f, -0.474444419f, 0.042360533f), new float3(-1.14375031f, -0.474444419f, 0.042360533f), new float3(-1.14375031f, -0.474444419f, -0.033889465f), new float3(1.14374971f, -0.474444419f, -0.033889465f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 1, 4, 7, 2, -1, 5, 0, 3, 6, -1, 6, 3, 2, 7, -1, 0, 5, 4, 1, -1 }))
				;
		}

		// Near (62.258, -0.94, 90.255): the exact CSG makes a triangle the mesh does not have
		static ContentsScene DetailsSite45()
		{
			return new ContentsScene().WithContentsList(kMapContents)
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 0f, -1f, -0.06778015f),
					new float4(0f, 0f, 1f, -0.08471985f),
					new float4(0f, 1f, 0f, -0.03812499f),
					new float4(0.707106769f, -0.707106769f, 0f, -0.74884516f),
					new float4(-0.707106769f, -0.707106769f, 0f, -0.9225785f),
					new float4(0f, -1f, 0f, -0.0381250121f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(63.5247231f, -0.495625f, 90.27153f, 1f)), name: "Solid 346563 #2453")
					.WithMesh(new float3[] { new float3(1.020902f, -0.0381250121f, -0.06778015f), new float3(-1.26659811f, -0.0381250121f, -0.06778015f), new float3(-1.34284806f, 0.03812499f, -0.06778015f), new float3(1.097152f, 0.03812499f, -0.06778015f), new float3(-1.26659811f, -0.0381250121f, 0.08471985f), new float3(1.020902f, -0.0381250121f, 0.08471985f), new float3(1.097152f, 0.03812499f, 0.08471985f), new float3(-1.34284806f, 0.03812499f, 0.08471985f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 2, 7, 6, 3, -1, 5, 0, 3, 6, -1, 1, 4, 7, 2, -1, 4, 1, 0, 5, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 0f, -1f, -0.08472504f),
					new float4(0f, 0f, 1f, -0.06777497f),
					new float4(-1f, 0f, 0f, -0.03388809f),
					new float4(0.707106769f, -0.707106769f, 0f, -0.3594465f),
					new float4(0.707106769f, 0.707106769f, 0f, -0.5631325f),
					new float4(1f, 0f, 0f, -0.0423619077f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(62.2157631f, -1.28777778f, 90.288475f, 1f)), name: "Solid 346564 #2454")
					.WithMesh(new float3[] { new float3(0.0423619077f, 0.7540278f, -0.08472504f), new float3(0.0423619077f, -0.465972215f, -0.08472504f), new float3(-0.03388809f, -0.5422222f, -0.08472504f), new float3(-0.03388809f, 0.830277741f, -0.08472504f), new float3(0.0423619077f, -0.465972215f, 0.06777497f), new float3(0.0423619077f, 0.7540278f, 0.06777497f), new float3(-0.03388809f, 0.830277741f, 0.06777497f), new float3(-0.03388809f, -0.5422222f, 0.06777497f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 6, 3, 2, 7, -1, 1, 4, 7, 2, -1, 5, 0, 3, 6, -1, 0, 5, 4, 1, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 1f, 0f, -0.7455556f),
					new float4(0f, -1f, 0f, -0.474444419f),
					new float4(1f, 0f, 0f, -1.14375043f),
					new float4(-1f, 0f, 0f, -1.14374959f),
					new float4(0f, 0f, -1f, -0.033889465f),
					new float4(0f, 0f, 1f, -0.042360533f),
				}, contents: 2, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(63.4018745f, -1.27930558f, 90.2757645f, 1f)), name: "Glass 346757 #2842")
					.WithMesh(new float3[] { new float3(-1.14374959f, 0.7455556f, 0.042360533f), new float3(1.14375043f, 0.7455556f, 0.042360533f), new float3(1.14375043f, 0.7455556f, -0.033889465f), new float3(-1.14374959f, 0.7455556f, -0.033889465f), new float3(1.14375043f, -0.474444419f, 0.042360533f), new float3(-1.14374959f, -0.474444419f, 0.042360533f), new float3(-1.14374959f, -0.474444419f, -0.033889465f), new float3(1.14375043f, -0.474444419f, -0.033889465f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 1, 4, 7, 2, -1, 5, 0, 3, 6, -1, 6, 3, 2, 7, -1, 0, 5, 4, 1, -1 }))
				;
		}

		// Near (68.778, -3.279, 94.398): the mesh has a triangle the exact CSG does not make
		static ContentsScene DetailsSite46()
		{
			return new ContentsScene().WithContentsList(kMapContents)
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, -1f, 0f, -0.0296528153f),
					new float4(0f, 1f, 0f, -0.0465971865f),
					new float4(0f, 0f, 1f, -1.42333317f),
					new float4(1f, 0f, 0f, -1.75374973f),
					new float4(-1f, 0f, 0f, -1.75375032f),
					new float4(0f, 0f, -1f, -1.77916682f),
				}, contents: 3, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(67.02375f, -3.32534719f, 92.97417f, 1f)), name: "Grate 353640 #2847")
					.WithMesh(new float3[] { new float3(-1.75375032f, -0.0296528153f, -1.77916682f), new float3(1.75374973f, -0.0296528153f, -1.77916682f), new float3(1.75374973f, -0.0296528153f, 1.42333317f), new float3(-1.75375032f, -0.0296528153f, 1.42333317f), new float3(1.75374973f, 0.0465971865f, -1.77916682f), new float3(-1.75375032f, 0.0465971865f, -1.77916682f), new float3(-1.75375032f, 0.0465971865f, 1.42333317f), new float3(1.75374973f, 0.0465971865f, 1.42333317f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 6, 3, 2, 7, -1, 1, 4, 7, 2, -1, 5, 0, 3, 6, -1, 0, 5, 4, 1, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, -1f, 0f, -0.6523613f),
					new float4(0f, 1f, 0f, -1.02513874f),
					new float4(-0.707106769f, 0f, -0.707106769f, -1.06635427f),
					new float4(1f, 0f, 0f, -0.0677792355f),
					new float4(-0.707106769f, 0f, 0.707106769f, -1.31796849f),
					new float4(-1f, 0f, 0f, -0.08472076f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(68.86222f, -4.22763872f, 92.61833f, 1f)), name: "Solid 353422 #2848")
					.WithMesh(new float3[] { new float3(-0.08472076f, -0.6523613f, 1.77916813f), new float3(-0.08472076f, -0.6523613f, -1.423332f), new float3(0.0677792355f, -0.6523613f, -1.57583189f), new float3(0.0677792355f, -0.6523613f, 1.93166816f), new float3(-0.08472076f, 1.02513874f, -1.423332f), new float3(-0.08472076f, 1.02513874f, 1.77916813f), new float3(0.0677792355f, 1.02513874f, 1.93166816f), new float3(0.0677792355f, 1.02513874f, -1.57583189f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 1, 4, 7, 2, -1, 6, 3, 2, 7, -1, 5, 0, 3, 6, -1, 0, 5, 4, 1, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, -1f, 0f, -0.6523613f),
					new float4(0f, 0f, 1f, -0.0593078621f),
					new float4(-0.8f, 0f, -0.6f, -1.15052927f),
					new float4(0f, 1f, 0f, -1.02513874f),
					new float4(0.707106769f, 0f, -0.707106769f, -1.57856262f),
					new float4(0f, 0f, -1f, -0.09319214f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(66.63827f, -4.22763872f, 94.49069f, 1f)), name: "Solid 353416 #2849")
					.WithMesh(new float3[] { new float3(-1.36826742f, -0.6523613f, -0.09319214f), new float3(2.13923264f, -0.6523613f, -0.09319214f), new float3(2.29173255f, -0.6523613f, 0.0593078621f), new float3(-1.48264241f, -0.6523613f, 0.0593078621f), new float3(2.29173255f, 1.02513874f, 0.0593078621f), new float3(-1.48264241f, 1.02513874f, 0.0593078621f), new float3(-1.36826742f, 1.02513874f, -0.09319214f), new float3(2.13923264f, 1.02513874f, -0.09319214f) }, new int[] { 0, 1, 2, 3, -1, 2, 4, 5, 3, -1, 6, 0, 3, 5, -1, 7, 6, 5, 4, -1, 1, 7, 4, 2, -1, 7, 1, 0, 6, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, -1f, 0f, -0.0593055524f),
					new float4(0f, 1f, 0f, -0.09319445f),
					new float4(0f, 0f, 1f, -1.42333317f),
					new float4(1f, 0f, 0f, -1.75374973f),
					new float4(-1f, 0f, 0f, -1.75375032f),
					new float4(0f, 0f, -1f, -1.77916682f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(67.02375f, -4.82069445f, 92.97417f, 1f)), name: "Solid 353429 #2850")
					.WithMesh(new float3[] { new float3(-1.75375032f, -0.0593055524f, -1.77916682f), new float3(1.75374973f, -0.0593055524f, -1.77916682f), new float3(1.75374973f, -0.0593055524f, 1.42333317f), new float3(-1.75375032f, -0.0593055524f, 1.42333317f), new float3(1.75374973f, 0.09319445f, -1.77916682f), new float3(-1.75375032f, 0.09319445f, -1.77916682f), new float3(-1.75375032f, 0.09319445f, 1.42333317f), new float3(1.75374973f, 0.09319445f, 1.42333317f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 6, 3, 2, 7, -1, 1, 4, 7, 2, -1, 5, 0, 3, 6, -1, 0, 5, 4, 1, -1 }))
				;
		}

		// Near (-36.432, 1.22, 150.849): the exact CSG makes a triangle the mesh does not have
		static ContentsScene DetailsSite47()
		{
			return new ContentsScene().WithContentsList(kMapContents)
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 1f, 0f, -0.0465972126f),
					new float4(0f, -1f, 0f, -0.0296527855f),
					new float4(1f, 0f, 0f, -1.09715319f),
					new float4(-0.9828722f, 0f, 0.184288532f, -1.37332368f),
					new float4(-0.09875158f, 0f, -0.9951121f, -0.153003111f),
					new float4(0.10104271f, 0f, 0.9948821f, -0.153010055f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(-35.8671532f, 1.17340279f, 150.932632f, 1f)), name: "Solid 504229 #1440")
					.WithMesh(new float3[] { new float3(-1.34284687f, 0.0465972126f, 0.290180057f), new float3(1.09715319f, 0.0465972126f, 0.0423675552f), new float3(1.09715319f, 0.0465972126f, -0.26263243f), new float3(-1.40003431f, 0.0465972126f, -0.0148199424f), new float3(1.09715319f, -0.0296527855f, 0.0423675552f), new float3(-1.34284687f, -0.0296527855f, 0.290180057f), new float3(-1.40003431f, -0.0296527855f, -0.0148199424f), new float3(1.09715319f, -0.0296527855f, -0.26263243f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 1, 4, 7, 2, -1, 5, 0, 3, 6, -1, 6, 3, 2, 7, -1, 0, 5, 4, 1, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 1f, 0f, -0.18638894f),
					new float4(0f, -1f, 0f, -0.11861106f),
					new float4(1f, 0f, 0f, -1.10562563f),
					new float4(-0.9701425f, 0f, 0.242535621f, -1.37415648f),
					new float4(-0.09875158f, 0f, -0.9951121f, -0.0383608267f),
					new float4(0.09950372f, 0f, 0.9950372f, -0.0383534469f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(-35.8756256f, 1.033611f, 150.818268f, 1f)), name: "Solid 504237 #1448")
					.WithMesh(new float3[] { new float3(-1.37249947f, 0.18638894f, 0.175794676f), new float3(1.10562563f, 0.18638894f, -0.0720178261f), new float3(1.10562563f, 0.18638894f, -0.14826782f), new float3(-1.391562f, 0.18638894f, 0.0995446742f), new float3(1.10562563f, -0.11861106f, -0.0720178261f), new float3(-1.37249947f, -0.11861106f, 0.175794676f), new float3(-1.391562f, -0.11861106f, 0.0995446742f), new float3(1.10562563f, -0.11861106f, -0.14826782f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 1, 4, 7, 2, -1, 5, 0, 3, 6, -1, 6, 3, 2, 7, -1, 0, 5, 4, 1, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(-0.07828888f, 0f, 0.9969307f, -0.356414348f),
					new float4(1f, 0f, 0f, -3.67601442f),
					new float4(0f, 0f, -1f, -0.407727659f),
					new float4(0.09875158f, 0f, 0.9951121f, -0.5642959f),
					new float4(0f, 1f, 0f, -0.7455556f),
					new float4(0f, -1f, 0f, -0.474444449f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(-38.4460144f, 0.474444449f, 150.467728f, 1f)), name: "Solid 288923 #5583")
					.WithMesh(new float3[] { new float3(-9.744558f, -0.474444449f, -0.407727659f), new float3(1.17882693f, -0.474444449f, 0.450084835f), new float3(1.17882693f, 0.7455556f, 0.450084835f), new float3(-9.744558f, 0.7455556f, -0.407727659f), new float3(3.67601442f, -0.474444449f, 0.202272326f), new float3(3.67601442f, -0.474444449f, -0.407727659f), new float3(3.67601442f, 0.7455556f, -0.407727659f), new float3(3.67601442f, 0.7455556f, 0.202272326f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 5, 0, 3, 6, -1, 1, 4, 7, 2, -1, 2, 7, 6, 3, -1, 4, 1, 0, 5, -1 }))
				;
		}

		// Near (-3.304, 2.313, 136.106): the exact CSG makes a triangle the mesh does not have
		static ContentsScene DetailsSite48()
		{
			return new ContentsScene().WithContentsList(kMapContents)
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(1f, 0f, 0f, -0.08472215f),
					new float4(-1f, 0f, 0f, -0.06777785f),
					new float4(0f, 0f, -1f, -0.03388855f),
					new float4(0f, -0.707106769f, 0.707106769f, -0.359446168f),
					new float4(0f, 0.707106769f, 0.707106769f, -0.5631322f),
					new float4(0f, 0f, 1f, -0.04236145f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(-3.28722215f, 2.37222219f, 136.063889f, 1f)), name: "Solid 247396 #2003")
					.WithMesh(new float3[] { new float3(0.08472215f, 0.7540278f, 0.04236145f), new float3(0.08472215f, -0.465972215f, 0.04236145f), new float3(0.08472215f, -0.5422222f, -0.03388855f), new float3(0.08472215f, 0.8302778f, -0.03388855f), new float3(-0.06777785f, -0.465972215f, 0.04236145f), new float3(-0.06777785f, 0.7540278f, 0.04236145f), new float3(-0.06777785f, 0.8302778f, -0.03388855f), new float3(-0.06777785f, -0.5422222f, -0.03388855f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 6, 3, 2, 7, -1, 1, 4, 7, 2, -1, 5, 0, 3, 6, -1, 0, 5, 4, 1, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(1f, 0f, 0f, -0.07624994f),
					new float4(-1f, 0f, 0f, -0.0762500539f),
					new float4(0f, -1f, 0f, -0.0296527576f),
					new float4(0f, 0.707106769f, 0.707106769f, -0.8447021f),
					new float4(0f, 0.707106769f, -0.707106769f, -0.8387031f),
					new float4(0f, 1f, 0f, -0.0465972424f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(-3.27875f, 1.85965276f, 137.245758f, 1f)), name: "Solid 247397 #2004")
					.WithMesh(new float3[] { new float3(0.07624994f, 0.0465972424f, -1.13950813f), new float3(0.07624994f, 0.0465972424f, 1.147992f), new float3(0.07624994f, -0.0296527576f, 1.224242f), new float3(0.07624994f, -0.0296527576f, -1.21575809f), new float3(-0.0762500539f, 0.0465972424f, 1.147992f), new float3(-0.0762500539f, 0.0465972424f, -1.13950813f), new float3(-0.0762500539f, -0.0296527576f, -1.21575809f), new float3(-0.0762500539f, -0.0296527576f, 1.224242f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 6, 3, 2, 7, -1, 1, 4, 7, 2, -1, 5, 0, 3, 6, -1, 0, 5, 4, 1, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(1f, 0f, 0f, -0.03812494f),
					new float4(-1f, 0f, 0f, -0.0381250568f),
					new float4(0f, 0f, 1f, -1.27084041f),
					new float4(0f, 0f, -1f, -1.0166595f),
					new float4(0f, -1f, 0f, -0.4744444f),
					new float4(0f, 1f, 0f, -0.745555639f),
				}, contents: 2, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(-3.27875f, 2.38069439f, 137.12291f, 1f)), name: "Glass 247400 #2007")
					.WithMesh(new float3[] { new float3(0.03812494f, 0.745555639f, -1.0166595f), new float3(0.03812494f, 0.745555639f, 1.27084041f), new float3(0.03812494f, -0.4744444f, 1.27084041f), new float3(0.03812494f, -0.4744444f, -1.0166595f), new float3(-0.0381250568f, 0.745555639f, 1.27084041f), new float3(-0.0381250568f, 0.745555639f, -1.0166595f), new float3(-0.0381250568f, -0.4744444f, -1.0166595f), new float3(-0.0381250568f, -0.4744444f, 1.27084041f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 1, 4, 7, 2, -1, 5, 0, 3, 6, -1, 6, 3, 2, 7, -1, 0, 5, 4, 1, -1 }))
				;
		}

		// Near (64.546, -0.94, 95.338): the exact CSG makes a triangle the mesh does not have
		static ContentsScene DetailsSite49()
		{
			return new ContentsScene().WithContentsList(kMapContents)
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 0f, -1f, -0.0677813739f),
					new float4(0f, 0f, 1f, -0.08471863f),
					new float4(0f, 1f, 0f, -0.03812499f),
					new float4(0.707106769f, -0.707106769f, 0f, -0.74884516f),
					new float4(-0.707106769f, -0.707106769f, 0f, -0.9225785f),
					new float4(0f, -1f, 0f, -0.0381250121f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(63.5247231f, -0.495625f, 95.30403f, 1f)), name: "Solid 338649 #2441")
					.WithMesh(new float3[] { new float3(1.020902f, -0.0381250121f, -0.0677813739f), new float3(-1.26659811f, -0.0381250121f, -0.0677813739f), new float3(-1.34284806f, 0.03812499f, -0.0677813739f), new float3(1.097152f, 0.03812499f, -0.0677813739f), new float3(-1.26659811f, -0.0381250121f, 0.08471863f), new float3(1.020902f, -0.0381250121f, 0.08471863f), new float3(1.097152f, 0.03812499f, 0.08471863f), new float3(-1.34284806f, 0.03812499f, 0.08471863f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 2, 7, 6, 3, -1, 5, 0, 3, 6, -1, 1, 4, 7, 2, -1, 4, 1, 0, 5, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 0f, -1f, -0.0677813739f),
					new float4(0f, 0f, 1f, -0.08471863f),
					new float4(1f, 0f, 0f, -0.0338851921f),
					new float4(-0.707106769f, -0.707106769f, 0f, -0.359448552f),
					new float4(-0.707106769f, 0.707106769f, 0f, -0.5631346f),
					new float4(-1f, 0f, 0f, -0.0423648059f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(64.58799f, -1.28777778f, 95.30403f, 1f)), name: "Solid 338650 #2444")
					.WithMesh(new float3[] { new float3(-0.0423648059f, -0.465972215f, -0.0677813739f), new float3(-0.0423648059f, 0.7540278f, -0.0677813739f), new float3(0.0338851921f, 0.8302778f, -0.0677813739f), new float3(0.0338851921f, -0.5422222f, -0.0677813739f), new float3(-0.0423648059f, 0.7540278f, 0.08471863f), new float3(-0.0423648059f, -0.465972215f, 0.08471863f), new float3(0.0338851921f, -0.5422222f, 0.08471863f), new float3(0.0338851921f, 0.8302778f, 0.08471863f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 2, 7, 6, 3, -1, 5, 0, 3, 6, -1, 1, 4, 7, 2, -1, 4, 1, 0, 5, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 1f, 0f, -0.7455556f),
					new float4(0f, -1f, 0f, -0.474444419f),
					new float4(1f, 0f, 0f, -1.14375043f),
					new float4(-1f, 0f, 0f, -1.14374959f),
					new float4(0f, 0f, -1f, -0.0338906869f),
					new float4(0f, 0f, 1f, -0.0423593149f),
				}, contents: 2, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(63.4018745f, -1.27930558f, 95.3082657f, 1f)), name: "Glass 346752 #2823")
					.WithMesh(new float3[] { new float3(-1.14374959f, 0.7455556f, 0.0423593149f), new float3(1.14375043f, 0.7455556f, 0.0423593149f), new float3(1.14375043f, 0.7455556f, -0.0338906869f), new float3(-1.14374959f, 0.7455556f, -0.0338906869f), new float3(1.14375043f, -0.474444419f, 0.0423593149f), new float3(-1.14374959f, -0.474444419f, 0.0423593149f), new float3(-1.14374959f, -0.474444419f, -0.0338906869f), new float3(1.14375043f, -0.474444419f, -0.0338906869f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 1, 4, 7, 2, -1, 5, 0, 3, 6, -1, 6, 3, 2, 7, -1, 0, 5, 4, 1, -1 }))
				;
		}

		// Near (64.546, -0.94, 90.305): the exact CSG makes a triangle the mesh does not have
		static ContentsScene DetailsSite50()
		{
			return new ContentsScene().WithContentsList(kMapContents)
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 0f, -1f, -0.06778015f),
					new float4(0f, 0f, 1f, -0.08471985f),
					new float4(0f, 1f, 0f, -0.03812499f),
					new float4(0.707106769f, -0.707106769f, 0f, -0.74884516f),
					new float4(-0.707106769f, -0.707106769f, 0f, -0.9225785f),
					new float4(0f, -1f, 0f, -0.0381250121f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(63.5247231f, -0.495625f, 90.27153f, 1f)), name: "Solid 346563 #2453")
					.WithMesh(new float3[] { new float3(1.020902f, -0.0381250121f, -0.06778015f), new float3(-1.26659811f, -0.0381250121f, -0.06778015f), new float3(-1.34284806f, 0.03812499f, -0.06778015f), new float3(1.097152f, 0.03812499f, -0.06778015f), new float3(-1.26659811f, -0.0381250121f, 0.08471985f), new float3(1.020902f, -0.0381250121f, 0.08471985f), new float3(1.097152f, 0.03812499f, 0.08471985f), new float3(-1.34284806f, 0.03812499f, 0.08471985f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 2, 7, 6, 3, -1, 5, 0, 3, 6, -1, 1, 4, 7, 2, -1, 4, 1, 0, 5, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 0f, -1f, -0.06778015f),
					new float4(0f, 0f, 1f, -0.08471985f),
					new float4(1f, 0f, 0f, -0.0338851921f),
					new float4(-0.707106769f, -0.707106769f, 0f, -0.359448552f),
					new float4(-0.707106769f, 0.707106769f, 0f, -0.5631346f),
					new float4(-1f, 0f, 0f, -0.0423648059f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(64.58799f, -1.28777778f, 90.27153f, 1f)), name: "Solid 346566 #2456")
					.WithMesh(new float3[] { new float3(-0.0423648059f, -0.465972215f, -0.06778015f), new float3(-0.0423648059f, 0.7540278f, -0.06778015f), new float3(0.0338851921f, 0.8302778f, -0.06778015f), new float3(0.0338851921f, -0.5422222f, -0.06778015f), new float3(-0.0423648059f, 0.7540278f, 0.08471985f), new float3(-0.0423648059f, -0.465972215f, 0.08471985f), new float3(0.0338851921f, -0.5422222f, 0.08471985f), new float3(0.0338851921f, 0.8302778f, 0.08471985f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 2, 7, 6, 3, -1, 5, 0, 3, 6, -1, 1, 4, 7, 2, -1, 4, 1, 0, 5, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 1f, 0f, -0.7455556f),
					new float4(0f, -1f, 0f, -0.474444419f),
					new float4(1f, 0f, 0f, -1.14375043f),
					new float4(-1f, 0f, 0f, -1.14374959f),
					new float4(0f, 0f, -1f, -0.033889465f),
					new float4(0f, 0f, 1f, -0.042360533f),
				}, contents: 2, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(63.4018745f, -1.27930558f, 90.2757645f, 1f)), name: "Glass 346757 #2842")
					.WithMesh(new float3[] { new float3(-1.14374959f, 0.7455556f, 0.042360533f), new float3(1.14375043f, 0.7455556f, 0.042360533f), new float3(1.14375043f, 0.7455556f, -0.033889465f), new float3(-1.14374959f, 0.7455556f, -0.033889465f), new float3(1.14375043f, -0.474444419f, 0.042360533f), new float3(-1.14374959f, -0.474444419f, 0.042360533f), new float3(-1.14374959f, -0.474444419f, -0.033889465f), new float3(1.14375043f, -0.474444419f, -0.033889465f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 1, 4, 7, 2, -1, 5, 0, 3, 6, -1, 6, 3, 2, 7, -1, 0, 5, 4, 1, -1 }))
				;
		}

		// Near (-3.412, 8.267, 162.006): the exact CSG makes a triangle the mesh does not have
		static ContentsScene DetailsSite51()
		{
			return new ContentsScene().WithContentsList(kMapContents)
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(-0.7179004f, -0.696145833f, 0f, -0.991677f),
					new float4(-0.908308864f, 0.418300122f, 0f, -0.4358428f),
					new float4(0.999621451f, -0.0275125168f, 0f, -0.501660943f),
					new float4(0f, 0f, 1f, -0.9760028f),
					new float4(0f, 0f, -1f, -1.46399724f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(-3.91925f, 9.627833f, 161.894f, 1f)), name: "Solid 1138481 #5146")
					.WithMesh(new float3[] { new float3(-0.770125f, -0.630333364f, -1.46399724f), new float3(0.449875f, -1.88845837f, -1.46399724f), new float3(0.449875f, -1.88845837f, 0.9760028f), new float3(-0.770125f, -0.630333364f, 0.9760028f), new float3(0.56425f, 2.26716661f, -1.46399724f), new float3(0.56425f, 2.26716661f, 0.9760028f) }, new int[] { 0, 1, 2, 3, -1, 4, 0, 3, 5, -1, 1, 4, 5, 2, -1, 2, 5, 3, -1, 4, 1, 0, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(-0.953582644f, -0.301131368f, 0f, -0.453484863f),
					new float4(-0.999621451f, 0.0275125168f, 0f, -0.0414320827f),
					new float4(1f, 0f, 0f, -0.0381250568f),
					new float4(0f, 0f, 1f, -1.30133545f),
					new float4(0f, 0f, -1f, -1.1386646f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(-3.393125f, 9.003854f, 161.568665f, 1f)), name: "Solid 1138483 #5148")
					.WithMesh(new float3[] { new float3(-0.07624994f, -1.2644788f, -1.1386646f), new float3(0.0381250568f, -1.62666631f, -1.1386646f), new float3(0.0381250568f, -1.62666631f, 1.30133545f), new float3(-0.07624994f, -1.2644788f, 1.30133545f), new float3(0.0381250568f, 2.89114618f, -1.1386646f), new float3(0.0381250568f, 2.89114618f, 1.30133545f) }, new int[] { 0, 1, 2, 3, -1, 4, 0, 3, 5, -1, 1, 4, 5, 2, -1, 2, 5, 3, -1, 4, 1, 0, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 0f, -1f, -0.871430039f),
					new float4(0f, 0f, 1f, -0.958569944f),
					new float4(-0.257662654f, 0.9662349f, 0f, -0.375342458f),
					new float4(0.953582644f, 0.301131368f, 0f, -0.138299644f),
					new float4(-0.952911f, -0.3032501f, 0f, -0.15647532f),
					new float4(0f, -1f, 0f, -0.33041656f),
					new float4(1f, 0f, 0f, -0.1349987f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(-3.490018f, 7.34541655f, 161.60643f, 1f)), name: "Solid 1138495 #5160")
					.WithMesh(new float3[] { new float3(0.1349987f, 0.03177096f, -0.871430039f), new float3(0.1349987f, -0.33041656f, -0.871430039f), new float3(-0.05905742f, -0.33041656f, -0.871430039f), new float3(-0.2653138f, 0.317708462f, -0.871430039f), new float3(0.0206236932f, 0.393958449f, -0.871430039f), new float3(0.1349987f, -0.33041656f, 0.958569944f), new float3(0.1349987f, 0.03177096f, 0.958569944f), new float3(0.0206236932f, 0.393958449f, 0.958569944f), new float3(-0.2653138f, 0.317708462f, 0.958569944f), new float3(-0.05905742f, -0.33041656f, 0.958569944f) }, new int[] { 0, 1, 2, 3, 4, -1, 5, 6, 7, 8, 9, -1, 3, 8, 7, 4, -1, 6, 0, 4, 7, -1, 2, 9, 8, 3, -1, 1, 5, 9, 2, -1, 5, 1, 0, 6, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 0f, -1f, -0.135561526f),
					new float4(0f, 0f, 1f, -0.169438481f),
					new float4(-0.49613893f, 0.868243158f, 0f, -0.8320102f),
					new float4(0.257662654f, -0.9662349f, 0f, -0.751898646f),
					new float4(0.7179004f, 0.696145833f, 0f, -0.107037693f),
					new float4(-0.70142746f, -0.712740839f, 0f, -0.169437423f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(-4.187415f, 8.32607651f, 162.700562f, 1f)), name: "Solid 1138506 #5171")
					.WithMesh(new float3[] { new float3(0.432083428f, -0.662951469f, -0.135561526f), new float3(-0.7688541f, 0.518923461f, -0.135561526f), new float3(-0.501979053f, 0.6714235f, -0.135561526f), new float3(0.7180209f, -0.5867015f, -0.135561526f), new float3(-0.7688541f, 0.518923461f, 0.169438481f), new float3(0.432083428f, -0.662951469f, 0.169438481f), new float3(0.7180209f, -0.5867015f, 0.169438481f), new float3(-0.501979053f, 0.6714235f, 0.169438481f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 1, 4, 7, 2, -1, 5, 0, 3, 6, -1, 6, 3, 2, 7, -1, 0, 5, 4, 1, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 0f, -1f, -0.1452356f),
					new float4(0f, 0f, 1f, -0.159764409f),
					new float4(-0.257662654f, 0.9662349f, 0f, -0.375342458f),
					new float4(0.953582644f, 0.301131368f, 0f, -0.138299644f),
					new float4(-0.952911f, -0.3032501f, 0f, -0.15647532f),
					new float4(0f, -1f, 0f, -0.33041656f),
					new float4(1f, 0f, 0f, -0.1349987f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(-3.490018f, 7.34541655f, 162.710236f, 1f)), name: "Solid 1138507 #5172")
					.WithMesh(new float3[] { new float3(0.1349987f, 0.03177096f, -0.1452356f), new float3(0.1349987f, -0.33041656f, -0.1452356f), new float3(-0.05905742f, -0.33041656f, -0.1452356f), new float3(-0.2653138f, 0.317708462f, -0.1452356f), new float3(0.0206236932f, 0.393958449f, -0.1452356f), new float3(0.1349987f, -0.33041656f, 0.159764409f), new float3(0.1349987f, 0.03177096f, 0.159764409f), new float3(0.0206236932f, 0.393958449f, 0.159764409f), new float3(-0.2653138f, 0.317708462f, 0.159764409f), new float3(-0.05905742f, -0.33041656f, 0.159764409f) }, new int[] { 0, 1, 2, 3, 4, -1, 5, 6, 7, 8, 9, -1, 3, 8, 7, 4, -1, 6, 0, 4, 7, -1, 2, 9, 8, 3, -1, 1, 5, 9, 2, -1, 5, 1, 0, 6, -1 }))
				;
		}

		// Near (-3.304, 2.313, 140.986): the exact CSG makes a triangle the mesh does not have
		static ContentsScene DetailsSite52()
		{
			return new ContentsScene().WithContentsList(kMapContents)
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(1f, 0f, 0f, -0.08472215f),
					new float4(-1f, 0f, 0f, -0.06777785f),
					new float4(0f, 0f, -1f, -0.0338934325f),
					new float4(0f, -0.707106769f, 0.707106769f, -0.3594427f),
					new float4(0f, 0.707106769f, 0.707106769f, -0.563128769f),
					new float4(0f, 0f, 1f, -0.0423565656f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(-3.28722215f, 2.37222219f, 140.9439f, 1f)), name: "Solid 247371 #1998")
					.WithMesh(new float3[] { new float3(0.08472215f, 0.7540278f, 0.0423565656f), new float3(0.08472215f, -0.4659722f, 0.0423565656f), new float3(0.08472215f, -0.5422222f, -0.0338934325f), new float3(0.08472215f, 0.8302778f, -0.0338934325f), new float3(-0.06777785f, -0.4659722f, 0.0423565656f), new float3(-0.06777785f, 0.7540278f, 0.0423565656f), new float3(-0.06777785f, 0.8302778f, -0.0338934325f), new float3(-0.06777785f, -0.5422222f, -0.0338934325f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 6, 3, 2, 7, -1, 1, 4, 7, 2, -1, 5, 0, 3, 6, -1, 0, 5, 4, 1, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(1f, 0f, 0f, -0.07624994f),
					new float4(-1f, 0f, 0f, -0.0762500539f),
					new float4(0f, -1f, 0f, -0.0296527576f),
					new float4(0f, 0.707106769f, 0.707106769f, -0.844698668f),
					new float4(0f, 0.707106769f, -0.707106769f, -0.838706553f),
					new float4(0f, 1f, 0f, -0.0465972424f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(-3.27875f, 1.85965276f, 142.125763f, 1f)), name: "Solid 247372 #1999")
					.WithMesh(new float3[] { new float3(0.07624994f, 0.0465972424f, -1.139513f), new float3(0.07624994f, 0.0465972424f, 1.14798713f), new float3(0.07624994f, -0.0296527576f, 1.22423708f), new float3(0.07624994f, -0.0296527576f, -1.215763f), new float3(-0.0762500539f, 0.0465972424f, 1.14798713f), new float3(-0.0762500539f, 0.0465972424f, -1.139513f), new float3(-0.0762500539f, -0.0296527576f, -1.215763f), new float3(-0.0762500539f, -0.0296527576f, 1.22423708f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 6, 3, 2, 7, -1, 1, 4, 7, 2, -1, 5, 0, 3, 6, -1, 0, 5, 4, 1, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(1f, 0f, 0f, -0.03812494f),
					new float4(-1f, 0f, 0f, -0.0381250568f),
					new float4(0f, 0f, 1f, -1.27083552f),
					new float4(0f, 0f, -1f, -1.01666439f),
					new float4(0f, -1f, 0f, -0.4744444f),
					new float4(0f, 1f, 0f, -0.745555639f),
				}, contents: 2, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(-3.27875f, 2.38069439f, 142.002914f, 1f)), name: "Glass 247375 #2002")
					.WithMesh(new float3[] { new float3(0.03812494f, 0.745555639f, -1.01666439f), new float3(0.03812494f, 0.745555639f, 1.27083552f), new float3(0.03812494f, -0.4744444f, 1.27083552f), new float3(0.03812494f, -0.4744444f, -1.01666439f), new float3(-0.0381250568f, 0.745555639f, 1.27083552f), new float3(-0.0381250568f, 0.745555639f, -1.01666439f), new float3(-0.0381250568f, -0.4744444f, -1.01666439f), new float3(-0.0381250568f, -0.4744444f, 1.27083552f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 1, 4, 7, 2, -1, 5, 0, 3, 6, -1, 6, 3, 2, 7, -1, 0, 5, 4, 1, -1 }))
				;
		}

		// Near (-34.694, 1.029, 160.239): the exact CSG makes a triangle the mesh does not have
		static ContentsScene DetailsSite53()
		{
			return new ContentsScene().WithContentsList(kMapContents)
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 1f, 0f, -0.09149997f),
					new float4(-0.10050378f, -0.7035265f, -0.7035265f, -0.0536413379f),
					new float4(1f, 0f, 0f, -0.266876668f),
					new float4(0.141421363f, 0f, 0.989949465f, -0.06039054f),
					new float4(-0.3939193f, 0f, -0.919145048f, -0.08409797f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(-35.0368767f, 1.05225f, 160.330872f, 1f)), name: "Solid 504246 #1400")
					.WithMesh(new float3[] { new float3(0.266876668f, 0.09149997f, -0.205871567f), new float3(-0.5337484f, 0.09149997f, 0.137253433f), new float3(0.266876668f, 0.09149997f, 0.0228784177f), new float3(-0.5337484f, -0.137250036f, 0.137253433f), new float3(0.266876668f, -0.137250021f, 0.0228784177f) }, new int[] { 0, 1, 2, -1, 3, 0, 4, -1, 0, 2, 4, -1, 1, 3, 4, 2, -1, 3, 1, 0, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 1f, 0f, -0.0571874864f),
					new float4(-0.08988323f, -0.719065845f, -0.689104736f, -0.0411170572f),
					new float4(-0.9486833f, 0f, 0.316227764f, -0.217014447f),
					new float4(0.3939193f, 0f, 0.919145048f, -0.0600784421f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(-35.38953f, 1.08656251f, 160.32515f, 1f)), name: "Solid 504247 #1401")
					.WithMesh(new float3[] { new float3(-0.181094825f, 0.0571874864f, 0.142975464f), new float3(0.619530261f, 0.0571874864f, -0.200149551f), new float3(-0.2573448f, 0.0571874864f, -0.08577454f), new float3(-0.181094825f, -0.171562508f, 0.142975464f) }, new int[] { 0, 1, 2, -1, 1, 3, 2, -1, 3, 0, 2, -1, 0, 3, 1, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 1f, 0f, -0.0465972126f),
					new float4(0f, -1f, 0f, -0.0296527855f),
					new float4(1f, 0f, 0f, -0.368542165f),
					new float4(-0.9486833f, 0f, 0.316227764f, -0.4554555f),
					new float4(-0.129339188f, 0f, -0.9916004f, -0.1497574f),
					new float4(0.121054374f, 0f, 0.99264586f, -0.149738133f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(-35.1385422f, 1.17340279f, 160.3241f, 1f)), name: "Solid 504258 #1412")
					.WithMesh(new float3[] { new float3(-0.4130203f, 0.0465972126f, 0.201215819f), new float3(0.368542165f, 0.0465972126f, 0.10590332f), new float3(0.368542165f, 0.0465972126f, -0.19909668f), new float3(-0.5083328f, 0.0465972126f, -0.08472168f), new float3(0.368542165f, -0.0296527855f, 0.10590332f), new float3(-0.4130203f, -0.0296527855f, 0.201215819f), new float3(-0.5083328f, -0.0296527855f, -0.08472168f), new float3(0.368542165f, -0.0296527855f, -0.19909668f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 1, 4, 7, 2, -1, 5, 0, 3, 6, -1, 6, 3, 2, 7, -1, 0, 5, 4, 1, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, -1f, 0f, -0.11861106f),
					new float4(0f, 0f, -1f, -0.182159424f),
					new float4(0f, 0f, 1f, -0.122840576f),
					new float4(-1f, 0f, 0f, -0.0889582857f),
					new float4(0f, -0.707106769f, -0.707106769f, -0.05092617f),
					new float4(0.707106769f, 0.707106769f, 0f, -0.06889388f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(-34.68104f, 1.033611f, 160.307159f, 1f)), name: "Solid 515902 #4246")
					.WithMesh(new float3[] { new float3(0.216041714f, -0.11861106f, 0.0465905778f), new float3(0.216041714f, -0.11861106f, 0.122840576f), new float3(-0.0889582857f, -0.11861106f, 0.122840576f), new float3(-0.0889582857f, -0.11861106f, 0.0465905778f), new float3(-0.0889582857f, 0.18638894f, -0.182159424f), new float3(-0.01270828f, 0.110138938f, -0.182159424f), new float3(-0.0889582857f, 0.110138938f, -0.182159424f), new float3(-0.0889582857f, 0.18638894f, 0.122840576f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, -1, 1, 7, 2, -1, 7, 4, 6, 3, 2, -1, 5, 0, 3, 6, -1, 0, 5, 4, 7, 1, -1 }))
				;
		}

		// Near (58.471, -5.82, 41.785): the exact CSG makes a triangle the mesh does not have
		static ContentsScene DetailsSite54()
		{
			return new ContentsScene().WithContentsList(kMapContents)
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(-1f, 0f, 0f, -0.07625031f),
					new float4(1f, 0f, 0f, -0.0762497f),
					new float4(0f, 1f, 0f, -0.038125135f),
					new float4(0f, -0.707106769f, -0.707106769f, -0.6529947f),
					new float4(0f, -0.707106769f, 0.707106769f, -1.0184288f),
					new float4(0f, -1f, 0f, -0.0381248668f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(58.4456253f, -5.375625f, 42.67035f, 1f)), name: "Solid 126071 #3395")
					.WithMesh(new float3[] { new float3(-0.07625031f, -0.0381248668f, -0.8853491f), new float3(-0.07625031f, -0.0381248668f, 1.402151f), new float3(-0.07625031f, 0.038125135f, 1.478401f), new float3(-0.07625031f, 0.038125135f, -0.9615991f), new float3(0.0762497f, -0.0381248668f, 1.402151f), new float3(0.0762497f, -0.0381248668f, -0.8853491f), new float3(0.0762497f, 0.038125135f, -0.9615991f), new float3(0.0762497f, 0.038125135f, 1.478401f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 2, 7, 6, 3, -1, 5, 0, 3, 6, -1, 1, 4, 7, 2, -1, 4, 1, 0, 5, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(-1f, 0f, 0f, -0.0677778646f),
					new float4(1f, 0f, 0f, -0.08472214f),
					new float4(0f, 0f, -1f, -0.0338876359f),
					new float4(0f, -0.707106769f, 0.707106769f, -0.359446675f),
					new float4(0f, 0.707106769f, 0.707106769f, -0.563133061f),
					new float4(0f, 0f, 1f, -0.0423623659f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(58.4371529f, -6.167778f, 41.7426376f, 1f)), name: "Solid 126072 #3396")
					.WithMesh(new float3[] { new float3(-0.0677778646f, -0.465972f, 0.0423623659f), new float3(-0.0677778646f, 0.7540281f, 0.0423623659f), new float3(-0.0677778646f, 0.830278039f, -0.0338876359f), new float3(-0.0677778646f, -0.542222f, -0.0338876359f), new float3(0.08472214f, 0.7540281f, 0.0423623659f), new float3(0.08472214f, -0.465972f, 0.0423623659f), new float3(0.08472214f, -0.542222f, -0.0338876359f), new float3(0.08472214f, 0.830278039f, -0.0338876359f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 2, 7, 6, 3, -1, 5, 0, 3, 6, -1, 1, 4, 7, 2, -1, 4, 1, 0, 5, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(-1f, 0f, 0f, -0.0381253064f),
					new float4(1f, 0f, 0f, -0.0381246954f),
					new float4(0f, 0f, -1f, -1.01666629f),
					new float4(0f, 0f, 1f, -1.27083373f),
					new float4(0f, -1f, 0f, -0.474444419f),
					new float4(0f, 1f, 0f, -0.7455556f),
				}, contents: 2, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(58.4456253f, -6.15930557f, 42.8016663f, 1f)), name: "Glass 126073 #3397")
					.WithMesh(new float3[] { new float3(-0.0381253064f, 0.7455556f, 1.27083373f), new float3(-0.0381253064f, 0.7455556f, -1.01666629f), new float3(-0.0381253064f, -0.474444419f, -1.01666629f), new float3(-0.0381253064f, -0.474444419f, 1.27083373f), new float3(0.0381246954f, 0.7455556f, -1.01666629f), new float3(0.0381246954f, 0.7455556f, 1.27083373f), new float3(0.0381246954f, -0.474444419f, 1.27083373f), new float3(0.0381246954f, -0.474444419f, -1.01666629f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 1, 4, 7, 2, -1, 5, 0, 3, 6, -1, 6, 3, 2, 7, -1, 0, 5, 4, 1, -1 }))
				;
		}

		// Near (89.791, -11.819, 60.847): the mesh has a triangle the exact CSG does not make
		static ContentsScene DetailsSite55()
		{
			return new ContentsScene().WithContentsList(kMapContents)
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(1f, 0f, 0f, -0.03812195f),
					new float4(-1f, 0f, 0f, -0.0381280519f),
					new float4(0f, 0f, 1f, -2.28749871f),
					new float4(0f, 0f, -1f, -1.83000135f),
					new float4(0f, -1f, 0f, -0.6523615f),
					new float4(0f, 1f, 0f, -1.0251385f),
				}, contents: 2, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(89.78438f, -11.1663885f, 58.56f, 1f)), name: "Glass 149620 #1867")
					.WithMesh(new float3[] { new float3(0.03812195f, 1.0251385f, -1.83000135f), new float3(0.03812195f, 1.0251385f, 2.28749871f), new float3(0.03812195f, -0.6523615f, 2.28749871f), new float3(0.03812195f, -0.6523615f, -1.83000135f), new float3(-0.0381280519f, 1.0251385f, 2.28749871f), new float3(-0.0381280519f, 1.0251385f, -1.83000135f), new float3(-0.0381280519f, -0.6523615f, -1.83000135f), new float3(-0.0381280519f, -0.6523615f, 2.28749871f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 1, 4, 7, 2, -1, 5, 0, 3, 6, -1, 6, 3, 2, 7, -1, 0, 5, 4, 1, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(1f, 0f, 0f, -0.07624695f),
					new float4(-1f, 0f, 0f, -0.07625305f),
					new float4(0f, -1f, 0f, -0.029653091f),
					new float4(0f, 0.707106769f, 0.707106769f, -1.49169946f),
					new float4(0f, 0.707106769f, -0.707106769f, -1.48571074f),
					new float4(0f, 1f, 0f, -0.0465969071f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(89.78438f, -11.8653469f, 58.7845154f, 1f)), name: "Solid 149621 #1868")
					.WithMesh(new float3[] { new float3(0.07624695f, 0.0465969071f, -2.05451536f), new float3(0.07624695f, 0.0465969071f, 2.0629847f), new float3(0.07624695f, -0.029653091f, 2.13923478f), new float3(0.07624695f, -0.029653091f, -2.13076544f), new float3(-0.07625305f, 0.0465969071f, 2.0629847f), new float3(-0.07625305f, 0.0465969071f, -2.05451536f), new float3(-0.07625305f, -0.029653091f, -2.13076544f), new float3(-0.07625305f, -0.029653091f, 2.13923478f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 6, 3, 2, 7, -1, 1, 4, 7, 2, -1, 5, 0, 3, 6, -1, 0, 5, 4, 1, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(1f, 0f, 0f, -0.06777832f),
					new float4(-1f, 0f, 0f, -0.08472168f),
					new float4(0f, 0f, 1f, -0.0338879377f),
					new float4(0f, -0.707106769f, -0.707106769f, -0.4852528f),
					new float4(0f, 0.707106769f, -0.707106769f, -0.760827839f),
					new float4(0f, 0f, -1f, -0.04236206f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(89.79285f, -11.174861f, 60.8898621f, 1f)), name: "Solid 149622 #1869")
					.WithMesh(new float3[] { new float3(0.06777832f, -0.643889f, -0.04236206f), new float3(0.06777832f, 1.033611f, -0.04236206f), new float3(0.06777832f, 1.109861f, 0.0338879377f), new float3(0.06777832f, -0.720139f, 0.0338879377f), new float3(-0.08472168f, 1.033611f, -0.04236206f), new float3(-0.08472168f, -0.643889f, -0.04236206f), new float3(-0.08472168f, -0.720139f, 0.0338879377f), new float3(-0.08472168f, 1.109861f, 0.0338879377f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 2, 7, 6, 3, -1, 5, 0, 3, 6, -1, 1, 4, 7, 2, -1, 4, 1, 0, 5, -1 }))
				;
		}

		// Near (-3.279, 2.516, 162.946): the exact CSG makes a triangle the mesh does not have
		static ContentsScene DetailsSite56()
		{
			return new ContentsScene().WithContentsList(kMapContents)
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(1f, 0f, 0f, -0.08472215f),
					new float4(-1f, 0f, 0f, -0.06777785f),
					new float4(0f, 0f, -1f, -0.0338848867f),
					new float4(0f, -0.707106769f, 0.707106769f, -0.359448761f),
					new float4(0f, 0.707106769f, 0.707106769f, -0.563134849f),
					new float4(0f, 0f, 1f, -0.04236511f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(-3.28722215f, 2.37222219f, 162.903885f, 1f)), name: "Solid 247408 #2008")
					.WithMesh(new float3[] { new float3(0.08472215f, 0.754027843f, 0.04236511f), new float3(0.08472215f, -0.465972215f, 0.04236511f), new float3(0.08472215f, -0.5422222f, -0.0338848867f), new float3(0.08472215f, 0.83027786f, -0.0338848867f), new float3(-0.06777785f, -0.465972215f, 0.04236511f), new float3(-0.06777785f, 0.754027843f, 0.04236511f), new float3(-0.06777785f, 0.83027786f, -0.0338848867f), new float3(-0.06777785f, -0.5422222f, -0.0338848867f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 6, 3, 2, 7, -1, 1, 4, 7, 2, -1, 5, 0, 3, 6, -1, 0, 5, 4, 1, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(1f, 0f, 0f, -0.07624994f),
					new float4(-1f, 0f, 0f, -0.0762500539f),
					new float4(0f, 1f, 0f, -0.0381249338f),
					new float4(0f, -0.707106769f, 0.707106769f, -0.838703156f),
					new float4(0f, -0.707106769f, -0.707106769f, -0.8327206f),
					new float4(0f, -1f, 0f, -0.0381250679f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(-3.27875f, 3.164375f, 164.08577f, 1f)), name: "Solid 247410 #2010")
					.WithMesh(new float3[] { new float3(0.07624994f, -0.0381250679f, 1.14798033f), new float3(0.07624994f, -0.0381250679f, -1.13951969f), new float3(0.07624994f, 0.0381249338f, -1.21576965f), new float3(0.07624994f, 0.0381249338f, 1.22423029f), new float3(-0.0762500539f, -0.0381250679f, -1.13951969f), new float3(-0.0762500539f, -0.0381250679f, 1.14798033f), new float3(-0.0762500539f, 0.0381249338f, 1.22423029f), new float3(-0.0762500539f, 0.0381249338f, -1.21576965f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 2, 7, 6, 3, -1, 5, 0, 3, 6, -1, 1, 4, 7, 2, -1, 4, 1, 0, 5, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(1f, 0f, 0f, -0.03812494f),
					new float4(-1f, 0f, 0f, -0.0381250568f),
					new float4(0f, 0f, 1f, -1.27082884f),
					new float4(0f, 0f, -1f, -1.01667118f),
					new float4(0f, -1f, 0f, -0.4744444f),
					new float4(0f, 1f, 0f, -0.745555639f),
				}, contents: 2, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(-3.27875f, 2.38069439f, 163.962921f, 1f)), name: "Glass 247412 #2012")
					.WithMesh(new float3[] { new float3(0.03812494f, 0.745555639f, -1.01667118f), new float3(0.03812494f, 0.745555639f, 1.27082884f), new float3(0.03812494f, -0.4744444f, 1.27082884f), new float3(0.03812494f, -0.4744444f, -1.01667118f), new float3(-0.0381250568f, 0.745555639f, 1.27082884f), new float3(-0.0381250568f, 0.745555639f, -1.01667118f), new float3(-0.0381250568f, -0.4744444f, -1.01667118f), new float3(-0.0381250568f, -0.4744444f, 1.27082884f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 1, 4, 7, 2, -1, 5, 0, 3, 6, -1, 6, 3, 2, 7, -1, 0, 5, 4, 1, -1 }))
				;
		}

		// Near (65.156, -4.232, 93.965): the exact CSG makes a triangle the mesh does not have
		static ContentsScene DetailsSite57()
		{
			return new ContentsScene().WithContentsList(kMapContents)
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 0f, 1f, -0.1597638f),
					new float4(0f, 0f, -1f, -0.145236209f),
					new float4(-0.196116135f, 0.9805807f, 0f, -0.224308133f),
					new float4(0.9805807f, -0.196116135f, 0f, -0.190126389f),
					new float4(0f, -1f, 0f, -0.197886825f),
					new float4(0.707106769f, 0.707106769f, 0f, -0.130940229f),
					new float4(-1f, 0f, 0f, -0.226935729f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(65.38256f, -4.682113f, 94.69524f, 1f)), name: "Solid 338707 #2434")
					.WithMesh(new float3[] { new float3(-0.226935729f, 0.183363155f, 0.1597638f), new float3(-0.226935729f, -0.197886825f, 0.1597638f), new float3(0.154314265f, -0.197886825f, 0.1597638f), new float3(0.192439273f, -0.00726181734f, 0.1597638f), new float3(-0.0363107137f, 0.221488163f, 0.1597638f), new float3(-0.226935729f, -0.197886825f, -0.145236209f), new float3(-0.226935729f, 0.183363155f, -0.145236209f), new float3(-0.0363107137f, 0.221488163f, -0.145236209f), new float3(0.192439273f, -0.00726181734f, -0.145236209f), new float3(0.154314265f, -0.197886825f, -0.145236209f) }, new int[] { 0, 1, 2, 3, 4, -1, 5, 6, 7, 8, 9, -1, 6, 0, 4, 7, -1, 8, 3, 2, 9, -1, 1, 5, 9, 2, -1, 3, 8, 7, 4, -1, 5, 1, 0, 6, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 0f, 1f, -0.1694455f),
					new float4(0f, 0f, -1f, -0.1355545f),
					new float4(1f, 0f, 0f, -0.06777618f),
					new float4(0f, -1f, 0f, -1.8977778f),
					new float4(0f, 1f, 0f, -2.982222f),
					new float4(-1f, 0f, 0f, -0.0847238153f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(65.24035f, -2.37222219f, 94.6855545f, 1f)), name: "Solid 338699 #2438")
					.WithMesh(new float3[] { new float3(-0.0847238153f, 2.982222f, 0.1694455f), new float3(-0.0847238153f, -1.8977778f, 0.1694455f), new float3(0.06777618f, -1.8977778f, 0.1694455f), new float3(0.06777618f, 2.982222f, 0.1694455f), new float3(-0.0847238153f, -1.8977778f, -0.1355545f), new float3(-0.0847238153f, 2.982222f, -0.1355545f), new float3(0.06777618f, 2.982222f, -0.1355545f), new float3(0.06777618f, -1.8977778f, -0.1355545f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 6, 3, 2, 7, -1, 1, 4, 7, 2, -1, 5, 0, 3, 6, -1, 0, 5, 4, 1, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, -1f, 0f, -0.6523613f),
					new float4(-1f, 0f, 0f, -0.04447876f),
					new float4(0f, 1f, 0f, -1.02513874f),
					new float4(0.8f, 0f, -0.6f, -0.915001869f),
					new float4(0.8f, 0f, 0.6f, -1.11833215f),
					new float4(1f, 0f, 0f, -0.06989624f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(65.2001f, -4.22763872f, 92.62681f, 1f)), name: "Solid 353425 #2846")
					.WithMesh(new float3[] { new float3(0.06989624f, -0.6523613f, -1.43180811f), new float3(0.06989624f, -0.6523613f, 1.77069187f), new float3(-0.04447876f, -0.6523613f, 1.92319191f), new float3(-0.04447876f, -0.6523613f, -1.584308f), new float3(-0.04447876f, 1.02513874f, 1.92319191f), new float3(-0.04447876f, 1.02513874f, -1.584308f), new float3(0.06989624f, 1.02513874f, 1.77069187f), new float3(0.06989624f, 1.02513874f, -1.43180811f) }, new int[] { 0, 1, 2, 3, -1, 2, 4, 5, 3, -1, 6, 7, 5, 4, -1, 7, 0, 3, 5, -1, 1, 6, 4, 2, -1, 6, 1, 0, 7, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, -1f, 0f, -0.6523613f),
					new float4(0f, 0f, 1f, -0.0593078621f),
					new float4(-0.8f, 0f, -0.6f, -1.15052927f),
					new float4(0f, 1f, 0f, -1.02513874f),
					new float4(0.707106769f, 0f, -0.707106769f, -1.57856262f),
					new float4(0f, 0f, -1f, -0.09319214f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(66.63827f, -4.22763872f, 94.49069f, 1f)), name: "Solid 353416 #2849")
					.WithMesh(new float3[] { new float3(-1.36826742f, -0.6523613f, -0.09319214f), new float3(2.13923264f, -0.6523613f, -0.09319214f), new float3(2.29173255f, -0.6523613f, 0.0593078621f), new float3(-1.48264241f, -0.6523613f, 0.0593078621f), new float3(2.29173255f, 1.02513874f, 0.0593078621f), new float3(-1.48264241f, 1.02513874f, 0.0593078621f), new float3(-1.36826742f, 1.02513874f, -0.09319214f), new float3(2.13923264f, 1.02513874f, -0.09319214f) }, new int[] { 0, 1, 2, 3, -1, 2, 4, 5, 3, -1, 6, 0, 3, 5, -1, 7, 6, 5, 4, -1, 1, 7, 4, 2, -1, 7, 1, 0, 6, -1 }))
				;
		}

		// Near (63.145, 1.002, 6.418): the exact CSG makes a triangle the mesh does not have
		static ContentsScene DetailsSite58()
		{
			return new ContentsScene().WithContentsList(kMapContents)
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 1f, 0f, -0.0571874864f),
					new float4(-0.334218144f, -0.7107627f, 0.6189625f, -0.040646825f),
					new float4(0.8192319f, 0f, 0.573462367f, -0.6073821f),
					new float4(0.3911702f, 0f, -0.920318365f, -0.0569079854f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(62.549305f, 1.08656251f, 6.22694635f, 1f)), name: "Solid 716307 #1524")
					.WithMesh(new float3[] { new float3(0.6047575f, 0.0571874864f, 0.195209771f), new float3(-1.68083453f, 0.0571874864f, -0.7762535f), new float3(0.47132f, 0.0571874864f, 0.385834754f), new float3(0.6047575f, -0.171562508f, 0.195209771f) }, new int[] { 0, 1, 2, -1, 1, 3, 2, -1, 3, 0, 2, -1, 0, 3, 1, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 1f, 0f, -0.07625f),
					new float4(-0.3347945f, -0.7087291f, 0.620979667f, -0.06485877f),
					new float4(-0.9103665f, 0f, -0.413802952f, -0.828290761f),
					new float4(0.474536151f, 0f, -0.880236f, -0.06129127f),
					new float4(-0.391126066f, 0.000249018951f, 0.9203371f, -0.0921159461f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(61.6672249f, 1.0675f, 5.69016647f, 1f)), name: "Solid 716308 #1525")
					.WithMesh(new float3[] { new float3(-0.8006621f, 0.07625f, -0.2401977f), new float3(1.48702812f, 0.07625f, 0.7320279f), new float3(-0.705349863f, 0.07625f, -0.449885339f), new float3(-0.8006624f, 0.07625f, -0.240197852f), new float3(1.48757064f, -0.152573347f, 0.732320368f), new float3(-0.705349863f, -0.152499974f, -0.449885339f) }, new int[] { 0, 1, 2, 3, -1, 4, 0, 3, 5, -1, 2, 5, 3, -1, 1, 4, 5, 2, -1, 4, 1, 0, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 1f, 0f, -0.07625f),
					new float4(-0.4456528f, -0.712205f, 0.542363f, -0.06517796f),
					new float4(-0.81892705f, 0.0008275957f, -0.573897f, -0.828737259f),
					new float4(0.634882569f, -0.000335065764f, -0.7726086f, -0.0618987046f),
					new float4(-0.5599715f, 0f, 0.8285119f, -0.0931128561f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(63.7607346f, 1.0675f, 7.00071335f, 1f)), name: "Solid 716310 #1527")
					.WithMesh(new float3[] { new float3(-0.740109563f, 0.07625f, -0.387836844f), new float3(1.31997573f, 0.07625f, 1.0045259f), new float3(-0.606481254f, 0.07625f, -0.578518867f), new float3(1.31929588f, -0.15242441f, 1.00406635f), new float3(-0.740109563f, 0.07624998f, -0.387836844f), new float3(-0.606672049f, -0.152500048f, -0.578576446f) }, new int[] { 0, 1, 2, -1, 3, 4, 5, -1, 4, 0, 2, 5, -1, 1, 3, 5, 2, -1, 3, 1, 0, 4, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 1f, 0f, -0.029652806f),
					new float4(0f, -1f, 0f, -0.0465971939f),
					new float4(0.707106769f, 0f, 0.707106769f, -1.23487747f),
					new float4(-0.841003f, 0f, -0.541030467f, -1.23329711f),
					new float4(0.6357027f, 0f, -0.771934f, -0.169469714f),
					new float4(-0.629463851f, 0f, 0.777029753f, -0.135585964f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(64.05847f, 1.19034719f, 7.278847f, 1f)), name: "Solid 716323 #1540")
					.WithMesh(new float3[] { new float3(-1.03784668f, 0.029652806f, -0.6662563f), new float3(0.868403256f, 0.029652806f, 0.877977252f), new float3(1.07809079f, 0.029652806f, 0.6682897f), new float3(-0.866284132f, 0.029652806f, -0.9329411f), new float3(0.868403256f, -0.0465971939f, 0.877977252f), new float3(-1.03784668f, -0.0465971939f, -0.6662563f), new float3(-0.866284132f, -0.0465971939f, -0.9329411f), new float3(1.07809079f, -0.0465971939f, 0.6682897f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 1, 4, 7, 2, -1, 5, 0, 3, 6, -1, 6, 3, 2, 7, -1, 0, 5, 4, 1, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 1f, 0f, -0.118611068f),
					new float4(0f, -1f, 0f, -0.186388925f),
					new float4(0.707106769f, 0f, 0.707106769f, -1.2460711f),
					new float4(-0.8944272f, 0f, -0.4472136f, -1.22049463f),
					new float4(0.6357027f, 0f, -0.771934f, -0.0454419143f),
					new float4(-0.634897947f, 0f, 0.772596f, -0.0363583453f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(64.1379f, 1.10138893f, 7.18358755f, 1f)), name: "Solid 716331 #1548")
					.WithMesh(new float3[] { new float3(-0.9838388f, 0.118611068f, -0.761431456f), new float3(0.9414737f, 0.118611068f, 0.820736945f), new float3(0.9986612f, 0.118611068f, 0.763549447f), new float3(-0.945713758f, 0.118611068f, -0.8376814f), new float3(0.9414737f, -0.186388925f, 0.820736945f), new float3(-0.9838388f, -0.186388925f, -0.761431456f), new float3(-0.945713758f, -0.186388925f, -0.8376814f), new float3(0.9986612f, -0.186388925f, 0.763549447f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 1, 4, 7, 2, -1, 5, 0, 3, 6, -1, 6, 3, 2, 7, -1, 0, 5, 4, 1, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 1f, 0f, -0.118611068f),
					new float4(0f, -1f, 0f, -0.186388925f),
					new float4(0.8944272f, 0f, 0.4472136f, -1.24843872f),
					new float4(-0.9701425f, 0f, -0.242535621f, -1.21080172f),
					new float4(0.471377283f, 0f, -0.881931663f, -0.044847f),
					new float4(-0.4745562f, 0f, 0.8802252f, -0.0358613059f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(62.07068f, 1.10138893f, 5.79733f, 1f)), name: "Solid 716332 #1549")
					.WithMesh(new float3[] { new float3(-1.10880363f, 0.118611068f, -0.5570488f), new float3(1.0833838f, 0.118611068f, 0.624826252f), new float3(1.12150884f, 0.118611068f, 0.548576236f), new float3(-1.08974123f, 0.118611068f, -0.633298755f), new float3(1.0833838f, -0.186388925f, 0.624826252f), new float3(-1.10880363f, -0.186388925f, -0.5570488f), new float3(-1.08974123f, -0.186388925f, -0.633298755f), new float3(1.12150884f, -0.186388925f, 0.548576236f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 1, 4, 7, 2, -1, 5, 0, 3, 6, -1, 6, 3, 2, 7, -1, 0, 5, 4, 1, -1 }))
				;
		}

		// Near (63.151, 1.047, 6.421): the exact CSG makes a triangle the mesh does not have
		static ContentsScene DetailsSite59()
		{
			return new ContentsScene().WithContentsList(kMapContents)
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 1f, 0f, -0.0571874864f),
					new float4(-0.334218144f, -0.7107627f, 0.6189625f, -0.040646825f),
					new float4(0.8192319f, 0f, 0.573462367f, -0.6073821f),
					new float4(0.3911702f, 0f, -0.920318365f, -0.0569079854f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(62.549305f, 1.08656251f, 6.22694635f, 1f)), name: "Solid 716307 #1524")
					.WithMesh(new float3[] { new float3(0.6047575f, 0.0571874864f, 0.195209771f), new float3(-1.68083453f, 0.0571874864f, -0.7762535f), new float3(0.47132f, 0.0571874864f, 0.385834754f), new float3(0.6047575f, -0.171562508f, 0.195209771f) }, new int[] { 0, 1, 2, -1, 1, 3, 2, -1, 3, 0, 2, -1, 0, 3, 1, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 1f, 0f, -0.07625f),
					new float4(-0.3347945f, -0.7087291f, 0.620979667f, -0.06485877f),
					new float4(-0.9103665f, 0f, -0.413802952f, -0.828290761f),
					new float4(0.474536151f, 0f, -0.880236f, -0.06129127f),
					new float4(-0.391126066f, 0.000249018951f, 0.9203371f, -0.0921159461f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(61.6672249f, 1.0675f, 5.69016647f, 1f)), name: "Solid 716308 #1525")
					.WithMesh(new float3[] { new float3(-0.8006621f, 0.07625f, -0.2401977f), new float3(1.48702812f, 0.07625f, 0.7320279f), new float3(-0.705349863f, 0.07625f, -0.449885339f), new float3(-0.8006624f, 0.07625f, -0.240197852f), new float3(1.48757064f, -0.152573347f, 0.732320368f), new float3(-0.705349863f, -0.152499974f, -0.449885339f) }, new int[] { 0, 1, 2, 3, -1, 4, 0, 3, 5, -1, 2, 5, 3, -1, 1, 4, 5, 2, -1, 4, 1, 0, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 1f, 0f, -0.07625f),
					new float4(-0.4456528f, -0.712205f, 0.542363f, -0.06517796f),
					new float4(-0.81892705f, 0.0008275957f, -0.573897f, -0.828737259f),
					new float4(0.634882569f, -0.000335065764f, -0.7726086f, -0.0618987046f),
					new float4(-0.5599715f, 0f, 0.8285119f, -0.0931128561f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(63.7607346f, 1.0675f, 7.00071335f, 1f)), name: "Solid 716310 #1527")
					.WithMesh(new float3[] { new float3(-0.740109563f, 0.07625f, -0.387836844f), new float3(1.31997573f, 0.07625f, 1.0045259f), new float3(-0.606481254f, 0.07625f, -0.578518867f), new float3(1.31929588f, -0.15242441f, 1.00406635f), new float3(-0.740109563f, 0.07624998f, -0.387836844f), new float3(-0.606672049f, -0.152500048f, -0.578576446f) }, new int[] { 0, 1, 2, -1, 3, 4, 5, -1, 4, 0, 2, 5, -1, 1, 3, 5, 2, -1, 3, 1, 0, 4, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 1f, 0f, -0.029652806f),
					new float4(0f, -1f, 0f, -0.0465971939f),
					new float4(0.707106769f, 0f, 0.707106769f, -1.23487747f),
					new float4(-0.841003f, 0f, -0.541030467f, -1.23329711f),
					new float4(0.6357027f, 0f, -0.771934f, -0.169469714f),
					new float4(-0.629463851f, 0f, 0.777029753f, -0.135585964f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(64.05847f, 1.19034719f, 7.278847f, 1f)), name: "Solid 716323 #1540")
					.WithMesh(new float3[] { new float3(-1.03784668f, 0.029652806f, -0.6662563f), new float3(0.868403256f, 0.029652806f, 0.877977252f), new float3(1.07809079f, 0.029652806f, 0.6682897f), new float3(-0.866284132f, 0.029652806f, -0.9329411f), new float3(0.868403256f, -0.0465971939f, 0.877977252f), new float3(-1.03784668f, -0.0465971939f, -0.6662563f), new float3(-0.866284132f, -0.0465971939f, -0.9329411f), new float3(1.07809079f, -0.0465971939f, 0.6682897f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 1, 4, 7, 2, -1, 5, 0, 3, 6, -1, 6, 3, 2, 7, -1, 0, 5, 4, 1, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 1f, 0f, -0.029652806f),
					new float4(0f, -1f, 0f, -0.0465971939f),
					new float4(0.8411785f, 0f, 0.5407576f, -1.23335433f),
					new float4(-0.9284767f, 0f, -0.371390671f, -1.22930169f),
					new float4(0.471377283f, 0f, -0.881931663f, -0.172867969f),
					new float4(-0.475028336f, 0f, 0.8799705f, -0.138286754f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(62.0071373f, 1.19034719f, 5.908528f, 1f)), name: "Solid 716324 #1541")
					.WithMesh(new float3[] { new float3(-1.14057481f, 0.029652806f, -0.458559215f), new float3(1.0134877f, 0.029652806f, 0.7042533f), new float3(1.18505013f, 0.029652806f, 0.4373783f), new float3(-1.02619982f, 0.029652806f, -0.7444967f), new float3(1.0134877f, -0.0465971939f, 0.7042533f), new float3(-1.14057481f, -0.0465971939f, -0.458559215f), new float3(-1.02619982f, -0.0465971939f, -0.7444967f), new float3(1.18505013f, -0.0465971939f, 0.4373783f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 1, 4, 7, 2, -1, 5, 0, 3, 6, -1, 6, 3, 2, 7, -1, 0, 5, 4, 1, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 1f, 0f, -0.118611068f),
					new float4(0f, -1f, 0f, -0.186388925f),
					new float4(0.707106769f, 0f, 0.707106769f, -1.2460711f),
					new float4(-0.8944272f, 0f, -0.4472136f, -1.22049463f),
					new float4(0.6357027f, 0f, -0.771934f, -0.0454419143f),
					new float4(-0.634897947f, 0f, 0.772596f, -0.0363583453f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(64.1379f, 1.10138893f, 7.18358755f, 1f)), name: "Solid 716331 #1548")
					.WithMesh(new float3[] { new float3(-0.9838388f, 0.118611068f, -0.761431456f), new float3(0.9414737f, 0.118611068f, 0.820736945f), new float3(0.9986612f, 0.118611068f, 0.763549447f), new float3(-0.945713758f, 0.118611068f, -0.8376814f), new float3(0.9414737f, -0.186388925f, 0.820736945f), new float3(-0.9838388f, -0.186388925f, -0.761431456f), new float3(-0.945713758f, -0.186388925f, -0.8376814f), new float3(0.9986612f, -0.186388925f, 0.763549447f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 1, 4, 7, 2, -1, 5, 0, 3, 6, -1, 6, 3, 2, 7, -1, 0, 5, 4, 1, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 1f, 0f, -0.118611068f),
					new float4(0f, -1f, 0f, -0.186388925f),
					new float4(0.8944272f, 0f, 0.4472136f, -1.24843872f),
					new float4(-0.9701425f, 0f, -0.242535621f, -1.21080172f),
					new float4(0.471377283f, 0f, -0.881931663f, -0.044847f),
					new float4(-0.4745562f, 0f, 0.8802252f, -0.0358613059f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(62.07068f, 1.10138893f, 5.79733f, 1f)), name: "Solid 716332 #1549")
					.WithMesh(new float3[] { new float3(-1.10880363f, 0.118611068f, -0.5570488f), new float3(1.0833838f, 0.118611068f, 0.624826252f), new float3(1.12150884f, 0.118611068f, 0.548576236f), new float3(-1.08974123f, 0.118611068f, -0.633298755f), new float3(1.0833838f, -0.186388925f, 0.624826252f), new float3(-1.10880363f, -0.186388925f, -0.5570488f), new float3(-1.08974123f, -0.186388925f, -0.633298755f), new float3(1.12150884f, -0.186388925f, 0.548576236f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 1, 4, 7, 2, -1, 5, 0, 3, 6, -1, 6, 3, 2, 7, -1, 0, 5, 4, 1, -1 }))
				;
		}

		// Near (58.471, -5.82, 69.235): the exact CSG makes a triangle the mesh does not have
		static ContentsScene DetailsSite60()
		{
			return new ContentsScene().WithContentsList(kMapContents)
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(-1f, 0f, 0f, -0.07625031f),
					new float4(1f, 0f, 0f, -0.0762497f),
					new float4(0f, 1f, 0f, -0.038125135f),
					new float4(0f, -0.707106769f, -0.707106769f, -0.652992547f),
					new float4(0f, -0.707106769f, 0.707106769f, -1.018431f),
					new float4(0f, -1f, 0f, -0.0381248668f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(58.4456253f, -5.375625f, 70.1203461f, 1f)), name: "Solid 127862 #3374")
					.WithMesh(new float3[] { new float3(-0.07625031f, -0.0381248668f, -0.885346055f), new float3(-0.07625031f, -0.0381248668f, 1.402154f), new float3(-0.07625031f, 0.038125135f, 1.478404f), new float3(-0.07625031f, 0.038125135f, -0.9615961f), new float3(0.0762497f, -0.0381248668f, 1.402154f), new float3(0.0762497f, -0.0381248668f, -0.885346055f), new float3(0.0762497f, 0.038125135f, -0.9615961f), new float3(0.0762497f, 0.038125135f, 1.478404f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 2, 7, 6, 3, -1, 5, 0, 3, 6, -1, 1, 4, 7, 2, -1, 4, 1, 0, 5, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(-1f, 0f, 0f, -0.0677778646f),
					new float4(1f, 0f, 0f, -0.08472214f),
					new float4(0f, 0f, -1f, -0.03389221f),
					new float4(0f, -0.707106769f, 0.707106769f, -0.359443426f),
					new float4(0f, 0.707106769f, 0.707106769f, -0.5631298f),
					new float4(0f, 0f, 1f, -0.0423577875f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(58.4371529f, -6.167778f, 69.19264f, 1f)), name: "Solid 127863 #3375")
					.WithMesh(new float3[] { new float3(-0.0677778646f, -0.465971977f, 0.0423577875f), new float3(-0.0677778646f, 0.754028f, 0.0423577875f), new float3(-0.0677778646f, 0.830278f, -0.03389221f), new float3(-0.0677778646f, -0.542221963f, -0.03389221f), new float3(0.08472214f, 0.754028f, 0.0423577875f), new float3(0.08472214f, -0.465971977f, 0.0423577875f), new float3(0.08472214f, -0.542221963f, -0.03389221f), new float3(0.08472214f, 0.830278f, -0.03389221f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 2, 7, 6, 3, -1, 5, 0, 3, 6, -1, 1, 4, 7, 2, -1, 4, 1, 0, 5, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(-1f, 0f, 0f, -0.0381253064f),
					new float4(1f, 0f, 0f, -0.0381246954f),
					new float4(0f, 0f, -1f, -1.01666319f),
					new float4(0f, 0f, 1f, -1.27083683f),
					new float4(0f, -1f, 0f, -0.474444419f),
					new float4(0f, 1f, 0f, -0.7455556f),
				}, contents: 2, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(58.4456253f, -6.15930557f, 70.25166f, 1f)), name: "Glass 127864 #3376")
					.WithMesh(new float3[] { new float3(-0.0381253064f, 0.7455556f, 1.27083683f), new float3(-0.0381253064f, 0.7455556f, -1.01666319f), new float3(-0.0381253064f, -0.474444419f, -1.01666319f), new float3(-0.0381253064f, -0.474444419f, 1.27083683f), new float3(0.0381246954f, 0.7455556f, -1.01666319f), new float3(0.0381246954f, 0.7455556f, 1.27083683f), new float3(0.0381246954f, -0.474444419f, 1.27083683f), new float3(0.0381246954f, -0.474444419f, -1.01666319f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 1, 4, 7, 2, -1, 5, 0, 3, 6, -1, 6, 3, 2, 7, -1, 0, 5, 4, 1, -1 }))
				;
		}

		// Near (89.778, -11.819, 49.867): the mesh has a triangle the exact CSG does not make
		static ContentsScene DetailsSite61()
		{
			return new ContentsScene().WithContentsList(kMapContents)
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(1f, 0f, 0f, -0.0847232044f),
					new float4(-1f, 0f, 0f, -0.06777679f),
					new float4(0f, 0f, -1f, -0.03388779f),
					new float4(0f, -0.707106769f, 0.707106769f, -0.485252917f),
					new float4(0f, 0.707106769f, 0.707106769f, -0.7608279f),
					new float4(0f, 0f, 1f, -0.0423622131f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(89.7759f, -11.174861f, 45.7076378f, 1f)), name: "Solid 149648 #1875")
					.WithMesh(new float3[] { new float3(0.0847232044f, 1.03361094f, 0.0423622131f), new float3(0.0847232044f, -0.64388907f, 0.0423622131f), new float3(0.0847232044f, -0.7201391f, -0.03388779f), new float3(0.0847232044f, 1.1098609f, -0.03388779f), new float3(-0.06777679f, -0.64388907f, 0.0423622131f), new float3(-0.06777679f, 1.03361094f, 0.0423622131f), new float3(-0.06777679f, 1.1098609f, -0.03388779f), new float3(-0.06777679f, -0.7201391f, -0.03388779f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 6, 3, 2, 7, -1, 1, 4, 7, 2, -1, 5, 0, 3, 6, -1, 0, 5, 4, 1, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(1f, 0f, 0f, -0.03812195f),
					new float4(-1f, 0f, 0f, -0.0381280519f),
					new float4(0f, 0f, 1f, -2.28749824f),
					new float4(0f, 0f, -1f, -1.83000183f),
					new float4(0f, -1f, 0f, -0.6523615f),
					new float4(0f, 1f, 0f, -1.0251385f),
				}, contents: 2, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(89.78438f, -11.1663885f, 47.58f, 1f)), name: "Glass 149650 #1877")
					.WithMesh(new float3[] { new float3(0.03812195f, 1.0251385f, -1.83000183f), new float3(0.03812195f, 1.0251385f, 2.28749824f), new float3(0.03812195f, -0.6523615f, 2.28749824f), new float3(0.03812195f, -0.6523615f, -1.83000183f), new float3(-0.0381280519f, 1.0251385f, 2.28749824f), new float3(-0.0381280519f, 1.0251385f, -1.83000183f), new float3(-0.0381280519f, -0.6523615f, -1.83000183f), new float3(-0.0381280519f, -0.6523615f, 2.28749824f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 1, 4, 7, 2, -1, 5, 0, 3, 6, -1, 6, 3, 2, 7, -1, 0, 5, 4, 1, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(1f, 0f, 0f, -0.07624695f),
					new float4(-1f, 0f, 0f, -0.07625305f),
					new float4(0f, -1f, 0f, -0.029653091f),
					new float4(0f, 0.707106769f, 0.707106769f, -1.49170172f),
					new float4(0f, 0.707106769f, -0.707106769f, -1.48570836f),
					new float4(0f, 1f, 0f, -0.0465969071f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(89.78438f, -11.8653469f, 47.804512f, 1f)), name: "Solid 149651 #1878")
					.WithMesh(new float3[] { new float3(0.07624695f, 0.0465969071f, -2.054512f), new float3(0.07624695f, 0.0465969071f, 2.062988f), new float3(0.07624695f, -0.029653091f, 2.13923788f), new float3(0.07624695f, -0.029653091f, -2.130762f), new float3(-0.07625305f, 0.0465969071f, 2.062988f), new float3(-0.07625305f, 0.0465969071f, -2.054512f), new float3(-0.07625305f, -0.029653091f, -2.130762f), new float3(-0.07625305f, -0.029653091f, 2.13923788f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 6, 3, 2, 7, -1, 1, 4, 7, 2, -1, 5, 0, 3, 6, -1, 0, 5, 4, 1, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(1f, 0f, 0f, -0.06777832f),
					new float4(-1f, 0f, 0f, -0.08472168f),
					new float4(0f, 0f, 1f, -0.0338874832f),
					new float4(0f, -0.707106769f, -0.707106769f, -0.485253125f),
					new float4(0f, 0.707106769f, -0.707106769f, -0.760828137f),
					new float4(0f, 0f, -1f, -0.04236252f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(89.79285f, -11.174861f, 49.9098625f, 1f)), name: "Solid 149652 #1879")
					.WithMesh(new float3[] { new float3(0.06777832f, -0.64388907f, -0.04236252f), new float3(0.06777832f, 1.03361094f, -0.04236252f), new float3(0.06777832f, 1.109861f, 0.0338874832f), new float3(0.06777832f, -0.720139f, 0.0338874832f), new float3(-0.08472168f, 1.03361094f, -0.04236252f), new float3(-0.08472168f, -0.64388907f, -0.04236252f), new float3(-0.08472168f, -0.720139f, 0.0338874832f), new float3(-0.08472168f, 1.109861f, 0.0338874832f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 2, 7, 6, 3, -1, 5, 0, 3, 6, -1, 1, 4, 7, 2, -1, 4, 1, 0, 5, -1 }))
				;
		}

		// Near (-3.279, 2.516, 167.826): the exact CSG makes a triangle the mesh does not have
		static ContentsScene DetailsSite62()
		{
			return new ContentsScene().WithContentsList(kMapContents)
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(1f, 0f, 0f, -0.08472215f),
					new float4(-1f, 0f, 0f, -0.06777785f),
					new float4(0f, 0f, -1f, -0.03388977f),
					new float4(0f, -0.707106769f, 0.707106769f, -0.3594453f),
					new float4(0f, 0.707106769f, 0.707106769f, -0.5631314f),
					new float4(0f, 0f, 1f, -0.04236023f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(-3.28722215f, 2.37222219f, 167.78389f, 1f)), name: "Solid 247426 #2013")
					.WithMesh(new float3[] { new float3(0.08472215f, 0.754027843f, 0.04236023f), new float3(0.08472215f, -0.465972215f, 0.04236023f), new float3(0.08472215f, -0.5422222f, -0.03388977f), new float3(0.08472215f, 0.83027786f, -0.03388977f), new float3(-0.06777785f, -0.465972215f, 0.04236023f), new float3(-0.06777785f, 0.754027843f, 0.04236023f), new float3(-0.06777785f, 0.83027786f, -0.03388977f), new float3(-0.06777785f, -0.5422222f, -0.03388977f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 6, 3, 2, 7, -1, 1, 4, 7, 2, -1, 5, 0, 3, 6, -1, 0, 5, 4, 1, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(1f, 0f, 0f, -0.07624994f),
					new float4(-1f, 0f, 0f, -0.0762500539f),
					new float4(0f, 1f, 0f, -0.0381249338f),
					new float4(0f, -0.707106769f, 0.707106769f, -0.838710546f),
					new float4(0f, -0.707106769f, -0.707106769f, -0.832713246f),
					new float4(0f, -1f, 0f, -0.0381250679f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(-3.27875f, 3.164375f, 168.965759f, 1f)), name: "Solid 247428 #2015")
					.WithMesh(new float3[] { new float3(0.07624994f, -0.0381250679f, 1.14799082f), new float3(0.07624994f, -0.0381250679f, -1.13950932f), new float3(0.07624994f, 0.0381249338f, -1.21575928f), new float3(0.07624994f, 0.0381249338f, 1.22424078f), new float3(-0.0762500539f, -0.0381250679f, -1.13950932f), new float3(-0.0762500539f, -0.0381250679f, 1.14799082f), new float3(-0.0762500539f, 0.0381249338f, 1.22424078f), new float3(-0.0762500539f, 0.0381249338f, -1.21575928f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 2, 7, 6, 3, -1, 5, 0, 3, 6, -1, 1, 4, 7, 2, -1, 4, 1, 0, 5, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(1f, 0f, 0f, -0.03812494f),
					new float4(-1f, 0f, 0f, -0.0381250568f),
					new float4(0f, 0f, 1f, -1.27083921f),
					new float4(0f, 0f, -1f, -1.01666081f),
					new float4(0f, -1f, 0f, -0.4744444f),
					new float4(0f, 1f, 0f, -0.745555639f),
				}, contents: 2, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(-3.27875f, 2.38069439f, 168.842911f, 1f)), name: "Glass 247430 #2017")
					.WithMesh(new float3[] { new float3(0.03812494f, 0.745555639f, -1.01666081f), new float3(0.03812494f, 0.745555639f, 1.27083921f), new float3(0.03812494f, -0.4744444f, 1.27083921f), new float3(0.03812494f, -0.4744444f, -1.01666081f), new float3(-0.0381250568f, 0.745555639f, 1.27083921f), new float3(-0.0381250568f, 0.745555639f, -1.01666081f), new float3(-0.0381250568f, -0.4744444f, -1.01666081f), new float3(-0.0381250568f, -0.4744444f, 1.27083921f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 1, 4, 7, 2, -1, 5, 0, 3, 6, -1, 6, 3, 2, 7, -1, 0, 5, 4, 1, -1 }))
				;
		}

		// Near (-3.279, 2.516, 145.866): the exact CSG makes a triangle the mesh does not have
		static ContentsScene DetailsSite63()
		{
			return new ContentsScene().WithContentsList(kMapContents)
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(1f, 0f, 0f, -0.08472215f),
					new float4(-1f, 0f, 0f, -0.06777785f),
					new float4(0f, 0f, -1f, -0.0338830575f),
					new float4(0f, -0.707106769f, 0.707106769f, -0.359450042f),
					new float4(0f, 0.707106769f, 0.707106769f, -0.56313616f),
					new float4(0f, 0f, 1f, -0.0423669443f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(-3.28722215f, 2.37222219f, 145.823883f, 1f)), name: "Solid 3897 #1993")
					.WithMesh(new float3[] { new float3(0.08472215f, 0.754027843f, 0.0423669443f), new float3(0.08472215f, -0.4659722f, 0.0423669443f), new float3(0.08472215f, -0.5422222f, -0.0338830575f), new float3(0.08472215f, 0.83027786f, -0.0338830575f), new float3(-0.06777785f, -0.4659722f, 0.0423669443f), new float3(-0.06777785f, 0.754027843f, 0.0423669443f), new float3(-0.06777785f, 0.83027786f, -0.0338830575f), new float3(-0.06777785f, -0.5422222f, -0.0338830575f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 6, 3, 2, 7, -1, 1, 4, 7, 2, -1, 5, 0, 3, 6, -1, 0, 5, 4, 1, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(1f, 0f, 0f, -0.07624994f),
					new float4(-1f, 0f, 0f, -0.0762500539f),
					new float4(0f, 1f, 0f, -0.0381249338f),
					new float4(0f, -0.707106769f, 0.707106769f, -0.838704467f),
					new float4(0f, -0.707106769f, -0.707106769f, -0.832719266f),
					new float4(0f, -1f, 0f, -0.0381250679f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(-3.27875f, 3.164375f, 147.005768f, 1f)), name: "Solid 3899 #1995")
					.WithMesh(new float3[] { new float3(0.07624994f, -0.0381250679f, 1.14798224f), new float3(0.07624994f, -0.0381250679f, -1.13951778f), new float3(0.07624994f, 0.0381249338f, -1.21576786f), new float3(0.07624994f, 0.0381249338f, 1.2242322f), new float3(-0.0762500539f, -0.0381250679f, -1.13951778f), new float3(-0.0762500539f, -0.0381250679f, 1.14798224f), new float3(-0.0762500539f, 0.0381249338f, 1.2242322f), new float3(-0.0762500539f, 0.0381249338f, -1.21576786f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 2, 7, 6, 3, -1, 5, 0, 3, 6, -1, 1, 4, 7, 2, -1, 4, 1, 0, 5, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(1f, 0f, 0f, -0.03812494f),
					new float4(-1f, 0f, 0f, -0.0381250568f),
					new float4(0f, 0f, 1f, -1.27083063f),
					new float4(0f, 0f, -1f, -1.01666927f),
					new float4(0f, -1f, 0f, -0.4744444f),
					new float4(0f, 1f, 0f, -0.745555639f),
				}, contents: 2, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(-3.27875f, 2.38069439f, 146.882919f, 1f)), name: "Glass 3901 #1997")
					.WithMesh(new float3[] { new float3(0.03812494f, 0.745555639f, -1.01666927f), new float3(0.03812494f, 0.745555639f, 1.27083063f), new float3(0.03812494f, -0.4744444f, 1.27083063f), new float3(0.03812494f, -0.4744444f, -1.01666927f), new float3(-0.0381250568f, 0.745555639f, 1.27083063f), new float3(-0.0381250568f, 0.745555639f, -1.01666927f), new float3(-0.0381250568f, -0.4744444f, -1.01666927f), new float3(-0.0381250568f, -0.4744444f, 1.27083063f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 1, 4, 7, 2, -1, 5, 0, 3, 6, -1, 6, 3, 2, 7, -1, 0, 5, 4, 1, -1 }))
				;
		}

		// Near (27.393, 0.737, 105.473): the exact CSG makes a triangle the mesh does not have
		static ContentsScene DetailsSite64()
		{
			return new ContentsScene().WithContentsList(kMapContents)
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(-0.1825622f, 0f, 0.9831943f, -0.383560538f),
					new float4(0.07829309f, 0f, -0.996930361f, -0.303404331f),
					new float4(0.2887418f, 0f, 0.957407f, -1.32504416f),
					new float4(0f, 1f, 0f, -0.732f),
					new float4(0f, -1f, 0f, -0.488f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(22.9512558f, 0.488f, 105.428337f, 1f)), name: "Solid 329351 #3435")
					.WithMesh(new float3[] { new float3(-6.481237f, -0.488f, -0.8133371f), new float3(2.039682f, -0.488f, 0.7688504f), new float3(2.039682f, 0.732f, 0.7688504f), new float3(-6.481237f, 0.732f, -0.8133371f), new float3(4.441557f, -0.488f, 0.04447541f), new float3(4.441557f, 0.732f, 0.04447541f) }, new int[] { 0, 1, 2, 3, -1, 4, 0, 3, 5, -1, 1, 4, 5, 2, -1, 2, 5, 3, -1, 4, 1, 0, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(-0.078294456f, 0f, 0.9969303f, -0.356419384f),
					new float4(1f, 0f, 0f, -3.67586017f),
					new float4(0f, 0f, -1f, -0.407727957f),
					new float4(0.09875158f, 0f, 0.9951121f, -0.564280331f),
					new float4(0f, 1f, 0f, -0.7455556f),
					new float4(0f, -1f, 0f, -0.474444449f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(26.21414f, 0.474444449f, 105.022728f, 1f)), name: "Solid 329350 #3438")
					.WithMesh(new float3[] { new float3(-9.743931f, -0.474444449f, -0.407727957f), new float3(1.17867243f, -0.474444449f, 0.450084537f), new float3(1.17867243f, 0.7455556f, 0.450084537f), new float3(-9.743931f, 0.7455556f, -0.407727957f), new float3(3.67586017f, -0.474444449f, 0.202272f), new float3(3.67586017f, -0.474444449f, -0.407727957f), new float3(3.67586017f, 0.7455556f, -0.407727957f), new float3(3.67586017f, 0.7455556f, 0.202272f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 5, 0, 3, 6, -1, 1, 4, 7, 2, -1, 2, 7, 6, 3, -1, 4, 1, 0, 5, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 1f, 0f, -0.0465972126f),
					new float4(0f, -1f, 0f, -0.0296527855f),
					new float4(0.9828722f, 0f, -0.184288532f, -1.23052585f),
					new float4(-0.9284767f, 0f, 0.371390671f, -1.23284054f),
					new float4(-0.2887418f, 0f, -0.957407f, -0.170914218f),
					new float4(0.288062066f, 0f, 0.95761174f, -0.136739239f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(26.23953f, 1.17340279f, 105.999146f, 1f)), name: "Solid 503714 #3714")
					.WithMesh(new float3[] { new float3(-1.1342181f, 0.0465972126f, 0.4839795f), new float3(1.21046937f, 0.0465972126f, -0.221333f), new float3(1.15328193f, 0.0465972126f, -0.526333034f), new float3(-1.24859309f, 0.0465972126f, 0.198042f), new float3(1.21046937f, -0.0296527855f, -0.221333f), new float3(-1.1342181f, -0.0296527855f, 0.4839795f), new float3(-1.24859309f, -0.0296527855f, 0.198042f), new float3(1.15328193f, -0.0296527855f, -0.526333034f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 1, 4, 7, 2, -1, 5, 0, 3, 6, -1, 6, 3, 2, 7, -1, 0, 5, 4, 1, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 1f, 0f, -0.18638894f),
					new float4(0f, -1f, 0f, -0.11861106f),
					new float4(1f, 0f, 0f, -1.24329817f),
					new float4(-0.9701425f, 0f, 0.242535621f, -1.2362293f),
					new float4(-0.09875158f, 0f, -0.9951121f, -0.0426755138f),
					new float4(0.09950372f, 0f, 0.9950372f, -0.0341436565f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(28.6467018f, 1.033611f, 105.391266f, 1f)), name: "Solid 503722 #3721")
					.WithMesh(new float3[] { new float3(-1.2348268f, 0.18638894f, 0.157796636f), new float3(1.24329817f, 0.18638894f, -0.0900158659f), new float3(1.24329817f, 0.18638894f, -0.16626586f), new float3(-1.25388932f, 0.18638894f, 0.0815466344f), new float3(1.24329817f, -0.11861106f, -0.0900158659f), new float3(-1.2348268f, -0.11861106f, 0.157796636f), new float3(-1.25388932f, -0.11861106f, 0.0815466344f), new float3(1.24329817f, -0.11861106f, -0.16626586f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 1, 4, 7, 2, -1, 5, 0, 3, 6, -1, 6, 3, 2, 7, -1, 0, 5, 4, 1, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 1f, 0f, -0.18638894f),
					new float4(0f, -1f, 0f, -0.11861106f),
					new float4(0.9701425f, 0f, -0.242535621f, -1.392138f),
					new float4(-0.9701425f, 0f, 0.242535621f, -1.11370969f),
					new float4(-0.2887418f, 0f, -0.957407f, -0.0392532647f),
					new float4(0.2887418f, 0f, 0.957407f, -0.03925316f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(26.0679684f, 1.033611f, 105.913368f, 1f)), name: "Solid 503723 #3722")
					.WithMesh(new float3[] { new float3(-1.05796838f, 0.18638894f, 0.360069275f), new float3(1.34390664f, 0.18638894f, -0.364305735f), new float3(1.32484412f, 0.18638894f, -0.440555722f), new float3(-1.0770309f, 0.18638894f, 0.2838193f), new float3(1.34390664f, -0.11861106f, -0.364305735f), new float3(-1.05796838f, -0.11861106f, 0.360069275f), new float3(-1.0770309f, -0.11861106f, 0.2838193f), new float3(1.32484412f, -0.11861106f, -0.440555722f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 1, 4, 7, 2, -1, 5, 0, 3, 6, -1, 6, 3, 2, 7, -1, 0, 5, 4, 1, -1 }))
				;
		}

		// Near (44.937, 1.093, 89.06): the exact CSG makes a triangle the mesh does not have
		static ContentsScene DetailsSite65()
		{
			return new ContentsScene().WithContentsList(kMapContents)
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, -1f, 0f, -0.130714253f),
					new float4(0f, 1f, 0f, -0.17428574f),
					new float4(0f, 0f, -1f, -0.18154633f),
					new float4(0f, 0f, 1f, -0.123453677f),
					new float4(0f, -0.707106769f, -0.707106769f, -0.0590509f),
					new float4(-1f, 0f, 0f, -5.428275f),
					new float4(0.707106769f, 2.19416313E-14f, 0.707106769f, -4.228625f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(50.2632751f, 1.04571426f, 88.93655f, 1f)), name: "Solid 352671 #2795")
					.WithMesh(new float3[] { new float3(5.932975f, -0.130714253f, 0.0472036675f), new float3(5.856725f, -0.130714253f, 0.123453677f), new float3(-5.428275f, -0.130714253f, 0.123453677f), new float3(-5.428275f, -0.130714253f, 0.0472036675f), new float3(5.856725f, 0.17428574f, 0.123453677f), new float3(6.161725f, 0.17428574f, -0.18154633f), new float3(-5.428275f, 0.17428574f, -0.18154633f), new float3(-5.428275f, 0.17428574f, 0.123453677f), new float3(6.161725f, 0.0980357453f, -0.18154633f), new float3(-5.428275f, 0.0980357453f, -0.18154633f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 5, 8, 9, 6, -1, 1, 4, 7, 2, -1, 8, 0, 3, 9, -1, 9, 3, 2, 7, 6, -1, 0, 8, 5, 4, 1, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(1f, 0f, 0f, -3.67594981f),
					new float4(-0.07829025f, 0f, -0.9969306f, -0.3564211f),
					new float4(0f, 0f, 1f, -0.4077243f),
					new float4(0.09875158f, 0f, -0.9951121f, -0.564292848f),
					new float4(0f, 1f, 0f, -0.7455556f),
					new float4(0f, -1f, 0f, -0.474444449f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(41.15867f, 0.474444449f, 89.2622757f, 1f)), name: "Solid 354660 #2870")
					.WithMesh(new float3[] { new float3(3.67594981f, -0.474444449f, 0.4077243f), new float3(3.67594981f, -0.474444449f, -0.2022757f), new float3(3.67594981f, 0.7455556f, -0.2022757f), new float3(3.67594981f, 0.7455556f, 0.4077243f), new float3(1.1787622f, -0.474444449f, -0.4500882f), new float3(-9.7444315f, -0.474444449f, 0.4077243f), new float3(-9.7444315f, 0.7455556f, 0.4077243f), new float3(1.1787622f, 0.7455556f, -0.4500882f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 5, 0, 3, 6, -1, 1, 4, 7, 2, -1, 2, 7, 6, 3, -1, 4, 1, 0, 5, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 1f, 0f, -0.0465972126f),
					new float4(0f, -1f, 0f, -0.0296527855f),
					new float4(-0.9828722f, 0f, -0.184288532f, -1.23442829f),
					new float4(1f, 0f, 0f, -1.23270893f),
					new float4(-0.09875158f, 0f, 0.9951121f, -0.170186833f),
					new float4(0.10104271f, 0f, -0.9948821f, -0.136143982f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(43.60229f, 1.17340279f, 88.76665f, 1f)), name: "Solid 507268 #4201")
					.WithMesh(new float3[] { new float3(1.23270893f, 0.0465972126f, -0.0116473408f), new float3(-1.20729113f, 0.0465972126f, -0.259459823f), new float3(-1.26447856f, 0.0465972126f, 0.04554017f), new float3(1.23270893f, 0.0465972126f, 0.293352664f), new float3(-1.20729113f, -0.0296527855f, -0.259459823f), new float3(1.23270893f, -0.0296527855f, -0.0116473408f), new float3(1.23270893f, -0.0296527855f, 0.293352664f), new float3(-1.26447856f, -0.0296527855f, 0.04554017f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 1, 4, 7, 2, -1, 5, 0, 3, 6, -1, 6, 3, 2, 7, -1, 0, 5, 4, 1, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 1f, 0f, -0.18638894f),
					new float4(0f, -1f, 0f, -0.11861106f),
					new float4(-0.9701425f, 0f, -0.242535621f, -1.236228f),
					new float4(1f, 0f, 0f, -1.24329853f),
					new float4(-0.09875158f, 0f, 0.9951121f, -0.0426794328f),
					new float4(0.09950372f, 0f, -0.9950372f, -0.0341397375f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(43.5917f, 1.033611f, 88.89373f, 1f)), name: "Solid 507277 #4209")
					.WithMesh(new float3[] { new float3(1.24329853f, 0.18638894f, 0.09001984f), new float3(-1.23482645f, 0.18638894f, -0.157792658f), new float3(-1.253889f, 0.18638894f, -0.08154266f), new float3(1.24329853f, 0.18638894f, 0.166269839f), new float3(-1.23482645f, -0.11861106f, -0.157792658f), new float3(1.24329853f, -0.11861106f, 0.09001984f), new float3(1.24329853f, -0.11861106f, 0.166269839f), new float3(-1.253889f, -0.11861106f, -0.08154266f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 1, 4, 7, 2, -1, 5, 0, 3, 6, -1, 6, 3, 2, 7, -1, 0, 5, 4, 1, -1 }))
				;
		}

		// Near (68.93, -4.334, 91.042): the mesh has a triangle the exact CSG does not make
		static ContentsScene DetailsSite66()
		{
			return new ContentsScene().WithContentsList(kMapContents)
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 0f, -1f, -0.1355545f),
					new float4(0f, 0f, 1f, -0.1694455f),
					new float4(0f, 1f, 0f, -0.1164932f),
					new float4(-0.196116135f, -0.9805807f, 0f, -0.0913842842f),
					new float4(1f, 0f, 0f, -0.09531372f),
					new float4(-0.9805807f, 0.196116135f, 0f, -0.07892196f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(68.83469f, -4.386493f, 94.6855545f, 1f)), name: "Solid 338703 #2432")
					.WithMesh(new float3[] { new float3(-0.09531128f, -0.0741317943f, -0.1355545f), new float3(-0.05718628f, 0.1164932f, -0.1355545f), new float3(0.09531372f, 0.1164932f, -0.1355545f), new float3(0.09531372f, -0.112256795f, -0.1355545f), new float3(-0.05718628f, 0.1164932f, 0.1694455f), new float3(-0.09531128f, -0.0741317943f, 0.1694455f), new float3(0.09531372f, -0.112256795f, 0.1694455f), new float3(0.09531372f, 0.1164932f, 0.1694455f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 1, 4, 7, 2, -1, 5, 0, 3, 6, -1, 6, 3, 2, 7, -1, 0, 5, 4, 1, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 0f, -1f, -0.130717471f),
					new float4(0f, 0f, 1f, -0.174282536f),
					new float4(0.196116135f, 0.9805807f, 0f, -0.2207476f),
					new float4(-0.9805807f, -0.196116135f, 0f, -0.199384779f),
					new float4(0f, -1f, 0f, -0.199702144f),
					new float4(-0.707106769f, 0.707106769f, 0f, -0.1360762f),
					new float4(1f, 0f, 0f, -0.217857063f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(68.71214f, -4.680298f, 90.86822f, 1f)), name: "Solid 353337 #2470")
					.WithMesh(new float3[] { new float3(0.217857063f, 0.18154785f, -0.130717471f), new float3(0.217857063f, -0.199702144f, -0.130717471f), new float3(-0.163392946f, -0.199702144f, -0.130717471f), new float3(-0.20151794f, -0.009077138f, -0.130717471f), new float3(0.027232049f, 0.219672859f, -0.130717471f), new float3(0.217857063f, -0.199702144f, 0.174282536f), new float3(0.217857063f, 0.18154785f, 0.174282536f), new float3(0.027232049f, 0.219672859f, 0.174282536f), new float3(-0.20151794f, -0.009077138f, 0.174282536f), new float3(-0.163392946f, -0.199702144f, 0.174282536f) }, new int[] { 0, 1, 2, 3, 4, -1, 5, 6, 7, 8, 9, -1, 6, 0, 4, 7, -1, 8, 3, 2, 9, -1, 1, 5, 9, 2, -1, 3, 8, 7, 4, -1, 5, 1, 0, 6, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 0f, -1f, -0.1355545f),
					new float4(0f, 0f, 1f, -0.1694455f),
					new float4(-1f, 0f, 0f, -0.0677758753f),
					new float4(0f, -1f, 0f, -1.8977778f),
					new float4(0f, 1f, 0f, -2.982222f),
					new float4(1f, 0f, 0f, -0.08472412f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(68.8452759f, -2.37222219f, 90.8730545f, 1f)), name: "Solid 353341 #2474")
					.WithMesh(new float3[] { new float3(0.08472412f, 2.982222f, -0.1355545f), new float3(0.08472412f, -1.8977778f, -0.1355545f), new float3(-0.0677758753f, -1.8977778f, -0.1355545f), new float3(-0.0677758753f, 2.982222f, -0.1355545f), new float3(0.08472412f, -1.8977778f, 0.1694455f), new float3(0.08472412f, 2.982222f, 0.1694455f), new float3(-0.0677758753f, 2.982222f, 0.1694455f), new float3(-0.0677758753f, -1.8977778f, 0.1694455f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 6, 3, 2, 7, -1, 1, 4, 7, 2, -1, 5, 0, 3, 6, -1, 0, 5, 4, 1, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, -1f, 0f, -0.6523613f),
					new float4(0f, 1f, 0f, -1.02513874f),
					new float4(0f, 0f, -1f, -0.06777527f),
					new float4(0.707106769f, 0f, 0.707106769f, -1.15472126f),
					new float4(-0.8f, 0f, 0.6f, -1.61819661f),
					new float4(0f, 0f, 1f, -0.08472473f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(67.2292f, -4.22763872f, 91.1102753f, 1f)), name: "Solid 353430 #2845")
					.WithMesh(new float3[] { new float3(1.54829776f, -0.6523613f, 0.08472473f), new float3(-1.95920217f, -0.6523613f, 0.08472473f), new float3(-2.07357717f, -0.6523613f, -0.06777527f), new float3(1.7007978f, -0.6523613f, -0.06777527f), new float3(-1.95920217f, 1.02513874f, 0.08472473f), new float3(1.54829776f, 1.02513874f, 0.08472473f), new float3(1.7007978f, 1.02513874f, -0.06777527f), new float3(-2.07357717f, 1.02513874f, -0.06777527f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 2, 7, 6, 3, -1, 5, 0, 3, 6, -1, 1, 4, 7, 2, -1, 4, 1, 0, 5, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, -1f, 0f, -0.6523613f),
					new float4(0f, 1f, 0f, -1.02513874f),
					new float4(-0.707106769f, 0f, -0.707106769f, -1.06635427f),
					new float4(1f, 0f, 0f, -0.0677792355f),
					new float4(-0.707106769f, 0f, 0.707106769f, -1.31796849f),
					new float4(-1f, 0f, 0f, -0.08472076f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(68.86222f, -4.22763872f, 92.61833f, 1f)), name: "Solid 353422 #2848")
					.WithMesh(new float3[] { new float3(-0.08472076f, -0.6523613f, 1.77916813f), new float3(-0.08472076f, -0.6523613f, -1.423332f), new float3(0.0677792355f, -0.6523613f, -1.57583189f), new float3(0.0677792355f, -0.6523613f, 1.93166816f), new float3(-0.08472076f, 1.02513874f, -1.423332f), new float3(-0.08472076f, 1.02513874f, 1.77916813f), new float3(0.0677792355f, 1.02513874f, 1.93166816f), new float3(0.0677792355f, 1.02513874f, -1.57583189f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 1, 4, 7, 2, -1, 6, 3, 2, 7, -1, 5, 0, 3, 6, -1, 0, 5, 4, 1, -1 }))
				;
		}

		// Near (58.446, -6.024, 48.343): the exact CSG makes a triangle the mesh does not have
		static ContentsScene DetailsSite67()
		{
			return new ContentsScene().WithContentsList(kMapContents)
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(-1f, 0f, 0f, -0.0381253064f),
					new float4(1f, 0f, 0f, -0.0381246954f),
					new float4(0f, 0f, -1f, -1.01666677f),
					new float4(0f, 0f, 1f, -1.27083325f),
					new float4(0f, -1f, 0f, -0.474444419f),
					new float4(0f, 1f, 0f, -0.7455556f),
				}, contents: 2, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(58.4456253f, -6.15930557f, 47.0716667f, 1f)), name: "Glass 126414 #3390")
					.WithMesh(new float3[] { new float3(-0.0381253064f, 0.7455556f, 1.27083325f), new float3(-0.0381253064f, 0.7455556f, -1.01666677f), new float3(-0.0381253064f, -0.474444419f, -1.01666677f), new float3(-0.0381253064f, -0.474444419f, 1.27083325f), new float3(0.0381246954f, 0.7455556f, -1.01666677f), new float3(0.0381246954f, 0.7455556f, 1.27083325f), new float3(0.0381246954f, -0.474444419f, 1.27083325f), new float3(0.0381246954f, -0.474444419f, -1.01666677f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 1, 4, 7, 2, -1, 5, 0, 3, 6, -1, 6, 3, 2, 7, -1, 0, 5, 4, 1, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(-1f, 0f, 0f, -0.07625031f),
					new float4(1f, 0f, 0f, -0.0762497f),
					new float4(0f, -1f, 0f, -0.0296525583f),
					new float4(0f, 0.707106769f, -0.707106769f, -0.7488472f),
					new float4(0f, 0.707106769f, 0.707106769f, -0.934558332f),
					new float4(0f, 1f, 0f, -0.0465974435f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(58.4456253f, -6.68034744f, 47.0674324f, 1f)), name: "Solid 126415 #3391")
					.WithMesh(new float3[] { new float3(-0.07625031f, 0.0465974435f, 1.27506769f), new float3(-0.07625031f, 0.0465974435f, -1.01243246f), new float3(-0.07625031f, -0.0296525583f, -1.08868241f), new float3(-0.07625031f, -0.0296525583f, 1.35131764f), new float3(0.0762497f, 0.0465974435f, -1.01243246f), new float3(0.0762497f, 0.0465974435f, 1.27506769f), new float3(0.0762497f, -0.0296525583f, 1.35131764f), new float3(0.0762497f, -0.0296525583f, -1.08868241f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 6, 3, 2, 7, -1, 1, 4, 7, 2, -1, 5, 0, 3, 6, -1, 0, 5, 4, 1, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(-1f, 0f, 0f, -0.08472275f),
					new float4(1f, 0f, 0f, -0.06777725f),
					new float4(0f, 0f, 1f, -0.0338890068f),
					new float4(0f, -0.707106769f, -0.707106769f, -0.3594457f),
					new float4(0f, 0.707106769f, -0.707106769f, -0.563132048f),
					new float4(0f, 0f, -1f, -0.04236099f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(58.4540977f, -6.167778f, 48.38486f, 1f)), name: "Solid 126417 #3393")
					.WithMesh(new float3[] { new float3(-0.08472275f, 0.754028f, -0.04236099f), new float3(-0.08472275f, -0.465971977f, -0.04236099f), new float3(-0.08472275f, -0.542221963f, 0.0338890068f), new float3(-0.08472275f, 0.830278f, 0.0338890068f), new float3(0.06777725f, -0.465971977f, -0.04236099f), new float3(0.06777725f, 0.754028f, -0.04236099f), new float3(0.06777725f, 0.830278f, 0.0338890068f), new float3(0.06777725f, -0.542221963f, 0.0338890068f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 6, 3, 2, 7, -1, 1, 4, 7, 2, -1, 5, 0, 3, 6, -1, 0, 5, 4, 1, -1 }))
				;
		}

		// Near (35.901, -2.707, 4.461): the exact CSG makes a triangle the mesh does not have
		static ContentsScene DetailsSite68()
		{
			return new ContentsScene().WithContentsList(kMapContents)
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(1f, 0f, 0f, -0.135554656f),
					new float4(-1f, 0f, 0f, -0.169445336f),
					new float4(0f, 0f, 1f, -0.07625011f),
					new float4(0f, -1f, 0f, -0.698958337f),
					new float4(0f, 0f, -1f, -0.07624988f),
					new float4(0f, 0.8944272f, -0.4472136f, -0.9775343f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(37.64632f, -3.87604165f, 4.65125f, 1f)), name: "Solid 716928 #4538")
					.WithMesh(new float3[] { new float3(0.135554656f, 1.05479169f, -0.07624988f), new float3(0.135554656f, 1.13104165f, 0.07625011f), new float3(0.135554656f, -0.698958337f, 0.07625011f), new float3(0.135554656f, -0.698958337f, -0.07624988f), new float3(-0.169445336f, 1.13104165f, 0.07625011f), new float3(-0.169445336f, 1.05479169f, -0.07624988f), new float3(-0.169445336f, -0.698958337f, -0.07624988f), new float3(-0.169445336f, -0.698958337f, 0.07625011f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 1, 4, 7, 2, -1, 6, 3, 2, 7, -1, 5, 0, 3, 6, -1, 0, 5, 4, 1, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(1f, 0f, 0f, -0.135554656f),
					new float4(-1f, 0f, 0f, -0.169445336f),
					new float4(0f, 0.707106769f, 0.707106769f, -0.0718891546f),
					new float4(0f, -0.707106769f, -0.707106769f, -0.08986153f),
					new float4(0f, 0.8944272f, -0.4472136f, -0.2841669f),
					new float4(0f, -0.8944272f, 0.4472136f, -0.227333635f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(37.64632f, -2.60944438f, 4.490278f, 1f)), name: "Solid 716930 #4540")
					.WithMesh(new float3[] { new float3(0.135554656f, -0.13555561f, 0.237222239f), new float3(0.135554656f, -0.211805627f, 0.08472223f), new float3(0.135554656f, 0.169444382f, -0.296527773f), new float3(0.135554656f, 0.245694384f, -0.144027755f), new float3(-0.169445336f, -0.211805627f, 0.08472223f), new float3(-0.169445336f, -0.13555561f, 0.237222239f), new float3(-0.169445336f, 0.245694384f, -0.144027755f), new float3(-0.169445336f, 0.169444382f, -0.296527773f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 5, 0, 3, 6, -1, 1, 4, 7, 2, -1, 2, 7, 6, 3, -1, 4, 1, 0, 5, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(1f, 0f, 0f, -0.1355545f),
					new float4(-1f, 0f, 0f, -0.1694455f),
					new float4(0f, 0f, 1f, -0.07625011f),
					new float4(0f, -1f, 0f, -0.698958337f),
					new float4(0f, 0f, -1f, -0.07624988f),
					new float4(0f, 0.8944272f, -0.4472136f, -0.9775343f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(33.98632f, -3.87604165f, 4.65125f, 1f)), name: "Solid 716971 #4579")
					.WithMesh(new float3[] { new float3(0.1355545f, 1.05479169f, -0.07624988f), new float3(0.1355545f, 1.13104165f, 0.07625011f), new float3(0.1355545f, -0.698958337f, 0.07625011f), new float3(0.1355545f, -0.698958337f, -0.07624988f), new float3(-0.1694455f, 1.13104165f, 0.07625011f), new float3(-0.1694455f, 1.05479169f, -0.07624988f), new float3(-0.1694455f, -0.698958337f, -0.07624988f), new float3(-0.1694455f, -0.698958337f, 0.07625011f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 1, 4, 7, 2, -1, 6, 3, 2, 7, -1, 5, 0, 3, 6, -1, 0, 5, 4, 1, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(1f, 0f, 0f, -0.1355545f),
					new float4(-1f, 0f, 0f, -0.1694455f),
					new float4(0f, 0.707106769f, 0.707106769f, -0.0718891546f),
					new float4(0f, -0.707106769f, -0.707106769f, -0.08986153f),
					new float4(0f, 0.8944272f, -0.4472136f, -0.2841669f),
					new float4(0f, -0.8944272f, 0.4472136f, -0.227333635f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(33.98632f, -2.60944438f, 4.490278f, 1f)), name: "Solid 716972 #4580")
					.WithMesh(new float3[] { new float3(0.1355545f, -0.13555561f, 0.237222239f), new float3(0.1355545f, -0.211805627f, 0.08472223f), new float3(0.1355545f, 0.169444382f, -0.296527773f), new float3(0.1355545f, 0.245694384f, -0.144027755f), new float3(-0.1694455f, -0.211805627f, 0.08472223f), new float3(-0.1694455f, -0.13555561f, 0.237222239f), new float3(-0.1694455f, 0.245694384f, -0.144027755f), new float3(-0.1694455f, 0.169444382f, -0.296527773f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 5, 0, 3, 6, -1, 1, 4, 7, 2, -1, 2, 7, 6, 3, -1, 4, 1, 0, 5, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 0.8944272f, 0.4472136f, -0.16671142f),
					new float4(0f, -0.8944272f, 0.4472136f, -0.206494644f),
					new float4(0f, 0f, 1f, -0.0169445612f),
					new float4(1f, 0f, 0f, -6.40500164f),
					new float4(-1f, 0f, 0f, -6.4049983f),
					new float4(0f, 0f, -1f, -0.02118044f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(43.2718735f, -3.01822925f, 4.59618044f, 1f)), name: "Solid 716997 #4604")
					.WithMesh(new float3[] { new float3(6.40500164f, 0.196979254f, -0.02118044f), new float3(-6.4049983f, 0.196979254f, -0.02118044f), new float3(-6.4049983f, 0.17791675f, 0.0169445612f), new float3(6.40500164f, 0.17791675f, 0.0169445612f), new float3(-6.4049983f, -0.241458252f, -0.02118044f), new float3(6.40500164f, -0.241458252f, -0.02118044f), new float3(6.40500164f, -0.222395748f, 0.0169445612f), new float3(-6.4049983f, -0.222395748f, 0.0169445612f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 2, 7, 6, 3, -1, 5, 0, 3, 6, -1, 1, 4, 7, 2, -1, 4, 1, 0, 5, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 0.8944272f, 0.4472136f, -0.18660298f),
					new float4(-1f, 0f, 0f, -2.168889f),
					new float4(0f, -0.8944272f, 0.4472136f, -0.186603084f),
					new float4(0f, 0f, 1f, -0.0169445612f),
					new float4(1f, 0f, 0f, -2.711111f),
					new float4(0f, 0f, -1f, -0.02118044f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(32.630764f, -3.04046869f, 4.59618044f, 1f)), name: "Solid 716998 #4605")
					.WithMesh(new float3[] { new float3(2.711111f, 0.2192187f, -0.02118044f), new float3(-2.168889f, 0.2192187f, -0.02118044f), new float3(-2.168889f, 0.2001562f, 0.0169445612f), new float3(2.711111f, 0.2001562f, 0.0169445612f), new float3(-2.168889f, -0.21921882f, -0.02118044f), new float3(-2.168889f, -0.200156316f, 0.0169445612f), new float3(2.711111f, -0.21921882f, -0.02118044f), new float3(2.711111f, -0.200156316f, 0.0169445612f) }, new int[] { 0, 1, 2, 3, -1, 1, 4, 5, 2, -1, 4, 6, 7, 5, -1, 7, 3, 2, 5, -1, 6, 0, 3, 7, -1, 0, 6, 4, 1, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, -1f, 0f, -1.14375007f),
					new float4(-1f, 0f, 0f, -11.1107149f),
					new float4(0f, 0f, 1f, -0.152499869f),
					new float4(1f, 0f, 0f, -14.8142853f),
					new float4(0f, 0.707106769f, 0.707106769f, -0.7548363f),
					new float4(0f, 0f, -1f, -0.228750139f),
					new float4(0f, 1f, 0f, -1.14375f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(41.26759f, -3.73625f, 4.4225f, 1f)), name: "Solid 1189294 #4615")
					.WithMesh(new float3[] { new float3(-11.1107149f, -1.14375007f, -0.228750139f), new float3(14.8142853f, -1.14375007f, -0.228750139f), new float3(14.8142853f, -1.14375007f, 0.152499869f), new float3(-11.1107149f, -1.14375007f, 0.152499869f), new float3(-11.1107149f, 1.14375f, -0.07625017f), new float3(-11.1107149f, 1.14375f, -0.228750139f), new float3(-11.1107149f, 0.9149999f, 0.152499869f), new float3(14.8142853f, 0.9149999f, 0.152499869f), new float3(14.8142853f, 1.14375f, -0.228750139f), new float3(14.8142853f, 1.14375f, -0.07625017f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 0, 3, 6, -1, 7, 6, 3, 2, -1, 8, 9, 7, 2, 1, -1, 9, 4, 6, 7, -1, 5, 8, 1, 0, -1, 8, 5, 4, 9, -1 }))
				;
		}

		// Near (50.439, -2.707, 4.461): the exact CSG makes a triangle the mesh does not have
		static ContentsScene DetailsSite69()
		{
			return new ContentsScene().WithContentsList(kMapContents)
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(1f, 0f, 0f, -0.135555118f),
					new float4(-1f, 0f, 0f, -0.169444889f),
					new float4(0f, 0f, 1f, -0.07625011f),
					new float4(0f, -1f, 0f, -0.698958337f),
					new float4(0f, 0f, -1f, -0.07624988f),
					new float4(0f, 0.8944272f, -0.4472136f, -0.9775343f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(48.62632f, -3.87604165f, 4.65125f, 1f)), name: "Solid 716692 #4434")
					.WithMesh(new float3[] { new float3(0.135555118f, 1.05479169f, -0.07624988f), new float3(0.135555118f, 1.13104165f, 0.07625011f), new float3(0.135555118f, -0.698958337f, 0.07625011f), new float3(0.135555118f, -0.698958337f, -0.07624988f), new float3(-0.169444889f, 1.13104165f, 0.07625011f), new float3(-0.169444889f, 1.05479169f, -0.07624988f), new float3(-0.169444889f, -0.698958337f, -0.07624988f), new float3(-0.169444889f, -0.698958337f, 0.07625011f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 1, 4, 7, 2, -1, 6, 3, 2, 7, -1, 5, 0, 3, 6, -1, 0, 5, 4, 1, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(1f, 0f, 0f, -0.135555118f),
					new float4(-1f, 0f, 0f, -0.169444889f),
					new float4(0f, 0.707106769f, 0.707106769f, -0.0718891546f),
					new float4(0f, -0.707106769f, -0.707106769f, -0.08986153f),
					new float4(0f, 0.8944272f, -0.4472136f, -0.2841669f),
					new float4(0f, -0.8944272f, 0.4472136f, -0.227333635f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(48.62632f, -2.60944438f, 4.490278f, 1f)), name: "Solid 716694 #4436")
					.WithMesh(new float3[] { new float3(0.135555118f, -0.13555561f, 0.237222239f), new float3(0.135555118f, -0.211805627f, 0.08472223f), new float3(0.135555118f, 0.169444382f, -0.296527773f), new float3(0.135555118f, 0.245694384f, -0.144027755f), new float3(-0.169444889f, -0.211805627f, 0.08472223f), new float3(-0.169444889f, -0.13555561f, 0.237222239f), new float3(-0.169444889f, 0.245694384f, -0.144027755f), new float3(-0.169444889f, 0.169444382f, -0.296527773f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 5, 0, 3, 6, -1, 1, 4, 7, 2, -1, 2, 7, 6, 3, -1, 4, 1, 0, 5, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(1f, 0f, 0f, -0.135555267f),
					new float4(-1f, 0f, 0f, -0.16944474f),
					new float4(0f, 0f, 1f, -0.07625011f),
					new float4(0f, -1f, 0f, -0.698958337f),
					new float4(0f, 0f, -1f, -0.07624988f),
					new float4(0f, 0.8944272f, -0.4472136f, -0.9775343f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(52.28632f, -3.87604165f, 4.65125f, 1f)), name: "Solid 716860 #4473")
					.WithMesh(new float3[] { new float3(0.135555267f, 1.05479169f, -0.07624988f), new float3(0.135555267f, 1.13104165f, 0.07625011f), new float3(0.135555267f, -0.698958337f, 0.07625011f), new float3(0.135555267f, -0.698958337f, -0.07624988f), new float3(-0.16944474f, 1.13104165f, 0.07625011f), new float3(-0.16944474f, 1.05479169f, -0.07624988f), new float3(-0.16944474f, -0.698958337f, -0.07624988f), new float3(-0.16944474f, -0.698958337f, 0.07625011f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 1, 4, 7, 2, -1, 6, 3, 2, 7, -1, 5, 0, 3, 6, -1, 0, 5, 4, 1, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(1f, 0f, 0f, -0.135555267f),
					new float4(-1f, 0f, 0f, -0.16944474f),
					new float4(0f, 0.707106769f, 0.707106769f, -0.0718891546f),
					new float4(0f, -0.707106769f, -0.707106769f, -0.08986153f),
					new float4(0f, 0.8944272f, -0.4472136f, -0.2841669f),
					new float4(0f, -0.8944272f, 0.4472136f, -0.227333635f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(52.28632f, -2.60944438f, 4.490278f, 1f)), name: "Solid 716861 #4474")
					.WithMesh(new float3[] { new float3(0.135555267f, -0.13555561f, 0.237222239f), new float3(0.135555267f, -0.211805627f, 0.08472223f), new float3(0.135555267f, 0.169444382f, -0.296527773f), new float3(0.135555267f, 0.245694384f, -0.144027755f), new float3(-0.16944474f, -0.211805627f, 0.08472223f), new float3(-0.16944474f, -0.13555561f, 0.237222239f), new float3(-0.16944474f, 0.245694384f, -0.144027755f), new float3(-0.16944474f, 0.169444382f, -0.296527773f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 5, 0, 3, 6, -1, 1, 4, 7, 2, -1, 2, 7, 6, 3, -1, 4, 1, 0, 5, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 0.8944272f, 0.4472136f, -0.16671142f),
					new float4(0f, -0.8944272f, 0.4472136f, -0.206494644f),
					new float4(0f, 0f, 1f, -0.0169445612f),
					new float4(1f, 0f, 0f, -6.40500164f),
					new float4(-1f, 0f, 0f, -6.4049983f),
					new float4(0f, 0f, -1f, -0.02118044f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(43.2718735f, -3.01822925f, 4.59618044f, 1f)), name: "Solid 716997 #4604")
					.WithMesh(new float3[] { new float3(6.40500164f, 0.196979254f, -0.02118044f), new float3(-6.4049983f, 0.196979254f, -0.02118044f), new float3(-6.4049983f, 0.17791675f, 0.0169445612f), new float3(6.40500164f, 0.17791675f, 0.0169445612f), new float3(-6.4049983f, -0.241458252f, -0.02118044f), new float3(6.40500164f, -0.241458252f, -0.02118044f), new float3(6.40500164f, -0.222395748f, 0.0169445612f), new float3(-6.4049983f, -0.222395748f, 0.0169445612f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 2, 7, 6, 3, -1, 5, 0, 3, 6, -1, 1, 4, 7, 2, -1, 4, 1, 0, 5, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 0.8944272f, 0.4472136f, -0.16671142f),
					new float4(0f, -0.8944272f, 0.4472136f, -0.206494644f),
					new float4(0f, 0f, 1f, -0.0169445612f),
					new float4(1f, 0f, 0f, -2.287499f),
					new float4(-1f, 0f, 0f, -2.287501f),
					new float4(0f, 0f, -1f, -0.02118044f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(53.4893761f, -3.01822925f, 4.59618044f, 1f)), name: "Solid 716999 #4606")
					.WithMesh(new float3[] { new float3(2.287499f, 0.196979254f, -0.02118044f), new float3(-2.287501f, 0.196979254f, -0.02118044f), new float3(-2.287501f, 0.17791675f, 0.0169445612f), new float3(2.287499f, 0.17791675f, 0.0169445612f), new float3(-2.287501f, -0.241458252f, -0.02118044f), new float3(2.287499f, -0.241458252f, -0.02118044f), new float3(2.287499f, -0.222395748f, 0.0169445612f), new float3(-2.287501f, -0.222395748f, 0.0169445612f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 2, 7, 6, 3, -1, 5, 0, 3, 6, -1, 1, 4, 7, 2, -1, 4, 1, 0, 5, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, -1f, 0f, -1.14375007f),
					new float4(-1f, 0f, 0f, -11.1107149f),
					new float4(0f, 0f, 1f, -0.152499869f),
					new float4(1f, 0f, 0f, -14.8142853f),
					new float4(0f, 0.707106769f, 0.707106769f, -0.7548363f),
					new float4(0f, 0f, -1f, -0.228750139f),
					new float4(0f, 1f, 0f, -1.14375f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(41.26759f, -3.73625f, 4.4225f, 1f)), name: "Solid 1189294 #4615")
					.WithMesh(new float3[] { new float3(-11.1107149f, -1.14375007f, -0.228750139f), new float3(14.8142853f, -1.14375007f, -0.228750139f), new float3(14.8142853f, -1.14375007f, 0.152499869f), new float3(-11.1107149f, -1.14375007f, 0.152499869f), new float3(-11.1107149f, 1.14375f, -0.07625017f), new float3(-11.1107149f, 1.14375f, -0.228750139f), new float3(-11.1107149f, 0.9149999f, 0.152499869f), new float3(14.8142853f, 0.9149999f, 0.152499869f), new float3(14.8142853f, 1.14375f, -0.228750139f), new float3(14.8142853f, 1.14375f, -0.07625017f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 0, 3, 6, -1, 7, 6, 3, 2, -1, 8, 9, 7, 2, 1, -1, 9, 4, 6, 7, -1, 5, 8, 1, 0, -1, 8, 5, 4, 9, -1 }))
				;
		}

		[Test] public void TheModelsSite1_TheMeshIsWhatTheExactCSGMade() => ExactJudgeCaseTests.AssertTheMeshIsWhatTheExactCSGMade(ModelSite1());
		[Test] public void TheModelsSite2_TheMeshIsWhatTheExactCSGMade() => ExactJudgeCaseTests.AssertTheMeshIsWhatTheExactCSGMade(ModelSite2());
		[Test] public void TheModelsSite3_TheMeshIsWhatTheExactCSGMade() => ExactJudgeCaseTests.AssertTheMeshIsWhatTheExactCSGMade(ModelSite3());
		[Test] public void TheDetailsSite1_TheMeshIsWhatTheExactCSGMade() => ExactJudgeCaseTests.AssertTheMeshIsWhatTheExactCSGMade(DetailsSite1());
		[Test] public void TheDetailsSite2_TheMeshIsWhatTheExactCSGMade() => ExactJudgeCaseTests.AssertTheMeshIsWhatTheExactCSGMade(DetailsSite2());
		[Test] public void TheDetailsSite3_TheMeshIsWhatTheExactCSGMade() => ExactJudgeCaseTests.AssertTheMeshIsWhatTheExactCSGMade(DetailsSite3());
		[Test] public void TheDetailsSite4_TheMeshIsWhatTheExactCSGMade() => ExactJudgeCaseTests.AssertTheMeshIsWhatTheExactCSGMade(DetailsSite4());
		[Test] public void TheDetailsSite5_TheMeshIsWhatTheExactCSGMade() => ExactJudgeCaseTests.AssertTheMeshIsWhatTheExactCSGMade(DetailsSite5());
		[Test] public void TheDetailsSite6_TheMeshIsWhatTheExactCSGMade() => ExactJudgeCaseTests.AssertTheMeshIsWhatTheExactCSGMade(DetailsSite6());
		[Test] public void TheDetailsSite7_TheMeshIsWhatTheExactCSGMade() => ExactJudgeCaseTests.AssertTheMeshIsWhatTheExactCSGMade(DetailsSite7());
		[Test] public void TheDetailsSite8_TheMeshIsWhatTheExactCSGMade() => ExactJudgeCaseTests.AssertTheMeshIsWhatTheExactCSGMade(DetailsSite8());
		[Test] public void TheDetailsSite9_TheMeshIsWhatTheExactCSGMade() => ExactJudgeCaseTests.AssertTheMeshIsWhatTheExactCSGMade(DetailsSite9());
		[Test] public void TheDetailsSite10_TheMeshIsWhatTheExactCSGMade() => ExactJudgeCaseTests.AssertTheMeshIsWhatTheExactCSGMade(DetailsSite10());
		[Test] public void TheDetailsSite11_TheMeshIsWhatTheExactCSGMade() => ExactJudgeCaseTests.AssertTheMeshIsWhatTheExactCSGMade(DetailsSite11());
		[Test] public void TheDetailsSite12_TheMeshIsWhatTheExactCSGMade() => ExactJudgeCaseTests.AssertTheMeshIsWhatTheExactCSGMade(DetailsSite12());
		[Test] public void TheDetailsSite13_TheMeshIsWhatTheExactCSGMade() => ExactJudgeCaseTests.AssertTheMeshIsWhatTheExactCSGMade(DetailsSite13());
		[Test] public void TheDetailsSite14_TheMeshIsWhatTheExactCSGMade() => ExactJudgeCaseTests.AssertTheMeshIsWhatTheExactCSGMade(DetailsSite14());
		[Test] public void TheDetailsSite15_TheMeshIsWhatTheExactCSGMade() => ExactJudgeCaseTests.AssertTheMeshIsWhatTheExactCSGMade(DetailsSite15());
		[Test] public void TheDetailsSite16_TheMeshIsWhatTheExactCSGMade() => ExactJudgeCaseTests.AssertTheMeshIsWhatTheExactCSGMade(DetailsSite16());
		[Test] public void TheDetailsSite17_TheMeshIsWhatTheExactCSGMade() => ExactJudgeCaseTests.AssertTheMeshIsWhatTheExactCSGMade(DetailsSite17());
		[Test] public void TheDetailsSite18_TheMeshIsWhatTheExactCSGMade() => ExactJudgeCaseTests.AssertTheMeshIsWhatTheExactCSGMade(DetailsSite18());
		[Test] public void TheDetailsSite19_TheMeshIsWhatTheExactCSGMade() => ExactJudgeCaseTests.AssertTheMeshIsWhatTheExactCSGMade(DetailsSite19());
		[Test] public void TheDetailsSite20_TheMeshIsWhatTheExactCSGMade() => ExactJudgeCaseTests.AssertTheMeshIsWhatTheExactCSGMade(DetailsSite20());
		[Test] public void TheDetailsSite21_TheMeshIsWhatTheExactCSGMade() => ExactJudgeCaseTests.AssertTheMeshIsWhatTheExactCSGMade(DetailsSite21());
		[Test] public void TheDetailsSite22_TheMeshIsWhatTheExactCSGMade() => ExactJudgeCaseTests.AssertTheMeshIsWhatTheExactCSGMade(DetailsSite22());
		[Test] public void TheDetailsSite23_TheMeshIsWhatTheExactCSGMade() => ExactJudgeCaseTests.AssertTheMeshIsWhatTheExactCSGMade(DetailsSite23());
		[Test] public void TheDetailsSite24_TheMeshIsWhatTheExactCSGMade() => ExactJudgeCaseTests.AssertTheMeshIsWhatTheExactCSGMade(DetailsSite24());
		[Test] public void TheDetailsSite25_TheMeshIsWhatTheExactCSGMade() => ExactJudgeCaseTests.AssertTheMeshIsWhatTheExactCSGMade(DetailsSite25());
		[Test] public void TheDetailsSite26_TheMeshIsWhatTheExactCSGMade() => ExactJudgeCaseTests.AssertTheMeshIsWhatTheExactCSGMade(DetailsSite26());
		[Test] public void TheDetailsSite27_TheMeshIsWhatTheExactCSGMade() => ExactJudgeCaseTests.AssertTheMeshIsWhatTheExactCSGMade(DetailsSite27());
		[Test] public void TheDetailsSite28_TheMeshIsWhatTheExactCSGMade() => ExactJudgeCaseTests.AssertTheMeshIsWhatTheExactCSGMade(DetailsSite28());
		[Test] public void TheDetailsSite29_TheMeshIsWhatTheExactCSGMade() => ExactJudgeCaseTests.AssertTheMeshIsWhatTheExactCSGMade(DetailsSite29());
		[Test] public void TheDetailsSite30_TheMeshIsWhatTheExactCSGMade() => ExactJudgeCaseTests.AssertTheMeshIsWhatTheExactCSGMade(DetailsSite30());
		[Test] public void TheDetailsSite31_TheMeshIsWhatTheExactCSGMade() => ExactJudgeCaseTests.AssertTheMeshIsWhatTheExactCSGMade(DetailsSite31());
		[Test] public void TheDetailsSite32_TheMeshIsWhatTheExactCSGMade() => ExactJudgeCaseTests.AssertTheMeshIsWhatTheExactCSGMade(DetailsSite32());
		[Test] public void TheDetailsSite33_TheMeshIsWhatTheExactCSGMade() => ExactJudgeCaseTests.AssertTheMeshIsWhatTheExactCSGMade(DetailsSite33());
		[Test] public void TheDetailsSite34_TheMeshIsWhatTheExactCSGMade() => ExactJudgeCaseTests.AssertTheMeshIsWhatTheExactCSGMade(DetailsSite34());
		[Test] public void TheDetailsSite35_TheMeshIsWhatTheExactCSGMade() => ExactJudgeCaseTests.AssertTheMeshIsWhatTheExactCSGMade(DetailsSite35());
		[Test] public void TheDetailsSite36_TheMeshIsWhatTheExactCSGMade() => ExactJudgeCaseTests.AssertTheMeshIsWhatTheExactCSGMade(DetailsSite36());
		[Test] public void TheDetailsSite37_TheMeshIsWhatTheExactCSGMade() => ExactJudgeCaseTests.AssertTheMeshIsWhatTheExactCSGMade(DetailsSite37());
		[Test] public void TheDetailsSite38_TheMeshIsWhatTheExactCSGMade() => ExactJudgeCaseTests.AssertTheMeshIsWhatTheExactCSGMade(DetailsSite38());
		[Test] public void TheDetailsSite39_TheMeshIsWhatTheExactCSGMade() => ExactJudgeCaseTests.AssertTheMeshIsWhatTheExactCSGMade(DetailsSite39());
		[Test] public void TheDetailsSite40_TheMeshIsWhatTheExactCSGMade() => ExactJudgeCaseTests.AssertTheMeshIsWhatTheExactCSGMade(DetailsSite40());
		[Test] public void TheDetailsSite41_TheMeshIsWhatTheExactCSGMade() => ExactJudgeCaseTests.AssertTheMeshIsWhatTheExactCSGMade(DetailsSite41());
		[Test] public void TheDetailsSite42_TheMeshIsWhatTheExactCSGMade() => ExactJudgeCaseTests.AssertTheMeshIsWhatTheExactCSGMade(DetailsSite42());
		[Test] public void TheDetailsSite43_TheMeshIsWhatTheExactCSGMade() => ExactJudgeCaseTests.AssertTheMeshIsWhatTheExactCSGMade(DetailsSite43());
		[Test] public void TheDetailsSite44_TheMeshIsWhatTheExactCSGMade() => ExactJudgeCaseTests.AssertTheMeshIsWhatTheExactCSGMade(DetailsSite44());
		[Test] public void TheDetailsSite45_TheMeshIsWhatTheExactCSGMade() => ExactJudgeCaseTests.AssertTheMeshIsWhatTheExactCSGMade(DetailsSite45());
		[Test] public void TheDetailsSite46_TheMeshIsWhatTheExactCSGMade() => ExactJudgeCaseTests.AssertTheMeshIsWhatTheExactCSGMade(DetailsSite46());
		[Test] public void TheDetailsSite47_TheMeshIsWhatTheExactCSGMade() => ExactJudgeCaseTests.AssertTheMeshIsWhatTheExactCSGMade(DetailsSite47());
		[Test] public void TheDetailsSite48_TheMeshIsWhatTheExactCSGMade() => ExactJudgeCaseTests.AssertTheMeshIsWhatTheExactCSGMade(DetailsSite48());
		[Test] public void TheDetailsSite49_TheMeshIsWhatTheExactCSGMade() => ExactJudgeCaseTests.AssertTheMeshIsWhatTheExactCSGMade(DetailsSite49());
		[Test] public void TheDetailsSite50_TheMeshIsWhatTheExactCSGMade() => ExactJudgeCaseTests.AssertTheMeshIsWhatTheExactCSGMade(DetailsSite50());
		[Test] public void TheDetailsSite51_TheMeshIsWhatTheExactCSGMade() => ExactJudgeCaseTests.AssertTheMeshIsWhatTheExactCSGMade(DetailsSite51());
		[Test] public void TheDetailsSite52_TheMeshIsWhatTheExactCSGMade() => ExactJudgeCaseTests.AssertTheMeshIsWhatTheExactCSGMade(DetailsSite52());
		[Test] public void TheDetailsSite53_TheMeshIsWhatTheExactCSGMade() => ExactJudgeCaseTests.AssertTheMeshIsWhatTheExactCSGMade(DetailsSite53());
		[Test] public void TheDetailsSite54_TheMeshIsWhatTheExactCSGMade() => ExactJudgeCaseTests.AssertTheMeshIsWhatTheExactCSGMade(DetailsSite54());
		[Test] public void TheDetailsSite55_TheMeshIsWhatTheExactCSGMade() => ExactJudgeCaseTests.AssertTheMeshIsWhatTheExactCSGMade(DetailsSite55());
		[Test] public void TheDetailsSite56_TheMeshIsWhatTheExactCSGMade() => ExactJudgeCaseTests.AssertTheMeshIsWhatTheExactCSGMade(DetailsSite56());
		[Test] public void TheDetailsSite57_TheMeshIsWhatTheExactCSGMade() => ExactJudgeCaseTests.AssertTheMeshIsWhatTheExactCSGMade(DetailsSite57());
		[Test] public void TheDetailsSite58_TheMeshIsWhatTheExactCSGMade() => ExactJudgeCaseTests.AssertTheMeshIsWhatTheExactCSGMade(DetailsSite58());
		[Test] public void TheDetailsSite59_TheMeshIsWhatTheExactCSGMade() => ExactJudgeCaseTests.AssertTheMeshIsWhatTheExactCSGMade(DetailsSite59());
		[Test] public void TheDetailsSite60_TheMeshIsWhatTheExactCSGMade() => ExactJudgeCaseTests.AssertTheMeshIsWhatTheExactCSGMade(DetailsSite60());
		[Test] public void TheDetailsSite61_TheMeshIsWhatTheExactCSGMade() => ExactJudgeCaseTests.AssertTheMeshIsWhatTheExactCSGMade(DetailsSite61());
		[Test] public void TheDetailsSite62_TheMeshIsWhatTheExactCSGMade() => ExactJudgeCaseTests.AssertTheMeshIsWhatTheExactCSGMade(DetailsSite62());
		[Test] public void TheDetailsSite63_TheMeshIsWhatTheExactCSGMade() => ExactJudgeCaseTests.AssertTheMeshIsWhatTheExactCSGMade(DetailsSite63());
		[Test] public void TheDetailsSite64_TheMeshIsWhatTheExactCSGMade() => ExactJudgeCaseTests.AssertTheMeshIsWhatTheExactCSGMade(DetailsSite64());
		[Test] public void TheDetailsSite65_TheMeshIsWhatTheExactCSGMade() => ExactJudgeCaseTests.AssertTheMeshIsWhatTheExactCSGMade(DetailsSite65());
		[Test] public void TheDetailsSite66_TheMeshIsWhatTheExactCSGMade() => ExactJudgeCaseTests.AssertTheMeshIsWhatTheExactCSGMade(DetailsSite66());
		[Test] public void TheDetailsSite67_TheMeshIsWhatTheExactCSGMade() => ExactJudgeCaseTests.AssertTheMeshIsWhatTheExactCSGMade(DetailsSite67());
		[Test] public void TheDetailsSite68_TheMeshIsWhatTheExactCSGMade() => ExactJudgeCaseTests.AssertTheMeshIsWhatTheExactCSGMade(DetailsSite68());
		[Test] public void TheDetailsSite69_TheMeshIsWhatTheExactCSGMade() => ExactJudgeCaseTests.AssertTheMeshIsWhatTheExactCSGMade(DetailsSite69());
	}
}
