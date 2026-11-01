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

        // A square's two diagonals are as good as each other: which one it gets must not depend on the order its triangles
        // came in, or a surface's squares flip back and forth as the cuts around them change
        [Test]
        public void RemovedVertex_IsFilledTheSameWay_WhateverOrderItsTrianglesCameIn()
        {
            bool? diagonalAC = null;
            for (int start = 0; start < 4; start++)
            {
                var corners = new float3[kFan.Length];
                for (int i = 0; i < kFan.Length; i++)
                    corners[i] = kFan[(i + start * 3) % kFan.Length];
                using var surface = new Surface(corners);
                Assert.That(surface.Clean(), Is.True);
                Assert.That(surface.TriangleCount, Is.EqualTo(2));
                var ac = surface.HasEdge(A, C);
                Assert.That(ac || surface.HasEdge(B, D), Is.True);
                diagonalAC ??= ac;
                Assert.That(ac, Is.EqualTo(diagonalAC.Value), $"starting at triangle {start}");
            }
        }

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

        static readonly float3[] kSquare = { A, B, C,  A, C, D };

        static bool Split(Surface surface, params float3[] points)
        {
            using var array = new NativeArray<float3>(points, Allocator.Temp);
            return OutputMeshCleanup.SplitOutline(surface.indices, surface.collider, surface.select, surface.render, array);
        }

        static void AssertNoTriangleWithoutArea(Surface surface)
        {
            for (int i = 0; i < surface.indices.Length; i += 3)
            {
                var a = surface.collider[surface.indices[i]]; var b = surface.collider[surface.indices[i + 1]]; var c = surface.collider[surface.indices[i + 2]];
                Assert.That(math.cross(b - a, c - a).z, Is.GreaterThan(1e-6f), $"a triangle without area, or facing away: {a} {b} {c}");
            }
        }

        // An edge running past another surface's corner draws a crack there (a T-junction)
        [Test]
        public void ACornerOnAnOutlineEdge_IsPutIntoIt()
        {
            using var before = new Surface(kSquare);
            using var after  = new Surface(kSquare);
            Assert.That(Split(after, Bottom), Is.True);
            Assert.That(after.Uses(Bottom), Is.True);
            Assert.That(after.TriangleCount, Is.EqualTo(3));
            AssertNoTriangleWithoutArea(after);
            AssertLooksTheSame(before, after, A.xy, C.xy);
        }

        // The corner's own position, so the two surfaces meet exactly there
        [Test]
        public void ACornerAHairOffTheEdge_IsPutIntoIt_WhereItIs()
        {
            var corner = new float3(1, -5e-5f, 0);
            using var surface = new Surface(kSquare);
            Assert.That(Split(surface, corner), Is.True);
            Assert.That(surface.Uses(corner), Is.True);
            AssertNoTriangleWithoutArea(surface);
        }

        [Test]
        public void SeveralCornersOnEdges_AreAllPutIn()
        {
            var right = new float3(2, 1, 0); var top1 = new float3(0.5f, 2, 0); var top2 = new float3(1.5f, 2, 0);
            using var before = new Surface(kSquare);
            using var after  = new Surface(kSquare);
            Assert.That(Split(after, top2, Bottom, right, top1), Is.True);
            foreach (var corner in new[] { Bottom, right, top1, top2 })
                Assert.That(after.Uses(corner), Is.True, $"{corner}");
            AssertNoTriangleWithoutArea(after);
            AssertLooksTheSame(before, after, A.xy, C.xy);
        }

        [Test]
        public void ACornerAtAnEnd_AwayFromTheEdge_OrOnAnInsideEdge_IsLeft()
        {
            using var surface = new Surface(kSquare);
            Assert.That(Split(surface, new float3(1e-4f, 0, 0), new float3(1, 0.01f, 0), new float3(1, -0.01f, 0), Centre), Is.False);
            Assert.That(surface.TriangleCount, Is.EqualTo(2));
        }

        // An edge two surfaces share has no crack: splitting it on one side only would make one
        [Test]
        public void AnEdgeTwoSurfacesShare_IsLeft()
        {
            using var surface = new Surface(kSquare);
            var shared = new NativeParallelHashMap<int3x2, int>(4, Allocator.Temp);
            using var _shared = shared;
            shared[OutputMeshCleanup.EdgeKey(A, B)] = 2;
            using var points = new NativeArray<float3>(new[] { Bottom }, Allocator.Temp);
            Assert.That(OutputMeshCleanup.SplitOutline(surface.indices, surface.collider, surface.select, surface.render, points, shared), Is.False);
            Assert.That(surface.Uses(Bottom), Is.False);
        }

        // The weld leaves a needle (three corners on a line) where an edge meets several: it draws nothing, so it is cut
        // however thin its pieces are
        [Test]
        public void ANeedle_IsCutAtTheCornerOnIt()
        {
            var far = new float3(1.5f, 0, 0);
            var needle = new[] { A, far, B,  A, B, C };
            using var surface = new Surface(needle);
            Assert.That(Split(surface, new float3(0.5f, 0, 0)), Is.True);
            Assert.That(surface.Uses(new float3(0.5f, 0, 0)), Is.True);
            for (int i = 0; i < surface.indices.Length; i += 3)
                Assert.That(surface.indices[i] != surface.indices[i + 1] && surface.indices[i + 1] != surface.indices[i + 2] && surface.indices[i] != surface.indices[i + 2], Is.True);
        }

        // Only what is drawn shows a crack: an edge only a surface of the collider shares is still open
        [Test]
        public void AnEdgeOnlyAHiddenSurfaceShares_IsStillSplit()
        {
            var hidden   = new[] { B, A, new float3(1, -1, 0) };
            var touching = new[] { Bottom, new float3(1, 0, -1), new float3(2, 0, -1) };
            var source = Brush(-1, 1, kSquare, hidden, touching);
            try
            {
                var result = OutputMeshCleanup.Clean(source);
                Assert.That(result.IsCreated, Is.True);
                try
                {
                    ref var surface = ref result.Value.surfaces[0];
                    var found = false;
                    for (int i = 0; i < surface.indices.Length; i++)
                        found |= math.all(surface.colliderVertices[surface.indices[i]] == Bottom);
                    Assert.That(found, Is.True);
                    Assert.That(result.Value.surfaces[1].indexCount, Is.EqualTo(3), "the hidden surface is left as it is");
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

        // Corners of two surfaces a hair apart (brushes a fraction of a millimetre off the grid) are one: the least of
        // them, so each surface picks the same one on its own
        [Test]
        public void CornersAHairApart_BecomeOne()
        {
            var hairOff  = new float3(2.00002f, 0, 0);
            var touching = new[] { hairOff, new float3(2.00002f, 0, -1), new float3(3, 0, -1) };
            var source = Brush(-1, kSquare, touching);
            try
            {
                var result = OutputMeshCleanup.Clean(source);
                Assert.That(result.IsCreated, Is.True);
                try
                {
                    ref var moved = ref result.Value.surfaces[1];
                    var atB = false; var atHair = false;
                    for (int i = 0; i < moved.indices.Length; i++)
                    {
                        var p = moved.colliderVertices[moved.indices[i]];
                        atB    |= math.all(p == B);
                        atHair |= math.all(p == hairOff);
                    }
                    Assert.That(atB, Is.True, "the corner a hair off moved onto the other");
                    Assert.That(atHair, Is.False);
                    ref var square = ref result.Value.surfaces[0];
                    for (int i = 0; i < square.indices.Length; i++)
                        Assert.That(math.all(square.colliderVertices[square.indices[i]] == hairOff), Is.False, "the least corner stays where it is");
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

        // Across brushes: a brush's surface takes the corner of a surface of the brush next to it
        [Test]
        public void ABrushsOutline_TakesTheCornerOfTheBrushNextToIt()
        {
            var touching = new[] { Bottom, new float3(1, 0, -1), new float3(2, 0, -1) };
            var first  = Brush(-1, kSquare);
            var second = Brush(-1, touching);
            var brushes = new NativeList<BrushData>(Allocator.Temp);
            var work    = new NativeList<MeshCleanupWork>(Allocator.Temp);
            var patched = new NativeList<BlobAssetReference<ChiselBrushRenderBuffer>>(Allocator.Temp);
            var neighbours = new NativeList<int>(Allocator.Temp);
            try
            {
                brushes.Add(new BrushData { brushRenderBuffer = first });
                brushes.Add(new BrushData { brushRenderBuffer = second });
                OutputMeshCleanup.Plan(brushes, default, work, neighbours);
                Assert.That(work.Length, Is.EqualTo(2));
                Assert.That(work[0].neighbourCount, Is.EqualTo(1), "they touch");
                for (int i = 0; i < work.Length; i++)
                    work[i] = OutputMeshCleanup.CleanPlanned(work[i], brushes.AsArray(), neighbours.AsArray());
                OutputMeshCleanup.Finish(brushes, patched, work, default);
                ref var surface = ref brushes[0].brushRenderBuffer.Value.surfaces[0];
                var found = false;
                for (int i = 0; i < surface.indices.Length; i++)
                    found |= math.all(surface.colliderVertices[surface.indices[i]] == Bottom);
                Assert.That(found, Is.True, "the corner of the brush next to it is on its outline");
                // the collider is split there too (what it draws, or the split triangles before the cleanup)
                ref var colliderIndices  = ref (surface.physicsIndices.Length > 0 ? ref surface.physicsIndices  : ref surface.indices);
                ref var colliderVertices = ref (surface.physicsIndices.Length > 0 ? ref surface.physicsVertices : ref surface.colliderVertices);
                var inCollider = false;
                for (int i = 0; i < colliderIndices.Length; i++)
                    inCollider |= math.all(colliderVertices[colliderIndices[i]] == Bottom);
                Assert.That(inCollider, Is.True, "the collider has the corner too");
            }
            finally
            {
                foreach (var result in patched)
                    result.Dispose();
                brushes.Dispose(); work.Dispose(); patched.Dispose(); neighbours.Dispose();
                first.Dispose(); second.Dispose();
            }
        }

        // A brush's buffer of the given surfaces, each as Surface makes it
        static BlobAssetReference<ChiselBrushRenderBuffer> Brush(int paintedSurface, params float3[][] surfaces)
        {
            return Brush(paintedSurface, -1, surfaces);
        }

        // The same, with one surface only in the collider (hiddenSurface)
        static BlobAssetReference<ChiselBrushRenderBuffer> Brush(int paintedSurface, int hiddenSurface, params float3[][] surfaces)
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
                                         s == hiddenSurface ? SurfaceDestinationFlags.Collidable
                                                            : SurfaceDestinationFlags.Renderable | SurfaceDestinationFlags.Collidable, default, default);
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
            var neighbours = new NativeList<int>(Allocator.Temp);
            try
            {
                brushes.Add(new BrushData { brushRenderBuffer = source });
                OutputMeshCleanup.Plan(brushes, cache, work, neighbours);
                Assert.That(work.Length, Is.EqualTo(1));
                for (int i = 0; i < work.Length; i++)
                    work[i] = OutputMeshCleanup.CleanPlanned(work[i], brushes.AsArray(), neighbours.AsArray());
                OutputMeshCleanup.Finish(brushes, patched, work, cache);
                var first = brushes[0].brushRenderBuffer;
                Assert.That(first, Is.Not.EqualTo(source));

                brushes[0] = new BrushData { brushRenderBuffer = source };
                OutputMeshCleanup.Plan(brushes, cache, work, neighbours);
                Assert.That(work.Length, Is.EqualTo(0), "nothing to do again");
                Assert.That(brushes[0].brushRenderBuffer, Is.EqualTo(first));
                OutputMeshCleanup.Finish(brushes, patched, work, cache);

                brushes.Clear();
                OutputMeshCleanup.Plan(brushes, cache, work, neighbours);
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
                neighbours.Dispose();
                source.Dispose();
            }
        }
    }
}
