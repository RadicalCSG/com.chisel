using Chisel.Components;
using Chisel.Core;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;

namespace Chisel.Editors.Tests
{
    // A brush being edited keeps its handles when its shape is invalid (concave, flat), so it can be dragged back
    public sealed class ChiselEditableOutlineTests
    {
        GameObject root;

        [TearDown]
        public void TearDown()
        {
            if (root)
                Object.DestroyImmediate(root);
        }

        // A box with its top corner pushed in past its middle: concave
        static BrushMesh ConcaveBox()
        {
            BrushMeshFactory.CreateBox(new Vector3(2, 2, 2), 0, out var box);
            var corner = 0;
            for (int v = 1; v < box.vertices.Length; v++)
            {
                if (box.vertices[v].x + box.vertices[v].y + box.vertices[v].z > box.vertices[corner].x + box.vertices[corner].y + box.vertices[corner].z)
                    corner = v;
            }
            box.vertices[corner] = new Unity.Mathematics.float3(-0.25f, -0.25f, -0.25f);
            box.CalculatePlanes();
            return box;
        }

        // Delete removes the selected vertices: selecting an edge selects its vertices, so it goes with them
        [Test]
        public void DeletingASelectedEdge_RemovesItsVertices()
        {
            BrushMeshFactory.CreateBox(-Vector3.one, Vector3.one, out var box);
            root = new GameObject("Brush");
            var brush = root.AddComponent<ChiselBrushComponent>();
            brush.definition.BrushOutline = box;
            brush.OnValidate();

            var editable = new ChiselEditableOutline(brush);
            var mesh     = editable.brushMesh;
            var edge     = -1;
            for (int e = 0; e < mesh.halfEdges.Length && edge < 0; e++)
            {
                var a = mesh.vertices[mesh.halfEdges[e].vertexIndex];
                var b = mesh.vertices[mesh.halfEdges[mesh.halfEdges[e].twinIndex].vertexIndex];
                if (a.x > 0 && a.y > 0 && b.x > 0 && b.y > 0)
                    edge = e;
            }
            editable.SelectEdge(edge, SelectionType.Replace);
            Assert.That(editable.selection.selectedVertices.Count, Is.EqualTo(2), "the edge's vertices");

            Assert.That(editable.DeleteSelectedVertices(), Is.True);

            Assert.That(editable.brushMesh.vertices.Length, Is.EqualTo(6), "a wedge");
            Assert.That(editable.brushMesh.polygons.Length, Is.EqualTo(5));
            Assert.That(editable.selection.selectedVertices, Is.Empty, "nothing selected after");
        }

        ChiselEditableOutline EditableBox(out BrushMesh before)
        {
            BrushMeshFactory.CreateBox(-Vector3.one, Vector3.one, out var box);
            root = new GameObject("Brush");
            var brush = root.AddComponent<ChiselBrushComponent>();
            brush.definition.BrushOutline = box;
            brush.OnValidate();
            var editable = new ChiselEditableOutline(brush);
            before = new BrushMesh(editable.brushMesh);
            return editable;
        }

        // the top face, its four vertices with it
        static void SelectTop(ChiselEditableOutline editable)
        {
            var mesh = editable.brushMesh;
            for (int p = 0; p < mesh.polygons.Length; p++)
            {
                if (mesh.planes[p].y > 0.99f)
                    editable.SelectPolygon(p, SelectionType.Replace);
            }
            Assert.That(editable.selection.selectedVertices.Count, Is.EqualTo(4), "the top's vertices");
        }

        static void AssertMoved(ChiselEditableOutline editable, BrushMesh before, System.Func<float3, float3> moved)
        {
            var vertices = editable.brushMesh.vertices;
            for (int v = 0; v < vertices.Length; v++)
            {
                var expected = editable.selection.selectedVertices.Contains(v) ? moved(before.vertices[v]) : before.vertices[v];
                Assert.That(math.distance(vertices[v], expected), Is.LessThan(1e-5f), $"vertex {v}: {vertices[v]}, expected {expected}");
            }
        }

        // A rotation turns the selected vertices about their center, from where they were when it started, so each step
        // of a drag isn't added to the last
        [Test]
        public void RotatingTheSelectedVertices_TurnsThemAboutTheirCenter()
        {
            var editable = EditableBox(out var before);
            SelectTop(editable);
            var center = new float3(0, 1, 0);

            editable.BeginVertexTransform();
            editable.RotateSelectedVertices(Quaternion.AngleAxis(30, Vector3.up));
            editable.RotateSelectedVertices(Quaternion.AngleAxis(45, Vector3.up));

            var rotation = (quaternion)Quaternion.AngleAxis(45, Vector3.up);
            AssertMoved(editable, before, position => center + math.mul(rotation, position - center));
        }

        // A scale stretches the selected vertices from their center along the handle's axes
        [TestCase(0,  TestName = "ScalingTheSelectedVertices_StretchesThemFromTheirCenter")]
        [TestCase(90, TestName = "ScalingTheSelectedVertices_AlongTurnedAxes_StretchesThemAlongThose")]
        public void ScalingTheSelectedVertices(float axesAngle)
        {
            var editable = EditableBox(out var before);
            SelectTop(editable);
            var center = new float3(0, 1, 0);
            var axes   = Quaternion.AngleAxis(axesAngle, Vector3.up);

            editable.BeginVertexTransform();
            editable.ScaleSelectedVertices(new Vector3(3, 1, 1), axes);
            editable.ScaleSelectedVertices(new Vector3(2, 1, 1), axes);

            // the handle's x axis is the brush's x, or turned a quarter its z
            var stretch = axesAngle == 0 ? new float3(2, 1, 1) : new float3(1, 1, 2);
            AssertMoved(editable, before, position => center + (position - center) * stretch);
        }

        // Unity's Global/Local toggle: Local is the brush's own axes, Global the world's, in the brush's space
        [Test]
        public void TheHandleAxes_FollowGlobalOrLocal()
        {
            var brushRotation = Quaternion.AngleAxis(90, Vector3.up);
            var brushToWorld  = Matrix4x4.TRS(new Vector3(5, 0, 0), brushRotation, Vector3.one);

            var local  = ChiselEditableOutline.VertexHandleAxes(PivotRotation.Local,  brushToWorld);
            var global = ChiselEditableOutline.VertexHandleAxes(PivotRotation.Global, brushToWorld);

            Assert.That(Quaternion.Angle(local, Quaternion.identity), Is.LessThan(1e-3f), "Local");
            Assert.That(Quaternion.Angle(brushRotation * global, Quaternion.identity), Is.LessThan(1e-3f), "Global, in the world");
        }

        [Test]
        public void AConcaveBrush_KeepsItsHandles()
        {
            var outline = ConcaveBox();
            Assert.That(outline.ValidateShape(out _), Is.False, "the brush isn't concave");

            root = new GameObject("Brush");
            var brush = root.AddComponent<ChiselBrushComponent>();
            brush.definition.BrushOutline = outline;
            brush.OnValidate();
            Assert.That(brush.definition.IsValid, Is.False, "an invalid brush is drawn as it is");

            var editable = new ChiselEditableOutline(brush);
            Assert.That(editable.polygonCenters, Is.Not.Null, "no handles");
            Assert.That(editable.polygonCenters.Length, Is.EqualTo(outline.polygons.Length));
        }
    }
}
