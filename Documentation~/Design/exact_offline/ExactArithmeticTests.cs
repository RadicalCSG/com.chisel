using System;
using System.Numerics;
using Chisel.Core;

// Offline checks of Core/2.Processing/Exact against System.Numerics.BigInteger. Every exact operation is compared with an
// independent arbitrary-precision computation; a failure prints the inputs and the program exits non-zero.
static class ExactArithmeticTests
{
    static int s_Failures;
    static int s_Checks;

    static void Fail(string what)
    {
        s_Failures++;
        if (s_Failures <= 40)
            Console.WriteLine("FAIL: " + what);
    }

    static void Check(bool ok, string what)
    {
        s_Checks++;
        if (!ok) Fail(what);
    }

    static BigInteger Big(Int128 v)
    {
        var lo = new BigInteger(v.lo);
        var hi = new BigInteger((long)v.hi);
        return hi * (BigInteger.One << 64) + lo;
    }

    static BigInteger Big(Int192 v)
    {
        var r = new BigInteger((long)v.l2);
        r = (r << 64) + new BigInteger(v.l1);
        r = (r << 64) + new BigInteger(v.l0);
        return r;
    }

    static unsafe BigInteger Big(BigInt v)
    {
        var r = new BigInteger((long)v.limb[BigInt.kLimbs - 1]);
        for (int i = BigInt.kLimbs - 2; i >= 0; i--)
            r = (r << 64) + new BigInteger(v.limb[i]);
        return r;
    }

    static long RandomLong(Random rng, int bits)
    {
        // magnitude < 2^bits, random sign, with a bias towards edge values
        int pick = rng.Next(10);
        long max = bits >= 63 ? long.MaxValue : (1L << bits) - 1;
        long value;
        if (pick == 0) value = 0;
        else if (pick == 1) value = max;
        else if (pick == 2) value = 1;
        else if (pick == 3) value = (long)(rng.NextDouble() * 1000);
        else
        {
            var bytes = new byte[8];
            rng.NextBytes(bytes);
            value = (long)(BitConverter.ToUInt64(bytes, 0) & (ulong)max);
        }
        return rng.Next(2) == 0 ? value : -value;
    }

    static Int128 RandomInt128(Random rng, int bits)
    {
        var bytes = new byte[16];
        rng.NextBytes(bytes);
        BigInteger magnitude = new BigInteger(bytes.Concat0()) & ((BigInteger.One << bits) - 1);
        if (rng.Next(8) == 0) magnitude = (BigInteger.One << bits) - 1;
        if (rng.Next(8) == 0) magnitude = 0;
        var value = rng.Next(2) == 0 ? magnitude : -magnitude;
        return FromBig(value);
    }

    static byte[] Concat0(this byte[] bytes)
    {
        var r = new byte[bytes.Length + 1];
        Array.Copy(bytes, r, bytes.Length);
        return r;
    }

    static Int128 FromBig(BigInteger value)
    {
        var mod = BigInteger.One << 128;
        var u = ((value % mod) + mod) % mod;
        ulong lo = (ulong)(u & ulong.MaxValue);
        ulong hi = (ulong)((u >> 64) & ulong.MaxValue);
        return new Int128(lo, hi);
    }

    static void TestInt128(Random rng)
    {
        for (int i = 0; i < 200000; i++)
        {
            long a = RandomLong(rng, 63), b = RandomLong(rng, 63);
            var p = Int128.Mul(a, b);
            Check(Big(p) == new BigInteger(a) * b, $"Int128.Mul {a} {b}");
            var x = RandomInt128(rng, 126); var y = RandomInt128(rng, 126);
            Check(Big(Int128.Add(x, y)) == Big(x) + Big(y), "Int128.Add");
            Check(Big(Int128.Sub(x, y)) == Big(x) - Big(y), "Int128.Sub");
            Check(Big(Int128.Negate(x)) == -Big(x), "Int128.Negate");
            Check(x.Sign() == Big(x).Sign, "Int128.Sign");
            // ToDouble within 2^-52 relative
            double d = x.ToDouble();
            var exact = (double)Big(x);
            Check(Math.Abs(d - exact) <= Math.Abs(exact) * 2.3e-16, $"Int128.ToDouble {Big(x)} {d} {exact}");
        }
        // long.MinValue magnitude
        Check(Big(Int128.Mul(long.MinValue, long.MinValue)) == new BigInteger(long.MinValue) * long.MinValue, "MinValue^2");
        Check(Big(Int128.Mul(long.MinValue, -1)) == -new BigInteger(long.MinValue), "MinValue*-1");
    }

