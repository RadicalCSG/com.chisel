using System;
using Unity.Mathematics;
using Unity.Jobs;
using Unity.Collections;
using Unity.Burst;
using Unity.Collections.LowLevel.Unsafe;
using Debug = UnityEngine.Debug;
using ReadOnlyAttribute = Unity.Collections.ReadOnlyAttribute;
using WriteOnlyAttribute = Unity.Collections.WriteOnlyAttribute;
using Unity.Entities;

namespace Chisel.Core
{
    [BurstCompile(CompileSynchronously = true)]
    struct PerformCSGJob : IJobParallelForDefer
    {
        // Read
        // 'Required' for scheduling with index count
        [NoAlias, ReadOnly] public NativeList<IndexOrder>                                allUpdateBrushIndexOrders;        

        // Gate the vertex re-weld on plane incidence (WeldIncidenceFilter); set from CSGManager's kUseIncidenceWeld.
        [NoAlias, ReadOnly] public bool useIncidenceWeld;
        // Canonical vertices (see CanonicalVertices): from Everywhere on, only the same vertex is merged here.
        [NoAlias, ReadOnly] public CanonicalVertexStage canonicalVertexStage;
        [NoAlias, ReadOnly] public NativeList<BlobAssetReference<RoutingTable>>          routingTableCache;
        [NoAlias, ReadOnly] public NativeList<BlobAssetReference<BrushTreeSpacePlanes>>  brushTreeSpacePlaneCache;
        [NoAlias, ReadOnly] public NativeList<BlobAssetReference<BrushesTouchedByBrush>> brushesTouchedByBrushCache;
        [NoAlias, ReadOnly] public NativeArray<UnsafeList<float3>>                       loopVerticesLookup;
        // Each brush's contents and whether it carves, for the contents rules
        [NoAlias, ReadOnly] public NativeReference<BlobAssetReference<CompactTree>>      compactTreeRef;

        [NoAlias, ReadOnly] public NativeStream.Reader      input;
        
        // Write
        [NoAlias, WriteOnly] public NativeStream.Writer     output;

        // Per thread scratch memory
        /*
        [NativeDisableContainerSafetyRestriction, NoAlias] NativeArray<EdgeCategory>     categories1;
        [NativeDisableContainerSafetyRestriction, NoAlias] NativeArray<EdgeCategory>     categories2;
        [NativeDisableContainerSafetyRestriction, NoAlias] NativeArray<Edge>             outEdges;
        [NativeDisableContainerSafetyRestriction, NoAlias] NativeArray<int>              intersectedHoleIndices;
        [NativeDisableContainerSafetyRestriction, NoAlias] NativeArray<IndexSurfaceInfo> intersectionSurfaceInfo;
        [NativeDisableContainerSafetyRestriction, NoAlias] NativeBitArray                destroyedEdges;        
        [NativeDisableContainerSafetyRestriction, NoAlias] NativeList<IndexSurfaceInfo>  allInfos;
        [NativeDisableContainerSafetyRestriction, NoAlias] NativeList<IndexSurfaceInfo>  intersectionSurfaceInfos;
        [NativeDisableContainerSafetyRestriction, NoAlias] NativeList<IndexSurfaceInfo>  basePolygonSurfaceInfos;
        [NativeDisableContainerSafetyRestriction, NoAlias] NativeList<float4>            alltreeSpacePlanes;
        [NativeDisableContainerSafetyRestriction, NoAlias] NativeList<LoopSegment>       allSegments;
        [NativeDisableContainerSafetyRestriction, NoAlias] NativeList<Edge>              allCombinedEdges;
        [NativeDisableContainerSafetyRestriction, NoAlias] NativeList<UnsafeList<int>>   holeIndices;
        [NativeDisableContainerSafetyRestriction, NoAlias] NativeList<UnsafeList<int>>   surfaceLoopIndices;
        [NativeDisableContainerSafetyRestriction, NoAlias] NativeList<UnsafeList<Edge>>  allEdges;
        [NativeDisableContainerSafetyRestriction, NoAlias] NativeList<UnsafeList<Edge>>  intersectionLoops;
        [NativeDisableContainerSafetyRestriction, NoAlias] NativeList<UnsafeList<Edge>>  basePolygonEdges;
        [NativeDisableContainerSafetyRestriction, NoAlias] HashedVertices                hashedTreeSpaceVertices;
        [NativeDisableContainerSafetyRestriction, NoAlias] NativeArray<ushort>           indexRemap;
        [NativeDisableContainerSafetyRestriction, NoAlias] NativeList<UnsafeList<Edge>>  intersectionEdges;
        */

        [BurstDiscard]
        private static void NotUniqueEdgeException() 
        {
            throw new Exception("Edge is not unique");
        }
        
        static bool AddEdgesNoResize(ref UnsafeList<Edge> dstEdges, in UnsafeList<Edge> srcEdges)
        {
            if (srcEdges.Length == 0)  
                return false;

            for (int ae = srcEdges.Length - 1; ae >= 0; ae--)
            {
                var addEdge = srcEdges[ae];
                for (int e = dstEdges.Length - 1; e >= 0; )
                {
                    if (addEdge.Equals(dstEdges[e]))
                    {
                        NotUniqueEdgeException();
                        dstEdges.RemoveAtSwapBack(e);
                    } else
                        e--;
                }
            }

            bool duplicates = false;

            for (int v = 0; v < srcEdges.Length;v++)
            {
                var addEdge = srcEdges[v];
                var index = IndexOf(dstEdges, addEdge, out bool _);
                if (index != -1)
                {
                    //Debug.Log($"Duplicate edge {inverted}  {values[v].index1}/{values[v].index2} {edges[index].index1}/{edges[index].index2}");
                    duplicates = true;
                    continue;
                }
                dstEdges.AddNoResize(addEdge);
            }

            return duplicates;
        }

        static int IndexOf(UnsafeList<Edge> edges, Edge edge, out bool inverted)
        {
            //var builder = new System.Text.StringBuilder();
            for (int e = 0; e < edges.Length; e++)
            {
                //builder.AppendLine($"{e}/{edges.Count}: {edges[e]} {edge}");
                if (edges[e].index1 == edge.index1 && edges[e].index2 == edge.index2) { inverted = false; return e; }
                if (edges[e].index1 == edge.index2 && edges[e].index2 == edge.index1) { inverted = true; return e; }
            }
            //Debug.Log(builder.ToString());
            inverted = false;
            return -1;
        }

        // Both endpoints of `edge` appear somewhere on `loop`.
        static bool BoundedByLoop(Edge edge, [NoAlias] in UnsafeList<Edge> loop)
        {
            bool first = false, second = false;
            for (int e = 0; e < loop.Length; e++)
            {
                var index1 = loop[e].index1;
                var index2 = loop[e].index2;
                if (index1 == edge.index1 || index2 == edge.index1) first = true;
                if (index1 == edge.index2 || index2 == edge.index2) second = true;
                if (first && second)
                    return true;
            }
            return false;
        }

        static UnsafeList<Edge> BuildCutLoop([NoAlias] in NativeArray<Edge> outEdges, int outEdgesLength)
        {
            var edges = new UnsafeList<Edge>(outEdgesLength, Allocator.Temp);
            edges.AddRangeNoResize(outEdges, outEdgesLength);

            if (edges.Length >= 3)
                LoopEdgeSplitter.RemoveAntiparallelEdgePairs(ref edges);
            if (kRemoveSimpleChords && edges.Length >= 3)
                LoopEdgeSplitter.RemoveSimpleChords(ref edges);
            if (kCloseOpenChains && edges.Length >= 2)
                LoopEdgeSplitter.CloseSingleOpenChain(ref edges);

            if (edges.Length >= 3)
                LoopEdgeSplitter.RewindClosedLoop(ref edges);
            return edges;
        }

        const float kSameRegionEpsilon = CSGConstants.kVertexEqualEpsilon * 2;

        const float kCanonicalSameRegionEpsilon = CSGConstants.kFatPlaneWidthEpsilon;

        static bool LoopsOutlineSameRegion([NoAlias] in UnsafeList<Edge> loopA, [NoAlias] in UnsafeList<Edge> loopB,
                                           [NoAlias] in HashedVertices vertices, float epsilon)
        {
            if (loopA.Length < 3 || loopB.Length < 3)
                return false;
            return AllCornersOnBoundary(in loopA, in loopB, in vertices, epsilon)
                && AllCornersOnBoundary(in loopB, in loopA, in vertices, epsilon);
        }

        static bool AllCornersOnBoundary([NoAlias] in UnsafeList<Edge> loopA, [NoAlias] in UnsafeList<Edge> loopB,
                                         [NoAlias] in HashedVertices vertices, float epsilon)
        {
            for (int a = 0; a < loopA.Length; a++)
            {
                if (!PointOnLoopBoundary(vertices[loopA[a].index1], in loopB, in vertices, epsilon) ||
                    !PointOnLoopBoundary(vertices[loopA[a].index2], in loopB, in vertices, epsilon))
                    return false;
            }
            return true;
        }

        static bool PointOnLoopBoundary(float3 position, [NoAlias] in UnsafeList<Edge> loop,
                                        [NoAlias] in HashedVertices vertices, float epsilon)
        {
            var sqrEpsilon = epsilon * epsilon;
            for (int e = 0; e < loop.Length; e++)
            {
                var from = vertices[loop[e].index1];
                if (math.distancesq(from, position) <= sqrEpsilon)
                    return true;
                var to = vertices[loop[e].index2];
                if (math.distancesq(to, position) <= sqrEpsilon)
                    return true;

                // strictly between the endpoints: the other loop samples this stretch of boundary
                // with a vertex that this one does not have
                var delta    = to - from;
                var lengthSq = math.lengthsq(delta);
                if (lengthSq <= 1e-12f)
                    continue;
                var t = math.dot(position - from, delta) / lengthSq;
                if (t <= 0 || t >= 1)
                    continue;
                if (math.distancesq(from + delta * t, position) <= sqrEpsilon)
                    return true;
            }
            return false;
        }

