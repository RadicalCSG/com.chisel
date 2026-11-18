using System.Runtime.CompilerServices;

namespace Chisel.Core
{
    // The stages that remember something of each brush of a tree from one update to the next (the weld, the cleanup plan,
    // the hotspot stage, the mesh sections) keep it by the brush's slot in its hierarchy: its node id's slot index, which
    // stays the brush's while it is in the hierarchy and is dense, so nothing has to be looked up by id. A slot given to
    // another node has another generation, so a memory keeps the id with what it remembers and takes a different id in
    // the same slot as that brush gone and a new one there.
    internal static class BrushSlots
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int Of(CompactNodeID id) => id.slotIndex.index;
    }
}
