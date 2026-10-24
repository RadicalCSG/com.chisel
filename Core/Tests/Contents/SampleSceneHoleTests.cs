using NUnit.Framework;
using Unity.Mathematics;

namespace Chisel.Core.Tests
{
	[TestFixture]
	[Category("Contents")]
	[Category("CSGCorpus")]
	public class SampleSceneHoleTests
	{
		[Test]
		public void HoleOnBoxTop_WithSubBoxAndAddBox39_Model1F01()
		{
			var scene = new ContentsScene()
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(-1f, 0f, 0f, -13f),
					new float4(0f, -1f, 0f, -3f),
					new float4(0f, 0f, -1f, -13f),
					new float4(1f, 0f, 0f, -13f),
					new float4(0f, 1f, 0f, 0f),
					new float4(0f, 0f, 1f, -12f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(-1.00000048f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, -1.00000048f, 0f), new float4(0f, -6.25f, -2.98022229E-08f, 1f)), name: "Box #0"))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(-1f, 0f, 0f, -3f),
					new float4(0f, -1f, 0f, -1f),
					new float4(0f, 0f, -1f, -3f),
					new float4(1f, 0f, 0f, -3f),
					new float4(0f, 1f, 0f, 0f),
					new float4(0f, 0f, 1f, -3f),
				}, contents: 0, operation: CSGOperationType.Subtractive,
					localToTree: new float4x4(new float4(0f, 2.38418636E-07f, 1.00000024f, 0f), new float4(1.00000024f, 0f, 0f, 0f), new float4(0f, 1.00000024f, -2.38418636E-07f, 0f), new float4(-11.0000067f, -3.249999f, 1.99999976f, 1f)), name: "Box #142"))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(-1f, 0f, 0f, -1.75f),
					new float4(0f, -1f, 0f, 0f),
					new float4(0f, 0f, -1f, -0.25f),
					new float4(1f, 0f, 0f, -1.75f),
					new float4(0f, 1f, 0f, -0.5f),
					new float4(0f, 0f, 1f, -0.25f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(-1.00000048f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, -1.00000048f, 0f), new float4(-11.25f, -6.75f, 4.75f, 1f)), name: "Box (39) #145"))
				;
			ContentsHarvest.AssertMatchesOracle(scene, "sample scene Model1 finding 1");
		}

		[Test]
		public void HoleOnBoxNegZ_WithAddBox9AndAddBox_Model2F06()
		{
			var scene = new ContentsScene()
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(-1f, 0f, 0f, -0.5f),
					new float4(0f, -1f, 0f, 0f),
					new float4(0f, 0f, -1f, -5.75f),
					new float4(1f, 0f, 0f, -0.5f),
					new float4(0f, 1f, 0f, -15f),
					new float4(0f, 0f, 1f, -2.25f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(-1.00000048f, 0f, 5.617607E-07f, 0f), new float4(5.61760658E-07f, -1.192093E-07f, 1.00000024f, 0f), new float4(0f, 1.00000024f, -1.192093E-07f, 0f), new float4(18.5f, -3.24999762f, -33.0000038f, 1f)), name: "Box (9) #251"))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(-1f, 0f, 0f, -34f),
					new float4(0f, -1f, 0f, 0f),
					new float4(0f, 0f, -1f, -4f),
					new float4(1f, 0f, 0f, -3.5f),
					new float4(0f, 1f, 0f, -0.9999989f),
					new float4(0f, 0f, 1f, -17f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(-1.00000024f, 0f, 0f, 0f), new float4(0f, 3.57627925E-07f, 1f, 0f), new float4(0f, 1f, -3.57627925E-07f, 0f), new float4(15.5000038f, -4.999997f, -17.9999981f, 1f)), name: "Box #262"))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(-1.4850649E-06f, 1f, -4.95021624E-08f, -0.4999974f),
					new float4(1.50021856E-06f, -1f, 5.00072872E-08f, 3.7505464E-07f),
					new float4(-1f, 0f, 0f, -0.25f),
					new float4(1f, 0f, 0f, -0.25f),
					new float4(0f, 0f, -1f, -7.5f),
					new float4(0.707106769f, 0f, 0.707106769f, -5.126524f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(-1f, 0f, 0f, 0f), new float4(0f, 1f, -1.685874E-07f, 0f), new float4(0f, -1.685874E-07f, -1f, 0f), new float4(18.75f, -1.5f, -25.5f, 1f)), name: "Box #341"))
				;
			ContentsHarvest.AssertMatchesOracle(scene, "sample scene Model2 finding 6");
		}

		[Test]
		public void HoleOnExtrudedShapeTop_WithAddBox34AndAddBoxAndAddBox46_Model1F02()
		{
			var scene = new ContentsScene()
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, -1f, 6.953876E-08f, 0.5f),
					new float4(0f, 1f, 0f, -2.5f),
					new float4(0f, 0f, -1f, -1.49999988f),
					new float4(0.9486831f, 0f, -0.31622833f, -10.9098549f),
					new float4(0f, 0f, 1f, -1.49999988f),
					new float4(-0.948683262f, 0f, -0.3162278f, -10.9098577f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(-1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, -1f, 0f), new float4(0f, -6.75f, 13.4999981f, 1f)), name: "Extruded Shape #7"))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(-1f, 0f, 0f, -3.5f),
					new float4(0f, -1f, 0f, 0f),
					new float4(0f, 0f, -1f, -1f),
					new float4(1f, 0f, 0f, -3.5f),
					new float4(0f, 1f, 0f, -2f),
					new float4(0f, 0f, 1f, -1f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(-1.00000048f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, -1.00000048f, 0f), new float4(0f, -6.25f, 12f, 1f)), name: "Box (34) #135"))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(-1f, 0f, 0f, -1.25f),
					new float4(0f, -1f, 0f, 0f),
					new float4(0f, 0f, -1f, -0.249999523f),
					new float4(1f, 0f, 0f, -1.25f),
					new float4(0f, 1f, 0f, -0.500000238f),
					new float4(0f, 0f, 1f, -0.249999523f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(-1.00000048f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, -1.00000048f, 0f), new float4(-4.75f, -4.75f, 13.25f, 1f)), name: "Box #136"))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(-1f, 0f, 0f, -0.75f),
					new float4(0f, -1f, 0f, 0f),
					new float4(0f, 0f, -1f, -0.7500001f),
					new float4(1f, 0f, 0f, -0.75f),
					new float4(0f, 1f, 0f, -1.5f),
					new float4(0f, 0f, 1f, -0.7500001f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(0.05914691f, 0f, 0.9982498f, 0f), new float4(0f, 1f, 0f, 0f), new float4(-0.9982498f, 0f, 0.05914691f, 0f), new float4(-3f, -4.25f, 12.5f, 1f)), name: "Box (46) #155"))
				;
			ContentsHarvest.AssertMatchesOracle(scene, "sample scene Model1 finding 2");
		}

		[Test]
		public void HoleOnExtrudedShapeNegZ_WithSubBoxAndAddBox_Model1F04()
		{
			var scene = new ContentsScene()
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, -1f, 6.953876E-08f, 0.5f),
					new float4(0f, 1f, 0f, -2.5f),
					new float4(0f, 0f, -1f, -1.49999988f),
					new float4(0.9486831f, 0f, -0.31622833f, -10.9098549f),
					new float4(0f, 0f, 1f, -1.49999988f),
					new float4(-0.948683262f, 0f, -0.3162278f, -10.9098577f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(-1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, -1f, 0f), new float4(0f, -6.75f, 13.4999981f, 1f)), name: "Extruded Shape #7"))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(-1f, 0f, 0f, -2.5f),
					new float4(0f, -1f, 0f, 0f),
					new float4(0f, 0f, -1f, -0.25f),
					new float4(1f, 0f, 0f, -1.5f),
					new float4(0f, 1f, 0f, -1.5f),
					new float4(0f, 0f, 1f, -0.25f),
				}, contents: 0, operation: CSGOperationType.Subtractive,
					localToTree: new float4x4(new float4(-1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, -1f, 0f), new float4(-8.5f, -5.75f, 12.125f, 1f)), name: "Box #22"))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 1f, 3.32604832E-05f, -1.66302416E-05f),
					new float4(0f, -0.7071071f, -0.7071064f, -0.353506476f),
					new float4(-0.5773246f, -0.5773634f, -0.5773628f, -6.639214f),
					new float4(0.5773246f, -0.5773634f, -0.5773628f, -6.639214f),
					new float4(0f, 3.36181474E-05f, 1f, -0.5f),
				}, contents: 0, operation: CSGOperationType.Subtractive,
					localToTree: new float4x4(new float4(-1.00000048f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, -1.00000048f, 0f), new float4(0f, -4.25f, 12.5f, 1f)), name: "Box #29"))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(-1f, 0f, 0f, -4.5f),
					new float4(0f, -1f, 0f, 0f),
					new float4(0f, 0f, -1f, -1f),
					new float4(1f, 0f, 0f, -4.5f),
					new float4(0f, 1f, 0f, -2f),
					new float4(0f, 0f, 1f, -1f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(-1.00000048f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, -1.00000048f, 0f), new float4(0f, -6.25f, 12f, 1f)), name: "Box #30"))
				;
			ContentsHarvest.AssertMatchesOracle(scene, "sample scene Model1 finding 4");
		}

		[Test]
		public void HoleOnExtrudedShapeNegZ_WithSubBoxAndAddBox_Model1F08()
		{
			var scene = new ContentsScene()
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, -1f, 6.953876E-08f, 0.5f),
					new float4(0f, 1f, 0f, -2.5f),
					new float4(0f, 0f, -1f, -1.49999988f),
					new float4(0.9486831f, 0f, -0.31622833f, -10.9098549f),
					new float4(0f, 0f, 1f, -1.49999988f),
					new float4(-0.948683262f, 0f, -0.3162278f, -10.9098577f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(-1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, -1f, 0f), new float4(0f, -6.75f, 13.4999981f, 1f)), name: "Extruded Shape #7"))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(-1f, 0f, 0f, -1.5f),
					new float4(0f, -1f, 0f, 0f),
					new float4(0f, 0f, -1f, -0.25f),
					new float4(1f, 0f, 0f, -2.5f),
					new float4(0f, 1f, 0f, -1.5f),
					new float4(0f, 0f, 1f, -0.25f),
				}, contents: 0, operation: CSGOperationType.Subtractive,
					localToTree: new float4x4(new float4(-1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, -1f, 0f), new float4(8.5f, -5.75f, 12.125f, 1f)), name: "Box #26"))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 1f, 3.32604832E-05f, -1.66302416E-05f),
					new float4(0f, -0.7071071f, -0.7071064f, -0.353506476f),
					new float4(-0.5773246f, -0.5773634f, -0.5773628f, -6.639214f),
					new float4(0.5773246f, -0.5773634f, -0.5773628f, -6.639214f),
					new float4(0f, 3.36181474E-05f, 1f, -0.5f),
				}, contents: 0, operation: CSGOperationType.Subtractive,
					localToTree: new float4x4(new float4(-1.00000048f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, -1.00000048f, 0f), new float4(0f, -4.25f, 12.5f, 1f)), name: "Box #29"))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(-1f, 0f, 0f, -4.5f),
					new float4(0f, -1f, 0f, 0f),
					new float4(0f, 0f, -1f, -1f),
					new float4(1f, 0f, 0f, -4.5f),
					new float4(0f, 1f, 0f, -2f),
					new float4(0f, 0f, 1f, -1f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(-1.00000048f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, -1.00000048f, 0f), new float4(0f, -6.25f, 12f, 1f)), name: "Box #30"))
				;
			ContentsHarvest.AssertMatchesOracle(scene, "sample scene Model1 finding 8");
		}

		[Test]
		public void HoleOnExtrudedShapeBottom_WithAddBoxAndAddBox15AndAddExtrudedShape2_Model2F04()
		{
			var scene = new ContentsScene()
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(-1.74902783E-14f, -1f, 5.9604627E-08f, -1f),
					new float4(1.74902783E-14f, 1f, -5.9604627E-08f, 0.5f),
					new float4(-0.8944274f, 0f, 0.4472131f, -5.813776f),
					new float4(-0.5144956f, 0f, -0.857493f, -1.11474156f),
					new float4(0.8944272f, 0f, -0.44721362f, -5.81377649f),
					new float4(0.51449573f, 0f, 0.8574929f, -1.11474061f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(-1.00000048f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, -1.00000048f, 0f), new float4(-7.500004f, 0f, -16.0000076f, 1f)), name: "Extruded Shape #172"))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(-1.45915264E-15f, 1f, -2.12873918E-09f, 1.06436939E-08f),
					new float4(0f, -1f, 0f, -1f),
					new float4(-1f, 4.4822762E-05f, -1.53269343E-06f, -7.99999142f),
					new float4(1f, -4.43458557E-05f, 4.72003418E-14f, -7.99999952f),
					new float4(-1.37090876E-06f, 4.386908E-05f, -1f, -8.999989f),
					new float4(0f, -4.386902E-05f, 1f, -4.999999f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(-1.00000048f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, -1.00000048f, 0f), new float4(-1.36423772E-12f, -1f, -25f, 1f)), name: "Box #173"))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(-1f, 0f, 0f, -1f),
					new float4(0f, -1f, 0f, -3.5f),
					new float4(0f, 0f, -1f, -1f),
					new float4(1f, 0f, 0f, -1f),
					new float4(0f, 1f, 0f, -6.5f),
					new float4(0f, 0f, 1f, -1f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1.00000048f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1.00000048f, 0f), new float4(-7.5f, 2.5f, -18f, 1f)), name: "Box (15) #260"))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(9.169917E-09f, -1f, 7.488786E-08f, -1f),
					new float4(-5.73119063E-09f, 1f, -6.667312E-08f, 0.5f),
					new float4(-0.8944274f, 0f, 0.447213173f, -5.813776f),
					new float4(-0.5144956f, 0f, -0.857493f, -1.11474156f),
					new float4(0.8944272f, 0f, -0.4472136f, -5.81377649f),
					new float4(0.51449573f, 0f, 0.8574929f, -0.5573704f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(-1.00000048f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, -1.00000048f, 0f), new float4(-7.500004f, 0f, -16.0000076f, 1f)), name: "Extruded Shape (2) #319"))
				;
			ContentsHarvest.AssertMatchesOracle(scene, "sample scene Model2 finding 4");
		}
	}
}
