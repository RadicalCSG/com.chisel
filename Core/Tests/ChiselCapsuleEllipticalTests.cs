using NUnit.Framework;

namespace Chisel.Core.Tests
{
    // Not elliptical, a capsule's x and z diameters are one: turned round, it keeps the larger of the two, as a cylinder
    // does
    [TestFixture]
    public class ChiselCapsuleEllipticalTests
    {
        static ChiselCapsule Elliptical(float diameterX, float diameterZ)
        {
            var settings = ChiselCapsule.DefaultSettings;
            settings.isElliptical = true;
            settings.diameterX    = diameterX;
            settings.diameterZ    = diameterZ;
            return settings;
        }

        [Test]
        public void TurningEllipticalOff_KeepsTheLargerDiameter([Values(true, false)] bool widerInZ)
        {
            var settings = widerInZ ? Elliptical(1, 3) : Elliptical(3, 1);
            settings.IsElliptical = false;
            Assert.That(settings.Validate(), Is.True);

            Assert.That(settings.isElliptical, Is.False);
            Assert.That(settings.diameterX, Is.EqualTo(3), "x");
            Assert.That(settings.diameterZ, Is.EqualTo(3), "z");
        }

        [Test]
        public void TurningEllipticalOn_KeepsTheDiameters()
        {
            var settings = Elliptical(1, 3);
            settings.isElliptical = false;
            settings.IsElliptical = true;

            Assert.That(settings.isElliptical, Is.True);
            Assert.That(settings.diameterX, Is.EqualTo(1), "x");
            Assert.That(settings.diameterZ, Is.EqualTo(3), "z");
        }
    }
}
