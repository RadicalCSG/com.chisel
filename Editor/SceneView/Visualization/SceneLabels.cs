﻿using System;
using System.Collections.Generic;
using UnityEngine;

namespace Chisel.Editors
{
    public partial class SceneHandles
    {
        struct StyleLookup : IEqualityComparer<StyleLookup>, IEquatable<StyleLookup>
        {
            public int          padding;
            public int          fontSize;
            public FontStyle    fontStyle;
            public Color        color;

            public bool Equals(StyleLookup x, StyleLookup y) { return x.padding == y.padding && x.fontSize == y.fontSize && x.fontStyle == y.fontStyle && x.color == y.color; }
            public bool Equals(StyleLookup other) { return Equals(this, other); }

            public int GetHashCode(StyleLookup obj)
            {
                return  (padding.GetHashCode() + fontSize.GetHashCode() + fontStyle.GetHashCode() + color.GetHashCode()) ^ 
                        (padding.GetHashCode() * fontSize.GetHashCode() * fontStyle.GetHashCode() * color.GetHashCode());
            }
        }

        readonly static Dictionary<StyleLookup, GUIStyle> s_LabelColorStyle = new();
        static GUIStyle GetLabelStyle(Color color, int padding, int fontSize = 11, FontStyle fontStyle = FontStyle.Normal)
        {
            var lookup = new StyleLookup() { padding = padding, color = color, fontSize = fontSize, fontStyle = fontStyle };
            if (s_LabelColorStyle.TryGetValue(lookup, out GUIStyle style))
                return style;

            style = new UnityEngine.GUIStyle
            {
                alignment   = UnityEngine.TextAnchor.UpperLeft,
                fontSize    = fontSize,

                // some eyeballed offsets because CalcSize returns a non-centered rect
                padding = new RectOffset
                {
                    left    = 4 + padding,
                    right   = padding,
                    top     = 1 + padding,
                    bottom  = 4 + padding
                },
                fontStyle = FontStyle.Bold
            };
            style.normal.textColor  = SceneHandles.Color;

            s_LabelColorStyle[lookup] = style;
            return style;
        }

        public static Rect DrawLabel(UnityEngine.Vector3 position, UnityEngine.Vector3 alignmentDirection, string text, int fontSize = 11, FontStyle fontStyle = FontStyle.Normal, int padding = 4)
        {
            var style = GetLabelStyle(SceneHandles.Color, padding, fontSize, fontStyle);
            return DrawLabel(position, alignmentDirection, new GUIContent(text), style);
        }

        public static Rect DrawLabel(UnityEngine.Vector3 position, UnityEngine.Vector3 alignmentDirection, GUIContent content, int fontSize = 11, FontStyle fontStyle = FontStyle.Normal, int padding = 4)
        {
            var style = GetLabelStyle(SceneHandles.Color, padding, fontSize, fontStyle);
            return DrawLabel(position, alignmentDirection, content, style);
        }

        static Rect GetLabelRect(UnityEngine.Vector3 position, UnityEngine.Vector3 alignmentDirection, GUIContent content, GUIStyle style)
        {
            var size		= style.CalcSize(content);
            var halfSize	= size * 0.5f;
            var screenpos	= UnityEditor.HandleUtility.WorldToGUIPoint(position);
            var screendir	= (UnityEditor.HandleUtility.WorldToGUIPoint(position + alignmentDirection) - screenpos).normalized;

            // align on the rect around the text in the direction of alignmentDirection
            screenpos.x += (screendir.x - 1) * halfSize.x;
            screenpos.y += (screendir.y - 1) * halfSize.y;

            return new Rect(screenpos.x, screenpos.y, size.x, size.y);
        }

