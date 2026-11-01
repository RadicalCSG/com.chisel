using UnityEditor;
using UnityEngine;

namespace Chisel.Editors
{
    // What the model inspector warns about in its lightmapping settings, as Unity's own renderer inspector does: these
    // checks are internal to Unity
#if UNITY_6000_7_OR_NEWER
    [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
#endif
    static class ChiselLightmappingInternals
    {
        const string kFeature = "the model inspector's lightmapping warnings";

        delegate bool RendererCheckDelegate(Renderer renderer);
        delegate bool ShaderCheckDelegate(Shader shader);

        static readonly RendererCheckDelegate s_HasClampedResolution = ChiselUnityInternals.Delegate<RendererCheckDelegate>(typeof(Lightmapping), "HasClampedResolution", kFeature);
        static readonly RendererCheckDelegate s_HasUVOverlaps        = ChiselUnityInternals.Delegate<RendererCheckDelegate>(typeof(Lightmapping), "HasUVOverlaps", kFeature);
        static readonly ShaderCheckDelegate   s_HasInstancing        = ChiselUnityInternals.Delegate<ShaderCheckDelegate>(typeof(ShaderUtil), "HasInstancing", kFeature);

        public static bool Available => s_HasClampedResolution != null && s_HasUVOverlaps != null && s_HasInstancing != null;

        public static bool HasClampedResolution(Renderer renderer) => s_HasClampedResolution != null && renderer && s_HasClampedResolution(renderer);
        public static bool HasUVOverlaps(Renderer renderer)        => s_HasUVOverlaps != null && renderer && s_HasUVOverlaps(renderer);
        public static bool HasInstancing(Shader shader)            => s_HasInstancing != null && shader && s_HasInstancing(shader);
    }
}
