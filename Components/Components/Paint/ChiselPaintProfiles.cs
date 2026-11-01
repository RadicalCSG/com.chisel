using System.Collections.Generic;
using Chisel.Core;
using UnityEngine;

namespace Chisel.Components
{
    /// <summary>The project's paint profiles (<see cref="ChiselPaintSettings"/>), by the shader they are for</summary>
#if UNITY_6000_7_OR_NEWER
    [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
#endif
    public static class ChiselPaintProfiles
    {
        static Dictionary<Shader, ChiselPaintProfile> s_ByShader;
        // profiles registered in code, which come first
        static readonly List<ChiselPaintProfile> s_Registered = new();

        public static void Register(ChiselPaintProfile profile)
        {
            if (profile != null && !s_Registered.Contains(profile))
                s_Registered.Add(profile);
            SetDirty();
            ChiselPaintManager.SetDirty();
        }

        public static void Unregister(ChiselPaintProfile profile)
        {
            s_Registered.Remove(profile);
            SetDirty();
            ChiselPaintManager.SetDirty();
        }

        /// <summary>Called when profiles may have been added, removed or changed</summary>
        public static void SetDirty()
        {
            s_ByShader = null;
            ChiselPaintMaterials.Changed();
        }

        // The attributes a material's shader takes paint in, each with the defaults of its modes
        static void Resolve(Material material, List<ChiselPaintMaterialSlot> slots)
        {
            var profile = Find(material.shader);
            if (profile == null)
                return;
            foreach (var mode in profile.modes)
            {
                var index = slots.FindIndex(slot => slot.attribute == mode.attribute);
                if (index < 0)
                {
                    slots.Add(new ChiselPaintMaterialSlot { attribute = mode.attribute });
                    index = slots.Count - 1;
                }
                var slot = slots[index];
                slot.defaultValue = ChiselPaintLayer.Apply(mode.defaultValue, slot.defaultValue, mode.components);
                slot.components  |= mode.components;
                slots[index] = slot;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Hook()
        {
            ChiselPaintMaterials.Resolver = Resolve;
            ChiselPaintMaterials.Changed();
        }

        public static ChiselPaintProfile Find(Shader shader)
        {
            if (shader == null)
                return null;
            foreach (var registered in s_Registered)
            {
                if (registered != null && registered.shader == shader)
                    return registered;
            }
            Load();
            return s_ByShader.TryGetValue(shader, out var found) ? found : null;
        }

        /// <summary>Every profile: those registered in code, then the project's (unless one in code is for its shader)</summary>
        public static IEnumerable<ChiselPaintProfile> All
        {
            get
            {
                foreach (var registered in s_Registered)
                {
                    if (registered != null)
                        yield return registered;
                }
                Load();
                foreach (var profile in s_ByShader.Values)
                {
                    if (profile != null && Find(profile.shader) == profile)
                        yield return profile;
                }
            }
        }

        static void Load()
        {
            if (s_ByShader != null)
                return;

            // Loading the settings asset calls SetDirty, so fill a local copy and publish it last
            var settings = ChiselPaintSettings.Project;
            var byShader = new Dictionary<Shader, ChiselPaintProfile>();
            if (settings)
            {
                foreach (var profile in settings.profiles)
                {
                    if (profile != null && profile.shader != null)
                        byShader.TryAdd(profile.shader, profile);
                }
            }
            s_ByShader = byShader;
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
            Hook();
            UnityEditor.EditorApplication.projectChanged += () =>
            {
                SetDirty();
                ChiselPaintManager.SetDirty();
            };
        }
#endif
    }
}
