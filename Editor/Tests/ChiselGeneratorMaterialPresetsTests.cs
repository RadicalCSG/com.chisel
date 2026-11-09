using System.Collections.Generic;
using Chisel.Components;
using Chisel.Core;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Chisel.Editors.Tests
{
    [TestFixture]
    public sealed class ChiselGeneratorMaterialPresetsTests
    {
        readonly List<Object> created = new();
        readonly List<string> createdAssets = new();

        [TearDown]
        public void TearDown()
        {
            for (int i = created.Count - 1; i >= 0; i--)
            {
                if (created[i]) Object.DestroyImmediate(created[i]);
            }
            created.Clear();
            foreach (var path in createdAssets)
                AssetDatabase.DeleteAsset(path);
            createdAssets.Clear();
        }

        T Generator<T>(string name) where T : ChiselGeneratorComponent
        {
            var generator = new GameObject(name).AddComponent<T>();
            created.Add(generator.gameObject);
            return generator;
        }

        Material NewMaterial(string name)
        {
            var material = new Material(Shader.Find("Standard")) { name = name };
            created.Add(material);
            return material;
        }

        ChiselGeneratorMaterialPreset PresetOf(ChiselGeneratorComponent generator)
        {
            var preset = ScriptableObject.CreateInstance<ChiselGeneratorMaterialPreset>();
            created.Add(preset);
            preset.CopyFrom(generator);
            return preset;
        }

        static Material MaterialOf(ChiselGeneratorComponent generator, StairsSurface surface)
        {
            return generator.SurfaceDefinition.surfaces[(int)surface].RenderMaterial;
        }

        // A preset made from a generator has each of its parts by name, with the material it has now
        [Test]
        public void APreset_TakesAGeneratorsPartsByName()
        {
            var stairs = Generator<ChiselLinearStairsComponent>("Stairs");
            var wood = NewMaterial("Wood");
            stairs.SurfaceDefinition.surfaces[(int)StairsSurface.TreadTop].RenderMaterial = wood;

            var preset = PresetOf(stairs);
            Assert.That(preset.IsFor(stairs), Is.True);
            Assert.That(preset.MaterialFor("Tread Top"), Is.SameAs(wood));
            var parts = ChiselSurfaceBrowserWindow.PartsOf(new[] { stairs });
            Assert.That(preset.parts.ConvertAll(part => part.name), Is.EquivalentTo(parts.ConvertAll(part => part.name)), "the parts the browser shows");
            Assume.That(stairs.IsSurfaceUsed((int)StairsSurface.Base), Is.False);
            Assert.That(preset.parts.ConvertAll(part => part.name), Does.Contain("Base"), "the base, even without one");
        }

        // Applied, a preset gives every selected generator the materials of its parts, as one undo step
        [Test]
        public void APreset_DressesEveryGenerator_InOneUndoStep()
        {
            var source = Generator<ChiselLinearStairsComponent>("Source");
            var wood   = NewMaterial("Wood");
            var metal  = NewMaterial("Metal");
            source.SurfaceDefinition.surfaces[(int)StairsSurface.TreadTop].RenderMaterial  = wood;
            source.SurfaceDefinition.surfaces[(int)StairsSurface.RiserFront].RenderMaterial = metal;
            var preset = PresetOf(source);

            var a = Generator<ChiselLinearStairsComponent>("A");
            var b = Generator<ChiselLinearStairsComponent>("B");
            var before = MaterialOf(a, StairsSurface.TreadTop);
            Undo.IncrementCurrentGroup();
            Assert.That(ChiselGeneratorMaterialPresets.Apply(preset, new[] { a, b }), Is.EqualTo(2));
            foreach (var generator in new[] { a, b })
            {
                Assert.That(MaterialOf(generator, StairsSurface.TreadTop), Is.SameAs(wood));
                Assert.That(MaterialOf(generator, StairsSurface.RiserFront), Is.SameAs(metal));
            }

            Undo.PerformUndo();
            Assert.That(MaterialOf(a, StairsSurface.TreadTop), Is.SameAs(before));
            Assert.That(MaterialOf(b, StairsSurface.TreadTop), Is.SameAs(before));
        }

        // A part the preset doesn't set is left as it is
        [Test]
        public void APartThePresetDoesntSet_IsLeftAsItIs()
        {
            var source = Generator<ChiselLinearStairsComponent>("Source");
            var preset = PresetOf(source);
            var wood = NewMaterial("Wood");
            for (int i = 0; i < preset.parts.Count; i++)
            {
                var part = preset.parts[i];
                part.material = part.name == "Tread Top" ? wood : null;
                preset.parts[i] = part;
            }

            var stairs = Generator<ChiselLinearStairsComponent>("Stairs");
            var stone = NewMaterial("Stone");
            stairs.SurfaceDefinition.surfaces[(int)StairsSurface.RiserFront].RenderMaterial = stone;
            ChiselGeneratorMaterialPresets.Apply(preset, new[] { stairs });
            Assert.That(MaterialOf(stairs, StairsSurface.TreadTop), Is.SameAs(wood));
            Assert.That(MaterialOf(stairs, StairsSurface.RiserFront), Is.SameAs(stone));
        }

        // A generator of another kind takes the parts with the same name, and nothing else
        [Test]
        public void AnotherKindOfGenerator_TakesThePartsWithTheSameName()
        {
            var source = Generator<ChiselLinearStairsComponent>("Source");
            var wood = NewMaterial("Wood");
            source.SurfaceDefinition.surfaces[(int)StairsSurface.TreadTop].RenderMaterial = wood;
            var preset = PresetOf(source);

            var spiral = Generator<ChiselSpiralStairsComponent>("Spiral");
            Assume.That(spiral.IsSurfaceUsed((int)StairsSurface.TreadTop), Is.True);
            Assert.That(preset.IsFor(spiral), Is.False);
            ChiselGeneratorMaterialPresets.Apply(preset, new[] { spiral });
            Assert.That(MaterialOf(spiral, StairsSurface.TreadTop), Is.SameAs(wood));

            var box = Generator<ChiselBoxComponent>("Box");
            var boxBefore = new List<Material>();
            foreach (var surface in box.SurfaceDefinition.surfaces)
                boxBefore.Add(surface.RenderMaterial);
            var boxNames = ChiselSurfaceBrowserWindow.PartsOf(new[] { box }).ConvertAll(part => part.name);
            Assume.That(boxNames, Does.Not.Contain("Tread Top"));
            Assert.That(ChiselGeneratorMaterialPresets.Apply(preset, new[] { box }), Is.Zero);
            for (int i = 0; i < boxBefore.Count; i++)
                Assert.That(box.SurfaceDefinition.surfaces[i].RenderMaterial, Is.SameAs(boxBefore[i]));
        }

        // Saved as an asset, a preset is found with the others
        [Test]
        public void ASavedPreset_IsFoundWithTheOthers()
        {
            var stairs = Generator<ChiselLinearStairsComponent>("Stairs");
            var preset = ChiselGeneratorMaterialPresets.Create(stairs, "Assets/ChiselTestMaterialPreset.asset");
            Assert.That(preset, Is.Not.Null);
            createdAssets.Add(AssetDatabase.GetAssetPath(preset));
            Assert.That(ChiselGeneratorMaterialPresets.All(), Does.Contain(preset));
            Assert.That(ChiselGeneratorMaterialPresets.KindName(preset.generator), Is.EqualTo("Linear Stairs"));
        }

        // A new preset goes in the folder picked in the browser when it can hold assets, else in the presets' own folder
        [Test]
        public void ANewPreset_GoesInTheFolderPicked()
        {
            Assert.That(ChiselGeneratorMaterialPresets.FolderFor(new[] { "Assets/Stairs" }), Is.EqualTo("Assets/Stairs"));
            Assert.That(ChiselGeneratorMaterialPresets.FolderFor(new[] { "Packages/com.chisel/Package Resources" }), Is.EqualTo(ChiselGeneratorMaterialPresets.kPresetsFolder), "not in a package");
            Assert.That(ChiselGeneratorMaterialPresets.FolderFor(new[] { "Assets/A", "Assets/B" }), Is.EqualTo(ChiselGeneratorMaterialPresets.kPresetsFolder), "not one of several");
            Assert.That(ChiselGeneratorMaterialPresets.FolderFor(new string[0]), Is.EqualTo(ChiselGeneratorMaterialPresets.kPresetsFolder));

            const string kFolder = "Assets/ChiselTestPresets";
            Assume.That(AssetDatabase.IsValidFolder(kFolder), Is.False);
            createdAssets.Add(kFolder);
            var stairs = Generator<ChiselLinearStairsComponent>("Stairs");
            var preset = ChiselGeneratorMaterialPresets.CreateIn(stairs, kFolder);
            Assert.That(preset, Is.Not.Null, "the folder is made");
            Assert.That(AssetDatabase.GetAssetPath(preset), Is.EqualTo(kFolder + "/Stairs Materials.asset"));
        }

        // A duplicate is a preset of its own with the same parts
        [Test]
        public void ADuplicate_IsAPresetOfItsOwn()
        {
            const string kFolder = "Assets/ChiselTestPresets";
            Assume.That(AssetDatabase.IsValidFolder(kFolder), Is.False);
            createdAssets.Add(kFolder);
            var stairs = Generator<ChiselLinearStairsComponent>("Stairs");
            var wood = NewMaterial("Wood");
            var preset = ChiselGeneratorMaterialPresets.CreateIn(stairs, kFolder);
            var copy = ChiselGeneratorMaterialPresets.Duplicate(preset);
            Assert.That(copy, Is.Not.Null);
            Assert.That(copy, Is.Not.SameAs(preset));
            Assert.That(copy.parts.ConvertAll(part => part.name), Is.EqualTo(preset.parts.ConvertAll(part => part.name)));
            Assert.That(copy.generator, Is.EqualTo(preset.generator));

            var tread = preset.parts.FindIndex(part => part.name == "Tread Top");
            ChiselGeneratorMaterialPresets.SetMaterial(copy, tread, wood);
            Assert.That(preset.parts[tread].material, Is.Not.SameAs(wood), "the original keeps its own");
        }

        // A generator is dressed with a preset when every one of its parts has the preset's material; of the presets that
        // dress the selected generators, one made for their kind is found first
        [Test]
        public void ThePresetAGeneratorIsDressedWith_IsFound()
        {
            var stairs = Generator<ChiselLinearStairsComponent>("Stairs");
            var wood   = NewMaterial("Wood");
            var stone  = NewMaterial("Stone");
            stairs.SurfaceDefinition.surfaces[(int)StairsSurface.TreadTop].RenderMaterial = wood;
            var dressing = PresetOf(stairs);
            var spiral   = Generator<ChiselSpiralStairsComponent>("Spiral");
            var otherKind = PresetOf(spiral);
            for (int i = 0; i < otherKind.parts.Count; i++)
            {
                var part = otherKind.parts[i];
                part.material = dressing.MaterialFor(part.name) ?? part.material;
                otherKind.parts[i] = part;
            }
            var other = PresetOf(stairs);
            ChiselGeneratorMaterialPresets.SetMaterial(other, other.parts.FindIndex(part => part.name == "Tread Top"), stone);

            Assert.That(ChiselGeneratorMaterialPresets.Dresses(dressing, stairs), Is.True);
            Assert.That(ChiselGeneratorMaterialPresets.Dresses(other, stairs), Is.False, "one material differs");
            Assert.That(ChiselGeneratorMaterialPresets.PresetDressing(new[] { otherKind, other, dressing }, new[] { stairs }), Is.SameAs(dressing), "its own kind first");

            var second = Generator<ChiselLinearStairsComponent>("Second");
            Assert.That(ChiselGeneratorMaterialPresets.PresetDressing(new[] { dressing, other }, new[] { stairs, second }), Is.Null, "not every generator has it");

            // a part it leaves as it is isn't the generator's material: not exactly what it's dressed with
            var partial = PresetOf(stairs);
            ChiselGeneratorMaterialPresets.SetMaterial(partial, 0, null);
            Assert.That(ChiselGeneratorMaterialPresets.Dresses(partial, stairs), Is.False);
        }

        // In the browser's presets mode, selecting generators selects the preset they're dressed with, or none when they
        // aren't dressed with one, so a click on a preset then puts it on them; after none was selected too. Listing
        // materials, the selection is left alone
        [Test]
        public void SelectingAGenerator_SelectsThePresetItsDressedWith()
        {
            const string kFolder = "Assets/ChiselTestPresets";
            Assume.That(AssetDatabase.IsValidFolder(kFolder), Is.False);
            createdAssets.Add(kFolder);
            var stairs = Generator<ChiselLinearStairsComponent>("Stairs");
            var wood   = NewMaterial("Wood");
            var stone  = NewMaterial("Stone");
            stairs.SurfaceDefinition.surfaces[(int)StairsSurface.TreadTop].RenderMaterial = wood;
            var woodPreset = ChiselGeneratorMaterialPresets.CreateIn(stairs, kFolder);
            stairs.SurfaceDefinition.surfaces[(int)StairsSurface.TreadTop].RenderMaterial = stone;
            var stonePreset = ChiselGeneratorMaterialPresets.CreateIn(stairs, kFolder);
            // a material of its own, so no preset in the project dresses it
            var plain = Generator<ChiselLinearStairsComponent>("Plain");
            plain.SurfaceDefinition.surfaces[(int)StairsSurface.RiserFront].RenderMaterial = NewMaterial("Its Own");

            var window = EditorWindow.CreateWindow<ChiselSurfaceBrowserWindow>();
            try
            {
                window.SetPresetMode(false);
                window.ShowQuickAccess(ChiselSurfaceBrowserModel.QuickAccess.All);
                window.SendEvent(new Event { type = EventType.Layout });
                Assume.That(window.Shown.Count, Is.GreaterThan(0));
                var material = window.Shown[0].guid;
                window.SetPresetMode(true);
                window.Select(ChiselSurfaceBrowserPrefs.ItemOf(woodPreset));

                window.SelectPresetOf(new[] { stairs });
                Assert.That(window.SelectedGuid, Is.EqualTo(ChiselSurfaceBrowserPrefs.ItemOf(stonePreset)));
                window.SelectPresetOf(new[] { plain });
                Assert.That(window.SelectedGuid, Is.Null, "dressed with none");
                window.SelectPresetOf(new[] { stairs });
                Assert.That(window.SelectedGuid, Is.EqualTo(ChiselSurfaceBrowserPrefs.ItemOf(stonePreset)), "after none was selected");

                // listing materials: the selection is left alone
                window.SetPresetMode(false);
                window.Select(material);
                window.SelectPresetOf(new[] { stairs });
                Assert.That(window.SelectedGuid, Is.EqualTo(material));
            }
            finally { window.Close(); }
        }

        // A part's material is changed in the preset, undoably; none leaves the part as it is
        [Test]
        public void APresetsPartMaterial_IsChangedUndoably()
        {
            var source = Generator<ChiselLinearStairsComponent>("Source");
            var preset = PresetOf(source);
            var tread = preset.parts.FindIndex(part => part.name == "Tread Top");
            var riser = preset.parts.FindIndex(part => part.name == "Riser Front");
            var wood  = NewMaterial("Wood");
            var before = preset.parts[tread].material;

            Undo.IncrementCurrentGroup();
            ChiselGeneratorMaterialPresets.SetMaterial(preset, tread, wood);
            Assert.That(preset.MaterialFor("Tread Top"), Is.SameAs(wood));
            Undo.PerformUndo();
            Assert.That(preset.parts[tread].material, Is.SameAs(before));

            ChiselGeneratorMaterialPresets.SetMaterial(preset, riser, null);
            var stairs = Generator<ChiselLinearStairsComponent>("Stairs");
            var stone = NewMaterial("Stone");
            stairs.SurfaceDefinition.surfaces[(int)StairsSurface.RiserFront].RenderMaterial = stone;
            ChiselGeneratorMaterialPresets.Apply(preset, new[] { stairs });
            Assert.That(MaterialOf(stairs, StairsSurface.RiserFront), Is.SameAs(stone));
        }
    }
}
