using System;
using Chisel.Core;
using NUnit.Framework;
using UnityEngine;

namespace Chisel.Components.Tests
{
    [TestFixture]
    public class StagedInputHashTests
    {
        GameObject root;
        ChiselModelComponent model;
        ChiselBoxComponent box;
        Material material;

        [SetUp]
        public void SetUp()
        {
            if (UnityEngine.Object.FindObjectsByType<ChiselModelComponent>(FindObjectsInactive.Include).Length > 0)
                Assert.Ignore("A Chisel model is loaded; run these in a scene without one");

            root  = new GameObject("Model");
            model = root.AddComponent<ChiselModelComponent>();
            box   = AddBox("Box", Vector3.zero);
            material = new Material(Shader.Find("Hidden/InternalErrorShader"));
            Update();
        }

        [TearDown]
        public void TearDown()
        {
            if (root)
                UnityEngine.Object.DestroyImmediate(root);
            if (material)
                UnityEngine.Object.DestroyImmediate(material);
            root = null;
            model = null;
            box = null;
            material = null;
            Update();
        }

        ChiselBoxComponent AddBox(string name, Vector3 localPosition)
        {
            var boxObject = new GameObject(name);
            boxObject.transform.SetParent(root.transform, false);
            boxObject.transform.localPosition = localPosition;
            var added = boxObject.AddComponent<ChiselBoxComponent>();
            added.OnValidate();
            return added;
        }

        static void Update()
        {
            for (int i = 0; i < 3; i++)
            {
                ChiselNodeHierarchyManager.Update();
                ChiselModelManager.Instance.UpdateModels();
            }
        }

        // What the model's meshes are built from, hashed from the tree that was built
        Hash128 Built() { return ChiselModelManager.GetInputHash(model); }

        // The same question asked of the components instead
        Hash128 FromComponents() { return ChiselStagedInputHashes.GetCSGInputHash(model, ChiselModelManager.SurfaceParameterIdentityMethod); }

        void PushToTree()
        {
            if (!root)
                return;
            foreach (var generator in root.GetComponentsInChildren<ChiselGeneratorComponent>(true))
            {
                ChiselNodeHierarchyManager.UpdateTreeNodeTransformation(generator);
                if (!generator.isActiveAndEnabled)
                    continue;
                generator.ClearHashes();
                generator.UpdateGeneratorNodes();
            }
        }

        /// <summary>
        /// Makes the change and requires both hashes to agree about whether it was a change of input. With
        /// <paramref name="mustChange"/> it also requires that it was one, for the changes that plainly are.
        /// </summary>
        void AssertBothAgree(string what, Action change, bool mustChange = true)
        {
            var builtBefore      = Built();
            var componentsBefore = FromComponents();
            Assert.That(builtBefore.isValid, Is.True, "the model has nothing built to hash");
            Assert.That(componentsBefore.isValid, Is.True, "the components hashed to nothing");

            change();
            PushToTree();
            Update();

            var builtChanged      = Built() != builtBefore;
            var componentsChanged = FromComponents() != componentsBefore;
            if (builtChanged != componentsChanged)
            {
                Assert.Fail(builtChanged
                    ? $"{what} changed what is built, but the hash taken from the components stayed the same: it misses this input"
                    : $"{what} changed the hash taken from the components, but not what is built: it hashes something that isn't input");
            }
            if (mustChange)
                Assert.That(builtChanged, Is.True, $"{what} is a change of input, but nothing noticed");
        }

        [Test]
        public void AMovedBrush_IsAChange()
        {
            AssertBothAgree("moving a brush", () => box.transform.localPosition = new Vector3(0.25f, 0, 0));
        }

        [Test]
        public void ARotatedBrush_IsAChange()
        {
            AssertBothAgree("rotating a brush", () => box.transform.localRotation = Quaternion.Euler(0, 30, 0));
        }

        [Test]
        public void AResizedBrush_IsAChange()
        {
            AssertBothAgree("resizing a brush", () =>
            {
                box.definition.settings.Size += new Unity.Mathematics.float3(0.5f, 0, 0);
                box.OnValidate();
            });
        }

