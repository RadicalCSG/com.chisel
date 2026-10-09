using Chisel.Components;
using Chisel.Core;
using UnityEditor;
using UnityEngine;

namespace Chisel.Editors
{
    // A surface's texture placement, as the surface options and the brush inspector show it: for a hotspot material its
    // HotspotSurface in the rows a planar mapping has (translation, scale, rotation) and the tile it keeps
    public static class HotspotTransformGUI
    {
        const float kSpacing = 2;
        const float kQuarterWidth = 34;

        static readonly GUIContent kTranslationContents = new("Translation", "How far a repeating tile's repeats move along the texture's right and up, in repeats");
        static readonly GUIContent kScaleContents       = new("Scale", "How many of the tile's pixels a unit of the surface takes, as the tile's pixels per unit times this: 2 draws the texture half as large. A negative X mirrors it left to right, a negative Y top to bottom");
        static readonly GUIContent kRotationContents    = new("Rotation", "Degrees the texture turns around the surface's normal, counter-clockwise seen from in front");
        static readonly GUIContent kTileContents        = new("Hotspot Tile", "The tile of the material's hotspot layout this surface keeps; Automatic lets matching choose");
        static readonly GUIContent kVariationContents   = new("Next Variation", "Another of the tiles that fit this surface equally well");
        internal static readonly GUIContent kLeftContents  = new("-90", "A quarter turn clockwise");
        internal static readonly GUIContent kRightContents = new("+90", "A quarter turn counter-clockwise");

        public static ChiselHotspotLayout LayoutOf(Material material)
        {
            if (material == null)
                return null;
            var metadata = material.GetMetadataOfType<ChiselSurfaceMetadata>();
            return metadata != null ? metadata.hotspotLayout : null;
        }

        public static float Height => 5 * EditorGUIUtility.singleLineHeight + 4 * kSpacing;

        // A rotation field with quarter turns beside it; the angle kept within 0 to 360 when a button turned it
        internal static float RotationField(Rect position, float rotation)
        {
            var field = new Rect(position.x, position.y, position.width - 2 * kQuarterWidth, position.height);
            rotation = EditorGUI.FloatField(field, GUIContent.none, rotation);
            var turn = 0;
            if (GUI.Button(new Rect(field.xMax, position.y, kQuarterWidth, position.height), kLeftContents, EditorStyles.miniButtonLeft))   turn = -90;
            if (GUI.Button(new Rect(field.xMax + kQuarterWidth, position.y, kQuarterWidth, position.height), kRightContents, EditorStyles.miniButtonRight)) turn = 90;
            if (turn != 0)
            {
                GUI.changed = true;
                rotation = Mathf.Repeat(rotation + turn, 360);
            }
            return rotation;
        }

        static Rect Row(ref Rect position, GUIContent label, bool hasLabel)
        {
            var row = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);
            position.y += row.height + kSpacing;
            var id = GUIUtility.GetControlID(FocusType.Keyboard);
            var prevIndent = EditorGUI.indentLevel;
            var field = EditorGUI.PrefixLabel(row, id, hasLabel ? label : GUIContent.none);
            EditorGUI.indentLevel = prevIndent;
            return field;
        }

