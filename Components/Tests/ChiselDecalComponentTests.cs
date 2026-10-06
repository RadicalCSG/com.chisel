using System.Collections.Generic;
using Chisel.Core;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Chisel.Components.Tests
{
    [TestFixture]
    public class ChiselDecalComponentTests
    {
        readonly List<Object> created = new();
        Material decalMaterial;

        [SetUp]
        public void SetUp()
        {
            if (Object.FindObjectsByType<ChiselModelComponent>(FindObjectsInactive.Include).Length > 0)
                Assert.Ignore("A Chisel model is loaded; run these in a scene without one");
            decalMaterial = new Material(Shader.Find("Standard")) { name = "DecalTestMaterial" };
            created.Add(decalMaterial);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var item in created)
            {
                if (item)
                    Object.DestroyImmediate(item);
            }
            created.Clear();
            Update();
        }

        static void Update()
        {
            for (int i = 0; i < 3; i++)
            {
                ChiselNodeHierarchyManager.Update();
                ChiselModelManager.Instance.UpdateModels();
            }
        }

        ChiselModelComponent Floors(out ChiselBoxComponent a, out ChiselBoxComponent b, out ChiselDecalComponent decal)
        {
            var root = new GameObject("decal test");
            created.Add(root);
            var model = root.AddComponent<ChiselModelComponent>();
            a = Box("A", model.transform, new Vector3(-1, -1, -0.5f));
            b = Box("B", model.transform, new Vector3(0, -1, -0.5f));

            var decalObject = new GameObject("Decal");
            decalObject.transform.SetParent(model.transform, false);
            decalObject.transform.rotation = Quaternion.LookRotation(Vector3.down, Vector3.forward);
            decal = decalObject.AddComponent<ChiselDecalComponent>();
            decal.Material    = decalMaterial;
            decal.Size        = new Vector3(1, 1, 0.5f);
            decal.Transparent = false;
            return model;
        }

        static ChiselBoxComponent Box(string name, Transform parent, Vector3 localPosition)
        {
            var gameObject = new GameObject(name);
            gameObject.transform.SetParent(parent, false);
            gameObject.transform.localPosition = localPosition;
            return gameObject.AddComponent<ChiselBoxComponent>();
        }

        // The area of the upward triangles drawn with the decal's material, and of the others
        (double decal, double other) UpAreas(ChiselModelComponent model)
        {
            double decal = 0, other = 0;
            if (model.generated?.renderables == null)
                return (decal, other);
            foreach (var r in model.generated.renderables)
            {
                if (r == null || !r.Valid || r.sharedMesh == null || r.sharedMesh.vertexCount == 0)
                    continue;
                const SurfaceDestinationFlags kDrawsAndCasts = SurfaceDestinationFlags.RenderShadowReceiveAndCasting;
                if ((r.query & kDrawsAndCasts) != kDrawsAndCasts)
                    continue;
                var vertices = r.sharedMesh.vertices;
                for (int s = 0; s < r.sharedMesh.subMeshCount; s++)
                {
                    var isDecal = r.renderMaterials != null && s < r.renderMaterials.Length && r.renderMaterials[s] == decalMaterial;
                    var triangles = r.sharedMesh.GetTriangles(s);
                    for (int t = 0; t < triangles.Length; t += 3)
                    {
                        var normal = Vector3.Cross(vertices[triangles[t + 1]] - vertices[triangles[t]], vertices[triangles[t + 2]] - vertices[triangles[t]]);
                        if (normal.normalized.y < 0.99f)
                            continue;
                        if (isDecal) decal += normal.magnitude * 0.5;
                        else         other += normal.magnitude * 0.5;
                    }
                }
            }
            return (decal, other);
        }

        static ChiselDecalSurfaceTarget Target(ChiselNodeComponent brush, int surface)
        {
            return new ChiselDecalSurfaceTarget { brush = brush, surfaceIndex = surface };
        }

        const double kTolerance = 1e-4;

        [Test]
        public void Decal_IsDrawnOnTheFloors()
        {
            var model = Floors(out _, out _, out _);
            Update();
            Assert.That(model.Node.GetDecalCount(), Is.EqualTo(1));
            var (decal, other) = UpAreas(model);
            Assert.That(decal, Is.EqualTo(1.0).Within(kTolerance));
            Assert.That(other, Is.EqualTo(1.0).Within(kTolerance), "the opaque decal replaces a square metre of the two floors");
        }

        // Rebuild recreates every tree, and a new tree can get the handle the old one had
        [Test]
        public void Rebuild_KeepsTheDecals()
        {
            var model = Floors(out _, out _, out _);
            Update();
            Assert.That(UpAreas(model).decal, Is.EqualTo(1.0).Within(kTolerance));

            ChiselNodeHierarchyManager.Rebuild();
            Update();
            Assert.That(model.Node.GetDecalCount(), Is.EqualTo(1), "the rebuilt tree has the decal again");
            Assert.That(UpAreas(model).decal, Is.EqualTo(1.0).Within(kTolerance));
        }

        [Test]
        public void Targets_LimitTheDecalToTheirSurfaces()
        {
            // A box's top is its surface 0 and its bottom surface 1 (ChiselBoxBrushMeshFactory.BoxSides)
            const int kTop = 0, kBottom = 1;
            var model = Floors(out var a, out var b, out var decal);

            decal.SetTargets(new[] { Target(a, ChiselDecalSurfaceTarget.kAllSurfaces) });
            Update();
            var areas = UpAreas(model);
            Assert.That(areas.decal, Is.EqualTo(0.5).Within(kTolerance), "on A only");
            Assert.That(areas.other, Is.EqualTo(1.5).Within(kTolerance), "B keeps its top");

            decal.SetTargets(new[] { Target(b, kTop) });
            Update();
            Assert.That(UpAreas(model).decal, Is.EqualTo(0.5).Within(kTolerance), "on B's top");

            decal.SetTargets(new[] { Target(b, kBottom) });
            Update();
            areas = UpAreas(model);
            Assert.That(areas.decal, Is.EqualTo(0).Within(kTolerance), "B's bottom is out of reach, and nothing else is a target");
            Assert.That(areas.other, Is.EqualTo(2).Within(kTolerance));

            // A deleted target: nothing is drawn, and once the decal is looked at again it is left out altogether
            decal.SetTargets(new[] { Target(a, ChiselDecalSurfaceTarget.kAllSurfaces) });
            Update();
            Object.DestroyImmediate(a.gameObject);
            Update();
            Assert.That(UpAreas(model).decal, Is.EqualTo(0).Within(kTolerance));
            ChiselDecalManager.SetDirty();
            Update();
            Assert.That(model.Node.GetDecalCount(), Is.EqualTo(0));
            Assert.That(UpAreas(model).other, Is.EqualTo(1).Within(kTolerance), "B's whole top");
        }

        [Test]
        public void ARemovedModel_LeavesNoMeshLoaded_AfterTheUpdateThatFindsItGone()
        {
            ChiselDestroyedModelTests.AssertNoneLoadedAfterAnUnload(DrawAndRemoveFloors());
        }

        // In a method of its own, so that the test refers to nothing of the model when the unload runs
        List<(string what, EntityId id)> DrawAndRemoveFloors()
        {
            var model = Floors(out _, out _, out _);
            Update();
            Assert.That(model.Node.GetDecalCount(), Is.EqualTo(1), "no decal drawn: the decal manager never had the model");
            var meshes = new List<(string what, EntityId id)>();
            ChiselDestroyedModelTests.RecordMeshes(model, meshes);
            Assert.That(meshes.Count, Is.GreaterThan(0), "the model made no meshes: nothing is measured");

            var root = model.gameObject;
            created.Remove(root);
            Object.DestroyImmediate(root);
            // One update: the next one would start by clearing what this one kept
            ChiselNodeHierarchyManager.Update();
            ChiselModelManager.Instance.UpdateModels();
            return meshes;
        }

        [Test]
        public void ARemovedDecal_LeavesItsMaterialUnloadable()
        {
            var material = DrawAndRemoveADecalWithAMaterialOfItsOwn();
            System.GC.Collect();
            System.GC.WaitForPendingFinalizers();
            System.GC.Collect();
            EditorUtility.UnloadUnusedAssetsImmediate(true);
            Assert.That((bool)Resources.EntityIdToObject(material), Is.False, "the removed decal's material is still loaded: something still refers to it");
        }

        // In a method of its own, so that the test refers to nothing of the decal or its material when the unload runs.
        // The material is the test's own and never destroyed: only an unload can take it.
        EntityId DrawAndRemoveADecalWithAMaterialOfItsOwn()
        {
            var material = new Material(Shader.Find("Standard")) { name = "Removed Decal Material" };
            var model = Floors(out _, out _, out var decal);
            decal.Material = material;
            Update();
            Assert.That(model.Node.GetDecalCount(), Is.EqualTo(1), "no decal drawn: the decal manager never had the material");

            var root = model.gameObject;
            created.Remove(root);
            Object.DestroyImmediate(root);
            Update();
            return material.GetEntityId();
        }
    }
}
