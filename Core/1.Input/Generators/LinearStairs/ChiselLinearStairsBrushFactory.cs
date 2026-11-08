using Unity.Mathematics;
using Unity.Collections;
using UnityEngine;
using Unity.Entities;

namespace Chisel.Core
{
    // TODO: rename
    public sealed partial class BrushMeshFactory
    {
        // TODO: Fix all overlapping brushes
        internal static bool GenerateLinearStairsSubMeshes(NativeList<BlobAssetReference<BrushMeshBlob>> brushMeshes, int subMeshOffset, 
                                                           in LineairStairsData description, in BlobAssetReference<InternalChiselSurfaceArray> surfaceDefinitionBlob, 
                                                           Allocator allocator = Allocator.Persistent)// Indirect
		{
            // TODO: properly assign all materials

            ref var surfaceDefinition = ref surfaceDefinitionBlob.Value;
            if (surfaceDefinition.surfaces.Length < StairsSurfaces.kCount)
                return false;

            var stepOffset = new float3(0, -description.stepHeight, description.stepDepth);
            if (description.stepCount > 0)
            {
                if (description.haveRiser)
                {
                    var min = description.bounds.Min;
                    var max = description.bounds.Max;
                    max.z = min.z + description.stepDepthOffset + description.stepDepth;
                    if (description.riserType != StairsRiserType.FillDown)
                    {
                        if (description.riserType == StairsRiserType.ThinRiser)
                            min.z = max.z - description.riserDepth;
                        else
                            min.z += description.stepDepthOffset;
                        if (description.thickRiser)
                            min.z -= description.offsetZ;
                    }
                    min.y = max.y - description.stepHeight;
                    min.y -= description.treadHeight;
                    max.y -= description.treadHeight;
                    // the right side is at min.x and the left at max.x, as the sides themselves are built (below); only a
                    // wall that goes down below the steps takes their place, one above them leaves the steps their width.
                    // Filled down, any wall goes down to the floor
                    var fillDown   = description.riserType == StairsRiserType.FillDown;
                    var rightTaken = description.rightSideType == StairsSideType.Down || description.rightSideType == StairsSideType.DownAndUp ||
                                     (fillDown && description.rightSideType != StairsSideType.None);
                    var leftTaken  = description.leftSideType  == StairsSideType.Down || description.leftSideType  == StairsSideType.DownAndUp ||
                                     (fillDown && description.leftSideType  != StairsSideType.None);
                    min.x += rightTaken ? description.rightSideWidth : 0;
                    max.x -= leftTaken  ? description.leftSideWidth : 0;

                    var extrusion = new float3(max.x - min.x, 0, 0);
                    if (description.riserType == StairsRiserType.FillDown)
                    {
                        GenerateFilledDown(brushMeshes, subMeshOffset, in description, min.x, max.x, max.y, ref surfaceDefinition, allocator);
                    } else
                    for (int i = 0; i < description.stepCount; i++)
                    {
                        if (i == 1 &&
                            description.thickRiser)
                        {
                            min.z += description.offsetZ;
                        }
                        if (i == description.stepCount - 1)
                        {
                            // the lowest step goes down to the floor, or to the base when there is one, which is its own
                            min.y += description.treadHeight - (description.haveBase ? 0 : description.offsetY);
                        }

                        var minZ = math.max(description.bounds.Min.z, min.z);
                        var maxZ = math.min(description.bounds.Max.z, max.z);

                        NativeArray<float3> vertices;
						using var _vertices = vertices = new NativeArray<float3>(4, Allocator.Temp);
                        if (i == 0 || description.riserType != StairsRiserType.Smooth)
                        {
                            vertices[0] = new float3(min.x, min.y, minZ);	// 0
                            vertices[1] = new float3(min.x, min.y, maxZ);	// 1
                            vertices[2] = new float3(min.x, max.y, maxZ);   // 2
                            vertices[3] = new float3(min.x, max.y, minZ);	// 3
                        } else
                        {
                            vertices[0] = new float3(min.x, min.y, minZ);	// 0
                            vertices[1] = new float3(min.x, min.y, maxZ);	// 1
                            vertices[2] = new float3(min.x, max.y, maxZ);   // 2
                            vertices[3] = new float3(min.x, max.y, minZ - description.stepDepth);	// 3
                        }

                        NativeArray<int> indices;
						using var _indices = indices = new NativeArray<int>(6, Allocator.Temp);
                        indices[0] = 0;
                        indices[1] = 1;
                        indices[2] = 2;
                        indices[3] = 0;
                        indices[4] = 2;
                        indices[5] = 3;

                        // its top and bottom start their texture at its front, its front and back at its bottom; its sides
                        // are turned as the side walls are
                        brushMeshes[subMeshOffset + i] = TurnSideFaces(AlignedUpright(AlignedToFront(CreateExtrudedSubMeshBlob(vertices, extrusion, indices, StairsPart.Step, ref surfaceDefinition, allocator),
                                                                                       new float2(min.x, maxZ), new float2(max.x, maxZ)),
                                                                        new float3(min.x, description.bounds.Max.y, 0), aroundOrigin: false, description.stepHeight, topRunsOnFromFront: 1));// Indirect

                        // the base under the lowest step, from the floor up, as wide as the steps and as deep as that step,
                        // at least a whole step deep under a thin riser
                        if (i == description.stepCount - 1 && description.haveBase)
                        {
                            var floor    = description.bounds.Min.y;
                            var baseMinZ = math.max(description.bounds.Min.z, math.min(minZ, maxZ - description.stepDepth));
                            vertices[0] = new float3(min.x, floor, baseMinZ);
                            vertices[1] = new float3(min.x, floor, maxZ);
                            vertices[2] = new float3(min.x, min.y, maxZ);
                            vertices[3] = new float3(min.x, min.y, baseMinZ);
                            brushMeshes[subMeshOffset + description.startBase] = TurnSideFaces(AlignedUpright(AlignedToFront(CreateExtrudedSubMeshBlob(vertices, extrusion, indices, StairsPart.Base, ref surfaceDefinition, allocator),
                                                                                                             new float2(min.x, maxZ), new float2(max.x, maxZ)),
                                                                                              // from where the step above it starts its texture, so it runs on across the two
                                                                                              new float3(min.x, StepTextureBottom(max.y, in description), 0), aroundOrigin: false, 0));// Indirect
                        }

						if (description.riserType != StairsRiserType.FillDown)
                            min.z += description.stepDepth;
                        max.z += description.stepDepth;
                        min.y -= description.stepHeight;
                        max.y -= description.stepHeight;
                    }
                }
                if (description.haveTread)
                {
                    var min = new float3(description.bounds.Min.x + description.rightSideWidth, description.bounds.Max.y - description.treadHeight, description.bounds.Min.z);
                    var max = new float3(description.bounds.Max.x - description.leftSideWidth, description.bounds.Max.y, description.bounds.Min.z + description.stepDepthOffset + description.stepDepth + description.nosingDepth);
                    for (int i = 0; i < description.stepCount; i++)
                    {
                        min.x = description.bounds.Min.x - description.rightNosingWidth;
                        max.x = description.bounds.Max.x + description.leftNosingWidth;
                        if (i == 1)
                        {
                            min.z = max.z - (description.stepDepth + description.nosingDepth);
                        }

                        NativeArray<float3> vertices;
						using var _vertices = vertices = new NativeArray<float3>(4, Allocator.Temp);
                        vertices[0] = new float3(min.x, min.y, min.z); // 0
                        vertices[1] = new float3(min.x, min.y, max.z); // 1
                        vertices[2] = new float3(min.x, max.y, max.z); // 2
                        vertices[3] = new float3(min.x, max.y, min.z); // 3
                        var extrusion = new float3(max.x - min.x, 0, 0);

                        NativeArray<int> indices;
						using var _indices = indices = new NativeArray<int>(6, Allocator.Temp);
                        indices[0] = 0;
                        indices[1] = 1;
                        indices[2] = 2;
                        indices[3] = 0;
                        indices[4] = 2;
                        indices[5] = 3;

                        // the tread's texture starts at its nose, the front edge it reaches out to, and on its front and back
                        // at its bottom
                        brushMeshes[subMeshOffset + description.startTread + i] = AlignedUpright(AlignedToFront(CreateExtrudedSubMeshBlob(vertices, extrusion, indices, StairsPart.Tread, ref surfaceDefinition, allocator),
                                                                                                                new float2(min.x, max.z), new float2(max.x, max.z)),
                                                                                                 // on steps solid down to the floor, from the floor up as the solid
                                                                                                 description.riserType == StairsRiserType.FillDown ? new float3(description.bounds.Min.x, description.bounds.Min.y, 0)
                                                                                                                                                   : new float3(min.x, description.bounds.Max.y, 0),
                                                                                                 aroundOrigin: false, description.riserType == StairsRiserType.FillDown ? 0 : description.stepHeight, topRunsOnFromFront: 1);// Indirect

						min += stepOffset;
                        max += stepOffset;
                    }
                }

                if (description.leftSideDescription.enabled)
                {
                    var minX = description.bounds.Max.x - description.leftSideWidth;
                    var maxX = description.bounds.Max.x;

                    GenerateStairsSide(brushMeshes, subMeshOffset + description.startLeftSide, description.stepCount, minX, maxX, 
                                       description.leftSideType, in description, ref surfaceDefinition, description.leftSideDescription, allocator);// Indirect
				}

                if (description.rightSideDescription.enabled)
                {
                    var minX = description.bounds.Min.x;
                    var maxX = description.bounds.Min.x + description.rightSideWidth;

                    GenerateStairsSide(brushMeshes, subMeshOffset + description.startRightSide, description.stepCount, minX, maxX, description.rightSideType,
                                       in description, ref surfaceDefinition, description.rightSideDescription, allocator);// Indirect
				}

                // the side walls' texture is turned a quarter round on their faces to the sides
                var sideEnd = subMeshOffset + description.startRightSide + (description.rightSideDescription.enabled ? description.rightSideDescription.subMeshCount : 0);
                for (int i = subMeshOffset + description.startLeftSide; i < sideEnd && i < brushMeshes.Length; i++)
                    brushMeshes[i] = TurnSideFaces(brushMeshes[i]);

                if (description.sideFrontOffset > 0)
                {
                    var front = subMeshOffset + description.startSideFronts;
                    if (description.leftSideDescription.enabled)
                        brushMeshes[front++] = SideFront(brushMeshes, subMeshOffset + description.startLeftSide, description.leftSideDescription.subMeshCount,
                                                         description.bounds.Max.x - description.leftSideWidth, description.bounds.Max.x, in description, ref surfaceDefinition, allocator);
                    if (description.rightSideDescription.enabled)
                        brushMeshes[front++] = SideFront(brushMeshes, subMeshOffset + description.startRightSide, description.rightSideDescription.subMeshCount,
                                                         description.bounds.Min.x, description.bounds.Min.x + description.rightSideWidth, in description, ref surfaceDefinition, allocator);
                }
            }
            return true;
        }

