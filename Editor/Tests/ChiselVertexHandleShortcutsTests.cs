using Chisel.Components;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.ShortcutManagement;
using UnityEngine;

namespace Chisel.Editors.Tests
{
    // While a brush is edited, Unity's Move, Rotate and Scale keys pick the handle on its selected vertices; with no brush
    // selected they pick Unity's tool, as they would without Chisel
    public sealed class ChiselVertexHandleShortcutsTests
    {
        GameObject root;
        Object[]   previousSelection;
        Tool       previousTool;
        Tool       previousHandle;

        [SetUp]
        public void SetUp()
        {
            previousSelection = Selection.objects;
            previousTool      = Tools.current;
            previousHandle    = ChiselEditableOutline.VertexHandle;
        }

        [TearDown]
        public void TearDown()
        {
            Selection.objects = previousSelection;
            if (Tools.current != previousTool)
                Tools.current = previousTool;
            ChiselEditableOutline.VertexHandle = previousHandle;
            if (root)
                Object.DestroyImmediate(root);
        }

        [TestCase(ChiselVertexHandleShortcuts.kMoveId,   "W")]
        [TestCase(ChiselVertexHandleShortcuts.kRotateId, "E")]
        [TestCase(ChiselVertexHandleShortcuts.kScaleId,  "R")]
        public void TheKeys_AreUnitysToolKeys(string id, string key)
        {
            Assert.That(ShortcutManager.instance.GetShortcutBinding(id).ToString(), Is.EqualTo(key));
        }

        [TestCase(Tool.Rotate)]
        [TestCase(Tool.Scale)]
        [TestCase(Tool.Move)]
        public void WithABrushSelected_TheKeyPicksTheVertexHandle(Tool tool)
        {
            root = new GameObject("Brush");
            root.AddComponent<ChiselBrushComponent>();
            Selection.activeGameObject = root;
            ChiselEditableOutline.VertexHandle = tool == Tool.Move ? Tool.Scale : Tool.Move;
            var toolBefore = Tools.current;

            ChiselVertexHandleShortcuts.Pick(tool);

            Assert.That(ChiselEditableOutline.VertexHandle, Is.EqualTo(tool));
            Assert.That(Tools.current, Is.EqualTo(toolBefore), "Unity's tool was switched");
        }

        [Test]
        public void WithoutABrushSelected_TheKeyPicksUnitysTool()
        {
            root = new GameObject("Not a brush");
            Selection.activeGameObject = root;
            Tools.current = Tool.Move;

            ChiselVertexHandleShortcuts.Pick(Tool.Rotate);

            Assert.That(Tools.current, Is.EqualTo(Tool.Rotate));
        }

        // without the edit tool active the keys are left to Unity
        [Test]
        public void WithoutTheEditTool_TheKeysAreInactive()
        {
            if (ChiselEditGeneratorTool.IsActive())
                Assert.Ignore("the edit tool is active");
            Assert.That(ChiselVertexHandleShortcuts.TagRegistered, Is.False);
        }
    }
}
