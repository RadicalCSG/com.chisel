using System;
using System.Collections.Generic;
using Chisel.Core;

// Offline checks of ExactPolytope.Build (the outline of a brush given as planes, ChiselBrushDefinition.SetPlanes):
// bm_c1a4b's cone, and generated
// brushes whose planes nearly meet in a point (cones, pyramids) or are tangent to a sphere.
static class ExactPolytopeTests
{
    static int failures;

    static void Fail(string what)
    {
        failures++;
        if (failures < 40)
            Console.WriteLine("FAIL " + what);
    }

    static float Bits(uint bits) => BitConverter.ToSingle(BitConverter.GetBytes(bits), 0);

    static readonly uint[] kCone =
    {
        0x00000000,0xBF800000,0x80000000,0x41AFAE14, 0x3E0C0DE9,0x3E8234EE,0x3F751857,0x4202885F, 0x3EBBA91A,0x80000000,0x3F6E2F3F,0x424041DC,
        0x3F18F346,0x3E825AF9,0x3F42A9FD,0x422BAA45, 0x3F494C29,0x80000000,0x3F1E298E,0x4250FE00, 0x3F665850,0x3E824729,0x3EB57BE2,0x421B81E5,
        0x3F7D6D54,0x80000000,0x3E10D0C3,0x422668BA, 0x3F751857,0x3E8234EE,0xBE0C0DE9,0x41AFA1DC, 0x3F6E2F3F,0x00000000,0xBEBBA91A,0x419B9193,
        0x3F42A9EC,0x3E825B95,0xBF18F33A,0xC06EF5D1, 0x3F1E298E,0x00000000,0xBF494C29,0xC1125CF1, 0x3EB57BE2,0x3E824729,0xBF665850,0xC1FB06E7,
        0x3E10D0C3,0x00000000,0xBF7D6D54,0xC20EC59D, 0xBE0C0DE9,0x3E8234EE,0xBF751857,0xC253332A, 0xBEBBA91A,0x80000000,0xBF6E2F3F,0xC254CD03,
        0xBF18F346,0x3E825AF9,0xBF42A9FD,0xC27C6CA1, 0xBF494C29,0x80000000,0xBF1E298E,0xC2658C7D, 0xBF665850,0x3E824729,0xBEB57BE2,0xC26C37FB,
        0xBF7D6D54,0x80000000,0xBE10D0C3,0xC23AF0CE, 0xBF751857,0x3E8234EE,0x3E0C0DE9,0xC2287BB9, 0xBF6E2F3F,0x00000000,0x3EBBA91A,0xC1C4A7E1,
        0xBF42A9EC,0x3E825B95,0x3F18F33A,0xC183A6A7, 0xBF1E298E,0x00000000,0x3F494C29,0x408045F3, 0xBEB57BE2,0x3E824729,0x3F665850,0x41333575,
        0xBE10D0C3,0x00000000,0x3F7D6D54,0x41F47B13, 0xBF6E2F3F,0x80000000,0xBEBBA91A,0xC254CD03, 0xBF1E298E,0x80000000,0xBF494C29,0xC2658C7D,
        0xBE10D0C3,0x80000000,0xBF7D6D54,0xC23AF0CE, 0x3EBBA91A,0x00000000,0xBF6E2F3F,0xC1C4A7E1, 0x3F494C29,0x00000000,0xBF1E298E,0x408045F3,
        0x3F7D6D54,0x00000000,0xBE10D0C3,0x41F47B13, 0x3F6E2F3F,0x80000000,0x3EBBA91A,0x424041DC, 0x3F1E298E,0x80000000,0x3F494C29,0x4250FE00,
        0x3E10D0C3,0x80000000,0x3F7D6D54,0x422668BA, 0xBEBBA91A,0x00000000,0x3F6E2F3F,0x419B9193, 0xBF494C29,0x00000000,0x3F1E298E,0xC1125CF1,
        0xBF7D6D54,0x00000000,0x3E10D0C3,0xC20EC59D, 0xBF751857,0x3E8234EE,0xBE0C0DE9,0xC253332A, 0xBF665850,0x3E824729,0x3EB57BE2,0xC1FB06E7,
        0xBF18F346,0x3E825AF9,0x3F42A9FD,0xC06EF375, 0xBE0C0DE9,0x3E8234EE,0x3F751857,0x41AFA1DC, 0x3EB57BEF,0x3E8246A3,0x3F665861,0x421B8211,
        0x3F42A9F7,0x3E825B2B,0x3F18F342,0x422BAA34, 0x3F75182F,0x3E823626,0x3E0C0DD2,0x420287FC, 0x3F665850,0x3E824729,0xBEB57BE2,0x41333575,
        0x3F18F346,0x3E825AF9,0xBF42A9FD,0xC183A664, 0x3E0C0DE9,0x3E8234EE,0xBF751857,0xC2287BB9, 0xBEB57BE2,0x3E824729,0xBF665850,0xC26C37FB,
        0xBF42A9FD,0x3E825AF9,0xBF18F346,0xC27C6CA1,
    };

