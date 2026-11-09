using System.Collections.Generic;
using Chisel.Core;
using UnityEditor;
using UnityEngine;

namespace Chisel.Editors
{
    // The layout window's tile list (left) and settings (right): the layout's own, the selected tile's grouped by
    // purpose, and the tile tried on a surface
    public sealed partial class ChiselHotspotLayoutWindow
    {
        const float kRowHeight = 20;

        [SerializeField] string tileFilter = string.Empty;
        [SerializeField] bool   layoutFoldout;
        Vector2 listScroll;
        // the list scrolls to the selected tile once it changed; it takes the arrow keys after a click in it
        bool revealSelected;
        bool listFocused;

        // The tiles the list shows: those whose name or tags match the filter, in the layout's order
        internal List<int> ListedTiles()
        {
            var listed = new List<int>();
            if (layout == null)
                return listed;
            foreach (var tile in layout.tiles)
            {
                if (HotspotTileKinds.Matches(tile, tileFilter))
                    listed.Add(tile.id);
            }
            return listed;
        }

        internal string TileFilter { get => tileFilter; set => tileFilter = value ?? string.Empty; }

        void TileList(Rect area)
        {
            if (area.width <= 0)
                return;
            var current = Event.current;
            if (current.type == EventType.Repaint)
                EditorGUI.DrawRect(area, new Color(0, 0, 0, 0.12f));
            var header = new Rect(area.x + 6, area.y + 4, area.width - 12, kRowHeight);
            GUI.Label(header, "TILES", EditorStyles.miniBoldLabel);
            var count = new GUIContent($"{layout.tiles.Count} tiles");
            var countWidth = EditorStyles.miniLabel.CalcSize(count).x;
            GUI.Label(new Rect(header.xMax - countWidth, header.y, countWidth, header.height), count, EditorStyles.miniLabel);
            var filterRect = new Rect(area.x + 6, header.yMax + 2, area.width - 12, EditorGUIUtility.singleLineHeight);
            tileFilter = EditorGUI.TextField(filterRect, tileFilter, EditorStyles.toolbarSearchField);
            if (string.IsNullOrEmpty(tileFilter) && current.type == EventType.Repaint)
                GUI.Label(new Rect(filterRect.x + 16, filterRect.y, filterRect.width - 16, filterRect.height), "Filter by name or flag", EditorStyles.centeredGreyMiniLabel);

            var listed = ListedTiles();
            var view = new Rect(area.x, filterRect.yMax + 4, area.width, area.yMax - filterRect.yMax - 4);
            var content = new Rect(0, 0, view.width - 14, listed.Count * kRowHeight);
            if (revealSelected && current.type == EventType.Layout)
            {
                var at = listed.IndexOf(selectedTile);
                if (at >= 0)
                {
                    var top = at * kRowHeight;
                    if (top < listScroll.y) listScroll.y = top;
                    else if (top + kRowHeight > listScroll.y + view.height) listScroll.y = top + kRowHeight - view.height;
                }
                revealSelected = false;
            }
            listScroll = GUI.BeginScrollView(view, listScroll, content, false, false);
            for (int i = 0; i < listed.Count; i++)
            {
                var row = new Rect(0, i * kRowHeight, content.width, kRowHeight);
                if (row.yMax < listScroll.y || row.y > listScroll.y + view.height)
                    continue;
                var index = layout.IndexOfTile(listed[i]);
                var tile = layout.tiles[index];
                if (current.type == EventType.Repaint)
                {
                    if (tile.id == selectedTile)
                        EditorGUI.DrawRect(row, new Color(0.17f, 0.36f, 0.53f, 1));
                    EditorGUI.DrawRect(new Rect(row.x + 8, row.y + 5, 10, 10), HotspotTileKinds.ColorOf(HotspotTileKinds.KindOf(tile)));
                    GUI.Label(new Rect(row.x + 24, row.y, 56, row.height), string.IsNullOrEmpty(tile.name) ? "Tile " + tile.id : tile.name);
                    var size = HotspotTileKinds.SizeOf(tile);
                    GUI.Label(new Rect(row.x + 82, row.y, 70, row.height), $"{size.x}{HotspotTileChoice.kTimes}{size.y}", EditorStyles.miniLabel);
                    var tags = new GUIContent(string.Join($" {HotspotTileChoice.kDot} ", HotspotTileKinds.TagsOf(tile)));
                    var tagsWidth = Mathf.Min(EditorStyles.miniLabel.CalcSize(tags).x, row.width - 156);
                    GUI.Label(new Rect(row.xMax - tagsWidth - 6, row.y, tagsWidth, row.height), tags, EditorStyles.miniLabel);
                }
                if (current.type == EventType.MouseDown && current.button == 0 && row.Contains(current.mousePosition))
                {
                    SelectedTile = tile.id;
                    revealSelected = false;
                    listFocused = true;
                    GUI.FocusControl(null);
                    current.Use();
                }
            }
            GUI.EndScrollView();

            // the arrow keys go up and down the list after a click in it
            if (listFocused && current.type == EventType.KeyDown && GUIUtility.keyboardControl == 0 &&
                (current.keyCode == KeyCode.UpArrow || current.keyCode == KeyCode.DownArrow) && listed.Count > 0)
            {
                var at = listed.IndexOf(selectedTile);
                at = Mathf.Clamp(at < 0 ? 0 : at + (current.keyCode == KeyCode.DownArrow ? 1 : -1), 0, listed.Count - 1);
                SelectedTile = listed[at];
                current.Use();
            }
        }

