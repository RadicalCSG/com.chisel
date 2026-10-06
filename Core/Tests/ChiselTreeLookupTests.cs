using NUnit.Framework;

namespace Chisel.Core.Tests
{
    [TestFixture]
    public class ChiselTreeLookupTests
    {
        [Test]
        public void DestroyingATree_ReleasesItsCaches()
        {
            var tree = CSGTree.Create(default(UnityEngine.EntityId));
            try
            {
                var data = ChiselTreeLookup.Value[tree];
                Assert.That(ChiselTreeLookup.Value.HasTree(tree), Is.True);
                Assert.That(data.brushIDValues.IsCreated, Is.True);

                // Destroy invalidates the handle it is called on, so keep a copy to look the tree up with
                var destroyed = tree;
                Assert.That(tree.Destroy(), Is.True);

                Assert.That(ChiselTreeLookup.Value.HasTree(destroyed), Is.False, "the destroyed tree still has caches");
                Assert.That(data.brushIDValues.IsCreated, Is.False, "the caches were dropped but not disposed");
            }
            finally
            {
                if (tree.Valid)
                    tree.Destroy();
            }
        }

        [Test]
        public void DestroyingATree_FreesItsHierarchy()
        {
            var first = CSGTree.Create(default(UnityEngine.EntityId));
            var firstHierarchy = first.CompactHierarchyID;
            Assert.That(first.Destroy(), Is.True);

            var second = CSGTree.Create(default(UnityEngine.EntityId));
            try
            {
                var secondHierarchy = second.CompactHierarchyID;
                Assert.That(secondHierarchy.slotIndex.index, Is.EqualTo(firstHierarchy.slotIndex.index),
                            "the destroyed tree's hierarchy slot was never freed");
                Assert.That(secondHierarchy.slotIndex.generation, Is.Not.EqualTo(firstHierarchy.slotIndex.generation));
            }
            finally
            {
                if (second.Valid)
                    second.Destroy();
            }
        }

        [Test]
        public void DestroyingATree_LeavesOtherTreesAlone()
        {
            var kept    = CSGTree.Create(default(UnityEngine.EntityId));
            var dropped = CSGTree.Create(default(UnityEngine.EntityId));
            try
            {
                var keptData = ChiselTreeLookup.Value[kept];
                _ = ChiselTreeLookup.Value[dropped];

                dropped.Destroy();

                Assert.That(ChiselTreeLookup.Value.HasTree(kept), Is.True);
                Assert.That(keptData.brushIDValues.IsCreated, Is.True);
            }
            finally
            {
                if (kept.Valid)
                    kept.Destroy();
                if (dropped.Valid)
                    dropped.Destroy();
            }
        }
    }
}
