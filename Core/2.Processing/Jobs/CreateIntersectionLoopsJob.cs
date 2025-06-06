using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Collections.LowLevel.Unsafe;
using System.Runtime.CompilerServices;
using System.Collections.Generic;
using Debug = UnityEngine.Debug;
using ReadOnlyAttribute = Unity.Collections.ReadOnlyAttribute;
using WriteOnlyAttribute = Unity.Collections.WriteOnlyAttribute;
using Unity.Entities;

namespace Chisel.Core
{
    [BurstCompile(CompileSynchronously = true)]
    struct CreateIntersectionLoopsJob : IJobParallelForDefer
    {
        const float kFatPlaneWidthEpsilon = CSGConstants.kFatPlaneWidthEpsilon;

        // Needed for count (forced & unused)
        [NoAlias, ReadOnly] public NativeList<BrushPair2> uniqueBrushPairs;

        // Read
        [NoAlias, ReadOnly] public NativeList<BlobAssetReference<BrushTreeSpacePlanes>>       brushTreeSpacePlaneCache;
        [NoAlias, ReadOnly] public NativeList<BlobAssetReference<BrushTreeSpaceVerticesBlob>> treeSpaceVerticesCache;
        [NoAlias, ReadOnly] public NativeStream.Reader                                  intersectingBrushesStream;

        // Write
        [NoAlias, WriteOnly] public NativeListExtensions.ParallelWriterExt<float3>      outputSurfaceVertices;
        [NoAlias, WriteOnly] public NativeList<BrushIntersectionLoop>.ParallelWriter    outputSurfaces;

        // Per thread scratch memory
        //[NativeDisableContainerSafetyRestriction] NativeArray<float4>                   localVertices;
        //[NativeDisableContainerSafetyRestriction] NativeArray<ushort>                   usedVertexIndices;
        //[NativeDisableContainerSafetyRestriction] NativeArray<PlaneIndexOffsetLength>   planeIndexOffsets;
        //[NativeDisableContainerSafetyRestriction] NativeList<ushort>                    uniqueIndices;
        //[NativeDisableContainerSafetyRestriction] NativeArray<int2>                     sortedStack;        
        //[NativeDisableContainerSafetyRestriction] NativeArray<PlaneVertexIndexPair>     foundIndices0;
        //[NativeDisableContainerSafetyRestriction] NativeArray<PlaneVertexIndexPair>     foundIndices1;
        //[NativeDisableContainerSafetyRestriction] NativeArray<float4>                   foundVertices;
        //[NativeDisableContainerSafetyRestriction] NativeArray<IntersectionEdge>         foundEdges;
        //[NativeDisableContainerSafetyRestriction] NativeArray<IntersectionPlanes>       foundIntersections;
        //[NativeDisableContainerSafetyRestriction] NativeArray<float3>                   temporaryVertices;
        //[NativeDisableContainerSafetyRestriction] HashedVertices                        hashedTreeSpaceVertices;
        //[NativeDisableContainerSafetyRestriction] HashedVertices                        snapHashedVertices;
                
        struct PlaneVertexIndexPair
        {
            public ushort planeIndex;
            public ushort vertexIndex;
        }

        struct PlaneIndexOffsetLength
        {
            public ushort length;
            public ushort offset;
            public ushort planeIndex;
        }
        
        static bool IsOutsidePlanes([NoAlias, ReadOnly] NativeArray<float4> planes, int planesLength, float4 localVertex)
        {
            int n = 0;
            for (; n + 4 < planesLength; n+=4)
            {
                var distance = new float4(math.dot(planes[n+0], localVertex),
                                          math.dot(planes[n+1], localVertex),
                                          math.dot(planes[n+2], localVertex),
                                          math.dot(planes[n+3], localVertex));

                // will be 'false' when distance is NaN or Infinity
                if (!math.all(distance <= kFatPlaneWidthEpsilon))
                    return true;
            }
            for (; n < planesLength; n ++)
            {
                var distance = math.dot(planes[n], localVertex);

                // will be 'false' when distance is NaN or Infinity
                if (!(distance <= kFatPlaneWidthEpsilon))
                    return true;
            }
            return false;
        }


        #region Sort
        static float3 FindPolygonCentroid([NoAlias, ReadOnly] HashedVertices vertices, [NoAlias, ReadOnly] NativeArray<ushort> indices, int offset, int indicesCount)
        {
            var centroid = float3.zero;
            for (int i = 0; i < indicesCount; i++, offset++)
                centroid += vertices[indices[offset]];
            return centroid / indicesCount;
        }

