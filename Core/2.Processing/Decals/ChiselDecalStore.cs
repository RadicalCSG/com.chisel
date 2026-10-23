using System.Collections.Generic;
using Unity.Collections;
using Unity.Mathematics;

namespace Chisel.Core
{
    internal static class ChiselDecalStore
    {
        // A decal and where it was in the list it was given in
        struct Sequenced
        {
            public ChiselDecalInstance decal;
            public int sequence;
        }

        struct ByEntityID : IComparer<Sequenced>
        {
            public int Compare(Sequenced x, Sequenced y) => x.decal.entityID.CompareTo(y.decal.entityID);
        }

        // Lowest decal first: the order GenerateSurfaceTrianglesJob stacks them in; of one order, as they were given (an
        // entity id is another every time a scene is loaded)
        struct ByStackingOrder : IComparer<DecalVolume>
        {
            public int Compare(DecalVolume x, DecalVolume y)
            {
                var order = x.order.CompareTo(y.order);
                return order != 0 ? order : x.sequence.CompareTo(y.sequence);
            }
        }

        struct ByFirst : IComparer<int4>
        {
            public int Compare(int4 x, int4 y) => x.x.CompareTo(y.x);
        }

        struct BySecond : IComparer<int4>
        {
            public int Compare(int4 x, int4 y) => x.y.CompareTo(y.y);
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
            NativeList<int> sequences;
            using var _sequences = sequences = new NativeList<int>(math.max(1, count), Allocator.Temp);
            if (count > 0)
            {
                // One decal per id: the first one given wins; each kept with where it was given
                using var seen = new NativeParallelHashSet<ulong>(count, Allocator.Temp);
                using var given = new NativeList<Sequenced>(count, Allocator.Temp);
                for (int i = 0; i < count; i++)
                {
                    if (seen.Add(decals[i].entityID))
                        given.Add(new Sequenced { decal = decals[i], sequence = given.Length });
                }
                given.Sort(new ByEntityID());
                for (int i = 0; i < given.Length; i++)
                {
                    sorted.Add(given[i].decal);
                    sequences.Add(given[i].sequence);
                }
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
                    volume.sequence    = sequences[i];
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
            var oldSequences = data.decalSequences;
            var changedBefore = data.changedDecalBounds.Length;
            // the decals there before and now: their sequence before and now, their index before and now
            NativeList<int4> stayed;
            using var _stayed = stayed = new NativeList<int4>(math.min(oldDecals.Length, sorted.Length) + 1, Allocator.Temp);
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
                    stayed.Add(new int4(o < oldSequences.Length ? oldSequences[o] : o, sequences[n], o, n));
                    o++;
                    n++;
                }
            }

            // A decal that stayed but now stacks elsewhere among the others that stayed (adding or removing one moves
            // nothing): what it covers is made again, before and after
            if (stayed.Length > 1)
            {
                using var rankBefore = new NativeArray<int>(stayed.Length, Allocator.Temp);
                stayed.Sort(new BySecond());
                for (int i = 0; i < stayed.Length; i++)
                {
                    var entry = stayed[i];
                    entry.y = i;
                    stayed[i] = entry;
                }
                stayed.Sort(new ByFirst());
                for (int i = 0; i < stayed.Length; i++)
                {
                    if (stayed[i].y == i)
                        continue;
                    AddChanged(data, oldBounds[stayed[i].z]);
                    AddChanged(data, bounds[stayed[i].w]);
                }
            }

            data.decalInstances.Clear();
            data.decalInstances.AddRange(sorted.AsArray());
            data.decalSequences.Clear();
            data.decalSequences.AddRange(sequences.AsArray());
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
