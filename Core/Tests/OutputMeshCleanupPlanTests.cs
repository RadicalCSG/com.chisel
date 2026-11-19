using System.Collections.Generic;
using NUnit.Framework;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Jobs.LowLevel.Unsafe;
using Unity.Mathematics;

namespace Chisel.Core.Tests
{
    // The cleanup's plan made by its jobs (MeshCleanupPlanJobs: each brush's key, bounds and the brushes it touches in
    // parallel) is the plan OutputMeshCleanup.Plan makes one brush after the other
    public class OutputMeshCleanupPlanTests
    {
        // A box's six sides, two triangles each, turned outwards
        static float3[][] BoxSides(float3 min, float3 max)
        {
            float3 P(int x, int y, int z) => new float3(x == 0 ? min.x : max.x, y == 0 ? min.y : max.y, z == 0 ? min.z : max.z);
            float3[] Quad(float3 a, float3 b, float3 c, float3 d) => new[] { a, b, c, a, c, d };
            return new[]
            {
                Quad(P(0,0,0), P(0,1,0), P(1,1,0), P(1,0,0)),
                Quad(P(0,0,1), P(1,0,1), P(1,1,1), P(0,1,1)),
                Quad(P(0,0,0), P(0,0,1), P(0,1,1), P(0,1,0)),
                Quad(P(1,0,0), P(1,1,0), P(1,1,1), P(1,0,1)),
                Quad(P(0,0,0), P(1,0,0), P(1,0,1), P(0,0,1)),
                Quad(P(0,1,0), P(0,1,1), P(1,1,1), P(1,1,0)),
            };
        }

        // Boxes on a grid, some touching their neighbours, some apart, some overlapping, and some brushes without output
        static NativeList<BrushData> Brushes(int count, uint seed, List<BlobAssetReference<ChiselBrushRenderBuffer>> made)
        {
            var random  = new Random(seed);
            var brushes = new NativeList<BrushData>(count, Allocator.Persistent);
            var side    = (int)math.ceil(math.sqrt(count));
            for (int i = 0; i < count; i++)
            {
                if (random.NextInt(12) == 0)
                {
                    brushes.Add(new BrushData { brushIndexOrder = IndexOf(i) });
                    continue;
                }
                var min = new float3(i % side, random.NextInt(3) * 0.5f, i / side);
                var gap = random.NextInt(3) switch { 0 => 0f, 1 => 0.25f, _ => -0.25f };
                brushes.Add(new BrushData { brushRenderBuffer = Box(min, min + new float3(1 + gap, 1, 1 + gap), made), brushIndexOrder = IndexOf(i) });
            }
            return brushes;
        }

        static IndexOrder IndexOf(int id) => new IndexOrder { nodeOrder = id, compactNodeID = new CompactNodeID(default, new SlotIndex { index = id + 1, generation = 1 }) };

        static BlobAssetReference<ChiselBrushRenderBuffer> Box(float3 min, float3 max, List<BlobAssetReference<ChiselBrushRenderBuffer>> made)
        {
            var buffer = OutputMeshCleanupTests.Brush(-1, BoxSides(min, max));
            made.Add(buffer);
            return buffer;
        }

        // Every work, key, neighbour and outline brush of the plan as expected
        static void AssertSamePlan(Planned expected, Planned actual, string what)
        {
            Assert.That(actual.work.Length, Is.EqualTo(expected.work.Length), $"{what}: work");
            Assert.That(actual.outlineBrushes.AsArray().ToArray(), Is.EqualTo(expected.outlineBrushes.AsArray().ToArray()), $"{what}: outline brushes");
            Assert.That(actual.outlines.Length, Is.EqualTo(actual.outlineBrushes.Length), $"{what}: a slot for each brush's outlines");
            for (int w = 0; w < expected.work.Length; w++)
            {
                var a = expected.work[w];
                var b = actual.work[w];
                Assert.That((b.brush, b.key, b.neighbourStart, b.neighbourCount), Is.EqualTo((a.brush, a.key, a.neighbourStart, a.neighbourCount)), $"{what}: work {w}");
                for (int n = 0; n < a.neighbourCount; n++)
                    Assert.That(actual.outlineBrushes[actual.neighbours[b.neighbourStart + n]], Is.EqualTo(expected.outlineBrushes[expected.neighbours[a.neighbourStart + n]]), $"{what}: work {w}");
            }
        }

