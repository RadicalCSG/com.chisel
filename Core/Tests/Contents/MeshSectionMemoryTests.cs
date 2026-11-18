using NUnit.Framework;
using Unity.Mathematics;

namespace Chisel.Core.Tests
{
    // The mesh sections are grouped again only where brushes changed (MeshSectionMemory): with Verify every update's lists
    // are compared with those made of every brush as before, and a difference is logged as an error
    [TestFixture]
    [Category("Contents")]
    public class MeshSectionMemoryTests
    {
        bool verify;

        [SetUp]
        public void SetUp()
        {
            verify = MeshSectionMemory.Verify;
            MeshSectionMemory.Verify = true;
        }

        [TearDown]
        public void TearDown()
        {
            MeshSectionMemory.Verify = verify;
            ModelSettingsStore.Remove(0);
        }

        static ContentsSceneNode Box(float3 min, float3 max, string name) => ContentsSceneNode.Brush(ContentsScene.BoxPlanes(min, max), name: name);

        // Boxes over several 32 m cells, 16 m chunks and 8 m cells, apart and touching, of a few sizes
        static ContentsScene Spread(uint seed, int count)
        {
            var random = new Random(seed);
            var scene  = new ContentsScene();
            for (int i = 0; i < count; i++)
            {
                var min = new float3(random.NextInt(0, 80), random.NextInt(0, 3), random.NextInt(0, 80));
                scene.Add(Box(min, min + new float3(random.NextInt(1, 4), random.NextInt(1, 3), random.NextInt(1, 4)), $"box {i}"));
            }
            return scene;
        }

        static void SetLightmapBudget(bool split)
        {
            // 4 texels a unit and at most 16 texels: a chunk with more than 16 square units of lightmap is split
            ModelSettingsStore.Set(0, new ModelSettings { LightmapTexelsPerUnit = 4, LightmapMaxSize = split ? 16 : 0 });
        }

        [Test]
        public void RandomEdits_GroupLikeEveryBrushGroupedAgain([Values(false, true)] bool split, [Values(1u, 2u)] uint seed)
        {
            SetLightmapBudget(split);
            var random = new Random(seed * 7919u);
            using var harness = ContentsTreeHarness.Build(Spread(seed, 60));
            harness.JudgeEachUpdate = false;
            Assert.That(harness.Update(), Is.True, "built");
            var memory = ChiselTreeLookup.Value[harness.Tree].meshSectionMemory;
            Assert.That(memory.lastFull.Value, Is.True, "the first update groups everything");
            var added = 0;
            for (int step = 0; step < 40; step++)
            {
                var roots = harness.Scene.roots;
                var brush = roots[random.NextInt(roots.Count)];
                string edit;
                switch (random.NextInt(6))
                {
                    case 0:
                    case 1:
                    {
                        // a little, or across cells and chunks
                        var offset = random.NextBool() ? new float3(0.25f, 0, 0) : new float3(random.NextInt(-12, 13), 0, random.NextInt(-12, 13));
                        harness.SetLocalToTree(brush, math.mul(float4x4.Translate(offset), brush.localToTree));
                        edit = $"moved {brush.name} by {offset}";
                        break;
                    }
                    case 2:
                        harness.Remove(brush, destroy: true);
                        edit = $"removed {brush.name}";
                        break;
                    case 3:
                    {
                        var min = new float3(random.NextInt(0, 80), 0, random.NextInt(0, 80));
                        var box = Box(min, min + new float3(2, 1, 2), $"added {added++}");
                        harness.Insert(null, random.NextInt(0, roots.Count + 1), box);
                        edit = $"added {box.name}";
                        break;
                    }
                    case 4:
                        harness.Move(brush, null, random.NextInt(0, roots.Count));
                        edit = $"moved {brush.name} in the tree";
                        break;
                    default:
                        // resized: more lightmap in its cells
                        harness.SetLocalToTree(brush, math.mul(brush.localToTree, float4x4.Scale(new float3(2, 1, 2))));
                        edit = $"scaled {brush.name}";
                        break;
                }
                Assert.That(harness.Update(), Is.True, $"step {step}, {edit}");
            }
        }

