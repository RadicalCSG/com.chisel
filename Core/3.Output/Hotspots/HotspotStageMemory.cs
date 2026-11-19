using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Mathematics;

namespace Chisel.Core
{
    // What the hotspot stage found for a brush at the last update: its surfaces' kinds and hashes (HotspotGroups.KindsOf)
    // and the hash of its buffer without what the cut writes into it (OutputHotspotSections.ContentHash)
    internal struct HotspotStageBrush
    {
        public CompactNodeID id;
        public bool used;
        // the update that last had it (HotspotStageMemory.stamp)
        public uint seen;
        public UnsafeList<byte>  kinds;
        public UnsafeList<uint4> hashes;
        // how many of its surfaces have a kind (candidates and slivers), once counted (HotspotStageMemory.CountKinds)
        public int candidates;
        public uint4 content;
        public bool  hasContent;

        public void Free()
        {
            if (kinds.IsCreated)  kinds.Dispose();
            if (hashes.IsCreated) hashes.Dispose();
            kinds = default; hashes = default;
            candidates = 0;
            hasContent = false;
        }
    }

    // The kinds and hashes of each brush's surfaces as the hotspot stage keeps them (HotspotStageMemory), read through the
    // brush's slot; candidates: how many surfaces of the model have a kind
    internal struct StageKinds
    {
        public NativeArray<int> slotOfBrush;
        public NativeArray<HotspotStageBrush> brushes;
        public int candidates;

        public readonly bool IsCreated => brushes.IsCreated;
    }

    // The hotspot stage's results kept with the tree, for the brushes whose buffers stay the same (WeldMemory.sameBuffer)
    internal struct HotspotStageMemory
    {
        public NativeList<HotspotStageBrush> brushes;   // by brush slot (BrushSlots.Of)
        public NativeReference<uint4> layoutsKey;
        // the kinds were made for the layouts of layoutsKey (CountKinds after Prepare)
        public NativeReference<bool>  valid;
        // how many surfaces of the model have a kind (HotspotStageBrush.candidates)
        public NativeReference<int>   candidates;
        // the update (counted by Prepare), and how many slots are used
        public NativeReference<uint> stamp;
        public NativeReference<int>  usedCount;
        // this update, brush by brush: its slot, and whether its kinds and its content hash are what was kept
        public NativeList<int>  slotOfBrush;
        public NativeList<bool> keptKinds, keptContent;
        // this update, the brushes whose kinds are made again (MakeKinds)
        public NativeList<int>  redo;
        // the brushes' own buffers holding group ids (OutputHotspotSections.StartCutKeys)
        public NativeParallelHashSet<BlobAssetReference<ChiselBrushRenderBuffer>> groupedBuffers;

        public static HotspotStageMemory Create(Allocator allocator)
        {
            return new HotspotStageMemory
            {
                groupedBuffers = new NativeParallelHashSet<BlobAssetReference<ChiselBrushRenderBuffer>>(16, allocator),
                brushes     = new NativeList<HotspotStageBrush>(64, allocator),
                layoutsKey  = new NativeReference<uint4>(allocator),
                valid       = new NativeReference<bool>(allocator),
                candidates  = new NativeReference<int>(allocator),
                stamp       = new NativeReference<uint>(allocator),
                usedCount   = new NativeReference<int>(allocator),
                slotOfBrush = new NativeList<int>(64, allocator),
                keptKinds   = new NativeList<bool>(64, allocator),
                keptContent = new NativeList<bool>(64, allocator),
                redo        = new NativeList<int>(64, allocator)
            };
        }

        public void Dispose()
        {
            if (brushes.IsCreated)
            {
                for (int i = 0; i < brushes.Length; i++)
                    brushes.ElementAt(i).Free();
                brushes.Dispose();
            }
            if (layoutsKey.IsCreated)  layoutsKey.Dispose();
            if (valid.IsCreated)       valid.Dispose();
            if (candidates.IsCreated)  candidates.Dispose();
            if (stamp.IsCreated)       stamp.Dispose();
            if (usedCount.IsCreated)   usedCount.Dispose();
            if (slotOfBrush.IsCreated) slotOfBrush.Dispose();
            if (keptKinds.IsCreated)   keptKinds.Dispose();
            if (keptContent.IsCreated) keptContent.Dispose();
            if (redo.IsCreated)        redo.Dispose();
            if (groupedBuffers.IsCreated) groupedBuffers.Dispose();
        }

