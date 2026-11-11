using UnityEditor;
using UnityEngine;

namespace Chisel.Editors
{
    // The colors Chisel's editor windows and inspectors draw with, each in one place. Unlike ChiselColors they aren't
    // set by the user. Those drawn on the window have a dark and a light version and follow Unity's skin; those only
    // drawn over a texture or picture (lines, tags, pins) are the same in both
    public static class ChiselEditorColors
    {
        static bool ProSkin => EditorGUIUtility.isProSkin;

        /// <summary>A selected thumbnail or cell, drawn over it</summary>
        public static Color Selection(float alpha) => new(0.24f, 0.48f, 0.9f, alpha);

        /// <summary>The background of the chosen row or cell of a list, with its text on it</summary>
        public static Color ChosenRow => ChosenRowFor(ProSkin);
        internal static Color ChosenRowFor(bool proSkin) => proSkin ? new Color(0.17f, 0.36f, 0.53f, 1) : new Color(0.55f, 0.7f, 0.95f, 1);

        /// <summary>A row under the mouse, drawn over it</summary>
        public static Color RowHover => RowHoverFor(ProSkin);
        internal static Color RowHoverFor(bool proSkin) => proSkin ? new Color(1, 1, 1, 0.08f) : new Color(0, 0, 0, 0.06f);

        /// <summary>What is applied or pinned, marked on top of it</summary>
        public static Color Accent => AccentFor(ProSkin);
        internal static Color AccentFor(bool proSkin) => proSkin ? new Color(0.45f, 0.7f, 1f, 1) : new Color(0.1f, 0.35f, 0.75f, 1);

        /// <summary>A tile or mark under the mouse, and a material's hotspot mark</summary>
        public static Color Highlight => HighlightFor(ProSkin);
        internal static Color HighlightFor(bool proSkin) => proSkin ? new Color(1f, 0.75f, 0.1f, 1) : new Color(0.65f, 0.3f, 0f, 1);

        /// <summary>Text that stands out from the labels around it</summary>
        public static Color WarmText => WarmTextFor(ProSkin);
        internal static Color WarmTextFor(bool proSkin) => proSkin ? new Color(1f, 0.65f, 0.3f, 1) : new Color(0.65f, 0.3f, 0f, 1);

        /// <summary>A grip that can be dragged</summary>
        public static Color Grip => GripFor(ProSkin);
        internal static Color GripFor(bool proSkin) => proSkin ? new Color(1, 1, 1, 0.25f) : new Color(0, 0, 0, 0.25f);

        /// <summary>A line between two parts of a window</summary>
        public static Color Separator => new(0, 0, 0, 0.3f);

        /// <summary>The background of a header or status bar</summary>
        public static Color Header => new(0, 0, 0, 0.15f);

        /// <summary>The background of a list or panel set off from the rest of the window</summary>
        public static Color Panel => new(0, 0, 0, 0.12f);

        /// <summary>Behind a thumbnail while it has no picture yet</summary>
        public static Color ThumbnailBackground => new(0, 0, 0, 0.2f);

        /// <summary>Over what can't be picked</summary>
        public static Color Dim => new(0, 0, 0, 0.6f);

        /// <summary>Behind a texture sheet and its tiles</summary>
        public static Color Canvas => CanvasFor(ProSkin);
        internal static Color CanvasFor(bool proSkin) => proSkin ? new Color(0.16f, 0.16f, 0.16f, 1) : new Color(0.6f, 0.6f, 0.6f, 1);

        /// <summary>Where a texture sheet would be when it has no texture; dark enough for the tiles' light lines on it</summary>
        public static Color NoTexture => NoTextureFor(ProSkin);
        internal static Color NoTextureFor(bool proSkin) => proSkin ? new Color(0.3f, 0.3f, 0.3f, 1) : new Color(0.45f, 0.45f, 0.45f, 1);

        /// <summary>Behind a label drawn on a texture sheet, with light text on it</summary>
        public static Color Tag(float alpha) => new(0.08f, 0.08f, 0.08f, alpha);

        /// <summary>Behind a picture in an inspector, as the window's own background</summary>
        public static Color PictureBackground(float alpha = 1) => PictureBackgroundFor(ProSkin, alpha);
        internal static Color PictureBackgroundFor(bool proSkin, float alpha = 1) => proSkin ? new Color(0.22f, 0.22f, 0.22f, alpha) : new Color(0.76f, 0.76f, 0.76f, alpha);

        /// <summary>Sizes drawn in an inspector's picture</summary>
        public static Color Measurement => MeasurementFor(ProSkin);
        internal static Color MeasurementFor(bool proSkin) => proSkin ? new Color(0.45f, 0.75f, 1f) : new Color(0.1f, 0.35f, 0.75f);
    }
}