        public static void OnGUI(Rect position, SerializedProperty hotspotProp, ChiselHotspotLayout layout, bool hasLabel = true)
        {
            var offsetX  = hotspotProp.FindPropertyRelative(nameof(HotspotSurface.offset) + ".x");
            var offsetY  = hotspotProp.FindPropertyRelative(nameof(HotspotSurface.offset) + ".y");
            var density  = hotspotProp.FindPropertyRelative(nameof(HotspotSurface.density));
            var mirror   = hotspotProp.FindPropertyRelative(nameof(HotspotSurface.mirror));
            var mirrorY  = hotspotProp.FindPropertyRelative(nameof(HotspotSurface.mirrorY));
            var rotation = hotspotProp.FindPropertyRelative(nameof(HotspotSurface.rotation));
            var pinned   = hotspotProp.FindPropertyRelative(nameof(HotspotSurface.pinnedTile));
            var seed     = hotspotProp.FindPropertyRelative(nameof(HotspotSurface.seed));

            var prevMixed  = EditorGUI.showMixedValue;
            var prevIndent = EditorGUI.indentLevel;
            try
            {
                var field = Row(ref position, kTranslationContents, hasLabel);
                EditorGUI.indentLevel = 0;
                EditorGUI.showMixedValue = offsetX.hasMultipleDifferentValues || offsetY.hasMultipleDifferentValues;
                EditorGUI.BeginChangeCheck();
                var offset = EditorGUI.Vector2Field(field, GUIContent.none, new Vector2(offsetX.floatValue, offsetY.floatValue));
                if (EditorGUI.EndChangeCheck())
                {
                    offsetX.floatValue = offset.x;
                    offsetY.floatValue = offset.y;
                }
                EditorGUI.indentLevel = prevIndent;

                // one density, its signs the mirrors
                field = Row(ref position, kScaleContents, hasLabel);
                EditorGUI.indentLevel = 0;
                var size = density.floatValue > 0 ? density.floatValue : 1;
                var scale = new Vector2(mirror.boolValue ? -size : size, mirrorY.boolValue ? -size : size);
                EditorGUI.showMixedValue = density.hasMultipleDifferentValues || mirror.hasMultipleDifferentValues || mirrorY.hasMultipleDifferentValues;
                EditorGUI.BeginChangeCheck();
                var newScale = EditorGUI.Vector2Field(field, GUIContent.none, scale);
                if (EditorGUI.EndChangeCheck())
                {
                    var magnitude = Mathf.Abs(newScale.x) != size ? Mathf.Abs(newScale.x) : Mathf.Abs(newScale.y);
                    density.floatValue = Mathf.Clamp(magnitude, 1.0f / 64, 64);
                    mirror.boolValue   = newScale.x < 0;
                    mirrorY.boolValue  = newScale.y < 0;
                }
                EditorGUI.indentLevel = prevIndent;

                field = Row(ref position, kRotationContents, hasLabel);
                EditorGUI.indentLevel = 0;
                EditorGUI.showMixedValue = rotation.hasMultipleDifferentValues;
                EditorGUI.BeginChangeCheck();
                var newRotation = RotationField(field, rotation.floatValue);
                if (EditorGUI.EndChangeCheck())
                    rotation.floatValue = newRotation;
                EditorGUI.indentLevel = prevIndent;

                var names = new GUIContent[layout.tiles.Count + 1];
                var ids   = new int[layout.tiles.Count + 1];
                names[0] = new GUIContent("Automatic");
                for (int i = 0; i < layout.tiles.Count; i++)
                {
                    var tile = layout.tiles[i];
                    names[i + 1] = new GUIContent(string.IsNullOrEmpty(tile.name) ? "Tile " + tile.id : tile.name);
                    ids[i + 1]   = tile.id;
                }
                field = Row(ref position, kTileContents, hasLabel);
                EditorGUI.indentLevel = 0;
                EditorGUI.showMixedValue = pinned.hasMultipleDifferentValues;
                var current = System.Array.IndexOf(ids, pinned.intValue);
                EditorGUI.BeginChangeCheck();
                var chosen = EditorGUI.Popup(field, Mathf.Max(0, current), names);
                if (EditorGUI.EndChangeCheck() && chosen >= 0)
                    pinned.intValue = ids[chosen];
                EditorGUI.showMixedValue = false;

                var button = new Rect(field.x, position.y, field.width, EditorGUIUtility.singleLineHeight);
                using (new EditorGUI.DisabledScope(pinned.intValue != 0 || pinned.hasMultipleDifferentValues))
                {
                    if (GUI.Button(button, kVariationContents))
                    {
                        seed.uintValue = NextSeed(seed.uintValue);
                        GUI.changed = true;
                    }
                }
            }
            finally
            {
                EditorGUI.showMixedValue = prevMixed;
                EditorGUI.indentLevel = prevIndent;
            }
        }

        internal static uint NextSeed(uint seed) => seed * 1664525u + 1013904223u;
    }
}
