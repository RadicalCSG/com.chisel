using System.Collections.Generic;
using System.IO;
using Chisel.Components;
using Chisel.Core;
using UnityEditor;
using UnityEngine;

namespace Chisel.Editors
{
    // Draws a hotspot layout over its texture, its tiles listed on the left and the selected one's settings on the right.
    // Tools make the mouse's work visible: Select (drag lines, edit a drawn shape, click selects), Split (click splits at
    // the dashed line), Draw shape and Draw hole (click corners). In every tool lines are also split from the right click
    // menu or with V / H at the cursor, Delete removes the line or drawn shape under the cursor, middle or Alt drag pans
    // and the wheel zooms.
    public sealed partial class ChiselHotspotLayoutWindow : EditorWindow
    {
        const float kMinPanelWidth = 220;
        const float kListWidth     = 220;
        const float kSplitterGrab  = 4;
        const float kStatusHeight  = 20;
        const float kLegendHeight  = 20;
        static readonly int kSplitterHash = nameof(ChiselHotspotLayoutWindow).GetHashCode();
        const float kLineGrab    = 6;
        static readonly int[]      kSnaps     = { 1, 2, 4, 8, 16, 32, 64 };
        static readonly string[]   kSnapNames = { "1 px", "2 px", "4 px", "8 px", "16 px", "32 px", "64 px" };
        static readonly Color      kLine      = new(1, 1, 1, 0.85f);
        static readonly Color      kHover     = new(1, 0.75f, 0.1f, 1);
        static readonly Color      kDisabled  = new(0, 0, 0, 0.55f);
        static readonly Color      kBorder    = new(1, 0.3f, 0.3f, 0.9f);
        static readonly Color      kPadding   = new(0.3f, 0.9f, 1, 0.9f);
        static readonly Color      kBackground = new(0.16f, 0.16f, 0.16f);
        const float kKindAlpha = 0.8f;

        // the mouse's work on the canvas
        internal enum Tool { Select, Split, DrawShape, DrawHole }
        // what the canvas shows the tiles as: coloured by kind, or the texture as it is
        internal enum View { Kinds, Texture }

        [SerializeField] ChiselHotspotLayout layout;
        [SerializeField] int     selectedTile;
        [SerializeField] float   zoom = 1;
        [SerializeField] Vector2 pan;
        [SerializeField] int     snapIndex = 3;
        [SerializeField] float   panelWidth = 300;
        [SerializeField] View    view = View.Kinds;
        [SerializeField] bool    showNames = true, showSizes, showBorders = true, showPadding;
        // the kind of split the Split tool makes (Shift swaps vertical and horizontal)
        [SerializeField] HotspotCutKind splitKind = HotspotCutKind.Vertical;

        Tool tool;
        readonly List<HotspotLine> lines = new();
        readonly List<Vector2Int>  drawn = new();
        int     hoverLine = -1;
        int     dragNode  = -1;
        // the selected drawn shape: a corner dragged (its loop, -1 the outline), or the whole shape from a pixel
        int        dragLoop, dragCorner = -1;
        bool       dragShape;
        Vector2Int dragFrom;
        LayoutDrag drag;
        Vector2 panelScroll;
        Rect    canvas;
        // where the mouse is on the canvas, in pixels (null when it isn't)
        Vector2? cursorPixel;
        GUIStyle tagStyle;

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

        // A drag of a line, a shape or one of its corners: one undo step, recorded at every move (a record only keeps
        // what changes before its event ends), and the models told once, when it ends
        internal struct LayoutDrag
        {
            ChiselHotspotLayout layout;
            int                 undoGroup;
            bool                moved;

            public static LayoutDrag Begin(ChiselHotspotLayout layout)
            {
                return new LayoutDrag { layout = layout, undoGroup = Undo.GetCurrentGroup() };
            }

            public bool Move(string name, System.Func<bool> move)
            {
                Undo.RecordObject(layout, name);
                if (!move())
                    return false;
                moved = true;
                EditorUtility.SetDirty(layout);
                return true;
            }

            public void End()
            {
                if (layout == null)
                    return;
                Undo.CollapseUndoOperations(undoGroup);
                if (moved)
                    layout.NotifyChanged();
                layout = null;
            }
        }

        // Records the layout for undo, and afterwards tells the models it changed
        internal static void EditLayout(ChiselHotspotLayout layout, string name, System.Action edit)
        {
            Undo.RecordObject(layout, name);
            edit();
            EditorUtility.SetDirty(layout);
            layout.NotifyChanged();
        }

