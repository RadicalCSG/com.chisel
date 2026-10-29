using System.Runtime.CompilerServices;
using Unity.Mathematics;

namespace Chisel.Core
{
    static class CSGMath
    {
        // Already double-precision (PlaneExtensions.Intersection operates on double4).
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static double3 PlaneIntersection(double4 plane0, double4 plane1, double4 plane2)
            => PlaneExtensions.Intersection(plane0, plane1, plane2);

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
