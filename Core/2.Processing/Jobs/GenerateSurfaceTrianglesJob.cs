using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using Debug = UnityEngine.Debug;
using ReadOnlyAttribute = Unity.Collections.ReadOnlyAttribute;
using WriteOnlyAttribute = Unity.Collections.WriteOnlyAttribute;
using Unity.Entities;
using andywiecko.BurstTriangulator.LowLevel.Unsafe;
using andywiecko.BurstTriangulator;

namespace Chisel.Core
{
    //[BurstCompile(CompileSynchronously = true)] // FIXME: For some reason causes infinite loop when burst compiled?
    struct GenerateSurfaceTrianglesJob : IJobParallelForDefer
    {
        // Read
        // 'Required' for scheduling with index count
        [NoAlias, ReadOnly] public NativeList<IndexOrder>                           allUpdateBrushIndexOrders;
        
        [NoAlias, ReadOnly] public NativeList<BlobAssetReference<BasePolygonsBlob>> basePolygonCache;
        [NoAlias, ReadOnly] public NativeList<NodeTransformations>                  transformationCache;
        [NoAlias, ReadOnly] public NativeStream.Reader                              input;        
        [NoAlias, ReadOnly] public NativeArray<MeshQuery>                           meshQueries;
		[NoAlias, ReadOnly] public CompactHierarchyManagerInstance.ReadOnlyInstanceIDLookup instanceIDLookup;

		// Write
		[NativeDisableParallelForRestriction]
        [NoAlias] public NativeList<BlobAssetReference<ChiselBrushRenderBuffer>>    brushRenderBufferCache;

