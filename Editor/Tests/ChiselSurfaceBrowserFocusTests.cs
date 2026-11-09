using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Chisel.Editors.Tests
{
    [TestFixture]
    public sealed class ChiselSurfaceBrowserFocusTests
    {
        ChiselSurfaceBrowserWindow window;

        [SetUp]
        public void SetUp()
        {
            window = EditorWindow.CreateWindow<ChiselSurfaceBrowserWindow>();
            window.position = new Rect(100, 100, 1000, 600);
            window.SetPresetMode(false);
            window.ShowQuickAccess(ChiselSurfaceBrowserModel.QuickAccess.All);
            // a first event lays it out and fills it
            window.SendEvent(new Event { type = EventType.Layout });
        }

        [TearDown]
        public void TearDown()
        {
            if (window) window.Close();
            EditorGUIUtility.editingTextField = false;
        }

        // The arrow keys go through the grid unless a text field has the keys: a field left marked as being edited after
        // it lost them doesn't keep them from it
        [Test]
        public void TheGrid_TakesTheArrowKeys_UnlessATextFieldHasThem()
        {
            Assert.That(ChiselSurfaceBrowserWindow.GridTakesKeys(editingTextField: false, keyboardControl: 0), Is.True);
            Assert.That(ChiselSurfaceBrowserWindow.GridTakesKeys(editingTextField: true,  keyboardControl: 0), Is.True, "a field marked as edited without the keys");
            Assert.That(ChiselSurfaceBrowserWindow.GridTakesKeys(editingTextField: true,  keyboardControl: 12), Is.False);
        }

        // A click somewhere else in the window ends typing in a text field
        [Test]
        public void AClickElsewhere_EndsTyping()
        {
            EditorGUIUtility.editingTextField = true;
            // on the grid's header, which does nothing with a click: under the toolbar, and under the window's tab, which
            // an event sent to the window is placed below
            const float kTab = 24;
            var header = new Vector2(ChiselSurfaceBrowserWindow.kSidebarWidth + 20, kTab + EditorGUIUtility.singleLineHeight + 6 + 10);
            window.SendEvent(new Event { type = EventType.MouseDown, button = 0, mousePosition = header, clickCount = 1 });
            Assert.That(EditorGUIUtility.editingTextField, Is.False);
        }

        // Label chips keep apart from each other, and the labels a material has look unlike the ones that can be added
        [Test]
        public void LabelChips_AreApart_AndAppliedOnesStandOut()
        {
            var style = ChiselSurfaceBrowserWindow.ChipStyle();
            Assert.That(style.margin.right, Is.GreaterThan(0), "beside each other");
            Assert.That(style.margin.bottom, Is.GreaterThan(0), "row under row");
            Assert.That(ChiselSurfaceBrowserWindow.ChipTint(applied: true), Is.Not.EqualTo(ChiselSurfaceBrowserWindow.ChipTint(applied: false)));
            Assert.That(ChiselSurfaceBrowserWindow.ChipBorder(applied: true).a, Is.EqualTo(1), "a label it has is outlined");
            Assert.That(ChiselSurfaceBrowserWindow.ChipBorder(applied: false).a, Is.Zero, "one that can be added isn't");
            Assert.That(ChiselSurfaceBrowserWindow.kChipCornerRadius, Is.GreaterThan(0), "with rounded corners");
        }

        // A click in the grid where there's no card deselects
        [Test]
        public void AClickOnEmptyGridSpace_Deselects()
        {
            Assume.That(window.Shown.Count, Is.GreaterThan(0));
            window.Select(window.Shown[0].guid);
            // nothing found, so the whole grid is empty space
            window.SetSearch("no material is called this");
            Assume.That(window.Shown.Count, Is.Zero);
            const float kTab = 24;
            var inGrid = new Vector2(ChiselSurfaceBrowserWindow.kSidebarWidth + 100, kTab + 120);
            window.SendEvent(new Event { type = EventType.MouseDown, button = 0, mousePosition = inGrid, clickCount = 1 });
            Assert.That(window.SelectedGuid, Is.Null);
        }

        // A sidebar row's count is as wide as its number, at the row's end, so the name before it gets the rest
        [Test]
        public void ASidebarCount_IsAsWideAsItsNumber()
        {
            var row = new Rect(0, 0, 200, 18);
            var small = ChiselSurfaceBrowserWindow.CountRect(row, 2);
            var large = ChiselSurfaceBrowserWindow.CountRect(row, 12345);
            Assert.That(small.width, Is.LessThan(20), "one digit");
            Assert.That(large.width, Is.GreaterThan(small.width), "more digits, wider");
            Assert.That(small.xMax, Is.EqualTo(large.xMax), "both at the row's end");
            Assert.That(ChiselSurfaceBrowserWindow.WidthBefore(small, 6), Is.GreaterThan(170), "the name gets the rest");
            Assert.That(ChiselSurfaceBrowserWindow.WidthBefore(small, 6) + 6, Is.LessThan(small.x), "and doesn't run into it");
        }

        // A card being renamed that is no longer shown (another list picked) doesn't stay waiting to be renamed
        [Test]
        public void ARenameOutOfSight_IsEnded()
        {
            Assume.That(window.Shown.Count, Is.GreaterThan(0));
            var item = window.Shown[0];
            Assume.That(item.preset, Is.False);
            window.StartRename(item);
            Assert.That(window.IsRenamingCard, Is.True);
            // the name isn't changed, so nothing is renamed
            window.SetPresetMode(true);
            Assert.That(window.IsRenamingCard, Is.False);
        }
    }
}
