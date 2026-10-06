using System.Globalization;
using System.Text;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;

namespace Chisel.Core
{
    static class CSGTrace
    {
        internal static StringBuilder Sink;

        static string F(float value) => value.ToString("0.#####", CultureInfo.InvariantCulture);

        internal static bool Enabled => Sink != null;

        internal static void Line(string text)
        {
            var sink = Sink;
            if (sink == null)
                return;
            lock (sink)
                sink.AppendLine(text);
        }

        static string Edges(in UnsafeList<Edge> edges)
        {
            if (!edges.IsCreated)
                return "(none)";
            var text = new StringBuilder();
            for (int e = 0; e < edges.Length; e++)
            {
                if (e > 0) text.Append(' ');
                text.Append(edges[e].index1).Append('>').Append(edges[e].index2);
            }
            return text.ToString();
        }

        internal static void Vertices(int brush, in HashedVertices vertices)
        {
            if (Sink == null)
                return;
            var text = new StringBuilder();
            text.Append("V b").Append(brush).Append(':');
            for (int v = 0; v < vertices.Length; v++)
            {
                var p = vertices[v];
                text.Append(' ').Append(v).Append('(').Append(F(p.x)).Append(',').Append(F(p.y)).Append(',').Append(F(p.z)).Append(')');
            }
            Line(text.ToString());
        }

        internal static void Loop(char stage, int brush, int surface, int loop, int category, int otherBrush, in UnsafeList<Edge> edges)
        {
            if (Sink == null)
                return;
            Line($"{stage} b{brush} s{surface} l{loop} c{category} o{otherBrush}: {Edges(in edges)}");
        }

        internal static void Route(int brush, int surface, int step, int loop, int inCategory, int intersectionCategory,
                                   int otherBrush, int intersectionLength, bool overlap, int outCategory, int cutCategory)
        {
            if (Sink == null)
                return;
            Line($"R b{brush} s{surface} r{step} l{loop} in{inCategory} ic{intersectionCategory} o{otherBrush} n{intersectionLength} " +
                 (overlap ? $"OVERLAP ->{outCategory}" : $"outside->{outCategory} cut->{cutCategory}"));
        }

        internal static void Loops(char stage, int brush, int surface, in UnsafeList<int> loopIndices,
                                   in NativeList<UnsafeList<int>> holeIndices, in NativeList<IndexSurfaceInfo> infos,
                                   in NativeList<UnsafeList<Edge>> allEdges)
        {
            if (Sink == null)
                return;
            for (int l = 0; l < loopIndices.Length; l++)
            {
                var index = loopIndices[l];
                var info  = infos[index];
                Loop(stage, brush, surface, index, info.interiorCategory, info.brushIndexOrder.nodeOrder, allEdges[index]);
                if (index >= holeIndices.Length || !holeIndices[index].IsCreated)
                    continue;
                var holes = holeIndices[index];
                for (int h = 0; h < holes.Length; h++)
                {
                    var hole     = holes[h];
                    var holeInfo = infos[hole];
                    Loop('H', brush, surface, hole, holeInfo.interiorCategory, holeInfo.brushIndexOrder.nodeOrder, allEdges[hole]);
                }
            }
        }
    }
}
