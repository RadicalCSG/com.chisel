using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;

namespace Chisel.Core.Tests
{
    [TestFixture]
    public class BooleanEdgesUtilityTests
    {
        // Inward planes of the axis-aligned box x in [0,1], z in [0,1] (y unconstrained).
        // Convention (see IsOutsidePlanes): a point is inside when dot(plane,(p,1)) <= eps for ALL planes.
        static NativeList<float4> BoxPlanesXZ()
        {
            var p = new NativeList<float4>(4, Allocator.Temp);
            p.Add(new float4(-1, 0,  0,  0)); // x >= 0  -> dot = -x
            p.Add(new float4( 1, 0,  0, -1)); // x <= 1  -> dot = x - 1
            p.Add(new float4( 0, 0, -1,  0)); // z >= 0  -> dot = -z
            p.Add(new float4( 0, 0,  1, -1)); // z <= 1  -> dot = z - 1
            return p;
        }

        static HashedVertices Verts(params float3[] vs)
        {
            var hv = new HashedVertices(math.max(1, vs.Length), Allocator.Temp);
            for (int i = 0; i < vs.Length; i++) hv.AddNoResize(vs[i]);
            return hv;
        }

        static NativeArray<Edge> EdgeArray(params (int a, int b)[] es)
        {
            var arr = new NativeArray<Edge>(es.Length, Allocator.Temp);
            for (int i = 0; i < es.Length; i++)
                arr[i] = new Edge { index1 = (ushort)es[i].a, index2 = (ushort)es[i].b };
            return arr;
        }

        static LoopSegment WholeSegment(int edgeCount, int planeCount)
            => new LoopSegment { edgeOffset = 0, edgeLength = edgeCount, planesOffset = 0, planesLength = planeCount };

        // Vertices: 0,1 = inside (used as the segment edge); 2,3 = inside (off-segment); 4,5 = outside;
        // 6 = inside the fat-plane band (x = 1 - eps/2, i.e. just inside the x<=1 face by < epsilon).
        static HashedVertices Scene() => Verts(
            new float3(0.3f,    0, 0.5f),   // 0 inside
            new float3(0.7f,    0, 0.5f),   // 1 inside
            new float3(0.4f,    0, 0.4f),   // 2 inside
            new float3(0.6f,    0, 0.6f),   // 3 inside
            new float3(2.0f,    0, 0.5f),   // 4 outside (x>1)
            new float3(2.5f,    0, 0.5f),   // 5 outside
            new float3(0.9997f, 0, 0.5f));  // 6 in the fat band of the x<=1 face (dot = -0.0003)

        static void DisposeAll(HashedVertices v, NativeArray<Edge> e, NativeList<float4> p)
        { v.Dispose(); e.Dispose(); p.Dispose(); }

        [Test]
        public void Aligned_WhenEdgeMatchesSegmentEdge_SameDirection()
        {
            var v = Scene(); var e = EdgeArray((0, 1)); var p = BoxPlanesXZ();
            var cat = BooleanEdgesUtility.CategorizeEdge(new Edge { index1 = 0, index2 = 1 }, in p, in e, WholeSegment(1, 4), in v);
            Assert.That(cat, Is.EqualTo(EdgeCategory.Aligned));
            DisposeAll(v, e, p);
        }

        [Test]
        public void ReverseAligned_WhenEdgeMatchesSegmentEdge_OppositeDirection()
        {
            var v = Scene(); var e = EdgeArray((0, 1)); var p = BoxPlanesXZ();
            var cat = BooleanEdgesUtility.CategorizeEdge(new Edge { index1 = 1, index2 = 0 }, in p, in e, WholeSegment(1, 4), in v);
            Assert.That(cat, Is.EqualTo(EdgeCategory.ReverseAligned));
            DisposeAll(v, e, p);
        }