        // TODO: sort by using plane information instead of unreliable floating point math ..
        static void SortIndices([NoAlias, ReadOnly] HashedVertices vertices, [NoAlias, ReadOnly] NativeArray<int2> sortedStack, [NoAlias, ReadOnly] NativeArray<ushort> indices, int offset, int indicesCount, float3 normal)
        {
            // There's no point in trying to sort a point or a line 
            if (indicesCount < 3)
                return;

            float3 tangentX, tangentY;
            if (normal.x > normal.y)
            {
                if (normal.x > normal.z)
                {
                    tangentX = math.cross(normal, new float3(0, 1, 0));
                    tangentY = math.cross(normal, tangentX);
                } else
                {
                    tangentX = math.cross(normal, new float3(0, 0, 1));
                    tangentY = math.cross(normal, tangentX);
                }
            } else
            {
                if (normal.y > normal.z)
                {
                    tangentX = math.cross(normal, new float3(1, 0, 0));
                    tangentY = math.cross(normal, tangentX);
                } else
                {
                    tangentX = math.cross(normal, new float3(0, 1, 0));
                    tangentY = math.cross(normal, tangentX);
                }
            }

            var centroid = FindPolygonCentroid(vertices, indices, offset, indicesCount);
            var center = new float2(math.dot(tangentX, centroid), // distance in direction of tangentX
                                    math.dot(tangentY, centroid)); // distance in direction of tangentY


            var sortedStackLength = 1;
            sortedStack[0] = new int2(0, indicesCount - 1);
            while (sortedStackLength > 0)
            {
                var top = sortedStack[sortedStackLength - 1];
                sortedStackLength--;
                var l = top.x;
                var r = top.y;
                var left = l;
                var right = r;
                var va = vertices[indices[offset + (left + right) / 2]];
                while (true)
                {
                    var a_angle = math.atan2(math.dot(tangentX, va) - center.x, math.dot(tangentY, va) - center.y);

                    {
                        var vb = vertices[indices[offset + left]];
                        var b_angle = math.atan2(math.dot(tangentX, vb) - center.x, math.dot(tangentY, vb) - center.y);
                        while (b_angle > a_angle)
                        {
                            left++;
                            vb = vertices[indices[offset + left]];
                            b_angle = math.atan2(math.dot(tangentX, vb) - center.x, math.dot(tangentY, vb) - center.y);
                        }
                    }

                    {
                        var vb = vertices[indices[offset + right]];
                        var b_angle = math.atan2(math.dot(tangentX, vb) - center.x, math.dot(tangentY, vb) - center.y);
                        while (a_angle > b_angle)
                        {
                            right--;
                            vb = vertices[indices[offset + right]];
                            b_angle = math.atan2(math.dot(tangentX, vb) - center.x, math.dot(tangentY, vb) - center.y);
                        }
                    }

                    if (left <= right)
                    {
                        if (left != right)
                        {
                            var t = indices[offset + left];
                            indices[offset + left] = indices[offset + right];
                            indices[offset + right] = t;
                        }

                        left++;
                        right--;
                    }
                    if (left > right)
                        break;
                }
                if (l < right)
                {
                    sortedStack[sortedStackLength] = new int2(l, right);
                    sortedStackLength++;
                }
                if (left < r)
                {
                    sortedStack[sortedStackLength] = new int2(left, r);
                    sortedStackLength++;
                }
            }
        }
        #endregion
        
        //[MethodImpl(MethodImplOptions.NoInlining)]
        void FindInsideVertices([NoAlias, ReadOnly] NativeArray<float3> usedVertices0,
                                int                                     usedVertices0Length,
                                [NoAlias, ReadOnly] NativeArray<ushort> vertexIntersectionPlanes,
                                int                                     vertexIntersectionPlanesLength,
                                [NoAlias, ReadOnly] NativeArray<int2>   vertexIntersectionSegments,
                                int                                     vertexIntersectionSegmentsLength,
                                [NoAlias, ReadOnly] NativeArray<float4> intersectingPlanes1,
                                int                                     intersectingPlanes1Length,
                                int                                     intersectingPlanesAndEdges1Length,
                                float4x4                                nodeToTreeSpaceMatrix1,
                                float4x4                                vertexToLocal0,
                                [NoAlias] ref HashedVertices            hashedTreeSpaceVertices,
                                [NoAlias] ref HashedVertices            snapHashedVertices,
                                [NoAlias] NativeArray<PlaneVertexIndexPair> foundIndices0,
                                ref int                                     foundIndices0Length)
		{
            NativeArray<float4> localVertices;
			using var _localVertices = localVertices = new NativeArray<float4>(usedVertices0Length, Allocator.Temp);
            //NativeCollectionHelpers.EnsureMinimumSize(ref localVertices, usedVertices0Length);

            NativeArray<ushort> usedVertexIndices;
			using var _usedVertexIndices = usedVertexIndices = new NativeArray<ushort>(usedVertices0Length, Allocator.Temp);
			//NativeCollectionHelpers.EnsureMinimumSize(ref usedVertexIndices, usedVertices0Length);
            
            for (int j = 0; j < usedVertices0Length; j++)
            {
                var brushVertex1 = new float4(usedVertices0[j], 1);
                localVertices[j] = math.mul(vertexToLocal0, brushVertex1);
                usedVertexIndices[j] = (ushort)j;
            }

            var foundVertexCount = usedVertices0Length;
            for (int j = foundVertexCount - 1; j >= 0; j--)
            {
                if (IsOutsidePlanes(intersectingPlanes1, intersectingPlanesAndEdges1Length, localVertices[j]))
                {
                    if (j < foundVertexCount - 1)
                    {
                        localVertices[j] = localVertices[foundVertexCount - 1];
                        usedVertexIndices[j] = usedVertexIndices[foundVertexCount - 1];
                    }
                    foundVertexCount--;
                }
            }

            for (int j = 0; j < foundVertexCount; j++)
            {
                var usedVertexIndex = usedVertexIndices[j];
                var segment = vertexIntersectionSegments[usedVertexIndex];
                if (segment.y == 0)
                    continue;

                var treeSpaceVertex = math.mul(nodeToTreeSpaceMatrix1, localVertices[j]).xyz;
                treeSpaceVertex = snapHashedVertices[snapHashedVertices.AddNoResize(treeSpaceVertex)];
                var treeSpaceVertexIndex = hashedTreeSpaceVertices.AddNoResize(treeSpaceVertex);
                for (int i = segment.x; i < segment.x + segment.y; i++)
                {
                    var planeIndex = vertexIntersectionPlanes[i];

                    // TODO: optimize
                    for (int k = 0; k < foundIndices0Length; k++)
                    {
                        if (foundIndices0[k].planeIndex == planeIndex &&
                            foundIndices0[k].vertexIndex == treeSpaceVertexIndex)
                        {
                            goto skipMe;
                        }
                    }

                    foundIndices0[foundIndices0Length] = new PlaneVertexIndexPair { planeIndex = (ushort)planeIndex, vertexIndex = (ushort)treeSpaceVertexIndex };
                    foundIndices0Length++;
                skipMe:
                    ;
                }
            }
        }
        
