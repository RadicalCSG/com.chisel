using System.Collections.Generic;
using Chisel.Core;
using Unity.Mathematics;
using UnityEngine;

namespace Chisel.Components
{
    /// <summary>
    /// A trim decal lies on the edge where two surfaces meet (surface A and B), its image split along its centre line: the
    /// lower half on A, the upper half on B, each laid flat on its own surface (Documentation~/Design/Decals.md). Each half
    /// is an orthographic box decal limited to its surface: a box in the surface's plane, one side on the edge.
    /// </summary>
    public static class ChiselTrimDecals
    {
        /// <summary>The second half's entity id: the decal's, with this bit (a decal is one entity per id)</summary>
        public const ulong kSecondHalf = 1ul << 62;

        /// <summary>The decal an entity id of a decal's piece belongs to</summary>
        public static ulong DecalEntityID(ulong entityID) => entityID & ~kSecondHalf;

        // how far the boxes reach either side of their surface
        const float kMargin = 0.01f;
        const float kEpsilon = 1e-4f;

        public struct Half
        {
            public ChiselDecalInstance decal;
            public ChiselDecalTarget   target;
        }

        // A surface of a brush, in the tree's space: its plane (unit normal, distance) and its outline
        struct Face
        {
            public float3 normal;
            public float  distance;
            public float3 centroid;
            public List<float3> outline;
        }

        static readonly HashSet<CSGTreeBrush> s_Brushes = new();

        /// <summary>
        /// The two halves of a trim decal, in the space of the model its first surface is in (the model is returned too).
        /// False when the two surfaces aren't in one model, don't meet in a line, or share no stretch of it.
        /// </summary>
        public static bool TryDescribe(ChiselDecalComponent decal, ulong entityID, ulong renderMaterial, SurfaceDestinationFlags destinationFlags,
                                       out ChiselModelComponent model, out Half a, out Half b)
        {
            a = b = default;
            model = null;
            var targets = decal.Targets;
            if (targets.Count < 2 || !TryFace(targets[0], out var modelA, out var faceA) || !TryFace(targets[1], out var modelB, out var faceB) ||
                modelA != modelB)
                return false;
            model = modelA;

            // the line both planes lie on
            var direction = math.cross(faceA.normal, faceB.normal);
            var lengthSq  = math.lengthsq(direction);
            if (lengthSq < kEpsilon * kEpsilon)
                return false;
            var point = (math.cross(faceB.normal, direction) * faceA.distance + math.cross(direction, faceA.normal) * faceB.distance) / lengthSq;
            direction = math.normalize(direction);

            // where both surfaces reach it
            float from = float.NegativeInfinity, to = float.PositiveInfinity;
            if (!Clip(faceA, point, direction, ref from, ref to) || !Clip(faceB, point, direction, ref from, ref to) || to - from <= kEpsilon)
                return false;
            var start  = point + direction * from;
            var length = to - from;

            var settings = decal.Settings;
            var size = math.abs((float3)settings.size);
            if (!(size.x > kEpsilon) || !(size.y > kEpsilon))
                return false;
            float repeats = decal.TrimRepeat switch
            {
                ChiselTrimRepeat.Stretch => 1,
                ChiselTrimRepeat.Fit     => math.max(1, math.round(length / size.x)),
                _                        => length / size.x
            };
            a = MakeHalf(decal, faceA, faceB.normal, targets[0], start, direction, length, repeats, settings, entityID,                renderMaterial, destinationFlags, lower: true);
            b = MakeHalf(decal, faceB, faceA.normal, targets[1], start, direction, length, repeats, settings, entityID | kSecondHalf, renderMaterial, destinationFlags, lower: false);
            return true;
        }

        static Half MakeHalf(ChiselDecalComponent decal, Face face, float3 otherNormal, ChiselDecalSurfaceTarget surface, float3 start, float3 direction, float length,
                             float repeats, ChiselDecalSettings settings, ulong entityID, ulong renderMaterial, SurfaceDestinationFlags destinationFlags, bool lower)
        {
            // across the edge, into the surface: the side its outline is on, or where it lies on both (a wall standing on a
            // floor), the side the other surface faces, as the other side is under the other's brush
            var across = math.normalize(math.cross(face.normal, direction));
            float below = 0, above = 0;
            foreach (var vertex in face.outline)
            {
                var side = math.dot(vertex - start, across);
                below = math.min(below, side);
                above = math.max(above, side);
            }
            var bothSides = below < -kEpsilon && above > kEpsilon;
            if (bothSides ? math.dot(otherNormal, across) < 0 : -below > above)
                across = -across;
            var height = math.abs(settings.size.y);
            var origin = start + direction * (length * 0.5f);
            var decalToTree = new float4x4(new float4(direction, 0), new float4(across, 0), new float4(-face.normal, 0), new float4(origin, 1));

            // the image's v is 0.5 on the edge: down to 0 across A, up to 1 across B; u runs along the edge
            var trimScale  = new float2(repeats, lower ? -0.5f : 0.5f);
            var trimOffset = new float2(0, 0.5f);
            var userScale  = math.all((float2)settings.uvScale == float2.zero) ? new float2(1) : (float2)settings.uvScale;
            var half = settings;
            half.center     = new float3(0, height * 0.25f, 0);
            half.size       = new float3(length, height * 0.5f, kMargin * 2);
            half.projection = ChiselDecalProjection.Orthographic;
            half.uvScale    = userScale * trimScale;
            half.uvOffset   = (float2)settings.uvOffset + userScale * trimOffset;
            return new Half
            {
                decal = new ChiselDecalInstance
                {
                    entityID         = entityID,
                    decalToTree      = decalToTree,
                    settings         = half,
                    renderMaterial   = renderMaterial,
                    destinationFlags = destinationFlags
                },
                target = new ChiselDecalTarget
                {
                    brushEntityID = UnityEngine.EntityId.ToULong(surface.brush.GetEntityId()),
                    surfaceIndex  = surface.surfaceIndex
                }
            };
        }

