using System;
using System.Collections.Generic;
using Chisel.Core;

// Offline checks of the exact face builder and triangulator on generated scenes of convex brushes combined in order
// (a flat CSG list: the first brush is additive, the rest additive or subtractive).
//
// The ORACLE is independent of the builder: for sample points on a face (exact plane triples), it evaluates every
// brush's membership directly from its planes and runs the routing - the definition of what should be drawn. The builder's
// triangles must cover exactly the samples the oracle says are drawn, with the right facing, and no sample twice.
//
// Then the WHOLE OUTPUT is checked for watertightness: every triangle edge must have a partner running the other way,
// and no vertex may lie strictly inside another triangle's edge (a T-junction). Positions are the correctly rounded
// floats, so a partner is found by exact float equality - which is the property the design promises.
//
// And every face's triangles have to be locally Delaunay where ExactTriangulator's flips promise it (ExactDelaunayCheck,
// Core/Tests/Contents, the same file the editor's tests use).
static class ExactFaceTests
{
    const byte Inside = 0, Aligned = 1, SelfAligned = 2, SelfReverseAligned = 3, ReverseAligned = 4, Outside = 5;
    const int OpAdditive = 0, OpSubtractive = 1, OpIntersecting = 2;

    // Chisel's operation tables (CategoryRoutingRow.kOperationTables), left = row, right = column.
    static readonly byte[] kTables =
    {
        // Additive
        Inside, Inside, Inside, Inside, Inside, Inside,
        Inside, Aligned, SelfAligned, Inside, Inside, Aligned,
        Inside, Aligned, SelfAligned, Inside, Inside, SelfAligned,
        Inside, Inside, Inside, SelfReverseAligned, ReverseAligned, SelfReverseAligned,
        Inside, Inside, Inside, SelfReverseAligned, ReverseAligned, ReverseAligned,
        Inside, Aligned, SelfAligned, SelfReverseAligned, ReverseAligned, Outside,
        // Subtractive
        Outside, ReverseAligned, SelfReverseAligned, SelfAligned, Aligned, Inside,
        Outside, Outside, Outside, Aligned, Aligned, Aligned,
        Outside, Outside, Outside, Aligned, Aligned, SelfAligned,
        Outside, ReverseAligned, SelfReverseAligned, Outside, Outside, SelfReverseAligned,
        Outside, ReverseAligned, SelfReverseAligned, Outside, Outside, ReverseAligned,
        Outside, Outside, Outside, Outside, Outside, Outside,
        // Intersecting
        Inside, Aligned, SelfAligned, SelfReverseAligned, ReverseAligned, Outside,
        Aligned, Aligned, SelfAligned, Outside, Outside, Outside,
        SelfAligned, Aligned, SelfAligned, Outside, Outside, Outside,
        SelfReverseAligned, Outside, Outside, SelfReverseAligned, ReverseAligned, Outside,
        ReverseAligned, Outside, Outside, SelfReverseAligned, ReverseAligned, Outside,
        Outside, Outside, Outside, Outside, Outside, Outside,
    };

    // min/max per axis: where a generated brush is, for placing samples on its faces
    struct double3x2
    {
        public double minX, minY, minZ, maxX, maxY, maxZ;
        public double Min(int axis) => axis == 0 ? minX : (axis == 1 ? minY : minZ);
        public double Max(int axis) => axis == 0 ? maxX : (axis == 1 ? maxY : maxZ);
    }

    class Brush
    {
        public List<ExactPlane> planes = new List<ExactPlane>();
        public int op;
        public double3x2 bounds;
    }

    static int s_Unresolvable, s_Failures, s_Checks, s_Samples, s_BoundarySkips, s_Faces, s_Triangles, s_EdgeChecks, s_TouchingPairs, s_SeparatePairs, s_Drawn, s_NotDrawn;
    static int s_InnerEdges, s_NotDelaunay, s_FloatUncertain;
    static string s_Scene = "";
    static bool s_Planted;  // running a check on a planted error: its failures are expected, and not printed
    static void Fail(string what) { s_Failures++; if (!s_Planted && s_Failures <= 30) Console.WriteLine("FAIL: " + what); }
    static void Check(bool ok, string what) { s_Checks++; if (!ok) Fail(what); }

    // ---- scene generation ---------------------------------------------------------------------------------------

    static Brush Box(double x0, double y0, double z0, double x1, double y1, double z1, int op)
    {
        var b = new Brush { op = op };
        b.planes.Add(ExactPlane.Quantize(1, 0, 0, -x1));
        b.planes.Add(ExactPlane.Quantize(-1, 0, 0, x0));
        b.planes.Add(ExactPlane.Quantize(0, 1, 0, -y1));
        b.planes.Add(ExactPlane.Quantize(0, -1, 0, y0));
        b.planes.Add(ExactPlane.Quantize(0, 0, 1, -z1));
        b.planes.Add(ExactPlane.Quantize(0, 0, -1, z0));
        b.bounds = new double3x2 { minX = x0, minY = y0, minZ = z0, maxX = x1, maxY = y1, maxZ = z1 };
        return b;
    }

    // A convex polytope: planes tangent to a sphere-ish shape around a center, random normals. Bounds from the exact
    // vertices of its planes.
    static Brush Random(Random rng, double cx, double cy, double cz, double radius, int op, int planeCount)
    {
        var b = new Brush { op = op };
        for (int i = 0; i < planeCount; i++)
        {
            double nx, ny, nz, len;
            do { nx = rng.NextDouble() * 2 - 1; ny = rng.NextDouble() * 2 - 1; nz = rng.NextDouble() * 2 - 1; len = Math.Sqrt(nx * nx + ny * ny + nz * nz); }
            while (len < 0.2 || len > 1);
            nx /= len; ny /= len; nz /= len;
            double r = radius * (0.7 + 0.3 * rng.NextDouble());
            double d = -(nx * cx + ny * cy + nz * cz) - r;
            b.planes.Add(ExactPlane.Quantize(nx, ny, nz, d));
        }
        // bounding axis planes so it is always bounded
        double R = radius * 1.8;
        foreach (var (nx, ny, nz) in new[] { (1.0, 0.0, 0.0), (-1.0, 0.0, 0.0), (0.0, 1.0, 0.0), (0.0, -1.0, 0.0), (0.0, 0.0, 1.0), (0.0, 0.0, -1.0) })
            b.planes.Add(ExactPlane.Quantize(nx, ny, nz, -(nx * cx + ny * cy + nz * cz) - R));
        b.bounds = new double3x2 { minX = cx - R, minY = cy - R, minZ = cz - R, maxX = cx + R, maxY = cy + R, maxZ = cz + R };
        return b;
    }

    static List<Brush> GridScene(Random rng)
    {
        // boxes on a coarse grid: lots of exactly shared planes, corners, edges - the configurations that break things
        var brushes = new List<Brush>();
        int count = rng.Next(2, 7);
        for (int i = 0; i < count; i++)
        {
            double x0 = rng.Next(-3, 3), y0 = rng.Next(-3, 3), z0 = rng.Next(-3, 3);
            double x1 = x0 + rng.Next(1, 4), y1 = y0 + rng.Next(1, 4), z1 = z0 + rng.Next(1, 4);
            // halves sometimes, still exact in binary
            if (rng.Next(3) == 0) { x0 += 0.5; y1 -= 0.5; }
            int op = i == 0 ? OpAdditive : (rng.Next(3) == 0 ? OpSubtractive : OpAdditive);
            brushes.Add(Box(x0, y0, z0, x1, y1, z1, op));
        }
        return brushes;
    }

    static List<Brush> RandomScene(Random rng)
    {
        var brushes = new List<Brush>();
        int count = rng.Next(2, 5);
        for (int i = 0; i < count; i++)
        {
            int op = i == 0 ? OpAdditive : (rng.Next(3) == 0 ? OpSubtractive : OpAdditive);
            if (rng.Next(2) == 0)
                brushes.Add(Random(rng, rng.NextDouble() * 2, rng.NextDouble() * 2, rng.NextDouble() * 2, 1 + rng.NextDouble(), op, rng.Next(4, 9)));
            else
            {
                double x0 = rng.NextDouble() * 2 - 1, y0 = rng.NextDouble() * 2 - 1, z0 = rng.NextDouble() * 2 - 1;
                brushes.Add(Box(x0, y0, z0, x0 + 0.5 + rng.NextDouble() * 2, y0 + 0.5 + rng.NextDouble() * 2, z0 + 0.5 + rng.NextDouble() * 2, op));
            }
        }
        return brushes;
    }