        void IntersectLoops([NoAlias] in HashedVertices                 hashedTreeSpaceVertices,

                            [NoAlias] ref UnsafeList<int>               loopIndices, 
                            int                                         surfaceLoopIndex,

                            [NoAlias] ref NativeList<UnsafeList<int>>   holeIndices,
                            [NoAlias] ref NativeList<IndexSurfaceInfo>  allInfos,
                            [NoAlias] ref NativeList<UnsafeList<Edge>>  allEdges,

							[NoAlias] ref NativeArray<Edge>             outEdges,
							[NoAlias] ref NativeArray<int>              intersectedHoleIndices,
                            [NoAlias] ref NativeArray<EdgeCategory>     categories1,
                            [NoAlias] ref NativeArray<EdgeCategory>     categories2,


							[NoAlias] in UnsafeList<Edge>               intersectionLoop, 
                            ushort                                      intersectionCategory,
                            IndexSurfaceInfo                            intersectionInfo)
        {
            if (intersectionLoop.Length == 0)
                return;

            //Debug.Assert(allEdges.Length == allInfos.Length);
            //Debug.Assert(allInfos.Length == holeIndices.Length);

            var currentLoopEdges    = allEdges[surfaceLoopIndex];
            var currentInfo         = allInfos[surfaceLoopIndex];
            var currentHoleIndices  = holeIndices[surfaceLoopIndex];

            // It might look like we could just set the interiorCategory of brush_intersection here, and let all other cut loops copy from it below,
            // but the same brush_intersection might be used by another categorized_loop and then we'd try to reroute it again, which wouldn't work
            //brush_intersection.interiorCategory = newHoleCategory;

            if (currentLoopEdges.Length == 0)
                return;

            var maxLength = math.max(16, intersectionLoop.Length + currentLoopEdges.Length);
            if (maxLength < 3)
                return;

            NativeCollectionHelpers.EnsureMinimumSize(ref categories1, intersectionLoop.Length);
            NativeCollectionHelpers.EnsureMinimumSize(ref categories2, currentLoopEdges.Length);

            int inside1 = 0, outside1 = 0;
            int currentBrushOrder = currentInfo.brushIndexOrder.nodeOrder;
            ref var treeSpacePlanes2 = ref brushTreeSpacePlaneCache[currentBrushOrder].Value.treeSpacePlanes;
            for (int e = 0; e < intersectionLoop.Length; e++)
            {
                var category = BooleanEdgesUtility.CategorizeEdge(intersectionLoop[e], ref treeSpacePlanes2, in currentLoopEdges, in hashedTreeSpaceVertices);
                categories1[e] = category;
                if      (category == EdgeCategory.Inside) inside1++;
                else if (category == EdgeCategory.Outside) outside1++;
            }
            var aligned1 = intersectionLoop.Length - (inside1 + outside1);

            int intersectionBrushOrder  = intersectionInfo.brushIndexOrder.nodeOrder;
            ref var treeSpacePlanes1    = ref brushTreeSpacePlaneCache[intersectionBrushOrder].Value.treeSpacePlanes;
            int inside2 = 0, outside2 = 0;
            for (int e = 0; e < currentLoopEdges.Length; e++)
            {
                var category = BooleanEdgesUtility.CategorizeEdge(currentLoopEdges[e], ref treeSpacePlanes1, in intersectionLoop, in hashedTreeSpaceVertices);
                categories2[e] = category;
                if      (category == EdgeCategory.Inside) inside2++;
                else if (category == EdgeCategory.Outside) outside2++;
            }
            var aligned2 = currentLoopEdges.Length - (inside2 + outside2);

            // Completely outside
            if ((inside1 + aligned1) == 0 && (aligned2 + inside2) == 0)
                return;

            if ((inside1 + (inside2 + aligned2)) < 3)
                return;

            // Completely aligned
            if (((outside1 + inside1) == 0 && (outside2 + inside2) == 0) ||
                // polygon1 edges Completely inside polygon2
                (inside1 == 0 && outside2 == 0))
            {
                // New polygon overrides the existing polygon
                currentInfo.interiorCategory = intersectionCategory;
                allInfos[surfaceLoopIndex] = currentInfo;
                //Debug.Assert(holeIndices.IsAllocated(surfaceLoopIndex));
                return;
            }

            var sameRegionEpsilon = canonicalVertexStage >= CanonicalVertexStage.Everywhere ? kCanonicalSameRegionEpsilon
                                                                                             : kSameRegionEpsilon;
            if (LoopsOutlineSameRegion(in intersectionLoop, in currentLoopEdges, in hashedTreeSpaceVertices, sameRegionEpsilon))
            {
                currentInfo.interiorCategory = intersectionCategory;
                allInfos[surfaceLoopIndex] = currentInfo;
                return;
            }

            NativeCollectionHelpers.EnsureMinimumSize(ref outEdges, maxLength);

            //var outEdges        = stackalloc Edge[maxLength];
            var outEdgesLength  = 0;

            // polygon2 edges Completely inside polygon1
            if (outside1 == 0 && inside2 == 0)
            {
                // polygon1 Completely inside polygon2
                for (int n = 0; n < intersectionLoop.Length; n++)
                {
                    outEdges[outEdgesLength] = intersectionLoop[n];
                    outEdgesLength++;
                }
                //OperationResult.Polygon1InsidePolygon2;
            } else
            {
                //int outEdgesLength = 0; // Can't read from outEdges.Length since it's marked as WriteOnly
                for (int e = 0; e < intersectionLoop.Length; e++)
                {
                    var category = categories1[e];
                    if (category == EdgeCategory.Inside)
                    {
                        outEdges[outEdgesLength] = intersectionLoop[e];
                        outEdgesLength++;
                    }
                }

                for (int e = 0; e < currentLoopEdges.Length; e++)
                {
                    var category = categories2[e];
                    if (category == EdgeCategory.Outside)
                        continue;

                    if (category == EdgeCategory.Inside &&
                        !BoundedByLoop(currentLoopEdges[e], in intersectionLoop))
                        continue;

                    outEdges[outEdgesLength] = currentLoopEdges[e];
                    outEdgesLength++;
                }
                //OperationResult.Cut;
            }

            if (outEdgesLength < 3)
                return;

            if (EnclosesNoArea(in outEdges, outEdgesLength, in hashedTreeSpaceVertices))
                return;

            // the output of cutting operations are both holes for the original polygon (categorized_loop)
            // and new polygons on the surface of the brush that need to be categorized
            intersectionInfo.interiorCategory = intersectionCategory;


            var brushesTouchedByBrush = brushesTouchedByBrushCache[currentBrushOrder];
            if (currentHoleIndices.Length > 0 &&
                // TODO: fix touching not being updated properly
                brushesTouchedByBrush != BlobAssetReference<BrushesTouchedByBrush>.Null)
            {
                NativeCollectionHelpers.EnsureMinimumSize(ref intersectedHoleIndices, currentHoleIndices.Length);
                var intersectedHoleIndicesLength = 0;

                // the output of cutting operations are both holes for the original polygon (categorized_loop)
                // and new polygons on the surface of the brush that need to be categorized

                ref var brushesTouchedByBrushRef    = ref brushesTouchedByBrush.Value;
                ref var brushIntersections          = ref brushesTouchedByBrushRef.brushIntersections;
                for (int h = 0; h < currentHoleIndices.Length; h++)
                {
                    // Need to make a copy so we can edit it without causing side effects
                    var holeIndex = currentHoleIndices[h];
                    var holeEdges = allEdges[holeIndex];
                    if (holeEdges.Length < 3)
                        continue;

                    var holeInfo            = allInfos[holeIndex];
                    var holeBrushNodeID     = holeInfo.brushIndexOrder.compactNodeID;

                    bool touches = brushesTouchedByBrushRef.Get(holeBrushNodeID) != IntersectionType.NoIntersection;
                    
                    // Only add if they touch
                    if (touches)
                    {
                        intersectedHoleIndices[intersectedHoleIndicesLength] = allEdges.Length;
                        intersectedHoleIndicesLength++;
                        holeIndices.Add(new UnsafeList<int>(1, Allocator.Temp));
                        //if (allInfos.Capacity < allInfos.Length + 1)
                        //    allInfos.Capacity = allInfos.Length + 16;
                        //allInfos.AddNoResize(holeInfo);
                        allInfos.Add(holeInfo);
                        var newHoleEdges = new UnsafeList<Edge>(holeEdges.Length, Allocator.Temp);
                        newHoleEdges.AddRangeNoResize(holeEdges);
                        allEdges.Add(newHoleEdges);
                        //Debug.Assert(allEdges.Length == allInfos.Length);
                        //Debug.Assert(allInfos.Length == holeIndices.Length);
                        //Debug.Assert(holeIndices.IsAllocated(allInfos.Length - 1));
                    }
                }

                // This loop is a hole 
                //if (currentHoleIndices.Capacity < currentHoleIndices.Length + 1) // TODO: figure out why capacity is sometimes not enough
                //    currentHoleIndices.Capacity = currentHoleIndices.Length + 16;
                //currentHoleIndices.AddNoResize(allEdges.Length);
                currentHoleIndices.Add(allEdges.Length);
                holeIndices[surfaceLoopIndex] = currentHoleIndices;
                holeIndices.Add(new UnsafeList<int>(1, Allocator.Temp));
                //if (allInfos.Capacity < allInfos.Length + 1)
                //    allInfos.Capacity = allInfos.Length + 16; 
                //allInfos.AddNoResize(intersectionInfo);
                allInfos.Add(intersectionInfo);
                var newOutEdges = BuildCutLoop(in outEdges, outEdgesLength);
                allEdges.Add(newOutEdges);
                //Debug.Assert(allEdges.Length == allInfos.Length);
                //Debug.Assert(allInfos.Length == holeIndices.Length);
                //Debug.Assert(holeIndices.IsAllocated(allInfos.Length - 1));

                // But also a polygon on its own
                //if (loopIndices.Capacity < loopIndices.Length + 1) // TODO: figure out why capacity is sometimes not enough
                //    loopIndices.Capacity = loopIndices.Length + 16;
                //loopIndices.AddNoResize(allEdges.Length);
                loopIndices.Add(allEdges.Length);
                var newHoleIndices = new UnsafeList<int>(intersectedHoleIndicesLength, Allocator.Temp);
                newHoleIndices.AddRangeNoResize(intersectedHoleIndices, intersectedHoleIndicesLength);
                holeIndices.Add(newHoleIndices);
                //if (allInfos.Capacity < allInfos.Length + 1)
                //    allInfos.Capacity = allInfos.Length + 16; 
                //allInfos.AddNoResize(intersectionInfo);
                allInfos.Add(intersectionInfo);
                newOutEdges = BuildCutLoop(in outEdges, outEdgesLength);
                allEdges.Add(newOutEdges);
                //Debug.Assert(allEdges.Length == allInfos.Length);
                //Debug.Assert(allInfos.Length == holeIndices.Length);
                //Debug.Assert(holeIndices.IsAllocated(allEdges.Length - 1));
            } else
            {
                // This loop is a hole 
                //currentHoleIndices.AddNoResize(allEdges.Length);
                currentHoleIndices.Add(allEdges.Length);
                holeIndices[surfaceLoopIndex] = currentHoleIndices;
                holeIndices.Add(new UnsafeList<int>(1, Allocator.Temp));
                //if (allInfos.Capacity < allInfos.Length + 1)
                //    allInfos.Capacity = allInfos.Length + 16;
                //allInfos.AddNoResize(intersectionInfo);
                allInfos.Add(intersectionInfo);
                var newOutEdges = BuildCutLoop(in outEdges, outEdgesLength);
                allEdges.Add(newOutEdges);
                //Debug.Assert(allEdges.Length == allInfos.Length);
                //Debug.Assert(allInfos.Length == holeIndices.Length);
                //Debug.Assert(holeIndices.IsAllocated(allInfos.Length - 1));

                // But also a polygon on its own
                //if (loopIndices.Capacity < loopIndices.Length + 1) // TODO: figure out why capacity is sometimes not enough
                //    loopIndices.Capacity = loopIndices.Length + 16;
                //loopIndices.AddNoResize(allEdges.Length);
                loopIndices.Add(allEdges.Length);
                holeIndices.Add(new UnsafeList<int>(1, Allocator.Temp));
                if (allInfos.Capacity < allInfos.Length + 1)
                    allInfos.Capacity = allInfos.Length + 16;
                allInfos.AddNoResize(intersectionInfo);
                newOutEdges = BuildCutLoop(in outEdges, outEdgesLength);
                allEdges.Add(newOutEdges);
                //Debug.Assert(allEdges.Length == allInfos.Length);
                //Debug.Assert(allInfos.Length == holeIndices.Length);
                //Debug.Assert(holeIndices.IsAllocated(allInfos.Length - 1));
            }
        }

