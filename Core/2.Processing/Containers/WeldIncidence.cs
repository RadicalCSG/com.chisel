using Unity.Entities;
using Unity.Mathematics;

namespace Chisel.Core
{
    unsafe struct WeldIncidenceFilter
    {
        /// <summary>How close a point must be to a plane to count as lying on it: the CSG's own plane tolerance.</summary>
        const float kOnPlane = CSGConstants.kFatPlaneWidthEpsilon;

        /// <summary>
        /// How far a merge may end up pushing a point off a plane it lies on. Two tolerances, because each of the two
        /// points may sit a full tolerance off the plane on opposite sides, and merging them anywhere between is no
        /// worse than the pipeline's own plane tolerance.
        /// </summary>
        const float kOffPlane = kOnPlane * 2;

        float4* m_Planes0;
        int     m_FaceCount0;
        float4* m_Planes1;
        int     m_FaceCount1;
        bool    m_SameVertexOnly;

        /// <summary>Allows every merge, i.e. the distance-only behaviour.</summary>
        public static WeldIncidenceFilter Disabled => default;

        /// <summary>
        /// For canonical vertices (<see cref="CanonicalVertices"/>): a vertex computed twice lands on the same bits,
        /// so a merge is allowed only between positions that are the same vertex. Everything else is a different
        /// vertex, however close.
        /// </summary>
        public static WeldIncidenceFilter SameVertexOnly => new WeldIncidenceFilter { m_SameVertexOnly = true };

        /// <summary>The filter a weld site uses for the given canonical-vertex decision and incidence setting.</summary>
        public static WeldIncidenceFilter Choose(bool canonicalDecides, bool useIncidenceWeld, in WeldIncidenceFilter incidence)
        {
            if (canonicalDecides)
                return SameVertexOnly;
            return useIncidenceWeld ? incidence : Disabled;
        }

        /// <summary>
        /// Gate against one brush's faces. Only the first <paramref name="facePlaneCount"/> planes bound the brush;
        /// the edge planes stored after them cut through its interior and would reject points that are inside it.
        /// </summary>
        public static WeldIncidenceFilter Create(ref BlobArray<float4> planes, int facePlaneCount)
        {
            return new WeldIncidenceFilter
            {
                m_Planes0    = (float4*)planes.GetUnsafePtr(),
                m_FaceCount0 = math.min(facePlaneCount, planes.Length)
            };
        }

        /// <summary>Gate against the faces of both brushes of a pair.</summary>
        public static WeldIncidenceFilter Create(ref BlobArray<float4> planes0, int facePlaneCount0,
                                                 ref BlobArray<float4> planes1, int facePlaneCount1)
        {
            return new WeldIncidenceFilter
            {
                m_Planes0    = (float4*)planes0.GetUnsafePtr(),
                m_FaceCount0 = math.min(facePlaneCount0, planes0.Length),
                m_Planes1    = (float4*)planes1.GetUnsafePtr(),
                m_FaceCount1 = math.min(facePlaneCount1, planes1.Length)
            };
        }

        public readonly bool IsEnabled { get { return m_SameVertexOnly || m_Planes0 != null || m_Planes1 != null; } }

        /// <summary>True when moving <paramref name="from"/> onto <paramref name="to"/> keeps every incidence.</summary>
        public readonly bool Allows(float3 from, float3 to)
        {
            if (m_SameVertexOnly)
                return math.lengthsq(from - to) <= CanonicalVertices.kSqrSameVertex;
            return KeepsIncidence(m_Planes0, m_FaceCount0, from, to) &&
                   KeepsIncidence(m_Planes1, m_FaceCount1, from, to);
        }

        static bool KeepsIncidence(float4* planes, int faceCount, float3 a, float3 b)
        {
            if (planes == null || faceCount <= 0)
                return true;

            var a4 = new float4(a, 1);
            var b4 = new float4(b, 1);
            bool aInside = true, bInside = true;
            for (int i = 0; i < faceCount; i++)
            {
                var plane = planes[i];
                if (math.dot(plane, a4) > kOnPlane) aInside = false;
                if (math.dot(plane, b4) > kOnPlane) bInside = false;
                if (!aInside && !bInside)
                    return true;
            }

            for (int i = 0; i < faceCount; i++)
            {
                var plane = planes[i];
                var distanceA = math.abs(math.dot(plane, a4));
                var distanceB = math.abs(math.dot(plane, b4));
                if (aInside && distanceA <= kOnPlane && distanceB > kOffPlane) return false;
                if (bInside && distanceB <= kOnPlane && distanceA > kOffPlane) return false;
            }
            return true;
        }
    }
}
