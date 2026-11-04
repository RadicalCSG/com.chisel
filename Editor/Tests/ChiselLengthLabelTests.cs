using NUnit.Framework;

namespace Chisel.Editors.Tests
{
    // Lengths shown in the scene read as the size they are, not with float noise
    public sealed class ChiselLengthLabelTests
    {
        [Test]
        public void SnappedLengthWithFloatNoise_ShowsRounded()
        {
            Assert.AreEqual("400 cm (Y)", Units.ToDistanceString(DistanceUnit.Centimeters, 4.000028f, "Y"));
            Assert.AreEqual("4 m",        Units.ToDistanceString(DistanceUnit.Meters, 4.000021f));
        }

        [Test]
        public void ATypedLength_IsReadInTheSetUnit_OrTheUnitTypedAfterIt()
        {
            Assert.IsTrue(Units.TryParseDistance("50", DistanceUnit.Centimeters, out var value));
            Assert.AreEqual(0.5f, value, 1e-6f);
            Assert.IsTrue(Units.TryParseDistance("2 m", DistanceUnit.Centimeters, out value));
            Assert.AreEqual(2f, value, 1e-6f);
            Assert.IsTrue(Units.TryParseDistance("1,5", DistanceUnit.Meters, out value));
            Assert.AreEqual(1.5f, value, 1e-6f);
            Assert.IsTrue(Units.TryParseDistance("1 ft", DistanceUnit.Meters, out value));
            Assert.AreEqual(0.3048f, value, 1e-4f);
            Assert.IsFalse(Units.TryParseDistance("abc", DistanceUnit.Meters, out _));
            Assert.IsFalse(Units.TryParseDistance("3 parsecs", DistanceUnit.Meters, out _));
        }

        [Test]
        public void InchesAndFeet_ShowTheirOwnSymbols()
        {
            Assert.AreEqual("\"", Units.GetUnitString(DistanceUnit.Inches));
            Assert.AreEqual("ft", Units.GetUnitString(DistanceUnit.Feet));
        }

        [Test]
        public void SmallLengths_KeepATenthOfAMillimeter()
        {
            Assert.AreEqual("0.0125 m", Units.ToDistanceString(DistanceUnit.Meters, 0.0125f));
        }
    }
}