        static bool EnclosesNoArea([NoAlias] in NativeArray<Edge> edges, int edgesLength, [NoAlias] in HashedVertices vertices)
        {
            var from = (double3)vertices[edges[0].index1];
            for (int sweep = 0; sweep < 2; sweep++)
            {
                var furthest = from;
                var best     = -1.0;
                for (int e = 0; e < edgesLength * 2; e++)
                {
                    var vertex   = (double3)vertices[(e & 1) == 0 ? edges[e >> 1].index1 : edges[e >> 1].index2];
                    var distance = math.distancesq(from, vertex);
                    if (distance > best) { best = distance; furthest = vertex; }
                }
                if (sweep == 0) { from = furthest; continue; }

                var direction = furthest - from;
                var lengthSq  = math.lengthsq(direction);
                var band      = (double)CSGConstants.kFatPlaneWidthEpsilon;
                if (lengthSq <= band * band)
                    return true;
                for (int e = 0; e < edgesLength * 2; e++)
                {
                    var offset = (double3)vertices[(e & 1) == 0 ? edges[e >> 1].index1 : edges[e >> 1].index2] - from;
                    // |offset x direction| / |direction| is the distance from the line
                    if (math.lengthsq(math.cross(offset, direction)) > band * band * lengthSq)
                        return false;
                }
            }
            return true;
        }

        internal static float3 CalculatePlaneNormal(in UnsafeList<Edge> edges, in HashedVertices hashedVertices)
        {
            // Newell's algorithm to create a plane for concave polygons.
            // NOTE: doesn't work well for self-intersecting polygons
            var normal = float3.zero;
            var vertices = hashedVertices;
            for (int n = 0; n < edges.Length; n++)
            {
                var edge = edges[n];
                var prevVertex = vertices[(int)edge.index1];
                var currVertex = vertices[(int)edge.index2];
                normal += CSGMath.NewellTerm(prevVertex, currVertex);
            }
            normal = math.normalizesafe(normal);

            return normal;
        }

        static readonly bool kDebugDumpNonSimpleLoops = false;

        // Investigation trace (CSGTrace): discarded under Burst, so it only runs when a test turns Burst off.
        [BurstDiscard]
        static void TraceInputs(int brushNodeOrder, [NoAlias] in HashedVertices vertices,
                                [NoAlias] in NativeList<UnsafeList<Edge>> basePolygonEdges,
                                [NoAlias] in NativeList<IndexSurfaceInfo> intersectionSurfaceInfos,
                                [NoAlias] in NativeList<UnsafeList<Edge>> intersectionEdges)
        {
            if (CSGTrace.Sink == null)
                return;
            CSGTrace.Vertices(brushNodeOrder, in vertices);
            for (int l = 0; l < basePolygonEdges.Length; l++)
                CSGTrace.Loop('B', brushNodeOrder, l, -1, 0, brushNodeOrder, basePolygonEdges[l]);
            for (int i = 0; i < intersectionSurfaceInfos.Length; i++)
            {
                var info = intersectionSurfaceInfos[i];
                CSGTrace.Loop('I', brushNodeOrder, info.basePlaneIndex, -1, info.interiorCategory, info.brushIndexOrder.nodeOrder, intersectionEdges[i]);
            }
        }

        [BurstDiscard]
        static void TraceRoute(int brushNodeOrder, int surfaceIndex, int step, int loopIndex, int inCategory, int intersectionCategory,
                               int otherBrush, int intersectionLength, bool overlap, int outCategory, int cutCategory)
        {
            CSGTrace.Route(brushNodeOrder, surfaceIndex, step, loopIndex, inCategory, intersectionCategory, otherBrush,
                           intersectionLength, overlap, outCategory, cutCategory);
        }

        [BurstDiscard]
        static void TraceLoops(char stage, int brushNodeOrder, int surfaceIndex, [NoAlias] in UnsafeList<int> loopIndices,
                               [NoAlias] in NativeList<UnsafeList<int>> holeIndices, [NoAlias] in NativeList<IndexSurfaceInfo> allInfos,
                               [NoAlias] in NativeList<UnsafeList<Edge>> allEdges)
        {
            CSGTrace.Loops(stage, brushNodeOrder, surfaceIndex, in loopIndices, in holeIndices, in allInfos, in allEdges);
        }

        // Set false to disable the conservative single-chord removal on the merged loop.
        const bool kRemoveSimpleChords = true;

        // Re-close a merged loop that is a single open path missing exactly one edge. See
        // LoopEdgeSplitter.CloseSingleOpenChain; only fires on one clean connected open chain.
        const bool kCloseOpenChains = true;

        static readonly bool kLogDestroyedEdges = false;

        static readonly bool kLogStrictCrossing = false;

		[BurstDiscard]
		static void LogStrictCrossing(int brushNodeOrder, int surfaceIndex, int loopIndex, int i1, int i2, EdgeCategory midpointVerdict, char kind)
		{
#if UNITY_EDITOR
			var msg = new FixedString128Bytes();
            msg.Append('X'); msg.Append('I'); msg.Append('N'); msg.Append('G'); msg.Append(' ');
            msg.Append(brushNodeOrder); msg.Append('/'); msg.Append(surfaceIndex); msg.Append('/'); msg.Append(loopIndex);
            msg.Append(' '); msg.Append('k'); msg.Append(kind);
            msg.Append(' '); msg.Append('m'); msg.Append(midpointVerdict == EdgeCategory.Inside ? 'I' : 'O');
            msg.Append(':'); msg.Append(' '); msg.Append(i1); msg.Append('-'); msg.Append(i2);
            Debug.Log(msg);
#endif
        }

        static readonly bool kKeepReverseAlignedBaseEdges = false;

		[BurstDiscard]
		static void LogDestroyedEdge(int brushNodeOrder, int surfaceIndex, int loopIndex, int i1, int i2, EdgeCategory category, char kind)
		{
#if UNITY_EDITOR
			var msg = new FixedString128Bytes();
            msg.Append('D'); msg.Append('E'); msg.Append('D'); msg.Append('G'); msg.Append('E'); msg.Append(' ');
            msg.Append(brushNodeOrder); msg.Append('/'); msg.Append(surfaceIndex); msg.Append('/'); msg.Append(loopIndex);
            msg.Append(' '); msg.Append('k'); msg.Append(kind);
            msg.Append(' '); msg.Append('c');
            switch (category)
            {
                case EdgeCategory.Inside:         msg.Append('I'); break;
                case EdgeCategory.Aligned:        msg.Append('A'); break;
                case EdgeCategory.ReverseAligned: msg.Append('R'); break;
                case EdgeCategory.Outside:        msg.Append('O'); break;
                default:                          msg.Append('?'); break;
            }
            msg.Append(':'); msg.Append(' '); msg.Append(i1); msg.Append('-'); msg.Append(i2);
            Debug.Log(msg);
#endif
        }

        const bool kKeepEdgesRestingOnTheOtherLoopsBoundary = true;

        // Diagnostic: a loop that EXITS CleanUp still containing antiparallel (A->B & B->A) pairs - i.e.
        // CleanUp's RemoveAntiparallelEdgePairs did NOT clean it.
        static readonly bool kLogCleanupAnti = false;

		[BurstDiscard]
		static void LogCleanupAnti(in NativeList<IndexSurfaceInfo> allInfos, int baseloopIndex, int pairCount)
		{
#if UNITY_EDITOR
			var brushNodeOrder = allInfos[baseloopIndex].brushIndexOrder.nodeOrder;
			var surfaceIndex   = allInfos[baseloopIndex].basePlaneIndex;
			var loopIndex      = baseloopIndex;
			var msg = new FixedString128Bytes();
            msg.Append('C'); msg.Append('A'); msg.Append('N'); msg.Append('T'); msg.Append(' ');
            msg.Append(brushNodeOrder); msg.Append('/'); msg.Append(surfaceIndex); msg.Append('/'); msg.Append(loopIndex);
            msg.Append(' '); msg.Append('p'); msg.Append(pairCount);
            Debug.Log(msg);
#endif
		}

