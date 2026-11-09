using System.Collections.Generic;
using Chisel.Components;
using Chisel.Core;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;

namespace Chisel.Editors.Tests
{
    // Choosing the tile surfaces keep: the selected surfaces by hotspot material, how each tile fits them, what the field
    // says, and pinning a tile on one material's surfaces
    [TestFixture]
    public class HotspotTileChoiceTests
    {
        const string kFolder = "Assets/__HotspotTileChoiceTests";

        readonly List<GameObject> roots = new();
        readonly List<Object> created = new();
        Material trim, facade, plain;

        [SetUp]
        public void SetUp()
        {
            if (Object.FindObjectsByType<ChiselModelComponent>(FindObjectsInactive.Include).Length > 0)
                Assert.Ignore("A Chisel model is loaded; run these in a scene without one");
            if (!AssetDatabase.IsValidFolder(kFolder))
                AssetDatabase.CreateFolder("Assets", kFolder.Substring("Assets/".Length));
            trim   = HotspotMaterial("Trim_Industrial_A", "A1", "B1", corners: true);
            facade = HotspotMaterial("Hotspot_Facade_B", "C1", "D1");
            plain  = new Material(ChiselProjectSettings.DefaultWallMaterial);
            AssetDatabase.CreateAsset(plain, kFolder + "/plain.mat");
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var root in roots)
                if (root) Object.DestroyImmediate(root);
            roots.Clear();
            Update();
            AssetDatabase.DeleteAsset(kFolder);
            foreach (var item in created)
                if (item) Object.DestroyImmediate(item);
            created.Clear();
        }

        // corners: the second tile is bordered, its inside corner pieces a third tile, IC1
        Material HotspotMaterial(string name, string first, string second, bool corners = false)
        {
            var layout = ScriptableObject.CreateInstance<ChiselHotspotLayout>();
            created.Add(layout);
            layout.size = new Vector2Int(2048, 2048);
            layout.pixelsPerUnit = 256;
            layout.tiles = new List<HotspotTile>
            {
                new() { id = 1, name = first,  points = new[] { new Vector2Int(0, 0), new Vector2Int(512, 0), new Vector2Int(512, 512), new Vector2Int(0, 512) } },
                new() { id = 2, name = second, points = new[] { new Vector2Int(1024, 0), new Vector2Int(2048, 0), new Vector2Int(2048, 512), new Vector2Int(1024, 512) } },
            };
            if (corners)
            {
                var bordered = layout.tiles[1];
                bordered.border = 16;
                bordered.insideCorners = 3;
                layout.tiles[1] = bordered;
                layout.tiles.Add(new() { id = 3, name = "IC1", flags = HotspotTileFlags.InsideCorners,
                                         points = new[] { new Vector2Int(0, 1024), new Vector2Int(64, 1024), new Vector2Int(64, 1088), new Vector2Int(0, 1088) } });
            }
            var material = new Material(ChiselProjectSettings.DefaultWallMaterial);
            AssetDatabase.CreateAsset(material, $"{kFolder}/{name}.mat");
            material.AddMetadataOfType<ChiselSurfaceMetadata>().hotspotLayout = layout;
            return material;
        }

        static void Update()
        {
            for (int i = 0; i < 3; i++)
            {
                ChiselNodeHierarchyManager.Update();
                ChiselModelManager.Instance.UpdateModels();
            }
        }

        ChiselModelComponent Model()
        {
            var root = new GameObject("hotspot tile choice test");
            roots.Add(root);
            return root.AddComponent<ChiselModelComponent>();
        }

        ChiselBoxComponent Box(ChiselModelComponent model, float3 min, float3 max, Material material)
        {
            var go = new GameObject("box");
            go.transform.SetParent(model.transform, false);
            var box = go.AddComponent<ChiselBoxComponent>();
            if (math.any(min > box.Max)) { box.Max = max; box.Min = min; }
            else                         { box.Min = min; box.Max = max; }
            Update();
            foreach (var surface in box.SurfaceDefinition.surfaces)
                surface.SetMaterial(material);
            box.OnValidate();
            Update();
            return box;
        }

        static List<SurfaceReference> SurfacesOf(ChiselGeneratorComponent generator)
        {
            var surfaces = new List<SurfaceReference>();
            ChiselSurfaceSelectionManager.GetAllSurfaceReferences(generator, surfaces);
            return surfaces;
        }

