using System;
using System.Collections.Generic;
using Chisel.Core;
using UnityEngine;

namespace Chisel.Editors
{
    // Where the clip tool's points go on the brushes it cuts: a vertex or an edge near the mouse on screen before the face
    // the mouse ray enters, so lines can be drawn exactly from edge to edge. Each one is on a surface: a face's own, or for
    // a vertex or an edge the face beside it that faces the camera most.
    static class ChiselClipPicking
    {
        public enum Kind { Face, Edge, Vertex }

        public readonly struct Brush
        {
            public readonly Matrix4x4 toWorld;
            public readonly BrushMesh mesh;
            public Brush(Matrix4x4 toWorld, BrushMesh mesh) { this.toWorld = toWorld; this.mesh = mesh; }
        }

        public struct Pick
        {
            public Kind    kind;
            public int     brush;       // index into the brushes picked from
            public Vector3 point;       // in the world
            public Vector3 normal;      // of the surface it is on, in the world
            public Vector3 edgeStart;   // for an edge, its ends
            public Vector3 edgeEnd;
        }

        /// <summary>The vertex nearest the mouse on screen, within snapDistance, else such an edge, else the face the ray
        /// enters first; toGUI gives a world point's place on screen. Of those as near on screen, the one nearest along the
        /// ray. False when the ray passes by every brush.</summary>
        public static bool TryPick(Ray ray, IReadOnlyList<Brush> brushes, Func<Vector3, Vector2> toGUI, Vector2 mouse, float snapDistance, out Pick pick)
        {
            pick = default;
            var towardsCamera = -ray.direction.normalized;

            // vertices
            var bestScreen = snapDistance;
            var bestDepth  = float.PositiveInfinity;
            var found      = false;
            for (int b = 0; b < brushes.Count; b++)
            {
                var mesh = brushes[b].mesh;
                if (mesh?.vertices == null)
                    continue;
                for (int v = 0; v < mesh.vertices.Length; v++)
                {
                    var world = brushes[b].toWorld.MultiplyPoint((Vector3)mesh.vertices[v]);
                    var gui   = toGUI(world);
                    if (!CouldBeNearer(gui, mouse, bestScreen) || IsHidden(world, ray, brushes) ||
                        !IsNearer(gui, world, ray, mouse, ref bestScreen, ref bestDepth))
                        continue;
                    found = true;
                    pick  = new Pick { kind = Kind.Vertex, brush = b, point = world, normal = VertexNormal(brushes[b], v, towardsCamera) };
                }
            }
            if (found)
                return true;

            // edges
            bestScreen = snapDistance;
            bestDepth  = float.PositiveInfinity;
            for (int b = 0; b < brushes.Count; b++)
            {
                var mesh = brushes[b].mesh;
                if (mesh?.halfEdges == null)
                    continue;
                for (int e = 0; e < mesh.halfEdges.Length; e++)
                {
                    var twin = mesh.halfEdges[e].twinIndex;
                    if (twin < e)
                        continue;
                    var start = brushes[b].toWorld.MultiplyPoint((Vector3)mesh.vertices[mesh.halfEdges[e].vertexIndex]);
                    var end   = brushes[b].toWorld.MultiplyPoint((Vector3)mesh.vertices[mesh.halfEdges[twin].vertexIndex]);
                    var world = ClosestOnSegment(ray, start, end);
                    var gui   = toGUI(world);
                    if (!CouldBeNearer(gui, mouse, bestScreen) || IsHidden(world, ray, brushes) ||
                        !IsNearer(gui, world, ray, mouse, ref bestScreen, ref bestDepth))
                        continue;
                    found = true;
                    var normal = MostFacing(FaceNormal(brushes[b], mesh.halfEdgePolygonIndices[e]), FaceNormal(brushes[b], mesh.halfEdgePolygonIndices[twin]), towardsCamera);
                    pick = new Pick { kind = Kind.Edge, brush = b, point = world, normal = normal, edgeStart = start, edgeEnd = end };
                }
            }
            if (found)
                return true;

            // faces
            var nearest = float.PositiveInfinity;
            for (int b = 0; b < brushes.Count; b++)
            {
                if (!TryEnter(ray, brushes[b], out var distance, out var face) || distance >= nearest)
                    continue;
                nearest = distance;
                found   = true;
                pick    = new Pick { kind = Kind.Face, brush = b, point = ray.GetPoint(distance), normal = FaceNormal(brushes[b], face) };
            }
            return found;
        }

        // points this near each other on screen are as near the mouse
        const float kSameScreenDistance = 0.5f;