    static void TestInt192(Random rng)
    {
        for (int i = 0; i < 200000; i++)
        {
            var x = RandomInt128(rng, 126);
            long b = RandomLong(rng, 63);
            var p = Int192.Mul(x, b);
            Check(Big(p) == Big(x) * b, $"Int192.Mul {Big(x)} {b}");
            var q = Int192.Mul(RandomInt128(rng, 126), RandomLong(rng, 60));
            Check(Big(Int192.Add(p, q)) == Big(p) + Big(q), "Int192.Add");
            Check(Big(Int192.Negate(p)) == -Big(p), "Int192.Negate");
            Check(p.Sign() == Big(p).Sign, "Int192.Sign");
            var small = Int192.FromInt128(x);
            var back = small.ToInt128(out bool fits);
            Check(fits && Big(back) == Big(x), "Int192.ToInt128 roundtrip");
            p.ToInt128(out bool pFits);
            var bp = Big(p);
            bool shouldFit = bp >= -(BigInteger.One << 127) && bp < (BigInteger.One << 127);
            Check(pFits == shouldFit, "Int192.ToInt128 fits flag");
        }
    }

    static void TestBigInt(Random rng)
    {
        for (int i = 0; i < 50000; i++)
        {
            var a = BigInt.FromInt128(RandomInt128(rng, 127));
            var b = BigInt.FromInt128(RandomInt128(rng, 127));
            var c = BigInt.FromInt128(RandomInt128(rng, 120));
            var ab = BigInt.Mul(a, b);
            Check(Big(ab) == Big(a) * Big(b), "BigInt.Mul");
            var abc = BigInt.Mul(ab, c);
            Check(Big(abc) == Big(a) * Big(b) * Big(c), "BigInt.Mul3");
            Check(Big(BigInt.Add(ab, c)) == Big(ab) + Big(c), "BigInt.Add");
            Check(Big(BigInt.Sub(ab, abc)) == Big(ab) - Big(abc), "BigInt.Sub");
            Check(Big(BigInt.Negate(abc)) == -Big(abc), "BigInt.Negate");
            int shift = rng.Next(0, 250);
            Check(Big(BigInt.ShiftLeft(ab, shift)) == Big(ab) * BigInteger.Pow(2, shift), $"BigInt.ShiftLeft {shift}");
            Check(BigInt.Compare(ab, abc) == (Big(ab) - Big(abc)).Sign, "BigInt.Compare");
        }
    }

    // ---- planes, vertices, predicates -------------------------------------------------------------------------------

    struct Rational
    {
        public BigInteger n, d;   // d > 0
        public Rational(BigInteger n, BigInteger d) { if (d.Sign < 0) { n = -n; d = -d; } this.n = n; this.d = d; }
        public int CompareTo(Rational o) => (n * o.d - o.n * d).Sign;
    }

    static ExactPlane RandomPlane(Random rng, bool axisAligned = false)
    {
        if (axisAligned)
        {
            int axis = rng.Next(3);
            double sign = rng.Next(2) == 0 ? 1 : -1;
            double offset = Math.Round((rng.NextDouble() - 0.5) * 200, rng.Next(4));
            return ExactPlane.Quantize(axis == 0 ? sign : 0, axis == 1 ? sign : 0, axis == 2 ? sign : 0, offset);
        }
        double nx = rng.NextDouble() * 2 - 1, ny = rng.NextDouble() * 2 - 1, nz = rng.NextDouble() * 2 - 1;
        double d = (rng.NextDouble() - 0.5) * (rng.Next(3) == 0 ? 2e4 : 200);
        return ExactPlane.Quantize(nx, ny, nz, d);
    }

