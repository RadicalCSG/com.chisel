using System;
using System.Collections.Generic;
using Chisel.Core;
using UnityEditor;
using UnityEngine;

namespace Chisel.Editors
{
    // The tiles of a hotspot layout over its texture, to pick the one a surface keeps: tiles the surface cannot take are
    // greyed out, the one under the mouse shows its outline and name, and Automatic lets matching choose again. A page for
    // each texture the layout is drawn with (its variants), and one for each other sheet drawn with it.
    sealed class HotspotTilePicker : PopupWindowContent
    {
        // What a page shows: a layout over a texture, and the sheet its tiles are on (0: the material's own; otherwise
        // its place in the material's other sheets, HotspotSurface.pinnedSheet)
        public struct Page
        {
            public ChiselHotspotLayout layout;
            public Texture             texture;
            public int                 sheet;
        }

        const float kWidth     = 520;
        const float kHeader    = 22;
        const float kThumbnail = 40;
        // a page this low at the window's width (a trim sheet) is drawn this high instead, and scrolls across
        const float kMinPageHeight = 128;
        const float kScrollBar     = 14;
        static readonly Color kLine      = new(1, 1, 1, 0.6f);
        static readonly Color kHover     = new(1, 0.75f, 0.1f, 1);
        static readonly Color kSelected  = new(0.2f, 0.6f, 1, 0.35f);
        static readonly Color kUnfit     = new(0, 0, 0, 0.7f);
        static readonly Color kDisabled  = new(0.5f, 0, 0, 0.45f);

        readonly IReadOnlyList<Page> pages;
        readonly HashSet<Vector2Int> fits;   // (sheet, id) of the tiles the surface can take; null: all of them
        readonly Vector2Int current;         // (sheet, id) pinned; id 0: none, -1: different ones
        readonly Action<Vector2Int> pick;
        int shown;
        int hover;
        Vector2 scroll;

        ChiselHotspotLayout Layout => pages[shown].layout;
        int Sheet => pages[shown].sheet;

        static readonly GUIContent kAutomatic = new GUIContent("Automatic", "Let matching choose the tile that fits best");
        readonly GUIContent none;

        // none: the button that picks no tile (id 0), Automatic by default
        public HotspotTilePicker(IReadOnlyList<Page> pages, HashSet<Vector2Int> fits, Vector2Int current, Action<Vector2Int> pick, GUIContent none = null)
        {
            this.pages = pages; this.fits = fits; this.current = current; this.pick = pick; this.none = none ?? kAutomatic;
            for (int i = 0; i < pages.Count; i++)
            {
                if (pages[i].sheet == current.x) { shown = i; break; }
            }
        }

        float Thumbnails => pages.Count > 1 ? kThumbnail + 4 : 0;


        public override Vector2 GetWindowSize()
        {
            var height = 0f;
            foreach (var page in pages)
                height = Mathf.Max(height, PageSize(page.layout, kWidth).y + (PageSize(page.layout, kWidth).x > kWidth ? kScrollBar : 0));
            return new Vector2(kWidth, kHeader + Thumbnails + height);
        }

        // A page's size in the window: across its width, or at least kMinPageHeight high (then wider than the window)
        static Vector2 PageSize(ChiselHotspotLayout layout, float width)
        {
            if (layout == null || layout.size.x <= 0 || layout.size.y <= 0)
                return new Vector2(width, width);
            var scale = Mathf.Max(width / layout.size.x, Mathf.Min(kMinPageHeight, width) / layout.size.y);
            return new Vector2(layout.size.x * scale, layout.size.y * scale);
        }

        public override void OnOpen()
        {
            editorWindow.wantsMouseMove = true;
        }

        bool Fits(int id) => fits == null || fits.Contains(new Vector2Int(Sheet, id));

        public override void OnGUI(Rect rect)
        {
            var current = Event.current;
            var header = new Rect(rect.x + 2, rect.y + 2, rect.width - 4, kHeader - 4);
            using (new EditorGUI.DisabledScope(this.current.y == 0))
            {
                if (GUI.Button(new Rect(header.x, header.y, 90, header.height), none, EditorStyles.miniButton))
                    Choose(0);
            }
            var label = hover != 0 ? NameOf(hover) + (IsPieces(hover) ? " (inside corner pieces)" : !Fits(hover) ? " (does not fit this surface)" : string.Empty) : "Click a tile to keep it";
            GUI.Label(new Rect(header.x + 96, header.y, header.width - 96, header.height), label, EditorStyles.miniLabel);

            var y = rect.y + kHeader;
            if (Thumbnails > 0)
            {
                for (int i = 0; i < pages.Count; i++)
                {
                    var thumb = new Rect(rect.x + 2 + i * (kThumbnail + 4), y, kThumbnail, kThumbnail);
                    if (current.type == EventType.Repaint)
                    {
                        if (pages[i].texture != null)
                            GUI.DrawTexture(thumb, pages[i].texture, ScaleMode.ScaleToFit);
                        else
                            EditorGUI.DrawRect(thumb, new Color(0.3f, 0.3f, 0.3f));
                        if (i == shown)
                            EditorGUI.DrawRect(new Rect(thumb.x, thumb.yMax - 2, thumb.width, 2), kHover);
                    }
                    if (current.type == EventType.MouseDown && thumb.Contains(current.mousePosition))
                    {
                        shown = i;
                        hover = 0;
                        current.Use();
                    }
                }
                y += Thumbnails;
            }

            // the page at its own aspect, along the top; a low one scrolls across
            var layout = Layout;
            if (layout == null)
                return;
            var pageSize = PageSize(layout, rect.width);
            var scrolls  = pageSize.x > rect.width + 0.5f;
            Rect sheet;
            if (scrolls)
            {
                scroll = GUI.BeginScrollView(new Rect(rect.x, y, rect.width, pageSize.y + kScrollBar), scroll, new Rect(0, 0, pageSize.x, pageSize.y), true, false);
                sheet = new Rect(0, 0, pageSize.x, pageSize.y);
            } else
                sheet = new Rect(rect.x, y, pageSize.x, pageSize.y);
            try
            {
                DrawPage(current, layout, sheet);
            }
            finally
            {
                if (scrolls)
                    GUI.EndScrollView();
            }
        }

