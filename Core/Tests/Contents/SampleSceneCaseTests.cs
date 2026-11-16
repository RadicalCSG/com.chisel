using NUnit.Framework;
using Unity.Mathematics;

namespace Chisel.Core.Tests
{
	// Spots where the exact judge disagreed with the mesh in the sample scene (Assets/ChiselChecks/Sample), each shrunk by the
	// harvester to the brushes that still show it, and copied here as the scene has them
	[TestFixture]
	[Category("Contents")]
	public class SampleSceneCaseTests
	{
		// Near (8, 6.75, -4.083): the exact CSG makes a triangle the mesh does not have
		static ContentsScene Model0Site1()
		{
			return new ContentsScene()
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 1f, 0f, -1f),
					new float4(0f, -1f, 0f, 0f),
					new float4(-1f, 0f, 0f, -0.499999523f),
					new float4(1f, 0f, 0f, -0.499999523f),
					new float4(0f, 0f, -1f, -1f),
					new float4(0f, 0f, 1f, -1f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(-1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, -1f, 0f), new float4(8.5f, 5.75f, -4f, 1f)), name: "Box #46")
					.WithMesh(new float3[] { new float3(-0.499999523f, 1f, -1f), new float3(0.499999523f, 1f, -1f), new float3(0.499999523f, 1f, 1f), new float3(-0.499999523f, 1f, 1f), new float3(-0.499999523f, 0f, -1f), new float3(0.499999523f, 0f, -1f), new float3(0.499999523f, 0f, 1f), new float3(-0.499999523f, 0f, 1f) }, new int[] { 0, 3, 2, 1, -1, 4, 5, 6, 7, -1, 0, 4, 7, 3, -1, 1, 2, 6, 5, -1, 0, 1, 5, 4, -1, 3, 7, 6, 2, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(-0.8944272f, 0.4472136f, 0f, -2.01246119f),
					new float4(0f, -1f, 0f, 0f),
					new float4(-0.832050145f, -0.5547004f, 0f, -0.416024685f),
					new float4(0.857493f, 0.5144956f, 0f, -0.4287461f),
					new float4(0f, 0f, -1f, -1f),
					new float4(0f, 0f, 1f, -1f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, -1f, 0f), new float4(8.5f, 6.75f, -4f, 1f)), name: "Box (11) #48")
					.WithMesh(new float3[] { new float3(-1.5f, 1.5f, -1f), new float3(-1f, 2.5f, -1f), new float3(-1f, 2.5f, 1f), new float3(-1.5f, 1.5f, 1f), new float3(-0.499999523f, 0f, -1f), new float3(0.499999523f, 0f, -1f), new float3(0.499999523f, 0f, 1f), new float3(-0.499999523f, 0f, 1f) }, new int[] { 0, 3, 2, 1, -1, 4, 5, 6, 7, -1, 0, 4, 7, 3, -1, 1, 2, 6, 5, -1, 0, 1, 5, 4, -1, 3, 7, 6, 2, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(-1f, 0f, 0f, -0.25f),
					new float4(0f, -1f, 0f, 0f),
					new float4(0f, 0f, -1f, -0.5f),
					new float4(1f, 0f, 0f, -0.25f),
					new float4(0f, 1f, 0f, -4f),
					new float4(0f, 0f, 1f, -0.5f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(-1f, 0f, 0f, 0f), new float4(0f, 1f, -1.685874E-07f, 0f), new float4(0f, -1.685874E-07f, -1f, 0f), new float4(7.75f, 3.75f, -4f, 1f)), name: "Box (27) #127")
					.WithMesh(new float3[] { new float3(-0.25f, 0f, 0.5f), new float3(-0.25f, 0f, -0.5f), new float3(0.25f, 0f, -0.5f), new float3(0.25f, 0f, 0.5f), new float3(-0.25f, 4f, 0.5f), new float3(-0.25f, 4f, -0.5f), new float3(0.25f, 4f, -0.5f), new float3(0.25f, 4f, 0.5f) }, new int[] { 1, 0, 4, 5, -1, 3, 0, 1, 2, -1, 6, 2, 1, 5, -1, 3, 2, 6, 7, -1, 4, 7, 6, 5, -1, 4, 0, 3, 7, -1 }))
				;
		}

		// Near (-1.333, -8.5, -39.556): the exact CSG makes a triangle the mesh does not have; 1 subtractive brush(es)
		static ContentsScene Model1Site1()
		{
			return new ContentsScene()
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 1f, 0f, 0.5f),
					new float4(0f, -1f, 0f, -8.5f),
					new float4(-0.707106769f, 1.95982852E-06f, 0.707106769f, -4.24263954f),
					new float4(0.707106769f, 1.95982852E-06f, 0.707106769f, -4.24263954f),
					new float4(0f, -2.771616E-06f, -1f, -2.00000143f),
					new float4(0f, 2.771616E-06f, 1f, -1.99999857f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(-1.00000048f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, -1.00000048f, 0f), new float4(0f, -0.5f, -35.99999f, 1f)), name: "Box (1) #178")
					.WithMesh(new float3[] { new float3(-8f, -0.5f, -2f), new float3(8f, -0.5f, -2f), new float3(4f, -0.5f, 2f), new float3(-4f, -0.5f, 2f), new float3(-8f, -8.5f, -1.99997783f), new float3(8f, -8.5f, -1.99997783f), new float3(4f, -8.5f, 2.00002217f), new float3(-4f, -8.5f, 2.00002217f) }, new int[] { 0, 3, 2, 1, -1, 4, 5, 6, 7, -1, 0, 4, 7, 3, -1, 1, 2, 6, 5, -1, 0, 1, 5, 4, -1, 3, 7, 6, 2, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, -1f, 5.960465E-08f, 0f),
					new float4(0f, 1f, -5.960465E-08f, -0.5f),
					new float4(-1f, 0f, 0f, -1.5f),
					new float4(0.8944272f, 0f, -0.44721362f, 0f),
					new float4(0f, 0f, 1f, -2.99999976f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(-1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, -1f, 0f), new float4(-5.5f, -8.5f, -41f, 1f)), name: "Extruded Shape #207")
					.WithMesh(new float3[] { new float3(1.5f, 1.78813934E-07f, 2.99999976f), new float3(-1.5f, 1.78813934E-07f, 2.99999976f), new float3(-1.5f, -1.78813934E-07f, -2.99999976f), new float3(1.5f, 0.5000002f, 2.99999976f), new float3(-1.5f, 0.5000002f, 2.99999976f), new float3(-1.5f, 0.499999821f, -2.99999976f) }, new int[] { 0, 1, 2, -1, 4, 3, 5, -1, 1, 4, 5, 2, -1, 2, 5, 3, 0, -1, 0, 3, 4, 1, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, -1f, 5.960465E-08f, 0f),
					new float4(0f, 1f, -5.960465E-08f, -0.5f),
					new float4(-1f, 0f, 0f, -1.5f),
					new float4(0.8944272f, 0f, -0.44721362f, 0f),
					new float4(0f, 0f, 1f, -2.99999976f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, -1f, 0f), new float4(5.5f, -8.5f, -41f, 1f)), name: "Extruded Shape #230")
					.WithMesh(new float3[] { new float3(1.5f, 1.78813934E-07f, 2.99999976f), new float3(-1.5f, 1.78813934E-07f, 2.99999976f), new float3(-1.5f, -1.78813934E-07f, -2.99999976f), new float3(1.5f, 0.5000002f, 2.99999976f), new float3(-1.5f, 0.5000002f, 2.99999976f), new float3(-1.5f, 0.499999821f, -2.99999976f) }, new int[] { 0, 1, 2, -1, 4, 3, 5, -1, 1, 4, 5, 2, -1, 2, 5, 3, 0, -1, 0, 3, 4, 1, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(-1f, 0f, 0f, -4f),
					new float4(0f, -1f, 0f, 0f),
					new float4(0f, 0f, -1f, -0.25f),
					new float4(1f, 0f, 0f, -4f),
					new float4(0f, 1f, 0f, -5.99997473f),
					new float4(0f, 0f, 1f, -0.25f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 1.0658141E-14f, 5.96046448E-08f, 0f), new float4(5.960465E-08f, 2.563E-06f, -1f, 0f), new float4(-1.59872129E-13f, 1f, 2.563E-06f, 0f), new float4(1.3997693E-12f, -8.749996f, -38.0000229f, 1f)), name: "Box #231")
					.WithMesh(new float3[] { new float3(-4f, 0f, 0.25f), new float3(-4f, 0f, -0.25f), new float3(4f, 0f, -0.25f), new float3(4f, 0f, 0.25f), new float3(-4f, 5.99997473f, 0.25f), new float3(-4f, 5.99997473f, -0.25f), new float3(4f, 5.99997473f, -0.25f), new float3(4f, 5.99997473f, 0.25f) }, new int[] { 1, 0, 4, 5, -1, 3, 0, 1, 2, -1, 6, 2, 1, 5, -1, 3, 2, 6, 7, -1, 4, 7, 6, 5, -1, 4, 0, 3, 7, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(-1f, 0f, 0f, -2f),
					new float4(0f, -1f, 0f, -4f),
					new float4(0f, 0f, -1f, -1.99999988f),
					new float4(1f, 0f, 0f, -2f),
					new float4(0f, 1f, 0f, 0f),
					new float4(0f, 0f, 1f, -1.99999988f),
				}, contents: 0, operation: CSGOperationType.Subtractive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, -1.192093E-07f, -1.00000024f, 0f), new float4(0f, 1.00000024f, -1.192093E-07f, 0f), new float4(0f, -6.5f, -38.0001f, 1f)), name: "Box #239")
					.WithMesh(new float3[] { new float3(-2f, -4f, 1.99999988f), new float3(-2f, -4f, -1.99999988f), new float3(2f, -4f, -1.99999988f), new float3(2f, -4f, 1.99999988f), new float3(-2f, 0f, 1.99999988f), new float3(-2f, 0f, -1.99999988f), new float3(2f, 0f, -1.99999988f), new float3(2f, 0f, 1.99999988f) }, new int[] { 1, 0, 4, 5, -1, 3, 0, 1, 2, -1, 6, 2, 1, 5, -1, 3, 2, 6, 7, -1, 4, 7, 6, 5, -1, 4, 0, 3, 7, -1 }))
				;
		}

		// Near (3.333, -8.5, -39.556): the mesh has a triangle the exact CSG does not make; 1 subtractive brush(es)
		static ContentsScene Model1Site2()
		{
			return new ContentsScene()
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 1f, 0f, 0.5f),
					new float4(0f, -1f, 0f, -8.5f),
					new float4(-0.707106769f, 1.95982852E-06f, 0.707106769f, -4.24263954f),
					new float4(0.707106769f, 1.95982852E-06f, 0.707106769f, -4.24263954f),
					new float4(0f, -2.771616E-06f, -1f, -2.00000143f),
					new float4(0f, 2.771616E-06f, 1f, -1.99999857f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(-1.00000048f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, -1.00000048f, 0f), new float4(0f, -0.5f, -35.99999f, 1f)), name: "Box (1) #178")
					.WithMesh(new float3[] { new float3(-8f, -0.5f, -2f), new float3(8f, -0.5f, -2f), new float3(4f, -0.5f, 2f), new float3(-4f, -0.5f, 2f), new float3(-8f, -8.5f, -1.99997783f), new float3(8f, -8.5f, -1.99997783f), new float3(4f, -8.5f, 2.00002217f), new float3(-4f, -8.5f, 2.00002217f) }, new int[] { 0, 3, 2, 1, -1, 4, 5, 6, 7, -1, 0, 4, 7, 3, -1, 1, 2, 6, 5, -1, 0, 1, 5, 4, -1, 3, 7, 6, 2, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, -1f, 5.960465E-08f, 0f),
					new float4(0f, 1f, -5.960465E-08f, -0.5f),
					new float4(-1f, 0f, 0f, -1.5f),
					new float4(0.8944272f, 0f, -0.44721362f, 0f),
					new float4(0f, 0f, 1f, -2.99999976f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(-1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, -1f, 0f), new float4(-5.5f, -8.5f, -41f, 1f)), name: "Extruded Shape #207")
					.WithMesh(new float3[] { new float3(1.5f, 1.78813934E-07f, 2.99999976f), new float3(-1.5f, 1.78813934E-07f, 2.99999976f), new float3(-1.5f, -1.78813934E-07f, -2.99999976f), new float3(1.5f, 0.5000002f, 2.99999976f), new float3(-1.5f, 0.5000002f, 2.99999976f), new float3(-1.5f, 0.499999821f, -2.99999976f) }, new int[] { 0, 1, 2, -1, 4, 3, 5, -1, 1, 4, 5, 2, -1, 2, 5, 3, 0, -1, 0, 3, 4, 1, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, -1f, 5.960465E-08f, 0f),
					new float4(0f, 1f, -5.960465E-08f, -0.5f),
					new float4(-1f, 0f, 0f, -1.5f),
					new float4(0.8944272f, 0f, -0.44721362f, 0f),
					new float4(0f, 0f, 1f, -2.99999976f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, -1f, 0f), new float4(5.5f, -8.5f, -41f, 1f)), name: "Extruded Shape #230")
					.WithMesh(new float3[] { new float3(1.5f, 1.78813934E-07f, 2.99999976f), new float3(-1.5f, 1.78813934E-07f, 2.99999976f), new float3(-1.5f, -1.78813934E-07f, -2.99999976f), new float3(1.5f, 0.5000002f, 2.99999976f), new float3(-1.5f, 0.5000002f, 2.99999976f), new float3(-1.5f, 0.499999821f, -2.99999976f) }, new int[] { 0, 1, 2, -1, 4, 3, 5, -1, 1, 4, 5, 2, -1, 2, 5, 3, 0, -1, 0, 3, 4, 1, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(-1f, 0f, 0f, -4f),
					new float4(0f, -1f, 0f, 0f),
					new float4(0f, 0f, -1f, -0.25f),
					new float4(1f, 0f, 0f, -4f),
					new float4(0f, 1f, 0f, -5.99997473f),
					new float4(0f, 0f, 1f, -0.25f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 1.0658141E-14f, 5.96046448E-08f, 0f), new float4(5.960465E-08f, 2.563E-06f, -1f, 0f), new float4(-1.59872129E-13f, 1f, 2.563E-06f, 0f), new float4(1.3997693E-12f, -8.749996f, -38.0000229f, 1f)), name: "Box #231")
					.WithMesh(new float3[] { new float3(-4f, 0f, 0.25f), new float3(-4f, 0f, -0.25f), new float3(4f, 0f, -0.25f), new float3(4f, 0f, 0.25f), new float3(-4f, 5.99997473f, 0.25f), new float3(-4f, 5.99997473f, -0.25f), new float3(4f, 5.99997473f, -0.25f), new float3(4f, 5.99997473f, 0.25f) }, new int[] { 1, 0, 4, 5, -1, 3, 0, 1, 2, -1, 6, 2, 1, 5, -1, 3, 2, 6, 7, -1, 4, 7, 6, 5, -1, 4, 0, 3, 7, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 1f, 0f, -3.00000477f),
					new float4(0.7071421f, -0.7070714f, 0f, 0.353571057f),
					new float4(-1f, 0f, 0f, -0.5f),
					new float4(1f, 0f, 0f, -0.5f),
					new float4(0f, 0f, -1f, -4f),
					new float4(0f, 0f, 1f, -4f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(-2.861023E-06f, 0f, -1f, 0f), new float4(-1f, 0f, 2.861023E-06f, 0f), new float4(0f, 1f, 0f, 0f), new float4(-4f, -5f, -44.5f, 1f)), name: "Box (4) #236")
					.WithMesh(new float3[] { new float3(-0.5f, 3.00000477f, -4f), new float3(0.5f, 3.00000477f, -4f), new float3(0.5f, 3.00000477f, 4f), new float3(-0.5f, 3.00000477f, 4f), new float3(-0.5f, 0f, -4f), new float3(0.5f, 1.0001f, -4f), new float3(0.5f, 1.0001f, 4f), new float3(-0.5f, 0f, 4f) }, new int[] { 0, 3, 2, 1, -1, 4, 5, 6, 7, -1, 0, 4, 7, 3, -1, 1, 2, 6, 5, -1, 0, 1, 5, 4, -1, 3, 7, 6, 2, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(-1f, 0f, 0f, -2f),
					new float4(0f, -1f, 0f, -4f),
					new float4(0f, 0f, -1f, -1.99999988f),
					new float4(1f, 0f, 0f, -2f),
					new float4(0f, 1f, 0f, 0f),
					new float4(0f, 0f, 1f, -1.99999988f),
				}, contents: 0, operation: CSGOperationType.Subtractive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, -1.192093E-07f, -1.00000024f, 0f), new float4(0f, 1.00000024f, -1.192093E-07f, 0f), new float4(0f, -6.5f, -38.0001f, 1f)), name: "Box #239")
					.WithMesh(new float3[] { new float3(-2f, -4f, 1.99999988f), new float3(-2f, -4f, -1.99999988f), new float3(2f, -4f, -1.99999988f), new float3(2f, -4f, 1.99999988f), new float3(-2f, 0f, 1.99999988f), new float3(-2f, 0f, -1.99999988f), new float3(2f, 0f, -1.99999988f), new float3(2f, 0f, 1.99999988f) }, new int[] { 1, 0, 4, 5, -1, 3, 0, 1, 2, -1, 6, 2, 1, 5, -1, 3, 2, 6, 7, -1, 4, 7, 6, 5, -1, 4, 0, 3, 7, -1 }))
				;
		}

		// Near (8, -2, -26.5): the mesh has a triangle the exact CSG does not make
		static ContentsScene Model1Site3()
		{
			return new ContentsScene()
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(-1.45915264E-15f, 1f, -2.12873918E-09f, 1.06436939E-08f),
					new float4(0f, -1f, 0f, -1f),
					new float4(-1f, 4.4822762E-05f, -1.53269343E-06f, -7.99999142f),
					new float4(1f, -4.43458557E-05f, 4.72003418E-14f, -7.99999952f),
					new float4(-1.37090876E-06f, 4.386908E-05f, -1f, -8.999989f),
					new float4(0f, -4.386902E-05f, 1f, -4.999999f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(-1.00000048f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, -1.00000048f, 0f), new float4(-1.36423772E-12f, -1f, -25f, 1f)), name: "Box #176")
					.WithMesh(new float3[] { new float3(-7.99997759f, -2.98023224E-08f, -8.999978f), new float3(7.99999952f, -2.98023224E-08f, -9f), new float3(7.99999952f, 0f, 4.999999f), new float3(-7.99999952f, 0f, 4.999999f), new float3(-8.000023f, -1f, -9.000022f), new float3(7.999955f, -1f, -9.000044f), new float3(7.999955f, -1f, 4.999955f), new float3(-8.000044f, -1f, 4.999955f) }, new int[] { 0, 3, 2, 1, -1, 4, 5, 6, 7, -1, 0, 4, 7, 3, -1, 1, 2, 6, 5, -1, 0, 1, 5, 4, -1, 3, 7, 6, 2, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(-1f, 0f, 0f, -0.25f),
					new float4(0f, -1f, 0f, 0f),
					new float4(0f, 0f, -1f, -3.75f),
					new float4(1f, 0f, 0f, -0.25f),
					new float4(0f, 1f, 0f, -8f),
					new float4(0f, 0f, 1f, -3.75f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(-1.00000024f, -8.881786E-16f, -7.450583E-09f, 0f), new float4(-7.450582E-09f, -2.50339531E-06f, 1f, 0f), new float4(-1.95399269E-14f, 1f, 2.563E-06f, 0f), new float4(7.750002f, -5.249997f, -34.0000076f, 1f)), name: "Box #180")
					.WithMesh(new float3[] { new float3(-0.25f, 0f, 3.75f), new float3(-0.25f, 0f, -3.75f), new float3(0.25f, 0f, -3.75f), new float3(0.25f, 0f, 3.75f), new float3(-0.25f, 8f, 3.75f), new float3(-0.25f, 8f, -3.75f), new float3(0.25f, 8f, -3.75f), new float3(0.25f, 8f, 3.75f) }, new int[] { 1, 0, 4, 5, -1, 3, 0, 1, 2, -1, 6, 2, 1, 5, -1, 3, 2, 6, 7, -1, 4, 7, 6, 5, -1, 4, 0, 3, 7, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(-1f, 0f, 0f, -0.5f),
					new float4(0f, -1f, 0f, 0f),
					new float4(0f, 0f, -1f, -0.25f),
					new float4(1f, 0f, 0f, -0.5f),
					new float4(0f, 1f, 0f, -6.500032f),
					new float4(0f, 0f, 1f, -0.25f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(0f, -2.682209E-07f, -1.00000012f, 0f), new float4(1.00000012f, -8.738041E-05f, -5.96046448E-08f, 0f), new float4(8.72612E-05f, 1f, -4.76837158E-07f, 0f), new float4(7.999899f, -2.25000143f, -29.5f, 1f)), name: "Box (4) #210")
					.WithMesh(new float3[] { new float3(-0.5f, 0f, 0.25f), new float3(-0.5f, 0f, -0.25f), new float3(0.5f, 0f, -0.25f), new float3(0.5f, 0f, 0.25f), new float3(-0.5f, 6.500032f, 0.25f), new float3(-0.5f, 6.500032f, -0.25f), new float3(0.5f, 6.500032f, -0.25f), new float3(0.5f, 6.500032f, 0.25f) }, new int[] { 1, 0, 4, 5, -1, 3, 0, 1, 2, -1, 6, 2, 1, 5, -1, 3, 2, 6, 7, -1, 4, 7, 6, 5, -1, 4, 0, 3, 7, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(-1f, 0f, 0f, -12f),
					new float4(0f, -1f, 0f, 0f),
					new float4(0f, 0f, -1f, -3.5f),
					new float4(1f, 0f, 0f, -4f),
					new float4(0f, 1f, 0f, -1.5f),
					new float4(0f, 0f, 1f, -3.5f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(-1.00000024f, -1.77635726E-15f, -7.450583E-09f, 0f), new float4(-7.450582E-09f, -2.384186E-06f, 1f, 0f), new float4(-1.95399269E-14f, 1f, 2.384186E-06f, 0f), new float4(-4.000001f, -5.49999762f, -25.9999981f, 1f)), name: "Box #241")
					.WithMesh(new float3[] { new float3(-12f, 0f, 3.5f), new float3(-12f, 0f, -3.5f), new float3(4f, 0f, -3.5f), new float3(4f, 0f, 3.5f), new float3(-12f, 1.5f, 3.5f), new float3(-12f, 1.5f, -3.5f), new float3(4f, 1.5f, -3.5f), new float3(4f, 1.5f, 3.5f) }, new int[] { 1, 0, 4, 5, -1, 3, 0, 1, 2, -1, 6, 2, 1, 5, -1, 3, 2, 6, 7, -1, 4, 7, 6, 5, -1, 4, 0, 3, 7, -1 }))
				;
		}

		// Near (8, -2.167, -28.167): the exact CSG makes a triangle the mesh does not have
		internal static ContentsScene Model1Site4()
		{
			return new ContentsScene()
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(-1.45915264E-15f, 1f, -2.12873918E-09f, 1.06436939E-08f),
					new float4(0f, -1f, 0f, -1f),
					new float4(-1f, 4.4822762E-05f, -1.53269343E-06f, -7.99999142f),
					new float4(1f, -4.43458557E-05f, 4.72003418E-14f, -7.99999952f),
					new float4(-1.37090876E-06f, 4.386908E-05f, -1f, -8.999989f),
					new float4(0f, -4.386902E-05f, 1f, -4.999999f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(-1.00000048f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, -1.00000048f, 0f), new float4(-1.36423772E-12f, -1f, -25f, 1f)), name: "Box #176")
					.WithMesh(new float3[] { new float3(-7.99997759f, -2.98023224E-08f, -8.999978f), new float3(7.99999952f, -2.98023224E-08f, -9f), new float3(7.99999952f, 0f, 4.999999f), new float3(-7.99999952f, 0f, 4.999999f), new float3(-8.000023f, -1f, -9.000022f), new float3(7.999955f, -1f, -9.000044f), new float3(7.999955f, -1f, 4.999955f), new float3(-8.000044f, -1f, 4.999955f) }, new int[] { 0, 3, 2, 1, -1, 4, 5, 6, 7, -1, 0, 4, 7, 3, -1, 1, 2, 6, 5, -1, 0, 1, 5, 4, -1, 3, 7, 6, 2, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(-1f, 0f, 0f, -0.25f),
					new float4(0f, -1f, 0f, 0f),
					new float4(0f, 0f, -1f, -3.75f),
					new float4(1f, 0f, 0f, -0.25f),
					new float4(0f, 1f, 0f, -8f),
					new float4(0f, 0f, 1f, -3.75f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(-1.00000024f, -8.881786E-16f, -7.450583E-09f, 0f), new float4(-7.450582E-09f, -2.50339531E-06f, 1f, 0f), new float4(-1.95399269E-14f, 1f, 2.563E-06f, 0f), new float4(7.750002f, -5.249997f, -34.0000076f, 1f)), name: "Box #180")
					.WithMesh(new float3[] { new float3(-0.25f, 0f, 3.75f), new float3(-0.25f, 0f, -3.75f), new float3(0.25f, 0f, -3.75f), new float3(0.25f, 0f, 3.75f), new float3(-0.25f, 8f, 3.75f), new float3(-0.25f, 8f, -3.75f), new float3(0.25f, 8f, -3.75f), new float3(0.25f, 8f, 3.75f) }, new int[] { 1, 0, 4, 5, -1, 3, 0, 1, 2, -1, 6, 2, 1, 5, -1, 3, 2, 6, 7, -1, 4, 7, 6, 5, -1, 4, 0, 3, 7, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(-1f, 0f, 0f, -0.5f),
					new float4(0f, -1f, 0f, 0f),
					new float4(0f, 0f, -1f, -0.25f),
					new float4(1f, 0f, 0f, -0.5f),
					new float4(0f, 1f, 0f, -6.500032f),
					new float4(0f, 0f, 1f, -0.25f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(0f, -2.682209E-07f, -1.00000012f, 0f), new float4(1.00000012f, -8.738041E-05f, -5.96046448E-08f, 0f), new float4(8.72612E-05f, 1f, -4.76837158E-07f, 0f), new float4(7.999899f, -2.25000143f, -29.5f, 1f)), name: "Box (4) #210")
					.WithMesh(new float3[] { new float3(-0.5f, 0f, 0.25f), new float3(-0.5f, 0f, -0.25f), new float3(0.5f, 0f, -0.25f), new float3(0.5f, 0f, 0.25f), new float3(-0.5f, 6.500032f, 0.25f), new float3(-0.5f, 6.500032f, -0.25f), new float3(0.5f, 6.500032f, -0.25f), new float3(0.5f, 6.500032f, 0.25f) }, new int[] { 1, 0, 4, 5, -1, 3, 0, 1, 2, -1, 6, 2, 1, 5, -1, 3, 2, 6, 7, -1, 4, 7, 6, 5, -1, 4, 0, 3, 7, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(-1f, 0f, 0f, -0.5f),
					new float4(0f, -1f, 0f, 0f),
					new float4(0f, 0f, -1f, -0.25f),
					new float4(1f, 0f, 0f, -0.5f),
					new float4(0f, 1f, 0f, -6.500032f),
					new float4(0f, 0f, 1f, -0.25f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(0f, -2.682209E-07f, -1.00000012f, 0f), new float4(1.00000012f, -8.738041E-05f, -5.96046448E-08f, 0f), new float4(8.72612E-05f, 1f, -4.76837158E-07f, 0f), new float4(7.99994373f, -1.75000167f, -28.5000076f, 1f)), name: "Box #212")
					.WithMesh(new float3[] { new float3(-0.5f, 0f, 0.25f), new float3(-0.5f, 0f, -0.25f), new float3(0.5f, 0f, -0.25f), new float3(0.5f, 0f, 0.25f), new float3(-0.5f, 6.500032f, 0.25f), new float3(-0.5f, 6.500032f, -0.25f), new float3(0.5f, 6.500032f, -0.25f), new float3(0.5f, 6.500032f, 0.25f) }, new int[] { 1, 0, 4, 5, -1, 3, 0, 1, 2, -1, 6, 2, 1, 5, -1, 3, 2, 6, 7, -1, 4, 7, 6, 5, -1, 4, 0, 3, 7, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(-1f, 0f, 0f, -12f),
					new float4(0f, -1f, 0f, 0f),
					new float4(0f, 0f, -1f, -3.5f),
					new float4(1f, 0f, 0f, -4f),
					new float4(0f, 1f, 0f, -1.5f),
					new float4(0f, 0f, 1f, -3.5f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(-1.00000024f, -1.77635726E-15f, -7.450583E-09f, 0f), new float4(-7.450582E-09f, -2.384186E-06f, 1f, 0f), new float4(-1.95399269E-14f, 1f, 2.384186E-06f, 0f), new float4(-4.000001f, -5.49999762f, -25.9999981f, 1f)), name: "Box #241")
					.WithMesh(new float3[] { new float3(-12f, 0f, 3.5f), new float3(-12f, 0f, -3.5f), new float3(4f, 0f, -3.5f), new float3(4f, 0f, 3.5f), new float3(-12f, 1.5f, 3.5f), new float3(-12f, 1.5f, -3.5f), new float3(4f, 1.5f, -3.5f), new float3(4f, 1.5f, 3.5f) }, new int[] { 1, 0, 4, 5, -1, 3, 0, 1, 2, -1, 6, 2, 1, 5, -1, 3, 2, 6, 7, -1, 4, 7, 6, 5, -1, 4, 0, 3, 7, -1 }))
				;
		}

		// Near (-13.833, 0.417, -5): the exact CSG makes a triangle the mesh does not have
		static ContentsScene Model3Site1()
		{
			return new ContentsScene()
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 1f, 0f, -3f),
					new float4(0f, -1f, 0f, 0f),
					new float4(-0.6f, 0.8f, 0f, -3.6f),
					new float4(0.6f, 0.8f, 0f, -3.6f),
					new float4(0f, 0f, -1f, -0.25f),
					new float4(0f, 0f, 1f, -0.25f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, -1.1920929E-07f, -1.00000012f, 0f), new float4(0f, 1.00000012f, -1.1920929E-07f, 0f), new float4(-18.5f, 0.5f, -4f, 1f)), name: "Box 1 #400")
					.WithMesh(new float3[] { new float3(-2f, 3f, -0.25f), new float3(2f, 3f, -0.25f), new float3(2f, 3f, 0.25f), new float3(-2f, 3f, 0.25f), new float3(-6f, 0f, -0.25f), new float3(6f, 0f, -0.25f), new float3(6f, 0f, 0.25f), new float3(-6f, 0f, 0.25f) }, new int[] { 0, 3, 2, 1, -1, 4, 5, 6, 7, -1, 0, 4, 7, 3, -1, 1, 2, 6, 5, -1, 0, 1, 5, 4, -1, 3, 7, 6, 2, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(-1f, 0f, 0f, -9.5f),
					new float4(0f, -1f, 0f, 0f),
					new float4(0f, 0f, -1f, -13f),
					new float4(1f, 0f, 0f, -2.5f),
					new float4(0f, 1f, 0f, -0.5f),
					new float4(0f, 0f, 1f, -3f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(-1.00000048f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, -1.00000048f, 0f), new float4(-22.0000057f, 0.25f, -1.00000048f, 1f)), name: "Box13 #412")
					.WithMesh(new float3[] { new float3(-9.5f, 0f, 3f), new float3(-9.5f, 0f, -13f), new float3(2.5f, 0f, -13f), new float3(2.5f, 0f, 3f), new float3(-9.5f, 0.5f, 3f), new float3(-9.5f, 0.5f, -13f), new float3(2.5f, 0.5f, -13f), new float3(2.5f, 0.5f, 3f) }, new int[] { 1, 0, 4, 5, -1, 3, 0, 1, 2, -1, 6, 2, 1, 5, -1, 3, 2, 6, 7, -1, 4, 7, 6, 5, -1, 4, 0, 3, 7, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(-1f, 0f, 0f, -0.125f),
					new float4(0f, -1f, 0f, 0f),
					new float4(0f, 0f, -1f, -1.75f),
					new float4(1f, 0f, 0f, -0.375f),
					new float4(0f, 1f, 0f, -0.25f),
					new float4(0f, 0f, 1f, -14.25f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(-1.00000048f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, -1.00000048f, 0f), new float4(-12.625f, 0.5f, 10.25f, 1f)), name: "Box23 #422")
					.WithMesh(new float3[] { new float3(-0.125f, 0f, 14.25f), new float3(-0.125f, 0f, -1.75f), new float3(0.375f, 0f, -1.75f), new float3(0.375f, 0f, 14.25f), new float3(-0.125f, 0.25f, 14.25f), new float3(-0.125f, 0.25f, -1.75f), new float3(0.375f, 0.25f, -1.75f), new float3(0.375f, 0.25f, 14.25f) }, new int[] { 1, 0, 4, 5, -1, 3, 0, 1, 2, -1, 6, 2, 1, 5, -1, 3, 2, 6, 7, -1, 4, 7, 6, 5, -1, 4, 0, 3, 7, -1 }))
				;
		}

		// Near (-23.167, 0.583, -5): the exact CSG makes a triangle the mesh does not have
		static ContentsScene Model3Site2()
		{
			return new ContentsScene()
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 1f, 0f, -3f),
					new float4(0f, -1f, 0f, 0f),
					new float4(-0.6f, 0.8f, 0f, -3.6f),
					new float4(0.6f, 0.8f, 0f, -3.6f),
					new float4(0f, 0f, -1f, -0.25f),
					new float4(0f, 0f, 1f, -0.25f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, -1.1920929E-07f, -1.00000012f, 0f), new float4(0f, 1.00000012f, -1.1920929E-07f, 0f), new float4(-18.5f, 0.5f, -4f, 1f)), name: "Box 1 #400")
					.WithMesh(new float3[] { new float3(-2f, 3f, -0.25f), new float3(2f, 3f, -0.25f), new float3(2f, 3f, 0.25f), new float3(-2f, 3f, 0.25f), new float3(-6f, 0f, -0.25f), new float3(6f, 0f, -0.25f), new float3(6f, 0f, 0.25f), new float3(-6f, 0f, 0.25f) }, new int[] { 0, 3, 2, 1, -1, 4, 5, 6, 7, -1, 0, 4, 7, 3, -1, 1, 2, 6, 5, -1, 0, 1, 5, 4, -1, 3, 7, 6, 2, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(-1f, 0f, 0f, -9.5f),
					new float4(0f, -1f, 0f, 0f),
					new float4(0f, 0f, -1f, -13f),
					new float4(1f, 0f, 0f, -2.5f),
					new float4(0f, 1f, 0f, -0.5f),
					new float4(0f, 0f, 1f, -3f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(-1.00000048f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, -1.00000048f, 0f), new float4(-22.0000057f, 0.25f, -1.00000048f, 1f)), name: "Box13 #412")
					.WithMesh(new float3[] { new float3(-9.5f, 0f, 3f), new float3(-9.5f, 0f, -13f), new float3(2.5f, 0f, -13f), new float3(2.5f, 0f, 3f), new float3(-9.5f, 0.5f, 3f), new float3(-9.5f, 0.5f, -13f), new float3(2.5f, 0.5f, -13f), new float3(2.5f, 0.5f, 3f) }, new int[] { 1, 0, 4, 5, -1, 3, 0, 1, 2, -1, 6, 2, 1, 5, -1, 3, 2, 6, 7, -1, 4, 7, 6, 5, -1, 4, 0, 3, 7, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(-1f, 0f, 0f, -0.125f),
					new float4(0f, -1f, 0f, 0f),
					new float4(0f, 0f, -1f, -1.75f),
					new float4(1f, 0f, 0f, -0.375f),
					new float4(0f, 1f, 0f, -0.25f),
					new float4(0f, 0f, 1f, -14.25f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(-1.00000048f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, -1.00000048f, 0f), new float4(-24.125f, 0.5f, 10.25f, 1f)), name: "Box24 #423")
					.WithMesh(new float3[] { new float3(-0.125f, 0f, 14.25f), new float3(-0.125f, 0f, -1.75f), new float3(0.375f, 0f, -1.75f), new float3(0.375f, 0f, 14.25f), new float3(-0.125f, 0.25f, 14.25f), new float3(-0.125f, 0.25f, -1.75f), new float3(0.375f, 0.25f, -1.75f), new float3(0.375f, 0.25f, 14.25f) }, new int[] { 1, 0, 4, 5, -1, 3, 0, 1, 2, -1, 6, 2, 1, 5, -1, 3, 2, 6, 7, -1, 4, 7, 6, 5, -1, 4, 0, 3, 7, -1 }))
				;
		}

		[Test] public void TheModel0Site1_TheMeshIsWhatTheExactCSGMade() => ExactJudgeCaseTests.AssertTheMeshIsWhatTheExactCSGMade(Model0Site1());
		[Test] public void TheModel1Site1_TheMeshIsWhatTheExactCSGMade() => ExactJudgeCaseTests.AssertTheMeshIsWhatTheExactCSGMade(Model1Site1());
		[Test] public void TheModel1Site2_TheMeshIsWhatTheExactCSGMade() => ExactJudgeCaseTests.AssertTheMeshIsWhatTheExactCSGMade(Model1Site2());
		[Test] public void TheModel1Site3_TheMeshIsWhatTheExactCSGMade() => ExactJudgeCaseTests.AssertTheMeshIsWhatTheExactCSGMade(Model1Site3());
		[Test] public void TheModel1Site4_TheMeshIsWhatTheExactCSGMade() => ExactJudgeCaseTests.AssertTheMeshIsWhatTheExactCSGMade(Model1Site4());
		[Test] public void TheModel3Site1_TheMeshIsWhatTheExactCSGMade() => ExactJudgeCaseTests.AssertTheMeshIsWhatTheExactCSGMade(Model3Site1());
		[Test] public void TheModel3Site2_TheMeshIsWhatTheExactCSGMade() => ExactJudgeCaseTests.AssertTheMeshIsWhatTheExactCSGMade(Model3Site2());
	}
}