        // where the texture of a step with its top here starts, as AlignedUpright has it for the steps
        static float StepTextureBottom(float top, in LineairStairsData description)
        {
            var origin = description.bounds.Max.y;
            return origin + (math.ceil((top - origin) / description.stepHeight - 0.001f) - 1) * description.stepHeight;
        }

        // the shortest a wall beside the top floor is, when the length set doesn't reach past the wall under the steps
        const float kMinTopSide = 0.001f;

        // In front of a side wall, as far out as asked: its front face pushed out straight, top and bottom flat
        static BlobAssetReference<BrushMeshBlob> SideFront(NativeList<BlobAssetReference<BrushMeshBlob>> brushMeshes, int first, int count, float minX, float maxX,
                                                           in LineairStairsData description, ref InternalChiselSurfaceArray surfaceDefinition, Allocator allocator)
        {
            var front = float.NegativeInfinity;
            for (int i = first; i < first + count; i++)
            {
                if (!brushMeshes[i].IsCreated)
                    continue;
                ref var vertices = ref brushMeshes[i].Value.localVertices;
                for (int v = 0; v < vertices.Length; v++)
                    front = math.max(front, vertices[v].z);
            }
            float bottom = float.PositiveInfinity, top = float.NegativeInfinity;
            for (int i = first; i < first + count; i++)
            {
                if (!brushMeshes[i].IsCreated)
                    continue;
                ref var vertices = ref brushMeshes[i].Value.localVertices;
                for (int v = 0; v < vertices.Length; v++)
                {
                    if (vertices[v].z < front - 1e-4f)
                        continue;
                    bottom = math.min(bottom, vertices[v].y);
                    top    = math.max(top,    vertices[v].y);
                }
            }
            if (!(top > bottom))
                return BlobAssetReference<BrushMeshBlob>.Null;

            var z0 = front;
            var z1 = front + description.sideFrontOffset;
            NativeArray<float3> corners;
            using var _corners = corners = new NativeArray<float3>(4, Allocator.Temp);
            corners[0] = new float3(minX, top,    z0);
            corners[1] = new float3(minX, bottom, z0);
            corners[2] = new float3(minX, bottom, z1);
            corners[3] = new float3(minX, top,    z1);
            NativeArray<int> indices;
            using var _indices = indices = new NativeArray<int>(6, Allocator.Temp);
            indices[0] = 0; indices[1] = 1; indices[2] = 2;
            indices[3] = 0; indices[4] = 2; indices[5] = 3;
            return TurnSideFaces(TexturedAsTheSteps(CreateExtrudedSubMeshBlob(corners, new float3(maxX - minX, 0, 0), indices, StairsPart.Wall, ref surfaceDefinition, allocator), in description));// Indirect
        }

        // Steps solid down to the floor, seen from the side: one shape under them all, flat on top at the back, sloping down
        // to the floor along the steps' inner corners and flat on the floor, and on its slope a small three sided piece for
        // each step, between its top and its front. So the back, the bottom and each side of the solid is a single face
        static void GenerateFilledDown(NativeList<BlobAssetReference<BrushMeshBlob>> brushMeshes, int subMeshOffset, in LineairStairsData description,
                                       float minX, float maxX, float firstTop, ref InternalChiselSurfaceArray surfaceDefinition, Allocator allocator)
        {
            var floor     = description.bounds.Min.y;
            var back      = description.bounds.Min.z;
            var lastZ     = description.bounds.Max.z;
            var depth     = description.stepDepth;
            var height    = description.stepHeight;
            var count     = description.stepCount;
            // step i has its top (under its tread) at firstTop - i * height and its front at firstFront + i * depth; the
            // line through the steps' inner corners, where each step's top meets the front of the one above it, starts at
            // the back of the first step
            var firstFront  = back + description.stepDepthOffset + depth;
            var lineStart   = firstFront - depth;
            var lineOnFloor = lineStart + (firstTop - floor) * (depth / height);

            using var vertices = new NativeList<float3>(8, Allocator.Temp);

            // each step's piece above the line
            for (int i = 0; i < count; i++)
            {
                var top      = firstTop - i * height;
                var front    = math.min(lastZ, firstFront + i * depth);
                var behind   = math.min(lastZ, firstFront + (i - 1) * depth);
                var lowFront = top - height;
                vertices.Clear();
                vertices.Add(new float3(minX, top, behind));
                if (lowFront >= floor)
                {
                    vertices.Add(new float3(minX, lowFront, front));
                } else
                {
                    // the bottom step reaches the floor before the line does
                    vertices.Add(new float3(minX, floor, math.min(front, lineOnFloor)));
                    vertices.Add(new float3(minX, floor, front));
                }
                vertices.Add(new float3(minX, top, front));
                brushMeshes[subMeshOffset + i] = FilledDownPiece(vertices, minX, maxX, front, fromFloor: true, in description, ref surfaceDefinition, allocator);
            }

            // the shape under them: back, floor, up the front to the line, along it, and flat on top at the back
            var lastFront = math.min(lastZ, firstFront + (count - 1) * depth);
            var end       = math.min(lastFront, lineOnFloor);
            vertices.Clear();
            vertices.Add(new float3(minX, floor, back));
            vertices.Add(new float3(minX, floor, end));
            if (end >= lastFront - 1e-5f)
            {
                var lineAtFront = firstTop - (lastFront - lineStart) * (height / depth);
                if (lineAtFront > floor + 1e-5f)
                    vertices.Add(new float3(minX, lineAtFront, lastFront));
            }
            vertices.Add(new float3(minX, firstTop, lineStart));
            if (lineStart > back + 1e-5f)
                vertices.Add(new float3(minX, firstTop, back));
            brushMeshes[subMeshOffset + count] = FilledDownPiece(vertices, minX, maxX, lineStart, fromFloor: true, in description, ref surfaceDefinition, allocator);
        }

        // A piece of the solid under the steps from its outline seen from the side, with its texture as the steps have it;
        // its fronts and back have it from the floor up, as the walls beside it, and across from the stairs' side, so the
        // solid and its walls show one texture in front and at the back
        static BlobAssetReference<BrushMeshBlob> FilledDownPiece(NativeList<float3> vertices, float minX, float maxX, float frontZ, bool fromFloor,
                                                                 in LineairStairsData description, ref InternalChiselSurfaceArray surfaceDefinition, Allocator allocator)
        {
            using var indices = new NativeList<int>(18, Allocator.Temp);
            // a fan of triangles over its corners, which are in the order the other step pieces have theirs
            for (int k = 1; k + 1 < vertices.Length; k++)
            {
                indices.Add(0);
                indices.Add(k);
                indices.Add(k + 1);
            }
            var extrusion = new float3(maxX - minX, 0, 0);
            return TurnSideFaces(AlignedUpright(AlignedToFront(CreateExtrudedSubMeshBlob(vertices.AsArray(), extrusion, indices.AsArray(), StairsPart.Step, ref surfaceDefinition, allocator),
                                                               new float2(minX, frontZ), new float2(maxX, frontZ)),
                                                fromFloor ? new float3(description.bounds.Min.x, description.bounds.Min.y, 0)
                                                          : new float3(description.bounds.Min.x, description.bounds.Max.y, 0),
                                                aroundOrigin: false, fromFloor ? 0 : description.stepHeight, topRunsOnFromFront: 1));// Indirect
        }

