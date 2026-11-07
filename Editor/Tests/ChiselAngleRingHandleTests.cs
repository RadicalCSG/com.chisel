using NUnit.Framework;
using UnityEngine;

namespace Chisel.Editors.Tests
{
    // The ring handle that sets an angle around an axis, such as where spiral stairs start
    public sealed class ChiselAngleRingHandleTests
    {
        const float kEpsilon = 1e-4f;

        [TestCase(0.0f)]
        [TestCase(90.0f)]
        [TestCase(135.0f)]
        [TestCase(-60.0f)]
        public void Knob_IsInTheDirectionSpiralStairsStartTheirLowestStep(float angle)
        {
            var center = new Vector3(1, 2, 3);
            var knob   = AngleRingHandle.PointAt(center, Vector3.up, Vector3.forward, 2.0f, angle);

            var radians = angle * Mathf.Deg2Rad;
            var stairs  = center + new Vector3(Mathf.Sin(radians), 0, Mathf.Cos(radians)) * 2.0f;
            Assert.That(Vector3.Distance(knob, stairs), Is.LessThan(kEpsilon));
        }

        [TestCase(30.0f, 90.0f)]
        [TestCase(-45.0f, 270.0f)]
        [TestCase(120.0f, 400.0f)]
        public void Knob_CountedFromTheLowestStep_IsWhereTheTopStepIs(float startAngle, float turn)
        {
            var center         = new Vector3(1, 3, -2);
            var startDirection = Quaternion.AngleAxis(startAngle, Vector3.up) * Vector3.forward;
            var knob           = AngleRingHandle.PointAt(center, Vector3.up, startDirection, 2.0f, turn);

            var radians = (startAngle + turn) * Mathf.Deg2Rad;
            var top     = center + new Vector3(Mathf.Sin(radians), 0, Mathf.Cos(radians)) * 2.0f;
            Assert.That(Vector3.Distance(knob, top), Is.LessThan(kEpsilon));
        }

        [TestCase(0.0f)]
        [TestCase(45.0f)]
        [TestCase(170.0f)]
        [TestCase(-100.0f)]
        public void AngleOf_APointOnTheRing_IsItsAngle(float angle)
        {
            var center = new Vector3(-2, 0.5f, 4);
            var point  = AngleRingHandle.PointAt(center, Vector3.up, Vector3.forward, 3.0f, angle);
            // above or below the ring it is the same angle
            point.y += 1.5f;

            Assert.That(AngleRingHandle.AngleOf(center, Vector3.up, Vector3.forward, point), Is.EqualTo(angle).Within(kEpsilon));
        }

        [TestCase(90.0f)]
        [TestCase(-30.0f)]
        [TestCase(160.0f)]
        public void Mouse_OverTheRingOfAGeneratorAwayFromTheOrigin_IsAtTheAngleUnderIt(float angle)
        {
            // the ring is in the generator's space, the mouse ray in world space
            var toWorld = Matrix4x4.TRS(new Vector3(8, 0, -98), Quaternion.Euler(0, 25, 0), Vector3.one);
            var center  = new Vector3(0, 1, 0);
            var onRing  = toWorld.MultiplyPoint(AngleRingHandle.PointAt(center, Vector3.up, Vector3.forward, 2.0f, angle));
            var ray     = new Ray(onRing + new Vector3(0, 10, 0), Vector3.down);

            Assert.That(AngleRingHandle.TryGetRayAngle(toWorld.inverse, ray, center, Vector3.up, Vector3.forward, out var mouseAngle), Is.True);
            Assert.That(mouseAngle, Is.EqualTo(angle).Within(1e-3f));
        }

        [Test]
        public void Dragging_PastTheBackOfTheRing_KeepsTurningTheSameWay()
        {
            var turned = AngleRingHandle.AddTurn(0, 170, -170);

            Assert.That(turned, Is.EqualTo(20).Within(kEpsilon));
        }

        [Test]
        public void Dragging_AllTheWayAround_TurnsAFullTurn()
        {
            var turned = 0.0f;
            var mouse  = 0.0f;
            for (int i = 1; i <= 12; i++)
            {
                var next = Mathf.DeltaAngle(0, i * 30.0f);
                turned = AngleRingHandle.AddTurn(turned, mouse, next);
                mouse  = next;
            }

            Assert.That(turned, Is.EqualTo(360).Within(kEpsilon));
        }

        [Test]
        public void Snapping_RoundsToTheRotateStep_OnlyWhenOn()
        {
            Assert.That(AngleRingHandle.Snap(37, snapping: true,  snapStep: 15), Is.EqualTo(30));
            Assert.That(AngleRingHandle.Snap(38, snapping: true,  snapStep: 15), Is.EqualTo(45));
            Assert.That(AngleRingHandle.Snap(37, snapping: false, snapStep: 15), Is.EqualTo(37));
            Assert.That(AngleRingHandle.Snap(37, snapping: true,  snapStep: 0),  Is.EqualTo(37));
        }

        [Test]
        public void Readout_IsTheAngleRounded_WithoutPadding()
        {
            var text = AngleRingHandle.AngleText(120.04f);

            Assert.That(text, Does.StartWith("120"));
            Assert.That(text, Does.Not.Contain("."));
            Assert.That(text, Is.EqualTo(text.Trim()));
        }

        [Test]
        public void Readout_WithAName_SaysWhatTheAngleIs()
        {
            var text = AngleRingHandle.AngleText(45, "Start Angle");

            Assert.That(text, Does.StartWith("45"));
            Assert.That(text, Does.EndWith(" (Start Angle)"));
        }

        [TestCase(0.0f)]
        [TestCase(100.0f)]
        [TestCase(-150.0f)]
        public void Label_GoesAwayFromTheMiddle_FlatOnTheRing(float angle)
        {
            var center = new Vector3(3, 2, 1);
            var knob   = AngleRingHandle.PointAt(center, Vector3.up, Vector3.forward, 2.5f, angle);
            var away   = AngleRingHandle.LabelDirection(center, Vector3.up, knob);
            // past the knob along it, the label is further from the axis than the ring, so outside the stairs
            var label  = knob + away * 0.2f;

            Assert.That(away.y, Is.EqualTo(0).Within(kEpsilon));
            Assert.That(away.magnitude, Is.EqualTo(1).Within(kEpsilon));
            var flat = label - center; flat.y = 0;
            Assert.That(flat.magnitude, Is.EqualTo(2.7f).Within(kEpsilon));
        }
    }
}
