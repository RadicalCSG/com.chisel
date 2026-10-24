using System.Linq;
using NUnit.Framework;
using Unity.Mathematics;

namespace Chisel.Core.Tests
{
	// Cases where the exact judge disagreed with the mesh, each shrunk to the brushes that still show it
	[TestFixture]
	[Category("Contents")]
	public class ExactJudgeCaseTests
	{
		// A long needle on the slabs' underside (y -0.915) that the exact CSG doesn't make
		static ContentsScene SlabsUnderside()
		{
			return new ContentsScene()
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(-0.707092941f, -1.85503231E-05f, 0.7071206f, -2.13823056f),
					new float4(0.8660391f, 2.87726834E-05f, -0.4999763f, -3.34780431f),
					new float4(-0.153352857f, -0.967751443f, -0.199850127f, -0.663077056f),
					new float4(0.303984284f, 0.8664019f, 0.396158129f, -0.8297414f),
					new float4(-0.562849164f, 0.380996168f, -0.733513951f, -0.301146239f),
					new float4(0.561914563f, -0.384699732f, 0.732296467f, -0.301130861f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(35.52286f, -1.53559029f, -7.76104164f, 1f)), name: "brush 1")
					.WithMesh(new float3[] { new float3(-1.68482745f, -0.694722235f, 1.3390758f), new float3(-1.29386628f, 0.62059027f, 1.73005617f), new float3(-1.67129755f, 0.9255834f, 1.35264766f), new float3(-2.10266876f, -0.542225659f, 0.9212548f), new float3(2.97385287f, 0.6205925f, -1.54470038f), new float3(2.69743443f, -0.694728136f, -2.02357721f), new float3(2.40199232f, -0.5422313f, -2.53532147f), new float3(2.70698142f, 0.9255865f, -2.0069468f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 5, 0, 3, 6, -1, 1, 4, 7, 2, -1, 2, 7, 6, 3, -1, 4, 1, 0, 5, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(-0.8659878f, -6.975805E-05f, 0.5000652f, -2.13821f),
					new float4(0.9659447f, -5.04837635E-06f, -0.258748651f, -3.347808f),
					new float4(-0.09641658f, -0.9677414f, -0.232767045f, -0.663069665f),
					new float4(0.191077828f, 0.8664214f, 0.461305976f, -0.82972765f),
					new float4(-0.353829622f, 0.3809212f, -0.854226947f, -0.301125467f),
					new float4(0.353239059f, -0.3846927f, 0.85278f, -0.301110864f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(40.1942749f, -1.53559029f, -10.6419287f, 1f)), name: "brush 2")
					.WithMesh(new float3[] { new float3(-1.97396231f, -0.694722235f, 0.857356966f), new float3(-1.697556f, 0.6205903f, 1.33620691f), new float3(-1.96451163f, 0.9255823f, 0.8739491f), new float3(-2.26942229f, -0.542222261f, 0.3457154f), new float3(3.272328f, 0.620611668f, -0.722411931f), new float3(3.12925434f, -0.6947206f, -1.25649989f), new float3(2.976392f, -0.542232335f, -1.82715952f), new float3(3.134219f, 0.9255817f, -1.237998f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 5, 0, 3, 6, -1, 1, 4, 7, 2, -1, 2, 7, 6, 3, -1, 4, 1, 0, 5, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, -1f, 0f, -0.7625f),
					new float4(0.366494775f, 0f, 0.9304201f, -0.813386738f),
					new float4(0.2591407f, -0.7071409f, 0.6578737f, -0.251601249f),
					new float4(-0.259165049f, 0.707115948f, -0.657891f, -0.395412f),
					new float4(0.9696436f, 0f, -0.2445226f, -2.928341f),
					new float4(-0.8521493f, -0.000409213768f, 0.523298562f, -2.2675972f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(40.9734764f, -0.1525f, -9.525695f, 1f)), name: "brush 3")
					.WithMesh(new float3[] { new float3(-2.35859f, -0.7625f, 0.4919074f), new float3(-2.84483671f, -0.7625f, -0.299906284f), new float3(2.421249f, -0.7625f, -2.37439179f), new float3(2.646853f, -0.7625f, -1.47976887f), new float3(-1.71097183f, 1.37249815f, 1.54817057f), new float3(-1.710618f, 0.457499981f, 1.54803109f), new float3(2.94767284f, 0.457517326f, -0.2868811f), new float3(2.94767284f, 1.372631f, -0.2868811f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 5, 0, 3, 6, -1, 1, 4, 7, 2, -1, 2, 7, 6, 3, -1, 4, 1, 0, 5, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, -1f, 0f, -0.7625f),
					new float4(0.608749f, 0f, 0.7933629f, -0.8133965f),
					new float4(0.430435836f, -0.707134247f, 0.5609689f, -0.251628131f),
					new float4(-0.430420876f, 0.7071393f, -0.560974f, -0.3953991f),
					new float4(-0.6300544f, 0.0002936061f, 0.776550949f, -1.86625f),
					new float4(0.8521952f, 1.064502E-05f, -0.523224f, -2.46816039f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(36.86753f, -0.1525f, -7.162032f, 1f)), name: "brush 4")
					.WithMesh(new float3[] { new float3(1.27345431f, -0.7625f, -2.6431067f), new float3(1.75481343f, -0.7625f, -1.85909843f), new float3(-1.84724009f, -0.7625f, 0.904785633f), new float3(-2.57791686f, -0.7625f, 0.311951131f), new float3(2.39664435f, 0.457499981f, -0.813698649f), new float3(2.39663672f, 1.37243271f, -0.8136927f), new float3(-0.872570336f, 1.37250483f, 1.69477642f), new float3(-0.8727895f, 0.4574848f, 1.69494462f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 1, 4, 7, 2, -1, 5, 0, 3, 6, -1, 6, 3, 2, 7, -1, 0, 5, 4, 1, -1 }));
		}

		[Test]
		public void TheSlabsUnderside_TheMeshIsWhatTheExactCSGMade()
		{
			AssertTheMeshIsWhatTheExactCSGMade(SlabsUnderside());
		}

		static void AssertTheMeshIsWhatTheExactCSGMade(ContentsScene scene)
		{
			using var harness = ContentsTreeHarness.Build(scene);
			Assert.That(harness.Update(), Is.True, "the CSG update did not run");
			var mismatches = ExactJudge.FindMismatches(harness, 100, out var judged);
			Assert.That(judged, Is.GreaterThan(0), "the judge judged nothing");
			Assert.That(mismatches, Is.Empty, string.Join("; ", mismatches.Select(m => m.Format(""))));
		}

		// Needles on a face at x -51.24, a corner of which the collapse moves (1 of 2)
		static ContentsScene CollapsedCorner1()
		{
			return new ContentsScene()
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 1f, 0f, -1.49111128f),
					new float4(0f, -1f, 0f, -0.9488888f),
					new float4(1f, 0f, 0f, -0.08472214f),
					new float4(-1f, 0f, 0f, -0.0677778646f),
					new float4(0f, 0f, 1f, -0.3049998f),
					new float4(0f, 0f, -1f, -0.3050002f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(-51.1722221f, 4.761389f, -12.2f, 1f)), name: "brush 5")
					.WithMesh(new float3[] { new float3(0.08472214f, 1.49111128f, -0.3050002f), new float3(-0.0677778646f, 1.49111128f, -0.3050002f), new float3(-0.0677778646f, 1.49111128f, 0.3049998f), new float3(0.08472214f, 1.49111128f, 0.3049998f), new float3(-0.0677778646f, -0.9488888f, -0.3050002f), new float3(0.08472214f, -0.9488888f, -0.3050002f), new float3(0.08472214f, -0.9488888f, 0.3049998f), new float3(-0.0677778646f, -0.9488888f, 0.3049998f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 5, 0, 3, 6, -1, 1, 4, 7, 2, -1, 2, 7, 6, 3, -1, 4, 1, 0, 5, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 1f, 0f, -1.49111128f),
					new float4(0f, -1f, 0f, -0.9488888f),
					new float4(1f, 0f, 0f, -0.08472214f),
					new float4(-1f, 0f, 0f, -0.0677778646f),
					new float4(0f, 0f, 1f, -0.304999322f),
					new float4(0f, 0f, -1f, -0.3050007f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(-51.1722221f, 4.761389f, -31.72f, 1f)), name: "brush 6")
					.WithMesh(new float3[] { new float3(0.08472214f, 1.49111128f, -0.3050007f), new float3(-0.0677778646f, 1.49111128f, -0.3050007f), new float3(-0.0677778646f, 1.49111128f, 0.304999322f), new float3(0.08472214f, 1.49111128f, 0.304999322f), new float3(-0.0677778646f, -0.9488888f, -0.3050007f), new float3(0.08472214f, -0.9488888f, -0.3050007f), new float3(0.08472214f, -0.9488888f, 0.304999322f), new float3(-0.0677778646f, -0.9488888f, 0.304999322f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 5, 0, 3, 6, -1, 1, 4, 7, 2, -1, 2, 7, 6, 3, -1, 4, 1, 0, 5, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 1f, 0f, -1.49111128f),
					new float4(0f, -1f, 0f, -0.9488888f),
					new float4(1f, 0f, 0f, -0.08472214f),
					new float4(-1f, 0f, 0f, -0.0677778646f),
					new float4(0f, 0f, 1f, -0.30499953f),
					new float4(0f, 0f, -1f, -0.305000454f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(-51.1722221f, 4.761389f, -41.48f, 1f)), name: "brush 7")
					.WithMesh(new float3[] { new float3(0.08472214f, 1.49111128f, -0.305000454f), new float3(-0.0677778646f, 1.49111128f, -0.305000454f), new float3(-0.0677778646f, 1.49111128f, 0.30499953f), new float3(0.08472214f, 1.49111128f, 0.30499953f), new float3(-0.0677778646f, -0.9488888f, -0.305000454f), new float3(0.08472214f, -0.9488888f, -0.305000454f), new float3(0.08472214f, -0.9488888f, 0.30499953f), new float3(-0.0677778646f, -0.9488888f, 0.30499953f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 5, 0, 3, 6, -1, 1, 4, 7, 2, -1, 2, 7, 6, 3, -1, 4, 1, 0, 5, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 1f, 0f, -0.3408018f),
					new float4(-1f, 0f, 0f, -0.609998941f),
					new float4(1f, 0f, 0f, -0.6100011f),
					new float4(0.707106769f, -0.707106769f, 0f, -0.193048626f),
					new float4(0f, 0f, 1f, -16.2666664f),
					new float4(0f, 0f, -1f, -13.0133324f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(-50.63f, 7.135511f, -28.4666672f, 1f)), name: "brush 8")
					.WithMesh(new float3[] { new float3(0.6100011f, 0.3408018f, -13.0133324f), new float3(-0.609998941f, 0.3408018f, -13.0133324f), new float3(-0.609998941f, 0.3408018f, 16.2666664f), new float3(0.6100011f, 0.3408018f, 16.2666664f), new float3(-0.609998941f, -0.8830109f, -13.0133324f), new float3(-0.609998941f, -0.8830109f, 16.2666664f), new float3(0.6100011f, 0.3369891f, -13.0133324f), new float3(0.6100011f, 0.3369891f, 16.2666664f) }, new int[] { 0, 1, 2, 3, -1, 1, 4, 5, 2, -1, 6, 0, 3, 7, -1, 4, 6, 7, 5, -1, 5, 7, 3, 2, -1, 6, 4, 1, 0, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0.705407739f, -0.705407739f, 0.0692811161f, -0.215149924f),
					new float4(0f, 1f, 0f, -0.304999977f),
					new float4(0f, 0f, -1f, -0.533749759f),
					new float4(-0.9039021f, 0f, 0.427739382f, -0.275689542f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(-50.6824226f, 7.1675f, -11.66625f, 1f)), name: "brush 9")
					.WithMesh(new float3[] { new float3(0.452735126f, 0.304999977f, 1.60125029f), new float3(-0.5575773f, -0.914999962f, -0.533749759f), new float3(0.662422657f, 0.304999977f, -0.533749759f), new float3(-0.5575773f, 0.304999977f, -0.533749759f) }, new int[] { 0, 1, 2, -1, 3, 0, 2, -1, 1, 3, 2, -1, 3, 1, 0, -1 }));
		}

		[Test]
		public void ACollapsedCorner_1_TheMeshIsWhatTheExactCSGMade()
		{
			AssertTheMeshIsWhatTheExactCSGMade(CollapsedCorner1());
		}

		// Needles on a face at x -51.24, a corner of which the collapse moves (2 of 2)
		static ContentsScene CollapsedCorner2()
		{
			return new ContentsScene()
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 1f, 0f, -1.49111128f),
					new float4(0f, -1f, 0f, -0.9488888f),
					new float4(1f, 0f, 0f, -0.08472214f),
					new float4(-1f, 0f, 0f, -0.0677778646f),
					new float4(0f, 0f, 1f, -0.3049998f),
					new float4(0f, 0f, -1f, -0.3050002f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(-51.1722221f, 4.761389f, -12.2f, 1f)), name: "brush 5")
					.WithMesh(new float3[] { new float3(0.08472214f, 1.49111128f, -0.3050002f), new float3(-0.0677778646f, 1.49111128f, -0.3050002f), new float3(-0.0677778646f, 1.49111128f, 0.3049998f), new float3(0.08472214f, 1.49111128f, 0.3049998f), new float3(-0.0677778646f, -0.9488888f, -0.3050002f), new float3(0.08472214f, -0.9488888f, -0.3050002f), new float3(0.08472214f, -0.9488888f, 0.3049998f), new float3(-0.0677778646f, -0.9488888f, 0.3049998f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 5, 0, 3, 6, -1, 1, 4, 7, 2, -1, 2, 7, 6, 3, -1, 4, 1, 0, 5, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 1f, 0f, -1.49111128f),
					new float4(0f, -1f, 0f, -0.9488888f),
					new float4(1f, 0f, 0f, -0.08472214f),
					new float4(-1f, 0f, 0f, -0.0677778646f),
					new float4(0f, 0f, 1f, -0.304999083f),
					new float4(0f, 0f, -1f, -0.3050009f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(-51.1722221f, 4.761389f, -21.96f, 1f)), name: "brush 10")
					.WithMesh(new float3[] { new float3(0.08472214f, 1.49111128f, -0.3050009f), new float3(-0.0677778646f, 1.49111128f, -0.3050009f), new float3(-0.0677778646f, 1.49111128f, 0.304999083f), new float3(0.08472214f, 1.49111128f, 0.304999083f), new float3(-0.0677778646f, -0.9488888f, -0.3050009f), new float3(0.08472214f, -0.9488888f, -0.3050009f), new float3(0.08472214f, -0.9488888f, 0.304999083f), new float3(-0.0677778646f, -0.9488888f, 0.304999083f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 5, 0, 3, 6, -1, 1, 4, 7, 2, -1, 2, 7, 6, 3, -1, 4, 1, 0, 5, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 1f, 0f, -1.49111128f),
					new float4(0f, -1f, 0f, -0.9488888f),
					new float4(1f, 0f, 0f, -0.08472214f),
					new float4(-1f, 0f, 0f, -0.0677778646f),
					new float4(0f, 0f, 1f, -0.30499953f),
					new float4(0f, 0f, -1f, -0.305000454f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(-51.1722221f, 4.761389f, -41.48f, 1f)), name: "brush 7")
					.WithMesh(new float3[] { new float3(0.08472214f, 1.49111128f, -0.305000454f), new float3(-0.0677778646f, 1.49111128f, -0.305000454f), new float3(-0.0677778646f, 1.49111128f, 0.30499953f), new float3(0.08472214f, 1.49111128f, 0.30499953f), new float3(-0.0677778646f, -0.9488888f, -0.305000454f), new float3(0.08472214f, -0.9488888f, -0.305000454f), new float3(0.08472214f, -0.9488888f, 0.30499953f), new float3(-0.0677778646f, -0.9488888f, 0.30499953f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 5, 0, 3, 6, -1, 1, 4, 7, 2, -1, 2, 7, 6, 3, -1, 4, 1, 0, 5, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 1f, 0f, -0.3408018f),
					new float4(-1f, 0f, 0f, -0.609998941f),
					new float4(1f, 0f, 0f, -0.6100011f),
					new float4(0.707106769f, -0.707106769f, 0f, -0.193048626f),
					new float4(0f, 0f, 1f, -16.2666664f),
					new float4(0f, 0f, -1f, -13.0133324f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(-50.63f, 7.135511f, -28.4666672f, 1f)), name: "brush 8")
					.WithMesh(new float3[] { new float3(0.6100011f, 0.3408018f, -13.0133324f), new float3(-0.609998941f, 0.3408018f, -13.0133324f), new float3(-0.609998941f, 0.3408018f, 16.2666664f), new float3(0.6100011f, 0.3408018f, 16.2666664f), new float3(-0.609998941f, -0.8830109f, -13.0133324f), new float3(-0.609998941f, -0.8830109f, 16.2666664f), new float3(0.6100011f, 0.3369891f, -13.0133324f), new float3(0.6100011f, 0.3369891f, 16.2666664f) }, new int[] { 0, 1, 2, 3, -1, 1, 4, 5, 2, -1, 6, 0, 3, 7, -1, 4, 6, 7, 5, -1, 5, 7, 3, 2, -1, 6, 4, 1, 0, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0.705407739f, -0.705407739f, 0.0692811161f, -0.215149924f),
					new float4(0f, 1f, 0f, -0.304999977f),
					new float4(0f, 0f, -1f, -0.533749759f),
					new float4(-0.9039021f, 0f, 0.427739382f, -0.275689542f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(-50.6824226f, 7.1675f, -11.66625f, 1f)), name: "brush 9")
					.WithMesh(new float3[] { new float3(0.452735126f, 0.304999977f, 1.60125029f), new float3(-0.5575773f, -0.914999962f, -0.533749759f), new float3(0.662422657f, 0.304999977f, -0.533749759f), new float3(-0.5575773f, 0.304999977f, -0.533749759f) }, new int[] { 0, 1, 2, -1, 3, 0, 2, -1, 1, 3, 2, -1, 3, 1, 0, -1 }));
		}

		[Test]
		public void ACollapsedCorner_2_TheMeshIsWhatTheExactCSGMade()
		{
			AssertTheMeshIsWhatTheExactCSGMade(CollapsedCorner2());
		}
	}
}
