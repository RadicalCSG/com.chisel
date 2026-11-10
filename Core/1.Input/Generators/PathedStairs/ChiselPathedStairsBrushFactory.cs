using Unity.Mathematics;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;
using Unity.Entities;

namespace Chisel.Core
{
    public sealed partial class BrushMeshFactory
    {
        public static int CountPathedStairBrushes(UnsafeList<SegmentVertex> shapeVertices,
                                                  bool              closedLoop,

												  MinMaxAABB        bounds,

                                                  float	            stepHeight,
                                                  float	            stepDepth,

                                                  float	            treadHeight,

                                                  float	            nosingDepth,

                                                  float             plateauHeight,

                                                  StairsRiserType   riserType,
                                                  float	            riserDepth,

                                                  StairsSideType    leftSide,
                                                  StairsSideType    rightSide,
                                                  StairsSideType    cornerSide,

                                                  float	            sideWidth,
                                                  float	            sideHeight,
                                                  float	            sideDepth,
                                                  float             sideFrontOffset,
                                                  float             topSideLength,
                                                  float             cornerTopSideLength)
        {
            var totalSubMeshCount = 0;
            for (int i = 0; i < shapeVertices.Length; i++)
            {
                if (i == 0 && !closedLoop)
                    continue;

                // i is where the part of the path ends
                SidesOf(closedLoop, i == 1, i == shapeVertices.Length - 1, leftSide, rightSide, out var segmentLeftSide, out var segmentRightSide);

                var endIsCorner = (closedLoop || i != shapeVertices.Length - 1) && IsControlPoint(in shapeVertices, i);
                var description = new LineairStairsData(bounds, stepHeight, stepDepth, treadHeight,
                                                        nosingDepth, nosingWidth: 0, plateauHeight,
                                                        riserType, riserDepth,
                                                        segmentLeftSide, segmentRightSide,
                                                        sideWidth, sideHeight, sideDepth,
                                                        leftAsSet: true, rightAsSet: true, sideFrontOffset: sideFrontOffset, topSideLength: topSideLength);
                totalSubMeshCount += description.subMeshCount;
                if (endIsCorner)
                {
                    var wall = CornerWallData(bounds, stepHeight, stepDepth, treadHeight, nosingDepth, plateauHeight,
                                              riserType, riserDepth, cornerSide, sideWidth, sideHeight, sideDepth, sideFrontOffset, cornerTopSideLength);
                    totalSubMeshCount += CornerWallCount(in wall);
                }
            }
            return totalSubMeshCount;
        }
        
        /// <summary>
        /// The part the brush at this place of pathed stairs is, in the order they're built (as CountPathedStairBrushes
        /// counts them): each part of the path as linear stairs (ChiselLinearStairs.Part), then its corner wall; -1 past the end
        /// </summary>
        public static int PathedStairsPartOf(int brushIndex, UnsafeList<SegmentVertex> shapeVertices, bool closedLoop, MinMaxAABB bounds,
                                             float stepHeight, float stepDepth, float treadHeight, float nosingDepth, float plateauHeight,
                                             StairsRiserType riserType, float riserDepth, StairsSideType leftSide, StairsSideType rightSide, StairsSideType cornerSide,
                                             float sideWidth, float sideHeight, float sideDepth, float sideFrontOffset, float topSideLength, float cornerTopSideLength)
        {
            var offset = 0;
            for (int i = 0; i < shapeVertices.Length; i++)
            {
                if (i == 0 && !closedLoop)
                    continue;
                SidesOf(closedLoop, i == 1, i == shapeVertices.Length - 1, leftSide, rightSide, out var segmentLeftSide, out var segmentRightSide);
                var endIsCorner = (closedLoop || i != shapeVertices.Length - 1) && IsControlPoint(in shapeVertices, i);
                var description = new LineairStairsData(bounds, stepHeight, stepDepth, treadHeight,
                                                        nosingDepth, nosingWidth: 0, plateauHeight,
                                                        riserType, riserDepth,
                                                        segmentLeftSide, segmentRightSide,
                                                        sideWidth, sideHeight, sideDepth,
                                                        leftAsSet: true, rightAsSet: true, sideFrontOffset: sideFrontOffset, topSideLength: topSideLength);
                if (brushIndex < offset + description.subMeshCount)
                    return (int)ChiselLinearStairs.PartOf(in description, brushIndex - offset);
                offset += description.subMeshCount;
                if (!endIsCorner)
                    continue;
                var wall = CornerWallData(bounds, stepHeight, stepDepth, treadHeight, nosingDepth, plateauHeight,
                                          riserType, riserDepth, cornerSide, sideWidth, sideHeight, sideDepth, sideFrontOffset, cornerTopSideLength);
                var wallCount = CornerWallCount(in wall);
                if (brushIndex < offset + wallCount)
                    return ChiselPathedStairs.kCornerPart;
                offset += wallCount;
            }
            return -1;
        }

