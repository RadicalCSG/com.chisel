using System.Collections.Generic;
using NUnit.Framework;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace Chisel.Core.Tests
{
    // What the hotspot cut made of a brush is kept until the brush's inputs change (OutputHotspotSections.CutModel, cache)
    [TestFixture]
    public class HotspotCutCacheTests
    {
        ChiselHotspotLayout layout;
        NativeList<HotspotTileData> tiles;
        NativeArray<HotspotLayoutData> layouts;
        NativeParallelHashMap<CompactNodeID, HotspotCutEntry> cache;
        readonly List<BlobAssetReference<ChiselBrushRenderBuffer>> buffers = new();

        [SetUp]
        public void SetUp()
        {
            layout = ScriptableObject.CreateInstance<ChiselHotspotLayout>();
            layout.size = new Vector2Int(2048, 2048);
            layout.pixelsPerUnit = 256;
            layout.tiles = new List<HotspotTile>
            {
                new() { id = 1, name = "A1", points = new[] { new Vector2Int(0, 0), new Vector2Int(512, 0), new Vector2Int(512, 512), new Vector2Int(0, 512) } },
                new() { id = 2, name = "B1", points = new[] { new Vector2Int(1024, 0), new Vector2Int(2048, 0), new Vector2Int(2048, 512), new Vector2Int(1024, 512) } },
            };
            tiles = new NativeList<HotspotTileData>(Allocator.Temp);
            HotspotMatcher.AddTiles(layout, tiles, out var data);
            layouts = new NativeArray<HotspotLayoutData>(new[] { data }, Allocator.Temp);
            cache = new NativeParallelHashMap<CompactNodeID, HotspotCutEntry>(8, Allocator.Temp);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var entry in cache)
                if (entry.Value.result.IsCreated) entry.Value.result.Dispose();
            cache.Dispose();
            foreach (var buffer in buffers)
                if (buffer.IsCreated) buffer.Dispose();
            buffers.Clear();
            tiles.Dispose();
            layouts.Dispose();
            Object.DestroyImmediate(layout);
        }

        // A brush's render buffer with one square surface, x from x0 to x0 + 2 and y from 0 to 2, facing -z
        BlobAssetReference<ChiselBrushRenderBuffer> Square(float x0, float density = 1)
        {
            var builder = new BlobBuilder(Allocator.Temp);
            ref var root = ref builder.ConstructRoot<ChiselBrushRenderBuffer>();
            var surfaces = builder.Allocate(ref root.surfaces, 1);
            builder.Allocate(ref root.querySurfaces, 0);
            using var indices  = new NativeList<int>(Allocator.Temp) { 0, 2, 1, 0, 3, 2 };
            using var collider = new NativeList<float3>(Allocator.Temp);
            using var select   = new NativeList<SelectVertex>(Allocator.Temp);
            using var render   = new NativeList<RenderVertex>(Allocator.Temp);
            foreach (var p in new[] { new float3(x0, 0, 0), new float3(x0 + 2, 0, 0), new float3(x0 + 2, 2, 0), new float3(x0, 2, 0) })
            {
                collider.Add(p);
                select.Add(new SelectVertex { position = p });
                render.Add(new RenderVertex { position = p, normal = new float3(0, 0, -1), tangent = new float4(1, 0, 0, -1) });
            }
            surfaces[0].Store(builder, indices, collider, select, render);
            surfaces[0].hotspotSource = new HotspotSurfaceSource { layout = 1, right = new float3(1, 0, 0), up = new float3(0, 1, 0), normal = new float3(0, 0, -1), density = density };
            root.surfaceCount = 1;
            var buffer = builder.CreateBlobAssetReference<ChiselBrushRenderBuffer>(Allocator.Persistent);
            builder.Dispose();
            buffers.Add(buffer);
            return buffer;
        }

        // The same square without a hotspot, so it is not cut
        BlobAssetReference<ChiselBrushRenderBuffer> Plain(float x0)
        {
            var buffer = Square(x0);
            buffer.Value.surfaces[0].hotspotSource = default;
            return buffer;
        }

        static CompactNodeID BrushID(int b) => new CompactNodeID(default, new SlotIndex { index = b + 1, generation = 1 });

        NativeList<BrushData> Brushes(params BlobAssetReference<ChiselBrushRenderBuffer>[] squares)
        {
            var brushes = new NativeList<BrushData>(Allocator.Temp);
            for (int b = 0; b < squares.Length; b++)
                brushes.Add(new BrushData { brushRenderBuffer = squares[b], brushSurfaceCount = 1,
                                            brushIndexOrder = new IndexOrder { compactNodeID = BrushID(b), nodeOrder = b } });
            return brushes;
        }

        // The buffers the cut hands on, one per brush; the ones it made without keeping them are ours to free
        BlobAssetReference<ChiselBrushRenderBuffer>[] Run(bool cached, params BlobAssetReference<ChiselBrushRenderBuffer>[] squares)
        {
            using var brushes = Brushes(squares);
            using var patched = new NativeList<BlobAssetReference<ChiselBrushRenderBuffer>>(Allocator.Temp);
            if (cached)
                OutputHotspotSections.CutModel(brushes, patched, tiles.AsArray(), layouts, default, default, default, default, cache);
            else
                OutputHotspotSections.CutModel(brushes, patched, tiles.AsArray(), layouts, default);
            foreach (var buffer in patched) buffers.Add(buffer);
            var result = new BlobAssetReference<ChiselBrushRenderBuffer>[squares.Length];
            for (int b = 0; b < squares.Length; b++)
                result[b] = brushes[b].brushRenderBuffer;
            return result;
        }

        // Where the buffers are, taken right away: a buffer the cache replaced is freed
        static unsafe long[] Ptrs(BlobAssetReference<ChiselBrushRenderBuffer>[] buffers)
        {
            var ptrs = new long[buffers.Length];
            for (int i = 0; i < ptrs.Length; i++)
                ptrs[i] = (long)buffers[i].GetUnsafePtr();
            return ptrs;
        }

        static float2[] UVs(BlobAssetReference<ChiselBrushRenderBuffer> buffer)
        {
            ref var surface = ref buffer.Value.surfaces[0];
            var uvs = new float2[surface.renderVertices.Length];
            for (int i = 0; i < uvs.Length; i++)
                uvs[i] = surface.renderVertices[i].uv0;
            return uvs;
        }

        void BorderOnB1(int pixels)
        {
            var b1 = layout.tiles[1];
            b1.border = pixels;
            layout.tiles[1] = b1;
            tiles.Clear();
            HotspotMatcher.AddTiles(layout, tiles, out var data);
            layouts[0] = data;
        }

        [Test]
        public void TheSameInputs_GiveTheSameBufferAgain()
        {
            var squares = new[] { Square(-2), Square(0) };
            var first = Ptrs(Run(true, squares));
            var second = Ptrs(Run(true, squares));
            Assert.That(cache.Count(), Is.EqualTo(2));
            Assert.That(second, Is.EqualTo(first), "cut again");
        }

        [Test]
        public void WhatIsKept_IsWhatTheCutMakesWithoutACache()
        {
            var squares = new[] { Square(-2), Square(0) };
            var plain = Run(false, squares);
            var kept = Run(true, squares);
            var again = Run(true, squares);
            for (int b = 0; b < 2; b++)
            {
                Assert.That(UVs(kept[b]), Is.EqualTo(UVs(plain[b])), $"brush {b} the first time");
                Assert.That(UVs(again[b]), Is.EqualTo(UVs(plain[b])), $"brush {b} reused");
            }
        }

        [Test]
        public void ABrushThatChanged_IsCutAgainWithItsGroupAndNotTheOtherGroups()
        {
            var first = Ptrs(Run(true, Square(-2), Square(0), Square(10), Square(12)));
            var second = Ptrs(Run(true, Square(-2, 2), buffers[1], buffers[2], buffers[3]));
            Assert.That(second[0], Is.Not.EqualTo(first[0]), "the changed one");
            Assert.That(second[1], Is.Not.EqualTo(first[1]), "its neighbour, mapped with it");
            Assert.That(second[2], Is.EqualTo(first[2]), "the other group");
            Assert.That(second[3], Is.EqualTo(first[3]), "the other group");
            Assert.That(cache.Count(), Is.EqualTo(4));
        }

        [Test]
        public void ALayoutThatChanged_CutsEveryBrushAgain()
        {
            var squares = new[] { Square(-2), Square(0) };
            var first = Ptrs(Run(true, squares));
            BorderOnB1(32);
            var secondBuffers = Run(true, squares);
            var second = Ptrs(secondBuffers);
            var plain = Run(false, squares);
            for (int b = 0; b < 2; b++)
            {
                Assert.That(second[b], Is.Not.EqualTo(first[b]), $"brush {b}");
                Assert.That(UVs(secondBuffers[b]), Is.EqualTo(UVs(plain[b])), $"brush {b} matches an uncached cut");
            }
        }

        [Test]
        public void ABrushThatIsGone_LeavesNothingKept()
        {
            var squares = new[] { Square(-2), Square(0) };
            Run(true, squares);
            Assert.That(cache.Count(), Is.EqualTo(2));
            Run(true, squares[0], Plain(5));
            Assert.That(cache.Count(), Is.Zero, "a lone square is not cut, and the other brush is gone");
        }

        [Test]
        public unsafe void ABrushThatIsNotCutAnyMore_LeavesNothingKept()
        {
            Run(true, Square(-2), Square(0));
            Assert.That(cache.Count(), Is.EqualTo(2));
            var plain = Plain(0);
            var result = Run(true, buffers[0], plain);
            Assert.That(cache.Count(), Is.Zero, "nothing left to group");
            Assert.That((long)result[1].GetUnsafePtr(), Is.EqualTo((long)plain.GetUnsafePtr()), "handed on as it was");
        }

        [Test]
        public void KeptBuffers_AreNotAlsoHandedBackToBeFreed()
        {
            using var brushes = Brushes(Square(-2), Square(0));
            using var patched = new NativeList<BlobAssetReference<ChiselBrushRenderBuffer>>(Allocator.Temp);
            OutputHotspotSections.CutModel(brushes, patched, tiles.AsArray(), layouts, default, default, default, default, cache);
            Assert.That(patched.Length, Is.Zero, "the cache owns them");
            Assert.That(cache.Count(), Is.EqualTo(2));
        }
    }
}
