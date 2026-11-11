using UnityEditor;

namespace Chisel.Editors
{
    // Chisel's icons and the styles it copies from the editor skin are made for the skin (light or dark) the scripts
    // were loaded in, and kept. Switching skins doesn't reload the scripts, so this reloads them, which makes all of
    // them again (toolbars, overlays, tools, inspectors). Not while playing: the reload waits until play mode ends
#if UNITY_6000_7_OR_NEWER
    [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup] // the skin the scripts were loaded in stays true until they reload
#endif
    static class ChiselEditorSkinWatcher
    {
        internal static bool s_LoadedInProSkin;
        internal static bool s_ReloadRequested;

        [InitializeOnLoadMethod]
        static void Watch()
        {
            s_LoadedInProSkin = EditorGUIUtility.isProSkin;
            s_ReloadRequested = false;
            EditorApplication.update -= OnUpdate;
            EditorApplication.update += OnUpdate;
        }

        static void OnUpdate()
        {
            if (!ReloadIfSkinChanged(EditorGUIUtility.isProSkin, EditorApplication.isPlayingOrWillChangePlaymode))
                return;
            ChiselEditorResources.ClearCache();
            EditorUtility.RequestScriptReload();
        }

        // Whether the scripts are to be reloaded now: the skin is not the one they were loaded in, nothing is playing,
        // and no reload was asked for yet
        internal static bool ReloadIfSkinChanged(bool isProSkin, bool isPlaying)
        {
            if (s_ReloadRequested || isPlaying || isProSkin == s_LoadedInProSkin)
                return false;
            s_ReloadRequested = true;
            return true;
        }
    }
}
