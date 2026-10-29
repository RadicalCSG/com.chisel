using System.Collections.Generic;
using System.Text;
using Chisel.Components;
using Chisel.Core;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;

namespace Chisel.Editors.Tests
{
    // Every edit the hotspot layout window makes, undone and redone: the layout comes back exactly as it was, and the
    // models using it are mapped with it again in the next update (the window doesn't have to be open for that)
    [TestFixture]
    public class ChiselHotspotLayoutUndoTests
    {
        const string kFolder = "Assets/__ChiselHotspotLayoutUndoTests";

        ChiselHotspotLayout  layout;
        Material             material;
        GameObject           root;
        ChiselModelComponent model;

        [SetUp]
        public void SetUp()
        {
            if (Object.FindObjectsByType<ChiselModelComponent>(FindObjectsInactive.Include).Length > 0)
                Assert.Ignore("A Chisel model is loaded; run these in a scene without one");

            layout = ScriptableObject.CreateInstance<ChiselHotspotLayout>();
            layout.size = new Vector2Int(1024, 1024);
            layout.pixelsPerUnit = 256;
            layout.ResetCuts();
            Assert.That(layout.Split(layout.tiles[0].id, HotspotCutKind.Vertical, 512), Is.True);
            Assert.That(layout.Split(TileAt(256, 256), HotspotCutKind.Horizontal, 512), Is.True);

            if (!AssetDatabase.IsValidFolder(kFolder))
                AssetDatabase.CreateFolder("Assets", kFolder.Substring("Assets/".Length));
            material = new Material(ChiselProjectSettings.DefaultWallMaterial);
            AssetDatabase.CreateAsset(material, kFolder + "/hotspot.mat");
            material.AddMetadataOfType<ChiselSurfaceMetadata>().hotspotLayout = layout;

            root  = new GameObject("hotspot undo test");
            model = root.AddComponent<ChiselModelComponent>();
            var go = new GameObject("box");
            go.transform.SetParent(root.transform, false);
            var box = go.AddComponent<ChiselBoxComponent>();
            box.Max = new float3(2, 2, 0.25f);
            box.Min = new float3(-2, 0, -0.25f);
            Update();
            foreach (var surface in box.SurfaceDefinition.surfaces)
                surface.SetMaterial(material);
            box.OnValidate();
            Update();
            Undo.ClearAll();
        }

        [TearDown]
        public void TearDown()
        {
            Undo.ClearAll();
            if (root)
                Object.DestroyImmediate(root);
            Update();
            AssetDatabase.DeleteAsset(kFolder);
            if (layout)
                Object.DestroyImmediate(layout);
        }

        static void Update()
        {
            for (int i = 0; i < 3; i++)
            {
                ChiselNodeHierarchyManager.Update();
                ChiselModelManager.Instance.UpdateModels();
            }
        }

        int TileAt(int x, int y) => layout.TileAt(new Vector2(x, y));

        int CutOf(HotspotCutKind kind)
        {
            for (int i = 0; i < layout.cuts.Count; i++)
            {
                if (layout.cuts[i].kind == kind)
                    return i;
            }
            Assert.Fail($"the layout has no {kind} line");
            return -1;
        }

        // Everything the layout keeps
        string Layout() => EditorJsonUtility.ToJson(layout);

        // What the model draws with the layout: every vertex's position and uv, in no particular order (a rebuild may
        // put the vertices in another)
        string UVs()
        {
            var vertices = new List<string>();
            var culture  = System.Globalization.CultureInfo.InvariantCulture;
            foreach (var renderable in model.generated.renderables)
            {
                if (renderable == null || renderable.sharedMesh == null)
                    continue;
                var mesh = renderable.sharedMesh;
                var positions = mesh.vertices; var uvs = mesh.uv;
                for (int i = 0; i < positions.Length && i < uvs.Length; i++)
                    vertices.Add(string.Format(culture, "({0:0.####} {1:0.####} {2:0.####}: {3:0.####} {4:0.####})",
                                               positions[i].x, positions[i].y, positions[i].z, uvs[i].x, uvs[i].y));
            }
            vertices.Sort(System.StringComparer.Ordinal);
            return string.Join(" ", vertices);
        }