        static void PlanInJobs(NativeList<BrushData> brushes, Planned planned)
        {
            MeshCleanupPlanJobs.Schedule(true, brushes, planned.cache, planned.memory, planned.work, planned.neighbours, planned.outlineBrushes, planned.outlines, false, default).Complete();
        }

        sealed class Planned : System.IDisposable
        {
            public NativeList<MeshCleanupWork> work = new(Allocator.Persistent);
            public NativeList<int> neighbours = new(Allocator.Persistent);
            public NativeList<int> outlineBrushes = new(Allocator.Persistent);
            public NativeList<OutputMeshCleanup.BrushOutlines> outlines = new(Allocator.Persistent);
            // empty: every brush is planned
            public NativeParallelHashMap<CompactNodeID, MeshCleanupEntry> cache = new(16, Allocator.Persistent);
            public MeshCleanupPlanMemory memory = MeshCleanupPlanMemory.Create(Allocator.Persistent);

            public void Dispose()
            {
                foreach (var entry in cache)
                    entry.Value.Dispose();
                work.Dispose(); neighbours.Dispose(); outlineBrushes.Dispose(); cache.Dispose(); memory.Dispose();
                foreach (var o in outlines) o.Dispose();
                outlines.Dispose();
            }
        }

        // The cleanup as an update runs it with the tree's memory and cache: planned, the outlines made, the planned brushes
        // cleaned up, the results handed to the brushes and kept
        static void CleanUp(NativeList<BrushData> brushes, Planned planned)
        {
            PlanInJobs(brushes, planned);
            OutputMeshCleanup.Outlines(brushes, planned.outlineBrushes, planned.outlines);
            for (int i = 0; i < planned.work.Length; i++)
                planned.work[i] = OutputMeshCleanup.CleanPlanned(planned.work[i], brushes.AsArray(), planned.neighbours.AsArray(), planned.outlines.AsArray(), planned.cache);
            using var patched = new NativeList<BlobAssetReference<ChiselBrushRenderBuffer>>(Allocator.Temp);
            OutputMeshCleanup.Finish(brushes, patched, planned.work, planned.cache, planned.memory);
            Assert.That(patched.Length, Is.Zero, "every result kept by the cache");
        }

        static void Free(NativeList<BrushData> brushes, List<BlobAssetReference<ChiselBrushRenderBuffer>> made)
        {
            brushes.Dispose();
            foreach (var buffer in made)
                buffer.Dispose();
        }

        [Test]
        public void PlanningInJobs_GivesThePlanOfPlanningOneBrushAfterTheOther([Values(1u, 2u)] uint seed)
        {
            var made = new List<BlobAssetReference<ChiselBrushRenderBuffer>>();
            var brushes = Brushes(400, seed, made);
            using var serial   = new Planned();
            using var parallel = new Planned();
            try
            {
                OutputMeshCleanup.Plan(brushes, serial.cache, serial.work, serial.neighbours, serial.outlineBrushes);
                PlanInJobs(brushes, parallel);

                Assert.That(parallel.memory.lastFull.Value, Is.True, "the first plan measures every brush");
                Assert.That(serial.work.Length, Is.GreaterThan(300));
                Assert.That(parallel.work.Length, Is.EqualTo(serial.work.Length));
                Assert.That(parallel.outlineBrushes.AsArray().ToArray(), Is.EqualTo(serial.outlineBrushes.AsArray().ToArray()));
                Assert.That(parallel.outlines.Length, Is.EqualTo(parallel.outlineBrushes.Length), "a slot for each brush's outlines");
                var touchingSome = 0;
                for (int w = 0; w < serial.work.Length; w++)
                {
                    var a = serial.work[w];
                    var b = parallel.work[w];
                    Assert.That((b.brush, b.key, b.neighbourStart, b.neighbourCount), Is.EqualTo((a.brush, a.key, a.neighbourStart, a.neighbourCount)), $"work {w}");
                    var last = -1;
                    for (int n = 0; n < a.neighbourCount; n++)
                    {
                        var neighbour = serial.outlineBrushes[serial.neighbours[a.neighbourStart + n]];
                        Assert.That(parallel.outlineBrushes[parallel.neighbours[b.neighbourStart + n]], Is.EqualTo(neighbour));
                        Assert.That(neighbour, Is.GreaterThan(last), "a brush's neighbours are in brush order");
                        last = neighbour;
                    }
                    touchingSome += a.neighbourCount > 0 ? 1 : 0;
                }
                Assert.That(touchingSome, Is.GreaterThan(200), "most boxes touch another");
            }
            finally
            {
                Free(brushes, made);
            }
        }

