using System;
using Unity.Collections;
using Unity.Mathematics;

namespace Chisel.Core
{
    /// <summary>How a decal maps its image onto the surfaces it reaches.</summary>
    public enum ChiselDecalProjection : byte
    {
        /// <summary>Straight along the decal's forward axis. The image has the same size at every depth.</summary>
        Orthographic = 0,

        /// <summary>From a point behind the box, like a projector. The image has the decal's size at the center
        /// of the box, and grows with depth.</summary>
        Perspective  = 1,

        /// <summary>From the center out in every direction, like Source's infodecal: every surface the decal reaches
        /// takes the whole image, laid flat on it round the point nearest the center, turned with the surface's
        /// texture.</summary>
        Omni         = 2,

        /// <summary>Like <see cref="Orthographic"/>, but only within an outline in the decal's XY plane (see
        /// <see cref="ChiselDecalInstance.outlineStart"/>) instead of the whole box. Its image spans the outline's bounds.</summary>
        Polygon      = 3
    }

    /// <summary>Which surfaces an omni-directional decal reaches: those passing within the decal's reach of its center.</summary>
    public enum ChiselDecalReach : byte
    {
        /// <summary>Within a sphere: a surface no further than the reach from the center.</summary>
        Sphere = 0,

        /// <summary>Within a cube turned with the decal: a surface passing through it.</summary>
        Cube   = 1
    }

    /// <summary>
    /// A decal as its component describes it, in the decal's own space. The decal is a box around
    /// <see cref="center"/>, and projects its image along +Z onto every surface of the model that the box reaches
    /// and that does not face away from the box's center.
    /// </summary>
    [Serializable]
    public struct ChiselDecalSettings : IEquatable<ChiselDecalSettings>
    {
        public const float kMinFieldOfView = 0.1f;
        public const float kMaxFieldOfView = 170.0f;
        public const float kDefaultSurfaceOffset = 0.001f;
        public const float kMaxSkew = 80.0f;

        /// <summary>The center of the box.</summary>
        public float3 center;

        /// <summary>The size of the box. X and Y are the image's width and height (for a perspective decal: at the
        /// center of the box), Z is how deep the decal reaches (for an omni-directional decal: the diameter of its
        /// sphere, or its cube's edge).</summary>
        public float3 size;

        public ChiselDecalProjection projection;

        /// <summary>The shape an omni-directional decal reaches in.</summary>
        public ChiselDecalReach reach;

        /// <summary>The vertical field of view of a perspective decal, in degrees.</summary>
        public float fieldOfView;

        /// <summary>A surface only takes the decal when its normal is within this many degrees of the direction
        /// back to the projector. 180 means no limit: every surface that does not face away from the box's center
        /// takes the decal, however steep.</summary>
        public float maxAngle;

        /// <summary>A transparent decal is drawn over the surfaces it covers. An opaque decal replaces them: the
        /// surface below is removed from the rendered mesh where the decal covers it, but stays in the collider.</summary>
        public bool transparent;

        /// <summary>How far a transparent decal is lifted off its surface, in the decal's model space units, so it
        /// doesn't fight the surface for depth. Opaque decals replace the surface and are never lifted.</summary>
        public float surfaceOffset;

        /// <summary>The image's texture coordinates are <c>uvOffset + uvScale * p</c>, where p goes from (0,0) at the
        /// image's lower left to (1,1) at its upper right.</summary>
        public float2 uvScale;
        public float2 uvOffset;

        /// <summary>How many quarter turns counter-clockwise the image is turned, about its middle, before uvScale and
        /// uvOffset: <c>uvOffset + uvScale * TurnImage(p, imageTurns)</c></summary>
        public int imageTurns;

        /// <summary>How an orthographic box's start (x) and end (y) lean: x moved per unit of y in the decal's space, 0 for
        /// square ends. A trim decal's pieces are cut this way where they meet round a corner.</summary>
        public float2 endSlant;

