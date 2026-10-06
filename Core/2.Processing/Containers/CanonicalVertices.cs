using System.Runtime.CompilerServices;
using System.Threading;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Mathematics;

namespace Chisel.Core
{
    /// <summary>
    /// How far the pipeline goes with canonical vertices. Each stage includes the ones before it; see
    /// Documentation~/Design/CanonicalVertices.md.
    /// </summary>
    enum CanonicalVertexStage
    {
        /// <summary>Shipped behaviour: vertices are welded by distance.</summary>
        Off = 0,
        /// <summary>Canonical positions are computed and counted, but nothing uses them.</summary>
        Measure = 1,
        /// <summary>Vertices are created at their canonical positions; welding is unchanged.</summary>
        Positions = 2,
        /// <summary>While building loops, two vertices are the same only when their canonical positions are.</summary>
        LoopIdentity = 3,
        /// <summary>The same in the merge, the CSG re-weld and triangulation.</summary>
        Everywhere = 4,
    }

    /// <summary>Where a vertex was created, for the counters.</summary>
    enum CanonicalVertexSite : byte
    {
        Unknown          = 0,
        BrushCorner      = 1,   // CreateBlobPolygonsBlobsJob
        PairIntersection = 2,   // CreateIntersectionLoopsJob: an edge of one brush through a face of the other
        InsideVertex     = 3,   // CreateIntersectionLoopsJob: a corner of one brush inside the other
        AlignedFace      = 4,   // CreateIntersectionLoopsJob: a corner of a face lying against the other brush
        LoopSplit        = 5,   // FindLoopOverlapIntersectionsJob: an intersection loop cut by another brush
        BaseSplit        = 6,   // FindLoopOverlapIntersectionsJob: a face cut by another brush
    }

    /// <summary>
    /// The planes a vertex lies on: a small set of face planes, each turned to face along its largest normal component,
    /// kept sorted by coefficients and without exact duplicates. Its contents depend only on which planes were added,
    /// never on the order they were added in.
    /// </summary>
    unsafe struct PlaneKey
    {
        internal const int kCapacity = 32;

        fixed float m_Planes[kCapacity * 4];
        public int  Length;
        public bool Overflowed;

        public readonly float4 this[int index]
        {
            get
            {
                int offset = index * 4;
                return new float4(m_Planes[offset], m_Planes[offset + 1], m_Planes[offset + 2], m_Planes[offset + 3]);
            }
        }

        void Set(int index, float4 plane)
        {
            int offset = index * 4;
            m_Planes[offset]     = plane.x;
            m_Planes[offset + 1] = plane.y;
            m_Planes[offset + 2] = plane.z;
            m_Planes[offset + 3] = plane.w;
        }

        /// <summary>Inserts <paramref name="plane"/> in order, ignoring exact duplicates.</summary>
        public void Add(float4 plane)
        {
            plane = Oriented(plane);
            int position = 0;
            while (position < Length && Compare(this[position], plane) < 0)
                position++;
            if (position < Length && Compare(this[position], plane) == 0)
                return;
            if (Length == kCapacity)
            {
                // Keep the smallest planes, so that a full key is still the same whatever order its planes came in.
                Overflowed = true;
                if (position == kCapacity)
                    return;
                Length--;
            }
            for (int i = Length; i > position; i--)
                Set(i, this[i - 1]);
            Set(position, plane);
            Length++;
        }

        public readonly bool Equals(in PlaneKey other)
        {
            if (Length != other.Length)
                return false;
            for (int i = 0; i < Length; i++)
            {
                if (Compare(this[i], other[i]) != 0)
                    return false;
            }
            return true;
        }

        /// <summary>
        /// A plane and its twin (the same plane facing the other way) are one plane here, so every plane is turned to
        /// face along its largest normal component. Negation is exact, and so is the intersection of negated planes,
        /// so this changes no position.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static float4 Oriented(float4 plane)
        {
            var magnitude = math.abs(plane.xyz);
            var major = (magnitude.x >= magnitude.y && magnitude.x >= magnitude.z) ? plane.x
                      : (magnitude.y >= magnitude.z) ? plane.y
                      : plane.z;
            if (major < 0)
                plane = -plane;
            // -0 and +0 compare equal but have different bits; adding +0 turns every -0 into +0
            return plane + 0.0f;
        }

