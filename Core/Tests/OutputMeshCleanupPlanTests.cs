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
                work.Dispose(); neighbours.Dispose(); outlineBrushes.Dispose(); cache.Dispose(); memory.Dispose();
                foreach (var o in outlines) o.Dispose();
                outlines.Dispose();
            }
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
