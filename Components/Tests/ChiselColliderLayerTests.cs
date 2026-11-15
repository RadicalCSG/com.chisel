using System.Collections.Generic;
using Chisel.Core;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;

namespace Chisel.Components.Tests
{
    // A material can put the colliders of its surfaces on a layer of its own (its Chisel Surface Parameters): colliders
    // are split by layer as well as by physics material, each layer's on a GameObject on that layer
    [TestFixture]
    public class ChiselColliderLayerTests
    {
        const string kFolder = "Assets/__ChiselColliderLayerTests";
        const int kWaterLayer = 4;  // one of Unity's own layers, always there

        readonly List<GameObject> roots = new();

        [SetUp]
        public void SetUp()
        {
            if (Object.FindObjectsByType<ChiselModelComponent>(FindObjectsInactive.Include).Length > 0)
                Assert.Ignore("A Chisel model is loaded; run these in a scene without one");
            if (!AssetDatabase.IsValidFolder(kFolder))
                AssetDatabase.CreateFolder("Assets", kFolder.Substring("Assets/".Length));
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
            Update();
            AssetDatabase.DeleteAsset(kFolder);
        }

        static void Update()
        {
            for (int i = 0; i < 3; i++)
            {
                ChiselNodeHierarchyManager.Update();
                ChiselModelManager.Instance.UpdateModels();
            }
        }

        // A material saved as an asset (only an asset can have Chisel Surface Parameters) whose colliders go on a layer
        static ChiselSurfaceMetadata MaterialOnLayer(string name, int layer, out Material material)
        {
            material = new Material(ChiselProjectSettings.DefaultWallMaterial);
            AssetDatabase.CreateAsset(material, $"{kFolder}/{name}.mat");
            var metadata = material.AddMetadataOfType<ChiselSurfaceMetadata>();
            metadata.colliderLayer = layer;
            return metadata;
        }

        // A box with its faces on the given materials, in turn
        (ChiselModelComponent, ChiselBoxComponent) ModelWithABoxOf(params Material[] materials)
        {
            var root = new GameObject("Collider layer model");
            roots.Add(root);
            var model = root.AddComponent<ChiselModelComponent>();
            var boxObject = new GameObject("Box");
            boxObject.transform.SetParent(root.transform, false);
            var box = boxObject.AddComponent<ChiselBoxComponent>();
            box.Min = new float3(0, 0, 0);
            box.Max = new float3(1, 1, 1);
            Update();
            var surfaces = box.SurfaceDefinition.surfaces;
            for (int i = 0; i < surfaces.Length; i++)
                surfaces[i].SetMaterial(materials[i % materials.Length]);
            box.OnValidate();
            Update();
            return (model, box);
        }

        // A box of one material in the model
        static ChiselBoxComponent AddBox(ChiselModelComponent model, float3 min, float3 max, Material material)
        {
            var boxObject = new GameObject("Box");
            boxObject.transform.SetParent(model.transform, false);
            var box = boxObject.AddComponent<ChiselBoxComponent>();
            box.Min = min;
            box.Max = max;
            Update();
            foreach (var surface in box.SurfaceDefinition.surfaces)
                surface.SetMaterial(material);
            box.OnValidate();
            Update();
            return box;
        }

        ChiselModelComponent EmptyModel()
        {
            var root = new GameObject("Collider layer model");
            roots.Add(root);
            return root.AddComponent<ChiselModelComponent>();
        }

        // The layers the model's colliders are on, with their vertex count on each
        static Dictionary<int, int> VerticesByLayer(ChiselModelComponent model)
        {
            var byLayer = new Dictionary<int, int>();
            foreach (var collider in model.generated.colliders)
            {
                if (collider == null || !collider.meshCollider || collider.sharedMesh.vertexCount == 0)
                    continue;
                var layer = collider.meshCollider.gameObject.layer;
                byLayer.TryGetValue(layer, out var count);
                byLayer[layer] = count + collider.sharedMesh.vertexCount;
            }
            return byLayer;
        }

        [Test]
        public void SurfacesOnTwoLayers_GetCollidersOnBothLayers()
        {
            MaterialOnLayer("water", kWaterLayer, out var water);
            MaterialOnLayer("plain", SurfaceDestinationParameters.kNoColliderLayer, out var plain);
            var (model, _) = ModelWithABoxOf(water, plain);

            var byLayer = VerticesByLayer(model);
            Assert.That(byLayer.Keys, Is.EquivalentTo(new[] { model.gameObject.layer, kWaterLayer }));
            foreach (var collider in model.generated.colliders)
            {
                var onWater = collider.meshCollider.gameObject.layer == kWaterLayer;
                Assert.That(collider.meshCollider.gameObject == model.generated.colliderContainer, Is.Not.EqualTo(onWater),
                            "the model's layer on the collider container, another on a GameObject of its own");
            }
        }

