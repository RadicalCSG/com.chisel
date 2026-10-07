using System.Collections.Generic;
using System.IO;
using Chisel.Components;
using Chisel.Core;
using UnityEditor;
using UnityEngine;

namespace Chisel.Editors
{
    // Draws a hotspot layout over its texture: lines are dragged, added (right click, or V / H at the cursor) and removed
    // (Delete over a line); the selected tile's properties are on the right.
    public sealed class ChiselHotspotLayoutWindow : EditorWindow
    {
        const float kPanelWidth  = 280;
        const float kLineGrab    = 6;
        static readonly int[]      kSnaps     = { 1, 2, 4, 8, 16, 32, 64 };
        static readonly string[]   kSnapNames = { "1 px", "2 px", "4 px", "8 px", "16 px", "32 px", "64 px" };
        static readonly Color      kLine      = new(1, 1, 1, 0.85f);
        static readonly Color      kHover     = new(1, 0.75f, 0.1f, 1);
        static readonly Color      kSelected  = new(0.2f, 0.6f, 1, 0.35f);
        static readonly Color      kDisabled  = new(0, 0, 0, 0.55f);
        static readonly Color      kBorder    = new(1, 0.3f, 0.3f, 0.9f);

        [SerializeField] ChiselHotspotLayout layout;
        [SerializeField] int     selectedTile;
        [SerializeField] float   zoom = 1;
        [SerializeField] Vector2 pan;
        [SerializeField] int     snapIndex = 3;

        readonly List<HotspotLine> lines = new();
        int     hoverLine = -1;
        int     dragNode  = -1;
        bool    dragged;
        Vector2 panelScroll;
        Rect    canvas;

        [MenuItem("Window/Chisel/Hotspot Layout")]
        public static void Open() { Open(Selection.activeObject as ChiselHotspotLayout); }

        public static void Open(ChiselHotspotLayout layout)
        {
            var window = GetWindow<ChiselHotspotLayoutWindow>();
            window.titleContent = new GUIContent("Hotspot Layout");
            if (layout != null)
                window.SetLayout(layout);
            window.Show();
        }

        void SetLayout(ChiselHotspotLayout newLayout)
        {
            if (layout == newLayout)
                return;
            layout = newLayout;
            selectedTile = 0;
            hoverLine = dragNode = -1;
            Repaint();
        }

        void OnEnable()
        {
            wantsMouseMove = true;
            Undo.undoRedoPerformed += Repaint;
            Selection.selectionChanged += OnSelectionChanged;
        }

        void OnDisable()
        {
            Undo.undoRedoPerformed -= Repaint;
            Selection.selectionChanged -= OnSelectionChanged;
        }

        void OnSelectionChanged()
        {
            if (Selection.activeObject is ChiselHotspotLayout selected)
                SetLayout(selected);
        }

        // Records the layout for undo, and afterwards tells the models it changed
        void Edit(string name, System.Action edit)
        {
            Undo.RecordObject(layout, name);
            edit();
            EditorUtility.SetDirty(layout);
            layout.NotifyChanged();
            Repaint();
        }

