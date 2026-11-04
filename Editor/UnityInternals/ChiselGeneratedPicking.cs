using Chisel.Components;
using UnityEditor;
using UnityEngine;

namespace Chisel.Editors
{
    // Keeps Unity's picking off the generated objects without an undo step for it (SceneVisibilityManager.DisablePicking
    // records one); where Unity has no way to, the generated objects use the public call, undo step and all
    [InitializeOnLoad]
#if UNITY_6000_7_OR_NEWER
    [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
#endif
    static class ChiselGeneratedPicking
    {
        const string kFeature = "keeping picking off generated objects without an undo step";

        delegate void SetPickingDisabled(GameObject gameObject, bool isPickingDisabled, bool includeDescendants);

        static readonly SetPickingDisabled s_SetPickingDisabled =
            ChiselUnityInternals.Delegate<SetPickingDisabled>(ChiselUnityInternals.Type("UnityEditor.SceneVisibilityState", kFeature), "SetGameObjectPickingDisabled", kFeature);

        public static bool Available => s_SetPickingDisabled != null;

        static ChiselGeneratedPicking()
        {
            if (s_SetPickingDisabled != null)
                ChiselGeneratedObjects.DisablePickingWithoutUndo = gameObject => s_SetPickingDisabled(gameObject, true, true);
        }
    }
}