        /// <summary>Where a transparent decal fades out, by its distance from the camera: x where it starts to fade, y where
        /// it is gone (at a 60 degree field of view). Each fading decal is a renderer of its own, cross-faded by a
        /// LODGroup. A y of 0 never fades; opaque decals never fade, since they replace the surface below.</summary>
        public float2 fadeDistance;

        public readonly bool Fades => transparent && fadeDistance.y > 0;

        /// <summary>How far the decal is sheared, in degrees, so its image frame is a parallelogram: x leans the image's
        /// sides (x moves with y), y leans its top and bottom (y moves with x). The shear is about the decal's origin.</summary>
        public float2 skew;

        /// <summary>The decal's pieces take the texture coordinates of the surface they are on instead of the image's, so
        /// its material lines up with the surface's texturing.</summary>
        public bool surfaceUVs;

        /// <summary>An opaque decal on a hotspot mapped surface: the surface's border runs round it too, as round a hole in
        /// it. Without, the hotspot mapping goes on under it as if it weren't there.</summary>
        public bool hotspotBorder;

        /// <summary>The shear <see cref="skew"/> describes, from the decal's unsheared space to its space</summary>
        public static float4x4 SkewMatrix(float2 skew)
        {
            var lean = math.tan(math.radians(math.clamp(skew, -kMaxSkew, kMaxSkew)));
            return new float4x4(1,      lean.x, 0, 0,
                                lean.y, 1,      0, 0,
                                0,      0,      1, 0,
                                0,      0,      0, 1);
        }

        /// <summary>Where in the image a point p of the decal (0 to 1 across it) looks, for an image turned this many quarter
        /// turns counter-clockwise</summary>
        public static float2 TurnImage(float2 p, int quarterTurns)
        {
            switch (quarterTurns & 3)
            {
                case 1:  return new float2(p.y, 1 - p.x);
                case 2:  return 1 - p;
                case 3:  return new float2(1 - p.y, p.x);
                default: return p;
            }
        }

        /// <summary>
        /// For an image coordinate q = offset + scale * p, the scale and offset that give the same coordinate turned:
        /// TurnImage(q, quarterTurns) = turnedOffset + turnedScale * TurnImage(p, quarterTurns)
        /// </summary>
        public static void TurnMapping(float2 scale, float2 offset, int quarterTurns, out float2 turnedScale, out float2 turnedOffset)
        {
            turnedScale  = (quarterTurns & 1) != 0 ? scale.yx : scale;
            turnedOffset = TurnImage(offset, quarterTurns) - turnedScale * TurnImage(float2.zero, quarterTurns);
        }

        public static ChiselDecalSettings Default => new ChiselDecalSettings
        {
            center        = float3.zero,
            size          = new float3(1, 1, 0.25f),
            projection    = ChiselDecalProjection.Orthographic,
            fieldOfView   = 30.0f,
            maxAngle      = 180.0f,
            transparent   = true,
            surfaceOffset = kDefaultSurfaceOffset,
            uvScale       = new float2(1, 1),
            uvOffset      = float2.zero
        };

        public readonly bool Equals(ChiselDecalSettings other)
        {
            return math.all(center == other.center) &&
                   math.all(size == other.size) &&
                   projection == other.projection &&
                   reach == other.reach &&
                   math.all(fadeDistance == other.fadeDistance) &&
                   fieldOfView == other.fieldOfView &&
                   maxAngle == other.maxAngle &&
                   transparent == other.transparent &&
                   surfaceOffset == other.surfaceOffset &&
                   math.all(uvScale == other.uvScale) &&
                   math.all(uvOffset == other.uvOffset) &&
                   (imageTurns & 3) == (other.imageTurns & 3) &&
                   math.all(endSlant == other.endSlant) &&
                   math.all(skew == other.skew) &&
                   surfaceUVs == other.surfaceUVs &&
                   hotspotBorder == other.hotspotBorder;
        }

        public override readonly bool Equals(object obj) => obj is ChiselDecalSettings other && Equals(other);

