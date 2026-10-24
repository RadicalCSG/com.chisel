using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;
using static Chisel.Core.Tests.ChiselTestUtility;

namespace Chisel.Core.Tests
{
    // Three-plane and plane-plane intersection are how CSG recovers vertices from brush planes.
    [TestFixture]
    public class PlaneExtensionsTests
    {
        [Test]
        public void Intersection_ThreeAxisPlanes_MeetAtExpectedPoint()
        {
            var px = new Plane(new Vector3(1, 0, 0), -1f); // x = 1
            var py = new Plane(new Vector3(0, 1, 0), -2f); // y = 2
            var pz = new Plane(new Vector3(0, 0, 1), -3f); // z = 3

            var point = PlaneExtensions.Intersection(px, py, pz);

            AssertApproximately(new Vector3(1, 2, 3), point, 1e-3f);
            Assert.That(IsOnPlane(px, point), Is.True);
            Assert.That(IsOnPlane(py, point), Is.True);
            Assert.That(IsOnPlane(pz, point), Is.True);
        }

        [Test]
        public void Intersection_ParallelPlanes_ReturnsNaN()
        {
            var p1 = new Plane(new Vector3(1, 0, 0), -1f); // x = 1
            var p2 = new Plane(new Vector3(1, 0, 0), -2f); // x = 2 (parallel to p1)
            var p3 = new Plane(new Vector3(0, 1, 0), 0f);

            var point = PlaneExtensions.Intersection(p1, p2, p3);
            Assert.That(float.IsNaN(point.x), Is.True);
        }

        [Test]
        public void Intersection_DoubleAndFloatOverloads_Agree()
        {
            var px = new Plane(new Vector3(1, 0, 0), -1f);
            var py = new Plane(new Vector3(0, 1, 0), -2f);
            var pz = new Plane(new Vector3(0, 0, 1), -3f);

            var floatPoint = PlaneExtensions.Intersection(px, py, pz);
            var doublePoint = PlaneExtensions.Intersection(
                new double4(px.normal.x, px.normal.y, px.normal.z, px.distance),
                new double4(py.normal.x, py.normal.y, py.normal.z, py.distance),
                new double4(pz.normal.x, pz.normal.y, pz.normal.z, pz.distance));

            AssertApproximately((double3)(float3)floatPoint, doublePoint, 1e-3);
            AssertApproximately(new double3(1, 2, 3), doublePoint, 1e-3);
        }

        [Test]
        public void Intersection_DoubleParallelPlanes_ReturnsNaN()
        {
            var point = PlaneExtensions.Intersection(
                new double4(1, 0, 0, -1),
                new double4(1, 0, 0, -2),
                new double4(0, 1, 0, 0));
            Assert.That(double.IsNaN(point.x), Is.True);
        }

        [Test]
        public void PlanePlaneIntersection_TwoOriginPlanes_FindLineOfIntersection()
        {
            // x = 0 plane and y = 0 plane intersect along the z axis.
            PlaneExtensions.PlanePlaneIntersection(out double3 linePoint, out double3 lineVec,
                new double4(1, 0, 0, 0), new double4(0, 1, 0, 0));

            // Direction is parallel to the z axis.
            var dir = math.normalize(lineVec);
            Assert.That(math.abs(dir.z), Is.EqualTo(1.0).Within(1e-6));
            Assert.That(math.abs(dir.x), Is.EqualTo(0.0).Within(1e-6));
            Assert.That(math.abs(dir.y), Is.EqualTo(0.0).Within(1e-6));

            // Any point on the line satisfies x = 0 and y = 0.
            Assert.That(linePoint.x, Is.EqualTo(0.0).Within(1e-6));
            Assert.That(linePoint.y, Is.EqualTo(0.0).Within(1e-6));
        }

        [Test]
        public void NanAccessors_AreNaN()
        {
            Assert.That(float.IsNaN(PlaneExtensions.NanVector.x), Is.True);
            Assert.That(float.IsNaN(PlaneExtensions.NanFloat3.x), Is.True);
            Assert.That(double.IsNaN(PlaneExtensions.NanDouble3.x), Is.True);
        }
    }
}
