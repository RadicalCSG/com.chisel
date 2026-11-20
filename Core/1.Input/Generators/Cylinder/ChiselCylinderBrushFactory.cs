using System;
using System.Runtime.CompilerServices;
using Unity.Mathematics;
using Unity.Collections;
using UnityEngine;
using Unity.Entities;

namespace Chisel.Core
{
    public sealed partial class BrushMeshFactory
    {
        [Serializable]
        public struct ChiselCircleDefinition
        {
            public float diameterX;
            public float diameterZ;
            public float height;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool GenerateCylinderSubMesh(float    diameter,
                                                   float    topHeight, 
                                                   float    bottomHeight, 
                                                   float    rotation, 
                                                   int      sides, 
                                                   bool     fitToBounds, 
                                                   in BlobAssetReference<InternalChiselSurfaceArray> surfaceDefinitionBlob,
                                                   out BlobAssetReference<BrushMeshBlob> brushMesh,
                                                   Allocator allocator = Allocator.Persistent)// Indirect
		{
            return GenerateConicalFrustumSubMesh(new float2(diameter, diameter), topHeight, 
                                                 new float2(diameter, diameter), bottomHeight, 
                                                 rotation, sides, fitToBounds, 
                                                 in surfaceDefinitionBlob, out brushMesh, allocator);// Indirect
		}

        public static bool GenerateConicalFrustumSubMesh(float2 topDiameter,    float topHeight,
                                                         float2 bottomDiameter, float bottomHeight, 
                                                         float                  rotation, 
                                                         int                    segments, 
                                                         bool                   fitToBounds, 
                                                         in BlobAssetReference<InternalChiselSurfaceArray> surfaceDefinitionBlob,
                                                         out BlobAssetReference<BrushMeshBlob>             brushMesh,
                                                         Allocator                                         allocator = Allocator.Persistent)// Indirect
		{
            brushMesh = BlobAssetReference<BrushMeshBlob>.Null;
            if (topHeight > bottomHeight) 
            {
                (bottomHeight, topHeight) = (topHeight, bottomHeight);
				(bottomDiameter, topDiameter) = (topDiameter, bottomDiameter);
			}
            if (segments < 3 || (topHeight - bottomHeight) == 0 || (bottomDiameter.x == 0 && topDiameter.x == 0) || (bottomDiameter.y == 0 && topDiameter.y == 0))
                return false;

            ref var surfaceDefinition = ref surfaceDefinitionBlob.Value;
            if (surfaceDefinition.surfaces.Length < segments + 2)
                return false;

			using var builder = new BlobBuilder(Allocator.Temp);
			ref var root = ref builder.ConstructRoot<BrushMeshBlob>();
			var arrays = CreateConicalFrustumArrays(topDiameter, topHeight, bottomDiameter, bottomHeight, rotation, segments, fitToBounds,
													in surfaceDefinitionBlob, in builder, ref root);
			var localVertices = arrays.localVertices;
			var polygons = arrays.polygons;
			var halfEdges = arrays.halfEdges;

			if (!Validate(in localVertices, in halfEdges, in polygons, logErrors: true))
				return false;

			var localPlanes = builder.Allocate(ref root.localPlanes, polygons.Length);
			root.localPlaneCount = polygons.Length;
			var halfEdgePolygonIndices = builder.Allocate(ref root.halfEdgePolygonIndices, halfEdges.Length);
			CalculatePlanes(ref localPlanes, in polygons, in halfEdges, in localVertices);
			UpdateHalfEdgePolygonIndices(ref halfEdgePolygonIndices, in polygons);
			root.localBounds = CalculateBounds(in localVertices);
			brushMesh = builder.CreateBlobAssetReference<BrushMeshBlob>(allocator); // Allocator.Persistent / Confirmed to dispose
			return true;
		}

