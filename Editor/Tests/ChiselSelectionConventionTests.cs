using Chisel.Components;
using Chisel.Core;
using NUnit.Framework;
using UnityEngine;

namespace Chisel.Editors.Tests
{
    // Selecting a brush's vertices, edges and faces follows Unity's scene view selection: a click on its own replaces the
    // selection; Shift adds, or takes away the active item (the one clicked last); Control (Command on macOS) adds, or
    // takes away any selected item, and wins from Shift. A marquee adds with Shift and takes away with Control, as held
    // when it is started.
    public sealed class ChiselSelectionConventionTests
    {
        GameObject root;

        [TearDown]
        public void TearDown()
        {
            GUIUtility.hotControl = 0;
            ChiselBrushMarquee.Cancel();
            if (root)
                Object.DestroyImmediate(root);
        }

        ChiselEditableOutline EditableBox()
        {
            BrushMeshFactory.CreateBox(-Vector3.one, Vector3.one, out var box);
            root = new GameObject("Brush");
            var brush = root.AddComponent<ChiselBrushComponent>();
            brush.definition.BrushOutline = box;
            brush.OnValidate();
            var editable = new ChiselEditableOutline(brush);
            editable.selection.Clear();
            return editable;
        }

        //        shift  action selected active  -> expected
        [TestCase(false, false, false,   false,     SelectionType.Replace)]
        [TestCase(false, false, true,    true,      SelectionType.Replace)]
        [TestCase(true,  false, false,   false,     SelectionType.Additive)]
        [TestCase(true,  false, true,    false,     SelectionType.Additive)]
        [TestCase(true,  false, true,    true,      SelectionType.Subtractive)]
        [TestCase(false, true,  false,   false,     SelectionType.Additive)]
        [TestCase(false, true,  true,    false,     SelectionType.Subtractive)]
        [TestCase(true,  true,  true,    false,     SelectionType.Subtractive)]
        [TestCase(true,  true,  false,   false,     SelectionType.Additive)]
        public void AClick_SelectsAsUnityDoes(bool shift, bool actionKey, bool isSelected, bool isActive, SelectionType expected)
        {
            Assert.That(ChiselEditableOutline.ClickSelectionType(shift, actionKey, isSelected, isActive), Is.EqualTo(expected));
        }

        [Test]
        public void ShiftClicks_AddAndTakeAwayTheActiveVertex()
        {
            var editable = EditableBox();
            var selected = editable.selection.selectedVertices;

            editable.ClickVertex(0, shift: false, actionKey: false);
            editable.ClickVertex(1, shift: true,  actionKey: false);
            Assert.That(selected, Is.EquivalentTo(new[] { 0, 1 }), "added");

            editable.ClickVertex(0, shift: true, actionKey: false);
            Assert.That(selected, Is.EquivalentTo(new[] { 0, 1 }), "a selected vertex that isn't the active one stays, and becomes active");

            editable.ClickVertex(0, shift: true, actionKey: false);
            Assert.That(selected, Is.EquivalentTo(new[] { 1 }), "the active one is taken away");
        }

        [Test]
        public void ControlClicks_AddAndTakeAwayAnySelectedVertex()
        {
            var editable = EditableBox();
            var selected = editable.selection.selectedVertices;

            editable.ClickVertex(0, shift: false, actionKey: false);
            editable.ClickVertex(1, shift: false, actionKey: true);
            editable.ClickVertex(2, shift: false, actionKey: true);
            Assert.That(selected, Is.EquivalentTo(new[] { 0, 1, 2 }), "added");

            editable.ClickVertex(0, shift: false, actionKey: true);
            Assert.That(selected, Is.EquivalentTo(new[] { 1, 2 }), "taken away, though not the active one");

            editable.ClickVertex(3, shift: false, actionKey: false);
            Assert.That(selected, Is.EquivalentTo(new[] { 3 }), "a plain click replaces");
        }

