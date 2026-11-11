using Unity.Entities;
using Unity.Mathematics;

namespace Chisel.Core
{
    /// <summary>The corners, edges and faces of a brush, read the same way from a BrushMesh and from a brush being built</summary>
    public interface IBrushShape
    {
        int    HalfEdgeCount { get; }
        int    PolygonCount  { get; }
        float3 VertexOf(int halfEdge);
        int    VertexIndexOf(int halfEdge);
        int    FirstEdgeOf(int polygon);
        int    EdgeCountOf(int polygon);
        /// <summary>The plane the face lies on, when the brush has its planes already</summary>
        bool   TryGetPlaneOf(int polygon, out float4 plane);
    }

    public struct BrushMeshShape : IBrushShape
    {
        readonly BrushMesh mesh;
        public BrushMeshShape(BrushMesh mesh) { this.mesh = mesh; }
        public int    HalfEdgeCount             => mesh.halfEdges.Length;
        public int    PolygonCount              => mesh.polygons.Length;
        public float3 VertexOf(int halfEdge)    => mesh.vertices[mesh.halfEdges[halfEdge].vertexIndex];
        public int    VertexIndexOf(int halfEdge) => mesh.halfEdges[halfEdge].vertexIndex;
        public int    FirstEdgeOf(int polygon)  => mesh.polygons[polygon].firstEdge;
        public int    EdgeCountOf(int polygon)  => mesh.polygons[polygon].edgeCount;
        public bool   TryGetPlaneOf(int polygon, out float4 plane)
        {
            var planes = mesh.planes;
            plane = (planes != null && planes.Length == mesh.polygons.Length) ? planes[polygon] : default;
            return planes != null && planes.Length == mesh.polygons.Length;
        }
    }

    // A brush being built: its arrays as pointers, valid while the builder they are in is (BlobBuilderArray can't be kept
    // in a struct that isn't a ref struct, and a ref struct can't be a type argument)
    public unsafe struct BrushBlobShape : IBrushShape
    {
        readonly float3*                 vertices;
        readonly BrushMeshBlob.HalfEdge* halfEdges;
        readonly BrushMeshBlob.Polygon*  polygons;
        readonly int halfEdgeCount;
        readonly int polygonCount;
        public BrushBlobShape(in BlobBuilderArray<float3> vertices, in BlobBuilderArray<BrushMeshBlob.HalfEdge> halfEdges, in BlobBuilderArray<BrushMeshBlob.Polygon> polygons)
        {
            this.vertices  = (float3*)vertices.GetUnsafePtr();
            this.halfEdges = (BrushMeshBlob.HalfEdge*)halfEdges.GetUnsafePtr();
            this.polygons  = (BrushMeshBlob.Polygon*)polygons.GetUnsafePtr();
            halfEdgeCount  = halfEdges.Length;
            polygonCount   = polygons.Length;
        }
        public int    HalfEdgeCount             => halfEdgeCount;
        public int    PolygonCount              => polygonCount;
        public float3 VertexOf(int halfEdge)    => vertices[halfEdges[halfEdge].vertexIndex];
        public int    VertexIndexOf(int halfEdge) => halfEdges[halfEdge].vertexIndex;
        public int    FirstEdgeOf(int polygon)  => polygons[polygon].firstEdge;
        public int    EdgeCountOf(int polygon)  => polygons[polygon].edgeCount;
        // its planes are made after it is checked
        public bool   TryGetPlaneOf(int polygon, out float4 plane) { plane = default; return false; }
    }

    /// <summary>
    /// Whether a brush whose edges and faces fit together (BrushMesh.ValidateData) is a solid shape: it has a volume, and
    /// its faces don't cross or fold over each other. Only the corners its edges use count. Without allocations, so it
    /// can run while a brush is built in a job.
    /// </summary>
    public static class BrushShapeChecks
    {
        // How far a corner may be off before it counts: float rounding at the brush's size for a brush to be flat, more for
        // a corner to be on the wrong side of a face, so a shape that is fine is never taken for a broken one
        const float kFlatTolerance     = 1e-6f;
        const float kCrossingTolerance = 1e-5f;

        static float SizeOf<T>(in T shape) where T : struct, IBrushShape
        {
            var min = shape.VertexOf(0);
            var max = min;
            for (int h = 1; h < shape.HalfEdgeCount; h++)
            {
                var vertex = shape.VertexOf(h);
                min = math.min(min, vertex);
                max = math.max(max, vertex);
            }
            return math.cmax(max - min);
        }

        /// <summary>Whether the corners span a volume, rather than lying on one plane, line or point</summary>
        public static bool HasVolume<T>(in T shape) where T : struct, IBrushShape
        {
            if (shape.HalfEdgeCount == 0 || shape.PolygonCount == 0)
                return false;
            var tolerance = math.max(1f, SizeOf(in shape)) * kFlatTolerance;

            // the corner farthest from the first, then the one farthest from the line through both, then the one farthest
            // from the plane through all three
            var first = shape.VertexOf(0);
            var along = float3.zero; var alongLength = 0f;
            for (int h = 1; h < shape.HalfEdgeCount; h++)
            {
                var offset = shape.VertexOf(h) - first;
                var length = math.lengthsq(offset);
                if (length > alongLength) { alongLength = length; along = offset; }
            }
            if (alongLength <= tolerance * tolerance)
                return false;   // a point
            along = math.normalize(along);

            var across = float3.zero; var acrossLength = 0f;
            for (int h = 1; h < shape.HalfEdgeCount; h++)
            {
                var offset = shape.VertexOf(h) - first;
                offset -= along * math.dot(offset, along);
                var length = math.lengthsq(offset);
                if (length > acrossLength) { acrossLength = length; across = offset; }
            }
            if (acrossLength <= tolerance * tolerance)
                return false;   // a line
            var normal = math.normalize(math.cross(along, across));

            for (int h = 1; h < shape.HalfEdgeCount; h++)
            {
                if (math.abs(math.dot(shape.VertexOf(h) - first, normal)) > tolerance)
                    return true;
            }
            return false;       // flat
        }