    // Boxes on a grid with some faces nudged by 2^-k: slivers and near-coincident planes far below any tolerance.
    static List<Brush> SliverScene(Random rng)
    {
        var brushes = new List<Brush>();
        int count = rng.Next(2, 6);
        for (int i = 0; i < count; i++)
        {
            double[] c = new double[6];
            for (int a = 0; a < 3; a++) { c[a] = rng.Next(-2, 2); c[a + 3] = c[a] + rng.Next(1, 3); }
            for (int a = 0; a < 6; a++)
                if (rng.Next(3) == 0) c[a] += (rng.Next(2) == 0 ? 1 : -1) * Math.Pow(2, -rng.Next(10, 31));
            int op = i == 0 ? OpAdditive : (rng.Next(3) == 0 ? OpSubtractive : (rng.Next(6) == 0 ? OpIntersecting : OpAdditive));
            brushes.Add(Box(c[0], c[1], c[2], c[3], c[4], c[5], op));
        }
        return brushes;
    }

    // Boxes under one random rotation, laid out on a grid in the rotated frame: faces meant to coincide are computed
    // from different points, so they agree only as far as the arithmetic lets them.
    static List<Brush> RotatedScene(Random rng, double offset)
    {
        double ax = rng.NextDouble() * 2 - 1, ay = rng.NextDouble() * 2 - 1, az = rng.NextDouble() * 2 - 1;
        double al = Math.Sqrt(ax * ax + ay * ay + az * az); ax /= al; ay /= al; az /= al;
        double angle = rng.NextDouble() * Math.PI;
        double cs = Math.Cos(angle), sn = Math.Sin(angle), t = 1 - cs;
        double[,] R = {
            { t * ax * ax + cs,      t * ax * ay - sn * az, t * ax * az + sn * ay },
            { t * ax * ay + sn * az, t * ay * ay + cs,      t * ay * az - sn * ax },
            { t * ax * az - sn * ay, t * ay * az + sn * ax, t * az * az + cs } };
        var brushes = new List<Brush>();
        int count = rng.Next(2, 6);
        for (int i = 0; i < count; i++)
        {
            double[] lo = { rng.Next(-2, 2), rng.Next(-2, 2), rng.Next(-2, 2) };
            double[] hi = { lo[0] + rng.Next(1, 3), lo[1] + rng.Next(1, 3), lo[2] + rng.Next(1, 3) };
            int op = i == 0 ? OpAdditive : (rng.Next(3) == 0 ? OpSubtractive : OpAdditive);
            var b = new Brush { op = op };
            for (int axis = 0; axis < 3; axis++)
            {
                for (int side = 0; side < 2; side++)
                {
                    double sign = side == 0 ? 1 : -1;
                    // local plane: sign * x_axis - sign * bound = 0, rotated, then translated by offset
                    double nx = sign * R[0, axis], ny = sign * R[1, axis], nz = sign * R[2, axis];
                    double bound = side == 0 ? hi[axis] : lo[axis];
                    double d = -sign * bound - (nx * offset + ny * offset + nz * offset);
                    b.planes.Add(ExactPlane.Quantize(nx, ny, nz, d));
                }
            }
            double r = Math.Sqrt(3) * 4 + 1;
            b.bounds = new double3x2 { minX = offset - r, minY = offset - r, minZ = offset - r, maxX = offset + r, maxY = offset + r, maxZ = offset + r };
            brushes.Add(b);
        }
        return brushes;
    }

    // A prism with n sides around the y axis: many planes, adjacent ones nearly parallel for large n.
    static Brush Cylinder(double cx, double cy, double cz, double radius, double height, int sides, double phase, int op)
    {
        var b = new Brush { op = op };
        for (int i = 0; i < sides; i++)
        {
            double a = phase + 2 * Math.PI * i / sides;
            double nx = Math.Cos(a), nz = Math.Sin(a);
            b.planes.Add(ExactPlane.Quantize(nx, 0, nz, -(nx * cx + nz * cz) - radius));
        }
        b.planes.Add(ExactPlane.Quantize(0, 1, 0, -(cy + height)));
        b.planes.Add(ExactPlane.Quantize(0, -1, 0, cy));
        double r = radius / Math.Cos(Math.PI / sides) + 0.01;
        b.bounds = new double3x2 { minX = cx - r, minY = cy, minZ = cz - r, maxX = cx + r, maxY = cy + height, maxZ = cz + r };
        return b;
    }

    static List<Brush> CylinderScene(Random rng)
    {
        var brushes = new List<Brush>();
        int count = rng.Next(2, 5);
        for (int i = 0; i < count; i++)
        {
            int op = i == 0 ? OpAdditive : (rng.Next(3) == 0 ? OpSubtractive : OpAdditive);
            if (rng.Next(2) == 0)
                brushes.Add(Cylinder(rng.Next(-1, 2) * 0.5, rng.Next(-1, 2) * 0.5, rng.Next(-1, 2) * 0.5, 0.5 + rng.Next(0, 3) * 0.25, 1 + rng.Next(0, 3),
                                     rng.Next(3, 48), rng.Next(2) == 0 ? 0 : rng.NextDouble(), op));
            else
                brushes.Add(Box(rng.Next(-2, 1), rng.Next(-2, 1), rng.Next(-2, 1), rng.Next(1, 3), rng.Next(1, 3), rng.Next(1, 3), op));
        }
        return brushes;
    }

    // One floor and many things standing on it or cut into it: one face shared by many brushes.
    // A long strip with boxes against both of its long sides, their tops level with its top, and sometimes slats crossing
    // it: the strip's top gets a vertex wherever a box's side meets its edge, a comb that ear clipping cut into fans of
    // slivers metres long (the light-blocker grate of bm_c2a3c, 2026-09-27).
    static List<Brush> StripScene(Random rng)
    {
        double length = 4 + rng.Next(0, 13), width = 0.0625 * rng.Next(1, 9), height = 1;
        var brushes = new List<Brush> { Box(0, 0, 0, length, height, width, OpAdditive) };
        for (int side = 0; side < 2; side++)
        {
            double x = 0.0625 * rng.Next(0, 16);
            while (true)
            {
                double w = 0.0625 * rng.Next(2, 12);
                if (x + w > length)
                    break;
                double z0 = side == 0 ? -0.5 : width, z1 = side == 0 ? 0 : width + 0.5;
                if (rng.Next(4) == 0)
                {
                    z0 = -0.5; z1 = width + 0.5;    // a slat across the strip
                }
                brushes.Add(Box(x, 0, z0, x + w, height, z1, OpAdditive));
                x += w + 0.0625 * rng.Next(1, 16);
            }
        }
        return brushes;
    }

    static List<Brush> FloorScene(Random rng)
    {
        var brushes = new List<Brush> { Box(-8, -1, -8, 8, 0, 8, OpAdditive) };
        int count = rng.Next(8, 24);
        for (int i = 0; i < count; i++)
        {
            double x = rng.Next(-8, 7) + (rng.Next(2) == 0 ? 0 : 0.5), z = rng.Next(-8, 7) + (rng.Next(2) == 0 ? 0 : 0.5);
            double w = 0.5 + rng.Next(0, 4) * 0.5, d = 0.5 + rng.Next(0, 4) * 0.5;
            int op = rng.Next(4) == 0 ? OpSubtractive : OpAdditive;
            double y0 = op == OpSubtractive ? -0.5 : 0, y1 = op == OpSubtractive ? 1 : 1 + rng.Next(0, 3);
            if (op == OpAdditive && rng.Next(3) == 0) y0 = -0.25;   // sunk into the floor
            brushes.Add(Box(x, y0, z, x + w, y1, z + d, op));
        }
        return brushes;
    }

