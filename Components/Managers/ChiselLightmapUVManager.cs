#if UNITY_EDITOR
using System.Collections.Generic;
using Chisel.Core;
using UnityEngine;
using UnityEditor;

namespace Chisel.Components
{
    /// <summary>
    /// Keeps generated UV1 layouts synchronized with the active lightmap resolution and each model's scale.
    /// </summary>
    public static class ChiselLightmapUVManager
	{
        /// <summary>
        /// Returns whether the model contributes to global illumination and receives it from lightmaps.
        /// </summary>
        public static bool IsLightmapped(ChiselModelComponent model, StaticEditorFlags staticFlags)
        {
            return model &&
                   (staticFlags & StaticEditorFlags.ContributeGI) == StaticEditorFlags.ContributeGI &&
                   model.RenderSettings.receiveGI == ReceiveGI.Lightmaps;
        }

        /// <summary>
        /// A lightmapped mesh's lightmap layout changed, from previousLayout to the renderable's: the lightmap it was baked
        /// with no longer fits it and is taken off. It is kept with the layout it was baked for, and given back when the
        /// mesh has that layout again, as when the edit is undone.
        /// </summary>
        public static bool LightmapLayoutChanged(ChiselModelComponent model, GameObjectState state, ChiselRenderObjects renderable, uint previousLayout)
        {
            if (!IsLightmapped(model, state.staticFlags))
                return false;

            var renderer = renderable.meshRenderer;
            // it had a lightmap, baked for the layout it had until now
            if (renderer.lightmapIndex != -1 && previousLayout != 0)
            {
                renderable.bakedLightmapHashValue   = previousLayout;
                renderable.bakedLightmapIndex       = renderer.lightmapIndex;
                renderable.bakedLightmapScaleOffset = renderer.lightmapScaleOffset;
#if !UNITY_6000_7_OR_NEWER
                renderable.bakedRealtimeLightmapIndex       = renderer.realtimeLightmapIndex;
                renderable.bakedRealtimeLightmapScaleOffset = renderer.realtimeLightmapScaleOffset;
#endif
            }

            var backToTheBakedLayout = renderable.lightmapHashValue != 0 && renderable.lightmapHashValue == renderable.bakedLightmapHashValue;
            renderer.lightmapIndex = backToTheBakedLayout ? renderable.bakedLightmapIndex : -1;
            if (backToTheBakedLayout)
                renderer.lightmapScaleOffset = renderable.bakedLightmapScaleOffset;
#if !UNITY_6000_7_OR_NEWER
            renderer.realtimeLightmapIndex = backToTheBakedLayout ? renderable.bakedRealtimeLightmapIndex : -1;
            if (backToTheBakedLayout)
                renderer.realtimeLightmapScaleOffset = renderable.bakedRealtimeLightmapScaleOffset;
#endif
            return true;
        }

        /// <summary>The lightmaps were baked again or cleared: the ones kept for undone edits are no longer theirs</summary>
        public static void ForgetBakedLightmaps()
        {
            if (ChiselModelManager.Instance == null)
                return;
            foreach (var model in ChiselModelManager.Instance.Models)
            {
                var renderables = model ? model.generated?.renderables : null;
                if (renderables == null)
                    continue;
                foreach (var renderable in renderables)
                    renderable?.ForgetBakedLightmap();
            }
        }

        /// <summary>Returns the active scene's lightmap resolution, or the default resolution.</summary>
        public static float LightmapResolution()
        {
            if (Lightmapping.TryGetLightingSettings(out var settings) && settings != null)
                return settings.lightmapResolution;
            return LightmapUVSettings.kDefaultTexelsPerUnit;
        }

        /// <summary>Returns the active scene's largest lightmap size, or Unity's default.</summary>
        public static int LightmapAtlasSize()
        {
            if (Lightmapping.TryGetLightingSettings(out var settings) && settings != null)
                return settings.lightmapMaxSize;
            return 1024;
        }

        /// <summary>The largest lightmap a renderer of the model may ask for: the atlas size when it bakes lightmaps, else 0</summary>
        public static int LightmapMaxSize(ChiselModelComponent model, int atlasSize)
        {
            return IsLightmapped(model, GameObjectUtility.GetStaticEditorFlags(model.gameObject)) ? atlasSize : 0;
        }

        /// <summary>
        /// Rebuilds model settings whose stored UV density or lightmap size no longer matches the active lighting settings
        /// and the model's own.
        /// </summary>
        public static void UpdateLightmapResolution()
        {
            if (ChiselModelManager.Instance == null)
                return;
            var resolution = LightmapResolution();
            var atlasSize  = LightmapAtlasSize();
            List<ChiselModelComponent> outdated = null;
            foreach (var model in ChiselModelManager.Instance.Models)
            {
                if (!model || model.RenderSettings == null)
                    continue;
                if (model.RenderSettings.lightmapTexelsPerUnit != resolution * model.RenderSettings.scaleInLightmap ||
                    model.RenderSettings.lightmapMaxSize != LightmapMaxSize(model, atlasSize))
                    (outdated ??= new List<ChiselModelComponent>()).Add(model);
            }
            // Syncing may mutate the model collection.
            if (outdated == null)
                return;
            foreach (var model in outdated)
                model.SyncModelSettingsStore();
        }
    }
}
#endif
