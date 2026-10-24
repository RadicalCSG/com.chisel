using System.Collections.Generic;
using NUnit.Framework;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace Chisel.Core.Tests
{
    // HotspotGroups.PrepareIncremental groups again only what an edit can have changed; whatever it groups again or keeps,
    // its result must be Prepare's, bit for bit (IncrementalHotspotGroups.md)
    [TestFixture]
    public class HotspotGroupsIncrementalTests
    {
        ChiselHotspotLayout layout;
        NativeList<HotspotTileData> tiles;
        NativeArray<HotspotLayoutData> layouts;
        HotspotGroupMemory memory;
        readonly List<BlobAssetReference<ChiselBrushRenderBuffer>> buffers = new();
        // each brush as the last Update had it: one the same again is kept (HotspotStageMemory.keptKinds)
        readonly Dictionary<int, Brush> last = new();

        // A brush of the test model: its id stays while it is edited
        class Brush
        {
            public int id;
            public float3[] corners;
            public float3 normal, right;
            // no hotspot layout: not a candidate
            public bool plain;
        }

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
            tiles = new NativeList<HotspotTileData>(Allocator.Persistent);
            HotspotMatcher.AddTiles(layout, tiles, out var data);
            layouts = new NativeArray<HotspotLayoutData>(new[] { data }, Allocator.Persistent);
            memory = HotspotGroupMemory.Create(Allocator.Persistent);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var buffer in buffers)
                if (buffer.IsCreated) buffer.Dispose();
            buffers.Clear();
            last.Clear();
            memory.Dispose();
            tiles.Dispose();
            layouts.Dispose();
            Object.DestroyImmediate(layout);
        }

        static CompactNodeID BrushID(int id) => new CompactNodeID(default, new SlotIndex { index = id + 1, generation = 1 });

        BlobAssetReference<ChiselBrushRenderBuffer> Buffer(Brush brush)
        {
            var builder = new BlobBuilder(Allocator.Temp);
            ref var root = ref builder.ConstructRoot<ChiselBrushRenderBuffer>();
            var surfaces = builder.Allocate(ref root.surfaces, 1);
            builder.Allocate(ref root.querySurfaces, 0);
            using var indices  = new NativeList<int>(Allocator.Temp) { 0, 2, 1, 0, 3, 2 };
            using var collider = new NativeList<float3>(Allocator.Temp);
            using var select   = new NativeList<SelectVertex>(Allocator.Temp);
            using var render   = new NativeList<RenderVertex>(Allocator.Temp);
            foreach (var p in brush.corners)
            {
                collider.Add(p);
                select.Add(new SelectVertex { position = p });
                render.Add(new RenderVertex { position = p, normal = brush.normal, tangent = new float4(brush.right, -1) });
            }
            surfaces[0].Store(builder, indices, collider, select, render);
            surfaces[0].hotspotSource = new HotspotSurfaceSource { layout = brush.plain ? 0 : 1, right = brush.right, up = new float3(0, 1, 0), normal = brush.normal, density = 1 };
            root.surfaceCount = 1;
            var buffer = builder.CreateBlobAssetReference<ChiselBrushRenderBuffer>(Allocator.Persistent);
            builder.Dispose();
            buffers.Add(buffer);
            return buffer;
        }

        // A square x from x0 to x0 + width and y from 0 to 2, facing -z (a sliver when width is under a texel)
        static Brush Square(int id, float x0, float width = 2, float3 right = default)
        {
            return new Brush
            {
                id = id, normal = new float3(0, 0, -1), right = math.all(right == 0) ? new float3(1, 0, 0) : right,
                corners = new[] { new float3(x0, 0, 0), new float3(x0 + width, 0, 0), new float3(x0 + width, 2, 0), new float3(x0, 2, 0) }
            };
        }

        // A square round the corner at x: in the plane x, z from 0 to 2, facing -x
        static Brush Side(int id, float x)
        {
            return new Brush
            {
                id = id, normal = new float3(-1, 0, 0), right = new float3(0, 0, 1),
                corners = new[] { new float3(x, 0, 2), new float3(x, 0, 0), new float3(x, 2, 0), new float3(x, 2, 2) }
            };
        }

        static HotspotLink Link(int a, int b, int order, bool connect) =>
            new() { brushA = BrushID(a), surfaceA = 0, brushB = BrushID(b), surfaceB = 0, order = order, connection = connect ? 1 : -1 };

        // The model grouped both ways; asserts they are the same and returns what the incremental one did
        (bool full, int dirty, int groups) Update(List<Brush> model, HotspotLink[] links = null, string step = "")
        {
            using var brushes = new NativeList<BrushData>(Allocator.Temp);
            for (int b = 0; b < model.Count; b++)
                brushes.Add(new BrushData { brushRenderBuffer = Buffer(model[b]), brushSurfaceCount = 1,
                                            brushIndexOrder = new IndexOrder { compactNodeID = BrushID(model[b].id), nodeOrder = b } });
            var keptList = new bool[model.Count];
            for (int b = 0; b < model.Count; b++)
                keptList[b] = last.TryGetValue(model[b].id, out var was) && Same(was, model[b]);
            using var kept = new NativeArray<bool>(keptList, Allocator.Temp);
            last.Clear();
            foreach (var brush in model)
                last[brush.id] = brush;
            using var linkArray = new NativeArray<HotspotLink>(links ?? new HotspotLink[0], Allocator.Temp);
            var full = FindState.Create(Allocator.Temp);
            var incremental = FindState.Create(Allocator.Temp);
            try
            {
                HotspotGroups.Prepare(brushes, tiles.AsArray(), layouts, linkArray, ref full);
                HotspotGroups.PrepareIncremental(brushes, tiles.AsArray(), layouts, linkArray, ref incremental, ref memory, kept);
                Assert.That(HotspotGroups.SameAs(incremental, full, out var difference), Is.True, $"{step}: {difference}");
                return (memory.lastFull.Value, memory.lastDirty.Value, full.leads.Length);
            }
            finally
            {
                full.Dispose();
                incremental.Dispose();
            }
        }

        static bool Same(Brush a, Brush b)
        {
            if (!a.normal.Equals(b.normal) || !a.right.Equals(b.right) || a.plain != b.plain || a.corners.Length != b.corners.Length)
                return false;
            for (int i = 0; i < a.corners.Length; i++)
            {
                if (!a.corners[i].Equals(b.corners[i]))
                    return false;
            }
            return true;
        }

        // Two rows of three squares, far apart: two groups
        static List<Brush> TwoRows() => new()
        {
            Square(0, 0), Square(1, 2), Square(2, 4),
            Square(3, 20), Square(4, 22), Square(5, 24)
        };

        // Squares far from the rest and from each other: enough of the model stays clean that it is grouped incrementally
        static List<Brush> WithFarSquares(List<Brush> model, int firstId)
        {
            for (int i = 0; i < 10; i++)
                model.Add(Square(firstId + i, 100 + i * 3));
            return model;
        }

        [Test]
        public void TheSameModelAgain_GroupsNothingAgain()
        {
            var model = TwoRows();
            Assert.That(Update(model).full, Is.True, "nothing kept the first time");
            var again = Update(model, step: "again");
            Assert.That(again.full, Is.False);
            Assert.That(again.dirty, Is.Zero);
            Assert.That(again.groups, Is.EqualTo(2));
        }

        [Test]
        public void MovingASquareOutOfItsRow_GroupsOnlyItsRowAgain()
        {
            var model = TwoRows();
            Update(model);
            model[2] = Square(2, 6.5f);
            var moved = Update(model, step: "moved away");
            Assert.That(moved.full, Is.False, "grouped incrementally");
            Assert.That(moved.dirty, Is.EqualTo(3), "the row it was in");
            model[2] = Square(2, 4);
            var back = Update(model, step: "moved back");
            Assert.That(back.full, Is.False);
            Assert.That(back.groups, Is.EqualTo(2));
        }

        [Test]
        public void AddingAndRemovingSquares_MatchesTheFullGrouping()
        {
            var model = WithFarSquares(TwoRows(), 100);
            Update(model);
            model.Add(Square(6, 26));
            Assert.That(Update(model, step: "added").full, Is.False);
            model.RemoveAt(4);
            Assert.That(Update(model, step: "removed from the middle of a row").full, Is.False);
            model.RemoveAt(0);
            Assert.That(Update(model, step: "removed the first").full, Is.False);
        }

        // A brush that stays, with a surface that is not a candidate any more: its kept candidate is gone
        [Test]
        public void ASquareThatLosesItsLayout_LeavesItsRow()
        {
            var model = WithFarSquares(TwoRows(), 100);
            Update(model);
            var plain = Square(1, 2);
            plain.plain = true;
            model[1] = plain;
            Assert.That(Update(model, step: "the middle square lost its layout").full, Is.False);
            model[1] = Square(1, 2);
            Assert.That(Update(model, step: "and has it again").full, Is.False);
        }

        [Test]
        public void ASliverBesideASquare_FollowsItsNeighbour()
        {
            var model = WithFarSquares(new List<Brush> { Square(0, 0), Square(1, 2, width: 0.001f), Square(2, 2.001f), Square(3, 20), Square(4, 22) }, 100);
            Update(model);
            model[2] = Square(2, 2.001f, right: new float3(-1, 0, 0));
            Assert.That(Update(model, step: "the sliver's other neighbour turned").full, Is.False);
            model[0] = Square(0, -0.5f);
            Assert.That(Update(model, step: "its first neighbour moved away").full, Is.False);
            model[0] = Square(0, 0);
            Assert.That(Update(model, step: "and back").full, Is.False);
        }

        [Test]
        public void AFoldedPair_KeepsItsUnfold()
        {
            var model = new List<Brush> { Square(0, -2), Side(1, 0), Square(2, 20), Square(3, 22) };
            var links = new[] { Link(0, 1, 1, true) };
            Update(model, links);
            model[3] = Square(3, 23);
            var moved = Update(model, links, "the other pair moved apart");
            Assert.That(moved.full, Is.False);
            Assert.That(moved.dirty, Is.EqualTo(2));
        }

        [Test]
        public void ALinkEdit_GroupsEverythingAgain()
        {
            var model = TwoRows();
            Update(model);
            Assert.That(Update(model, new[] { Link(0, 1, 1, false) }, "linked").full, Is.True);
        }

        [Test]
        public void BrushesInAnotherOrder_GroupEverythingAgain()
        {
            var model = TwoRows();
            Update(model);
            (model[0], model[1]) = (model[1], model[0]);
            Assert.That(Update(model, step: "reordered").full, Is.True);
        }

        // Random edits on a strip of squares, a fold and slivers: whatever is grouped again, the result is the full one's
        [Test]
        public void RandomEdits_AlwaysMatchTheFullGrouping([Values(1, 2, 3, 4)] int seed)
        {
            var random = new Unity.Mathematics.Random((uint)seed * 7919u);
            var model = new List<Brush>();
            var nextId = 0;
            for (int i = 0; i < 12; i++)
                model.Add(Square(nextId++, i * 2));
            model.Add(Side(nextId++, 24));
            // slivers between two squares: each follows the neighbour it shares its longest edge with
            for (int i = 0; i < 3; i++)
            {
                var x = 40 + i * 10;
                model.Add(Square(nextId++, x));
                model.Add(Square(nextId++, x + 2, width: 0.001f));
                // in another frame: the sliver is only grouped with it when its other neighbour is gone
                model.Add(Square(nextId++, x + 2.001f, right: new float3(-1, 0, 0)));
            }
            for (int i = 0; i < 10; i++)
                model.Add(Square(nextId++, 200 + i * 3));
            Update(model);
            int incremental = 0;
            for (int step = 0; step < 200; step++)
            {
                var what = random.NextInt(7);
                var at   = random.NextInt(model.Count);
                if (what == 6)
                {
                    // nudge a square beside a sliver away from it, or back
                    var x = 40 + random.NextInt(3) * 10 + (random.NextBool() ? 0 : 2.001f);
                    for (int k = 0; k < model.Count; k++)
                    {
                        if (model[k].corners[0].x == x || model[k].corners[0].x == x - 0.5f)
                        {
                            var width = model[k].corners[1].x - model[k].corners[0].x;
                            model[k] = Square(model[k].id, model[k].corners[0].x == x ? x - 0.5f : x, width, model[k].right);
                            break;
                        }
                    }
                    if (Update(model, step: $"seed {seed} step {step}: nudged beside a sliver").full == false)
                        incremental++;
                    continue;
                }
                string description;
                switch (what)
                {
                    case 0: // move a square along the strip, touching other squares or not
                        model[at] = Square(model[at].id, random.NextInt(0, 34) * 1.0f);
                        description = $"moved {model[at].id}";
                        break;
                    case 1: // turn its frame
                        model[at] = Square(model[at].id, model[at].corners[0].x, model[at].corners[1].x - model[at].corners[0].x, random.NextBool() ? new float3(1, 0, 0) : new float3(-1, 0, 0));
                        description = $"turned {model[at].id}";
                        break;
                    case 2: // make it a sliver, or not
                        model[at] = Square(model[at].id, model[at].corners[0].x, random.NextBool() ? 0.001f : 2);
                        description = $"resized {model[at].id}";
                        break;
                    case 3: // remove one
                        if (model.Count <= 3) { description = "nothing"; break; }
                        description = $"removed {model[at].id}";
                        model.RemoveAt(at);
                        break;
                    case 4: // add one, after the others
                        model.Add(Square(nextId++, random.NextInt(0, 34) * 1.0f));
                        description = $"added {nextId - 1}";
                        break;
                    default: // nothing
                        description = "nothing";
                        break;
                }
                if (Update(model, step: $"seed {seed} step {step}: {description}").full == false)
                    incremental++;
            }
            Assert.That(incremental, Is.GreaterThan(100), "most steps were grouped incrementally");
        }
    }
}
