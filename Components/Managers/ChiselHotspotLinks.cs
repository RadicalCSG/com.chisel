using System;
using System.Collections.Generic;
using Chisel.Core;
using Unity.Collections;
using UnityEngine;

namespace Chisel.Components
{
    // Two surfaces connected or disconnected by hand (Chisel.Core.HotspotLink), kept by the model they are in
    [Serializable]
    public struct ChiselHotspotLink
    {
        public ChiselGeneratorComponent a;
        public int                      brushA, surfaceA;   // the brush, in the order its generator makes them, and its surface (polygon)
        public ChiselGeneratorComponent b;
        public int                      brushB, surfaceB;
        public bool                     connect;
        public int                      order;              // the newer, the higher
    }

    public enum ChiselHotspotLinkState { Automatic, Connected, Disconnected }

    // Connecting and disconnecting surfaces by hand: two touching surfaces of one model are mapped as one, even when their
    // frames differ, or never; the newest link between two surfaces counts, and it counts only while they touch
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

        // How two surfaces are linked by hand
        public static ChiselHotspotLinkState StateOf(SurfaceReference a, SurfaceReference b)
        {
            var model = ModelOf(a, b);
            if (!model || !Locate(a, out var brushA) || !Locate(b, out var brushB))
                return ChiselHotspotLinkState.Automatic;
            var state = ChiselHotspotLinkState.Automatic; var newest = int.MinValue;
            foreach (var link in model.hotspotLinks)
            {
                if (!Between(link, a.node, brushA, a.surfaceIndex, b.node, brushB, b.surfaceIndex) || link.order < newest)
                    continue;
                newest = link.order;
                state = link.connect ? ChiselHotspotLinkState.Connected : ChiselHotspotLinkState.Disconnected;
            }
            return state;
        }

        public static bool Connect(SurfaceReference a, SurfaceReference b)    { return Set(a, b, ChiselHotspotLinkState.Connected); }
        public static bool Disconnect(SurfaceReference a, SurfaceReference b) { return Set(a, b, ChiselHotspotLinkState.Disconnected); }
        // Back to what touching coplanar surfaces do by themselves
        public static bool Forget(SurfaceReference a, SurfaceReference b)     { return Set(a, b, ChiselHotspotLinkState.Automatic); }

        // The model the two surfaces are in (to record for undo first), or null when they are not in one
        public static ChiselModelComponent ModelOfSurfaces(SurfaceReference a, SurfaceReference b) { return ModelOf(a, b); }

        static bool Set(SurfaceReference a, SurfaceReference b, ChiselHotspotLinkState state)
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
            if (state != ChiselHotspotLinkState.Automatic)
                model.hotspotLinks.Add(new ChiselHotspotLink { a = a.node, brushA = brushA, surfaceA = a.surfaceIndex,
                                                               b = b.node, brushB = brushB, surfaceB = b.surfaceIndex,
                                                               connect = state == ChiselHotspotLinkState.Connected, order = order + 1 });
            a.node.SetDirty();
            b.node.SetDirty();
            return true;
        }

        // Every model's links to its tree, with its brushes as they are now
        public static void Update()
        {
            foreach (var model in ChiselModelManager.Instance.Models)
            {
                if (!model || !model.Node.Valid)
                    continue;
                var stored = model.hotspotLinks;
                using var links = new NativeList<HotspotLink>(Math.Max(1, stored.Count), Allocator.Temp);
                foreach (var link in stored)
                {
                    if (!Resolve(link.a, link.brushA, out var brushA) || !Resolve(link.b, link.brushB, out var brushB))
                        continue;
                    links.Add(new HotspotLink { brushA = CompactHierarchyManager.GetCompactNodeID(brushA), surfaceA = link.surfaceA,
                                                brushB = CompactHierarchyManager.GetCompactNodeID(brushB), surfaceB = link.surfaceB,
                                                order = link.order, connect = link.connect });
                }
                if (ChiselHotspotLinkStore.SetLinks(model.Node, links.AsArray()))
                {
                    // the surfaces of every link are mapped again
                    foreach (var link in stored)
                    {
                        if (link.a) link.a.SetDirty();
                        if (link.b) link.b.SetDirty();
                    }
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