        public override readonly int GetHashCode()
        {
            var hash = math.hash(center);
            hash = math.hash(new uint2(hash, math.hash(size)));
            hash = math.hash(new uint3(hash, (uint)projection, transparent ? 1u : 0u));
            hash = math.hash(new uint2(hash, math.hash(new float3(fieldOfView, maxAngle, surfaceOffset))));
            hash = math.hash(new uint3(hash, math.hash(uvScale), math.hash(uvOffset)));
            hash = math.hash(new uint3(hash, math.hash(endSlant), (uint)(imageTurns & 3)));
            hash = math.hash(new uint3(hash, math.hash(skew), (uint)reach));
            hash = math.hash(new uint2(hash, math.hash(fadeDistance)));
            hash = math.hash(new uint3(hash, surfaceUVs ? 1u : 0u, hotspotBorder ? 1u : 0u));
            return (int)hash;
        }
    }

    /// <summary>
    /// A surface a decal is limited to: surface <see cref="surfaceIndex"/> of the brush whose node was created with
    /// <see cref="brushEntityID"/>. The surface index is the one the brush's surface array uses.
    /// </summary>
    public struct ChiselDecalTarget : IEquatable<ChiselDecalTarget>
    {
        public const int kAllSurfaces = -1;

        public ulong brushEntityID;

        /// <summary>The brush's surface, or <see cref="kAllSurfaces"/> for every surface of the brush.</summary>
        public int surfaceIndex;

        public readonly bool Matches(ulong entityID, int surface)
        {
            return brushEntityID == entityID && (surfaceIndex < 0 || surfaceIndex == surface);
        }

        public readonly bool Equals(ChiselDecalTarget other) => brushEntityID == other.brushEntityID && surfaceIndex == other.surfaceIndex;
        public override readonly bool Equals(object obj) => obj is ChiselDecalTarget other && Equals(other);
        public override readonly int GetHashCode() => (int)math.hash(new uint3((uint)brushEntityID, (uint)(brushEntityID >> 32), (uint)surfaceIndex));
    }

    /// <summary>One decal as a tree receives it: the settings, where the decal is and what it draws with.</summary>
    public struct ChiselDecalInstance : IEquatable<ChiselDecalInstance>
    {
        /// <summary>Identifies the decal between calls to <see cref="CSGTreeDecalExtensions.SetDecals"/>, for
        /// instance the EntityId of its component. Two decals of one tree must not share it.</summary>
        public ulong entityID;

        /// <summary>From the decal's space to the space of the tree (the model).</summary>
        public float4x4 decalToTree;

        public ChiselDecalSettings settings;

        /// <summary>The material the decal draws with: the EntityId of a Material, as a ulong. A decal without one
        /// draws nothing, and removes nothing.</summary>
        public ulong renderMaterial;

        /// <summary>The destination flags of the decal's material. Only Renderable and ShadowReceiving are used: a
        /// decal is drawn only where both the surface and this allow it. Casting shadows follows the surface
        /// below; a decal never collides (the collider keeps the uncut surface).</summary>
        public SurfaceDestinationFlags destinationFlags;

        /// <summary>
        /// With a <see cref="targetCount"/> above 0 the decal only draws on the surfaces
        /// <c>targets[targetStart .. targetStart + targetCount - 1]</c> of the targets handed to
        /// <see cref="CSGTreeDecalExtensions.SetDecals(CSGTree, NativeArray{ChiselDecalInstance}, NativeArray{ChiselDecalTarget})"/>.
        /// Other surfaces it reaches show nothing of it; an opaque decal still cuts them, which they don't show either.
        /// </summary>
        public int targetStart;
        public int targetCount;

        /// <summary>
        /// A <see cref="ChiselDecalProjection.Polygon"/> decal's outline: <c>outlines[outlineStart .. outlineStart + outlineCount - 1]</c>
        /// of the outlines handed to <see cref="CSGTreeDecalExtensions.SetDecals(CSGTree, NativeArray{ChiselDecalInstance}, NativeArray{ChiselDecalTarget}, NativeArray{float2})"/>,
        /// in the decal's XY plane, either way round. It must not cross itself, and has no holes.
        /// </summary>
        public int outlineStart;
        public int outlineCount;

