using NUnit.Framework;

namespace Chisel.Core.Tests
{
    // Locks the numerical tolerances the CSG algorithm depends on, and the relationships
    // between the squared/linear variants, so accidental edits get caught.
    [TestFixture]
    public class CSGConstantsTests
    {
        [Test]
        public void Epsilons_ArePositive()
        {
            Assert.That(CSGConstants.kBoundsDistanceEpsilon,  Is.GreaterThan(0f));
            Assert.That(CSGConstants.kFatPlaneWidthEpsilon,   Is.GreaterThan(0f));
            Assert.That(CSGConstants.kEdgeIntersectionEpsilon, Is.GreaterThan(0f));
            Assert.That(CSGConstants.kVertexEqualEpsilon,     Is.GreaterThan(0f));
            Assert.That(CSGConstants.kPlaneDAlignEpsilon,     Is.GreaterThan(0f));
            Assert.That(CSGConstants.kDivideMinimumEpsilon,   Is.GreaterThan(0.0));
        }

        [Test]
        public void VertexEqualEpsilon_HasExpectedValue()
        {
            Assert.That(CSGConstants.kVertexEqualEpsilon, Is.EqualTo(0.0125f).Within(1e-6f));
        }

        [Test]
        public void SquaredEpsilons_MatchTheirLinearVariants()
        {
            Assert.That(CSGConstants.kSqrVertexEqualEpsilon,
                        Is.EqualTo(CSGConstants.kVertexEqualEpsilon * CSGConstants.kVertexEqualEpsilon).Within(1e-9f));
            Assert.That(CSGConstants.kSqrEdgeDistanceEpsilon,
                        Is.EqualTo(CSGConstants.kEdgeIntersectionEpsilon * CSGConstants.kEdgeIntersectionEpsilon).Within(1e-9f));
        }

        [Test]
        public void NormalDotAlignEpsilon_IsCloseToButBelowOne()
        {
            Assert.That(CSGConstants.kNormalDotAlignEpsilon, Is.LessThan(1f));
            Assert.That(CSGConstants.kNormalDotAlignEpsilon, Is.GreaterThan(0.99f));
        }
    }
}
