using System.Collections.Generic;
using Chisel.Components;
using Chisel.Core;
using UnityEditor;

namespace Chisel.Editors
{
    // Lock texture to object, as the surface options and the brush inspector toggle it: a surface whose flag changed is
    // carried over to the other space (ChiselTextureLock), so toggling it changes nothing on screen
    static class ChiselTextureLockEdit
    {
        // Whether each surface's texture is in the world now, before an edit
        public static bool[] WorldSpaceOf(IReadOnlyList<SurfaceReference> surfaces)
        {
            var world = new bool[surfaces.Count];
            for (int i = 0; i < surfaces.Count; i++)
                world[i] = surfaces[i] != null && surfaces[i].node && ChiselTextureLock.IsInWorldSpace(surfaces[i].SurfaceDetails);
            return world;
        }

        // After an edit: every surface (each of a generator's surface definitions once) whose flag the edit changed keeps
        // its look; true when any did
        public static bool KeepLook(IReadOnlyList<SurfaceReference> surfaces, bool[] wasWorld, string undoName)
        {
            var changed = false;
            var done = new HashSet<(ChiselGeneratorComponent, int)>();
            for (int i = 0; i < surfaces.Count && i < wasWorld.Length; i++)
            {
                var surface = surfaces[i];
                if (surface == null || !surface.node)
                    continue;
                var details = surface.SurfaceDetails;
                if (ChiselTextureLock.IsInWorldSpace(details) == wasWorld[i] || !done.Add((surface.node, surface.descriptionIndex)))
                    continue;
                // as it was, then carried over
                if (wasWorld[i]) details.detailFlags |=  SurfaceDetailFlags.TextureIsInWorldSpace;
                else             details.detailFlags &= ~SurfaceDetailFlags.TextureIsInWorldSpace;
                Undo.RecordObject(surface.node, undoName);
                surface.SurfaceDetails = ChiselTextureLock.Toggled(surface, details);
                changed = true;
            }
            return changed;
        }
    }
}