        /// <summary>
        /// Whether its faces cross or fold over each other: a face that isn't a simple polygon going round once, a face
        /// with corners on both sides of it, or faces that face opposite ways (one turned out, another in). A brush that is
        /// inside out as a whole is not.
        /// </summary>
        public static bool IsSelfIntersecting<T>(in T shape) where T : struct, IBrushShape
        {
            if (shape.HalfEdgeCount == 0 || shape.PolygonCount == 0)
                return false;
            var size      = math.max(1f, SizeOf(in shape));
            var tolerance = size * kCrossingTolerance;

            var facesOut = false;
            var facesIn  = false;
            for (int p = 0; p < shape.PolygonCount; p++)
            {
                var firstEdge = shape.FirstEdgeOf(p);
                var edgeCount = shape.EdgeCountOf(p);

                // its area: the averaged normal of its corners (Newell's), from its first corner so that corners far from
                // the origin don't cancel each other out
                var origin = shape.VertexOf(firstEdge);
                var normal = float3.zero;
                var center = float3.zero;
                for (int e = 0; e < edgeCount; e++)
                {
                    var a = shape.VertexOf(firstEdge + e);
                    var b = shape.VertexOf(firstEdge + (e + 1) % edgeCount);
                    normal += math.cross(a - origin, b - origin);
                    center += a;
                }
                center /= edgeCount;
                var area = math.length(normal);
                // no area (all its corners on one line): it has no plane to be on either side of
                if (area <= size * tolerance)
                    continue;

                // its plane: the one the brush has for it, which it was made from, else the one through its corners
                float offset;
                if (shape.TryGetPlaneOf(p, out var plane) && math.lengthsq(plane.xyz) > 0)
                {
                    var length = math.length(plane.xyz);
                    normal = plane.xyz / length;
                    offset = plane.w / length;
                } else
                {
                    normal /= area;
                    offset = -math.dot(normal, center);
                }

                if (!GoesRoundOnce(in shape, firstEdge, edgeCount, normal, size * kFlatTolerance))
                    return true;

                var inFront = false;
                var behind  = false;
                for (int h = 0; h < shape.HalfEdgeCount; h++)
                {
                    if (IsCornerOf(in shape, shape.VertexIndexOf(h), firstEdge, edgeCount))
                        continue;
                    var distance = math.dot(shape.VertexOf(h), normal) + offset;
                    if (distance >  tolerance) inFront = true;
                    if (distance < -tolerance) behind  = true;
                }
                if (inFront && behind)
                    return true;
                if (behind)  facesOut = true;
                if (inFront) facesIn  = true;
            }
            return facesOut && facesIn;
        }

        static bool IsCornerOf<T>(in T shape, int vertexIndex, int firstEdge, int edgeCount) where T : struct, IBrushShape
        {
            for (int e = 0; e < edgeCount; e++)
            {
                if (shape.VertexIndexOf(firstEdge + e) == vertexIndex)
                    return true;
            }
            return false;
        }

        // Turns the same way at every corner, all the way round once: not a bow tie, a star, or a polygon with a dent. An
        // edge with its ends in the same place has no direction: the turn is from the edge before it to the one after it
        static bool GoesRoundOnce<T>(in T shape, int firstEdge, int edgeCount, float3 normal, float samePlace) where T : struct, IBrushShape
        {
            var first = -1;
            for (int e = 0; e < edgeCount && first < 0; e++)
            {
                if (math.lengthsq(DirectionOf(in shape, firstEdge, edgeCount, e)) > samePlace * samePlace)
                    first = e;
            }
            if (first < 0)
                return true;    // no edge with a direction: no area, not checked here

            var turned   = 0f;
            var left     = false;
            var right    = false;
            var previous = DirectionOf(in shape, firstEdge, edgeCount, first);
            for (int i = 1; i <= edgeCount; i++)
            {
                var direction = DirectionOf(in shape, firstEdge, edgeCount, (first + i) % edgeCount);
                if (math.lengthsq(direction) <= samePlace * samePlace)
                    continue;
                var turn = math.atan2(math.dot(math.cross(previous, direction), normal), math.dot(previous, direction));
                turned += turn;
                if (turn >  1e-3f) left  = true;
                if (turn < -1e-3f) right = true;
                previous = direction;
            }
            return !(left && right) && math.abs(math.abs(turned) - 2 * math.PI) < 0.5f;
        }

        static float3 DirectionOf<T>(in T shape, int firstEdge, int edgeCount, int edge) where T : struct, IBrushShape
        {
            return shape.VertexOf(firstEdge + (edge + 1) % edgeCount) - shape.VertexOf(firstEdge + edge);
        }
    }
}
