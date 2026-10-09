using System.Collections.Generic;
using System.IO;
using Chisel.Components;
using Chisel.Core;
using UnityEditor;
using UnityEngine;

namespace Chisel.Editors
{
    // Draws a hotspot layout over its texture: lines are dragged, added (right click, or V / H at the cursor) and removed
    // (Delete over a line); shapes are drawn over them corner by corner, with holes; the selected tile's properties are
    // on the right.
    public sealed class ChiselHotspotLayoutWindow : EditorWindow
    {
        const float kMinPanelWidth = 200;
        const float kSplitterGrab  = 4;
        static readonly int kSplitterHash = nameof(ChiselHotspotLayoutWindow).GetHashCode();
        const float kLineGrab    = 6;
        static readonly int[]      kSnaps     = { 1, 2, 4, 8, 16, 32, 64 };
        static readonly string[]   kSnapNames = { "1 px", "2 px", "4 px", "8 px", "16 px", "32 px", "64 px" };
        static readonly Color      kLine      = new(1, 1, 1, 0.85f);
        static readonly Color      kHover     = new(1, 0.75f, 0.1f, 1);
        static readonly Color      kSelected  = new(0.2f, 0.6f, 1, 0.35f);
        static readonly Color      kDisabled  = new(0, 0, 0, 0.55f);
        static readonly Color      kBorder    = new(1, 0.3f, 0.3f, 0.9f);
        static readonly Color      kPadding   = new(0.3f, 0.9f, 1, 0.9f);

        [SerializeField] ChiselHotspotLayout layout;
        [SerializeField] int     selectedTile;
        [SerializeField] float   zoom = 1;
        [SerializeField] Vector2 pan;
        [SerializeField] int     snapIndex = 3;
        [SerializeField] float   panelWidth = 280;

        enum Drawing { None, Shape, Hole }

        readonly List<HotspotLine> lines = new();
        readonly List<Vector2Int>  drawn = new();
        Drawing drawing;
        int     hoverLine = -1;
        int     dragNode  = -1;
        bool    dragged;
        // the selected drawn shape: a corner dragged (its loop, -1 the outline), or the whole shape from a pixel
        int        dragLoop, dragCorner = -1;
        bool       dragShape;
        Vector2Int dragFrom;
        Vector2 panelScroll;
        Rect    canvas;

        [MenuItem("Window/Chisel/Hotspot Layout")]
        public static void Open() { Open(Selection.activeObject as ChiselHotspotLayout); }

        public static void Open(ChiselHotspotLayout layout) { Open(layout, null); }

        // From a material: a layout without a texture of its own takes the material's
        public static void Open(ChiselHotspotLayout layout, Material material)
        {
            if (layout != null && layout.texture == null && material != null && material.mainTexture is Texture2D texture)
                EditLayout(layout, "Hotspot Layout Texture", () => layout.texture = texture);
            var window = GetWindow<ChiselHotspotLayoutWindow>();
            window.titleContent = new GUIContent("Hotspot Layout");
            if (layout != null)
                window.SetLayout(layout);
            window.Show();
        }

        // Records the layout for undo, and afterwards tells the models it changed
        internal static void EditLayout(ChiselHotspotLayout layout, string name, System.Action edit)
        {
            Undo.RecordObject(layout, name);
            edit();
            EditorUtility.SetDirty(layout);
            layout.NotifyChanged();
        }

