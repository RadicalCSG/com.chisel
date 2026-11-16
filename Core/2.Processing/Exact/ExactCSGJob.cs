using System.Threading;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using ReadOnlyAttribute = Unity.Collections.ReadOnlyAttribute;
using WriteOnlyAttribute = Unity.Collections.WriteOnlyAttribute;

namespace Chisel.Core
{
    enum ExactCSGStat
    {
        Brushes,
        Faces,
        InputBrushes,           // brushes whose exact input (ExactBrush) was built in this update: only the ones that changed
        InvalidPlane,           // a brush with a plane that cannot be made exact (ExactInputJob): it takes no part
        OpenBrush,              // a brush whose exact planes do not close it within the world: it takes no part
        FaceOutsideBounds,
        DegenerateVertex,
        RoutingOutOfRange,
        UnbalancedVertex,
        UnpairedEdge,
        NoBridge,
        NoEar,
        Count
    }

    [BurstCompile(CompileSynchronously = true)]
    struct ExactCSGJob : IJobParallelForDefer
    {
        // Read
        [NoAlias, ReadOnly] public NativeList<IndexOrder>                                   allUpdateBrushIndexOrders;
        [NoAlias, ReadOnly] public NativeArray<BlobAssetReference<BrushMeshBlob>>           brushMeshLookup;
        [NoAlias, ReadOnly] public NativeList<BlobAssetReference<ExactBrush>>               exactBrushCache;    // by node order (ExactInputJob)
        [NoAlias, ReadOnly] public NativeList<BlobAssetReference<BrushesTouchedByBrush>>    brushesTouchedByBrushCache;
        [NoAlias, ReadOnly] public NativeList<BlobAssetReference<RoutingTable>>             routingTableCache;
        [NoAlias, ReadOnly] public NativeReference<BlobAssetReference<CompactTree>>         compactTreeRef;
        [NoAlias, ReadOnly] public bool                                                     captureExact;
        // the Discarded debug visualization is shown: write what the CSG removed of each face too
        [NoAlias, ReadOnly] public bool                                                     collectDiscarded;

        // Write
        [NativeDisableParallelForRestriction]
        [NoAlias] public NativeArray<int>                                                   stats;
        [NoAlias, WriteOnly] public NativeStream.Writer                                     output;
        [NoAlias, WriteOnly] public NativeStream.Writer                                     capture;    // only with captureExact

        unsafe void Count(ExactCSGStat stat, int amount = 1)
        {
            if (!stats.IsCreated || amount == 0)
                return;
            Interlocked.Add(ref ((int*)stats.GetUnsafePtr())[(int)stat], amount);
        }

        // The brush's face planes in tree space, each made exact once, on its own, when the brush last changed (ExactBrush),
        // so one brush's plane is the same in its own job and in all its neighbours'.
        void AddTreePlanes(ref ExactList<ExactPlane> planes, int nodeOrder)
        {
            var exactBrush = exactBrushCache[nodeOrder];
            if (!exactBrush.IsCreated)
                return;
            ref var brushPlanes = ref exactBrush.Value.planes;
            for (int p = 0; p < brushPlanes.Length; p++)
                planes.Add(brushPlanes[p]);
        }

        // > 0 when the brush's exact planes enclose volume (ExactBrush.cornerCount)
        int CornerCount(int nodeOrder)
        {
            var exactBrush = exactBrushCache[nodeOrder];
            return exactBrush.IsCreated ? exactBrush.Value.cornerCount : -1;
        }

        static bool AllValid(ref ExactList<ExactPlane> planes, int offset, int count)
        {
            for (int p = 0; p < count; p++)
                if (!planes[offset + p].IsValid)
                    return false;
            return true;
        }

        // Writes go to the output and, when capturing, to the capture as well.
        void Write(int value)
        {
            output.Write(value);
            if (captureExact) capture.Write(value);
        }

        void CaptureVertex(in ExactOutputVertex vertex)
        {
            var position = new float3(vertex.x, vertex.y, vertex.z);
            capture.Write(vertex.vertex.X);
            capture.Write(vertex.vertex.Y);
            capture.Write(vertex.vertex.Z);
            capture.Write(vertex.vertex.W);
            capture.Write(position);
        }