        internal ChiselHotspotLayout Layout => layout;
        internal int SelectedTile { get => selectedTile; set { if (selectedTile != value) { selectedTile = value; revealSelected = true; Repaint(); } } }
        internal Tool CurrentTool => tool;

        internal void SetLayout(ChiselHotspotLayout newLayout)
        {
            if (layout == newLayout)
                return;
            layout = newLayout;
            selectedTile = 0;
            hoverLine = dragNode = dragCorner = -1;
            dragShape = false;
            SetTool(Tool.Select);
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
            // a layout without lines has nothing to split
            if (tool == Tool.Split && !layout.HasCuts)
                SetTool(Tool.Select);
            var top = EditorStyles.toolbar.fixedHeight > 0 ? EditorStyles.toolbar.fixedHeight : EditorGUIUtility.singleLineHeight + 4;
            var area = new Rect(0, top, position.width, position.height - top);
            var listWidth = Mathf.Min(kListWidth, Mathf.Max(0, area.width - 2 * 64));
            panelWidth = Mathf.Clamp(panelWidth, kMinPanelWidth, Mathf.Max(kMinPanelWidth, area.width - listWidth - 64));
            var middle = new Rect(area.x + listWidth, area.y, Mathf.Max(64, area.width - listWidth - panelWidth), area.height);
            var legend = view == View.Kinds ? kLegendHeight : 0;
            canvas = new Rect(middle.x, middle.y, middle.width, Mathf.Max(32, middle.height - legend - kStatusHeight));

            TileList(new Rect(area.x, area.y, listWidth, area.height));
            Splitter(new Rect(middle.xMax - kSplitterGrab, area.y, kSplitterGrab * 2, area.height), area.width);
            Canvas();
            if (legend > 0)
                Legend(new Rect(middle.x, canvas.yMax, middle.width, legend));
            StatusBar(new Rect(middle.x, middle.yMax - kStatusHeight, middle.width, kStatusHeight));

            GUILayout.BeginArea(new Rect(middle.xMax, area.y, area.xMax - middle.xMax, area.height));
            // scrolls down only: the panel's width is the area's
            panelScroll = EditorGUILayout.BeginScrollView(panelScroll, false, false, GUIStyle.none, GUI.skin.verticalScrollbar, GUIStyle.none);
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
                    panelWidth = Mathf.Clamp(width - current.mousePosition.x, kMinPanelWidth, Mathf.Max(kMinPanelWidth, width - kListWidth - 64));
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

        static readonly GUIContent kSelectContents    = new("Select", HintOf(Tool.Select));
        static readonly GUIContent kSplitContents     = new("Split", HintOf(Tool.Split));
        internal static readonly GUIContent kSplitDisabled = new("Split", "This layout has no lines to split: its tiles were imported without them");
        static readonly GUIContent kDrawShapeContents = new("Draw shape", HintOf(Tool.DrawShape));
        static readonly GUIContent kDrawHoleContents  = new("Draw hole", HintOf(Tool.DrawHole));
        static readonly GUIContent kDrawHoleDisabled  = new("Draw hole", "Select a drawn shape first: holes go in drawn shapes");
        static readonly (HotspotCutKind kind, string name)[] kSplitKinds =
        {
            (HotspotCutKind.Vertical, "Vertical"), (HotspotCutKind.Horizontal, "Horizontal"),
            (HotspotCutKind.Slash, "Diagonal /"), (HotspotCutKind.Backslash, "Diagonal \\"), (HotspotCutKind.Apex, "Apex"),
        };
        static readonly GUIContent kKindsContents     = new("Kinds", "The tiles coloured by the kind of surface they're for");
        static readonly GUIContent kTextureContents   = new("Texture", "The texture as it is, with plain outlines");
        static readonly GUIContent kNamesContents     = new("Names", "Each tile's name, where it fits");
        static readonly GUIContent kSizesContents     = new("Sizes", "Each tile's aspect and size in pixels, where it fits");
        static readonly GUIContent kBordersContents   = new("Borders", "The inner edge of each tile's border");
        static readonly GUIContent kPaddingContents2  = new("Padding", "What each tile samples, less the layout's padding");
        static readonly GUIContent kFitContents       = new("Fit", "Show the whole texture");
        static readonly GUIContent kOneToOneContents  = new("1:1", "One pixel of the screen for each pixel of the texture");
        static readonly GUIContent kImportContents    = new("Import...", "A Chisel layout (.json), Valve rectangles (.rect), an SVG, or a colour mask image (one flat colour per tile)");
        static readonly GUIContent kMoreContents      = new("More", kResetName.TrimEnd('.'));

        void Toolbar()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                var chosen = (ChiselHotspotLayout)EditorGUILayout.ObjectField(layout, typeof(ChiselHotspotLayout), false, GUILayout.Width(200));
                if (chosen != layout)
                    SetLayout(chosen);
                GUILayout.Space(6);
                using (new EditorGUI.DisabledScope(layout == null))
                {
                    ToolButton(Tool.Select, kSelectContents);
                    var canSplit = layout != null && layout.HasCuts;
                    using (new EditorGUI.DisabledScope(!canSplit))
                        ToolButton(Tool.Split, canSplit ? kSplitContents : kSplitDisabled);
                    ToolButton(Tool.DrawShape, kDrawShapeContents);
                    var canHole = tool == Tool.DrawHole || (layout != null && layout.IsDrawn(selectedTile));
                    using (new EditorGUI.DisabledScope(!canHole))
                        ToolButton(Tool.DrawHole, canHole ? kDrawHoleContents : kDrawHoleDisabled);
                    if (tool == Tool.Split)
                    {
                        GUILayout.Space(4);
                        if (EditorGUILayout.DropdownButton(new GUIContent(SplitKindName(splitKind), "The kind of split; Shift swaps vertical and horizontal"),
                                                           FocusType.Passive, EditorStyles.toolbarDropDown, GUILayout.Width(90)))
                            SplitKindMenu();
                    }
                    GUILayout.Space(6);
                    GUILayout.Label("Snap", EditorStyles.miniLabel, GUILayout.ExpandWidth(false));
                    snapIndex = EditorGUILayout.Popup(snapIndex, kSnapNames, EditorStyles.toolbarPopup, GUILayout.Width(52));
                    GUILayout.Space(6);
                    if (GUILayout.Toggle(view == View.Texture, kTextureContents, EditorStyles.toolbarButton)) view = View.Texture;
                    if (GUILayout.Toggle(view == View.Kinds,   kKindsContents,   EditorStyles.toolbarButton)) view = View.Kinds;
                    GUILayout.Space(6);
                    showNames   = GUILayout.Toggle(showNames,   kNamesContents,    EditorStyles.toolbarButton);
                    showSizes   = GUILayout.Toggle(showSizes,   kSizesContents,    EditorStyles.toolbarButton);
                    showBorders = GUILayout.Toggle(showBorders, kBordersContents,  EditorStyles.toolbarButton);
                    showPadding = GUILayout.Toggle(showPadding, kPaddingContents2, EditorStyles.toolbarButton);
                    GUILayout.FlexibleSpace();
                    if (GUILayout.Toggle(IsFitted, kFitContents, EditorStyles.toolbarButton) && !IsFitted)
                        Fit();
                    if (GUILayout.Toggle(layout != null && IsOneToOne, kOneToOneContents, EditorStyles.toolbarButton) && layout != null && !IsOneToOne)
                        OneToOne();
                    GUILayout.Space(6);
                    if (GUILayout.Button(kImportContents, EditorStyles.toolbarButton))
                        Import();
                    if (EditorGUILayout.DropdownButton(kMoreContents, FocusType.Passive, EditorStyles.toolbarDropDown))
                        MoreMenu();
                }
            }
        }