        void DrawPage(Event current, ChiselHotspotLayout layout, Rect sheet)
        {
            var scale = new Vector2(sheet.width / Mathf.Max(1, layout.size.x), sheet.height / Mathf.Max(1, layout.size.y));
            Vector3 ToSheet(Vector2Int p) => new(sheet.x + p.x * scale.x, sheet.y + p.y * scale.y, 0);

            if (current.type == EventType.MouseMove || current.type == EventType.MouseDown)
            {
                var pixel = (current.mousePosition - sheet.position) / scale;
                var at = sheet.Contains(current.mousePosition) ? layout.TileAt(pixel) : 0;
                if (current.type == EventType.MouseDown && current.button == 0 && at != 0 && !IsPieces(at))
                {
                    Choose(at);
                    current.Use();
                    return;
                }
                if (at != hover)
                {
                    hover = at;
                    editorWindow.Repaint();
                }
            }

            if (current.type != EventType.Repaint)
                return;
            var texture = pages[shown].texture != null ? pages[shown].texture : layout.texture;
            if (texture != null)
                GUI.DrawTexture(sheet, texture, ScaleMode.StretchToFill, true);
            else
                EditorGUI.DrawRect(sheet, new Color(0.3f, 0.3f, 0.3f));
            foreach (var tile in layout.tiles)
            {
                if (tile.points == null || tile.points.Length < 3)
                    continue;
                var points = new Vector3[tile.points.Length];
                for (int i = 0; i < points.Length; i++)
                    points[i] = ToSheet(tile.points[i]);
                var shaped = ChiselHotspotLayout.IsShaped(tile);
                if (!Fits(tile.id) || IsPieces(tile.id))
                {
                    Handles.color = kUnfit;
                    ChiselHotspotLayoutWindow.Fill(points, shaped);
                } else if ((tile.flags & HotspotTileFlags.Disabled) != 0)
                {
                    Handles.color = kDisabled;
                    ChiselHotspotLayoutWindow.Fill(points, shaped);
                }
                if (tile.id == this.current.y && Sheet == this.current.x)
                {
                    Handles.color = kSelected;
                    ChiselHotspotLayoutWindow.Fill(points, shaped);
                }
                Handles.color = tile.id == hover ? kHover : kLine;
                var outline = new Vector3[points.Length + 1];
                points.CopyTo(outline, 0);
                outline[points.Length] = points[0];
                Handles.DrawAAPolyLine(tile.id == hover ? 4 : 1.5f, outline);
                if (tile.holes != null)
                {
                    foreach (var hole in tile.holes)
                    {
                        if (hole.points == null || hole.points.Length < 3)
                            continue;
                        var loop = new Vector3[hole.points.Length + 1];
                        for (int i = 0; i < hole.points.Length; i++)
                            loop[i] = ToSheet(hole.points[i]);
                        loop[hole.points.Length] = loop[0];
                        Handles.DrawAAPolyLine(tile.id == hover ? 4 : 1.5f, loop);
                    }
                }
            }
        }

        // a tile of inside corner pieces, which no surface takes
        bool IsPieces(int id)
        {
            var layout = Layout;
            var index = layout.IndexOfTile(id);
            return index >= 0 && (layout.tiles[index].flags & (HotspotTileFlags.InsideCorners | HotspotTileFlags.InsideCornerTurned)) != 0;
        }

        string NameOf(int id) => NameOf(Layout, id);

        internal static string NameOf(ChiselHotspotLayout layout, int id)
        {
            var index = layout != null ? layout.IndexOfTile(id) : -1;
            return index < 0 ? string.Empty : (string.IsNullOrEmpty(layout.tiles[index].name) ? "Tile " + id : layout.tiles[index].name);
        }

        void Choose(int id)
        {
            pick?.Invoke(new Vector2Int(id == 0 ? 0 : Sheet, id));
            editorWindow.Close();
        }
    }
}
