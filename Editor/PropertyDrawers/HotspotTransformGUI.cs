using System.Collections.Generic;
using Chisel.Components;
using Chisel.Core;
using UnityEditor;
using UnityEditorInternal;
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
        static readonly GUIContent kPreviousContents    = new("<", "The previous variation: another of the tiles that fit this surface equally well, or another of the material's variants");
        static readonly GUIContent kNextContents        = new(">", "The next variation: another of the tiles that fit this surface equally well, or another of the material's variants");
        const float kStepWidth = 22;
        internal static readonly GUIContent kLeftContents  = new("-90", "A quarter turn clockwise");
        internal static readonly GUIContent kRightContents = new("+90", "A quarter turn counter-clockwise");

        public static ChiselHotspotLayout LayoutOf(Material material)
        {
            if (material == null)
                return null;
            var metadata = material.GetMetadataOfType<ChiselSurfaceMetadata>();
            return metadata != null ? metadata.hotspotLayout : null;
        }

        public static float Height => 4 * EditorGUIUtility.singleLineHeight + 3 * kSpacing;

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

        // variations: how many the surfaces have (ChiselHotspotGroupEdit.MostVariations); Next Variation is off at 1, and
        // for a pinned tile when it is not known (-1)
        // the tile a picker chose (HotspotTilePicker), applied to its property on its next draw
        static (string path, Object target, int id)? s_Picked;

        // The textures a material and its variants are drawn with, for the tile picker
        public static List<Texture> TexturesOf(Material material, ChiselHotspotLayout layout)
        {
            var textures = new List<Texture>();
            void Add(Material m)
            {
                var texture = m != null && m.mainTexture != null ? m.mainTexture : null;
                if (texture != null && !textures.Contains(texture))
                    textures.Add(texture);
            }
            Add(material);
            var metadata = material != null ? material.GetMetadataOfType<ChiselSurfaceMetadata>() : null;
            if (metadata != null && metadata.hotspotVariants != null)
            {
                foreach (var variant in metadata.hotspotVariants)
                    Add(variant);
            }
            if (textures.Count == 0 && layout.texture != null)
                textures.Add(layout.texture);
            return textures;
        }

        // fits: the ids of the tiles the surface can take (ChiselHotspotGroupEdit.FitsOf), the others greyed out in the
        // picker; textures: what it shows (TexturesOf); both asked for when the picker opens
        // descriptionIndex: the surface's, which the seed is hashed with (the variation the arrows step from)
        public static void OnGUI(Rect position, SerializedProperty hotspotProp, ChiselHotspotLayout layout, bool hasLabel = true, int variations = -1,
                                 System.Func<IReadOnlyList<Texture>> textures = null, System.Func<HashSet<int>> fits = null, int descriptionIndex = 0)
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
                var previous = new Rect(field.xMax - 2 * kStepWidth, field.y, kStepWidth, field.height);
                var next     = new Rect(field.xMax - kStepWidth, field.y, kStepWidth, field.height);
                field.width -= 2 * kStepWidth + 2;
                using (new EditorGUI.DisabledScope(variations >= 0 ? variations <= 1 : (pinned.intValue != 0 || pinned.hasMultipleDifferentValues)))
                {
                    var step = 0;
                    if (GUI.Button(previous, kPreviousContents, EditorStyles.miniButtonLeft)) step = -1;
                    if (GUI.Button(next, kNextContents, EditorStyles.miniButtonRight))         step = 1;
                    if (step != 0)
                    {
                        seed.uintValue = StepSeed(seed.uintValue, descriptionIndex, variations, step);
                        GUI.changed = true;
                    }
                }
                if (s_Picked.HasValue && s_Picked.Value.path == pinned.propertyPath && s_Picked.Value.target == pinned.serializedObject.targetObject)
                {
                    pinned.intValue = s_Picked.Value.id;
                    s_Picked = null;
                    GUI.changed = true;
                }
                var current = System.Array.IndexOf(ids, pinned.intValue);
                var shown = pinned.hasMultipleDifferentValues ? new GUIContent("\u2014") : names[Mathf.Max(0, current)];
                if (EditorGUI.DropdownButton(field, shown, FocusType.Keyboard))
                {
                    var path = pinned.propertyPath; var target = pinned.serializedObject.targetObject;
                    PopupWindow.Show(field, new HotspotTilePicker(layout, textures?.Invoke() ?? new List<Texture> { layout.texture }, fits?.Invoke(),
                                                                  pinned.hasMultipleDifferentValues ? -1 : pinned.intValue,
                                                                  id => { s_Picked = (path, target, id); InternalEditorUtility.RepaintAllViews(); }));
                }

            }
            finally
            {
                EditorGUI.showMixedValue = prevMixed;
                EditorGUI.indentLevel = prevIndent;
            }
        }

        internal static uint NextSeed(uint seed)     => seed * 1664525u + 1013904223u;
        internal static uint PreviousSeed(uint seed) => (seed - 1013904223u) * 4276115653u;

        // The variation a surface's seed picks, as matching does (HotspotMatcher.Pick, the job hashing the seed with the
        // surface's description index), of variations
        static uint VariationOf(uint seed, int descriptionIndex, int variations)
        {
            var hashed = Unity.Mathematics.math.hash(new Unity.Mathematics.uint2(seed, (uint)descriptionIndex));
            return Unity.Mathematics.math.hash(new Unity.Mathematics.uint2(hashed, 0x9E3779B9u)) % (uint)variations;
        }

        // The seed of the next (step 1) or previous (-1) variation, so the arrows go through them in order; when the
        // number of variations is not known, just the next or previous seed
        internal static uint StepSeed(uint seed, int descriptionIndex, int variations, int step)
        {
            if (variations <= 1)
                return step > 0 ? NextSeed(seed) : PreviousSeed(seed);
            var target = (uint)((VariationOf(seed, descriptionIndex, variations) + variations + step) % variations);
            var start = seed;
            for (int i = 0; i < 1 << 16; i++)
            {
                seed = step > 0 ? NextSeed(seed) : PreviousSeed(seed);
                if (VariationOf(seed, descriptionIndex, variations) == target)
                    return seed;
            }
            return step > 0 ? NextSeed(start) : PreviousSeed(start);
        }
    }
}
