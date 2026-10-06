using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;
using static Chisel.Core.Tests.ChiselTestUtility;

namespace Chisel.Core.Tests
{
    // Pure geometry helpers used throughout the CSG pipeline (plane/bounds classification,
    // point-on-segment tests, line-line intersection, plane fitting, plane transforms, ...).
    [TestFixture]
    public class MathExtensionsTests
    {
        [Test]
        public void Vector3Equals_RespectsEpsilon()
        {
            var a = new Vector3(1, 2, 3);
            var b = new Vector3(1, 2, 3.0001f);
            Assert.That(a.Equals(b, 0.001f), Is.True);
            Assert.That(a.Equals(b, 0.00001f), Is.False);
        }

        [Test]
        public void ClosestTangentAxis_PicksAxisAwayFromDominantComponent()
        {
            // When z dominates, the chosen tangent axis is x; otherwise z.
            AssertApproximately(new float3(1, 0, 0), MathExtensions.ClosestTangentAxis(new float3(0, 0, 1)));
            AssertApproximately(new float3(0, 0, 1), MathExtensions.ClosestTangentAxis(new float3(1, 0, 0)));
            AssertApproximately(new float3(0, 0, 1), MathExtensions.ClosestTangentAxis(new float3(0, 1, 0)));
        }

        [Test]
        public void ClosestTangentAxis_IsStableAroundThe45DegreeTie()
        {
            const float k = 0.70710678f;
            var expected = MathExtensions.ClosestTangentAxis(new float3(k, 0, k));
            foreach (var nudge in new[] { 0f, 1e-7f, 1e-5f, 1e-4f, 5e-3f, -1e-4f, -5e-3f })
            {
                AssertApproximately(expected, MathExtensions.ClosestTangentAxis(math.normalize(new float3(k, 0, k + nudge))));
                AssertApproximately(expected, MathExtensions.ClosestTangentAxis(math.normalize(new float3(0, k, k + nudge))));
            }
        }

        [Test]
        public void CalculateTangents_SurviveA45DegreePlaneRefit()
        {
            // Same thing one level up: the basis of a 45 degree plane may not swing around just because the
            // plane was recomputed from slightly different vertices.
            const float k = 0.70710678f;
            MathExtensions.CalculateTangents(new float3(k, 0, k), out float3 tangent, out float3 binormal);
            MathExtensions.CalculateTangents(math.normalize(new float3(k - 1e-4f, 0, k + 1e-4f)), out float3 refitTangent, out float3 refitBinormal);

            Assert.That(math.dot(tangent, refitTangent),   Is.GreaterThan(0.99f));
            Assert.That(math.dot(binormal, refitBinormal), Is.GreaterThan(0.99f));
        }

        [Test]
        public void CalculateTangents_ProducesOrthonormalBasis()
        {
            var normal = math.normalize(new float3(0.3f, 0.7f, 0.65f));
            MathExtensions.CalculateTangents(normal, out float3 tangent, out float3 binormal);

            Assert.That(math.length(tangent),  Is.EqualTo(1f).Within(1e-4f));
            Assert.That(math.length(binormal), Is.EqualTo(1f).Within(1e-4f));
            Assert.That(math.dot(tangent, normal),   Is.EqualTo(0f).Within(1e-4f));
            Assert.That(math.dot(binormal, normal),  Is.EqualTo(0f).Within(1e-4f));
            Assert.That(math.dot(tangent, binormal), Is.EqualTo(0f).Within(1e-4f));
        }

        // -- Plane vs Bounds classification ------------------------------------------------

        static readonly Plane kPlaneY0 = new Plane(new Vector3(0, 1, 0), 0f);

        [Test]
        public void Intersection_Bounds_Above_IsOutside()
        {
            var bounds = new Bounds(new Vector3(0, 1.5f, 0), Vector3.one);
            Assert.That(kPlaneY0.Intersection(bounds), Is.EqualTo(IntersectionResult.Outside));
        }

        [Test]
        public void Intersection_Bounds_Below_IsInside()
        {
            var bounds = new Bounds(new Vector3(0, -1.5f, 0), Vector3.one);
            Assert.That(kPlaneY0.Intersection(bounds), Is.EqualTo(IntersectionResult.Inside));
        }

        [Test]
        public void Intersection_Bounds_Straddling_IsIntersecting()
        {
            var bounds = new Bounds(Vector3.zero, new Vector3(2, 2, 2));
            Assert.That(kPlaneY0.Intersection(bounds), Is.EqualTo(IntersectionResult.Intersecting));
        }

