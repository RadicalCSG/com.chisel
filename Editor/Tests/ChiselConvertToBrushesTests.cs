using System.Collections;
using System.Collections.Generic;
using Chisel.Components;
using Chisel.Core;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.TestTools;

namespace Chisel.Editors.Tests
{
    [TestFixture]
    public sealed class ChiselConvertToBrushesTests
    {
        readonly List<Object> created = new();
        readonly HashSet<GameObject> rootsBefore = new();
        UnityEngine.SceneManagement.Scene scene;
        bool wasDirty;
        int undoGroup;

        // Converting moves the generator's object and can make a default model for it, as undo steps. The test runner
        // reverts a run's undo steps when the run ends, which would bring back what a test destroyed, so a test's own
        // steps are reverted first; roots a test still added are taken out, and a scene that was clean is marked clean
        [SetUp]
        public void SetUp()
        {
            scene    = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            wasDirty = scene.isDirty;
            rootsBefore.Clear();
            rootsBefore.UnionWith(scene.GetRootGameObjects());
            UnityEditor.Undo.IncrementCurrentGroup();
            undoGroup = UnityEditor.Undo.GetCurrentGroup();
        }

        [TearDown]
        public void TearDown()
        {
            UnityEditor.Undo.RevertAllDownToGroup(undoGroup);
            for (int i = created.Count - 1; i >= 0; i--)
            {
                if (created[i]) Object.DestroyImmediate(created[i]);
            }
            created.Clear();
            if (scene.IsValid() && scene.isLoaded)
            {
                foreach (var root in scene.GetRootGameObjects())
                {
                    if (!rootsBefore.Contains(root))
                        Object.DestroyImmediate(root);
                }
                UpdateModels();
                if (!wasDirty && scene.isDirty)
                    typeof(UnityEditor.SceneManagement.EditorSceneManager)
                        .GetMethod("ClearSceneDirtiness", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic,
                                   null, new[] { typeof(UnityEngine.SceneManagement.Scene) }, null)
                        ?.Invoke(null, new object[] { scene });
            }
        }

        static void UpdateModels()
        {
            for (int i = 0; i < 3; i++)
            {
                ChiselNodeHierarchyManager.Update();
                ChiselModelManager.Instance.UpdateModels();
            }
        }

        static UVMatrix[] UVsOf(CSGTreeBrush brush)
        {
            ref var mesh = ref BrushMeshManager.GetBrushMeshBlob(brush.BrushMesh).Value;
            var uvs = new UVMatrix[mesh.polygons.Length];
            for (int p = 0; p < uvs.Length; p++)
                uvs[p] = mesh.polygons[p].surface.details.UV0;
            return uvs;
        }

        // A generator turned into brushes keeps its faces' uvs as the generator made them (a guard rail's run on from piece
        // to piece), not its surfaces' own, which are only where the generator starts from
        [UnityTest]
        public IEnumerator SpiralStairs_ConvertedToBrushes_KeepTheirUVs()
        {
            var model = new GameObject("Model").AddComponent<ChiselModelComponent>();
            created.Add(model.gameObject);
            var stairs = new GameObject("Stairs").AddComponent<ChiselSpiralStairsComponent>();
            stairs.transform.SetParent(model.transform, false);
            stairs.definition.settings.outerSide = StairsSideType.DownAndUp;
            stairs.OnValidate();
            UpdateModels();

            var brushes = new List<CSGTreeBrush>();
            ChiselGeneratorParts.BrushesOf(stairs, brushes);
            var made = new List<UVMatrix[]>();
            foreach (var brush in brushes)
                made.Add(UVsOf(brush));
            var root = stairs.gameObject;
            // converting logs nothing: the brushes made under the inactive object while converting are not put in place
            var errors = new List<string>();
            void Logged(string message, string stackTrace, LogType type)
            {
                if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
                    errors.Add(message);
            }
            Application.logMessageReceived += Logged;
            try
            {
                Assert.That(ConvertToBrushesButton.ConvertToBrushes(stairs), Is.True);
                // the brushes are updated by the editor's own updates after converting
                for (int frame = 0; frame < 5; frame++)
                    yield return null;
            }
            finally
            {
                Application.logMessageReceived -= Logged;
            }
            Assert.That(errors, Is.Empty, "errors while converting");

            var converted = root.GetComponentsInChildren<ChiselBrushComponent>();
            Assert.That(converted.Length, Is.EqualTo(made.Count), "a brush for each");
            var compared = 0;
            for (int b = 0; b < converted.Length; b++)
            {
                var surfaces = converted[b].surfaceArray.surfaces;
                Assert.That(surfaces.Length, Is.EqualTo(made[b].Length), $"brush {b}: its faces");
                for (int p = 0; p < surfaces.Length; p++)
                {
                    var a = (float4x4)surfaces[p].surfaceDetails.UV0.ToFloat4x4();
                    var e = (float4x4)made[b][p].ToFloat4x4();
                    Assert.That(math.length(a.c0 - e.c0) + math.length(a.c1 - e.c1) + math.length(a.c3 - e.c3), Is.LessThan(1e-4f), $"brush {b}, face {p}");
                    compared++;
                }
            }
            Assert.That(compared, Is.GreaterThan(0));
        }

        // A generator of one brush (a cylinder, though it is made of several when warped) converts to that brush, and
        // the undo step says so
        [Test]
        public void ACylinder_ConvertedToBrushes_IsOneBrush()
        {
            var model = new GameObject("Model").AddComponent<ChiselModelComponent>();
            created.Add(model.gameObject);
            var cylinder = new GameObject("Cylinder").AddComponent<ChiselCylinderComponent>();
            cylinder.transform.SetParent(model.transform, false);
            UpdateModels();

            UnityEditor.Undo.IncrementCurrentGroup();
            var root = cylinder.gameObject;
            Assert.That(ConvertToBrushesButton.ConvertToBrushes(cylinder), Is.True);
            Assert.That(root.GetComponentsInChildren<ChiselBrushComponent>().Length, Is.EqualTo(1), "one brush");
            Assert.That(UnityEditor.Undo.GetCurrentGroupName(), Is.EqualTo("Converted Cylinder to brush"));
        }
    }
}
