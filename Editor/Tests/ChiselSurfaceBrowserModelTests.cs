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
            Assert.That(Names(model.Filter("", ChiselSurfaceBrowserModel.QuickAccess.All, new[] { "Assets/Materials/Environment" })),
                        Is.EqualTo(new[] { "Concrete_Panels", "Metal_Floor" }), "a folder shows what's in its subfolders");
            Assert.That(Names(model.Filter("", ChiselSurfaceBrowserModel.QuickAccess.All, new[] { "Assets/Materials/Trims", "Assets/Models" })),
                        Is.EqualTo(new[] { "Guard", "Trim_Industrial" }), "several folders show what's in any of them");
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

        // A filter asks for a trait or its absence; filters combine
        [Test]
        public void Traits_FilterWithAndWithout()
        {
            var model = Model("Assets/Models");
            model.traits.Add(new ChiselSurfaceBrowserModel.Trait { name = "Metal", has = item => System.Array.Exists(item.labels, l => l.ToLower() == "metal") });
            model.traits.Add(new ChiselSurfaceBrowserModel.Trait { name = "Trim",  has = item => item.name.StartsWith("Trim") });

            model.SetFilter("Metal", ChiselSurfaceBrowserModel.TraitFilter.With);
            Assert.That(Names(model.Filter("", ChiselSurfaceBrowserModel.QuickAccess.All)), Is.EqualTo(new[] { "Metal_Floor", "Trim_Industrial" }));
            model.SetFilter("Trim", ChiselSurfaceBrowserModel.TraitFilter.Without);
            Assert.That(Names(model.Filter("", ChiselSurfaceBrowserModel.QuickAccess.All)), Is.EqualTo(new[] { "Metal_Floor" }));
            model.SetFilter("Metal", ChiselSurfaceBrowserModel.TraitFilter.Without);
            Assert.That(Names(model.Filter("", ChiselSurfaceBrowserModel.QuickAccess.All)), Is.EqualTo(new[] { "Concrete_Panels", "Wall" }));
            model.SetFilter("Metal", ChiselSurfaceBrowserModel.TraitFilter.Any);
            model.SetFilter("Trim", ChiselSurfaceBrowserModel.TraitFilter.Any);
            Assert.That(model.Filter("", ChiselSurfaceBrowserModel.QuickAccess.All).Count, Is.EqualTo(4));

            Assert.That(model.CountWith(model.traits[0]), Is.EqualTo(2));
            Assert.That(ChiselSurfaceBrowserModel.Next(ChiselSurfaceBrowserModel.TraitFilter.Without), Is.EqualTo(ChiselSurfaceBrowserModel.TraitFilter.Any));
        }

        // Plain: none of the other traits; unticking it leaves out the materials that have none
        [Test]
        public void Plain_IsWithoutAnyOtherTrait()
        {
            var model = Model("Assets/Models");
            var metal = new ChiselSurfaceBrowserModel.Trait { name = "Metal", has = item => System.Array.Exists(item.labels, l => l.ToLower() == "metal") };
            model.traits.Add(metal);
            model.traits.Add(ChiselSurfaceBrowserModel.PlainTrait(new[] { metal }));
            model.SetFilter("Plain", ChiselSurfaceBrowserModel.TraitFilter.Without);
            Assert.That(Names(model.Filter("", ChiselSurfaceBrowserModel.QuickAccess.All)), Is.EqualTo(new[] { "Metal_Floor", "Trim_Industrial" }));
            Assert.That(model.CountWith(model.traits[1]), Is.EqualTo(2), "Concrete_Panels and Wall");
        }

        [Test]
        public void QuickAccess_DecalsAndNotSetUp()
        {
            var model = Model("Assets/Models");
            model.isDecal = item => item.name == "Trim_Industrial";
            model.isSetUp = item => item.name != "Wall";
            Assert.That(Names(model.Filter("", ChiselSurfaceBrowserModel.QuickAccess.Decals)), Is.EqualTo(new[] { "Trim_Industrial" }));
            Assert.That(Names(model.Filter("", ChiselSurfaceBrowserModel.QuickAccess.NotSetUp)), Is.EqualTo(new[] { "Wall" }));
        }

        [Test]
        public void Unused_IsWhatIsntInTheScene()
        {
            var model = Model("Assets/Models");
            model.isInScene = item => item.name == "Metal_Floor";
            Assert.That(Names(model.Filter("", ChiselSurfaceBrowserModel.QuickAccess.UnusedInThisScene)),
                        Is.EqualTo(new[] { "Concrete_Panels", "Trim_Industrial", "Wall" }), "hidden ones are left out too");
            Assert.That(model.Count(ChiselSurfaceBrowserModel.QuickAccess.UnusedInThisScene), Is.EqualTo(3));
        }

        [Test]
        public void CountPerFolder_IncludesSubfolders()
        {
            var counts = ChiselSurfaceBrowserModel.CountPerFolder(new[] { "Assets/A/B/x.mat", "Assets/A/y.mat", "Packages/P/z.mat" });
            Assert.That(counts["Assets"], Is.EqualTo(2));
            Assert.That(counts["Assets/A"], Is.EqualTo(2));
            Assert.That(counts["assets/a/b"], Is.EqualTo(1), "case is ignored");
            Assert.That(counts["Packages"], Is.EqualTo(1));
            Assert.That(counts.ContainsKey("Assets/C"), Is.False);
        }

        // A package's own folder shows its display name, so it's clear where a material comes from
        [Test]
        public void PackageFolders_ShowThePackagesName()
        {
            var chisel = UnityEditor.PackageManager.PackageInfo.FindForAssetPath("Packages/com.chisel");
            Assume.That(chisel, Is.Not.Null);
            Assert.That(ChiselSurfaceBrowserModel.FolderLabel("Packages/com.chisel", "com.chisel"), Is.EqualTo(chisel.displayName));
            Assert.That(ChiselSurfaceBrowserModel.FolderLabel("Packages/com.chisel/Editor", "Editor"), Is.EqualTo("Editor"));
            Assert.That(ChiselSurfaceBrowserModel.FolderLabel("Assets/Materials", "Materials"), Is.EqualTo("Materials"));
            Assert.That(ChiselSurfaceBrowserModel.PackageNameOf("Packages/com.chisel/Package Resources/x.mat"), Is.EqualTo(chisel.displayName));
            Assert.That(ChiselSurfaceBrowserModel.PackageNameOf("Assets/x.mat"), Is.Null);
        }

        [Test]
        public void HiddenFolders_OnlyThoseWithMaterialsAreCounted()
        {
            var counts = ChiselSurfaceBrowserModel.CountPerFolder(new[] { "Assets/A/x.mat", "Assets/B/C/y.mat" });
            Assert.That(ChiselProjectSettingsEditor.HiddenWithMaterials(new[] { "Assets/A", "Assets/Empty", "Assets/B/C" }, counts), Is.EqualTo(2));
        }

        [Test]
        public void ByDefault_UnitysPackagesAndChiselsDebugMaterialsAreHidden()
        {
            var defaults = ChiselProjectSettingsEditor.DefaultHiddenFolders();
            Assert.That(defaults, Has.Some.StartsWith("Packages/com.unity."));
            Assert.That(defaults, Has.Some.EndsWith("/DebugVisualization"));
            Assert.That(defaults, Has.None.EqualTo("Packages/com.chisel"));
        }

        // A material in a package from a registry can't be changed; one in the project or an embedded package can
        [Test]
        public void Materials_InReadOnlyPackages_AreNotEditable()
        {
            Assert.That(ChiselSurfaceBrowserWindow.IsEditable((string)null), Is.False);
            Assert.That(ChiselSurfaceBrowserWindow.IsEditable(null, openForEdit: true), Is.True, "in the project");
            Assert.That(ChiselSurfaceBrowserWindow.IsEditable(null, openForEdit: false), Is.False, "locked by version control");
            Assert.That(ChiselSurfaceBrowserWindow.IsEditable(UnityEditor.PackageManager.PackageSource.Embedded, true), Is.True);
            Assert.That(ChiselSurfaceBrowserWindow.IsEditable(UnityEditor.PackageManager.PackageSource.Local, true), Is.True);
            Assert.That(ChiselSurfaceBrowserWindow.IsEditable(UnityEditor.PackageManager.PackageSource.Registry, true), Is.False);
            Assert.That(ChiselSurfaceBrowserWindow.IsEditable(UnityEditor.PackageManager.PackageSource.BuiltIn, true), Is.False);
        }

        [Test]
        public void TheSettingsFolderTree_ListsThePackages()
        {
            Assert.That(ChiselProjectSettingsEditor.SubFoldersOf("Packages"), Does.Contain("Packages/com.chisel"));
        }

        [Test]
        public void TheSidebar_KeepsWithinTheWindow()
        {
            Assert.That(ChiselSurfaceBrowserWindow.ClampSidebarWidth(10, 1200, 380), Is.EqualTo(ChiselSurfaceBrowserWindow.kMinSidebarWidth));
            Assert.That(ChiselSurfaceBrowserWindow.ClampSidebarWidth(300, 1200, 380), Is.EqualTo(300));
            Assert.That(ChiselSurfaceBrowserWindow.ClampSidebarWidth(1000, 1200, 380), Is.EqualTo(1200 - 380 - ChiselSurfaceBrowserWindow.kMinGridWidth), "leaves the grid its smallest");
            Assert.That(ChiselSurfaceBrowserWindow.ClampDetailsWidth(1000, 1200, 0), Is.EqualTo(1200 - ChiselSurfaceBrowserWindow.kMinGridWidth), "the sidebar collapsed leaves the details more room");
        }

        [Test]
        public void ThePreviewHeight_StaysWithinItsLimits()
        {
            Assert.That(ChiselSurfaceBrowserWindow.ClampPreviewHeight(5), Is.EqualTo(ChiselSurfaceBrowserWindow.kMinPreviewHeight));
            Assert.That(ChiselSurfaceBrowserWindow.ClampPreviewHeight(300), Is.EqualTo(300));
            Assert.That(ChiselSurfaceBrowserWindow.ClampPreviewHeight(1e6f), Is.EqualTo(ChiselSurfaceBrowserWindow.kMaxPreviewHeight));
        }

        [Test]
        public void TheDetailsPanel_KeepsWithinTheWindow()
        {
            Assert.That(ChiselSurfaceBrowserWindow.ClampDetailsWidth(1000, 1200), Is.EqualTo(1200 - ChiselSurfaceBrowserWindow.kSidebarWidth - ChiselSurfaceBrowserWindow.kMinGridWidth));
            Assert.That(ChiselSurfaceBrowserWindow.ClampDetailsWidth(10, 1200), Is.EqualTo(ChiselSurfaceBrowserWindow.kMinDetailsWidth));
            Assert.That(ChiselSurfaceBrowserWindow.ClampDetailsWidth(400, 1200), Is.EqualTo(400));
            Assert.That(ChiselSurfaceBrowserWindow.ClampDetailsWidth(400, 500), Is.EqualTo(ChiselSurfaceBrowserWindow.kMinDetailsWidth), "a narrow window still keeps its smallest");
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
