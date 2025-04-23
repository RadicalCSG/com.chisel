using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using Debug = UnityEngine.Debug;
using Vector3 = UnityEngine.Vector3;
using Quaternion = UnityEngine.Quaternion;
using ReadOnlyAttribute = Unity.Collections.ReadOnlyAttribute;
using WriteOnlyAttribute = Unity.Collections.WriteOnlyAttribute;
using Unity.Entities;
using andywiecko.BurstTriangulator.LowLevel.Unsafe;
using andywiecko.BurstTriangulator;

namespace Chisel.Core
{
    // [BurstCompile(CompileSynchronously = true)]
    struct GenerateSurfaceTrianglesJob : IJobParallelForDefer
    {
        // Read
        [NoAlias, ReadOnly] public NativeList<IndexOrder>                           allUpdateBrushIndexOrders;
        [NoAlias, ReadOnly] public NativeList<BlobAssetReference<BasePolygonsBlob>> basePolygonCache;
        [NoAlias, ReadOnly] public NativeList<NodeTransformations>                  transformationCache;
        [NoAlias, ReadOnly] public NativeStream.Reader                              input;
        [NoAlias, ReadOnly] public NativeArray<MeshQuery>                           meshQueries;

        // Write
        [NativeDisableParallelForRestriction]
        [NoAlias] public NativeList<BlobAssetReference<ChiselBrushRenderBuffer>>    brushRenderBufferCache;

        // Scratch per-thread
        [NativeDisableContainerSafetyRestriction] HashedVertices                    brushVertices;
        [NativeDisableContainerSafetyRestriction] NativeList<UnsafeList<int>>       surfaceLoopIndices;
        [NativeDisableContainerSafetyRestriction] NativeArray<SurfaceInfo>          surfaceLoopAllInfos;
        [NativeDisableContainerSafetyRestriction] NativeList<UnsafeList<Edge>>      surfaceLoopAllEdges;

        [BurstDiscard]
        public static void InvalidFinalCategory(CategoryIndex _interiorCategory)
        {
            Debug.Assert(false, $"Invalid final category {_interiorCategory}");
        }

        struct CompareSortByBasePlaneIndex : System.Collections.Generic.IComparer<ChiselQuerySurface>
        {
            public readonly int Compare(ChiselQuerySurface x, ChiselQuerySurface y)
            {
                var diff = x.surfaceParameter - y.surfaceParameter;
                if (diff != 0) return diff;
                return x.surfaceIndex - y.surfaceIndex;
            }
        }
        static readonly CompareSortByBasePlaneIndex compareSortByBasePlaneIndex = new();