        public static Rect DrawLabel(UnityEngine.Vector3 position, UnityEngine.Vector3 alignmentDirection, GUIContent content, GUIStyle style)
        {
            if (Event.current.type == EventType.Repaint)
            { 
                var matrix  = SceneHandles.Matrix;
                var pt      = UnityEngine.Camera.current.WorldToViewportPoint(matrix.MultiplyPoint(position));

                // cull if behind camera
                if (pt.z < 0)
                    return default;

                var rect = GetLabelRect(position, alignmentDirection, content, style);
                SceneHandles.BeginGUI();
                {
                    GUI.Label(rect, content, style);
                }
                SceneHandles.EndGUI();
                return rect;
            } else
            if (Event.current.type == EventType.Layout)
                return GetLabelRect(position, alignmentDirection, content, style);
            return default;
        }


        public static bool ClickableLabel(UnityEngine.Vector3 position, UnityEngine.Vector3 alignmentDirection, string text, int fontSize = 11, FontStyle fontStyle = FontStyle.Normal, int padding = 4)
        {
            return ClickableLabel(position, alignmentDirection, new GUIContent(text), fontSize, fontStyle, padding);
        }

        static float s_DragDistance = 0;
        const float kMinimumClickDistance = 8;
		readonly static int kClickableLabelHash = "ClickableLabel".GetHashCode();
        public static bool ClickableLabel(UnityEngine.Vector3 position, UnityEngine.Vector3 alignmentDirection, GUIContent content, int fontSize = 11, FontStyle fontStyle = FontStyle.Normal, int padding = 4)
        {
            var id = GUIUtility.GetControlID(kClickableLabelHash, FocusType.Keyboard);
            var prevColor = UnityEditor.Handles.color;
            if (Event.current.type == EventType.Repaint)
            {
                var focusControl        = Chisel.Editors.SceneHandleUtility.FocusControl;
                var focusColor          = (focusControl == id) ? Chisel.Editors.SceneHandles.SelectedColor : prevColor;
                UnityEditor.Handles.color = SceneHandles.Disabled ? Color.Lerp(focusColor, Chisel.Editors.SceneHandles.StaticColor, Chisel.Editors.SceneHandles.StaticBlend) : focusColor;
            }

            var style = GetLabelStyle(SceneHandles.Color, padding, fontSize, fontStyle);
            var rect = DrawLabel(position, alignmentDirection, content, style);

            UnityEditor.Handles.color = prevColor;

            if (SceneHandles.Disabled)
                return false;

            var evt     = Event.current;
            var type    = evt.GetTypeForControl(id);
            switch (type)
            {
                case EventType.Layout:
                {
                    if (InCameraOrbitMode)
                        break;

                    if (rect.Contains(Event.current.mousePosition))
                        UnityEditor.HandleUtility.AddControl(id, 3);
                    break;
                }
                case EventType.MouseDown:
                {
                    if (InCameraOrbitMode)
                        break;

                    if (GUIUtility.hotControl != 0)
                        break;

                    if ((UnityEditor.HandleUtility.nearestControl != id || evt.button != 0) &&
                        (GUIUtility.keyboardControl != id || evt.button != 2))
                        break;

                    GUIUtility.hotControl = GUIUtility.keyboardControl = id;
                    evt.Use();
                    UnityEditor.EditorGUIUtility.SetWantsMouseJumping(1);
                    s_DragDistance = 0;
                    break;
                }
                case EventType.MouseDrag:
                {
                    if (GUIUtility.hotControl != id)
                        break;

                    s_DragDistance += evt.delta.magnitude;
                    evt.Use();
                    break;
                }
                case EventType.MouseMove:
                {
                    s_DragDistance = 0;
                    break;
                }
                case EventType.MouseUp:
                {
                    if (GUIUtility.hotControl != id || (evt.button != 0 && evt.button != 2))
                        break;

                    GUIUtility.hotControl = 0;
                    GUIUtility.keyboardControl = 0;
                    evt.Use();
                    UnityEditor.EditorGUIUtility.SetWantsMouseJumping(0);
                    var result = s_DragDistance < kMinimumClickDistance;
                    s_DragDistance = 0;
                    return result;
                }
            }
            return false;
        }
    }
}
