using Chisel.Components;
using Chisel.Core;
using NUnit.Framework;
using UnityEngine;

namespace Chisel.Editors.Tests
{
    // Vertices, edges and faces exactly behind each other on screen, as in an axis aligned orthographic view, are picked
    // nearest first: each one's pick distance gets a tiny bias that grows with its depth.
    public sealed class ChiselNearestPickTests
    {
        GameObject root;

        [TearDown]
        public void TearDown()
        {
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

        // a camera on the box's +z side looking down -z: z = 1 is in front, z = -1 behind it
        static readonly Vector3 kLookingDownZ = new(0, 0, -1);
        static readonly Vector3 kFront = new(1, 1, 1);
        static readonly Vector3 kBack  = new(1, 1, -1);

        [Test]
        public void OfTwoAtTheSameSpotOnScreen_TheNearerIsPicked()
        {
            var editable = EditableBox();
            editable.UpdatePickDepth(kLookingDownZ);

            Assert.That(editable.PickDistance(0, kFront), Is.LessThan(editable.PickDistance(0, kBack)));
            Assert.That(editable.PickDistance(2, kFront), Is.LessThan(editable.PickDistance(2, kBack)));
        }

        // the bias only breaks ties: what is nearer to the mouse is still picked first
        [Test]
        public void TheBias_DoesntOutweighTheDistanceOnScreen()
        {
            var editable = EditableBox();
            editable.UpdatePickDepth(kLookingDownZ);

            Assert.That(editable.PickDistance(0, kBack), Is.LessThan(editable.PickDistance(0.5f, kFront)));
            foreach (var position in editable.brushMesh.vertices)
            {
                var bias = editable.PickDistance(3, position) - 3;
                Assert.That(bias, Is.InRange(0, ChiselEditableOutline.kPickTieBias), $"{position}");
            }
        }

        // the depth of a point of the brush is taken along the camera's forward in world space, also when the brush is
        // turned and scaled unevenly
        [Test]
        public void TheDepthOrder_IsTheOneInTheWorld()
        {
            var localToWorld = Matrix4x4.TRS(new Vector3(3, -2, 5), Quaternion.Euler(20, 50, 10), new Vector3(1, 6, 0.25f));
            var forward      = new Vector3(0.3f, -0.4f, 0.87f).normalized;
            var direction    = ChiselEditableOutline.LocalDepthDirection(localToWorld, forward);

            var points = new[] { new Vector3(1, 0, 0), new Vector3(0, 1, 0), new Vector3(0, 0, 1), new Vector3(-1, 1, -1), new Vector3(1, -1, 2) };
            for (int a = 0; a < points.Length; a++)
            {
                for (int b = 0; b < points.Length; b++)
                {
                    var worldNearer = Vector3.Dot(localToWorld.MultiplyPoint(points[a]), forward) < Vector3.Dot(localToWorld.MultiplyPoint(points[b]), forward);
                    var localNearer = Vector3.Dot(points[a], direction) < Vector3.Dot(points[b], direction);
                    Assert.That(localNearer, Is.EqualTo(worldNearer), $"{points[a]} and {points[b]}");
                }
            }
        }
    }
}
