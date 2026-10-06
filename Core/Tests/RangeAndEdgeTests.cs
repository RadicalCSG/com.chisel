using NUnit.Framework;

namespace Chisel.Core.Tests
{
    [TestFixture]
    public class RangeTests
    {
        [Test]
        public void Length_IsEndMinusStart()
        {
            Assert.That(new Range { start = 2, end = 10 }.Length, Is.EqualTo(8));
            Assert.That(new Range { start = 0, end = 0 }.Length, Is.EqualTo(0));
            Assert.That(new Range { start = 7, end = 5 }.Length, Is.EqualTo(-2));
        }

        [Test]
        public void Center_IsMidpointRoundedTowardStart()
        {
            Assert.That(new Range { start = 2, end = 10 }.Center, Is.EqualTo(6));
            Assert.That(new Range { start = 5, end = 12 }.Center, Is.EqualTo(8));
            Assert.That(new Range { start = 0, end = 1 }.Center, Is.EqualTo(0));
        }
    }

    [TestFixture]
    public class EdgeTests
    {
        [Test]
        public void Equals_IsOrderSensitive()
        {
            var a = new Edge { index1 = 3, index2 = 7 };
            var b = new Edge { index1 = 3, index2 = 7 };
            var reversed = new Edge { index1 = 7, index2 = 3 };

            Assert.That(a.Equals(b), Is.True);
            Assert.That(a.Equals(reversed), Is.False);
        }

        [Test]
        public void GetHashCode_IsStableForEqualEdges()
        {
            var a = new Edge { index1 = 11, index2 = 22 };
            var b = new Edge { index1 = 11, index2 = 22 };
            Assert.That(a.GetHashCode(), Is.EqualTo(b.GetHashCode()));
        }

        [Test]
        public void ToString_ListsBothIndices()
        {
            var edge = new Edge { index1 = 4, index2 = 9 };
            Assert.That(edge.ToString(), Is.EqualTo("(4, 9)"));
        }
    }
}
