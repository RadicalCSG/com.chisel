using Unity.Collections;
using Unity.Mathematics;

namespace Chisel.Core
{
    // What two surfaces were given by hand: connected, they are mapped as one even when their frames differ; apart, never,
    // however they lie. Along the edge they share a border can be put (where they are mapped as one) or left out (where
    // each has its own). The newest link between two surfaces counts, and a link counts only while its surfaces touch.
    // A link of one surface by itself (surfaceB -1) puts or leaves out the border along one of its edges, from edgeFrom
    // to edgeTo (tree space): every part of its outline that lies along that edge.
    public struct HotspotLink : System.IEquatable<HotspotLink>
    {
        public CompactNodeID brushA, brushB;
        public int           surfaceA, surfaceB;    // the surface's index in its brush (its polygon)
        public int           order;                 // the newer, the higher
        public int           connection;            // 1: connected, -1: apart, 0: as they would be by themselves
        public int           border;                // along the edge they share: 1 a border, -1 none, 0 as it would be
        public float3        edgeFrom, edgeTo;      // surfaceB -1: the edge of surface A

        public readonly bool OfOneEdge => surfaceB < 0;

        public readonly bool Equals(HotspotLink other) =>
            brushA == other.brushA && brushB == other.brushB && surfaceA == other.surfaceA && surfaceB == other.surfaceB &&
            order == other.order && connection == other.connection && border == other.border &&
            edgeFrom.Equals(other.edgeFrom) && edgeTo.Equals(other.edgeTo);
    }

    // A corner of the edge two surfaces share, where a link puts or leaves out the border (HotspotLink.border): an edge of
    // a surface's outline both of whose ends a rule has follows that rule, the newest rule first. Of a link of one edge
    // (segment), the rule is the edge from position to end: an edge of the outline along it follows it.
    public struct HotspotBorderCorner
    {
        public float3 position;
        public float3 end;
        public bool   segment;
        public int    rule;     // the link's order
        public int    border;   // 1: a border along it, -1: none
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
