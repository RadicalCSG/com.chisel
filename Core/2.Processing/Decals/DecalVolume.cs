using System;
using System.Runtime.CompilerServices;
using Unity.Mathematics;

namespace Chisel.Core
{
    [Flags]
    internal enum DecalVolumeFlags : byte
    {
        None         = 0,
        Perspective  = 1 << 0,
        Transparent  = 1 << 1,
        AngleLimited = 1 << 2
    }

    // The six planes around a decal's volume, in tree space. Their normals point inwards and are unit length, so
    // dot(plane.xyz, p) + plane.w is the distance of p inside the plane.
    internal struct DecalPlanes
    {
        public const int kCount = 6;

        public double4 p0, p1, p2, p3, p4, p5;

        public double4 this[int index]
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            readonly get
            {
                switch (index)
                {
                    case 0: return p0;
                    case 1: return p1;
                    case 2: return p2;
                    case 3: return p3;
                    case 4: return p4;
                    default: return p5;
                }
            }
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set
            {
                switch (index)
                {
                    case 0: p0 = value; break;
                    case 1: p1 = value; break;
                    case 2: p2 = value; break;
                    case 3: p3 = value; break;
                    case 4: p4 = value; break;
                    default: p5 = value; break;
                }
            }
        }
    }

    // A decal made ready for the update: its volume and projection in tree space. Built once per SetDecals call
    // (ChiselDecalStore), read by GenerateSurfaceTrianglesJob. See Documentation~/Design/Decals.md.
    internal struct DecalVolume
    {
        // A point closer to a plane than this is on it. Decal boxes are often snapped onto brush faces, and a plane
        // made from a float transformation is only this accurate at map scale.
        public const double kPlaneEpsilon = 1e-4;
        // Smaller boxes don't make a decal.
        public const double kMinSize = 1e-4;

        public ulong                    entityID;
        public int                      order;
        public DecalVolumeFlags         flags;
        public ulong                    renderMaterial;
        public SurfaceDestinationFlags  materialFlags;
        public float                    surfaceOffset;
        public float2                   uvScale;
        public float2                   uvOffset;
        public double                   cosMaxAngle;

        // The projection, in decal space
        public double4x4                treeToDecal;
        public double3                  center;
        public double3                  size;
        public double                   focal;          // perspective: the distance from the apex to the center plane

        // Tree space
        public double3                  facingPoint;    // the box's center: surfaces facing away from it are skipped
        public double3                  axis;           // unit, the direction the image travels in (orthographic)
        public double3                  apex;           // perspective: where the image comes from
        public DecalPlanes              planes;
        public float3                   boundsMin;
        public float3                   boundsMax;

        // The surfaces the decal is limited to, in the update's target array (none: every surface)
        public int                      targetStart;
        public int                      targetCount;

        public readonly bool IsTransparent
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get { return (flags & DecalVolumeFlags.Transparent) != 0; }
        }

        public readonly bool IsPerspective
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get { return (flags & DecalVolumeFlags.Perspective) != 0; }
        }

        // False for a decal that cannot draw anything: no material, a flat box, a transformation without an inverse.
        public static bool TryCreate(in ChiselDecalInstance instance, out DecalVolume volume)
        {
            volume = default;
            var settings = instance.settings;
            if (instance.renderMaterial == 0 ||
                (instance.destinationFlags & SurfaceDestinationFlags.Renderable) == 0)
                return false;

            var size = math.abs((double3)settings.size);
            if (!math.all(math.isfinite(size)) || !math.all(size > kMinSize))
                return false;

            var decalToTree = (double4x4)instance.decalToTree;
            if (!math.all(math.isfinite(decalToTree.c0)) || !math.all(math.isfinite(decalToTree.c1)) ||
                !math.all(math.isfinite(decalToTree.c2)) || !math.all(math.isfinite(decalToTree.c3)))
                return false;
            if (math.abs(math.determinant(decalToTree)) < 1e-12)
                return false;
            var treeToDecal = math.inverse(decalToTree);

            var center      = (double3)settings.center;
            var halfSize    = size * 0.5;
            var perspective = settings.projection == ChiselDecalProjection.Perspective &&
                              settings.fieldOfView >= ChiselDecalSettings.kMinFieldOfView &&
                              settings.fieldOfView <= ChiselDecalSettings.kMaxFieldOfView;
            var nearZ = center.z - halfSize.z;
            var farZ  = center.z + halfSize.z;

            var local = new DecalPlanes();
            double focal = 0, apexZ = 0, slopeX = 0, slopeY = 0;
            if (perspective)
            {
                // The image is size.xy at the center plane and comes from a point focal behind it. Each side plane
                // goes through that point: inside the left one, (x - cx) + slopeX * (z - apexZ) >= 0.
                focal  = halfSize.y / math.tan(math.radians((double)settings.fieldOfView) * 0.5);
                apexZ  = center.z - focal;
                slopeX = halfSize.x / focal;
                slopeY = halfSize.y / focal;
                nearZ  = math.max(nearZ, apexZ + focal * 1e-3);
                local.p0 = new double4( 1, 0, slopeX, -center.x - slopeX * apexZ);
                local.p1 = new double4(-1, 0, slopeX,  center.x - slopeX * apexZ);
                local.p2 = new double4( 0, 1, slopeY, -center.y - slopeY * apexZ);
                local.p3 = new double4( 0,-1, slopeY,  center.y - slopeY * apexZ);
            } else
            {
                local.p0 = new double4( 1, 0, 0, -(center.x - halfSize.x));
                local.p1 = new double4(-1, 0, 0,   center.x + halfSize.x);
                local.p2 = new double4( 0, 1, 0, -(center.y - halfSize.y));
                local.p3 = new double4( 0,-1, 0,   center.y + halfSize.y);
            }
            if (farZ - nearZ <= kMinSize)
                return false;
            local.p4 = new double4(0, 0,  1, -nearZ);
            local.p5 = new double4(0, 0, -1,  farZ);

            // A point p in tree space is decal space treeToDecal * p, so a decal space plane q becomes
            // transpose(treeToDecal) * q in tree space.
            var planeToTree = math.transpose(treeToDecal);
            var planes = new DecalPlanes();
            for (int i = 0; i < DecalPlanes.kCount; i++)
            {
                var plane  = math.mul(planeToTree, local[i]);
                var length = math.length(plane.xyz);
                if (!(length > 1e-12))
                    return false;
                planes[i] = plane / length;
            }

            // The corners of the near and far faces
            var boundsMin = new double3(double.PositiveInfinity);
            var boundsMax = new double3(double.NegativeInfinity);
            for (int face = 0; face < 2; face++)
            {
                var z      = face == 0 ? nearZ : farZ;
                var extent = perspective ? new double2(slopeX, slopeY) * (z - apexZ) : halfSize.xy;
                for (int corner = 0; corner < 4; corner++)
                {
                    var sign     = new double2((corner & 1) == 0 ? -1 : 1, (corner & 2) == 0 ? -1 : 1);
                    var point    = new double4(center.xy + sign * extent, z, 1);
                    var treeSide = math.mul(decalToTree, point).xyz;
                    boundsMin = math.min(boundsMin, treeSide);
                    boundsMax = math.max(boundsMax, treeSide);
                }
            }

            var flags = DecalVolumeFlags.None;
            if (perspective)          flags |= DecalVolumeFlags.Perspective;
            if (settings.transparent) flags |= DecalVolumeFlags.Transparent;
            var cosMaxAngle = -1.0;
            if (settings.maxAngle < 180.0f)
            {
                flags |= DecalVolumeFlags.AngleLimited;
                cosMaxAngle = math.cos(math.radians(math.clamp((double)settings.maxAngle, 0.0, 180.0)));
            }

            var uvScale = settings.uvScale;
            if (math.all(uvScale == float2.zero))
                uvScale = new float2(1, 1);

            volume = new DecalVolume
            {
                entityID        = instance.entityID,
                order           = settings.order,
                flags           = flags,
                renderMaterial  = instance.renderMaterial,
                materialFlags   = instance.destinationFlags,
                surfaceOffset   = settings.transparent ? math.max(0, settings.surfaceOffset) : 0,
                uvScale         = uvScale,
                uvOffset        = settings.uvOffset,
                cosMaxAngle     = cosMaxAngle,

                treeToDecal     = treeToDecal,
                center          = center,
                size            = size,
                focal           = focal,

                facingPoint     = math.mul(decalToTree, new double4(center, 1)).xyz,
                axis            = math.normalizesafe(math.mul(decalToTree, new double4(0, 0, 1, 0)).xyz),
                apex            = perspective ? math.mul(decalToTree, new double4(center.xy, apexZ, 1)).xyz : double3.zero,
                planes          = planes,
                boundsMin       = (float3)(boundsMin - kPlaneEpsilon),
                boundsMax       = (float3)(boundsMax + kPlaneEpsilon)
            };
            return true;
        }

        // Whether the decal may draw on a surface of a brush: always, unless it has targets and none of them is it
        public readonly bool Targets(Unity.Collections.NativeArray<ChiselDecalTarget> targets, ulong brushEntityID, int surfaceIndex)
        {
            if (targetCount <= 0)
                return true;
            for (int i = 0; i < targetCount; i++)
            {
                if (targets[targetStart + i].Matches(brushEntityID, surfaceIndex))
                    return true;
            }
            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly double Distance(int plane, double3 point)
        {
            var p = planes[plane];
            return math.dot(p.xyz, point) + p.w;
        }

        public readonly bool Contains(double3 point)
        {
            for (int i = 0; i < DecalPlanes.kCount; i++)
            {
                if (Distance(i, point) < -kPlaneEpsilon)
                    return false;
            }
            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly bool Overlaps(float3 min, float3 max)
        {
            return math.all(max >= boundsMin) && math.all(min <= boundsMax);
        }

        // Whether all of the points are outside one of the planes, which means none of what they span is inside.
        public readonly bool AllOutsideOnePlane(double3 a, double3 b, double3 c)
        {
            for (int i = 0; i < DecalPlanes.kCount; i++)
            {
                if (Distance(i, a) < -kPlaneEpsilon &&
                    Distance(i, b) < -kPlaneEpsilon &&
                    Distance(i, c) < -kPlaneEpsilon)
                    return true;
            }
            return false;
        }

        // Where on the image a point lands: (0,0) at the image's lower left, (1,1) at its upper right.
        public readonly double2 ProjectImage(double3 treePosition)
        {
            var local  = math.mul(treeToDecal, new double4(treePosition, 1)).xyz;
            var offset = local.xy - center.xy;
            if (IsPerspective)
            {
                var depth = local.z - (center.z - focal);
                if (depth > 1e-12)
                    offset *= focal / depth;
            }
            return new double2(0.5 + offset.x / size.x, 0.5 + offset.y / size.y);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly float2 ProjectUV(double3 treePosition)
        {
            return uvOffset + uvScale * (float2)ProjectImage(treePosition);
        }

        // Whether a surface takes this decal. surfaceNormal is the unit normal of its visible side, and
        // surfacePoint a point on it. A surface facing away from the box's center never does.
        public readonly bool Accepts(double3 surfaceNormal, double3 surfacePoint)
        {
            if (math.dot(surfaceNormal, facingPoint - surfacePoint) < -kPlaneEpsilon)
                return false;
            if ((flags & DecalVolumeFlags.AngleLimited) != 0)
            {
                var towardsProjector = IsPerspective ? math.normalizesafe(apex - surfacePoint) : -axis;
                if (math.dot(surfaceNormal, towardsProjector) < cosMaxAngle)
                    return false;
            }
            return true;
        }

        public readonly SurfaceDestinationFlags GetDestinationFlags(SurfaceDestinationFlags surfaceFlags)
        {
            const SurfaceDestinationFlags kRendered = SurfaceDestinationFlags.Renderable | SurfaceDestinationFlags.ShadowReceiving;
            const SurfaceDestinationFlags kKept     = SurfaceDestinationFlags.ShadowCasting | SurfaceDestinationFlags.Collidable |
                                                     SurfaceDestinationFlags.ExcludedFromGlobalIllumination;
            var rendered = surfaceFlags & materialFlags & kRendered;
            if ((rendered & SurfaceDestinationFlags.Renderable) == 0)
                return SurfaceDestinationFlags.None;
            if (IsTransparent)
                return rendered.Normalize();
            return (rendered | (surfaceFlags & kKept)).Normalize();
        }
    }
}
