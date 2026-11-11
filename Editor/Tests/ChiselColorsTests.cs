using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Chisel.Editors.Tests
{
    // Chisel's colors can be set in its preferences; one that isn't set is its default, which can follow one of Unity's
    [TestFixture]
    public sealed class ChiselColorsTests
    {
        const string kTestKey = "Chisel/Test/Test";

        // What the user had for every color the tests touch, put back afterwards
        readonly Dictionary<string, string> savedPrefs = new();

        void Save(string key)
        {
            if (!savedPrefs.ContainsKey(key))
                savedPrefs[key] = EditorPrefs.HasKey(key) ? EditorPrefs.GetString(key) : null;
        }

        [SetUp]
        public void SetUp()
        {
            Save(kTestKey);
            foreach (var setting in ChiselColors.All)
                Save(setting.Key);
            EditorPrefs.DeleteKey(kTestKey);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var pair in savedPrefs)
            {
                if (pair.Value == null)
                    EditorPrefs.DeleteKey(pair.Key);
                else
                    EditorPrefs.SetString(pair.Key, pair.Value);
            }
            savedPrefs.Clear();
            ChiselColorSetting.ReadAgain();
        }

        static void AssertColor(Color actual, Color expected, string what = null)
        {
            Assert.That(actual.r, Is.EqualTo(expected.r).Within(1e-6f), what);
            Assert.That(actual.g, Is.EqualTo(expected.g).Within(1e-6f), what);
            Assert.That(actual.b, Is.EqualTo(expected.b).Within(1e-6f), what);
            Assert.That(actual.a, Is.EqualTo(expected.a).Within(1e-6f), what);
        }

        [Test]
        public void AColorThatIsntSet_IsItsDefault()
        {
            var setting = new ChiselColorSetting(kTestKey, "Test", "Test", "", new Color(0.1f, 0.2f, 0.3f, 0.4f));
            Assert.That(setting.IsSet, Is.False);
            AssertColor(setting.Value, new Color(0.1f, 0.2f, 0.3f, 0.4f));
        }

        [Test]
        public void ASetColor_IsKeptAcrossSessions()
        {
            var color = new Color(0.123456f, 0.654321f, 0.5f, 0.25f);
            new ChiselColorSetting(kTestKey, "Test", "Test", "", Color.white).Value = color;

            // read back by a new setting with the same key, as after a domain reload
            var again = new ChiselColorSetting(kTestKey, "Test", "Test", "", Color.white);
            Assert.That(again.IsSet, Is.True);
            AssertColor(again.Value, color);
        }

        // As Unity keeps the colors of its own Colors preferences: "name;r;g;b;a" under the name
        [Test]
        public void AColor_IsKeptAsUnityKeepsItsColors()
        {
            var color = new Color(0.25f, 0.5f, 0.75f, 1f);
            new ChiselColorSetting(kTestKey, "Test", "Test", "", Color.white).Value = color;

            var stored = EditorPrefs.GetString(kTestKey);
            Assert.That(stored, Does.StartWith(kTestKey + ";"));
            Assert.That(ColorUtility.TryParsePreferenceColor(stored, out var read), Is.True);
            AssertColor(read, color);
            AssertColor(ColorUtility.GetPreferenceColor(kTestKey, Color.white), color);
        }

        [Test]
        public void ResettingAColor_GoesBackToItsDefault()
        {
            var setting = new ChiselColorSetting(kTestKey, "Test", "Test", "", Color.green);
            setting.Value = Color.red;
            setting.Reset();
            Assert.That(setting.IsSet, Is.False);
            AssertColor(setting.Value, Color.green);
        }

        // A default taken from one of Unity's colors changes with it, until the color is set
        [Test]
        public void ADefaultFromUnity_FollowsItUntilTheColorIsSet()
        {
            var unity = Color.blue;
            var setting = new ChiselColorSetting(kTestKey, "Test", "Test", "", () => unity);
            unity = Color.yellow;
            AssertColor(setting.Value, Color.yellow);

            setting.Value = Color.magenta;
            unity = Color.cyan;
            AssertColor(setting.Value, Color.magenta);
        }

        [Test]
        public void TheOutlineColors_AreTheSettings()
        {
            ChiselColors.UnselectedOutline.Value     = new Color(0.1f, 0, 0, 1);
            ChiselColors.PreSelectedOutline.Value    = new Color(0.2f, 0, 0, 1);
            ChiselColors.SelectedOutline.Value       = new Color(0.3f, 0, 0, 1);
            ChiselColors.SelectedHoverOutline.Value  = new Color(0.4f, 0, 0, 1);

            AssertColor(ColorManager.UnselectedOutlineColor,    new Color(0.1f, 0, 0, 1), "unselected");
            AssertColor(ColorManager.PreSelectedOutlineColor,   new Color(0.2f, 0, 0, 1), "pre-selected");
            AssertColor(ColorManager.SelectedOutlineColor,      new Color(0.3f, 0, 0, 1), "selected");
            AssertColor(ColorManager.SelectedHoverOutlineColor, new Color(0.4f, 0, 0, 1), "selected and hovered");
        }

        [Test]
        public void TheHandleColors_AreTheSettings()
        {
            ChiselColors.Handles.Value       = new Color(0.1f, 0.2f, 0, 1);
            ChiselColors.HoveredHandle.Value = new Color(0.3f, 0.4f, 0, 1);
            ChiselColors.Measurements.Value  = new Color(0.5f, 0.6f, 0, 1);

            AssertColor(SceneHandles.HandleColor,       new Color(0.1f, 0.2f, 0, 1), "handles");
            AssertColor(SceneHandles.PreSelectionColor, new Color(0.3f, 0.4f, 0, 1), "hovered handle");
            AssertColor(SceneHandles.MeasureColor,      new Color(0.5f, 0.6f, 0, 1), "measurements");
        }

        // Every color has its own key, so setting one never sets another
        [Test]
        public void EveryColor_HasItsOwnKey()
        {
            var keys = new HashSet<string>();
            foreach (var setting in ChiselColors.All)
                Assert.That(keys.Add(setting.Key), Is.True, setting.Key);
        }

        // The brush editor draws polygons in their own color, edges and vertices in theirs, and backfaced ones fainter
        [Test]
        public void TheBrushEditorColors_AreTheSettings()
        {
            ChiselColors.BrushEditorEdges.Value    = new Color(0.1f, 0.2f, 0.3f, 0.8f);
            ChiselColors.BrushEditorPolygons.Value = new Color(0.4f, 0.5f, 0.6f, 0.5f);
            ChiselColors.Invalid.Value        = new Color(0.7f, 0.0f, 0.1f, 1.0f);

            var colors = ChiselEditableOutline.BaseColors();
            var faint  = SceneHandles.BackfaceAlphaMultiplier;
            AssertColor(colors.frontfacedEdge,    new Color(0.1f, 0.2f, 0.3f, 0.8f),          "front edge");
            AssertColor(colors.backfacedEdge,     new Color(0.1f, 0.2f, 0.3f, 0.8f * faint),  "back edge");
            AssertColor(colors.frontfacedPolygon, new Color(0.4f, 0.5f, 0.6f, 0.5f),          "front polygon");
            AssertColor(colors.backfacedPolygon,  new Color(0.4f, 0.5f, 0.6f, 0.5f * faint),  "back polygon");
            AssertColor(colors.invalid,           new Color(0.7f, 0.0f, 0.1f, 1.0f),          "invalid");
        }

        // Left unset, the brush editor looks as it did: Unity's z axis handle color for edges and polygons
        [Test]
        public void TheBrushEditorColors_StartAsUnitysAxisColor()
        {
            ChiselColors.BrushEditorEdges.Reset();
            ChiselColors.BrushEditorPolygons.Reset();
            ChiselColors.Invalid.Reset();

            var colors = ChiselEditableOutline.BaseColors();
            AssertColor(colors.frontfacedEdge,    Handles.zAxisColor, "edge");
            AssertColor(colors.frontfacedPolygon, Handles.zAxisColor, "polygon");
            AssertColor(colors.invalid,           Color.red,          "invalid");
        }
    }
}