        public void Execute(int index)
        {
            var brushIndexOrder = allUpdateBrushIndexOrders[index];
            int brushNodeOrder  = brushIndexOrder.nodeOrder;

            output.BeginForEachIndex(index);
            output.Write(brushIndexOrder);
            if (captureExact)
            {
                capture.BeginForEachIndex(index);
                capture.Write(brushIndexOrder);
            }

            var meshRef = brushMeshLookup[brushNodeOrder];
            var routingTableRef = routingTableCache[brushNodeOrder];
            if (!meshRef.IsCreated || !routingTableRef.IsCreated || !compactTreeRef.Value.IsCreated)
            {
                Write(0);
                EndForEachIndex();
                return;
            }
            ref var mesh = ref meshRef.Value;
            int surfaceCount = mesh.polygons.Length;
            Write(surfaceCount);
            Count(ExactCSGStat.Brushes);

            var builder      = ExactFaceBuilder.Create();
            builder.collectDiscarded = collectDiscarded;
            var triangulator = ExactTriangulator.Create();
            var touching     = new ExactList<ExactTouchingBrush>(16);
            var routing      = ExactRouting.Create();
            var triangles    = new ExactList<int>(64);
            try
            {
                // This brush's planes, then every touching brush's that can meet it. A brush whose exact planes enclose
                // no volume draws nothing, nor does one that takes no part (ExactInputJob has counted it).
                AddTreePlanes(ref builder.planes, brushNodeOrder);
                int faceCount = builder.planes.Length;
                bool valid = CornerCount(brushNodeOrder) > 0 && AllValid(ref builder.planes, 0, faceCount);

                AddLevel(brushNodeOrder, brushIndexOrder.compactNodeID, valid, brushNodeOrder, faceCount, ref builder.planes, ref touching, ref routing);
                int ownTouching = touching.Length;
                int ownPlanes   = builder.planes.Length;

                for (int surface = 0; surface < surfaceCount; surface++)
                {
                    // a face that is never output (SurfaceDestinationFlags.None) has nothing to draw: its plane still
                    // bounds the brush, which is all the other brushes need of it
                    if (!valid || surface >= faceCount || mesh.polygons[surface].surface.destinationFlags == SurfaceDestinationFlags.None)
                    {
                        Write(0);
                        continue;
                    }
                    Count(ExactCSGStat.Faces);

                    // The routing of every brush with a face in this face's plane, facing the same way, this face gives
                    // way to
                    ref var own    = ref exactBrushCache[brushNodeOrder].Value;
                    var face       = builder.planes[surface];
                    var faceBounds = own.faceBounds[surface];
                    for (int t = 0; t < ownTouching; t++)
                    {
                        var other = touching[t];
                        if (!other.givesWay ||
                            !math.all(other.bounds.Min <= faceBounds.Max) || !math.all(other.bounds.Max >= faceBounds.Min) ||
                            !HasPlane(ref builder.planes, other.planeOffset, other.planeCount, face))
                            continue;
                        int level = routing.levels.Length;
                        if (AddLevel(other.nodeOrder, other.nodeID, true, brushNodeOrder, faceCount, ref builder.planes, ref touching, ref routing))
                            touching[t].tieLevel = level;
                    }

                    builder.routingErrors = 0;
                    var failure = builder.BuildFace(surface, ref own, ref touching, ref routing);

                    for (int t = 0; t < ownTouching; t++)
                        touching[t].tieLevel = 0;
                    touching.Resize(ownTouching);
                    builder.planes.Resize(ownPlanes);
                    routing.Truncate(1);

                    Count(ExactCSGStat.RoutingOutOfRange, builder.routingErrors);
                    if (failure != ExactFaceFailure.None)
                    {
                        CountFailure(failure);
                        Write(0);
                        continue;
                    }

                    int partCount = (builder.alignedEdges.Length > 0 ? 1 : 0) + (builder.reverseAlignedEdges.Length > 0 ? 1 : 0) +
                                    (builder.discardedEdges.Length > 0 ? 1 : 0);
                    // the face's vertices once, the parts' triangles index them (the capture has them by part)
                    Write(partCount);
                    if (partCount > 0)
                    {
                        output.Write(builder.vertices.Length);
                        for (int v = 0; v < builder.vertices.Length; v++)
                            output.Write(new float3(builder.vertices[v].x, builder.vertices[v].y, builder.vertices[v].z));
                    }
                    for (int pass = 0; pass < 3; pass++)
                    {
                        ref var edges = ref (pass == 0 ? ref builder.alignedEdges : ref (pass == 1 ? ref builder.reverseAlignedEdges : ref builder.discardedEdges));
                        if (edges.Length == 0)
                            continue;
                        triangles.Clear();
                        var result = triangulator.Triangulate(builder.planes[surface], ref builder.vertices, ref edges, ref builder.lines, ref triangles);
                        // what was removed is only looked at: a failure there is no failure of the CSG
                        if (result != ExactTriangulationFailure.None)
                        {
                            if (pass < 2)
                                CountFailure(result);
                            triangles.Clear();
                        }
                        // the discarded part is written as Inside (GenerateSurfaceTrianglesJob.kDiscardedPart)
                        Write((int)(pass == 0 ? CategoryIndex.SelfAligned : pass == 1 ? CategoryIndex.SelfReverseAligned : CategoryIndex.Inside));
                        if (captureExact)
                        {
                            capture.Write(builder.vertices.Length);
                            for (int v = 0; v < builder.vertices.Length; v++)
                                CaptureVertex(builder.vertices[v]);
                        }
                        Write(triangles.Length);
                        for (int t = 0; t < triangles.Length; t++)
                            Write(triangles[t]);
                    }
                }
            }
            finally
            {
                builder.Dispose();
                triangulator.Dispose();
                touching.Dispose();
                routing.Dispose();
                triangles.Dispose();
            }
            EndForEachIndex();
        }