        // Per thread scratch memory
        //[NativeDisableContainerSafetyRestriction] HashedVertices                    brushVertices;
        //[NativeDisableContainerSafetyRestriction] NativeList<UnsafeList<int>>       surfaceLoopIndices;
        //[NativeDisableContainerSafetyRestriction] NativeArray<SurfaceInfo>          surfaceLoopAllInfos;
        //[NativeDisableContainerSafetyRestriction] NativeList<UnsafeList<Edge>>      surfaceLoopAllEdges;
        
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
                if (diff != 0)
                    return diff;
                return x.surfaceIndex - y.surfaceIndex;
            }
        }

		readonly static CompareSortByBasePlaneIndex kCompareSortByBasePlaneIndex = new();

        public unsafe void Execute(int index)
        {
            var count = input.BeginForEachIndex(index);
            if (count == 0)
                return;

            var brushIndexOrder = input.Read<IndexOrder>();
            var brushNodeOrder = brushIndexOrder.nodeOrder;
            var vertexCount = input.Read<int>();

            var brushVertices = new HashedVertices(vertexCount, Allocator.Temp);
			//NativeCollectionHelpers.EnsureCapacityAndClear(ref brushVertices, vertexCount);
			try
            { 

                for (int v = 0; v < vertexCount; v++)
                {
                    var vertex = input.Read<float3>();
                    brushVertices.AddNoResize(vertex);
                }

                var surfaceOuterCount = input.Read<int>();
				var surfaceLoopIndices = new NativeList<UnsafeList<int>>(surfaceOuterCount, Allocator.Temp);
				surfaceLoopIndices.Resize(surfaceOuterCount, NativeArrayOptions.ClearMemory);				
				//NativeCollectionHelpers.EnsureSizeAndClear(ref surfaceLoopIndices, surfaceOuterCount);
                try
                { 
                    for (int o = 0; o < surfaceOuterCount; o++)
                    {
                        UnsafeList<int> inner = default;
                        var surfaceInnerCount = input.Read<int>();
                        if (surfaceInnerCount > 0)
                        {
                            inner = new UnsafeList<int>(surfaceInnerCount, Allocator.Temp);
                            for (int i = 0; i < surfaceInnerCount; i++)
                            {
                                inner.AddNoResize(input.Read<int>());
                            }
                        }
                        surfaceLoopIndices[o] = inner;
                    }

                    var surfaceLoopCount = input.Read<int>();
				    var surfaceLoopAllInfos = new NativeArray<SurfaceInfo>(surfaceLoopCount, Allocator.Temp);
				    //NativeCollectionHelpers.EnsureMinimumSizeAndClear(ref surfaceLoopAllInfos, surfaceLoopCount);
				
                    var surfaceLoopAllEdges = new NativeList<UnsafeList<Edge>>(surfaceLoopCount, Allocator.Temp);
				    surfaceLoopAllEdges.Resize(surfaceLoopCount, NativeArrayOptions.ClearMemory);
			        //NativeCollectionHelpers.EnsureSizeAndClear(ref surfaceLoopAllEdges, surfaceLoopCount);
                    try
                    { 
                        for (int l = 0; l < surfaceLoopCount; l++)
                        {
                            surfaceLoopAllInfos[l] = input.Read<SurfaceInfo>();
                            var edgeCount   = input.Read<int>();
                            if (edgeCount > 0)
                            { 
                                var edgesInner  = new UnsafeList<Edge>(edgeCount, Allocator.Temp);
                                for (int e = 0; e < edgeCount; e++)
                                {
                                    edgesInner.AddNoResize(input.Read<Edge>());
                                }
                                surfaceLoopAllEdges[l] = edgesInner;
                            }
                        }
                        input.EndForEachIndex();



                        if (!basePolygonCache[brushNodeOrder].IsCreated)
                            return;

			            int instanceID = instanceIDLookup.SafeGetNodeInstanceID(brushIndexOrder.compactNodeID);

			            var maxLoops = 0;
                        var maxIndices = 0;
                        for (int s = 0; s < surfaceLoopIndices.Length; s++)
                        {
                            if (!surfaceLoopIndices[s].IsCreated)
                                continue;
                            var length = surfaceLoopIndices[s].Length;
                            maxIndices += length;
                            maxLoops = math.max(maxLoops, length);
                        }


                        ref var baseSurfaces            = ref basePolygonCache[brushNodeOrder].Value.surfaces;
                        var brushTransformations        = transformationCache[brushNodeOrder];
                        var treeToNode                  = brushTransformations.treeToNode;
                        var nodeToTreeInverseTransposed = math.transpose(treeToNode);
            
			            Vertex2DRemapper vertex2DRemapper = new()
			            {
				            lookup      = new NativeList<int>(64, Allocator.Temp),
				            positions2D = new NativeList<double2>(64, Allocator.Temp),
				            edgeIndices = new NativeList<int>(128, Allocator.Temp)
			            };
			            UniqueVertexMapper uniqueVertexMapper = new()
                        {
				            indexRemap = new NativeArray<int>(brushVertices.Length, Allocator.Temp),
                            surfaceColliderVertices = new NativeList<float3>(brushVertices.Length, Allocator.Temp),
				            surfaceSelectVertices = new NativeList<SelectVertex>(brushVertices.Length, Allocator.Temp),
				            surfaceRenderVertices = new NativeList<RenderVertex>(brushVertices.Length, Allocator.Temp)
                        };

                        Args settings = new(
                                autoHolesAndBoundary: true,
                                concentricShellsParameter: 0.001f,
                                preprocessor: andywiecko.BurstTriangulator.Preprocessor.None,
                                refineMesh: false,
                                restoreBoundary: true,
                                sloanMaxIters: 1_000_000,
                                validateInput: true,
                                verbose: true,
                                refinementThresholdAngle: math.radians(5),
                                refinementThresholdArea: 1f
				            );

			            var surfaceIndexList = new NativeList<int>(maxIndices, Allocator.Temp);
                        var loops = new NativeList<int>(maxLoops, Allocator.Temp);

			            //var outputPositions = new NativeList<double2>(64, Allocator.Temp);
			            var output = new andywiecko.BurstTriangulator.LowLevel.Unsafe.OutputData<double2>()
			            {
				            //Positions = outputPositions,
				            Triangles = new NativeList<int>(64, Allocator.Temp),
                            Status = new NativeReference<Status>(Allocator.Temp)
			            };
			            var triangulator = new UnsafeTriangulator<double2>();

			            var builder = new BlobBuilder(Allocator.Temp, 4096);
                        ref var root = ref builder.ConstructRoot<ChiselBrushRenderBuffer>();
                        var surfaceRenderBuffers = builder.Allocate(ref root.surfaces, surfaceLoopIndices.Length);


			            for (int surfaceIndex = 0; surfaceIndex < surfaceLoopIndices.Length; surfaceIndex++)
                        {
                            if (!surfaceLoopIndices[surfaceIndex].IsCreated)
                                continue;

                            loops.Clear();
                            uniqueVertexMapper.Reset();

				            var loopIndices = surfaceLoopIndices[surfaceIndex];
                            for (int l = 0; l < loopIndices.Length; l++)
                            {
                                var surfaceLoopIndex    = loopIndices[l];
                                var surfaceLoopEdges    = surfaceLoopAllEdges[surfaceLoopIndex];

                                // TODO: verify that this never happens, check should be in previous job
                                Debug.Assert(surfaceLoopEdges.Length >= 3);
                                if (surfaceLoopEdges.Length < 3)
                                    continue;

                                loops.Add(surfaceLoopIndex);
                            }

                            if (loops.Length == 0)
                                continue;
                
				            // We need to convert our UV matrix from tree-space, to brush local-space, to plane-space
                            // since the vertices of the polygons, at this point, are in tree-space.
                            var localSpacePlane         = baseSurfaces[surfaceIndex].localPlane;
				            var localSpaceToPlaneSpace  = MathExtensions.GenerateLocalToPlaneSpaceMatrix(localSpacePlane);
                            var treeSpaceToPlaneSpace   = math.mul(localSpaceToPlaneSpace, treeToNode);
                            var surfaceTreeSpacePlane   = math.mul(nodeToTreeInverseTransposed, localSpacePlane);
				            var map3DTo2D               = new Map3DTo2D(surfaceTreeSpacePlane.xyz);


				            surfaceIndexList.Clear();
                            for (int l = 0; l < loops.Length; l++)
                            {
                                var loopIndex   = loops[l];
                                var loopEdges   = surfaceLoopAllEdges[loopIndex];
                                var loopInfo    = surfaceLoopAllInfos[loopIndex];
					            Debug.Assert(surfaceIndex == loopInfo.basePlaneIndex, "surfaceIndex != loopInfo.basePlaneIndex");

					            #region Map 3D polygon to 2D surface
					            vertex2DRemapper.ConvertToPlaneSpace(*brushVertices.m_Vertices, loopEdges, map3DTo2D);
                                var readOnlyVertices = vertex2DRemapper.AsReadOnly();
					            #endregion

					            #region Triangulate
					            output.Triangles.Clear();
                                var input = new andywiecko.BurstTriangulator.LowLevel.Unsafe.InputData<double2>()
                                {
                                    Positions = readOnlyVertices.positions2D,
                                    ConstraintEdges = readOnlyVertices.edgeIndices
                                };
					            triangulator.Triangulate(input, output, settings, Allocator.Temp);
                                if (output.Status.Value != andywiecko.BurstTriangulator.Status.OK) { Debug.LogError($"{output.Status.Value}"); continue; }
                                if (output.Triangles.Length == 0) continue;
					            #endregion

					            #region Add triangles to surface
					            var prevSurfaceIndexCount = surfaceIndexList.Length;
					            CategoryIndex interiorCategory = (CategoryIndex)loopInfo.interiorCategory;
					            readOnlyVertices.RemapTriangles(interiorCategory, output.Triangles, surfaceIndexList);
                                uniqueVertexMapper.RegisterVertices(surfaceIndexList, prevSurfaceIndexCount, *brushVertices.m_Vertices, map3DTo2D.normal, instanceID, interiorCategory);
					            #endregion
				            }

				            if (surfaceIndexList.Length == 0)
                                continue;
				
                            var destinationFlags      = baseSurfaces[surfaceIndex].destinationFlags;
                            var destinationParameters = baseSurfaces[surfaceIndex].destinationParameters;
                            var UV0                   = baseSurfaces[surfaceIndex].UV0;

				            var uv0Matrix = math.mul(UV0.ToFloat4x4(), treeSpaceToPlaneSpace);
				            MeshAlgorithms.ComputeUVs(uniqueVertexMapper.surfaceRenderVertices, uv0Matrix);
				            MeshAlgorithms.ComputeTangents(surfaceIndexList, uniqueVertexMapper.surfaceRenderVertices);

				            ref var surfaceRenderBuffer = ref surfaceRenderBuffers[surfaceIndex];
                            surfaceRenderBuffer.Construct(builder, surfaceIndexList,
											              uniqueVertexMapper.surfaceColliderVertices,
											              uniqueVertexMapper.surfaceSelectVertices,
											              uniqueVertexMapper.surfaceRenderVertices, 
                                                          surfaceIndex, destinationFlags, destinationParameters);
			            }

			            //triangulator.Dispose();
			            output.Triangles.Dispose();
                        output.Status.Dispose();
			            uniqueVertexMapper.Dispose();
			            vertex2DRemapper.Dispose();
                        surfaceIndexList.Dispose();
                        loops.Dispose();

			            var querySurfaceList = new NativeList<ChiselQuerySurface>(surfaceRenderBuffers.Length, Allocator.Temp);			
                        var querySurfaces = builder.Allocate(ref root.querySurfaces, meshQueries.Length);
                        for (int t = 0; t < meshQueries.Length; t++)
                        {
                            var meshQuery       = meshQueries[t];
                            var layerQueryMask  = meshQuery.LayerQueryMask;
                            var layerQuery      = meshQuery.LayerQuery;
                            var surfaceParameterIndex = (meshQuery.LayerParameterIndex >= SurfaceParameterIndex.Parameter1 && 
                                                         meshQuery.LayerParameterIndex <= SurfaceParameterIndex.MaxParameterIndex) ?
                                                         (int)meshQuery.LayerParameterIndex - 1 : -1;

                            querySurfaceList.Clear();

                            for (int s = 0; s < surfaceRenderBuffers.Length; s++)
                            {
					            ref var renderBuffer = ref surfaceRenderBuffers[s];
					            var destinationFlags  = renderBuffer.destinationFlags;
                                if ((destinationFlags & layerQueryMask) != layerQuery)
                                    continue;

					            querySurfaceList.AddNoResize(new ChiselQuerySurface
                                {
                                    surfaceIndex     = renderBuffer.surfaceIndex,
                                    surfaceParameter = surfaceParameterIndex < 0 ? 0 : renderBuffer.destinationParameters.parameters[surfaceParameterIndex],
                                    vertexCount      = renderBuffer.vertexCount,
                                    indexCount       = renderBuffer.indexCount,
                                    surfaceHash      = renderBuffer.surfaceHash,
                                    geometryHash     = renderBuffer.geometryHash
                                });
                            }
                            querySurfaceList.Sort(kCompareSortByBasePlaneIndex);

                            builder.Construct(ref querySurfaces[t].surfaces, querySurfaceList);
                            querySurfaces[t].brushNodeID = brushIndexOrder.compactNodeID;
			            }

                        querySurfaceList.Dispose();


			            root.surfaceOffset = 0;
                        root.surfaceCount = surfaceRenderBuffers.Length;

                        var brushRenderBuffer = builder.CreateBlobAssetReference<ChiselBrushRenderBuffer>(Allocator.Persistent);
                        if (brushRenderBufferCache[brushNodeOrder].IsCreated)
                        {
                            brushRenderBufferCache[brushNodeOrder].Dispose();
                            brushRenderBufferCache[brushNodeOrder] = default;
                        }
                        brushRenderBufferCache[brushNodeOrder] = brushRenderBuffer;
                    }
                    finally
				    {
					    surfaceLoopAllInfos.Dispose();
					    surfaceLoopAllEdges.Dispose();
				    }
                }
                finally
                {
                    surfaceLoopIndices.Dispose();
				}
            }
            finally
            {
                brushVertices.Dispose();
            }
        }
    }
}
