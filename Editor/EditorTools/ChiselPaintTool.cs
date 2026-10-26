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
        static readonly GUIContent kModeContent      = new("Mode", "What to paint: the modes of the project's paint profiles, by name. A surface takes a mode when the paint profile of its shader has one of that name.");
        static readonly GUIContent kOperationContent = new("Operation", "Paint: towards the color or value. Add / Subtract: a value up or down. Smooth: towards the neighbouring paint. Erase: back to the default.");
        static readonly GUIContent kColorContent     = new("Color");
        static readonly GUIContent kRadiusContent    = new("Radius", "The brush's radius, in metres.");
        static readonly GUIContent kStrengthContent  = new("Strength", "How much a dab moves the paint towards what it paints.");
        static readonly GUIContent kHardnessContent  = new("Hardness", "How much of the radius is painted at full strength; beyond it the brush falls off smoothly.");
        static readonly GUIContent kFillContent      = new("Fill Surface", "Sets the whole of the last surface under the mouse to the color or value.");
        static readonly GUIContent kChannelsContent  = new("Channels", "Works on the paint of the last surface under the mouse, in the mode's attribute.");
        static readonly GUIContent kCopyContent      = new("Copy", "Copies the first component's paint to the second.");
        static readonly GUIContent kSwapContent      = new("Swap", "Swaps the two components' paint.");
        static readonly GUIContent kInvertContent    = new("Invert", "Turns the first component's paint over within the mode's range.");
        static readonly string[]   kComponentNames   = { "X (R)", "Y (G)", "Z (B)", "W (A)" };

        // the mode painted in, by name: a surface takes it when its shader's profile has a mode of that name
        static string               s_ModeName;
        static ChiselPaintOperation s_Operation = ChiselPaintOperation.Paint;
        static Color                s_Color     = Color.red;
        static float                s_Value     = 1;
        static float                s_Radius    = 0.5f;
        static float                s_Strength  = 0.5f;
        static float                s_Hardness  = 0.5f;

        static bool                 s_ShowChannels;
        static int                  s_ChannelFrom, s_ChannelTo = 1;
        // the surface last under the mouse: what Fill and the channel buttons work on
        static Hit                  s_Last;

        static readonly List<ChiselIntersection> s_Intersections = new();
        // the surface under the mouse, as it was last picked
        static Hit                  s_Hover;
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
            if (type == EventType.MouseMove || type == EventType.MouseDown || type == EventType.MouseDrag)
            {
                s_Hover = Pick(evt.mousePosition);
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
                Handles.color = new Color(1, 1, 1, 0.8f);
                Handles.DrawWireDisc(hit.point, hit.normal, s_Radius);
                Handles.DrawWireDisc(hit.point, hit.normal, s_Radius * Mathf.Clamp01(s_Hardness));
            } else if (hit.valid)
                NotPaintable(window, hit, evt.mousePosition);

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
                    if (hit.profile.modes[ModeIndex(hit.profile)].kind == ChiselPaintKind.Color && s_Operation == ChiselPaintOperation.Paint)
                        Painted(s_Color);
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

        // The paint of the last surface under the mouse in the mode's attribute, made when it has none
        static bool LastPaint(out ChiselSurfacePaint paint, out ChiselPaintMode mode)
        {
            paint = null;
            mode  = default;
            var index = ModeIndex(s_Last.profile);
            if (!s_Last.valid || !s_Last.generator || index < 0)
                return false;
            mode = s_Last.profile.modes[index];
            Undo.RecordObject(s_Last.generator, "Chisel Paint");
            paint = ChiselPaintBrush.PaintFor(s_Last.generator, s_Last.surfaceIndex, mode);
            return paint != null;
        }

        static void Changed()
        {
            s_Last.generator.PaintChanged();
            EditorUtility.SetDirty(s_Last.generator);
        }

        static void Dab(Hit hit)
        {
            var index = ModeIndex(hit.profile);
            if (index < 0)
                return;
            var mode = hit.profile.modes[index];
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
            EditorGUIUtility.AddCursorRect(new Rect(0, 0, window.position.width, window.position.height), MouseCursor.NotAllowed);
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

            EditorGUILayout.LabelField(kModeContent);
            if (s_ModeContents.Length != s_ModeNames.Count)
                s_ModeContents = new GUIContent[s_ModeNames.Count];
            var chosen = -1;
            for (int n = 0; n < s_ModeNames.Count; n++)
            {
                s_ModeContents[n] ??= new GUIContent();
                s_ModeContents[n].text = s_ModeNames[n];
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
                s_OperationContents[i].text    = kOperations[i].name;
                s_OperationContents[i].image   = icons[on ? 1 : 0].image;
                s_OperationContents[i].tooltip = kOperations[i].tooltip;
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

        static readonly Color[] kSwatches =
        {
            Color.white, new(0.75f, 0.75f, 0.75f), new(0.5f, 0.5f, 0.5f), new(0.25f, 0.25f, 0.25f), Color.black,
            new(0.9f, 0.1f, 0.1f), new(1, 0.5f, 0), new(1, 0.9f, 0.1f), new(0.2f, 0.8f, 0.2f), new(0.1f, 0.8f, 0.8f),
            new(0.15f, 0.35f, 0.95f), new(0.55f, 0.25f, 0.85f), new(0.95f, 0.3f, 0.7f), new(0.5f, 0.3f, 0.15f),
        };
        // the colors painted with last, newest first
        static readonly List<Color> s_Recent = new();
        const int kMaxRecent = 9;
        const float kSwatchSize = 16;

        internal static void Painted(Color color)
        {
            s_Recent.Remove(color);
            s_Recent.Insert(0, color);
            if (s_Recent.Count > kMaxRecent)
                s_Recent.RemoveAt(s_Recent.Count - 1);
        }

        // A row of swatches; clicking one takes its color
        static void SwatchRow(IReadOnlyList<Color> colors)
        {
            var perRow = Mathf.Max(1, Mathf.FloorToInt(kRowWidth / (kSwatchSize + 2)));
            for (int start = 0; start < colors.Count; start += perRow)
            {
                var rect = GUILayoutUtility.GetRect(kRowWidth, kSwatchSize + 2);
                for (int i = start; i < Mathf.Min(start + perRow, colors.Count); i++)
                {
                    var swatch = new Rect(rect.x + (i - start) * (kSwatchSize + 2), rect.y + 1, kSwatchSize, kSwatchSize);
                    if (Event.current.type == EventType.Repaint)
                    {
                        var selected = colors[i] == s_Color;
                        EditorGUI.DrawRect(swatch, selected ? (EditorGUIUtility.isProSkin ? Color.white : Color.black) : new Color(0, 0, 0, 0.5f));
                        EditorGUI.DrawRect(new Rect(swatch.x + 1, swatch.y + 1, swatch.width - 2, swatch.height - 2), colors[i]);
                    }
                    EditorGUIUtility.AddCursorRect(swatch, MouseCursor.Link);
                    if (Event.current.type == EventType.MouseDown && Event.current.button == 0 && swatch.Contains(Event.current.mousePosition))
                    {
                        s_Color = colors[i];
                        GUI.changed = true;
                        Event.current.Use();
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
                        SwatchRow(kSwatches);
                        if (s_Recent.Count > 0)
                            SwatchRow(s_Recent);
                    }
                    else
                        s_Value = EditorGUILayout.Slider(new GUIContent(s_ModeName), s_Value, chosen.range.x, chosen.range.y);
                }
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
                using (new EditorGUI.DisabledScope(!Paintable(s_Last)))
                {
                    if (GUILayout.Button(kFillContent) && LastPaint(out var paint, out var mode))
                    {
                        var target = mode.kind == ChiselPaintKind.Color ? (Vector4)s_Color : Vector4.one * s_Value;
                        ChiselPaintBrush.Fill(paint, mode, target, s_Last.profile.GroupOf(ModeIndex(s_Last.profile)));
                        Changed();
                    }
                    s_ShowChannels = EditorGUILayout.Foldout(s_ShowChannels, kChannelsContent, true);
                    if (s_ShowChannels)
                    {
                        using (new EditorGUILayout.HorizontalScope())
                        {
                            s_ChannelFrom = EditorGUILayout.Popup(s_ChannelFrom, kComponentNames);
                            s_ChannelTo   = EditorGUILayout.Popup(s_ChannelTo, kComponentNames);
                        }
                        using (new EditorGUILayout.HorizontalScope())
                        {
                            if (GUILayout.Button(kCopyContent) && LastPaint(out var copyPaint, out _))
                            {
                                ChiselPaintBrush.CopyComponent(copyPaint, s_ChannelFrom, s_ChannelTo);
                                Changed();
                            }
                            if (GUILayout.Button(kSwapContent) && LastPaint(out var swapPaint, out _))
                            {
                                ChiselPaintBrush.SwapComponents(swapPaint, s_ChannelFrom, s_ChannelTo);
                                Changed();
                            }
                            if (GUILayout.Button(kInvertContent) && LastPaint(out var invertPaint, out var invertMode))
                            {
                                ChiselPaintBrush.InvertComponent(invertPaint, s_ChannelFrom, invertMode.range);
                                Changed();
                            }
                        }
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
