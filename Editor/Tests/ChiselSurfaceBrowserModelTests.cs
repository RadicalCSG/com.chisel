using System.Collections.Generic;
using NUnit.Framework;

namespace Chisel.Editors.Tests
{
    [TestFixture]
    public sealed class ChiselSurfaceBrowserModelTests
    {
        static ChiselSurfaceBrowserModel.Item Item(string path, params string[] labels)
        {
            var name = path.Substring(path.LastIndexOf('/') + 1).Replace(".mat", "");
            return new ChiselSurfaceBrowserModel.Item { guid = name, path = path, name = name, labels = labels };
        }

        static ChiselSurfaceBrowserModel Model(params string[] hidden)
        {
            var model = new ChiselSurfaceBrowserModel();
            var hiddenFolders = new List<string>(hidden);
            model.isHidden = path => hiddenFolders.Exists(folder => path == folder || path.StartsWith(folder + "/"));
            model.SetItems(new[]
            {
                Item("Assets/Materials/Environment/Concrete/Concrete_Panels.mat", "Concrete", "Wall"),
                Item("Assets/Materials/Environment/Metal/Metal_Floor.mat", "metal", "Floor"),
                Item("Assets/Materials/Trims/Trim_Industrial.mat", "Metal", "Trim"),
                Item("Assets/Models/Characters/Guard.mat", "Character"),
                Item("Packages/com.chisel/Package Resources/Materials/Wall.mat"),
            });
            return model;
        }

        static List<string> Names(List<ChiselSurfaceBrowserModel.Item> items) { return items.ConvertAll(item => item.name); }

        [Test]
        public void Search_MatchesTheName_AndLabelsWithL()
        {
            var model = Model();
            Assert.That(Names(model.Filter("metal", ChiselSurfaceBrowserModel.QuickAccess.All)), Is.EqualTo(new[] { "Metal_Floor" }));
            Assert.That(Names(model.Filter("l:METAL", ChiselSurfaceBrowserModel.QuickAccess.All)), Is.EqualTo(new[] { "Metal_Floor", "Trim_Industrial" }), "labels ignore case");
            Assert.That(Names(model.Filter("trim l:metal", ChiselSurfaceBrowserModel.QuickAccess.All)), Is.EqualTo(new[] { "Trim_Industrial" }));
        }

        // Hidden folders leave their items out of the grid, counts and labels
        [Test]
        public void AHiddenFolder_IsLeftOutEverywhere()
        {
            var model = Model("Assets/Models");
            Assert.That(Names(model.Filter("", ChiselSurfaceBrowserModel.QuickAccess.All)), Does.Not.Contain("Guard"));
            Assert.That(model.Count(ChiselSurfaceBrowserModel.QuickAccess.All), Is.EqualTo(4));
            Assert.That(model.Labels().ConvertAll(pair => pair.Key), Does.Not.Contain("Character"));

            var roots = model.BuildFolders(includeHidden: false);
            Assert.That(roots[0].children.ConvertAll(folder => folder.name), Is.EqualTo(new[] { "Materials" }));
            Assert.That(roots[0].count, Is.EqualTo(3));

            var shown = model.BuildFolders(includeHidden: true);
            var models = shown[0].children.Find(folder => folder.name == "Models");
            Assert.That(models.hidden, Is.True);
            Assert.That(models.count, Is.EqualTo(1), "a hidden folder counts its own");
            Assert.That(shown[0].count, Is.EqualTo(3), "a shown one leaves hidden items out");
        }

        // Folders are trees under Assets and Packages, each counting its subfolders; only folders with items show
        [Test]
        public void Folders_CountTheirSubfolders()
        {
            var roots = Model().BuildFolders(includeHidden: false);
            Assert.That(roots.ConvertAll(folder => folder.name), Is.EqualTo(new[] { "Assets", "Packages" }));
            var materials = roots[0].children.Find(folder => folder.name == "Materials");
            Assert.That(materials.count, Is.EqualTo(3));
            Assert.That(materials.children.ConvertAll(folder => folder.name), Is.EqualTo(new[] { "Environment", "Trims" }));
            Assert.That(materials.children[0].children.ConvertAll(folder => folder.depth), Is.EqualTo(new[] { 3, 3 }));

            var model = Model();
            Assert.That(Names(model.Filter("", ChiselSurfaceBrowserModel.QuickAccess.All, "Assets/Materials/Environment")),
                        Is.EqualTo(new[] { "Concrete_Panels", "Metal_Floor" }), "a folder shows what's in its subfolders");
        }

        // Labels that differ only in case are one label, and adding one reuses its existing spelling
        [Test]
        public void Labels_AreCountedIgnoringCase()
        {
            var model = Model();
            var labels = model.Labels();
            var metal = labels.Find(pair => string.Equals(pair.Key, "metal", System.StringComparison.OrdinalIgnoreCase));
            Assert.That(metal.Value, Is.EqualTo(2));
            Assert.That(labels.FindAll(pair => string.Equals(pair.Key, "metal", System.StringComparison.OrdinalIgnoreCase)).Count, Is.EqualTo(1));
            Assert.That(model.ExistingLabelFor("FLOOR"), Is.EqualTo("Floor"));
            Assert.That(model.ExistingLabelFor("Glass"), Is.EqualTo("Glass"));
        }

        [Test]
        public void RecentlyUsed_KeepsItsOwnOrder()
        {
            var model = Model();
            model.recent = () => new[] { "Trim_Industrial", "Concrete_Panels" };
            Assert.That(Names(model.Filter("", ChiselSurfaceBrowserModel.QuickAccess.RecentlyUsed)), Is.EqualTo(new[] { "Trim_Industrial", "Concrete_Panels" }));
        }
    }
}
