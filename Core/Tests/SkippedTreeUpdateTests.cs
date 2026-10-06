using System.Collections.Generic;
using NUnit.Framework;
using Unity.Mathematics;

namespace Chisel.Core.Tests
{
	[TestFixture]
	public class SkippedTreeUpdateTests
	{
		static ContentsScene TwoBoxes()
		{
			return new ContentsScene()
				.Add(ContentsSceneNode.Brush(ContentsScene.BoxPlanes(new float3(0), new float3(2)), name: "a"))
				.Add(ContentsSceneNode.Brush(ContentsScene.BoxPlanes(new float3(1), new float3(3)), operation: CSGOperationType.Subtractive, name: "b"));
		}

		// Flushes every tree that needs an update except the one given, and returns the trees that were built
		static List<CSGTree> FlushSkipping(CSGTree skipped)
		{
			var built = new List<CSGTree>();
			CompactHierarchyManager.Flush((tree, meshUpdates, dependencies) =>
			{
				dependencies.Complete();
				built.Add(tree);
				if (meshUpdates.meshDataArray.Length > 0)
					meshUpdates.meshDataArray.Dispose();
				return 0;
			}, tree => tree == skipped);
			return built;
		}

		[Test]
		public void ASkippedTree_IsBuiltAfterUpdateSkippedTrees()
		{
			using var harness = ContentsTreeHarness.Build(TwoBoxes());
			Assert.That(FlushSkipping(harness.Tree), Has.No.Member(harness.Tree));
			Assert.That(CompactHierarchyManager.IsTreeUpdateSkipped(harness.Tree), Is.True);

			CompactHierarchyManager.UpdateSkippedTrees();
			harness.Update();
			Assert.That(harness.Delivered, Is.True, harness.LastUpdateReport);
			Assert.That(harness.Triangles, Is.Not.Empty);
			Assert.That(CompactHierarchyManager.IsTreeUpdateSkipped(harness.Tree), Is.False);
		}

		[Test]
		public void ADestroyedTree_IsNoLongerSkipped()
		{
			CSGTree destroyed;
			using (var harness = ContentsTreeHarness.Build(TwoBoxes()))
			{
				destroyed = harness.Tree;
				FlushSkipping(destroyed);
				Assert.That(CompactHierarchyManager.IsTreeUpdateSkipped(destroyed), Is.True);
			}
			Assert.That(CompactHierarchyManager.IsTreeUpdateSkipped(destroyed), Is.False);

			using var next = ContentsTreeHarness.Build(TwoBoxes());
			Assert.DoesNotThrow(CompactHierarchyManager.UpdateSkippedTrees);
		}
	}
}