    static void RationalVertex(ExactPlane p1, ExactPlane p2, ExactPlane p3, out Rational x, out Rational y, out Rational z, out bool valid)
    {
        BigInteger a1 = p1.a, b1 = p1.b, c1 = p1.c, w1 = p1.w;
        BigInteger a2 = p2.a, b2 = p2.b, c2 = p2.c, w2 = p2.w;
        BigInteger a3 = p3.a, b3 = p3.b, c3 = p3.c, w3 = p3.w;
        var D = a1 * (b2 * c3 - b3 * c2) - b1 * (a2 * c3 - a3 * c2) + c1 * (a2 * b3 - a3 * b2);
        valid = !D.IsZero;
        if (!valid) { x = y = z = default; return; }
        // Cramer: N x = -w
        var Dx = (-w1) * (b2 * c3 - b3 * c2) - b1 * ((-w2) * c3 - (-w3) * c2) + c1 * ((-w2) * b3 - (-w3) * b2);
        var Dy = a1 * ((-w2) * c3 - (-w3) * c2) - (-w1) * (a2 * c3 - a3 * c2) + c1 * (a2 * (-w3) - a3 * (-w2));
        var Dz = a1 * (b2 * (-w3) - b3 * (-w2)) - b1 * (a2 * (-w3) - a3 * (-w2)) + (-w1) * (a2 * b3 - a3 * b2);
        x = new Rational(Dx, D); y = new Rational(Dy, D); z = new Rational(Dz, D);
    }

    static int RationalSide(ExactPlane q, Rational x, Rational y, Rational z)
    {
        // q.a x + q.b y + q.c z + q.w, all over the common denominator (same D for all three)
        var D = x.d;
        var value = q.a * x.n + q.b * y.n + q.c * z.n + q.w * D;
        return value.Sign;
    }

    static void TestVertices(Random rng)
    {
        int degenerate = 0, onPlane = 0, near = 0;
        for (int i = 0; i < 100000; i++)
        {
            bool aligned = rng.Next(3) == 0;
            var p1 = RandomPlane(rng, aligned && rng.Next(2) == 0);
            var p2 = RandomPlane(rng, aligned && rng.Next(2) == 0);
            var p3 = RandomPlane(rng, aligned && rng.Next(2) == 0);
            var v = ExactVertex.Intersect(p1, p2, p3);
            RationalVertex(p1, p2, p3, out var rx, out var ry, out var rz, out bool valid);
            Check(v.IsValid == valid, "Intersect validity");
            if (!valid || !v.IsValid) { degenerate++; continue; }
            Check(new Rational(Big(v.X), Big(v.W)).CompareTo(rx) == 0, "Intersect X");
            Check(new Rational(Big(v.Y), Big(v.W)).CompareTo(ry) == 0, "Intersect Y");
            Check(new Rational(Big(v.Z), Big(v.W)).CompareTo(rz) == 0, "Intersect Z");

            // the vertex lies on its own planes
            Check(ExactPredicates.Side(p1, v) == 0 && ExactPredicates.Side(p2, v) == 0 && ExactPredicates.Side(p3, v) == 0, "own planes");

            // random plane
            var q = RandomPlane(rng, aligned && rng.Next(2) == 0);
            Check(ExactPredicates.Side(q, v) == RationalSide(q, rx, ry, rz), "Side random");

            // a plane through the vertex: an integer combination of two of its planes (stays within bounds when the
            // coefficients are halved)
            var through = new ExactPlane((p1.a + p2.a) / 2, (p1.b + p2.b) / 2, (p1.c + p2.c) / 2, (p1.w + p2.w) / 2);
            if (((p1.a + p2.a) & 1) == 0 && ((p1.b + p2.b) & 1) == 0 && ((p1.c + p2.c) & 1) == 0 && ((p1.w + p2.w) & 1) == 0 && through.IsValid)
            {
                onPlane++;
                Check(ExactPredicates.Side(through, v) == 0, "Side through vertex");
            }
            // a plane through the vertex nudged by one unit of w: the exact sign must match the rational one
            var nudged = new ExactPlane(p1.a, p1.b, p1.c, p1.w + (rng.Next(2) == 0 ? 1 : -1));
            near++;
            Check(ExactPredicates.Side(nudged, v) == RationalSide(nudged, rx, ry, rz), "Side nudged");
        }
        Console.WriteLine($"  vertices: {degenerate} degenerate triples, {onPlane} exact on-plane checks, {near} nudged");
    }

