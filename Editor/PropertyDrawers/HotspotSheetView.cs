using System;
using System.Collections.Generic;
using Chisel.Core;
using UnityEditor;
using UnityEngine;

namespace Chisel.Editors
{
    // A hotspot sheet in a part of a popup: fitted to it or at one pixel per texel, zoomed with the wheel around the
    // mouse and panned with a middle drag; the texture as it is with plain tile outlines
    sealed class HotspotSheetView
    {
        internal const float kMinScale = 1f / 64;
        internal const float kMaxScale = 32;
        const float kWheelStep = 1.15f;

        static readonly Color kBackground = new(0.16f, 0.16f, 0.16f);
        static readonly Color kNoTexture  = new(0.3f, 0.3f, 0.3f);
        internal static readonly Color kLine    = new(1, 1, 1, 0.55f);
        internal static readonly Color kHover   = new(1, 0.75f, 0.1f, 1);
        internal static readonly Color kPinned  = new(0.25f, 0.5f, 0.85f, 0.55f);
        internal static readonly Color kPinMark = new(0.45f, 0.7f, 1, 1);

        // pixels of the view a texel takes, and where the sheet's top left is in the view
        public float   scale = 1;
        public Vector2 origin;
        // fitted to the view whenever it is laid out, until zoomed or panned
        public bool    fit = true;
        Rect lastView;
        Vector2Int lastSize;

        // The scale that shows a sheet of this size whole in a view of this size, a little in from its sides
        internal static float FitScale(Vector2 view, Vector2Int size, float margin = 8)
        {
            if (size.x <= 0 || size.y <= 0)
                return 1;
            return Mathf.Clamp(Mathf.Min((view.x - 2 * margin) / size.x, (view.y - 2 * margin) / size.y), kMinScale, kMaxScale);
        }

        // Lays the sheet out in the view: fitted while fit is on
        public void Layout(Rect view, Vector2Int size)
        {
            lastView = view;
            lastSize = size;
            if (!fit)
                return;
            scale  = FitScale(view.size, size);
            origin = view.size * 0.5f - (Vector2)size * (scale * 0.5f);
        }

        public void Fit()
        {
            fit = true;
            Layout(lastView, lastSize);
        }

        // One pixel per texel, around the middle of the view
        public void OneToOne() => ZoomAt(lastView.size * 0.5f, 1 / Mathf.Max(scale, kMinScale));

        public bool IsOneToOne => !fit && Mathf.Approximately(scale, 1);

        public Vector2 ToView(Vector2 pixel)  => origin + pixel * scale;
        public Vector2 ToPixel(Vector2 local) => (local - origin) / scale;

        // Zooms by a factor keeping the texel under a point of the view where it is
        public void ZoomAt(Vector2 local, float factor)
        {
            var pixel = ToPixel(local);
            scale = Mathf.Clamp(scale * factor, kMinScale, kMaxScale);
            origin = local - pixel * scale;
            fit = false;
        }

        public void Pan(Vector2 delta)
        {
            origin += delta;
            fit = false;
        }

        // The wheel and a middle drag, in the view's own coordinates; true when used
        public bool Events(Event current, Vector2 local, bool inside)
        {
            switch (current.type)
            {
                case EventType.ScrollWheel when inside:
                    ZoomAt(local, current.delta.y > 0 ? 1 / kWheelStep : kWheelStep);
                    current.Use();
                    return true;
                case EventType.MouseDrag when current.button == 2:
                    Pan(current.delta);
                    current.Use();
                    return true;
            }
            return false;
        }

        // The sheet's texture over the view's background, with a checkerboard under it for one with see through parts
        public void DrawTexture(Vector2Int size, Texture texture, Texture checker = null)
        {
            EditorGUI.DrawRect(new Rect(0, 0, lastView.width, lastView.height), kBackground);
            var sheet = new Rect(origin, (Vector2)size * scale);
            if (checker != null)
                GUI.DrawTextureWithTexCoords(sheet, checker, new Rect(0, 0, sheet.width / 16, sheet.height / 16), false);
            if (texture != null)
                GUI.DrawTexture(sheet, texture, ScaleMode.StretchToFill, true);
            else if (checker == null)
                EditorGUI.DrawRect(sheet, kNoTexture);
        }

        // A tile's corners in the view
        public Vector3[] PointsOf(in HotspotTile tile)
        {
            var points = new Vector3[tile.points.Length];
            for (int i = 0; i < points.Length; i++)
                points[i] = ToView(tile.points[i]);
            return points;
        }