        static bool CouldBeNearer(Vector2 gui, Vector2 mouse, float bestScreen)
        {
            return Vector2.Distance(gui, mouse) <= bestScreen + kSameScreenDistance;
        }

        // A point is hidden when, looking along the ray, it is inside a brush or behind one of its faces: one of the
        // brushes is entered before it. A point on a face it is entered through is seen.
        internal static bool IsHidden(Vector3 world, Ray ray, IReadOnlyList<Brush> brushes)
        {
            var depth = Vector3.Dot(world - ray.origin, ray.direction);
            var along = new Ray(world - ray.direction * depth, ray.direction);
            var limit = depth - kOnSurface * Mathf.Max(1, Mathf.Abs(depth));
            for (int b = 0; b < brushes.Count; b++)
            {
                if (TryEnter(along, brushes[b], out var entered, out _) && entered < limit)
                    return true;
            }
            return false;
        }

        // how near, relative to the distance from the camera, a point is to a surface to be on it
        const float kOnSurface = 1e-4f;

        static bool IsNearer(Vector2 gui, Vector3 world, Ray ray, Vector2 mouse, ref float bestScreen, ref float bestDepth)
        {
            var screen = Vector2.Distance(gui, mouse);
            var depth  = Vector3.Dot(world - ray.origin, ray.direction);
            if (depth < 0 || screen > bestScreen + kSameScreenDistance)
                return false;
            if (screen >= bestScreen - kSameScreenDistance && depth >= bestDepth)
                return false;
            bestScreen = Mathf.Min(bestScreen, screen);
            bestDepth  = depth;
            return true;
        }

        // the point on the segment nearest the ray
        static Vector3 ClosestOnSegment(Ray ray, Vector3 start, Vector3 end)
        {
            var along   = end - start;
            var dir     = ray.direction;
            var offset  = start - ray.origin;
            var a = Vector3.Dot(along, along);
            var b = Vector3.Dot(along, dir);
            var c = Vector3.Dot(dir, dir);
            var d = Vector3.Dot(along, offset);
            var e = Vector3.Dot(dir, offset);
            var denominator = a * c - b * b;
            var s = denominator > 1e-12f ? (b * e - c * d) / denominator : 0;
            return start + along * Mathf.Clamp01(s);
        }

        // where the ray enters the convex brush, and through which face
        static bool TryEnter(Ray ray, Brush brush, out float distance, out int face)
        {
            distance = 0;
            face     = -1;
            var mesh = brush.mesh;
            if (mesh?.planes == null)
                return false;
            var enter = 0f;
            var exit  = float.PositiveInfinity;
            for (int p = 0; p < mesh.planes.Length; p++)
            {
                var plane       = WorldPlane(brush, p);
                var facing      = Vector3.Dot(plane.normal, ray.direction);
                var outside     = plane.GetDistanceToPoint(ray.origin);
                if (Mathf.Abs(facing) < 1e-8f)
                {
                    if (outside > 0)
                        return false;
                    continue;
                }
                var t = -outside / facing;
                if (facing < 0)
                {
                    if (t > enter || face < 0 && t >= enter) { enter = t; face = p; }
                } else
                    exit = Mathf.Min(exit, t);
            }
            if (face < 0 || enter > exit)
                return false;
            distance = enter;
            return true;
        }

        static Plane WorldPlane(Brush brush, int polygon)
        {
            var local = brush.mesh.planes[polygon];
            return brush.toWorld.TransformPlane(new Plane(new Vector3(local.x, local.y, local.z), local.w));
        }

        static Vector3 FaceNormal(Brush brush, int polygon) => WorldPlane(brush, polygon).normal;

        static Vector3 MostFacing(Vector3 a, Vector3 b, Vector3 towardsCamera)
        {
            return Vector3.Dot(a, towardsCamera) >= Vector3.Dot(b, towardsCamera) ? a : b;
        }

        // of the faces around the vertex, the one facing the camera most
        static Vector3 VertexNormal(Brush brush, int vertex, Vector3 towardsCamera)
        {
            var mesh = brush.mesh;
            var best = Vector3.zero;
            var bestFacing = float.NegativeInfinity;
            for (int e = 0; e < mesh.halfEdges.Length; e++)
            {
                if (mesh.halfEdges[e].vertexIndex != vertex)
                    continue;
                var normal = FaceNormal(brush, mesh.halfEdgePolygonIndices[e]);
                var facing = Vector3.Dot(normal, towardsCamera);
                if (facing <= bestFacing)
                    continue;
                bestFacing = facing;
                best       = normal;
            }
            return best;
        }
    }
}
