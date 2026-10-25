using System.Collections.Generic;
using Chisel.Components;
using Chisel.Core;
using UnityEditor;
using UnityEditor.EditorTools;
using UnityEngine;

namespace Chisel.Editors
{
    /// <summary>The paint profiles of the project, by the shader they are for</summary>
    static class ChiselPaintProfiles
    {
        static Dictionary<Shader, ChiselPaintProfile> s_ByShader;

        [InitializeOnLoadMethod]
        static void Initialize() => EditorApplication.projectChanged += () => s_ByShader = null;

        public static ChiselPaintProfile Find(Shader shader)
        {
            if (shader == null)
                return null;
            if (s_ByShader == null)
            {
                s_ByShader = new Dictionary<Shader, ChiselPaintProfile>();
                foreach (var guid in AssetDatabase.FindAssets("t:" + nameof(ChiselPaintProfile)))
                {
                    var profile = AssetDatabase.LoadAssetAtPath<ChiselPaintProfile>(AssetDatabase.GUIDToAssetPath(guid));
                    if (profile != null && profile.shader != null)
                        s_ByShader.TryAdd(profile.shader, profile);
                }
            }
            return s_ByShader.TryGetValue(shader, out var found) ? found : null;
        }
    }

    /// <summary>
    /// Paints the surface under the mouse in a mode of the paint profile of its material's shader: a color, or a named
    /// value, with a round brush. Only the surface that is hit is painted; a stroke is one undo.
    /// </summary>
    [EditorTool("Chisel Paint")]
    sealed class ChiselPaintTool : EditorTool
    {
        static readonly GUIContent kModeContent      = new("Mode", "What to paint: the modes of the paint profile of the surface's shader.");
        static readonly GUIContent kOperationContent = new("Operation", "Paint: towards the color or value. Add / Subtract: a value up or down. Smooth: towards the neighbouring paint. Erase: back to the default.");
        static readonly GUIContent kColorContent     = new("Color");
        static readonly GUIContent kRadiusContent    = new("Radius", "The brush's radius, in metres.");
        static readonly GUIContent kStrengthContent  = new("Strength", "How much a dab moves the paint towards what it paints.");
        static readonly GUIContent kHardnessContent  = new("Hardness", "How much of the radius is painted at full strength; beyond it the brush falls off smoothly.");
        static readonly GUIContent kCellSizeContent  = new("Cell Size", "How finely a surface painted for the first time is divided: its paint is a grid of this size, and the surface is cut at its points.");
        static readonly GUIContent kFillContent      = new("Fill Surface", "Sets the whole of the last surface under the mouse to the color or value.");
        static readonly GUIContent kChannelsContent  = new("Channels", "Works on the paint of the last surface under the mouse, in the mode's attribute.");
        static readonly GUIContent kCopyContent      = new("Copy", "Copies the first component's paint to the second.");
        static readonly GUIContent kSwapContent      = new("Swap", "Swaps the two components' paint.");
        static readonly GUIContent kInvertContent    = new("Invert", "Turns the first component's paint over within the mode's range.");
        static readonly string[]   kComponentNames   = { "X (R)", "Y (G)", "Z (B)", "W (A)" };

        static int                  s_Mode;
        static ChiselPaintOperation s_Operation = ChiselPaintOperation.Paint;
        static Color                s_Color     = Color.red;
        static float                s_Value     = 1;
        static float                s_Radius    = 0.5f;
        static float                s_Strength  = 0.5f;
        static float                s_Hardness  = 0.5f;
        static float                s_CellSize  = 0.25f;

        static bool                 s_ShowChannels;
        static int                  s_ChannelFrom, s_ChannelTo = 1;
        // the surface last under the mouse: what Fill and the channel buttons work on
        static Hit                  s_Last;

        static readonly List<ChiselIntersection> s_Intersections = new();
        static Rect                 s_Window = new(10, 30, 260, 200);
        static int                  s_UndoGroup = -1;

        public override GUIContent toolbarIcon => new("Paint", "Chisel Paint: paint vertex data onto surfaces, in the modes of their shaders' paint profiles");

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

