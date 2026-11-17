using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;

namespace Chisel.Core.Tests
{
	// The update builds the tree's contents volume in its jobs and hands it over with the meshes: it is what building it
	// from the tree as it is gives (ContentsVolume.Build), after every kind of edit
	[TestFixture]
	[Category("Contents")]
	public class ContentsVolumeUpdateTests
	{
		static ContentsSceneNode Box(float3 min, float3 max, string name, CSGOperationType operation = CSGOperationType.Additive)
			=> ContentsSceneNode.Brush(ContentsScene.BoxPlanes(min, max), operation: operation, name: name);

		static void AssertHandedOverAsBuilt(ContentsTreeHarness harness, string edit)
		{
			Assert.That(harness.Update(), Is.True, edit);
			Assert.That(harness.Delivered, Is.True, edit);
			using var nodes  = new NativeList<ContentsVolumeNode>(Allocator.TempJob);
			using var planes = new NativeList<float4>(Allocator.TempJob);
			ContentsVolume.Build(harness.Tree, nodes, planes);
			Assert.That(nodes.Length, Is.GreaterThan(0), edit);
			Assert.That(harness.ContentsVolumeNodes, Is.EqualTo(nodes.AsArray().ToArray()), $"{edit}: nodes");
			Assert.That(harness.ContentsVolumePlanes, Is.EqualTo(planes.AsArray().ToArray()), $"{edit}: planes");
		}

		[Test]
		public void EachUpdate_HandsOverTheContentsVolumeOfTheTreeAsItIs()
		{
			var scene = new ContentsScene();
			var cut = Box(new float3(1, 0, 0), new float3(2, 1, 1), "cut", CSGOperationType.Subtractive);
			var inner = Box(new float3(0, 0, 3), new float3(3, 1, 4), "inner");
			scene.Add(Box(new float3(0, 0, 0), new float3(3, 1, 1), "floor"));
			scene.Add(cut);
			scene.Add(ContentsSceneNode.Composite(CSGOperationType.Additive, inner, Box(new float3(1, 0, 3), new float3(2, 2, 4), "post")));
			using var harness = ContentsTreeHarness.Build(scene);
			harness.JudgeEachUpdate = false;
			AssertHandedOverAsBuilt(harness, "built");

			harness.SetLocalToTree(cut, math.mul(float4x4.Translate(new float3(0.5f, 0, 0)), cut.localToTree));
			AssertHandedOverAsBuilt(harness, "moved the cut");
			harness.SetOperation(cut, CSGOperationType.Intersecting);
			AssertHandedOverAsBuilt(harness, "made the cut intersecting");
			harness.Move(inner, null, 0);
			AssertHandedOverAsBuilt(harness, "moved a brush out of its composite");
			harness.Insert(null, 1, Box(new float3(5, 0, 0), new float3(6, 1, 1), "added"));
			AssertHandedOverAsBuilt(harness, "added a brush");
			harness.Remove(cut, destroy: true);
			AssertHandedOverAsBuilt(harness, "removed the cut");
		}
	}
}
