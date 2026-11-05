using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace Chisel.Editors
{
    // Clicking again on the same spot in the scene view selects what is behind what was selected; Unity goes on to the
    // next object behind whenever a click finds the same objects under it as the last one, wherever it is. A generator
    // is one object however big it is, so a click anywhere else on it would select what's behind it. Moving the mouse
    // starts over, so only clicks on the same spot go through
    [InitializeOnLoad]
#if UNITY_6000_7_OR_NEWER
    [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
#endif
    static class ChiselClickThrough
    {
        const string kFeature = "clicking through only on the same spot";

        static readonly System.Type s_SceneViewPicking = ChiselUnityInternals.Type("UnityEditor.SceneViewPicking", kFeature);
        static readonly FieldInfo s_PreviousTopmostHash = ChiselUnityInternals.Field(s_SceneViewPicking, "s_PreviousTopmostHash", kFeature);
        static readonly FieldInfo s_PreviousPrefixHash  = ChiselUnityInternals.Field(s_SceneViewPicking, "s_PreviousPrefixHash", kFeature);

        static Vector2 s_LastMousePosition;

        static ChiselClickThrough()
        {
            SceneView.beforeSceneGui -= OnBeforeSceneGUI;
            SceneView.beforeSceneGui += OnBeforeSceneGUI;
        }

        static void OnBeforeSceneGUI(SceneView sceneView)
        {
            var current = Event.current;
            if (current.type != EventType.MouseMove && current.type != EventType.MouseDrag)
                return;
            if (current.mousePosition == s_LastMousePosition)
                return;
            s_LastMousePosition = current.mousePosition;
            StartOver();
        }

        // the next click selects what is in front again
        public static void StartOver()
        {
            s_PreviousTopmostHash?.SetValue(null, 0);
            s_PreviousPrefixHash?.SetValue(null, 0);
        }

        public static bool Available => s_PreviousTopmostHash != null && s_PreviousPrefixHash != null;
    }
}