        [Test]
        public void ADifferentOperation_IsAChange()
        {
            AssertBothAgree("changing a brush's operation", () => box.Operation = CSGOperationType.Subtractive);
        }

        [Test]
        public void ADifferentPivot_IsAChange()
        {
            AssertBothAgree("moving a brush's pivot", () => box.PivotOffset = new Vector3(0, 0.5f, 0));
        }

        [Test]
        public void ADifferentMaterial_IsAChange()
        {
            AssertBothAgree("giving a surface another material", () => box.SurfaceDefinition.surfaces[0].SetMaterial(material));
        }

        [Test]
        public void ADifferentSurfaceUV_IsAChange()
        {
            AssertBothAgree("moving a surface's UVs", () =>
            {
                var uv0 = box.GetSurfaceUV0(0);
                uv0.U.w += 0.5f;
                box.SetSurfaceUV0(0, uv0);
            });
        }

        [Test]
        public void AnAddedBrush_IsAChange()
        {
            AssertBothAgree("adding a brush", () => AddBox("Second", new Vector3(2, 0, 0)));
        }

        [Test]
        public void ARemovedBrush_IsAChange()
        {
            var second = AddBox("Second", new Vector3(2, 0, 0));
            Update();
            AssertBothAgree("removing a brush", () => UnityEngine.Object.DestroyImmediate(second.gameObject));
        }

        [Test]
        public void ADeactivatedBrush_IsAChange()
        {
            AssertBothAgree("deactivating a brush", () => box.gameObject.SetActive(false));
        }

        [Test]
        public void AReorderedBrush_IsAChange()
        {
            var second = AddBox("Second", new Vector3(2, 0, 0));
            Update();
            AssertBothAgree("reordering two brushes", () => second.transform.SetSiblingIndex(0));
        }

        [Test]
        public void AnAddedDecal_IsAChange()
        {
            AssertBothAgree("adding a decal", () =>
            {
                var decalObject = new GameObject("Decal");
                decalObject.transform.SetParent(root.transform, false);
                decalObject.transform.localPosition = new Vector3(0.5f, 1.0f, 0.5f);
                decalObject.transform.localRotation = Quaternion.Euler(90, 0, 0);
                var decal = decalObject.AddComponent<ChiselDecalComponent>();
                decal.Material = material;
                decal.Size = new Vector3(0.5f, 0.5f, 0.5f);
            });
        }

        [Test]
        public void ADifferentModelSetting_IsAChange()
        {
            AssertBothAgree("changing the model's smoothing angle", () =>
            {
                model.renderSettings.normalSmoothingAngle = 30.0f;
                model.SyncModelSettingsStore();
            });
        }

        // What a brush is made of is an index into the contents list, and a project may only have the one it starts
        // with, so this only requires the two hashes to agree
        [Test]
        public void DifferentContents_AreNoticedByBoth()
        {
            AssertBothAgree("changing what a brush is made of", () => box.Contents = 0, mustChange: false);
        }

        [Test]
        public void ARenamedObject_IsNotAChange()
        {
            AssertBothAgree("renaming a brush", () => box.gameObject.name = "Renamed", mustChange: false);
            Assert.That(Built(), Is.EqualTo(Built()), "the built hash isn't stable");
        }

        [Test]
        public void AMovedModel_IsNotAChange()
        {
            var before = FromComponents();
            AssertBothAgree("moving the model", () => root.transform.position = new Vector3(10, 20, 30), mustChange: false);
            Assert.That(FromComponents(), Is.EqualTo(before), "moving a model changed what it is built from");
        }

        // The same components hash the same way twice, or nothing above means anything
        [Test]
        public void TheSameComponents_HashTheSame()
        {
            var first = FromComponents();
            Update();
            Assert.That(FromComponents(), Is.EqualTo(first));
            Assert.That(first.isValid, Is.True);
        }
    }
}
