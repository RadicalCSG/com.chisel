using NUnit.Framework;
using Unity.Mathematics;

namespace Chisel.Core.Tests
{
	// Spots where the exact judge disagreed with the mesh in the other imported maps, each shrunk by the harvester to the
	// brushes that still show it, and copied here as the map has them
	public partial class ImportedMapCaseTests
	{
		// bm_c1a1e, Model: the mesh draws nothing where the exact CSG has a face; Solid 137420 #256 face 2 near (-23.79, -80,
		// -125.05) (3 brushes)
		static ContentsScene BmC1a1eModel0Site1()
		{
			return new ContentsScene()
				.WithContentsList("Solid", "Water", "Glass", "Grate")
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, -1f, 0f, -1.66055632f),
					new float4(1f, 0f, 0f, -1.35555494f),
					new float4(-1f, 0f, 0f, -1.084445f),
					new float4(0f, 0f, -1f, -1.89777529f),
					new float4(0f, 1f, 0f, -2.60944366f),
					new float4(0f, 0f, 1f, -2.37222481f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(-22.705555f, -78.85944f, -126.202225f, 1f)), name: "Solid 137420 #256")
					.WithMesh(new float3[] { new float3(1.35555494f, -1.66055632f, 2.37222481f), new float3(-1.084445f, -1.66055632f, 2.37222481f), new float3(-1.084445f, -1.66055632f, -1.89777529f), new float3(1.35555494f, -1.66055632f, -1.89777529f), new float3(1.35555494f, 2.60944366f, 2.37222481f), new float3(1.35555494f, 2.60944366f, -1.89777529f), new float3(-1.084445f, 2.60944366f, 2.37222481f), new float3(-1.084445f, 2.60944366f, -1.89777529f) }, new int[] { 0, 1, 2, 3, -1, 4, 0, 3, 5, -1, 1, 6, 7, 2, -1, 7, 5, 3, 2, -1, 6, 4, 5, 7, -1, 4, 6, 1, 0, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 1f, 0f, -3.354997f),
					new float4(0f, -1f, 0f, -2.135003f),
					new float4(-1f, 0f, 0f, -26.2977753f),
					new float4(0f, 0f, 1f, -5.87125f),
					new float4(0f, 0f, -1f, -5.87124968f),
					new float4(1f, 0f, 0f, -32.8722229f),
				}, contents: 1, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(-56.6622238f, -80.825f, -120.39875f, 1f)), name: "Water 3407867 #933")
					.WithMesh(new float3[] { new float3(32.8722229f, 3.354997f, 5.87125f), new float3(32.8722229f, 3.354997f, -5.87124968f), new float3(-26.2977753f, 3.354997f, -5.87124968f), new float3(-26.2977753f, 3.354997f, 5.87125f), new float3(32.8722229f, -2.135003f, -5.87124968f), new float3(32.8722229f, -2.135003f, 5.87125f), new float3(-26.2977753f, -2.135003f, 5.87125f), new float3(-26.2977753f, -2.135003f, -5.87124968f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 2, 7, 6, 3, -1, 5, 0, 3, 6, -1, 1, 4, 7, 2, -1, 4, 1, 0, 5, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 1f, 0f, -3.354997f),
					new float4(0f, -1f, 0f, -2.135003f),
					new float4(1f, 0f, 0f, -8.133333f),
					new float4(0f, 0f, 1f, -5.87125f),
					new float4(0f, 0f, -1f, -5.87124968f),
					new float4(-1f, 0f, 0f, -10.166667f),
				}, contents: 1, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(-13.623333f, -80.825f, -120.39875f, 1f)), name: "Water 3407868 #934")
					.WithMesh(new float3[] { new float3(-10.166667f, 3.354997f, -5.87124968f), new float3(-10.166667f, 3.354997f, 5.87125f), new float3(8.133333f, 3.354997f, 5.87125f), new float3(8.133333f, 3.354997f, -5.87124968f), new float3(-10.166667f, -2.135003f, 5.87125f), new float3(-10.166667f, -2.135003f, -5.87124968f), new float3(8.133333f, -2.135003f, -5.87124968f), new float3(8.133333f, -2.135003f, 5.87125f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 6, 3, 2, 7, -1, 1, 4, 7, 2, -1, 5, 0, 3, 6, -1, 0, 5, 4, 1, -1 }))
				;
		}

		// bm_c1a1e, Model: the mesh draws nothing where the exact CSG has a face; Water 3407867 #933 face 5 near (-23.79,
		// -80, -115.519) (3 brushes)
		static ContentsScene BmC1a1eModel0Site2()
		{
			return new ContentsScene()
				.WithContentsList("Solid", "Water", "Glass", "Grate")
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, -1f, 0f, -1.66055632f),
					new float4(1f, 0f, 0f, -1.35555494f),
					new float4(-1f, 0f, 0f, -1.084445f),
					new float4(0f, 0f, 1f, -1.89777923f),
					new float4(0f, 1f, 0f, -2.60944366f),
					new float4(0f, 0f, -1f, -2.37222075f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(-22.705555f, -78.85944f, -114.137779f, 1f)), name: "Solid 137421 #257")
					.WithMesh(new float3[] { new float3(-1.084445f, -1.66055632f, -2.37222075f), new float3(1.35555494f, -1.66055632f, -2.37222075f), new float3(1.35555494f, -1.66055632f, 1.89777923f), new float3(-1.084445f, -1.66055632f, 1.89777923f), new float3(1.35555494f, 2.60944366f, -2.37222075f), new float3(1.35555494f, 2.60944366f, 1.89777923f), new float3(-1.084445f, 2.60944366f, -2.37222075f), new float3(-1.084445f, 2.60944366f, 1.89777923f) }, new int[] { 0, 1, 2, 3, -1, 1, 4, 5, 2, -1, 6, 0, 3, 7, -1, 5, 7, 3, 2, -1, 4, 6, 7, 5, -1, 6, 4, 1, 0, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 1f, 0f, -3.354997f),
					new float4(0f, -1f, 0f, -2.135003f),
					new float4(-1f, 0f, 0f, -26.2977753f),
					new float4(0f, 0f, 1f, -5.87125f),
					new float4(0f, 0f, -1f, -5.87124968f),
					new float4(1f, 0f, 0f, -32.8722229f),
				}, contents: 1, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(-56.6622238f, -80.825f, -120.39875f, 1f)), name: "Water 3407867 #933")
					.WithMesh(new float3[] { new float3(32.8722229f, 3.354997f, 5.87125f), new float3(32.8722229f, 3.354997f, -5.87124968f), new float3(-26.2977753f, 3.354997f, -5.87124968f), new float3(-26.2977753f, 3.354997f, 5.87125f), new float3(32.8722229f, -2.135003f, -5.87124968f), new float3(32.8722229f, -2.135003f, 5.87125f), new float3(-26.2977753f, -2.135003f, 5.87125f), new float3(-26.2977753f, -2.135003f, -5.87124968f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 2, 7, 6, 3, -1, 5, 0, 3, 6, -1, 1, 4, 7, 2, -1, 4, 1, 0, 5, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 1f, 0f, -3.354997f),
					new float4(0f, -1f, 0f, -2.135003f),
					new float4(1f, 0f, 0f, -8.133333f),
					new float4(0f, 0f, 1f, -5.87125f),
					new float4(0f, 0f, -1f, -5.87124968f),
					new float4(-1f, 0f, 0f, -10.166667f),
				}, contents: 1, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(-13.623333f, -80.825f, -120.39875f, 1f)), name: "Water 3407868 #934")
					.WithMesh(new float3[] { new float3(-10.166667f, 3.354997f, -5.87124968f), new float3(-10.166667f, 3.354997f, 5.87125f), new float3(8.133333f, 3.354997f, 5.87125f), new float3(8.133333f, 3.354997f, -5.87124968f), new float3(-10.166667f, -2.135003f, 5.87125f), new float3(-10.166667f, -2.135003f, -5.87124968f), new float3(8.133333f, -2.135003f, -5.87124968f), new float3(8.133333f, -2.135003f, 5.87125f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 6, 3, 2, 7, -1, 1, 4, 7, 2, -1, 5, 0, 3, 6, -1, 0, 5, 4, 1, -1 }))
				;
		}

		// bm_c1a1e, Model: the mesh draws nothing where the exact CSG has a face; Water 3407868 #934 face 5 near (-23.79,
		// -82, -119.941) (8 brushes)
		static ContentsScene BmC1a1eModel0Site3()
		{
			return new ContentsScene()
				.WithContentsList("Solid", "Water", "Glass", "Grate")
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 1f, 0f, -0.372778922f),
					new float4(0f, -1f, 0f, -0.237221062f),
					new float4(0f, 0f, -1f, -7.93000174f),
					new float4(0f, 0f, 1f, -7.9299984f),
					new float4(-1f, 0f, 0f, -21.96f),
					new float4(1f, 0f, 0f, -27.45f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(-51.24f, -83.33278f, -120.17f, 1f)), name: "Solid 172124 #722")
					.WithMesh(new float3[] { new float3(27.45f, 0.372778922f, 7.9299984f), new float3(27.45f, 0.372778922f, -7.93000174f), new float3(-21.96f, 0.372778922f, -7.93000174f), new float3(-21.96f, 0.372778922f, 7.9299984f), new float3(27.45f, -0.237221062f, -7.93000174f), new float3(27.45f, -0.237221062f, 7.9299984f), new float3(-21.96f, -0.237221062f, 7.9299984f), new float3(-21.96f, -0.237221062f, -7.93000174f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 1, 4, 7, 2, -1, 5, 0, 3, 6, -1, 6, 3, 2, 7, -1, 0, 5, 4, 1, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 1f, 0f, -1.49110842f),
					new float4(0f, -1f, 0f, -0.9488916f),
					new float4(1f, 0f, 0f, -3.795555f),
					new float4(0f, 0f, 1f, -5.828886f),
					new float4(0f, 0f, -1f, -7.286114f),
					new float4(-1f, 0f, 0f, -4.744445f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(-19.0455551f, -82.01111f, -118.068886f, 1f)), name: "Solid 1359006 #786")
					.WithMesh(new float3[] { new float3(-4.744445f, 1.49110842f, -7.286114f), new float3(-4.744445f, 1.49110842f, 5.828886f), new float3(3.795555f, 1.49110842f, 5.828886f), new float3(3.795555f, 1.49110842f, -7.286114f), new float3(-4.744445f, -0.9488916f, 5.828886f), new float3(-4.744445f, -0.9488916f, -7.286114f), new float3(3.795555f, -0.9488916f, -7.286114f), new float3(3.795555f, -0.9488916f, 5.828886f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 6, 3, 2, 7, -1, 1, 4, 7, 2, -1, 5, 0, 3, 6, -1, 0, 5, 4, 1, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 1f, 0f, -3.354997f),
					new float4(0f, -1f, 0f, -2.135003f),
					new float4(-1f, 0f, 0f, -26.2977753f),
					new float4(0f, 0f, 1f, -5.87125f),
					new float4(0f, 0f, -1f, -5.87124968f),
					new float4(1f, 0f, 0f, -32.8722229f),
				}, contents: 1, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(-56.6622238f, -80.825f, -120.39875f, 1f)), name: "Water 3407867 #933")
					.WithMesh(new float3[] { new float3(32.8722229f, 3.354997f, 5.87125f), new float3(32.8722229f, 3.354997f, -5.87124968f), new float3(-26.2977753f, 3.354997f, -5.87124968f), new float3(-26.2977753f, 3.354997f, 5.87125f), new float3(32.8722229f, -2.135003f, -5.87124968f), new float3(32.8722229f, -2.135003f, 5.87125f), new float3(-26.2977753f, -2.135003f, 5.87125f), new float3(-26.2977753f, -2.135003f, -5.87124968f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 2, 7, 6, 3, -1, 5, 0, 3, 6, -1, 1, 4, 7, 2, -1, 4, 1, 0, 5, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 1f, 0f, -3.354997f),
					new float4(0f, -1f, 0f, -2.135003f),
					new float4(1f, 0f, 0f, -8.133333f),
					new float4(0f, 0f, 1f, -5.87125f),
					new float4(0f, 0f, -1f, -5.87124968f),
					new float4(-1f, 0f, 0f, -10.166667f),
				}, contents: 1, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(-13.623333f, -80.825f, -120.39875f, 1f)), name: "Water 3407868 #934")
					.WithMesh(new float3[] { new float3(-10.166667f, 3.354997f, -5.87124968f), new float3(-10.166667f, 3.354997f, 5.87125f), new float3(8.133333f, 3.354997f, 5.87125f), new float3(8.133333f, 3.354997f, -5.87124968f), new float3(-10.166667f, -2.135003f, 5.87125f), new float3(-10.166667f, -2.135003f, -5.87124968f), new float3(8.133333f, -2.135003f, -5.87124968f), new float3(8.133333f, -2.135003f, 5.87125f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 6, 3, 2, 7, -1, 1, 4, 7, 2, -1, 5, 0, 3, 6, -1, 0, 5, 4, 1, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(1f, 0f, 0f, -1.35555494f),
					new float4(-1f, 0f, 0f, -1.084445f),
					new float4(0f, 0f, -1f, -1.62666535f),
					new float4(0f, -1f, 0f, -0.9488913f),
					new float4(0f, 1f, 0f, -1.49110866f),
					new float4(0f, 0f, 1f, -2.03333473f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(-22.705555f, -74.69111f, -125.863335f, 1f)), name: "Solid 3472755 #937")
					.WithMesh(new float3[] { new float3(1.35555494f, 1.49110866f, 2.03333473f), new float3(1.35555494f, -0.9488913f, 2.03333473f), new float3(1.35555494f, -0.9488913f, -1.62666535f), new float3(1.35555494f, 1.49110866f, -1.62666535f), new float3(-1.084445f, -0.9488913f, 2.03333473f), new float3(-1.084445f, 1.49110866f, 2.03333473f), new float3(-1.084445f, 1.49110866f, -1.62666535f), new float3(-1.084445f, -0.9488913f, -1.62666535f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 6, 3, 2, 7, -1, 1, 4, 7, 2, -1, 5, 0, 3, 6, -1, 0, 5, 4, 1, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(1f, 0f, 0f, -1.35555494f),
					new float4(-1f, 0f, 0f, -1.084445f),
					new float4(0f, 0f, 1f, -1.89777923f),
					new float4(0f, -1f, 0f, -0.9488913f),
					new float4(0f, 1f, 0f, -1.49110866f),
					new float4(0f, 0f, -1f, -2.37222075f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(-22.705555f, -74.69111f, -114.137779f, 1f)), name: "Solid 3472757 #938")
					.WithMesh(new float3[] { new float3(1.35555494f, -0.9488913f, -2.37222075f), new float3(1.35555494f, 1.49110866f, -2.37222075f), new float3(1.35555494f, 1.49110866f, 1.89777923f), new float3(1.35555494f, -0.9488913f, 1.89777923f), new float3(-1.084445f, 1.49110866f, -2.37222075f), new float3(-1.084445f, -0.9488913f, -2.37222075f), new float3(-1.084445f, -0.9488913f, 1.89777923f), new float3(-1.084445f, 1.49110866f, 1.89777923f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 2, 7, 6, 3, -1, 5, 0, 3, 6, -1, 1, 4, 7, 2, -1, 4, 1, 0, 5, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 1f, 0f, -0.5422244f),
					new float4(1f, 0f, 0f, -1.35555494f),
					new float4(-1f, 0f, 0f, -1.084445f),
					new float4(0f, 0f, -1f, -1.89777529f),
					new float4(0f, -1f, 0f, -0.677775562f),
					new float4(0f, 0f, 1f, -2.37222481f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(-22.705555f, -72.5222244f, -126.202225f, 1f)), name: "Solid 3472758 #939")
					.WithMesh(new float3[] { new float3(-1.084445f, 0.5422244f, 2.37222481f), new float3(1.35555494f, 0.5422244f, 2.37222481f), new float3(1.35555494f, 0.5422244f, -1.89777529f), new float3(-1.084445f, 0.5422244f, -1.89777529f), new float3(1.35555494f, -0.677775562f, 2.37222481f), new float3(1.35555494f, -0.677775562f, -1.89777529f), new float3(-1.084445f, -0.677775562f, 2.37222481f), new float3(-1.084445f, -0.677775562f, -1.89777529f) }, new int[] { 0, 1, 2, 3, -1, 1, 4, 5, 2, -1, 6, 0, 3, 7, -1, 5, 7, 3, 2, -1, 4, 6, 7, 5, -1, 6, 4, 1, 0, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 1f, 0f, -0.4744449f),
					new float4(1f, 0f, 0f, -1.35555494f),
					new float4(-1f, 0f, 0f, -1.084445f),
					new float4(0f, 0f, 1f, -1.89777923f),
					new float4(0f, -1f, 0f, -0.7455551f),
					new float4(0f, 0f, -1f, -2.37222075f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(-22.705555f, -72.4544449f, -114.137779f, 1f)), name: "Solid 3472760 #940")
					.WithMesh(new float3[] { new float3(1.35555494f, 0.4744449f, -2.37222075f), new float3(-1.084445f, 0.4744449f, -2.37222075f), new float3(-1.084445f, 0.4744449f, 1.89777923f), new float3(1.35555494f, 0.4744449f, 1.89777923f), new float3(1.35555494f, -0.7455551f, -2.37222075f), new float3(1.35555494f, -0.7455551f, 1.89777923f), new float3(-1.084445f, -0.7455551f, -2.37222075f), new float3(-1.084445f, -0.7455551f, 1.89777923f) }, new int[] { 0, 1, 2, 3, -1, 4, 0, 3, 5, -1, 1, 6, 7, 2, -1, 7, 5, 3, 2, -1, 6, 4, 5, 7, -1, 4, 6, 1, 0, -1 }))
				;
		}

		// bm_c1a1e, Model: the mesh draws nothing where the exact CSG has a face; Solid 1359183 #790 face 5 near (-23.79,
		// -82, -125.813) (3 brushes)
		static ContentsScene BmC1a1eModel0Site4()
		{
			return new ContentsScene()
				.WithContentsList("Solid", "Water", "Glass", "Grate")
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 1f, 0f, -1.49110842f),
					new float4(0f, -1f, 0f, -0.9488916f),
					new float4(1f, 0f, 0f, -1.0844444f),
					new float4(0f, 0f, 1f, -1.21999693f),
					new float4(0f, 0f, -1f, -1.52500308f),
					new float4(-1f, 0f, 0f, -1.35555553f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(-22.4344444f, -82.01111f, -126.575f, 1f)), name: "Solid 1359183 #790")
					.WithMesh(new float3[] { new float3(-1.35555553f, 1.49110842f, -1.52500308f), new float3(-1.35555553f, 1.49110842f, 1.21999693f), new float3(1.0844444f, 1.49110842f, 1.21999693f), new float3(1.0844444f, 1.49110842f, -1.52500308f), new float3(-1.35555553f, -0.9488916f, 1.21999693f), new float3(-1.35555553f, -0.9488916f, -1.52500308f), new float3(1.0844444f, -0.9488916f, -1.52500308f), new float3(1.0844444f, -0.9488916f, 1.21999693f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 6, 3, 2, 7, -1, 1, 4, 7, 2, -1, 5, 0, 3, 6, -1, 0, 5, 4, 1, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 1f, 0f, -3.354997f),
					new float4(0f, -1f, 0f, -2.135003f),
					new float4(-1f, 0f, 0f, -26.2977753f),
					new float4(0f, 0f, 1f, -5.87125f),
					new float4(0f, 0f, -1f, -5.87124968f),
					new float4(1f, 0f, 0f, -32.8722229f),
				}, contents: 1, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(-56.6622238f, -80.825f, -120.39875f, 1f)), name: "Water 3407867 #933")
					.WithMesh(new float3[] { new float3(32.8722229f, 3.354997f, 5.87125f), new float3(32.8722229f, 3.354997f, -5.87124968f), new float3(-26.2977753f, 3.354997f, -5.87124968f), new float3(-26.2977753f, 3.354997f, 5.87125f), new float3(32.8722229f, -2.135003f, -5.87124968f), new float3(32.8722229f, -2.135003f, 5.87125f), new float3(-26.2977753f, -2.135003f, 5.87125f), new float3(-26.2977753f, -2.135003f, -5.87124968f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 2, 7, 6, 3, -1, 5, 0, 3, 6, -1, 1, 4, 7, 2, -1, 4, 1, 0, 5, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 1f, 0f, -3.354997f),
					new float4(0f, -1f, 0f, -2.135003f),
					new float4(1f, 0f, 0f, -8.133333f),
					new float4(0f, 0f, 1f, -5.87125f),
					new float4(0f, 0f, -1f, -5.87124968f),
					new float4(-1f, 0f, 0f, -10.166667f),
				}, contents: 1, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(-13.623333f, -80.825f, -120.39875f, 1f)), name: "Water 3407868 #934")
					.WithMesh(new float3[] { new float3(-10.166667f, 3.354997f, -5.87124968f), new float3(-10.166667f, 3.354997f, 5.87125f), new float3(8.133333f, 3.354997f, 5.87125f), new float3(8.133333f, 3.354997f, -5.87124968f), new float3(-10.166667f, -2.135003f, 5.87125f), new float3(-10.166667f, -2.135003f, -5.87124968f), new float3(8.133333f, -2.135003f, -5.87124968f), new float3(8.133333f, -2.135003f, 5.87125f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 6, 3, 2, 7, -1, 1, 4, 7, 2, -1, 5, 0, 3, 6, -1, 0, 5, 4, 1, -1 }))
				;
		}

		// bm_c1a1e, Details: the mesh has a triangle the exact CSG did not make; Grate 1153952 #1214 face -1 near (-46.395,
		// -79.102, -117.836) (4 brushes)
		static ContentsScene BmC1a1eDetails31Site1()
		{
			return new ContentsScene()
				.WithContentsList("Solid", "Water", "Glass", "Grate")
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(-0.044794865f, 0.6173589f, -0.7854052f, -0.0169438254f),
					new float4(0.04507169f, -0.613272667f, 0.7885843f, -0.02122822f),
					new float4(-0.997598231f, 0.0146392649f, 0.06770098f, -1.516502f),
					new float4(0.9975513f, -0.0138787245f, -0.06854733f, -1.89562726f),
					new float4(0.05340556f, 0.787520945f, 0.6139695f, -0.0508654639f),
					new float4(-0.053164117f, -0.791265547f, -0.609157145f, -0.06359907f),
				}, contents: 3, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(-48.26305f, -79.03484f, -117.6552f, 1f)), name: "Grate 1153952 #1214")
					.WithMesh(new float3[] { new float3(1.88684571f, -0.06709451f, -0.181926757f), new float3(-1.51695132f, -0.0177256744f, 0.0510115065f), new float3(-1.51084816f, 0.0723494962f, 0.121465996f), new float3(1.89294636f, 0.0230706781f, -0.111401364f), new float3(-1.51523221f, -0.0412619151f, 0.08143389f), new float3(1.88860857f, -0.09063139f, -0.151507348f), new float3(1.89466977f, -0.000285561633f, -0.0815928951f), new float3(-1.50916779f, 0.0489935167f, 0.15127787f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 1, 4, 7, 2, -1, 5, 0, 3, 6, -1, 6, 3, 2, 7, -1, 0, 5, 4, 1, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(-0.0432901271f, 0.615944445f, -0.7865992f, -0.0169710312f),
					new float4(0.0455527157f, -0.61596483f, 0.7864555f, -0.0211761687f),
					new float4(0.9975536f, -0.0143593112f, -0.0684149638f, -0.0572308972f),
					new float4(-0.9975679f, 0.01455204f, 0.06816528f, -0.05722938f),
					new float4(0.0537943356f, 0.7876037f, 0.613829434f, -4.2054677f),
					new float4(-0.051748462f, -0.7851443f, -0.617147f, -2.676184f),
				}, contents: 3, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(-49.8277245f, -78.9362f, -117.488571f, 1f)), name: "Grate 1153949 #1217")
					.WithMesh(new float3[] { new float3(-0.08666316f, -2.09817743f, -1.65977812f), new float3(-0.200852737f, -2.09657645f, -1.65224f), new float3(0.166857153f, 3.3237412f, 2.57188869f), new float3(0.281048417f, 3.32193923f, 2.56419325f), new float3(-0.199145928f, -2.12030435f, -1.62219608f), new float3(-0.0849559158f, -2.12174487f, -1.62993848f), new float3(0.282758683f, 3.29856324f, 2.594037f), new float3(0.1685671f, 3.30020547f, 2.60193729f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 5, 0, 3, 6, -1, 1, 4, 7, 2, -1, 2, 7, 6, 3, -1, 4, 1, 0, 5, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(-0.0435682163f, 0.615931451f, -0.7865941f, -0.0169270076f),
					new float4(0.0446154177f, -0.6159774f, 0.7864994f, -0.0211702455f),
					new float4(0.9975595f, -0.0144400662f, -0.068311736f, -0.0572295375f),
					new float4(-0.99755615f, 0.0143951271f, 0.0683706552f, -0.057233844f),
					new float4(0.0551764742f, 0.7870079f, 0.614470541f, -4.20533f),
					new float4(-0.052692946f, -0.790018141f, -0.610814869f, -2.676203f),
				}, contents: 3, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(-46.3288f, -78.98699f, -117.727829f, 1f)), name: "Grate 1153943 #1223")
					.WithMesh(new float3[] { new float3(-0.0866700038f, -2.098405f, -1.65984535f), new float3(-0.200868428f, -2.09670663f, -1.65219021f), new float3(0.166857868f, 3.323732f, 2.57184f), new float3(0.28103587f, 3.32182837f, 2.5640254f), new float3(-0.199147522f, -2.1200316f, -1.62217069f), new float3(-0.0849573538f, -2.121656f, -1.62992048f), new float3(0.282750547f, 3.29826975f, 2.59404516f), new float3(0.1685808f, 3.300099f, 2.60195422f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 5, 0, 3, 6, -1, 1, 4, 7, 2, -1, 2, 7, 6, 3, -1, 4, 1, 0, 5, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(-0.0439570323f, 0.615967631f, -0.786544144f, -0.114458084f),
					new float4(0.0449157171f, -0.6159695f, 0.786488533f, -0.114460528f),
					new float4(-0.9975593f, 0.0144378757f, 0.0683153346f, -0.06777224f),
					new float4(0.9975569f, -0.0144021744f, -0.06835788f, -0.0847125351f),
					new float4(0.0523254164f, 0.788517058f, 0.612782955f, -4.205398f),
					new float4(-0.0532100424f, -0.7872884f, -0.6142847f, -2.67605186f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(-46.3430634f, -78.9059448f, -117.830711f, 1f)), name: "Solid 1153895 #1225")
					.WithMesh(new float3[] { new float3(-0.0636292845f, -2.03843665f, -1.73832846f), new float3(-0.215749383f, -2.03617215f, -1.72805381f), new float3(0.151953742f, 3.38363314f, 2.495818f), new float3(0.304067463f, 3.381465f, 2.48561883f), new float3(-0.205462724f, -2.17734838f, -1.54800868f), new float3(-0.05333651f, -2.17952251f, -1.55839932f), new float3(0.314379573f, 3.24080157f, 2.66574121f), new float3(0.162259758f, 3.24287987f, 2.67605615f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 1, 4, 7, 2, -1, 5, 0, 3, 6, -1, 6, 3, 2, 7, -1, 0, 5, 4, 1, -1 }))
				;
		}

		// bm_c1a4a, Model: the mesh draws nothing where the exact CSG has a face; Water 3053185 #390 face 5 near (-12,
		// -21.693, -84.18) (3 brushes)
		static ContentsScene BmC1a4aModel0Site1()
		{
			return new ContentsScene()
				.WithContentsList("Solid", "Water", "Glass", "Grate")
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(-1f, 0f, 0f, -22.875f),
					new float4(0f, 0f, 1f, -0.305f),
					new float4(0f, 0f, -1f, -0.305f),
					new float4(1f, 0f, 0f, -18.3f),
					new float4(0f, -1f, 0f, -9.319445f),
					new float4(0f, 1f, 0f, -5.93055534f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(-26.535f, -12.6405554f, -83.875f, 1f)), name: "Solid 135914 #199")
					.WithMesh(new float3[] { new float3(-22.875f, 5.93055534f, 0.305f), new float3(-22.875f, 5.93055534f, -0.305f), new float3(-22.875f, -9.319445f, -0.305f), new float3(-22.875f, -9.319445f, 0.305f), new float3(18.3f, 5.93055534f, 0.305f), new float3(18.3f, -9.319445f, 0.305f), new float3(18.3f, 5.93055534f, -0.305f), new float3(18.3f, -9.319445f, -0.305f) }, new int[] { 0, 1, 2, 3, -1, 4, 0, 3, 5, -1, 1, 6, 7, 2, -1, 6, 4, 5, 7, -1, 7, 5, 3, 2, -1, 4, 6, 1, 0, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 1f, 0f, -2.09687471f),
					new float4(0f, -1f, 0f, -1.33437526f),
					new float4(-1f, 0f, 0f, -21.6888885f),
					new float4(1f, 0f, 0f, -27.11111f),
					new float4(0f, 0f, -1f, -16.164999f),
					new float4(0f, 0f, 1f, -16.165f),
				}, contents: 1, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(-27.7211113f, -23.5231247f, -100.345f, 1f)), name: "Water 3053185 #390")
					.WithMesh(new float3[] { new float3(-21.6888885f, 2.09687471f, 16.165f), new float3(27.11111f, 2.09687471f, 16.165f), new float3(27.11111f, 2.09687471f, -16.164999f), new float3(-21.6888885f, 2.09687471f, -16.164999f), new float3(27.11111f, -1.33437526f, 16.165f), new float3(-21.6888885f, -1.33437526f, 16.165f), new float3(-21.6888885f, -1.33437526f, -16.164999f), new float3(27.11111f, -1.33437526f, -16.164999f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 5, 0, 3, 6, -1, 1, 4, 7, 2, -1, 2, 7, 6, 3, -1, 4, 1, 0, 5, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 1f, 0f, -2.09687471f),
					new float4(0f, -1f, 0f, -1.33437526f),
					new float4(0f, 0f, 1f, -7.62500048f),
					new float4(0f, 0f, -1f, -7.62499952f),
					new float4(-1f, 0f, 0f, -1.86388838f),
					new float4(1f, 0f, 0f, -1.49111152f),
				}, contents: 1, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(-10.8317366f, -23.5231247f, -76.555f, 1f)), name: "Water 3053248 #393")
					.WithMesh(new float3[] { new float3(1.49111152f, 2.09687471f, 7.62500048f), new float3(1.49111152f, 2.09687471f, -7.62499952f), new float3(-1.86388838f, 2.09687471f, -7.62499952f), new float3(-1.86388838f, 2.09687471f, 7.62500048f), new float3(1.49111152f, -1.33437526f, -7.62499952f), new float3(1.49111152f, -1.33437526f, 7.62500048f), new float3(-1.86388838f, -1.33437526f, 7.62500048f), new float3(-1.86388838f, -1.33437526f, -7.62499952f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 5, 0, 3, 6, -1, 1, 4, 7, 2, -1, 2, 7, 6, 3, -1, 4, 1, 0, 5, -1 }))
				;
		}

		// bm_c1a4a, Model: the mesh draws nothing where the exact CSG has a face; Water 3053208 #391 face 3 near (-40,
		// -21.693, -84.18) (5 brushes)
		static ContentsScene BmC1a4aModel0Site2()
		{
			return new ContentsScene()
				.WithContentsList("Solid", "Water", "Glass", "Grate")
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(-1f, 0f, 0f, -22.875f),
					new float4(0f, 0f, 1f, -0.305f),
					new float4(0f, 0f, -1f, -0.305f),
					new float4(1f, 0f, 0f, -18.3f),
					new float4(0f, -1f, 0f, -9.319445f),
					new float4(0f, 1f, 0f, -5.93055534f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(-26.535f, -12.6405554f, -83.875f, 1f)), name: "Solid 135914 #199")
					.WithMesh(new float3[] { new float3(-22.875f, 5.93055534f, 0.305f), new float3(-22.875f, 5.93055534f, -0.305f), new float3(-22.875f, -9.319445f, -0.305f), new float3(-22.875f, -9.319445f, 0.305f), new float3(18.3f, 5.93055534f, 0.305f), new float3(18.3f, -9.319445f, 0.305f), new float3(18.3f, 5.93055534f, -0.305f), new float3(18.3f, -9.319445f, -0.305f) }, new int[] { 0, 1, 2, 3, -1, 4, 0, 3, 5, -1, 1, 6, 7, 2, -1, 6, 4, 5, 7, -1, 7, 5, 3, 2, -1, 4, 6, 1, 0, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 1f, 0f, -0.6099999f),
					new float4(0f, -1f, 0f, -0.6100001f),
					new float4(-1f, 0f, 0f, -24.4f),
					new float4(1f, 0f, 0f, -24.4f),
					new float4(0f, 0f, 1f, -2.71111321f),
					new float4(0f, 0f, -1f, -2.16888666f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(-25.01f, -6.1f, -86.89111f, 1f)), name: "Solid 1113408 #221")
					.WithMesh(new float3[] { new float3(24.4f, 0.6099999f, -2.16888666f), new float3(-24.4f, 0.6099999f, -2.16888666f), new float3(-24.4f, 0.6099999f, 2.71111321f), new float3(24.4f, 0.6099999f, 2.71111321f), new float3(-24.4f, -0.6100001f, -2.16888666f), new float3(24.4f, -0.6100001f, -2.16888666f), new float3(24.4f, -0.6100001f, 2.71111321f), new float3(-24.4f, -0.6100001f, 2.71111321f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 1, 4, 7, 2, -1, 5, 0, 3, 6, -1, 6, 3, 2, 7, -1, 0, 5, 4, 1, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 1f, 0f, -2.09687471f),
					new float4(0f, -1f, 0f, -1.33437526f),
					new float4(-1f, 0f, 0f, -21.6888885f),
					new float4(1f, 0f, 0f, -27.11111f),
					new float4(0f, 0f, -1f, -16.164999f),
					new float4(0f, 0f, 1f, -16.165f),
				}, contents: 1, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(-27.7211113f, -23.5231247f, -100.345f, 1f)), name: "Water 3053185 #390")
					.WithMesh(new float3[] { new float3(-21.6888885f, 2.09687471f, 16.165f), new float3(27.11111f, 2.09687471f, 16.165f), new float3(27.11111f, 2.09687471f, -16.164999f), new float3(-21.6888885f, 2.09687471f, -16.164999f), new float3(27.11111f, -1.33437526f, 16.165f), new float3(-21.6888885f, -1.33437526f, 16.165f), new float3(-21.6888885f, -1.33437526f, -16.164999f), new float3(27.11111f, -1.33437526f, -16.164999f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 5, 0, 3, 6, -1, 1, 4, 7, 2, -1, 2, 7, 6, 3, -1, 4, 1, 0, 5, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 1f, 0f, -2.09687471f),
					new float4(0f, -1f, 0f, -1.33437526f),
					new float4(0f, 0f, 1f, -7.62500048f),
					new float4(0f, 0f, -1f, -7.62499952f),
					new float4(-1f, 0f, 0f, -1.863889f),
					new float4(1f, 0f, 0f, -1.491111f),
				}, contents: 1, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(-38.85361f, -23.5231247f, -76.555f, 1f)), name: "Water 3053208 #391")
					.WithMesh(new float3[] { new float3(1.491111f, 2.09687471f, 7.62500048f), new float3(1.491111f, 2.09687471f, -7.62499952f), new float3(-1.863889f, 2.09687471f, -7.62499952f), new float3(-1.863889f, 2.09687471f, 7.62500048f), new float3(1.491111f, -1.33437526f, -7.62499952f), new float3(1.491111f, -1.33437526f, 7.62500048f), new float3(-1.863889f, -1.33437526f, 7.62500048f), new float3(-1.863889f, -1.33437526f, -7.62499952f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 5, 0, 3, 6, -1, 1, 4, 7, 2, -1, 2, 7, 6, 3, -1, 4, 1, 0, 5, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 1f, 0f, -2.09687471f),
					new float4(0f, -1f, 0f, -1.33437526f),
					new float4(0f, 0f, 1f, -7.62500048f),
					new float4(0f, 0f, -1f, -7.62499952f),
					new float4(-1f, 0f, 0f, -1.86388969f),
					new float4(1f, 0f, 0f, -1.49111032f),
				}, contents: 1, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(-24.82361f, -23.5231247f, -76.555f, 1f)), name: "Water 3053214 #392")
					.WithMesh(new float3[] { new float3(1.49111032f, 2.09687471f, 7.62500048f), new float3(1.49111032f, 2.09687471f, -7.62499952f), new float3(-1.86388969f, 2.09687471f, -7.62499952f), new float3(-1.86388969f, 2.09687471f, 7.62500048f), new float3(1.49111032f, -1.33437526f, -7.62499952f), new float3(1.49111032f, -1.33437526f, 7.62500048f), new float3(-1.86388969f, -1.33437526f, 7.62500048f), new float3(-1.86388969f, -1.33437526f, -7.62499952f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 5, 0, 3, 6, -1, 1, 4, 7, 2, -1, 2, 7, 6, 3, -1, 4, 1, 0, 5, -1 }))
				;
		}

		// bm_c1a4a, Model: the mesh draws nothing where the exact CSG has a face; Water 3053214 #392 face 3 near (-26,
		// -21.693, -84.18) (6 brushes)
		static ContentsScene BmC1a4aModel0Site3()
		{
			return new ContentsScene()
				.WithContentsList("Solid", "Water", "Glass", "Grate")
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 1f, 0f, -0.372778028f),
					new float4(0f, -1f, 0f, -0.237221986f),
					new float4(-1f, 0f, 0f, -1.49111176f),
					new float4(1f, 0f, 0f, -1.86388826f),
					new float4(0f, 0f, 1f, -19.05297f),
					new float4(0f, 0f, -1f, -19.0529671f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(-25.1963882f, -25.230278f, -87.98297f, 1f)), name: "Solid 1108663 #194")
					.WithMesh(new float3[] { new float3(1.86388826f, 0.372778028f, -19.0529671f), new float3(-1.49111176f, 0.372778028f, -19.0529671f), new float3(-1.49111176f, 0.372778028f, 19.05297f), new float3(1.86388826f, 0.372778028f, 19.05297f), new float3(-1.49111176f, -0.237221986f, -19.0529671f), new float3(1.86388826f, -0.237221986f, -19.0529671f), new float3(1.86388826f, -0.237221986f, 19.05297f), new float3(-1.49111176f, -0.237221986f, 19.05297f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 1, 4, 7, 2, -1, 5, 0, 3, 6, -1, 6, 3, 2, 7, -1, 0, 5, 4, 1, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(-1f, 0f, 0f, -22.875f),
					new float4(0f, 0f, 1f, -0.305f),
					new float4(0f, 0f, -1f, -0.305f),
					new float4(1f, 0f, 0f, -18.3f),
					new float4(0f, -1f, 0f, -9.319445f),
					new float4(0f, 1f, 0f, -5.93055534f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(-26.535f, -12.6405554f, -83.875f, 1f)), name: "Solid 135914 #199")
					.WithMesh(new float3[] { new float3(-22.875f, 5.93055534f, 0.305f), new float3(-22.875f, 5.93055534f, -0.305f), new float3(-22.875f, -9.319445f, -0.305f), new float3(-22.875f, -9.319445f, 0.305f), new float3(18.3f, 5.93055534f, 0.305f), new float3(18.3f, -9.319445f, 0.305f), new float3(18.3f, 5.93055534f, -0.305f), new float3(18.3f, -9.319445f, -0.305f) }, new int[] { 0, 1, 2, 3, -1, 4, 0, 3, 5, -1, 1, 6, 7, 2, -1, 6, 4, 5, 7, -1, 7, 5, 3, 2, -1, 4, 6, 1, 0, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 1f, 0f, -0.6099999f),
					new float4(0f, -1f, 0f, -0.6100001f),
					new float4(-1f, 0f, 0f, -24.4f),
					new float4(1f, 0f, 0f, -24.4f),
					new float4(0f, 0f, 1f, -2.71111321f),
					new float4(0f, 0f, -1f, -2.16888666f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(-25.01f, -6.1f, -86.89111f, 1f)), name: "Solid 1113408 #221")
					.WithMesh(new float3[] { new float3(24.4f, 0.6099999f, -2.16888666f), new float3(-24.4f, 0.6099999f, -2.16888666f), new float3(-24.4f, 0.6099999f, 2.71111321f), new float3(24.4f, 0.6099999f, 2.71111321f), new float3(-24.4f, -0.6100001f, -2.16888666f), new float3(24.4f, -0.6100001f, -2.16888666f), new float3(24.4f, -0.6100001f, 2.71111321f), new float3(-24.4f, -0.6100001f, 2.71111321f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 1, 4, 7, 2, -1, 5, 0, 3, 6, -1, 6, 3, 2, 7, -1, 0, 5, 4, 1, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 1f, 0f, -2.09687471f),
					new float4(0f, -1f, 0f, -1.33437526f),
					new float4(-1f, 0f, 0f, -21.6888885f),
					new float4(1f, 0f, 0f, -27.11111f),
					new float4(0f, 0f, -1f, -16.164999f),
					new float4(0f, 0f, 1f, -16.165f),
				}, contents: 1, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(-27.7211113f, -23.5231247f, -100.345f, 1f)), name: "Water 3053185 #390")
					.WithMesh(new float3[] { new float3(-21.6888885f, 2.09687471f, 16.165f), new float3(27.11111f, 2.09687471f, 16.165f), new float3(27.11111f, 2.09687471f, -16.164999f), new float3(-21.6888885f, 2.09687471f, -16.164999f), new float3(27.11111f, -1.33437526f, 16.165f), new float3(-21.6888885f, -1.33437526f, 16.165f), new float3(-21.6888885f, -1.33437526f, -16.164999f), new float3(27.11111f, -1.33437526f, -16.164999f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 5, 0, 3, 6, -1, 1, 4, 7, 2, -1, 2, 7, 6, 3, -1, 4, 1, 0, 5, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 1f, 0f, -2.09687471f),
					new float4(0f, -1f, 0f, -1.33437526f),
					new float4(0f, 0f, 1f, -7.62500048f),
					new float4(0f, 0f, -1f, -7.62499952f),
					new float4(-1f, 0f, 0f, -1.86388969f),
					new float4(1f, 0f, 0f, -1.49111032f),
				}, contents: 1, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(-24.82361f, -23.5231247f, -76.555f, 1f)), name: "Water 3053214 #392")
					.WithMesh(new float3[] { new float3(1.49111032f, 2.09687471f, 7.62500048f), new float3(1.49111032f, 2.09687471f, -7.62499952f), new float3(-1.86388969f, 2.09687471f, -7.62499952f), new float3(-1.86388969f, 2.09687471f, 7.62500048f), new float3(1.49111032f, -1.33437526f, -7.62499952f), new float3(1.49111032f, -1.33437526f, 7.62500048f), new float3(-1.86388969f, -1.33437526f, 7.62500048f), new float3(-1.86388969f, -1.33437526f, -7.62499952f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 5, 0, 3, 6, -1, 1, 4, 7, 2, -1, 2, 7, 6, 3, -1, 4, 1, 0, 5, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 1f, 0f, -2.09687471f),
					new float4(0f, -1f, 0f, -1.33437526f),
					new float4(0f, 0f, 1f, -7.62500048f),
					new float4(0f, 0f, -1f, -7.62499952f),
					new float4(-1f, 0f, 0f, -1.86388838f),
					new float4(1f, 0f, 0f, -1.49111152f),
				}, contents: 1, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(-10.8317366f, -23.5231247f, -76.555f, 1f)), name: "Water 3053248 #393")
					.WithMesh(new float3[] { new float3(1.49111152f, 2.09687471f, 7.62500048f), new float3(1.49111152f, 2.09687471f, -7.62499952f), new float3(-1.86388838f, 2.09687471f, -7.62499952f), new float3(-1.86388838f, 2.09687471f, 7.62500048f), new float3(1.49111152f, -1.33437526f, -7.62499952f), new float3(1.49111152f, -1.33437526f, 7.62500048f), new float3(-1.86388838f, -1.33437526f, 7.62500048f), new float3(-1.86388838f, -1.33437526f, -7.62499952f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 5, 0, 3, 6, -1, 1, 4, 7, 2, -1, 2, 7, 6, 3, -1, 4, 1, 0, 5, -1 }))
				;
		}

		// bm_c1a4a, Details: the mesh has a triangle the exact CSG did not make; Solid 1747384 #3482 face -1 near (-19.52,
		// -15.504, -62.651) (3 brushes)
		static ContentsScene BmC1a4aDetails27Site1()
		{
			return new ContentsScene()
				.WithContentsList("Solid", "Water", "Glass", "Grate")
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 1f, 0f, -0.09319435f),
					new float4(0f, -1f, 0f, -0.05930565f),
					new float4(-1f, 0f, 0f, -1.084445f),
					new float4(1f, 0f, 0f, -1.355555f),
					new float4(0f, 0f, 1f, -3.65999722f),
					new float4(0f, 0f, -1f, -3.66000271f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(-20.875555f, -14.7331944f, -65.88f, 1f)), name: "Solid 203940 #2968")
					.WithMesh(new float3[] { new float3(1.355555f, 0.09319435f, -3.66000271f), new float3(-1.084445f, 0.09319435f, -3.66000271f), new float3(-1.084445f, 0.09319435f, 3.65999722f), new float3(1.355555f, 0.09319435f, 3.65999722f), new float3(-1.084445f, -0.05930565f, -3.66000271f), new float3(1.355555f, -0.05930565f, -3.66000271f), new float3(1.355555f, -0.05930565f, 3.65999722f), new float3(-1.084445f, -0.05930565f, 3.65999722f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 1, 4, 7, 2, -1, 5, 0, 3, 6, -1, 6, 3, 2, 7, -1, 0, 5, 4, 1, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 1f, 0f, -0.9150003f),
					new float4(0f, -1f, 0f, -1.52499974f),
					new float4(-0.701640546f, 0.124101408f, -0.701640546f, -0.138669476f),
					new float4(0.707104862f, 0f, 0.7071087f, -0.203297421f),
					new float4(-0.9961122f, 0.08809326f, 0f, -0.308781952f),
					new float4(0.7064883f, 0.000165262158f, -0.7077247f, -0.2351111f),
					new float4(0f, 0.0880942f, -0.996112168f, -0.2103199f),
					new float4(-0.7077247f, 0f, 0.7064883f, -0.374752522f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(-19.83002f, -15.555f, -62.62887f, 1f)), name: "Solid 1747384 #3482")
					.WithMesh(new float3[] { new float3(-0.229067147f, 0.9150003f, 0.300976f), new float3(-0.121363558f, 0.9150003f, 0.4088681f), new float3(0.3100209f, 0.9150003f, -0.0225140359f), new float3(0.202126488f, 0.9150003f, -0.130219966f), new float3(0.09442293f, 0.9150003f, -0.130219966f), new float3(-0.229067147f, 0.9150003f, 0.193270117f), new float3(-0.121363558f, -1.52499974f, 0.4088681f), new float3(-0.444853634f, -1.52499974f, 0.0848119f), new float3(-0.444853634f, -1.52499974f, -0.022514008f), new float3(-0.121358886f, -1.52499974f, -0.346008748f), new float3(-0.0134691736f, -1.52499974f, -0.346008748f), new float3(0.310306042f, -1.52499974f, -0.02279917f) }, new int[] { 0, 1, 2, 3, 4, 5, -1, 6, 7, 8, 9, 10, 11, -1, 4, 9, 8, 5, -1, 1, 6, 11, 2, -1, 7, 0, 5, 8, -1, 10, 3, 2, 11, -1, 3, 10, 9, 4, -1, 0, 7, 6, 1, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 1f, 0f, -0.457500368f),
					new float4(-0.7071119f, 0f, -0.7071017f, -0.203356341f),
					new float4(0.707104862f, 0f, 0.7071087f, -0.330421627f),
					new float4(-1f, 0f, 0f, -0.323513836f),
					new float4(0.7071119f, 0f, -0.7071017f, -0.279616147f),
					new float4(0f, 0f, -1f, -0.2875676f),
					new float4(-0.707106769f, 0f, 0.707106769f, -0.33045435f),
					new float4(0f, -1f, 0f, -0.76249963f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(-19.95136f, -17.5375f, -62.68731f, 1f)), name: "Solid 1747387 #3485")
					.WithMesh(new float3[] { new float3(-0.323513836f, 0.457500368f, 0.1438192f), new float3(-2.37689128E-05f, 0.457500368f, 0.467309266f), new float3(0.4313607f, 0.457500368f, 0.0359271243f), new float3(0.107870646f, 0.457500368f, -0.2875676f), new float3(-2.37742224E-05f, 0.457500368f, -0.2875676f), new float3(-0.323513836f, 0.457500368f, 0.03592714f), new float3(-2.37742224E-05f, -0.76249963f, -0.2875676f), new float3(-0.323513836f, -0.76249963f, 0.03592714f), new float3(-2.37689128E-05f, -0.76249963f, 0.467309266f), new float3(0.4313607f, -0.76249963f, 0.0359271243f), new float3(-0.323513836f, -0.76249963f, 0.1438192f), new float3(0.107870646f, -0.76249963f, -0.2875676f) }, new int[] { 0, 1, 2, 3, 4, 5, -1, 6, 7, 5, 4, -1, 8, 9, 2, 1, -1, 7, 10, 0, 5, -1, 9, 11, 3, 2, -1, 11, 6, 4, 3, -1, 10, 8, 1, 0, -1, 8, 10, 7, 6, 11, 9, -1 }))
				;
		}

		// bm_c1a4a, Details: the exact CSG made a triangle the mesh does not have; Solid 2024569 #3843 face -1 near (-32.374,
		// -14.403, -88.722) (5 brushes)
		static ContentsScene BmC1a4aDetails27Site2()
		{
			return new ContentsScene()
				.WithContentsList("Solid", "Water", "Glass", "Grate")
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0.9654222f, 0.26067397f, 0.003014844f, -0.40144667f),
					new float4(-0.86536473f, -0.5011371f, -0.00235040952f, -0.405080557f),
					new float4(0.271673828f, -0.6556821f, 0.7044674f, -0.138662547f),
					new float4(-0.343212515f, 0.828403056f, -0.442666382f, -0.219479933f),
					new float4(0.218083322f, -0.5263419f, -0.8218296f, -0.0161837433f),
					new float4(-0.210046217f, 0.5070542f, 0.8359286f, -0.02032686f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(-31.6806259f, -14.22467f, -88.58053f, 1f)), name: "Solid 2024553 #3827")
					.WithMesh(new float3[] { new float3(0.32330513f, 0.343844384f, -0.103013411f), new float3(0.400980681f, 0.0539084747f, 0.0923725f), new float3(0.407258481f, 0.03093356f, 0.0685676262f), new float3(0.3283525f, 0.325591475f, -0.141085163f), new float3(-0.325316846f, -0.2469983f, 0.0923964158f), new float3(-0.475337029f, 0.012973343f, -0.10299214f), new float3(-0.465743631f, -0.00341387023f, -0.141097128f), new float3(-0.313307583f, -0.26762414f, 0.0685676262f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 1, 4, 7, 2, -1, 5, 0, 3, 6, -1, 6, 3, 2, 7, -1, 0, 5, 4, 1, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0.9659513f, 0.258723527f, 0.000353927928f, -0.495405734f),
					new float4(-0.8660014f, -0.500041246f, 0.000512785045f, -0.3951663f),
					new float4(0.342819959f, -0.827599f, 0.444470972f, -0.135373622f),
					new float4(-0.382644922f, 0.9238955f, 0f, -0.217127815f),
					new float4(0.07377801f, -0.178096846f, -0.981243253f, -0.01972243f),
					new float4(-0.077651076f, 0.187439576f, 0.9792021f, -0.0199497715f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(-31.85223f, -13.9474249f, -88.73038f, 1f)), name: "Solid 2024554 #3828")
					.WithMesh(new float3[] { new float3(0.405003f, 0.402751356f, -0.0246045776f), new float3(0.49500078f, 0.0666445f, 0.0468700975f), new float3(0.499950558f, 0.0482165f, 0.008739648f), new float3(0.4050156f, 0.402756572f, -0.06274781f), new float3(-0.303721339f, -0.2642165f, 0.0468648039f), new float3(-0.4777699f, 0.0371379443f, -0.0246228f), new float3(-0.47778815f, 0.0371303968f, -0.06276272f), new float3(-0.294197977f, -0.280748785f, 0.00873657f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 1, 4, 7, 2, -1, 5, 0, 3, 6, -1, 6, 3, 2, 7, -1, 0, 5, 4, 1, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0.865059733f, 0.5016624f, 0.00255035749f, -0.401515f),
					new float4(-0.7075395f, -0.706672966f, 0.00108069251f, -0.405248433f),
					new float4(0.432802767f, -0.5641097f, 0.703179955f, -0.138722748f),
					new float4(-0.545300841f, 0.7107264f, -0.444426656f, -0.219514489f),
					new float4(0.346937239f, -0.452174783f, -0.821688831f, -0.0162021276f),
					new float4(-0.3340331f, 0.435284138f, 0.836032033f, -0.0203724168f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(-32.3793945f, -14.6275358f, -88.58053f, 1f)), name: "Solid 2024569 #3843")
					.WithMesh(new float3[] { new float3(0.223302871f, 0.415831774f, -0.102917127f), new float3(0.373485684f, 0.155865446f, 0.09244052f), new float3(0.385473967f, 0.135314316f, 0.06857514f), new float3(0.23296842f, 0.399358869f, -0.141119987f), new float3(-0.250271529f, -0.3227399f, 0.09240914f), new float3(-0.462601036f, -0.11044883f, -0.1029566f), new float3(-0.449137956f, -0.123986781f, -0.141125336f), new float3(-0.233487755f, -0.3395807f, 0.06856868f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 1, 4, 7, 2, -1, 5, 0, 3, 6, -1, 6, 3, 2, 7, -1, 0, 5, 4, 1, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0.865958f, 0.5001147f, 0.00144385546f, -0.495464861f),
					new float4(-0.7069787f, -0.7072344f, 0.000823315233f, -0.395199358f),
					new float4(0.546324253f, -0.712009668f, 0.44110322f, -0.135485321f),
					new float4(-0.6088042f, 0.7933205f, 0f, -0.217047349f),
					new float4(0.117396496f, -0.152993888f, -0.9812293f, -0.0197081212f),
					new float4(-0.12355414f, 0.160986215f, 0.979192436f, -0.01993645f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(-32.61704f, -14.4041576f, -88.73038f, 1f)), name: "Solid 2024570 #3844")
					.WithMesh(new float3[] { new float3(0.286994666f, 0.49383688f, -0.02461733f), new float3(0.460941166f, 0.192438856f, 0.04688316f), new float3(0.470441341f, 0.176099211f, 0.008742036f), new float3(0.2870387f, 0.4938707f, -0.0627478f), new float3(-0.224958986f, -0.333863258f, 0.04686435f), new float3(-0.471077263f, -0.087917164f, -0.0246260539f), new float3(-0.4711024f, -0.08793643f, -0.0627377853f), new float3(-0.211616784f, -0.347245f, 0.008739215f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 1, 4, 7, 2, -1, 5, 0, 3, 6, -1, 6, 3, 2, 7, -1, 0, 5, 4, 1, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0.707106769f, 0.707106769f, 0f, -0.495417118f),
					new float4(-0.4996527f, -0.8662258f, -0.000341871666f, -0.400427f),
					new float4(0.7102826f, -0.545011163f, 0.445490122f, -0.154201508f),
					new float4(-0.793261349f, 0.6088813f, 0f, -0.196869329f),
					new float4(0.1529884f, -0.117390446f, -0.981230855f, -0.0218056533f),
					new float4(-0.160993114f, 0.123537846f, 0.97919333f, -0.01766747f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(-33.2517662f, -15.028821f, -88.73223f, 1f)), name: "Solid 2024586 #3860")
					.WithMesh(new float3[] { new float3(0.163841039f, 0.5367846f, -0.02274164f), new float3(0.409744322f, 0.2908813f, 0.0487121753f), new float3(0.4232733f, 0.277352333f, 0.0105905114f), new float3(0.163841039f, 0.5367846f, -0.0608962253f), new float3(-0.1165775f, -0.395041823f, 0.0487155169f), new float3(-0.417946845f, -0.221179f, -0.0227688625f), new float3(-0.417938828f, -0.221168563f, -0.0609259f), new float3(-0.09999384f, -0.404592484f, 0.010590517f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 1, 4, 7, 2, -1, 5, 0, 3, 6, -1, 6, 3, 2, 7, -1, 0, 5, 4, 1, -1 }))
				;
		}

		// bm_c1a4a, Details: the mesh has a triangle the exact CSG did not make; Solid 2027340 #3968 face -1 near (-8.563,
		// -13.059, -85.575) (4 brushes)
		static ContentsScene BmC1a4aDetails27Site3()
		{
			return new ContentsScene()
				.WithContentsList("Solid", "Water", "Glass", "Grate")
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(-0.7094062f, 0.00246866979f, -0.704795539f, -0.37070173f),
					new float4(0.499451548f, 0.000199308488f, 0.8663418f, -0.376308084f),
					new float4(0.5629637f, 0.7045534f, -0.43206048f, -0.197602361f),
					new float4(-0.356920183f, -0.89309454f, 0.273843259f, -0.160226852f),
					new float4(-0.655568838f, 0.563176036f, 0.50305295f, -0.0189856663f),
					new float4(0.666733f, -0.5419707f, -0.5116003f, -0.0189969838f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(-8.710511f, -12.9280825f, -85.92493f, 1f)), name: "Solid 2027323 #3951")
					.WithMesh(new float3[] { new float3(-0.1397628f, 0.156198308f, -0.384746373f), new float3(-0.277903974f, -0.144003764f, -0.246753022f), new float3(-0.3048035f, -0.12493106f, -0.219610721f), new float3(-0.15653795f, 0.180007875f, -0.3677781f), new float3(0.1696844f, -0.14401865f, 0.336573452f), new float3(0.338866621f, 0.156243846f, 0.238969743f), new float3(0.318214566f, 0.180043474f, 0.250870317f), new float3(0.136601523f, -0.12495055f, 0.355641544f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 5, 0, 3, 6, -1, 1, 4, 7, 2, -1, 2, 7, 6, 3, -1, 4, 1, 0, 5, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(-0.7051993f, -0.0008710963f, -0.709008634f, -0.356276155f),
					new float4(0.500012159f, 0.000190525578f, 0.866018355f, -0.35765934f),
					new float4(0.35651812f, 0.893363535f, -0.273489326f, -0.195094645f),
					new float4(0f, -1f, 0f, -0.158860132f),
					new float4(-0.778889358f, 0.189762875f, 0.597763658f, -0.019366011f),
					new float4(0.777286649f, -0.199810013f, -0.5965747f, -0.019383762f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(-8.811373f, -13.26114f, -85.84508f, 1f)), name: "Solid 2027324 #3952")
					.WithMesh(new float3[] { new float3(-0.177106485f, 0.189084455f, -0.3265764f), new float3(-0.2276427f, -0.158860132f, -0.2758842f), new float3(-0.2546674f, -0.158860132f, -0.249004722f), new float3(-0.204160631f, 0.2081116f, -0.299691f), new float3(0.208645344f, -0.158860132f, 0.2925624f), new float3(0.2705679f, 0.189000577f, 0.256733716f), new float3(0.237502f, 0.2080395f, 0.275820732f), new float3(0.175603f, -0.158860132f, 0.311640024f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 5, 0, 3, 6, -1, 1, 4, 7, 2, -1, 2, 7, 6, 3, -1, 4, 1, 0, 5, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(-0.49839884f, -0.000925619737f, -0.866947353f, -0.371048242f),
					new float4(0.255960375f, 0.00176979217f, 0.966685653f, -0.3762691f),
					new float4(0.657231152f, 0.7028359f, -0.272155941f, -0.197508931f),
					new float4(-0.416160166f, -0.892803669f, 0.172372729f, -0.160301611f),
					new float4(-0.763360143f, 0.563290536f, 0.31620416f, -0.01896659f),
					new float4(0.776403069f, -0.5420403f, -0.321544051f, -0.0189835876f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(-8.336237f, -12.9280844f, -85.27813f, 1f)), name: "Solid 2027339 #3967")
					.WithMesh(new float3[] { new float3(-0.03539411f, 0.156199053f, -0.407813042f), new float3(-0.204604745f, -0.144069612f, -0.310215056f), new float3(-0.237695217f, -0.124976352f, -0.291212082f), new float3(-0.0560094826f, 0.18005611f, -0.395986944f), new float3(0.0767696f, -0.144057125f, 0.369172841f), new float3(0.2654578f, 0.156178057f, 0.318662047f), new float3(0.242480457f, 0.180003479f, 0.324702382f), new float3(0.03988045f, -0.12498302f, 0.378905475f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 5, 0, 3, 6, -1, 1, 4, 7, 2, -1, 2, 7, 6, 3, -1, 4, 1, 0, 5, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(-0.500310957f, -0.000201549876f, -0.8658458f, -0.3561318f),
					new float4(0.259910017f, -0.000247968943f, 0.9656328f, -0.357723534f),
					new float4(0.416184336f, 0.8927929f, -0.172370255f, -0.194999725f),
					new float4(0f, -1f, 0f, -0.158845827f),
					new float4(-0.907146752f, 0.189606145f, 0.37567848f, -0.01937382f),
					new float4(0.905271947f, -0.199671552f, -0.374985248f, -0.0193900671f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(-8.454296f, -13.2611542f, -85.22702f, 1f)), name: "Solid 2027340 #3968")
					.WithMesh(new float3[] { new float3(-0.0865660161f, 0.189006686f, -0.3613345f), new float3(-0.148445711f, -0.158845827f, -0.325497627f), new float3(-0.181457236f, -0.158845827f, -0.306422621f), new float3(-0.119600154f, 0.208090335f, -0.342250854f), new float3(0.125792652f, -0.158845827f, 0.336555868f), new float3(0.194850817f, 0.188990653f, 0.318057477f), new float3(0.157969862f, 0.2081006f, 0.32798925f), new float3(0.0889297351f, -0.158845827f, 0.3464779f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 5, 0, 3, 6, -1, 1, 4, 7, 2, -1, 2, 7, 6, 3, -1, 4, 1, 0, 5, -1 }))
				;
		}

		// bm_c1a4b, Details: the exact CSG made a triangle the mesh does not have; Solid 1421848 #1921 face -1 near (17.54,
		// 45.75, 17.54) (3 brushes)
		static ContentsScene BmC1a4bDetails35Site1()
		{
			return new ContentsScene()
				.WithContentsList("Solid", "Water", "Glass", "Grate")
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, -1f, 0f, -6.405001f),
					new float4(0.6086999f, 0f, -0.7934005f, -1.88441491f),
					new float4(-0.707106769f, 0f, 0.707106769f, -1.77533448f),
					new float4(-0.753316939f, 0f, -0.6576576f, -3.653444f),
					new float4(1f, 0f, 0f, -3.38888979f),
					new float4(0f, 1f, 0f, -10.0649986f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(21.01111f, 35.685f, 18.5004082f, 1f)), name: "Solid 1421848 #1921")
					.WithMesh(new float3[] { new float3(3.38888979f, -6.405001f, 0.224857464f), new float3(3.38888979f, -6.405001f, 5.899592f), new float3(-3.759548f, -6.405001f, -1.24884582f), new float3(-1.662673f, -6.405001f, -3.65072083f), new float3(-1.662673f, 10.0649986f, -3.65072083f), new float3(3.38888979f, 10.0649986f, 0.224857464f), new float3(3.38888979f, 10.0649986f, 5.899592f), new float3(-3.759548f, 10.0649986f, -1.24884582f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 0, 3, -1, 6, 7, 2, 1, -1, 7, 4, 3, 2, -1, 5, 6, 1, 0, -1, 6, 5, 4, 7, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0.70709914f, -1.44327532E-06f, -0.7071144f, -1.36696529f),
					new float4(0f, -1f, 0f, -1.48687363f),
					new float4(-0.162266389f, 0.9638207f, -0.211469382f, -1.63569784f),
					new float4(0.6036008f, 0.1299417f, 0.7866265f, -0.7142843f),
					new float4(-0.6034781f, -0.1314691f, -0.786466837f, -0.4872253f),
					new float4(-0.793337f, -1.495757E-06f, 0.6087827f, -1.84503245f),
					new float4(-0.646164536f, -0.197721869f, -0.7371414f, -0.496248454f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(16.3694687f, 47.2368736f, 18.3026447f, 1f)), name: "Solid 3961149 #3370")
					.WithMesh(new float3[] { new float3(0.8839426f, -1.48687363f, -1.04923344f), new float3(0.883942544f, -1.48687327f, -1.04923356f), new float3(0.600864947f, 1.50593889f, -1.33231115f), new float3(1.43659461f, 1.83000135f, -0.496600151f), new float3(1.74661613f, -1.48687363f, -0.1865786f), new float3(-1.51627326f, -1.48687363f, 1.05475163f), new float3(-0.906571f, -1.48687363f, 1.84928691f), new float3(-1.88452351f, 1.50593889f, 0.5747971f), new float3(-1.8845582f, 1.5059396f, 0.5748274f), new float3(-1.17120409f, 1.83000147f, 1.50443769f) }, new int[] { 0, 1, 2, 3, 4, -1, 5, 0, 4, 6, -1, 7, 8, 9, 3, 2, -1, 9, 6, 4, 3, -1, 1, 7, 2, -1, 8, 5, 6, 9, -1, 5, 8, 7, 1, 0, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(-0.70709914f, 5.84153349E-06f, 0.7071144f, -1.68644929f),
					new float4(0f, -1f, 0f, -1.48687363f),
					new float4(-0.211479321f, 0.9638173f, -0.162273541f, -1.64840722f),
					new float4(0.786635637f, 0.129938781f, 0.6035895f, -0.6669796f),
					new float4(-0.7864682f, -0.131464258f, -0.603477359f, -0.534485161f),
					new float4(0.6087703f, -3.12198881E-06f, -0.7933465f, -1.52941644f),
					new float4(-0.737154961f, -0.19770883f, -0.646153033f, -0.522708237f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(18.5328255f, 47.2368736f, 16.1478119f, 1f)), name: "Solid 3961152 #3373")
					.WithMesh(new float3[] { new float3(-1.5624733f, 1.50593889f, 0.822521746f), new float3(-1.2794143f, -1.48687363f, 1.1055994f), new float3(-0.4167496f, -1.48687363f, 1.96824551f), new float3(-0.7267567f, 1.83001292f, 1.65821767f), new float3(-1.27941382f, -1.48687363f, 1.10559893f), new float3(0.8247617f, -1.48687363f, -1.2949214f), new float3(1.61928773f, -1.48687363f, -0.685246f), new float3(0.344868064f, 1.505939f, -1.663177f), new float3(0.3448639f, 1.50593889f, -1.66317225f), new float3(1.27444541f, 1.83000135f, -0.949871957f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 2, 1, -1, 7, 8, 0, 3, 9, -1, 6, 9, 3, 2, -1, 8, 4, 1, 0, -1, 5, 7, 9, 6, -1, 7, 5, 4, 8, -1 }))
				;
		}

		// bm_c1a4c, Details: the exact CSG made a triangle the mesh does not have; Solid 60633 #1574 face -1 near (-83.265,
		// -6.71, -5.488) (9 brushes)
		static ContentsScene BmC1a4cDetails27Site1()
		{
			return new ContentsScene()
				.WithContentsList("Solid", "Water", "Glass", "Grate")
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, -0.9991141f, -0.0420829467f, -0.0256564412f),
					new float4(0f, -0.992928267f, 0.118715867f, -0.07240343f),
					new float4(0f, 1f, 0f, -0.01901146f),
					new float4(0.707127154f, 0.00222400459f, -0.707082868f, -0.7090864f),
					new float4(-1f, 0f, 0f, -0.6684576f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(-82.59654f, -6.72920227f, -4.42928f, 1f)), name: "Solid 60327 #1537")
					.WithMesh(new float3[] { new float3(-0.6684576f, -0.0379858278f, 0.292178541f), new float3(-0.6684576f, 0.01901146f, -1.061025f), new float3(-0.05824762f, 0.01901146f, -1.061025f), new float3(1.2950505f, -0.0379858278f, 0.292178541f), new float3(-0.6684576f, 0.01901146f, 0.7688984f), new float3(1.77156126f, 0.01901146f, 0.7688984f) }, new int[] { 0, 1, 2, 3, -1, 4, 0, 3, 5, -1, 1, 4, 5, 2, -1, 2, 5, 3, -1, 4, 1, 0, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 0.9991085f, 0.0422158539f, -0.06955365f),
					new float4(0f, -0.965226352f, -0.261415482f, -0.09944671f),
					new float4(0f, -0.917070031f, 0.3987261f, -0.197465166f),
					new float4(0.7071658f, -0.00027114572f, -0.70704776f, -0.57970345f),
					new float4(-1f, 0f, 0f, -0.546456f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(-82.718544f, -6.81175566f, -4.73387051f, 1f)), name: "Solid 60328 #1538")
					.WithMesh(new float3[] { new float3(-0.546456f, 0.101529576f, -0.7552947f), new float3(-0.546456f, 0.0443773456f, 0.59730804f), new float3(1.41698146f, 0.0443773456f, 0.59730804f), new float3(0.06462636f, 0.101529576f, -0.7552947f), new float3(-0.546456f, -0.146130085f, 0.15914093f), new float3(0.9788144f, -0.146130085f, 0.15914093f) }, new int[] { 0, 1, 2, 3, -1, 4, 0, 3, 5, -1, 1, 4, 5, 2, -1, 2, 5, 3, -1, 4, 1, 0, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 0.261421978f, 0.9652246f, -0.119508758f),
					new float4(0f, -0.04221939f, -0.9991084f, -0.0697314963f),
					new float4(0f, -0.39906168f, 0.916924059f, -0.158092842f),
					new float4(0.7074498f, 9.242827E-06f, -0.7067636f, -0.232886866f),
					new float4(-1f, 0f, 0f, -0.386299729f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(-82.8787f, -7.55616426f, -5.38464546f, 1f)), name: "Solid 60331 #1541")
					.WithMesh(new float3[] { new float3(-0.386299729f, 0.846991062f, -0.105585083f), new float3(-0.386299729f, -0.06883574f, 0.142457962f), new float3(0.471512765f, -0.06883574f, 0.142457962f), new float3(0.223698333f, 0.846991062f, -0.105585083f), new float3(-0.386299729f, -0.507273257f, -0.0483578481f), new float3(0.280887753f, -0.507273257f, -0.0483578481f) }, new int[] { 0, 1, 2, 3, -1, 4, 0, 3, 5, -1, 1, 4, 5, 2, -1, 2, 5, 3, -1, 4, 1, 0, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 0.04232194f, 0.999104f, -0.0309784971f),
					new float4(0f, -0.11981114f, 0.9927967f, -0.0584705919f),
					new float4(0f, 0f, -1f, -0.0191262439f),
					new float4(0.7082835f, 0f, -0.7059281f, -0.207026258f),
					new float4(-1f, 0f, 0f, -0.3367697f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(-82.92823f, -7.89349031f, -5.47106457f, 1f)), name: "Solid 60332 #1542")
					.WithMesh(new float3[] { new float3(-0.3367697f, 1.18349028f, -0.0191262439f), new float3(-0.3367697f, -0.171053082f, 0.0382520668f), new float3(0.330417782f, -0.171053082f, 0.0382520668f), new float3(0.273230284f, 1.18349028f, -0.0191262439f), new float3(-0.3367697f, -0.6465097f, -0.0191262439f), new float3(0.273230284f, -0.6465097f, -0.0191262439f) }, new int[] { 0, 1, 2, 3, -1, 1, 4, 5, 2, -1, 4, 0, 3, 5, -1, 5, 3, 2, -1, 0, 4, 1, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 0.9991085f, 0.0422158539f, -0.06965452f),
					new float4(0f, -0.965226352f, -0.261415482f, -0.099537015f),
					new float4(0f, -0.917070031f, 0.3987261f, -0.1973655f),
					new float4(-1f, 0f, 0f, -3.903998f),
					new float4(1f, 0f, 0f, -5.85600233f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(-89.121f, -6.81166649f, -4.73385429f, 1f)), name: "Solid 60633 #1574")
					.WithMesh(new float3[] { new float3(5.85600233f, 0.0444789864f, 0.5972918f), new float3(5.85600233f, 0.101666488f, -0.7561457f), new float3(-3.903998f, 0.101666488f, -0.7561457f), new float3(-3.903998f, 0.0444789864f, 0.5972918f), new float3(5.85600233f, -0.146146014f, 0.1588543f), new float3(-3.903998f, -0.146146014f, 0.1588543f) }, new int[] { 0, 1, 2, 3, -1, 1, 4, 5, 2, -1, 4, 0, 3, 5, -1, 5, 3, 2, -1, 0, 4, 1, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 0.965226352f, 0.261415482f, -0.125219852f),
					new float4(0f, -0.707106769f, -0.707106769f, -0.157257438f),
					new float4(0f, -0.8f, 0.6f, -0.249083266f),
					new float4(-1f, 0f, 0f, -3.903998f),
					new float4(1f, 0f, 0f, -5.85600233f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(-89.121f, -6.970521f, -5.00708342f, 1f)), name: "Solid 60634 #1575")
					.WithMesh(new float3[] { new float3(5.85600233f, 0.0127084814f, 0.4320834f), new float3(5.85600233f, 0.260521f, -0.4829166f), new float3(-3.903998f, 0.260521f, -0.4829166f), new float3(-3.903998f, 0.0127084814f, 0.4320834f), new float3(5.85600233f, -0.273229f, 0.05083341f), new float3(-3.903998f, -0.273229f, 0.05083341f) }, new int[] { 0, 1, 2, 3, -1, 1, 4, 5, 2, -1, 4, 0, 3, 5, -1, 5, 3, 2, -1, 0, 4, 1, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 0.707409739f, 0.7068037f, -0.188706324f),
					new float4(0f, -0.261537224f, -0.9651934f, -0.100190796f),
					new float4(0f, -0.6f, 0.8f, -0.249004275f),
					new float4(-1f, 0f, 0f, -4.554663f),
					new float4(1f, 0f, 0f, -5.205337f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(-88.47034f, -7.21846533f, -5.248542f, 1f)), name: "Solid 60635 #1576")
					.WithMesh(new float3[] { new float3(5.205337f, -0.02528467f, 0.29229185f), new float3(5.205337f, 0.508008f, -0.241458148f), new float3(-4.554663f, 0.508008f, -0.241458148f), new float3(-4.554663f, -0.02528467f, 0.29229185f), new float3(5.205337f, -0.406534672f, 0.00635432638f), new float3(-4.554663f, -0.406534672f, 0.00635432638f) }, new int[] { 0, 1, 2, 3, -1, 1, 4, 5, 2, -1, 4, 0, 3, 5, -1, 5, 3, 2, -1, 0, 4, 1, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 0.261442244f, 0.96521914f, -0.119488165f),
					new float4(0f, -0.0422169976f, -0.9991085f, -0.06974412f),
					new float4(0f, -0.39906168f, 0.916924059f, -0.158090174f),
					new float4(-1f, 0f, 0f, -5.205336f),
					new float4(1f, 0f, 0f, -4.554664f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(-87.819664f, -7.55614138f, -5.38463259f, 1f)), name: "Solid 60636 #1577")
					.WithMesh(new float3[] { new float3(4.554664f, -0.0688586459f, 0.142445087f), new float3(4.554664f, 0.8468637f, -0.105590291f), new float3(-5.205336f, 0.8468637f, -0.105590291f), new float3(-5.205336f, -0.0688586459f, 0.142445087f), new float3(4.554664f, -0.507296145f, -0.04837072f), new float3(-5.205336f, -0.507296145f, -0.04837072f) }, new int[] { 0, 1, 2, 3, -1, 1, 4, 5, 2, -1, 4, 0, 3, 5, -1, 5, 3, 2, -1, 0, 4, 1, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 0.0423934162f, 0.999101f, -0.0309556127f),
					new float4(0f, -0.118854582f, 0.9929117f, -0.0580514632f),
					new float4(0f, -0.000104268394f, -1f, -0.019079214f),
					new float4(-1f, 0f, 0f, -5.205336f),
					new float4(1f, 0f, 0f, -4.554664f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(-87.819664f, -7.89275169f, -5.47098827f, 1f)), name: "Solid 60637 #1578")
					.WithMesh(new float3[] { new float3(4.554664f, -0.169503689f, 0.0381757729f), new float3(4.554664f, 1.18275177f, -0.0192025378f), new float3(-5.205336f, 1.18275177f, -0.0192025378f), new float3(-5.205336f, -0.169503689f, 0.0381757729f), new float3(4.554664f, -0.6472483f, -0.0190117266f), new float3(-5.205336f, -0.6472483f, -0.0190117266f) }, new int[] { 0, 1, 2, 3, -1, 4, 0, 3, 5, -1, 1, 4, 5, 2, -1, 2, 5, 3, -1, 4, 1, 0, -1 }))
				;
		}

		// bm_c1a4c, Details: the exact CSG made a triangle the mesh does not have; Solid 60328 #1538 face -1 near (-83.061,
		// -6.71, -5.489) (6 brushes)
		static ContentsScene BmC1a4cDetails27Site2()
		{
			return new ContentsScene()
				.WithContentsList("Solid", "Water", "Glass", "Grate")
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, -0.9991141f, -0.0420829467f, -0.0256564412f),
					new float4(0f, -0.992928267f, 0.118715867f, -0.07240343f),
					new float4(0f, 1f, 0f, -0.01901146f),
					new float4(0.707127154f, 0.00222400459f, -0.707082868f, -0.7090864f),
					new float4(-1f, 0f, 0f, -0.6684576f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(-82.59654f, -6.72920227f, -4.42928f, 1f)), name: "Solid 60327 #1537")
					.WithMesh(new float3[] { new float3(-0.6684576f, -0.0379858278f, 0.292178541f), new float3(-0.6684576f, 0.01901146f, -1.061025f), new float3(-0.05824762f, 0.01901146f, -1.061025f), new float3(1.2950505f, -0.0379858278f, 0.292178541f), new float3(-0.6684576f, 0.01901146f, 0.7688984f), new float3(1.77156126f, 0.01901146f, 0.7688984f) }, new int[] { 0, 1, 2, 3, -1, 4, 0, 3, 5, -1, 1, 4, 5, 2, -1, 2, 5, 3, -1, 4, 1, 0, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 0.9991085f, 0.0422158539f, -0.06955365f),
					new float4(0f, -0.965226352f, -0.261415482f, -0.09944671f),
					new float4(0f, -0.917070031f, 0.3987261f, -0.197465166f),
					new float4(0.7071658f, -0.00027114572f, -0.70704776f, -0.57970345f),
					new float4(-1f, 0f, 0f, -0.546456f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(-82.718544f, -6.81175566f, -4.73387051f, 1f)), name: "Solid 60328 #1538")
					.WithMesh(new float3[] { new float3(-0.546456f, 0.101529576f, -0.7552947f), new float3(-0.546456f, 0.0443773456f, 0.59730804f), new float3(1.41698146f, 0.0443773456f, 0.59730804f), new float3(0.06462636f, 0.101529576f, -0.7552947f), new float3(-0.546456f, -0.146130085f, 0.15914093f), new float3(0.9788144f, -0.146130085f, 0.15914093f) }, new int[] { 0, 1, 2, 3, -1, 4, 0, 3, 5, -1, 1, 4, 5, 2, -1, 2, 5, 3, -1, 4, 1, 0, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0.04234454f, 0.999103069f, 0f, -0.06960971f),
					new float4(-0.2615014f, -0.9652031f, 0f, -0.07969989f),
					new float4(0.3987261f, -0.917070031f, 0f, -0.236851975f),
					new float4(-0.7069303f, 0.00102790294f, 0.7072825f, -0.7173338f),
					new float4(0f, 0f, -1f, -0.871840954f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(-81.9892349f, -6.807905f, -5.83854f, 1f)), name: "Solid 60343 #1553")
					.WithMesh(new float3[] { new float3(0.687272549f, 0.0405438319f, -0.871840954f), new float3(-0.6661467f, 0.0979051962f, -0.871840954f), new float3(-0.6661467f, 0.0979051962f, 0.348253936f), new float3(0.687272549f, 0.0405438319f, 1.70108259f), new float3(0.248964548f, -0.150024861f, -0.871840954f), new float3(0.248964548f, -0.150024861f, 1.26326978f) }, new int[] { 0, 1, 2, 3, -1, 1, 4, 5, 2, -1, 4, 0, 3, 5, -1, 5, 3, 2, -1, 0, 4, 1, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 0.9991085f, 0.0422158539f, -0.06965452f),
					new float4(0f, -0.965226352f, -0.261415482f, -0.099537015f),
					new float4(0f, -0.917070031f, 0.3987261f, -0.1973655f),
					new float4(-1f, 0f, 0f, -3.903998f),
					new float4(1f, 0f, 0f, -5.85600233f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(-89.121f, -6.81166649f, -4.73385429f, 1f)), name: "Solid 60633 #1574")
					.WithMesh(new float3[] { new float3(5.85600233f, 0.0444789864f, 0.5972918f), new float3(5.85600233f, 0.101666488f, -0.7561457f), new float3(-3.903998f, 0.101666488f, -0.7561457f), new float3(-3.903998f, 0.0444789864f, 0.5972918f), new float3(5.85600233f, -0.146146014f, 0.1588543f), new float3(-3.903998f, -0.146146014f, 0.1588543f) }, new int[] { 0, 1, 2, 3, -1, 1, 4, 5, 2, -1, 4, 0, 3, 5, -1, 5, 3, 2, -1, 0, 4, 1, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 0.965226352f, 0.261415482f, -0.125219852f),
					new float4(0f, -0.707106769f, -0.707106769f, -0.157257438f),
					new float4(0f, -0.8f, 0.6f, -0.249083266f),
					new float4(-1f, 0f, 0f, -3.903998f),
					new float4(1f, 0f, 0f, -5.85600233f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(-89.121f, -6.970521f, -5.00708342f, 1f)), name: "Solid 60634 #1575")
					.WithMesh(new float3[] { new float3(5.85600233f, 0.0127084814f, 0.4320834f), new float3(5.85600233f, 0.260521f, -0.4829166f), new float3(-3.903998f, 0.260521f, -0.4829166f), new float3(-3.903998f, 0.0127084814f, 0.4320834f), new float3(5.85600233f, -0.273229f, 0.05083341f), new float3(-3.903998f, -0.273229f, 0.05083341f) }, new int[] { 0, 1, 2, 3, -1, 1, 4, 5, 2, -1, 4, 0, 3, 5, -1, 5, 3, 2, -1, 0, 4, 1, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 0.261442244f, 0.96521914f, -0.119488165f),
					new float4(0f, -0.0422169976f, -0.9991085f, -0.06974412f),
					new float4(0f, -0.39906168f, 0.916924059f, -0.158090174f),
					new float4(-1f, 0f, 0f, -5.205336f),
					new float4(1f, 0f, 0f, -4.554664f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(-87.819664f, -7.55614138f, -5.38463259f, 1f)), name: "Solid 60636 #1577")
					.WithMesh(new float3[] { new float3(4.554664f, -0.0688586459f, 0.142445087f), new float3(4.554664f, 0.8468637f, -0.105590291f), new float3(-5.205336f, 0.8468637f, -0.105590291f), new float3(-5.205336f, -0.0688586459f, 0.142445087f), new float3(4.554664f, -0.507296145f, -0.04837072f), new float3(-5.205336f, -0.507296145f, -0.04837072f) }, new int[] { 0, 1, 2, 3, -1, 1, 4, 5, 2, -1, 4, 0, 3, 5, -1, 5, 3, 2, -1, 0, 4, 1, -1 }))
				;
		}

		// bm_c1a4d, Details: the mesh has a triangle the exact CSG did not make; Solid 127043 #719 face -1 near (34.821,
		// -23.79, 71.014) (7 brushes)
		static ContentsScene BmC1a4dDetails30Site1()
		{
			return new ContentsScene()
				.WithContentsList("Solid", "Water", "Glass", "Grate")
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 1f, 0f, -0.0593061075f),
					new float4(0f, -1f, 0f, -0.0931938961f),
					new float4(-1f, 0f, 0f, -0.169445336f),
					new float4(1f, 0f, 0f, -0.135554656f),
					new float4(0f, 0f, 1f, -1.22f),
					new float4(0f, 0f, -1f, -1.22f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(34.7869453f, -23.8493061f, 76.25f, 1f)), name: "Solid 131665 #512")
					.WithMesh(new float3[] { new float3(0.135554656f, 0.0593061075f, -1.22f), new float3(-0.169445336f, 0.0593061075f, -1.22f), new float3(-0.169445336f, 0.0593061075f, 1.22f), new float3(0.135554656f, 0.0593061075f, 1.22f), new float3(-0.169445336f, -0.0931938961f, -1.22f), new float3(0.135554656f, -0.0931938961f, -1.22f), new float3(0.135554656f, -0.0931938961f, 1.22f), new float3(-0.169445336f, -0.0931938961f, 1.22f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 1, 4, 7, 2, -1, 5, 0, 3, 6, -1, 6, 3, 2, 7, -1, 0, 5, 4, 1, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 0f, 1f, -0.248894036f),
					new float4(0.258777559f, -3.309111E-05f, -0.965936959f, -0.312214077f),
					new float4(-0.991451263f, 0f, -0.130477756f, -0.26985395f),
					new float4(0.9605039f, -0.247871965f, 0.1264587f, -0.261161149f),
					new float4(0.120316245f, 0.992609f, 0.01585287f, -0.369866639f),
					new float4(-0.119130574f, -0.9927548f, -0.0156779177f, -0.235336661f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(35.0749359f, -23.5856075f, 52.821106f, 1f)), name: "Solid 110435 #557")
					.WithMesh(new float3[] { new float3(-0.3049359f, -0.204392478f, 0.248894036f), new float3(0.171626568f, -0.261579961f, 0.248894036f), new float3(0.324126571f, 0.329357535f, 0.248894036f), new float3(-0.3049359f, 0.405607522f, 0.248894036f), new float3(0.238535315f, -0.261583269f, -0.259310752f), new float3(-0.221823692f, -0.204392478f, -0.382644325f), new float3(-0.221821025f, 0.405619562f, -0.382664531f), new float3(0.3858444f, 0.3293631f, -0.219866425f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 5, 0, 3, 6, -1, 1, 4, 7, 2, -1, 2, 7, 6, 3, -1, 4, 1, 0, 5, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 0f, 1f, -1.13866591f),
					new float4(0f, 0f, -1f, -1.301334f),
					new float4(-0.0422158539f, -0.9991085f, 0f, -0.0205933321f),
					new float4(0.11928647f, -0.9928599f, 0f, -0.0872930437f),
					new float4(0f, 1f, 0f, -0.0190629587f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(33.87954f, -23.809063f, 54.3713341f, 1f)), name: "Solid 125303 #676")
					.WithMesh(new float3[] { new float3(0.8904605f, 0.0190629587f, 1.13866591f), new float3(-0.938967049f, 0.0190629587f, 1.13866591f), new float3(0.414470434f, -0.0381245427f, 1.13866591f), new float3(-0.938967049f, 0.0190629587f, -1.301334f), new float3(0.8904605f, 0.0190629587f, -1.301334f), new float3(0.414470434f, -0.0381245427f, -1.301334f) }, new int[] { 0, 1, 2, -1, 3, 4, 5, -1, 1, 3, 5, 2, -1, 4, 0, 2, 5, -1, 0, 4, 3, 1, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 0f, 1f, -1.13866544f),
					new float4(0f, 0f, -1f, -1.3013345f),
					new float4(-0.0422158539f, -0.9991085f, 0f, -0.0205986556f),
					new float4(0.119239353f, -0.992865562f, 0f, -0.08725869f),
					new float4(0f, 1f, 0f, -0.0190629587f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(33.8790932f, -23.809063f, 73.8913345f, 1f)), name: "Solid 125407 #712")
					.WithMesh(new float3[] { new float3(0.890525162f, 0.0190629587f, 1.13866544f), new float3(-0.9390932f, 0.0190629587f, 1.13866544f), new float3(0.414344281f, -0.0381245427f, 1.13866544f), new float3(-0.9390932f, 0.0190629587f, -1.3013345f), new float3(0.890525162f, 0.0190629587f, -1.3013345f), new float3(0.414344281f, -0.0381245427f, -1.3013345f) }, new int[] { 0, 1, 2, -1, 3, 4, 5, -1, 1, 3, 5, 2, -1, 4, 0, 2, 5, -1, 0, 4, 3, 1, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 1f, 0f, -0.0931940451f),
					new float4(0f, -1f, 0f, -0.0593059547f),
					new float4(-1f, 0f, 0f, -0.135555565f),
					new float4(1f, 0f, 0f, -0.169444427f),
					new float4(0f, 0f, 1f, -10.9799967f),
					new float4(0f, 0f, -1f, -10.9800034f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(34.7530556f, -23.8830032f, 64.05f, 1f)), name: "Solid 127043 #719")
					.WithMesh(new float3[] { new float3(0.169444427f, 0.0931940451f, -10.9800034f), new float3(-0.135555565f, 0.0931940451f, -10.9800034f), new float3(-0.135555565f, 0.0931940451f, 10.9799967f), new float3(0.169444427f, 0.0931940451f, 10.9799967f), new float3(-0.135555565f, -0.0593059547f, -10.9800034f), new float3(0.169444427f, -0.0593059547f, -10.9800034f), new float3(0.169444427f, -0.0593059547f, 10.9799967f), new float3(-0.135555565f, -0.0593059547f, 10.9799967f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 1, 4, 7, 2, -1, 5, 0, 3, 6, -1, 6, 3, 2, 7, -1, 0, 5, 4, 1, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 1f, 0f, -0.09319397f),
					new float4(0f, -1f, 0f, -0.05930603f),
					new float4(1f, 0f, 0f, -1.08444548f),
					new float4(0f, 0f, -1f, -0.152496338f),
					new float4(0f, 0f, 1f, -0.152503669f),
					new float4(-1f, 0f, 0f, -1.35555446f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(33.6855545f, -23.883194f, 72.59f, 1f)), name: "Solid 470521 #1550")
					.WithMesh(new float3[] { new float3(-1.35555446f, 0.09319397f, -0.152496338f), new float3(-1.35555446f, 0.09319397f, 0.152503669f), new float3(1.08444548f, 0.09319397f, 0.152503669f), new float3(1.08444548f, 0.09319397f, -0.152496338f), new float3(-1.35555446f, -0.05930603f, 0.152503669f), new float3(-1.35555446f, -0.05930603f, -0.152496338f), new float3(1.08444548f, -0.05930603f, -0.152496338f), new float3(1.08444548f, -0.05930603f, 0.152503669f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 2, 7, 6, 3, -1, 5, 0, 3, 6, -1, 1, 4, 7, 2, -1, 4, 1, 0, 5, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 0f, 1f, -6.50666761f),
					new float4(0.0422158539f, -0.9991085f, 0f, -0.02575224f),
					new float4(-0.119145222f, -0.9928768f, 0f, -0.07267523f),
					new float4(0f, 1f, 0f, -0.0190629587f),
					new float4(0f, 0f, -1f, -5.693332f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(35.53883f, -23.809063f, 58.7633324f, 1f)), name: "Solid 2144450 #2813")
					.WithMesh(new float3[] { new float3(1.06117022f, 0.0190629587f, 6.50666761f), new float3(-0.7688298f, 0.0190629587f, 6.50666761f), new float3(-0.2922673f, -0.0381245427f, 6.50666761f), new float3(-0.2922673f, -0.0381245427f, -5.693332f), new float3(1.06117022f, 0.0190629587f, -5.693332f), new float3(-0.7688298f, 0.0190629587f, -5.693332f) }, new int[] { 0, 1, 2, -1, 3, 4, 0, 2, -1, 5, 3, 2, 1, -1, 4, 5, 1, 0, -1, 5, 4, 3, -1 }))
				;
		}

		// bm_c1a4d, Details: the mesh has a triangle the exact CSG did not make; Solid 95981 #718 face -1 near (38.837,
		// -23.79, 50.529) (5 brushes)
		static ContentsScene BmC1a4dDetails30Site2()
		{
			return new ContentsScene()
				.WithContentsList("Solid", "Water", "Glass", "Grate")
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(1f, 0f, 0f, -1.03700018f),
					new float4(0f, -0.9991088f, -0.0422099121f, -0.0257513039f),
					new float4(0f, -0.9928768f, 0.119145222f, -0.0726828f),
					new float4(0f, 1f, 0f, -0.0190629587f),
					new float4(-1f, 0f, 0f, -1.55549991f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(50.9655f, -23.809063f, 49.86168f, 1f)), name: "Solid 137881 #533")
					.WithMesh(new float3[] { new float3(1.03700018f, 0.0190629587f, -1.06129754f), new float3(1.03700018f, 0.0190629587f, 0.768893361f), new float3(1.03700018f, -0.03812454f, 0.292330861f), new float3(-1.55549991f, -0.03812454f, 0.292330861f), new float3(-1.55549991f, 0.0190629587f, -1.06129754f), new float3(-1.55549991f, 0.0190629587f, 0.768893361f) }, new int[] { 0, 1, 2, -1, 3, 4, 0, 2, -1, 5, 3, 2, 1, -1, 4, 5, 1, 0, -1, 5, 4, 3, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(-1f, 0f, 0f, -1.95199871f),
					new float4(0f, -0.9991088f, -0.0422099121f, -0.0257513039f),
					new float4(0f, -0.9928768f, 0.119145222f, -0.0726828f),
					new float4(0f, 1f, 0f, -0.0190629587f),
					new float4(1f, 0f, 0f, -2.92800117f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(39.162f, -23.809063f, 49.86168f, 1f)), name: "Solid 137882 #534")
					.WithMesh(new float3[] { new float3(-1.95199871f, 0.0190629587f, 0.768893361f), new float3(-1.95199871f, 0.0190629587f, -1.06129754f), new float3(-1.95199871f, -0.03812454f, 0.292330861f), new float3(2.92800117f, 0.0190629587f, -1.06129754f), new float3(2.92800117f, -0.03812454f, 0.292330861f), new float3(2.92800117f, 0.0190629587f, 0.768893361f) }, new int[] { 0, 1, 2, -1, 3, 4, 2, 1, -1, 4, 5, 0, 2, -1, 5, 3, 1, 0, -1, 3, 5, 4, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 1f, 0f, -0.0931940451f),
					new float4(0f, -1f, 0f, -0.0593059547f),
					new float4(-1f, 0f, 0f, -6.574443f),
					new float4(1f, 0f, 0f, -8.218057f),
					new float4(0f, 0f, 1f, -0.152499154f),
					new float4(0f, 0f, -1f, -0.152500838f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(43.7844429f, -23.8830032f, 50.6305733f, 1f)), name: "Solid 95981 #718")
					.WithMesh(new float3[] { new float3(8.218057f, 0.0931940451f, -0.152500838f), new float3(-6.574443f, 0.0931940451f, -0.152500838f), new float3(-6.574443f, 0.0931940451f, 0.152499154f), new float3(8.218057f, 0.0931940451f, 0.152499154f), new float3(-6.574443f, -0.0593059547f, -0.152500838f), new float3(8.218057f, -0.0593059547f, -0.152500838f), new float3(8.218057f, -0.0593059547f, 0.152499154f), new float3(-6.574443f, -0.0593059547f, 0.152499154f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 1, 4, 7, 2, -1, 5, 0, 3, 6, -1, 6, 3, 2, 7, -1, 0, 5, 4, 1, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 1f, 0f, -0.0593061075f),
					new float4(0f, -1f, 0f, -0.0931938961f),
					new float4(0f, 0f, 1f, -2.64997387f),
					new float4(1f, 0f, 0f, -0.152499855f),
					new float4(-1f, 0f, 0f, -0.152500153f),
					new float4(0.478966624f, 0f, -0.8778331f, -2.318117f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(42.09f, -23.8493061f, 47.9800262f, 1f)), name: "Solid 470365 #1462")
					.WithMesh(new float3[] { new float3(0.152499855f, 0.0593061075f, -2.55751872f), new float3(-0.152500153f, 0.0593061075f, -2.723934f), new float3(-0.152500153f, 0.0593061075f, 2.64997387f), new float3(0.152499855f, 0.0593061075f, 2.64997387f), new float3(-0.152500153f, -0.0931938961f, -2.723934f), new float3(0.152499855f, -0.0931938961f, -2.55751872f), new float3(0.152499855f, -0.0931938961f, 2.64997387f), new float3(-0.152500153f, -0.0931938961f, 2.64997387f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 2, 7, 6, 3, -1, 5, 0, 3, 6, -1, 1, 4, 7, 2, -1, 4, 1, 0, 5, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(-1f, 0f, 0f, -0.169443965f),
					new float4(1f, 0f, 0f, -0.135556027f),
					new float4(0f, -0.257662654f, 0.9662349f, -0.232284173f),
					new float4(0f, 0f, -1f, -0.236161351f),
					new float4(0f, 0.989203453f, 0.146548659f, -0.18224214f),
					new float4(0f, -0.9916004f, -0.129339188f, -0.115606323f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(37.226944f, -23.8567181f, 50.86616f, 1f)), name: "Solid 471208 #1605")
					.WithMesh(new float3[] { new float3(-0.169443965f, -0.08578194f, -0.236161351f), new float3(-0.169443965f, -0.142969444f, 0.202276155f), new float3(-0.169443965f, 0.142968073f, 0.278526157f), new float3(-0.169443965f, 0.21921806f, -0.236161351f), new float3(0.135556027f, -0.142969444f, 0.202276155f), new float3(0.135556027f, -0.08578194f, -0.236161351f), new float3(0.135556027f, 0.21921806f, -0.236161351f), new float3(0.135556027f, 0.142968073f, 0.278526157f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 1, 4, 7, 2, -1, 5, 0, 3, 6, -1, 6, 3, 2, 7, -1, 0, 5, 4, 1, -1 }))
				;
		}

		// bm_c1a4d, Details: the mesh has a triangle the exact CSG did not make; Solid 95981 #718 face -1 near (44.581,
		// -23.79, 50.63) (6 brushes)
		static ContentsScene BmC1a4dDetails30Site3()
		{
			return new ContentsScene()
				.WithContentsList("Solid", "Water", "Glass", "Grate")
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(1f, 0f, 0f, -1.03700018f),
					new float4(0f, -0.9991088f, -0.0422099121f, -0.0257513039f),
					new float4(0f, -0.9928768f, 0.119145222f, -0.0726828f),
					new float4(0f, 1f, 0f, -0.0190629587f),
					new float4(-1f, 0f, 0f, -1.55549991f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(50.9655f, -23.809063f, 49.86168f, 1f)), name: "Solid 137881 #533")
					.WithMesh(new float3[] { new float3(1.03700018f, 0.0190629587f, -1.06129754f), new float3(1.03700018f, 0.0190629587f, 0.768893361f), new float3(1.03700018f, -0.03812454f, 0.292330861f), new float3(-1.55549991f, -0.03812454f, 0.292330861f), new float3(-1.55549991f, 0.0190629587f, -1.06129754f), new float3(-1.55549991f, 0.0190629587f, 0.768893361f) }, new int[] { 0, 1, 2, -1, 3, 4, 0, 2, -1, 5, 3, 2, 1, -1, 4, 5, 1, 0, -1, 5, 4, 3, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(-1f, 0f, 0f, -1.95199871f),
					new float4(0f, -0.9991088f, -0.0422099121f, -0.0257513039f),
					new float4(0f, -0.9928768f, 0.119145222f, -0.0726828f),
					new float4(0f, 1f, 0f, -0.0190629587f),
					new float4(1f, 0f, 0f, -2.92800117f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(39.162f, -23.809063f, 49.86168f, 1f)), name: "Solid 137882 #534")
					.WithMesh(new float3[] { new float3(-1.95199871f, 0.0190629587f, 0.768893361f), new float3(-1.95199871f, 0.0190629587f, -1.06129754f), new float3(-1.95199871f, -0.03812454f, 0.292330861f), new float3(2.92800117f, 0.0190629587f, -1.06129754f), new float3(2.92800117f, -0.03812454f, 0.292330861f), new float3(2.92800117f, 0.0190629587f, 0.768893361f) }, new int[] { 0, 1, 2, -1, 3, 4, 2, 1, -1, 4, 5, 0, 2, -1, 5, 3, 1, 0, -1, 3, 5, 4, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 1f, 0f, -0.0931940451f),
					new float4(0f, -1f, 0f, -0.0593059547f),
					new float4(-1f, 0f, 0f, -6.574443f),
					new float4(1f, 0f, 0f, -8.218057f),
					new float4(0f, 0f, 1f, -0.152499154f),
					new float4(0f, 0f, -1f, -0.152500838f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(43.7844429f, -23.8830032f, 50.6305733f, 1f)), name: "Solid 95981 #718")
					.WithMesh(new float3[] { new float3(8.218057f, 0.0931940451f, -0.152500838f), new float3(-6.574443f, 0.0931940451f, -0.152500838f), new float3(-6.574443f, 0.0931940451f, 0.152499154f), new float3(8.218057f, 0.0931940451f, 0.152499154f), new float3(-6.574443f, -0.0593059547f, -0.152500838f), new float3(8.218057f, -0.0593059547f, -0.152500838f), new float3(8.218057f, -0.0593059547f, 0.152499154f), new float3(-6.574443f, -0.0593059547f, 0.152499154f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 1, 4, 7, 2, -1, 5, 0, 3, 6, -1, 6, 3, 2, 7, -1, 0, 5, 4, 1, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 1f, 0f, -0.0593061075f),
					new float4(0f, -1f, 0f, -0.0931938961f),
					new float4(-1f, 0f, 0f, -0.1355539f),
					new float4(1f, 0f, 0f, -0.169446111f),
					new float4(0f, 0f, 1f, -2.19273663f),
					new float4(0.0927232057f, 0f, -0.9956919f, -2.183293f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(44.5130539f, -23.8493061f, 48.4372635f, 1f)), name: "Solid 470354 #1452")
					.WithMesh(new float3[] { new float3(0.169446111f, 0.0593061075f, -2.17696f), new float3(-0.1355539f, 0.0593061075f, -2.205363f), new float3(-0.1355539f, 0.0593061075f, 2.19273663f), new float3(0.169446111f, 0.0593061075f, 2.19273663f), new float3(-0.1355539f, -0.0931938961f, -2.205363f), new float3(0.169446111f, -0.0931938961f, -2.17696f), new float3(0.169446111f, -0.0931938961f, 2.19273663f), new float3(-0.1355539f, -0.0931938961f, 2.19273663f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 1, 4, 7, 2, -1, 5, 0, 3, 6, -1, 6, 3, 2, 7, -1, 0, 5, 4, 1, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(-1f, 0f, 0f, -0.169443965f),
					new float4(1f, 0f, 0f, -0.135556027f),
					new float4(0f, -0.257662654f, 0.9662349f, -0.232284173f),
					new float4(0f, 0f, -1f, -0.236161351f),
					new float4(0f, 0.989203453f, 0.146548659f, -0.18224214f),
					new float4(0f, -0.9916004f, -0.129339188f, -0.115606323f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(37.226944f, -23.8567181f, 50.86616f, 1f)), name: "Solid 471208 #1605")
					.WithMesh(new float3[] { new float3(-0.169443965f, -0.08578194f, -0.236161351f), new float3(-0.169443965f, -0.142969444f, 0.202276155f), new float3(-0.169443965f, 0.142968073f, 0.278526157f), new float3(-0.169443965f, 0.21921806f, -0.236161351f), new float3(0.135556027f, -0.142969444f, 0.202276155f), new float3(0.135556027f, -0.08578194f, -0.236161351f), new float3(0.135556027f, 0.21921806f, -0.236161351f), new float3(0.135556027f, 0.142968073f, 0.278526157f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 1, 4, 7, 2, -1, 5, 0, 3, 6, -1, 6, 3, 2, 7, -1, 0, 5, 4, 1, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(-1f, 0f, 0f, -6.344001f),
					new float4(1.01550938E-06f, -0.999109f, 0.042203974f, -0.0257450789f),
					new float4(0f, -0.9928768f, -0.119145222f, -0.07268712f),
					new float4(0f, 1f, 0f, -0.0190629587f),
					new float4(1f, 0f, 0f, -9.515999f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(43.554f, -23.809063f, 51.39893f, 1f)), name: "Solid 2134652 #2775")
					.WithMesh(new float3[] { new float3(-6.344001f, 0.0190629587f, 1.061452f), new float3(-6.344001f, 0.0190629587f, -0.7689296f), new float3(-6.344001f, -0.0381245427f, -0.2923671f), new float3(9.515999f, -0.038112618f, -0.292466462f), new float3(9.515999f, 0.0190629587f, 1.06107044f), new float3(9.515999f, 0.0190629587f, -0.7689296f) }, new int[] { 0, 1, 2, -1, 3, 4, 0, 2, -1, 5, 3, 2, 1, -1, 4, 5, 1, 0, -1, 5, 4, 3, -1 }))
				;
		}

		// bm_c1a4d, Details: the mesh has a triangle the exact CSG did not make; Solid 1086589 #2592 face -1 near (26.772,
		// -10.256, 73.541) (5 brushes)
		static ContentsScene BmC1a4dDetails30Site4()
		{
			return new ContentsScene()
				.WithContentsList("Solid", "Water", "Glass", "Grate")
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 1f, 0f, -0.0127083967f),
					new float4(-0.09560418f, -0.970146656f, -0.222879484f, -0.0246604942f),
					new float4(0.09557719f, -0.9701186f, 0.223013431f, -0.0246592574f),
					new float4(-0.8821432f, -2.8746419E-05f, 0.47098133f, -0.0616372973f),
					new float4(0.9097442f, 0.00101117045f, -0.415168017f, -0.092824474f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(26.6917439f, -10.2302084f, 73.57479f, 1f)), name: "Solid 1086597 #2096")
					.WithMesh(new float3[] { new float3(0.148637772f, 0.0127083967f, 0.102153167f), new float3(0.0219791122f, 0.0127083967f, -0.175389841f), new float3(-0.1289489f, 0.0127083967f, -0.11064925f), new float3(0.0152002741f, 0.0127083967f, 0.159340665f), new float3(0.08535925f, -0.025422791f, -0.036599867f), new float3(-0.0568565354f, -0.0254166033f, 0.0243766755f) }, new int[] { 0, 1, 2, 3, -1, 1, 4, 5, 2, -1, 4, 0, 3, 5, -1, 5, 3, 2, -1, 0, 4, 1, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(-0.27845192f, -0.707068264f, -0.650014639f, -0.03594617f),
					new float4(-0.09549721f, 0.9701459f, -0.222928837f, -0.0554777868f),
					new float4(0.382013381f, -0.242522269f, 0.8917672f, -0.04622467f),
					new float4(-0.881663561f, 0.00030134985f, 0.4718785f, -0.069280915f),
					new float4(0.90985f, -4.027609E-06f, -0.414937347f, -0.07984909f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(26.73954f, -10.2912083f, 73.64826f, 1f)), name: "Solid 1086599 #2098")
					.WithMesh(new float3[] { new float3(0.03755261f, 0.0355832f, -0.110093705f), new float3(0.08501816f, -0.07879173f, -0.00601295335f), new float3(-0.0507067479f, -0.07879173f, 0.0521285981f), new float3(-0.104843594f, 0.03558327f, -0.0490943864f), new float3(0.100840852f, 0.07370185f, 0.02868064f), new float3(-0.0326079726f, 0.07370194f, 0.08584719f) }, new int[] { 0, 1, 2, 3, -1, 4, 0, 3, 5, -1, 1, 4, 5, 2, -1, 2, 5, 3, -1, 4, 1, 0, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, -1f, 0f, -0.7455559f),
					new float4(0f, 1f, 0f, -0.474444121f),
					new float4(-0.5547002f, 0f, -0.8320503f, -0.475239843f),
					new float4(0.4472136f, 0f, 0.8944272f, -0.481191128f),
					new float4(0.909688f, 0f, -0.415292352f, -0.008501071f),
					new float4(-0.911550343f, 0f, 0.411188483f, -0.0106260153f),
				}, contents: 2, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(26.613369f, -10.6919441f, 73.26142f, 1f)), name: "Glass 1086618 #2111")
					.WithMesh(new float3[] { new float3(0.188506f, -0.7455559f, 0.443735033f), new float3(-0.207040861f, -0.7455559f, -0.43313995f), new float3(-0.192743987f, -0.7455559f, -0.4426712f), new float3(0.207568511f, -0.7455559f, 0.434203774f), new float3(-0.207040861f, 0.474444121f, -0.43313995f), new float3(0.188506f, 0.474444121f, 0.443735033f), new float3(0.207568511f, 0.474444121f, 0.434203774f), new float3(-0.192743987f, 0.474444121f, -0.4426712f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 1, 4, 7, 2, -1, 5, 0, 3, 6, -1, 6, 3, 2, 7, -1, 0, 5, 4, 1, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0.352863342f, -0.447849452f, 0.821534157f, -0.07204361f),
					new float4(-0.39351055f, 0f, -0.9193201f, -0.06997412f),
					new float4(0.0956693739f, 0.970148146f, 0.222845f, -0.0241495147f),
					new float4(-0.10823369f, -0.961452842f, -0.252772331f, -0.0151223438f),
					new float4(-0.881834447f, 0f, 0.471559167f, -0.07032072f),
					new float4(0.9093814f, -0.00169110624f, -0.415959865f, -0.08933893f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(26.6605682f, -10.259861f, 73.4982147f, 1f)), name: "Solid 1086588 #2591")
					.WithMesh(new float3[] { new float3(0.108519822f, -0.03388901f, 0.0226086415f), new float3(0.116522394f, 0.0042255735f, 0.0399491f), new float3(-0.0257134847f, 0.004219519f, 0.101038605f), new float3(-0.034757968f, -0.0339328647f, 0.0841250643f), new float3(0.0531065874f, 0.0423609763f, -0.09884709f), new float3(0.0530473664f, 0.00428054947f, -0.0988217443f), new float3(-0.09801157f, 0.00428615976f, -0.0341616943f), new float3(-0.09801157f, 0.0424048454f, -0.0341616943f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 1, 4, 7, 2, -1, 5, 0, 3, 6, -1, 6, 3, 2, 7, -1, 0, 5, 4, 1, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0.17630063f, -0.8941708f, 0.411553949f, -0.06964358f),
					new float4(-0.3518502f, 0.448301762f, -0.8217219f, -0.06966575f),
					new float4(0.278851956f, 0.70685035f, 0.6500801f, -0.0224705487f),
					new float4(-0.278508723f, -0.7074893f, -0.64953196f, -0.017989859f),
					new float4(-0.8812065f, 0.00114142883f, 0.47273016f, -0.05871595f),
					new float4(0.9097344f, 0.0006347583f, -0.4151901f, -0.09313822f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(26.7085533f, -10.3286982f, 73.61246f, 1f)), name: "Solid 1086589 #2592")
					.WithMesh(new float3[] { new float3(0.100191049f, -0.060381487f, -0.004887645f), new float3(0.1159852f, -0.0413256623f, 0.0297485f), new float3(-0.0195511151f, -0.0413018726f, 0.08786096f), new float3(-0.03763759f, -0.0603643432f, 0.054192353f), new float3(0.0684499443f, 0.07305673f, -0.07423244f), new float3(0.0605238676f, 0.0348957963f, -0.0916578248f), new float3(-0.0828156248f, 0.03494816f, -0.03025322f), new float3(-0.07368968f, 0.07312321f, -0.0133339036f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 1, 4, 7, 2, -1, 5, 0, 3, 6, -1, 6, 3, 2, 7, -1, 0, 5, 4, 1, -1 }))
				;
		}

		// bm_c1a4d, Details: the mesh has a triangle the exact CSG did not make; Solid 127043 #719 face -1 near (34.77,
		// -23.79, 60.822) (4 brushes)
		static ContentsScene BmC1a4dDetails30Site5()
		{
			return new ContentsScene()
				.WithContentsList("Solid", "Water", "Glass", "Grate")
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 1f, 0f, -0.0931940451f),
					new float4(0f, -1f, 0f, -0.0593059547f),
					new float4(-1f, 0f, 0f, -0.135555565f),
					new float4(1f, 0f, 0f, -0.169444427f),
					new float4(0f, 0f, 1f, -10.9799967f),
					new float4(0f, 0f, -1f, -10.9800034f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(34.7530556f, -23.8830032f, 64.05f, 1f)), name: "Solid 127043 #719")
					.WithMesh(new float3[] { new float3(0.169444427f, 0.0931940451f, -10.9800034f), new float3(-0.135555565f, 0.0931940451f, -10.9800034f), new float3(-0.135555565f, 0.0931940451f, 10.9799967f), new float3(0.169444427f, 0.0931940451f, 10.9799967f), new float3(-0.135555565f, -0.0593059547f, -10.9800034f), new float3(0.169444427f, -0.0593059547f, -10.9800034f), new float3(0.169444427f, -0.0593059547f, 10.9799967f), new float3(-0.135555565f, -0.0593059547f, 10.9799967f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 1, 4, 7, 2, -1, 5, 0, 3, 6, -1, 6, 3, 2, 7, -1, 0, 5, 4, 1, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 0f, 1f, -0.169445187f),
					new float4(0f, 0f, -1f, -0.1355548f),
					new float4(0.9662349f, -0.257662654f, 0f, -0.232281074f),
					new float4(-1f, 0f, 0f, -0.236164555f),
					new float4(0.146548659f, 0.989203453f, 0f, -0.182241663f),
					new float4(-0.129339188f, -0.9916004f, 0f, -0.115606733f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(35.0061646f, -23.8567181f, 53.0530548f, 1f)), name: "Solid 471733 #1641")
					.WithMesh(new float3[] { new float3(-0.236164555f, -0.08578193f, 0.169445187f), new float3(0.202272952f, -0.142969444f, 0.169445187f), new float3(0.278522938f, 0.142968059f, 0.169445187f), new float3(-0.236164555f, 0.21921806f, 0.169445187f), new float3(0.202272952f, -0.142969444f, -0.1355548f), new float3(-0.236164555f, -0.08578193f, -0.1355548f), new float3(-0.236164555f, 0.21921806f, -0.1355548f), new float3(0.278522938f, 0.142968059f, -0.1355548f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 1, 4, 7, 2, -1, 5, 0, 3, 6, -1, 6, 3, 2, 7, -1, 0, 5, 4, 1, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 0f, 1f, -3.90400052f),
					new float4(0.0422158539f, -0.9991085f, 0f, -0.0206008721f),
					new float4(-0.119145222f, -0.9928768f, 0f, -0.08721387f),
					new float4(0f, 1f, 0f, -0.0190629587f),
					new float4(0f, 0f, -1f, -5.85599947f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(35.6608543f, -23.809063f, 71.126f, 1f)), name: "Solid 2144436 #2800")
					.WithMesh(new float3[] { new float3(0.9391457f, 0.0190629587f, 3.90400052f), new float3(-0.8908543f, 0.0190629587f, 3.90400052f), new float3(-0.414291829f, -0.0381245427f, 3.90400052f), new float3(-0.414291829f, -0.0381245427f, -5.85599947f), new float3(0.9391457f, 0.0190629587f, -5.85599947f), new float3(-0.8908543f, 0.0190629587f, -5.85599947f) }, new int[] { 0, 1, 2, -1, 3, 4, 0, 2, -1, 5, 3, 2, 1, -1, 4, 5, 1, 0, -1, 5, 4, 3, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 0f, 1f, -6.50666761f),
					new float4(0.0422158539f, -0.9991085f, 0f, -0.02575224f),
					new float4(-0.119145222f, -0.9928768f, 0f, -0.07267523f),
					new float4(0f, 1f, 0f, -0.0190629587f),
					new float4(0f, 0f, -1f, -5.693332f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(35.53883f, -23.809063f, 58.7633324f, 1f)), name: "Solid 2144450 #2813")
					.WithMesh(new float3[] { new float3(1.06117022f, 0.0190629587f, 6.50666761f), new float3(-0.7688298f, 0.0190629587f, 6.50666761f), new float3(-0.2922673f, -0.0381245427f, 6.50666761f), new float3(-0.2922673f, -0.0381245427f, -5.693332f), new float3(1.06117022f, 0.0190629587f, -5.693332f), new float3(-0.7688298f, 0.0190629587f, -5.693332f) }, new int[] { 0, 1, 2, -1, 3, 4, 0, 2, -1, 5, 3, 2, 1, -1, 4, 5, 1, 0, -1, 5, 4, 3, -1 }))
				;
		}

		// bm_c1a4e, Model: the mesh draws nothing where the exact CSG has a face; Water 647604 #317 face 3 near (26.001,
		// -43.5, -13.496) (6 brushes)
		static ContentsScene BmC1a4eModel0Site1()
		{
			return new ContentsScene()
				.WithContentsList("Solid", "Water", "Glass", "Grate")
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, -1f, 0f, -1.89777935f),
					new float4(0f, 1f, 0f, -2.98222065f),
					new float4(-0.707106769f, 0f, -0.707106769f, -2.56329918f),
					new float4(0.49613893f, 0f, 0.868243158f, -2.56144619f),
					new float4(0.7934314f, 0f, -0.6086597f, -0.3399626f),
					new float4(-0.792763f, 0f, 0.609530032f, -0.27196905f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(27.7708855f, -42.02222f, -11.6408329f, 1f)), name: "Solid 83854 #225")
					.WithMesh(new float3[] { new float3(1.33755207f, -1.89777935f, 2.185833f), new float3(-1.76963544f, -1.89777935f, -1.85541713f), new float3(-1.33119786f, -1.89777935f, -2.29385471f), new float3(1.87130213f, -1.89777935f, 1.880833f), new float3(-1.76963544f, 2.98222065f, -1.85541713f), new float3(1.33755207f, 2.98222065f, 2.185833f), new float3(1.87130213f, 2.98222065f, 1.880833f), new float3(-1.33119786f, 2.98222065f, -2.29385471f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 1, 4, 7, 2, -1, 5, 0, 3, 6, -1, 6, 3, 2, 7, -1, 0, 5, 4, 1, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, -1f, 0f, -0.372777253f),
					new float4(0f, 1f, 0f, -0.237222746f),
					new float4(-0.707106769f, 0f, -0.707106769f, -2.14993644f),
					new float4(0.499237537f, 0f, 0.866465151f, -2.150663f),
					new float4(0.7934314f, 0f, -0.6086597f, -3.49582839f),
					new float4(-0.7963929f, 0f, 0.6047796f, -4.369743f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(25.2662849f, -44.1572227f, -9.720816f, 1f)), name: "Solid 77348 #237")
					.WithMesh(new float3[] { new float3(-2.50565982f, -0.372777253f, 3.92581558f), new float3(-4.43097258f, -0.372777253f, 1.390503f), new float3(1.17340255f, -0.372777253f, -4.213872f), new float3(4.37590265f, -0.372777253f, -0.0391843654f), new float3(-4.43097258f, 0.237222746f, 1.390503f), new float3(-2.50565982f, 0.237222746f, 3.92581558f), new float3(4.37590265f, 0.237222746f, -0.0391843654f), new float3(1.17340255f, 0.237222746f, -4.213872f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 1, 4, 7, 2, -1, 5, 0, 3, 6, -1, 6, 3, 2, 7, -1, 0, 5, 4, 1, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, -1f, 0f, -0.372778922f),
					new float4(0f, 1f, 0f, -0.237221062f),
					new float4(-0.866465151f, 0f, -0.499237537f, -2.15066385f),
					new float4(0.707106769f, 0f, 0.707106769f, -2.14993572f),
					new float4(0.6086597f, 0f, -0.7934314f, -3.495828f),
					new float4(-0.6047796f, 0f, 0.7963929f, -4.36974335f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(22.2258167f, -38.66722f, -12.7612848f, 1f)), name: "Solid 83910 #242")
					.WithMesh(new float3[] { new float3(-1.390504f, -0.372778922f, 4.430972f), new float3(-3.92581654f, -0.372778922f, 2.50565958f), new float3(0.03918336f, -0.372778922f, -4.37590265f), new float3(4.213871f, -0.372778922f, -1.17340267f), new float3(-3.92581654f, 0.237221062f, 2.50565958f), new float3(-1.390504f, 0.237221062f, 4.430972f), new float3(4.213871f, 0.237221062f, -1.17340267f), new float3(0.03918336f, 0.237221062f, -4.37590265f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 1, 4, 7, 2, -1, 5, 0, 3, 6, -1, 6, 3, 2, 7, -1, 0, 5, 4, 1, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, -1f, 0f, -0.372778922f),
					new float4(0f, 1f, 0f, -0.237221062f),
					new float4(-0.707106769f, 0f, -0.707106769f, -2.14993644f),
					new float4(0.499237537f, 0f, 0.866465151f, -2.150663f),
					new float4(0.7934314f, 0f, -0.6086597f, -3.49582839f),
					new float4(-0.7963929f, 0f, 0.6047796f, -4.369743f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(25.2662849f, -38.66722f, -9.720816f, 1f)), name: "Solid 83911 #243")
					.WithMesh(new float3[] { new float3(-2.50565982f, -0.372778922f, 3.92581558f), new float3(-4.43097258f, -0.372778922f, 1.390503f), new float3(1.17340255f, -0.372778922f, -4.213872f), new float3(4.37590265f, -0.372778922f, -0.0391843654f), new float3(-4.43097258f, 0.237221062f, 1.390503f), new float3(-2.50565982f, 0.237221062f, 3.92581558f), new float3(4.37590265f, 0.237221062f, -0.0391843654f), new float3(1.17340255f, 0.237221062f, -4.213872f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 1, 4, 7, 2, -1, 5, 0, 3, 6, -1, 6, 3, 2, 7, -1, 0, 5, 4, 1, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, -1f, 0f, -0.355832964f),
					new float4(0f, 1f, 0f, -0.559167f),
					new float4(-0.866465151f, 0f, -0.499237537f, -2.15066385f),
					new float4(0.707106769f, 0f, 0.707106769f, -2.14993572f),
					new float4(0.6086597f, 0f, -0.7934314f, -3.495828f),
					new float4(-0.6047796f, 0f, 0.7963929f, -4.36974335f),
				}, contents: 1, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(22.2258167f, -43.564167f, -12.7612848f, 1f)), name: "Water 647604 #317")
					.WithMesh(new float3[] { new float3(-1.390504f, -0.355832964f, 4.430972f), new float3(-3.92581654f, -0.355832964f, 2.50565958f), new float3(0.03918336f, -0.355832964f, -4.37590265f), new float3(4.213871f, -0.355832964f, -1.17340267f), new float3(-3.92581654f, 0.559167f, 2.50565958f), new float3(-1.390504f, 0.559167f, 4.430972f), new float3(4.213871f, 0.559167f, -1.17340267f), new float3(0.03918336f, 0.559167f, -4.37590265f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 1, 4, 7, 2, -1, 5, 0, 3, 6, -1, 6, 3, 2, 7, -1, 0, 5, 4, 1, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, -1f, 0f, -0.355832964f),
					new float4(0f, 1f, 0f, -0.559167f),
					new float4(-0.707106769f, 0f, -0.707106769f, -2.14993644f),
					new float4(0.499237537f, 0f, 0.866465151f, -2.150663f),
					new float4(0.7934314f, 0f, -0.6086597f, -3.49582839f),
					new float4(-0.7963929f, 0f, 0.6047796f, -4.369743f),
				}, contents: 1, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(25.2662849f, -43.564167f, -9.720816f, 1f)), name: "Water 647605 #320")
					.WithMesh(new float3[] { new float3(-2.50565982f, -0.355832964f, 3.92581558f), new float3(-4.43097258f, -0.355832964f, 1.390503f), new float3(1.17340255f, -0.355832964f, -4.213872f), new float3(4.37590265f, -0.355832964f, -0.0391843654f), new float3(-4.43097258f, 0.559167f, 1.390503f), new float3(-2.50565982f, 0.559167f, 3.92581558f), new float3(4.37590265f, 0.559167f, -0.0391843654f), new float3(1.17340255f, 0.559167f, -4.213872f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 1, 4, 7, 2, -1, 5, 0, 3, 6, -1, 6, 3, 2, 7, -1, 0, 5, 4, 1, -1 }))
				;
		}

		// bm_c1a4e, Details: the exact CSG made a triangle the mesh does not have; Solid 87 #2502 face -1 near (-4.289,
		// -34.16, 2.478) (3 brushes)
		static ContentsScene BmC1a4eDetails226Site1()
		{
			return new ContentsScene()
				.WithContentsList("Solid", "Water", "Glass", "Grate")
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 1f, 0f, -0.948888063f),
					new float4(-0.495951623f, -0.00505867368f, -0.8683354f, -0.355468661f),
					new float4(0.5643749f, -0.7039298f, -0.4312352f, -0.0415550768f),
					new float4(0.707106769f, 0f, 0.707106769f, -0.5352361f),
					new float4(-0.554041147f, 0.7146733f, 0.426943183f, -0.0320374072f),
					new float4(0f, -1f, 0f, -1.49111187f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 0.99999994f, 2.13162821E-14f, 0f), new float4(0f, -2.13162821E-14f, 0.99999994f, 0f), new float4(-2.76565361f, -32.6688843f, 2.00871634f, 1f)), name: "Solid 28 #2444")
					.WithMesh(new float3[] { new float3(0.587762952f, 0.948888063f, -0.750597954f), new float3(0.9880659f, 0.948888063f, -0.231127679f), new float3(1.0404917f, 0.948888063f, -0.283553481f), new float3(0.6545008f, 0.948888063f, -0.788715363f), new float3(-1.45664978f, -1.49111187f, 0.4312875f), new float3(-1.59014678f, -1.49111187f, 0.5075346f), new float3(-0.6846702f, -1.49111187f, 1.44160843f), new float3(-0.7895392f, -1.49111187f, 1.54647744f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 0, 3, -1, 6, 4, 3, 2, -1, 7, 6, 2, 1, -1, 5, 7, 1, 0, -1, 7, 5, 4, 6, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0.4960587f, 0.00493165245f, 0.868275f, -0.4658183f),
					new float4(0f, 1f, 0f, -0.948888063f),
					new float4(-0.257670224f, -0.00187987834f, -0.9662311f, -0.423429072f),
					new float4(-0.6453324f, 0.714862645f, 0.269290775f, -0.0380492844f),
					new float4(0.6540174f, -0.7041306f, -0.276516467f, -0.03503047f),
					new float4(0f, -1f, 0f, -1.49111187f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 0.99999994f, 2.13162821E-14f, 0f), new float4(0f, -2.13162821E-14f, 0.99999994f, 0f), new float4(-3.1580112f, -32.6688843f, 1.28698719f, 1f)), name: "Solid 29 #2445")
					.WithMesh(new float3[] { new float3(-1.19777143f, -1.49111187f, 1.2292614f), new float3(-1.06431341f, -1.49111187f, 1.15301478f), new float3(1.04683936f, 0.948888063f, -0.06697771f), new float3(0.98012054f, 0.948888063f, -0.0288602486f), new float3(0.7990078f, 0.948888063f, -0.653149545f), new float3(0.7275615f, 0.948888063f, -0.634096563f), new float3(-1.55997264f, -1.49111187f, -0.0193198584f), new float3(-1.70288813f, -1.49111187f, 0.0187922027f) }, new int[] { 0, 1, 2, 3, -1, 2, 4, 5, 3, -1, 6, 7, 5, 4, -1, 7, 0, 3, 5, -1, 1, 6, 4, 2, -1, 6, 1, 0, 7, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0.707106769f, 0f, 0.707106769f, -0.713652849f),
					new float4(0f, 1f, 0f, -1.08444583f),
					new float4(-0.4959254f, 0f, -0.8683651f, -0.566586554f),
					new float4(0.794585f, 0f, -0.607152939f, -0.07529725f),
					new float4(-0.792094231f, 0f, 0.6103988f, -0.0747041255f),
					new float4(0f, -1f, 0f, -1.35555422f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 0.99999994f, 2.13162821E-14f, 0f), new float4(0f, -2.13162821E-14f, 0.99999994f, 0f), new float4(-3.94118237f, -35.24444f, 2.93192649f, 1f)), name: "Solid 87 #2502")
					.WithMesh(new float3[] { new float3(0.385988f, -1.35555422f, 0.623269558f), new float3(0.49086988f, -1.35555422f, 0.5183877f), new float3(0.49086988f, 1.08444583f, 0.5183877f), new float3(0.385988f, 1.08444583f, 0.623269558f), new float3(-0.414637f, 1.08444583f, -0.415674865f), new float3(-0.2811233f, 1.08444583f, -0.491924852f), new float3(-0.2811233f, -1.35555422f, -0.491924852f), new float3(-0.414637f, -1.35555422f, -0.415674865f) }, new int[] { 0, 1, 2, 3, -1, 4, 3, 2, 5, -1, 6, 7, 4, 5, -1, 1, 6, 5, 2, -1, 7, 0, 3, 4, -1, 0, 7, 6, 1, -1 }))
				;
		}

		// bm_c1a4e, Details: the exact CSG made a triangle the mesh does not have; Solid 79911 #740 face -1 near (24.317,
		// -40.778, -14.297) (6 brushes)
		static ContentsScene BmC1a4eDetails24Site1()
		{
			return new ContentsScene()
				.WithContentsList("Solid", "Water", "Glass", "Grate")
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(-0.965982139f, -0.000124303726f, -0.2586088f, -3.05827713f),
					new float4(0.8660298f, -4.6723485E-06f, 0.499992341f, -1.95184994f),
					new float4(0.15766184f, -0.911190331f, -0.380624384f, -0.4011342f),
					new float4(-0.213534236f, 0.8298431f, 0.515522659f, -0.319900274f),
					new float4(0.339064658f, 0.463636041f, -0.8185822f, -0.303223759f),
					new float4(-0.336206526f, -0.4776729f, 0.8116611f, -0.303357244f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(20.0601521f, -40.6391335f, -17.2128811f, 1f)), name: "Solid 79889 #718")
					.WithMesh(new float3[] { new float3(-2.8720634f, -0.478676975f, -1.09762585f), new float3(-2.95594549f, 0.112281494f, -0.784584939f), new float3(-2.822886f, 0.455384433f, -1.28176749f), new float3(-2.72913432f, -0.230881572f, -1.63162827f), new float3(1.61391056f, 0.112258419f, 1.1083287f), new float3(1.77594173f, -0.478679061f, 0.827671051f), new float3(2.05234671f, -0.230866551f, 0.348916054f), new float3(1.87125432f, 0.45538345f, 0.6625903f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 5, 0, 3, 6, -1, 1, 4, 7, 2, -1, 2, 7, 6, 3, -1, 4, 1, 0, 5, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(-0.866016448f, 2.77915387E-05f, -0.5000155f, -3.1027782f),
					new float4(0.707004f, 0.000105031024f, 0.7072095f, -1.98086846f),
					new float4(0.135194525f, -0.975028f, -0.176189959f, -0.397390753f),
					new float4(-0.2507857f, 0.911201239f, 0.32683143f, -0.315997154f),
					new float4(0.5822549f, 0.291843772f, -0.758819163f, -0.298879176f),
					new float4(-0.5829928f, -0.287917763f, 0.7597518f, -0.29881987f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(24.50373f, -41.2989044f, -14.800148f, 1f)), name: "Solid 79910 #739")
					.WithMesh(new float3[] { new float3(-2.57227135f, -0.4479543f, -1.75026858f), new float3(-2.6676414f, 0.181119755f, -1.58505476f), new float3(-2.39124131f, 0.428895056f, -2.06376f), new float3(-2.27685523f, -0.314536929f, -2.26191568f), new float3(1.32366931f, 0.181091964f, 1.47765255f), new float3(1.458631f, -0.447970569f, 1.34282351f), new float3(1.87651694f, -0.314533085f, 0.9250392f), new float3(1.71464133f, 0.428904474f, 1.0867573f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 5, 0, 3, 6, -1, 1, 4, 7, 2, -1, 2, 7, 6, 3, -1, 4, 1, 0, 5, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(-0.8660467f, -5.6595014E-05f, -0.4999631f, -3.05833626f),
					new float4(0.7070219f, -0.000134036411f, 0.707191646f, -1.951816f),
					new float4(0.250799626f, -0.911192834f, -0.326844275f, -0.401123881f),
					new float4(-0.339646935f, 0.8298888f, 0.442633569f, -0.319895834f),
					new float4(0.539405048f, 0.463568f, -0.702955842f, -0.3032542f),
					new float4(-0.534821153f, -0.47768864f, 0.6969791f, -0.303386122f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(24.3262711f, -40.6391335f, -14.5816278f, 1f)), name: "Solid 79911 #740")
					.WithMesh(new float3[] { new float3(-2.49015069f, -0.478673339f, -1.80357766f), new float3(-2.65221262f, 0.112273261f, -1.52291739f), new float3(-2.39485717f, 0.455393583f, -1.96875286f), new float3(-2.21377158f, -0.23086454f, -2.28235555f), new float3(1.27199745f, 0.112258464f, 1.48828256f), new float3(1.501129f, -0.478679031f, 1.25909388f), new float3(1.89211559f, -0.230866566f, 0.8682482f), new float3(1.63609064f, 0.45538345f, 1.12434173f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 5, 0, 3, 6, -1, 1, 4, 7, 2, -1, 2, 7, 6, 3, -1, 4, 1, 0, 5, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(-0.8660161f, 3.7269423E-05f, -0.5000161f, -2.99451256f),
					new float4(0.707165062f, 1.32831237E-05f, 0.7070485f, -1.91044593f),
					new float4(0.33963f, -0.829912066f, -0.442603022f, -0.3910768f),
					new float4(-0.432325f, 0.7040228f, 0.5634243f, -0.313146055f),
					new float4(0.468051553f, 0.639404535f, -0.609991431f, -0.306656361f),
					new float4(-0.466440827f, -0.6425768f, 0.6078881f, -0.30667758f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(24.07185f, -40.05349f, -14.2686281f, 1f)), name: "Solid 79912 #741")
					.WithMesh(new float3[] { new float3(-2.39784837f, -0.4734297f, -1.83585072f), new float3(-2.60747266f, 0.0222341362f, -1.47274959f), new float3(-2.38826346f, 0.460662574f, -1.85238183f), new float3(-2.14042139f, -0.130313262f, -2.28168249f), new float3(1.22999823f, 0.0222396553f, 1.47179985f), new float3(1.52641869f, -0.473385334f, 1.17533982f), new float3(1.89042032f, -0.130260333f, 0.811271667f), new float3(1.53995228f, 0.460677177f, 1.16178644f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 5, 0, 3, 6, -1, 1, 4, 7, 2, -1, 2, 7, 6, 3, -1, 4, 1, 0, 5, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(-0.707035542f, -0.000168214014f, -0.707177937f, -2.82296634f),
					new float4(0.499941677f, -5.20146168E-05f, 0.866059065f, -2.26921725f),
					new float4(0.176173955f, -0.975032449f, -0.135183215f, -0.3973815f),
					new float4(-0.326811671f, 0.911211252f, 0.2507751f, -0.322634935f),
					new float4(0.7588392f, 0.291744173f, -0.5822786f, -0.265627235f),
					new float4(-0.759795249f, -0.2877413f, 0.58302325f, -0.332184523f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(27.845377f, -41.2914925f, -11.3481493f, 1f)), name: "Solid 79932 #761")
					.WithMesh(new float3[] { new float3(-1.88310039f, -0.4553973f, -2.109046f), new float3(-2.01797581f, 0.173674464f, -1.97434747f), new float3(-1.62698424f, 0.42150563f, -2.36531925f), new float3(-1.46516442f, -0.321945041f, -2.52692962f), new float3(1.04479587f, 0.1736799f, 2.01705551f), new float3(1.20987666f, -0.455387384f, 1.92172325f), new float3(1.7217046f, -0.321945f, 1.62627316f), new float3(1.52364576f, 0.421492457f, 1.74064934f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 5, 0, 3, 6, -1, 1, 4, 7, 2, -1, 2, 7, 6, 3, -1, 4, 1, 0, 5, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(-0.7070453f, 2.55060841E-05f, -0.7071682f, -2.78131247f),
					new float4(0.5000121f, 5.349194E-05f, 0.8660184f, -2.2369113f),
					new float4(0.326824963f, -0.911202431f, -0.2507899f, -0.401140958f),
					new float4(-0.44270888f, 0.8298191f, 0.339719146f, -0.3254741f),
					new float4(0.702891469f, 0.463711739f, -0.5393654f, -0.2698738f),
					new float4(-0.6970242f, -0.477579653f, 0.5348597f, -0.3367023f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(27.617569f, -40.6253662f, -11.1792889f, 1f)), name: "Solid 79933 #762")
					.WithMesh(new float3[] { new float3(-1.79018939f, -0.492464364f, -2.1431675f), new float3(-2.01931071f, 0.09851763f, -1.91406476f), new float3(-1.65537727f, 0.4416358f, -2.27792263f), new float3(-1.39919686f, -0.244633779f, -2.53408337f), new float3(0.992002f, 0.09849115f, 2.01022625f), new float3(1.27261508f, -0.4924701f, 1.84824562f), new float3(1.75144041f, -0.244633824f, 1.57177138f), new float3(1.43768346f, 0.4416162f, 1.7528826f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 5, 0, 3, 6, -1, 1, 4, 7, 2, -1, 2, 7, 6, 3, -1, 4, 1, 0, 5, -1 }))
				;
		}

		// bm_c2a2a, Model: the mesh draws nothing where the exact CSG has a face; Water 8905 #90 face 5 near (-37.21, 7,
		// -34.77) (3 brushes)
		static ContentsScene BmC2a2aModel0Site1()
		{
			return new ContentsScene()
				.WithContentsList("Solid", "Water", "Glass", "Grate")
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 1f, 0f, -0.5824653f),
					new float4(0f, -1f, 0f, -0.370659739f),
					new float4(1f, 0f, 0f, -7.04888964f),
					new float4(0f, 0f, 1f, -26.84f),
					new float4(0f, 0f, -1f, -26.84f),
					new float4(-1f, 0f, 0f, -8.8111105f),
				}, contents: 1, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(-28.39889f, 6.47065973f, -19.52f, 1f)), name: "Water 8905 #90")
					.WithMesh(new float3[] { new float3(-8.8111105f, 0.5824653f, -26.84f), new float3(-8.8111105f, 0.5824653f, 26.84f), new float3(7.04888964f, 0.5824653f, 26.84f), new float3(7.04888964f, 0.5824653f, -26.84f), new float3(-8.8111105f, -0.370659739f, 26.84f), new float3(-8.8111105f, -0.370659739f, -26.84f), new float3(7.04888964f, -0.370659739f, -26.84f), new float3(7.04888964f, -0.370659739f, 26.84f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 6, 3, 2, 7, -1, 1, 4, 7, 2, -1, 5, 0, 3, 6, -1, 0, 5, 4, 1, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 1f, 0f, -0.559166551f),
					new float4(0f, -1f, 0f, -0.355833441f),
					new float4(-1f, 0f, 0f, -0.5422208f),
					new float4(1f, 0f, 0f, -0.677779257f),
					new float4(0f, 0f, 1f, -2.71111035f),
					new float4(-0.707106769f, 0f, -0.707106769f, -1.91704452f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(-37.88778f, 6.45583344f, -34.43111f, 1f)), name: "Solid 1925099 #741")
					.WithMesh(new float3[] { new float3(0.677779257f, 0.559166551f, -3.38888955f), new float3(-0.5422208f, 0.559166551f, -2.16888952f), new float3(-0.5422208f, 0.559166551f, 2.71111035f), new float3(0.677779257f, 0.559166551f, 2.71111035f), new float3(-0.5422208f, -0.355833441f, -2.16888952f), new float3(0.677779257f, -0.355833441f, -3.38888955f), new float3(0.677779257f, -0.355833441f, 2.71111035f), new float3(-0.5422208f, -0.355833441f, 2.71111035f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 1, 4, 7, 2, -1, 5, 0, 3, 6, -1, 6, 3, 2, 7, -1, 0, 5, 4, 1, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 1f, 0f, -0.5824653f),
					new float4(0f, -1f, 0f, -0.370659739f),
					new float4(-1f, 0f, 0f, -0.5422208f),
					new float4(0f, 0f, 1f, -9.963332f),
					new float4(1f, 0f, 0f, -0.677779257f),
					new float4(-0.707106769f, 0f, -0.707106769f, -8.866333f),
				}, contents: 1, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(-37.88778f, 6.47065973f, -24.6033325f, 1f)), name: "Water 8898 #742")
					.WithMesh(new float3[] { new float3(0.677779257f, 0.5824653f, -13.2166681f), new float3(-0.5422208f, 0.5824653f, -11.9966679f), new float3(-0.5422208f, 0.5824653f, 9.963332f), new float3(0.677779257f, 0.5824653f, 9.963332f), new float3(-0.5422208f, -0.370659739f, -11.9966679f), new float3(0.677779257f, -0.370659739f, -13.2166681f), new float3(0.677779257f, -0.370659739f, 9.963332f), new float3(-0.5422208f, -0.370659739f, 9.963332f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 1, 4, 7, 2, -1, 6, 3, 2, 7, -1, 5, 0, 3, 6, -1, 0, 5, 4, 1, -1 }))
				;
		}

		// bm_c2a2b, func_movelinear 2922 lvl4_lift_cage: the mesh has a triangle the exact CSG did not make; Grate 2951 #5070
		// face -1 near (32.194, 3.673, 1.086) (5 brushes)
		static ContentsScene BmC2a2bFuncMovelinear2922Lvl4LiftCage280Site1()
		{
			return new ContentsScene()
				.WithContentsList("Solid", "Water", "Glass", "Grate")
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, -1f, 0f, -0.446488261f),
					new float4(0f, 0f, 1f, -0.0338888653f),
					new float4(0f, 0f, -1f, -0.0423611365f),
					new float4(0.84030503f, -0.5421139f, 0f, -0.0320297256f),
					new float4(-0.8402966f, 0.5421269f, 0f, -0.032035023f),
					new float4(0.840456247f, 0.541879356f, 0.000270790857f, -0.7652076f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 0.99999994f, 2.13162821E-14f, 0f), new float4(0f, -2.13162821E-14f, 0.99999994f, 0f), new float4(31.74118f, 2.96273637f, 1.10986078f, 1f)), name: "Solid 2931 #5054")
					.WithMesh(new float3[] { new float3(-0.326180428f, -0.446488261f, 0.0338888653f), new float3(-0.326180428f, -0.446488261f, -0.0423611365f), new float3(-0.2499304f, -0.446488261f, -0.0423611365f), new float3(-0.2499304f, -0.446488261f, 0.0338888653f), new float3(0.474419981f, 0.6762929f, 0.0338888653f), new float3(0.4363196f, 0.7353867f, 0.0338888653f), new float3(0.4363319f, 0.735405743f, -0.0423611365f), new float3(0.4744323f, 0.67631197f, -0.0423611365f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 0, 3, -1, 6, 7, 2, 1, -1, 7, 4, 3, 2, -1, 5, 6, 1, 0, -1, 6, 5, 4, 7, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 0f, 1f, -0.0762499645f),
					new float4(0f, 0f, -1f, -0.07625003f),
					new float4(0.8402169f, -0.5422505f, -6.72269452E-05f, -0.0284703f),
					new float4(-0.840456247f, 0.541879356f, 0f, -0.03559608f),
					new float4(0.840456247f, 0.541879356f, 0f, -0.03915355f),
					new float4(-0.8401378f, -0.542373f, -0.000271037541f, -0.0249355789f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 0.99999994f, 2.13162821E-14f, 0f), new float4(0f, -2.13162821E-14f, 0.99999994f, 0f), new float4(32.21351f, 3.688282f, 1.105625f, 1f)), name: "Solid 2932 #5055")
					.WithMesh(new float3[] { new float3(-0.03603319f, 0.009802477f, 0.0762499645f), new float3(0.00209789933f, -0.04926272f, 0.0762499645f), new float3(0.0402413942f, 0.009840602f, 0.0762499645f), new float3(0.002116392f, 0.06897257f, 0.0762499645f), new float3(0.0021163926f, -0.04921516f, -0.07625003f), new float3(-0.0360086076f, 0.009840606f, -0.07625003f), new float3(0.002116392f, 0.06897257f, -0.07625003f), new float3(0.0402352959f, 0.009850061f, -0.07625003f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 1, 4, 7, 2, -1, 5, 0, 3, 6, -1, 6, 3, 2, 7, -1, 0, 5, 4, 1, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 1f, 0f, -0.4464873f),
					new float4(0f, 0f, 1f, -0.0381249674f),
					new float4(0f, 0f, -1f, -0.0381250344f),
					new float4(0.8402966f, -0.5421269f, 0f, -0.0355955474f),
					new float4(-0.84030503f, 0.5421139f, 0f, -0.0284691658f),
					new float4(-0.8402169f, -0.5422505f, -0.000136522416f, -0.761802554f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 0.99999994f, 2.13162821E-14f, 0f), new float4(0f, -2.13162821E-14f, 0.99999994f, 0f), new float4(32.685833f, 4.4335103f, 1.105625f, 1f)), name: "Solid 2933 #5056")
					.WithMesh(new float3[] { new float3(0.254167f, 0.4464873f, -0.0381250344f), new float3(0.254167f, 0.4464873f, 0.0381249674f), new float3(0.330417037f, 0.4464873f, 0.0381249674f), new float3(0.330417037f, 0.4464873f, -0.0381250344f), new float3(-0.47020185f, -0.6763225f, 0.0381249674f), new float3(-0.432082981f, -0.7353877f, 0.0381249674f), new float3(-0.4320768f, -0.7353781f, -0.0381250344f), new float3(-0.470195681f, -0.676312864f, -0.0381250344f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 2, 1, -1, 6, 7, 0, 3, -1, 5, 6, 3, 2, -1, 7, 4, 1, 0, -1, 4, 7, 6, 5, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 1f, 0f, -1.44451392f),
					new float4(0f, -1f, 0f, -0.919236064f),
					new float4(0f, 0f, -1f, -0.0381250344f),
					new float4(0f, 0f, 1f, -0.0381249674f),
					new float4(-0.8402966f, -0.5421269f, 0f, -0.028476147f),
					new float4(0.8402966f, 0.5421269f, 0f, -0.03559647f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 0.99999994f, 2.13162821E-14f, 0f), new float4(0f, -2.13162821E-14f, 0.99999994f, 0f), new float4(32.3808327f, 3.43548346f, 1.105625f, 1f)), name: "Solid 2934 #5057")
					.WithMesh(new float3[] { new float3(-0.889582753f, 1.44451392f, 0.0381249674f), new float3(-0.889582753f, 1.44451392f, -0.0381250344f), new float3(-0.9658327f, 1.44451392f, -0.0381250344f), new float3(-0.9658327f, 1.44451392f, 0.0381249674f), new float3(0.635417342f, -0.919236064f, -0.0381250344f), new float3(0.635417342f, -0.919236064f, 0.0381249674f), new float3(0.5591674f, -0.919236064f, 0.0381249674f), new float3(0.5591674f, -0.919236064f, -0.0381250344f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 1, 4, 7, 2, -1, 5, 0, 3, 6, -1, 6, 3, 2, 7, -1, 0, 5, 4, 1, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 1f, 0f, -1.44451392f),
					new float4(0f, -1f, 0f, -0.919236064f),
					new float4(0f, 0f, 1f, -0.0190624669f),
					new float4(0f, 0f, -1f, -0.019062534f),
					new float4(1f, 0f, 0f, -0.889583f),
					new float4(-1f, 0f, 0f, -0.711667f),
				}, contents: 3, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 0.99999994f, 2.13162821E-14f, 0f), new float4(0f, -2.13162821E-14f, 0.99999994f, 0f), new float4(32.126667f, 3.43548346f, 1.105625f, 1f)), name: "Grate 2951 #5070")
					.WithMesh(new float3[] { new float3(-0.711667f, 1.44451392f, -0.019062534f), new float3(-0.711667f, 1.44451392f, 0.0190624669f), new float3(0.889583f, 1.44451392f, 0.0190624669f), new float3(0.889583f, 1.44451392f, -0.019062534f), new float3(-0.711667f, -0.919236064f, 0.0190624669f), new float3(-0.711667f, -0.919236064f, -0.019062534f), new float3(0.889583f, -0.919236064f, -0.019062534f), new float3(0.889583f, -0.919236064f, 0.0190624669f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 1, 4, 7, 2, -1, 5, 0, 3, 6, -1, 6, 3, 2, 7, -1, 0, 5, 4, 1, -1 }))
				;
		}

		// bm_c2a2b, Details: the mesh has a triangle the exact CSG did not make; Solid 2927239 #2524 face -1 near (-12.81,
		// 5.947, 13.839) (3 brushes)
		static ContentsScene BmC2a2bDetails64Site1()
		{
			return new ContentsScene()
				.WithContentsList("Solid", "Water", "Glass", "Grate")
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(-0.707106769f, 0.707106769f, 0f, -0.658983946f),
					new float4(0f, 0f, 1f, -0.152500421f),
					new float4(0f, 0f, -1f, -0.152499586f),
					new float4(-1f, 0f, 0f, -0.0338886455f),
					new float4(1f, 0f, 0f, -0.0423613563f),
					new float4(0f, -1f, 0f, -1.46569455f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(-12.8525906f, 4.9731946f, 13.8775f, 1f)), name: "Solid 2927240 #2523")
					.WithMesh(new float3[] { new float3(0.0423613563f, 0.9743054f, 0.152500421f), new float3(0.0423613563f, 0.9743054f, -0.152499586f), new float3(-0.0338886455f, 0.8980554f, -0.152499586f), new float3(-0.0338886455f, 0.8980554f, 0.152500421f), new float3(-0.0338886455f, -1.46569455f, 0.152500421f), new float3(0.0423613563f, -1.46569455f, 0.152500421f), new float3(0.0423613563f, -1.46569455f, -0.152499586f), new float3(-0.0338886455f, -1.46569455f, -0.152499586f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 0, 3, -1, 6, 7, 2, 1, -1, 7, 4, 3, 2, -1, 5, 6, 1, 0, -1, 6, 5, 4, 7, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 0f, -1f, -0.152499586f),
					new float4(0f, 0f, 1f, -0.152500421f),
					new float4(-0.707106769f, -0.707106769f, 0f, -1.88105547f),
					new float4(0.7071067f, -0.7071067f, 0.0005313874f, -1.5036459f),
					new float4(0f, 1f, 0f, -0.0465972535f),
					new float4(0f, -1f, 0f, -0.0296527483f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(-14.9831886f, 5.90090275f, 13.8775f, 1f)), name: "Solid 2927239 #2524")
					.WithMesh(new float3[] { new float3(2.09693837f, -0.0296527483f, -0.152499586f), new float3(-2.63056135f, -0.0296527483f, -0.152499586f), new float3(-2.70681143f, 0.0465972535f, -0.152499586f), new float3(2.17318845f, 0.0465972535f, -0.152499586f), new float3(-2.63056135f, -0.0296527483f, 0.152500421f), new float3(2.09670925f, -0.0296527483f, 0.152500421f), new float3(2.17295933f, 0.0465972535f, 0.152500421f), new float3(-2.70681143f, 0.0465972535f, 0.152500421f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 1, 4, 7, 2, -1, 5, 0, 3, 6, -1, 6, 3, 2, 7, -1, 0, 5, 4, 1, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 1f, 0f, -0.948889f),
					new float4(0f, -1f, 0f, -1.49111092f),
					new float4(-1f, 0f, 0f, -2.16878748f),
					new float4(1f, 0f, 0f, -2.71098328f),
					new float4(0f, 0f, 1f, -0.03812546f),
					new float4(0f, 0f, -1f, -0.0381245427f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(-15.5212126f, 4.998611f, 13.8393745f, 1f)), name: "Solid 2927242 #2526")
					.WithMesh(new float3[] { new float3(2.71098328f, 0.948889f, -0.0381245427f), new float3(-2.16878748f, 0.948889f, -0.0381245427f), new float3(-2.16878748f, 0.948889f, 0.03812546f), new float3(2.71098328f, 0.948889f, 0.03812546f), new float3(-2.16878748f, -1.49111092f, -0.0381245427f), new float3(2.71098328f, -1.49111092f, -0.0381245427f), new float3(2.71098328f, -1.49111092f, 0.03812546f), new float3(-2.16878748f, -1.49111092f, 0.03812546f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 1, 4, 7, 2, -1, 5, 0, 3, 6, -1, 6, 3, 2, 7, -1, 0, 5, 4, 1, -1 }))
				;
		}

		// bm_c2a2c, Details: the mesh has a triangle the exact CSG did not make; Solid 2135082 #2471 face -1 near (33.245,
		// 3.51, -90.242) (4 brushes)
		static ContentsScene BmC2a2cDetails40Site1()
		{
			return new ContentsScene()
				.WithContentsList("Solid", "Water", "Glass", "Grate")
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, -1f, 0f, -2.79583335f),
					new float4(0f, 1f, 0f, -1.77916658f),
					new float4(1f, 0f, 0f, -0.057324525f),
					new float4(-0.998833835f, -1.96453129E-06f, 0.0482803173f, -0.0649899542f),
					new float4(-0.9904981f, 0f, -0.137526289f, -0.181392133f),
					new float4(0f, 0f, -1f, -1.12636256f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(33.3405571f, 3.5583334f, -89.61114f, 1f)), name: "Solid 2135082 #2471")
					.WithMesh(new float3[] { new float3(-0.0267417673f, -2.79583335f, -1.12636256f), new float3(0.057324525f, -2.79583335f, -1.12636256f), new float3(0.057324525f, -2.79583335f, 2.531925f), new float3(-0.0955504254f, -2.79583335f, -0.6307857f), new float3(0.057324525f, 1.77916658f, -1.12636256f), new float3(-0.0267417673f, 1.77916658f, -1.12636256f), new float3(-0.0955571f, 1.77916658f, -0.630737662f), new float3(0.057324525f, 1.77916658f, 2.532111f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 1, 4, 7, 2, -1, 6, 3, 2, 7, -1, 5, 0, 3, 6, -1, 0, 5, 4, 1, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, -1f, 0f, -1.82999992f),
					new float4(0f, 1f, 0f, -2.74500012f),
					new float4(-0.965948045f, 0f, 0.258736074f, -0.223808274f),
					new float4(0.998840749f, 0f, -0.0481369048f, -0.15614818f),
					new float4(-0.9260924f, 0f, -0.3772969f, -0.4450455f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(33.15604f, 2.5925f, -88.843956f, 1f)), name: "Solid 2134896 #2477")
					.WithMesh(new float3[] { new float3(-0.3304152f, -1.82999992f, -0.368544f), new float3(0.08895981f, -1.82999992f, -1.397919f), new float3(0.241459817f, -1.82999992f, 1.76645613f), new float3(0.08895981f, 2.74500012f, -1.397919f), new float3(-0.3304152f, 2.74500012f, -0.368544f), new float3(0.241459817f, 2.74500012f, 1.76645613f) }, new int[] { 0, 1, 2, -1, 3, 4, 5, -1, 4, 0, 2, 5, -1, 1, 3, 5, 2, -1, 3, 1, 0, 4, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, -1f, 0f, -0.677777946f),
					new float4(0f, 1f, 0f, -0.5422221f),
					new float4(0f, 0f, -1f, -0.432689816f),
					new float4(-0.2497185f, 0.009904816f, 0.9682678f, -0.571431041f),
					new float4(0.655880034f, 0.749548f, 0.0894381851f, -0.146263525f),
					new float4(-0.703772664f, -0.703772664f, -0.09699487f, -0.204679564f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(32.95092f, 4.185278f, -90.91481f, 1f)), name: "Solid 2134875 #4333")
					.WithMesh(new float3[] { new float3(0.294197053f, -0.677777946f, 0.6729654f), new float3(0.4465799f, -0.677777946f, -0.432689816f), new float3(1.05657983f, -0.677777946f, -0.432689816f), new float3(0.88503f, -0.677777946f, 0.8253426f), new float3(-0.773420155f, 0.5422221f, -0.432689816f), new float3(-0.8822663f, 0.5422221f, 0.35707292f), new float3(-0.460190326f, 0.5422221f, 0.4659273f), new float3(-0.33765164f, 0.5422221f, -0.432689816f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 1, 4, 7, 2, -1, 5, 0, 3, 6, -1, 6, 3, 2, 7, -1, 0, 5, 4, 1, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, -1f, 0f, -0.677777946f),
					new float4(0f, 1f, 0f, -0.5422221f),
					new float4(0.249718323f, -0.00998088252f, -0.9682671f, -0.432943761f),
					new float4(-0.496374279f, 0.004219743f, 0.8680984f, -0.5706414f),
					new float4(0.6137621f, 0.7496982f, 0.247484714f, -0.1466328f),
					new float4(-0.6579364f, -0.7037772f, -0.267987639f, -0.205187321f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(32.7088661f, 4.185278f, -89.9399f, 1f)), name: "Solid 2134876 #4334")
					.WithMesh(new float3[] { new float3(0.116835751f, -0.677777946f, 0.727447152f), new float3(0.5360986f, -0.677777946f, -0.301884979f), new float3(1.12707138f, -0.677777946f, -0.14947173f), new float3(0.650444865f, -0.677777946f, 1.03256214f), new float3(-0.6402123f, 0.5422221f, -0.617834032f), new float3(-0.939685762f, 0.5422221f, 0.117403187f), new float3(-0.558607638f, 0.5422221f, 0.335301757f), new float3(-0.218168259f, 0.5422221f, -0.5089879f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 1, 4, 7, 2, -1, 5, 0, 3, 6, -1, 6, 3, 2, 7, -1, 0, 5, 4, 1, -1 }))
				;
		}

		// bm_c2a2c, Details: the mesh has a triangle the exact CSG did not make; Solid 2134568 #4234 face -1 near (31.234,
		// -10.952, -88.045) (4 brushes)
		static ContentsScene BmC2a2cDetails40Site2()
		{
			return new ContentsScene()
				.WithContentsList("Solid", "Water", "Glass", "Grate")
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, -1f, 0f, -2.83214259f),
					new float4(0f, 1f, 0f, -1.74285734f),
					new float4(0.706945956f, 0f, -0.7072676f, -0.4648258f),
					new float4(0.9999982f, 0f, 0.0018768278f, -0.9977946f),
					new float4(-0.6084315f, 0f, -0.793606341f, -0.888160944f),
					new float4(0f, 0f, 1f, -0.6444922f),
					new float4(-1f, 0f, 0f, -0.833413243f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(32.4009132f, -11.6553574f, -87.41699f, 1f)), name: "Solid 2135073 #4211")
					.WithMesh(new float3[] { new float3(-0.833413243f, -2.83214259f, 0.6444922f), new float3(-0.833413243f, -2.83214259f, -0.480195343f), new float3(-0.261538237f, -2.83214259f, -0.9186328f), new float3(0.997159243f, -2.83214259f, 0.3394923f), new float3(0.9965868f, -2.83214259f, 0.6444922f), new float3(-0.833413243f, 1.74285734f, -0.480195343f), new float3(-0.833413243f, 1.74285734f, 0.6444922f), new float3(0.9965868f, 1.74285734f, 0.6444922f), new float3(0.997159243f, 1.74285734f, 0.3394923f), new float3(-0.261538237f, 1.74285734f, -0.9186328f) }, new int[] { 0, 1, 2, 3, 4, -1, 5, 6, 7, 8, 9, -1, 2, 9, 8, 3, -1, 7, 4, 3, 8, -1, 1, 5, 9, 2, -1, 6, 0, 4, 7, -1, 0, 6, 5, 1, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, -1f, 0f, -0.677777946f),
					new float4(0f, 1f, 0f, -0.5422221f),
					new float4(0.7071094f, 4.24471637E-06f, -0.707104146f, -0.4430027f),
					new float4(0.4068872f, 0.7500427f, 0.52142f, -0.170702934f),
					new float4(-0.438992143f, -0.7035094f, -0.558892131f, -0.183744535f),
					new float4(-0.427890927f, 8.043525E-05f, 0.903830349f, -0.3907642f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(31.4944057f, -11.0647221f, -88.3540955f, 1f)), name: "Solid 2134565 #4231")
					.WithMesh(new float3[] { new float3(0.6938592f, -0.677777946f, 0.760889232f), new float3(0.07307191f, -0.677777946f, 0.4669964f), new float3(0.6445895f, -0.677777946f, 0.0180874057f), new float3(1.08340681f, -0.677777946f, 0.456908f), new float3(-1.14671493f, 0.5422221f, -0.110583067f), new float3(-0.705777943f, 0.5422221f, 0.09816511f), new float3(0.09768558f, 0.5422221f, -0.5288132f), new float3(-0.215512291f, 0.5422221f, -0.8420134f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 2, 7, 6, 3, -1, 5, 0, 3, 6, -1, 1, 4, 7, 2, -1, 4, 1, 0, 5, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 1f, 0f, -0.325333625f),
					new float4(-0.707106769f, -0.707106769f, 0f, -0.345067918f),
					new float4(1f, 0f, 0f, -0.406666636f),
					new float4(0f, 0f, 1f, -2.174f),
					new float4(0.4280146f, 0f, -0.9037718f, -1.6974467f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(31.1608334f, -10.8478336f, -86.2015f, 1f)), name: "Solid 2134568 #4234")
					.WithMesh(new float3[] { new float3(0.406666636f, 0.325333625f, -1.6855886f), new float3(-0.8133334f, 0.325333625f, -2.26336479f), new float3(-0.8133334f, 0.325333625f, 2.174f), new float3(0.406666636f, 0.325333625f, 2.174f), new float3(0.406666636f, -0.8946664f, -1.6855886f), new float3(0.406666636f, -0.8946664f, 2.174f) }, new int[] { 0, 1, 2, 3, -1, 1, 4, 5, 2, -1, 4, 0, 3, 5, -1, 5, 3, 2, -1, 0, 4, 1, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, -1f, 0f, -0.3727779f),
					new float4(0f, 1f, 0f, -0.2372221f),
					new float4(-0.8663155f, 0f, 0.4994972f, -0.481290638f),
					new float4(0.707106769f, 0f, -0.707106769f, -0.48225677f),
					new float4(0.6163083f, 0f, 0.787505031f, -0.5362903f),
					new float4(-0.6114475f, 0f, -0.791285f, -0.670395553f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(31.3747578f, -10.1497221f, -88.41823f, 1f)), name: "Solid 2134747 #4237")
					.WithMesh(new float3[] { new float3(-0.722257733f, -0.3727779f, -0.2891156f), new float3(-0.09319529f, -0.3727779f, -0.775209367f), new float3(0.7646172f, -0.3727779f, 0.08260313f), new float3(-0.112257786f, -0.3727779f, 0.7688531f), new float3(-0.09319529f, 0.2372221f, -0.775209367f), new float3(-0.722257733f, 0.2372221f, -0.2891156f), new float3(-0.112257786f, 0.2372221f, 0.7688531f), new float3(0.7646172f, 0.2372221f, 0.08260313f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 5, 0, 3, 6, -1, 1, 4, 7, 2, -1, 2, 7, 6, 3, -1, 4, 1, 0, 5, -1 }))
				;
		}

		// bm_c2a3a, Model: the mesh draws nothing where the exact CSG has a face; Water 1002 #894 face 5 near (45, -15.25,
		// 2.745) (3 brushes)
		static ContentsScene BmC2a3aModel0Site1()
		{
			return new ContentsScene()
				.WithContentsList("Solid", "Water", "Glass", "Grate")
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, -1f, 0f, -0.94888854f),
					new float4(0f, 0f, 1f, -0.152500033f),
					new float4(0f, 0f, -1f, -0.152499974f),
					new float4(1f, 0f, 0f, -4.786807f),
					new float4(-0.707106769f, 0f, 0.707106769f, -2.70782518f),
					new float4(0f, 1f, 0f, -1.49111152f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(47.7494431f, -15.5211115f, 2.5925f, 1f)), name: "Solid 83 #80")
					.WithMesh(new float3[] { new float3(-3.676943f, -0.94888854f, 0.152500033f), new float3(-3.98194313f, -0.94888854f, -0.152499974f), new float3(4.786807f, -0.94888854f, -0.152499974f), new float3(4.786807f, -0.94888854f, 0.152500033f), new float3(4.786807f, 1.49111152f, 0.152500033f), new float3(-3.676943f, 1.49111152f, 0.152500033f), new float3(-3.98194313f, 1.49111152f, -0.152499974f), new float3(4.786807f, 1.49111152f, -0.152499974f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 0, 3, -1, 6, 7, 2, 1, -1, 7, 4, 3, 2, -1, 5, 6, 1, 0, -1, 6, 5, 4, 7, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 1f, 0f, -5.079097f),
					new float4(0f, -1f, 0f, -3.23215318f),
					new float4(-1f, 0f, 0f, -5.1511097f),
					new float4(0f, 0f, 1f, -7.591111f),
					new float4(1f, 0f, 0f, -6.43889f),
					new float4(0f, 0f, -1f, -9.488889f),
				}, contents: 1, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(49.22361f, -17.5078468f, 12.2338886f, 1f)), name: "Water 1002 #894")
					.WithMesh(new float3[] { new float3(6.43889f, 5.079097f, -9.488889f), new float3(-5.1511097f, 5.079097f, -9.488889f), new float3(-5.1511097f, 5.079097f, 7.591111f), new float3(6.43889f, 5.079097f, 7.591111f), new float3(-5.1511097f, -3.23215318f, -9.488889f), new float3(6.43889f, -3.23215318f, -9.488889f), new float3(6.43889f, -3.23215318f, 7.591111f), new float3(-5.1511097f, -3.23215318f, 7.591111f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 1, 4, 7, 2, -1, 6, 3, 2, 7, -1, 5, 0, 3, 6, -1, 0, 5, 4, 1, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 1f, 0f, -2.46965313f),
					new float4(0f, -1f, 0f, -1.57159686f),
					new float4(-1f, 0f, 0f, -4.87999964f),
					new float4(0f, 0f, -1f, -2.44f),
					new float4(1f, 0f, 0f, -6.10000038f),
					new float4(0f, 0f, 1f, -3.05f),
				}, contents: 1, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(49.2575f, -14.8984032f, -0.305f, 1f)), name: "Water 1003 #895")
					.WithMesh(new float3[] { new float3(-4.87999964f, 2.46965313f, 3.05f), new float3(6.10000038f, 2.46965313f, 3.05f), new float3(6.10000038f, 2.46965313f, -2.44f), new float3(-4.87999964f, 2.46965313f, -2.44f), new float3(6.10000038f, -1.57159686f, 3.05f), new float3(-4.87999964f, -1.57159686f, 3.05f), new float3(-4.87999964f, -1.57159686f, -2.44f), new float3(6.10000038f, -1.57159686f, -2.44f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 5, 0, 3, 6, -1, 2, 7, 6, 3, -1, 1, 4, 7, 2, -1, 4, 1, 0, 5, -1 }))
				;
		}

		// bm_c2a3a, Model: the mesh draws nothing where the exact CSG has a face; Water 1002 #894 face 5 near (48, -13.229,
		// 2.745) (4 brushes)
		static ContentsScene BmC2a3aModel0Site2()
		{
			return new ContentsScene()
				.WithContentsList("Solid", "Water", "Glass", "Grate")
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 1f, 0f, -0.237221837f),
					new float4(0f, -1f, 0f, -0.372778177f),
					new float4(-1f, 0f, 0f, -2.20277929f),
					new float4(1f, 0f, 0f, -1.76222062f),
					new float4(0f, 0f, 1f, -3.58375f),
					new float4(0f, 0f, -1f, -3.58375f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(49.47778f, -20.3672218f, 0.99125f, 1f)), name: "Solid 78 #75")
					.WithMesh(new float3[] { new float3(1.76222062f, 0.237221837f, -3.58375f), new float3(-2.20277929f, 0.237221837f, -3.58375f), new float3(-2.20277929f, 0.237221837f, 3.58375f), new float3(1.76222062f, 0.237221837f, 3.58375f), new float3(-2.20277929f, -0.372778177f, -3.58375f), new float3(1.76222062f, -0.372778177f, -3.58375f), new float3(1.76222062f, -0.372778177f, 3.58375f), new float3(-2.20277929f, -0.372778177f, 3.58375f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 1, 4, 7, 2, -1, 5, 0, 3, 6, -1, 6, 3, 2, 7, -1, 0, 5, 4, 1, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 0f, 1f, -0.152500033f),
					new float4(0f, 0f, -1f, -0.152499974f),
					new float4(0.707106769f, 0f, 0.707106769f, -3.738239f),
					new float4(-0.707106769f, 0f, 0.707106769f, -4.67279625f),
					new float4(0f, -1f, 0f, -1.491111f),
					new float4(0f, 1f, 0f, -0.948888958f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(50.52833f, -12.5388889f, 2.5925f, 1f)), name: "Solid 85 #82")
					.WithMesh(new float3[] { new float3(5.13416862f, 0.948888958f, 0.152500033f), new float3(-6.455832f, 0.948888958f, 0.152500033f), new float3(-6.455832f, -1.491111f, 0.152500033f), new float3(5.13416862f, -1.491111f, 0.152500033f), new float3(-6.760832f, 0.948888958f, -0.152499974f), new float3(5.43916845f, 0.948888958f, -0.152499974f), new float3(5.43916845f, -1.491111f, -0.152499974f), new float3(-6.760832f, -1.491111f, -0.152499974f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 5, 0, 3, 6, -1, 1, 4, 7, 2, -1, 2, 7, 6, 3, -1, 4, 1, 0, 5, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 1f, 0f, -5.079097f),
					new float4(0f, -1f, 0f, -3.23215318f),
					new float4(-1f, 0f, 0f, -5.1511097f),
					new float4(0f, 0f, 1f, -7.591111f),
					new float4(1f, 0f, 0f, -6.43889f),
					new float4(0f, 0f, -1f, -9.488889f),
				}, contents: 1, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(49.22361f, -17.5078468f, 12.2338886f, 1f)), name: "Water 1002 #894")
					.WithMesh(new float3[] { new float3(6.43889f, 5.079097f, -9.488889f), new float3(-5.1511097f, 5.079097f, -9.488889f), new float3(-5.1511097f, 5.079097f, 7.591111f), new float3(6.43889f, 5.079097f, 7.591111f), new float3(-5.1511097f, -3.23215318f, -9.488889f), new float3(6.43889f, -3.23215318f, -9.488889f), new float3(6.43889f, -3.23215318f, 7.591111f), new float3(-5.1511097f, -3.23215318f, 7.591111f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 1, 4, 7, 2, -1, 6, 3, 2, 7, -1, 5, 0, 3, 6, -1, 0, 5, 4, 1, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 1f, 0f, -2.46965313f),
					new float4(0f, -1f, 0f, -1.57159686f),
					new float4(-1f, 0f, 0f, -4.87999964f),
					new float4(0f, 0f, -1f, -2.44f),
					new float4(1f, 0f, 0f, -6.10000038f),
					new float4(0f, 0f, 1f, -3.05f),
				}, contents: 1, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(49.2575f, -14.8984032f, -0.305f, 1f)), name: "Water 1003 #895")
					.WithMesh(new float3[] { new float3(-4.87999964f, 2.46965313f, 3.05f), new float3(6.10000038f, 2.46965313f, 3.05f), new float3(6.10000038f, 2.46965313f, -2.44f), new float3(-4.87999964f, 2.46965313f, -2.44f), new float3(6.10000038f, -1.57159686f, 3.05f), new float3(-4.87999964f, -1.57159686f, 3.05f), new float3(-4.87999964f, -1.57159686f, -2.44f), new float3(6.10000038f, -1.57159686f, -2.44f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 5, 0, 3, 6, -1, 2, 7, 6, 3, -1, 1, 4, 7, 2, -1, 4, 1, 0, 5, -1 }))
				;
		}

		// bm_c2a3a, Model: the mesh draws nothing where the exact CSG has a face; Water 1002 #894 face 5 near (48, -15.25,
		// 2.745) (4 brushes)
		static ContentsScene BmC2a3aModel0Site3()
		{
			return new ContentsScene()
				.WithContentsList("Solid", "Water", "Glass", "Grate")
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 1f, 0f, -0.237221837f),
					new float4(0f, -1f, 0f, -0.372778177f),
					new float4(-1f, 0f, 0f, -2.20277929f),
					new float4(1f, 0f, 0f, -1.76222062f),
					new float4(0f, 0f, 1f, -3.58375f),
					new float4(0f, 0f, -1f, -3.58375f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(49.47778f, -20.3672218f, 0.99125f, 1f)), name: "Solid 78 #75")
					.WithMesh(new float3[] { new float3(1.76222062f, 0.237221837f, -3.58375f), new float3(-2.20277929f, 0.237221837f, -3.58375f), new float3(-2.20277929f, 0.237221837f, 3.58375f), new float3(1.76222062f, 0.237221837f, 3.58375f), new float3(-2.20277929f, -0.372778177f, -3.58375f), new float3(1.76222062f, -0.372778177f, -3.58375f), new float3(1.76222062f, -0.372778177f, 3.58375f), new float3(-2.20277929f, -0.372778177f, 3.58375f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 1, 4, 7, 2, -1, 5, 0, 3, 6, -1, 6, 3, 2, 7, -1, 0, 5, 4, 1, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, -1f, 0f, -0.94888854f),
					new float4(0f, 0f, 1f, -0.152500033f),
					new float4(0f, 0f, -1f, -0.152499974f),
					new float4(1f, 0f, 0f, -4.786807f),
					new float4(-0.707106769f, 0f, 0.707106769f, -2.70782518f),
					new float4(0f, 1f, 0f, -1.49111152f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(47.7494431f, -15.5211115f, 2.5925f, 1f)), name: "Solid 83 #80")
					.WithMesh(new float3[] { new float3(-3.676943f, -0.94888854f, 0.152500033f), new float3(-3.98194313f, -0.94888854f, -0.152499974f), new float3(4.786807f, -0.94888854f, -0.152499974f), new float3(4.786807f, -0.94888854f, 0.152500033f), new float3(4.786807f, 1.49111152f, 0.152500033f), new float3(-3.676943f, 1.49111152f, 0.152500033f), new float3(-3.98194313f, 1.49111152f, -0.152499974f), new float3(4.786807f, 1.49111152f, -0.152499974f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 0, 3, -1, 6, 7, 2, 1, -1, 7, 4, 3, 2, -1, 5, 6, 1, 0, -1, 6, 5, 4, 7, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 1f, 0f, -5.079097f),
					new float4(0f, -1f, 0f, -3.23215318f),
					new float4(-1f, 0f, 0f, -5.1511097f),
					new float4(0f, 0f, 1f, -7.591111f),
					new float4(1f, 0f, 0f, -6.43889f),
					new float4(0f, 0f, -1f, -9.488889f),
				}, contents: 1, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(49.22361f, -17.5078468f, 12.2338886f, 1f)), name: "Water 1002 #894")
					.WithMesh(new float3[] { new float3(6.43889f, 5.079097f, -9.488889f), new float3(-5.1511097f, 5.079097f, -9.488889f), new float3(-5.1511097f, 5.079097f, 7.591111f), new float3(6.43889f, 5.079097f, 7.591111f), new float3(-5.1511097f, -3.23215318f, -9.488889f), new float3(6.43889f, -3.23215318f, -9.488889f), new float3(6.43889f, -3.23215318f, 7.591111f), new float3(-5.1511097f, -3.23215318f, 7.591111f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 1, 4, 7, 2, -1, 6, 3, 2, 7, -1, 5, 0, 3, 6, -1, 0, 5, 4, 1, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 1f, 0f, -2.46965313f),
					new float4(0f, -1f, 0f, -1.57159686f),
					new float4(-1f, 0f, 0f, -4.87999964f),
					new float4(0f, 0f, -1f, -2.44f),
					new float4(1f, 0f, 0f, -6.10000038f),
					new float4(0f, 0f, 1f, -3.05f),
				}, contents: 1, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(49.2575f, -14.8984032f, -0.305f, 1f)), name: "Water 1003 #895")
					.WithMesh(new float3[] { new float3(-4.87999964f, 2.46965313f, 3.05f), new float3(6.10000038f, 2.46965313f, 3.05f), new float3(6.10000038f, 2.46965313f, -2.44f), new float3(-4.87999964f, 2.46965313f, -2.44f), new float3(6.10000038f, -1.57159686f, 3.05f), new float3(-4.87999964f, -1.57159686f, 3.05f), new float3(-4.87999964f, -1.57159686f, -2.44f), new float3(6.10000038f, -1.57159686f, -2.44f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 5, 0, 3, 6, -1, 2, 7, 6, 3, -1, 1, 4, 7, 2, -1, 4, 1, 0, 5, -1 }))
				;
		}

		// bm_c2a3a, Model: the mesh draws nothing where the exact CSG has a face; Water 265 #257 face 5 near (33, -23.78,
		// 31.72) (3 brushes)
		static ContentsScene BmC2a3aModel0Site4()
		{
			return new ContentsScene()
				.WithContentsList("Solid", "Water", "Glass", "Grate")
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 1f, 0f, -0.9065271f),
					new float4(0f, -0.9363292f, -0.351123452f, -1.57862568f),
					new float4(-1f, 0f, 0f, -2.03333449f),
					new float4(1f, 0f, 0f, -1.62666547f),
					new float4(0f, 0f, 1f, -4.87999964f),
					new float4(0f, 0f, -1f, -4.88f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(34.2108345f, -22.4090271f, 26.84f, 1f)), name: "Solid 263 #255")
					.WithMesh(new float3[] { new float3(1.62666547f, 0.9065271f, -4.88f), new float3(-2.03333449f, 0.9065271f, -4.88f), new float3(-2.03333449f, 0.9065271f, 4.87999964f), new float3(1.62666547f, 0.9065271f, 4.87999964f), new float3(-2.03333449f, 0.144027129f, -4.88f), new float3(1.62666547f, 0.144027129f, -4.88f), new float3(1.62666547f, -3.51597285f, 4.87999964f), new float3(-2.03333449f, -3.51597285f, 4.87999964f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 1, 4, 7, 2, -1, 5, 0, 3, 6, -1, 6, 3, 2, 7, -1, 0, 5, 4, 1, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 1f, 0f, -2.61685681f),
					new float4(0f, -1f, 0f, -4.11220551f),
					new float4(-1f, 0f, 0f, -2.03333449f),
					new float4(1f, 0f, 0f, -1.62666547f),
					new float4(0f, 0f, 1f, -8.54f),
					new float4(0f, 0f, -1f, -8.54f),
				}, contents: 1, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(34.2108345f, -24.2527943f, 23.18f, 1f)), name: "Water 264 #256")
					.WithMesh(new float3[] { new float3(1.62666547f, 2.61685681f, -8.54f), new float3(-2.03333449f, 2.61685681f, -8.54f), new float3(-2.03333449f, 2.61685681f, 8.54f), new float3(1.62666547f, 2.61685681f, 8.54f), new float3(-2.03333449f, -4.11220551f, -8.54f), new float3(1.62666547f, -4.11220551f, -8.54f), new float3(1.62666547f, -4.11220551f, 8.54f), new float3(-2.03333449f, -4.11220551f, 8.54f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 1, 4, 7, 2, -1, 5, 0, 3, 6, -1, 6, 3, 2, 7, -1, 0, 5, 4, 1, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 1f, 0f, -4.11220455f),
					new float4(0f, -1f, 0f, -2.61685777f),
					new float4(-1f, 0f, 0f, -5.96444464f),
					new float4(1f, 0f, 0f, -7.45555544f),
					new float4(0f, 0f, 1f, -4.26999855f),
					new float4(0f, 0f, -1f, -4.270002f),
				}, contents: 1, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(34.0244446f, -25.7481422f, 35.99f, 1f)), name: "Water 265 #257")
					.WithMesh(new float3[] { new float3(7.45555544f, 4.11220455f, -4.270002f), new float3(-5.96444464f, 4.11220455f, -4.270002f), new float3(-5.96444464f, 4.11220455f, 4.26999855f), new float3(7.45555544f, 4.11220455f, 4.26999855f), new float3(-5.96444464f, -2.61685777f, -4.270002f), new float3(7.45555544f, -2.61685777f, -4.270002f), new float3(7.45555544f, -2.61685777f, 4.26999855f), new float3(-5.96444464f, -2.61685777f, 4.26999855f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 1, 4, 7, 2, -1, 5, 0, 3, 6, -1, 6, 3, 2, 7, -1, 0, 5, 4, 1, -1 }))
				;
		}

		// bm_c2a3a, Model: the mesh draws nothing where the exact CSG has a face; Water 1001 #893 face 3 near (60.547,
		// -13.306, 19.825) (4 brushes)
		static ContentsScene BmC2a3aModel0Site5()
		{
			return new ContentsScene()
				.WithContentsList("Solid", "Water", "Glass", "Grate")
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 0f, 1f, -0.15249908f),
					new float4(0f, 0f, -1f, -0.152500913f),
					new float4(1f, 0f, 0f, -4.346251f),
					new float4(0f, 1f, 0f, -1.35555589f),
					new float4(-1f, 0f, 0f, -4.346249f),
					new float4(0f, -1f, 0f, -1.08444417f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(61.99125f, -13.0980558f, 19.9775f, 1f)), name: "Solid 463 #432")
					.WithMesh(new float3[] { new float3(-4.346249f, -1.08444417f, 0.15249908f), new float3(4.346251f, -1.08444417f, 0.15249908f), new float3(4.346251f, 1.35555589f, 0.15249908f), new float3(-4.346249f, 1.35555589f, 0.15249908f), new float3(4.346251f, -1.08444417f, -0.152500913f), new float3(-4.346249f, -1.08444417f, -0.152500913f), new float3(-4.346249f, 1.35555589f, -0.152500913f), new float3(4.346251f, 1.35555589f, -0.152500913f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 1, 4, 7, 2, -1, 6, 3, 2, 7, -1, 5, 0, 3, 6, -1, 0, 5, 4, 1, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 0f, 1f, -0.15249908f),
					new float4(0f, 0f, -1f, -0.152500913f),
					new float4(-1f, 0f, 0f, -1.355557f),
					new float4(1f, 0f, 0f, -1.08444309f),
					new float4(0f, -1f, 0f, -0.6523609f),
					new float4(0f, 1f, 0f, -0.415139079f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(59.0005569f, -14.5976391f, 19.9775f, 1f)), name: "Solid 465 #434")
					.WithMesh(new float3[] { new float3(1.08444309f, 0.415139079f, 0.15249908f), new float3(-1.355557f, 0.415139079f, 0.15249908f), new float3(-1.355557f, -0.6523609f, 0.15249908f), new float3(1.08444309f, -0.6523609f, 0.15249908f), new float3(-1.355557f, 0.415139079f, -0.152500913f), new float3(1.08444309f, 0.415139079f, -0.152500913f), new float3(1.08444309f, -0.6523609f, -0.152500913f), new float3(-1.355557f, -0.6523609f, -0.152500913f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 1, 4, 7, 2, -1, 5, 0, 3, 6, -1, 6, 3, 2, 7, -1, 0, 5, 4, 1, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 1f, 0f, -2.99493027f),
					new float4(0f, -1f, 0f, -4.70632f),
					new float4(-1f, 0f, 0f, -5.08333254f),
					new float4(1f, 0f, 0f, -4.06666756f),
					new float4(0f, 0f, 1f, -3.50749946f),
					new float4(0f, 0f, -1f, -3.50750041f),
				}, contents: 1, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(62.5758324f, -15.42368f, 23.3325f, 1f)), name: "Water 481 #450")
					.WithMesh(new float3[] { new float3(4.06666756f, 2.99493027f, -3.50750041f), new float3(-5.08333254f, 2.99493027f, -3.50750041f), new float3(-5.08333254f, 2.99493027f, 3.50749946f), new float3(4.06666756f, 2.99493027f, 3.50749946f), new float3(-5.08333254f, -4.70632f, -3.50750041f), new float3(4.06666756f, -4.70632f, -3.50750041f), new float3(4.06666756f, -4.70632f, 3.50749946f), new float3(-5.08333254f, -4.70632f, 3.50749946f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 1, 4, 7, 2, -1, 5, 0, 3, 6, -1, 6, 3, 2, 7, -1, 0, 5, 4, 1, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 1f, 0f, -3.232153f),
					new float4(0f, -1f, 0f, -5.079097f),
					new float4(1f, 0f, 0f, -5.42222166f),
					new float4(0f, 0f, 1f, -7.625f),
					new float4(0f, 0f, -1f, -7.625f),
					new float4(-1f, 0f, 0f, -4.33777857f),
				}, contents: 1, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(60.00028f, -15.660903f, 12.2f, 1f)), name: "Water 1001 #893")
					.WithMesh(new float3[] { new float3(-4.33777857f, 3.232153f, -7.625f), new float3(-4.33777857f, 3.232153f, 7.625f), new float3(5.42222166f, 3.232153f, 7.625f), new float3(5.42222166f, 3.232153f, -7.625f), new float3(-4.33777857f, -5.079097f, 7.625f), new float3(-4.33777857f, -5.079097f, -7.625f), new float3(5.42222166f, -5.079097f, -7.625f), new float3(5.42222166f, -5.079097f, 7.625f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 6, 3, 2, 7, -1, 1, 4, 7, 2, -1, 5, 0, 3, 6, -1, 0, 5, 4, 1, -1 }))
				;
		}

		// bm_c2a3a, Model: the mesh draws nothing where the exact CSG has a face; Water 1002 #894 face 5 near (55, -13.229,
		// 2.745) (4 brushes)
		static ContentsScene BmC2a3aModel0Site6()
		{
			return new ContentsScene()
				.WithContentsList("Solid", "Water", "Glass", "Grate")
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 1f, 0f, -1.49111152f),
					new float4(0f, -1f, 0f, -0.94888854f),
					new float4(-1f, 0f, 0f, -0.372779548f),
					new float4(0.707106769f, 0f, 0.707106769f, -0.3294909f),
					new float4(0f, 0f, 1f, -0.152500033f),
					new float4(0f, 0f, -1f, -0.152499974f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(55.34903f, -15.5211115f, 2.5925f, 1f)), name: "Solid 84 #81")
					.WithMesh(new float3[] { new float3(0.6184705f, 1.49111152f, -0.152499974f), new float3(-0.372779548f, 1.49111152f, -0.152499974f), new float3(-0.372779548f, 1.49111152f, 0.152500033f), new float3(0.313470483f, 1.49111152f, 0.152500033f), new float3(-0.372779548f, -0.94888854f, -0.152499974f), new float3(0.6184705f, -0.94888854f, -0.152499974f), new float3(0.313470483f, -0.94888854f, 0.152500033f), new float3(-0.372779548f, -0.94888854f, 0.152500033f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 1, 4, 7, 2, -1, 5, 0, 3, 6, -1, 6, 3, 2, 7, -1, 0, 5, 4, 1, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 0f, 1f, -0.152500033f),
					new float4(0f, 0f, -1f, -0.152499974f),
					new float4(0.707106769f, 0f, 0.707106769f, -3.738239f),
					new float4(-0.707106769f, 0f, 0.707106769f, -4.67279625f),
					new float4(0f, -1f, 0f, -1.491111f),
					new float4(0f, 1f, 0f, -0.948888958f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(50.52833f, -12.5388889f, 2.5925f, 1f)), name: "Solid 85 #82")
					.WithMesh(new float3[] { new float3(5.13416862f, 0.948888958f, 0.152500033f), new float3(-6.455832f, 0.948888958f, 0.152500033f), new float3(-6.455832f, -1.491111f, 0.152500033f), new float3(5.13416862f, -1.491111f, 0.152500033f), new float3(-6.760832f, 0.948888958f, -0.152499974f), new float3(5.43916845f, 0.948888958f, -0.152499974f), new float3(5.43916845f, -1.491111f, -0.152499974f), new float3(-6.760832f, -1.491111f, -0.152499974f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 5, 0, 3, 6, -1, 1, 4, 7, 2, -1, 2, 7, 6, 3, -1, 4, 1, 0, 5, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 1f, 0f, -5.079097f),
					new float4(0f, -1f, 0f, -3.23215318f),
					new float4(-1f, 0f, 0f, -5.1511097f),
					new float4(0f, 0f, 1f, -7.591111f),
					new float4(1f, 0f, 0f, -6.43889f),
					new float4(0f, 0f, -1f, -9.488889f),
				}, contents: 1, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(49.22361f, -17.5078468f, 12.2338886f, 1f)), name: "Water 1002 #894")
					.WithMesh(new float3[] { new float3(6.43889f, 5.079097f, -9.488889f), new float3(-5.1511097f, 5.079097f, -9.488889f), new float3(-5.1511097f, 5.079097f, 7.591111f), new float3(6.43889f, 5.079097f, 7.591111f), new float3(-5.1511097f, -3.23215318f, -9.488889f), new float3(6.43889f, -3.23215318f, -9.488889f), new float3(6.43889f, -3.23215318f, 7.591111f), new float3(-5.1511097f, -3.23215318f, 7.591111f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 1, 4, 7, 2, -1, 6, 3, 2, 7, -1, 5, 0, 3, 6, -1, 0, 5, 4, 1, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 1f, 0f, -2.46965313f),
					new float4(0f, -1f, 0f, -1.57159686f),
					new float4(-1f, 0f, 0f, -4.87999964f),
					new float4(0f, 0f, -1f, -2.44f),
					new float4(1f, 0f, 0f, -6.10000038f),
					new float4(0f, 0f, 1f, -3.05f),
				}, contents: 1, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(49.2575f, -14.8984032f, -0.305f, 1f)), name: "Water 1003 #895")
					.WithMesh(new float3[] { new float3(-4.87999964f, 2.46965313f, 3.05f), new float3(6.10000038f, 2.46965313f, 3.05f), new float3(6.10000038f, 2.46965313f, -2.44f), new float3(-4.87999964f, 2.46965313f, -2.44f), new float3(6.10000038f, -1.57159686f, 3.05f), new float3(-4.87999964f, -1.57159686f, 3.05f), new float3(-4.87999964f, -1.57159686f, -2.44f), new float3(6.10000038f, -1.57159686f, -2.44f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 5, 0, 3, 6, -1, 2, 7, 6, 3, -1, 1, 4, 7, 2, -1, 4, 1, 0, 5, -1 }))
				;
		}

		// bm_c2a3a, Model: the mesh draws nothing where the exact CSG has a face; Water 480 #449 face 2 near (66.643, -15,
		// 22.418) (3 brushes)
		static ContentsScene BmC2a3aModel0Site7()
		{
			return new ContentsScene()
				.WithContentsList("Solid", "Water", "Glass", "Grate")
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(-1f, 0f, 0f, -0.169442743f),
					new float4(1f, 0f, 0f, -0.135557249f),
					new float4(-0.707106769f, 0f, 0.707106769f, -2.540083f),
					new float4(0f, 0f, -1f, -3.592223f),
					new float4(0f, -1f, 0f, -0.3558332f),
					new float4(0f, 1f, 0f, -0.5591668f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(66.50694f, -14.7416668f, 23.417223f, 1f)), name: "Solid 466 #435")
					.WithMesh(new float3[] { new float3(-0.169442743f, 0.5591668f, 3.422777f), new float3(-0.169442743f, 0.5591668f, -3.592223f), new float3(-0.169442743f, -0.3558332f, -3.592223f), new float3(-0.169442743f, -0.3558332f, 3.422777f), new float3(0.135557249f, 0.5591668f, -3.592223f), new float3(0.135557249f, 0.5591668f, 3.727777f), new float3(0.135557249f, -0.3558332f, 3.727777f), new float3(0.135557249f, -0.3558332f, -3.592223f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 5, 0, 3, 6, -1, 1, 4, 7, 2, -1, 2, 7, 6, 3, -1, 4, 1, 0, 5, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 1f, 0f, -2.04604149f),
					new float4(0f, -1f, 0f, -3.21520853f),
					new float4(-1f, 0f, 0f, -4.06666756f),
					new float4(1f, 0f, 0f, -3.25333261f),
					new float4(0f, 0f, 1f, -5.26124954f),
					new float4(0f, 0f, -1f, -5.2612505f),
				}, contents: 1, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(70.70917f, -14.4747915f, 19.74875f, 1f)), name: "Water 480 #449")
					.WithMesh(new float3[] { new float3(3.25333261f, 2.04604149f, -5.2612505f), new float3(-4.06666756f, 2.04604149f, -5.2612505f), new float3(-4.06666756f, 2.04604149f, 5.26124954f), new float3(3.25333261f, 2.04604149f, 5.26124954f), new float3(-4.06666756f, -3.21520853f, -5.2612505f), new float3(3.25333261f, -3.21520853f, -5.2612505f), new float3(3.25333261f, -3.21520853f, 5.26124954f), new float3(-4.06666756f, -3.21520853f, 5.26124954f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 1, 4, 7, 2, -1, 5, 0, 3, 6, -1, 6, 3, 2, 7, -1, 0, 5, 4, 1, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 1f, 0f, -2.99493027f),
					new float4(0f, -1f, 0f, -4.70632f),
					new float4(-1f, 0f, 0f, -5.08333254f),
					new float4(1f, 0f, 0f, -4.06666756f),
					new float4(0f, 0f, 1f, -3.50749946f),
					new float4(0f, 0f, -1f, -3.50750041f),
				}, contents: 1, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(62.5758324f, -15.42368f, 23.3325f, 1f)), name: "Water 481 #450")
					.WithMesh(new float3[] { new float3(4.06666756f, 2.99493027f, -3.50750041f), new float3(-5.08333254f, 2.99493027f, -3.50750041f), new float3(-5.08333254f, 2.99493027f, 3.50749946f), new float3(4.06666756f, 2.99493027f, 3.50749946f), new float3(-5.08333254f, -4.70632f, -3.50750041f), new float3(4.06666756f, -4.70632f, -3.50750041f), new float3(4.06666756f, -4.70632f, 3.50749946f), new float3(-5.08333254f, -4.70632f, 3.50749946f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 1, 4, 7, 2, -1, 5, 0, 3, 6, -1, 6, 3, 2, 7, -1, 0, 5, 4, 1, -1 }))
				;
		}

		// bm_c2a3a, Model: the mesh draws nothing where the exact CSG has a face; Solid 219 #211 face 4 near (33.5, -24.171,
		// 14.64) (3 brushes)
		static ContentsScene BmC2a3aModel0Site8()
		{
			return new ContentsScene()
				.WithContentsList("Solid", "Water", "Glass", "Grate")
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 1f, 0f, -2.88902783f),
					new float4(0f, -1f, 0f, -1.83847213f),
					new float4(-1f, 0f, 0f, -1.76222384f),
					new float4(1f, 0f, 0f, -2.20277619f),
					new float4(0f, 0f, -1f, -0.07625042f),
					new float4(0f, 0f, 1f, -0.07624958f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(33.7872238f, -26.9840279f, 14.71625f, 1f)), name: "Solid 219 #211")
					.WithMesh(new float3[] { new float3(-1.76222384f, 2.88902783f, 0.07624958f), new float3(2.20277619f, 2.88902783f, 0.07624958f), new float3(2.20277619f, 2.88902783f, -0.07625042f), new float3(-1.76222384f, 2.88902783f, -0.07625042f), new float3(2.20277619f, -1.83847213f, 0.07624958f), new float3(-1.76222384f, -1.83847213f, 0.07624958f), new float3(-1.76222384f, -1.83847213f, -0.07625042f), new float3(2.20277619f, -1.83847213f, -0.07625042f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 5, 0, 3, 6, -1, 1, 4, 7, 2, -1, 2, 7, 6, 3, -1, 4, 1, 0, 5, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 1f, 0f, -1.01560712f),
					new float4(0f, -1f, 0f, -1.59595537f),
					new float4(-1f, 0f, 0f, -11.5222216f),
					new float4(1f, 0f, 0f, -9.217778f),
					new float4(0f, 0f, 1f, -7.31999969f),
					new float4(0f, 0f, -1f, -7.32f),
				}, contents: 1, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(35.15972f, -22.6515446f, 7.32f, 1f)), name: "Water 260 #252")
					.WithMesh(new float3[] { new float3(9.217778f, 1.01560712f, -7.32f), new float3(-11.5222216f, 1.01560712f, -7.32f), new float3(-11.5222216f, 1.01560712f, 7.31999969f), new float3(9.217778f, 1.01560712f, 7.31999969f), new float3(-11.5222216f, -1.59595537f, -7.32f), new float3(9.217778f, -1.59595537f, -7.32f), new float3(9.217778f, -1.59595537f, 7.31999969f), new float3(-11.5222216f, -1.59595537f, 7.31999969f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 1, 4, 7, 2, -1, 5, 0, 3, 6, -1, 6, 3, 2, 7, -1, 0, 5, 4, 1, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 1f, 0f, -2.61685681f),
					new float4(0f, -1f, 0f, -4.11220551f),
					new float4(-1f, 0f, 0f, -2.03333449f),
					new float4(1f, 0f, 0f, -1.62666547f),
					new float4(0f, 0f, 1f, -8.54f),
					new float4(0f, 0f, -1f, -8.54f),
				}, contents: 1, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(34.2108345f, -24.2527943f, 23.18f, 1f)), name: "Water 264 #256")
					.WithMesh(new float3[] { new float3(1.62666547f, 2.61685681f, -8.54f), new float3(-2.03333449f, 2.61685681f, -8.54f), new float3(-2.03333449f, 2.61685681f, 8.54f), new float3(1.62666547f, 2.61685681f, 8.54f), new float3(-2.03333449f, -4.11220551f, -8.54f), new float3(1.62666547f, -4.11220551f, -8.54f), new float3(1.62666547f, -4.11220551f, 8.54f), new float3(-2.03333449f, -4.11220551f, 8.54f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 1, 4, 7, 2, -1, 5, 0, 3, 6, -1, 6, 3, 2, 7, -1, 0, 5, 4, 1, -1 }))
				;
		}

		// bm_c2a3a, Model: the mesh draws nothing where the exact CSG has a face; Solid 258 #250 face 4 near (28.5, -17.804,
		// 10.98) (3 brushes)
		static ContentsScene BmC2a3aModel0Site9()
		{
			return new ContentsScene()
				.WithContentsList("Solid", "Water", "Glass", "Grate")
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 1f, 0f, -1.48263931f),
					new float4(0f, -1f, 0f, -2.32986069f),
					new float4(-1f, 0f, 0f, -0.762499154f),
					new float4(0.4472136f, 0f, -0.8944272f, -0.272801042f),
					new float4(0f, 0f, 1f, -0.07624958f),
					new float4(0f, 0f, -1f, -0.07625042f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(27.755f, -16.73264f, 10.90375f, 1f)), name: "Solid 258 #250")
					.WithMesh(new float3[] { new float3(0.457500845f, 1.48263931f, -0.07625042f), new float3(-0.762499154f, 1.48263931f, -0.07625042f), new float3(-0.762499154f, 1.48263931f, 0.07624958f), new float3(0.7625008f, 1.48263931f, 0.07624958f), new float3(-0.762499154f, -2.32986069f, -0.07625042f), new float3(0.457500845f, -2.32986069f, -0.07625042f), new float3(0.7625008f, -2.32986069f, 0.07624958f), new float3(-0.762499154f, -2.32986069f, 0.07624958f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 1, 4, 7, 2, -1, 5, 0, 3, 6, -1, 6, 3, 2, 7, -1, 0, 5, 4, 1, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 1f, 0f, -1.35131979f),
					new float4(0f, -1f, 0f, -0.8599302f),
					new float4(-1f, 0f, 0f, -5.625555f),
					new float4(1f, 0f, 0f, -7.031945f),
					new float4(0f, 0f, -1f, -4.04124975f),
					new float4(0f, 0f, 1f, -4.04125f),
				}, contents: 1, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(33.9905548f, -18.05007f, 6.93875f, 1f)), name: "Water 360 #332")
					.WithMesh(new float3[] { new float3(-5.625555f, 1.35131979f, 4.04125f), new float3(7.031945f, 1.35131979f, 4.04125f), new float3(7.031945f, 1.35131979f, -4.04124975f), new float3(-5.625555f, 1.35131979f, -4.04124975f), new float3(7.031945f, -0.8599302f, 4.04125f), new float3(-5.625555f, -0.8599302f, 4.04125f), new float3(-5.625555f, -0.8599302f, -4.04124975f), new float3(7.031945f, -0.8599302f, -4.04124975f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 5, 0, 3, 6, -1, 1, 4, 7, 2, -1, 2, 7, 6, 3, -1, 4, 1, 0, 5, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 1f, 0f, -1.35131979f),
					new float4(0f, -1f, 0f, -0.8599302f),
					new float4(-1f, 0f, 0f, -6.16777849f),
					new float4(1f, 0f, 0f, -7.70972157f),
					new float4(0f, 0f, 1f, -1.82999957f),
					new float4(0f, 0f, -1f, -1.8300004f),
				}, contents: 1, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(33.31278f, -18.05007f, 12.81f, 1f)), name: "Water 1042 #929")
					.WithMesh(new float3[] { new float3(7.70972157f, 1.35131979f, -1.8300004f), new float3(-6.16777849f, 1.35131979f, -1.8300004f), new float3(-6.16777849f, 1.35131979f, 1.82999957f), new float3(7.70972157f, 1.35131979f, 1.82999957f), new float3(-6.16777849f, -0.8599302f, -1.8300004f), new float3(7.70972157f, -0.8599302f, -1.8300004f), new float3(7.70972157f, -0.8599302f, 1.82999957f), new float3(-6.16777849f, -0.8599302f, 1.82999957f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 1, 4, 7, 2, -1, 5, 0, 3, 6, -1, 6, 3, 2, 7, -1, 0, 5, 4, 1, -1 }))
				;
		}

		// bm_c2a3a, Model: the mesh draws nothing where the exact CSG has a face; Solid 462 #431 face 1 near (60.547,
		// -18.224, 19.825) (7 brushes)
		static ContentsScene BmC2a3aModel0Site10()
		{
			return new ContentsScene()
				.WithContentsList("Solid", "Water", "Glass", "Grate")
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 0f, 1f, -0.15249908f),
					new float4(0f, 0f, -1f, -0.152500913f),
					new float4(1f, 0f, 0f, -3.47361231f),
					new float4(-1f, 0f, 0f, -2.77888775f),
					new float4(0f, 1f, 0f, -2.70263958f),
					new float4(0f, -1f, 0f, -1.71986032f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(62.8638878f, -19.02014f, 19.9775f, 1f)), name: "Solid 462 #431")
					.WithMesh(new float3[] { new float3(-2.77888775f, -1.71986032f, 0.15249908f), new float3(3.47361231f, -1.71986032f, 0.15249908f), new float3(3.47361231f, 2.70263958f, 0.15249908f), new float3(-2.77888775f, 2.70263958f, 0.15249908f), new float3(3.47361231f, -1.71986032f, -0.152500913f), new float3(-2.77888775f, -1.71986032f, -0.152500913f), new float3(-2.77888775f, 2.70263958f, -0.152500913f), new float3(3.47361231f, 2.70263958f, -0.152500913f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 1, 4, 7, 2, -1, 5, 0, 3, 6, -1, 6, 3, 2, 7, -1, 0, 5, 4, 1, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 1f, 0f, -2.04604149f),
					new float4(0f, -1f, 0f, -3.21520853f),
					new float4(-1f, 0f, 0f, -4.06666756f),
					new float4(1f, 0f, 0f, -3.25333261f),
					new float4(0f, 0f, 1f, -5.26124954f),
					new float4(0f, 0f, -1f, -5.2612505f),
				}, contents: 1, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(70.70917f, -14.4747915f, 19.74875f, 1f)), name: "Water 480 #449")
					.WithMesh(new float3[] { new float3(3.25333261f, 2.04604149f, -5.2612505f), new float3(-4.06666756f, 2.04604149f, -5.2612505f), new float3(-4.06666756f, 2.04604149f, 5.26124954f), new float3(3.25333261f, 2.04604149f, 5.26124954f), new float3(-4.06666756f, -3.21520853f, -5.2612505f), new float3(3.25333261f, -3.21520853f, -5.2612505f), new float3(3.25333261f, -3.21520853f, 5.26124954f), new float3(-4.06666756f, -3.21520853f, 5.26124954f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 1, 4, 7, 2, -1, 5, 0, 3, 6, -1, 6, 3, 2, 7, -1, 0, 5, 4, 1, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 1f, 0f, -2.99493027f),
					new float4(0f, -1f, 0f, -4.70632f),
					new float4(-1f, 0f, 0f, -5.08333254f),
					new float4(1f, 0f, 0f, -4.06666756f),
					new float4(0f, 0f, 1f, -3.50749946f),
					new float4(0f, 0f, -1f, -3.50750041f),
				}, contents: 1, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(62.5758324f, -15.42368f, 23.3325f, 1f)), name: "Water 481 #450")
					.WithMesh(new float3[] { new float3(4.06666756f, 2.99493027f, -3.50750041f), new float3(-5.08333254f, 2.99493027f, -3.50750041f), new float3(-5.08333254f, 2.99493027f, 3.50749946f), new float3(4.06666756f, 2.99493027f, 3.50749946f), new float3(-5.08333254f, -4.70632f, -3.50750041f), new float3(4.06666756f, -4.70632f, -3.50750041f), new float3(4.06666756f, -4.70632f, 3.50749946f), new float3(-5.08333254f, -4.70632f, 3.50749946f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 1, 4, 7, 2, -1, 5, 0, 3, 6, -1, 6, 3, 2, 7, -1, 0, 5, 4, 1, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, -1f, 0f, -2.92800045f),
					new float4(0f, 0f, 1f, -0.610000849f),
					new float4(1f, 0f, 0f, -0.737085f),
					new float4(0f, 1f, 0f, -1.95199966f),
					new float4(-0.6375677f, 0f, -0.7703943f, -0.469938844f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(54.925415f, -17.202f, 20.13f, 1f)), name: "Solid 961 #863")
					.WithMesh(new float3[] { new float3(-1.47416508f, -2.92800045f, 0.610000849f), new float3(0.737085f, -2.92800045f, -1.21999919f), new float3(0.737085f, -2.92800045f, 0.610000849f), new float3(-1.47416508f, 1.95199966f, 0.610000849f), new float3(0.737085f, 1.95199966f, 0.610000849f), new float3(0.737085f, 1.95199966f, -1.21999919f) }, new int[] { 0, 1, 2, -1, 3, 0, 2, 4, -1, 1, 5, 4, 2, -1, 5, 3, 4, -1, 3, 5, 1, 0, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, -1f, 0f, -2.92800045f),
					new float4(0f, 0f, -1f, -0.610000432f),
					new float4(-1f, 0f, 0f, -0.7370816f),
					new float4(0f, 1f, 0f, -1.95199966f),
					new float4(0.6375677f, 0f, 0.7703943f, -0.4699413f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(54.18833f, -17.202f, 19.52f, 1f)), name: "Solid 962 #864")
					.WithMesh(new float3[] { new float3(1.47416842f, -2.92800045f, -0.610000432f), new float3(-0.7370816f, -2.92800045f, 1.21999955f), new float3(-0.7370816f, -2.92800045f, -0.610000432f), new float3(1.47416842f, 1.95199966f, -0.610000432f), new float3(-0.7370816f, 1.95199966f, -0.610000432f), new float3(-0.7370816f, 1.95199966f, 1.21999955f) }, new int[] { 0, 1, 2, -1, 3, 0, 2, 4, -1, 1, 5, 4, 2, -1, 5, 3, 4, -1, 3, 5, 1, 0, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 1f, 0f, -3.232153f),
					new float4(0f, -1f, 0f, -5.079097f),
					new float4(1f, 0f, 0f, -5.42222166f),
					new float4(0f, 0f, 1f, -7.625f),
					new float4(0f, 0f, -1f, -7.625f),
					new float4(-1f, 0f, 0f, -4.33777857f),
				}, contents: 1, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(60.00028f, -15.660903f, 12.2f, 1f)), name: "Water 1001 #893")
					.WithMesh(new float3[] { new float3(-4.33777857f, 3.232153f, -7.625f), new float3(-4.33777857f, 3.232153f, 7.625f), new float3(5.42222166f, 3.232153f, 7.625f), new float3(5.42222166f, 3.232153f, -7.625f), new float3(-4.33777857f, -5.079097f, 7.625f), new float3(-4.33777857f, -5.079097f, -7.625f), new float3(5.42222166f, -5.079097f, -7.625f), new float3(5.42222166f, -5.079097f, 7.625f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 6, 3, 2, 7, -1, 1, 4, 7, 2, -1, 5, 0, 3, 6, -1, 0, 5, 4, 1, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(-0.498233438f, 0.867042959f, 0f, -0.0235092286f),
					new float4(0.498233438f, -0.867042959f, 0f, -0.033677265f),
					new float4(0.866026f, 0.499998957f, 0f, -0.0291645117f),
					new float4(-0.866026f, -0.499998957f, 0f, -0.0278870817f),
					new float4(0f, 0f, 1f, -0.0190631859f),
					new float4(0f, 0f, -1f, -0.0190618131f),
					new float4(0.259874f, 0.9656425f, 0f, -0.0238133278f),
					new float4(-0.9670051f, 0.254757017f, 0f, -0.022905089f),
					new float4(-0.2599144f, -0.965631664f, 0f, -0.0300926324f),
					new float4(0.9670079f, -0.254746377f, 0f, -0.0309730023f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(60.12794f, -15.9377127f, 19.8059368f, 1f)), name: "Solid 1080 #951")
					.WithMesh(new float3[] { new float3(-0.01949463f, 0.01591196f, -0.0190618131f), new float3(-0.01949463f, 0.01591196f, 0.0190631859f), new float3(-0.002908018f, 0.0254432112f, 0.0190631859f), new float3(-0.002908018f, 0.0254432112f, -0.0190618131f), new float3(0.0256857313f, -0.0240815915f, -0.0190618131f), new float3(0.0256857313f, -0.0240815915f, 0.0190631859f), new float3(0.009099121f, -0.0336128436f, 0.0190631859f), new float3(0.009099121f, -0.0336128436f, -0.0190618131f), new float3(0.0325456262f, 0.00195831363f, 0.0190631859f), new float3(0.0325456262f, 0.00195831363f, -0.0190618131f), new float3(0.0230143741f, 0.01846697f, -0.0190618131f), new float3(0.0230143741f, 0.01846697f, 0.0190631859f), new float3(-0.0168232713f, -0.0266354382f, -0.0190618131f), new float3(-0.0168232713f, -0.0266354382f, 0.0190631859f), new float3(-0.0263545234f, -0.0101267807f, 0.0190631859f), new float3(-0.0263545234f, -0.0101267807f, -0.0190618131f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 8, 9, 10, 11, -1, 12, 13, 14, 15, -1, 5, 8, 11, 2, 1, 14, 13, 6, -1, 9, 4, 7, 12, 15, 0, 3, 10, -1, 10, 3, 2, 11, -1, 0, 15, 14, 1, -1, 12, 7, 6, 13, -1, 4, 9, 8, 5, -1 }))
				;
		}

		// bm_c2a3a, Model: the mesh draws nothing where the exact CSG has a face; Water 1001 #893 face 3 near (57.75,
		// -18.567, 19.825) (3 brushes)
		static ContentsScene BmC2a3aModel0Site11()
		{
			return new ContentsScene()
				.WithContentsList("Solid", "Water", "Glass", "Grate")
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, -1f, 0f, -1.18611169f),
					new float4(0f, 0f, 1f, -0.15249908f),
					new float4(0f, 0f, -1f, -0.152500913f),
					new float4(-1f, 0f, 0f, -1.08444262f),
					new float4(1f, 0f, 0f, -1.35555744f),
					new float4(0f, 1f, 0f, -1.86388826f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(58.7294426f, -19.5538883f, 19.9775f, 1f)), name: "Solid 87 #84")
					.WithMesh(new float3[] { new float3(1.35555744f, -1.18611169f, -0.152500913f), new float3(1.35555744f, -1.18611169f, 0.15249908f), new float3(-1.08444262f, -1.18611169f, 0.15249908f), new float3(-1.08444262f, -1.18611169f, -0.152500913f), new float3(1.35555744f, 1.86388826f, 0.15249908f), new float3(-1.08444262f, 1.86388826f, 0.15249908f), new float3(-1.08444262f, 1.86388826f, -0.152500913f), new float3(1.35555744f, 1.86388826f, -0.152500913f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 2, 1, -1, 6, 7, 0, 3, -1, 5, 6, 3, 2, -1, 7, 4, 1, 0, -1, 4, 7, 6, 5, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 1f, 0f, -2.99493027f),
					new float4(0f, -1f, 0f, -4.70632f),
					new float4(-1f, 0f, 0f, -5.08333254f),
					new float4(1f, 0f, 0f, -4.06666756f),
					new float4(0f, 0f, 1f, -3.50749946f),
					new float4(0f, 0f, -1f, -3.50750041f),
				}, contents: 1, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(62.5758324f, -15.42368f, 23.3325f, 1f)), name: "Water 481 #450")
					.WithMesh(new float3[] { new float3(4.06666756f, 2.99493027f, -3.50750041f), new float3(-5.08333254f, 2.99493027f, -3.50750041f), new float3(-5.08333254f, 2.99493027f, 3.50749946f), new float3(4.06666756f, 2.99493027f, 3.50749946f), new float3(-5.08333254f, -4.70632f, -3.50750041f), new float3(4.06666756f, -4.70632f, -3.50750041f), new float3(4.06666756f, -4.70632f, 3.50749946f), new float3(-5.08333254f, -4.70632f, 3.50749946f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 1, 4, 7, 2, -1, 5, 0, 3, 6, -1, 6, 3, 2, 7, -1, 0, 5, 4, 1, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 1f, 0f, -3.232153f),
					new float4(0f, -1f, 0f, -5.079097f),
					new float4(1f, 0f, 0f, -5.42222166f),
					new float4(0f, 0f, 1f, -7.625f),
					new float4(0f, 0f, -1f, -7.625f),
					new float4(-1f, 0f, 0f, -4.33777857f),
				}, contents: 1, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(60.00028f, -15.660903f, 12.2f, 1f)), name: "Water 1001 #893")
					.WithMesh(new float3[] { new float3(-4.33777857f, 3.232153f, -7.625f), new float3(-4.33777857f, 3.232153f, 7.625f), new float3(5.42222166f, 3.232153f, 7.625f), new float3(5.42222166f, 3.232153f, -7.625f), new float3(-4.33777857f, -5.079097f, 7.625f), new float3(-4.33777857f, -5.079097f, -7.625f), new float3(5.42222166f, -5.079097f, -7.625f), new float3(5.42222166f, -5.079097f, 7.625f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 6, 3, 2, 7, -1, 1, 4, 7, 2, -1, 5, 0, 3, 6, -1, 0, 5, 4, 1, -1 }))
				;
		}

		// bm_c2a3a, Model: the mesh draws nothing where the exact CSG has a face; Water 1002 #894 face 4 near (55.662, -20,
		// 19.367) (3 brushes)
		static ContentsScene BmC2a3aModel0Site12()
		{
			return new ContentsScene()
				.WithContentsList("Solid", "Water", "Glass", "Grate")
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, -1f, 0f, -2.92800045f),
					new float4(0f, 0f, 1f, -0.610000849f),
					new float4(1f, 0f, 0f, -0.737085f),
					new float4(0f, 1f, 0f, -1.95199966f),
					new float4(-0.6375677f, 0f, -0.7703943f, -0.469938844f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(54.925415f, -17.202f, 20.13f, 1f)), name: "Solid 961 #863")
					.WithMesh(new float3[] { new float3(-1.47416508f, -2.92800045f, 0.610000849f), new float3(0.737085f, -2.92800045f, -1.21999919f), new float3(0.737085f, -2.92800045f, 0.610000849f), new float3(-1.47416508f, 1.95199966f, 0.610000849f), new float3(0.737085f, 1.95199966f, 0.610000849f), new float3(0.737085f, 1.95199966f, -1.21999919f) }, new int[] { 0, 1, 2, -1, 3, 0, 2, 4, -1, 1, 5, 4, 2, -1, 5, 3, 4, -1, 3, 5, 1, 0, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 1f, 0f, -3.232153f),
					new float4(0f, -1f, 0f, -5.079097f),
					new float4(1f, 0f, 0f, -5.42222166f),
					new float4(0f, 0f, 1f, -7.625f),
					new float4(0f, 0f, -1f, -7.625f),
					new float4(-1f, 0f, 0f, -4.33777857f),
				}, contents: 1, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(60.00028f, -15.660903f, 12.2f, 1f)), name: "Water 1001 #893")
					.WithMesh(new float3[] { new float3(-4.33777857f, 3.232153f, -7.625f), new float3(-4.33777857f, 3.232153f, 7.625f), new float3(5.42222166f, 3.232153f, 7.625f), new float3(5.42222166f, 3.232153f, -7.625f), new float3(-4.33777857f, -5.079097f, 7.625f), new float3(-4.33777857f, -5.079097f, -7.625f), new float3(5.42222166f, -5.079097f, -7.625f), new float3(5.42222166f, -5.079097f, 7.625f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 6, 3, 2, 7, -1, 1, 4, 7, 2, -1, 5, 0, 3, 6, -1, 0, 5, 4, 1, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 1f, 0f, -5.079097f),
					new float4(0f, -1f, 0f, -3.23215318f),
					new float4(-1f, 0f, 0f, -5.1511097f),
					new float4(0f, 0f, 1f, -7.591111f),
					new float4(1f, 0f, 0f, -6.43889f),
					new float4(0f, 0f, -1f, -9.488889f),
				}, contents: 1, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(49.22361f, -17.5078468f, 12.2338886f, 1f)), name: "Water 1002 #894")
					.WithMesh(new float3[] { new float3(6.43889f, 5.079097f, -9.488889f), new float3(-5.1511097f, 5.079097f, -9.488889f), new float3(-5.1511097f, 5.079097f, 7.591111f), new float3(6.43889f, 5.079097f, 7.591111f), new float3(-5.1511097f, -3.23215318f, -9.488889f), new float3(6.43889f, -3.23215318f, -9.488889f), new float3(6.43889f, -3.23215318f, 7.591111f), new float3(-5.1511097f, -3.23215318f, 7.591111f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 1, 4, 7, 2, -1, 6, 3, 2, 7, -1, 5, 0, 3, 6, -1, 0, 5, 4, 1, -1 }))
				;
		}

		// bm_c2a3a, Model: the mesh draws nothing where the exact CSG has a face; Water 864 #801 face 5 near (9, -16.737,
		// -3.203) (3 brushes)
		static ContentsScene BmC2a3aModel0Site13()
		{
			return new ContentsScene()
				.WithContentsList("Solid", "Water", "Glass", "Grate")
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 1f, 0f, -0.186389238f),
					new float4(0f, 0f, 1f, -0.152500018f),
					new float4(0f, 0f, -1f, -0.152499974f),
					new float4(-1f, 0f, 0f, -0.5930558f),
					new float4(1f, 0f, 0f, -0.47444424f),
					new float4(0f, -1f, 0f, -0.118610762f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(9.209306f, -16.65639f, -3.355f, 1f)), name: "Solid 862 #799")
					.WithMesh(new float3[] { new float3(0.47444424f, 0.186389238f, 0.152500018f), new float3(0.47444424f, 0.186389238f, -0.152499974f), new float3(-0.5930558f, 0.186389238f, -0.152499974f), new float3(-0.5930558f, 0.186389238f, 0.152500018f), new float3(-0.5930558f, -0.118610762f, 0.152500018f), new float3(0.47444424f, -0.118610762f, 0.152500018f), new float3(0.47444424f, -0.118610762f, -0.152499974f), new float3(-0.5930558f, -0.118610762f, -0.152499974f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 0, 3, -1, 6, 7, 2, 1, -1, 7, 4, 3, 2, -1, 5, 6, 1, 0, -1, 6, 5, 4, 7, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 1f, 0f, -1.35131979f),
					new float4(0f, -1f, 0f, -0.8599302f),
					new float4(-1f, 0f, 0f, -1.35555553f),
					new float4(1f, 0f, 0f, -1.69444442f),
					new float4(0f, 0f, 1f, -1.525f),
					new float4(0f, 0f, -1f, -1.5250001f),
				}, contents: 1, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(8.980556f, -18.05007f, -4.7275f, 1f)), name: "Water 863 #800")
					.WithMesh(new float3[] { new float3(1.69444442f, 1.35131979f, -1.5250001f), new float3(-1.35555553f, 1.35131979f, -1.5250001f), new float3(-1.35555553f, 1.35131979f, 1.525f), new float3(1.69444442f, 1.35131979f, 1.525f), new float3(-1.35555553f, -0.8599302f, -1.5250001f), new float3(1.69444442f, -0.8599302f, -1.5250001f), new float3(1.69444442f, -0.8599302f, 1.525f), new float3(-1.35555553f, -0.8599302f, 1.525f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 1, 4, 7, 2, -1, 5, 0, 3, 6, -1, 6, 3, 2, 7, -1, 0, 5, 4, 1, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 1f, 0f, -7.408958f),
					new float4(0f, -1f, 0f, -4.71479225f),
					new float4(-1f, 0f, 0f, -3.388889f),
					new float4(1f, 0f, 0f, -4.236111f),
					new float4(0f, 0f, 1f, -3.5075f),
					new float4(0f, 0f, -1f, -3.5075f),
				}, contents: 1, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(7.811389f, -24.107708f, 0.305f, 1f)), name: "Water 864 #801")
					.WithMesh(new float3[] { new float3(4.236111f, 7.408958f, -3.5075f), new float3(-3.388889f, 7.408958f, -3.5075f), new float3(-3.388889f, 7.408958f, 3.5075f), new float3(4.236111f, 7.408958f, 3.5075f), new float3(-3.388889f, -4.71479225f, -3.5075f), new float3(4.236111f, -4.71479225f, -3.5075f), new float3(4.236111f, -4.71479225f, 3.5075f), new float3(-3.388889f, -4.71479225f, 3.5075f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 1, 4, 7, 2, -1, 5, 0, 3, 6, -1, 6, 3, 2, 7, -1, 0, 5, 4, 1, -1 }))
				;
		}

		// bm_c2a3a, Model: the mesh draws nothing where the exact CSG has a face; Water 1001 #893 face 3 near (60.547,
		// -15.596, 19.825) (3 brushes)
		static ContentsScene BmC2a3aModel0Site14()
		{
			return new ContentsScene()
				.WithContentsList("Solid", "Water", "Glass", "Grate")
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 0f, 1f, -0.15249908f),
					new float4(0f, 0f, -1f, -0.152500913f),
					new float4(1f, 0f, 0f, -3.12624979f),
					new float4(-1f, 0f, 0f, -3.12625027f),
					new float4(0f, -1f, 0f, -0.948888659f),
					new float4(0f, 1f, 0f, -1.18611133f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(63.21125f, -15.3686113f, 19.9775f, 1f)), name: "Solid 464 #433")
					.WithMesh(new float3[] { new float3(3.12624979f, 1.18611133f, 0.15249908f), new float3(-3.12625027f, 1.18611133f, 0.15249908f), new float3(-3.12625027f, -0.948888659f, 0.15249908f), new float3(3.12624979f, -0.948888659f, 0.15249908f), new float3(-3.12625027f, 1.18611133f, -0.152500913f), new float3(3.12624979f, 1.18611133f, -0.152500913f), new float3(3.12624979f, -0.948888659f, -0.152500913f), new float3(-3.12625027f, -0.948888659f, -0.152500913f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 5, 0, 3, 6, -1, 1, 4, 7, 2, -1, 2, 7, 6, 3, -1, 4, 1, 0, 5, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 1f, 0f, -2.99493027f),
					new float4(0f, -1f, 0f, -4.70632f),
					new float4(-1f, 0f, 0f, -5.08333254f),
					new float4(1f, 0f, 0f, -4.06666756f),
					new float4(0f, 0f, 1f, -3.50749946f),
					new float4(0f, 0f, -1f, -3.50750041f),
				}, contents: 1, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(62.5758324f, -15.42368f, 23.3325f, 1f)), name: "Water 481 #450")
					.WithMesh(new float3[] { new float3(4.06666756f, 2.99493027f, -3.50750041f), new float3(-5.08333254f, 2.99493027f, -3.50750041f), new float3(-5.08333254f, 2.99493027f, 3.50749946f), new float3(4.06666756f, 2.99493027f, 3.50749946f), new float3(-5.08333254f, -4.70632f, -3.50750041f), new float3(4.06666756f, -4.70632f, -3.50750041f), new float3(4.06666756f, -4.70632f, 3.50749946f), new float3(-5.08333254f, -4.70632f, 3.50749946f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 1, 4, 7, 2, -1, 5, 0, 3, 6, -1, 6, 3, 2, 7, -1, 0, 5, 4, 1, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 1f, 0f, -3.232153f),
					new float4(0f, -1f, 0f, -5.079097f),
					new float4(1f, 0f, 0f, -5.42222166f),
					new float4(0f, 0f, 1f, -7.625f),
					new float4(0f, 0f, -1f, -7.625f),
					new float4(-1f, 0f, 0f, -4.33777857f),
				}, contents: 1, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(60.00028f, -15.660903f, 12.2f, 1f)), name: "Water 1001 #893")
					.WithMesh(new float3[] { new float3(-4.33777857f, 3.232153f, -7.625f), new float3(-4.33777857f, 3.232153f, 7.625f), new float3(5.42222166f, 3.232153f, 7.625f), new float3(5.42222166f, 3.232153f, -7.625f), new float3(-4.33777857f, -5.079097f, 7.625f), new float3(-4.33777857f, -5.079097f, -7.625f), new float3(5.42222166f, -5.079097f, -7.625f), new float3(5.42222166f, -5.079097f, 7.625f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 6, 3, 2, 7, -1, 1, 4, 7, 2, -1, 5, 0, 3, 6, -1, 0, 5, 4, 1, -1 }))
				;
		}

		// bm_c2a3a, Model: the mesh draws nothing where the exact CSG has a face; Water 1002 #894 face 4 near (55.662, -15.5,
		// 19.367) (3 brushes)
		static ContentsScene BmC2a3aModel0Site15()
		{
			return new ContentsScene()
				.WithContentsList("Solid", "Water", "Glass", "Grate")
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 0f, 1f, -0.7455561f),
					new float4(0f, -1f, 0f, -1.626667f),
					new float4(0f, 1f, 0f, -2.033333f),
					new float4(1f, 0f, 0f, -1.29625094f),
					new float4(-0.116841249f, 0f, -0.9931506f, -0.925560534f),
					new float4(-1f, 0f, 0f, -1.296249f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(54.36625f, -13.623333f, 19.9944439f, 1f)), name: "Solid 964 #866")
					.WithMesh(new float3[] { new float3(-1.296249f, 2.033333f, 0.7455561f), new float3(-1.296249f, -1.626667f, 0.7455561f), new float3(1.29625094f, -1.626667f, 0.7455561f), new float3(1.29625094f, 2.033333f, 0.7455561f), new float3(-1.296249f, -1.626667f, -0.7794439f), new float3(1.29625094f, -1.626667f, -1.08444393f), new float3(-1.296249f, 2.033333f, -0.7794439f), new float3(1.29625094f, 2.033333f, -1.08444393f) }, new int[] { 0, 1, 2, 3, -1, 1, 4, 5, 2, -1, 6, 0, 3, 7, -1, 5, 7, 3, 2, -1, 4, 6, 7, 5, -1, 6, 4, 1, 0, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 1f, 0f, -3.232153f),
					new float4(0f, -1f, 0f, -5.079097f),
					new float4(1f, 0f, 0f, -5.42222166f),
					new float4(0f, 0f, 1f, -7.625f),
					new float4(0f, 0f, -1f, -7.625f),
					new float4(-1f, 0f, 0f, -4.33777857f),
				}, contents: 1, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(60.00028f, -15.660903f, 12.2f, 1f)), name: "Water 1001 #893")
					.WithMesh(new float3[] { new float3(-4.33777857f, 3.232153f, -7.625f), new float3(-4.33777857f, 3.232153f, 7.625f), new float3(5.42222166f, 3.232153f, 7.625f), new float3(5.42222166f, 3.232153f, -7.625f), new float3(-4.33777857f, -5.079097f, 7.625f), new float3(-4.33777857f, -5.079097f, -7.625f), new float3(5.42222166f, -5.079097f, -7.625f), new float3(5.42222166f, -5.079097f, 7.625f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 6, 3, 2, 7, -1, 1, 4, 7, 2, -1, 5, 0, 3, 6, -1, 0, 5, 4, 1, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 1f, 0f, -5.079097f),
					new float4(0f, -1f, 0f, -3.23215318f),
					new float4(-1f, 0f, 0f, -5.1511097f),
					new float4(0f, 0f, 1f, -7.591111f),
					new float4(1f, 0f, 0f, -6.43889f),
					new float4(0f, 0f, -1f, -9.488889f),
				}, contents: 1, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(49.22361f, -17.5078468f, 12.2338886f, 1f)), name: "Water 1002 #894")
					.WithMesh(new float3[] { new float3(6.43889f, 5.079097f, -9.488889f), new float3(-5.1511097f, 5.079097f, -9.488889f), new float3(-5.1511097f, 5.079097f, 7.591111f), new float3(6.43889f, 5.079097f, 7.591111f), new float3(-5.1511097f, -3.23215318f, -9.488889f), new float3(6.43889f, -3.23215318f, -9.488889f), new float3(6.43889f, -3.23215318f, 7.591111f), new float3(-5.1511097f, -3.23215318f, 7.591111f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 1, 4, 7, 2, -1, 6, 3, 2, 7, -1, 5, 0, 3, 6, -1, 0, 5, 4, 1, -1 }))
				;
		}

		// bm_c2a3b, Model: the mesh draws nothing where the exact CSG has a face; Solid 19 #12 face 1 near (6.71, -5,
		// -19.825) (3 brushes)
		static ContentsScene BmC2a3bModel0Site1()
		{
			return new ContentsScene()
				.WithContentsList("Solid", "Water", "Glass", "Grate")
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, -1f, 0f, -1.08444452f),
					new float4(-1f, 0f, 0f, -0.08472208f),
					new float4(1f, 0f, 0f, -0.06777792f),
					new float4(0f, 0f, -1f, -1.98249948f),
					new float4(0f, 1f, 0f, -1.35555553f),
					new float4(0f, 0f, 1f, -1.98250055f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(6.794722f, -3.94805551f, -21.5025f, 1f)), name: "Solid 19 #12")
					.WithMesh(new float3[] { new float3(0.06777792f, -1.08444452f, 1.98250055f), new float3(-0.08472208f, -1.08444452f, 1.98250055f), new float3(-0.08472208f, -1.08444452f, -1.98249948f), new float3(0.06777792f, -1.08444452f, -1.98249948f), new float3(-0.08472208f, 1.35555553f, 1.98250055f), new float3(-0.08472208f, 1.35555553f, -1.98249948f), new float3(0.06777792f, 1.35555553f, 1.98250055f), new float3(0.06777792f, 1.35555553f, -1.98249948f) }, new int[] { 0, 1, 2, 3, -1, 1, 4, 5, 2, -1, 6, 0, 3, 7, -1, 5, 7, 3, 2, -1, 4, 6, 7, 5, -1, 6, 4, 1, 0, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 1f, 0f, -2.93562531f),
					new float4(0f, -1f, 0f, -1.86812472f),
					new float4(-1f, 0f, 0f, -5.13416672f),
					new float4(1f, 0f, 0f, -6.4177084f),
					new float4(0f, 0f, 1f, -5.547188f),
					new float4(0f, 0f, -1f, -5.54718733f),
				}, contents: 1, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(0.292291671f, -4.994375f, -17.8615627f, 1f)), name: "Water 251 #220")
					.WithMesh(new float3[] { new float3(6.4177084f, 2.93562531f, -5.54718733f), new float3(-5.13416672f, 2.93562531f, -5.54718733f), new float3(-5.13416672f, 2.93562531f, 5.547188f), new float3(6.4177084f, 2.93562531f, 5.547188f), new float3(-5.13416672f, -1.86812472f, -5.54718733f), new float3(6.4177084f, -1.86812472f, -5.54718733f), new float3(6.4177084f, -1.86812472f, 5.547188f), new float3(-5.13416672f, -1.86812472f, 5.547188f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 1, 4, 7, 2, -1, 5, 0, 3, 6, -1, 6, 3, 2, 7, -1, 0, 5, 4, 1, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 1f, 0f, -2.93562531f),
					new float4(0f, -1f, 0f, -1.86812472f),
					new float4(-1f, 0f, 0f, -2.50777817f),
					new float4(0f, 0f, 1f, -1.82999921f),
					new float4(0f, 0f, -1f, -1.83000076f),
					new float4(0.707106769f, 0f, 0.707106769f, -2.21658254f),
				}, contents: 1, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(9.217778f, -4.994375f, -18.3f, 1f)), name: "Water 253 #222")
					.WithMesh(new float3[] { new float3(1.304722f, 2.93562531f, 1.82999921f), new float3(4.96472168f, 2.93562531f, -1.83000076f), new float3(-2.50777817f, 2.93562531f, -1.83000076f), new float3(-2.50777817f, 2.93562531f, 1.82999921f), new float3(4.96472168f, -1.86812472f, -1.83000076f), new float3(1.304722f, -1.86812472f, 1.82999921f), new float3(-2.50777817f, -1.86812472f, 1.82999921f), new float3(-2.50777817f, -1.86812472f, -1.83000076f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 2, 7, 6, 3, -1, 5, 0, 3, 6, -1, 1, 4, 7, 2, -1, 4, 1, 0, 5, -1 }))
				;
		}

		// bm_c2a3b, Model: the mesh draws nothing where the exact CSG has a face; Water 256 #225 face 2 near (19.52, -4.5,
		// -9.15) (3 brushes)
		static ContentsScene BmC2a3bModel0Site2()
		{
			return new ContentsScene()
				.WithContentsList("Solid", "Water", "Glass", "Grate")
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 1f, 0f, -0.372777581f),
					new float4(0f, -1f, 0f, -0.2372224f),
					new float4(1f, 0f, 0f, -2.64333272f),
					new float4(0f, 0f, 1f, -0.9149996f),
					new float4(0f, 0f, -1f, -0.9150004f),
					new float4(-1f, 0f, 0f, -3.30416727f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(22.8241673f, -4.7952776f, -9.15f, 1f)), name: "Solid 148 #130")
					.WithMesh(new float3[] { new float3(-3.30416727f, 0.372777581f, -0.9150004f), new float3(-3.30416727f, 0.372777581f, 0.9149996f), new float3(2.64333272f, 0.372777581f, 0.9149996f), new float3(2.64333272f, 0.372777581f, -0.9150004f), new float3(-3.30416727f, -0.2372224f, 0.9149996f), new float3(-3.30416727f, -0.2372224f, -0.9150004f), new float3(2.64333272f, -0.2372224f, -0.9150004f), new float3(2.64333272f, -0.2372224f, 0.9149996f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 6, 3, 2, 7, -1, 1, 4, 7, 2, -1, 5, 0, 3, 6, -1, 0, 5, 4, 1, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 1f, 0f, -1.81729162f),
					new float4(0f, -1f, 0f, -1.15645838f),
					new float4(1f, 0f, 0f, -3.185556f),
					new float4(0f, 0f, 1f, -1.82999957f),
					new float4(0f, 0f, -1f, -1.8300004f),
					new float4(-0.707106769f, 0f, -0.707106769f, -2.81566f),
				}, contents: 1, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(16.334444f, -3.87604165f, -9.15f, 1f)), name: "Water 255 #224")
					.WithMesh(new float3[] { new float3(-2.15194416f, 1.81729162f, -1.8300004f), new float3(-5.811944f, 1.81729162f, 1.82999957f), new float3(3.185556f, 1.81729162f, 1.82999957f), new float3(3.185556f, 1.81729162f, -1.8300004f), new float3(-5.811944f, -1.15645838f, 1.82999957f), new float3(-2.15194416f, -1.15645838f, -1.8300004f), new float3(3.185556f, -1.15645838f, -1.8300004f), new float3(3.185556f, -1.15645838f, 1.82999957f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 6, 3, 2, 7, -1, 1, 4, 7, 2, -1, 5, 0, 3, 6, -1, 0, 5, 4, 1, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 1f, 0f, -1.81729162f),
					new float4(0f, -1f, 0f, -1.15645838f),
					new float4(-1f, 0f, 0f, -2.643334f),
					new float4(1f, 0f, 0f, -3.304166f),
					new float4(0f, 0f, 1f, -3.20249987f),
					new float4(0f, 0f, -1f, -3.2025f),
				}, contents: 1, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(22.1633339f, -3.87604165f, -9.455f, 1f)), name: "Water 256 #225")
					.WithMesh(new float3[] { new float3(3.304166f, 1.81729162f, -3.2025f), new float3(-2.643334f, 1.81729162f, -3.2025f), new float3(-2.643334f, 1.81729162f, 3.20249987f), new float3(3.304166f, 1.81729162f, 3.20249987f), new float3(-2.643334f, -1.15645838f, -3.2025f), new float3(3.304166f, -1.15645838f, -3.2025f), new float3(3.304166f, -1.15645838f, 3.20249987f), new float3(-2.643334f, -1.15645838f, 3.20249987f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 1, 4, 7, 2, -1, 5, 0, 3, 6, -1, 6, 3, 2, 7, -1, 0, 5, 4, 1, -1 }))
				;
		}

		// bm_c2a4a, Details: the exact CSG made a triangle the mesh does not have; Solid 440 #6144 face -1 near (1.385,
		// 0.137, 0.244) (3 brushes)
		static ContentsScene BmC2a4aDetails167Site1()
		{
			return new ContentsScene()
				.WithContentsList("Solid", "Water", "Glass", "Grate")
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(-1f, 0f, 0f, -0.703194439f),
					new float4(1f, 0f, 0f, -0.8789931f),
					new float4(0f, 1f, 0f, -0.111197926f),
					new float4(0f, -0.707106769f, 0.707106769f, -0.104089551f),
					new float4(0f, -0.5368755f, -0.8436615f, -0.0117774354f),
					new float4(0f, 0.573462367f, 0.8192319f, -0.009977283f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, -5.113243E-09f, -8.940696E-08f, 0f), new float4(5.11325E-09f, 1.00000012f, 7.045154E-08f, 0f), new float4(8.940697E-08f, -7.045154E-08f, 1.00000012f, 0f), new float4(0.3410071f, 0.270050555f, 0.122846976f, 1f)), name: "Solid 278 #6101")
					.WithMesh(new float3[] { new float3(-0.703194439f, -0.07942708f, 0.06777778f), new float3(-0.703194439f, 0.111197926f, -0.0656597242f), new float3(-0.703194439f, 0.111197926f, -0.08472222f), new float3(-0.703194439f, -0.0984895751f, 0.04871528f), new float3(0.8789931f, 0.111197926f, -0.0656597242f), new float3(0.8789931f, -0.07942708f, 0.06777778f), new float3(0.8789931f, -0.0984895751f, 0.04871528f), new float3(0.8789931f, 0.111197926f, -0.08472222f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 1, 4, 7, 2, -1, 5, 0, 3, 6, -1, 6, 3, 2, 7, -1, 0, 5, 4, 1, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0.9239892f, 0.0002312337f, -0.382418543f, -0.126011491f),
					new float4(-1f, 0f, 0f, -0.151186138f),
					new float4(0f, 1f, 0f, -0.100607641f),
					new float4(0.1392624f, -0.7002164f, 0.7002164f, -0.116405345f),
					new float4(-0.165506989f, -0.529307365f, -0.8321305f, -0.0101717962f),
					new float4(0.1605456f, 0.565828145f, 0.808742f, -0.0107016377f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, -5.113243E-09f, -8.940696E-08f, 0f), new float4(5.11325E-09f, 1.00000012f, 7.045154E-08f, 0f), new float4(8.940697E-08f, -7.045154E-08f, 1.00000012f, 0f), new float4(1.37118518f, 0.280639976f, 0.0843296f, 1f)), name: "Solid 439 #6143")
					.WithMesh(new float3[] { new float3(0.104140438f, 0.100607641f, -0.0778298751f), new float3(0.155185f, -0.08999069f, 0.0453873277f), new float3(0.147888944f, -0.109081812f, 0.027747279f), new float3(0.09683578f, 0.100607641f, -0.0954792053f), new float3(-0.151186138f, -0.09005775f, 0.106252827f), new float3(-0.151186138f, 0.100607641f, -0.02714429f), new float3(-0.151186138f, 0.100607641f, -0.04614877f), new float3(-0.151186138f, -0.10907986f, 0.08723072f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 5, 0, 3, 6, -1, 1, 4, 7, 2, -1, 2, 7, 6, 3, -1, 4, 1, 0, 5, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0.923879266f, 0.000114311755f, -0.382684052f, -0.151556119f),
					new float4(-1f, 0f, 0f, -0.185200959f),
					new float4(-0.139216542f, 0.7006416f, -0.6998001f, -0.104541495f),
					new float4(0.195102721f, 0f, 0.9807828f, -0.109059833f),
					new float4(-0.10621345f, -0.838896155f, -0.533827662f, -0.009940416f),
					new float4(0.1133254f, 0.8138257f, 0.5699518f, -0.0116223721f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, -5.113243E-09f, -8.940696E-08f, 0f), new float4(5.11325E-09f, 1.00000012f, 7.045154E-08f, 0f), new float4(8.940697E-08f, -7.045154E-08f, 1.00000012f, 0f), new float4(1.40520108f, 0.120726928f, 0.233211368f, 1f)), name: "Solid 440 #6144")
					.WithMesh(new float3[] { new float3(0.121160313f, 0.06989955f, -0.103507392f), new float3(0.194115654f, -0.06358142f, 0.07258217f), new float3(0.194117829f, -0.08261389f, 0.07258173f), new float3(0.113855325f, 0.0508277826f, -0.121148862f), new float3(-0.185200959f, -0.06360591f, 0.14803791f), new float3(-0.185200959f, 0.0698864f, -0.0425737873f), new float3(-0.185200959f, 0.05083106f, -0.061652042f), new float3(-0.185200959f, -0.08260417f, 0.14803791f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 5, 0, 3, 6, -1, 1, 4, 7, 2, -1, 2, 7, 6, 3, -1, 4, 1, 0, 5, -1 }))
				;
		}

		// bm_c2a4a, Details: the mesh draws nothing where the exact CSG has a face; Grate 1284289 #3688 face 5 near (-5.49,
		// 0.953, 14.678) (13 brushes)
		static ContentsScene BmC2a4aDetails28Site1()
		{
			return new ContentsScene()
				.WithContentsList("Solid", "Water", "Glass", "Grate")
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 1f, 0f, -1.525f),
					new float4(0f, -1f, 0f, -0.915f),
					new float4(0f, 0f, -1f, -0.06989609f),
					new float4(-0.707106769f, 0f, 0.707106769f, -0.186462492f),
					new float4(-1f, 0f, 0f, -0.257343858f),
					new float4(0.707106769f, 0f, 0.707106769f, -0.253858447f),
					new float4(0f, 0f, 1f, -0.08260391f),
					new float4(1f, 0f, 0f, -0.352656126f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(-5.842656f, 0.915f, 14.7098961f, 1f)), name: "Solid 160143 #2059")
					.WithMesh(new float3[] { new float3(0.352656126f, 1.525f, 0.006353939f), new float3(0.352656126f, 1.525f, -0.06989609f), new float3(-0.257343858f, 1.525f, -0.06989609f), new float3(-0.257343858f, 1.525f, 0.00635393057f), new float3(-0.181093886f, 1.525f, 0.08260391f), new float3(0.276406169f, 1.525f, 0.08260391f), new float3(0.352656126f, -0.915f, -0.06989609f), new float3(0.352656126f, -0.915f, 0.006353939f), new float3(0.276406169f, -0.915f, 0.08260391f), new float3(-0.181093886f, -0.915f, 0.08260391f), new float3(-0.257343858f, -0.915f, 0.00635393057f), new float3(-0.257343858f, -0.915f, -0.06989609f) }, new int[] { 0, 1, 2, 3, 4, 5, -1, 6, 7, 8, 9, 10, 11, -1, 1, 6, 11, 2, -1, 9, 4, 3, 10, -1, 10, 3, 2, 11, -1, 7, 0, 5, 8, -1, 8, 5, 4, 9, -1, 0, 7, 6, 1, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 0f, -1f, -0.368541449f),
					new float4(0f, -1f, 0f, -0.3049999f),
					new float4(0f, -0.707106769f, -0.707106769f, -0.04493051f),
					new float4(-0.577350259f, 0.577350259f, 0.577350259f, -0.15408057f),
					new float4(-1f, 0f, 0f, -0.2541667f),
					new float4(0.577350259f, 0.577350259f, 0.577350259f, -0.212777808f),
					new float4(0f, 0.707106769f, 0.707106769f, -0.06290328f),
					new float4(1f, 0f, 0f, -0.3558333f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(-5.8458333f, 2.745f, 14.3985415f, 1f)), name: "Solid 160144 #2060")
					.WithMesh(new float3[] { new float3(0.3558333f, 0.381250143f, -0.368541449f), new float3(0.3558333f, 0.3050001f, -0.368541449f), new float3(-0.2541667f, 0.3050001f, -0.368541449f), new float3(-0.2541667f, 0.381250143f, -0.368541449f), new float3(-0.1779167f, 0.45750013f, -0.368541449f), new float3(0.2795833f, 0.45750013f, -0.368541449f), new float3(0.3558333f, -0.3049999f, 0.24145855f), new float3(0.3558333f, -0.3049999f, 0.317708582f), new float3(0.2795833f, -0.3049999f, 0.393958569f), new float3(-0.1779167f, -0.3049999f, 0.393958569f), new float3(-0.2541667f, -0.3049999f, 0.317708582f), new float3(-0.2541667f, -0.3049999f, 0.24145855f) }, new int[] { 0, 1, 2, 3, 4, 5, -1, 6, 7, 8, 9, 10, 11, -1, 1, 6, 11, 2, -1, 9, 4, 3, 10, -1, 10, 3, 2, 11, -1, 7, 0, 5, 8, -1, 8, 5, 4, 9, -1, 0, 7, 6, 1, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 0.707106769f, -0.707106769f, -0.4695264f),
					new float4(0f, 0f, 1f, -1.10244775f),
					new float4(0f, -1f, 0f, -0.0635416061f),
					new float4(-0.707106769f, 0.707106769f, 0f, -0.2246537f),
					new float4(-1f, 0f, 0f, -0.304999918f),
					new float4(0.707106769f, 0.707106769f, 0f, -0.22465381f),
					new float4(0f, 1f, 0f, -0.0889584f),
					new float4(1f, 0f, 0f, -0.305000067f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(-5.795f, 3.1135416f, 12.9275522f, 1f)), name: "Solid 160145 #2061")
					.WithMesh(new float3[] { new float3(0.305000067f, 0.0127084041f, -0.6513022f), new float3(0.305000067f, -0.0635416061f, -0.727552235f), new float3(-0.304999918f, -0.0635416061f, -0.727552235f), new float3(-0.304999918f, 0.012708405f, -0.6513022f), new float3(-0.228749931f, 0.0889584f, -0.5750522f), new float3(0.22875008f, 0.0889584f, -0.5750522f), new float3(0.305000067f, -0.0635416061f, 1.10244775f), new float3(0.305000067f, 0.0127084041f, 1.10244775f), new float3(0.22875008f, 0.0889584f, 1.10244775f), new float3(-0.228749931f, 0.0889584f, 1.10244775f), new float3(-0.304999918f, 0.012708405f, 1.10244775f), new float3(-0.304999918f, -0.0635416061f, 1.10244775f) }, new int[] { 0, 1, 2, 3, 4, 5, -1, 6, 7, 8, 9, 10, 11, -1, 1, 6, 11, 2, -1, 9, 4, 3, 10, -1, 10, 3, 2, 11, -1, 7, 0, 5, 8, -1, 8, 5, 4, 9, -1, 0, 7, 6, 1, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 1f, 0f, -1.61395848f),
					new float4(0f, -0.707106769f, 0.707106769f, -0.9660111f),
					new float4(0f, 0f, -1f, -0.06989555f),
					new float4(-0.707106769f, 0f, 0.707106769f, -0.2179142f),
					new float4(-1f, 0f, 0f, -0.301822752f),
					new float4(0.707106769f, 0f, 0.707106769f, -0.22240749f),
					new float4(0f, 0f, 1f, -0.0826044455f),
					new float4(1f, 0f, 0f, -0.308177233f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(-5.79817724f, 4.48604155f, 12.2698956f, 1f)), name: "Solid 160146 #2062")
					.WithMesh(new float3[] { new float3(0.308177233f, 1.61395848f, 0.00635446142f), new float3(0.308177233f, 1.61395848f, -0.06989555f), new float3(-0.301822752f, 1.61395848f, -0.06989555f), new float3(-0.301822752f, 1.61395848f, 0.006354462f), new float3(-0.225572765f, 1.61395848f, 0.0826044455f), new float3(0.231927246f, 1.61395848f, 0.0826044455f), new float3(0.308177233f, -1.43604159f, -0.06989555f), new float3(0.308177233f, -1.35979152f, 0.00635446142f), new float3(0.231927246f, -1.28354156f, 0.0826044455f), new float3(-0.225572765f, -1.28354156f, 0.0826044455f), new float3(-0.301822752f, -1.35979152f, 0.006354462f), new float3(-0.301822752f, -1.43604159f, -0.06989555f) }, new int[] { 0, 1, 2, 3, 4, 5, -1, 6, 7, 8, 9, 10, 11, -1, 1, 6, 11, 2, -1, 9, 4, 3, 10, -1, 10, 3, 2, 11, -1, 7, 0, 5, 8, -1, 8, 5, 4, 9, -1, 0, 7, 6, 1, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 1f, 0f, -0.1863889f),
					new float4(0f, -1f, 0f, -0.118611112f),
					new float4(-1f, 0f, 0f, -0.0169446375f),
					new float4(1f, 0f, 0f, -0.0211803634f),
					new float4(0f, 0f, 1f, -1.14375019f),
					new float4(0f, 0f, -1f, -1.14374983f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(-5.47305536f, 0.7286111f, 20.81625f, 1f)), name: "Solid 2764684 #2261")
					.WithMesh(new float3[] { new float3(0.0211803634f, 0.1863889f, -1.14374983f), new float3(-0.0169446375f, 0.1863889f, -1.14374983f), new float3(-0.0169446375f, 0.1863889f, 1.14375019f), new float3(0.0211803634f, 0.1863889f, 1.14375019f), new float3(-0.0169446375f, -0.118611112f, -1.14374983f), new float3(0.0211803634f, -0.118611112f, -1.14374983f), new float3(0.0211803634f, -0.118611112f, 1.14375019f), new float3(-0.0169446375f, -0.118611112f, 1.14375019f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 1, 4, 7, 2, -1, 5, 0, 3, 6, -1, 6, 3, 2, 7, -1, 0, 5, 4, 1, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 1f, 0f, -0.09319458f),
					new float4(0f, -1f, 0f, -0.05930542f),
					new float4(-1f, 0f, 0f, -0.2711111f),
					new float4(1f, 0f, 0f, -0.338888884f),
					new float4(0f, 0f, 1f, -6.966282f),
					new float4(0f, -0.6919207f, -0.721973538f, -5.03533459f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(-5.828889f, 6.00680542f, 19.8737183f, 1f)), name: "Solid 427502 #2529")
					.WithMesh(new float3[] { new float3(0.338888884f, 0.09319458f, -7.06371832f), new float3(-0.2711111f, 0.09319458f, -7.06371832f), new float3(-0.2711111f, 0.09319458f, 6.966282f), new float3(0.338888884f, 0.09319458f, 6.966282f), new float3(-0.2711111f, -0.05930542f, -6.9175663f), new float3(0.338888884f, -0.05930542f, -6.9175663f), new float3(0.338888884f, -0.05930542f, 6.966282f), new float3(-0.2711111f, -0.05930542f, 6.966282f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 1, 4, 7, 2, -1, 5, 0, 3, 6, -1, 6, 3, 2, 7, -1, 0, 5, 4, 1, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 1f, 0f, -0.0465972051f),
					new float4(0f, -1f, 0f, -0.0296527967f),
					new float4(0f, 0f, 1f, -0.61f),
					new float4(0f, 0f, -1f, -0.61f),
					new float4(1f, 0f, 0f, -3.9819448f),
					new float4(-1f, 0f, 0f, -3.18555522f),
				}, contents: 3, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(-9.471945f, 0.9446528f, 15.25f, 1f)), name: "Grate 1284499 #3687")
					.WithMesh(new float3[] { new float3(-3.18555522f, 0.0465972051f, -0.61f), new float3(-3.18555522f, 0.0465972051f, 0.61f), new float3(3.9819448f, 0.0465972051f, 0.61f), new float3(3.9819448f, 0.0465972051f, -0.61f), new float3(-3.18555522f, -0.0296527967f, 0.61f), new float3(-3.18555522f, -0.0296527967f, -0.61f), new float3(3.9819448f, -0.0296527967f, -0.61f), new float3(3.9819448f, -0.0296527967f, 0.61f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 1, 4, 7, 2, -1, 5, 0, 3, 6, -1, 6, 3, 2, 7, -1, 0, 5, 4, 1, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 1f, 0f, -0.0465972051f),
					new float4(0f, -1f, 0f, -0.0296527967f),
					new float4(1f, 0f, 0f, -0.542222f),
					new float4(0f, 0f, 1f, -3.6600008f),
					new float4(0f, 0f, -1f, -3.65999913f),
					new float4(-1f, 0f, 0f, -0.677778f),
				}, contents: 3, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(-4.812222f, 0.9446528f, 18.3f, 1f)), name: "Grate 1284289 #3688")
					.WithMesh(new float3[] { new float3(-0.677778f, 0.0465972051f, -3.65999913f), new float3(-0.677778f, 0.0465972051f, 3.6600008f), new float3(0.542222f, 0.0465972051f, 3.6600008f), new float3(0.542222f, 0.0465972051f, -3.65999913f), new float3(-0.677778f, -0.0296527967f, 3.6600008f), new float3(-0.677778f, -0.0296527967f, -3.65999913f), new float3(0.542222f, -0.0296527967f, -3.65999913f), new float3(0.542222f, -0.0296527967f, 3.6600008f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 6, 3, 2, 7, -1, 1, 4, 7, 2, -1, 5, 0, 3, 6, -1, 0, 5, 4, 1, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 1f, 0f, -0.05718752f),
					new float4(0f, -1f, 0f, -0.0571874827f),
					new float4(-0.707106769f, 0f, -0.707106769f, -0.4463117f),
					new float4(0.707106769f, 0f, -0.707106769f, -0.446311861f),
					new float4(0f, 0f, 1f, -0.0169448089f),
					new float4(0f, 0f, -1f, -0.02118019f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(-4.88f, 0.9340625f, 21.98118f, 1f)), name: "Solid 2406708 #4867")
					.WithMesh(new float3[] { new float3(0.610000134f, 0.05718752f, -0.02118019f), new float3(-0.6099999f, 0.05718752f, -0.02118019f), new float3(-0.6481249f, 0.05718752f, 0.0169448089f), new float3(0.6481251f, 0.05718752f, 0.0169448089f), new float3(-0.6099999f, -0.0571874827f, -0.02118019f), new float3(0.610000134f, -0.0571874827f, -0.02118019f), new float3(0.6481251f, -0.0571874827f, 0.0169448089f), new float3(-0.6481249f, -0.0571874827f, 0.0169448089f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 1, 4, 7, 2, -1, 5, 0, 3, 6, -1, 6, 3, 2, 7, -1, 0, 5, 4, 1, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 1f, 0f, -0.0444791541f),
					new float4(0f, -1f, 0f, -0.06989585f),
					new float4(-1f, 0f, 0f, -0.0211803429f),
					new float4(1f, 0f, 0f, -0.0169446561f),
					new float4(-0.707106769f, 0f, -0.707106769f, -0.8506885f),
					new float4(-0.707106769f, 0f, 0.707106769f, -0.850688636f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(-5.50694466f, 0.946770847f, 17.08f, 1f)), name: "Solid 2763278 #4869")
					.WithMesh(new float3[] { new float3(-0.0211803429f, 0.0444791541f, 1.18187511f), new float3(0.0169446561f, 0.0444791541f, 1.22f), new float3(0.0169446561f, 0.0444791541f, -1.21999991f), new float3(-0.0211803429f, 0.0444791541f, -1.18187487f), new float3(0.0169446561f, -0.06989585f, 1.22f), new float3(-0.0211803429f, -0.06989585f, 1.18187511f), new float3(-0.0211803429f, -0.06989585f, -1.18187487f), new float3(0.0169446561f, -0.06989585f, -1.21999991f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 5, 0, 3, 6, -1, 1, 4, 7, 2, -1, 2, 7, 6, 3, -1, 4, 1, 0, 5, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 1f, 0f, -0.118611135f),
					new float4(0f, -1f, 0f, -0.186388865f),
					new float4(1f, 0f, 0f, -0.7879164f),
					new float4(0f, 0f, 1f, -0.0190625768f),
					new float4(0f, 0f, -1f, -0.0190624241f),
					new float4(-0.707106769f, 0f, -0.707106769f, -0.69642663f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(-6.27791643f, 0.796388865f, 19.4628124f, 1f)), name: "Solid 2763254 #4872")
					.WithMesh(new float3[] { new float3(-0.9658336f, 0.118611135f, -0.0190624241f), new float3(-1.00395858f, 0.118611135f, 0.0190625768f), new float3(0.7879164f, 0.118611135f, 0.0190625768f), new float3(0.7879164f, 0.118611135f, -0.0190624241f), new float3(-1.00395858f, -0.186388865f, 0.0190625768f), new float3(-0.9658336f, -0.186388865f, -0.0190624241f), new float3(0.7879164f, -0.186388865f, -0.0190624241f), new float3(0.7879164f, -0.186388865f, 0.0190625768f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 6, 3, 2, 7, -1, 1, 4, 7, 2, -1, 5, 0, 3, 6, -1, 0, 5, 4, 1, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 1f, 0f, -0.152500018f),
					new float4(0f, -1f, 0f, -0.152499989f),
					new float4(-0.707106769f, 0f, 0.707106769f, -0.626034558f),
					new float4(1f, 0f, 0f, -0.8853471f),
					new float4(0f, 0f, 1f, -0.0169436652f),
					new float4(0f, 0f, -1f, -0.0211813357f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(-6.375347f, 0.7625f, 18.3593063f, 1f)), name: "Solid 2763262 #4874")
					.WithMesh(new float3[] { new float3(0.8853471f, 0.152500018f, -0.0211813357f), new float3(-0.906527936f, 0.152500018f, -0.0211813357f), new float3(-0.8684029f, 0.152500018f, 0.0169436652f), new float3(0.8853471f, 0.152500018f, 0.0169436652f), new float3(-0.906527936f, -0.152499989f, -0.0211813357f), new float3(0.8853471f, -0.152499989f, -0.0211813357f), new float3(0.8853471f, -0.152499989f, 0.0169436652f), new float3(-0.8684029f, -0.152499989f, 0.0169436652f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 1, 4, 7, 2, -1, 5, 0, 3, 6, -1, 6, 3, 2, 7, -1, 0, 5, 4, 1, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 1f, 0f, -0.038125f),
					new float4(0f, -1f, 0f, -0.038125f),
					new float4(-1f, 0f, 0f, -0.9149998f),
					new float4(1f, 0f, 0f, -0.9150002f),
					new float4(0f, 0f, 1f, -0.5422225f),
					new float4(0f, 0f, -1f, -0.677777469f),
				}, contents: 3, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(-6.405f, 0.953125f, 18.9777775f, 1f)), name: "Grate 2763237 #4877")
					.WithMesh(new float3[] { new float3(0.9150002f, 0.038125f, -0.677777469f), new float3(-0.9149998f, 0.038125f, -0.677777469f), new float3(-0.9149998f, 0.038125f, 0.5422225f), new float3(0.9150002f, 0.038125f, 0.5422225f), new float3(-0.9149998f, -0.038125f, -0.677777469f), new float3(0.9150002f, -0.038125f, -0.677777469f), new float3(0.9150002f, -0.038125f, 0.5422225f), new float3(-0.9149998f, -0.038125f, 0.5422225f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 1, 4, 7, 2, -1, 5, 0, 3, 6, -1, 6, 3, 2, 7, -1, 0, 5, 4, 1, -1 }))
				;
		}

		// bm_c2a4a, Details: the mesh draws nothing where the exact CSG has a face; Solid 160143 #2059 face 7 near (-5.49,
		// 0.97, 14.678) (3 brushes)
		static ContentsScene BmC2a4aDetails28Site2()
		{
			return new ContentsScene()
				.WithContentsList("Solid", "Water", "Glass", "Grate")
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 1f, 0f, -1.525f),
					new float4(0f, -1f, 0f, -0.915f),
					new float4(0f, 0f, -1f, -0.06989609f),
					new float4(-0.707106769f, 0f, 0.707106769f, -0.186462492f),
					new float4(-1f, 0f, 0f, -0.257343858f),
					new float4(0.707106769f, 0f, 0.707106769f, -0.253858447f),
					new float4(0f, 0f, 1f, -0.08260391f),
					new float4(1f, 0f, 0f, -0.352656126f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(-5.842656f, 0.915f, 14.7098961f, 1f)), name: "Solid 160143 #2059")
					.WithMesh(new float3[] { new float3(0.352656126f, 1.525f, 0.006353939f), new float3(0.352656126f, 1.525f, -0.06989609f), new float3(-0.257343858f, 1.525f, -0.06989609f), new float3(-0.257343858f, 1.525f, 0.00635393057f), new float3(-0.181093886f, 1.525f, 0.08260391f), new float3(0.276406169f, 1.525f, 0.08260391f), new float3(0.352656126f, -0.915f, -0.06989609f), new float3(0.352656126f, -0.915f, 0.006353939f), new float3(0.276406169f, -0.915f, 0.08260391f), new float3(-0.181093886f, -0.915f, 0.08260391f), new float3(-0.257343858f, -0.915f, 0.00635393057f), new float3(-0.257343858f, -0.915f, -0.06989609f) }, new int[] { 0, 1, 2, 3, 4, 5, -1, 6, 7, 8, 9, 10, 11, -1, 1, 6, 11, 2, -1, 9, 4, 3, 10, -1, 10, 3, 2, 11, -1, 7, 0, 5, 8, -1, 8, 5, 4, 9, -1, 0, 7, 6, 1, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 1f, 0f, -0.0465972051f),
					new float4(0f, -1f, 0f, -0.0296527967f),
					new float4(0f, 0f, 1f, -0.61f),
					new float4(0f, 0f, -1f, -0.61f),
					new float4(1f, 0f, 0f, -3.9819448f),
					new float4(-1f, 0f, 0f, -3.18555522f),
				}, contents: 3, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(-9.471945f, 0.9446528f, 15.25f, 1f)), name: "Grate 1284499 #3687")
					.WithMesh(new float3[] { new float3(-3.18555522f, 0.0465972051f, -0.61f), new float3(-3.18555522f, 0.0465972051f, 0.61f), new float3(3.9819448f, 0.0465972051f, 0.61f), new float3(3.9819448f, 0.0465972051f, -0.61f), new float3(-3.18555522f, -0.0296527967f, 0.61f), new float3(-3.18555522f, -0.0296527967f, -0.61f), new float3(3.9819448f, -0.0296527967f, -0.61f), new float3(3.9819448f, -0.0296527967f, 0.61f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 1, 4, 7, 2, -1, 5, 0, 3, 6, -1, 6, 3, 2, 7, -1, 0, 5, 4, 1, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 1f, 0f, -0.0465972051f),
					new float4(0f, -1f, 0f, -0.0296527967f),
					new float4(1f, 0f, 0f, -0.542222f),
					new float4(0f, 0f, 1f, -3.6600008f),
					new float4(0f, 0f, -1f, -3.65999913f),
					new float4(-1f, 0f, 0f, -0.677778f),
				}, contents: 3, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(-4.812222f, 0.9446528f, 18.3f, 1f)), name: "Grate 1284289 #3688")
					.WithMesh(new float3[] { new float3(-0.677778f, 0.0465972051f, -3.65999913f), new float3(-0.677778f, 0.0465972051f, 3.6600008f), new float3(0.542222f, 0.0465972051f, 3.6600008f), new float3(0.542222f, 0.0465972051f, -3.65999913f), new float3(-0.677778f, -0.0296527967f, 3.6600008f), new float3(-0.677778f, -0.0296527967f, -3.65999913f), new float3(0.542222f, -0.0296527967f, -3.65999913f), new float3(0.542222f, -0.0296527967f, 3.6600008f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 6, 3, 2, 7, -1, 1, 4, 7, 2, -1, 5, 0, 3, 6, -1, 0, 5, 4, 1, -1 }))
				;
		}

		// bm_c2a4b, Model: the mesh draws nothing where the exact CSG has a face; Water 345442 #374 face 3 near (-5.49, -3.5,
		// 0) (4 brushes)
		static ContentsScene BmC2a4bModel0Site1()
		{
			return new ContentsScene()
				.WithContentsList("Solid", "Water", "Glass", "Grate")
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 1f, 0f, -1.30472219f),
					new float4(0f, -1f, 0f, -0.83027786f),
					new float4(-1f, 0f, 0f, -0.135555491f),
					new float4(1f, 0f, 0f, -0.1694445f),
					new float4(0f, 0f, 1f, -2.135f),
					new float4(0f, 0f, -1f, -2.135f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(-5.3544445f, -2.82972217f, 0f, 1f)), name: "Solid 36155 #101")
					.WithMesh(new float3[] { new float3(0.1694445f, 1.30472219f, -2.135f), new float3(-0.135555491f, 1.30472219f, -2.135f), new float3(-0.135555491f, 1.30472219f, 2.135f), new float3(0.1694445f, 1.30472219f, 2.135f), new float3(-0.135555491f, -0.83027786f, -2.135f), new float3(0.1694445f, -0.83027786f, -2.135f), new float3(0.1694445f, -0.83027786f, 2.135f), new float3(-0.135555491f, -0.83027786f, 2.135f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 1, 4, 7, 2, -1, 5, 0, 3, 6, -1, 6, 3, 2, 7, -1, 0, 5, 4, 1, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 1f, 0f, -0.18638885f),
					new float4(0f, -1f, 0f, -0.118611157f),
					new float4(-1f, 0f, 0f, -0.6777779f),
					new float4(1f, 0f, 0f, -0.8472221f),
					new float4(0f, 0f, 1f, -1.22000015f),
					new float4(0f, 0f, -1f, -1.21999991f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(-6.337222f, 2.55861115f, 6.1f, 1f)), name: "Solid 58603 #133")
					.WithMesh(new float3[] { new float3(0.8472221f, 0.18638885f, -1.21999991f), new float3(-0.6777779f, 0.18638885f, -1.21999991f), new float3(-0.6777779f, 0.18638885f, 1.22000015f), new float3(0.8472221f, 0.18638885f, 1.22000015f), new float3(-0.6777779f, -0.118611157f, -1.21999991f), new float3(0.8472221f, -0.118611157f, -1.21999991f), new float3(0.8472221f, -0.118611157f, 1.22000015f), new float3(-0.6777779f, -0.118611157f, 1.22000015f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 1, 4, 7, 2, -1, 5, 0, 3, 6, -1, 6, 3, 2, 7, -1, 0, 5, 4, 1, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 1f, 0f, -1.11833358f),
					new float4(0f, -1f, 0f, -0.711666465f),
					new float4(-1f, 0f, 0f, -0.27111125f),
					new float4(1f, 0f, 0f, -0.338888764f),
					new float4(0f, 0f, 1f, -2.135f),
					new float4(0f, 0f, -1f, -2.135f),
				}, contents: 1, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(-5.21888876f, -4.16833353f, 0f, 1f)), name: "Water 67374 #142")
					.WithMesh(new float3[] { new float3(0.338888764f, 1.11833358f, -2.135f), new float3(-0.27111125f, 1.11833358f, -2.135f), new float3(-0.27111125f, 1.11833358f, 2.135f), new float3(0.338888764f, 1.11833358f, 2.135f), new float3(-0.27111125f, -0.711666465f, -2.135f), new float3(0.338888764f, -0.711666465f, -2.135f), new float3(0.338888764f, -0.711666465f, 2.135f), new float3(-0.27111125f, -0.711666465f, 2.135f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 1, 4, 7, 2, -1, 5, 0, 3, 6, -1, 6, 3, 2, 7, -1, 0, 5, 4, 1, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 1f, 0f, -1.3942858f),
					new float4(-0.274721116f, -0.96152395f, 0f, -1.42442894f),
					new float4(-1f, 0f, 0f, -3.66f),
					new float4(1f, 0f, 0f, -4.88f),
					new float4(0f, 0f, 1f, -2.0333333f),
					new float4(0f, 0f, -1f, -2.23666668f),
					new float4(0.274721116f, 0.96152395f, 0f, -2.09474874f),
				}, contents: 1, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(-10.37f, -2.00428581f, 0.101666667f, 1f)), name: "Water 345442 #374")
					.WithMesh(new float3[] { new float3(2.74500036f, 1.3942858f, 2.0333333f), new float3(2.74500036f, 1.3942858f, -2.23666668f), new float3(-3.66f, 1.3942858f, -2.23666668f), new float3(-3.66f, 1.3942858f, 2.0333333f), new float3(-3.66f, -0.4357142f, -2.23666668f), new float3(4.88f, -2.875714f, -2.23666668f), new float3(4.88f, -2.875714f, 2.0333333f), new float3(-3.66f, -0.4357142f, 2.0333333f), new float3(4.88f, 0.7842859f, -2.23666668f), new float3(4.88f, 0.7842859f, 2.0333333f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 2, 4, 7, 3, -1, 8, 9, 6, 5, -1, 9, 0, 3, 7, 6, -1, 1, 8, 5, 4, 2, -1, 8, 1, 0, 9, -1 }))
				;
		}

		// bm_c2a4b, Model: the mesh draws nothing where the exact CSG has a face; Water 67401 #144 face 5 near (20, -5.185,
		// -2.44) (3 brushes)
		static ContentsScene BmC2a4bModel0Site2()
		{
			return new ContentsScene()
				.WithContentsList("Solid", "Water", "Glass", "Grate")
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 1f, 0f, -7.45555544f),
					new float4(0f, -1f, 0f, -4.74444437f),
					new float4(0f, 0f, 1f, -1.22f),
					new float4(0f, 0f, -1f, -1.21999991f),
					new float4(-1f, 0f, 0f, -4.88f),
					new float4(0.707106769f, 0f, -0.707106769f, -4.313351f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(14.64f, -9.8955555f, -3.66f, 1f)), name: "Solid 4046 #35")
					.WithMesh(new float3[] { new float3(7.31999969f, 7.45555544f, 1.22f), new float3(4.88f, 7.45555544f, -1.21999991f), new float3(-4.88f, 7.45555544f, -1.21999991f), new float3(-4.88f, 7.45555544f, 1.22f), new float3(4.88f, -4.74444437f, -1.21999991f), new float3(7.31999969f, -4.74444437f, 1.22f), new float3(-4.88f, -4.74444437f, 1.22f), new float3(-4.88f, -4.74444437f, -1.21999991f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 5, 0, 3, 6, -1, 1, 4, 7, 2, -1, 2, 7, 6, 3, -1, 4, 1, 0, 5, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 1f, 0f, -7.082778f),
					new float4(0f, -1f, 0f, -4.5072217f),
					new float4(-1f, 0f, 0f, -15.1822214f),
					new float4(1f, 0f, 0f, -18.97778f),
					new float4(0f, 0f, 1f, -2.44f),
					new float4(0f, 0f, -1f, -2.44f),
				}, contents: 1, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(20.0622215f, -10.1327782f, 0f, 1f)), name: "Water 67401 #144")
					.WithMesh(new float3[] { new float3(18.97778f, 7.082778f, -2.44f), new float3(-15.1822214f, 7.082778f, -2.44f), new float3(-15.1822214f, 7.082778f, 2.44f), new float3(18.97778f, 7.082778f, 2.44f), new float3(-15.1822214f, -4.5072217f, -2.44f), new float3(18.97778f, -4.5072217f, -2.44f), new float3(18.97778f, -4.5072217f, 2.44f), new float3(-15.1822214f, -4.5072217f, 2.44f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 1, 4, 7, 2, -1, 5, 0, 3, 6, -1, 6, 3, 2, 7, -1, 0, 5, 4, 1, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 1f, 0f, -2.60944438f),
					new float4(0f, -1f, 0f, -1.66055572f),
					new float4(-1f, 0f, 0f, -5.42222261f),
					new float4(1f, 0f, 0f, -6.777777f),
					new float4(0f, 0f, 1f, -6.1f),
					new float4(0f, 0f, -1f, -6.1f),
				}, contents: 1, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(24.9422226f, -5.65944433f, -8.54f, 1f)), name: "Water 389658 #417")
					.WithMesh(new float3[] { new float3(6.777777f, 2.60944438f, -6.1f), new float3(-5.42222261f, 2.60944438f, -6.1f), new float3(-5.42222261f, 2.60944438f, 6.1f), new float3(6.777777f, 2.60944438f, 6.1f), new float3(-5.42222261f, -1.66055572f, -6.1f), new float3(6.777777f, -1.66055572f, -6.1f), new float3(6.777777f, -1.66055572f, 6.1f), new float3(-5.42222261f, -1.66055572f, 6.1f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 1, 4, 7, 2, -1, 5, 0, 3, 6, -1, 6, 3, 2, 7, -1, 0, 5, 4, 1, -1 }))
				;
		}

		// bm_c2a4b, Model: the mesh draws nothing where the exact CSG has a face; Solid 143 #25 face 2 near (20, -5.185,
		// 2.44) (21 brushes)
		static ContentsScene BmC2a4bModel0Site3()
		{
			return new ContentsScene()
				.WithContentsList("Solid", "Water", "Glass", "Grate")
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 1f, 0f, -2.982222f),
					new float4(0f, -1f, 0f, -1.89777792f),
					new float4(0f, 0f, -1f, -1.22f),
					new float4(0f, 0f, 1f, -1.21999991f),
					new float4(1f, 0f, 0f, -5.42222261f),
					new float4(-1f, 0f, 0f, -4.337777f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(14.0977774f, -5.422222f, 3.66f, 1f)), name: "Solid 97 #18")
					.WithMesh(new float3[] { new float3(-4.337777f, 2.982222f, -1.22f), new float3(-4.337777f, 2.982222f, 1.21999991f), new float3(5.42222261f, 2.982222f, 1.21999991f), new float3(5.42222261f, 2.982222f, -1.22f), new float3(-4.337777f, -1.89777792f, 1.21999991f), new float3(-4.337777f, -1.89777792f, -1.22f), new float3(5.42222261f, -1.89777792f, -1.22f), new float3(5.42222261f, -1.89777792f, 1.21999991f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 5, 0, 3, 6, -1, 1, 4, 7, 2, -1, 2, 7, 6, 3, -1, 4, 1, 0, 5, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 1f, 0f, -5.96444464f),
					new float4(0f, -1f, 0f, -3.79555535f),
					new float4(0f, 0f, 1f, -2.44f),
					new float4(0f, 0f, -1f, -2.44f),
					new float4(-1f, 0f, 0f, -1.084445f),
					new float4(1f, 0f, 0f, -1.35555494f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(23.044445f, -8.404445f, 0f, 1f)), name: "Solid 125 #24")
					.WithMesh(new float3[] { new float3(1.35555494f, 5.96444464f, 2.44f), new float3(1.35555494f, 5.96444464f, -2.44f), new float3(-1.084445f, 5.96444464f, -2.44f), new float3(-1.084445f, 5.96444464f, 2.44f), new float3(1.35555494f, -3.79555535f, -2.44f), new float3(1.35555494f, -3.79555535f, 2.44f), new float3(-1.084445f, -3.79555535f, 2.44f), new float3(-1.084445f, -3.79555535f, -2.44f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 5, 0, 3, 6, -1, 1, 4, 7, 2, -1, 2, 7, 6, 3, -1, 4, 1, 0, 5, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 1f, 0f, -7.32f),
					new float4(0f, -1f, 0f, -4.87999964f),
					new float4(0f, 0f, -1f, -0.813333333f),
					new float4(-1f, 0f, 0f, -0.813334f),
					new float4(0.707106769f, 0f, 0.707106769f, -0.575113058f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(20.333334f, -9.76f, 3.25333333f, 1f)), name: "Solid 143 #25")
					.WithMesh(new float3[] { new float3(-0.813334f, 7.32f, 1.62666667f), new float3(1.62666607f, 7.32f, -0.813333333f), new float3(-0.813334f, 7.32f, -0.813333333f), new float3(1.62666607f, -4.87999964f, -0.813333333f), new float3(-0.813334f, -4.87999964f, 1.62666667f), new float3(-0.813334f, -4.87999964f, -0.813333333f) }, new int[] { 0, 1, 2, -1, 3, 4, 5, -1, 1, 3, 5, 2, -1, 4, 0, 2, 5, -1, 0, 4, 3, 1, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 1f, 0f, -5.964444f),
					new float4(0f, -1f, 0f, -3.79555583f),
					new float4(0f, 0f, 1f, -6.1f),
					new float4(0f, 0f, -1f, -6.1f),
					new float4(-1f, 0f, 0f, -1.0844444f),
					new float4(1f, 0f, 0f, -1.35555553f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(-3.79555559f, -10.8444443f, 0f, 1f)), name: "Solid 5797 #39")
					.WithMesh(new float3[] { new float3(1.35555553f, 5.964444f, 6.1f), new float3(1.35555553f, 5.964444f, -6.1f), new float3(-1.0844444f, 5.964444f, -6.1f), new float3(-1.0844444f, 5.964444f, 6.1f), new float3(1.35555553f, -3.79555583f, -6.1f), new float3(1.35555553f, -3.79555583f, 6.1f), new float3(-1.0844444f, -3.79555583f, 6.1f), new float3(-1.0844444f, -3.79555583f, -6.1f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 5, 0, 3, 6, -1, 1, 4, 7, 2, -1, 2, 7, 6, 3, -1, 4, 1, 0, 5, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 1f, 0f, -4.473333f),
					new float4(0f, -1f, 0f, -2.846667f),
					new float4(1f, 0f, 0f, -4.0666666f),
					new float4(-1f, 0f, 0f, -3.25333333f),
					new float4(0f, 0f, 1f, -2.44f),
					new float4(0f, 0f, -1f, -2.44f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(0.813333333f, -11.7933331f, 0f, 1f)), name: "Solid 5807 #41")
					.WithMesh(new float3[] { new float3(4.0666666f, 4.473333f, -2.44f), new float3(-3.25333333f, 4.473333f, -2.44f), new float3(-3.25333333f, 4.473333f, 2.44f), new float3(4.0666666f, 4.473333f, 2.44f), new float3(-3.25333333f, -2.846667f, -2.44f), new float3(4.0666666f, -2.846667f, -2.44f), new float3(4.0666666f, -2.846667f, 2.44f), new float3(-3.25333333f, -2.846667f, 2.44f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 5, 0, 3, 6, -1, 1, 4, 7, 2, -1, 2, 7, 6, 3, -1, 4, 1, 0, 5, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 1f, 0f, -6.04190445f),
					new float4(0f, -1f, 0f, -3.71809554f),
					new float4(0f, 0f, 1f, -1.56857169f),
					new float4(1f, 0f, 0f, -4.64761925f),
					new float4(0.242535621f, 0f, 0.9701425f, -1.46537733f),
					new float4(-1f, 0f, 0f, -2.672381f),
					new float4(0f, 0f, -1f, -2.09142828f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(0.232380956f, -10.9219046f, 4.53142834f, 1f)), name: "Solid 5808 #42")
					.WithMesh(new float3[] { new float3(4.64761925f, 6.04190445f, -2.09142828f), new float3(-2.672381f, 6.04190445f, -2.09142828f), new float3(-2.672381f, 6.04190445f, 1.56857169f), new float3(-0.232381135f, 6.04190445f, 1.56857169f), new float3(4.64761925f, 6.04190445f, 0.3485716f), new float3(-2.672381f, -3.71809554f, -2.09142828f), new float3(4.64761925f, -3.71809554f, -2.09142828f), new float3(4.64761925f, -3.71809554f, 0.3485716f), new float3(-0.232381135f, -3.71809554f, 1.56857169f), new float3(-2.672381f, -3.71809554f, 1.56857169f) }, new int[] { 0, 1, 2, 3, 4, -1, 5, 6, 7, 8, 9, -1, 2, 9, 8, 3, -1, 6, 0, 4, 7, -1, 7, 4, 3, 8, -1, 1, 5, 9, 2, -1, 5, 1, 0, 6, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 1f, 0f, -2.66875f),
					new float4(0f, -1f, 0f, -1.60125f),
					new float4(0f, 0f, 1f, -6.1f),
					new float4(0f, 0f, -1f, -6.1f),
					new float4(-1f, 0f, 0f, -4.0666666f),
					new float4(1f, 0f, 0f, -5.693333f),
					new float4(0.242535621f, 0f, 0.9701425f, -6.11513138f),
					new float4(0.242535621f, 0f, -0.9701425f, -6.11513138f),
				}, contents: 1, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(-0.813333333f, -5.71875f, 0f, 1f)), name: "Water 67398 #143")
					.WithMesh(new float3[] { new float3(5.693333f, 2.66875f, -4.88f), new float3(0.8133334f, 2.66875f, -6.1f), new float3(-4.0666666f, 2.66875f, -6.1f), new float3(-4.0666666f, 2.66875f, 6.1f), new float3(0.8133334f, 2.66875f, 6.1f), new float3(5.693333f, 2.66875f, 4.88f), new float3(0.8133334f, -1.60125f, -6.1f), new float3(5.693333f, -1.60125f, -4.88f), new float3(5.693333f, -1.60125f, 4.88f), new float3(0.8133334f, -1.60125f, 6.1f), new float3(-4.0666666f, -1.60125f, 6.1f), new float3(-4.0666666f, -1.60125f, -6.1f) }, new int[] { 0, 1, 2, 3, 4, 5, -1, 6, 7, 8, 9, 10, 11, -1, 9, 4, 3, 10, -1, 1, 6, 11, 2, -1, 2, 11, 10, 3, -1, 7, 0, 5, 8, -1, 8, 5, 4, 9, -1, 0, 7, 6, 1, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 1f, 0f, -7.082778f),
					new float4(0f, -1f, 0f, -4.5072217f),
					new float4(-1f, 0f, 0f, -15.1822214f),
					new float4(1f, 0f, 0f, -18.97778f),
					new float4(0f, 0f, 1f, -2.44f),
					new float4(0f, 0f, -1f, -2.44f),
				}, contents: 1, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(20.0622215f, -10.1327782f, 0f, 1f)), name: "Water 67401 #144")
					.WithMesh(new float3[] { new float3(18.97778f, 7.082778f, -2.44f), new float3(-15.1822214f, 7.082778f, -2.44f), new float3(-15.1822214f, 7.082778f, 2.44f), new float3(18.97778f, 7.082778f, 2.44f), new float3(-15.1822214f, -4.5072217f, -2.44f), new float3(18.97778f, -4.5072217f, -2.44f), new float3(18.97778f, -4.5072217f, 2.44f), new float3(-15.1822214f, -4.5072217f, 2.44f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 1, 4, 7, 2, -1, 5, 0, 3, 6, -1, 6, 3, 2, 7, -1, 0, 5, 4, 1, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 1f, 0f, -2.60944438f),
					new float4(0f, -1f, 0f, -1.66055572f),
					new float4(-1f, 0f, 0f, -5.42222261f),
					new float4(1f, 0f, 0f, -6.777777f),
					new float4(0f, 0f, 1f, -6.1f),
					new float4(0f, 0f, -1f, -6.1f),
				}, contents: 1, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(24.9422226f, -5.65944433f, 8.54f, 1f)), name: "Water 67411 #145")
					.WithMesh(new float3[] { new float3(6.777777f, 2.60944438f, -6.1f), new float3(-5.42222261f, 2.60944438f, -6.1f), new float3(-5.42222261f, 2.60944438f, 6.1f), new float3(6.777777f, 2.60944438f, 6.1f), new float3(-5.42222261f, -1.66055572f, -6.1f), new float3(6.777777f, -1.66055572f, -6.1f), new float3(6.777777f, -1.66055572f, 6.1f), new float3(-5.42222261f, -1.66055572f, 6.1f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 1, 4, 7, 2, -1, 5, 0, 3, 6, -1, 6, 3, 2, 7, -1, 0, 5, 4, 1, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, -1f, 0f, -0.948888838f),
					new float4(-1f, 0f, 0f, -1.084445f),
					new float4(1f, 0f, 0f, -1.35555494f),
					new float4(0f, -0.4472136f, 0.8944272f, -2.54613614f),
					new float4(0f, -0.4472136f, -0.8944272f, -2.66738057f),
					new float4(0f, 1f, 0f, -1.49111116f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(23.044445f, -1.49111116f, 0.0677777752f, 1f)), name: "Solid 89681 #211")
					.WithMesh(new float3[] { new float3(-1.084445f, -0.948888838f, -2.50777769f), new float3(1.35555494f, -0.948888838f, -2.50777769f), new float3(1.35555494f, -0.948888838f, 2.37222242f), new float3(-1.084445f, -0.948888838f, 2.37222242f), new float3(-1.084445f, 1.49111116f, 3.59222245f), new float3(-1.084445f, 1.49111116f, -3.72777772f), new float3(1.35555494f, 1.49111116f, -3.72777772f), new float3(1.35555494f, 1.49111116f, 3.59222245f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 0, 3, -1, 6, 7, 2, 1, -1, 7, 4, 3, 2, -1, 5, 6, 1, 0, -1, 6, 5, 4, 7, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 1f, 0f, -0.372777343f),
					new float4(0f, -1f, 0f, -0.237222672f),
					new float4(0f, 0f, 1f, -2.44f),
					new float4(0f, 0f, -1f, -2.44f),
					new float4(-1f, 0f, 0f, -2.16888857f),
					new float4(1f, 0f, 0f, -2.71111155f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(21.6888885f, -15.0127773f, 0f, 1f)), name: "Solid 92151 #214")
					.WithMesh(new float3[] { new float3(2.71111155f, 0.372777343f, 2.44f), new float3(2.71111155f, 0.372777343f, -2.44f), new float3(-2.16888857f, 0.372777343f, -2.44f), new float3(-2.16888857f, 0.372777343f, 2.44f), new float3(2.71111155f, -0.237222672f, -2.44f), new float3(2.71111155f, -0.237222672f, 2.44f), new float3(-2.16888857f, -0.237222672f, 2.44f), new float3(-2.16888857f, -0.237222672f, -2.44f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 5, 0, 3, 6, -1, 1, 4, 7, 2, -1, 2, 7, 6, 3, -1, 4, 1, 0, 5, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 1f, 0f, -0.372777343f),
					new float4(0f, -1f, 0f, -0.237222672f),
					new float4(0f, 0f, 1f, -2.44f),
					new float4(0f, 0f, -1f, -2.44f),
					new float4(1f, 0f, 0f, -1.35555637f),
					new float4(-1f, 0f, 0f, -1.08444369f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(25.4844437f, -15.0127773f, 0f, 1f)), name: "Solid 92152 #215")
					.WithMesh(new float3[] { new float3(-1.08444369f, 0.372777343f, -2.44f), new float3(-1.08444369f, 0.372777343f, 2.44f), new float3(1.35555637f, 0.372777343f, 2.44f), new float3(1.35555637f, 0.372777343f, -2.44f), new float3(-1.08444369f, -0.237222672f, 2.44f), new float3(-1.08444369f, -0.237222672f, -2.44f), new float3(1.35555637f, -0.237222672f, -2.44f), new float3(1.35555637f, -0.237222672f, 2.44f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 1, 4, 7, 2, -1, 5, 0, 3, 6, -1, 6, 3, 2, 7, -1, 0, 5, 4, 1, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 1f, 0f, -2.982222f),
					new float4(0f, -1f, 0f, -1.89777792f),
					new float4(-1f, 0f, 0f, -2.16888857f),
					new float4(1f, 0f, 0f, -2.71111131f),
					new float4(0f, 0f, -1f, -0.609999955f),
					new float4(0f, 0f, 1f, -0.6100001f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(7.04888868f, -5.422222f, 3.05f, 1f)), name: "Solid 105358 #232")
					.WithMesh(new float3[] { new float3(-2.16888857f, 2.982222f, 0.6100001f), new float3(2.71111131f, 2.982222f, 0.6100001f), new float3(2.71111131f, 2.982222f, -0.609999955f), new float3(-2.16888857f, 2.982222f, -0.609999955f), new float3(2.71111131f, -1.89777792f, 0.6100001f), new float3(-2.16888857f, -1.89777792f, 0.6100001f), new float3(-2.16888857f, -1.89777792f, -0.609999955f), new float3(2.71111131f, -1.89777792f, -0.609999955f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 5, 0, 3, 6, -1, 1, 4, 7, 2, -1, 2, 7, 6, 3, -1, 4, 1, 0, 5, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, -1f, 0f, -1.30472219f),
					new float4(1f, 0f, 0f, -0.338888764f),
					new float4(-1f, 0f, 0f, -0.27111125f),
					new float4(0f, 1f, 0f, -2.05027771f),
					new float4(0f, 0f, -1f, -1.410625f),
					new float4(0f, 0f, 1f, -1.410625f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(-5.21888876f, -3.57527781f, 3.545625f, 1f)), name: "Solid 132619 #260")
					.WithMesh(new float3[] { new float3(0.338888764f, -1.30472219f, 1.410625f), new float3(-0.27111125f, -1.30472219f, 1.410625f), new float3(-0.27111125f, -1.30472219f, -1.410625f), new float3(0.338888764f, -1.30472219f, -1.410625f), new float3(0.338888764f, 2.05027771f, 1.410625f), new float3(0.338888764f, 2.05027771f, -1.410625f), new float3(-0.27111125f, 2.05027771f, 1.410625f), new float3(-0.27111125f, 2.05027771f, -1.410625f) }, new int[] { 0, 1, 2, 3, -1, 4, 0, 3, 5, -1, 1, 6, 7, 2, -1, 6, 4, 5, 7, -1, 7, 5, 3, 2, -1, 4, 6, 1, 0, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(1f, 0f, 0f, -0.254166871f),
					new float4(-1f, 0f, 0f, -0.203333125f),
					new float4(0f, 1f, 0f, -1.35555565f),
					new float4(0f, -1f, 0f, -1.08444428f),
					new float4(0f, 0f, -1f, -5.25277758f),
					new float4(0f, 0f, 1f, -4.20222235f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(-5.286667f, 3.52444434f, 0.5252778f, 1f)), name: "Solid 284321 #342")
					.WithMesh(new float3[] { new float3(0.254166871f, 1.35555565f, 4.20222235f), new float3(0.254166871f, -1.08444428f, 4.20222235f), new float3(0.254166871f, -1.08444428f, -5.25277758f), new float3(0.254166871f, 1.35555565f, -5.25277758f), new float3(-0.203333125f, -1.08444428f, 4.20222235f), new float3(-0.203333125f, 1.35555565f, 4.20222235f), new float3(-0.203333125f, 1.35555565f, -5.25277758f), new float3(-0.203333125f, -1.08444428f, -5.25277758f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 5, 0, 3, 6, -1, 1, 4, 7, 2, -1, 2, 7, 6, 3, -1, 4, 1, 0, 5, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(1f, 0f, 0f, -0.338888764f),
					new float4(-1f, 0f, 0f, -0.27111125f),
					new float4(0f, 0f, -1f, -3.79555535f),
					new float4(0f, -1f, 0f, -0.11861112f),
					new float4(0f, 1f, 0f, -0.18638888f),
					new float4(0f, 0f, 1f, -4.74444437f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(-5.21888876f, -1.40638888f, -2.30444455f, 1f)), name: "Solid 284379 #343")
					.WithMesh(new float3[] { new float3(0.338888764f, 0.18638888f, 4.74444437f), new float3(0.338888764f, -0.11861112f, 4.74444437f), new float3(0.338888764f, -0.11861112f, -3.79555535f), new float3(0.338888764f, 0.18638888f, -3.79555535f), new float3(-0.27111125f, -0.11861112f, 4.74444437f), new float3(-0.27111125f, 0.18638888f, 4.74444437f), new float3(-0.27111125f, 0.18638888f, -3.79555535f), new float3(-0.27111125f, -0.11861112f, -3.79555535f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 6, 3, 2, 7, -1, 1, 4, 7, 2, -1, 5, 0, 3, 6, -1, 0, 5, 4, 1, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(1f, 0f, 0f, -0.338888764f),
					new float4(-1f, 0f, 0f, -0.27111125f),
					new float4(0f, 0f, 1f, -1.11833334f),
					new float4(0f, -1f, 0f, -0.474444419f),
					new float4(0f, 1f, 0f, -0.7455556f),
					new float4(0f, 0f, -1f, -1.39791656f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(-5.21888876f, 1.69444442f, 3.83791661f, 1f)), name: "Solid 284741 #347")
					.WithMesh(new float3[] { new float3(0.338888764f, -0.474444419f, -1.39791656f), new float3(0.338888764f, 0.7455556f, -1.39791656f), new float3(0.338888764f, 0.7455556f, 1.11833334f), new float3(0.338888764f, -0.474444419f, 1.11833334f), new float3(-0.27111125f, 0.7455556f, -1.39791656f), new float3(-0.27111125f, -0.474444419f, -1.39791656f), new float3(-0.27111125f, -0.474444419f, 1.11833334f), new float3(-0.27111125f, 0.7455556f, 1.11833334f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 2, 7, 6, 3, -1, 5, 0, 3, 6, -1, 1, 4, 7, 2, -1, 4, 1, 0, 5, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(1f, 0f, 0f, -0.6777779f),
					new float4(-1f, 0f, 0f, -0.542222142f),
					new float4(0f, -1f, 0f, -0.474444419f),
					new float4(0f, 1f, 0f, -0.7455556f),
					new float4(0f, 0f, -1f, -4.27f),
					new float4(0f, 0f, 1f, -4.27f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(-5.557778f, 1.69444442f, -1.83f, 1f)), name: "Solid 284742 #348")
					.WithMesh(new float3[] { new float3(0.6777779f, 0.7455556f, 4.27f), new float3(0.6777779f, -0.474444419f, 4.27f), new float3(0.6777779f, -0.474444419f, -4.27f), new float3(0.6777779f, 0.7455556f, -4.27f), new float3(-0.542222142f, -0.474444419f, 4.27f), new float3(-0.542222142f, 0.7455556f, 4.27f), new float3(-0.542222142f, 0.7455556f, -4.27f), new float3(-0.542222142f, -0.474444419f, -4.27f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 1, 4, 7, 2, -1, 5, 0, 3, 6, -1, 6, 3, 2, 7, -1, 0, 5, 4, 1, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(1f, 0f, 0f, -0.338888764f),
					new float4(-1f, 0f, 0f, -0.27111125f),
					new float4(0f, 0f, 1f, -1.11833334f),
					new float4(0f, 1f, 0f, -0.6777778f),
					new float4(0f, 0f, -1f, -1.39791656f),
					new float4(0f, -1f, 0f, -0.5422222f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(-5.21888876f, 0.5422222f, 3.83791661f, 1f)), name: "Solid 284750 #349")
					.WithMesh(new float3[] { new float3(0.338888764f, -0.5422222f, 1.11833334f), new float3(0.338888764f, -0.5422222f, -1.39791656f), new float3(0.338888764f, 0.6777778f, -1.39791656f), new float3(0.338888764f, 0.6777778f, 1.11833334f), new float3(-0.27111125f, -0.5422222f, -1.39791656f), new float3(-0.27111125f, -0.5422222f, 1.11833334f), new float3(-0.27111125f, 0.6777778f, 1.11833334f), new float3(-0.27111125f, 0.6777778f, -1.39791656f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 5, 0, 3, 6, -1, 2, 7, 6, 3, -1, 1, 4, 7, 2, -1, 4, 1, 0, 5, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(1f, 0f, 0f, -0.338888764f),
					new float4(-1f, 0f, 0f, -0.27111125f),
					new float4(0f, -1f, 0f, -0.593055546f),
					new float4(0f, 0f, 1f, -1.11833334f),
					new float4(0f, 0f, -1f, -1.39791656f),
					new float4(0f, 1f, 0f, -0.93194443f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(-5.21888876f, -0.93194443f, 3.83791661f, 1f)), name: "Solid 284751 #350")
					.WithMesh(new float3[] { new float3(0.338888764f, 0.93194443f, -1.39791656f), new float3(0.338888764f, 0.93194443f, 1.11833334f), new float3(0.338888764f, -0.593055546f, 1.11833334f), new float3(0.338888764f, -0.593055546f, -1.39791656f), new float3(-0.27111125f, 0.93194443f, 1.11833334f), new float3(-0.27111125f, 0.93194443f, -1.39791656f), new float3(-0.27111125f, -0.593055546f, -1.39791656f), new float3(-0.27111125f, -0.593055546f, 1.11833334f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 6, 3, 2, 7, -1, 1, 4, 7, 2, -1, 5, 0, 3, 6, -1, 0, 5, 4, 1, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 1f, 0f, -1.03603172f),
					new float4(-1f, 0f, 0f, -0.2711111f),
					new float4(1f, 0f, 0f, -0.338888884f),
					new float4(0f, 0f, 1f, -0.305f),
					new float4(-0.274711251f, -0.961526752f, 0f, -1.25685644f),
					new float4(0f, 0f, -1f, -0.38125f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(-5.828889f, 0.183968276f, 2.135f, 1f)), name: "Solid 308270 #357")
					.WithMesh(new float3[] { new float3(0.338888884f, 1.03603172f, -0.38125f), new float3(-0.2711111f, 1.03603172f, -0.38125f), new float3(-0.2711111f, 1.03603172f, 0.305f), new float3(0.338888884f, 1.03603172f, 0.305f), new float3(-0.2711111f, -1.22968936f, -0.38125f), new float3(-0.2711111f, -1.22968936f, 0.305f), new float3(0.338888884f, -1.40396821f, -0.38125f), new float3(0.338888884f, -1.40396821f, 0.305f) }, new int[] { 0, 1, 2, 3, -1, 1, 4, 5, 2, -1, 6, 0, 3, 7, -1, 5, 7, 3, 2, -1, 4, 6, 7, 5, -1, 6, 4, 1, 0, -1 }))
				;
		}

		// bm_c2a4b, Model: the mesh draws nothing where the exact CSG has a face; Water 67450 #155 face 5 near (-21.5,
		// -9.608, -12.2) (4 brushes)
		static ContentsScene BmC2a4bModel0Site4()
		{
			return new ContentsScene()
				.WithContentsList("Solid", "Water", "Glass", "Grate")
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 1f, 0f, -2.982222f),
					new float4(0f, -1f, 0f, -1.8977778f),
					new float4(0f, 0f, 1f, -0.152499661f),
					new float4(0f, 0f, -1f, -0.152500346f),
					new float4(1f, 0f, 0f, -1.355555f),
					new float4(-1f, 0f, 0f, -1.084445f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(-20.875555f, -7.862222f, -12.0475f, 1f)), name: "Solid 33990 #115")
					.WithMesh(new float3[] { new float3(-1.084445f, 2.982222f, -0.152500346f), new float3(-1.084445f, 2.982222f, 0.152499661f), new float3(1.355555f, 2.982222f, 0.152499661f), new float3(1.355555f, 2.982222f, -0.152500346f), new float3(-1.084445f, -1.8977778f, 0.152499661f), new float3(-1.084445f, -1.8977778f, -0.152500346f), new float3(1.355555f, -1.8977778f, -0.152500346f), new float3(1.355555f, -1.8977778f, 0.152499661f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 1, 4, 7, 2, -1, 5, 0, 3, 6, -1, 6, 3, 2, 7, -1, 0, 5, 4, 1, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 1f, 0f, -4.12476158f),
					new float4(0f, -1f, 0f, -5.635238f),
					new float4(-1f, 0f, 0f, -1.39428616f),
					new float4(1f, 0f, 0f, -1.0457139f),
					new float4(0f, 0f, 1f, -10.3409529f),
					new float4(0f, 0f, -1f, -9.179048f),
					new float4(0f, 0.8479983f, -0.529998958f, -4.483084f),
				}, contents: 1, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(-20.5657139f, -9.004762f, -3.02095246f, 1f)), name: "Water 67450 #155")
					.WithMesh(new float3[] { new float3(1.0457139f, 4.12476158f, -1.85904765f), new float3(-1.39428616f, 4.12476158f, -1.85904765f), new float3(-1.39428616f, 4.12476158f, 10.3409529f), new float3(1.0457139f, 4.12476158f, 10.3409529f), new float3(-1.39428616f, -5.635238f, -9.179048f), new float3(1.0457139f, -5.635238f, -9.179048f), new float3(1.0457139f, -5.635238f, 10.3409529f), new float3(-1.39428616f, -5.635238f, 10.3409529f), new float3(-1.39428616f, -0.450238466f, -9.179048f), new float3(1.0457139f, -0.450238466f, -9.179048f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 1, 8, 4, 7, 2, -1, 9, 0, 3, 6, 5, -1, 6, 3, 2, 7, -1, 8, 9, 5, 4, -1, 9, 8, 1, 0, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 1f, 0f, -5.591667f),
					new float4(0f, -1f, 0f, -3.55833316f),
					new float4(0f, 0f, 1f, -7.32000065f),
					new float4(0f, 0f, -1f, -7.31999969f),
					new float4(1f, 0f, 0f, -5.42222261f),
					new float4(-1f, 0f, 0f, -4.337777f),
				}, contents: 1, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(-21.2822227f, -11.0816669f, -19.52f, 1f)), name: "Water 67457 #156")
					.WithMesh(new float3[] { new float3(-4.337777f, 5.591667f, -7.31999969f), new float3(-4.337777f, 5.591667f, 7.32000065f), new float3(5.42222261f, 5.591667f, 7.32000065f), new float3(5.42222261f, 5.591667f, -7.31999969f), new float3(-4.337777f, -3.55833316f, 7.32000065f), new float3(-4.337777f, -3.55833316f, -7.31999969f), new float3(5.42222261f, -3.55833316f, -7.31999969f), new float3(5.42222261f, -3.55833316f, 7.32000065f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 1, 4, 7, 2, -1, 5, 0, 3, 6, -1, 6, 3, 2, 7, -1, 0, 5, 4, 1, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, -1f, 0f, -0.237222672f),
					new float4(-1f, 0f, 0f, -4.337777f),
					new float4(1f, 0f, 0f, -5.42222261f),
					new float4(0f, 1f, 0f, -0.372777343f),
					new float4(0f, 0f, 1f, -7.32000065f),
					new float4(0f, 0f, -1f, -7.31999969f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(-21.2822227f, -15.0127773f, -19.52f, 1f)), name: "Solid 544441 #446")
					.WithMesh(new float3[] { new float3(-4.337777f, -0.237222672f, -7.31999969f), new float3(5.42222261f, -0.237222672f, -7.31999969f), new float3(5.42222261f, -0.237222672f, 7.32000065f), new float3(-4.337777f, -0.237222672f, 7.32000065f), new float3(-4.337777f, 0.372777343f, -7.31999969f), new float3(-4.337777f, 0.372777343f, 7.32000065f), new float3(5.42222261f, 0.372777343f, -7.31999969f), new float3(5.42222261f, 0.372777343f, 7.32000065f) }, new int[] { 0, 1, 2, 3, -1, 4, 0, 3, 5, -1, 1, 6, 7, 2, -1, 6, 4, 5, 7, -1, 7, 5, 3, 2, -1, 4, 6, 1, 0, -1 }))
				;
		}

		// bm_c2a4b, Model: the mesh draws nothing where the exact CSG has a face; Water 67457 #156 face 2 near (-20.5,
		// -9.608, -12.2) (3 brushes)
		static ContentsScene BmC2a4bModel0Site5()
		{
			return new ContentsScene()
				.WithContentsList("Solid", "Water", "Glass", "Grate")
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 1f, 0f, -2.982222f),
					new float4(0f, -1f, 0f, -1.8977778f),
					new float4(0f, 0f, 1f, -0.152499661f),
					new float4(0f, 0f, -1f, -0.152500346f),
					new float4(1f, 0f, 0f, -1.355555f),
					new float4(-1f, 0f, 0f, -1.084445f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(-20.875555f, -7.862222f, -12.0475f, 1f)), name: "Solid 33990 #115")
					.WithMesh(new float3[] { new float3(-1.084445f, 2.982222f, -0.152500346f), new float3(-1.084445f, 2.982222f, 0.152499661f), new float3(1.355555f, 2.982222f, 0.152499661f), new float3(1.355555f, 2.982222f, -0.152500346f), new float3(-1.084445f, -1.8977778f, 0.152499661f), new float3(-1.084445f, -1.8977778f, -0.152500346f), new float3(1.355555f, -1.8977778f, -0.152500346f), new float3(1.355555f, -1.8977778f, 0.152499661f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 1, 4, 7, 2, -1, 5, 0, 3, 6, -1, 6, 3, 2, 7, -1, 0, 5, 4, 1, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 1f, 0f, -4.12476158f),
					new float4(0f, -1f, 0f, -5.635238f),
					new float4(-1f, 0f, 0f, -1.39428616f),
					new float4(1f, 0f, 0f, -1.0457139f),
					new float4(0f, 0f, 1f, -10.3409529f),
					new float4(0f, 0f, -1f, -9.179048f),
					new float4(0f, 0.8479983f, -0.529998958f, -4.483084f),
				}, contents: 1, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(-20.5657139f, -9.004762f, -3.02095246f, 1f)), name: "Water 67450 #155")
					.WithMesh(new float3[] { new float3(1.0457139f, 4.12476158f, -1.85904765f), new float3(-1.39428616f, 4.12476158f, -1.85904765f), new float3(-1.39428616f, 4.12476158f, 10.3409529f), new float3(1.0457139f, 4.12476158f, 10.3409529f), new float3(-1.39428616f, -5.635238f, -9.179048f), new float3(1.0457139f, -5.635238f, -9.179048f), new float3(1.0457139f, -5.635238f, 10.3409529f), new float3(-1.39428616f, -5.635238f, 10.3409529f), new float3(-1.39428616f, -0.450238466f, -9.179048f), new float3(1.0457139f, -0.450238466f, -9.179048f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 1, 8, 4, 7, 2, -1, 9, 0, 3, 6, 5, -1, 6, 3, 2, 7, -1, 8, 9, 5, 4, -1, 9, 8, 1, 0, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 1f, 0f, -5.591667f),
					new float4(0f, -1f, 0f, -3.55833316f),
					new float4(0f, 0f, 1f, -7.32000065f),
					new float4(0f, 0f, -1f, -7.31999969f),
					new float4(1f, 0f, 0f, -5.42222261f),
					new float4(-1f, 0f, 0f, -4.337777f),
				}, contents: 1, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(-21.2822227f, -11.0816669f, -19.52f, 1f)), name: "Water 67457 #156")
					.WithMesh(new float3[] { new float3(-4.337777f, 5.591667f, -7.31999969f), new float3(-4.337777f, 5.591667f, 7.32000065f), new float3(5.42222261f, 5.591667f, 7.32000065f), new float3(5.42222261f, 5.591667f, -7.31999969f), new float3(-4.337777f, -3.55833316f, 7.32000065f), new float3(-4.337777f, -3.55833316f, -7.31999969f), new float3(5.42222261f, -3.55833316f, -7.31999969f), new float3(5.42222261f, -3.55833316f, 7.32000065f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 1, 4, 7, 2, -1, 5, 0, 3, 6, -1, 6, 3, 2, 7, -1, 0, 5, 4, 1, -1 }))
				;
		}

		// bm_c2a4e, Details: the exact CSG made a triangle the mesh does not have; Solid 2028 #1640 face -1 near (5.458,
		// 10.637, 26.203) (5 brushes)
		static ContentsScene BmC2a4eDetails19Site1()
		{
			return new ContentsScene()
				.WithContentsList("Solid", "Water", "Glass", "Grate")
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0.7070572f, 0f, 0.70715636f, -0.9989078f),
					new float4(-0.7070824f, 0f, -0.7071312f, -0.9989077f),
					new float4(-0.7071353f, 0f, 0.7070782f, -0.169427678f),
					new float4(0f, -1f, 0f, -0.0338889323f),
					new float4(0f, 1f, 0f, -0.04236107f),
					new float4(0.7071305f, 0f, -0.707083046f, -0.2117851f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(6.014888f, 10.6326389f, 27.05942f, 1f)), name: "Solid 2028 #1640")
					.WithMesh(new float3[] { new float3(0.856076f, -0.0338889323f, 0.556614041f), new float3(0.856076f, 0.04236107f, 0.556614041f), new float3(0.5864941f, 0.04236107f, 0.826158047f), new float3(0.5864941f, -0.0338889323f, 0.826158047f), new float3(-0.55655086f, 0.04236107f, -0.8561076f), new float3(-0.55655086f, -0.0338889323f, -0.8561076f), new float3(-0.826113462f, -0.0338889323f, -0.5865636f), new float3(-0.826113462f, 0.04236107f, -0.5865636f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 2, 7, 6, 3, -1, 5, 0, 3, 6, -1, 1, 4, 7, 2, -1, 4, 1, 0, 5, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, -1f, 0f, -0.055069007f),
					new float4(0.7071074f, -0.0005286906f, 0.707105935f, -0.05507158f),
					new float4(-0.7065999f, 0f, 0.7076133f, -0.169446781f),
					new float4(0.499597728f, -0.7075511f, 0.4997735f, -0.02395947f),
					new float4(-0.49996227f, 0.707086146f, -0.500066936f, -0.0299288873f),
					new float4(0.7078694f, -0.0009126573f, -0.706342757f, -0.211824968f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(5.269576f, 10.577569f, 26.3141174f, 1f)), name: "Solid 2032 #1644")
					.WithMesh(new float3[] { new float3(0.0809720755f, -0.055069007f, -0.21867153f), new float3(0.134775892f, -0.055069007f, -0.164751425f), new float3(-0.134862959f, -0.055069007f, 0.104792595f), new float3(-0.1887443f, -0.055069007f, 0.0509884022f), new float3(0.188703835f, 0.0211135913f, -0.11080537f), new float3(0.188781574f, 0.097431f, -0.11082606f), new float3(-0.08081101f, 0.09747133f, 0.158767134f), new float3(-0.08083955f, 0.02118099f, 0.158738628f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 6, 3, 2, 7, -1, 1, 4, 7, 2, -1, 5, 0, 3, 6, -1, 0, 5, 4, 1, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 1f, 0f, -0.177917063f),
					new float4(0f, -1f, 0f, -0.203332946f),
					new float4(0.706966043f, 0.0003541878f, 0.707247436f, -0.2033503f),
					new float4(-0.706232f, -0.000353820069f, 0.7079804f, -0.290580362f),
					new float4(0.707127154f, 7.01343743E-05f, -0.707086444f, -0.3195473f),
					new float4(0.5001147f, -0.706991732f, 0.5000479f, -0.179710418f),
					new float4(-0.706997633f, -0.000211982784f, -0.707215846f, -0.2541316f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(5.169392f, 10.7258329f, 26.2046375f, 1f)), name: "Solid 2038 #1650")
					.WithMesh(new float3[] { new float3(0.04626042f, 0.177917063f, -0.4056404f), new float3(-0.385494858f, 0.177917063f, 0.0259816647f), new float3(-0.0616333932f, 0.177917063f, 0.349043339f), new float3(0.369731873f, 0.177917063f, -0.0821503f), new float3(-0.38534224f, -0.203332946f, 0.0259433724f), new float3(0.0463364758f, -0.203332946f, -0.405602127f), new float3(0.2618979f, -0.203332946f, -0.190028325f), new float3(-0.169412613f, -0.203332946f, 0.241339743f), new float3(-0.06151879f, -0.0508329459f, 0.349043339f), new float3(0.369800478f, -0.0506709f, -0.0821043849f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 8, 9, 3, 2, -1, 1, 4, 7, 8, 2, -1, 5, 0, 3, 9, 6, -1, 6, 9, 8, 7, -1, 0, 5, 4, 1, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0.392088473f, -0.8321932f, -0.392072827f, -0.0169175565f),
					new float4(0.7077722f, 0.000350991759f, 0.7064406f, -0.9323177f),
					new float4(-0.7081891f, 0.000172897722f, -0.7060228f, -1.06550348f),
					new float4(0f, 1f, 0f, -0.0254171379f),
					new float4(-0.7071306f, -0.0003506736f, 0.707082868f, -0.04578649f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(6.24403858f, 10.6495829f, 26.9243565f, 1f)), name: "Solid 2039 #1651")
					.WithMesh(new float3[] { new float3(0.626906037f, -0.05083287f, 0.6916772f), new float3(-0.7857202f, -0.0508144945f, -0.7210444f), new float3(-0.7049338f, 0.0254171379f, -0.80206f), new float3(0.7077314f, 0.0254171379f, 0.610661566f), new float3(0.6268682f, 0.0254171379f, 0.6916772f), new float3(-0.785729766f, 0.0254171379f, -0.721016169f) }, new int[] { 0, 1, 2, 3, -1, 4, 0, 3, -1, 1, 5, 2, -1, 5, 4, 3, 2, -1, 4, 5, 1, 0, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, -1f, 0f, -0.06608299f),
					new float4(0.7070228f, 0f, 0.707190752f, -0.0559319034f),
					new float4(0.7536982f, -0.639700651f, 0.150738344f, -0.016260596f),
					new float4(-0.499866843f, 0.7072111f, -0.499985576f, -0.0215710755f),
					new float4(-0.707224965f, 0f, 0.7069886f, -0.03048987f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(5.440318f, 10.588583f, 26.1421986f, 1f)), name: "Solid 2040 #1652")
					.WithMesh(new float3[] { new float3(-0.03594694f, -0.06608299f, 0.00716743758f), new float3(-0.08986651f, -0.06608299f, -0.0467701666f), new float3(-0.008987737f, -0.06608299f, -0.127629742f), new float3(0.0179810636f, 0.08641701f, 0.061113473f), new float3(0.0179810636f, 0.01016701f, 0.061113473f), new float3(0.09887248f, 0.08641701f, -0.019758733f) }, new int[] { 0, 1, 2, -1, 3, 4, 5, -1, 4, 0, 2, 5, -1, 1, 3, 5, 2, -1, 3, 1, 0, 4, -1 }))
				;
		}

		// bm_c2a4f, Details: the mesh has a triangle the exact CSG did not make; Solid 74920 #2146 face -1 near (-29.803,
		// -3.656, 18.19) (4 brushes)
		static ContentsScene BmC2a4fDetails40Site1()
		{
			return new ContentsScene()
				.WithContentsList("Solid", "Water", "Glass", "Grate")
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 1f, 0f, -0.415138841f),
					new float4(0f, -1f, 0f, -0.652361155f),
					new float4(0.706984758f, 0f, -0.7072288f, -0.211787388f),
					new float4(-0.7071556f, 0f, -0.707057953f, -0.0381389149f),
					new float4(0.7071556f, 0f, 0.707057953f, -0.0381391421f),
					new float4(-0.706984758f, 0f, 0.7072288f, -0.169431761f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(-29.8284378f, -2.93138885f, 18.21281f, 1f)), name: "Solid 74914 #2140")
					.WithMesh(new float3[] { new float3(-0.146771014f, 0.415138841f, 0.09285098f), new float3(-0.09282498f, 0.415138841f, 0.1467784f), new float3(0.176719055f, 0.415138841f, -0.122802846f), new float3(0.122773021f, 0.415138841f, -0.176730275f), new float3(-0.09282498f, -0.652361155f, 0.1467784f), new float3(-0.146771014f, -0.652361155f, 0.09285098f), new float3(0.122773021f, -0.652361155f, -0.176730275f), new float3(0.176719055f, -0.652361155f, -0.122802846f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 2, 7, 6, 3, -1, 5, 0, 3, 6, -1, 1, 4, 7, 2, -1, 4, 1, 0, 5, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 1f, 0f, -0.66083324f),
					new float4(0f, -1f, 0f, -0.406666756f),
					new float4(-0.7072317f, 0f, -0.706981838f, -0.1441411f),
					new float4(0.7070562f, 0f, -0.7071573f, -0.3060714f),
					new float4(-0.7072002f, 0f, 0.7070133f, -0.303851664f),
					new float4(0.707107961f, 0f, 0.707105637f, -0.160880327f),
					new float4(1f, 0f, 0f, -0.2528504f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(-29.68535f, -3.17708325f, 18.3275051f, 1f)), name: "Solid 74917 #2143")
					.WithMesh(new float3[] { new float3(0.2528504f, 0.66083324f, -0.0253317356f), new float3(0.2528504f, 0.66083324f, -0.180005133f), new float3(0.1144565f, 0.66083324f, -0.318379253f), new float3(-0.3167273f, 0.66083324f, 0.112956919f), new float3(-0.101110689f, 0.66083324f, 0.328630537f), new float3(0.2528504f, -0.406666756f, -0.180005133f), new float3(0.2528504f, -0.406666756f, -0.0253317356f), new float3(-0.101110689f, -0.406666756f, 0.328630537f), new float3(-0.3167273f, -0.406666756f, 0.112956919f), new float3(0.1144565f, -0.406666756f, -0.318379253f) }, new int[] { 0, 1, 2, 3, 4, -1, 5, 6, 7, 8, 9, -1, 8, 3, 2, 9, -1, 1, 5, 9, 2, -1, 7, 4, 3, 8, -1, 6, 0, 4, 7, -1, 0, 6, 5, 1, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 1f, 0f, -0.231927171f),
					new float4(0f, -1f, 0f, -0.149322838f),
					new float4(-0.7070753f, 0f, -0.70713824f, -0.227740154f),
					new float4(0.7069965f, 0f, -0.707217038f, -0.323186129f),
					new float4(-0.707125843f, 0f, 0.7070877f, -0.286722928f),
					new float4(-0.499881744f, 0.707242548f, -0.499926239f, -0.217199147f),
					new float4(0.707107961f, 0f, 0.707105637f, -0.229746282f),
					new float4(1f, 0f, 0f, -0.3136586f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(-29.7461586f, -3.81567717f, 18.2909222f, 1f)), name: "Solid 74920 #2146")
					.WithMesh(new float3[] { new float3(0.3136586f, 0.231927171f, 0.0112512074f), new float3(0.3136586f, 0.231927171f, -0.143422171f), new float3(0.175348386f, 0.231927171f, -0.281689227f), new float3(-0.2559319f, 0.231927171f, 0.149552673f), new float3(-0.0402926281f, 0.231927171f, 0.3652036f), new float3(0.3136586f, -0.149322838f, -0.143422171f), new float3(0.3136586f, -0.149322838f, 0.0112512074f), new float3(-0.0402926281f, -0.149322838f, 0.3652036f), new float3(-0.363785028f, -0.149322838f, 0.0416937545f), new float3(0.06747556f, -0.149322838f, -0.389528424f), new float3(-0.363785028f, 0.0794542953f, 0.0416937545f), new float3(0.06747556f, 0.0794542953f, -0.389528424f) }, new int[] { 0, 1, 2, 3, 4, -1, 5, 6, 7, 8, 9, -1, 10, 11, 9, 8, -1, 1, 5, 9, 11, 2, -1, 7, 4, 3, 10, 8, -1, 10, 3, 2, 11, -1, 6, 0, 4, 7, -1, 0, 6, 5, 1, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 1f, 0f, -0.06100012f),
					new float4(-0.707356036f, 0f, -0.706857443f, -0.0711572f),
					new float4(-0.7540585f, 0.639239f, -0.150895014f, -0.0162860136f),
					new float4(0.49969852f, -0.706866741f, 0.5006404f, -0.0144082205f),
					new float4(0.706984758f, -9.711937E-05f, -0.7072288f, -0.04576408f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(-30.0111752f, -3.64475f, 18.334425f, 1f)), name: "Solid 74925 #2151")
					.WithMesh(new float3[] { new float3(0.08991238f, 0.06100012f, 0.0251639578f), new float3(0.03588587f, 0.06100012f, -0.0288439076f), new float3(0.008896753f, 0.06100012f, 0.106027164f), new float3(-0.0179767869f, -0.0152449375f, -0.08267751f), new float3(-0.0179820322f, -0.09164795f, -0.08267226f), new float3(-0.09880684f, -0.09149988f, -0.00179044169f) }, new int[] { 0, 1, 2, -1, 3, 4, 5, -1, 1, 3, 5, 2, -1, 4, 0, 2, 5, -1, 0, 4, 3, 1, -1 }))
				;
		}

		// bm_c2a4f, Details: the mesh has a triangle the exact CSG did not make; Solid 74917 #2143 face -1 near (-29.845,
		// -3.05, 18.283) (4 brushes)
		static ContentsScene BmC2a4fDetails40Site2()
		{
			return new ContentsScene()
				.WithContentsList("Solid", "Water", "Glass", "Grate")
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, -1f, 0f, -0.06777774f),
					new float4(-0.7071556f, 0f, -0.707057953f, -0.0550842658f),
					new float4(0.7069161f, 0f, -0.707297444f, -0.211786568f),
					new float4(-0.4999803f, -0.7071835f, -0.499911249f, -0.0329547934f),
					new float4(0.499961436f, 0.7072101f, 0.499892384f, -0.0209716782f),
					new float4(-0.706984758f, -9.711937E-05f, 0.7072288f, -0.1694297f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(-29.8703976f, -2.44847226f, 18.1708775f, 1f)), name: "Solid 74915 #2141")
					.WithMesh(new float3[] { new float3(-0.05086522f, -0.06777774f, 0.188711479f), new float3(-0.104811251f, -0.06777774f, 0.134784058f), new float3(0.164727539f, -0.06777774f, -0.13479197f), new float3(0.218678817f, -0.06777774f, -0.0808697939f), new float3(-0.158754379f, 0.008472262f, 0.08087001f), new float3(-0.158759624f, 0.0847222656f, 0.08087525f), new float3(0.110784419f, 0.08472226f, -0.188706011f), new float3(0.110784419f, 0.008472254f, -0.188706011f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 6, 3, 2, 7, -1, 1, 4, 7, 2, -1, 5, 0, 3, 6, -1, 0, 5, 4, 1, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 1f, 0f, -0.66083324f),
					new float4(0f, -1f, 0f, -0.406666756f),
					new float4(-0.7072317f, 0f, -0.706981838f, -0.1441411f),
					new float4(0.7070562f, 0f, -0.7071573f, -0.3060714f),
					new float4(-0.7072002f, 0f, 0.7070133f, -0.303851664f),
					new float4(0.707107961f, 0f, 0.707105637f, -0.160880327f),
					new float4(1f, 0f, 0f, -0.2528504f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(-29.68535f, -3.17708325f, 18.3275051f, 1f)), name: "Solid 74917 #2143")
					.WithMesh(new float3[] { new float3(0.2528504f, 0.66083324f, -0.0253317356f), new float3(0.2528504f, 0.66083324f, -0.180005133f), new float3(0.1144565f, 0.66083324f, -0.318379253f), new float3(-0.3167273f, 0.66083324f, 0.112956919f), new float3(-0.101110689f, 0.66083324f, 0.328630537f), new float3(0.2528504f, -0.406666756f, -0.180005133f), new float3(0.2528504f, -0.406666756f, -0.0253317356f), new float3(-0.101110689f, -0.406666756f, 0.328630537f), new float3(-0.3167273f, -0.406666756f, 0.112956919f), new float3(0.1144565f, -0.406666756f, -0.318379253f) }, new int[] { 0, 1, 2, 3, 4, -1, 5, 6, 7, 8, 9, -1, 8, 3, 2, 9, -1, 1, 5, 9, 2, -1, 7, 4, 3, 8, -1, 6, 0, 4, 7, -1, 0, 6, 5, 1, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 1f, 0f, -0.235104069f),
					new float4(0f, -1f, 0f, -0.146145925f),
					new float4(-0.7070753f, 0f, -0.70713824f, -0.234100416f),
					new float4(0.70700866f, 3.9239676E-05f, -0.7072049f, -0.318624735f),
					new float4(-0.7071983f, 0f, 0.7070152f, -0.291258782f),
					new float4(-0.499870956f, -0.707273066f, -0.499893874f, -0.16102463f),
					new float4(0.707107961f, 0f, 0.707105637f, -0.223385811f),
					new float4(1f, 0f, 0f, -0.305937648f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(-29.7384377f, -2.370104f, 18.2921963f, 1f)), name: "Solid 74921 #2147")
					.WithMesh(new float3[] { new float3(0.305937648f, 0.235104069f, 0.009977101f), new float3(0.305937648f, 0.235104069f, -0.1446751f), new float3(0.05974826f, 0.235104069f, -0.390796185f), new float3(-0.371472776f, 0.235104069f, 0.04038647f), new float3(-0.04801356f, 0.235104069f, 0.36392948f), new float3(0.305937648f, -0.146145925f, -0.144696265f), new float3(0.305937648f, -0.146145925f, 0.009977101f), new float3(-0.04801356f, -0.146145925f, 0.36392948f), new float3(-0.26362142f, -0.146145925f, 0.148265764f), new float3(0.167629823f, -0.146145925f, -0.28296572f), new float3(0.0597546063f, 0.0063137915f, -0.390802532f), new float3(-0.371472776f, 0.006326948f, 0.04038647f) }, new int[] { 0, 1, 2, 3, 4, -1, 5, 6, 7, 8, 9, -1, 10, 11, 3, 2, -1, 1, 5, 9, 10, 2, -1, 7, 4, 3, 11, 8, -1, 8, 11, 10, 9, -1, 6, 0, 4, 7, -1, 0, 6, 5, 1, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 1f, 0f, -0.426999927f),
					new float4(0f, -1f, 0f, -0.640500069f),
					new float4(-0.980424464f, 0f, -0.196895629f, -0.0211700629f),
					new float4(0.7064407f, 0f, 0.707772255f, -0.02542499f),
					new float4(0.706984758f, 0f, -0.7072288f, -0.0381554551f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(-29.96625f, -2.94325f, 18.3685684f, 1f)), name: "Solid 74924 #2150")
					.WithMesh(new float3[] { new float3(0.0449866839f, 0.426999927f, -0.008979491f), new float3(-0.008959351f, 0.426999927f, -0.06290691f), new float3(-0.0360289365f, 0.426999927f, 0.0718837157f), new float3(-0.008959351f, -0.640500069f, -0.06290691f), new float3(0.0449866839f, -0.640500069f, -0.008979491f), new float3(-0.0360289365f, -0.640500069f, 0.0718837157f) }, new int[] { 0, 1, 2, -1, 3, 4, 5, -1, 1, 3, 5, 2, -1, 4, 0, 2, 5, -1, 0, 4, 3, 1, -1 }))
				;
		}

		// bm_c2a5a, Details: the exact CSG made a triangle the mesh does not have; Solid 4379 #1345 face -1 near (-70.701,
		// -37.22, -8.268) (4 brushes)
		static ContentsScene BmC2a5aDetails24Site1()
		{
			return new ContentsScene()
				.WithContentsList("Solid", "Water", "Glass", "Grate")
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0.8090465f, -0.587744653f, -0.000123018355f, -0.912695944f),
					new float4(-0.587785542f, 0.809016764f, -0.00013987784f, -0.58579427f),
					new float4(0.583713353f, 0.583713353f, 0.564408958f, -0.5027694f),
					new float4(-0.671586454f, -0.671586454f, -0.312958866f, -0.400064915f),
					new float4(0.325005352f, 0.325005352f, -0.888112068f, -0.148782045f),
					new float4(-0.3255185f, -0.32552f, 0.887735546f, -0.148779765f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(-69.99378f, -37.4297256f, -8.058143f, 1f)), name: "Solid 4379 #1345")
					.WithMesh(new float3[] { new float3(0.8706797f, -0.354438782f, 0.3568914f), new float3(0.4112734f, -0.986740232f, -0.04342154f), new float3(0.467315674f, -0.90953666f, -0.329357326f), new float3(0.9714838f, -0.215627238f, 0.109080151f), new float3(-0.7526843f, 0.177216649f, -0.043419838f), new float3(-0.1204162f, 0.636655867f, 0.356892645f), new float3(0.0183928255f, 0.7374638f, 0.109080151f), new float3(-0.675480247f, 0.23325929f, -0.329357326f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 5, 0, 3, 6, -1, 1, 4, 7, 2, -1, 2, 7, 6, 3, -1, 4, 1, 0, 5, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0.8090678f, -0.5877153f, 0f, -1.060661f),
					new float4(-0.587700069f, 0.8090789f, -0.000104625906f, -0.6882319f),
					new float4(0.671586454f, 0.671586454f, 0.312958866f, -0.435501635f),
					new float4(-0.707107365f, -0.7071062f, 0f, -0.435151935f),
					new float4(0.115309551f, 0.11530935f, -0.986614168f, -0.166220263f),
					new float4(-0.112446181f, -0.112446181f, 0.9872749f, -0.132977575f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(-70.57589f, -37.9792671f, -8.299601f, 1f)), name: "Solid 4380 #1346")
					.WithMesh(new float3[] { new float3(0.9933806f, -0.437199265f, 0.1980381f), new float3(0.5004223f, -1.11582148f, 0.06460034f), new float3(0.5004223f, -1.11582148f, -0.240399376f), new float3(1.04944587f, -0.360018f, -0.08789917f), new float3(-0.849199f, 0.23380211f, 0.0646005943f), new float3(-0.170576766f, 0.7267581f, 0.1980381f), new float3(-0.09337271f, 0.782800734f, -0.08789939f), new float3(-0.8491761f, 0.233779266f, -0.24039939f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 5, 0, 3, 6, -1, 1, 4, 7, 2, -1, 2, 7, 6, 3, -1, 4, 1, 0, 5, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0.587982237f, -0.808873832f, -0.000171371634f, -0.912726164f),
					new float4(-0.3088167f, 0.951121569f, -0.000140998673f, -0.585815668f),
					new float4(0.7355018f, 0.374753982f, 0.5644435f, -0.5027353f),
					new float4(-0.846427739f, -0.431298524f, -0.312316567f, -0.400006115f),
					new float4(0.409493029f, 0.208676f, -0.8881271f, -0.14876461f),
					new float4(-0.410240769f, -0.209033534f, 0.8876979f, -0.1487581f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(-70.83268f, -36.1869354f, -8.058143f, 1f)), name: "Solid 4399 #1365")
					.WithMesh(new float3[] { new float3(0.7185035f, -0.6061762f, 0.356885344f), new float3(0.0864022151f, -1.065575f, -0.043412406f), new float3(0.163377509f, -1.00956f, -0.3293826f), new float3(0.857274532f, -0.505248964f, 0.109049708f), new float3(-0.6610357f, 0.401284844f, -0.04341984f), new float3(0.08218922f, 0.6426596f, 0.356892675f), new float3(0.245387152f, 0.69561106f, 0.109080166f), new float3(-0.5704888f, 0.43064186f, -0.329357356f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 5, 0, 3, 6, -1, 1, 4, 7, 2, -1, 2, 7, 6, 3, -1, 4, 1, 0, 5, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0.58771354f, -0.8090691f, 0f, -1.06062579f),
					new float4(-0.3093472f, 0.950949132f, 0.0001014601f, -0.68819195f),
					new float4(0.846187353f, 0.431213677f, 0.313084155f, -0.43548432f),
					new float4(-0.890983641f, -0.454035282f, -0.000284049165f, -0.435155779f),
					new float4(0.1453051f, 0.0740375444f, -0.9866128f, -0.166221336f),
					new float4(-0.141686991f, -0.0722062141f, 0.9872746f, -0.132979289f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(-71.5561752f, -36.5297661f, -8.299601f, 1f)), name: "Solid 4400 #1366")
					.WithMesh(new float3[] { new float3(0.8096867f, -0.722758949f, 0.198033735f), new float3(0.131086648f, -1.21569884f, 0.0645935237f), new float3(0.1311576f, -1.21564734f, -0.2403851f), new float3(0.886894941f, -0.6666744f, -0.08788647f), new float3(-0.7353077f, 0.484484375f, 0.06460061f), new float3(0.06226896f, 0.743924737f, 0.1980381f), new float3(0.153006643f, 0.773472548f, -0.08789941f), new float3(-0.7352385f, 0.484539449f, -0.2403994f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 5, 0, 3, 6, -1, 1, 4, 7, 2, -1, 2, 7, 6, 3, -1, 4, 1, 0, 5, -1 }))
				;
		}

		// bm_c2a5a, Details: the mesh has a triangle the exact CSG did not make; Solid 5230 #1898 face -1 near (-67.642,
		// -37.706, 3.759) (4 brushes)
		static ContentsScene BmC2a5aDetails24Site2()
		{
			return new ContentsScene()
				.WithContentsList("Solid", "Water", "Glass", "Grate")
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(1f, 0f, 0f, -0.610021353f),
					new float4(-0.9510091f, 0.309162855f, -0.000202955489f, -0.488295674f),
					new float4(0.0500234067f, 0.315963566f, 0.947451651f, -0.452222526f),
					new float4(-0.08985199f, -0.566994846f, -0.8188061f, -0.456804961f),
					new float4(0.139593482f, 0.8817164f, -0.450655f, -0.147692189f),
					new float4(-0.139641851f, -0.881337941f, 0.4513797f, -0.147665411f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(-67.10002f, -37.6174622f, 4.195868f, 1f)), name: "Solid 5229 #1897")
					.WithMesh(new float3[] { new float3(0.610021353f, -0.0309560224f, 0.455419719f), new float3(0.610021353f, -0.431263417f, -0.326197445f), new float3(0.610021353f, -0.183490083f, -0.4977719f), new float3(0.610021353f, 0.254962146f, 0.360069484f), new float3(-0.5916794f, -0.240853667f, -0.326180518f), new float3(-0.4680857f, 0.139843211f, 0.455382f), new float3(-0.379702151f, 0.411655337f, 0.360069484f), new float3(-0.515073538f, -0.005320644f, -0.497685343f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 5, 0, 3, 6, -1, 1, 4, 7, 2, -1, 2, 7, 6, 3, -1, 4, 1, 0, 5, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(1f, 0f, 0f, -0.696737051f),
					new float4(-0.9510565f, 0.309017181f, 9.54140851E-05f, -0.5717142f),
					new float4(0.0897783563f, 0.5667096f, 0.8190117f, -0.490139931f),
					new float4(-0.129114643f, -0.8153102f, -0.5644455f, -0.385757774f),
					new float4(0.111278377f, 0.7026809f, -0.7027494f, -0.130997732f),
					new float4(-0.111254446f, -0.702755332f, 0.70267874f, -0.163723588f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(-67.18674f, -38.1544533f, 3.42065978f, 1f)), name: "Solid 5230 #1898")
					.WithMesh(new float3[] { new float3(0.696737051f, 0.105649896f, 0.4489743f), new float3(0.696737051f, -0.485207856f, -0.141947836f), new float3(0.696737051f, -0.3136717f, -0.3897223f), new float3(0.696737051f, 0.3535542f, 0.2774386f), new float3(-0.6875828f, -0.266012162f, -0.141906261f), new float3(-0.5049335f, 0.295941859f, 0.449027717f), new float3(-0.428332031f, 0.531749666f, 0.2774652f), new float3(-0.6345896f, -0.102839366f, -0.3897223f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 5, 0, 3, 6, -1, 1, 4, 7, 2, -1, 2, 7, 6, 3, -1, 4, 1, 0, 5, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0.950871646f, -0.3095853f, 0.0002746711f, -0.610158443f),
					new float4(-0.80907315f, 0.5877079f, 0.000162943354f, -0.488316f),
					new float4(0.145212069f, 0.285069168f, 0.9474434f, -0.4522334f),
					new float4(-0.260577f, -0.5113523f, -0.818913f, -0.456826866f),
					new float4(0.4052178f, 0.7954924f, -0.450544477f, -0.147766247f),
					new float4(-0.405199677f, -0.794970751f, 0.451480538f, -0.147733346f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(-68.13855f, -37.2597961f, 4.195868f, 1f)), name: "Solid 5249 #1917")
					.WithMesh(new float3[] { new float3(0.5705768f, -0.217994988f, 0.455459833f), new float3(0.446822226f, -0.598792732f, -0.326120645f), new float3(0.523656964f, -0.362952083f, -0.497834861f), new float3(0.659137249f, 0.0539281443f, 0.360069484f), new float3(-0.6372329f, -0.0462796f, -0.326180518f), new float3(-0.4018156f, 0.277593f, 0.455382f), new float3(-0.233872086f, 0.5088203f, 0.360069484f), new float3(-0.491595656f, 0.15426074f, -0.4977451f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 5, 0, 3, 6, -1, 1, 4, 7, 2, -1, 2, 7, 6, 3, -1, 4, 1, 0, 5, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0.9510658f, -0.308988482f, -7.45200741E-05f, -0.6967376f),
					new float4(-0.80905f, 0.5877398f, -7.273832E-05f, -0.5717386f),
					new float4(0.260538429f, 0.5113774f, 0.8189096f, -0.490191251f),
					new float4(-0.374755144f, -0.7355013f, -0.5644434f, -0.385764241f),
					new float4(0.322988153f, 0.633902431f, -0.70274204f, -0.130987391f),
					new float4(-0.3230315f, -0.633912146f, 0.7027133f, -0.163736492f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(-68.38701f, -37.7436562f, 3.42065978f, 1f)), name: "Solid 5250 #1918")
					.WithMesh(new float3[] { new float3(0.695320368f, -0.114812285f, 0.4490679f), new float3(0.5126818f, -0.6768308f, -0.141882092f), new float3(0.565682f, -0.51363647f, -0.3897223f), new float3(0.771902144f, 0.120947763f, 0.277480125f), new float3(-0.73609966f, -0.04051649f, -0.141922429f), new float3(-0.3888193f, 0.437603474f, 0.449027717f), new float3(-0.243138552f, 0.638118267f, 0.277465224f), new float3(-0.6352555f, 0.0982693061f, -0.3897223f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 5, 0, 3, 6, -1, 1, 4, 7, 2, -1, 2, 7, 6, 3, -1, 4, 1, 0, 5, -1 }))
				;
		}

		// bm_c2a5b, Details: the mesh has a triangle the exact CSG did not make; Glass 808242 #1332 face -1 near (-43.085,
		// 1.158, -37.591) (3 brushes)
		static ContentsScene BmC2a5bDetails43Site1()
		{
			return new ContentsScene()
				.WithContentsList("Solid", "Water", "Glass", "Grate")
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 0f, 1f, -0.266875148f),
					new float4(-1f, 0f, 0f, -2.37222219f),
					new float4(0f, 1f, 0f, -0.68625f),
					new float4(0f, -1f, 0f, -0.686250031f),
					new float4(0f, 0f, -1f, -0.26687485f),
					new float4(0.9701425f, 0.242535621f, 0f, -2.876742f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(-43.9877777f, 1.22f, -37.3243752f, 1f)), name: "Solid 708947 #1195")
					.WithMesh(new float3[] { new float3(3.13684f, -0.686250031f, 0.266875148f), new float3(2.79371524f, 0.68625f, 0.266875148f), new float3(-2.37222219f, 0.68625f, 0.266875148f), new float3(-2.37222219f, -0.686250031f, 0.266875148f), new float3(-2.37222219f, 0.68625f, -0.26687485f), new float3(-2.37222219f, -0.686250031f, -0.26687485f), new float3(2.79371524f, 0.68625f, -0.26687485f), new float3(3.13684f, -0.686250031f, -0.26687485f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 3, 2, -1, 1, 6, 4, 2, -1, 7, 0, 3, 5, -1, 6, 7, 5, 4, -1, 7, 6, 1, 0, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(1f, 0f, 0f, -1.2200011f),
					new float4(0f, 1f, 0f, -0.08128044f),
					new float4(-0.000457811053f, -0.966518462f, -0.2565967f, -0.08405167f),
					new float4(-0.0810118839f, -0.13466911f, 0.987573445f, -0.07047375f),
					new float4(0.06577959f, 0.110192351f, -0.991731167f, -0.06019041f),
					new float4(-1f, 0f, 0f, -1.219999f),
				}, contents: 2, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(-43.005f, 1.13871956f, -37.6586342f, 1f)), name: "Glass 808242 #1332")
					.WithMesh(new float3[] { new float3(1.2200011f, -0.0902492553f, 0.0102003291f), new float3(1.2200011f, 0.08128044f, 0.0292591844f), new float3(1.2200011f, 0.08128044f, 0.182522431f), new float3(1.2200011f, -0.128407061f, 0.153928682f), new float3(-1.219999f, 0.08128044f, -0.132581249f), new float3(-1.219999f, 0.08128044f, -0.0176338237f), new float3(-1.219999f, -0.07600962f, -0.03908247f), new float3(-1.219999f, -0.0473914333f, -0.146878123f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 2, 1, -1, 6, 7, 0, 3, -1, 5, 6, 3, 2, -1, 7, 4, 1, 0, -1, 4, 7, 6, 5, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(1f, 0f, 0f, -1.2200011f),
					new float4(-0.0004638556f, 0.966490269f, -0.25670293f, -0.08113822f),
					new float4(0f, -1f, 0f, -0.08419266f),
					new float4(-0.08097352f, 0.134669527f, 0.987576544f, -0.07040384f),
					new float4(0.06577959f, -0.110192351f, -0.991731167f, -0.0600459874f),
					new float4(-1f, 0f, 0f, -1.219999f),
				}, contents: 2, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(-43.005f, 1.30419266f, -37.6591034f, 1f)), name: "Glass 808265 #1355")
					.WithMesh(new float3[] { new float3(1.2200011f, -0.08419266f, 0.02972839f), new float3(1.2200011f, 0.08736983f, 0.0106658908f), new float3(1.2200011f, 0.125494838f, 0.154207081f), new float3(1.2200011f, -0.08419266f, 0.182800829f), new float3(-1.219999f, 0.04447921f, -0.146408916f), new float3(-1.219999f, 0.07308519f, -0.0387069955f), new float3(-1.219999f, -0.08419266f, -0.0172600169f), new float3(-1.219999f, -0.08419266f, -0.132112041f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 2, 1, -1, 6, 7, 0, 3, -1, 5, 6, 3, 2, -1, 7, 4, 1, 0, -1, 4, 7, 6, 5, -1 }))
				;
		}

		// bm_c2a5b, Details: the exact CSG made a triangle the mesh does not have; Solid 1625733 #1924 face -1 near (-15.08,
		// 1.735, -41.055) (6 brushes)
		static ContentsScene BmC2a5bDetails43Site2()
		{
			return new ContentsScene()
				.WithContentsList("Solid", "Water", "Glass", "Grate")
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0.923584461f, -0.00055138825f, 0.383394659f, -0.652648f),
					new float4(-0.7066433f, 0.000245781877f, -0.707569957f, -0.6532349f),
					new float4(-0.279777884f, 0.863947332f, 0.418711454f, -0.105851166f),
					new float4(0.13707155f, -0.9690806f, -0.205168486f, -0.08310207f),
					new float4(0.49882865f, 0.4401915f, -0.7465932f, -0.0755831748f),
					new float4(-0.499007285f, -0.439237058f, 0.747035861f, -0.07554618f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(-14.3640051f, 1.791875f, -40.2412453f, 1f)), name: "Solid 1625708 #1899")
					.WithMesh(new float3[] { new float3(0.509460151f, 0.0572464f, 0.475098f), new float3(0.5385369f, -0.09528941f, 0.404833823f), new float3(0.597041845f, -0.0571875051f, 0.263952255f), new float3(0.560600936f, 0.13346152f, 0.352011323f), new float3(-0.5809949f, -0.095312506f, -0.343008f), new float3(-0.6347521f, 0.05715803f, -0.2892683f), new float3(-0.5403084f, 0.133441627f, -0.3835618f), new float3(-0.472921133f, -0.05717799f, -0.450927019f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 5, 0, 3, 6, -1, 1, 4, 7, 2, -1, 2, 7, 6, 3, -1, 4, 1, 0, 5, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0.9239185f, 0.001032542f, 0.382588059f, -0.6448215f),
					new float4(-0.707102835f, -0.000632384443f, -0.707110465f, -0.642371237f),
					new float4(-0.137070909f, 0.9690747f, 0.205196857f, -0.105202049f),
					new float4(0f, -1f, 0f, -0.08472226f),
					new float4(0.5532885f, 0.08904925f, -0.8282162f, -0.07530114f),
					new float4(-0.5522249f, -0.109314151f, 0.8264975f, -0.07537223f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(-14.3388138f, 1.60972226f, -40.28191f, 1f)), name: "Solid 1625709 #1900")
					.WithMesh(new float3[] { new float3(0.513290763f, 0.08680159f, 0.445630878f), new float3(0.5207992f, -0.08472226f, 0.427961558f), new float3(0.5792077f, -0.08472226f, 0.286909848f), new float3(0.5717114f, 0.124959938f, 0.304446876f), new float3(-0.592491567f, -0.08472226f, -0.315884441f), new float3(-0.606186f, 0.086837f, -0.3023436f), new float3(-0.498234749f, 0.1249713f, -0.410327762f), new float3(-0.4846058f, -0.08472226f, -0.423769057f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 5, 0, 3, 6, -1, 1, 4, 7, 2, -1, 2, 7, 6, 3, -1, 4, 1, 0, 5, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0.7070791f, 0.00129717286f, 0.7071333f, -0.5823748f),
					new float4(-0.383764237f, 0.0005135182f, -0.923431f, -0.726764262f),
					new float4(-0.418459326f, 0.8641337f, 0.279579461f, -0.10352096f),
					new float4(0.205091879f, -0.9691071f, -0.136999041f, -0.08312886f),
					new float4(0.746424139f, 0.440587163f, -0.498732358f, -0.08395488f),
					new float4(-0.746397555f, -0.440812f, 0.498573422f, -0.06721448f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(-15.2536812f, 1.78975689f, -41.098938f, 1f)), name: "Solid 1625732 #1923")
					.WithMesh(new float3[] { new float3(0.254824638f, 0.0592149943f, 0.568657756f), new float3(0.308901161f, -0.09319068f, 0.5148649f), new float3(0.4168396f, -0.0550801232f, 0.406864852f), new float3(0.349250376f, 0.135534137f, 0.474099249f), new float3(-0.438919544f, -0.09318739f, -0.604669452f), new float3(-0.5093536f, 0.0592766851f, -0.5753133f), new float3(-0.386109918f, 0.135515109f, -0.626489162f), new float3(-0.29809773f, -0.05511506f, -0.6631717f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 5, 0, 3, 6, -1, 1, 4, 7, 2, -1, 2, 7, 6, 3, -1, 4, 1, 0, 5, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0.7079901f, -0.000799616333f, 0.7062219f, -0.574970365f),
					new float4(-0.3825063f, -0.000329760835f, -0.9239528f, -0.715369046f),
					new float4(-0.20532243f, 0.9690346f, 0.137166575f, -0.103130929f),
					new float4(0f, -1f, 0f, -0.08472226f),
					new float4(0.828274131f, 0.08823448f, -0.553332269f, -0.08351165f),
					new float4(-0.826592f, -0.108280748f, 0.5522869f, -0.0669631958f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(-15.2151089f, 1.60972226f, -41.12726f, 1f)), name: "Solid 1625733 #1924")
					.WithMesh(new float3[] { new float3(0.270477653f, 0.08686152f, 0.5430932f), new float3(0.283860564f, -0.08472226f, 0.529482543f), new float3(0.391491354f, -0.08472226f, 0.421582282f), new float3(0.378208071f, 0.1249691f, 0.4351362f), new float3(-0.459974349f, -0.08472226f, -0.5837939f), new float3(-0.477610916f, 0.0868396237f, -0.576553762f), new float3(-0.3366502f, 0.1249691f, -0.6349236f), new float3(-0.3191125f, -0.08472226f, -0.642109156f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 5, 0, 3, 6, -1, 1, 4, 7, 2, -1, 2, 7, 6, 3, -1, 4, 1, 0, 5, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0.3837436f, 0.0006776053f, 0.923439443f, -0.582229257f),
					new float4(0f, 0f, -1f, -0.726653457f),
					new float4(-0.4936842f, 0.864079058f, 0.09820012f, -0.103538565f),
					new float4(0.242233858f, -0.969020665f, -0.04818446f, -0.08312452f),
					new float4(0.8805297f, 0.440438241f, -0.175161779f, -0.08392325f),
					new float4(-0.88059634f, -0.44029817f, 0.175178856f, -0.06713537f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(-15.7811165f, 1.78975689f, -42.29741f, 1f)), name: "Solid 1625756 #1947")
					.WithMesh(new float3[] { new float3(0.01802633f, 0.0593261942f, 0.62296623f), new float3(0.08848224f, -0.09319f, 0.593799531f), new float3(0.2293298f, -0.05506939f, 0.535241067f), new float3(0.141238183f, 0.135547534f, 0.5717085f), new float3(-0.17419602f, -0.09319439f, -0.726653457f), new float3(-0.250450522f, 0.0593146533f, -0.726653457f), new float3(-0.117037721f, 0.135538921f, -0.726653457f), new float3(-0.0216945354f, -0.05507237f, -0.726653457f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 5, 0, 3, 6, -1, 1, 4, 7, 2, -1, 2, 7, 6, 3, -1, 4, 1, 0, 5, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0.382545829f, -0.0008175941f, 0.9239361f, -0.575093269f),
					new float4(0f, 0f, -1f, -0.71532f),
					new float4(-0.242116824f, 0.9690506f, 0.04817098f, -0.103142194f),
					new float4(0f, -1f, 0f, -0.08472226f),
					new float4(0.976908743f, 0.08877088f, -0.194342509f, -0.0835343152f),
					new float4(-0.97501564f, -0.10833507f, 0.193927824f, -0.06701678f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(-15.7346544f, 1.60972226f, -42.3087425f, 1f)), name: "Solid 1625757 #1948")
					.WithMesh(new float3[] { new float3(0.04197639f, 0.08684317f, 0.6051353f), new float3(0.05956091f, -0.08472226f, 0.5977028f), new float3(0.200503141f, -0.08472226f, 0.5393472f), new float3(0.182933509f, 0.124960691f, 0.5468073f), new float3(-0.201595575f, -0.08472226f, -0.71532f), new float3(-0.220660552f, 0.08686257f, -0.71532f), new float3(-0.06814989f, 0.124967292f, -0.71532f), new float3(-0.0490955748f, -0.08472226f, -0.71532f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 5, 0, 3, 6, -1, 1, 4, 7, 2, -1, 2, 7, 6, 3, -1, 4, 1, 0, 5, -1 }))
				;
		}

		// bm_c2a5d, Details: the exact CSG made a triangle the mesh does not have; Solid 55362 #1097 face -1 near (-11.834,
		// -4.27, 8.501) (4 brushes)
		static ContentsScene BmC2a5dDetails52Site1()
		{
			return new ContentsScene()
				.WithContentsList("Solid", "Water", "Glass", "Grate")
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 0.8944272f, 0.4472136f, -0.0416780524f),
					new float4(0f, -0.8944272f, -0.4472136f, -0.02652202f),
					new float4(0f, 0f, 1f, -0.6100004f),
					new float4(0f, 0f, -1f, -0.6099996f),
					new float4(-0.707106769f, 0f, -0.707106769f, -0.845752954f),
					new float4(0.924809337f, -0.0006914621f, 0.380430251f, -1.38268685f),
				}, contents: 3, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(-12.8339243f, -4.62159729f, 9.15f, 1f)), name: "Grate 55372 #1076")
					.WithMesh(new float3[] { new float3(1.24398136f, -0.2584027f, 0.6100004f), new float3(1.7462976f, 0.351597279f, -0.6099996f), new float3(-0.5860757f, 0.351597279f, -0.6099996f), new float3(-1.80607569f, -0.2584027f, 0.6100004f), new float3(1.7462405f, 0.2753473f, -0.6099996f), new float3(1.24392438f, -0.3346527f, 0.6100004f), new float3(-1.80607569f, -0.3346527f, 0.6100004f), new float3(-0.5860757f, 0.2753473f, -0.6099996f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 5, 0, 3, 6, -1, 1, 4, 7, 2, -1, 2, 7, 6, 3, -1, 4, 1, 0, 5, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(-0.316243976f, 0.894417346f, 0.316239357f, -0.0416846834f),
					new float4(0.316243529f, -0.894416034f, -0.316243529f, -0.0265162438f),
					new float4(-0.707106769f, 0f, 0.707106769f, -0.609974563f),
					new float4(0.7071092f, -0.000361451355f, -0.7071042f, -0.609856844f),
					new float4(0.380754679f, -4.77274E-05f, 0.9246761f, -1.5878731f),
					new float4(-0.9246772f, 0f, -0.380752057f, -1.2702868f),
				}, contents: 3, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(-10.3653173f, -4.62159729f, 10.1220484f, 1f)), name: "Grate 55373 #1077")
					.WithMesh(new float3[] { new float3(-0.7214706f, 0.3516111f, -1.58412635f), new float3(-1.22468269f, -0.258402735f, -0.362048358f), new float3(0.605320036f, -0.2583933f, 1.4679544f), new float3(1.82740474f, 0.351617873f, 0.964767f), new float3(-1.22468269f, -0.334652722f, -0.362048358f), new float3(-0.721482f, 0.27535367f, -1.58409882f), new float3(1.82737434f, 0.2753473f, 0.964775562f), new float3(0.605317235f, -0.334652722f, 1.46795154f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 1, 4, 7, 2, -1, 5, 0, 3, 6, -1, 6, 3, 2, 7, -1, 0, 5, 4, 1, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 1f, 0f, -0.0296525769f),
					new float4(0f, -1f, 0f, -0.0465974249f),
					new float4(0f, 0f, 1f, -0.0381250754f),
					new float4(0f, 0f, -1f, -0.0381249227f),
					new float4(-0.707106769f, 0f, -0.707106769f, -0.7259399f),
					new float4(0.9246851f, 0f, 0.380732775f, -1.186642f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(-12.3552408f, -4.29965258f, 8.501875f, 1f)), name: "Solid 55362 #1097")
					.WithMesh(new float3[] { new float3(1.26759541f, 0.0296525769f, 0.0381250754f), new float3(1.29899085f, 0.0296525769f, -0.0381249227f), new float3(-0.9885092f, 0.0296525769f, -0.0381249227f), new float3(-1.06475914f, 0.0296525769f, 0.0381250754f), new float3(1.29899085f, -0.0465974249f, -0.0381249227f), new float3(1.26759541f, -0.0465974249f, 0.0381250754f), new float3(-1.06475914f, -0.0465974249f, 0.0381250754f), new float3(-0.9885092f, -0.0465974249f, -0.0381249227f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 5, 0, 3, 6, -1, 1, 4, 7, 2, -1, 2, 7, 6, 3, -1, 4, 1, 0, 5, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 1f, 0f, -0.0296525769f),
					new float4(0f, -1f, 0f, -0.0465974249f),
					new float4(-0.7071095f, 0f, 0.707104f, -0.0423584f),
					new float4(0.7071067f, 0f, -0.7071069f, -0.0338869542f),
					new float4(0.3807647f, 0f, 0.924671948f, -1.680001f),
					new float4(-0.9246432f, 0f, -0.3808345f, -1.68005073f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(-9.782392f, -4.29965258f, 9.782406f, 1f)), name: "Solid 55363 #1098")
					.WithMesh(new float3[] { new float3(-1.272944f, 0.0296525769f, -1.32086706f), new float3(-1.30439639f, 0.0296525769f, -1.24450243f), new float3(1.24448824f, 0.0296525769f, 1.30440187f), new float3(1.32087207f, 0.0296525769f, 1.27294827f), new float3(-1.30439639f, -0.0465974249f, -1.24450243f), new float3(-1.272944f, -0.0465974249f, -1.32086706f), new float3(1.32087207f, -0.0465974249f, 1.27294827f), new float3(1.24448824f, -0.0465974249f, 1.30440187f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 1, 4, 7, 2, -1, 5, 0, 3, 6, -1, 6, 3, 2, 7, -1, 0, 5, 4, 1, -1 }))
				;
		}

		// bm_c2a5d, Details: the mesh has a triangle the exact CSG did not make; Grate 55367 #1072 face -1 near (-7.733,
		// -4.271, 6.1) (3 brushes)
		static ContentsScene BmC2a5dDetails52Site2()
		{
			return new ContentsScene()
				.WithContentsList("Solid", "Water", "Glass", "Grate")
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 0.8944272f, -0.4472136f, -0.04167778f),
					new float4(0f, -0.8944272f, 0.4472136f, -0.0265222918f),
					new float4(0f, 0f, -1f, -0.6099998f),
					new float4(0f, 0f, 1f, -0.610000253f),
					new float4(-0.707106769f, 0f, 0.707106769f, -1.90013f),
					new float4(0.9246676f, 0f, -0.380775362f, -3.10596466f),
				}, contents: 3, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(-11.3428106f, -4.62159729f, 5.49f, 1f)), name: "Grate 55367 #1072")
					.WithMesh(new float3[] { new float3(3.610203f, 0.3515973f, 0.610000253f), new float3(3.10781074f, -0.2584027f, -0.6099998f), new float3(-3.29718947f, -0.2584027f, -0.6099998f), new float3(-2.07718945f, 0.3515973f, 0.610000253f), new float3(3.10781074f, -0.334652722f, -0.6099998f), new float3(3.610203f, 0.2753473f, 0.610000253f), new float3(-2.07718945f, 0.2753473f, 0.610000253f), new float3(-3.29718947f, -0.334652722f, -0.6099998f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 1, 4, 7, 2, -1, 5, 0, 3, 6, -1, 6, 3, 2, 7, -1, 0, 5, 4, 1, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(-0.316246331f, 0.894414067f, -0.316246331f, -0.04169222f),
					new float4(0.3162216f, -0.8944303f, 0.3162252f, -0.026533762f),
					new float4(-0.707106769f, 0f, -0.707106769f, -0.60998863f),
					new float4(0.7071028f, 0f, 0.7071108f, -0.6099823f),
					new float4(-0.9247604f, 0.000421545556f, 0.3805496f, -0.8452118f),
					new float4(0.707106769f, 0f, -0.707106769f, -1.14465117f),
				}, contents: 3, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(-7.335876f, -4.62159729f, 4.84353f, 1f)), name: "Grate 55368 #1073")
					.WithMesh(new float3[] { new float3(1.24071789f, 0.351630777f, -0.3780634f), new float3(0.37806353f, -0.2584027f, -1.24071777f), new float3(-0.8990994f, -0.2584027f, 0.0364451967f), new float3(-0.395911962f, 0.3516242f, 1.25854778f), new float3(0.37806353f, -0.334657848f, -1.24071777f), new float3(1.24071789f, 0.275320321f, -0.3780634f), new float3(-0.3959366f, 0.275320351f, 1.25857246f), new float3(-0.899124f, -0.334652722f, 0.03646982f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 1, 4, 7, 2, -1, 5, 0, 3, 6, -1, 6, 3, 2, 7, -1, 0, 5, 4, 1, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 1f, 0f, -0.0465969853f),
					new float4(0f, -1f, 0f, -0.0296530146f),
					new float4(0f, 0f, -1f, -0.03812494f),
					new float4(0f, 0f, 1f, -0.0381250568f),
					new float4(-0.707106769f, 0f, 0.707106769f, -1.78031325f),
					new float4(0.924600661f, 0f, -0.380937845f, -2.90987945f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(-10.8641319f, -4.316597f, 6.138125f, 1f)), name: "Solid 55360 #1095")
					.WithMesh(new float3[] { new float3(3.16288185f, 0.0465969853f, 0.0381250568f), new float3(3.13146663f, 0.0465969853f, -0.03812494f), new float3(-2.55586815f, 0.0465969853f, -0.03812494f), new float3(-2.479618f, 0.0465969853f, 0.0381250568f), new float3(3.13146663f, -0.0296530146f, -0.03812494f), new float3(3.16288185f, -0.0296530146f, 0.0381250568f), new float3(-2.479618f, -0.0296530146f, 0.0381250568f), new float3(-2.55586815f, -0.0296530146f, -0.03812494f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 1, 4, 7, 2, -1, 5, 0, 3, 6, -1, 6, 3, 2, 7, -1, 0, 5, 4, 1, -1 }))
				;
		}

		// bm_c2a5d, Details: the exact CSG made a triangle the mesh does not have; Grate 55372 #1076 face -1 near (-11.93,
		// -4.462, 8.923) (4 brushes)
		static ContentsScene BmC2a5dDetails52Site3()
		{
			return new ContentsScene()
				.WithContentsList("Solid", "Water", "Glass", "Grate")
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, -1f, 0f, -2.95826173f),
					new float4(-1f, 0f, 0f, -0.07625019f),
					new float4(1f, 0f, 0f, -0.07624981f),
					new float4(0f, 0f, -1f, -0.03494797f),
					new float4(0.164398983f, 0f, 0.9863939f, -0.0250707641f),
					new float4(-0.164398983f, 0f, 0.9863939f, -0.0250708275f),
					new float4(0f, 0f, 1f, -0.0222395323f),
					new float4(0f, 0.707106769f, 0.707106769f, -3.49083781f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(-12.2f, -1.92173827f, 9.66151f, 1f)), name: "Solid 35241 #678")
					.WithMesh(new float3[] { new float3(0.01906231f, -2.95826173f, 0.0222395323f), new float3(-0.019062696f, -2.95826173f, 0.0222395323f), new float3(-0.07625019f, -2.95826173f, 0.012708284f), new float3(-0.07625019f, -2.95826173f, -0.03494797f), new float3(0.07624981f, -2.95826173f, -0.03494797f), new float3(0.07624981f, -2.95826173f, 0.012708283f), new float3(-0.07625019f, 4.924082f, 0.012708284f), new float3(-0.07625019f, 4.97173834f, -0.03494797f), new float3(0.07624981f, 4.97173834f, -0.03494797f), new float3(0.07624981f, 4.924082f, 0.012708283f), new float3(0.01906231f, 4.914551f, 0.0222395323f), new float3(-0.019062696f, 4.914551f, 0.0222395323f) }, new int[] { 0, 1, 2, 3, 4, 5, -1, 6, 7, 3, 2, -1, 8, 9, 5, 4, -1, 7, 8, 4, 3, -1, 9, 10, 0, 5, -1, 11, 6, 2, 1, -1, 10, 11, 1, 0, -1, 11, 10, 9, 8, 7, 6, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 0.8944272f, 0.4472136f, -0.0416780524f),
					new float4(0f, -0.8944272f, -0.4472136f, -0.02652202f),
					new float4(0f, 0f, 1f, -0.6100004f),
					new float4(0f, 0f, -1f, -0.6099996f),
					new float4(-0.707106769f, 0f, -0.707106769f, -0.845752954f),
					new float4(0.924809337f, -0.0006914621f, 0.380430251f, -1.38268685f),
				}, contents: 3, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(-12.8339243f, -4.62159729f, 9.15f, 1f)), name: "Grate 55372 #1076")
					.WithMesh(new float3[] { new float3(1.24398136f, -0.2584027f, 0.6100004f), new float3(1.7462976f, 0.351597279f, -0.6099996f), new float3(-0.5860757f, 0.351597279f, -0.6099996f), new float3(-1.80607569f, -0.2584027f, 0.6100004f), new float3(1.7462405f, 0.2753473f, -0.6099996f), new float3(1.24392438f, -0.3346527f, 0.6100004f), new float3(-1.80607569f, -0.3346527f, 0.6100004f), new float3(-0.5860757f, 0.2753473f, -0.6099996f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 5, 0, 3, 6, -1, 1, 4, 7, 2, -1, 2, 7, 6, 3, -1, 4, 1, 0, 5, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(-0.316243976f, 0.894417346f, 0.316239357f, -0.0416846834f),
					new float4(0.316243529f, -0.894416034f, -0.316243529f, -0.0265162438f),
					new float4(-0.707106769f, 0f, 0.707106769f, -0.609974563f),
					new float4(0.7071092f, -0.000361451355f, -0.7071042f, -0.609856844f),
					new float4(0.380754679f, -4.77274E-05f, 0.9246761f, -1.5878731f),
					new float4(-0.9246772f, 0f, -0.380752057f, -1.2702868f),
				}, contents: 3, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(-10.3653173f, -4.62159729f, 10.1220484f, 1f)), name: "Grate 55373 #1077")
					.WithMesh(new float3[] { new float3(-0.7214706f, 0.3516111f, -1.58412635f), new float3(-1.22468269f, -0.258402735f, -0.362048358f), new float3(0.605320036f, -0.2583933f, 1.4679544f), new float3(1.82740474f, 0.351617873f, 0.964767f), new float3(-1.22468269f, -0.334652722f, -0.362048358f), new float3(-0.721482f, 0.27535367f, -1.58409882f), new float3(1.82737434f, 0.2753473f, 0.964775562f), new float3(0.605317235f, -0.334652722f, 1.46795154f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 1, 4, 7, 2, -1, 5, 0, 3, 6, -1, 6, 3, 2, 7, -1, 0, 5, 4, 1, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 1f, 0f, -0.0296525769f),
					new float4(0f, -1f, 0f, -0.0465974249f),
					new float4(0f, 0f, 1f, -0.0381250754f),
					new float4(0f, 0f, -1f, -0.0381249227f),
					new float4(-0.707106769f, 0f, -0.707106769f, -0.7259399f),
					new float4(0.9246851f, 0f, 0.380732775f, -1.186642f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(-12.3552408f, -4.29965258f, 8.501875f, 1f)), name: "Solid 55362 #1097")
					.WithMesh(new float3[] { new float3(1.26759541f, 0.0296525769f, 0.0381250754f), new float3(1.29899085f, 0.0296525769f, -0.0381249227f), new float3(-0.9885092f, 0.0296525769f, -0.0381249227f), new float3(-1.06475914f, 0.0296525769f, 0.0381250754f), new float3(1.29899085f, -0.0465974249f, -0.0381249227f), new float3(1.26759541f, -0.0465974249f, 0.0381250754f), new float3(-1.06475914f, -0.0465974249f, 0.0381250754f), new float3(-0.9885092f, -0.0465974249f, -0.0381249227f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 5, 0, 3, 6, -1, 1, 4, 7, 2, -1, 2, 7, 6, 3, -1, 4, 1, 0, 5, -1 }))
				;
		}

		// bm_c2a5e, Details: the exact CSG made a triangle the mesh does not have; Solid 1068700 #2897 face -1 near (-23.72,
		// -57.832, -11.094) (3 brushes)
		static ContentsScene BmC2a5eDetails44Site1()
		{
			return new ContentsScene()
				.WithContentsList("Solid", "Water", "Glass", "Grate")
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, -1f, 0f, -0.0518942252f),
					new float4(0f, 0f, -1f, -0.228707656f),
					new float4(0.000172582164f, 0f, 1f, -0.228718132f),
					new float4(-1f, 0f, 0f, -0.430028677f),
					new float4(0f, 1f, 0f, -0.0815432742f),
					new float4(1f, 0f, 0f, -0.6755963f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(-23.6268463f, -58.0506058f, -10.8656673f, 1f)), name: "Solid 1068696 #2893")
					.WithMesh(new float3[] { new float3(0.6755963f, -0.0518942252f, -0.228707656f), new float3(0.6755963f, -0.0518942252f, 0.22860153f), new float3(-0.430028677f, -0.0518942252f, 0.22879234f), new float3(-0.430028677f, -0.0518942252f, -0.228707656f), new float3(0.6755963f, 0.0815432742f, -0.228707656f), new float3(-0.430028677f, 0.0815432742f, -0.228707656f), new float3(0.6755963f, 0.0815432742f, 0.22860153f), new float3(-0.430028677f, 0.0815432742f, 0.22879234f) }, new int[] { 0, 1, 2, 3, -1, 4, 0, 3, 5, -1, 1, 6, 7, 2, -1, 7, 5, 3, 2, -1, 6, 4, 5, 7, -1, 4, 6, 1, 0, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 1f, 0f, -0.3261818f),
					new float4(0f, -1f, 0f, -0.207568213f),
					new float4(-0.00125121977f, 0f, -0.9999992f, -0.03807172f),
					new float4(0f, 0f, 1f, -0.0380722433f),
					new float4(-1f, 0f, 0f, -0.06777817f),
					new float4(1f, 0f, 0f, -0.08472183f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(-23.70316f, -57.89493f, -11.1324472f, 1f)), name: "Solid 1068697 #2894")
					.WithMesh(new float3[] { new float3(0.08472183f, 0.3261818f, 0.0380722433f), new float3(0.08472183f, 0.3261818f, -0.03817776f), new float3(-0.06777817f, 0.3261818f, -0.0379869454f), new float3(-0.06777817f, 0.3261818f, 0.0380722433f), new float3(0.08472183f, -0.207568213f, -0.03817776f), new float3(0.08472183f, -0.207568213f, 0.0380722433f), new float3(-0.06777817f, -0.207568213f, 0.0380722433f), new float3(-0.06777817f, -0.207568213f, -0.0379869454f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 1, 4, 7, 2, -1, 5, 0, 3, 6, -1, 6, 3, 2, 7, -1, 0, 5, 4, 1, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(-0.000294404861f, 0f, -0.99999994f, -0.228654936f),
					new float4(0f, 0f, 1f, -0.228728443f),
					new float4(-1f, 0f, 0f, -0.252048254f),
					new float4(0f, 1f, 0f, -0.174739376f),
					new float4(0f, -1f, 0f, -0.11119812f),
					new float4(1f, 0f, 0f, -0.396076739f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(-23.8048267f, -57.8578644f, -10.8656034f, 1f)), name: "Solid 1068700 #2897")
					.WithMesh(new float3[] { new float3(0.396076739f, 0.174739376f, -0.228771552f), new float3(0.396076739f, -0.11119812f, -0.228771552f), new float3(-0.252048254f, -0.11119812f, -0.228580743f), new float3(-0.252048254f, 0.174739376f, -0.228580743f), new float3(0.396076739f, -0.11119812f, 0.228728443f), new float3(0.396076739f, 0.174739376f, 0.228728443f), new float3(-0.252048254f, 0.174739376f, 0.228728443f), new float3(-0.252048254f, -0.11119812f, 0.228728443f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 2, 7, 6, 3, -1, 5, 0, 3, 6, -1, 1, 4, 7, 2, -1, 4, 1, 0, 5, -1 }))
				;
		}

		// bm_c2a5e, Details: the exact CSG made a triangle the mesh does not have; Solid 1068714 #2911 face -1 near (-23.72,
		// -57.639, -11.094) (3 brushes)
		static ContentsScene BmC2a5eDetails44Site2()
		{
			return new ContentsScene()
				.WithContentsList("Solid", "Water", "Glass", "Grate")
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 1f, 0f, -0.0148262028f),
					new float4(0f, 0f, -1f, -0.228707656f),
					new float4(0.000172582164f, 0f, 1f, -0.228707537f),
					new float4(1f, 0f, 0f, -0.6142369f),
					new float4(-1f, 0f, 0f, -0.491388083f),
					new float4(0f, -1f, 0f, -0.0232987981f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(-23.5654869f, -57.5835762f, -10.8656673f, 1f)), name: "Solid 1068695 #2892")
					.WithMesh(new float3[] { new float3(-0.491388083f, 0.0148262028f, -0.228707656f), new float3(-0.491388083f, 0.0148262028f, 0.22879234f), new float3(0.6142369f, 0.0148262028f, 0.22860153f), new float3(0.6142369f, 0.0148262028f, -0.228707656f), new float3(0.6142369f, -0.0232987981f, -0.228707656f), new float3(-0.491388083f, -0.0232987981f, -0.228707656f), new float3(-0.491388083f, -0.0232987981f, 0.22879234f), new float3(0.6142369f, -0.0232987981f, 0.22860153f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 0, 3, -1, 6, 7, 2, 1, -1, 7, 4, 3, 2, -1, 5, 6, 1, 0, -1, 6, 5, 4, 7, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 1f, 0f, -0.3261818f),
					new float4(0f, -1f, 0f, -0.207568213f),
					new float4(-0.00125121977f, 0f, -0.9999992f, -0.03807172f),
					new float4(0f, 0f, 1f, -0.0380722433f),
					new float4(-1f, 0f, 0f, -0.06777817f),
					new float4(1f, 0f, 0f, -0.08472183f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(-23.70316f, -57.89493f, -11.1324472f, 1f)), name: "Solid 1068697 #2894")
					.WithMesh(new float3[] { new float3(0.08472183f, 0.3261818f, 0.0380722433f), new float3(0.08472183f, 0.3261818f, -0.03817776f), new float3(-0.06777817f, 0.3261818f, -0.0379869454f), new float3(-0.06777817f, 0.3261818f, 0.0380722433f), new float3(0.08472183f, -0.207568213f, -0.03817776f), new float3(0.08472183f, -0.207568213f, 0.0380722433f), new float3(-0.06777817f, -0.207568213f, 0.0380722433f), new float3(-0.06777817f, -0.207568213f, -0.0379869454f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 1, 4, 7, 2, -1, 5, 0, 3, 6, -1, 6, 3, 2, 7, -1, 0, 5, 4, 1, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(-0.000294404861f, 0f, -0.99999994f, -0.228654951f),
					new float4(0f, 0f, 1f, -0.228728443f),
					new float4(-1f, 0f, 0f, -0.2521131f),
					new float4(0f, 1f, 0f, -0.0465979f),
					new float4(0f, -1f, 0f, -0.0296521f),
					new float4(1f, 0f, 0f, -0.3960119f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(-23.8047619f, -57.6534729f, -10.8656034f, 1f)), name: "Solid 1068714 #2911")
					.WithMesh(new float3[] { new float3(0.3960119f, 0.0465979f, -0.228771552f), new float3(0.3960119f, -0.0296521f, -0.228771552f), new float3(-0.2521131f, -0.0296521f, -0.228580743f), new float3(-0.2521131f, 0.0465979f, -0.228580743f), new float3(0.3960119f, -0.0296521f, 0.228728443f), new float3(0.3960119f, 0.0465979f, 0.228728443f), new float3(-0.2521131f, 0.0465979f, 0.228728443f), new float3(-0.2521131f, -0.0296521f, 0.228728443f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 2, 7, 6, 3, -1, 5, 0, 3, 6, -1, 1, 4, 7, 2, -1, 4, 1, 0, 5, -1 }))
				;
		}

		// bm_c2a5e, Details: the exact CSG made a triangle the mesh does not have; Solid 1224778 #3092 face -1 near (-19.221,
		// -57.658, -10.256) (3 brushes)
		static ContentsScene BmC2a5eDetails44Site3()
		{
			return new ContentsScene()
				.WithContentsList("Solid", "Water", "Glass", "Grate")
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 1f, 0f, -0.0148257446f),
					new float4(0f, 0f, -1f, -0.228728518f),
					new float4(0.000172582164f, 0f, 1f, -0.228686675f),
					new float4(1f, 0f, 0f, -0.614236236f),
					new float4(-1f, 0f, 0f, -0.491388768f),
					new float4(0f, -1f, 0f, -0.0232992545f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(-19.0667362f, -57.60264f, -10.0268965f, 1f)), name: "Solid 1224759 #3073")
					.WithMesh(new float3[] { new float3(-0.491388768f, 0.0148257446f, -0.228728518f), new float3(-0.491388768f, 0.0148257446f, 0.228771478f), new float3(0.614236236f, 0.0148257446f, 0.228580669f), new float3(0.614236236f, 0.0148257446f, -0.228728518f), new float3(0.614236236f, -0.0232992545f, -0.228728518f), new float3(-0.491388768f, -0.0232992545f, -0.228728518f), new float3(-0.491388768f, -0.0232992545f, 0.228771478f), new float3(0.614236236f, -0.0232992545f, 0.228580669f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 0, 3, -1, 6, 7, 2, 1, -1, 7, 4, 3, 2, -1, 5, 6, 1, 0, -1, 6, 5, 4, 7, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 1f, 0f, -0.326181322f),
					new float4(0f, -1f, 0f, -0.20756866f),
					new float4(-0.00125121977f, 0f, -0.9999992f, -0.0380716063f),
					new float4(0f, 0f, 1f, -0.03807236f),
					new float4(-1f, 0f, 0f, -0.06777695f),
					new float4(1f, 0f, 0f, -0.0847230554f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(-19.20441f, -57.9139938f, -10.2936974f, 1f)), name: "Solid 1224761 #3075")
					.WithMesh(new float3[] { new float3(0.0847230554f, 0.326181322f, 0.03807236f), new float3(0.0847230554f, 0.326181322f, -0.038177643f), new float3(-0.06777695f, 0.326181322f, -0.0379868336f), new float3(-0.06777695f, 0.326181322f, 0.03807236f), new float3(0.0847230554f, -0.20756866f, -0.038177643f), new float3(0.0847230554f, -0.20756866f, 0.03807236f), new float3(-0.06777695f, -0.20756866f, 0.03807236f), new float3(-0.06777695f, -0.20756866f, -0.0379868336f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 1, 4, 7, 2, -1, 5, 0, 3, 6, -1, 6, 3, 2, 7, -1, 0, 5, 4, 1, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(-0.000294404861f, 0f, -0.99999994f, -0.228654817f),
					new float4(0f, 0f, 1f, -0.228728563f),
					new float4(-1f, 0f, 0f, -0.252048939f),
					new float4(0f, 1f, 0f, -0.0465974435f),
					new float4(0f, -1f, 0f, -0.0296525583f),
					new float4(1f, 0f, 0f, -0.396076053f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(-19.306076f, -57.6725349f, -10.0268536f, 1f)), name: "Solid 1224778 #3092")
					.WithMesh(new float3[] { new float3(0.396076053f, 0.0465974435f, -0.228771433f), new float3(0.396076053f, -0.0296525583f, -0.228771433f), new float3(-0.252048939f, -0.0296525583f, -0.228580624f), new float3(-0.252048939f, 0.0465974435f, -0.228580624f), new float3(0.396076053f, -0.0296525583f, 0.228728563f), new float3(0.396076053f, 0.0465974435f, 0.228728563f), new float3(-0.252048939f, 0.0465974435f, 0.228728563f), new float3(-0.252048939f, -0.0296525583f, 0.228728563f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 2, 7, 6, 3, -1, 5, 0, 3, 6, -1, 1, 4, 7, 2, -1, 4, 1, 0, 5, -1 }))
				;
		}

		// bm_c2a5e, Details: the exact CSG made a triangle the mesh does not have; Solid 1224764 #3078 face -1 near (-19.221,
		// -57.852, -10.256) (3 brushes)
		static ContentsScene BmC2a5eDetails44Site4()
		{
			return new ContentsScene()
				.WithContentsList("Solid", "Water", "Glass", "Grate")
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, -1f, 0f, -0.05189087f),
					new float4(0f, 0f, -1f, -0.228739008f),
					new float4(0.000172582164f, 0f, 1f, -0.22868678f),
					new float4(-1f, 0f, 0f, -0.4299645f),
					new float4(0f, 1f, 0f, -0.0815466344f),
					new float4(1f, 0f, 0f, -0.6756605f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(-19.12816f, -58.06967f, -10.026886f, 1f)), name: "Solid 1224760 #3074")
					.WithMesh(new float3[] { new float3(0.6756605f, -0.05189087f, -0.228739008f), new float3(0.6756605f, -0.05189087f, 0.228570178f), new float3(-0.4299645f, -0.05189087f, 0.228760988f), new float3(-0.4299645f, -0.05189087f, -0.228739008f), new float3(0.6756605f, 0.0815466344f, -0.228739008f), new float3(-0.4299645f, 0.0815466344f, -0.228739008f), new float3(0.6756605f, 0.0815466344f, 0.228570178f), new float3(-0.4299645f, 0.0815466344f, 0.228760988f) }, new int[] { 0, 1, 2, 3, -1, 4, 0, 3, 5, -1, 1, 6, 7, 2, -1, 7, 5, 3, 2, -1, 6, 4, 5, 7, -1, 4, 6, 1, 0, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 1f, 0f, -0.326181322f),
					new float4(0f, -1f, 0f, -0.20756866f),
					new float4(-0.00125121977f, 0f, -0.9999992f, -0.0380716063f),
					new float4(0f, 0f, 1f, -0.03807236f),
					new float4(-1f, 0f, 0f, -0.06777695f),
					new float4(1f, 0f, 0f, -0.0847230554f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(-19.20441f, -57.9139938f, -10.2936974f, 1f)), name: "Solid 1224761 #3075")
					.WithMesh(new float3[] { new float3(0.0847230554f, 0.326181322f, 0.03807236f), new float3(0.0847230554f, 0.326181322f, -0.038177643f), new float3(-0.06777695f, 0.326181322f, -0.0379868336f), new float3(-0.06777695f, 0.326181322f, 0.03807236f), new float3(0.0847230554f, -0.20756866f, -0.038177643f), new float3(0.0847230554f, -0.20756866f, 0.03807236f), new float3(-0.06777695f, -0.20756866f, 0.03807236f), new float3(-0.06777695f, -0.20756866f, -0.0379868336f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 1, 4, 7, 2, -1, 5, 0, 3, 6, -1, 6, 3, 2, 7, -1, 0, 5, 4, 1, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(-0.000294404861f, 0f, -0.99999994f, -0.228654817f),
					new float4(0f, 0f, 1f, -0.228728563f),
					new float4(-1f, 0f, 0f, -0.252048939f),
					new float4(0f, 1f, 0f, -0.174738929f),
					new float4(0f, -1f, 0f, -0.111198574f),
					new float4(1f, 0f, 0f, -0.396076053f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(-19.306076f, -57.8769264f, -10.0268536f, 1f)), name: "Solid 1224764 #3078")
					.WithMesh(new float3[] { new float3(0.396076053f, 0.174738929f, -0.228771433f), new float3(0.396076053f, -0.111198574f, -0.228771433f), new float3(-0.252048939f, -0.111198574f, -0.228580624f), new float3(-0.252048939f, 0.174738929f, -0.228580624f), new float3(0.396076053f, -0.111198574f, 0.228728563f), new float3(0.396076053f, 0.174738929f, 0.228728563f), new float3(-0.252048939f, 0.174738929f, 0.228728563f), new float3(-0.252048939f, -0.111198574f, 0.228728563f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 2, 7, 6, 3, -1, 5, 0, 3, 6, -1, 1, 4, 7, 2, -1, 4, 1, 0, 5, -1 }))
				;
		}

		// bm_c2a5f, Details: the exact CSG made a triangle the mesh does not have; Solid 2518076 #4472 face -1 near (-3.895,
		// -23.672, -85.591) (3 brushes)
		static ContentsScene BmC2a5fDetails105Site1()
		{
			return new ContentsScene()
				.WithContentsList("Solid", "Water", "Glass", "Grate")
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, -1f, 0f, -0.0518921651f),
					new float4(0f, 0f, -1f, -0.228739932f),
					new float4(0.000168372848f, 0f, 1f, -0.228687674f),
					new float4(-1f, 0f, 0f, -0.429965317f),
					new float4(0f, 1f, 0f, -0.08154533f),
					new float4(1f, 0f, 0f, -0.675659657f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(-3.80190969f, -23.8906078f, -85.3618851f, 1f)), name: "Solid 2518072 #4468")
					.WithMesh(new float3[] { new float3(0.675659657f, -0.0518921651f, -0.228739932f), new float3(0.675659657f, -0.0518921651f, 0.228573918f), new float3(-0.429965317f, -0.0518921651f, 0.228760064f), new float3(-0.429965317f, -0.0518921651f, -0.228739932f), new float3(0.675659657f, 0.08154533f, -0.228739932f), new float3(-0.429965317f, 0.08154533f, -0.228739932f), new float3(0.675659657f, 0.08154533f, 0.228573918f), new float3(-0.429965317f, 0.08154533f, 0.228760064f) }, new int[] { 0, 1, 2, 3, -1, 4, 0, 3, 5, -1, 1, 6, 7, 2, -1, 7, 5, 3, 2, -1, 6, 4, 5, 7, -1, 4, 6, 1, 0, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 1f, 0f, -0.326180041f),
					new float4(0f, -1f, 0f, -0.207569957f),
					new float4(-0.00122070219f, 0f, -0.9999993f, -0.0380712934f),
					new float4(0f, 0f, 1f, -0.0380752571f),
					new float4(-1f, 0f, 0f, -0.06777774f),
					new float4(1f, 0f, 0f, -0.08472226f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(-3.87815976f, -23.73493f, -85.6287f, 1f)), name: "Solid 2518073 #4469")
					.WithMesh(new float3[] { new float3(0.08472226f, 0.326180041f, 0.0380752571f), new float3(0.08472226f, 0.326180041f, -0.03817474f), new float3(-0.06777774f, 0.326180041f, -0.0379885845f), new float3(-0.06777774f, 0.326180041f, 0.0380752571f), new float3(0.08472226f, -0.207569957f, -0.03817474f), new float3(0.08472226f, -0.207569957f, 0.0380752571f), new float3(-0.06777774f, -0.207569957f, 0.0380752571f), new float3(-0.06777774f, -0.207569957f, -0.0379885845f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 1, 4, 7, 2, -1, 5, 0, 3, 6, -1, 6, 3, 2, 7, -1, 0, 5, 4, 1, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(-0.000287224248f, 0f, -0.99999994f, -0.228656679f),
					new float4(0f, 0f, 1f, -0.228729546f),
					new float4(-1f, 0f, 0f, -0.252048552f),
					new float4(0f, 1f, 0f, -0.17473954f),
					new float4(0f, -1f, 0f, -0.111197971f),
					new float4(1f, 0f, 0f, -0.396076441f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(-3.97982645f, -23.6978645f, -85.3618546f, 1f)), name: "Solid 2518076 #4472")
					.WithMesh(new float3[] { new float3(0.396076441f, 0.17473954f, -0.22877045f), new float3(0.396076441f, -0.111197971f, -0.22877045f), new float3(-0.252048552f, -0.111197971f, -0.2285843f), new float3(-0.252048552f, 0.17473954f, -0.2285843f), new float3(0.396076441f, -0.111197971f, 0.228729546f), new float3(0.396076441f, 0.17473954f, 0.228729546f), new float3(-0.252048552f, 0.17473954f, 0.228729546f), new float3(-0.252048552f, -0.111197971f, 0.228729546f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 2, 7, 6, 3, -1, 5, 0, 3, 6, -1, 1, 4, 7, 2, -1, 4, 1, 0, 5, -1 }))
				;
		}

		// bm_c2a5f, Details: the exact CSG made a triangle the mesh does not have; Solid 2518090 #4486 face -1 near (-3.895,
		// -23.479, -85.591) (3 brushes)
		static ContentsScene BmC2a5fDetails105Site2()
		{
			return new ContentsScene()
				.WithContentsList("Solid", "Water", "Glass", "Grate")
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 1f, 0f, -0.0148263546f),
					new float4(0f, 0f, -1f, -0.2287323f),
					new float4(0.000168372848f, 0f, 1f, -0.228684962f),
					new float4(1f, 0f, 0f, -0.6142361f),
					new float4(-1f, 0f, 0f, -0.491388857f),
					new float4(0f, -1f, 0f, -0.0232986454f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(-3.74048615f, -23.4235764f, -85.36189f, 1f)), name: "Solid 2518071 #4467")
					.WithMesh(new float3[] { new float3(-0.491388857f, 0.0148263546f, -0.2287323f), new float3(-0.491388857f, 0.0148263546f, 0.2287677f), new float3(0.6142361f, 0.0148263546f, 0.228581548f), new float3(0.6142361f, 0.0148263546f, -0.2287323f), new float3(0.6142361f, -0.0232986454f, -0.2287323f), new float3(-0.491388857f, -0.0232986454f, -0.2287323f), new float3(-0.491388857f, -0.0232986454f, 0.2287677f), new float3(0.6142361f, -0.0232986454f, 0.228581548f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 0, 3, -1, 6, 7, 2, 1, -1, 7, 4, 3, 2, -1, 5, 6, 1, 0, -1, 6, 5, 4, 7, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 1f, 0f, -0.326180041f),
					new float4(0f, -1f, 0f, -0.207569957f),
					new float4(-0.00122070219f, 0f, -0.9999993f, -0.0380712934f),
					new float4(0f, 0f, 1f, -0.0380752571f),
					new float4(-1f, 0f, 0f, -0.06777774f),
					new float4(1f, 0f, 0f, -0.08472226f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(-3.87815976f, -23.73493f, -85.6287f, 1f)), name: "Solid 2518073 #4469")
					.WithMesh(new float3[] { new float3(0.08472226f, 0.326180041f, 0.0380752571f), new float3(0.08472226f, 0.326180041f, -0.03817474f), new float3(-0.06777774f, 0.326180041f, -0.0379885845f), new float3(-0.06777774f, 0.326180041f, 0.0380752571f), new float3(0.08472226f, -0.207569957f, -0.03817474f), new float3(0.08472226f, -0.207569957f, 0.0380752571f), new float3(-0.06777774f, -0.207569957f, 0.0380752571f), new float3(-0.06777774f, -0.207569957f, -0.0379885845f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 1, 4, 7, 2, -1, 5, 0, 3, 6, -1, 6, 3, 2, 7, -1, 0, 5, 4, 1, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(-0.000287224248f, 0f, -0.99999994f, -0.228656679f),
					new float4(0f, 0f, 1f, -0.228729546f),
					new float4(-1f, 0f, 0f, -0.252048552f),
					new float4(0f, 1f, 0f, -0.0465980545f),
					new float4(0f, -1f, 0f, -0.0296519473f),
					new float4(1f, 0f, 0f, -0.396076441f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(-3.97982645f, -23.4934731f, -85.3618546f, 1f)), name: "Solid 2518090 #4486")
					.WithMesh(new float3[] { new float3(0.396076441f, 0.0465980545f, -0.22877045f), new float3(0.396076441f, -0.0296519473f, -0.22877045f), new float3(-0.252048552f, -0.0296519473f, -0.2285843f), new float3(-0.252048552f, 0.0465980545f, -0.2285843f), new float3(0.396076441f, -0.0296519473f, 0.228729546f), new float3(0.396076441f, 0.0465980545f, 0.228729546f), new float3(-0.252048552f, 0.0465980545f, 0.228729546f), new float3(-0.252048552f, -0.0296519473f, 0.228729546f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 2, 7, 6, 3, -1, 5, 0, 3, 6, -1, 1, 4, 7, 2, -1, 4, 1, 0, 5, -1 }))
				;
		}

		// bm_c2a5g, Model: the mesh has a triangle the exact CSG did not make; Solid 1311817 #327 face -1 near (21.96, 3.253,
		// -98.82) (5 brushes)
		static ContentsScene BmC2a5gModel0Site1()
		{
			return new ContentsScene()
				.WithContentsList("Solid", "Water", "Glass", "Grate")
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 1f, 0f, -1.18611121f),
					new float4(0f, -1f, 0f, -1.86388874f),
					new float4(1f, 0f, 0f, -2.16888857f),
					new float4(-1f, 0f, 0f, -2.71111155f),
					new float4(0f, 0f, 1f, -0.6100003f),
					new float4(0f, 0f, -1f, -0.6099997f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(22.2311115f, 4.303889f, -99.43f, 1f)), name: "Solid 1311817 #327")
					.WithMesh(new float3[] { new float3(2.16888857f, 1.18611121f, -0.6099997f), new float3(-2.71111155f, 1.18611121f, -0.6099997f), new float3(-2.71111155f, 1.18611121f, 0.6100003f), new float3(2.16888857f, 1.18611121f, 0.6100003f), new float3(-2.71111155f, -1.86388874f, -0.6099997f), new float3(2.16888857f, -1.86388874f, -0.6099997f), new float3(2.16888857f, -1.86388874f, 0.6100003f), new float3(-2.71111155f, -1.86388874f, 0.6100003f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 5, 0, 3, 6, -1, 1, 4, 7, 2, -1, 2, 7, 6, 3, -1, 4, 1, 0, 5, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, -1f, 0f, -0.3049999f),
					new float4(0f, 0f, 1f, -0.610000134f),
					new float4(0f, 0f, -1f, -0.609999835f),
					new float4(0f, 1f, 0f, -0.305000126f),
					new float4(-1f, 0f, 0f, -3.25333357f),
					new float4(1f, 0f, 0f, -4.066666f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(27.6533337f, 2.745f, -99.43038f, 1f)), name: "Solid 1311828 #337")
					.WithMesh(new float3[] { new float3(4.066666f, -0.3049999f, -0.609999835f), new float3(4.066666f, -0.3049999f, 0.610000134f), new float3(-3.25333357f, -0.3049999f, 0.610000134f), new float3(-3.25333357f, -0.3049999f, -0.609999835f), new float3(4.066666f, 0.305000126f, 0.610000134f), new float3(-3.25333357f, 0.305000126f, 0.610000134f), new float3(4.066666f, 0.305000126f, -0.609999835f), new float3(-3.25333357f, 0.305000126f, -0.609999835f) }, new int[] { 0, 1, 2, 3, -1, 1, 4, 5, 2, -1, 6, 0, 3, 7, -1, 4, 6, 7, 5, -1, 5, 7, 3, 2, -1, 6, 4, 1, 0, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, -1f, 0f, -0.304999948f),
					new float4(0f, 0f, 1f, -0.610000134f),
					new float4(0f, 0f, -1f, -0.609999835f),
					new float4(0f, 1f, 0f, -0.305000067f),
					new float4(1f, 0f, 0f, -4.40555525f),
					new float4(-1f, 0f, 0f, -3.52444482f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(15.1144447f, 5.185f, -99.43038f, 1f)), name: "Solid 3308987 #1024")
					.WithMesh(new float3[] { new float3(-3.52444482f, -0.304999948f, 0.610000134f), new float3(-3.52444482f, -0.304999948f, -0.609999835f), new float3(4.40555525f, -0.304999948f, -0.609999835f), new float3(4.40555525f, -0.304999948f, 0.610000134f), new float3(-3.52444482f, 0.305000067f, 0.610000134f), new float3(4.40555525f, 0.305000067f, 0.610000134f), new float3(-3.52444482f, 0.305000067f, -0.609999835f), new float3(4.40555525f, 0.305000067f, -0.609999835f) }, new int[] { 0, 1, 2, 3, -1, 4, 0, 3, 5, -1, 1, 6, 7, 2, -1, 6, 4, 5, 7, -1, 7, 5, 3, 2, -1, 4, 6, 1, 0, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, -1f, 0f, -0.3049999f),
					new float4(0f, 0f, 1f, -0.610000134f),
					new float4(0f, 0f, -1f, -0.609999835f),
					new float4(0f, 1f, 0f, -0.305000126f),
					new float4(1f, 0f, 0f, -4.40555525f),
					new float4(-1f, 0f, 0f, -3.52444482f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(15.1144447f, 2.745f, -99.43038f, 1f)), name: "Solid 3308988 #1025")
					.WithMesh(new float3[] { new float3(-3.52444482f, -0.3049999f, 0.610000134f), new float3(-3.52444482f, -0.3049999f, -0.609999835f), new float3(4.40555525f, -0.3049999f, -0.609999835f), new float3(4.40555525f, -0.3049999f, 0.610000134f), new float3(-3.52444482f, 0.305000126f, 0.610000134f), new float3(4.40555525f, 0.305000126f, 0.610000134f), new float3(-3.52444482f, 0.305000126f, -0.609999835f), new float3(4.40555525f, 0.305000126f, -0.609999835f) }, new int[] { 0, 1, 2, 3, -1, 4, 0, 3, 5, -1, 1, 6, 7, 2, -1, 6, 4, 5, 7, -1, 7, 5, 3, 2, -1, 4, 6, 1, 0, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 1f, 0f, -0.7116669f),
					new float4(0f, -1f, 0f, -1.1183331f),
					new float4(0f, 0f, -1f, -0.6099997f),
					new float4(0f, 0f, 1f, -0.6100003f),
					new float4(-1f, 0f, 0f, -17.2155571f),
					new float4(0.8944272f, 0f, -0.4472136f, -19.2475758f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(39.1755562f, 6.608333f, -99.43f, 1f)), name: "Solid 3309074 #1057")
					.WithMesh(new float3[] { new float3(21.8244438f, 0.7116669f, 0.6100003f), new float3(21.2144451f, 0.7116669f, -0.6099997f), new float3(-17.2155571f, 0.7116669f, -0.6099997f), new float3(-17.2155571f, 0.7116669f, 0.6100003f), new float3(21.2144451f, -1.1183331f, -0.6099997f), new float3(21.8244438f, -1.1183331f, 0.6100003f), new float3(-17.2155571f, -1.1183331f, 0.6100003f), new float3(-17.2155571f, -1.1183331f, -0.6099997f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 1, 4, 7, 2, -1, 5, 0, 3, 6, -1, 6, 3, 2, 7, -1, 0, 5, 4, 1, -1 }))
				;
		}

		// bm_c2a5g, Model: the mesh has a triangle the exact CSG did not make; Solid 1311817 #327 face -1 near (23.587, 4.88,
		// -98.82) (4 brushes)
		static ContentsScene BmC2a5gModel0Site2()
		{
			return new ContentsScene()
				.WithContentsList("Solid", "Water", "Glass", "Grate")
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 1f, 0f, -1.18611121f),
					new float4(0f, -1f, 0f, -1.86388874f),
					new float4(1f, 0f, 0f, -2.16888857f),
					new float4(-1f, 0f, 0f, -2.71111155f),
					new float4(0f, 0f, 1f, -0.6100003f),
					new float4(0f, 0f, -1f, -0.6099997f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(22.2311115f, 4.303889f, -99.43f, 1f)), name: "Solid 1311817 #327")
					.WithMesh(new float3[] { new float3(2.16888857f, 1.18611121f, -0.6099997f), new float3(-2.71111155f, 1.18611121f, -0.6099997f), new float3(-2.71111155f, 1.18611121f, 0.6100003f), new float3(2.16888857f, 1.18611121f, 0.6100003f), new float3(-2.71111155f, -1.86388874f, -0.6099997f), new float3(2.16888857f, -1.86388874f, -0.6099997f), new float3(2.16888857f, -1.86388874f, 0.6100003f), new float3(-2.71111155f, -1.86388874f, 0.6100003f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 5, 0, 3, 6, -1, 1, 4, 7, 2, -1, 2, 7, 6, 3, -1, 4, 1, 0, 5, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, -1f, 0f, -0.3049999f),
					new float4(0f, 0f, 1f, -0.610000134f),
					new float4(0f, 0f, -1f, -0.609999835f),
					new float4(0f, 1f, 0f, -0.305000126f),
					new float4(-1f, 0f, 0f, -3.25333357f),
					new float4(1f, 0f, 0f, -4.066666f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(27.6533337f, 2.745f, -99.43038f, 1f)), name: "Solid 1311828 #337")
					.WithMesh(new float3[] { new float3(4.066666f, -0.3049999f, -0.609999835f), new float3(4.066666f, -0.3049999f, 0.610000134f), new float3(-3.25333357f, -0.3049999f, 0.610000134f), new float3(-3.25333357f, -0.3049999f, -0.609999835f), new float3(4.066666f, 0.305000126f, 0.610000134f), new float3(-3.25333357f, 0.305000126f, 0.610000134f), new float3(4.066666f, 0.305000126f, -0.609999835f), new float3(-3.25333357f, 0.305000126f, -0.609999835f) }, new int[] { 0, 1, 2, 3, -1, 1, 4, 5, 2, -1, 6, 0, 3, 7, -1, 4, 6, 7, 5, -1, 5, 7, 3, 2, -1, 6, 4, 1, 0, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, -1f, 0f, -0.304999948f),
					new float4(0f, 0f, 1f, -0.610000134f),
					new float4(0f, 0f, -1f, -0.609999835f),
					new float4(0f, 1f, 0f, -0.305000067f),
					new float4(-1f, 0f, 0f, -3.25333357f),
					new float4(1f, 0f, 0f, -4.066666f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(27.6533337f, 5.185f, -99.43038f, 1f)), name: "Solid 1311830 #339")
					.WithMesh(new float3[] { new float3(4.066666f, -0.304999948f, -0.609999835f), new float3(4.066666f, -0.304999948f, 0.610000134f), new float3(-3.25333357f, -0.304999948f, 0.610000134f), new float3(-3.25333357f, -0.304999948f, -0.609999835f), new float3(4.066666f, 0.305000067f, 0.610000134f), new float3(-3.25333357f, 0.305000067f, 0.610000134f), new float3(4.066666f, 0.305000067f, -0.609999835f), new float3(-3.25333357f, 0.305000067f, -0.609999835f) }, new int[] { 0, 1, 2, 3, -1, 1, 4, 5, 2, -1, 6, 0, 3, 7, -1, 4, 6, 7, 5, -1, 5, 7, 3, 2, -1, 6, 4, 1, 0, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 1f, 0f, -0.7116669f),
					new float4(0f, -1f, 0f, -1.1183331f),
					new float4(0f, 0f, -1f, -0.6099997f),
					new float4(0f, 0f, 1f, -0.6100003f),
					new float4(-0.8944272f, 0f, -0.4472136f, -12.4881907f),
					new float4(1f, 0f, 0f, -17.4527779f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(4.507222f, 6.608333f, -99.43f, 1f)), name: "Solid 1311850 #346")
					.WithMesh(new float3[] { new float3(17.4527779f, 0.7116669f, 0.6100003f), new float3(17.4527779f, 0.7116669f, -0.6099997f), new float3(-13.6572218f, 0.7116669f, -0.6099997f), new float3(-14.2672224f, 0.7116669f, 0.6100003f), new float3(17.4527779f, -1.1183331f, -0.6099997f), new float3(17.4527779f, -1.1183331f, 0.6100003f), new float3(-14.2672224f, -1.1183331f, 0.6100003f), new float3(-13.6572218f, -1.1183331f, -0.6099997f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 1, 4, 7, 2, -1, 5, 0, 3, 6, -1, 6, 3, 2, 7, -1, 0, 5, 4, 1, -1 }))
				;
		}

		// bm_c2a5g, func_breakable 4760975 g_vent_brush_03: the exact CSG made a triangle the mesh does not have; Solid
		// 4760980 #4855 face -1 near (-56.732, 6.101, -14.055) (3 brushes)
		static ContentsScene BmC2a5gFuncBreakable4760975GVentBrush03106Site1()
		{
			return new ContentsScene()
				.WithContentsList("Solid", "Water", "Glass", "Grate")
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 1f, 0f, -0.0148136523f),
					new float4(0f, -0.119558983f, -0.9928271f, -0.0748893544f),
					new float4(-0.707284749f, 0.7069288f, 0f, -0.36852482f),
					new float4(0.7076403f, 0.7065729f, 0f, -0.46165216f),
					new float4(-1.62760425E-05f, -1.626785E-05f, 1f, -0.11802958f),
					new float4(0f, -1f, 0f, -0.0233113486f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(-57.40566f, 6.12426424f, -14.1089239f, 1f)), name: "Solid 4760977 #4852")
					.WithMesh(new float3[] { new float3(-0.5062355f, 0.0148136523f, 0.118021578f), new float3(0.637591243f, 0.0148136523f, 0.1180402f), new float3(0.637591243f, 0.0148136523f, -0.07721431f), new float3(-0.5062355f, 0.0148136523f, -0.07721431f), new float3(0.6756587f, -0.0233113486f, -0.07262319f), new float3(-0.544341266f, -0.0233113486f, -0.07262319f), new float3(-0.544341266f, -0.0233113486f, 0.118020341f), new float3(0.6756587f, -0.0233113486f, 0.1180402f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 3, 2, -1, 5, 6, 0, 3, -1, 7, 4, 2, 1, -1, 6, 7, 1, 0, -1, 7, 6, 5, 4, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 0f, -1f, -0.5718749f),
					new float4(0f, 0f, 1f, -0.5718751f),
					new float4(0f, 1f, 0f, -0.0233317669f),
					new float4(0f, -1f, 0f, -0.014850826f),
					new float4(1f, 0f, 0f, -0.67777586f),
					new float4(-1f, 0f, 0f, -0.5422241f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(-57.4077759f, 6.11580372f, -13.42f, 1f)), name: "Solid 4760979 #4854")
					.WithMesh(new float3[] { new float3(-0.5422241f, -0.014850826f, -0.5718749f), new float3(-0.5422241f, 0.0233317669f, -0.5718749f), new float3(0.67777586f, 0.0233317669f, -0.5718749f), new float3(0.67777586f, -0.014850826f, -0.5718749f), new float3(-0.5422241f, 0.0233317669f, 0.5718751f), new float3(-0.5422241f, -0.014850826f, 0.5718751f), new float3(0.67777586f, -0.014850826f, 0.5718751f), new float3(0.67777586f, 0.0233317669f, 0.5718751f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 1, 4, 7, 2, -1, 5, 0, 3, 6, -1, 6, 3, 2, 7, -1, 0, 5, 4, 1, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(1f, 0f, 0f, -0.0190612022f),
					new float4(-1f, 0f, 0f, -0.0190637968f),
					new float4(-0.707106769f, 0.707106769f, 0f, -0.512211442f),
					new float4(-0.707106769f, -0.707106769f, 0f, -0.3235022f),
					new float4(-0.00210833712f, -0.124172352f, -0.9922584f, -0.138725609f),
					new float4(-0.00051879877f, 0f, 0.9999999f, -0.110404223f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(-56.7500153f, 6.5765624f, -14.1013165f, 1f)), name: "Solid 4760980 #4855")
					.WithMesh(new float3[] { new float3(0.0190612022f, 0.7434376f, 0.110414125f), new float3(0.0190612022f, -0.47656244f, 0.110414125f), new float3(0.0190612022f, -0.47656244f, -0.08021088f), new float3(0.0190612022f, 0.7434376f, -0.232883066f), new float3(-0.0190637968f, -0.438437432f, 0.110394344f), new float3(-0.0190637968f, 0.7053126f, 0.110394344f), new float3(-0.0190637968f, 0.7053126f, -0.228031054f), new float3(-0.0190637968f, -0.438437432f, -0.08490088f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 5, 0, 3, 6, -1, 1, 4, 7, 2, -1, 2, 7, 6, 3, -1, 4, 1, 0, 5, -1 }))
				;
		}

		// bm_c2a5g, Details: the exact CSG made a triangle the mesh does not have; Solid 35303 #2427 face -1 near (-48.151,
		// -0.457, 13.112) (6 brushes)
		static ContentsScene BmC2a5gDetails65Site1()
		{
			return new ContentsScene()
				.WithContentsList("Solid", "Water", "Glass", "Grate")
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 1f, 0f, -0.474444449f),
					new float4(0f, -1f, 0f, -0.7455556f),
					new float4(-1f, -7.211747E-08f, 7.82012939E-05f, -0.0423596f),
					new float4(1f, 2.1718936E-08f, -6.951226E-05f, -0.0338903777f),
					new float4(-0.992277861f, 0f, -0.124034733f, -0.6476759f),
					new float4(-0.992277861f, -8.71788E-05f, 0.124034733f, -0.646926761f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(-48.14726f, -0.474444449f, 7.9298f, 1f)), name: "Solid 35303 #2427")
					.WithMesh(new float3[] { new float3(-0.0419779979f, 0.474444449f, 4.8802f), new float3(0.0342720039f, 0.474444449f, 5.4902f), new float3(0.03350876f, 0.474444449f, -5.4898f), new float3(-0.0427412428f, 0.474444449f, -4.8798f), new float3(0.03427197f, -0.7455556f, 5.489342f), new float3(-0.0419779755f, -0.7455556f, 4.87934256f), new float3(-0.0427411534f, -0.7455556f, -4.879801f), new float3(0.0335087851f, -0.7455556f, -5.4898f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 5, 0, 3, 6, -1, 1, 4, 7, 2, -1, 2, 7, 6, 3, -1, 4, 1, 0, 5, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 1f, 0f, -0.488f),
					new float4(0f, -1f, 0f, -0.732f),
					new float4(-0.9969396f, 2.44538569E-05f, -0.078176f, -0.06559403f),
					new float4(-0.9662841f, -9.25725E-05f, 0.257478267f, -0.216014966f),
					new float4(1f, 0f, 0f, -0.0508335866f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(-48.24007f, -0.488f, 11.7805271f, 1f)), name: "Solid 35325 #2449")
					.WithMesh(new float3[] { new float3(0.0508335866f, 0.488f, 1.02991152f), new float3(0.0508335866f, 0.488f, -1.48715866f), new float3(-0.101666413f, 0.488f, 0.457597882f), new float3(0.0508335866f, -0.732f, -1.48754025f), new float3(0.0508335866f, -0.732f, 1.02947283f), new float3(-0.101662956f, -0.732f, 0.457172215f) }, new int[] { 0, 1, 2, -1, 3, 4, 5, -1, 1, 3, 5, 2, -1, 4, 0, 2, 5, -1, 0, 4, 3, 1, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 1f, 0f, -0.488f),
					new float4(0f, -1f, 0f, -0.732f),
					new float4(0.9969384f, 0f, 0.07819124f, -0.116261013f),
					new float4(-0.9486833f, 0f, -0.316227764f, -0.156729981f),
					new float4(-0.882352948f, 0f, 0.470588237f, -0.3498526f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(-48.39257f, -0.488f, 11.399375f, 1f)), name: "Solid 35326 #2450")
					.WithMesh(new float3[] { new float3(-0.254166245f, 0.488f, 0.266875029f), new float3(0.05083374f, 0.488f, 0.83875f), new float3(0.203333721f, 0.488f, -1.10562491f), new float3(0.05083374f, -0.732f, 0.83875f), new float3(-0.254166245f, -0.732f, 0.266875029f), new float3(0.203333721f, -0.732f, -1.10562491f) }, new int[] { 0, 1, 2, -1, 3, 4, 5, -1, 1, 3, 5, 2, -1, 4, 0, 2, 5, -1, 0, 4, 3, 1, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, -1f, 0f, -0.09319445f),
					new float4(0f, 1f, 0f, -0.0593055561f),
					new float4(0f, 0f, 1f, -0.262638956f),
					new float4(-0.229039326f, 0f, -0.973417163f, -0.261963874f),
					new float4(0.992277861f, 0f, -0.124034733f, -0.3730507f),
					new float4(-0.996545732f, 0f, 0.08304548f, -0.298318833f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(-48.52177f, -0.0593055561f, 13.157361f, 1f)), name: "Solid 35328 #2452")
					.WithMesh(new float3[] { new float3(-0.2774663f, -0.09319445f, 0.262638956f), new float3(-0.3155913f, -0.09319445f, -0.19486101f), new float3(0.332533717f, -0.09319445f, -0.347361028f), new float3(0.408783734f, -0.09319445f, 0.262638956f), new float3(-0.3155913f, 0.0593055561f, -0.19486101f), new float3(-0.2774663f, 0.0593055561f, 0.262638956f), new float3(0.408783734f, 0.0593055561f, 0.262638956f), new float3(0.332533717f, 0.0593055561f, -0.347361028f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 5, 0, 3, 6, -1, 1, 4, 7, 2, -1, 2, 7, 6, 3, -1, 4, 1, 0, 5, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, -1f, 0f, -0.7455556f),
					new float4(0f, 1f, 0f, -0.474444449f),
					new float4(0.229039326f, 0f, 0.973417163f, -0.2648748f),
					new float4(-0.3807498f, 0f, -0.9246781f, -0.2640541f),
					new float4(0.9662349f, 0f, -0.257662654f, -0.377928257f),
					new float4(-0.9486833f, 0f, 0.316227764f, -0.302743524f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(-48.6255569f, -0.474444449f, 12.6405554f, 1f)), name: "Solid 35329 #2453")
					.WithMesh(new float3[] { new float3(-0.211804822f, -0.7455556f, 0.321944624f), new float3(-0.3643048f, -0.7455556f, -0.1355554f), new float3(0.283820182f, -0.7455556f, -0.4024304f), new float3(0.4363202f, -0.7455556f, 0.169444621f), new float3(-0.3643048f, 0.474444449f, -0.1355554f), new float3(-0.211804822f, 0.474444449f, 0.321944624f), new float3(0.4363202f, 0.474444449f, 0.169444621f), new float3(0.283820182f, 0.474444449f, -0.4024304f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 5, 0, 3, 6, -1, 1, 4, 7, 2, -1, 2, 7, 6, 3, -1, 4, 1, 0, 5, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 1f, 0f, -0.474444449f),
					new float4(0f, -1f, 0f, -0.7455556f),
					new float4(-1f, 0f, 0f, -2.32986021f),
					new float4(1f, 0f, 0f, -1.86388981f),
					new float4(0f, 0f, 1f, -5.49000025f),
					new float4(0f, 0f, -1f, -5.49f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(-45.78389f, -0.474444449f, 7.93f, 1f)), name: "Solid 35567 #2456")
					.WithMesh(new float3[] { new float3(1.86388981f, 0.474444449f, -5.49f), new float3(-2.32986021f, 0.474444449f, -5.49f), new float3(-2.32986021f, 0.474444449f, 5.49000025f), new float3(1.86388981f, 0.474444449f, 5.49000025f), new float3(-2.32986021f, -0.7455556f, -5.49f), new float3(1.86388981f, -0.7455556f, -5.49f), new float3(1.86388981f, -0.7455556f, 5.49000025f), new float3(-2.32986021f, -0.7455556f, 5.49000025f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 1, 4, 7, 2, -1, 5, 0, 3, 6, -1, 6, 3, 2, 7, -1, 0, 5, 4, 1, -1 }))
				;
		}

		// bm_c2a5g, Details: the mesh has a triangle the exact CSG did not make; Solid 1872515 #3745 face -1 near (-37.819,
		// 10.812, -18.299) (5 brushes)
		static ContentsScene BmC2a5gDetails65Site2()
		{
			return new ContentsScene()
				.WithContentsList("Solid", "Water", "Glass", "Grate")
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 1f, 0f, -0.122000195f),
					new float4(0f, -1f, 0f, -0.1829998f),
					new float4(0.7731922f, -0.000960293633f, 0.634171f, -0.6706568f),
					new float4(-0.9356569f, 0.000232639562f, 0.352910936f, -0.2777294f),
					new float4(0.707055f, 6.48055263E-08f, -0.707158566f, -0.319064081f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(-37.0695229f, 10.858f, -17.0977173f, 1f)), name: "Solid 1872512 #3742")
					.WithMesh(new float3[] { new float3(0.6799736f, 0.122000195f, 0.22868228f), new float3(-0.7497139f, 0.122000195f, -1.20079577f), new float3(0.06997359f, 0.122000195f, 0.9724048f), new float3(-0.7498356f, -0.1829998f, -1.20091748f), new float3(0.679765463f, -0.1829998f, 0.22847417f), new float3(0.069802314f, -0.1829998f, 0.972151756f) }, new int[] { 0, 1, 2, -1, 3, 4, 5, -1, 4, 0, 2, 5, -1, 1, 3, 5, 2, -1, 3, 1, 0, 4, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 1f, 0f, -0.122000195f),
					new float4(0f, -1f, 0f, -0.1829998f),
					new float4(0.4789211f, 0.000135528637f, 0.8778579f, -0.586845338f),
					new float4(-0.35282582f, -0.0003385207f, 0.935689f, -0.241306841f),
					new float4(0.119371668f, 7.468015E-05f, -0.992849648f, -0.184757173f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(-36.09127f, 10.858f, -17.90474f, 1f)), name: "Solid 1872514 #3744")
					.WithMesh(new float3[] { new float3(1.283527f, 0.122000195f, -0.0317583866f), new float3(-1.72872972f, 0.122000195f, -0.3939261f), new float3(0.4449679f, 0.122000195f, 0.425723f), new float3(-1.72838938f, -0.1829998f, -0.393908143f), new float3(1.28363216f, -0.1829998f, -0.03176868f), new float3(0.445138544f, -0.1829998f, 0.425676972f) }, new int[] { 0, 1, 2, -1, 3, 4, 5, -1, 4, 0, 2, 5, -1, 1, 3, 5, 2, -1, 3, 1, 0, 4, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 1f, 0f, -0.122000195f),
					new float4(0f, -1f, 0f, -0.1829998f),
					new float4(0.6343397f, -0.0003024825f, 0.773054361f, -0.6707756f),
					new float4(-0.707106054f, 0.00141613267f, 0.707106054f, -0.3190535f),
					new float4(0.352907628f, -0.0004155586f, -0.9356581f, -0.277742773f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(-36.6184769f, 10.858f, -17.5489483f, 1f)), name: "Solid 1872515 #3745")
					.WithMesh(new float3[] { new float3(0.972365141f, 0.122000195f, 0.069856286f), new float3(-1.20075989f, 0.122000195f, -0.749793947f), new float3(0.228927583f, 0.122000195f, 0.6798935f), new float3(-1.20152318f, -0.1829998f, -0.749946356f), new float3(0.9721524f, -0.1829998f, 0.0699115f), new float3(0.228526518f, -0.1829998f, 0.680103242f) }, new int[] { 0, 1, 2, -1, 3, 4, 5, -1, 4, 0, 2, 5, -1, 1, 3, 5, 2, -1, 3, 1, 0, 4, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 1f, 0f, -0.122000195f),
					new float4(0f, -1f, 0f, -0.1829998f),
					new float4(0.8778545f, -2.92314089E-05f, 0.4789274f, -0.586782038f),
					new float4(-0.992846f, 0f, 0.119401917f, -0.18479f),
					new float4(0.935649753f, 0.0002867662f, -0.352929771f, -0.241361529f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(-37.42547f, 10.858f, -16.5703869f, 1f)), name: "Solid 1872517 #3747")
					.WithMesh(new float3[] { new float3(0.425728351f, 0.122000195f, 0.444865f), new float3(-0.393959135f, 0.122000195f, -1.728203f), new float3(-0.0317716263f, 0.122000195f, 1.28344393f), new float3(-0.394002885f, -0.1829998f, -1.72856688f), new float3(0.4257795f, -0.1829998f, 0.444752663f), new float3(-0.0317734629f, -0.1829998f, 1.28342867f) }, new int[] { 0, 1, 2, -1, 3, 4, 5, -1, 4, 0, 2, 5, -1, 1, 3, 5, 2, -1, 3, 1, 0, 4, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 1f, 0f, -0.122000195f),
					new float4(0f, -1f, 0f, -0.1829998f),
					new float4(0.9599889f, -0.000455078378f, 0.2800377f, -0.397018969f),
					new float4(-0.9997055f, -2.87906987E-05f, 0.0242678747f, -0.0963351354f),
					new float4(0.992845237f, 0.00035073873f, -0.119407862f, -0.124750614f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(-37.6669273f, 10.858f, -15.9856138f, 1f)), name: "Solid 1872518 #3748")
					.WithMesh(new float3[] { new float3(0.209687263f, 0.122000195f, 0.6991096f), new float3(-0.152500227f, 0.122000195f, -2.312385f), new float3(-0.05718773f, 0.122000195f, 1.61397576f), new float3(-0.152516469f, -0.1829998f, -2.313416f), new float3(0.209721357f, -0.1829998f, 0.6984971f), new float3(-0.0571907274f, -0.1829998f, 1.61349046f) }, new int[] { 0, 1, 2, -1, 3, 4, 5, -1, 4, 0, 2, 5, -1, 1, 3, 5, 2, -1, 3, 1, 0, 4, -1 }))
				;
		}

		// bm_c2a5g, Details: the mesh has a triangle the exact CSG did not make; Solid 1871609 #3543 face -1 near (-35.38,
		// -0.293, -18.3) (7 brushes)
		static ContentsScene BmC2a5gDetails65Site3()
		{
			return new ContentsScene()
				.WithContentsList("Solid", "Water", "Glass", "Grate")
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 1f, 0f, -0.122f),
					new float4(0f, -1f, 0f, -0.183f),
					new float4(0.279999971f, -0.000350341783f, 0.9599999f, -0.397021919f),
					new float4(-0.11937809f, 7.468417E-05f, 0.9928489f, -0.124731481f),
					new float4(0.0242623426f, 0f, -0.9997056f, -0.09633495f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(-33.0665245f, -0.122f, -18.1475f, 1f)), name: "Solid 1871608 #3542")
					.WithMesh(new float3[] { new float3(1.6141628f, 0.122f, -0.057188414f), new float3(-2.313094f, 0.122f, -0.152500927f), new float3(0.6991628f, 0.122f, 0.209686577f), new float3(-2.313333f, -0.183f, -0.152506724f), new float3(1.61381054f, -0.183f, -0.0571969636f), new float3(0.698836863f, -0.183f, 0.209670335f) }, new int[] { 0, 1, 2, -1, 3, 4, 5, -1, 4, 0, 2, 5, -1, 1, 3, 5, 2, -1, 3, 1, 0, 4, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 1f, 0f, -0.122f),
					new float4(0f, -1f, 0f, -0.183f),
					new float4(0.478852123f, 0f, 0.8778956f, -0.586820662f),
					new float4(-0.352867424f, 0.000220757516f, 0.9356733f, -0.241355568f),
					new float4(0.11937809f, -7.468417E-05f, -0.9928489f, -0.184782058f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(-33.65108f, -0.122f, -17.9060421f, 1f)), name: "Solid 1871609 #3543")
					.WithMesh(new float3[] { new float3(1.28371942f, 0.122f, -0.031770397f), new float3(-1.72853732f, 0.122f, -0.3939579f), new float3(0.444969356f, 0.122f, 0.4257296f), new float3(-1.72872818f, -0.183f, -0.3939579f), new float3(1.283685f, -0.183f, -0.0317515954f), new float3(0.444891363f, -0.183f, 0.42577216f) }, new int[] { 0, 1, 2, -1, 3, 4, 5, -1, 4, 0, 2, 5, -1, 1, 3, 5, 2, -1, 3, 1, 0, 4, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 1f, 0f, -0.122f),
					new float4(0f, -1f, 0f, -0.183f),
					new float4(0.634316564f, 0.000396835f, 0.7730733f, -0.670741f),
					new float4(-0.707106769f, 0f, 0.707106769f, -0.31902793f),
					new float4(0.3529217f, 0f, -0.935652852f, -0.2776976f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(-34.178463f, -0.122f, -17.550209f, 1f)), name: "Solid 1871610 #3544")
					.WithMesh(new float3[] { new float3(0.9721604f, 0.122f, 0.06989655f), new float3(-1.20096457f, 0.122f, -0.749790967f), new float3(0.228722915f, 0.122f, 0.679896533f), new float3(-1.20096457f, -0.183f, -0.749790967f), new float3(0.9722911f, -0.183f, 0.06994586f), new float3(0.22880891f, -0.183f, 0.679982543f) }, new int[] { 0, 1, 2, -1, 3, 4, 5, -1, 4, 0, 2, 5, -1, 1, 3, 5, 2, -1, 3, 1, 0, 4, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 1f, 0f, -0.122f),
					new float4(0f, -1f, 0f, -0.183f),
					new float4(0.099484f, -0.000124476443f, 0.995039165f, -0.161834329f),
					new float4(0f, 0f, -1f, -0.03177147f),
					new float4(-0.02426352f, 1.51795093E-05f, 0.9997056f, -0.0394704752f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(-32.4435234f, -0.122f, -18.2682285f, 1f)), name: "Solid 1871611 #3545")
					.WithMesh(new float3[] { new float3(-2.93571329f, 0.122f, -0.03177147f), new float3(0.991352439f, 0.122f, 0.06354103f), new float3(1.94466829f, 0.122f, -0.03177147f), new float3(0.9910081f, -0.183f, 0.0635373f), new float3(-2.93590426f, -0.183f, -0.03177147f), new float3(1.9442867f, -0.183f, -0.03177147f) }, new int[] { 0, 1, 2, -1, 3, 4, 5, -1, 1, 3, 5, 2, -1, 4, 0, 2, 5, -1, 0, 4, 3, 1, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 1f, 0f, -0.122f),
					new float4(0f, -1f, 0f, -0.183f),
					new float4(0.8778956f, 0f, 0.478852123f, -0.5867357f),
					new float4(-0.9928471f, 0f, 0.119393006f, -0.1847889f),
					new float4(0.935652852f, 0f, -0.3529217f, -0.241358608f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(-34.98547f, -0.122f, -16.5716667f, 1f)), name: "Solid 1871612 #3546")
					.WithMesh(new float3[] { new float3(0.425729752f, 0.122f, 0.4447917f), new float3(-0.393957734f, 0.122f, -1.72833323f), new float3(-0.03177024f, 0.122f, 1.28354168f), new float3(-0.393957734f, -0.183f, -1.72833323f), new float3(0.425729752f, -0.183f, 0.4447917f), new float3(-0.03177024f, -0.183f, 1.28354168f) }, new int[] { 0, 1, 2, -1, 3, 4, 5, -1, 4, 0, 2, 5, -1, 1, 3, 5, 2, -1, 3, 1, 0, 4, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 1f, 0f, -0.122f),
					new float4(0f, -1f, 0f, -0.183f),
					new float4(0.9599984f, -0.0002275128f, 0.280005246f, -0.397014916f),
					new float4(-0.999705553f, -1.82355161E-05f, 0.02426564f, -0.09633316f),
					new float4(0.9928464f, 0.000239120753f, -0.119398229f, -0.124750659f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(-35.22693f, -0.122f, -15.9869423f, 1f)), name: "Solid 1871613 #3547")
					.WithMesh(new float3[] { new float3(0.209688649f, 0.122f, 0.699065149f), new float3(-0.152498856f, 0.122f, -2.31267619f), new float3(-0.057186354f, 0.122f, 1.61404645f), new float3(-0.152510464f, -0.183f, -2.31338334f), new float3(0.209719554f, -0.183f, 0.698711336f), new float3(-0.05718677f, -0.183f, 1.6138f) }, new int[] { 0, 1, 2, -1, 3, 4, 5, -1, 4, 0, 2, 5, -1, 1, 3, 5, 2, -1, 3, 1, 0, 4, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 1f, 0f, -0.177916661f),
					new float4(0f, -1f, 0f, -0.279583335f),
					new float4(0f, 0f, -1f, -0.0762508363f),
					new float4(-0.707106769f, 0f, 0.707106769f, -0.371425539f),
					new float4(0.707106769f, 0f, 0.707106769f, -0.383409768f),
					new float4(0f, 0f, 1f, -0.07624916f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(-35.9984741f, -0.0254166666f, -18.22375f, 1f)), name: "Solid 1870829 #4748")
					.WithMesh(new float3[] { new float3(-0.449025869f, 0.177916661f, 0.07624916f), new float3(0.465974152f, 0.177916661f, 0.07624916f), new float3(0.6184741f, 0.177916661f, -0.0762508363f), new float3(-0.6015259f, 0.177916661f, -0.0762508363f), new float3(0.465974152f, -0.279583335f, 0.07624916f), new float3(-0.449025869f, -0.279583335f, 0.07624916f), new float3(-0.6015259f, -0.279583335f, -0.0762508363f), new float3(0.6184741f, -0.279583335f, -0.0762508363f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 2, 7, 6, 3, -1, 5, 0, 3, 6, -1, 1, 4, 7, 2, -1, 4, 1, 0, 5, -1 }))
				;
		}

		// bm_c2a5h, Details: the exact CSG made a triangle the mesh does not have; Solid 14258 #1858 face -1 near (16.47,
		// 24.095, -56.425) (5 brushes)
		static ContentsScene BmC2a5hDetails53Site1()
		{
			return new ContentsScene()
				.WithContentsList("Solid", "Water", "Glass", "Grate")
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0.0459594727f, 0.992713749f, 0.111387342f, -0.448392659f),
					new float4(0.147673786f, 0.9222294f, 0.357330859f, -0.326862037f),
					new float4(0.22734575f, 0.804053f, 0.5493748f, -0.27655378f),
					new float4(0.306732684f, 0.5966718f, 0.741551f, -0.286540926f),
					new float4(0.352942169f, 0.38683778f, 0.8519321f, -0.353744239f),
					new float4(0.3796043f, 0.120555207f, 0.9172606f, -0.491458863f),
					new float4(-0.3814174f, -7.389629E-05f, -0.924402952f, -0.640945733f),
					new float4(0.09283151f, -0.9701348f, 0.224099845f, -0.810284257f),
					new float4(0.7074814f, 0.00268843747f, -0.706726849f, -0.300843954f),
					new float4(-0.9807196f, 0.000399595418f, 0.195419818f, -0.250203818f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(17.8098316f, 24.7855129f, -57.1014252f, 1f)), name: "Solid 14256 #1856")
					.WithMesh(new float3[] { new float3(-0.3632134f, 0.5294871f, -0.543538868f), new float3(-0.300561f, 0.49129796f, -0.229038164f), new float3(0.04982711f, 0.491334468f, -0.373936951f), new float3(-0.19080174f, 0.5294871f, -0.614677548f), new float3(-0.242036611f, 0.368028283f, 0.06492021f), new float3(0.274697065f, 0.368286f, -0.149294972f), new float3(-0.19080174f, 0.177594066f, 0.322433233f), new float3(0.4726611f, 0.177403152f, 0.04815428f), new float3(-0.152375445f, -0.0824889243f, 0.515808463f), new float3(0.6203602f, -0.08106118f, 0.195027843f), new float3(-0.128162161f, -0.373503417f, 0.637918532f), new float3(0.7153382f, -0.374650419f, 0.288990378f), new float3(-0.120611921f, -0.690508962f, 0.6764578f), new float3(0.745519757f, -0.690432f, 0.3180029f), new float3(-0.363765121f, -0.995512962f, -0.543189347f), new float3(-0.186612055f, -0.995446146f, -0.6162844f) }, new int[] { 0, 1, 2, 3, -1, 1, 4, 5, 2, -1, 4, 6, 7, 5, -1, 6, 8, 9, 7, -1, 8, 10, 11, 9, -1, 10, 12, 13, 11, -1, 14, 0, 3, 15, -1, 12, 14, 15, 13, -1, 13, 15, 3, 2, 5, 7, 9, 11, -1, 14, 12, 10, 8, 6, 4, 1, 0, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 0.992689967f, 0.120692149f, -0.448655963f),
					new float4(0.0001579461f, 0.9223066f, 0.386458963f, -0.327067375f),
					new float4(5.47892269E-05f, 0.8031934f, 0.5957183f, -0.276773572f),
					new float4(2.09871032E-05f, 0.5953152f, 0.803492248f, -0.286957145f),
					new float4(0.0001011695f, 0.3874579f, 0.9218874f, -0.352941841f),
					new float4(3.13825E-05f, 0.12018837f, 0.9927511f, -0.4913521f),
					new float4(0f, 0f, -1f, -0.641582f),
					new float4(0f, -0.9701425f, 0.242535621f, -0.8097203f),
					new float4(0.980537832f, 0.0011212161f, -0.19632706f, -1.80398774f),
					new float4(-0.98107177f, 0.0008901212f, -0.193642318f, -1.56050062f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(15.7349672f, 24.7850361f, -57.003418f, 1f)), name: "Solid 14258 #1858")
					.WithMesh(new float3[] { new float3(-1.46349275f, 0.5299639f, -0.641582f), new float3(-1.52573931f, 0.491642833f, -0.326391846f), new float3(1.77349985f, 0.491873771f, -0.328291446f), new float3(1.710728f, 0.5299639f, -0.641582f), new float3(-1.5840404f, 0.3681235f, -0.03158204f), new float3(1.83326936f, 0.367081046f, -0.0304908175f), new float3(-1.63504541f, 0.177116886f, 0.2259525f), new float3(1.88487136f, 0.176735252f, 0.226143315f), new float3(-1.67297125f, -0.08062296f, 0.416915447f), new float3(1.92320371f, -0.07968504f, 0.4161266f), new float3(-1.69758964f, -0.374171436f, 0.540293f), new float3(1.94840264f, -0.375123441f, 0.540293f), new float3(-1.70542336f, -0.690008f, 0.578530252f), new float3(1.95639622f, -0.6900361f, 0.5784179f), new float3(-1.46487641f, -0.995036066f, -0.641582f), new float3(1.71247184f, -0.995036066f, -0.641582f) }, new int[] { 0, 1, 2, 3, -1, 1, 4, 5, 2, -1, 4, 6, 7, 5, -1, 6, 8, 9, 7, -1, 8, 10, 11, 9, -1, 10, 12, 13, 11, -1, 14, 0, 3, 15, -1, 12, 14, 15, 13, -1, 13, 15, 3, 2, 5, 7, 9, 11, -1, 14, 12, 10, 8, 6, 4, 1, 0, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(-0.04627103f, 0.9926942f, 0.111432821f, -0.5095721f),
					new float4(-0.147391587f, 0.9223473f, 0.357143044f, -0.383350134f),
					new float4(-0.2277508f, 0.8030014f, 0.5507434f, -0.3251662f),
					new float4(-0.307498366f, 0.595005f, 0.7425724f, -0.3225905f),
					new float4(-0.352616221f, 0.3860171f, 0.8524392f, -0.3758799f),
					new float4(-0.380025953f, 0.120600924f, 0.91708f, -0.4971139f),
					new float4(0.3836945f, 5.31925361E-05f, -0.923460066f, -0.6429364f),
					new float4(-0.09318861f, -0.9700584f, 0.224282488f, -0.7494263f),
					new float4(-0.701369345f, -0.002097682f, -0.71279496f, -0.210308239f),
					new float4(0.98056823f, -0.000453803164f, 0.19617781f, -0.341125369f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(13.8234138f, 24.7235413f, -57.1350327f, 1f)), name: "Solid 14261 #1861")
					.WithMesh(new float3[] { new float3(0.387328625f, 0.5533635f, -0.195871651f), new float3(0.450033277f, 0.591458738f, -0.5092041f), new float3(0.285396785f, 0.5914635f, -0.57761f), new float3(0.04201942f, 0.5532248f, -0.338021278f), new float3(0.328032136f, 0.4292353f, 0.100226767f), new float3(-0.1862806f, 0.42961818f, -0.113016985f), new float3(0.276234537f, 0.237278372f, 0.35868603f), new float3(-0.3859523f, 0.237848461f, 0.084018454f), new float3(0.238271192f, -0.0184186157f, 0.5478491f), new float3(-0.532847345f, -0.0193995945f, 0.229315922f), new float3(0.213315189f, -0.315160334f, 0.671901941f), new float3(-0.627436638f, -0.313246757f, 0.323253781f), new float3(0.20557636f, -0.6281835f, 0.7098593f), new float3(-0.6563716f, -0.627968848f, 0.352651149f), new float3(0.4493979f, -0.9335413f, -0.509555936f), new float3(0.288666427f, -0.9335413f, -0.5763393f) }, new int[] { 0, 1, 2, 3, -1, 4, 0, 3, 5, -1, 6, 4, 5, 7, -1, 8, 6, 7, 9, -1, 10, 8, 9, 11, -1, 12, 10, 11, 13, -1, 1, 14, 15, 2, -1, 14, 12, 13, 15, -1, 15, 13, 11, 9, 7, 5, 3, 2, -1, 12, 14, 1, 0, 4, 6, 8, 10, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(-0.383095f, 0f, -0.923709f, -0.610036f),
					new float4(0.382512748f, 0f, 0.9239502f, -0.610009968f),
					new float4(0.7070927f, 0f, -0.7071209f, -0.287673026f),
					new float4(-0.9805866f, 0f, 0.19608663f, -0.244378522f),
					new float4(0f, 1f, 0f, -2.05027843f),
					new float4(0f, -1f, 0f, -1.30472159f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(17.7978516f, 22.0447216f, -57.1300278f, 1f)), name: "Solid 14295 #1895")
					.WithMesh(new float3[] { new float3(-0.179259166f, -1.30472159f, -0.586075f), new float3(-0.352080524f, -1.30472159f, -0.5143998f), new float3(-0.352080524f, 2.05027843f, -0.5143998f), new float3(-0.179259166f, 2.05027843f, -0.586075f), new float3(-0.108233176f, -1.30472159f, 0.705027759f), new float3(0.754649758f, -1.30472159f, 0.347796679f), new float3(0.754649758f, 2.05027843f, 0.347796679f), new float3(-0.108233176f, 2.05027843f, 0.705027759f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 5, 0, 3, 6, -1, 1, 4, 7, 2, -1, 2, 7, 6, 3, -1, 4, 1, 0, 5, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0.383528978f, 0f, -0.92352885f, -0.6100229f),
					new float4(-0.3828462f, 0f, 0.9238121f, -0.609959543f),
					new float4(-0.707128346f, 0f, -0.7070852f, -0.230071768f),
					new float4(0.980601132f, 0f, 0.196013764f, -0.3053018f),
					new float4(0f, 1f, 0f, -2.05027843f),
					new float4(0f, -1f, 0f, -1.30472159f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(13.8639793f, 22.0447216f, -57.1539078f, 1f)), name: "Solid 14301 #1901")
					.WithMesh(new float3[] { new float3(0.409392148f, -1.30472159f, -0.490519762f), new float3(0.236799955f, -1.30472159f, -0.562194943f), new float3(0.236799955f, 2.05027843f, -0.562194943f), new float3(0.409392148f, 2.05027843f, -0.490519762f), new float3(-0.6968239f, -1.30472159f, 0.3714859f), new float3(0.165639043f, -1.30472159f, 0.728907764f), new float3(0.165639043f, 2.05027843f, 0.728907764f), new float3(-0.6968239f, 2.05027843f, 0.3714859f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 1, 4, 7, 2, -1, 5, 0, 3, 6, -1, 6, 3, 2, 7, -1, 0, 5, 4, 1, -1 }))
				;
		}

		// bm_c2a5h, Details: the exact CSG made a triangle the mesh does not have; Solid 231681 #1597 face -1 near (-10.256,
		// 13.425, -25.291) (2 brushes)
		static ContentsScene BmC2a5hDetails53Site2()
		{
			return new ContentsScene()
				.WithContentsList("Solid", "Water", "Glass", "Grate")
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 0f, 1f, -0.0171987154f),
					new float4(1f, 0f, 0f, -0.190624774f),
					new float4(-1f, 0f, 0f, -0.190625235f),
					new float4(0f, 0f, -1f, -0.0214987174f),
					new float4(0f, 0.707457662f, -0.706755757f, -0.269170433f),
					new float4(3.4535522E-05f, -0.7072875f, -0.706926f, -0.171796665f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(-10.2556248f, 13.0274515f, -25.30837f, 1f)), name: "Solid 231680 #1596")
					.WithMesh(new float3[] { new float3(-0.190625235f, -0.260094345f, 0.0171987154f), new float3(0.190624774f, -0.260075718f, 0.0171987154f), new float3(0.190624774f, 0.397657335f, 0.0171987154f), new float3(-0.190625235f, 0.397657335f, 0.0171987154f), new float3(0.190624774f, -0.221398056f, -0.0214987174f), new float3(0.190624774f, 0.3589983f, -0.0214987174f), new float3(-0.190625235f, -0.221416682f, -0.0214987174f), new float3(-0.190625235f, 0.3589983f, -0.0214987174f) }, new int[] { 0, 1, 2, 3, -1, 1, 4, 5, 2, -1, 6, 0, 3, 7, -1, 4, 6, 7, 5, -1, 5, 7, 3, 2, -1, 6, 4, 1, 0, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 1f, 0f, -0.017198544f),
					new float4(1f, 0f, 0f, -0.190624774f),
					new float4(-1f, 0f, 0f, -0.190625235f),
					new float4(-0.000353898649f, -0.7071067f, 0.7071067f, -0.243277133f),
					new float4(0f, -0.707106769f, -0.707106769f, -0.197636008f),
					new float4(0f, -1f, 0f, -0.02149889f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(-10.2556248f, 13.4080057f, -25.65232f, 1f)), name: "Solid 231681 #1597")
					.WithMesh(new float3[] { new float3(0.190624774f, 0.017198544f, -0.296698064f), new float3(-0.190625235f, 0.017198544f, -0.296698064f), new float3(-0.190625235f, 0.017198544f, 0.361148983f), new float3(0.190624774f, 0.017198544f, 0.3613398f), new float3(0.190624774f, -0.02149889f, 0.322642356f), new float3(0.190624774f, -0.02149889f, -0.258000642f), new float3(-0.190625235f, -0.02149889f, -0.258000642f), new float3(-0.190625235f, -0.02149889f, 0.322451562f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 0, 3, -1, 6, 7, 2, 1, -1, 7, 4, 3, 2, -1, 5, 6, 1, 0, -1, 6, 5, 4, 7, -1 }))
				;
		}

		// bm_c2a5h, Details: the mesh draws nothing where the exact CSG has a face; Solid 2006753 #3296 face 3 near (-77.969,
		// 8.997, -19.215) (3 brushes)
		static ContentsScene BmC2a5hDetails53Site3()
		{
			return new ContentsScene()
				.WithContentsList("Solid", "Water", "Glass", "Grate")
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(1f, 0f, 0f, -0.169442743f),
					new float4(-1f, 0f, 0f, -0.135557249f),
					new float4(0f, 1f, 0f, -1.75374961f),
					new float4(0f, 0f, -1f, -0.40666756f),
					new float4(0f, 0f, 1f, -0.508332431f),
					new float4(0f, -1f, 0f, -1.75375032f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(-77.94444f, 8.00625f, -18.8083324f, 1f)), name: "Solid 2006753 #3296")
					.WithMesh(new float3[] { new float3(0.169442743f, -1.75375032f, 0.508332431f), new float3(0.169442743f, -1.75375032f, -0.40666756f), new float3(0.169442743f, 1.75374961f, -0.40666756f), new float3(0.169442743f, 1.75374961f, 0.508332431f), new float3(-0.135557249f, -1.75375032f, -0.40666756f), new float3(-0.135557249f, -1.75375032f, 0.508332431f), new float3(-0.135557249f, 1.75374961f, 0.508332431f), new float3(-0.135557249f, 1.75374961f, -0.40666756f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 6, 3, 2, 7, -1, 1, 4, 7, 2, -1, 5, 0, 3, 6, -1, 0, 5, 4, 1, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 1f, 0f, -2.13500023f),
					new float4(1f, 0f, 0f, -0.0423638932f),
					new float4(-1f, 0f, 0f, -0.03388611f),
					new float4(0f, 0f, -1f, -4.473333f),
					new float4(0f, -1f, 0f, -3.35499978f),
					new float4(0f, 0f, 1f, -5.591667f),
				}, contents: 2, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(-77.9698639f, 11.285f, -24.8066673f, 1f)), name: "Glass 1577653 #3393")
					.WithMesh(new float3[] { new float3(-0.03388611f, 2.13500023f, 5.591667f), new float3(0.0423638932f, 2.13500023f, 5.591667f), new float3(0.0423638932f, 2.13500023f, -4.473333f), new float3(-0.03388611f, 2.13500023f, -4.473333f), new float3(0.0423638932f, -3.35499978f, 5.591667f), new float3(0.0423638932f, -3.35499978f, -4.473333f), new float3(-0.03388611f, -3.35499978f, 5.591667f), new float3(-0.03388611f, -3.35499978f, -4.473333f) }, new int[] { 0, 1, 2, 3, -1, 1, 4, 5, 2, -1, 6, 0, 3, 7, -1, 5, 7, 3, 2, -1, 4, 6, 7, 5, -1, 6, 4, 1, 0, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 1f, 0f, -2.30444431f),
					new float4(1f, 0f, 0f, -0.0423638932f),
					new float4(-1f, 0f, 0f, -0.03388611f),
					new float4(0f, -1f, 0f, -2.88055587f),
					new float4(0f, 0f, -1f, -2.37222266f),
					new float4(0f, 0f, 1f, -1.8977772f),
				}, contents: 2, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(-77.9698639f, 11.1155558f, -16.8427773f, 1f)), name: "Glass 1577652 #3394")
					.WithMesh(new float3[] { new float3(-0.03388611f, 2.30444431f, 1.8977772f), new float3(0.0423638932f, 2.30444431f, 1.8977772f), new float3(0.0423638932f, 2.30444431f, -2.37222266f), new float3(-0.03388611f, 2.30444431f, -2.37222266f), new float3(0.0423638932f, -2.88055587f, 1.8977772f), new float3(0.0423638932f, -2.88055587f, -2.37222266f), new float3(-0.03388611f, -2.88055587f, 1.8977772f), new float3(-0.03388611f, -2.88055587f, -2.37222266f) }, new int[] { 0, 1, 2, 3, -1, 1, 4, 5, 2, -1, 6, 0, 3, 7, -1, 4, 6, 7, 5, -1, 5, 7, 3, 2, -1, 6, 4, 1, 0, -1 }))
				;
		}

		// bm_c2a5h, Details: the exact CSG made a triangle the mesh does not have; Solid 231836 #1605 face -1 near (-10.256,
		// 13.425, -27.731) (2 brushes)
		static ContentsScene BmC2a5hDetails53Site4()
		{
			return new ContentsScene()
				.WithContentsList("Solid", "Water", "Glass", "Grate")
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 0f, 1f, -0.01719925f),
					new float4(1f, 0f, 0f, -0.190624774f),
					new float4(-1f, 0f, 0f, -0.190625235f),
					new float4(0f, 0f, -1f, -0.0214981847f),
					new float4(0f, 0.707457662f, -0.706755757f, -0.269170046f),
					new float4(3.4535522E-05f, -0.7072875f, -0.706926f, -0.171796277f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(-10.2556248f, 13.0274515f, -27.7483711f, 1f)), name: "Solid 231835 #1604")
					.WithMesh(new float3[] { new float3(-0.190625235f, -0.2600943f, 0.01719925f), new float3(0.190624774f, -0.2600757f, 0.01719925f), new float3(0.190624774f, 0.3976573f, 0.01719925f), new float3(-0.190625235f, 0.3976573f, 0.01719925f), new float3(0.190624774f, -0.221398041f, -0.0214981847f), new float3(0.190624774f, 0.358998269f, -0.0214981847f), new float3(-0.190625235f, -0.221416667f, -0.0214981847f), new float3(-0.190625235f, 0.358998269f, -0.0214981847f) }, new int[] { 0, 1, 2, 3, -1, 1, 4, 5, 2, -1, 6, 0, 3, 7, -1, 4, 6, 7, 5, -1, 5, 7, 3, 2, -1, 6, 4, 1, 0, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 1f, 0f, -0.017198544f),
					new float4(1f, 0f, 0f, -0.190624774f),
					new float4(-1f, 0f, 0f, -0.190625235f),
					new float4(-0.000353898649f, -0.7071067f, 0.7071067f, -0.24327752f),
					new float4(0f, -0.707106769f, -0.707106769f, -0.197635636f),
					new float4(0f, -1f, 0f, -0.02149889f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(-10.2556248f, 13.4080057f, -28.0923214f, 1f)), name: "Solid 231836 #1605")
					.WithMesh(new float3[] { new float3(0.190624774f, 0.017198544f, -0.296697557f), new float3(-0.190625235f, 0.017198544f, -0.296697557f), new float3(-0.190625235f, 0.017198544f, 0.361149549f), new float3(0.190624774f, 0.017198544f, 0.361340344f), new float3(0.190624774f, -0.02149889f, 0.322642922f), new float3(0.190624774f, -0.02149889f, -0.2580001f), new float3(-0.190625235f, -0.02149889f, -0.2580001f), new float3(-0.190625235f, -0.02149889f, 0.3224521f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 0, 3, -1, 6, 7, 2, 1, -1, 7, 4, 3, 2, -1, 5, 6, 1, 0, -1, 6, 5, 4, 7, -1 }))
				;
		}

		// bm_c2a5h, Details: the exact CSG made a triangle the mesh does not have; Solid 365382 #2435 face -1 near (-37.797,
		// 16.318, 35.939) (4 brushes)
		static ContentsScene BmC2a5hDetails53Site5()
		{
			return new ContentsScene()
				.WithContentsList("Solid", "Water", "Glass", "Grate")
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0.08521926f, 0.9927121f, 0.08520785f, -0.6547925f),
					new float4(0.273406118f, 0.9222439f, 0.27334094f, -0.508777857f),
					new float4(0.420259655f, 0.804195762f, 0.4202987f, -0.426636368f),
					new float4(0.568191946f, 0.5952244f, 0.568212867f, -0.3853606f),
					new float4(0.651860952f, 0.38754946f, 0.651830256f, -0.40296635f),
					new float4(0.701868236f, 0.120409645f, 0.702055931f, -0.482233375f),
					new float4(-0.7071062f, 1.080984E-06f, -0.707107365f, -0.6763258f),
					new float4(0.7070127f, 1.18095095E-07f, 0.7072008f, -0.543726444f),
					new float4(-0.927053332f, -0.00220259419f, 0.374923f, -1.05177379f),
					new float4(0.383191049f, -0.0006540681f, -0.923668861f, -1.32450676f),
					new float4(0f, -1f, 0f, -0.7832909f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(-38.4620934f, 18.01579f, 36.7696381f, 1f)), name: "Solid 365375 #2428")
					.WithMesh(new float3[] { new float3(0.651441336f, 0.7036044f, -1.16420567f), new float3(0.3378583f, 0.7416925f, -1.29432487f), new float3(-1.08451259f, 0.741709054f, 0.128043562f), new float3(-0.9565467f, 0.7035841f, 0.444234371f), new float3(0.9480404f, 0.579179943f, -1.041071f), new float3(-0.8358694f, 0.5795846f, 0.7418987f), new float3(1.204947f, 0.3891524f, -0.9343569f), new float3(-0.730549157f, 0.3890271f, 1.00119936f), new float3(1.39470267f, 0.132692128f, -0.8554537f), new float3(-0.6526752f, 0.132312864f, 1.19224608f), new float3(1.51900673f, -0.163475335f, -0.803675532f), new float3(-0.6018059f, -0.161898971f, 1.31629968f), new float3(1.55696356f, -0.4778357f, -0.787706256f), new float3(-0.5856656f, -0.477852643f, 1.354353f), new float3(0.337093383f, -0.7832909f, -1.29356229f), new float3(-1.08193338f, -0.7832909f, 0.125462011f), new float3(-0.5851488f, -0.7832909f, 1.3538363f), new float3(1.55681062f, -0.7832909f, -0.78755337f) }, new int[] { 0, 1, 2, 3, -1, 4, 0, 3, 5, -1, 6, 4, 5, 7, -1, 8, 6, 7, 9, -1, 10, 8, 9, 11, -1, 12, 10, 11, 13, -1, 14, 15, 2, 1, -1, 16, 17, 12, 13, -1, 15, 16, 13, 11, 9, 7, 5, 3, 2, -1, 17, 14, 1, 0, 4, 6, 8, 10, 12, -1, 14, 17, 16, 15, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(-0.7070946f, -5.070501E-09f, -0.7071189f, -0.542312443f),
					new float4(0.706947744f, -0.000147457773f, 0.7072658f, -0.6779802f),
					new float4(0.383056045f, -0.000216137239f, -0.923725069f, -0.147691578f),
					new float4(0f, 1f, 0f, -1.01666629f),
					new float4(0f, -1f, 0f, -0.8133337f),
					new float4(-0.707028568f, 0f, 0.707185f, -0.159892425f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(-37.69581f, 16.2158337f, 35.81383f, 1f)), name: "Solid 365382 #2435")
					.WithMesh(new float3[] { new float3(-0.4965782f, -0.8133337f, -0.270371318f), new float3(-0.4965782f, 1.01666629f, -0.270371318f), new float3(-0.428944856f, 1.01666629f, -0.338002354f), new float3(-0.429247528f, -0.8133337f, -0.337699652f), new float3(0.366476983f, 1.01666629f, 0.592493f), new float3(0.366286159f, -0.8133337f, 0.5923022f), new float3(0.7906176f, -0.8133337f, 0.168161571f), new float3(0.791190147f, 1.01666629f, 0.1679708f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 6, 3, 2, 7, -1, 1, 4, 7, 2, -1, 5, 0, 3, 6, -1, 0, 5, 4, 1, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0.7071589f, 0f, 0.7070547f, -0.254232675f),
					new float4(-0.707106769f, 0f, -0.707106769f, -0.203185543f),
					new float4(0.707106769f, 0f, -0.707106769f, -0.9149312f),
					new float4(-0.7073153f, 0f, 0.706898153f, -0.9149333f),
					new float4(0f, -1f, 0f, -0.7116663f),
					new float4(0f, 1f, 0f, -1.1183337f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(-38.2641869f, 16.1141663f, 36.7655678f, 1f)), name: "Solid 365393 #2446")
					.WithMesh(new float3[] { new float3(0.826676f, 1.1183337f, -0.4672321f), new float3(-0.4671913f, 1.1183337f, 0.826826f), new float3(-0.4671913f, -0.7116663f, 0.826826f), new float3(0.826676f, -0.7116663f, -0.4672321f), new float3(-0.790587068f, 1.1183337f, 0.503239334f), new float3(0.503280163f, 1.1183337f, -0.790627956f), new float3(0.503280163f, -0.7116663f, -0.790627956f), new float3(-0.790587068f, -0.7116663f, 0.503239334f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 5, 0, 3, 6, -1, 1, 4, 7, 2, -1, 2, 7, 6, 3, -1, 4, 1, 0, 5, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0.7083692f, 0f, 0.705842137f, -0.08474548f),
					new float4(-0.707106769f, 0f, -0.707106769f, -0.06763705f),
					new float4(0.70709914f, 0f, -0.7071144f, -0.0381598547f),
					new float4(-0.499416977f, -0.7072977f, 0.500312567f, -0.5452545f),
					new float4(-0.5003015f, 0.7060518f, 0.501187861f, -0.6939348f),
					new float4(-0.707106769f, 0f, 0.707106769f, -0.0381320938f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(-37.4164772f, 16.2115974f, 36.3731461f, 1f)), name: "Solid 365394 #2447")
					.WithMesh(new float3[] { new float3(0.03300892f, 0.9445171f, 0.08693584f), new float3(0.03300892f, -0.7327108f, 0.08693584f), new float3(0.08685972f, -0.8089626f, 0.0328922421f), new float3(0.08685972f, 1.02103782f, 0.0328922421f), new float3(-0.07479008f, -0.7328473f, -0.0208631549f), new float3(-0.07479008f, 0.944652438f, -0.0208631549f), new float3(-0.02084404f, 1.02117145f, -0.07480919f), new float3(-0.02084404f, -0.8090973f, -0.07480919f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 6, 3, 2, 7, -1, 1, 4, 7, 2, -1, 5, 0, 3, 6, -1, 0, 5, 4, 1, -1 }))
				;
		}

		// bm_c2a5h, Details: the mesh has a triangle the exact CSG did not make; Solid 14258 #1858 face -1 near (14.07,
		// 24.044, -56.629) (6 brushes)
		static ContentsScene BmC2a5hDetails53Site6()
		{
			return new ContentsScene()
				.WithContentsList("Solid", "Water", "Glass", "Grate")
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0.0459594727f, 0.992713749f, 0.111387342f, -0.448392659f),
					new float4(0.147673786f, 0.9222294f, 0.357330859f, -0.326862037f),
					new float4(0.22734575f, 0.804053f, 0.5493748f, -0.27655378f),
					new float4(0.306732684f, 0.5966718f, 0.741551f, -0.286540926f),
					new float4(0.352942169f, 0.38683778f, 0.8519321f, -0.353744239f),
					new float4(0.3796043f, 0.120555207f, 0.9172606f, -0.491458863f),
					new float4(-0.3814174f, -7.389629E-05f, -0.924402952f, -0.640945733f),
					new float4(0.09283151f, -0.9701348f, 0.224099845f, -0.810284257f),
					new float4(0.7074814f, 0.00268843747f, -0.706726849f, -0.300843954f),
					new float4(-0.9807196f, 0.000399595418f, 0.195419818f, -0.250203818f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(17.8098316f, 24.7855129f, -57.1014252f, 1f)), name: "Solid 14256 #1856")
					.WithMesh(new float3[] { new float3(-0.3632134f, 0.5294871f, -0.543538868f), new float3(-0.300561f, 0.49129796f, -0.229038164f), new float3(0.04982711f, 0.491334468f, -0.373936951f), new float3(-0.19080174f, 0.5294871f, -0.614677548f), new float3(-0.242036611f, 0.368028283f, 0.06492021f), new float3(0.274697065f, 0.368286f, -0.149294972f), new float3(-0.19080174f, 0.177594066f, 0.322433233f), new float3(0.4726611f, 0.177403152f, 0.04815428f), new float3(-0.152375445f, -0.0824889243f, 0.515808463f), new float3(0.6203602f, -0.08106118f, 0.195027843f), new float3(-0.128162161f, -0.373503417f, 0.637918532f), new float3(0.7153382f, -0.374650419f, 0.288990378f), new float3(-0.120611921f, -0.690508962f, 0.6764578f), new float3(0.745519757f, -0.690432f, 0.3180029f), new float3(-0.363765121f, -0.995512962f, -0.543189347f), new float3(-0.186612055f, -0.995446146f, -0.6162844f) }, new int[] { 0, 1, 2, 3, -1, 1, 4, 5, 2, -1, 4, 6, 7, 5, -1, 6, 8, 9, 7, -1, 8, 10, 11, 9, -1, 10, 12, 13, 11, -1, 14, 0, 3, 15, -1, 12, 14, 15, 13, -1, 13, 15, 3, 2, 5, 7, 9, 11, -1, 14, 12, 10, 8, 6, 4, 1, 0, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 0.992689967f, 0.120692149f, -0.448655963f),
					new float4(0.0001579461f, 0.9223066f, 0.386458963f, -0.327067375f),
					new float4(5.47892269E-05f, 0.8031934f, 0.5957183f, -0.276773572f),
					new float4(2.09871032E-05f, 0.5953152f, 0.803492248f, -0.286957145f),
					new float4(0.0001011695f, 0.3874579f, 0.9218874f, -0.352941841f),
					new float4(3.13825E-05f, 0.12018837f, 0.9927511f, -0.4913521f),
					new float4(0f, 0f, -1f, -0.641582f),
					new float4(0f, -0.9701425f, 0.242535621f, -0.8097203f),
					new float4(0.980537832f, 0.0011212161f, -0.19632706f, -1.80398774f),
					new float4(-0.98107177f, 0.0008901212f, -0.193642318f, -1.56050062f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(15.7349672f, 24.7850361f, -57.003418f, 1f)), name: "Solid 14258 #1858")
					.WithMesh(new float3[] { new float3(-1.46349275f, 0.5299639f, -0.641582f), new float3(-1.52573931f, 0.491642833f, -0.326391846f), new float3(1.77349985f, 0.491873771f, -0.328291446f), new float3(1.710728f, 0.5299639f, -0.641582f), new float3(-1.5840404f, 0.3681235f, -0.03158204f), new float3(1.83326936f, 0.367081046f, -0.0304908175f), new float3(-1.63504541f, 0.177116886f, 0.2259525f), new float3(1.88487136f, 0.176735252f, 0.226143315f), new float3(-1.67297125f, -0.08062296f, 0.416915447f), new float3(1.92320371f, -0.07968504f, 0.4161266f), new float3(-1.69758964f, -0.374171436f, 0.540293f), new float3(1.94840264f, -0.375123441f, 0.540293f), new float3(-1.70542336f, -0.690008f, 0.578530252f), new float3(1.95639622f, -0.6900361f, 0.5784179f), new float3(-1.46487641f, -0.995036066f, -0.641582f), new float3(1.71247184f, -0.995036066f, -0.641582f) }, new int[] { 0, 1, 2, 3, -1, 1, 4, 5, 2, -1, 4, 6, 7, 5, -1, 6, 8, 9, 7, -1, 8, 10, 11, 9, -1, 10, 12, 13, 11, -1, 14, 0, 3, 15, -1, 12, 14, 15, 13, -1, 13, 15, 3, 2, 5, 7, 9, 11, -1, 14, 12, 10, 8, 6, 4, 1, 0, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(-0.04627103f, 0.9926942f, 0.111432821f, -0.5095721f),
					new float4(-0.147391587f, 0.9223473f, 0.357143044f, -0.383350134f),
					new float4(-0.2277508f, 0.8030014f, 0.5507434f, -0.3251662f),
					new float4(-0.307498366f, 0.595005f, 0.7425724f, -0.3225905f),
					new float4(-0.352616221f, 0.3860171f, 0.8524392f, -0.3758799f),
					new float4(-0.380025953f, 0.120600924f, 0.91708f, -0.4971139f),
					new float4(0.3836945f, 5.31925361E-05f, -0.923460066f, -0.6429364f),
					new float4(-0.09318861f, -0.9700584f, 0.224282488f, -0.7494263f),
					new float4(-0.701369345f, -0.002097682f, -0.71279496f, -0.210308239f),
					new float4(0.98056823f, -0.000453803164f, 0.19617781f, -0.341125369f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(13.8234138f, 24.7235413f, -57.1350327f, 1f)), name: "Solid 14261 #1861")
					.WithMesh(new float3[] { new float3(0.387328625f, 0.5533635f, -0.195871651f), new float3(0.450033277f, 0.591458738f, -0.5092041f), new float3(0.285396785f, 0.5914635f, -0.57761f), new float3(0.04201942f, 0.5532248f, -0.338021278f), new float3(0.328032136f, 0.4292353f, 0.100226767f), new float3(-0.1862806f, 0.42961818f, -0.113016985f), new float3(0.276234537f, 0.237278372f, 0.35868603f), new float3(-0.3859523f, 0.237848461f, 0.084018454f), new float3(0.238271192f, -0.0184186157f, 0.5478491f), new float3(-0.532847345f, -0.0193995945f, 0.229315922f), new float3(0.213315189f, -0.315160334f, 0.671901941f), new float3(-0.627436638f, -0.313246757f, 0.323253781f), new float3(0.20557636f, -0.6281835f, 0.7098593f), new float3(-0.6563716f, -0.627968848f, 0.352651149f), new float3(0.4493979f, -0.9335413f, -0.509555936f), new float3(0.288666427f, -0.9335413f, -0.5763393f) }, new int[] { 0, 1, 2, 3, -1, 4, 0, 3, 5, -1, 6, 4, 5, 7, -1, 8, 6, 7, 9, -1, 10, 8, 9, 11, -1, 12, 10, 11, 13, -1, 1, 14, 15, 2, -1, 14, 12, 13, 15, -1, 15, 13, 11, 9, 7, 5, 3, 2, -1, 12, 14, 1, 0, 4, 6, 8, 10, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, -1f, 0f, -0.271111131f),
					new float4(0f, 0f, 1f, -0.305000156f),
					new float4(0f, 0f, -1f, -0.304999858f),
					new float4(1f, 0f, 0f, -2.03333879f),
					new float4(0f, 1f, 0f, -0.338888854f),
					new float4(-1f, 0f, -3.05175781E-05f, -1.62667048f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(15.65628f, 23.7561111f, -57.34f, 1f)), name: "Solid 14288 #1888")
					.WithMesh(new float3[] { new float3(-1.62667978f, -0.271111131f, 0.305000156f), new float3(-1.62666118f, -0.271111131f, -0.304999858f), new float3(2.03333879f, -0.271111131f, -0.304999858f), new float3(2.03333879f, -0.271111131f, 0.305000156f), new float3(-1.62667978f, 0.338888854f, 0.305000156f), new float3(2.03333879f, 0.338888854f, 0.305000156f), new float3(-1.62666118f, 0.338888854f, -0.304999858f), new float3(2.03333879f, 0.338888854f, -0.304999858f) }, new int[] { 0, 1, 2, 3, -1, 4, 0, 3, 5, -1, 1, 6, 7, 2, -1, 7, 5, 3, 2, -1, 6, 4, 5, 7, -1, 4, 6, 1, 0, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(-0.383095f, 0f, -0.923709f, -0.610036f),
					new float4(0.382512748f, 0f, 0.9239502f, -0.610009968f),
					new float4(0.7070927f, 0f, -0.7071209f, -0.287673026f),
					new float4(-0.9805866f, 0f, 0.19608663f, -0.244378522f),
					new float4(0f, 1f, 0f, -2.05027843f),
					new float4(0f, -1f, 0f, -1.30472159f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(17.7978516f, 22.0447216f, -57.1300278f, 1f)), name: "Solid 14295 #1895")
					.WithMesh(new float3[] { new float3(-0.179259166f, -1.30472159f, -0.586075f), new float3(-0.352080524f, -1.30472159f, -0.5143998f), new float3(-0.352080524f, 2.05027843f, -0.5143998f), new float3(-0.179259166f, 2.05027843f, -0.586075f), new float3(-0.108233176f, -1.30472159f, 0.705027759f), new float3(0.754649758f, -1.30472159f, 0.347796679f), new float3(0.754649758f, 2.05027843f, 0.347796679f), new float3(-0.108233176f, 2.05027843f, 0.705027759f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 5, 0, 3, 6, -1, 1, 4, 7, 2, -1, 2, 7, 6, 3, -1, 4, 1, 0, 5, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0.383528978f, 0f, -0.92352885f, -0.6100229f),
					new float4(-0.3828462f, 0f, 0.9238121f, -0.609959543f),
					new float4(-0.707128346f, 0f, -0.7070852f, -0.230071768f),
					new float4(0.980601132f, 0f, 0.196013764f, -0.3053018f),
					new float4(0f, 1f, 0f, -2.05027843f),
					new float4(0f, -1f, 0f, -1.30472159f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(13.8639793f, 22.0447216f, -57.1539078f, 1f)), name: "Solid 14301 #1901")
					.WithMesh(new float3[] { new float3(0.409392148f, -1.30472159f, -0.490519762f), new float3(0.236799955f, -1.30472159f, -0.562194943f), new float3(0.236799955f, 2.05027843f, -0.562194943f), new float3(0.409392148f, 2.05027843f, -0.490519762f), new float3(-0.6968239f, -1.30472159f, 0.3714859f), new float3(0.165639043f, -1.30472159f, 0.728907764f), new float3(0.165639043f, 2.05027843f, 0.728907764f), new float3(-0.6968239f, 2.05027843f, 0.3714859f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 1, 4, 7, 2, -1, 5, 0, 3, 6, -1, 6, 3, 2, 7, -1, 0, 5, 4, 1, -1 }))
				;
		}

		// bm_c2a5h, Details: the exact CSG made a triangle the mesh does not have; Solid 633125 #2684 face -1 near (-13.115,
		// 5.188, -14.03) (3 brushes)
		static ContentsScene BmC2a5hDetails53Site7()
		{
			return new ContentsScene()
				.WithContentsList("Solid", "Water", "Glass", "Grate")
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 0f, 1f, -0.325333327f),
					new float4(0f, 0f, -1f, -0.284666657f),
					new float4(0.9486833f, 0.316227764f, 0f, -0.101286583f),
					new float4(-0.998460352f, 0.05547002f, -2.078983E-05f, -0.029602658f),
					new float4(0.970171f, -0.2424216f, 9.08581642E-05f, -0.0345000066f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(-13.0667086f, 5.52045441f, -14.3553333f, 1f)), name: "Solid 17757 #2212")
					.WithMesh(new float3[] { new float3(-0.0482914373f, -0.335454434f, 0.325333327f), new float3(0.06605092f, 0.122143544f, 0.325333327f), new float3(-0.0101664355f, 0.3507956f, 0.325333327f), new float3(0.0660835654f, 0.122045591f, -0.284666657f), new float3(-0.0482914373f, -0.335683048f, -0.284666657f), new float3(-0.0101555483f, 0.350762933f, -0.284666657f) }, new int[] { 0, 1, 2, -1, 3, 4, 5, -1, 1, 3, 5, 2, -1, 4, 0, 2, 5, -1, 0, 4, 3, 1, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 0f, 1f, -0.325333327f),
					new float4(0f, 0f, -1f, -0.284666657f),
					new float4(-1f, 0f, 0f, -0.0127079394f),
					new float4(0.9864205f, 0.164239272f, 0f, -0.06017018f),
					new float4(0.998463333f, -0.0554163f, 1.74402044E-06f, -0.01353493f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(-13.1022921f, 5.657548f, -14.3553333f, 1f)), name: "Solid 17758 #2213")
					.WithMesh(new float3[] { new float3(-0.0127079394f, -0.47319603f, 0.325333327f), new float3(0.0254162624f, 0.213706851f, 0.325333327f), new float3(-0.0127079394f, 0.4426807f, 0.325333327f), new float3(0.0254170615f, 0.213702053f, -0.284666657f), new float3(-0.0127079394f, -0.473215222f, -0.284666657f), new float3(-0.0127079394f, 0.4426807f, -0.284666657f) }, new int[] { 0, 1, 2, -1, 3, 4, 5, -1, 4, 0, 2, 5, -1, 1, 3, 5, 2, -1, 3, 1, 0, 4, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0.9863939f, 0.164398983f, 0f, -0.0601702668f),
					new float4(-1f, 0f, 0f, -0.0127080726f),
					new float4(0f, 0f, 1f, -0.325333327f),
					new float4(0f, 0f, -1f, -0.284666657f),
					new float4(0.998460352f, -0.05547002f, 0f, -0.013534952f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(-13.1020441f, 5.65775f, -14.3553333f, 1f)), name: "Solid 633125 #2684")
					.WithMesh(new float3[] { new float3(0.0254169274f, 0.213499889f, 0.325333327f), new float3(0.0254169274f, 0.213499889f, -0.284666657f), new float3(-0.0127080726f, 0.4422499f, -0.284666657f), new float3(-0.0127080726f, 0.4422499f, 0.325333327f), new float3(-0.0127080726f, -0.472750127f, -0.284666657f), new float3(-0.0127080726f, -0.472750127f, 0.325333327f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 3, 2, -1, 5, 0, 3, -1, 1, 4, 2, -1, 4, 1, 0, 5, -1 }))
				;
		}

		// bm_c2a5h, Details: the mesh has a triangle the exact CSG did not make; Solid 14302 #1902 face -1 near (13.766,
		// 22.523, -57.381) (5 brushes)
		static ContentsScene BmC2a5hDetails53Site8()
		{
			return new ContentsScene()
				.WithContentsList("Solid", "Water", "Glass", "Grate")
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(-0.111340776f, 0.9927112f, 0.0461270846f, -0.447430521f),
					new float4(-0.3566423f, 0.9223744f, 0.148430869f, -0.325351864f),
					new float4(-0.549922f, 0.8034254f, 0.22824f, -0.274683535f),
					new float4(-0.7430553f, 0.594122946f, 0.308036923f, -0.28460446f),
					new float4(-0.8517627f, 0.3873298f, 0.3528115f, -0.350610316f),
					new float4(-0.9171914f, 0.1201199f, 0.379909366f, -0.489026666f),
					new float4(0.9238554f, -2.16097615E-05f, -0.3827417f, -0.644010544f),
					new float4(-0.223765388f, -0.970225334f, 0.09269221f, -0.810464561f),
					new float4(0.706251562f, 0.00158938125f, 0.7079592f, -0.297906846f),
					new float4(-0.1943832f, -0.000796828943f, -0.9809253f, -0.2536743f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(13.4848137f, 24.7860985f, -57.5207558f, 1f)), name: "Solid 14260 #1860")
					.WithMesh(new float3[] { new float3(0.5450409f, 0.5289015f, -0.3670438f), new float3(0.229954451f, 0.490659267f, -0.304574221f), new float3(0.375681728f, 0.49076432f, 0.0449193f), new float3(0.6162518f, 0.5289015f, -0.195156142f), new float3(-0.06531117f, 0.367060959f, -0.245963052f), new float3(0.1487282f, 0.366532475f, 0.2716043f), new float3(-0.322843879f, 0.176245287f, -0.194774568f), new float3(-0.048253864f, 0.175757721f, 0.468539536f), new float3(-0.5141739f, -0.0828134045f, -0.156649575f), new float3(-0.194774136f, -0.08357715f, 0.6152886f), new float3(-0.6370461f, -0.375412375f, -0.132063165f), new float3(-0.28849104f, -0.375422448f, 0.7094347f), new float3(-0.675202f, -0.691482961f, -0.124245308f), new float3(-0.3175725f, -0.691477358f, 0.739155531f), new float3(0.5454822f, -0.996098459f, -0.36589244f), new float3(0.6172302f, -0.9961004f, -0.192708492f) }, new int[] { 0, 1, 2, 3, -1, 1, 4, 5, 2, -1, 4, 6, 7, 5, -1, 6, 8, 9, 7, -1, 8, 10, 11, 9, -1, 10, 12, 13, 11, -1, 14, 0, 3, 15, -1, 12, 14, 15, 13, -1, 13, 15, 3, 2, 5, 7, 9, 11, -1, 14, 12, 10, 8, 6, 4, 1, 0, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, -1f, 0f, -0.271111131f),
					new float4(-1f, -0.000123977661f, 0f, -0.33886975f),
					new float4(1f, 3.05175781E-05f, 0f, -0.271098554f),
					new float4(0f, 0f, 1f, -1.82999849f),
					new float4(0f, 1f, 0f, -0.338888854f),
					new float4(0f, 0f, -1f, -1.83000147f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(13.7585115f, 23.7561111f, -59.475f, 1f)), name: "Solid 14270 #1870")
					.WithMesh(new float3[] { new float3(-0.338836133f, -0.271111131f, -1.83000147f), new float3(0.271106839f, -0.271111131f, -1.83000147f), new float3(0.271106839f, -0.271111131f, 1.82999849f), new float3(-0.338836133f, -0.271111131f, 1.82999849f), new float3(-0.338911772f, 0.338888854f, -1.83000147f), new float3(-0.338911772f, 0.338888854f, 1.82999849f), new float3(0.2710882f, 0.338888854f, -1.83000147f), new float3(0.2710882f, 0.338888854f, 1.82999849f) }, new int[] { 0, 1, 2, 3, -1, 4, 0, 3, 5, -1, 1, 6, 7, 2, -1, 7, 5, 3, 2, -1, 6, 4, 5, 7, -1, 4, 6, 1, 0, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, -1f, 0f, -0.271110624f),
					new float4(-1f, -0.000123977661f, 0f, -0.33886975f),
					new float4(1f, 3.05175781E-05f, 0f, -0.271098554f),
					new float4(0f, 0f, 1f, -1.82999849f),
					new float4(0f, 1f, 0f, -0.3388894f),
					new float4(0f, 0f, -1f, -1.83000147f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(13.7585115f, 21.31611f, -59.475f, 1f)), name: "Solid 14271 #1871")
					.WithMesh(new float3[] { new float3(-0.338836133f, -0.271110624f, -1.83000147f), new float3(0.271106839f, -0.271110624f, -1.83000147f), new float3(0.271106839f, -0.271110624f, 1.82999849f), new float3(-0.338836133f, -0.271110624f, 1.82999849f), new float3(-0.338911772f, 0.3388894f, -1.83000147f), new float3(-0.338911772f, 0.3388894f, 1.82999849f), new float3(0.2710882f, 0.3388894f, -1.83000147f), new float3(0.2710882f, 0.3388894f, 1.82999849f) }, new int[] { 0, 1, 2, 3, -1, 4, 0, 3, 5, -1, 1, 6, 7, 2, -1, 7, 5, 3, 2, -1, 6, 4, 5, 7, -1, 4, 6, 1, 0, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0.383528978f, 0f, -0.92352885f, -0.6100229f),
					new float4(-0.3828462f, 0f, 0.9238121f, -0.609959543f),
					new float4(-0.707128346f, 0f, -0.7070852f, -0.230071768f),
					new float4(0.980601132f, 0f, 0.196013764f, -0.3053018f),
					new float4(0f, 1f, 0f, -2.05027843f),
					new float4(0f, -1f, 0f, -1.30472159f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(13.8639793f, 22.0447216f, -57.1539078f, 1f)), name: "Solid 14301 #1901")
					.WithMesh(new float3[] { new float3(0.409392148f, -1.30472159f, -0.490519762f), new float3(0.236799955f, -1.30472159f, -0.562194943f), new float3(0.236799955f, 2.05027843f, -0.562194943f), new float3(0.409392148f, 2.05027843f, -0.490519762f), new float3(-0.6968239f, -1.30472159f, 0.3714859f), new float3(0.165639043f, -1.30472159f, 0.728907764f), new float3(0.165639043f, 2.05027843f, 0.728907764f), new float3(-0.6968239f, 2.05027843f, 0.3714859f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 1, 4, 7, 2, -1, 5, 0, 3, 6, -1, 6, 3, 2, 7, -1, 0, 5, 4, 1, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0.924017847f, 0f, -0.3823494f, -0.5421538f),
					new float4(-0.9238086f, 0f, 0.38285467f, -0.6777309f),
					new float4(0.7071645f, 0f, 0.7070491f, -0.239664555f),
					new float4(-0.196040034f, 0f, -0.9805959f, -0.2544741f),
					new float4(0f, 1f, 0f, -2.05027843f),
					new float4(0f, -1f, 0f, -1.30472159f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(13.5864143f, 22.0447216f, -57.54079f, 1f)), name: "Solid 14302 #1902")
					.WithMesh(new float3[] { new float3(0.51419276f, -1.30472159f, -0.175312176f), new float3(0.442728132f, -1.30472159f, -0.34801954f), new float3(0.442728132f, 2.05027843f, -0.34801954f), new float3(0.51419276f, 2.05027843f, -0.175312176f), new float3(-0.7768146f, -1.30472159f, -0.104209438f), new float3(-0.4193357f, -1.30472159f, 0.758368731f), new float3(-0.4193357f, 2.05027843f, 0.758368731f), new float3(-0.7768146f, 2.05027843f, -0.104209438f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 5, 0, 3, 6, -1, 1, 4, 7, 2, -1, 2, 7, 6, 3, -1, 4, 1, 0, 5, -1 }))
				;
		}

		// bm_c2a5i, Details: the mesh has a triangle the exact CSG did not make; Solid 628 #4431 face -1 near (-14.64, 1.255,
		// 31.72) (5 brushes)
		static ContentsScene BmC2a5iDetails122Site1()
		{
			return new ContentsScene()
				.WithContentsList("Solid", "Water", "Glass", "Grate")
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(-0.09154736f, -0.995800734f, 0f, -0.060748022f),
					new float4(0.0915366039f, 0.9958017f, 0f, -0.09109968f),
					new float4(1f, 0f, 0f, -1.10559738f),
					new float4(0.102873981f, 0f, 0.9946944f, -0.0505576544f),
					new float4(-0.147596866f, 0.00160465646f, -0.9890463f, -0.0502066649f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 3.316179E-15f, 1.97659727E-22f, 0f), new float4(-3.31617888E-15f, 0.99999994f, 5.960465E-08f, 0f), new float4(0f, -5.960465E-08f, 0.99999994f, 0f), new float4(-12.4286575f, 1.07764614f, 31.4403915f, 1f)), name: "Solid 437 #4297")
					.WithMesh(new float3[] { new float3(-2.21265244f, 0.142412513f, 0.279665828f), new float3(1.10559738f, -0.162645534f, -0.216016546f), new float3(1.10559738f, -0.162645534f, -0.06351654f), new float3(1.10559738f, -0.0101455441f, -0.215769127f), new float3(-2.20727015f, 0.294381618f, 0.27910918f), new float3(1.10559738f, -0.0101455441f, -0.06351654f) }, new int[] { 0, 1, 2, -1, 3, 4, 5, -1, 1, 3, 5, 2, -1, 4, 0, 2, 5, -1, 0, 4, 3, 1, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(-0.0915504843f, -0.995800436f, 0f, -0.0607288368f),
					new float4(0.0916042551f, 0.9957955f, 0f, -0.0910703242f),
					new float4(-1f, 0f, 0f, -1.10552847f),
					new float4(-0.102883369f, 0f, -0.9946934f, -0.0505354367f),
					new float4(0.147973791f, 0.00240466441f, 0.98898834f, -0.05017179f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 3.316179E-15f, 1.97659727E-22f, 0f), new float4(-3.31617888E-15f, 0.99999994f, 5.960465E-08f, 0f), new float4(0f, -5.960465E-08f, 0.99999994f, 0f), new float4(-13.5344086f, 1.179346f, 31.5039558f, 1f)), name: "Solid 438 #4298")
					.WithMesh(new float3[] { new float3(2.21217084f, -0.264364362f, -0.279614836f), new float3(-1.10552847f, 0.0406535566f, 0.21604225f), new float3(-1.10552847f, 0.0406535566f, 0.06354225f), new float3(-1.10552847f, 0.19315356f, 0.215671465f), new float3(2.20411348f, -0.111303836f, -0.278781444f), new float3(-1.10552847f, 0.19315356f, 0.06354225f) }, new int[] { 0, 1, 2, -1, 3, 4, 5, -1, 1, 3, 5, 2, -1, 4, 0, 2, 5, -1, 0, 4, 3, 1, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0.08971226f, 0.9958061f, -0.0179424528f, -0.8909145f),
					new float4(0.9805807f, 0f, -0.196116135f, -1.10658491f),
					new float4(0.102899149f, 0f, 0.9946918f, -1.61280155f),
					new float4(-0.9075625f, 0f, -0.419917f, -0.955395937f),
					new float4(0f, -1f, 0f, -1.342f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 3.316179E-15f, 1.97659727E-22f, 0f), new float4(-3.31617888E-15f, 0.99999994f, 5.960465E-08f, 0f), new float4(0f, -5.960465E-08f, 0.99999994f, 0f), new float4(-12.7463989f, 0.121997818f, 29.9027042f, 1f)), name: "Solid 454 #4313")
					.WithMesh(new float3[] { new float3(0.470208019f, 0.793f, -3.291458f), new float3(-1.893542f, 1.098f, 1.817292f), new float3(1.42333293f, 0.793000042f, 1.474167f), new float3(1.42333293f, -1.342f, 1.474167f), new float3(0.470208019f, -1.342f, -3.291458f), new float3(-1.893542f, -1.342f, 1.817292f) }, new int[] { 0, 1, 2, -1, 3, 4, 0, 2, -1, 5, 3, 2, 1, -1, 4, 5, 1, 0, -1, 5, 4, 3, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, -1f, 0f, -0.9353334f),
					new float4(0.127971336f, 0.991777837f, 0f, -1.39146435f),
					new float4(-1f, 0f, 0f, -0.7879168f),
					new float4(-0.0963242f, 0f, -0.99535f, -1.61910307f),
					new float4(0.9075625f, 0f, 0.419917f, -0.68306464f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 3.316179E-15f, 1.97659727E-22f, 0f), new float4(-3.31617888E-15f, 0.99999994f, 5.960465E-08f, 0f), new float4(0f, -5.960465E-08f, 0.99999994f, 0f), new float4(-13.85202f, -0.284667939f, 28.3904171f, 1f)), name: "Solid 455 #4314")
					.WithMesh(new float3[] { new float3(1.5758332f, -0.9353334f, -1.779167f), new float3(-0.7879168f, -0.9353334f, 3.32958269f), new float3(-0.7879168f, -0.9353334f, -1.55041707f), new float3(-0.7879168f, 1.50466669f, 3.32958269f), new float3(1.5758332f, 1.19966674f, -1.779167f), new float3(-0.7879168f, 1.50466669f, -1.55041707f) }, new int[] { 0, 1, 2, -1, 3, 4, 5, -1, 1, 3, 5, 2, -1, 4, 0, 2, 5, -1, 0, 4, 3, 1, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 1f, 0f, -0.0931944251f),
					new float4(0f, -1f, 0f, -0.05930558f),
					new float4(0f, 0f, 1f, -0.07625076f),
					new float4(0f, 0f, -1f, -0.0762492344f),
					new float4(-1f, 0f, 0f, -1.08444417f),
					new float4(1f, 0f, 0f, -1.35555589f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 3.316179E-15f, 1.97659727E-22f, 0f), new float4(-3.31617888E-15f, 0.99999994f, 5.960465E-08f, 0f), new float4(0f, -5.960465E-08f, 0.99999994f, 0f), new float4(-15.99549f, 1.27930629f, 31.64375f, 1f)), name: "Solid 628 #4431")
					.WithMesh(new float3[] { new float3(1.35555589f, 0.0931944251f, 0.07625076f), new float3(1.35555589f, 0.0931944251f, -0.0762492344f), new float3(-1.08444417f, 0.0931944251f, -0.0762492344f), new float3(-1.08444417f, 0.0931944251f, 0.07625076f), new float3(1.35555589f, -0.05930558f, -0.0762492344f), new float3(1.35555589f, -0.05930558f, 0.07625076f), new float3(-1.08444417f, -0.05930558f, 0.07625076f), new float3(-1.08444417f, -0.05930558f, -0.0762492344f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 5, 0, 3, 6, -1, 1, 4, 7, 2, -1, 2, 7, 6, 3, -1, 4, 1, 0, 5, -1 }))
				;
		}

		// bm_c2a5i, Details: the mesh has a triangle the exact CSG did not make; Solid 324429 #1864 face -1 near (58.56,
		// 3.66, 134.963) (3 brushes)
		static ContentsScene BmC2a5iDetails20Site1()
		{
			return new ContentsScene()
				.WithContentsList("Solid", "Water", "Glass", "Grate")
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(-0.120692149f, 0.992689967f, 0f, -0.417100847f),
					new float4(-0.386432976f, 0.9223175f, 3.32947752E-06f, -0.3010517f),
					new float4(-0.5944121f, 0.8041606f, -7.63934452E-07f, -0.257955939f),
					new float4(-0.8031347f, 0.595797539f, 1.42749423E-06f, -0.277466059f),
					new float4(-0.9228138f, 0.385246336f, 3.37083E-07f, -0.352587223f),
					new float4(-0.9926812f, 0.120764188f, 5.832299E-07f, -0.499677747f),
					new float4(1f, 0f, 0f, -0.6286226f),
					new float4(-0.242535621f, -0.9701425f, 0f, -0.8452304f),
					new float4(0f, 0f, 1f, -24.8829746f),
					new float4(-0.707121f, 2.11127681E-05f, -0.7070925f, -13.5016937f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(57.9313774f, 4.078399f, 154.685776f, 1f)), name: "Solid 324429 #1864")
					.WithMesh(new float3[] { new float3(0.6286226f, 0.496600837f, -19.7232971f), new float3(0.315045476f, 0.458475828f, -19.409708f), new float3(0.3155831f, 0.4585412f, 24.8829746f), new float3(0.6286226f, 0.496600837f, 24.8829746f), new float3(0.0178593379f, 0.333959579f, -19.1125126f), new float3(0.0172327943f, 0.333538264f, 24.8829746f), new float3(-0.238148734f, 0.144726127f, -18.8565f), new float3(-0.237908363f, 0.144945353f, 24.8829746f), new float3(-0.42915535f, -0.112751141f, -18.6654949f), new float3(-0.4292186f, -0.112940714f, 24.8829746f), new float3(-0.5532524f, -0.410011649f, -18.5414f), new float3(-0.553222954f, -0.409979016f, 24.8829746f), new float3(-0.591377437f, -0.7233992f, -18.5032845f), new float3(-0.5913527f, -0.7234054f, 24.8829746f), new float3(0.6286226f, -1.02839923f, -19.7233429f), new float3(0.6286226f, -1.02839923f, 24.8829746f) }, new int[] { 0, 1, 2, 3, -1, 1, 4, 5, 2, -1, 4, 6, 7, 5, -1, 6, 8, 9, 7, -1, 8, 10, 11, 9, -1, 10, 12, 13, 11, -1, 14, 0, 3, 15, -1, 12, 14, 15, 13, -1, 13, 15, 3, 2, 5, 7, 9, 11, -1, 14, 12, 10, 8, 6, 4, 1, 0, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 1f, 0f, -3.4397223f),
					new float4(-1f, 0f, 0f, -0.6777765f),
					new float4(1f, 0f, 0f, -0.5422235f),
					new float4(0f, 0f, 1f, -1.143753f),
					new float4(0f, 0f, -1f, -1.143747f),
					new float4(0f, -1f, 0f, -5.40527773f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(58.0177765f, -0.08472222f, 136.106247f, 1f)), name: "Solid 324456 #1867")
					.WithMesh(new float3[] { new float3(0.5422235f, 3.4397223f, -1.143747f), new float3(-0.6777765f, 3.4397223f, -1.143747f), new float3(-0.6777765f, 3.4397223f, 1.143753f), new float3(0.5422235f, 3.4397223f, 1.143753f), new float3(-0.6777765f, -5.40527773f, -1.143747f), new float3(-0.6777765f, -5.40527773f, 1.143753f), new float3(0.5422235f, -5.40527773f, 1.143753f), new float3(0.5422235f, -5.40527773f, -1.143747f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 2, 1, -1, 6, 7, 0, 3, -1, 5, 6, 3, 2, -1, 7, 4, 1, 0, -1, 4, 7, 6, 5, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 1f, 0f, -0.18638885f),
					new float4(0f, -1f, 0f, -0.118611142f),
					new float4(0f, 0f, -1f, -15.8600006f),
					new float4(0f, 0f, 1f, -15.8599987f),
					new float4(1f, 0f, 0f, -10.9291677f),
					new float4(-1f, 0f, 0f, -8.743333f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(47.6308327f, 4.693611f, 119.1025f, 1f)), name: "Solid 331363 #1870")
					.WithMesh(new float3[] { new float3(-8.743333f, 0.18638885f, -15.8600006f), new float3(-8.743333f, 0.18638885f, 15.8599987f), new float3(10.9291677f, 0.18638885f, 15.8599987f), new float3(10.9291677f, 0.18638885f, -15.8600006f), new float3(-8.743333f, -0.118611142f, 15.8599987f), new float3(-8.743333f, -0.118611142f, -15.8600006f), new float3(10.9291677f, -0.118611142f, -15.8600006f), new float3(10.9291677f, -0.118611142f, 15.8599987f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 5, 0, 3, 6, -1, 1, 4, 7, 2, -1, 2, 7, 6, 3, -1, 4, 1, 0, 5, -1 }))
				;
		}

		// bm_c2a5i, func_brush 674043: the exact CSG made a triangle the mesh does not have; Solid 653212 #3891 face -1 near
		// (-9.303, -4.097, 173.545) (3 brushes)
		static ContentsScene BmC2a5iFuncBrush67404328Site1()
		{
			return new ContentsScene()
				.WithContentsList("Solid", "Water", "Glass", "Grate")
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(-0.08853979f, -0.99607265f, 0f, -0.13670741f),
					new float4(0.0885446742f, 0.996072233f, 0f, -0.0911385342f),
					new float4(-1f, 0f, 0f, -1.14371228f),
					new float4(0.132170945f, 0f, 0.9912269f, -0.0314848535f),
					new float4(-0.104985848f, -8.75736441E-05f, -0.9944737f, -0.031593807f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(-8.158788f, -4.158167f, 173.360733f, 1f)), name: "Solid 653212 #3891")
					.WithMesh(new float3[] { new float3(-1.14371228f, -0.0355831161f, 0.08897462f), new float3(2.28662157f, -0.340501666f, -0.273136318f), new float3(-1.14371228f, -0.0355831161f, 0.184266984f), new float3(2.2873466f, -0.111833073f, -0.273233f), new float3(-1.14371228f, 0.193166882f, 0.08895447f), new float3(-1.14371228f, 0.193166882f, 0.184266984f) }, new int[] { 0, 1, 2, -1, 3, 4, 5, -1, 4, 0, 2, 5, -1, 1, 3, 5, 2, -1, 3, 1, 0, 4, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(-0.0890408754f, -0.996027946f, 0f, -0.136704937f),
					new float4(0.0890408754f, 0.996027946f, 0f, -0.09113647f),
					new float4(1f, 0f, 0f, -1.13726211f),
					new float4(0.132904381f, 0f, -0.991128862f, -0.0314947963f),
					new float4(-0.1599442f, 0f, 0.987126052f, -0.03135604f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(-10.4397621f, -3.95483327f, 173.328964f, 1f)), name: "Solid 651776 #3912")
					.WithMesh(new float3[] { new float3(1.13726211f, -0.23891674f, 0.216035753f), new float3(-2.274526f, 0.06608332f, -0.336776853f), new float3(1.13726211f, -0.23891674f, 0.12072327f), new float3(-2.274526f, 0.294833332f, -0.336776853f), new float3(1.13726211f, -0.0101667261f, 0.216035753f), new float3(1.13726211f, -0.0101667261f, 0.12072327f) }, new int[] { 0, 1, 2, -1, 3, 4, 5, -1, 4, 0, 2, 5, -1, 1, 3, 5, 2, -1, 3, 1, 0, 4, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(-0.08624456f, -0.9961247f, -0.0172489118f, -0.136714056f),
					new float4(0.08625058f, 0.996124148f, 0.0172501169f, -0.09114229f),
					new float4(-0.9805807f, 0f, -0.196116135f, -1.15139973f),
					new float4(-0.1321731f, 0f, 0.9912267f, -0.03232885f),
					new float4(0.159952343f, 0.000173288208f, -0.987124741f, -0.03239614f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(-11.5836954f, -3.85316658f, 173.208237f, 1f)), name: "Solid 651777 #3913")
					.WithMesh(new float3[] { new float3(-1.13098431f, -0.0355834253f, -0.216088086f), new float3(2.282348f, -0.34068644f, 0.336950034f), new float3(-1.15005457f, -0.0355834253f, -0.120736696f), new float3(2.280948f, -0.111833453f, 0.336763352f), new float3(-1.130992f, 0.193166569f, -0.2160492f), new float3(-1.15005457f, 0.193166569f, -0.120736696f) }, new int[] { 0, 1, 2, -1, 3, 4, 5, -1, 4, 0, 2, 5, -1, 1, 3, 5, 2, -1, 3, 1, 0, 4, -1 }))
				;
		}

		// bm_c3a1a, Model: the mesh draws nothing where the exact CSG has a face; Water 661 #197 face 3 near (25, -22.989,
		// -45.903) (3 brushes)
		static ContentsScene BmC3a1aModel0Site1()
		{
			return new ContentsScene()
				.WithContentsList("Solid", "Water", "Glass", "Grate")
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 1f, 0f, -1.63090277f),
					new float4(0f, -1f, 0f, -2.56284738f),
					new float4(-1f, 0f, 0f, -25.416666f),
					new float4(0f, 0f, -1f, -3.15590262f),
					new float4(1f, 0f, 0f, -20.333334f),
					new float4(0f, 0f, 1f, -2.52472234f),
				}, contents: 1, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(35.7866669f, -23.9721527f, -42.7465973f, 1f)), name: "Water 661 #197")
					.WithMesh(new float3[] { new float3(-25.416666f, 1.63090277f, 2.52472234f), new float3(20.333334f, 1.63090277f, 2.52472234f), new float3(20.333334f, 1.63090277f, -3.15590262f), new float3(-25.416666f, 1.63090277f, -3.15590262f), new float3(20.333334f, -2.56284738f, 2.52472234f), new float3(-25.416666f, -2.56284738f, 2.52472234f), new float3(-25.416666f, -2.56284738f, -3.15590262f), new float3(20.333334f, -2.56284738f, -3.15590262f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 5, 0, 3, 6, -1, 2, 7, 6, 3, -1, 1, 4, 7, 2, -1, 4, 1, 0, 5, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 0f, 1f, -0.1525003f),
					new float4(0f, 0f, -1f, -0.152499691f),
					new float4(0f, -1f, 0f, -0.8006251f),
					new float4(0f, 1f, 0f, -1.258125f),
					new float4(-1f, 0f, 0f, -1.22000086f),
					new float4(1f, 0f, 0f, -1.52499914f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(25.62f, -23.7518749f, -46.055f, 1f)), name: "Solid 792 #280")
					.WithMesh(new float3[] { new float3(1.52499914f, -0.8006251f, 0.1525003f), new float3(1.52499914f, 1.258125f, 0.1525003f), new float3(-1.22000086f, 1.258125f, 0.1525003f), new float3(-1.22000086f, -0.8006251f, 0.1525003f), new float3(1.52499914f, 1.258125f, -0.152499691f), new float3(1.52499914f, -0.8006251f, -0.152499691f), new float3(-1.22000086f, -0.8006251f, -0.152499691f), new float3(-1.22000086f, 1.258125f, -0.152499691f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 5, 0, 3, 6, -1, 1, 4, 7, 2, -1, 2, 7, 6, 3, -1, 4, 1, 0, 5, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 1f, 0f, -0.444791257f),
					new float4(0f, -1f, 0f, -0.698958755f),
					new float4(-1f, 0f, 0f, -1.52499926f),
					new float4(1f, 0f, 0f, -1.22000074f),
					new float4(0f, 0f, 1f, -2.43999887f),
					new float4(0f, 0f, -1f, -2.44000125f),
				}, contents: 1, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(25.925f, -22.7860413f, -48.3425f, 1f)), name: "Water 828 #302")
					.WithMesh(new float3[] { new float3(1.22000074f, 0.444791257f, -2.44000125f), new float3(-1.52499926f, 0.444791257f, -2.44000125f), new float3(-1.52499926f, 0.444791257f, 2.43999887f), new float3(1.22000074f, 0.444791257f, 2.43999887f), new float3(-1.52499926f, -0.698958755f, -2.44000125f), new float3(1.22000074f, -0.698958755f, -2.44000125f), new float3(1.22000074f, -0.698958755f, 2.43999887f), new float3(-1.52499926f, -0.698958755f, 2.43999887f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 1, 4, 7, 2, -1, 5, 0, 3, 6, -1, 6, 3, 2, 7, -1, 0, 5, 4, 1, -1 }))
				;
		}

		// bm_c3a1a, Model: the mesh draws nothing where the exact CSG has a face; Water 661 #197 face 3 near (41.75, -23.18,
		// -45.903) (3 brushes)
		static ContentsScene BmC3a1aModel0Site2()
		{
			return new ContentsScene()
				.WithContentsList("Solid", "Water", "Glass", "Grate")
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 1f, 0f, -1.63090277f),
					new float4(0f, -1f, 0f, -2.56284738f),
					new float4(-1f, 0f, 0f, -25.416666f),
					new float4(0f, 0f, -1f, -3.15590262f),
					new float4(1f, 0f, 0f, -20.333334f),
					new float4(0f, 0f, 1f, -2.52472234f),
				}, contents: 1, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(35.7866669f, -23.9721527f, -42.7465973f, 1f)), name: "Water 661 #197")
					.WithMesh(new float3[] { new float3(-25.416666f, 1.63090277f, 2.52472234f), new float3(20.333334f, 1.63090277f, 2.52472234f), new float3(20.333334f, 1.63090277f, -3.15590262f), new float3(-25.416666f, 1.63090277f, -3.15590262f), new float3(20.333334f, -2.56284738f, 2.52472234f), new float3(-25.416666f, -2.56284738f, 2.52472234f), new float3(-25.416666f, -2.56284738f, -3.15590262f), new float3(20.333334f, -2.56284738f, -3.15590262f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 5, 0, 3, 6, -1, 2, 7, 6, 3, -1, 1, 4, 7, 2, -1, 4, 1, 0, 5, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(-1f, 0f, 0f, -1.22000074f),
					new float4(1f, 0f, 0f, -1.52499926f),
					new float4(0f, 0f, 1f, -0.07624832f),
					new float4(0f, 0f, -1f, -0.07625168f),
					new float4(0f, -1f, 0f, -0.652361155f),
					new float4(0f, 1f, 0f, -1.02513885f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(42.7f, -23.9001389f, -45.97875f, 1f)), name: "Solid 666 #200")
					.WithMesh(new float3[] { new float3(-1.22000074f, 1.02513885f, 0.07624832f), new float3(-1.22000074f, 1.02513885f, -0.07625168f), new float3(-1.22000074f, -0.652361155f, -0.07625168f), new float3(-1.22000074f, -0.652361155f, 0.07624832f), new float3(1.52499926f, 1.02513885f, -0.07625168f), new float3(1.52499926f, 1.02513885f, 0.07624832f), new float3(1.52499926f, -0.652361155f, 0.07624832f), new float3(1.52499926f, -0.652361155f, -0.07625168f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 5, 0, 3, 6, -1, 1, 4, 7, 2, -1, 2, 7, 6, 3, -1, 4, 1, 0, 5, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 1f, 0f, -0.444791257f),
					new float4(0f, -1f, 0f, -0.698958755f),
					new float4(-1f, 0f, 0f, -1.56735945f),
					new float4(0f, 0f, -1f, -2.054513f),
					new float4(1f, 0f, 0f, -1.25389051f),
					new float4(0f, 0f, 1f, -1.643612f),
				}, contents: 1, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(43.04736f, -22.7860413f, -47.5461121f, 1f)), name: "Water 701 #216")
					.WithMesh(new float3[] { new float3(-1.56735945f, 0.444791257f, 1.643612f), new float3(1.25389051f, 0.444791257f, 1.643612f), new float3(1.25389051f, 0.444791257f, -2.054513f), new float3(-1.56735945f, 0.444791257f, -2.054513f), new float3(1.25389051f, -0.698958755f, 1.643612f), new float3(-1.56735945f, -0.698958755f, 1.643612f), new float3(-1.56735945f, -0.698958755f, -2.054513f), new float3(1.25389051f, -0.698958755f, -2.054513f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 5, 0, 3, 6, -1, 2, 7, 6, 3, -1, 1, 4, 7, 2, -1, 4, 1, 0, 5, -1 }))
				;
		}

		// bm_c3a1a, Model: the mesh draws nothing where the exact CSG has a face; Water 661 #197 face 3 near (44.25, -23.18,
		// -45.903) (3 brushes)
		static ContentsScene BmC3a1aModel0Site3()
		{
			return new ContentsScene()
				.WithContentsList("Solid", "Water", "Glass", "Grate")
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 1f, 0f, -0.6523609f),
					new float4(-1f, 0f, 0f, -4.33777857f),
					new float4(1f, 0f, 0f, -5.42222166f),
					new float4(0f, 0f, -1f, -0.667186558f),
					new float4(0f, 0f, 1f, -0.6671884f),
					new float4(0f, -1f, 0f, -1.02513909f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(48.56278f, -23.52736f, -45.2353134f, 1f)), name: "Solid 646 #191")
					.WithMesh(new float3[] { new float3(-4.33777857f, 0.6523609f, 0.6671884f), new float3(5.42222166f, 0.6523609f, 0.6671884f), new float3(5.42222166f, 0.6523609f, -0.667186558f), new float3(-4.33777857f, 0.6523609f, -0.667186558f), new float3(-4.33777857f, -1.02513909f, -0.667186558f), new float3(-4.33777857f, -1.02513909f, 0.6671884f), new float3(5.42222166f, -1.02513909f, 0.6671884f), new float3(5.42222166f, -1.02513909f, -0.667186558f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 0, 3, -1, 6, 7, 2, 1, -1, 7, 4, 3, 2, -1, 5, 6, 1, 0, -1, 6, 5, 4, 7, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 1f, 0f, -1.63090277f),
					new float4(0f, -1f, 0f, -2.56284738f),
					new float4(-1f, 0f, 0f, -25.416666f),
					new float4(0f, 0f, -1f, -3.15590262f),
					new float4(1f, 0f, 0f, -20.333334f),
					new float4(0f, 0f, 1f, -2.52472234f),
				}, contents: 1, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(35.7866669f, -23.9721527f, -42.7465973f, 1f)), name: "Water 661 #197")
					.WithMesh(new float3[] { new float3(-25.416666f, 1.63090277f, 2.52472234f), new float3(20.333334f, 1.63090277f, 2.52472234f), new float3(20.333334f, 1.63090277f, -3.15590262f), new float3(-25.416666f, 1.63090277f, -3.15590262f), new float3(20.333334f, -2.56284738f, 2.52472234f), new float3(-25.416666f, -2.56284738f, 2.52472234f), new float3(-25.416666f, -2.56284738f, -3.15590262f), new float3(20.333334f, -2.56284738f, -3.15590262f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 5, 0, 3, 6, -1, 2, 7, 6, 3, -1, 1, 4, 7, 2, -1, 4, 1, 0, 5, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 1f, 0f, -0.444791257f),
					new float4(0f, -1f, 0f, -0.698958755f),
					new float4(-1f, 0f, 0f, -1.56735945f),
					new float4(0f, 0f, -1f, -2.054513f),
					new float4(1f, 0f, 0f, -1.25389051f),
					new float4(0f, 0f, 1f, -1.643612f),
				}, contents: 1, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(43.04736f, -22.7860413f, -47.5461121f, 1f)), name: "Water 701 #216")
					.WithMesh(new float3[] { new float3(-1.56735945f, 0.444791257f, 1.643612f), new float3(1.25389051f, 0.444791257f, 1.643612f), new float3(1.25389051f, 0.444791257f, -2.054513f), new float3(-1.56735945f, 0.444791257f, -2.054513f), new float3(1.25389051f, -0.698958755f, 1.643612f), new float3(-1.56735945f, -0.698958755f, 1.643612f), new float3(-1.56735945f, -0.698958755f, -2.054513f), new float3(1.25389051f, -0.698958755f, -2.054513f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 5, 0, 3, 6, -1, 2, 7, 6, 3, -1, 1, 4, 7, 2, -1, 4, 1, 0, 5, -1 }))
				;
		}

		// bm_c3a1a, Details: the mesh has a triangle the exact CSG did not make; Solid 1526657 #3106 face -1 near (83.902,
		// -4.918, 12.14) (4 brushes)
		static ContentsScene BmC3a1aDetails26Site1()
		{
			return new ContentsScene()
				.WithContentsList("Solid", "Water", "Glass", "Grate")
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0.122795776f, -0.9693791f, -0.212662548f, -0.203444123f),
					new float4(-0.1269061f, 0.9631362f, 0.237199321f, -0.339557f),
					new float4(0.289399564f, -0.318106264f, 0.9028047f, -0.223085821f),
					new float4(-0.8681457f, -0.0023566f, -0.496303856f, -0.282357484f),
					new float4(-0.446962237f, -0.4482721f, 0.774129748f, -0.0665031448f),
					new float4(0.4404195f, 0.4735424f, -0.762750447f, -0.0800085f),
					new float4(-0.909052253f, -0.334972f, 0.247826114f, -0.186789125f),
					new float4(0.868297756f, 0.00298253959f, 0.4960344f, -0.327539563f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(84.05197f, -4.75331545f, 12.43386f, 1f)), name: "Solid 1526657 #3106")
					.WithMesh(new float3[] { new float3(0.372999638f, -0.1644587f, 0.0083771f), new float3(0.335055262f, -0.1838623f, 0.07491457f), new float3(0.230757326f, -0.202934548f, 0.101627745f), new float3(-0.165339f, -0.202938169f, -0.12707f), new float3(-0.197554976f, -0.18609038f, -0.222469434f), new float3(-0.155221164f, -0.164459839f, -0.2966234f), new float3(0.2114343f, 0.3093998f, 0.2883446f), new float3(0.240923882f, 0.326023161f, 0.236623749f), new float3(-0.288390756f, 0.330816329f, -0.0660319f), new float3(-0.322209f, 0.3117693f, -0.00678584026f), new float3(-0.289224982f, 0.2926747f, 0.08839397f), new float3(0.106536388f, 0.289061725f, 0.314804077f) }, new int[] { 0, 1, 2, 3, 4, 5, -1, 6, 7, 8, 9, 10, 11, -1, 1, 6, 11, 2, -1, 4, 9, 8, 5, -1, 10, 3, 2, 11, -1, 7, 0, 5, 8, -1, 3, 10, 9, 4, -1, 0, 7, 6, 1, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0.0615083f, -0.992407f, -0.106513277f, -0.246068165f),
					new float4(-0.119893752f, 0.970134556f, 0.210865974f, -0.415252358f),
					new float4(0.276084274f, -0.204985082f, 0.939020038f, -0.227888688f),
					new float4(-0.864427745f, 0.00115431973f, -0.5027557f, -0.282784671f),
					new float4(-0.478798926f, -0.2886058f, 0.829131f, -0.0704391152f),
					new float4(0.47979486f, 0.2807898f, -0.8312364f, -0.08275562f),
					new float4(-0.9548229f, -0.201570347f, 0.218363434f, -0.197366878f),
					new float4(0.862144f, -0.00130160886f, 0.506661654f, -0.326668024f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(84.1601639f, -5.33750153f, 12.247036f, 1f)), name: "Solid 1526660 #3109")
					.WithMesh(new float3[] { new float3(0.360297948f, -0.228954524f, 0.0310686547f), new float3(0.322152019f, -0.238282844f, 0.0959544554f), new float3(0.217898086f, -0.247810975f, 0.124526523f), new float3(-0.17822586f, -0.247810975f, -0.104223482f), new float3(-0.204722732f, -0.23799096f, -0.21101974f), new float3(-0.168115914f, -0.228969f, -0.273940027f), new float3(0.224562153f, 0.3984818f, 0.263650864f), new float3(0.2650602f, 0.41845414f, 0.194790021f), new float3(-0.262830883f, 0.419376463f, -0.109600469f), new float3(-0.3012003f, 0.400304675f, -0.04367268f), new float3(-0.2735369f, 0.381253868f, 0.0597035028f), new float3(0.124321289f, 0.38053894f, 0.289206117f) }, new int[] { 0, 1, 2, 3, 4, 5, -1, 6, 7, 8, 9, 10, 11, -1, 1, 6, 11, 2, -1, 4, 9, 8, 5, -1, 10, 3, 2, 11, -1, 7, 0, 5, 8, -1, 3, 10, 9, 4, -1, 0, 7, 6, 1, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, -1f, 0f, -0.252048641f),
					new float4(0f, 1f, 0f, -0.396076351f),
					new float4(0.8666894f, -1.52236389E-05f, 0.4988482f, -0.3391258f),
					new float4(-0.866688669f, -0.00126109843f, -0.4988478f, -0.271309376f),
					new float4(-0.479773521f, -0.280935735f, 0.8311994f, -0.177077815f),
					new float4(0.499906451f, -8.971756E-05f, -0.8660794f, -0.195031121f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(84.28197f, -5.31420135f, 12.0126505f, 1f)), name: "Solid 1526664 #3113")
					.WithMesh(new float3[] { new float3(-0.137420818f, -0.252048641f, -0.304482549f), new float3(0.3909865f, -0.252048641f, 0.000517459353f), new float3(0.2384704f, -0.252048641f, 0.265496016f), new float3(-0.2899369f, -0.252048641f, -0.0395040065f), new float3(0.391024053f, 0.396076351f, 0.0004719954f), new float3(-0.1380997f, 0.396076351f, -0.304941535f), new float3(-0.385287464f, 0.396076351f, 0.124517761f), new float3(0.143836275f, 0.396076351f, 0.4299313f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 1, 4, 7, 2, -1, 5, 0, 3, 6, -1, 6, 3, 2, 7, -1, 0, 5, 4, 1, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, -1f, 0f, -0.192743152f),
					new float4(0f, 1f, 0f, -0.302881837f),
					new float4(0.865865052f, 0f, 0.5002776f, -0.338990241f),
					new float4(-0.865855038f, -0.000265786f, -0.500294864f, -0.2711713f),
					new float4(-0.440257579f, -0.4736369f, 0.762785256f, -0.257249445f),
					new float4(0.500014246f, -0.0001341767f, -0.866017163f, -0.306882f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(84.22582f, -4.725382f, 12.1094446f, 1f)), name: "Solid 1526665 #3114")
					.WithMesh(new float3[] { new float3(-0.0812761f, -0.192743152f, -0.401256859f), new float3(0.447084635f, -0.192743152f, -0.09619606f), new float3(0.199326783f, -0.192743152f, 0.332615644f), new float3(-0.3290886f, -0.192743152f, 0.0276295766f), new float3(0.447117925f, 0.302881837f, -0.09625364f), new float3(-0.08135691f, 0.302881837f, -0.40138033f), new float3(-0.4625508f, 0.302881837f, 0.2583479f), new float3(0.06598328f, 0.302881837f, 0.5634025f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 1, 4, 7, 2, -1, 5, 0, 3, 6, -1, 6, 3, 2, 7, -1, 0, 5, 4, 1, -1 }))
				;
		}

		// bm_c3a1b, Details: the exact CSG made a triangle the mesh does not have; Solid 1393 #3147 face -1 near (28.142,
		// -1.83, -34.846) (3 brushes)
		static ContentsScene BmC3a1bDetails130Site1()
		{
			return new ContentsScene()
				.WithContentsList("Solid", "Water", "Glass", "Grate")
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(-0.04598326f, 0.992721558f, 0.111308113f, -0.468853354f),
					new float4(-0.1476987f, 0.922151f, 0.357522815f, -0.338357478f),
					new float4(-0.2277572f, 0.80343926f, 0.550101757f, -0.279775649f),
					new float4(-0.307786077f, 0.5948965f, 0.7425402f, -0.279687017f),
					new float4(-0.353067815f, 0.3860026f, 0.8522588f, -0.33820343f),
					new float4(-0.380212218f, 0.1208082f, 0.9169755f, -0.467619628f),
					new float4(0.383153647f, -0.000109885463f, -0.9236846f, -0.66789f),
					new float4(-0.7041384f, -0.000978213f, -0.7100621f, -0.2333601f),
					new float4(0.9803065f, 0.00183872762f, 0.197473511f, -0.3354426f),
					new float4(0f, -1f, 0f, -0.6667132f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 0.99999994f, 2.13162821E-14f, 0f), new float4(0f, -2.13162821E-14f, 0.99999994f, 0f), new float4(20.53549f, -1.163288f, -34.3089f, 1f)), name: "Solid 1387 #3141")
					.WithMesh(new float3[] { new float3(0.38610515f, 0.5151618f, -0.222845256f), new float3(0.4492718f, 0.5532868f, -0.5367749f), new float3(0.279940754f, 0.5533189f, -0.6070151f), new float3(0.0377246663f, 0.5151618f, -0.3667671f), new float3(0.326797843f, 0.391068131f, 0.072726056f), new float3(-0.189228863f, 0.3914881f, -0.141536564f), new float3(0.2752102f, 0.199882179f, 0.330599844f), new float3(-0.387625f, 0.200361565f, 0.0554677658f), new float3(0.237280533f, -0.0577593f, 0.521290958f), new float3(-0.5347287f, -0.05827198f, 0.201700553f), new float3(0.212973222f, -0.3524743f, 0.644702554f), new float3(-0.6289103f, -0.351519436f, 0.295500457f), new float3(-0.65812993f, -0.6667132f, 0.324910551f), new float3(0.205820456f, -0.6667132f, 0.6831366f), new float3(0.451356679f, -0.6667132f, -0.535764933f), new float3(0.2810326f, -0.6667132f, -0.60641706f) }, new int[] { 0, 1, 2, 3, -1, 4, 0, 3, 5, -1, 6, 4, 5, 7, -1, 8, 6, 7, 9, -1, 10, 8, 9, 11, -1, 12, 13, 10, 11, -1, 14, 15, 2, 1, -1, 15, 12, 11, 9, 7, 5, 3, 2, -1, 13, 14, 1, 0, 4, 6, 8, 10, -1, 14, 13, 12, 15, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(-4.049668E-06f, 0.992725134f, 0.120402552f, -0.5184673f),
					new float4(-6.652654E-06f, 0.92215246f, 0.386826664f, -0.38616842f),
					new float4(-2.33754818E-05f, 0.803431749f, 0.5953968f, -0.323216438f),
					new float4(-1.90661049E-05f, 0.5948407f, 0.8038436f, -0.31401965f),
					new float4(-2.24464748E-05f, 0.3862915f, 0.922376752f, -0.362951428f),
					new float4(-3.28801143E-05f, 0.120182842f, 0.9927518f, -0.480162919f),
					new float4(3.36355333E-05f, 1.05213687E-08f, -1f, -0.6615831f),
					new float4(-0.9803594f, -0.000731520355f, -0.197218016f, -4.615688f),
					new float4(0f, -1f, 0f, -0.6174932f),
					new float4(0.707106769f, 0f, 0.707106769f, -4.319462f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 0.99999994f, 2.13162821E-14f, 0f), new float4(0f, -2.13162821E-14f, 0.99999994f, 0f), new float4(25.5600033f, -1.21250522f, -34.18489f, 1f)), name: "Solid 1393 #3147")
					.WithMesh(new float3[] { new float3(6.455653f, 0.564380348f, -0.3470116f), new float3(6.769997f, 0.602506757f, -0.6613554f), new float3(-4.57548761f, 0.602506757f, -0.661737f), new float3(-4.63874865f, 0.564349234f, -0.3471282f), new float3(6.159449f, 0.440125823f, -0.05080751f), new float3(-4.698118f, 0.440357357f, -0.0515462458f), new float3(5.90279245f, 0.249918655f, 0.205849f), new float3(-4.74974537f, 0.249646977f, 0.205797374f), new float3(5.71128845f, -0.008878364f, 0.3973531f), new float3(-4.78803444f, -0.008858198f, 0.397089154f), new float3(5.588104f, -0.3030226f, 0.520537734f), new float3(-4.81257248f, -0.302715719f, 0.520156145f), new float3(5.550035f, -0.6174932f, 0.5586064f), new float3(-4.82000351f, -0.6174932f, 0.558262944f), new float3(6.769997f, -0.6174932f, -0.661355436f), new float3(-4.57457733f, -0.6174932f, -0.661737f) }, new int[] { 0, 1, 2, 3, -1, 4, 0, 3, 5, -1, 6, 4, 5, 7, -1, 8, 6, 7, 9, -1, 10, 8, 9, 11, -1, 12, 10, 11, 13, -1, 1, 14, 15, 2, -1, 15, 13, 11, 9, 7, 5, 3, 2, -1, 14, 12, 13, 15, -1, 12, 14, 1, 0, 4, 6, 8, 10, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(1f, 0f, 0f, -4.608888f),
					new float4(0f, 0f, -1f, -0.609998167f),
					new float4(0f, 0f, 1f, -0.6100018f),
					new float4(-1f, 0f, 0f, -5.761112f),
					new float4(0f, -1f, 0f, -2.135f),
					new float4(0f, 1f, 0f, -3.355f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 0.99999994f, 2.13162821E-14f, 0f), new float4(0f, -2.13162821E-14f, 0.99999994f, 0f), new float4(26.501112f, -5.185001f, -34.236248f, 1f)), name: "Solid 1401 #3154")
					.WithMesh(new float3[] { new float3(4.608888f, 3.355f, -0.609998167f), new float3(4.608888f, 3.355f, 0.6100018f), new float3(4.608888f, -2.135f, 0.6100018f), new float3(4.608888f, -2.135f, -0.609998167f), new float3(-5.761112f, 3.355f, -0.609998167f), new float3(-5.761112f, -2.135f, -0.609998167f), new float3(-5.761112f, 3.355f, 0.6100018f), new float3(-5.761112f, -2.135f, 0.6100018f) }, new int[] { 0, 1, 2, 3, -1, 4, 0, 3, 5, -1, 1, 6, 7, 2, -1, 6, 4, 5, 7, -1, 7, 5, 3, 2, -1, 4, 6, 1, 0, -1 }))
				;
		}

		// bm_c3a1b, Details: the exact CSG made a triangle the mesh does not have; Solid 531871 #1957 face -1 near (26.148,
		// -24.459, -10.822) (6 brushes)
		static ContentsScene BmC3a1bDetails34Site1()
		{
			return new ContentsScene()
				.WithContentsList("Solid", "Water", "Glass", "Grate")
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(-0.9659422f, -6.165032E-05f, -0.258758f, -0.9645563f),
					new float4(0.866028f, 1.60401614E-05f, 0.4999955f, -0.7792502f),
					new float4(0.332942456f, -0.49300608f, -0.8037999f, -0.485747784f),
					new float4(-0.366368055f, 0.2889081f, 0.884480953f, -0.3938657f),
					new float4(0.147173762f, 0.9230962f, -0.355293274f, -0.253370553f),
					new float4(-0.147652075f, -0.92257f, 0.3564597f, -0.202747717f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(23.8401241f, -24.6245136f, -12.0069065f, 1f)), name: "Solid 531847 #1933")
					.WithMesh(new float3[] { new float3(-0.8072854f, -0.366419852f, -0.713959f), new float3(-1.00953889f, -0.0423613749f, 0.04097482f), new float3(-0.975031435f, 0.396083385f, -0.0879457742f), new float3(-0.748105466f, 0.03388863f, -0.9349729f), new float3(0.5122196f, -0.04236137f, 0.6713148f), new float3(0.9030017f, -0.366423875f, -0.00553742843f), new float3(1.01738989f, 0.0338769928f, -0.203678861f), new float3(0.578938365f, 0.396076143f, 0.555739045f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 5, 0, 3, 6, -1, 1, 4, 7, 2, -1, 2, 7, 6, 3, -1, 4, 1, 0, 5, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(-0.866028547f, -1.57183949E-05f, -0.4999946f, -0.964527667f),
					new float4(0.7070778f, 1.43498692E-05f, 0.707135737f, -0.7792559f),
					new float4(0.5296709f, -0.492945671f, -0.690256f, -0.485738933f),
					new float4(-0.582816541f, 0.288892657f, 0.7595169f, -0.393868536f),
					new float4(0.234101072f, 0.923102f, -0.305089027f, -0.25336495f),
					new float4(-0.234884366f, -0.9225691f, 0.306097448f, -0.202748671f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(25.3380756f, -24.6245136f, -11.1138792f, 1f)), name: "Solid 531859 #1945")
					.WithMesh(new float3[] { new float3(-0.5949509f, -0.366424918f, -0.8985645f), new float3(-0.98573184f, -0.0423613861f, -0.22171253f), new float3(-0.9190177f, 0.396063954f, -0.337280363f), new float3(-0.4805765f, 0.033888638f, -1.09668231f), new float3(0.3210034f, -0.0423613675f, 0.781012952f), new float3(0.8736762f, -0.366423875f, 0.22839202f), new float3(1.03544211f, 0.03390232f, 0.06663123f), new float3(0.415361851f, 0.396076143f, 0.6866533f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 5, 0, 3, 6, -1, 1, 4, 7, 2, -1, 2, 7, 6, 3, -1, 4, 1, 0, 5, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(-0.7070822f, -7.532688E-05f, -0.7071313f, -1.16957235f),
					new float4(0.499977976f, 9.7805816E-05f, 0.8660381f, -0.9371379f),
					new float4(0.396163523f, -0.8664011f, -0.3039797f, -0.484215766f),
					new float4(-0.5634214f, 0.704021335f, 0.4323312f, -0.3907362f),
					new float4(0.6365774f, 0.596794963f, -0.488472074f, -0.251498222f),
					new float4(-0.638772845f, -0.5930611f, 0.490150779f, -0.201360688f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(27.6712532f, -25.660244f, -10.6909857f, 1f)), name: "Solid 531869 #1955")
					.WithMesh(new float3[] { new float3(-0.63721174f, -0.49351877f, -1.01674759f), new float3(-0.9877556f, 0.173681453f, -0.666299164f), new float3(-0.758648634f, 0.497736841f, -0.8954247f), new float3(-0.35412842f, -0.264756f, -1.29983568f), new float3(0.245205626f, 0.173681483f, 0.940516949f), new float3(0.6745217f, -0.493505985f, 0.6927411f), new float3(1.02122939f, -0.264737338f, 0.492555231f), new float3(0.525805056f, 0.497744f, 0.7784857f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 5, 0, 3, 6, -1, 1, 4, 7, 2, -1, 2, 7, 6, 3, -1, 4, 1, 0, 5, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(-0.707024455f, 0.000101969054f, -0.7071891f, -1.07718873f),
					new float4(0.5000333f, 3.25542533E-05f, 0.8660062f, -0.866696656f),
					new float4(0.563397467f, -0.704051733f, -0.432312846f, -0.4816726f),
					new float4(-0.6902896f, 0.492871284f, 0.5296962f, -0.391000569f),
					new float4(0.478638679f, 0.7975096f, -0.367264777f, -0.252526015f),
					new float4(-0.4757154f, -0.8002745f, 0.3650419f, -0.201854289f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(27.1696758f, -25.0502434f, -10.3200893f, 1f)), name: "Solid 531870 #1956")
					.WithMesh(new float3[] { new float3(-0.486186624f, -0.436331034f, -1.0371871f), new float3(-0.9579751f, 0.0593059f, -0.565437f), new float3(-0.7961975f, 0.45964542f, -0.7271192f), new float3(-0.256995052f, -0.112256624f, -1.26627862f), new float3(0.1689998f, 0.05930588f, 0.903214335f), new float3(0.7467827f, -0.436319143f, 0.5696202f), new float3(1.0273962f, -0.11226856f, 0.407581359f), new float3(0.367059469f, 0.45961836f, 0.78883934f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 5, 0, 3, 6, -1, 1, 4, 7, 2, -1, 2, 7, 6, 3, -1, 4, 1, 0, 5, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(-0.707129061f, 7.721881E-06f, -0.7070845f, -0.96456176f),
					new float4(0.5000056f, -0.000170859363f, 0.86602217f, -0.7792333f),
					new float4(0.690213144f, -0.493041426f, -0.5296376f, -0.485753119f),
					new float4(-0.759482443f, 0.289115071f, 0.582751155f, -0.393854082f),
					new float4(0.3051386f, 0.9230801f, -0.234122962f, -0.253385752f),
					new float4(-0.306087673f, -0.9225782f, 0.234861165f, -0.202749223f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(26.5538731f, -24.6245136f, -9.863577f, 1f)), name: "Solid 531871 #1957")
					.WithMesh(new float3[] { new float3(-0.342163861f, -0.366402864f, -1.02195787f), new float3(-0.894794f, -0.04236136f, -0.4692894f), new float3(-0.800353f, 0.396088958f, -0.5637316f), new float3(-0.180375859f, 0.03388863f, -1.1837517f), new float3(0.107893012f, -0.04236138f, 0.8374831f), new float3(0.7847481f, -0.366423875f, 0.446630865f), new float3(0.9829353f, 0.0338537581f, 0.3322847f), new float3(0.223601371f, 0.3960761f, 0.770764351f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 5, 0, 3, 6, -1, 1, 4, 7, 2, -1, 2, 7, 6, 3, -1, 4, 1, 0, 5, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(-0.707164645f, 0f, -0.707048953f, -0.840385258f),
					new float4(0.5000116f, 0f, 0.8660187f, -0.682525337f),
					new float4(0.7595361f, -0.2888911f, -0.5827923f, -0.4798881f),
					new float4(-0.7933452f, 0f, 0.608772039f, -0.390550554f),
					new float4(0.110926226f, 0.99017626f, -0.08512546f, -0.2515861f),
					new float4(-0.110583492f, -0.9902377f, 0.08485604f, -0.201255172f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(25.8741245f, -24.4042358f, -9.359365f, 1f)), name: "Solid 531872 #1958")
					.WithMesh(new float3[] { new float3(-0.215046883f, -0.26264587f, -0.973499358f), new float3(-0.794545949f, -0.14826417f, -0.3939055f), new float3(-0.794545949f, 0.30922848f, -0.3939055f), new float3(-0.120693184f, 0.175798342f, -1.06786847f), new float3(0.07794508f, -0.14826417f, 0.7431155f), new float3(0.787688136f, -0.262639165f, 0.333332419f), new float3(0.9032562f, 0.175813511f, 0.266607136f), new float3(0.07794508f, 0.309235841f, 0.7431155f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 5, 0, 3, 6, -1, 1, 4, 7, 2, -1, 2, 7, 6, 3, -1, 4, 1, 0, 5, -1 }))
				;
		}

		// bm_c3a2c, Model: the mesh draws nothing where the exact CSG has a face; Solid 2476171 #185 face 0 near (33.398,
		// -13, 34.465) (3 brushes)
		static ContentsScene BmC3a2cModel0Site1()
		{
			return new ContentsScene()
				.WithContentsList("Solid", "Water", "Glass", "Grate")
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(1f, 0f, 0f, -5.422223f),
					new float4(-1f, 0f, 0f, -4.337777f),
					new float4(0f, 0f, 1f, -3.81249976f),
					new float4(0f, 0f, -1f, -3.81250024f),
					new float4(0f, -1f, 0f, -2.84666634f),
					new float4(0f, 1f, 0f, -4.47333336f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(27.9752769f, -10.4208336f, 34.465f, 1f)), name: "Solid 2476171 #185")
					.WithMesh(new float3[] { new float3(5.422223f, 4.47333336f, -3.81250024f), new float3(5.422223f, 4.47333336f, 3.81249976f), new float3(5.422223f, -2.84666634f, 3.81249976f), new float3(5.422223f, -2.84666634f, -3.81250024f), new float3(-4.337777f, 4.47333336f, 3.81249976f), new float3(-4.337777f, 4.47333336f, -3.81250024f), new float3(-4.337777f, -2.84666634f, -3.81250024f), new float3(-4.337777f, -2.84666634f, 3.81249976f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 1, 4, 7, 2, -1, 5, 0, 3, 6, -1, 6, 3, 2, 7, -1, 0, 5, 4, 1, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 1f, 0f, -9.319444f),
					new float4(0f, -1f, 0f, -5.930556f),
					new float4(0f, 0f, 1f, -1.21999967f),
					new float4(1f, 0f, 0f, -4.88f),
					new float4(0f, 0f, -1f, -1.52500033f),
					new float4(-1f, 0f, 0f, -4.88f),
				}, contents: 1, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(28.5175f, -21.976944f, 34.6175f, 1f)), name: "Water 2477629 #191")
					.WithMesh(new float3[] { new float3(-4.88f, 9.319444f, -1.52500033f), new float3(-4.88f, 9.319444f, 1.21999967f), new float3(4.88f, 9.319444f, 1.21999967f), new float3(4.88f, 9.319444f, -1.52500033f), new float3(-4.88f, -5.930556f, 1.21999967f), new float3(-4.88f, -5.930556f, -1.52500033f), new float3(4.88f, -5.930556f, -1.52500033f), new float3(4.88f, -5.930556f, 1.21999967f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 1, 4, 7, 2, -1, 6, 3, 2, 7, -1, 5, 0, 3, 6, -1, 0, 5, 4, 1, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 1f, 0f, -9.319444f),
					new float4(0f, -1f, 0f, -5.930556f),
					new float4(0f, 0f, 1f, -2.8974998f),
					new float4(0f, 0f, -1f, -2.8975f),
					new float4(1f, 0f, 0f, -2.11805439f),
					new float4(-1f, 0f, 0f, -1.69444561f),
				}, contents: 1, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(35.0919456f, -21.976944f, 34.465f, 1f)), name: "Water 3528854 #288")
					.WithMesh(new float3[] { new float3(-1.69444561f, 9.319444f, -2.8975f), new float3(-1.69444561f, 9.319444f, 2.8974998f), new float3(2.11805439f, 9.319444f, 2.8974998f), new float3(2.11805439f, 9.319444f, -2.8975f), new float3(-1.69444561f, -5.930556f, 2.8974998f), new float3(-1.69444561f, -5.930556f, -2.8975f), new float3(2.11805439f, -5.930556f, -2.8975f), new float3(2.11805439f, -5.930556f, 2.8974998f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 1, 4, 7, 2, -1, 5, 0, 3, 6, -1, 6, 3, 2, 7, -1, 0, 5, 4, 1, -1 }))
				;
		}

		// bm_c3a2c, Model: the mesh draws nothing where the exact CSG has a face; Water 2477638 #192 face 3 near (23.638,
		// -13, 34.465) (3 brushes)
		static ContentsScene BmC3a2cModel0Site2()
		{
			return new ContentsScene()
				.WithContentsList("Solid", "Water", "Glass", "Grate")
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(1f, 0f, 0f, -5.422223f),
					new float4(-1f, 0f, 0f, -4.337777f),
					new float4(0f, 0f, 1f, -3.81249976f),
					new float4(0f, 0f, -1f, -3.81250024f),
					new float4(0f, -1f, 0f, -2.84666634f),
					new float4(0f, 1f, 0f, -4.47333336f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(27.9752769f, -10.4208336f, 34.465f, 1f)), name: "Solid 2476171 #185")
					.WithMesh(new float3[] { new float3(5.422223f, 4.47333336f, -3.81250024f), new float3(5.422223f, 4.47333336f, 3.81249976f), new float3(5.422223f, -2.84666634f, 3.81249976f), new float3(5.422223f, -2.84666634f, -3.81250024f), new float3(-4.337777f, 4.47333336f, 3.81249976f), new float3(-4.337777f, 4.47333336f, -3.81250024f), new float3(-4.337777f, -2.84666634f, -3.81250024f), new float3(-4.337777f, -2.84666634f, 3.81249976f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 1, 4, 7, 2, -1, 5, 0, 3, 6, -1, 6, 3, 2, 7, -1, 0, 5, 4, 1, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 1f, 0f, -9.319444f),
					new float4(0f, -1f, 0f, -5.930556f),
					new float4(0f, 0f, 1f, -1.21999967f),
					new float4(1f, 0f, 0f, -4.88f),
					new float4(0f, 0f, -1f, -1.52500033f),
					new float4(-1f, 0f, 0f, -4.88f),
				}, contents: 1, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(28.5175f, -21.976944f, 34.6175f, 1f)), name: "Water 2477629 #191")
					.WithMesh(new float3[] { new float3(-4.88f, 9.319444f, -1.52500033f), new float3(-4.88f, 9.319444f, 1.21999967f), new float3(4.88f, 9.319444f, 1.21999967f), new float3(4.88f, 9.319444f, -1.52500033f), new float3(-4.88f, -5.930556f, 1.21999967f), new float3(-4.88f, -5.930556f, -1.52500033f), new float3(4.88f, -5.930556f, -1.52500033f), new float3(4.88f, -5.930556f, 1.21999967f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 1, 4, 7, 2, -1, 6, 3, 2, 7, -1, 5, 0, 3, 6, -1, 0, 5, 4, 1, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 1f, 0f, -9.319444f),
					new float4(0f, -1f, 0f, -5.930556f),
					new float4(0f, 0f, 1f, -3.388889f),
					new float4(1f, 0f, 0f, -3.81249928f),
					new float4(0f, 0f, -1f, -4.23611069f),
					new float4(-1f, 0f, 0f, -3.81250072f),
				}, contents: 1, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(19.825f, -21.976944f, 34.88861f, 1f)), name: "Water 2477638 #192")
					.WithMesh(new float3[] { new float3(-3.81250072f, 9.319444f, -4.23611069f), new float3(-3.81250072f, 9.319444f, 3.388889f), new float3(3.81249928f, 9.319444f, 3.388889f), new float3(3.81249928f, 9.319444f, -4.23611069f), new float3(-3.81250072f, -5.930556f, 3.388889f), new float3(-3.81250072f, -5.930556f, -4.23611069f), new float3(3.81249928f, -5.930556f, -4.23611069f), new float3(3.81249928f, -5.930556f, 3.388889f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 1, 4, 7, 2, -1, 6, 3, 2, 7, -1, 5, 0, 3, 6, -1, 0, 5, 4, 1, -1 }))
				;
		}

		// bm_c3a2c, Model: the mesh draws nothing where the exact CSG has a face; Water 2477638 #192 face 5 near (16.013,
		// -13, 34.465) (3 brushes)
		static ContentsScene BmC3a2cModel0Site3()
		{
			return new ContentsScene()
				.WithContentsList("Solid", "Water", "Glass", "Grate")
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(-1f, 0f, 0f, -0.271110684f),
					new float4(1f, 0f, 0f, -0.3388893f),
					new float4(0f, 0f, 1f, -3.81249976f),
					new float4(0f, 0f, -1f, -3.81250024f),
					new float4(0f, -1f, 0f, -2.90597248f),
					new float4(0f, 1f, 0f, -4.56652737f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(15.6736107f, -10.5140276f, 34.465f, 1f)), name: "Solid 2475620 #182")
					.WithMesh(new float3[] { new float3(-0.271110684f, 4.56652737f, 3.81249976f), new float3(-0.271110684f, 4.56652737f, -3.81250024f), new float3(-0.271110684f, -2.90597248f, -3.81250024f), new float3(-0.271110684f, -2.90597248f, 3.81249976f), new float3(0.3388893f, 4.56652737f, -3.81250024f), new float3(0.3388893f, 4.56652737f, 3.81249976f), new float3(0.3388893f, -2.90597248f, 3.81249976f), new float3(0.3388893f, -2.90597248f, -3.81250024f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 5, 0, 3, 6, -1, 1, 4, 7, 2, -1, 2, 7, 6, 3, -1, 4, 1, 0, 5, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 1f, 0f, -9.319444f),
					new float4(0f, -1f, 0f, -5.930556f),
					new float4(0f, 0f, 1f, -1.15222216f),
					new float4(1f, 0f, 0f, -1.83000016f),
					new float4(0f, 0f, -1f, -1.44027781f),
					new float4(-1f, 0f, 0f, -1.82999992f),
				}, contents: 1, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(14.1825f, -21.976944f, 34.6090279f, 1f)), name: "Water 2477622 #190")
					.WithMesh(new float3[] { new float3(-1.82999992f, 9.319444f, -1.44027781f), new float3(-1.82999992f, 9.319444f, 1.15222216f), new float3(1.83000016f, 9.319444f, 1.15222216f), new float3(1.83000016f, 9.319444f, -1.44027781f), new float3(-1.82999992f, -5.930556f, 1.15222216f), new float3(-1.82999992f, -5.930556f, -1.44027781f), new float3(1.83000016f, -5.930556f, -1.44027781f), new float3(1.83000016f, -5.930556f, 1.15222216f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 1, 4, 7, 2, -1, 6, 3, 2, 7, -1, 5, 0, 3, 6, -1, 0, 5, 4, 1, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 1f, 0f, -9.319444f),
					new float4(0f, -1f, 0f, -5.930556f),
					new float4(0f, 0f, 1f, -3.388889f),
					new float4(1f, 0f, 0f, -3.81249928f),
					new float4(0f, 0f, -1f, -4.23611069f),
					new float4(-1f, 0f, 0f, -3.81250072f),
				}, contents: 1, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(19.825f, -21.976944f, 34.88861f, 1f)), name: "Water 2477638 #192")
					.WithMesh(new float3[] { new float3(-3.81250072f, 9.319444f, -4.23611069f), new float3(-3.81250072f, 9.319444f, 3.388889f), new float3(3.81249928f, 9.319444f, 3.388889f), new float3(3.81249928f, 9.319444f, -4.23611069f), new float3(-3.81250072f, -5.930556f, 3.388889f), new float3(-3.81250072f, -5.930556f, -4.23611069f), new float3(3.81249928f, -5.930556f, -4.23611069f), new float3(3.81249928f, -5.930556f, 3.388889f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 1, 4, 7, 2, -1, 6, 3, 2, 7, -1, 5, 0, 3, 6, -1, 0, 5, 4, 1, -1 }))
				;
		}

		// bm_c3a2c, Model: the mesh draws nothing where the exact CSG has a face; Water 3528854 #288 face 5 near (33.398,
		// -23, 34.465) (3 brushes)
		static ContentsScene BmC3a2cModel0Site4()
		{
			return new ContentsScene()
				.WithContentsList("Solid", "Water", "Glass", "Grate")
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 0f, 1f, -1.37249982f),
					new float4(0f, 0f, -1f, -1.37250018f),
					new float4(0f, -1f, 0f, -2.37222266f),
					new float4(0f, 1f, 0f, -3.72777724f),
					new float4(-1f, 0f, 0f, -4.337777f),
					new float4(1f, 0f, 0f, -5.422223f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(27.9752769f, -20.6552773f, 34.465f, 1f)), name: "Solid 2476174 #188")
					.WithMesh(new float3[] { new float3(5.422223f, -2.37222266f, 1.37249982f), new float3(5.422223f, 3.72777724f, 1.37249982f), new float3(-4.337777f, 3.72777724f, 1.37249982f), new float3(-4.337777f, -2.37222266f, 1.37249982f), new float3(5.422223f, 3.72777724f, -1.37250018f), new float3(5.422223f, -2.37222266f, -1.37250018f), new float3(-4.337777f, -2.37222266f, -1.37250018f), new float3(-4.337777f, 3.72777724f, -1.37250018f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 5, 0, 3, 6, -1, 1, 4, 7, 2, -1, 2, 7, 6, 3, -1, 4, 1, 0, 5, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 1f, 0f, -9.319444f),
					new float4(0f, -1f, 0f, -5.930556f),
					new float4(0f, 0f, 1f, -1.21999967f),
					new float4(1f, 0f, 0f, -4.88f),
					new float4(0f, 0f, -1f, -1.52500033f),
					new float4(-1f, 0f, 0f, -4.88f),
				}, contents: 1, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(28.5175f, -21.976944f, 34.6175f, 1f)), name: "Water 2477629 #191")
					.WithMesh(new float3[] { new float3(-4.88f, 9.319444f, -1.52500033f), new float3(-4.88f, 9.319444f, 1.21999967f), new float3(4.88f, 9.319444f, 1.21999967f), new float3(4.88f, 9.319444f, -1.52500033f), new float3(-4.88f, -5.930556f, 1.21999967f), new float3(-4.88f, -5.930556f, -1.52500033f), new float3(4.88f, -5.930556f, -1.52500033f), new float3(4.88f, -5.930556f, 1.21999967f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 1, 4, 7, 2, -1, 6, 3, 2, 7, -1, 5, 0, 3, 6, -1, 0, 5, 4, 1, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 1f, 0f, -9.319444f),
					new float4(0f, -1f, 0f, -5.930556f),
					new float4(0f, 0f, 1f, -2.8974998f),
					new float4(0f, 0f, -1f, -2.8975f),
					new float4(1f, 0f, 0f, -2.11805439f),
					new float4(-1f, 0f, 0f, -1.69444561f),
				}, contents: 1, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(35.0919456f, -21.976944f, 34.465f, 1f)), name: "Water 3528854 #288")
					.WithMesh(new float3[] { new float3(-1.69444561f, 9.319444f, -2.8975f), new float3(-1.69444561f, 9.319444f, 2.8974998f), new float3(2.11805439f, 9.319444f, 2.8974998f), new float3(2.11805439f, 9.319444f, -2.8975f), new float3(-1.69444561f, -5.930556f, 2.8974998f), new float3(-1.69444561f, -5.930556f, -2.8975f), new float3(2.11805439f, -5.930556f, -2.8975f), new float3(2.11805439f, -5.930556f, 2.8974998f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 1, 4, 7, 2, -1, 5, 0, 3, 6, -1, 6, 3, 2, 7, -1, 0, 5, 4, 1, -1 }))
				;
		}

		// bm_c3a2c, Model: the mesh draws nothing where the exact CSG has a face; Water 4512771 #400 face 4 near (-20.13,
		// -13, 34.465) (4 brushes)
		static ContentsScene BmC3a2cModel0Site5()
		{
			return new ContentsScene()
				.WithContentsList("Solid", "Water", "Glass", "Grate")
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, -1f, 0f, -0.47444436f),
					new float4(0f, 0f, 1f, -6.71f),
					new float4(0f, 0f, -1f, -6.71f),
					new float4(1f, 0f, 0f, -1.101388f),
					new float4(-1f, 0f, 0f, -0.881112f),
					new float4(0f, 1f, 0f, -0.745555639f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(-19.248888f, -17.6730556f, 30.3475f, 1f)), name: "Solid 791243 #115")
					.WithMesh(new float3[] { new float3(-0.881112f, -0.47444436f, 6.71f), new float3(-0.881112f, -0.47444436f, -6.71f), new float3(1.101388f, -0.47444436f, -6.71f), new float3(1.101388f, -0.47444436f, 6.71f), new float3(1.101388f, 0.745555639f, 6.71f), new float3(-0.881112f, 0.745555639f, 6.71f), new float3(-0.881112f, 0.745555639f, -6.71f), new float3(1.101388f, 0.745555639f, -6.71f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 0, 3, -1, 6, 7, 2, 1, -1, 7, 4, 3, 2, -1, 5, 6, 1, 0, -1, 6, 5, 4, 7, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 1f, 0f, -4.62583351f),
					new float4(0f, 0f, 1f, -6.71f),
					new float4(0f, 0f, -1f, -6.71f),
					new float4(1f, 0f, 0f, -2.88055587f),
					new float4(-1f, 0f, 0f, -2.30444431f),
					new float4(0f, -1f, 0f, -7.26916647f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(-17.8255558f, -5.99833345f, 30.3475f, 1f)), name: "Solid 791245 #116")
					.WithMesh(new float3[] { new float3(-2.30444431f, 4.62583351f, -6.71f), new float3(-2.30444431f, 4.62583351f, 6.71f), new float3(2.88055587f, 4.62583351f, 6.71f), new float3(2.88055587f, 4.62583351f, -6.71f), new float3(-2.30444431f, -7.26916647f, 6.71f), new float3(2.88055587f, -7.26916647f, 6.71f), new float3(2.88055587f, -7.26916647f, -6.71f), new float3(-2.30444431f, -7.26916647f, -6.71f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 2, 1, -1, 6, 7, 0, 3, -1, 5, 6, 3, 2, -1, 7, 4, 1, 0, -1, 4, 7, 6, 5, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 1f, 0f, -9.319444f),
					new float4(0f, -1f, 0f, -5.930556f),
					new float4(0f, 0f, 1f, -1.21999967f),
					new float4(1f, 0f, 0f, -13.34375f),
					new float4(0f, 0f, -1f, -1.52500033f),
					new float4(-1f, 0f, 0f, -13.34375f),
				}, contents: 1, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(-6.78625f, -21.976944f, 34.6175f, 1f)), name: "Water 2477612 #189")
					.WithMesh(new float3[] { new float3(-13.34375f, 9.319444f, -1.52500033f), new float3(-13.34375f, 9.319444f, 1.21999967f), new float3(13.34375f, 9.319444f, 1.21999967f), new float3(13.34375f, 9.319444f, -1.52500033f), new float3(-13.34375f, -5.930556f, 1.21999967f), new float3(-13.34375f, -5.930556f, -1.52500033f), new float3(13.34375f, -5.930556f, -1.52500033f), new float3(13.34375f, -5.930556f, 1.21999967f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 1, 4, 7, 2, -1, 6, 3, 2, 7, -1, 5, 0, 3, 6, -1, 0, 5, 4, 1, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 1f, 0f, -9.319444f),
					new float4(0f, -1f, 0f, -5.930556f),
					new float4(-1f, 0f, 0f, -5.21888971f),
					new float4(0f, 0f, 1f, -9.624443f),
					new float4(1f, 0f, 0f, -6.52361f),
					new float4(0f, 0f, -1f, -12.0305567f),
				}, contents: 1, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(-26.65361f, -21.976944f, 35.6680565f, 1f)), name: "Water 4512771 #400")
					.WithMesh(new float3[] { new float3(6.52361f, 9.319444f, -12.0305567f), new float3(-5.21888971f, 9.319444f, -12.0305567f), new float3(-5.21888971f, 9.319444f, 9.624443f), new float3(6.52361f, 9.319444f, 9.624443f), new float3(-5.21888971f, -5.930556f, -12.0305567f), new float3(6.52361f, -5.930556f, -12.0305567f), new float3(6.52361f, -5.930556f, 9.624443f), new float3(-5.21888971f, -5.930556f, 9.624443f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 1, 4, 7, 2, -1, 6, 3, 2, 7, -1, 5, 0, 3, 6, -1, 0, 5, 4, 1, -1 }))
				;
		}

		// bm_c3a2c, Model: the mesh draws nothing where the exact CSG has a face; Water 4512771 #400 face 4 near (-20.13,
		// -16, 33.169) (3 brushes)
		static ContentsScene BmC3a2cModel0Site6()
		{
			return new ContentsScene()
				.WithContentsList("Solid", "Water", "Glass", "Grate")
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 0f, -1f, -3.73625064f),
					new float4(1f, 0f, 0f, -1.101388f),
					new float4(-1f, 0f, 0f, -0.881112f),
					new float4(0f, 1f, 0f, -2.23666668f),
					new float4(0f, -1f, 0f, -1.42333341f),
					new float4(0f, 0f, 1f, -5.871249f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(-19.248888f, -15.5041666f, 27.37375f, 1f)), name: "Solid 791267 #118")
					.WithMesh(new float3[] { new float3(1.101388f, -1.42333341f, -3.73625064f), new float3(-0.881112f, -1.42333341f, -3.73625064f), new float3(-0.881112f, 2.23666668f, -3.73625064f), new float3(1.101388f, 2.23666668f, -3.73625064f), new float3(1.101388f, 2.23666668f, 5.871249f), new float3(1.101388f, -1.42333341f, 5.871249f), new float3(-0.881112f, -1.42333341f, 5.871249f), new float3(-0.881112f, 2.23666668f, 5.871249f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 0, 3, -1, 6, 7, 2, 1, -1, 7, 4, 3, 2, -1, 5, 6, 1, 0, -1, 6, 5, 4, 7, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 1f, 0f, -9.319444f),
					new float4(0f, -1f, 0f, -5.930556f),
					new float4(0f, 0f, 1f, -1.21999967f),
					new float4(1f, 0f, 0f, -13.34375f),
					new float4(0f, 0f, -1f, -1.52500033f),
					new float4(-1f, 0f, 0f, -13.34375f),
				}, contents: 1, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(-6.78625f, -21.976944f, 34.6175f, 1f)), name: "Water 2477612 #189")
					.WithMesh(new float3[] { new float3(-13.34375f, 9.319444f, -1.52500033f), new float3(-13.34375f, 9.319444f, 1.21999967f), new float3(13.34375f, 9.319444f, 1.21999967f), new float3(13.34375f, 9.319444f, -1.52500033f), new float3(-13.34375f, -5.930556f, 1.21999967f), new float3(-13.34375f, -5.930556f, -1.52500033f), new float3(13.34375f, -5.930556f, -1.52500033f), new float3(13.34375f, -5.930556f, 1.21999967f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 1, 4, 7, 2, -1, 6, 3, 2, 7, -1, 5, 0, 3, 6, -1, 0, 5, 4, 1, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 1f, 0f, -9.319444f),
					new float4(0f, -1f, 0f, -5.930556f),
					new float4(-1f, 0f, 0f, -5.21888971f),
					new float4(0f, 0f, 1f, -9.624443f),
					new float4(1f, 0f, 0f, -6.52361f),
					new float4(0f, 0f, -1f, -12.0305567f),
				}, contents: 1, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(-26.65361f, -21.976944f, 35.6680565f, 1f)), name: "Water 4512771 #400")
					.WithMesh(new float3[] { new float3(6.52361f, 9.319444f, -12.0305567f), new float3(-5.21888971f, 9.319444f, -12.0305567f), new float3(-5.21888971f, 9.319444f, 9.624443f), new float3(6.52361f, 9.319444f, 9.624443f), new float3(-5.21888971f, -5.930556f, -12.0305567f), new float3(6.52361f, -5.930556f, -12.0305567f), new float3(6.52361f, -5.930556f, 9.624443f), new float3(-5.21888971f, -5.930556f, 9.624443f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 1, 4, 7, 2, -1, 6, 3, 2, 7, -1, 5, 0, 3, 6, -1, 0, 5, 4, 1, -1 }))
				;
		}

		// bm_c3a2c, Model: the mesh draws nothing where the exact CSG has a face; Water 4512771 #400 face 4 near (-20.13,
		// -16, 35.761) (3 brushes)
		static ContentsScene BmC3a2cModel0Site7()
		{
			return new ContentsScene()
				.WithContentsList("Solid", "Water", "Glass", "Grate")
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 0f, 1f, -0.5337497f),
					new float4(1f, 0f, 0f, -1.101388f),
					new float4(-1f, 0f, 0f, -0.881112f),
					new float4(0f, 1f, 0f, -2.23666668f),
					new float4(0f, -1f, 0f, -1.42333341f),
					new float4(0f, 0f, -1f, -0.8387503f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(-19.248888f, -15.5041666f, 36.52375f, 1f)), name: "Solid 791265 #117")
					.WithMesh(new float3[] { new float3(-0.881112f, -1.42333341f, 0.5337497f), new float3(1.101388f, -1.42333341f, 0.5337497f), new float3(1.101388f, 2.23666668f, 0.5337497f), new float3(-0.881112f, 2.23666668f, 0.5337497f), new float3(1.101388f, -1.42333341f, -0.8387503f), new float3(1.101388f, 2.23666668f, -0.8387503f), new float3(-0.881112f, 2.23666668f, -0.8387503f), new float3(-0.881112f, -1.42333341f, -0.8387503f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 2, 1, -1, 6, 7, 0, 3, -1, 5, 6, 3, 2, -1, 7, 4, 1, 0, -1, 4, 7, 6, 5, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 1f, 0f, -9.319444f),
					new float4(0f, -1f, 0f, -5.930556f),
					new float4(0f, 0f, 1f, -1.21999967f),
					new float4(1f, 0f, 0f, -13.34375f),
					new float4(0f, 0f, -1f, -1.52500033f),
					new float4(-1f, 0f, 0f, -13.34375f),
				}, contents: 1, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(-6.78625f, -21.976944f, 34.6175f, 1f)), name: "Water 2477612 #189")
					.WithMesh(new float3[] { new float3(-13.34375f, 9.319444f, -1.52500033f), new float3(-13.34375f, 9.319444f, 1.21999967f), new float3(13.34375f, 9.319444f, 1.21999967f), new float3(13.34375f, 9.319444f, -1.52500033f), new float3(-13.34375f, -5.930556f, 1.21999967f), new float3(-13.34375f, -5.930556f, -1.52500033f), new float3(13.34375f, -5.930556f, -1.52500033f), new float3(13.34375f, -5.930556f, 1.21999967f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 1, 4, 7, 2, -1, 6, 3, 2, 7, -1, 5, 0, 3, 6, -1, 0, 5, 4, 1, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 1f, 0f, -9.319444f),
					new float4(0f, -1f, 0f, -5.930556f),
					new float4(-1f, 0f, 0f, -5.21888971f),
					new float4(0f, 0f, 1f, -9.624443f),
					new float4(1f, 0f, 0f, -6.52361f),
					new float4(0f, 0f, -1f, -12.0305567f),
				}, contents: 1, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(-26.65361f, -21.976944f, 35.6680565f, 1f)), name: "Water 4512771 #400")
					.WithMesh(new float3[] { new float3(6.52361f, 9.319444f, -12.0305567f), new float3(-5.21888971f, 9.319444f, -12.0305567f), new float3(-5.21888971f, 9.319444f, 9.624443f), new float3(6.52361f, 9.319444f, 9.624443f), new float3(-5.21888971f, -5.930556f, -12.0305567f), new float3(6.52361f, -5.930556f, -12.0305567f), new float3(6.52361f, -5.930556f, 9.624443f), new float3(-5.21888971f, -5.930556f, 9.624443f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 1, 4, 7, 2, -1, 6, 3, 2, 7, -1, 5, 0, 3, 6, -1, 0, 5, 4, 1, -1 }))
				;
		}

		// bm_c3a2c, Model: the mesh draws nothing where the exact CSG has a face; Water 4831643 #315 face 2 near (12.352,
		// -13, 34.465) (3 brushes)
		static ContentsScene BmC3a2cModel0Site8()
		{
			return new ContentsScene()
				.WithContentsList("Solid", "Water", "Glass", "Grate")
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(1f, 0f, 0f, -0.7625004f),
					new float4(-1f, 0f, 0f, -0.6099996f),
					new float4(0f, 1f, 0f, -2.43999958f),
					new float4(0f, 0f, 1f, -3.20249987f),
					new float4(0f, 0f, -1f, -3.2025f),
					new float4(0f, -1f, 0f, -2.44000053f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(12.9625f, -10.98f, 34.465f, 1f)), name: "Solid 741009 #105")
					.WithMesh(new float3[] { new float3(0.7625004f, -2.44000053f, 3.20249987f), new float3(0.7625004f, -2.44000053f, -3.2025f), new float3(0.7625004f, 2.43999958f, -3.2025f), new float3(0.7625004f, 2.43999958f, 3.20249987f), new float3(-0.6099996f, -2.44000053f, -3.2025f), new float3(-0.6099996f, -2.44000053f, 3.20249987f), new float3(-0.6099996f, 2.43999958f, 3.20249987f), new float3(-0.6099996f, 2.43999958f, -3.2025f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 2, 7, 6, 3, -1, 5, 0, 3, 6, -1, 1, 4, 7, 2, -1, 4, 1, 0, 5, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 1f, 0f, -9.319444f),
					new float4(0f, -1f, 0f, -5.930556f),
					new float4(0f, 0f, 1f, -1.15222216f),
					new float4(1f, 0f, 0f, -1.83000016f),
					new float4(0f, 0f, -1f, -1.44027781f),
					new float4(-1f, 0f, 0f, -1.82999992f),
				}, contents: 1, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(14.1825f, -21.976944f, 34.6090279f, 1f)), name: "Water 2477622 #190")
					.WithMesh(new float3[] { new float3(-1.82999992f, 9.319444f, -1.44027781f), new float3(-1.82999992f, 9.319444f, 1.15222216f), new float3(1.83000016f, 9.319444f, 1.15222216f), new float3(1.83000016f, 9.319444f, -1.44027781f), new float3(-1.82999992f, -5.930556f, 1.15222216f), new float3(-1.82999992f, -5.930556f, -1.44027781f), new float3(1.83000016f, -5.930556f, -1.44027781f), new float3(1.83000016f, -5.930556f, 1.15222216f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 1, 4, 7, 2, -1, 6, 3, 2, 7, -1, 5, 0, 3, 6, -1, 0, 5, 4, 1, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 1f, 0f, -9.319444f),
					new float4(0f, -1f, 0f, -5.930556f),
					new float4(1f, 0f, 0f, -3.21944427f),
					new float4(-1f, 0f, 0f, -2.5755558f),
					new float4(0f, 0f, -1f, -10.0650005f),
					new float4(0f, 0f, 1f, -10.065f),
				}, contents: 1, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(9.133056f, -21.976944f, 34.465f, 1f)), name: "Water 4831643 #315")
					.WithMesh(new float3[] { new float3(-2.5755558f, 9.319444f, 10.065f), new float3(3.21944427f, 9.319444f, 10.065f), new float3(3.21944427f, 9.319444f, -10.0650005f), new float3(-2.5755558f, 9.319444f, -10.0650005f), new float3(3.21944427f, -5.930556f, 10.065f), new float3(-2.5755558f, -5.930556f, 10.065f), new float3(-2.5755558f, -5.930556f, -10.0650005f), new float3(3.21944427f, -5.930556f, -10.0650005f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 1, 4, 7, 2, -1, 5, 0, 3, 6, -1, 6, 3, 2, 7, -1, 0, 5, 4, 1, -1 }))
				;
		}

		// bm_c3a2c, Model: the mesh draws nothing where the exact CSG has a face; Water 4831643 #315 face 3 near (6.557, -22,
		// 34.465) (3 brushes)
		static ContentsScene BmC3a2cModel0Site9()
		{
			return new ContentsScene()
				.WithContentsList("Solid", "Water", "Glass", "Grate")
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 1f, 0f, -2.9822228f),
					new float4(0f, -1f, 0f, -1.8977772f),
					new float4(1f, 0f, 0f, -3.21944427f),
					new float4(-1f, 0f, 0f, -2.5755558f),
					new float4(0f, 0f, -1f, -4.11750031f),
					new float4(0f, 0f, 1f, -4.1175f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(9.133056f, -21.2822227f, 34.465f, 1f)), name: "Solid 711878 #63")
					.WithMesh(new float3[] { new float3(-2.5755558f, 2.9822228f, 4.1175f), new float3(3.21944427f, 2.9822228f, 4.1175f), new float3(3.21944427f, 2.9822228f, -4.11750031f), new float3(-2.5755558f, 2.9822228f, -4.11750031f), new float3(3.21944427f, -1.8977772f, 4.1175f), new float3(-2.5755558f, -1.8977772f, 4.1175f), new float3(-2.5755558f, -1.8977772f, -4.11750031f), new float3(3.21944427f, -1.8977772f, -4.11750031f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 1, 4, 7, 2, -1, 5, 0, 3, 6, -1, 6, 3, 2, 7, -1, 0, 5, 4, 1, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 1f, 0f, -9.319444f),
					new float4(0f, -1f, 0f, -5.930556f),
					new float4(0f, 0f, 1f, -1.21999967f),
					new float4(1f, 0f, 0f, -13.34375f),
					new float4(0f, 0f, -1f, -1.52500033f),
					new float4(-1f, 0f, 0f, -13.34375f),
				}, contents: 1, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(-6.78625f, -21.976944f, 34.6175f, 1f)), name: "Water 2477612 #189")
					.WithMesh(new float3[] { new float3(-13.34375f, 9.319444f, -1.52500033f), new float3(-13.34375f, 9.319444f, 1.21999967f), new float3(13.34375f, 9.319444f, 1.21999967f), new float3(13.34375f, 9.319444f, -1.52500033f), new float3(-13.34375f, -5.930556f, 1.21999967f), new float3(-13.34375f, -5.930556f, -1.52500033f), new float3(13.34375f, -5.930556f, -1.52500033f), new float3(13.34375f, -5.930556f, 1.21999967f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 1, 4, 7, 2, -1, 6, 3, 2, 7, -1, 5, 0, 3, 6, -1, 0, 5, 4, 1, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 1f, 0f, -9.319444f),
					new float4(0f, -1f, 0f, -5.930556f),
					new float4(1f, 0f, 0f, -3.21944427f),
					new float4(-1f, 0f, 0f, -2.5755558f),
					new float4(0f, 0f, -1f, -10.0650005f),
					new float4(0f, 0f, 1f, -10.065f),
				}, contents: 1, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(9.133056f, -21.976944f, 34.465f, 1f)), name: "Water 4831643 #315")
					.WithMesh(new float3[] { new float3(-2.5755558f, 9.319444f, 10.065f), new float3(3.21944427f, 9.319444f, 10.065f), new float3(3.21944427f, 9.319444f, -10.0650005f), new float3(-2.5755558f, 9.319444f, -10.0650005f), new float3(3.21944427f, -5.930556f, 10.065f), new float3(-2.5755558f, -5.930556f, 10.065f), new float3(-2.5755558f, -5.930556f, -10.0650005f), new float3(3.21944427f, -5.930556f, -10.0650005f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 1, 4, 7, 2, -1, 5, 0, 3, 6, -1, 6, 3, 2, 7, -1, 0, 5, 4, 1, -1 }))
				;
		}

		// bm_c3a2c, Model: the mesh draws nothing where the exact CSG has a face; Water 2477638 #192 face 3 near (23.638,
		// -23, 34.465) (3 brushes)
		static ContentsScene BmC3a2cModel0Site10()
		{
			return new ContentsScene()
				.WithContentsList("Solid", "Water", "Glass", "Grate")
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 0f, 1f, -1.37249982f),
					new float4(0f, 0f, -1f, -1.37250018f),
					new float4(0f, -1f, 0f, -2.37222266f),
					new float4(0f, 1f, 0f, -3.72777724f),
					new float4(-1f, 0f, 0f, -4.337777f),
					new float4(1f, 0f, 0f, -5.422223f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(27.9752769f, -20.6552773f, 34.465f, 1f)), name: "Solid 2476174 #188")
					.WithMesh(new float3[] { new float3(5.422223f, -2.37222266f, 1.37249982f), new float3(5.422223f, 3.72777724f, 1.37249982f), new float3(-4.337777f, 3.72777724f, 1.37249982f), new float3(-4.337777f, -2.37222266f, 1.37249982f), new float3(5.422223f, 3.72777724f, -1.37250018f), new float3(5.422223f, -2.37222266f, -1.37250018f), new float3(-4.337777f, -2.37222266f, -1.37250018f), new float3(-4.337777f, 3.72777724f, -1.37250018f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 5, 0, 3, 6, -1, 1, 4, 7, 2, -1, 2, 7, 6, 3, -1, 4, 1, 0, 5, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 1f, 0f, -9.319444f),
					new float4(0f, -1f, 0f, -5.930556f),
					new float4(0f, 0f, 1f, -1.21999967f),
					new float4(1f, 0f, 0f, -4.88f),
					new float4(0f, 0f, -1f, -1.52500033f),
					new float4(-1f, 0f, 0f, -4.88f),
				}, contents: 1, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(28.5175f, -21.976944f, 34.6175f, 1f)), name: "Water 2477629 #191")
					.WithMesh(new float3[] { new float3(-4.88f, 9.319444f, -1.52500033f), new float3(-4.88f, 9.319444f, 1.21999967f), new float3(4.88f, 9.319444f, 1.21999967f), new float3(4.88f, 9.319444f, -1.52500033f), new float3(-4.88f, -5.930556f, 1.21999967f), new float3(-4.88f, -5.930556f, -1.52500033f), new float3(4.88f, -5.930556f, -1.52500033f), new float3(4.88f, -5.930556f, 1.21999967f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 1, 4, 7, 2, -1, 6, 3, 2, 7, -1, 5, 0, 3, 6, -1, 0, 5, 4, 1, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 1f, 0f, -9.319444f),
					new float4(0f, -1f, 0f, -5.930556f),
					new float4(0f, 0f, 1f, -3.388889f),
					new float4(1f, 0f, 0f, -3.81249928f),
					new float4(0f, 0f, -1f, -4.23611069f),
					new float4(-1f, 0f, 0f, -3.81250072f),
				}, contents: 1, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(19.825f, -21.976944f, 34.88861f, 1f)), name: "Water 2477638 #192")
					.WithMesh(new float3[] { new float3(-3.81250072f, 9.319444f, -4.23611069f), new float3(-3.81250072f, 9.319444f, 3.388889f), new float3(3.81249928f, 9.319444f, 3.388889f), new float3(3.81249928f, 9.319444f, -4.23611069f), new float3(-3.81250072f, -5.930556f, 3.388889f), new float3(-3.81250072f, -5.930556f, -4.23611069f), new float3(3.81249928f, -5.930556f, -4.23611069f), new float3(3.81249928f, -5.930556f, 3.388889f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 1, 4, 7, 2, -1, 6, 3, 2, 7, -1, 5, 0, 3, 6, -1, 0, 5, 4, 1, -1 }))
				;
		}

		// bm_c3a2c, Model: the mesh draws nothing where the exact CSG has a face; Water 2477638 #192 face 5 near (16.013,
		// -23, 34.465) (3 brushes)
		static ContentsScene BmC3a2cModel0Site11()
		{
			return new ContentsScene()
				.WithContentsList("Solid", "Water", "Glass", "Grate")
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 0f, 1f, -1.37249982f),
					new float4(0f, 0f, -1f, -1.37250018f),
					new float4(0f, -1f, 0f, -2.31291747f),
					new float4(0f, 1f, 0f, -3.63458252f),
					new float4(1f, 0f, 0f, -1.27083325f),
					new float4(-1f, 0f, 0f, -1.01666677f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(14.7416668f, -20.7145824f, 34.465f, 1f)), name: "Solid 741683 #114")
					.WithMesh(new float3[] { new float3(-1.01666677f, 3.63458252f, 1.37249982f), new float3(-1.01666677f, -2.31291747f, 1.37249982f), new float3(1.27083325f, -2.31291747f, 1.37249982f), new float3(1.27083325f, 3.63458252f, 1.37249982f), new float3(-1.01666677f, -2.31291747f, -1.37250018f), new float3(-1.01666677f, 3.63458252f, -1.37250018f), new float3(1.27083325f, 3.63458252f, -1.37250018f), new float3(1.27083325f, -2.31291747f, -1.37250018f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 1, 4, 7, 2, -1, 5, 0, 3, 6, -1, 6, 3, 2, 7, -1, 0, 5, 4, 1, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 1f, 0f, -9.319444f),
					new float4(0f, -1f, 0f, -5.930556f),
					new float4(0f, 0f, 1f, -1.15222216f),
					new float4(1f, 0f, 0f, -1.83000016f),
					new float4(0f, 0f, -1f, -1.44027781f),
					new float4(-1f, 0f, 0f, -1.82999992f),
				}, contents: 1, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(14.1825f, -21.976944f, 34.6090279f, 1f)), name: "Water 2477622 #190")
					.WithMesh(new float3[] { new float3(-1.82999992f, 9.319444f, -1.44027781f), new float3(-1.82999992f, 9.319444f, 1.15222216f), new float3(1.83000016f, 9.319444f, 1.15222216f), new float3(1.83000016f, 9.319444f, -1.44027781f), new float3(-1.82999992f, -5.930556f, 1.15222216f), new float3(-1.82999992f, -5.930556f, -1.44027781f), new float3(1.83000016f, -5.930556f, -1.44027781f), new float3(1.83000016f, -5.930556f, 1.15222216f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 1, 4, 7, 2, -1, 6, 3, 2, 7, -1, 5, 0, 3, 6, -1, 0, 5, 4, 1, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 1f, 0f, -9.319444f),
					new float4(0f, -1f, 0f, -5.930556f),
					new float4(0f, 0f, 1f, -3.388889f),
					new float4(1f, 0f, 0f, -3.81249928f),
					new float4(0f, 0f, -1f, -4.23611069f),
					new float4(-1f, 0f, 0f, -3.81250072f),
				}, contents: 1, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(19.825f, -21.976944f, 34.88861f, 1f)), name: "Water 2477638 #192")
					.WithMesh(new float3[] { new float3(-3.81250072f, 9.319444f, -4.23611069f), new float3(-3.81250072f, 9.319444f, 3.388889f), new float3(3.81249928f, 9.319444f, 3.388889f), new float3(3.81249928f, 9.319444f, -4.23611069f), new float3(-3.81250072f, -5.930556f, 3.388889f), new float3(-3.81250072f, -5.930556f, -4.23611069f), new float3(3.81249928f, -5.930556f, -4.23611069f), new float3(3.81249928f, -5.930556f, 3.388889f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 1, 4, 7, 2, -1, 6, 3, 2, 7, -1, 5, 0, 3, 6, -1, 0, 5, 4, 1, -1 }))
				;
		}

		// bm_c3a2c, Model: the mesh draws nothing where the exact CSG has a face; Water 3528854 #288 face 5 near (33.398,
		// -20, 34.465) (4 brushes)
		static ContentsScene BmC3a2cModel0Site12()
		{
			return new ContentsScene()
				.WithContentsList("Solid", "Water", "Glass", "Grate")
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 0f, 1f, -1.37249982f),
					new float4(0f, 0f, -1f, -1.37250018f),
					new float4(0f, -1f, 0f, -2.37222266f),
					new float4(0f, 1f, 0f, -3.72777724f),
					new float4(-1f, 0f, 0f, -4.337777f),
					new float4(1f, 0f, 0f, -5.422223f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(27.9752769f, -20.6552773f, 34.465f, 1f)), name: "Solid 2476174 #188")
					.WithMesh(new float3[] { new float3(5.422223f, -2.37222266f, 1.37249982f), new float3(5.422223f, 3.72777724f, 1.37249982f), new float3(-4.337777f, 3.72777724f, 1.37249982f), new float3(-4.337777f, -2.37222266f, 1.37249982f), new float3(5.422223f, 3.72777724f, -1.37250018f), new float3(5.422223f, -2.37222266f, -1.37250018f), new float3(-4.337777f, -2.37222266f, -1.37250018f), new float3(-4.337777f, 3.72777724f, -1.37250018f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 5, 0, 3, 6, -1, 1, 4, 7, 2, -1, 2, 7, 6, 3, -1, 4, 1, 0, 5, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 1f, 0f, -9.319444f),
					new float4(0f, -1f, 0f, -5.930556f),
					new float4(0f, 0f, 1f, -1.21999967f),
					new float4(1f, 0f, 0f, -4.88f),
					new float4(0f, 0f, -1f, -1.52500033f),
					new float4(-1f, 0f, 0f, -4.88f),
				}, contents: 1, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(28.5175f, -21.976944f, 34.6175f, 1f)), name: "Water 2477629 #191")
					.WithMesh(new float3[] { new float3(-4.88f, 9.319444f, -1.52500033f), new float3(-4.88f, 9.319444f, 1.21999967f), new float3(4.88f, 9.319444f, 1.21999967f), new float3(4.88f, 9.319444f, -1.52500033f), new float3(-4.88f, -5.930556f, 1.21999967f), new float3(-4.88f, -5.930556f, -1.52500033f), new float3(4.88f, -5.930556f, -1.52500033f), new float3(4.88f, -5.930556f, 1.21999967f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 1, 4, 7, 2, -1, 6, 3, 2, 7, -1, 5, 0, 3, 6, -1, 0, 5, 4, 1, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 1f, 0f, -9.319444f),
					new float4(0f, -1f, 0f, -5.930556f),
					new float4(0f, 0f, 1f, -2.8974998f),
					new float4(0f, 0f, -1f, -2.8975f),
					new float4(1f, 0f, 0f, -2.11805439f),
					new float4(-1f, 0f, 0f, -1.69444561f),
				}, contents: 1, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(35.0919456f, -21.976944f, 34.465f, 1f)), name: "Water 3528854 #288")
					.WithMesh(new float3[] { new float3(-1.69444561f, 9.319444f, -2.8975f), new float3(-1.69444561f, 9.319444f, 2.8974998f), new float3(2.11805439f, 9.319444f, 2.8974998f), new float3(2.11805439f, 9.319444f, -2.8975f), new float3(-1.69444561f, -5.930556f, 2.8974998f), new float3(-1.69444561f, -5.930556f, -2.8975f), new float3(2.11805439f, -5.930556f, -2.8975f), new float3(2.11805439f, -5.930556f, 2.8974998f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 1, 4, 7, 2, -1, 5, 0, 3, 6, -1, 6, 3, 2, 7, -1, 0, 5, 4, 1, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(1f, 0f, 0f, -2.11805439f),
					new float4(-1f, 0f, 0f, -1.69444561f),
					new float4(0f, 0f, 1f, -2.8974998f),
					new float4(0f, 0f, -1f, -2.8975f),
					new float4(0f, -1f, 0f, -0.2372226f),
					new float4(0f, 1f, 0f, -0.3727774f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(35.0919456f, -20.8077774f, 34.465f, 1f)), name: "Solid 3529635 #293")
					.WithMesh(new float3[] { new float3(2.11805439f, 0.3727774f, -2.8975f), new float3(2.11805439f, 0.3727774f, 2.8974998f), new float3(2.11805439f, -0.2372226f, 2.8974998f), new float3(2.11805439f, -0.2372226f, -2.8975f), new float3(-1.69444561f, 0.3727774f, 2.8974998f), new float3(-1.69444561f, 0.3727774f, -2.8975f), new float3(-1.69444561f, -0.2372226f, -2.8975f), new float3(-1.69444561f, -0.2372226f, 2.8974998f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 1, 4, 7, 2, -1, 5, 0, 3, 6, -1, 6, 3, 2, 7, -1, 0, 5, 4, 1, -1 }))
				;
		}

		// bm_c3a2c, Model: the mesh draws nothing where the exact CSG has a face; Water 4831643 #315 face 2 near (12.352,
		// -18, 34.465) (4 brushes)
		static ContentsScene BmC3a2cModel0Site13()
		{
			return new ContentsScene()
				.WithContentsList("Solid", "Water", "Glass", "Grate")
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 1f, 0f, -2.9822228f),
					new float4(0f, -1f, 0f, -1.8977772f),
					new float4(1f, 0f, 0f, -3.21944427f),
					new float4(-1f, 0f, 0f, -2.5755558f),
					new float4(0f, 0f, -1f, -4.11750031f),
					new float4(0f, 0f, 1f, -4.1175f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(9.133056f, -21.2822227f, 34.465f, 1f)), name: "Solid 711878 #63")
					.WithMesh(new float3[] { new float3(-2.5755558f, 2.9822228f, 4.1175f), new float3(3.21944427f, 2.9822228f, 4.1175f), new float3(3.21944427f, 2.9822228f, -4.11750031f), new float3(-2.5755558f, 2.9822228f, -4.11750031f), new float3(3.21944427f, -1.8977772f, 4.1175f), new float3(-2.5755558f, -1.8977772f, 4.1175f), new float3(-2.5755558f, -1.8977772f, -4.11750031f), new float3(3.21944427f, -1.8977772f, -4.11750031f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 1, 4, 7, 2, -1, 5, 0, 3, 6, -1, 6, 3, 2, 7, -1, 0, 5, 4, 1, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, -1f, 0f, -2.37222266f),
					new float4(1f, 0f, 0f, -0.7625004f),
					new float4(-1f, 0f, 0f, -0.6099996f),
					new float4(0f, 0f, 1f, -3.20249987f),
					new float4(0f, 0f, -1f, -3.2025f),
					new float4(0f, 1f, 0f, -3.72777748f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(12.9625f, -20.8077774f, 34.465f, 1f)), name: "Solid 741011 #106")
					.WithMesh(new float3[] { new float3(-0.6099996f, -2.37222266f, -3.2025f), new float3(0.7625004f, -2.37222266f, -3.2025f), new float3(0.7625004f, -2.37222266f, 3.20249987f), new float3(-0.6099996f, -2.37222266f, 3.20249987f), new float3(0.7625004f, 3.72777748f, -3.2025f), new float3(0.7625004f, 3.72777748f, 3.20249987f), new float3(-0.6099996f, 3.72777748f, 3.20249987f), new float3(-0.6099996f, 3.72777748f, -3.2025f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 2, 1, -1, 6, 7, 0, 3, -1, 5, 6, 3, 2, -1, 7, 4, 1, 0, -1, 4, 7, 6, 5, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 1f, 0f, -9.319444f),
					new float4(0f, -1f, 0f, -5.930556f),
					new float4(0f, 0f, 1f, -1.15222216f),
					new float4(1f, 0f, 0f, -1.83000016f),
					new float4(0f, 0f, -1f, -1.44027781f),
					new float4(-1f, 0f, 0f, -1.82999992f),
				}, contents: 1, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(14.1825f, -21.976944f, 34.6090279f, 1f)), name: "Water 2477622 #190")
					.WithMesh(new float3[] { new float3(-1.82999992f, 9.319444f, -1.44027781f), new float3(-1.82999992f, 9.319444f, 1.15222216f), new float3(1.83000016f, 9.319444f, 1.15222216f), new float3(1.83000016f, 9.319444f, -1.44027781f), new float3(-1.82999992f, -5.930556f, 1.15222216f), new float3(-1.82999992f, -5.930556f, -1.44027781f), new float3(1.83000016f, -5.930556f, -1.44027781f), new float3(1.83000016f, -5.930556f, 1.15222216f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 1, 4, 7, 2, -1, 6, 3, 2, 7, -1, 5, 0, 3, 6, -1, 0, 5, 4, 1, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 1f, 0f, -9.319444f),
					new float4(0f, -1f, 0f, -5.930556f),
					new float4(1f, 0f, 0f, -3.21944427f),
					new float4(-1f, 0f, 0f, -2.5755558f),
					new float4(0f, 0f, -1f, -10.0650005f),
					new float4(0f, 0f, 1f, -10.065f),
				}, contents: 1, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(9.133056f, -21.976944f, 34.465f, 1f)), name: "Water 4831643 #315")
					.WithMesh(new float3[] { new float3(-2.5755558f, 9.319444f, 10.065f), new float3(3.21944427f, 9.319444f, 10.065f), new float3(3.21944427f, 9.319444f, -10.0650005f), new float3(-2.5755558f, 9.319444f, -10.0650005f), new float3(3.21944427f, -5.930556f, 10.065f), new float3(-2.5755558f, -5.930556f, 10.065f), new float3(-2.5755558f, -5.930556f, -10.0650005f), new float3(3.21944427f, -5.930556f, -10.0650005f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 1, 4, 7, 2, -1, 5, 0, 3, 6, -1, 6, 3, 2, 7, -1, 0, 5, 4, 1, -1 }))
				;
		}

		// bm_c3a2c, Details: the exact CSG made a triangle the mesh does not have; Solid 2475874 #1388 face -1 near (17.9,
		// -15.326, 35.685) (5 brushes)
		static ContentsScene BmC3a2cDetails34Site1()
		{
			return new ContentsScene()
				.WithContentsList("Solid", "Water", "Glass", "Grate")
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(1f, 0f, 0f, -0.0847213f),
					new float4(-1f, 0f, 0f, -0.0677787f),
					new float4(0f, 0.9417419f, 0.3363364f, -0.09631369f),
					new float4(0f, 0f, -1f, -0.232987061f),
					new float4(0f, 0.9863939f, -0.164398983f, -0.103069559f),
					new float4(0f, -1f, 0f, -0.08683991f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(17.8149662f, -16.84066f, 35.1554871f, 1f)), name: "Solid 2466609 #1379")
					.WithMesh(new float3[] { new float3(0.0847213f, -0.08683991f, 0.529512942f), new float3(0.0847213f, -0.08683991f, -0.232987061f), new float3(0.0847213f, 0.0656601f, -0.232987061f), new float3(0.0847213f, 0.1037851f, -0.004237067f), new float3(-0.0677787f, -0.08683991f, -0.232987061f), new float3(-0.0677787f, -0.08683991f, 0.529512942f), new float3(-0.0677787f, 0.1037851f, -0.004237067f), new float3(-0.0677787f, 0.0656601f, -0.232987061f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 5, 0, 3, 6, -1, 1, 4, 7, 2, -1, 2, 7, 6, 3, -1, 4, 1, 0, 5, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 1f, 0f, -1.11833286f),
					new float4(0f, -1f, 0f, -0.7116671f),
					new float4(0f, 0f, 1f, -0.07625061f),
					new float4(0f, 0f, -1f, -0.07624939f),
					new float4(-1f, 0f, 0f, -0.0677787f),
					new float4(1f, 0f, 0f, -0.0847213f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(17.8149662f, -15.4533329f, 35.60875f, 1f)), name: "Solid 2466610 #1380")
					.WithMesh(new float3[] { new float3(0.0847213f, 1.11833286f, 0.07625061f), new float3(0.0847213f, 1.11833286f, -0.07624939f), new float3(-0.0677787f, 1.11833286f, -0.07624939f), new float3(-0.0677787f, 1.11833286f, 0.07625061f), new float3(0.0847213f, -0.7116671f, -0.07624939f), new float3(0.0847213f, -0.7116671f, 0.07625061f), new float3(-0.0677787f, -0.7116671f, 0.07625061f), new float3(-0.0677787f, -0.7116671f, -0.07624939f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 5, 0, 3, 6, -1, 1, 4, 7, 2, -1, 2, 7, 6, 3, -1, 4, 1, 0, 5, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 1f, 0f, -0.1863889f),
					new float4(0f, -1f, 0f, -0.118611105f),
					new float4(0f, 0f, 1f, -1.21999979f),
					new float4(0f, 0f, -1f, -1.22000015f),
					new float4(-1f, 0f, 0f, -0.09319534f),
					new float4(1f, 0f, 0f, -0.11649216f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(17.6306953f, -13.4538889f, 34.465f, 1f)), name: "Solid 2475711 #1381")
					.WithMesh(new float3[] { new float3(0.11649216f, 0.1863889f, 1.21999979f), new float3(0.11649216f, 0.1863889f, -1.22000015f), new float3(-0.09319534f, 0.1863889f, -1.22000015f), new float3(-0.09319534f, 0.1863889f, 1.21999979f), new float3(0.11649216f, -0.118611105f, -1.22000015f), new float3(0.11649216f, -0.118611105f, 1.21999979f), new float3(-0.09319534f, -0.118611105f, 1.21999979f), new float3(-0.09319534f, -0.118611105f, -1.22000015f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 5, 0, 3, 6, -1, 1, 4, 7, 2, -1, 2, 7, 6, 3, -1, 4, 1, 0, 5, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, -1f, 0f, -4.18285751f),
					new float4(0f, 1f, 0f, -6.79714251f),
					new float4(0.707106769f, 0f, 0.707106769f, -0.35716185f),
					new float4(-0.502877831f, 0f, -0.8643575f, -0.374811232f),
					new float4(-0.79495126f, 0f, 0.6066733f, -0.665932536f),
					new float4(0.7961986f, 0f, -0.605035365f, -0.536991f),
					new float4(0f, 0f, -1f, -0.4965338f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(17.603363f, -18.8446426f, 36.1815338f, 1f)), name: "Solid 2475874 #1388")
					.WithMesh(new float3[] { new float3(0.10812065f, -4.18285751f, -0.4965338f), new float3(0.297124982f, -4.18285751f, -0.4965338f), new float3(0.601324439f, -4.18285751f, -0.09622132f), new float3(-0.256488055f, -4.18285751f, 0.7615912f), new float3(-0.809300542f, -4.18285751f, 0.0372161642f), new float3(0.297124982f, 6.79714251f, -0.4965338f), new float3(0.10812065f, 6.79714251f, -0.4965338f), new float3(-0.809300542f, 6.79714251f, 0.0372161642f), new float3(-0.256488055f, 6.79714251f, 0.7615912f), new float3(0.601324439f, 6.79714251f, -0.09622132f) }, new int[] { 0, 1, 2, 3, 4, -1, 5, 6, 7, 8, 9, -1, 2, 9, 8, 3, -1, 6, 0, 4, 7, -1, 7, 4, 3, 8, -1, 1, 5, 9, 2, -1, 5, 1, 0, 6, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 1f, 0f, -2.4400003f),
					new float4(-0.5032206f, 0f, -0.864158f, -0.0316798054f),
					new float4(0.7963516f, 0f, -0.604833961f, -0.05013007f),
					new float4(0f, 0f, 1f, -0.0254159551f),
					new float4(0f, -1f, 0f, -3.65999961f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(17.81816f, -19.3675f, 35.659584f, 1f)), name: "Solid 2476712 #1413")
					.WithMesh(new float3[] { new float3(-0.106599778f, 2.4400003f, 0.0254159551f), new float3(0.08225324f, 2.4400003f, 0.0254159551f), new float3(0.0243408959f, 2.4400003f, -0.0508340448f), new float3(0.0243408959f, -3.65999961f, -0.0508340448f), new float3(-0.106599778f, -3.65999961f, 0.0254159551f), new float3(0.08225324f, -3.65999961f, 0.0254159551f) }, new int[] { 0, 1, 2, -1, 3, 4, 0, 2, -1, 5, 3, 2, 1, -1, 4, 5, 1, 0, -1, 5, 4, 3, -1 }))
				;
		}

		// bm_c3a2c, Details: the exact CSG made a triangle the mesh does not have; Solid 2475891 #1403 face -1 near (21.75,
		// -15.326, 35.685) (4 brushes)
		static ContentsScene BmC3a2cDetails34Site2()
		{
			return new ContentsScene()
				.WithContentsList("Solid", "Water", "Glass", "Grate")
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, -1f, 0f, -4.18285751f),
					new float4(0f, 1f, 0f, -6.79714251f),
					new float4(-0.707106769f, 0f, 0.707106769f, -0.410976022f),
					new float4(0.5028545f, 0f, -0.864371061f, -0.3079712f),
					new float4(0.79495126f, 0f, 0.6066733f, -0.7161272f),
					new float4(-0.7961629f, 0f, -0.605082333f, -0.486901522f),
					new float4(0f, 0f, -1f, -0.417558134f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(22.043766f, -18.8446426f, 36.10256f, 1f)), name: "Solid 2475891 #1403")
					.WithMesh(new float3[] { new float3(-0.294216752f, -4.18285751f, -0.417558134f), new float3(-0.105306715f, -4.18285751f, -0.417558134f), new float3(0.812171459f, -4.18285751f, 0.116191857f), new float3(0.259358972f, -4.18285751f, 0.8405669f), new float3(-0.5984535f, -4.18285751f, -0.017245641f), new float3(-0.105306715f, 6.79714251f, -0.417558134f), new float3(-0.294216752f, 6.79714251f, -0.417558134f), new float3(-0.5984535f, 6.79714251f, -0.017245641f), new float3(0.259358972f, 6.79714251f, 0.8405669f), new float3(0.812171459f, 6.79714251f, 0.116191857f) }, new int[] { 0, 1, 2, 3, 4, -1, 5, 6, 7, 8, 9, -1, 7, 4, 3, 8, -1, 1, 5, 9, 2, -1, 2, 9, 8, 3, -1, 6, 0, 4, 7, -1, 0, 6, 5, 1, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 1f, 0f, -2.4400003f),
					new float4(0.5031672f, 0f, -0.8641891f, -0.0316847973f),
					new float4(-0.796158433f, 0f, -0.6050882f, -0.05013495f),
					new float4(0f, 0f, 1f, -0.0254159551f),
					new float4(0f, -1f, 0f, -3.65999961f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(21.8318367f, -19.3675f, 35.659584f, 1f)), name: "Solid 2476701 #1423")
					.WithMesh(new float3[] { new float3(-0.0822874457f, 2.4400003f, 0.0254159551f), new float3(0.106622584f, 2.4400003f, 0.0254159551f), new float3(-0.0243367013f, 2.4400003f, -0.0508340448f), new float3(0.106622584f, -3.65999961f, 0.0254159551f), new float3(-0.0243367013f, -3.65999961f, -0.0508340448f), new float3(-0.0822874457f, -3.65999961f, 0.0254159551f) }, new int[] { 0, 1, 2, -1, 3, 4, 2, 1, -1, 4, 5, 0, 2, -1, 5, 3, 1, 0, -1, 3, 5, 4, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 1f, 0f, -1.11833286f),
					new float4(0f, -1f, 0f, -0.7116671f),
					new float4(0f, 0f, -1f, -0.07624939f),
					new float4(0f, 0f, 1f, -0.07625061f),
					new float4(1f, 0f, 0f, -0.0847220644f),
					new float4(-1f, 0f, 0f, -0.06777794f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(21.81809f, -15.4533329f, 35.60875f, 1f)), name: "Solid 2475961 #1461")
					.WithMesh(new float3[] { new float3(-0.06777794f, 1.11833286f, -0.07624939f), new float3(-0.06777794f, 1.11833286f, 0.07625061f), new float3(0.0847220644f, 1.11833286f, 0.07625061f), new float3(0.0847220644f, 1.11833286f, -0.07624939f), new float3(-0.06777794f, -0.7116671f, 0.07625061f), new float3(-0.06777794f, -0.7116671f, -0.07624939f), new float3(0.0847220644f, -0.7116671f, -0.07624939f), new float3(0.0847220644f, -0.7116671f, 0.07625061f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 5, 0, 3, 6, -1, 1, 4, 7, 2, -1, 2, 7, 6, 3, -1, 4, 1, 0, 5, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(-1f, 0f, 0f, -0.06777794f),
					new float4(1f, 0f, 0f, -0.0847220644f),
					new float4(0f, 0.9417419f, 0.3363364f, -0.09631369f),
					new float4(0f, 0f, -1f, -0.232987061f),
					new float4(0f, 0.9863939f, -0.164398983f, -0.103069559f),
					new float4(0f, -1f, 0f, -0.08683991f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(21.81809f, -16.84066f, 35.1554871f, 1f)), name: "Solid 2475965 #1465")
					.WithMesh(new float3[] { new float3(-0.06777794f, -0.08683991f, -0.232987061f), new float3(-0.06777794f, -0.08683991f, 0.529512942f), new float3(-0.06777794f, 0.1037851f, -0.004237067f), new float3(-0.06777794f, 0.0656601f, -0.232987061f), new float3(0.0847220644f, -0.08683991f, 0.529512942f), new float3(0.0847220644f, -0.08683991f, -0.232987061f), new float3(0.0847220644f, 0.0656601f, -0.232987061f), new float3(0.0847220644f, 0.1037851f, -0.004237067f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 1, 4, 7, 2, -1, 5, 0, 3, 6, -1, 6, 3, 2, 7, -1, 0, 5, 4, 1, -1 }))
				;
		}

		// bm_c3a2f, Model: the mesh draws nothing where the exact CSG has a face; Water 2747524 #357 face 5 near (-4.88, -30,
		// -2.745) (5 brushes)
		static ContentsScene BmC3a2fModel0Site1()
		{
			return new ContentsScene()
				.WithContentsList("Solid", "Water", "Glass", "Grate")
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 1f, 0f, -1.98250055f),
					new float4(0f, -1f, 0f, -1.98249948f),
					new float4(-1f, 0f, 0f, -4.7275f),
					new float4(1f, 0f, 0f, -4.7275f),
					new float4(0f, 0f, 1f, -0.169444233f),
					new float4(0f, 0f, -1f, -0.135555759f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(-9.6075f, -17.69f, -4.134444f, 1f)), name: "Solid 1048861 #15")
					.WithMesh(new float3[] { new float3(4.7275f, 1.98250055f, -0.135555759f), new float3(-4.7275f, 1.98250055f, -0.135555759f), new float3(-4.7275f, 1.98250055f, 0.169444233f), new float3(4.7275f, 1.98250055f, 0.169444233f), new float3(-4.7275f, -1.98249948f, -0.135555759f), new float3(4.7275f, -1.98249948f, -0.135555759f), new float3(4.7275f, -1.98249948f, 0.169444233f), new float3(-4.7275f, -1.98249948f, 0.169444233f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 1, 4, 7, 2, -1, 5, 0, 3, 6, -1, 6, 3, 2, 7, -1, 0, 5, 4, 1, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 1f, 0f, -0.152499661f),
					new float4(0f, -1f, 0f, -0.152500346f),
					new float4(-1f, 0f, 0f, -4.7275f),
					new float4(1f, 0f, 0f, -4.7275f),
					new float4(0f, 0f, 1f, -1.35555565f),
					new float4(0f, 0f, -1f, -1.0844444f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(-9.6075f, -15.86f, -2.88055563f, 1f)), name: "Solid 1048867 #17")
					.WithMesh(new float3[] { new float3(4.7275f, 0.152499661f, -1.0844444f), new float3(-4.7275f, 0.152499661f, -1.0844444f), new float3(-4.7275f, 0.152499661f, 1.35555565f), new float3(4.7275f, 0.152499661f, 1.35555565f), new float3(-4.7275f, -0.152500346f, -1.0844444f), new float3(4.7275f, -0.152500346f, -1.0844444f), new float3(4.7275f, -0.152500346f, 1.35555565f), new float3(-4.7275f, -0.152500346f, 1.35555565f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 1, 4, 7, 2, -1, 5, 0, 3, 6, -1, 6, 3, 2, 7, -1, 0, 5, 4, 1, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 1f, 0f, -4.269999f),
					new float4(0f, -1f, 0f, -6.710001f),
					new float4(-1f, 0f, 0f, -0.169444621f),
					new float4(1f, 0f, 0f, -0.135555387f),
					new float4(0f, 0f, 1f, -1.52499986f),
					new float4(0f, 0f, -1f, -1.5250001f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(-5.01555538f, -23.9425f, -2.745f, 1f)), name: "Solid 1049000 #28")
					.WithMesh(new float3[] { new float3(0.135555387f, 4.269999f, -1.5250001f), new float3(-0.169444621f, 4.269999f, -1.5250001f), new float3(-0.169444621f, 4.269999f, 1.52499986f), new float3(0.135555387f, 4.269999f, 1.52499986f), new float3(-0.169444621f, -6.710001f, -1.5250001f), new float3(0.135555387f, -6.710001f, -1.5250001f), new float3(0.135555387f, -6.710001f, 1.52499986f), new float3(-0.169444621f, -6.710001f, 1.52499986f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 1, 4, 7, 2, -1, 5, 0, 3, 6, -1, 6, 3, 2, 7, -1, 0, 5, 4, 1, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 1f, 0f, -9.785418f),
					new float4(0f, -1f, 0f, -6.22708273f),
					new float4(0f, 0f, 1f, -1.21999991f),
					new float4(0f, 0f, -1f, -1.22000015f),
					new float4(1f, 0f, 0f, -5.252778f),
					new float4(-1f, 0f, 0f, -4.202222f),
				}, contents: 1, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(-10.1327782f, -24.8829174f, -2.745f, 1f)), name: "Water 959436 #141")
					.WithMesh(new float3[] { new float3(-4.202222f, 9.785418f, -1.22000015f), new float3(-4.202222f, 9.785418f, 1.21999991f), new float3(5.252778f, 9.785418f, 1.21999991f), new float3(5.252778f, 9.785418f, -1.22000015f), new float3(-4.202222f, -6.22708273f, 1.21999991f), new float3(-4.202222f, -6.22708273f, -1.22000015f), new float3(5.252778f, -6.22708273f, -1.22000015f), new float3(5.252778f, -6.22708273f, 1.21999991f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 1, 4, 7, 2, -1, 5, 0, 3, 6, -1, 6, 3, 2, 7, -1, 0, 5, 4, 1, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, -1f, 0f, -6.04916763f),
					new float4(0f, 1f, 0f, -9.505833f),
					new float4(0.194778189f, 0f, 0.9808473f, -1.09691429f),
					new float4(0.194778189f, 0f, -0.9808473f, -1.37114275f),
					new float4(1f, 0f, 0f, -1.34390616f),
					new float4(-1f, 0f, 0f, -1.34390628f),
				}, contents: 1, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(-3.53609371f, -24.6033325f, -2.6052084f, 1f)), name: "Water 2747524 #357")
					.WithMesh(new float3[] { new float3(-1.34390628f, -6.04916763f, 1.38520837f), new float3(-1.34390628f, -6.04916763f, -1.66479158f), new float3(1.34390616f, -6.04916763f, -1.13104165f), new float3(1.34390616f, -6.04916763f, 0.85145843f), new float3(-1.34390628f, 9.505833f, -1.66479158f), new float3(-1.34390628f, 9.505833f, 1.38520837f), new float3(1.34390616f, 9.505833f, 0.85145843f), new float3(1.34390616f, 9.505833f, -1.13104165f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 5, 0, 3, 6, -1, 1, 4, 7, 2, -1, 2, 7, 6, 3, -1, 4, 1, 0, 5, -1 }))
				;
		}

		// bm_c3a2f, Model: the mesh has a triangle the exact CSG did not make; Solid 1049247 #41 face -1 near (10.065,
		// -3.264, 0.305) (4 brushes)
		static ContentsScene BmC3a2fModel0Site2()
		{
			return new ContentsScene()
				.WithContentsList("Solid", "Water", "Glass", "Grate")
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 1f, 0f, -3.73625016f),
					new float4(0f, -1f, 0f, -5.87124968f),
					new float4(-0.4472136f, 0f, 0.8944272f, -2.21271372f),
					new float4(0f, 0f, -1f, -2.32138872f),
					new float4(0.991777837f, 0f, 0.127971336f, -0.173472017f),
					new float4(-0.9912279f, 0f, -0.132163718f, -0.138845563f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(10.2005558f, 2.51625f, -2.10111117f, 1f)), name: "Solid 1049098 #37")
					.WithMesh(new float3[] { new float3(0.169444174f, 3.73625016f, -2.32138872f), new float3(-0.4405558f, 3.73625016f, 2.25361133f), new float3(-0.135555819f, 3.73625016f, 2.40611124f), new float3(0.474444181f, 3.73625016f, -2.32138872f), new float3(-0.4405558f, -5.87124968f, 2.25361133f), new float3(0.169444174f, -5.87124968f, -2.32138872f), new float3(0.474444181f, -5.87124968f, -2.32138872f), new float3(-0.135555819f, -5.87124968f, 2.40611124f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 1, 4, 7, 2, -1, 5, 0, 3, 6, -1, 6, 3, 2, 7, -1, 0, 5, 4, 1, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, -1f, 0f, -3.73625f),
					new float4(0.4472136f, 0f, -0.8944272f, -1.54965711f),
					new float4(-0.707106769f, 0f, 0.707106769f, -1.53363609f),
					new float4(-0.839570165f, 0f, -0.5432513f, -0.122176766f),
					new float4(0.816967845f, 0f, 0.5766832f, -0.152680784f),
					new float4(0f, 1f, 0f, -5.87125f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(9.022917f, 0.38125f, 1.51652777f, 1f)), name: "Solid 1049247 #41")
					.WithMesh(new float3[] { new float3(1.04208326f, -3.73625f, -1.21152771f), new float3(-0.787916839f, -3.73625f, 1.38097215f), new float3(-0.940416753f, -3.73625f, 1.22847223f), new float3(0.7370832f, -3.73625f, -1.36402774f), new float3(0.7370832f, 5.87125f, -1.36402774f), new float3(1.04208326f, 5.87125f, -1.21152771f), new float3(-0.787916839f, 5.87125f, 1.38097215f), new float3(-0.940416753f, 5.87125f, 1.22847223f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 0, 3, -1, 6, 7, 2, 1, -1, 7, 4, 3, 2, -1, 5, 6, 1, 0, -1, 6, 5, 4, 7, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 1f, 0f, -0.186388925f),
					new float4(-0.81697154f, 0.0004534285f, -0.5766778f, -1.01377928f),
					new float4(1f, 0f, 0f, -1.42335415f),
					new float4(0f, 0f, 1f, -2.05870128f),
					new float4(-0.7682219f, 0f, -0.6401836f, -1.00558138f),
					new float4(0f, -1f, 0f, -0.118611075f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(10.9291458f, -3.236389f, 0.8387987f, 1f)), name: "Solid 1049642 #59")
					.WithMesh(new float3[] { new float3(1.42335415f, 0.186388925f, -3.27879858f), new float3(-0.8632301f, 0.186388925f, -0.534891963f), new float3(-2.6939764f, 0.186388925f, 2.05870128f), new float3(1.42335415f, 0.186388925f, 2.05870128f), new float3(-0.8643369f, -0.118611075f, -0.533563852f), new float3(-2.69414568f, -0.118611075f, 2.05870128f), new float3(1.42335415f, -0.118611075f, 2.05870128f), new float3(1.42335415f, -0.118611075f, -3.27879858f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 2, 1, -1, 6, 7, 0, 3, -1, 5, 6, 3, 2, -1, 7, 4, 1, 0, -1, 4, 7, 6, 5, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 1f, 0f, -0.142333224f),
					new float4(-0.991777837f, 0f, -0.127971336f, -0.639136851f),
					new float4(0.768221259f, 0f, 0.6401844f, -0.6820951f),
					new float4(0f, -1f, 0f, -0.162666783f),
					new float4(0.7633863f, 0f, -0.6459423f, -1.4077431f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(10.9901667f, -3.19233322f, -1.87066662f, 1f)), name: "Solid 1049663 #60")
					.WithMesh(new float3[] { new float3(1.3623333f, 0.142333224f, -0.5693333f), new float3(-0.315166682f, 0.142333224f, -2.55183339f), new float3(-0.925166667f, 0.142333224f, 2.17566657f), new float3(-0.315166682f, -0.162666783f, -2.55183339f), new float3(-0.925166667f, -0.162666783f, 2.17566657f), new float3(1.3623333f, -0.162666783f, -0.5693333f) }, new int[] { 0, 1, 2, -1, 1, 3, 4, 2, -1, 5, 0, 2, 4, -1, 3, 5, 4, -1, 5, 3, 1, 0, -1 }))
				;
		}

		// bm_c3a2f, Model: the mesh draws nothing where the exact CSG has a face; Solid 1561734 #118 face 1 near (-58.102,
		// -20, -2.745) (5 brushes)
		static ContentsScene BmC3a2fModel0Site3()
		{
			return new ContentsScene()
				.WithContentsList("Solid", "Water", "Glass", "Grate")
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 1f, 0f, -6.506666f),
					new float4(0f, 0f, 1f, -0.18638894f),
					new float4(0f, -1f, 0f, -8.133333f),
					new float4(0f, 0f, -1f, -0.11861106f),
					new float4(1f, 0f, 0f, -5.794999f),
					new float4(-1f, 0f, 0f, -5.795001f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(-63.8975f, -12.4541664f, 1.033611f, 1f)), name: "Solid 1561726 #116")
					.WithMesh(new float3[] { new float3(-5.795001f, 6.506666f, -0.11861106f), new float3(-5.795001f, 6.506666f, 0.18638894f), new float3(5.794999f, 6.506666f, 0.18638894f), new float3(5.794999f, 6.506666f, -0.11861106f), new float3(-5.795001f, -8.133333f, 0.18638894f), new float3(5.794999f, -8.133333f, 0.18638894f), new float3(-5.795001f, -8.133333f, -0.11861106f), new float3(5.794999f, -8.133333f, -0.11861106f) }, new int[] { 0, 1, 2, 3, -1, 1, 4, 5, 2, -1, 4, 6, 7, 5, -1, 6, 0, 3, 7, -1, 7, 3, 2, 5, -1, 0, 6, 4, 1, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(1f, 0f, 0f, -0.118610993f),
					new float4(-1f, 0f, 0f, -0.186389014f),
					new float4(0f, 0f, -1f, -3.66f),
					new float4(0f, 0f, 1f, -3.65999985f),
					new float4(0f, 1f, 0f, -0.5083338f),
					new float4(0f, -1f, 0f, -0.4066662f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(-57.91611f, -20.1808338f, -2.745f, 1f)), name: "Solid 1561734 #118")
					.WithMesh(new float3[] { new float3(0.118610993f, -0.4066662f, 3.65999985f), new float3(0.118610993f, -0.4066662f, -3.66f), new float3(0.118610993f, 0.5083338f, -3.66f), new float3(0.118610993f, 0.5083338f, 3.65999985f), new float3(-0.186389014f, -0.4066662f, -3.66f), new float3(-0.186389014f, -0.4066662f, 3.65999985f), new float3(-0.186389014f, 0.5083338f, 3.65999985f), new float3(-0.186389014f, 0.5083338f, -3.66f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 1, 4, 7, 2, -1, 5, 0, 3, 6, -1, 6, 3, 2, 7, -1, 0, 5, 4, 1, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(1f, 0f, 0f, -0.118610993f),
					new float4(-1f, 0f, 0f, -0.186389014f),
					new float4(0f, 0f, -1f, -1.0675f),
					new float4(0f, 0f, 1f, -1.0675f),
					new float4(0f, 1f, 0f, -2.20277834f),
					new float4(0f, -1f, 0f, -1.76222169f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(-57.91611f, -17.9102783f, -0.1525f, 1f)), name: "Solid 1561739 #120")
					.WithMesh(new float3[] { new float3(0.118610993f, -1.76222169f, 1.0675f), new float3(0.118610993f, -1.76222169f, -1.0675f), new float3(0.118610993f, 2.20277834f, -1.0675f), new float3(0.118610993f, 2.20277834f, 1.0675f), new float3(-0.186389014f, -1.76222169f, -1.0675f), new float3(-0.186389014f, -1.76222169f, 1.0675f), new float3(-0.186389014f, 2.20277834f, 1.0675f), new float3(-0.186389014f, 2.20277834f, -1.0675f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 1, 4, 7, 2, -1, 5, 0, 3, 6, -1, 6, 3, 2, 7, -1, 0, 5, 4, 1, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 1f, 0f, -9.785418f),
					new float4(0f, -1f, 0f, -6.22708273f),
					new float4(0f, 0f, 1f, -1.21999991f),
					new float4(0f, 0f, -1f, -1.22000015f),
					new float4(1f, 0f, 0f, -20.0791664f),
					new float4(-1f, 0f, 0f, -16.0633354f),
				}, contents: 1, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(-42.0391655f, -24.8829174f, -2.745f, 1f)), name: "Water 3122176 #290")
					.WithMesh(new float3[] { new float3(-16.0633354f, 9.785418f, -1.22000015f), new float3(-16.0633354f, 9.785418f, 1.21999991f), new float3(20.0791664f, 9.785418f, 1.21999991f), new float3(20.0791664f, 9.785418f, -1.22000015f), new float3(-16.0633354f, -6.22708273f, 1.21999991f), new float3(-16.0633354f, -6.22708273f, -1.22000015f), new float3(20.0791664f, -6.22708273f, -1.22000015f), new float3(20.0791664f, -6.22708273f, 1.21999991f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 1, 4, 7, 2, -1, 5, 0, 3, 6, -1, 6, 3, 2, 7, -1, 0, 5, 4, 1, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 1f, 0f, -9.785418f),
					new float4(0f, -1f, 0f, -6.22708273f),
					new float4(0f, 0f, 1f, -3.65999985f),
					new float4(0f, 0f, -1f, -3.66f),
					new float4(1f, 0f, 0f, -6.4388895f),
					new float4(-1f, 0f, 0f, -5.15111065f),
				}, contents: 1, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(-64.54139f, -24.8829174f, -2.745f, 1f)), name: "Water 3122179 #291")
					.WithMesh(new float3[] { new float3(-5.15111065f, 9.785418f, -3.66f), new float3(-5.15111065f, 9.785418f, 3.65999985f), new float3(6.4388895f, 9.785418f, 3.65999985f), new float3(6.4388895f, 9.785418f, -3.66f), new float3(-5.15111065f, -6.22708273f, 3.65999985f), new float3(-5.15111065f, -6.22708273f, -3.66f), new float3(6.4388895f, -6.22708273f, -3.66f), new float3(6.4388895f, -6.22708273f, 3.65999985f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 1, 4, 7, 2, -1, 5, 0, 3, 6, -1, 6, 3, 2, 7, -1, 0, 5, 4, 1, -1 }))
				;
		}

		// bm_c3a2f, Model: the mesh draws nothing where the exact CSG has a face; Water 2743079 #276 face 4 near (-14.335,
		// -15.75, -2.745) (3 brushes)
		static ContentsScene BmC3a2fModel0Site4()
		{
			return new ContentsScene()
				.WithContentsList("Solid", "Water", "Glass", "Grate")
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, -1f, 0f, -2.84666681f),
					new float4(0f, 0f, 1f, -1.52499986f),
					new float4(0f, 0f, -1f, -1.5250001f),
					new float4(1f, 0f, 0f, -5.08333349f),
					new float4(-1f, 0f, 0f, -4.0666666f),
					new float4(0f, 1f, 0f, -4.47333336f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(-10.2683334f, -12.8608332f, -2.745f, 1f)), name: "Solid 1076598 #92")
					.WithMesh(new float3[] { new float3(-4.0666666f, -2.84666681f, 1.52499986f), new float3(-4.0666666f, -2.84666681f, -1.5250001f), new float3(5.08333349f, -2.84666681f, -1.5250001f), new float3(5.08333349f, -2.84666681f, 1.52499986f), new float3(5.08333349f, 4.47333336f, 1.52499986f), new float3(-4.0666666f, 4.47333336f, 1.52499986f), new float3(-4.0666666f, 4.47333336f, -1.5250001f), new float3(5.08333349f, 4.47333336f, -1.5250001f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 0, 3, -1, 6, 7, 2, 1, -1, 7, 4, 3, 2, -1, 5, 6, 1, 0, -1, 6, 5, 4, 7, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 1f, 0f, -9.785418f),
					new float4(0f, -1f, 0f, -6.22708273f),
					new float4(0f, 0f, 1f, -1.21999991f),
					new float4(0f, 0f, -1f, -1.22000015f),
					new float4(1f, 0f, 0f, -5.252778f),
					new float4(-1f, 0f, 0f, -4.202222f),
				}, contents: 1, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(-10.1327782f, -24.8829174f, -2.745f, 1f)), name: "Water 959436 #141")
					.WithMesh(new float3[] { new float3(-4.202222f, 9.785418f, -1.22000015f), new float3(-4.202222f, 9.785418f, 1.21999991f), new float3(5.252778f, 9.785418f, 1.21999991f), new float3(5.252778f, 9.785418f, -1.22000015f), new float3(-4.202222f, -6.22708273f, 1.21999991f), new float3(-4.202222f, -6.22708273f, -1.22000015f), new float3(5.252778f, -6.22708273f, -1.22000015f), new float3(5.252778f, -6.22708273f, 1.21999991f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 1, 4, 7, 2, -1, 5, 0, 3, 6, -1, 6, 3, 2, 7, -1, 0, 5, 4, 1, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 1f, 0f, -9.971806f),
					new float4(0f, -1f, 0f, -6.34569454f),
					new float4(0f, 0f, 1f, -3.8125f),
					new float4(0f, 0f, -1f, -3.8125f),
					new float4(1f, 0f, 0f, -4.23611164f),
					new float4(-1f, 0f, 0f, -3.38888836f),
				}, contents: 1, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(-18.5711117f, -25.0693054f, -2.745f, 1f)), name: "Water 2743079 #276")
					.WithMesh(new float3[] { new float3(-3.38888836f, 9.971806f, -3.8125f), new float3(-3.38888836f, 9.971806f, 3.8125f), new float3(4.23611164f, 9.971806f, 3.8125f), new float3(4.23611164f, 9.971806f, -3.8125f), new float3(-3.38888836f, -6.34569454f, 3.8125f), new float3(-3.38888836f, -6.34569454f, -3.8125f), new float3(4.23611164f, -6.34569454f, -3.8125f), new float3(4.23611164f, -6.34569454f, 3.8125f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 1, 4, 7, 2, -1, 5, 0, 3, 6, -1, 6, 3, 2, 7, -1, 0, 5, 4, 1, -1 }))
				;
		}

		// bm_c3a2f, Model: the mesh draws nothing where the exact CSG has a face; Water 2743079 #276 face 4 near (-14.335,
		// -19.5, -2.745) (3 brushes)
		static ContentsScene BmC3a2fModel0Site5()
		{
			return new ContentsScene()
				.WithContentsList("Solid", "Water", "Glass", "Grate")
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 1f, 0f, -0.118610688f),
					new float4(0f, -1f, 0f, -0.186389312f),
					new float4(-1f, 0f, 0f, -5.252778f),
					new float4(1f, 0f, 0f, -4.202222f),
					new float4(0f, 0f, 1f, -1.21999991f),
					new float4(0f, 0f, -1f, -1.22000015f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(-9.082222f, -19.48611f, -2.745f, 1f)), name: "Solid 1048862 #16")
					.WithMesh(new float3[] { new float3(4.202222f, 0.118610688f, -1.22000015f), new float3(-5.252778f, 0.118610688f, -1.22000015f), new float3(-5.252778f, 0.118610688f, 1.21999991f), new float3(4.202222f, 0.118610688f, 1.21999991f), new float3(-5.252778f, -0.186389312f, -1.22000015f), new float3(4.202222f, -0.186389312f, -1.22000015f), new float3(4.202222f, -0.186389312f, 1.21999991f), new float3(-5.252778f, -0.186389312f, 1.21999991f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 1, 4, 7, 2, -1, 5, 0, 3, 6, -1, 6, 3, 2, 7, -1, 0, 5, 4, 1, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 1f, 0f, -9.785418f),
					new float4(0f, -1f, 0f, -6.22708273f),
					new float4(0f, 0f, 1f, -1.21999991f),
					new float4(0f, 0f, -1f, -1.22000015f),
					new float4(1f, 0f, 0f, -5.252778f),
					new float4(-1f, 0f, 0f, -4.202222f),
				}, contents: 1, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(-10.1327782f, -24.8829174f, -2.745f, 1f)), name: "Water 959436 #141")
					.WithMesh(new float3[] { new float3(-4.202222f, 9.785418f, -1.22000015f), new float3(-4.202222f, 9.785418f, 1.21999991f), new float3(5.252778f, 9.785418f, 1.21999991f), new float3(5.252778f, 9.785418f, -1.22000015f), new float3(-4.202222f, -6.22708273f, 1.21999991f), new float3(-4.202222f, -6.22708273f, -1.22000015f), new float3(5.252778f, -6.22708273f, -1.22000015f), new float3(5.252778f, -6.22708273f, 1.21999991f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 1, 4, 7, 2, -1, 5, 0, 3, 6, -1, 6, 3, 2, 7, -1, 0, 5, 4, 1, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 1f, 0f, -9.971806f),
					new float4(0f, -1f, 0f, -6.34569454f),
					new float4(0f, 0f, 1f, -3.8125f),
					new float4(0f, 0f, -1f, -3.8125f),
					new float4(1f, 0f, 0f, -4.23611164f),
					new float4(-1f, 0f, 0f, -3.38888836f),
				}, contents: 1, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(-18.5711117f, -25.0693054f, -2.745f, 1f)), name: "Water 2743079 #276")
					.WithMesh(new float3[] { new float3(-3.38888836f, 9.971806f, -3.8125f), new float3(-3.38888836f, 9.971806f, 3.8125f), new float3(4.23611164f, 9.971806f, 3.8125f), new float3(4.23611164f, 9.971806f, -3.8125f), new float3(-3.38888836f, -6.34569454f, 3.8125f), new float3(-3.38888836f, -6.34569454f, -3.8125f), new float3(4.23611164f, -6.34569454f, -3.8125f), new float3(4.23611164f, -6.34569454f, 3.8125f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 1, 4, 7, 2, -1, 5, 0, 3, 6, -1, 6, 3, 2, 7, -1, 0, 5, 4, 1, -1 }))
				;
		}

		// bm_c3a2f, Model: the mesh draws nothing where the exact CSG has a face; Water 2743079 #276 face 4 near (-14.335,
		// -25.25, -2.745) (3 brushes)
		static ContentsScene BmC3a2fModel0Site6()
		{
			return new ContentsScene()
				.WithContentsList("Solid", "Water", "Glass", "Grate")
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, -1f, 0f, -2.2536118f),
					new float4(0f, 0f, 1f, -1.52499986f),
					new float4(0f, 0f, -1f, -1.5250001f),
					new float4(1f, 0f, 0f, -0.338888556f),
					new float4(-1f, 0f, 0f, -0.271111459f),
					new float4(0f, 1f, 0f, -3.54138827f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(-14.0638885f, -23.2138882f, -2.745f, 1f)), name: "Solid 1076601 #93")
					.WithMesh(new float3[] { new float3(-0.271111459f, -2.2536118f, 1.52499986f), new float3(-0.271111459f, -2.2536118f, -1.5250001f), new float3(0.338888556f, -2.2536118f, -1.5250001f), new float3(0.338888556f, -2.2536118f, 1.52499986f), new float3(0.338888556f, 3.54138827f, 1.52499986f), new float3(-0.271111459f, 3.54138827f, 1.52499986f), new float3(-0.271111459f, 3.54138827f, -1.5250001f), new float3(0.338888556f, 3.54138827f, -1.5250001f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 0, 3, -1, 6, 7, 2, 1, -1, 7, 4, 3, 2, -1, 5, 6, 1, 0, -1, 6, 5, 4, 7, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 1f, 0f, -9.785418f),
					new float4(0f, -1f, 0f, -6.22708273f),
					new float4(0f, 0f, 1f, -1.21999991f),
					new float4(0f, 0f, -1f, -1.22000015f),
					new float4(1f, 0f, 0f, -5.252778f),
					new float4(-1f, 0f, 0f, -4.202222f),
				}, contents: 1, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(-10.1327782f, -24.8829174f, -2.745f, 1f)), name: "Water 959436 #141")
					.WithMesh(new float3[] { new float3(-4.202222f, 9.785418f, -1.22000015f), new float3(-4.202222f, 9.785418f, 1.21999991f), new float3(5.252778f, 9.785418f, 1.21999991f), new float3(5.252778f, 9.785418f, -1.22000015f), new float3(-4.202222f, -6.22708273f, 1.21999991f), new float3(-4.202222f, -6.22708273f, -1.22000015f), new float3(5.252778f, -6.22708273f, -1.22000015f), new float3(5.252778f, -6.22708273f, 1.21999991f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 1, 4, 7, 2, -1, 5, 0, 3, 6, -1, 6, 3, 2, 7, -1, 0, 5, 4, 1, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 1f, 0f, -9.971806f),
					new float4(0f, -1f, 0f, -6.34569454f),
					new float4(0f, 0f, 1f, -3.8125f),
					new float4(0f, 0f, -1f, -3.8125f),
					new float4(1f, 0f, 0f, -4.23611164f),
					new float4(-1f, 0f, 0f, -3.38888836f),
				}, contents: 1, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(-18.5711117f, -25.0693054f, -2.745f, 1f)), name: "Water 2743079 #276")
					.WithMesh(new float3[] { new float3(-3.38888836f, 9.971806f, -3.8125f), new float3(-3.38888836f, 9.971806f, 3.8125f), new float3(4.23611164f, 9.971806f, 3.8125f), new float3(4.23611164f, 9.971806f, -3.8125f), new float3(-3.38888836f, -6.34569454f, 3.8125f), new float3(-3.38888836f, -6.34569454f, -3.8125f), new float3(4.23611164f, -6.34569454f, -3.8125f), new float3(4.23611164f, -6.34569454f, 3.8125f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 1, 4, 7, 2, -1, 5, 0, 3, 6, -1, 6, 3, 2, 7, -1, 0, 5, 4, 1, -1 }))
				;
		}

		// bm_c3a2f, Model: the mesh draws nothing where the exact CSG has a face; Water 2743079 #276 face 5 near (-21.96,
		// -16, -2.745) (3 brushes)
		static ContentsScene BmC3a2fModel0Site7()
		{
			return new ContentsScene()
				.WithContentsList("Solid", "Water", "Glass", "Grate")
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 1f, 0f, -0.186388478f),
					new float4(0f, -1f, 0f, -0.118611529f),
					new float4(-1f, 0f, 0f, -16.0633354f),
					new float4(1f, 0f, 0f, -20.0791664f),
					new float4(0f, 0f, 1f, -1.21999991f),
					new float4(0f, 0f, -1f, -1.22000015f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(-42.0391655f, -15.8938885f, -2.745f, 1f)), name: "Solid 1075754 #87")
					.WithMesh(new float3[] { new float3(20.0791664f, 0.186388478f, -1.22000015f), new float3(-16.0633354f, 0.186388478f, -1.22000015f), new float3(-16.0633354f, 0.186388478f, 1.21999991f), new float3(20.0791664f, 0.186388478f, 1.21999991f), new float3(-16.0633354f, -0.118611529f, -1.22000015f), new float3(20.0791664f, -0.118611529f, -1.22000015f), new float3(20.0791664f, -0.118611529f, 1.21999991f), new float3(-16.0633354f, -0.118611529f, 1.21999991f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 1, 4, 7, 2, -1, 5, 0, 3, 6, -1, 6, 3, 2, 7, -1, 0, 5, 4, 1, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 1f, 0f, -9.971806f),
					new float4(0f, -1f, 0f, -6.34569454f),
					new float4(0f, 0f, 1f, -3.8125f),
					new float4(0f, 0f, -1f, -3.8125f),
					new float4(1f, 0f, 0f, -4.23611164f),
					new float4(-1f, 0f, 0f, -3.38888836f),
				}, contents: 1, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(-18.5711117f, -25.0693054f, -2.745f, 1f)), name: "Water 2743079 #276")
					.WithMesh(new float3[] { new float3(-3.38888836f, 9.971806f, -3.8125f), new float3(-3.38888836f, 9.971806f, 3.8125f), new float3(4.23611164f, 9.971806f, 3.8125f), new float3(4.23611164f, 9.971806f, -3.8125f), new float3(-3.38888836f, -6.34569454f, 3.8125f), new float3(-3.38888836f, -6.34569454f, -3.8125f), new float3(4.23611164f, -6.34569454f, -3.8125f), new float3(4.23611164f, -6.34569454f, 3.8125f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 1, 4, 7, 2, -1, 5, 0, 3, 6, -1, 6, 3, 2, 7, -1, 0, 5, 4, 1, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 1f, 0f, -9.785418f),
					new float4(0f, -1f, 0f, -6.22708273f),
					new float4(0f, 0f, 1f, -1.21999991f),
					new float4(0f, 0f, -1f, -1.22000015f),
					new float4(1f, 0f, 0f, -20.0791664f),
					new float4(-1f, 0f, 0f, -16.0633354f),
				}, contents: 1, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(-42.0391655f, -24.8829174f, -2.745f, 1f)), name: "Water 3122176 #290")
					.WithMesh(new float3[] { new float3(-16.0633354f, 9.785418f, -1.22000015f), new float3(-16.0633354f, 9.785418f, 1.21999991f), new float3(20.0791664f, 9.785418f, 1.21999991f), new float3(20.0791664f, 9.785418f, -1.22000015f), new float3(-16.0633354f, -6.22708273f, 1.21999991f), new float3(-16.0633354f, -6.22708273f, -1.22000015f), new float3(20.0791664f, -6.22708273f, -1.22000015f), new float3(20.0791664f, -6.22708273f, 1.21999991f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 1, 4, 7, 2, -1, 5, 0, 3, 6, -1, 6, 3, 2, 7, -1, 0, 5, 4, 1, -1 }))
				;
		}

		// bm_c3a2f, Model: the mesh draws nothing where the exact CSG has a face; Water 2743079 #276 face 5 near (-21.96,
		// -19.5, -2.745) (3 brushes)
		static ContentsScene BmC3a2fModel0Site8()
		{
			return new ContentsScene()
				.WithContentsList("Solid", "Water", "Glass", "Grate")
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 1f, 0f, -0.186388314f),
					new float4(0f, -1f, 0f, -0.118611678f),
					new float4(-1f, 0f, 0f, -16.0633354f),
					new float4(1f, 0f, 0f, -20.0791664f),
					new float4(0f, 0f, 1f, -1.21999991f),
					new float4(0f, 0f, -1f, -1.22000015f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(-42.0391655f, -19.5538883f, -2.745f, 1f)), name: "Solid 1075756 #89")
					.WithMesh(new float3[] { new float3(20.0791664f, 0.186388314f, -1.22000015f), new float3(-16.0633354f, 0.186388314f, -1.22000015f), new float3(-16.0633354f, 0.186388314f, 1.21999991f), new float3(20.0791664f, 0.186388314f, 1.21999991f), new float3(-16.0633354f, -0.118611678f, -1.22000015f), new float3(20.0791664f, -0.118611678f, -1.22000015f), new float3(20.0791664f, -0.118611678f, 1.21999991f), new float3(-16.0633354f, -0.118611678f, 1.21999991f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 1, 4, 7, 2, -1, 5, 0, 3, 6, -1, 6, 3, 2, 7, -1, 0, 5, 4, 1, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 1f, 0f, -9.971806f),
					new float4(0f, -1f, 0f, -6.34569454f),
					new float4(0f, 0f, 1f, -3.8125f),
					new float4(0f, 0f, -1f, -3.8125f),
					new float4(1f, 0f, 0f, -4.23611164f),
					new float4(-1f, 0f, 0f, -3.38888836f),
				}, contents: 1, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(-18.5711117f, -25.0693054f, -2.745f, 1f)), name: "Water 2743079 #276")
					.WithMesh(new float3[] { new float3(-3.38888836f, 9.971806f, -3.8125f), new float3(-3.38888836f, 9.971806f, 3.8125f), new float3(4.23611164f, 9.971806f, 3.8125f), new float3(4.23611164f, 9.971806f, -3.8125f), new float3(-3.38888836f, -6.34569454f, 3.8125f), new float3(-3.38888836f, -6.34569454f, -3.8125f), new float3(4.23611164f, -6.34569454f, -3.8125f), new float3(4.23611164f, -6.34569454f, 3.8125f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 1, 4, 7, 2, -1, 5, 0, 3, 6, -1, 6, 3, 2, 7, -1, 0, 5, 4, 1, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 1f, 0f, -9.785418f),
					new float4(0f, -1f, 0f, -6.22708273f),
					new float4(0f, 0f, 1f, -1.21999991f),
					new float4(0f, 0f, -1f, -1.22000015f),
					new float4(1f, 0f, 0f, -20.0791664f),
					new float4(-1f, 0f, 0f, -16.0633354f),
				}, contents: 1, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(-42.0391655f, -24.8829174f, -2.745f, 1f)), name: "Water 3122176 #290")
					.WithMesh(new float3[] { new float3(-16.0633354f, 9.785418f, -1.22000015f), new float3(-16.0633354f, 9.785418f, 1.21999991f), new float3(20.0791664f, 9.785418f, 1.21999991f), new float3(20.0791664f, 9.785418f, -1.22000015f), new float3(-16.0633354f, -6.22708273f, 1.21999991f), new float3(-16.0633354f, -6.22708273f, -1.22000015f), new float3(20.0791664f, -6.22708273f, -1.22000015f), new float3(20.0791664f, -6.22708273f, 1.21999991f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 1, 4, 7, 2, -1, 5, 0, 3, 6, -1, 6, 3, 2, 7, -1, 0, 5, 4, 1, -1 }))
				;
		}

		// bm_c3a2f, Model: the mesh draws nothing where the exact CSG has a face; Water 2743079 #276 face 5 near (-21.96,
		// -25.25, -2.745) (3 brushes)
		static ContentsScene BmC3a2fModel0Site9()
		{
			return new ContentsScene()
				.WithContentsList("Solid", "Water", "Glass", "Grate")
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, -1f, 0f, -2.2536118f),
					new float4(0f, 0f, 1f, -1.52499986f),
					new float4(0f, 0f, -1f, -1.5250001f),
					new float4(1f, 0f, 0f, -0.338889152f),
					new float4(-1f, 0f, 0f, -0.271110833f),
					new float4(0f, 1f, 0f, -3.54138827f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(-22.29889f, -23.2138882f, -2.745f, 1f)), name: "Solid 1076616 #96")
					.WithMesh(new float3[] { new float3(-0.271110833f, -2.2536118f, 1.52499986f), new float3(-0.271110833f, -2.2536118f, -1.5250001f), new float3(0.338889152f, -2.2536118f, -1.5250001f), new float3(0.338889152f, -2.2536118f, 1.52499986f), new float3(0.338889152f, 3.54138827f, 1.52499986f), new float3(-0.271110833f, 3.54138827f, 1.52499986f), new float3(-0.271110833f, 3.54138827f, -1.5250001f), new float3(0.338889152f, 3.54138827f, -1.5250001f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 0, 3, -1, 6, 7, 2, 1, -1, 7, 4, 3, 2, -1, 5, 6, 1, 0, -1, 6, 5, 4, 7, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 1f, 0f, -9.971806f),
					new float4(0f, -1f, 0f, -6.34569454f),
					new float4(0f, 0f, 1f, -3.8125f),
					new float4(0f, 0f, -1f, -3.8125f),
					new float4(1f, 0f, 0f, -4.23611164f),
					new float4(-1f, 0f, 0f, -3.38888836f),
				}, contents: 1, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(-18.5711117f, -25.0693054f, -2.745f, 1f)), name: "Water 2743079 #276")
					.WithMesh(new float3[] { new float3(-3.38888836f, 9.971806f, -3.8125f), new float3(-3.38888836f, 9.971806f, 3.8125f), new float3(4.23611164f, 9.971806f, 3.8125f), new float3(4.23611164f, 9.971806f, -3.8125f), new float3(-3.38888836f, -6.34569454f, 3.8125f), new float3(-3.38888836f, -6.34569454f, -3.8125f), new float3(4.23611164f, -6.34569454f, -3.8125f), new float3(4.23611164f, -6.34569454f, 3.8125f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 1, 4, 7, 2, -1, 5, 0, 3, 6, -1, 6, 3, 2, 7, -1, 0, 5, 4, 1, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 1f, 0f, -9.785418f),
					new float4(0f, -1f, 0f, -6.22708273f),
					new float4(0f, 0f, 1f, -1.21999991f),
					new float4(0f, 0f, -1f, -1.22000015f),
					new float4(1f, 0f, 0f, -20.0791664f),
					new float4(-1f, 0f, 0f, -16.0633354f),
				}, contents: 1, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(-42.0391655f, -24.8829174f, -2.745f, 1f)), name: "Water 3122176 #290")
					.WithMesh(new float3[] { new float3(-16.0633354f, 9.785418f, -1.22000015f), new float3(-16.0633354f, 9.785418f, 1.21999991f), new float3(20.0791664f, 9.785418f, 1.21999991f), new float3(20.0791664f, 9.785418f, -1.22000015f), new float3(-16.0633354f, -6.22708273f, 1.21999991f), new float3(-16.0633354f, -6.22708273f, -1.22000015f), new float3(20.0791664f, -6.22708273f, -1.22000015f), new float3(20.0791664f, -6.22708273f, 1.21999991f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 1, 4, 7, 2, -1, 5, 0, 3, 6, -1, 6, 3, 2, 7, -1, 0, 5, 4, 1, -1 }))
				;
		}

		// bm_c3a2f, Model: the mesh draws nothing where the exact CSG has a face; Water 2747524 #357 face 5 near (-4.88,
		// -19.5, -2.745) (3 brushes)
		static ContentsScene BmC3a2fModel0Site10()
		{
			return new ContentsScene()
				.WithContentsList("Solid", "Water", "Glass", "Grate")
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 1f, 0f, -0.118610688f),
					new float4(0f, -1f, 0f, -0.186389312f),
					new float4(-1f, 0f, 0f, -5.252778f),
					new float4(1f, 0f, 0f, -4.202222f),
					new float4(0f, 0f, 1f, -1.21999991f),
					new float4(0f, 0f, -1f, -1.22000015f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(-9.082222f, -19.48611f, -2.745f, 1f)), name: "Solid 1048862 #16")
					.WithMesh(new float3[] { new float3(4.202222f, 0.118610688f, -1.22000015f), new float3(-5.252778f, 0.118610688f, -1.22000015f), new float3(-5.252778f, 0.118610688f, 1.21999991f), new float3(4.202222f, 0.118610688f, 1.21999991f), new float3(-5.252778f, -0.186389312f, -1.22000015f), new float3(4.202222f, -0.186389312f, -1.22000015f), new float3(4.202222f, -0.186389312f, 1.21999991f), new float3(-5.252778f, -0.186389312f, 1.21999991f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 1, 4, 7, 2, -1, 5, 0, 3, 6, -1, 6, 3, 2, 7, -1, 0, 5, 4, 1, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 1f, 0f, -9.785418f),
					new float4(0f, -1f, 0f, -6.22708273f),
					new float4(0f, 0f, 1f, -1.21999991f),
					new float4(0f, 0f, -1f, -1.22000015f),
					new float4(1f, 0f, 0f, -5.252778f),
					new float4(-1f, 0f, 0f, -4.202222f),
				}, contents: 1, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(-10.1327782f, -24.8829174f, -2.745f, 1f)), name: "Water 959436 #141")
					.WithMesh(new float3[] { new float3(-4.202222f, 9.785418f, -1.22000015f), new float3(-4.202222f, 9.785418f, 1.21999991f), new float3(5.252778f, 9.785418f, 1.21999991f), new float3(5.252778f, 9.785418f, -1.22000015f), new float3(-4.202222f, -6.22708273f, 1.21999991f), new float3(-4.202222f, -6.22708273f, -1.22000015f), new float3(5.252778f, -6.22708273f, -1.22000015f), new float3(5.252778f, -6.22708273f, 1.21999991f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 1, 4, 7, 2, -1, 5, 0, 3, 6, -1, 6, 3, 2, 7, -1, 0, 5, 4, 1, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, -1f, 0f, -6.04916763f),
					new float4(0f, 1f, 0f, -9.505833f),
					new float4(0.194778189f, 0f, 0.9808473f, -1.09691429f),
					new float4(0.194778189f, 0f, -0.9808473f, -1.37114275f),
					new float4(1f, 0f, 0f, -1.34390616f),
					new float4(-1f, 0f, 0f, -1.34390628f),
				}, contents: 1, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(-3.53609371f, -24.6033325f, -2.6052084f, 1f)), name: "Water 2747524 #357")
					.WithMesh(new float3[] { new float3(-1.34390628f, -6.04916763f, 1.38520837f), new float3(-1.34390628f, -6.04916763f, -1.66479158f), new float3(1.34390616f, -6.04916763f, -1.13104165f), new float3(1.34390616f, -6.04916763f, 0.85145843f), new float3(-1.34390628f, 9.505833f, -1.66479158f), new float3(-1.34390628f, 9.505833f, 1.38520837f), new float3(1.34390616f, 9.505833f, 0.85145843f), new float3(1.34390616f, 9.505833f, -1.13104165f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 5, 0, 3, 6, -1, 1, 4, 7, 2, -1, 2, 7, 6, 3, -1, 4, 1, 0, 5, -1 }))
				;
		}

		// bm_c3a2f, Model: the mesh draws nothing where the exact CSG has a face; Water 3122179 #291 face 4 near (-58.102,
		// -15.75, -2.745) (3 brushes)
		static ContentsScene BmC3a2fModel0Site11()
		{
			return new ContentsScene()
				.WithContentsList("Solid", "Water", "Glass", "Grate")
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(1f, 0f, 0f, -0.118610993f),
					new float4(-1f, 0f, 0f, -0.186389014f),
					new float4(0f, 0f, -1f, -3.66f),
					new float4(0f, 0f, 1f, -3.65999985f),
					new float4(0f, 1f, 0f, -5.422222f),
					new float4(0f, -1f, 0f, -4.33777761f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(-57.91611f, -11.3697224f, -2.745f, 1f)), name: "Solid 1561730 #117")
					.WithMesh(new float3[] { new float3(0.118610993f, -4.33777761f, 3.65999985f), new float3(0.118610993f, -4.33777761f, -3.66f), new float3(0.118610993f, 5.422222f, -3.66f), new float3(0.118610993f, 5.422222f, 3.65999985f), new float3(-0.186389014f, -4.33777761f, -3.66f), new float3(-0.186389014f, -4.33777761f, 3.65999985f), new float3(-0.186389014f, 5.422222f, 3.65999985f), new float3(-0.186389014f, 5.422222f, -3.66f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 1, 4, 7, 2, -1, 5, 0, 3, 6, -1, 6, 3, 2, 7, -1, 0, 5, 4, 1, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 1f, 0f, -9.785418f),
					new float4(0f, -1f, 0f, -6.22708273f),
					new float4(0f, 0f, 1f, -1.21999991f),
					new float4(0f, 0f, -1f, -1.22000015f),
					new float4(1f, 0f, 0f, -20.0791664f),
					new float4(-1f, 0f, 0f, -16.0633354f),
				}, contents: 1, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(-42.0391655f, -24.8829174f, -2.745f, 1f)), name: "Water 3122176 #290")
					.WithMesh(new float3[] { new float3(-16.0633354f, 9.785418f, -1.22000015f), new float3(-16.0633354f, 9.785418f, 1.21999991f), new float3(20.0791664f, 9.785418f, 1.21999991f), new float3(20.0791664f, 9.785418f, -1.22000015f), new float3(-16.0633354f, -6.22708273f, 1.21999991f), new float3(-16.0633354f, -6.22708273f, -1.22000015f), new float3(20.0791664f, -6.22708273f, -1.22000015f), new float3(20.0791664f, -6.22708273f, 1.21999991f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 1, 4, 7, 2, -1, 5, 0, 3, 6, -1, 6, 3, 2, 7, -1, 0, 5, 4, 1, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 1f, 0f, -9.785418f),
					new float4(0f, -1f, 0f, -6.22708273f),
					new float4(0f, 0f, 1f, -3.65999985f),
					new float4(0f, 0f, -1f, -3.66f),
					new float4(1f, 0f, 0f, -6.4388895f),
					new float4(-1f, 0f, 0f, -5.15111065f),
				}, contents: 1, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(-64.54139f, -24.8829174f, -2.745f, 1f)), name: "Water 3122179 #291")
					.WithMesh(new float3[] { new float3(-5.15111065f, 9.785418f, -3.66f), new float3(-5.15111065f, 9.785418f, 3.65999985f), new float3(6.4388895f, 9.785418f, 3.65999985f), new float3(6.4388895f, 9.785418f, -3.66f), new float3(-5.15111065f, -6.22708273f, 3.65999985f), new float3(-5.15111065f, -6.22708273f, -3.66f), new float3(6.4388895f, -6.22708273f, -3.66f), new float3(6.4388895f, -6.22708273f, 3.65999985f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 1, 4, 7, 2, -1, 5, 0, 3, 6, -1, 6, 3, 2, 7, -1, 0, 5, 4, 1, -1 }))
				;
		}

		// bm_c3a2f, Model: the mesh draws nothing where the exact CSG has a face; Water 3122179 #291 face 4 near (-58.102,
		// -20.125, -2.745) (3 brushes)
		static ContentsScene BmC3a2fModel0Site12()
		{
			return new ContentsScene()
				.WithContentsList("Solid", "Water", "Glass", "Grate")
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(1f, 0f, 0f, -0.118610993f),
					new float4(-1f, 0f, 0f, -0.186389014f),
					new float4(0f, 0f, -1f, -3.66f),
					new float4(0f, 0f, 1f, -3.65999985f),
					new float4(0f, 1f, 0f, -0.5083338f),
					new float4(0f, -1f, 0f, -0.4066662f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(-57.91611f, -20.1808338f, -2.745f, 1f)), name: "Solid 1561734 #118")
					.WithMesh(new float3[] { new float3(0.118610993f, -0.4066662f, 3.65999985f), new float3(0.118610993f, -0.4066662f, -3.66f), new float3(0.118610993f, 0.5083338f, -3.66f), new float3(0.118610993f, 0.5083338f, 3.65999985f), new float3(-0.186389014f, -0.4066662f, -3.66f), new float3(-0.186389014f, -0.4066662f, 3.65999985f), new float3(-0.186389014f, 0.5083338f, 3.65999985f), new float3(-0.186389014f, 0.5083338f, -3.66f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 1, 4, 7, 2, -1, 5, 0, 3, 6, -1, 6, 3, 2, 7, -1, 0, 5, 4, 1, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 1f, 0f, -9.785418f),
					new float4(0f, -1f, 0f, -6.22708273f),
					new float4(0f, 0f, 1f, -1.21999991f),
					new float4(0f, 0f, -1f, -1.22000015f),
					new float4(1f, 0f, 0f, -20.0791664f),
					new float4(-1f, 0f, 0f, -16.0633354f),
				}, contents: 1, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(-42.0391655f, -24.8829174f, -2.745f, 1f)), name: "Water 3122176 #290")
					.WithMesh(new float3[] { new float3(-16.0633354f, 9.785418f, -1.22000015f), new float3(-16.0633354f, 9.785418f, 1.21999991f), new float3(20.0791664f, 9.785418f, 1.21999991f), new float3(20.0791664f, 9.785418f, -1.22000015f), new float3(-16.0633354f, -6.22708273f, 1.21999991f), new float3(-16.0633354f, -6.22708273f, -1.22000015f), new float3(20.0791664f, -6.22708273f, -1.22000015f), new float3(20.0791664f, -6.22708273f, 1.21999991f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 1, 4, 7, 2, -1, 5, 0, 3, 6, -1, 6, 3, 2, 7, -1, 0, 5, 4, 1, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 1f, 0f, -9.785418f),
					new float4(0f, -1f, 0f, -6.22708273f),
					new float4(0f, 0f, 1f, -3.65999985f),
					new float4(0f, 0f, -1f, -3.66f),
					new float4(1f, 0f, 0f, -6.4388895f),
					new float4(-1f, 0f, 0f, -5.15111065f),
				}, contents: 1, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(-64.54139f, -24.8829174f, -2.745f, 1f)), name: "Water 3122179 #291")
					.WithMesh(new float3[] { new float3(-5.15111065f, 9.785418f, -3.66f), new float3(-5.15111065f, 9.785418f, 3.65999985f), new float3(6.4388895f, 9.785418f, 3.65999985f), new float3(6.4388895f, 9.785418f, -3.66f), new float3(-5.15111065f, -6.22708273f, 3.65999985f), new float3(-5.15111065f, -6.22708273f, -3.66f), new float3(6.4388895f, -6.22708273f, -3.66f), new float3(6.4388895f, -6.22708273f, 3.65999985f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 1, 4, 7, 2, -1, 5, 0, 3, 6, -1, 6, 3, 2, 7, -1, 0, 5, 4, 1, -1 }))
				;
		}

		// bm_c3a2g, Details: the mesh has a triangle the exact CSG did not make; Solid 121827 #3540 face -1 near (28.12,
		// -8.233, 34.397) (4 brushes)
		static ContentsScene BmC3a2gDetails29Site1()
		{
			return new ContentsScene()
				.WithContentsList("Solid", "Water", "Glass", "Grate")
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(-0.7071908f, 0.000516672444f, -0.707022548f, -0.2790491f),
					new float4(0.500097632f, 0.000249275559f, 0.865969f, -0.282549649f),
					new float4(0.563529968f, 0.703886151f, -0.432409853f, -0.227348611f),
					new float4(-0.29685235f, -0.927367449f, 0.2277461f, -0.183042958f),
					new float4(-0.66185236f, 0.5514669f, 0.5077753f, -0.147379175f),
					new float4(0.674517453f, -0.5264136f, -0.517604947f, -0.147451758f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(27.7832012f, -8.135451f, 34.0689125f, 1f)), name: "Solid 121823 #3536")
					.WithMesh(new float3[] { new float3(-0.007428093f, 0.09108247f, -0.3871856f), new float3(-0.142185912f, -0.213904038f, -0.2526186f), new float3(-0.3442823f, -0.0995486751f, -0.0503905527f), new float3(-0.155475f, 0.300672174f, -0.238950312f), new float3(0.209316775f, -0.213923678f, 0.205462486f), new float3(0.374204963f, 0.09107093f, 0.110151663f), new float3(0.192734912f, 0.3006984f, 0.214890391f), new float3(-0.0383049026f, -0.0995486751f, 0.34843123f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 5, 0, 3, 6, -1, 1, 4, 7, 2, -1, 2, 7, 6, 3, -1, 4, 1, 0, 5, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(-0.4998677f, -6.549373E-05f, -0.866101742f, -0.313306421f),
					new float4(0.2584574f, 0.000357480982f, 0.9660226f, -0.321757376f),
					new float4(0.8587053f, 0.368838757f, -0.3557853f, -0.227459744f),
					new float4(-0.6560023f, -0.704114258f, 0.271816283f, -0.181957111f),
					new float4(-0.515439f, 0.829884946f, 0.213573486f, -0.147362739f),
					new float4(0.4925747f, -0.8460072f, -0.204063684f, -0.147276253f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(28.3234863f, -7.8378644f, 34.44505f, 1f)), name: "Solid 121826 #3539")
					.WithMesh(new float3[] { new float3(0.09832258f, -0.0158943385f, -0.418488562f), new float3(-0.165907413f, -0.206525743f, -0.265974671f), new float3(-0.3475565f, 0.00317689357f, -0.1611524f), new float3(-0.0008045223f, 0.2700519f, -0.3612993f), new float3(0.07410981f, -0.206510589f, 0.313322872f), new float3(0.368826658f, -0.0158797614f, 0.23440142f), new float3(0.2582301f, 0.270044237f, 0.2638855f), new float3(-0.128524676f, 0.00317690056f, 0.3674597f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 5, 0, 3, 6, -1, 1, 4, 7, 2, -1, 2, 7, 6, 3, -1, 4, 1, 0, 5, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(-0.499912828f, -6.977279E-05f, -0.8660757f, -0.2790486f),
					new float4(0.258770734f, 0.000289746473f, 0.965938747f, -0.282588422f),
					new float4(0.655941248f, 0.7041809f, -0.271790981f, -0.227440521f),
					new float4(-0.3456657f, -0.9273702f, 0.143177316f, -0.183052689f),
					new float4(-0.7708069f, 0.551290154f, 0.319273978f, -0.1474498f),
					new float4(0.7855011f, -0.5264202f, -0.325376451f, -0.1474546f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(28.0644264f, -8.135451f, 34.55505f, 1f)), name: "Solid 121827 #3540")
					.WithMesh(new float3[] { new float3(0.09309358f, 0.09116854f, -0.3759413f), new float3(-0.07190667f, -0.213920444f, -0.280675918f), new float3(-0.319545746f, -0.09954869f, -0.13774389f), new float3(-0.08849271f, 0.300763816f, -0.271143675f), new float3(0.149027616f, -0.2139237f, 0.252693474f), new float3(0.3330595f, 0.0912099853f, 0.20330061f), new float3(0.130506471f, 0.300806731f, 0.2575008f), new float3(-0.1271878f, -0.09954869f, 0.326656073f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 5, 0, 3, 6, -1, 1, 4, 7, 2, -1, 2, 7, 6, 3, -1, 4, 1, 0, 5, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(-0.499693274f, 8.423494E-05f, -0.8662024f, -0.2623074f),
					new float4(0.259413123f, -0.00104597316f, 0.9657659f, -0.26070562f),
					new float4(0.345481277f, 0.9274472f, -0.143123537f, -0.2239545f),
					new float4(0f, -1f, 0f, -0.177916408f),
					new float4(-0.904769063f, 0.202275768f, 0.374803245f, -0.149119079f),
					new float4(0.902284443f, -0.2151628f, -0.3736144f, -0.1490234f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(27.9287128f, -8.514584f, 34.6141f, 1f)), name: "Solid 121828 #3541")
					.WithMesh(new float3[] { new float3(0.0639145449f, 0.165246174f, -0.33967945f), new float3(-0.00215034932f, -0.177916408f, -0.3016014f), new float3(-0.26638484f, -0.177916408f, -0.1491703f), new float3(-0.183816522f, 0.279583573f, -0.196757674f), new float3(0.210969433f, -0.177916408f, 0.2130861f), new float3(0.284757018f, 0.165282086f, 0.1936378f), new float3(0.008677815f, 0.2795868f, 0.267918885f), new float3(-0.08354759f, -0.177916408f, 0.292195946f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 5, 0, 3, 6, -1, 1, 4, 7, 2, -1, 2, 7, 6, 3, -1, 4, 1, 0, 5, -1 }))
				;
		}

		// bm_c3a2g, Details: the mesh has a triangle the exact CSG did not make; Solid 3115790 #3583 face -1 near (29.207,
		// -10.523, 37.728) (4 brushes)
		static ContentsScene BmC3a2gDetails29Site2()
		{
			return new ContentsScene()
				.WithContentsList("Solid", "Water", "Glass", "Grate")
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, -1f, 0f, -0.0593051538f),
					new float4(0f, 1f, 0f, -0.09319485f),
					new float4(0.4472136f, 0f, -0.8944272f, -0.489714026f),
					new float4(-0.707106769f, 0f, 0.707106769f, -0.4949875f),
					new float4(0.795431733f, 0f, 0.6060432f, -0.0264747068f),
					new float4(-0.7883158f, 0f, -0.615270853f, -0.0211768616f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(29.0872574f, -10.4631948f, 37.84224f, 1f)), name: "Solid 3115790 #3583")
					.WithMesh(new float3[] { new float3(-0.3219449f, -0.0593051538f, 0.378073126f), new float3(0.288055122f, -0.0593051538f, -0.403489381f), new float3(0.3261801f, -0.0593051538f, -0.3844269f), new float3(-0.283819884f, -0.0593051538f, 0.416198134f), new float3(0.288055122f, 0.09319485f, -0.403489381f), new float3(-0.3219449f, 0.09319485f, 0.378073126f), new float3(-0.283819884f, 0.09319485f, 0.416198134f), new float3(0.3261801f, 0.09319485f, -0.3844269f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 1, 4, 7, 2, -1, 5, 0, 3, 6, -1, 6, 3, 2, 7, -1, 0, 5, 4, 1, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 1f, 0f, -0.09319485f),
					new float4(0f, -1f, 0f, -0.0593051538f),
					new float4(-0.7071902f, 0f, 0.7070234f, -0.0170152653f),
					new float4(0.707190156f, 0f, -0.7070234f, -0.0212668963f),
					new float4(0.707167566f, 0f, 0.707046f, -0.571887f),
					new float4(-0.7096033f, 0f, -0.7046014f, -0.5718813f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(29.2031784f, -10.4631948f, 38.6589928f, 1f)), name: "Solid 3115817 #3610")
					.WithMesh(new float3[] { new float3(-0.389351368f, 0.09319485f, -0.4195227f), new float3(-0.416325182f, 0.09319485f, -0.392357379f), new float3(0.392306924f, 0.09319485f, 0.416465551f), new float3(0.4193742f, 0.09319485f, 0.389393628f), new float3(-0.416325182f, -0.0593051538f, -0.392357379f), new float3(-0.389351368f, -0.0593051538f, -0.4195227f), new float3(0.4193742f, -0.0593051538f, 0.389393628f), new float3(0.392306924f, -0.0593051538f, 0.416465551f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 1, 4, 7, 2, -1, 5, 0, 3, 6, -1, 6, 3, 2, 7, -1, 0, 5, 4, 1, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 1f, 0f, -0.09319485f),
					new float4(0f, -1f, 0f, -0.0593051538f),
					new float4(-0.500010431f, 0f, 0.8660194f, -0.0190471113f),
					new float4(0.500010431f, 0f, -0.8660194f, -0.01904371f),
					new float4(0.8657663f, 0f, 0.5004486f, -0.6354038f),
					new float4(-0.8657663f, 0f, -0.5004486f, -0.508322239f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(29.85251f, -10.4631948f, 37.7097855f, 1f)), name: "Solid 3115821 #3614")
					.WithMesh(new float3[] { new float3(-0.430686563f, 0.09319485f, -0.270653844f), new float3(-0.449749053f, 0.09319485f, -0.237676084f), new float3(0.540739954f, 0.09319485f, 0.3341989f), new float3(0.5598025f, 0.09319485f, 0.301221162f), new float3(-0.449749053f, -0.0593051538f, -0.237676084f), new float3(-0.430686563f, -0.0593051538f, -0.270653844f), new float3(0.5598025f, -0.0593051538f, 0.301221162f), new float3(0.540739954f, -0.0593051538f, 0.3341989f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 1, 4, 7, 2, -1, 5, 0, 3, 6, -1, 6, 3, 2, 7, -1, 0, 5, 4, 1, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, -1f, 0f, -1.83847189f),
					new float4(0f, 1f, 0f, -2.889028f),
					new float4(0.4472136f, 0f, -0.8944272f, -0.489714026f),
					new float4(-0.707106769f, 0f, 0.707106769f, -0.4949875f),
					new float4(0.795431733f, 0f, 0.6060432f, -0.0264747068f),
					new float4(-0.7883158f, 0f, -0.615270853f, -0.0211768616f),
				}, contents: 2, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(29.0872574f, -13.3924656f, 37.84224f, 1f)), name: "Glass 1280960 #3647")
					.WithMesh(new float3[] { new float3(-0.3219449f, -1.83847189f, 0.378073126f), new float3(0.288055122f, -1.83847189f, -0.403489381f), new float3(0.3261801f, -1.83847189f, -0.3844269f), new float3(-0.283819884f, -1.83847189f, 0.416198134f), new float3(0.288055122f, 2.889028f, -0.403489381f), new float3(-0.3219449f, 2.889028f, 0.378073126f), new float3(-0.283819884f, 2.889028f, 0.416198134f), new float3(0.3261801f, 2.889028f, -0.3844269f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 1, 4, 7, 2, -1, 5, 0, 3, 6, -1, 6, 3, 2, 7, -1, 0, 5, 4, 1, -1 }))
				;
		}

		// bm_c3a2h, Details: the mesh has a triangle the exact CSG did not make; Glass 806980 #1001 face -1 near (35.769,
		// 5.204, -38.123) (4 brushes)
		static ContentsScene BmC3a2hDetails29Site1()
		{
			return new ContentsScene()
				.WithContentsList("Solid", "Water", "Glass", "Grate")
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 1f, 0f, -1.77069438f),
					new float4(0f, -1f, 0f, -1.12680554f),
					new float4(0.7070762f, 0f, -0.7071374f, -0.5443382f),
					new float4(0.707106769f, 0f, 0.707106769f, -0.0211182013f),
					new float4(-0.7071058f, 0f, -0.7071077f, -0.01689322f),
					new float4(-0.9252408f, 0f, 0.379380345f, -0.628709435f),
				}, contents: 2, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(36.2489624f, 4.25305557f, -38.6090279f, 1f)), name: "Glass 807004 #997")
					.WithMesh(new float3[] { new float3(-0.488857269f, 1.77069438f, 0.464965373f), new float3(-0.473224729f, 1.77069438f, 0.5030904f), new float3(0.399838716f, 1.77069438f, -0.369973063f), new float3(0.372959942f, 1.77069438f, -0.3968495f), new float3(-0.473224729f, -1.12680554f, 0.5030904f), new float3(-0.488857269f, -1.12680554f, 0.464965373f), new float3(0.372959942f, -1.12680554f, -0.3968495f), new float3(0.399838716f, -1.12680554f, -0.369973063f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 6, 3, 2, 7, -1, 1, 4, 7, 2, -1, 5, 0, 3, 6, -1, 0, 5, 4, 1, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 1f, 0f, -1.77069438f),
					new float4(0f, -1f, 0f, -1.12680554f),
					new float4(-1f, 0f, 0f, -0.5451453f),
					new float4(0f, 0f, 1f, -0.0190625f),
					new float4(0f, 0f, -1f, -0.0190625f),
					new float4(0.9236297f, 0f, -0.383286029f, -0.6293907f),
				}, contents: 2, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(35.0863953f, 4.25305557f, -38.125f, 1f)), name: "Glass 806980 #1001")
					.WithMesh(new float3[] { new float3(0.6893424f, 1.77069438f, 0.0190625f), new float3(0.67352134f, 1.77069438f, -0.0190625f), new float3(-0.5451453f, 1.77069438f, -0.0190625f), new float3(-0.5451453f, 1.77069438f, 0.0190625f), new float3(0.67352134f, -1.12680554f, -0.0190625f), new float3(0.6893424f, -1.12680554f, 0.0190625f), new float3(-0.5451453f, -1.12680554f, 0.0190625f), new float3(-0.5451453f, -1.12680554f, -0.0190625f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 2, 7, 6, 3, -1, 5, 0, 3, 6, -1, 1, 4, 7, 2, -1, 4, 1, 0, 5, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 1f, 0f, -0.0232987776f),
					new float4(0f, -1f, 0f, -0.0148262214f),
					new float4(-1f, 0f, 0f, -0.545187235f),
					new float4(0f, 0f, 1f, -0.038125f),
					new float4(0f, 0f, -1f, -0.038125f),
					new float4(0.923619747f, 0f, -0.38331008f, -0.629433334f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(35.0864372f, 5.18076372f, -38.125f, 1f)), name: "Solid 806982 #1002")
					.WithMesh(new float3[] { new float3(0.6973076f, 0.0232987776f, 0.038125f), new float3(0.6656632f, 0.0232987776f, -0.038125f), new float3(-0.545187235f, 0.0232987776f, -0.038125f), new float3(-0.545187235f, 0.0232987776f, 0.038125f), new float3(0.6656632f, -0.0148262214f, -0.038125f), new float3(0.6973076f, -0.0148262214f, 0.038125f), new float3(-0.545187235f, -0.0148262214f, 0.038125f), new float3(-0.545187235f, -0.0148262214f, -0.038125f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 2, 7, 6, 3, -1, 5, 0, 3, 6, -1, 1, 4, 7, 2, -1, 4, 1, 0, 5, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 1f, 0f, -0.038125135f),
					new float4(0.7070319f, 8.43783346E-05f, 0.707181633f, -0.0762553439f),
					new float4(-0.707106769f, 0f, -0.707106769f, -0.07625513f),
					new float4(0f, -1f, 0f, -0.0381248668f),
					new float4(0.498980463f, -0.707289636f, -0.5007593f, -0.4967924f),
					new float4(-0.9240285f, 0f, 0.382323563f, -0.519337535f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(36.1653862f, 6.061875f, -38.52252f, 1f)), name: "Solid 833320 #1004")
					.WithMesh(new float3[] { new float3(-0.429109216f, 0.038125135f, 0.321268171f), new float3(-0.366013885f, 0.038125135f, 0.473761767f), new float3(0.5779642f, 0.038125135f, -0.47001642f), new float3(0.469877779f, 0.038125135f, -0.577718854f), new float3(-0.3660112f, -0.0381248668f, 0.4737682f), new float3(0.5240181f, -0.0381248668f, -0.4160727f), new float3(-0.429109216f, -0.0381248668f, 0.321268171f), new float3(0.415932924f, -0.0381248668f, -0.523773968f) }, new int[] { 0, 1, 2, 3, -1, 1, 4, 5, 2, -1, 6, 0, 3, 7, -1, 4, 6, 7, 5, -1, 5, 7, 3, 2, -1, 6, 4, 1, 0, -1 }))
				;
		}

		// bm_c3a2h, Details: the exact CSG made a triangle the mesh does not have; Solid 812220 #1005 face -1 near (32.426,
		// 1.017, -37.82) (4 brushes)
		static ContentsScene BmC3a2hDetails29Site2()
		{
			return new ContentsScene()
				.WithContentsList("Solid", "Water", "Glass", "Grate")
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 1f, 0f, -0.2614286f),
					new float4(0f, -1f, 0f, -0.34857142f),
					new float4(0f, 0f, -1f, -0.290478051f),
					new float4(0f, 0f, 1f, -0.319521934f),
					new float4(0f, 0.707106769f, 0.707106769f, -0.195126489f),
					new float4(0.707106769f, 0f, 0.707106769f, -1.12968516f),
					new float4(-0.707106769f, 0f, 0.707106769f, -1.04752767f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(34.1019058f, 0.34857142f, -37.5295219f, 1f)), name: "Solid 814317 #939")
					.WithMesh(new float3[] { new float3(-1.7719059f, 0.2614286f, -0.290478051f), new float3(-1.466906f, 0.2614286f, 0.0145219369f), new float3(1.58309412f, 0.2614286f, 0.0145219369f), new float3(1.88809419f, 0.2614286f, -0.290478051f), new float3(-1.16190588f, -0.34857142f, 0.319521934f), new float3(-1.7719059f, -0.34857142f, -0.290478051f), new float3(1.88809419f, -0.34857142f, -0.290478051f), new float3(1.27809417f, -0.34857142f, 0.319521934f), new float3(-1.16190588f, -0.0435714f, 0.319521934f), new float3(1.27809417f, -0.0435714f, 0.319521934f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 5, 0, 3, 6, -1, 8, 4, 7, 9, -1, 1, 8, 9, 2, -1, 2, 9, 7, 6, 3, -1, 8, 1, 0, 5, 4, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 1f, 0f, -1.49111116f),
					new float4(0f, -1f, 0f, -0.9488889f),
					new float4(0f, 0f, 1f, -1.10033f),
					new float4(-0.707106769f, 0f, 0.707106769f, -0.0299543384f),
					new float4(0.707106769f, 0f, -0.707106769f, -0.0374417752f),
					new float4(-1f, 0f, 0f, -1.3820318f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(31.2720318f, 0.9488889f, -38.92033f, 1f)), name: "Solid 830447 #965")
					.WithMesh(new float3[] { new float3(-1.3820318f, 1.49111116f, -1.43498242f), new float3(-1.3820318f, 1.49111116f, -1.33967f), new float3(1.05796814f, 1.49111116f, 1.10033f), new float3(1.15328062f, 1.49111116f, 1.10033f), new float3(-1.3820318f, -0.9488889f, -1.33967f), new float3(-1.3820318f, -0.9488889f, -1.43498242f), new float3(1.15328062f, -0.9488889f, 1.10033f), new float3(1.05796814f, -0.9488889f, 1.10033f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 6, 3, 2, 7, -1, 1, 4, 7, 2, -1, 5, 0, 3, 6, -1, 0, 5, 4, 1, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, -1f, 0f, -0.9488889f),
					new float4(0f, 0f, -1f, -0.305f),
					new float4(0f, 0f, 1f, -0.305f),
					new float4(-0.923928857f, 0f, -0.3825643f, -1.32049918f),
					new float4(0.9238267f, 0f, -0.38281098f, -1.6504401f),
					new float4(0f, 1f, 0f, -1.49111116f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(33.9813957f, 0.9488889f, -38.125f, 1f)), name: "Solid 812220 #1005")
					.WithMesh(new float3[] { new float3(1.66014123f, -0.9488889f, -0.305f), new float3(1.9129101f, -0.9488889f, 0.305f), new float3(-1.55551076f, -0.9488889f, 0.305f), new float3(-1.30293262f, -0.9488889f, -0.305f), new float3(-1.30293262f, 1.49111116f, -0.305f), new float3(1.66014123f, 1.49111116f, -0.305f), new float3(1.9129101f, 1.49111116f, 0.305f), new float3(-1.55551076f, 1.49111116f, 0.305f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 0, 3, -1, 6, 7, 2, 1, -1, 7, 4, 3, 2, -1, 5, 6, 1, 0, -1, 6, 5, 4, 7, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, -1f, 0f, -0.9488889f),
					new float4(0.707013845f, -0.000110607856f, -0.7071997f, -0.304929584f),
					new float4(-0.7070536f, 0f, 0.70716f, -0.305032343f),
					new float4(-0.38281098f, 0f, -0.9238267f, -1.36881781f),
					new float4(0.9239712f, 0f, 0.382462174f, -1.71089959f),
					new float4(0f, 1f, 0f, -1.49111116f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(31.2427063f, 0.9488889f, -39.43454f, 1f)), name: "Solid 812224 #1006")
					.WithMesh(new float3[] { new float3(1.43589163f, -0.9488889f, 1.00448382f), new float3(1.1833694f, -0.9488889f, 1.61453974f), new float3(-1.35270631f, -0.9488889f, -0.9211544f), new float3(-0.7429762f, -0.9488889f, -1.17381144f), new float3(-0.742706358f, 1.49111116f, -1.17392325f), new float3(1.43600345f, 1.49111116f, 1.00421393f), new float3(1.1833694f, 1.49111116f, 1.61453974f), new float3(-1.35270631f, 1.49111116f, -0.9211544f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 0, 3, -1, 6, 7, 2, 1, -1, 7, 4, 3, 2, -1, 5, 6, 1, 0, -1, 6, 5, 4, 7, -1 }))
				;
		}

		// bm_c3a2h, Details: the mesh has a triangle the exact CSG did not make; Glass 4010491 #2408 face -1 near (19.883,
		// 3.488, -19.344) (3 brushes)
		static ContentsScene BmC3a2hDetails29Site3()
		{
			return new ContentsScene()
				.WithContentsList("Solid", "Water", "Glass", "Grate")
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 1f, 0f, -0.02329855f),
					new float4(0f, -1f, 0f, -0.0148264505f),
					new float4(-0.707114458f, 0f, -0.70709914f, -0.44221136f),
					new float4(-0.7072128f, 0f, 0.707000732f, -0.03804417f),
					new float4(0.7071167f, 0f, -0.7070969f, -0.03803813f),
					new float4(0.6700765f, 0f, 0.742292047f, -0.55204463f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(20.2080078f, 3.503264f, -19.04354f, 1f)), name: "Solid 919158 #1102")
					.WithMesh(new float3[] { new float3(0.362526268f, 0.02329855f, 0.416445673f), new float3(0.419130921f, 0.02329855f, 0.3653479f), new float3(-0.28578952f, 0.02329855f, -0.339592338f), new float3(-0.339544743f, 0.02329855f, -0.285835952f), new float3(0.419130921f, -0.0148264505f, 0.3653479f), new float3(0.362526268f, -0.0148264505f, 0.416445673f), new float3(-0.339544743f, -0.0148264505f, -0.285835952f), new float3(-0.28578952f, -0.0148264505f, -0.339592338f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 2, 7, 6, 3, -1, 5, 0, 3, 6, -1, 1, 4, 7, 2, -1, 4, 1, 0, 5, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(-0.7064883f, 0f, -0.7077247f, -0.0339080654f),
					new float4(-0.707106769f, 0f, 0.707106769f, -0.07622415f),
					new float4(0.70709914f, 0f, -0.7071144f, -0.07622538f),
					new float4(0.706476867f, 0f, 0.707736135f, -0.0423843563f),
					new float4(0.499427766f, -0.7072977f, 0.5003018f, -0.6621578f),
					new float4(0f, 1f, 0f, -1.46993053f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(19.86537f, 2.03756952f, -19.3861275f, 1f)), name: "Solid 919167 #1109")
					.WithMesh(new float3[] { new float3(-0.07792237f, 1.46993053f, 0.02987486f), new float3(0.0299697127f, 1.46993053f, -0.0778287351f), new float3(0.0299697127f, -0.9700695f, -0.0778287351f), new float3(-0.07792237f, -0.9700695f, 0.02987486f), new float3(-0.0239763279f, 1.46993053f, 0.0838208944f), new float3(-0.0239763279f, -0.8938195f, 0.0838208944f), new float3(0.08391808f, 1.46993053f, -0.0238815341f), new float3(0.08391808f, -0.893817067f, -0.0238815341f) }, new int[] { 0, 1, 2, 3, -1, 4, 0, 3, 5, -1, 1, 6, 7, 2, -1, 6, 4, 5, 7, -1, 7, 5, 3, 2, -1, 4, 6, 1, 0, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0.0153147839f, 0.999765456f, -0.0153143685f, -0.9110887f),
					new float4(0.0189350359f, -0.9996414f, -0.0189345218f, -1.43157f),
					new float4(-0.71104f, 0f, -0.7031515f, -0.538456559f),
					new float4(-0.707019031f, 0f, 0.7071945f, -0.0269209519f),
					new float4(0.707116365f, 0f, -0.7070972f, -0.0215289928f),
					new float4(0.6728724f, 0f, 0.739758551f, -0.4304308f),
				}, contents: 2, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(20.2687645f, 2.576553f, -18.9685745f, 1f)), name: "Glass 4010491 #2408")
					.WithMesh(new float3[] { new float3(0.2848037f, 0.911884367f, 0.322800279f), new float3(0.320641279f, 0.910836041f, 0.290202975f), new float3(-0.3656087f, 0.910836041f, -0.396065623f), new float3(-0.399728984f, 0.9118872f, -0.36156255f), new float3(0.320641279f, -1.43150687f, 0.290202975f), new float3(0.2848037f, -1.43280315f, 0.322800279f), new float3(-0.399728984f, -1.43280673f, -0.36156255f), new float3(-0.3656087f, -1.43150687f, -0.396065623f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 2, 7, 6, 3, -1, 5, 0, 3, 6, -1, 1, 4, 7, 2, -1, 4, 1, 0, 5, -1 }))
				;
		}

		// bm_c3a2h, Details: the mesh has a triangle the exact CSG did not make; Solid 824101 #973 face -1 near (38.64,
		// 0.379, -32.387) (5 brushes)
		static ContentsScene BmC3a2hDetails29Site4()
		{
			return new ContentsScene()
				.WithContentsList("Solid", "Water", "Glass", "Grate")
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 1f, 0f, -0.152500018f),
					new float4(0.707106769f, 0f, 0.707106769f, -0.08072601f),
					new float4(-0.7071057f, 0.00176949054f, -0.7071057f, -0.08075469f),
					new float4(0f, -1f, 0f, -0.152499989f),
					new float4(0.8942024f, 0.00028076506f, -0.447662741f, -1.30026376f),
					new float4(-0.7043607f, 0.08804509f, 0.7043607f, -1.09202158f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(38.64295f, 0.7625f, -32.504612f, 1f)), name: "Solid 824138 #882")
					.WithMesh(new float3[] { new float3(-0.8225665f, 0.152500018f, 0.7087436f), new float3(-0.708573163f, 0.152500018f, 0.822737f), new float3(1.007052f, 0.152500018f, -0.892888069f), new float3(0.9309927f, 0.152500018f, -1.04481566f), new float3(-0.7276356f, -0.152499989f, 0.8417995f), new float3(1.00711572f, -0.152499989f, -0.8929519f), new float3(-0.8420107f, -0.152499989f, 0.7274245f), new float3(0.930801868f, -0.152499989f, -1.0453881f) }, new int[] { 0, 1, 2, 3, -1, 1, 4, 5, 2, -1, 6, 0, 3, 7, -1, 4, 6, 7, 5, -1, 5, 7, 3, 2, -1, 6, 4, 1, 0, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0.444444448f, 0.7777778f, -0.444444448f, -0.498650461f),
					new float4(0.707106769f, 0f, 0.707106769f, -0.184856057f),
					new float4(-0.707106769f, 0f, -0.707106769f, -0.1386453f),
					new float4(0f, -1f, 0f, -1.147381f),
					new float4(-0.707106769f, 0f, 0.707106769f, -1.12968779f),
					new float4(0f, 1f, 0f, -1.29261911f),
					new float4(0.707106769f, 0f, -0.707106769f, -1.08090484f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(37.2680969f, 1.147381f, -30.8195229f, 1f)), name: "Solid 824078 #886")
					.WithMesh(new float3[] { new float3(0.8950281f, -0.232381016f, -0.6336022f), new float3(0.6662781f, -0.232381016f, -0.8623522f), new float3(-0.668097f, 1.29261911f, 0.472022921f), new float3(-0.439347f, 1.29261911f, 0.700772941f), new float3(0.8950281f, -1.147381f, -0.6336022f), new float3(-0.66809696f, 1.29261911f, 0.9295229f), new float3(-0.66809696f, -1.147381f, 0.9295229f), new float3(0.6662781f, -1.147381f, -0.8623522f), new float3(-0.89684695f, -1.147381f, 0.7007729f), new float3(-0.89684695f, 1.29261911f, 0.7007729f) }, new int[] { 0, 1, 2, 3, -1, 4, 0, 3, 5, 6, -1, 1, 7, 8, 9, 2, -1, 7, 4, 6, 8, -1, 5, 9, 8, 6, -1, 9, 5, 3, 2, -1, 4, 7, 1, 0, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 1f, 0f, -0.3727778f),
					new float4(0f, -1f, 0f, -0.237222224f),
					new float4(1f, 0f, 0f, -1.42333162f),
					new float4(0f, 0f, 1f, -1.44874835f),
					new float4(0f, 0f, -1f, -1.44875169f),
					new float4(-1f, 0f, 0f, -1.77916837f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(41.12417f, 0.237222224f, -34.54125f, 1f)), name: "Solid 826784 #955")
					.WithMesh(new float3[] { new float3(-1.77916837f, 0.3727778f, -1.44875169f), new float3(-1.77916837f, 0.3727778f, 1.44874835f), new float3(1.42333162f, 0.3727778f, 1.44874835f), new float3(1.42333162f, 0.3727778f, -1.44875169f), new float3(-1.77916837f, -0.237222224f, 1.44874835f), new float3(-1.77916837f, -0.237222224f, -1.44875169f), new float3(1.42333162f, -0.237222224f, -1.44875169f), new float3(1.42333162f, -0.237222224f, 1.44874835f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 6, 3, 2, 7, -1, 1, 4, 7, 2, -1, 5, 0, 3, 6, -1, 0, 5, 4, 1, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(-0.316227764f, 0.9486833f, 0f, -0.192898616f),
					new float4(0f, -1f, 0f, -0.162666664f),
					new float4(1f, 0f, 0f, -0.732001066f),
					new float4(0.707106769f, 0f, 0.707106769f, -1.26525068f),
					new float4(-0.316227764f, 0f, -0.9486833f, -1.514256f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(38.613f, 0.162666664f, -34.1498337f, 1f)), name: "Solid 837136 #958")
					.WithMesh(new float3[] { new float3(0.732001066f, 0.447333336f, -1.84016633f), new float3(-1.097999f, -0.162666664f, -1.23016632f), new float3(-1.097999f, -0.162666664f, 2.88733363f), new float3(0.732001066f, 0.447333336f, 1.05733359f), new float3(0.732001066f, -0.162666664f, -1.84016633f), new float3(0.732001066f, -0.162666664f, 1.05733359f) }, new int[] { 0, 1, 2, 3, -1, 1, 4, 5, 2, -1, 4, 0, 3, 5, -1, 5, 3, 2, -1, 0, 4, 1, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0.707288265f, 0f, 0.7069253f, -0.161736712f),
					new float4(-0.707264066f, 0f, -0.7069495f, -0.1617308f),
					new float4(0f, -1f, 0f, -0.355833322f),
					new float4(0f, 1f, 0f, -0.55916667f),
					new float4(-0.707106769f, 0f, 0.707106769f, -0.9884772f),
					new float4(1f, 0f, 0f, -0.902079f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(38.74792f, 0.355833322f, -32.2662468f, 1f)), name: "Solid 824101 #973")
					.WithMesh(new float3[] { new float3(0.902079f, -0.355833322f, -0.6737532f), new float3(0.902079f, 0.55916667f, -0.6737532f), new float3(-0.584414363f, 0.55916667f, 0.813503444f), new float3(-0.584414363f, -0.355833322f, 0.813503444f), new float3(0.902079f, 0.55916667f, -1.13125324f), new float3(0.902079f, -0.355833322f, -1.13125324f), new float3(-0.813164353f, -0.355833322f, 0.584753454f), new float3(-0.813164353f, 0.55916667f, 0.584753454f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 5, 0, 3, 6, -1, 1, 4, 7, 2, -1, 2, 7, 6, 3, -1, 4, 1, 0, 5, -1 }))
				;
		}

		// bm_c3a2h, Details: the mesh draws nothing where the exact CSG has a face; Glass 653469 #753 face 0 near (41.221,
		// 5.185, -30.347) (3 brushes)
		static ContentsScene BmC3a2hDetails29Site5()
		{
			return new ContentsScene()
				.WithContentsList("Solid", "Water", "Glass", "Grate")
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0.7071144f, 0f, -0.70709914f, -0.0338711478f),
					new float4(-0.7058376f, 0f, -0.708373666f, -0.06771688f),
					new float4(0.707122f, 0f, 0.7070915f, -0.08465871f),
					new float4(-0.7077247f, 0f, 0.7064883f, -0.04234531f),
					new float4(-0.5003073f, -0.7066795f, 0.500296533f, -0.5777432f),
					new float4(0f, 1f, 0f, -1.28354168f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(41.25646f, 3.90145826f, -30.3710213f, 1f)), name: "Solid 653468 #752")
					.WithMesh(new float3[] { new float3(-0.0238891579f, 1.28354168f, -0.07179123f), new float3(0.08381211f, 1.28354168f, 0.03591236f), new float3(0.08381211f, -0.8514583f, 0.03591236f), new float3(-0.0238891579f, -0.8514583f, -0.07179123f), new float3(-0.07783753f, 1.28354168f, -0.0180360042f), new float3(-0.07783753f, -0.7752083f, -0.0180360042f), new float3(0.0298683941f, 1.28354168f, 0.0898584053f), new float3(0.0298683941f, -0.7750765f, 0.0898584053f) }, new int[] { 0, 1, 2, 3, -1, 4, 0, 3, 5, -1, 1, 6, 7, 2, -1, 6, 4, 5, 7, -1, 7, 5, 3, 2, -1, 4, 6, 1, 0, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 1f, 0f, -1.25812483f),
					new float4(0f, -1f, 0f, -0.8006251f),
					new float4(0.7096338f, 0f, -0.704570651f, -0.470602632f),
					new float4(-0.707106769f, 0f, -0.707106769f, -0.0169540122f),
					new float4(0.707107842f, 0f, 0.7071057f, -0.0211923216f),
					new float4(-0.923609734f, 0f, 0.38333413f, -0.5446386f),
				}, contents: 2, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(40.8982124f, 3.926875f, -30.0038452f, 1f)), name: "Glass 653469 #753")
					.WithMesh(new float3[] { new float3(-0.4237593f, 1.25812483f, 0.399782717f), new float3(-0.407935947f, 1.25812483f, 0.4379077f), new float3(0.347699523f, 1.25812483f, -0.317730069f), new float3(0.320823073f, 1.25812483f, -0.344799668f), new float3(-0.407935947f, -0.8006251f, 0.4379077f), new float3(-0.4237593f, -0.8006251f, 0.399782717f), new float3(0.320823073f, -0.8006251f, -0.344799668f), new float3(0.347699523f, -0.8006251f, -0.317730069f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 2, 7, 6, 3, -1, 5, 0, 3, 6, -1, 1, 4, 7, 2, -1, 4, 1, 0, 5, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(-0.7073938f, 0.00353458687f, -0.7068108f, -0.4942627f),
					new float4(0f, -1f, 0f, -0.4945354f),
					new float4(0.7145615f, 0.01037957f, -0.6994956f, -0.415472984f),
					new float4(-0.5001444f, -0.707174242f, -0.499760121f, -0.0104837362f),
					new float4(0.499967754f, 0.707112551f, 0.5000241f, -0.0164634474f),
					new float4(-0.922935069f, 0.002710189f, 0.384946048f, -0.4056939f),
				}, contents: 2, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(40.5860443f, 5.67953539f, -30.39397f, 1f)), name: "Glass 653470 #754")
					.WithMesh(new float3[] { new float3(-0.5141934f, 0.477652133f, -0.182279527f), new float3(-0.514058948f, 0.515608f, -0.182224318f), new float3(-0.0545898043f, 0.5159115f, -0.642070949f), new float3(-0.05440378f, 0.477670223f, -0.642448366f), new float3(-0.09569033f, -0.4945354f, 0.827955544f), new float3(-0.111400373f, -0.4945354f, 0.7902896f), new float3(0.6329912f, -0.4945354f, 0.04532564f), new float3(0.659720063f, -0.4945354f, 0.0726302341f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 2, 7, 6, 3, -1, 5, 0, 3, 6, -1, 1, 4, 7, 2, -1, 4, 1, 0, 5, -1 }))
				;
		}

		// bm_c3a2h, Details: the mesh has a triangle the exact CSG did not make; Solid 653382 #621 face -1 near (24.645,
		// 5.185, -30.332) (5 brushes)
		static ContentsScene BmC3a2hDetails29Site6()
		{
			return new ContentsScene()
				.WithContentsList("Solid", "Water", "Glass", "Grate")
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(-0.707106769f, -1.911856E-14f, -0.707106769f, -0.0296092872f),
					new float4(0.498496473f, -0.7071097f, -0.501494944f, -0.0479167774f),
					new float4(-0.5000267f, 0.7070538f, 0.5000483f, -0.0599112026f),
					new float4(0.707834f, -0.0008838351f, 0.7063782f, -0.0465433933f),
					new float4(0f, -1f, 0f, -0.5040974f),
					new float4(1f, 0f, 0f, -0.37139076f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(24.97735f, 5.6890974f, -30.7304764f, 1f)), name: "Solid 653379 #618")
					.WithMesh(new float3[] { new float3(0.37139076f, 0.63965255f, -0.4132646f), new float3(0.37139076f, 0.4871526f, -0.4132646f), new float3(-0.329537749f, -0.5040974f, 0.287663877f), new float3(-0.437241346f, -0.5040974f, 0.395367473f), new float3(0.37139076f, 0.4109026f, -0.30575186f), new float3(-0.2755257f, -0.5040974f, 0.341352969f), new float3(0.37139076f, 0.5634815f, -0.305560946f), new float3(-0.383278579f, -0.5040974f, 0.449327916f) }, new int[] { 0, 1, 2, 3, -1, 1, 4, 5, 2, -1, 6, 0, 3, 7, -1, 4, 6, 7, 5, -1, 5, 7, 3, 2, -1, 6, 4, 1, 0, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(-0.707106769f, 0f, -0.707106769f, -0.03387792f),
					new float4(0.7058529f, 0f, -0.7083584f, -0.0761880353f),
					new float4(-0.7070915f, 0f, 0.707122f, -0.0761870146f),
					new float4(0.707739949f, 0f, 0.706473053f, -0.0423466749f),
					new float4(0.499859482f, -0.707305431f, 0.499859482f, -0.5782925f),
					new float4(0f, 1f, 0f, -1.28354168f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(24.6179371f, 3.90145826f, -30.3650265f, 1f)), name: "Solid 653382 #621")
					.WithMesh(new float3[] { new float3(-0.0778281838f, 1.28354168f, 0.0299175642f), new float3(0.0298754126f, 1.28354168f, -0.07778603f), new float3(0.0298754126f, -0.851458251f, -0.07778603f), new float3(-0.0778281838f, -0.851458251f, 0.0299175642f), new float3(0.0838214457f, 1.28354168f, -0.0240308f), new float3(0.0838214457f, -0.7753448f, -0.0240308f), new float3(-0.0238798149f, 1.28354168f, 0.0838636f), new float3(-0.0238798149f, -0.7752083f, 0.0838636f) }, new int[] { 0, 1, 2, 3, -1, 1, 4, 5, 2, -1, 6, 0, 3, 7, -1, 4, 6, 7, 5, -1, 5, 7, 3, 2, -1, 6, 4, 1, 0, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 1f, 0f, -1.25812483f),
					new float4(0f, -1f, 0f, -0.8006251f),
					new float4(-0.7096338f, 0f, -0.704570651f, -0.471469641f),
					new float4(0.707106769f, 0f, -0.707106769f, -0.01907352f),
					new float4(-0.707107842f, 0f, 0.7071057f, -0.0190728158f),
					new float4(0.923609734f, 0f, 0.38333413f, -0.544640064f),
				}, contents: 2, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(24.9812889f, 3.926875f, -30.0017281f, 1f)), name: "Glass 653383 #622")
					.WithMesh(new float3[] { new float3(0.408816248f, 1.25812483f, 0.435790569f), new float3(0.4246396f, 1.25812483f, 0.3976656f), new float3(-0.3199428f, 1.25812483f, -0.346916825f), new float3(-0.346819252f, 1.25812483f, -0.319847226f), new float3(0.4246396f, -0.8006251f, 0.3976656f), new float3(0.408816248f, -0.8006251f, 0.435790569f), new float3(-0.346819252f, -0.8006251f, -0.319847226f), new float3(-0.3199428f, -0.8006251f, -0.346916825f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 6, 3, 2, 7, -1, 1, 4, 7, 2, -1, 5, 0, 3, 6, -1, 0, 5, 4, 1, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0.7073823f, 0.00707485946f, -0.7067957f, -0.498002f),
					new float4(0f, -1f, 0f, -0.494552076f),
					new float4(-0.71223253f, 0.007025939f, -0.7019084f, -0.344119638f),
					new float4(0.499947578f, -0.7071809f, -0.499947578f, -0.01204961f),
					new float4(-0.5000282f, 0.7070909f, 0.499994278f, -0.0150197614f),
					new float4(0.9236094f, 0.0008511164f, 0.383333981f, -0.472219467f),
				}, contents: 2, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(25.242403f, 5.679552f, -30.4430351f, 1f)), name: "Glass 653384 #623")
					.WithMesh(new float3[] { new float3(0.565989554f, 0.515514433f, -0.132971659f), new float3(0.566126347f, 0.47736752f, -0.133216575f), new float3(0.106337965f, 0.4776354f, -0.5933839f), new float3(0.106337965f, 0.5157604f, -0.5930023f), new float3(0.163525477f, -0.494552076f, 0.8389726f), new float3(0.147687763f, -0.494552076f, 0.877132237f), new float3(-0.607833f, -0.494552076f, 0.121560186f), new float3(-0.581056952f, -0.494552076f, 0.09439025f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 6, 3, 2, 7, -1, 1, 4, 7, 2, -1, 5, 0, 3, 6, -1, 0, 5, 4, 1, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 1f, 0f, -0.0232987776f),
					new float4(0f, -1f, 0f, -0.0148262214f),
					new float4(-0.7070915f, 0f, -0.707122f, -0.469684154f),
					new float4(0.707106769f, 0f, -0.707106769f, -0.03388523f),
					new float4(-0.707018137f, 0f, 0.7071954f, -0.04234877f),
					new float4(0.9244366f, 0f, 0.381335735f, -0.5441311f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(24.9830379f, 5.18076372f, -30.00591f, 1f)), name: "Solid 653385 #624")
					.WithMesh(new float3[] { new float3(0.399253249f, 0.0232987776f, 0.459035873f), new float3(0.430706829f, 0.0232987776f, 0.3827859f), new float3(-0.308155864f, 0.0232987776f, -0.3560768f), new float3(-0.362104237f, 0.0232987776f, -0.302130759f), new float3(0.430706829f, -0.0148262214f, 0.3827859f), new float3(0.399253249f, -0.0148262214f, 0.459035873f), new float3(-0.362104237f, -0.0148262214f, -0.302130759f), new float3(-0.308155864f, -0.0148262214f, -0.3560768f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 6, 3, 2, 7, -1, 1, 4, 7, 2, -1, 5, 0, 3, 6, -1, 0, 5, 4, 1, -1 }))
				;
		}

		// bm_c3a2h, Details: the mesh has a triangle the exact CSG did not make; Solid 653468 #752 face -1 near (41.227,
		// 5.185, -30.34) (5 brushes)
		static ContentsScene BmC3a2hDetails29Site7()
		{
			return new ContentsScene()
				.WithContentsList("Solid", "Water", "Glass", "Grate")
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0.7071144f, 9.362504E-06f, -0.70709914f, -0.0296099223f),
					new float4(-0.499099821f, -0.7071107f, -0.500893056f, -0.04789744f),
					new float4(0.499509066f, 0.707053065f, 0.500566363f, -0.0598781556f),
					new float4(-0.7077245f, -0.0007268698f, 0.706488132f, -0.0465391651f),
					new float4(0f, -1f, 0f, -0.461736143f),
					new float4(-1f, 0f, 0f, -0.4013504f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(40.93299f, 5.646736f, -30.7005157f, 1f)), name: "Solid 653465 #749")
					.WithMesh(new float3[] { new float3(-0.4013504f, 0.5295153f, -0.443227261f), new float3(-0.4013504f, 0.6820138f, -0.443225235f), new float3(0.407279372f, -0.461736143f, 0.365406841f), new float3(0.2995781f, -0.461736143f, 0.257703245f), new float3(-0.4013504f, 0.453355432f, -0.3357124f), new float3(0.245629713f, -0.461736143f, 0.311458468f), new float3(-0.4013504f, 0.6057879f, -0.335555583f), new float3(0.3532775f, -0.461736143f, 0.419294655f) }, new int[] { 0, 1, 2, 3, -1, 4, 0, 3, 5, -1, 1, 6, 7, 2, -1, 6, 4, 5, 7, -1, 7, 5, 3, 2, -1, 4, 6, 1, 0, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0.7071144f, 0f, -0.70709914f, -0.0338711478f),
					new float4(-0.7058376f, 0f, -0.708373666f, -0.06771688f),
					new float4(0.707122f, 0f, 0.7070915f, -0.08465871f),
					new float4(-0.7077247f, 0f, 0.7064883f, -0.04234531f),
					new float4(-0.5003073f, -0.7066795f, 0.500296533f, -0.5777432f),
					new float4(0f, 1f, 0f, -1.28354168f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(41.25646f, 3.90145826f, -30.3710213f, 1f)), name: "Solid 653468 #752")
					.WithMesh(new float3[] { new float3(-0.0238891579f, 1.28354168f, -0.07179123f), new float3(0.08381211f, 1.28354168f, 0.03591236f), new float3(0.08381211f, -0.8514583f, 0.03591236f), new float3(-0.0238891579f, -0.8514583f, -0.07179123f), new float3(-0.07783753f, 1.28354168f, -0.0180360042f), new float3(-0.07783753f, -0.7752083f, -0.0180360042f), new float3(0.0298683941f, 1.28354168f, 0.0898584053f), new float3(0.0298683941f, -0.7750765f, 0.0898584053f) }, new int[] { 0, 1, 2, 3, -1, 4, 0, 3, 5, -1, 1, 6, 7, 2, -1, 6, 4, 5, 7, -1, 7, 5, 3, 2, -1, 4, 6, 1, 0, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 1f, 0f, -1.25812483f),
					new float4(0f, -1f, 0f, -0.8006251f),
					new float4(0.7096338f, 0f, -0.704570651f, -0.470602632f),
					new float4(-0.707106769f, 0f, -0.707106769f, -0.0169540122f),
					new float4(0.707107842f, 0f, 0.7071057f, -0.0211923216f),
					new float4(-0.923609734f, 0f, 0.38333413f, -0.5446386f),
				}, contents: 2, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(40.8982124f, 3.926875f, -30.0038452f, 1f)), name: "Glass 653469 #753")
					.WithMesh(new float3[] { new float3(-0.4237593f, 1.25812483f, 0.399782717f), new float3(-0.407935947f, 1.25812483f, 0.4379077f), new float3(0.347699523f, 1.25812483f, -0.317730069f), new float3(0.320823073f, 1.25812483f, -0.344799668f), new float3(-0.407935947f, -0.8006251f, 0.4379077f), new float3(-0.4237593f, -0.8006251f, 0.399782717f), new float3(0.320823073f, -0.8006251f, -0.344799668f), new float3(0.347699523f, -0.8006251f, -0.317730069f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 2, 7, 6, 3, -1, 5, 0, 3, 6, -1, 1, 4, 7, 2, -1, 4, 1, 0, 5, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(-0.7073938f, 0.00353458687f, -0.7068108f, -0.4942627f),
					new float4(0f, -1f, 0f, -0.4945354f),
					new float4(0.7145615f, 0.01037957f, -0.6994956f, -0.415472984f),
					new float4(-0.5001444f, -0.707174242f, -0.499760121f, -0.0104837362f),
					new float4(0.499967754f, 0.707112551f, 0.5000241f, -0.0164634474f),
					new float4(-0.922935069f, 0.002710189f, 0.384946048f, -0.4056939f),
				}, contents: 2, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(40.5860443f, 5.67953539f, -30.39397f, 1f)), name: "Glass 653470 #754")
					.WithMesh(new float3[] { new float3(-0.5141934f, 0.477652133f, -0.182279527f), new float3(-0.514058948f, 0.515608f, -0.182224318f), new float3(-0.0545898043f, 0.5159115f, -0.642070949f), new float3(-0.05440378f, 0.477670223f, -0.642448366f), new float3(-0.09569033f, -0.4945354f, 0.827955544f), new float3(-0.111400373f, -0.4945354f, 0.7902896f), new float3(0.6329912f, -0.4945354f, 0.04532564f), new float3(0.659720063f, -0.4945354f, 0.0726302341f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 2, 7, 6, 3, -1, 5, 0, 3, 6, -1, 1, 4, 7, 2, -1, 4, 1, 0, 5, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 1f, 0f, -0.0232987776f),
					new float4(0f, -1f, 0f, -0.0148262214f),
					new float4(0.707122f, 0f, -0.7070915f, -0.469682336f),
					new float4(-0.707106769f, 0f, -0.707106769f, -0.03388558f),
					new float4(0.7070192f, 0f, 0.7071943f, -0.0423458442f),
					new float4(-0.9244466f, 0f, 0.381311625f, -0.54412663f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(40.8973465f, 5.18076372f, -30.00591f, 1f)), name: "Solid 653471 #755")
					.WithMesh(new float3[] { new float3(-0.430707335f, 0.0232987776f, 0.3827859f), new float3(-0.39925608f, 0.0232987776f, 0.459035873f), new float3(0.362099081f, 0.0232987776f, -0.302130759f), new float3(0.308155358f, 0.0232987776f, -0.3560768f), new float3(-0.39925608f, -0.0148262214f, 0.459035873f), new float3(-0.430707335f, -0.0148262214f, 0.3827859f), new float3(0.308155358f, -0.0148262214f, -0.3560768f), new float3(0.362099081f, -0.0148262214f, -0.302130759f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 2, 7, 6, 3, -1, 5, 0, 3, 6, -1, 1, 4, 7, 2, -1, 4, 1, 0, 5, -1 }))
				;
		}

		// bm_c4a1b1, Model: the mesh has a triangle the exact CSG did not make; Solid 735630 #1153 face -1 near (-14.843,
		// -7.32, -15.25) (7 brushes)
		static ContentsScene BmC4a1b1Model294Site1()
		{
			return new ContentsScene()
				.WithContentsList("Solid", "Water", "Glass", "Grate")
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 1f, 0f, -16.4022217f),
					new float4(0f, -1f, 0f, -10.4377775f),
					new float4(0f, 0f, -1f, -0.304999232f),
					new float4(0f, 0f, 1f, -0.305000752f),
					new float4(0.707106769f, 0f, -0.707106769f, -2.39630628f),
					new float4(-0.707106769f, 0f, 0.707106769f, -1.91704512f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 0.99999994f, 2.13162821E-14f, 0f), new float4(0f, -2.13162821E-14f, 0.99999994f, 0f), new float4(-18.94389f, 7.99775028f, -19.8250027f, 1f)), name: "Solid 109892 #479")
					.WithMesh(new float3[] { new float3(-3.01611042f, 16.4022217f, -0.304999232f), new float3(-2.40611053f, 16.4022217f, 0.305000752f), new float3(3.69388962f, 16.4022217f, 0.305000752f), new float3(3.08388972f, 16.4022217f, -0.304999232f), new float3(-2.40611053f, -10.4377775f, 0.305000752f), new float3(-3.01611042f, -10.4377775f, -0.304999232f), new float3(3.08388972f, -10.4377775f, -0.304999232f), new float3(3.69388962f, -10.4377775f, 0.305000752f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 5, 0, 3, 6, -1, 1, 4, 7, 2, -1, 2, 7, 6, 3, -1, 4, 1, 0, 5, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 1f, 0f, -1.22f),
					new float4(0f, -1f, 0f, -1.21999991f),
					new float4(0f, 0f, 1f, -2.13500023f),
					new float4(0f, 0f, -1f, -2.13499975f),
					new float4(1f, 0f, 0f, -1.76222229f),
					new float4(-0.707106769f, 0f, 0.707106769f, -1.55759919f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 0.99999994f, 2.13162821E-14f, 0f), new float4(0f, -2.13162821E-14f, 0.99999994f, 0f), new float4(-17.0122223f, -3.66003942f, -17.3849945f, 1f)), name: "Solid 135746 #554")
					.WithMesh(new float3[] { new float3(-4.33777761f, 1.22f, -2.13499975f), new float3(-0.0677777f, 1.22f, 2.13500023f), new float3(1.76222229f, 1.22f, 2.13500023f), new float3(1.76222229f, 1.22f, -2.13499975f), new float3(-0.0677777f, -1.21999991f, 2.13500023f), new float3(-4.33777761f, -1.21999991f, -2.13499975f), new float3(1.76222229f, -1.21999991f, -2.13499975f), new float3(1.76222229f, -1.21999991f, 2.13500023f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 1, 4, 7, 2, -1, 5, 0, 3, 6, -1, 6, 3, 2, 7, -1, 0, 5, 4, 1, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 1f, 0f, -0.6099998f),
					new float4(0f, -1f, 0f, -0.610000253f),
					new float4(0.707106769f, 0f, 0.707106769f, -3.01934552f),
					new float4(-0.707106769f, 0f, -0.707106769f, -3.01934648f),
					new float4(0.707106769f, 0f, -0.707106769f, -5.271874f),
					new float4(-0.707106769f, 0f, 0.707106769f, -4.217499f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 0.99999994f, 2.13162821E-14f, 0f), new float4(0f, -2.13162821E-14f, 0.99999994f, 0f), new float4(-22.9427776f, -5.49003363f, -13.6572132f, 1f)), name: "Solid 135748 #555")
					.WithMesh(new float3[] { new float3(-5.11722231f, 0.6099998f, 0.8472216f), new float3(-0.8472223f, 0.6099998f, 5.117222f), new float3(5.86277771f, 0.6099998f, -1.59277821f), new float3(1.59277749f, 0.6099998f, -5.862778f), new float3(-0.8472223f, -0.610000253f, 5.117222f), new float3(-5.11722231f, -0.610000253f, 0.8472216f), new float3(1.59277749f, -0.610000253f, -5.862778f), new float3(5.86277771f, -0.610000253f, -1.59277821f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 1, 4, 7, 2, -1, 5, 0, 3, 6, -1, 6, 3, 2, 7, -1, 0, 5, 4, 1, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 1f, 0f, -5.69333267f),
					new float4(0f, 0f, -1f, -0.3050003f),
					new float4(0f, 0f, 1f, -0.3049997f),
					new float4(-0.707106769f, 0f, 0.707106769f, -1.5336355f),
					new float4(0.707106769f, 0f, 0.707106769f, -1.91704512f),
					new float4(0f, -1f, 0f, -8.946667f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 0.99999994f, 2.13162821E-14f, 0f), new float4(0f, -2.13162821E-14f, 0.99999994f, 0f), new float4(-14.6061115f, 28.4666328f, -14.9449968f, 1f)), name: "Solid 110809 #1014")
					.WithMesh(new float3[] { new float3(2.40611148f, 5.69333267f, 0.3049997f), new float3(3.01611161f, 5.69333267f, -0.3050003f), new float3(-2.4738884f, 5.69333267f, -0.3050003f), new float3(-1.8638885f, 5.69333267f, 0.3049997f), new float3(3.01611161f, -8.946667f, -0.3050003f), new float3(-2.4738884f, -8.946667f, -0.3050003f), new float3(-1.8638885f, -8.946667f, 0.3049997f), new float3(2.40611148f, -8.946667f, 0.3049997f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 2, 1, -1, 6, 7, 0, 3, -1, 5, 6, 3, 2, -1, 7, 4, 1, 0, -1, 4, 7, 6, 5, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, -1f, 0f, -0.237222522f),
					new float4(0.40613848f, 0f, -0.913811564f, -1.38668144f),
					new float4(0f, 0f, 1f, -1.59277725f),
					new float4(-0.989949465f, 0f, 0.141421363f, -2.20460129f),
					new float4(1f, 0f, 0f, -2.88055587f),
					new float4(0f, 1f, 0f, -0.3727775f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 0.99999994f, 2.13162821E-14f, 0f), new float4(0f, -2.13162821E-14f, 0.99999994f, 0f), new float4(-15.0805559f, -11.3528175f, -16.8427753f, 1f)), name: "Solid 260315 #1068")
					.WithMesh(new float3[] { new float3(2.88055587f, -0.237222522f, -0.237222716f), new float3(2.88055587f, -0.237222522f, 1.59277725f), new float3(-1.999444f, -0.237222522f, 1.59277725f), new float3(-2.60944414f, -0.237222522f, -2.67722273f), new float3(-2.60944414f, 0.3727775f, -2.67722273f), new float3(2.88055587f, 0.3727775f, -0.237222716f), new float3(2.88055587f, 0.3727775f, 1.59277725f), new float3(-1.999444f, 0.3727775f, 1.59277725f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 0, 3, -1, 6, 7, 2, 1, -1, 7, 4, 3, 2, -1, 5, 6, 1, 0, -1, 6, 5, 4, 7, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 0f, -1f, -0.3050003f),
					new float4(0f, 0f, 1f, -0.3049997f),
					new float4(-0.707106769f, 0f, 0.707106769f, -1.5336355f),
					new float4(0.707106769f, 0f, 0.707106769f, -1.91704512f),
					new float4(0f, -1f, 0f, -12.0983372f),
					new float4(0f, 1f, 0f, -19.0116634f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 0.99999994f, 2.13162821E-14f, 0f), new float4(0f, -2.13162821E-14f, 0.99999994f, 0f), new float4(-14.6061115f, 0.5083026f, -14.944993f, 1f)), name: "Solid 735630 #1153")
					.WithMesh(new float3[] { new float3(-2.4738884f, 19.0116634f, -0.3050003f), new float3(3.01611161f, 19.0116634f, -0.3050003f), new float3(3.01611161f, -12.0983372f, -0.3050003f), new float3(-2.4738884f, -12.0983372f, -0.3050003f), new float3(2.40611148f, 19.0116634f, 0.3049997f), new float3(-1.8638885f, 19.0116634f, 0.3049997f), new float3(-1.8638885f, -12.0983372f, 0.3049997f), new float3(2.40611148f, -12.0983372f, 0.3049997f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 5, 0, 3, 6, -1, 1, 4, 7, 2, -1, 2, 7, 6, 3, -1, 4, 1, 0, 5, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(-0.707106769f, 0f, -0.707106769f, -0.383408934f),
					new float4(0.707106769f, 0f, 0.707106769f, -0.479261339f),
					new float4(0.707106769f, 0f, -0.707106769f, -2.6838634f),
					new float4(0f, -1f, 0f, -11.8611155f),
					new float4(-0.707106769f, 0f, 0.707106769f, -3.3548286f),
					new float4(0f, 1f, 0f, -18.6388855f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 0.99999994f, 2.13162821E-14f, 0f), new float4(0f, -2.13162821E-14f, 0.99999994f, 0f), new float4(-18.7066669f, 0.8810824f, -13.0811014f, 1f)), name: "Solid 735632 #1154")
					.WithMesh(new float3[] { new float3(-2.6433332f, 18.6388855f, 2.101111f), new float3(1.626667f, 18.6388855f, -2.168889f), new float3(1.626667f, -11.8611155f, -2.168889f), new float3(-2.6433332f, -11.8611155f, 2.101111f), new float3(2.236667f, 18.6388855f, -1.55888915f), new float3(-2.033333f, 18.6388855f, 2.711111f), new float3(-2.033333f, -11.8611155f, 2.711111f), new float3(2.236667f, -11.8611155f, -1.55888915f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 1, 4, 7, 2, -1, 6, 3, 2, 7, -1, 5, 0, 3, 6, -1, 0, 5, 4, 1, -1 }))
				;
		}

		// bm_c4a1b1, Model: the exact CSG made a triangle the mesh does not have; Solid 132430 #964 face -1 near (9.15, 10.7,
		// 6.1) (6 brushes)
		static ContentsScene BmC4a1b1Model294Site2()
		{
			return new ContentsScene()
				.WithContentsList("Solid", "Water", "Glass", "Grate")
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, -1f, 0f, -2.982222f),
					new float4(-0.707106769f, 0f, -0.707106769f, -0.431345165f),
					new float4(0.7072393f, -3.04243222E-05f, 0.706974268f, -0.4313392f),
					new float4(0.707106769f, 0f, -0.707106769f, -0.3834197f),
					new float4(-0.7072393f, -3.31212141E-05f, 0.706974268f, -0.479268551f),
					new float4(0f, 1f, 0f, -1.8977778f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 0.99999994f, 2.13162821E-14f, 0f), new float4(0f, -2.13162821E-14f, 0.99999994f, 0f), new float4(9.183889f, 7.862188f, 6.675891f, 1f)), name: "Solid 132570 #530")
					.WithMesh(new float3[] { new float3(-0.03387913f, -2.982222f, 0.6438836f), new float3(-0.643774152f, -2.982222f, 0.0337599553f), new float3(-0.0338884257f, -2.982222f, -0.576125741f), new float3(0.5760066f, -2.982222f, 0.03376925f), new float3(-0.6438884f, 1.8977778f, 0.0338742435f), new float3(-0.0338884257f, 1.8977778f, -0.576125741f), new float3(0.576111555f, 1.8977778f, 0.033874236f), new float3(-0.0338884369f, 1.8977778f, 0.6441029f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 2, 1, -1, 6, 7, 0, 3, -1, 5, 6, 3, 2, -1, 7, 4, 1, 0, -1, 4, 7, 6, 5, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, -1f, 0f, -0.237221941f),
					new float4(-0.707106769f, 0f, -0.707106769f, -1.05436575f),
					new float4(-0.7069742f, -0.000265069044f, 0.7072393f, -1.05457234f),
					new float4(1f, 0f, 0f, -0.338888884f),
					new float4(-1f, 0f, 0f, -0.2711111f),
					new float4(0f, 1f, 0f, -0.372778058f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 0.99999994f, 2.13162821E-14f, 0f), new float4(0f, -2.13162821E-14f, 0.99999994f, 0f), new float4(9.42111f, 9.99718952f, 4.87998533f, 1f)), name: "Solid 132573 #533")
					.WithMesh(new float3[] { new float3(-0.2711111f, -0.237221941f, 1.22001266f), new float3(-0.2711111f, -0.237221941f, -1.21998727f), new float3(0.338888884f, -0.237221941f, -1.82998729f), new float3(0.338888884f, -0.237221941f, 1.829784f), new float3(-0.2711111f, 0.372778058f, -1.21998727f), new float3(0.338888884f, 0.372778058f, -1.82998729f), new float3(0.338888884f, 0.372778058f, 1.83001268f), new float3(-0.2711111f, 0.372778058f, 1.22024131f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 2, 1, -1, 6, 7, 0, 3, -1, 5, 6, 3, 2, -1, 7, 4, 1, 0, -1, 4, 7, 6, 5, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 1f, 0f, -12.0983334f),
					new float4(0f, -1f, 0f, -19.0116673f),
					new float4(-1f, 0f, 0f, -1.35555542f),
					new float4(0f, 0f, -1f, -1.35555542f),
					new float4(0.707106769f, 0f, 0.707106769f, -0.2396308f),
					new float4(-0.707106769f, 0f, -0.707106769f, -0.191704348f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 0.99999994f, 2.13162821E-14f, 0f), new float4(0f, -2.13162821E-14f, 0.99999994f, 0f), new float4(7.455555f, 2.54163241f, 7.45556164f, 1f)), name: "Solid 110039 #955")
					.WithMesh(new float3[] { new float3(1.08444452f, 12.0983334f, -1.35555542f), new float3(-1.35555542f, 12.0983334f, 1.08444452f), new float3(-1.35555542f, 12.0983334f, 1.69444454f), new float3(1.69444454f, 12.0983334f, -1.35555542f), new float3(-1.35555542f, -19.0116673f, 1.08444452f), new float3(1.08444452f, -19.0116673f, -1.35555542f), new float3(1.69444454f, -19.0116673f, -1.35555542f), new float3(-1.35555542f, -19.0116673f, 1.69444454f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 1, 4, 7, 2, -1, 5, 0, 3, 6, -1, 6, 3, 2, 7, -1, 0, 5, 4, 1, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 1f, 0f, -1.89777815f),
					new float4(0f, 0f, 1f, -1.22003376f),
					new float4(-0.0005002021f, -6.25252651E-05f, -0.9999999f, -1.2199831f),
					new float4(1f, 0f, 0f, -0.338888556f),
					new float4(-1f, 0f, 0f, -0.271111459f),
					new float4(0f, -1f, 0f, -2.98222184f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 0.99999994f, 2.13162821E-14f, 0f), new float4(0f, -2.13162821E-14f, 0.99999994f, 0f), new float4(8.811111f, 12.7421837f, 4.87996435f, 1f)), name: "Solid 132430 #964")
					.WithMesh(new float3[] { new float3(-0.271111459f, 1.89777815f, -1.21996629f), new float3(-0.271111459f, 1.89777815f, 1.22003376f), new float3(0.338888556f, 1.89777815f, 1.22003376f), new float3(0.338888556f, 1.89777815f, -1.22027147f), new float3(-0.271111459f, -2.98222184f, 1.22003376f), new float3(0.338888556f, -2.98222184f, 1.22003376f), new float3(0.338888556f, -2.98222184f, -1.21996629f), new float3(-0.271111459f, -2.98222184f, -1.21966112f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 2, 1, -1, 6, 7, 0, 3, -1, 5, 6, 3, 2, -1, 7, 4, 1, 0, -1, 4, 7, 6, 5, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, -1f, 0f, -8.302778f),
					new float4(0f, 0f, 1f, -1.22006381f),
					new float4(-0.0006251334f, 0f, -0.9999998f, -1.22010541f),
					new float4(1f, 0f, 0f, -0.338888556f),
					new float4(-1f, 0f, 0f, -0.271111459f),
					new float4(0f, 1f, 0f, -13.0472221f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 0.99999994f, 2.13162821E-14f, 0f), new float4(0f, -2.13162821E-14f, 0.99999994f, 0f), new float4(8.811111f, -8.167258f, 4.879938f, 1f)), name: "Solid 132432 #965")
					.WithMesh(new float3[] { new float3(-0.271111459f, -8.302778f, 1.22006381f), new float3(-0.271111459f, -8.302778f, -1.21993613f), new float3(0.338888556f, -8.302778f, -1.22031748f), new float3(0.338888556f, -8.302778f, 1.22006381f), new float3(0.338888556f, 13.0472221f, 1.22006381f), new float3(-0.271111459f, 13.0472221f, 1.22006381f), new float3(-0.271111459f, 13.0472221f, -1.21993613f), new float3(0.338888556f, 13.0472221f, -1.22031748f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 0, 3, -1, 6, 7, 2, 1, -1, 7, 4, 3, 2, -1, 5, 6, 1, 0, -1, 6, 5, 4, 7, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 1f, 0f, -0.372778535f),
					new float4(0f, -1f, 0f, -0.23722145f),
					new float4(-0.707106769f, 0f, 0.707106769f, -2.15667558f),
					new float4(0.707106769f, 0f, -0.707106769f, -2.15667558f),
					new float4(0.707106769f, 0f, 0.707106769f, -2.39630628f),
					new float4(-0.707106769f, 0f, -0.707106769f, -1.917045f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 0.99999994f, 2.13162821E-14f, 0f), new float4(0f, -2.13162821E-14f, 0.99999994f, 0f), new float4(8.980555f, -16.23281f, 8.98056f, 1f)), name: "Solid 112025 #985")
					.WithMesh(new float3[] { new float3(0.169444412f, 0.372778535f, -2.88055539f), new float3(-2.88055539f, 0.372778535f, 0.169444412f), new float3(0.1694445f, 0.372778535f, 3.21944451f), new float3(3.21944451f, 0.372778535f, 0.1694445f), new float3(-2.88055539f, -0.23722145f, 0.169444412f), new float3(0.169444412f, -0.23722145f, -2.88055539f), new float3(3.21944451f, -0.23722145f, 0.1694445f), new float3(0.1694445f, -0.23722145f, 3.21944451f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 1, 4, 7, 2, -1, 5, 0, 3, 6, -1, 6, 3, 2, 7, -1, 0, 5, 4, 1, -1 }))
				;
		}

		// bm_c4a1c, Clip: the mesh has a triangle the exact CSG did not make; Clip 12408 #448 face -1 near (-8.807, -5.011,
		// 0.859) (4 brushes)
		static ContentsScene BmC4a1cClip90Site1()
		{
			return new ContentsScene()
				.WithContentsList("Solid", "Water", "Glass", "Grate")
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0.9986448f, -2.62699359E-07f, 0.05204306f, -3.247742f),
					new float4(0f, 1f, 0f, -3.355f),
					new float4(-0.358863562f, -9.543732E-06f, 0.93339f, -0.438919872f),
					new float4(0f, -1f, 0f, -4.19375f),
					new float4(0.35806042f, 0f, -0.9336984f, -0.279058963f),
					new float4(-0.9879114f, 0f, -0.155019417f, -2.12004685f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 0.99999994f, 2.13162821E-14f, 0f), new float4(0f, -2.13162821E-14f, 0.99999994f, 0f), new float4(-7.475054f, -0.07627129f, 1.66815531f, 1f)), name: "Clip 8947 #442")
					.WithMesh(new float3[] { new float3(3.203698f, -4.19375f, 0.929699f), new float3(3.2037f, 3.355f, 0.9296998f), new float3(3.16424251f, 3.355f, 1.68684387f), new float3(3.16424441f, -4.19375f, 1.68676746f), new float3(-1.97994673f, 3.355f, -1.058157f), new float3(-2.093483f, 3.355f, -0.3346113f), new float3(-2.09347153f, -4.19375f, -0.334684074f), new float3(-1.97994673f, -4.19375f, -1.058157f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 2, 1, -1, 5, 6, 3, 2, -1, 6, 7, 0, 3, -1, 7, 4, 1, 0, -1, 4, 7, 6, 5, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 1f, 0f, -1.30714655f),
					new float4(-0.408483952f, -3.14354038E-05f, 0.9127655f, -0.3922078f),
					new float4(0.409097582f, 0f, -0.912490666f, -0.313708544f),
					new float4(0.9880786f, -2.93508742E-06f, 0.153950274f, -0.581860363f),
					new float4(-0.995975137f, -8.240013E-06f, -0.0896296948f, -0.5703289f),
					new float4(-0.514577031f, -0.857444167f, 3.7445312E-05f, -0.6885228f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 0.99999994f, 2.13162821E-14f, 0f), new float4(0f, -2.13162821E-14f, 0.99999994f, 0f), new float4(-10.0554962f, -4.12840939f, 0.6845812f, 1f)), name: "Clip 12302 #447")
					.WithMesh(new float3[] { new float3(-0.5206977f, 1.30714655f, -0.577238441f), new float3(-0.5876506f, 1.30714655f, 0.166749358f), new float3(0.4879076f, 1.30714655f, 0.648086846f), new float3(0.6005031f, 1.30714655f, -0.07456974f), new float3(-0.587631345f, -0.4503325f, 0.166697428f), new float3(0.487912983f, -1.09577668f, 0.6480065f), new float3(0.600496233f, -1.16337276f, -0.07457281f), new float3(-0.5206834f, -0.490542382f, -0.577232063f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 2, 1, -1, 6, 7, 0, 3, -1, 5, 6, 3, 2, -1, 7, 4, 1, 0, -1, 4, 7, 6, 5, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 1f, 0f, -0.7399373f),
					new float4(-0.358865321f, 2.87648963E-05f, 0.933389366f, -0.358334154f),
					new float4(0.358037978f, 1.713693E-05f, -0.933707f, -0.358430326f),
					new float4(-0.9879164f, 0f, -0.154987767f, -0.787322f),
					new float4(1f, 0f, 0f, -0.601328433f),
					new float4(-0.219945759f, -0.9755121f, -0.000121116711f, -0.4600433f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 0.99999994f, 2.13162821E-14f, 0f), new float4(0f, -2.13162821E-14f, 0.99999994f, 0f), new float4(-8.760078f, -5.00995541f, 1.26036394f, 1f)), name: "Clip 12408 #448")
					.WithMesh(new float3[] { new float3(0.601328433f, 0.7399373f, 0.615079641f), new float3(0.601328433f, 0.7399373f, -0.153280661f), new float3(-0.6949244f, 0.7399373f, -0.650339961f), new float3(-0.8084152f, 0.7399373f, 0.0730678f), new float3(-0.8084198f, -0.2893287f, 0.07309771f), new float3(0.601328433f, -0.607247651f, 0.6151211f), new float3(0.601328433f, -0.6071523f, -0.153305382f), new float3(-0.694921553f, -0.314829f, -0.65035826f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 0, 3, -1, 6, 7, 2, 1, -1, 7, 4, 3, 2, -1, 5, 6, 1, 0, -1, 6, 5, 4, 7, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 1f, 0f, -0.85600996f),
					new float4(-0.358876735f, 4.21792465E-05f, 0.933384955f, -0.358879834f),
					new float4(0.358050048f, -1.55709745E-06f, -0.9337024f, -0.35899207f),
					new float4(-1f, 0f, 0f, -0.683096349f),
					new float4(0.9959743f, -1.20984914E-05f, 0.08963903f, -0.56307f),
					new float4(-0.0729464144f, -0.997335851f, -7.345216E-05f, -0.544192255f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 0.99999994f, 2.13162821E-14f, 0f), new float4(0f, -2.13162821E-14f, 0.99999994f, 0f), new float4(-7.4756546f, -5.12602949f, 1.75358891f, 1f)), name: "Clip 12580 #449")
					.WithMesh(new float3[] { new float3(0.5130027f, 0.85600996f, 0.5816983f), new float3(0.5799447f, 0.85600996f, -0.162090391f), new float3(-0.683096349f, 0.85600996f, -0.646433055f), new float3(-0.683096349f, 0.85600996f, 0.121810772f), new float3(-0.683096349f, -0.495692372f, 0.121871851f), new float3(0.5129801f, -0.5832088f, 0.5817547f), new float3(0.5799275f, -0.588050663f, -0.162094563f), new float3(-0.683096349f, -0.495635778f, -0.6464308f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 0, 3, -1, 6, 7, 2, 1, -1, 7, 4, 3, 2, -1, 5, 6, 1, 0, -1, 6, 5, 4, 7, -1 }))
				;
		}

		// bm_c4a3b, Model: the exact CSG made a triangle the mesh does not have; Solid 512484 #90 face -1 near (74.013,
		// 28.67, -8.133) (7 brushes)
		static ContentsScene BmC4a3bModel0Site1()
		{
			return new ContentsScene()
				.WithContentsList("Solid", "Water", "Glass", "Grate")
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 1f, 0f, -17.08f),
					new float4(0f, -1f, 0f, -26.84f),
					new float4(-0.707106769f, 0f, -0.707106769f, -8.243376f),
					new float4(-0.707106769f, -5.825631E-06f, 0.707106769f, -8.243363f),
					new float4(-1f, 0f, 0f, -0.67777437f),
					new float4(1f, 0f, 0f, -0.542225659f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(78.7577744f, 29.28f, -23.17988f, 1f)), name: "Solid 484456 #38")
					.WithMesh(new float3[] { new float3(0.542225659f, 17.08f, 12.200243f), new float3(0.542225659f, 17.08f, -12.20012f), new float3(-0.67777437f, 17.08f, -10.98012f), new float3(-0.67777437f, 17.08f, 10.9802427f), new float3(0.542225659f, -26.84f, -12.20012f), new float3(0.542225659f, -26.84f, 12.1998806f), new float3(-0.67777437f, -26.84f, 10.9798813f), new float3(-0.67777437f, -26.84f, -10.98012f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 1, 4, 7, 2, -1, 5, 0, 3, 6, -1, 6, 3, 2, 7, -1, 0, 5, 4, 1, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 1f, 0f, -17.08f),
					new float4(0f, -1f, 0f, -26.84f),
					new float4(0f, 0f, -1f, -1.35555565f),
					new float4(1f, 0f, 0f, -1.08444273f),
					new float4(-0.707106769f, 0f, 0.707106769f, -0.862671435f),
					new float4(0.707106769f, 0f, -0.707106769f, -0.8626691f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(74.55556f, 29.28f, -4.74444437f, 1f)), name: "Solid 484458 #39")
					.WithMesh(new float3[] { new float3(1.08444273f, 17.08f, -0.13555564f), new float3(-0.135557279f, 17.08f, -1.35555565f), new float3(-2.57555723f, 17.08f, -1.35555565f), new float3(1.08444273f, 17.08f, 2.30444431f), new float3(-0.135557279f, -26.84f, -1.35555565f), new float3(1.08444273f, -26.84f, -0.13555564f), new float3(1.08444273f, -26.84f, 2.30444431f), new float3(-2.57555723f, -26.84f, -1.35555565f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 1, 4, 7, 2, -1, 5, 0, 3, 6, -1, 6, 3, 2, 7, -1, 0, 5, 4, 1, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 1f, 0f, -17.08f),
					new float4(0f, -1f, 0f, -26.84f),
					new float4(0.707106769f, 0f, -0.707106769f, -4.31335163f),
					new float4(0f, 0f, 1f, -2.44f),
					new float4(-0.707106769f, 0f, -0.707106769f, -0.8626699f),
					new float4(0.707106769f, 0f, 0.707106769f, -0.86267066f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(75.64f, 29.28f, -8.54f, 1f)), name: "Solid 512484 #90")
					.WithMesh(new float3[] { new float3(-1.21999943f, 17.08f, 2.44f), new float3(3.66000056f, 17.08f, -2.44f), new float3(2.44000053f, 17.08f, -3.66f), new float3(-3.65999961f, 17.08f, 2.44f), new float3(3.66000056f, -26.84f, -2.44f), new float3(-1.21999943f, -26.84f, 2.44f), new float3(-3.65999961f, -26.84f, 2.44f), new float3(2.44000053f, -26.84f, -3.66f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 1, 4, 7, 2, -1, 5, 0, 3, 6, -1, 6, 3, 2, 7, -1, 0, 5, 4, 1, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 1f, 0f, -4.473335f),
					new float4(0f, -1f, 0f, -2.84666538f),
					new float4(1f, 0f, 0f, -3.25333214f),
					new float4(-0.707106769f, 0f, -0.707106769f, -1.72529435f),
					new float4(0.7071222f, 3.70884163E-06f, 0.707091331f, -1.7252506f),
					new float4(-0.707106769f, 0f, 0.707106769f, -5.751183f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(74.82667f, 41.8866653f, -11.3867331f, 1f)), name: "Solid 4065866 #218")
					.WithMesh(new float3[] { new float3(-5.286668f, 4.473335f, 2.84673333f), new float3(-2.846687f, 4.473335f, 5.286714f), new float3(3.25333214f, 4.473335f, -0.8135716f), new float3(3.25333214f, 4.473335f, -5.693267f), new float3(-2.846668f, -2.84666538f, 5.286733f), new float3(-5.286668f, -2.84666538f, 2.84673333f), new float3(3.25333214f, -2.84666538f, -5.693267f), new float3(3.25333214f, -2.84666538f, -0.8135331f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 2, 7, 6, 3, -1, 5, 0, 3, 6, -1, 1, 4, 7, 2, -1, 4, 1, 0, 5, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 1f, 0f, -2.32380986f),
					new float4(0f, -1f, 0f, -3.77619f),
					new float4(-1f, 0f, 0f, -2.61428547f),
					new float4(-0.707106769f, 0f, -0.707106769f, -1.80749965f),
					new float4(0f, 0f, 1f, -2.498095f),
					new float4(0.707106769f, 0f, 0.707106769f, -1.64318144f),
					new float4(0.707106769f, 0f, -0.707106769f, -3.28636312f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(68.4942856f, 44.03619f, -4.938095f, 1f)), name: "Solid 4065867 #219")
					.WithMesh(new float3[] { new float3(3.48571444f, 2.32380986f, -1.16190493f), new float3(1.04571438f, 2.32380986f, -3.60190487f), new float3(-2.61428547f, 2.32380986f, 0.05809491f), new float3(-2.61428547f, 2.32380986f, 2.498095f), new float3(-0.174285516f, 2.32380986f, 2.498095f), new float3(1.04571438f, -3.77619f, -3.60190487f), new float3(3.48571444f, -3.77619f, -1.16190493f), new float3(-0.174285516f, -3.77619f, 2.498095f), new float3(-2.61428547f, -3.77619f, 2.498095f), new float3(-2.61428547f, -3.77619f, 0.05809491f) }, new int[] { 0, 1, 2, 3, 4, -1, 5, 6, 7, 8, 9, -1, 8, 3, 2, 9, -1, 1, 5, 9, 2, -1, 7, 4, 3, 8, -1, 6, 0, 4, 7, -1, 0, 6, 5, 1, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, -1f, 0f, -7.900952f),
					new float4(1f, 0f, 0f, -4.00857353f),
					new float4(0.707106769f, 0f, 0.707106769f, -2.012899f),
					new float4(0f, 0f, -1f, -2.3819046f),
					new float4(-0.707106769f, 0f, 0.707106769f, -4.97062254f),
					new float4(-1f, 0f, 0f, -4.53142643f),
					new float4(0f, 1f, 0f, -12.8390484f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(74.07143f, 10.3409519f, -11.0380955f, 1f)), name: "Solid 4066453 #238")
					.WithMesh(new float3[] { new float3(-4.53142643f, -7.900952f, 2.49809551f), new float3(-4.53142643f, -7.900952f, -2.3819046f), new float3(4.00857353f, -7.900952f, -2.3819046f), new float3(4.00857353f, -7.900952f, -1.16190457f), new float3(-2.09142637f, -7.900952f, 4.93809557f), new float3(4.00857353f, 12.8390484f, -2.3819046f), new float3(4.00857353f, 12.8390484f, -1.16190457f), new float3(-2.09142637f, 12.8390484f, 4.93809557f), new float3(-4.53142643f, 12.8390484f, -2.3819046f), new float3(-4.53142643f, 12.8390484f, 2.49809551f) }, new int[] { 0, 1, 2, 3, 4, -1, 5, 6, 3, 2, -1, 6, 7, 4, 3, -1, 8, 5, 2, 1, -1, 7, 9, 0, 4, -1, 9, 8, 1, 0, -1, 8, 9, 7, 6, 5, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 1f, 0f, -4.0260005f),
					new float4(0f, -1f, 0f, -2.6839993f),
					new float4(1f, 0f, 0f, -0.6099997f),
					new float4(-0.707106769f, 0f, 0.707106769f, -0.8626704f),
					new float4(-0.707106769f, 0f, -0.707106769f, -0.862670541f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(68.93f, 19.154f, -10.37f, 1f)), name: "Solid 4081713 #244")
					.WithMesh(new float3[] { new float3(0.6099997f, 4.0260005f, -1.83000016f), new float3(-1.22000027f, 4.0260005f, -8.42936956E-08f), new float3(0.6099997f, 4.0260005f, 1.82999992f), new float3(-1.22000027f, -2.6839993f, -8.42936956E-08f), new float3(0.6099997f, -2.6839993f, -1.83000016f), new float3(0.6099997f, -2.6839993f, 1.82999992f) }, new int[] { 0, 1, 2, -1, 3, 4, 5, -1, 4, 0, 2, 5, -1, 1, 3, 5, 2, -1, 3, 1, 0, 4, -1 }))
				;
		}

		// bm_c4a3b, Details: the mesh has a triangle the exact CSG did not make; Glass 4102811 #1392 face -1 near (1.601,
		// 6.75, -13.342) (10 brushes)
		static ContentsScene BmC4a3bDetails115Site1()
		{
			return new ContentsScene()
				.WithContentsList("Solid", "Water", "Glass", "Grate")
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 1f, 0f, -0.225925878f),
					new float4(-0.8944272f, 0f, 0.4472136f, -3.62723351f),
					new float4(-0.5547002f, 0f, -0.8320503f, -3.85989165f),
					new float4(0f, 0f, -1f, -3.43407416f),
					new float4(0f, 0f, 1f, -4.495926f),
					new float4(0.9486833f, 0f, 0.316227764f, -4.915351f),
					new float4(-1f, 0f, 0f, -3.6374073f),
					new float4(0.707106769f, 0f, -0.707106769f, -4.60090828f),
					new float4(0f, -1f, 0f, -0.384074122f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(-3.68259263f, 6.484074f, -13.0359259f, 1f)), name: "Solid 1792509 #1194")
					.WithMesh(new float3[] { new float3(5.512593f, 0.225925878f, -0.9940743f), new float3(3.07259274f, 0.225925878f, -3.43407416f), new float3(-1.80740726f, 0.225925878f, -3.43407416f), new float3(-3.6374073f, 0.225925878f, -2.21407413f), new float3(-3.6374073f, 0.225925878f, 0.8359262f), new float3(-1.8074075f, 0.225925878f, 4.495926f), new float3(3.68259263f, 0.225925878f, 4.495926f), new float3(-3.6374073f, -0.384074122f, 0.8359262f), new float3(-1.8074075f, -0.384074122f, 4.495926f), new float3(-1.80740726f, -0.384074122f, -3.43407416f), new float3(-3.6374073f, -0.384074122f, -2.21407413f), new float3(3.07259274f, -0.384074122f, -3.43407416f), new float3(3.68259263f, -0.384074122f, 4.495926f), new float3(5.512593f, -0.384074122f, -0.9940743f) }, new int[] { 0, 1, 2, 3, 4, 5, 6, -1, 7, 8, 5, 4, -1, 9, 10, 3, 2, -1, 11, 9, 2, 1, -1, 8, 12, 6, 5, -1, 12, 13, 0, 6, -1, 10, 7, 4, 3, -1, 13, 11, 1, 0, -1, 11, 13, 12, 8, 7, 10, 9, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0.707106769f, 0f, -0.707106769f, -0.419353426f),
					new float4(0f, 1f, 0f, -0.271110624f),
					new float4(0.707106769f, 0f, 0.707106769f, -0.988476455f),
					new float4(-0.9486833f, 0f, -0.316227764f, -0.8573286f),
					new float4(-0.707106769f, 0f, 0.707106769f, -0.6589844f),
					new float4(0f, -1f, 0f, -0.3388894f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(1.44451392f, 16.80889f, -10.1624308f, 1f)), name: "Solid 2234156 #1209")
					.WithMesh(new float3[] { new float3(0.9954861f, -0.3388894f, 0.402430773f), new float3(-0.529513955f, -0.3388894f, -1.1225692f), new float3(-0.529513955f, 0.271110624f, -1.1225692f), new float3(0.9954861f, 0.271110624f, 0.402430773f), new float3(-0.9107639f, 0.271110624f, 0.0211807955f), new float3(0.232986048f, 0.271110624f, 1.16493082f), new float3(0.232986048f, -0.3388894f, 1.16493082f), new float3(-0.9107639f, -0.3388894f, 0.0211807955f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 3, 2, -1, 6, 0, 3, 5, -1, 1, 7, 4, 2, -1, 7, 6, 5, 4, -1, 6, 7, 1, 0, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0.707106769f, 0f, -0.707106769f, -0.479260981f),
					new float4(0.707106769f, 0f, 0.707106769f, -0.958522856f),
					new float4(0f, -1f, 0f, -1.6605556f),
					new float4(-0.9486833f, 0f, -0.316227764f, -0.8573284f),
					new float4(-0.707106769f, 0f, 0.707106769f, -0.599076867f),
					new float4(0f, 1f, 0f, -2.60944438f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(1.42333329f, 13.8605556f, -10.0988894f, 1f)), name: "Solid 2234157 #1210")
					.WithMesh(new float3[] { new float3(-0.508333266f, 2.60944438f, -1.18611062f), new float3(1.01666677f, 2.60944438f, 0.338889331f), new float3(1.01666677f, -1.6605556f, 0.338889331f), new float3(-0.508333266f, -1.6605556f, -1.18611062f), new float3(0.2541667f, 2.60944438f, 1.10138929f), new float3(0.2541667f, -1.6605556f, 1.10138929f), new float3(-0.8895833f, -1.6605556f, -0.0423606224f), new float3(-0.8895833f, 2.60944438f, -0.0423606224f) }, new int[] { 0, 1, 2, 3, -1, 1, 4, 5, 2, -1, 5, 6, 3, 2, -1, 7, 0, 3, 6, -1, 4, 7, 6, 5, -1, 7, 4, 1, 0, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, -1f, 0f, -1.66055584f),
					new float4(-0.9486833f, 0f, -0.316227764f, -0.3429315f),
					new float4(-0.707106769f, 0f, 0.707106769f, -2.300454f),
					new float4(0.9486833f, 0f, 0.316227764f, -0.428664237f),
					new float4(0.707106769f, 0f, -0.707106769f, -2.87556767f),
					new float4(0f, 1f, 0f, -2.60944414f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(0.474444449f, 8.370556f, -11.3188887f, 1f)), name: "Solid 2585718 #1218")
					.WithMesh(new float3[] { new float3(0.7455555f, -1.66055584f, -3.32111144f), new float3(1.35555553f, -1.66055584f, -2.71111131f), new float3(-0.474444449f, -1.66055584f, 2.7788887f), new float3(-1.0844444f, -1.66055584f, 2.16888881f), new float3(-1.0844444f, 2.60944414f, 2.16888881f), new float3(0.7455555f, 2.60944414f, -3.32111144f), new float3(-0.474444449f, 2.60944414f, 2.7788887f), new float3(1.35555553f, 2.60944414f, -2.71111131f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 0, 3, -1, 6, 4, 3, 2, -1, 7, 6, 2, 1, -1, 5, 7, 1, 0, -1, 7, 5, 4, 6, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(-0.9486833f, 0f, -0.316227764f, -0.3000651f),
					new float4(0f, 1f, 0f, -1.89777756f),
					new float4(-0.707106769f, 0f, 0.707106769f, -2.300454f),
					new float4(0.9486833f, 0f, 0.316227764f, -0.471530646f),
					new float4(0.707106769f, 0f, -0.707106769f, -2.875568f),
					new float4(0f, -1f, 0f, -2.37222242f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(0.440555543f, 13.3522224f, -11.3527775f, 1f)), name: "Solid 2585719 #1220")
					.WithMesh(new float3[] { new float3(0.7794445f, -2.37222242f, -3.28722262f), new float3(-1.05055559f, -2.37222242f, 2.20277762f), new float3(-1.05055559f, 1.89777756f, 2.20277762f), new float3(0.7794445f, 1.89777756f, -3.28722262f), new float3(1.38944447f, 1.89777756f, -2.67722273f), new float3(-0.440555573f, 1.89777756f, 2.81277752f), new float3(-0.440555573f, -2.37222242f, 2.81277752f), new float3(1.38944447f, -2.37222242f, -2.67722273f) }, new int[] { 0, 1, 2, 3, -1, 4, 3, 2, 5, -1, 1, 6, 5, 2, -1, 6, 7, 4, 5, -1, 7, 0, 3, 4, -1, 0, 7, 6, 1, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(-0.1670121f, 0.9717067f, 0.1670121f, -0.308712542f),
					new float4(0.707106769f, 5.455118E-16f, 0.707106769f, -3.33085585f),
					new float4(-0.948687553f, 0f, -0.316214919f, -2.29330444f),
					new float4(0.301511347f, -0.904534042f, -0.301511347f, -0.714625359f),
					new float4(0.707106769f, 0f, -0.707106769f, -2.01292515f),
					new float4(0f, -1f, 0f, -0.451146036f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(2.93137622f, 6.551146f, -10.0819178f, 1f)), name: "Solid 4102670 #1226")
					.WithMesh(new float3[] { new float3(-2.93131423f, -0.451146036f, 1.54197943f), new float3(0.11862392f, -0.451146036f, 4.59191751f), new float3(3.77862382f, 0.806979f, 0.9319177f), new float3(-1.10137618f, 0.806979f, -3.94808221f), new float3(2.86362386f, -0.451146036f, 1.84691775f), new float3(3.77862382f, 0.158853978f, 0.9319177f), new float3(-1.55886078f, -0.451146036f, -2.57556677f), new float3(-1.10137618f, 0.158853978f, -3.94808221f) }, new int[] { 0, 1, 2, 3, -1, 1, 4, 5, 2, -1, 6, 0, 3, 7, -1, 4, 6, 7, 5, -1, 5, 7, 3, 2, -1, 6, 4, 1, 0, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, -1f, 0f, -1.89777792f),
					new float4(-0.7186021f, 0f, 0.695421457f, -0.09583952f),
					new float4(0.7186021f, 0f, -0.695421457f, -0.11979907f),
					new float4(0.707106769f, 0f, 0.707106769f, -3.28893065f),
					new float4(-0.707106769f, 0f, -0.707106769f, -3.28893018f),
					new float4(0f, 1f, 0f, -2.982222f),
				}, contents: 2, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(3.72777772f, 7.997778f, -10.9715281f, 1f)), name: "Glass 4102811 #1392")
					.WithMesh(new float3[] { new float3(-2.35527778f, -1.89777792f, -2.29597187f), new float3(-2.20277786f, -1.89777792f, -2.44847178f), new float3(2.37222242f, -1.89777792f, 2.279028f), new float3(2.21972251f, -1.89777792f, 2.431528f), new float3(2.21972251f, 2.982222f, 2.431528f), new float3(-2.35527778f, 2.982222f, -2.29597187f), new float3(-2.20277786f, 2.982222f, -2.44847178f), new float3(2.37222242f, 2.982222f, 2.279028f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 0, 3, -1, 6, 7, 2, 1, -1, 7, 4, 3, 2, -1, 5, 6, 1, 0, -1, 6, 5, 4, 7, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 1f, 0f, -0.04659706f),
					new float4(0f, -1f, 0f, -0.0296529382f),
					new float4(0.707106769f, 0f, -0.707106769f, -0.431335449f),
					new float4(-0.707106769f, 0f, 0.707106769f, -0.431334823f),
					new float4(0.707106769f, 0f, 0.707106769f, -3.44469023f),
					new float4(-0.9486833f, 0f, -0.316227764f, -2.46482f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(3.626111f, 10.8571529f, -11.1663885f, 1f)), name: "Solid 3809938 #1505")
					.WithMesh(new float3[] { new float3(-1.79611111f, 0.04659706f, -2.40611148f), new float3(-2.10111117f, 0.04659706f, -1.49111152f), new float3(2.130764f, 0.04659706f, 2.74076366f), new float3(2.74076414f, 0.04659706f, 2.13076353f), new float3(-2.10111117f, -0.0296529382f, -1.49111152f), new float3(-1.79611111f, -0.0296529382f, -2.40611148f), new float3(2.74076414f, -0.0296529382f, 2.13076353f), new float3(2.130764f, -0.0296529382f, 2.74076366f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 5, 0, 3, 6, -1, 1, 4, 7, 2, -1, 2, 7, 6, 3, -1, 4, 1, 0, 5, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 1f, 0f, -1.57159758f),
					new float4(0f, -1f, 0f, -2.46965241f),
					new float4(0.707106769f, 0f, -0.707106769f, -0.4313348f),
					new float4(-0.707106769f, 0f, 0.707106769f, -0.431335479f),
					new float4(-0.707106769f, 0f, -0.707106769f, -0.0449307f),
					new float4(0.707106769f, 0f, 0.707106769f, -0.03594464f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(6.093646f, 9.332152f, -8.698854f, 1f)), name: "Solid 3809899 #1531")
					.WithMesh(new float3[] { new float3(-0.279583544f, 1.57159758f, 0.330416948f), new float3(0.330416471f, 1.57159758f, -0.279583067f), new float3(0.273228973f, 1.57159758f, -0.336770564f), new float3(-0.336771041f, 1.57159758f, 0.27322945f), new float3(0.330416471f, -2.46965241f, -0.279583067f), new float3(-0.279583544f, -2.46965241f, 0.330416948f), new float3(-0.336771041f, -2.46965241f, 0.27322945f), new float3(0.273228973f, -2.46965241f, -0.336770564f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 1, 4, 7, 2, -1, 5, 0, 3, 6, -1, 6, 3, 2, 7, -1, 0, 5, 4, 1, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 1f, 0f, -1.60125017f),
					new float4(0f, -1f, 0f, -2.51625f),
					new float4(-0.707106769f, 0f, 0.707106769f, -0.862670362f),
					new float4(0.707106769f, 0f, -0.707106769f, -0.8626702f),
					new float4(-0.9486833f, 0f, -0.316227764f, -0.03214984f),
					new float4(0.9486833f, 0f, 0.316227764f, -0.04018726f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(1.55041671f, 9.37875f, -13.0895834f, 1f)), name: "Solid 3809927 #1534")
					.WithMesh(new float3[] { new float3(-0.2732292f, 1.60125017f, 0.9467709f), new float3(0.336770773f, 1.60125017f, -0.883229136f), new float3(0.279583275f, 1.60125017f, -0.940416634f), new float3(-0.3304167f, 1.60125017f, 0.8895834f), new float3(0.336770773f, -2.51625f, -0.883229136f), new float3(-0.2732292f, -2.51625f, 0.9467709f), new float3(-0.3304167f, -2.51625f, 0.8895834f), new float3(0.279583275f, -2.51625f, -0.940416634f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 5, 0, 3, 6, -1, 1, 4, 7, 2, -1, 2, 7, 6, 3, -1, 4, 1, 0, 5, -1 }))
				;
		}

		// bm_c4a3c2, func_brush 4576957: the mesh has a triangle the exact CSG did not make; Grate 4576960 #3023 face -1 near
		// (92.266, -185.631, 139.677) (3 brushes)
		static ContentsScene BmC4a3c2FuncBrush4576957144Site1()
		{
			return new ContentsScene()
				.WithContentsList("Solid", "Water", "Glass", "Grate")
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 1f, 0f, -0.06036621f),
					new float4(0.5818826f, -0.001531721f, 0.813271344f, -0.0777780041f),
					new float4(-0.580748856f, -0.00118695665f, -0.8140819f, -0.0761773139f),
					new float4(0.5753867f, -0.7071331f, -0.41096583f, -0.750347137f),
					new float4(-0.5753823f, -0.7071206f, 0.410993516f, -0.8895961f),
					new float4(0f, -1f, 0f, -0.130258784f),
					new float4(-0.5769138f, 0.705756545f, 0.411191165f, -0.9227078f),
					new float4(0.575532556f, 0.7072864f, -0.410497427f, -0.781604052f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(93.17206f, -185.500366f, 139.00528f, 1f)), name: "Solid 4576958 #3021")
					.WithMesh(new float3[] { new float3(0.8952828f, 0.06036621f, -0.544810832f), new float3(0.8066347f, 0.06036621f, -0.669098735f), new float3(-1.05557978f, 0.06036621f, 0.659365833f), new float3(-0.965122938f, 0.06036621f, 0.7862796f), new float3(0.9263115f, 0.0221910346f, -0.56708324f), new float3(-0.996043444f, 0.022242913f, 0.808330953f), new float3(-0.8721489f, -0.130258784f, 0.7193992f), new float3(0.8021833f, -0.130258784f, -0.47855863f), new float3(0.8377136f, 0.0222412776f, -0.691214144f), new float3(0.7136619f, -0.130258784f, -0.602495968f), new float3(-0.9623424f, -0.130258784f, 0.5931302f), new float3(-1.08653593f, 0.022162566f, 0.681505f) }, new int[] { 0, 1, 2, 3, -1, 4, 0, 3, 5, 6, 7, -1, 1, 8, 9, 10, 11, 2, -1, 8, 4, 7, 9, -1, 5, 11, 10, 6, -1, 6, 10, 9, 7, -1, 11, 5, 3, 2, -1, 4, 8, 1, 0, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0.580686331f, 0f, 0.8141274f, -0.0380871519f),
					new float4(-0.580744147f, 0f, -0.814086139f, -0.0380998477f),
					new float4(0f, 1f, 0f, -0.3240564f),
					new float4(-0.813865662f, 0f, 0.581053138f, -0.9149596f),
					new float4(0.8147465f, 0f, -0.579817355f, -1.14371085f),
					new float4(0f, -1f, 0f, -0.3240686f),
				}, contents: 3, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(93.02091f, -185.954681f, 139.159912f, 1f)), name: "Grate 4576959 #3022")
					.WithMesh(new float3[] { new float3(-0.722763062f, -0.3240686f, 0.5623024f), new float3(0.9532104f, -0.3240686f, -0.633106232f), new float3(0.9532104f, 0.3240564f, -0.633106232f), new float3(-0.722763062f, 0.3240564f, 0.5623024f), new float3(0.9089888f, -0.3240686f, -0.695245564f), new float3(-0.766994f, -0.3240686f, 0.5003493f), new float3(-0.766994f, 0.3240564f, 0.5003493f), new float3(0.9089888f, 0.3240564f, -0.695245564f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 2, 7, 6, 3, -1, 5, 0, 3, 6, -1, 1, 4, 7, 2, -1, 4, 1, 0, 5, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0.5809518f, 0f, 0.813937962f, -0.07261613f),
					new float4(-0.579898357f, 0f, -0.814688861f, -0.07979287f),
					new float4(-0.813729346f, 0f, 0.581244f, -0.0689966f),
					new float4(0.5753823f, 0.7071206f, -0.410993516f, -0.2541889f),
					new float4(0.813729346f, 0f, -0.581244f, -0.121646121f),
					new float4(0.418631673f, -0.857514f, -0.299027115f, -0.316338569f),
					new float4(-0.575304747f, 0.7072159f, 0.4109381f, -0.297864676f),
				}, contents: 3, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(92.15693f, -185.868454f, 139.7338f, 1f)), name: "Grate 4576960 #3023")
					.WithMesh(new float3[] { new float3(0.0170897115f, 0.390328974f, 0.07701795f), new float3(-0.0139512643f, 0.352203965f, 0.09917358f), new float3(-0.0139512643f, -0.410296023f, 0.09917358f), new float3(0.1412201f, -0.295921028f, -0.0115806591f), new float3(0.1412201f, 0.237829f, -0.0115806591f), new float3(-0.102590024f, 0.352203965f, -0.0249188188f), new float3(-0.07152037f, 0.390328974f, -0.04703431f), new float3(0.05272468f, 0.237829f, -0.135472372f), new float3(0.05272468f, -0.295921028f, -0.135472372f), new float3(-0.102590024f, -0.410296023f, -0.0249188188f) }, new int[] { 0, 1, 2, 3, 4, -1, 5, 6, 7, 8, 9, -1, 1, 5, 9, 2, -1, 6, 0, 4, 7, -1, 3, 8, 7, 4, -1, 8, 3, 2, 9, -1, 0, 6, 5, 1, -1 }))
				;
		}

		// bm_c4a3c2, Invisible: the exact CSG made a triangle the mesh does not have; Invisible 4135611 #1195 face -1 near
		// (131.76, -43.463, 181.17) (4 brushes)
		static ContentsScene BmC4a3c2Invisible4Site1()
		{
			return new ContentsScene()
				.WithContentsList("Solid", "Water", "Glass", "Grate")
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 1f, 0f, -0.33359468f),
					new float4(0f, -1f, 0f, -0.200155333f),
					new float4(-0.8944272f, 0f, 0.4472136f, -2.36994219f),
					new float4(0.5547002f, 0f, -0.8320503f, -2.315703f),
					new float4(0.9647638f, 0f, 0.2631174f, -3.04954648f),
					new float4(-0.274698973f, 0f, -0.961530268f, -2.15237212f),
					new float4(-0.51449573f, 0f, -0.8574929f, -2.34072852f),
					new float4(0.371390671f, 0f, 0.9284767f, -3.476112f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(131.18811f, -43.5673447f, 183.571869f, 1f)), name: "Invisible 1597700 #1108")
					.WithMesh(new float3[] { new float3(-0.64811033f, 0.33359468f, 4.00313139f), new float3(2.40188956f, 0.33359468f, 2.78313136f), new float3(3.31688952f, 0.33359468f, -0.5718688f), new float3(0.57188946f, 0.33359468f, -2.40186882f), new float3(-1.56294119f, 0.33359468f, -1.79197037f), new float3(-3.08811045f, 0.33359468f, -0.8768689f), new float3(2.40188956f, -0.200155333f, 2.78313136f), new float3(-0.64811033f, -0.200155333f, 4.00313139f), new float3(-3.08811045f, -0.200155333f, -0.8768689f), new float3(-1.56294119f, -0.200155333f, -1.79197037f), new float3(0.57188946f, -0.200155333f, -2.40186882f), new float3(3.31688952f, -0.200155333f, -0.5718688f) }, new int[] { 0, 1, 2, 3, 4, 5, -1, 6, 7, 8, 9, 10, 11, -1, 7, 0, 5, 8, -1, 10, 3, 2, 11, -1, 1, 6, 11, 2, -1, 9, 4, 3, 10, -1, 4, 9, 8, 5, -1, 6, 1, 0, 7, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 1f, 0f, -0.190625608f),
					new float4(0f, -1f, 0f, -0.114374392f),
					new float4(0.371390671f, 0f, 0.9284767f, -2.77051139f),
					new float4(0.554700136f, -0.000338562124f, -0.832050264f, -2.69995046f),
					new float4(0.9647638f, 0f, 0.2631174f, -2.60930586f),
					new float4(-0.274675727f, 0f, -0.961536944f, -2.852392f),
					new float4(-0.8944272f, 0f, 0.4472136f, -2.33013129f),
					new float4(-0.514590442f, 0f, -0.8574361f, -3.04056025f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(131.467667f, -43.5006256f, 184.22f, 1f)), name: "Invisible 4135611 #1195")
					.WithMesh(new float3[] { new float3(-1.84304833f, 0.190625608f, -2.44000125f), new float3(-3.36766672f, 0.190625608f, -1.52500129f), new float3(-0.927666664f, 0.190625608f, 3.35499883f), new float3(2.12233353f, 0.190625608f, 2.1349988f), new float3(3.037362f, 0.190625608f, -1.22010636f), new float3(0.292463779f, 0.190625608f, -3.05003834f), new float3(-3.36766672f, -0.114374392f, -1.52500129f), new float3(-1.84304833f, -0.114374392f, -2.44000125f), new float3(0.292333454f, -0.114374392f, -3.05000114f), new float3(3.03733349f, -0.114374392f, -1.22000134f), new float3(2.12233353f, -0.114374392f, 2.1349988f), new float3(-0.927666664f, -0.114374392f, 3.35499883f) }, new int[] { 0, 1, 2, 3, 4, 5, -1, 6, 7, 8, 9, 10, 11, -1, 2, 11, 10, 3, -1, 8, 5, 4, 9, -1, 9, 4, 3, 10, -1, 7, 0, 5, 8, -1, 1, 6, 11, 2, -1, 6, 1, 0, 7, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 1f, 0f, -0.09531158f),
					new float4(0f, -1f, 0f, -0.057188414f),
					new float4(-0.8944272f, 0f, 0.4472136f, -2.36996937f),
					new float4(0.5547002f, 0f, -0.8320503f, -2.315686f),
					new float4(0.9647638f, 0f, 0.2631174f, -3.049517f),
					new float4(-0.2747433f, 0f, -0.961517632f, -2.152325f),
					new float4(-0.51449573f, 0f, -0.8574929f, -2.34074426f),
					new float4(0.371390671f, 0f, 0.9284767f, -3.47610044f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(131.188141f, -43.40531f, 183.571869f, 1f)), name: "Invisible 4135632 #1196")
					.WithMesh(new float3[] { new float3(-0.6481408f, 0.09531158f, 4.003131f), new float3(2.401859f, 0.09531158f, 2.78313112f), new float3(3.316859f, 0.09531158f, -0.571868837f), new float3(0.5718588f, 0.09531158f, -2.40186882f), new float3(-1.56331015f, 0.09531158f, -1.79176736f), new float3(-3.088141f, 0.09531158f, -0.876869f), new float3(2.401859f, -0.057188414f, 2.78313112f), new float3(-0.6481408f, -0.057188414f, 4.003131f), new float3(-3.088141f, -0.057188414f, -0.876869f), new float3(-1.56331015f, -0.057188414f, -1.79176736f), new float3(0.5718588f, -0.057188414f, -2.40186882f), new float3(3.316859f, -0.057188414f, -0.571868837f) }, new int[] { 0, 1, 2, 3, 4, 5, -1, 6, 7, 8, 9, 10, 11, -1, 7, 0, 5, 8, -1, 10, 3, 2, 11, -1, 1, 6, 11, 2, -1, 9, 4, 3, 10, -1, 4, 9, 8, 5, -1, 6, 1, 0, 7, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 1f, 0f, -0.190625757f),
					new float4(0f, -1f, 0f, -0.114374235f),
					new float4(0.371390671f, 0f, 0.9284767f, -2.77051139f),
					new float4(0.5547002f, 0f, -0.8320503f, -2.69991183f),
					new float4(0.9647638f, 0f, 0.2631174f, -2.60930586f),
					new float4(-0.274675727f, -0.000167648759f, -0.9615369f, -2.852411f),
					new float4(-0.8944272f, 0f, 0.4472136f, -2.33013129f),
					new float4(-0.51449573f, 0f, -0.8574929f, -3.040328f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(131.467667f, -43.6531258f, 184.22f, 1f)), name: "Invisible 5662194 #1306")
					.WithMesh(new float3[] { new float3(-1.84215093f, 0.190625757f, -2.44031072f), new float3(-3.36766672f, 0.190625757f, -1.52500141f), new float3(-0.927666664f, 0.190625757f, 3.35499883f), new float3(2.12233353f, 0.190625757f, 2.1349988f), new float3(3.03733349f, 0.190625757f, -1.22000122f), new float3(0.2922774f, 0.190625757f, -3.05003858f), new float3(-3.36766672f, -0.114374235f, -1.52500141f), new float3(-1.84232008f, -0.114374235f, -2.44020939f), new float3(0.292333245f, -0.114374235f, -3.05000114f), new float3(3.03733349f, -0.114374235f, -1.22000122f), new float3(2.12233353f, -0.114374235f, 2.1349988f), new float3(-0.927666664f, -0.114374235f, 3.35499883f) }, new int[] { 0, 1, 2, 3, 4, 5, -1, 6, 7, 8, 9, 10, 11, -1, 2, 11, 10, 3, -1, 8, 5, 4, 9, -1, 9, 4, 3, 10, -1, 7, 0, 5, 8, -1, 1, 6, 11, 2, -1, 6, 1, 0, 7, -1 }))
				;
		}

		// bm_c4a3c2, func_brush 4540697: the mesh has a triangle the exact CSG did not make; Grate 4540700 #1900 face -1 near
		// (73.168, -238.091, 142.628) (3 brushes)
		static ContentsScene BmC4a3c2FuncBrush454069758Site1()
		{
			return new ContentsScene()
				.WithContentsList("Solid", "Water", "Glass", "Grate")
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 1f, 0f, -0.0603576675f),
					new float4(-0.5004486f, 1.14675657E-13f, 0.8657663f, -0.07575602f),
					new float4(0.5000112f, -0.0005045686f, -0.8660188f, -0.0762182f),
					new float4(0.6126906f, -0.7067896f, 0.3536364f, -0.75064677f),
					new float4(-0.612350643f, -0.7071823f, -0.353440166f, -0.8895053f),
					new float4(0f, -1f, 0f, -0.130267337f),
					new float4(-0.6126582f, 0.706827f, -0.3536177f, -0.9213972f),
					new float4(0.6126906f, 0.7067896f, 0.3536364f, -0.782074451f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(74.14446f, -237.960358f, 143.191742f, 1f)), name: "Solid 4540698 #1898")
					.WithMesh(new float3[] { new float3(0.867047369f, 0.0603576675f, 0.588690758f), new float3(0.9432973f, 0.0603576675f, 0.4565843f), new float3(-1.03767622f, 0.0603576675f, -0.6871657f), new float3(-1.11334872f, 0.0603576675f, -0.5560597f), new float3(0.90002507f, 0.0222326983f, 0.6077533f), new float3(-1.14633f, 0.02223268f, -0.575124264f), new float3(-1.01427233f, -0.130267337f, -0.498789519f), new float3(0.76811403f, -0.130267337f, 0.53150326f), new float3(0.9762751f, 0.0222326983f, 0.4756468f), new float3(0.8442871f, -0.130267337f, 0.399530053f), new float3(-0.938619f, -0.130267337f, -0.629862249f), new float3(-1.07067668f, 0.0222326778f, -0.706196845f) }, new int[] { 0, 1, 2, 3, -1, 4, 0, 3, 5, 6, 7, -1, 1, 8, 9, 10, 11, 2, -1, 8, 4, 7, 9, -1, 5, 11, 10, 6, -1, 6, 10, 9, 7, -1, 11, 5, 3, 2, -1, 4, 8, 1, 0, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(-0.500002861f, 0f, 0.8660238f, -0.0381704159f),
					new float4(0.500002861f, 0f, -0.8660238f, -0.038180504f),
					new float4(0f, 1f, 0f, -0.324063122f),
					new float4(-0.866406739f, 0f, -0.499338955f, -0.914999f),
					new float4(0.866406739f, 0f, 0.499338955f, -1.14373863f),
					new float4(0f, -1f, 0f, -0.3240619f),
				}, contents: 3, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(73.94113f, -238.414688f, 143.1183f, 1f)), name: "Grate 4540699 #1899")
					.WithMesh(new float3[] { new float3(-0.8114711f, -0.3240619f, -0.424431115f), new float3(0.971445143f, -0.3240619f, 0.6049439f), new float3(0.971445143f, 0.324063122f, 0.6049439f), new float3(-0.8114711f, 0.324063122f, -0.424431115f), new float3(1.00957012f, -0.3240619f, 0.538792849f), new float3(-0.773346066f, -0.3240619f, -0.490582079f), new float3(-0.773346066f, 0.324063122f, -0.490582079f), new float3(1.00957012f, 0.324063122f, 0.538792849f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 2, 7, 6, 3, -1, 5, 0, 3, 6, -1, 1, 4, 7, 2, -1, 4, 1, 0, 5, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(-0.500004232f, 0f, 0.866022944f, -0.07263024f),
					new float4(0.5004486f, 0f, -0.8657663f, -0.07986691f),
					new float4(-0.866087f, 0f, -0.499893248f, -0.06898147f),
					new float4(0.612350643f, 0.7071823f, 0.353440166f, -0.254187047f),
					new float4(0.866087f, 0f, 0.499893248f, -0.121641912f),
					new float4(0.445600867f, -0.857491f, 0.257194549f, -0.316341579f),
					new float4(-0.6126906f, 0.7067896f, -0.3536364f, -0.2977272f),
				}, contents: 3, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(73.06062f, -238.328445f, 142.570145f, 1f)), name: "Grate 4540700 #1900")
					.WithMesh(new float3[] { new float3(-0.06305937f, 0.39032042f, 0.0474586636f), new float3(-0.0960469f, 0.3521954f, 0.0284130834f), new float3(-0.0960469f, -0.410304546f, 0.0284130834f), new float3(0.06903732f, -0.295929551f, 0.123725586f), new float3(0.06903732f, 0.237820446f, 0.123725586f), new float3(-0.019796906f, 0.3521954f, -0.103693388f), new float3(0.0131808594f, 0.39032042f, -0.0846308842f), new float3(0.145238414f, 0.237820446f, -0.008296172f), new float3(0.145238414f, -0.295929551f, -0.008296172f), new float3(-0.019796906f, -0.410304546f, -0.103693388f) }, new int[] { 0, 1, 2, 3, 4, -1, 5, 6, 7, 8, 9, -1, 1, 5, 9, 2, -1, 6, 0, 4, 7, -1, 3, 8, 7, 4, -1, 8, 3, 2, 9, -1, 0, 6, 5, 1, -1 }))
				;
		}

		// bm_c4a3c2, Details: the exact CSG made a triangle the mesh does not have; Solid 5499360 #2343 face -1 near (134.94,
		// -11.317, 182.065) (2 brushes)
		static ContentsScene BmC4a3c2Details83Site1()
		{
			return new ContentsScene()
				.WithContentsList("Solid", "Water", "Glass", "Grate")
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0.1361247f, 0.9873071f, 0.08182209f, -0.161020249f),
					new float4(-0.1361247f, -0.9873071f, -0.08182209f, -0.102467321f),
					new float4(0.5549074f, 0f, -0.8319121f, -0.209457949f),
					new float4(-0.5549074f, 0f, 0.8319121f, -0.16755037f),
					new float4(0.857083559f, 0f, 0.5151774f, -1.06650114f),
					new float4(-0.857083559f, 0f, -0.5151774f, -1.06649876f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(134.137817f, -11.1303558f, 181.329132f, 1f)), name: "Solid 5499360 #2343")
					.WithMesh(new float3[] { new float3(-0.780188143f, 0.334652632f, -0.7721851f), new float3(-0.9746294f, 0.334652662f, -0.448699653f), new float3(0.8018039f, -0.008472343f, 0.736228347f), new float3(0.996245146f, -0.008472345f, 0.4127429f), new float3(-0.9746294f, 0.067777656f, -0.448699653f), new float3(-0.780188143f, 0.06777765f, -0.7721851f), new float3(0.996245146f, -0.275347352f, 0.4127429f), new float3(0.8018039f, -0.275347352f, 0.736228347f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 5, 0, 3, 6, -1, 1, 4, 7, 2, -1, 2, 7, 6, 3, -1, 4, 1, 0, 5, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 1f, 0f, -0.9488889f),
					new float4(0f, -1f, 0f, -1.49111116f),
					new float4(0.242028236f, 0f, 0.9702692f, -0.1482343f),
					new float4(-0.242052943f, 0f, -0.970263064f, -0.118576549f),
					new float4(0.9702245f, 0f, -0.242207274f, -0.154624313f),
					new float4(-0.857094467f, 0f, -0.5151593f, -0.109298266f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(135.012756f, -12.0877171f, 181.894348f, 1f)), name: "Solid 5499362 #2345")
					.WithMesh(new float3[] { new float3(-0.06360382f, 0.9488889f, -0.106343381f), new float3(-0.258035749f, 0.9488889f, 0.217142031f), new float3(0.18593064f, 0.9488889f, 0.1063971f), new float3(0.121306159f, 0.9488889f, -0.152473152f), new float3(-0.258035749f, -1.49111116f, 0.217142031f), new float3(-0.06360382f, -1.49111116f, -0.106343381f), new float3(0.121306159f, -1.49111116f, -0.152473152f), new float3(0.18593064f, -1.49111116f, 0.1063971f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 1, 4, 7, 2, -1, 5, 0, 3, 6, -1, 6, 3, 2, 7, -1, 0, 5, 4, 1, -1 }))
				;
		}

		// bm_c4a3c, Details: the mesh draws nothing where the exact CSG has a face; Solid 6742952 #1391 face 2 near (-132.98,
		// -256.5, 9.226) (3 brushes)
		static ContentsScene BmC4a3cDetails88Site1()
		{
			return new ContentsScene()
				.WithContentsList("Solid", "Water", "Glass", "Grate")
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 1f, 0f, -2.236676f),
					new float4(0f, -1f, 0f, -1.423324f),
					new float4(1f, 0f, 0f, -4.74444151f),
					new float4(-1f, 0f, 0f, -3.79555845f),
					new float4(0f, 0.316227764f, -0.9486833f, -4.95107555f),
					new float4(0f, 0f, 1f, -5.28666639f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(-137.724442f, -258.436676f, 6.91333342f, 1f)), name: "Solid 6742952 #1391")
					.WithMesh(new float3[] { new float3(-3.79555845f, 2.236676f, 5.28666639f), new float3(4.74444151f, 2.236676f, 5.28666639f), new float3(4.74444151f, 2.236676f, -4.473333f), new float3(-3.79555845f, 2.236676f, -4.473333f), new float3(4.74444151f, -1.423324f, 5.28666639f), new float3(-3.79555845f, -1.423324f, 5.28666639f), new float3(-3.79555845f, -1.423324f, -5.693333f), new float3(4.74444151f, -1.423324f, -5.693333f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 1, 4, 7, 2, -1, 5, 0, 3, 6, -1, 6, 3, 2, 7, -1, 0, 5, 4, 1, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, -1f, 0f, -0.830278933f),
					new float4(-1f, 0f, 0f, -2.9822278f),
					new float4(1f, 0f, 0f, -3.72777224f),
					new float4(-0.178885445f, 0f, -0.9838699f, -0.07502097f),
					new float4(0.178885445f, 0f, 0.9838699f, -0.07501919f),
					new float4(0f, 1f, 0f, -1.30472112f),
				}, contents: 2, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(-129.997772f, -255.979721f, 8.684028f, 1f)), name: "Glass 4017842 #1457")
					.WithMesh(new float3[] { new float3(3.72777224f, -0.830278933f, -0.6015277f), new float3(-2.9822278f, -0.830278933f, 0.618472338f), new float3(-2.9822278f, -0.830278933f, 0.465972364f), new float3(3.72777224f, -0.830278933f, -0.7540277f), new float3(-2.9822278f, 1.30472112f, 0.618472338f), new float3(-2.9822278f, 1.30472112f, 0.465972364f), new float3(3.72777224f, 1.30472112f, -0.7540277f), new float3(3.72777224f, 1.30472112f, -0.6015277f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 2, 1, -1, 6, 7, 0, 3, -1, 5, 6, 3, 2, -1, 7, 4, 1, 0, -1, 4, 7, 6, 5, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, -1f, 0f, -0.830278933f),
					new float4(-1f, 0f, 0f, -0.271106571f),
					new float4(1f, 0f, 0f, -0.338893443f),
					new float4(0f, 0f, -1f, -0.0762497f),
					new float4(0f, 0f, 1f, -0.07625031f),
					new float4(0f, 1f, 0f, -1.30472112f),
				}, contents: 2, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(-133.3189f, -255.979721f, 9.22625f, 1f)), name: "Glass 4017892 #1459")
					.WithMesh(new float3[] { new float3(0.338893443f, -0.830278933f, 0.07625031f), new float3(-0.271106571f, -0.830278933f, 0.07625031f), new float3(-0.271106571f, -0.830278933f, -0.0762497f), new float3(0.338893443f, -0.830278933f, -0.0762497f), new float3(-0.271106571f, 1.30472112f, 0.07625031f), new float3(-0.271106571f, 1.30472112f, -0.0762497f), new float3(0.338893443f, 1.30472112f, -0.0762497f), new float3(0.338893443f, 1.30472112f, 0.07625031f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 2, 1, -1, 6, 7, 0, 3, -1, 5, 6, 3, 2, -1, 7, 4, 1, 0, -1, 4, 7, 6, 5, -1 }))
				;
		}

		// c2a5h_sky, Model: the mesh has a triangle the exact CSG did not make; Grate 71303 #1621 face -1 near (-0.763,
		// -0.23, 0.248) (5 brushes)
		static ContentsScene C2a5hSkyModel2Site1()
		{
			return new ContentsScene()
				.WithContentsList("Solid", "Water", "Glass", "Grate")
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, -1f, 0f, -0.1482639f),
					new float4(-1f, 0f, 0f, -0.677777767f),
					new float4(1f, 0f, 0f, -0.8472222f),
					new float4(0f, 0f, 1f, -0.305f),
					new float4(0f, 0f, -1f, -0.305f),
					new float4(0f, 1f, 0f, -0.232986107f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 0.99999994f, 2.13162821E-14f, 0f), new float4(0f, -2.13162821E-14f, 0.99999994f, 0f), new float4(-0.08472252f, -0.156733483f, -0.0381298177f, 1f)), name: "Solid 6109 #668")
					.WithMesh(new float3[] { new float3(-0.677777767f, -0.1482639f, -0.305f), new float3(0.8472222f, -0.1482639f, -0.305f), new float3(0.8472222f, -0.1482639f, 0.305f), new float3(-0.677777767f, -0.1482639f, 0.305f), new float3(-0.677777767f, 0.232986107f, 0.305f), new float3(-0.677777767f, 0.232986107f, -0.305f), new float3(0.8472222f, 0.232986107f, -0.305f), new float3(0.8472222f, 0.232986107f, 0.305f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 0, 3, -1, 6, 7, 2, 1, -1, 7, 4, 3, 2, -1, 5, 6, 1, 0, -1, 6, 5, 4, 7, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, -1f, 0f, -0.018532984f),
					new float4(1f, 0f, 0f, -0.0105903056f),
					new float4(-1f, 0f, 0f, -0.008472195f),
					new float4(0.707106769f, 0f, 0.707106769f, -0.04830057f),
					new float4(0f, 1f, 0f, -0.0291232653f),
					new float4(0f, 0f, -1f, -0.08644314f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 0.99999994f, 2.13162821E-14f, 0f), new float4(0f, -2.13162821E-14f, 0.99999994f, 0f), new float4(-3.03914452f, -0.2864637f, 0.190092012f, 1f)), name: "Solid 8079 #1219")
					.WithMesh(new float3[] { new float3(-0.008472195f, -0.018532984f, -0.08644314f), new float3(0.0105903056f, -0.018532984f, -0.08644314f), new float3(0.0105903056f, -0.018532984f, 0.0577170178f), new float3(-0.008472195f, -0.018532984f, 0.07677952f), new float3(0.0105903056f, 0.0291232653f, -0.08644314f), new float3(0.0105903056f, 0.0291232653f, 0.0577170178f), new float3(-0.008472195f, 0.0291232653f, -0.08644314f), new float3(-0.008472195f, 0.0291232653f, 0.07677952f) }, new int[] { 0, 1, 2, 3, -1, 1, 4, 5, 2, -1, 6, 0, 3, 7, -1, 5, 7, 3, 2, -1, 4, 6, 7, 5, -1, 6, 4, 1, 0, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, -1f, 0f, -0.0213129371f),
					new float4(0f, 0f, 1f, -0.00953126047f),
					new float4(0f, 0f, -1f, -0.009531239f),
					new float4(0f, 1f, 0f, -0.03349175f),
					new float4(1f, 0f, 0f, -1.26315522f),
					new float4(-0.707106769f, 0f, -0.707106769f, -0.7158966f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 0.99999994f, 2.13162821E-14f, 0f), new float4(0f, -2.13162821E-14f, 0.99999994f, 0f), new float4(-2.02565575f, -0.2908325f, 0.257337481f, 1f)), name: "Solid 10549 #1444")
					.WithMesh(new float3[] { new float3(-1.02196193f, -0.0213129371f, 0.00953126047f), new float3(-1.00289953f, -0.0213129371f, -0.009531239f), new float3(1.26315522f, -0.0213129371f, -0.009531239f), new float3(1.26315522f, -0.0213129371f, 0.00953126047f), new float3(-1.02196193f, 0.03349175f, 0.00953126047f), new float3(1.26315522f, 0.03349175f, 0.00953126047f), new float3(-1.00289953f, 0.03349175f, -0.009531239f), new float3(1.26315522f, 0.03349175f, -0.009531239f) }, new int[] { 0, 1, 2, 3, -1, 4, 0, 3, 5, -1, 1, 6, 7, 2, -1, 6, 4, 5, 7, -1, 7, 5, 3, 2, -1, 4, 6, 1, 0, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, -1f, 0f, -0.0378921032f),
					new float4(1f, 0f, 0f, -0.0105903056f),
					new float4(-1f, 0f, 0f, -0.008472195f),
					new float4(0.707106769f, 0f, 0.707106769f, -0.0483006835f),
					new float4(0f, 1f, 0f, -0.059544716f),
					new float4(0f, 0f, -1f, -0.08644336f),
				}, contents: 3, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 0.99999994f, 2.13162821E-14f, 0f), new float4(0f, -2.13162821E-14f, 0.99999994f, 0f), new float4(-3.03914452f, -0.219180077f, 0.189695284f, 1f)), name: "Grate 71302 #1620")
					.WithMesh(new float3[] { new float3(-0.008472195f, -0.0378921032f, -0.08644336f), new float3(0.0105903056f, -0.0378921032f, -0.08644336f), new float3(0.0105903056f, -0.0378921032f, 0.057717178f), new float3(-0.008472195f, -0.0378921032f, 0.07677968f), new float3(0.0105903056f, 0.059544716f, -0.08644336f), new float3(0.0105903056f, 0.059544716f, 0.057717178f), new float3(-0.008472195f, 0.059544716f, -0.08644336f), new float3(-0.008472195f, 0.059544716f, 0.07677968f) }, new int[] { 0, 1, 2, 3, -1, 1, 4, 5, 2, -1, 6, 0, 3, 7, -1, 5, 7, 3, 2, -1, 4, 6, 7, 5, -1, 6, 4, 1, 0, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, -1f, 0f, -0.0435757861f),
					new float4(0f, 0f, 1f, -0.009531241f),
					new float4(0f, 0f, -1f, -0.009531259f),
					new float4(0f, 1f, 0f, -0.0684762448f),
					new float4(1f, 0f, 0f, -1.26315522f),
					new float4(-0.707106769f, 0f, -0.707106769f, -0.7158966f),
				}, contents: 3, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 0.99999994f, 2.13162821E-14f, 0f), new float4(0f, -2.13162821E-14f, 0.99999994f, 0f), new float4(-2.02565575f, -0.228111237f, 0.256944567f, 1f)), name: "Grate 71303 #1621")
					.WithMesh(new float3[] { new float3(-1.02196193f, -0.0435757861f, 0.009531241f), new float3(-1.00289941f, -0.0435757861f, -0.009531259f), new float3(1.26315522f, -0.0435757861f, -0.009531259f), new float3(1.26315522f, -0.0435757861f, 0.009531241f), new float3(-1.02196193f, 0.0684762448f, 0.009531241f), new float3(1.26315522f, 0.0684762448f, 0.009531241f), new float3(-1.00289941f, 0.0684762448f, -0.009531259f), new float3(1.26315522f, 0.0684762448f, -0.009531259f) }, new int[] { 0, 1, 2, 3, -1, 4, 0, 3, 5, -1, 1, 6, 7, 2, -1, 6, 4, 5, 7, -1, 7, 5, 3, 2, -1, 4, 6, 1, 0, -1 }))
				;
		}

		// c2a5h_sky, Model: the mesh has a triangle the exact CSG did not make; Solid 7389 #883 face -1 near (3.768, 0.343,
		// -3.044) (2 brushes)
		static ContentsScene C2a5hSkyModel2Site2()
		{
			return new ContentsScene()
				.WithContentsList("Solid", "Water", "Glass", "Grate")
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(-0.707106769f, -2.69739826E-06f, 0.707106769f, -0.0381244458f),
					new float4(0.7070576f, 0f, -0.707155943f, -0.0381201729f),
					new float4(-0.9249832f, -0.0007463616f, -0.3800073f, -0.06461905f),
					new float4(0.3829895f, -9.49642854E-05f, 0.923752666f, -0.0808870643f),
					new float4(0f, -1f, 0f, -0.0296527818f),
					new float4(0f, 1f, 0f, -0.04659722f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 0.99999994f, 2.13162821E-14f, 0f), new float4(0f, -2.13162821E-14f, 0.99999994f, 0f), new float4(3.78867722f, 0.3346543f, -3.07673383f, 1f)), name: "Solid 7389 #883")
					.WithMesh(new float3[] { new float3(0.0237890668f, 0.04659722f, 0.07770535f), new float3(-0.0652437f, 0.04659722f, -0.0113274138f), new float3(-0.06520001f, -0.0296527818f, -0.0112840105f), new float3(0.02378373f, -0.0296527818f, 0.07769973f), new float3(-0.03384762f, 0.04659722f, -0.08774923f), new float3(0.100020073f, 0.04659722f, 0.04609984f), new float3(0.10001453f, -0.0296527818f, 0.0460943021f), new float3(-0.0338040069f, -0.0296527818f, -0.08770563f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 1, 4, 7, 2, -1, 5, 0, 3, 6, -1, 6, 3, 2, 7, -1, 0, 5, 4, 1, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(-0.7071312f, 1.806398E-06f, 0.7070824f, -0.03813205f),
					new float4(0.7073587f, -0.000117013507f, -0.7068547f, -0.038135156f),
					new float4(0.382991254f, -0.000216983288f, 0.92375195f, -0.0108546149f),
					new float4(0f, 1f, 0f, -0.06989584f),
					new float4(0f, -1f, 0f, -0.0444791541f),
					new float4(-0.707106769f, 0.000118685522f, -0.707106769f, -0.009995575f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 0.99999994f, 2.13162821E-14f, 0f), new float4(0f, -2.13162821E-14f, 0.99999994f, 0f), new float4(3.84230232f, 0.425730169f, -3.02311826f, 1f)), name: "Solid 7397 #891")
					.WithMesh(new float3[] { new float3(-0.03402517f, 0.06989584f, 0.0199010242f), new float3(-0.0340349153f, -0.0444791541f, 0.0198915713f), new float3(-0.0298222341f, -0.0444791541f, 0.024104543f), new float3(-0.0298030358f, 0.06989584f, 0.0241234489f), new float3(0.0198927987f, -0.0444791541f, -0.03403614f), new float3(0.0199118573f, 0.06989584f, -0.0340360031f), new float3(0.04644122f, 0.06989584f, -0.007487724f), new float3(0.04640886f, -0.0444791541f, -0.00750117376f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 2, 7, 6, 3, -1, 5, 0, 3, 6, -1, 1, 4, 7, 2, -1, 4, 1, 0, 5, -1 }))
				;
		}

		// c2a5h_sky, Model: the mesh has a triangle the exact CSG did not make; Solid 7391 #885 face -1 near (3.498, 0.343,
		// -2.774) (3 brushes)
		static ContentsScene C2a5hSkyModel2Site3()
		{
			return new ContentsScene()
				.WithContentsList("Solid", "Water", "Glass", "Grate")
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(-0.12058986f, 0.9927024f, 0f, -0.0789714754f),
					new float4(-0.3857729f, 0.9225938f, 5.5668148E-05f, -0.06609542f),
					new float4(-0.594925165f, 0.8037811f, -3.276966E-05f, -0.0554574132f),
					new float4(-0.8036884f, 0.595050335f, 0.000137701863f, -0.0439542755f),
					new float4(-0.92228955f, 0.386499584f, 7.590223E-05f, -0.0364802666f),
					new float4(-0.992740035f, 0.120279625f, 0f, -0.0308101885f),
					new float4(1f, 0f, 0f, -0.04629595f),
					new float4(-1f, 0f, 0f, -0.0299540516f),
					new float4(0.384103924f, -0.000554139144f, 0.9232897f, -0.07101472f),
					new float4(0.387028754f, 0.00226264633f, -0.9220649f, -0.08091297f),
					new float4(0f, -1f, 0f, -0.200761616f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 0.99999994f, 2.13162821E-14f, 0f), new float4(0f, -2.13162821E-14f, 0.99999994f, 0f), new float4(3.46120453f, 0.505763233f, -2.93045187f, 1f)), name: "Solid 7382 #876")
					.WithMesh(new float3[] { new float3(0.04629595f, 0.08517587f, -0.06811059f), new float3(0.02665156f, 0.08278955f, -0.07636201f), new float3(0.0266804863f, 0.08279306f, 0.06586504f), new float3(0.04629595f, 0.08517587f, 0.0577061139f), new float3(0.008240841f, 0.0750917643f, -0.0841086358f), new float3(0.008191339f, 0.07506155f, 0.0735522f), new float3(-0.008019426f, 0.06305631f, -0.0909632742f), new float3(-0.007943086f, 0.0631198f, 0.08025722f), new float3(-0.0198104121f, 0.0471323f, -0.09595152f), new float3(-0.0198165383f, 0.0470821075f, 0.0851871446f), new float3(-0.0275676772f, 0.0286220759f, -0.0992529839f), new float3(-0.027573958f, 0.0285702348f, 0.0884032547f), new float3(-0.0299540516f, 0.008925887f, -0.100302972f), new float3(-0.0299540516f, 0.008925887f, 0.08938162f), new float3(0.04629595f, -0.200761616f, 0.0575345f), new float3(0.04629595f, -0.200761616f, -0.06881225f), new float3(-0.0299540516f, -0.200761616f, -0.100817524f), new float3(-0.0299540516f, -0.200761616f, 0.08925577f) }, new int[] { 0, 1, 2, 3, -1, 1, 4, 5, 2, -1, 4, 6, 7, 5, -1, 6, 8, 9, 7, -1, 8, 10, 11, 9, -1, 10, 12, 13, 11, -1, 14, 15, 0, 3, -1, 16, 17, 13, 12, -1, 17, 14, 3, 2, 5, 7, 9, 11, 13, -1, 15, 16, 12, 10, 8, 6, 4, 1, 0, -1, 16, 15, 14, 17, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0.707032263f, -2.809072E-06f, -0.7071813f, -0.0381243154f),
					new float4(-0.707106769f, 0.000178028291f, 0.707106769f, -0.03812818f),
					new float4(0.923123956f, 0.0010780131f, 0.384501f, -0.08134313f),
					new float4(-0.383386374f, 0.0004615403f, -0.9235879f, -0.06503973f),
					new float4(0f, -1f, 0f, -0.0296527818f),
					new float4(0f, 1f, 0f, -0.04659722f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 0.99999994f, 2.13162821E-14f, 0f), new float4(0f, -2.13162821E-14f, 0.99999994f, 0f), new float4(3.51905823f, 0.334654063f, -2.80718374f, 1f)), name: "Solid 7391 #885")
					.WithMesh(new float3[] { new float3(-0.011652464f, 0.04659722f, -0.06556044f), new float3(0.0780253038f, 0.04659722f, 0.0240984317f), new float3(0.0780880749f, -0.0296527818f, 0.0241614971f), new float3(-0.0116796093f, -0.0296527818f, -0.0655872747f), new float3(0.0463164747f, 0.04659722f, 0.100226134f), new float3(-0.08784299f, 0.04659722f, -0.0339333341f), new float3(-0.08788349f, -0.0296527818f, -0.03395463f), new float3(0.04637369f, -0.0296527818f, 0.100302547f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 1, 4, 7, 2, -1, 5, 0, 3, 6, -1, 6, 3, 2, 7, -1, 0, 5, 4, 1, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0.705533f, 1.04741082E-06f, -0.708677f, -0.038104862f),
					new float4(-0.707106769f, -5.899269E-18f, 0.707106769f, -0.038119413f),
					new float4(-0.383351415f, 0.000435412f, -0.923602462f, -0.00866169f),
					new float4(0f, 1f, 0f, -0.06989584f),
					new float4(0f, -1f, 0f, -0.0444791541f),
					new float4(0.707232654f, 0f, 0.7069809f, -0.0124766007f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 0.99999994f, 2.13162821E-14f, 0f), new float4(0f, -2.13162821E-14f, 0.99999994f, 0f), new float4(3.47595024f, 0.42572993f, -2.85028982f, 1f)), name: "Solid 7394 #888")
					.WithMesh(new float3[] { new float3(0.0357813127f, 0.06989584f, -0.0181463361f), new float3(0.0357814f, -0.0444791541f, -0.01814642f), new float3(0.0314541161f, -0.0444791541f, -0.0224545058f), new float3(0.03149222f, 0.06989584f, -0.0224164017f), new float3(-0.0181274079f, -0.0444791541f, 0.0357815847f), new float3(-0.0181274079f, 0.06989584f, 0.0357815847f), new float3(-0.0447006859f, 0.06989584f, 0.009208307f), new float3(-0.04473879f, -0.0444791541f, 0.009170203f) }, new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 2, 7, 6, 3, -1, 5, 0, 3, 6, -1, 1, 4, 7, 2, -1, 4, 1, 0, 5, -1 }))
				;
		}

		[Test] public void TheBmC1a1eModel0Site1_TheMeshIsWhatTheExactCSGMade() => ExactJudgeCaseTests.AssertTheMeshIsWhatTheExactCSGMade(BmC1a1eModel0Site1());
		[Test] public void TheBmC1a1eModel0Site2_TheMeshIsWhatTheExactCSGMade() => ExactJudgeCaseTests.AssertTheMeshIsWhatTheExactCSGMade(BmC1a1eModel0Site2());
		[Test] public void TheBmC1a1eModel0Site3_TheMeshIsWhatTheExactCSGMade() => ExactJudgeCaseTests.AssertTheMeshIsWhatTheExactCSGMade(BmC1a1eModel0Site3());
		[Test] public void TheBmC1a1eModel0Site4_TheMeshIsWhatTheExactCSGMade() => ExactJudgeCaseTests.AssertTheMeshIsWhatTheExactCSGMade(BmC1a1eModel0Site4());
		[Test] public void TheBmC1a1eDetails31Site1_TheMeshIsWhatTheExactCSGMade() => ExactJudgeCaseTests.AssertTheMeshIsWhatTheExactCSGMade(BmC1a1eDetails31Site1());
		[Test] public void TheBmC1a4aModel0Site1_TheMeshIsWhatTheExactCSGMade() => ExactJudgeCaseTests.AssertTheMeshIsWhatTheExactCSGMade(BmC1a4aModel0Site1());
		[Test] public void TheBmC1a4aModel0Site2_TheMeshIsWhatTheExactCSGMade() => ExactJudgeCaseTests.AssertTheMeshIsWhatTheExactCSGMade(BmC1a4aModel0Site2());
		[Test] public void TheBmC1a4aModel0Site3_TheMeshIsWhatTheExactCSGMade() => ExactJudgeCaseTests.AssertTheMeshIsWhatTheExactCSGMade(BmC1a4aModel0Site3());
		[Test] public void TheBmC1a4aDetails27Site1_TheMeshIsWhatTheExactCSGMade() => ExactJudgeCaseTests.AssertTheMeshIsWhatTheExactCSGMade(BmC1a4aDetails27Site1());
		[Test] public void TheBmC1a4aDetails27Site2_TheMeshIsWhatTheExactCSGMade() => ExactJudgeCaseTests.AssertTheMeshIsWhatTheExactCSGMade(BmC1a4aDetails27Site2());
		[Test] public void TheBmC1a4aDetails27Site3_TheMeshIsWhatTheExactCSGMade() => ExactJudgeCaseTests.AssertTheMeshIsWhatTheExactCSGMade(BmC1a4aDetails27Site3());
		[Test] public void TheBmC1a4bDetails35Site1_TheMeshIsWhatTheExactCSGMade() => ExactJudgeCaseTests.AssertTheMeshIsWhatTheExactCSGMade(BmC1a4bDetails35Site1());
		[Test] public void TheBmC1a4cDetails27Site1_TheMeshIsWhatTheExactCSGMade() => ExactJudgeCaseTests.AssertTheMeshIsWhatTheExactCSGMade(BmC1a4cDetails27Site1());
		[Test] public void TheBmC1a4cDetails27Site2_TheMeshIsWhatTheExactCSGMade() => ExactJudgeCaseTests.AssertTheMeshIsWhatTheExactCSGMade(BmC1a4cDetails27Site2());
		[Test] public void TheBmC1a4dDetails30Site1_TheMeshIsWhatTheExactCSGMade() => ExactJudgeCaseTests.AssertTheMeshIsWhatTheExactCSGMade(BmC1a4dDetails30Site1());
		[Test] public void TheBmC1a4dDetails30Site2_TheMeshIsWhatTheExactCSGMade() => ExactJudgeCaseTests.AssertTheMeshIsWhatTheExactCSGMade(BmC1a4dDetails30Site2());
		[Test] public void TheBmC1a4dDetails30Site3_TheMeshIsWhatTheExactCSGMade() => ExactJudgeCaseTests.AssertTheMeshIsWhatTheExactCSGMade(BmC1a4dDetails30Site3());
		[Test] public void TheBmC1a4dDetails30Site4_TheMeshIsWhatTheExactCSGMade() => ExactJudgeCaseTests.AssertTheMeshIsWhatTheExactCSGMade(BmC1a4dDetails30Site4());
		[Test] public void TheBmC1a4dDetails30Site5_TheMeshIsWhatTheExactCSGMade() => ExactJudgeCaseTests.AssertTheMeshIsWhatTheExactCSGMade(BmC1a4dDetails30Site5());
		[Test] public void TheBmC1a4eModel0Site1_TheMeshIsWhatTheExactCSGMade() => ExactJudgeCaseTests.AssertTheMeshIsWhatTheExactCSGMade(BmC1a4eModel0Site1());
		[Test] public void TheBmC1a4eDetails226Site1_TheMeshIsWhatTheExactCSGMade() => ExactJudgeCaseTests.AssertTheMeshIsWhatTheExactCSGMade(BmC1a4eDetails226Site1());
		[Test] public void TheBmC1a4eDetails24Site1_TheMeshIsWhatTheExactCSGMade() => ExactJudgeCaseTests.AssertTheMeshIsWhatTheExactCSGMade(BmC1a4eDetails24Site1());
		[Test] public void TheBmC2a2aModel0Site1_TheMeshIsWhatTheExactCSGMade() => ExactJudgeCaseTests.AssertTheMeshIsWhatTheExactCSGMade(BmC2a2aModel0Site1());
		[Test] public void TheBmC2a2bFuncMovelinear2922Lvl4LiftCage280Site1_TheMeshIsWhatTheExactCSGMade() => ExactJudgeCaseTests.AssertTheMeshIsWhatTheExactCSGMade(BmC2a2bFuncMovelinear2922Lvl4LiftCage280Site1());
		[Test] public void TheBmC2a2bDetails64Site1_TheMeshIsWhatTheExactCSGMade() => ExactJudgeCaseTests.AssertTheMeshIsWhatTheExactCSGMade(BmC2a2bDetails64Site1());
		[Test] public void TheBmC2a2cDetails40Site1_TheMeshIsWhatTheExactCSGMade() => ExactJudgeCaseTests.AssertTheMeshIsWhatTheExactCSGMade(BmC2a2cDetails40Site1());
		[Test] public void TheBmC2a2cDetails40Site2_TheMeshIsWhatTheExactCSGMade() => ExactJudgeCaseTests.AssertTheMeshIsWhatTheExactCSGMade(BmC2a2cDetails40Site2());
		[Test] public void TheBmC2a3aModel0Site1_TheMeshIsWhatTheExactCSGMade() => ExactJudgeCaseTests.AssertTheMeshIsWhatTheExactCSGMade(BmC2a3aModel0Site1());
		[Test] public void TheBmC2a3aModel0Site2_TheMeshIsWhatTheExactCSGMade() => ExactJudgeCaseTests.AssertTheMeshIsWhatTheExactCSGMade(BmC2a3aModel0Site2());
		[Test] public void TheBmC2a3aModel0Site3_TheMeshIsWhatTheExactCSGMade() => ExactJudgeCaseTests.AssertTheMeshIsWhatTheExactCSGMade(BmC2a3aModel0Site3());
		[Test] public void TheBmC2a3aModel0Site4_TheMeshIsWhatTheExactCSGMade() => ExactJudgeCaseTests.AssertTheMeshIsWhatTheExactCSGMade(BmC2a3aModel0Site4());
		[Test] public void TheBmC2a3aModel0Site5_TheMeshIsWhatTheExactCSGMade() => ExactJudgeCaseTests.AssertTheMeshIsWhatTheExactCSGMade(BmC2a3aModel0Site5());
		[Test] public void TheBmC2a3aModel0Site6_TheMeshIsWhatTheExactCSGMade() => ExactJudgeCaseTests.AssertTheMeshIsWhatTheExactCSGMade(BmC2a3aModel0Site6());
		[Test] public void TheBmC2a3aModel0Site7_TheMeshIsWhatTheExactCSGMade() => ExactJudgeCaseTests.AssertTheMeshIsWhatTheExactCSGMade(BmC2a3aModel0Site7());
		[Test] public void TheBmC2a3aModel0Site8_TheMeshIsWhatTheExactCSGMade() => ExactJudgeCaseTests.AssertTheMeshIsWhatTheExactCSGMade(BmC2a3aModel0Site8());
		[Test] public void TheBmC2a3aModel0Site9_TheMeshIsWhatTheExactCSGMade() => ExactJudgeCaseTests.AssertTheMeshIsWhatTheExactCSGMade(BmC2a3aModel0Site9());
		[Test] public void TheBmC2a3aModel0Site10_TheMeshIsWhatTheExactCSGMade() => ExactJudgeCaseTests.AssertTheMeshIsWhatTheExactCSGMade(BmC2a3aModel0Site10());
		[Test] public void TheBmC2a3aModel0Site11_TheMeshIsWhatTheExactCSGMade() => ExactJudgeCaseTests.AssertTheMeshIsWhatTheExactCSGMade(BmC2a3aModel0Site11());
		[Test] public void TheBmC2a3aModel0Site12_TheMeshIsWhatTheExactCSGMade() => ExactJudgeCaseTests.AssertTheMeshIsWhatTheExactCSGMade(BmC2a3aModel0Site12());
		[Test] public void TheBmC2a3aModel0Site13_TheMeshIsWhatTheExactCSGMade() => ExactJudgeCaseTests.AssertTheMeshIsWhatTheExactCSGMade(BmC2a3aModel0Site13());
		[Test] public void TheBmC2a3aModel0Site14_TheMeshIsWhatTheExactCSGMade() => ExactJudgeCaseTests.AssertTheMeshIsWhatTheExactCSGMade(BmC2a3aModel0Site14());
		[Test] public void TheBmC2a3aModel0Site15_TheMeshIsWhatTheExactCSGMade() => ExactJudgeCaseTests.AssertTheMeshIsWhatTheExactCSGMade(BmC2a3aModel0Site15());
		[Test] public void TheBmC2a3bModel0Site1_TheMeshIsWhatTheExactCSGMade() => ExactJudgeCaseTests.AssertTheMeshIsWhatTheExactCSGMade(BmC2a3bModel0Site1());
		[Test] public void TheBmC2a3bModel0Site2_TheMeshIsWhatTheExactCSGMade() => ExactJudgeCaseTests.AssertTheMeshIsWhatTheExactCSGMade(BmC2a3bModel0Site2());
		[Test] public void TheBmC2a4aDetails167Site1_TheMeshIsWhatTheExactCSGMade() => ExactJudgeCaseTests.AssertTheMeshIsWhatTheExactCSGMade(BmC2a4aDetails167Site1());
		[Test] public void TheBmC2a4aDetails28Site1_TheMeshIsWhatTheExactCSGMade() => ExactJudgeCaseTests.AssertTheMeshIsWhatTheExactCSGMade(BmC2a4aDetails28Site1());
		[Test] public void TheBmC2a4aDetails28Site2_TheMeshIsWhatTheExactCSGMade() => ExactJudgeCaseTests.AssertTheMeshIsWhatTheExactCSGMade(BmC2a4aDetails28Site2());
		[Test] public void TheBmC2a4bModel0Site1_TheMeshIsWhatTheExactCSGMade() => ExactJudgeCaseTests.AssertTheMeshIsWhatTheExactCSGMade(BmC2a4bModel0Site1());
		[Test] public void TheBmC2a4bModel0Site2_TheMeshIsWhatTheExactCSGMade() => ExactJudgeCaseTests.AssertTheMeshIsWhatTheExactCSGMade(BmC2a4bModel0Site2());
		[Test] public void TheBmC2a4bModel0Site3_TheMeshIsWhatTheExactCSGMade() => ExactJudgeCaseTests.AssertTheMeshIsWhatTheExactCSGMade(BmC2a4bModel0Site3());
		[Test] public void TheBmC2a4bModel0Site4_TheMeshIsWhatTheExactCSGMade() => ExactJudgeCaseTests.AssertTheMeshIsWhatTheExactCSGMade(BmC2a4bModel0Site4());
		[Test] public void TheBmC2a4bModel0Site5_TheMeshIsWhatTheExactCSGMade() => ExactJudgeCaseTests.AssertTheMeshIsWhatTheExactCSGMade(BmC2a4bModel0Site5());
		[Test] public void TheBmC2a4eDetails19Site1_TheMeshIsWhatTheExactCSGMade() => ExactJudgeCaseTests.AssertTheMeshIsWhatTheExactCSGMade(BmC2a4eDetails19Site1());
		[Test] public void TheBmC2a4fDetails40Site1_TheMeshIsWhatTheExactCSGMade() => ExactJudgeCaseTests.AssertTheMeshIsWhatTheExactCSGMade(BmC2a4fDetails40Site1());
		[Test] public void TheBmC2a4fDetails40Site2_TheMeshIsWhatTheExactCSGMade() => ExactJudgeCaseTests.AssertTheMeshIsWhatTheExactCSGMade(BmC2a4fDetails40Site2());
		[Test] public void TheBmC2a5aDetails24Site1_TheMeshIsWhatTheExactCSGMade() => ExactJudgeCaseTests.AssertTheMeshIsWhatTheExactCSGMade(BmC2a5aDetails24Site1());
		[Test] public void TheBmC2a5aDetails24Site2_TheMeshIsWhatTheExactCSGMade() => ExactJudgeCaseTests.AssertTheMeshIsWhatTheExactCSGMade(BmC2a5aDetails24Site2());
		[Test] public void TheBmC2a5bDetails43Site1_TheMeshIsWhatTheExactCSGMade() => ExactJudgeCaseTests.AssertTheMeshIsWhatTheExactCSGMade(BmC2a5bDetails43Site1());
		[Test] public void TheBmC2a5bDetails43Site2_TheMeshIsWhatTheExactCSGMade() => ExactJudgeCaseTests.AssertTheMeshIsWhatTheExactCSGMade(BmC2a5bDetails43Site2());
		[Test] public void TheBmC2a5dDetails52Site1_TheMeshIsWhatTheExactCSGMade() => ExactJudgeCaseTests.AssertTheMeshIsWhatTheExactCSGMade(BmC2a5dDetails52Site1());
		[Test] public void TheBmC2a5dDetails52Site2_TheMeshIsWhatTheExactCSGMade() => ExactJudgeCaseTests.AssertTheMeshIsWhatTheExactCSGMade(BmC2a5dDetails52Site2());
		[Test] public void TheBmC2a5dDetails52Site3_TheMeshIsWhatTheExactCSGMade() => ExactJudgeCaseTests.AssertTheMeshIsWhatTheExactCSGMade(BmC2a5dDetails52Site3());
		[Test] public void TheBmC2a5eDetails44Site1_TheMeshIsWhatTheExactCSGMade() => ExactJudgeCaseTests.AssertTheMeshIsWhatTheExactCSGMade(BmC2a5eDetails44Site1());
		[Test] public void TheBmC2a5eDetails44Site2_TheMeshIsWhatTheExactCSGMade() => ExactJudgeCaseTests.AssertTheMeshIsWhatTheExactCSGMade(BmC2a5eDetails44Site2());
		[Test] public void TheBmC2a5eDetails44Site3_TheMeshIsWhatTheExactCSGMade() => ExactJudgeCaseTests.AssertTheMeshIsWhatTheExactCSGMade(BmC2a5eDetails44Site3());
		[Test] public void TheBmC2a5eDetails44Site4_TheMeshIsWhatTheExactCSGMade() => ExactJudgeCaseTests.AssertTheMeshIsWhatTheExactCSGMade(BmC2a5eDetails44Site4());
		[Test] public void TheBmC2a5fDetails105Site1_TheMeshIsWhatTheExactCSGMade() => ExactJudgeCaseTests.AssertTheMeshIsWhatTheExactCSGMade(BmC2a5fDetails105Site1());
		[Test] public void TheBmC2a5fDetails105Site2_TheMeshIsWhatTheExactCSGMade() => ExactJudgeCaseTests.AssertTheMeshIsWhatTheExactCSGMade(BmC2a5fDetails105Site2());
		[Test] public void TheBmC2a5gModel0Site1_TheMeshIsWhatTheExactCSGMade() => ExactJudgeCaseTests.AssertTheMeshIsWhatTheExactCSGMade(BmC2a5gModel0Site1());
		[Test] public void TheBmC2a5gModel0Site2_TheMeshIsWhatTheExactCSGMade() => ExactJudgeCaseTests.AssertTheMeshIsWhatTheExactCSGMade(BmC2a5gModel0Site2());
		[Test] public void TheBmC2a5gFuncBreakable4760975GVentBrush03106Site1_TheMeshIsWhatTheExactCSGMade() => ExactJudgeCaseTests.AssertTheMeshIsWhatTheExactCSGMade(BmC2a5gFuncBreakable4760975GVentBrush03106Site1());
		[Test] public void TheBmC2a5gDetails65Site1_TheMeshIsWhatTheExactCSGMade() => ExactJudgeCaseTests.AssertTheMeshIsWhatTheExactCSGMade(BmC2a5gDetails65Site1());
		[Test] public void TheBmC2a5gDetails65Site2_TheMeshIsWhatTheExactCSGMade() => ExactJudgeCaseTests.AssertTheMeshIsWhatTheExactCSGMade(BmC2a5gDetails65Site2());
		[Test] public void TheBmC2a5gDetails65Site3_TheMeshIsWhatTheExactCSGMade() => ExactJudgeCaseTests.AssertTheMeshIsWhatTheExactCSGMade(BmC2a5gDetails65Site3());
		[Test] public void TheBmC2a5hDetails53Site1_TheMeshIsWhatTheExactCSGMade() => ExactJudgeCaseTests.AssertTheMeshIsWhatTheExactCSGMade(BmC2a5hDetails53Site1());
		[Test] public void TheBmC2a5hDetails53Site2_TheMeshIsWhatTheExactCSGMade() => ExactJudgeCaseTests.AssertTheMeshIsWhatTheExactCSGMade(BmC2a5hDetails53Site2());
		[Test] public void TheBmC2a5hDetails53Site3_TheMeshIsWhatTheExactCSGMade() => ExactJudgeCaseTests.AssertTheMeshIsWhatTheExactCSGMade(BmC2a5hDetails53Site3());
		[Test] public void TheBmC2a5hDetails53Site4_TheMeshIsWhatTheExactCSGMade() => ExactJudgeCaseTests.AssertTheMeshIsWhatTheExactCSGMade(BmC2a5hDetails53Site4());
		[Test] public void TheBmC2a5hDetails53Site5_TheMeshIsWhatTheExactCSGMade() => ExactJudgeCaseTests.AssertTheMeshIsWhatTheExactCSGMade(BmC2a5hDetails53Site5());
		[Test] public void TheBmC2a5hDetails53Site6_TheMeshIsWhatTheExactCSGMade() => ExactJudgeCaseTests.AssertTheMeshIsWhatTheExactCSGMade(BmC2a5hDetails53Site6());
		[Test] public void TheBmC2a5hDetails53Site7_TheMeshIsWhatTheExactCSGMade() => ExactJudgeCaseTests.AssertTheMeshIsWhatTheExactCSGMade(BmC2a5hDetails53Site7());
		[Test] public void TheBmC2a5hDetails53Site8_TheMeshIsWhatTheExactCSGMade() => ExactJudgeCaseTests.AssertTheMeshIsWhatTheExactCSGMade(BmC2a5hDetails53Site8());
		[Test] public void TheBmC2a5iDetails122Site1_TheMeshIsWhatTheExactCSGMade() => ExactJudgeCaseTests.AssertTheMeshIsWhatTheExactCSGMade(BmC2a5iDetails122Site1());
		[Test] public void TheBmC2a5iDetails20Site1_TheMeshIsWhatTheExactCSGMade() => ExactJudgeCaseTests.AssertTheMeshIsWhatTheExactCSGMade(BmC2a5iDetails20Site1());
		[Test] public void TheBmC2a5iFuncBrush67404328Site1_TheMeshIsWhatTheExactCSGMade() => ExactJudgeCaseTests.AssertTheMeshIsWhatTheExactCSGMade(BmC2a5iFuncBrush67404328Site1());
		[Test] public void TheBmC3a1aModel0Site1_TheMeshIsWhatTheExactCSGMade() => ExactJudgeCaseTests.AssertTheMeshIsWhatTheExactCSGMade(BmC3a1aModel0Site1());
		[Test] public void TheBmC3a1aModel0Site2_TheMeshIsWhatTheExactCSGMade() => ExactJudgeCaseTests.AssertTheMeshIsWhatTheExactCSGMade(BmC3a1aModel0Site2());
		[Test] public void TheBmC3a1aModel0Site3_TheMeshIsWhatTheExactCSGMade() => ExactJudgeCaseTests.AssertTheMeshIsWhatTheExactCSGMade(BmC3a1aModel0Site3());
		[Test] public void TheBmC3a1aDetails26Site1_TheMeshIsWhatTheExactCSGMade() => ExactJudgeCaseTests.AssertTheMeshIsWhatTheExactCSGMade(BmC3a1aDetails26Site1());
		[Test] public void TheBmC3a1bDetails130Site1_TheMeshIsWhatTheExactCSGMade() => ExactJudgeCaseTests.AssertTheMeshIsWhatTheExactCSGMade(BmC3a1bDetails130Site1());
		[Test] public void TheBmC3a1bDetails34Site1_TheMeshIsWhatTheExactCSGMade() => ExactJudgeCaseTests.AssertTheMeshIsWhatTheExactCSGMade(BmC3a1bDetails34Site1());
		[Test] public void TheBmC3a2cModel0Site1_TheMeshIsWhatTheExactCSGMade() => ExactJudgeCaseTests.AssertTheMeshIsWhatTheExactCSGMade(BmC3a2cModel0Site1());
		[Test] public void TheBmC3a2cModel0Site2_TheMeshIsWhatTheExactCSGMade() => ExactJudgeCaseTests.AssertTheMeshIsWhatTheExactCSGMade(BmC3a2cModel0Site2());
		[Test] public void TheBmC3a2cModel0Site3_TheMeshIsWhatTheExactCSGMade() => ExactJudgeCaseTests.AssertTheMeshIsWhatTheExactCSGMade(BmC3a2cModel0Site3());
		[Test] public void TheBmC3a2cModel0Site4_TheMeshIsWhatTheExactCSGMade() => ExactJudgeCaseTests.AssertTheMeshIsWhatTheExactCSGMade(BmC3a2cModel0Site4());
		[Test] public void TheBmC3a2cModel0Site5_TheMeshIsWhatTheExactCSGMade() => ExactJudgeCaseTests.AssertTheMeshIsWhatTheExactCSGMade(BmC3a2cModel0Site5());
		[Test] public void TheBmC3a2cModel0Site6_TheMeshIsWhatTheExactCSGMade() => ExactJudgeCaseTests.AssertTheMeshIsWhatTheExactCSGMade(BmC3a2cModel0Site6());
		[Test] public void TheBmC3a2cModel0Site7_TheMeshIsWhatTheExactCSGMade() => ExactJudgeCaseTests.AssertTheMeshIsWhatTheExactCSGMade(BmC3a2cModel0Site7());
		[Test] public void TheBmC3a2cModel0Site8_TheMeshIsWhatTheExactCSGMade() => ExactJudgeCaseTests.AssertTheMeshIsWhatTheExactCSGMade(BmC3a2cModel0Site8());
		[Test] public void TheBmC3a2cModel0Site9_TheMeshIsWhatTheExactCSGMade() => ExactJudgeCaseTests.AssertTheMeshIsWhatTheExactCSGMade(BmC3a2cModel0Site9());
		[Test] public void TheBmC3a2cModel0Site10_TheMeshIsWhatTheExactCSGMade() => ExactJudgeCaseTests.AssertTheMeshIsWhatTheExactCSGMade(BmC3a2cModel0Site10());
		[Test] public void TheBmC3a2cModel0Site11_TheMeshIsWhatTheExactCSGMade() => ExactJudgeCaseTests.AssertTheMeshIsWhatTheExactCSGMade(BmC3a2cModel0Site11());
		[Test] public void TheBmC3a2cModel0Site12_TheMeshIsWhatTheExactCSGMade() => ExactJudgeCaseTests.AssertTheMeshIsWhatTheExactCSGMade(BmC3a2cModel0Site12());
		[Test] public void TheBmC3a2cModel0Site13_TheMeshIsWhatTheExactCSGMade() => ExactJudgeCaseTests.AssertTheMeshIsWhatTheExactCSGMade(BmC3a2cModel0Site13());
		[Test] public void TheBmC3a2cDetails34Site1_TheMeshIsWhatTheExactCSGMade() => ExactJudgeCaseTests.AssertTheMeshIsWhatTheExactCSGMade(BmC3a2cDetails34Site1());
		[Test] public void TheBmC3a2cDetails34Site2_TheMeshIsWhatTheExactCSGMade() => ExactJudgeCaseTests.AssertTheMeshIsWhatTheExactCSGMade(BmC3a2cDetails34Site2());
		[Test] public void TheBmC3a2fModel0Site1_TheMeshIsWhatTheExactCSGMade() => ExactJudgeCaseTests.AssertTheMeshIsWhatTheExactCSGMade(BmC3a2fModel0Site1());
		[Test] public void TheBmC3a2fModel0Site2_TheMeshIsWhatTheExactCSGMade() => ExactJudgeCaseTests.AssertTheMeshIsWhatTheExactCSGMade(BmC3a2fModel0Site2());
		[Test] public void TheBmC3a2fModel0Site3_TheMeshIsWhatTheExactCSGMade() => ExactJudgeCaseTests.AssertTheMeshIsWhatTheExactCSGMade(BmC3a2fModel0Site3());
		[Test] public void TheBmC3a2fModel0Site4_TheMeshIsWhatTheExactCSGMade() => ExactJudgeCaseTests.AssertTheMeshIsWhatTheExactCSGMade(BmC3a2fModel0Site4());
		[Test] public void TheBmC3a2fModel0Site5_TheMeshIsWhatTheExactCSGMade() => ExactJudgeCaseTests.AssertTheMeshIsWhatTheExactCSGMade(BmC3a2fModel0Site5());
		[Test] public void TheBmC3a2fModel0Site6_TheMeshIsWhatTheExactCSGMade() => ExactJudgeCaseTests.AssertTheMeshIsWhatTheExactCSGMade(BmC3a2fModel0Site6());
		[Test] public void TheBmC3a2fModel0Site7_TheMeshIsWhatTheExactCSGMade() => ExactJudgeCaseTests.AssertTheMeshIsWhatTheExactCSGMade(BmC3a2fModel0Site7());
		[Test] public void TheBmC3a2fModel0Site8_TheMeshIsWhatTheExactCSGMade() => ExactJudgeCaseTests.AssertTheMeshIsWhatTheExactCSGMade(BmC3a2fModel0Site8());
		[Test] public void TheBmC3a2fModel0Site9_TheMeshIsWhatTheExactCSGMade() => ExactJudgeCaseTests.AssertTheMeshIsWhatTheExactCSGMade(BmC3a2fModel0Site9());
		[Test] public void TheBmC3a2fModel0Site10_TheMeshIsWhatTheExactCSGMade() => ExactJudgeCaseTests.AssertTheMeshIsWhatTheExactCSGMade(BmC3a2fModel0Site10());
		[Test] public void TheBmC3a2fModel0Site11_TheMeshIsWhatTheExactCSGMade() => ExactJudgeCaseTests.AssertTheMeshIsWhatTheExactCSGMade(BmC3a2fModel0Site11());
		[Test] public void TheBmC3a2fModel0Site12_TheMeshIsWhatTheExactCSGMade() => ExactJudgeCaseTests.AssertTheMeshIsWhatTheExactCSGMade(BmC3a2fModel0Site12());
		[Test] public void TheBmC3a2gDetails29Site1_TheMeshIsWhatTheExactCSGMade() => ExactJudgeCaseTests.AssertTheMeshIsWhatTheExactCSGMade(BmC3a2gDetails29Site1());
		[Test] public void TheBmC3a2gDetails29Site2_TheMeshIsWhatTheExactCSGMade() => ExactJudgeCaseTests.AssertTheMeshIsWhatTheExactCSGMade(BmC3a2gDetails29Site2());
		[Test] public void TheBmC3a2hDetails29Site1_TheMeshIsWhatTheExactCSGMade() => ExactJudgeCaseTests.AssertTheMeshIsWhatTheExactCSGMade(BmC3a2hDetails29Site1());
		[Test] public void TheBmC3a2hDetails29Site2_TheMeshIsWhatTheExactCSGMade() => ExactJudgeCaseTests.AssertTheMeshIsWhatTheExactCSGMade(BmC3a2hDetails29Site2());
		[Test] public void TheBmC3a2hDetails29Site3_TheMeshIsWhatTheExactCSGMade() => ExactJudgeCaseTests.AssertTheMeshIsWhatTheExactCSGMade(BmC3a2hDetails29Site3());
		[Test] public void TheBmC3a2hDetails29Site4_TheMeshIsWhatTheExactCSGMade() => ExactJudgeCaseTests.AssertTheMeshIsWhatTheExactCSGMade(BmC3a2hDetails29Site4());
		[Test] public void TheBmC3a2hDetails29Site5_TheMeshIsWhatTheExactCSGMade() => ExactJudgeCaseTests.AssertTheMeshIsWhatTheExactCSGMade(BmC3a2hDetails29Site5());
		[Test] public void TheBmC3a2hDetails29Site6_TheMeshIsWhatTheExactCSGMade() => ExactJudgeCaseTests.AssertTheMeshIsWhatTheExactCSGMade(BmC3a2hDetails29Site6());
		[Test] public void TheBmC3a2hDetails29Site7_TheMeshIsWhatTheExactCSGMade() => ExactJudgeCaseTests.AssertTheMeshIsWhatTheExactCSGMade(BmC3a2hDetails29Site7());
		[Test] public void TheBmC4a1b1Model294Site1_TheMeshIsWhatTheExactCSGMade() => ExactJudgeCaseTests.AssertTheMeshIsWhatTheExactCSGMade(BmC4a1b1Model294Site1());
		[Test] public void TheBmC4a1b1Model294Site2_TheMeshIsWhatTheExactCSGMade() => ExactJudgeCaseTests.AssertTheMeshIsWhatTheExactCSGMade(BmC4a1b1Model294Site2());
		[Test] public void TheBmC4a1cClip90Site1_TheMeshIsWhatTheExactCSGMade() => ExactJudgeCaseTests.AssertTheMeshIsWhatTheExactCSGMade(BmC4a1cClip90Site1());
		[Test] public void TheBmC4a3bModel0Site1_TheMeshIsWhatTheExactCSGMade() => ExactJudgeCaseTests.AssertTheMeshIsWhatTheExactCSGMade(BmC4a3bModel0Site1());
		[Test] public void TheBmC4a3bDetails115Site1_TheMeshIsWhatTheExactCSGMade() => ExactJudgeCaseTests.AssertTheMeshIsWhatTheExactCSGMade(BmC4a3bDetails115Site1());
		[Test] public void TheBmC4a3c2FuncBrush4576957144Site1_TheMeshIsWhatTheExactCSGMade() => ExactJudgeCaseTests.AssertTheMeshIsWhatTheExactCSGMade(BmC4a3c2FuncBrush4576957144Site1());
		[Test] public void TheBmC4a3c2Invisible4Site1_TheMeshIsWhatTheExactCSGMade() => ExactJudgeCaseTests.AssertTheMeshIsWhatTheExactCSGMade(BmC4a3c2Invisible4Site1());
		[Test] public void TheBmC4a3c2FuncBrush454069758Site1_TheMeshIsWhatTheExactCSGMade() => ExactJudgeCaseTests.AssertTheMeshIsWhatTheExactCSGMade(BmC4a3c2FuncBrush454069758Site1());
		[Test] public void TheBmC4a3c2Details83Site1_TheMeshIsWhatTheExactCSGMade() => ExactJudgeCaseTests.AssertTheMeshIsWhatTheExactCSGMade(BmC4a3c2Details83Site1());
		[Test] public void TheBmC4a3cDetails88Site1_TheMeshIsWhatTheExactCSGMade() => ExactJudgeCaseTests.AssertTheMeshIsWhatTheExactCSGMade(BmC4a3cDetails88Site1());
		[Test] public void TheC2a5hSkyModel2Site1_TheMeshIsWhatTheExactCSGMade() => ExactJudgeCaseTests.AssertTheMeshIsWhatTheExactCSGMade(C2a5hSkyModel2Site1());
		[Test] public void TheC2a5hSkyModel2Site2_TheMeshIsWhatTheExactCSGMade() => ExactJudgeCaseTests.AssertTheMeshIsWhatTheExactCSGMade(C2a5hSkyModel2Site2());
		[Test] public void TheC2a5hSkyModel2Site3_TheMeshIsWhatTheExactCSGMade() => ExactJudgeCaseTests.AssertTheMeshIsWhatTheExactCSGMade(C2a5hSkyModel2Site3());
	}
}
