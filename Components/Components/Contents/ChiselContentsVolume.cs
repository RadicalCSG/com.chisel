using System;
using System.Collections.Generic;
using Chisel.Core;
using Unity.Collections;
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

        [SerializeField] ContentsVolumeNode[] nodes  = Array.Empty<ContentsVolumeNode>();
        [SerializeField] float4[]             planes = Array.Empty<float4>();

        static readonly List<ChiselContentsVolume> s_Volumes = new();
        // whether each brush holds the point, during one query: 0 not known yet, 1 inside, 2 outside
        static byte[] s_Inside = Array.Empty<byte>();

        void OnEnable()  { s_Volumes.Add(this); }
        void OnDisable() { s_Volumes.Remove(this); }

        /// <summary>The nodes of the model's tree, in order, each followed by those below it</summary>
        public ReadOnlySpan<ContentsVolumeNode> Nodes => nodes;

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
            var builtNodes  = new NativeList<ContentsVolumeNode>(math.max(1, nodes.Length), Allocator.TempJob);
            var builtPlanes = new NativeList<float4>(math.max(1, planes.Length), Allocator.TempJob);
            try
            {
                ContentsVolume.Build(tree, builtNodes, builtPlanes);
                Take(builtNodes.AsArray(), builtPlanes.AsArray());
            }
            finally
            {
                builtNodes.Dispose();
                builtPlanes.Dispose();
            }
        }

        /// <summary>Takes in a volume built already, as the model's update hands it over (ChiselMeshUpdates)</summary>
        internal void Take(NativeArray<ContentsVolumeNode> builtNodes, NativeArray<float4> builtPlanes)
        {
            // the arrays are kept when their size stays, as it does while brushes are moved
            if (nodes.Length != builtNodes.Length)
                nodes = new ContentsVolumeNode[builtNodes.Length];
            if (planes.Length != builtPlanes.Length)
                planes = new float4[builtPlanes.Length];
            builtNodes.CopyTo(nodes);
            builtPlanes.CopyTo(planes);
        }
    }
}