        [Test]
        public void Intersection_MinMax_MatchesBoundsOverload()
        {
            var min = new Vector3(-1, 1, -1);
            var max = new Vector3(1, 2, 1);
            Assert.That(kPlaneY0.Intersection(min, max), Is.EqualTo(IntersectionResult.Outside));
        }

        [Test]
        public void IsInside_And_IsOutside_AreConsistentWithSide()
        {
            var above = new Bounds(new Vector3(0, 1.5f, 0), Vector3.one);
            var below = new Bounds(new Vector3(0, -1.5f, 0), Vector3.one);

            Assert.That(kPlaneY0.IsOutside(above), Is.True);
            Assert.That(kPlaneY0.IsInside(above),  Is.False);

            Assert.That(kPlaneY0.IsInside(below),  Is.True);
            Assert.That(kPlaneY0.IsOutside(below), Is.False);
        }

        // -- Edge/vertex intersection interpolation ---------------------------------------

        [Test]
        public void Intersection_BetweenDistances_FindsZeroCrossing()
        {
            // distance goes from -1 at (0,0,0) to +1 at (2,0,0) -> crossing at the midpoint.
            var point = MathExtensions.Intersection(new Vector3(0, 0, 0), new Vector3(2, 0, 0), -1f, 1f);
            AssertApproximately(new Vector3(1, 0, 0), point);
        }

        [Test]
        public void Intersection_BetweenDistances_IsDirectionIndependent()
        {
            // Cutting direction is normalized internally so the result is stable when swapped.
            var a = MathExtensions.Intersection(new Vector3(0, 0, 0), new Vector3(2, 0, 0), -1f, 3f);
            var b = MathExtensions.Intersection(new Vector3(2, 0, 0), new Vector3(0, 0, 0), 3f, -1f);
            AssertApproximately(a, b);
        }

        // -- Triangle / point helpers -----------------------------------------------------

        [Test]
        public void PointInTriangle_InteriorTrue_ExteriorFalse()
        {
            var a = new float3(0, 0, 0);
            var b = new float3(1, 0, 0);
            var c = new float3(0, 1, 0);
            Assert.That(MathExtensions.PointInTriangle(new float3(0.25f, 0.25f, 0), a, b, c), Is.True);
            Assert.That(MathExtensions.PointInTriangle(new float3(1f, 1f, 0), a, b, c),       Is.False);
        }

        [Test]
        public void GetTriangleArraySize_MatchesTriangularNumbers()
        {
            Assert.That(MathExtensions.GetTriangleArraySize(1), Is.EqualTo(0));
            Assert.That(MathExtensions.GetTriangleArraySize(2), Is.EqualTo(1));
            Assert.That(MathExtensions.GetTriangleArraySize(4), Is.EqualTo(6));
            Assert.That(MathExtensions.GetTriangleArraySize(5), Is.EqualTo(10));
        }

        [Test]
        public void ContainsPoint_InsideAndOutsideSquare()
        {
            var square = new[]
            {
                new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1)
            };
            Assert.That(MathExtensions.ContainsPoint(square, new Vector2(0.5f, 0.5f)), Is.True);
            Assert.That(MathExtensions.ContainsPoint(square, new Vector2(1.5f, 0.5f)), Is.False);
            Assert.That(MathExtensions.ContainsPoint(square, new Vector2(-0.5f, 0.5f)), Is.False);
        }

        // -- Projection -------------------------------------------------------------------

        [Test]
        public void ProjectPointLine_ClampsToSegment()
        {
            var start = new Vector3(0, 0, 0);
            var end   = new Vector3(1, 0, 0);
            AssertApproximately(new Vector3(0.5f, 0, 0), MathExtensions.ProjectPointLine(new Vector3(0.5f, 1, 0), start, end));
            AssertApproximately(new Vector3(1, 0, 0),    MathExtensions.ProjectPointLine(new Vector3(2, 1, 0), start, end));
            AssertApproximately(new Vector3(0, 0, 0),    MathExtensions.ProjectPointLine(new Vector3(-1, 1, 0), start, end));
        }

        [Test]
        public void ProjectPointRay_DoesNotClamp()
        {
            var start     = new Vector3(0, 0, 0);
            var direction = new Vector3(1, 0, 0);
            AssertApproximately(new Vector3(2, 0, 0), MathExtensions.ProjectPointRay(new Vector3(2, 1, 0), start, direction));
        }

