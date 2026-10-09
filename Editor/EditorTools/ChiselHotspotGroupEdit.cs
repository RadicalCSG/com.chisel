using System.Collections.Generic;
using Chisel.Components;
using Chisel.Core;
using UnityEditor;

namespace Chisel.Editors
{
    // Connected hotspot surfaces are mapped as one, on the first one's settings (HotspotGroups), and coplanar ones stay
    // connected only while their frames agree: an edit to how the texture lies on one of them goes to all of them, so
    // turning one turns the group instead of tearing it off
    static class ChiselHotspotGroupEdit
    {
        static readonly List<ChiselGeneratorComponent> s_Generators = new();
        static readonly List<SurfaceReference>         s_Surfaces   = new();

        // The connected group a surface was last mapped with (its index in the model + 1), 0 for none
        static int GroupOf(SurfaceReference surface, ChiselModelComponent model)
        {
            if (surface == null || !surface.node || !surface.brush.Valid || !model || !model.Node.Valid)
                return 0;
            var lookups = ChiselTreeLookup.Value;
            if (!lookups.HasTree(model.Node))
                return 0;
            var data = lookups[model.Node];
            data.lastJobHandle.Complete();
            if (!data.brushRenderBufferLookup.IsCreated ||
                !data.brushRenderBufferLookup.TryGetValue(CompactHierarchyManager.GetCompactNodeID(surface.brush), out var buffer) || !buffer.IsCreated)
                return 0;
            ref var surfaces = ref buffer.Value.surfaces;
            return surface.surfaceIndex >= 0 && surface.surfaceIndex < surfaces.Length ? surfaces[surface.surfaceIndex].hotspot.group : 0;
        }

        // Gives every surface connected to an edited one the edited one's hotspot settings; true when any changed
        public static bool Spread(IReadOnlyList<SurfaceReference> edited, string undoName = null)
        {
            var changed = false;
            var done = new HashSet<(ChiselModelComponent, int)>();
            foreach (var source in edited)
            {
                if (source == null || !source.node)
                    continue;
                var model = source.node.hierarchyItem.Model;
                var group = GroupOf(source, model);
                if (group == 0 || !done.Add((model, group)))
                    continue;
                var settings = source.SurfaceDetails.hotspot;
                s_Generators.Clear();
                model.GetComponentsInChildren(false, s_Generators);
                foreach (var generator in s_Generators)
                {
                    s_Surfaces.Clear();
                    if (!ChiselSurfaceSelectionManager.GetAllSurfaceReferences(generator, s_Surfaces))
                        continue;
                    foreach (var member in s_Surfaces)
                    {
                        if (member.Equals(source) || GroupOf(member, model) != group)
                            continue;
                        var details = member.SurfaceDetails;
                        if (Same(details.hotspot, settings))
                            continue;
                        if (undoName != null)
                            Undo.RecordObject(member.node, undoName);
                        details.hotspot = settings;
                        member.SurfaceDetails = details;
                        changed = true;
                    }
                }
            }
            return changed;
        }

        static bool Same(in HotspotSurface a, in HotspotSurface b)
        {
            return a.pinnedTile == b.pinnedTile && a.seed == b.seed && a.rotation == b.rotation && a.density == b.density &&
                   a.mirror == b.mirror && a.mirrorY == b.mirrorY && a.offset.x == b.offset.x && a.offset.y == b.offset.y;
        }
    }
}