        // An edit as an event of its own, then undone and redone: each time the layout and the model as they should be
        void UndoAndRedo(string what, System.Action edit, bool movesTheModel = false)
        {
            var layoutBefore = Layout();
            var uvsBefore    = UVs();
            Undo.IncrementCurrentGroup();
            edit();
            Undo.IncrementCurrentGroup();
            Update();
            var layoutAfter = Layout();
            var uvsAfter    = UVs();
            Assert.That(layoutAfter, Is.Not.EqualTo(layoutBefore), $"{what}: the edit changed nothing");
            if (movesTheModel)
                Assert.That(uvsAfter, Is.Not.EqualTo(uvsBefore), $"{what}: the model wasn't mapped again");

            Undo.PerformUndo();
            Update();
            Assert.That(Layout(), Is.EqualTo(layoutBefore), $"{what}: undone, the layout isn't as it was");
            var uvsUndone = UVs();
            Assert.That(uvsUndone, Is.EqualTo(uvsBefore), $"{what}: undone, the model isn't mapped as it was" +
                                                          (uvsUndone == uvsAfter ? " (it still has the edit's)" : uvsAfter == uvsBefore ? " (the edit didn't change it)" : ""));

            Undo.PerformRedo();
            Update();
            Assert.That(Layout(), Is.EqualTo(layoutAfter), $"{what}: redone, the layout isn't as it was edited");
            Assert.That(UVs(), Is.EqualTo(uvsAfter), $"{what}: redone, the model isn't mapped as it was");
        }

        void Edit(string name, System.Action edit) => ChiselHotspotLayoutWindow.EditLayout(layout, name, edit);

        // A drag as the window makes it: every move an event of its own, one undo step for them all
        void Drag(string name, params System.Func<bool>[] moves)
        {
            var drag = ChiselHotspotLayoutWindow.LayoutDrag.Begin(layout);
            foreach (var move in moves)
            {
                Undo.IncrementCurrentGroup();
                drag.Move(name, move);
            }
            drag.End();
        }

        int AShape()
        {
            var id = -1;
            Edit("Draw Hotspot Shape", () => id = layout.AddShape(new[] { new Vector2Int(600, 100), new Vector2Int(900, 100), new Vector2Int(900, 400), new Vector2Int(600, 400) }));
            Update();
            Undo.ClearAll();
            Assert.That(layout.IsDrawn(id), Is.True);
            return id;
        }

        int AShapeWithAHole()
        {
            var id = AShape();
            Edit("Draw Hotspot Hole", () => layout.AddHole(id, new[] { new Vector2Int(700, 200), new Vector2Int(800, 200), new Vector2Int(750, 300) }));
            Update();
            Undo.ClearAll();
            return id;
        }

        // The control: told the layout changed when it didn't, the model is mapped the same
        [Test]
        public void ALayoutThatDidntChange_MapsTheModelTheSame()
        {
            var uvs = UVs();
            for (int i = 0; i < 3; i++)
            {
                layout.NotifyChanged();
                Update();
                Assert.That(UVs(), Is.EqualTo(uvs), $"mapped again ({i + 1})");
            }
        }

        [Test] public void SplittingVertically()      => UndoAndRedo("split", () => Edit("Split Hotspot Tile", () => layout.Split(TileAt(768, 512), HotspotCutKind.Vertical, 768)), movesTheModel: true);
        [Test] public void SplittingHorizontally()    => UndoAndRedo("split", () => Edit("Split Hotspot Tile", () => layout.Split(TileAt(768, 512), HotspotCutKind.Horizontal, 768)), movesTheModel: true);
        [Test] public void SplittingAlongADiagonal()  => UndoAndRedo("split", () => Edit("Split Hotspot Tile", () => layout.Split(TileAt(256, 768), HotspotCutKind.Slash, 0)));
        [Test] public void SplittingAlongTheOtherDiagonal() => UndoAndRedo("split", () => Edit("Split Hotspot Tile", () => layout.Split(TileAt(256, 768), HotspotCutKind.Backslash, 0)));
        [Test] public void SplittingIntoThreeTriangles() => UndoAndRedo("split", () => Edit("Split Hotspot Tile", () => layout.Split(TileAt(256, 768), HotspotCutKind.Apex, 256)));
        [Test] public void RemovingALine()            => UndoAndRedo("remove", () => Edit("Remove Hotspot Line", () => layout.RemoveCut(CutOf(HotspotCutKind.Horizontal))));