        void SetLayout(ChiselHotspotLayout newLayout)
        {
            if (layout == newLayout)
                return;
            layout = newLayout;
            selectedTile = 0;
            hoverLine = dragNode = dragCorner = -1;
            dragShape = false;
            StopDrawing();
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

        void Edit(string name, System.Action edit)
        {
            EditLayout(layout, name, edit);
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
            panelWidth = Mathf.Clamp(panelWidth, kMinPanelWidth, Mathf.Max(kMinPanelWidth, area.width - 64));
            canvas = new Rect(area.x, area.y, Mathf.Max(64, area.width - panelWidth), area.height);
            Splitter(new Rect(canvas.xMax - kSplitterGrab, area.y, kSplitterGrab * 2, area.height), area.width);
            Canvas();
            GUILayout.BeginArea(new Rect(canvas.xMax, area.y, area.width - canvas.width, area.height));
            panelScroll = EditorGUILayout.BeginScrollView(panelScroll);
            Panel();
            EditorGUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        // The edge between the canvas and the panel, dragged to size the panel
        void Splitter(Rect grab, float width)
        {
            var id = GUIUtility.GetControlID(kSplitterHash, FocusType.Passive);
            EditorGUIUtility.AddCursorRect(grab, MouseCursor.ResizeHorizontal);
            var current = Event.current;
            switch (current.GetTypeForControl(id))
            {
                case EventType.MouseDown when current.button == 0 && grab.Contains(current.mousePosition):
                    GUIUtility.hotControl = id;
                    current.Use();
                    break;
                case EventType.MouseDrag when GUIUtility.hotControl == id:
                    panelWidth = Mathf.Clamp(width - current.mousePosition.x, kMinPanelWidth, Mathf.Max(kMinPanelWidth, width - 64));
                    current.Use();
                    Repaint();
                    break;
                case EventType.MouseUp when GUIUtility.hotControl == id:
                    GUIUtility.hotControl = 0;
                    current.Use();
                    break;
                case EventType.Repaint:
                    EditorGUI.DrawRect(new Rect(grab.center.x - 0.5f, grab.y, 1, grab.height), new Color(0, 0, 0, 0.5f));
                    break;
            }
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
                GUILayout.Space(8);
                using (new EditorGUI.DisabledScope(layout == null))
                {
                    if (GUILayout.Toggle(drawing == Drawing.Shape, kDrawShapeContents, EditorStyles.toolbarButton, GUILayout.Width(80)) != (drawing == Drawing.Shape))
                        StartDrawing(drawing == Drawing.Shape ? Drawing.None : Drawing.Shape);
                    using (new EditorGUI.DisabledScope(drawing != Drawing.Hole && (layout == null || !layout.IsDrawn(selectedTile))))
                    {
                        if (GUILayout.Toggle(drawing == Drawing.Hole, kDrawHoleContents, EditorStyles.toolbarButton, GUILayout.Width(70)) != (drawing == Drawing.Hole))
                            StartDrawing(drawing == Drawing.Hole ? Drawing.None : Drawing.Hole);
                    }
                }
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("Fit", EditorStyles.toolbarButton, GUILayout.Width(40)))
                {
                    zoom = 1;
                    pan = Vector2.zero;
                }
            }
        }

        static readonly GUIContent kDrawShapeContents = new("Draw Shape", "Click the corners of a new tile of any shape, over the others; click its first corner or press Enter to finish, Backspace takes back a corner, Escape stops");
        static readonly GUIContent kDrawHoleContents  = new("Draw Hole", "Click the corners of a hole in the selected shape; click its first corner or press Enter to finish");

        void StartDrawing(Drawing mode)
        {
            drawing = mode;
            drawn.Clear();
            Repaint();
        }

        void StopDrawing()
        {
            drawing = Drawing.None;
            drawn.Clear();
        }

        void FinishDrawing()
        {
            if (drawn.Count < 3)
                return;
            var points = drawn.ToArray();
            if (drawing == Drawing.Shape)
            {
                var id = 0;
                Edit("Draw Hotspot Shape", () => id = layout.AddShape(points));
                if (id != 0)
                    selectedTile = id;
            } else
            {
                var tile = selectedTile;
                Edit("Draw Hotspot Hole", () => layout.AddHole(tile, points));
            }
            StopDrawing();
        }

        Vector2Int SnappedPixel(Vector2 local)
        {
            var pixel = ToPixel(local);
            return new Vector2Int(Mathf.Clamp(Snap(pixel.x), 0, layout.size.x), Mathf.Clamp(Snap(pixel.y), 0, layout.size.y));
        }

