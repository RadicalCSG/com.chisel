using Chisel.Core;
using NUnit.Framework;
using UnityEngine;

namespace Chisel.Editors.Tests
{
    // While a brush is edited, clicking a square of the move handle picks the plane its vertices, edges and faces are
    // dragged on; changing the selection clears it.
    public sealed class ChiselWorkingPlaneTests
    {
        Axes workingPlane;

        [SetUp]
        public void SetUp()
        {
            workingPlane = ChiselEditableOutline.WorkingPlane;
            ChiselEditableOutline.WorkingPlane = Axes.None;
        }

        [TearDown]
        public void TearDown()
        {
            ChiselEditableOutline.WorkingPlane = workingPlane;
            GUIUtility.hotControl = 0;
        }

        static SceneHandles.PositionHandleIDs HandleIDs() => new()
        {
            xAxisId = 101, yAxisId = 102, zAxisId = 103,
            xzPlaneId = 104, xyPlaneId = 105, yzPlaneId = 106,
            centerId = 107
        };

        static Event Mouse(EventType type) => new() { type = type, button = 0 };

        // the square is pressed (its slider takes the mouse), maybe dragged, then released
        static Axes PressAndRelease(ref SceneHandles.PositionHandleIDs ids, int pressedId, bool drag)
        {
            GUIUtility.hotControl = 0;
            SceneHandles.TrackPlaneClick(ref ids, Mouse(EventType.MouseDown));
            GUIUtility.hotControl = pressedId;
            if (drag)
                SceneHandles.TrackPlaneClick(ref ids, Mouse(EventType.MouseDrag));
            SceneHandles.TrackPlaneClick(ref ids, Mouse(EventType.MouseUp));
            var clicked = ids.clickedPlane;
            GUIUtility.hotControl = 0;
            return clicked;
        }

        [TestCase(104, Axes.XZ)]
        [TestCase(105, Axes.XY)]
        [TestCase(106, Axes.YZ)]
        public void ClickingASquare_IsAClickOnItsPlane(int squareId, Axes plane)
        {
            var ids = HandleIDs();
            Assert.That(PressAndRelease(ref ids, squareId, drag: false), Is.EqualTo(plane));
        }

        [Test]
        public void DraggingASquare_IsNoClick()
        {
            var ids = HandleIDs();
            Assert.That(PressAndRelease(ref ids, 104, drag: true), Is.EqualTo(Axes.None));
        }

        [TestCase(101)]
        [TestCase(107)]
        public void ClickingAnArrowOrTheCenter_IsNoClickOnAPlane(int controlId)
        {
            var ids = HandleIDs();
            Assert.That(PressAndRelease(ref ids, controlId, drag: false), Is.EqualTo(Axes.None));
        }

        // the click is only seen in the event of the release
        [Test]
        public void TheClick_IsForgottenAfterTheRelease()
        {
            var ids = HandleIDs();
            PressAndRelease(ref ids, 104, drag: false);
            SceneHandles.TrackPlaneClick(ref ids, Mouse(EventType.MouseMove));
            Assert.That(ids.clickedPlane, Is.EqualTo(Axes.None));
        }

        [Test]
        public void TheWorkingPlane_IsShownOnItsSquare()
        {
            var ids = HandleIDs();
            ids.SetActivePlane(Axes.XY);
            Assert.That(ids.xyPlaneState & ControlState.Active, Is.EqualTo(ControlState.Active));
            Assert.That(ids.xzPlaneState & ControlState.Active, Is.EqualTo(ControlState.None));
            Assert.That(ids.yzPlaneState & ControlState.Active, Is.EqualTo(ControlState.None));
        }

        static Grid TurnedGrid() => new(Matrix4x4.Rotate(Quaternion.Euler(0, 30, 0)));

        // the plane in the move handle's axes, which are the grid's
        [TestCase(Axes.XZ)]
        [TestCase(Axes.XY)]
        [TestCase(Axes.YZ)]
        public void TheWorkingPlane_SlidesAlongItsTwoAxes(Axes plane)
        {
            var grid = TurnedGrid();
            ChiselEditableOutline.WorkingPlane = plane;
            Assert.That(ChiselEditableOutline.TryGetWorkingPlane(grid, out var normal, out var slide1, out var slide2), Is.True);

            var expected = plane == Axes.XZ ? grid.Up : plane == Axes.XY ? grid.Forward : grid.Right;
            Assert.That(Vector3.Distance(normal, expected), Is.LessThan(1e-5f), $"normal {normal}");
            Assert.That(Mathf.Abs(Vector3.Dot(slide1, normal)), Is.LessThan(1e-5f), $"slide {slide1}");
            Assert.That(Mathf.Abs(Vector3.Dot(slide2, normal)), Is.LessThan(1e-5f), $"slide {slide2}");
            Assert.That(Mathf.Abs(Vector3.Dot(slide1, slide2)), Is.LessThan(1e-5f));
        }

        [Test]
        public void WithoutAWorkingPlane_DraggingIsAsBefore()
        {
            Assert.That(ChiselEditableOutline.TryGetWorkingPlane(TurnedGrid(), out _, out _, out _), Is.False);
        }

        [Test]
        public void ChangingTheSelection_ClearsTheWorkingPlane()
        {
            ChiselEditableOutline.WorkingPlane = Axes.XZ;
            ChiselEditableOutline.OnSelectionChanged();
            Assert.That(ChiselEditableOutline.WorkingPlane, Is.EqualTo(Axes.None));
        }
    }
}
