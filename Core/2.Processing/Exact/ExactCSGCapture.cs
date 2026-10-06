using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Chisel.Core
{
    static class ExactCSGCapture
    {
        // One part of one face: the triangles drawn with one category, over the face's output vertices.
        internal sealed class Part
        {
            public int           surface;    // the brush's face, which is also its plane's index in Brush.planes
            public CategoryIndex category;   // SelfAligned or SelfReverseAligned
            public ExactVertex[] vertices;   // exact, as the triangulator saw them
            public float3[]      positions;  // the same vertices rounded to float, as they were emitted
            public int[]         triangles;  // counter-clockwise seen from the face's plane
        }

        internal sealed class Brush
        {
            public ExactPlane[] planes;         // the brush's planes as the last update of its tree made them
            public int          cornerCount;    // ExactInputJob: > 0 the planes enclose volume, 0 they don't, -1 no part
            public int          planesUpdate;   // UpdateCount when the planes were captured
            public Part[]       parts;          // null until an update built the brush
            public int          partsUpdate;    // UpdateCount when the parts were captured
        }

        static int s_Users;

        internal static bool Enabled => s_Users > 0;

        // Every capture that has been made while enabled, by brush.
        internal static readonly Dictionary<NodeID, Brush> Brushes = new Dictionary<NodeID, Brush>();

        // How many tree updates have been captured; lets a caller tell what the last update did.
        internal static int UpdateCount { get; private set; }

        // Turns capturing on for as long as at least one caller holds it.
        internal static void Acquire() { s_Users++; }

        internal static void Release()
        {
            if (s_Users > 0)
                s_Users--;
            if (s_Users == 0)
            {
                Brushes.Clear();
                UpdateCount = 0;
            }
        }

        internal static void Store(NativeList<IndexOrder> allTreeBrushIndexOrders, NodeID[] nodeIDs,
                                   NativeList<BlobAssetReference<ExactBrush>> exactBrushCache,
                                   Dictionary<CompactNodeID, Part[]> parts)
        {
            UpdateCount++;
            var byCompactNodeID = new Dictionary<CompactNodeID, NodeID>(allTreeBrushIndexOrders.Length);
            for (int b = 0; b < allTreeBrushIndexOrders.Length; b++)
            {
                var indexOrder = allTreeBrushIndexOrders[b];
                var nodeID     = nodeIDs[b];
                byCompactNodeID[indexOrder.compactNodeID] = nodeID;
                if (!Brushes.TryGetValue(nodeID, out var brush))
                    Brushes[nodeID] = brush = new Brush();
                var exactBrush = exactBrushCache[indexOrder.nodeOrder];
                if (exactBrush.IsCreated)
                {
                    ref var planes = ref exactBrush.Value.planes;
                    brush.planes = new ExactPlane[planes.Length];
                    for (int p = 0; p < planes.Length; p++)
                        brush.planes[p] = planes[p];
                    brush.cornerCount = exactBrush.Value.cornerCount;
                } else
                {
                    brush.planes      = new ExactPlane[0];
                    brush.cornerCount = -1;
                }
                brush.planesUpdate = UpdateCount;
            }
            foreach (var pair in parts)
            {
                if (!byCompactNodeID.TryGetValue(pair.Key, out var nodeID))
                    continue;
                var brush = Brushes[nodeID];
                brush.parts       = pair.Value;
                brush.partsUpdate = UpdateCount;
            }
        }

        internal static Dictionary<CompactNodeID, Part[]> ReadParts(NativeStream stream)
        {
            var result = new Dictionary<CompactNodeID, Part[]>();
            var reader = stream.AsReader();
            for (int index = 0; index < reader.ForEachCount; index++)
            {
                if (reader.BeginForEachIndex(index) == 0)
                    continue;
                var indexOrder   = reader.Read<IndexOrder>();
                var surfaceCount = reader.Read<int>();
                var parts        = new List<Part>();
                for (int s = 0; s < surfaceCount; s++)
                {
                    var partCount = reader.Read<int>();
                    for (int p = 0; p < partCount; p++)
                    {
                        var part = new Part { surface = s, category = (CategoryIndex)reader.Read<int>() };
                        var vertexCount = reader.Read<int>();
                        part.vertices  = new ExactVertex[vertexCount];
                        part.positions = new float3[vertexCount];
                        for (int v = 0; v < vertexCount; v++)
                        {
                            var x = reader.Read<Int128>();
                            var y = reader.Read<Int128>();
                            var z = reader.Read<Int128>();
                            var w = reader.Read<Int128>();
                            part.vertices[v]  = ExactVertex.FromHomogeneous(x, y, z, w);
                            part.positions[v] = reader.Read<float3>();
                        }
                        var indexCount = reader.Read<int>();
                        part.triangles = new int[indexCount];
                        for (int i = 0; i < indexCount; i++)
                            part.triangles[i] = reader.Read<int>();
                        parts.Add(part);
                    }
                }
                reader.EndForEachIndex();
                result[indexOrder.compactNodeID] = parts.ToArray();
            }
            return result;
        }
    }
}
