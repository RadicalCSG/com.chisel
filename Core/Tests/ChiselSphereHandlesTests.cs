using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;

namespace Chisel.Core.Tests
{
    // A sphere's x and z diameters are its own: its handles change each alone, and its placement preview is a sphere
    [TestFixture]
    public class ChiselSphereHandlesTests
    {
        static ChiselSphereDefinition Sphere(float3 diameter)
        {
            var definition = new ChiselSphereDefinition();
            definition.settings = ChiselSphere.DefaultSettings;
            definition.settings.diameterXYZ = diameter;
            return definition;
        }

        static float3 DraggedAlong(Vector3 axis, float amount, float3 diameter)
        {
            var definition = Sphere(diameter);
            var handles = new FakeChiselHandles { dragAxis = axis, dragAmount = amount };
            definition.OnEdit(handles);
            Assert.That(handles.modified, Is.True, "a handle moves along the drag");
            return definition.settings.diameterXYZ;
        }

        // any handle used left the z diameter as the x diameter
        [Test]
        public void DraggingTheTop_KeepsADifferentZ()
        {
            var diameter = DraggedAlong(Vector3.up, 0.5f, new float3(1, 2, 3));
            Assert.That(diameter.x, Is.EqualTo(1), "x");
            Assert.That(diameter.z, Is.EqualTo(3), "z");
        }

        [Test]
        public void DraggingAlongX_ChangesXAlone()
        {
            var diameter = DraggedAlong(Vector3.right, 0.5f, new float3(1, 2, 3));
            Assert.That(diameter.x, Is.Not.EqualTo(1), "x");
            Assert.That(diameter.y, Is.EqualTo(2), "y");
            Assert.That(diameter.z, Is.EqualTo(3), "z");
        }

        [Test]
        public void DraggingAlongZ_ChangesZAlone()
        {
            var diameter = DraggedAlong(Vector3.forward, 0.5f, new float3(1, 2, 3));
            Assert.That(diameter.x, Is.EqualTo(1), "x");
            Assert.That(diameter.y, Is.EqualTo(2), "y");
            Assert.That(diameter.z, Is.Not.EqualTo(3), "z");
        }

        // what it draws while placed lies on the sphere that fills the placed box
        [Test]
        public void ThePlacementPreview_IsASphere()
        {
            var tool = ScriptableObject.CreateInstance<ChiselSpherePlacementTool>();
            try
            {
                var bounds = new Bounds(new Vector3(2, 1.5f, -1), new Vector3(2, 3, 4));
                var renderer = new FakeChiselHandles();
                tool.OnPaint(renderer, bounds);

                Assert.That(renderer.lines, Is.Not.Empty, "a preview is drawn");
                var top = float.NegativeInfinity;
                foreach (var (from, to) in renderer.lines)
                {
                    foreach (var point in new[] { from, to })
                    {
                        var onUnitSphere = (point - bounds.center);
                        onUnitSphere = new Vector3(onUnitSphere.x / bounds.extents.x, onUnitSphere.y / bounds.extents.y, onUnitSphere.z / bounds.extents.z);
                        Assert.That(onUnitSphere.magnitude, Is.EqualTo(1).Within(1e-4f), $"{point} is on the sphere");
                        top = math.max(top, point.y);
                    }
                }
                Assert.That(top, Is.EqualTo(bounds.max.y).Within(1e-4f), "it reaches the top of the box");
            }
            finally
            {
                Object.DestroyImmediate(tool);
            }
        }
    }
}