        // Each part of the path is the linear stairs as they are, so with their own UVs, moved onto it by its brushes'
        // transformations; brushTransformations is as long as brushMeshes
        public static bool GeneratePathedStairs(NativeList<BlobAssetReference<BrushMeshBlob>> brushMeshes,
                                                NativeList<float4x4> brushTransformations,
                                                UnsafeList<SegmentVertex> shapeVertices,
                                                bool                closedLoop,
												MinMaxAABB          bounds,

                                                float	            stepHeight,
                                                float	            stepDepth,

                                                float	            treadHeight,

                                                float	            nosingDepth,
                                                float               nosingWidth,
                                                
                                                float               plateauHeight,

                                                StairsRiserType     riserType,
                                                float	            riserDepth,

                                                StairsSideType      leftSide,
                                                StairsSideType      rightSide,
                                                StairsSideType      cornerSide,

                                                float	            sideWidth,
                                                float	            sideHeight,
                                                float	            sideDepth,
                                                float               sideFrontOffset,
                                                float               topSideLength,
                                                float               cornerTopSideLength,
                                                in BlobAssetReference<InternalChiselSurfaceArray> surfaceDefinitionBlob,
                                                Allocator           allocator = Allocator.Persistent)// Indirect
        {
            var absDepth    = math.abs(bounds.Max.z - bounds.Min.z);
            var absHeight   = math.abs(bounds.Max.y - bounds.Min.y);
            var width       = (bounds.Max.x - bounds.Min.x);
            var centerX     = (bounds.Max.x + bounds.Min.x) * 0.5f;

            var halfDepth	= absDepth  * 0.5f;
            var halfHeight	= absHeight * 0.5f;

            int subMeshIndex = 0;
            var count = shapeVertices.Length;
            if (count == 0)
                return false;
            // by the point a part starts at: its brushes and where they're put; by corner: the front of its wall
            var partRanges = new NativeArray<int2>(count, Allocator.Temp); using var _partRanges = partRanges;
            var partToPath = new NativeArray<float4x4>(count, Allocator.Temp); using var _partToPath = partToPath;
            var wallFronts = new NativeArray<float4>(count, Allocator.Temp); using var _wallFronts = wallFronts;
            for (int vi0 = (count + count - 3) % count, vi1 = (count + count - 2) % count, vi2 = (count + count - 1) % count, vi3 = 0; vi3 < count; vi0 = vi1, vi1 = vi2, vi2 = vi3, vi3++)
            {
                if (vi2 == 0 && !closedLoop)
                    continue;

                var v0 = shapeVertices[vi0].position;
                var v1 = shapeVertices[vi1].position;
                var v2 = shapeVertices[vi2].position;
                var v3 = shapeVertices[vi3].position;

                var m1 = (v1 + v2) * 0.5f;

                var d0 = (v1 - v0);
                var d1 = (v2 - v1);
                var d2 = (v3 - v2);

                var maxWidth0 = math.length(d0);
                var maxWidth1 = math.length(d1);
                var maxWidth2 = math.length(d2);

                d0 /= maxWidth0;
                d1 /= maxWidth1;
                d2 /= maxWidth2;

                var depthVector = new float3(d1.y, 0, -d1.x);
                var lineCenter  = new float3(m1.x, halfHeight, m1.y) - (depthVector * halfDepth);

                // Each part of the path is a staircase along its line, cut by a plane at each end (see PartPlanes)
                var part = PartPlanes(shapeVertices, closedLoop, vi1);
                var startPlane = part.start;
                var endPlane   = part.end;
                var along3     = new float3(d1.x, 0, d1.y);
                var inward3    = new float3(-d1.y, 0, d1.x);
                var point1     = new float3(v1.x, 0, v1.y);
                var point2     = new float3(v2.x, 0, v2.y);
                var maxDepth   = part.hasTip ? math.dot(part.tip - point1, inward3) : float.PositiveInfinity;

                var transform = float4x4.TRS(lineCenter, // move to center of line
                                              quaternion.LookRotationSafe(depthVector, Vector3.up),	// rotate to align with line
                                              new float3(1));

                // set the width to the width of the line
                var newWidth = math.abs(maxWidth1);
                var halfWidth = newWidth * 0.5f;
                var min = bounds.Min;
                var max = bounds.Max;
                min.x = centerX - halfWidth;
                max.x = centerX + halfWidth;
                bounds = BoundsExtensions.CreateAABB(min: min, max: max);

                SidesOf(closedLoop, vi2 == 1, vi2 == count - 1, leftSide, rightSide, out var segmentLeftSide, out var segmentRightSide);
                var startIsCorner = closedLoop || vi1 != 0;
                var endIsJoin     = closedLoop || vi2 != count - 1;
                // points inside a curve join two parts without a wall
                var endIsCorner   = endIsJoin && IsControlPoint(in shapeVertices, vi2);

                var description = new LineairStairsData(bounds,
                                                        stepHeight, stepDepth,
                                                        treadHeight,
                                                        nosingDepth, nosingWidth,
                                                        plateauHeight,
                                                        riserType, riserDepth,
                                                        segmentLeftSide, segmentRightSide,
                                                        sideWidth, sideHeight, sideDepth,
                                                        leftAsSet: true, rightAsSet: true, sideFrontOffset: sideFrontOffset, topSideLength: topSideLength);
                // the treads reach out past a side without a guard rail only at an open path's start and end
                if (startIsCorner) description.leftNosingWidth  = math.min(description.leftNosingWidth,  0);
                if (endIsJoin)     description.rightNosingWidth = math.min(description.rightNosingWidth, 0);
                var subMeshCount = description.subMeshCount;
                if (subMeshCount > 0 &&
                    !GenerateLinearStairsSubMeshes(brushMeshes, subMeshIndex, in description, in surfaceDefinitionBlob, allocator)) // Indirect
                    return false;

                // the stairs are built in their bounds, which needn't be around the origin
                var stairsCenter  = (bounds.Min + bounds.Max) * 0.5f;
                var toPath        = math.mul(transform, float4x4.Translate(-stairsCenter));
                var fromPath      = math.inverse(toPath);
                var toPathPlanes  = math.transpose(toPath);
                partRanges[vi1] = new int2(subMeshIndex, subMeshCount);
                partToPath[vi1] = toPath;
                for (int m = 0; m < subMeshCount; m++)
                {
                    brushTransformations[subMeshIndex + m] = toPath;
                    ref var localVertices = ref brushMeshes[subMeshIndex + m].Value.localVertices;
                    for (int v = 0; v < localVertices.Length; v++)
                    {
                        // where it is on the path, still as wide as its part of it
                        var vertex = math.mul(toPath, new float4(localVertices[v], 1)).xyz;
                        var depth  = math.dot(new float3(vertex.x, 0, vertex.z) - point1, inward3);
                        if (depth > maxDepth)
                            vertex -= inward3 * (depth - maxDepth);

                        // Every brush of the stairs is a shape pushed out along their width, so cutting it with an
                        // upright plane moves its end along its edges onto that plane. A vertex near an end moves as
                        // far as the end does where it is, so whatever is a way in from it, like a side wall's inner
                        // face, stays that far in
                        var slopeStart = math.max(math.dot(startPlane.xyz, along3), kMinSlope);
                        var slopeEnd   = math.max(math.dot(endPlane.xyz,   along3), kMinSlope);
                        var along      = math.dot(vertex - point1, along3);
                        var atStart    = vertex - (along3 * along);
                        var startMoves = -Distance(startPlane, atStart) / slopeStart;
                        var endMoves   = -Distance(endPlane, atStart + (along3 * maxWidth1)) / slopeEnd;
                        var nearStart  = along < maxWidth1 * 0.5f;
                        var moved      = along + (nearStart ? startMoves : endMoves);

                        // where the two planes cross, deeper than they meet, nothing is left: it goes where they cross
                        var startAt = startMoves;
                        var endAt   = maxWidth1 + endMoves;
                        if (startAt > endAt)
                            moved = ((startAt * slopeStart) + (endAt * slopeEnd)) / (slopeStart + slopeEnd);
                        var position = atStart + (along3 * moved);

                        // back in the stairs' own space: only its ends have moved
                        localVertices[v] = math.mul(fromPath, new float4(position, 1)).xyz;
                    }
                    // the faces at its ends are where they're cut now; the planes are what it's built from
                    ref var brushMesh = ref brushMeshes[subMeshIndex + m].Value;
                    UpdatePlanesAndBounds(ref brushMesh);
                    UseCutPlanes(ref brushMesh, math.mul(toPathPlanes, startPlane), math.mul(toPathPlanes, endPlane));
                }
                subMeshIndex += subMeshCount;

                if (!endIsCorner)
                    continue;

                // The wall at the corner this part ends at: one wall, as wide as a side wall, square to the plane and
                // halfway through it, not cut. Built as the stairs' right side, at their end, half past it
                var wall = CornerWallData(bounds, stepHeight, stepDepth, treadHeight, nosingDepth, plateauHeight,
                                          riserType, riserDepth, cornerSide, sideWidth, sideHeight, sideDepth, sideFrontOffset, cornerTopSideLength);
                var wallCount = CornerWallCount(in wall);
                if (wallCount == 0)
                    continue;
                ref var surfaceDefinition = ref surfaceDefinitionBlob.Value;
                if (surfaceDefinition.surfaces.Length < StairsSurfaces.kCount)
                    return false;
                var wallMinX  = bounds.Min.x - (sideWidth * 0.5f);
                var sideCount = wall.rightSideDescription.subMeshCount;
                GenerateStairsSide(brushMeshes, subMeshIndex, wall.stepCount, wallMinX, wallMinX + sideWidth, wall.rightSideType,
                                   in wall, ref surfaceDefinition, wall.rightSideDescription, allocator);// Indirect
                for (int m = 0; m < sideCount; m++)
                    brushMeshes[subMeshIndex + m] = TurnSideFaces(brushMeshes[subMeshIndex + m]);
                if (wallCount > sideCount)
                    brushMeshes[subMeshIndex + sideCount] = SideFront(brushMeshes, subMeshIndex, sideCount, wallMinX, wallMinX + sideWidth, in wall, ref surfaceDefinition, allocator);// Indirect

                // square to the plane, along it, to the front of the stairs, which is at the path
                var plane    = endPlane.xyz;
                var forward3 = math.normalizesafe(math.cross(new float3(0, 1, 0), plane));
                if (math.dot(forward3, inward3) > 0)
                    forward3 = -forward3;
                // Moved along the plane until the corners of its front are on both parts' fronts; where they meet in a
                // point sticking out, that point is under it and cut off flush with its front (see CutOff)
                var reach    = -math.dot(forward3, inward3);
                var behind   = (reach > kMinSlope) ? (sideWidth * 0.5f) * -math.dot(plane, inward3) / reach : 0;
                // where that point is cut off under it, a little further, so the cut ends inside it
                var shift    = forward3 * (behind < 0 ? behind + kInsideWall : behind);
                // its back the same way round, back
                var backShift = -forward3 * math.max(-behind, 0);
                // all but its back a little forward, so the steps' fronts end inside it rather than on its edges
                var nudge    = (reach > kMinSlope) ? forward3 * (kInsideWall / reach) : float3.zero;
                var backDepth = float.NegativeInfinity;
                for (int m = 0; m < wallCount; m++)
                {
                    ref var localVertices = ref brushMeshes[subMeshIndex + m].Value.localVertices;
                    for (int v = 0; v < localVertices.Length; v++)
                        backDepth = math.max(backDepth, math.dot(math.mul(toPath, new float4(localVertices[v], 1)).xyz - point1, inward3));
                }

                for (int m = 0; m < wallCount; m++)
                {
                    brushTransformations[subMeshIndex + m] = toPath;
                    ref var localVertices = ref brushMeshes[subMeshIndex + m].Value.localVertices;
                    for (int v = 0; v < localVertices.Length; v++)
                    {
                        var vertex   = math.mul(toPath, new float4(localVertices[v], 1)).xyz;
                        var atBack   = math.dot(vertex - point1, inward3) > backDepth - kOnCutPlane;
                        var along    = math.dot(vertex - point1, along3);
                        var atStart  = vertex - (along3 * along);
                        var slopeEnd = math.max(math.dot(plane, along3), kMinSlope);
                        var onPlane  = atStart + (along3 * (maxWidth1 - (Distance(endPlane, atStart + (along3 * maxWidth1)) / slopeEnd)));
                        var position = onPlane + (plane * (along - maxWidth1)) + (atBack ? backShift : shift + nudge);
                        localVertices[v] = math.mul(fromPath, new float4(position, 1)).xyz;
                    }
                    UpdatePlanesAndBounds(ref brushMeshes[subMeshIndex + m].Value);
                }

                // the steps of both parts are cut off flush with its front
                var front = float.NegativeInfinity;
                for (int m = 0; m < wallCount; m++)
                {
                    ref var localVertices = ref brushMeshes[subMeshIndex + m].Value.localVertices;
                    for (int v = 0; v < localVertices.Length; v++)
                        front = math.max(front, math.dot(forward3, math.mul(toPath, new float4(localVertices[v], 1)).xyz));
                }
                // only where their fronts meet in a point under it: elsewhere they reach out past it, away from it
                // where its front would be without that, its corners on the noses
                if (behind < 0)
                    wallFronts[vi2] = new float4(forward3, -(front - kInsideWall));
                subMeshIndex += wallCount;
            }

            for (int part = 0; part < count; part++)
            {
                var range = partRanges[part];
                if (range.y == 0)
                    continue;
                var next   = (part + 1) % count;
                var toPath = partToPath[part];
                for (int m = range.x; m < range.x + range.y; m++)
                {
                    if (math.any(wallFronts[part] != float4.zero))
                        brushMeshes[m] = CutOff(brushMeshes[m], math.mul(math.transpose(toPath), wallFronts[part]), allocator);
                    if (math.any(wallFronts[next] != float4.zero))
                        brushMeshes[m] = CutOff(brushMeshes[m], math.mul(math.transpose(toPath), wallFronts[next]), allocator);
                }
            }

            // the corners' walls go after all the stairs, so where they meet the walls' faces are the ones kept
            const int firstIndex = 0;
            var stairsCount = 0;
            for (int part = 0; part < count; part++)
                stairsCount += partRanges[part].y;
            var total = subMeshIndex - firstIndex;
            if (stairsCount < total)
            {
                var order      = new NativeList<BlobAssetReference<BrushMeshBlob>>(total, Allocator.Temp);
                using var _order = order;
                var transforms = new NativeList<float4x4>(total, Allocator.Temp);
                using var _transforms = transforms;
                for (int pass = 0; pass < 2; pass++)
                {
                    for (int i = firstIndex; i < subMeshIndex; i++)
                    {
                        var isStairs = false;
                        for (int part = 0; part < count && !isStairs; part++)
                            isStairs = i >= partRanges[part].x && i < partRanges[part].x + partRanges[part].y;
                        if (isStairs != (pass == 0))
                            continue;
                        order.Add(brushMeshes[i]);
                        transforms.Add(brushTransformations[i]);
                    }
                }
                for (int i = 0; i < total; i++)
                {
                    brushMeshes[firstIndex + i]          = order[i];
                    brushTransformations[firstIndex + i] = transforms[i];
                }
            }
            return true;
        }