        [BurstCompile(CompileSynchronously = true)]
        struct PlanOneAfterTheOtherJob : IJob
        {
            public NativeList<BrushData> brushes;
            public NativeList<MeshCleanupWork> work;
            public NativeList<int> neighbours, outlineBrushes;
            public NativeParallelHashMap<CompactNodeID, MeshCleanupEntry> cache;

            public void Execute() => OutputMeshCleanup.Plan(brushes, cache, work, neighbours, outlineBrushes);
        }

        // Each brush is hashed, bounded and compared with the brushes near it on its own
        [Test]
        public void PlanningInJobs_IsFasterThanPlanningOneBrushAfterTheOther()
        {
            Assume.That(JobsUtility.JobWorkerCount, Is.GreaterThanOrEqualTo(4), "needs worker threads to run on");
            var made = new List<BlobAssetReference<ChiselBrushRenderBuffer>>();
            var brushes = Brushes(3000, 3, made);
            using var serial   = new Planned();
            using var parallel = new Planned();
            try
            {
                double Best(System.Action action)
                {
                    action();
                    var best = double.MaxValue;
                    for (int r = 0; r < 10; r++)
                    {
                        var clock = System.Diagnostics.Stopwatch.StartNew();
                        action();
                        best = System.Math.Min(best, clock.Elapsed.TotalMilliseconds);
                    }
                    return best;
                }
                var oneAfterTheOther = Best(() => new PlanOneAfterTheOtherJob { brushes = brushes, work = serial.work, neighbours = serial.neighbours, outlineBrushes = serial.outlineBrushes, cache = serial.cache }.Run());
                // every brush planned again each time, as with no memory
                var inJobs = Best(() => { parallel.memory.Clear(); PlanInJobs(brushes, parallel); });
                Assert.That(inJobs * 2, Is.LessThan(oneAfterTheOther), $"planned in jobs in {inJobs:F3} ms, one brush after the other in {oneAfterTheOther:F3} ms");
            }
            finally
            {
                Free(brushes, made);
            }
        }

