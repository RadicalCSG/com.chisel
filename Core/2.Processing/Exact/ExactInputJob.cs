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
        public BlobArray<ExactPlane>  planes;
        public BlobArray<ExactVertex> corners;
        // > 0: the planes enclose volume. 0: they enclose none. -1: the brush takes no part - it has no mesh, a plane that
        // cannot be made exact, or planes that do not close it within the world.
        public int                    cornerCount;
        public MinMaxAABB             bounds;       // contains the corners; empty without them
    }

    static class ExactBrushes
    {
        public static BlobAssetReference<ExactBrush> Build(BlobAssetReference<BrushMeshBlob> meshRef, in float4x4 nodeToTree,
                                                           ref ExactFaceBuilder builder, ref ExactList<ExactVertex> corners,
                                                           out ExactCSGStat failure)
        {
            failure = ExactCSGStat.Count;
            builder.planes.Clear();
            corners.Clear();

            bool valid = meshRef.IsCreated;
            if (valid)
                valid = AddPlanes(ref meshRef.Value, nodeToTree, ref builder.planes);
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
                if (builder.PolytopeCorners(ref corners) != ExactFaceFailure.None)
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
            var cornerArray = blobBuilder.Allocate(ref root.corners, math.max(0, cornerCount));
            for (int c = 0; c < cornerCount; c++)
                cornerArray[c] = corners[c];
            root.cornerCount = cornerCount;
            root.bounds      = box;
            return blobBuilder.CreateBlobAssetReference<ExactBrush>(Allocator.Persistent); // disposed through ExactInputJob's dispose list or CacheRemappingJob
        }

        static bool AddPlanes(ref BrushMeshBlob mesh, in float4x4 m, ref ExactList<ExactPlane> planes)
        {
            var affine = ExactAffine.Create(m.c0.x, m.c0.y, m.c0.z, m.c1.x, m.c1.y, m.c1.z,
                                            m.c2.x, m.c2.y, m.c2.z, m.c3.x, m.c3.y, m.c3.z);
            bool valid = affine.IsValid;
            for (int p = 0; p < mesh.localPlaneCount; p++)
            {
                var plane = default(ExactPlane);
                if (affine.IsValid)
                {
                    var local = mesh.localPlanes[p];
                    affine.TransformPlane(local.x, local.y, local.z, local.w, out double nx, out double ny, out double nz, out double d);
                    plane = ExactPlane.Quantize(nx, ny, nz, d);
                }
                valid &= plane.IsValid;
                planes.Add(plane);
            }
            return valid;
        }

        // Widens the box to contain the exact corner: each coordinate rounded down for the minimum and up for the maximum.
        static void Include(ref MinMaxAABB box, in ExactVertex corner)
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
