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

        void WriteVertex(in ExactOutputVertex vertex)
        {
            var position = new float3(vertex.x, vertex.y, vertex.z);
            output.Write(position);
            if (!captureExact)
                return;
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
            var triangulator = ExactTriangulator.Create();
            var touching     = new ExactList<ExactTouchingBrush>(16);
            var routingRows  = new ExactList<ushort>(64);
            var lookupStart  = new ExactList<int>(16);
            var lookupEnd    = new ExactList<int>(16);
            var lookupBrush  = new ExactList<int>(16);
            var triangles    = new ExactList<int>(64);
            try
            {
                // This brush's planes, then every touching brush's that can meet it. A brush whose exact planes enclose
                // no volume draws nothing, nor does one that takes no part (ExactInputJob has counted it).
                AddTreePlanes(ref builder.planes, brushNodeOrder);
                int faceCount = builder.planes.Length;
                bool valid = CornerCount(brushNodeOrder) > 0 && AllValid(ref builder.planes, 0, faceCount);

                ref var compactTree   = ref compactTreeRef.Value.Value;
                var processedContents = compactTree.GetBrushContents(brushIndexOrder.compactNodeID);

                ref var routingTable = ref routingTableRef.Value;
                ref var lookups = ref routingTable.routingLookups;
                for (int k = 0; k < lookups.Length; k++)
                {
                    lookupStart.Add(lookups[k].startIndex);
                    lookupEnd.Add(lookups[k].endIndex);
                    lookupBrush.Add(-1);
                }
                ref var rows = ref routingTable.routingRows;
                for (int r = 0; r < rows.Length; r++)
                {
                    var row = rows[r];
                    routingRows.Add(row.inside);
                    routingRows.Add(row.aligned);
                    routingRows.Add(row.selfAligned);
                    routingRows.Add(row.selfReverseAligned);
                    routingRows.Add(row.reverseAligned);
                    routingRows.Add(row.outside);
                }

                var touchedRef = brushesTouchedByBrushCache[brushNodeOrder];
                if (valid && touchedRef.IsCreated)
                {
                    ref var intersections = ref touchedRef.Value.brushIntersections;
                    for (int i = 0; i < intersections.Length; i++)
                    {
                        var intersection = intersections[i];
                        if (intersection.type != IntersectionType.Intersection)
                            continue;
                        int otherOrder = intersection.nodeIndexOrder.nodeOrder;
                        if (otherOrder == brushNodeOrder || otherOrder < 0 || otherOrder >= brushMeshLookup.Length)
                            continue;
                        var otherMesh = brushMeshLookup[otherOrder];
                        if (!otherMesh.IsCreated)
                            continue;
                        // the same brush listed twice is one brush
                        bool duplicate = false;
                        for (int t = 0; t < touching.Length && !duplicate; t++)
                            duplicate = touching[t].nodeOrder == otherOrder;
                        if (duplicate)
                            continue;
                        // A brush without volume covers nothing, and one that takes no part covers nothing either, the
                        // same way in every job that sees it; the exact broad phase never pairs either of them.
                        if (CornerCount(otherOrder) <= 0)
                            continue;

                        int offset = builder.planes.Length;
                        AddTreePlanes(ref builder.planes, otherOrder);
                        int count = builder.planes.Length - offset;

                        var otherNodeID   = intersection.nodeIndexOrder.compactNodeID;
                        var otherContents = compactTree.GetBrushContents(otherNodeID);
                        bool later        = otherOrder > brushNodeOrder;
                        var touchingBrush = new ExactTouchingBrush
                        {
                            planeOffset = offset,
                            planeCount  = count,
                            nodeOrder   = otherOrder,
                            insideCategory         = (byte)ContentsRules.Rewrite(CategoryIndex.Inside,         processedContents.Contents, processedContents.Carving, otherContents.Contents, otherContents.Carving, otherContents.Intersecting, later),
                            alignedCategory        = (byte)ContentsRules.Rewrite(CategoryIndex.Aligned,        processedContents.Contents, processedContents.Carving, otherContents.Contents, otherContents.Carving, otherContents.Intersecting, later),
                            reverseAlignedCategory = (byte)ContentsRules.Rewrite(CategoryIndex.ReverseAligned, processedContents.Contents, processedContents.Carving, otherContents.Contents, otherContents.Carving, otherContents.Intersecting, later),
                            routes = false
                        };

                        // Its lookup in the routing table, if it has one
                        var idWithOffset = otherNodeID.slotIndex.index - routingTable.nodeIDOffset;
                        if (idWithOffset >= 0 && idWithOffset < routingTable.nodeIDToTableIndex.Length)
                        {
                            int lookup = routingTable.nodeIDToTableIndex[idWithOffset];
                            if (lookup >= 0 && lookup < lookupBrush.Length)
                            {
                                lookupBrush[lookup] = touching.Length;
                                touchingBrush.routes = true;
                            }
                        }
                        touching.Add(touchingBrush);
                    }
                }

                TraceBrush(brushNodeOrder, faceCount, ref builder.planes, ref touching, ref lookupBrush, ref lookupStart, ref lookupEnd);

                for (int surface = 0; surface < surfaceCount; surface++)
                {
                    if (!valid || surface >= faceCount)
                    {
                        Write(0);
                        continue;
                    }
                    Count(ExactCSGStat.Faces);
                    builder.routingErrors = 0;
                    var failure = builder.BuildFace(surface, faceCount, ref touching, ref routingRows, ref lookupStart, ref lookupEnd, ref lookupBrush);
                    TraceFace(brushNodeOrder, surface, ref builder, ref touching, failure);
                    Count(ExactCSGStat.RoutingOutOfRange, builder.routingErrors);
                    if (failure != ExactFaceFailure.None)
                    {
                        CountFailure(failure);
                        Write(0);
                        continue;
                    }

                    int partCount = (builder.alignedEdges.Length > 0 ? 1 : 0) + (builder.reverseAlignedEdges.Length > 0 ? 1 : 0);
                    Write(partCount);
                    for (int pass = 0; pass < 2; pass++)
                    {
                        bool aligned = pass == 0;
                        if ((aligned ? builder.alignedEdges.Length : builder.reverseAlignedEdges.Length) == 0)
                            continue;
                        triangles.Clear();
                        var result = aligned
                            ? triangulator.Triangulate(builder.planes[surface], ref builder.vertices, ref builder.alignedEdges, ref builder.lines, ref triangles)
                            : triangulator.Triangulate(builder.planes[surface], ref builder.vertices, ref builder.reverseAlignedEdges, ref builder.lines, ref triangles);
                        if (result != ExactTriangulationFailure.None)
                        {
                            CountFailure(result);
                            triangles.Clear();
                        }
                        Write((int)(aligned ? CategoryIndex.SelfAligned : CategoryIndex.SelfReverseAligned));
                        Write(builder.vertices.Length);
                        for (int v = 0; v < builder.vertices.Length; v++)
                            WriteVertex(builder.vertices[v]);
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
                routingRows.Dispose();
                lookupStart.Dispose();
                lookupEnd.Dispose();
                lookupBrush.Dispose();
                triangles.Dispose();
            }
            EndForEachIndex();
        }

        void EndForEachIndex()
        {
            output.EndForEachIndex();
            if (captureExact)
                capture.EndForEachIndex();
        }

        // Investigation trace (CSGTrace): discarded under Burst, so it only runs when a test turns Burst off.
        [BurstDiscard]
        static void TraceBrush(int brushNodeOrder, int faceCount, ref ExactList<ExactPlane> planes, ref ExactList<ExactTouchingBrush> touching,
                               ref ExactList<int> lookupBrush, ref ExactList<int> lookupStart, ref ExactList<int> lookupEnd)
        {
            if (!CSGTrace.Enabled)
                return;
            var text = new System.Text.StringBuilder();
            text.Append("X b").Append(brushNodeOrder).Append(": faces ").Append(faceCount).Append("; lookups");
            for (int k = 0; k < lookupBrush.Length; k++)
            {
                var t = lookupBrush[k];
                text.Append(' ').Append(k).Append(':').Append(t < 0 ? "-" : ("b" + touching[t].nodeOrder))
                    .Append('[').Append(lookupStart[k]).Append(',').Append(lookupEnd[k]).Append(')');
            }
            text.Append("; touching");
            for (int t = 0; t < touching.Length; t++)
            {
                var brush = touching[t];
                text.Append(" b").Append(brush.nodeOrder).Append(brush.routes ? "" : "(no route)")
                    .Append(" I").Append(brush.insideCategory).Append(" A").Append(brush.alignedCategory).Append(" R").Append(brush.reverseAlignedCategory);
            }
            CSGTrace.Line(text.ToString());
            // prefixed with the brush: jobs run in parallel and their lines interleave
            for (int p = 0; p < planes.Length; p++)
            {
                var plane = planes[p];
                CSGTrace.Line($"X b{brushNodeOrder} plane {p}: {plane.a} {plane.b} {plane.c} {plane.w}");
            }
        }

        [BurstDiscard]
        static void TraceFace(int brushNodeOrder, int surface, ref ExactFaceBuilder builder, ref ExactList<ExactTouchingBrush> touching,
                              ExactFaceFailure failure)
        {
            if (!CSGTrace.Enabled)
                return;
            var text = new System.Text.StringBuilder();
            text.Append("X b").Append(brushNodeOrder).Append(" f").Append(surface).Append(": ").Append(failure)
                .Append("; regions");
            for (int r = 0; r < builder.regions.Length; r++)
            {
                var region = builder.regions[r];
                text.Append(' ').Append(region.brush < 0 ? "face" : ("b" + touching[region.brush].nodeOrder))
                    .Append(":c").Append(region.category).Append(":n").Append(region.polygon.count);
            }
            text.Append("; lines ").Append(builder.lines.Length)
                .Append("; edges aligned ").Append(builder.alignedEdges.Length).Append(" reverse ").Append(builder.reverseAlignedEdges.Length)
                .Append("; vertices");
            for (int v = 0; v < builder.vertices.Length; v++)
            {
                var vertex = builder.vertices[v];
                text.Append(' ').Append(v).Append('(').Append(vertex.x.ToString("0.#####", System.Globalization.CultureInfo.InvariantCulture))
                    .Append(',').Append(vertex.y.ToString("0.#####", System.Globalization.CultureInfo.InvariantCulture))
                    .Append(',').Append(vertex.z.ToString("0.#####", System.Globalization.CultureInfo.InvariantCulture)).Append(')');
            }
            text.Append("; aligned");
            for (int e = 0; e < builder.alignedEdges.Length; e++)
                text.Append(' ').Append(builder.alignedEdges[e].from).Append('>').Append(builder.alignedEdges[e].to);
            text.Append("; reverse");
            for (int e = 0; e < builder.reverseAlignedEdges.Length; e++)
                text.Append(' ').Append(builder.reverseAlignedEdges[e].from).Append('>').Append(builder.reverseAlignedEdges[e].to);
            CSGTrace.Line(text.ToString());
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
