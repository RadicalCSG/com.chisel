using Unity.Collections;
using Unity.Mathematics;

namespace Chisel.Core
{
    // A cap of many faces round one corner, a sphere's or a capsule's pole, close enough to flat to take one radial tile
    // together: each face is mapped as the cap's flat circle, seen along its normal
    static class HotspotCaps
    {
        // At least this many faces meet at the corner, none turned further than this from them all (its cosine)
        public const int   kCapFaces   = HotspotMatcher.kRadialCorners;
        public const float kCapCosine  = 0.9659258f;    // 15 degrees

        // The faces (surface indices, ascending) of the cap surf is in, with the same hotspot layout, and the cap's normal
        // and corners (tree space); false when it is in none
        public static bool Find(ref BasePolygonsBlob blob, int surf, NativeList<int> faces, NativeList<float3> corners, out float3 normal)
        {
            faces.Clear();
            corners.Clear();
            normal = default;
            ref var polygons = ref blob.polygons;
            ref var surfaces = ref blob.surfaces;
            if (surf < 0 || surf >= surfaces.Length || surfaces[surf].hotspotLayout == 0)
                return false;
            var own = -1;
            for (int p = 0; p < polygons.Length && own < 0; p++)
            {
                if (polygons[p].surfaceInfo.basePlaneIndex == surf)
                    own = p;
            }
            if (own < 0)
                return false;

            var layout  = surfaces[surf].hotspotLayout;
            var members = new NativeList<int>(16, Allocator.Temp);
            var found   = false;
            for (int e = polygons[own].startEdgeIndex; e < polygons[own].endEdgeIndex && !found; e++)
            {
                var corner = blob.edges[e].index1;
                members.Clear();
                var sum = float3.zero;
                for (int p = 0; p < polygons.Length; p++)
                {
                    var surface = polygons[p].surfaceInfo.basePlaneIndex;
                    if (surface >= surfaces.Length || surfaces[surface].hotspotLayout != layout || !Has(ref blob, p, corner))
                        continue;
                    members.Add(p);
                    sum += Normal(ref blob, p);
                }
                if (members.Length < kCapFaces || !(math.lengthsq(sum) > 0))
                    continue;
                var mean = math.normalize(sum);
                found = true;
                for (int m = 0; m < members.Length && found; m++)
                    found = math.dot(Normal(ref blob, members[m]), mean) >= kCapCosine;
                if (found)
                    normal = mean;
            }
            if (found)
            {
                for (int m = 0; m < members.Length; m++)
                {
                    var p = members[m];
                    faces.Add(polygons[p].surfaceInfo.basePlaneIndex);
                    for (int e = polygons[p].startEdgeIndex; e < polygons[p].endEdgeIndex; e++)
                        corners.Add(blob.vertices[blob.edges[e].index1]);
                }
                faces.Sort();
            }
            members.Dispose();
            return found;
        }

        static bool Has(ref BasePolygonsBlob blob, int polygon, int corner)
        {
            for (int e = blob.polygons[polygon].startEdgeIndex; e < blob.polygons[polygon].endEdgeIndex; e++)
            {
                if (blob.edges[e].index1 == corner)
                    return true;
            }
            return false;
        }

        // Newell's normal of a polygon, unit length (zero when it has no area)
        static float3 Normal(ref BasePolygonsBlob blob, int polygon)
        {
            var sum = double3.zero;
            for (int e = blob.polygons[polygon].startEdgeIndex; e < blob.polygons[polygon].endEdgeIndex; e++)
            {
                var a = (double3)blob.vertices[blob.edges[e].index1];
                var b = (double3)blob.vertices[blob.edges[e].index2];
                sum += new double3((a.y - b.y) * (a.z + b.z), (a.z - b.z) * (a.x + b.x), (a.x - b.x) * (a.y + b.y));
            }
            return math.lengthsq(sum) > 0 ? (float3)math.normalize(sum) : float3.zero;
        }
    }
}
