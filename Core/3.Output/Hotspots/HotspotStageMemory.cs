using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;

namespace Chisel.Core
{
    // What the hotspot stage found for a brush at the last update: its surfaces' kinds and hashes (HotspotGroups.KindsOf)
    // and the hash of its buffer without what the cut writes into it (OutputHotspotSections.ContentHash)
    internal struct HotspotStageBrush
    {
        public CompactNodeID id;
        public bool used, live;
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
        public NativeList<HotspotStageBrush> brushes;   // by slot
        public NativeList<int> freeSlots;
        public NativeParallelHashMap<CompactNodeID, int> slotOf;
        public NativeReference<uint4> layoutsKey;
        public NativeReference<bool>  valid;
        // this update, brush by brush: its slot, and whether its kinds and its content hash are what was kept
        public NativeList<int>  slotOfBrush;
        public NativeList<bool> keptKinds, keptContent;

        public static HotspotStageMemory Create(Allocator allocator)
        {
            return new HotspotStageMemory
            {
                brushes     = new NativeList<HotspotStageBrush>(64, allocator),
                freeSlots   = new NativeList<int>(16, allocator),
                slotOf      = new NativeParallelHashMap<CompactNodeID, int>(64, allocator),
                layoutsKey  = new NativeReference<uint4>(allocator),
                valid       = new NativeReference<bool>(allocator),
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
            if (freeSlots.IsCreated)   freeSlots.Dispose();
            if (slotOf.IsCreated)      slotOf.Dispose();
            if (layoutsKey.IsCreated)  layoutsKey.Dispose();
            if (valid.IsCreated)       valid.Dispose();
            if (slotOfBrush.IsCreated) slotOfBrush.Dispose();
            if (keptKinds.IsCreated)   keptKinds.Dispose();
            if (keptContent.IsCreated) keptContent.Dispose();
        }

        // Which brushes keep what was found for them (sameBuffer: the brush's buffer is the one of the last update), room
        // for the others; the kinds also need the same layouts
        public unsafe void Prepare(NativeList<BrushData> brushes, NativeArray<HotspotLayoutData> layouts, NativeList<bool> sameBuffer)
        {
            var key = layouts.IsCreated && layouts.Length > 0 ? xxHash3.Hash128(layouts.GetUnsafeReadOnlyPtr(), (long)layouts.Length * UnsafeUtility.SizeOf<HotspotLayoutData>()) : default;
            var sameLayouts = valid.Value && layoutsKey.Value.Equals(key);
            layoutsKey.Value = key;
            valid.Value = true;
            for (int i = 0; i < this.brushes.Length; i++)
                this.brushes.ElementAt(i).live = false;
            slotOfBrush.ResizeUninitialized(brushes.Length);
            keptKinds.ResizeUninitialized(brushes.Length);
            keptContent.ResizeUninitialized(brushes.Length);
            for (int b = 0; b < brushes.Length; b++)
            {
                var id = brushes[b].brushIndexOrder.compactNodeID;
                var known = slotOf.TryGetValue(id, out var slot);
                if (!known)
                {
                    if (freeSlots.Length > 0)
                    {
                        slot = freeSlots[freeSlots.Length - 1];
                        freeSlots.RemoveAt(freeSlots.Length - 1);
                    } else
                    {
                        slot = this.brushes.Length;
                        this.brushes.Add(default);
                    }
                    this.brushes[slot] = new HotspotStageBrush { id = id, used = true };
                    slotOf.Add(id, slot);
                }
                ref var brush = ref this.brushes.ElementAt(slot);
                brush.live = true;
                slotOfBrush[b] = slot;
                var surfaceCount = brushes[b].brushRenderBuffer.IsCreated ? brushes[b].brushRenderBuffer.Value.surfaces.Length : 0;
                var same = known && b < sameBuffer.Length && sameBuffer[b];
                keptContent[b] = same && brush.hasContent;
                keptKinds[b]   = same && sameLayouts && brush.kinds.IsCreated && brush.kinds.Length == surfaceCount;
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
            }
            // the brushes that are gone
            for (int i = 0; i < this.brushes.Length; i++)
            {
                ref var brush = ref this.brushes.ElementAt(i);
                if (!brush.used || brush.live)
                    continue;
                slotOf.Remove(brush.id);
                brush.Free();
                brush = default;
                freeSlots.Add(i);
            }
        }
    }
}
