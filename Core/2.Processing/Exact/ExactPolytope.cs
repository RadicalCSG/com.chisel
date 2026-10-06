using System;
using System.Collections.Generic;

namespace Chisel.Core
{
    static class ExactPolytope
    {
        public enum Result
        {
            Built,
            Empty,              // the planes enclose no volume, or less than a float can show
            InvalidPlane,       // a plane of the brush has no direction, or lies beyond the exact world (+-2^24)
            OutsideWorld,       // the planes do not close the brush within the exact world
            DegenerateVertex,   // three planes that had to meet in a point did not
            Inconsistent,       // an exact edge without exactly one twin: a bug, the exact faces always close up
        }

        public static Result Build(double[] planes, int planeCount, int inputCount,
                                   List<float> vertices, List<int> edgeVertex, List<int> edgeTwin,
                                   List<int> faceFirstEdge, List<int> faceEdgeCount, List<int> facePlane)
        {
            vertices.Clear(); edgeVertex.Clear(); edgeTwin.Clear();
            faceFirstEdge.Clear(); faceEdgeCount.Clear(); facePlane.Clear();

            var builder = new ExactFaceBuilder
            {
                planes      = new ExactList<ExactPlane>(planeCount),
                polygonPool = new ExactList<ExactPolygonEdge>(64),
                signs       = new ExactList<int>(16),
            };
            try
            {
                var faces = new List<Face>();
                var corners = new Corners();
                var result = ExactFaces(ref builder, planes, planeCount, inputCount, faces, corners);
                if (result == Result.Built)
                    result = Round(faces, corners, vertices, edgeVertex, edgeTwin, faceFirstEdge, faceEdgeCount, facePlane);
                if (result != Result.Built)
                {
                    vertices.Clear(); edgeVertex.Clear(); edgeTwin.Clear();
                    faceFirstEdge.Clear(); faceEdgeCount.Clear(); facePlane.Clear();
                }
                return result;
            }
            finally
            {
                builder.Dispose();
            }
        }

        struct Face
        {
            public int plane;
            public int first, count;    // into Corners.loops: the face's corners, counter-clockwise seen from outside
        }

        sealed class Corners
        {
            public readonly List<ExactVertex> points = new List<ExactVertex>();
            public readonly List<int> vertex = new List<int>();        // the float vertex of each corner
            public readonly List<float> floats = new List<float>();     // x, y, z of each float vertex
            public readonly List<int> loops = new List<int>();          // the corners of every face, one face after another
            readonly Dictionary<(float, float, float), List<int>> byFloat = new Dictionary<(float, float, float), List<int>>();
            readonly Dictionary<(float, float, float), int> vertexOf = new Dictionary<(float, float, float), int>();

            public int Add(in ExactVertex point)
            {
                // + 0f turns -0 into 0, so both zeros are one vertex
                float x = ExactPredicates.RoundToFloat(point.X, point.W) + 0f;
                float y = ExactPredicates.RoundToFloat(point.Y, point.W) + 0f;
                float z = ExactPredicates.RoundToFloat(point.Z, point.W) + 0f;
                var key = (x, y, z);
                if (!byFloat.TryGetValue(key, out var candidates))
                {
                    candidates = new List<int>(1);
                    byFloat.Add(key, candidates);
                }
                foreach (var candidate in candidates)
                {
                    if (ExactPredicates.SamePoint(points[candidate], point))
                        return candidate;
                }
                if (!vertexOf.TryGetValue(key, out var index))
                {
                    index = floats.Count / 3;
                    floats.Add(x); floats.Add(y); floats.Add(z);
                    vertexOf.Add(key, index);
                }
                int corner = points.Count;
                points.Add(point);
                vertex.Add(index);
                candidates.Add(corner);
                return corner;
            }
        }

        static Result ExactFaces(ref ExactFaceBuilder builder, double[] planes, int planeCount, int inputCount,
                                 List<Face> faces, Corners corners)
        {
            var alive = new bool[planeCount];
            for (int p = 0; p < planeCount; p++)
            {
                var plane = ExactPlane.AsGiven(planes[p * 4 + 0], planes[p * 4 + 1], planes[p * 4 + 2], planes[p * 4 + 3]);
                if (!plane.IsValid && p < inputCount)
                    return Result.InvalidPlane;
                builder.planes.Add(plane);
                alive[p] = plane.IsValid;
            }

            for (int s = 0; s < planeCount; s++)
            {
                for (int t = s + 1; t < planeCount && alive[s]; t++)
                {
                    if (!alive[t])
                        continue;
                    int same = ExactPredicates.SamePlane(builder.planes[s], builder.planes[t]);
                    if (same < 0)
                        return Result.Empty;
                    if (same == 0)
                        continue;
                    if (t < inputCount)
                        alive[s] = false;   // both are the brush's own: the later one describes the face
                    else
                        alive[t] = false;   // t only bounds the brush
                }
            }

            // The quad every face starts from has the ids planeCount..planeCount+3; an edge of it that survives every clip
            // means the planes leave the brush open.
            int worldId = planeCount;
            var failure = ExactFaceFailure.None;
            for (int f = 0; f < planeCount; f++)
            {
                if (!alive[f])
                    continue;
                builder.polygonPool.Clear();
                var face = builder.planes[f];
                var polygon = builder.BoundingQuad(face, worldId, ref failure);
                for (int p = 0; p < planeCount && !polygon.IsEmpty; p++)
                {
                    if (p != f && alive[p])
                        polygon = builder.Clip(polygon, face, builder.planes[p], p, ref failure);
                }
                if (failure != ExactFaceFailure.None)
                    return Result.DegenerateVertex;
                if (polygon.IsEmpty)
                    continue;

                int first = corners.loops.Count;
                for (int e = 0; e < polygon.count; e++)
                {
                    var edge = builder.polygonPool[polygon.offset + e];
                    if (edge.planeId >= worldId)
                        return Result.OutsideWorld;
                    corners.loops.Add(corners.Add(edge.start));
                }
                faces.Add(new Face { plane = f, first = first, count = corners.loops.Count - first });
            }
            return faces.Count < 4 ? Result.Empty : Result.Built;
        }

