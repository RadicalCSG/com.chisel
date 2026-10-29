using Unity.Mathematics;
using Unity.Collections;
using UnityEngine;
using Unity.Entities;

namespace Chisel.Core
{
    public sealed partial class BrushMeshFactory
    {
        public static bool GenerateSphere(float3 diameterXYZ, float offsetY, float rotation, bool generateFromCenter, int horzSegments, int vertSegments,
                                          uint smoothingGroup,
                                          in BlobAssetReference<InternalChiselSurfaceArray> surfaceDefinitionBlob,
                                          out BlobAssetReference<BrushMeshBlob> brushMesh,
                                          Allocator allocator = Allocator.Persistent)// Indirect
		{
            brushMesh = BlobAssetReference<BrushMeshBlob>.Null;
			using var builder = new BlobBuilder(Allocator.Temp);
			ref var root = ref builder.ConstructRoot<BrushMeshBlob>();
			ref var surfaceDefinition = ref surfaceDefinitionBlob.Value;

			var transform = float4x4.TRS(Vector3.zero, quaternion.AxisAngle(new Vector3(0, 1, 0), rotation), Vector3.one);
			var arrays = CreateSphere(diameterXYZ, offsetY, generateFromCenter, horzSegments, vertSegments,
									  ref surfaceDefinition, in builder, ref root);
			var localVertices = arrays.localVertices;
			var polygons = arrays.polygons;
			var halfEdges = arrays.halfEdges;
			if (localVertices.Length == 0)
				return false;

			// TODO: do something more intelligent with surface assignment, and put it inside CreateSphere
			for (int i = 0; i < polygons.Length; i++)
			{
				var surfaceID = i < surfaceDefinition.surfaces.Length ? i : 0;
				polygons[i].descriptionIndex = surfaceID;
				polygons[i].surface = surfaceDefinition.surfaces[surfaceID];
				if (smoothingGroup != 0)
					polygons[i].surface.details.smoothingGroup = smoothingGroup;
			}

			// TODO: eventually remove when it's more battle tested
			if (!Validate(in localVertices, in halfEdges, in polygons, logErrors: true))
				return false;

			var localPlanes = builder.Allocate(ref root.localPlanes, polygons.Length);
			root.localPlaneCount = polygons.Length;
			// TODO: calculate corner planes
			var halfEdgePolygonIndices = builder.Allocate(ref root.halfEdgePolygonIndices, halfEdges.Length);
			CalculatePlanes(ref localPlanes, in polygons, in halfEdges, in localVertices);
			var halfEdgeUVs = builder.Allocate(ref root.halfEdgeUVs, halfEdges.Length);
			MapSphere(ref polygons, in halfEdges, in localVertices, in localPlanes, ref halfEdgeUVs, (localVertices[0] + localVertices[1]) * 0.5f, 0.5f * math.abs(diameterXYZ));
			UpdateHalfEdgePolygonIndices(ref halfEdgePolygonIndices, in polygons);
			root.localBounds = CalculateBounds(in localVertices);
			brushMesh = builder.CreateBlobAssetReference<BrushMeshBlob>(allocator); // Allocator.Persistent / Confirmed to dispose
			return true;
		}

