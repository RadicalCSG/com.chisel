using NUnit.Framework;
using Unity.Mathematics;

namespace Chisel.Core.Tests
{
	[TestFixture]
	[Category("Contents")]
	[Category("CSGCorpus")]
	public class SampleSceneExtraFaceTests
	{
		[Test]
		public void ExtraFaceOnBox38Top_WithAddBoxAndAddBox9_Model1F03()
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
					new float4(0f, 0.780868769f, 0.6246951f, -1.24939024f),
					new float4(0f, -1f, 0f, 0f),
					new float4(-1f, 0f, 0f, -0.9999999f),
					new float4(1f, 0f, 0f, -0.9999999f),
					new float4(0f, 0f, -1f, -0.499999523f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(0f, -2.38418579E-07f, -1f, 0f), new float4(-1f, 0f, 0f, 0f), new float4(0f, 1f, -2.38418579E-07f, 0f), new float4(11.000001f, -5.75f, 2.98023224E-07f, 1f)), name: "Box (9) #80"))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(-1f, 0f, 0f, -0.25f),
					new float4(0f, -1f, 0f, 0f),
					new float4(0f, 0f, -1f, -13.5f),
					new float4(1f, 0f, 0f, -0.25f),
					new float4(0f, 1f, 0f, -0.5f),
					new float4(0f, 0f, 1f, -1.5f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(-1.00000048f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, -1.00000048f, 0f), new float4(9.75f, -6.75f, -6.50000048f, 1f)), name: "Box (38) #141"))
				;
			ContentsHarvest.AssertMatchesOracle(scene, "sample scene Model1 finding 3");
		}

		[Test]
		public void ExtraFaceOnBoxBottom_WithAddBox_Model1F05()
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
					new float4(-1f, 0f, 0f, -0.25f),
					new float4(0f, -1f, 0f, 0f),
					new float4(0f, 0f, -1f, -5.5f),
					new float4(1f, 0f, 0f, -0.25f),
					new float4(0f, 1f, 0f, -0.5f),
					new float4(0f, 0f, 1f, -1.5f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(-1.00000048f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, -1.00000048f, 0f), new float4(-9.75f, -6.75f, -6.5f, 1f)), name: "Box #140"))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(-1f, 0f, 0f, -1.5f),
					new float4(0f, -1f, 0f, -0.5f),
					new float4(0f, 0f, -1f, -1.5f),
					new float4(1f, 0f, 0f, -1.5f),
					new float4(0f, 1f, 0f, -2.5f),
					new float4(0f, 0f, 1f, -1.5f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(-0.9658843f, 0f, -0.258973718f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0.258973837f, 0f, -0.9658848f, 0f), new float4(-9f, -5.75f, -1f, 1f)), name: "Box #146"))
				;
			ContentsHarvest.AssertMatchesOracle(scene, "sample scene Model1 finding 5");
		}

		[Test]
		public void ExtraFaceOnBox38Top_WithAddBoxAndAddBox9_Model1F07()
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
					new float4(0f, 0.780868769f, 0.6246951f, -1.24939024f),
					new float4(0f, -1f, 0f, 0f),
					new float4(-1f, 0f, 0f, -0.9999999f),
					new float4(1f, 0f, 0f, -0.9999999f),
					new float4(0f, 0f, -1f, -0.499999523f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(0f, -2.38418579E-07f, -1f, 0f), new float4(-1f, 0f, 0f, 0f), new float4(0f, 1f, -2.38418579E-07f, 0f), new float4(11.000001f, -5.75000048f, -4f, 1f)), name: "Box (9) #84"))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(-1f, 0f, 0f, -0.25f),
					new float4(0f, -1f, 0f, 0f),
					new float4(0f, 0f, -1f, -13.5f),
					new float4(1f, 0f, 0f, -0.25f),
					new float4(0f, 1f, 0f, -0.5f),
					new float4(0f, 0f, 1f, -1.5f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(-1.00000048f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, -1.00000048f, 0f), new float4(9.75f, -6.75f, -6.50000048f, 1f)), name: "Box (38) #141"))
				;
			ContentsHarvest.AssertMatchesOracle(scene, "sample scene Model1 finding 7");
		}

		[Test]
		public void ExtraFaceOnBox38Top_WithAddBoxAndAddBox9_Model1F09()
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
					new float4(0f, 0.780868769f, 0.6246951f, -1.24939024f),
					new float4(0f, -1f, 0f, 0f),
					new float4(-1f, 0f, 0f, -0.9999999f),
					new float4(1f, 0f, 0f, -0.9999999f),
					new float4(0f, 0f, -1f, -0.499999523f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(0f, -2.38418579E-07f, -1f, 0f), new float4(-1f, 0f, 0f, 0f), new float4(0f, 1f, -2.38418579E-07f, 0f), new float4(11.000001f, -5.75f, 4f, 1f)), name: "Box (9) #76"))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(-1f, 0f, 0f, -0.25f),
					new float4(0f, -1f, 0f, 0f),
					new float4(0f, 0f, -1f, -13.5f),
					new float4(1f, 0f, 0f, -0.25f),
					new float4(0f, 1f, 0f, -0.5f),
					new float4(0f, 0f, 1f, -1.5f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(-1.00000048f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, -1.00000048f, 0f), new float4(9.75f, -6.75f, -6.50000048f, 1f)), name: "Box (38) #141"))
				;
			ContentsHarvest.AssertMatchesOracle(scene, "sample scene Model1 finding 9");
		}

		[Test]
		public void ExtraFaceOnBox3Bottom_WithAddBox1AndAddBox2_Model4F01()
		{
			var scene = new ContentsScene()
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 1f, 0f, -3f),
					new float4(0f, -1f, 0f, 0f),
					new float4(-0.6f, 0.8f, 0f, -3.6f),
					new float4(0.6f, 0.8f, 0f, -3.6f),
					new float4(0f, 0f, -1f, -0.25f),
					new float4(0f, 0f, 1f, -0.25f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, -1.1920929E-07f, -1.00000012f, 0f), new float4(0f, 1.00000012f, -1.1920929E-07f, 0f), new float4(-18.5f, 0.5f, -4f, 1f)), name: "Box 1 #397"))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 1f, 0f, -2.5f),
					new float4(0f, -1f, 0f, 0f),
					new float4(-0.5812382f, 0.813733459f, 0f, -3.19681f),
					new float4(0.5812382f, 0.813733459f, 0f, -3.19681f),
					new float4(0f, 0f, -1f, -0.25f),
					new float4(0f, 0f, 1f, -0.25f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, -1.1920929E-07f, -1.00000012f, 0f), new float4(0f, 1.00000012f, -1.1920929E-07f, 0f), new float4(-18.5f, 0.5f, -4f, 1f)), name: "Box 2 #398"))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(-1f, 0f, 0f, -0.125f),
					new float4(0f, -1f, 0f, -0.5000004f),
					new float4(0f, 0f, -1f, -0.625f),
					new float4(1f, 0f, 0f, -0.125f),
					new float4(0f, 1f, 0f, 0f),
					new float4(0f, 0f, 1f, -1.125f),
				}, contents: 0, operation: CSGOperationType.Subtractive,
					localToTree: new float4x4(new float4(-1f, 0f, 0f, 0f), new float4(0f, 1f, -1.685874E-07f, 0f), new float4(0f, -1.685874E-07f, -1f, 0f), new float4(-19.5f, 0.75f, -5.125f, 1f)), name: "Box 3 #399"))
				;
			ContentsHarvest.AssertMatchesOracle(scene, "sample scene Model4 finding 1");
		}

		[Test]
		public void ExtraFaceOnBox4Bottom_WithAddBox1AndAddBox2_Model4F02()
		{
			var scene = new ContentsScene()
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 1f, 0f, -3f),
					new float4(0f, -1f, 0f, 0f),
					new float4(-0.6f, 0.8f, 0f, -3.6f),
					new float4(0.6f, 0.8f, 0f, -3.6f),
					new float4(0f, 0f, -1f, -0.25f),
					new float4(0f, 0f, 1f, -0.25f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, -1.1920929E-07f, -1.00000012f, 0f), new float4(0f, 1.00000012f, -1.1920929E-07f, 0f), new float4(-18.5f, 0.5f, -4f, 1f)), name: "Box 1 #397"))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 1f, 0f, -2.5f),
					new float4(0f, -1f, 0f, 0f),
					new float4(-0.5812382f, 0.813733459f, 0f, -3.19681f),
					new float4(0.5812382f, 0.813733459f, 0f, -3.19681f),
					new float4(0f, 0f, -1f, -0.25f),
					new float4(0f, 0f, 1f, -0.25f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, -1.1920929E-07f, -1.00000012f, 0f), new float4(0f, 1.00000012f, -1.1920929E-07f, 0f), new float4(-18.5f, 0.5f, -4f, 1f)), name: "Box 2 #398"))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(-1f, 0f, 0f, -0.125f),
					new float4(0f, -1f, 0f, -0.5000004f),
					new float4(0f, 0f, -1f, -0.625f),
					new float4(1f, 0f, 0f, -0.125f),
					new float4(0f, 1f, 0f, 0f),
					new float4(0f, 0f, 1f, -1.125f),
				}, contents: 0, operation: CSGOperationType.Subtractive,
					localToTree: new float4x4(new float4(-1f, 0f, 0f, 0f), new float4(0f, 1f, -1.685874E-07f, 0f), new float4(0f, -1.685874E-07f, -1f, 0f), new float4(-18.75f, 0.75f, -5.125f, 1f)), name: "Box 4 #400"))
				;
			ContentsHarvest.AssertMatchesOracle(scene, "sample scene Model4 finding 2");
		}

		[Test]
		public void ExtraFaceOnBox5Bottom_WithAddBox1AndAddBox2_Model4F03()
		{
			var scene = new ContentsScene()
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 1f, 0f, -3f),
					new float4(0f, -1f, 0f, 0f),
					new float4(-0.6f, 0.8f, 0f, -3.6f),
					new float4(0.6f, 0.8f, 0f, -3.6f),
					new float4(0f, 0f, -1f, -0.25f),
					new float4(0f, 0f, 1f, -0.25f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, -1.1920929E-07f, -1.00000012f, 0f), new float4(0f, 1.00000012f, -1.1920929E-07f, 0f), new float4(-18.5f, 0.5f, -4f, 1f)), name: "Box 1 #397"))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 1f, 0f, -2.5f),
					new float4(0f, -1f, 0f, 0f),
					new float4(-0.5812382f, 0.813733459f, 0f, -3.19681f),
					new float4(0.5812382f, 0.813733459f, 0f, -3.19681f),
					new float4(0f, 0f, -1f, -0.25f),
					new float4(0f, 0f, 1f, -0.25f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, -1.1920929E-07f, -1.00000012f, 0f), new float4(0f, 1.00000012f, -1.1920929E-07f, 0f), new float4(-18.5f, 0.5f, -4f, 1f)), name: "Box 2 #398"))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(-1f, 0f, 0f, -0.125f),
					new float4(0f, -1f, 0f, -0.5000004f),
					new float4(0f, 0f, -1f, -0.625f),
					new float4(1f, 0f, 0f, -0.125f),
					new float4(0f, 1f, 0f, 0f),
					new float4(0f, 0f, 1f, -1.125f),
				}, contents: 0, operation: CSGOperationType.Subtractive,
					localToTree: new float4x4(new float4(-1f, 0f, 0f, 0f), new float4(0f, 1f, -1.685874E-07f, 0f), new float4(0f, -1.685874E-07f, -1f, 0f), new float4(-17.9999943f, 0.75f, -5.125f, 1f)), name: "Box 5 #401"))
				;
			ContentsHarvest.AssertMatchesOracle(scene, "sample scene Model4 finding 3");
		}

		[Test]
		public void ExtraFaceOnBox6Bottom_WithAddBox1AndAddBox2_Model4F04()
		{
			var scene = new ContentsScene()
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 1f, 0f, -3f),
					new float4(0f, -1f, 0f, 0f),
					new float4(-0.6f, 0.8f, 0f, -3.6f),
					new float4(0.6f, 0.8f, 0f, -3.6f),
					new float4(0f, 0f, -1f, -0.25f),
					new float4(0f, 0f, 1f, -0.25f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, -1.1920929E-07f, -1.00000012f, 0f), new float4(0f, 1.00000012f, -1.1920929E-07f, 0f), new float4(-18.5f, 0.5f, -4f, 1f)), name: "Box 1 #397"))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 1f, 0f, -2.5f),
					new float4(0f, -1f, 0f, 0f),
					new float4(-0.5812382f, 0.813733459f, 0f, -3.19681f),
					new float4(0.5812382f, 0.813733459f, 0f, -3.19681f),
					new float4(0f, 0f, -1f, -0.25f),
					new float4(0f, 0f, 1f, -0.25f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, -1.1920929E-07f, -1.00000012f, 0f), new float4(0f, 1.00000012f, -1.1920929E-07f, 0f), new float4(-18.5f, 0.5f, -4f, 1f)), name: "Box 2 #398"))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(-1f, 0f, 0f, -0.125f),
					new float4(0f, -1f, 0f, -0.5000004f),
					new float4(0f, 0f, -1f, -0.625f),
					new float4(1f, 0f, 0f, -0.125f),
					new float4(0f, 1f, 0f, 0f),
					new float4(0f, 0f, 1f, -0.625f),
				}, contents: 0, operation: CSGOperationType.Subtractive,
					localToTree: new float4x4(new float4(-1f, 0f, 0f, 0f), new float4(0f, 1f, -1.685874E-07f, 0f), new float4(0f, -1.685874E-07f, -1f, 0f), new float4(-20.9999943f, 0.75f, -5.125f, 1f)), name: "Box 6 #402"))
				;
			ContentsHarvest.AssertMatchesOracle(scene, "sample scene Model4 finding 4");
		}

		[Test]
		public void ExtraFaceOnBox7Bottom_WithAddBox1AndAddBox2_Model4F05()
		{
			var scene = new ContentsScene()
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 1f, 0f, -3f),
					new float4(0f, -1f, 0f, 0f),
					new float4(-0.6f, 0.8f, 0f, -3.6f),
					new float4(0.6f, 0.8f, 0f, -3.6f),
					new float4(0f, 0f, -1f, -0.25f),
					new float4(0f, 0f, 1f, -0.25f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, -1.1920929E-07f, -1.00000012f, 0f), new float4(0f, 1.00000012f, -1.1920929E-07f, 0f), new float4(-18.5f, 0.5f, -4f, 1f)), name: "Box 1 #397"))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 1f, 0f, -2.5f),
					new float4(0f, -1f, 0f, 0f),
					new float4(-0.5812382f, 0.813733459f, 0f, -3.19681f),
					new float4(0.5812382f, 0.813733459f, 0f, -3.19681f),
					new float4(0f, 0f, -1f, -0.25f),
					new float4(0f, 0f, 1f, -0.25f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, -1.1920929E-07f, -1.00000012f, 0f), new float4(0f, 1.00000012f, -1.1920929E-07f, 0f), new float4(-18.5f, 0.5f, -4f, 1f)), name: "Box 2 #398"))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(-1f, 0f, 0f, -0.125f),
					new float4(0f, -1f, 0f, -0.5000004f),
					new float4(0f, 0f, -1f, -0.625f),
					new float4(1f, 0f, 0f, -0.125f),
					new float4(0f, 1f, 0f, 0f),
					new float4(0f, 0f, 1f, -0.125f),
				}, contents: 0, operation: CSGOperationType.Subtractive,
					localToTree: new float4x4(new float4(-1f, 0f, 0f, 0f), new float4(0f, 1f, -1.685874E-07f, 0f), new float4(0f, -1.685874E-07f, -1f, 0f), new float4(-21.75f, 0.75f, -5.125f, 1f)), name: "Box 7 #403"))
				;
			ContentsHarvest.AssertMatchesOracle(scene, "sample scene Model4 finding 5");
		}

		[Test]
		public void ExtraFaceOnBox8Bottom_WithAddBox1AndAddBox2_Model4F06()
		{
			var scene = new ContentsScene()
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 1f, 0f, -3f),
					new float4(0f, -1f, 0f, 0f),
					new float4(-0.6f, 0.8f, 0f, -3.6f),
					new float4(0.6f, 0.8f, 0f, -3.6f),
					new float4(0f, 0f, -1f, -0.25f),
					new float4(0f, 0f, 1f, -0.25f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, -1.1920929E-07f, -1.00000012f, 0f), new float4(0f, 1.00000012f, -1.1920929E-07f, 0f), new float4(-18.5f, 0.5f, -4f, 1f)), name: "Box 1 #397"))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 1f, 0f, -2.5f),
					new float4(0f, -1f, 0f, 0f),
					new float4(-0.5812382f, 0.813733459f, 0f, -3.19681f),
					new float4(0.5812382f, 0.813733459f, 0f, -3.19681f),
					new float4(0f, 0f, -1f, -0.25f),
					new float4(0f, 0f, 1f, -0.25f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, -1.1920929E-07f, -1.00000012f, 0f), new float4(0f, 1.00000012f, -1.1920929E-07f, 0f), new float4(-18.5f, 0.5f, -4f, 1f)), name: "Box 2 #398"))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(-1f, 0f, 0f, -0.125f),
					new float4(0f, -1f, 0f, -0.5000004f),
					new float4(0f, 0f, -1f, -0.625f),
					new float4(1f, 0f, 0f, -0.125f),
					new float4(0f, 1f, 0f, 0f),
					new float4(0f, 0f, 1f, -1.125f),
				}, contents: 0, operation: CSGOperationType.Subtractive,
					localToTree: new float4x4(new float4(-1f, 0f, 0f, 0f), new float4(0f, 1f, -1.685874E-07f, 0f), new float4(0f, -1.685874E-07f, -1f, 0f), new float4(-17.2499943f, 0.75f, -5.125f, 1f)), name: "Box 8 #404"))
				;
			ContentsHarvest.AssertMatchesOracle(scene, "sample scene Model4 finding 6");
		}

		[Test]
		public void ExtraFaceOnBox9Bottom_WithAddBox1AndAddBox2_Model4F07()
		{
			var scene = new ContentsScene()
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 1f, 0f, -3f),
					new float4(0f, -1f, 0f, 0f),
					new float4(-0.6f, 0.8f, 0f, -3.6f),
					new float4(0.6f, 0.8f, 0f, -3.6f),
					new float4(0f, 0f, -1f, -0.25f),
					new float4(0f, 0f, 1f, -0.25f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, -1.1920929E-07f, -1.00000012f, 0f), new float4(0f, 1.00000012f, -1.1920929E-07f, 0f), new float4(-18.5f, 0.5f, -4f, 1f)), name: "Box 1 #397"))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 1f, 0f, -2.5f),
					new float4(0f, -1f, 0f, 0f),
					new float4(-0.5812382f, 0.813733459f, 0f, -3.19681f),
					new float4(0.5812382f, 0.813733459f, 0f, -3.19681f),
					new float4(0f, 0f, -1f, -0.25f),
					new float4(0f, 0f, 1f, -0.25f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, -1.1920929E-07f, -1.00000012f, 0f), new float4(0f, 1.00000012f, -1.1920929E-07f, 0f), new float4(-18.5f, 0.5f, -4f, 1f)), name: "Box 2 #398"))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(-1f, 0f, 0f, -0.125f),
					new float4(0f, -1f, 0f, -0.5000004f),
					new float4(0f, 0f, -1f, -0.625f),
					new float4(1f, 0f, 0f, -0.125f),
					new float4(0f, 1f, 0f, 0f),
					new float4(0f, 0f, 1f, -0.875f),
				}, contents: 0, operation: CSGOperationType.Subtractive,
					localToTree: new float4x4(new float4(-1f, 0f, 0f, 0f), new float4(0f, 1f, -1.685874E-07f, 0f), new float4(0f, -1.685874E-07f, -1f, 0f), new float4(-20.25f, 0.75f, -5.125f, 1f)), name: "Box 9 #405"))
				;
			ContentsHarvest.AssertMatchesOracle(scene, "sample scene Model4 finding 7");
		}

		[Test]
		public void ExtraFaceOnBox10Bottom_WithAddBox1AndAddBox2_Model4F08()
		{
			var scene = new ContentsScene()
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 1f, 0f, -3f),
					new float4(0f, -1f, 0f, 0f),
					new float4(-0.6f, 0.8f, 0f, -3.6f),
					new float4(0.6f, 0.8f, 0f, -3.6f),
					new float4(0f, 0f, -1f, -0.25f),
					new float4(0f, 0f, 1f, -0.25f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, -1.1920929E-07f, -1.00000012f, 0f), new float4(0f, 1.00000012f, -1.1920929E-07f, 0f), new float4(-18.5f, 0.5f, -4f, 1f)), name: "Box 1 #397"))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 1f, 0f, -2.5f),
					new float4(0f, -1f, 0f, 0f),
					new float4(-0.5812382f, 0.813733459f, 0f, -3.19681f),
					new float4(0.5812382f, 0.813733459f, 0f, -3.19681f),
					new float4(0f, 0f, -1f, -0.25f),
					new float4(0f, 0f, 1f, -0.25f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, -1.1920929E-07f, -1.00000012f, 0f), new float4(0f, 1.00000012f, -1.1920929E-07f, 0f), new float4(-18.5f, 0.5f, -4f, 1f)), name: "Box 2 #398"))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(-1f, 0f, 0f, -0.125f),
					new float4(0f, -1f, 0f, -0.5000004f),
					new float4(0f, 0f, -1f, -0.625f),
					new float4(1f, 0f, 0f, -0.125f),
					new float4(0f, 1f, 0f, 0f),
					new float4(0f, 0f, 1f, -0.625f),
				}, contents: 0, operation: CSGOperationType.Subtractive,
					localToTree: new float4x4(new float4(-1f, 0f, 0f, 0f), new float4(0f, 1f, -1.685874E-07f, 0f), new float4(0f, -1.685874E-07f, -1f, 0f), new float4(-15.75f, 0.75f, -5.125f, 1f)), name: "Box10 #406"))
				;
			ContentsHarvest.AssertMatchesOracle(scene, "sample scene Model4 finding 8");
		}

		[Test]
		public void ExtraFaceOnBox11Bottom_WithAddBox1AndAddBox2_Model4F09()
		{
			var scene = new ContentsScene()
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 1f, 0f, -3f),
					new float4(0f, -1f, 0f, 0f),
					new float4(-0.6f, 0.8f, 0f, -3.6f),
					new float4(0.6f, 0.8f, 0f, -3.6f),
					new float4(0f, 0f, -1f, -0.25f),
					new float4(0f, 0f, 1f, -0.25f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, -1.1920929E-07f, -1.00000012f, 0f), new float4(0f, 1.00000012f, -1.1920929E-07f, 0f), new float4(-18.5f, 0.5f, -4f, 1f)), name: "Box 1 #397"))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 1f, 0f, -2.5f),
					new float4(0f, -1f, 0f, 0f),
					new float4(-0.5812382f, 0.813733459f, 0f, -3.19681f),
					new float4(0.5812382f, 0.813733459f, 0f, -3.19681f),
					new float4(0f, 0f, -1f, -0.25f),
					new float4(0f, 0f, 1f, -0.25f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, -1.1920929E-07f, -1.00000012f, 0f), new float4(0f, 1.00000012f, -1.1920929E-07f, 0f), new float4(-18.5f, 0.5f, -4f, 1f)), name: "Box 2 #398"))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(-1f, 0f, 0f, -0.125f),
					new float4(0f, -1f, 0f, -0.5000004f),
					new float4(0f, 0f, -1f, -0.625f),
					new float4(1f, 0f, 0f, -0.125f),
					new float4(0f, 1f, 0f, 0f),
					new float4(0f, 0f, 1f, -0.875f),
				}, contents: 0, operation: CSGOperationType.Subtractive,
					localToTree: new float4x4(new float4(-1f, 0f, 0f, 0f), new float4(0f, 1f, -1.685874E-07f, 0f), new float4(0f, -1.685874E-07f, -1f, 0f), new float4(-16.5f, 0.75f, -5.125f, 1f)), name: "Box11 #407"))
				;
			ContentsHarvest.AssertMatchesOracle(scene, "sample scene Model4 finding 9");
		}

		[Test]
		public void ExtraFaceOnBox12Bottom_WithAddBox1AndAddBox2_Model4F10()
		{
			var scene = new ContentsScene()
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 1f, 0f, -3f),
					new float4(0f, -1f, 0f, 0f),
					new float4(-0.6f, 0.8f, 0f, -3.6f),
					new float4(0.6f, 0.8f, 0f, -3.6f),
					new float4(0f, 0f, -1f, -0.25f),
					new float4(0f, 0f, 1f, -0.25f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, -1.1920929E-07f, -1.00000012f, 0f), new float4(0f, 1.00000012f, -1.1920929E-07f, 0f), new float4(-18.5f, 0.5f, -4f, 1f)), name: "Box 1 #397"))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 1f, 0f, -2.5f),
					new float4(0f, -1f, 0f, 0f),
					new float4(-0.5812382f, 0.813733459f, 0f, -3.19681f),
					new float4(0.5812382f, 0.813733459f, 0f, -3.19681f),
					new float4(0f, 0f, -1f, -0.25f),
					new float4(0f, 0f, 1f, -0.25f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, -1.1920929E-07f, -1.00000012f, 0f), new float4(0f, 1.00000012f, -1.1920929E-07f, 0f), new float4(-18.5f, 0.5f, -4f, 1f)), name: "Box 2 #398"))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(-1f, 0f, 0f, -0.125f),
					new float4(0f, -1f, 0f, -0.5000004f),
					new float4(0f, 0f, -1f, -0.625f),
					new float4(1f, 0f, 0f, -0.125f),
					new float4(0f, 1f, 0f, 0f),
					new float4(0f, 0f, 1f, -0.125f),
				}, contents: 0, operation: CSGOperationType.Subtractive,
					localToTree: new float4x4(new float4(-1f, 0f, 0f, 0f), new float4(0f, 1f, -1.685874E-07f, 0f), new float4(0f, -1.685874E-07f, -1f, 0f), new float4(-15f, 0.75f, -5.125f, 1f)), name: "Box12 #408"))
				;
			ContentsHarvest.AssertMatchesOracle(scene, "sample scene Model4 finding 10");
		}

		[Test]
		public void ExtraFaceOnBoxBottom_WithAddBox_Model1F06()
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
					new float4(-1f, 0f, 0f, -0.5f),
					new float4(0f, -1f, 0f, 0f),
					new float4(0f, 0f, -1f, -1.00000012f),
					new float4(1f, 0f, 0f, -0.5f),
					new float4(0f, 1f, 0f, -10f),
					new float4(0f, 0f, 1f, -1.00000012f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, -1f, 0f), new float4(-11.5000048f, -6.25f, 0f, 1f)), name: "Box #96"))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(-1f, 0f, 0f, -1.75f),
					new float4(0f, -1f, 0f, 0f),
					new float4(0f, 0f, -1f, -0.25f),
					new float4(1f, 0f, 0f, -1.75f),
					new float4(0f, 1f, 0f, -0.5f),
					new float4(0f, 0f, 1f, -0.25f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(-1.00000048f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, -1.00000048f, 0f), new float4(-11.25f, -6.75f, -0.75f, 1f)), name: "Box #144"))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(-1f, 0f, 0f, -0.5f),
					new float4(0f, -1f, 0f, 0f),
					new float4(0f, 0f, -1f, -3f),
					new float4(1f, 0f, 0f, -0.5f),
					new float4(0f, 1f, 0f, -6f),
					new float4(0f, 0f, 1f, -3f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(-1.00000048f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, -1.00000048f, 0f), new float4(-12.0000057f, -6.25f, 2.000001f, 1f)), name: "Box #165"))
				;
			ContentsHarvest.AssertMatchesOracle(scene, "sample scene Model1 finding 6");
		}

		[Test]
		public void ExtraFaceOnBoxTop_WithAddBoxAndAddBox29_Model2F01()
		{
			var scene = new ContentsScene()
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 1f, 0f, 0.5f),
					new float4(0f, -1f, 0f, -1f),
					new float4(-0.707106769f, 0f, 0.707106769f, -5.656854f),
					new float4(0.8944272f, 0f, -0.4472136f, -6.708204f),
					new float4(0f, 0f, -1f, -1f),
					new float4(0f, 0f, 1f, -1f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(-1.00000048f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, -1.00000048f, 0f), new float4(5.00000238f, 0f, -19.00001f, 1f)), name: "Box #170"))
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
					new float4(-1f, 0f, 0f, -7f),
					new float4(0f, -1f, 0f, -0.5f),
					new float4(0f, 0f, -1f, -3.000001f),
					new float4(1f, 0f, 0f, -3f),
					new float4(0f, 1f, 0f, 0f),
					new float4(0f, 0f, 1f, -7f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(-1.00000048f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, -1.00000048f, 0f), new float4(11f, -1f, -21f, 1f)), name: "Box #176"))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 1f, 0f, 0.5f),
					new float4(0f, -1f, 0f, -1f),
					new float4(-0.707106769f, 0f, 0.707106769f, -5.303301f),
					new float4(0.8944272f, 0f, -0.4472136f, -6.708204f),
					new float4(0f, 0f, -1f, -1f),
					new float4(0f, 0f, 1f, -0.5f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(-1.00000048f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, -1.00000048f, 0f), new float4(5.00000238f, 0f, -19.00001f, 1f)), name: "Box (29) #317"))
				;
			ContentsHarvest.AssertMatchesOracle(scene, "sample scene Model2 finding 1");
		}

		[Test]
		public void ExtraFaceOnBox1Top_WithAddBoxAndAddExtrudedShapeAdditiveAndSubExtrudedShapeRemove_Model2F02()
		{
			var scene = new ContentsScene()
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 1f, 0f, 0.5f),
					new float4(0f, -1f, 0f, -8.5f),
					new float4(-0.707106769f, 1.95982852E-06f, 0.707106769f, -4.24263954f),
					new float4(0.707106769f, 1.95982852E-06f, 0.707106769f, -4.24263954f),
					new float4(0f, -2.771616E-06f, -1f, -2.00000143f),
					new float4(0f, 2.771616E-06f, 1f, -1.99999857f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(-1.00000048f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, -1.00000048f, 0f), new float4(0f, -0.5f, -36f, 1f)), name: "Box (1) #175"))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(-1f, 0f, 0f, -12f),
					new float4(0f, -1f, 0f, 0f),
					new float4(0f, 0f, -1f, -3.5f),
					new float4(1f, 0f, 0f, -4f),
					new float4(0f, 1f, 0f, -1.5f),
					new float4(0f, 0f, 1f, -3.5f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(-1.00000024f, -1.77635726E-15f, -7.450583E-09f, 0f), new float4(-7.450582E-09f, -2.384186E-06f, 1f, 0f), new float4(-1.95399269E-14f, 1f, 2.384186E-06f, 0f), new float4(-4.000001f, -5.49999762f, -25.9999981f, 1f)), name: "Box #238"))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, -1f, 7.40718E-08f, -7f),
					new float4(0f, 1f, -7.40718E-08f, 6f),
					new float4(1f, 0f, 0f, -5.5f),
					new float4(0.707106769f, 0f, 0.7071068f, -5.656854f),
					new float4(0f, 0f, 1f, -5.49999952f),
					new float4(-0.707106769f, 0f, 0.7071068f, -5.656854f),
					new float4(-1f, 0f, 0f, -5.5f),
					new float4(-0.707106769f, 0f, -0.7071068f, -5.656854f),
					new float4(0f, 0f, -1f, -5.49999952f),
					new float4(0.707106769f, 0f, -0.7071068f, -5.656854f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(-1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, -1f, 0f), new float4(0f, 5f, -30f, 1f)), name: "Extruded Shape (Additive) #240"))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, -1f, 8.141123E-08f, -7f),
					new float4(0f, 1f, -8.141123E-08f, 6f),
					new float4(1f, 0f, 0f, -5f),
					new float4(0.7071067f, 0f, 0.7071068f, -4.949747f),
					new float4(0f, 0f, 1f, -4.99999952f),
					new float4(-0.7071067f, 0f, 0.7071068f, -4.949747f),
					new float4(-1f, 0f, 0f, -5f),
					new float4(-0.7071067f, 0f, -0.7071068f, -4.949747f),
					new float4(0f, 0f, -1f, -4.99999952f),
					new float4(0.7071067f, 0f, -0.7071068f, -4.949747f),
				}, contents: 0, operation: CSGOperationType.Subtractive,
					localToTree: new float4x4(new float4(-1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, -1f, 0f), new float4(0f, 5f, -30f, 1f)), name: "Extruded Shape (Remove) #274"))
				;
			ContentsHarvest.AssertMatchesOracle(scene, "sample scene Model2 finding 2");
		}

		[Test]
		public void ExtraFaceOnBox16Bottom_WithAddBox_Model2F03()
		{
			var scene = new ContentsScene()
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 1f, 0f, 0.5f),
					new float4(0f, -1f, 0f, -1f),
					new float4(-0.707106769f, 0f, 0.707106769f, -5.656854f),
					new float4(0.8944272f, 0f, -0.4472136f, -6.708204f),
					new float4(0f, 0f, -1f, -1f),
					new float4(0f, 0f, 1f, -1f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(-1.00000048f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, -1.00000048f, 0f), new float4(5.00000238f, 0f, -19.00001f, 1f)), name: "Box #170"))
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
					new float4(-1f, 0f, 0f, -7f),
					new float4(0f, -1f, 0f, -0.5f),
					new float4(0f, 0f, -1f, -3.000001f),
					new float4(1f, 0f, 0f, -3f),
					new float4(0f, 1f, 0f, 0f),
					new float4(0f, 0f, 1f, -7f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(-1.00000048f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, -1.00000048f, 0f), new float4(11f, -1f, -21f, 1f)), name: "Box #176"))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(-1f, 0f, 0f, -1f),
					new float4(0f, -1f, 0f, -3.5f),
					new float4(0f, 0f, -1f, -1f),
					new float4(1f, 0f, 0f, -1f),
					new float4(0f, 1f, 0f, -6.5f),
					new float4(0f, 0f, 1f, -1f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1.00000048f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1.00000048f, 0f), new float4(7.5f, 2.5f, -18f, 1f)), name: "Box (16) #261"))
				;
			ContentsHarvest.AssertMatchesOracle(scene, "sample scene Model2 finding 3");
		}

		[Test]
		public void ExtraFaceOnBoxBottom_WithAddBoxAndAddExtrudedShapeAdditiveAndSubExtrudedShapeRemove_Model2F05()
		{
			var scene = new ContentsScene()
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
					new float4(-1f, 0f, 0f, -12f),
					new float4(0f, -1f, 0f, 0f),
					new float4(0f, 0f, -1f, -3.5f),
					new float4(1f, 0f, 0f, -4f),
					new float4(0f, 1f, 0f, -1.5f),
					new float4(0f, 0f, 1f, -3.5f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(-1.00000024f, -1.77635726E-15f, -7.450583E-09f, 0f), new float4(-7.450582E-09f, -2.384186E-06f, 1f, 0f), new float4(-1.95399269E-14f, 1f, 2.384186E-06f, 0f), new float4(-4.000001f, -5.49999762f, -25.9999981f, 1f)), name: "Box #238"))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, -1f, 7.40718E-08f, -7f),
					new float4(0f, 1f, -7.40718E-08f, 6f),
					new float4(1f, 0f, 0f, -5.5f),
					new float4(0.707106769f, 0f, 0.7071068f, -5.656854f),
					new float4(0f, 0f, 1f, -5.49999952f),
					new float4(-0.707106769f, 0f, 0.7071068f, -5.656854f),
					new float4(-1f, 0f, 0f, -5.5f),
					new float4(-0.707106769f, 0f, -0.7071068f, -5.656854f),
					new float4(0f, 0f, -1f, -5.49999952f),
					new float4(0.707106769f, 0f, -0.7071068f, -5.656854f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(-1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, -1f, 0f), new float4(0f, 5f, -30f, 1f)), name: "Extruded Shape (Additive) #240"))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, -1f, 8.141123E-08f, -7f),
					new float4(0f, 1f, -8.141123E-08f, 6f),
					new float4(1f, 0f, 0f, -5f),
					new float4(0.7071067f, 0f, 0.7071068f, -4.949747f),
					new float4(0f, 0f, 1f, -4.99999952f),
					new float4(-0.7071067f, 0f, 0.7071068f, -4.949747f),
					new float4(-1f, 0f, 0f, -5f),
					new float4(-0.7071067f, 0f, -0.7071068f, -4.949747f),
					new float4(0f, 0f, -1f, -4.99999952f),
					new float4(0.7071067f, 0f, -0.7071068f, -4.949747f),
				}, contents: 0, operation: CSGOperationType.Subtractive,
					localToTree: new float4x4(new float4(-1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, -1f, 0f), new float4(0f, 5f, -30f, 1f)), name: "Extruded Shape (Remove) #274"))
				;
			ContentsHarvest.AssertMatchesOracle(scene, "sample scene Model2 finding 5");
		}

		[Test]
		public void ExtraFaceOnBoxTop_WithAddBox1AndAddExtrudedShapeAdditiveAndSubExtrudedShapeRemove_Model2F07()
		{
			var scene = new ContentsScene()
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(-1f, 0f, 0f, -8f),
					new float4(0f, -1f, 0f, -1.5f),
					new float4(0f, 0f, -1f, -2f),
					new float4(1f, 0f, 0f, -8f),
					new float4(0f, 1f, 0f, 0.5f),
					new float4(0f, 0f, 1f, -2f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(-1.00000048f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, -1.00000048f, 0f), new float4(0f, -0.5f, -32f, 1f)), name: "Box #174"))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 1f, 0f, 0.5f),
					new float4(0f, -1f, 0f, -8.5f),
					new float4(-0.707106769f, 1.95982852E-06f, 0.707106769f, -4.24263954f),
					new float4(0.707106769f, 1.95982852E-06f, 0.707106769f, -4.24263954f),
					new float4(0f, -2.771616E-06f, -1f, -2.00000143f),
					new float4(0f, 2.771616E-06f, 1f, -1.99999857f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(-1.00000048f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, -1.00000048f, 0f), new float4(0f, -0.5f, -36f, 1f)), name: "Box (1) #175"))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, -1f, 7.40718E-08f, -7f),
					new float4(0f, 1f, -7.40718E-08f, 6f),
					new float4(1f, 0f, 0f, -5.5f),
					new float4(0.707106769f, 0f, 0.7071068f, -5.656854f),
					new float4(0f, 0f, 1f, -5.49999952f),
					new float4(-0.707106769f, 0f, 0.7071068f, -5.656854f),
					new float4(-1f, 0f, 0f, -5.5f),
					new float4(-0.707106769f, 0f, -0.7071068f, -5.656854f),
					new float4(0f, 0f, -1f, -5.49999952f),
					new float4(0.707106769f, 0f, -0.7071068f, -5.656854f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(-1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, -1f, 0f), new float4(0f, 5f, -30f, 1f)), name: "Extruded Shape (Additive) #240"))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, -1f, 8.141123E-08f, -7f),
					new float4(0f, 1f, -8.141123E-08f, 6f),
					new float4(1f, 0f, 0f, -5f),
					new float4(0.7071067f, 0f, 0.7071068f, -4.949747f),
					new float4(0f, 0f, 1f, -4.99999952f),
					new float4(-0.7071067f, 0f, 0.7071068f, -4.949747f),
					new float4(-1f, 0f, 0f, -5f),
					new float4(-0.7071067f, 0f, -0.7071068f, -4.949747f),
					new float4(0f, 0f, -1f, -4.99999952f),
					new float4(0.7071067f, 0f, -0.7071068f, -4.949747f),
				}, contents: 0, operation: CSGOperationType.Subtractive,
					localToTree: new float4x4(new float4(-1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, -1f, 0f), new float4(0f, 5f, -30f, 1f)), name: "Extruded Shape (Remove) #274"))
				;
			ContentsHarvest.AssertMatchesOracle(scene, "sample scene Model2 finding 7");
		}
	}
}
