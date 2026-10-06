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
        // Gate the cross-brush snap on plane incidence (WeldIncidenceFilter); set from CSGManager's kUseIncidenceWeld.
        [NoAlias, ReadOnly] public bool useIncidenceWeld;
        // Canonical vertices (see CanonicalVertices): from Everywhere on, only the same vertex is merged here.
        [NoAlias, ReadOnly] public CanonicalVertexStage canonicalVertexStage;
        [NoAlias, ReadOnly] public NativeList<BlobAssetReference<BrushTreeSpacePlanes>> brushTreeSpacePlaneCache;
        [NoAlias, ReadOnly] public NativeList<BlobAssetReference<BasePolygonsBlob>>     basePolygonCache;
        [NoAlias, ReadOnly] public NativeList<BlobAssetReference<BrushTreeSpaceVerticesBlob>> treeSpaceVerticesArray;

        [NativeDisableParallelForRestriction]
        [NoAlias, ReadOnly] public NativeArray<UnsafeList<float3>>  loopVerticesLookup;

        // Write (own brush only): the snapped vertices for this pass.
        [NativeDisableParallelForRestriction]
        [NoAlias] public NativeArray<UnsafeList<float3>>            loopVerticesLookupOut;

        internal const int kRan     = 1;
        internal const int kChanged = 2;

        [NativeDisableParallelForRestriction]
        [NoAlias] public NativeArray<int>                           brushState;
        [NoAlias, ReadOnly] public int                              iterationIndex;

        // Per thread scratch memory
        [NativeDisableContainerSafetyRestriction] HashedVertices    mergeVertices;

        public void Execute(int b)
        {
            var brushIndexOrder = allUpdateBrushIndexOrders[b];
            int brushNodeOrder  = brushIndexOrder.nodeOrder;

            var brushIntersectionsBlob = brushesTouchedByBrushCache[brushNodeOrder];
            if (brushIntersectionsBlob == BlobAssetReference<BrushesTouchedByBrush>.Null)
                return;

            // NOTE: assumes brushIntersections is in the same order as the brushes are in the tree
            ref var brushIntersections = ref brushIntersectionsBlob.Value.brushIntersections;

            var brushCount = loopVerticesLookup.Length;
            if (iterationIndex > 0)
            {
                var prevRow = (iterationIndex - 1) * brushCount;
                var dirty   = (brushState[prevRow + brushNodeOrder] & kChanged) != 0;
                for (int i = 0; !dirty && i < brushIntersections.Length; i++)
                {
                    var neighbourOrder = brushIntersections[i].nodeIndexOrder.nodeOrder;
                    if (neighbourOrder >= 0 && neighbourOrder < brushNodeOrder &&
                        (brushState[prevRow + neighbourOrder] & kChanged) != 0)
                        dirty = true;
                }
                if (!dirty)
                    return;
            }

            // A snap may pull a vertex onto a neighbour's, but not off the faces of its own brush: that is what collapsed
            // a 19 mm slab onto a neighbour vertex lying between its two faces.
            var weldFilter = WeldIncidenceFilter.Disabled;
            if (canonicalVertexStage >= CanonicalVertexStage.Everywhere)
                weldFilter = WeldIncidenceFilter.SameVertexOnly;   // canonical vertices: only the same vertex is merged
            else
            if (useIncidenceWeld &&
                brushTreeSpacePlaneCache[brushNodeOrder].IsCreated && basePolygonCache[brushNodeOrder].IsCreated)
                weldFilter = WeldIncidenceFilter.Create(ref brushTreeSpacePlaneCache[brushNodeOrder].Value.treeSpacePlanes,
                                                        basePolygonCache[brushNodeOrder].Value.polygons.Length);

            var vertices = loopVerticesLookup[brushIndexOrder.nodeOrder];

            mergeVertices = new HashedVertices(math.max(vertices.Length, 1000), Allocator.Temp);
            //NativeCollectionHelpers.EnsureCapacityAndClear(ref mergeVertices, math.max(vertices.Length, 1000));
            try
            {
                mergeVertices.AddUniqueVertices(in vertices);

                var mergeMin = new float3(float.PositiveInfinity);
                var mergeMax = new float3(float.NegativeInfinity);
                for (int i = 0; i < vertices.Length; i++)
                {
                    mergeMin = math.min(mergeMin, vertices[i]);
                    mergeMax = math.max(mergeMax, vertices[i]);
                }
                mergeMin -= HashedVertices.kCellSize;
                mergeMax += HashedVertices.kCellSize;

                for (int i = 0; i < brushIntersections.Length; i++)
                {
                    var intersectingNodeOrder = brushIntersections[i].nodeIndexOrder.nodeOrder;
                    if (intersectingNodeOrder > brushNodeOrder ||
                        !loopVerticesLookup[intersectingNodeOrder].IsCreated)
                        continue;

                    // In order, goes through the previous brushes in the tree,
                    // and snaps any vertex that is almost the same in the next brush, with that vertex
                    ref var intersectingVertices = ref treeSpaceVerticesArray[intersectingNodeOrder].Value.treeSpaceVertices;
                    mergeVertices.ReplaceIfExists(ref intersectingVertices, mergeMin, mergeMax);
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
                    mergeVertices.ReplaceIfExists(in intersectingVertices, mergeMin, mergeMax);
                }


                var outList = loopVerticesLookupOut[brushNodeOrder];
                if (!outList.IsCreated)
                    outList = new UnsafeList<float3>(vertices.Length, Allocator.Persistent);
                else
                    outList.Clear();

                var snapped = new NativeArray<float3>(vertices.Length, Allocator.Temp);
                for (int i = 0; i < vertices.Length; i++)
                    snapped[i] = mergeVertices.GetUniqueVertex(vertices[i]);

                var claimHash    = new HashedVertices(math.max(vertices.Length, 1), Allocator.Temp);
                var claimFirst   = new UnsafeList<float3>(vertices.Length, Allocator.Temp); // first original to claim each merged slot
                var claimCollide = new UnsafeList<bool>  (vertices.Length, Allocator.Temp); // slot claimed by >=2 distinct originals
                var claimIdx     = new NativeArray<int>(vertices.Length, Allocator.Temp);
                for (int i = 0; i < vertices.Length; i++)
                {
                    int ci = claimHash.AddNoResize(snapped[i]);
                    claimIdx[i] = ci;
                    if (ci >= claimFirst.Length)
                    {
                        claimFirst.Add(vertices[i]);
                        claimCollide.Add(false);
                    }
                    else if (math.distancesq(claimFirst[ci], vertices[i]) > CSGConstants.kSqrVertexEqualEpsilon)
                    {
                        claimCollide[ci] = true; // a DISTINCT original wants the same merged slot -> collapse
                    }
                }
                var changed = false;
                for (int i = 0; i < vertices.Length; i++)
                {
                    var merged = claimCollide[claimIdx[i]] ? vertices[i] : snapped[i];
                    if (weldFilter.IsEnabled && !weldFilter.Allows(vertices[i], merged)) merged = vertices[i];
                    outList.Add(merged);
                    if (!changed && !merged.Equals(vertices[i]))
                        changed = true;
                }
                brushState[(iterationIndex * brushCount) + brushNodeOrder] = changed ? (kRan | kChanged) : kRan;

                snapped.Dispose();
                claimHash.Dispose();
                claimFirst.Dispose();
                claimCollide.Dispose();
                claimIdx.Dispose();
                loopVerticesLookupOut[brushNodeOrder] = outList;
            }
            finally
            {
                mergeVertices.Dispose();
            }
        }
    }

    [BurstCompile(CompileSynchronously = true)]
    struct CopyBackLoopVerticesJob : IJobParallelForDefer
    {
        [NoAlias, ReadOnly] public NativeList<IndexOrder>          allUpdateBrushIndexOrders;
        [NativeDisableParallelForRestriction]
        [NoAlias, ReadOnly] public NativeArray<UnsafeList<float3>> loopVerticesLookupOut;
        [NativeDisableParallelForRestriction]
        [NoAlias] public NativeArray<UnsafeList<float3>>           loopVerticesLookup;

        // Written by the merge pass this follows: a brush that pass skipped left its out-buffer holding
        // what loopVerticesLookup already contains, so copying it back would be a no-op.
        [NoAlias, ReadOnly] public NativeArray<int>                brushState;
        [NoAlias, ReadOnly] public int                             iterationIndex;

        public void Execute(int b)
        {
            var brushNodeOrder = allUpdateBrushIndexOrders[b].nodeOrder;

            var brushCount = loopVerticesLookup.Length;
            if ((brushState[(iterationIndex * brushCount) + brushNodeOrder] &
                 MergeTouchingBrushVerticesIndirectJob.kRan) == 0)
                return;

            var src = loopVerticesLookupOut[brushNodeOrder];
            if (!src.IsCreated)
                return;
            var dst = loopVerticesLookup[brushNodeOrder];
            if (!dst.IsCreated)
                return;
            dst.Clear();
            dst.AddRange(src);
            loopVerticesLookup[brushNodeOrder] = dst;
        }
    }
}
