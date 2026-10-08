using Unity.Collections;

namespace Chisel.Core
{
    // A connection made or broken by hand between two surfaces (surface
    // connections): connected, they are mapped as one even when their frames differ; disconnected, never, however they
    // lie. The newest link between two surfaces counts, and a link counts only while its surfaces touch.
    public struct HotspotLink : System.IEquatable<HotspotLink>
    {
        public CompactNodeID brushA, brushB;
        public int           surfaceA, surfaceB;    // the surface's index in its brush (its polygon)
        public int           order;                 // the newer, the higher
        public bool          connect;

        public readonly bool Equals(HotspotLink other) =>
            brushA == other.brushA && brushB == other.brushB && surfaceA == other.surfaceA && surfaceB == other.surfaceB &&
            order == other.order && connect == other.connect;
    }

    internal static class ChiselHotspotLinkStore
    {
        // The tree's links; true when they changed (the surfaces of the links that changed want rebuilding)
        public static bool SetLinks(CSGTree tree, NativeArray<HotspotLink> links)
        {
            if (!tree.Valid)
                return false;
            var data = ChiselTreeLookup.Value[tree];
            var count = links.IsCreated ? links.Length : 0;
            var same = data.hotspotLinks.IsCreated && data.hotspotLinks.Length == count;
            for (int i = 0; i < count && same; i++)
                same = data.hotspotLinks[i].Equals(links[i]);
            if (same)
                return false;
            data.lastJobHandle.Complete();
            data.hotspotLinks.Clear();
            if (count > 0)
                data.hotspotLinks.AddRange(links);
            return true;
        }
    }
}
