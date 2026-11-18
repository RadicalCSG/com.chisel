using NUnit.Framework;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Chisel.Core.Tests
{
    // Vertices at one position that are smoothed together get one normal (OutputSmoothNormals)
    [TestFixture]
    public class OutputSmoothNormalsTests
    {
        static readonly float3 kShared = new(0, 0, 0);

        // A brush of one triangle, a corner at kShared, every vertex with the given normal; noSmoothingGroups: what its buffer
        // says (ChiselBrushRenderBuffer.noSmoothingGroups)
        static BlobAssetReference<ChiselBrushRenderBuffer> Brush(ulong entity, uint group, float3 normal, float3 b, float3 c, bool noSmoothingGroups = false)
        {
            var builder = new BlobBuilder(Allocator.Temp);
            try
            {
                ref var root  = ref builder.ConstructRoot<ChiselBrushRenderBuffer>();
                root.entityID = entity;
                root.noSmoothingGroups = noSmoothingGroups;
                var buffers   = builder.Allocate(ref root.surfaces, 1);
                using var indices  = new NativeList<int>(Allocator.Temp) { 0, 1, 2 };
                using var collider = new NativeList<float3>(Allocator.Temp) { kShared, b, c };
                using var select   = new NativeList<SelectVertex>(Allocator.Temp);
                using var render   = new NativeList<RenderVertex>(Allocator.Temp);
                foreach (var position in collider)
                {
                    select.Add(new SelectVertex { position = position });
                    render.Add(new RenderVertex { position = position, normal = normal, tangent = new float4(1, 0, 0, 1) });
                }
                buffers[0].Construct(builder, indices, collider, select, render, 0, 0,
                                     SurfaceDestinationFlags.Renderable | SurfaceDestinationFlags.Collidable, default, default);
                buffers[0].smoothingGroup = group;
                var queries = builder.Allocate(ref root.querySurfaces, 1);
                using var list = new NativeList<ChiselQuerySurface>(Allocator.Temp)
                {
                    new ChiselQuerySurface { surfaceIndex = 0, vertexCount = buffers[0].vertexCount, indexCount = buffers[0].indexCount,
                                             geometryHashValue = buffers[0].geometryHashValue, surfaceHashValue = buffers[0].surfaceHashValue }
                };
                builder.Construct(ref queries[0].surfaces, list);
                root.surfaceCount = 1;
                return builder.CreateBlobAssetReference<ChiselBrushRenderBuffer>(Allocator.Persistent);
            }
            finally
            {
                builder.Dispose();
            }
        }

        static float3 NormalAtShared(BlobAssetReference<ChiselBrushRenderBuffer> buffer)
        {
            ref var vertices = ref buffer.Value.surfaces[0].renderVertices;
            for (int v = 0; v < vertices.Length; v++)
            {
                if (math.all(vertices[v].position == kShared))
                    return vertices[v].normal;
            }
            Assert.Fail("no vertex at the shared position");
            return default;
        }

        // Without a smoothing angle, a brush whose buffer says none of its surfaces has a smoothing group is not gone
        // through. Planted: one that does have one, beside one it would be smoothed with
        [Test]
        public void ABrushWhoseBufferSaysItHasNoSmoothingGroups_IsNotGoneThrough()
        {
            var left    = math.normalize(new float3(-0.2f, 1, 0));
            var right   = math.normalize(new float3(0.2f, 1, 0));
            var a       = Brush(7, 1, left,  new float3(-1, 0.2f, 1), new float3(-1, 0.2f, -1), noSmoothingGroups: true);
            var b       = Brush(7, 1, right, new float3(1, 0.2f, -1), new float3(1, 0.2f, 1));
            var brushes = new NativeList<BrushData>(Allocator.Temp);
            var patched = new NativeList<BlobAssetReference<ChiselBrushRenderBuffer>>(Allocator.Temp);
            try
            {
                brushes.Add(new BrushData { brushRenderBuffer = a });
                brushes.Add(new BrushData { brushRenderBuffer = b });
                OutputSmoothNormals.Smooth(brushes, patched, 0);
                Assert.That(brushes[0].brushRenderBuffer, Is.EqualTo(a), "not gone through");
                Assert.That(brushes[1].brushRenderBuffer, Is.EqualTo(b), "nothing of it to be smoothed with");
                Assert.That(patched.Length, Is.Zero);
            }
            finally
            {
                foreach (var buffer in patched) buffer.Dispose();
                brushes.Dispose(); patched.Dispose(); a.Dispose(); b.Dispose();
            }
        }

        // The CSG says when none of a brush's surfaces has a smoothing group, and only then
        [Test]
        public void TheBufferOfABrush_SaysWhetherItHasNoSmoothingGroups([Values(0u, 3u)] uint smoothingGroup)
        {
            var box = ContentsSceneNode.Brush(ContentsScene.BoxPlanes(new float3(0), new float3(2)), name: "box");
            box.smoothingGroup = smoothingGroup;
            using var harness = ContentsTreeHarness.Build(new ContentsScene().Add(box));
            Assert.That(harness.Update(), Is.True);
            var cache = ChiselTreeLookup.Value[harness.Tree].brushRenderBufferCache;
            var buffers = 0;
            for (int i = 0; i < cache.Length; i++)
            {
                if (!cache[i].IsCreated)
                    continue;
                buffers++;
                Assert.That(cache[i].Value.noSmoothingGroups, Is.EqualTo(smoothingGroup == 0));
            }
            Assert.That(buffers, Is.EqualTo(1));
        }

        [Test]
        public void VerticesAtOnePosition_SmoothedTogether_GetOneNormal_AndOthersKeepTheirs()
        {
            // two faces of one generator in one group, tilted apart, and one of another group
            var left    = math.normalize(new float3(-0.2f, 1, 0));
            var right   = math.normalize(new float3(0.2f, 1, 0));
            var a       = Brush(7, 1, left,  new float3(-1, 0.2f, 1), new float3(-1, 0.2f, -1));
            var b       = Brush(7, 1, right, new float3(1, 0.2f, -1), new float3(1, 0.2f, 1));
            var other   = Brush(7, 2, left,  new float3(0, 0, 1), new float3(-1, 0.2f, 1));
            var brushes = new NativeList<BrushData>(Allocator.Temp);
            var patched = new NativeList<BlobAssetReference<ChiselBrushRenderBuffer>>(Allocator.Temp);
            try
            {
                brushes.Add(new BrushData { brushRenderBuffer = a });
                brushes.Add(new BrushData { brushRenderBuffer = b });
                brushes.Add(new BrushData { brushRenderBuffer = other });
                OutputSmoothNormals.Smooth(brushes, patched, 0);
                var na = NormalAtShared(brushes[0].brushRenderBuffer);
                var nb = NormalAtShared(brushes[1].brushRenderBuffer);
                Assert.That(math.dot(na, nb), Is.GreaterThan(0.99999f), $"one normal where they meet: {na} and {nb}");
                Assert.That(math.dot(na, new float3(0, 1, 0)), Is.GreaterThan(0.99999f), "their average");
                Assert.That(math.dot(NormalAtShared(brushes[2].brushRenderBuffer), left), Is.GreaterThan(0.99999f), "another group keeps its own");
            }
            finally
            {
                foreach (var result in patched)
                    result.Dispose();
                brushes.Dispose(); patched.Dispose();
                a.Dispose(); b.Dispose(); other.Dispose();
            }
        }
    }
}
