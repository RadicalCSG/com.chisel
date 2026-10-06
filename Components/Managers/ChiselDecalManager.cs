using System;
using System.Collections.Generic;
using Chisel.Core;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Profiling;

namespace Chisel.Components
{
    /// <summary>
    /// Synchronizes active decals with each model's CSG tree.
    /// </summary>
    public static class ChiselDecalManager
    {
        static readonly HashSet<ChiselDecalComponent> s_Decals = new();
        // Last synchronized state for each model.
        static readonly Dictionary<ChiselModelComponent, ModelState> s_Models = new();
        static readonly List<ChiselModelComponent> s_ModelList = new();
        static readonly List<ChiselModelComponent> s_Stale = new();
        static readonly Dictionary<ulong, SurfaceDestinationFlags> s_MaterialFlags = new();
        // Cached until registration or material metadata changes.
        static readonly Dictionary<ChiselDecalComponent, DecalState> s_DecalStates = new();
        static bool s_Dirty = true;

        struct DecalState
        {
            public ChiselModelComponent    parentModel;
            public ulong                   entityID;
            public ulong                   renderMaterial;
            public SurfaceDestinationFlags destinationFlags;
            // Null targets apply to every surface.
            public ChiselDecalTarget[]     targets;
        }

        sealed class ModelState
        {
            public CSGTree tree;
            public readonly List<ChiselDecalInstance> decals = new();
            public readonly List<ChiselDecalTarget>   targets = new();
            public readonly List<ChiselDecalInstance> previous = new();
            public readonly List<ChiselDecalTarget>   previousTargets = new();
        }

        public static void Register(ChiselDecalComponent decal)
        {
            if (decal != null && s_Decals.Add(decal))
                s_Dirty = true;
        }

        public static void Unregister(ChiselDecalComponent decal)
        {
            if (s_Decals.Remove(decal))
                s_Dirty = true;
            s_DecalStates.Remove(decal);
        }

        /// <summary>Makes the next update look at every decal again.</summary>
        public static void SetDirty()
        {
            s_Dirty = true;
            s_MaterialFlags.Clear();
        }

        public static int DecalCount => s_Decals.Count;