        [Test]
        public void MovingALine_InOneDrag_IsOneUndoStep()
        {
            var node = CutOf(HotspotCutKind.Vertical);
            UndoAndRedo("drag", () => Drag("Move Hotspot Line", () => layout.MoveCut(node, 576), () => layout.MoveCut(node, 640), () => layout.MoveCut(node, 704)), movesTheModel: true);
        }

        [Test] public void DrawingAShape() => UndoAndRedo("draw", () => Edit("Draw Hotspot Shape", () => layout.AddShape(new[] { new Vector2Int(600, 100), new Vector2Int(900, 100), new Vector2Int(750, 400) })));

        [Test]
        public void DrawingAHole()
        {
            var id = AShape();
            UndoAndRedo("hole", () => Edit("Draw Hotspot Hole", () => layout.AddHole(id, new[] { new Vector2Int(700, 200), new Vector2Int(800, 200), new Vector2Int(750, 300) })));
        }

        [Test]
        public void AddingACorner()
        {
            var id = AShape();
            UndoAndRedo("corner", () => Edit("Add Hotspot Shape Corner", () => layout.InsertShapePoint(id, -1, 0, new Vector2Int(750, 50))));
        }

        [Test]
        public void MovingACorner_InOneDrag_IsOneUndoStep()
        {
            var id = AShape();
            UndoAndRedo("drag", () => Drag("Move Hotspot Shape Corner", () => layout.MoveShapePoint(id, -1, 0, new Vector2Int(580, 90)),
                                                                        () => layout.MoveShapePoint(id, -1, 0, new Vector2Int(560, 80))));
        }

        [Test]
        public void MovingAShape_InOneDrag_IsOneUndoStep()
        {
            var id = AShape();
            UndoAndRedo("drag", () => Drag("Move Hotspot Shape", () => layout.MoveShape(id, new Vector2Int(8, 0)), () => layout.MoveShape(id, new Vector2Int(8, 8))));
        }

        [Test]
        public void RemovingACorner()
        {
            var id = AShape();
            UndoAndRedo("corner", () => Edit("Remove Hotspot Shape Corner", () => layout.RemoveShapePoint(id, -1, 0)));
        }

        [Test]
        public void RemovingAHole()
        {
            var id = AShapeWithAHole();
            UndoAndRedo("hole", () => Edit("Remove Hotspot Shape Hole", () => layout.RemoveShapeHole(id, 0)));
        }

        [Test]
        public void RemovingAShape()
        {
            var id = AShapeWithAHole();
            UndoAndRedo("shape", () => Edit("Remove Hotspot Shape", () => layout.RemoveShape(id)));
        }

        // A tile's settings, as the window's tile panel writes them
        void TileSetting(string what, System.Func<HotspotTile, HotspotTile> change, bool movesTheModel = false)
        {
            var index = layout.IndexOfTile(TileAt(768, 512));
            var tile  = change(layout.tiles[index]);
            UndoAndRedo(what, () => Edit("Change Hotspot Tile", () => layout.tiles[index] = tile), movesTheModel);
        }

        [Test] public void ATilesName()          => TileSetting("name", tile => { tile.name = "renamed"; return tile; });
        [Test] public void ATilesFlags()         => TileSetting("flags", tile => { tile.flags |= HotspotTileFlags.Disabled; return tile; }, movesTheModel: true);
        [Test] public void ATilesBorder()        => TileSetting("border", tile => { tile.border = 32; return tile; }, movesTheModel: true);
        [Test] public void ATilesPixelsPerUnit() => TileSetting("pixels per unit", tile => { tile.pixelsPerUnit = 128; return tile; });

        [Test]
        public void ATilesBorderSides()
        {
            TileSetting("sides", tile =>
            {
                tile.border = 32;
                tile.sides  = new HotspotBorderSide[tile.points.Length];
                tile.sides[0].mode = HotspotBorderMode.Stretch;
                tile.sides[0].maxStretch = 2;
                return tile;
            });
        }