    static void TestPlaneRelations(Random rng)
    {
        for (int i = 0; i < 100000; i++)
        {
            var p = RandomPlane(rng, rng.Next(3) == 0);
            // the same plane scaled, flipped, parallel
            Check(ExactPredicates.SamePlane(p, p) == 1, "SamePlane self");
            Check(ExactPredicates.SamePlane(p, p.Flipped()) == -1, "SamePlane flipped");
            var shifted = new ExactPlane(p.a, p.b, p.c, p.w + 1);
            Check(ExactPredicates.SamePlane(p, shifted) == 0, "SamePlane shifted");
            Check(ExactPredicates.NormalsParallel(p, shifted), "parallel shifted");
            // ParallelSide: p lies on shifted's positive side (shifted = p + 1 there)
            Check(ExactPredicates.ParallelSide(shifted, p) == 1, "ParallelSide +1");
            Check(ExactPredicates.ParallelSide(p, p) == 0, "ParallelSide self");
            Check(ExactPredicates.ParallelSide(p.Flipped(), p) == 0, "ParallelSide flipped self");
            var flippedShift = new ExactPlane(-p.a, -p.b, -p.c, -p.w + 5);
            // on p, flippedShift = -(p) + 5 = 5
            Check(ExactPredicates.ParallelSide(flippedShift, p) == 1, "ParallelSide flipped +5");

            // Quantize symmetry
            double nx = rng.NextDouble() * 2 - 1, ny = rng.NextDouble() * 2 - 1, nz = rng.NextDouble() * 2 - 1, d = (rng.NextDouble() - 0.5) * 1e4;
            var q1 = ExactPlane.Quantize(nx, ny, nz, d);
            var q2 = ExactPlane.Quantize(-nx, -ny, -nz, -d);
            Check(q1.a == -q2.a && q1.b == -q2.b && q1.c == -q2.c && q1.w == -q2.w, "Quantize symmetry");
            Check(Math.Abs(q1.a) <= ExactPlane.kMaxNormal && Math.Abs(q1.w) <= ExactPlane.kMaxW, "Quantize bounds");

            // SameLine: e2 = e1 + k p (same line, same side), e3 = -e1 + p (same line, other side), e4 random
            var e1 = RandomPlane(rng, rng.Next(3) == 0);
            if (ExactPredicates.NormalsParallel(p, e1)) continue;
            var e2 = new ExactPlane(e1.a + p.a / 4, e1.b + p.b / 4, e1.c + p.c / 4, e1.w + p.w / 4);
            // e2 is only exactly on the line when p's coefficients are divisible by 4
            bool divisible = p.a % 4 == 0 && p.b % 4 == 0 && p.c % 4 == 0 && p.w % 4 == 0;
            if (divisible && e2.IsValid)
                Check(ExactPredicates.SameLine(p, e1, e2) == 1, "SameLine +");
            var e3 = new ExactPlane(-e1.a, -e1.b, -e1.c, -e1.w);
            Check(ExactPredicates.SameLine(p, e1, e3) == -1, "SameLine -");
            var e4 = RandomPlane(rng);
            // compare with a rank computation
            int expected = RankTwo(p, e1, e4) ? 99 : 0;
            int got = ExactPredicates.SameLine(p, e1, e4);
            if (expected == 0) Check(got == 0, "SameLine random different");
            var e5 = new ExactPlane(e1.a, e1.b, e1.c, e1.w + 1);
            Check(ExactPredicates.SameLine(p, e1, e5) == 0, "SameLine shifted");
        }
    }

    static bool RankTwo(ExactPlane p, ExactPlane q, ExactPlane r)
    {
        BigInteger[][] m = { new BigInteger[] { p.a, p.b, p.c, p.w }, new BigInteger[] { q.a, q.b, q.c, q.w }, new BigInteger[] { r.a, r.b, r.c, r.w } };
        int[][] cols = { new[] { 0, 1, 2 }, new[] { 0, 1, 3 }, new[] { 0, 2, 3 }, new[] { 1, 2, 3 } };
        foreach (var c in cols)
        {
            var det = m[0][c[0]] * (m[1][c[1]] * m[2][c[2]] - m[2][c[1]] * m[1][c[2]])
                    - m[0][c[1]] * (m[1][c[0]] * m[2][c[2]] - m[2][c[0]] * m[1][c[2]])
                    + m[0][c[2]] * (m[1][c[0]] * m[2][c[1]] - m[2][c[0]] * m[1][c[1]]);
            if (!det.IsZero) return false;
        }
        return true;
    }