        // Lexicographic on (x, y, z, w). Only ever used to order planes and break exact ties, so it has to be total,
        // not meaningful.
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static int Compare(float4 a, float4 b)
        {
            if (a.x != b.x) return a.x < b.x ? -1 : 1;
            if (a.y != b.y) return a.y < b.y ? -1 : 1;
            if (a.z != b.z) return a.z < b.z ? -1 : 1;
            if (a.w != b.w) return a.w < b.w ? -1 : 1;
            return 0;
        }
    }

    unsafe struct CanonicalVertices
    {
        /// <summary>τ: how close a plane must be to a point to count as passing through it.</summary>
        internal const double kOnPlane = CSGConstants.kFatPlaneWidthEpsilon;

        /// <summary>
        /// How close two canonical positions must be to be the same vertex. Canonical positions of one vertex are
        /// bit-identical; this only absorbs the rounding of a position that was stored and transformed on the way.
        /// </summary>
        internal const float kSameVertex    = 0.00001f;
        internal const float kSqrSameVertex = kSameVertex * kSameVertex;

        /// <summary>
        /// The furthest a canonical position may lie from the computed one. Every plane of the key passes within τ of
        /// the computed point, so a well-conditioned triple lands within a few τ of it; anything further means the
        /// triple is too close to degenerate to trust, and the computed position is kept (and counted).
        /// </summary>
        internal const double kMaxMove = kOnPlane * 4;

        /// <summary>Below this |det| of three unit normals the triple is too close to degenerate to intersect.</summary>
        const double kMinConditioning = 1e-6;

        [NativeDisableUnsafePtrRestriction] BlobAssetReference<BrushTreeSpacePlanes>* m_TreeSpacePlanes;
        int                                         m_TreeSpacePlaneCount;
        [NativeDisableUnsafePtrRestriction] BlobAssetReference<BrushesTouchedByBrush>* m_Touched;
        int                                         m_TouchedCount;

        public CanonicalVertexStage Stage;

        public static CanonicalVertices Disabled => default;

        public readonly bool Computes            => Stage >= CanonicalVertexStage.Measure;
        public readonly bool MovesVertices       => Stage >= CanonicalVertexStage.Positions;
        public readonly bool DecidesLoopIdentity => Stage >= CanonicalVertexStage.LoopIdentity;
        public readonly bool DecidesAllIdentity  => Stage >= CanonicalVertexStage.Everywhere;

        /// <summary>Whether <see cref="Create"/> would enable <paramref name="stage"/> with these inputs.</summary>
        public static bool IsAvailable(CanonicalVertexStage                                  stage,
                                       NativeList<BlobAssetReference<BrushTreeSpacePlanes>>  treeSpacePlanes,
                                       NativeList<BlobAssetReference<BrushesTouchedByBrush>> touched)
        {
            return stage != CanonicalVertexStage.Off && treeSpacePlanes.IsCreated && touched.IsCreated;
        }

        public static CanonicalVertices Create(CanonicalVertexStage                                  stage,
                                               NativeList<BlobAssetReference<BrushTreeSpacePlanes>>  treeSpacePlanes,
                                               NativeList<BlobAssetReference<BrushesTouchedByBrush>> touched)
        {
            if (!IsAvailable(stage, treeSpacePlanes, touched))
                return Disabled;
            return new CanonicalVertices
            {
                Stage                 = stage,
                m_TreeSpacePlanes     = (BlobAssetReference<BrushTreeSpacePlanes>*)treeSpacePlanes.GetUnsafeReadOnlyPtr(),
                m_TreeSpacePlaneCount = treeSpacePlanes.Length,
                m_Touched             = (BlobAssetReference<BrushesTouchedByBrush>*)touched.GetUnsafeReadOnlyPtr(),
                m_TouchedCount        = touched.Length,
            };
        }