		// Every face's uvs as a part of one map of the sphere, so the texture goes on from one face to the next: u round the
		// sphere (its longitude, at the equator's scale) and v along it from pole to pole (its latitude's arc), in units,
		// stretched towards the poles and cut where the longitude goes round. Each corner gets its place on the map (the
		// face's uvs are interpolated between them, so faces meet exactly), and each face the flat mapping closest to it
		// (for its tangents, decals and lightmap). The surface's own uv settings are applied to the map; a surface mapped
		// in world space keeps its own.
		static void MapSphere(ref BlobBuilderArray<BrushMeshBlob.Polygon> polygons, in BlobBuilderArray<BrushMeshBlob.HalfEdge> halfEdges,
							  in BlobBuilderArray<float3> localVertices, in BlobBuilderArray<float4> localPlanes, ref BlobBuilderArray<float2> halfEdgeUVs,
							  float3 centre, float3 radius)
		{
			var equator  = (radius.x + radius.z) * 0.5f;
			var meridian = (equator + radius.y) * 0.5f;
			if (!(equator > 0) || !(radius.y > 0))
				return;
			for (int p = 0; p < polygons.Length; p++)
			{
				ref var polygon = ref polygons[p];
				if ((polygon.surface.details.detailFlags & SurfaceDetailFlags.TextureIsInWorldSpace) != 0)
					continue;
				var faceCentre = float3.zero;
				for (int e = 0; e < polygon.edgeCount; e++)
					faceCentre += localVertices[halfEdges[polygon.firstEdge + e].vertexIndex];
				faceCentre /= polygon.edgeCount;

				var faceOffset = faceCentre - centre;
				var faceLongitude = math.atan2(faceOffset.z, faceOffset.x);
				var normal     = math.normalizesafe(localPlanes[p].xyz);
				// westwards, so the texture isn't mirrored seen from outside, and northwards, in the face's plane
				var west       = new float3(math.sin(faceLongitude), 0, -math.cos(faceLongitude));
				west           = math.normalizesafe(west - normal * math.dot(west, normal));
				var north      = math.normalizesafe(math.cross(west, normal));
				if (math.dot(north, new float3(0, 1, 0) - normal * normal.y) < 0)
					north = -north;

				// the flat mapping that puts the face's corners closest to where they are on the map (exactly, for a
				// triangle): least squares over (west, north, 1) in the face's plane
				ref var uv0 = ref polygon.surface.details.UV0;
				float4 givenU = uv0.U, givenV = uv0.V;
				var normalMatrix = float3x3.zero;
				var targetU = float3.zero; var targetV = float3.zero;
				for (int e = 0; e < polygon.edgeCount; e++)
				{
					var corner = localVertices[halfEdges[polygon.firstEdge + e].vertexIndex];
					var offset = corner - centre;
					var ring   = math.length(offset.xz);
					// a pole is on every longitude: the face's own
					var longitude = ring > equator * 1e-4f ? math.atan2(offset.z, offset.x) : faceLongitude;
					// on the same side of where the map is cut as the face
					longitude = faceLongitude + math.atan2(math.sin(longitude - faceLongitude), math.cos(longitude - faceLongitude));
					var latitude  = math.atan2(offset.y / radius.y, ring / equator);
					var mapU = -longitude * equator;
					var mapV = (latitude + math.PI * 0.5f) * meridian;
					halfEdgeUVs[polygon.firstEdge + e] = new float2(givenU.x * mapU + givenU.y * mapV + givenU.w,
																	givenV.x * mapU + givenV.y * mapV + givenV.w);
					var row = new float3(math.dot(west, corner - faceCentre), math.dot(north, corner - faceCentre), 1);
					normalMatrix += new float3x3(row * row.x, row * row.y, row * row.z);
					targetU += row * mapU;
					targetV += row * mapV;
				}
				if (math.abs(math.determinant(normalMatrix)) < 1e-12f)
					continue;
				var inverse = math.inverse(normalMatrix);
				var fitU = math.mul(inverse, targetU);
				var fitV = math.mul(inverse, targetV);
				// u = dot(along, position) + atU, v = dot(up, position) + atV, in the brush's space
				var along = west * fitU.x + north * fitU.y;
				var atU   = fitU.z - math.dot(along, faceCentre);
				var up    = west * fitV.x + north * fitV.y;
				var atV   = fitV.z - math.dot(up, faceCentre);

				// the same in the plane's space, which the surface's uv settings work in
				var toPlane  = MathExtensions.GenerateLocalToPlaneSpaceMatrix(localPlanes[p]);
				var rotation = new float3x3(toPlane);
				var shift    = toPlane.c3.xyz;
				var u = new float4(math.mul(rotation, along), atU - math.dot(math.mul(rotation, along), shift));
				var v = new float4(math.mul(rotation, up),    atV - math.dot(math.mul(rotation, up),    shift));

				uv0.U = givenU.x * u + givenU.y * v + new float4(0, 0, 0, givenU.w);
				uv0.V = givenV.x * u + givenV.y * v + new float4(0, 0, 0, givenV.w);
			}
		}
        