        void ToolButton(Tool which, GUIContent content)
        {
            if (GUILayout.Toggle(tool == which, content, EditorStyles.toolbarButton) && tool != which)
                SetTool(which);
        }

        internal static string SplitKindName(HotspotCutKind kind)
        {
            foreach (var (k, name) in kSplitKinds)
                if (k == kind) return name;
            return kind.ToString();
        }

        internal HotspotCutKind SplitKind { get => splitKind; set => splitKind = value; }

        void SplitKindMenu()
        {
            var menu = new GenericMenu();
            foreach (var (kind, name) in kSplitKinds)
            {
                var chosen = kind;
                menu.AddItem(new GUIContent(name), splitKind == kind, () => { splitKind = chosen; Repaint(); });
            }
            menu.ShowAsContext();
        }

        // The kind of split a click makes: Shift swaps vertical and horizontal
        internal static HotspotCutKind SplitKindWith(HotspotCutKind kind, bool shift)
        {
            if (!shift) return kind;
            if (kind == HotspotCutKind.Vertical)   return HotspotCutKind.Horizontal;
            if (kind == HotspotCutKind.Horizontal) return HotspotCutKind.Vertical;
            return kind;
        }

        // The More menu's command, as it names it
        internal const string kResetName = "Reset to One Tile...";

