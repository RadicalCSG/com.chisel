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
        [ReadOnly] public NativeArray<float4>                                    values;
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
        public static unsafe uint HashOf(in ChiselPaintLayer layer, float4* values)
        {
            var copy = layer;
            copy.valueStart = 0;
            var hash = math.hash(&copy, UnsafeUtility.SizeOf<ChiselPaintLayer>());
            return math.hash(new uint2(hash, math.hash(values + layer.valueStart, layer.ValueCount * UnsafeUtility.SizeOf<float4>())));
        }

        /// <summary>Replaces the paint of one entity: its layers, and the values their grids take from. True when it changed;
        /// its brushes then have to be rebuilt to show it.</summary>
        public static unsafe bool SetPaint(CSGTree tree, ulong entityID, NativeArray<ChiselPaintLayer> layers, NativeArray<float4> values)
        {
            if (!tree.Valid)
                return false;
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
            var valuesPointer = values.IsCreated ? (float4*)values.GetUnsafeReadOnlyPtr() : null;
            for (int l = 0; l < sorted.Length; l++)
            {
                hashes.Add(HashOf(sorted[l], valuesPointer));
                entityHash = math.hash(new uint2(entityHash, hashes[l]));
            }
            data.paintEntityHashes.TryGetValue(entityID, out var previous);
            if (sorted.Length == 0 ? !data.paintEntityHashes.ContainsKey(entityID) : previous == entityHash && data.paintEntityHashes.ContainsKey(entityID))
                return false;

            data.lastJobHandle.Complete();

            // its old layers out, their values left behind
            var first = 0;
            while (first < data.paintLayers.Length && data.paintLayers[first].entityID != entityID)
                first++;
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
            return true;
        }

        // The values of the layers only, in their order
        static void Compact(ChiselTreeLookup.Data data)
        {
            using var compacted = new NativeList<float4>(math.max(1, data.paintValues.Length - data.paintUnusedValues), Allocator.Temp);
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
        public static bool SetPaint(CSGTree tree, NativeArray<ChiselPaintLayer> layers, NativeArray<float4> values, NativeList<ulong> changedEntities)
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
                    hash = math.hash(new uint3(hash, (uint)slots[end].attribute, math.hash(math.asuint(slots[end].defaultValue))));
                data.paintMaterials.TryAdd(material, new ChiselPaintRange { start = start, count = end - start, hash = hash });
                start = end;
            }
        }
    }
}
