using Chisel.Core;
using NUnit.Framework;

namespace Chisel.Components.Tests
{
    [TestFixture]
    public class SurfaceReferenceTests
    {
        // A reference that may be null compares with null either way round, without throwing
        [Test]
        public void ANullReference_ComparesWithNull_EitherWayRound()
        {
            SurfaceReference none = null;
            var some = new SurfaceReference(null, 1, default(CSGTreeBrush), 2);

            Assert.That(none == null, Is.True);
            Assert.That(none != null, Is.False);
            Assert.That(null == none, Is.True);
            Assert.That(some == null, Is.False);
            Assert.That(some != null, Is.True);
            Assert.That(none == some, Is.False);
            Assert.That(none != some, Is.True);
            Assert.That(some == new SurfaceReference(null, 1, default(CSGTreeBrush), 2), Is.True, "the same surface");
        }
    }
}