        public static BrushMeshArrays CreateSphere(float3 diameterXYZ, float offsetY, bool generateFromCenter, int horzSegments, int vertSegments,
                                                   ref InternalChiselSurfaceArray surfaceDefinition,
                                                   in BlobBuilder builder,
                                                   ref BrushMeshBlob root)
        {
            if (diameterXYZ.x == 0 ||
                diameterXYZ.y == 0 ||
                diameterXYZ.z == 0)
                return default;

            var lastVertSegment = vertSegments - 1;

            var triangleCount   = horzSegments + horzSegments;    // top & bottom
            var quadCount       = horzSegments * (vertSegments - 2);
            int polygonCount    = triangleCount + quadCount;
            int halfEdgeCount   = (triangleCount * 3) + (quadCount * 4);

            var vertices    = CreateSphereVertices(diameterXYZ, offsetY, generateFromCenter, horzSegments, vertSegments, in builder, ref root);
            var polygons    = builder.Allocate(ref root.polygons, polygonCount);
            var halfEdges   = builder.Allocate(ref root.halfEdges, halfEdgeCount);

            var edgeIndex       = 0;
            var polygonIndex    = 0;
            var startVertex     = 2;
            for (int v = 0; v < vertSegments; v++)
            {
                var startEdge = edgeIndex;
                for (int h = 0, p = horzSegments - 1; h < horzSegments; p = h, h++)
                {
                    var n = (h + 1) % horzSegments;
                    int polygonEdgeCount;
                    if (v == 0) // top
                    {
                        //          0
                        //          *
                        //         ^ \
                        //     p1 /0 1\ n0
                        //       /  2  v
                        //		*<------*  
                        //     2    t    1
                        polygonEdgeCount = 3;
                        var p1 = (p * 3) + 1;
                        var n0 = (n * 3) + 0;
                        var t = ((vertSegments == 2) ? (startEdge + (horzSegments * 3) + (h * 3) + 1) : (startEdge + (horzSegments * 3) + (h * 4) + 1));
                        halfEdges[edgeIndex + 0] = new BrushMeshBlob.HalfEdge { twinIndex = p1, vertexIndex = 0 };
                        halfEdges[edgeIndex + 1] = new BrushMeshBlob.HalfEdge { twinIndex = n0, vertexIndex = startVertex + (horzSegments - 1) - h };
                        halfEdges[edgeIndex + 2] = new BrushMeshBlob.HalfEdge { twinIndex = t, vertexIndex = startVertex + (horzSegments - 1) - p };
                    }
                    else
                    if (v == lastVertSegment)
                    {
                        //     0    t    1
                        //		*------>*
                        //       ^  1  /  
                        //     p1 \0 2/ n0
                        //         \ v
                        //          *
                        //          2
                        polygonEdgeCount = 3;
                        var p2 = startEdge + (p * 3) + 2;
                        var n0 = startEdge + (n * 3) + 0;
                        var t = ((vertSegments == 2) ? (startEdge - (horzSegments * 3) + (h * 3) + 2) : (startEdge - (horzSegments * 4) + (h * 4) + 3));
                        halfEdges[edgeIndex + 0] = new BrushMeshBlob.HalfEdge { twinIndex = p2, vertexIndex = startVertex + (horzSegments - 1) - p };
                        halfEdges[edgeIndex + 1] = new BrushMeshBlob.HalfEdge { twinIndex = t, vertexIndex = startVertex + (horzSegments - 1) - h };
                        halfEdges[edgeIndex + 2] = new BrushMeshBlob.HalfEdge { twinIndex = n0, vertexIndex = 1 };
                    }
                    else
                    {
                        //     0    t3   1
                        //		*------>*
                        //      ^   1   |  
                        //   p1 |0     2| n0
                        //      |   3   v
                        //		*<------*
                        //     3    t1   2
                        polygonEdgeCount = 4;
                        var p1 = startEdge + (p * 4) + 2;
                        var n0 = startEdge + (n * 4) + 0;
                        var t3 = ((v == 1) ? (startEdge - (horzSegments * 3) + (h * 3) + 2) : (startEdge - (horzSegments * 4) + (h * 4) + 3));
                        var t1 = ((v == lastVertSegment - 1) ? (startEdge + (horzSegments * 4) + (h * 3) + 1) : (startEdge + (horzSegments * 4) + (h * 4) + 1));
                        halfEdges[edgeIndex + 0] = new BrushMeshBlob.HalfEdge { twinIndex = p1, vertexIndex = startVertex + (horzSegments - 1) - p };
                        halfEdges[edgeIndex + 1] = new BrushMeshBlob.HalfEdge { twinIndex = t3, vertexIndex = startVertex + (horzSegments - 1) - h };
                        halfEdges[edgeIndex + 2] = new BrushMeshBlob.HalfEdge { twinIndex = n0, vertexIndex = startVertex + (horzSegments - 1) - h + horzSegments };
                        halfEdges[edgeIndex + 3] = new BrushMeshBlob.HalfEdge { twinIndex = t1, vertexIndex = startVertex + (horzSegments - 1) - p + horzSegments };
                    }

                    polygons[polygonIndex] = new BrushMeshBlob.Polygon
                    {
                        firstEdge   = edgeIndex,
                        edgeCount   = polygonEdgeCount,
                        // TODO: do something more intelligent with surface assignment
                        surface     = surfaceDefinition.surfaces[0]
                    };
                    
                    edgeIndex += polygonEdgeCount;
                    polygonIndex++;
                }
                if (v > 0)
                    startVertex += horzSegments;
            }
            return new BrushMeshArrays { localVertices = vertices, polygons = polygons, halfEdges = halfEdges };
        }

