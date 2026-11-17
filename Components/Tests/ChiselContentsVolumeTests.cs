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
            return Box(model.transform, min, max, contents, operation);
        }

        static ChiselBoxComponent Box(Transform parent, float3 min, float3 max, int contents = ChiselContentsList.kSolidIndex,
                                      CSGOperationType operation = CSGOperationType.Additive)
        {
            var go = new GameObject("box");
            go.transform.SetParent(parent, false);
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

        static ChiselCompositeComponent Composite(Transform parent, CSGOperationType operation, Vector3 position, Quaternion rotation, Vector3 scale)
        {
            var go = new GameObject("composite");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            go.transform.localRotation = rotation;
            go.transform.localScale    = scale;
            var composite = go.AddComponent<ChiselCompositeComponent>();
            composite.Operation = operation;
            return composite;
        }

        // Brushes rotated and scaled at random, some in composites that carve, of every contents type
        ChiselModelComponent ModelOfManyBrushes(int seed, int brushCount)
        {
            var model  = Model(new Vector3(3, -2, 5));
            var random = new Unity.Mathematics.Random((uint)seed * 104729u);
            var parents = new List<Transform> { model.transform };
            parents.Add(Composite(model.transform, CSGOperationType.Subtractive, new Vector3(1, 0, 2), Quaternion.Euler(0, 30, 0), new Vector3(1, 2, 1)).transform);
            parents.Add(Composite(parents[1], CSGOperationType.Intersecting, new Vector3(0, 1, 0), Quaternion.Euler(10, 0, 20), Vector3.one).transform);
            parents.Add(Composite(model.transform, CSGOperationType.Additive, new Vector3(-4, 0, 0), Quaternion.identity, new Vector3(0.5f, 1, 2)).transform);
            for (int b = 0; b < brushCount; b++)
            {
                var min = random.NextFloat3(-8, 8);
                var box = Box(parents[random.NextInt(parents.Count)], min, min + random.NextFloat3(0.25f, 4), random.NextInt(0, kNames.Length),
                              random.NextInt(0, 5) == 0 ? CSGOperationType.Subtractive : CSGOperationType.Additive);
                box.transform.localRotation = Quaternion.Euler(random.NextFloat3(0, 360));
                box.transform.localScale    = random.NextFloat3(0.5f, 2);
            }
            Update();
            return model;
        }

        // How the volume was built by walking the tree's nodes one by one: what any build of it has to give
        static void ReferenceBuild(CSGTree tree, List<ContentsVolumeNode> nodes, List<float4> planes)
        {
            var contentsCount = CompactHierarchyManager.ContentsCount;
            for (int i = 0; i < tree.Count; i++)
                ReferenceAdd(tree[i], float4x4.identity, false, contentsCount, nodes, planes);
        }

        static void ReferenceAdd(CSGTreeNode node, float4x4 parentToTree, bool carving, int contentsCount, List<ContentsVolumeNode> nodes, List<float4> planes)
        {
            if (!node.Valid)
                return;
            var operation  = node.Operation;
            var nodeToTree = math.mul(parentToTree, node.LocalTransformation);
            carving        = carving || operation == CSGOperationType.Subtractive || operation == CSGOperationType.Intersecting;
            var index      = nodes.Count;
            if (node.Type != CSGNodeType.Brush)
            {
                nodes.Add(new ContentsVolumeNode { operation = operation, carving = carving });
                for (int i = 0; i < node.Count; i++)
                    ReferenceAdd(node[i], nodeToTree, carving, contentsCount, nodes, planes);
                var branch = nodes[index];
                branch.descendants = nodes.Count - index - 1;
                nodes[index] = branch;
                return;
            }
            var brush = (CSGTreeBrush)node;
            var blob  = BrushMeshManager.GetBrushMeshBlob(brush.BrushMesh.BrushMeshID);
            if (!blob.IsCreated)
                return;
            ref var mesh = ref blob.Value;
            var planeToTree = math.transpose(math.inverse(nodeToTree));
            var firstPlane  = planes.Count;
            for (int p = 0; p < mesh.localPlaneCount; p++)
            {
                var plane = math.mul(planeToTree, mesh.localPlanes[p]);
                planes.Add(plane / math.length(plane.xyz));
            }
            var min = new float3(float.PositiveInfinity);
            var max = new float3(float.NegativeInfinity);
            for (int v = 0; v < mesh.localVertices.Length; v++)
            {
                var vertex = math.transform(nodeToTree, mesh.localVertices[v]);
                min = math.min(min, vertex);
                max = math.max(max, vertex);
            }
            nodes.Add(new ContentsVolumeNode
            {
                operation  = operation, isBrush = true, carving = carving,
                contents   = ContentsRules.Resolve(brush.Contents, contentsCount),
                firstPlane = firstPlane, planeCount = planes.Count - firstPlane,
                min        = min - 1e-4f, max = max + 1e-4f
            });
        }

        static void AssertClose(float4 actual, float4 expected, string what)
        {
            Assert.That(math.all(math.abs(actual - expected) <= 1e-5f * math.max(1, math.abs(expected))), Is.True, $"{what}: {actual} is not {expected}");
        }

        [Test]
        public void NestedCompositesOfTurnedAndScaledBrushes_BuildLikeTheTreeWalkedNodeByNode([Values(1, 2)] int seed)
        {
            var model = ModelOfManyBrushes(seed, 60);
            var expectedNodes  = new List<ContentsVolumeNode>();
            var expectedPlanes = new List<float4>();
            ReferenceBuild(model.Node, expectedNodes, expectedPlanes);

            using var nodes  = new Unity.Collections.NativeList<ContentsVolumeNode>(Unity.Collections.Allocator.TempJob);
            using var planes = new Unity.Collections.NativeList<float4>(Unity.Collections.Allocator.TempJob);
            ContentsVolume.Build(model.Node, nodes, planes);

            Assert.That(expectedNodes.Count, Is.GreaterThan(60), "the brushes and composites are all there");
            Assert.That(nodes.Length, Is.EqualTo(expectedNodes.Count), "nodes");
            Assert.That(planes.Length, Is.EqualTo(expectedPlanes.Count), "planes");
            for (int n = 0; n < nodes.Length; n++)
            {
                var actual = nodes[n]; var expected = expectedNodes[n];
                Assert.That((actual.operation, actual.isBrush, actual.carving, actual.contents, actual.descendants, actual.firstPlane, actual.planeCount),
                            Is.EqualTo((expected.operation, expected.isBrush, expected.carving, expected.contents, expected.descendants, expected.firstPlane, expected.planeCount)), $"node {n}");
                AssertClose(new float4(actual.min, 0), new float4(expected.min, 0), $"node {n} min");
                AssertClose(new float4(actual.max, 0), new float4(expected.max, 0), $"node {n} max");
            }
            for (int p = 0; p < planes.Length; p++)
                AssertClose(planes[p], expectedPlanes[p], $"plane {p}");
        }

        // The volume is built again after every update of the model's meshes, so it must not cost more than the update
        [Test]
        public void BuildingTheVolume_IsFarFasterThanWalkingTheTreeNodeByNode()
        {
            var model  = ModelOfManyBrushes(3, 600);
            var volume = model.generated.generatedDataContainer.GetComponent<ChiselContentsVolume>();
            var nodes  = new List<ContentsVolumeNode>();
            var planes = new List<float4>();
            volume.Build(model.Node);
            ReferenceBuild(model.Node, nodes, planes);

            double Best(System.Action action)
            {
                var best = double.MaxValue;
                for (int r = 0; r < 10; r++)
                {
                    var clock = System.Diagnostics.Stopwatch.StartNew();
                    action();
                    best = System.Math.Min(best, clock.Elapsed.TotalMilliseconds);
                }
                return best;
            }
            var reference = Best(() => { nodes.Clear(); planes.Clear(); ReferenceBuild(model.Node, nodes, planes); });
            var build     = Best(() => volume.Build(model.Node));
            Assert.That(build * 5, Is.LessThan(reference), $"built in {build:F3} ms, walked node by node in {reference:F3} ms");
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
