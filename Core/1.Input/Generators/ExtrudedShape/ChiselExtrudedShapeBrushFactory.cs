using System.Collections.Generic;
using Unity.Mathematics;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;
using Unity.Entities;

namespace Chisel.Core
{
    public sealed partial class BrushMeshFactory
    {
        static BlobBuilderArray<float3> GetExtrudedVertices(UnsafeList<SegmentVertex> shapeVertices, Range range, Matrix4x4 matrix0, Matrix4x4 matrix1, in BlobBuilder builder, ref BrushMeshBlob root, out NativeArray<int> segmentIndices, Allocator allocator)
        {
            const int pathSegments = 2;
            var rangeLength = range.Length;
            var vertexCount = rangeLength * pathSegments;

            segmentIndices = default;
            if (range.Length <= 0)
            {
                Debug.LogError($"range.Length <= 0");
                return default;
            }

            if (range.start < 0)
            {
                Debug.LogError($"range.start < 0");
                return default;
            }

            if (range.end > shapeVertices.Length)
            {
                Debug.LogError($"range.end {range.end} > shapeVertices.length {shapeVertices.Length}");
                return default;
            }


            var localVertices = builder.Allocate(ref root.localVertices, vertexCount);
            segmentIndices = new NativeArray<int>(vertexCount, allocator);

            for (int s = range.start, v = 0; s < range.end; s++, v++)
            {
                Debug.Assert(s < shapeVertices.Length);
                var srcPoint   = shapeVertices[s].position;
                var srcSegment = shapeVertices[s].segmentIndex;
                var srcPoint3  = new float3(srcPoint.x, srcPoint.y, 0);
                localVertices[              v] = matrix0.MultiplyPoint(srcPoint3);
                localVertices[rangeLength + v] = matrix1.MultiplyPoint(srcPoint3);
                segmentIndices[              v] = srcSegment + 2;
                segmentIndices[rangeLength + v] = srcSegment + 2;
                Debug.Assert(rangeLength + v < vertexCount);
            }
            return localVertices;
        }

        // The faces of a side from a corner of the shape that is smooth (ChiselCurve2DBlob.Point.smoothSide), and not a cut
        // between pieces, are put in the smoothing group
        static void SmoothSides(ref BlobBuilderArray<BrushMeshBlob.Polygon> polygons, in BlobBuilderArray<BrushMeshBlob.HalfEdge> halfEdges,
                                in UnsafeList<SegmentVertex> polygonVerticesArray, Range range, ref ChiselCurve2DBlob curve, uint smoothingGroup)
        {
            var segments = range.Length;
            for (int p = 2; p < polygons.Length; p++)
            {
                // a side's corners are those of the edge it runs along, at either end of the brush: from one corner to the next
                var a = -1;
                var b = -1;
                var firstEdge = polygons[p].firstEdge;
                for (int e = firstEdge; e < firstEdge + polygons[p].edgeCount && b < 0; e++)
                {
                    var corner = halfEdges[e].vertexIndex % segments;
                    if (a < 0) a = corner;
                    else if (corner != a) b = corner;
                }
                if (b < 0)
                    continue;
                var from   = ((a + 1) % segments == b) ? a : b;
                var vertex = polygonVerticesArray[range.start + from];
                if (vertex.cut || vertex.segmentIndex < 0 || vertex.segmentIndex >= curve.controlPoints.Length ||
                    !curve.controlPoints[vertex.segmentIndex].smoothSide)
                    continue;
                polygons[p].surface.details.smoothingGroup = smoothingGroup;
            }
        }

        // the group the shape's smooth sides are in: along the whole path, which turns in small steps (ChiselPathBlob)
        const uint kSmoothSidesGroup = 1;