        public static BlobBuilderArray<float3> CreateSphereVertices(float3 diameterXYZ, float offsetY, bool generateFromCenter, int horzSegments, int vertSegments,
                                                                    in BlobBuilder builder, ref BrushMeshBlob root)
        {
            var vertexCount = (horzSegments * (vertSegments - 1)) + 2;

            var vertices = builder.Allocate(ref root.localVertices, vertexCount);

            var radius = 0.5f * diameterXYZ;

            var offset = generateFromCenter ? offsetY : radius.y + offsetY;
            vertices[0] = new float3(0, 1, 0) * -radius.y;
            vertices[1] = new float3(0, 1, 0) * radius.y;

            vertices[0].y += offset;
            vertices[1].y += offset;

            // TODO: optimize

            var doublePI            = math.PI * 2;
            var degreePerSegment    = doublePI / horzSegments;
            var angleOffset         = ((horzSegments & 1) == 1) ? 0.0f : degreePerSegment * 0.5f;
            for (int v = 1, vertexIndex = 2; v < vertSegments; v++)
            {
                var segmentFactor   = ((v - (vertSegments / 2.0f)) / vertSegments); // [-0.5f ... 0.5f]
                var segmentDegree   = (segmentFactor * 180);                        // [-90 .. 90]
                var segmentHeight   = math.sin(math.radians(segmentDegree));
                var segmentRadius   = math.cos(math.radians(segmentDegree));     // [0 .. 0.707 .. 1 .. 0.707 .. 0]

                var yRingPos        = (segmentHeight * radius.y) + offset;
                var xRingRadius     = segmentRadius * radius.x;
                var zRingRadius     = segmentRadius * radius.z;

                if (radius.y < 0)
                {
                    for (int h = horzSegments - 1; h >= 0; h--, vertexIndex++)
                    {
                        var hRad = (h * degreePerSegment) + angleOffset;
                        vertices[vertexIndex] = new float3(math.cos(hRad) * segmentRadius * radius.x,
                                                           yRingPos,
                                                           math.sin(hRad) * segmentRadius * radius.z);
                    }
                } else
                {
                    for (int h = 0; h < horzSegments; h++, vertexIndex++)
                    {
                        var hRad = (h * degreePerSegment) + angleOffset;
                        vertices[vertexIndex] = new float3(math.cos(hRad) * segmentRadius * radius.x,
                                                           yRingPos,
                                                           math.sin(hRad) * segmentRadius * radius.z);
                    }
                }
            }
            return vertices;
        }