        // One to three brushes changed between plans (given a new buffer, the same buffer built again, no buffer, removed,
        // added or moved in the list), and now and then most of them: what the memory keeps gives the plan of planning
        // everything again
        // edits random changes to the brushes: new buffers, the same buffers built again, no buffer, removed, added, replaced
        // by a brush in their slot, swapped; what was done
        static string Edit(NativeList<BrushData> brushes, ref Random random, List<BlobAssetReference<ChiselBrushRenderBuffer>> made, ref int nextID, int edits)
        {
            var what = "";
            for (int e = 0; e < edits; e++)
            {
                var b    = random.NextInt(brushes.Length);
                var data = brushes[b];
                switch (random.NextInt(7))
                {
                    case 0:
                    {
                        var min = new float3(b % 18 + random.NextInt(-1, 2) * 0.25f, random.NextInt(3) * 0.5f, b / 18);
                        data.brushRenderBuffer = Box(min, min + new float3(1, 1, 1), made);
                        brushes[b] = data;
                        what += $", new buffer for {b}";
                        break;
                    }
                    case 1:
                        // with the brush next to it on the grid, which it touches: two measured brushes that touch
                        for (int n = b; n < math.min(b + 2, brushes.Length); n++)
                        {
                            var rebuilt = brushes[n];
                            if (!rebuilt.brushRenderBuffer.IsCreated)
                                continue;
                            ref var surfaces = ref rebuilt.brushRenderBuffer.Value.surfaces;
                            var bounds = surfaces[0].aabb;
                            for (int s = 1; s < surfaces.Length; s++)
                            {
                                bounds.Min = math.min(bounds.Min, surfaces[s].aabb.Min);
                                bounds.Max = math.max(bounds.Max, surfaces[s].aabb.Max);
                            }
                            rebuilt.brushRenderBuffer = Box(bounds.Min, bounds.Max, made);
                            brushes[n] = rebuilt;
                        }
                        what += $", same buffers built again for {b} and the next";
                        break;
                    case 2:
                        data.brushRenderBuffer = default;
                        brushes[b] = data;
                        what += $", no buffer for {b}";
                        break;
                    case 3:
                        brushes.RemoveAt(b);
                        what += $", removed {b}";
                        break;
                    case 4:
                    {
                        var min = new float3(random.NextInt(18), 0, random.NextInt(18));
                        brushes.InsertRange(b, 1);
                        brushes[b] = new BrushData { brushRenderBuffer = Box(min, min + new float3(1, 1, 1), made), brushIndexOrder = IndexOf(nextID++) };
                        what += $", added at {b}";
                        break;
                    }
                    case 5:
                    {
                        // gone, and a new brush in its slot (the next generation of its node id)
                        var id  = data.brushIndexOrder.compactNodeID;
                        var min = new float3(random.NextInt(18), 0, random.NextInt(18));
                        brushes[b] = new BrushData
                        {
                            brushRenderBuffer = Box(min, min + new float3(1, 1, 1), made),
                            brushIndexOrder   = new IndexOrder { nodeOrder = nextID++, compactNodeID = new CompactNodeID(default, new SlotIndex { index = id.slotIndex.index, generation = id.slotIndex.generation + 1 }) }
                        };
                        what += $", {b} replaced by a brush in its slot";
                        break;
                    }
                    default:
                    {
                        var o = random.NextInt(brushes.Length);
                        (brushes[b], brushes[o]) = (brushes[o], brushes[b]);
                        what += $", swapped {b} and {o}";
                        break;
                    }
                }
            }
            return what;
        }

        [Test]
        public void PlanningWithAMemory_GivesThePlanOfPlanningEverythingAgain([Values(1u, 2u)] uint seed)
        {
            var random  = new Random(seed * 7919u);
            var made    = new List<BlobAssetReference<ChiselBrushRenderBuffer>>();
            var brushes = Brushes(300, seed, made);
            var nextID  = 1000;
            using var kept = new Planned();
            try
            {
                PlanInJobs(brushes, kept);
                for (int step = 0; step < 30; step++)
                {
                    var edits = step % 10 == 9 ? 200 : random.NextInt(1, 4);
                    var what  = $"step {step}";
                    what += Edit(brushes, ref random, made, ref nextID, edits);
                    PlanInJobs(brushes, kept);
                    using var fresh = new Planned();
                    OutputMeshCleanup.Plan(brushes, fresh.cache, fresh.work, fresh.neighbours, fresh.outlineBrushes);
                    fresh.outlines.Resize(fresh.outlineBrushes.Length, NativeArrayOptions.ClearMemory);
                    AssertSamePlan(fresh, kept, edits > 100 ? $"step {step}, {edits} edits" : what);
                }
            }
            finally
            {
                Free(brushes, made);
            }
        }

