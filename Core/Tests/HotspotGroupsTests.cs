using System.Collections.Generic;
using NUnit.Framework;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace Chisel.Core.Tests
{
    // Coplanar neighbouring surfaces of different brushes mapped as one (HotspotGroups, OutputHotspotSections.CutModel)
    [TestFixture]
    public class HotspotGroupsTests
    {
        // A1 (512 x 512 at the top left) and B1 (1024 x 512 at the top right) of a 2048 sheet, 256 pixels a unit
        ChiselHotspotLayout layout;
        NativeList<HotspotTileData> tiles;
        NativeArray<HotspotLayoutData> layouts;
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
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var buffer in buffers)
                if (buffer.IsCreated) buffer.Dispose();
            buffers.Clear();
            tiles.Dispose();
            layouts.Dispose();
            Object.DestroyImmediate(layout);
        }

        // A brush's render buffer with one quad surface (corners counter-clockwise seen from in front)
        BlobAssetReference<ChiselBrushRenderBuffer> Quad(float3 normal, float3 right, params float3[] corners)
        {
            var builder = new BlobBuilder(Allocator.Temp);
            ref var root = ref builder.ConstructRoot<ChiselBrushRenderBuffer>();
            var surfaces = builder.Allocate(ref root.surfaces, 1);
            builder.Allocate(ref root.querySurfaces, 0);
            using var indices  = new NativeList<int>(Allocator.Temp) { 0, 2, 1, 0, 3, 2 };
            using var collider = new NativeList<float3>(Allocator.Temp);
            using var select   = new NativeList<SelectVertex>(Allocator.Temp);
            using var render   = new NativeList<RenderVertex>(Allocator.Temp);
            foreach (var p in corners)
            {
                collider.Add(p);
                select.Add(new SelectVertex { position = p });
                render.Add(new RenderVertex { position = p, normal = normal, tangent = new float4(right, -1) });
            }
            surfaces[0].Store(builder, indices, collider, select, render);
            surfaces[0].hotspotSource = new HotspotSurfaceSource { layout = 1, right = right, up = new float3(0, 1, 0), normal = normal, density = 1 };
            root.surfaceCount = 1;
            var buffer = builder.CreateBlobAssetReference<ChiselBrushRenderBuffer>(Allocator.Persistent);
            builder.Dispose();
            buffers.Add(buffer);
            return buffer;
        }

        // A brush's render buffer with one square surface, x from x0 to x0 + 2 and y from 0 to 2, facing -z
        BlobAssetReference<ChiselBrushRenderBuffer> Square(float x0, float3 right)
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
            surfaces[0].hotspotSource = new HotspotSurfaceSource { layout = 1, right = right, up = new float3(0, 1, 0), normal = new float3(0, 0, -1), density = 1 };
            root.surfaceCount = 1;
            var buffer = builder.CreateBlobAssetReference<ChiselBrushRenderBuffer>(Allocator.Persistent);
            builder.Dispose();
            buffers.Add(buffer);
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

        int GroupsOf(NativeList<BrushData> brushes, params HotspotLink[] links)
        {
            var groupOf = new UnsafeHashMap<int2, int>(4, Allocator.Temp);
            var unfoldOf = new UnsafeHashMap<int2, float4x4>(4, Allocator.Temp);
            using var groups = new NativeList<HotspotGroups.Group>(Allocator.Temp);
            using var lines = new NativeList<HotspotSectionLine>(Allocator.Temp);
            using var halfPlanes = new NativeList<HotspotSectionHalfPlane>(Allocator.Temp);
            using var cells = new NativeList<HotspotSectionCell>(Allocator.Temp);
            using var linkArray = new NativeArray<HotspotLink>(links, Allocator.Temp);
            HotspotGroups.Find(brushes, tiles.AsArray(), layouts, linkArray, default, ref groupOf, ref unfoldOf, groups, lines, halfPlanes, cells);
            groupOf.Dispose();
            unfoldOf.Dispose();
            return groups.Length;
        }

        static HotspotLink Link(int order, bool connect) =>
            new() { brushA = BrushID(0), surfaceA = 0, brushB = BrushID(1), surfaceB = 0, order = order, connection = connect ? 1 : -1 };

        // B1 with a border this many pixels wide
        void BorderOnB1(int pixels)
        {
            var b1 = layout.tiles[1];
            b1.border = pixels;
            layout.tiles[1] = b1;
            tiles.Clear();
            HotspotMatcher.AddTiles(layout, tiles, out var data);
            layouts[0] = data;
        }

        static List<float2> UVsAt(ref ChiselSurfaceRenderBuffer surface, float3 position)
        {
            var uvs = new List<float2>();
            for (int i = 0; i < surface.renderVertices.Length; i++)
                if (math.distance(surface.renderVertices[i].position, position) < 1e-5f)
                    uvs.Add(surface.renderVertices[i].uv0);
            return uvs;
        }

        [Test]
        public void TwoCoplanarSquares_WithTheBorderShownByHand_HaveABorderWhereTheyMeet()
        {
            BorderOnB1(32);
            using var brushes = Brushes(Square(-2, new float3(1, 0, 0)), Square(0, new float3(1, 0, 0)));
            using var patched = new NativeList<BlobAssetReference<ChiselBrushRenderBuffer>>(Allocator.Temp);
            using var links = new NativeArray<HotspotLink>(new[] { new HotspotLink { brushA = BrushID(0), brushB = BrushID(1), order = 1, border = 1 } }, Allocator.Temp);
            OutputHotspotSections.CutModel(brushes, patched, tiles.AsArray(), layouts, links);
            foreach (var buffer in patched) buffers.Add(buffer);
            // still one 4 x 2 surface on B1, but where they meet the left one has the tile's right side and the right one its
            // left side (without the link both have the interior there, u 0.75)
            var left  = UVsAt(ref brushes[0].brushRenderBuffer.Value.surfaces[0], new float3(0, 0.125f, 0));
            var right = UVsAt(ref brushes[1].brushRenderBuffer.Value.surfaces[0], new float3(0, 0.125f, 0));
            Assert.That(left.Count, Is.GreaterThan(0), "the left one has a vertex there");
            Assert.That(right.Count, Is.GreaterThan(0), "the right one has a vertex there");
            foreach (var uv in left)
                Assert.That(uv.x, Is.EqualTo(1).Within(1e-5f), $"the left one: {uv}");
            foreach (var uv in right)
                Assert.That(uv.x, Is.EqualTo(0.5f).Within(1e-5f), $"the right one: {uv}");
        }

        [Test]
        public void TheBrushesOwnBuffers_AreToldTheGroupTheirSurfacesWereMappedWith()
        {
            BorderOnB1(32);
            using var brushes = Brushes(Square(-2, new float3(1, 0, 0)), Square(0, new float3(1, 0, 0)), Square(5, new float3(1, 0, 0)));
            using var patched = new NativeList<BlobAssetReference<ChiselBrushRenderBuffer>>(Allocator.Temp);
            var own = new BlobAssetReference<ChiselBrushRenderBuffer>[brushes.Length];
            for (int b = 0; b < brushes.Length; b++)
                own[b] = brushes[b].brushRenderBuffer;
            using var kept = new NativeArray<BlobAssetReference<ChiselBrushRenderBuffer>>(own, Allocator.Temp);
            OutputHotspotSections.CutModel(brushes, patched, tiles.AsArray(), layouts, default, default, kept);
            foreach (var buffer in patched) buffers.Add(buffer);
            var a = kept[0].Value.surfaces[0].hotspot.group;
            Assert.That(a, Is.GreaterThan(0), "the touching squares are a group");
            Assert.That(kept[1].Value.surfaces[0].hotspot.group, Is.EqualTo(a), "the same one");
            Assert.That(kept[2].Value.surfaces[0].hotspot.group, Is.Zero, "the one apart is by itself");
        }

        [Test]
        public void TwoCoplanarSquares_WithoutALink_HaveNoBorderWhereTheyMeet()
        {
            BorderOnB1(32);
            using var brushes = Brushes(Square(-2, new float3(1, 0, 0)), Square(0, new float3(1, 0, 0)));
            using var patched = new NativeList<BlobAssetReference<ChiselBrushRenderBuffer>>(Allocator.Temp);
            OutputHotspotSections.CutModel(brushes, patched, tiles.AsArray(), layouts, default);
            foreach (var buffer in patched) buffers.Add(buffer);
            foreach (var uv in UVsAt(ref brushes[0].brushRenderBuffer.Value.surfaces[0], new float3(0, 1, 0)))
                Assert.That(uv.x, Is.EqualTo(0.75f).Within(1e-5f), $"the left one: {uv}");
        }

        [Test]
        public void TwoSquaresRoundACorner_ConnectedByHand_HaveNoBorderAlongTheFold()
        {
            BorderOnB1(32);
            var front = Quad(new float3(0, 0, -1), new float3(1, 0, 0), new float3(-2, 0, 0), new float3(0, 0, 0), new float3(0, 2, 0), new float3(-2, 2, 0));
            var side  = Quad(new float3(1, 0, 0), new float3(0, 0, 1), new float3(0, 0, 0), new float3(0, 0, 2), new float3(0, 2, 2), new float3(0, 2, 0));
            using var brushes = Brushes(front, side);
            using var patched = new NativeList<BlobAssetReference<ChiselBrushRenderBuffer>>(Allocator.Temp);
            using var links = new NativeArray<HotspotLink>(new[] { Link(1, true) }, Allocator.Temp);
            OutputHotspotSections.CutModel(brushes, patched, tiles.AsArray(), layouts, links);
            foreach (var buffer in patched) buffers.Add(buffer);
            // along the fold, halfway up, both faces have the interior's u (the middle of B1), not a side of the tile
            foreach (var uv in UVsAt(ref brushes[0].brushRenderBuffer.Value.surfaces[0], new float3(0, 0.125f, 0)))
                Assert.That(uv.x, Is.EqualTo(0.75f).Within(1e-4f), $"the front: {uv}");
            foreach (var uv in UVsAt(ref brushes[1].brushRenderBuffer.Value.surfaces[0], new float3(0, 0.125f, 0)))
                Assert.That(uv.x, Is.EqualTo(0.75f).Within(1e-4f), $"the side: {uv}");
        }

        [Test]
        public void TwoCoplanarSquaresInDifferentFrames_ConnectedByHand_AreOneGroup()
        {
            using var brushes = Brushes(Square(-2, new float3(1, 0, 0)), Square(0, new float3(-1, 0, 0)));
            Assert.That(GroupsOf(brushes, Link(1, true)), Is.EqualTo(1));
        }

        [Test]
        public void TwoCoplanarSquaresSharingAnEdge_DisconnectedByHand_AreNoGroup()
        {
            using var brushes = Brushes(Square(-2, new float3(1, 0, 0)), Square(0, new float3(1, 0, 0)));
            Assert.That(GroupsOf(brushes, Link(1, false)), Is.EqualTo(0));
        }

        [Test]
        public void TheNewestLink_Counts()
        {
            using var brushes = Brushes(Square(-2, new float3(1, 0, 0)), Square(0, new float3(1, 0, 0)));
            Assert.That(GroupsOf(brushes, Link(1, true), Link(2, false)), Is.EqualTo(0), "disconnected last");
            Assert.That(GroupsOf(brushes, Link(2, true), Link(1, false)), Is.EqualTo(1), "connected last");
        }

        [Test]
        public void TwoSquaresRoundACorner_ConnectedByHand_WrapOneTileRoundIt()
        {
            // a box's front (x -2..0, facing -z) and its right side (z 0..2, facing +x), sharing the edge x = 0, z = 0
            var front = Quad(new float3(0, 0, -1), new float3(1, 0, 0), new float3(-2, 0, 0), new float3(0, 0, 0), new float3(0, 2, 0), new float3(-2, 2, 0));
            var side  = Quad(new float3(1, 0, 0), new float3(0, 0, 1), new float3(0, 0, 0), new float3(0, 0, 2), new float3(0, 2, 2), new float3(0, 2, 0));
            using var brushes = Brushes(front, side);
            using var patched = new NativeList<BlobAssetReference<ChiselBrushRenderBuffer>>(Allocator.Temp);
            using var links = new NativeArray<HotspotLink>(new[] { Link(1, true) }, Allocator.Temp);
            OutputHotspotSections.CutModel(brushes, patched, tiles.AsArray(), layouts, links);
            foreach (var buffer in patched) buffers.Add(buffer);
            // unfolded, 4 x 2: B1, u 0.5..1, v 0.75..1; the side continues where the front ends
            AssertUV(new float2(0.5f, 0.75f),  UVAt(ref brushes[0].brushRenderBuffer.Value.surfaces[0], new float3(-2, 0, 0)), "the front's bottom left");
            AssertUV(new float2(0.75f, 0.75f), UVAt(ref brushes[0].brushRenderBuffer.Value.surfaces[0], new float3(0, 0, 0)), "the front's bottom right");
            AssertUV(new float2(0.75f, 0.75f), UVAt(ref brushes[1].brushRenderBuffer.Value.surfaces[0], new float3(0, 0, 0)), "the side's bottom left");
            AssertUV(new float2(1, 1),         UVAt(ref brushes[1].brushRenderBuffer.Value.surfaces[0], new float3(0, 2, 2)), "the side's top right");
            // and its corners where they were
            ref var sideSurface = ref brushes[1].brushRenderBuffer.Value.surfaces[0];
            for (int v = 0; v < sideSurface.renderVertices.Length; v++)
                Assert.That(sideSurface.renderVertices[v].position.x, Is.EqualTo(0), $"vertex {v} stays on the side");
        }

        // A box's four sides, each 1 x 2, connected round it by hand: front, right, back, left
        static readonly float3[][] kRing =
        {
            new[] { new float3(-0.5f, 0, -0.5f), new float3(0.5f, 0, -0.5f), new float3(0.5f, 2, -0.5f), new float3(-0.5f, 2, -0.5f) },
            new[] { new float3(0.5f, 0, -0.5f), new float3(0.5f, 0, 0.5f), new float3(0.5f, 2, 0.5f), new float3(0.5f, 2, -0.5f) },
            new[] { new float3(0.5f, 0, 0.5f), new float3(-0.5f, 0, 0.5f), new float3(-0.5f, 2, 0.5f), new float3(0.5f, 2, 0.5f) },
            new[] { new float3(-0.5f, 0, 0.5f), new float3(-0.5f, 0, -0.5f), new float3(-0.5f, 2, -0.5f), new float3(-0.5f, 2, 0.5f) },
        };
        static readonly float3[] kRingNormals = { new(0, 0, -1), new(1, 0, 0), new(0, 0, 1), new(-1, 0, 0) };
        static readonly float3[] kRingRights  = { new(1, 0, 0), new(0, 0, 1), new(-1, 0, 0), new(0, 0, -1) };

        [TestCase(0)]
        [TestCase(2)]
        public void ARingOfConnectedSurfaces_KeepsItsSeamAtTheOldestLink(int oldest)
        {
            var sides = new BlobAssetReference<ChiselBrushRenderBuffer>[4];
            for (int i = 0; i < 4; i++)
                sides[i] = Quad(kRingNormals[i], kRingRights[i], kRing[i]);
            using var brushes = Brushes(sides);
            // side i and the next share edge i; the oldest link is edge oldest's
            var links = new HotspotLink[4];
            for (int e = 0, order = 2; e < 4; e++)
                links[e] = new HotspotLink { brushA = BrushID(e), brushB = BrushID((e + 1) % 4), order = e == oldest ? 1 : order++, connection = 1 };
            using var linkArray = new NativeArray<HotspotLink>(links, Allocator.Temp);
            using var patched = new NativeList<BlobAssetReference<ChiselBrushRenderBuffer>>(Allocator.Temp);
            OutputHotspotSections.CutModel(brushes, patched, tiles.AsArray(), layouts, linkArray);
            foreach (var buffer in patched) buffers.Add(buffer);
            for (int e = 0; e < 4; e++)
            {
                // edge e runs up from the second corner of side e (the first of side e + 1)
                var corner = kRing[e][1];
                var a = UVAt(ref brushes[e].brushRenderBuffer.Value.surfaces[0], corner);
                var b = UVAt(ref brushes[(e + 1) % 4].brushRenderBuffer.Value.surfaces[0], corner);
                if (e == oldest)
                    Assert.That(math.distance(a, b), Is.GreaterThan(1e-3f), $"edge {e}, the oldest link: the seam ({a} and {b})");
                else
                    Assert.That(math.distance(a, b), Is.LessThan(1e-5f), $"edge {e}: continuous ({a} and {b})");
            }
        }

        [Test]
        public void TwoSquaresRoundACorner_ByThemselves_AreNoGroup()
        {
            var front = Quad(new float3(0, 0, -1), new float3(1, 0, 0), new float3(-2, 0, 0), new float3(0, 0, 0), new float3(0, 2, 0), new float3(-2, 2, 0));
            var side  = Quad(new float3(1, 0, 0), new float3(0, 0, 1), new float3(0, 0, 0), new float3(0, 0, 2), new float3(0, 2, 2), new float3(0, 2, 0));
            using var brushes = Brushes(front, side);
            Assert.That(GroupsOf(brushes), Is.EqualTo(0));
        }

        [Test]
        public void ALinkBetweenSquaresThatDoNotTouch_DoesNothing()
        {
            using var brushes = Brushes(Square(-3, new float3(1, 0, 0)), Square(0, new float3(1, 0, 0)));
            Assert.That(GroupsOf(brushes, Link(1, true)), Is.EqualTo(0));
        }

        static float2 UVAt(ref ChiselSurfaceRenderBuffer surface, float3 position)
        {
            for (int i = 0; i < surface.renderVertices.Length; i++)
                if (math.distance(surface.renderVertices[i].position, position) < 1e-5f)
                    return surface.renderVertices[i].uv0;
            Assert.Fail($"no vertex at {position}");
            return default;
        }

        [Test]
        public void TwoCoplanarSquaresSharingAnEdge_AreOneGroup()
        {
            using var brushes = Brushes(Square(-2, new float3(1, 0, 0)), Square(0, new float3(1, 0, 0)));
            var groupOf = new UnsafeHashMap<int2, int>(4, Allocator.Temp);
            var unfoldOf = new UnsafeHashMap<int2, float4x4>(4, Allocator.Temp);
            using var groups = new NativeList<HotspotGroups.Group>(Allocator.Temp);
            using var lines = new NativeList<HotspotSectionLine>(Allocator.Temp);
            using var halfPlanes = new NativeList<HotspotSectionHalfPlane>(Allocator.Temp);
            using var cells = new NativeList<HotspotSectionCell>(Allocator.Temp);
            HotspotGroups.Find(brushes, tiles.AsArray(), layouts, default, default, ref groupOf, ref unfoldOf, groups, lines, halfPlanes, cells);
            Assert.That(groups.Length, Is.EqualTo(1), "groups");
            Assert.That(groupOf.ContainsKey(new int2(0, 0)) && groupOf.ContainsKey(new int2(1, 0)), Is.True, "both in it");
            Assert.That(unfoldOf.Count, Is.EqualTo(0), "coplanar: nothing unfolded");
            groupOf.Dispose();
            unfoldOf.Dispose();
        }

        [Test]
        public void TwoCoplanarSquaresSharingAnEdge_TakeOneTileAcrossBoth()
        {
            using var brushes = Brushes(Square(-2, new float3(1, 0, 0)), Square(0, new float3(1, 0, 0)));
            using var patched = new NativeList<BlobAssetReference<ChiselBrushRenderBuffer>>(Allocator.Temp);
            OutputHotspotSections.CutModel(brushes, patched, tiles.AsArray(), layouts, default);
            foreach (var buffer in patched) buffers.Add(buffer);
            // 4 x 2 together: B1, u 0.5..1, v 0.75..1
            AssertUV(new float2(0.5f, 0.75f), UVAt(ref brushes[0].brushRenderBuffer.Value.surfaces[0], new float3(-2, 0, 0)), "bottom left");
            AssertUV(new float2(0.75f, 0.75f), UVAt(ref brushes[0].brushRenderBuffer.Value.surfaces[0], new float3(0, 0, 0)), "the left's bottom right");
            AssertUV(new float2(0.75f, 0.75f), UVAt(ref brushes[1].brushRenderBuffer.Value.surfaces[0], new float3(0, 0, 0)), "the right's bottom left");
            AssertUV(new float2(1, 1), UVAt(ref brushes[1].brushRenderBuffer.Value.surfaces[0], new float3(2, 2, 0)), "top right");
        }

        [Test]
        public void TwoCoplanarSquaresSharingAnEdge_AreMarkedAsOneGroupForTheDiagnostics()
        {
            using var brushes = Brushes(Square(-2, new float3(1, 0, 0)), Square(0, new float3(1, 0, 0)), Square(4, new float3(1, 0, 0)));
            using var patched = new NativeList<BlobAssetReference<ChiselBrushRenderBuffer>>(Allocator.Temp);
            OutputHotspotSections.CutModel(brushes, patched, tiles.AsArray(), layouts, default);
            foreach (var buffer in patched) buffers.Add(buffer);
            var left  = brushes[0].brushRenderBuffer.Value.surfaces[0].hotspot.group;
            var right = brushes[1].brushRenderBuffer.Value.surfaces[0].hotspot.group;
            var apart = brushes[2].brushRenderBuffer.Value.surfaces[0].hotspot.group;
            Assert.That(left, Is.GreaterThan(0), "in a group");
            Assert.That(right, Is.EqualTo(left), "the same one");
            Assert.That(apart, Is.EqualTo(0), "the one that touches neither is by itself");
        }

        [Test]
        public void TwoCoplanarSquaresInDifferentFrames_AreNotConnected()
        {
            using var brushes = Brushes(Square(-2, new float3(1, 0, 0)), Square(0, new float3(-1, 0, 0)));
            var groupOf = new UnsafeHashMap<int2, int>(4, Allocator.Temp);
            var unfoldOf = new UnsafeHashMap<int2, float4x4>(4, Allocator.Temp);
            using var groups = new NativeList<HotspotGroups.Group>(Allocator.Temp);
            using var lines = new NativeList<HotspotSectionLine>(Allocator.Temp);
            using var halfPlanes = new NativeList<HotspotSectionHalfPlane>(Allocator.Temp);
            using var cells = new NativeList<HotspotSectionCell>(Allocator.Temp);
            HotspotGroups.Find(brushes, tiles.AsArray(), layouts, default, default, ref groupOf, ref unfoldOf, groups, lines, halfPlanes, cells);
            Assert.That(groups.Length, Is.EqualTo(0));
            groupOf.Dispose();
            unfoldOf.Dispose();
        }

        static void AssertUV(float2 expected, float2 actual, string what)
        {
            Assert.That(math.distance(expected, actual), Is.LessThan(1e-5f), $"{what}: expected {expected}, got {actual}");
        }

        // The model's groups found and matched with a cache, as the pipeline's jobs do; what each match was made from
        (int groups, int hits, int misses, float2 uv) MatchWithCache(NativeList<BrushData> brushes, NativeParallelHashMap<uint4, GroupMatchEntry> cache)
        {
            var state = FindState.Create(Allocator.Temp);
            var groupOf = new UnsafeHashMap<int2, int>(4, Allocator.Temp);
            var unfoldOf = new UnsafeHashMap<int2, float4x4>(4, Allocator.Temp);
            var bordersOf = new UnsafeParallelMultiHashMap<int2, HotspotBorderCorner>(4, Allocator.Temp);
            using var groups = new NativeList<HotspotGroups.Group>(Allocator.Temp);
            using var lines = new NativeList<HotspotSectionLine>(Allocator.Temp);
            using var halfPlanes = new NativeList<HotspotSectionHalfPlane>(Allocator.Temp);
            using var cells = new NativeList<HotspotSectionCell>(Allocator.Temp);
            using var links = new NativeArray<HotspotLink>(0, Allocator.Temp);
            try
            {
                HotspotGroups.Prepare(brushes, tiles.AsArray(), layouts, links, ref state);
                var inputs = HotspotGroups.InputsKey(tiles.AsArray(), layouts, default);
                int hits = 0, misses = 0;
                for (int i = 0; i < state.matches.Length; i++)
                {
                    var match = HotspotGroups.Match(state.leads[i], brushes.AsArray(), tiles.AsArray(), layouts, default, bordersOf, state, cache, inputs);
                    if (match.owned) misses++; else hits++;
                    state.matches[i] = match;
                }
                HotspotGroups.Assemble(ref state, ref groupOf, ref unfoldOf, groups, lines, halfPlanes, cells, cache);
                var uv = groups.Length > 0 ? new float2(groups[0].uvMatrix.c3.x, groups[0].uvMatrix.c3.y) : float2.zero;
                return (groups.Length, hits, misses, uv);
            }
            finally
            {
                state.Dispose(); groupOf.Dispose(); unfoldOf.Dispose(); bordersOf.Dispose();
            }
        }

        static void DisposeCache(NativeParallelHashMap<uint4, GroupMatchEntry> cache)
        {
            foreach (var entry in cache)
                entry.Value.match.Dispose();
            cache.Dispose();
        }

        [Test]
        public void AGroupMatchedAgain_WithTheSameInputs_IsTakenFromTheCache()
        {
            var cache = new NativeParallelHashMap<uint4, GroupMatchEntry>(4, Allocator.Temp);
            try
            {
                using var brushes = Brushes(Square(-2, new float3(1, 0, 0)), Square(0, new float3(1, 0, 0)), Square(10, new float3(1, 0, 0)), Square(12, new float3(1, 0, 0)));
                var first = MatchWithCache(brushes, cache);
                Assert.That((first.groups, first.hits, first.misses), Is.EqualTo((2, 0, 2)), "the first time every group is matched");
                Assert.That(cache.Count(), Is.EqualTo(2));
                var again = MatchWithCache(brushes, cache);
                Assert.That((again.groups, again.hits, again.misses), Is.EqualTo((2, 2, 0)), "then taken from the cache");
                Assert.That(again.uv, Is.EqualTo(first.uv), "as it was matched");
            }
            finally
            {
                DisposeCache(cache);
            }
        }

        [Test]
        public void AGroupWhoseMemberChanged_IsMatchedAgain_AndTheOtherIsNot()
        {
            var cache = new NativeParallelHashMap<uint4, GroupMatchEntry>(4, Allocator.Temp);
            try
            {
                using (var brushes = Brushes(Square(-2, new float3(1, 0, 0)), Square(0, new float3(1, 0, 0)), Square(10, new float3(1, 0, 0)), Square(12, new float3(1, 0, 0))))
                    MatchWithCache(brushes, cache);
                using (var moved = Brushes(Square(-2.5f, new float3(1, 0, 0)), Square(-0.5f, new float3(1, 0, 0)), Square(10, new float3(1, 0, 0)), Square(12, new float3(1, 0, 0))))
                {
                    var after = MatchWithCache(moved, cache);
                    Assert.That((after.groups, after.hits, after.misses), Is.EqualTo((2, 1, 1)), "only the moved group is matched again");
                }
                Assert.That(cache.Count(), Is.EqualTo(2), "the old match of the moved group is forgotten");
            }
            finally
            {
                DisposeCache(cache);
            }
        }

        [Test]
        public void ALayoutEdit_MatchesEveryGroupAgain()
        {
            var cache = new NativeParallelHashMap<uint4, GroupMatchEntry>(4, Allocator.Temp);
            try
            {
                using var brushes = Brushes(Square(-2, new float3(1, 0, 0)), Square(0, new float3(1, 0, 0)));
                MatchWithCache(brushes, cache);
                BorderOnB1(32);
                var after = MatchWithCache(brushes, cache);
                Assert.That((after.hits, after.misses), Is.EqualTo((0, 1)));
            }
            finally
            {
                DisposeCache(cache);
            }
        }
    }
}
