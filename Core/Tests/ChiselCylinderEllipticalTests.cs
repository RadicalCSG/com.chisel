using NUnit.Framework;

namespace Chisel.Core.Tests
{
    // Not elliptical, a cylinder's x and z diameters are one: turned round, each circle keeps the larger of its two, so it
    // doesn't suddenly shrink
    [TestFixture]
    public class ChiselCylinderEllipticalTests
    {
        static ChiselCylinder Elliptical(CylinderShapeType type)
        {
            var settings = ChiselCylinder.DefaultSettings;
            settings.type            = type;
            settings.isElliptical    = true;
            settings.bottomDiameterX = 1;
            settings.bottomDiameterZ = 3;
            settings.topDiameterX    = 2;
            settings.topDiameterZ    = 0.5f;
            return settings;
        }

        [Test]
        public void TurningEllipticalOff_KeepsTheLargerDiameters([Values(CylinderShapeType.Cylinder, CylinderShapeType.ConicalFrustum)] CylinderShapeType type)
        {
            var settings = Elliptical(type);
            settings.IsElliptical = false;

            Assert.That(settings.isElliptical, Is.False);
            Assert.That(settings.bottomDiameterX, Is.EqualTo(3), "bottom x");
            Assert.That(settings.bottomDiameterZ, Is.EqualTo(3), "bottom z");
            Assert.That(settings.topDiameterX,    Is.EqualTo(2), "top x");
            Assert.That(settings.topDiameterZ,    Is.EqualTo(2), "top z");
        }

        [Test]
        public void TurningEllipticalOn_KeepsTheDiameters()
        {
            var settings = Elliptical(CylinderShapeType.ConicalFrustum);
            settings.isElliptical = false;
            settings.IsElliptical = true;

            Assert.That(settings.isElliptical, Is.True);
            Assert.That(settings.bottomDiameterX, Is.EqualTo(1), "bottom x");
            Assert.That(settings.bottomDiameterZ, Is.EqualTo(3), "bottom z");
            Assert.That(settings.topDiameterX,    Is.EqualTo(2), "top x");
            Assert.That(settings.topDiameterZ,    Is.EqualTo(0.5f), "top z");
        }
    }
}
