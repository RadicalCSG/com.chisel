using System.Collections.Generic;
using Chisel.Components;
using Chisel.Core;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Chisel.Editors.Tests
{
    // Subtractive and intersecting brushes can't be seen on their own: shown, their outlines are drawn whether they're
    // selected or not, each in its own color, and a selected one is drawn as selected
    [TestFixture]
    public sealed class ChiselCarvingOutlinesTests
    {
        readonly List<Object> created = new();
        int undoGroup;
        bool showedSubtractive, showedIntersecting;
        Color subtractiveColor, intersectingColor;
        bool subtractiveColorSet, intersectingColorSet;
        Object[] selection;

        ChiselModelComponent model;
        ChiselBoxComponent   additive, subtractive, intersecting, inSubtractiveComposite;

        // the test runner reverts a run's undo steps when the run ends: a test's own are reverted before what it made goes
        [SetUp]
        public void SetUp()
        {
            Undo.IncrementCurrentGroup();
            undoGroup = Undo.GetCurrentGroup();
            showedSubtractive    = ChiselEditorSettings.ShowSubtractiveOutlines;
            showedIntersecting   = ChiselEditorSettings.ShowIntersectingOutlines;
            subtractiveColorSet  = ChiselColors.SubtractiveOutline.IsSet;
            intersectingColorSet = ChiselColors.IntersectingOutline.IsSet;
            subtractiveColor     = ChiselColors.SubtractiveOutline.Value;
            intersectingColor    = ChiselColors.IntersectingOutline.Value;
            selection            = Selection.objects;
            Selection.objects    = new Object[0];

            ChiselColors.SubtractiveOutline.Value  = new Color(0.9f, 0.1f, 0.1f, 1);
            ChiselColors.IntersectingOutline.Value = new Color(0.1f, 0.9f, 0.1f, 1);

            model = new GameObject("Model").AddComponent<ChiselModelComponent>();
            created.Add(model.gameObject);
            additive     = Box("Additive",     model.transform, CSGOperationType.Additive,     0);
            subtractive  = Box("Subtractive",  model.transform, CSGOperationType.Subtractive,  2);
            intersecting = Box("Intersecting", model.transform, CSGOperationType.Intersecting, 4);
            var composite = new GameObject("Subtractive Composite").AddComponent<ChiselCompositeComponent>();
            composite.transform.SetParent(model.transform, false);
            composite.Operation = CSGOperationType.Subtractive;
            inSubtractiveComposite = Box("In Subtractive Composite", composite.transform, CSGOperationType.Additive, 6);
            Update();
        }

        [TearDown]
        public void TearDown()
        {
            Selection.objects = selection;
            Undo.RevertAllDownToGroup(undoGroup);
            for (int i = created.Count - 1; i >= 0; i--)
            {
                if (created[i]) Object.DestroyImmediate(created[i]);
            }
            created.Clear();
            Update();

            ChiselCarvingOutlines.Show(CSGOperationType.Subtractive,  showedSubtractive);
            ChiselCarvingOutlines.Show(CSGOperationType.Intersecting, showedIntersecting);
            if (subtractiveColorSet)  ChiselColors.SubtractiveOutline.Value  = subtractiveColor; else ChiselColors.SubtractiveOutline.Reset();
            if (intersectingColorSet) ChiselColors.IntersectingOutline.Value = intersectingColor; else ChiselColors.IntersectingOutline.Reset();
            ChiselOutlineRenderer.Instance.UpdateBrushOutlinesNow();
        }

        static void Update()
        {
            for (int i = 0; i < 3; i++)
            {
                ChiselNodeHierarchyManager.Update();
                ChiselModelManager.Instance.UpdateModels();
            }
        }

        static ChiselBoxComponent Box(string name, Transform parent, CSGOperationType operation, float x)
        {
            var box = new GameObject(name).AddComponent<ChiselBoxComponent>();
            box.transform.SetParent(parent, false);
            box.transform.localPosition = new Vector3(x, 0, 0);
            box.Operation = operation;
            return box;
        }

        static List<CSGTreeBrush> BrushesOf(ChiselGeneratorComponent generator)
        {
            var brushes = new HashSet<CSGTreeBrush>();
            ChiselModelManager.Instance.GetAllTreeBrushes(generator, brushes);
            Assert.That(brushes, Is.Not.Empty, generator.name);
            return new List<CSGTreeBrush>(brushes);
        }

        static void Show(bool subtractive, bool intersecting)
        {
            ChiselCarvingOutlines.Show(CSGOperationType.Subtractive,  subtractive);
            ChiselCarvingOutlines.Show(CSGOperationType.Intersecting, intersecting);
            ChiselOutlineRenderer.Instance.UpdateBrushOutlinesNow();
        }

        static void AssertDrawn(ChiselGeneratorComponent generator, Color expected)
        {
            var renderer = ChiselOutlineRenderer.Instance;
            foreach (var brush in BrushesOf(generator))
            {
                Assert.That(renderer.HasOutline(brush), Is.True, generator.name + ": an outline");
                Assert.That(renderer.TryGetOutlineColor(brush, out var color, out _), Is.True, generator.name);
                Assert.That((Vector4)color, Is.EqualTo((Vector4)expected), generator.name + ": its color");
            }
        }

        static void AssertNotDrawn(ChiselGeneratorComponent generator)
        {
            var renderer = ChiselOutlineRenderer.Instance;
            foreach (var brush in BrushesOf(generator))
            {
                Assert.That(renderer.HasOutline(brush), Is.False, generator.name + ": an outline");
                Assert.That(renderer.TryGetOutlineColor(brush, out _, out _), Is.False, generator.name);
            }
        }

        [Test]
        public void Shown_SubtractiveAndIntersectingBrushes_AreDrawnInTheirColors()
        {
            Show(true, true);

            AssertDrawn(subtractive,            ColorManager.SubtractiveOutlineColor);
            AssertDrawn(inSubtractiveComposite, ColorManager.SubtractiveOutlineColor);
            AssertDrawn(intersecting,           ColorManager.IntersectingOutlineColor);
            AssertNotDrawn(additive);
        }

        [Test]
        public void NotShown_NothingIsDrawnForThem()
        {
            Show(false, false);

            AssertNotDrawn(subtractive);
            AssertNotDrawn(inSubtractiveComposite);
            AssertNotDrawn(intersecting);
        }

        [Test]
        public void EachKind_IsShownOnItsOwn()
        {
            Show(true, false);
            AssertDrawn(subtractive, ColorManager.SubtractiveOutlineColor);
            AssertNotDrawn(intersecting);

            Show(false, true);
            AssertNotDrawn(subtractive);
            AssertDrawn(intersecting, ColorManager.IntersectingOutlineColor);
        }

        [Test]
        public void ASelectedSubtractiveBrush_IsDrawnAsSelected()
        {
            Show(true, true);
            Selection.objects = new Object[] { subtractive.gameObject };
            ChiselOutlineRenderer.Instance.UpdateBrushOutlinesNow();

            AssertDrawn(subtractive, ColorManager.UnselectedOutlineColor);
            AssertDrawn(intersecting, ColorManager.IntersectingOutlineColor);
        }

        [Test]
        public void ABrushMadeSubtractive_IsShownOnceTheModelIsUpdated()
        {
            Show(true, true);
            AssertNotDrawn(additive);

            additive.Operation = CSGOperationType.Subtractive;
            Update();
            // as the scene view would, after the models were updated
            ChiselOutlineRenderer.Instance.UpdateBrushOutlinesNow(all: false);

            AssertDrawn(additive, ColorManager.SubtractiveOutlineColor);
        }

        [Test]
        public void WhatIsShown_IsKept()
        {
            Show(true, false);
            ChiselEditorSettings.Load();

            Assert.That(ChiselCarvingOutlines.Shows(CSGOperationType.Subtractive),  Is.True);
            Assert.That(ChiselCarvingOutlines.Shows(CSGOperationType.Intersecting), Is.False);
        }
    }
}