    static void TestOrientationAndRounding(Random rng)
    {
        int exactHits = 0;
        for (int i = 0; i < 60000; i++)
        {
            // three points on one plane p: each the intersection of p with two more planes
            var p = RandomPlane(rng, rng.Next(2) == 0);
            ExactVertex[] v = new ExactVertex[3];
            Rational[,] r = new Rational[3, 3];
            bool ok = true;
            // sometimes make the points collinear: all on one line (p, e) with different third planes
            bool collinear = rng.Next(4) == 0;
            var e = RandomPlane(rng, rng.Next(2) == 0);
            for (int k = 0; k < 3 && ok; k++)
            {
                var q1 = collinear ? e : RandomPlane(rng, rng.Next(2) == 0);
                var q2 = RandomPlane(rng, rng.Next(2) == 0);
                v[k] = ExactVertex.Intersect(p, q1, q2);
                RationalVertex(p, q1, q2, out r[k, 0], out r[k, 1], out r[k, 2], out bool valid);
                ok = valid && v[k].IsValid;
            }
            if (!ok) continue;
            int axis = p.DominantAxis();
            long dominant = axis == 0 ? p.a : (axis == 1 ? p.b : p.c);
            ExactPredicates.ProjectionAxes(axis, dominant < 0, out int u, out int w);
            int got = ExactPredicates.Orientation(v[0], v[1], v[2], u, w);
            // rational orientation in (u, w)
            Rational U(int k) => r[k, u]; Rational W(int k) => r[k, w];
            var du1 = Sub(U(1), U(0)); var dv1 = Sub(W(1), W(0)); var du2 = Sub(U(2), U(0)); var dv2 = Sub(W(2), W(0));
            var det = Sub(Mul(du1, dv2), Mul(dv1, du2));
            int expected = det.n.Sign;
            Check(got == expected, $"Orientation got {got} expected {expected} collinear={collinear}");
            if (collinear) exactHits++;
            Check(ExactPredicates.OrientationExact(v[0], v[1], v[2], u, w) == expected, "OrientationExact");

            // coordinate compare
            for (int ax = 0; ax < 3; ax++)
                Check(ExactPredicates.CompareCoordinate(v[0], v[1], ax) == r[0, ax].CompareTo(r[1, ax]), "CompareCoordinate");
            Check(ExactPredicates.SamePoint(v[0], v[0]), "SamePoint self");
            bool same = r[0, 0].CompareTo(r[1, 0]) == 0 && r[0, 1].CompareTo(r[1, 1]) == 0 && r[0, 2].CompareTo(r[1, 2]) == 0;
            Check(ExactPredicates.SamePoint(v[0], v[1]) == same, "SamePoint");

            // correct rounding of every coordinate
            for (int ax = 0; ax < 3; ax++)
            {
                var num = v[0].Coordinate(ax);
                float f = ExactPredicates.RoundToFloat(num, v[0].W);
                Check(IsCorrectlyRounded(f, r[0, ax]), $"RoundToFloat {r[0, ax].n}/{r[0, ax].d} -> {f:R}");
            }
        }
        Console.WriteLine($"  orientation: {exactHits} collinear cases");

        // rounding at exact float midpoints and exact floats: X/W constructed from chosen values
        for (int i = 0; i < 100000; i++)
        {
            float f = (float)((rng.NextDouble() - 0.5) * Math.Pow(2, rng.Next(-20, 24)));
            float g = NextUp(f);
            // the midpoint between f and g, as an exact rational n/d
            var mid = ToRational(((double)f + (double)g) * 0.5);
            var scale = new BigInteger(rng.Next(1, 1 << 20)) * (rng.Next(2) == 0 ? 1 : -1);
            var num = mid.n * scale; var den = mid.d * scale;
            if (BigInteger.Abs(num) >= (BigInteger.One << 126) || BigInteger.Abs(den) >= (BigInteger.One << 126)) continue;
            float got = ExactPredicates.RoundToFloat(FromBig(num), FromBig(den));
            Check(IsCorrectlyRounded(got, new Rational(num, den)), $"midpoint rounding {f:R} {g:R} got {got:R}");
            // exactly f
            var exactF = ToRational(f);
            num = exactF.n * scale; den = exactF.d * scale;
            if (BigInteger.Abs(num) >= (BigInteger.One << 126) || BigInteger.Abs(den) >= (BigInteger.One << 126)) continue;
            Check(ExactPredicates.RoundToFloat(FromBig(num), FromBig(den)) == f, "exact float");
            // just above and below the midpoint
            var above = new Rational(mid.n * 1000000007 + 1, mid.d * 1000000007);
            if (BigInteger.Abs(above.n) < (BigInteger.One << 126) && BigInteger.Abs(above.d) < (BigInteger.One << 126))
                Check(IsCorrectlyRounded(ExactPredicates.RoundToFloat(FromBig(above.n), FromBig(above.d)), above), "just above midpoint");
            var below = new Rational(mid.n * 1000000007 - 1, mid.d * 1000000007);
            if (BigInteger.Abs(below.n) < (BigInteger.One << 126) && BigInteger.Abs(below.d) < (BigInteger.One << 126))
                Check(IsCorrectlyRounded(ExactPredicates.RoundToFloat(FromBig(below.n), FromBig(below.d)), below), "just below midpoint");
        }
    }

