using System.Collections.Generic;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEngine;

namespace Chisel.Core
{
    /// <summary>
    /// Asked by <see cref="CompactHierarchyManager.Flush(FinishMeshUpdate, CanSkipTreeUpdate)"/> about every tree that
    /// needs an update: true when the output the tree already has is still what its input builds, for instance meshes
    /// that were saved with a scene, so the tree doesn't need to be built.
    /// </summary>
    public delegate bool CanSkipTreeUpdate(CSGTree tree);

    /// <summary>
    /// What a surface parameter (the EntityId of a Material or a PhysicsMaterial) stands for, as a value that is the
    /// same in every session, such as the asset's GUID. EntityIds only last as long as the session.
    /// </summary>
    public delegate Hash128 SurfaceParameterIdentity(ulong surfaceParameter);

    static partial class CompactHierarchyManager
    {
        // Bump when GetTreeInputHash hashes something else, or the same things differently
        const int kTreeInputHashVersion = 2;

        // The trees whose update was skipped, see Flush(FinishMeshUpdate, CanSkipTreeUpdate)
        static readonly HashSet<NodeID> s_SkippedTrees = new();

        /// <summary>
        /// True when the tree's update was skipped (see <see cref="Flush(FinishMeshUpdate, CanSkipTreeUpdate)"/>) and it
        /// hasn't changed since. None of its CSG has been built then, so there is nothing to query in it yet.
        /// </summary>
        public static bool IsTreeUpdateSkipped(CSGTree tree) { return s_SkippedTrees.Contains(tree.NodeID); }

        /// <summary>True when any tree's update was skipped, see <see cref="IsTreeUpdateSkipped"/>.</summary>
        public static bool HasSkippedTreeUpdates { get { return s_SkippedTrees.Count > 0; } }

        /// <summary>
        /// Marks every tree whose update was skipped as changed, so the next <see cref="Flush(FinishMeshUpdate)"/> builds
        /// all of them. For whatever needs what the CSG builds besides the meshes, such as scene queries.
        /// </summary>
        public static void UpdateSkippedTrees()
        {
            // A tree leaves the set when it's built, or destroyed (ForgetSkippedTree)
            foreach (var nodeID in s_SkippedTrees)
                CSGTree.Find(nodeID).SetDirty();
        }

        // For a tree that's destroyed before it's built
        internal static void ForgetSkippedTree(NodeID treeNodeID)
        {
            s_SkippedTrees.Remove(treeNodeID);
        }

        // The tree is marked up to date, but its brushes stay dirty: whatever changes it next makes the next update
        // build all of it, which an incremental update needs before it can build a part
        static void SkipTreeUpdate(CSGTree tree, CompactNodeID treeCompactNodeID)
        {
            ref var compactHierarchy = ref GetHierarchy(treeCompactNodeID);
            compactHierarchy.ClearStatusFlag(treeCompactNodeID, NodeStatusFlags.TreeNeedsUpdate | NodeStatusFlags.TreeMeshNeedsUpdate);
            s_SkippedTrees.Add(tree.NodeID);
        }

