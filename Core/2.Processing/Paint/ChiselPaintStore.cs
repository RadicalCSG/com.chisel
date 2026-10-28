using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;

namespace Chisel.Core
{
    // The surface a paint layer belongs to
    internal struct ChiselPaintKey : IEquatable<ChiselPaintKey>
    {
        public ulong entityID;
        public int   surfaceIndex;

        public readonly bool Equals(ChiselPaintKey other) => entityID == other.entityID && surfaceIndex == other.surfaceIndex;
        public override readonly int GetHashCode() => (int)math.hash(new uint3((uint)entityID, (uint)(entityID >> 32), (uint)surfaceIndex));
    }

    // A surface's layers in the tree's layer list, and a hash of them and their values
    internal struct ChiselPaintRange
    {
        public int  start;
        public int  count;
        public uint hash;
    }

    // The tree's paint as an update reads it: views of what the tree keeps (ChiselPaintStore)
    internal struct ChiselPaintLookup
    {
        [ReadOnly] public NativeParallelHashMap<ChiselPaintKey, ChiselPaintRange> ranges;
        [ReadOnly] public NativeArray<ChiselPaintLayer>                          layers;
        [ReadOnly] public NativeArray<float>                                     values;
        public bool                                                              hasPaint;
        // the attributes of the materials whose shaders take paint, by material (ChiselPaintMaterials)
        [ReadOnly] public NativeParallelHashMap<ulong, ChiselPaintRange>         materials;
        [ReadOnly] public NativeArray<ChiselPaintMaterialSlot>                   materialSlots;
        public bool                                                              hasMaterials;

        public readonly bool TryGetRange(ulong entityID, int surfaceIndex, out ChiselPaintRange range)
        {
            range = default;
            return hasPaint && ranges.TryGetValue(new ChiselPaintKey { entityID = entityID, surfaceIndex = surfaceIndex }, out range);
        }

        public readonly bool TryGetMaterial(ulong material, out ChiselPaintRange range)
        {
            range = default;
            return hasMaterials && materials.TryGetValue(material, out range);
        }

        public static ChiselPaintLookup Of(ChiselTreeLookup.Data data)
        {
            return new ChiselPaintLookup
            {
                ranges        = data.paintRanges,
                layers        = data.paintLayers.AsArray(),
                values        = data.paintValues.AsArray(),
                hasPaint      = data.paintLayers.Length > 0,
                materials     = data.paintMaterials,
                materialSlots = data.paintMaterialSlots.AsArray(),
                hasMaterials  = data.paintMaterialSlots.Length > 0
            };
        }
    }

    /// <summary>What setting an entity's paint changed</summary>
    public enum ChiselPaintChange : byte
    {
        None,
        /// <summary>Only values on the same grids: the meshes are copied again, no brush is built</summary>
        Values,
        /// <summary>The grids (or which surfaces have paint): the entity's brushes have to be built again</summary>
        Grids
    }

    // The tree keeps its paint between updates: each entity's layers together, their values appended as they come (the
    // values of replaced layers are left until they are half of them), and the ranges and hashes an update looks up
    internal static class ChiselPaintStore
    {
        struct BySurface : IComparer<ChiselPaintLayer>
        {
            public int Compare(ChiselPaintLayer x, ChiselPaintLayer y)
            {
                if (x.surfaceIndex != y.surfaceIndex) return x.surfaceIndex.CompareTo(y.surfaceIndex);
                return ((int)x.attribute).CompareTo((int)y.attribute);
            }
        }

        // A layer and its values
        public static unsafe uint HashOf(in ChiselPaintLayer layer, float* values)
        {
            var copy = layer;
            copy.valueStart = 0;
            var hash = math.hash(&copy, UnsafeUtility.SizeOf<ChiselPaintLayer>());
            return math.hash(new uint2(hash, math.hash(values + layer.valueStart, layer.ValueCount * sizeof(float))));
        }

        /// <summary>Replaces the paint of one entity: its layers, and the values their grids take from. True when it changed;
        /// its brushes then have to be rebuilt to show it.</summary>
        public static bool SetPaint(CSGTree tree, ulong entityID, NativeArray<ChiselPaintLayer> layers, NativeArray<float> values)
        {
            return Set(tree, entityID, layers, values, false) != ChiselPaintChange.None;
        }