        // Cleaned up update after update with the memory and the cache, which plan again only the brushes an edit can have
        // changed the result of, the brushes come out as cleaned up from nothing
        [Test]
        public void CleaningUpWithTheMemoryAndTheCache_GivesWhatCleaningUpFromNothingGives([Values(1u, 2u)] uint seed)
        {
            var random  = new Random(seed * 104729u);
            var made    = new List<BlobAssetReference<ChiselBrushRenderBuffer>>();
            var brushes = Brushes(300, seed, made);
            var nextID  = 1000;
            using var kept = new Planned();
            try
            {
                using (var first = new NativeList<BrushData>(brushes.Length, Allocator.TempJob))
                {
                    first.AddRange(brushes.AsArray());
                    CleanUp(first, kept);
                }
                var plannedSome = 0;
                for (int step = 0; step < 30; step++)
                {
                    var edits = step % 10 == 9 ? 200 : random.NextInt(1, 4);
                    var what  = $"step {step}" + Edit(brushes, ref random, made, ref nextID, edits);
                    using var withMemory = new NativeList<BrushData>(brushes.Length, Allocator.TempJob);
                    using var fromNothing = new NativeList<BrushData>(brushes.Length, Allocator.TempJob);
                    withMemory.AddRange(brushes.AsArray());
                    fromNothing.AddRange(brushes.AsArray());
                    CleanUp(withMemory, kept);
                    using (var fresh = new Planned())
                    {
                        CleanUp(fromNothing, fresh);
                        for (int b = 0; b < brushes.Length; b++)
                            Assert.That(OutputModelWeld.SameBuffer(withMemory[b].brushRenderBuffer, fromNothing[b].brushRenderBuffer), Is.True,
                                        $"{(edits > 100 ? $"step {step}, {edits} edits" : what)}: brush {b}");
                    }
                    if (edits < 100 && kept.work.Length < brushes.Length / 4)
                        plannedSome++;
                }
                Assert.That(plannedSome, Is.GreaterThan(20), "after a few edits only some brushes are planned again");
            }
            finally
            {
                Free(brushes, made);
            }
        }

        // A brush whose result depends on a brush touching it (a corner of that one on its outline) is cleaned up again when
        // that one moves away, and when it comes back, though it didn't change itself
        [Test]
        public void ABrushWhoseNeighbourMoves_IsCleanedUpAgain()
        {
            var made     = new List<BlobAssetReference<ChiselBrushRenderBuffer>>();
            var brushes  = new NativeList<BrushData>(Allocator.Persistent);
            using var kept = new Planned();
            try
            {
                // a square, and a triangle with a corner on the middle of its bottom edge (OutputMeshCleanupTests)
                BlobAssetReference<ChiselBrushRenderBuffer> Made(params float3[] corners)
                {
                    var buffer = OutputMeshCleanupTests.Brush(-1, corners);
                    made.Add(buffer);
                    return buffer;
                }
                var square   = Made(new float3(0, 0, 0), new float3(2, 0, 0), new float3(2, 2, 0), new float3(0, 0, 0), new float3(2, 2, 0), new float3(0, 2, 0));
                var touching = Made(new float3(1, 0, 0), new float3(1, 0, -1), new float3(2, 0, -1));
                var away     = Made(new float3(11, 0, 0), new float3(11, 0, -1), new float3(12, 0, -1));
                brushes.Add(new BrushData { brushRenderBuffer = square, brushIndexOrder = IndexOf(0) });
                brushes.Add(new BrushData { brushRenderBuffer = touching, brushIndexOrder = IndexOf(1) });

                // what the first brush is made into, from the memory and from nothing; the hash of what it is made into (the
                // buffer itself goes when the cache is given another)
                uint4 Step(BlobAssetReference<ChiselBrushRenderBuffer> second, string what)
                {
                    var data = brushes[1];
                    data.brushRenderBuffer = second;
                    brushes[1] = data;
                    using var withMemory  = new NativeList<BrushData>(brushes.Length, Allocator.TempJob);
                    using var fromNothing = new NativeList<BrushData>(brushes.Length, Allocator.TempJob);
                    withMemory.AddRange(brushes.AsArray());
                    fromNothing.AddRange(brushes.AsArray());
                    CleanUp(withMemory, kept);
                    using var fresh = new Planned();
                    CleanUp(fromNothing, fresh);
                    Assert.That(OutputModelWeld.SameBuffer(withMemory[0].brushRenderBuffer, fromNothing[0].brushRenderBuffer), Is.True, what);
                    Assert.That(OutputHotspotSections.ContentHash(withMemory[0].brushRenderBuffer, out var hash), Is.True);
                    return hash;
                }
                var touched = Step(touching, "touched");
                var alone   = Step(away, "the other moved away");
                Assert.That(touched, Is.Not.EqualTo(alone), "what it is made into depends on the other");
                Step(touching, "the other back");
            }
            finally
            {
                Free(brushes, made);
            }
        }

