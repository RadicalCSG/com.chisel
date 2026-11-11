using NUnit.Framework;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace Chisel.Core.Tests
{
    // A brush whose edges and faces fit together still isn't a solid when it is flat, collapsed to a line or a point, or
    // when its faces cross or fold over each other; a box of any size, also inside out, is
    [TestFixture]
    public class BrushShapeChecksTests
    {
        static BrushMesh Box(float3 min, float3 max)
        {
            BrushMeshFactory.CreateBox(min, max, out var box);
            return box;
        }

        static BrushMesh UnitBox() => Box(new float3(-1, -1, -1), new float3(1, 1, 1));

        static void AssertNotASolid(BrushMesh brush, string what)
        {
            Assert.That(brush.ValidateData(out var dataProblem), Is.True, what + ": its edges and faces should still fit together: " + dataProblem);
            Assert.That(brush.ValidateShape(out var problem), Is.False, what + " passed as a solid");
            Assert.That(problem, Is.Not.Null.And.Not.Empty, what + ": no reason given");
            Assert.That(brush.Validate(), Is.False, what);
        }

        [Test]
        public void AFlatBrush_HasNoVolume()
        {
            var box = UnitBox();
            for (int v = 0; v < box.vertices.Length; v++)
                box.vertices[v].y = 0;
            Assert.That(box.HasVolume(), Is.False);
            AssertNotASolid(box, "a flat brush");
        }

        [Test]
        public void ABrushCollapsedToALineOrAPoint_HasNoVolume()
        {
            var line = UnitBox();
            for (int v = 0; v < line.vertices.Length; v++)
                line.vertices[v] = new float3(line.vertices[v].x, 0, 0);
            Assert.That(line.HasVolume(), Is.False, "a line");
            AssertNotASolid(line, "a line");

            var point = UnitBox();
            for (int v = 0; v < point.vertices.Length; v++)
                point.vertices[v] = new float3(3, 4, 5);
            Assert.That(point.HasVolume(), Is.False, "a point");
            AssertNotASolid(point, "a point");
        }

        // Two corners of its top face swap places: the face crosses itself
        static void SwapTopCorners(BrushMesh box)
        {
            int a = -1, b = -1;
            for (int v = 0; v < box.vertices.Length; v++)
            {
                var vertex = box.vertices[v];
                if (vertex.y < 0) continue;
                if (vertex.x < 0 && vertex.z < 0) a = v;
                if (vertex.x > 0 && vertex.z > 0) b = v;
            }
            (box.vertices[a], box.vertices[b]) = (box.vertices[b], box.vertices[a]);
        }

        [Test]
        public void ABrushWithAFaceThatCrossesItself_IsSelfIntersecting()
        {
            var box = UnitBox();
            SwapTopCorners(box);
            Assert.That(box.HasVolume(), Is.True);
            Assert.That(box.IsSelfIntersecting(), Is.True);
            AssertNotASolid(box, "a face crossing itself");
        }

        // One corner pushed through the opposite face: the faces fold over each other
        [Test]
        public void ABrushFoldedThroughItself_IsSelfIntersecting()
        {
            var box = UnitBox();
            for (int v = 0; v < box.vertices.Length; v++)
            {
                if (box.vertices[v].y > 0 && box.vertices[v].x > 0 && box.vertices[v].z > 0)
                {
                    box.vertices[v].y = -3;
                    break;
                }
            }
            Assert.That(box.IsSelfIntersecting(), Is.True);
            AssertNotASolid(box, "a folded brush");
        }

        [Test]
        public void Boxes_AreSolids_AtAnySizeAndInsideOut()
        {
            Assert.That(UnitBox().Validate(logErrors: true), Is.True, "a unit box");
            Assert.That(Box(new float3(-1000, -1000, -1000), new float3(1000, 1000, 1000)).Validate(logErrors: true), Is.True, "a box 2 km wide");
            Assert.That(Box(new float3(-5, 0, -5), new float3(5, 0.0001f, 5)).Validate(logErrors: true), Is.True, "a box 0.1 mm thick");
            Assert.That(Box(new float3(-0.001f, -0.001f, -0.001f), new float3(0.001f, 0.001f, 0.001f)).Validate(logErrors: true), Is.True, "a box 2 mm wide");

            var insideOut = UnitBox();
            insideOut.Invert();
            Assert.That(insideOut.HasVolume(), Is.True, "inside out");
            Assert.That(insideOut.IsSelfIntersecting(), Is.False, "inside out");
        }

        sealed class Messages : IChiselMessageHandler
        {
            public readonly System.Collections.Generic.List<string> warnings = new();
            public MessageDestination Destination => MessageDestination.Inspector;
            public void SetTitle(string name, Object reference) { }
            public void Warning(string message, System.Action buttonAction, string buttonText) { warnings.Add(message); }
            public void Warning(string message) { warnings.Add(message); }
        }

        // A Brush component with such a shape is invalid, and its inspector says why (its editor draws it in the Invalid
        // color and keeps its handles, so it can be fixed)
        [TestCase("flat"), TestCase("crossing")]
        public void ABrushComponentWithSuchAShape_IsInvalid_AndSaysWhy(string shape)
        {
            var outline = UnitBox();
            if (shape == "flat")
            {
                for (int v = 0; v < outline.vertices.Length; v++)
                    outline.vertices[v].y = 0;
            } else
                SwapTopCorners(outline);
            outline.planes = null;  // fitted again, as the brush editor does after moving corners
            var definition = new ChiselBrushDefinition { BrushOutline = outline };

            Assert.That(definition.Validate(), Is.False, shape);
            Assert.That(definition.ValidState, Is.False, shape);
            var messages = new Messages();
            definition.GetMessages(messages);
            Assert.That(messages.warnings, Has.Count.EqualTo(1), shape);
            Assert.That(messages.warnings[0], shape == "flat" ? Does.Contain("no volume") : Does.Contain("concave").Or.Contain("cross"), shape);
        }

        // The checks a brush being built by a generator gets agree with the ones a BrushMesh gets
        [Test]
        public void ABrushBeingBuilt_IsCheckedTheSameWay()
        {
            Assert.That(ValidateAsBuilt(UnitBox()), Is.True, "a box");
            // its faces' planes come from its corners, which far from the origin mustn't cancel each other out
            Assert.That(ValidateAsBuilt(Box(new float3(4999, 4999, 4999), new float3(5001, 5001, 5001))), Is.True, "a box far from the origin");

            var flat = UnitBox();
            for (int v = 0; v < flat.vertices.Length; v++)
                flat.vertices[v].y = 0;
            Assert.That(ValidateAsBuilt(flat), Is.False, "a flat brush");

            var folded = UnitBox();
            for (int v = 0; v < folded.vertices.Length; v++)
            {
                if (folded.vertices[v].y > 0 && folded.vertices[v].x > 0 && folded.vertices[v].z > 0)
                {
                    folded.vertices[v].y = -3;
                    break;
                }
            }
            Assert.That(ValidateAsBuilt(folded), Is.False, "a folded brush");
        }

        static bool ValidateAsBuilt(BrushMesh brush)
        {
            using var builder = new BlobBuilder(Allocator.Temp);
            ref var root = ref builder.ConstructRoot<BrushMeshBlob>();
            var vertices  = builder.Allocate(ref root.localVertices, brush.vertices.Length);
            var halfEdges = builder.Allocate(ref root.halfEdges, brush.halfEdges.Length);
            var polygons  = builder.Allocate(ref root.polygons, brush.polygons.Length);
            for (int v = 0; v < brush.vertices.Length; v++)
                vertices[v] = brush.vertices[v];
            for (int h = 0; h < brush.halfEdges.Length; h++)
                halfEdges[h] = new BrushMeshBlob.HalfEdge { vertexIndex = brush.halfEdges[h].vertexIndex, twinIndex = brush.halfEdges[h].twinIndex };
            for (int p = 0; p < brush.polygons.Length; p++)
                polygons[p] = new BrushMeshBlob.Polygon { firstEdge = brush.polygons[p].firstEdge, edgeCount = brush.polygons[p].edgeCount };
            return BrushMeshFactory.Validate(in vertices, in halfEdges, in polygons);
        }
    }
}
