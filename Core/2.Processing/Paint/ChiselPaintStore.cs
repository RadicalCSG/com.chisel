using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
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

    // A surface's layers in the update's layer array, and a hash of them and their values
    internal struct ChiselPaintRange
    {
        public int  start;
        public int  count;
        public uint hash;
    }

    // The tree's paint as an update reads it: each painted surface's layers, found by its key
    internal struct ChiselPaintLookup
    {
        [ReadOnly] public NativeParallelHashMap<ChiselPaintKey, ChiselPaintRange> ranges;
        [ReadOnly] public NativeArray<ChiselPaintLayer> layers;
        [ReadOnly] public NativeArray<float4>           values;
        public bool                                     hasPaint;
        // the attributes of the materials whose shaders take paint, by material (ChiselPaintMaterials)
        [ReadOnly] public NativeParallelHashMap<ulong, ChiselPaintRange> materials;
        [ReadOnly] public NativeArray<ChiselPaintMaterialSlot>          materialSlots;
        public bool                                     hasMaterials;

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

        public static ChiselPaintLookup Create(NativeList<ChiselPaintLayer> layers, NativeList<float4> values,
                                               NativeList<ChiselPaintMaterialSlot> materialSlots, Allocator allocator)
        {
            var count  = layers.IsCreated ? layers.Length : 0;
            var slots  = materialSlots.IsCreated ? materialSlots.Length : 0;
            var lookup = new ChiselPaintLookup
            {
                ranges        = new NativeParallelHashMap<ChiselPaintKey, ChiselPaintRange>(math.max(1, count), allocator),
                layers        = new NativeArray<ChiselPaintLayer>(math.max(1, count), allocator),
                values        = new NativeArray<float4>(math.max(1, values.IsCreated ? values.Length : 0), allocator),
                materials     = new NativeParallelHashMap<ulong, ChiselPaintRange>(math.max(1, slots), allocator),
                materialSlots = new NativeArray<ChiselPaintMaterialSlot>(math.max(1, slots), allocator)
            };
            if (slots > 0)
            {
                lookup.hasMaterials = true;
                NativeArray<ChiselPaintMaterialSlot>.Copy(materialSlots.AsArray(), lookup.materialSlots, slots);
                // a material's slots are together (ChiselPaintMaterials)
                for (int start = 0; start < slots;)
                {
                    var material = materialSlots[start].material;
                    var end  = start;
                    var hash = 0u;
                    for (; end < slots && materialSlots[end].material == material; end++)
                        hash = math.hash(new uint3(hash, (uint)materialSlots[end].attribute, math.hash(math.asuint(materialSlots[end].defaultValue))));
                    lookup.materials.TryAdd(material, new ChiselPaintRange { start = start, count = end - start, hash = hash });
                    start = end;
                }
            }
            if (count == 0)
                return lookup;
            lookup.hasPaint = true;
            NativeArray<ChiselPaintLayer>.Copy(layers.AsArray(), lookup.layers, count);
            NativeArray<float4>.Copy(values.AsArray(), lookup.values, values.Length);
            // the layers are sorted by surface (ChiselPaintStore.SetPaint)
            for (int start = 0; start < count;)
            {
                var key = new ChiselPaintKey { entityID = layers[start].entityID, surfaceIndex = layers[start].surfaceIndex };
                var end = start + 1;
                while (end < count && layers[end].entityID == key.entityID && layers[end].surfaceIndex == key.surfaceIndex)
                    end++;
                var hash = 0u;
                for (int l = start; l < end; l++)
                    hash = math.hash(new uint2(hash, ChiselPaintStore.HashOf(layers[l], values)));
                lookup.ranges.TryAdd(key, new ChiselPaintRange { start = start, count = end - start, hash = hash });
                start = end;
            }
            return lookup;
        }

        public JobHandle Dispose(JobHandle dependency)
        {
            var handle = dependency;
            if (ranges.IsCreated) handle = JobHandle.CombineDependencies(handle, ranges.Dispose(dependency));
            if (layers.IsCreated) handle = JobHandle.CombineDependencies(handle, layers.Dispose(dependency));
            if (values.IsCreated) handle = JobHandle.CombineDependencies(handle, values.Dispose(dependency));
            if (materials.IsCreated)     handle = JobHandle.CombineDependencies(handle, materials.Dispose(dependency));
            if (materialSlots.IsCreated) handle = JobHandle.CombineDependencies(handle, materialSlots.Dispose(dependency));
            this = default;
            return handle;
        }
    }

    internal static class ChiselPaintStore
    {
        struct BySurface : IComparer<ChiselPaintLayer>
        {
            public int Compare(ChiselPaintLayer x, ChiselPaintLayer y)
            {
                if (x.entityID != y.entityID) return x.entityID.CompareTo(y.entityID);
                if (x.surfaceIndex != y.surfaceIndex) return x.surfaceIndex.CompareTo(y.surfaceIndex);
                return ((int)x.attribute).CompareTo((int)y.attribute);
            }
        }

        // A layer and its values
        public static unsafe uint HashOf(in ChiselPaintLayer layer, NativeList<float4> values)
        {
            var copy = layer;
            copy.valueStart = 0;
            var hash = math.hash(&copy, UnsafeUtility.SizeOf<ChiselPaintLayer>());
            var count = math.min(layer.ValueCount, values.Length - layer.valueStart);
            if (count > 0)
                hash = math.hash(new uint2(hash, math.hash((float4*)values.GetUnsafeReadOnlyPtr() + layer.valueStart, count * UnsafeUtility.SizeOf<float4>())));
            return hash;
        }

        // Each entity's paint, hashed
        static void HashByEntity(NativeList<ChiselPaintLayer> layers, NativeList<float4> values, NativeParallelHashMap<ulong, uint> hashes)
        {
            for (int l = 0; l < layers.Length; l++)
            {
                var entityID = layers[l].entityID;
                hashes.TryGetValue(entityID, out var hash);
                hashes[entityID] = math.hash(new uint2(hash, HashOf(layers[l], values)));
            }
        }

        public static bool SetPaint(CSGTree tree, NativeArray<ChiselPaintLayer> layers, NativeArray<float4> values, NativeList<ulong> changedEntities)
        {
            if (changedEntities.IsCreated)
                changedEntities.Clear();
            if (!tree.Valid)
                return false;
            var data = ChiselTreeLookup.Value[tree];

            // the new paint, its layers in their surfaces' order and each with its own values
            var count = layers.IsCreated ? layers.Length : 0;
            NativeList<ChiselPaintLayer> sorted;
            using var _sorted = sorted = new NativeList<ChiselPaintLayer>(math.max(1, count), Allocator.Temp);
            NativeList<float4> packed;
            using var _packed = packed = new NativeList<float4>(math.max(1, values.IsCreated ? values.Length : 0), Allocator.Temp);
            for (int l = 0; l < count; l++)
            {
                var layer = layers[l];
                if (layer.ValueCount <= 0 || layer.valueStart < 0 || layer.valueStart + layer.ValueCount > values.Length)
                    continue;
                sorted.Add(layer);
            }
            sorted.Sort(new BySurface());
            for (int l = 0; l < sorted.Length; l++)
            {
                var layer = sorted[l];
                var start = packed.Length;
                for (int v = 0; v < layer.ValueCount; v++)
                    packed.Add(values[layer.valueStart + v]);
                layer.valueStart = start;
                sorted[l] = layer;
            }

            // which entities' paint changed: theirs is new, gone, or other
            using var before = new NativeParallelHashMap<ulong, uint>(math.max(1, data.paintLayers.Length), Allocator.Temp);
            using var after  = new NativeParallelHashMap<ulong, uint>(math.max(1, sorted.Length), Allocator.Temp);
            HashByEntity(data.paintLayers, data.paintValues, before);
            HashByEntity(sorted, packed, after);
            foreach (var entry in after)
            {
                if (!before.TryGetValue(entry.Key, out var hash) || hash != entry.Value)
                    changedEntities.Add(entry.Key);
            }
            foreach (var entry in before)
            {
                if (!after.ContainsKey(entry.Key))
                    changedEntities.Add(entry.Key);
            }
            if (changedEntities.Length == 0)
                return false;

            data.lastJobHandle.Complete();
            data.paintLayers.Clear();
            data.paintLayers.AddRange(sorted.AsArray());
            data.paintValues.Clear();
            data.paintValues.AddRange(packed.AsArray());
            return true;
        }
    }
}
