using Chisel.Components;
using Chisel.Core;
using UnityEditor;
using UnityEngine;

namespace Chisel.Editors
{
    // New brushes start with the UV scale set in Chisel's preferences, on every surface
    public static class ChiselDefaultUVScale
    {
        /// <summary>Gives every surface of a new generator the default UV scale, keeping its translation and rotation. It
        /// sets the scale rather than multiplying it, so giving it again changes nothing.</summary>
        public static void GiveTo(ChiselGeneratorComponent generator)
        {
            if (!generator)
                return;
            var defaultScale = ChiselEditorSettings.DefaultUVScale;
            if (generator.SurfaceDefinition?.surfaces == null)
                generator.OnValidate();
            var surfaces = generator.SurfaceDefinition?.surfaces;
            if (surfaces == null)
                return;
            var changed = false;
            foreach (var surface in surfaces)
            {
                if (surface == null)
                    continue;
                var uv0 = surface.surfaceDetails.UV0;
                uv0.Decompose(out var translation, out var rotation, out var scale);
                if (Mathf.Approximately(scale.x, defaultScale) && Mathf.Approximately(scale.y, defaultScale))
                    continue;
                surface.surfaceDetails.UV0 = UVMatrix.TRS(translation, uv0.PlaneNormal, rotation, new Vector2(defaultScale, defaultScale));
                changed = true;
            }
            if (changed)
            {
                generator.OnValidate();
                EditorUtility.SetDirty(generator);
            }
        }
    }
}