        public static void Fill(Vector3[] points, Color color, bool shaped)
        {
            Handles.color = color;
            ChiselHotspotLayoutWindow.Fill(points, shaped);
        }

        public static void Outline(Vector3[] points, Color color, float width)
        {
            Handles.color = color;
            var loop = new Vector3[points.Length + 1];
            points.CopyTo(loop, 0);
            loop[points.Length] = points[0];
            Handles.DrawAAPolyLine(width, loop);
        }

        public static void Dashed(Vector3[] points, Color color)
        {
            Handles.color = color;
            var segments = new Vector3[points.Length * 2];
            for (int i = 0; i < points.Length; i++)
            {
                segments[i * 2]     = points[i];
                segments[i * 2 + 1] = points[(i + 1) % points.Length];
            }
            Handles.DrawDottedLines(segments, 4);
        }

        public static Rect BoundsOf(Vector3[] points)
        {
            var min = (Vector2)points[0]; var max = min;
            foreach (var point in points) { min = Vector2.Min(min, point); max = Vector2.Max(max, point); }
            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }

        GUIStyle tag;
        GUIStyle Tag => tag ??= new GUIStyle(EditorStyles.whiteMiniLabel)
        {
            alignment = TextAnchor.MiddleCenter, padding = new RectOffset(4, 4, 1, 1),
            normal = { background = Texture2D.whiteTexture, textColor = Color.white }
        };

        // A small dark tag with text, centered on a point or against a corner
        public void Label(Vector2 at, string text, TextAnchor anchor = TextAnchor.MiddleCenter)
        {
            var content = new GUIContent(text);
            var size = Tag.CalcSize(content);
            var rect = anchor switch
            {
                TextAnchor.LowerRight => new Rect(at.x - size.x - 2, at.y - size.y - 2, size.x, size.y),
                _                     => new Rect(at.x - size.x * 0.5f, at.y - size.y * 0.5f, size.x, size.y),
            };
            var color = GUI.backgroundColor;
            GUI.backgroundColor = new Color(0.08f, 0.08f, 0.08f, 0.9f);
            GUI.Label(rect, content, Tag);
            GUI.backgroundColor = color;
        }

        // A pin, its point down at the bottom of this rect's middle
        public static void PinMark(Rect rect, Color color)
        {
            var x = Mathf.Round(rect.center.x);
            var y = rect.y;
            EditorGUI.DrawRect(new Rect(x - 3, y,      7, 2), color);
            EditorGUI.DrawRect(new Rect(x - 2, y + 2,  5, 4), color);
            EditorGUI.DrawRect(new Rect(x - 4, y + 6,  9, 1), color);
            EditorGUI.DrawRect(new Rect(x,     y + 7,  1, 4), color);
        }

        // The tile nearest one in a direction (by their middles), for the arrow keys; the same when there's none
        internal static int NextTile(ChiselHotspotLayout layout, int from, Vector2 direction, Func<HotspotTile, bool> skip = null)
        {
            if (layout == null || layout.tiles == null || layout.tiles.Count == 0)
                return from;
            var index = layout.IndexOfTile(from);
            if (index < 0)
            {
                foreach (var tile in layout.tiles)
                    if (skip == null || !skip(tile)) return tile.id;
                return from;
            }
            var start = MiddleOf(layout.tiles[index]);
            var best = from;
            var bestCost = float.PositiveInfinity;
            foreach (var tile in layout.tiles)
            {
                if (tile.id == from || (skip != null && skip(tile)))
                    continue;
                var offset = MiddleOf(tile) - start;
                var along = Vector2.Dot(offset, direction);
                if (along <= 0)
                    continue;
                var across = Mathf.Abs(offset.x * direction.y - offset.y * direction.x);
                var cost = along + 2 * across;
                if (cost < bestCost) { bestCost = cost; best = tile.id; }
            }
            return best;
        }

        static Vector2 MiddleOf(in HotspotTile tile)
        {
            if (tile.points == null || tile.points.Length == 0)
                return Vector2.zero;
            var min = tile.points[0]; var max = tile.points[0];
            foreach (var point in tile.points) { min = Vector2Int.Min(min, point); max = Vector2Int.Max(max, point); }
            return (Vector2)(min + max) * 0.5f;
        }

