using System.Runtime.CompilerServices;

namespace Chisel.Core
{
    static class ExactMath
    {
        // Unsigned 64 x 64 -> 128 bit product, returned as (high, low).
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ulong MulHigh(ulong a, ulong b, out ulong low)
        {
            ulong aLo = a & 0xFFFFFFFFUL, aHi = a >> 32;
            ulong bLo = b & 0xFFFFFFFFUL, bHi = b >> 32;
            ulong p0  = aLo * bLo;
            ulong p1  = aLo * bHi;
            ulong p2  = aHi * bLo;
            ulong p3  = aHi * bHi;
            ulong mid = (p0 >> 32) + (p1 & 0xFFFFFFFFUL) + (p2 & 0xFFFFFFFFUL);
            low = (mid << 32) | (p0 & 0xFFFFFFFFUL);
            return p3 + (p1 >> 32) + (p2 >> 32) + (mid >> 32);
        }

        // |value| as an unsigned number. Correct for long.MinValue too (2^63).
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ulong Magnitude(long value)
        {
            return value < 0 ? unchecked((ulong)(-value)) : (ulong)value;
        }
    }

    // 128 bit two's complement.
    struct Int128
    {
        public ulong lo;
        public ulong hi;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Int128(ulong lo, ulong hi) { this.lo = lo; this.hi = hi; }

        public bool IsNegative
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => (long)hi < 0;
        }

