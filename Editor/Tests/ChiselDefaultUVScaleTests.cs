using System.Collections.Generic;
using Chisel.Components;
using Chisel.Core;
using NUnit.Framework;
using UnityEngine;

namespace Chisel.Editors.Tests
{
    // New brushes start with the UV scale set in the preferences
    [TestFixture]
    public sealed class ChiselDefaultUVScaleTests
    {
        readonly List<Object> created = new();
        float previousScale;

        [SetUp]
        public void SetUp()
        {
            previousScale = ChiselEditorSettings.DefaultUVScale;
        }

        [TearDown]
        public void TearDown()
        {
            ChiselEditorSettings.DefaultUVScale = previousScale;
            foreach (var obj in created)
            {
                if (obj) Object.DestroyImmediate(obj);
            }
            created.Clear();
        }

        static Vector2 ScaleOf(ChiselSurface surface)
        {
            surface.surfaceDetails.UV0.Decompose(out _, out _, out var scale);
            return scale;
        }

        [Test]
        public void ANewBrush_GetsTheDefaultUVScale_OnEverySurface()
        {
            ChiselEditorSettings.DefaultUVScale = 2;
            var box = new GameObject("box").AddComponent<ChiselBoxComponent>();
            created.Add(box.gameObject);
            box.OnValidate();

            ChiselDefaultUVScale.GiveTo(box);
            foreach (var surface in box.SurfaceDefinition.surfaces)
            {
                var scale = ScaleOf(surface);
                Assert.That(scale.x, Is.EqualTo(2).Within(1e-4f));
                Assert.That(scale.y, Is.EqualTo(2).Within(1e-4f));
            }
        }

        // The placement tools give a brush its defaults again on every update while it is drawn
        [Test]
        public void GivingTheDefaultUVScaleAgain_DoesntCompound()
        {
            ChiselEditorSettings.DefaultUVScale = 2;
            var box = new GameObject("box").AddComponent<ChiselBoxComponent>();
            created.Add(box.gameObject);
            box.OnValidate();

            ChiselDefaultUVScale.GiveTo(box);
            ChiselDefaultUVScale.GiveTo(box);
            var scale = ScaleOf(box.SurfaceDefinition.surfaces[0]);
            Assert.That(scale.x, Is.EqualTo(2).Within(1e-4f));
        }

        [Test]
        public void TheDefaultUVScaleOfOne_LeavesTheSurfacesAsTheyAre()
        {
            ChiselEditorSettings.DefaultUVScale = 1;
            var box = new GameObject("box").AddComponent<ChiselBoxComponent>();
            created.Add(box.gameObject);
            box.OnValidate();
            var before = box.SurfaceDefinition.surfaces[0].surfaceDetails.UV0;

            ChiselDefaultUVScale.GiveTo(box);
            Assert.That(box.SurfaceDefinition.surfaces[0].surfaceDetails.UV0, Is.EqualTo(before));
        }
    }
}