        // A two by two checkerboard to repeat under see through textures; destroyed by its owner
        internal static Texture2D CreateChecker()
        {
            var checker = new Texture2D(2, 2, TextureFormat.RGBA32, false)
            {
                hideFlags = HideFlags.HideAndDontSave, filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Repeat
            };
            var dark = new Color(0.27f, 0.27f, 0.27f); var light = new Color(0.36f, 0.36f, 0.36f);
            checker.SetPixels(new[] { dark, light, light, dark });
            checker.Apply();
            return checker;
        }
    }

    // A popup resized from its bottom right corner, at least a minimum size, its size remembered per user
    abstract class ResizablePopup : PopupWindowContent
    {
        const float kGrip = 14;

        readonly string sizeKey;
        readonly Vector2 minSize;
        Vector2 size;
        bool resizing;
        Vector2 resizeStart, sizeStart;

        protected ResizablePopup(string sizeKey, Vector2 minSize, Vector2 defaultSize)
        {
            this.sizeKey = sizeKey;
            this.minSize = minSize;
            size = ClampSize(ReadSize(sizeKey, defaultSize), minSize, MaxSize);
        }

        // as large as the main editor window, at most
        static Vector2 MaxSize
        {
            get
            {
                var main = EditorGUIUtility.GetMainWindowPosition();
                return main.width > 0 && main.height > 0 ? main.size * 0.9f : new Vector2(4096, 4096);
            }
        }

        internal static Vector2 ClampSize(Vector2 size, Vector2 min, Vector2 max)
        {
            return new Vector2(Mathf.Clamp(size.x, min.x, Mathf.Max(min.x, max.x)), Mathf.Clamp(size.y, min.y, Mathf.Max(min.y, max.y)));
        }

        internal static Vector2 ReadSize(string key, Vector2 fallback)
        {
            var x = EditorPrefs.GetFloat(key + ".width", 0);
            var y = EditorPrefs.GetFloat(key + ".height", 0);
            return x > 0 && y > 0 ? new Vector2(x, y) : fallback;
        }

        internal static void WriteSize(string key, Vector2 size)
        {
            EditorPrefs.SetFloat(key + ".width", size.x);
            EditorPrefs.SetFloat(key + ".height", size.y);
        }

        public Vector2 Size => size;

        public override Vector2 GetWindowSize() => size;

        // The grip in the bottom right corner; dragging it resizes, and the size is remembered
        protected void ResizeGrip(Rect rect)
        {
            var grip = new Rect(rect.xMax - kGrip, rect.yMax - kGrip, kGrip, kGrip);
            var id = GUIUtility.GetControlID(FocusType.Passive);
            var current = Event.current;
            EditorGUIUtility.AddCursorRect(grip, MouseCursor.ResizeUpLeft);
            switch (current.GetTypeForControl(id))
            {
                case EventType.MouseDown when current.button == 0 && grip.Contains(current.mousePosition):
                    GUIUtility.hotControl = id;
                    resizing = true;
                    resizeStart = GUIUtility.GUIToScreenPoint(current.mousePosition);
                    sizeStart = size;
                    current.Use();
                    break;
                case EventType.MouseDrag when GUIUtility.hotControl == id && resizing:
                    size = ClampSize(sizeStart + GUIUtility.GUIToScreenPoint(current.mousePosition) - resizeStart, minSize, MaxSize);
                    current.Use();
                    editorWindow?.Repaint();
                    break;
                case EventType.MouseUp when GUIUtility.hotControl == id:
                    GUIUtility.hotControl = 0;
                    resizing = false;
                    WriteSize(sizeKey, size);
                    current.Use();
                    break;
                case EventType.Repaint:
                    Handles.color = new Color(1, 1, 1, 0.35f);
                    Handles.DrawLine(new Vector3(grip.xMax - 3, grip.yMax - 10), new Vector3(grip.xMax - 10, grip.yMax - 3));
                    Handles.DrawLine(new Vector3(grip.xMax - 3, grip.yMax - 6),  new Vector3(grip.xMax - 6,  grip.yMax - 3));
                    break;
            }
        }

        // The page's height for a sheet across the popup's width, with the rest of the popup around it
        protected static float HeightFor(Vector2Int sheet, float width, float around)
        {
            if (sheet.x <= 0 || sheet.y <= 0)
                return width + around;
            return width * sheet.y / sheet.x + around;
        }

        // the size it opens at when none is remembered
        protected void SetSizeIfNotRemembered(Vector2 opening)
        {
            if (ReadSize(sizeKey, Vector2.zero) == Vector2.zero)
                size = ClampSize(opening, minSize, MaxSize);
        }
    }
}
