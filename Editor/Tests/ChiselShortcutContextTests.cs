using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Chisel.Components;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.EditorTools;
using UnityEditor.ShortcutManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Chisel.Editors.Tests
{
    // Chisel's keys are only in use while their own tool is; everywhere else a key does what it does without Chisel. What a
    // key does is asked of Unity's shortcut directory, as a key press in the Scene view would.
    public sealed class ChiselShortcutContextTests
    {
        GameObject root;
        Object[]   previousSelection;
        Type       previousContext;
        Type       previousTool;

        [SetUp]
        public void SetUp()
        {
            previousSelection = Selection.objects;
            previousContext   = ToolManager.activeContextType;
            previousTool      = ToolManager.activeToolType;
        }

        [TearDown]
        public void TearDown()
        {
            Selection.objects = previousSelection;
            if (ToolManager.activeContextType != previousContext)
                ToolManager.SetActiveContext(previousContext);
            if (ToolManager.activeToolType != previousTool && previousTool != null)
                ToolManager.SetActiveTool(previousTool);
            if (root)
                Object.DestroyImmediate(root);
        }

        const BindingFlags kFlags = BindingFlags.Static | BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        static object Find(object owner, string typeName)
        {
            foreach (var property in owner.GetType().GetProperties(kFlags))
            {
                if (property.GetIndexParameters().Length > 0 || property.PropertyType.Name != typeName && !property.PropertyType.IsInterface)
                    continue;
                var value = property.GetValue(owner);
                if (value != null && value.GetType().Name == typeName)
                    return value;
            }
            foreach (var field in owner.GetType().GetFields(kFlags))
            {
                var value = field.GetValue(owner);
                if (value != null && value.GetType().Name == typeName)
                    return value;
            }
            return null;
        }

        // the shortcuts a key press runs with this window focused
        static List<string> Resolve(KeyCode key, EditorWindow focused)
        {
            var assembly       = typeof(ShortcutManager).Assembly;
            var integration    = assembly.GetType("UnityEditor.ShortcutManagement.ShortcutIntegration");
            var controller     = integration.GetProperty("instance", kFlags).GetValue(null);
            var directory      = Find(controller, "Directory");
            var contextManager = Find(controller, "ContextManager");
            Assume.That(directory,      Is.Not.Null, "Unity's shortcut directory");
            Assume.That(contextManager, Is.Not.Null, "Unity's shortcut context manager");

            var focusField   = contextManager.GetType().GetField("m_FocusedWindow", kFlags);
            var focus        = (WeakReference)focusField.GetValue(contextManager);
            var focusedBefore = focus.Target;
            focus.Target = focused;
            try
            {
                var combinationType = assembly.GetType("UnityEditor.ShortcutManagement.KeyCombination");
                var entryType       = assembly.GetType("UnityEditor.ShortcutManagement.ShortcutEntry");
                var combinations    = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(combinationType));
                combinations.Add(Activator.CreateInstance(combinationType, key, ShortcutModifiers.None));
                var entries = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(entryType));
                var find    = directory.GetType().GetMethod("FindShortcutEntries", kFlags, null,
                    new[] { combinations.GetType(), assembly.GetType("UnityEditor.ShortcutManagement.IContextManager"), entries.GetType() }, null);
                find.Invoke(directory, new[] { combinations, contextManager, entries });

                var ids = new List<string>();
                foreach (var entry in entries)
                    ids.Add(entryType.GetProperty("identifier", kFlags).GetValue(entry).ToString());
                return ids;
            }
            finally
            {
                focus.Target = focusedBefore;
            }
        }

        static SceneView AnySceneView()
        {
            var sceneView = SceneView.lastActiveSceneView;
            if (!sceneView && SceneView.sceneViews.Count > 0)
                sceneView = (SceneView)SceneView.sceneViews[0];
            Assume.That(sceneView, Is.Not.Null, "a Scene view is open");
            return sceneView;
        }

        void SelectABrush()
        {
            root = new GameObject("Brush");
            root.AddComponent<ChiselBrushComponent>();
            Selection.activeGameObject = root;
        }

        [TestCase(KeyCode.W, "Tools/Move")]
        [TestCase(KeyCode.E, "Tools/Rotate")]
        [TestCase(KeyCode.R, "Tools/Scale")]
        public void InUnitysTools_TheToolKeysAreUnitys(KeyCode key, string unitys)
        {
            SelectABrush();
            ToolManager.SetActiveContext<GameObjectToolContext>();
            Tools.current = Tool.Move;

            Assert.That(Resolve(key, AnySceneView()), Is.EqualTo(new[] { unitys }));
        }

        [TestCase(KeyCode.W, "Tools/Move")]
        [TestCase(KeyCode.E, "Tools/Rotate")]
        [TestCase(KeyCode.R, "Tools/Scale")]
        public void InAnotherChiselTool_TheToolKeysAreUnitys(KeyCode key, string unitys)
        {
            SelectABrush();
            ToolManager.SetActiveContext<ChiselToolsContext>();
            ToolManager.SetActiveTool<ChiselMovePivotTool>();

            Assert.That(Resolve(key, AnySceneView()), Is.EqualTo(new[] { unitys }));
        }

        [TestCase(KeyCode.B)]
        [TestCase(KeyCode.C)]
        [TestCase(KeyCode.K)]
        [TestCase(KeyCode.L)]
        [TestCase(KeyCode.P)]
        [TestCase(KeyCode.N)]
        [TestCase(KeyCode.Alpha3)]
        public void OutsideChiselsTools_TheGeneratorKeysArentChisels(KeyCode key)
        {
            ToolManager.SetActiveContext<GameObjectToolContext>();

            foreach (var id in Resolve(key, AnySceneView()))
                Assert.That(id, Does.Not.StartWith("Chisel/"));
        }

        //        scene view, edit tool, brush -> in use
        [TestCase(true,  true,  true,  true)]
        [TestCase(false, true,  true,  false)]
        [TestCase(true,  false, true,  false)]
        [TestCase(true,  true,  false, false)]
        public void TheVertexHandleKeys_AreInUseWhileABrushIsEdited(bool inSceneView, bool editToolActive, bool brushSelected, bool expected)
        {
            Assert.That(ChiselVertexHandleShortcuts.IsInUse(inSceneView, editToolActive, brushSelected), Is.EqualTo(expected));
        }

        [TestCase(true,  true,  true)]
        [TestCase(false, true,  false)]
        [TestCase(true,  false, false)]
        public void TheGeneratorKeys_AreInUseWithChiselsTools(bool inSceneView, bool chiselTools, bool expected)
        {
            Assert.That(ChiselGeneratorShortcuts.IsInUse(inSceneView, chiselTools), Is.EqualTo(expected));
        }
    }
}
