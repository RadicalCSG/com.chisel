using System.Collections.Generic;
using NUnit.Framework;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;

namespace Chisel.Core.Tests
{
    // Each query's sub-meshes (SubMeshDescriptionJobs): a run of its surfaces with the same parameter, chunk, collider
    // layer and kind of decal, without the runs that have nothing to draw, with their counts and hashes, in query order
    public class SubMeshDescriptionsTests
    {
        static SubMeshSurface Surface(ulong parameter, uint chunk, int vertices, int indices, uint geometry, uint attributes, byte layer = 0) =>
            new SubMeshSurface { surfaceParameter = parameter, chunk = chunk, colliderLayer = layer, vertexCount = vertices, indexCount = indices,
                                 geometryHashValue = geometry, surfaceHashValue = attributes };

        // A run's description as the surfaces make it: the counts summed, the hashes chained in order
        static SubMeshDescriptions Expected(MeshQuery query, int queryIndex, int subMesh, int offset, params SubMeshSurface[] run)
        {
            var description = new SubMeshDescriptions
            {
                meshQuery = query, meshQueryIndex = queryIndex, subMeshQueryIndex = subMesh, surfacesOffset = offset, surfacesCount = run.Length,
                surfaceParameter = run[0].surfaceParameter, chunk = run[0].chunk, colliderLayer = run[0].colliderLayer
            };
            foreach (var surface in run)
            {
                description.vertexCount += surface.vertexCount;
                description.indexCount  += surface.indexCount;
                description.surfaceHashValue  = math.hash(new uint2(description.surfaceHashValue, surface.surfaceHashValue));
                description.geometryHashValue = math.hash(new uint2(description.geometryHashValue, surface.geometryHashValue));
            }
            return description;
        }

        [Test]
        public void EachQuerysSubMeshes_AreDescribedInQueryOrder([Values(false, true)] bool runInParallel)
        {
            var queries = new NativeArray<MeshQuery>(4, Allocator.TempJob);
            var lists   = new NativeArray<UnsafeList<SubMeshSurface>>(4, Allocator.TempJob);
            var descriptions = new NativeList<SubMeshDescriptions>(64, Allocator.TempJob);
            try
            {
                for (int t = 0; t < queries.Length; t++)
                    queries[t] = new MeshQuery((SurfaceDestinationFlags)(1 << t));
                var a = Surface(1, 0, 4, 6, 11, 21);
                var b = Surface(1, 0, 3, 3, 12, 22);
                var c = Surface(2, 0, 4, 6, 13, 23);
                var d = Surface(3, 0, 4, 6, 14, 24);
                var empty = Surface(4, 0, 0, 0, 15, 25);
                var e = Surface(5, 1, 4, 6, 16, 26);
                var f = Surface(5, 2, 4, 6, 17, 27);
                var g = Surface(5, 2, 4, 6, 18, 28, layer: 3);
                var surfaces = new[] { new[] { a, b, c }, new SubMeshSurface[0], new[] { d, empty, e, f, g }, new[] { c } };
                for (int t = 0; t < lists.Length; t++)
                {
                    var list = new UnsafeList<SubMeshSurface>(math.max(1, surfaces[t].Length), Allocator.TempJob);
                    foreach (var surface in surfaces[t])
                        list.Add(surface);
                    lists[t] = list;
                }

                SubMeshDescriptionJobs.Schedule(runInParallel, queries, lists, descriptions, default).Complete();

                var expected = new List<SubMeshDescriptions>
                {
                    Expected(queries[0], 0, 0, 0, a, b),
                    Expected(queries[0], 0, 1, 2, c),
                    Expected(queries[2], 2, 0, 0, d),
                    // the empty run has nothing to draw, but counts as a sub-mesh of the query
                    Expected(queries[2], 2, 2, 2, e),
                    Expected(queries[2], 2, 3, 3, f),
                    Expected(queries[2], 2, 4, 4, g),
                    Expected(queries[3], 3, 0, 0, c),
                };
                Assert.That(descriptions.Length, Is.EqualTo(expected.Count));
                for (int i = 0; i < expected.Count; i++)
                {
                    var made = descriptions[i];
                    var want = expected[i];
                    Assert.That((made.meshQuery.LayerQuery, made.meshQueryIndex, made.subMeshQueryIndex, made.surfacesOffset, made.surfacesCount, made.surfaceParameter,
                                 made.chunk, made.colliderLayer, made.transparentDecals, made.vertexCount, made.indexCount, made.geometryHashValue, made.surfaceHashValue),
                                Is.EqualTo((want.meshQuery.LayerQuery, want.meshQueryIndex, want.subMeshQueryIndex, want.surfacesOffset, want.surfacesCount, want.surfaceParameter,
                                            want.chunk, want.colliderLayer, want.transparentDecals, want.vertexCount, want.indexCount, want.geometryHashValue, want.surfaceHashValue)),
                                $"description {i}");
                }
            }
            finally
            {
                for (int t = 0; t < lists.Length; t++)
                {
                    if (lists[t].IsCreated)
                        lists[t].Dispose();
                }
                lists.Dispose(); queries.Dispose(); descriptions.Dispose();
            }
        }
    }
}