    static List<Brush> Translate(List<Brush> brushes, double offset)
    {
        foreach (var b in brushes)
        {
            for (int i = 0; i < b.planes.Count; i++)
            {
                var q = b.planes[i];
                double nx = q.a / ExactPlane.kNormalScale, ny = q.b / ExactPlane.kNormalScale, nz = q.c / ExactPlane.kNormalScale;
                double d = q.w / ExactPlane.kNormalScale - (nx + ny + nz) * offset;
                b.planes[i] = ExactPlane.Quantize(nx, ny, nz, d);
            }
            b.bounds.minX += offset; b.bounds.minY += offset; b.bounds.minZ += offset;
            b.bounds.maxX += offset; b.bounds.maxY += offset; b.bounds.maxZ += offset;
        }
        return brushes;
    }

    // ---- routing for a flat list: the state is the category so far ------------------------------------------------

    static void BuildRouting(List<Brush> brushes, int self, List<ushort> rows, List<int> start, List<int> end, List<int> lookupBrush,
                             List<int> touchingIndex)
    {
        // lookup k is brush k. Row s of lookup k combines state s with brush k's category.
        for (int k = 0; k < brushes.Count; k++)
        {
            start.Add(rows.Count / 6);
            int rowCount = k == 0 ? 1 : 6;
            for (int s = 0; s < rowCount; s++)
            {
                for (int c = 0; c < 6; c++)
                {
                    int effective = k == self ? SelfAligned : c;
                    int value = k == 0 ? effective : kTables[brushes[k].op * 36 + s * 6 + effective];
                    rows.Add((ushort)value);
                }
            }
            end.Add(rows.Count / 6);
            lookupBrush.Add(k == self ? -1 : touchingIndex[k]);
        }
    }

    // ---- the oracle -----------------------------------------------------------------------------------------------

    static int Classify(Brush brush, ExactPlane face, in ExactVertex p)
    {
        // the category of point p (on face) relative to the brush
        int coplanar = 0;
        foreach (var q in brush.planes)
        {
            int same = ExactPredicates.SamePlane(face, q);
            if (same != 0) coplanar = coplanar == 0 ? same : (coplanar == same ? same : 2);
        }
        if (coplanar == 2) return Outside;
        foreach (var q in brush.planes)
        {
            if (ExactPredicates.SamePlane(face, q) != 0) continue;
            int side = ExactPredicates.Side(q, p);
            if (side > 0) return Outside;
            if (side == 0) return -1;   // on the brush's boundary: ambiguous sample
        }
        if (coplanar > 0) return Aligned;
        if (coplanar < 0) return ReverseAligned;
        return Inside;
    }

    static int Route(List<ushort> rows, List<int> start, List<int> end, int[] categories)
    {
        int input = 0;
        for (int k = 0; k < start.Count; k++)
        {
            int row = start[k] + input;
            if (row >= end[k]) throw new Exception("route out of range");
            input = rows[row * 6 + categories[k]];
        }
        return input;
    }

    // ---- running the builder --------------------------------------------------------------------------------------

    struct OutTriangle { public ExactVertex a, b, c; public float[] fa, fb, fc; public int category; public int brush, face; public ExactPlane plane; }

    // Whether two brushes' closed volumes meet: some vertex of the combined plane set satisfies every plane of both.
    static bool Touch(Brush a, Brush b)
    {
        var all = new List<ExactPlane>(a.planes); all.AddRange(b.planes);
        for (int i = 0; i < all.Count; i++)
            for (int j = i + 1; j < all.Count; j++)
                for (int k = j + 1; k < all.Count; k++)
                {
                    var v = ExactVertex.Intersect(all[i], all[j], all[k]);
                    if (!v.IsValid) continue;
                    bool inside = true;
                    foreach (var q in all) if (ExactPredicates.Side(q, v) > 0) { inside = false; break; }
                    if (inside) return true;
                }
        return false;
    }

    static bool[,] s_Touching;

    static List<OutTriangle> BuildBrush(List<Brush> brushes, int self, ref ExactFaceBuilder builder, ref ExactTriangulator triangulator,
                                        out int faceFailures)
    {
        faceFailures = 0;
        var result = new List<OutTriangle>();
        var brush = brushes[self];
        builder.planes.Clear();
        foreach (var p in brush.planes) builder.planes.Add(p);
        var touching = new ExactList<ExactTouchingBrush>(8);
        var touchingIndex = new List<int>();
        for (int k = 0; k < brushes.Count; k++)
        {
            if (k == self || !s_Touching[self, k]) { touchingIndex.Add(-1); continue; }
            touchingIndex.Add(touching.Length);
            int offset = builder.planes.Length;
            foreach (var p in brushes[k].planes) builder.planes.Add(p);
            touching.Add(new ExactTouchingBrush
            {
                planeOffset = offset, planeCount = brushes[k].planes.Count,
                insideCategory = Inside, alignedCategory = Aligned, reverseAlignedCategory = ReverseAligned, routes = true
            });
        }
        var rows = new List<ushort>(); var start = new List<int>(); var end = new List<int>(); var lookupBrush = new List<int>();
        BuildRouting(brushes, self, rows, start, end, lookupBrush, touchingIndex);
        var rowList = new ExactList<ushort>(rows.Count); foreach (var r in rows) rowList.Add(r);
        var startList = new ExactList<int>(start.Count); foreach (var r in start) startList.Add(r);
        var endList = new ExactList<int>(end.Count); foreach (var r in end) endList.Add(r);
        var lookupList = new ExactList<int>(lookupBrush.Count); foreach (var r in lookupBrush) lookupList.Add(r);
        var triangles = new ExactList<int>(64);

        for (int f = 0; f < brush.planes.Count; f++)
        {
            var failure = builder.BuildFace(f, brush.planes.Count, ref touching, ref rowList, ref startList, ref endList, ref lookupList);
            if (failure != ExactFaceFailure.None) { Fail($"face failure {failure} brush {self} face {f}"); faceFailures++; continue; }
            s_Faces++;
            for (int pass = 0; pass < 2; pass++)
            {
                ref var edges = ref (pass == 0 ? ref builder.alignedEdges : ref builder.reverseAlignedEdges);
                triangles.Clear();
                var tf = triangulator.Triangulate(builder.planes[f], ref builder.vertices, ref edges, ref builder.lines, ref triangles);
                if (tf != ExactTriangulationFailure.None) { Fail($"triangulation failure {tf} brush {self} face {f} pass {pass} edges {edges.Length}"); faceFailures++; continue; }
                int fdom = builder.planes[f].DominantAxis();
                long fcomp = fdom == 0 ? builder.planes[f].a : (fdom == 1 ? builder.planes[f].b : builder.planes[f].c);
                ExactPredicates.ProjectionAxes(fdom, fcomp < 0, out int fu, out int fv);
                for (int t = 0; t + 2 < triangles.Length; t += 3)
                {
                    var va = builder.vertices[triangles[t]]; var vb = builder.vertices[triangles[t + 1]]; var vc = builder.vertices[triangles[t + 2]];
                    Check(ExactPredicates.Orientation(va.vertex, vb.vertex, vc.vertex, fu, fv) > 0, $"triangle not counter-clockwise, brush {self} face {f}");
                    result.Add(new OutTriangle
                    {
                        a = va.vertex, b = vb.vertex, c = vc.vertex,
                        fa = new[] { va.x, va.y, va.z }, fb = new[] { vb.x, vb.y, vb.z }, fc = new[] { vc.x, vc.y, vc.z },
                        category = pass == 0 ? SelfAligned : SelfReverseAligned, brush = self, face = f, plane = builder.planes[f]
                    });
                }
                CheckDelaunay(ref builder.vertices, ref triangles, fu, fv, $"{s_Scene}: brush {self} face {f} pass {pass}");
            }
        }
        touching.Dispose(); rowList.Dispose(); startList.Dispose(); endList.Dispose(); lookupList.Dispose(); triangles.Dispose();
        return result;
    }

    // ---- the checks ----------------------------------------------------------------------------------------------

