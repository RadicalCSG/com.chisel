using System.Runtime.CompilerServices;
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
    struct ExactBrush
    {
        // the planes the CSG uses: ownPlanes, where a face lies in another brush's coplanar face that face's plane
        // (ExactPlaneMergeJob)
        public BlobArray<ExactPlane>  planes;
        // each face's plane through its own snapped corners (ExactBrushes.AddPlanes), and those corners: by face, offset
        // and count into facePoints
        public BlobArray<ExactPlane>  ownPlanes;
        // each face's float plane rounded (ExactPlane.Quantize): faces of different brushes that round to one plane are
        // one plane in float, the faces the merge makes one
        public BlobArray<ExactPlane>  floatPlanes;
        public BlobArray<int2>        faceRanges;
        public BlobArray<ExactPoint>  facePoints;
        public BlobArray<ExactVertex> corners;
        // > 0: the planes enclose volume. 0: they enclose none. -1: the brush takes no part - it has no mesh, a plane that
        // cannot be made exact, or planes that do not close it within the world.
        public int                    cornerCount;
        public MinMaxAABB             bounds;       // contains the corners; empty without them
        // by face: its polygon (offset and count into faceEdges, none when it has no area) and the box round it
        public BlobArray<int2>             faces;
        public BlobArray<MinMaxAABB>       faceBounds;
        public BlobArray<ExactPolygonEdge> faceEdges;
    }

    static class ExactBrushes
    {
        public static BlobAssetReference<ExactBrush> Build(BlobAssetReference<BrushMeshBlob> meshRef, in float4x4 nodeToTree,
                                                           ref ExactFaceBuilder builder, ref ExactList<ExactVertex> corners,
                                                           out ExactCSGStat failure)
        {
            builder.planes.Clear();
            var faceRanges  = new ExactList<int2>(8);
            var facePoints  = new ExactList<ExactPoint>(32);
            var ownPlanes   = new ExactList<ExactPlane>(8);
            var floatPlanes = new ExactList<ExactPlane>(8);
            try
            {
                bool valid = meshRef.IsCreated && AddPlanes(ref meshRef.Value, nodeToTree, ref builder.planes, ref faceRanges, ref facePoints, ref floatPlanes);
                for (int p = 0; p < builder.planes.Length; p++)
                    ownPlanes.Add(builder.planes[p]);
                return Make(valid, ref builder, ref corners, ref ownPlanes, ref floatPlanes, ref faceRanges, ref facePoints, out failure);
            }
            finally
            {
                faceRanges.Dispose();
                facePoints.Dispose();
                ownPlanes.Dispose();
                floatPlanes.Dispose();
            }
        }

        // The brush again with the planes in builder.planes (ExactPlaneMergeJob), one for each of its own
        public static BlobAssetReference<ExactBrush> Rebuild(ref ExactBrush previous, ref ExactFaceBuilder builder,
                                                             ref ExactList<ExactVertex> corners, out ExactCSGStat failure)
        {
            var faceRanges  = new ExactList<int2>(math.max(1, previous.faceRanges.Length));
            var facePoints  = new ExactList<ExactPoint>(math.max(1, previous.facePoints.Length));
            var ownPlanes   = new ExactList<ExactPlane>(math.max(1, previous.ownPlanes.Length));
            var floatPlanes = new ExactList<ExactPlane>(math.max(1, previous.floatPlanes.Length));
            try
            {
                for (int p = 0; p < previous.ownPlanes.Length; p++)
                    ownPlanes.Add(previous.ownPlanes[p]);
                for (int p = 0; p < previous.floatPlanes.Length; p++)
                    floatPlanes.Add(previous.floatPlanes[p]);
                for (int f = 0; f < previous.faceRanges.Length; f++)
                    faceRanges.Add(previous.faceRanges[f]);
                for (int v = 0; v < previous.facePoints.Length; v++)
                    facePoints.Add(previous.facePoints[v]);
                bool valid = builder.planes.Length == ownPlanes.Length;
                for (int p = 0; p < builder.planes.Length && valid; p++)
                    valid = builder.planes[p].IsValid;
                return Make(valid, ref builder, ref corners, ref ownPlanes, ref floatPlanes, ref faceRanges, ref facePoints, out failure);
            }
            finally
            {
                faceRanges.Dispose();
                facePoints.Dispose();
                ownPlanes.Dispose();
                floatPlanes.Dispose();
            }
        }

        // The brush with the planes in builder.planes: its polytope, and what it was made from
        static BlobAssetReference<ExactBrush> Make(bool valid, ref ExactFaceBuilder builder, ref ExactList<ExactVertex> corners,
                                                   ref ExactList<ExactPlane> ownPlanes, ref ExactList<ExactPlane> floatPlanes, ref ExactList<int2> faceRanges,
                                                   ref ExactList<ExactPoint> facePoints, out ExactCSGStat failure)
        {
            failure = ExactCSGStat.Count;
            corners.Clear();
            var faces     = new ExactList<int2>(8);
            var faceEdges = new ExactList<ExactPolygonEdge>(32);
            int planeCount = builder.planes.Length;

            int cornerCount = -1;
            var box = new MinMaxAABB { Min = new float3(float.PositiveInfinity), Max = new float3(float.NegativeInfinity) };
            if (!valid)
            {
                // Counted once, here: without corners it meets no brush and draws nothing
                if (planeCount > 0)
                    failure = ExactCSGStat.InvalidPlane;
            } else if (planeCount > 0)
            {
                if (builder.PolytopeCorners(ref corners, ref faces, ref faceEdges) != ExactFaceFailure.None)
                    failure = ExactCSGStat.OpenBrush;
                else
                {
                    cornerCount = corners.Length;
                    for (int c = 0; c < cornerCount; c++)
                        Include(ref box, corners[c]);
                }
            }

            using var blobBuilder = new BlobBuilder(Allocator.Temp);
            ref var root = ref blobBuilder.ConstructRoot<ExactBrush>();
            var planeArray = blobBuilder.Allocate(ref root.planes, planeCount);
            for (int p = 0; p < planeCount; p++)
                planeArray[p] = builder.planes[p];
            var ownArray = blobBuilder.Allocate(ref root.ownPlanes, ownPlanes.Length);
            for (int p = 0; p < ownPlanes.Length; p++)
                ownArray[p] = ownPlanes[p];
            var floatArray = blobBuilder.Allocate(ref root.floatPlanes, floatPlanes.Length);
            for (int p = 0; p < floatPlanes.Length; p++)
                floatArray[p] = floatPlanes[p];
            var rangeArray = blobBuilder.Allocate(ref root.faceRanges, faceRanges.Length);
            for (int f = 0; f < faceRanges.Length; f++)
                rangeArray[f] = faceRanges[f];
            var pointArray = blobBuilder.Allocate(ref root.facePoints, facePoints.Length);
            for (int v = 0; v < facePoints.Length; v++)
                pointArray[v] = facePoints[v];
            var cornerArray = blobBuilder.Allocate(ref root.corners, math.max(0, cornerCount));
            for (int c = 0; c < cornerCount; c++)
                cornerArray[c] = corners[c];
            root.cornerCount = cornerCount;
            root.bounds      = box;
            // the faces only of a brush that encloses volume: no other one is built
            int faceCount   = cornerCount > 0 ? faces.Length : 0;
            var faceArray   = blobBuilder.Allocate(ref root.faces, faceCount);
            var boundsArray = blobBuilder.Allocate(ref root.faceBounds, faceCount);
            var edgeArray   = blobBuilder.Allocate(ref root.faceEdges, faceCount > 0 ? faceEdges.Length : 0);
            for (int e = 0; e < edgeArray.Length; e++)
                edgeArray[e] = faceEdges[e];
            for (int f = 0; f < faceCount; f++)
            {
                faceArray[f] = faces[f];
                var faceBox = new MinMaxAABB { Min = new float3(float.PositiveInfinity), Max = new float3(float.NegativeInfinity) };
                for (int e = 0; e < faces[f].y; e++)
                    Include(ref faceBox, faceEdges[faces[f].x + e].start);
                boundsArray[f] = faceBox;
            }
            faces.Dispose();
            faceEdges.Dispose();
            return blobBuilder.CreateBlobAssetReference<ExactBrush>(Allocator.Persistent); // disposed through ExactInputJob's dispose list or CacheRemappingJob
        }

        // The brush's face planes in tree space. Each is made exactly from the face's corners snapped to the grid
        // (ExactPlane.FromPoints), so faces that meet at a corner meet there exactly, however many of them there are. A face
        // whose snapped corners do not lie in one plane takes the plane through the three that span the most, chosen by
        // position, so a brush with the same face makes the same plane. A face with no three corners off one line (smaller
        // than the grid), or one too large for FromPoints, takes its own plane rounded (ExactPlane.Quantize).
        internal static bool AddPlanes(ref BrushMeshBlob mesh, in float4x4 m, ref ExactList<ExactPlane> planes)
        {
            var faceRanges  = new ExactList<int2>(8);
            var facePoints  = new ExactList<ExactPoint>(32);
            var floatPlanes = new ExactList<ExactPlane>(8);
            try
            {
                return AddPlanes(ref mesh, m, ref planes, ref faceRanges, ref facePoints, ref floatPlanes);
            }
            finally
            {
                faceRanges.Dispose();
                facePoints.Dispose();
                floatPlanes.Dispose();
            }
        }

        // The same, with each face's snapped corners (by face, offset and count into facePoints; none where a corner did not
        // snap, or for a plane without a polygon) and its float plane rounded (ExactPlane.Quantize)
        internal static bool AddPlanes(ref BrushMeshBlob mesh, in float4x4 m, ref ExactList<ExactPlane> planes,
                                       ref ExactList<int2> faceRanges, ref ExactList<ExactPoint> facePoints,
                                       ref ExactList<ExactPlane> floatPlanes)
        {
            var affine = ExactAffine.Create(m.c0.x, m.c0.y, m.c0.z, m.c1.x, m.c1.y, m.c1.z,
                                            m.c2.x, m.c2.y, m.c2.z, m.c3.x, m.c3.y, m.c3.z);
            bool valid = affine.IsValid;
            var points  = new ExactList<ExactPoint>(math.max(1, mesh.localVertices.Length));
            var snapped = new ExactList<bool>(math.max(1, mesh.localVertices.Length));
            try
            {
                if (affine.IsValid)
                {
                    for (int v = 0; v < mesh.localVertices.Length; v++)
                    {
                        var local = mesh.localVertices[v];
                        affine.TransformPoint(local.x, local.y, local.z, out double x, out double y, out double z);
                        snapped.Add(ExactPlane.SnapPoint(x, y, z, out var point));
                        points.Add(point);
                    }
                }
                for (int p = 0; p < mesh.localPlaneCount; p++)
                {
                    int offset = facePoints.Length;
                    bool all = affine.IsValid && p < mesh.polygons.Length;
                    if (all)
                    {
                        var polygon = mesh.polygons[p];
                        for (int e = 0; e < polygon.edgeCount && all; e++)
                        {
                            int v = mesh.halfEdges[polygon.firstEdge + e].vertexIndex;
                            all = v >= 0 && v < points.Length && snapped[v];
                            if (all)
                                facePoints.Add(points[v]);
                        }
                    }
                    if (!all)
                        facePoints.Resize(offset);
                    int count = facePoints.Length - offset;
                    faceRanges.Add(new int2(offset, count));

                    var plane = default(ExactPlane);
                    var rounded = default(ExactPlane);
                    if (affine.IsValid)
                    {
                        var local = mesh.localPlanes[p];
                        affine.TransformPlane(local.x, local.y, local.z, local.w, out double nx, out double ny, out double nz, out double d);
                        rounded = ExactPlane.Quantize(nx, ny, nz, d);
                        plane = PlaneThrough(ref facePoints, offset, count);
                        if (plane.IsValid && (plane.a * nx + plane.b * ny + plane.c * nz) < 0)
                            plane = plane.Flipped();
                        if (!plane.IsValid)
                            plane = rounded;
                    }
                    valid &= plane.IsValid;
                    planes.Add(plane);
                    floatPlanes.Add(rounded);
                }
            }
            finally
            {
                points.Dispose();
                snapped.Dispose();
            }
            return valid;
        }

        // The plane through three of the snapped points points[offset .. offset + count): the first by position, the one
        // farthest from it, and the one farthest from the line through those two. It depends only on which points there
        // are, not on their order. Facing either way; invalid when the points don't span a plane.
        internal static ExactPlane PlaneThrough(ref ExactList<ExactPoint> points, int offset, int count)
        {
            if (count < 3)
                return default;
            int first = offset;
            for (int i = offset + 1; i < offset + count; i++)
            {
                if (Before(points[i], points[first]))
                    first = i;
            }
            var p0 = points[first];

            int second = -1;
            double farthest = 0;
            for (int i = offset; i < offset + count; i++)
            {
                var p = points[i];
                double dx = p.x - p0.x, dy = p.y - p0.y, dz = p.z - p0.z;
                double distance = dx * dx + dy * dy + dz * dz;
                if (distance > farthest || (distance == farthest && second >= 0 && distance > 0 && Before(p, points[second])))
                {
                    farthest = distance;
                    second = i;
                }
            }
            if (second < 0)
                return default;
            var p1 = points[second];

            int third = -1;
            double widest = 0;
            double ux = p1.x - p0.x, uy = p1.y - p0.y, uz = p1.z - p0.z;
            for (int i = offset; i < offset + count; i++)
            {
                var p = points[i];
                double vx = p.x - p0.x, vy = p.y - p0.y, vz = p.z - p0.z;
                double cx = uy * vz - uz * vy, cy = uz * vx - ux * vz, cz = ux * vy - uy * vx;
                double area = cx * cx + cy * cy + cz * cz;
                if (area > widest || (area == widest && third >= 0 && area > 0 && Before(p, points[third])))
                {
                    widest = area;
                    third = i;
                }
            }
            if (third < 0)
                return default;
            return ExactPlane.FromPoints(p0, p1, points[third]);
        }

        // Lexicographic order of snapped points: x, then y, then z
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static bool Before(in ExactPoint p, in ExactPoint q)
        {
            return p.x != q.x ? p.x < q.x : (p.y != q.y ? p.y < q.y : p.z < q.z);
        }

        // Widens the box to contain the exact corner: each coordinate rounded down for the minimum and up for the maximum.
        public static void Include(ref MinMaxAABB box, in ExactVertex corner)
        {
            var min = new float3(ExactPredicates.DirectedToFloat(corner.X, corner.W, false),
                                 ExactPredicates.DirectedToFloat(corner.Y, corner.W, false),
                                 ExactPredicates.DirectedToFloat(corner.Z, corner.W, false));
            var max = new float3(ExactPredicates.DirectedToFloat(corner.X, corner.W, true),
                                 ExactPredicates.DirectedToFloat(corner.Y, corner.W, true),
                                 ExactPredicates.DirectedToFloat(corner.Z, corner.W, true));
            box.Min = math.min(box.Min, min);
            box.Max = math.max(box.Max, max);
        }
    }

    [BurstCompile(CompileSynchronously = true)]
    struct ExactInputJob : IJobParallelForDefer
    {
        // Read
        [NoAlias, ReadOnly] public NativeList<IndexOrder>                           rebuildTreeBrushIndexOrders;
        [NoAlias, ReadOnly] public NativeArray<BlobAssetReference<BrushMeshBlob>>   brushMeshLookup;
        [NoAlias, ReadOnly] public NativeList<NodeTransformations>                  transformationCache;

        // Read / Write
        [NativeDisableParallelForRestriction]
        [NoAlias] public NativeArray<int>                                           stats;
        [NativeDisableParallelForRestriction]
        [NoAlias] public NativeList<BlobAssetReference<ExactBrush>>                 exactBrushCache;    // by node order

        // Write
        [NoAlias, WriteOnly] public NativeList<BlobAssetReference<ExactBrush>>.ParallelWriter disposeList;

        public unsafe void Execute(int index)
        {
            int nodeOrder = rebuildTreeBrushIndexOrders[index].nodeOrder;
            var builder = ExactFaceBuilder.Create();
            var corners = new ExactList<ExactVertex>(32);
            try
            {
                var brush = ExactBrushes.Build(brushMeshLookup[nodeOrder], transformationCache[nodeOrder].nodeToTree,
                                               ref builder, ref corners, out var failure);
                var counters = (int*)stats.GetUnsafePtr();
                Interlocked.Increment(ref counters[(int)ExactCSGStat.InputBrushes]);
                if (failure != ExactCSGStat.Count)
                    Interlocked.Increment(ref counters[(int)failure]);
                var previous = exactBrushCache[nodeOrder];
                if (previous.IsCreated)
                    disposeList.AddNoResize(previous);
                exactBrushCache[nodeOrder] = brush;
            }
            finally
            {
                builder.Dispose();
                corners.Dispose();
            }
        }
    }

    [BurstCompile(CompileSynchronously = true)]
    struct ExactBoundsJob : IJob
    {
        // Read
        [NoAlias, ReadOnly] public NativeList<IndexOrder>                           allTreeBrushIndexOrders;
        [NoAlias, ReadOnly] public NativeArray<BlobAssetReference<BrushMeshBlob>>   brushMeshLookup;
        [NoAlias, ReadOnly] public NativeList<NodeTransformations>                  transformationCache;

        // Read / Write
        [NoAlias] public NativeArray<int>                                           stats;
        [NoAlias] public NativeList<BlobAssetReference<ExactBrush>>                 exactBrushCache;    // by node order

        // Write
        [NoAlias] public NativeList<MinMaxAABB>                                     bounds;             // by node order

        public void Execute()
        {
            int brushCount = allTreeBrushIndexOrders.Length;
            bounds.Clear();
            bounds.ResizeUninitialized(brushCount);
            var builder = ExactFaceBuilder.Create();
            var corners = new ExactList<ExactVertex>(32);
            try
            {
                for (int b = 0; b < brushCount; b++)
                {
                    int nodeOrder = allTreeBrushIndexOrders[b].nodeOrder;
                    if (!exactBrushCache[nodeOrder].IsCreated)
                    {
                        exactBrushCache[nodeOrder] = ExactBrushes.Build(brushMeshLookup[nodeOrder], transformationCache[nodeOrder].nodeToTree,
                                                                        ref builder, ref corners, out var failure);
                        stats[(int)ExactCSGStat.InputBrushes]++;
                        if (failure != ExactCSGStat.Count)
                            stats[(int)failure]++;
                    }
                    bounds[nodeOrder] = exactBrushCache[nodeOrder].Value.bounds;
                }
            }
            finally
            {
                builder.Dispose();
                corners.Dispose();
            }
        }
    }
}