        // While drawing: clicks add corners, the first corner or Enter finishes; true when the event was the drawing's
        bool DrawingEvents(Event current, Vector2 local, bool inside)
        {
            if (drawing == Drawing.None)
                return false;
            switch (current.type)
            {
                case EventType.MouseMove:
                    Repaint();
                    return false;
                case EventType.MouseDown when inside && current.button == 0 && !current.alt:
                {
                    if (drawn.Count >= 3 && Vector2.Distance(local, ToCanvas(drawn[0])) <= kLineGrab * 1.5f)
                        FinishDrawing();
                    else
                    {
                        var corner = SnappedPixel(local);
                        if (drawn.Count == 0 || drawn[^1] != corner)
                            drawn.Add(corner);
                    }
                    current.Use();
                    Repaint();
                    return true;
                }
                case EventType.KeyDown:
                {
                    switch (current.keyCode)
                    {
                        case KeyCode.Return:
                        case KeyCode.KeypadEnter: FinishDrawing(); break;
                        case KeyCode.Escape:      StopDrawing(); break;
                        case KeyCode.Backspace:
                        case KeyCode.Delete:      if (drawn.Count > 0) drawn.RemoveAt(drawn.Count - 1); break;
                        default: return false;
                    }
                    current.Use();
                    Repaint();
                    return true;
                }
            }
            return false;
        }

        // A corner of the selected drawn shape near the mouse: its loop (-1 the outline) and index
        bool CornerAt(Vector2 local, out int loop, out int corner)
        {
            loop = -1; corner = -1;
            if (selectedTile == 0 || !layout.IsDrawn(selectedTile))
                return false;
            var best = kLineGrab * 1.5f;
            for (int l = -1; ; l++)
            {
                var points = layout.ShapeLoop(selectedTile, l);
                if (points == null)
                    break;
                for (int i = 0; i < points.Length; i++)
                {
                    var distance = Vector2.Distance(local, ToCanvas(points[i]));
                    if (distance < best) { best = distance; loop = l; corner = i; }
                }
            }
            return corner >= 0;
        }

        // An edge of the selected drawn shape near the mouse: its loop and the corner it starts at
        bool EdgeAt(Vector2 local, out int loop, out int after)
        {
            loop = -1; after = -1;
            if (selectedTile == 0 || !layout.IsDrawn(selectedTile))
                return false;
            var best = kLineGrab;
            for (int l = -1; ; l++)
            {
                var points = layout.ShapeLoop(selectedTile, l);
                if (points == null)
                    break;
                for (int i = 0; i < points.Length; i++)
                {
                    var distance = DistanceToSegment(local, ToCanvas(points[i]), ToCanvas(points[(i + 1) % points.Length]));
                    if (distance < best) { best = distance; loop = l; after = i; }
                }
            }
            return after >= 0;
        }

        // Editing the selected drawn shape: its corners are dragged, a double click on an edge adds one, and the shape is
        // dragged by its inside; true when the event was the shape's
        bool ShapeEvents(Event current, Vector2 local, bool inside)
        {
            if (drawing != Drawing.None)
                return false;
            switch (current.type)
            {
                case EventType.MouseDown when inside && current.button == 0 && !current.alt:
                {
                    if (current.clickCount == 2 && EdgeAt(local, out var edgeLoop, out var after) && !CornerAt(local, out _, out _))
                    {
                        var tile = selectedTile; var at = SnappedPixel(local);
                        Edit("Add Hotspot Shape Corner", () => layout.InsertShapePoint(tile, edgeLoop, after, at));
                        current.Use();
                        return true;
                    }
                    if (CornerAt(local, out dragLoop, out dragCorner))
                    {
                        Undo.RecordObject(layout, "Move Hotspot Shape Corner");
                        dragged = false;
                        current.Use();
                        return true;
                    }
                    if (LineAt(local) < 0 && layout.IsDrawn(selectedTile) && layout.TileAt(ToPixel(local)) == selectedTile)
                    {
                        Undo.RecordObject(layout, "Move Hotspot Shape");
                        dragShape = true;
                        dragged = false;
                        dragFrom = SnappedPixel(local);
                        GUI.FocusControl(null);
                        current.Use();
                        return true;
                    }
                    return false;
                }
                case EventType.MouseDrag when dragCorner >= 0:
                    if (layout.MoveShapePoint(selectedTile, dragLoop, dragCorner, SnappedPixel(local)))
                    {
                        dragged = true;
                        EditorUtility.SetDirty(layout);
                    }
                    current.Use();
                    Repaint();
                    return true;
                case EventType.MouseDrag when dragShape:
                {
                    var to = SnappedPixel(local);
                    if (to != dragFrom && layout.MoveShape(selectedTile, to - dragFrom))
                    {
                        dragFrom = to;
                        dragged = true;
                        EditorUtility.SetDirty(layout);
                    }
                    current.Use();
                    Repaint();
                    return true;
                }
                case EventType.MouseUp when dragCorner >= 0 || dragShape:
                    dragCorner = -1;
                    dragShape = false;
                    if (dragged)
                        layout.NotifyChanged();
                    current.Use();
                    return true;
                case EventType.KeyDown when inside && (current.keyCode == KeyCode.Delete || current.keyCode == KeyCode.Backspace):
                {
                    if (!CornerAt(local, out var loop, out var corner))
                        return false;
                    var tile = selectedTile;
                    // a hole down to its last three corners goes as a whole
                    if (loop >= 0 && layout.ShapeLoop(tile, loop).Length <= 3)
                        Edit("Remove Hotspot Shape Hole", () => layout.RemoveShapeHole(tile, loop));
                    else
                        Edit("Remove Hotspot Shape Corner", () => layout.RemoveShapePoint(tile, loop, corner));
                    current.Use();
                    return true;
                }
            }
            return false;
        }