        /// <summary>
        /// The position of the vertex near <paramref name="approximate"/> that lies on brush
        /// <paramref name="nodeOrder"/>. Below <see cref="CanonicalVertexStage.Positions"/> this only counts, and
        /// returns <paramref name="approximate"/>.
        /// </summary>
        public readonly float3 Canonicalize(float3 approximate, int nodeOrder, CanonicalVertexSite site = CanonicalVertexSite.Unknown)
        {
            if (!Computes)
                return approximate;

            var key = new PlaneKey();
            int ambiguous = Gather(approximate, nodeOrder, ref key);
            if (!TryPosition(in key, out var position))
            {
                CanonicalVertexStats.Record(site, in key, approximate, approximate, CanonicalVertexStats.Outcome.Degenerate, refined: false, ambiguous);
                return approximate;
            }

            // One refinement: the key at the canonical point. Two computations that started a little apart but
            // agree here end up with the same bits even if their first keys differed at the edge of τ.
            var refinedKey = new PlaneKey();
            Gather(position, nodeOrder, ref refinedKey);
            bool refined = !refinedKey.Equals(in key);
            if (refined && TryPosition(in refinedKey, out var refinedPosition))
                position = refinedPosition;

            ref var usedKey = ref (refined ? ref refinedKey : ref key);
            if (math.distance((double3)approximate, (double3)position) > kMaxMove)
            {
                CanonicalVertexStats.Record(site, in usedKey, approximate, position, CanonicalVertexStats.Outcome.Rejected, refined, ambiguous);
                return approximate;
            }
            CanonicalVertexStats.Record(site, in usedKey, approximate, position, CanonicalVertexStats.Outcome.Placed, refined, ambiguous);
            return MovesVertices ? position : approximate;
        }

        /// <summary>
        /// The key of the point: the face planes within τ of it, from <paramref name="nodeOrder"/> and every brush it
        /// touches that contains the point. Returns how many faces lay between τ and 2τ.
        /// </summary>
        public readonly int Gather(float3 point, int nodeOrder, ref PlaneKey key)
        {
            int ambiguous = AddBrush(point, nodeOrder, ref key);
            if (nodeOrder < 0 || nodeOrder >= m_TouchedCount)
                return ambiguous;
            var touchedRef = m_Touched[nodeOrder];
            if (!touchedRef.IsCreated)
                return ambiguous;
            ref var intersections = ref touchedRef.Value.brushIntersections;
            for (int i = 0; i < intersections.Length; i++)
            {
                var other = intersections[i].nodeIndexOrder.nodeOrder;
                if (other != nodeOrder)
                    ambiguous += AddBrush(point, other, ref key);
            }
            return ambiguous;
        }

        readonly int AddBrush(float3 point, int nodeOrder, ref PlaneKey key)
        {
            if (nodeOrder < 0 || nodeOrder >= m_TreeSpacePlaneCount)
                return 0;
            var planesRef = m_TreeSpacePlanes[nodeOrder];
            if (!planesRef.IsCreated)
                return 0;
            ref var brush       = ref planesRef.Value;
            ref var brushPlanes = ref brush.treeSpacePlanes;
            // Only the faces: the planes stored after them are edge planes, which cut through the interior and must
            // not decide containment.
            int faceCount = math.min(brush.faceCount, brushPlanes.Length);

            // Only a brush that contains the point identifies it: a plane is infinite, and one of a brush far
            // away can pass through the point by accident.
            for (int f = 0; f < faceCount; f++)
            {
                if (Distance(brushPlanes[f], point) > kOnPlane)
                    return 0;
            }

            int ambiguous = 0;
            for (int f = 0; f < faceCount; f++)
            {
                var distance = math.abs(Distance(brushPlanes[f], point));
                if (distance <= kOnPlane)
                    key.Add(brushPlanes[f]);
                else if (distance <= kOnPlane * 2)
                    ambiguous++;
            }
            return ambiguous;
        }

