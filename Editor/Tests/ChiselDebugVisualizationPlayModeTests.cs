using System.Collections;
using System.Linq;
using Chisel.Components;
using Chisel.Core;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace Chisel.Editors.Tests
{
    // The debug visualization surfaces are editor only: entering play mode removes them
    public sealed class ChiselDebugVisualizationPlayModeTests
    {
        const string kRootName = "Debug visualization play mode test";

        static int DebugRendererCount()
        {
            var names = ChiselGeneratedObjects.kGeneratedVisualizationRendererNames;
            return Object.FindObjectsByType<MeshRenderer>(FindObjectsInactive.Include).Count(r => names.Contains(r.gameObject.name));
        }

        static void Update()
        {
            for (int i = 0; i < 3; i++)
            {
                ChiselNodeHierarchyManager.Update();
                ChiselModelManager.Instance.UpdateModels();
            }
        }

        [UnityTest]
        public IEnumerator EnteringPlayMode_RemovesTheDebugSurfaces([Values(true, false)] bool domainReload)
        {
            if (Object.FindObjectsByType<ChiselModelComponent>(FindObjectsInactive.Include).Length > 0)
                Assert.Ignore("A Chisel model is loaded; run this in a scene without one");

            SessionState.SetBool("ChiselDebugVisualizationPlayModeTests.optionsEnabled", EditorSettings.enterPlayModeOptionsEnabled);
            SessionState.SetInt("ChiselDebugVisualizationPlayModeTests.options", (int)EditorSettings.enterPlayModeOptions);
            SessionState.SetInt("ChiselDebugVisualizationPlayModeTests.visualizations", CompactHierarchyManager.DebugVisualizations);
            CompactHierarchyManager.DebugVisualizations = (1 << ChiselGeneratedObjects.kGeneratedVisualizationRendererNames.Length) - 1;

            var root = new GameObject(kRootName);
            root.AddComponent<ChiselModelComponent>();
            var box = new GameObject("Box").AddComponent<ChiselBoxComponent>();
            box.transform.SetParent(root.transform, false);
            foreach (var surface in box.SurfaceDefinition.surfaces)
                surface.SetMaterial(ChiselProjectSettings.ShadowOnlySurfacesMaterial);
            box.OnValidate();
            Update();
            Assert.That(DebugRendererCount(), Is.GreaterThan(0), "no debug surfaces to remove");

            EditorSettings.enterPlayModeOptionsEnabled = !domainReload;
            EditorSettings.enterPlayModeOptions = domainReload ? EnterPlayModeOptions.None : EnterPlayModeOptions.DisableDomainReload;
            yield return new EnterPlayMode();
            yield return null;
            var inPlayMode = DebugRendererCount();
            yield return new ExitPlayMode();

            EditorSettings.enterPlayModeOptionsEnabled = SessionState.GetBool("ChiselDebugVisualizationPlayModeTests.optionsEnabled", false);
            EditorSettings.enterPlayModeOptions = (EnterPlayModeOptions)SessionState.GetInt("ChiselDebugVisualizationPlayModeTests.options", 0);
            CompactHierarchyManager.DebugVisualizations = SessionState.GetInt("ChiselDebugVisualizationPlayModeTests.visualizations", 0);
            var leftover = GameObject.Find(kRootName);
            if (leftover)
                Object.DestroyImmediate(leftover);
            Update();

            Assert.That(inPlayMode, Is.Zero);
        }
    }
}