        // The selected drawn shape's corners, to drag
        void DrawShapeHandles(Vector2 local)
        {
            if (drawing != Drawing.None || selectedTile == 0 || !layout.IsDrawn(selectedTile))
                return;
            CornerAt(local, out var hoverLoop, out var hoverCorner);
            for (int l = -1; ; l++)
            {
                var points = layout.ShapeLoop(selectedTile, l);
                if (points == null)
                    break;
                for (int i = 0; i < points.Length; i++)
                {
                    var hot = (l == hoverLoop && i == hoverCorner) || (l == dragLoop && i == dragCorner);
                    Handles.color = hot ? kHover : kLine;
                    var p = ToCanvas(points[i]);
                    Handles.DrawSolidDisc(p, Vector3.forward, hot ? 5 : 4);
                    Handles.color = new Color(0, 0, 0, 0.8f);
                    Handles.DrawWireDisc(p, Vector3.forward, hot ? 5 : 4);
                }
            }
        }

        void DrawDrawing(Vector2 local)
        {
            if (drawing == Drawing.None)
                return;
            var points = new Vector3[drawn.Count + 1];
            for (int i = 0; i < drawn.Count; i++)
                points[i] = ToCanvas(drawn[i]);
            var cursor = SnappedPixel(local);
            points[drawn.Count] = ToCanvas(cursor);
            Handles.color = kHover;
            if (points.Length > 1)
                Handles.DrawAAPolyLine(2, points);
            foreach (var p in points)
                Handles.DrawSolidDisc(p, Vector3.forward, 3);
            if (drawn.Count >= 3 && Vector2.Distance(local, ToCanvas(drawn[0])) <= kLineGrab * 1.5f)
                Handles.DrawWireDisc(points[0], Vector3.forward, kLineGrab * 1.5f);
            var label = new GUIContent($"{cursor.x}, {cursor.y}");
            var size = EditorStyles.whiteMiniLabel.CalcSize(label);
            GUI.Label(new Rect(local + new Vector2(12, 8), size), label, EditorStyles.whiteMiniLabel);
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
                DrawDrawing(local);
                DrawShapeHandles(local);
            }

