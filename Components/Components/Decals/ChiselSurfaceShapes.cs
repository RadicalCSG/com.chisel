using System;
using System.Collections.Generic;
using Chisel.Core;
using Unity.Mathematics;
using UnityEngine;

namespace Chisel.Components
{
    /// <summary>Where a corner of a shape drawn on a surface is held: on an edge of the surface's outline, or nowhere</summary>
    [Serializable]
    public struct ChiselDecalCornerAnchor
    {
        public static readonly ChiselDecalCornerAnchor None = new() { edge = -1 };

        /// <summary>The edge's number along the surface's outline, -1 for a corner that is held nowhere</summary>
        public int edge;

        /// <summary>How far along the edge, from its start (0, the outline's corner) to its end (1)</summary>
        public float along;

        public readonly bool IsAnchored => edge >= 0;
    }

    /// <summary>
    /// Shapes drawn on a surface (polygon decals): their corners on the surface's edges stay on them when the brush
    /// changes, and the shape stays on the surface's plane.
    /// </summary>
    public static class ChiselSurfaceShapes
    {
        // A corner this close to an edge (world units) is on it
        public const float kOnEdgeDistance = 1e-3f;

        /// <summary>The outline of a brush's surface in world space, in order, and its plane (normal out of the brush). False
        /// when the brush has no such surface.</summary>
        public static bool SurfaceOutline(ChiselNodeComponent brush, int surfaceIndex, List<Vector3> outline, out Plane plane)
        {
            outline.Clear();
            plane = default;
            if (brush == null || brush.TopTreeNode.Type != CSGNodeType.Brush)
                return false;
            var treeBrush = (CSGTreeBrush)brush.TopTreeNode;
            // a brush without its mesh yet has no surfaces
            if (!treeBrush.Valid || !treeBrush.BrushMesh.Valid)
                return false;
            var blob = BrushMeshManager.GetBrushMeshBlob(treeBrush.BrushMesh);
            if (!blob.IsCreated)
                return false;
            ref var mesh = ref blob.Value;
            var polygonIndex = -1;
            for (int p = 0; p < mesh.polygons.Length && polygonIndex < 0; p++)
            {
                if (mesh.polygons[p].descriptionIndex == surfaceIndex)
                    polygonIndex = p;
            }
            if (polygonIndex < 0)
                return false;

            var model   = brush.hierarchyItem.Model;
            var toWorld = math.mul(model != null ? (float4x4)model.transform.localToWorldMatrix : float4x4.identity, treeBrush.NodeToTreeSpaceMatrix);
            ref var polygon = ref mesh.polygons[polygonIndex];
            for (int e = 0; e < polygon.edgeCount; e++)
                outline.Add(math.transform(toWorld, mesh.localVertices[mesh.halfEdges[polygon.firstEdge + e].vertexIndex]));
            if (outline.Count < 3)
                return false;

            var normal = Vector3.zero;
            for (int i = 0; i < outline.Count; i++)
                normal += Vector3.Cross(outline[i], outline[(i + 1) % outline.Count]);
            var localNormal = (Vector3)mesh.localPlanes[polygonIndex].xyz;
            var worldNormal = ((Matrix4x4)toWorld).inverse.transpose.MultiplyVector(localNormal).normalized;
            if (Vector3.Dot(normal, worldNormal) < 0)
                normal = -normal;
            if (normal.sqrMagnitude < 1e-12f)
                normal = worldNormal;
            plane = new Plane(normal.normalized, outline[0]);
            return true;
        }

