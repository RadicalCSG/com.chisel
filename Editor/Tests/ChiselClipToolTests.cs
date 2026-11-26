using Chisel.Core;
using NUnit.Framework;
using UnityEngine;

namespace Chisel.Editors.Tests
{
    // The clip tool's plane goes through a line drawn on a surface, at right angles to that surface: the line and a
    // point into the surface below its start. Its points are put and dragged on the brushes it cuts, else on the surface
    // under the mouse. Tab goes through what it keeps of the brushes it cuts; it is one of Chisel's tools, and its keys
    // are only in use while it is.
    public sealed class ChiselClipToolTests
    {
        [Test]
        public void ThePlane_GoesThroughTheLine_AtRightAnglesToTheSurface()
        {
            var start   = new Vector3(0, 1, 0);
            var end     = new Vector3(2, 1, 1);
            var surface = Vector3.up;

            Assert.That(ChiselClipTool.TryGetPlane(start, end, surface, 1, out var plane), Is.True);

            Assert.That(Mathf.Abs(plane.GetDistanceToPoint(start)), Is.LessThan(1e-5f), "through the start");
            Assert.That(Mathf.Abs(plane.GetDistanceToPoint(end)),   Is.LessThan(1e-5f), "through the end");
            Assert.That(Mathf.Abs(Vector3.Dot(plane.normal, surface)), Is.LessThan(1e-5f), "at right angles to the surface");
        }

        [Test]
        public void ALineOfNoLength_GivesNoPlane()
        {
            var point = new Vector3(1, 2, 3);
            Assert.That(ChiselClipTool.TryGetPlane(point, point, Vector3.up, 1, out _), Is.False);
        }

        // a line along the surface's normal, as on a face seen edge-on, doesn't say which way the plane goes
        [Test]
        public void ALineAlongTheSurfacesNormal_GivesNoPlane()
        {
            Assert.That(ChiselClipTool.TryGetPlane(Vector3.zero, Vector3.up, Vector3.up, 1, out _), Is.False);
        }

        [TestCase(ChiselClipMode.KeepBehind,  ChiselClipMode.KeepInFront)]
        [TestCase(ChiselClipMode.KeepInFront, ChiselClipMode.Split)]
        [TestCase(ChiselClipMode.Split,       ChiselClipMode.KeepBehind)]
        public void TheModes_GoRound(ChiselClipMode mode, ChiselClipMode next)
        {
            Assert.That(ChiselClipTool.NextMode(mode), Is.EqualTo(next));
        }

        [Test]
        public void TheTool_IsOneOfChiselsTools()
        {
            var context = ScriptableObject.CreateInstance<ChiselToolsContext>();
            try
            {
                Assert.That(context.GetAdditionalToolTypes(), Does.Contain(typeof(ChiselClipTool)));
            }
            finally
            {
                Object.DestroyImmediate(context);
            }
        }

        // a line can start on anything: off the brushes cut, on the surface the scene view shows nearest under the mouse
        [Test]
        public void OffTheBrushes_ThePointIsOnTheSurfaceUnderTheMouse()
        {
            var front = new PlaneIntersection(new Vector3(0, 0, 3), Vector3.forward);
            var ray   = new Ray(new Vector3(0, 0, 10), Vector3.back);

            Assert.That(ChiselClipTool.TryGetPoint(ray, new ChiselClipPicking.Brush[0], ToGUI, Vector2.zero, () => front,
                                                   out var point, out var surface, out var brush), Is.True);

            Assert.That(Vector3.Distance(point, front.point), Is.LessThan(1e-4f), $"{point}");
            Assert.That(Vector3.Distance(surface.normal, Vector3.forward), Is.LessThan(1e-4f), $"{surface.normal}");
            Assert.That(brush, Is.EqualTo(-1));
        }

        // picking in the scene view draws it again, so it is only done when the brushes cut aren't under the mouse
        [Test]
        public void OnTheBrushes_TheSceneIsNotPicked()
        {
            var brushes = Box();
            var ray     = new Ray(new Vector3(0.2f, 0.3f, 10), Vector3.back);
            var picked  = 0;

            Assert.That(ChiselClipTool.TryGetPoint(ray, brushes, ToGUI, ToGUI(ray.origin), () => { picked++; return null; },
                                                   out var point, out _, out var brush), Is.True);

            Assert.That(brush, Is.EqualTo(0));
            Assert.That(point.z, Is.EqualTo(1).Within(1e-4f), "on the front face");
            Assert.That(picked, Is.Zero);
        }

        [Test]
        public void WithNothingUnderTheMouse_ThereIsNoPoint()
        {
            var ray = new Ray(new Vector3(0, 0, 10), Vector3.back);
            Assert.That(ChiselClipTool.TryGetPoint(ray, new ChiselClipPicking.Brush[0], ToGUI, Vector2.zero, () => null,
                                                   out _, out _, out _), Is.False);
        }

        // a 2x2x2 box around the origin, seen down -z
        static ChiselClipPicking.Brush[] Box()
        {
            BrushMeshFactory.CreateBox(-Vector3.one, Vector3.one, out var box);
            box.CalculatePlanes();
            return new[] { new ChiselClipPicking.Brush(Matrix4x4.identity, box) };
        }

        // a line over the top of the box, and the point below its start
        static Vector3[] PlaneOnTop() => new[] { new Vector3(-1, 1, 0), new Vector3(1, 1, 0), new Vector3(-1, 0, 0) };