		[BurstDiscard]
		static void LogCleanupAntiOut(int brushNodeOrder, int surfaceIndex, int loopIndex, int pairCount)
		{
#if UNITY_EDITOR
			var msg = new FixedString128Bytes();
            msg.Append('C'); msg.Append('A'); msg.Append('N'); msg.Append('T'); msg.Append('O'); msg.Append(' ');
            msg.Append(brushNodeOrder); msg.Append('/'); msg.Append(surfaceIndex); msg.Append('/'); msg.Append(loopIndex);
            msg.Append(' '); msg.Append('p'); msg.Append(pairCount);
            Debug.Log(msg);
#endif
        }

        static readonly bool kLogHoleFlip = false;

		[BurstDiscard]
		static void LogHoleFlip(in NativeList<IndexSurfaceInfo> allInfos, int baseloopIndex,
								float3 holeNormal, float3 baseLoopNormal,
								in UnsafeList<Edge> holeEdges, in UnsafeList<Edge> baseLoopEdges)
		{
#if UNITY_EDITOR
			int coincidentWithBase = 0;
			for (int hn = 0; hn < holeEdges.Length; hn++)
				for (int bn = 0; bn < baseLoopEdges.Length; bn++)
					if ((holeEdges[hn].index1 == baseLoopEdges[bn].index1 && holeEdges[hn].index2 == baseLoopEdges[bn].index2) ||
						(holeEdges[hn].index1 == baseLoopEdges[bn].index2 && holeEdges[hn].index2 == baseLoopEdges[bn].index1))
					{ coincidentWithBase++; break; }

			var brushNodeOrder = allInfos[baseloopIndex].brushIndexOrder.nodeOrder;
			var surfaceIndex   = allInfos[baseloopIndex].basePlaneIndex;
			var dot            = (float)math.dot(holeNormal, baseLoopNormal);
			var holeNormalLen  = math.length(holeNormal);
			var holeLen        = holeEdges.Length;

			var msg = new FixedString128Bytes();
            msg.Append('H'); msg.Append('F'); msg.Append('L'); msg.Append('P'); msg.Append(' ');
            msg.Append(brushNodeOrder); msg.Append('/'); msg.Append(surfaceIndex);
            // dot and normal-length scaled x1000 and logged as ints (FixedString float formatting is fiddly)
            msg.Append(' '); msg.Append('d'); msg.Append((int)(dot * 1000));
            msg.Append(' '); msg.Append('n'); msg.Append((int)(holeNormalLen * 1000));
            msg.Append(' '); msg.Append('h'); msg.Append(holeLen);
            msg.Append(' '); msg.Append('c'); msg.Append(coincidentWithBase);
            Debug.Log(msg);
#endif
        }

		[BurstDiscard]
		static void LogCleanupDup(int brushNodeOrder, int surfaceIndex, int loopIndex, int dupCount)
		{
#if UNITY_EDITOR
			var msg = new FixedString128Bytes();
            msg.Append('C'); msg.Append('D'); msg.Append('U'); msg.Append('P'); msg.Append(' ');
            msg.Append(brushNodeOrder); msg.Append('/'); msg.Append(surfaceIndex); msg.Append('/'); msg.Append(loopIndex);
            msg.Append(' '); msg.Append('d'); msg.Append(dupCount);
            Debug.Log(msg);
#endif
		}

		[BurstDiscard]
		static void LogLoopDefect(char stage, int brushNodeOrder, int surfaceIndex, int loopIndex, in UnsafeList<Edge> edges, in HashedVertices vertices)
        {
#if UNITY_EDITOR
            var defect = LoopValidation.Classify(in edges, vertices.Length, out int badVertex);
            if (defect == LoopDefect.None || defect == LoopDefect.Empty)
                return;
            var msg = new FixedString512Bytes();
            msg.Append('N'); msg.Append('S'); msg.Append('L'); msg.Append(stage); msg.Append(' ');
            msg.Append(brushNodeOrder); msg.Append('/'); msg.Append(surfaceIndex); msg.Append('/'); msg.Append(loopIndex);
            msg.Append(' ');
            switch (defect)
            {
                case LoopDefect.DegenerateEdge: msg.Append('D'); msg.Append('E'); msg.Append('G'); break;
                case LoopDefect.OpenChain:      msg.Append('O'); msg.Append('P'); msg.Append('N'); break;
                case LoopDefect.Pinch:          msg.Append('P'); msg.Append('I'); msg.Append('N'); break;
            }
            msg.Append('@'); msg.Append(badVertex); msg.Append(':');
            for (int e = 0; e < edges.Length && msg.Length < 470; e++)
            {
                msg.Append(' ');
                msg.Append(edges[e].index1);
                msg.Append('-');
                msg.Append(edges[e].index2);
            }
            Debug.Log(msg);
#endif
		}

        static int AddBoundingPlanes([NoAlias] ref NativeList<float4> planes, [NoAlias] ref BlobArray<float4> brushPlanes,
                                     float3 loopNormal, [NoAlias] in UnsafeList<Edge> loop, [NoAlias] in HashedVertices vertices)
        {
            int added = 0;
            for (int p = 0; p < brushPlanes.Length; p++)
            {
                var plane = brushPlanes[p];
                if (ContainsLoop(plane, loopNormal, in loop, in vertices))
                    continue;
                planes.AddNoResize(plane);
                added++;
            }
            return added;
        }

        static bool ContainsLoop(float4 plane, float3 loopNormal, [NoAlias] in UnsafeList<Edge> loop, [NoAlias] in HashedVertices vertices)
        {
            if (math.abs(math.dot(plane.xyz, loopNormal)) < CSGConstants.kNormalDotAlignEpsilon)
                return false;
            for (int e = 0; e < loop.Length; e++)
            {
                if (math.abs(CSGMath.SignedDistance(plane, vertices[loop[e].index1])) > CSGConstants.kFatPlaneWidthEpsilon)
                    return false;
            }
            return true;
        }