        static BrushMeshArrays CreateConicalFrustumArrays(float2 topDiameter,    float topHeight,
                                                          float2 bottomDiameter, float bottomHeight,
                                                          float rotation, int segments, bool fitToBounds,
                                                          in BlobAssetReference<InternalChiselSurfaceArray> surfaceDefinitionBlob,
                                                          in BlobBuilder builder, ref BrushMeshBlob root)
        {

            // a cone where a ring is a point
            if (math.all(topDiameter == 0))
            {
                var localVertices = GetConeFrustumVertices(topHeight,
                                                           bottomDiameter, bottomHeight,
                                                           rotation, segments,
                                                           in builder, ref root, inverse: false, fitToBounds: fitToBounds);

                return CreateConeSubMesh(segments, in localVertices, in surfaceDefinitionBlob, in builder, ref root);
            }

            if (math.all(bottomDiameter == 0))
            {
                var localVertices = GetConeFrustumVertices(bottomHeight,
                                                           topDiameter, topHeight,
                                                           rotation, segments,
                                                           in builder, ref root, inverse: true, fitToBounds: fitToBounds);

                return CreateConeSubMesh(segments, in localVertices, in surfaceDefinitionBlob, in builder, ref root);
            }

            {
                var localVertices = GetConicalFrustumVertices(topDiameter, topHeight,
                                                              bottomDiameter, bottomHeight,
                                                              rotation, segments,
                                                              in builder, ref root, fitToBounds: fitToBounds);

                return CreateExtrudedSubMesh(segments, 0, 1, in localVertices, in surfaceDefinitionBlob, in builder, ref root);
            }
        }

        public static BlobBuilderArray<float3> GetConeFrustumVertices(float topHeight, float2 bottomDiameter, float bottomHeight, float rotation, int segments,
                                                                      in BlobBuilder builder, ref BrushMeshBlob root,
                                                                      bool inverse = false, bool fitToBounds = false)
        {
            var rotate			= quaternion.AxisAngle(new float3(0, 1, 0), math.radians(rotation));
            var bottomRadiusX	= math.mul(rotate, new float3(bottomDiameter.x * 0.5f, 0, 0));
            var bottomRadiusZ	= math.mul(rotate, new float3(0, 0, bottomDiameter.y * 0.5f));
            var topY			= new float3(0, topHeight, 0);
            var bottomY			= new float3(0, bottomHeight, 0);

            var localVertices = builder.Allocate(ref root.localVertices, segments + 1);
            localVertices[0] = topY;

            const float doublePI = (math.PI * 2);
            var angleStep     = doublePI / segments;
            var angleOffset = ((segments & 1) == 1) ? 0.0f : angleStep * 0.5f;
            var angle = angleOffset;
            for (int v = 0; v < segments; v++, angle += angleStep)
            {
                var s = math.sin(angle);
                var c = math.cos(angle);

                var bottomVertex = (bottomRadiusX * c) + (bottomRadiusZ * s);
                bottomVertex += bottomY;

                var vi = inverse ? (segments - v) : (v + 1);
                localVertices[vi] = bottomVertex;
            }

            if (fitToBounds)
                FitXZ(ref localVertices, 1, localVertices.Length - 1, bottomDiameter);
            return localVertices;
        }

