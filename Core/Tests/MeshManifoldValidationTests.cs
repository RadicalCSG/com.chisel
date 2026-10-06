using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;

namespace Chisel.Core.Tests
{
    [TestFixture]
    public class MeshManifoldValidationTests
    {
        static MeshManifoldReport Classify(float3[] vertices, int[] indices, float weldEpsilon = MeshManifoldValidation.kDefaultWeldEpsilon)
        {
            var nativeVertices = new NativeArray<float3>(vertices, Allocator.Temp);
            var nativeIndices  = new NativeArray<int>(indices, Allocator.Temp);
            var report = MeshManifoldValidation.Classify(nativeVertices, nativeIndices, weldEpsilon);
            nativeIndices.Dispose();
            nativeVertices.Dispose();
            return report;
        }

        // ---- shapes ----

        static readonly float3[] kTetrahedronVertices =
        {
            new float3(0, 0, 0), new float3(1, 0, 0), new float3(0, 1, 0), new float3(0, 0, 1)
        };

        static readonly int[] kTetrahedronIndices =
        {
            0, 2, 1,  0, 1, 3,  0, 3, 2,  1, 2, 3
        };

        static readonly float3[] kCubeVertices =
        {
            new float3(0, 0, 0), new float3(1, 0, 0), new float3(1, 1, 0), new float3(0, 1, 0),
            new float3(0, 0, 1), new float3(1, 0, 1), new float3(1, 1, 1), new float3(0, 1, 1)
        };

        // 6 quads, each split into 2 triangles: 18 distinct edges (12 cube edges + 6 face diagonals)
        static readonly int[] kCubeIndices =
        {
            0, 2, 1,  0, 3, 2,      // z = 0
            4, 5, 6,  4, 6, 7,      // z = 1
            0, 4, 7,  0, 7, 3,      // x = 0
            1, 2, 6,  1, 6, 5,      // x = 1
            0, 1, 5,  0, 5, 4,      // y = 0
            3, 7, 6,  3, 6, 2       // y = 1
        };

        static void UnsharedCube(float jitter, out float3[] vertices, out int[] indices)
        {
            var quads = new[]
            {
                new[] { 0, 3, 2, 1 },   // z = 0
                new[] { 4, 5, 6, 7 },   // z = 1
                new[] { 0, 4, 7, 3 },   // x = 0
                new[] { 1, 2, 6, 5 },   // x = 1
                new[] { 0, 1, 5, 4 },   // y = 0
                new[] { 3, 7, 6, 2 }    // y = 1
            };
            vertices = new float3[quads.Length * 4];
            indices  = new int[quads.Length * 6];
            var copies = new int[kCubeVertices.Length];
            for (int q = 0; q < quads.Length; q++)
            {
                int first = q * 4;
                for (int c = 0; c < 4; c++)
                {
                    int index  = first + c;
                    int corner = quads[q][c];
                    vertices[index] = kCubeVertices[corner] + Stray(copies[corner]++, jitter);
                }
                int t = q * 6;
                indices[t + 0] = first;     indices[t + 1] = first + 1; indices[t + 2] = first + 2;
                indices[t + 3] = first;     indices[t + 4] = first + 2; indices[t + 5] = first + 3;
            }
        }

        static float3 Stray(int copy, float amount)
        {
            switch (copy)
            {
                case 0:  return new float3(amount, 0, 0);
                case 1:  return new float3(0, amount, 0);
                default: return new float3(0, 0, amount);
            }
        }

        static float3[] Concat(float3[] a, float3[] b)
        {
            var result = new float3[a.Length + b.Length];
            a.CopyTo(result, 0);
            b.CopyTo(result, a.Length);
            return result;
        }

        static int[] Concat(int[] a, int[] b)
        {
            var result = new int[a.Length + b.Length];
            a.CopyTo(result, 0);
            b.CopyTo(result, a.Length);
            return result;
        }

        // ---- closed shapes ----

        [Test]
        public void Tetrahedron_IsClosedManifold()
        {
            var report = Classify(kTetrahedronVertices, kTetrahedronIndices);
            Assert.IsTrue(report.IsClosedManifold);
            Assert.AreEqual(6, report.edgeCount);
            Assert.AreEqual(2, report.maxEdgeUseCount);
        }

        [Test]
        public void Cube_IsClosedManifold()
        {
            var report = Classify(kCubeVertices, kCubeIndices);
            Assert.IsTrue(report.IsClosedManifold);
            Assert.AreEqual(8,  report.weldedVertexCount);
            Assert.AreEqual(12, report.triangleCount);
            Assert.AreEqual(18, report.edgeCount);
            Assert.AreEqual(0,  report.boundaryEdgeCount);
            Assert.AreEqual(0,  report.nonManifoldEdgeCount);
            Assert.AreEqual(0f, report.boundaryEdgeLength);
        }

        // ---- holes ----

