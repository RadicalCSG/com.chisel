using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace Chisel.Core
{
    /// <summary>
    /// A node of a model's contents volume: the model's tree, in order, each node followed by those below it
    /// </summary>
    [Serializable]
    public struct ContentsVolumeNode
    {
        public CSGOperationType operation;
        public bool   isBrush;
        // the brush, or any brush above it, is subtractive or intersecting
        public bool   carving;
        public int    contents;
        // how many nodes below it follow it: the next sibling is that many further on
        public int    descendants;
        public int    firstPlane, planeCount;
        public float3 min, max;
    }

    /// <summary>
    /// Builds a model's contents volume (its brushes, their operations and contents, in the model's space) from the tree's
    /// hierarchy and brush meshes
    /// </summary>
    public static class ContentsVolume
    {
        /// <summary>The nodes and planes of the volume of a tree; both lists are cleared first</summary>
        public static void Build(CSGTree tree, NativeList<ContentsVolumeNode> nodes, NativeList<float4> planes)
        {
            nodes.Clear();
            planes.Clear();
            if (!tree.Valid)
                return;
            var treeID = tree.CompactNodeID;
            new BuildContentsVolumeJob
            {
                hierarchy     = CompactHierarchyManager.GetReadOnlyHierarchy(treeID),
                tree          = treeID,
                brushMeshes   = ChiselMeshLookup.Value.brushMeshBlobCache,
                contentsCount = CompactHierarchyManager.ContentsCount,
                nodes         = nodes,
                planes        = planes
            }.Run();
        }
    }

    [BurstCompile(CompileSynchronously = true)]
    struct BuildContentsVolumeJob : IJob
    {
        // Read
        [ReadOnly] public CompactHierarchy.ReadOnly                                  hierarchy;
        public CompactNodeID                                                         tree;
        [ReadOnly] public NativeParallelHashMap<int, RefCountedBrushMeshBlob>        brushMeshes;
        public int                                                                   contentsCount;

        // Write
        public NativeList<ContentsVolumeNode>                                        nodes;
        public NativeList<float4>                                                    planes;

        public void Execute()
        {
            int count = hierarchy.ChildCount(tree);
            for (int i = 0; i < count; i++)
                Add(hierarchy.GetChildCompactNodeIDAt(tree, i), float4x4.identity, false);
        }

        void Add(CompactNodeID node, float4x4 parentToTree, bool carving)
        {
            if (!hierarchy.IsValidCompactNodeID(node))
                return;
            var operation  = hierarchy.GetOperation(node);
            var nodeToTree = math.mul(parentToTree, hierarchy.GetChildTransformation(node));
            carving        = carving || operation == CSGOperationType.Subtractive || operation == CSGOperationType.Intersecting;
            int index      = nodes.Length;
            if (hierarchy.GetTypeOfNode(node) != CSGNodeType.Brush)
            {
                nodes.Add(new ContentsVolumeNode { operation = operation, carving = carving });
                int count = hierarchy.ChildCount(node);
                for (int i = 0; i < count; i++)
                    Add(hierarchy.GetChildCompactNodeIDAt(node, i), nodeToTree, carving);
                var branch = nodes[index];
                branch.descendants = nodes.Length - index - 1;
                nodes[index] = branch;
                return;
            }

            if (!brushMeshes.TryGetValue(hierarchy.GetBrushMeshID(node), out var refCounted) || !refCounted.brushMeshBlob.IsCreated)
                return;
            ref var mesh = ref refCounted.brushMeshBlob.Value;
            // planes go from the brush's space to the tree's by the inverse transpose
            var planeToTree = math.transpose(math.inverse(nodeToTree));
            int firstPlane  = planes.Length;
            for (int p = 0; p < mesh.localPlaneCount; p++)
            {
                var plane = math.mul(planeToTree, mesh.localPlanes[p]);
                planes.Add(plane / math.length(plane.xyz));
            }
            var min = new float3(float.PositiveInfinity);
            var max = new float3(float.NegativeInfinity);
            for (int v = 0; v < mesh.localVertices.Length; v++)
            {
                var vertex = math.transform(nodeToTree, mesh.localVertices[v]);
                min = math.min(min, vertex);
                max = math.max(max, vertex);
            }
            const float kSlack = 1e-4f;
            nodes.Add(new ContentsVolumeNode
            {
                operation  = operation,
                isBrush    = true,
                carving    = carving,
                contents   = ContentsRules.Resolve(hierarchy.GetContents(node), contentsCount),
                firstPlane = firstPlane,
                planeCount = planes.Length - firstPlane,
                min        = min - kSlack,
                max        = max + kSlack
            });
        }
    }
}