        /// <summary>
        /// Replaces the paint of one entity. When only values changed on the same grids, they are written in place and
        /// the meshes are copied again at the next update without building a brush (<see cref="ChiselPaintChange.Values"/>);
        /// otherwise the entity's brushes have to be built again (<see cref="ChiselPaintChange.Grids"/>).
        /// </summary>
        public static ChiselPaintChange SetPaintOrValues(CSGTree tree, ulong entityID, NativeArray<ChiselPaintLayer> layers, NativeArray<float> values)
        {
            return Set(tree, entityID, layers, values, true);
        }

        static unsafe ChiselPaintChange Set(CSGTree tree, ulong entityID, NativeArray<ChiselPaintLayer> layers, NativeArray<float> values, bool valuesInPlace)
        {
            if (!tree.Valid)
                return ChiselPaintChange.None;
            var data = ChiselTreeLookup.Value[tree];

            NativeList<ChiselPaintLayer> sorted;
            using var _sorted = sorted = new NativeList<ChiselPaintLayer>(math.max(1, layers.IsCreated ? layers.Length : 0), Allocator.Temp);
            for (int l = 0; layers.IsCreated && l < layers.Length; l++)
            {
                var layer = layers[l];
                if (layer.ValueCount <= 0 || layer.valueStart < 0 || layer.valueStart + layer.ValueCount > values.Length)
                    continue;
                layer.entityID = entityID;
                sorted.Add(layer);
            }
            sorted.Sort(new BySurface());

            NativeList<uint> hashes;
            using var _hashes = hashes = new NativeList<uint>(math.max(1, sorted.Length), Allocator.Temp);
            var entityHash = 0u;
            var valuesPointer = values.IsCreated ? (float*)values.GetUnsafeReadOnlyPtr() : null;
            for (int l = 0; l < sorted.Length; l++)
            {
                hashes.Add(HashOf(sorted[l], valuesPointer));
                entityHash = math.hash(new uint2(entityHash, hashes[l]));
            }
            data.paintEntityHashes.TryGetValue(entityID, out var previous);
            if (sorted.Length == 0 ? !data.paintEntityHashes.ContainsKey(entityID) : previous == entityHash && data.paintEntityHashes.ContainsKey(entityID))
                return ChiselPaintChange.None;

            data.lastJobHandle.Complete();

            var first = 0;
            while (first < data.paintLayers.Length && data.paintLayers[first].entityID != entityID)
                first++;
            // a tree that kept its saved meshes has no buffers to put right: it is built
            if (valuesInPlace && !CompactHierarchyManager.IsTreeUpdateSkipped(tree) && SameGrids(data, first, sorted) &&
                SetValues(tree, data, entityID, first, sorted, hashes, valuesPointer))
            {
                data.paintEntityHashes[entityID] = entityHash;
                return ChiselPaintChange.Values;
            }

            // its old layers out, their values left behind
            var last = first;
            while (last < data.paintLayers.Length && data.paintLayers[last].entityID == entityID)
            {
                data.paintUnusedValues += data.paintLayers[last].ValueCount;
                last++;
            }
            data.paintLayers.RemoveRange(first, last - first);
            data.paintLayerHashes.RemoveRange(first, last - first);

            // its new layers at the end, their values after the others
            for (int l = 0; l < sorted.Length; l++)
            {
                var layer = sorted[l];
                var start = data.paintValues.Length;
                data.paintValues.AddRange(valuesPointer + layer.valueStart, layer.ValueCount);
                layer.valueStart = start;
                data.paintLayers.Add(layer);
                data.paintLayerHashes.Add(hashes[l]);
            }
            if (sorted.Length == 0)
                data.paintEntityHashes.Remove(entityID);
            else
                data.paintEntityHashes[entityID] = entityHash;

            if (data.paintUnusedValues > 0 && data.paintUnusedValues * 2 >= data.paintValues.Length)
                Compact(data);
            UpdateRanges(data);
            return ChiselPaintChange.Grids;
        }

