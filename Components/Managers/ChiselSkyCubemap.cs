using UnityEngine;
using UnityEngine.Rendering;

namespace Chisel.Components
{
    /// <summary>
    /// Gives the Chisel/Sky shader, which draws sky surfaces, the scene's sky: the cubemap of a cubemap skybox
    /// (Skybox/Cubemap, or any skybox with a cubemap in _Tex), with its rotation, tint and exposure; any other sky
    /// (procedural, 6 sided, panoramic) is drawn with the reflection cubemap Unity renders from it. It is set again
    /// before every frame is drawn, so it follows changes to the sky.
    /// </summary>
#if UNITY_6000_7_OR_NEWER
    [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
#endif
    public static class ChiselSkyCubemap
    {
        public static readonly int kCubemapID    = Shader.PropertyToID("_ChiselSkyCubemap");
        public static readonly int kDecodeHDRID  = Shader.PropertyToID("_ChiselSkyCubemap_HDR");
        public static readonly int kRotationID   = Shader.PropertyToID("_ChiselSkyRotation");
        public static readonly int kMultiplierID = Shader.PropertyToID("_ChiselSkyMultiplier");

        static readonly int kTexID      = Shader.PropertyToID("_Tex");
        static readonly int kRotationPropertyID = Shader.PropertyToID("_Rotation");
        static readonly int kExposureID = Shader.PropertyToID("_Exposure");
        static readonly int kTintID     = Shader.PropertyToID("_Tint");

        // The cubemap, how its values are decoded (DecodeHDREnvironment; a skybox's cubemap is read as it is, (1, 1, 0, 0)),
        // its rotation around y in degrees, and the color it is multiplied by
        public struct Sky
        {
            public Texture  cubemap;
            public Vector4  decodeHDR;
            public float    rotation;
            public Vector3  multiplier;
        }

        static readonly Vector4 kNoDecode = new(1, 1, 0, 0);

        // What the skybox shaders double their tint with: unity_ColorSpaceDouble
        static float ColorSpaceDouble => QualitySettings.activeColorSpace == ColorSpace.Linear ? 4.59479380f : 2.0f;

        public static Sky SkyFor(Material skybox, Texture reflectionCubemap, Vector4 reflectionDecodeHDR)
        {
            if (skybox != null && skybox.HasProperty(kTexID) && skybox.GetTexture(kTexID) is Cubemap cubemap)
            {
                var exposure = skybox.HasProperty(kExposureID) ? skybox.GetFloat(kExposureID) : 1.0f;
                var tint     = skybox.HasProperty(kTintID) ? skybox.GetColor(kTintID) : new Color(0.5f, 0.5f, 0.5f);
                if (QualitySettings.activeColorSpace == ColorSpace.Linear)
                    tint = tint.linear;
                var scale    = ColorSpaceDouble * exposure;
                return new Sky
                {
                    cubemap    = cubemap,
                    decodeHDR  = kNoDecode,
                    rotation   = skybox.HasProperty(kRotationPropertyID) ? skybox.GetFloat(kRotationPropertyID) : 0,
                    multiplier = new Vector3(tint.r * scale, tint.g * scale, tint.b * scale)
                };
            }
            return new Sky
            {
                cubemap    = reflectionCubemap,
                decodeHDR  = reflectionDecodeHDR,
                rotation   = 0,
                multiplier = Vector3.one
            };
        }

        public static void Apply(in Sky sky)
        {
            Shader.SetGlobalTexture(kCubemapID, sky.cubemap != null ? sky.cubemap : Texture2D.blackTexture);
            Shader.SetGlobalVector(kDecodeHDRID, sky.decodeHDR);
            Shader.SetGlobalFloat(kRotationID, sky.rotation);
            Shader.SetGlobalVector(kMultiplierID, sky.multiplier);
        }

        static void ApplySceneSky()
        {
            Apply(SkyFor(RenderSettings.skybox, ReflectionProbe.defaultTexture, ReflectionProbe.defaultTextureHDRDecodeValues));
        }

        static void OnBeginContextRendering(ScriptableRenderContext context, System.Collections.Generic.List<Camera> cameras)
        {
            ApplySceneSky();
        }

#if UNITY_EDITOR
        [UnityEditor.InitializeOnLoadMethod]
#endif
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Initialize()
        {
            RenderPipelineManager.beginContextRendering -= OnBeginContextRendering;
            RenderPipelineManager.beginContextRendering += OnBeginContextRendering;
        }
    }
}
