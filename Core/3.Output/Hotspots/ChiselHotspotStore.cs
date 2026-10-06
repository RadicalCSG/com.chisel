using System.Collections.Generic;
using Unity.Collections;
using Unity.Mathematics;

namespace Chisel.Core
{
    // The hotspot layouts surfaces use, by the id a surface carries (InternalChiselSurface.hotspotLayout, 0 for none)
    public static class ChiselHotspotStore
    {
        static readonly List<ChiselHotspotLayout>            s_Layouts = new();
        static readonly Dictionary<ChiselHotspotLayout, int> s_Ids     = new();

        public static int Register(ChiselHotspotLayout layout)
        {
            if (layout == null)
                return 0;
            if (!s_Ids.TryGetValue(layout, out var id))
            {
                s_Layouts.Add(layout);
                id = s_Layouts.Count;
                s_Ids[layout] = id;
            }
            return id;
        }

        // An edited layout that surfaces use: every tree updates, and the changed hash maps every surface again
        public static void LayoutChanged(ChiselHotspotLayout layout)
        {
            if (layout == null || !s_Ids.ContainsKey(layout))
                return;
            using var trees = new NativeList<CSGTree>(Allocator.Temp);
            CompactHierarchyManager.GetAllTrees(trees);
            foreach (var tree in trees)
            {
                if (tree.Valid)
                    tree.SetDirty();
            }
        }

        // Every registered layout's tiles as they are now; layouts[id - 1] says where a layout's tiles are. Returns a hash
        // of all of it, which changes when a layout does.
        public static uint Gather(NativeList<HotspotTileData> tiles, NativeList<HotspotLayoutData> layouts)
        {
            tiles.Clear();
            layouts.Clear();
            foreach (var layout in s_Layouts)
            {
                if (layout == null)
                {
                    layouts.Add(new HotspotLayoutData { tileStart = tiles.Length });
                    continue;
                }
                HotspotMatcher.AddTiles(layout, tiles, out var data);
                layouts.Add(data);
            }
            return math.hash(new uint2(tiles.Hash(), layouts.Hash()));
        }
    }
}