        // A brush gone and a new one added in the same update: the hierarchy gives the new one the slot of the one gone, and
        // the memories that keep by slot (BrushSlots: the sections, the weld) take it as that brush gone and a new one there
        [Test]
        public void ABrushReplacedInOneUpdate_GroupsAndWeldsLikeEverythingDoneAgain([Values(false, true)] bool split)
        {
            SetLightmapBudget(split);
            var weldVerify = OutputModelWeld.VerifyIncremental;
            OutputModelWeld.VerifyIncremental = true;
            try
            {
                var random = new Random(11);
                using var harness = ContentsTreeHarness.Build(Spread(6, 40));
                harness.JudgeEachUpdate = false;
                Assert.That(harness.Update(), Is.True, "built");
                var reused = 0;
                for (int step = 0; step < 8; step++)
                {
                    var roots  = harness.Scene.roots;
                    var gone   = roots[random.NextInt(roots.Count)];
                    var goneID = harness.TreeNodeOf(gone).CompactNodeID;
                    harness.Remove(gone, destroy: true);
                    var min = new float3(random.NextInt(0, 80), 0, random.NextInt(0, 80));
                    var box = Box(min, min + new float3(2, 1, 2), $"replacement {step}");
                    harness.Insert(null, random.NextInt(0, roots.Count + 1), box);
                    if (BrushSlots.Of(harness.TreeNodeOf(box).CompactNodeID) == BrushSlots.Of(goneID))
                        reused++;
                    Assert.That(harness.Update(), Is.True, $"step {step}, {gone.name} replaced");
                }
                Assert.That(reused, Is.GreaterThan(0), "the hierarchy gave a new brush the slot of the one gone");
            }
            finally
            {
                OutputModelWeld.VerifyIncremental = weldVerify;
            }
        }

        // Planted: a surface the memory lost; the verify has to see it
        [Test]
        public void ALostSurface_IsReportedByTheVerify()
        {
            var scene = Spread(4, 30);
            using var harness = ContentsTreeHarness.Build(scene);
            harness.JudgeEachUpdate = false;
            Assert.That(harness.Update(), Is.True);
            var memory = ChiselTreeLookup.Value[harness.Tree].meshSectionMemory;
            var lost = false;
            for (int t = 0; t < memory.queries.Length && !lost; t++)
            {
                ref var query = ref memory.queries.ElementAt(t);
                for (int f = 0; f < query.fines.Length && !lost; f++)
                {
                    ref var fine = ref query.fines.ElementAt(f);
                    if (!fine.alive || fine.surfaces.Length < 2)
                        continue;
                    fine.surfaces.RemoveAt(fine.surfaces.Length - 1);
                    fine.entries.RemoveAt(fine.entries.Length - 1);
                    lost = true;
                }
            }
            Assert.That(lost, Is.True, "a group of two or more surfaces to take one from");

            UnityEngine.TestTools.LogAssert.Expect(UnityEngine.LogType.Error, new System.Text.RegularExpressions.Regex("mesh sections made with the memory differ"));
            var moved = scene.roots[0];
            harness.SetLocalToTree(moved, math.mul(float4x4.Translate(new float3(0, 0.25f, 0)), moved.localToTree));
            Assert.That(harness.Update(), Is.True);
        }

