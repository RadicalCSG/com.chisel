using NUnit.Framework;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;

namespace Chisel.Core.Tests
{
    [TestFixture]
    public class ConvertToPlaneSpaceTests
    {
        static UnsafeList<float3> Verts(params float3[] p)
        {
            var list = new UnsafeList<float3>(p.Length, Allocator.Temp);
            for (int i = 0; i < p.Length; i++) list.Add(p[i]);
            return list;
        }

        static UnsafeList<Edge> Edges(params (int a, int b)[] e)
        {
            var list = new UnsafeList<Edge>(e.Length, Allocator.Temp);
            for (int i = 0; i < e.Length; i++)
                list.Add(new Edge { index1 = (ushort)e[i].a, index2 = (ushort)e[i].b });
            return list;
        }

        static Vertex2DRemapper NewRemapper() => new()
        {
            lookup      = new NativeList<int>(16, Allocator.Temp),
            positions2D = new NativeList<double2>(16, Allocator.Temp),
            edgeIndices = new NativeList<int>(32, Allocator.Temp)
        };

        // A 2x2 square in the z=0 plane whose bottom edge 0->1 is already split at midpoint v4=(1,0,0).
        // 0=(0,0) 1=(2,0) 2=(2,2) 3=(0,2) 4=(1,0).  Edges: 0-4 4-1 1-2 2-3 3-0.
        static UnsafeList<float3> SquareWithMidpoint() =>
            Verts(new float3(0, 0, 0), new float3(2, 0, 0), new float3(2, 2, 0),
                  new float3(0, 2, 0), new float3(1, 0, 0));
        static UnsafeList<Edge> SquareEdgesWithMidpoint() =>
            Edges((0, 4), (4, 1), (1, 2), (2, 3), (3, 0));

        [Test]
        public void CollinearMidpoint_IsRemoved_WhenUnprotected()
        {
            var verts = SquareWithMidpoint();
            var edges = SquareEdgesWithMidpoint();
            var map   = new Map3DTo2D(new float3(0, 0, 1));
            var r     = NewRemapper();

            r.ConvertToPlaneSpace(verts, edges, map);

            // v4 is degree-2 and collinear on 0-1 -> collapsed away, leaving the 4 corners / 4 edges.
            Assert.That(r.positions2D.Length, Is.EqualTo(4), "midpoint should have been collapsed");
            Assert.That(r.edgeIndices.Length / 2, Is.EqualTo(4));

            r.Dispose(); verts.Dispose(); edges.Dispose();
        }

        [Test]
        public void CollinearMidpoint_IsKept_WhenProtected()
        {
            var verts = SquareWithMidpoint();
            var edges = SquareEdgesWithMidpoint();
            var map   = new Map3DTo2D(new float3(0, 0, 1));
            var r     = NewRemapper();

            var protectedVertices = new NativeArray<bool>(verts.Length, Allocator.Temp);
            protectedVertices[4] = true; // the inserted T-junction vertex

            r.ConvertToPlaneSpace(verts, edges, map, protectedVertices);

            // Protected -> v4 survives: 5 vertices, the bottom edge stays split into two.
            Assert.That(r.positions2D.Length, Is.EqualTo(5), "protected midpoint must be kept");
            Assert.That(r.edgeIndices.Length / 2, Is.EqualTo(5));

            protectedVertices.Dispose();
            r.Dispose(); verts.Dispose(); edges.Dispose();
        }

        [Test]
        public void SquareCorners_AreNeverCollapsed()
        {
            // Plain square, no midpoint: every vertex is a real (non-collinear) corner; none collapse,
            // with or without a (here empty) protection mask.
            var verts = Verts(new float3(0, 0, 0), new float3(2, 0, 0), new float3(2, 2, 0), new float3(0, 2, 0));
            var edges = Edges((0, 1), (1, 2), (2, 3), (3, 0));
            var map   = new Map3DTo2D(new float3(0, 0, 1));
            var r     = NewRemapper();

            r.ConvertToPlaneSpace(verts, edges, map);

            Assert.That(r.positions2D.Length, Is.EqualTo(4));
            Assert.That(r.edgeIndices.Length / 2, Is.EqualTo(4));

            r.Dispose(); verts.Dispose(); edges.Dispose();
        }
    }
}
