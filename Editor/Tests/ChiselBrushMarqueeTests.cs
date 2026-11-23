using System.Collections.Generic;
using Chisel.Components;
using Chisel.Core;
using NUnit.Framework;
using UnityEngine;

namespace Chisel.Editors.Tests
{
    // While a brush is edited, a drag on empty space selects the vertices inside the rectangle: only those on its faces
    // turned towards the camera, unless back faces are selectable too. A click is left to Unity's picking.
    public sealed class ChiselBrushMarqueeTests
    {
        GameObject root;
        bool       selectedBackFaces;

        // a camera in front of the box, on its +z side, looking at it
        static readonly Vector3 kCameraPosition = new(0, 0, 10);
        static readonly Vector3 kCameraForward  = new(0, 0, -1);

        [SetUp]
        public void SetUp()
        {
            selectedBackFaces = ChiselEditorSettings.SelectBackFaces;
            ChiselEditorSettings.SelectBackFaces = false;
            forgotten = 0;
        }

        [TearDown]
        public void TearDown()
        {
            ChiselEditorSettings.SelectBackFaces = selectedBackFaces;
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
            return new ChiselEditableOutline(brush);
        }

        // looking down -z: x to the right, y up, 100 points a unit
        static Vector2 ToGUI(Vector3 position) => new(position.x * 100, -position.y * 100);

        static HashSet<Vector3> SelectedPositions(ChiselEditableOutline editable)
        {
            var positions = new HashSet<Vector3>();
            foreach (var v in editable.selection.selectedVertices)
                positions.Add(editable.brushMesh.vertices[v]);
            return positions;
        }

        [Test]
        public void TheVerticesShown_AreThoseOnFacesTurnedTowardsTheCamera()
        {
            var editable = EditableBox();
            editable.UpdateVisibility(kCameraPosition, kCameraForward, orthographic: false);

            var mesh = editable.brushMesh;
            for (int v = 0; v < mesh.vertices.Length; v++)
                Assert.That(editable.IsVertexShown(v), Is.EqualTo(mesh.vertices[v].z > 0), $"vertex at {mesh.vertices[v]}");
            for (int p = 0; p < mesh.polygons.Length; p++)
                Assert.That(editable.IsPolygonShown(p), Is.EqualTo(mesh.planes[p].z > 0.99f), $"face facing {mesh.planes[p].xyz}");
        }

        [Test]
        public void WithBackFacesSelectable_EveryVertexIsShown()
        {
            var editable = EditableBox();
            ChiselEditorSettings.SelectBackFaces = true;
            editable.UpdateVisibility(kCameraPosition, kCameraForward, orthographic: false);

            for (int v = 0; v < editable.brushMesh.vertices.Length; v++)
                Assert.That(editable.IsVertexShown(v), Is.True);
        }

        // from inside the brush every face is seen from behind: all of them are shown, or nothing could be selected
        [Test]
        public void FromInsideTheBrush_EveryVertexIsShown()
        {
            var editable = EditableBox();
            editable.UpdateVisibility(Vector3.zero, kCameraForward, orthographic: false);

            for (int v = 0; v < editable.brushMesh.vertices.Length; v++)
                Assert.That(editable.IsVertexShown(v), Is.True);
        }

        // the right half of the box as seen: its front two corners, not the two behind them
        static readonly Rect kRightHalf = new(50, -150, 100, 300);

        [TestCase(false, 2)]
        [TestCase(true,  4)]
        public void TheMarquee_SelectsTheVerticesShownInsideIt(bool backFaces, int expected)
        {
            var editable = EditableBox();
            ChiselEditorSettings.SelectBackFaces = backFaces;
            editable.UpdateVisibility(kCameraPosition, kCameraForward, orthographic: false);

            editable.SelectVerticesInRect(kRightHalf, SelectionType.Replace, ToGUI);

            var selected = SelectedPositions(editable);
            Assert.That(selected.Count, Is.EqualTo(expected));
            foreach (var position in selected)
            {
                Assert.That(position.x, Is.GreaterThan(0), $"{position}");
                if (!backFaces)
                    Assert.That(position.z, Is.GreaterThan(0), $"{position} is behind");
            }
        }

        [Test]
        public void TheMarquee_AddsWithShift_AndTakesAwayWithControl()
        {
            var editable = EditableBox();
            editable.UpdateVisibility(kCameraPosition, kCameraForward, orthographic: false);
            var leftHalf = new Rect(-150, -150, 100, 300);

            editable.SelectVerticesInRect(kRightHalf, SelectionType.Replace, ToGUI);
            editable.SelectVerticesInRect(leftHalf, SelectionType.Additive, ToGUI);
            Assert.That(SelectedPositions(editable).Count, Is.EqualTo(4), "added");

            editable.SelectVerticesInRect(kRightHalf, SelectionType.Subtractive, ToGUI);
            var selected = SelectedPositions(editable);
            Assert.That(selected.Count, Is.EqualTo(2), "taken away");
            foreach (var position in selected)
                Assert.That(position.x, Is.LessThan(0), $"{position}");

            editable.SelectVerticesInRect(new Rect(1000, 1000, 10, 10), SelectionType.Replace, ToGUI);
            Assert.That(editable.selection.selectedVertices, Is.Empty, "an empty rectangle selects nothing");
        }

