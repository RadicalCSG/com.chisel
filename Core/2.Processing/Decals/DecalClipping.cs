using System.Runtime.CompilerServices;
using Unity.Mathematics;

namespace Chisel.Core
{
    // A point of a piece of a surface triangle, and where it sits in the triangle the piece was cut from, so the
    // vertex attributes of that triangle can be interpolated at it.
    internal struct DecalClipVertex
    {
        public double3 position;
        public double3 barycentric;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static DecalClipVertex Lerp(in DecalClipVertex a, in DecalClipVertex b, double t)
        {
            return new DecalClipVertex
            {
                position    = a.position + (b.position - a.position) * t,
                barycentric = a.barycentric + (b.barycentric - a.barycentric) * t
            };
        }
    }

    // A triangle cut from a surface triangle. owner is an index into the caller's list of decals: the decal that
    // is drawn there instead of the surface, or -1 when the surface itself is drawn there.
    internal struct DecalPiece
    {
        public DecalClipVertex v0, v1, v2;
        public int owner;

        public readonly float3 Min => (float3)math.min(v0.position, math.min(v1.position, v2.position));
        public readonly float3 Max => (float3)math.max(v0.position, math.max(v1.position, v2.position));
    }

    // Why DecalClipping.Split gave up, for diagnostics
    internal enum DecalSplitFailure : byte
    {
        None,
        TooManyPoints,
        RingTurnedOver,
        TooManyTriangles,
        AreaMismatch
    }

    internal interface IDecalPieceSink
    {
        void Add(in DecalPiece piece, bool inside);
    }

    internal unsafe struct DecalClipPolygon
    {
        public const int kCapacity = 32;
        const int kStride = 6;

        fixed double m_Values[kCapacity * kStride];
        public int  Count;
        public bool Overflowed;

        public DecalClipVertex this[int index]
        {
            get
            {
                var o = index * kStride;
                return new DecalClipVertex
                {
                    position    = new double3(m_Values[o], m_Values[o + 1], m_Values[o + 2]),
                    barycentric = new double3(m_Values[o + 3], m_Values[o + 4], m_Values[o + 5])
                };
            }
            set
            {
                var o = index * kStride;
                m_Values[o]     = value.position.x;
                m_Values[o + 1] = value.position.y;
                m_Values[o + 2] = value.position.z;
                m_Values[o + 3] = value.barycentric.x;
                m_Values[o + 4] = value.barycentric.y;
                m_Values[o + 5] = value.barycentric.z;
            }
        }

        public void Add(in DecalClipVertex vertex)
        {
            if (Count >= kCapacity)
            {
                Overflowed = true;
                return;
            }
            this[Count] = vertex;
            Count++;
        }

        public void Clear()
        {
            Count = 0;
            Overflowed = false;
        }
    }

    // Triangles as index triples into a vertex set that the caller keeps.
    internal unsafe struct DecalTriangleIndices
    {
        public const int kCapacity = 96;

        fixed byte m_Indices[kCapacity * 3];
        public int  Count;
        public bool Overflowed;

        public void Add(int a, int b, int c)
        {
            if (Count >= kCapacity)
            {
                Overflowed = true;
                return;
            }
            m_Indices[Count * 3]     = (byte)a;
            m_Indices[Count * 3 + 1] = (byte)b;
            m_Indices[Count * 3 + 2] = (byte)c;
            Count++;
        }

        public int this[int triangle, int corner] => m_Indices[triangle * 3 + corner];
    }