        // The pieces of the shape, from each matrix of the path to the next, its smooth sides in one smoothing group
        public static bool GenerateExtrudedShape(NativeList<BlobAssetReference<BrushMeshBlob>> brushMeshes, 
                                                        in UnsafeList<SegmentVertex>    polygonVerticesArray, 
                                                        in UnsafeList<int>              polygonVerticesSegments,
                                                        in UnsafeList<float4x4>         pathMatrices,
                                                        in BlobAssetReference<ChiselCurve2DBlob> curveBlob,
                                                        in BlobAssetReference<InternalChiselSurfaceArray> surfaceDefinitionBlob,
                                                        Allocator allocator = Allocator.Persistent)// Indirect
		{

            if (!brushMeshes.IsCreated)
            {
                Debug.Log($"brushMeshes.IsCreated {brushMeshes.IsCreated}");
                return false;
            }

            int brushMeshIndex = 0;

            //Profiler.BeginSample("CreateExtrudedSubMeshes");
            for (int p = 0; p < polygonVerticesSegments.Length; p++)
            {
                var range = new Range
                { 
                    start = p == 0 ? 0 : polygonVerticesSegments[p - 1],
                    end   =              polygonVerticesSegments[p    ]
                };

                // which way round the polygon goes, and its middle (a point of it on the axis a shape is revolved round
                // doesn't move)
                var middle  = float2.zero;
                var winding = 0.0f;
                for (int i = range.start; i < range.end; i++)
                {
                    var point = polygonVerticesArray[i].position;
                    var next  = polygonVerticesArray[i + 1 < range.end ? i + 1 : range.start].position;
                    winding += (point.x * next.y) - (next.x * point.y);
                    middle  += point;
                }
                middle /= range.Length;

                for (int s = 1; s < pathMatrices.Length; s++)
                {
                    var matrix0 = pathMatrices[s - 1];
                    var matrix1 = pathMatrices[s];

                    // the brush is built going along matrix0's z with the polygon wound one way: a mirrored matrix (an
                    // extrusion's path), going the other way, or the polygon wound the other way round (the half of a
                    // revolved shape across its axis) each turn it inside out, so the ends are swapped when they do
                    var travel     = math.mul(math.inverse(matrix0), math.mul(matrix1, new float4(middle, 0, 1))).z;
                    var handedness = math.determinant(new float3x3(matrix0.c0.xyz, matrix0.c1.xyz, matrix0.c2.xyz));
                    if (travel * handedness * winding > 0) { var m = matrix0; matrix0 = matrix1; matrix1 = m; }

                    if (brushMeshIndex >= brushMeshes.Length)
                    {
                        Debug.Log($"{brushMeshIndex} >= {brushMeshes.Length}");
                        return false;
                    }
                    brushMeshes[brushMeshIndex] = BlobAssetReference<BrushMeshBlob>.Null;

                    using var builder = new BlobBuilder(Allocator.Temp);
                    ref var root = ref builder.ConstructRoot<BrushMeshBlob>();
                    
                    var localVertices = GetExtrudedVertices(polygonVerticesArray, range, matrix0, matrix1, in builder, ref root, out var segmentIndices, Allocator.Temp);
                    if (localVertices.Length == 0)
                        continue;

                    using (segmentIndices)
                    {
                        var arrays = CreateExtrudedSubMesh(range.Length, segmentIndices, segmentIndices.Length, 0, 1, in localVertices, in surfaceDefinitionBlob, in builder, ref root);
                        var polygons = arrays.polygons;
                        var halfEdges = arrays.halfEdges;
                        if (curveBlob.IsCreated)
                            SmoothSides(ref polygons, in halfEdges, in polygonVerticesArray, range, ref curveBlob.Value, kSmoothSidesGroup);

                        if (!Validate(in localVertices, in halfEdges, in polygons, logErrors: true))
                            return false;

                        var localPlanes = builder.Allocate(ref root.localPlanes, polygons.Length);
                        root.localPlaneCount = polygons.Length;
                        var halfEdgePolygonIndices = builder.Allocate(ref root.halfEdgePolygonIndices, halfEdges.Length);
                        CalculatePlanes(ref localPlanes, in polygons, in halfEdges, in localVertices);
                        UpdateHalfEdgePolygonIndices(ref halfEdgePolygonIndices, in polygons);
                        root.localBounds = CalculateBounds(in localVertices);
                        brushMeshes[brushMeshIndex] = builder.CreateBlobAssetReference<BrushMeshBlob>(allocator); // Allocator.Persistent / Confirmed to dispose
						brushMeshIndex++;
                    }
                }
            }
            //Profiler.EndSample();
            return true;
        }

        static Vector2 PointOnBezier(Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3, float t)
        {
            return (1 - t) * (1 - t) * (1 - t) * p0 + 3 * t * (1 - t) * (1 - t) * p1 + 3 * t * t * (1 - t) * p2 + t * t * t * p3;
        }

        public static void GetPathVertices(Curve2D shape, int shapeCurveSegments, List<SegmentVertex> shapeVertices)
        {
            var points = shape.controlPoints;
            var length = points.Length;

            for (int i = 0; i < length; i++)
            {
                var index1 = i;
                var index2 = (i + 1) % length;
                var p1 = points[index1];
                var p2 = points[index2];
                var v1 = p1.position;
                var v2 = p2.position;

                if (shapeCurveSegments == 0 ||
                    (points[index1].constraint2 == ControlPointConstraint.Straight &&
                     points[index2].constraint1 == ControlPointConstraint.Straight))
                {
                    shapeVertices.Add(new SegmentVertex { position = v1, segmentIndex = i });
                    continue;
                }

                float2 v0, v3;

                if (p1.constraint2 != ControlPointConstraint.Straight)
                    v0 = v1 - p1.tangent2;
                else
                    v0 = v1;
                if (p2.constraint1 != ControlPointConstraint.Straight)
                    v3 = v2 - p2.tangent1;
                else
                    v3 = v2;

                shapeVertices.Add(new SegmentVertex { position = v1, segmentIndex = i });
                for (int n = 1; n < shapeCurveSegments; n++)
                {
                    shapeVertices.Add(new SegmentVertex { position = PointOnBezier(v1, v0, v3, v2, n / (float)shapeCurveSegments), segmentIndex = i });
                }
            }
        }
    }
}