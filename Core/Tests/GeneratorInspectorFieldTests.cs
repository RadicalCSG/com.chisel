using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace Chisel.Core.Tests
{
    [TestFixture]
    public class GeneratorInspectorFieldTests
    {
        // Every generator that applies a smoothing group lets it be edited in the inspector
        [TestCase(typeof(ChiselCylinder))]
        [TestCase(typeof(ChiselCapsule))]
        [TestCase(typeof(ChiselSphere))]
        [TestCase(typeof(ChiselTorus))]
        public void SmoothingGroup_IsShownInTheInspector(Type generator)
        {
            var field = generator.GetField("smoothingGroup", BindingFlags.Public | BindingFlags.Instance);
            Assert.That(field, Is.Not.Null);
            Assert.That(field.IsDefined(typeof(HideInInspector), inherit: true), Is.False);
        }
    }
}
