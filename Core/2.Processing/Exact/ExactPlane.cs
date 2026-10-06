using System;
using System.Runtime.CompilerServices;

namespace Chisel.Core
{
    struct ExactPlane
    {
        public const double kNormalScale  = 1048576.0;      // 2^20: a snapped plane's a, b, c are its unit normal times this
        public const double kOffsetScale  = 16384.0;        // 2^14: its offset is snapped to multiples of 1 / this
        public const double kMaxDistance  = 16777216.0;     // 2^24, |d| of a unit plane
        public const long   kMaxNormal    = 1L << 32;
        public const long   kMaxW         = 1L << 56;

        public long a, b, c, w;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ExactPlane(long a, long b, long c, long w) { this.a = a; this.b = b; this.c = c; this.w = w; }

        public bool IsValid
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => (a | b | c) != 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ExactPlane Flipped() => new ExactPlane(-a, -b, -c, -w);

        // Round half away from zero, so Quantize(-p) == -Quantize(p) exactly. Values at or above 2^52 are integers
        // already, and adding 0.5 to them would round, so they are only truncated.
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static long RoundHalfAway(double value)
        {
            var magnitude = Math.Abs(value);
            double rounded = magnitude >= 4503599627370496.0 ? magnitude : Math.Floor(magnitude + 0.5);
            return value < 0 ? -(long)rounded : (long)rounded;
        }

        public static ExactPlane Quantize(double nx, double ny, double nz, double d)
        {
            double length = Math.Sqrt(nx * nx + ny * ny + nz * nz);
            if (!(length > 0) || double.IsInfinity(length) || double.IsNaN(d))
                return default;
            nx /= length; ny /= length; nz /= length; d /= length;
            if (!(Math.Abs(d) <= kMaxDistance))
                return default;
            // w is the snapped offset in the normal's scale: an integer, since kNormalScale is a multiple of kOffsetScale
            var plane = new ExactPlane(RoundHalfAway(nx * kNormalScale), RoundHalfAway(ny * kNormalScale),
                                       RoundHalfAway(nz * kNormalScale),
                                       RoundHalfAway(d * kOffsetScale) * (long)(kNormalScale / kOffsetScale));
            return plane;
        }

        public static ExactPlane AsGiven(double nx, double ny, double nz, double d)
        {
            double largest = Math.Max(Math.Abs(nx), Math.Max(Math.Abs(ny), Math.Abs(nz)));
            if (!(largest > 0) || double.IsInfinity(largest) || double.IsNaN(d) || double.IsInfinity(d))
                return default;
            // the plane's distance from the origin is |d| / |n|, and |n| >= largest
            if (!(Math.Abs(d) <= kMaxDistance * largest))
                return default;
            double scale = 1;
            while (largest * scale >= 4294967296.0) scale *= 0.5;   // 2^32
            while (largest * scale <  2147483648.0) scale *= 2.0;   // 2^31
            // |w| = |d| * scale <= kMaxDistance * largest * scale < 2^24 * 2^32 = kMaxW
            return new ExactPlane(RoundHalfAway(nx * scale), RoundHalfAway(ny * scale), RoundHalfAway(nz * scale), RoundHalfAway(d * scale));
        }
        // The largest normal component, 0 = x, 1 = y, 2 = z; ties go to the lowest axis. Decides the projection used for
        // 2D orientation, and nothing else.
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int DominantAxis()
        {
            ulong ax = ExactMath.Magnitude(a), ay = ExactMath.Magnitude(b), az = ExactMath.Magnitude(c);
            if (ax >= ay && ax >= az) return 0;
            if (ay >= az) return 1;
            return 2;
        }
    }

    struct ExactVertex
    {
        public Int128 X, Y, Z, W;
        public double Xd, Yd, Zd, Wd;   // the same, converted with relative error below 2^-52 (filters only)
        public int    signW;            // -1 or 1

        public bool IsValid
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => signW != 0;
        }