        public unsafe void Execute(int index)
        {
            var count = input.BeginForEachIndex(index);
            if (count == 0)
                return;

            // Read brush data
            var brushIndexOrder = input.Read<IndexOrder>();
            var brushNodeOrder  = brushIndexOrder.nodeOrder;
            var vertexCount     = input.Read<int>();
            NativeCollectionHelpers.EnsureCapacityAndClear(ref brushVertices, vertexCount);
            for (int v = 0; v < vertexCount; v++)
                brushVertices.AddNoResize(input.Read<float3>());

            // Pre-check: need at least 3 vertices
            if (brushVertices.Length < 3)
            {
                input.EndForEachIndex();
                return;
            }

            // Read surface loops
            var surfaceOuterCount = input.Read<int>();
            NativeCollectionHelpers.EnsureSizeAndClear(ref surfaceLoopIndices, surfaceOuterCount);
            for (int o = 0; o < surfaceOuterCount; o++)
            {
                UnsafeList<int> inner = default;
                var countInner = input.Read<int>();
                if (countInner > 0)
                {
                    inner = new UnsafeList<int>(countInner, Allocator.Temp);
                    for (int i = 0; i < countInner; i++)
                        inner.AddNoResize(input.Read<int>());
                }
                surfaceLoopIndices[o] = inner;
            }

            // Read loop infos and edges
            var loopCount = input.Read<int>();
            NativeCollectionHelpers.EnsureMinimumSizeAndClear(ref surfaceLoopAllInfos, loopCount);
            NativeCollectionHelpers.EnsureSizeAndClear(ref surfaceLoopAllEdges, loopCount);
            for (int l = 0; l < loopCount; l++)
            {
                surfaceLoopAllInfos[l] = input.Read<SurfaceInfo>();
                var edgeCount = input.Read<int>();
                if (edgeCount > 0)
                {
                    var edges = new UnsafeList<Edge>(edgeCount, Allocator.Temp);
                    for (int e = 0; e < edgeCount; e++)
                        edges.AddNoResize(input.Read<Edge>());
                    surfaceLoopAllEdges[l] = edges;
                }
            }
            input.EndForEachIndex();

            if (!basePolygonCache[brushNodeOrder].IsCreated)
                return;

            // Compute maximum sizes
            int maxLoops = 0, maxIndices = 0;
            for (int s = 0; s < surfaceLoopIndices.Length; s++)
            {
                if (!surfaceLoopIndices[s].IsCreated) continue;
                var len = surfaceLoopIndices[s].Length;
                maxIndices += len;
                maxLoops = math.max(maxLoops, len);
            }

            ref var baseSurfaces            = ref basePolygonCache[brushNodeOrder].Value.surfaces;
            var transform                    = transformationCache[brushNodeOrder];
            var treeToNode                   = transform.treeToNode;
            var nodeToTreeInvTrans          = math.transpose(treeToNode);

            // Scratch allocators
            var vertex2DRemapper = new Vertex2DRemapper
            {
                lookup      = new NativeList<int>(64, Allocator.Temp),
                positions2D = new NativeList<double2>(64, Allocator.Temp),
                edgeIndices = new NativeList<int>(128, Allocator.Temp)
            };
            var uniqueVertexMapper = new UniqueVertexMapper
            {
                indexRemap               = new NativeArray<int>(brushVertices.Length, Allocator.Temp),
                surfaceColliderVertices  = new NativeList<float3>(brushVertices.Length, Allocator.Temp),
                surfaceRenderVertices    = new NativeList<RenderVertex>(brushVertices.Length, Allocator.Temp)
            };

            // Configure triangulation settings
            var settings = new Args(
                autoHolesAndBoundary:       true,
                concentricShellsParameter:  0.001f,
                preprocessor:               Preprocessor.None,
                refineMesh:                 false,
                restoreBoundary:            true,
                sloanMaxIters:              1_000_000,
                validateInput:              true,
                verbose:                    true,
                refinementThresholdAngle:   math.radians(5),
                refinementThresholdArea:    1f
            );

            var surfaceIndexList = new NativeList<int>(maxIndices, Allocator.Temp);
            var loops            = new NativeList<int>(maxLoops, Allocator.Temp);

            var output = new andywiecko.BurstTriangulator.LowLevel.Unsafe.OutputData<double2>
            {
                Triangles = new NativeList<int>(64, Allocator.Temp),
                Status    = new NativeReference<Status>(Allocator.Temp)
            };
            var triangulator = new UnsafeTriangulator<double2>();

            var builder = new BlobBuilder(Allocator.Temp, 4096);
            ref var root = ref builder.ConstructRoot<ChiselBrushRenderBuffer>();
            var surfaceBuffers = builder.Allocate(ref root.surfaces, surfaceLoopIndices.Length);

            // Process each surface
            for (int surf = 0; surf < surfaceLoopIndices.Length; surf++)
            {
                if (!surfaceLoopIndices[surf].IsCreated) continue;
                loops.Clear(); uniqueVertexMapper.Reset();

                // Collect valid loops
                var loopIndices = surfaceLoopIndices[surf];
                for (int i = 0; i < loopIndices.Length; i++)
                {
                    var loopIdx = loopIndices[i];
                    var edges   = surfaceLoopAllEdges[loopIdx];
                    if (edges.Length < 3) continue;
                    loops.AddNoResize(loopIdx);
                }
                if (loops.Length == 0) continue;

                // Setup plane-space mapping
                var plane           = baseSurfaces[surf].localPlane;
                var localToPlane    = MathExtensions.GenerateLocalToPlaneSpaceMatrix(plane);
                var treeToPlane     = math.mul(localToPlane, treeToNode);
                var planeNormalMap  = math.mul(nodeToTreeInvTrans, plane);
                var map3DTo2D       = new Map3DTo2D(planeNormalMap.xyz);

                surfaceIndexList.Clear();
                for (int li = 0; li < loops.Length; li++)
                {
                    var loopIdx  = loops[li];
                    var edges    = surfaceLoopAllEdges[loopIdx];
                    var info     = surfaceLoopAllInfos[loopIdx];
                    Debug.Assert(surf == info.basePlaneIndex, "surfaceIndex mismatch");

                    // Convert 3D -> 2D
                    vertex2DRemapper.ConvertToPlaneSpace(*brushVertices.m_Vertices, edges, map3DTo2D);
                    
                    if (!vertex2DRemapper.CheckForSelfIntersections())
                    {
                        Debug.LogError($"Self-intersection detected in loop {loopIdx} of surface {surf}");
                        // TODO - handle self-intersections
                    }
                    
                    var roVerts = vertex2DRemapper.AsReadOnly();
                    
                    // TODO - check for self-intersections and split them

                    // Pre-check: need enough points and edges
                    if (roVerts.positions2D.Length < 3 || roVerts.edgeIndices.Length < 3)
                        continue;

                    // Triangulate with exception guard
                    output.Triangles.Clear();
                    triangulator.Triangulate(
                        new andywiecko.BurstTriangulator.LowLevel.Unsafe.InputData<double2>
                        {
                            Positions = roVerts.positions2D, 
                            ConstraintEdges = roVerts.edgeIndices
                        },
                        output,
                        settings,
                        Allocator.Temp);
                        
#if UNITY_EDITOR
                    // Inside the loop after calling Triangulate:
                    if (output.Status.Value != Status.OK)
                    {
                        // Log the specific status enum value/name for more detail!
                        Debug.LogError($"Triangulator failed for surface {surf}, loop index {loopIdx} with status {output.Status.Value.ToString()} ({output.Status.Value})");
                    }
                    else if (output.Triangles.Length == 0)
                    {
                        Debug.LogWarning($"Triangulator returned zero triangles for surface {surf}, loop index {loopIdx}.");
                    }
#endif

                    if (output.Status.Value != Status.OK || output.Triangles.Length == 0)
                        continue;

                    // Map triangles back
                    var prevCount = surfaceIndexList.Length;
                    var interiorCat = (CategoryIndex)info.interiorCategory;
                    roVerts.RemapTriangles(interiorCat, output.Triangles, surfaceIndexList);
                    uniqueVertexMapper.RegisterVertices(
                        surfaceIndexList,
                        prevCount,
                        *brushVertices.m_Vertices,
                        map3DTo2D.normal,
                        interiorCat);
                }

                if (surfaceIndexList.Length == 0) continue;

                // Finalize mesh data
                var flags  = baseSurfaces[surf].destinationFlags;
                var parms  = baseSurfaces[surf].destinationParameters;
                var UV0    = baseSurfaces[surf].UV0;
                var uvMat  = math.mul(UV0.ToFloat4x4(), treeToPlane);
                MeshAlgorithms.ComputeUVs(uniqueVertexMapper.surfaceRenderVertices, uvMat);
                MeshAlgorithms.ComputeTangents(surfaceIndexList, uniqueVertexMapper.surfaceRenderVertices);

                ref var buf = ref surfaceBuffers[surf];
                buf.Construct(
                    builder,
                    surfaceIndexList,
                    uniqueVertexMapper.surfaceRenderVertices,
                    uniqueVertexMapper.surfaceColliderVertices,
                    surf,
                    flags,
                    parms);
            }

            // Clean up
            output.Triangles.Dispose(); output.Status.Dispose();
            uniqueVertexMapper.Dispose(); vertex2DRemapper.Dispose();
            surfaceIndexList.Dispose(); loops.Dispose();

            // Build query surfaces...
            var queryList = new NativeList<ChiselQuerySurface>(surfaceBuffers.Length, Allocator.Temp);
            var queryArr  = builder.Allocate(ref root.querySurfaces, meshQueries.Length);
            for (int i = 0; i < meshQueries.Length; i++)
            {
                var mq = meshQueries[i];
                queryList.Clear();
                for (int s = 0; s < surfaceBuffers.Length; s++)
                {
                    ref var rb = ref surfaceBuffers[s];
                    if ((rb.destinationFlags & mq.LayerQueryMask) != mq.LayerQuery) continue;
                    queryList.AddNoResize(new ChiselQuerySurface
                    {
                        surfaceIndex     = rb.surfaceIndex,
                        surfaceParameter = rb.destinationParameters.parameters[Math.Max(0, (int)mq.LayerParameterIndex-1)],
                        vertexCount      = rb.vertexCount,
                        indexCount       = rb.indexCount,
                        surfaceHash      = rb.surfaceHash,
                        geometryHash     = rb.geometryHash
                    });
                }
                queryList.Sort(compareSortByBasePlaneIndex);
                builder.Construct(ref queryArr[i].surfaces, queryList);
                queryArr[i].brushNodeID = brushIndexOrder.compactNodeID;
            }
            queryList.Dispose();

            // Create or replace blob asset
            root.surfaceOffset = 0;
            root.surfaceCount  = surfaceBuffers.Length;
            var asset = builder.CreateBlobAssetReference<ChiselBrushRenderBuffer>(Allocator.Persistent);
            if (brushRenderBufferCache[brushNodeOrder].IsCreated)
                brushRenderBufferCache[brushNodeOrder].Dispose();
            brushRenderBufferCache[brushNodeOrder] = asset;
        }
        
        [BurstDiscard]
        static void LogException(Exception ex)
        {
            Debug.LogError($"Triangulator exception: {ex}");
        }
    }
}