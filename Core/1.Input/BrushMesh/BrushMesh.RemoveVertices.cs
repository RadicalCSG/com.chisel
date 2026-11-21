using System;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

namespace Chisel.Core
{
    sealed partial class BrushMesh
    {
        // how near a plane of the shape left a face of the brush is to be in it, and a corner to be one of the brush's
        const float kSamePlaneDistance = 1e-3f;
        const float kSamePlaneCosine   = 1 - 1e-4f;
        const float kSameCornerDistance = 1e-3f;

        /// <summary>The brush without the vertices: the convex hull of the ones left, which must be at least four, not all in
        /// one plane. A face in the plane of one of the brush's faces keeps that face's surface; a face that closes where the
        /// vertices were takes the surface of the face it turns least from, of those they were on. The vertices left keep
        /// their positions. False when it can't be done, and the brush is left as it was.</summary>
        public bool RemoveVertices(ICollection<int> removed)
        {
            if (vertices == null || polygons == null || halfEdges == null || removed == null || removed.Count == 0)
                return false;

            var remaining = new List<Vector3>(vertices.Length);
            for (int v = 0; v < vertices.Length; v++)
            {
                if (!removed.Contains(v))
                    remaining.Add(vertices[v]);
            }
            if (remaining.Count < 4 || remaining.Count == vertices.Length)
                return false;

            var hullPlanes = new List<float4>();
            Bounds bounds;
            try
            {
                new ConvexHullCalculator().GenerateHull(remaining, ref hullPlanes, out bounds);
            }
            catch (ArgumentException)
            {
                // all in one plane
                return false;
            }
            if (hullPlanes.Count < 4)
                return false;

            // facing away from the middle of what's left, whichever way the hull gives them
            var middle = Vector3.zero;
            foreach (var point in remaining)
                middle += point;
            middle /= remaining.Count;
            for (int p = 0; p < hullPlanes.Count; p++)
            {
                if (math.dot(hullPlanes[p].xyz, (float3)middle) + hullPlanes[p].w > 0)
                    hullPlanes[p] = -hullPlanes[p];
            }

            var surfaces = new ChiselSurfaceArray();
            BrushMeshFactory.CreateFromPlanes(hullPlanes.ToArray(), bounds, ref surfaces, out var hull);
            if (hull == null || hull.polygons == null || hull.polygons.Length < 4 || hull.vertices == null)
                return false;

            // exactly where they were, not where the planes meet
            for (int v = 0; v < hull.vertices.Length; v++)
            {
                var nearest  = kSameCornerDistance;
                foreach (var point in remaining)
                {
                    var distance = math.distance(hull.vertices[v], (float3)point);
                    if (distance >= nearest)
                        continue;
                    nearest = distance;
                    hull.vertices[v] = point;
                }
            }
            hull.CalculatePlanes();

            // the faces the removed vertices were on
            CalculatePlanes();
            var touched = new List<int>();
            for (int p = 0; p < polygons.Length; p++)
            {
                for (int e = polygons[p].firstEdge; e < polygons[p].firstEdge + polygons[p].edgeCount; e++)
                {
                    if (!removed.Contains(halfEdges[e].vertexIndex))
                        continue;
                    touched.Add(p);
                    break;
                }
            }

            for (int p = 0; p < hull.polygons.Length; p++)
            {
                var plane = hull.planes[p];
                int samePlane = -1, closest = -1;
                var closestCosine = float.MinValue;
                for (int q = 0; q < polygons.Length && samePlane < 0; q++)
                {
                    if (math.dot(planes[q].xyz, plane.xyz) > kSamePlaneCosine && math.abs(planes[q].w - plane.w) < kSamePlaneDistance)
                        samePlane = q;
                }
                if (samePlane < 0)
                {
                    foreach (var q in touched)
                    {
                        var cosine = math.dot(planes[q].xyz, plane.xyz);
                        if (cosine <= closestCosine)
                            continue;
                        closestCosine = cosine;
                        closest = q;
                    }
                }
                var from = samePlane >= 0 ? samePlane : closest;
                hull.polygons[p].descriptionIndex = from >= 0 ? polygons[from].descriptionIndex : 0;
            }

            vertices               = hull.vertices;
            halfEdges              = hull.halfEdges;
            polygons               = hull.polygons;
            planes                 = hull.planes;
            halfEdgePolygonIndices = hull.halfEdgePolygonIndices;
            halfEdgeUVs            = null;
            halfEdgeNormals        = null;
            UpdateHalfEdgePolygonIndices();
            return true;
        }
    }
}