        // A face to the side has its texture turned a quarter round: u becomes v and v becomes -u. The faces on either side
        // look the other way, so a quarter round in the face's own space is the other way round on each; whichever way
        // it ends up, v goes up, so neither side is upside down
        static BlobAssetReference<BrushMeshBlob> TurnSideFaces(BlobAssetReference<BrushMeshBlob> brushMesh)
        {
            if (!brushMesh.IsCreated)
                return brushMesh;
            ref var mesh = ref brushMesh.Value;
            for (int p = 0; p < mesh.polygons.Length && p < mesh.localPlanes.Length; p++)
            {
                if (math.abs(mesh.localPlanes[p].x) < 0.5f)
                    continue;
                ref var polygon = ref mesh.polygons[p];
                ref var uv0 = ref polygon.surface.details.UV0;
                var u = uv0.U;
                uv0.U = uv0.V;
                uv0.V = -u;

                // how v changes going up, in the brush's space: the plane space's axes are the rows of its rotation
                var toPlane  = MathExtensions.GenerateLocalToPlaneSpaceMatrix(mesh.localPlanes[p]);
                var rotation = new float3x3(toPlane);
                var upInPlane = math.mul(rotation, new float3(0, 1, 0));
                var turnedV = (float4)uv0.V;
                if (math.dot(turnedV.xyz, upInPlane) < 0)
                {
                    // a half turn more: still not mirrored
                    uv0.U = -uv0.U;
                    uv0.V = -uv0.V;
                }
            }
            return brushMesh;
        }

        static BlobAssetReference<BrushMeshBlob> CreateExtrudedSubMeshBlob([ReadOnly] NativeArray<float3> vertices, float3 extrusion, [ReadOnly] NativeArray<int> indices,
                                                                           StairsPart part,
                                                                           ref InternalChiselSurfaceArray surfaceDefinition, Allocator allocator = Allocator.Persistent)// Indirect
		{
			using var builder = new BlobBuilder(Allocator.Temp);
			ref var root = ref builder.ConstructRoot<BrushMeshBlob>();
			var arrays = BrushMeshFactory.CreateExtrudedSubMesh(vertices, extrusion, indices,
																ref surfaceDefinition,
																in builder, ref root);
			var polygons = arrays.polygons;
			var halfEdges = arrays.halfEdges;
			var localVertices = arrays.localVertices;
			if (localVertices.Length > 0)
			{
				// TODO: eventually remove when it's more battle tested
				if (!Validate(in localVertices, in halfEdges, in polygons, logErrors: true))
					return BlobAssetReference<BrushMeshBlob>.Null;

				var localPlanes = builder.Allocate(ref root.localPlanes, polygons.Length);
				root.localPlaneCount = polygons.Length;
				// TODO: calculate corner planes
				var halfEdgePolygonIndices = builder.Allocate(ref root.halfEdgePolygonIndices, halfEdges.Length);
				CalculatePlanes(ref localPlanes, in polygons, in halfEdges, in localVertices);
				// the steps go down towards +z, so a step's front faces +z
				for (int p = 0; p < polygons.Length; p++)
				{
					var normal = localPlanes[p].xyz;
					ref var polygon = ref polygons[p];
					var surface = StairsSurfaces.Classify(part, normal);
					polygon.descriptionIndex = (int)surface;
					polygon.surface          = surfaceDefinition.surfaces[(int)surface];
				}
				UpdateHalfEdgePolygonIndices(ref halfEdgePolygonIndices, in polygons);
				root.localBounds = CalculateBounds(in localVertices);
				return builder.CreateBlobAssetReference<BrushMeshBlob>(allocator); // Allocator.Persistent / Confirmed to dispose
			}
			else
				return BlobAssetReference<BrushMeshBlob>.Null;
		}

        // A wall that goes down beside the steps has its texture placed as theirs, so its front and back run on from
        // theirs where they meet
        static BlobAssetReference<BrushMeshBlob> TexturedAsTheSteps(BlobAssetReference<BrushMeshBlob> wall, in LineairStairsData description)
        {
            // its faces to the sides are turned with the rest of the side walls'
            return AlignedUpright(wall, new float3(description.bounds.Min.x + description.rightSideWidth, description.bounds.Max.y, 0),
                                  aroundOrigin: false, description.stepHeight);
        }

        // A corner of a wall's outline, left out where it's where the one before is (or, last, where the first is): a side
        // between the two would have no length, and its plane no direction
        static void AddCorner(NativeList<float3> corners, float3 corner)
        {
            const float kSame = 1e-5f;
            if (corners.Length > 0 && math.lengthsq(corners[corners.Length - 1] - corner) < kSame * kSame)
                return;
            corners.Add(corner);
        }

        // Triangles fanned out over all of an outline's corners, from its first; a last corner on the first is left out
        static NativeList<int> FanIndices(NativeList<float3> corners)
        {
            const float kSame = 1e-5f;
            if (corners.Length > 1 && math.lengthsq(corners[corners.Length - 1] - corners[0]) < kSame * kSame)
                corners.RemoveAt(corners.Length - 1);
            var indices = new NativeList<int>(math.max(0, corners.Length - 2) * 3, Allocator.Temp);
            for (int k = 1; k + 1 < corners.Length; k++)
            {
                indices.Add(0);
                indices.Add(k);
                indices.Add(k + 1);
            }
            return indices;
        }