        // a box of each kind of material, every surface of each
        (List<SurfaceReference> trims, List<SurfaceReference> facades, List<SurfaceReference> plains) Boxes()
        {
            var model = Model();
            var trims   = SurfacesOf(Box(model, new float3(0, 0, 0),  new float3(2, 2, 2),  trim));
            var facades = SurfacesOf(Box(model, new float3(5, 0, 0),  new float3(7, 2, 2),  facade));
            var plains  = SurfacesOf(Box(model, new float3(10, 0, 0), new float3(12, 2, 2), plain));
            Update();
            return (trims, facades, plains);
        }

        // The surfaces by hotspot material, the one most use first; ordinary materials have no tiles to choose
        [Test]
        public void TheSurfaces_AreGroupedByHotspotMaterial_MostUsedFirst()
        {
            var (trims, facades, plains) = Boxes();
            Assume.That(trims.Count, Is.GreaterThan(2));
            var selected = new List<SurfaceReference>();
            selected.AddRange(facades.GetRange(0, 2));
            selected.AddRange(trims);
            selected.AddRange(plains);

            var groups = HotspotTileChoice.GroupsOf(selected, (_, _) => null);
            Assert.That(groups.ConvertAll(group => group.material), Is.EqualTo(new[] { trim, facade }));
            Assert.That(groups[0].Count, Is.EqualTo(trims.Count));
            Assert.That(groups[1].Count, Is.EqualTo(2));
        }

        // "Applies to N of M" counts the surfaces with a hotspot material only; the others aren't changed either
        [Test]
        public void AppliesTo_CountsOnlyHotspotSurfaces()
        {
            var (trims, facades, plains) = Boxes();
            var selected = new List<SurfaceReference>(facades.GetRange(0, 2));
            selected.AddRange(trims);
            selected.AddRange(plains);
            var groups = HotspotTileChoice.GroupsOf(selected, (_, _) => null);
            var facadeGroup = groups.Find(group => group.material == facade);
            Assert.That(HotspotTileChoice.AppliesTo(facadeGroup, groups), Is.EqualTo($"Applies to 2 of {2 + trims.Count} surfaces"));
        }

        // How many surfaces each tile fits: all, some or none; a surface whose shape isn't known yet counts as fitting
        [Test]
        public void EachTile_FitsAllSomeOrNoneOfTheSurfaces()
        {
            var (trims, _, _) = Boxes();
            var four = trims.GetRange(0, 4);
            var a1 = new Vector2Int(0, 1);
            var b1 = new Vector2Int(0, 2);
            // A1 fits every one, B1 only the first; the last isn't known
            var group = HotspotTileChoice.GroupsOf(four, (surface, _) =>
                surface == four[3] ? null : surface == four[0] ? new HashSet<Vector2Int> { a1, b1 } : new HashSet<Vector2Int> { a1 })[0];

            Assert.That(group.FitCount(a1), Is.EqualTo(4));
            Assert.That(group.FitOf(a1), Is.EqualTo(HotspotTileFit.All));
            Assert.That(group.FitCount(b1), Is.EqualTo(2), "the first, and the one not known");
            Assert.That(group.FitOf(b1), Is.EqualTo(HotspotTileFit.Some));
            Assert.That(group.FitOf(new Vector2Int(1, 1)), Is.EqualTo(HotspotTileFit.Some), "only the one not known: another sheet's tile");
            Assert.That(HotspotTileChoice.Summary(group, group.layout, 0), Is.EqualTo("1 fit all 4 \u00B7 1 fit some"));
            Assert.That(HotspotTileChoice.FitText(group, b1), Is.EqualTo("Fits 2 of 4"));
            Assert.That(group.CanPin(b1), Is.True);
        }

        // Inside corner pieces never fit, not even a surface whose shape isn't known, and can't be pinned
        [Test]
        public void InsideCornerPieces_NeverFit()
        {
            var (trims, _, _) = Boxes();
            var pieces = new Vector2Int(0, 3);
            var group = HotspotTileChoice.GroupsOf(trims, (_, _) => null)[0];
            Assert.That(group.FitOf(pieces), Is.EqualTo(HotspotTileFit.None));
            Assert.That(group.CanPin(pieces), Is.False);
            Assert.That(HotspotTileChoice.Summary(group, group.layout, 0), Is.EqualTo($"2 fit all {trims.Count}"), "counted as not fitting");
            Assert.That(HotspotTileChoice.FitText(group, pieces), Is.EqualTo("Inside corner pieces, never matched"));
        }

        // A bordered tile names the tile with its inside corner pieces, and those pieces the tiles that use them
        [Test]
        public void InsideCornerPieces_AndTheTilesUsingThem()
        {
            var layout = HotspotTransformGUI.LayoutOf(trim);
            Assert.That(HotspotTileChoice.CornersOf(layout, 2), Is.EqualTo(3));
            Assert.That(HotspotTileChoice.CornersOf(layout, 1), Is.Zero, "no border pieces");
            Assert.That(HotspotTileChoice.UsersOf(layout, 3), Is.EqualTo(new[] { 2 }));
            Assert.That(HotspotTileChoice.UsersOf(layout, 1), Is.Empty);
        }

