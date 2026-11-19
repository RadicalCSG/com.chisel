using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Chisel.Core
{
    // What the cleanup's last plan found for each brush (OutputMeshCleanup.PrepareFromMemory), kept with the tree so the
    // next plan measures again, and compares with the others, only the brushes whose buffers changed: a buffer keeps its
    // serial while it holds the same (ChiselBrushRenderBuffer.serial)
    internal struct MeshCleanupPlanMemory
    {
        public struct Slot
        {
            public CompactNodeID id;
            // the serial of the buffer key and box were measured from; 0 for no buffer, or for one built without a serial
            // (measured each time)
            public ulong      serial;
            public ulong      key;
            public MinMaxAABB box;
            // where the brush is in this update's brushes, and the update that saw it last
            public int        brush;
            public int        stamp;
            public bool       alive;
            public bool       hasBuffer;
            // what the cache gave the brush at the last plan that planned it (OutputMeshCleanup.PlanFromMemory), or what the
            // cleanup made of it (Finish); pending: planned to be cleaned up and not yet given what was made
            public BlobAssetReference<ChiselBrushRenderBuffer> result;
            public bool       pending;
        }

        // by brush slot (BrushSlots.Of)
        public NativeList<Slot>                          slots;
        // the slots each slot's brush touches, both ways
        public NativeParallelMultiHashMap<int, int>      touching;
        public NativeReference<int>                      stamp;
        public NativeReference<bool>                     valid;
        // whether the last plan measured every brush again, and how many it measured
        public NativeReference<bool>                     lastFull;
        public NativeReference<int>                      lastMeasured;
        // this update, by slot: the brushes whose result an edit can have changed (measured again, or touching one that
        // was or that is gone), planned again; the others keep their result
        public NativeList<bool>                          affected;
        // whether the last plan recorded the cleanup's changes: its keys are made with it
        public NativeReference<bool>                     lastRecord;
        // this update: the brushes the cache forgets (gone, replaced by another brush in their slot, left without a
        // buffer); with every brush measured again it forgets all it wasn't given (lastFull)
        public NativeList<CompactNodeID>                 forget;

        public static MeshCleanupPlanMemory Create(Allocator allocator)
        {
            return new MeshCleanupPlanMemory
            {
                slots        = new NativeList<Slot>(1024, allocator),
                touching     = new NativeParallelMultiHashMap<int, int>(4096, allocator),
                stamp        = new NativeReference<int>(allocator),
                valid        = new NativeReference<bool>(allocator),
                lastFull     = new NativeReference<bool>(allocator),
                lastMeasured = new NativeReference<int>(allocator),
                affected     = new NativeList<bool>(1024, allocator),
                lastRecord   = new NativeReference<bool>(allocator),
                forget       = new NativeList<CompactNodeID>(16, allocator)
            };
        }

        public bool IsCreated => slots.IsCreated;

        public void Clear()
        {
            slots.Clear();
            touching.Clear();
            forget.Clear();
            valid.Value = false;
        }

        public void Dispose()
        {
            if (slots.IsCreated)        slots.Dispose();
            if (touching.IsCreated)     touching.Dispose();
            if (stamp.IsCreated)        stamp.Dispose();
            if (valid.IsCreated)        valid.Dispose();
            if (lastFull.IsCreated)     lastFull.Dispose();
            if (lastMeasured.IsCreated) lastMeasured.Dispose();
            if (affected.IsCreated)     affected.Dispose();
            if (lastRecord.IsCreated)   lastRecord.Dispose();
            if (forget.IsCreated)       forget.Dispose();
        }
    }
}
