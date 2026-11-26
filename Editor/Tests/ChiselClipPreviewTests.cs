using System.Collections.Generic;
using System.Linq;
using Chisel.Components;
using Chisel.Core;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Chisel.Editors.Tests
{
    // The clip tool shows its cut on the brushes themselves, so what is kept is seen before it is committed. Changing the
    // plane or the mode cuts the brushes again from how they were; committing keeps the cut as one undo step, cancelling
    // puts the brushes back as they were.
    [TestFixture]
    public sealed class ChiselClipPreviewTests
    {
        const string kFolder = "Assets/__ChiselClipPreviewTests";

        readonly List<GameObject> objects = new();
        readonly Dictionary<string, string> savedPrefs = new();
        ChiselClipPreview preview;

        [SetUp]
        public void SetUp()
        {
            foreach (var key in ChiselSurfaceBrowserPrefs.StorageKeys)
                savedPrefs[key] = EditorPrefs.GetString(key, null);
            if (!AssetDatabase.IsValidFolder(kFolder))
                AssetDatabase.CreateFolder("Assets", kFolder.Substring("Assets/".Length));
            var cutMaterial = new Material(ChiselProjectSettings.DefaultWallMaterial);
            AssetDatabase.CreateAsset(cutMaterial, kFolder + "/Cut.mat");
            ChiselSurfaceBrowserPrefs.ActiveMaterial = cutMaterial;
            preview = new ChiselClipPreview();
            Undo.IncrementCurrentGroup();
        }

        [TearDown]
        public void TearDown()
        {
            preview.Cancel();
            foreach (var brush in Object.FindObjectsByType<ChiselBrushComponent>(FindObjectsInactive.Exclude))
                if (brush.name == kName) Object.DestroyImmediate(brush.gameObject);
            foreach (var go in objects)
                if (go) Object.DestroyImmediate(go);
            objects.Clear();
            AssetDatabase.DeleteAsset(kFolder);
            foreach (var (key, value) in savedPrefs)
            {
                if (value == null) EditorPrefs.DeleteKey(key);
                else EditorPrefs.SetString(key, value);
            }
            savedPrefs.Clear();
        }

        const string kName = "__ChiselClipPreviewTests Brush";

        // a 2x2x2 box brush around the position
        ChiselBrushComponent Box(Vector3 position)
        {
            BrushMeshFactory.CreateBox(-Vector3.one, Vector3.one, out var box);
            var go = new GameObject(kName);
            objects.Add(go);
            go.transform.position = position;
            var brush = go.AddComponent<ChiselBrushComponent>();
            brush.definition.BrushOutline = box;
            brush.OnValidate();
            return brush;
        }

        // the world plane x = at, in front where x is larger
        static Plane PlaneAt(float at) => new(Vector3.right, new Vector3(at, 0, 0));

        static (float min, float max) ExtentX(ChiselBrushComponent brush)
        {
            var toWorld = brush.GlobalTransformation;
            var xs      = brush.definition.BrushOutline.vertices.Select(v => toWorld.MultiplyPoint((Vector3)v).x).ToArray();
            return (xs.Min(), xs.Max());
        }

        static void AssertExtentX(ChiselBrushComponent brush, float min, float max)
        {
            var (actualMin, actualMax) = ExtentX(brush);
            Assert.That(actualMin, Is.EqualTo(min).Within(1e-4f), "least x");
            Assert.That(actualMax, Is.EqualTo(max).Within(1e-4f), "largest x");
        }

        static ChiselBrushComponent[] Brushes() =>
            Object.FindObjectsByType<ChiselBrushComponent>(FindObjectsInactive.Exclude).Where(b => b.name == kName).ToArray();

        [Test]
        public void Shown_TheBrushIsCut()
        {
            var brush = Box(new Vector3(5, 0, 0));

            preview.Begin(new[] { brush });
            preview.Show(PlaneAt(5.25f), ChiselClipMode.KeepBehind);

            AssertExtentX(brush, 4, 5.25f);
            Assert.That(preview.Left, Is.EquivalentTo(new[] { brush }));
        }

        // the other side of the same plane, not what is left of the cut already shown
        [Test]
        public void AnotherMode_CutsTheBrushAsItWas()
        {
            var brush = Box(new Vector3(5, 0, 0));
            preview.Begin(new[] { brush });
            preview.Show(PlaneAt(5.25f), ChiselClipMode.KeepBehind);

            preview.Show(PlaneAt(5.25f), ChiselClipMode.KeepInFront);

            AssertExtentX(brush, 5.25f, 6);
        }

        [Test]
        public void AMovedPlane_CutsTheBrushAsItWas()
        {
            var brush = Box(new Vector3(5, 0, 0));
            preview.Begin(new[] { brush });
            preview.Show(PlaneAt(5.25f), ChiselClipMode.KeepBehind);

            preview.Show(PlaneAt(5.5f), ChiselClipMode.KeepBehind);

            AssertExtentX(brush, 4, 5.5f);
        }

        [Test]
        public void FromSplitToKeeping_TheHalfSplitOffGoes()
        {
            var brush = Box(new Vector3(5, 0, 0));
            preview.Begin(new[] { brush });
            preview.Show(PlaneAt(5.25f), ChiselClipMode.Split);
            Assert.That(Brushes().Length, Is.EqualTo(2), "split");

            preview.Show(PlaneAt(5.25f), ChiselClipMode.KeepBehind);

            Assert.That(Brushes(), Is.EquivalentTo(new[] { brush }));
        }

        // a brush wholly on the side removed goes while shown, and comes back when the cut is shown another way
        [Test]
        public void ABrushThatWent_IsCutAgainAsItWas()
        {
            var brush = Box(new Vector3(10, 0, 0));
            preview.Begin(new[] { brush });
            preview.Show(PlaneAt(5.25f), ChiselClipMode.KeepBehind);
            Assert.That(Brushes(), Is.Empty, "gone");

            preview.Show(PlaneAt(10.5f), ChiselClipMode.KeepBehind);

            var back = Brushes();
            Assert.That(back.Length, Is.EqualTo(1));
            AssertExtentX(back[0], 9, 10.5f);
        }

        [TestCase(ChiselClipMode.KeepBehind)]
        [TestCase(ChiselClipMode.Split)]
        public void Cancelled_TheBrushesAreAsTheyWere(ChiselClipMode mode)
        {
            var cut  = Box(new Vector3(5, 0, 0));
            var gone = Box(new Vector3(10, 0, 0));
            preview.Begin(new[] { cut, gone });
            preview.Show(PlaneAt(5.25f), mode);
            preview.Show(PlaneAt(5.5f), mode);

            preview.Cancel();

            Assert.That(preview.IsShown, Is.False);
            var brushes = Brushes().OrderBy(b => b.transform.position.x).ToArray();
            Assert.That(brushes.Length, Is.EqualTo(2));
            AssertExtentX(brushes[0], 4, 6);
            AssertExtentX(brushes[1], 9, 11);
        }

        // while a line has no length there is no plane: the brushes are as they were until it has one again
        [Test]
        public void Hidden_TheBrushesAreAsTheyWere_UntilShownAgain()
        {
            var brush = Box(new Vector3(5, 0, 0));
            preview.Begin(new[] { brush });
            preview.Show(PlaneAt(5.25f), ChiselClipMode.KeepBehind);

            preview.Hide();

            Assert.That(preview.IsShown, Is.True, "still begun");
            AssertExtentX(brush, 4, 6);

            preview.Show(PlaneAt(5.5f), ChiselClipMode.KeepBehind);

            AssertExtentX(brush, 4, 5.5f);
        }

        [Test]
        public void Committed_TheCutIsOneUndoStep()
        {
            var brush = Box(new Vector3(5, 0, 0));
            preview.Begin(new[] { brush });
            preview.Show(PlaneAt(5.25f), ChiselClipMode.Split);
            preview.Show(PlaneAt(5.5f), ChiselClipMode.Split);

            var left = preview.Commit();

            Assert.That(preview.IsShown, Is.False);
            Assert.That(left.Count, Is.EqualTo(2));
            Assert.That(Brushes().Length, Is.EqualTo(2), "split");

            Undo.PerformUndo();

            Assert.That(Brushes(), Is.EquivalentTo(new[] { brush }));
            AssertExtentX(brush, 4, 6);
        }

        // undoing the cut to make it again calls Undo.undoRedoPerformed, which is then not an undo from elsewhere
        [Test]
        public void MadeAgain_ItIsKnownToBeReverting()
        {
            var brush = Box(new Vector3(5, 0, 0));
            var seen  = new List<bool>();
            void Seen() => seen.Add(preview.IsReverting);
            Undo.undoRedoPerformed += Seen;
            try
            {
                preview.Begin(new[] { brush });
                preview.Show(PlaneAt(5.25f), ChiselClipMode.KeepBehind);
                preview.Show(PlaneAt(5.5f), ChiselClipMode.KeepBehind);
                preview.Cancel();
            }
            finally
            {
                Undo.undoRedoPerformed -= Seen;
            }
            Assert.That(seen, Is.All.True);
            Assert.That(preview.IsReverting, Is.False);
        }

        // what the brushes were, to pick points on and to draw what is cut off, while they are shown cut
        [Test]
        public void TheBrushesAsTheyWere_AreKept()
        {
            var brush = Box(new Vector3(5, 0, 0));
            preview.Begin(new[] { brush });
            preview.Show(PlaneAt(5.25f), ChiselClipMode.KeepBehind);

            Assert.That(preview.Originals.Count, Is.EqualTo(1));
            var original = preview.Originals[0];
            var xs = original.mesh.vertices.Select(v => original.toWorld.MultiplyPoint((Vector3)v).x).ToArray();
            Assert.That(xs.Max(), Is.EqualTo(6).Within(1e-4f));
        }
    }
}