        // The info row: pixels, aspect, flags
        [Test]
        public void TheInfoRow_SaysSizeAspectAndFlags()
        {
            var layout = HotspotTransformGUI.LayoutOf(trim);
            Assert.That(HotspotTileChoice.InfoOf(layout, 2), Is.EqualTo("1024 \u00D7 512 \u00B7 2 : 1 \u00B7 Border"));
            Assert.That(HotspotTileChoice.InfoOf(layout, 3), Is.EqualTo("64 \u00D7 64 \u00B7 1 : 1 \u00B7 Inside corners"));
            Assert.That(HotspotTileChoice.AspectText(1000, 301), Is.EqualTo("3.32 : 1"), "not a ratio of small numbers");
            Assert.That(HotspotTileChoice.InfoOf(layout, 99), Is.Empty);
        }

        // Without a stand-in, the fits are matching's own: on these boxes every rectangle tile fits every side
        [Test]
        public void MatchingsOwnFits_AreCounted()
        {
            var (trims, _, _) = Boxes();
            var group = HotspotTileChoice.GroupsOf(trims)[0];
            Assert.That(group.FitCount(new Vector2Int(0, 1)), Is.EqualTo(trims.Count));
            Assert.That(group.FitOf(new Vector2Int(0, 2)), Is.EqualTo(HotspotTileFit.All));
        }

        // The field says what each material's surfaces keep, most used first
        [Test]
        public void TheField_SaysWhatEachMaterialKeeps()
        {
            var (trims, facades, _) = Boxes();
            var groups = HotspotTileChoice.GroupsOf(new List<SurfaceReference>(trims) { facades[0] }, (_, _) => null);
            Assert.That(HotspotTileChoice.FieldText(groups), Is.EqualTo("Automatic / Automatic"));
            HotspotTileChoice.Pin(groups[0], new Vector2Int(0, 2));
            groups = HotspotTileChoice.GroupsOf(new List<SurfaceReference>(trims) { facades[0] }, (_, _) => null);
            Assert.That(HotspotTileChoice.FieldText(groups), Is.EqualTo("B1 / Automatic"));
        }

        // A tile pinned for one material's surfaces is pinned on them only, and undone in one step
        [Test]
        public void PinningATile_ChangesOnlyThatMaterialsSurfaces_AndIsUndone()
        {
            var (trims, facades, _) = Boxes();
            var selected = new List<SurfaceReference>(trims);
            selected.AddRange(facades);
            // as many of each: the trim's group by its material
            var trimGroup = HotspotTileChoice.GroupsOf(selected, (_, _) => null).Find(group => group.material == trim);

            Undo.IncrementCurrentGroup();
            Assert.That(HotspotTileChoice.Pin(trimGroup, new Vector2Int(0, 1)), Is.True);
            foreach (var surface in trims)
                Assert.That(surface.SurfaceDetails.hotspot.pinnedTile, Is.EqualTo(1));
            foreach (var surface in facades)
                Assert.That(surface.SurfaceDetails.hotspot.pinnedTile, Is.Zero, "the other material's surfaces");
            Assert.That(HotspotTileChoice.Pin(trimGroup, new Vector2Int(0, 1)), Is.False, "pinned already");

            Undo.PerformUndo();
            foreach (var surface in trims)
                Assert.That(surface.SurfaceDetails.hotspot.pinnedTile, Is.Zero);
        }

        // Surfaces keeping different tiles are mixed
        [Test]
        public void SurfacesKeepingDifferentTiles_AreMixed()
        {
            var (trims, _, _) = Boxes();
            var one = HotspotTileChoice.GroupsOf(trims.GetRange(0, 1), (_, _) => null)[0];
            HotspotTileChoice.Pin(one, new Vector2Int(0, 2));
            var group = HotspotTileChoice.GroupsOf(trims, (_, _) => null)[0];
            Assume.That(trims[0].descriptionIndex, Is.Not.EqualTo(trims[trims.Count - 1].descriptionIndex));
            Assert.That(group.pinned.y, Is.EqualTo(-1));
            Assert.That(HotspotTileChoice.ChoiceOf(group), Is.EqualTo("\u2014"));
        }

