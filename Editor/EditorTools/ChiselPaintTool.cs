using System.Collections.Generic;
using Chisel.Components;
using Chisel.Core;
using UnityEditor;
using UnityEditor.EditorTools;
using UnityEngine;

namespace Chisel.Editors
{
    /// <summary>
    /// Paints the surface under the mouse in a mode of the paint profile of its material's shader: a color, or a named
    /// value, with a round brush. Only the surface that is hit is painted; a stroke is one undo.
    /// </summary>
    sealed class ChiselPaintTool : EditorTool
    {
        const string kModeTooltip = "A surface takes this mode when the paint profile of its shader has a mode of this name";
        static readonly GUIContent kOperationContent = new("Operations", "Paint: towards the color or value. Add / Subtract: a value up or down. Smooth: towards the neighbouring paint. Erase: back to the default.");
        static readonly GUIContent kColorContent     = new("Color");
        static readonly GUIContent kRadiusContent    = new("Radius", "The brush's radius, in metres.");
        static readonly GUIContent kStrengthContent  = new("Strength", "How much a dab moves the paint towards what it paints.");
        static readonly GUIContent kHardnessContent  = new("Hardness", "How much of the radius is painted at full strength; beyond it the brush falls off smoothly.");

        // the mode painted in, by name: a surface takes it when its shader's profile has a mode of that name
        static string               s_ModeName;
        static ChiselPaintOperation s_Operation = ChiselPaintOperation.Paint;
        static Color                s_Color     = Color.red;
        static float                s_Value     = 1;
        static float                s_Radius    = 0.5f;
        static float                s_Strength  = 0.5f;
        static float                s_Hardness  = 0.5f;

        // the surface last under the mouse, whose mode's color or range the panel shows
        static Hit                  s_Last;

        static readonly List<ChiselIntersection> s_Intersections = new();
        // the surface under the mouse, as it was last picked
        static Hit                  s_Hover;
        // where the mouse was in the scene when it was picked (an overlay takes the mouse events over it)
        static Vector2              s_HoverMouse;
        static int                  s_UndoGroup = -1;

        static GUIContent s_ToolbarIcon;
        public override GUIContent toolbarIcon => s_ToolbarIcon ??= new GUIContent(ChiselEditorResources.LoadIconImages("paint_tool")?[0],
                                                                                   "Chisel Paint Tool: paint vertex data onto surfaces, in the modes of their shaders' paint profiles");

        [MenuItem("Tools/Chisel/Paint")]
        static void Activate() => ToolManager.SetActiveTool<ChiselPaintTool>();

        struct Hit
        {
            public bool                      valid;
            public ChiselGeneratorComponent  generator;
            public int                       surfaceIndex;
            public Vector3                   point;
            public Vector3                   normal;
            public ChiselPaintProfile        profile;
            public Shader                    shader;
        }

        static Hit Pick(Vector2 mousePosition)
        {
            ChiselSelectionManager.GetOverlappingObjects(mousePosition, s_Intersections);
            foreach (var intersection in s_Intersections)
            {
                if (intersection.treeNode is not ChiselGeneratorComponent generator || intersection.model == null ||
                    !intersection.brushIntersection.brush.Valid)
                    continue;
                var surfaceIndex = ChiselTrimDecals.SurfaceOf(intersection.brushIntersection.brush, intersection.brushIntersection.surfaceIndex);
                var surfaces     = generator.SurfaceDefinition?.surfaces;
                if (surfaceIndex < 0 || surfaces == null || surfaceIndex >= surfaces.Length)
                    continue;
                var shader = surfaces[surfaceIndex].RenderMaterial ? surfaces[surfaceIndex].RenderMaterial.shader : null;
                return new Hit
                {
                    valid        = true,
                    generator    = generator,
                    surfaceIndex = surfaceIndex,
                    point        = intersection.worldPlaneIntersection,
                    normal       = intersection.worldPlane.normal,
                    shader       = shader,
                    profile      = ChiselPaintProfiles.Find(shader)
                };
            }
            return default;
        }

        static string NameOf(ChiselPaintProfile profile, int index)
        {
            var name = profile.modes[index].name;
            return string.IsNullOrEmpty(name) ? $"Mode {index}" : name;
        }