        // selecting both vertices of an edge selects the edge, all of a face's the face
        [Test]
        public void TheMarquee_SelectsTheEdgesAndFacesOfTheVerticesItSelects()
        {
            var editable = EditableBox();
            editable.UpdateVisibility(kCameraPosition, kCameraForward, orthographic: false);

            editable.SelectVerticesInRect(new Rect(-150, -150, 300, 300), SelectionType.Replace, ToGUI);

            Assert.That(editable.selection.selectedVertices.Count, Is.EqualTo(4));
            Assert.That(editable.selection.selectedPolygons.Count, Is.EqualTo(1), "the front face");
        }

        const int kMarqueeID = 5678;

        static Event Mouse(EventType type, Vector2 position) => new() { type = type, button = 0, mousePosition = position };

        // how often the marquee told Unity's rect selection to forget the press it noted
        int forgotten;

        // a press on empty space that Unity's rect selection noted, held by the marquee
        void Press(Vector2 position, bool unityNotedIt = true, bool editing = true)
        {
            var evt = Mouse(EventType.MouseDown, position);
            if (ChiselBrushMarquee.HandleEvent(evt, kMarqueeID, editing))
                forgotten++;
            ChiselBrushMarquee.HandlePress(evt, kMarqueeID, unityNotedIt, editing);
        }

        void Send(Event evt, bool editing = true)
        {
            if (ChiselBrushMarquee.HandleEvent(evt, kMarqueeID, editing))
                forgotten++;
        }

        // the marquee holds the press, so Unity doesn't start a box selection: Unity only does while nothing holds the
        // mouse; dragged past the threshold, it selects on release
        [Test]
        public void ADrag_IsTheMarquees_AndSelectsOnRelease()
        {
            GUIUtility.hotControl = 0;
            Press(new Vector2(10, 10));
            Assert.That(GUIUtility.hotControl, Is.EqualTo(kMarqueeID), "held");

            Send(Mouse(EventType.MouseDrag, new Vector2(13, 10)));
            Assert.That(ChiselBrushMarquee.Dragging, Is.False, "within the threshold");

            Send(Mouse(EventType.MouseDrag, new Vector2(20, 10)));
            Assert.That(ChiselBrushMarquee.Dragging, Is.True, "past the threshold");

            Send(Mouse(EventType.MouseUp, new Vector2(30, 40)));
            Assert.That(GUIUtility.hotControl, Is.EqualTo(0));
            Assert.That(ChiselBrushMarquee.TryGetReleased(out var rect, out var selectionType), Is.True);
            Assert.That(rect, Is.EqualTo(new Rect(10, 10, 20, 30)));
            Assert.That(selectionType, Is.EqualTo(SelectionType.Replace));
        }

        // released without a drag it lets go and selects nothing: Unity picks, as it noted the press
        [Test]
        public void AClick_IsLeftToUnity()
        {
            GUIUtility.hotControl = 0;
            Press(new Vector2(10, 10));
            Send(Mouse(EventType.MouseUp, new Vector2(11, 10)));

            Assert.That(GUIUtility.hotControl, Is.EqualTo(0));
            Assert.That(ChiselBrushMarquee.TryGetReleased(out _, out _), Is.False);
            Assert.That(forgotten, Is.EqualTo(0), "Unity still picks");
        }

        // Unity picks on the release of a press it noted, even after a drag: under where the drag started, empty space,
        // which deselected the brush. A drag tells it to forget the press, once.
        [Test]
        public void ADrag_MakesUnityForgetThePress_SoItDoesntPickOnRelease()
        {
            GUIUtility.hotControl = 0;
            Press(new Vector2(10, 10));
            Send(Mouse(EventType.MouseDrag, new Vector2(13, 10)));
            Assert.That(forgotten, Is.EqualTo(0), "within the threshold");

            Send(Mouse(EventType.MouseDrag, new Vector2(20, 10)));
            Send(Mouse(EventType.MouseDrag, new Vector2(40, 30)));
            Send(Mouse(EventType.MouseUp, new Vector2(40, 30)));
            Assert.That(forgotten, Is.EqualTo(1));
        }

        [TestCase(false, true,  TestName = "APressUnityDidntNote_IsntHeld")]
        [TestCase(true,  false, TestName = "WithoutABrushEdited_APressIsntHeld")]
        public void APressNotOnEmptySpaceOfAnEditedBrush_IsntHeld(bool unityNotedIt, bool editing)
        {
            GUIUtility.hotControl = 0;
            Press(new Vector2(10, 10), unityNotedIt, editing);
            Assert.That(GUIUtility.hotControl, Is.EqualTo(0));
        }

        [Test]
        public void Escape_CancelsTheMarquee()
        {
            GUIUtility.hotControl = 0;
            Press(new Vector2(10, 10));
            Send(Mouse(EventType.MouseDrag, new Vector2(50, 50)));

            Send(new Event { type = EventType.KeyDown, keyCode = KeyCode.Escape });
            Send(Mouse(EventType.MouseUp, new Vector2(50, 50)));

            Assert.That(GUIUtility.hotControl, Is.EqualTo(0));
            Assert.That(ChiselBrushMarquee.TryGetReleased(out _, out _), Is.False);
        }
    }
}