        [Test]
        public void FindingATile_IgnoresCase()
        {
            Assert.That(HotspotTileChoice.NameMatches("Trim B2", "b2"), Is.True);
            Assert.That(HotspotTileChoice.NameMatches("Trim B2", "  "), Is.True, "nothing typed");
            Assert.That(HotspotTileChoice.NameMatches("Trim B2", "c"), Is.False);
            Assert.That(HotspotTileChoice.NameMatches(null, "c"), Is.False);
        }

        // The picker follows the surfaces it's given: another selection, other materials; it shows the asked one first
        [Test]
        public void ThePicker_FollowsTheSurfacesItIsGiven()
        {
            var (trims, facades, _) = Boxes();
            var given = new List<SurfaceReference>(trims);
            var picker = new HotspotTilePicker(() => given, facade);
            Assert.That(picker.Groups.Count, Is.EqualTo(1));
            Assert.That(picker.Group.material, Is.EqualTo(trim), "the asked material isn't there: the most used");

            given.AddRange(facades);
            given.AddRange(facades);
            picker.Refresh();
            Assert.That(picker.Groups.Count, Is.EqualTo(2));
            Assert.That(picker.Group.material, Is.EqualTo(trim), "still the one shown");
            picker.ShowMaterial(facade);
            Assert.That(picker.Group.material, Is.EqualTo(facade));
        }

        // A click pins on the shown material's surfaces only, a tile that fits none of them is never pinned, and the
        // picker shows the pin right away
        [Test]
        public void ThePicker_PinsOnTheShownMaterialsSurfaces()
        {
            var (trims, facades, _) = Boxes();
            var given = new List<SurfaceReference>(trims);
            given.AddRange(facades);
            var picker = new HotspotTilePicker(() => given, trim);
            Assume.That(picker.Group.material, Is.EqualTo(trim));

            Assert.That(picker.Pin(new Vector2Int(0, 3)), Is.False, "inside corner pieces");
            Assert.That(picker.Pin(new Vector2Int(0, 2)), Is.True);
            Assert.That(picker.Group.pinned, Is.EqualTo(new Vector2Int(0, 2)));
            foreach (var surface in trims)
                Assert.That(surface.SurfaceDetails.hotspot.pinnedTile, Is.EqualTo(2));
            foreach (var surface in facades)
                Assert.That(surface.SurfaceDetails.hotspot.pinnedTile, Is.Zero);
            Assert.That(picker.Pin(Vector2Int.zero), Is.True, "Automatic");
            Assert.That(picker.Group.pinned, Is.EqualTo(Vector2Int.zero));
        }

