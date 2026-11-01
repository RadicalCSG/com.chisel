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
#if UNITY_6000_7_OR_NEWER
    [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
#endif
    public static class ChiselDecalManager
    {
        static readonly HashSet<ChiselDecalComponent> s_Decals = new();
        // the decals in hierarchy order: how decals stack (SetDecals), the same every time a scene is loaded
        static readonly List<ChiselDecalComponent> s_Ordered = new();
        static bool s_OrderDirty = true;
        // Last synchronized state for each model.
        static readonly Dictionary<ChiselModelComponent, ModelState> s_Models = new();
        static readonly List<ChiselModelComponent> s_ModelList = new();
        static readonly HashSet<ChiselModelComponent> s_ModelSet = new();
        static readonly List<ChiselModelComponent> s_Stale = new();
        static readonly Dictionary<ulong, SurfaceDestinationFlags> s_MaterialFlags = new();
        // Cached until registration or material metadata changes.
        static readonly Dictionary<ChiselDecalComponent, DecalState> s_DecalStates = new();
        static bool s_Dirty = true;

        sealed class DecalState
        {
            public ChiselModelComponent    parentModel;
            public Transform               transform;
            // transform.localToWorldMatrix at the last update
            public Matrix4x4               localToWorld;
            public ulong                   entityID;
            public ulong                   renderMaterial;
            public SurfaceDestinationFlags destinationFlags;
            // Null targets apply to every surface.
            public ChiselDecalTarget[]     targets;
            // a trim decal (ChiselTrimDecals): its halves, worked out from its surfaces in every update
            public ChiselDecalComponent    trimDecal;
            public bool                    trimValid;
            public ChiselModelComponent    trimModel;
            public readonly List<ChiselDecalInstance> trimPieces = new();
            public readonly List<ChiselDecalTarget[]> trimTargets = new();
        }

        sealed class ModelState
        {
            public CSGTree tree;
            // this update's model.transform.worldToLocalMatrix
            public Matrix4x4 worldToLocal;
            public readonly List<ChiselDecalInstance> decals = new();
            public readonly List<ChiselDecalTarget>   targets = new();
            public readonly List<ChiselDecalInstance> previous = new();
            public readonly List<ChiselDecalTarget>   previousTargets = new();
        }

        // a decal's image can be a tile of its material's hotspot layout
        static ChiselDecalManager() { ChiselHotspotLayout.AnyChanged += _ => SetDirty(); }

        // Entering play mode without a domain reload keeps all of this: what was destroyed goes, the decals that are still
        // there stay registered (without a scene reload nothing registers them again), and everything is worked out again
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetState()
        {
            s_Decals.RemoveWhere(decal => decal == null);
            s_Ordered.Clear();
            s_DecalStates.Clear();
            s_MaterialFlags.Clear();
            s_Stale.Clear();
            foreach (var model in s_Models.Keys)
            {
                if (model == null)
                    s_Stale.Add(model);
            }
            foreach (var model in s_Stale)
                s_Models.Remove(model);
            s_Stale.Clear();
            s_Dirty = s_OrderDirty = true;
        }

        public static void Register(ChiselDecalComponent decal)
        {
            if (decal != null && s_Decals.Add(decal))
                s_Dirty = s_OrderDirty = true;
        }

        public static void Unregister(ChiselDecalComponent decal)
        {
            if (s_Decals.Remove(decal))
                s_Dirty = s_OrderDirty = true;
            // nothing keeps a removed decal (or its material) loaded
            s_Ordered.Remove(decal);
            s_DecalStates.Remove(decal);
        }

        /// <summary>The hierarchy changed: the decals may be in another order.</summary>
        public static void HierarchyChanged()
        {
            s_OrderDirty = true;
        }

        // Scene by scene, then by the sibling indices from the scene's root down
        static void Order()
        {
            s_Ordered.Clear();
            var keys = new List<(string scene, int[] path, ChiselDecalComponent decal)>(s_Decals.Count);
            var path = new List<int>(16);
            foreach (var decal in s_Decals)
            {
                if (decal == null)
                    continue;
                path.Clear();
                for (var transform = decal.transform; transform != null; transform = transform.parent)
                    path.Add(transform.GetSiblingIndex());
                path.Reverse();
                keys.Add((decal.gameObject.scene.path, path.ToArray(), decal));
            }
            keys.Sort((a, b) =>
            {
                var scene = string.CompareOrdinal(a.scene, b.scene);
                if (scene != 0)
                    return scene;
                for (int i = 0; i < a.path.Length && i < b.path.Length; i++)
                {
                    if (a.path[i] != b.path[i])
                        return a.path[i].CompareTo(b.path[i]);
                }
                return a.path.Length.CompareTo(b.path.Length);
            });
            foreach (var key in keys)
                s_Ordered.Add(key.decal);
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
                // only a moved decal or model goes unannounced: anything else sets s_Dirty
                var changed = false;
                s_ModelList.Clear();
                s_ModelSet.Clear();
                foreach (var model in ChiselModelManager.Instance.Models)
                {
                    if (model != null && model.isActiveAndEnabled && model.Node.Valid && s_ModelSet.Add(model))
                        s_ModelList.Add(model);
                }

                // Forget models that are gone
                s_Stale.Clear();
                foreach (var pair in s_Models)
                {
                    if (pair.Key == null || !s_ModelSet.Contains(pair.Key))
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
                    var worldToLocal = model.transform.worldToLocalMatrix;
                    if (!worldToLocal.Equals(state.worldToLocal))
                    {
                        state.worldToLocal = worldToLocal;
                        changed = true;
                    }
                }

                if (s_Dirty || s_OrderDirty)
                {
                    Order();
                    s_OrderDirty = false;
                    changed = true;
                }

                if (s_Dirty)
                {
                    s_DecalStates.Clear();
                    foreach (var decal in s_Ordered)
                    {
                        if (decal == null || decal.Material == null)
                            continue;
                        if (decal.Shape == ChiselDecalShape.Trim)
                        {
                            s_DecalStates[decal] = new DecalState
                            {
                                transform        = decal.transform,
                                entityID         = EntityId.ToULong(decal.GetEntityId()),
                                renderMaterial   = EntityId.ToULong(decal.Material.GetEntityId()),
                                destinationFlags = GetDestinationFlags(decal.Material),
                                trimDecal        = decal
                            };
                            continue;
                        }
                        // A box decal's target is its first; the rest are a trim's, kept for switching back
                        ChiselDecalTarget[] targets = null;
                        if (decal.Targets.Count > 0)
                        {
                            var target = decal.Targets[0];
                            // Limited to a surface that is gone: it draws nothing
                            if (target.brush == null)
                                continue;
                            targets = new[]
                            {
                                new ChiselDecalTarget
                                {
                                    brushEntityID = EntityId.ToULong(target.brush.GetEntityId()),
                                    surfaceIndex  = target.surfaceIndex
                                }
                            };
                        }
                        s_DecalStates[decal] = new DecalState
                        {
                            parentModel      = decal.GetComponentInParent<ChiselModelComponent>(),
                            transform        = decal.transform,
                            entityID         = EntityId.ToULong(decal.GetEntityId()),
                            renderMaterial   = EntityId.ToULong(decal.Material.GetEntityId()),
                            destinationFlags = GetDestinationFlags(decal.Material),
                            targets          = targets
                        };
                    }
                }

                foreach (var decalState in s_DecalStates.Values)
                {
                    // a trim decal follows its surfaces, which nothing announces either
                    if (decalState.trimDecal != null && UpdateTrim(decalState))
                        changed = true;
                    if (decalState.transform == null)
                        continue;
                    var localToWorld = decalState.transform.localToWorldMatrix;
                    if (!localToWorld.Equals(decalState.localToWorld))
                    {
                        decalState.localToWorld = localToWorld;
                        changed = true;
                    }
                }

                if (!changed && !s_Dirty)
                    return;

                foreach (var model in s_ModelList)
                {
                    var state = s_Models[model];
                    state.decals.Clear();
                    state.targets.Clear();
                }

                foreach (var decal in s_Ordered)
                {
                    if (decal == null || !decal.isActiveAndEnabled ||
                        !s_DecalStates.TryGetValue(decal, out var decalState))
                        continue;
                    if (decalState.trimDecal != null)
                    {
                        if (decalState.trimValid && s_Models.TryGetValue(decalState.trimModel, out var trimModelState))
                        {
                            for (int p = 0; p < decalState.trimPieces.Count; p++)
                                Add(trimModelState, decalState.trimPieces[p], decalState.trimTargets[p]);
                        }
                        continue;
                    }
                    if (decalState.parentModel != null)
                    {
                        if (s_Models.TryGetValue(decalState.parentModel, out var state))
                            AddBox(state, decal, decalState);
                        continue;
                    }
                    var scene = decal.gameObject.scene;
                    foreach (var model in s_ModelList)
                    {
                        if (model.gameObject.scene == scene)
                        {
                            var state = s_Models[model];
                            AddBox(state, decal, decalState);
                        }
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
                        {
                            decals[i] = state.decals[i];
                            ChiselDecalFades.Register(state.decals[i]);
                        }
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
                s_ModelSet.Clear();
                s_Stale.Clear();
                Profiler.EndSample();
            }
        }

        /// <summary>Whether a trim decal's halves are no longer what the last update gave its tree (its surfaces changed since)</summary>
        public static bool TrimsChanged()
        {
            foreach (var state in s_DecalStates.Values)
            {
                if (state.trimDecal == null)
                    continue;
                var valid = ChiselTrimDecals.TryDescribe(state.trimDecal, state.entityID, state.renderMaterial, state.destinationFlags,
                                                         out var model, s_TrimHalves, out _, out _);
                if (valid != state.trimValid || (valid && !SameTrim(state, model)))
                    return true;
            }
            return false;
        }

        static readonly List<ChiselTrimDecals.Half> s_TrimHalves = new();

        static bool SameTrim(DecalState state, ChiselModelComponent model)
        {
            if (model != state.trimModel || s_TrimHalves.Count != state.trimPieces.Count)
                return false;
            for (int p = 0; p < s_TrimHalves.Count; p++)
            {
                if (!s_TrimHalves[p].decal.Equals(state.trimPieces[p]) || !SameTargets(s_TrimHalves[p].targets, state.trimTargets[p]))
                    return false;
            }
            return true;
        }

        static bool SameTargets(ChiselDecalTarget[] a, ChiselDecalTarget[] b)
        {
            if (a == null || b == null)
                return a == b;
            if (a.Length != b.Length)
                return false;
            for (int i = 0; i < a.Length; i++)
            {
                if (!a[i].Equals(b[i]))
                    return false;
            }
            return true;
        }

        // The trim decal's pieces now; true when they are not what they were
        static bool UpdateTrim(DecalState state)
        {
            var valid = ChiselTrimDecals.TryDescribe(state.trimDecal, state.entityID, state.renderMaterial, state.destinationFlags,
                                                     out var model, s_TrimHalves, out var edgeFrom, out var edgeTo);
            if (valid)
                KeepOnEdge(state.trimDecal.transform, model, edgeFrom, edgeTo);
            if (valid == state.trimValid && (!valid || SameTrim(state, model)))
                return false;
            state.trimValid = valid;
            state.trimModel = model;
            state.trimPieces.Clear();
            state.trimTargets.Clear();
            foreach (var half in s_TrimHalves)
            {
                state.trimPieces.Add(half.decal);
                state.trimTargets.Add(half.targets);
            }
            return true;
        }

        // A trim decal's transform stays on its edge, at the point nearest to where it is (it can slide along it)
        static void KeepOnEdge(Transform transform, ChiselModelComponent model, Unity.Mathematics.float3 edgeFrom, Unity.Mathematics.float3 edgeTo)
        {
            var toWorld = model.transform.localToWorldMatrix;
            var start = toWorld.MultiplyPoint3x4(edgeFrom);
            var end   = toWorld.MultiplyPoint3x4(edgeTo);
            var edge  = end - start;
            var position = transform.position;
            var t = Mathf.Clamp01(Vector3.Dot(position - start, edge) / Mathf.Max(edge.sqrMagnitude, 1e-12f));
            var onEdge = start + edge * t;
            if ((onEdge - position).sqrMagnitude > 1e-10f)
                transform.position = onEdge;
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

        static readonly List<ChiselDecalInstance> s_Tiles = new();

        static void AddBox(ModelState state, ChiselDecalComponent decal, DecalState decalState)
        {
            Describe(decal, decalState, state.worldToLocal, s_Tiles);
            foreach (var tile in s_Tiles)
                Add(state, tile, decalState.targets);
        }

        /// <summary>
        /// A box decal's instances in a model's space: one, its image repeated by wrapping the texture; or for an image in a
        /// trim sheet, which can't wrap, a tile of the box for every image, the last row and column cut short (orthographic
        /// only: a perspective decal draws a sheet's image once)
        /// </summary>
        static void Describe(ChiselDecalComponent decal, DecalState decalState, Matrix4x4 modelWorldToLocal, List<ChiselDecalInstance> tiles)
        {
            tiles.Clear();
            // The tree's space is the model's own space
            var decalToTree = (float4x4)(modelWorldToLocal * decalState.localToWorld);
            var settings = decal.Settings;
            var rect     = decal.ImageRect;
            var repeat   = math.max((float2)(Vector2)decal.Repeat, new float2(1e-3f));
            var sheet    = rect != new Rect(0, 0, 1, 1);
            var userScale = math.all((float2)settings.uvScale == float2.zero) ? new float2(1) : (float2)settings.uvScale;
            var instance = new ChiselDecalInstance
            {
                entityID         = decalState.entityID,
                decalToTree      = decalToTree,
                renderMaterial   = decalState.renderMaterial,
                destinationFlags = decalState.destinationFlags
            };
            if (decal.Shape == ChiselDecalShape.Polygon)
            {
                DescribePolygon(decal, decalState, instance, rect, userScale, tiles);
                return;
            }

            // a perspective or omni-directional decal's image isn't a box to cut into tiles: it is drawn once
            var perspective = settings.projection != ChiselDecalProjection.Orthographic;
            if (!sheet || perspective)
            {
                // uv = offset + scale * (rect.position + rect.size * repeat * p)
                var tile = settings;
                tile.uvScale  = userScale * (float2)rect.size * (sheet ? new float2(1) : repeat);
                tile.uvOffset = (float2)settings.uvOffset + userScale * (float2)rect.position;
                instance.settings = tile;
                tiles.Add(instance);
                return;
            }

            var size  = (float3)settings.size;
            var count = (int2)math.ceil(repeat - 1e-4f);
            if (count.x * count.y > ChiselTrimDecals.MaxPieces)
                return;
            var tileSize = size.xy / repeat;
            var low      = ((float3)settings.center).xy - size.xy * 0.5f;
            for (int y = 0, piece = 0; y < count.y; y++)
            {
                for (int x = 0; x < count.x; x++, piece++)
                {
                    var from  = low + tileSize * new float2(x, y);
                    var to    = math.min(from + tileSize, low + size.xy);
                    var shown = (to - from) / tileSize;
                    var tile  = settings;
                    tile.center   = new float3((from + to) * 0.5f, settings.center.z);
                    tile.size     = new float3(to - from, size.z);
                    ChiselDecalSettings.TurnMapping(shown, float2.zero, settings.imageTurns, out var imageScale, out var imageOffset);
                    tile.uvScale  = userScale * (float2)rect.size * imageScale;
                    tile.uvOffset = (float2)settings.uvOffset + userScale * ((float2)rect.position + (float2)rect.size * imageOffset);
                    instance.entityID = ChiselTrimDecals.PieceEntityID(decalState.entityID, piece);
                    instance.settings = tile;
                    tiles.Add(instance);
                }
            }
        }

        static readonly List<int> s_PolygonTriangles = new();

        // A polygon decal's instances: a triangle decal for each triangle of its outline, the image spanning the outline's bounds
        static void DescribePolygon(ChiselDecalComponent decal, DecalState decalState, ChiselDecalInstance instance,
                                    Rect rect, float2 userScale, List<ChiselDecalInstance> tiles)
        {
            var outline = decal.Polygon;
            if (!ChiselDecalComponent.Triangulate(outline, s_PolygonTriangles))
                return;
            var settings = decal.Settings;
            var min = (float2)outline[0];
            var max = min;
            foreach (var point in outline)
            {
                min = math.min(min, point);
                max = math.max(max, point);
            }
            var polygonSize = math.max(max - min, new float2(1e-6f));
            for (int t = 0, piece = 0; t + 2 < s_PolygonTriangles.Count && piece < ChiselTrimDecals.MaxPieces; t += 3, piece++)
            {
                var a = (float2)outline[s_PolygonTriangles[t]];
                var b = (float2)outline[s_PolygonTriangles[t + 1]];
                var c = (float2)outline[s_PolygonTriangles[t + 2]];
                var triangleMin  = math.min(a, math.min(b, c));
                var triangleSize = math.max(a, math.max(b, c)) - triangleMin;
                // where the triangle's image lies in the outline's
                ChiselDecalSettings.TurnMapping(triangleSize / polygonSize, (triangleMin - min) / polygonSize, settings.imageTurns,
                                                out var imageScale, out var imageOffset);
                var tile = settings;
                tile.projection = ChiselDecalProjection.Triangle;
                tile.triangle   = new float2x3(a, b, c);
                tile.uvScale    = userScale * (float2)rect.size * imageScale;
                tile.uvOffset   = (float2)settings.uvOffset + userScale * ((float2)rect.position + (float2)rect.size * imageOffset);
                instance.entityID = ChiselTrimDecals.PieceEntityID(decalState.entityID, piece);
                instance.settings = tile;
                tiles.Add(instance);
            }
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