        // Which brushes keep what was found for them (sameBuffer: the brush's buffer is the one of the last update), room
        // for the others, whose kinds are made again (redo: MakeKinds, then CountKinds); the kinds also need the same
        // layouts. A brush with the same buffer has as many surfaces as kinds were kept for it: only the others' buffers
        // are read
        public void Prepare(NativeList<BrushData> brushes, NativeArray<HotspotLayoutData> layouts, NativeList<bool> sameBuffer)
        {
            var key = layouts.Hash128();
            var sameLayouts = valid.Value && layoutsKey.Value.Equals(key);
            layoutsKey.Value = key;
            valid.Value = false;
            var now = ++stamp.Value;
            slotOfBrush.ResizeUninitialized(brushes.Length);
            keptKinds.ResizeUninitialized(brushes.Length);
            keptContent.ResizeUninitialized(brushes.Length);
            redo.Clear();
            for (int b = 0; b < brushes.Length; b++)
            {
                var id   = brushes[b].brushIndexOrder.compactNodeID;
                var slot = BrushSlots.Of(id);
                if (slot >= this.brushes.Length)
                    this.brushes.Resize(slot + 1, NativeArrayOptions.ClearMemory);
                ref var brush = ref this.brushes.ElementAt(slot);
                var known = brush.used && brush.id == id;
                if (!known)
                {
                    // a slot another brush had: what was found for that one goes
                    if (!brush.used)
                        usedCount.Value++;
                    candidates.Value -= brush.candidates;
                    brush.Free();
                    brush = new HotspotStageBrush { id = id, used = true };
                }
                brush.seen = now;
                slotOfBrush[b] = slot;
                var same = known && b < sameBuffer.Length && sameBuffer[b] && brush.kinds.IsCreated;
                var contentKept = same && brush.hasContent;
                var kindsKept   = same && sameLayouts;
                keptContent[b] = contentKept;
                keptKinds[b]   = kindsKept;
                if (!contentKept)
                    brush.hasContent = false;
                if (kindsKept)
                    continue;
                var surfaceCount = same ? brush.kinds.Length :
                                   brushes[b].brushRenderBuffer.IsCreated ? brushes[b].brushRenderBuffer.Value.surfaces.Length : 0;
                if (!brush.kinds.IsCreated)
                {
                    brush.kinds  = new UnsafeList<byte>(surfaceCount, Allocator.Persistent);
                    brush.hashes = new UnsafeList<uint4>(surfaceCount, Allocator.Persistent);
                }
                brush.kinds.Resize(surfaceCount, NativeArrayOptions.ClearMemory);
                brush.hashes.Resize(surfaceCount, NativeArrayOptions.ClearMemory);
                candidates.Value -= brush.candidates;
                brush.candidates = 0;
                if (surfaceCount > 0)
                    redo.Add(b);
            }

            // the brushes that are gone: each brush has a slot of its own, so there are some when more slots are used
            if (usedCount.Value <= brushes.Length)
                return;
            for (int i = 0; i < this.brushes.Length; i++)
            {
                ref var brush = ref this.brushes.ElementAt(i);
                if (!brush.used || brush.seen == now)
                    continue;
                candidates.Value -= brush.candidates;
                brush.Free();
                brush = default;
                usedCount.Value--;
            }
        }

        // The kinds of a brush to make again (redo) made into its slot (HotspotGroups.KindsOf); each brush is independent
        // of the others
        public static void MakeKinds(int b, NativeArray<BrushData> brushes, NativeArray<HotspotLayoutData> layouts, NativeArray<int> slotOfBrush,
                                     NativeArray<HotspotStageBrush> stage)
        {
            if (!brushes[b].brushRenderBuffer.IsCreated)
                return;
            var brush = stage[slotOfBrush[b]];
            HotspotGroups.KindsOf(ref brushes[b].brushRenderBuffer.Value, layouts, brush.kinds, brush.hashes);
        }

        // The brushes whose kinds were made again (MakeKinds) counted in: how many of their surfaces have a kind. Until
        // then nothing is kept at the next update
        public void CountKinds()
        {
            for (int i = 0; i < redo.Length; i++)
            {
                ref var brush = ref brushes.ElementAt(slotOfBrush[redo[i]]);
                var count = 0;
                for (int s = 0; s < brush.kinds.Length; s++)
                    count += brush.kinds[s] != 0 ? 1 : 0;
                brush.candidates = count;
                candidates.Value += count;
            }
            redo.Clear();
            valid.Value = true;
        }

        // Each brush's kinds as kept, once counted (CountKinds)
        public StageKinds Kinds => new StageKinds { slotOfBrush = slotOfBrush.AsArray(), brushes = brushes.AsArray(), candidates = candidates.Value };
    }
}