        static bool HasPlane(ref ExactList<ExactPlane> planes, int offset, int count, in ExactPlane plane)
        {
            for (int p = 0; p < count; p++)
                if (ExactPredicates.SamePlane(plane, planes[offset + p]) > 0)
                    return true;
            return false;
        }

        // A level of routing (ExactRouting): the routing table of brush levelOrder, and, with addTouching, every brush
        // touching it that can meet it, with the categories it gives that brush's faces. The faces built are those of
        // brush faceOrder, whose faceCount planes come first in the plane table; a brush already in the table shares its
        // planes. False when the brush has no routing table, and so draws nothing.
        bool AddLevel(int levelOrder, CompactNodeID levelNodeID, bool addTouching, int faceOrder, int faceCount,
                      ref ExactList<ExactPlane> planes, ref ExactList<ExactTouchingBrush> touching, ref ExactRouting routing)
        {
            var routingTableRef = routingTableCache[levelOrder];
            if (!routingTableRef.IsCreated)
                return false;
            ref var routingTable = ref routingTableRef.Value;

            int firstLookup = routing.lookupStart.Length;
            int firstRow    = routing.rows.Length / ExactFaceBuilder.kCategoryCount;
            ref var lookups = ref routingTable.routingLookups;
            for (int k = 0; k < lookups.Length; k++)
            {
                routing.lookupStart.Add(firstRow + lookups[k].startIndex);
                routing.lookupEnd.Add(firstRow + lookups[k].endIndex);
                routing.lookupBrush.Add(-1);
            }
            ref var rows = ref routingTable.routingRows;
            for (int r = 0; r < rows.Length; r++)
            {
                var row = rows[r];
                routing.rows.Add(row.inside);
                routing.rows.Add(row.aligned);
                routing.rows.Add(row.selfAligned);
                routing.rows.Add(row.selfReverseAligned);
                routing.rows.Add(row.reverseAligned);
                routing.rows.Add(row.outside);
            }
            routing.levels.Add(new int3(firstLookup, routing.lookupStart.Length, firstRow));

            var touchedRef = brushesTouchedByBrushCache[levelOrder];
            if (!addTouching || !touchedRef.IsCreated)
                return true;

            ref var compactTree = ref compactTreeRef.Value.Value;
            var levelContents   = compactTree.GetBrushContents(levelNodeID);
            ref var intersections = ref touchedRef.Value.brushIntersections;
            int firstTouching = touching.Length;
            int lastOrder = -1;
            for (int i = 0; i < intersections.Length; i++)
            {
                var intersection = intersections[i];
                if (intersection.type != IntersectionType.Intersection)
                    continue;
                int otherOrder = intersection.nodeIndexOrder.nodeOrder;
                if (otherOrder == levelOrder || otherOrder < 0 || otherOrder >= brushMeshLookup.Length)
                    continue;
                var otherMesh = brushMeshLookup[otherOrder];
                if (!otherMesh.IsCreated)
                    continue;
                // the same brush listed twice is one brush: in node order (StoreBrushIntersectionsJob) that is the one
                // before it, else any before it
                bool duplicate = false;
                if (otherOrder > lastOrder)
                    lastOrder = otherOrder;
                else
                {
                    for (int t = firstTouching; t < touching.Length && !duplicate; t++)
                        duplicate = touching[t].nodeOrder == otherOrder;
                }
                if (duplicate)
                    continue;
                // A brush without volume covers nothing, and one that takes no part covers nothing either, the same way
                // in every job that sees it; the exact broad phase never pairs either of them.
                if (CornerCount(otherOrder) <= 0)
                    continue;

                int offset = -1, count = 0;
                if (otherOrder == faceOrder)
                {
                    offset = 0;
                    count  = faceCount;
                }
                else
                {
                    for (int t = 0; t < firstTouching && offset < 0; t++)
                    {
                        if (touching[t].nodeOrder != otherOrder)
                            continue;
                        offset = touching[t].planeOffset;
                        count  = touching[t].planeCount;
                    }
                }
                if (offset < 0)
                {
                    offset = planes.Length;
                    AddTreePlanes(ref planes, otherOrder);
                    count = planes.Length - offset;
                }

                var otherNodeID   = intersection.nodeIndexOrder.compactNodeID;
                var otherContents = compactTree.GetBrushContents(otherNodeID);
                bool later        = otherOrder > levelOrder;
                var touchingBrush = new ExactTouchingBrush
                {
                    planeOffset = offset,
                    planeCount  = count,
                    nodeOrder   = otherOrder,
                    nodeID      = otherNodeID,
                    givesWay    = ContentsRules.GivesWay(levelContents.Contents, levelContents.Carving, otherContents.Contents, otherContents.Carving, later),
                    insideCategory         = (byte)ContentsRules.Rewrite(CategoryIndex.Inside,         levelContents.Contents, levelContents.Carving, otherContents.Contents, otherContents.Carving, otherContents.Intersecting, later),
                    alignedCategory        = (byte)ContentsRules.Rewrite(CategoryIndex.Aligned,        levelContents.Contents, levelContents.Carving, otherContents.Contents, otherContents.Carving, otherContents.Intersecting, later),
                    reverseAlignedCategory = (byte)ContentsRules.Rewrite(CategoryIndex.ReverseAligned, levelContents.Contents, levelContents.Carving, otherContents.Contents, otherContents.Carving, otherContents.Intersecting, later),
                    routes = false,
                    bounds = exactBrushCache[otherOrder].Value.bounds
                };

                // Its lookup in the routing table, if it has one
                var idWithOffset = otherNodeID.slotIndex.index - routingTable.nodeIDOffset;
                if (idWithOffset >= 0 && idWithOffset < routingTable.nodeIDToTableIndex.Length)
                {
                    int lookup = routingTable.nodeIDToTableIndex[idWithOffset];
                    if (lookup >= 0 && lookup < lookups.Length)
                    {
                        routing.lookupBrush[firstLookup + lookup] = touching.Length;
                        touchingBrush.routes = true;
                    }
                }
                touching.Add(touchingBrush);
            }
            return true;
        }

        void EndForEachIndex()
        {
            output.EndForEachIndex();
            if (captureExact)
                capture.EndForEachIndex();
        }

        void CountFailure(ExactFaceFailure failure)
        {
            switch (failure)
            {
                case ExactFaceFailure.InvalidPlane:      Count(ExactCSGStat.InvalidPlane); break;
                case ExactFaceFailure.FaceOutsideBounds: Count(ExactCSGStat.FaceOutsideBounds); break;
                case ExactFaceFailure.DegenerateVertex:  Count(ExactCSGStat.DegenerateVertex); break;
            }
        }

        void CountFailure(ExactTriangulationFailure failure)
        {
            switch (failure)
            {
                case ExactTriangulationFailure.UnbalancedVertex: Count(ExactCSGStat.UnbalancedVertex); break;
                case ExactTriangulationFailure.UnpairedEdge:     Count(ExactCSGStat.UnpairedEdge); break;
                case ExactTriangulationFailure.NoBridge:         Count(ExactCSGStat.NoBridge); break;
                case ExactTriangulationFailure.NoEar:            Count(ExactCSGStat.NoEar); break;
            }
        }
    }
}
