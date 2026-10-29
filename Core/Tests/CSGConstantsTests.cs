using NUnit.Framework;

namespace Chisel.Core.Tests
{
    [TestFixture]
    public class CSGConstantsTests
    {
        [Test]
        public void Epsilons_ArePositive()
        {
            Assert.That(CSGConstants.kFatPlaneWidthEpsilon,   Is.GreaterThan(0f));
            Assert.That(CSGConstants.kDivideMinimumEpsilon,   Is.GreaterThan(0.0));
        }
    }
}