        struct IntersectionPlanes
        {
            //public float4       plane0;
            //public float4       plane1;
            public float4       plane2;
            public int          planeIndex0;
            public int          planeIndex1;
            public int          planeIndex2;
        }

        struct IntersectionEdge
        {
            public float4       edgeVertex0;
            public float4       edgeVertex1;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        void FindIntersectionVertices(ref NativeArray<float4>           intersectingPlanes0,
                                      int                               intersectingPlanes0Length,
                                      int                               intersectingPlanesAndEdges0Length,
                                      ref NativeArray<float4>           intersectingPlanes1,
                                      int                               intersectingPlanes1Length,
                                      int                               intersectingPlanesAndEdges1Length,
                                      ref NativeArray<PlanePair>        usedPlanePairs1,
                                      int                               usedPlanePairs1Length,
                                      ref NativeArray<int>              intersectingPlaneIndices0,
                                      int                               intersectingPlaneIndices0Length,
                                      float4x4                          nodeToTreeSpaceMatrix0,
                                      ref HashedVertices                hashedTreeSpaceVertices,
                                      ref HashedVertices                snapHashedVertices,
                                      NativeArray<PlaneVertexIndexPair> foundIndices0,
                                      ref int                           foundIndices0Length,
                                      NativeArray<PlaneVertexIndexPair> foundIndices1,
                                      ref int                           foundIndices1Length)
        {
            int foundVerticesCount = usedPlanePairs1Length * intersectingPlanes0Length;
            NativeArray<float4> foundVertices;
			using var _foundVertices = foundVertices = new NativeArray<float4> (foundVerticesCount, Allocator.Temp);
            //NativeCollectionHelpers.EnsureMinimumSize(ref foundVertices, foundVerticesCount);
            
            NativeArray<IntersectionEdge> foundEdges;
			using var _foundEdges = foundEdges = new NativeArray<IntersectionEdge> (foundVerticesCount, Allocator.Temp);
            //NativeCollectionHelpers.EnsureMinimumSize(ref foundEdges, foundVerticesCount);

            NativeArray<IntersectionPlanes> foundIntersections;
			using var _foundIntersections = foundIntersections = new NativeArray<IntersectionPlanes> (foundVerticesCount, Allocator.Temp);
			//NativeCollectionHelpers.EnsureMinimumSize(ref foundIntersections, foundVerticesCount);
            
            var n = 0;
            for (int i = 0; i < usedPlanePairs1Length; i++)
            {
                for (int j = 0; j < intersectingPlanes0Length; j++)
                { 
                    var plane0 = usedPlanePairs1[i].plane0;
                    var plane1 = usedPlanePairs1[i].plane1;
                    var plane2 = intersectingPlanes0[j];

                    foundIntersections[n] = new IntersectionPlanes
                    {
                        //plane0    = plane0,
                        //plane1    = plane1,
                        plane2      = plane2,
                        planeIndex0 = usedPlanePairs1[i].planeIndex0,
                        planeIndex1 = usedPlanePairs1[i].planeIndex1,
                        planeIndex2 = intersectingPlaneIndices0[j]
                    };

                    foundEdges[n] = new IntersectionEdge
                    {
                        edgeVertex0 = usedPlanePairs1[i].edgeVertex0,
                        edgeVertex1 = usedPlanePairs1[i].edgeVertex1
                    };

                    if (math.abs(math.dot(plane2.xyz, plane0.xyz)) >= CSGConstants.kNormalDotAlignEpsilon ||
                        math.abs(math.dot(plane2.xyz, plane1.xyz)) >= CSGConstants.kNormalDotAlignEpsilon ||
                        math.abs(math.dot(plane0.xyz, plane1.xyz)) >= CSGConstants.kNormalDotAlignEpsilon)
                        continue;

                    var localVertex = PlaneExtensions.Intersection(plane2, plane0, plane1);
                    if (double.IsNaN(localVertex.x))
                        continue;

                    foundVertices[n] = new float4((float3)localVertex,1);
                    n++;
                }
            }

            for (int k = n - 1; k >= 0; k--)
            {
                var edgeVertex0 = foundEdges[k].edgeVertex0;
                var edgeVertex1 = foundEdges[k].edgeVertex1;
                var plane2      = foundIntersections[k].plane2;

                if (math.abs(math.dot(plane2, edgeVertex0)) <= kFatPlaneWidthEpsilon &&
                    math.abs(math.dot(plane2, edgeVertex1)) <= kFatPlaneWidthEpsilon)
                {
                    if (k < n - 1)
                    {
                        foundIntersections[k] = foundIntersections[n - 1];
                        foundVertices[k] = foundVertices[n - 1];
                    }
                    n--;
                }
            }

            // TODO: since we're using a pair in the outer loop, we could also determine which 
            //       2 planes it intersects at both ends and just check those two planes ..

            // NOTE: for brush2, the intersection will always be only on two planes
            //       UNLESS it's a corner vertex along that edge (we can compare to the two vertices)
            //       in which case we could use a pre-calculated list of planes ..
            //       OR when the intersection is outside of the edge ..

            for (int k = n - 1; k >= 0; k--)
            {
                if (IsOutsidePlanes(intersectingPlanes0, intersectingPlanesAndEdges0Length, foundVertices[k]) ||
                    IsOutsidePlanes(intersectingPlanes1, intersectingPlanesAndEdges1Length, foundVertices[k]))
                {
                    if (k < n - 1)
                    {
                        foundIntersections[k] = foundIntersections[n - 1];
                        foundVertices[k] = foundVertices[n - 1];
                    }
                    n--;
                }
            }

            for (int k = 0; k < n; k++)
            {
                var planeIndex0 = (ushort)foundIntersections[k].planeIndex0;
                var planeIndex1 = (ushort)foundIntersections[k].planeIndex1;
                var planeIndex2 = (ushort)foundIntersections[k].planeIndex2;

                var localVertex = foundVertices[k];

                // TODO: should be having a Loop for each plane that intersects this vertex, and add that vertex 
                //       to ensure they are identical
                var treeSpaceVertex         = math.mul(nodeToTreeSpaceMatrix0, localVertex).xyz;
                treeSpaceVertex             = snapHashedVertices[snapHashedVertices.AddNoResize(treeSpaceVertex)];
                var treeSpaceVertexIndex    = hashedTreeSpaceVertices.AddNoResize(treeSpaceVertex);

                { 
                    // TODO: optimize
                    for (int f = 0; f < foundIndices0Length; f++)
                    {
                        if (foundIndices0[f].vertexIndex == treeSpaceVertexIndex &&
                            foundIndices0[f].planeIndex == planeIndex2)
                        {
                            goto skip0;
                        }
                    }

                    foundIndices0[foundIndices0Length] = new PlaneVertexIndexPair { planeIndex = planeIndex2, vertexIndex = treeSpaceVertexIndex };
                    foundIndices0Length++;
                    skip0: ;
                }

                {
                    // TODO: optimize
                    for (int f = 0; f < foundIndices1Length; f++)
                    {
                        if (foundIndices1[f].vertexIndex == treeSpaceVertexIndex &&
                            foundIndices1[f].planeIndex == planeIndex0)
                        {
                            goto skip1;
                        }
                    }

                    foundIndices1[foundIndices1Length] = new PlaneVertexIndexPair { planeIndex = planeIndex0, vertexIndex = treeSpaceVertexIndex };
                    foundIndices1Length++;
                    skip1: ;
                }

                {
                    // TODO: optimize
                    for (int f = 0; f < foundIndices1Length; f++)
                    {
                        if (foundIndices1[f].vertexIndex == treeSpaceVertexIndex &&
                            foundIndices1[f].planeIndex == planeIndex1)
                        {
                            goto skip2;
                        }
                    }

                    foundIndices1[foundIndices1Length] = new PlaneVertexIndexPair { planeIndex = planeIndex1, vertexIndex = treeSpaceVertexIndex };
                    foundIndices1Length++;
                    skip2: ;
                }
            }
        }
		/*
        struct Comparer : IComparer<PlaneVertexIndexPair>
        {
            public int Compare(PlaneVertexIndexPair x, PlaneVertexIndexPair y)
            {
                if (x.planeIndex > y.planeIndex)
                    continue;
                if (x.planeIndex == y.planeIndex)
                {
                    if (x.vertexIndex <= y.vertexIndex)
                        continue;
                }

                var t = x;
                foundIndices0[i] = foundIndices0[j];
                foundIndices0[j] = t;
            }
        }
        readonly static readonly Comparer kComparer = new Comparer();
        */
		[MethodImpl(MethodImplOptions.NoInlining)]
        void GenerateLoop(IndexOrder brushIndexOrder0,
                          IndexOrder brushIndexOrder1,
                          bool       invertedTransform,
                          [NoAlias, ReadOnly] NativeArray<SurfaceInfo>          surfaceInfos,
                          [NoAlias] int                                         surfaceInfosLength,
                          [NoAlias, ReadOnly] ref BrushTreeSpacePlanes          brushTreeSpacePlanes0,
                          [NoAlias, ReadOnly] NativeArray<PlaneVertexIndexPair> foundIndices0,
                          [NoAlias] ref int                                     foundIndices0Length,
                          [NoAlias] ref HashedVertices                          hashedTreeSpaceVertices,
                          [NoAlias] NativeList<BrushIntersectionLoop>.ParallelWriter outputSurfaces)
        {
            // Why is the unity NativeSort slower than bubble sort?
            // TODO: revisit this assumption
            //*
            for (int i = 0; i < foundIndices0Length - 1; i++)
            {
                for (int j = i + 1; j < foundIndices0Length; j++)
                {
                    var x = foundIndices0[i];
                    var y = foundIndices0[j];
                    if (x.planeIndex > y.planeIndex)
                        continue;
                    if (x.planeIndex == y.planeIndex)
                    {
                        if (x.vertexIndex <= y.vertexIndex)
                            continue;
                    }

                    var t = x;
                    foundIndices0[i] = foundIndices0[j];
                    foundIndices0[j] = t;
                }
            }
            /*/
            foundIndices0.Sort(kComparer);
            //*/

            NativeArray<PlaneIndexOffsetLength> planeIndexOffsets;
			using var _planeIndexOffsets = planeIndexOffsets = new NativeArray<PlaneIndexOffsetLength>(foundIndices0Length, Allocator.Temp);
            //NativeCollectionHelpers.EnsureMinimumSize(ref planeIndexOffsets, foundIndices0Length);
            
            NativeList<ushort> uniqueIndices;
			using var _uniqueIndices = uniqueIndices = new NativeList<ushort>(foundIndices0Length, Allocator.Temp);
			//NativeCollectionHelpers.EnsureCapacityAndClear(ref uniqueIndices, foundIndices0Length);

            var planeIndexOffsetsLength = 0;
            //var planeIndexOffsets       = stackalloc PlaneIndexOffsetLength[foundIndices0Length];
            //var uniqueIndices           = stackalloc ushort[foundIndices0Length];

            // Now that our indices are sorted by planeIndex, we can segment them by start/end offset
            var previousPlaneIndex  = foundIndices0[0].planeIndex;
            var previousVertexIndex = foundIndices0[0].vertexIndex;
            uniqueIndices.Add(previousVertexIndex);
            var loopStart = 0;
            for (int i = 1; i < foundIndices0Length; i++)
            {
                var indices     = foundIndices0[i];

                var planeIndex  = indices.planeIndex;
                var vertexIndex = indices.vertexIndex;

                // TODO: why do we have soooo many duplicates sometimes?
                if (planeIndex  == previousPlaneIndex &&
                    vertexIndex == previousVertexIndex)
                    continue;

                if (planeIndex != previousPlaneIndex)
                {
                    var currLength = RemoveDuplicateEdges(ref uniqueIndices, loopStart, uniqueIndices.Length);
                    //var currLength = (uniqueIndices.Length - loopStart);
                    if (currLength > 2)
                    {
                        planeIndexOffsets[planeIndexOffsetsLength] = new PlaneIndexOffsetLength
                        {
                            length = (ushort)currLength,
                            offset = (ushort)loopStart,
                            planeIndex = previousPlaneIndex
                        };
                        planeIndexOffsetsLength++;
                    }
                    loopStart = uniqueIndices.Length;
                }

                uniqueIndices.Add(vertexIndex);
                previousVertexIndex = vertexIndex;
                previousPlaneIndex = planeIndex;
            }
            {
                var currLength = RemoveDuplicateEdges(ref uniqueIndices, loopStart, uniqueIndices.Length);
                //var currLength = (uniqueIndices.Length - loopStart);
                if (currLength > 2)
                {
                    planeIndexOffsets[planeIndexOffsetsLength] = new PlaneIndexOffsetLength
                    {
                        length = (ushort)currLength,
                        offset = (ushort)loopStart,
                        planeIndex = previousPlaneIndex
                    };
                    planeIndexOffsetsLength++;
                }
            }

            var maxLength = 0;
            for (int i = 0; i < planeIndexOffsetsLength; i++)
                maxLength = math.max(maxLength, planeIndexOffsets[i].length);

			var uniqueIndicesArray = uniqueIndices.AsArray();

			using var sortedStack = new NativeArray<int2>(maxLength * 2, Allocator.Temp);
			//NativeCollectionHelpers.EnsureMinimumSize(ref sortedStack, maxLength * 2);

            // For each segment, we now sort our vertices within each segment, 
            // making the assumption that they are convex
            //var sortedStack = stackalloc int2[maxLength * 2];
            ref var vertices = ref hashedTreeSpaceVertices;//.GetUnsafeReadOnlyPtr();
            for (int n = planeIndexOffsetsLength - 1; n >= 0; n--)
            {
                var planeIndexOffset    = planeIndexOffsets[n];
                var length              = planeIndexOffset.length;
                var offset              = planeIndexOffset.offset;
                var planeIndex          = planeIndexOffset.planeIndex;

                float3 normal = brushTreeSpacePlanes0.treeSpacePlanes[planeIndex].xyz * (invertedTransform ? 1 : -1);

                // TODO: use plane information instead
                SortIndices(vertices, sortedStack, uniqueIndicesArray, offset, length, normal);
            }

            
            var totalLoopsSize = 16 + (planeIndexOffsetsLength * UnsafeUtility.SizeOf<BrushIntersectionLoop>());
            var totalSize      = totalLoopsSize;
            for (int j = 0; j < planeIndexOffsetsLength; j++)
            {
                var planeIndexLength = planeIndexOffsets[j];
                var loopLength = planeIndexLength.length;
                totalSize += (loopLength * UnsafeUtility.SizeOf<float3>()); 
            }

            ref var srcVertices = ref hashedTreeSpaceVertices;
            for (int j = 0; j < planeIndexOffsetsLength; j++)
            { 
                var planeIndexLength = planeIndexOffsets[j];
                var offset           = planeIndexLength.offset;
                var loopLength       = planeIndexLength.length;
                var basePlaneIndex   = planeIndexLength.planeIndex;
                var surfaceInfo      = surfaceInfos[basePlaneIndex];

                NativeArray<float3> temporaryVertices;
				using var _temporaryVertices = temporaryVertices = new NativeArray<float3>(loopLength, Allocator.Temp);
			    //NativeCollectionHelpers.EnsureMinimumSize(ref temporaryVertices, loopLength);
                
                for (int d = 0; d < loopLength; d++)
                    temporaryVertices[d] = srcVertices[uniqueIndicesArray[offset + d]];

                var loopVertexIndex = outputSurfaceVertices.AddRangeNoResize(temporaryVertices, loopLength);

                outputSurfaces.AddNoResize(new BrushIntersectionLoop
                {
                    indexOrder0 = brushIndexOrder0,
                    indexOrder1 = brushIndexOrder1,
                    surfaceInfo = surfaceInfo,
                    loopVertexIndex = loopVertexIndex,
                    loopVertexCount = loopLength
                });
            }
        }