    // The planes as CreateFromPlanes gets them (float), then the 6 planes of a box of half size `half` around the origin.
    static double[] WithBox(float[] input, double half)
    {
        int n = input.Length / 4;
        var planes = new double[(n + 6) * 4];
        for (int i = 0; i < input.Length; i++) planes[i] = input[i];
        double[] box = { -1, 0, 0, -half, 1, 0, 0, -half, 0, -1, 0, -half, 0, 1, 0, -half, 0, 0, -1, -half, 0, 0, 1, -half };
        for (int i = 0; i < 24; i++) planes[n * 4 + i] = box[i];
        return planes;
    }

    struct Mesh
    {
        public List<float> v; public List<int> ev, et, ff, fc, fp;
        public ExactPolytope.Result result;
    }

    static Mesh Build(double[] planes, int inputCount)
    {
        var m = new Mesh { v = new List<float>(), ev = new List<int>(), et = new List<int>(), ff = new List<int>(), fc = new List<int>(), fp = new List<int>() };
        m.result = ExactPolytope.Build(planes, planes.Length / 4, inputCount, m.v, m.ev, m.et, m.ff, m.fc, m.fp);
        return m;
    }

    // What a brush has to be: closed (every half-edge has a twin running the other way), one face per plane at most, every
    // face at least a triangle, Euler's V - E + F = 2, every vertex inside or on every plane and on the planes of its faces,
    // each within what rounding one float coordinate at a time allows.
    static int eulerBroken;

    static void CheckBrush(string name, double[] planes, int inputCount, Mesh m, bool expectBoxFaces, bool strictEuler)
    {
        if (m.result != ExactPolytope.Result.Built) { Fail(name + ": " + m.result); return; }
        int V = m.v.Count / 3, E = m.ev.Count, F = m.ff.Count;
        if (V - E / 2 + F != 2)
        {
            if (strictEuler) Fail($"{name}: Euler V {V} - E {E / 2} + F {F} != 2");
            else eulerBroken++;
        }
        var seen = new HashSet<int>();
        for (int f = 0; f < F; f++)
        {
            if (m.fc[f] < 3) Fail($"{name}: face {f} has {m.fc[f]} edges");
            for (int k = 0; k < m.fc[f]; k++)
            {
                int a = m.ev[m.ff[f] + k], b = m.ev[m.ff[f] + (k + 1) % m.fc[f]], c = m.ev[m.ff[f] + (k + 2) % m.fc[f]];
                if (a == b) Fail($"{name}: face {f} has an edge with no length at vertex {a}");
                if (a == c) Fail($"{name}: face {f} runs out to vertex {b} and straight back to {a}");
            }
            if (!seen.Add(m.fp[f])) Fail($"{name}: plane {m.fp[f]} has two faces");
            if (!expectBoxFaces && m.fp[f] >= inputCount) Fail($"{name}: a face of the box (plane {m.fp[f]}) survived");
        }
        for (int f = 0; f < F; f++)
        {
            int first = m.ff[f], count = m.fc[f];
            for (int e = first; e < first + count; e++)
            {
                int prev = e == first ? first + count - 1 : e - 1;
                int t = m.et[e];
                if (t < 0 || t >= E || m.et[t] != e) { Fail($"{name}: edge {e} twin {t}"); continue; }
                // the twin runs from our vertex back to our start
                int tf = FaceOf(m, t);
                int tprev = t == m.ff[tf] ? m.ff[tf] + m.fc[tf] - 1 : t - 1;
                if (m.ev[t] != m.ev[prev] || m.ev[tprev] != m.ev[e]) Fail($"{name}: edge {e} and its twin {t} do not join the same vertices");
            }
        }
        int P = planes.Length / 4;
        for (int p = 0; p < P; p++)
        {
            double nx = planes[p * 4], ny = planes[p * 4 + 1], nz = planes[p * 4 + 2], d = planes[p * 4 + 3];
            double len = Math.Sqrt(nx * nx + ny * ny + nz * nz);
            nx /= len; ny /= len; nz /= len; d /= len;
            for (int i = 0; i < V; i++)
            {
                double x = m.v[i * 3], y = m.v[i * 3 + 1], z = m.v[i * 3 + 2];
                double s = nx * x + ny * y + nz * z + d;
                double tolerance = Tolerance(x, y, z);
                if (s > tolerance) Fail($"{name}: vertex {i} ({x}, {y}, {z}) is {s} outside plane {p} (allowed {tolerance})");
            }
        }
        for (int f = 0; f < F; f++)
        {
            int p = m.fp[f];
            double nx = planes[p * 4], ny = planes[p * 4 + 1], nz = planes[p * 4 + 2], d = planes[p * 4 + 3];
            double len = Math.Sqrt(nx * nx + ny * ny + nz * nz);
            nx /= len; ny /= len; nz /= len; d /= len;
            for (int e = m.ff[f]; e < m.ff[f] + m.fc[f]; e++)
            {
                int i = m.ev[e];
                double x = m.v[i * 3], y = m.v[i * 3 + 1], z = m.v[i * 3 + 2];
                double s = nx * x + ny * y + nz * z + d;
                double tolerance = Tolerance(x, y, z);
                if (Math.Abs(s) > tolerance) Fail($"{name}: vertex {i} of face {f} is {s} off its plane {p} (allowed {tolerance})");
            }
            // counter-clockwise seen from outside: the Newell normal points along the plane's
            double sx = 0, sy = 0, sz = 0;
            int first = m.ff[f], count = m.fc[f];
            for (int e = first; e < first + count; e++)
            {
                int a = m.ev[e], b = m.ev[e + 1 < first + count ? e + 1 : first];
                sx += (m.v[a * 3 + 1] - m.v[b * 3 + 1]) * (m.v[a * 3 + 2] + (double)m.v[b * 3 + 2]);
                sy += (m.v[a * 3 + 2] - m.v[b * 3 + 2]) * (m.v[a * 3 + 0] + (double)m.v[b * 3 + 0]);
                sz += (m.v[a * 3 + 0] - m.v[b * 3 + 0]) * (m.v[a * 3 + 1] + (double)m.v[b * 3 + 1]);
            }
            if (sx * nx + sy * ny + sz * nz < 0) Fail($"{name}: face {f} (plane {p}) winds the wrong way");
        }
    }