        // The profile's mode of the chosen name, -1 when it has none
        static int ModeIndex(ChiselPaintProfile profile)
        {
            if (profile == null)
                return -1;
            for (int i = 0; i < profile.modes.Count; i++)
            {
                if (NameOf(profile, i) == s_ModeName)
                    return i;
            }
            return -1;
        }

        static bool Paintable(Hit hit) => hit.valid && ModeIndex(hit.profile) >= 0;

        public override void OnToolGUI(EditorWindow window)
        {
            if (window is not SceneView)
                return;
            var evt = Event.current;
            var id  = GUIUtility.GetControlID(FocusType.Passive);
            // picking renders: only on the mouse's own events, never inside the scene view's layout or repaint
            var type = evt.GetTypeForControl(id);
            if (type == EventType.MouseLeaveWindow)
            {
                s_Hover = default;
                HandleUtility.Repaint();
            }
            if (type == EventType.MouseMove || type == EventType.MouseDown || type == EventType.MouseDrag)
            {
                s_Hover = Pick(evt.mousePosition);
                s_HoverMouse = evt.mousePosition;
                if (s_Hover.valid && s_Hover.profile != null)
                {
                    s_Last = s_Hover;
                    if (string.IsNullOrEmpty(s_ModeName) && s_Hover.profile.modes.Count > 0)
                        s_ModeName = NameOf(s_Hover.profile, 0);
                }
            }
            var hit = s_Hover;
            if (Paintable(hit))
            {
                if (s_Operation != ChiselPaintOperation.Fill)
                {
                    Handles.color = new Color(1, 1, 1, 0.8f);
                    Handles.DrawWireDisc(hit.point, hit.normal, s_Radius);
                    Handles.DrawWireDisc(hit.point, hit.normal, s_Radius * Mathf.Clamp01(s_Hardness));
                }
            } else if (hit.valid)
                NotPaintable(window, hit, s_HoverMouse);

            switch (evt.GetTypeForControl(id))
            {
                case EventType.Layout:
                    HandleUtility.AddDefaultControl(id);
                    break;
                case EventType.MouseMove:
                    HandleUtility.Repaint();
                    break;
                case EventType.MouseDown:
                    if (evt.button != 0 || evt.alt || !Paintable(hit))
                        break;
                    // control-click takes the paint under the mouse as the brush's color or value
                    if (evt.control)
                    {
                        Sample(hit);
                        evt.Use();
                        break;
                    }
                    GUIUtility.hotControl = id;
                    Undo.IncrementCurrentGroup();
                    Undo.SetCurrentGroupName("Chisel Paint");
                    s_UndoGroup = Undo.GetCurrentGroup();
                    s_Filled.Clear();
                    Dab(hit);
                    evt.Use();
                    break;
                case EventType.MouseDrag:
                    if (GUIUtility.hotControl != id)
                        break;
                    if (Paintable(hit))
                        Dab(hit);
                    evt.Use();
                    break;
                case EventType.MouseUp:
                    if (GUIUtility.hotControl != id)
                        break;
                    GUIUtility.hotControl = 0;
                    if (s_UndoGroup >= 0)
                        Undo.CollapseUndoOperations(s_UndoGroup);
                    s_UndoGroup = -1;
                    evt.Use();
                    break;
            }
        }

        static void Sample(Hit hit)
        {
            var index = ModeIndex(hit.profile);
            if (index < 0)
                return;
            var mode  = hit.profile.modes[index];
            var paint = hit.generator.GetSurfacePaint(hit.surfaceIndex, mode.attribute);
            var value = paint != null ? paint.Sample(hit.generator.transform.InverseTransformPoint(hit.point)) : mode.defaultValue;
            if (mode.kind == ChiselPaintKind.Color)
                s_Color = value;
            else
            {
                // the mode's first component
                for (int c = 0; c < 4; c++)
                {
                    if (((int)mode.components & (1 << c)) != 0)
                    {
                        s_Value = value[c];
                        break;
                    }
                }
            }
        }

        static readonly HashSet<(ChiselGeneratorComponent, int)> s_Filled = new();