        // -- Point-on-segment / distance --------------------------------------------------

        [Test]
        public void IsPointOnLineSegment_DetectsOnAndOff()
        {
            var a = new float3(0, 0, 0);
            var b = new float3(1, 0, 0);
            Assert.That(MathExtensions.IsPointOnLineSegment(new float3(0.5f, 0, 0), a, b, 0.001f, 0.001f), Is.True);
            Assert.That(MathExtensions.IsPointOnLineSegment(new float3(0.5f, 0.5f, 0), a, b, 0.001f, 0.001f), Is.False);
            // endpoints count as on-segment via the vertex epsilon
            Assert.That(MathExtensions.IsPointOnLineSegment(a, a, b, 0.001f, 0.001f), Is.True);
        }

        [Test]
        public void IsPointOnLineSegmentButNotOnVertex_ExcludesEndpoints()
        {
            var a = new float3(0, 0, 0);
            var b = new float3(1, 0, 0);
            Assert.That(MathExtensions.IsPointOnLineSegmentButNotOnVertex(new float3(0.5f, 0, 0), a, b, 0.001f), Is.True);
            Assert.That(MathExtensions.IsPointOnLineSegmentButNotOnVertex(a, a, b, 0.001f), Is.False);
            Assert.That(MathExtensions.IsPointOnLineSegmentButNotOnVertex(b, a, b, 0.001f), Is.False);
        }

        [Test]
        public void IsPointOnLineSegment_ProductionEpsilonsRejectNearbyDistinctVertex()
        {
            var a = new float3(93.90189f, -15.86011f, 38.64029f);   // bottom end of the edge
            var b = new float3(93.90189f, -13.41992f, 38.64029f);   // top end of the edge
            var v = new float3(93.90186f, -15.85986f, 38.65869f);   // neighbour's vertex, 0.0184 off in z

            Assert.That(MathExtensions.IsPointOnLineSegment(v, a, b, CSGConstants.kSqrVertexEqualEpsilon, CSGConstants.kSqrEdgeDistanceEpsilon), Is.False);
            Assert.That(MathExtensions.IsPointOnLineSegmentButNotOnVertex(v, a, b, CSGConstants.kSqrEdgeDistanceEpsilon), Is.False);

            // The un-squared constant is exactly what let it through: 0.0184^2 = 0.00034 < 0.0006.
            Assert.That(MathExtensions.IsPointOnLineSegment(v, a, b, CSGConstants.kVertexEqualEpsilon, CSGConstants.kEdgeIntersectionEpsilon), Is.True);

            // A vertex genuinely on the edge (0.1 mm off, inside the 0.6 mm band) must still split it.
            var on = new float3(93.90199f, -15.0f, 38.64029f);
            Assert.That(MathExtensions.IsPointOnLineSegment(on, a, b, CSGConstants.kSqrVertexEqualEpsilon, CSGConstants.kSqrEdgeDistanceEpsilon), Is.True);
        }

        [Test]
        public void SqrDistanceFromPointToLineSegment_OnSegmentBeyondAndOn()
        {
            var a = new float3(0, 0, 0);
            var b = new float3(1, 0, 0);
            Assert.That(MathExtensions.SqrDistanceFromPointToLineSegment(new float3(0.5f, 0.5f, 0), a, b), Is.EqualTo(0.25f).Within(1e-5f));
            Assert.That(MathExtensions.SqrDistanceFromPointToLineSegment(new float3(2, 0, 0), a, b),       Is.EqualTo(1f).Within(1e-5f));
            Assert.That(MathExtensions.SqrDistanceFromPointToLineSegment(new float3(0.5f, 0, 0), a, b),    Is.EqualTo(0f).Within(1e-5f));
        }

        // -- Line/line intersection -------------------------------------------------------

        [Test]
        public void LineLineIntersection_CrossingSegments_ReturnsPoint()
        {
            var ok = MathExtensions.LineLineIntersection(
                new Vector3(0, 0, 0), new Vector3(2, 0, 0),
                new Vector3(1, -1, 0), new Vector3(1, 1, 0),
                out var intersection, 0.01);
            Assert.That(ok, Is.True);
            AssertApproximately(new Vector3(1, 0, 0), intersection, 1e-3f);
        }

