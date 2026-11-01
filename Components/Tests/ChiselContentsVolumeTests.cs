using System.Collections.Generic;
using Chisel.Core;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;

namespace Chisel.Components.Tests
{
    // The contents at a point, as the CSG decides them, kept with the model's meshes so a player can ask without the CSG
    [TestFixture]
    public class ChiselContentsVolumeTests
    {
        const int kWater = 3;
        static readonly string[] kNames = { ChiselContentsList.kSolidName, "Glass", "Grate", "Water" };

        readonly List<Object> created = new();
        ChiselContentsList previousList;

        [SetUp]
        public void SetUp()
        {
            if (Object.FindObjectsByType<ChiselModelComponent>(FindObjectsInactive.Include).Length > 0)
                Assert.Ignore("A Chisel model is loaded; run these in a scene without one");
            previousList = ChiselContentsList.Instance;
            var list = ChiselContentsList.Create(kNames);
            created.Add(list);
            ChiselContentsList.Instance = list;
            CompactHierarchyManager.ApplyContentsList();
        }

        [TearDown]
        public void TearDown()
        {
            ChiselContentsList.Instance = previousList;
            CompactHierarchyManager.ApplyContentsList();
            foreach (var obj in created)
            {
                if (obj)
                    Object.DestroyImmediate(obj);
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

        ChiselModelComponent Model(Vector3 position = default)
        {
            var root = new GameObject("contents test");
            created.Add(root);
            root.transform.position = position;
            return root.AddComponent<ChiselModelComponent>();
        }

        static ChiselBoxComponent Box(ChiselModelComponent model, float3 min, float3 max, int contents = ChiselContentsList.kSolidIndex,
                                      CSGOperationType operation = CSGOperationType.Additive)
        {
            var go = new GameObject("box");
            go.transform.SetParent(model.transform, false);
            var box = go.AddComponent<ChiselBoxComponent>();
            box.Max = new float3(1e4f);
            box.Min = min;
            box.Max = max;
            box.Contents  = contents;
            box.Operation = operation;
            return box;
        }

        [Test]
        public void InsideAWaterBrush_IsWater_AndOutsideIsNothing()
        {
            var model = Model();
            Box(model, new float3(0, 0, 0), new float3(2, 2, 2), kWater);
            Update();
            Assert.That(ChiselContentsVolume.ContentsAt(new Vector3(1, 1, 1)), Is.EqualTo(kWater), "inside");
            Assert.That(ChiselContentsVolume.ContentsAt(new Vector3(3, 1, 1)), Is.EqualTo(ChiselContentsVolume.kNothing), "outside");
        }

        [Test]
        public void WhereSolidAndWaterOverlap_SolidWins()
        {
            var model = Model();
            Box(model, new float3(0, 0, 0), new float3(2, 2, 2), kWater);
            Box(model, new float3(1, 0, 0), new float3(3, 2, 2));
            Update();
            Assert.That(ChiselContentsVolume.ContentsAt(new Vector3(0.5f, 1, 1)), Is.EqualTo(kWater), "only water");
            Assert.That(ChiselContentsVolume.ContentsAt(new Vector3(1.5f, 1, 1)), Is.EqualTo(ChiselContentsList.kSolidIndex), "both");
            Assert.That(ChiselContentsVolume.ContentsAt(new Vector3(2.5f, 1, 1)), Is.EqualTo(ChiselContentsList.kSolidIndex), "only solid");
        }

        [Test]
        public void ASubtractiveBrush_CarvesTheWaterAway()
        {
            var model = Model();
            Box(model, new float3(0, 0, 0), new float3(4, 2, 2), kWater);
            Box(model, new float3(2, 0, 0), new float3(3, 2, 2), operation: CSGOperationType.Subtractive);
            Update();
            Assert.That(ChiselContentsVolume.ContentsAt(new Vector3(1, 1, 1)),    Is.EqualTo(kWater), "before the cut");
            Assert.That(ChiselContentsVolume.ContentsAt(new Vector3(2.5f, 1, 1)), Is.EqualTo(ChiselContentsVolume.kNothing), "in the cut");
            Assert.That(ChiselContentsVolume.ContentsAt(new Vector3(3.5f, 1, 1)), Is.EqualTo(kWater), "after the cut");
        }

        [Test]
        public void AMovedModel_IsAskedWhereItIs()
        {
            var model = Model(new Vector3(10, 0, 0));
            Box(model, new float3(0, 0, 0), new float3(2, 2, 2), kWater);
            Update();
            Assert.That(ChiselContentsVolume.ContentsAt(new Vector3(11, 1, 1)), Is.EqualTo(kWater), "where the model is");
            Assert.That(ChiselContentsVolume.ContentsAt(new Vector3(1, 1, 1)), Is.EqualTo(ChiselContentsVolume.kNothing), "where it would be unmoved");
        }

        // A baked model keeps only the volume, not the brushes or the CSG: its saved data alone answers
        [Test]
        public void TheVolumesSavedData_AnswersWithoutTheModel()
        {
            var model = Model();
            Box(model, new float3(0, 0, 0), new float3(4, 2, 2), kWater);
            Box(model, new float3(2, 0, 0), new float3(3, 2, 2), operation: CSGOperationType.Subtractive);
            Update();
            var volume = model.generated.generatedDataContainer.GetComponent<ChiselContentsVolume>();
            Assert.That(volume, Is.Not.Null, "the model's generated objects have a volume");
            var saved = JsonUtility.ToJson(volume);

            var baked = new GameObject("baked");
            created.Add(baked);
            var copy = baked.AddComponent<ChiselContentsVolume>();
            JsonUtility.FromJsonOverwrite(saved, copy);
            model.gameObject.SetActive(false);

            Assert.That(ChiselContentsVolume.ContentsAt(new Vector3(1, 1, 1)),    Is.EqualTo(kWater), "water");
            Assert.That(ChiselContentsVolume.ContentsAt(new Vector3(2.5f, 1, 1)), Is.EqualTo(ChiselContentsVolume.kNothing), "the cut");
        }

        // Every point of a scene of overlapping brushes of several types, against the rules the CSG follows
        [Test]
        public void RandomBrushes_AgreeWithTheRules([Values(1, 2, 3)] int seed)
        {
            var model  = Model();
            var random = new Unity.Mathematics.Random((uint)seed * 7919u);
            var boxes  = new List<(float3 min, float3 max, int contents, CSGOperationType operation)>();
            for (int b = 0; b < 8; b++)
            {
                var min = math.floor(random.NextFloat3(0, 6));
                var max = min + math.floor(random.NextFloat3(1, 4));
                var contents  = random.NextInt(0, kNames.Length);
                var operation = random.NextInt(0, 4) == 0 ? CSGOperationType.Subtractive : CSGOperationType.Additive;
                boxes.Add((min, max, contents, operation));
                Box(model, min, max, contents, operation);
            }
            Update();

            for (int z = 0; z < 10; z++)
            for (int y = 0; y < 10; y++)
            for (int x = 0; x < 10; x++)
            {
                var point = new float3(x, y, z) + 0.5f;
                Assert.That(ChiselContentsVolume.ContentsAt(point), Is.EqualTo(Expected(boxes, point)), $"at {point}");
            }
        }

        // The rules, for brushes in a list (ContentsOracle): each type is filled where its own, Solid and carving brushes
        // put it; Solid wins, then the earliest type
        static int Expected(List<(float3 min, float3 max, int contents, CSGOperationType operation)> boxes, float3 point)
        {
            for (int contents = 0; contents < kNames.Length; contents++)
            {
                var filled = false;
                foreach (var box in boxes)
                {
                    var carving = box.operation != CSGOperationType.Additive;
                    if (!carving && box.contents != contents && box.contents != ChiselContentsList.kSolidIndex)
                        continue;
                    var inside = math.all(point > box.min) && math.all(point < box.max);
                    filled = box.operation == CSGOperationType.Additive ? filled || inside : filled && !inside;
                }
                if (filled)
                    return contents;
            }
            return ChiselContentsVolume.kNothing;
        }
    }
}
