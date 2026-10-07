using System;
using Unity.Mathematics;

namespace Chisel.Core
{
    [Flags]
    public enum HotspotSurfaceFlags : byte
    {
        None       = 0,
        // repeated because no tile was large enough (HotspotMatcher.kLastResortScale)
        LastResort = 1,
        Repeated   = 2,
        // too small for its border to leave an interior
        AllBorder  = 4,
        Pinned     = 8,
    }

    // What hotspot mapping chose for a surface, for the diagnostic view; all zero when it is not hotspot mapped
    public struct HotspotSurfaceInfo
    {
        public int                 tileId;
        public float2              textureSize;
        public float               pixelsPerUnit;
        public float               scale;
        public HotspotSurfaceFlags flags;

        public readonly bool IsMapped => pixelsPerUnit > 0;
    }

    // How a texture lies on drawn triangles, measured against the tile's own density (low distortion optimization): the singular values s1 >= s2 of the map from a triangle
    // (units) to the texture (pixels / pixelsPerUnit). 1 and 1 is the tile at its density without distortion; above 1
    // the texture is squeezed, below 1 stretched.
    public static class HotspotDiagnostics
    {
        public static HotspotSurfaceInfo Info(in HotspotTileData tile, in HotspotLayoutData layout, in HotspotChoice choice,
                                              in HotspotSectionInput sectionInput, bool pinned)
        {
            var flags = HotspotSurfaceFlags.None;
            if (choice.lastResort)                                              flags |= HotspotSurfaceFlags.LastResort;
            if (choice.Repeated)                                                flags |= HotspotSurfaceFlags.Repeated;
            if (sectionInput.count > 0 && HotspotSections.IsAllBorder(sectionInput)) flags |= HotspotSurfaceFlags.AllBorder;
            if (pinned)                                                         flags |= HotspotSurfaceFlags.Pinned;
            return new HotspotSurfaceInfo
            {
                tileId        = tile.id,
                textureSize   = layout.textureSize,
                pixelsPerUnit = tile.pixelsPerUnit,
                scale         = choice.scale,
                flags         = flags
            };
        }

        // The singular values of a triangle's map onto the texture, and whether it is mirrored (its uvs wind the other
        // way round than its corners); false for a triangle without area
        public static bool Measure(float3 a, float3 b, float3 c, float2 uvA, float2 uvB, float2 uvC, float2 textureSize, float pixelsPerUnit,
                                   out double s1, out double s2, out bool mirrored)
        {
            s1 = s2 = 0;
            mirrored = false;
            var e1 = (double3)b - (double3)a;
            var e2 = (double3)c - (double3)a;
            var normal = math.cross(e1, e2);
            var twiceArea = math.length(normal);
            var length = math.length(e1);
            if (!(twiceArea > 0) || !(length > 0) || !(pixelsPerUnit > 0))
                return false;
            // the triangle in a frame of its own plane
            var x = e1 / length;
            var y = math.cross(normal / twiceArea, x);
            var corners = new double2x2(new double2(length, 0), new double2(math.dot(e2, x), math.dot(e2, y)));
            var toUnits = (double2)textureSize / pixelsPerUnit;
            var texture = new double2x2(((double2)uvB - (double2)uvA) * toUnits, ((double2)uvC - (double2)uvA) * toUnits);
            var map = math.mul(texture, math.inverse(corners));
            SingularValues(map, out s1, out s2);
            mirrored = math.determinant(map) < 0;
            return true;
        }

        // Symmetric Dirichlet energy, 1 without distortion, growing without bound as a triangle squeezes flat or
        // stretches
        public static double Energy(double s1, double s2)
        {
            if (!(s1 > 0) || !(s2 > 0))
                return double.PositiveInfinity;
            return 0.25 * (s1 * s1 + 1 / (s1 * s1) + s2 * s2 + 1 / (s2 * s2));
        }

        // Of a 2x2 matrix, in closed form, the larger first
        public static void SingularValues(double2x2 m, out double s1, out double s2)
        {
            var a = m.c0.x; var b = m.c1.x; var c = m.c0.y; var d = m.c1.y;
            var e = (a + d) * 0.5; var f = (a - d) * 0.5; var g = (c + b) * 0.5; var h = (c - b) * 0.5;
            var q = math.sqrt(e * e + h * h);
            var r = math.sqrt(f * f + g * g);
            s1 = q + r;
            s2 = math.abs(q - r);
        }
    }
}