        // dragged over the box's front face, the point is on that face, not where it was along the view
        [Test]
        public void ADraggedPoint_MovesOverTheBrush()
        {
            var points = PlaneOnTop();
            var ray    = new Ray(new Vector3(0, 0, 10), Vector3.back);

            Assert.That(ChiselClipTool.TryDragPoint(points, 1, ray, Box(), ToGUI, ToGUI(ray.origin), () => null, out var plane), Is.True);

            Assert.That(Vector3.Distance(points[1], new Vector3(0, 0, 1)), Is.LessThan(1e-4f), $"{points[1]}");
            foreach (var point in points)
                Assert.That(Mathf.Abs(plane.GetDistanceToPoint(point)), Is.LessThan(1e-4f), "the plane goes through the points");
        }

        [Test]
        public void DraggedOffEverything_ThePointStays()
        {
            var points = PlaneOnTop();
            var ray    = new Ray(new Vector3(5, 0, 10), Vector3.back);

            Assert.That(ChiselClipTool.TryDragPoint(points, 1, ray, Box(), ToGUI, ToGUI(ray.origin), () => null, out _), Is.False);

            Assert.That(points, Is.EqualTo(PlaneOnTop()));
        }

        // on the line through the other two, the points make no plane
        [Test]
        public void DraggedInLineWithTheOthers_ThePointStays()
        {
            var points = PlaneOnTop();
            var ray    = new Ray(new Vector3(0, 5, 0), Vector3.down);
            var onLine = new PlaneIntersection(new Vector3(0, 1, 0), Vector3.up);

            Assert.That(ChiselClipTool.TryDragPoint(points, 2, ray, new ChiselClipPicking.Brush[0], ToGUI, Vector2.zero, () => onLine, out _), Is.False);

            Assert.That(points, Is.EqualTo(PlaneOnTop()));
        }

        // seen from above and in front: the box's top and front faces are seen, its bottom and back aren't
        static readonly Vector3 kDownForward = new Vector3(0, -1, -1).normalized;
        static Ray ViewFromAboveInFront(Vector3 point) => new(point - kDownForward * 20, kDownForward);

        // a line drawn over the box's top, front to back: the cut goes down through the box at x = 0
        static readonly Vector3 kTopStart = new(0, 1, 0.5f);
        static readonly Vector3 kTopEnd   = new(0, 1, -0.5f);

        // the third point is where the cut meets the brush's front face at its bottom, where it is seen, not inside
        // the brush or on its back
        [Test]
        public void TheThirdPoint_IsOnACornerOfTheCutThatIsSeen()
        {
            Assert.That(ChiselClipTool.TryGetPlane(kTopStart, kTopEnd, Vector3.up, 1, out var plane), Is.True);

            var third = ChiselClipTool.ThirdPoint(kTopStart, Vector3.up, plane, Box(), ViewFromAboveInFront);

            Assert.That(Vector3.Distance(third, new Vector3(0, -1, 1)), Is.LessThan(1e-4f), $"{third}");
        }

        // the points still make the plane drawn, facing the same way
        [Test]
        public void TheThirdPoint_KeepsThePlane()
        {
            Assert.That(ChiselClipTool.TryGetPlane(kTopStart, kTopEnd, Vector3.up, 1, out var plane), Is.True);

            var third = ChiselClipTool.ThirdPoint(kTopStart, Vector3.up, plane, Box(), ViewFromAboveInFront);

            var through = new Plane(kTopStart, kTopEnd, third);
            Assert.That(Vector3.Distance(through.normal, plane.normal), Is.LessThan(1e-4f), $"{through.normal} {plane.normal}");
        }

        // with no brush to cut, it is below the line's start, into the surface
        [Test]
        public void WithoutBrushes_TheThirdPointIsBelowTheStart()
        {
            Assert.That(ChiselClipTool.TryGetPlane(kTopStart, kTopEnd, Vector3.up, 1, out var plane), Is.True);

            var third = ChiselClipTool.ThirdPoint(kTopStart, Vector3.up, plane, new ChiselClipPicking.Brush[0], ViewFromAboveInFront);

            Assert.That(Vector3.Distance(third, kTopStart - Vector3.up), Is.LessThan(1e-4f), $"{third}");
        }

        // the cut the plane makes through a brush, to draw it
        [Test]
        public void TheCut_IsWhereThePlaneGoesThroughTheBrush()
        {
            var box   = Box()[0];
            var plane = new Plane(Vector3.right, Vector3.zero);

            var corners = ChiselClipTool.CutFace(box, plane);

            Assert.That(corners, Is.Not.Null);
            Assert.That(corners.Length, Is.EqualTo(4));
            foreach (var corner in corners)
            {
                var world = box.toWorld.MultiplyPoint(corner);
                Assert.That(world.x, Is.EqualTo(0).Within(1e-4f), $"{world}");
                Assert.That(Mathf.Abs(world.y), Is.EqualTo(1).Within(1e-4f), $"{world}");
                Assert.That(Mathf.Abs(world.z), Is.EqualTo(1).Within(1e-4f), $"{world}");
            }
            Assert.That(ChiselClipTool.CutFace(box, new Plane(Vector3.right, new Vector3(5, 0, 0))), Is.Null, "past the brush");
        }

        // looking down -z: x to the right, y up, 100 points a unit
        static Vector2 ToGUI(Vector3 position) => new(position.x * 100, -position.y * 100);

        //        scene view, clip tool -> in use
        [TestCase(true,  true,  true)]
        [TestCase(false, true,  false)]
        [TestCase(true,  false, false)]
        public void TheModeKey_IsInUseOnlyInTheClipTool(bool inSceneView, bool clipToolActive, bool expected)
        {
            Assert.That(ChiselClipTool.IsInUse(inSceneView, clipToolActive), Is.EqualTo(expected));
        }
    }
}