        // Handles situation where a vertex leads back to itself, could potentially happen when planes graze edges
        private readonly int RemoveDuplicateEdges(ref NativeList<ushort> uniqueIndices, int start, int end)
        {
            int length = end - start;
            if (length <= 2)
                return 0;

            // UNTESTED
            for (int indexA = end - 1, indexB = start, indexC = start + 1; indexA >= start; )
            {
                var vertexIndexA = uniqueIndices[indexA];
                //var vertexIndexB = uniqueIndices[indexB];
                var vertexIndexC = uniqueIndices[indexC];

                //    B
                //    *
                //    |
                //  A * C

                if (vertexIndexA != vertexIndexC)
                {
                    indexC = indexB;
                    indexB = indexA;
                    indexA --;
                    continue;
                }

                // TODO: test this
                Debug.Log("Untested condition occured, please share scene");
                length -=2;
                if (length <= 2)
                    return 0;

                if (indexC > end - 2)
                {
                    // 0 1 2 3 5 6 1
                    // B C         A 
                    uniqueIndices.MemMove(start, indexC + 1, end - (indexC + 1));
                    indexA -= 2;
                    end -= 2;
                } else
                if (indexC < end - 2)
                {
                    // 0 1 2 3 4 5 6
                    //   A B C
                    uniqueIndices.MemMove(indexA, indexC + 1, end - (indexC + 1));
                    end -= 2;
                } else
                if (indexC == end - 2)
                {
                    // 0 1 2 3 4 5 6
                    //         A B C
                    end -= 2;
                }

                indexB = indexA + 1; if (indexB >= end) indexB = start;
                indexC = indexB + 1; if (indexC >= end) indexC = start;
            }
            return length;
        }

