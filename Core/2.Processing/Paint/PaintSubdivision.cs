using Unity.Collections;
using Unity.Mathematics;

namespace Chisel.Core
{
    // A painted surface is subdivided at its paint's grid points, so the paint has vertices to land on: each grid point
    // inside the surface becomes a vertex, inserted as in an incremental Delaunay triangulation (it splits the triangle it
    // is in, or the two on the edge it is on, and the edges round it are flipped until the triangles round it are
    // Delaunay), so no sliver is left for the paint to streak across. The surface's outline is never cut, so the surfaces
    // it touches don't get T-junctions.
    internal static class PaintSubdivision
    {
        // Most points one surface gets; a grid of tiny cells on a big surface stops here
        public const int kMaxPoints = 4096;
        // How close to an edge or a corner a grid point counts as on it, in parts of the grid's cell
        const float kSnap = 0.05f;

        public static void Subdivide(NativeList<int> indices, ref UniqueVertexMapper mapper, float3 surfaceNormal,
                                     in ChiselPaintLookup paint, in ChiselPaintRange range)
        {
            if (range.count <= 0 || indices.Length < 3)
                return;
            var snap = float.PositiveInfinity;
            using var points = new NativeList<float3>(64, Allocator.Temp);
            using var seen   = new NativeParallelHashSet<int3>(64, Allocator.Temp);
            for (int l = range.start; l < range.start + range.count; l++)
            {
                var layer  = paint.layers[l];
                var normal = math.normalizesafe(math.cross(layer.cellU, layer.cellV));
                // only a surface in the grid's plane, or parallel to it
                if (math.abs(math.dot(normal, surfaceNormal)) < 0.999f)
                    continue;
                snap = math.min(snap, kSnap * math.min(math.length(layer.cellU), math.length(layer.cellV)));
                for (int j = 0; j < layer.resolution.y && points.Length < kMaxPoints; j++)
                {
                    for (int i = 0; i < layer.resolution.x && points.Length < kMaxPoints; i++)
                    {
                        var point = layer.origin + i * layer.cellU + j * layer.cellV;
                        if (seen.Add((int3)math.round(point * 1e4f)))
                            points.Add(point);
                    }
                }
            }
            if (points.Length == 0)
                return;

            var triangulation = new Triangulation(indices, ref mapper, surfaceNormal, snap);
            for (int p = 0; p < points.Length; p++)
                triangulation.Insert(ref mapper, points[p]);
            triangulation.Dispose();
        }

        struct Triangulation
        {
            NativeList<int>      indices;
            NativeList<double2>  flat;      // every vertex in the surface's plane
            NativeList<int2>     pending;   // edges (a, b) of triangles (a, b, v) round a new vertex v, to be made Delaunay
            double3              axisU, axisV;
            double               snap;

            public Triangulation(NativeList<int> indices, ref UniqueVertexMapper mapper, float3 normal, double snap)
            {
                this.indices = indices;
                this.snap    = snap;
                axisU   = math.normalizesafe(math.cross((double3)normal, math.abs(normal.x) < 0.9f ? new double3(1, 0, 0) : new double3(0, 1, 0)));
                axisV   = math.normalizesafe(math.cross((double3)normal, axisU));
                flat    = new NativeList<double2>(mapper.surfaceColliderVertices.Length, Allocator.Temp);
                pending = new NativeList<int2>(16, Allocator.Temp);
                for (int i = 0; i < mapper.surfaceColliderVertices.Length; i++)
                    flat.Add(Flat(mapper.surfaceColliderVertices[i]));
            }

            public void Dispose()
            {
                flat.Dispose();
                pending.Dispose();
            }

            readonly double2 Flat(float3 position) => new double2(math.dot((double3)position, axisU), math.dot((double3)position, axisV));

            public void Insert(ref UniqueVertexMapper mapper, float3 position)
            {
                var point = Flat(position);
                for (int t = 0; t + 2 < indices.Length; t += 3)
                {
                    int i0 = indices[t], i1 = indices[t + 1], i2 = indices[t + 2];
                    var a = flat[i0]; var b = flat[i1]; var c = flat[i2];
                    var twiceArea = Cross(a, b, c);
                    if (!(math.abs(twiceArea) > 1e-18))
                        continue;
                    // the point's weights, and how far it is from each edge (the one opposite each corner)
                    var w = new double3(Cross(point, b, c), Cross(a, point, c), Cross(a, b, point)) / twiceArea;
                    var distance = w * math.abs(twiceArea) / new double3(math.distance(b, c), math.distance(c, a), math.distance(a, b));
                    if (math.cmin(distance) < -snap)
                        continue;
                    if (math.distance(point, a) < snap || math.distance(point, b) < snap || math.distance(point, c) < snap)
                        return;     // there is a vertex already

                    if (math.cmin(distance) >= snap)
                    {
                        // inside: three triangles round it
                        var v = AddVertex(ref mapper, i0, i1, i2, w);
                        indices[t + 2] = v;
                        indices.Add(i1); indices.Add(i2); indices.Add(v);
                        indices.Add(i2); indices.Add(i0); indices.Add(v);
                        pending.Add(new int2(i0, i1));
                        pending.Add(new int2(i1, i2));
                        pending.Add(new int2(i2, i0));
                        Legalize(v);
                        return;
                    }

                    // on an edge (the nearest): it splits both of its triangles, or none on the surface's outline
                    var nearest  = distance.x <= distance.y && distance.x <= distance.z ? 0 : distance.y <= distance.z ? 1 : 2;
                    int opposite = nearest == 0 ? i0 : nearest == 1 ? i1 : i2;
                    int e0       = nearest == 0 ? i1 : nearest == 1 ? i2 : i0;
                    int e1       = nearest == 0 ? i2 : nearest == 1 ? i0 : i1;
                    var other = FindEdge(e1, e0, out var otherOpposite);
                    if (other < 0)
                        return;
                    // exactly on the edge
                    w = math.max(w, 0);
                    w[nearest] = 0;
                    w /= math.csum(w);
                    var vertex = AddVertex(ref mapper, i0, i1, i2, w);
                    indices[t] = opposite; indices[t + 1] = e0; indices[t + 2] = vertex;
                    indices.Add(opposite); indices.Add(vertex); indices.Add(e1);
                    indices[other] = otherOpposite; indices[other + 1] = e1; indices[other + 2] = vertex;
                    indices.Add(otherOpposite); indices.Add(vertex); indices.Add(e0);
                    pending.Add(new int2(opposite, e0));
                    pending.Add(new int2(e1, opposite));
                    pending.Add(new int2(otherOpposite, e1));
                    pending.Add(new int2(e0, otherOpposite));
                    Legalize(vertex);
                    return;
                }
            }

