using System.Collections.Generic;
using Chisel.Core;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace Chisel.Components
{
    /// <summary>
    /// Gives each model's tree the paint of its generators' surfaces (<see cref="ChiselGeneratorComponent.SurfacePaint"/>),
    /// in the model's space, and rebuilds the brushes whose paint changed. Called before the trees are built.
    /// </summary>
    public static class ChiselPaintManager
    {
        static bool s_Dirty = true;
        static readonly List<ChiselGeneratorComponent>                   s_Painted       = new();
        // each painted generator's space in its model's, as its paint was last given in
        static readonly Dictionary<ChiselGeneratorComponent, Matrix4x4>  s_Spaces        = new();
        static readonly HashSet<ChiselModelComponent>                    s_PaintedModels = new();
        static readonly Dictionary<ChiselModelComponent, List<ChiselGeneratorComponent>> s_ByModel = new();
        // every generator paint was given for, by entity: one that lost all of its paint is rebuilt without it too
        static readonly Dictionary<ulong, ChiselGeneratorComponent>      s_Known         = new();

        public static void SetDirty() => s_Dirty = true;

        static Matrix4x4 SpaceOf(ChiselGeneratorComponent generator, ChiselModelComponent model)
        {
            return model.transform.worldToLocalMatrix * generator.transform.localToWorldMatrix;
        }

        public static void Update()
        {
            ChiselPaintMaterials.Update();
            if (!s_Dirty)
            {
                foreach (var generator in s_Painted)
                {
                    var model = generator ? generator.hierarchyItem?.Model : null;
                    if (!generator || !model || !s_Spaces.TryGetValue(generator, out var space) || space != SpaceOf(generator, model))
                    {
                        s_Dirty = true;
                        break;
                    }
                }
                if (!s_Dirty)
                    return;
            }
            s_Dirty = false;

            s_Painted.Clear();
            s_Spaces.Clear();
            foreach (var list in s_ByModel.Values)
                list.Clear();
            foreach (var generator in Object.FindObjectsByType<ChiselGeneratorComponent>(FindObjectsInactive.Exclude))
            {
                var model = generator.hierarchyItem?.Model;
                if (!generator.isActiveAndEnabled || generator.SurfacePaint.Count == 0 || !model)
                    continue;
                ChiselPaintBrush.Respace(generator);
                s_Painted.Add(generator);
                s_Spaces[generator] = SpaceOf(generator, model);
                if (!s_ByModel.TryGetValue(model, out var generators))
                    s_ByModel[model] = generators = new List<ChiselGeneratorComponent>();
                generators.Add(generator);
            }

            // every model with paint, and every model that had paint (which loses it)
            var models = new HashSet<ChiselModelComponent>(s_PaintedModels);
            foreach (var entry in s_ByModel)
            {
                if (entry.Value.Count > 0)
                    models.Add(entry.Key);
            }
            s_PaintedModels.Clear();
            using var changed = new NativeList<ulong>(16, Allocator.Temp);
            foreach (var model in models)
            {
                if (!model || !model.Node.Valid)
                    continue;
                s_ByModel.TryGetValue(model, out var generators);
                if (generators != null && generators.Count > 0)
                    s_PaintedModels.Add(model);
                SetPaint(model, generators, changed);
            }
            foreach (var model in new List<ChiselModelComponent>(s_ByModel.Keys))
            {
                if (!model)
                    s_ByModel.Remove(model);
            }
        }

        static void SetPaint(ChiselModelComponent model, List<ChiselGeneratorComponent> generators, NativeList<ulong> changed)
        {
            var layerCount = 0;
            var valueCount = 0;
            if (generators != null)
            {
                foreach (var generator in generators)
                {
                    foreach (var paint in generator.SurfacePaint)
                    {
                        if (!Drawn(generator, paint))
                            continue;
                        layerCount++;
                        valueCount += paint.values.Length;
                    }
                }
            }

            NativeArray<ChiselPaintLayer> layers;
            using var _layers = layers = new NativeArray<ChiselPaintLayer>(layerCount, Allocator.Temp);
            NativeArray<float4> values;
            using var _values = values = new NativeArray<float4>(valueCount, Allocator.Temp);
            int l = 0, v = 0;
            if (generators != null)
            {
                foreach (var generator in generators)
                {
                    var entityID = EntityId.ToULong(generator.GetEntityId());
                    s_Known[entityID] = generator;
                    var space = s_Spaces[generator];
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
                        foreach (var value in paint.values)
                            values[v++] = value;
                    }
                }
            }

            if (!model.Node.SetPaint(layers, values, changed))
                return;
            // the brushes of a generator whose paint changed are built again, with it
            foreach (var entityID in changed)
            {
                if (s_Known.TryGetValue(entityID, out var generator) && generator)
                    SetBrushesDirty(generator.TopTreeNode);
                else
                    s_Known.Remove(entityID);
            }
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
                node.SetDirty();
                return;
            }
            for (int i = 0; i < node.Count; i++)
                SetBrushesDirty(node[i]);
        }
    }
}
