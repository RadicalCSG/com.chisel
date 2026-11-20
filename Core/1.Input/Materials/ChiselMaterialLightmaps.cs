using System.Collections.Generic;
using UnityEngine;

namespace Chisel.Core
{
    // Whether the surfaces of a material get lightmap texels: not when its shader can't sample a lightmap (it has no
    // LIGHTMAP_ON variant, as an unlit shader), unless the material's Chisel surface parameters say how many they get.
    // Measured once per material, until Refresh.
#if UNITY_6000_7_OR_NEWER
    [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
#endif
    public static class ChiselMaterialLightmaps
    {
        const string kLightmapKeyword = "LIGHTMAP_ON";
        // Drawn in place of a shader that doesn't compile: what the shader samples isn't known then
        const string kErrorShaderName = "Hidden/InternalErrorShader";

        // measured once, until Refresh; by the material's id, so that measuring it doesn't keep it loaded
        static readonly Dictionary<EntityId, bool> s_SamplesLightmaps = new();

        /// <summary>Whether the shader samples a lightmap: true when that can't be told</summary>
        public static bool SamplesLightmaps(Shader shader)
        {
            if (shader == null || shader.name == kErrorShaderName)
                return true;
            return shader.keywordSpace.FindKeyword(kLightmapKeyword).isValid;
        }

        /// <summary>Whether the material's shader samples a lightmap (true without a material)</summary>
        public static bool SamplesLightmaps(Material material)
        {
            if (material == null)
                return true;
            var id = material.GetEntityId();
            if (!s_SamplesLightmaps.TryGetValue(id, out var samples))
            {
                samples = SamplesLightmaps(material.shader);
                s_SamplesLightmaps[id] = samples;
            }
            return samples;
        }

        /// <summary>
        /// The output flags of a surface with this material, given the flags its surface parameters set: no lightmap
        /// texels when its shader can't sample a lightmap and the parameters don't say how many it gets
        /// </summary>
        public static SurfaceOutputFlags OutputFlagsOf(Material material, SurfaceOutputFlags flags)
        {
            const SurfaceOutputFlags kLightmapFlags = SurfaceOutputFlags.NoLightmap | SurfaceOutputFlags.SingleLightmapTexel;
            if ((flags & kLightmapFlags) != 0 || SamplesLightmaps(material))
                return flags;
            return flags | SurfaceOutputFlags.NoLightmap;
        }

        /// <summary>Whether the surfaces of this material get lightmap texels, so a renderer drawing them is lit by a lightmap</summary>
        public static bool GetsLightmap(Material material)
        {
            var metadata = (material != null) ? material.GetMetadataOfType<ChiselSurfaceMetadata>() : null;
            var flags    = (metadata != null) ? metadata.outputFlags : SurfaceOutputFlags.Default;
            return LightmapUVLayout.ModeOf(OutputFlagsOf(material, flags)) != LightmapChartMode.None;
        }

        /// <summary>Measures the materials again, as their shaders can have been swapped or changed; adds the ones whose
        /// answer changed to <paramref name="changed"/></summary>
        public static void Refresh(List<Material> changed)
        {
            var ids = new List<EntityId>(s_SamplesLightmaps.Keys);
            foreach (var id in ids)
            {
                var material = Resources.EntityIdToObject(id) as Material;
                if (material == null)
                {
                    s_SamplesLightmaps.Remove(id);
                    continue;
                }
                var samples = SamplesLightmaps(material.shader);
                if (samples == s_SamplesLightmaps[id])
                    continue;
                s_SamplesLightmaps[id] = samples;
                changed?.Add(material);
            }
        }
    }
}