    static void CheckDelaunay(ref ExactList<ExactOutputVertex> vertices, ref ExactList<int> triangles, int axisU, int axisV, string where)
    {
        var exact = new ExactVertex[vertices.Length];
        var positions = new float[vertices.Length * 3];
        for (int v = 0; v < vertices.Length; v++)
        {
            exact[v] = vertices[v].vertex;
            positions[v * 3] = vertices[v].x; positions[v * 3 + 1] = vertices[v].y; positions[v * 3 + 2] = vertices[v].z;
        }
        var indices = new int[triangles.Length];
        for (int i = 0; i < triangles.Length; i++)
            indices[i] = triangles[i];
        var result = Chisel.Core.Tests.ExactDelaunayCheck.Check(exact, positions, indices, axisU, axisV);
        s_InnerEdges += result.innerEdges;
        s_NotDelaunay += result.violations;
        s_FloatUncertain += result.floatUncertain;
        Check(result.violations == 0, $"{where}: {result.violations} of {result.innerEdges} inner edge(s) not locally Delaunay, first {result.first}");
    }

    static void CheckAgainstOracle(Random rng, List<Brush> brushes, List<OutTriangle> all, string scene)
    {
        for (int self = 0; self < brushes.Count; self++)
        {
            var brush = brushes[self];
            var touchingIndex = new List<int>();
            for (int k = 0; k < brushes.Count; k++) touchingIndex.Add(k == self ? -1 : k);
            var rows = new List<ushort>(); var start = new List<int>(); var end = new List<int>(); var lookupBrush = new List<int>();
            BuildRouting(brushes, self, rows, start, end, lookupBrush, touchingIndex);
            for (int f = 0; f < brush.planes.Count; f++)
            {
                var face = brush.planes[f];
                // Sample points on the face: along lines u = const, one point between every two consecutive places where
                // the line crosses any plane of any brush - so every cell the line passes through gets a sample, however
                // thin it is - plus random points.
                int axis = face.DominantAxis();
                int u = (axis + 1) % 3, v = (axis + 2) % 3;
                var samples = new List<ExactVertex>();
                for (int line = 0; line < 6; line++)
                {
                    double pu = Lerp(brush.bounds.Min(u), brush.bounds.Max(u), rng.NextDouble());
                    var qu = AxisPlane(u, pu);
                    var crossings = new List<ExactVertex>();
                    foreach (var other in brushes)
                        foreach (var q in other.planes)
                        {
                            var x = ExactVertex.Intersect(face, qu, q);
                            if (x.IsValid) crossings.Add(x);
                        }
                    crossings.Sort((x, y) => ExactPredicates.CompareCoordinate(x, y, v));
                    for (int i = 0; i + 1 < crossings.Count; i++)
                    {
                        if (ExactPredicates.CompareCoordinate(crossings[i], crossings[i + 1], v) == 0) continue;
                        double lo = crossings[i].CoordinateD(v) / crossings[i].Wd, hi = crossings[i + 1].CoordinateD(v) / crossings[i + 1].Wd;
                        var p = ExactVertex.Intersect(face, qu, AxisPlane(v, (lo + hi) * 0.5));
                        if (!p.IsValid) continue;
                        if (ExactPredicates.CompareCoordinate(p, crossings[i], v) <= 0 || ExactPredicates.CompareCoordinate(p, crossings[i + 1], v) >= 0)
                        { s_Unresolvable++; continue; }    // the cell is thinner than the sample grid can split
                        samples.Add(p);
                    }
                }
                for (int r = 0; r < 20; r++)
                {
                    var p = ExactVertex.Intersect(face, AxisPlane(u, Lerp(brush.bounds.Min(u), brush.bounds.Max(u), rng.NextDouble())),
                                                        AxisPlane(v, Lerp(brush.bounds.Min(v), brush.bounds.Max(v), rng.NextDouble())));
                    if (p.IsValid) samples.Add(p);
                }
                foreach (var p in samples)
                {
                    // inside the brush's face at all?
                    bool onFace = true, ambiguous = false;
                    for (int g = 0; g < brush.planes.Count; g++)
                    {
                        if (g == f || ExactPredicates.SamePlane(face, brush.planes[g]) != 0) continue;
                        int side = ExactPredicates.Side(brush.planes[g], p);
                        if (side > 0) onFace = false;
                        if (side == 0) ambiguous = true;
                    }
                    if (!onFace) continue;
                    if (ambiguous) { s_BoundarySkips++; continue; }
                    var categories = new int[brushes.Count];
                    for (int k = 0; k < brushes.Count; k++)
                    {
                        if (k == self) { categories[k] = Outside; continue; }
                        int c = Classify(brushes[k], face, p);
                        if (c < 0) { ambiguous = true; break; }
                        categories[k] = c;
                    }
                    if (ambiguous) { s_BoundarySkips++; continue; }
                    int expected = Route(rows, start, end, categories);
                    bool drawn = expected == SelfAligned || expected == SelfReverseAligned;
                    if (drawn) s_Drawn++; else s_NotDrawn++;

                    // what the builder produced at p
                    int found = 0, foundCategory = -1; bool onTriangleEdge = false;
                    foreach (var t in all)
                    {
                        if (t.brush != self || t.face != f) continue;
                        int dom = face.DominantAxis();
                        long comp = dom == 0 ? face.a : (dom == 1 ? face.b : face.c);
                        ExactPredicates.ProjectionAxes(dom, comp < 0, out int au, out int av);
                        int o1 = ExactPredicates.Orientation(t.a, t.b, p, au, av);
                        int o2 = ExactPredicates.Orientation(t.b, t.c, p, au, av);
                        int o3 = ExactPredicates.Orientation(t.c, t.a, p, au, av);
                        if (o1 >= 0 && o2 >= 0 && o3 >= 0)
                        {
                            if (o1 == 0 || o2 == 0 || o3 == 0) { onTriangleEdge = true; continue; }
                            found++; foundCategory = t.category;
                        }
                    }
                    if (onTriangleEdge) { s_BoundarySkips++; continue; }
                    s_Samples++;
                    Check(found <= 1, $"{scene}: brush {self} face {f}: sample covered {found} times");
                    if (drawn)
                        Check(found == 1 && foundCategory == expected, $"{scene}: brush {self} face {f}: HOLE expected {expected} found {found}/{foundCategory}");
                    else
                        Check(found == 0, $"{scene}: brush {self} face {f}: EXTRA, expected {expected} found {found}/{foundCategory}");
                }
            }
        }
    }

    static double Lerp(double a, double b, double t) => a + (b - a) * t;

    static ExactPlane AxisPlane(int axis, double value)
    {
        return ExactPlane.Quantize(axis == 0 ? 1 : 0, axis == 1 ? 1 : 0, axis == 2 ? 1 : 0, -value);
    }

    static int Bits(float f) => BitConverter.ToInt32(BitConverter.GetBytes(f), 0);
    static string Key(float[] a) => $"{Bits(a[0])},{Bits(a[1])},{Bits(a[2])}";

    static int s_PointContacts, s_TJunctions;

    // Whether some triangle has x as a corner and an edge from x along the line through p and q, on the plane they lie on.
    static bool HasEdgeAlong(List<OutTriangle> all, in ExactVertex x, in ExactPlane plane, in ExactVertex p, in ExactVertex q, int axisU, int axisV)
    {
        foreach (var u in all)
        {
            var corners = new[] { u.a, u.b, u.c };
            for (int i = 0; i < 3; i++)
            {
                if (!ExactPredicates.SamePoint(corners[i], x))
                    continue;
                for (int j = 1; j < 3; j++)
                {
                    var y = corners[(i + j) % 3];
                    if (ExactPredicates.Side(plane, y) == 0 && ExactPredicates.Orientation(p, q, y, axisU, axisV) == 0)
                        return true;
                }
            }
        }
        return false;
    }