        [Test]
        public void ATilesInsideCorners()
        {
            var corners = layout.IndexOfTile(TileAt(256, 768));
            var marked  = layout.tiles[corners];
            marked.flags |= HotspotTileFlags.InsideCorners;
            layout.tiles[corners] = marked;
            layout.NotifyChanged();
            Update();
            Undo.ClearAll();
            TileSetting("inside corners", tile => { tile.border = 16; tile.insideCorners = marked.id; return tile; });
        }

        [Test] public void TheLayoutsPixelsPerUnit()    => UndoAndRedo("pixels per unit", () => Edit("Change Hotspot Layout", () => layout.pixelsPerUnit = 128));
        [Test] public void TheLayoutsPadding()          => UndoAndRedo("padding", () => Edit("Change Hotspot Layout", () => layout.padding = 4), movesTheModel: true);
        [Test] public void TheLayoutsInteriorPadding()  => UndoAndRedo("interior padding", () => Edit("Change Hotspot Layout", () => layout.interiorPadding = 2));
        [Test] public void TheLayoutsBudgets()          => UndoAndRedo("budgets", () => Edit("Change Hotspot Layout", () => { layout.maxRepeats = 2; layout.maxTriangles = 64; }));
        [Test] public void ResizingTheLayout()          => UndoAndRedo("resize", () => Edit("Change Hotspot Layout", () => layout.Resize(new Vector2Int(2048, 2048))));
        [Test] public void ResettingTheLayout()         => UndoAndRedo("reset", () => Edit("Reset Hotspot Layout", layout.ResetCuts), movesTheModel: true);

        [Test]
        public void TheLayoutsTexture()
        {
            var texture = new Texture2D(512, 512) { hideFlags = HideFlags.DontSave };
            try { UndoAndRedo("texture", () => Edit("Change Hotspot Layout", () => { layout.texture = texture; layout.Resize(new Vector2Int(512, 512)); })); }
            finally { Undo.ClearAll(); Object.DestroyImmediate(texture); }
        }

        [Test]
        public void ImportingJson()
        {
            const string json = "{\"format\":\"" + ChiselHotspotLayout.kJsonFormat + "\",\"width\":1024,\"height\":1024,\"tiles\":[" +
                                "{\"name\":\"left\",\"points\":[0,0,512,0,512,1024,0,1024]},{\"name\":\"right\",\"points\":[512,0,1024,0,1024,1024,512,1024]}]}";
            string error = null;
            UndoAndRedo("json", () => Edit("Import Hotspot Layout", () => ChiselHotspotLayout.TryReadJson(json, layout, out error)));
            Assert.That(error, Is.Null);
        }

        [Test]
        public void ImportingSvg()
        {
            const string svg = "<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 1024 1024\">" +
                               "<rect id=\"top\" x=\"0\" y=\"0\" width=\"1024\" height=\"512\"/><rect id=\"bottom\" x=\"0\" y=\"512\" width=\"1024\" height=\"512\"/></svg>";
            string error = null;
            UndoAndRedo("svg", () => Edit("Import Hotspot Layout", () => ChiselHotspotLayout.TryReadSvg(svg, layout, out error)));
            Assert.That(error, Is.Null);
        }

        // Several edits undone one at a time, each back to the layout before it
        [Test]
        public void SeveralEdits_AreUndoneOneAtATime()
        {
            var states = new List<string> { Layout() };
            Undo.IncrementCurrentGroup();
            Edit("Split Hotspot Tile", () => layout.Split(TileAt(768, 512), HotspotCutKind.Horizontal, 256));
            Undo.IncrementCurrentGroup();
            states.Add(Layout());
            Edit("Change Hotspot Layout", () => layout.padding = 4);
            Undo.IncrementCurrentGroup();
            states.Add(Layout());
            var node = CutOf(HotspotCutKind.Vertical);
            Drag("Move Hotspot Line", () => layout.MoveCut(node, 576), () => layout.MoveCut(node, 640));
            Undo.IncrementCurrentGroup();
            for (int i = states.Count - 1; i >= 0; i--)
            {
                Undo.PerformUndo();
                Assert.That(Layout(), Is.EqualTo(states[i]), $"undo {states.Count - i}");
            }
        }
    }
}