        // Whether the entity's layers from first are the new layers but for their values
        static bool SameGrids(ChiselTreeLookup.Data data, int first, NativeList<ChiselPaintLayer> layers)
        {
            for (int l = 0; l < layers.Length; l++)
            {
                if (first + l >= data.paintLayers.Length)
                    return false;
                var old   = data.paintLayers[first + l];
                var layer = layers[l];
                if (old.entityID != layer.entityID)
                    return false;
                old.valueStart = layer.valueStart = 0;
                if (!old.Equals(layer))
                    return false;
            }
            var next = first + layers.Length;
            return layers.Length > 0 && (next >= data.paintLayers.Length || data.paintLayers[next].entityID != layers[0].entityID);
        }

        // New values written over the old, and the hashes of the surfaces they are on put right in the brushes' cached
        // buffers, so the next update copies their meshes again without building a brush. False when no buffer of the
        // entity's brushes has the paint (they are built then)
        static unsafe bool SetValues(CSGTree tree, ChiselTreeLookup.Data data, ulong entityID, int first, NativeList<ChiselPaintLayer> layers,
                                     NativeList<uint> hashes, float* values)
        {
            for (int l = 0; l < layers.Length; l++)
            {
                var old = data.paintLayers[first + l];
                UnsafeUtility.MemCpy((float*)data.paintValues.GetUnsafePtr() + old.valueStart, values + layers[l].valueStart, old.ValueCount * sizeof(float));
                data.paintLayerHashes[first + l] = hashes[l];
            }

            // each surface's new hash, and what changes in the hash of the surfaces it is on
            using var changes = new NativeParallelHashMap<int, uint>(math.max(1, layers.Length), Allocator.Temp);
            for (int start = first; start < first + layers.Length;)
            {
                var surfaceIndex = data.paintLayers[start].surfaceIndex;
                var key = new ChiselPaintKey { entityID = entityID, surfaceIndex = surfaceIndex };
                data.paintRanges.TryGetValue(key, out var range);
                var hash = 0u;
                var end  = start;
                for (; end < first + layers.Length && data.paintLayers[end].surfaceIndex == surfaceIndex; end++)
                    hash = math.hash(new uint2(hash, data.paintLayerHashes[end]));
                if (hash != range.hash)
                    changes.TryAdd(surfaceIndex, hash ^ range.hash);
                range.hash = hash;
                data.paintRanges[key] = range;
                start = end;
            }
            if (changes.IsEmpty)
                return true;

            var found = false;
            var cache = data.brushRenderBufferCache;
            for (int b = 0; b < cache.Length; b++)
            {
                if (!cache[b].IsCreated)
                    continue;
                ref var buffer = ref cache[b].Value;
                if (buffer.entityID != entityID)
                    continue;
                var repainted = false;
                for (int s = 0; s < buffer.surfaces.Length; s++)
                {
                    ref var surface = ref buffer.surfaces[s];
                    if (surface.paintSurface < 0 || !changes.TryGetValue(surface.paintSurface, out var change))
                        continue;
                    found = true;
                    // the weld keeps what it made of an unchanged buffer, so it has to know this one changed
                    if (!repainted)
                    {
                        repainted = true;
                        data.weldMemory.repainted.Add(cache[b]);
                    }
                    surface.surfaceHashValue ^= change;
                    for (int q = 0; q < buffer.querySurfaces.Length; q++)
                    {
                        ref var querySurfaces = ref buffer.querySurfaces[q].surfaces;
                        for (int i = 0; i < querySurfaces.Length; i++)
                        {
                            if (querySurfaces[i].surfaceIndex == s)
                                querySurfaces[i].surfaceHashValue ^= change;
                        }
                    }
                }
            }
            if (!found)
                return false;
            data.paintValuesChanged = true;
            tree.SetDirty();
            return true;
        }

