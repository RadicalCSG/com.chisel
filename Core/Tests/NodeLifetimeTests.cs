using NUnit.Framework;
using UnityEngine;

namespace Chisel.Core.Tests
{
	[TestFixture]
	public class NodeLifetimeTests
	{
		static BrushMeshInstance Box()
		{
			BrushMeshFactory.CreateBox(new Vector3(0, 0, 0), new Vector3(1.25f, 1.5f, 1.75f), out var box);
			var surfaceArray = new ChiselSurfaceArray();
			surfaceArray.EnsureSize(box.polygons.Length);
			for (int i = 0; i < surfaceArray.surfaces.Length; i++)
				surfaceArray.surfaces[i] = new ChiselSurface();
			return BrushMeshInstance.Create(box, surfaceArray);
		}

		// Destroying a branch's children frees their ids too, all the way down
		[Test]
		public void DestroyedChildren_AreNoLongerValid()
		{
			var tree    = CSGTree.Create();
			var branch  = tree.CreateBranch();
			var inner   = CSGTreeBranch.Create();
			var brush   = CSGTreeBrush.Create();
			var nested  = CSGTreeBrush.Create();
			try
			{
				inner.Add(nested);
				branch.Add(brush);
				branch.Add(inner);
				branch.DestroyChildren();
				Assert.That(brush.Valid, Is.False, "a destroyed child kept its id");
				Assert.That(inner.Valid, Is.False, "a destroyed child branch kept its id");
				Assert.That(nested.Valid, Is.False, "a destroyed grandchild kept its id");
				Assert.That(branch.Valid, Is.True, "the parent went with its children");
			}
			finally
			{
				foreach (var node in new CSGTreeNode[] { nested, brush, inner })
					if (node.Valid) node.Destroy();
				tree.Destroy();
			}
		}

		// A destroyed tree hands its brushes to the default hierarchy, and their wireframes go with them
		[Test]
		public void ABrushOfADestroyedTree_KeepsItsWireframe()
		{
			var instance = Box();
			var tree     = CSGTree.Create();
			var brush    = tree.CreateBrush(brushMesh: instance);
			try
			{
				var before = CompactHierarchyManager.GetOutline(brush.NodeID);
				Assert.That(before.IsCreated, Is.True);
				tree.Destroy();
				var after = CompactHierarchyManager.GetOutline(brush.NodeID);
				Assert.That(after == before, Is.True, "the brush got a new wireframe and the old one was left behind");
			}
			finally
			{
				if (brush.Valid) brush.Destroy();
				if (tree.Valid) tree.Destroy();
				instance.Destroy();
			}
		}
	}
}