        void Panel()
        {
            layoutFoldout = EditorGUILayout.Foldout(layoutFoldout, "LAYOUT", true, EditorStyles.foldoutHeader);
            if (layoutFoldout && LayoutFields(layout, full: true))
                Repaint();
            Separator();

            var index = layout.IndexOfTile(selectedTile);
            if (index >= 0 && TileFields(layout, index))
                Repaint();
        }

        static void Separator()
        {
            GUILayout.Space(4);
            var rect = GUILayoutUtility.GetRect(1, 1, GUILayout.ExpandWidth(true));
            if (Event.current.type == EventType.Repaint)
                EditorGUI.DrawRect(rect, new Color(0, 0, 0, 0.3f));
            GUILayout.Space(4);
        }

        static readonly GUIContent kPixelsPerUnitContents = new("Pixels per unit", "How many of the texture's pixels cover one unit of a surface (one unit in the scene): a 1024 pixel tile at 256 covers 4 units. Matching, the border's width and the repeats go by it; a tile can have its own");
        static readonly GUIContent kPaddingContents = new("Padding", "Pixels left out along every tile's edges, so a filtered (or mipmapped) texture never shows a neighbouring tile: a gutter. A tile's border keeps its inner edge and narrows by as much");
        static readonly GUIContent kInteriorPaddingContents = new("Interior padding", "Pixels a bordered tile's interior leaves out where it meets the border (0 for none), so filtering at its seams never shows the border");
        static readonly GUIContent kMaxRepeatsContents = new("Max repeats", "The most repeats a border strip, a repeating tile or a bordered tile's interior lays on one surface; more and it stretches instead. 0 takes the project's setting");
        static readonly GUIContent kMaxTrianglesContents = new("Max triangles", "The most triangles a surface's cut may draw; more and its interior's repeats are left out, then the cut. 0 takes the project's setting");

        // What a budget of 0 means: the project's setting, as it is now
        internal static string ProjectSettingText(int projectValue) => $"Project setting ({projectValue})";