        public override void OnToolGUI(EditorWindow window)
        {
            if (window is not SceneView)
                return;
            var evt = Event.current;
            var id  = GUIUtility.GetControlID(FocusType.Passive);
            var hit = Pick(evt.mousePosition);
            if (hit.valid && hit.profile != null)
                s_Last = hit;

            DrawPanel(hit);
            if (hit.valid)
            {
                Handles.color = hit.profile != null ? new Color(1, 1, 1, 0.8f) : new Color(1, 0.3f, 0.3f, 0.8f);
                Handles.DrawWireDisc(hit.point, hit.normal, s_Radius);
                Handles.DrawWireDisc(hit.point, hit.normal, s_Radius * Mathf.Clamp01(s_Hardness));
            }

            switch (evt.GetTypeForControl(id))
            {
                case EventType.Layout:
                    HandleUtility.AddDefaultControl(id);
                    break;
                case EventType.MouseMove:
                    HandleUtility.Repaint();
                    break;
                case EventType.MouseDown:
                    if (evt.button != 0 || evt.alt || !hit.valid || hit.profile == null || s_Window.Contains(evt.mousePosition))
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
                    Dab(hit);
                    evt.Use();
                    break;
                case EventType.MouseDrag:
                    if (GUIUtility.hotControl != id)
                        break;
                    if (hit.valid && hit.profile != null)
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
            if (s_Mode < 0 || s_Mode >= hit.profile.modes.Count)
                return;
            var mode  = hit.profile.modes[s_Mode];
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
            if (!s_Last.valid || !s_Last.generator || s_Last.profile == null || s_Mode < 0 || s_Mode >= s_Last.profile.modes.Count)
                return false;
            mode = s_Last.profile.modes[s_Mode];
            Undo.RecordObject(s_Last.generator, "Chisel Paint");
            paint = ChiselPaintBrush.PaintFor(s_Last.generator, s_Last.surfaceIndex, mode, s_CellSize);
            return paint != null;
        }

        static void Changed()
        {
            s_Last.generator.PaintChanged();
            EditorUtility.SetDirty(s_Last.generator);
        }

        static void Dab(Hit hit)
        {
            if (s_Mode < 0 || s_Mode >= hit.profile.modes.Count)
                return;
            var mode = hit.profile.modes[s_Mode];
            Undo.RecordObject(hit.generator, "Chisel Paint");
            var paint = ChiselPaintBrush.PaintFor(hit.generator, hit.surfaceIndex, mode, s_CellSize);
            if (paint == null)
                return;
            var target = mode.kind == ChiselPaintKind.Color ? (Vector4)s_Color : Vector4.one * s_Value;
            var scale  = hit.generator.transform.lossyScale;
            var radius = s_Radius / Mathf.Max(Mathf.Abs(scale.x), Mathf.Max(Mathf.Abs(scale.y), Mathf.Abs(scale.z)), 1e-5f);
            var center = hit.generator.transform.InverseTransformPoint(hit.point);
            if (ChiselPaintBrush.Dab(paint, mode, s_Operation, target, center, radius, s_Strength, s_Hardness, hit.profile.GroupOf(s_Mode)))
            {
                hit.generator.PaintChanged();
                EditorUtility.SetDirty(hit.generator);
            }
        }

        static void DrawPanel(Hit hit)
        {
            Handles.BeginGUI();
            s_Window = GUILayout.Window(typeof(ChiselPaintTool).GetHashCode(), s_Window, id =>
            {
                var profile = hit.profile;
                if (!hit.valid)
                    EditorGUILayout.HelpBox("Hover over a surface to paint it.", MessageType.None);
                else if (profile == null || profile.modes.Count == 0)
                    EditorGUILayout.HelpBox($"The shader {(hit.shader ? hit.shader.name : "(none)")} has no paint profile (Assets > Create > Chisel > Paint Profile).", MessageType.Info);
                else
                {
                    var names = new GUIContent[profile.modes.Count];
                    for (int i = 0; i < names.Length; i++)
                        names[i] = new GUIContent(string.IsNullOrEmpty(profile.modes[i].name) ? $"Mode {i}" : profile.modes[i].name);
                    s_Mode = Mathf.Clamp(EditorGUILayout.Popup(kModeContent, s_Mode, names), 0, names.Length - 1);
                    var mode = profile.modes[s_Mode];
                    if (mode.kind == ChiselPaintKind.Color)
                        s_Color = EditorGUILayout.ColorField(kColorContent, s_Color, true, true, false);
                    else
                        s_Value = EditorGUILayout.Slider(new GUIContent(names[s_Mode].text), s_Value, mode.range.x, mode.range.y);
                }
                s_Operation = (ChiselPaintOperation)EditorGUILayout.EnumPopup(kOperationContent, s_Operation);
                s_Radius    = Mathf.Max(0.01f, EditorGUILayout.FloatField(kRadiusContent, s_Radius));
                s_Strength  = EditorGUILayout.Slider(kStrengthContent, s_Strength, 0, 1);
                s_Hardness  = EditorGUILayout.Slider(kHardnessContent, s_Hardness, 0, 1);
                s_CellSize  = Mathf.Max(0.01f, EditorGUILayout.FloatField(kCellSizeContent, s_CellSize));
                using (new EditorGUI.DisabledScope(!s_Last.valid))
                {
                    if (GUILayout.Button(kFillContent) && LastPaint(out var paint, out var mode))
                    {
                        var target = mode.kind == ChiselPaintKind.Color ? (Vector4)s_Color : Vector4.one * s_Value;
                        ChiselPaintBrush.Fill(paint, mode, target, s_Last.profile.GroupOf(s_Mode));
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
                GUI.DragWindow();
            }, "Chisel Paint");
            Handles.EndGUI();
        }
    }
}
