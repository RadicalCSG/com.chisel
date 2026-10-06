using NUnit.Framework;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

namespace Chisel.Core.Tests
{
    [TestFixture]
    public class LoopValidationTests
    {
        // Builds a Temp UnsafeList<Edge> from (index1, index2) pairs. Caller disposes.
        static UnsafeList<Edge> Loop(params (int a, int b)[] edges)
        {
            var list = new UnsafeList<Edge>(edges.Length, Allocator.Temp);
            for (int i = 0; i < edges.Length; i++)
                list.Add(new Edge { index1 = (ushort)edges[i].a, index2 = (ushort)edges[i].b });
            return list;
        }

        static int VertexCount(in UnsafeList<Edge> edges)
        {
            int max = 0;
            for (int i = 0; i < edges.Length; i++)
            {
                if (edges[i].index1 > max) max = edges[i].index1;
                if (edges[i].index2 > max) max = edges[i].index2;
            }
            return max + 1;
        }

        static LoopDefect Classify(in UnsafeList<Edge> edges)
            => LoopValidation.Classify(in edges, VertexCount(in edges), out _);

        // ---- valid shapes ----

        [Test]
        public void Triangle_IsValid()
        {
            var loop = Loop((0, 1), (1, 2), (2, 0));
            Assert.That(Classify(in loop), Is.EqualTo(LoopDefect.None));
            loop.Dispose();
        }

        [Test]
        public void Quad_IsValid()
        {
            var loop = Loop((0, 1), (1, 2), (2, 3), (3, 0));
            Assert.That(Classify(in loop), Is.EqualTo(LoopDefect.None));
            loop.Dispose();
        }

        [Test]
        public void OuterPlusVertexDisjointHole_IsValid()
        {
            // A surface with a hole is two vertex-disjoint cycles - legitimate output,
            // not a defect, even though it is not a single connected loop.
            var loop = Loop((0, 1), (1, 2), (2, 3), (3, 0),   // outer
                            (4, 5), (5, 6), (6, 4));          // hole
            Assert.That(Classify(in loop), Is.EqualTo(LoopDefect.None));
            loop.Dispose();
        }

        // ---- Mode A: open chains ----

        [Test]
        public void OpenChain_DanglingEnds_IsOpenChain()
        {
            // 12-13 10-11 11-12  -> path 10->11->12->13, never closes.
            var loop = Loop((12, 13), (10, 11), (11, 12));
            Assert.That(Classify(in loop), Is.EqualTo(LoopDefect.OpenChain));
            loop.Dispose();
        }

        [Test]
        public void TwoDisjointOpenChains_IsOpenChain()
        {
            // The boundary fragmented into two unconnected paths.
            var loop = Loop((0, 1), (1, 2),   // path 0->1->2
                            (3, 4), (4, 5));  // path 3->4->5
            Assert.That(Classify(in loop), Is.EqualTo(LoopDefect.OpenChain));
            loop.Dispose();
        }

        [Test]
        public void DegreeTwoFork_IsOpenChain()
        {
            var loop = Loop((0, 1), (0, 2), (1, 2));
            Assert.That(Classify(in loop), Is.EqualTo(LoopDefect.OpenChain));
            loop.Dispose();
        }

        // ---- Mode B: pinches / surviving chords ----

        [Test]
        public void BowtieSharedVertices_IsPinch()
        {
            // 0-5 5-3 2-3 3-0 0-2  -> vertices 0 and 3 reach degree 3.
            var loop = Loop((0, 5), (5, 3), (2, 3), (3, 0), (0, 2));
            Assert.That(Classify(in loop), Is.EqualTo(LoopDefect.Pinch));
            loop.Dispose();
        }

        [Test]
        public void QuadWithInteriorChord_IsPinch()
        {
            // 2-3 3-0 0-4 4-2 4-3  -> quad 2-3-0-4 plus diagonal 4-3.
            var loop = Loop((2, 3), (3, 0), (0, 4), (4, 2), (4, 3));
            Assert.That(Classify(in loop), Is.EqualTo(LoopDefect.Pinch));
            loop.Dispose();
        }

        // ---- degenerate / empty ----

        [Test]
        public void SelfEdge_IsDegenerateEdge()
        {
            var loop = Loop((0, 1), (1, 2), (2, 2));
            Assert.That(Classify(in loop), Is.EqualTo(LoopDefect.DegenerateEdge));
            loop.Dispose();
        }

        [Test]
        public void FewerThanThreeEdges_IsEmpty()
        {
            var loop = Loop((0, 1), (1, 0));
            Assert.That(Classify(in loop), Is.EqualTo(LoopDefect.Empty));
            loop.Dispose();
        }

        [Test]
        public void Classify_ReportsOffendingVertex()
        {
            var loop = Loop((0, 5), (5, 3), (2, 3), (3, 0), (0, 2));
            var defect = LoopValidation.Classify(in loop, VertexCount(in loop), out int badVertex);
            Assert.That(defect, Is.EqualTo(LoopDefect.Pinch));
            Assert.That(badVertex, Is.EqualTo(0).Or.EqualTo(3));
            loop.Dispose();
        }

        [Test]
        public void IsValid_AgreesWithClassify()
        {
            var good = Loop((0, 1), (1, 2), (2, 0));
            var bad  = Loop((0, 1), (1, 2), (2, 3));
            Assert.That(LoopValidation.IsValid(in good, VertexCount(in good)), Is.True);
            Assert.That(LoopValidation.IsValid(in bad, VertexCount(in bad)), Is.False);
            good.Dispose();
            bad.Dispose();
        }
    }
}