        /// <summary>
        /// A hash of everything the CSG builds the tree's output from, the same in every session for the same input:
        /// the operation, contents and transformation of every node and the mesh and surfaces of every brush, in
        /// hierarchy order; the tree's decals; the model settings stored for it; and the number of contents types.
        /// Surface parameters are hashed as <paramref name="surfaceParameterIdentity"/> identifies them, and decals and
        /// their targets without their EntityIds, since EntityIds change from session to session. The tree's own
        /// transformation is left out: moving a model moves its output without changing it.
        /// </summary>
        public static unsafe Hash128 GetTreeInputHash(CSGTree tree, SurfaceParameterIdentity surfaceParameterIdentity)
        {
            if (!tree.Valid || surfaceParameterIdentity == null)
                return default;

            var treeCompactNodeID = GetCompactNodeID(tree);
            ref var hierarchy = ref GetHierarchy(treeCompactNodeID);
            if (!hierarchy.IsValidCompactNodeID(treeCompactNodeID))
                return default;

            var brushMeshBlobs = ChiselMeshLookup.Value.brushMeshBlobCache;
            var identities     = new Dictionary<ulong, Hash128>();
            var brushOrder     = new Dictionary<ulong, int>();
            var buffer         = new UnsafeAppendBuffer(16 * 1024, 16, Allocator.Temp);
            var stack          = new NativeList<CompactNodeID>(64, Allocator.Temp);
            try
            {
                buffer.Add(kTreeInputHashVersion);
                buffer.Add(ContentsCount);

                if (ModelSettingsStore.TryGet(hierarchy.GetChildRef(treeCompactNodeID).entityID, out var settings))
                {
                    buffer.Add(1);
                    buffer.Add(settings.SubtractiveWorkflow ? 1 : 0);
                    buffer.Add(settings.NormalSmoothing ? 1 : 0);
                    buffer.Add(settings.NormalSmoothingAngle);
                    buffer.Add(settings.LightmapTexelsPerUnit);
                    buffer.Add(settings.LightmapPaddingTexels);
                } else
                    buffer.Add(0);

                // Depth first, and every node's children in order
                buffer.Add(hierarchy.ChildCount(treeCompactNodeID));
                for (int i = hierarchy.ChildCount(treeCompactNodeID) - 1; i >= 0; i--)
                    stack.Add(hierarchy.GetChildCompactNodeIDAtInternal(treeCompactNodeID, i));
                while (stack.Length > 0)
                {
                    var compactNodeID = stack[stack.Length - 1];
                    stack.RemoveAt(stack.Length - 1);

                    var nodeType = hierarchy.GetTypeOfNode(compactNodeID);
                    var node     = hierarchy.GetChild(compactNodeID);
                    buffer.Add((int)nodeType);
                    buffer.Add((int)node.operation);
                    buffer.Add(node.contents);
                    buffer.Add(node.transformation);
                    if (nodeType == CSGNodeType.Brush)
                    {
                        brushOrder.TryAdd(node.entityID, brushOrder.Count);
                        AppendBrushMesh(ref buffer, brushMeshBlobs, node.brushMeshHash, identities, surfaceParameterIdentity);
                        continue;
                    }

                    var childCount = hierarchy.ChildCount(compactNodeID);
                    buffer.Add(childCount);
                    for (int i = childCount - 1; i >= 0; i--)
                        stack.Add(hierarchy.GetChildCompactNodeIDAtInternal(compactNodeID, i));
                }

                AppendDecals(ref buffer, tree, brushOrder, identities, surfaceParameterIdentity);

                var hash = xxHash3.Hash128(buffer.Ptr, buffer.Length);
                return new Hash128(hash.x, hash.y, hash.z, hash.w);
            }
            finally
            {
                stack.Dispose();
                buffer.Dispose();
            }
        }

        static Hash128 GetIdentity(Dictionary<ulong, Hash128> identities, SurfaceParameterIdentity surfaceParameterIdentity, ulong surfaceParameter)
        {
            if (surfaceParameter == 0)
                return default;
            if (!identities.TryGetValue(surfaceParameter, out var identity))
            {
                identity = surfaceParameterIdentity(surfaceParameter);
                identities.Add(surfaceParameter, identity);
            }
            return identity;
        }

        // Field by field where a struct could have padding, whose bytes are whatever the memory held
        static unsafe void AppendBrushMesh(ref UnsafeAppendBuffer buffer, NativeParallelHashMap<int, RefCountedBrushMeshBlob> brushMeshBlobs, int brushMeshHash,
                                           Dictionary<ulong, Hash128> identities, SurfaceParameterIdentity surfaceParameterIdentity)
        {
            if (!brushMeshBlobs.IsCreated ||
                !brushMeshBlobs.TryGetValue(brushMeshHash, out var item) ||
                !item.brushMeshBlob.IsCreated)
            {
                buffer.Add(-1);
                return;
            }

            ref var brushMesh = ref item.brushMeshBlob.Value;
            AppendArray(ref buffer, brushMesh.localVertices.GetUnsafePtr(), brushMesh.localVertices.Length, UnsafeUtility.SizeOf<float3>());
            AppendArray(ref buffer, brushMesh.halfEdges.GetUnsafePtr(), brushMesh.halfEdges.Length, UnsafeUtility.SizeOf<BrushMeshBlob.HalfEdge>());
            AppendArray(ref buffer, brushMesh.halfEdgePolygonIndices.GetUnsafePtr(), brushMesh.halfEdgePolygonIndices.Length, sizeof(int));
            AppendArray(ref buffer, brushMesh.localPlanes.GetUnsafePtr(), brushMesh.localPlanes.Length, UnsafeUtility.SizeOf<float4>());
            buffer.Add(brushMesh.localPlaneCount);

            ref var polygons = ref brushMesh.polygons;
            buffer.Add(polygons.Length);
            for (int p = 0; p < polygons.Length; p++)
            {
                ref var polygon = ref polygons[p];
                buffer.Add(polygon.firstEdge);
                buffer.Add(polygon.edgeCount);
                buffer.Add(polygon.descriptionIndex);

                ref var surface = ref polygon.surface;
                buffer.Add(surface.details.smoothingGroup.value);
                buffer.Add((int)surface.details.detailFlags);
                buffer.Add(surface.details.UV0.U);
                buffer.Add(surface.details.UV0.V);
                buffer.Add((int)surface.destinationFlags);
                buffer.Add((int)surface.outputFlags);
                buffer.Add(GetIdentity(identities, surfaceParameterIdentity, surface.parameters.parameter1));
                buffer.Add(GetIdentity(identities, surfaceParameterIdentity, surface.parameters.parameter2));
            }
        }

