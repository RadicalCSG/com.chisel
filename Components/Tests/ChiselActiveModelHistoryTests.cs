using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Chisel.Components.Tests
{
    // When the active model is deleted or disabled, the model that was active before it becomes active again
    [TestFixture]
    public class ChiselActiveModelHistoryTests
    {
        readonly List<GameObject> roots = new();
        (Scene scene, bool wasDirty) remembered;
        ChiselModelComponent previousActive;

        [SetUp]
        public void SetUp()
        {
            remembered = SceneLeftClean.Remember();
            previousActive = ChiselModelManager.Instance.ActiveModel;
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var root in roots)
            {
                if (root)
                    Object.DestroyImmediate(root);
            }
            roots.Clear();
            if (previousActive)
                ChiselModelManager.Instance.ActiveModel = previousActive;
            SceneLeftClean.Restore(remembered);
        }

        ChiselModelComponent Model(string name)
        {
            var model = new GameObject(name).AddComponent<ChiselModelComponent>();
            roots.Add(model.gameObject);
            return model;
        }

        // a, b, and c last at the scene root, which is where a model is looked for when there is no history
        (ChiselModelComponent a, ChiselModelComponent b, ChiselModelComponent c) Models()
        {
            return (Model("a"), Model("b"), Model("c"));
        }

        [Test]
        public void DeletingTheActiveModel_GoesBackToTheOneBefore()
        {
            var (a, b, _) = Models();
            ChiselModelManager.Instance.ActiveModel = a;
            ChiselModelManager.Instance.ActiveModel = b;

            Object.DestroyImmediate(b.gameObject);
            Assert.That(ChiselModelManager.Instance.ActiveModel, Is.SameAs(a));
        }

        [Test]
        public void DisablingTheActiveModel_GoesBackToTheOneBefore_UntilItIsEnabledAgain()
        {
            var (a, b, _) = Models();
            ChiselModelManager.Instance.ActiveModel = a;
            ChiselModelManager.Instance.ActiveModel = b;

            b.gameObject.SetActive(false);
            Assert.That(ChiselModelManager.Instance.ActiveModel, Is.SameAs(a));

            b.gameObject.SetActive(true);
            Assert.That(ChiselModelManager.Instance.ActiveModel, Is.SameAs(b));
        }

        [Test]
        public void MakingAModelActiveAgain_MovesItToTheFront()
        {
            var (a, b, _) = Models();
            ChiselModelManager.Instance.ActiveModel = a;
            ChiselModelManager.Instance.ActiveModel = b;
            ChiselModelManager.Instance.ActiveModel = a;

            Object.DestroyImmediate(a.gameObject);
            Assert.That(ChiselModelManager.Instance.ActiveModel, Is.SameAs(b));
        }

        // With no model left in the history, another model in the scene is used, not none
        [Test]
        public void WithNoModelLeftInTheHistory_AnotherModelInTheSceneIsUsed()
        {
            var (a, b, c) = Models();
            ChiselModelManager.Instance.ActiveModel = a;
            Object.DestroyImmediate(a.gameObject);
            Object.DestroyImmediate(b.gameObject);
            Assert.That(ChiselModelManager.Instance.ActiveModel, Is.SameAs(c));
        }
    }
}
