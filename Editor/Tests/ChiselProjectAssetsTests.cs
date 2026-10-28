using System.Collections.Generic;
using Chisel.Components;
using Chisel.Core;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Chisel.Editors.Tests
{
    // Every project gets a contents list and paint settings with defaults, and a build preloads them, so a player
    // building brushes has the same types and profiles as the editor
    [TestFixture]
    public class ChiselProjectAssetsTests
    {
        ChiselContentsList storedList;
        ChiselContentsList storedInstance;
        ChiselPaintSettings storedPaint;
        Object[] storedPreloaded;
        readonly List<string> createdPaths = new();

        [SetUp]
        public void SetUp()
        {
            storedList      = ChiselProjectSettings.ContentsList;
            storedInstance  = ChiselContentsList.Instance;
            storedPaint     = ChiselPaintSettings.Project;
            storedPreloaded = PlayerSettings.GetPreloadedAssets();
        }

        [TearDown]
        public void TearDown()
        {
            ChiselProjectSettings.ContentsList = storedList;
            ChiselContentsList.Instance = storedList != null ? storedList : storedInstance;
            CompactHierarchyManager.ApplyContentsList();
            ChiselPaintSettings.Project = storedPaint;
            PlayerSettings.SetPreloadedAssets(storedPreloaded);
            foreach (var path in createdPaths)
                AssetDatabase.DeleteAsset(path);
            createdPaths.Clear();
        }

        T Created<T>(T asset) where T : Object
        {
            var path = AssetDatabase.GetAssetPath(asset);
            if (!string.IsNullOrEmpty(path))
                createdPaths.Add(path);
            return asset;
        }

        static bool IsPreloaded(Object asset) => System.Array.IndexOf(PlayerSettings.GetPreloadedAssets(), asset) >= 0;

        [Test]
        public void AProjectWithoutAContentsList_GetsOne_ThatABuildPreloads()
        {
            ChiselProjectSettings.ContentsList = null;
            var hadOne = AssetDatabase.FindAssets("t:" + nameof(ChiselContentsList)).Length > 0;

            var list = ChiselContentsSettings.EnsureProjectList();
            if (!hadOne)
                Created(list);

            Assert.That(list, Is.Not.Null);
            Assert.That(AssetDatabase.Contains(list), Is.True);
            Assert.That(ChiselProjectSettings.ContentsList, Is.SameAs(list));
            Assert.That(ChiselContentsList.Instance, Is.SameAs(list));
            Assert.That(IsPreloaded(list), Is.True);
        }

        [Test]
        public void ANewContentsList_HasTheDefaultTypes_AndIsPreloaded()
        {
            var list = Created(ChiselContentsSettings.Create());

            Assert.That(list.Names, Is.EqualTo(new[] { ChiselContentsList.kSolidName }));
            Assert.That(ChiselProjectSettings.ContentsList, Is.SameAs(list));
            Assert.That(IsPreloaded(list), Is.True);
        }

        [Test]
        public void AProjectsPaintSettings_AreMadeWhenMissing_AndPreloaded()
        {
            var settings = ChiselPaintSettingsProvider.EnsureProjectSettings();
            if (settings != storedPaint)
                Created(settings);

            Assert.That(settings, Is.Not.Null);
            Assert.That(ChiselPaintSettings.Project, Is.SameAs(settings));
            Assert.That(IsPreloaded(settings), Is.True);
        }

        // The package's paint shaders can be painted without setting anything up
        [Test]
        public void NewPaintSettings_PaintThePackagesShaders_AndArePreloaded()
        {
            var settings = Created(ChiselPaintSettingsProvider.CreateProjectAsset());

            var color = Shader.Find("Chisel/Test/Paint Color");
            var value = Shader.Find("Chisel/Test/Paint Value");
            Assert.That(color, Is.Not.Null);
            Assert.That(value, Is.Not.Null);
            Assert.That(ChiselPaintSettings.Project, Is.SameAs(settings));
            Assert.That(ChiselPaintProfiles.Find(color), Is.Not.Null);
            Assert.That(ChiselPaintProfiles.Find(value), Is.Not.Null);
            Assert.That(IsPreloaded(settings), Is.True);
            Assert.That(PlayerSettings.GetPreloadedAssets(), Has.Exactly(1).InstanceOf<ChiselPaintSettings>());
        }
    }
}
