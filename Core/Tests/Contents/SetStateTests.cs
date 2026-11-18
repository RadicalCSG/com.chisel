using NUnit.Framework;
using Unity.Mathematics;

namespace Chisel.Core.Tests
{
	[TestFixture]
	[Category("Contents")]
	public class SetStateTests
	{
		static ContentsTreeHarness BuildAndUpdate()
		{
			var scene = new ContentsScene()
				.Add(ContentsSceneNode.Brush(ContentsScene.BoxPlanes(new float3(0), new float3(2)), name: "a"))
				.Add(ContentsSceneNode.Brush(ContentsScene.BoxPlanes(new float3(1), new float3(3)), name: "b"));
			var harness = ContentsTreeHarness.Build(scene);
			Assert.That(harness.Update(), Is.True, "the CSG update did not run");
			return harness;
		}

		static CompactNodeID FirstBrush(ContentsTreeHarness harness) => ((CSGTreeBrush)harness.Tree[0]).CompactNodeID;

		static void SetState(ref CompactHierarchy hierarchy, CompactNodeID brushID, float4x4 transformation)
		{
			hierarchy.SetState(brushID, ChiselMeshLookup.Value.brushMeshBlobCache, hierarchy.GetBrushMeshID(brushID),
							   hierarchy.GetOperation(brushID), hierarchy.GetContents(brushID), transformation);
		}

		[Test]
		public void SetState_WithNothingChanged_FlagsNothing()
		{
			using (var harness = BuildAndUpdate())
			{
				var brushID = FirstBrush(harness);
				ref var hierarchy = ref CompactHierarchyManager.GetHierarchy(brushID);
				Assert.That(hierarchy.IsAnyStatusFlagSet(brushID), Is.False, "the update left flags on the brush");

				SetState(ref hierarchy, brushID, hierarchy.GetLocalTransformation(brushID));

				Assert.That(hierarchy.IsAnyStatusFlagSet(brushID), Is.False, "an unchanged state flagged the brush");
			}
		}

		[Test]
		public void SetBrushMeshID_WithTheSameID_FlagsNothing()
		{
			using (var harness = BuildAndUpdate())
			{
				var brushID = FirstBrush(harness);
				ref var hierarchy = ref CompactHierarchyManager.GetHierarchy(brushID);
				Assert.That(hierarchy.IsAnyStatusFlagSet(brushID), Is.False, "the update left flags on the brush");

				var changed = hierarchy.SetBrushMeshID(brushID, hierarchy.GetBrushMeshID(brushID));

				Assert.That(changed, Is.False);
				Assert.That(hierarchy.IsAnyStatusFlagSet(brushID), Is.False,
					"reassigning the same mesh made the editor rebuild the tree again next tick");
			}
		}

		[Test]
		public void SetState_WithAMovedBrush_FlagsItAndWhatItTouches()
		{
			using (var harness = BuildAndUpdate())
			{
				var brushID = FirstBrush(harness);
				ref var hierarchy = ref CompactHierarchyManager.GetHierarchy(brushID);
				var moved = math.mul(float4x4.Translate(new float3(0.25f, 0, 0)), hierarchy.GetLocalTransformation(brushID));

				SetState(ref hierarchy, brushID, moved);

				Assert.That(hierarchy.IsStatusFlagSet(brushID, NodeStatusFlags.TransformationModified), Is.True);
				Assert.That(hierarchy.IsStatusFlagSet(brushID, NodeStatusFlags.NeedAllTouchingUpdated), Is.True);
				Assert.That(hierarchy.GetLocalTransformation(brushID), Is.EqualTo(moved));
			}
		}

		// Surfaces built again without a change of shape (paint): the brush alone, not what it touches
		[Test]
		public void SetSurfacesDirty_FlagsTheBrushOnly()
		{
			using (var harness = BuildAndUpdate())
			{
				var brush   = (CSGTreeBrush)harness.Tree[0];
				var brushID = brush.CompactNodeID;
				ref var hierarchy = ref CompactHierarchyManager.GetHierarchy(brushID);

				((CSGTreeNode)brush).SetSurfacesDirty();

				Assert.That(hierarchy.IsStatusFlagSet(brushID, NodeStatusFlags.OutlineModified), Is.True);
				Assert.That(hierarchy.IsStatusFlagSet(brushID, NodeStatusFlags.NeedAllTouchingUpdated), Is.False);
				Assert.That(hierarchy.IsStatusFlagSet(brushID, NodeStatusFlags.ShapeModified), Is.False);
				Assert.That(harness.Update(), Is.True, "the CSG update did not run");
				Assert.That(hierarchy.IsAnyStatusFlagSet(brushID), Is.False, "the update left flags on the brush");
			}
		}

		// Every brush flagged, and the tree: one update clears them all
		[Test]
		public void AnUpdate_ClearsTheFlagsOfEveryBrushAndTheTree()
		{
			var scene = new ContentsScene();
			for (int i = 0; i < 40; i++)
				scene.Add(ContentsSceneNode.Brush(ContentsScene.BoxPlanes(new float3(i * 3, 0, 0), new float3(i * 3 + 2, 2, 2)), name: $"box {i}"));
			using (var harness = ContentsTreeHarness.Build(scene))
			{
				Assert.That(harness.Update(), Is.True, "the CSG update did not run");
				var treeID = harness.Tree.CompactNodeID;
				ref var hierarchy = ref CompactHierarchyManager.GetHierarchy(treeID);
				for (int i = 0; i < harness.Tree.Count; i++)
					((CSGTreeNode)harness.Tree[i]).SetSurfacesDirty();
				hierarchy.SetStatusFlag(treeID, NodeStatusFlags.TreeNeedsUpdate);

				Assert.That(harness.Update(), Is.True, "the CSG update did not run");

				Assert.That(hierarchy.IsAnyStatusFlagSet(treeID), Is.False, "the update left flags on the tree");
				for (int i = 0; i < harness.Tree.Count; i++)
					Assert.That(hierarchy.IsAnyStatusFlagSet(((CSGTreeBrush)harness.Tree[i]).CompactNodeID), Is.False, $"the update left flags on brush {i}");
			}
		}
	}
}
