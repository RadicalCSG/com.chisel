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
        // the other sheets each layout is drawn with: their layouts and materials
        static readonly List<(ChiselHotspotLayout layout, ulong material, int index)[]> s_Sheets = new();
        // the material a layout with other sheets is registered for (a surface mapped on the layout's own tiles takes it
        // back after its own match put another sheet's on it); 0 for one without
        static readonly List<ulong>                                  s_Own    = new();

        public static int Register(ChiselHotspotLayout layout) { return Register(layout, null, null); }

        public static int Register(ChiselHotspotLayout layout, UnityEngine.Material material, List<UnityEngine.Material> variants)
        {
            return Register(layout, material, variants, null);
        }

        // The same, and the other sheets drawn with it (ChiselSurfaceMetadata.hotspotSheets): materials with a layout of
        // their own, whose tiles matching picks from too
        public static int Register(ChiselHotspotLayout layout, UnityEngine.Material material, List<UnityEngine.Material> variants, List<UnityEngine.Material> sheets)
        {
            if (layout == null)
                return 0;
            (ChiselHotspotLayout layout, ulong material, int index)[] sheetList = null;
            ulong sheetKey = 0;
            if (sheets != null && sheets.Count > 0)
            {
                var found = new List<(ChiselHotspotLayout, ulong, int)>(sheets.Count);
                for (int place = 0; place < sheets.Count; place++)
                {
                    var sheet = sheets[place];
                    if (sheet == null || sheet == material)
                        continue;
                    var sheetLayout = sheet.GetMetadataOfType<ChiselSurfaceMetadata>()?.hotspotLayout;
                    var id = UnityEngine.EntityId.ToULong(sheet.GetEntityId());
                    if (sheetLayout == null || found.Exists(f => f.Item2 == id))
                        continue;
                    found.Add((sheetLayout, id, place + 1));
                    sheetKey = (sheetKey ^ id ^ (ulong)(uint)sheetLayout.GetEntityId().GetHashCode() << 1) * 1099511628211UL + 1;
                }
                if (found.Count > 0)
                    sheetList = found.ToArray();
            }
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
            var variantKey = key;
            var own = sheetList != null && material != null ? UnityEngine.EntityId.ToULong(material.GetEntityId()) : 0UL;
            key ^= sheetKey * 0x9E3779B97F4A7C15UL ^ own * 0xC2B2AE3D27D4EB4FUL;
            if (!s_Ids.TryGetValue((layout, key), out var index))
            {
                if (variantKey != 0)
                    materials = s_Scratch.ToArray();
                s_Layouts.Add(layout);
                s_Materials.Add(materials);
                s_Sheets.Add(sheetList);
                s_Own.Add(own);
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
                var sheetHash = 0u;
                if (s_Sheets[i] != null)
                {
                    foreach (var sheet in s_Sheets[i])
                    {
                        if (sheet.layout == null)
                            continue;
                        HotspotMatcher.AddTiles(sheet.layout, tiles, out var sheetData, sides);
                        sheetHash = math.hash(new uint4(sheetHash, (uint)sheet.material, (uint)(sheet.material >> 32),
                                                        math.hash(new float4(sheetData.textureSize, sheetData.pixelsPerUnit, sheetData.interiorPadding))));
                    }
                }
                var settings = math.hash(new float4(data.textureSize, data.pixelsPerUnit, data.padding)) ^
                               math.hash(new float4(data.interiorPadding, data.maxRepeats, data.maxTriangles, s_Materials[i]?.Length ?? 0));
                var materials = 0u;
                if (s_Materials[i] != null)
                {
                    foreach (var id in s_Materials[i])
                        materials = math.hash(new uint3(materials, (uint)id, (uint)(id >> 32)));
                }
                hashes.Add(math.hash(new uint4(tiles.Hash(), sides.Hash(), settings, materials ^ sheetHash)) | 1);
            }
        }

        // An edited layout that surfaces use: every tree updates, and only the brushes drawn on it are mapped again
        // (LayoutHashes)
        public static void LayoutChanged(ChiselHotspotLayout layout)
        {
            if (layout == null || (!s_Layouts.Contains(layout) && !s_Sheets.Exists(sheets => sheets != null && System.Array.Exists(sheets, sheet => sheet.layout == layout))))
                return;
            using var trees = new NativeList<CSGTree>(Allocator.Temp);
            CompactHierarchyManager.GetAllTrees(trees);
            foreach (var tree in trees)
            {
                if (tree.Valid)
                    tree.SetDirty();
            }
        }

        static int2 s_Budgets = new int2(-1, -1);

        static int2 Budgets() { return new int2(ChiselProjectSettings.HotspotMaxRepeats, ChiselProjectSettings.HotspotMaxTriangles); }

        // After an undo or a redo, which put the project's hotspot budgets back without telling anyone: updates the trees
        // when they differ from those the trees were last told of; true when they did
        public static bool CheckSettings()
        {
            var current = Budgets();
            if (s_Budgets.x < 0)
            {
                s_Budgets = current;
                return false;
            }
            if (current.Equals(s_Budgets))
                return false;
            SettingsChanged();
            return true;
        }

        // The project's hotspot budgets changed: every tree updates, and the brushes drawn on layouts without budgets of
        // their own are mapped again (LayoutHashes)
        public static void SettingsChanged()
        {
            s_Budgets = Budgets();
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
            // the other sheets' own entries, after the registered layouts' (HotspotTileData.sheet)
            var sheetEntries = new List<HotspotLayoutData>();
            for (int i = 0; i < s_Layouts.Count; i++)
            {
                var layout = s_Layouts[i];
                if (layout == null)
                {
                    layouts.Add(new HotspotLayoutData { tileStart = tiles.Length });
                    continue;
                }
                HotspotMatcher.AddTiles(layout, tiles, out var data, sides);
                // the other sheets' tiles right after its own, matched with them
                if (s_Sheets[i] != null)
                {
                    foreach (var sheet in s_Sheets[i])
                    {
                        if (sheet.layout == null)
                            continue;
                        HotspotMatcher.AddTiles(sheet.layout, tiles, out var sheetData, sides);
                        sheetData.material = sheet.material;
                        var entry = s_Layouts.Count + sheetEntries.Count + 1;
                        for (int t = sheetData.tileStart; t < tiles.Length; t++)
                        {
                            var tile = tiles[t];
                            tile.sheet = entry;
                            tile.sheetIndex = sheet.index;
                            tiles[t] = tile;
                        }
                        sheetEntries.Add(sheetData);
                    }
                    data.tileCount = tiles.Length - data.tileStart;
                }
                data.material = s_Own[i];
                if (variants.IsCreated && s_Materials[i] != null)
                {
                    data.variantStart = variants.Length;
                    data.variantCount = s_Materials[i].Length;
                    foreach (var id in s_Materials[i])
                        variants.Add(id);
                }
                layouts.Add(data);
            }
            foreach (var entry in sheetEntries)
                layouts.Add(entry);
            return math.hash(new uint4(tiles.Hash(), layouts.Hash(), sides.IsCreated ? sides.Hash() : 0, variants.IsCreated ? variants.Hash() : 0));
        }
    }
}