    // Every directed edge a->b of the output has a partner b->a; no vertex lies inside another edge where a seam runs.
    static void CheckWatertight(List<OutTriangle> all, string scene)
    {
        var directed = new Dictionary<string, int>();
        var points = new Dictionary<string, ExactVertex>();
        void AddEdge(float[] a, float[] b, int sign)
        {
            var k = Key(a) + "|" + Key(b);
            directed.TryGetValue(k, out int n);
            directed[k] = n + sign;
        }
        foreach (var t in all)
        {
            // triangles are counter-clockwise seen from the face normal; reverse-aligned ones face the other way, so
            // their edges run the other way in the closed surface
            var a = t.fa; var b = t.fb; var c = t.fc;
            if (t.category == SelfReverseAligned) { var tmp = b; b = c; c = tmp; }
            AddEdge(a, b, 1); AddEdge(b, c, 1); AddEdge(c, a, 1);
            points[Key(t.fa)] = t.a; points[Key(t.fb)] = t.b; points[Key(t.fc)] = t.c;
        }
        int unmatched = 0;
        foreach (var pair in directed)
        {
            var parts = pair.Key.Split('|');
            directed.TryGetValue(parts[1] + "|" + parts[0], out int reverse);
            s_EdgeChecks++;
            if (pair.Value != reverse)
            {
                unmatched++;
                if (unmatched <= 3) Fail($"{scene}: unmatched edge {pair.Key} x{pair.Value} vs reverse x{reverse}");
            }
        }
        if (unmatched > 3) Fail($"{scene}: {unmatched} unmatched edges in total");

        // T-junctions: a vertex strictly inside a triangle edge (same line, between the ends)
        var list = new List<ExactVertex>(points.Values);
        int tj = 0;
        foreach (var t in all)
        {
            var corners = new[] { t.a, t.b, t.c };
            int dom = t.plane.DominantAxis();
            long comp = dom == 0 ? t.plane.a : (dom == 1 ? t.plane.b : t.plane.c);
            ExactPredicates.ProjectionAxes(dom, comp < 0, out int au, out int av);
            for (int e = 0; e < 3; e++)
            {
                var p = corners[e]; var q = corners[(e + 1) % 3];
                foreach (var x in list)
                {
                    if (ExactPredicates.SamePoint(x, p) || ExactPredicates.SamePoint(x, q)) continue;
                    // x on the triangle's plane?
                    if (ExactPredicates.Side(t.plane, x) != 0) continue;
                    if (ExactPredicates.Orientation(p, q, x, au, av) != 0) continue;
                    // strictly between p and q
                    bool between = false;
                    for (int axis = 0; axis < 3; axis++)
                    {
                        int sp = ExactPredicates.CompareCoordinate(x, p, axis), sq = ExactPredicates.CompareCoordinate(x, q, axis);
                        if (sp != 0 || sq != 0) { between = sp != 0 && sq != 0 && sp != sq; break; }
                    }
                    // A vertex inside an edge opens a crack only where a seam runs along that edge: some triangle has the
                    // vertex as a corner and an edge from it along the same line. Where another surface merely touches this
                    // one in a point - a cone's apex on a flat face - no seam is shared, and nothing can open.
                    if (between && !HasEdgeAlong(all, x, t.plane, p, q, au, av))
                    {
                        s_PointContacts++;
                        between = false;
                    }
                    if (between)
                    {
                        tj++;
                        s_TJunctions++;
                        if (tj <= 3)
                        {
                            var owners = new List<string>();
                            foreach (var o in all)
                                if (ExactPredicates.SamePoint(o.a, x) || ExactPredicates.SamePoint(o.b, x) || ExactPredicates.SamePoint(o.c, x))
                                    owners.Add($"b{o.brush}f{o.face}");
                            Fail($"{scene}: T-junction brush {t.brush} face {t.face}: ({x.ApproxX:R}, {x.ApproxY:R}, {x.ApproxZ:R}) lies inside the edge " +
                                 $"({p.ApproxX:R}, {p.ApproxY:R}, {p.ApproxZ:R}) - ({q.ApproxX:R}, {q.ApproxY:R}, {q.ApproxZ:R}); it is a vertex of " +
                                 string.Join(" ", new HashSet<string>(owners)));
                        }
                    }
                }
            }
        }
    }

    // ---- noise: the same scenes with float-sized errors in their planes -------------------------------------------

    static bool s_Progress = Environment.GetEnvironmentVariable("EXACT_PROGRESS") != null;
    static int s_PolytopeChecks, s_NoisyScenes;

    // What float transforms do to a scene: each brush turned about its centre by up to `tilt` radians around a random
    // axis and shifted by up to `shift` times (1 + its distance from the origin). A third of the brushes are left exact,
    // as axis-aligned brushes are in a real scene. Faces that coincided in the clean scene now miss each other by a hair,
    // and the exact CSG has to draw that exactly as it is (Documentation~/Design/ExactCSGRules.md, rules 1 to 3).
    static List<Brush> AddNoise(List<Brush> clean, Random rng, double tilt, double shift)
    {
        var noisy = new List<Brush>();
        foreach (var b in clean)
        {
            var nb = new Brush { op = b.op, bounds = b.bounds };
            bool quiet = rng.Next(3) == 0;
            double cx = (b.bounds.minX + b.bounds.maxX) / 2, cy = (b.bounds.minY + b.bounds.maxY) / 2, cz = (b.bounds.minZ + b.bounds.maxZ) / 2;
            double ax, ay, az, al;
            do { ax = rng.NextDouble() * 2 - 1; ay = rng.NextDouble() * 2 - 1; az = rng.NextDouble() * 2 - 1; al = Math.Sqrt(ax * ax + ay * ay + az * az); }
            while (al < 0.1 || al > 1);
            ax /= al; ay /= al; az /= al;
            double angle = quiet ? 0 : (rng.NextDouble() * 2 - 1) * tilt;
            double cs = Math.Cos(angle), sn = Math.Sin(angle), t = 1 - cs;
            double[,] R = {
                { t * ax * ax + cs,      t * ax * ay - sn * az, t * ax * az + sn * ay },
                { t * ax * ay + sn * az, t * ay * ay + cs,      t * ay * az - sn * ax },
                { t * ax * az - sn * ay, t * ay * az + sn * ax, t * az * az + cs } };
            double distance = Math.Sqrt(cx * cx + cy * cy + cz * cz);
            double sx = 0, sy = 0, sz = 0;
            if (!quiet)
            {
                double s = shift * (1 + distance) * rng.NextDouble();
                double dx, dy, dz, dl;
                do { dx = rng.NextDouble() * 2 - 1; dy = rng.NextDouble() * 2 - 1; dz = rng.NextDouble() * 2 - 1; dl = Math.Sqrt(dx * dx + dy * dy + dz * dz); }
                while (dl < 0.1 || dl > 1);
                sx = dx / dl * s; sy = dy / dl * s; sz = dz / dl * s;
            }
            foreach (var q in b.planes)
            {
                double nx = q.a, ny = q.b, nz = q.c, len = Math.Sqrt(nx * nx + ny * ny + nz * nz);
                double d = q.w / len; nx /= len; ny /= len; nz /= len;
                // x' = R (x - c) + c + s  =>  n' = R n, d' = n.c + d - n'.(c + s)
                double mx = R[0, 0] * nx + R[0, 1] * ny + R[0, 2] * nz;
                double my = R[1, 0] * nx + R[1, 1] * ny + R[1, 2] * nz;
                double mz = R[2, 0] * nx + R[2, 1] * ny + R[2, 2] * nz;
                double md = nx * cx + ny * cy + nz * cz + d - (mx * (cx + sx) + my * (cy + sy) + mz * (cz + sz));
                nb.planes.Add(ExactPlane.Quantize(mx, my, mz, md));
            }
            noisy.Add(nb);
        }
        return noisy;
    }

    static void Shuffle(int[] items, Random rng)
    {
        for (int i = items.Length - 1; i > 0; i--) { int j = rng.Next(i + 1); (items[i], items[j]) = (items[j], items[i]); }
    }

    static bool s_Dump;     // one round run on its own: print its brushes