        // The layout's own settings, as the window and the inspector show them; true when they changed. full: also the
        // padding and the budgets, beside the tiles they change
        internal static bool LayoutFields(ChiselHotspotLayout layout, bool full = false)
        {
            EditorGUI.BeginChangeCheck();
            var texture       = (Texture2D)EditorGUILayout.ObjectField("Texture", layout.texture, typeof(Texture2D), false);
            // the tiles' pixels: the texture's, or given when there is none
            Vector2Int size;
            if (layout.texture != null)
            {
                EditorGUILayout.LabelField("Size", $"{layout.size.x} {HotspotTileChoice.kTimes} {layout.size.y} px");
                size = layout.size;
            } else
                size = EditorGUILayout.Vector2IntField("Size in pixels", layout.size);
            var pixelsPerUnit = EditorGUILayout.FloatField(kPixelsPerUnitContents, layout.pixelsPerUnit);
            var padding       = full ? EditorGUILayout.FloatField(kPaddingContents, layout.padding) : layout.padding;
            var interior      = full ? EditorGUILayout.FloatField(kInteriorPaddingContents, layout.interiorPadding) : layout.interiorPadding;
            var maxRepeats    = full ? BudgetField(kMaxRepeatsContents, layout.maxRepeats, ChiselProjectSettings.HotspotMaxRepeats) : layout.maxRepeats;
            var maxTriangles  = full ? BudgetField(kMaxTrianglesContents, layout.maxTriangles, ChiselProjectSettings.HotspotMaxTriangles) : layout.maxTriangles;
            if (!EditorGUI.EndChangeCheck())
                return false;
            EditLayout(layout, "Change Hotspot Layout", () =>
            {
                if (texture != layout.texture && texture != null)
                    size = new Vector2Int(texture.width, texture.height);
                layout.texture = texture;
                layout.pixelsPerUnit = Mathf.Max(0.001f, pixelsPerUnit);
                layout.padding = Mathf.Max(0, padding);
                layout.interiorPadding = Mathf.Max(0, interior);
                layout.maxRepeats = Mathf.Max(0, maxRepeats);
                layout.maxTriangles = Mathf.Max(0, maxTriangles);
                layout.Resize(size);
            });
            return true;
        }

        // A budget: 0 shows what it takes, the project's setting
        static int BudgetField(GUIContent label, int value, int projectValue)
        {
            var rect = EditorGUILayout.GetControlRect();
            value = EditorGUI.IntField(rect, label, value);
            if (value == 0 && Event.current.type == EventType.Repaint)
            {
                var hint = new GUIContent(ProjectSettingText(projectValue));
                var width = EditorStyles.miniLabel.CalcSize(hint).x;
                GUI.Label(new Rect(rect.xMax - width - 4, rect.y, width, rect.height), hint, EditorStyles.centeredGreyMiniLabel);
            }
            return value;
        }

        // What kind of tile it is, as its header says
        internal static string ShapeOf(in HotspotTile tile)
        {
            if (tile.points == null)
                return string.Empty;
            if (IsShaped(tile))
                return $"Shape ({tile.points.Length} corners" + (tile.holes != null && tile.holes.Length > 0 ? $", {tile.holes.Length} holes)" : ")");
            return tile.points.Length == 3 ? "Triangle" : "Rectangle";
        }

        // How large the tile is on a surface, at its pixels per unit: "2 x 1 m" (times sign)
        internal static string OnASurface(Vector2Int pixels, float pixelsPerUnit)
        {
            if (!(pixelsPerUnit > 0))
                return string.Empty;
            var culture = System.Globalization.CultureInfo.InvariantCulture;
            return $"{(pixels.x / pixelsPerUnit).ToString("0.###", culture)} {HotspotTileChoice.kTimes} {(pixels.y / pixelsPerUnit).ToString("0.###", culture)} m";
        }

        static void Section(string title)
        {
            GUILayout.Space(6);
            GUILayout.Label(title, EditorStyles.miniBoldLabel);
        }