        // A convex brush with all of it in front of the plane cut off, the cut closed by a face on the plane with the
        // surface of the face most like it. The source is disposed when a new brush is made from it
        static BlobAssetReference<BrushMeshBlob> CutOff(BlobAssetReference<BrushMeshBlob> source, float4 plane, Allocator allocator)
        {
            const float kOnPlane = 1e-4f;
            if (!source.IsCreated)
                return source;
            ref var src = ref source.Value;
            if (src.halfEdgeUVs.Length > 0 || src.halfEdgeNormals.Length > 0)
                return source;
            var vertexCount = src.localVertices.Length;
            var distances = new NativeArray<float>(vertexCount, Allocator.Temp); using var _distances = distances;
            bool anyIn = false, anyOut = false;
            for (int v = 0; v < vertexCount; v++)
            {
                var d = Distance(plane, src.localVertices[v]);
                if (math.abs(d) < kOnPlane) d = 0;
                distances[v] = d;
                anyIn  |= d < 0;
                anyOut |= d > 0;
            }
            if (!anyIn || !anyOut)
                return source;

            var vertices = new NativeList<float3>(vertexCount + 8, Allocator.Temp); using var _vertices = vertices;
            var remap    = new NativeArray<int>(vertexCount, Allocator.Temp); using var _remap = remap;
            for (int v = 0; v < vertexCount; v++)
            {
                remap[v] = -1;
                if (distances[v] > 0)
                    continue;
                remap[v] = vertices.Length;
                vertices.Add(src.localVertices[v]);
            }

            // each polygon's part behind the plane, with a corner where it crosses each edge
            var crossings = new NativeParallelHashMap<int2, int>(16, Allocator.Temp); using var _crossings = crossings;
            var loops     = new NativeList<int>(src.halfEdges.Length + 16, Allocator.Temp); using var _loops = loops;
            var loopsOf   = new NativeList<int3>(src.polygons.Length + 1, Allocator.Temp); using var _loopsOf = loopsOf; // start, count, polygon
            for (int p = 0; p < src.polygons.Length; p++)
            {
                var polygon = src.polygons[p];
                var start   = loops.Length;
                for (int e = 0; e < polygon.edgeCount; e++)
                {
                    var a  = src.halfEdges[polygon.firstEdge + e].vertexIndex;
                    var b  = src.halfEdges[polygon.firstEdge + ((e + 1) % polygon.edgeCount)].vertexIndex;
                    var da = distances[a];
                    var db = distances[b];
                    if (da <= 0)
                        loops.Add(remap[a]);
                    if ((da < 0 && db > 0) || (da > 0 && db < 0))
                    {
                        var key = new int2(math.min(a, b), math.max(a, b));
                        if (!crossings.TryGetValue(key, out var crossing))
                        {
                            crossing = vertices.Length;
                            vertices.Add(math.lerp(src.localVertices[a], src.localVertices[b], da / (da - db)));
                            crossings.Add(key, crossing);
                        }
                        loops.Add(crossing);
                    }
                }
                if (loops.Length - start < 3)
                {
                    loops.Length = start;
                    continue;
                }
                loopsOf.Add(new int3(start, loops.Length - start, p));
            }

            // the cut: every corner on the plane, round it in order
            var onPlane = new NativeList<int>(16, Allocator.Temp); using var _onPlane = onPlane;
            for (int v = 0; v < vertices.Length; v++)
            {
                if (math.abs(Distance(plane, vertices[v])) < kOnPlane * 10)
                    onPlane.Add(v);
            }
            if (onPlane.Length < 3 || loopsOf.Length == 0)
                return source;
            var middle = float3.zero;
            for (int i = 0; i < onPlane.Length; i++)
                middle += vertices[onPlane[i]];
            middle /= onPlane.Length;
            var normal = math.normalize(plane.xyz);
            var axisU  = math.normalizesafe(math.cross(normal, math.abs(normal.y) < 0.9f ? new float3(0, 1, 0) : new float3(1, 0, 0)));
            var axisV  = math.cross(normal, axisU);
            var angles = new NativeArray<float>(onPlane.Length, Allocator.Temp); using var _angles = angles;
            for (int i = 0; i < onPlane.Length; i++)
            {
                var offset = vertices[onPlane[i]] - middle;
                angles[i] = math.atan2(math.dot(offset, axisV), math.dot(offset, axisU));
            }
            for (int i = 1; i < onPlane.Length; i++)
            {
                for (int j = i; j > 0 && angles[j - 1] > angles[j]; j--)
                {
                    (angles[j - 1], angles[j]) = (angles[j], angles[j - 1]);
                    (onPlane[j - 1], onPlane[j]) = (onPlane[j], onPlane[j - 1]);
                }
            }
            // turned the way the brush's own faces turn around their planes
            var first     = loopsOf[0];
            var turnsWith = math.dot(Turn(vertices, loops, first.x, first.y), src.localPlanes[first.z].xyz) > 0;
            var capStart  = loops.Length;
            for (int i = 0; i < onPlane.Length; i++)
                loops.Add(onPlane[i]);
            if ((math.dot(Turn(vertices, loops, capStart, onPlane.Length), normal) > 0) != turnsWith)
            {
                for (int i = 0, j = onPlane.Length - 1; i < j; i++, j--)
                    (loops[capStart + i], loops[capStart + j]) = (loops[capStart + j], loops[capStart + i]);
            }
            var likest = 0;
            for (int p = 1; p < src.polygons.Length; p++)
            {
                if (math.dot(src.localPlanes[p].xyz, normal) > math.dot(src.localPlanes[likest].xyz, normal))
                    likest = p;
            }
            loopsOf.Add(new int3(capStart, onPlane.Length, -1 - likest));

            using var builder = new BlobBuilder(Allocator.Temp);
            ref var root = ref builder.ConstructRoot<BrushMeshBlob>();
            var newVertices  = builder.Allocate(ref root.localVertices, vertices.Length);
            for (int v = 0; v < vertices.Length; v++)
                newVertices[v] = vertices[v];
            var newHalfEdges = builder.Allocate(ref root.halfEdges, loops.Length);
            var newPolygons  = builder.Allocate(ref root.polygons, loopsOf.Length);
            var newPlanes    = builder.Allocate(ref root.localPlanes, loopsOf.Length);
            root.localPlaneCount = loopsOf.Length;
            var edgeOf = new NativeParallelHashMap<int2, int>(loops.Length, Allocator.Temp); using var _edgeOf = edgeOf;
            for (int p = 0; p < loopsOf.Length; p++)
            {
                var loop = loopsOf[p];
                var from = loop.z >= 0 ? loop.z : (-1 - loop.z);
                newPolygons[p] = new BrushMeshBlob.Polygon
                {
                    firstEdge        = loop.x,
                    edgeCount        = loop.y,
                    descriptionIndex = src.polygons[from].descriptionIndex,
                    surface          = src.polygons[from].surface
                };
                newPlanes[p] = loop.z >= 0 ? src.localPlanes[loop.z] : new float4(normal, plane.w / math.length(plane.xyz));
                for (int e = 0; e < loop.y; e++)
                {
                    var a = loops[loop.x + e];
                    var b = loops[loop.x + ((e + loop.y - 1) % loop.y)];
                    newHalfEdges[loop.x + e] = new BrushMeshBlob.HalfEdge { vertexIndex = a, twinIndex = -1 };
                    edgeOf.TryAdd(new int2(a, b), loop.x + e);
                }
            }
            for (int p = 0; p < loopsOf.Length; p++)
            {
                var loop = loopsOf[p];
                for (int e = 0; e < loop.y; e++)
                {
                    var a = loops[loop.x + e];
                    var b = loops[loop.x + ((e + loop.y - 1) % loop.y)];
                    if (edgeOf.TryGetValue(new int2(b, a), out var twin))
                    {
                        var halfEdge = newHalfEdges[loop.x + e];
                        halfEdge.twinIndex = twin;
                        newHalfEdges[loop.x + e] = halfEdge;
                    }
                }
            }
            if (!Validate(in newVertices, in newHalfEdges, in newPolygons, logErrors: true))
                return source;
            var halfEdgePolygonIndices = builder.Allocate(ref root.halfEdgePolygonIndices, loops.Length);
            UpdateHalfEdgePolygonIndices(ref halfEdgePolygonIndices, in newPolygons);
            root.localBounds = CalculateBounds(in newVertices);
            var result = builder.CreateBlobAssetReference<BrushMeshBlob>(allocator);
            source.Dispose();
            return result;
        }