        // Planted: remembered section hashes that are wrong; the sections whose groups didn't change take them, and the
        // verify, which hashes their surfaces too, has to see it
        [Test]
        public void WrongRememberedSectionHashes_AreReportedByTheVerify()
        {
            var scene = Spread(5, 30);
            using var harness = ContentsTreeHarness.Build(scene);
            harness.JudgeEachUpdate = false;
            Assert.That(harness.Update(), Is.True);
            var memory = ChiselTreeLookup.Value[harness.Tree].meshSectionMemory;
            Assert.That(memory.sectionHashes.Count(), Is.GreaterThan(0), "sections remembered");
            using (var keys = memory.sectionHashes.GetKeyArray(Unity.Collections.Allocator.Temp))
            {
                foreach (var key in keys)
                {
                    var hashes = memory.sectionHashes[key];
                    hashes.lightmap ^= 1;
                    hashes.section.x ^= 1;
                    memory.sectionHashes[key] = hashes;
                }
            }

            UnityEngine.TestTools.LogAssert.Expect(UnityEngine.LogType.Error, new System.Text.RegularExpressions.Regex("remembered hashes of [0-9]+ sections differ"));
            var moved = scene.roots[0];
            harness.SetLocalToTree(moved, math.mul(float4x4.Translate(new float3(0, 0.25f, 0)), moved.localToTree));
            Assert.That(harness.Update(), Is.True);
        }

        // Brushes whose buffers stay the same but come in another order are grouped again, every one: a group keeps its
        // surfaces in the order of their brushes
        [Test]
        public void BrushesInAnotherOrder_AreAllGroupedAgain()
        {
            float3[] Quad(float x) => new[] { new float3(x, 0, 0), new float3(x + 1, 0, 0), new float3(x + 1, 0, 1), new float3(x, 0, 0), new float3(x + 1, 0, 1), new float3(x, 0, 1) };
            var buffers = new[] { OutputMeshCleanupTests.Brush(-1, Quad(0)), OutputMeshCleanupTests.Brush(-1, Quad(2)), OutputMeshCleanupTests.Brush(-1, Quad(4)) };
            var memory  = MeshSectionMemory.Create(Unity.Collections.Allocator.Persistent);
            var brushes = new Unity.Collections.NativeList<BrushData>(Unity.Collections.Allocator.Persistent);
            var slotOfBrush = new Unity.Collections.NativeList<int>(Unity.Collections.Allocator.Persistent);
            var changed     = new Unity.Collections.NativeList<int>(Unity.Collections.Allocator.Persistent);
            var touched     = new Unity.Collections.NativeList<int>(Unity.Collections.Allocator.Persistent);
            var touchedSlot = new Unity.Collections.NativeList<bool>(Unity.Collections.Allocator.Persistent);
            try
            {
                void Prepare(params int[] order)
                {
                    brushes.Clear();
                    foreach (var b in order)
                        brushes.Add(new BrushData { brushRenderBuffer = buffers[b], brushIndexOrder = new IndexOrder { nodeOrder = b, compactNodeID = new CompactNodeID(default, new SlotIndex { index = b + 1, generation = 1 }) } });
                    MeshSections.Prepare(brushes, ref memory, 1, 0, slotOfBrush, changed, touched, touchedSlot);
                    MeshSections.Finish(ref memory, touched.AsArray());
                }
                Prepare(0, 1, 2);
                Assert.That(memory.lastFull.Value, Is.True, "nothing grouped yet");
                Prepare(0, 1, 2);
                Assert.That((memory.lastFull.Value, memory.lastChanged.Value), Is.EqualTo((false, 0)), "the same brushes, in the same order");
                Prepare(0, 2);
                Assert.That((memory.lastFull.Value, memory.lastChanged.Value), Is.EqualTo((false, 0)), "one removed: the others keep their order");
                Prepare(2, 0);
                Assert.That(memory.lastFull.Value, Is.True, "another order");
            }
            finally
            {
                memory.Dispose();
                brushes.Dispose(); slotOfBrush.Dispose(); changed.Dispose(); touched.Dispose(); touchedSlot.Dispose();
                foreach (var buffer in buffers)
                    buffer.Dispose();
            }
        }

