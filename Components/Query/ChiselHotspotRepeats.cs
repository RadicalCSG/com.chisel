using Chisel.Core;
using Unity.Mathematics;

namespace Chisel.Components
{
    // A hotspot surface's repeats as its last build laid them
    public static class ChiselHotspotRepeats
    {
        // The section input a surface was last built with; false when it has none
        public static bool TryGetSectionInput(SurfaceReference surface, out HotspotSectionInput input)
        {
            input = default;
            if (surface == null || !surface.node || !surface.brush.Valid)
                return false;
            var model = surface.node.hierarchyItem.Model;
            if (!model || !model.Node.Valid)
                return false;
            var lookups = ChiselTreeLookup.Value;
            if (!lookups.HasTree(model.Node))
                return false;
            var data = lookups[model.Node];
            data.lastJobHandle.Complete();
            if (!data.brushRenderBufferLookup.IsCreated ||
                !data.brushRenderBufferLookup.TryGetValue(CompactHierarchyManager.GetCompactNodeID(surface.brush), out var buffer) || !buffer.IsCreated)
                return false;
            ref var surfaces = ref buffer.Value.surfaces;
            if (surface.surfaceIndex < 0 || surface.surfaceIndex >= surfaces.Length)
                return false;
            input = surfaces[surface.surfaceIndex].sectionInput;
            return input.count > 0;
        }

        // Where the surface's repeats that keep their size start, over their size, along the axes they repeat along (0
        // along the others): its interior's repeats (interior), or a tile's own exact size repeats; false when it has
        // none
        public static bool TryGetOrigin(SurfaceReference surface, out bool interior, out float2 origin, out float2 size)
        {
            interior = false; origin = 0; size = 1;
            if (!TryGetSectionInput(surface, out var input))
                return false;
            if (HotspotSections.InteriorRepeatStart(input, out var corner, out var period))
            {
                interior = true;
                origin = (float2)corner;
                size   = (float2)period;
                return true;
            }
            if (HotspotSections.PartialRepeatStart(input, out var start, out var length))
            {
                var acrossRepeats = input.repeatX > 1 || (input.lastRepeat.x > 0 && input.lastRepeat.x < 1);
                var upRepeats     = input.repeatY > 1 || (input.lastRepeat.y > 0 && input.lastRepeat.y < 1);
                origin = new float2(acrossRepeats ? (float)start.x : 0, upRepeats ? (float)start.y : 0);
                size   = (float2)length;
                return true;
            }
            return false;
        }
    }
}