        [Test]
        public void Inside_WhenMidpointInsidePlanes_AndNotAligned()
        {
            var v = Scene(); var e = EdgeArray((0, 1)); var p = BoxPlanesXZ();
            // edge (2,3): midpoint (0.5,0,0.5) is inside the box; not in the segment edge list.
            var cat = BooleanEdgesUtility.CategorizeEdge(new Edge { index1 = 2, index2 = 3 }, in p, in e, WholeSegment(1, 4), in v);
            Assert.That(cat, Is.EqualTo(EdgeCategory.Inside));
            DisposeAll(v, e, p);
        }

        [Test]
        public void Outside_WhenMidpointOutsidePlanes()
        {
            var v = Scene(); var e = EdgeArray((0, 1)); var p = BoxPlanesXZ();
            // edge (4,5): midpoint (2.25,0,0.5) is outside (x>1).
            var cat = BooleanEdgesUtility.CategorizeEdge(new Edge { index1 = 4, index2 = 5 }, in p, in e, WholeSegment(1, 4), in v);
            Assert.That(cat, Is.EqualTo(EdgeCategory.Outside));
            DisposeAll(v, e, p);
        }

        [Test]
        public void Straddle_True_WhenEndpointsOnOppositeSides()
        {
            var v = Scene(); var e = EdgeArray((0, 1)); var p = BoxPlanesXZ();
            // edge (2 inside, 4 outside) crosses the boundary.
            var straddle = BooleanEdgesUtility.EdgeStraddlesSegmentPlanes(new Edge { index1 = 2, index2 = 4 }, in p, WholeSegment(1, 4), in v);
            Assert.That(straddle, Is.True);
            DisposeAll(v, e, p);
        }

        [Test]
        public void Straddle_False_WhenBothInside_OrBothOutside()
        {
            var v = Scene(); var e = EdgeArray((0, 1)); var p = BoxPlanesXZ();
            Assert.That(BooleanEdgesUtility.EdgeStraddlesSegmentPlanes(new Edge { index1 = 2, index2 = 3 }, in p, WholeSegment(1, 4), in v), Is.False);
            Assert.That(BooleanEdgesUtility.EdgeStraddlesSegmentPlanes(new Edge { index1 = 4, index2 = 5 }, in p, WholeSegment(1, 4), in v), Is.False);
            DisposeAll(v, e, p);
        }

        [Test]
        public void StrictCrossing_True_WhenOneStrictlyInside_OtherStrictlyOutside()
        {
            var v = Scene(); var e = EdgeArray((0, 1)); var p = BoxPlanesXZ();
            Assert.That(BooleanEdgesUtility.EdgeStrictlyCrossesSegmentPlanes(new Edge { index1 = 2, index2 = 4 }, in p, WholeSegment(1, 4), in v), Is.True);
            DisposeAll(v, e, p);
        }

        [Test]
        public void StrictCrossing_False_BothInside_OrBothOutside()
        {
            var v = Scene(); var e = EdgeArray((0, 1)); var p = BoxPlanesXZ();
            Assert.That(BooleanEdgesUtility.EdgeStrictlyCrossesSegmentPlanes(new Edge { index1 = 2, index2 = 3 }, in p, WholeSegment(1, 4), in v), Is.False);
            Assert.That(BooleanEdgesUtility.EdgeStrictlyCrossesSegmentPlanes(new Edge { index1 = 4, index2 = 5 }, in p, WholeSegment(1, 4), in v), Is.False);
            DisposeAll(v, e, p);
        }

        [Test]
        public void StrictCrossing_False_ForBandTouch_EvenThoughStraddleIsTrue()
        {
            var v = Scene(); var e = EdgeArray((0, 1)); var p = BoxPlanesXZ();
            var edge = new Edge { index1 = 6, index2 = 4 };
            Assert.That(BooleanEdgesUtility.EdgeStraddlesSegmentPlanes(edge, in p, WholeSegment(1, 4), in v), Is.True);
            Assert.That(BooleanEdgesUtility.EdgeStrictlyCrossesSegmentPlanes(edge, in p, WholeSegment(1, 4), in v), Is.False);
            DisposeAll(v, e, p);
        }
    }
}