    static List<OutTriangle> RunScene(List<Brush> brushes, string scene, Random rng, ref ExactFaceBuilder builder, ref ExactTriangulator triangulator)
    {
        s_Scene = scene;
        if (s_Dump)
        {
            Console.WriteLine($"{scene}:");
            for (int b = 0; b < brushes.Count; b++)
            {
                var brush = brushes[b];
                Console.WriteLine($"  brush {b} op {brush.op} bounds ({brush.bounds.minX}, {brush.bounds.minY}, {brush.bounds.minZ}) - ({brush.bounds.maxX}, {brush.bounds.maxY}, {brush.bounds.maxZ})");
                for (int p = 0; p < brush.planes.Count; p++)
                {
                    var q = brush.planes[p];
                    Console.WriteLine($"    f{p}: {q.a} {q.b} {q.c} {q.w}   ~ ({q.a / ExactPlane.kNormalScale:0.######}, {q.b / ExactPlane.kNormalScale:0.######}, {q.c / ExactPlane.kNormalScale:0.######}, {q.w / ExactPlane.kNormalScale:0.######})");
                }
            }
        }
        var all = new List<OutTriangle>();
        s_Touching = new bool[brushes.Count, brushes.Count];
        for (int i = 0; i < brushes.Count; i++)
            for (int j = i + 1; j < brushes.Count; j++)
            {
                bool t = Touch(brushes[i], brushes[j]) || rng.Next(4) == 0;   // over-inclusion must be harmless
                s_Touching[i, j] = s_Touching[j, i] = t;
                if (t) s_TouchingPairs++; else s_SeparatePairs++;
            }
        for (int b = 0; b < brushes.Count; b++)
        {
            if (s_Progress) { Console.WriteLine($"  {scene} build brush {b}"); Console.Out.Flush(); }
            all.AddRange(BuildBrush(brushes, b, ref builder, ref triangulator, out int ff));
        }
        s_Triangles += all.Count;
        if (s_Progress) { Console.WriteLine($"  {scene} oracle"); Console.Out.Flush(); }
        CheckAgainstOracle(rng, brushes, all, scene);
        if (s_Progress) { Console.WriteLine($"  {scene} watertight"); Console.Out.Flush(); }
        CheckWatertight(all, scene);
        return all;
    }

    // A clean scene and the same scene with float-sized noise: each is drawn exactly as its planes say, judged by the
    // oracle on those same planes, and its brushes' corners are checked.
    static void RunNoisyScene(List<Brush> clean, string scene, Random rng, ref ExactFaceBuilder builder, ref ExactTriangulator triangulator)
    {
        if (s_Progress) { Console.WriteLine(scene); Console.Out.Flush(); }
        RunScene(clean, scene + " clean", rng, ref builder, ref triangulator);
        CheckPolytopeCorners(clean, scene + " clean");
        var noisy = AddNoise(clean, rng, 2e-7, 2e-7);
        RunScene(noisy, scene + " noisy", rng, ref builder, ref triangulator);
        CheckPolytopeCorners(noisy, scene + " noisy");
        s_NoisyScenes++;
    }

    // ---- brush corners -----------------------------------------------------------------------------------------

    // The corners of a brush from every triple of its planes that meets in a point inside all of them, each exact point
    // once, however many planes pass through it: an independent computation of ExactFaceBuilder.PolytopeCorners.
    static List<ExactVertex> TripleCorners(List<ExactPlane> planes)
    {
        var corners = new List<ExactVertex>();
        for (int i = 0; i < planes.Count; i++)
            for (int j = i + 1; j < planes.Count; j++)
                for (int k = j + 1; k < planes.Count; k++)
                {
                    var v = ExactVertex.Intersect(planes[i], planes[j], planes[k]);
                    if (!v.IsValid) continue;
                    bool inside = true;
                    foreach (var q in planes) if (ExactPredicates.Side(q, v) > 0) { inside = false; break; }
                    if (!inside || corners.Exists(c => ExactPredicates.SamePoint(c, v))) continue;
                    corners.Add(v);
                }
        return corners;
    }

    // ExactFaceBuilder.PolytopeCorners against TripleCorners: the same exact points, each once.
    static void CheckPolytopeCorners(List<Brush> brushes, string scene)
    {
        var builder = ExactFaceBuilder.Create();
        var corners = new ExactList<ExactVertex>(64);
        foreach (var brush in brushes)
        {
            builder.planes.Clear();
            foreach (var p in brush.planes) builder.planes.Add(p);
            corners.Clear();
            var failure = builder.PolytopeCorners(ref corners);
            s_PolytopeChecks++;
            Check(failure == ExactFaceFailure.None, $"{scene}: PolytopeCorners failed: {failure}");
            var expected = TripleCorners(brush.planes);
            Check(corners.Length == expected.Count, $"{scene}: PolytopeCorners found {corners.Length} corners, the plane triples {expected.Count}");
            for (int c = 0; c < corners.Length; c++)
            {
                var v = corners[c];
                Check(expected.Exists(e => ExactPredicates.SamePoint(e, v)), $"{scene}: PolytopeCorners has ({v.ApproxX}, {v.ApproxY}, {v.ApproxZ}), which is no corner of the brush");
                for (int d = 0; d < c; d++)
                    Check(!ExactPredicates.SamePoint(corners[d], v), $"{scene}: PolytopeCorners lists ({v.ApproxX}, {v.ApproxY}, {v.ApproxZ}) twice");
            }
        }
        corners.Dispose();
        builder.Dispose();
    }

    // ---- where more than three planes meet ------------------------------------------------------------------------

    // A cone of `sides` planes through one apex, on a base plane. Each side plane is made exact on its own, so after
    // Quantize the side planes generally no longer pass through one point: the apex becomes a small cluster of exact
    // vertices and edges, which the exact CSG has to handle like any other geometry (ExactCSGRules.md, rule 4).
    // Its base at y = cy, its apex at y = cy + height; a negative height hangs the cone below its base.
    static Brush Cone(double cx, double cy, double cz, double radius, double height, int sides, double phase, int op)
    {
        var b = new Brush { op = op };
        double up = height > 0 ? 1 : -1, h = Math.Abs(height);
        for (int i = 0; i < sides; i++)
        {
            double a = phase + 2 * Math.PI * i / sides;
            // outwards, and away from the base; through the apex and the base's rim at angle a
            double nx = Math.Cos(a) * h, ny = up * radius, nz = Math.Sin(a) * h;
            b.planes.Add(ExactPlane.Quantize(nx, ny, nz, -(nx * cx + ny * (cy + height) + nz * cz)));
        }
        b.planes.Add(ExactPlane.Quantize(0, -up, 0, up * cy));
        double r = radius / Math.Cos(Math.PI / sides) + 0.01;
        b.bounds = new double3x2 { minX = cx - r, minY = Math.Min(cy, cy + height), minZ = cz - r,
                                   maxX = cx + r, maxY = Math.Max(cy, cy + height), maxZ = cz + r };

        // Do the exact side planes still meet in one point? Counted, so the run shows it exercised split apexes.
        var apex = ExactVertex.Intersect(b.planes[0], b.planes[1], b.planes[sides / 2]);
        bool whole = apex.IsValid;
        for (int i = 0; i < sides && whole; i++)
            whole = ExactPredicates.Side(b.planes[i], apex) == 0;
        if (whole) s_WholeApexes++; else s_SplitApexes++;
        return b;
    }
    static int s_WholeApexes, s_SplitApexes;

    // Cones, pyramids and boxes: apexes where many planes met before each was made exact, touching a box's face or
    // corner, cut by a subtracted box through the apex, or two apexes on one point.
    static List<Brush> ApexScene(Random rng)
    {
        var brushes = new List<Brush> { Box(-2, -1, -2, 2, 0, 2, OpAdditive) };
        int count = rng.Next(2, 5);
        for (int i = 0; i < count; i++)
        {
            double x = rng.Next(-1, 2) * 0.5, z = rng.Next(-1, 2) * 0.5;
            double height = 1 + rng.Next(0, 3) * 0.5;
            int sides = rng.Next(0, 3) == 0 ? 4 : rng.Next(3, 25);
            double phase = rng.Next(2) == 0 ? 0 : rng.NextDouble();
            int op = rng.Next(4) == 0 ? OpSubtractive : OpAdditive;
            switch (rng.Next(4))
            {
                case 0: brushes.Add(Cone(x, 0, z, 0.5 + rng.Next(0, 3) * 0.25, height, sides, phase, op)); break;
                case 1: brushes.Add(Cone(x, 0, z, 0.5, height, sides, phase, op)); brushes.Add(Box(x - 1, height, z - 1, x + 1, height + 0.5, z + 1, OpAdditive)); break;
                case 2: brushes.Add(Cone(x, 0, z, 0.75, height, sides, phase, op)); brushes.Add(Box(x, 0, z, x + 1, height + 1, z + 1, OpSubtractive)); break;
                default: brushes.Add(Cone(x, 0, z, 0.5, height, sides, phase, op)); brushes.Add(Cone(x, 2 * height, z, 0.5, -height, sides, phase, OpAdditive)); break;
            }
        }
        return brushes;
    }