            var inside = new Rect(0, 0, canvas.width, canvas.height).Contains(local);
            // a drawn shape's corners and the shape itself move
            if (drawing == Drawing.None && inside && (dragCorner >= 0 || dragShape || CornerAt(local, out _, out _) ||
                                                      (dragNode < 0 && LineAt(local) < 0 && layout.IsDrawn(selectedTile) && layout.TileAt(ToPixel(local)) == selectedTile)))
                EditorGUIUtility.AddCursorRect(new Rect(0, 0, canvas.width, canvas.height), MouseCursor.MoveArrow);
            // a line that moves shows which way it goes, under the mouse and while dragged
            else if (drawing == Drawing.None && inside)
            {
                var node = dragNode;
                if (node < 0)
                {
                    var line = LineAt(local);
                    if (line >= 0 && layout.MoveRange(lines[line].node, out _, out _))
                        node = lines[line].node;
                }
                if (node >= 0)
                    EditorGUIUtility.AddCursorRect(new Rect(0, 0, canvas.width, canvas.height),
                                                   layout.cuts[node].kind == HotspotCutKind.Horizontal ? MouseCursor.ResizeVertical : MouseCursor.ResizeHorizontal);
            }
            if (DrawingEvents(current, local, inside) || ShapeEvents(current, local, inside))
            {
                GUI.EndClip();
                return;
            }
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
            var ofCuts = layout.TilesOfCuts();
            foreach (var tile in layout.tiles)
            {
                if (tile.points == null || tile.points.Length < 3)
                    continue;
                var points = new Vector3[tile.points.Length];
                for (int i = 0; i < points.Length; i++)
                    points[i] = ToCanvas(tile.points[i]);
                var shaped = IsShaped(tile);
                if ((tile.flags & HotspotTileFlags.Disabled) != 0)
                {
                    Handles.color = kDisabled;
                    Fill(points, shaped);
                }
                if (tile.id == selectedTile)
                {
                    Handles.color = kSelected;
                    Fill(points, shaped);
                }
                // its outline where no line of the cuts draws it, and its holes
                if (!ofCuts.Contains(tile.id))
                {
                    Handles.color = kLine;
                    DrawLoop(points);
                    if (tile.holes != null)
                    {
                        foreach (var hole in tile.holes)
                        {
                            if (hole.points == null || hole.points.Length < 3)
                                continue;
                            var drawn = new Vector3[hole.points.Length];
                            for (int i = 0; i < drawn.Length; i++)
                                drawn[i] = ToCanvas(hole.points[i]);
                            DrawLoop(drawn);
                        }
                    }
                }
                // what the tile samples: its outline less the layout's padding
                if (layout.padding > 0 && !shaped)
                {
                    var padded = Inset(tile.points, layout.padding);
                    if (padded != null)
                    {
                        var drawn = new Vector3[padded.Length + 1];
                        for (int i = 0; i <= padded.Length; i++)
                            drawn[i] = ToCanvas(padded[i % padded.Length]);
                        Handles.color = kPadding;
                        Handles.DrawAAPolyLine(1.5f, drawn);
                    }
                }
                if (tile.border > 0 && !shaped)
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

        static void DrawLoop(Vector3[] points)
        {
            var outline = new Vector3[points.Length + 1];
            points.CopyTo(outline, 0);
            outline[points.Length] = points[0];
            Handles.DrawAAPolyLine(2, outline);
        }

        // Not a rectangle with upright sides or a triangle: drawn by its outline, filled triangle by triangle
        internal static bool IsShaped(in HotspotTile tile) => ChiselHotspotLayout.IsShaped(tile);

        // A polygon filled: as it is when convex, otherwise by its ears (its holes not left out)
        static void Fill(Vector3[] points, bool shaped)
        {
            if (!shaped)
            {
                Handles.DrawAAConvexPolygon(points);
                return;
            }
            var left = new List<int>();
            for (int i = 0; i < points.Length; i++)
                left.Add(i);
            var area = 0.0f;
            for (int i = 0; i < points.Length; i++)
                area += points[i].x * points[(i + 1) % points.Length].y - points[(i + 1) % points.Length].x * points[i].y;
            var turn = area > 0 ? 1 : -1;
            var guard = points.Length * points.Length;
            while (left.Count > 3 && guard-- > 0)
            {
                for (int i = 0; i < left.Count; i++)
                {
                    Vector3 a = points[left[(i + left.Count - 1) % left.Count]], b = points[left[i]], c = points[left[(i + 1) % left.Count]];
                    if (turn * ((b.x - a.x) * (c.y - a.y) - (b.y - a.y) * (c.x - a.x)) <= 0)
                        continue;
                    Handles.DrawAAConvexPolygon(a, b, c);
                    left.RemoveAt(i);
                    break;
                }
            }
            if (left.Count == 3)
                Handles.DrawAAConvexPolygon(points[left[0]], points[left[1]], points[left[2]]);
        }

        // The outline a border of this width leaves inside a tile: every edge moved inward; null when it does not fit
        static Vector2[] Inset(Vector2Int[] points, float border)
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
            if (tile != 0 && layout.HasCuts && !layout.IsDrawn(tile))
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
            menu.AddSeparator(string.Empty);
            menu.AddItem(new GUIContent("Draw a shape"), false, () => StartDrawing(Drawing.Shape));
            if (tile != 0 && layout.IsDrawn(tile))
            {
                selectedTile = tile;
                menu.AddItem(new GUIContent("Draw a hole in this shape"), false, () => StartDrawing(Drawing.Hole));
                menu.AddItem(new GUIContent("Remove this shape"), false, () => Edit("Remove Hotspot Shape", () => layout.RemoveShape(tile)));
            }
            if (menu.GetItemCount() > 0)
                menu.ShowAsContext();
        }