        void MoreMenu()
        {
            var menu = new GenericMenu();
            menu.AddItem(new GUIContent(kResetName), false, () =>
            {
                if (EditorUtility.DisplayDialog("Reset to One Tile", "Remove every tile, line and drawn shape, leaving one tile over the whole texture?", "Reset", "Cancel"))
                    Edit("Reset Hotspot Layout", layout.ResetCuts);
            });
            menu.ShowAsContext();
        }

        // Switches the mouse's work; a shape being drawn is dropped
        internal void SetTool(Tool which)
        {
            if (which == Tool.DrawHole && (layout == null || !layout.IsDrawn(selectedTile)))
                which = Tool.Select;
            if (which == Tool.Split && (layout == null || !layout.HasCuts))
                which = Tool.Select;
            tool = which;
            drawn.Clear();
            Repaint();
        }

        bool Drawing => tool == Tool.DrawShape || tool == Tool.DrawHole;

        void FinishDrawing()
        {
            if (drawn.Count < 3)
                return;
            var points = drawn.ToArray();
            if (tool == Tool.DrawShape)
            {
                var id = 0;
                Edit("Draw Hotspot Shape", () => id = layout.AddShape(points));
                if (id != 0)
                    SelectedTile = id;
            } else
            {
                var tile = selectedTile;
                Edit("Draw Hotspot Hole", () => layout.AddHole(tile, points));
            }
            SetTool(Tool.Select);
        }

        Vector2Int SnappedPixel(Vector2 local)
        {
            var pixel = ToPixel(local);
            return new Vector2Int(Mathf.Clamp(Snap(pixel.x), 0, layout.size.x), Mathf.Clamp(Snap(pixel.y), 0, layout.size.y));
        }

