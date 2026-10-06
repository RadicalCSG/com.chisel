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
    // TODO: Modifying a lightmap index *should also be undoable*
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

        /// <summary>Clears stale lightmap assignments after a lightmapped mesh changes.</summary>
        public static bool ClearLightmapData(ChiselModelComponent model, GameObjectState state, ChiselRenderObjects renderable)
        {
            if (!IsLightmapped(model, state.staticFlags))
                return false;

            renderable.meshRenderer.realtimeLightmapIndex = -1;
            renderable.meshRenderer.lightmapIndex = -1;
            return true;
        }

        /// <summary>Returns the active scene's lightmap resolution, or the default resolution.</summary>
        public static float LightmapResolution()
        {
            if (Lightmapping.TryGetLightingSettings(out var settings) && settings != null)
                return settings.lightmapResolution;
            return LightmapUVSettings.kDefaultTexelsPerUnit;
        }

        /// <summary>
        /// Rebuilds model settings whose stored UV density no longer matches the active lighting settings.
        /// </summary>
        public static void UpdateLightmapResolution()
        {
            if (ChiselModelManager.Instance == null)
                return;
            var resolution = LightmapResolution();
            List<ChiselModelComponent> outdated = null;
            foreach (var model in ChiselModelManager.Instance.Models)
            {
                if (!model || model.RenderSettings == null)
                    continue;
                if (model.RenderSettings.lightmapTexelsPerUnit != resolution * model.RenderSettings.scaleInLightmap)
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