        private static void GenerateStairsSide(NativeList<BlobAssetReference<BrushMeshBlob>> brushMeshes, int startIndex, int stepCount, float minX, float maxX, 
                                               StairsSideType sideType, in LineairStairsData description, ref InternalChiselSurfaceArray surfaceDefinition, 
                                               in LinearStairsSideData side, Allocator allocator = Allocator.Persistent)// Indirect
		{
            var min = new float3(minX, description.bounds.Max.y - description.treadHeight - description.stepHeight, description.bounds.Min.z + description.stepDepthOffset);
            var max = new float3(maxX, description.bounds.Max.y - description.treadHeight                         , description.bounds.Min.z + description.stepDepthOffset + description.stepDepth);

            // thick steps reach the front; a thin riser is a riser's depth behind it
            var maxZ = description.thickRiser ? description.bounds.Max.z : description.bounds.Max.z - description.riserDepth;

            var aspect = description.stepHeight / description.stepDepth;

            if (sideType == StairsSideType.DownAndUp)
            {
                var extrusion = new float3(max.x - min.x, 0, 0);
                // Top "wall"
                if (LinearStairsSideData.HasTopWall(description.stepDepthOffset, description.sideDepth, description.riserType, description.topSideLength))
                {
                    // z0 y0 *----* z1 y0
                    //       |    |
                    // z0 y1 *----* z1 y2
                    var y0 = max.y + description.sideHeight;
                    var y1 = max.y;
                    var y2 = min.y;
                    // a thick top floor goes down to y2 all the way to the first step's front: so does its wall, which
                    // overlaps the wall under the steps there
                    var z1 = description.thickRiser ? min.z : min.z - description.sideDepth;
                    // back to the back of the stairs, or as far back from the top step as set
                    var z0 = math.min(math.max(description.bounds.Min.z, min.z - description.topSideLength), z1 - kMinTopSide);
                    var bottom = description.thickRiser ? y2 : y1;

                    NativeArray<float3> vertices;
					using var _vertices = vertices = new NativeArray<float3>(4, Allocator.Temp);
                    vertices[0] = new float3(min.x, y0,     z0); // 0
                    vertices[1] = new float3(min.x, bottom, z0); // 1
                    vertices[2] = new float3(min.x, bottom, z1); // 2
                    vertices[3] = new float3(min.x, y0,     z1); // 3

                    NativeArray<int> indices;
					using var _indices = indices = new NativeArray<int>(6, Allocator.Temp);
                    indices[0] = 0; 
                    indices[1] = 1; 
                    indices[2] = 2;
                    indices[3] = 0; 
                    indices[4] = 2; 
                    indices[5] = 3;

                    brushMeshes[startIndex] = TexturedAsTheSteps(CreateExtrudedSubMeshBlob(vertices, extrusion, indices, StairsPart.Wall, ref surfaceDefinition, allocator), in description);// Indirect
				} else
                    startIndex--;

                // "wall" on stair steps
                if (description.sideDepth <= 0)
                {
                    var y1 = max.y;
                    var y0 = y1 + description.sideHeight;
                    var y2 = min.y - (description.stepHeight * (stepCount - 1));
                    var y3 = y2 + description.sideHeight;
                    var z0 = min.z;
                    var z1 = max.z + (description.stepDepth * (stepCount - 1));

                    // z0 y0 *
                    //       |\
                    //       | \
                    //       |  \
                    //       |   \
                    //       |    \
                    //       |     \
                    // z0 y1 *      * z1 y3 
                    //        \     |
                    //         \    |
                    //          \   |
                    //           \  |
                    //            \ |
                    //             \|
                    //              * z1 y2 

                    NativeArray<float3> vertices;
					using var _vertices = vertices = new NativeArray<float3>(4, Allocator.Temp);
                    vertices[0] = new float3(min.x, y0, z0); // 0
                    vertices[1] = new float3(min.x, y1, z0); // 1
                    vertices[2] = new float3(min.x, y2, z1); // 2
                    vertices[3] = new float3(min.x, y3, z1); // 3

                    NativeArray<int> indices;
					using var _indices = indices = new NativeArray<int>(6, Allocator.Temp);
                    indices[0] = 0;
                    indices[1] = 1;
                    indices[2] = 2;
                    indices[3] = 0;
                    indices[4] = 2;
                    indices[5] = 3;

                    brushMeshes[startIndex + 1] = TexturedAsTheSteps(CreateExtrudedSubMeshBlob(vertices, extrusion, indices, StairsPart.Wall, ref surfaceDefinition, allocator), in description);// Indirect
				} else
                {
                    var y1 = max.y;
                    var y0 = y1 + description.sideHeight;
                    var y2 = description.bounds.Min.y;
                    var y4 = min.y - (description.stepHeight * (stepCount - 1));
                    var y3 = y4 + description.sideHeight;
                    var z0 = min.z;
                    var z1 = max.z + (description.stepDepth * (stepCount - 1));
                    var z2 = z0 - description.sideDepth;
                    var z3 = z1 - description.sideDepth - ((y2- y4) / aspect);

                    if (z2 < description.bounds.Min.z)
                    {
                        y1 -= (description.bounds.Min.z - z2) * aspect;
                        z2 = description.bounds.Min.z;
                    }
                    if (y1 < description.bounds.Min.y)
                    {
                        // y0 z2 *---* y0 z0 
                        //       |    \
                        //       |     \
                        //       |      \
                        //       |       \
                        //       |        \
                        //       |         \
                        //       |          * y3 z1 
                        //       |          |
                        //       |          |
                        //       |          |
                        //       |          |
                        //       |          |
                        //       |          |
                        // y2 z2 *----------* y2 z1 

                        NativeArray<float3> vertices;
						using var _vertices = vertices = new NativeArray<float3>(5, Allocator.Temp);
                        vertices[0] = new float3(min.x, y0, z2); // 0
                        vertices[1] = new float3(min.x, y2, z2); // 1
                        vertices[2] = new float3(min.x, y2, z1); // 2
                        vertices[3] = new float3(min.x, y3, z1); // 3
                        vertices[4] = new float3(min.x, y0, z0); // 4

                        NativeArray<int> indices;
						using var _indices = indices = new NativeArray<int>(9, Allocator.Temp);
                        indices[0] = 0;
                        indices[1] = 1;
                        indices[2] = 2;
                        indices[3] = 0;
                        indices[4] = 2;
                        indices[5] = 3;
                        indices[6] = 0;
                        indices[7] = 3;
                        indices[8] = 4; // TODO: fix this
                                
                        brushMeshes[startIndex + 1] = TexturedAsTheSteps(CreateExtrudedSubMeshBlob(vertices, extrusion, indices, StairsPart.Wall, ref surfaceDefinition, allocator), in description);// Indirect
					} else
                    if (z3 > maxZ)
                    {
                        var z4 = maxZ;
                        var y5 = y2 + ((z3 - z4) * aspect);
                        var y6 = y3 + ((z1 - z4) * aspect);
                        // y0 z2 *---* y0 z0 
                        //       |    \
                        //       |     \
                        // y1 z2 *      * y6 z4 
                        //        \     | 
                        //         \    |
                        //          \   |
                        //           \  |
                        //            \ |
                        //             \|
                        //              * y5 z4 

                        {
                            NativeArray<float3> vertices;
							using var _vertices = vertices = new NativeArray<float3>(5, Allocator.Temp);
                            vertices[0] = new float3(min.x, y0, z2); // 0
                            vertices[1] = new float3(min.x, y1, z2); // 1
                            vertices[2] = new float3(min.x, y5, z4); // 2
                            vertices[3] = new float3(min.x, y6, z4); // 3
                            vertices[4] = new float3(min.x, y0, z0); // 4

                            NativeArray<int> indices;
							using var _indices = indices = new NativeArray<int>(9, Allocator.Temp);
                            indices[0] = 0;
                            indices[1] = 1;
                            indices[2] = 2;
                            indices[3] = 0;
                            indices[4] = 2;
                            indices[5] = 3;
                            indices[6] = 0;
                            indices[7] = 3;
                            indices[8] = 4;
                                    
                            brushMeshes[startIndex + 1] = TexturedAsTheSteps(CreateExtrudedSubMeshBlob(vertices, extrusion, indices, StairsPart.Wall, ref surfaceDefinition, allocator), in description);// Indirect
						}

                        if ((description.sideDepth * aspect) < (description.plateauHeight - description.treadHeight))
                        {
                            var y7 = description.bounds.Min.y;
                            // y6 z4 *
                            //       |\
                            //       | \
                            //       |  \
                            //       |   \
                            //       |    * y3 z1 
                            //       |    |
                            // y7 z4 *----* y7 z1 

                            NativeArray<float3> vertices;
							using var _vertices = vertices = new NativeArray<float3>(4, Allocator.Temp);
                            vertices[0] = new float3(min.x, y6, z4); // 0
                            vertices[1] = new float3(min.x, y7, z4); // 1
                            vertices[2] = new float3(min.x, y7, z1); // 2
                            vertices[3] = new float3(min.x, y3, z1); // 3

                            NativeArray<int> indices;
							using var _indices = indices = new NativeArray<int>(6, Allocator.Temp);
                            indices[0] = 0;
                            indices[1] = 1;
                            indices[2] = 2;
                            indices[3] = 0;
                            indices[4] = 2;
                            indices[5] = 3;

                            brushMeshes[startIndex + 2] = TexturedAsTheSteps(CreateExtrudedSubMeshBlob(vertices, extrusion, indices, StairsPart.Wall, ref surfaceDefinition, allocator), in description);// Indirect
						}
                    } else
                    {
                        // y0 z2 *---* y0 z0 
                        //       |    \
                        //       |     \
                        //       |      \
                        //       |       \
                        //       |        \
                        //       |         \
                        // y1 z2 *          * y3 z1 
                        //        \         |
                        //         \        |
                        //          \       |
                        //           \      |
                        //            \     |
                        //             \    |
                        //        y2 z3 *---* y2 z1 

                        using var vertices = new NativeList<float3>(6, Allocator.Temp);
                        AddCorner(vertices, new float3(min.x, y0, z2));
                        AddCorner(vertices, new float3(min.x, y1, z2));
                        AddCorner(vertices, new float3(min.x, y2, z3));
                        AddCorner(vertices, new float3(min.x, y2, z1));
                        AddCorner(vertices, new float3(min.x, y3, z1));
                        AddCorner(vertices, new float3(min.x, y0, z0));
                        using var indices = FanIndices(vertices);

                        brushMeshes[startIndex + 1] = TexturedAsTheSteps(CreateExtrudedSubMeshBlob(vertices.AsArray(), extrusion, indices.AsArray(), StairsPart.Wall, ref surfaceDefinition, allocator), in description);// Indirect
					}
                }

                // Thick steps on a base: the bottom step is solid down to the floor all along it, which the wall beside the
                // steps, only dropping along their slope, doesn't cover. Its piece beside the base is all of the base's side
                // (in the place the count of brushes keeps for it)
                if (description.thickRiser && description.sideDepth > 0 &&
                    (description.bounds.Max.y - description.treadHeight) >= description.bounds.Min.y &&
                    (description.sideDepth * aspect) < (description.plateauHeight - description.treadHeight))
                {
                    var floor = description.bounds.Min.y;
                    // up to the bottom step's top, under its tread
                    var top   = floor + description.plateauHeight + description.stepHeight - description.treadHeight;
                    var front = description.bounds.Max.z;
                    var back  = math.max(description.bounds.Min.z, description.bounds.Min.z + description.stepDepthOffset + (description.stepDepth * (stepCount - 2)));

                    // back top *----* front top
                    //          |    |
                    // back     *----* front floor
                    NativeArray<float3> vertices;
					using var _vertices = vertices = new NativeArray<float3>(4, Allocator.Temp);
                    vertices[0] = new float3(min.x, top,   back);
                    vertices[1] = new float3(min.x, floor, back);
                    vertices[2] = new float3(min.x, floor, front);
                    vertices[3] = new float3(min.x, top,   front);

                    NativeArray<int> indices;
					using var _indices = indices = new NativeArray<int>(6, Allocator.Temp);
                    indices[0] = 0; indices[1] = 1; indices[2] = 2;
                    indices[3] = 0; indices[4] = 2; indices[5] = 3;

                    if (brushMeshes[startIndex + 2].IsCreated)
                        brushMeshes[startIndex + 2].Dispose();
                    // textured as the steps beside it, so its front runs on from the base's
                    brushMeshes[startIndex + 2] = TexturedAsTheSteps(CreateExtrudedSubMeshBlob(vertices, extrusion, indices, StairsPart.Wall, ref surfaceDefinition, allocator), in description);// Indirect
                }
            } else
            if (sideType == StairsSideType.Up)
            {
                var extrusion = new float3(max.x - min.x, 0, 0);
                // Top "wall" (filled down, the wall on the steps reaches back over it too)
                if ((min.z - description.bounds.Min.z) > 0 && description.riserType != StairsRiserType.FillDown && description.topSideLength > 0)
                {
                    // z0 y0 *----* z1 y0
                    //       |    |
                    // z0 y1 *----* z1 y2 
                    var y0 = max.y + description.sideHeight;
                    var y1 = max.y;
                    var y2 = min.y;
                    var z1 = min.z;
                    // back to the back of the stairs, or as far back from the top step as set
                    var z0 = math.max(description.bounds.Min.z, z1 - description.topSideLength);
                    NativeArray<float3> vertices;
					using var _vertices = vertices = new NativeArray<float3>(4, Allocator.Temp);
                    vertices[0] = new float3(min.x, y0, z0); // 0
                    vertices[1] = new float3(min.x, y1, z0); // 1
                    vertices[2] = new float3(min.x, y1, z1); // 2
                    vertices[3] = new float3(min.x, y0, z1); // 3

                    NativeArray<int> indices;
					using var _indices = indices = new NativeArray<int>(6, Allocator.Temp);
                    indices[0] = 0;
                    indices[1] = 1;
                    indices[2] = 2;
                    indices[3] = 0;
                    indices[4] = 2;
                    indices[5] = 3;

                    brushMeshes[startIndex] = CreateExtrudedSubMeshBlob(vertices, extrusion, indices, StairsPart.Wall, ref surfaceDefinition, allocator);// Indirect
				} else
                    startIndex--;
                // "wall" on stair steps: steps solid down to the floor have a wall down to the floor, in place of the solid
                // under them on this side, so its outside is one face: from the floor up to its height above the steps
                if (description.riserType == StairsRiserType.FillDown)
                {
                    var floor   = description.bounds.Min.y;
                    var back    = description.bounds.Min.z;
                    var lastTop = max.y - description.stepHeight * (stepCount - 1);
                    var z0 = min.z;
                    var z1 = max.z + description.stepDepth * (stepCount - 1);
                    var y0 = max.y + description.sideHeight;
                    var y3 = math.max(floor + description.sideHeight, lastTop - description.stepHeight + description.sideHeight);

                    // back y0 *--* z0 y0
                    //         |    \
                    //         |      * z1 y3
                    //         |      |
                    //         *------* floor

                    using var vertices = new NativeList<float3>(5, Allocator.Temp);
                    vertices.Add(new float3(min.x, y0, back));
                    vertices.Add(new float3(min.x, floor, back));
                    vertices.Add(new float3(min.x, floor, z1));
                    vertices.Add(new float3(min.x, y3, z1));
                    if (z0 > back + 1e-5f)
                        vertices.Add(new float3(min.x, y0, z0));

                    using var indices = new NativeList<int>(9, Allocator.Temp);
                    for (int k = 1; k + 1 < vertices.Length; k++)
                    {
                        indices.Add(0);
                        indices.Add(k);
                        indices.Add(k + 1);
                    }

                    // its back and front have their texture from the floor up and across from the stairs' side, as the
                    // solid between the walls
                    brushMeshes[startIndex + 1] = TurnSideFaces(AlignedUpright(CreateExtrudedSubMeshBlob(vertices.AsArray(), extrusion, indices.AsArray(), StairsPart.Wall, ref surfaceDefinition, allocator),
                                                                               new float3(description.bounds.Min.x, floor, 0), aroundOrigin: false, 0));// Indirect
                } else
                for (int i = 0, j = startIndex + 1; i < stepCount; i++, j++)
                {
                    var y0 = max.y + description.sideHeight;
                    var y1 = max.y;
                    var y2 = min.y;
                    var y3 = min.y + description.sideHeight;
                    var z0 = min.z;
                    var z1 = max.z;

                    // z0 y0 *
                    //       |\
                    //       | \
                    //       |  \
                    //       |   \
                    //       |    * z1 y3 
                    //       |    |
                    // z0 y1 *----* z1 y2 

                    NativeArray<float3> vertices;
					using var _vertices = vertices = new NativeArray<float3>(4, Allocator.Temp);
                    vertices[0] = new float3(min.x, y0, z0); // 0
                    vertices[1] = new float3(min.x, y1, z0); // 1
                    vertices[2] = new float3(min.x, y1, z1); // 2
                    vertices[3] = new float3(min.x, y3, z1); // 3

                    NativeArray<int> indices;
					using var _indices = indices = new NativeArray<int>(6, Allocator.Temp);
                    indices[0] = 0;
                    indices[1] = 1;
                    indices[2] = 2;
                    indices[3] = 0;
                    indices[4] = 2;
                    indices[5] = 3;
                    
                    brushMeshes[j] = CreateExtrudedSubMeshBlob(vertices, extrusion, indices, StairsPart.Wall, ref surfaceDefinition, allocator);// Indirect

					min.z += description.stepDepth;
                    max.z += description.stepDepth;
                    min.y -= description.stepHeight;
                    max.y -= description.stepHeight;
                }
            } else
            if (sideType == StairsSideType.Down)
            {
                var extrusion = new float3(max.x - min.x, 0, 0);
                if (description.sideDepth == 0)
                {
                    for (int i = 0, j = startIndex; i < stepCount - 1; i++, j++)
                    {
                        var y0 = max.y;
                        var y1 = min.y;
                        var z0 = min.z;
                        var z1 = max.z;

                        // z0 y0 *------* z1 y0 
                        //        \     |
                        //         \    |
                        //          \   |
                        //           \  |
                        //            \ |
                        //             \|
                        //              * z1 y1 
                        NativeArray<float3> vertices;
						using var _vertices = vertices = new NativeArray<float3>(3, Allocator.Temp);
                        vertices[0] = new float3(min.x, y0, z0); // 0
                        vertices[1] = new float3(min.x, y1, z1); // 1
                        vertices[2] = new float3(min.x, y0, z1); // 2

                        NativeArray<int> indices;
						using var _indices = indices = new NativeArray<int>(6, Allocator.Temp);
                        indices[0] = 0;
                        indices[1] = 1;
                        indices[2] = 2;
                        indices[3] = 0;
                        indices[4] = 2;
                        indices[5] = 3;
                        
                        brushMeshes[j] = CreateExtrudedSubMeshBlob(vertices, extrusion, indices, StairsPart.Wall, ref surfaceDefinition, allocator);// Indirect

						min.z += description.stepDepth;
                        max.z += description.stepDepth;
                        min.y -= description.stepHeight;
                        max.y -= description.stepHeight;
                    }
                } else
                {
                    {
                        var y0 = max.y;
                        var y1 = min.y - (description.sideDepth * aspect);
                        var z0 = min.z - description.sideDepth;
                        var z1 = max.z;
                        if (y1 < description.bounds.Min.y)
                        {
                            var y2 = description.bounds.Min.y;
                            var z2 = max.z - ((description.bounds.Min.y - y1) / aspect);
                            if (z2 < description.bounds.Min.z)
                            {
                                var z3 = description.bounds.Min.z;
                                // z3 y0 *---------* z1 y0 
                                //       |         |
                                //       |         |
                                //       |         |
                                //       |         |
                                //       |         |
                                //       |         |
                                // z3 y2 *---------* z1 y2 
                                NativeArray<float3> vertices;
								using var _vertices = vertices = new NativeArray<float3>(4, Allocator.Temp);
                                vertices[0] = new float3(min.x, y0, z3); // 0
                                vertices[1] = new float3(min.x, y2, z3); // 1
                                vertices[2] = new float3(min.x, y2, z1); // 2
                                vertices[3] = new float3(min.x, y0, z1); // 3

                                NativeArray<int> indices;
								using var _indices = indices = new NativeArray<int>(6, Allocator.Temp);
                                indices[0] = 0;
                                indices[1] = 1;
                                indices[2] = 2;
                                indices[3] = 0;
                                indices[4] = 2;
                                indices[5] = 3;
                                
                                brushMeshes[startIndex] = CreateExtrudedSubMeshBlob(vertices, extrusion, indices, StairsPart.Wall, ref surfaceDefinition, allocator);// Indirect

								min.z += description.stepDepth;
                                max.z += description.stepDepth;
                                min.y -= description.stepHeight;
                                max.y -= description.stepHeight;
                            } else
                            if (z0 < description.bounds.Min.z)
                            {
                                var z3 = description.bounds.Min.z;
                                var y3 = max.y - ((description.bounds.Min.z - z0) * aspect);
                                // z3 y0 *---------* z1 y0 
                                //       |         |
                                //       |         |
                                //       |         |
                                // z3 y3 *         |
                                //        \        |
                                //         \       |
                                //          \      |
                                //           \     |
                                //            \    |
                                //             \   |
                                //        y2 z2 *--* z1 y2
                                NativeArray<float3> vertices;
								using var _vertices = vertices = new NativeArray<float3>(5, Allocator.Temp);
                                vertices[0] = new float3(min.x, y0, z3); // 0
                                vertices[1] = new float3(min.x, y3, z3); // 1
                                vertices[2] = new float3(min.x, y2, z2); // 2
                                vertices[3] = new float3(min.x, y2, z1); // 3
                                vertices[4] = new float3(min.x, y0, z1); // 4

                                NativeArray<int> indices;
								using var _indices = indices = new NativeArray<int>(6, Allocator.Temp);
                                indices[0] = 0;
                                indices[1] = 1;
                                indices[2] = 2;
                                indices[3] = 0;
                                indices[4] = 2;
                                indices[5] = 3;
                                
                                brushMeshes[startIndex] = CreateExtrudedSubMeshBlob(vertices, extrusion, indices, StairsPart.Wall, ref surfaceDefinition, allocator);// Indirect

								min.z += description.stepDepth;
                                max.z += description.stepDepth;
                                min.y -= description.stepHeight;
                                max.y -= description.stepHeight;
                            } else
                            {
                                // z0 y0 *---------* z1 y0 
                                //        \        |
                                //         \       |
                                //          \      |
                                //           \     |
                                //            \    |
                                //             \   |
                                //        z2 y2 *--* z1 y2 
                                NativeArray<float3> vertices;
								using var _vertices = vertices = new NativeArray<float3>(4, Allocator.Temp);
                                vertices[0] = new float3(min.x, y0, z0); // 0
                                vertices[1] = new float3(min.x, y2, z2); // 1
                                vertices[2] = new float3(min.x, y2, z1); // 2
                                vertices[3] = new float3(min.x, y0, z1); // 3

                                NativeArray<int> indices;
								using var _indices = indices = new NativeArray<int>(6, Allocator.Temp);
                                indices[0] = 0;
                                indices[1] = 1;
                                indices[2] = 2;
                                indices[3] = 0;
                                indices[4] = 2;
                                indices[5] = 3;

                                brushMeshes[startIndex] = CreateExtrudedSubMeshBlob(vertices, extrusion, indices, StairsPart.Wall, ref surfaceDefinition, allocator);// Indirect

								min.z += description.stepDepth;
                                max.z += description.stepDepth;
                                min.y -= description.stepHeight;
                                max.y -= description.stepHeight;
                            }
                        } else
                        if (z0 < description.bounds.Min.z)
                        {
                            var z2 = description.bounds.Min.z;
                            var y2 = max.y - ((description.bounds.Min.z - z0) * aspect);
                            // z2 y0 *------* z1 y0 
                            //       |      |
                            //       |      |
                            //       |      |
                            // z2 y2 *      |
                            //        \     |
                            //         \    |
                            //          \   |
                            //           \  |
                            //            \ |
                            //             \|
                            //              * z1 y1 
                            NativeArray<float3> vertices;
							using var _vertices = vertices = new NativeArray<float3>(4, Allocator.Temp);
                            vertices[0] = new float3(min.x, y0, z2); // 0
                            vertices[1] = new float3(min.x, y2, z2); // 1
                            vertices[2] = new float3(min.x, y1, z1); // 2
                            vertices[3] = new float3(min.x, y0, z1); // 3

                            NativeArray<int> indices;
                            using var _indices = indices = new NativeArray<int>(6, Allocator.Temp);
                            indices[0] = 0;
                            indices[1] = 1;
                            indices[2] = 2;
                            indices[3] = 0;
                            indices[4] = 2;
                            indices[5] = 3;
                            
                            brushMeshes[startIndex] = CreateExtrudedSubMeshBlob(vertices, extrusion, indices, StairsPart.Wall, ref surfaceDefinition, allocator);// Indirect

							min.z += description.stepDepth;
                            max.z += description.stepDepth;
                            min.y -= description.stepHeight;
                            max.y -= description.stepHeight;
                        } else
                        {
                            // z0 y0 *------* z1 y0 
                            //        \     |
                            //         \    |
                            //          \   |
                            //           \  |
                            //            \ |
                            //             \|
                            //              * z1 y1 
                            NativeArray<float3> vertices;
							using var _vertices = vertices = new NativeArray<float3>(3, Allocator.Temp);
                            vertices[0] = new float3(min.x, y0, z0); // 0
                            vertices[1] = new float3(min.x, y1, z1); // 1
                            vertices[2] = new float3(min.x, y0, z1); // 2

                            NativeArray<int> indices;
							using var _indices = indices = new NativeArray<int>(6, Allocator.Temp);
                            indices[0] = 0;
                            indices[1] = 1;
                            indices[2] = 2;
                            indices[3] = 0;
                            indices[4] = 2;
                            indices[5] = 3;
                            
                            brushMeshes[startIndex] = CreateExtrudedSubMeshBlob(vertices, extrusion, indices, StairsPart.Wall, ref surfaceDefinition, allocator);// Indirect

							min.z += description.stepDepth;
                            max.z += description.stepDepth;
                            min.y -= description.stepHeight;
                            max.y -= description.stepHeight;
                        }
                    }
                    for (int i = 1, j = startIndex + 1; i < stepCount - 1; i++, j++)
                    {
                        var y0 = max.y;
                        var y1 = min.y - (description.sideDepth * aspect);
                        var y2 = max.y - (description.sideDepth * aspect);
                        var z0 = min.z;
                        var z1 = max.z;

                        if (y2 < description.bounds.Min.y)
                        {
                            var y3 = description.bounds.Min.y;
                            // z0 y0 *------* z1 y0 
                            //       |      |
                            //       |      |
                            //       |      |
                            // z0 y3 *------* z1 y3 
                            NativeArray<float3> vertices;
							using var _vertices = vertices = new NativeArray<float3>(4, Allocator.Temp);
                            vertices[0] = new float3(min.x, y0, z0); // 0
                            vertices[1] = new float3(min.x, y3, z0); // 1
                            vertices[2] = new float3(min.x, y3, z1); // 2
                            vertices[3] = new float3(min.x, y0, z1); // 3

                            NativeArray<int> indices;
							using var _indices = indices = new NativeArray<int>(6, Allocator.Temp);
                            indices[0] = 0;
                            indices[1] = 1;
                            indices[2] = 2;
                            indices[3] = 0;
                            indices[4] = 2;
                            indices[5] = 3;
                            
                            brushMeshes[j] = CreateExtrudedSubMeshBlob(vertices, extrusion, indices, StairsPart.Wall, ref surfaceDefinition, allocator);// Indirect
						} else
                        if (y1 < description.bounds.Min.y)
                        {
                            var z2 = max.z - ((description.bounds.Min.y - y1) / aspect);
                            var y3 = description.bounds.Min.y;
                            // z0 y0 *---------* z1 y0 
                            //       |         |
                            //       |         |
                            //       |         |
                            // z0 y2 *         |
                            //        \        |
                            //         \       |
                            //          \      |
                            //           \     |
                            //            \    |
                            //             \   |
                            //        y3 z2 *--* z1 y3
                            NativeArray<float3> vertices;
							using var _vertices = vertices = new NativeArray<float3>(5, Allocator.Temp);
                            vertices[0] = new float3(min.x, y0, z0); // 0
                            vertices[1] = new float3(min.x, y2, z0); // 1
                            vertices[2] = new float3(min.x, y3, z2); // 2
                            vertices[3] = new float3(min.x, y3, z1); // 3
                            vertices[4] = new float3(min.x, y0, z1); // 4

                            NativeArray<int> indices;
							using var _indices = indices = new NativeArray<int>(6, Allocator.Temp);
                            indices[0] = 0;
                            indices[1] = 1;
                            indices[2] = 2;
                            indices[3] = 0;
                            indices[4] = 2;
                            indices[5] = 3;

                            brushMeshes[j] = CreateExtrudedSubMeshBlob(vertices, extrusion, indices, StairsPart.Wall, ref surfaceDefinition, allocator);// Indirect
						} else
                        {
                            // z0 y0 *------* z1 y0 
                            //       |      |
                            //       |      |
                            //       |      |
                            // z0 y2 *      |
                            //        \     |
                            //         \    |
                            //          \   |
                            //           \  |
                            //            \ |
                            //             \|
                            //              * z1 y1 
                            NativeArray<float3> vertices;
							using var _vertices = vertices = new NativeArray<float3>(4, Allocator.Temp);
                            vertices[0] = new float3(min.x, y0, z0); // 0
                            vertices[1] = new float3(min.x, y2, z0); // 1
                            vertices[2] = new float3(min.x, y1, z1); // 2
                            vertices[3] = new float3(min.x, y0, z1); // 3

                            NativeArray<int> indices;
							using var _indices = indices = new NativeArray<int>(6, Allocator.Temp);
                            indices[0] = 0;
                            indices[1] = 1;
                            indices[2] = 2;
                            indices[3] = 0;
                            indices[4] = 2;
                            indices[5] = 3;

                            brushMeshes[j] = CreateExtrudedSubMeshBlob(vertices, extrusion, indices, StairsPart.Wall, ref surfaceDefinition, allocator);// Indirect
						}

                        min.z += description.stepDepth;
                        max.z += description.stepDepth;
                        min.y -= description.stepHeight;
                        max.y -= description.stepHeight;
                    }
                    if (description.riserType == StairsRiserType.ThinRiser &&
                        description.riserDepth > description.stepDepth &&
                        description.sideDepth < (description.plateauHeight - description.treadHeight) / aspect &&
                        description.riserDepth < description.absDepth)
                    {
                        var z0 = description.bounds.Max.z - description.riserDepth;
                        var z1 = min.z;
                        var y1 = max.y - (description.sideDepth * aspect) + ((min.z - z0) * aspect);
                        var y2 = max.y - (description.sideDepth * aspect);
                        var y3 = description.bounds.Min.y;
                        if (y2 <= y3)
                        {
                            if (y2 < y3)
                            {
                                z1 -= (y3 - y2) / aspect;
                            }
                            // y1 z0 *
                            //       |\  
                            //       | \ 
                            //       |  \
                            // y3 z0 *---* y3 z1 
                            NativeArray<float3> vertices;
							using var _vertices = vertices = new NativeArray<float3>(3, Allocator.Temp);
                            vertices[0] = new float3(min.x, y1, z0); // 0
                            vertices[1] = new float3(min.x, y3, z0); // 1
                            vertices[2] = new float3(min.x, y3, z1); // 2

                            NativeArray<int> indices;
							using var _indices = indices = new NativeArray<int>(6, Allocator.Temp);
                            indices[0] = 0;
                            indices[1] = 1;
                            indices[2] = 2;
                            indices[3] = 0;
                            indices[4] = 2;
                            indices[5] = 3;

                            brushMeshes[startIndex + stepCount] = CreateExtrudedSubMeshBlob(vertices, extrusion, indices, StairsPart.Wall, ref surfaceDefinition, allocator);// Indirect
						} else
                        {
                            // y1 z0 *
                            //       |\  
                            //       | \ 
                            //       |  \
                            //       |   * y2 z1 
                            //       |   |
                            // y3 z0 *---* y3 z1 
                            NativeArray<float3> vertices;
							using var _vertices = vertices = new NativeArray<float3>(4, Allocator.Temp);
                            vertices[0] = new float3(min.x, y1, z0); // 0
                            vertices[1] = new float3(min.x, y3, z0); // 1
                            vertices[2] = new float3(min.x, y3, z1); // 2
                            vertices[3] = new float3(min.x, y2, z1); // 3

                            NativeArray<int> indices;
							using var _indices = indices = new NativeArray<int>(6, Allocator.Temp);
                            indices[0] = 0;
                            indices[1] = 1;
                            indices[2] = 2;
                            indices[3] = 0;
                            indices[4] = 2;
                            indices[5] = 3;
                            
                            brushMeshes[startIndex + stepCount] = CreateExtrudedSubMeshBlob(vertices, extrusion, indices, StairsPart.Wall, ref surfaceDefinition, allocator);// Indirect
						}
                    }
                }
                {
                    var y0 = max.y;
                    var z0 = min.z;
                    var z1 = max.z;

                    var y3 = description.bounds.Min.y;
					// z0 y0 *------* z1 y0 
					//       |      |
					//       |      |
					//       |      |
					// z0 y3 *------* z1 y3 
					NativeArray<float3> vertices;
					using var _vertices = vertices = new NativeArray<float3>(4, Allocator.Temp);
                    vertices[0] = new float3(min.x, y0, z0); // 0
                    vertices[1] = new float3(min.x, y3, z0); // 1
                    vertices[2] = new float3(min.x, y3, z1); // 2
                    vertices[3] = new float3(min.x, y0, z1); // 3

					NativeArray<int> indices;
					using var _indices = indices = new NativeArray<int>(6, Allocator.Temp);
                    indices[0] = 0;
                    indices[1] = 1;
                    indices[2] = 2;
                    indices[3] = 0;
                    indices[4] = 2;
                    indices[5] = 3;

                    brushMeshes[startIndex + stepCount - 1] = CreateExtrudedSubMeshBlob(vertices, extrusion, indices, StairsPart.Wall, ref surfaceDefinition, allocator);// Indirect
				}
            }
        }