        static void Dab(Hit hit)
        {
            var index = ModeIndex(hit.profile);
            if (index < 0)
                return;
            var mode = hit.profile.modes[index];
            if (s_Operation == ChiselPaintOperation.Fill)
            {
                // a stroke fills each surface it goes over once
                if (!s_Filled.Add((hit.generator, hit.surfaceIndex)))
                    return;
            }
            Undo.RecordObject(hit.generator, "Chisel Paint");
            var paint = ChiselPaintBrush.PaintFor(hit.generator, hit.surfaceIndex, mode);
            if (paint == null)
                return;
            var target = mode.kind == ChiselPaintKind.Color ? (Vector4)s_Color : Vector4.one * s_Value;
            var scale  = hit.generator.transform.lossyScale;
            var radius = s_Radius / Mathf.Max(Mathf.Abs(scale.x), Mathf.Max(Mathf.Abs(scale.y), Mathf.Abs(scale.z)), 1e-5f);
            var center = hit.generator.transform.InverseTransformPoint(hit.point);
            if (ChiselPaintBrush.Dab(paint, mode, s_Operation, target, center, radius, s_Strength, s_Hardness, hit.profile.GroupOf(index)))
            {
                hit.generator.PaintChanged();
                EditorUtility.SetDirty(hit.generator);
            }
        }

        static GUIStyle s_TooltipStyle;

        // Over a surface that can't be painted: no brush, a cursor that says so, and why next to it
        static void NotPaintable(EditorWindow window, Hit hit, Vector2 mousePosition)
        {
            // only round where the scene last had the mouse: over an overlay (which takes the mouse events) it isn't
            EditorGUIUtility.AddCursorRect(new Rect(mousePosition.x - 4, mousePosition.y - 4, 8, 8), MouseCursor.NotAllowed);
            if (Event.current.type != EventType.Repaint)
                return;
            // solid, as a tooltip is: a panel's color with a border round it
            s_TooltipStyle ??= new GUIStyle(EditorStyles.label) { padding = new RectOffset(6, 6, 3, 3), wordWrap = false };
            var content = new GUIContent(!hit.shader         ? "The surface\nhas no shader\nto paint for"
                                       : hit.profile == null ? $"The shader {hit.shader.name}\nis not set up to be paintable\nit has no paint profile"
                                                             : $"The shader {hit.shader.name}\nhas no paint mode \"{s_ModeName}\"\nin its paint profile");
            var size = s_TooltipStyle.CalcSize(content);
            var rect = new Rect(mousePosition + new Vector2(16, 20), size);
            Handles.BeginGUI();
            EditorGUI.DrawRect(new Rect(rect.x - 1, rect.y - 1, rect.width + 2, rect.height + 2), EditorGUIUtility.isProSkin ? new Color(0.1f, 0.1f, 0.1f, 1) : new Color(0.5f, 0.5f, 0.5f, 1));
            EditorGUI.DrawRect(rect, EditorGUIUtility.isProSkin ? new Color(0.24f, 0.24f, 0.24f, 1) : new Color(0.94f, 0.94f, 0.94f, 1));
            s_TooltipStyle.Draw(rect, content, false, false, false, false);
            Handles.EndGUI();
        }

        // The overlay's width, and what its rows have of it
        internal const float kPanelWidth = 260;
        const float kRowWidth = kPanelWidth - 6;

        static readonly List<string> s_ModeNames = new();

        // The mode of the chosen name: the last painted surface's, else the first profile's that has one
        static bool ChosenMode(out ChiselPaintMode mode)
        {
            var index = ModeIndex(s_Last.profile);
            if (index >= 0)
            {
                mode = s_Last.profile.modes[index];
                return true;
            }
            foreach (var profile in ChiselPaintProfiles.All)
            {
                index = ModeIndex(profile);
                if (index >= 0)
                {
                    mode = profile.modes[index];
                    return true;
                }
            }
            mode = default;
            return false;
        }

        // Every mode name of the project's profiles, as toggle buttons wrapping within the panel
        static void ModeButtons()
        {
            s_ModeNames.Clear();
            foreach (var profile in ChiselPaintProfiles.All)
            {
                for (int i = 0; i < profile.modes.Count; i++)
                {
                    var name = NameOf(profile, i);
                    if (!s_ModeNames.Contains(name))
                        s_ModeNames.Add(name);
                }
            }
            if (s_ModeNames.Count == 0)
            {
                EditorGUILayout.HelpBox("The project has no paint profiles.", MessageType.None);
                return;
            }
            if (!s_ModeNames.Contains(s_ModeName))
                s_ModeName = s_ModeNames[0];

            if (s_ModeContents.Length != s_ModeNames.Count)
                s_ModeContents = new GUIContent[s_ModeNames.Count];
            var chosen = -1;
            for (int n = 0; n < s_ModeNames.Count; n++)
            {
                s_ModeContents[n] ??= new GUIContent();
                s_ModeContents[n].text    = s_ModeNames[n];
                s_ModeContents[n].tooltip = kModeTooltip;
                if (s_ModeNames[n] == s_ModeName)
                    chosen = n;
            }
            s_ModeName = s_ModeNames[Toggles(chosen, s_ModeContents, null)];
        }