        // The part of the line inside the face's outline, narrowing [from, to]
        static bool Clip(in Face face, float3 point, float3 direction, ref float from, ref float to)
        {
            var outline = face.outline;
            for (int i = 0; i < outline.Count; i++)
            {
                var v0 = outline[i];
                var v1 = outline[(i + 1) % outline.Count];
                var inward = math.cross(face.normal, v1 - v0);
                var scale  = math.length(inward);
                if (scale < kEpsilon)
                    continue;
                inward /= scale;
                if (math.dot(face.centroid - v0, inward) < 0)
                    inward = -inward;
                // dot(point + t direction - v0, inward) >= 0
                var slope  = math.dot(direction, inward);
                var offset = math.dot(point - v0, inward);
                if (math.abs(slope) < kEpsilon)
                {
                    if (offset < -kEpsilon)
                        return false;
                    continue;
                }
                var t = -offset / slope;
                if (slope > 0) from = math.max(from, t);
                else           to   = math.min(to, t);
            }
            return from < to;
        }

        // A target's surface, of the first brush of its component that has it (depth first)
        static bool TryFace(ChiselDecalSurfaceTarget target, out ChiselModelComponent model, out Face face)
        {
            face = default;
            model = null;
            if (target.brush == null || target.surfaceIndex < 0 || target.brush is not ChiselGeneratorComponent generator)
                return false;
            model = generator.hierarchyItem.Model;
            if (model == null)
                return false;
            return TryFace(generator.TopTreeNode, target.surfaceIndex, out face);
        }

        static bool TryFace(CSGTreeNode node, int surfaceIndex, out Face face)
        {
            face = default;
            if (!node.Valid)
                return false;
            if (node.Type == CSGNodeType.Brush)
                return TryBrushFace((CSGTreeBrush)node, surfaceIndex, out face);
            for (int i = 0; i < node.Count; i++)
            {
                if (TryFace(node[i], surfaceIndex, out face))
                    return true;
            }
            return false;
        }

        static bool TryBrushFace(CSGTreeBrush brush, int surfaceIndex, out Face face)
        {
            face = default;
            var blob = BrushMeshManager.GetBrushMeshBlob(brush.BrushMesh);
            if (!blob.IsCreated)
                return false;
            ref var mesh = ref blob.Value;
            // a target's surface is the generator's (the polygon's descriptionIndex), as the decals' targets match it
            var polygonIndex = -1;
            for (int p = 0; p < mesh.polygons.Length && p < mesh.localPlanes.Length; p++)
            {
                if (mesh.polygons[p].descriptionIndex == surfaceIndex)
                {
                    polygonIndex = p;
                    break;
                }
            }
            if (polygonIndex < 0)
                return false;
            var nodeToTree = brush.NodeToTreeSpaceMatrix;
            var planeToTree = math.transpose(math.inverse(nodeToTree));
            var plane = math.mul(planeToTree, mesh.localPlanes[polygonIndex]);
            var normalLength = math.length(plane.xyz);
            if (!(normalLength > kEpsilon))
                return false;
            ref var polygon = ref mesh.polygons[polygonIndex];
            var outline = new List<float3>(polygon.edgeCount);
            var centroid = float3.zero;
            for (int e = 0; e < polygon.edgeCount; e++)
            {
                var vertex = math.transform(nodeToTree, mesh.localVertices[mesh.halfEdges[polygon.firstEdge + e].vertexIndex]);
                outline.Add(vertex);
                centroid += vertex;
            }
            if (outline.Count < 3)
                return false;
            face = new Face
            {
                normal   = plane.xyz / normalLength,
                distance = -plane.w / normalLength,
                centroid = centroid / outline.Count,
                outline  = outline
            };
            return true;
        }
    }
}