        static readonly GUIContent kDisabledContents  = new("Disabled", "Never chosen by matching; surfaces can still be pinned to it");
        static readonly GUIContent kAlternateContents = new("Alternate", "Only taken when a surface is pinned to it, like Valve's alt rectangles");
        static readonly GUIContent kRadialContents    = new("Radial", "Only for round surfaces, and round surfaces only take radial tiles");
        static readonly GUIContent kMayTurnContents   = new("May turn", "Matching may turn the texture to fit; otherwise it stays upright");
        static readonly GUIContent kMayMirrorContents = new("May mirror", "Matching may mirror the texture to fit");
        static readonly GUIContent kRepeatsAcrossContents = new("Repeats across", "Meant to repeat along its width");
        static readonly GUIContent kRepeatsDownContents   = new("Repeats down", "Meant to repeat along its height");
        static readonly GUIContent kRepeatsCutContents    = new("Repeats cut off", "Repeats keep their exact size and the last one is cut off, instead of all stretching to fit");
        static readonly GUIContent kBorderRepeatsContents = new("Border repeats", "The border's strips repeat along the edges instead of stretching");
        static readonly GUIContent kCornerPiecesContents  = new("Corner pieces", "Whether this tile is the pieces other bordered tiles use at inside corners (never matched itself): four pieces, one for each way an inside corner turns, their outer edges meeting in the middle; or one piece, drawn as the top left of the four (its outer edges along its bottom and right), turned for the others");

        // What a tile is for other tiles' inside corners, as its Corner pieces dropdown says (InsideCorners,
        // InsideCornerTurned; four pieces first when both are set)
        internal enum CornerPieces { None, [InspectorName("Four pieces")] Four, [InspectorName("One, turned")] OneTurned }

        internal static CornerPieces CornerPiecesOf(HotspotTileFlags flags)
        {
            if ((flags & HotspotTileFlags.InsideCorners) != 0)      return CornerPieces.Four;
            if ((flags & HotspotTileFlags.InsideCornerTurned) != 0) return CornerPieces.OneTurned;
            return CornerPieces.None;
        }

        internal static HotspotTileFlags WithCornerPieces(HotspotTileFlags flags, CornerPieces pieces)
        {
            if (CornerPiecesOf(flags) == pieces)
                return flags;
            flags &= ~(HotspotTileFlags.InsideCorners | HotspotTileFlags.InsideCornerTurned);
            switch (pieces)
            {
                case CornerPieces.Four:      return flags | HotspotTileFlags.InsideCorners;
                case CornerPieces.OneTurned: return flags | HotspotTileFlags.InsideCornerTurned;
                default:                     return flags;
            }
        }
        static readonly GUIContent kBorderWidthContents   = new("Width", "The width of the tile's border in pixels, a band along its outline (0 for none)");
        static readonly GUIContent kTilePixelsPerUnitContents = new("Pixels per unit", "The texture density this tile is meant for; 0 takes the layout's");

        // A tile's settings, grouped by purpose; true when they changed
        internal static bool TileFields(ChiselHotspotLayout layout, int index)
        {
            var tile = layout.tiles[index];
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Label(string.IsNullOrEmpty(tile.name) ? "Tile " + tile.id : tile.name, EditorStyles.largeLabel);
                GUILayout.FlexibleSpace();
                GUILayout.Label(new GUIContent(ShapeOf(tile), $"Id {tile.id}"), EditorStyles.miniLabel);
            }
            var pixels = HotspotTileKinds.SizeOf(tile);
            var layoutsDensity = tile.pixelsPerUnit <= 0;
            var density = layoutsDensity ? layout.pixelsPerUnit : tile.pixelsPerUnit;

