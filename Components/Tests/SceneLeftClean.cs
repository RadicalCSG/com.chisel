using System.Reflection;

using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

namespace Chisel.Components.Tests
{
    static class SceneLeftClean
    {
        static readonly MethodInfo s_ClearSceneDirtiness = typeof(EditorSceneManager).GetMethod("ClearSceneDirtiness",
            BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic, null, new[] { typeof(Scene) }, null);

        /// <summary>The active scene and whether it is dirty already, for <see cref="Restore"/>: first thing in a test.</summary>
        public static (Scene scene, bool wasDirty) Remember()
        {
            var scene = SceneManager.GetActiveScene();
            return (scene, scene.isDirty);
        }

        /// <summary>
        /// The scene <see cref="Remember"/> saw clean again if the test made it dirty, and nothing else: no other scene, not
        /// one closed since, and nothing said. The very last thing in a test, after anything it measures.
        /// </summary>
        public static void Restore((Scene scene, bool wasDirty) remembered)
        {
            if (remembered.wasDirty || !remembered.scene.IsValid() || !remembered.scene.isLoaded || !remembered.scene.isDirty ||
                s_ClearSceneDirtiness == null)
                return;
            s_ClearSceneDirtiness.Invoke(null, new object[] { remembered.scene });
        }
    }
}
