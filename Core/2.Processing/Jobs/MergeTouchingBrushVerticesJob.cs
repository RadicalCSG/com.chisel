using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Debug = UnityEngine.Debug;
using ReadOnlyAttribute = Unity.Collections.ReadOnlyAttribute;
using WriteOnlyAttribute = Unity.Collections.WriteOnlyAttribute;

namespace Chisel.Core
{/*
    [BurstCompile(CompileSynchronously = true)]
    struct MergeTouchingBrushVerticesJob : IJobParallelForDefer
    {
        // Read
        [NoAlias, ReadOnly] public NativeArray<IndexOrder>                                  treeBrushIndexOrders;
        [NoAlias, ReadOnly] public NativeArray<BlobAssetReference<BrushesTouchedByBrush>>   brushesTouchedByBrushes;

        // Read/Write
        [NativeDisableParallelForRestriction]
        [NoAlias] public NativeArray<BlobAssetReference<BrushTreeSpaceVerticesBlob>>        treeSpaceVerticesArray;

        // Per thread scratch memory
        //[NativeDisableContainerSafetyRestriction] HashedVertices mergeVertices;

        public void Execute(int b)
        {
            var brushIndexOrder = treeBrushIndexOrders[b];
            int brushNodeOrder  = brushIndexOrder.nodeOrder;

            var brushIntersectionsBlob = brushesTouchedByBrushes[brushNodeOrder];
            if (brushIntersectionsBlob == BlobAssetReference<BrushesTouchedByBrush>.Null)
                return;
            var treeSpaceVerticesBlob = treeSpaceVerticesArray[brushIndexOrder.nodeOrder];
            if (treeSpaceVerticesBlob == BlobAssetReference<BrushTreeSpaceVerticesBlob>.Null)
                return;
            ref var vertices  = ref treeSpaceVerticesBlob.Value.treeSpaceVertices;
    
            var mergeVertices = new HashedVertices(math.max(vertices.Length, 1000), Allocator.Temp);
            //NativeCollectionHelpers.EnsureCapacityAndClear(ref mergeVertices, math.max(vertices.Length, 1000));
            try
            {
                mergeVertices.AddUniqueVertices(ref vertices);

                // NOTE: assumes brushIntersections is in the same order as the brushes are in the tree
                ref var brushIntersections = ref brushIntersectionsBlob.Value.brushIntersections;
                for (int i = 0; i < brushIntersections.Length; i++)
                {
                    var intersectingNodeOrder = brushIntersections[i].nodeIndexOrder.nodeOrder;
                    if (intersectingNodeOrder > brushNodeOrder)
                        continue;

                    // In order, goes through the previous brushes in the tree, 
                    // and snaps any vertex that is almost the same in the next brush, with that vertex
                    ref var intersectingVertices = ref treeSpaceVerticesArray[intersectingNodeOrder].Value.treeSpaceVertices;
                    mergeVertices.ReplaceIfExists(ref intersectingVertices);
                }


                for (int i = 0; i < vertices.Length; i++)
                {
                    vertices[i] = mergeVertices.GetUniqueVertex(vertices[i]);
                }

                //treeSpaceVerticesLookup.TryAdd(brushNodeIndex, treeSpaceVerticesBlob);
            }
            finally
            {
                mergeVertices.Dispose();
            }
        }
    }
    */
	[BurstCompile(CompileSynchronously = true)]
    struct MergeTouchingBrushVerticesIndirectJob : IJobParallelForDefer
    {
        // Read
        [NoAlias, ReadOnly] public NativeList<IndexOrder>                                     allUpdateBrushIndexOrders;
        [NoAlias, ReadOnly] public NativeList<BlobAssetReference<BrushesTouchedByBrush>>      brushesTouchedByBrushCache;
        [NoAlias, ReadOnly] public NativeList<BlobAssetReference<BrushTreeSpaceVerticesBlob>> treeSpaceVerticesArray;

        // Read Write
        [NativeDisableParallelForRestriction]
        [NoAlias] public NativeArray<UnsafeList<float3>>            loopVerticesLookup;

        // Per thread scratch memory
        [NativeDisableContainerSafetyRestriction] HashedVertices    mergeVertices;

        public void Execute(int b)
        {
            var brushIndexOrder = allUpdateBrushIndexOrders[b];
            int brushNodeOrder  = brushIndexOrder.nodeOrder;

            var brushIntersectionsBlob = brushesTouchedByBrushCache[brushNodeOrder];
            if (brushIntersectionsBlob == BlobAssetReference<BrushesTouchedByBrush>.Null)
                return;
            
            var vertices = loopVerticesLookup[brushIndexOrder.nodeOrder];

            mergeVertices = new HashedVertices(math.max(vertices.Length, 1000), Allocator.Temp);
            //NativeCollectionHelpers.EnsureCapacityAndClear(ref mergeVertices, math.max(vertices.Length, 1000));
            try
            {
                mergeVertices.AddUniqueVertices(in vertices);

                // NOTE: assumes brushIntersections is in the same order as the brushes are in the tree
                ref var brushIntersections = ref brushIntersectionsBlob.Value.brushIntersections;
                for (int i = 0; i < brushIntersections.Length; i++)
                {
                    var intersectingNodeOrder = brushIntersections[i].nodeIndexOrder.nodeOrder;
                    if (intersectingNodeOrder > brushNodeOrder ||
                        !loopVerticesLookup[intersectingNodeOrder].IsCreated)
                        continue;

                    // In order, goes through the previous brushes in the tree, 
                    // and snaps any vertex that is almost the same in the next brush, with that vertex
                    ref var intersectingVertices = ref treeSpaceVerticesArray[intersectingNodeOrder].Value.treeSpaceVertices;
                    mergeVertices.ReplaceIfExists(ref intersectingVertices);
                }

                // NOTE: assumes brushIntersections is in the same order as the brushes are in the tree
                for (int i = 0; i < brushIntersections.Length; i++)
                {
                    var intersectingNodeOrder = brushIntersections[i].nodeIndexOrder.nodeOrder;
                    if (intersectingNodeOrder > brushNodeOrder ||
                        !loopVerticesLookup[intersectingNodeOrder].IsCreated)
                        continue;

                    // In order, goes through the previous brushes in the tree, 
                    // and snaps any vertex that is almost the same in the next brush, with that vertex
                    var intersectingVertices = loopVerticesLookup[intersectingNodeOrder];
                    mergeVertices.ReplaceIfExists(in intersectingVertices);
                }


                for (int i = 0; i < vertices.Length; i++)
                {
                    vertices[i] = mergeVertices.GetUniqueVertex(vertices[i]);
                }

                //treeSpaceVerticesLookup.TryAdd(brushNodeIndex, treeSpaceVerticesBlob);
            }
            finally
            {
                mergeVertices.Dispose();
            }
        }
    }
}
