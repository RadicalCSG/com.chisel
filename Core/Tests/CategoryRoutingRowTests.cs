using NUnit.Framework;

namespace Chisel.Core.Tests
{
    [TestFixture]
    public class CategoryRoutingRowTests
    {
        const int kAdditive     = 0;
        const int kSubtractive  = 1;
        const int kIntersection = 2;
        const int kInvalidOp    = 3;

        [Test]
        public void Length_EqualsNumberOfCategories()
        {
            // 6 self-categories: Inside, Aligned, SelfAligned, SelfReverseAligned, ReverseAligned, Outside
            Assert.That(CategoryRoutingRow.Length, Is.EqualTo((int)CategoryIndex.LastCategory + 1));
            Assert.That(CategoryRoutingRow.Length, Is.EqualTo(6));
        }

        [Test]
        public void OperationStride_IsRowStrideTimesOperationCount()
        {
            Assert.That(CategoryRoutingRow.RowStride, Is.EqualTo(CategoryRoutingRow.OperationCount));
            Assert.That(CategoryRoutingRow.OperationStride,
                        Is.EqualTo(CategoryRoutingRow.OperationCount * CategoryRoutingRow.RowStride));
        }

        [Test]
        public void OperationTables_HaveExactlyFourOperationBlocks()
        {
            Assert.That(CategoryRoutingRow.kOperationTables.Length,
                        Is.EqualTo(CategoryRoutingRow.OperationStride * 4));
        }

        [Test]
        public void OperationTables_ValidOperationsOnlyContainValidCategories()
        {
            // The first three operation blocks (additive/subtractive/intersection) must only
            // reference valid category indices (0..LastCategory); never the Invalid (255) sentinel.
            var stride = CategoryRoutingRow.OperationStride;
            for (int op = kAdditive; op <= kIntersection; op++)
            {
                for (int i = 0; i < stride; i++)
                {
                    int value = CategoryRoutingRow.kOperationTables[op * stride + i];
                    Assert.That(value, Is.InRange(0, (int)CategoryIndex.LastCategory),
                                $"operation {op}, entry {i} is out of range ({value})");
                }
            }
        }

        [Test]
        public void OperationTables_InvalidOperationBlockIsAllInvalid()
        {
            var stride = CategoryRoutingRow.OperationStride;
            for (int i = 0; i < stride; i++)
            {
                int value = CategoryRoutingRow.kOperationTables[kInvalidOp * stride + i];
                // Compared with the sentinel itself rather than a literal: it was 255 while destinations were bytes, and
                // moved to ushort.MaxValue when they were widened, because 255 became an ordinary row index.
                Assert.That(value, Is.EqualTo((int)CategoryRoutingRow.AllInvalid.inside),
                            $"invalid-op entry {i} should be the Invalid sentinel");
            }
        }

        // Building a routing row from the Identity right-row reproduces a raw table row,
        // because Identity = (Inside=0, Aligned=1, ... Outside=5) indexes each column in order.

        [Test]
        public void Additive_InsideRow_RoutesEverythingInside()
        {
            var row = new CategoryRoutingRow(kAdditive, CategoryIndex.Inside, CategoryRoutingRow.Identity);
            Assert.That(row.Equals(CategoryRoutingRow.AllInside), Is.True);
            Assert.That(row.AreAllValue((int)CategoryIndex.Inside), Is.True);
        }

        [Test]
        public void Additive_OutsideRow_IsIdentity()
        {
            // For the additive (union) operation, the "outside" left-node row passes the right
            // node through unchanged -> it equals the Identity row.
            var row = new CategoryRoutingRow(kAdditive, CategoryIndex.Outside, CategoryRoutingRow.Identity);
            Assert.That(row.Equals(CategoryRoutingRow.Identity), Is.True);
        }

        [Test]
        public void Subtractive_OutsideRow_RoutesEverythingOutside()
        {
            var row = new CategoryRoutingRow(kSubtractive, CategoryIndex.Outside, CategoryRoutingRow.Identity);
            Assert.That(row.Equals(CategoryRoutingRow.AllOutside), Is.True);
            Assert.That(row.AreAllValue((int)CategoryIndex.Outside), Is.True);
        }

