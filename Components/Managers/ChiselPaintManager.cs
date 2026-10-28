using System.Collections.Generic;
using Chisel.Core;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace Chisel.Components
{
    /// <summary>
    /// Gives each model's tree the paint of its generators' surfaces (<see cref="ChiselGeneratorComponent.SurfacePaint"/>),
    /// in the model's space, and rebuilds the brushes whose paint changed. Called before the trees are built. Only the
    /// generators that said their paint changed, or that moved, are looked at.
    /// </summary>
    public static class ChiselPaintManager
    {
        // a generator whose paint a tree has: which tree, and the generator's space in its model's as the paint was given in
        struct Given
        {
            public ulong     entityID;
            public CSGTree   tree;
            public Matrix4x4 space;
        }

        static bool s_All = true;
        static int  s_ClearCount;
        static readonly HashSet<ChiselGeneratorComponent>             s_Changed = new();
        static readonly Dictionary<ChiselGeneratorComponent, Given>   s_Given   = new();
        static readonly HashSet<ChiselGeneratorComponent>             s_Work    = new();

        /// <summary>Every generator's paint is looked at again (what shaders take may have changed)</summary>
        public static void SetDirty() => s_All = true;

        // Entering play mode without a domain reload keeps all of this: what was destroyed goes, and every generator's
        // paint is looked at again
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetState()
        {
            s_Changed.RemoveWhere(generator => generator == null);
            s_Work.Clear();
            s_GivenNow.Clear();
            s_All = true;
        }

        /// <summary>A generator's paint, or what it is drawn with, may have changed</summary>
        public static void SetDirty(ChiselGeneratorComponent generator)
        {
            // only one with paint, or whose paint a tree has (it may have lost it): no other generator is held on to
            if (generator && (generator.SurfacePaint.Count > 0 || s_Given.ContainsKey(generator)))
                s_Changed.Add(generator);
        }

        static Matrix4x4 SpaceOf(ChiselGeneratorComponent generator, ChiselModelComponent model)
        {
            return model.transform.worldToLocalMatrix * generator.transform.localToWorldMatrix;
        }

        public static void Update()
        {
            ChiselPaintMaterials.Update();

            // every tree was released, and their paint with them: all of it is given again
            if (s_ClearCount != CompactHierarchyManager.ClearCount)
            {
                s_ClearCount = CompactHierarchyManager.ClearCount;
                s_Given.Clear();
                s_All = true;
            }

            s_Work.Clear();
            // the painted generators that moved, are in another tree, or are gone
            foreach (var entry in s_Given)
            {
                var generator = entry.Key;
                var model = generator ? generator.hierarchyItem?.Model : null;
                if (s_All || !generator || !model || entry.Value.tree != model.Node || entry.Value.space != SpaceOf(generator, model))
                    s_Work.Add(generator);
            }
            if (s_All)
            {
                foreach (var generator in Object.FindObjectsByType<ChiselGeneratorComponent>(FindObjectsInactive.Exclude))
                {
                    if (generator.SurfacePaint.Count > 0 && !s_Given.ContainsKey(generator))
                        s_Work.Add(generator);
                }
                s_All = false;
            }
            s_Work.UnionWith(s_Changed);
            s_Changed.Clear();

            foreach (var generator in s_Work)
                Give(generator);
            s_GivenNow.UnionWith(s_Work);
            s_Work.Clear();
        }

        // the generators given since the last trees were built: the build may have given them new shapes
        static readonly HashSet<ChiselGeneratorComponent> s_GivenNow = new();

        /// <summary>
        /// After the trees were built: grows the grids of the generators given since the last build that no longer cover
        /// their surfaces, as the generators only get their new shapes then. True when any did; their paint is given
        /// again at the next <see cref="Update"/>.
        /// </summary>
        public static bool GridsGrew()
        {
            var grew = false;
            foreach (var generator in s_GivenNow)
            {
                if (!generator || generator.SurfacePaint.Count == 0 || !ChiselPaintBrush.Refit(generator))
                    continue;
                s_Changed.Add(generator);
#if UNITY_EDITOR
                UnityEditor.EditorUtility.SetDirty(generator);
#endif
                grew = true;
            }
            s_GivenNow.Clear();
            return grew;
        }

        // The generator's paint given to its model's tree, and taken from the tree it was in before
        static void Give(ChiselGeneratorComponent generator)
        {
            s_Given.TryGetValue(generator, out var before);
            var given = s_Given.ContainsKey(generator);
            var model = generator ? generator.hierarchyItem?.Model : null;
            if (!generator || !generator.isActiveAndEnabled || !model || !model.Node.Valid || generator.SurfacePaint.Count == 0)
            {
                if (given)
                {
                    // built again without it, if it is still there
                    if (before.tree.Valid && before.tree.SetPaint(before.entityID, default, default) != ChiselPaintChange.None && generator)
                        SetBrushesDirty(generator.TopTreeNode);
                    s_Given.Remove(generator);
                }
                return;
            }

            var entityID = EntityId.ToULong(generator.GetEntityId());
            var tree     = model.Node;
            if (given && (before.tree != tree || before.entityID != entityID) && before.tree.Valid)
                before.tree.SetPaint(before.entityID, default, default);

            ChiselPaintBrush.Respace(generator);
            var space = SpaceOf(generator, model);
            var layerCount = 0;
            var valueCount = 0;
            foreach (var paint in generator.SurfacePaint)
            {
                if (!Drawn(generator, paint))
                    continue;
                layerCount++;
                valueCount += paint.values.Length;
            }

            NativeArray<ChiselPaintLayer> layers;
            using var _layers = layers = new NativeArray<ChiselPaintLayer>(layerCount, Allocator.Temp);
            NativeArray<float> values;
            using var _values = values = new NativeArray<float>(valueCount, Allocator.Temp);
            int l = 0, v = 0;
            foreach (var paint in generator.SurfacePaint)
            {
                if (!Drawn(generator, paint))
                    continue;
                layers[l++] = new ChiselPaintLayer
                {
                    entityID     = entityID,
                    surfaceIndex = paint.surfaceIndex,
                    attribute    = paint.attribute,
                    components   = paint.components,
                    defaultValue = paint.defaultValue,
                    origin       = space.MultiplyPoint3x4(paint.origin),
                    cellU        = space.MultiplyVector(paint.cellU),
                    cellV        = space.MultiplyVector(paint.cellV),
                    resolution   = new int2(paint.resolution.x, paint.resolution.y),
                    valueStart   = v
                };
                NativeArray<float>.Copy(paint.values, 0, values, v, paint.values.Length);
                v += paint.values.Length;
            }
            s_Given[generator] = new Given { entityID = entityID, tree = tree, space = space };
            // the brushes of a generator whose grids changed are built again, with them; new values on the same grids only
            // have the meshes copied again
            if (tree.SetPaint(entityID, layers, values) == ChiselPaintChange.Grids)
                SetBrushesDirty(generator.TopTreeNode);
        }

        // Whether the surface's shader takes paint in this attribute; paint it doesn't take is kept, but not drawn
        static bool Drawn(ChiselGeneratorComponent generator, ChiselSurfacePaint paint)
        {
            if (paint == null || !paint.IsValid)
                return false;
            var surfaces = generator.SurfaceDefinition?.surfaces;
            if (surfaces == null || paint.surfaceIndex < 0 || paint.surfaceIndex >= surfaces.Length)
                return false;
            return ChiselPaintProfiles.TakesPaint(surfaces[paint.surfaceIndex].RenderMaterial, paint.attribute);
        }

        static void SetBrushesDirty(CSGTreeNode node)
        {
            if (!node.Valid)
                return;
            if (node.Type == CSGNodeType.Brush)
            {
                node.SetSurfacesDirty();
                return;
            }
            for (int i = 0; i < node.Count; i++)
                SetBrushesDirty(node[i]);
        }
    }
}