        [Test]
        public void EdgesAndFaces_FollowTheSameConvention()
        {
            var editable = EditableBox();
            var mesh     = editable.brushMesh;

            editable.ClickPolygon(0, shift: false, actionKey: false);
            editable.ClickPolygon(1, shift: true,  actionKey: false);
            Assert.That(editable.selection.selectedPolygons, Is.EquivalentTo(new[] { 0, 1 }));
            editable.ClickPolygon(1, shift: true, actionKey: false);
            Assert.That(editable.selection.selectedPolygons, Is.EquivalentTo(new[] { 0 }), "the active face is taken away");

            editable.selection.Clear();
            var edge = mesh.polygons[0].firstEdge;
            editable.ClickEdge(edge, shift: false, actionKey: true);
            Assert.That(editable.selection.selectedEdges, Has.Member(edge), "added");
            editable.ClickEdge(edge, shift: false, actionKey: true);
            Assert.That(editable.selection.selectedEdges, Has.No.Member(edge), "taken away");
        }

        // looking down -z: x to the right, y up, 100 points a unit
        static Vector2 ToGUI(Vector3 position) => new(position.x * 100, -position.y * 100);

        // the right half of the box as seen, front and back
        static readonly Rect kRightHalf = new(50, -150, 100, 300);

        // what is drawn as selected while the marquee is dragged is what it selects when released
        [TestCase(SelectionType.Replace)]
        [TestCase(SelectionType.Additive)]
        [TestCase(SelectionType.Subtractive)]
        public void WhileDragged_TheMarqueeShowsWhatItWillSelect(SelectionType selectionType)
        {
            var selectBackFaces = ChiselEditorSettings.SelectBackFaces;
            ChiselEditorSettings.SelectBackFaces = true;
            try
            {
                var editable = EditableBox();
                editable.UpdateVisibility(new Vector3(0, 0, 10), new Vector3(0, 0, -1), orthographic: false);
                var vertices = editable.brushMesh.vertices;
                var left     = -1;
                var right    = -1;
                for (int v = 0; v < vertices.Length; v++)
                {
                    if (vertices[v].x < 0) left = v; else right = v;
                }
                editable.ClickVertex(left,  shift: false, actionKey: false);
                editable.ClickVertex(right, shift: true,  actionKey: false);

                var preview = editable.MarqueePreview(kRightHalf, selectionType, ToGUI);
                var shownVertices = new System.Collections.Generic.HashSet<int>(preview.selectedVertices);
                var shownEdges    = new System.Collections.Generic.HashSet<int>(preview.selectedEdges);
                var shownPolygons = new System.Collections.Generic.HashSet<int>(preview.selectedPolygons);

                editable.SelectVerticesInRect(kRightHalf, selectionType, ToGUI);

                Assert.That(shownVertices, Is.EquivalentTo(editable.selection.selectedVertices), "vertices");
                Assert.That(shownEdges,    Is.EquivalentTo(editable.selection.selectedEdges),    "edges");
                Assert.That(shownPolygons, Is.EquivalentTo(editable.selection.selectedPolygons), "faces");
                if (selectionType == SelectionType.Subtractive)
                    Assert.That(shownVertices, Has.No.Member(right), "taking away shows it taken away");
                else
                    Assert.That(shownVertices, Has.Member(right));
            }
            finally
            {
                ChiselEditorSettings.SelectBackFaces = selectBackFaces;
            }
        }

        const int kMarqueeID = 5678;

        // Unity takes the kind of a box selection from the keys held when it starts
        [Test]
        public void AMarquee_SelectsAsTheKeysHeldWhenItStarted()
        {
            GUIUtility.hotControl = 0;
            var press = new Event { type = EventType.MouseDown, button = 0, mousePosition = new Vector2(10, 10), shift = true };
            ChiselBrushMarquee.HandleEvent(press, kMarqueeID, editing: true);
            ChiselBrushMarquee.HandlePress(press, kMarqueeID, unityNotedIt: true, editing: true);
            ChiselBrushMarquee.HandleEvent(new Event { type = EventType.MouseDrag, button = 0, mousePosition = new Vector2(40, 40) }, kMarqueeID, editing: true);
            ChiselBrushMarquee.HandleEvent(new Event { type = EventType.MouseUp,   button = 0, mousePosition = new Vector2(40, 40) }, kMarqueeID, editing: true);

            Assert.That(ChiselBrushMarquee.TryGetReleased(out _, out var selectionType), Is.True);
            Assert.That(selectionType, Is.EqualTo(SelectionType.Additive), "Shift was let go of before the release");
        }
    }
}
