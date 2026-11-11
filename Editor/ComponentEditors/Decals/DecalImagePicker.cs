using System.Collections.Generic;
using Chisel.Components;
using Chisel.Core;
using UnityEditor;
using UnityEngine;

namespace Chisel.Editors
{
    // Choosing the image of a decal sheet the decals show: the material's own texture over a checkerboard, every tile
    // can be chosen, and Whole texture shows all of it. Stays open; picking sets every decal given in one undo step
    sealed class DecalImagePicker : ResizablePopup
    {
        internal const string kSizeKey = "Chisel.DecalImagePicker.Size";
        internal static readonly Vector2 kMinSize = new(320, 320);
        internal const float kDefaultWidth = 460;
        internal const string kUndoName = "Decal Image";

        const float kPad  = 6;
        const float kRow  = 28;
        const float kInfo = 26;


        static readonly GUIContent kWholeTextureContent = new("Whole texture", "The decal shows the whole texture");
        static readonly GUIContent kFitContent          = new("Fit", "Show the whole sheet");
        static readonly GUIContent kOneToOneContent     = new("1:1", "One pixel of the screen for each pixel of the texture");
        static readonly GUIContent kEditLayoutContent   = new("Edit layout...", "Open the layout editor for this sheet");

        readonly ChiselDecalComponent[] decals;
        readonly Material material;
        readonly ChiselHotspotLayout layout;
        readonly HotspotSheetView view = new();
        Texture2D checker;
        string find = string.Empty;
        int hover;

        public DecalImagePicker(ChiselDecalComponent[] decals, Material material, ChiselHotspotLayout layout)
            : base(kSizeKey, kMinSize, new Vector2(kDefaultWidth, kDefaultWidth))
        {
            this.decals = decals; this.material = material; this.layout = layout;
            SetSizeIfNotRemembered(new Vector2(kDefaultWidth, HeightFor(layout != null ? layout.size : Vector2Int.zero, kDefaultWidth - 2 * kPad, kRow + kInfo + 2 * kPad)));
        }

        // The image the decals show (0: the whole texture), -1 when they differ
        public int Current
        {
            get
            {
                var current = -2;
                foreach (var decal in decals)
                {
                    if (decal == null) continue;
                    if (current == -2) current = decal.ImageTile;
                    else if (current != decal.ImageTile) return -1;
                }
                return current == -2 ? 0 : current;
            }
        }

        // Sets the image of every decal, in one undo step; false when they all show it already
        public static bool Choose(IReadOnlyList<ChiselDecalComponent> decals, int image)
        {
            var changing = new List<Object>();
            foreach (var decal in decals)
            {
                if (decal != null && decal.ImageTile != image)
                    changing.Add(decal);
            }
            if (changing.Count == 0)
                return false;
            Undo.RecordObjects(changing.ToArray(), kUndoName);
            foreach (ChiselDecalComponent decal in changing)
            {
                decal.ImageTile = image;
                EditorUtility.SetDirty(decal);
            }
            return true;
        }

        public override void OnOpen()
        {
            editorWindow.wantsMouseMove = true;
            checker = HotspotSheetView.CreateChecker();
            Undo.undoRedoPerformed += Repaint;
        }

        public override void OnClose()
        {
            Undo.undoRedoPerformed -= Repaint;
            if (checker != null)
                Object.DestroyImmediate(checker);
        }

        void Repaint()
        {
            if (editorWindow != null)
                editorWindow.Repaint();
        }

        void Pick(int image)
        {
            if (Choose(decals, image))
                UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
        }

        bool Finding => !string.IsNullOrWhiteSpace(find);

        public override void OnGUI(Rect rect)
        {
            var current = Event.current;
            if (current.type == EventType.KeyDown && current.keyCode == KeyCode.Escape)
            {
                editorWindow.Close();
                current.Use();
                GUIUtility.ExitGUI();
            }
            var chosen = Current;

            // the toolbar
            var top = rect.y + 4;
            var height = kRow - 8;
            var whole = new Rect(rect.x + kPad, top, 100, height);
            if (GUI.Toggle(whole, chosen == 0, kWholeTextureContent, EditorStyles.miniButton) && chosen != 0)
                Pick(0);
            const float kFitWidth = 70;
            var fitX = rect.xMax - kPad - kFitWidth;
            find = EditorGUI.TextField(new Rect(whole.xMax + 6, top + 1, Mathf.Max(40, fitX - 6 - whole.xMax - 6), height), find, EditorStyles.toolbarSearchField);
            if (GUI.Toggle(new Rect(fitX, top, kFitWidth * 0.5f, height), view.fit, kFitContent, EditorStyles.miniButtonLeft) && !view.fit)
                view.Fit();
            if (GUI.Toggle(new Rect(fitX + kFitWidth * 0.5f, top, kFitWidth * 0.5f, height), view.IsOneToOne, kOneToOneContent, EditorStyles.miniButtonRight) && !view.IsOneToOne)
                view.OneToOne();

            var area = new Rect(rect.x + kPad, rect.y + kRow + kPad, rect.width - 2 * kPad, rect.height - kRow - kInfo - 2 * kPad);
            if (layout != null)
                SheetGUI(area, current, chosen);
            InfoRow(new Rect(rect.x, rect.yMax - kInfo, rect.width, kInfo), chosen);
            ResizeGrip(rect);
        }

