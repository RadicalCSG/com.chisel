using System.Collections.Generic;
using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace Chisel.Core.Tests
{
	[TestFixture]
	public class TreeInputHashTests
	{
		static readonly SurfaceParameterIdentity kByParameter = parameter => new Hash128((uint)parameter, (uint)(parameter >> 32), 1, 2);
		static readonly SurfaceParameterIdentity kAllTheSame  = parameter => new Hash128(3, 4, 5, 6);

		static ContentsScene TwoBoxes()
		{
			return new ContentsScene()
				.Add(ContentsSceneNode.Brush(ContentsScene.BoxPlanes(new float3(0), new float3(2)), name: "a"))
				.Add(ContentsSceneNode.Brush(ContentsScene.BoxPlanes(new float3(1), new float3(3)), operation: CSGOperationType.Subtractive, name: "b"));
		}

		static Hash128 HashOf(ContentsTreeHarness harness) { return CompactHierarchyManager.GetTreeInputHash(harness.Tree, kByParameter); }

		[Test]
		public void TheSameInput_HasTheSameHash()
		{
			using var first  = ContentsTreeHarness.Build(TwoBoxes());
			using var second = ContentsTreeHarness.Build(TwoBoxes());
			Assert.That(HashOf(first).isValid, Is.True);
			Assert.That(HashOf(second), Is.EqualTo(HashOf(first)), "the brushes' EntityIds differ, the input doesn't");
		}

		[Test]
		public void ChangingAnOperation_ChangesTheHash()
		{
			using var harness = ContentsTreeHarness.Build(TwoBoxes());
			var before = HashOf(harness);
			harness.SetOperation(harness.Scene.roots[1], CSGOperationType.Additive);
			Assert.That(HashOf(harness), Is.Not.EqualTo(before));
		}

		[Test]
		public void ChangingContents_ChangesTheHash()
		{
			using var harness = ContentsTreeHarness.Build(TwoBoxes());
			var before = HashOf(harness);
			harness.SetContents(harness.Scene.roots[0], 1);
			Assert.That(HashOf(harness), Is.Not.EqualTo(before));
		}

		[Test]
		public void MovingABrush_ChangesTheHash()
		{
			using var harness = ContentsTreeHarness.Build(TwoBoxes());
			var before = HashOf(harness);
			var brush = (CSGTreeBrush)harness.Tree[1];
			brush.LocalTransformation = float4x4.Translate(new float3(0.5f, 0, 0));
			Assert.That(HashOf(harness), Is.Not.EqualTo(before));
		}

		// The later brush of two overlapping ones wins, so their order is part of the input
		[Test]
		public void ReorderingBrushes_ChangesTheHash()
		{
			using var harness = ContentsTreeHarness.Build(TwoBoxes());
			var before = HashOf(harness);
			harness.Move(harness.Scene.roots[1], null, 0);
			Assert.That(HashOf(harness), Is.Not.EqualTo(before));
		}

		// Brushes made with other materials. In another session the same materials have other EntityIds, so the hash
		// only sees the materials through the identity it is given.
		[Test]
		public void Materials_AreHashedByTheirIdentity()
		{
			var materials = new List<Material>();
			var boxes     = new List<CSGTreeBrush>();
			var instances = new List<BrushMeshInstance>();
			var trees     = new List<CSGTree>();
			try
			{
				ContentsTreeHarness.PrepareForUpdate();
				for (int t = 0; t < 2; t++)
				{
					var material = new Material(Shader.Find("Hidden/InternalErrorShader"));
					materials.Add(material);
					var box = Box(material, instances);
					boxes.Add(box);
					trees.Add(CSGTree.Create(default(UnityEngine.EntityId), box));
				}

				Assert.That(CompactHierarchyManager.GetTreeInputHash(trees[1], kAllTheSame),
							Is.EqualTo(CompactHierarchyManager.GetTreeInputHash(trees[0], kAllTheSame)),
							"materials with the same identity");
				Assert.That(CompactHierarchyManager.GetTreeInputHash(trees[1], kByParameter),
							Is.Not.EqualTo(CompactHierarchyManager.GetTreeInputHash(trees[0], kByParameter)),
							"materials with another identity");
			}
			finally
			{
				foreach (var box in boxes)
				{
					if (box.Valid)
						box.Destroy();
				}
				foreach (var tree in trees)
				{
					if (tree.Valid)
						tree.Destroy();
				}
				foreach (var instance in instances)
					instance.Destroy();
				foreach (var material in materials)
					Object.DestroyImmediate(material);
			}
		}

		static CSGTreeBrush Box(Material material, List<BrushMeshInstance> instances)
		{
			var planes       = ContentsScene.BoxPlanes(new float3(0), new float3(2));
			var surfaceArray = new ChiselSurfaceArray();
			surfaceArray.EnsureSize(planes.Length);
			for (int i = 0; i < surfaceArray.surfaces.Length; i++)
				surfaceArray.surfaces[i] = ChiselSurface.Create(material);
			BrushMeshFactory.CreateFromPlanes(planes, new Bounds(Vector3.zero, new Vector3(16, 16, 16)), ref surfaceArray, out var brushMesh);
			var instance = BrushMeshInstance.Create(brushMesh, in surfaceArray);
			instances.Add(instance);
			return CSGTreeBrush.Create(default(UnityEngine.EntityId), float4x4.identity, instance);
		}

		static ChiselDecalInstance Decal(ulong entityID, float3 position)
		{
			return new ChiselDecalInstance
			{
				entityID         = entityID,
				decalToTree      = float4x4.Translate(position),
				settings         = ChiselDecalSettings.Default,
				destinationFlags = SurfaceDestinationFlags.Renderable
			};
		}

		static void SetDecals(CSGTree tree, params ChiselDecalInstance[] decals)
		{
			using var array = new NativeArray<ChiselDecalInstance>(decals, Allocator.Temp);
			tree.SetDecals(array);
		}

		// The tree keeps its decals in EntityId order, which is another order in every session
		[Test]
		public void Decals_AreHashedWithoutTheirEntityIds()
		{
			using var first  = ContentsTreeHarness.Build(TwoBoxes());
			using var second = ContentsTreeHarness.Build(TwoBoxes());
			SetDecals(first.Tree,  Decal(1, new float3(1, 1, 0)), Decal(2, new float3(2, 1, 0)));
			SetDecals(second.Tree, Decal(9, new float3(1, 1, 0)), Decal(4, new float3(2, 1, 0)));
			Assert.That(HashOf(second), Is.EqualTo(HashOf(first)));
		}

		[Test]
		public void MovingADecal_ChangesTheHash()
		{
			using var harness = ContentsTreeHarness.Build(TwoBoxes());
			SetDecals(harness.Tree, Decal(1, new float3(1, 1, 0)));
			var before = HashOf(harness);
			SetDecals(harness.Tree, Decal(1, new float3(1, 1.5f, 0)));
			Assert.That(HashOf(harness), Is.Not.EqualTo(before));
		}
	}
}