        internal struct LinearStairsSideData
        {
            public bool enabled;        // TODO: just check if subMeshCount == 0
            public int  subMeshCount;

            // A wall going down beside the top floor has a piece of its own there when the top floor reaches further back
            // than the wall's depth, or when the top floor is thick: the wall under the steps only drops along their
            // slope from the tread, so it doesn't cover the top floor's side down to its underside
            public static bool HasTopWall(float stepDepthOffset, float sideDepth, StairsRiserType riserType, float topSideLength)
            {
                if (topSideLength <= 0)
                    return false;
                var thickTop = riserType == StairsRiserType.ThickRiser || riserType == StairsRiserType.Smooth;
                return stepDepthOffset > sideDepth || (thickTop && stepDepthOffset > 0);
            }

            // TODO: just turn into method that returns subMeshCount
            public LinearStairsSideData(float stepHeight,
                                        float stepDepth,
                                        float plateauHeight,
                                        float treadHeight,
                                        float absDepth,
                                        float stepDepthOffset,
                                        int stepCount, float sideDepth, MinMaxAABB bounds, StairsRiserType riserType, float riserDepth, StairsSideType sideType,
                                        float topSideLength)
            {
                this.enabled = sideType != StairsSideType.None;

                if (!enabled)
                {
                    this.subMeshCount = 0;
                    return;
                }

                if (sideType == StairsSideType.Down)
                {
                    this.subMeshCount = stepCount;

                    var aspect = stepHeight / stepDepth;
                    if (riserType == StairsRiserType.ThinRiser &&
                        riserDepth > stepDepth && 
                        sideDepth < (plateauHeight - treadHeight) / aspect &&
                        riserDepth < absDepth)
                    {
                        this.subMeshCount++;
                    }
                } else
                if (sideType == StairsSideType.Up)
                {
                    // with steps solid down to the floor the wall on them is one piece, otherwise one per step
                    var onSteps = (riserType == StairsRiserType.FillDown) ? 1 : stepCount;
                    if (stepDepthOffset > 0 && riserType != StairsRiserType.FillDown && topSideLength > 0)
                        this.subMeshCount = onSteps + 1;
                    else
                        this.subMeshCount = onSteps;
                } else
                if (sideType == StairsSideType.DownAndUp)
                {
                    if (HasTopWall(stepDepthOffset, sideDepth, riserType, topSideLength))
                        this.subMeshCount = 2;
                    else
                        this.subMeshCount = 1;

                    if ((bounds.Max.y - treadHeight) >= bounds.Min.y)
                    {
                        var aspect = stepHeight / stepDepth;
                        if ((sideDepth * aspect) < (plateauHeight - treadHeight))
                            this.subMeshCount ++;
                    }
                } else
                    this.subMeshCount = 0;
            }
        }
    