    // ---- small cases with a known answer -----------------------------------------------------------------------------

    // ExactAffine against an explicit inverse transpose, for random transforms including mirrors, and exactness for
    // quarter turns with representable scales.
    static void TransformUnitTests()
    {
        var rng = new Random(4);
        for (int trial = 0; trial < 20000; trial++)
        {
            double[] m = new double[12];
            bool axisAligned = trial % 2 == 0;
            if (axisAligned)
            {
                int[] perm = { 0, 1, 2 }; Shuffle(perm, rng);
                double[] scales = { 1, 2, 0.5, 3, 0.25, 1.5 };
                for (int col = 0; col < 3; col++)
                {
                    double sc = scales[rng.Next(scales.Length)] * (rng.Next(2) == 0 ? 1 : -1);
                    m[col * 3 + perm[col]] = sc;
                }
                for (int k = 9; k < 12; k++) m[k] = (float)(rng.NextDouble() * 200 - 100);
            }
            else
            {
                for (int k = 0; k < 12; k++) m[k] = (float)(rng.NextDouble() * 4 - 2);
            }
            var affine = ExactAffine.Create(m[0], m[1], m[2], m[3], m[4], m[5], m[6], m[7], m[8], m[9], m[10], m[11]);
            if (!affine.IsValid) continue;
            // a random plane, float coefficients like a brush mesh's
            double nx = (float)(rng.NextDouble() * 2 - 1), ny = (float)(rng.NextDouble() * 2 - 1), nz = (float)(rng.NextDouble() * 2 - 1);
            if (axisAligned) { int ax = rng.Next(3); nx = ax == 0 ? 1 : 0; ny = ax == 1 ? 1 : 0; nz = ax == 2 ? 1 : 0; if (rng.Next(2) == 0) { nx = -nx; ny = -ny; nz = -nz; } }
            double d = (float)(rng.NextDouble() * 20 - 10);
            affine.TransformPlane(nx, ny, nz, d, out double ox, out double oy, out double oz, out double od);
            // points: one on the local plane, one inside it, transformed forward
            double nn = nx * nx + ny * ny + nz * nz;
            double px = -d * nx / nn, py = -d * ny / nn, pz = -d * nz / nn;   // on the plane
            double qx = px - nx, qy = py - ny, qz = pz - nz;                   // inside (plane value -nn < 0)
            affine.TransformPoint(px, py, pz, out double Px, out double Py, out double Pz);
            affine.TransformPoint(qx, qy, qz, out double Qx, out double Qy, out double Qz);
            double olen = Math.Sqrt(ox * ox + oy * oy + oz * oz);
            double onP = (ox * Px + oy * Py + oz * Pz + od) / olen;
            double atQ = (ox * Qx + oy * Qy + oz * Qz + od) / olen;
            double scale = 1 + Math.Abs(Px) + Math.Abs(Py) + Math.Abs(Pz);
            Check(Math.Abs(onP) <= 1e-12 * scale * 100, $"affine: point on the plane is {onP} off it");
            Check(atQ < 0, "affine: the inside of the plane is not inside after transforming");
            if (axisAligned)
            {
                // exact: the normalized result is an axis and the distance the exact one
                var exact = ExactPlane.Quantize(ox, oy, oz, od);
                var fromPoint = ExactPlane.Quantize(ox / olen, oy / olen, oz / olen, -(ox * Px + oy * Py + oz * Pz) / olen);
                Check(exact.a == fromPoint.a && exact.b == fromPoint.b && exact.c == fromPoint.c && exact.w == fromPoint.w,
                      "affine: an axis-aligned transform did not move the plane exactly");
            }
        }
    }

    // list.Add(list[i]) across a growth: the element must be read before the old buffer goes. Elements of 4 KB make the
    // buffer big enough that the heap returns it to the system when it is freed, so reading it afterwards faults.
    unsafe struct Big { public fixed long data[512]; public long Tag { get { fixed (long* p = data) return p[0]; } set { fixed (long* p = data) p[0] = value; } } }
    static void ListSelfAddTest()
    {
        var list = new ExactList<Big>(200);
        for (int i = 0; i < 200; i++) { var b = new Big(); b.Tag = 1000 + i; list.Add(b); }
        while (list.Length < 4000)
        {
            int from = list.Length / 3;
            long expected = list[from].Tag;
            list.Add(list[from]);
            Check(list[list.Length - 1].Tag == expected, "ExactList: adding one of its own elements lost it");
        }
        list.Dispose();
    }

    // The watertightness check itself, on meshes made by hand: a vertex inside an edge where a seam runs along it is a
    // T-junction, and a vertex where another surface touches a face in a point is not.
    static void WatertightCheckUnitTests()
    {
        ExactVertex P(double x, double y, double z) => ExactVertex.Intersect(AxisPlane(0, x), AxisPlane(1, y), AxisPlane(2, z));
        float[] F(double x, double y, double z) => new[] { (float)x, (float)y, (float)z };
        OutTriangle T(double[] a, double[] b, double[] c, int brush, ExactPlane plane) => new OutTriangle
        {
            a = P(a[0], a[1], a[2]), b = P(b[0], b[1], b[2]), c = P(c[0], c[1], c[2]),
            fa = F(a[0], a[1], a[2]), fb = F(b[0], b[1], b[2]), fc = F(c[0], c[1], c[2]),
            category = SelfAligned, brush = brush, face = 0, plane = plane
        };
        var floor = ExactPlane.Quantize(0, 0, 1, 0);        // z = 0, facing +z; counter-clockwise seen from +z
        double[] p = { 0, 0, 0 }, q = { 2, 0, 0 }, r = { 1, 2, 0 }, s = { 1, -2, 0 }, x = { 1, 0, 0 };

        // Both meshes are open, so the edge pairing fails on them too: the planted failures are the point, and they are
        // taken back once the T-junction counts have been read.
        int failures = s_Failures;
        s_Planted = true;

        // p -> q is an edge of one triangle; across it the other side runs p <- x <- q in two triangles: x is a T-junction
        int before = s_TJunctions;
        CheckWatertight(new List<OutTriangle> { T(p, q, r, 0, floor), T(q, x, s, 1, floor), T(x, p, s, 1, floor) }, "unit: seam T-junction");
        bool seamReported = s_TJunctions > before;

        // a cone's apex touching the floor triangle in the middle of its edge p -> q: a point contact, not a T-junction
        var slope = ExactPlane.Quantize(0, 1, 1, 0);
        before = s_TJunctions; int contacts = s_PointContacts;
        CheckWatertight(new List<OutTriangle> { T(p, q, r, 0, floor), T(x, new double[] { 1, -1, 1 }, new double[] { 2, -1, 1 }, 1, slope) }, "unit: point contact");
        bool contactPassed = s_TJunctions == before && s_PointContacts > contacts;

        s_Planted = false;
        s_Failures = failures;
        Check(seamReported, "watertight check: a vertex inside a shared edge was not reported as a T-junction");
        Check(contactPassed, "watertight check: a point contact was taken for a T-junction");
    }

    // The Delaunay check itself, on a quad made by hand: (0,0) (4,0) (4,1) (1,1) on z = 0. The diagonal (0,0)-(4,1) leaves
    // (1,1) inside the circle through the other three and the other diagonal is valid, so it is a violation; the other
    // diagonal is not.
    static void DelaunayCheckUnitTests()
    {
        ExactVertex P(double x, double y) => ExactVertex.Intersect(AxisPlane(0, x), AxisPlane(1, y), AxisPlane(2, 0));
        var vertices  = new[] { P(0, 0), P(4, 0), P(4, 1), P(1, 1) };
        var positions = new float[] { 0, 0, 0, 4, 0, 0, 4, 1, 0, 1, 1, 0 };
        var floor = ExactPlane.Quantize(0, 0, 1, 0);
        ExactPredicates.ProjectionAxes(floor.DominantAxis(), floor.c < 0, out int u, out int v);
        var bad  = Chisel.Core.Tests.ExactDelaunayCheck.Check(vertices, positions, new[] { 0, 1, 2, 0, 2, 3 }, u, v);
        var good = Chisel.Core.Tests.ExactDelaunayCheck.Check(vertices, positions, new[] { 0, 1, 3, 1, 2, 3 }, u, v);
        Check(bad.innerEdges == 1 && bad.violations == 1, $"Delaunay check: the wrong diagonal gave {bad.violations} violation(s) of {bad.innerEdges} inner edge(s)");
        Check(good.innerEdges == 1 && good.violations == 0, $"Delaunay check: the right diagonal gave {good.violations} violation(s) of {good.innerEdges} inner edge(s): {good.first}");
        Check(bad.floatUncertain == 0 && good.floatUncertain == 0, "Delaunay check: a triangle of the quad was taken for a degenerate one");
        // a triangle whose floats lie on one line
        var flat = Chisel.Core.Tests.ExactDelaunayCheck.Check(new[] { P(0, 0), P(1, 0), P(2, 0) }, new float[] { 0, 0, 0, 1, 0, 0, 2, 0, 0 }, new[] { 0, 1, 2 }, u, v);
        Check(flat.floatUncertain == 1, "Delaunay check: three floats on a line were taken for a triangle that turns left");
    }