        // A brush planned again because a brush touching it changed, where nothing of that one near it changed, is given
        // what it was made into last time instead of being cleaned up again
        [Test]
        public unsafe void ABrushWhoseNeighbourChangedAwayFromIt_KeepsWhatItWasMadeInto()
        {
            var made     = new List<BlobAssetReference<ChiselBrushRenderBuffer>>();
            var brushes  = new NativeList<BrushData>(Allocator.Persistent);
            using var kept = new Planned();
            try
            {
                // a square, and a brush of two triangles: one with a corner on the middle of the square's bottom edge, and
                // one far away
                BlobAssetReference<ChiselBrushRenderBuffer> Made(params float3[][] surfaces)
                {
                    var buffer = OutputMeshCleanupTests.Brush(-1, surfaces);
                    made.Add(buffer);
                    return buffer;
                }
                var square = Made(new[] { new float3(0, 0, 0), new float3(2, 0, 0), new float3(2, 2, 0), new float3(0, 0, 0), new float3(2, 2, 0), new float3(0, 2, 0) });
                float3[] Touching() => new[] { new float3(1, 0, 0), new float3(1, 0, -1), new float3(2, 0, -1) };
                float3[] Far(float x) => new[] { new float3(x, 0, 0), new float3(x, 0, -1), new float3(x + 1, 0, -1) };
                brushes.Add(new BrushData { brushRenderBuffer = square, brushIndexOrder = IndexOf(0) });
                brushes.Add(new BrushData { brushRenderBuffer = Made(Touching(), Far(5)), brushIndexOrder = IndexOf(1) });

                using (var first = new NativeList<BrushData>(brushes.Length, Allocator.TempJob))
                {
                    first.AddRange(brushes.AsArray());
                    CleanUp(first, kept);
                    Assert.That(first[0].brushRenderBuffer.IsCreated && first[0].brushRenderBuffer.GetUnsafePtr() != square.GetUnsafePtr(), Is.True, "the square is split where the corner is");
                }
                Assert.That(kept.cache.TryGetValue(IndexOf(0).compactNodeID, out var before), Is.True);
                // the cache's buffer is let go of when it is given another
                var madeInto = before.result.GetUnsafePtr();

                var data = brushes[1];
                data.brushRenderBuffer = Made(Touching(), Far(6));
                brushes[1] = data;
                using var withMemory  = new NativeList<BrushData>(brushes.Length, Allocator.TempJob);
                using var fromNothing = new NativeList<BrushData>(brushes.Length, Allocator.TempJob);
                withMemory.AddRange(brushes.AsArray());
                fromNothing.AddRange(brushes.AsArray());
                CleanUp(withMemory, kept);
                var planned = false;
                for (int w = 0; w < kept.work.Length; w++)
                    planned |= kept.work[w].brush == 0;
                Assert.That(planned, Is.True, "the square is planned again: a brush touching it changed");
                Assert.That(withMemory[0].brushRenderBuffer.IsCreated && withMemory[0].brushRenderBuffer.GetUnsafePtr() == madeInto, Is.True, "given what it was made into");
                using var fresh = new Planned();
                CleanUp(fromNothing, fresh);
                Assert.That(OutputModelWeld.SameBuffer(withMemory[0].brushRenderBuffer, fromNothing[0].brushRenderBuffer), Is.True, "what cleaning up from nothing gives");
            }
            finally
            {
                Free(brushes, made);
            }
        }

