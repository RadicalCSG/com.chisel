using System.Collections.Generic;
using Chisel.Components;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Chisel.Editors.Tests
{
    [TestFixture]
#if UNITY_6000_7_OR_NEWER
    [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
#endif
    public class ChiselSurfaceShapeToolTests
    {
        GameObject root;

        [SetUp]
        public void SetUp()
        {
            root = new GameObject("surface shape test");
            root.AddComponent<ChiselModelComponent>();
        }

        [TearDown]
        public void TearDown()
        {
            if (root)
                Object.DestroyImmediate(root);
        }

        static ChiselDecalComponent DecalOf(ChiselBoxComponent box) => box.GetComponentInChildren<ChiselDecalComponent>(true);

        ChiselBoxComponent Floor()
        {
            var floor = new GameObject("floor");
            floor.transform.SetParent(root.transform, false);
            floor.transform.localPosition = new Vector3(0, -0.5f, 0);
            return floor.AddComponent<ChiselBoxComponent>();
        }

        static readonly List<Vector3> kSquare = new()
        {
            new Vector3(-0.25f, 0, -0.25f), new Vector3(0.25f, 0, -0.25f), new Vector3(0.25f, 0, 0.25f), new Vector3(-0.25f, 0, 0.25f)
        };

        // Every shape can be chosen in the inspector; Polygon is a projection only a polygon decal is given
        [Test]
        public void Inspector_OffersEveryShape_AndNoPolygonProjection()
        {
            Assert.That(ChiselDecalEditor.kShapeOptions.Length, Is.EqualTo(System.Enum.GetValues(typeof(ChiselDecalShape)).Length));
            Assert.That(ChiselDecalEditor.kShapeOptions[(int)ChiselDecalShape.Polygon].text, Is.EqualTo("Polygon"));
            Assert.That(ChiselDecalEditor.kProjectionOptions.Length, Is.EqualTo((int)Chisel.Core.ChiselDecalProjection.Polygon));
        }

        // The target is saved with the scene: what the serialized object holds, not only the field
        [Test]
        public void DrawnShape_KeepsItsBrushAsTarget()
        {
            var floor = Floor();
            var decal = ChiselSurfaceShapeTool.Create(floor, 0, null, Vector3.up, kSquare);
            using var serialized = new SerializedObject(decal);
            var brush = serialized.FindProperty("targets").GetArrayElementAtIndex(0).FindPropertyRelative("brush");
            Assert.That(brush.objectReferenceValue, Is.EqualTo(floor));
        }

        const string kFolder     = "Assets/__ChiselSurfaceShapeTests";
        const string kPrefabPath = kFolder + "/surface shape.prefab";

        // Written to a file and read back, as saving the scene does: the target is a reference within what is saved
        [Test]
        public void DrawnShape_KeepsItsTarget_WhenSaved()
        {
            if (!AssetDatabase.IsValidFolder(kFolder))
                AssetDatabase.CreateFolder("Assets", kFolder.Substring("Assets/".Length));
            try
            {
                var floor = Floor();
                ChiselSurfaceShapeTool.Create(floor, 0, null, Vector3.up, kSquare);
                PrefabUtility.SaveAsPrefabAsset(root, kPrefabPath, out var saved);
                Assert.That(saved, Is.True);

                var loaded = AssetDatabase.LoadAssetAtPath<GameObject>(kPrefabPath);
                var decal  = loaded.GetComponentInChildren<ChiselDecalComponent>(true);
                Assert.That(decal, Is.Not.Null);
                Assert.That(decal.Targets[0].brush, Is.Not.Null, "the target's brush is kept");
                Assert.That(decal.Targets[0].brush, Is.EqualTo(decal.transform.parent.GetComponent<ChiselNodeComponent>()));
            }
            finally
            {
                AssetDatabase.DeleteAsset(kFolder);
            }
        }

        // Extruding replaces the shape with a brush, and one undo brings the shape back
        [Test]
        public void ExtrudedShape_IsOneUndo()
        {
            var floor = Floor();
            var decal = ChiselSurfaceShapeTool.Create(floor, 0, null, Vector3.up, kSquare);
            Undo.IncrementCurrentGroup();
            var selection = Selection.objects;
            try
            {
                var extruded = ChiselDecalEditor.ExtrudeIntoBrush(decal, 0.5f);
                Undo.IncrementCurrentGroup();
                Assert.That(extruded, Is.Not.Null);
                Assert.That(DecalOf(floor), Is.Null, "the shape is the brush now");

                Undo.PerformUndo();
                Assert.That(DecalOf(floor), Is.Not.Null, "undo brings the shape back");
                Assert.That(root.GetComponentInChildren<ChiselExtrudedShapeComponent>(true), Is.Null, "and takes the brush away");
            }
            finally
            {
                Selection.objects = selection;
            }
        }

        [Test]
        public void DrawnShape_IsUndoneAndRedone()
        {
            var floor = Floor();
            Undo.IncrementCurrentGroup();
            ChiselSurfaceShapeTool.Create(floor, 0, null, Vector3.up, kSquare);
            Undo.IncrementCurrentGroup();
            Assert.That(DecalOf(floor), Is.Not.Null);

            Undo.PerformUndo();
            Assert.That(DecalOf(floor), Is.Null, "undo takes the shape away");

            Undo.PerformRedo();
            var decal = DecalOf(floor);
            Assert.That(decal, Is.Not.Null, "redo brings it back");
            Assert.That(decal.Targets[0].brush, Is.EqualTo(floor), "with its target");
            Assert.That(decal.Polygon.Count, Is.EqualTo(kSquare.Count), "and its outline");
        }
    }
}