        // Without a layer the colliders are as they were: all on the collider container, on the model's layer
        [Test]
        public void SurfacesWithoutALayer_GetTheSameCollidersAsBefore()
        {
            MaterialOnLayer("plain", SurfaceDestinationParameters.kNoColliderLayer, out var plain);
            var withMetadata = ModelWithABoxOf(plain).Item1;
            var withoutMetadata = ModelWithABoxOf(ChiselProjectSettings.DefaultWallMaterial).Item1;

            foreach (var model in new[] { withMetadata, withoutMetadata })
            {
                Assert.That(model.generated.colliders, Is.Not.Empty);
                foreach (var collider in model.generated.colliders)
                    Assert.That(collider.meshCollider.gameObject, Is.SameAs(model.generated.colliderContainer));
                Assert.That(model.generated.colliderLayerContainers == null ||
                            System.Array.TrueForAll(model.generated.colliderLayerContainers, c => !c), "no layer of its own");
            }
            Assert.That(VerticesByLayer(withMetadata), Is.EqualTo(VerticesByLayer(withoutMetadata)));
        }

        // Changing a material's layer moves its colliders there, and back; a layer no collider is on any more goes
        [Test]
        public void ChangingTheLayer_MovesTheColliders()
        {
            var metadata = MaterialOnLayer("water", SurfaceDestinationParameters.kNoColliderLayer, out var water);
            var (model, box) = ModelWithABoxOf(water);
            var allVertices = VerticesByLayer(model)[model.gameObject.layer];

            metadata.colliderLayer = kWaterLayer;
            box.OnValidate();
            Update();
            Assert.That(VerticesByLayer(model), Is.EqualTo(new Dictionary<int, int> { { kWaterLayer, allVertices } }));

            metadata.colliderLayer = SurfaceDestinationParameters.kNoColliderLayer;
            box.OnValidate();
            Update();
            Assert.That(VerticesByLayer(model), Is.EqualTo(new Dictionary<int, int> { { model.gameObject.layer, allVertices } }));
            Assert.That(System.Array.TrueForAll(model.generated.colliderLayerContainers, c => !c), "the water layer's container is gone");
        }

        // What a ray down onto a point hits, with only these layers
        static bool HitsDown(float3 point, int layerMask, out RaycastHit hit)
        {
            Physics.SyncTransforms();
            return Physics.Raycast(new Vector3(point.x, point.y + 5, point.z), Vector3.down, out hit, 10, layerMask);
        }

        // Physics with a layer mask finds a surface's colliders on its layer, and only there
        [Test]
        public void ARaycastWithALayerMask_HitsOnlyTheCollidersOnThatLayer()
        {
            MaterialOnLayer("water", kWaterLayer, out var water);
            MaterialOnLayer("plain", SurfaceDestinationParameters.kNoColliderLayer, out var plain);
            var model = EmptyModel();
            AddBox(model, new float3(0, 0, 0), new float3(1, 1, 1), water);
            AddBox(model, new float3(3, 0, 0), new float3(4, 1, 1), plain);
            var waterMask = 1 << kWaterLayer;
            var modelMask = 1 << model.gameObject.layer;
            var onWater = new float3(0.5f, 0, 0.5f);
            var onPlain = new float3(3.5f, 0, 0.5f);

            Assert.That(HitsDown(onWater, waterMask, out var hit), Is.True, "the water box, on the water layer");
            Assert.That(hit.point.y, Is.EqualTo(1).Within(1e-3f), "its top");
            Assert.That(HitsDown(onWater, modelMask, out _), Is.False, "the water box isn't on the model's layer");
            Assert.That(HitsDown(onPlain, waterMask, out _), Is.False, "the plain box isn't on the water layer");
            Assert.That(HitsDown(onPlain, modelMask, out hit), Is.True, "the plain box, on the model's layer");
            Assert.That(hit.point.y, Is.EqualTo(1).Within(1e-3f), "its top");
        }

        // An edit elsewhere keeps the colliders it didn't touch as they are, the ones on a layer too: they are found by
        // their layer as well as their physics material and chunk
        [Test]
        public void AnEditElsewhere_KeepsTheCollidersOnALayer()
        {
            MaterialOnLayer("water", kWaterLayer, out var water);
            MaterialOnLayer("plain", SurfaceDestinationParameters.kNoColliderLayer, out var plain);
            var model = EmptyModel();
            AddBox(model, new float3(0, 0, 0), new float3(1, 1, 1), water);
            // far enough to be in another chunk of space (PrepareSubSections.ChunkOf)
            var far = AddBox(model, new float3(40, 0, 0), new float3(41, 1, 1), plain);

            var onWater = System.Array.Find(model.generated.colliders, c => c != null && c.colliderLayer > 0);
            Assume.That(onWater, Is.Not.Null, "the water box's collider");
            var meshCollider = onWater.meshCollider;
            var mesh = onWater.sharedMesh;

            far.Max = new float3(41, 2, 1);
            far.OnValidate();
            Update();

            Assert.That(model.generated.colliders, Has.Member(onWater), "the same collider");
            Assert.That(onWater.meshCollider, Is.SameAs(meshCollider));
            Assert.That(onWater.sharedMesh, Is.SameAs(mesh), "its mesh, not built again");
            Assert.That(onWater.meshCollider.gameObject.layer, Is.EqualTo(kWaterLayer));
            Assert.That(HitsDown(new float3(40.5f, 0, 0.5f), 1 << model.gameObject.layer, out var hit) && hit.point.y > 1.5f, Is.True,
                        "the edited box was built again");
        }
    }
}