        internal struct LineairStairsData
        {

            public const float kStepSmudgeValue = 0.0001f;

            public float treadHeight;

            public StairsRiserType riserType;
            public StairsSideType leftSideType;
            public StairsSideType rightSideType;

            public MinMaxAABB bounds;

            public bool haveRiser;

            public bool haveLeftSideDown;
            public bool haveRightSideDown;
            public bool haveLeftSideUp;
            public bool haveRightSideUp;

            public float sideWidth;
            // how wide each side wall is; both the side width, unless set apart
            public float leftSideWidth;
            public float rightSideWidth;
            public bool thickRiser;
            public float riserDepth;
            public float sideDepth;
            // how far the side walls reach out past the steps' fronts
            public float sideFrontOffset;
            // how far back from the top step the side walls go beside the top floor
            public float topSideLength;

            public int stepCount;
            public float offsetZ;
            public float offsetY;
            public float nosingDepth;

            public bool haveTread;
            public bool haveTopSide;

            public float leftNosingWidth;
            public float rightNosingWidth;

            public int subMeshCount;

            public int startTread;
            public int startLeftSide;
            public int startRightSide;
            public int startSideFronts;
            // the base under the lowest step is its own brush, with its own surface
            public bool haveBase;
            public int startBase;

            public float stepDepthOffset;
            public float stepHeight;
            public float stepDepth;
            public float sideHeight;
            public float plateauHeight;
            public float absDepth;

