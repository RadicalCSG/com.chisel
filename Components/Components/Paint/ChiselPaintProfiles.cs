using System.Collections.Generic;
using Chisel.Core;
using UnityEngine;

namespace Chisel.Components
{
    /// <summary>The project's paint profiles, by the shader they are for</summary>
    public static class ChiselPaintProfiles
    {
        static Dictionary<Shader, ChiselPaintProfile> s_ByShader;
        // profiles made in code (not assets), which come first
        static readonly List<ChiselPaintProfile> s_Registered = new();

        public static void Register(ChiselPaintProfile profile)
        {
            if (profile != null && !s_Registered.Contains(profile))
                s_Registered.Add(profile);
            ChiselPaintManager.SetDirty();
        }

        public static void Unregister(ChiselPaintProfile profile)
        {
            s_Registered.Remove(profile);
            ChiselPaintManager.SetDirty();
        }

        /// <summary>Called when profiles may have been added, removed or changed</summary>
        public static void SetDirty() => s_ByShader = null;

        public static ChiselPaintProfile Find(Shader shader)
        {
            if (shader == null)
                return null;
            foreach (var registered in s_Registered)
            {
                if (registered != null && registered.shader == shader)
                    return registered;
            }
            if (s_ByShader == null)
            {
                s_ByShader = new Dictionary<Shader, ChiselPaintProfile>();
#if UNITY_EDITOR
                foreach (var guid in UnityEditor.AssetDatabase.FindAssets("t:" + nameof(ChiselPaintProfile)))
                {
                    var profile = UnityEditor.AssetDatabase.LoadAssetAtPath<ChiselPaintProfile>(UnityEditor.AssetDatabase.GUIDToAssetPath(guid));
                    if (profile != null && profile.shader != null)
                        s_ByShader.TryAdd(profile.shader, profile);
                }
#endif
            }
            return s_ByShader.TryGetValue(shader, out var found) ? found : null;
        }

        /// <summary>Whether surfaces drawn with this material take paint in this attribute: its shader's profile has a mode
        /// in it. Paint of a surface whose material doesn't is kept, but not drawn and not subdividing it.</summary>
        public static bool TakesPaint(Material material, ChiselPaintAttribute attribute)
        {
            var profile = material ? Find(material.shader) : null;
            if (profile == null)
                return false;
            foreach (var mode in profile.modes)
            {
                if (mode.attribute == attribute)
                    return true;
            }
            return false;
        }

#if UNITY_EDITOR
        [UnityEditor.InitializeOnLoadMethod]
        static void Initialize()
        {
            UnityEditor.EditorApplication.projectChanged += () =>
            {
                SetDirty();
                ChiselPaintManager.SetDirty();
            };
        }
#endif
    }
}