        // Which way round a loop of corners goes, as a normal by the right hand rule
        static float3 Turn(NativeList<float3> vertices, NativeList<int> loops, int start, int count)
        {
            var normal = float3.zero;
            for (int i = 0; i < count; i++)
            {
                var a = vertices[loops[start + i]];
                var b = vertices[loops[start + ((i + 1) % count)]];
                normal += math.cross(a, b);
            }
            return normal;
        }

        // A corner's wall: the right side of stairs with no other side, with a piece in front of it reaching out past the
        // steps as far as asked, at least as far as the treads' noses
        static LineairStairsData CornerWallData(MinMaxAABB bounds, float stepHeight, float stepDepth, float treadHeight,
                                                float nosingDepth, float plateauHeight, StairsRiserType riserType, float riserDepth,
                                                StairsSideType cornerSide, float sideWidth, float sideHeight, float sideDepth, float sideFrontOffset,
                                                float topSideLength)
        {
            return new LineairStairsData(bounds, stepHeight, stepDepth, treadHeight, nosingDepth, nosingWidth: 0, plateauHeight,
                                         riserType, riserDepth, StairsSideType.None, cornerSide,
                                         sideWidth, sideHeight, sideDepth, leftAsSet: true, rightAsSet: true,
                                         sideFrontOffset: math.max(nosingDepth + kInsideWall, sideFrontOffset), topSideLength: topSideLength,
                                         bandUnderSlope: false);
        }