            public LinearStairsSideData leftSideDescription;
            public LinearStairsSideData rightSideDescription;

            public LineairStairsData(MinMaxAABB      bounds,
                                     float           stepHeight,
                                     float           stepDepth,
                                     float           treadHeight,
                                     float           nosingDepth,
                                     float           nosingWidth,
                                     float           plateauHeight,
                                     StairsRiserType riserType,
                                     float           riserDepth,
                                     StairsSideType  leftSide,
                                     StairsSideType  rightSide,
                                     float           sideWidth,
                                     float           sideHeight,
                                     float           sideDepth,
                                     bool            leftAsSet = false,
                                     bool            rightAsSet = false,
                                     float           sideFrontOffset = 0,
                                     float           topSideLength = float.PositiveInfinity)
            {
                // TODO: implement smooth riser-type

                const float kEpsilon = 0.001f;

                this.treadHeight     = (treadHeight < kEpsilon) ? 0 : treadHeight;
                this.riserType       = (this.treadHeight == 0 && riserType == StairsRiserType.ThinRiser) ? StairsRiserType.ThickRiser : riserType;
                this.leftSideType    = (this.riserType == StairsRiserType.None && leftSide  == StairsSideType.Up) ? StairsSideType.DownAndUp : leftSide;
                this.rightSideType   = (this.riserType == StairsRiserType.None && rightSide == StairsSideType.Up) ? StairsSideType.DownAndUp : rightSide;
                this.sideHeight      = sideHeight;
                this.plateauHeight   = plateauHeight;

                if (sideHeight <= 0)
                {
                    switch (this.leftSideType)
                    {
                        case StairsSideType.Up:         this.leftSideType = StairsSideType.None; break;
                        case StairsSideType.DownAndUp:  this.leftSideType = StairsSideType.Down; break;
                    }
                    switch (this.rightSideType)
                    {
                        case StairsSideType.Up:         this.rightSideType = StairsSideType.None; break;
                        case StairsSideType.DownAndUp:  this.rightSideType = StairsSideType.Down; break;
                    }
                }
                if (this.riserType == StairsRiserType.FillDown)
                {
                    switch (this.leftSideType)
                    {
                        case StairsSideType.Down:       this.leftSideType = StairsSideType.None; break;
                        case StairsSideType.DownAndUp:  this.leftSideType = StairsSideType.Up; break;
                    }
                    switch (this.rightSideType)
                    {
                        case StairsSideType.Down:       this.rightSideType = StairsSideType.None; break;
                        case StairsSideType.DownAndUp:  this.rightSideType = StairsSideType.Up; break;
                    }
                } else
                if (riserType == StairsRiserType.Smooth)
                {
                    // a guard rail goes down to the floor too, unless the sides are to be as set (pathed stairs: where
                    // parts meet they close each other, and their ends are as asked); without one the steps close
                    // their own ends
                    if (!leftAsSet && this.leftSideType == StairsSideType.Up)
                        this.leftSideType = StairsSideType.DownAndUp;
                    if (!rightAsSet && this.rightSideType == StairsSideType.Up)
                        this.rightSideType = StairsSideType.DownAndUp;
                }
                this.bounds              = bounds;

                this.haveRiser           = this.riserType != StairsRiserType.None;

                this.haveLeftSideDown    = this.riserType != StairsRiserType.FillDown && (this.leftSideType == StairsSideType.Down || this.leftSideType == StairsSideType.DownAndUp) && plateauHeight > 0;
                this.haveLeftSideUp      = (this.leftSideType == StairsSideType.Up || this.leftSideType == StairsSideType.DownAndUp);
                this.haveRightSideDown   = this.riserType != StairsRiserType.FillDown && (this.rightSideType == StairsSideType.Down || this.rightSideType == StairsSideType.DownAndUp) && plateauHeight > 0;
                this.haveRightSideUp     = (this.rightSideType == StairsSideType.Up || this.rightSideType == StairsSideType.DownAndUp);
                
                this.sideWidth           = sideWidth;
                this.leftSideWidth       = sideWidth;
                this.rightSideWidth      = sideWidth;
                this.thickRiser          = this.riserType == StairsRiserType.ThickRiser || this.riserType == StairsRiserType.Smooth;
                this.riserDepth          = (this.haveRiser && !this.thickRiser) ? riserDepth : 0;

                this.sideDepth           = this.riserDepth + math.max(sideDepth, this.thickRiser ? stepDepth : 0);
                this.sideFrontOffset     = math.max(0, sideFrontOffset);
                this.topSideLength       = math.max(0, topSideLength);
                
                var boundsSize           = bounds.Max - bounds.Min;
                var absWidth             = Mathf.Abs(boundsSize.x); 
                var absHeight            = Mathf.Abs(boundsSize.y);
                this.absDepth            = Mathf.Abs(boundsSize.z);

                this.stepCount           = Mathf.Max(1, Mathf.FloorToInt((absHeight - plateauHeight + kStepSmudgeValue) / stepHeight));
                this.stepDepthOffset     = Mathf.Max(0, this.absDepth - (this.stepCount * stepDepth));
                this.offsetZ             = (this.stepDepthOffset < kEpsilon) ? 0 : this.stepDepthOffset;
                this.offsetY             = plateauHeight;
                this.nosingDepth         = nosingDepth;
                this.stepHeight          = stepHeight;
                this.stepDepth           = stepDepth;

                this.haveTread           = (this.treadHeight >= kEpsilon);
                this.haveTopSide         = (sideHeight > kEpsilon);

                // filled down to the floor, the treads don't reach out past the sides of the solid under them
                var sideOverhang         = (this.riserType == StairsRiserType.FillDown) ? 0 : nosingWidth;
                this.leftNosingWidth     = haveLeftSideUp  ? -this.leftSideWidth : sideOverhang;
                this.rightNosingWidth    = haveRightSideUp ? -this.rightSideWidth : sideOverhang;



                this.leftSideDescription     = new LinearStairsSideData(stepHeight, stepDepth, plateauHeight, 
                                                                        this.treadHeight, this.absDepth, this.stepDepthOffset, 
                                                                        this.stepCount, this.sideDepth, this.bounds, this.riserType, this.riserDepth, this.leftSideType, this.topSideLength);
                this.rightSideDescription    = new LinearStairsSideData(stepHeight, stepDepth, plateauHeight,
                                                                        this.treadHeight, this.absDepth, this.stepDepthOffset, 
                                                                        this.stepCount, this.sideDepth, this.bounds, this.riserType, this.riserDepth, this.rightSideType, this.topSideLength);

                // filled down there is one more: the shape under all the steps
                this.subMeshCount        = 0; if (haveRiser) this.subMeshCount = this.stepCount + (this.riserType == StairsRiserType.FillDown ? 1 : 0);
                this.startTread          = this.subMeshCount; if (haveTread) this.subMeshCount += this.stepCount;

                this.startLeftSide       = this.subMeshCount; if (leftSideDescription.enabled)  this.subMeshCount += leftSideDescription.subMeshCount;
                this.startRightSide      = this.subMeshCount; if (rightSideDescription.enabled) this.subMeshCount += rightSideDescription.subMeshCount;
                // a piece in front of each side wall, when it reaches out past the steps' fronts
                this.startSideFronts     = this.subMeshCount;
                if (this.sideFrontOffset > 0)
                    this.subMeshCount += (leftSideDescription.enabled ? 1 : 0) + (rightSideDescription.enabled ? 1 : 0);
                // solid down to the floor, the base is part of the one shape under all the steps
                this.haveBase            = haveRiser && this.riserType != StairsRiserType.FillDown && plateauHeight > kEpsilon;
                this.startBase           = this.subMeshCount;
                if (this.haveBase)
                    this.subMeshCount += 1;
            }
        }
    }
}