    // Rounding each coordinate to float moves a point by at most half an ulp per axis, |c| * 2^-24; a plane made exact moves
    // by far less (2^-32 steps). A float input plane is only a unit normal to about 2^-24 as well.
    static double Tolerance(double x, double y, double z) => (Math.Abs(x) + Math.Abs(y) + Math.Abs(z)) * 1.2e-7 + 1e-6;

    static int FaceOf(Mesh m, int e)
    {
        for (int f = 0; f < m.ff.Count; f++)
            if (e >= m.ff[f] && e < m.ff[f] + m.fc[f]) return f;
        return -1;
    }

    static float[] ConePlanes()
    {
        var planes = new float[kCone.Length];
        for (int i = 0; i < kCone.Length; i++) planes[i] = Bits(kCone[i]);
        return planes;
    }

    // n side planes through (nearly) one apex, leaning outwards, plus a base: a cone or pyramid with every side meeting at
    // the tip, the planes rounded to float the way an importer hands them over.
    static float[] Cone(Random random, int sides, double apexX, double apexY, double apexZ, double jitter)
    {
        var planes = new List<float>();
        double slope = 0.2 + random.NextDouble() * 3;
        for (int i = 0; i < sides; i++)
        {
            double angle = 2 * Math.PI * (i + random.NextDouble() * 0.3) / sides;
            double nx = Math.Cos(angle), nz = Math.Sin(angle), ny = 1 / slope;
            double len = Math.Sqrt(nx * nx + ny * ny + nz * nz);
            nx /= len; ny /= len; nz /= len;
            double px = apexX + (random.NextDouble() - 0.5) * jitter, py = apexY + (random.NextDouble() - 0.5) * jitter, pz = apexZ + (random.NextDouble() - 0.5) * jitter;
            planes.Add((float)nx); planes.Add((float)ny); planes.Add((float)nz); planes.Add((float)-(nx * px + ny * py + nz * pz));
        }
        double height = 1 + random.NextDouble() * 200;
        planes.Add(0); planes.Add(-1); planes.Add(0); planes.Add((float)(apexY - height));
        return planes.ToArray();
    }

    // Planes tangent to a sphere: a generic convex brush.
    static float[] Sphere(Random random, int count, double cx, double cy, double cz, double radius)
    {
        var planes = new List<float>();
        for (int i = 0; i < count; i++)
        {
            double x, y, z, l;
            do { x = random.NextDouble() * 2 - 1; y = random.NextDouble() * 2 - 1; z = random.NextDouble() * 2 - 1; l = x * x + y * y + z * z; } while (l > 1 || l < 1e-3);
            l = Math.Sqrt(l); x /= l; y /= l; z /= l;
            planes.Add((float)x); planes.Add((float)y); planes.Add((float)z); planes.Add((float)-(x * cx + y * cy + z * cz + radius));
        }
        return planes.ToArray();
    }