        // its pieces: the wall's own, then the one in front of it
        static int CornerWallCount(in LineairStairsData wall)
        {
            if (!wall.rightSideDescription.enabled)
                return 0;
            return wall.rightSideDescription.subMeshCount + (wall.sideFrontOffset > 0 ? 1 : 0);
        }


        // The side walls of a part of the path. A linear stairs' left side is at the start of its part, its right side
        // at the end. An open path has the stairs' own left side at its start and right side at its end; a corner has
        // a wall of its own (see CornerWallData), so the stairs on either side of it have none there
        static void SidesOf(bool closed, bool isFirst, bool isLast,
                            StairsSideType leftSide, StairsSideType rightSide,
                            out StairsSideType startSide, out StairsSideType endSide)
        {
            startSide = (!closed && isFirst) ? leftSide  : StairsSideType.None;
            endSide   = (!closed && isLast)  ? rightSide : StairsSideType.None;
        }

        // A point of the path that is one of its curve's control points, not one inside a curved span
        static bool IsControlPoint(in UnsafeList<SegmentVertex> points, int index)
        {
            return index == 0 || points[index].segmentIndex != points[index - 1].segmentIndex;
        }

        // a cut this close to along its part of the path is taken as this
        const float kMinSlope = 0.01f;
        const float kOnCutPlane = 0.001f;
        const float kInsideWall = 0.001f;

