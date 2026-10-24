using System.Reflection;
using Chisel.Core;
using NUnit.Framework;
using UnityEngine;

namespace Chisel.Components.Tests
{
    // Entering play mode without a domain reload (Enter Play Mode Options) runs the managers' ResetState methods on
    // statics that are still there; the editor has to go on working after them
    [TestFixture]
    public class ChiselPlayModeResetTests
    {
        (UnityEngine.SceneManagement.Scene scene, bool wasDirty) sceneAtStart;
        GameObject root;

        [SetUp]
        public void SetUp()
        {
            if (Object.FindObjectsByType<ChiselModelComponent>(FindObjectsInactive.Include).Length > 0)
                Assert.Ignore("A Chisel model is loaded; run these in a scene without one");
            sceneAtStart = SceneLeftClean.Remember();
        }

        [TearDown]
        public void TearDown()
        {
            RestorePools();
            if (root)
                Object.DestroyImmediate(root);
            Update();
            ChiselUnityVisibilityManager.UpdateVisibility(force: true);
            SceneLeftClean.Restore(sceneAtStart);
        }

        static void Update()
        {
            for (int i = 0; i < 3; i++)
            {
                ChiselNodeHierarchyManager.Update();
                ChiselModelManager.Instance.UpdateModels();
            }
        }

        // What Unity calls on entering play mode
        static void ResetState(System.Type manager)
        {
            var method = manager.GetMethod("ResetState", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.That(method, Is.Not.Null, manager.Name + ".ResetState");
            try { method.Invoke(null, null); }
            catch (TargetInvocationException e) { throw e.InnerException; }
        }

        // Every generator type's pool registered and allocated again, whatever a reset left of it
        static void RestorePools()
        {
            foreach (var type in typeof(ChiselGeneratorComponent).Assembly.GetTypes())
            {
                if (type.IsAbstract || !typeof(ChiselGeneratorComponent).IsAssignableFrom(type))
                    continue;
                for (var t = type; t != null; t = t.BaseType)
                {
                    if (t.ContainsGenericParameters)
                        continue;
                    var field = t.GetField("s_JobPool", BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.DeclaredOnly);
                    if (field?.GetValue(null) is IGeneratorJobPool pool)
                    {
                        GeneratorJobPoolManager.Register(pool);
                        pool.AllocateOrClear();
                    }
                }
            }
        }

        static int RegisteredPools()
        {
            var instance = typeof(GeneratorJobPoolManager).GetProperty("Instance", BindingFlags.Public | BindingFlags.Static).GetValue(null);
            var pools = typeof(GeneratorJobPoolManager).GetField("generatorPools", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(instance);
            return (int)pools.GetType().GetProperty("Count").GetValue(pools);
        }

        // Everything the model draws
        static Bounds Drawn(ChiselModelComponent model)
        {
            var bounds = default(Bounds);
            var any = false;
            foreach (var r in model.generated.renderables)
            {
                if (r == null || !r.Valid || r.sharedMesh == null || r.sharedMesh.vertexCount == 0)
                    continue;
                if (any) bounds.Encapsulate(r.sharedMesh.bounds); else bounds = r.sharedMesh.bounds;
                any = true;
            }
            return bounds;
        }

        ChiselModelComponent Model(out ChiselBoxComponent box)
        {
            root = new GameObject("play mode reset test");
            var model = root.AddComponent<ChiselModelComponent>();
            // generators of three types, with a pool per type
            box = new GameObject("Box").AddComponent<ChiselBoxComponent>();
            box.transform.SetParent(root.transform, false);
            var cylinder = new GameObject("Cylinder").AddComponent<ChiselCylinderComponent>();
            cylinder.transform.SetParent(root.transform, false);
            cylinder.transform.localPosition = new Vector3(4, 0, 0);
            var stairs = new GameObject("Stairs").AddComponent<ChiselLinearStairsComponent>();
            stairs.transform.SetParent(root.transform, false);
            stairs.transform.localPosition = new Vector3(-4, 0, 0);
            Update();
            return model;
        }

        [Test]
        public void ResettingTheGeneratorPools_DoesNotThrow()
        {
            Model(out _);
            Assert.That(RegisteredPools(), Is.GreaterThanOrEqualTo(2), "one pool can't show a pool unregistering itself while they are gone through");
            Assert.DoesNotThrow(() => ResetState(typeof(GeneratorJobPoolManager)));
        }

        [Test]
        public void ResettingTheGeneratorPools_KeepsThemRegistered()
        {
            Model(out _);
            var pools = RegisteredPools();
            try { ResetState(typeof(GeneratorJobPoolManager)); } catch { }
            Assert.That(RegisteredPools(), Is.EqualTo(pools), "a pool that isn't registered never runs again");
        }

        [Test]
        public void AfterResettingTheGeneratorPools_AnEditedGeneratorIsBuilt()
        {
            var model = Model(out var box);
            Assert.That(Drawn(model).size.x, Is.GreaterThan(0), "the model draws nothing to start with");
            try { ResetState(typeof(GeneratorJobPoolManager)); } catch { }

            var min = box.Min;
            min.y -= 10;
            box.Min = min;
            Update();
            Assert.That(Drawn(model).min.y, Is.EqualTo(min.y).Within(1e-3f), "the box grew 10 m downwards");
        }

        // Its lookup tables don't exist yet after a domain reload, until visibility is first updated
        [Test]
        public void ResettingTheVisibilityManager_BeforeItsTablesExist_DoesNotThrow()
        {
            typeof(ChiselUnityVisibilityManager).GetMethod("OnBeforeAssemblyReload", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, null);
            Assert.DoesNotThrow(() => ResetState(typeof(ChiselUnityVisibilityManager)));
        }
    }
}
