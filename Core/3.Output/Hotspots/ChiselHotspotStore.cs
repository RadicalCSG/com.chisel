using System.Collections.Generic;
using Unity.Collections;
using Unity.Mathematics;

namespace Chisel.Core
{
    // The hotspot layouts surfaces use, by the id a surface carries (InternalChiselSurface.hotspotLayout, 0 for none)
    // A layout is registered once for each set of materials drawn on it: a material with variants (the material first,
    // then its variants) gets an id of its own, so its surfaces can be given any of them
    public static class ChiselHotspotStore
    {
        static readonly List<ChiselHotspotLayout>                    s_Layouts   = new();
        static readonly List<ulong[]>                                s_Materials = new();
        static readonly Dictionary<(ChiselHotspotLayout, ulong), int> s_Ids      = new();
        static readonly List<ulong>                                  s_Scratch   = new();

        public static int Register(ChiselHotspotLayout layout) { return Register(layout, null, null); }

        public static int Register(ChiselHotspotLayout layout, UnityEngine.Material material, List<UnityEngine.Material> variants)
        {
            if (layout == null)
                return 0;
            ulong key = 0;
            ulong[] materials = null;
            if (material != null && variants != null && variants.Count > 0)
            {
                s_Scratch.Clear();
                s_Scratch.Add(UnityEngine.EntityId.ToULong(material.GetEntityId()));
                foreach (var variant in variants)
                {
                    if (variant == null)
                        continue;
                    var id = UnityEngine.EntityId.ToULong(variant.GetEntityId());
                    if (!s_Scratch.Contains(id))
                        s_Scratch.Add(id);
                }
                if (s_Scratch.Count > 1)
                {
                    key = 14695981039346656037UL;
                    foreach (var id in s_Scratch)
                        key = (key ^ id) * 1099511628211UL;
                }
            }
            if (!s_Ids.TryGetValue((layout, key), out var index))
            {
                if (key != 0)
                    materials = s_Scratch.ToArray();
                s_Layouts.Add(layout);
                s_Materials.Add(materials);
                index = s_Layouts.Count;
                s_Ids[(layout, key)] = index;
            }
            return index;
        }

        // An edited layout that surfaces use: every tree updates, and the changed hash maps every surface again
        public static void LayoutChanged(ChiselHotspotLayout layout)
        {
            if (layout == null || !s_Layouts.Contains(layout))
                return;
            using var trees = new NativeList<CSGTree>(Allocator.Temp);
            CompactHierarchyManager.GetAllTrees(trees);
            foreach (var tree in trees)
            {
                if (tree.Valid)
                    tree.SetDirty();
            }
        }

        // The project's hotspot budgets changed: every tree updates (the changed hash maps every surface again)
        public static void SettingsChanged()
        {
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
            return Gather(tiles, layouts, default);
        }

        // The same, and the sides of the shaped tiles (HotspotTileData.sideStart)
        public static uint Gather(NativeList<HotspotTileData> tiles, NativeList<HotspotLayoutData> layouts, NativeList<float4> sides)
        {
            return Gather(tiles, layouts, sides, default);
        }

        // The same, and the materials of the layouts with variants (HotspotLayoutData.variantStart: render material ids)
        public static uint Gather(NativeList<HotspotTileData> tiles, NativeList<HotspotLayoutData> layouts, NativeList<float4> sides, NativeList<ulong> variants)
        {
            if (sides.IsCreated)
                sides.Clear();
            if (variants.IsCreated)
                variants.Clear();
            tiles.Clear();
            layouts.Clear();
            for (int i = 0; i < s_Layouts.Count; i++)
            {
                var layout = s_Layouts[i];
                if (layout == null)
                {
                    layouts.Add(new HotspotLayoutData { tileStart = tiles.Length });
                    continue;
                }
                HotspotMatcher.AddTiles(layout, tiles, out var data, sides);
                if (variants.IsCreated && s_Materials[i] != null)
                {
                    data.variantStart = variants.Length;
                    data.variantCount = s_Materials[i].Length;
                    foreach (var id in s_Materials[i])
                        variants.Add(id);
                }
                layouts.Add(data);
            }
            return math.hash(new uint4(tiles.Hash(), layouts.Hash(), sides.IsCreated ? sides.Hash() : 0, variants.IsCreated ? variants.Hash() : 0));
        }
    }
}