        /// <summary>
        /// Synchronizes changed decal state. Transform changes are detected on every call.
        /// </summary>
        public static void Update()
        {
            Profiler.BeginSample("ChiselDecalManager.Update");
            try
            {
                s_ModelList.Clear();
                foreach (var model in ChiselModelManager.Instance.Models)
                {
                    if (model != null && model.isActiveAndEnabled && model.Node.Valid)
                        s_ModelList.Add(model);
                }

                // Forget models that are gone
                s_Stale.Clear();
                foreach (var pair in s_Models)
                {
                    if (pair.Key == null || !s_ModelList.Contains(pair.Key))
                        s_Stale.Add(pair.Key);
                }
                foreach (var model in s_Stale)
                    s_Models.Remove(model);

                if (s_Decals.Count == 0 && s_Models.Count == 0)
                {
                    s_Dirty = false;
                    return;
                }

                foreach (var model in s_ModelList)
                {
                    if (!s_Models.TryGetValue(model, out var state))
                    {
                        state = new ModelState();
                        s_Models.Add(model, state);
                    }
                    // A rebuilt tree may reuse a handle, so also compare its decal count.
                    if (state.tree != model.Node || state.tree.GetDecalCount() != state.previous.Count)
                    {
                        state.tree = model.Node;
                        state.previous.Clear();
                        s_Dirty = true;
                    }
                    state.decals.Clear();
                    state.targets.Clear();
                }

                if (s_Dirty)
                {
                    s_DecalStates.Clear();
                    foreach (var decal in s_Decals)
                    {
                        if (decal == null || decal.Material == null)
                            continue;
                        ChiselDecalTarget[] targets = null;
                        if (decal.Targets.Count > 0)
                        {
                            var resolved = new List<ChiselDecalTarget>(decal.Targets.Count);
                            foreach (var target in decal.Targets)
                            {
                                if (target.brush == null)
                                    continue;
                                resolved.Add(new ChiselDecalTarget
                                {
                                    brushEntityID = EntityId.ToULong(target.brush.GetEntityId()),
                                    surfaceIndex  = target.surfaceIndex
                                });
                            }
                            // Limited to surfaces that are all gone: it draws nothing
                            if (resolved.Count == 0)
                                continue;
                            targets = resolved.ToArray();
                        }
                        s_DecalStates[decal] = new DecalState
                        {
                            parentModel      = decal.GetComponentInParent<ChiselModelComponent>(),
                            entityID         = EntityId.ToULong(decal.GetEntityId()),
                            renderMaterial   = EntityId.ToULong(decal.Material.GetEntityId()),
                            destinationFlags = GetDestinationFlags(decal.Material),
                            targets          = targets
                        };
                    }
                }

                foreach (var decal in s_Decals)
                {
                    if (decal == null || !decal.isActiveAndEnabled ||
                        !s_DecalStates.TryGetValue(decal, out var decalState))
                        continue;
                    if (decalState.parentModel != null)
                    {
                        if (s_Models.TryGetValue(decalState.parentModel, out var state))
                            Add(state, Describe(decal, decalState, decalState.parentModel), decalState.targets);
                        continue;
                    }
                    var scene = decal.gameObject.scene;
                    foreach (var model in s_ModelList)
                    {
                        if (model.gameObject.scene == scene)
                            Add(s_Models[model], Describe(decal, decalState, model), decalState.targets);
                    }
                }

                foreach (var model in s_ModelList)
                {
                    var state = s_Models[model];
                    if (!s_Dirty && Same(state.decals, state.previous) && Same(state.targets, state.previousTargets))
                        continue;
                    var decals  = new NativeArray<ChiselDecalInstance>(state.decals.Count, Allocator.Temp);
                    var targets = new NativeArray<ChiselDecalTarget>(state.targets.Count, Allocator.Temp);
                    try
                    {
                        for (int i = 0; i < state.decals.Count; i++)
                            decals[i] = state.decals[i];
                        for (int i = 0; i < state.targets.Count; i++)
                            targets[i] = state.targets[i];
                        state.tree.SetDecals(decals, targets);
                    }
                    finally
                    {
                        decals.Dispose();
                        targets.Dispose();
                    }
                    state.previous.Clear();
                    state.previous.AddRange(state.decals);
                    state.previousTargets.Clear();
                    state.previousTargets.AddRange(state.targets);
                }
                s_Dirty = false;
            }
            finally
            {
                // Do not retain temporary model references between updates.
                s_ModelList.Clear();
                s_Stale.Clear();
                Profiler.EndSample();
            }
        }

        static void Add(ModelState state, ChiselDecalInstance decal, ChiselDecalTarget[] targets)
        {
            if (targets != null)
            {
                decal.targetStart = state.targets.Count;
                decal.targetCount = targets.Length;
                state.targets.AddRange(targets);
            }
            state.decals.Add(decal);
        }

        static bool Same<T>(List<T> a, List<T> b) where T : struct, IEquatable<T>
        {
            if (a.Count != b.Count)
                return false;
            for (int i = 0; i < a.Count; i++)
            {
                if (!a[i].Equals(b[i]))
                    return false;
            }
            return true;
        }

        static ChiselDecalInstance Describe(ChiselDecalComponent decal, in DecalState decalState, ChiselModelComponent model)
        {
            // The tree's space is the model's own space
            var decalToTree = model.transform.worldToLocalMatrix * decal.transform.localToWorldMatrix;
            return new ChiselDecalInstance
            {
                entityID         = decalState.entityID,
                decalToTree      = (float4x4)decalToTree,
                settings         = decal.Settings,
                renderMaterial   = decalState.renderMaterial,
                destinationFlags = decalState.destinationFlags
            };
        }

        // Resolve the same destination flags used by a brush surface.
        static SurfaceDestinationFlags GetDestinationFlags(Material material)
        {
            var id = EntityId.ToULong(material.GetEntityId());
            if (!s_MaterialFlags.TryGetValue(id, out var flags))
            {
                flags = ChiselSurface.Create(material).DestinationFlags;
                s_MaterialFlags.Add(id, flags);
            }
            return flags;
        }
    }
}
