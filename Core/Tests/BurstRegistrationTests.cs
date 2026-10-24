using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;

namespace Chisel.Core.Tests
{
    [TestFixture]
    public sealed class BurstRegistrationTests
    {
        static HashSet<System.Type> Registered()
        {
            return new HashSet<System.Type>(typeof(ChiselTreeLookup).Assembly.GetCustomAttributes<RegisterGenericJobTypeAttribute>()
                                                                              .Select(attribute => attribute.ConcreteType));
        }

        [Test]
        public void EveryBlobCacheOfATree_HasItsDisposeJobRegisteredForBurst()
        {
            var registered = Registered();
            var caches = new List<(string field, System.Type blob)>();
            foreach (var field in typeof(ChiselTreeLookup.Data).GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
            {
                var type = field.FieldType;
                if (!type.IsGenericType || type.GetGenericTypeDefinition() != typeof(NativeList<>))
                    continue;
                var element = type.GetGenericArguments()[0];
                if (!element.IsGenericType || element.GetGenericTypeDefinition() != typeof(BlobAssetReference<>))
                    continue;
                caches.Add((field.Name, element.GetGenericArguments()[0]));
            }
            System.Type DisposeJobOf(System.Type blob) => typeof(DisposeListChildrenBlobAssetReferenceJob<>).MakeGenericType(blob);

            // both sides are read: the caches are found, and the registrations of some of them as well
            Assert.That(caches, Is.Not.Empty, "no blob cache found in ChiselTreeLookup.Data");
            Assert.That(caches.Any(cache => registered.Contains(DisposeJobOf(cache.blob))), Is.True,
                        "no registration of DisposeListChildrenBlobAssetReferenceJob<T> read from com.chisel.core");

            var missing = caches.Where(cache => !registered.Contains(DisposeJobOf(cache.blob)))
                                .Select(cache => cache.field + " (" + cache.blob.Name + ")").ToList();
            Assert.That(missing, Is.Empty, "blob caches whose dispose job Burst cannot see: " + string.Join(", ", missing));
        }

        [Test]
        public void EveryGeneratorOfTheCore_HasItsJobsRegisteredForBurst()
        {
            var registered = Registered();
            var brushJobs  = new[] { typeof(CreateBrushesJob<>) };
            var branchJobs = new[] { typeof(BranchPrepareAndCountBrushesJob<>), typeof(BranchAllocateBrushesJob<>),
                                     typeof(BranchCreateBrushesJob<>), typeof(GeneratorBranchJobPool<>.InitializeArraysJob) };
            var generators = new List<System.Type>();
            var missing    = new List<string>();
            foreach (var type in typeof(ChiselTreeLookup).Assembly.GetTypes())
            {
                if (!type.IsValueType || type.IsGenericTypeDefinition)
                    continue;
                var jobs = typeof(IBranchGenerator).IsAssignableFrom(type) ? branchJobs
                         : typeof(IBrushGenerator).IsAssignableFrom(type)  ? brushJobs
                         : null;
                if (jobs == null)
                    continue;
                generators.Add(type);
                foreach (var job in jobs)
                {
                    if (!registered.Contains(job.MakeGenericType(type)))
                        missing.Add(type.Name + ": " + job.Name.Split('`')[0]);
                }
            }

            // both sides are read: the generators are found, and the registrations of some of them as well
            Assert.That(generators, Is.Not.Empty, "no generator found in com.chisel.core");
            Assert.That(missing.Count, Is.LessThan(generators.Sum(type => typeof(IBranchGenerator).IsAssignableFrom(type) ? branchJobs.Length : brushJobs.Length)),
                        "no registration of a generator's job read from com.chisel.core");

            Assert.That(missing, Is.Empty, "generator jobs Burst cannot see: " + string.Join(", ", missing));
        }

        [Test] public void ALinearStairs_IsGeneratedThroughItsJobs()  { Generate(new ChiselLinearStairsDefinition()); }
        [Test] public void APathedStairs_IsGeneratedThroughItsJobs()  { Generate(new ChiselPathedStairsDefinition()); }
        [Test] public void ARevolvedShape_IsGeneratedThroughItsJobs() { Generate(new ChiselRevolvedShapeDefinition()); }
        [Test] public void ASpiralStairs_IsGeneratedThroughItsJobs()  { Generate(new ChiselSpiralStairsDefinition()); }
        [Test] public void ATorus_IsGeneratedThroughItsJobs()         { Generate(new ChiselTorusDefinition()); }
        [Test] public void AnExtrudedShape_IsGeneratedThroughItsJobs(){ Generate(new ChiselExtrudedShapeDefinition()); }

        static void Generate<Generator>(SerializedBranchGenerator<Generator> definition)
            where Generator : unmanaged, IBranchGenerator
        {
            definition.Reset();
            Assert.That(definition.Validate(), Is.True, "the default definition is not valid");
            var pool = new GeneratorBranchJobPool<Generator>();
            var tree = CSGTree.Create(default(UnityEngine.EntityId));
            try
            {
                var branch = tree.CreateBranch();
                pool.ScheduleUpdate(branch, definition.GetBranchGenerator(),
                                    BrushMeshManager.BuildInternalSurfaceArrayBlob(definition.RequiredSurfaceCount, Allocator.TempJob));
                GeneratorJobPoolManager.ScheduleJobs(runInParallel: false).Complete();
                Assert.That(branch.Count, Is.GreaterThan(0), typeof(Generator).Name + " made no brushes");
            }
            finally
            {
                GeneratorJobPoolManager.Clear();
                pool.Dispose();
                if (tree.Valid)
                    tree.Destroy();
            }
        }
    }
}
