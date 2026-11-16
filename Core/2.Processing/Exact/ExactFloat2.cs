using System.Runtime.CompilerServices;
using Unity.Collections;
using Unity.Mathematics;

namespace Chisel.Core
{
    // Exact signs of 2D expressions over float coordinates. In double they are almost always decided already: each
    // difference of two floats and each product rounds once, so the error is bounded (Shewchuk's orient2d filter). Where
    // the value is within that bound of zero, the floats are taken as the whole numbers they are times a common power of
    // two (a float is a 24 bit whole number times 2^-149 to 2^104), and the expression is worked out exactly.
    static class ExactFloat2
    {
        // Shewchuk's bound for a - b, with a and b products of rounded differences, is (3 + 16 eps) eps (|a| + |b|) for
        // eps = 2^-53; this rounds it up to 4 eps, for any order of evaluation
        const double kErrorBound = 4.0 / (1L << 53);

        // The sign of (b - a) x (c - a): 1 when c lies left of the line from a to b, -1 when right, 0 when on it
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int Orientation(float2 a, float2 b, float2 c) => CrossSign(a, b, a, c);

        // The sign of (b - a) x (d - c). The double filter is all that is inlined; what it can't decide is worked out
        // apart, rarely.
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int CrossSign(float2 a, float2 b, float2 c, float2 d)
        {
            double ux = (double)b.x - a.x, uy = (double)b.y - a.y;
            double vx = (double)d.x - c.x, vy = (double)d.y - c.y;
            double left = ux * vy, right = uy * vx;
            double value = left - right;
            double bound = kErrorBound * (math.abs(left) + math.abs(right));
            if (value > bound) return 1;
            if (value < -bound) return -1;
            return CrossSignUndecided(a, b, c, d);
        }

