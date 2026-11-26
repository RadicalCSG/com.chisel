using Chisel.Core;
using NUnit.Framework;
using UnityEngine;

namespace Chisel.Editors.Tests
{
    // The clip tool picks its points on the brushes themselves: a vertex or an edge near the mouse (on screen) before the
    // face under it, so a line can be drawn from edge to edge. A point on an edge or a vertex is on the face beside it
    // that faces the camera most.
    public sealed class ChiselClipPickingTests
    {
        // a 2x2x2 box around the origin, seen from the camera at z = 10 looking down -z
        static ChiselClipPicking.Brush Box()
        {
            BrushMeshFactory.CreateBox(-Vector3.one, Vector3.one, out var box);
            box.CalculatePlanes();
            return new ChiselClipPicking.Brush(Matrix4x4.identity, box);
        }

        static readonly Vector3 kCamera = new(0, 0, 10);

        // looking down -z: x to the right, y up, 100 points a unit
        static Vector2 ToGUI(Vector3 position) => new(position.x * 100, -position.y * 100);

        static Ray RayAt(Vector2 gui) => new(new Vector3(gui.x / 100, -gui.y / 100, kCamera.z), Vector3.back);

        static bool Pick(Vector2 gui, out ChiselClipPicking.Pick pick)
        {
            return ChiselClipPicking.TryPick(RayAt(gui), new[] { Box() }, ToGUI, gui, 8, out pick);
        }

        [Test]
        public void OnAFace_ThePointIsWhereTheMouseRayEntersIt()
        {
            Assert.That(Pick(new Vector2(20, -30), out var pick), Is.True);

            Assert.That(pick.kind, Is.EqualTo(ChiselClipPicking.Kind.Face));
            Assert.That(Vector3.Distance(pick.point, new Vector3(0.2f, 0.3f, 1)), Is.LessThan(1e-4f), $"{pick.point}");
            Assert.That(Vector3.Distance(pick.normal, Vector3.forward), Is.LessThan(1e-4f), $"{pick.normal}");
        }

        // the right edge of the front face is at x = 1 (100 points): 5 points off it, the point is on it
        [Test]
        public void NearAnEdge_ThePointIsOnTheEdge()
        {
            Assert.That(Pick(new Vector2(105, -30), out var pick), Is.True);

            Assert.That(pick.kind, Is.EqualTo(ChiselClipPicking.Kind.Edge));
            Assert.That(pick.point.x, Is.EqualTo(1).Within(1e-4f), $"{pick.point}");
            Assert.That(pick.point.z, Is.EqualTo(1).Within(1e-4f), "the edge nearest the camera");
            Assert.That(pick.point.y, Is.EqualTo(0.3f).Within(1e-3f), $"{pick.point}");
            Assert.That(Vector3.Distance(pick.normal, Vector3.forward), Is.LessThan(1e-4f), "the face beside it facing the camera");
        }

        [Test]
        public void NearAVertex_ThePointIsTheVertex()
        {
            Assert.That(Pick(new Vector2(96, -104), out var pick), Is.True);

            Assert.That(pick.kind, Is.EqualTo(ChiselClipPicking.Kind.Vertex));
            Assert.That(Vector3.Distance(pick.point, new Vector3(1, 1, 1)), Is.LessThan(1e-5f), $"{pick.point}");
        }

        // turned so its back corner (1, -1, -1) and the back edges beside it are seen through its front faces, on screen
        static ChiselClipPicking.Brush TurnedBox()
        {
            BrushMeshFactory.CreateBox(-Vector3.one, Vector3.one, out var box);
            box.CalculatePlanes();
            return new ChiselClipPicking.Brush(Matrix4x4.Rotate(Quaternion.Euler(25, 35, 0)), box);
        }

        static ChiselClipPicking.Pick PickOnTurnedBox(Vector3 local)
        {
            var box = TurnedBox();
            var gui = ToGUI(box.toWorld.MultiplyPoint(local));
            Assert.That(ChiselClipPicking.TryPick(RayAt(gui), new[] { box }, ToGUI, gui, 8, out var pick), Is.True);
            return pick;
        }

        // a corner behind the brush's front faces is hidden: the point is on the face in front of it
        [Test]
        public void OverAHiddenVertex_ThePointIsOnTheFaceInFront()
        {
            var pick = PickOnTurnedBox(new Vector3(1, -1, -1));

            Assert.That(pick.kind, Is.EqualTo(ChiselClipPicking.Kind.Face));
            Assert.That(pick.normal.z, Is.GreaterThan(0), "a face towards the camera");
        }

        [Test]
        public void OverAHiddenEdge_ThePointIsOnTheFaceInFront()
        {
            var pick = PickOnTurnedBox(new Vector3(0, -1, -1));

            Assert.That(pick.kind, Is.EqualTo(ChiselClipPicking.Kind.Face));
            Assert.That(pick.normal.z, Is.GreaterThan(0), "a face towards the camera");
        }

        // the corner nearest the camera is seen, on the faces it is on
        [Test]
        public void OverASeenVertex_ThePointIsTheVertex()
        {
            var box     = TurnedBox();
            var nearest = Vector3.zero;
            foreach (var vertex in box.mesh.vertices)
            {
                var world = box.toWorld.MultiplyPoint((Vector3)vertex);
                if (world.z > nearest.z)
                    nearest = world;
            }
            var gui = ToGUI(nearest);

            Assert.That(ChiselClipPicking.TryPick(RayAt(gui), new[] { box }, ToGUI, gui, 8, out var pick), Is.True);

            Assert.That(pick.kind, Is.EqualTo(ChiselClipPicking.Kind.Vertex));
            Assert.That(Vector3.Distance(pick.point, nearest), Is.LessThan(1e-5f), $"{pick.point}");
        }

        // a brush in front of another hides its corners too
        [Test]
        public void AVertexBehindAnotherBrush_IsNotPicked()
        {
            var back  = Box();
            BrushMeshFactory.CreateBox(-Vector3.one, Vector3.one, out var frontMesh);
            frontMesh.CalculatePlanes();
            var front = new ChiselClipPicking.Brush(Matrix4x4.Translate(new Vector3(0.5f, 0.5f, 4)), frontMesh);
            var gui   = new Vector2(100, -100); // the back box's corner (1, 1, 1), inside the front box on screen

            Assert.That(ChiselClipPicking.TryPick(RayAt(gui), new[] { back, front }, ToGUI, gui, 8, out var pick), Is.True);

            Assert.That(pick.brush, Is.EqualTo(1), "the brush in front");
            Assert.That(pick.point.z, Is.EqualTo(5).Within(1e-4f), "on its front face");
        }

        [Test]
        public void AwayFromTheBrush_NothingIsPicked()
        {
            Assert.That(Pick(new Vector2(300, -300), out _), Is.False);
        }
    }
}
