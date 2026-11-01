using System;
using System.Collections.Generic;
using Chisel.Core;
using Unity.Mathematics;
using UnityEngine;

namespace Chisel.Components
{
    /// <summary>
    /// What contents a point of a model is in (water, a trigger, Solid, or nothing), as the CSG decides it: the model's
    /// brushes, their operations and contents, kept with its generated meshes. It needs none of the CSG at runtime, so it
    /// answers in a player too, built with the model's meshes.
    /// <para>For each contents type the model is filled where its own brushes, the Solid ones and the carving ones put
    /// it, each node's operation applied in order. Where several types are filled, Solid wins, then the earliest entry of
    /// the contents list (<see cref="ChiselContentsList"/>).</para>
    /// </summary>
#if UNITY_6000_7_OR_NEWER
    [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
#endif
    [ExecuteAlways, DisallowMultipleComponent, AddComponentMenu("")]
    public sealed class ChiselContentsVolume : MonoBehaviour
    {
        /// <summary>The contents of a point no brush fills</summary>
        public const int kNothing = -1;

        [Serializable]
        public struct Node
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

        [SerializeField] Node[]   nodes  = Array.Empty<Node>();
        [SerializeField] float4[] planes = Array.Empty<float4>();

        static readonly List<ChiselContentsVolume> s_Volumes = new();
        // whether each brush holds the point, during one query: 0 not known yet, 1 inside, 2 outside
        static byte[] s_Inside = Array.Empty<byte>();

        void OnEnable()  { s_Volumes.Add(this); }
        void OnDisable() { s_Volumes.Remove(this); }

        /// <summary>The nodes of the model's tree, in order, each followed by those below it</summary>
        public ReadOnlySpan<Node> Nodes => nodes;

        /// <summary>
        /// The contents at a point in the world, of every model that has one there: an index of the contents list
        /// (<see cref="ChiselContentsList.kSolidIndex"/> for Solid), or <see cref="kNothing"/>
        /// </summary>
        public static int ContentsAt(Vector3 worldPosition)
        {
            var best = kNothing;
            foreach (var volume in s_Volumes)
                best = Better(best, volume.ContentsAtWorld(worldPosition));
            return best;
        }

        /// <summary>The contents of this model at a point in the world</summary>
        public int ContentsAtWorld(Vector3 worldPosition)
        {
            return ContentsAtLocal(transform.InverseTransformPoint(worldPosition));
        }

        /// <summary>The contents of this model at a point in its own space</summary>
        public int ContentsAtLocal(float3 point)
        {
            if (nodes.Length == 0)
                return kNothing;
            if (s_Inside.Length < nodes.Length)
                s_Inside = new byte[nodes.Length];
            Array.Clear(s_Inside, 0, nodes.Length);

            // only the types of the brushes that hold the point can be filled there
            var candidates = 0u;
            for (int n = 0; n < nodes.Length; n++)
            {
                if (nodes[n].isBrush && !nodes[n].carving && Inside(n, point))
                    candidates |= 1u << nodes[n].contents;
            }
            if (candidates == 0)
                return kNothing;
            for (int contents = ChiselContentsList.kSolidIndex; contents < ChiselContentsList.kMaxEntries; contents++)
            {
                if ((candidates & (1u << contents)) != 0 && Filled(0, nodes.Length, contents, point))
                    return contents;
            }
            return kNothing;
        }

        // Of two contents, the one that wins: Solid, then the earliest of the list
        static int Better(int a, int b)
        {
            if (a == kNothing) return b;
            if (b == kNothing) return a;
            return math.min(a, b);
        }

        // Whether the siblings from start to end leave the point filled for a contents type, each one's operation applied
        // in order (ContentsOracle.Evaluate)
        bool Filled(int start, int end, int contents, float3 point)
        {
            var filled = false;
            for (int n = start; n < end; n += nodes[n].descendants + 1)
            {
                var node = nodes[n];
                bool inside;
                if (node.isBrush)
                {
                    if (!node.carving && node.contents != contents && node.contents != ChiselContentsList.kSolidIndex)
                        continue;
                    inside = Inside(n, point);
                } else
                    inside = Filled(n + 1, n + 1 + node.descendants, contents, point);

                switch (node.operation)
                {
                    case CSGOperationType.Additive:     filled = filled || inside;  break;
                    case CSGOperationType.Subtractive:  filled = filled && !inside; break;
                    case CSGOperationType.Intersecting: filled = filled && inside;  break;
                }
            }
            return filled;
        }

        bool Inside(int n, float3 point)
        {
            if (s_Inside[n] != 0)
                return s_Inside[n] == 1;
            var node = nodes[n];
            var inside = math.all(point >= node.min) && math.all(point <= node.max);
            for (int p = node.firstPlane; inside && p < node.firstPlane + node.planeCount; p++)
                inside = math.dot(planes[p].xyz, point) + planes[p].w <= 0;
            s_Inside[n] = (byte)(inside ? 1 : 2);
            return inside;
        }

        /// <summary>Takes in the brushes of a model's tree, in the model's space</summary>
        internal void Build(CSGTree tree)
        {
            var builtNodes  = new List<Node>();
            var builtPlanes = new List<float4>();
            var contentsCount = CompactHierarchyManager.ContentsCount;
            for (int i = 0; i < tree.Count; i++)
                Add(tree[i], float4x4.identity, false, contentsCount, builtNodes, builtPlanes);
            nodes  = builtNodes.ToArray();
            planes = builtPlanes.ToArray();
        }

        static void Add(CSGTreeNode node, float4x4 parentToTree, bool carving, int contentsCount, List<Node> builtNodes, List<float4> builtPlanes)
        {
            if (!node.Valid)
                return;
            var operation   = node.Operation;
            var nodeToTree  = math.mul(parentToTree, node.LocalTransformation);
            carving         = carving || operation == CSGOperationType.Subtractive || operation == CSGOperationType.Intersecting;
            var index       = builtNodes.Count;
            if (node.Type != CSGNodeType.Brush)
            {
                builtNodes.Add(new Node { operation = operation, carving = carving });
                for (int i = 0; i < node.Count; i++)
                    Add(node[i], nodeToTree, carving, contentsCount, builtNodes, builtPlanes);
                var branch = builtNodes[index];
                branch.descendants = builtNodes.Count - index - 1;
                builtNodes[index] = branch;
                return;
            }

            var brush = (CSGTreeBrush)node;
            var blob  = BrushMeshManager.GetBrushMeshBlob(brush.BrushMesh.BrushMeshID);
            if (!blob.IsCreated)
                return;
            ref var mesh = ref blob.Value;
            // planes go from the brush's space to the tree's by the inverse transpose
            var planeToTree = math.transpose(math.inverse(nodeToTree));
            var firstPlane  = builtPlanes.Count;
            for (int p = 0; p < mesh.localPlaneCount; p++)
            {
                var plane = math.mul(planeToTree, mesh.localPlanes[p]);
                builtPlanes.Add(plane / math.length(plane.xyz));
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
            builtNodes.Add(new Node
            {
                operation  = operation,
                isBrush    = true,
                carving    = carving,
                contents   = ContentsRules.Resolve(brush.Contents, contentsCount),
                firstPlane = firstPlane,
                planeCount = builtPlanes.Count - firstPlane,
                min        = min - kSlack,
                max        = max + kSlack
            });
        }
    }
}