        // Note: Temporary BlobAssetReference that only exists during a single frame
        public struct BrushPairIntersection
        {
            public IntersectionType         type;
            // Note: that the localSpacePlanes0/localSpacePlaneIndices0 parameters for both brush0 and brush1 are in localspace of >brush0<
            public BrushIntersectionInfo    brush0;
            public BrushIntersectionInfo    brush1;
        }

        public struct BrushIntersectionInfo
        {
            public IndexOrder               brushIndexOrder;

            [NativeDisableContainerSafetyRestriction] 
            public NativeArray<float3>      usedVertices;
            public int                      usedVerticesLength;
            
            [NativeDisableContainerSafetyRestriction] 
            public NativeArray<PlanePair>   usedPlanePairs;
            public int                      usedPlanePairsLength;
            
            [NativeDisableContainerSafetyRestriction] 
            public NativeArray<float4>      localSpacePlanes0;         // planes in local space of >brush0<
            public int                      localSpacePlanes0Length;
            public int                      localSpacePlanesAndEdges0Length;
            
            [NativeDisableContainerSafetyRestriction] 
            public NativeArray<int>         localSpacePlaneIndices0;   // planes indices of >brush0<
            public int                      localSpacePlaneIndices0Length;

            public float4x4                 nodeToTreeSpace;
            public float4x4                 toOtherBrushSpace;

