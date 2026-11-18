using Unity.Collections;
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

        public static MeshCleanupPlanMemory Create(Allocator allocator)
        {
            return new MeshCleanupPlanMemory
            {
                slots        = new NativeList<Slot>(1024, allocator),
                touching     = new NativeParallelMultiHashMap<int, int>(4096, allocator),
                stamp        = new NativeReference<int>(allocator),
                valid        = new NativeReference<bool>(allocator),
                lastFull     = new NativeReference<bool>(allocator),
                lastMeasured = new NativeReference<int>(allocator)
            };
        }

        public void Clear()
        {
            slots.Clear();
            touching.Clear();
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
        }
    }
}
