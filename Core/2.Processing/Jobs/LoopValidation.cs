using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

namespace Chisel.Core
{
    enum LoopDefect : byte
    {
        None = 0,       // valid: every used vertex has out == 1 && in == 1
        Empty,          // fewer than 3 edges - cannot bound an area
        DegenerateEdge, // an edge with index1 == index2
        OpenChain,      // a vertex with degree 1, or a degree-2 fork (out/in unbalanced) -> boundary has a gap
        Pinch,          // a vertex with degree > 2 -> loop touches itself / a chord survived
    }

    static class LoopValidation
    {
        public static LoopDefect Classify(in UnsafeList<Edge> edges, int vertexCount, out int badVertex)
        {
            badVertex = -1;
            if (edges.Length < 3)
                return LoopDefect.Empty;

            var outCount = new NativeArray<int>(vertexCount, Allocator.Temp);
            var inCount  = new NativeArray<int>(vertexCount, Allocator.Temp);

            var defect = LoopDefect.None;
            int found  = -1;

            for (int e = 0; e < edges.Length; e++)
            {
                int a = edges[e].index1;
                int b = edges[e].index2;
                if (a == b)
                {
                    defect = LoopDefect.DegenerateEdge;
                    found  = a;
                    break;
                }
                outCount[a] = outCount[a] + 1;
                inCount[b]  = inCount[b]  + 1;
            }

            if (defect == LoopDefect.None)
            {
                int pinchVertex = -1, openVertex = -1;
                for (int v = 0; v < vertexCount; v++)
                {
                    int o = outCount[v];
                    int i = inCount[v];
                    int degree = o + i;
                    if (degree == 0)
                        continue;
                    if (degree > 2)
                    {
                        if (pinchVertex == -1) pinchVertex = v;
                    }
                    else if (o != 1 || i != 1)
                    {
                        // degree 1 (dangling end) or degree-2 fork (out==2/in==0 etc.)
                        if (openVertex == -1) openVertex = v;
                    }
                }
                // Pinch is reported in preference to OpenChain: a self-touch is the
                // more structurally severe defect and usually the root of the trouble.
                if      (pinchVertex != -1) { defect = LoopDefect.Pinch;     found = pinchVertex; }
                else if (openVertex  != -1) { defect = LoopDefect.OpenChain; found = openVertex; }
            }

            outCount.Dispose();
            inCount.Dispose();
            badVertex = found;
            return defect;
        }

        public static bool IsValid(in UnsafeList<Edge> edges, int vertexCount)
            => Classify(in edges, vertexCount, out _) == LoopDefect.None;
    }
}