        [Test]
        public void CubeMissingOneFace_HasFourBoundaryEdges()
        {
            // drop the two triangles of the z = 0 face; its diagonal vanishes with them
            var indices = new int[kCubeIndices.Length - 6];
            System.Array.Copy(kCubeIndices, 6, indices, 0, indices.Length);

            var report = Classify(kCubeVertices, indices);
            Assert.IsFalse(report.IsClosedManifold);
            Assert.AreEqual(4,  report.boundaryEdgeCount);
            Assert.AreEqual(17, report.edgeCount);
            Assert.AreEqual(0,  report.nonManifoldEdgeCount);
            // the hole is a unit square, so its rim is four unit-length edges
            Assert.AreEqual(4f, report.boundaryEdgeLength, 1e-5f);
            Assert.AreEqual(1f, report.longestBoundaryEdgeLength, 1e-5f);
        }

        [Test]
        public void BoundaryEdgeLength_DistinguishesAHairlineSeamFromAMissingWall()
        {
            var indices = new int[kCubeIndices.Length - 6];
            System.Array.Copy(kCubeIndices, 6, indices, 0, indices.Length);

            var big = Classify(kCubeVertices, indices, 0f);

            var small = new float3[kCubeVertices.Length];
            for (int i = 0; i < small.Length; i++)
                small[i] = kCubeVertices[i] * 0.001f;
            var tiny = Classify(small, indices, 0f);

            Assert.AreEqual(big.boundaryEdgeCount, tiny.boundaryEdgeCount);
            Assert.Greater(big.boundaryEdgeLength, tiny.boundaryEdgeLength * 100f);
        }

        // ---- duplicated / overlapping surface ----

        [Test]
        public void DuplicatedTriangle_IsNonManifold()
        {
            var indices = Concat(kCubeIndices, new[] { 0, 2, 1 });

            var report = Classify(kCubeVertices, indices);
            Assert.IsFalse(report.IsClosedManifold);
            Assert.AreEqual(0, report.boundaryEdgeCount);
            Assert.AreEqual(3, report.nonManifoldEdgeCount);
            Assert.AreEqual(3, report.maxEdgeUseCount);
        }

        [Test]
        public void DegenerateTriangles_AreCountedAndDoNotContributeEdges()
        {
            var indices = Concat(kCubeIndices, new[] { 0, 0, 1,  3, 3, 3 });

            var report = Classify(kCubeVertices, indices);
            Assert.AreEqual(2, report.degenerateTriangleCount);
            Assert.IsTrue(report.IsClosedManifold);
            Assert.AreEqual(18, report.edgeCount);
        }

        // ---- welding: the reason this test is meaningful on real output ----

        [Test]
        public void SeparatelyComputedVertices_AtTheSamePosition_WeldIntoOneClosedSurface()
        {
            UnsharedCube(1e-6f, out var vertices, out var indices);

            var unwelded = Classify(vertices, indices, 0f);
            Assert.AreEqual(24, unwelded.weldedVertexCount);
            Assert.AreEqual(30, unwelded.edgeCount);            // 6 faces x (4 rim + 1 diagonal)
            Assert.AreEqual(24, unwelded.boundaryEdgeCount);    // every rim edge belongs to one face only
            Assert.IsFalse(unwelded.IsClosedManifold);

            var welded = Classify(vertices, indices, MeshManifoldValidation.kDefaultWeldEpsilon);
            Assert.AreEqual(8,  welded.weldedVertexCount);
            Assert.AreEqual(18, welded.edgeCount);
            Assert.IsTrue(welded.IsClosedManifold);
        }

        [Test]
        public void VerticesFurtherApartThanTheWeldEpsilon_AreNotMerged()
        {
            // the guard against reporting a manifold by fusing things that are genuinely apart
            UnsharedCube(1e-3f, out var vertices, out var indices);

            var report = Classify(vertices, indices, MeshManifoldValidation.kDefaultWeldEpsilon);
            Assert.AreEqual(24, report.weldedVertexCount);
            Assert.AreEqual(24, report.boundaryEdgeCount);
            Assert.IsFalse(report.IsClosedManifold);
        }

        // ---- T-junctions: no gap, still not stitched ----

        [Test]
        public void TJunction_AddsThreeBoundaryEdges_AlthoughTheSurfaceHasNoGap()
        {
            var shared = new[]
            {
                new float3(0, 0, 0), new float3(1, 0, 0), new float3(1, 1, 0), new float3(0, 1, 0),
                new float3(2, 0, 0), new float3(2, 1, 0), new float3(1, 0.5f, 0)
            };

            var stitched  = new[] { 0, 1, 2,  0, 2, 3,             1, 4, 5,  1, 5, 2 };
            var tJunction = new[] { 0, 1, 6,  0, 6, 2,  0, 2, 3,   1, 4, 5,  1, 5, 2 };

            var withoutT = Classify(shared, stitched);
            var withT    = Classify(shared, tJunction);

            Assert.AreEqual(withoutT.boundaryEdgeCount + 3, withT.boundaryEdgeCount);
            Assert.AreEqual(0, withT.nonManifoldEdgeCount);
        }

        // ---- degenerate input ----

        [Test]
        public void EmptyMesh_ReportsNothingRatherThanFailing()
        {
            var report = Classify(new float3[0], new int[0]);
            Assert.AreEqual(0, report.triangleCount);
            Assert.AreEqual(0, report.edgeCount);
            Assert.IsTrue(report.IsClosedManifold);
        }
    }
}