        // An upright plane through a point of the path, its normal along the path; the path's (x, y) is (x, z)
        static float4 CutPlane(float2 normal, float2 point)
        {
            var normal3 = new float3(normal.x, 0, normal.y);
            return new float4(normal3, -math.dot(normal3, new float3(point.x, 0, point.y)));
        }

        static float Distance(float4 plane, float3 point) { return math.dot(plane.xyz, point) + plane.w; }

        struct Part
        {
            public float4 start;    // the plane its start is cut by, its normal into it
            public float4 end;      // the plane its end is cut by, its normal out of it, into the next
            public bool   hasTip;   // the two cross on the stairs' side, so it ends in a point there
            public float3 tip;
        }

        // The planes the part of the path from point index to the next is cut by. Between two parts it's the one plane
        // where both are as far from their own line, so their steps meet there; it's shared, so both are cut by
        // exactly the same one. An open path's ends are cut straight across; the parts around the path wrap, so
        // without that they'd meet the part from its end back to its start
        static Part PartPlanes(in UnsafeList<SegmentVertex> points, bool closed, int index)
        {
            var count  = points.Length;
            var p0     = points[(index + count - 1) % count].position;
            var p1     = points[index].position;
            var p2     = points[(index + 1) % count].position;
            var p3     = points[(index + 2) % count].position;
            var along  = math.normalizesafe(p2 - p1);
            var before = math.normalizesafe(p1 - p0, along);
            var after  = math.normalizesafe(p3 - p2, along);
            var isFirst = !closed && index == 0;
            var isLast  = !closed && index == count - 2;

            var part = new Part
            {
                start = CutPlane(isFirst ? along : math.normalizesafe(before + along, along), p1),
                end   = CutPlane(isLast  ? along : math.normalizesafe(along + after,  along), p2)
            };
            var inward = new float3(-along.y, 0, along.x);
            if (CrossAt(part.start, part.end, out var tip) && math.dot(tip - new float3(p1.x, 0, p1.y), inward) > 0)
            {
                part.hasTip = true;
                part.tip    = tip;
            }
            return part;
        }