        void OnGUI()
        {
            Toolbar();
            if (layout == null)
            {
                EditorGUILayout.HelpBox("Select a Hotspot Layout asset (Assets > Create > Chisel > Hotspot Layout), or drop one in the field above.", MessageType.Info);
                return;
            }
            if (layout.tiles.Count == 0 && !layout.HasCuts)
            {
                Undo.RecordObject(layout, "New Hotspot Layout");
                layout.ResetCuts();
                EditorUtility.SetDirty(layout);
            }
            var area = new Rect(0, EditorGUIUtility.singleLineHeight + 4, position.width, position.height - EditorGUIUtility.singleLineHeight - 4);
            canvas = new Rect(area.x, area.y, Mathf.Max(64, area.width - kPanelWidth), area.height);
            Canvas();
            GUILayout.BeginArea(new Rect(canvas.xMax, area.y, area.width - canvas.width, area.height));
            panelScroll = EditorGUILayout.BeginScrollView(panelScroll);
            Panel();
            EditorGUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        void Toolbar()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                var chosen = (ChiselHotspotLayout)EditorGUILayout.ObjectField(layout, typeof(ChiselHotspotLayout), false, GUILayout.Width(240));
                if (chosen != layout)
                    SetLayout(chosen);
                GUILayout.Space(8);
                GUILayout.Label("Snap", EditorStyles.miniLabel, GUILayout.ExpandWidth(false));
                snapIndex = EditorGUILayout.Popup(snapIndex, kSnapNames, EditorStyles.toolbarPopup, GUILayout.Width(60));
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("Fit", EditorStyles.toolbarButton, GUILayout.Width(40)))
                {
                    zoom = 1;
                    pan = Vector2.zero;
                }
            }
        }

        // Pixels (origin top left) to and from the canvas
        float Scale => Mathf.Min(canvas.width / Mathf.Max(1, layout.size.x), canvas.height / Mathf.Max(1, layout.size.y)) * 0.95f * zoom;
        Vector2 Origin => new Vector2(canvas.width, canvas.height) * 0.5f - (Vector2)layout.size * (Scale * 0.5f) + pan;
        Vector2 ToCanvas(Vector2 pixel) => Origin + pixel * Scale;
        Vector2 ToPixel(Vector2 local)  => (local - Origin) / Scale;
        int Snap(float value) { var snap = kSnaps[Mathf.Clamp(snapIndex, 0, kSnaps.Length - 1)]; return Mathf.RoundToInt(value / snap) * snap; }

        static float DistanceToSegment(Vector2 p, Vector2 a, Vector2 b)
        {
            var ab = b - a;
            var t = ab.sqrMagnitude > 0 ? Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude) : 0;
            return Vector2.Distance(p, a + ab * t);
        }

        int LineAt(Vector2 local)
        {
            var best = -1;
            var bestDistance = kLineGrab;
            for (int i = 0; i < lines.Count; i++)
            {
                var distance = DistanceToSegment(local, ToCanvas(lines[i].a), ToCanvas(lines[i].b));
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = i;
                }
            }
            return best;
        }

        void Canvas()
        {
            layout.GetLines(lines);
            var current = Event.current;
            GUI.BeginClip(canvas);
            var local = current.mousePosition;
            var sheet = new Rect(Origin, (Vector2)layout.size * Scale);

            if (current.type == EventType.Repaint)
            {
                EditorGUI.DrawRect(new Rect(0, 0, canvas.width, canvas.height), new Color(0.16f, 0.16f, 0.16f));
                if (layout.texture != null)
                    GUI.DrawTexture(sheet, layout.texture, ScaleMode.StretchToFill, true);
                else
                    EditorGUI.DrawRect(sheet, new Color(0.3f, 0.3f, 0.3f));
                DrawTiles();
                for (int i = 0; i < lines.Count; i++)
                {
                    var movable = layout.MoveRange(lines[i].node, out _, out _);
                    Handles.color = (i == hoverLine || (dragNode >= 0 && lines[i].node == dragNode)) ? kHover : kLine;
                    Handles.DrawAAPolyLine(movable ? 3 : 2, ToCanvas(lines[i].a), ToCanvas(lines[i].b));
                }
                Handles.color = kLine;
                Handles.DrawAAPolyLine(2, sheet.min, new Vector2(sheet.xMax, sheet.yMin), sheet.max, new Vector2(sheet.xMin, sheet.yMax), sheet.min);
            }

            var inside = new Rect(0, 0, canvas.width, canvas.height).Contains(local);
            switch (current.type)
            {
                case EventType.MouseMove:
                {
                    var hover = inside ? LineAt(local) : -1;
                    if (hover != hoverLine) { hoverLine = hover; Repaint(); }
                    break;
                }
                case EventType.ScrollWheel when inside:
                {
                    var pixel = ToPixel(local);
                    zoom = Mathf.Clamp(zoom * (current.delta.y > 0 ? 1 / 1.15f : 1.15f), 0.25f, 32);
                    pan += local - ToCanvas(pixel);
                    current.Use();
                    break;
                }
                case EventType.MouseDown when inside && current.button == 0 && !current.alt:
                {
                    hoverLine = LineAt(local);
                    if (hoverLine >= 0 && layout.MoveRange(lines[hoverLine].node, out _, out _))
                    {
                        dragNode = lines[hoverLine].node;
                        dragged = false;
                        Undo.RecordObject(layout, "Move Hotspot Line");
                    } else
                        selectedTile = layout.TileAt(ToPixel(local));
                    current.Use();
                    GUI.FocusControl(null);
                    Repaint();
                    break;
                }
                case EventType.MouseDrag when dragNode >= 0:
                {
                    var pixel = ToPixel(local);
                    var cut = layout.cuts[dragNode];
                    layout.MoveCut(dragNode, Snap(cut.kind == HotspotCutKind.Horizontal ? pixel.y : pixel.x));
                    dragged = true;
                    EditorUtility.SetDirty(layout);
                    current.Use();
                    Repaint();
                    break;
                }
                case EventType.MouseUp when dragNode >= 0:
                {
                    dragNode = -1;
                    if (dragged)
                        layout.NotifyChanged();
                    current.Use();
                    break;
                }
                case EventType.MouseDrag when current.button == 2 || (current.button == 0 && current.alt):
                {
                    pan += current.delta;
                    current.Use();
                    Repaint();
                    break;
                }
                case EventType.ContextClick when inside:
                {
                    ShowContextMenu(local);
                    current.Use();
                    break;
                }
                case EventType.KeyDown when inside:
                    Keys(current, local);
                    break;
            }
            GUI.EndClip();
        }

        void DrawTiles()
        {
            foreach (var tile in layout.tiles)
            {
                if (tile.points == null || tile.points.Length < 3)
                    continue;
                var points = new Vector3[tile.points.Length];
                for (int i = 0; i < points.Length; i++)
                    points[i] = ToCanvas(tile.points[i]);
                if ((tile.flags & HotspotTileFlags.Disabled) != 0)
                {
                    Handles.color = kDisabled;
                    Handles.DrawAAConvexPolygon(points);
                }
                if (tile.id == selectedTile)
                {
                    Handles.color = kSelected;
                    Handles.DrawAAConvexPolygon(points);
                }
                if (!layout.HasCuts)
                {
                    var outline = new Vector3[points.Length + 1];
                    points.CopyTo(outline, 0);
                    outline[points.Length] = points[0];
                    Handles.color = kLine;
                    Handles.DrawAAPolyLine(2, outline);
                }
                if (tile.border > 0)
                {
                    var inset = Inset(tile.points, tile.border);
                    if (inset != null)
                    {
                        var drawn = new Vector3[inset.Length + 1];
                        for (int i = 0; i <= inset.Length; i++)
                            drawn[i] = ToCanvas(inset[i % inset.Length]);
                        Handles.color = kBorder;
                        Handles.DrawAAPolyLine(1.5f, drawn);
                    }
                }
                var center = Vector2.zero;
                foreach (var p in tile.points)
                    center += (Vector2)p;
                center = ToCanvas(center / tile.points.Length);
                if (Scale * 64 > 24)
                {
                    var label = new GUIContent(tile.name);
                    var labelSize = EditorStyles.whiteMiniLabel.CalcSize(label);
                    GUI.Label(new Rect(center - labelSize * 0.5f, labelSize), label, EditorStyles.whiteMiniLabel);
                }
            }
        }

        // The outline a border of this width leaves inside a tile: every edge moved inward; null when it does not fit
        static Vector2[] Inset(Vector2Int[] points, int border)
        {
            var count = points.Length;
            var area = 0.0f;
            for (int i = 0; i < count; i++)
                area += (float)points[i].x * points[(i + 1) % count].y - (float)points[(i + 1) % count].x * points[i].y;
            var inward = area > 0 ? 1 : -1;
            var result = new Vector2[count];
            for (int i = 0; i < count; i++)
            {
                Vector2 a0 = points[(i + count - 1) % count], a1 = points[i], b1 = points[(i + 1) % count];
                var n0 = Normal(a1 - a0, inward) * border;
                var n1 = Normal(b1 - a1, inward) * border;
                if (!Intersect(a0 + n0, a1 + n0, a1 + n1, b1 + n1, out result[i]))
                    return null;
            }
            for (int i = 0; i < count; i++)
            {
                Vector2 a = result[i], b = result[(i + 1) % count];
                Vector2 oa = points[i], ob = points[(i + 1) % count];
                if (Vector2.Dot(b - a, ob - oa) <= 0)
                    return null;
            }
            return result;
        }

        static Vector2 Normal(Vector2 edge, int inward) { return new Vector2(-edge.y, edge.x).normalized * inward; }

        static bool Intersect(Vector2 a0, Vector2 a1, Vector2 b0, Vector2 b1, out Vector2 point)
        {
            var da = a1 - a0; var db = b1 - b0;
            var denominator = da.x * db.y - da.y * db.x;
            point = a1;
            if (Mathf.Abs(denominator) < 1e-6f)
                return false;
            var t = ((b0.x - a0.x) * db.y - (b0.y - a0.y) * db.x) / denominator;
            point = a0 + da * t;
            return true;
        }

        void ShowContextMenu(Vector2 local)
        {
            var pixel = ToPixel(local);
            var x = Snap(pixel.x); var y = Snap(pixel.y);
            var tile = layout.TileAt(pixel);
            var menu = new GenericMenu();
            var line = LineAt(local);
            if (tile != 0 && layout.HasCuts)
            {
                selectedTile = tile;
                menu.AddItem(new GUIContent($"Split vertically at x {x}"),   false, () => Edit("Split Hotspot Tile", () => layout.Split(tile, HotspotCutKind.Vertical, x)));
                menu.AddItem(new GUIContent($"Split horizontally at y {y}"), false, () => Edit("Split Hotspot Tile", () => layout.Split(tile, HotspotCutKind.Horizontal, y)));
                menu.AddItem(new GUIContent("Split along the diagonal ╱"),  false, () => Edit("Split Hotspot Tile", () => layout.Split(tile, HotspotCutKind.Slash, 0)));
                menu.AddItem(new GUIContent("Split along the diagonal ╲"),  false, () => Edit("Split Hotspot Tile", () => layout.Split(tile, HotspotCutKind.Backslash, 0)));
                menu.AddItem(new GUIContent($"Split into three triangles, apex at x {x}"), false, () => Edit("Split Hotspot Tile", () => layout.Split(tile, HotspotCutKind.Apex, x)));
            }
            if (line >= 0)
            {
                var node = lines[line].node;
                menu.AddSeparator(string.Empty);
                menu.AddItem(new GUIContent("Remove this line"), false, () => Edit("Remove Hotspot Line", () => layout.RemoveCut(node)));
            }
            if (menu.GetItemCount() > 0)
                menu.ShowAsContext();
        }

        void Keys(Event current, Vector2 local)
        {
            if (!layout.HasCuts)
                return;
            var pixel = ToPixel(local);
            var tile = layout.TileAt(pixel);
            switch (current.keyCode)
            {
                case KeyCode.V when tile != 0:
                    Edit("Split Hotspot Tile", () => layout.Split(tile, HotspotCutKind.Vertical, Snap(pixel.x)));
                    current.Use();
                    break;
                case KeyCode.H when tile != 0:
                    Edit("Split Hotspot Tile", () => layout.Split(tile, HotspotCutKind.Horizontal, Snap(pixel.y)));
                    current.Use();
                    break;
                case KeyCode.Delete:
                case KeyCode.Backspace:
                {
                    var line = LineAt(local);
                    if (line < 0)
                        break;
                    var node = lines[line].node;
                    Edit("Remove Hotspot Line", () => layout.RemoveCut(node));
                    hoverLine = -1;
                    current.Use();
                    break;
                }
            }
        }

        void Panel()
        {
            EditorGUILayout.LabelField("Layout", EditorStyles.boldLabel);
            EditorGUI.BeginChangeCheck();
            var texture       = (Texture2D)EditorGUILayout.ObjectField("Texture", layout.texture, typeof(Texture2D), false);
            var size          = EditorGUILayout.Vector2IntField("Size in pixels", layout.size);
            var pixelsPerUnit = EditorGUILayout.FloatField("Pixels per unit", layout.pixelsPerUnit);
            if (EditorGUI.EndChangeCheck())
            {
                Edit("Change Hotspot Layout", () =>
                {
                    if (texture != layout.texture && texture != null && size == layout.size)
                        size = new Vector2Int(texture.width, texture.height);
                    layout.texture = texture;
                    layout.pixelsPerUnit = Mathf.Max(0.001f, pixelsPerUnit);
                    layout.Resize(size);
                });
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button(new GUIContent("Import…", "A Chisel layout (.json), Valve rectangles (.rect), an SVG, or a colour mask image (one flat colour per tile)")))
                    Import();
                if (GUILayout.Button("Start over") &&
                    EditorUtility.DisplayDialog("Start over", "Replace every tile with one tile over the whole texture?", "Start over", "Cancel"))
                    Edit("Reset Hotspot Layout", layout.ResetCuts);
            }
            if (!layout.HasCuts)
            {
                EditorGUILayout.HelpBox("These tiles have no lines to edit: they are not divided by straight lines across the texture.", MessageType.Warning);
                if (GUILayout.Button("Find lines from the tiles"))
                {
                    string error = null;
                    Edit("Find Hotspot Lines", () => layout.BuildCuts(out error));
                    if (error != null)
                        EditorUtility.DisplayDialog("Find lines", error, "OK");
                }
            }
            EditorGUILayout.HelpBox("Drag a line to move it. Right click a tile to split it; V or H splits it at the cursor. Delete removes the line under the cursor. Middle drag or Alt drag pans, the wheel zooms.", MessageType.None);

            EditorGUILayout.Space();
            var index = layout.IndexOfTile(selectedTile);
            if (index < 0)
            {
                EditorGUILayout.LabelField("Click a tile to edit it", EditorStyles.centeredGreyMiniLabel);
                return;
            }
            TilePanel(index);
        }

        void TilePanel(int index)
        {
            var tile = layout.tiles[index];
            EditorGUILayout.LabelField("Tile", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Id", tile.id.ToString());
            if (tile.points != null && tile.points.Length >= 3)
            {
                int x0 = int.MaxValue, y0 = int.MaxValue, x1 = int.MinValue, y1 = int.MinValue;
                foreach (var p in tile.points) { x0 = Mathf.Min(x0, p.x); y0 = Mathf.Min(y0, p.y); x1 = Mathf.Max(x1, p.x); y1 = Mathf.Max(y1, p.y); }
                var ppu = tile.pixelsPerUnit > 0 ? tile.pixelsPerUnit : layout.pixelsPerUnit;
                EditorGUILayout.LabelField(tile.points.Length == 3 ? "Triangle" : "Rectangle",
                                           $"{x1 - x0} x {y1 - y0} px, {(x1 - x0) / ppu:0.##} x {(y1 - y0) / ppu:0.##} units");
            }
            EditorGUI.BeginChangeCheck();
            tile.name = EditorGUILayout.TextField("Name", tile.name);
            tile.flags = Flag(tile.flags, HotspotTileFlags.Disabled,      "Disabled",      "Never chosen by matching; surfaces can still be pinned to it");
            tile.flags = Flag(tile.flags, HotspotTileFlags.Radial,        "Radial",        "Only for round surfaces, and round surfaces only take radial tiles");
            tile.flags = Flag(tile.flags, HotspotTileFlags.RepeatU,       "Repeats across", "Meant to repeat along its width");
            tile.flags = Flag(tile.flags, HotspotTileFlags.RepeatV,       "Repeats down",  "Meant to repeat along its height");
            tile.flags = Flag(tile.flags, HotspotTileFlags.AllowRotation, "May turn",      "Matching may turn the texture to fit; otherwise it stays upright");
            tile.flags = Flag(tile.flags, HotspotTileFlags.AllowMirror,   "May mirror",    "Matching may mirror the texture to fit");
            tile.flags = Flag(tile.flags, HotspotTileFlags.Alternate,     "Alternate",     "Only taken when a surface is pinned to it, like Valve's alt rectangles");
            tile.flags = Flag(tile.flags, HotspotTileFlags.RepeatBorder,  "Border repeats", "The border's strips repeat along the edges instead of stretching");
            tile.flags = Flag(tile.flags, HotspotTileFlags.PartialRepeats, "Repeats cut off", "Repeats keep their exact size and the last one is cut off, instead of all stretching to fit");
            tile.border = Mathf.Max(0, EditorGUILayout.IntField(new GUIContent("Border (px)", "The width of the tile's border, a band along its outline (0 for none)"), tile.border));
            tile.pixelsPerUnit = Mathf.Max(0, EditorGUILayout.FloatField(new GUIContent("Pixels per unit", "The texture density this tile is meant for; 0 takes the layout's"), tile.pixelsPerUnit));
            if (EditorGUI.EndChangeCheck())
                Edit("Change Hotspot Tile", () => layout.tiles[index] = tile);
        }

        static HotspotTileFlags Flag(HotspotTileFlags flags, HotspotTileFlags flag, string label, string tooltip)
        {
            var on = EditorGUILayout.Toggle(new GUIContent(label, tooltip), (flags & flag) != 0);
            return on ? flags | flag : flags & ~flag;
        }

        void Import()
        {
            var path = EditorUtility.OpenFilePanelWithFilters("Import hotspot layout", Application.dataPath,
                new[] { "Hotspot layouts", "json,rect,svg,png,tga", "All files", "*" });
            if (string.IsNullOrEmpty(path))
                return;
            string error = null;
            var extension = Path.GetExtension(path).ToLowerInvariant();
            if (extension == ".png" || extension == ".tga")
            {
                var mask = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                try
                {
                    if (!mask.LoadImage(File.ReadAllBytes(path)))
                        error = "the image could not be read";
                    else
                    {
                        var pixels = mask.GetPixels32();
                        Edit("Import Hotspot Layout", () => ChiselHotspotLayout.TryReadColorMask(pixels, mask.width, mask.height, layout, out error));
                    }
                }
                finally { DestroyImmediate(mask); }
            } else
            {
                var text = File.ReadAllText(path);
                var size = layout.texture != null ? new Vector2Int(layout.texture.width, layout.texture.height) : layout.size;
                Edit("Import Hotspot Layout", () =>
                {
                    switch (extension)
                    {
                        case ".rect": ChiselHotspotLayout.TryReadRect(text, size, layout, out error); break;
                        case ".svg":  ChiselHotspotLayout.TryReadSvg(text, layout, out error); break;
                        default:      ChiselHotspotLayout.TryReadJson(text, layout, out error); break;
                    }
                });
            }
            // a layout drawn at another resolution than its texture's: to the texture's
            if (error == null && layout.texture != null && layout.size != new Vector2Int(layout.texture.width, layout.texture.height))
                Edit("Import Hotspot Layout", () => layout.Resize(new Vector2Int(layout.texture.width, layout.texture.height)));
            if (error != null)
                EditorUtility.DisplayDialog("Import hotspot layout", error, "OK");
            else if (!layout.HasCuts)
                EditorUtility.DisplayDialog("Import hotspot layout", "The tiles were read, but they are not divided by straight lines across the texture, so they have no lines to edit.", "OK");
            selectedTile = 0;
        }

        // The generators in the open scenes with surfaces of this material map them again (a material's metadata
        // reaches surfaces when they are converted)
        public static void RefreshSurfacesUsing(Material material)
        {
            if (material == null)
                return;
            foreach (var generator in Object.FindObjectsByType<ChiselGeneratorComponent>(FindObjectsInactive.Include))
            {
                var surfaces = generator.SurfaceDefinition?.surfaces;
                if (surfaces == null)
                    continue;
                foreach (var surface in surfaces)
                {
                    if (surface != null && surface.RenderMaterial == material)
                    {
                        generator.OnValidate();
                        break;
                    }
                }
            }
        }
    }

    [CustomEditor(typeof(ChiselHotspotLayout))]
    public sealed class ChiselHotspotLayoutEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            var layout = (ChiselHotspotLayout)target;
            serializedObject.Update();
            EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(ChiselHotspotLayout.texture)));
            EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(ChiselHotspotLayout.size)));
            EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(ChiselHotspotLayout.pixelsPerUnit)));
            serializedObject.ApplyModifiedProperties();
            EditorGUILayout.LabelField("Tiles", $"{layout.tiles.Count} ({(layout.HasCuts ? "divided by lines" : "without lines")})");
            if (GUILayout.Button("Edit Layout"))
                ChiselHotspotLayoutWindow.Open(layout);
        }
    }
}
