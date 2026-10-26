using Chisel.Components;
using Chisel.Core;
using NUnit.Framework;
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
