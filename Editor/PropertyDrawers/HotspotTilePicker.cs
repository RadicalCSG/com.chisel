using System;
using System.Collections.Generic;
using Chisel.Core;
using UnityEditor;
using UnityEngine;

namespace Chisel.Editors
{
    // The tiles of a hotspot layout over its texture, to pick the one a surface keeps: tiles the surface cannot take are
    // greyed out, the one under the mouse shows its outline and name, and Automatic lets matching choose again
    sealed class HotspotTilePicker : PopupWindowContent
    {
        const float kWidth     = 520;
        const float kHeader    = 22;
        const float kThumbnail = 40;
        static readonly Color kLine      = new(1, 1, 1, 0.6f);
        static readonly Color kHover     = new(1, 0.75f, 0.1f, 1);
        static readonly Color kSelected  = new(0.2f, 0.6f, 1, 0.35f);
        static readonly Color kUnfit     = new(0, 0, 0, 0.7f);
        static readonly Color kDisabled  = new(0.5f, 0, 0, 0.45f);

        readonly ChiselHotspotLayout layout;
        readonly IReadOnlyList<Texture> textures;
        readonly HashSet<int> fits;   // the ids of the tiles the surface can take; null: all of them
        readonly int current;
        readonly Action<int> pick;
        int shown;
        int hover;

        public HotspotTilePicker(ChiselHotspotLayout layout, IReadOnlyList<Texture> textures, HashSet<int> fits, int current, Action<int> pick)
        {
            this.layout = layout; this.textures = textures; this.fits = fits; this.current = current; this.pick = pick;
        }

        float Thumbnails => textures != null && textures.Count > 1 ? kThumbnail + 4 : 0;

        public override Vector2 GetWindowSize()
        {
            var aspect = layout.size.x > 0 ? (float)layout.size.y / layout.size.x : 1;
            return new Vector2(kWidth, kHeader + Thumbnails + kWidth * aspect);
        }

        public override void OnOpen()
        {
            editorWindow.wantsMouseMove = true;
        }

        public override void OnGUI(Rect rect)
        {
            var current = Event.current;
            var header = new Rect(rect.x + 2, rect.y + 2, rect.width - 4, kHeader - 4);
            var automatic = new GUIContent("Automatic", "Let matching choose the tile that fits best");
            using (new EditorGUI.DisabledScope(this.current == 0))
            {
                if (GUI.Button(new Rect(header.x, header.y, 90, header.height), automatic, EditorStyles.miniButton))
                    Choose(0);
            }
            var label = hover != 0 ? NameOf(hover) + (IsPieces(hover) ? " (inside corner pieces)" : fits != null && !fits.Contains(hover) ? " (does not fit this surface)" : string.Empty) : "Click a tile to keep it";
            GUI.Label(new Rect(header.x + 96, header.y, header.width - 96, header.height), label, EditorStyles.miniLabel);

            var y = rect.y + kHeader;
            if (Thumbnails > 0)
            {
                for (int i = 0; i < textures.Count; i++)
                {
                    var thumb = new Rect(rect.x + 2 + i * (kThumbnail + 4), y, kThumbnail, kThumbnail);
                    if (current.type == EventType.Repaint)
                    {
                        if (textures[i] != null)
                            GUI.DrawTexture(thumb, textures[i], ScaleMode.ScaleToFit);
                        if (i == shown)
                            EditorGUI.DrawRect(new Rect(thumb.x, thumb.yMax - 2, thumb.width, 2), kHover);
                    }
                    if (current.type == EventType.MouseDown && thumb.Contains(current.mousePosition))
                    {
                        shown = i;
                        current.Use();
                    }
                }
                y += Thumbnails;
            }

            var sheet = new Rect(rect.x, y, rect.width, rect.yMax - y);
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
            var texture = textures != null && textures.Count > 0 ? textures[Mathf.Clamp(shown, 0, textures.Count - 1)] : layout.texture;
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
                if ((fits != null && !fits.Contains(tile.id)) || IsPieces(tile.id))
                {
                    Handles.color = kUnfit;
                    ChiselHotspotLayoutWindow.Fill(points, shaped);
                } else if ((tile.flags & HotspotTileFlags.Disabled) != 0)
                {
                    Handles.color = kDisabled;
                    ChiselHotspotLayoutWindow.Fill(points, shaped);
                }
                if (tile.id == this.current)
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
            var index = layout.IndexOfTile(id);
            return index >= 0 && (layout.tiles[index].flags & (HotspotTileFlags.InsideCorners | HotspotTileFlags.InsideCornerTurned)) != 0;
        }

        string NameOf(int id)
        {
            var index = layout.IndexOfTile(id);
            return index < 0 ? string.Empty : (string.IsNullOrEmpty(layout.tiles[index].name) ? "Tile " + id : layout.tiles[index].name);
        }

        void Choose(int id)
        {
            pick?.Invoke(id);
            editorWindow.Close();
        }
    }
}