        // The values of the layers only, in their order
        static void Compact(ChiselTreeLookup.Data data)
        {
            using var compacted = new NativeList<float>(math.max(1, data.paintValues.Length - data.paintUnusedValues), Allocator.Temp);
            for (int l = 0; l < data.paintLayers.Length; l++)
            {
                var layer = data.paintLayers[l];
                var start = compacted.Length;
                compacted.AddRange(data.paintValues.AsArray().GetSubArray(layer.valueStart, layer.ValueCount));
                layer.valueStart = start;
                data.paintLayers[l] = layer;
            }
            data.paintValues.Clear();
            data.paintValues.AddRange(compacted.AsArray());
            data.paintUnusedValues = 0;
        }

        // Each surface's layers (an entity's are together, by surface) and their hash
        static void UpdateRanges(ChiselTreeLookup.Data data)
        {
            var layers = data.paintLayers;
            data.paintRanges.Clear();
            if (data.paintRanges.Capacity < layers.Length)
                data.paintRanges.Capacity = layers.Length;
            for (int start = 0; start < layers.Length;)
            {
                var key  = new ChiselPaintKey { entityID = layers[start].entityID, surfaceIndex = layers[start].surfaceIndex };
                var end  = start;
                var hash = 0u;
                for (; end < layers.Length && layers[end].entityID == key.entityID && layers[end].surfaceIndex == key.surfaceIndex; end++)
                    hash = math.hash(new uint2(hash, data.paintLayerHashes[end]));
                data.paintRanges.TryAdd(key, new ChiselPaintRange { start = start, count = end - start, hash = hash });
                start = end;
            }
        }

        /// <summary>Replaces all paint of the tree: the entities in the layers get theirs, every other entity loses its paint.
        /// Returns the entities whose paint changed.</summary>
        public static bool SetPaint(CSGTree tree, NativeArray<ChiselPaintLayer> layers, NativeArray<float> values, NativeList<ulong> changedEntities)
        {
            if (changedEntities.IsCreated)
                changedEntities.Clear();
            if (!tree.Valid)
                return false;
            var data = ChiselTreeLookup.Value[tree];

            var entities = new List<ulong>();
            for (int l = 0; layers.IsCreated && l < layers.Length; l++)
            {
                if (!entities.Contains(layers[l].entityID))
                    entities.Add(layers[l].entityID);
            }
            using var gone = data.paintEntityHashes.GetKeyArray(Allocator.Temp);
            foreach (var entityID in gone)
            {
                if (!entities.Contains(entityID) && SetPaint(tree, entityID, default, default) && changedEntities.IsCreated)
                    changedEntities.Add(entityID);
            }

            using var own = new NativeList<ChiselPaintLayer>(Allocator.Temp);
            foreach (var entityID in entities)
            {
                own.Clear();
                for (int l = 0; l < layers.Length; l++)
                {
                    if (layers[l].entityID == entityID)
                        own.Add(layers[l]);
                }
                if (SetPaint(tree, entityID, own.AsArray(), values) && changedEntities.IsCreated)
                    changedEntities.Add(entityID);
            }
            return changedEntities.IsCreated && changedEntities.Length > 0;
        }

        // The tree's copy of the materials' paint attributes, made again when they changed
        public static void UpdateMaterials(ChiselTreeLookup.Data data)
        {
            var version = ChiselPaintMaterials.Refresh();
            if (data.paintMaterialsVersion == version)
                return;
            data.lastJobHandle.Complete();
            data.paintMaterialsVersion = version;
            var slots = data.paintMaterialSlots;
            slots.Clear();
            ChiselPaintMaterials.Gather(slots);
            data.paintMaterials.Clear();
            if (data.paintMaterials.Capacity < slots.Length)
                data.paintMaterials.Capacity = slots.Length;
            // a material's slots are together (ChiselPaintMaterials)
            for (int start = 0; start < slots.Length;)
            {
                var material = slots[start].material;
                var end  = start;
                var hash = 0u;
                for (; end < slots.Length && slots[end].material == material; end++)
                    hash = math.hash(new uint4(hash, (uint)slots[end].attribute, (uint)slots[end].components, math.hash(math.asuint(slots[end].defaultValue))));
                data.paintMaterials.TryAdd(material, new ChiselPaintRange { start = start, count = end - start, hash = hash });
                start = end;
            }
        }
    }
}
