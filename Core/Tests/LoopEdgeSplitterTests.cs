using NUnit.Framework;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;

namespace Chisel.Core.Tests
{
    [TestFixture]
    public class LoopEdgeSplitterTests
    {
        const float kEdgeEps = 0.001f;

        static UnsafeList<Edge> Edges(params (int a, int b)[] e)
        {
            var list = new UnsafeList<Edge>(math.max(1, e.Length), Allocator.Temp);
            for (int i = 0; i < e.Length; i++)
                list.Add(new Edge { index1 = (ushort)e[i].a, index2 = (ushort)e[i].b });
            return list;
        }

        static NativeArray<float3> Positions(params float3[] p)
        {
            var arr = new NativeArray<float3>(p.Length, Allocator.Temp);
            for (int i = 0; i < p.Length; i++) arr[i] = p[i];
            return arr;
        }

        static NativeArray<ushort> AllIndices(int count)
        {
            var arr = new NativeArray<ushort>(count, Allocator.Temp);
            for (int i = 0; i < count; i++) arr[i] = (ushort)i;
            return arr;
        }

        static bool HasEdge(in UnsafeList<Edge> edges, int a, int b)
        {
            for (int i = 0; i < edges.Length; i++)
                if (edges[i].index1 == a && edges[i].index2 == b) return true;
            return false;
        }

        [Test]
        public void SplitsEdgeAtMidpointVertex()
        {
            // vertices: 0=(0,0,0) 1=(2,0,0) 2=(1,0,0) on the 0->1 segment.
            var pos   = Positions(new float3(0, 0, 0), new float3(2, 0, 0), new float3(1, 0, 0));
            var cand  = AllIndices(3);
            var edges = Edges((0, 1));

            LoopEdgeSplitter.SplitEdgesAtVertices(ref edges, in pos, in cand, cand.Length, kEdgeEps);

            Assert.That(edges.Length, Is.EqualTo(2));
            Assert.That(HasEdge(in edges, 0, 2), Is.True);
            Assert.That(HasEdge(in edges, 2, 1), Is.True);
            edges.Dispose(); pos.Dispose(); cand.Dispose();
        }

        [Test]
        public void SplitsEdgeAtTwoVerticesInOrder()
        {
            // 0=(0,0,0) 1=(3,0,0); on-segment 2=(2,0,0) and 3=(1,0,0) (given out of order).
            var pos   = Positions(new float3(0, 0, 0), new float3(3, 0, 0), new float3(2, 0, 0), new float3(1, 0, 0));
            var cand  = AllIndices(4);
            var edges = Edges((0, 1));

            LoopEdgeSplitter.SplitEdgesAtVertices(ref edges, in pos, in cand, cand.Length, kEdgeEps);

            // Expect 0 -> 3 -> 2 -> 1 (sorted along the edge).
            Assert.That(edges.Length, Is.EqualTo(3));
            Assert.That(HasEdge(in edges, 0, 3), Is.True);
            Assert.That(HasEdge(in edges, 3, 2), Is.True);
            Assert.That(HasEdge(in edges, 2, 1), Is.True);
            edges.Dispose(); pos.Dispose(); cand.Dispose();
        }

        [Test]
        public void DoesNotSplitWhenVertexOffSegment()
        {
            var pos   = Positions(new float3(0, 0, 0), new float3(2, 0, 0), new float3(1, 1, 0)); // off the line
            var cand  = AllIndices(3);
            var edges = Edges((0, 1));

            LoopEdgeSplitter.SplitEdgesAtVertices(ref edges, in pos, in cand, cand.Length, kEdgeEps);

            Assert.That(edges.Length, Is.EqualTo(1));
            Assert.That(HasEdge(in edges, 0, 1), Is.True);
            edges.Dispose(); pos.Dispose(); cand.Dispose();
        }

