using Chisel.Core;
using Unity.Mathematics;

namespace Chisel.Components
{
    // Locking a surface's texture to its object, or to the world (SurfaceDetailFlags.TextureIsInWorldSpace), without
    // changing how it looks now: the surface's uv matrix and hotspot settings are carried over to the other space, so
    // the two only differ once the object moves
    public static class ChiselTextureLock
    {
        public static bool IsInWorldSpace(in SurfaceDetails details) => (details.detailFlags & SurfaceDetailFlags.TextureIsInWorldSpace) != 0;

        // The surface's details in the other space (the flag flipped), looking as they do now
        public static SurfaceDetails Toggled(SurfaceReference surface, SurfaceDetails details)
        {
            var toWorld = !IsInWorldSpace(details);
            if (surface == null || !surface.brush.Valid)
                return Flip(details, toWorld);
            var brushMesh = surface.BrushMeshBlob;
            if (!brushMesh.IsCreated || surface.surfaceIndex < 0 || surface.surfaceIndex >= brushMesh.Value.localPlanes.Length)
                return Flip(details, toWorld);
            var plane      = brushMesh.Value.localPlanes[surface.surfaceIndex];
            var treeToNode = surface.brush.TreeToNodeSpaceMatrix;
            var nodeToTree = surface.brush.NodeToTreeSpaceMatrix;

            // uv = UV0 * treeToPlane * p for the plane's points: the same uvs from the other space's treeToPlane
            var objectToPlane = math.mul(MathExtensions.GenerateLocalToPlaneSpaceMatrix(plane), treeToNode);
            var treePlane     = math.mul(math.transpose(treeToNode), plane);
            treePlane /= math.length(treePlane.xyz);
            var worldToPlane  = MathExtensions.GenerateLocalToPlaneSpaceMatrix(treePlane);
            var from = toWorld ? objectToPlane : worldToPlane;
            var to   = toWorld ? worldToPlane  : objectToPlane;
            details.UV0 = (UVMatrix)math.mul((float4x4)details.UV0, math.mul(from, math.inverse(to)));

            details.hotspot = ToggledHotspot(surface, details.hotspot, toWorld, treePlane.xyz, math.normalizesafe(plane.xyz), nodeToTree, treeToNode);
            return Flip(details, toWorld);
        }

        static SurfaceDetails Flip(SurfaceDetails details, bool toWorld)
        {
            if (toWorld) details.detailFlags |=  SurfaceDetailFlags.TextureIsInWorldSpace;
            else         details.detailFlags &= ~SurfaceDetailFlags.TextureIsInWorldSpace;
            return details;
        }

        // A hotspot surface's settings in the other space: the turn between the object's frame and the world's, its
        // density in the brush's own units or the tree's, and its repeats' offset from the inset corner or the frame's
        // origin (from its last build)
        static HotspotSurface ToggledHotspot(SurfaceReference surface, HotspotSurface hotspot, bool toWorld, float3 normal, float3 localNormal,
                                             float4x4 nodeToTree, float4x4 treeToNode)
        {
            HotspotMatcher.Frame(normal, localNormal, nodeToTree, 0, false, out _, out var objectUp);
            HotspotMatcher.Frame(normal, 0, false, out var worldRight, out var worldUp);
            // the world frame turned by delta is the object's: up' = up cos - right sin
            var delta = math.degrees(math.atan2(-math.dot(objectUp, worldRight), math.dot(objectUp, worldUp)));
            var rotation = hotspot.rotation + (toWorld ? delta : -delta);
            rotation -= 360 * math.floor(rotation / 360);
            // the brush's own units along the frame as locked (its rotation there)
            var units = HotspotMatcher.OwnUnits(normal, localNormal, treeToNode, toWorld ? hotspot.rotation : rotation, hotspot.mirror);
            hotspot.rotation = rotation;
            var density = hotspot.Density * (toWorld ? units : 1 / units);
            hotspot.density = density == 1 ? 0 : density;

            if (ChiselHotspotRepeats.TryGetOrigin(surface, out var interior, out var origin, out var period))
            {
                // the repeats start at the inset corner (object) or on the grid from the frame's origin (world)
                var shift = origin / period;
                var offset = hotspot.offset + (toWorld ? -shift : shift);
                hotspot.offset = interior ? offset - math.floor(offset) : new float2(offset.x - math.floor(offset.x), offset.y - math.floor(offset.y));
            }
            return hotspot;
        }
    }
}