            [NativeDisableContainerSafetyRestriction] 
            public NativeArray<ushort>      vertexIntersectionPlanes;
            public int                      vertexIntersectionPlanesLength;
            
            [NativeDisableContainerSafetyRestriction] 
            public NativeArray<int2>        vertexIntersectionSegments;
            public int                      vertexIntersectionSegmentsLength;

            [NativeDisableContainerSafetyRestriction]
            public NativeArray<SurfaceInfo> surfaceInfos;
            public int                      surfaceInfosLength;
        }

        [NativeDisableContainerSafetyRestriction] 
        BrushPairIntersection intersection;

        public void Execute(int index)
        {
            intersectingBrushesStream.BeginForEachIndex(index);

			intersectingBrushesStream.Read(out this.intersection.type);
            if (this.intersection.type == IntersectionType.InvalidValue)
                return;

			intersectingBrushesStream.Read(out this.intersection.brush0.brushIndexOrder);
			intersectingBrushesStream.Read(out this.intersection.brush0.nodeToTreeSpace);
			intersectingBrushesStream.Read(out this.intersection.brush0.toOtherBrushSpace);

			intersectingBrushesStream.ReadArrayAndEnsureSize(ref this.intersection.brush0.usedVertices, out this.intersection.brush0.usedVerticesLength);
			intersectingBrushesStream.ReadArrayAndEnsureSize(ref this.intersection.brush0.usedPlanePairs, out this.intersection.brush0.usedPlanePairsLength);
			intersectingBrushesStream.ReadArrayAndEnsureSize(ref this.intersection.brush0.localSpacePlanes0, out this.intersection.brush0.localSpacePlanesAndEdges0Length);

			intersectingBrushesStream.Read(out this.intersection.brush0.localSpacePlanes0Length);

			intersectingBrushesStream.ReadArrayAndEnsureSize(ref this.intersection.brush0.localSpacePlaneIndices0, out this.intersection.brush0.localSpacePlaneIndices0Length);
			intersectingBrushesStream.ReadArrayAndEnsureSize(ref this.intersection.brush0.vertexIntersectionPlanes, out this.intersection.brush0.vertexIntersectionPlanesLength);
			intersectingBrushesStream.ReadArrayAndEnsureSize(ref this.intersection.brush0.vertexIntersectionSegments, out this.intersection.brush0.vertexIntersectionSegmentsLength);
			intersectingBrushesStream.ReadArrayAndEnsureSize(ref this.intersection.brush0.surfaceInfos, out this.intersection.brush0.surfaceInfosLength);

			intersectingBrushesStream.Read(out this.intersection.brush1.brushIndexOrder);
			intersectingBrushesStream.Read(out this.intersection.brush1.nodeToTreeSpace);
			intersectingBrushesStream.Read(out this.intersection.brush1.toOtherBrushSpace);

			intersectingBrushesStream.ReadArrayAndEnsureSize(ref this.intersection.brush1.usedVertices, out this.intersection.brush1.usedVerticesLength);
			intersectingBrushesStream.ReadArrayAndEnsureSize(ref this.intersection.brush1.usedPlanePairs, out this.intersection.brush1.usedPlanePairsLength);
			intersectingBrushesStream.ReadArrayAndEnsureSize(ref this.intersection.brush1.localSpacePlanes0, out this.intersection.brush1.localSpacePlanesAndEdges0Length);

			intersectingBrushesStream.Read(out this.intersection.brush1.localSpacePlanes0Length);

			intersectingBrushesStream.ReadArrayAndEnsureSize(ref this.intersection.brush1.localSpacePlaneIndices0, out this.intersection.brush1.localSpacePlaneIndices0Length);
			intersectingBrushesStream.ReadArrayAndEnsureSize(ref this.intersection.brush1.vertexIntersectionPlanes, out this.intersection.brush1.vertexIntersectionPlanesLength);
			intersectingBrushesStream.ReadArrayAndEnsureSize(ref this.intersection.brush1.vertexIntersectionSegments, out this.intersection.brush1.vertexIntersectionSegmentsLength);
			intersectingBrushesStream.ReadArrayAndEnsureSize(ref this.intersection.brush1.surfaceInfos, out this.intersection.brush1.surfaceInfosLength);
            intersectingBrushesStream.EndForEachIndex();


            ref var brushPairIntersection0      = ref intersection.brush0;
            ref var brushPairIntersection1      = ref intersection.brush1;
            var brushIndexOrder0                = brushPairIntersection0.brushIndexOrder;
            var brushIndexOrder1                = brushPairIntersection1.brushIndexOrder;

            UnityEngine.Debug.Assert(brushPairIntersection0.brushIndexOrder.compactNodeID == brushIndexOrder0.compactNodeID);
            UnityEngine.Debug.Assert(brushPairIntersection1.brushIndexOrder.compactNodeID == brushIndexOrder1.compactNodeID);

            int insideVerticesStream0Capacity   = math.max(1, brushPairIntersection0.usedVerticesLength);
            int insideVerticesStream1Capacity   = math.max(1, brushPairIntersection1.usedVerticesLength);
            int intersectionStream0Capacity     = math.max(1, brushPairIntersection1.usedPlanePairsLength) * brushPairIntersection0.localSpacePlanes0Length;
            int intersectionStream1Capacity     = math.max(1, brushPairIntersection0.usedPlanePairsLength) * brushPairIntersection1.localSpacePlanes0Length;
            int foundIndices0Capacity           = intersectionStream0Capacity + (2 * intersectionStream1Capacity) + (brushPairIntersection0.localSpacePlanes0Length * insideVerticesStream0Capacity);
            int foundIndices1Capacity           = intersectionStream1Capacity + (2 * intersectionStream0Capacity) + (brushPairIntersection1.localSpacePlanes0Length * insideVerticesStream1Capacity);

            var foundIndices0Length     = 0;
            var foundIndices1Length     = 0;

			var desiredVertexCapacity = math.max(foundIndices0Capacity, foundIndices1Capacity);

			NativeArray<PlaneVertexIndexPair> foundIndices0;
			using var _foundIndices0 = foundIndices0 = new NativeArray<PlaneVertexIndexPair>(foundIndices0Capacity, Allocator.Temp);
            //NativeCollectionHelpers.EnsureMinimumSize(ref foundIndices0, foundIndices0Capacity);

            NativeArray<PlaneVertexIndexPair> foundIndices1;
			using var _foundIndices1 = foundIndices1 = new NativeArray<PlaneVertexIndexPair>(foundIndices1Capacity, Allocator.Temp);
            //NativeCollectionHelpers.EnsureMinimumSize(ref foundIndices1, foundIndices1Capacity);

            HashedVertices hashedTreeSpaceVertices;
            using var _hashedTreeSpaceVertices = hashedTreeSpaceVertices = new HashedVertices(desiredVertexCapacity, Allocator.Temp);
			//NativeCollectionHelpers.EnsureCapacityAndClear(ref hashedTreeSpaceVertices, desiredVertexCapacity);

			HashedVertices snapHashedVertices;
			using var _snapHashedVertices = snapHashedVertices = new HashedVertices(desiredVertexCapacity, Allocator.Temp);
            //NativeCollectionHelpers.EnsureCapacityAndClear(ref snapHashedVertices, desiredVertexCapacity);


            // TODO: fill them with original brush vertices so that they're always snapped to these
            
            if (brushIndexOrder0.nodeOrder < brushIndexOrder1.nodeOrder)
            {
                snapHashedVertices.AddUniqueVertices(ref treeSpaceVerticesCache[brushIndexOrder0.nodeOrder].Value.treeSpaceVertices);
                snapHashedVertices.ReplaceIfExists(ref treeSpaceVerticesCache[brushIndexOrder1.nodeOrder].Value.treeSpaceVertices);
            } else
            {
                snapHashedVertices.AddUniqueVertices(ref treeSpaceVerticesCache[brushIndexOrder1.nodeOrder].Value.treeSpaceVertices);
                snapHashedVertices.ReplaceIfExists(ref treeSpaceVerticesCache[brushIndexOrder0.nodeOrder].Value.treeSpaceVertices);
            }



            // First find vertices from other brush that are inside the other brush, so that any vertex we 
            // find during the intersection part will be snapped to those vertices and not the other way around

            // TODO: when all vertices of a polygon are inside the other brush, don't bother intersecting it.
            //       same when two planes overlap each other ...

            // Now find all the intersection vertices
            if (intersection.type == IntersectionType.Intersection)
            { 
                if (brushPairIntersection1.usedPlanePairsLength > 0)
                {
                    FindIntersectionVertices(ref brushPairIntersection0.localSpacePlanes0,
                                                    brushPairIntersection0.localSpacePlanes0Length,
                                                    brushPairIntersection0.localSpacePlanesAndEdges0Length,
                                                ref brushPairIntersection1.localSpacePlanes0,
                                                    brushPairIntersection1.localSpacePlanes0Length,
                                                    brushPairIntersection1.localSpacePlanesAndEdges0Length,
                                                ref brushPairIntersection1.usedPlanePairs,
                                                    brushPairIntersection1.usedPlanePairsLength,
                                                ref brushPairIntersection0.localSpacePlaneIndices0,
                                                    brushPairIntersection0.localSpacePlaneIndices0Length,
                                                brushPairIntersection0.nodeToTreeSpace,
                                                ref hashedTreeSpaceVertices,
                                                ref snapHashedVertices,
                                                foundIndices0, ref foundIndices0Length,
                                                foundIndices1, ref foundIndices1Length);
                }

                if (brushPairIntersection0.usedPlanePairsLength > 0)
                {
                    FindIntersectionVertices(ref brushPairIntersection1.localSpacePlanes0,
                                                    brushPairIntersection1.localSpacePlanes0Length,
                                                    brushPairIntersection1.localSpacePlanesAndEdges0Length,
                                                ref brushPairIntersection0.localSpacePlanes0,
                                                    brushPairIntersection0.localSpacePlanes0Length,
                                                    brushPairIntersection0.localSpacePlanesAndEdges0Length,
                                                ref brushPairIntersection0.usedPlanePairs,
                                                    brushPairIntersection0.usedPlanePairsLength,
                                                ref brushPairIntersection1.localSpacePlaneIndices0,
                                                    brushPairIntersection1.localSpacePlaneIndices0Length,
                                                brushPairIntersection0.nodeToTreeSpace,
                                                ref hashedTreeSpaceVertices,
                                                ref snapHashedVertices,
                                                foundIndices1, ref foundIndices1Length,
                                                foundIndices0, ref foundIndices0Length);
                }
            }

            // Find all vertices of brush0 that are inside brush1, and put their intersections into the appropriate loops
            if (foundIndices0Length > 0 &&
                brushPairIntersection0.usedVerticesLength > 0)
            {
                FindInsideVertices(brushPairIntersection0.usedVertices,
                                    brushPairIntersection0.usedVerticesLength,
                                    brushPairIntersection0.vertexIntersectionPlanes,
                                    brushPairIntersection0.vertexIntersectionPlanesLength,
                                    brushPairIntersection0.vertexIntersectionSegments,
                                    brushPairIntersection0.vertexIntersectionSegmentsLength,
                                    brushPairIntersection1.localSpacePlanes0,
                                    brushPairIntersection1.localSpacePlanes0Length,
                                    brushPairIntersection1.localSpacePlanesAndEdges0Length,
                                    brushPairIntersection0.nodeToTreeSpace,
                                    float4x4.identity,
                                    ref hashedTreeSpaceVertices,
                                    ref snapHashedVertices,
                                    foundIndices0, ref foundIndices0Length);
            }

            // Find all vertices of brush1 that are inside brush0, and put their intersections into the appropriate loops
            if (foundIndices1Length > 0 && 
                brushPairIntersection1.usedVerticesLength > 0)
            {
                FindInsideVertices(brushPairIntersection1.usedVertices,
                                    brushPairIntersection1.usedVerticesLength,
                                    brushPairIntersection1.vertexIntersectionPlanes,
                                    brushPairIntersection1.vertexIntersectionPlanesLength,
                                    brushPairIntersection1.vertexIntersectionSegments,
                                    brushPairIntersection1.vertexIntersectionSegmentsLength,
                                    brushPairIntersection0.localSpacePlanes0,
                                    brushPairIntersection0.localSpacePlanes0Length,
                                    brushPairIntersection0.localSpacePlanesAndEdges0Length,
                                    brushPairIntersection0.nodeToTreeSpace,
                                    brushPairIntersection1.toOtherBrushSpace,
                                    ref hashedTreeSpaceVertices,
                                    ref snapHashedVertices,
                                    foundIndices1, ref foundIndices1Length);
            }


            ref var brushTreeSpacePlanes0 = ref brushTreeSpacePlaneCache[brushIndexOrder0.nodeOrder].Value;
            ref var brushTreeSpacePlanes1 = ref brushTreeSpacePlaneCache[brushIndexOrder1.nodeOrder].Value;


            if (foundIndices0Length >= 3)
            {
                if (brushTreeSpacePlaneCache[brushIndexOrder0.nodeOrder].IsCreated)
                {
                    var brushTransformations0 = brushPairIntersection0.nodeToTreeSpace;
                    var invertedTransform = math.determinant(brushTransformations0) < 0;

                    GenerateLoop(brushIndexOrder0,
                                    brushIndexOrder1,
                                    invertedTransform,
                                    brushPairIntersection0.surfaceInfos,
                                    brushPairIntersection0.surfaceInfosLength,
                                    ref brushTreeSpacePlanes0,
                                    foundIndices0, ref foundIndices0Length,
                                    ref hashedTreeSpaceVertices,
                                    outputSurfaces);
                } else
                {
                    UnityEngine.Debug.LogError($"brushTreeSpacePlaneCache not initialized for brush with index {brushIndexOrder0.compactNodeID}");
                }
            }

            if (foundIndices1Length >= 3)
            {
                if (brushTreeSpacePlaneCache[brushIndexOrder1.nodeOrder].IsCreated)
                {
                    var brushTransformations1 = brushPairIntersection1.nodeToTreeSpace;
                    var invertedTransform = math.determinant(brushTransformations1) < 0;

                    GenerateLoop(brushIndexOrder1,
                                    brushIndexOrder0,
                                    invertedTransform,
                                    brushPairIntersection1.surfaceInfos,
                                    brushPairIntersection1.surfaceInfosLength,
                                    ref brushTreeSpacePlanes1,
                                    foundIndices1, 
                                    ref foundIndices1Length,
                                    ref hashedTreeSpaceVertices,
                                    outputSurfaces);
                } else
                {
                    UnityEngine.Debug.LogError($"brushTreeSpacePlaneCache not initialized for brush with index {brushIndexOrder1.compactNodeID}");
                }
            }
		}
    }
}
