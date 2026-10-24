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
            var settings = decal.Settings;
            var size = math.abs((float3)settings.size);
            if (!(size.x > kEpsilon) || !(size.y > kEpsilon))
                return false;

            // one image round the decal's position on the edge (kept on the edge), its whole length near the ends
            var fill = decal.TrimFill;
            if (fill == ChiselTrimFill.Single && size.x < to - from)
            {
                var position = (float3)model.transform.worldToLocalMatrix.MultiplyPoint3x4(decal.transform.position);
                var at = math.clamp(math.dot(position - point, direction), from, to);
                from = math.clamp(at - size.x * 0.5f, from, to - size.x);
                to   = from + size.x;
            }
            var start  = point + direction * from;
            var length = to - from;
            float repeats = fill switch
            {
                ChiselTrimFill.Repeat => length / size.x,
                ChiselTrimFill.Fit    => math.max(1, math.round(length / size.x)),
                _                     => 1
            };
            a = MakeHalf(decal, faceA, faceB.normal, targets[0], start, direction, length, repeats, settings, entityID,                renderMaterial, destinationFlags, lower: true);
            b = MakeHalf(decal, faceB, faceA.normal, targets[1], start, direction, length, repeats, settings, entityID | kSecondHalf, renderMaterial, destinationFlags, lower: decal.TrimMirror);
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

        /// <summary>The surface (of its generator: the polygon's descriptionIndex) a decal target names for a brush's polygon</summary>
        public static int SurfaceOf(CSGTreeBrush brush, int polygonIndex)
        {
            var blob = BrushMeshManager.GetBrushMeshBlob(brush.BrushMesh);
            if (!blob.IsCreated || polygonIndex < 0 || polygonIndex >= blob.Value.polygons.Length)
                return -1;
            return blob.Value.polygons[polygonIndex].descriptionIndex;
        }

        /// <summary>
        /// The edge of a brush's polygon nearest to a point (both in the tree's space): its ends, and the polygon of the
        /// same brush on its other side
        /// </summary>
        public static bool NearestEdge(CSGTreeBrush brush, int polygonIndex, float3 treePoint, out float3 from, out float3 to, out int otherPolygon)
        {
            from = to = float3.zero;
            otherPolygon = -1;
            var blob = BrushMeshManager.GetBrushMeshBlob(brush.BrushMesh);
            if (!blob.IsCreated || polygonIndex < 0 || polygonIndex >= blob.Value.polygons.Length)
                return false;
            ref var mesh = ref blob.Value;
            ref var polygon = ref mesh.polygons[polygonIndex];
            var nodeToTree = brush.NodeToTreeSpaceMatrix;
            var nearest = float.PositiveInfinity;
            for (int e = 0; e < polygon.edgeCount; e++)
            {
                var edge = polygon.firstEdge + e;
                var next = polygon.firstEdge + (e + 1) % polygon.edgeCount;
                var a = math.transform(nodeToTree, mesh.localVertices[mesh.halfEdges[edge].vertexIndex]);
                var b = math.transform(nodeToTree, mesh.localVertices[mesh.halfEdges[next].vertexIndex]);
                var ab = b - a;
                var t = math.saturate(math.dot(treePoint - a, ab) / math.max(math.lengthsq(ab), 1e-12f));
                var distance = math.distancesq(treePoint, a + ab * t);
                if (distance >= nearest)
                    continue;
                nearest = distance;
                from = a;
                to = b;
                // the polygon on the other side: the one across either half edge here that has both its vertices
                var vertexA = mesh.halfEdges[edge].vertexIndex;
                var vertexB = mesh.halfEdges[next].vertexIndex;
                otherPolygon = -1;
                for (int h = 0; h < 2 && otherPolygon < 0; h++)
                {
                    var twin = mesh.halfEdges[h == 0 ? edge : next].twinIndex;
                    if (twin < 0 || twin >= mesh.halfEdgePolygonIndices.Length)
                        continue;
                    var across = mesh.halfEdgePolygonIndices[twin];
                    if (across != polygonIndex && HasVertices(ref mesh, across, vertexA, vertexB))
                        otherPolygon = across;
                }
            }
            return nearest < float.PositiveInfinity;
        }

        static bool HasVertices(ref BrushMeshBlob mesh, int polygonIndex, int vertexA, int vertexB)
        {
            ref var polygon = ref mesh.polygons[polygonIndex];
            bool foundA = false, foundB = false;
            for (int e = 0; e < polygon.edgeCount; e++)
            {
                var vertex = mesh.halfEdges[polygon.firstEdge + e].vertexIndex;
                foundA |= vertex == vertexA;
                foundB |= vertex == vertexB;
            }
            return foundA && foundB;
        }

        /// <summary>Whether a brush's polygon's plane holds the line through these points (in the tree's space)</summary>
        public static bool PlaneHolds(CSGTreeBrush brush, int polygonIndex, float3 a, float3 b)
        {
            var blob = BrushMeshManager.GetBrushMeshBlob(brush.BrushMesh);
            if (!blob.IsCreated || polygonIndex < 0 || polygonIndex >= blob.Value.localPlanes.Length)
                return false;
            var plane = math.mul(math.transpose(math.inverse(brush.NodeToTreeSpaceMatrix)), blob.Value.localPlanes[polygonIndex]);
            var length = math.length(plane.xyz);
            if (!(length > kEpsilon))
                return false;
            plane /= length;
            const float kOnPlane = 1e-3f;
            return math.abs(math.dot(plane.xyz, a) + plane.w) < kOnPlane && math.abs(math.dot(plane.xyz, b) + plane.w) < kOnPlane;
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