        public bool IsZero
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => (lo | hi) == 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int Sign() => IsNegative ? -1 : (IsZero ? 0 : 1);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Int128 FromLong(long value) => new Int128((ulong)value, value < 0 ? ulong.MaxValue : 0UL);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Int128 Negate(Int128 value)
        {
            ulong lo = ~value.lo + 1UL;
            ulong hi = ~value.hi + (lo == 0 ? 1UL : 0UL);
            return new Int128(lo, hi);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Int128 Add(Int128 a, Int128 b)
        {
            ulong lo = a.lo + b.lo;
            ulong hi = a.hi + b.hi + (lo < a.lo ? 1UL : 0UL);
            return new Int128(lo, hi);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Int128 Sub(Int128 a, Int128 b) => Add(a, Negate(b));

        // Signed 64 x 64 -> 128. Always exact: |a*b| <= 2^126.
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Int128 Mul(long a, long b)
        {
            ulong hi = ExactMath.MulHigh(ExactMath.Magnitude(a), ExactMath.Magnitude(b), out ulong lo);
            var result = new Int128(lo, hi);
            return ((a < 0) != (b < 0)) ? Negate(result) : result;
        }

        // a*b - c*d, exact while |a*b| + |c*d| < 2^127.
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Int128 DifferenceOfProducts(long a, long b, long c, long d) => Sub(Mul(a, b), Mul(c, d));

        // Nearest-ish double: at most two roundings, so the relative error is below 2^-52. Only ever used by filters,
        // whose error bounds allow for exactly that.
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public double ToDouble()
        {
            if (IsNegative)
            {
                var magnitude = Negate(this);
                return -((double)magnitude.hi * 18446744073709551616.0 + (double)magnitude.lo);
            }
            return (double)hi * 18446744073709551616.0 + (double)lo;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool Equals(Int128 a, Int128 b) => a.lo == b.lo && a.hi == b.hi;
    }

    // 192 bit two's complement: the accumulator for the side-of-plane predicate, whose terms are 128 x 64 bit products.
    struct Int192
    {
        public ulong l0, l1, l2;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Int192(ulong l0, ulong l1, ulong l2) { this.l0 = l0; this.l1 = l1; this.l2 = l2; }

        public bool IsNegative
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => (long)l2 < 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int Sign() => IsNegative ? -1 : ((l0 | l1 | l2) == 0 ? 0 : 1);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Int192 Negate(Int192 v)
        {
            ulong l0 = ~v.l0 + 1UL;
            ulong c0 = l0 == 0 ? 1UL : 0UL;
            ulong l1 = ~v.l1 + c0;
            ulong c1 = (c0 != 0 && l1 == 0) ? 1UL : 0UL;
            ulong l2 = ~v.l2 + c1;
            return new Int192(l0, l1, l2);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Int192 Add(Int192 a, Int192 b)
        {
            ulong l0 = a.l0 + b.l0;
            ulong c0 = l0 < a.l0 ? 1UL : 0UL;
            ulong s1 = a.l1 + b.l1;
            ulong c1 = s1 < a.l1 ? 1UL : 0UL;
            ulong l1 = s1 + c0;
            c1 += l1 < s1 ? 1UL : 0UL;
            ulong l2 = a.l2 + b.l2 + c1;
            return new Int192(l0, l1, l2);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Int192 FromInt128(Int128 v) => new Int192(v.lo, v.hi, v.IsNegative ? ulong.MaxValue : 0UL);

        // Signed 128 x 64 -> 192. Exact while |x| < 2^127 (always, for an Int128 that is not the minimum value).
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Int192 Mul(Int128 x, long b)
        {
            bool negative = x.IsNegative != (b < 0);
            var magnitude = x.IsNegative ? Int128.Negate(x) : x;
            ulong ub = ExactMath.Magnitude(b);
            ulong h0 = ExactMath.MulHigh(magnitude.lo, ub, out ulong p0);
            ulong h1 = ExactMath.MulHigh(magnitude.hi, ub, out ulong p1);
            ulong r1 = h0 + p1;
            ulong r2 = h1 + (r1 < h0 ? 1UL : 0UL);
            var result = new Int192(p0, r1, r2);
            return negative ? Negate(result) : result;
        }

        // Back to 128 bits. Only called where the value is known to fit; `fits` reports it anyway, so a violated bound
        // is a counted failure instead of a silently wrong vertex.
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Int128 ToInt128(out bool fits)
        {
            // fits when l2 is the sign extension of l1's top bit
            ulong extension = (long)l1 < 0 ? ulong.MaxValue : 0UL;
            fits = l2 == extension;
            return new Int128(l0, l1);
        }
    }

    unsafe struct BigInt
    {
        public const int kLimbs = 8;
        public fixed ulong limb[kLimbs];

        public bool IsNegative
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get { return (long)limb[kLimbs - 1] < 0; }
        }

        public int Sign()
        {
            if (IsNegative)
                return -1;
            for (int i = 0; i < kLimbs; i++)
                if (limb[i] != 0)
                    return 1;
            return 0;
        }

        public static BigInt FromLong(long value)
        {
            var result = new BigInt();
            var fill = value < 0 ? ulong.MaxValue : 0UL;
            result.limb[0] = (ulong)value;
            for (int i = 1; i < kLimbs; i++)
                result.limb[i] = fill;
            return result;
        }

        public static BigInt FromInt128(Int128 value)
        {
            var result = new BigInt();
            var fill = value.IsNegative ? ulong.MaxValue : 0UL;
            result.limb[0] = value.lo;
            result.limb[1] = value.hi;
            for (int i = 2; i < kLimbs; i++)
                result.limb[i] = fill;
            return result;
        }

        public static BigInt FromULong(ulong value)
        {
            var result = new BigInt();
            result.limb[0] = value;
            return result;
        }

        public static BigInt Negate(BigInt value)
        {
            var result = new BigInt();
            ulong carry = 1;
            for (int i = 0; i < kLimbs; i++)
            {
                ulong v = ~value.limb[i] + carry;
                carry = (carry != 0 && v == 0) ? 1UL : 0UL;
                result.limb[i] = v;
            }
            return result;
        }

        public static BigInt Add(BigInt a, BigInt b)
        {
            var result = new BigInt();
            ulong carry = 0;
            for (int i = 0; i < kLimbs; i++)
            {
                ulong s = a.limb[i] + b.limb[i];
                ulong c = s < a.limb[i] ? 1UL : 0UL;
                ulong t = s + carry;
                c += t < s ? 1UL : 0UL;
                result.limb[i] = t;
                carry = c;
            }
            return result;
        }

        public static BigInt Sub(BigInt a, BigInt b) => Add(a, Negate(b));

        // Truncated to 512 bits; exact while the true product fits (every caller stays below 2^400).
        public static BigInt Mul(BigInt a, BigInt b)
        {
            bool negative = a.IsNegative != b.IsNegative;
            var ma = a.IsNegative ? Negate(a) : a;
            var mb = b.IsNegative ? Negate(b) : b;
            var result = new BigInt();
            for (int i = 0; i < kLimbs; i++)
            {
                ulong ai = ma.limb[i];
                if (ai == 0)
                    continue;
                ulong carry = 0;
                for (int j = 0; i + j < kLimbs; j++)
                {
                    ulong high = ExactMath.MulHigh(ai, mb.limb[j], out ulong low);
                    // result[i+j] += low + carry, the overflow goes into high
                    ulong s = result.limb[i + j] + low;
                    high += s < low ? 1UL : 0UL;
                    ulong t = s + carry;
                    high += t < s ? 1UL : 0UL;
                    result.limb[i + j] = t;
                    carry = high;
                }
            }
            return negative ? Negate(result) : result;
        }

        // value * 2^bits, bits >= 0, truncated to 512 bits.
        public static BigInt ShiftLeft(BigInt value, int bits)
        {
            var result = new BigInt();
            int limbShift = bits >> 6;
            int bitShift  = bits & 63;
            for (int i = kLimbs - 1; i >= 0; i--)
            {
                int source = i - limbShift;
                if (source < 0)
                    continue;
                ulong v = value.limb[source] << bitShift;
                if (bitShift != 0 && source > 0)
                    v |= value.limb[source - 1] >> (64 - bitShift);
                result.limb[i] = v;
            }
            return result;
        }

        public static int Compare(BigInt a, BigInt b) => Sub(a, b).Sign();
    }
}
