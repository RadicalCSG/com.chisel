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
                    brushes.Add(new BrushData());
                    continue;
                }
                var min = new float3(i % side, random.NextInt(3) * 0.5f, i / side);
                var gap = random.NextInt(3) switch { 0 => 0f, 1 => 0.25f, _ => -0.25f };
                var buffer = OutputMeshCleanupTests.Brush(-1, BoxSides(min, min + new float3(1 + gap, 1, 1 + gap)));
                made.Add(buffer);
                brushes.Add(new BrushData { brushRenderBuffer = buffer, brushIndexOrder = new IndexOrder { nodeOrder = i } });
            }
            return brushes;
        }

        sealed class Planned : System.IDisposable
        {
            public NativeList<MeshCleanupWork> work = new(Allocator.Persistent);
            public NativeList<int> neighbours = new(Allocator.Persistent);
            public NativeList<int> outlineBrushes = new(Allocator.Persistent);
            public NativeList<OutputMeshCleanup.BrushOutlines> outlines = new(Allocator.Persistent);
            // empty: every brush is planned
            public NativeParallelHashMap<CompactNodeID, MeshCleanupEntry> cache = new(16, Allocator.Persistent);

            public void Dispose()
            {
                work.Dispose(); neighbours.Dispose(); outlineBrushes.Dispose(); cache.Dispose();
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
                MeshCleanupPlanJobs.Schedule(true, brushes, parallel.cache, parallel.work, parallel.neighbours, parallel.outlineBrushes, parallel.outlines, false, default).Complete();

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
                var inJobs = Best(() => MeshCleanupPlanJobs.Schedule(true, brushes, parallel.cache, parallel.work, parallel.neighbours, parallel.outlineBrushes, parallel.outlines, false, default).Complete());
                Assert.That(inJobs * 2, Is.LessThan(oneAfterTheOther), $"planned in jobs in {inJobs:F3} ms, one brush after the other in {oneAfterTheOther:F3} ms");
            }
            finally
            {
                Free(brushes, made);
            }
        }
    }
}