    // What ExactInputJob reads from PolytopeCorners: planes that do not close the brush within the world are a failure
    // (the brush then takes no part), planes that enclose nothing give no corners and no failure (the brush has no
    // volume), and every exact corner comes once, however many planes meet there.
    static void PolytopeUnitTests()
    {
        var builder = ExactFaceBuilder.Create();
        var corners = new ExactList<ExactVertex>(64);
        ExactFaceFailure Run(List<ExactPlane> planes)
        {
            builder.planes.Clear();
            foreach (var p in planes) builder.planes.Add(p);
            corners.Clear();
            return builder.PolytopeCorners(ref corners);
        }
        // a box without its top: every side runs up to the world's edge
        var open = Box(0, 0, 0, 1, 1, 1, OpAdditive).planes;
        open.RemoveAt(2);
        Check(Run(open) == ExactFaceFailure.FaceOutsideBounds, "polytope: a box without its top was not reported");
        // a slab whose top lies below its bottom
        var inverted = Box(0, 0, 0, 1, 1, 1, OpAdditive).planes;
        inverted[2] = ExactPlane.Quantize(0, 1, 0, 0.5);    // y <= -0.5, below the bottom at y = 0
        var failure = Run(inverted);
        Check(failure == ExactFaceFailure.None && corners.Length == 0, $"polytope: an inverted slab gave {failure} and {corners.Length} corners");
        // a flat brush: its top is its bottom turned over
        var flat = Box(0, 0, 0, 1, 1, 1, OpAdditive).planes;
        flat[2] = ExactPlane.Quantize(0, 1, 0, 0);          // y <= 0, and the bottom says y >= 0
        failure = Run(flat);
        Check(failure == ExactFaceFailure.None && corners.Length == 0, $"polytope: a flat brush gave {failure} and {corners.Length} corners");
        // the unit box: eight corners, each once although three faces have it
        failure = Run(Box(0, 0, 0, 1, 1, 1, OpAdditive).planes);
        Check(failure == ExactFaceFailure.None && corners.Length == 8, $"polytope: the unit box gave {failure} and {corners.Length} corners");
        // a square pyramid whose four sides are exact planes through one apex: five corners, the apex once
        var pyramid = new List<ExactPlane>
        {
            ExactPlane.Quantize( 1, 1,  0, -1), ExactPlane.Quantize(-1, 1,  0, -1),
            ExactPlane.Quantize( 0, 1,  1, -1), ExactPlane.Quantize( 0, 1, -1, -1),
            ExactPlane.Quantize( 0, -1, 0,  0)
        };
        failure = Run(pyramid);
        Check(failure == ExactFaceFailure.None && corners.Length == 5, $"polytope: the pyramid gave {failure} and {corners.Length} corners");
        corners.Dispose();
        builder.Dispose();
    }

    // Each round its own generator, so a failing scene can be run again on its own: args "<regular> <noisy> <only>" with
    // <only> "r<round>" for one regular round or "n<round>" for one noisy one.
    static Random RoundRng(int seed, int round) => new Random(unchecked(seed * 1000003 + round));

    static int Main(string[] args)
    {
        ListSelfAddTest();
        PolytopeUnitTests();
        TransformUnitTests();
        WatertightCheckUnitTests();
        DelaunayCheckUnitTests();
        var builder = ExactFaceBuilder.Create();
        var triangulator = ExactTriangulator.Create();
        int scenes = 0;
        int rounds = args.Length > 0 ? int.Parse(args[0]) : 600;
        int noisyRounds = args.Length > 1 ? int.Parse(args[1]) : 150;
        string only = args.Length > 2 ? args[2] : null;
        s_Dump = only != null;
        for (int round = 0; round < noisyRounds; round++)
        {
            if (only != null && only != "n" + round) continue;
            var noiseRng = RoundRng(777, round);
            int kind = round % 4;
            List<Brush> clean;
            string name;
            switch (kind)
            {
                case 0: clean = GridScene(noiseRng); name = "noisy-grid"; break;
                case 1: clean = FloorScene(noiseRng); name = "noisy-floor"; break;
                case 2: clean = ApexScene(noiseRng); name = "noisy-apex"; break;
                default: clean = CylinderScene(noiseRng); name = "noisy-cylinder"; break;
            }
            RunNoisyScene(clean, name + " #" + round, noiseRng, ref builder, ref triangulator);
            scenes += 2;
        }
        int stripRounds = args.Length > 3 ? int.Parse(args[3]) : 40;
        for (int round = 0; round < stripRounds; round++)
        {
            if (only != null && only != "s" + round) continue;
            var stripRng = RoundRng(20260927, round);
            RunNoisyScene(StripScene(stripRng), "strip #" + round, stripRng, ref builder, ref triangulator);
            scenes += 2;
        }
        Console.WriteLine($"{s_NoisyScenes} scenes run clean and with noise; {s_PolytopeChecks} brushes' corners checked");
        for (int round = 0; round < rounds; round++)
        {
            if (only != null && only != "r" + round) continue;
            var rng = RoundRng(20260923, round);
            int kind = round % 9;
            List<Brush> brushes;
            string name;
            switch (kind)
            {
                case 0: brushes = GridScene(rng); name = "grid"; break;
                case 1: brushes = RandomScene(rng); name = "random"; break;
                case 2: brushes = SliverScene(rng); name = "sliver"; break;
                case 3: brushes = RotatedScene(rng, 0); name = "rotated"; break;
                case 4: brushes = Translate(GridScene(rng), 4096 + rng.Next(0, 4096)); name = "far-grid"; break;
                case 6: brushes = CylinderScene(rng); name = "cylinder"; break;
                case 7: brushes = FloorScene(rng); name = "floor"; break;
                case 8: brushes = ApexScene(rng); name = "apex"; break;
                default: brushes = RotatedScene(rng, 1000 + rng.NextDouble() * 5000); name = "far-rotated"; break;
            }
            RunScene(brushes, name + " #" + round, rng, ref builder, ref triangulator);
            scenes++;
        }
        builder.Dispose();
        triangulator.Dispose();
        Console.WriteLine($"cone apexes: {s_SplitApexes} split into several points once each plane was made exact, {s_WholeApexes} still one point; " +
                          $"{s_PointContacts} vertices touch another surface in a point, on no seam; {s_TJunctions} T-junctions");
        if (only == null && rounds + noisyRounds >= 20)
            Check(s_SplitApexes > 0, "no cone apex was split by making its planes exact: the apex scenes tested nothing of rule 4");
        Console.WriteLine($"{scenes} scenes, {s_Faces} faces, {s_Triangles} triangles, {s_Samples} samples judged ({s_BoundarySkips} on boundaries skipped), {s_EdgeChecks} edges checked");
        Console.WriteLine($"pairs touching (or over-included) {s_TouchingPairs}, separate {s_SeparatePairs}; samples drawn {s_Drawn}, not drawn {s_NotDrawn}; cells too thin to sample {s_Unresolvable}");
        Console.WriteLine($"{s_InnerEdges} inner edges judged for local Delaunay-ness, {s_NotDelaunay} not; {s_FloatUncertain} triangles do not certainly turn left in their floats");
        Console.WriteLine($"{s_Checks} checks, {s_Failures} failures");
        return s_Failures == 0 ? 0 : 1;
    }
}