        // A brush cleaned up again because something near one of its surfaces changed: a surface of it the cleanup did take
        // something out of is gone through again, though what it reads of that surface is the same, and comes out as
        // cleaning up from nothing makes it
        [Test]
        public void ASurfaceTheCleanupChanged_IsGoneThroughAgain()
        {
            var made     = new List<BlobAssetReference<ChiselBrushRenderBuffer>>();
            var brushes  = new NativeList<BrushData>(Allocator.Persistent);
            using var kept = new Planned();
            try
            {
                BlobAssetReference<ChiselBrushRenderBuffer> Made(params float3[][] surfaces)
                {
                    var buffer = OutputMeshCleanupTests.Brush(-1, surfaces);
                    made.Add(buffer);
                    return buffer;
                }
                // a square cut into four around its centre (which the cleanup takes out), and below it a triangle whose
                // far edge another brush puts a corner on
                float3 a = new(0, 0, 0), b = new(2, 0, 0), c = new(2, 2, 0), d = new(0, 2, 0), centre = new(1, 1, 0);
                var fan  = new[] { a, b, centre,  b, c, centre,  c, d, centre,  d, a, centre };
                var side = new[] { a, new float3(0, 0, -2), new float3(2, 0, -2) };
                brushes.Add(new BrushData { brushRenderBuffer = Made(fan, side), brushIndexOrder = IndexOf(0) });
                float3[] Corner(float x) => new[] { new float3(x, 0, -2), new float3(x, -1, -2), new float3(x + 0.25f, -1, -2) };
                brushes.Add(new BrushData { brushRenderBuffer = Made(Corner(1)), brushIndexOrder = IndexOf(1) });

                for (int step = 0; step < 2; step++)
                {
                    if (step == 1)
                    {
                        var data = brushes[1];
                        data.brushRenderBuffer = Made(Corner(1.5f));
                        brushes[1] = data;
                    }
                    using var withMemory  = new NativeList<BrushData>(brushes.Length, Allocator.TempJob);
                    using var fromNothing = new NativeList<BrushData>(brushes.Length, Allocator.TempJob);
                    withMemory.AddRange(brushes.AsArray());
                    fromNothing.AddRange(brushes.AsArray());
                    CleanUp(withMemory, kept);
                    var planned = false;
                    for (int w = 0; w < kept.work.Length; w++)
                        planned |= kept.work[w].brush == 0 && !kept.work[w].kept;
                    Assert.That(planned, Is.True, $"step {step}: the brush is cleaned up again");
                    using var fresh = new Planned();
                    CleanUp(fromNothing, fresh);
                    Assert.That(withMemory[0].brushRenderBuffer.IsCreated && withMemory[0].brushRenderBuffer.Value.surfaces[0].indexCount == 6, Is.True,
                                $"step {step}: the centre is taken out");
                    Assert.That(OutputModelWeld.SameBuffer(withMemory[0].brushRenderBuffer, fromNothing[0].brushRenderBuffer), Is.True, $"step {step}");
                }
            }
            finally
            {
                Free(brushes, made);
            }
        }

        // The cache forgets the brushes the plan found gone (removed, replaced by another brush in its slot, left without a
        // buffer) without going through all of its entries: an entry planted for a brush the plan never saw is left
        [Test]
        public void TheCache_ForgetsTheBrushesThePlanFoundGone_WithoutGoingThroughIt()
        {
            var made    = new List<BlobAssetReference<ChiselBrushRenderBuffer>>();
            var brushes = Brushes(60, 7, made);
            using var kept = new Planned();
            try
            {
                void Step()
                {
                    using var copy = new NativeList<BrushData>(brushes.Length, Allocator.TempJob);
                    copy.AddRange(brushes.AsArray());
                    CleanUp(copy, kept);
                }
                Step();
                Step();
                int Find(int from)
                {
                    while (!brushes[from].brushRenderBuffer.IsCreated)
                        from++;
                    return from;
                }
                var removed   = Find(0);
                var replaced  = Find(removed + 1);
                var noBuffer  = Find(replaced + 1);
                var removedID  = brushes[removed].brushIndexOrder.compactNodeID;
                var replacedID = brushes[replaced].brushIndexOrder.compactNodeID;
                var noBufferID = brushes[noBuffer].brushIndexOrder.compactNodeID;
                Assert.That(kept.cache.ContainsKey(removedID) && kept.cache.ContainsKey(replacedID) && kept.cache.ContainsKey(noBufferID), Is.True, "each was cleaned up");
                var planted = new CompactNodeID(default, new SlotIndex { index = 5000, generation = 1 });
                kept.cache.Add(planted, new MeshCleanupEntry { key = 1 });

                var data = brushes[noBuffer];
                data.brushRenderBuffer = default;
                brushes[noBuffer] = data;
                var min = new float3(30, 0, 30);
                brushes[replaced] = new BrushData
                {
                    brushRenderBuffer = Box(min, min + new float3(1, 1, 1), made),
                    brushIndexOrder   = new IndexOrder { nodeOrder = 1000, compactNodeID = new CompactNodeID(default, new SlotIndex { index = replacedID.slotIndex.index, generation = replacedID.slotIndex.generation + 1 }) }
                };
                brushes.RemoveAt(removed);
                Step();

                Assert.That(kept.memory.lastFull.Value, Is.False, "a few brushes changed");
                Assert.That(kept.cache.ContainsKey(removedID), Is.False, "the removed brush is forgotten");
                Assert.That(kept.cache.ContainsKey(replacedID), Is.False, "the brush replaced in its slot is forgotten");
                Assert.That(kept.cache.ContainsKey(noBufferID), Is.False, "the brush without a buffer is forgotten");
                Assert.That(kept.cache.ContainsKey(planted), Is.True, "the cache is not gone through");
            }
            finally
            {
                Free(brushes, made);
            }
        }