        // Approximate position, for bounding boxes and heuristics that never decide anything.
        public double ApproxX { [MethodImpl(MethodImplOptions.AggressiveInlining)] get => Xd / Wd; }
        public double ApproxY { [MethodImpl(MethodImplOptions.AggressiveInlining)] get => Yd / Wd; }
        public double ApproxZ { [MethodImpl(MethodImplOptions.AggressiveInlining)] get => Zd / Wd; }

        // Invalid (signW == 0) when the three normals are linearly dependent: the planes meet in no single point.
        public static ExactVertex Intersect(in ExactPlane p1, in ExactPlane p2, in ExactPlane p3)
        {
            // Cofactors of the normal matrix N (rows = planes). Each is a difference of two 64 bit products, <= 2^65.
            var c11 = Int128.DifferenceOfProducts(p2.b, p3.c, p3.b, p2.c);
            var c12 = Int128.DifferenceOfProducts(p3.a, p2.c, p2.a, p3.c);
            var c13 = Int128.DifferenceOfProducts(p2.a, p3.b, p3.a, p2.b);
            var c21 = Int128.DifferenceOfProducts(p3.b, p1.c, p1.b, p3.c);
            var c22 = Int128.DifferenceOfProducts(p1.a, p3.c, p3.a, p1.c);
            var c23 = Int128.DifferenceOfProducts(p3.a, p1.b, p1.a, p3.b);
            var c31 = Int128.DifferenceOfProducts(p1.b, p2.c, p2.b, p1.c);
            var c32 = Int128.DifferenceOfProducts(p2.a, p1.c, p1.a, p2.c);
            var c33 = Int128.DifferenceOfProducts(p1.a, p2.b, p2.a, p1.b);

            var d = Int192.Add(Int192.Add(Int192.Mul(c11, p1.a), Int192.Mul(c12, p1.b)), Int192.Mul(c13, p1.c));
            var W = d.ToInt128(out _);  // < 2^98.6 by the plane bounds
            if (W.IsZero)
                return default;

            // N x = -w  =>  x = -adj(N) w / det(N), adj(N) = transpose of the cofactor matrix
            var x = Int192.Add(Int192.Add(Int192.Mul(c11, p1.w), Int192.Mul(c21, p2.w)), Int192.Mul(c31, p3.w));
            var y = Int192.Add(Int192.Add(Int192.Mul(c12, p1.w), Int192.Mul(c22, p2.w)), Int192.Mul(c32, p3.w));
            var z = Int192.Add(Int192.Add(Int192.Mul(c13, p1.w), Int192.Mul(c23, p2.w)), Int192.Mul(c33, p3.w));

            var vertex = new ExactVertex
            {
                X = Int128.Negate(x.ToInt128(out _)),
                Y = Int128.Negate(y.ToInt128(out _)),
                Z = Int128.Negate(z.ToInt128(out _)),
                W = W
            };
            vertex.Xd = vertex.X.ToDouble();
            vertex.Yd = vertex.Y.ToDouble();
            vertex.Zd = vertex.Z.ToDouble();
            vertex.Wd = vertex.W.ToDouble();
            vertex.signW = W.IsNegative ? -1 : 1;
            return vertex;
        }

        // The point (X/W, Y/W, Z/W) from coordinates an Intersect made before (ExactCSGCapture keeps only these four).
        // Invalid when W is zero.
        public static ExactVertex FromHomogeneous(Int128 X, Int128 Y, Int128 Z, Int128 W)
        {
            if (W.IsZero)
                return default;
            return new ExactVertex
            {
                X = X, Y = Y, Z = Z, W = W,
                Xd = X.ToDouble(), Yd = Y.ToDouble(), Zd = Z.ToDouble(), Wd = W.ToDouble(),
                signW = W.IsNegative ? -1 : 1
            };
        }