        // Where two upright planes cross, at height 0
        static bool CrossAt(float4 a, float4 b, out float3 point)
        {
            var determinant = (a.x * b.z) - (a.z * b.x);
            if (math.abs(determinant) < 1e-6f)
            {
                point = default;
                return false;
            }
            point = new float3(((-a.w * b.z) + (b.w * a.z)) / determinant, 0, ((-b.w * a.x) + (a.w * b.x)) / determinant);
            return true;
        }

        // A face on a cut plane takes that plane as it is, facing out, instead of one from its own vertices: the
        // neighbouring part's face on it then has exactly the same plane the other way round, and the two cancel
        static void UseCutPlanes(ref BrushMeshBlob brushMesh, float4 startPlane, float4 endPlane)
        {
            ref var vertices  = ref brushMesh.localVertices;
            ref var halfEdges = ref brushMesh.halfEdges;
            ref var polygons  = ref brushMesh.polygons;
            ref var planes    = ref brushMesh.localPlanes;
            for (int p = 0; p < polygons.Length && p < planes.Length; p++)
            {
                ref var polygon = ref polygons[p];
                bool onStart = true, onEnd = true;
                for (int e = polygon.firstEdge; e < polygon.firstEdge + polygon.edgeCount; e++)
                {
                    var vertex = vertices[halfEdges[e].vertexIndex];
                    onStart &= math.abs(Distance(startPlane, vertex)) < kOnCutPlane;
                    onEnd   &= math.abs(Distance(endPlane,   vertex)) < kOnCutPlane;
                }
                var cut = onStart ? startPlane : onEnd ? endPlane : float4.zero;
                if (!math.any(cut.xyz != float3.zero))
                    continue;
                planes[p] = (math.dot(planes[p].xyz, cut.xyz) >= 0) ? cut : -cut;
            }
        }

