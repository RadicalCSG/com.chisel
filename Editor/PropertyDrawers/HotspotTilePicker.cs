using System;
using System.Collections.Generic;
using Chisel.Components;
using Chisel.Core;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace Chisel.Editors
{
    // Choosing the tile surfaces keep: for each hotspot material on them ("Sheet of") its sheet as it is, how each tile
    // fits the surfaces using it, and a click pins a tile on those surfaces. Follows the surfaces it is given live (the
    // surface selection), and stays open to choose for each material
    sealed class HotspotTilePicker : ResizablePopup
    {
        // What a page shows: a layout over a texture, and the sheet its tiles are on (0: the material's own, also on its
        // variants' textures; otherwise its place in the material's other sheets, HotspotSurface.pinnedSheet)
        public struct Page
        {
            public ChiselHotspotLayout layout;
            public Texture             texture;
            public int                 sheet;
            public string              name;
            public string              kind;
            public Material            material;
        }

        internal const string kSizeKey = "Chisel.HotspotTilePicker.Size";
        internal static readonly Vector2 kMinSize = new(420, 420);
        internal const float kDefaultWidth = 600;

        const float kPad      = 6;
        const float kRow      = 28;
        const float kPagesRow = 46;
        const float kLegend   = 22;
        const float kInfo     = 26;

        static readonly Color kSome     = new(0, 0, 0, 0.38f);
        static readonly Color kNone     = new(0, 0, 0, 0.72f);
        static readonly Color kDim      = new(0, 0, 0, 0.6f);
        static readonly Color kHidden   = new(0.16f, 0.16f, 0.16f, 1);
        static readonly Color kRowColor = new(0, 0, 0, 0.12f);
        static readonly Color kWarm     = new(1f, 0.65f, 0.3f);

        static readonly GUIContent kAutomaticContent   = new("Automatic", "Let matching choose the tile for these surfaces");
        static readonly GUIContent kFittingOnlyContent = new("Fitting only", "Hide the tiles that fit none of these surfaces");
        static readonly GUIContent kFitContent         = new("Fit", "Show the whole sheet");
        static readonly GUIContent kOneToOneContent    = new("1:1", "One pixel of the screen for each pixel of the texture");
        static readonly GUIContent kEditLayoutContent  = new("Edit layout...", "Open the layout editor for this sheet");

        readonly Func<IEnumerable<SurfaceReference>> source;
        readonly Dictionary<(SurfaceReference, Material), HashSet<Vector2Int>> fitsCache = new();
        readonly HotspotSheetView view = new();
        List<HotspotTileGroup> groups = new();
        Material shownMaterial;
        List<Page> pages = new();
        int shown;
        string find = string.Empty;
        bool fittingOnly;
        int hover;

        // surfaces: the ones to choose for, asked again as the picker is drawn; material: the one whose sheet is shown
        // first, the most used when not given
        public HotspotTilePicker(Func<IEnumerable<SurfaceReference>> surfaces, Material material = null)
            : base(kSizeKey, kMinSize, new Vector2(kDefaultWidth, kDefaultWidth))
        {
            source = surfaces;
            shownMaterial = material;
            Refresh();
            var layout = Group?.layout;
            SetSizeIfNotRemembered(new Vector2(kDefaultWidth, HeightFor(layout != null ? layout.size : Vector2Int.zero, kDefaultWidth - 2 * kPad, Chrome)));
        }

        public static void Show(Rect activator, Func<IEnumerable<SurfaceReference>> surfaces, Material material = null)
        {
            PopupWindow.Show(activator, new HotspotTilePicker(surfaces, material));
        }

        float Chrome => kRow * 2 + (pages.Count > 1 ? kPagesRow : 0) + kLegend + kInfo + 2 * kPad;

        public IReadOnlyList<HotspotTileGroup> Groups => groups;
        public HotspotTileGroup Group => groups.Find(group => group.material == shownMaterial);
        public IReadOnlyList<Page> Pages => pages;
        public Page Shown => pages.Count > 0 ? pages[Mathf.Clamp(shown, 0, pages.Count - 1)] : default;

        // The groups read again from the surfaces: what they keep now, how tiles fit them (kept per surface once known)
        public void Refresh()
        {
            var surfaces = new List<SurfaceReference>();
            var given = source?.Invoke();
            if (given != null)
                surfaces.AddRange(given);
            groups = HotspotTileChoice.GroupsOf(surfaces, FitsOf);
            if (Group == null)
                ShowMaterial(groups.Count > 0 ? groups[0].material : null);
        }

        HashSet<Vector2Int> FitsOf(SurfaceReference surface, HotspotTileGroup group)
        {
            var key = (surface, group.material);
            if (fitsCache.TryGetValue(key, out var fits))
                return fits;
            fits = ChiselHotspotGroupEdit.FitsOf(surface, group.layout, group.material);
            // not known yet (not mapped): asked again later
            if (fits != null)
                fitsCache[key] = fits;
            return fits;
        }

        public void ShowMaterial(Material material)
        {
            var sheet = Shown.sheet;
            shownMaterial = material;
            var group = Group;
            pages = group != null ? PagesOf(group.material, group.layout) : new List<Page>();
            shown = 0;
            // the pinned tile's sheet
            var pinnedSheet = group != null && group.pinned.y > 0 ? group.pinned.x : sheet;
            for (int i = 0; i < pages.Count; i++)
            {
                if (pages[i].sheet == pinnedSheet) { shown = i; break; }
            }
            hover = 0;
            view.fit = true;
        }

        public void ShowPage(int index)
        {
            if (index < 0 || index >= pages.Count || index == shown)
                return;
            var sameSheet = pages[index].layout == pages[shown].layout;
            shown = index;
            if (!sameSheet)
            {
                hover = 0;
                view.fit = true;
            }
        }

        // What the picker shows of a material: a page for each texture of its layout (its own and its variants'), and
        // one for each other sheet drawn with it (ChiselSurfaceMetadata.hotspotSheets, by place)
        internal static List<Page> PagesOf(Material material, ChiselHotspotLayout layout)
        {
            var pages = new List<Page>();
            if (layout == null)
                return pages;
            void Add(Material m, bool variant)
            {
                var texture = m != null && m.mainTexture != null ? m.mainTexture : null;
                if (texture != null && !pages.Exists(page => page.sheet == 0 && page.texture == texture))
                {
                    pages.Add(new Page { layout = layout, texture = texture, material = material,
                                         name = variant ? m.name : "Base", kind = variant ? $"Variant {HotspotTileChoice.kDot} preview" : "Layout" });
                }
            }
            Add(material, false);
            var metadata = material != null ? material.GetMetadataOfType<ChiselSurfaceMetadata>() : null;
            if (pages.Count == 0)
                pages.Add(new Page { layout = layout, texture = layout.texture, material = material, name = "Base", kind = "Layout" });
            if (metadata != null && metadata.hotspotVariants != null)
            {
                foreach (var variant in metadata.hotspotVariants)
                    Add(variant, true);
            }
            for (int i = 0; metadata != null && metadata.hotspotSheets != null && i < metadata.hotspotSheets.Count; i++)
            {
                var sheet = metadata.hotspotSheets[i];
                var sheetLayout = sheet != null ? sheet.GetMetadataOfType<ChiselSurfaceMetadata>()?.hotspotLayout : null;
                if (sheetLayout == null || sheet == material)
                    continue;
                pages.Add(new Page { layout = sheetLayout, texture = sheet.mainTexture != null ? sheet.mainTexture : sheetLayout.texture, sheet = i + 1,
                                     material = sheet, name = sheet.name, kind = "Other sheet" });
            }
            return pages;
        }

        public override void OnOpen()
        {
            editorWindow.wantsMouseMove = true;
            ChiselSurfaceSelectionManager.SelectionChanged += Repaint;
            Undo.undoRedoPerformed += Repaint;
        }

        public override void OnClose()
        {
            ChiselSurfaceSelectionManager.SelectionChanged -= Repaint;
            Undo.undoRedoPerformed -= Repaint;
        }

        void Repaint()
        {
            if (editorWindow != null)
                editorWindow.Repaint();
        }

        // Pins a tile (sheet, id) on the shown material's surfaces; Automatic with id 0
        public bool Pin(Vector2Int tile)
        {
            var group = Group;
            if (group == null || (tile.y != 0 && !group.CanPin(tile)))
                return false;
            if (!HotspotTileChoice.Pin(group, tile))
                return false;
            Refresh();
            InternalEditorUtility.RepaintAllViews();
            return true;
        }

        public override void OnGUI(Rect rect)
        {
            var current = Event.current;
            if (current.type == EventType.Layout)
                Refresh();
            if (current.type == EventType.KeyDown && current.keyCode == KeyCode.Escape)
            {
                editorWindow.Close();
                current.Use();
                GUIUtility.ExitGUI();
            }

            var group = Group;
            var y = rect.y;
            SheetOfRow(new Rect(rect.x, y, rect.width, kRow), group);
            y += kRow;
            ToolbarRow(new Rect(rect.x, y, rect.width, kRow), group);
            y += kRow;
            if (pages.Count > 1)
            {
                PagesRow(new Rect(rect.x, y, rect.width, kPagesRow));
                y += kPagesRow;
            }
            var sheetArea = new Rect(rect.x + kPad, y + kPad, rect.width - 2 * kPad, rect.yMax - kLegend - kInfo - kPad - (y + kPad));
            if (group == null)
                GUI.Label(sheetArea, "None of these surfaces has a hotspot material", EditorStyles.centeredGreyMiniLabel);
            else
                SheetGUI(sheetArea, group, current);
            LegendRow(new Rect(rect.x, rect.yMax - kLegend - kInfo, rect.width, kLegend), group);
            InfoRow(new Rect(rect.x, rect.yMax - kInfo, rect.width, kInfo), group);
            ResizeGrip(rect);
            if (current.type == EventType.KeyDown && group != null)
                Keys(current, group);
        }

        void SheetOfRow(Rect row, HotspotTileGroup group)
        {
            if (Event.current.type == EventType.Repaint)
                EditorGUI.DrawRect(row, kRowColor);
            var x = row.x + kPad;
            var label = new GUIContent("Sheet of");
            var labelSize = EditorStyles.label.CalcSize(label);
            GUI.Label(new Rect(x, row.y + 4, labelSize.x, row.height - 8), label, EditorStyles.label);
            x += labelSize.x + 6;
            var applies = group != null ? new GUIContent(HotspotTileChoice.AppliesTo(group, groups)) : GUIContent.none;
            var appliesWidth = EditorStyles.miniLabel.CalcSize(applies).x;
            var right = row.xMax - kPad - appliesWidth - 6;
            for (int i = 0; i < groups.Count; i++)
            {
                var g = groups[i];
                var content = new GUIContent($"{g.material.name}  {g.Count}", AssetPreview.GetMiniThumbnail(g.material), g.material.name);
                var style = groups.Count == 1 ? EditorStyles.miniButton : i == 0 ? EditorStyles.miniButtonLeft : i == groups.Count - 1 ? EditorStyles.miniButtonRight : EditorStyles.miniButtonMid;
                var width = Mathf.Min(style.CalcSize(content).x + 4, Mathf.Max(60, right - x));
                var button = new Rect(x, row.y + 4, width, row.height - 8);
                if (GUI.Toggle(button, g == group, content, style) && g != group)
                    ShowMaterial(g.material);
                x += width;
                if (x >= right)
                    break;
            }
            GUI.Label(new Rect(row.xMax - kPad - appliesWidth, row.y + 4, appliesWidth, row.height - 8), applies, EditorStyles.miniLabel);
        }

        void ToolbarRow(Rect row, HotspotTileGroup group)
        {
            var x = row.x + kPad;
            var top = row.y + 4;
            var height = row.height - 8;
            using (new EditorGUI.DisabledScope(group == null))
            {
                var automatic = group != null && group.pinned.y == 0;
                if (GUI.Toggle(new Rect(x, top, 80, height), automatic, kAutomaticContent, EditorStyles.miniButton) && !automatic)
                    Pin(Vector2Int.zero);
            }
            x += 86;
            const float kFitWidth = 70, kFittingWidth = 84;
            var fitX = row.xMax - kPad - kFitWidth;
            var fittingX = fitX - 6 - kFittingWidth;
            find = EditorGUI.TextField(new Rect(x, top + 1, Mathf.Max(40, fittingX - 6 - x), height), find, EditorStyles.toolbarSearchField);
            fittingOnly = GUI.Toggle(new Rect(fittingX, top, kFittingWidth, height), fittingOnly, kFittingOnlyContent, EditorStyles.miniButton);
            if (GUI.Toggle(new Rect(fitX, top, kFitWidth * 0.5f, height), view.fit, kFitContent, EditorStyles.miniButtonLeft) && !view.fit)
                view.Fit();
            if (GUI.Toggle(new Rect(fitX + kFitWidth * 0.5f, top, kFitWidth * 0.5f, height), view.IsOneToOne, kOneToOneContent, EditorStyles.miniButtonRight) && !view.IsOneToOne)
                view.OneToOne();
        }

        void PagesRow(Rect row)
        {
            if (Event.current.type == EventType.Repaint)
                EditorGUI.DrawRect(row, kRowColor);
            var x = row.x + kPad;
            for (int i = 0; i < pages.Count; i++)
            {
                var page = pages[i];
                var nameSize = EditorStyles.label.CalcSize(new GUIContent(page.name));
                var kindSize = EditorStyles.miniLabel.CalcSize(new GUIContent(page.kind));
                var width = 4 + 32 + 6 + Mathf.Max(nameSize.x, kindSize.x) + 8;
                var button = new Rect(x, row.y + 4, width, row.height - 8);
                if (GUI.Toggle(button, i == shown, GUIContent.none, EditorStyles.helpBox) && i != shown)
                    ShowPage(i);
                if (Event.current.type == EventType.Repaint)
                {
                    var thumb = new Rect(button.x + 4, button.y + 3, 32, button.height - 6);
                    if (page.texture != null)
                        GUI.DrawTexture(thumb, page.texture, ScaleMode.ScaleAndCrop);
                    if (i == shown)
                        HotspotSheetView.Outline(new Vector3[] { new(button.xMin, button.yMin), new(button.xMax, button.yMin), new(button.xMax, button.yMax), new(button.xMin, button.yMax) }, HotspotSheetView.kPinMark, 2);
                    GUI.Label(new Rect(thumb.xMax + 6, button.y + 1, width, 18), page.name);
                    GUI.Label(new Rect(thumb.xMax + 6, button.y + 17, width, 16), page.kind, EditorStyles.miniLabel);
                }
                x += width + 6;
                if (x > row.xMax)
                    break;
            }
        }

        bool Matches(in HotspotTile tile) => HotspotTileChoice.NameMatches(tile.name, find);
        bool Finding => !string.IsNullOrWhiteSpace(find);

        void SheetGUI(Rect area, HotspotTileGroup group, Event current)
        {
            var page = Shown;
            var layout = page.layout;
            if (layout == null)
                return;
            view.Layout(area, layout.size);
            GUI.BeginClip(area);
            try
            {
                var local = current.mousePosition;
                var inside = new Rect(0, 0, area.width, area.height).Contains(local);
                if (view.Events(current, local, inside))
                {
                    Repaint();
                    return;
                }
                var at = inside ? layout.TileAt(view.ToPixel(local)) : 0;
                var key = new Vector2Int(page.sheet, at);
                if (at != 0 && (fittingOnly && group.FitOf(key) == HotspotTileFit.None))
                    at = 0;
                switch (current.type)
                {
                    case EventType.MouseMove:
                        if (at != hover) { hover = at; Repaint(); }
                        break;
                    case EventType.MouseDown when current.button == 0 && inside:
                        if (at != 0 && group.CanPin(new Vector2Int(page.sheet, at)))
                            Pin(new Vector2Int(page.sheet, at));
                        current.Use();
                        break;
                    case EventType.Repaint:
                        if (at != 0 && group.CanPin(new Vector2Int(page.sheet, at)))
                            EditorGUIUtility.AddCursorRect(new Rect(0, 0, area.width, area.height), MouseCursor.Link);
                        DrawSheet(page, group);
                        break;
                }
            }
            finally
            {
                GUI.EndClip();
            }
        }

        bool IsPinned(HotspotTileGroup group, Page page, int id) => group.pinned.y == id && group.pinned.x == page.sheet && id != 0;

        void DrawSheet(Page page, HotspotTileGroup group)
        {
            var layout = page.layout;
            view.DrawTexture(layout.size, page.texture != null ? page.texture : layout.texture);
            if (layout.tiles == null)
                return;
            var pinnedId = group.pinned.x == page.sheet ? group.pinned.y : 0;
            var corners = new HashSet<int> { HotspotTileChoice.CornersOf(layout, hover), HotspotTileChoice.CornersOf(layout, pinnedId) };
            corners.Remove(0);
            foreach (var tile in layout.tiles)
            {
                if (tile.points == null || tile.points.Length < 3)
                    continue;
                var points = view.PointsOf(tile);
                var shaped = ChiselHotspotLayout.IsShaped(tile);
                var key = new Vector2Int(page.sheet, tile.id);
                var fit = group.FitOf(key);
                if (fittingOnly && fit == HotspotTileFit.None)
                {
                    HotspotSheetView.Fill(points, kHidden, shaped);
                    continue;
                }
                if (Finding && !Matches(tile))
                    HotspotSheetView.Fill(points, kDim, shaped);
                else if (fit == HotspotTileFit.None)
                    HotspotSheetView.Fill(points, kNone, shaped);
                else if (fit == HotspotTileFit.Some)
                    HotspotSheetView.Fill(points, kSome, shaped);
                if (IsPinned(group, page, tile.id))
                    HotspotSheetView.Fill(points, HotspotSheetView.kPinned, shaped);
                HotspotSheetView.Outline(points, HotspotSheetView.kLine, 1.5f);
                if (tile.holes != null)
                {
                    foreach (var hole in tile.holes)
                    {
                        if (hole.points == null || hole.points.Length < 3)
                            continue;
                        var loop = new Vector3[hole.points.Length];
                        for (int i = 0; i < loop.Length; i++)
                            loop[i] = view.ToView(hole.points[i]);
                        HotspotSheetView.Outline(loop, HotspotSheetView.kLine, 1.5f);
                    }
                }
                var bounds = HotspotSheetView.BoundsOf(points);
                if (fit == HotspotTileFit.Some)
                    view.Label(bounds.max, $"{group.FitCount(key)}/{group.Count}", TextAnchor.LowerRight);
                if (HotspotTileChoice.IsPinOnly(tile) && fit != HotspotTileFit.None)
                    HotspotSheetView.PinMark(new Rect(bounds.xMax - 14, bounds.y + 3, 10, 12), HotspotSheetView.kPinMark);
            }
            // on top: the pieces of the hovered or pinned tile's inside corners, the pinned, the found and the hovered
            foreach (var tile in layout.tiles)
            {
                if (tile.points == null || tile.points.Length < 3)
                    continue;
                var key = new Vector2Int(page.sheet, tile.id);
                if (fittingOnly && group.FitOf(key) == HotspotTileFit.None)
                    continue;
                var points = view.PointsOf(tile);
                var pinned = IsPinned(group, page, tile.id);
                var found = Finding && Matches(tile);
                if (corners.Contains(tile.id))
                    HotspotSheetView.Dashed(points, HotspotSheetView.kHover);
                if (tile.id == hover || pinned)
                    HotspotSheetView.Outline(points, HotspotSheetView.kHover, 3);
                else if (found)
                    HotspotSheetView.Outline(points, Color.white, 2.5f);
                if (tile.id == hover || pinned || found)
                    view.Label(HotspotSheetView.BoundsOf(points).center, HotspotTileChoice.NameOf(layout, tile.id));
            }
        }

        void LegendRow(Rect row, HotspotTileGroup group)
        {
            if (Event.current.type != EventType.Repaint)
                return;
            EditorGUI.DrawRect(row, kRowColor);
            var x = row.x + kPad;
            var top = row.y + (row.height - 10) * 0.5f;
            void Item(string text, Color fill, bool pin = false)
            {
                var swatch = new Rect(x, top, 10, 10);
                if (pin)
                    HotspotSheetView.PinMark(new Rect(swatch.x, swatch.y - 1, 10, 12), HotspotSheetView.kPinMark);
                else
                {
                    EditorGUI.DrawRect(swatch, new Color(0.55f, 0.55f, 0.55f));
                    EditorGUI.DrawRect(swatch, fill);
                    HotspotSheetView.Outline(new Vector3[] { new(swatch.xMin, swatch.yMin), new(swatch.xMax, swatch.yMin), new(swatch.xMax, swatch.yMax), new(swatch.xMin, swatch.yMax) }, HotspotSheetView.kLine, 1);
                }
                var content = new GUIContent(text);
                var width = EditorStyles.miniLabel.CalcSize(content).x;
                GUI.Label(new Rect(x + 14, row.y, width, row.height), content, EditorStyles.miniLabel);
                x += 14 + width + 10;
            }
            Item("Fits all", Color.clear);
            Item("Fits some", kSome);
            Item("Doesn't fit", kNone);
            Item("Pin only", Color.clear, pin: true);
            if (group != null && Shown.layout != null)
            {
                var summary = new GUIContent(HotspotTileChoice.Summary(group, Shown.layout, Shown.sheet));
                var width = EditorStyles.miniLabel.CalcSize(summary).x;
                GUI.Label(new Rect(row.xMax - kPad - width, row.y, width, row.height), summary, EditorStyles.miniLabel);
            }
        }

        // The hovered tile, or else the pinned one: name, pixels, aspect, flags, its inside corner pieces (or the tiles
        // using it), and how many surfaces it fits
        void InfoRow(Rect row, HotspotTileGroup group)
        {
            var page = Shown;
            var layout = page.layout;
            var link = EditorStyles.linkLabel.CalcSize(kEditLayoutContent);
            var linkRect = new Rect(row.xMax - 18 - link.x, row.y + (row.height - link.y) * 0.5f, link.x, link.y);
            using (new EditorGUI.DisabledScope(layout == null))
            {
                if (EditorGUI.LinkButton(linkRect, kEditLayoutContent))
                {
                    ChiselHotspotLayoutWindow.Open(layout, page.material);
                    editorWindow.Close();
                    GUIUtility.ExitGUI();
                }
            }
            if (group == null || layout == null)
                return;
            var id = hover != 0 ? hover : group.pinned.x == page.sheet && group.pinned.y > 0 ? group.pinned.y : 0;
            if (id == 0 || layout.IndexOfTile(id) < 0)
            {
                GUI.Label(new Rect(row.x + kPad, row.y, linkRect.x - row.x - kPad, row.height), "Click a tile to pin it on these surfaces", EditorStyles.miniLabel);
                return;
            }
            var x = row.x + kPad;
            var nameContent = new GUIContent(HotspotTileChoice.NameOf(layout, id));
            var nameWidth = EditorStyles.boldLabel.CalcSize(nameContent).x;
            GUI.Label(new Rect(x, row.y, nameWidth, row.height), nameContent, EditorStyles.boldLabel);
            x += nameWidth + 4;
            var info = new GUIContent(HotspotTileChoice.InfoOf(layout, id));
            var infoWidth = Mathf.Min(EditorStyles.label.CalcSize(info).x, linkRect.x - x);
            GUI.Label(new Rect(x, row.y, infoWidth, row.height), info, EditorStyles.label);
            x += infoWidth + 6;
            var parts = new List<string>();
            var corners = HotspotTileChoice.CornersOf(layout, id);
            if (corners != 0)
                parts.Add("Corners: " + HotspotTileChoice.NameOf(layout, corners));
            var users = HotspotTileChoice.UsersOf(layout, id);
            if (users.Count > 0)
                parts.Add("Used by " + string.Join(", ", users.ConvertAll(user => HotspotTileChoice.NameOf(layout, user))));
            parts.Add(HotspotTileChoice.FitText(group, new Vector2Int(page.sheet, id)));
            var warm = new GUIStyle(EditorStyles.label) { normal = { textColor = kWarm } };
            GUI.Label(new Rect(x, row.y, Mathf.Max(0, linkRect.x - 6 - x), row.height), string.Join($" {HotspotTileChoice.kDot} ", parts), warm);
        }

        // The arrow keys go from tile to tile (those that can be pinned), Enter pins the one reached
        void Keys(Event current, HotspotTileGroup group)
        {
            var page = Shown;
            var direction = current.keyCode switch
            {
                KeyCode.LeftArrow  => Vector2.left,
                KeyCode.RightArrow => Vector2.right,
                KeyCode.UpArrow    => Vector2.down,
                KeyCode.DownArrow  => Vector2.up,
                _                  => Vector2.zero,
            };
            if (direction != Vector2.zero && GUIUtility.keyboardControl == 0)
            {
                var from = hover != 0 ? hover : group.pinned.x == page.sheet ? group.pinned.y : 0;
                hover = HotspotSheetView.NextTile(page.layout, from, direction, tile => !group.CanPin(new Vector2Int(page.sheet, tile.id)));
                current.Use();
                Repaint();
            } else if ((current.keyCode == KeyCode.Return || current.keyCode == KeyCode.KeypadEnter) && GUIUtility.keyboardControl == 0 && hover != 0)
            {
                Pin(new Vector2Int(page.sheet, hover));
                current.Use();
            }
        }
    }
}
