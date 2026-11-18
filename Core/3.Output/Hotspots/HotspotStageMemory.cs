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
        public uint4 content;
        public bool  hasContent;

        public void Free()
        {
            if (kinds.IsCreated)  kinds.Dispose();
            if (hashes.IsCreated) hashes.Dispose();
            kinds = default; hashes = default;
            hasContent = false;
        }
    }

    // The hotspot stage's results kept with the tree, for the brushes whose buffers stay the same (WeldMemory.sameBuffer)
    internal struct HotspotStageMemory
    {
        public NativeList<HotspotStageBrush> brushes;   // by brush slot (BrushSlots.Of)
        public NativeReference<uint4> layoutsKey;
        public NativeReference<bool>  valid;
        // the update (counted by Prepare), and how many slots are used
        public NativeReference<uint> stamp;
        public NativeReference<int>  usedCount;
        // this update, brush by brush: its slot, and whether its kinds and its content hash are what was kept
        public NativeList<int>  slotOfBrush;
        public NativeList<bool> keptKinds, keptContent;
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
                stamp       = new NativeReference<uint>(allocator),
                usedCount   = new NativeReference<int>(allocator),
                slotOfBrush = new NativeList<int>(64, allocator),
                keptKinds   = new NativeList<bool>(64, allocator),
                keptContent = new NativeList<bool>(64, allocator)
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
            if (stamp.IsCreated)       stamp.Dispose();
            if (usedCount.IsCreated)   usedCount.Dispose();
            if (slotOfBrush.IsCreated) slotOfBrush.Dispose();
            if (keptKinds.IsCreated)   keptKinds.Dispose();
            if (keptContent.IsCreated) keptContent.Dispose();
            if (groupedBuffers.IsCreated) groupedBuffers.Dispose();
        }

        // Which brushes keep what was found for them (sameBuffer: the brush's buffer is the one of the last update), room
        // for the others; the kinds also need the same layouts. And room for each surface's kind in find, brush by brush
        // (kindStart, read by HotspotGroups.KindsOf): a brush with the same buffer has as many surfaces as kinds were kept
        // for it, so only the others' buffers are read
        public void Prepare(NativeList<BrushData> brushes, NativeArray<HotspotLayoutData> layouts, NativeList<bool> sameBuffer, ref FindState find)
        {
            var key = layouts.Hash128();
            var sameLayouts = valid.Value && layoutsKey.Value.Equals(key);
            layoutsKey.Value = key;
            valid.Value = true;
            var now = ++stamp.Value;
            slotOfBrush.ResizeUninitialized(brushes.Length);
            keptKinds.ResizeUninitialized(brushes.Length);
            keptContent.ResizeUninitialized(brushes.Length);
            find.kindStart.ResizeUninitialized(brushes.Length + 1);
            var total = 0;
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
                    brush.Free();
                    brush = new HotspotStageBrush { id = id, used = true };
                }
                brush.seen = now;
                slotOfBrush[b] = slot;
                var same = known && b < sameBuffer.Length && sameBuffer[b] && brush.kinds.IsCreated;
                var surfaceCount = same ? brush.kinds.Length :
                                   brushes[b].brushRenderBuffer.IsCreated ? brushes[b].brushRenderBuffer.Value.surfaces.Length : 0;
                keptContent[b] = same && brush.hasContent;
                keptKinds[b]   = same && sameLayouts;
                if (!keptContent[b])
                    brush.hasContent = false;
                if (!keptKinds[b])
                {
                    if (!brush.kinds.IsCreated)
                    {
                        brush.kinds  = new UnsafeList<byte>(surfaceCount, Allocator.Persistent);
                        brush.hashes = new UnsafeList<uint4>(surfaceCount, Allocator.Persistent);
                    }
                    brush.kinds.Resize(surfaceCount);
                    brush.hashes.Resize(surfaceCount);
                }
                find.kindStart[b] = total;
                total += surfaceCount;
            }
            find.kindStart[brushes.Length] = total;
            find.kinds.ResizeUninitialized(total);
            find.hashes.ResizeUninitialized(total);

            // the brushes that are gone: each brush has a slot of its own, so there are some when more slots are used
            if (usedCount.Value <= brushes.Length)
                return;
            for (int i = 0; i < this.brushes.Length; i++)
            {
                ref var brush = ref this.brushes.ElementAt(i);
                if (!brush.used || brush.seen == now)
                    continue;
                brush.Free();
                brush = default;
                usedCount.Value--;
            }
        }
    }
}
