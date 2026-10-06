using System.Threading;
using NUnit.Framework;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;

namespace Chisel.Core.Tests
{
    [TestFixture]
    public unsafe class GeneratorBranchJobPoolTests
    {
        struct CountingGenerator : IBranchGenerator
        {
            [NativeDisableUnsafePtrRestriction] public int* disposed;

            public int PrepareAndCountRequiredBrushMeshes() => 0;
            public bool GenerateNodes(BlobAssetReference<InternalChiselSurfaceArray> internalSurfaceArrayBlob, NativeList<GeneratedNode> nodes, Allocator allocator = Allocator.Persistent) => false;
            public void Dispose()
            {
                if (disposed != null)
                    Interlocked.Increment(ref *disposed);
                disposed = null;
            }
            public void Reset() { }
            public int RequiredSurfaceCount => 0;
            public void UpdateSurfaces(ref ChiselSurfaceArray surfaceArray) { }
            public bool Validate() => true;
            public void GetWarningMessages(IChiselMessageHandler messages) { }
        }

        NativeArray<int> disposed;
        GeneratorBranchJobPool<CountingGenerator> pool;
        readonly System.Collections.Generic.List<CSGTreeBranch> branches = new();

        [SetUp]
        public void SetUp()
        {
            disposed = new NativeArray<int>(1, Allocator.Persistent);
            pool     = new GeneratorBranchJobPool<CountingGenerator>();
        }

        [TearDown]
        public void TearDown()
        {
            pool.Dispose();
            foreach (var branch in branches)
            {
                if (branch.Valid)
                    branch.Destroy();
            }
            branches.Clear();
            disposed.Dispose();
        }

        CountingGenerator Generator() => new CountingGenerator { disposed = (int*)disposed.GetUnsafePtr() };

        // The pool takes ownership of the surface array as well, and disposes it with the generator
        static BlobAssetReference<InternalChiselSurfaceArray> Surfaces() => BrushMeshManager.BuildInternalSurfaceArrayBlob(1, Allocator.TempJob);

        CSGTreeBranch Branch()
        {
            var branch = CSGTreeBranch.Create();
            branches.Add(branch);
            return branch;
        }

        // The control: a generator that runs is disposed once, by the create job
        [Test]
        public void AGeneratorThatRuns_IsDisposedOnce()
        {
            pool.ScheduleUpdate(Branch(), Generator(), Surfaces());
            pool.ScheduleGenerateJob(runInParallel: false).Complete();
            Assert.That(disposed[0], Is.EqualTo(1));
        }

        // The same node scheduled twice before the pool runs: the second generator replaces the first
        [Test]
        public void AGeneratorReplacedBeforeItRuns_IsDisposed()
        {
            var branch = Branch();
            pool.ScheduleUpdate(branch, Generator(), Surfaces());
            pool.ScheduleUpdate(branch, Generator(), Surfaces());
            pool.ScheduleGenerateJob(runInParallel: false).Complete();
            Assert.That(disposed[0], Is.EqualTo(2), "two generators were handed to the pool");
        }

        // The node went away before the pool ran, so the pool drops its generator
        [Test]
        public void AGeneratorWhoseNodeIsGone_IsDisposed()
        {
            var branch = Branch();
            pool.ScheduleUpdate(branch, Generator(), Surfaces());
            var destroyed = branch;
            destroyed.Destroy();
            pool.ScheduleGenerateJob(runInParallel: false).Complete();
            Assert.That(disposed[0], Is.EqualTo(1));
        }

        // GeneratorJobPoolManager.Clear, at the end of every tree update, clears every pool
        [Test]
        public void AGeneratorStillWaitingWhenThePoolIsCleared_IsDisposed()
        {
            pool.ScheduleUpdate(Branch(), Generator(), Surfaces());
            pool.AllocateOrClear();
            Assert.That(disposed[0], Is.EqualTo(1));
        }

        // A domain reload disposes every pool
        [Test]
        public void AGeneratorStillWaitingWhenThePoolIsDisposed_IsDisposed()
        {
            pool.ScheduleUpdate(Branch(), Generator(), Surfaces());
            pool.Dispose();
            Assert.That(disposed[0], Is.EqualTo(1));
        }
    }
}