        [Test]
        public void DoesNotSplitAtEndpoint()
        {
            // vertex 2 coincides with endpoint 1 - must not produce a zero-length edge.
            var pos   = Positions(new float3(0, 0, 0), new float3(2, 0, 0), new float3(2, 0, 0));
            var cand  = AllIndices(3);
            var edges = Edges((0, 1));

            LoopEdgeSplitter.SplitEdgesAtVertices(ref edges, in pos, in cand, cand.Length, kEdgeEps);

            Assert.That(edges.Length, Is.EqualTo(1));
            Assert.That(HasEdge(in edges, 0, 1), Is.True);
            edges.Dispose(); pos.Dispose(); cand.Dispose();
        }

        [Test]
        public void SharedEdgeIsSplitIdenticallyInBothDirections()
        {
            // The crack-safety property: an edge shared by two surfaces appears as (0->1) on one and
            // (1->0) on the other. Both must gain vertex 2, or the surfaces crack apart.
            var pos = Positions(new float3(0, 0, 0), new float3(2, 0, 0), new float3(1, 0, 0));
            var cand = AllIndices(3);

            var fwd = Edges((0, 1));
            var rev = Edges((1, 0));
            LoopEdgeSplitter.SplitEdgesAtVertices(ref fwd, in pos, in cand, cand.Length, kEdgeEps);
            LoopEdgeSplitter.SplitEdgesAtVertices(ref rev, in pos, in cand, cand.Length, kEdgeEps);

            Assert.That(HasEdge(in fwd, 0, 2), Is.True);
            Assert.That(HasEdge(in fwd, 2, 1), Is.True);
            Assert.That(HasEdge(in rev, 1, 2), Is.True);
            Assert.That(HasEdge(in rev, 2, 0), Is.True);
            fwd.Dispose(); rev.Dispose(); pos.Dispose(); cand.Dispose();
        }

        [Test]
        public void TJunctionQuadBecomesValidSimpleLoop()
        {
            // Quad 0-1-2-3 with vertex 4 sitting on edge 1->2. After splitting, the loop is still a
            // single simple cycle, now a pentagon 0->1->4->2->3->0.
            var pos = Positions(
                new float3(0, 0, 0),   // 0
                new float3(2, 0, 0),   // 1
                new float3(2, 2, 0),   // 2
                new float3(0, 2, 0),   // 3
                new float3(2, 1, 0));  // 4 on edge 1->2
            var cand = AllIndices(5);
            var edges = Edges((0, 1), (1, 2), (2, 3), (3, 0));

            LoopEdgeSplitter.SplitEdgesAtVertices(ref edges, in pos, in cand, cand.Length, kEdgeEps);

            Assert.That(edges.Length, Is.EqualTo(5));
            Assert.That(HasEdge(in edges, 1, 4), Is.True);
            Assert.That(HasEdge(in edges, 4, 2), Is.True);
            Assert.That(LoopValidation.Classify(in edges, 5, out _), Is.EqualTo(LoopDefect.None));
            edges.Dispose(); pos.Dispose(); cand.Dispose();
        }

        [Test]
        public void EmptyOrNoCandidates_LeavesEdgesUnchanged()
        {
            var pos = Positions(new float3(0, 0, 0), new float3(2, 0, 0));
            var cand = AllIndices(2);
            var edges = Edges((0, 1));
            LoopEdgeSplitter.SplitEdgesAtVertices(ref edges, in pos, in cand, cand.Length, kEdgeEps);
            Assert.That(edges.Length, Is.EqualTo(1));
            edges.Dispose(); pos.Dispose(); cand.Dispose();
        }

        // ---- RemoveAntiparallelEdgePairs (the figure-eight / slit class) ----

        [Test]
        public void RemovesAntiparallelPair_LeavingValidCycle()
        {
            // Valid quad 55-21-11-2 plus the slit 3<->55 at hub vertex 55.
            var loop = Edges((3, 55), (55, 21), (21, 11), (11, 2), (2, 55), (55, 3));
            LoopEdgeSplitter.RemoveAntiparallelEdgePairs(ref loop);

            Assert.That(loop.Length, Is.EqualTo(4));
            Assert.That(HasEdge(in loop, 3, 55), Is.False);
            Assert.That(HasEdge(in loop, 55, 3), Is.False);
            Assert.That(LoopValidation.Classify(in loop, 56, out _), Is.EqualTo(LoopDefect.None));
            loop.Dispose();
        }

