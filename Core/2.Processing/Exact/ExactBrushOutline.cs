using System.Collections.Generic;
using Unity.Mathematics;

namespace Chisel.Core
{
    static class ExactBrushOutline
    {
        // The box the brush is built in: the exact world (ExactPlane.kMaxDistance is 2^24). A face of it left in the result
        // means the given planes do not close the brush.
        const float kWorld = 1 << 23;

        // False, with the reason, when the planes do not make a brush: they do not close it, enclose nothing, or one of them
        // has no direction.
        public static bool FromPlanes(float4[] planes, out BrushMesh outline, out string problem)
        {
            outline = null;
            problem = null;
            if (planes == null || planes.Length < 4)
            {
                problem = $"a brush needs at least 4 planes, it was given {planes?.Length ?? 0}";
                return false;
            }

            int planeCount = planes.Length + 6;
            var exactInput = new double[planeCount * 4];
            for (int p = 0; p < planes.Length; p++)
            {
                exactInput[p * 4 + 0] = planes[p].x;
                exactInput[p * 4 + 1] = planes[p].y;
                exactInput[p * 4 + 2] = planes[p].z;
                exactInput[p * 4 + 3] = planes[p].w;
            }
            // -x, +x, -y, +y, -z, +z
            for (int b = 0; b < 6; b++)
            {
                int p = planes.Length + b;
                int axis = b / 2;
                double sign = (b % 2 == 0) ? -1 : 1;
                exactInput[p * 4 + axis] = sign;
                exactInput[p * 4 + 3] = -kWorld;
            }

            var vertices      = new List<float>();
            var edgeVertex    = new List<int>();
            var edgeTwin      = new List<int>();
            var faceFirstEdge = new List<int>();
            var faceEdgeCount = new List<int>();
            var facePlane     = new List<int>();
            var result = ExactPolytope.Build(exactInput, planeCount, planes.Length, vertices, edgeVertex, edgeTwin, faceFirstEdge, faceEdgeCount, facePlane);
            switch (result)
            {
                case ExactPolytope.Result.Built:        break;
                case ExactPolytope.Result.Empty:        problem = "the planes enclose no volume"; return false;
                case ExactPolytope.Result.InvalidPlane: problem = "a plane has no direction, or lies beyond the exact world"; return false;
                case ExactPolytope.Result.OutsideWorld: problem = "the planes do not close the brush"; return false;
                default:                                problem = "the planes make no brush (" + result + ")"; return false;
            }
            for (int f = 0; f < facePlane.Count; f++)
            {
                if (facePlane[f] >= planes.Length)
                {
                    problem = "the planes do not close the brush";
                    return false;
                }
            }

            outline = new BrushMesh
            {
                vertices               = new float3[vertices.Count / 3],
                halfEdges              = new BrushMesh.HalfEdge[edgeVertex.Count],
                halfEdgePolygonIndices = new int[edgeVertex.Count],
                polygons               = new BrushMesh.Polygon[faceFirstEdge.Count],
                planes                 = new float4[faceFirstEdge.Count]
            };
            for (int v = 0; v < outline.vertices.Length; v++)
                outline.vertices[v] = new float3(vertices[v * 3], vertices[v * 3 + 1], vertices[v * 3 + 2]);
            for (int e = 0; e < outline.halfEdges.Length; e++)
                outline.halfEdges[e] = new BrushMesh.HalfEdge { vertexIndex = edgeVertex[e], twinIndex = edgeTwin[e] };
            for (int f = 0; f < outline.polygons.Length; f++)
            {
                outline.polygons[f] = new BrushMesh.Polygon
                {
                    firstEdge        = faceFirstEdge[f],
                    edgeCount        = faceEdgeCount[f],
                    descriptionIndex = facePlane[f]
                };
                // the plane as it was given, bit for bit: it is the CSG's input
                outline.planes[f] = planes[facePlane[f]];
                for (int e = faceFirstEdge[f]; e < faceFirstEdge[f] + faceEdgeCount[f]; e++)
                    outline.halfEdgePolygonIndices[e] = f;
            }
            return true;
        }
    }
}
