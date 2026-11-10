using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

namespace Chisel.Core
{
    // How a material's texture is scaled along its short axis, so a texture that isn't square covers a surface with
    // square pixels: from its main texture's size, 1:1 without one. It goes on the uvs the uv matrix gives
    // (UVMatrix.Scaled), so the matrix stays the same whatever material a surface has.
#if UNITY_6000_7_OR_NEWER
    [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
#endif
    public static class ChiselTextureScale
    {
        // the main texture of Unity's own shaders (built-in, URP, HDRP lit and unlit), for a shader that marks none
        static readonly string[] kMainTextureNames = { "_BaseMap", "_BaseColorMap", "_MainTex", "_UnlitColorMap" };

        // measured once, until Refresh
        static readonly Dictionary<Material, float2> s_Scales = new();

        /// <summary>The scale for a texture of this size: its short axis scaled up to its long one's, 1 for a square</summary>
        public static float2 ScaleOf(int width, int height)
        {
            if (width <= 0 || height <= 0 || width == height)
                return 1;
            return width > height ? new float2(1, (float)width / height) : new float2((float)height / width, 1);
        }

        /// <summary>The material's main texture: the one its shader marks as main, else the one Unity's own shaders use</summary>
        public static Texture MainTextureOf(Material material)
        {
            if (material == null)
                return null;
            var shader = material.shader;
            if (shader != null)
            {
                var count = shader.GetPropertyCount();
                for (int i = 0; i < count; i++)
                {
                    if (shader.GetPropertyType(i) != ShaderPropertyType.Texture ||
                        (shader.GetPropertyFlags(i) & ShaderPropertyFlags.MainTexture) == 0)
                        continue;
                    var texture = material.GetTexture(shader.GetPropertyNameId(i));
                    if (texture != null)
                        return texture;
                }
            }
            foreach (var name in kMainTextureNames)
            {
                if (!material.HasTexture(name))
                    continue;
                var texture = material.GetTexture(name);
                if (texture != null)
                    return texture;
            }
            return null;
        }

        static float2 Measure(Material material)
        {
            var texture = MainTextureOf(material);
            return texture != null ? ScaleOf(texture.width, texture.height) : 1;
        }

        /// <summary>The material's texture scale (1 without a material)</summary>
        public static float2 Of(Material material)
        {
            if (material == null)
                return 1;
            if (!s_Scales.TryGetValue(material, out var scale))
            {
                scale = Measure(material);
                s_Scales[material] = scale;
            }
            return scale;
        }

        /// <summary>Measures the materials again, as their textures can have been swapped or resized; adds the ones whose
        /// scale changed to <paramref name="changed"/></summary>
        public static void Refresh(List<Material> changed)
        {
            var materials = new List<Material>(s_Scales.Keys);
            foreach (var material in materials)
            {
                if (material == null)
                {
                    s_Scales.Remove(material);
                    continue;
                }
                var scale = Measure(material);
                if (math.all(scale == s_Scales[material]))
                    continue;
                s_Scales[material] = scale;
                changed?.Add(material);
            }
        }
    }
}
