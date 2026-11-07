using System.Collections.Generic;
using Chisel.Core;
using NUnit.Framework;
using UnityEditor;

namespace Chisel.Editors.Tests
{
    [TestFixture]
    public sealed class ChiselSurfaceBrowserDataTests
    {
        List<string> savedHidden;
        readonly Dictionary<string, string> savedPrefs = new();

        [SetUp]
        public void SetUp()
        {
            savedHidden = new List<string>(ChiselProjectSettings.HiddenFolders);
            foreach (var folder in savedHidden)
                ChiselProjectSettings.UnhideFolder(folder);
            foreach (var key in ChiselSurfaceBrowserPrefs.StorageKeys)
            {
                savedPrefs[key] = EditorPrefs.HasKey(key) ? EditorPrefs.GetString(key) : null;
                EditorPrefs.DeleteKey(key);
            }
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var folder in new List<string>(ChiselProjectSettings.HiddenFolders))
                ChiselProjectSettings.UnhideFolder(folder);
            foreach (var folder in savedHidden)
                ChiselProjectSettings.HideFolder(folder);
            foreach (var pair in savedPrefs)
            {
                if (pair.Value == null) EditorPrefs.DeleteKey(pair.Key);
                else EditorPrefs.SetString(pair.Key, pair.Value);
            }
            savedPrefs.Clear();
        }

        // A hidden folder hides everything under it; a folder hidden under it is then hidden through it, and only
        // unhiding the folder itself shows it again
        [Test]
        public void HidingAFolder_HidesEverythingUnderIt()
        {
            ChiselProjectSettings.HideFolder("Assets/Models/Characters");
            ChiselProjectSettings.HideFolder("Assets/Models/");

            Assert.That(ChiselProjectSettings.HiddenFolders, Is.EqualTo(new[] { "Assets/Models" }), "the one under it goes through it");
            Assert.That(ChiselProjectSettings.IsHidden("Assets/Models/Characters/Guard.mat"), Is.True);
            Assert.That(ChiselProjectSettings.IsHidden("Assets/Models"), Is.True);
            Assert.That(ChiselProjectSettings.IsHidden("Assets/ModelsOther/Rock.mat"), Is.False, "a folder whose name only starts the same");
            Assert.That(ChiselProjectSettings.IsHidden("Assets/Materials/Rock.mat"), Is.False);

            Assert.That(ChiselProjectSettings.UnhideFolder("Assets/Models/Characters"), Is.False, "unhidden through its parent");
            Assert.That(ChiselProjectSettings.UnhideFolder("Assets/Models"), Is.True);
            Assert.That(ChiselProjectSettings.IsHidden("Assets/Models/Characters/Guard.mat"), Is.False);
        }

        [Test]
        public void HidingAFolder_CanBeUndone()
        {
            ChiselProjectSettings.HideFolder("Assets/Particles");
            Undo.PerformUndo();
            Assert.That(ChiselProjectSettings.IsHidden("Assets/Particles/Smoke.mat"), Is.False);
        }

        [Test]
        public void HidingSeveralFolders_IsOneUndo()
        {
            ChiselSurfaceBrowserWindow.HideFolders(new[] { "Assets/Particles", "Assets/Models" });
            Assert.That(ChiselProjectSettings.IsHidden("Assets/Particles/Smoke.mat"), Is.True);
            Assert.That(ChiselProjectSettings.IsHidden("Assets/Models/Guard.mat"), Is.True);
            Undo.PerformUndo();
            Assert.That(ChiselProjectSettings.HiddenFolders, Is.Empty);
        }

        [Test]
        public void RecentlyUsed_KeepsTheLatestFirst_AndNoMoreThanItsLimit()
        {
            for (int i = 0; i < ChiselSurfaceBrowserPrefs.kMaxRecent + 5; i++)
                ChiselSurfaceBrowserPrefs.MarkUsed("item" + i);
            ChiselSurfaceBrowserPrefs.MarkUsed("item3");

            var recent = ChiselSurfaceBrowserPrefs.Recent;
            Assert.That(recent.Count, Is.EqualTo(ChiselSurfaceBrowserPrefs.kMaxRecent));
            Assert.That(recent[0], Is.EqualTo("item3"), "used again: first, once");
            Assert.That(recent[1], Is.EqualTo("item" + (ChiselSurfaceBrowserPrefs.kMaxRecent + 4)));
            Assert.That(new HashSet<string>(recent).Count, Is.EqualTo(recent.Count));
        }