        /// <summary>The edge of the outline a point lies on, if any (within kOnEdgeDistance)</summary>
        public static ChiselDecalCornerAnchor AnchorOf(IReadOnlyList<Vector3> outline, Vector3 point)
        {
            var best = ChiselDecalCornerAnchor.None;
            var bestDistance = kOnEdgeDistance;
            for (int e = 0; e < outline.Count; e++)
            {
                var a = outline[e];
                var b = outline[(e + 1) % outline.Count];
                var ab = b - a;
                var lengthSq = ab.sqrMagnitude;
                if (lengthSq < 1e-12f)
                    continue;
                var t = Mathf.Clamp01(Vector3.Dot(point - a, ab) / lengthSq);
                var distance = Vector3.Distance(point, a + ab * t);
                if (distance > bestDistance)
                    continue;
                // a corner of the outline is the start of its edge
                if (t >= 1)
                {
                    best = new ChiselDecalCornerAnchor { edge = (e + 1) % outline.Count, along = 0 };
                } else
                    best = new ChiselDecalCornerAnchor { edge = e, along = t };
                bestDistance = distance;
            }
            return best;
        }

        /// <summary>Where an anchored corner is on the outline now</summary>
        public static Vector3 PositionOf(IReadOnlyList<Vector3> outline, ChiselDecalCornerAnchor anchor)
        {
            var a = outline[anchor.edge % outline.Count];
            var b = outline[(anchor.edge + 1) % outline.Count];
            return Vector3.LerpUnclamped(a, b, anchor.along);
        }

        /// <summary>The nearest point on the outline's edges, within maxDistance (world units) of the point</summary>
        public static bool NearestOnOutline(IReadOnlyList<Vector3> outline, Vector3 point, float maxDistance, float cornerDistance, out Vector3 nearest)
        {
            nearest = point;
            var found = false;
            // corners first: within cornerDistance a corner wins over an edge
            for (int i = 0; i < outline.Count; i++)
            {
                var distance = Vector3.Distance(point, outline[i]);
                if (distance <= cornerDistance)
                {
                    cornerDistance = distance;
                    nearest = outline[i];
                    found = true;
                }
            }
            if (found)
                return true;
            for (int e = 0; e < outline.Count; e++)
            {
                var a = outline[e];
                var ab = outline[(e + 1) % outline.Count] - a;
                var t = Mathf.Clamp01(Vector3.Dot(point - a, ab) / Mathf.Max(ab.sqrMagnitude, 1e-12f));
                var on = a + ab * t;
                var distance = Vector3.Distance(point, on);
                if (distance <= maxDistance)
                {
                    maxDistance = distance;
                    nearest = on;
                    found = true;
                }
            }
            return found;
        }

        /// <summary>
        /// A polygon decal as its surface has it now: its frame laid on the surface's plane (world space, Z into the surface),
        /// and its corners in that frame's XY, the anchored ones where their edges are now. False when it has no surface
        /// to be on: then it is where its transform and outline put it.
        /// </summary>
        public static bool Resolve(ChiselDecalComponent decal, out Matrix4x4 frame, List<Vector2> corners)
        {
            frame = decal.transform.localToWorldMatrix;
            corners.Clear();
            var polygon = decal.Polygon;
            foreach (var corner in polygon)
                corners.Add(corner);
            var targets = decal.Targets;
            var outline = new List<Vector3>();
            if (targets.Count == 0 || targets[0].brush == null ||
                !SurfaceOutline(targets[0].brush, targets[0].surfaceIndex, outline, out var plane))
                return false;

            // the decal's frame turned onto the surface's plane, and moved onto it
            var transform = decal.transform;
            var forward   = -plane.normal;
            var turn      = Quaternion.FromToRotation(transform.forward, forward);
            var rotation  = turn * transform.rotation;
            var position  = plane.ClosestPointOnPlane(transform.position);
            frame = Matrix4x4.TRS(position, rotation, transform.lossyScale);
            var toFrame = frame.inverse;
            for (int i = 0; i < corners.Count; i++)
            {
                var anchor = decal.AnchorOf(i);
                if (!anchor.IsAnchored || anchor.edge >= outline.Count)
                    continue;
                corners[i] = toFrame.MultiplyPoint3x4(PositionOf(outline, anchor));
            }
            return true;
        }
    }
}