        [Test]
        public void LineLineIntersection_ParallelSegments_ReturnsFalse()
        {
            var ok = MathExtensions.LineLineIntersection(
                new Vector3(0, 0, 0), new Vector3(1, 0, 0),
                new Vector3(0, 1, 0), new Vector3(1, 1, 0),
                out _, 0.01);
            Assert.That(ok, Is.False);
        }

        [Test]
        public void LineLineIntersection_SkewSegments_ReturnsFalse()
        {
            // line 1 is the x axis at z=0; line 2 is vertical at x=0.5, z=1.
            // They are not parallel, but never approach within epsilon -> no intersection.
            var ok = MathExtensions.LineLineIntersection(
                new Vector3(0, 0, 0), new Vector3(1, 0, 0),
                new Vector3(0.5f, -1, 1), new Vector3(0.5f, 1, 1),
                out _, 0.01);
            Assert.That(ok, Is.False);
        }

        // -- Plane fitting ----------------------------------------------------------------

        [Test]
        public void CalculatePlane_FitsPolygonPlane()
        {
            var quad = new[]
            {
                new Vector3(0, 0, 0), new Vector3(1, 0, 0),
                new Vector3(1, 1, 0), new Vector3(0, 1, 0)
            };
            var plane = MathExtensions.CalculatePlane(quad);

            // Plane is the z=0 plane: normal parallel to z, all vertices lie on it.
            Assert.That(Mathf.Abs(plane.normal.z), Is.EqualTo(1f).Within(1e-4f));
            Assert.That(Mathf.Abs(plane.normal.x), Is.EqualTo(0f).Within(1e-4f));
            Assert.That(Mathf.Abs(plane.normal.y), Is.EqualTo(0f).Within(1e-4f));
            foreach (var v in quad)
                Assert.That(IsOnPlane(plane, v), Is.True, $"vertex {v} should lie on the fitted plane");
        }

        // -- Plane transforms -------------------------------------------------------------

        [Test]
        public void Transform_MovesPlaneWithTranslation()
        {
            var plane  = new Plane(new Vector3(0, 1, 0), 0f); // y = 0
            var matrix = Matrix4x4.Translate(new Vector3(0, 5, 0));
            var moved  = matrix.Transform(plane);

            // A point on the original plane, pushed through the matrix, lies on the transformed plane.
            var onOriginal = new Vector3(3, 0, 7);
            var transformed = matrix.MultiplyPoint3x4(onOriginal);
            Assert.That(IsOnPlane(moved, transformed), Is.True);
        }

        [Test]
        public void Transform_HandlesRotationAndTranslation()
        {
            var plane  = new Plane(new Vector3(0, 1, 0), -1f); // y = 1
            var matrix = Matrix4x4.TRS(new Vector3(1, 2, 3), Quaternion.Euler(0, 90, 0), Vector3.one);
            var moved  = matrix.Transform(plane);

            foreach (var p in new[] { new Vector3(2, 1, 5), new Vector3(-4, 1, 8), new Vector3(0, 1, 0) })
            {
                var transformed = matrix.MultiplyPoint3x4(p);
                Assert.That(IsOnPlane(moved, transformed, 1e-3f), Is.True,
                            $"transformed point {transformed} should lie on the transformed plane");
            }
        }

        [Test]
        public void InverseTransform_IsInverseOfTransform()
        {
            var plane  = new Plane(new Vector3(0, 1, 0), 0f);
            var matrix = Matrix4x4.Translate(new Vector3(0, 5, 0));
            var inv    = matrix.InverseTransform(plane);

            // InverseTransform pulls the plane the opposite way: a point on the original plane,
            // pushed through the inverse matrix, lies on the inverse-transformed plane.
            var onOriginal  = new Vector3(3, 0, 7);
            var transformed = matrix.inverse.MultiplyPoint3x4(onOriginal);
            Assert.That(IsOnPlane(inv, transformed), Is.True);
        }

        [Test]
        public void ProjectPointPlane_ProjectsOntoPlane()
        {
            // plane y = 0 (Unity distance 0): (0,5,0) -> (0,0,0)
            AssertApproximately(new double3(0, 0, 0),
                MathExtensions.ProjectPointPlane(new double3(0, 5, 0), new double4(0, 1, 0, 0)));

            // plane y = 2 (Unity distance -2): (0,5,0) -> (0,2,0)
            AssertApproximately(new double3(0, 2, 0),
                MathExtensions.ProjectPointPlane(new double3(0, 5, 0), new double4(0, 1, 0, -2)), 1e-6);
        }
    }
}