        public static bool GenerateSphereVertices(ChiselSphereDefinition definition, ref Vector3[] vertices)
        {
            //var transform = float4x4.TRS(Vector3.zero, quaternion.AxisAngle(new Vector3(0, 1, 0), definition.rotation), new Vector3(1));
            BrushMeshFactory.CreateSphereVertices(definition.settings.diameterXYZ, definition.settings.offsetY, definition.settings.generateFromCenter, definition.settings.horizontalSegments, definition.settings.verticalSegments, ref vertices);
            return true;
        }

        public static void CreateSphereVertices(Vector3 diameterXYZ, float offsetY, bool generateFromCenter, int horzSegments, int vertSegments, ref Vector3[] vertices)
        {
            //var lastVertSegment	= vertSegments - 1;
            int vertexCount = (horzSegments * (vertSegments - 1)) + 2;

            if (vertices == null ||
                vertices.Length != vertexCount)
                vertices = new Vector3[vertexCount];

            var radius = 0.5f * diameterXYZ;

            var offset = generateFromCenter ? offsetY : radius.y + offsetY;
            vertices[0] = new Vector3(0, 1, 0) * -radius.y;
            vertices[1] = new Vector3(0, 1, 0) * radius.y;

            vertices[0].y += offset;
            vertices[1].y += offset;

            // TODO: optimize

            var degreePerSegment = (360.0f / horzSegments) * Mathf.Deg2Rad;
            var angleOffset = ((horzSegments & 1) == 1) ? 0.0f : ((360.0f / horzSegments) * 0.5f) * Mathf.Deg2Rad;
            for (int v = 1, vertexIndex = 2; v < vertSegments; v++)
            {
                var segmentFactor   = ((v - (vertSegments / 2.0f)) / vertSegments); // [-0.5f ... 0.5f]
                var segmentDegree   = (segmentFactor * 180);                        // [-90 .. 90]
                var segmentHeight   = math.sin(segmentDegree * Mathf.Deg2Rad);
                var segmentRadius   = math.cos(segmentDegree * Mathf.Deg2Rad);     // [0 .. 0.707 .. 1 .. 0.707 .. 0]

                var yRingPos        = (segmentHeight * radius.y) + offset;
                var xRingRadius     = segmentRadius * radius.x;
                var zRingRadius     = segmentRadius * radius.z;

                if (radius.y < 0)
                {
                    for (int h = horzSegments - 1; h >= 0; h--, vertexIndex++)
                    {
                        var hRad = (h * degreePerSegment) + angleOffset;
                        vertices[vertexIndex] = new Vector3(math.cos(hRad) * segmentRadius * radius.x,
                                                           yRingPos,
                                                           math.sin(hRad) * segmentRadius * radius.z);
                    }
                } else
                {
                    for (int h = 0; h < horzSegments; h++, vertexIndex++)
                    {
                        var hRad = (h * degreePerSegment) + angleOffset;
                        vertices[vertexIndex] = new Vector3(math.cos(hRad) * segmentRadius * radius.x,
                                                           yRingPos,
                                                           math.sin(hRad) * segmentRadius * radius.z);
                    }
                }
            }
        }
    }
}