        static unsafe void AppendArray(ref UnsafeAppendBuffer buffer, void* pointer, int length, int elementSize)
        {
            buffer.Add(length);
            if (length > 0)
                buffer.Add(pointer, length * elementSize);
        }

        struct HashOrder : IComparer<uint4>
        {
            public int Compare(uint4 a, uint4 b)
            {
                if (a.x != b.x) return a.x.CompareTo(b.x);
                if (a.y != b.y) return a.y.CompareTo(b.y);
                if (a.z != b.z) return a.z.CompareTo(b.z);
                return a.w.CompareTo(b.w);
            }
        }

        static unsafe void AppendDecals(ref UnsafeAppendBuffer buffer, CSGTree tree, Dictionary<ulong, int> brushOrder,
                                        Dictionary<ulong, Hash128> identities, SurfaceParameterIdentity surfaceParameterIdentity)
        {
            if (!ChiselTreeLookup.Value.HasTree(tree))
            {
                buffer.Add(0);
                return;
            }

            var data    = ChiselTreeLookup.Value[tree];
            var decals  = data.decalInstances;
            var targets = data.decalTargets;
            if (!decals.IsCreated || decals.Length == 0)
            {
                buffer.Add(0);
                return;
            }

            var decalHashes = new NativeArray<uint4>(decals.Length, Allocator.Temp);
            var decalBuffer = new UnsafeAppendBuffer(256, 16, Allocator.Temp);
            try
            {
                for (int d = 0; d < decals.Length; d++)
                {
                    var decal    = decals[d];
                    var settings = decal.settings;
                    decalBuffer.Reset();
                    decalBuffer.Add(decal.decalToTree);
                    decalBuffer.Add(settings.center);
                    decalBuffer.Add(settings.size);
                    decalBuffer.Add((int)settings.projection);
                    decalBuffer.Add(settings.fieldOfView);
                    decalBuffer.Add(settings.maxAngle);
                    decalBuffer.Add(settings.transparent ? 1 : 0);
                    decalBuffer.Add(settings.order);
                    decalBuffer.Add(settings.surfaceOffset);
                    decalBuffer.Add(settings.uvScale);
                    decalBuffer.Add(settings.uvOffset);
                    decalBuffer.Add(GetIdentity(identities, surfaceParameterIdentity, decal.renderMaterial));
                    decalBuffer.Add((int)decal.destinationFlags);
                    decalBuffer.Add(decal.targetCount);
                    for (int t = 0; t < decal.targetCount; t++)
                    {
                        var target = targets[decal.targetStart + t];
                        decalBuffer.Add(brushOrder.TryGetValue(target.brushEntityID, out var order) ? order : -1);
                        decalBuffer.Add(target.surfaceIndex);
                    }
                    decalHashes[d] = xxHash3.Hash128(decalBuffer.Ptr, decalBuffer.Length);
                }
                decalHashes.Sort(new HashOrder());
                AppendArray(ref buffer, decalHashes.GetUnsafeReadOnlyPtr(), decalHashes.Length, UnsafeUtility.SizeOf<uint4>());
            }
            finally
            {
                decalBuffer.Dispose();
                decalHashes.Dispose();
            }
        }
    }
}
