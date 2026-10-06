using System.Runtime.CompilerServices;
using Unity.Mathematics;

namespace Chisel.Core
{
    static class CSGMath
    {
        // Signed distance of a point to a plane = dot(plane, (point,1)), computed in double so the sign
        // is stable within the plane tolerance. plane = (nx,ny,nz,w).
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static double SignedDistance(float4 plane, float4 pointW) => math.dot((double4)plane, (double4)pointW);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static double SignedDistance(float4 plane, float3 point) => math.dot((double4)plane, new double4((double3)point, 1.0));

        // Vertex equality (weld PREDICATE)
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static double SqrDistance(float3 a, float3 b) => math.lengthsq((double3)a - (double3)b);

        // Matches the HashedVertices weld test: distance-squared (in double) below kSqrVertexEqualEpsilon.
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool VerticesEqual(float3 a, float3 b)
            => math.lengthsq((double3)a - (double3)b) < CSGConstants.kSqrVertexEqualEpsilon;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float3 EdgePlaneCrossing(float3 v0, float3 v1, float d0, float d1)
        {
            double3 a = v0, b = v1;
            if (d0 > 0)
            {
                var delta = (double)d0 / ((double)d0 - d1);
                return (float3)(a - ((a - b) * delta));
            } else
            {
                var delta = (double)d1 / ((double)d1 - d0);
                return (float3)(b - ((b - a) * delta));
            }
        }

        // Already double-precision (PlaneExtensions.Intersection operates on double4).
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static double3 PlaneIntersection(double4 plane0, double4 plane1, double4 plane2)
            => PlaneExtensions.Intersection(plane0, plane1, plane2);

        // One vertex-pair term of Newell's polygon-normal accumulation, in double. Matches
        // CalculatePlaneNormal in PerformCSGJob. Accumulate over consecutive (prev,curr) verts, normalize.
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float3 NewellTerm(float3 prev, float3 curr)
        {
            double3 p = prev, c = curr;
            return (float3)new double3((p.y - c.y) * (p.z + c.z),
                                       (p.z - c.z) * (p.x + c.x),
                                       (p.x - c.x) * (p.y + c.y));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static double Orient2D(double2 p, double2 q, double2 r)
            => DifferenceOfProducts(q.y - p.y, r.x - q.x, q.x - p.x, r.y - q.y);

        // Veltkamp/Dekker splitter for double (2^27 + 1). Splits a double into two ~26-bit halves whose
        // sum is exact, so their pairwise products are exactly representable.
        const double kSplitter = 134217729.0;

        // Knuth TwoSum: returns s = fl(a+b) and err such that a + b == s + err EXACTLY.
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void TwoSum(double a, double b, out double s, out double err)
        {
            s = a + b;
            var bb = s - a;
            err = (a - (s - bb)) + (b - bb);
        }

        // Dekker TwoProduct (no FMA dependency): p = fl(a*b) and err such that a*b == p + err EXACTLY.
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void TwoProduct(double a, double b, out double p, out double err)
        {
            p = a * b;
            var ca = kSplitter * a; var ahi = ca - (ca - a); var alo = a - ahi;
            var cb = kSplitter * b; var bhi = cb - (cb - b); var blo = b - bhi;
            err = ((ahi * bhi - p) + ahi * blo + alo * bhi) + alo * blo;
        }

        // a*b - c*d with the rounding errors of both products recovered (compensated). Far more accurate
        // than the naive expression when the two products nearly cancel.
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static double DifferenceOfProducts(double a, double b, double c, double d)
        {
            TwoProduct(a, b, out var p1, out var e1);
            TwoProduct(c, d, out var p2, out var e2);
            TwoSum(p1, -p2, out var s, out var e);
            return s + (e + (e1 - e2));
        }
    }
}
