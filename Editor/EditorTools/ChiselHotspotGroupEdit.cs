using System.Collections.Generic;
using Chisel.Components;
using Chisel.Core;
using UnityEditor;

namespace Chisel.Editors
{
    // Connected hotspot surfaces are mapped as one, on the first one's settings (HotspotGroups), and coplanar ones stay
    // connected only while their frames agree: an edit to how the texture lies on one of them goes to all of them, so
    // turning one turns the group instead of tearing it off
#if UNITY_6000_7_OR_NEWER
    [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
#endif
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

        // How many variations a surface had when it was last mapped (its group's when it has one): the tiles that tie times
        // the materials drawn on the layout; -1 when it is not known
        public static int VariationsOf(SurfaceReference surface)
        {
            if (surface == null || !surface.node || !surface.brush.Valid)
                return -1;
            var model = surface.node.hierarchyItem.Model;
            if (!model || !model.Node.Valid)
                return -1;
            var lookups = ChiselTreeLookup.Value;
            if (!lookups.HasTree(model.Node))
                return -1;
            var data = lookups[model.Node];
            data.lastJobHandle.Complete();
            if (!data.brushRenderBufferLookup.IsCreated ||
                !data.brushRenderBufferLookup.TryGetValue(CompactHierarchyManager.GetCompactNodeID(surface.brush), out var buffer) || !buffer.IsCreated)
                return -1;
            ref var surfaces = ref buffer.Value.surfaces;
            if (surface.surfaceIndex < 0 || surface.surfaceIndex >= surfaces.Length || !surfaces[surface.surfaceIndex].hotspot.IsMapped)
                return -1;
            var info = surfaces[surface.surfaceIndex].hotspot;
            return info.group > 0 ? info.groupVariations : info.variations;
        }

        // The ids of a layout's tiles a surface could take, by its shape when it was last mapped; null when not known
        // (sheet, id) of the tiles the surface can take: its layout's (sheet 0) and those of the other sheets drawn with its
        // material (ChiselSurfaceMetadata.hotspotSheets, by place)
        public static HashSet<UnityEngine.Vector2Int> FitsOf(SurfaceReference surface, ChiselHotspotLayout layout, UnityEngine.Material material = null)
        {
            if (layout == null)
                return null;
            using var tiles = new Unity.Collections.NativeList<HotspotTileData>(Unity.Collections.Allocator.Temp);
            var sheetOf = new List<int>();
            TilesOf(layout, material, tiles, sheetOf);
            return FitsOf(surface, tiles, sheetOf);
        }

        // The tiles matching chooses from for a material: its layout's (sheet 0), then those of each other sheet drawn
        // with it (ChiselSurfaceMetadata.hotspotSheets, by place), and the sheet each is of
        public static void TilesOf(ChiselHotspotLayout layout, UnityEngine.Material material, Unity.Collections.NativeList<HotspotTileData> tiles, List<int> sheetOf)
        {
            tiles.Clear();
            sheetOf.Clear();
            if (layout == null)
                return;
            HotspotMatcher.AddTiles(layout, tiles, out _);
            while (sheetOf.Count < tiles.Length) sheetOf.Add(0);
            var metadata = material != null ? material.GetMetadataOfType<ChiselSurfaceMetadata>() : null;
            for (int i = 0; metadata != null && metadata.hotspotSheets != null && i < metadata.hotspotSheets.Count; i++)
            {
                var sheet = metadata.hotspotSheets[i];
                var sheetLayout = sheet != null && sheet != material ? sheet.GetMetadataOfType<ChiselSurfaceMetadata>()?.hotspotLayout : null;
                if (sheetLayout == null)
                    continue;
                HotspotMatcher.AddTiles(sheetLayout, tiles, out _);
                while (sheetOf.Count < tiles.Length) sheetOf.Add(i + 1);
            }
        }

        // A surface's shape as matching saw it when it was last mapped; false when not known
        static bool TryShapeOf(SurfaceReference surface, out HotspotShape shape)
        {
            shape = default;
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
            ref var drawn = ref surfaces[surface.surfaceIndex];
            var source = drawn.hotspotSource;
            if (source.layout == 0 || drawn.colliderVertices.Length == 0)
                return false;
            var positions = new Unity.Collections.NativeArray<Unity.Mathematics.float3>(drawn.colliderVertices.Length, Unity.Collections.Allocator.Temp);
            var indices   = new Unity.Collections.NativeArray<int>(drawn.indices.Length, Unity.Collections.Allocator.Temp);
            try
            {
                for (int i = 0; i < positions.Length; i++)
                    positions[i] = drawn.colliderVertices[i];
                for (int i = 0; i < indices.Length; i++)
                    indices[i] = drawn.indices[i];
                shape = HotspotMatcher.ShapeOf(positions, source.right, source.up);
                HotspotMatcher.MaskOf(ref shape, positions, indices, source.right, source.up);
            }
            finally
            {
                positions.Dispose();
                indices.Dispose();
            }
            return true;
        }

        // (sheet, id) of the tiles (TilesOf) a surface can take; null when its shape is not known
        public static HashSet<UnityEngine.Vector2Int> FitsOf(SurfaceReference surface, Unity.Collections.NativeList<HotspotTileData> tiles, List<int> sheetOf)
        {
            if (!TryShapeOf(surface, out var shape))
                return null;
            var fits = new HashSet<UnityEngine.Vector2Int>();
            for (int t = 0; t < tiles.Length; t++)
            {
                if (HotspotMatcher.Fits(tiles[t], shape, shape.radial))
                    fits.Add(new UnityEngine.Vector2Int(sheetOf[t], tiles[t].id));
            }
            // a round surface takes the other tiles when the layout has no radial one for it
            if (fits.Count == 0 && shape.radial)
            {
                for (int t = 0; t < tiles.Length; t++)
                {
                    if (HotspotMatcher.Fits(tiles[t], shape, false))
                        fits.Add(new UnityEngine.Vector2Int(sheetOf[t], tiles[t].id));
                }
            }
            return fits;
        }

        // The most variations of these surfaces, -1 when none is known
        public static int MostVariations(IReadOnlyList<SurfaceReference> surfaces)
        {
            var most = -1;
            foreach (var surface in surfaces)
                most = System.Math.Max(most, VariationsOf(surface));
            return most;
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
            return a.pinnedTile == b.pinnedTile && a.pinnedSheet == b.pinnedSheet && a.seed == b.seed && a.rotation == b.rotation && a.density == b.density &&
                   a.mirror == b.mirror && a.mirrorY == b.mirrorY && a.offset.x == b.offset.x && a.offset.y == b.offset.y;
        }
    }
}
