using System.Collections.Generic;
using Chisel.Components;
using Chisel.Core;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;

namespace Chisel.Editors.Tests
{
    // A hotspot edit to one surface of a group goes to every surface of the group (ChiselHotspotGroupEdit.Spread)
    [TestFixture]
    public class ChiselHotspotGroupEditTests
    {
        const string kFolder = "Assets/__ChiselHotspotGroupEditTests";

        readonly List<GameObject> roots = new();
        ChiselHotspotLayout layout;
        Material material;

        [SetUp]
        public void SetUp()
        {
            if (Object.FindObjectsByType<ChiselModelComponent>(FindObjectsInactive.Include).Length > 0)
                Assert.Ignore("A Chisel model is loaded; run these in a scene without one");
            layout = ScriptableObject.CreateInstance<ChiselHotspotLayout>();
            layout.size = new Vector2Int(2048, 2048);
            layout.pixelsPerUnit = 256;
            layout.tiles = new List<HotspotTile>
            {
                new() { id = 1, name = "A1", points = new[] { new Vector2Int(0, 0), new Vector2Int(512, 0), new Vector2Int(512, 512), new Vector2Int(0, 512) } },
                new() { id = 2, name = "B1", points = new[] { new Vector2Int(1024, 0), new Vector2Int(2048, 0), new Vector2Int(2048, 512), new Vector2Int(1024, 512) } },
            };
            if (!AssetDatabase.IsValidFolder(kFolder))
                AssetDatabase.CreateFolder("Assets", kFolder.Substring("Assets/".Length));
            material = new Material(ChiselProjectSettings.DefaultWallMaterial);
            AssetDatabase.CreateAsset(material, kFolder + "/hotspot.mat");
            material.AddMetadataOfType<ChiselSurfaceMetadata>().hotspotLayout = layout;
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var root in roots)
                if (root) Object.DestroyImmediate(root);
            roots.Clear();
            Update();
            AssetDatabase.DeleteAsset(kFolder);
            if (layout)
                Object.DestroyImmediate(layout);
        }

        static void Update()
        {
            for (int i = 0; i < 3; i++)
            {
                ChiselNodeHierarchyManager.Update();
                ChiselModelManager.Instance.UpdateModels();
            }
        }

        ChiselModelComponent Model()
        {
            var root = new GameObject("hotspot group edit test");
            roots.Add(root);
            return root.AddComponent<ChiselModelComponent>();
        }

        ChiselBoxComponent Box(ChiselModelComponent model, float3 min, float3 max)
        {
            var go = new GameObject("box");
            go.transform.SetParent(model.transform, false);
            var box = go.AddComponent<ChiselBoxComponent>();
            if (math.any(min > box.Max)) { box.Max = max; box.Min = min; }
            else                         { box.Min = min; box.Max = max; }
            Update();
            foreach (var surface in box.SurfaceDefinition.surfaces)
                surface.SetMaterial(material);
            box.OnValidate();
            Update();
            return box;
        }

        // The face of a generator's brush toward -z
        static SurfaceReference FrontOf(ChiselGeneratorComponent generator)
        {
            var brush = (CSGTreeBrush)generator.TopTreeNode;
            var blob = BrushMeshManager.GetBrushMeshBlob(brush.BrushMesh);
            ref var mesh = ref blob.Value;
            var best = -1; var bestZ = float.PositiveInfinity;
            for (int p = 0; p < mesh.polygons.Length; p++)
            {
                var z = 0f;
                for (int e = 0; e < mesh.polygons[p].edgeCount; e++)
                    z += mesh.localVertices[mesh.halfEdges[mesh.polygons[p].firstEdge + e].vertexIndex].z;
                z /= mesh.polygons[p].edgeCount;
                if (z < bestZ) { bestZ = z; best = p; }
            }
            return new SurfaceReference(generator, mesh.polygons[best].descriptionIndex, brush, best);
        }

        static void SetSeed(SurfaceReference surface, uint seed)
        {
            var details = surface.SurfaceDetails;
            details.hotspot.seed = seed;
            surface.SurfaceDetails = details;
        }

        // Two boxes side by side, their fronts one group; a third one apart
        (SurfaceReference left, SurfaceReference right, SurfaceReference apart) Fronts()
        {
            var model = Model();
            var left  = Box(model, new float3(-2, 0, -0.125f), new float3(0, 2, 0.125f));
            var right = Box(model, new float3(0, 0, -0.125f), new float3(2, 2, 0.125f));
            var apart = Box(model, new float3(10, 0, -0.125f), new float3(12, 2, 0.125f));
            Update();
            return (FrontOf(left), FrontOf(right), FrontOf(apart));
        }

        [Test]
        public void AnEditToOneSurfaceOfAGroup_GoesToTheOthers_NotToASurfaceApart()
        {
            var (left, right, apart) = Fronts();
            var apartSeed = apart.SurfaceDetails.hotspot.seed;
            SetSeed(left, 1234);
            Assert.That(ChiselHotspotGroupEdit.Spread(new[] { left }), Is.True, "something changed");
            Assert.That(right.SurfaceDetails.hotspot.seed, Is.EqualTo(1234u), "the other surface of the group");
            Assert.That(apart.SurfaceDetails.hotspot.seed, Is.EqualTo(apartSeed), "the surface apart");
        }

        [Test]
        public void SpreadingWhatTheGroupHasAlready_ChangesNothing()
        {
            var (left, right, _) = Fronts();
            SetSeed(left, 1234);
            ChiselHotspotGroupEdit.Spread(new[] { left });
            Assert.That(ChiselHotspotGroupEdit.Spread(new[] { left }), Is.False);
            Assert.That(ChiselHotspotGroupEdit.Spread(new[] { right }), Is.False, "from the other one either");
        }

        [Test]
        public void ASpreadEdit_IsUndone()
        {
            var (left, right, _) = Fronts();
            var rightSeed = right.SurfaceDetails.hotspot.seed;
            SetSeed(left, 1234);
            Undo.IncrementCurrentGroup();
            Assert.That(ChiselHotspotGroupEdit.Spread(new[] { left }, "Spread Hotspot"), Is.True);
            Assert.That(right.SurfaceDetails.hotspot.seed, Is.EqualTo(1234u));
            Undo.PerformUndo();
            Assert.That(right.SurfaceDetails.hotspot.seed, Is.EqualTo(rightSeed), "the other surface has its own seed back");
        }
    }
}