        // Its planes and bounds from where its vertices are now
        internal static void UpdatePlanesAndBounds(ref BrushMeshBlob brushMesh)
        {
            ref var vertices  = ref brushMesh.localVertices;
            ref var halfEdges = ref brushMesh.halfEdges;
            ref var polygons  = ref brushMesh.polygons;
            ref var planes    = ref brushMesh.localPlanes;
            for (int p = 0; p < polygons.Length && p < planes.Length; p++)
            {
                ref var polygon = ref polygons[p];
                var lastEdge   = polygon.firstEdge + polygon.edgeCount;
                var normal     = double3.zero;
                var prevVertex = (double3)vertices[halfEdges[lastEdge - 1].vertexIndex];
                for (int n = polygon.firstEdge; n < lastEdge; n++)
                {
                    var currVertex = (double3)vertices[halfEdges[n].vertexIndex];
                    normal.x += (prevVertex.y - currVertex.y) * (prevVertex.z + currVertex.z);
                    normal.y += (prevVertex.z - currVertex.z) * (prevVertex.x + currVertex.x);
                    normal.z += (prevVertex.x - currVertex.x) * (prevVertex.y + currVertex.y);
                    prevVertex = currVertex;
                }
                // a side squeezed to nothing where a part ends in a point keeps the plane it had
                if (math.lengthsq(normal) < 1e-12)
                    continue;
                normal = math.normalize(normal);
                var d = 0.0;
                for (int n = polygon.firstEdge; n < lastEdge; n++)
                    d -= math.dot(normal, vertices[halfEdges[n].vertexIndex]);
                d /= polygon.edgeCount;
                planes[p] = new float4((float3)normal, (float)d);
            }

            var min = new float3(float.PositiveInfinity);
            var max = new float3(float.NegativeInfinity);
            for (int v = 0; v < vertices.Length; v++)
            {
                min = math.min(min, vertices[v]);
                max = math.max(max, vertices[v]);
            }
            brushMesh.localBounds = new MinMaxAABB { Min = min, Max = max };
        }
    }
}