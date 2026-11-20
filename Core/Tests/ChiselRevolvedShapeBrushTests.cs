using System.Collections.Generic;
using NUnit.Framework;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Chisel.Core.Tests
{
    // A revolved shape's brushes are right side out wherever its shape is: away from the axis it turns round (a ring), on
    // either side of it, or across it
    [TestFixture]
    public class ChiselRevolvedShapeBrushTests
    {
        internal static ChiselRevolvedShapeDefinition Revolved(Curve2D shape, float startAngle = 0, float totalAngle = 360)
        {
            var definition = new ChiselRevolvedShapeDefinition();
            definition.Reset();
            if (shape != null)
                definition.shape = shape;
            definition.settings.revolveSegments = 6;
            definition.settings.startAngle      = startAngle;
            definition.settings.totalAngle      = totalAngle;
            return definition;
        }

        internal static Curve2D Square(float fromX, float toX)
        {
            return new Curve2D(new[] { new CurveControlPoint2D(fromX, 0), new CurveControlPoint2D(toX, 0), new CurveControlPoint2D(toX, 1), new CurveControlPoint2D(fromX, 1) });
        }

        static BlobAssetReference<InternalChiselSurfaceArray> Surfaces(int count)
        {
            using var builder = new BlobBuilder(Allocator.Temp);
            ref var root = ref builder.ConstructRoot<InternalChiselSurfaceArray>();
            var surfaces = builder.Allocate(ref root.surfaces, count);
            for (int i = 0; i < count; i++)
                surfaces[i] = InternalChiselSurface.Default;
            return builder.CreateBlobAssetReference<InternalChiselSurfaceArray>(Allocator.Temp);
        }

        // each brush's vertices and planes
        internal static List<(float3[] vertices, float4[] planes)> Brushes(ChiselRevolvedShapeDefinition definition)
        {
            Assert.That(definition.Validate(), Is.True);
            var settings = definition.GetBranchGenerator();
            var surfaces = Surfaces(definition.RequiredSurfaceCount);
            try
            {
                var count = settings.PrepareAndCountRequiredBrushMeshes();
                var nodes = new NativeList<GeneratedNode>(count, Allocator.Temp);
                try
                {
                    nodes.Resize(count, NativeArrayOptions.ClearMemory);
                    Assert.That(settings.GenerateNodes(surfaces, nodes, Allocator.Temp), Is.True, "generated");
                    var brushes = new List<(float3[], float4[])>();
                    foreach (var node in nodes)
                    {
                        ref var mesh = ref node.brushMesh.Value;
                        var vertices = new float3[mesh.localVertices.Length];
                        for (int v = 0; v < vertices.Length; v++)
                            vertices[v] = mesh.localVertices[v];
                        var planes = new float4[mesh.localPlanes.Length];
                        for (int p = 0; p < planes.Length; p++)
                            planes[p] = mesh.localPlanes[p];
                        brushes.Add((vertices, planes));
                        node.brushMesh.Dispose();
                    }
                    return brushes;
                }
                finally
                {
                    nodes.Dispose();
                }
            }
            finally
            {
                surfaces.Dispose();
                settings.Dispose();
            }
        }

        // a ring's brushes were inside out: the middle of each was outside all its planes, so nothing was made
        [Test]
        public void ItsBrushes_AreRightSideOut([Values(1f, -2f, -1f)] float fromX, [Values(360f, 90f)] float totalAngle)
        {
            var brushes = Brushes(Revolved(Square(fromX, fromX + 1), 30, totalAngle));
            Assert.That(brushes, Is.Not.Empty);
            foreach (var (vertices, planes) in brushes)
            {
                var middle = float3.zero;
                foreach (var vertex in vertices)
                    middle += vertex;
                middle /= vertices.Length;
                foreach (var plane in planes)
                    Assert.That(math.dot(plane.xyz, middle) + plane.w, Is.LessThan(0), $"the middle {middle} is inside {plane}");
            }
        }
    }
}