        // A cleanup's input key changes with everything the cleanup reads: the brush's own buffer, the outlines of the
        // brushes touching it, drawn and of their colliders, and whether its changes are recorded
        [Test]
        public void TheInputKey_FollowsEverythingTheCleanupReads()
        {
            NativeList<OutputMeshCleanup.Outline> drawn, colliders;
            using var _drawn     = drawn     = new NativeList<OutputMeshCleanup.Outline>(Allocator.Temp);
            using var _colliders = colliders = new NativeList<OutputMeshCleanup.Outline>(Allocator.Temp);
            var edge = new OutputMeshCleanup.Outline { from = new float3(0, 0, 0), to = new float3(1, 0, 0), surface = -1 };
            drawn.Add(edge);
            colliders.Add(edge);
            var key = OutputMeshCleanup.InputKey(1, drawn, colliders, false);
            Assert.That(OutputMeshCleanup.InputKey(1, drawn, colliders, false), Is.EqualTo(key), "the same input");
            Assert.That(OutputMeshCleanup.InputKey(2, drawn, colliders, false), Is.Not.EqualTo(key), "another buffer");
            Assert.That(OutputMeshCleanup.InputKey(1, drawn, colliders, true), Is.Not.EqualTo(key), "recorded");
            drawn[0] = new OutputMeshCleanup.Outline { from = edge.from, to = new float3(2, 0, 0), surface = -1 };
            Assert.That(OutputMeshCleanup.InputKey(1, drawn, colliders, false), Is.Not.EqualTo(key), "another drawn outline");
            drawn[0] = edge;
            colliders[0] = new OutputMeshCleanup.Outline { from = edge.from, to = new float3(2, 0, 0), surface = -1 };
            Assert.That(OutputMeshCleanup.InputKey(1, drawn, colliders, false), Is.Not.EqualTo(key), "another collider outline");
            colliders.Clear();
            Assert.That(OutputMeshCleanup.InputKey(1, drawn, colliders, false), Is.Not.EqualTo(key), "no collider outline");
        }

        // The memory measures again only the brush whose buffer changed, of 400
        [Test]
        public void ABrushGivenANewBuffer_IsTheOnlyOneMeasuredAgain()
        {
            var made    = new List<BlobAssetReference<ChiselBrushRenderBuffer>>();
            var brushes = Brushes(400, 5, made);
            using var kept = new Planned();
            try
            {
                PlanInJobs(brushes, kept);
                Assert.That(kept.memory.lastFull.Value, Is.True, "the first plan measures every brush");
                PlanInJobs(brushes, kept);
                Assert.That(kept.memory.lastFull.Value, Is.False, "nothing changed");
                Assert.That(kept.memory.lastMeasured.Value, Is.EqualTo(0), "nothing changed");

                var b = 0;
                while (!brushes[b].brushRenderBuffer.IsCreated)
                    b++;
                var data = brushes[b];
                data.brushRenderBuffer = Box(new float3(0.25f, 0, 0), new float3(1.25f, 1, 1), made);
                brushes[b] = data;
                PlanInJobs(brushes, kept);
                Assert.That(kept.memory.lastFull.Value, Is.False, "one brush changed");
                Assert.That(kept.memory.lastMeasured.Value, Is.EqualTo(1), "one brush changed");
            }
            finally
            {
                Free(brushes, made);
            }
        }
    }
}