    static Rational Sub(Rational a, Rational b) => new Rational(a.n * b.d - b.n * a.d, a.d * b.d);
    static Rational Mul(Rational a, Rational b) => new Rational(a.n * b.n, a.d * b.d);

    static Rational ToRational(double value)
    {
        long bits = BitConverter.DoubleToInt64Bits(value);
        int biased = (int)((bits >> 52) & 0x7FF);
        long mantissa = bits & 0xFFFFFFFFFFFFFL;
        int exponent;
        if (biased == 0) exponent = -1074; else { mantissa |= 1L << 52; exponent = biased - 1075; }
        BigInteger n = mantissa; if (value < 0) n = -n;
        if (exponent >= 0) return new Rational(n << exponent, 1);
        return new Rational(n, BigInteger.One << -exponent);
    }

    static float NextUp(float f)
    {
        int bits = BitConverter.ToInt32(BitConverter.GetBytes(f), 0);
        if (f == 0) return float.Epsilon;
        bits += f > 0 ? 1 : -1;
        return BitConverter.ToSingle(BitConverter.GetBytes(bits), 0);
    }

    static float NextDown(float f) => -NextUp(-f);

    // f is the float nearest to r, ties to even
    static bool IsCorrectlyRounded(float f, Rational r)
    {
        var rf = ToRational(f);
        var dist = Abs(Sub(r, rf));
        foreach (var neighbour in new[] { NextUp(f), NextDown(f) })
        {
            var rn = ToRational(neighbour);
            var dn = Abs(Sub(r, rn));
            int c = dist.CompareTo(dn);
            if (c > 0) return false;
            if (c == 0)
            {
                // tie: f must have an even mantissa
                int bits = BitConverter.ToInt32(BitConverter.GetBytes(f), 0);
                if ((bits & 1) != 0) return false;
            }
        }
        return true;
    }

    static Rational Abs(Rational r) => new Rational(BigInteger.Abs(r.n), r.d);

    static int Main()
    {
        var rng = new Random(12345);
        Console.WriteLine("Int128"); TestInt128(rng);
        Console.WriteLine("Int192"); TestInt192(rng);
        Console.WriteLine("BigInt"); TestBigInt(rng);
        Console.WriteLine("Vertices"); TestVertices(rng);
        Console.WriteLine("Plane relations"); TestPlaneRelations(rng);
        Console.WriteLine("Orientation and rounding"); TestOrientationAndRounding(rng);
        Console.WriteLine($"{s_Checks} checks, {s_Failures} failures");
        return s_Failures == 0 ? 0 : 1;
    }
}
