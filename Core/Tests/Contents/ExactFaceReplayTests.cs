using System;
using System.Collections.Generic;
using NUnit.Framework;
using Unity.Mathematics;

namespace Chisel.Core.Tests
{
	// A face of a brush built again only because a brush it touches changed is replayed from the last update when nothing that
	// can reach it changed (ExactCSGJob, ExactFaceCache): what it writes must be what building the face gives
	[TestFixture]
	[Category("Contents")]
	public class ExactFaceReplayTests
	{
		const int kWater = 3;

		static ContentsSceneNode Box(float3 min, float3 max, string name, CSGOperationType operation = CSGOperationType.Additive, int contents = 0)
			=> ContentsSceneNode.Brush(ContentsScene.BoxPlanes(min, max), contents, operation, name: name);

		// A floor with posts standing on it and touching each other, walls along two sides, a hole cut in the floor, water in
		// it, and a composite that keeps the part of a block inside another one
		static ContentsScene Scene()
		{
			var scene = new ContentsScene().WithContentsList("Solid", "Glass", "Grate", "Water");
			scene.Add(Box(new float3(-1, -1, -1), new float3(9, 0, 5), "floor"));
			for (int x = 0; x < 4; x++)
				for (int z = 0; z < 2; z++)
					scene.Add(Box(new float3(x * 2, 0, z * 2), new float3(x * 2 + 1.5f, 2, z * 2 + 2), $"post {x} {z}"));
			scene.Add(Box(new float3(-1, 0, -1), new float3(9, 3, -0.5f), "wall south"));
			scene.Add(Box(new float3(8.5f, 0, -1), new float3(9, 3, 5), "wall east"));
			scene.Add(Box(new float3(3, -0.5f, 1), new float3(5, 0.5f, 3), "hole", CSGOperationType.Subtractive));
			scene.Add(Box(new float3(3, -0.5f, 1), new float3(5, -0.25f, 3), "water", contents: kWater));
			scene.Add(ContentsSceneNode.Composite(CSGOperationType.Additive,
				Box(new float3(1, 1, 3.5f), new float3(4, 2.5f, 4.5f), "block"),
				Box(new float3(2, 0.5f, 3), new float3(6, 2, 5), "keeps", CSGOperationType.Intersecting)));
			return scene;
		}

		// The same scene twice: one tree updated with faces replayed (and every replayed face built as well and compared,
		// which counts ReplayMismatch, a failure the harness throws on), one with every face built
		sealed class Pair : IDisposable
		{
			public ContentsTreeHarness replayed;
			public ContentsTreeHarness built;
			public int replayedFaces;

			// One after the other: an update builds every tree that changed, and a harness only sees its own tree's meshes
			public Pair()
			{
				try
				{
					replayed = ContentsTreeHarness.Build(Scene(), captureExact: false);
					Update(replayed, true, "first update");
					built = ContentsTreeHarness.Build(Scene(), captureExact: false);
					Update(built, false, "first update");
					AssertSameTriangles("first update");
				}
				catch
				{
					Dispose();
					throw;
				}
			}

			// Makes the same edit to both and updates them, each on its own: an update builds every tree that changed
			public void Edit(Action<ContentsTreeHarness> edit, string what)
			{
				edit(replayed);
				Update(replayed, true, what);
				edit(built);
				Update(built, false, what);
				AssertSameTriangles(what);
			}

			public void Update(ContentsTreeHarness harness, bool replay, string what)
			{
				var buildEveryFace = CompactHierarchyManager.BuildEveryFace;
				var verify      = CompactHierarchyManager.VerifyReplayedFaces;
				try
				{
					CompactHierarchyManager.BuildEveryFace      = !replay;
					CompactHierarchyManager.VerifyReplayedFaces = replay;
					harness.Update();
					if (replay)
						replayedFaces += CompactHierarchyManager.TreeUpdate.s_LastExactCSGStats[(int)ExactCSGStat.FacesReplayed];
					else
						Assert.That(CompactHierarchyManager.TreeUpdate.s_LastExactCSGStats[(int)ExactCSGStat.FacesReplayed], Is.Zero, $"{what}: replayed while switched off");
				}
				finally
				{
					CompactHierarchyManager.BuildEveryFace      = buildEveryFace;
					CompactHierarchyManager.VerifyReplayedFaces = verify;
				}
			}

			public void AssertSameTriangles(string what)
			{
				var a = replayed.Triangles;
				var b = built.Triangles;
				Assert.That(a.Count, Is.EqualTo(b.Count), $"{what}: triangle count");
				for (int i = 0; i < a.Count; i++)
					Assert.That(Same(a[i], b[i]), Is.True, $"{what}: triangle {i} differs");
			}

			public void Dispose()
			{
				replayed?.Dispose();
				built?.Dispose();
			}
		}

		// Everything but the brush's node id, which is another in each tree
		static bool Same(ContentsTreeHarness.CapturedTriangle p, ContentsTreeHarness.CapturedTriangle q)
		{
			return math.all(p.a == q.a) && math.all(p.b == q.b) && math.all(p.c == q.c) &&
				   math.all(p.surfaceNormal == q.surfaceNormal) && p.material == q.material &&
				   math.all(p.uvA == q.uvA) && math.all(p.uvB == q.uvB) && math.all(p.uvC == q.uvC) &&
				   math.all(p.lightmapA == q.lightmapA) && math.all(p.lightmapB == q.lightmapB) && math.all(p.lightmapC == q.lightmapC) &&
				   math.all(p.normalA == q.normalA) && math.all(p.normalB == q.normalB) && math.all(p.normalC == q.normalC) &&
				   math.all(p.tangentA == q.tangentA) && math.all(p.tangentB == q.tangentB) && math.all(p.tangentC == q.tangentC) &&
				   p.section == q.section && p.selectedEntityID == q.selectedEntityID;
		}

