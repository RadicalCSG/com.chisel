using System;
using System.Collections.Generic;
using Chisel.Core;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace Chisel.Components
{
    // Two surfaces connected or disconnected by hand, and the border along the edge they share (Chisel.Core.HotspotLink),
    // kept by the model they are in
    [Serializable]
    public struct ChiselHotspotLink
    {
        public ChiselGeneratorComponent a;
        public int                      brushA, surfaceA;   // the brush, in the order its generator makes them, and its surface (polygon)
        public ChiselGeneratorComponent b;
        public int                      brushB, surfaceB;
        public bool                     connect;
        public bool                     automatic;          // connected or not as they would be by themselves (connect unused)
        public bool                     single;             // the border along one edge of surface A by itself (b, brushB unused)
        public int                      edge;               // single: the edge, by its place in the polygon (from its corner edge to the next)
        public ChiselHotspotBorderState border;
        public int                      order;              // the newer, the higher
    }

    public enum ChiselHotspotLinkState { Automatic, Connected, Disconnected }

    // Along the edge two surfaces share: a border as they would have by themselves, a border, or none
    public enum ChiselHotspotBorderState { Automatic, Shown, Hidden }

    // Connecting and disconnecting surfaces by hand: two touching surfaces of one model are mapped as one, even when their
    // frames differ, or never; along the edge they share a border can be shown or hidden. The newest link between two
    // surfaces counts, and it counts only while they touch.
    public static class ChiselHotspotLinks
    {
        static readonly List<CSGTreeBrush> s_Brushes = new();
        static readonly List<CSGTreeNode>  s_Nodes   = new();

        // The brushes a generator makes, in order
        static void BrushesOf(ChiselGeneratorComponent generator, List<CSGTreeBrush> brushes)
        {
            brushes.Clear();
            if (!generator || !generator.TopTreeNode.Valid)
                return;
            s_Nodes.Clear();
            s_Nodes.Add(generator.TopTreeNode);
            while (s_Nodes.Count > 0)
            {
                var node = s_Nodes[s_Nodes.Count - 1];
                s_Nodes.RemoveAt(s_Nodes.Count - 1);
                if (node.Type == CSGNodeType.Brush)
                    brushes.Add((CSGTreeBrush)node);
                else
                {
                    for (int i = node.Count - 1; i >= 0; i--)
                        s_Nodes.Add(node[i]);
                }
            }
        }

        static bool Locate(SurfaceReference surface, out int brush)
        {
            brush = -1;
            if (surface == null || !surface.node)
                return false;
            BrushesOf(surface.node, s_Brushes);
            brush = s_Brushes.IndexOf(surface.brush);
            return brush >= 0;
        }

        static ChiselModelComponent ModelOf(SurfaceReference a, SurfaceReference b)
        {
            if (a == null || b == null || !a.node || !b.node)
                return null;
            var model = a.node.hierarchyItem.Model;
            return model && model == b.node.hierarchyItem.Model ? model : null;
        }

        static bool Between(in ChiselHotspotLink link, ChiselGeneratorComponent a, int brushA, int surfaceA, ChiselGeneratorComponent b, int brushB, int surfaceB)
        {
            return (link.a == a && link.brushA == brushA && link.surfaceA == surfaceA && link.b == b && link.brushB == brushB && link.surfaceB == surfaceB) ||
                   (link.a == b && link.brushA == brushB && link.surfaceA == surfaceB && link.b == a && link.brushB == brushA && link.surfaceB == surfaceA);
        }

        static ChiselHotspotLinkState ConnectionOf(in ChiselHotspotLink link)
        {
            return link.automatic ? ChiselHotspotLinkState.Automatic : (link.connect ? ChiselHotspotLinkState.Connected : ChiselHotspotLinkState.Disconnected);
        }

        // The newest link between two surfaces, if they have one
        static bool Newest(SurfaceReference a, SurfaceReference b, out ChiselHotspotLink newest)
        {
            newest = default;
            var model = ModelOf(a, b);
            if (!model || !Locate(a, out var brushA) || !Locate(b, out var brushB))
                return false;
            var found = false;
            foreach (var link in model.hotspotLinks)
            {
                if (!Between(link, a.node, brushA, a.surfaceIndex, b.node, brushB, b.surfaceIndex) || (found && link.order < newest.order))
                    continue;
                newest = link;
                found = true;
            }
            return found;
        }

        // How two surfaces are linked by hand
        public static ChiselHotspotLinkState StateOf(SurfaceReference a, SurfaceReference b)
        {
            return Newest(a, b, out var link) ? ConnectionOf(link) : ChiselHotspotLinkState.Automatic;
        }

        // The border along the edge two surfaces share, as given by hand
        public static ChiselHotspotBorderState BorderOf(SurfaceReference a, SurfaceReference b)
        {
            return Newest(a, b, out var link) ? link.border : ChiselHotspotBorderState.Automatic;
        }

        // The border along one edge of a surface by itself (edge: its place in the brush polygon), as given by hand
        public static ChiselHotspotBorderState EdgeBorderOf(SurfaceReference surface, int edge)
        {
            if (surface == null || !surface.node || !Locate(surface, out var brush))
                return ChiselHotspotBorderState.Automatic;
            var model = surface.node.hierarchyItem.Model;
            if (!model)
                return ChiselHotspotBorderState.Automatic;
            foreach (var link in model.hotspotLinks)
            {
                if (link.single && link.a == surface.node && link.brushA == brush && link.surfaceA == surface.surfaceIndex && link.edge == edge)
                    return link.border;
            }
            return ChiselHotspotBorderState.Automatic;
        }

        // A border along one edge of a surface by itself (Shown), none (Hidden), or as it would have by itself
        public static bool SetEdgeBorder(SurfaceReference surface, int edge, ChiselHotspotBorderState border)
        {
            if (surface == null || !surface.node || !Locate(surface, out var brush) || edge < 0)
                return false;
            var model = surface.node.hierarchyItem.Model;
            if (!model)
                return false;
            var order = 0;
            for (int i = model.hotspotLinks.Count - 1; i >= 0; i--)
            {
                var link = model.hotspotLinks[i];
                order = Math.Max(order, link.order);
                if (link.single && link.a == surface.node && link.brushA == brush && link.surfaceA == surface.surfaceIndex && link.edge == edge)
                    model.hotspotLinks.RemoveAt(i);
            }
            if (border != ChiselHotspotBorderState.Automatic)
                model.hotspotLinks.Add(new ChiselHotspotLink { a = surface.node, brushA = brush, surfaceA = surface.surfaceIndex,
                                                               b = surface.node, brushB = brush, surfaceB = -1,
                                                               single = true, edge = edge, automatic = true, border = border, order = order + 1 });
            surface.node.SetDirty();
            return true;
        }

        // An edge of a brush polygon in tree space: from its corner to the next
        public static bool EdgeOf(CSGTreeBrush brush, int polygon, int edge, out float3 from, out float3 to)
        {
            from = to = default;
            if (!brush.Valid)
                return false;
            var blob = BrushMeshManager.GetBrushMeshBlob(brush.BrushMesh);
            if (!blob.IsCreated)
                return false;
            ref var mesh = ref blob.Value;
            if (polygon < 0 || polygon >= mesh.polygons.Length || edge < 0 || edge >= mesh.polygons[polygon].edgeCount)
                return false;
            var first = mesh.polygons[polygon].firstEdge; var count = mesh.polygons[polygon].edgeCount;
            var toTree = brush.NodeToTreeSpaceMatrix;
            from = math.mul(toTree, new float4(mesh.localVertices[mesh.halfEdges[first + edge].vertexIndex], 1)).xyz;
            to   = math.mul(toTree, new float4(mesh.localVertices[mesh.halfEdges[first + (edge + 1) % count].vertexIndex], 1)).xyz;
            return true;
        }

        public static bool Connect(SurfaceReference a, SurfaceReference b)    { return SetConnection(a, b, ChiselHotspotLinkState.Connected); }
        public static bool Disconnect(SurfaceReference a, SurfaceReference b) { return SetConnection(a, b, ChiselHotspotLinkState.Disconnected); }
        // Connected, apart, or as they would be by themselves; the border along their edge as it was given
        public static bool SetConnection(SurfaceReference a, SurfaceReference b, ChiselHotspotLinkState state) { return Set(a, b, state, BorderOf(a, b)); }
        // Back to what touching coplanar surfaces do by themselves (the border along their edge too)
        public static bool Forget(SurfaceReference a, SurfaceReference b)     { return Set(a, b, ChiselHotspotLinkState.Automatic, ChiselHotspotBorderState.Automatic); }

        // A border along the edge two surfaces share (Shown), none (Hidden), or as they would have by themselves
        public static bool SetBorder(SurfaceReference a, SurfaceReference b, ChiselHotspotBorderState border) { return Set(a, b, StateOf(a, b), border); }

        // Every link of a surface removed (it takes another hotspot material); true when it had any
        public static bool ForgetAllOf(SurfaceReference surface)
        {
            if (surface == null || !surface.node || !Locate(surface, out var brush))
                return false;
            var model = surface.node.hierarchyItem.Model;
            if (!model)
                return false;
            var removed = model.hotspotLinks.RemoveAll(link => (link.a == surface.node && link.brushA == brush && link.surfaceA == surface.surfaceIndex) ||
                                                               (link.b == surface.node && link.brushB == brush && link.surfaceB == surface.surfaceIndex));
            if (removed > 0)
                surface.node.SetDirty();
            return removed > 0;
        }

        // The links a model keeps
        public static IReadOnlyList<ChiselHotspotLink> LinksOf(ChiselModelComponent model)
        {
            return model ? model.hotspotLinks : Array.Empty<ChiselHotspotLink>();
        }

        // A link's brushes as they are now; false when either is gone
        public static bool Resolve(in ChiselHotspotLink link, out CSGTreeBrush brushA, out CSGTreeBrush brushB)
        {
            brushB = default;
            return Resolve(link.a, link.brushA, out brushA) && Resolve(link.b, link.brushB, out brushB);
        }

        // The model the two surfaces are in (to record for undo first), or null when they are not in one
        public static ChiselModelComponent ModelOfSurfaces(SurfaceReference a, SurfaceReference b) { return ModelOf(a, b); }

        static bool Set(SurfaceReference a, SurfaceReference b, ChiselHotspotLinkState state, ChiselHotspotBorderState border)
        {
            var model = ModelOf(a, b);
            if (!model || !Locate(a, out var brushA) || !Locate(b, out var brushB) ||
                (a.node == b.node && brushA == brushB && a.surfaceIndex == b.surfaceIndex))
                return false;
            var order = 0;
            for (int i = model.hotspotLinks.Count - 1; i >= 0; i--)
            {
                var link = model.hotspotLinks[i];
                order = Math.Max(order, link.order);
                if (Between(link, a.node, brushA, a.surfaceIndex, b.node, brushB, b.surfaceIndex))
                    model.hotspotLinks.RemoveAt(i);
            }
            if (state != ChiselHotspotLinkState.Automatic || border != ChiselHotspotBorderState.Automatic)
                model.hotspotLinks.Add(new ChiselHotspotLink { a = a.node, brushA = brushA, surfaceA = a.surfaceIndex,
                                                               b = b.node, brushB = brushB, surfaceB = b.surfaceIndex,
                                                               connect = state == ChiselHotspotLinkState.Connected,
                                                               automatic = state == ChiselHotspotLinkState.Automatic,
                                                               border = border, order = order + 1 });
            a.node.SetDirty();
            b.node.SetDirty();
            return true;
        }

        // The links each model's surfaces were last mapped with
        static readonly Dictionary<ChiselModelComponent, List<ChiselHotspotLink>> s_Mapped = new();
        static readonly List<ChiselModelComponent> s_Destroyed = new();

        // Every model's links to its tree, with its brushes as they are now
        public static void Update()
        {
            if (s_Mapped.Count > 0)
            {
                s_Destroyed.Clear();
                foreach (var key in s_Mapped.Keys)
                {
                    if (!key)
                        s_Destroyed.Add(key);
                }
                foreach (var key in s_Destroyed)
                    s_Mapped.Remove(key);
            }
            foreach (var model in ChiselModelManager.Instance.Models)
            {
                if (!model || !model.Node.Valid)
                    continue;
                var stored = model.hotspotLinks;
                using var links = new NativeList<HotspotLink>(Math.Max(1, stored.Count), Allocator.Temp);
                foreach (var link in stored)
                {
                    if (link.single)
                    {
                        if (!Resolve(link.a, link.brushA, out var brush) || !EdgeOf(brush, link.surfaceA, link.edge, out var from, out var to))
                            continue;
                        var id = CompactHierarchyManager.GetCompactNodeID(brush);
                        links.Add(new HotspotLink { brushA = id, surfaceA = link.surfaceA, brushB = id, surfaceB = -1, edgeFrom = from, edgeTo = to,
                                                    order = link.order,
                                                    border = link.border == ChiselHotspotBorderState.Shown ? 1 : (link.border == ChiselHotspotBorderState.Hidden ? -1 : 0) });
                        continue;
                    }
                    if (!Resolve(link.a, link.brushA, out var brushA) || !Resolve(link.b, link.brushB, out var brushB))
                        continue;
                    links.Add(new HotspotLink { brushA = CompactHierarchyManager.GetCompactNodeID(brushA), surfaceA = link.surfaceA,
                                                brushB = CompactHierarchyManager.GetCompactNodeID(brushB), surfaceB = link.surfaceB,
                                                order = link.order,
                                                connection = link.automatic ? 0 : (link.connect ? 1 : -1),
                                                border = link.border == ChiselHotspotBorderState.Shown ? 1 : (link.border == ChiselHotspotBorderState.Hidden ? -1 : 0) });
                }
                if (ChiselHotspotLinkStore.SetLinks(model.Node, links.AsArray()))
                {
                    // the surfaces of every link are mapped again, those of a link that is gone too (an undo removes links)
                    foreach (var link in stored)
                    {
                        if (link.a) link.a.SetDirty();
                        if (link.b) link.b.SetDirty();
                    }
                    if (s_Mapped.TryGetValue(model, out var previous))
                    {
                        foreach (var link in previous)
                        {
                            if (link.a) link.a.SetDirty();
                            if (link.b) link.b.SetDirty();
                        }
                    }
                    s_Mapped[model] = new List<ChiselHotspotLink>(stored);
                }
            }
        }

        static bool Resolve(ChiselGeneratorComponent generator, int index, out CSGTreeBrush brush)
        {
            brush = default;
            if (!generator)
                return false;
            BrushesOf(generator, s_Brushes);
            if (index < 0 || index >= s_Brushes.Count)
                return false;
            brush = s_Brushes[index];
            return brush.Valid;
        }
    }
}