            // Each edge (a, b) of a triangle (a, b, v) is flipped when the far corner d of the triangle across it is in
            // the circle through a, b and v; the two edges that then face v are looked at in turn
            void Legalize(int v)
            {
                while (pending.Length > 0)
                {
                    var edge = pending[pending.Length - 1];
                    pending.RemoveAt(pending.Length - 1);
                    int a = edge.x, b = edge.y;
                    var t = FindEdge(a, b, out var third);
                    if (t < 0 || third != v)
                        continue;
                    var u = FindEdge(b, a, out var d);
                    if (u < 0)
                        continue;   // the outline
                    if (!InCircle(flat[a], flat[b], flat[v], flat[d]) || !Convex(flat[a], flat[d], flat[b], flat[v]))
                        continue;
                    // (a, b, v) and (b, a, d) become (v, a, d) and (d, b, v)
                    indices[t] = v; indices[t + 1] = a; indices[t + 2] = d;
                    indices[u] = d; indices[u + 1] = b; indices[u + 2] = v;
                    pending.Add(new int2(a, d));
                    pending.Add(new int2(d, b));
                }
            }

            // The triangle with the edge from a to b (in its winding), and its third corner; -1 when there is none
            readonly int FindEdge(int a, int b, out int opposite)
            {
                for (int t = 0; t + 2 < indices.Length; t += 3)
                {
                    for (int k = 0; k < 3; k++)
                    {
                        if (indices[t + k] == a && indices[t + (k + 1) % 3] == b)
                        {
                            opposite = indices[t + (k + 2) % 3];
                            return t;
                        }
                    }
                }
                opposite = -1;
                return -1;
            }

            // A vertex where the weights put it in the triangle, every stream interpolated
            int AddVertex(ref UniqueVertexMapper mapper, int i0, int i1, int i2, double3 w)
            {
                var f  = (float3)w;
                var r0 = mapper.surfaceRenderVertices[i0];
                var r1 = mapper.surfaceRenderVertices[i1];
                var r2 = mapper.surfaceRenderVertices[i2];
                var render = new RenderVertex
                {
                    position = r0.position * f.x + r1.position * f.y + r2.position * f.z,
                    normal   = math.normalizesafe(r0.normal * f.x + r1.normal * f.y + r2.normal * f.z),
                    tangent  = r0.tangent * f.x + r1.tangent * f.y + r2.tangent * f.z,
                    uv0      = r0.uv0 * f.x + r1.uv0 * f.y + r2.uv0 * f.z,
                    uv1      = r0.uv1 * f.x + r1.uv1 * f.y + r2.uv1 * f.z
                };
                var select = mapper.surfaceSelectVertices[i0];
                select.position = render.position;
                var index = mapper.surfaceRenderVertices.Length;
                mapper.surfaceRenderVertices.Add(render);
                mapper.surfaceColliderVertices.Add(render.position);
                mapper.surfaceSelectVertices.Add(select);
                flat.Add(flat[i0] * w.x + flat[i1] * w.y + flat[i2] * w.z);
                return index;
            }
        }

        static double Cross(double2 o, double2 p, double2 q) => (p.x - o.x) * (q.y - o.y) - (p.y - o.y) * (q.x - o.x);

        // Whether d is inside the circle through a, b and c, whichever way round they go (cocircular points are not)
        static bool InCircle(double2 a, double2 b, double2 c, double2 d)
        {
            var ad = a - d; var bd = b - d; var cd = c - d;
            var det = math.lengthsq(ad) * (bd.x * cd.y - cd.x * bd.y) -
                      math.lengthsq(bd) * (ad.x * cd.y - cd.x * ad.y) +
                      math.lengthsq(cd) * (ad.x * bd.y - bd.x * ad.y);
            var scale = math.lengthsq(ad) * math.lengthsq(bd) + 1e-30;
            return Cross(a, b, c) > 0 ? det > 1e-9 * scale : det < -1e-9 * scale;
        }

        // Whether the quadrilateral a, d, b, c (in order) is convex, so its diagonal c-d lies inside it
        static bool Convex(double2 a, double2 d, double2 b, double2 c)
        {
            var s0 = Cross(a, d, b); var s1 = Cross(d, b, c); var s2 = Cross(b, c, a); var s3 = Cross(c, a, d);
            return (s0 > 0 && s1 > 0 && s2 > 0 && s3 > 0) || (s0 < 0 && s1 < 0 && s2 < 0 && s3 < 0);
        }
    }
}