		static ContentsSceneNode Named(ContentsTreeHarness harness, string name)
		{
			var found = Find(harness.Scene.roots, name);
			Assert.That(found, Is.Not.Null, name);
			return found;
		}

		static ContentsSceneNode Find(List<ContentsSceneNode> nodes, string name)
		{
			foreach (var node in nodes)
			{
				if (node.name == name)
					return node;
				if (node.children != null && Find(node.children, name) is ContentsSceneNode child)
					return child;
			}
			return null;
		}

		static void Shift(ContentsTreeHarness harness, string name, float3 offset)
		{
			var node = Named(harness, name);
			harness.SetLocalToTree(node, math.mul(float4x4.Translate(offset), node.localToTree));
		}

		static Pair Start() => new Pair();

		[Test]
		public void MovingAPost_ReplaysTheFacesItCannotReach()
		{
			using var pair = Start();
			pair.replayedFaces = 0;
			pair.Edit(h => Shift(h, "post 1 0", new float3(0, 0.25f, 0)), "post 1 0 up");
			Assert.That(pair.replayedFaces, Is.GreaterThan(0), "no face of the posts and floor around it was replayed");
		}

		[Test]
		public void APostMovedThroughItsNeighbourAndBack_GivesWhatBuildingGives()
		{
			using var pair = Start();
			for (int step = 0; step < 8; step++)
				pair.Edit(h => Shift(h, "post 1 0", new float3(0.25f, 0, 0)), $"step {step} east");
			for (int step = 0; step < 8; step++)
				pair.Edit(h => Shift(h, "post 1 0", new float3(-0.25f, 0, 0)), $"step {step} back");
			Assert.That(pair.replayedFaces, Is.GreaterThan(0));
		}

		[TestCase("hole", CSGOperationType.Additive)]
		[TestCase("post 2 0", CSGOperationType.Subtractive)]
		[TestCase("post 2 0", CSGOperationType.Intersecting)]
		[TestCase("keeps", CSGOperationType.Additive)]
		[TestCase("block", CSGOperationType.Subtractive)]
		public void AnOperationChanged_GivesWhatBuildingGives(string name, CSGOperationType operation)
		{
			using var pair = Start();
			pair.Edit(h => h.SetOperation(Named(h, name), operation), $"{name} made {operation}");
			pair.Edit(h => Shift(h, "post 1 1", new float3(0, 0.25f, 0)), "post 1 1 up");
		}

		[Test]
		public void ContentsChanged_GiveWhatBuildingGives()
		{
			using var pair = Start();
			pair.Edit(h => h.SetContents(Named(h, "water"), 0), "water made solid");
			pair.replayedFaces = 0;
			pair.Edit(h => h.SetContents(Named(h, "post 2 1"), kWater), "post 2 1 made water");
			// the faces post 2 1 can't reach are replayed; the ones it can are built with its new categories
			Assert.That(pair.replayedFaces, Is.GreaterThan(0), "no face was replayed after the contents change");
			pair.Edit(h => Shift(h, "post 2 0", new float3(0, 0.25f, 0)), "post 2 0 up");
		}

		[Test]
		public void BrushesReorderedRemovedAndAdded_GiveWhatBuildingGives()
		{
			using var pair = Start();
			pair.Edit(h => h.Move(Named(h, "post 1 0"), null, h.Scene.roots.Count - 1), "post 1 0 moved last");
			pair.Edit(h => h.Move(Named(h, "hole"), null, 0), "hole moved first");
			pair.Edit(h => h.Remove(Named(h, "post 2 1"), destroy: true), "post 2 1 removed");
			pair.Edit(h => h.Insert(null, 3, Box(new float3(2.5f, 0, 1), new float3(4.5f, 1, 2.5f), "added")), "a box added");
			pair.Edit(h => Shift(h, "added", new float3(0, 0, 0.25f)), "the added box moved");
		}

		[Test]
		public void RandomEdits_GiveWhatBuildingGives([Values(1, 2, 3)] int seed)
		{
			var random = new Unity.Mathematics.Random((uint)seed * 7919u);
			using var pair = Start();
			var names = new List<string>();
			foreach (var root in pair.replayed.Scene.roots)
				if (root.IsBrush)
					names.Add(root.name);
			for (int step = 0; step < 24; step++)
			{
				var name = names[random.NextInt(names.Count)];
				switch (random.NextInt(5))
				{
					case 0:
					case 1:
					{
						var offset = random.NextInt(3) switch { 0 => new float3(0.25f, 0, 0), 1 => new float3(0, -0.25f, 0), _ => new float3(0, 0, 0.5f) };
						if (random.NextBool()) offset = -offset;
						pair.Edit(h => Shift(h, name, offset), $"step {step}: {name} moved by {offset}");
						break;
					}
					case 2:
					{
						var operation = (CSGOperationType)random.NextInt(3);
						pair.Edit(h => h.SetOperation(Named(h, name), operation), $"step {step}: {name} made {operation}");
						break;
					}
					case 3:
					{
						int index = random.NextInt(pair.replayed.Scene.roots.Count);
						pair.Edit(h => h.Move(Named(h, name), null, index), $"step {step}: {name} moved to {index}");
						break;
					}
					default:
					{
						var contents = random.NextBool() ? kWater : 0;
						pair.Edit(h => h.SetContents(Named(h, name), contents), $"step {step}: {name} given contents {contents}");
						break;
					}
				}
			}
			Assert.That(pair.replayedFaces, Is.GreaterThan(0));
		}
	}
}