        // The pages: the material's own texture first (Base), its variants (a preview), then its other sheets
        [Test]
        public void ThePages_AreBaseVariantsThenOtherSheets()
        {
            var texture = new Texture2D(4, 4); created.Add(texture);
            var rusted  = new Texture2D(4, 4); created.Add(rusted);
            trim.mainTexture = texture;
            var variant = new Material(trim) { name = "Rusted", mainTexture = rusted }; created.Add(variant);
            var metadata = trim.GetMetadataOfType<ChiselSurfaceMetadata>();
            metadata.hotspotVariants = new List<Material> { variant };
            metadata.hotspotSheets   = new List<Material> { facade };

            var pages = HotspotTilePicker.PagesOf(trim, HotspotTransformGUI.LayoutOf(trim));
            Assert.That(pages.ConvertAll(page => page.name), Is.EqualTo(new[] { "Base", "Rusted", facade.name }));
            Assert.That(pages.ConvertAll(page => page.sheet), Is.EqualTo(new[] { 0, 0, 1 }));
            Assert.That(pages[1].kind, Does.StartWith("Variant"));
            Assert.That(pages[2].kind, Is.EqualTo("Other sheet"));
            Assert.That(pages[2].layout, Is.EqualTo(HotspotTransformGUI.LayoutOf(facade)));
        }
    }

    // The sheet view's zoom and pan, the arrow keys, and the popups' sizes
    [TestFixture]
    public class HotspotSheetViewTests
    {
        [Test]
        public void Fitting_ShowsTheWholeSheetInTheMiddle()
        {
            var view = new HotspotSheetView();
            view.Layout(new Rect(0, 0, 216, 116), new Vector2Int(200, 100));
            Assert.That(view.scale, Is.EqualTo(1).Within(1e-5f));
            Assert.That(view.ToView(new Vector2(100, 50)), Is.EqualTo(new Vector2(108, 58)));
        }

        [Test]
        public void TheWheel_ZoomsAroundTheMouse_AndStopsFitting()
        {
            var view = new HotspotSheetView();
            view.Layout(new Rect(0, 0, 216, 116), new Vector2Int(200, 100));
            var mouse = new Vector2(40, 30);
            var under = view.ToPixel(mouse);
            view.ZoomAt(mouse, 2);
            Assert.That(view.scale, Is.EqualTo(2).Within(1e-5f));
            Assert.That(Vector2.Distance(view.ToView(under), mouse), Is.LessThan(1e-3f), "the texel under the mouse stays under it");
            Assert.That(view.fit, Is.False);
            view.Layout(new Rect(0, 0, 400, 400), new Vector2Int(200, 100));
            Assert.That(view.scale, Is.EqualTo(2).Within(1e-5f), "a zoomed view isn't fitted again");
            view.Fit();
            Assert.That(view.fit, Is.True);
            view.OneToOne();
            Assert.That(view.IsOneToOne, Is.True);
        }

        [Test]
        public void TheArrowKeys_GoToTheNearestTileThatWay()
        {
            var layout = ScriptableObject.CreateInstance<ChiselHotspotLayout>();
            try
            {
                static HotspotTile Tile(int id, int x, int y) => new() { id = id, name = "T" + id,
                    points = new[] { new Vector2Int(x, y), new Vector2Int(x + 10, y), new Vector2Int(x + 10, y + 10), new Vector2Int(x, y + 10) } };
                layout.tiles = new List<HotspotTile> { Tile(1, 0, 0), Tile(2, 10, 0), Tile(3, 30, 0), Tile(4, 0, 10) };
                Assert.That(HotspotSheetView.NextTile(layout, 1, Vector2.right), Is.EqualTo(2));
                Assert.That(HotspotSheetView.NextTile(layout, 1, Vector2.up), Is.EqualTo(4), "down the sheet");
                Assert.That(HotspotSheetView.NextTile(layout, 1, Vector2.left), Is.EqualTo(1), "nothing that way");
                Assert.That(HotspotSheetView.NextTile(layout, 1, Vector2.right, tile => tile.id == 2), Is.EqualTo(3), "past a skipped one");
            }
            finally
            {
                Object.DestroyImmediate(layout);
            }
        }

        [Test]
        public void APopupsSize_StaysWithinItsLimits_AndIsRemembered()
        {
            Assert.That(ResizablePopup.ClampSize(new Vector2(100, 2000), new Vector2(420, 420), new Vector2(1000, 800)), Is.EqualTo(new Vector2(420, 800)));
            const string kKey = "Chisel.Tests.ResizablePopup";
            try
            {
                Assert.That(ResizablePopup.ReadSize(kKey, new Vector2(600, 500)), Is.EqualTo(new Vector2(600, 500)), "none remembered");
                ResizablePopup.WriteSize(kKey, new Vector2(700, 640));
                Assert.That(ResizablePopup.ReadSize(kKey, new Vector2(600, 500)), Is.EqualTo(new Vector2(700, 640)));
            }
            finally
            {
                EditorPrefs.DeleteKey(kKey + ".width");
                EditorPrefs.DeleteKey(kKey + ".height");
            }
            Assert.That(HotspotTilePicker.kMinSize, Is.EqualTo(new Vector2(420, 420)));
            Assert.That(DecalImagePicker.kMinSize, Is.EqualTo(new Vector2(320, 320)));
        }
    }

    // Choosing the image decals show
    [TestFixture]
    public class DecalImagePickerTests
    {
        readonly List<GameObject> objects = new();

        [TearDown]
        public void TearDown()
        {
            foreach (var go in objects)
                if (go) Object.DestroyImmediate(go);
            objects.Clear();
        }

        ChiselDecalComponent Decal()
        {
            var go = new GameObject("decal image test");
            objects.Add(go);
            return go.AddComponent<ChiselDecalComponent>();
        }

        // Every decal takes the image, in one undo step; the picker says when they differ
        [Test]
        public void Choosing_SetsEveryDecal_InOneUndo()
        {
            var decals = new[] { Decal(), Decal() };
            decals[1].ImageTile = 4;
            var picker = new DecalImagePicker(decals, null, null);
            Assert.That(picker.Current, Is.EqualTo(-1), "different images");

            Undo.IncrementCurrentGroup();
            Assert.That(DecalImagePicker.Choose(decals, 7), Is.True);
            Assert.That(decals[0].ImageTile, Is.EqualTo(7));
            Assert.That(decals[1].ImageTile, Is.EqualTo(7));
            Assert.That(picker.Current, Is.EqualTo(7));
            Assert.That(DecalImagePicker.Choose(decals, 7), Is.False, "they show it already");

            Undo.PerformUndo();
            Assert.That(decals[0].ImageTile, Is.Zero);
            Assert.That(decals[1].ImageTile, Is.EqualTo(4));
        }
    }
}