    internal static class DecalClipping
    {
        const double kEpsilon = DecalVolume.kPlaneEpsilon;
        // Pieces smaller than this (in square units) are dropped as rounding.
        public const double kMinArea = 1e-10;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static bool IsLess(double3 a, double3 b)
        {
            if (a.x != b.x) return a.x < b.x;
            if (a.y != b.y) return a.y < b.y;
            return a.z < b.z;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static bool Same(double3 a, double3 b)
        {
            return a.x == b.x && a.y == b.y && a.z == b.z;
        }

        public static int GetEdgePoints(in DecalVolume volume, in DecalClipVertex a, in DecalClipVertex b,
                                        out DecalClipVertex first, out DecalClipVertex second)
        {
            first  = default;
            second = default;

            // Always walk the edge from its lexicographically smaller end, so both triangles on the edge compute
            // exactly the same numbers.
            var swapped = IsLess(b.position, a.position);
            var p = swapped ? b.position : a.position;
            var q = swapped ? a.position : b.position;

            double enter = 0, exit = 1;
            for (int i = 0; i < DecalPlanes.kCount; i++)
            {
                var dp = volume.Distance(i, p);
                var dq = volume.Distance(i, q);
                var pOutside = dp < -kEpsilon;
                var qOutside = dq < -kEpsilon;
                if (pOutside && qOutside)
                    return 0;
                if (!pOutside && !qOutside)
                    continue;
                var t = dp / (dp - dq);
                if (pOutside) enter = math.max(enter, t);
                else          exit  = math.min(exit, t);
            }
            if (enter > exit)
                return 0;

            var length = math.length(q - p);
            if (!(length > 0))
                return 0;
            var tolerance = kEpsilon / length;

            double t0 = 0, t1 = 0;
            var count = 0;
            if (exit - enter <= tolerance)
            {
                t0 = (enter + exit) * 0.5;
                if (t0 > tolerance && t0 < 1.0 - tolerance)
                    count = 1;
            } else
            {
                if (enter > tolerance)
                {
                    t0 = enter;
                    count = 1;
                }
                if (exit < 1.0 - tolerance)
                {
                    if (count == 0) t0 = exit;
                    else            t1 = exit;
                    count++;
                }
            }
            if (count == 0)
                return 0;

            first = PointOnEdge(p, q, t0, a, b, swapped);
            if (count == 2)
            {
                second = PointOnEdge(p, q, t1, a, b, swapped);
                if (swapped)
                    (first, second) = (second, first);
            }
            return count;
        }

        // The position comes from p-q (the fixed order), the barycentric from a-b (the caller's order).
        static DecalClipVertex PointOnEdge(double3 p, double3 q, double t, in DecalClipVertex a, in DecalClipVertex b, bool swapped)
        {
            var fromA = swapped ? 1.0 - t : t;
            return new DecalClipVertex
            {
                position    = p + (q - p) * t,
                barycentric = a.barycentric + (b.barycentric - a.barycentric) * fromA
            };
        }

        internal static void AddEdge(ref DecalClipPolygon polygon, in DecalVolume volume, in DecalClipVertex a, in DecalClipVertex b)
        {
            polygon.Add(a);
            var count = GetEdgePoints(volume, a, b, out var first, out var second);
            if (count > 0) polygon.Add(first);
            if (count > 1) polygon.Add(second);
        }

        // Sutherland-Hodgman against the volume's planes. A point within kPlaneEpsilon of a plane counts as on it and
        // is kept as it is; a new point is only made where an edge clearly crosses a plane.
        public static void Clip(ref DecalClipPolygon polygon, in DecalVolume volume)
        {
            var result = new DecalClipPolygon();
            for (int i = 0; i < DecalPlanes.kCount && polygon.Count > 0; i++)
            {
                result.Clear();
                result.Overflowed = polygon.Overflowed;
                var count = polygon.Count;
                for (int v = 0; v < count; v++)
                {
                    var current = polygon[v];
                    var next    = polygon[(v + 1) % count];
                    var dc = volume.Distance(i, current.position);
                    var dn = volume.Distance(i, next.position);
                    if (dc >= -kEpsilon)
                        result.Add(current);
                    if ((dc < -kEpsilon && dn > kEpsilon) ||
                        (dc > kEpsilon && dn < -kEpsilon))
                        result.Add(DecalClipVertex.Lerp(current, next, dc / (dc - dn)));
                }
                polygon = result;
            }
        }

        // Replaces points that lie on one of the boundary's points by that point, and drops the repeats this makes,
        // so the pieces inside and outside share exactly the boundary's points.
        internal static void SnapTo(ref DecalClipPolygon polygon, ref DecalClipPolygon boundary)
        {
            var result = new DecalClipPolygon { Overflowed = polygon.Overflowed };
            var epsilonSqr = kEpsilon * kEpsilon;
            for (int v = 0; v < polygon.Count; v++)
            {
                var vertex = polygon[v];
                for (int b = 0; b < boundary.Count; b++)
                {
                    var candidate = boundary[b];
                    if (math.distancesq(vertex.position, candidate.position) <= epsilonSqr)
                    {
                        vertex = candidate;
                        break;
                    }
                }
                if (result.Count > 0 && Same(result[result.Count - 1].position, vertex.position))
                    continue;
                result.Add(vertex);
            }
            while (result.Count > 1 && Same(result[0].position, result[result.Count - 1].position))
                result.Count--;
            polygon = result;
        }

        // Twice the polygon's area, along its normal (Newell).
        internal static double3 AreaVector(ref DecalClipPolygon polygon)
        {
            var sum = double3.zero;
            var count = polygon.Count;
            if (count < 3)
                return sum;
            var origin = polygon[0].position;
            for (int v = 1; v + 1 < count; v++)
                sum += math.cross(polygon[v].position - origin, polygon[v + 1].position - origin);
            return sum;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static double TriangleArea(double3 a, double3 b, double3 c)
        {
            return math.length(math.cross(b - a, c - a)) * 0.5;
        }

        // Adds a convex polygon as triangles. A polygon with points along its edges is fanned from its centroid, so
        // no triangle is flat and every point stays a corner.
        static void AddPolygon<TSink>(ref DecalClipPolygon polygon, int owner, bool inside, ref TSink sink)
            where TSink : struct, IDecalPieceSink
        {
            var count = polygon.Count;
            if (count < 3)
                return;
            if (count == 3)
            {
                var triangle = new DecalPiece { v0 = polygon[0], v1 = polygon[1], v2 = polygon[2], owner = owner };
                if (TriangleArea(triangle.v0.position, triangle.v1.position, triangle.v2.position) > kMinArea)
                    sink.Add(triangle, inside);
                return;
            }

            var center = new DecalClipVertex();
            for (int v = 0; v < count; v++)
            {
                var vertex = polygon[v];
                center.position    += vertex.position;
                center.barycentric += vertex.barycentric;
            }
            center.position    /= count;
            center.barycentric /= count;

            for (int v = 0; v < count; v++)
            {
                var a = polygon[v];
                var b = polygon[(v + 1) % count];
                if (TriangleArea(center.position, a.position, b.position) <= kMinArea)
                    continue;
                sink.Add(new DecalPiece { v0 = center, v1 = a, v2 = b, owner = owner }, inside);
            }
        }

        // Adds the part of a piece that lies inside the volume, without splitting what lies outside. For decals that
        // are drawn over the surface rather than replacing it. Returns false when nothing lies inside.
        public static bool AddInside<TSink>(in DecalVolume volume, in DecalPiece piece, int owner, ref TSink sink)
            where TSink : struct, IDecalPieceSink
        {
            if (volume.AllOutsideOnePlane(piece.v0.position, piece.v1.position, piece.v2.position))
                return false;
            var polygon = new DecalClipPolygon();
            polygon.Add(piece.v0);
            polygon.Add(piece.v1);
            polygon.Add(piece.v2);
            Clip(ref polygon, volume);
            if (polygon.Count < 3 || polygon.Overflowed ||
                math.length(AreaVector(ref polygon)) * 0.5 <= kMinArea)
                return false;
            AddPolygon(ref polygon, owner, true, ref sink);
            return true;
        }

        public static bool Split<TSink>(in DecalVolume volume, in DecalPiece piece, ref TSink sink)
            where TSink : struct, IDecalPieceSink
        {
            return Split(volume, piece, ref sink, out _);
        }

        public static bool Split<TSink>(in DecalVolume volume, in DecalPiece piece, ref TSink sink, out DecalSplitFailure failure)
            where TSink : struct, IDecalPieceSink
        {
            failure = DecalSplitFailure.None;
            if (volume.AllOutsideOnePlane(piece.v0.position, piece.v1.position, piece.v2.position))
            {
                sink.Add(piece, false);
                return true;
            }

            var boundary = new DecalClipPolygon();
            AddEdge(ref boundary, volume, piece.v0, piece.v1);
            AddEdge(ref boundary, volume, piece.v1, piece.v2);
            AddEdge(ref boundary, volume, piece.v2, piece.v0);
            if (boundary.Overflowed)
            {
                failure = DecalSplitFailure.TooManyPoints;
                return false;
            }

            var allInside = true;
            for (int v = 0; v < boundary.Count; v++)
            {
                if (!volume.Contains(boundary[v].position))
                {
                    allInside = false;
                    break;
                }
            }
            if (allInside)
            {
                AddPolygon(ref boundary, piece.owner, true, ref sink);
                return true;
            }

            var inner = boundary;
            Clip(ref inner, volume);
            SnapTo(ref inner, ref boundary);
            if (inner.Overflowed)
            {
                failure = DecalSplitFailure.TooManyPoints;
                return false;
            }
            var innerArea = math.length(AreaVector(ref inner)) * 0.5;
            if (inner.Count < 3 || innerArea <= kMinArea)
            {
                // Nothing inside, but keep the points where the volume touches an edge: the triangle on the other
                // side of that edge may well have a piece inside.
                AddPolygon(ref boundary, piece.owner, false, ref sink);
                return true;
            }

            var ring = new DecalTriangleIndices();
            if (!TriangulateRing(ref boundary, ref inner, ref ring))
            {
                failure = ring.Overflowed ? DecalSplitFailure.TooManyTriangles : DecalSplitFailure.RingTurnedOver;
                return false;
            }

            // Everything must add up to the piece, or the ring is wrong
            var outerArea = math.length(AreaVector(ref boundary)) * 0.5;
            var ringArea  = 0.0;
            for (int t = 0; t < ring.Count; t++)
            {
                ringArea += TriangleArea(RingVertex(ref boundary, ref inner, ring[t, 0]).position,
                                         RingVertex(ref boundary, ref inner, ring[t, 1]).position,
                                         RingVertex(ref boundary, ref inner, ring[t, 2]).position);
            }
            if (math.abs(ringArea + innerArea - outerArea) > 1e-6 * outerArea + kMinArea)
            {
                failure = DecalSplitFailure.AreaMismatch;
                return false;
            }

            AddPolygon(ref inner, piece.owner, true, ref sink);
            for (int t = 0; t < ring.Count; t++)
            {
                sink.Add(new DecalPiece
                {
                    v0    = RingVertex(ref boundary, ref inner, ring[t, 0]),
                    v1    = RingVertex(ref boundary, ref inner, ring[t, 1]),
                    v2    = RingVertex(ref boundary, ref inner, ring[t, 2]),
                    owner = piece.owner
                }, false);
            }
            return true;
        }

        // Ring indices below boundary.Count are boundary points, the rest are inner points.
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static DecalClipVertex RingVertex(ref DecalClipPolygon boundary, ref DecalClipPolygon inner, int index)
        {
            return index < boundary.Count ? boundary[index] : inner[index - boundary.Count];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static double WrapAngle(double angle)
        {
            const double kTwoPi = 2.0 * math.PI_DBL;
            angle %= kTwoPi;
            if (angle < 0)
                angle += kTwoPi;
            return angle;
        }

        internal static unsafe bool TriangulateRing(ref DecalClipPolygon outer, ref DecalClipPolygon inner, ref DecalTriangleIndices triangles)
        {
            var outerCount = outer.Count;
            var innerCount = inner.Count;
            if (outerCount < 3 || innerCount < 3 || outerCount + innerCount > 255)
                return false;

            var normal   = AreaVector(ref outer);
            var abs      = math.abs(normal);
            var dominant = abs.x >= abs.y && abs.x >= abs.z ? 0 : (abs.y >= abs.z ? 1 : 2);
            var axisU    = (dominant + 1) % 3;
            var axisV    = (dominant + 2) % 3;
            var mirror   = normal[dominant] < 0;

            // Ring index space: outer points first, then inner points
            var points    = stackalloc double2[outerCount + innerCount];
            var boundsMin = new double2(double.PositiveInfinity);
            var boundsMax = new double2(double.NegativeInfinity);
            for (int v = 0; v < outerCount; v++)
            {
                points[v] = Project(outer[v].position, axisU, axisV, mirror);
                boundsMin = math.min(boundsMin, points[v]);
                boundsMax = math.max(boundsMax, points[v]);
            }
            var center = double2.zero;
            for (int v = 0; v < innerCount; v++)
            {
                points[outerCount + v] = Project(inner[v].position, axisU, axisV, mirror);
                center += points[outerCount + v];
            }
            center /= innerCount;
            var size      = math.cmax(boundsMax - boundsMin);
            var tolerance = 1e-12 * size * size;

            // Where to start
            int outerStart = -1, innerStart = -1;
            for (int o = 0; o < outerCount && outerStart < 0; o++)
            {
                for (int i = 0; i < innerCount; i++)
                {
                    if (Same(outer[o].position, inner[i].position))
                    {
                        outerStart = o;
                        innerStart = i;
                        break;
                    }
                }
            }
            var touching   = outerStart >= 0;
            if (!touching)
                outerStart = 0;
            var startAngle = Angle(points[outerStart], center);
            if (!touching)
            {
                var best = double.PositiveInfinity;
                for (int i = 0; i < innerCount; i++)
                {
                    var behind = WrapAngle(startAngle - Angle(points[outerCount + i], center));
                    if (behind < best)
                    {
                        best = behind;
                        innerStart = i;
                    }
                }
            }

            // How far around the center each point is from where the walk starts, to choose between two steps
            var outerTurn = stackalloc double[outerCount + 1];
            var innerTurn = stackalloc double[innerCount + 1];
            outerTurn[0] = 0;
            for (int k = 1; k <= outerCount; k++)
            {
                var from = points[(outerStart + k - 1) % outerCount];
                var to   = points[(outerStart + k) % outerCount];
                outerTurn[k] = outerTurn[k - 1] + WrapAngle(Angle(to, center) - Angle(from, center));
            }
            innerTurn[0] = touching ? 0 : -WrapAngle(startAngle - Angle(points[outerCount + innerStart], center));
            for (int k = 1; k <= innerCount; k++)
            {
                var from = points[outerCount + (innerStart + k - 1) % innerCount];
                var to   = points[outerCount + (innerStart + k) % innerCount];
                innerTurn[k] = innerTurn[k - 1] + WrapAngle(Angle(to, center) - Angle(from, center));
            }

            int outerStep = 0, innerStep = 0;
            while (outerStep < outerCount || innerStep < innerCount)
            {
                var o0 = (outerStart + outerStep) % outerCount;
                var o1 = (o0 + 1) % outerCount;
                var i0 = (innerStart + innerStep) % innerCount;
                var i1 = (i0 + 1) % innerCount;

                if (outerStep < outerCount && innerStep < innerCount &&
                    Same(outer[o0].position, inner[i0].position) &&
                    Same(outer[o1].position, inner[i1].position))
                {
                    // An edge both polygons share: nothing between them there
                    outerStep++;
                    innerStep++;
                    continue;
                }

                var p0 = points[o0];
                var q0 = points[outerCount + i0];

                var canInner = false;
                if (innerStep < innerCount)
                    canInner = Orient(p0, points[outerCount + i1], q0) > -tolerance;

                var canOuter = false;
                if (outerStep < outerCount)
                {
                    var p1 = points[o1];
                    canOuter = Orient(p0, p1, q0) > -tolerance;
                    if (canOuter && !Same(outer[o1].position, inner[i0].position))
                    {
                        // The new edge p1-q0 must not start into the inner polygon's corner at q0 ...
                        var qPrev = points[outerCount + (i0 + innerCount - 1) % innerCount];
                        var qNext = points[outerCount + i1];
                        if (Orient(qPrev, q0, p1) > tolerance && Orient(q0, qNext, p1) > tolerance)
                            canOuter = false;
                        // ... and the triangle must not hold any inner point
                        for (int i = 0; canOuter && i < innerCount; i++)
                        {
                            if (i == i0)
                                continue;
                            var q = points[outerCount + i];
                            if (Orient(p0, p1, q) > tolerance &&
                                Orient(p1, q0, q) > tolerance &&
                                Orient(q0, p0, q) > tolerance)
                                canOuter = false;
                        }
                    }
                }

                bool advanceOuter;
                if (canOuter && canInner)
                    advanceOuter = outerTurn[outerStep + 1] <= innerTurn[innerStep + 1];
                else if (canOuter)
                    advanceOuter = true;
                else if (canInner)
                    advanceOuter = false;
                else
                    return false;

                if (advanceOuter)
                {
                    AddRingTriangle(points, ref triangles, o0, o1, outerCount + i0, tolerance);
                    outerStep++;
                } else
                {
                    AddRingTriangle(points, ref triangles, o0, outerCount + i1, outerCount + i0, tolerance);
                    innerStep++;
                }
            }
            return !triangles.Overflowed;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static double Orient(double2 a, double2 b, double2 c)
        {
            return (b.x - a.x) * (c.y - a.y) - (b.y - a.y) * (c.x - a.x);
        }

        // Flat triangles (points along an edge) are left out
        static unsafe void AddRingTriangle(double2* points, ref DecalTriangleIndices triangles, int a, int b, int c, double tolerance)
        {
            if (Orient(points[a], points[b], points[c]) <= tolerance)
                return;
            triangles.Add(a, b, c);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static double Angle(double2 point, double2 center)
        {
            var offset = point - center;
            return math.atan2(offset.y, offset.x);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static double2 Project(double3 position, int axisU, int axisV, bool mirror)
        {
            var point = new double2(position[axisU], position[axisV]);
            if (mirror)
                point.x = -point.x;
            return point;
        }

    }
}