        void Keys(Event current, Vector2 local)
        {
            var pixel = ToPixel(local);
            var tile = layout.TileAt(pixel);
            var drawnTile = tile != 0 && layout.IsDrawn(tile);
            switch (current.keyCode)
            {
                case KeyCode.V when tile != 0 && !drawnTile && layout.HasCuts:
                    Edit("Split Hotspot Tile", () => layout.Split(tile, HotspotCutKind.Vertical, Snap(pixel.x)));
                    current.Use();
                    break;
                case KeyCode.H when tile != 0 && !drawnTile && layout.HasCuts:
                    Edit("Split Hotspot Tile", () => layout.Split(tile, HotspotCutKind.Horizontal, Snap(pixel.y)));
                    current.Use();
                    break;
                case KeyCode.Delete:
                case KeyCode.Backspace:
                {
                    var line = LineAt(local);
                    if (line < 0)
                    {
                        if (!drawnTile)
                            break;
                        Edit("Remove Hotspot Shape", () => layout.RemoveShape(tile));
                        current.Use();
                        break;
                    }
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
            if (LayoutFields(layout, withPadding: true))
                Repaint();
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
            EditorGUILayout.HelpBox("Drag a line to move it. Right click a tile to split it; V or H splits it at the cursor. Delete removes the line, or the drawn shape, under the cursor. Draw Shape adds a tile of any shape over the others, Draw Hole a hole in the selected shape. A selected shape's corners drag; a double click on its edge adds one, Delete over one removes it, and dragging inside it moves the shape. Middle drag or Alt drag pans, the wheel zooms.", MessageType.None);

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
            EditorGUILayout.LabelField("Tile", EditorStyles.boldLabel);
            if (TileFields(layout, index))
                Repaint();
        }

        static readonly GUIContent kPixelsPerUnitContents = new("Pixels per unit", "How many of the texture's pixels cover one unit of a surface (one unit in the scene): a 1024 pixel tile at 256 covers 4 units. Matching, the border's width and the repeats go by it; a tile can have its own");

        // The layout's own settings, as the window and the inspectors show them; true when they changed
        static readonly GUIContent kPaddingContents = new("Padding", "Pixels left out along every tile's edges, so a filtered (or mipmapped) texture never shows a neighbouring tile: a gutter. A tile's border keeps its inner edge and narrows by as much");

        // padding only in the window, beside the tiles it changes
        internal static bool LayoutFields(ChiselHotspotLayout layout, bool withPadding = false)
        {
            EditorGUI.BeginChangeCheck();
            var texture       = (Texture2D)EditorGUILayout.ObjectField("Texture", layout.texture, typeof(Texture2D), false);
            // the tiles' pixels: the texture's, or given when there is none
            var size          = layout.texture != null ? layout.size : EditorGUILayout.Vector2IntField("Size in pixels", layout.size);
            var pixelsPerUnit = EditorGUILayout.FloatField(kPixelsPerUnitContents, layout.pixelsPerUnit);
            var padding       = withPadding ? EditorGUILayout.FloatField(kPaddingContents, layout.padding) : layout.padding;
            if (!EditorGUI.EndChangeCheck())
                return false;
            EditLayout(layout, "Change Hotspot Layout", () =>
            {
                if (texture != layout.texture && texture != null)
                    size = new Vector2Int(texture.width, texture.height);
                layout.texture = texture;
                layout.pixelsPerUnit = Mathf.Max(0.001f, pixelsPerUnit);
                layout.padding = Mathf.Max(0, padding);
                layout.Resize(size);
            });
            return true;
        }

        // A tile's settings, as the window and the inspectors show them; true when they changed
        internal static bool TileFields(ChiselHotspotLayout layout, int index)
        {
            var tile = layout.tiles[index];
            EditorGUILayout.LabelField("Id", tile.id.ToString());
            if (tile.points != null && tile.points.Length >= 3)
            {
                int x0 = int.MaxValue, y0 = int.MaxValue, x1 = int.MinValue, y1 = int.MinValue;
                foreach (var p in tile.points) { x0 = Mathf.Min(x0, p.x); y0 = Mathf.Min(y0, p.y); x1 = Mathf.Max(x1, p.x); y1 = Mathf.Max(y1, p.y); }
                var ppu = tile.pixelsPerUnit > 0 ? tile.pixelsPerUnit : layout.pixelsPerUnit;
                var kind = IsShaped(tile) ? $"Shape ({tile.points.Length} corners" + (tile.holes != null && tile.holes.Length > 0 ? $", {tile.holes.Length} holes)" : ")")
                                          : (tile.points.Length == 3 ? "Triangle" : "Rectangle");
                EditorGUILayout.LabelField(kind, $"{x1 - x0} x {y1 - y0} px, {(x1 - x0) / ppu:0.##} x {(y1 - y0) / ppu:0.##} units");
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
            if (tile.border > 0 && tile.points != null && tile.points.Length >= 3 && !IsShaped(tile))
                SideFields(ref tile);
            if (!EditorGUI.EndChangeCheck())
                return false;
            EditLayout(layout, "Change Hotspot Tile", () => layout.tiles[index] = tile);
            return true;
        }

        static readonly GUIContent kSideModeContents    = new("", "How its border strip takes a surface's edge: as the tile's flags say, stretched, repeated (stretched to fit), or repeated at its exact size with the last one cut off");
        static readonly GUIContent kSideStretchContents = new("Max stretch", "Stretching: the most the strip may stretch (2: twice its length in the tile) before it repeats instead, the fewest times that keep within it; 0 for no limit");

        // The border along each side of a tile, each named by where it faces in the texture
        static void SideFields(ref HotspotTile tile)
        {
            var count = tile.points.Length;
            var sides = tile.sides != null && tile.sides.Length == count ? (HotspotBorderSide[])tile.sides.Clone() : new HotspotBorderSide[count];
            var centre = Vector2.zero;
            foreach (var point in tile.points)
                centre += point;
            centre /= count;
            EditorGUILayout.LabelField("Border along each side");
            EditorGUI.indentLevel++;
            var changed = false;
            for (int i = 0; i < count; i++)
            {
                // the texture's y goes down
                var middle = ((Vector2)tile.points[i] + tile.points[(i + 1) % count]) * 0.5f - centre;
                var name = Mathf.Abs(middle.x) > Mathf.Abs(middle.y) ? (middle.x > 0 ? "Right" : "Left") : (middle.y > 0 ? "Bottom" : "Top");
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUI.BeginChangeCheck();
                    var mode = (HotspotBorderMode)EditorGUILayout.EnumPopup(new GUIContent(name, kSideModeContents.tooltip), sides[i].mode);
                    float maxStretch;
                    using (new EditorGUI.DisabledScope(mode != HotspotBorderMode.Stretch && mode != HotspotBorderMode.Tile))
                        maxStretch = Mathf.Max(0, EditorGUILayout.FloatField(kSideStretchContents, sides[i].maxStretch));
                    if (EditorGUI.EndChangeCheck())
                    {
                        sides[i].mode = mode;
                        sides[i].maxStretch = maxStretch;
                        changed = true;
                    }
                }
            }
            EditorGUI.indentLevel--;
            if (changed)
                tile.sides = sides;
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

    // The layout's own settings (its tiles are edited in the window)
    [CustomEditor(typeof(ChiselHotspotLayout))]
    public sealed class ChiselHotspotLayoutEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            var layout = (ChiselHotspotLayout)target;
            ChiselHotspotLayoutWindow.LayoutFields(layout);
            if (GUILayout.Button("Edit Layout"))
                ChiselHotspotLayoutWindow.Open(layout);
        }
    }
}