        // While drawing: clicks add corners, the first corner or Enter finishes; true when the event was the drawing's
        bool DrawingEvents(Event current, Vector2 local, bool inside)
        {
            if (!Drawing)
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
                        case KeyCode.Escape:      SetTool(Tool.Select); break;
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
            if (tool != Tool.Select)
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
                        drag = LayoutDrag.Begin(layout);
                        current.Use();
                        return true;
                    }
                    if (LineAt(local) < 0 && layout.IsDrawn(selectedTile) && layout.TileAt(ToPixel(local)) == selectedTile)
                    {
                        drag = LayoutDrag.Begin(layout);
                        dragShape = true;
                        dragFrom = SnappedPixel(local);
                        GUI.FocusControl(null);
                        current.Use();
                        return true;
                    }
                    return false;
                }
                case EventType.MouseDrag when dragCorner >= 0:
                {
                    var tile = selectedTile; var loop = dragLoop; var corner = dragCorner; var to = SnappedPixel(local);
                    drag.Move("Move Hotspot Shape Corner", () => layout.MoveShapePoint(tile, loop, corner, to));
                    current.Use();
                    Repaint();
                    return true;
                }
                case EventType.MouseDrag when dragShape:
                {
                    var tile = selectedTile; var to = SnappedPixel(local); var by = to - dragFrom;
                    if (to != dragFrom && drag.Move("Move Hotspot Shape", () => layout.MoveShape(tile, by)))
                        dragFrom = to;
                    current.Use();
                    Repaint();
                    return true;
                }
                case EventType.MouseUp when dragCorner >= 0 || dragShape:
                    dragCorner = -1;
                    dragShape = false;
                    drag.End();
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

        // The split of a kind the Split tool would make at a pixel: the tile, and where (x for a vertical line or an apex,
        // y for a horizontal line, none for a diagonal); false where a click splits nothing (a drawn shape, a triangle,
        // outside the tiles, or on a tile's edge)
        internal static bool SplitAt(ChiselHotspotLayout layout, Vector2 pixel, HotspotCutKind kind, int snap, out int tile, out int position)
        {
            tile = layout != null && layout.HasCuts ? layout.TileAt(pixel) : 0;
            position = 0;
            if (kind == HotspotCutKind.Vertical || kind == HotspotCutKind.Horizontal || kind == HotspotCutKind.Apex)
            {
                var value = kind == HotspotCutKind.Horizontal ? pixel.y : pixel.x;
                position = snap > 1 ? Mathf.RoundToInt(value / snap) * snap : Mathf.RoundToInt(value);
            }
            return tile != 0 && !layout.IsDrawn(tile) && layout.CanSplit(tile, kind, position);
        }

        // The lines a split of a kind would add to a tile's bounds (min, max in pixels), as the layout draws them
        internal static List<(Vector2 a, Vector2 b)> SplitLines(HotspotCutKind kind, int position, Vector2Int min, Vector2Int max)
        {
            var lines = new List<(Vector2, Vector2)>();
            switch (kind)
            {
                case HotspotCutKind.Vertical:   lines.Add((new Vector2(position, min.y), new Vector2(position, max.y))); break;
                case HotspotCutKind.Horizontal: lines.Add((new Vector2(min.x, position), new Vector2(max.x, position))); break;
                case HotspotCutKind.Slash:      lines.Add((new Vector2(min.x, max.y), new Vector2(max.x, min.y))); break;
                case HotspotCutKind.Backslash:  lines.Add((new Vector2(min.x, min.y), new Vector2(max.x, max.y))); break;
                case HotspotCutKind.Apex:
                    lines.Add((new Vector2(min.x, max.y), new Vector2(position, min.y)));
                    lines.Add((new Vector2(position, min.y), new Vector2(max.x, max.y)));
                    break;
            }
            return lines;
        }

        // The Split tool: a click splits at the dashed line; a line under the mouse still drags. True when it was its
        bool SplitEvents(Event current, Vector2 local, bool inside)
        {
            if (tool != Tool.Split)
                return false;
            switch (current.type)
            {
                case EventType.MouseDown when inside && current.button == 0 && !current.alt:
                {
                    var line = LineAt(local);
                    if (line >= 0 && layout.MoveRange(lines[line].node, out _, out _))
                        return false;
                    var kind = SplitKindWith(splitKind, current.shift);
                    if (SplitAt(layout, ToPixel(local), kind, kSnaps[Mathf.Clamp(snapIndex, 0, kSnaps.Length - 1)], out var tile, out var position))
                    {
                        Edit("Split Hotspot Tile", () => layout.Split(tile, kind, position));
                        SelectedTile = tile;
                    }
                    current.Use();
                    return true;
                }
                case EventType.MouseMove:
                case EventType.KeyDown when current.keyCode == KeyCode.LeftShift || current.keyCode == KeyCode.RightShift:
                case EventType.KeyUp   when current.keyCode == KeyCode.LeftShift || current.keyCode == KeyCode.RightShift:
                    Repaint();
                    return false;
            }
            return false;
        }

        // The dashed lines the Split tool splits along, with the pixel position of a line or an apex
        void DrawSplitPreview(Vector2 local, bool shift)
        {
            if (tool != Tool.Split || dragNode >= 0 || !new Rect(0, 0, canvas.width, canvas.height).Contains(local))
                return;
            var line = LineAt(local);
            if (line >= 0 && layout.MoveRange(lines[line].node, out _, out _))
                return;
            var kind = SplitKindWith(splitKind, shift);
            if (!SplitAt(layout, ToPixel(local), kind, kSnaps[Mathf.Clamp(snapIndex, 0, kSnaps.Length - 1)], out var tile, out var position))
                return;
            var index = layout.IndexOfTile(tile);
            var min = layout.tiles[index].points[0]; var max = min;
            foreach (var p in layout.tiles[index].points) { min = Vector2Int.Min(min, p); max = Vector2Int.Max(max, p); }
            Handles.color = kHover;
            foreach (var (a, b) in SplitLines(kind, position, min, max))
                Handles.DrawDottedLine(ToCanvas(a), ToCanvas(b), 4);
            if (kind == HotspotCutKind.Horizontal)
                Tag(ToCanvas(new Vector2((min.x + max.x) * 0.5f, position)) + new Vector2(0, -10), "y " + position, 1);
            else if (kind == HotspotCutKind.Vertical || kind == HotspotCutKind.Apex)
                Tag(ToCanvas(new Vector2(position, kind == HotspotCutKind.Apex ? min.y : (min.y + max.y) * 0.5f)) + new Vector2(28, kind == HotspotCutKind.Apex ? 10 : 0), "x " + position, 1);
        }

        // The selected drawn shape's corners, to drag
        void DrawShapeHandles(Vector2 local)
        {
            if (tool != Tool.Select || selectedTile == 0 || !layout.IsDrawn(selectedTile))
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
            if (!Drawing)
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
        float FitScale => Mathf.Min(canvas.width / Mathf.Max(1, layout.size.x), canvas.height / Mathf.Max(1, layout.size.y)) * 0.95f;
        float Scale => FitScale * zoom;
        Vector2 Origin => new Vector2(canvas.width, canvas.height) * 0.5f - (Vector2)layout.size * (Scale * 0.5f) + pan;
        Vector2 ToCanvas(Vector2 pixel) => Origin + pixel * Scale;
        Vector2 ToPixel(Vector2 local)  => (local - Origin) / Scale;
        int Snap(float value) { var snap = kSnaps[Mathf.Clamp(snapIndex, 0, kSnaps.Length - 1)]; return Mathf.RoundToInt(value / snap) * snap; }

        bool IsFitted => Mathf.Approximately(zoom, 1) && pan == Vector2.zero;
        bool IsOneToOne => canvas.width > 0 && Mathf.Abs(Scale - 1) < 1e-3f;

        void Fit()
        {
            zoom = 1;
            pan = Vector2.zero;
            Repaint();
        }

        // One pixel of the screen for one of the texture, the pixel in the middle of the canvas kept there
        void OneToOne()
        {
            var middle = new Vector2(canvas.width, canvas.height) * 0.5f;
            var pixel = ToPixel(middle);
            zoom = 1 / Mathf.Max(1e-6f, FitScale);
            pan += middle - ToCanvas(pixel);
            Repaint();
        }

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
            var inside = new Rect(0, 0, canvas.width, canvas.height).Contains(local);

            if (current.type == EventType.MouseMove || current.type == EventType.MouseDrag)
            {
                var pixel = inside ? (Vector2?)ToPixel(local) : null;
                if (pixel != cursorPixel)
                {
                    cursorPixel = pixel;
                    Repaint();
                }
            }

            if (current.type == EventType.Repaint)
            {
                EditorGUI.DrawRect(new Rect(0, 0, canvas.width, canvas.height), kBackground);
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
                DrawSelected();
                DrawLabels();
                DrawSplitPreview(local, current.shift);
                DrawDrawing(local);
                DrawShapeHandles(local);
            }

            // a drawn shape's corners and the shape itself move
            if (tool == Tool.Select && inside && (dragCorner >= 0 || dragShape || CornerAt(local, out _, out _) ||
                                                  (dragNode < 0 && LineAt(local) < 0 && layout.IsDrawn(selectedTile) && layout.TileAt(ToPixel(local)) == selectedTile)))
                EditorGUIUtility.AddCursorRect(new Rect(0, 0, canvas.width, canvas.height), MouseCursor.MoveArrow);
            // a line that moves shows which way it goes, under the mouse and while dragged
            else if (!Drawing && inside)
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
            if (DrawingEvents(current, local, inside) || ShapeEvents(current, local, inside) || SplitEvents(current, local, inside))
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
                    zoom = Mathf.Clamp(zoom * (current.delta.y > 0 ? 1 / 1.15f : 1.15f), 0.25f, 64);
                    pan += local - ToCanvas(pixel);
                    current.Use();
                    break;
                }
                case EventType.MouseDown when inside && current.button == 0 && !current.alt && !Drawing:
                {
                    hoverLine = LineAt(local);
                    listFocused = false;
                    if (hoverLine >= 0 && layout.MoveRange(lines[hoverLine].node, out _, out _))
                    {
                        dragNode = lines[hoverLine].node;
                        drag = LayoutDrag.Begin(layout);
                    } else if (tool == Tool.Select)
                        SelectedTile = layout.TileAt(ToPixel(local));
                    current.Use();
                    GUI.FocusControl(null);
                    Repaint();
                    break;
                }
                case EventType.MouseDrag when dragNode >= 0:
                {
                    var pixel = ToPixel(local);
                    var node = dragNode;
                    var to = Snap(layout.cuts[node].kind == HotspotCutKind.Horizontal ? pixel.y : pixel.x);
                    drag.Move("Move Hotspot Line", () => layout.MoveCut(node, to));
                    current.Use();
                    Repaint();
                    break;
                }
                case EventType.MouseUp when dragNode >= 0:
                {
                    dragNode = -1;
                    drag.End();
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

        Vector3[] CanvasPoints(Vector2Int[] points)
        {
            var result = new Vector3[points.Length];
            for (int i = 0; i < points.Length; i++)
                result[i] = ToCanvas(points[i]);
            return result;
        }

        void DrawTiles()
        {
            var ofCuts = layout.TilesOfCuts();
            foreach (var tile in layout.tiles)
            {
                if (tile.points == null || tile.points.Length < 3)
                    continue;
                var points = CanvasPoints(tile.points);
                var shaped = IsShaped(tile);
                if (view == View.Kinds)
                {
                    var color = HotspotTileKinds.ColorOf(HotspotTileKinds.KindOf(tile));
                    color.a = kKindAlpha;
                    Handles.color = color;
                    Fill(points, shaped);
                }
                if ((tile.flags & HotspotTileFlags.Disabled) != 0)
                {
                    Handles.color = kDisabled;
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
                            DrawLoop(CanvasPoints(hole.points));
                        }
                    }
                }
                // what the tile samples: its outline less the layout's padding
                if (showPadding && layout.padding > 0 && !shaped)
                    DrawInset(tile.points, layout.padding, kPadding);
                if (showBorders && tile.border > 0 && !shaped)
                    DrawInset(tile.points, tile.border, kBorder);
            }
        }

        void DrawInset(Vector2Int[] points, float by, Color color)
        {
            var inset = Inset(points, by);
            if (inset == null)
                return;
            var drawn = new Vector3[inset.Length + 1];
            for (int i = 0; i <= inset.Length; i++)
                drawn[i] = ToCanvas(inset[i % inset.Length]);
            Handles.color = color;
            Handles.DrawAAPolyLine(1.5f, drawn);
        }

        // The selected tile outlined, with a handle on the middle of each of its sides
        void DrawSelected()
        {
            var index = layout.IndexOfTile(selectedTile);
            if (index < 0 || layout.tiles[index].points == null || layout.tiles[index].points.Length < 3)
                return;
            var points = CanvasPoints(layout.tiles[index].points);
            Handles.color = kHover;
            var loop = new Vector3[points.Length + 1];
            points.CopyTo(loop, 0);
            loop[points.Length] = points[0];
            Handles.DrawAAPolyLine(3, loop);
            if (IsShaped(layout.tiles[index]))
                return;
            for (int i = 0; i < points.Length; i++)
            {
                var middle = (Vector2)(points[i] + points[(i + 1) % points.Length]) * 0.5f;
                EditorGUI.DrawRect(new Rect(middle.x - 4, middle.y - 4, 8, 8), kHover);
            }
        }

        // Each tile's name and size, where they fit
        void DrawLabels()
        {
            if (!showNames && !showSizes)
                return;
            foreach (var tile in layout.tiles)
            {
                if (tile.points == null || tile.points.Length < 3)
                    continue;
                var bounds = BoundsOf(CanvasPoints(tile.points));
                var text = LabelOf(tile, showNames, showSizes);
                if (!TagFits(text, bounds) && showNames && showSizes)
                    text = LabelOf(tile, true, false);
                if (TagFits(text, bounds))
                    Tag(bounds.center, text, tile.id == selectedTile ? 1 : 0.85f);
            }
        }

        // What a tile's label says: its name, its aspect and pixels ("B1 2:1 512x256", times sign)
        internal static string LabelOf(in HotspotTile tile, bool name, bool size)
        {
            var parts = new List<string>();
            if (name)
                parts.Add(string.IsNullOrEmpty(tile.name) ? "Tile " + tile.id : tile.name);
            if (size)
            {
                var pixels = HotspotTileKinds.SizeOf(tile);
                parts.Add(HotspotTileChoice.AspectText(pixels.x, pixels.y).Replace(" ", string.Empty));
                parts.Add($"{pixels.x}{HotspotTileChoice.kTimes}{pixels.y}");
            }
            return string.Join(" ", parts);
        }

        GUIStyle TagStyle => tagStyle ??= new GUIStyle(EditorStyles.whiteMiniLabel)
        {
            alignment = TextAnchor.MiddleCenter, padding = new RectOffset(4, 4, 1, 1),
            normal = { background = Texture2D.whiteTexture, textColor = Color.white }
        };

        bool TagFits(string text, Rect bounds) => text.Length > 0 && HotspotTileKinds.LabelFits(TagStyle.CalcSize(new GUIContent(text)), bounds);

        // A small dark tag with text, centered on a point
        void Tag(Vector2 at, string text, float alpha)
        {
            var content = new GUIContent(text);
            var size = TagStyle.CalcSize(content);
            var color = GUI.backgroundColor;
            GUI.backgroundColor = new Color(0.08f, 0.08f, 0.08f, 0.85f * alpha);
            GUI.Label(new Rect(at - size * 0.5f, size), content, TagStyle);
            GUI.backgroundColor = color;
        }

        static Rect BoundsOf(Vector3[] points)
        {
            var min = (Vector2)points[0]; var max = min;
            foreach (var point in points) { min = Vector2.Min(min, point); max = Vector2.Max(max, point); }
            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }

        // Under the canvas in the Kinds view: what each colour means
        void Legend(Rect row)
        {
            if (Event.current.type != EventType.Repaint)
                return;
            var widths = 0f;
            var names = HotspotTileKinds.Names;
            for (int i = 0; i < names.Count; i++)
                widths += 14 + EditorStyles.miniLabel.CalcSize(new GUIContent(names[i])).x + 10;
            var x = row.x + Mathf.Max(6, (row.width - widths) * 0.5f);
            for (int i = 0; i < names.Count; i++)
            {
                var content = new GUIContent(names[i]);
                var width = EditorStyles.miniLabel.CalcSize(content).x;
                EditorGUI.DrawRect(new Rect(x, row.y + (row.height - 10) * 0.5f, 10, 10), HotspotTileKinds.ColorOf((HotspotTileKind)i));
                GUI.Label(new Rect(x + 14, row.y, width, row.height), content, EditorStyles.miniLabel);
                x += 14 + width + 10;
            }
        }

        // What the mouse and keys do in a tool, as its tooltip says; the keys every tool has follow
        internal static string HintOf(Tool tool)
        {
            const string kEvery = "\nIn every tool: right click for the split and shape menu, V or H splits at the cursor, Delete removes the line or drawn shape under it, Alt or middle drag pans, the wheel zooms";
            switch (tool)
            {
                case Tool.Split:     return "Click splits the tile under the cursor along the dashed lines, with the kind of split chosen beside the tool. Shift swaps vertical and horizontal. Dragging a line still moves it" + kEvery;
                case Tool.DrawShape: return "Click adds a corner of a new tile of any shape, over the others. Click the first corner or press Enter to finish, Backspace takes a corner back, Escape stops" + kEvery;
                case Tool.DrawHole:  return "Click adds a corner of a hole in the selected shape. Click the first corner or press Enter to finish, Backspace takes a corner back, Escape stops" + kEvery;
                default:             return "Click selects a tile. Drag a line to move it. On the selected drawn shape: drag a corner to move it, double-click an edge to add one, drag inside to move the shape, Delete or Backspace over a corner removes it" + kEvery;
            }
        }

        // The cursor's pixel
        void StatusBar(Rect row)
        {
            if (Event.current.type == EventType.Repaint)
                EditorGUI.DrawRect(row, new Color(0, 0, 0, 0.15f));
            var position = cursorPixel.HasValue ? $"{Mathf.FloorToInt(cursorPixel.Value.x)}, {Mathf.FloorToInt(cursorPixel.Value.y)} px" : string.Empty;
            GUI.Label(new Rect(row.x + 6, row.y, row.width - 12, row.height), position, EditorStyles.miniLabel);
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
        internal static void Fill(Vector3[] points, bool shaped)
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
                SelectedTile = tile;
                menu.AddItem(new GUIContent($"Split vertically at x {x}"),   false, () => Edit("Split Hotspot Tile", () => layout.Split(tile, HotspotCutKind.Vertical, x)));
                menu.AddItem(new GUIContent($"Split horizontally at y {y}"), false, () => Edit("Split Hotspot Tile", () => layout.Split(tile, HotspotCutKind.Horizontal, y)));
                menu.AddItem(new GUIContent("Split along the diagonal \u2571"),  false, () => Edit("Split Hotspot Tile", () => layout.Split(tile, HotspotCutKind.Slash, 0)));
                menu.AddItem(new GUIContent("Split along the diagonal \u2572"),  false, () => Edit("Split Hotspot Tile", () => layout.Split(tile, HotspotCutKind.Backslash, 0)));
                menu.AddItem(new GUIContent($"Split into three triangles, apex at x {x}"), false, () => Edit("Split Hotspot Tile", () => layout.Split(tile, HotspotCutKind.Apex, x)));
            }
            if (line >= 0)
            {
                var node = lines[line].node;
                menu.AddSeparator(string.Empty);
                menu.AddItem(new GUIContent("Remove this line"), false, () => Edit("Remove Hotspot Line", () => layout.RemoveCut(node)));
            }
            menu.AddSeparator(string.Empty);
            menu.AddItem(new GUIContent("Draw a shape"), false, () => SetTool(Tool.DrawShape));
            if (tile != 0 && layout.IsDrawn(tile))
            {
                SelectedTile = tile;
                menu.AddItem(new GUIContent("Draw a hole in this shape"), false, () => SetTool(Tool.DrawHole));
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