		void CleanUp(in NativeList<IndexSurfaceInfo> allInfos, ref NativeList<UnsafeList<Edge>> allEdges, in HashedVertices brushVertices, ref UnsafeList<int> loopIndices, ref NativeList<UnsafeList<int>> holeIndices,
			        [NoAlias] ref NativeList<float4> alltreeSpacePlanes, [NoAlias] ref NativeList<LoopSegment> allSegments, [NoAlias] ref NativeList<Edge> allCombinedEdges, [NoAlias] ref NativeBitArray destroyedEdges)
        {
            for (int l = loopIndices.Length - 1; l >= 0; l--)
            {
                var baseloopIndex   = loopIndices[l];
                var baseLoopEdges   = allEdges[baseloopIndex];


                // Remove degenerate edges that loop back on itself (zero-area slits/spikes).
                if (baseLoopEdges.Length >= 3)
                    LoopEdgeSplitter.RemoveAntiparallelEdgePairs(ref baseLoopEdges);

                if (baseLoopEdges.Length < 3)
                {
                    baseLoopEdges.Clear();
                    allEdges[baseloopIndex] = baseLoopEdges;
                    continue;
                }

                var surfaceLoopInfo     = allInfos[baseloopIndex];
                var interiorCategory    = (CategoryIndex)surfaceLoopInfo.interiorCategory;
                if (interiorCategory != CategoryIndex.ValidAligned &&
                    interiorCategory != CategoryIndex.ValidReverseAligned)
                {
                    baseLoopEdges.Clear();
                    allEdges[baseloopIndex] = baseLoopEdges;
                    continue;
                }

                var baseLoopNormal = CalculatePlaneNormal(in baseLoopEdges, in brushVertices);
                if (math.all(baseLoopNormal == float3.zero))
                {
                    baseLoopEdges.Clear();
                    allEdges[baseloopIndex] = baseLoopEdges;
                    continue;
                }

                allEdges[baseloopIndex] = baseLoopEdges;
                var holeIndicesList = holeIndices[baseloopIndex];
                if (holeIndicesList.Length == 0)
                    continue;


                for (int h = holeIndicesList.Length - 1; h >= 0; h--)
                {
                    var holeIndex   = holeIndicesList[h];
                    var holeEdges   = allEdges[holeIndex];
                    
                    // Remove degenerate edges that loop back on itself
                    TryNextHole:
                    if (holeEdges.Length >= 3)
                    {
                        for (int a = 0; a < holeEdges.Length; a++)
                        {
                            for (int b = a + 1; b < holeEdges.Length; b++)
                            {
                                if (holeEdges[a].index1 == holeEdges[b].index2 &&
                                    holeEdges[a].index2 == holeEdges[b].index1)
                                {
                                    //Debug.Log($"hole [{a},{b}] ({holeEdges[a].index1}, {holeEdges[a].index2}) ({holeEdges[b].index1}, {holeEdges[b].index2})");
                                    holeEdges.RemoveAtSwapBack(b);
                                    holeEdges.RemoveAtSwapBack(a);
                                    goto TryNextHole;
                                }
                            }
                        }
                    }

                    allEdges[holeIndex] = holeEdges;
                    if (holeEdges.Length < 3)
                    {
                        holeIndicesList.RemoveAtSwapBack(h);
                        holeIndices[baseloopIndex] = holeIndicesList;
                        continue;
                    }

                    var holeNormal = CalculatePlaneNormal(in holeEdges, in brushVertices);
                    if (math.all(holeNormal == float3.zero))
                    {
                        holeIndicesList.RemoveAtSwapBack(h);
                        holeIndices[baseloopIndex] = holeIndicesList;
                        continue;
                    }
                }

                if (holeIndicesList.Length == 0)
                    continue;

                int totalPlaneCount = 0;
                int totalEdgeCount = baseLoopEdges.Length;
                {
                    int brushNodeOrder  = allInfos[baseloopIndex].brushIndexOrder.nodeOrder;
                    ref var treeSpacePlanes = ref brushTreeSpacePlaneCache[brushNodeOrder].Value.treeSpacePlanes;
                    totalPlaneCount += treeSpacePlanes.Length;
                }
                for (int h = 0; h < holeIndicesList.Length; h++)
                {
                    var holeIndex = holeIndicesList[h];
                    var holeEdges = allEdges[holeIndex];
                    totalEdgeCount += holeEdges.Length;
                    
                    int brushNodeOrder  = allInfos[holeIndex].brushIndexOrder.nodeOrder;
                    ref var treeSpacePlanes = ref brushTreeSpacePlaneCache[brushNodeOrder].Value.treeSpacePlanes;

                    totalPlaneCount += treeSpacePlanes.Length;
                }


                NativeCollectionHelpers.EnsureCapacityAndClear(ref alltreeSpacePlanes, totalPlaneCount);
                NativeCollectionHelpers.EnsureCapacityAndClear(ref allSegments, holeIndicesList.Length + 1);
                NativeCollectionHelpers.EnsureCapacityAndClear(ref allCombinedEdges, totalEdgeCount);

                {                
                    int edgeOffset = 0;
                    int planeOffset = 0;
                    for (int h = 0; h < holeIndicesList.Length; h++)
                    {
                        var holeIndex   = holeIndicesList[h];
                        var holeEdges   = allEdges[holeIndex];

                        // TODO: figure out why sometimes polygons are flipped around, and try to fix this at the source
                        var holeNormal  = CalculatePlaneNormal(in holeEdges, in brushVertices);
                        if (kLogHoleFlip)
                        {
                            LogHoleFlip(in allInfos, baseloopIndex, holeNormal, baseLoopNormal, in holeEdges, in baseLoopEdges);
                        }
                        if (math.dot(holeNormal, baseLoopNormal) > 0)
                        {
                            for (int n = 0; n < holeEdges.Length; n++)
                            {
                                var holeEdge = holeEdges[n];
                                var i1 = holeEdge.index1;
                                var i2 = holeEdge.index2;
                                holeEdge.index1 = i2;
                                holeEdge.index2 = i1;
                                holeEdges[n] = holeEdge;
                            }
                            allEdges[holeIndex] = holeEdges;
                        }

                        int brushNodeOrder = allInfos[holeIndex].brushIndexOrder.nodeOrder;
                        ref var treeSpacePlanes = ref brushTreeSpacePlaneCache[brushNodeOrder].Value.treeSpacePlanes;

                        // TODO: ideally we'd only use the planes that intersect our edges
                        var planesLength    = AddBoundingPlanes(ref alltreeSpacePlanes, ref treeSpacePlanes, baseLoopNormal, in baseLoopEdges, in brushVertices);
                        var edgesLength     = holeEdges.Length;

                        allSegments.AddNoResize(new LoopSegment
                        {
                            edgeOffset      = edgeOffset,
                            edgeLength      = edgesLength,
                            planesOffset    = planeOffset,
                            planesLength    = planesLength
                        });

                        allCombinedEdges.AddRangeNoResize(holeEdges);

                        edgeOffset += edgesLength;
                        planeOffset += planesLength;
                    }
                    if (baseLoopEdges.Length > 0)
                    {
                        int brushNodeOrder = allInfos[baseloopIndex].brushIndexOrder.nodeOrder;
                        ref var treeSpacePlanes = ref brushTreeSpacePlaneCache[brushNodeOrder].Value.treeSpacePlanes;

                        // TODO: ideally we'd only use the planes that intersect our edges
                        var planesLength    = AddBoundingPlanes(ref alltreeSpacePlanes, ref treeSpacePlanes, baseLoopNormal, in baseLoopEdges, in brushVertices);
                        var edgesLength     = baseLoopEdges.Length;

                        allSegments.AddNoResize(new LoopSegment
                        {
                            edgeOffset      = edgeOffset,
                            edgeLength      = edgesLength,
                            planesOffset    = planeOffset,
                            planesLength    = planesLength
                        });

                        allCombinedEdges.AddRangeNoResize(baseLoopEdges);

                        edgeOffset += edgesLength;
                        planeOffset += planesLength;
                    }

                    NativeCollectionHelpers.EnsureMinimumSizeAndClear(ref destroyedEdges, edgeOffset);

                    var allCombinedEdgesArray = allCombinedEdges.AsArray();

                    // For the destroyedEdges instrumentation (DEDGE logs): brush/surface of this loop,
                    // so each deletion can be correlated with the NSLE dump of the same brush/surface/loop.
                    var dbgInfo  = allInfos[baseloopIndex];
                    var dbgBrush = dbgInfo.brushIndexOrder.nodeOrder;
                    int dbgSurf  = dbgInfo.basePlaneIndex;

                    {
                        {
                            var segment1 = allSegments[holeIndicesList.Length];
                            for (int j = 0; j < holeIndicesList.Length; j++)
                            {
                                var segment2 = allSegments[j];

                                if (segment1.edgeLength == 0 ||
                                    segment2.edgeLength == 0)
                                    continue;

                                for (int e = 0; e < segment1.edgeLength; e++)
                                {
                                    var category = BooleanEdgesUtility.CategorizeEdge(allCombinedEdgesArray[segment1.edgeOffset + e], in alltreeSpacePlanes, in allCombinedEdgesArray, segment2, in brushVertices);
                                    if (kLogStrictCrossing && (category == EdgeCategory.Inside || category == EdgeCategory.Outside) &&
                                        BooleanEdgesUtility.EdgeStrictlyCrossesSegmentPlanes(allCombinedEdgesArray[segment1.edgeOffset + e], in alltreeSpacePlanes, segment2, in brushVertices))
                                    {
                                        var se = allCombinedEdgesArray[segment1.edgeOffset + e];
                                        LogStrictCrossing(dbgBrush, dbgSurf, baseloopIndex, se.index1, se.index2, category, 'B');
                                    }
                                    if (category == EdgeCategory.Outside || category == EdgeCategory.Aligned ||
                                        (kKeepReverseAlignedBaseEdges && category == EdgeCategory.ReverseAligned))
                                        continue;
                                    destroyedEdges.Set(segment1.edgeOffset + e, true);
                                    if (kLogDestroyedEdges)
                                    {
                                        var de = allCombinedEdgesArray[segment1.edgeOffset + e];
                                        LogDestroyedEdge(dbgBrush, dbgSurf, baseloopIndex, de.index1, de.index2, category, 'B');
                                    }
                                }

                                for (int e = 0; e < segment2.edgeLength; e++)
                                {
                                    var category = BooleanEdgesUtility.CategorizeEdge(allCombinedEdgesArray[segment2.edgeOffset + e], in alltreeSpacePlanes, in allCombinedEdgesArray, segment1, in brushVertices);
                                    if (kLogStrictCrossing && (category == EdgeCategory.Inside || category == EdgeCategory.Outside) &&
                                        BooleanEdgesUtility.EdgeStrictlyCrossesSegmentPlanes(allCombinedEdgesArray[segment2.edgeOffset + e], in alltreeSpacePlanes, segment1, in brushVertices))
                                    {
                                        var se = allCombinedEdgesArray[segment2.edgeOffset + e];
                                        LogStrictCrossing(dbgBrush, dbgSurf, baseloopIndex, se.index1, se.index2, category, 'h');
                                    }
                                    if (category == EdgeCategory.Inside)
                                        continue;
                                    destroyedEdges.Set(segment2.edgeOffset + e, true);
                                    if (kLogDestroyedEdges)
                                    {
                                        var de = allCombinedEdgesArray[segment2.edgeOffset + e];
                                        LogDestroyedEdge(dbgBrush, dbgSurf, baseloopIndex, de.index1, de.index2, category, 'h');
                                    }
                                }
                            }
                        }

                        // TODO: optimize, keep track which holes (potentially) intersect
                        // TODO: create our own bounds data structure that doesn't use stupid slow properties for everything
                        {
                            for (int j = 0, length = MathExtensions.GetTriangleArraySize(holeIndicesList.Length); j < length; j++)
                            {
                                var arrayIndex = MathExtensions.GetTriangleArrayIndex(j, holeIndicesList.Length);
                                var segmentIndex1 = arrayIndex.x;
                                var segmentIndex2 = arrayIndex.y;
                                var segment1 = allSegments[segmentIndex1];
                                var segment2 = allSegments[segmentIndex2];
                                if (segment1.edgeLength > 0 && segment2.edgeLength > 0)
                                {
                                    for (int e = 0; e < segment1.edgeLength; e++)
                                    {
                                        var category = BooleanEdgesUtility.CategorizeEdge(allCombinedEdgesArray[segment1.edgeOffset + e], in alltreeSpacePlanes, in allCombinedEdgesArray, segment2, in brushVertices);
                                        if (category == EdgeCategory.Outside ||
                                            category == EdgeCategory.Aligned)
                                            continue;
                                        // Inside only by a fat-band touch: this hole's edge rests on the
                                        // other hole's boundary, which its loop need not run along
                                        if (kKeepEdgesRestingOnTheOtherLoopsBoundary && category == EdgeCategory.Inside &&
                                            BooleanEdgesUtility.EdgeRestsOnSegmentPlanes(allCombinedEdgesArray[segment1.edgeOffset + e], in alltreeSpacePlanes, segment2, in brushVertices))
                                            continue;
                                        destroyedEdges.Set(segment1.edgeOffset + e, true);
                                        if (kLogDestroyedEdges)
                                        {
                                            var de = allCombinedEdgesArray[segment1.edgeOffset + e];
                                            LogDestroyedEdge(dbgBrush, dbgSurf, baseloopIndex, de.index1, de.index2, category, 'p');
                                        }
                                    }

                                    for (int e = 0; e < segment2.edgeLength; e++)
                                    {
                                        var category = BooleanEdgesUtility.CategorizeEdge(allCombinedEdgesArray[segment2.edgeOffset + e], in alltreeSpacePlanes, in allCombinedEdgesArray, segment1, in brushVertices);
                                        if (category == EdgeCategory.Outside)
                                            continue;
                                        // Inside only by a fat-band touch: this hole's edge rests on the
                                        // other hole's boundary, which its loop need not run along
                                        if (kKeepEdgesRestingOnTheOtherLoopsBoundary && category == EdgeCategory.Inside &&
                                            BooleanEdgesUtility.EdgeRestsOnSegmentPlanes(allCombinedEdgesArray[segment2.edgeOffset + e], in alltreeSpacePlanes, segment1, in brushVertices))
                                            continue;
                                        destroyedEdges.Set(segment2.edgeOffset + e, true);
                                        if (kLogDestroyedEdges)
                                        {
                                            var de = allCombinedEdgesArray[segment2.edgeOffset + e];
                                            LogDestroyedEdge(dbgBrush, dbgSurf, baseloopIndex, de.index1, de.index2, category, 'q');
                                        }
                                    }
                                }
                            }
                        }

                        {
                            var segment = allSegments[holeIndicesList.Length];
                            for (int e = baseLoopEdges.Length - 1; e >= 0; e--)
                            {
                                if (!destroyedEdges.IsSet(segment.edgeOffset + e))
                                    continue;
                                baseLoopEdges.RemoveAtSwapBack(e);
                            }
                            allEdges[baseloopIndex] = baseLoopEdges;
                        }

                        for (int h1 = holeIndicesList.Length - 1; h1 >= 0; h1--)
                        {
                            var holeIndex1  = holeIndicesList[h1];
                            var holeEdges1  = allEdges[holeIndex1];
                            var segment     = allSegments[h1];
                            for (int e = holeEdges1.Length - 1; e >= 0; e--)
                            {
                                if (!destroyedEdges.IsSet(segment.edgeOffset + e))
                                    continue;
                                holeEdges1.RemoveAtSwapBack(e);
                            }
                            allEdges[holeIndex1] = holeEdges1;
                        }
                    }
                }

                for (int h = holeIndicesList.Length - 1; h >= 0; h--)
                {
                    var holeIndex   = holeIndicesList[h];
                    var holeEdges   = allEdges[holeIndex];


                    // TODO: why is baseLoopEdges sometimes not properly allocated?
                    if (baseLoopEdges.Capacity < baseLoopEdges.Length + holeEdges.Length)
                        baseLoopEdges.Capacity = baseLoopEdges.Capacity + (holeEdges.Length * 2);

                    // Note: can have duplicate edges when multiple holes share an edge
                    //          (only edges between holes and base-loop are guaranteed to not be duplciate)
                    AddEdgesNoResize(ref baseLoopEdges, in holeEdges);
                }

                if (baseLoopEdges.Length >= 3)
                    LoopEdgeSplitter.RemoveAntiparallelEdgePairs(ref baseLoopEdges);

                if (kRemoveSimpleChords && baseLoopEdges.Length >= 3)
                    LoopEdgeSplitter.RemoveSimpleChords(ref baseLoopEdges);

                // Re-close a loop that ended up a single open path missing exactly one edge.
                // Exact and unambiguous; refuses anything that isn't one clean open chain.
                if (kCloseOpenChains && baseLoopEdges.Length >= 2)
                    LoopEdgeSplitter.CloseSingleOpenChain(ref baseLoopEdges);

                allEdges[baseloopIndex] = baseLoopEdges;
                holeIndicesList.Clear();
                holeIndices[baseloopIndex] = holeIndicesList;
            }

            // TODO: remove the need for this
            for (int l = loopIndices.Length - 1; l >= 0; l--)
            {
                var baseloopIndex   = loopIndices[l];
                var baseLoopEdges   = allEdges[baseloopIndex];
                if (baseLoopEdges.Length < 3)
                {
                    loopIndices.RemoveAtSwapBack(l);
                    continue;
                }
                if (kLogCleanupAnti)
                {
                    int anti = 0;
                    for (int a = 0; a < baseLoopEdges.Length; a++)
                        for (int b = a + 1; b < baseLoopEdges.Length; b++)
                            if (baseLoopEdges[a].index1 == baseLoopEdges[b].index2 &&
                                baseLoopEdges[a].index2 == baseLoopEdges[b].index1)
                                anti++;
                    if (anti > 0)
                        LogCleanupAnti(in allInfos, baseloopIndex, anti);
                }
            }
        }