        [Test]
        public void RemovesMultipleAntiparallelPairs()
        {
            // Triangle 0-1-2 with two slits: 0<->3 and 1<->4.
            var loop = Edges((0, 1), (1, 2), (2, 0), (0, 3), (3, 0), (1, 4), (4, 1));
            LoopEdgeSplitter.RemoveAntiparallelEdgePairs(ref loop);

            Assert.That(loop.Length, Is.EqualTo(3));
            Assert.That(LoopValidation.Classify(in loop, 5, out _), Is.EqualTo(LoopDefect.None));
            loop.Dispose();
        }

        [Test]
        public void LeavesCleanLoopUntouched()
        {
            var loop = Edges((0, 1), (1, 2), (2, 3), (3, 0));
            LoopEdgeSplitter.RemoveAntiparallelEdgePairs(ref loop);
            Assert.That(loop.Length, Is.EqualTo(4));
            Assert.That(LoopValidation.Classify(in loop, 4, out _), Is.EqualTo(LoopDefect.None));
            loop.Dispose();
        }

        // ---- RemoveSimpleChords (the fork/sink class) ----

        [Test]
        public void RemovesInteriorChord_LeavingValidPolygon()
        {
            // Hexagon 0-9-7-1-2-3-0 plus the one-way diagonal 7->0.
            var loop = Edges((0, 9), (9, 7), (7, 1), (1, 2), (2, 3), (3, 0), (7, 0));
            LoopEdgeSplitter.RemoveSimpleChords(ref loop);

            Assert.That(loop.Length, Is.EqualTo(6));
            Assert.That(HasEdge(in loop, 7, 0), Is.False);
            Assert.That(LoopValidation.Classify(in loop, 10, out _), Is.EqualTo(LoopDefect.None));
            loop.Dispose();
        }

        [Test]
        public void RemoveSimpleChords_LeavesCleanLoopUntouched()
        {
            var loop = Edges((0, 1), (1, 2), (2, 3), (3, 0));
            LoopEdgeSplitter.RemoveSimpleChords(ref loop);
            Assert.That(loop.Length, Is.EqualTo(4));
            Assert.That(LoopValidation.Classify(in loop, 4, out _), Is.EqualTo(LoopDefect.None));
            loop.Dispose();
        }

        [Test]
        public void RemoveSimpleChords_LeavesTangledLoopUntouched()
        {
            // A bowtie (two triangles sharing vertex 0) is NOT the single-chord pattern, so it must
            // be left alone rather than risk cutting a real edge.
            var loop = Edges((0, 1), (1, 2), (2, 0), (0, 3), (3, 4), (4, 0));
            LoopEdgeSplitter.RemoveSimpleChords(ref loop);
            Assert.That(loop.Length, Is.EqualTo(6));
            loop.Dispose();
        }

        // ---- CloseSingleOpenChain (the over-deletion open-chain class) ----

        [Test]
        public void ClosesSingleOpenChain_IntoValidPolygon()
        {
            // Open path 0->1->2->3 (the quad lost its closing edge 3->0).
            var loop = Edges((0, 1), (1, 2), (2, 3));
            var closed = LoopEdgeSplitter.CloseSingleOpenChain(ref loop);
            Assert.That(closed, Is.True);
            Assert.That(loop.Length, Is.EqualTo(4));
            Assert.That(HasEdge(in loop, 3, 0), Is.True);
            Assert.That(LoopValidation.Classify(in loop, 4, out _), Is.EqualTo(LoopDefect.None));
            loop.Dispose();
        }

