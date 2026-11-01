using System.Collections.Generic;
using UnityEngine;

namespace Chisel.Components
{
    /// <summary>
    /// The project's paint profiles: one per shader whose surfaces can be painted, in one asset, edited in
    /// Project Settings > Chisel > Paint
    /// </summary>
#if UNITY_6000_7_OR_NEWER
    [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
#endif
    public sealed class ChiselPaintSettings : ScriptableObject
    {
        public const string kAssetPath = "Assets/Chisel/ChiselPaintSettings.asset";

        public List<ChiselPaintProfile> profiles = new();

        static ChiselPaintSettings s_Project;

        /// <summary>The project's paint settings, null when it has none</summary>
        public static ChiselPaintSettings Project
        {
            get
            {
#if UNITY_EDITOR
                if (!s_Project)
                {
                    s_Project = UnityEditor.AssetDatabase.LoadAssetAtPath<ChiselPaintSettings>(kAssetPath);
                    if (!s_Project)
                    {
                        foreach (var guid in UnityEditor.AssetDatabase.FindAssets("t:" + nameof(ChiselPaintSettings)))
                        {
                            s_Project = UnityEditor.AssetDatabase.LoadAssetAtPath<ChiselPaintSettings>(UnityEditor.AssetDatabase.GUIDToAssetPath(guid));
                            if (s_Project)
                                break;
                        }
                    }
                }
#endif
                return s_Project;
            }
            set
            {
                s_Project = value;
                Changed();
            }
        }

        /// <summary>The first profile for a shader, or null</summary>
        public ChiselPaintProfile Find(Shader shader)
        {
            if (!shader)
                return null;
            foreach (var profile in profiles)
            {
                if (profile != null && profile.shader == shader)
                    return profile;
            }
            return null;
        }

        /// <summary>Called after the profiles changed</summary>
        public static void Changed()
        {
            ChiselPaintProfiles.SetDirty();
            ChiselPaintManager.SetDirty();
        }

        void OnEnable()
        {
            if (!s_Project)
                s_Project = this;
            Changed();
        }

        void OnValidate() => Changed();
    }
}
