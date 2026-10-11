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

        // Each registered layout's hash (by id - 1): its tiles, settings and materials as they are now, apart from where
        // they lie among the other layouts', so an edit to one layout changes its own only (0: none)
        public static void LayoutHashes(NativeList<uint> hashes)
        {
            hashes.Clear();
            using var tiles = new NativeList<HotspotTileData>(16, Allocator.Temp);
            using var sides = new NativeList<float4>(16, Allocator.Temp);
            for (int i = 0; i < s_Layouts.Count; i++)
            {
                var layout = s_Layouts[i];
                if (layout == null)
                {
                    hashes.Add(0);
                    continue;
                }
                tiles.Clear();
                sides.Clear();
                HotspotMatcher.AddTiles(layout, tiles, out var data, sides);
                var settings = math.hash(new float4(data.textureSize, data.pixelsPerUnit, data.padding)) ^
                               math.hash(new float4(data.interiorPadding, data.maxRepeats, data.maxTriangles, s_Materials[i]?.Length ?? 0));
                var materials = 0u;
                if (s_Materials[i] != null)
                {
                    foreach (var id in s_Materials[i])
                        materials = math.hash(new uint3(materials, (uint)id, (uint)(id >> 32)));
                }
                hashes.Add(math.hash(new uint4(tiles.Hash(), sides.Hash(), settings, materials)) | 1);
            }
        }

        // An edited layout that surfaces use: every tree updates, and only the brushes drawn on it are mapped again
        // (LayoutHashes)
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

        // The project's hotspot budgets changed: every tree updates, and the brushes drawn on layouts without budgets of
        // their own are mapped again (LayoutHashes)
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