        /// <summary>
        /// The canonical position of <paramref name="key"/>: the intersection of its best-conditioned triple.
        /// </summary>
        public static bool TryPosition(in PlaneKey key, out float3 position)
        {
            position = default;
            if (key.Length < 3)
                return false;

            // The key is sorted, so triples are visited in lexicographic order, and keeping the first of equally
            // conditioned triples picks the one with the smallest planes: the choice depends only on the planes.
            double bestConditioning = 0;
            int bestA = -1, bestB = -1, bestC = -1;
            for (int a = 0; a < key.Length - 2; a++)
            {
                var normalA = (double3)key[a].xyz;
                for (int b = a + 1; b < key.Length - 1; b++)
                {
                    var crossAB = math.cross(normalA, (double3)key[b].xyz);
                    for (int c = b + 1; c < key.Length; c++)
                    {
                        var conditioning = math.abs(math.dot(crossAB, (double3)key[c].xyz));
                        if (conditioning <= bestConditioning)
                            continue;
                        bestConditioning = conditioning;
                        bestA = a; bestB = b; bestC = c;
                    }
                }
            }
            if (bestA < 0 || bestConditioning < kMinConditioning)
                return false;

            // The same three planes in the same order give the same bits, wherever this runs.
            var intersection = PlaneExtensions.Intersection((double4)key[bestA], (double4)key[bestB], (double4)key[bestC]);
            if (math.any(math.isnan(intersection)) || math.any(math.isinf(intersection)))
                return false;
            position = (float3)intersection;
            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static double Distance(float4 plane, float3 point)
        {
            return (double)plane.x * point.x + (double)plane.y * point.y + (double)plane.z * point.z + plane.w;
        }
    }

    /// <summary>
    /// What canonicalization did during the last rebuilds, in total and per creation site: how often it placed,
    /// rejected or could not place a vertex, how far it moved them, how large keys were, and how often a face sat at
    /// the edge of τ. Burst-safe; read and reset from the editor.
    /// </summary>
    static unsafe class CanonicalVertexStats
    {
        internal enum Outcome { Placed, Degenerate, Rejected }

        internal const int kComputed     = 0;
        internal const int kDegenerate   = 1;   // fewer than three planes, or no usable triple
        internal const int kRejected     = 2;   // the triple landed more than kMaxMove away
        internal const int kMoved        = 3;
        internal const int kRefined      = 4;
        internal const int kOverflowed   = 5;
        internal const int kAmbiguous    = 6;   // vertices with at least one face between τ and 2τ
        internal const int kMaxMovedNm   = 7;   // largest displacement of a placed vertex, in nanometres
        internal const int kDisplacement = 8;   // 8 bins: 0, <1e-6, <1e-5, <1e-4, <τ, <2τ, <4τ, >=4τ
        internal const int kDisplacementBins = 8;
        internal const int kKeySize      = kDisplacement + kDisplacementBins;   // 8 bins: <3, 3, 4, 5, 6, 7, 8, >8
        internal const int kKeySizeBins  = 8;
        internal const int kCount        = kKeySize + kKeySizeBins;

        const int kSites = 7;   // block 0 is every site together, then one block per CanonicalVertexSite

        struct Block { public fixed long values[kCount * kSites]; }

        static readonly SharedStatic<Block> s_Block = SharedStatic<Block>.GetOrCreate<CanonicalVertices, Block>();

        public static void Record(CanonicalVertexSite site, in PlaneKey key, float3 approximate, float3 canonical,
                                  Outcome outcome, bool refined, int ambiguous)
        {
            var values = (long*)s_Block.UnsafeDataPointer;
            RecordInto(values, in key, approximate, canonical, outcome, refined, ambiguous);
            var index = (int)site;
            if (index > 0 && index < kSites)
                RecordInto(values + index * kCount, in key, approximate, canonical, outcome, refined, ambiguous);
        }