            EditorGUI.BeginChangeCheck();
            tile.name = EditorGUILayout.TextField("Name", tile.name);
            var aspect = HotspotTileChoice.AspectText(pixels.x, pixels.y).Replace(" ", string.Empty);
            EditorGUILayout.LabelField("Pixels", $"{pixels.x} {HotspotTileChoice.kTimes} {pixels.y}" + (aspect.Length > 0 ? $" ({aspect})" : string.Empty));
            EditorGUILayout.LabelField("On a surface", OnASurface(pixels, density));
            var densityRect = EditorGUILayout.GetControlRect();
            tile.pixelsPerUnit = Mathf.Max(0, EditorGUI.FloatField(densityRect, kTilePixelsPerUnitContents, tile.pixelsPerUnit));
            if (layoutsDensity && Event.current.type == EventType.Repaint)
            {
                var hint = new GUIContent($"Layout's ({layout.pixelsPerUnit:0.###})");
                var width = EditorStyles.miniLabel.CalcSize(hint).x;
                GUI.Label(new Rect(densityRect.xMax - width - 4, densityRect.y, width, densityRect.height), hint, EditorStyles.centeredGreyMiniLabel);
            }

            Section("MATCHING");
            tile.flags = FlagRow(tile.flags, (HotspotTileFlags.Disabled, kDisabledContents), (HotspotTileFlags.Alternate, kAlternateContents));
            tile.flags = FlagRow(tile.flags, (HotspotTileFlags.Radial, kRadialContents), (HotspotTileFlags.AllowRotation, kMayTurnContents));
            tile.flags = FlagRow(tile.flags, (HotspotTileFlags.AllowMirror, kMayMirrorContents), (HotspotTileFlags.None, null));

            Section("REPEATING");
            tile.flags = FlagRow(tile.flags, (HotspotTileFlags.RepeatU, kRepeatsAcrossContents), (HotspotTileFlags.RepeatV, kRepeatsDownContents));
            tile.flags = FlagRow(tile.flags, (HotspotTileFlags.PartialRepeats, kRepeatsCutContents), (HotspotTileFlags.RepeatBorder, kBorderRepeatsContents));

            Section("BORDER");
            tile.border = Mathf.Max(0, EditorGUILayout.IntField(kBorderWidthContents, tile.border));
            if (tile.border > 0 && tile.points != null && tile.points.Length >= 3 && !IsShaped(tile))
                SideFields(ref tile);
            if (tile.border > 0)
                tile.insideCorners = InsideCornersField(layout, tile);
            var pieces = (CornerPieces)EditorGUILayout.EnumPopup(kCornerPiecesContents, CornerPiecesOf(tile.flags));
            tile.flags = WithCornerPieces(tile.flags, pieces);
            if (!EditorGUI.EndChangeCheck())
                return false;
            EditLayout(layout, "Change Hotspot Tile", () => layout.tiles[index] = tile);
            return true;
        }

        // Two flags side by side; a flag of None leaves its half empty
        static HotspotTileFlags FlagRow(HotspotTileFlags flags, (HotspotTileFlags flag, GUIContent content) left, (HotspotTileFlags flag, GUIContent content) right)
        {
            var rect = EditorGUILayout.GetControlRect();
            var half = new Rect(rect.x, rect.y, rect.width * 0.5f - 2, rect.height);
            flags = Flag(half, flags, left.flag, left.content);
            half.x += rect.width * 0.5f + 2;
            flags = Flag(half, flags, right.flag, right.content);
            return flags;
        }

        static HotspotTileFlags Flag(Rect rect, HotspotTileFlags flags, HotspotTileFlags flag, GUIContent content)
        {
            if (flag == HotspotTileFlags.None || content == null)
                return flags;
            var on = EditorGUI.ToggleLeft(rect, content, (flags & flag) != 0);
            return on ? flags | flag : flags & ~flag;
        }

        static readonly GUIContent kSideModeContents    = new("Along the edge", "How its border strip takes a surface's edge: as the tile's flags say, stretched, repeated (stretched to fit), or repeated at its exact size with the last one cut off");
        static readonly GUIContent kSideStretchContents = new("Max stretch", "Stretching: the most the strip may stretch (2: twice its length in the tile) before it repeats instead, the fewest times that keep within it; 0 for no limit");