        public void Execute(int index)
		{
			NativeArray<EdgeCategory> categories1 = default;
			NativeArray<EdgeCategory> categories2 = default;
			NativeArray<Edge> outEdges = default;
			NativeArray<int> intersectedHoleIndices = default;
			NativeArray<IndexSurfaceInfo> intersectionSurfaceInfo = default;
			NativeBitArray destroyedEdges = default;
			NativeList<IndexSurfaceInfo> allInfos = default;
			NativeList<IndexSurfaceInfo> intersectionSurfaceInfos = default;
			NativeList<IndexSurfaceInfo> basePolygonSurfaceInfos = default;
			NativeList<float4> alltreeSpacePlanes = default;
			NativeList<LoopSegment> allSegments = default;
			NativeList<Edge> allCombinedEdges = default;
			NativeList<UnsafeList<int>> holeIndices = default;
			NativeList<UnsafeList<int>> surfaceLoopIndices = default;
			NativeList<UnsafeList<Edge>> allEdges = default;
			NativeList<UnsafeList<Edge>> intersectionLoops = default;
			NativeList<UnsafeList<Edge>> basePolygonEdges = default;
			HashedVertices hashedTreeSpaceVertices = default;
			NativeArray<ushort> indexRemap = default;
			NativeList<UnsafeList<Edge>> intersectionEdges = default;
            try
            { 
			    var count = input.BeginForEachIndex(index);
                if (count == 0)
                {
                    input.EndForEachIndex();

                    output.BeginForEachIndex(index);
                    output.Write(new IndexOrder());
                    output.Write(0);
                    output.Write(0);
                    output.Write(0);
                    output.EndForEachIndex();
                    return;
                }

                var brushIndexOrder = input.Read<IndexOrder>();
                var brushNodeOrder = brushIndexOrder.nodeOrder;
                var surfaceCount = input.Read<int>();

                // This list is already welded upstream; re-welding it may only merge what the brush's own faces allow.
                var weldFilter = WeldIncidenceFilter.Disabled;
                if (canonicalVertexStage >= CanonicalVertexStage.Everywhere)
                    weldFilter = WeldIncidenceFilter.SameVertexOnly;   // canonical vertices: only the same vertex is merged
                else
                if (useIncidenceWeld && brushTreeSpacePlaneCache[brushNodeOrder].IsCreated)
                    weldFilter = WeldIncidenceFilter.Create(ref brushTreeSpacePlaneCache[brushNodeOrder].Value.treeSpacePlanes, surfaceCount);

                var inputVertices = loopVerticesLookup[brushNodeOrder];
                var vertexCount = inputVertices.Length;

                hashedTreeSpaceVertices = new HashedVertices(vertexCount, Allocator.Temp);
			    //NativeCollectionHelpers.EnsureCapacityAndClear(ref hashedTreeSpaceVertices, vertexCount);
                try
                { 
                    NativeCollectionHelpers.EnsureMinimumSize(ref indexRemap, vertexCount);
            

                    hashedTreeSpaceVertices.Clear();
                    for (int v = 0; v < inputVertices.Length; v++)
                    {
                        var vertex = inputVertices[v];
                        indexRemap[v] = hashedTreeSpaceVertices.AddNoResize(vertex, in weldFilter);
                    }
                    //Debug.Assert(hashedTreeSpaceVertices.Length == inputVertices.Length);

                    var basePolygonEdgesLength = input.Read<int>();
                    NativeCollectionHelpers.EnsureSizeAndClear(ref basePolygonSurfaceInfos, basePolygonEdgesLength);
                    NativeCollectionHelpers.EnsureSizeAndClear(ref basePolygonEdges, basePolygonEdgesLength);
                    int polygonIndex = 0;
                    for (int l = 0; l < basePolygonEdgesLength; l++)
                    {
                        var indexSurfaceInfo = input.Read<IndexSurfaceInfo>();
                        //if (l >= basePolygonSurfaceInfos.Length)
                        //    Debug.Log("F");
                        basePolygonSurfaceInfos[l] = indexSurfaceInfo;

                        var edgesLength     = input.Read<int>();
                        if (edgesLength == 0)
                        {
                            //if (l >= basePolygonEdges.Length)
                            //    Debug.Log("C");
                            if (basePolygonEdges[l].IsCreated)
                                basePolygonEdges[l].Clear();
                            continue;
                        }
                        //if (l >= basePolygonEdges.Length)
                        //    Debug.Log($"D {l} {basePolygonEdges.Length} {basePolygonSurfaceInfos.Length} {basePolygonEdgesLength} {basePolygonEdges.Length}");
                        var edgesInner = new UnsafeList<Edge>(edgesLength, Allocator.Temp);
                        //Debug.Log("E");
                        //edgesInner.ResizeUninitialized(edgesLength);
                        for (int e = 0; e < edgesLength; e++)
                        {
                            var edge = input.Read<Edge>();
                            edge.index1 = indexRemap[edge.index1];
                            edge.index2 = indexRemap[edge.index2];
                            if (edge.index1 == edge.index2)
                                continue;
                            //Debug.Assert(edge.index1 >= 0 && edge.index1 < hashedTreeSpaceVertices.Length);
                            //Debug.Assert(edge.index2 >= 0 && edge.index2 < hashedTreeSpaceVertices.Length);
                            //if (edgesInner.Length + 1 >= edgesInner.Capacity)
                            //    Debug.Log("E");
                            edgesInner.AddNoResize(edge);
                        }
                        if (edgesInner.Length < 3)
                            edgesInner.Clear();
                        basePolygonEdges[l] = edgesInner;
                        if (kDebugDumpNonSimpleLoops)
                            LogLoopDefect('I', brushNodeOrder, l, -1, in edgesInner, in hashedTreeSpaceVertices);
                    }
                    //basePolygonSurfaceInfos.ResizeUninitialized(polygonIndex);
                    //basePolygonEdges.ResizeExact(polygonIndex);

                    var intersectionEdgesLength = input.Read<int>();
                    NativeCollectionHelpers.EnsureSizeAndClear(ref intersectionSurfaceInfos, intersectionEdgesLength);
                    NativeCollectionHelpers.EnsureSizeAndClear(ref intersectionEdges, intersectionEdgesLength);
                    polygonIndex = 0;
                    for (int l = 0; l < intersectionEdgesLength; l++)
                    {
                        var indexSurfaceInfo = input.Read<IndexSurfaceInfo>();
                        var edgesLength = input.Read<int>();
                        var edgesInner  = new UnsafeList<Edge>(edgesLength, Allocator.Temp);
                        //edgesInner.ResizeUninitialized(edgesLength);
                        for (int e = 0; e < edgesLength; e++)
                        {
                            var edge = input.Read<Edge>();
                            edge.index1 = indexRemap[edge.index1];
                            edge.index2 = indexRemap[edge.index2];
                            if (edge.index1 == edge.index2)
                                continue;
                            //Debug.Assert(edge.index1 >= 0 && edge.index1 < hashedTreeSpaceVertices.Length);
                            //Debug.Assert(edge.index2 >= 0 && edge.index2 < hashedTreeSpaceVertices.Length);
                            edgesInner.AddNoResize(edge);
                        }
                        if (edgesInner.Length >= 3)
                        {
                            intersectionSurfaceInfos[polygonIndex] = indexSurfaceInfo;
                            intersectionEdges[polygonIndex] = edgesInner;
                            polygonIndex++;
                            if (kDebugDumpNonSimpleLoops)
                                LogLoopDefect('I', brushNodeOrder, indexSurfaceInfo.basePlaneIndex, -1, in edgesInner, in hashedTreeSpaceVertices);
                        } else
                        {
                            edgesInner.Clear();
                            intersectionEdges[polygonIndex] = edgesInner;
                        }
                    }
                    intersectionSurfaceInfos.ResizeUninitialized(polygonIndex);
                    intersectionEdges.Resize(polygonIndex, NativeArrayOptions.ClearMemory);
                    input.EndForEachIndex();
                    TraceInputs(brushNodeOrder, in hashedTreeSpaceVertices, in basePolygonEdges, in intersectionSurfaceInfos, in intersectionEdges);

                    //int brushNodeIndex = treeBrushNodeIndices[index];

                    if (surfaceCount == 0)
                    {
                        output.BeginForEachIndex(index);
                        output.Write(brushIndexOrder);
                        output.Write(0);
                        output.Write(0);
                        output.Write(0);
                        output.EndForEachIndex();
                        return;
                    }


			        BlobAssetReference<RoutingTable> routingTableRef = routingTableCache[brushNodeOrder];
                    if (routingTableRef == BlobAssetReference<RoutingTable>.Null)
                    {
                        //Debug.LogError("No routing table found");
                        output.BeginForEachIndex(index);
                        output.Write(brushIndexOrder);
                        output.Write(0);
                        output.Write(0);
                        output.Write(0);
                        output.EndForEachIndex();
                        return;
                    }

            

                    ref var nodeIDToTableIndex      = ref routingTableRef.Value.nodeIDToTableIndex;
                    ref var nodeIDOffset            = ref routingTableRef.Value.nodeIDOffset;
                    ref var routingLookups          = ref routingTableRef.Value.routingLookups;
                    var routingLookupsLength        = routingLookups.Length;


                    int maxIndex = intersectionSurfaceInfos.Length + (surfaceCount * routingLookupsLength);
                    for (int i = 0; i < intersectionSurfaceInfos.Length; i++)
                    {
                        var surfaceInfo     = intersectionSurfaceInfos[i];
                        var brushNodeID1    = surfaceInfo.brushIndexOrder.compactNodeID;

                        // check if brush does not exist in routing table (will not have any effect)
                        var idWithOffset = brushNodeID1.slotIndex.index - nodeIDOffset;
                        if (idWithOffset < 0 || idWithOffset >= nodeIDToTableIndex.Length)
                            continue;

                        var routingTableIndex = nodeIDToTableIndex[idWithOffset];
                        if (routingTableIndex == -1)
                            continue;

                        var surfaceIndex = surfaceInfo.basePlaneIndex;
                        maxIndex = math.max(maxIndex, routingTableIndex + (surfaceIndex * routingLookupsLength));
                    }


                    // Brush contents (Documentation~/Design/BrushContents.md): a piece of this brush's surface inside, or
                    // against, a brush of another type carries the category the contents rules give it into routing
                    ref var compactTree       = ref compactTreeRef.Value.Value;
                    var processedContents     = compactTree.GetBrushContents(brushIndexOrder.compactNodeID);

                    int intersectionLoopCount = maxIndex + 1;
                    NativeCollectionHelpers.EnsureSizeAndClear(ref intersectionLoops, intersectionLoopCount);
                    NativeCollectionHelpers.EnsureMinimumSize(ref intersectionSurfaceInfo, intersectionLoopCount);

                    {
                        // TODO: Sort the brushSurfaceInfos/intersectionEdges based on nodeIndexToTableIndex[surfaceInfo.brushNodeID], 
                        //       have a sequential list of all data. 
                        //       Have segment list to determine which part of array belong to which brushNodeID
                        //       Don't need bottom part, can determine this in Job

                        for (int i = 0; i < intersectionSurfaceInfos.Length; i++)
                        {
                            var surfaceInfo      = intersectionSurfaceInfos[i];
                            var brushNodeID1     = surfaceInfo.brushIndexOrder.compactNodeID;

                            // check if brush does not exist in routing table (will not have any effect)
                            var idWithOffset = brushNodeID1.slotIndex.index - nodeIDOffset;
                            if (idWithOffset < 0 || idWithOffset >= nodeIDToTableIndex.Length)
                                continue;

                            var routingTableIndex = nodeIDToTableIndex[idWithOffset];
                            if (routingTableIndex == -1)
                                continue;

                            var surfaceIndex    = surfaceInfo.basePlaneIndex;
                            int offset          = routingTableIndex + (surfaceIndex * routingLookupsLength);

                            var srcEdges = intersectionEdges[i];
                            var loops = new UnsafeList<Edge>(srcEdges.Length, Allocator.Temp);
                            loops.AddRangeNoResize(srcEdges);
                            var otherContents = compactTree.GetBrushContents(brushNodeID1);
                            surfaceInfo.interiorCategory = (byte)ContentsRules.Rewrite((CategoryIndex)surfaceInfo.interiorCategory,
                                                                                       processedContents.Contents, processedContents.Carving, otherContents.Contents, otherContents.Carving, otherContents.Intersecting,
                                                                                       // Brushes are numbered depth first, the order their operations apply in
                                                                                       bIsLater: surfaceInfo.brushIndexOrder.nodeOrder > brushIndexOrder.nodeOrder);
                            intersectionSurfaceInfo[offset] = surfaceInfo;
                            intersectionLoops[offset] = loops;
                        }
                    }


                    var maxLoops            = (routingLookupsLength + routingLookupsLength) * (surfaceCount + surfaceCount); // TODO: find a more reliable "max"

                    NativeCollectionHelpers.EnsureCapacityAndClear(ref holeIndices, maxLoops);
                    NativeCollectionHelpers.EnsureSizeAndClear(ref surfaceLoopIndices, surfaceCount);
                    NativeCollectionHelpers.EnsureCapacityAndClear(ref allInfos, maxLoops);
                    NativeCollectionHelpers.EnsureCapacityAndClear(ref allEdges, maxLoops);


                    ref var routingTable = ref routingTableRef.Value;
                    for (int surfaceIndex = 0; surfaceIndex < surfaceCount; surfaceIndex++)
                    {
                        if (!basePolygonEdges[surfaceIndex].IsCreated)
                            continue;
                        var basePolygonSrc = basePolygonEdges[surfaceIndex];
                        if (basePolygonSrc.Length < 3)
                            continue;

                        var info = basePolygonSurfaceInfos[surfaceIndex];
                        info.interiorCategory = 0; // TODO: make sure that it's always set to "0" so we don't need to do this

                        var maxAllocation       = 1 + (2 * (routingLookupsLength + allEdges.Length)); // TODO: find a more reliable "max"
                        var maxEdgeAllocation   = 1 + (hashedTreeSpaceVertices.Length * 2);

                        var loopIndices = new UnsafeList<int>(maxAllocation, Allocator.Temp);                
                        loopIndices.AddNoResize(allEdges.Length);                
                        holeIndices.Add(new UnsafeList<int>(maxAllocation, Allocator.Temp));
                        allInfos   .AddNoResize(info);
                        //Debug.Assert(holeIndices.IsAllocated(allInfos.Length - 1));

                        var basePolygonDst = new UnsafeList<Edge>(basePolygonSrc.Length + maxEdgeAllocation, Allocator.Temp);// TODO: find a more reliable "max"
                        basePolygonDst.AddRangeNoResize(basePolygonSrc);
                        allEdges.Add(basePolygonDst);

                        //Debug.Assert(allEdges.Length == allInfos.Length);
                        //Debug.Assert(allInfos.Length == holeIndices.Length);

                        for (int routingTableIndex = 0; routingTableIndex < routingLookupsLength; routingTableIndex++)
                        {
                            int offset              = routingTableIndex + (surfaceIndex * routingLookupsLength);
                            ref var routingLookup   = ref routingLookups[routingTableIndex];
                            var intersectionLoop    = intersectionLoops[offset];
                            var intersectionInfo    = intersectionSurfaceInfo[offset];
                            for (int l = loopIndices.Length - 1; l >= 0; l--)
                            {
                                var surfaceLoopIndex = loopIndices[l];
                                var surfaceLoopEdges = allEdges[surfaceLoopIndex];
                                if (surfaceLoopEdges.Length < 3)
                                    continue;

                                var surfaceLoopInfo = allInfos[surfaceLoopIndex];
                                //Debug.Assert(holeIndices.IsAllocated(surfaceLoopIndex));

                                // Lookup categorization between original surface & other surface ...
                                if (!routingLookup.TryGetRoute(ref routingTable, surfaceLoopInfo.interiorCategory, out CategoryRoutingRow routingRow))
                                { 
                                    Debug.Assert(false, "Could not find route");
                                    continue;
                                }

                                var intersectionLength = intersectionLoop.IsCreated ? intersectionLoop.Length : 0;
                                bool overlap = intersectionLength != 0 &&
                                                BooleanEdgesUtility.AreLoopsOverlapping(in surfaceLoopEdges, in intersectionLoop);

                                var inCategory = surfaceLoopInfo.interiorCategory;
                                if (overlap)
                                {
                                    // If we overlap don't bother with creating a new polygon & hole and just reuse existing polygon + replace category
                                    var overlapCategory = routingRow[(int)intersectionInfo.interiorCategory];
                                    TraceRoute(brushNodeOrder, surfaceIndex, routingTableIndex, surfaceLoopIndex, inCategory,
                                               intersectionInfo.interiorCategory, intersectionInfo.brushIndexOrder.nodeOrder, intersectionLength,
                                               true, overlapCategory, -1);
                                    surfaceLoopInfo.interiorCategory = overlapCategory;
                                    allInfos[surfaceLoopIndex] = surfaceLoopInfo;
                                    continue;
                                } else
                                {
                                    surfaceLoopInfo.interiorCategory = routingRow.outside;
                                    allInfos[surfaceLoopIndex] = surfaceLoopInfo;
                                }

                                // Add all holes that share the same plane to the polygon
                                if (intersectionLength != 0)
                                {
                                    // Categorize between original surface & intersection
                                    var intersectionCategory = routingRow[intersectionInfo.interiorCategory];
                                    TraceRoute(brushNodeOrder, surfaceIndex, routingTableIndex, surfaceLoopIndex, inCategory,
                                               intersectionInfo.interiorCategory, intersectionInfo.brushIndexOrder.nodeOrder, intersectionLength,
                                               false, surfaceLoopInfo.interiorCategory, intersectionCategory);

                                    // If the intersection polygon would get the same category, we don't need to do a pointless intersection
                                    if (intersectionCategory == surfaceLoopInfo.interiorCategory)
                                        continue;

                                    IntersectLoops(in hashedTreeSpaceVertices, ref loopIndices, surfaceLoopIndex,
                                                   ref holeIndices, ref allInfos, ref allEdges,
												   
                                                   ref outEdges, ref intersectedHoleIndices,
                                                   ref categories1, ref categories2,

												   in intersectionLoop, 
                                                   intersectionCategory, 
                                                   intersectionInfo);
                                    surfaceLoopIndices[surfaceIndex] = loopIndices;
                                }
                            }
                        }
                        if (kDebugDumpNonSimpleLoops)
                        {
                            for (int li = 0; li < loopIndices.Length; li++)
                            {
                                var ci = loopIndices[li];
                                var ce = allEdges[ci];
                                LogLoopDefect('C', brushIndexOrder.nodeOrder, surfaceIndex, ci, in ce, in hashedTreeSpaceVertices);
                                var chs = holeIndices[ci];
                                for (int hi = 0; hi < chs.Length; hi++)
                                {
                                    var hidx = chs[hi];
                                    var he = allEdges[hidx];
                                    LogLoopDefect('C', brushIndexOrder.nodeOrder, surfaceIndex, hidx, in he, in hashedTreeSpaceVertices);
                                }
                            }
                        }
                        TraceLoops('C', brushNodeOrder, surfaceIndex, in loopIndices, in holeIndices, in allInfos, in allEdges);
                        CleanUp(in allInfos, ref allEdges, in hashedTreeSpaceVertices, ref loopIndices, ref holeIndices,
							ref alltreeSpacePlanes, ref allSegments, ref allCombinedEdges, ref destroyedEdges);
                        TraceLoops('E', brushNodeOrder, surfaceIndex, in loopIndices, in holeIndices, in allInfos, in allEdges);
                        surfaceLoopIndices[surfaceIndex] = loopIndices;
                    }

                    output.BeginForEachIndex(index);
                    output.Write(brushIndexOrder);
                    output.Write(hashedTreeSpaceVertices.Length);
                    for (int l = 0; l < hashedTreeSpaceVertices.Length; l++)
                        output.Write(hashedTreeSpaceVertices[l]);

                    output.Write(surfaceLoopIndices.Length);
                    for (int o = 0; o < surfaceLoopIndices.Length; o++)
                    {
                        var inner = surfaceLoopIndices[o];
                        if (!inner.IsCreated)
                        {
                            output.Write(0);
                            continue;
                        }
                        output.Write(inner.Length);
                        for (int i = 0; i < inner.Length; i++)
                            output.Write(inner[i]);
                    }

                    var dbgReferenced = new NativeBitArray(math.max(1, allEdges.Length), Allocator.Temp);
                    if (kDebugDumpNonSimpleLoops)
                    {
                        for (int o = 0; o < surfaceLoopIndices.Length; o++)
                        {
                            var inner = surfaceLoopIndices[o];
                            if (!inner.IsCreated)
                                continue;
                            for (int i = 0; i < inner.Length; i++)
                            {
                                var idx = inner[i];
                                if (idx >= 0 && idx < allEdges.Length)
                                    dbgReferenced.Set(idx, true);
                            }
                        }
                    }

                    output.Write(allEdges.Length);
                    for (int l = 0; l < allEdges.Length; l++)
                    {
                        var surfaceInfo = allInfos[l];
                        output.Write(new SurfaceInfo { basePlaneIndex = surfaceInfo.basePlaneIndex, interiorCategory = surfaceInfo.interiorCategory//, nodeIndex = surfaceInfo.brushIndexOrder.nodeIndex
                                        });
                        var edges = allEdges[l];
                        if (kDebugDumpNonSimpleLoops && dbgReferenced.IsSet(l))
                            LogLoopDefect('E', brushIndexOrder.nodeOrder, surfaceInfo.basePlaneIndex, l, in edges, in hashedTreeSpaceVertices);
                        if (kLogCleanupAnti && edges.Length >= 3)
                        {
                            int oanti = 0;
                            for (int a = 0; a < edges.Length; a++)
                                for (int b = a + 1; b < edges.Length; b++)
                                    if (edges[a].index1 == edges[b].index2 && edges[a].index2 == edges[b].index1)
                                        oanti++;
                            if (oanti > 0)
                                LogCleanupAntiOut(brushIndexOrder.nodeOrder, surfaceInfo.basePlaneIndex, l, oanti);
                            int dup = 0;
                            for (int a = 0; a < edges.Length; a++)
                            {
                                int ia = edges[a].index1;
                                for (int b = a + 1; b < edges.Length; b++)
                                {
                                    int ib = edges[b].index1;
                                    if (ia != ib && math.distancesq(hashedTreeSpaceVertices[ia], hashedTreeSpaceVertices[ib]) < CSGConstants.kSqrVertexEqualEpsilon)
                                    { dup++; break; }
                                }
                            }
                            if (dup > 0)
                                LogCleanupDup(brushIndexOrder.nodeOrder, surfaceInfo.basePlaneIndex, l, dup);
                        }
                        output.Write(edges.Length);
                        for (int e = 0; e < edges.Length; e++)
                            output.Write(edges[e]);
                    }
                    dbgReferenced.Dispose();
                    output.EndForEachIndex();
                }
                finally
                {
                    hashedTreeSpaceVertices.Dispose();
                }
            }
            finally
			{
				if (categories1.IsCreated) categories1.Dispose();
				if (categories2.IsCreated) categories2.Dispose();
				if (outEdges.IsCreated) outEdges.Dispose();
				if (intersectedHoleIndices.IsCreated) intersectedHoleIndices.Dispose();
				if (intersectionSurfaceInfo.IsCreated) intersectionSurfaceInfo.Dispose();
				if (destroyedEdges.IsCreated) destroyedEdges.Dispose();
				if (allInfos.IsCreated) allInfos.Dispose();
				if (intersectionSurfaceInfos.IsCreated) intersectionSurfaceInfos.Dispose();
				if (basePolygonSurfaceInfos.IsCreated) basePolygonSurfaceInfos.Dispose();
				if (alltreeSpacePlanes.IsCreated) alltreeSpacePlanes.Dispose();
				if (allSegments.IsCreated) allSegments.Dispose();
				if (allCombinedEdges.IsCreated) allCombinedEdges.Dispose();
				if (hashedTreeSpaceVertices.IsCreated) hashedTreeSpaceVertices.Dispose();
				if (indexRemap.IsCreated) indexRemap.Dispose();
                if (holeIndices.IsCreated)
				{
					if (holeIndices.IsCreated)
					{
						for (int i = 0; i < holeIndices.Length; i++)
						{
							if (holeIndices[i].IsCreated)
								holeIndices[i].Dispose();
							holeIndices[i] = default;
						}
					}
					holeIndices.Dispose();
                }
                if (surfaceLoopIndices.IsCreated)
				{
					if (surfaceLoopIndices.IsCreated)
					{
						for (int i = 0; i < surfaceLoopIndices.Length; i++)
						{
							if (surfaceLoopIndices[i].IsCreated)
								surfaceLoopIndices[i].Dispose();
							surfaceLoopIndices[i] = default;
						}
					}
					surfaceLoopIndices.Dispose();
                }
                if (allEdges.IsCreated)
				{ 
					if (allEdges.IsCreated)
					{
						for (int i = 0; i < allEdges.Length; i++)
						{
							if (allEdges[i].IsCreated)
								allEdges[i].Dispose();
							allEdges[i] = default;
						}
					}
					allEdges.Dispose();
                }
                if (intersectionLoops.IsCreated)
				{
					if (intersectionLoops.IsCreated)
					{
						for (int i = 0; i < intersectionLoops.Length; i++) 
						{
							if (intersectionLoops[i].IsCreated)
								intersectionLoops[i].Dispose();
							intersectionLoops[i] = default;
						}
					}
					intersectionLoops.Dispose();
                }
                if (basePolygonEdges.IsCreated)
				{
					if (basePolygonEdges.IsCreated)
					{
						for (int i = 0; i < basePolygonEdges.Length; i++)
						{
							if (basePolygonEdges[i].IsCreated)
								basePolygonEdges[i].Dispose();
							basePolygonEdges[i] = default;
						}
					}
					basePolygonEdges.Dispose();
                }
                if (intersectionEdges.IsCreated)
				{
					if (intersectionEdges.IsCreated)
					{
						for (int i = 0; i < intersectionEdges.Length; i++)
						{
							if (intersectionEdges[i].IsCreated)
								intersectionEdges[i].Dispose();
							intersectionEdges[i] = default;
						}
					}
					intersectionEdges.Dispose();
                }
			}
        }
    }
} 