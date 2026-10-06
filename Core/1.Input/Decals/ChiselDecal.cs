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
        Perspective  = 1
    }

    /// <summary>
    /// A decal as its component describes it, in the decal's own space. The decal is a box around
    /// <see cref="center"/>, and projects its image along +Z onto every surface of the model that the box reaches
    /// and that does not face away from the box's center.
    /// </summary>
    /// <remarks>See Documentation~/Design/Decals.md.</remarks>
    [Serializable]
    public struct ChiselDecalSettings : IEquatable<ChiselDecalSettings>
    {
        public const float kMinFieldOfView = 0.1f;
        public const float kMaxFieldOfView = 170.0f;
        public const float kDefaultSurfaceOffset = 0.001f;

        /// <summary>The center of the box.</summary>
        public float3 center;

        /// <summary>The size of the box. X and Y are the image's width and height (for a perspective decal: at the
        /// center of the box), Z is how deep the decal reaches.</summary>
        public float3 size;

        public ChiselDecalProjection projection;

        /// <summary>The vertical field of view of a perspective decal, in degrees.</summary>
        public float fieldOfView;

        /// <summary>A surface only takes the decal when its normal is within this many degrees of the direction
        /// back to the projector. 180 means no limit: every surface that does not face away from the box's center
        /// takes the decal, however steep.</summary>
        public float maxAngle;

        /// <summary>A transparent decal is drawn over the surfaces it covers. An opaque decal replaces them: the
        /// surface below is removed from the rendered mesh where the decal covers it, but stays in the collider.</summary>
        public bool transparent;

        /// <summary>Decals with a higher order are drawn on top of decals with a lower order.</summary>
        public int order;

        /// <summary>How far a transparent decal is lifted off its surface, in the decal's model space units, so it
        /// doesn't fight the surface for depth. Opaque decals replace the surface and are never lifted.</summary>
        public float surfaceOffset;

        /// <summary>The image's texture coordinates are <c>uvOffset + uvScale * p</c>, where p goes from (0,0) at the
        /// image's lower left to (1,1) at its upper right.</summary>
        public float2 uvScale;
        public float2 uvOffset;

        public static ChiselDecalSettings Default => new ChiselDecalSettings
        {
            center        = float3.zero,
            size          = new float3(1, 1, 0.25f),
            projection    = ChiselDecalProjection.Orthographic,
            fieldOfView   = 30.0f,
            maxAngle      = 180.0f,
            transparent   = true,
            order         = 0,
            surfaceOffset = kDefaultSurfaceOffset,
            uvScale       = new float2(1, 1),
            uvOffset      = float2.zero
        };

        public readonly bool Equals(ChiselDecalSettings other)
        {
            return math.all(center == other.center) &&
                   math.all(size == other.size) &&
                   projection == other.projection &&
                   fieldOfView == other.fieldOfView &&
                   maxAngle == other.maxAngle &&
                   transparent == other.transparent &&
                   order == other.order &&
                   surfaceOffset == other.surfaceOffset &&
                   math.all(uvScale == other.uvScale) &&
                   math.all(uvOffset == other.uvOffset);
        }

        public override readonly bool Equals(object obj) => obj is ChiselDecalSettings other && Equals(other);

        public override readonly int GetHashCode()
        {
            var hash = math.hash(center);
            hash = math.hash(new uint2(hash, math.hash(size)));
            hash = math.hash(new uint3(hash, (uint)projection, transparent ? 1u : 0u));
            hash = math.hash(new uint2(hash, math.hash(new float3(fieldOfView, maxAngle, surfaceOffset))));
            hash = math.hash(new uint2(hash, (uint)order));
            hash = math.hash(new uint3(hash, math.hash(uvScale), math.hash(uvOffset)));
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
        /// decal is drawn only where both the surface and this allow it. Casting shadows and colliding follow the
        /// surface below.</summary>
        public SurfaceDestinationFlags destinationFlags;

        /// <summary>
        /// With a <see cref="targetCount"/> above 0 the decal only draws on the surfaces
        /// <c>targets[targetStart .. targetStart + targetCount - 1]</c> of the targets handed to
        /// <see cref="CSGTreeDecalExtensions.SetDecals(CSGTree, NativeArray{ChiselDecalInstance}, NativeArray{ChiselDecalTarget})"/>.
        /// Other surfaces it reaches show nothing of it; an opaque decal still cuts them, which they don't show either.
        /// </summary>
        public int targetStart;
        public int targetCount;

        public readonly bool Equals(ChiselDecalInstance other)
        {
            return entityID == other.entityID &&
                   decalToTree.Equals(other.decalToTree) &&
                   settings.Equals(other.settings) &&
                   renderMaterial == other.renderMaterial &&
                   destinationFlags == other.destinationFlags &&
                   targetStart == other.targetStart &&
                   targetCount == other.targetCount;
        }

        public override readonly bool Equals(object obj) => obj is ChiselDecalInstance other && Equals(other);

        public override readonly int GetHashCode()
        {
            var hash = math.hash(new uint2((uint)entityID, (uint)(entityID >> 32)));
            hash = math.hash(new uint2(hash, math.hash(decalToTree)));
            hash = math.hash(new uint2(hash, (uint)settings.GetHashCode()));
            hash = math.hash(new uint4(hash, (uint)renderMaterial, (uint)(renderMaterial >> 32), (uint)destinationFlags));
            hash = math.hash(new uint3(hash, (uint)targetStart, (uint)targetCount));
            return (int)hash;
        }
    }

    /// <summary>Decals of a <see cref="CSGTree"/>. See Documentation~/Design/Decals.md.</summary>
    public static class CSGTreeDecalExtensions
    {
        /// <summary>
        /// Replaces all decals of the tree at once. Pass every decal the tree should have, in any order. Only the
        /// brushes near a decal that was added, removed, moved or changed are rebuilt, at the tree's next update.
        /// </summary>
        public static void SetDecals(this CSGTree tree, NativeArray<ChiselDecalInstance> decals)
        {
            ChiselDecalStore.SetDecals(tree, decals, default);
        }

        /// <summary>
        /// Replaces all decals of the tree at once, with the surfaces some of them are limited to (see
        /// <see cref="ChiselDecalInstance.targetStart"/>).
        /// </summary>
        public static void SetDecals(this CSGTree tree, NativeArray<ChiselDecalInstance> decals, NativeArray<ChiselDecalTarget> targets)
        {
            ChiselDecalStore.SetDecals(tree, decals, targets);
        }

        /// <summary>Removes all decals from the tree.</summary>
        public static void ClearDecals(this CSGTree tree)
        {
            ChiselDecalStore.SetDecals(tree, default, default);
        }

        /// <summary>How many decals the tree has.</summary>
        public static int GetDecalCount(this CSGTree tree)
        {
            return ChiselDecalStore.GetDecalCount(tree);
        }
    }
}