    static int Main(string[] args)
    {
        int count = args.Length > 0 ? int.Parse(args[0]) : 2000;

        var cone = ConePlanes();
        var conePlanes = WithBox(cone, 4096);
        var coneMesh = Build(conePlanes, cone.Length / 4);
        CheckBrush("bm_c1a4b cone", conePlanes, cone.Length / 4, coneMesh, false, true);
        Console.WriteLine($"cone: {coneMesh.result}, {coneMesh.v.Count / 3} vertices, {coneMesh.ff.Count} faces for {cone.Length / 4} planes");

        // The same plane twice is one face, described by the later of the two; a plane of the box that is also one of the
        // brush's own is described by the brush's.
        {
            float[] cube = { -1, 0, 0, -1, 1, 0, 0, -1, 0, -1, 0, -1, 0, 1, 0, -1, 0, 0, -1, -1, 0, 0, 1, -1, 1, 0, 0, -1 };
            var planes = WithBox(cube, 1);
            var mesh = Build(planes, cube.Length / 4);
            CheckBrush("cube with +x twice, in a box on its faces", planes, cube.Length / 4, mesh, false, true);
            if (mesh.ff.Count != 6) Fail($"cube with +x twice: {mesh.ff.Count} faces");
            if (mesh.fp.Contains(1)) Fail("cube with +x twice: the first +x describes a face, not the later one");
            if (!mesh.fp.Contains(6)) Fail("cube with +x twice: the later +x describes no face");
        }

        // BrushFromPlanesTouchingPlaneTests' planes, as Unity.Mathematics makes them in float: a plane touching the unit box
        // along its (+x, +y) edge or at its (+x, +y, +z) corner keeps the box exactly; the opposite plane leaves nothing.
        {
            float[] box = { -1, 0, 0, -1, 1, 0, 0, -1, 0, -1, 0, -1, 0, 1, 0, -1, 0, 0, -1, -1, 0, 0, 1, -1 };
            float e = Bits(0x3F3504F3), ed = Bits(0xBFB504F3), c = Bits(0x3F13CD3A), cd = Bits(0xBFDDB3D7);
            var touching = new[] { ("edge", new[] { e, e, 0f, ed }), ("corner", new[] { c, c, c, cd }) };
            foreach (var (what, plane) in touching)
            {
                foreach (bool flipped in new[] { false, true })
                {
                    var input = new float[28];
                    box.CopyTo(input, 0);
                    for (int k = 0; k < 4; k++) input[24 + k] = flipped ? -plane[k] : plane[k];
                    var planes = WithBox(input, 8);
                    var mesh = Build(planes, 7);
                    string name = $"unit box and a plane touching its {what}{(flipped ? ", facing in" : "")}";
                    if (flipped)
                    {
                        if (mesh.result != ExactPolytope.Result.Empty) Fail($"{name}: {mesh.result}, not empty");
                        continue;
                    }
                    CheckBrush(name, planes, 7, mesh, false, true);
                    if (mesh.ff.Count != 6 || mesh.v.Count != 24) Fail($"{name}: {mesh.ff.Count} faces, {mesh.v.Count / 3} vertices");
                    for (int k = 0; k < mesh.v.Count; k++)
                        if (Math.Abs(mesh.v[k]) != 1) Fail($"{name}: a vertex coordinate is {mesh.v[k]}");
                }
            }
        }

        var random = new Random(12345);
        int built = 0, empty = 0;
        for (int i = 0; i < count; i++)
        {
            float[] input;
            string name;
            double cx = (random.NextDouble() - 0.5) * 4000, cy = (random.NextDouble() - 0.5) * 4000, cz = (random.NextDouble() - 0.5) * 4000;
            if (i % 2 == 0)
            {
                int sides = 3 + random.Next(48);
                double jitter = random.Next(3) == 0 ? 0 : Math.Pow(10, -1 - random.NextDouble() * 5);
                input = Cone(random, sides, cx, cy, cz, jitter);
                name = $"cone {i} ({sides} sides, jitter {jitter:G3})";
            }
            else
            {
                input = Sphere(random, 8 + random.Next(60), cx, cy, cz, 0.01 + random.NextDouble() * 50);
                name = $"sphere {i}";
            }
            var planes = WithBox(input, 4096);
            var mesh = Build(planes, input.Length / 4);
            if (mesh.result == ExactPolytope.Result.Empty) { empty++; continue; }
            // A sphere with few planes may be open on one side, and the box closes it
            bool boxFaces = i % 2 == 1;
            CheckBrush(name, planes, input.Length / 4, mesh, boxFaces, i % 2 == 1);
            built++;
        }
        Console.WriteLine($"generated: {built} built, {empty} empty, {failures} failure(s); cones whose float apex joins exact edges (Euler != 2): {eulerBroken}");
        return failures == 0 ? 0 : 1;
    }
}