        static void RecordInto(long* values, in PlaneKey key, float3 approximate, float3 canonical,
                               Outcome outcome, bool refined, int ambiguous)
        {
            Interlocked.Increment(ref values[kComputed]);
            if (outcome == Outcome.Degenerate)
                Interlocked.Increment(ref values[kDegenerate]);
            if (outcome == Outcome.Rejected)
                Interlocked.Increment(ref values[kRejected]);
            if (refined)
                Interlocked.Increment(ref values[kRefined]);
            if (key.Overflowed)
                Interlocked.Increment(ref values[kOverflowed]);
            if (ambiguous > 0)
                Interlocked.Increment(ref values[kAmbiguous]);

            int size = key.Length < 3 ? 0 : key.Length > 8 ? 7 : key.Length - 2;
            Interlocked.Increment(ref values[kKeySize + size]);

            if (outcome == Outcome.Degenerate)
                return;

            var tau   = CanonicalVertices.kOnPlane;
            var moved = math.distance((double3)approximate, (double3)canonical);
            int bin = moved == 0 ? 0 : moved < 1e-6 ? 1 : moved < 1e-5 ? 2 : moved < 1e-4 ? 3
                    : moved < tau ? 4 : moved < tau * 2 ? 5 : moved < tau * 4 ? 6 : 7;
            Interlocked.Increment(ref values[kDisplacement + bin]);
            if (outcome != Outcome.Placed)
                return;
            if (moved > 0)
                Interlocked.Increment(ref values[kMoved]);

            var nanometres = (long)(moved * 1e9);
            long current;
            do
            {
                current = values[kMaxMovedNm];
                if (nanometres <= current)
                    break;
            } while (Interlocked.CompareExchange(ref values[kMaxMovedNm], nanometres, current) != current);
        }

        public static void Reset()
        {
            var values = (long*)s_Block.UnsafeDataPointer;
            for (int i = 0; i < kCount * kSites; i++)
                values[i] = 0;
        }

        public static long Get(int index, CanonicalVertexSite site = CanonicalVertexSite.Unknown)
        {
            return ((long*)s_Block.UnsafeDataPointer)[(int)site * kCount + index];
        }

        // Managed only (read from the editor after a rebuild). ASCII, so any console can print it.
        public static string Describe()
        {
            var text = new System.Text.StringBuilder();
            for (int site = 0; site < kSites; site++)
            {
                var s = (CanonicalVertexSite)site;
                long V(int i) => Get(i, s);
                if (V(kComputed) == 0 && site > 0)
                    continue;
                text.Append(site == 0 ? "all sites" : s.ToString())
                    .Append(": computed ").Append(V(kComputed))
                    .Append(", no usable key ").Append(V(kDegenerate))
                    .Append(", rejected (moved > 4 tau) ").Append(V(kRejected))
                    .Append(", moved ").Append(V(kMoved))
                    .Append(" (max ").Append((V(kMaxMovedNm) / 1e6).ToString("0.######", System.Globalization.CultureInfo.InvariantCulture)).Append(" mm)")
                    .Append(", refined ").Append(V(kRefined))
                    .Append(", overflowed ").Append(V(kOverflowed))
                    .Append(", face near tau ").Append(V(kAmbiguous)).Append('\n');
                text.Append("    displacement: 0 ").Append(V(kDisplacement))
                    .Append(", <1e-6 ").Append(V(kDisplacement + 1))
                    .Append(", <1e-5 ").Append(V(kDisplacement + 2))
                    .Append(", <1e-4 ").Append(V(kDisplacement + 3))
                    .Append(", <tau ").Append(V(kDisplacement + 4))
                    .Append(", <2tau ").Append(V(kDisplacement + 5))
                    .Append(", <4tau ").Append(V(kDisplacement + 6))
                    .Append(", >=4tau ").Append(V(kDisplacement + 7)).Append('\n');
                text.Append("    planes per vertex: <3 ").Append(V(kKeySize))
                    .Append(", 3 ").Append(V(kKeySize + 1))
                    .Append(", 4 ").Append(V(kKeySize + 2))
                    .Append(", 5 ").Append(V(kKeySize + 3))
                    .Append(", 6 ").Append(V(kKeySize + 4))
                    .Append(", 7 ").Append(V(kKeySize + 5))
                    .Append(", 8 ").Append(V(kKeySize + 6))
                    .Append(", >8 ").Append(V(kKeySize + 7)).Append('\n');
            }
            return text.ToString();
        }
    }
}
