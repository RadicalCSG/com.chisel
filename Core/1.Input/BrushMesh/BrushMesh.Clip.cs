using Unity.Mathematics;

namespace Chisel.Core
{
    sealed partial class BrushMesh
    {
        public enum PlaneSide { Behind, InFront, Both }

        /// <summary>The side of the plane the brush is on, in front where math.dot(plane, new float4(point, 1)) is
        /// positive; Both when the plane passes through it. Vertices on the plane are on neither side.</summary>
        public PlaneSide SideOf(float4 plane)
        {
            plane = Normalized(plane);
            bool inFront = false, behind = false;
            for (int v = 0; v < vertices.Length; v++)
            {
                var distance = math.dot(plane, new float4(vertices[v], 1));
                if (distance > kDistanceEpsilon) inFront = true;
                else if (distance < -kDistanceEpsilon) behind = true;
            }
            if (inFront && behind)
                return PlaneSide.Both;
            return inFront ? PlaneSide.InFront : PlaneSide.Behind;
        }

        /// <summary>The brush cut by the plane, keeping what is behind it (see <see cref="SideOf"/>). Its faces keep their
        /// surfaces, the face the cut makes takes cutDescriptionIndex, and the corners the cut doesn't touch stay where they
        /// are. False when the plane doesn't pass through the brush, which is left as it was.</summary>
        public bool Clip(float4 plane, int cutDescriptionIndex)
        {
            if (vertices == null || polygons == null || halfEdges == null)
                return false;
            plane = Normalized(plane);
            if (SideOf(plane) != PlaneSide.Both)
                return false;

            var clipped = new BrushMesh(this);
            if (!clipped.Cut(new[] { plane }, 1))
                return false;
            clipped.CalculatePlanes();

            // the cut marks its face with the plane's index in the list it was given; it is found by its plane instead
            for (int p = 0; p < clipped.polygons.Length; p++)
            {
                var facePlane = clipped.planes[p];
                if (math.dot(facePlane.xyz, plane.xyz) > kSamePlaneCosine && math.abs(facePlane.w - plane.w) < kSamePlaneDistance)
                    clipped.polygons[p].descriptionIndex = cutDescriptionIndex;
            }

            vertices               = clipped.vertices;
            halfEdges              = clipped.halfEdges;
            halfEdgePolygonIndices = clipped.halfEdgePolygonIndices;
            polygons               = clipped.polygons;
            planes                 = clipped.planes;
            halfEdgeUVs            = null;
            halfEdgeNormals        = null;
            UpdateHalfEdgePolygonIndices();
            return true;
        }

        static float4 Normalized(float4 plane)
        {
            var length = math.length(plane.xyz);
            return length > 0 ? plane / length : plane;
        }
    }
}