        // The exact faces rounded to float. A half-edge is the edge from the corner before it in its face to its own
        // corner; its twin is the exact edge the other way, in the neighbouring face.
        static Result Round(List<Face> faces, Corners corners,
                            List<float> vertices, List<int> edgeVertex, List<int> edgeTwin,
                            List<int> faceFirstEdge, List<int> faceEdgeCount, List<int> facePlane)
        {
            var loops = corners.loops;
            int edgeCount = loops.Count;
            var twin = new int[edgeCount];
            var byCorners = new Dictionary<(int, int), int>(edgeCount);
            foreach (var face in faces)
            {
                for (int e = face.first; e < face.first + face.count; e++)
                {
                    int previous = e == face.first ? face.first + face.count - 1 : e - 1;
                    var key = (loops[previous], loops[e]);
                    if (loops[previous] == loops[e] || byCorners.ContainsKey(key))
                        return Result.Inconsistent;
                    byCorners.Add(key, e);
                }
            }
            foreach (var face in faces)
            {
                for (int e = face.first; e < face.first + face.count; e++)
                {
                    int previous = e == face.first ? face.first + face.count - 1 : e - 1;
                    if (!byCorners.TryGetValue((loops[e], loops[previous]), out twin[e]))
                        return Result.Inconsistent;
                }
            }

            // Each face as a list of its live half-edges, in order; the float vertex a half-edge runs to is its corner's.
            var vertexOfEdge = new int[edgeCount];
            for (int e = 0; e < edgeCount; e++)
                vertexOfEdge[e] = corners.vertex[loops[e]];
            var faceEdges = new List<int>[faces.Count];
            for (int f = 0; f < faces.Count; f++)
            {
                var list = new List<int>(faces[f].count);
                for (int e = faces[f].first; e < faces[f].first + faces[f].count; e++)
                    list.Add(e);
                faceEdges[f] = list;
            }

            // An edge between corners that round to one vertex has no length: it goes from its face, and its twin, which
            // joins the same two corners, goes from the neighbouring face. Nothing is left without a twin.
            foreach (var list in faceEdges)
            {
                for (int i = list.Count - 1; i >= 0 && list.Count > 0; i--)
                {
                    int e = list[i];
                    int previous = list[(i + list.Count - 1) % list.Count];
                    if (vertexOfEdge[previous] == vertexOfEdge[e])
                        list.RemoveAt(i);
                }
            }

            bool removed = true;
            while (removed)
            {
                removed = false;
                foreach (var list in faceEdges)
                {
                    for (int i = 0; i < list.Count && list.Count >= 2; i++)
                    {
                        int x = list[i], y = list[(i + 1) % list.Count];
                        int before = list[(i + list.Count - 1) % list.Count];
                        // x runs from `before`'s vertex to its own, y back to `before`'s vertex
                        if (vertexOfEdge[y] != vertexOfEdge[before])
                            continue;
                        int tx = twin[x], ty = twin[y];
                        if (tx != y)
                        {
                            twin[tx] = ty;
                            twin[ty] = tx;
                        }
                        int j = (i + 1) % list.Count;
                        list.RemoveAt(Math.Max(i, j));
                        list.RemoveAt(Math.Min(i, j));
                        removed = true;
                        break;
                    }
                }
            }

            // Compact: the faces that still have an area, their half-edges renumbered, the vertices they use.
            var newIndex = new int[edgeCount];
            for (int e = 0; e < edgeCount; e++)
                newIndex[e] = -1;
            var vertexRemap = new Dictionary<int, int>();
            for (int f = 0; f < faces.Count; f++)
            {
                var list = faceEdges[f];
                if (list.Count < 3)
                {
                    if (list.Count != 0)
                        return Result.Inconsistent;     // two half-edges are an antenna, one cannot close
                    continue;
                }
                faceFirstEdge.Add(edgeVertex.Count);
                faceEdgeCount.Add(list.Count);
                facePlane.Add(faces[f].plane);
                foreach (var e in list)
                {
                    int v = vertexOfEdge[e];
                    if (!vertexRemap.TryGetValue(v, out var nv))
                    {
                        nv = vertices.Count / 3;
                        vertices.Add(corners.floats[v * 3]); vertices.Add(corners.floats[v * 3 + 1]); vertices.Add(corners.floats[v * 3 + 2]);
                        vertexRemap.Add(v, nv);
                    }
                    newIndex[e] = edgeVertex.Count;
                    edgeVertex.Add(nv);
                    edgeTwin.Add(e);    // the old index for now
                }
            }
            for (int e = 0; e < edgeTwin.Count; e++)
            {
                int t = newIndex[twin[edgeTwin[e]]];
                if (t < 0)
                    return Result.Inconsistent;
                edgeTwin[e] = t;
            }
            return faceFirstEdge.Count < 4 ? Result.Empty : Result.Built;
        }
    }
}