        // The sign of (b - a) . (d - c)
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int DotSign(float2 a, float2 b, float2 c, float2 d)
        {
            double ux = (double)b.x - a.x, uy = (double)b.y - a.y;
            double vx = (double)d.x - c.x, vy = (double)d.y - c.y;
            double first = ux * vx, second = uy * vy;
            double value = first + second;
            double bound = kErrorBound * (math.abs(first) + math.abs(second));
            if (value > bound) return 1;
            if (value < -bound) return -1;
            return DotSignUndecided(a, b, c, d);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        static int CrossSignUndecided(float2 a, float2 b, float2 c, float2 d)
        {
            double ux = (double)b.x - a.x, uy = (double)b.y - a.y;
            double vx = (double)d.x - c.x, vy = (double)d.y - c.y;
            double left = ux * vy, right = uy * vx;
            if (Exactly(a, b, c, d, ux, uy, vx, vy) && ProductIsExact(ux, vy, left) && ProductIsExact(uy, vx, right))
                return left > right ? 1 : left < right ? -1 : 0;
            return Exact(a, b, c, d, true);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        static int DotSignUndecided(float2 a, float2 b, float2 c, float2 d)
        {
            double ux = (double)b.x - a.x, uy = (double)b.y - a.y;
            double vx = (double)d.x - c.x, vy = (double)d.y - c.y;
            double first = ux * vx, second = uy * vy;
            if (Exactly(a, b, c, d, ux, uy, vx, vy) && ProductIsExact(ux, vx, first) && ProductIsExact(uy, vy, second))
                return first > -second ? 1 : first < -second ? -1 : 0;
            return Exact(a, b, c, d, false);
        }

        // Where the double values were worked out without rounding (whole numbers and short fractions, as on a grid),
        // their sign is the exact one: two exact products compare exactly. The differences of floats are exact in double
        // when no bits drop (the sum back gives the float again, Knuth's TwoSum with no error); a product is exact when
        // Dekker's split finds no error. Neither overflows or underflows for float inputs.
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static bool Exactly(float2 a, float2 b, float2 c, float2 d, double ux, double uy, double vx, double vy)
        {
            return DifferenceIsExact(b.x, a.x, ux) && DifferenceIsExact(b.y, a.y, uy) &&
                   DifferenceIsExact(d.x, c.x, vx) && DifferenceIsExact(d.y, c.y, vy);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static bool DifferenceIsExact(double x, double y, double difference)
        {
            // TwoSum of x and -y: difference plus its error is x - y
            var back = difference - x;
            var error = (x - (difference - back)) + (-y - back);
            return error == 0 && math.isfinite(difference);
        }

        const double kSplitter = 134217729.0; // 2^27 + 1

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static bool ProductIsExact(double x, double y, double product)
        {
            // Dekker's TwoProduct: product plus its error is x * y
            var cx = kSplitter * x; var xHigh = cx - (cx - x); var xLow = x - xHigh;
            var cy = kSplitter * y; var yHigh = cy - (cy - y); var yLow = y - yHigh;
            var error = xLow * yLow - (((product - xHigh * yHigh) - xLow * yHigh) - xHigh * yLow);
            return error == 0;
        }

        // Exactly: (b - a) x (d - c) when cross, else (b - a) . (d - c). Non-finite coordinates give 0.
        static int Exact(float2 a, float2 b, float2 c, float2 d, bool cross)
        {
            var mantissa = new FixedList128Bytes<long>();
            var exponent = new FixedList64Bytes<int>();
            int lowest = int.MaxValue, highest = int.MinValue;
            for (int k = 0; k < 8; k++)
            {
                var value = k < 2 ? a[k] : (k < 4 ? b[k - 2] : (k < 6 ? c[k - 4] : d[k - 6]));
                var bits  = math.asuint(value);
                var field = (int)((bits >> 23) & 0xFF);
                if (field == 0xFF)
                    return 0;
                long whole = field == 0 ? (bits & 0x7FFFFF) : ((bits & 0x7FFFFF) | 0x800000);
                mantissa.Add((bits & 0x80000000u) != 0 ? -whole : whole);
                exponent.Add(field == 0 ? -149 : field - 150);
                if (whole == 0)
                    continue;
                lowest  = math.min(lowest, exponent[k]);
                highest = math.max(highest, exponent[k]);
            }
            if (lowest == int.MaxValue)
                return 0;
            if (highest - lowest <= 38)
            {
                // |value| < 2^24 * 2^38 = 2^62, so the differences fit a long, and their products and sum an Int128
                var v = new FixedList128Bytes<long>();
                for (int k = 0; k < 8; k++)
                    v.Add(mantissa[k] == 0 ? 0 : mantissa[k] << (exponent[k] - lowest));
                long ux = v[2] - v[0], uy = v[3] - v[1], vx = v[6] - v[4], vy = v[7] - v[5];
                return cross ? Int128.DifferenceOfProducts(ux, vy, uy, vx).Sign()
                             : Int128.Add(Int128.Mul(ux, vx), Int128.Mul(uy, vy)).Sign();
            }
            // |value| < 2^24 * 2^253 = 2^277 (exponents run from -149 to 104), differences < 2^278, products < 2^556,
            // their sum or difference < 2^557: within BigInt's 640 bits
            var ax = Scaled(mantissa[0], exponent[0], lowest); var ay = Scaled(mantissa[1], exponent[1], lowest);
            var bx = Scaled(mantissa[2], exponent[2], lowest); var by = Scaled(mantissa[3], exponent[3], lowest);
            var cx = Scaled(mantissa[4], exponent[4], lowest); var cy = Scaled(mantissa[5], exponent[5], lowest);
            var dx = Scaled(mantissa[6], exponent[6], lowest); var dy = Scaled(mantissa[7], exponent[7], lowest);
            var wx = BigInt.Sub(bx, ax); var wy = BigInt.Sub(by, ay);
            var zx = BigInt.Sub(dx, cx); var zy = BigInt.Sub(dy, cy);
            return cross ? BigInt.Sub(BigInt.Mul(wx, zy), BigInt.Mul(wy, zx)).Sign()
                         : BigInt.Add(BigInt.Mul(wx, zx), BigInt.Mul(wy, zy)).Sign();
        }

        static BigInt Scaled(long mantissa, int exponent, int lowest)
        {
            return mantissa == 0 ? BigInt.FromLong(0) : BigInt.ShiftLeft(BigInt.FromLong(mantissa), exponent - lowest);
        }
    }
}