        public static BlobBuilderArray<float3> GetConicalFrustumVertices(float2 topDiameter, float topHeight,
                                                                         float2 bottomDiameter, float bottomHeight,
                                                                         float rotation, int segments,
                                                                         in BlobBuilder builder, ref BrushMeshBlob root,
                                                                         bool fitToBounds = false)
        {
            var localVertices = builder.Allocate(ref root.localVertices, segments * 2);
            using var rings = new NativeArray<float3>(segments * 2, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
            ConicalFrustumRings(topDiameter, topHeight, bottomDiameter, bottomHeight, rotation, segments, fitToBounds, rings);
            for (int v = 0; v < rings.Length; v++)
                localVertices[v] = rings[v];
            return localVertices;
        }

        // The two rings of a conical frustum: the lower one, then the higher one, a corner per segment at the same
        // angles in each, so corner v of one is below or above corner v of the other
        static void ConicalFrustumRings(float2 topDiameter, float topHeight, float2 bottomDiameter, float bottomHeight,
                                        float rotation, int segments, bool fitToBounds, NativeArray<float3> rings)
        {
            if (topHeight > bottomHeight)
            {
                { var temp = topHeight;   topHeight   = bottomHeight;   bottomHeight   = temp; }
                { var temp = topDiameter; topDiameter = bottomDiameter; bottomDiameter = temp; }
            }

            var rotate		= quaternion.AxisAngle(new float3(0, 1, 0), math.radians(rotation));
            var topAxisX	= math.mul(rotate, new float3(topDiameter.x * 0.5f, 0, 0));
            var topAxisZ	= math.mul(rotate, new float3(0, 0, topDiameter.y * 0.5f));
            var bottomAxisX = math.mul(rotate, new float3(bottomDiameter.x * 0.5f, 0, 0));
            var bottomAxisZ = math.mul(rotate, new float3(0, 0, bottomDiameter.y * 0.5f));
            var topY		= new float3(0, topHeight, 0);
            var bottomY		= new float3(0, bottomHeight, 0);

            const float doublePI = (math.PI * 2);
            var angleStep   = doublePI / segments;
            var angleOffset = ((segments & 1) == 1) ? 0.0f : angleStep * 0.5f;
            var angle       = angleOffset;
            for (int v = 0; v < segments; v++, angle += angleStep)
            {
                var s = Mathf.Sin(angle);
                var c = Mathf.Cos(angle);

                var topVertex = (topAxisX * c) + (topAxisZ * s);
                var bottomVertex = (bottomAxisX * c) + (bottomAxisZ * s);
                topVertex += topY;
                bottomVertex += bottomY;
                rings[v] = topVertex;
                rings[v + segments] = bottomVertex;
            }

            if (fitToBounds)
            {
                FitXZ(rings, 0, segments, topDiameter);
                FitXZ(rings, segments, segments, bottomDiameter);
            }
        }

        static void FitXZ(NativeArray<float3> vertices, int firstVertex, int vertexCount, float2 expectedSize)
        {
            if (math.any(expectedSize == float2.zero))
                return;
            var min  = vertices[firstVertex].xz;
            var max  = vertices[firstVertex].xz;
            var last = firstVertex + vertexCount;
            for (int v = firstVertex + 1; v < last; v++)
            {
                min = math.min(min, vertices[v].xz);
                max = math.max(max, vertices[v].xz);
            }
            var size = math.abs(max - min);
            if (math.any(size == float2.zero))
                return;
            var translation = (max + min) * 0.5f;
            var resize      = expectedSize / size;
            for (int v = firstVertex; v < last; v++)
            {
                var vertex = vertices[v];
                vertex.xz   = (vertex.xz - translation) * resize;
                vertices[v] = vertex;
            }
        }

        // How far a side of an extruded mesh (CreateExtrudedSubMesh) can be out of its plane and still be one face, in metres
        const float kSideOutOfPlane = 0.001f;

        /// <summary>
        /// Whether a side of the conical frustum between these ellipses is out of its plane (more than
        /// <see cref="kSideOutOfPlane"/>): the top isn't the bottom scaled the same along x and z. Then the frustum isn't
        /// convex, and is made of several brushes (<see cref="GenerateWarpedConicalFrustum"/>). A cone never is.
        /// </summary>
        public static bool IsWarpedConicalFrustum(float2 topDiameter, float topHeight, float2 bottomDiameter, float bottomHeight,
                                                  float rotation, int segments, bool fitToBounds)
        {
            if (segments < 3 || math.all(topDiameter == 0) || math.all(bottomDiameter == 0))
                return false;
            using var rings = new NativeArray<float3>(segments * 2, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
            ConicalFrustumRings(topDiameter, topHeight, bottomDiameter, bottomHeight, rotation, segments, fitToBounds, rings);
            for (int v = 0; v < segments; v++)
            {
                var n = (v + 1) % segments;
                if (math.abs(OutOfPlane(rings[v], rings[n], rings[v + segments], rings[n + segments])) > kSideOutOfPlane)
                    return true;
            }
            return false;
        }

        // How far d is from the plane through a, b and c (0 when they are on a line)
        static double OutOfPlane(double3 a, double3 b, double3 c, double3 d)
        {
            var normal = math.cross(b - a, c - a);
            var length = math.length(normal);
            return length > 1e-12 ? math.dot(normal, d - a) / length : 0;
        }

        /// <summary>The brushes a warped conical frustum is made of: one for each side</summary>
        public static int GetWarpedConicalFrustumBrushCount(int segments) { return segments; }

        /// <summary>
        /// A warped conical frustum (<see cref="IsWarpedConicalFrustum"/>) as convex brushes, one for each side: the convex
        /// hull of the side's corners and the middles of the two rings. Together they are the frustum, with each side
        /// out of its plane folded out in two triangles; they overlap inside it. Only the caps and sides are output: the
        /// faces inside take a copy of their side's surface that isn't. The surfaces are as on the frustum as one brush:
        /// the lower ring's first, the higher ring's second, then the sides, each from its corner to the next.
        /// </summary>
        public static bool GenerateWarpedConicalFrustum(float2 topDiameter, float topHeight, float2 bottomDiameter, float bottomHeight,
                                                        float rotation, int segments, bool fitToBounds,
                                                        in BlobAssetReference<InternalChiselSurfaceArray> surfaceDefinitionBlob,
                                                        NativeArray<BlobAssetReference<BrushMeshBlob>> brushMeshes,
                                                        Allocator allocator = Allocator.Persistent)// Indirect
        {
            if (segments < 3 || brushMeshes.Length != GetWarpedConicalFrustumBrushCount(segments) ||
                surfaceDefinitionBlob.Value.surfaces.Length < segments + 2)
                return false;

            using var rings = new NativeArray<float3>(segments * 2, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
            ConicalFrustumRings(topDiameter, topHeight, bottomDiameter, bottomHeight, rotation, segments, fitToBounds, rings);
            var lowerY  = rings[0].y;
            var higherY = rings[segments].y;

            const int kLower = 0, kHigher = 1;
            var piece = new ConvexPiece(6, Allocator.Temp);
            try
            {
                for (int v = 0; v < segments; v++)
                {
                    var n = (v + 1) % segments;
                    piece.Clear();
                    piece.corners.Add(new float3(0, lowerY, 0));
                    piece.corners.Add(new float3(0, higherY, 0));
                    piece.corners.Add(rings[v]);
                    piece.corners.Add(rings[n]);
                    piece.corners.Add(rings[v + segments]);
                    piece.corners.Add(rings[n + segments]);
                    if (!piece.AddHullFaces(lowerY, kLower, higherY, kHigher, sideSurface: v + 2) ||
                        !piece.Build(in surfaceDefinitionBlob, allocator, out var brushMesh))
                        return DisposeAll(brushMeshes);
                    brushMeshes[v] = brushMesh;
                }
                return true;
            }
            finally
            {
                piece.Dispose();
            }
        }

        static bool DisposeAll(NativeArray<BlobAssetReference<BrushMeshBlob>> brushMeshes)
        {
            for (int i = 0; i < brushMeshes.Length; i++)
            {
                if (brushMeshes[i].IsCreated)
                    brushMeshes[i].Dispose();
                brushMeshes[i] = default;
            }
            return false;
        }

        // A convex brush given its corners and its faces, each any way round: the faces are turned to face out of it,
        // and each edge is joined to the same edge of the face on its other side
        struct ConvexPiece : IDisposable
        {
            public NativeList<float3> corners;
            NativeList<int>  faceCorners;
            NativeList<int3> faces;     // first corner in faceCorners, corner count, surface
            NativeList<bool> shown;

            public ConvexPiece(int capacity, Allocator allocator)
            {
                corners     = new NativeList<float3>(capacity, allocator);
                faceCorners = new NativeList<int>(capacity * 4, allocator);
                faces       = new NativeList<int3>(capacity * 2, allocator);
                shown       = new NativeList<bool>(capacity * 2, allocator);
            }

            public void Clear() { corners.Clear(); faceCorners.Clear(); faces.Clear(); shown.Clear(); }

            public void BeginFace(int surface, bool shown)
            {
                faces.Add(new int3(faceCorners.Length, 0, surface));
                this.shown.Add(shown);
            }

            public void Corner(int corner)
            {
                faceCorners.Add(corner);
                var face = faces[faces.Length - 1];
                face.y++;
                faces[faces.Length - 1] = face;
            }

            /// <summary>
            /// The faces of the convex hull of its corners, the first two being the middles of the rings and the others a
            /// side's: a face on the lower or higher plane is that cap, one of only the side's corners the side, and the
            /// others are inside the frustum and not shown
            /// </summary>
            public bool AddHullFaces(float lowerY, int lowerSurface, float higherY, int higherSurface, int sideSurface)
            {
                var count  = corners.Length;
                var extent = 1.0;
                for (int i = 0; i < count; i++)
                    extent = math.max(extent, math.cmax(math.abs((double3)corners[i])));
                // far below the precision of the floats the corners are, far above the rounding of the doubles they are
                // measured in: four corners close to one plane are two faces, and a face is the same from any of its corners
                var epsilon = 1e-9 * extent;

                Span<int>    onPlane = stackalloc int[count];
                Span<double> angles  = stackalloc double[count];
                Span<ulong>  found   = stackalloc ulong[count * count * count];
                var foundCount = 0;
                for (int i = 0; i < count; i++)
                for (int j = i + 1; j < count; j++)
                for (int k = j + 1; k < count; k++)
                {
                    double3 a = corners[i], b = corners[j], c = corners[k];
                    var normal = math.cross(b - a, c - a);
                    var length = math.length(normal);
                    if (length <= epsilon * epsilon)
                        continue;
                    normal /= length;
                    double highest = double.MinValue, lowest = double.MaxValue;
                    for (int p = 0; p < count; p++)
                    {
                        var distance = math.dot(normal, (double3)corners[p] - a);
                        highest = math.max(highest, distance);
                        lowest  = math.min(lowest, distance);
                    }
                    if (highest > epsilon)
                    {
                        if (lowest < -epsilon)
                            continue;
                        normal = -normal;
                    }

                    // the corners on it, as a set, so a face found through other corners of it isn't added again
                    var onCount = 0;
                    var set     = 0ul;
                    for (int p = 0; p < count; p++)
                    {
                        if (math.abs(math.dot(normal, (double3)corners[p] - a)) > epsilon)
                            continue;
                        onPlane[onCount++] = p;
                        set |= 1ul << p;
                    }
                    var known = false;
                    for (int f = 0; f < foundCount && !known; f++)
                        known = found[f] == set;
                    if (known)
                        continue;
                    found[foundCount++] = set;

                    // going round it facing out
                    var center = double3.zero;
                    for (int q = 0; q < onCount; q++)
                        center += corners[onPlane[q]];
                    center /= onCount;
                    var axisU = math.normalize((double3)corners[onPlane[0]] - center);
                    var axisV = math.cross(normal, axisU);
                    for (int q = 0; q < onCount; q++)
                    {
                        var offset = (double3)corners[onPlane[q]] - center;
                        angles[q] = math.atan2(math.dot(offset, axisV), math.dot(offset, axisU));
                    }
                    for (int q = 1; q < onCount; q++)
                    {
                        for (int r = q; r > 0 && angles[r] < angles[r - 1]; r--)
                        {
                            (angles[r], angles[r - 1])   = (angles[r - 1], angles[r]);
                            (onPlane[r], onPlane[r - 1]) = (onPlane[r - 1], onPlane[r]);
                        }
                    }

                    bool onLower = true, onHigher = true, onSide = true;
                    for (int q = 0; q < onCount; q++)
                    {
                        var corner = corners[onPlane[q]];
                        onLower  &= math.abs(corner.y - lowerY)  <= epsilon;
                        onHigher &= math.abs(corner.y - higherY) <= epsilon;
                        onSide   &= onPlane[q] >= 2;
                    }
                    var surface = onLower ? lowerSurface : onHigher ? higherSurface : sideSurface;
                    BeginFace(surface, shown: onLower || onHigher || onSide);
                    for (int q = 0; q < onCount; q++)
                        Corner(onPlane[q]);
                }
                return faces.Length >= 4;
            }

            public bool Build(in BlobAssetReference<InternalChiselSurfaceArray> surfaceDefinitionBlob, Allocator allocator,
                              out BlobAssetReference<BrushMeshBlob> brushMesh)
            {
                brushMesh = default;
                var middle = double3.zero;
                for (int i = 0; i < corners.Length; i++)
                    middle += corners[i];
                middle /= corners.Length;

                // facing out: Newell's normal of a face points away from the middle
                for (int f = 0; f < faces.Length; f++)
                {
                    var face   = faces[f];
                    var normal = double3.zero;
                    var center = double3.zero;
                    for (int i = 0; i < face.y; i++)
                    {
                        double3 curr = corners[faceCorners[face.x + i]];
                        double3 next = corners[faceCorners[face.x + (i + 1) % face.y]];
                        normal += new double3((curr.y - next.y) * (curr.z + next.z),
                                              (curr.z - next.z) * (curr.x + next.x),
                                              (curr.x - next.x) * (curr.y + next.y));
                        center += curr;
                    }
                    if (math.dot(normal, center / face.y - middle) < 0)
                    {
                        for (int i = 0, j = face.y - 1; i < j; i++, j--)
                            (faceCorners[face.x + i], faceCorners[face.x + j]) = (faceCorners[face.x + j], faceCorners[face.x + i]);
                    }
                }

                ref var surfaces = ref surfaceDefinitionBlob.Value;
                using var builder = new BlobBuilder(Allocator.Temp);
                ref var root = ref builder.ConstructRoot<BrushMeshBlob>();
                var localVertices = builder.Allocate(ref root.localVertices, corners.Length);
                for (int i = 0; i < corners.Length; i++)
                    localVertices[i] = corners[i];
                var polygons  = builder.Allocate(ref root.polygons, faces.Length);
                var halfEdges = builder.Allocate(ref root.halfEdges, faceCorners.Length);
                // a half edge ends at its corner: the one after the corner it starts at
                using var edgeOf = new NativeHashMap<int2, int>(faceCorners.Length, Allocator.Temp);
                for (int f = 0; f < faces.Length; f++)
                {
                    var face    = faces[f];
                    var surface = surfaces.surfaces[face.z];
                    if (!shown[f])
                        surface.destinationFlags = SurfaceDestinationFlags.None;
                    polygons[f] = new BrushMeshBlob.Polygon { firstEdge = face.x, edgeCount = face.y, descriptionIndex = face.z, surface = surface };
                    for (int i = 0; i < face.y; i++)
                    {
                        var from = faceCorners[face.x + i];
                        var to   = faceCorners[face.x + (i + 1) % face.y];
                        halfEdges[face.x + i] = new BrushMeshBlob.HalfEdge { vertexIndex = to, twinIndex = -1 };
                        edgeOf.TryAdd(new int2(from, to), face.x + i);
                    }
                }
                for (int f = 0; f < faces.Length; f++)
                {
                    var face = faces[f];
                    for (int i = 0; i < face.y; i++)
                    {
                        var from = faceCorners[face.x + i];
                        var to   = faceCorners[face.x + (i + 1) % face.y];
                        if (edgeOf.TryGetValue(new int2(to, from), out var twin))
                        {
                            var halfEdge = halfEdges[face.x + i];
                            halfEdge.twinIndex = twin;
                            halfEdges[face.x + i] = halfEdge;
                        }
                    }
                }

                if (!Validate(in localVertices, in halfEdges, in polygons, logErrors: true))
                    return false;

                var localPlanes = builder.Allocate(ref root.localPlanes, polygons.Length);
                root.localPlaneCount = polygons.Length;
                var halfEdgePolygonIndices = builder.Allocate(ref root.halfEdgePolygonIndices, halfEdges.Length);
                CalculatePlanes(ref localPlanes, in polygons, in halfEdges, in localVertices);
                UpdateHalfEdgePolygonIndices(ref halfEdgePolygonIndices, in polygons);
                root.localBounds = CalculateBounds(in localVertices);
                brushMesh = builder.CreateBlobAssetReference<BrushMeshBlob>(allocator); // Confirmed to dispose
                return true;
            }

            public void Dispose()
            {
                if (corners.IsCreated)     corners.Dispose();
                if (faceCorners.IsCreated) faceCorners.Dispose();
                if (faces.IsCreated)       faces.Dispose();
                if (shown.IsCreated)       shown.Dispose();
            }
        }

        static BrushMeshArrays CreateConeSubMesh(int segments,
                                                 in BlobBuilderArray<float3>                        localVertices,
                                                 in BlobAssetReference<InternalChiselSurfaceArray>  surfaceDefinitionBlob,
                                                 in BlobBuilder builder, ref BrushMeshBlob root)
        {
            ref var surfaceDefinition   = ref surfaceDefinitionBlob.Value;
            const int descriptionIndex0 = 0;
            ref var chiselSurface0      = ref surfaceDefinition.surfaces[descriptionIndex0];

            var polygons = builder.Allocate(ref root.polygons, segments + 1);
            polygons[0] = new BrushMeshBlob.Polygon { firstEdge = 0, edgeCount = segments, descriptionIndex = descriptionIndex0, surface = chiselSurface0 };

            for (int s = 0, surfaceID = 1; s < segments; s++)
            {
                var descriptionIndex = s + 2;
                polygons[surfaceID] = new BrushMeshBlob.Polygon 
                { 
                    firstEdge           = segments + (s * 3), 
                    edgeCount           = 3, 
                    descriptionIndex    = descriptionIndex,
                    surface             = surfaceDefinition.surfaces[descriptionIndex] 
                };
                surfaceID++;
            }

            var halfEdges = builder.Allocate(ref root.halfEdges, 4 * segments);
            for (int n = 0, e = segments - 1; n < segments; e=n, n++)
            {
                var p = (n + 1) % segments;
                //			 	    v0	 
                //	                 *             
                //                ^ /^ \
                //               / /  \ \
                //			    / / e2 \ \   
                //             / /      \ \  
                //	       t2 / /e3 q2 e2\ \ t3
                //           / /          \ \  
                //			/ v     e1     \ v
                //	 ------>   ----------->   ----->
                //        v2 *              * v1
                //	 <-----    <----------    <-----  
                //			        e0    

                var v0 = 0;
                var v1 = n + 1;
                var v2 = e + 1;
                
                var e0 = n;
                var e1 = ((n * 3) + segments) + 0;
                var e2 = ((n * 3) + segments) + 1;
                var e3 = ((n * 3) + segments) + 2;

                var t3 = ((e * 3) + segments) + 2;
                var t2 = ((p * 3) + segments) + 1;

                // bottom polygon
                halfEdges[e0] = new BrushMeshBlob.HalfEdge { vertexIndex = v1, twinIndex = e1 };

                // triangle
                halfEdges[e1] = new BrushMeshBlob.HalfEdge { vertexIndex = v2, twinIndex = e0 };
                halfEdges[e2] = new BrushMeshBlob.HalfEdge { vertexIndex = v0, twinIndex = t3 };
                halfEdges[e3] = new BrushMeshBlob.HalfEdge { vertexIndex = v1, twinIndex = t2 };
            }
            return new BrushMeshArrays { localVertices = localVertices, polygons = polygons, halfEdges = halfEdges };
        }

        public static void FitXZ(float3[] vertices, int offset, int count, float2 expectedSize)
        {
            if (math.any(expectedSize == float2.zero))
                return;
            float2 min = vertices[offset].xz, max = vertices[offset].xz;
            var last = offset + count;
            for (int v = offset + 1; v < last; v++)
            {
                min = math.min(min, vertices[v].xz);
                max = math.max(max, vertices[v].xz);
            }

            var size = math.abs(max - min);
            if (math.any(size == float2.zero))
                return;
            var translation = (max + min) * 0.5f;
            var resize = expectedSize / size;
            for (int v = offset; v < last; v++)
            {
                vertices[v].xz -= translation;
                vertices[v].xz = vertices[v].xz * resize;
            }
        }

        public static void FitXZ(Vector3[] vertices, int offset, int count, float diameterX, float diameterZ)
        {
            float2 min = ((float3)vertices[offset]).xz, max = ((float3)vertices[offset]).xz;
            var last = offset + count;
            for (int v = offset + 1; v < last; v++)
            {
                var vert = (float3)vertices[v];
                min = math.min(min, vert.xz);
                max = math.min(max, vert.xz);
            }

            var expectedSize = new float2(diameterX, diameterZ);
            var size = math.abs(max - min);
            for (int v = offset; v < last; v++)
            {
                var vert = vertices[v];
                var newXZ = (((float3)vert).xz / size) * expectedSize;
                vertices[v] = new Vector3(newXZ.x, vert.y, newXZ.y);
            }
        }

        public static void GetCircleVertices(float radiusX, float radiusZ, float rotation, int segments, ref float3[] vertices, bool fitToBounds = false)
        {
            var rotate		= Quaternion.AngleAxis(rotation, Vector3.up);
            var circleAxisX = rotate * Vector3.right	* radiusX;
            var circleAxisZ = rotate * Vector3.forward	* radiusZ;

            if (vertices == null ||
                vertices.Length != segments)
                vertices = new float3[segments];

            for (int v = 0; v < segments; v++)
            {
                var r = ((v * 360.0f) / (float)segments) * Mathf.Deg2Rad;
                var s = Mathf.Sin(r);
                var c = Mathf.Cos(r);

                var bottomVertex = (circleAxisX * c) + (circleAxisZ * s);
                vertices[v] = bottomVertex;
            }

            if (fitToBounds)
                FitXZ(vertices, 0, vertices.Length, new float2(radiusX, radiusZ) * 2.0f);
        }
        
        public static Vector3[] GetConicalFrustumVertices(ChiselCircleDefinition  bottom, ChiselCircleDefinition  top, float rotation, int segments, ref Vector3[] vertices, bool fitToBounds = false)
        {
            if (top.height > bottom.height) { var temp = top; top = bottom; bottom = temp; }

            var rotate		= Quaternion.AngleAxis(rotation, Vector3.up);
            var topAxisX	= rotate * Vector3.right	* top.diameterX * 0.5f;
            var topAxisZ	= rotate * Vector3.forward	* top.diameterZ * 0.5f;
            var bottomAxisX = rotate * Vector3.right	* bottom.diameterX * 0.5f;
            var bottomAxisZ = rotate * Vector3.forward	* bottom.diameterZ * 0.5f;
            var topY		= Vector3.up * top.height;
            var bottomY		= Vector3.up * bottom.height;

            if (vertices == null ||
                vertices.Length != segments * 2)
                vertices = new Vector3[segments * 2];

            float angleOffset = ((segments & 1) == 1) ? 0.0f : ((360.0f / segments) * 0.5f);

            for (int v = 0; v < segments; v++)
            {
                var r = (((v * 360.0f) / (float)segments) + angleOffset) * Mathf.Deg2Rad;
                var s = Mathf.Sin(r);
                var c = Mathf.Cos(r);

                var topVertex = (topAxisX * c) + (topAxisZ * s);
                var bottomVertex = (bottomAxisX * c) + (bottomAxisZ * s);
                topVertex += topY;
                bottomVertex += bottomY;
                vertices[v] = topVertex;
                vertices[v + segments] = bottomVertex;
            }

            if (fitToBounds)
            {
                FitXZ(vertices, 0, segments, top.diameterX, top.diameterZ);
                FitXZ(vertices, segments, segments, bottom.diameterX, bottom.diameterZ);
            }

            return vertices;
        }

    }
}