        // A brush gone and a new one in its slot (BrushSlots), put last: the new one is no brush that moved, so the others
        // keep their order and only it is grouped again
        [Test]
        public void ANewBrushInTheSlotOfOneGone_IsNoReorder()
        {
            float3[] Quad(float x) => new[] { new float3(x, 0, 0), new float3(x + 1, 0, 0), new float3(x + 1, 0, 1), new float3(x, 0, 0), new float3(x + 1, 0, 1), new float3(x, 0, 1) };
            var buffers = new[] { OutputMeshCleanupTests.Brush(-1, Quad(0)), OutputMeshCleanupTests.Brush(-1, Quad(2)), OutputMeshCleanupTests.Brush(-1, Quad(4)), OutputMeshCleanupTests.Brush(-1, Quad(6)) };
            var memory  = MeshSectionMemory.Create(Unity.Collections.Allocator.Persistent);
            var brushes = new Unity.Collections.NativeList<BrushData>(Unity.Collections.Allocator.Persistent);
            var slotOfBrush = new Unity.Collections.NativeList<int>(Unity.Collections.Allocator.Persistent);
            var changed     = new Unity.Collections.NativeList<int>(Unity.Collections.Allocator.Persistent);
            var touched     = new Unity.Collections.NativeList<int>(Unity.Collections.Allocator.Persistent);
            var touchedSlot = new Unity.Collections.NativeList<bool>(Unity.Collections.Allocator.Persistent);
            try
            {
                // (slot, generation, buffer) per brush
                void Prepare(params int3[] order)
                {
                    brushes.Clear();
                    foreach (var o in order)
                        brushes.Add(new BrushData { brushRenderBuffer = buffers[o.z], brushIndexOrder = new IndexOrder { nodeOrder = o.z, compactNodeID = new CompactNodeID(default, new SlotIndex { index = o.x, generation = o.y }) } });
                    MeshSections.Prepare(brushes, ref memory, 1, 0, slotOfBrush, changed, touched, touchedSlot);
                    MeshSections.Finish(ref memory, touched.AsArray());
                }
                Prepare(new int3(1, 1, 0), new int3(2, 1, 1), new int3(3, 1, 2));
                Assert.That(memory.lastFull.Value, Is.True, "nothing grouped yet");
                Prepare(new int3(2, 1, 1), new int3(3, 1, 2), new int3(1, 2, 3));
                Assert.That((memory.lastFull.Value, memory.lastChanged.Value), Is.EqualTo((false, 1)), "the new brush grouped, the others kept");
                Assert.That(touched.AsArray().ToArray(), Is.EqualTo(new[] { 1 }), "the slot: the gone brush's surfaces out, the new one's in");
            }
            finally
            {
                memory.Dispose();
                brushes.Dispose(); slotOfBrush.Dispose(); changed.Dispose(); touched.Dispose(); touchedSlot.Dispose();
                foreach (var buffer in buffers)
                    buffer.Dispose();
            }
        }

        // Of many brushes, moving one groups that one again
        [Test]
        public void MovingOneBrush_GroupsOnlyItAgain([Values(false, true)] bool split)
        {
            SetLightmapBudget(split);
            var scene = Spread(3, 200);
            using var harness = ContentsTreeHarness.Build(scene);
            harness.JudgeEachUpdate = false;
            Assert.That(harness.Update(), Is.True);
            var memory = ChiselTreeLookup.Value[harness.Tree].meshSectionMemory;
            Assert.That(memory.lastFull.Value, Is.True, "the first update groups everything");

            var moved = scene.roots[100];
            harness.SetLocalToTree(moved, math.mul(float4x4.Translate(new float3(0, 0.25f, 0)), moved.localToTree));
            Assert.That(harness.Update(), Is.True);
            Assert.That(memory.lastFull.Value, Is.False, "one brush moved");
            Assert.That(memory.lastChanged.Value, Is.LessThanOrEqualTo(10), "brushes grouped again, of 200: the moved one and those whose buffers it changed");
        }
    }
}
