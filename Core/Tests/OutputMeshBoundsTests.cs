using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace Chisel.Core.Tests
{
    [TestFixture]
    public class OutputMeshBoundsTests
    {
        [Test]
        public void ACopyThatExactlyFillsTheBuffer_Fits()
        {
            Assert.That(ChiselOutputMeshValidation.FitsInBuffer(0, 10, 10), Is.True);
            Assert.That(ChiselOutputMeshValidation.FitsInBuffer(7, 3, 10), Is.True);
        }

        [Test]
        public void ACopyOnePastTheEnd_DoesNotFit()
        {
            // the exact shape of the original failure: writing index N into an N-length buffer
            Assert.That(ChiselOutputMeshValidation.FitsInBuffer(0, 11, 10), Is.False);
            Assert.That(ChiselOutputMeshValidation.FitsInBuffer(10, 1, 10), Is.False);
            Assert.That(ChiselOutputMeshValidation.FitsInBuffer(8, 3, 10), Is.False);
        }

        [Test]
        public void AnEmptyCopy_Fits_EvenAtTheveryEnd()
        {
            Assert.That(ChiselOutputMeshValidation.FitsInBuffer(10, 0, 10), Is.True);
            Assert.That(ChiselOutputMeshValidation.FitsInBuffer(0, 0, 0), Is.True);
        }

        [Test]
        public void NegativeOffsetsOrCounts_DoNotFit()
        {
            Assert.That(ChiselOutputMeshValidation.FitsInBuffer(-1, 1, 10), Is.False);
            Assert.That(ChiselOutputMeshValidation.FitsInBuffer(0, -1, 10), Is.False);
        }

        [Test]
        public void NothingFitsInAZeroLengthBuffer_ExceptAnEmptyCopy()
        {
            Assert.That(ChiselOutputMeshValidation.FitsInBuffer(0, 1, 0), Is.False);
            Assert.That(ChiselOutputMeshValidation.FitsInBuffer(0, 0, 0), Is.True);
        }
    }

    [TestFixture]
    public class VisibilityMeshGenerationTests
    {
        // every brush visible, so nothing is filtered out for the wrong reason
        struct AllVisible : IBrushVisibilityLookup
        {
            public bool IsBrushVisible(CompactNodeID brushID) { return true; }
            public bool IsBrushVisible(ulong entityID) { return true; }
        }

        static ManagedSubMeshTriangleLookup LookupFor(int triangleCount)
        {
            var lookup = new ManagedSubMeshTriangleLookup
            {
                perTriangleNodeIDLookup       = new CompactNodeID[triangleCount],
                perTriangleSelectionIDLookup  = new int[triangleCount],
                perTriangleSurfaceIndexLookup = new int[triangleCount],
                selectionIndexDescriptions    = new SelectionDescription[1],
                validTriangleCount            = triangleCount
            };
            return lookup;
        }

        [Test]
        public void ASourceMeshWithTrianglesButNoVertices_ProducesAnEmptyMeshInsteadOfThrowing()
        {
            var src = new Mesh { indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            // declare a sub-mesh whose indices cannot possibly be valid - no vertices were ever set
            src.SetVertices(new Vector3[0]);
            src.subMeshCount = 1;
            src.SetIndexBufferParams(3, UnityEngine.Rendering.IndexFormat.UInt32);
            src.SetIndexBufferData(new int[] { 0, 1, 2 }, 0, 0, 3, UnityEngine.Rendering.MeshUpdateFlags.DontValidateIndices);
            src.SetSubMesh(0, new UnityEngine.Rendering.SubMeshDescriptor(0, 3),
                           UnityEngine.Rendering.MeshUpdateFlags.DontValidateIndices | UnityEngine.Rendering.MeshUpdateFlags.DontRecalculateBounds);

            var dst = new Mesh();
            var lookup = LookupFor(1);

            Assert.DoesNotThrow(() => lookup.GenerateSubMesh(new AllVisible(), src, dst));
            Assert.That(dst.vertexCount, Is.EqualTo(0), "an unusable source must not leave geometry behind");

            Object.DestroyImmediate(src);
            Object.DestroyImmediate(dst);
        }

        [Test]
        public void OneOutOfRangeTriangle_DoesNotDiscardTheWholeSubMesh()
        {
            // SetTriangles rejects an ENTIRE sub-mesh if any single index is out of range, so a lone
            // bad triangle used to cost every good one alongside it.
            var src = new Mesh { indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            src.SetVertices(new[]
            {
                new Vector3(0, 0, 0), new Vector3(1, 0, 0), new Vector3(0, 1, 0),
                new Vector3(1, 1, 0)
            });
            src.subMeshCount = 1;
            src.SetIndexBufferParams(9, UnityEngine.Rendering.IndexFormat.UInt32);
            // two good triangles, then one referencing a vertex that does not exist
            src.SetIndexBufferData(new int[] { 0, 1, 2,  1, 3, 2,  0, 1, 99 }, 0, 0, 9,
                                   UnityEngine.Rendering.MeshUpdateFlags.DontValidateIndices);
            src.SetSubMesh(0, new UnityEngine.Rendering.SubMeshDescriptor(0, 9),
                           UnityEngine.Rendering.MeshUpdateFlags.DontValidateIndices | UnityEngine.Rendering.MeshUpdateFlags.DontRecalculateBounds);

            var dst = new Mesh();
            var lookup = LookupFor(3);

            Assert.DoesNotThrow(() => lookup.GenerateSubMesh(new AllVisible(), src, dst));
            Assert.That(dst.subMeshCount, Is.EqualTo(1));
            Assert.That(dst.GetIndexCount(0), Is.EqualTo(6),
                        "the two valid triangles must survive; only the out-of-range one is dropped");

            Object.DestroyImmediate(src);
            Object.DestroyImmediate(dst);
        }
    }
}
