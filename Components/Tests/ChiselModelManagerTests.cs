using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace Chisel.Components.Tests
{
    [TestFixture]
    public class ChiselModelManagerTests
    {
        ChiselModelManager manager;
        GameObject gameObject;

        [SetUp]
        public void SetUp()
        {
            manager = ScriptableObject.CreateInstance<ChiselModelManager>();
            manager.hideFlags = HideFlags.HideAndDontSave;
            gameObject = new GameObject(nameof(ChiselModelManagerTests));
            gameObject.SetActive(false); // so the model never enables, and never registers itself
        }

        [TearDown]
        public void TearDown()
        {
            if (gameObject)
                Object.DestroyImmediate(gameObject);
            if (manager)
                Object.DestroyImmediate(manager);
        }

        [Test]
        public void Reset_ForgetsAModelThatWasDestroyedBeforeItCouldBeUnregistered()
        {
            var model = gameObject.AddComponent<ChiselModelComponent>();
            manager.Register(model);
            Assert.That(manager.Models, Does.Contain(model));

            Object.DestroyImmediate(gameObject);
            manager.Reset();

            Assert.That(manager.Models, Is.Empty);
            Assert.That(manager.Nodes, Is.Empty);
        }

        // Live nodes are forgotten as well; the node hierarchy registers them again right after its reset.
        [Test]
        public void Reset_StartsOverForLiveNodesToo()
        {
            var model = gameObject.AddComponent<ChiselModelComponent>();
            manager.Register(model);

            manager.Reset();
            Assert.That(manager.Models, Is.Empty);

            manager.Register(model);
            Assert.That(manager.Models, Does.Contain(model));
        }

        [Test]
        public void Instance_FindsTheManagerThereIs_AfterADomainReload()
        {
            var inUse = ChiselModelManager.Instance;
            var instanceField = typeof(ChiselModelManager).GetField("_instance", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.That(instanceField, Is.Not.Null, "ChiselModelManager._instance is gone: this test no longer looks at it");
            DestroyManagersBut(inUse);
            instanceField.SetValue(null, null);     // all a domain reload does to the manager
            try
            {
                var found = ChiselModelManager.Instance;
                Assert.That(ReferenceEquals(found, inUse), Is.True, "Instance made a manager of its own instead of finding the one there is");
                Assert.That(Resources.FindObjectsOfTypeAll<ChiselModelManager>(), Has.Length.EqualTo(1), "one manager");
            }
            finally
            {
                instanceField.SetValue(null, inUse);
                DestroyManagersBut(inUse);
            }
        }

        static void DestroyManagersBut(ChiselModelManager inUse)
        {
            foreach (var other in Resources.FindObjectsOfTypeAll<ChiselModelManager>())
            {
                if (!ReferenceEquals(other, inUse))
                    Object.DestroyImmediate(other);
            }
        }
    }
}
