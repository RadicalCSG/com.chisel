using NUnit.Framework;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace Chisel.Core.Tests
{
    [TestFixture]
    public class OutputMeshCleanupTests
    {
        // What a vertex carries follows from its position, as a surface's attributes follow from its plane
        static RenderVertex Render(float3 p)
        {
            return new RenderVertex { position = p, normal = new float3(0, 0, 1), tangent = new float4(1, 0, 0, 1),
                                      uv0 = new float2(p.x * 0.5f, p.y + 2), uv1 = new float2(p.x, p.y) * 0.25f };
        }

        static readonly float3 A = new float3(0, 0, 0), B = new float3(2, 0, 0), C = new float3(2, 2, 0), D = new float3(0, 2, 0);
        static readonly float3 Centre = new float3(1, 1, 0), Bottom = new float3(1, 0, 0);

        // A square cut into four around its centre
        static readonly float3[] kFan = { A, B, Centre,  B, C, Centre,  C, D, Centre,  D, A, Centre };

        sealed class Surface : System.IDisposable
        {
            public NativeList<int>          indices  = new NativeList<int>(Allocator.Temp);
            public NativeList<float3>       collider = new NativeList<float3>(Allocator.Temp);
            public NativeList<SelectVertex> select   = new NativeList<SelectVertex>(Allocator.Temp);
            public NativeList<RenderVertex> render   = new NativeList<RenderVertex>(Allocator.Temp);

            // Every corner of every triangle its own vertex unless shared is true
            public Surface(float3[] corners, bool shared = true, System.Func<float3, RenderVertex> attributes = null)
            {
                attributes ??= Render;
                for (int i = 0; i < corners.Length; i++)
                {
                    var found = -1;
                    for (int v = 0; shared && v < collider.Length && found < 0; v++)
                    {
                        if (math.all(collider[v] == corners[i]))
                            found = v;
                    }
                    if (found < 0)
                    {
                        found = collider.Length;
                        collider.Add(corners[i]);
                        select.Add(new SelectVertex { position = corners[i], entityID = new Vector4(1, 0, 0, 0) });
                        render.Add(attributes(corners[i]));
                    }
                    indices.Add(found);
                }
            }

            public bool Clean(bool cut = false) => OutputMeshCleanup.CleanSurface(indices, collider, select, render, cut);

            public int TriangleCount => indices.Length / 3;

            public float Area()
            {
                var area = 0.0f;
                for (int i = 0; i < indices.Length; i += 3)
                    area += Cross(collider[indices[i + 1]].xy - collider[indices[i]].xy, collider[indices[i + 2]].xy - collider[indices[i]].xy) * 0.5f;
                return area;
            }

            // The attributes interpolated at a point of the plane, from the triangle it is in
            public bool At(float2 point, out RenderVertex value)
            {
                for (int i = 0; i < indices.Length; i += 3)
                {
                    var a = collider[indices[i]].xy; var b = collider[indices[i + 1]].xy; var c = collider[indices[i + 2]].xy;
                    var area = Cross(b - a, c - a);
                    var wb = Cross(point - a, c - a) / area;
                    var wc = Cross(b - a, point - a) / area;
                    var wa = 1 - wb - wc;
                    if (wa < -1e-5f || wb < -1e-5f || wc < -1e-5f)
                        continue;
                    var ra = render[indices[i]]; var rb = render[indices[i + 1]]; var rc = render[indices[i + 2]];
                    value = new RenderVertex
                    {
                        normal  = ra.normal  * wa + rb.normal  * wb + rc.normal  * wc,
                        tangent = ra.tangent * wa + rb.tangent * wb + rc.tangent * wc,
                        uv0     = ra.uv0     * wa + rb.uv0     * wb + rc.uv0     * wc,
                        uv1     = ra.uv1     * wa + rb.uv1     * wb + rc.uv1     * wc
                    };
                    return true;
                }
                value = default;
                return false;
            }

            public bool Uses(float3 position)
            {
                for (int i = 0; i < indices.Length; i++)
                {
                    if (math.all(collider[indices[i]] == position))
                        return true;
                }
                return false;
            }

            public bool HasEdge(float3 from, float3 to)
            {
                for (int i = 0; i < indices.Length; i += 3)
                {
                    for (int c = 0; c < 3; c++)
                    {
                        var p = collider[indices[i + c]]; var q = collider[indices[i + (c + 1) % 3]];
                        if ((math.all(p == from) && math.all(q == to)) || (math.all(p == to) && math.all(q == from)))
                            return true;
                    }
                }
                return false;
            }

            public void Dispose()
            {
                indices.Dispose(); collider.Dispose(); select.Dispose(); render.Dispose();
            }
        }

        static float Cross(float2 a, float2 b) => a.x * b.y - a.y * b.x;

        // Every attribute at every point of the square what it was
        static void AssertLooksTheSame(Surface before, Surface after, float2 min, float2 max)
        {
            Assert.That(after.Area(), Is.EqualTo(before.Area()).Within(1e-5f), "the surface covers what it did");
            for (int y = 0; y <= 8; y++)
            {
                for (int x = 0; x <= 8; x++)
                {
                    var point = math.lerp(min, max, new float2(x, y) / 8);
                    Assert.That(before.At(point, out var was), Is.True);
                    Assert.That(after.At(point, out var now), Is.True, $"nothing drawn at {point}");
                    Assert.That(math.distance(now.uv0, was.uv0), Is.LessThan(1e-4f), $"uv0 at {point}");
                    Assert.That(math.distance(now.uv1, was.uv1), Is.LessThan(1e-4f), $"uv1 at {point}");
                    Assert.That(math.distance(now.normal, was.normal), Is.LessThan(1e-4f), $"normal at {point}");
                    Assert.That(math.distance(now.tangent, was.tangent), Is.LessThan(1e-4f), $"tangent at {point}");
                }
            }
        }

        [Test]
        public void AVertexInsideAFlatSurface_IsTakenOut()
        {
            using var before = new Surface(kFan);
            using var after  = new Surface(kFan);
            Assert.That(after.Clean(), Is.True);
            Assert.That(after.Uses(Centre), Is.False, "the centre changes nothing that is drawn");
            Assert.That(after.TriangleCount, Is.EqualTo(2));
            Assert.That(after.render.Length, Is.EqualTo(4));
            AssertLooksTheSame(before, after, A.xy, C.xy);
        }

        [Test]
        public void AVertexWhoseUvsDontInterpolate_Stays()
        {
            System.Func<float3, RenderVertex> bumped = p =>
            {
                var vertex = Render(p);
                if (math.all(p == Centre))
                    vertex.uv0 += 0.1f;
                return vertex;
            };
            using var surface = new Surface(kFan, attributes: bumped);
            Assert.That(surface.Clean(), Is.False);
            Assert.That(surface.Uses(Centre), Is.True);
            Assert.That(surface.TriangleCount, Is.EqualTo(4));
        }

        [Test]
        public void AVertexWhoseNormalDoesntInterpolate_Stays()
        {
            System.Func<float3, RenderVertex> smoothed = p =>
            {
                var vertex = Render(p);
                if (math.all(p == Centre))
                    vertex.normal = math.normalize(new float3(0.1f, 0, 1));
                return vertex;
            };
            using var surface = new Surface(kFan, attributes: smoothed);
            Assert.That(surface.Clean(), Is.False);
            Assert.That(surface.Uses(Centre), Is.True);
        }

        [Test]
        public void AVertexOfAnotherSelection_Stays()
        {
            using var surface = new Surface(kFan);
            for (int v = 0; v < surface.collider.Length; v++)
            {
                if (math.all(surface.collider[v] == Centre))
                    surface.select[v] = new SelectVertex { position = Centre, entityID = new Vector4(2, 0, 0, 0) };
            }
            Assert.That(surface.Clean(), Is.False);
            Assert.That(surface.Uses(Centre), Is.True);
        }

        // The hole around a vertex next to the outline has corners in a line there: filling it must not make a triangle of
        // them (it has no area, and points nowhere). Tilted, so the plane's 2D coordinates are rounded.
        [Test]
        public void FillingTheHoleNextToTheOutline_MakesNoTriangleWithoutArea()
        {
            var e = new float3(1, 0, 0); var v = new float3(1, 0.5f, 0);
            var flat = new[] { A, e, v,  e, B, v,  B, C, v,  C, D, v,  D, A, v };
            for (int turn = 0; turn < 16; turn++)
            {
                var rotation = math.mul(quaternion.RotateX(0.37f * turn), quaternion.RotateY(0.23f * turn));
                var corners = new float3[flat.Length];
                for (int i = 0; i < flat.Length; i++)
                    corners[i] = math.mul(rotation, flat[i]) + new float3(3.1f, -7.3f, 11.7f);
                var normal = math.mul(rotation, new float3(0, 0, 1));
                using var surface = new Surface(corners, attributes: p => new RenderVertex
                {
                    position = p, normal = normal, tangent = new float4(1, 0, 0, 1), uv0 = new float2(p.x + p.y, p.z), uv1 = new float2(p.y, p.z)
                });
                Assert.That(surface.Clean(), Is.True, $"turn {turn}");
                var area = 0.0f;
                for (int i = 0; i < surface.indices.Length; i += 3)
                {
                    var a = surface.collider[surface.indices[i]]; var b = surface.collider[surface.indices[i + 1]]; var c = surface.collider[surface.indices[i + 2]];
                    var cross = math.cross(b - a, c - a);
                    Assert.That(math.dot(cross, normal), Is.GreaterThan(1e-4f), $"turn {turn}: a triangle without area, or facing away: {a} {b} {c}");
                    area += math.length(cross) * 0.5f;
                }
                Assert.That(area, Is.EqualTo(4).Within(1e-3f), $"turn {turn}");
            }
        }

        // A vertex on the outline is shared with the surface across it: taking it out would leave a crack there
        [Test]
        public void AVertexOnTheOutline_Stays()
        {
            var corners = new[] { Bottom, B, C,  Bottom, C, D,  Bottom, D, A };
            using var before = new Surface(corners);
            using var after  = new Surface(corners);
            after.Clean();
            Assert.That(after.Uses(Bottom), Is.True);
            Assert.That(after.render.Length, Is.EqualTo(5));
            AssertLooksTheSame(before, after, A.xy, C.xy);
        }

        static readonly float3 ThinA = new float3(0, 0, 0), ThinB = new float3(3, -0.5f, 0), ThinC = new float3(6, 0, 0), ThinD = new float3(3, 0.5f, 0);
        static readonly float3[] kThinPair = { ThinA, ThinB, ThinC,  ThinA, ThinC, ThinD };

        [Test]
        public void AThinPairOfTrianglesTheCutsMade_IsFlippedFatter()
        {
            var a = ThinA; var b = ThinB; var c = ThinC; var d = ThinD;
            var corners = kThinPair;
            using var before = new Surface(corners);
            using var after  = new Surface(corners);
            Assert.That(after.Clean(cut: true), Is.True);
            Assert.That(after.HasEdge(a, c), Is.False, "the long diagonal is gone");
            Assert.That(after.HasEdge(b, d), Is.True);
            Assert.That(after.TriangleCount, Is.EqualTo(2));
            Assert.That(after.Area(), Is.EqualTo(before.Area()).Within(1e-5f));
            for (int i = 0; i <= 10; i++)
            {
                var point = new float2(0.6f * i, 0);
                Assert.That(before.At(point, out var was), Is.True);
                Assert.That(after.At(point, out var now), Is.True);
                Assert.That(math.distance(now.uv0, was.uv0), Is.LessThan(1e-4f));
            }
        }

        // The CSG's own triangles are what everything else compares against (the oracle tests, the collider)
        [Test]
        public void ThinTrianglesOfAnUncutSurface_StayAsTheCSGMadeThem()
        {
            using var surface = new Surface(kThinPair);
            Assert.That(surface.Clean(), Is.False);
            Assert.That(surface.HasEdge(ThinA, ThinC), Is.True);
        }

        [Test]
        public void VerticesTheSameInEveryWay_AreOne()
        {
            var corners = new[] { A, B, C,  A, C, D };
            using var surface = new Surface(corners, shared: false);
            Assert.That(surface.render.Length, Is.EqualTo(6));
            Assert.That(surface.Clean(), Is.True);
            Assert.That(surface.render.Length, Is.EqualTo(4));
            Assert.That(surface.TriangleCount, Is.EqualTo(2));
        }

        [Test]
        public void ACleanSurface_IsLeftAsItIs()
        {
            using var surface = new Surface(new[] { A, B, C,  A, C, D });
            Assert.That(surface.Clean(), Is.False);
            using var cleaned = new Surface(kFan);
            cleaned.Clean();
            Assert.That(cleaned.Clean(), Is.False, "a second cleanup finds nothing");
        }

        // A brush's buffer of the given surfaces, each as Surface makes it
        static BlobAssetReference<ChiselBrushRenderBuffer> Brush(int paintedSurface, params float3[][] surfaces)
        {
            var builder = new BlobBuilder(Allocator.Temp);
            try
            {
                ref var root = ref builder.ConstructRoot<ChiselBrushRenderBuffer>();
                var buffers = builder.Allocate(ref root.surfaces, surfaces.Length);
                for (int s = 0; s < surfaces.Length; s++)
                {
                    using var surface = new Surface(surfaces[s]);
                    buffers[s].Construct(builder, surface.indices, surface.collider, surface.select, surface.render, s, s,
                                         SurfaceDestinationFlags.Renderable | SurfaceDestinationFlags.Collidable, default, default);
                    if (s == paintedSurface)
                        buffers[s].paintSurface = s;
                }
                var queries = builder.Allocate(ref root.querySurfaces, 1);
                using var list = new NativeList<ChiselQuerySurface>(Allocator.Temp);
                for (int s = 0; s < surfaces.Length; s++)
                    list.Add(new ChiselQuerySurface { surfaceIndex = s, vertexCount = buffers[s].vertexCount, indexCount = buffers[s].indexCount,
                                                      geometryHashValue = buffers[s].geometryHashValue, surfaceHashValue = buffers[s].surfaceHashValue });
                builder.Construct(ref queries[0].surfaces, list);
                root.surfaceCount = surfaces.Length;
                return builder.CreateBlobAssetReference<ChiselBrushRenderBuffer>(Allocator.Persistent);
            }
            finally
            {
                builder.Dispose();
            }
        }

        [Test]
        public void ABrushsCleanedSurface_KeepsItsColliderAndLightmapLayout()
        {
            var source = Brush(-1, kFan, new[] { A, B, C });
            try
            {
                var result = OutputMeshCleanup.Clean(source);
                Assert.That(result.IsCreated, Is.True);
                try
                {
                    ref var was = ref source.Value.surfaces[0];
                    ref var now = ref result.Value.surfaces[0];
                    Assert.That(now.indexCount, Is.EqualTo(6));
                    Assert.That(now.physicsIndices.Length, Is.EqualTo(was.indices.Length), "the collider keeps its triangles");
                    Assert.That(now.physicsVertices.Length, Is.EqualTo(was.colliderVertices.Length));
                    Assert.That(now.shapeHashValue, Is.EqualTo(was.shapeHashValue), "the lightmap layout follows the shape");
                    Assert.That(now.lightmapChart, Is.EqualTo(was.lightmapChart));
                    Assert.That(result.Value.querySurfaces[0].surfaces[0].indexCount, Is.EqualTo(6), "the meshes are sized for what is drawn");
                    Assert.That(result.Value.surfaces[1].indexCount, Is.EqualTo(3), "a surface with nothing to take out is as it was");
                    Assert.That(result.Value.surfaces[1].physicsIndices.Length, Is.EqualTo(0));
                }
                finally
                {
                    result.Dispose();
                }
            }
            finally
            {
                source.Dispose();
            }
        }

        // Its paint is added per vertex when its mesh is made
        [Test]
        public void APaintedSurface_IsLeftAsItIs()
        {
            var source = Brush(0, kFan);
            try
            {
                var result = OutputMeshCleanup.Clean(source);
                Assert.That(result.IsCreated, Is.False);
            }
            finally
            {
                source.Dispose();
            }
        }

        // The cache hands a brush whose buffer is the same what it made before, and forgets a brush it wasn't asked for
        [Test]
        public void TheCache_KeepsWhatAnUnchangedBrushWasGiven()
        {
            var source = Brush(-1, kFan);
            var cache  = new NativeParallelHashMap<CompactNodeID, MeshCleanupEntry>(4, Allocator.Persistent);
            var brushes = new NativeList<BrushData>(Allocator.Temp);
            var work    = new NativeList<MeshCleanupWork>(Allocator.Temp);
            var patched = new NativeList<BlobAssetReference<ChiselBrushRenderBuffer>>(Allocator.Temp);
            try
            {
                brushes.Add(new BrushData { brushRenderBuffer = source });
                OutputMeshCleanup.Plan(brushes, cache, work);
                Assert.That(work.Length, Is.EqualTo(1));
                for (int i = 0; i < work.Length; i++)
                    work[i] = OutputMeshCleanup.CleanPlanned(work[i], brushes.AsArray());
                OutputMeshCleanup.Finish(brushes, patched, work, cache);
                var first = brushes[0].brushRenderBuffer;
                Assert.That(first, Is.Not.EqualTo(source));

                brushes[0] = new BrushData { brushRenderBuffer = source };
                OutputMeshCleanup.Plan(brushes, cache, work);
                Assert.That(work.Length, Is.EqualTo(0), "nothing to do again");
                Assert.That(brushes[0].brushRenderBuffer, Is.EqualTo(first));
                OutputMeshCleanup.Finish(brushes, patched, work, cache);

                brushes.Clear();
                OutputMeshCleanup.Plan(brushes, cache, work);
                OutputMeshCleanup.Finish(brushes, patched, work, cache);
                Assert.That(cache.Count(), Is.EqualTo(0));
                Assert.That(patched.Length, Is.EqualTo(0));
            }
            finally
            {
                foreach (var pair in cache)
                {
                    if (pair.Value.result.IsCreated)
                        pair.Value.result.Dispose();
                }
                cache.Dispose();
                brushes.Dispose();
                work.Dispose();
                patched.Dispose();
                source.Dispose();
            }
        }
    }
}
