using System.Collections.Generic;
using Unity.Collections;
using Unity.Mathematics;

namespace Chisel.Core
{
    internal static class ChiselDecalStore
    {
        struct ByEntityID : IComparer<ChiselDecalInstance>
        {
            public int Compare(ChiselDecalInstance x, ChiselDecalInstance y) => x.entityID.CompareTo(y.entityID);
        }

        // Lowest decal first: the order GenerateSurfaceTrianglesJob stacks them in
        struct ByStackingOrder : IComparer<DecalVolume>
        {
            public int Compare(DecalVolume x, DecalVolume y)
            {
                var order = x.order.CompareTo(y.order);
                return order != 0 ? order : x.entityID.CompareTo(y.entityID);
            }
        }

        static bool IsEmpty(in MinMaxAABB bounds) => math.any(bounds.Min > bounds.Max);

        public static void SetDecals(CSGTree tree, NativeArray<ChiselDecalInstance> decals, NativeArray<ChiselDecalTarget> targets)
        {
            if (!tree.Valid)
                return;

            var data = ChiselTreeLookup.Value[tree];
            data.lastJobHandle.Complete();

            var count       = decals.IsCreated ? decals.Length : 0;
            var targetCount = targets.IsCreated ? targets.Length : 0;
            NativeList<ChiselDecalInstance> sorted;
            using var _sorted = sorted = new NativeList<ChiselDecalInstance>(math.max(1, count), Allocator.Temp);
            if (count > 0)
            {
                sorted.AddRange(decals);
                sorted.Sort(new ByEntityID());

                // One decal per id: the first one wins
                var unique = 1;
                for (int i = 1; i < sorted.Length; i++)
                {
                    if (sorted[i].entityID != sorted[unique - 1].entityID)
                        sorted[unique++] = sorted[i];
                }
                sorted.Length = unique;
            }

            // The targets packed in the order of the sorted decals. A decal whose targets reach outside the array
            // keeps the ones inside it.
            NativeList<ChiselDecalTarget> packedTargets;
            using var _packedTargets = packedTargets = new NativeList<ChiselDecalTarget>(math.max(1, targetCount), Allocator.Temp);
            for (int i = 0; i < sorted.Length; i++)
            {
                var decal  = sorted[i];
                var wanted = decal.targetCount;
                var start  = math.max(0, decal.targetStart);
                var end    = math.min(targetCount, decal.targetStart + math.max(0, wanted));
                decal.targetStart = packedTargets.Length;
                decal.targetCount = 0;
                if (wanted > 0)
                {
                    for (int t = start; t < end; t++)
                        packedTargets.Add(targets[t]);
                    decal.targetCount = math.max(0, end - start);
                    // Asked for targets but got none that exist: it draws nothing, rather than everywhere
                    if (decal.targetCount == 0)
                        decal.renderMaterial = 0;
                }
                sorted[i] = decal;
            }

            using var bounds  = new NativeList<MinMaxAABB>(sorted.Length, Allocator.Temp);
            NativeList<DecalVolume> volumes;
            using var _volumes = volumes = new NativeList<DecalVolume>(sorted.Length, Allocator.Temp);
            for (int i = 0; i < sorted.Length; i++)
            {
                if (DecalVolume.TryCreate(sorted[i], out var volume))
                {
                    volume.targetStart = sorted[i].targetStart;
                    volume.targetCount = sorted[i].targetCount;
                    volumes.Add(volume);
                    bounds.Add(new MinMaxAABB { Min = volume.boundsMin, Max = volume.boundsMax });
                } else
                    bounds.Add(MinMaxAABB.Empty);
            }

            // Where anything changed, as the volumes before and after
            var oldDecals  = data.decalInstances;
            var oldBounds  = data.decalBounds;
            var oldTargets = data.decalTargets;
            var changedBefore = data.changedDecalBounds.Length;
            int o = 0, n = 0;
            while (o < oldDecals.Length || n < sorted.Length)
            {
                if (n == sorted.Length ||
                    (o < oldDecals.Length && oldDecals[o].entityID < sorted[n].entityID))
                {
                    AddChanged(data, oldBounds[o]);
                    o++;
                } else
                if (o == oldDecals.Length ||
                    sorted[n].entityID < oldDecals[o].entityID)
                {
                    AddChanged(data, bounds[n]);
                    n++;
                } else
                {
                    if (!SameDecal(oldDecals[o], oldTargets, sorted[n], packedTargets))
                    {
                        AddChanged(data, oldBounds[o]);
                        AddChanged(data, bounds[n]);
                    }
                    o++;
                    n++;
                }
            }

            data.decalInstances.Clear();
            data.decalInstances.AddRange(sorted.AsArray());
            data.decalBounds.Clear();
            data.decalBounds.AddRange(bounds.AsArray());
            data.decalTargets.Clear();
            data.decalTargets.AddRange(packedTargets.AsArray());

            // The volumes stack lowest first; their targets are packed again in that order
            volumes.Sort(new ByStackingOrder());
            data.decalVolumeTargets.Clear();
            for (int i = 0; i < volumes.Length; i++)
            {
                var volume = volumes[i];
                var start  = volume.targetStart;
                volume.targetStart = data.decalVolumeTargets.Length;
                for (int t = 0; t < volume.targetCount; t++)
                    data.decalVolumeTargets.Add(packedTargets[start + t]);
                volumes[i] = volume;
            }
            data.decalVolumes.Clear();
            data.decalVolumes.AddRange(volumes.AsArray());

            if (data.changedDecalBounds.Length > changedBefore)
                tree.SetDirty();
        }

        // The same decal, apart from where its targets sit in the array
        static bool SameDecal(ChiselDecalInstance a, NativeList<ChiselDecalTarget> aTargets,
                              ChiselDecalInstance b, NativeList<ChiselDecalTarget> bTargets)
        {
            if (a.targetCount != b.targetCount)
                return false;
            for (int t = 0; t < a.targetCount; t++)
            {
                if (!aTargets[a.targetStart + t].Equals(bTargets[b.targetStart + t]))
                    return false;
            }
            a.targetStart = 0;
            b.targetStart = 0;
            return a.Equals(b);
        }

        static void AddChanged(ChiselTreeLookup.Data data, MinMaxAABB bounds)
        {
            if (!IsEmpty(bounds))
                data.changedDecalBounds.Add(bounds);
        }

        public static int GetDecalCount(CSGTree tree)
        {
            if (!tree.Valid || !ChiselTreeLookup.Value.HasTree(tree))
                return 0;
            return ChiselTreeLookup.Value[tree].decalInstances.Length;
        }
    }
}
