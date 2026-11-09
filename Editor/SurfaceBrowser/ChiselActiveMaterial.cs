using Chisel.Components;
using UnityEditor;

namespace Chisel.Editors
{
    // New brushes get the active material, the one last picked in the Surface Browser, on every surface
    public static class ChiselActiveMaterial
    {
        /// <summary>Gives every surface of a new generator the active material; false when there is none</summary>
        public static bool GiveTo(ChiselGeneratorComponent generator)
        {
            var material = ChiselSurfaceBrowserPrefs.ActiveMaterial;
            if (material == null || !generator)
                return false;
            if (generator.SurfaceDefinition?.surfaces == null)
                generator.OnValidate();
            var surfaces = generator.SurfaceDefinition?.surfaces;
            if (surfaces == null)
                return false;
            var changed = false;
            foreach (var surface in surfaces)
            {
                if (surface == null || (surface.HasMaterial && surface.RenderMaterial == material))
                    continue;
                surface.SetMaterial(material);
                changed = true;
            }
            if (changed)
                EditorUtility.SetDirty(generator);
            return true;
        }
    }
}