        void SheetGUI(Rect area, Event current, int chosen)
        {
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
                switch (current.type)
                {
                    case EventType.MouseMove:
                        if (at != hover) { hover = at; Repaint(); }
                        break;
                    case EventType.MouseDown when current.button == 0 && inside:
                        if (at != 0)
                            Pick(at);
                        current.Use();
                        break;
                    case EventType.Repaint:
                        if (at != 0)
                            EditorGUIUtility.AddCursorRect(new Rect(0, 0, area.width, area.height), MouseCursor.Link);
                        Draw(chosen);
                        break;
                }
            }
            finally
            {
                GUI.EndClip();
            }
        }

        void Draw(int chosen)
        {
            view.DrawTexture(layout.size, material != null && material.mainTexture != null ? material.mainTexture : layout.texture, checker);
            var sheet = new Vector3[] { view.ToView(Vector2.zero), view.ToView(new Vector2(layout.size.x, 0)), view.ToView(layout.size), view.ToView(new Vector2(0, layout.size.y)) };
            if (chosen == 0)
                HotspotSheetView.Outline(sheet, ChiselEditorColors.Highlight, 3);
            if (layout.tiles == null)
                return;
            foreach (var tile in layout.tiles)
            {
                if (tile.points == null || tile.points.Length < 3)
                    continue;
                var points = view.PointsOf(tile);
                var shaped = ChiselHotspotLayout.IsShaped(tile);
                if (Finding && !HotspotTileChoice.NameMatches(tile.name, find))
                    HotspotSheetView.Fill(points, ChiselEditorColors.Dim, shaped);
                if (tile.id == chosen)
                    HotspotSheetView.Fill(points, HotspotSheetView.kPinned, shaped);
                HotspotSheetView.Outline(points, HotspotSheetView.kLine, 1.5f);
            }
            foreach (var tile in layout.tiles)
            {
                if (tile.points == null || tile.points.Length < 3)
                    continue;
                var found = Finding && HotspotTileChoice.NameMatches(tile.name, find);
                if (tile.id != hover && tile.id != chosen && !found)
                    continue;
                var points = view.PointsOf(tile);
                HotspotSheetView.Outline(points, tile.id == hover || tile.id == chosen ? ChiselEditorColors.Highlight : Color.white, tile.id == hover || tile.id == chosen ? 3 : 2.5f);
                view.Label(HotspotSheetView.BoundsOf(points).center, HotspotTileChoice.NameOf(layout, tile.id));
            }
        }

        // The hovered image, or else the chosen one: name, pixels and aspect
        void InfoRow(Rect row, int chosen)
        {
            var link = EditorStyles.linkLabel.CalcSize(kEditLayoutContent);
            var linkRect = new Rect(row.xMax - 18 - link.x, row.y + (row.height - link.y) * 0.5f, link.x, link.y);
            if (EditorGUI.LinkButton(linkRect, kEditLayoutContent))
            {
                ChiselHotspotLayoutWindow.Open(layout, material);
                editorWindow.Close();
                GUIUtility.ExitGUI();
            }
            var id = hover != 0 ? hover : chosen;
            var x = row.x + kPad;
            string name, info;
            if (id > 0 && layout != null && layout.IndexOfTile(id) >= 0)
            {
                name = HotspotTileChoice.NameOf(layout, id);
                info = HotspotTileChoice.InfoOf(layout, id);
            } else if (id == 0)
            {
                name = kWholeTextureContent.text;
                info = layout != null ? $"{layout.size.x} {HotspotTileChoice.kTimes} {layout.size.y} {HotspotTileChoice.kDot} {HotspotTileChoice.AspectText(layout.size.x, layout.size.y)}" : string.Empty;
            } else
            {
                name = HotspotTileChoice.kDash;
                info = "The decals show different images";
            }
            var nameContent = new GUIContent(name);
            var nameWidth = EditorStyles.boldLabel.CalcSize(nameContent).x;
            GUI.Label(new Rect(x, row.y, nameWidth, row.height), nameContent, EditorStyles.boldLabel);
            x += nameWidth + 4;
            GUI.Label(new Rect(x, row.y, Mathf.Max(0, linkRect.x - 6 - x), row.height), info, EditorStyles.label);
        }
    }
}