        [Test]
        public void CloseSingleOpenChain_LeavesClosedLoopUntouched()
        {
            var loop = Edges((0, 1), (1, 2), (2, 3), (3, 0));
            var closed = LoopEdgeSplitter.CloseSingleOpenChain(ref loop);
            Assert.That(closed, Is.False);
            Assert.That(loop.Length, Is.EqualTo(4));
            loop.Dispose();
        }

        [Test]
        public void CloseSingleOpenChain_RefusesDisconnected_CyclePlusDanglingEdge()
        {
            // Triangle 0-1-2 (closed) PLUS a dangling edge 3->4: 2 dangling ends but TWO components.
            // Closing 4->3 would be wrong, so it must refuse.
            var loop = Edges((0, 1), (1, 2), (2, 0), (3, 4));
            var closed = LoopEdgeSplitter.CloseSingleOpenChain(ref loop);
            Assert.That(closed, Is.False);
            Assert.That(loop.Length, Is.EqualTo(4));
            loop.Dispose();
        }

        [Test]
        public void CloseSingleOpenChain_RefusesFragmented()
        {
            // Two separate open paths (4 dangling ends) - not a single chain.
            var loop = Edges((0, 1), (1, 2), (5, 6), (6, 7));
            var closed = LoopEdgeSplitter.CloseSingleOpenChain(ref loop);
            Assert.That(closed, Is.False);
            loop.Dispose();
        }

        [Test]
        public void RemoveTinyComponents_DropsStrayFragments_KeepsMainAndHoles()
        {
            // Outer quad 0-1-2-3 + a real hole triangle 4-5-6 (both >=3 edges) + a stray 1-edge
            // fragment 7-8 (a weld-artifact sliver). Only the stray must be dropped.
            var loop = Edges((0, 1), (1, 2), (2, 3), (3, 0),  (4, 5), (5, 6), (6, 4),  (7, 8));
            var removed = LoopEdgeSplitter.RemoveTinyComponents(ref loop);
            Assert.That(removed, Is.True);
            Assert.That(loop.Length, Is.EqualTo(7));
            Assert.That(HasEdge(in loop, 7, 8), Is.False);
            Assert.That(HasEdge(in loop, 4, 5), Is.True); // hole preserved
            loop.Dispose();
        }

        [Test]
        public void RemoveTinyComponents_ThenClose_RecoversFragmentedChain()
        {
            // A main open chain 0-1-2-3 missing its close, plus a stray edge 5-6.
            var loop = Edges((0, 1), (1, 2), (2, 3), (5, 6));
            LoopEdgeSplitter.RemoveTinyComponents(ref loop);   // drops 5-6 (1-edge component)
            var closed = LoopEdgeSplitter.CloseSingleOpenChain(ref loop); // closes 0-1-2-3
            Assert.That(closed, Is.True);
            Assert.That(LoopValidation.Classify(in loop, 4, out _), Is.EqualTo(LoopDefect.None));
            loop.Dispose();
        }

        [Test]
        public void RemoveTinyComponents_LeavesCleanLoopUntouched()
        {
            var loop = Edges((0, 1), (1, 2), (2, 3), (3, 0));
            var removed = LoopEdgeSplitter.RemoveTinyComponents(ref loop);
            Assert.That(removed, Is.False);
            Assert.That(loop.Length, Is.EqualTo(4));
            loop.Dispose();
        }

        [Test]
        public void ClosesOpenChain_WithInconsistentEdgeDirections()
        {
            // Undirected simple path 0-1-2-3 but with some edges reversed (the hole-flip case):
            // 0->1, 2->1, 2->3. Must reconstruct a consistently-wound closed cycle over {0,1,2,3}.
            var loop = Edges((0, 1), (2, 1), (2, 3));
            var closed = LoopEdgeSplitter.CloseSingleOpenChain(ref loop);
            Assert.That(closed, Is.True);
            Assert.That(loop.Length, Is.EqualTo(4));
            Assert.That(LoopValidation.Classify(in loop, 4, out _), Is.EqualTo(LoopDefect.None));
            loop.Dispose();
        }
    }
}