        // The border along each side of a tile as a table, each side named by where it faces in the texture
        static void SideFields(ref HotspotTile tile)
        {
            var count = tile.points.Length;
            var sides = tile.sides != null && tile.sides.Length == count ? (HotspotBorderSide[])tile.sides.Clone() : new HotspotBorderSide[count];
            var centre = Vector2.zero;
            foreach (var point in tile.points)
                centre += point;
            centre /= count;

            var header = EditorGUILayout.GetControlRect();
            Columns(header, out var nameColumn, out var modeColumn, out var stretchColumn);
            GUI.Label(nameColumn, "Side", EditorStyles.miniLabel);
            GUI.Label(modeColumn, kSideModeContents, EditorStyles.miniLabel);
            GUI.Label(stretchColumn, kSideStretchContents, EditorStyles.miniLabel);
            var changed = false;
            for (int i = 0; i < count; i++)
            {
                // the texture's y goes down
                var middle = ((Vector2)tile.points[i] + tile.points[(i + 1) % count]) * 0.5f - centre;
                var name = Mathf.Abs(middle.x) > Mathf.Abs(middle.y) ? (middle.x > 0 ? "Right" : "Left") : (middle.y > 0 ? "Bottom" : "Top");
                var row = EditorGUILayout.GetControlRect();
                Columns(row, out nameColumn, out modeColumn, out stretchColumn);
                GUI.Label(nameColumn, name);
                EditorGUI.BeginChangeCheck();
                var mode = (HotspotBorderMode)EditorGUI.EnumPopup(modeColumn, new GUIContent(string.Empty, kSideModeContents.tooltip), sides[i].mode);
                float maxStretch;
                using (new EditorGUI.DisabledScope(mode != HotspotBorderMode.Stretch && mode != HotspotBorderMode.Tile))
                    maxStretch = Mathf.Max(0, EditorGUI.FloatField(stretchColumn, sides[i].maxStretch));
                if (EditorGUI.EndChangeCheck())
                {
                    sides[i].mode = mode;
                    sides[i].maxStretch = maxStretch;
                    changed = true;
                }
            }
            if (changed)
                tile.sides = sides;
        }

        static void Columns(Rect row, out Rect name, out Rect mode, out Rect stretch)
        {
            const float kName = 60, kStretch = 70;
            name    = new Rect(row.x, row.y, kName, row.height);
            stretch = new Rect(row.xMax - kStretch, row.y, kStretch, row.height);
            mode    = new Rect(name.xMax + 4, row.y, stretch.x - 4 - (name.xMax + 4), row.height);
        }

        static readonly GUIContent kInsideCornersContents = new("Inside corners", "The tile with the pieces this tile's border takes at inside corners (a tile marked Inside corners or Inside corner (turned)); none: the strips meet on a line through the corner");

        // The tile with this tile's inside corner pieces: none, or one of the tiles marked for them
        static int InsideCornersField(ChiselHotspotLayout layout, in HotspotTile tile)
        {
            var ids = new List<int> { 0 };
            var names = new List<string> { "None" };
            foreach (var other in layout.tiles)
            {
                if (other.id == tile.id || (other.flags & (HotspotTileFlags.InsideCorners | HotspotTileFlags.InsideCornerTurned)) == 0)
                    continue;
                ids.Add(other.id);
                names.Add(string.IsNullOrEmpty(other.name) ? $"Tile {other.id}" : other.name);
            }
            var index = ids.IndexOf(tile.insideCorners);
            if (index < 0)
            {
                ids.Add(tile.insideCorners);
                names.Add($"Missing ({tile.insideCorners})");
                index = ids.Count - 1;
            }
            var contents = new GUIContent[names.Count];
            for (int i = 0; i < names.Count; i++)
                contents[i] = new GUIContent(names[i]);
            return ids[EditorGUILayout.Popup(kInsideCornersContents, index, contents)];
        }
    }
}
