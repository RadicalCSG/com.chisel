using Unity.Collections;
using Unity.Mathematics;

namespace Chisel.Core
{
    struct MeshManifoldReport
    {
        public int    inputVertexCount;         // before welding; meshes duplicate vertices per submesh
        public int    weldedVertexCount;
        public int    triangleCount;
        public int    degenerateTriangleCount;  // two or three corners welded together, so they bound no area
        public int    edgeCount;
        public int    boundaryEdgeCount;        // used by exactly one triangle
        public int    nonManifoldEdgeCount;     // used by more than two
        public int    maxEdgeUseCount;
        public float  boundaryEdgeLength;       // total, so a hairline seam and a missing wall are distinguishable
        public float  longestBoundaryEdgeLength;
        public float3 longestBoundaryEdgeFrom;  // deterministic - the single worst offender, to look at first
        public float3 longestBoundaryEdgeTo;

        public readonly bool IsClosedManifold => boundaryEdgeCount == 0 && nonManifoldEdgeCount == 0;
    }

    static class MeshManifoldValidation
    {
        public const float kDefaultWeldEpsilon = 0.0001f;

        // Cell size never goes below this, so a weldEpsilon of 0 cannot produce a degenerate grid.
        const float kMinCellSize = 1e-6f;

        public static MeshManifoldReport Classify(NativeArray<float3> vertices, NativeArray<int> indices, float weldEpsilon)
        {
            var report = new MeshManifoldReport
            {
                inputVertexCount = vertices.Length,
                triangleCount    = indices.Length / 3
            };
            if (vertices.Length == 0 || report.triangleCount == 0)
                return report;

            var canonical = new NativeArray<int>(vertices.Length, Allocator.Temp);
            var positions = new NativeList<float3>(vertices.Length, Allocator.Temp);
            Weld(vertices, weldEpsilon, canonical, positions);
            report.weldedVertexCount = positions.Length;

            var edgeUse = new NativeParallelHashMap<int2, int>(report.triangleCount * 3, Allocator.Temp);
            for (int t = 0; t + 2 < indices.Length; t += 3)
            {
                int a = canonical[indices[t]], b = canonical[indices[t + 1]], c = canonical[indices[t + 2]];
                if (a == b || b == c || a == c)
                {
                    report.degenerateTriangleCount++;
                    continue;
                }
                CountEdge(ref edgeUse, a, b);
                CountEdge(ref edgeUse, b, c);
                CountEdge(ref edgeUse, c, a);
            }

            var edges = edgeUse.GetKeyValueArrays(Allocator.Temp);
            report.edgeCount = edges.Length;
            for (int e = 0; e < edges.Length; e++)
            {
                int used = edges.Values[e];
                if (used > report.maxEdgeUseCount)
                    report.maxEdgeUseCount = used;
                if (used == 2)
                    continue;
                if (used > 2)
                {
                    report.nonManifoldEdgeCount++;
                    continue;
                }

                var key    = edges.Keys[e];
                var from   = positions[key.x];
                var to     = positions[key.y];
                var length = math.distance(from, to);
                report.boundaryEdgeCount++;
                report.boundaryEdgeLength += length;
                if (length > report.longestBoundaryEdgeLength)
                {
                    report.longestBoundaryEdgeLength = length;
                    report.longestBoundaryEdgeFrom   = from;
                    report.longestBoundaryEdgeTo     = to;
                }
            }

            edges.Dispose();
            edgeUse.Dispose();
            positions.Dispose();
            canonical.Dispose();
            return report;
        }

        public static MeshManifoldReport Classify(NativeArray<float3> vertices, NativeArray<int> indices)
            => Classify(vertices, indices, kDefaultWeldEpsilon);

        static void Weld(NativeArray<float3> vertices, float weldEpsilon, NativeArray<int> canonical, NativeList<float3> positions)
        {
            var cellSize   = math.max(weldEpsilon, kMinCellSize);
            var sqrEpsilon = weldEpsilon * weldEpsilon;
            var nextInCell = new NativeArray<int>(vertices.Length, Allocator.Temp);
            var cellHead   = new NativeParallelHashMap<int3, int>(vertices.Length, Allocator.Temp);

            for (int i = 0; i < vertices.Length; i++)
            {
                var position = vertices[i];
                var cell     = (int3)math.floor(position / cellSize);
                int match    = -1;
                for (int x = -1; x <= 1 && match < 0; x++)
                for (int y = -1; y <= 1 && match < 0; y++)
                for (int z = -1; z <= 1 && match < 0; z++)
                {
                    if (!cellHead.TryGetValue(cell + new int3(x, y, z), out int walk))
                        continue;
                    while (walk != -1)
                    {
                        if (math.distancesq(positions[walk], position) <= sqrEpsilon)
                        {
                            match = walk;
                            break;
                        }
                        walk = nextInCell[walk];
                    }
                }
                if (match < 0)
                {
                    match = positions.Length;
                    positions.Add(position);
                    if (!cellHead.TryGetValue(cell, out int head))
                        head = -1;
                    nextInCell[match] = head;
                    cellHead[cell]    = match;
                }
                canonical[i] = match;
            }

            cellHead.Dispose();
            nextInCell.Dispose();
        }

        static void CountEdge(ref NativeParallelHashMap<int2, int> edgeUse, int a, int b)
        {
            var key = a < b ? new int2(a, b) : new int2(b, a);
            edgeUse.TryGetValue(key, out int used);
            edgeUse[key] = used + 1;
        }
    }
}