        static GUIContent[] s_ModeContents = System.Array.Empty<GUIContent>();

        // Toggle buttons wrapping within the panel, one of them on; disabled (when given) are drawn but can't be chosen
        static int Toggles(int chosen, GUIContent[] contents, bool[] disabled)
        {
            var style = EditorStyles.miniButton;
            for (int n = 0; n < contents.Length;)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    var used = 0f;
                    do
                    {
                        var width = Mathf.Min(kRowWidth, style.CalcSize(contents[n]).x + 8);
                        if (used > 0 && used + width > kRowWidth)
                            break;
                        using (new EditorGUI.DisabledScope(disabled != null && disabled[n]))
                        {
                            if (GUILayout.Toggle(n == chosen, contents[n], style, GUILayout.Width(width), GUILayout.Height(20)))
                                chosen = n;
                        }
                        used += width + style.margin.horizontal;
                        n++;
                    } while (n < contents.Length);
                    GUILayout.FlexibleSpace();
                }
            }
            return Mathf.Max(chosen, 0);
        }

        static readonly (ChiselPaintOperation operation, string icon, string name, string tooltip)[] kOperations =
        {
            (ChiselPaintOperation.Paint,    "paint_paint",    "Paint",    "Towards the color or value"),
            (ChiselPaintOperation.Fill,     "paint_fill",     "Fill",     "The whole surface clicked on, to the color or value"),
            (ChiselPaintOperation.Add,      "paint_add",      "Add",      "Raises the value"),
            (ChiselPaintOperation.Subtract, "paint_subtract", "Subtract", "Lowers the value"),
            (ChiselPaintOperation.Smooth,   "paint_smooth",   "Smooth",   "Towards the neighbouring paint"),
            (ChiselPaintOperation.Erase,    "paint_erase",    "Erase",    "Back to the default"),
        };
        static readonly GUIContent[] s_OperationContents = new GUIContent[kOperations.Length];
        static readonly bool[]       s_OperationDisabled = new bool[kOperations.Length];

        // The operations, with their icons; adding and subtracting only for a value
        static void OperationButtons(bool isValue)
        {
            var chosen = 0;
            for (int i = 0; i < kOperations.Length; i++)
            {
                var icons = ChiselEditorResources.GetIconContent(kOperations[i].icon, kOperations[i].tooltip);
                var on    = kOperations[i].operation == s_Operation;
                s_OperationContents[i] ??= new GUIContent();
                s_OperationContents[i].text    = null;
                s_OperationContents[i].image   = icons[on ? 1 : 0].image;
                s_OperationContents[i].tooltip = $"{kOperations[i].name}: {kOperations[i].tooltip}";
                s_OperationDisabled[i] = !isValue && (kOperations[i].operation == ChiselPaintOperation.Add || kOperations[i].operation == ChiselPaintOperation.Subtract);
                if (on)
                    chosen = i;
            }
            if (s_OperationDisabled[chosen])
                s_Operation = ChiselPaintOperation.Paint;
            EditorGUILayout.LabelField(kOperationContent);
            s_Operation = kOperations[Toggles(chosen, s_OperationContents, s_OperationDisabled)].operation;
        }

        const int kFalloffSize = 40;
        static Texture2D s_Falloff;
        static float s_FalloffStrength = -1, s_FalloffHardness = -1;

        // A blob as a dab paints it: how much the brush counts across its radius
        static Texture2D Falloff()
        {
            if (s_Falloff && s_FalloffStrength == s_Strength && s_FalloffHardness == s_Hardness)
                return s_Falloff;
            if (!s_Falloff)
                s_Falloff = new Texture2D(kFalloffSize, kFalloffSize, TextureFormat.RGBA32, false) { hideFlags = HideFlags.HideAndDontSave, filterMode = FilterMode.Bilinear };
            s_FalloffStrength = s_Strength;
            s_FalloffHardness = s_Hardness;
            var pixels = new Color32[kFalloffSize * kFalloffSize];
            var radius = kFalloffSize * 0.5f;
            for (int y = 0; y < kFalloffSize; y++)
            {
                for (int x = 0; x < kFalloffSize; x++)
                {
                    var distance = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(radius, radius));
                    var weight = ChiselPaintBrush.Weight(distance, radius, s_Strength, s_Hardness);
                    var shade = (byte)Mathf.RoundToInt(Mathf.Lerp(32, 255, weight));
                    pixels[y * kFalloffSize + x] = new Color32(shade, shade, shade, 255);
                }
            }
            s_Falloff.SetPixels32(pixels);
            s_Falloff.Apply();
            return s_Falloff;
        }

        static readonly Color[] kDefaultSwatches =
        {
            Color.white, new(0.75f, 0.75f, 0.75f), new(0.5f, 0.5f, 0.5f), new(0.25f, 0.25f, 0.25f), Color.black,
            new(0.9f, 0.1f, 0.1f), new(1, 0.5f, 0), new(1, 0.9f, 0.1f), new(0.2f, 0.8f, 0.2f), new(0.1f, 0.8f, 0.8f),
            new(0.15f, 0.35f, 0.95f), new(0.55f, 0.25f, 0.85f), new(0.95f, 0.3f, 0.7f), new(0.5f, 0.3f, 0.15f),
        };
        const string kSwatchesPref = "Chisel.Paint.Swatches";
        const float  kSwatchSize   = 14;
        // neighbours share their border
        const float  kSwatchGap    = -1;
        static List<Color> s_Swatches;
        static readonly GUIContent kAddSwatchContent = new(string.Empty, "Adds the current color as a swatch, and picks its color (middle-click or right-click a swatch to remove it)");

        // the swatches, kept per user
        static List<Color> Swatches
        {
            get
            {
                if (s_Swatches != null)
                    return s_Swatches;
                s_Swatches = new List<Color>();
                foreach (var hex in EditorPrefs.GetString(kSwatchesPref, string.Empty).Split(';'))
                {
                    if (UnityEngine.ColorUtility.TryParseHtmlString("#" + hex, out var color) && !s_Swatches.Contains(color))
                        s_Swatches.Add(color);
                }
                if (s_Swatches.Count == 0 && !EditorPrefs.HasKey(kSwatchesPref))
                    s_Swatches.AddRange(kDefaultSwatches);
                return s_Swatches;
            }
        }

        static void SaveSwatches()
        {
            var hexes = new string[Swatches.Count];
            for (int i = 0; i < hexes.Length; i++)
                hexes[i] = UnityEngine.ColorUtility.ToHtmlStringRGBA(Swatches[i]);
            EditorPrefs.SetString(kSwatchesPref, string.Join(";", hexes));
        }

        static int ChosenSwatch() => SwatchOf(s_Color);

        // The swatch of a color, as the swatches are kept (8 bits a component); -1 when it has none
        static int SwatchOf(Color color)
        {
            Color32 wanted = color;
            for (int i = 0; i < Swatches.Count; i++)
            {
                Color32 swatch = Swatches[i];
                if (swatch.r == wanted.r && swatch.g == wanted.g && swatch.b == wanted.b && swatch.a == wanted.a)
                    return i;
            }
            return -1;
        }

        // Unity's color picker, giving every change to the callback
        static void PickColor(Color color, System.Action<Color> changed)
        {
            var picker = typeof(EditorWindow).Assembly.GetType("UnityEditor.ColorPicker");
            foreach (var method in picker?.GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static) ?? System.Array.Empty<System.Reflection.MethodInfo>())
            {
                var parameters = method.GetParameters();
                if (method.Name != "Show" || parameters.Length < 4 || parameters[0].ParameterType != typeof(System.Action<Color>))
                    continue;
                var arguments = new object[parameters.Length];
                arguments[0] = changed;
                arguments[1] = color;
                arguments[2] = true;
                arguments[3] = false;
                for (int i = 4; i < parameters.Length; i++)
                    arguments[i] = parameters[i].HasDefaultValue ? parameters[i].DefaultValue : null;
                method.Invoke(null, arguments);
                return;
            }
        }

        // The swatches, wrapping over as many rows as they need, then the current color (adds it as a swatch, picked);
        // a drag reorders them, a middle-click removes one
        static int s_DraggedSwatch = -1;

        static void RemoveSwatch(int index)
        {
            if (index < 0 || index >= Swatches.Count)
                return;
            Swatches.RemoveAt(index);
            SaveSwatches();
            SceneView.RepaintAll();
        }

        // The current color as a new swatch, then Unity's color picker on it; a color some other swatch has already
        // isn't added twice, that swatch is chosen instead
        static void AddSwatch()
        {
            var index = Swatches.Count;
            Swatches.Add(s_Color);
            SaveSwatches();
            PickColor(s_Color, color =>
            {
                s_Color = color;
                var existing = SwatchOf(color);
                if (existing >= 0 && existing != index)
                {
                    if (index >= 0 && index < Swatches.Count)
                        Swatches.RemoveAt(index);
                    index = -1;
                } else if (index < 0)
                {
                    index = Swatches.Count;
                    Swatches.Add(color);
                } else if (index < Swatches.Count)
                    Swatches[index] = color;
                SaveSwatches();
                SceneView.RepaintAll();
            });
        }

        static void SwatchesGUI()
        {
            var swatches = Swatches;
            var chosen   = ChosenSwatch();
            var count    = swatches.Count + 1;
            // starting where the labels' text does
            var indent   = EditorStyles.label.margin.left + EditorStyles.label.padding.left;
            var perRow   = Mathf.Max(1, Mathf.FloorToInt((kRowWidth - indent + kSwatchGap) / (kSwatchSize + kSwatchGap)));
            var rows     = (count + perRow - 1) / perRow;
            var area     = GUILayoutUtility.GetRect(kRowWidth, rows * (kSwatchSize + kSwatchGap) - kSwatchGap + 2);
            area.xMin   += indent;
            var evt      = Event.current;
            var id       = GUIUtility.GetControlID(FocusType.Passive);
            var border   = EditorGUIUtility.isProSkin ? new Color(0.36f, 0.36f, 0.36f) : new Color(0.58f, 0.58f, 0.58f);
            var highlight = new Color(0.25f, 0.6f, 1f);
            Rect CellOf(int i) => new(area.x + (i % perRow) * (kSwatchSize + kSwatchGap), area.y + (i / perRow) * (kSwatchSize + kSwatchGap) + 1, kSwatchSize, kSwatchSize);

            // dragging a swatch moves it to the cell under the mouse
            if (GUIUtility.hotControl == id)
            {
                if (evt.type == EventType.MouseDrag && s_DraggedSwatch >= 0 && s_DraggedSwatch < swatches.Count)
                {
                    for (int i = 0; i < swatches.Count; i++)
                    {
                        var cell = CellOf(i);
                        cell.xMax += kSwatchGap;
                        cell.yMax += kSwatchGap;
                        if (i == s_DraggedSwatch || !cell.Contains(evt.mousePosition))
                            continue;
                        var moved = swatches[s_DraggedSwatch];
                        swatches.RemoveAt(s_DraggedSwatch);
                        swatches.Insert(i, moved);
                        s_DraggedSwatch = i;
                        SaveSwatches();
                        break;
                    }
                    evt.Use();
                } else if (evt.type == EventType.MouseUp)
                {
                    GUIUtility.hotControl = 0;
                    s_DraggedSwatch = -1;
                    evt.Use();
                }
            }

            for (int i = 0; i < count; i++)
            {
                var cell = CellOf(i);
                var inner = new Rect(cell.x + 1, cell.y + 1, cell.width - 2, cell.height - 2);
                if (i == swatches.Count)
                {
                    // as the color picker's own: the current color, smaller than a swatch, in an empty cell
                    if (evt.type == EventType.Repaint)
                    {
                        EditorGUI.DrawRect(cell, border);
                        EditorGUI.DrawRect(inner, EditorGUIUtility.isProSkin ? new Color(0.22f, 0.22f, 0.22f) : new Color(0.76f, 0.76f, 0.76f));
                        EditorGUI.DrawRect(new Rect(cell.x + 3, cell.y + 3, cell.width - 6, cell.height - 6), s_Color);
                    }
                    GUI.Label(cell, kAddSwatchContent, GUIStyle.none);
                    if (evt.type == EventType.MouseDown && evt.button == 0 && cell.Contains(evt.mousePosition))
                    {
                        evt.Use();
                        // a color that has a swatch already is chosen there
                        if (chosen < 0)
                            AddSwatch();
                    }
                    continue;
                }
                if (evt.type == EventType.Repaint)
                {
                    if (i == chosen)
                    {
                        // inside the cell: there is no room between the swatches
                        EditorGUI.DrawRect(cell, highlight);
                        EditorGUI.DrawRect(new Rect(cell.x + 2, cell.y + 2, cell.width - 4, cell.height - 4), swatches[i]);
                    } else
                    {
                        EditorGUI.DrawRect(cell, border);
                        EditorGUI.DrawRect(inner, swatches[i]);
                    }
                }
                EditorGUIUtility.AddCursorRect(cell, GUIUtility.hotControl == id ? MouseCursor.Pan : MouseCursor.Link);
                if (evt.type == EventType.MouseDown && cell.Contains(evt.mousePosition))
                {
                    if (evt.button == 0)
                    {
                        // a click takes the color; a drag moves the swatch
                        s_Color = swatches[i];
                        s_DraggedSwatch = i;
                        GUIUtility.hotControl = id;
                        GUI.changed = true;
                        evt.Use();
                    } else if (evt.button == 2)
                    {
                        RemoveSwatch(i);
                        evt.Use();
                        GUIUtility.ExitGUI();
                    } else if (evt.button == 1)
                    {
                        // for those without a middle mouse button
                        var index = i;
                        var menu = new GenericMenu();
                        menu.AddItem(new GUIContent("Delete"), false, () => RemoveSwatch(index));
                        menu.ShowAsContext();
                        evt.Use();
                    }
                }
            }
        }
        // The tool's settings, in its overlay
        internal static void PanelGUI()
        {
            var labelWidth = EditorGUIUtility.labelWidth;
            EditorGUIUtility.labelWidth = 90;
            {
                ModeButtons();
                var hasMode = ChosenMode(out var chosen);
                OperationButtons(hasMode && chosen.kind == ChiselPaintKind.Value);
                EditorGUILayout.Space(2);
                if (hasMode)
                {
                    if (chosen.kind == ChiselPaintKind.Color)
                    {
                        s_Color = EditorGUILayout.ColorField(kColorContent, s_Color, true, true, false);
                        SwatchesGUI();
                    }
                    else
                        s_Value = EditorGUILayout.Slider(new GUIContent(s_ModeName), s_Value, chosen.range.x, chosen.range.y);
                }
                // a fill takes the whole surface: the brush doesn't count
                using (new EditorGUI.DisabledScope(s_Operation == ChiselPaintOperation.Fill))
                using (new EditorGUILayout.VerticalScope())
                {
                    s_Radius = Mathf.Max(0.01f, EditorGUILayout.FloatField(kRadiusContent, s_Radius));
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        using (new EditorGUILayout.VerticalScope())
                        {
                            s_Strength = EditorGUILayout.Slider(kStrengthContent, s_Strength, 0, 1);
                            s_Hardness = EditorGUILayout.Slider(kHardnessContent, s_Hardness, 0, 1);
                        }
                        var falloff = GUILayoutUtility.GetRect(kFalloffSize, kFalloffSize, GUILayout.Width(kFalloffSize), GUILayout.Height(kFalloffSize));
                        if (Event.current.type == EventType.Repaint)
                            GUI.DrawTexture(falloff, Falloff(), ScaleMode.ScaleToFit);
                    }
                }
                EditorGUILayout.HelpBox("Control-click takes the paint under the mouse.", MessageType.None);
            }
            EditorGUIUtility.labelWidth = labelWidth;
        }
    }

    /// <summary>The Chisel Paint tool's settings, while it is the active tool</summary>
    [UnityEditor.Overlays.Overlay(typeof(SceneView), kOverlayTitle)]
    sealed class ChiselPaintOverlay : UnityEditor.Overlays.IMGUIOverlay, UnityEditor.Overlays.ITransientOverlay
    {
        const string kOverlayTitle = "Chisel Paint";

        public bool visible => ToolManager.activeToolType == typeof(ChiselPaintTool);

        public override void OnGUI()
        {
            // one width, whatever it shows
            using (new EditorGUILayout.VerticalScope(GUILayout.Width(ChiselPaintTool.kPanelWidth)))
                ChiselPaintTool.PanelGUI();
        }
    }
}