        // The scene's use of a material and of each decal image, counted once and again only after a change
        [Test]
        public void SceneUsage_IsCountedOnce_UntilSomethingChanges()
        {
            var created = new List<UnityEngine.Object>();
            try
            {
                var material = new UnityEngine.Material(UnityEngine.Shader.Find("Standard"));
                created.Add(material);

                var brushObject = new UnityEngine.GameObject("Brush");
                created.Add(brushObject);
                var brush = brushObject.AddComponent<Chisel.Components.ChiselBrushComponent>();
                var surfaces = brush.SurfaceDefinition?.surfaces;
                Assert.That(surfaces, Is.Not.Null.And.Not.Empty, "a new brush has surfaces");
                foreach (var surface in surfaces)
                    surface.RenderMaterial = material;

                for (int i = 0; i < 3; i++)
                {
                    var decalObject = new UnityEngine.GameObject("Decal");
                    created.Add(decalObject);
                    var decal = decalObject.AddComponent<Chisel.Components.ChiselDecalComponent>();
                    decal.Material  = material;
                    decal.ImageTile = i < 2 ? 1 : 0;
                }

                ChiselSceneMaterialUsage.SetDirty();
                Assert.That(ChiselSceneMaterialUsage.SurfacesUsing(material), Is.EqualTo(surfaces.Length));
                Assert.That(ChiselSceneMaterialUsage.DecalsUsing(material, 1), Is.EqualTo(2));
                Assert.That(ChiselSceneMaterialUsage.DecalsUsing(material, 0), Is.EqualTo(1));
                Assert.That(ChiselSceneMaterialUsage.DecalsUsing(material), Is.EqualTo(3));

                var counted = ChiselSceneMaterialUsage.RecountCount;
                ChiselSceneMaterialUsage.SurfacesUsing(material);
                ChiselSceneMaterialUsage.DecalsUsing(material, 1);
                Assert.That(ChiselSceneMaterialUsage.RecountCount, Is.EqualTo(counted), "asked again without a change: not counted again");

                UnityEngine.Object.DestroyImmediate(created[^1]);
                created.RemoveAt(created.Count - 1);
                ChiselSceneMaterialUsage.SetDirty();
                Assert.That(ChiselSceneMaterialUsage.DecalsUsing(material, 0), Is.EqualTo(0), "counted again after a change");
            }
            finally
            {
                for (int i = created.Count - 1; i >= 0; i--)
                {
                    if (created[i])
                        UnityEngine.Object.DestroyImmediate(created[i]);
                }
                ChiselSceneMaterialUsage.SetDirty();
            }
        }

        // Unity's undo doesn't cover labels; adding and removing them here can be undone and redone
        [Test]
        public void Labels_CanBeUndoneAndRedone()
        {
            const string kFolder = "Assets/ChiselLabelTests";
            if (!AssetDatabase.IsValidFolder(kFolder))
                AssetDatabase.CreateFolder("Assets", "ChiselLabelTests");
            try
            {
                var material = new UnityEngine.Material(UnityEngine.Shader.Find("Standard"));
                AssetDatabase.CreateAsset(material, kFolder + "/labelled.mat");

                ChiselAssetLabels.Add(material, "metal");
                Undo.IncrementCurrentGroup();
                ChiselAssetLabels.Add(material, "METAL");
                Assert.That(ChiselAssetLabels.Get(material), Is.EqualTo(new[] { "metal" }), "the same label in another case isn't added twice");
                ChiselAssetLabels.Add(material, "Trim");
                Undo.IncrementCurrentGroup();
                Assert.That(ChiselAssetLabels.Get(material), Is.EquivalentTo(new[] { "metal", "Trim" }));

                Undo.PerformUndo();
                Assert.That(ChiselAssetLabels.Get(material), Is.EqualTo(new[] { "metal" }));
                Undo.PerformRedo();
                Assert.That(ChiselAssetLabels.Get(material), Is.EquivalentTo(new[] { "metal", "Trim" }));

                ChiselAssetLabels.Remove(material, "metal");
                Undo.IncrementCurrentGroup();
                Assert.That(ChiselAssetLabels.Get(material), Is.EqualTo(new[] { "Trim" }));
                Undo.PerformUndo();
                Assert.That(ChiselAssetLabels.Get(material), Is.EquivalentTo(new[] { "metal", "Trim" }));
            }
            finally
            {
                AssetDatabase.DeleteAsset(kFolder);
            }
        }

        [Test]
        public void Rename_RenamesTheMaterialAsset()
        {
            const string kFolder = "Assets/ChiselRenameTests";
            if (!AssetDatabase.IsValidFolder(kFolder))
                AssetDatabase.CreateFolder("Assets", "ChiselRenameTests");
            try
            {
                var material = new UnityEngine.Material(UnityEngine.Shader.Find("Standard"));
                AssetDatabase.CreateAsset(material, kFolder + "/before.mat");
                var guid = AssetDatabase.AssetPathToGUID(kFolder + "/before.mat");
                Assert.That(ChiselSurfaceBrowserWindow.Rename(kFolder + "/before.mat", "  after "), Is.Null);
                Assert.That(AssetDatabase.GUIDToAssetPath(guid), Is.EqualTo(kFolder + "/after.mat"));
                Assert.That(ChiselSurfaceBrowserWindow.Rename(kFolder + "/after.mat", "after"), Is.Null, "the same name: nothing to do");
                Assert.That(ChiselSurfaceBrowserWindow.Rename(kFolder + "/after.mat", " "), Is.Not.Null, "no empty names");
            }
            finally
            {
                AssetDatabase.DeleteAsset(kFolder);
            }
        }

        [Test]
        public void Favorites_AreAddedAndRemoved()
        {
            ChiselSurfaceBrowserPrefs.SetFavorite("a", true);
            ChiselSurfaceBrowserPrefs.SetFavorite("b:3", true);
            ChiselSurfaceBrowserPrefs.SetFavorite("a", true);
            Assert.That(ChiselSurfaceBrowserPrefs.Favorites, Is.EqualTo(new[] { "a", "b:3" }));
            ChiselSurfaceBrowserPrefs.SetFavorite("a", false);
            Assert.That(ChiselSurfaceBrowserPrefs.IsFavorite("a"), Is.False);
            Assert.That(ChiselSurfaceBrowserPrefs.IsFavorite("b:3"), Is.True);
        }
    }
}