        [Test]
        public void Subtractive_InsideRow_InvertsAlignment()
        {
            // inside row of subtractive table: Outside, ReverseAligned, SelfReverseAligned, SelfAligned, Aligned, Inside
            var row = new CategoryRoutingRow(kSubtractive, CategoryIndex.Inside, CategoryRoutingRow.Identity);
            Assert.That(row.inside,             Is.EqualTo((byte)CategoryIndex.Outside));
            Assert.That(row.aligned,            Is.EqualTo((byte)CategoryIndex.ReverseAligned));
            Assert.That(row.selfAligned,        Is.EqualTo((byte)CategoryIndex.SelfReverseAligned));
            Assert.That(row.selfReverseAligned, Is.EqualTo((byte)CategoryIndex.SelfAligned));
            Assert.That(row.reverseAligned,     Is.EqualTo((byte)CategoryIndex.Aligned));
            Assert.That(row.outside,            Is.EqualTo((byte)CategoryIndex.Inside));
        }

        [Test]
        public void Intersection_InsideRow_IsIdentity()
        {
            var row = new CategoryRoutingRow(kIntersection, CategoryIndex.Inside, CategoryRoutingRow.Identity);
            Assert.That(row.Equals(CategoryRoutingRow.Identity), Is.True);
        }

        [Test]
        public void Intersection_OutsideRow_RoutesEverythingOutside()
        {
            var row = new CategoryRoutingRow(kIntersection, CategoryIndex.Outside, CategoryRoutingRow.Identity);
            Assert.That(row.Equals(CategoryRoutingRow.AllOutside), Is.True);
        }

        [Test]
        public void Identity_Indexer_ReturnsCategoryInOrder()
        {
            var identity = CategoryRoutingRow.Identity;
            Assert.That(identity[0], Is.EqualTo((byte)CategoryIndex.Inside));
            Assert.That(identity[1], Is.EqualTo((byte)CategoryIndex.Aligned));
            Assert.That(identity[2], Is.EqualTo((byte)CategoryIndex.SelfAligned));
            Assert.That(identity[3], Is.EqualTo((byte)CategoryIndex.SelfReverseAligned));
            Assert.That(identity[4], Is.EqualTo((byte)CategoryIndex.ReverseAligned));
            Assert.That(identity[5], Is.EqualTo((byte)CategoryIndex.Outside));
        }

        [Test]
        public void AreAllTheSame_TrueForUniformRows_FalseForIdentity()
        {
            Assert.That(CategoryRoutingRow.AllInside.AreAllTheSame(),             Is.True);
            Assert.That(CategoryRoutingRow.AllOutside.AreAllTheSame(),            Is.True);
            Assert.That(CategoryRoutingRow.AllSelfAligned.AreAllTheSame(),        Is.True);
            Assert.That(CategoryRoutingRow.AllSelfReverseAligned.AreAllTheSame(), Is.True);
            Assert.That(CategoryRoutingRow.Identity.AreAllTheSame(),             Is.False);
        }

        [Test]
        public void OperatorPlus_OffsetsEveryColumn()
        {
            // AllInside is all zeroes; adding 3 produces all SelfReverseAligned (== 3).
            var shifted = CategoryRoutingRow.AllInside + (int)CategoryIndex.SelfReverseAligned;
            Assert.That(shifted.Equals(CategoryRoutingRow.AllSelfReverseAligned), Is.True);
        }

        [Test]
        public void OperatorPlus_Zero_IsNoOp()
        {
            var same = CategoryRoutingRow.Identity + 0;
            Assert.That(same.Equals(CategoryRoutingRow.Identity), Is.True);
        }

        [Test]
        public void Equals_DistinguishesDifferentRows()
        {
            Assert.That(CategoryRoutingRow.Identity.Equals(CategoryRoutingRow.Identity), Is.True);
            Assert.That(CategoryRoutingRow.Identity.Equals(CategoryRoutingRow.AllInside), Is.False);
            Assert.That(CategoryRoutingRow.AllInside.Equals(CategoryRoutingRow.AllOutside), Is.False);
        }

        [Test]
        public void ByteValueConstructor_FillsAllColumns()
        {
            var row = new CategoryRoutingRow((byte)CategoryIndex.Aligned);
            Assert.That(row.AreAllValue((int)CategoryIndex.Aligned), Is.True);
        }
    }
}