        // Homogeneous coordinate by axis: 0 = X, 1 = Y, 2 = Z, 3 = W.
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Int128 Coordinate(int axis) => axis == 0 ? X : (axis == 1 ? Y : (axis == 2 ? Z : W));

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public double CoordinateD(int axis) => axis == 0 ? Xd : (axis == 1 ? Yd : (axis == 2 ? Zd : Wd));
    }

    struct ExactAffine
    {
        double c0x, c0y, c0z, c1x, c1y, c1z, c2x, c2y, c2z, tx, ty, tz;   // the columns of A, and t
        double k0x, k0y, k0z, k1x, k1y, k1z, k2x, k2y, k2z;               // the columns of sign(det A) cof(A)
        double absDet;

        public bool IsValid
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => absDet > 0 && !double.IsInfinity(absDet);
        }

        public static ExactAffine Create(double c0x, double c0y, double c0z, double c1x, double c1y, double c1z,
                                         double c2x, double c2y, double c2z, double tx, double ty, double tz)
        {
            var m = new ExactAffine
            {
                c0x = c0x, c0y = c0y, c0z = c0z, c1x = c1x, c1y = c1y, c1z = c1z, c2x = c2x, c2y = c2y, c2z = c2z,
                tx = tx, ty = ty, tz = tz
            };
            // columns of the cofactor matrix: c1 x c2, c2 x c0, c0 x c1
            double x12x = c1y * c2z - c1z * c2y, x12y = c1z * c2x - c1x * c2z, x12z = c1x * c2y - c1y * c2x;
            double x20x = c2y * c0z - c2z * c0y, x20y = c2z * c0x - c2x * c0z, x20z = c2x * c0y - c2y * c0x;
            double x01x = c0y * c1z - c0z * c1y, x01y = c0z * c1x - c0x * c1z, x01z = c0x * c1y - c0y * c1x;
            double det  = c0x * x12x + c0y * x12y + c0z * x12z;
            double sign = det < 0 ? -1.0 : 1.0;
            m.k0x = sign * x12x; m.k0y = sign * x12y; m.k0z = sign * x12z;
            m.k1x = sign * x20x; m.k1y = sign * x20y; m.k1z = sign * x20z;
            m.k2x = sign * x01x; m.k2y = sign * x01y; m.k2z = sign * x01z;
            m.absDet = double.IsNaN(det) ? 0 : Math.Abs(det);
            return m;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void TransformPoint(double x, double y, double z, out double ox, out double oy, out double oz)
        {
            ox = c0x * x + c1x * y + c2x * z + tx;
            oy = c0y * x + c1y * y + c2y * z + ty;
            oz = c0z * x + c1z * y + c2z * z + tz;
        }

        // The plane n.p + d = 0 of the brush's own space, in the transformed space, scaled by |det A| (not normalized).
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void TransformPlane(double nx, double ny, double nz, double d, out double ox, out double oy, out double oz, out double od)
        {
            ox = nx * k0x + ny * k1x + nz * k2x;
            oy = nx * k0y + ny * k1y + nz * k2y;
            oz = nx * k0z + ny * k1z + nz * k2z;
            od = absDet * d - (ox * tx + oy * ty + oz * tz);
        }
    }

    static class ExactPredicates
    {
        // 2^-53, the unit roundoff of a double
        const double kUnitRoundoff = 1.1102230246251565e-16;

        // Which side of `plane` the vertex lies on: +1 outside (positive), 0 on it, -1 inside.
        //   sign(plane(x)) = sign(a X + b Y + c Z + w W) * sign(W), and the sum is below 2^157.
        public static int Side(in ExactPlane plane, in ExactVertex vertex)
        {
            // Each term carries at most three roundings (the conversion of X, of the coefficient, and the product) and the
            // sum two more; 10 u of the absolute sum covers all of it with room to spare.
            double t0 = plane.a * vertex.Xd;
            double t1 = plane.b * vertex.Yd;
            double t2 = plane.c * vertex.Zd;
            double t3 = (double)plane.w * vertex.Wd;
            double sum = (t0 + t1) + (t2 + t3);
            double magnitude = Math.Abs(t0) + Math.Abs(t1) + Math.Abs(t2) + Math.Abs(t3);
            if (magnitude == 0)
                return 0;   // every term is exactly zero
            if (Math.Abs(sum) > magnitude * (10 * kUnitRoundoff))
                return (sum > 0 ? 1 : -1) * vertex.signW;

            var accumulator = Int192.Add(Int192.Add(Int192.Mul(vertex.X, plane.a), Int192.Mul(vertex.Y, plane.b)),
                                         Int192.Add(Int192.Mul(vertex.Z, plane.c), Int192.Mul(vertex.W, plane.w)));
            return accumulator.Sign() * vertex.signW;
        }

        // Sign of the scalar product of `plane`'s normal with the direction of the line where `onPlane` and `edge` meet,
        // d = n(onPlane) x n(edge). Positive: the plane's value grows moving along d.
        public static int DirectionSign(in ExactPlane plane, in ExactPlane onPlane, in ExactPlane edge)
        {
            // d components are 2x2 minors (< 2^65); the dot product is below 3 * 2^32 * 2^65
            var dx = Int128.DifferenceOfProducts(onPlane.b, edge.c, onPlane.c, edge.b);
            var dy = Int128.DifferenceOfProducts(onPlane.c, edge.a, onPlane.a, edge.c);
            var dz = Int128.DifferenceOfProducts(onPlane.a, edge.b, onPlane.b, edge.a);
            var dot = Int192.Add(Int192.Add(Int192.Mul(dx, plane.a), Int192.Mul(dy, plane.b)), Int192.Mul(dz, plane.c));
            return dot.Sign();
        }

        // Determinant of three normals, sign only. Zero when the planes do not meet in a single point.
        public static int NormalDeterminantSign(in ExactPlane p1, in ExactPlane p2, in ExactPlane p3)
        {
            var c11 = Int128.DifferenceOfProducts(p2.b, p3.c, p3.b, p2.c);
            var c12 = Int128.DifferenceOfProducts(p3.a, p2.c, p2.a, p3.c);
            var c13 = Int128.DifferenceOfProducts(p2.a, p3.b, p3.a, p2.b);
            return Int192.Add(Int192.Add(Int192.Mul(c11, p1.a), Int192.Mul(c12, p1.b)), Int192.Mul(c13, p1.c)).Sign();
        }

        // Whether two normals are parallel (either way round).
        public static bool NormalsParallel(in ExactPlane p, in ExactPlane q)
        {
            return Int128.DifferenceOfProducts(p.b, q.c, p.c, q.b).IsZero &&
                   Int128.DifferenceOfProducts(p.c, q.a, p.a, q.c).IsZero &&
                   Int128.DifferenceOfProducts(p.a, q.b, p.b, q.a).IsZero;
        }

        // +1 when q is p (a positive multiple), -1 when q is p flipped (a negative multiple), 0 otherwise.
        public static int SamePlane(in ExactPlane p, in ExactPlane q)
        {
            if (!NormalsParallel(p, q))
                return 0;
            if (!Int128.DifferenceOfProducts(p.a, q.w, q.a, p.w).IsZero ||
                !Int128.DifferenceOfProducts(p.b, q.w, q.b, p.w).IsZero ||
                !Int128.DifferenceOfProducts(p.c, q.w, q.c, p.w).IsZero)
                return 0;
            var dot = Int192.Add(Int192.Add(Int192.FromInt128(Int128.Mul(p.a, q.a)), Int192.FromInt128(Int128.Mul(p.b, q.b))),
                                 Int192.FromInt128(Int128.Mul(p.c, q.c)));
            return dot.Sign();
        }

        public static int ParallelSide(in ExactPlane q, in ExactPlane p)
        {
            int k = p.DominantAxis();
            long pk = k == 0 ? p.a : (k == 1 ? p.b : p.c);
            long qk = k == 0 ? q.a : (k == 1 ? q.b : q.c);
            var value = Int128.DifferenceOfProducts(q.w, pk, qk, p.w);
            return value.Sign() * (pk < 0 ? -1 : 1);
        }

        public static int SameLine(in ExactPlane p, in ExactPlane e1, in ExactPlane e2)
        {
            if (NormalDeterminantSign(p, e1, e2) != 0)
                return 0;
            // columns (a, b, w), (a, c, w), (b, c, w)
            if (Minor(p.a, p.b, p.w, e1.a, e1.b, e1.w, e2.a, e2.b, e2.w) != 0) return 0;
            if (Minor(p.a, p.c, p.w, e1.a, e1.c, e1.w, e2.a, e2.c, e2.w) != 0) return 0;
            if (Minor(p.b, p.c, p.w, e1.b, e1.c, e1.w, e2.b, e2.c, e2.w) != 0) return 0;

            // Same line. e2's direction d2 = n_p x n_e2 is alpha times d1 = n_p x n_e1; compare signs on the largest
            // component of d1.
            var d1x = Int128.DifferenceOfProducts(p.b, e1.c, p.c, e1.b);
            var d1y = Int128.DifferenceOfProducts(p.c, e1.a, p.a, e1.c);
            var d1z = Int128.DifferenceOfProducts(p.a, e1.b, p.b, e1.a);
            var d2x = Int128.DifferenceOfProducts(p.b, e2.c, p.c, e2.b);
            var d2y = Int128.DifferenceOfProducts(p.c, e2.a, p.a, e2.c);
            var d2z = Int128.DifferenceOfProducts(p.a, e2.b, p.b, e2.a);
            if (!d1x.IsZero) return d1x.Sign() * d2x.Sign();
            if (!d1y.IsZero) return d1y.Sign() * d2y.Sign();
            if (!d1z.IsZero) return d1z.Sign() * d2z.Sign();
            return 0;   // p and e1 are parallel: not a line at all
        }

        // 3x3 determinant sign of long entries whose products stay below 2^125 per term.
        static int Minor(long a1, long b1, long c1, long a2, long b2, long c2, long a3, long b3, long c3)
        {
            var m1 = Int128.DifferenceOfProducts(b2, c3, b3, c2);
            var m2 = Int128.DifferenceOfProducts(a2, c3, a3, c2);
            var m3 = Int128.DifferenceOfProducts(a2, b3, a3, b2);
            var sum = Int192.Add(Int192.Add(Int192.Mul(m1, a1), Int192.Negate(Int192.Mul(m2, b1))), Int192.Mul(m3, c1));
            return sum.Sign();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int CompareAlongLine(in ExactVertex pointI, in ExactPlane planeJ, int planeJDirectionSign)
        {
            return Side(planeJ, pointI) * planeJDirectionSign;
        }

        // Whether two points are the same point: X1 W2 == X2 W1 for all three coordinates (Int256 products).
        public static bool SamePoint(in ExactVertex p, in ExactVertex q)
        {
            // cheap rejection on the approximations first: equal points have equal ratios up to the filter error
            if (Math.Abs(p.Xd * q.Wd - q.Xd * p.Wd) > 16 * kUnitRoundoff * (Math.Abs(p.Xd * q.Wd) + Math.Abs(q.Xd * p.Wd)) ||
                Math.Abs(p.Yd * q.Wd - q.Yd * p.Wd) > 16 * kUnitRoundoff * (Math.Abs(p.Yd * q.Wd) + Math.Abs(q.Yd * p.Wd)) ||
                Math.Abs(p.Zd * q.Wd - q.Zd * p.Wd) > 16 * kUnitRoundoff * (Math.Abs(p.Zd * q.Wd) + Math.Abs(q.Zd * p.Wd)))
                return false;
            var pW = BigInt.FromInt128(p.W);
            var qW = BigInt.FromInt128(q.W);
            if (BigInt.Compare(BigInt.Mul(BigInt.FromInt128(p.X), qW), BigInt.Mul(BigInt.FromInt128(q.X), pW)) != 0) return false;
            if (BigInt.Compare(BigInt.Mul(BigInt.FromInt128(p.Y), qW), BigInt.Mul(BigInt.FromInt128(q.Y), pW)) != 0) return false;
            if (BigInt.Compare(BigInt.Mul(BigInt.FromInt128(p.Z), qW), BigInt.Mul(BigInt.FromInt128(q.Z), pW)) != 0) return false;
            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ProjectionAxes(int dominantAxis, bool dominantNegative, out int axisU, out int axisV)
        {
            // drop x keep (y, z); drop y keep (z, x); drop z keep (x, y): cyclic, so u x v = +dominant axis
            axisU = (dominantAxis + 1) % 3;
            axisV = (dominantAxis + 2) % 3;
            if (dominantNegative)
            {
                var t = axisU; axisU = axisV; axisV = t;
            }
        }

        public static int Orientation(in ExactVertex a, in ExactVertex b, in ExactVertex c, int axisU, int axisV)
        {
            double ua = a.CoordinateD(axisU) / a.Wd, va = a.CoordinateD(axisV) / a.Wd;
            double ub = b.CoordinateD(axisU) / b.Wd, vb = b.CoordinateD(axisV) / b.Wd;
            double uc = c.CoordinateD(axisU) / c.Wd, vc = c.CoordinateD(axisV) / c.Wd;

            double du1 = ub - ua, dv1 = vb - va, du2 = uc - ua, dv2 = vc - va;
            double det = du1 * dv2 - dv1 * du2;
            double m = Math.Max(Math.Max(Math.Max(Math.Abs(ua), Math.Abs(va)), Math.Max(Math.Abs(ub), Math.Abs(vb))),
                                Math.Max(Math.Abs(uc), Math.Abs(vc)));
            double spread = Math.Abs(du1) + Math.Abs(dv1) + Math.Abs(du2) + Math.Abs(dv2);
            double bound = 64 * kUnitRoundoff * (m * spread + Math.Abs(du1 * dv2) + Math.Abs(dv1 * du2));
            if (Math.Abs(det) > bound && !double.IsNaN(det))
                return det > 0 ? 1 : -1;
            return OrientationExact(a, b, c, axisU, axisV);
        }

        public static int OrientationExact(in ExactVertex a, in ExactVertex b, in ExactVertex c, int axisU, int axisV)
        {
            var Ua = BigInt.FromInt128(a.Coordinate(axisU)); var Va = BigInt.FromInt128(a.Coordinate(axisV)); var Wa = BigInt.FromInt128(a.W);
            var Ub = BigInt.FromInt128(b.Coordinate(axisU)); var Vb = BigInt.FromInt128(b.Coordinate(axisV)); var Wb = BigInt.FromInt128(b.W);
            var Uc = BigInt.FromInt128(c.Coordinate(axisU)); var Vc = BigInt.FromInt128(c.Coordinate(axisV)); var Wc = BigInt.FromInt128(c.W);
            // det = Ua (Vb Wc - Wb Vc) - Va (Ub Wc - Wb Uc) + Wa (Ub Vc - Vb Uc)
            var m1 = BigInt.Sub(BigInt.Mul(Vb, Wc), BigInt.Mul(Wb, Vc));
            var m2 = BigInt.Sub(BigInt.Mul(Ub, Wc), BigInt.Mul(Wb, Uc));
            var m3 = BigInt.Sub(BigInt.Mul(Ub, Vc), BigInt.Mul(Vb, Uc));
            var det = BigInt.Add(BigInt.Sub(BigInt.Mul(Ua, m1), BigInt.Mul(Va, m2)), BigInt.Mul(Wa, m3));
            return det.Sign() * a.signW * b.signW * c.signW;
        }

        // Compares one coordinate of two points exactly: sign(p[axis] - q[axis]).
        public static int CompareCoordinate(in ExactVertex p, in ExactVertex q, int axis)
        {
            double pa = p.CoordinateD(axis) / p.Wd, qa = q.CoordinateD(axis) / q.Wd;
            double difference = pa - qa;
            if (Math.Abs(difference) > 16 * kUnitRoundoff * (Math.Abs(pa) + Math.Abs(qa)))
                return difference > 0 ? 1 : -1;
            // p/pW - q/qW = (p qW - q pW) / (pW qW)
            var value = BigInt.Sub(BigInt.Mul(BigInt.FromInt128(p.Coordinate(axis)), BigInt.FromInt128(q.W)),
                                   BigInt.Mul(BigInt.FromInt128(q.Coordinate(axis)), BigInt.FromInt128(p.W)));
            return value.Sign() * p.signW * q.signW;
        }

        public static float RoundToFloat(Int128 numerator, Int128 denominator)
        {
            if (numerator.IsZero)
                return 0f;
            double estimate = numerator.ToDouble() / denominator.ToDouble();   // relative error below 3 u
            float nearest = (float)estimate;
            if (float.IsInfinity(nearest) || nearest == 0f)
                return nearest;     // outside the range a tree ever uses; no exactness is claimed there

            // The float interval rounding to `nearest` is bounded by the midpoints to its neighbours. When the estimate is
            // clearly inside it, the exact value is too.
            double below = MidpointToNeighbour(nearest, false);
            double above = MidpointToNeighbour(nearest, true);
            double slack = 4 * kUnitRoundoff * Math.Abs(estimate);
            if (estimate - below > slack && above - estimate > slack)
                return nearest;

            // Otherwise compare the exact value with the midpoint it is close to.
            double midpoint = (estimate - below) <= (above - estimate) ? below : above;
            int side = CompareWithDouble(numerator, denominator, midpoint);
            if (side == 0)
                return RoundTieToEven(midpoint);
            if (midpoint == below)
                return side < 0 ? NextFloat(nearest, false) : nearest;
            return side > 0 ? NextFloat(nearest, true) : nearest;
        }

        // The largest float at or below X/W (up == false) or the smallest at or above it (up == true), exactly: bounds made
        // of these contain the exact points they are made from.
        public static float DirectedToFloat(Int128 numerator, Int128 denominator, bool up)
        {
            float nearest = RoundToFloat(numerator, denominator);
            if (float.IsInfinity(nearest))
                return nearest;     // outside the range a tree ever uses
            int side;               // sign(X/W - nearest)
            if (nearest == 0f)
                side = numerator.IsZero ? 0 : (numerator.IsNegative == denominator.IsNegative ? 1 : -1);
            else
                side = CompareWithDouble(numerator, denominator, nearest);
            if (up)
                return side > 0 ? NextFloat(nearest, true) : nearest;
            return side < 0 ? NextFloat(nearest, false) : nearest;
        }

        // The midpoint between `value` and its neighbour towards +inf (up) or -inf (down), exactly representable as a
        // double because floats carry 24 bits.
        static double MidpointToNeighbour(float value, bool up)
        {
            float neighbour = NextFloat(value, up);
            return ((double)value + (double)neighbour) * 0.5;
        }

        static unsafe float NextFloat(float value, bool up)
        {
            int bits = *(int*)&value;
            if (value == 0f)
                bits = up ? 1 : unchecked((int)0x80000001);
            else if ((value > 0) == up)
                bits++;
            else
                bits--;
            return *(float*)&bits;
        }

        // The tie at an exact midpoint goes to the float with an even mantissa.
        static unsafe float RoundTieToEven(double midpoint)
        {
            float low  = (float)midpoint;   // IEEE conversion already rounds half to even
            return low;
        }

        // sign(numerator / denominator - value), exactly, for a double value with at most 53 significant bits.
        static unsafe int CompareWithDouble(Int128 numerator, Int128 denominator, double value)
        {
            // value = mantissa * 2^exponent with an integer mantissa
            long bits = *(long*)&value;
            int biased = (int)((bits >> 52) & 0x7FF);
            long mantissa = bits & 0xFFFFFFFFFFFFFL;
            int exponent;
            if (biased == 0) { exponent = -1074; }
            else { mantissa |= 1L << 52; exponent = biased - 1075; }
            if (value < 0) mantissa = -mantissa;

            // numerator/denominator - mantissa 2^e  has the sign of  (numerator - mantissa 2^e denominator) * sign(denominator)
            var n = BigInt.FromInt128(numerator);
            var m = BigInt.Mul(BigInt.FromLong(mantissa), BigInt.FromInt128(denominator));
            int result;
            if (exponent >= 0)
                result = BigInt.Compare(n, BigInt.ShiftLeft(m, exponent));
            else
                result = BigInt.Compare(BigInt.ShiftLeft(n, -exponent), m);
            return result * (denominator.IsNegative ? -1 : 1);
        }
    }
}
