using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;

namespace Chisel.Core.Tests
{
    // A painted surface subdivided at its grid (PaintSubdivision): every grid point in it a vertex, its outline cut where
    // the grid meets it, and no sliver left by the flips
    public sealed class PaintSubdivisionTests
    {
        // A square of side 2 in the xz plane at offset, two triangles, and a grid of 0.1 cells over it exactly
        static void Subdivide(float3 offset, bool otherDiagonal, out int vertexCount, out int triangleCount, out float minAngle, out string worst, out string offGrid)
        {
            var mapper = new UniqueVertexMapper
            {
                surfaceColliderVertices = new NativeList<float3>(Allocator.Temp),
                surfaceSelectVertices   = new NativeList<SelectVertex>(Allocator.Temp),
                surfaceRenderVertices   = new NativeList<RenderVertex>(Allocator.Temp)
            };
            using var indices = new NativeList<int>(Allocator.Temp);
            var corners = new[] { new float3(0, 0, 0), new float3(0, 0, 2), new float3(2, 0, 2), new float3(2, 0, 0) };
            foreach (var corner in corners)
            {
                mapper.surfaceColliderVertices.Add(offset + corner);
                mapper.surfaceSelectVertices.Add(new SelectVertex { position = offset + corner });
                mapper.surfaceRenderVertices.Add(new RenderVertex { position = offset + corner, normal = new float3(0, 1, 0) });
            }
            if (otherDiagonal) { indices.Add(0); indices.Add(1); indices.Add(3); indices.Add(1); indices.Add(2); indices.Add(3); }
            else               { indices.Add(0); indices.Add(1); indices.Add(2); indices.Add(0); indices.Add(2); indices.Add(3); }

            NativeArray<ChiselPaintLayer> layers;
            using var _layers = layers = new NativeArray<ChiselPaintLayer>(1, Allocator.Temp);
            using var values = new NativeArray<float4>(21 * 21, Allocator.Temp);
            layers[0] = new ChiselPaintLayer { origin = offset, cellU = new float3(0.1f, 0, 0), cellV = new float3(0, 0, 0.1f), resolution = new int2(21, 21) };
            var lookup = new ChiselPaintLookup { layers = layers, values = values };
            PaintSubdivision.Subdivide(indices, ref mapper, new float3(0, 1, 0), lookup, new ChiselPaintRange { start = 0, count = 1 });

            vertexCount   = mapper.surfaceColliderVertices.Length;
            offGrid       = "";
            for (int v = 0; v < vertexCount; v++)
            {
                var cell = (mapper.surfaceColliderVertices[v] - offset) / 0.1f;
                if (math.any(math.abs(cell - math.round(cell)) > 0.01f))
                    offGrid += " " + (mapper.surfaceColliderVertices[v] - offset);
            }
            triangleCount = indices.Length / 3;
            minAngle      = 180;
            worst         = null;
            for (int t = 0; t < indices.Length; t += 3)
            {
                var a = mapper.surfaceColliderVertices[indices[t]];
                var b = mapper.surfaceColliderVertices[indices[t + 1]];
                var c = mapper.surfaceColliderVertices[indices[t + 2]];
                var angle = math.min(Angle(a, b, c), math.min(Angle(b, c, a), Angle(c, a, b)));
                if (angle < minAngle)
                {
                    minAngle = angle;
                    worst    = $"{a - offset} {b - offset} {c - offset}";
                }
            }
            mapper.surfaceColliderVertices.Dispose();
            mapper.surfaceSelectVertices.Dispose();
            mapper.surfaceRenderVertices.Dispose();
        }

        static float Angle(float3 o, float3 p, float3 q) => math.degrees(math.acos(math.clamp(math.dot(math.normalize(p - o), math.normalize(q - o)), -1, 1)));

        // All 21 x 21 points, the outline's too: the triangles are the grid's half cells, whichever way the square was cut and
        // however far from the origin it is
        [Test]
        public void EveryGridPointInside_IsAVertex_AndNoTriangleIsASliver([Values(false, true)] bool otherDiagonal, [Values(0, 5000)] float offset)
        {
            Subdivide(new float3(offset, offset * 0.5f, -offset), otherDiagonal, out var vertices, out var triangles, out var minAngle, out var worst, out var offGrid);
            Assert.That(vertices, Is.EqualTo(21 * 21), "not on a grid point:" + offGrid);
            Assert.That(triangles, Is.EqualTo(2 * 20 * 20));
            Assert.That(minAngle, Is.GreaterThan(30), "no sliver; the thinnest: " + worst);
        }
    }
}