        public readonly bool Equals(ChiselDecalInstance other)
        {
            return entityID == other.entityID &&
                   decalToTree.Equals(other.decalToTree) &&
                   settings.Equals(other.settings) &&
                   renderMaterial == other.renderMaterial &&
                   destinationFlags == other.destinationFlags &&
                   targetStart == other.targetStart &&
                   targetCount == other.targetCount &&
                   outlineStart == other.outlineStart &&
                   outlineCount == other.outlineCount;
        }

        public override readonly bool Equals(object obj) => obj is ChiselDecalInstance other && Equals(other);

        public override readonly int GetHashCode()
        {
            var hash = math.hash(new uint2((uint)entityID, (uint)(entityID >> 32)));
            hash = math.hash(new uint2(hash, math.hash(decalToTree)));
            hash = math.hash(new uint2(hash, (uint)settings.GetHashCode()));
            hash = math.hash(new uint4(hash, (uint)renderMaterial, (uint)(renderMaterial >> 32), (uint)destinationFlags));
            hash = math.hash(new uint3(hash, (uint)targetStart, (uint)targetCount));
            hash = math.hash(new uint3(hash, (uint)outlineStart, (uint)outlineCount));
            return (int)hash;
        }
    }

    /// <summary>Decals of a <see cref="CSGTree"/>.</summary>
    public static class ChiselDecalMeshes
    {
        /// <summary>The surface parameter of a chunk's transparent decals' mesh: all their materials, one sub-mesh per
        /// material, in the order the decals stack, so that they draw in that order</summary>
        public const ulong kTransparentDecals = ulong.MaxValue;

        /// <summary>The chunk of a fading decal's mesh (see <see cref="ChiselDecalSettings.fadeDistance"/>): its own, so
        /// that it is a renderer of its own</summary>
        public static uint FadeChunkOf(ulong decalEntityID)
        {
            return math.hash(new uint3((uint)decalEntityID, (uint)(decalEntityID >> 32), 0x46414445u));
        }
    }

    public static class CSGTreeDecalExtensions
    {
        /// <summary>
        /// Replaces all decals of the tree at once. Pass every decal the tree should have; they stack in
        /// the order they are given, the last on top (ChiselDecalManager gives them in hierarchy order). Only the brushes
        /// near a decal that was added, removed, moved, changed or stacked otherwise are rebuilt, at the tree's next update.
        /// </summary>
        public static void SetDecals(this CSGTree tree, NativeArray<ChiselDecalInstance> decals)
        {
            ChiselDecalStore.SetDecals(tree, decals, default, default);
        }

        /// <summary>
        /// Replaces all decals of the tree at once, with the surfaces some of them are limited to (see
        /// <see cref="ChiselDecalInstance.targetStart"/>).
        /// </summary>
        public static void SetDecals(this CSGTree tree, NativeArray<ChiselDecalInstance> decals, NativeArray<ChiselDecalTarget> targets)
        {
            ChiselDecalStore.SetDecals(tree, decals, targets, default);
        }

        /// <summary>
        /// Replaces all decals of the tree at once, with the surfaces some of them are limited to and the outlines of the
        /// polygon decals (see <see cref="ChiselDecalInstance.outlineStart"/>).
        /// </summary>
        public static void SetDecals(this CSGTree tree, NativeArray<ChiselDecalInstance> decals, NativeArray<ChiselDecalTarget> targets,
                                     NativeArray<float2> outlines)
        {
            ChiselDecalStore.SetDecals(tree, decals, targets, outlines);
        }

        /// <summary>Removes all decals from the tree.</summary>
        public static void ClearDecals(this CSGTree tree)
        {
            ChiselDecalStore.SetDecals(tree, default, default, default);
        }

        /// <summary>How many decals the tree has.</summary>
        public static int GetDecalCount(this CSGTree tree)
        {
            return ChiselDecalStore.GetDecalCount(tree);
        }
    }
}
