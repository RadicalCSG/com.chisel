using System.Collections.Generic;
using Chisel.Core;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;

namespace Chisel.Components.Tests
{
    // Each material of a model is a mesh and a renderer of its own, so an edit makes again only the meshes of the
    // materials it changed (ChiselGeneratedObjects.FindRenderables, CompactHierarchyManager's kept sections)
    [TestFixture]
    public class ChiselMaterialMeshTests
    {
        // A rebuild recomputes the bounds of every mesh it writes, so bounds this far off only survive when it didn't run
        static readonly Bounds kMarker = new(new Vector3(1000, 1000, 1000), new Vector3(1, 1, 1));

        readonly List<GameObject> roots = new();
        Material first, second;

        [SetUp]
        public void SetUp()
        {
            if (Object.FindObjectsByType<ChiselModelComponent>(FindObjectsInactive.Include).Length > 0)
                Assert.Ignore("A Chisel model is loaded; run these in a scene without one");
            first  = new Material(ChiselProjectSettings.DefaultWallMaterial) { name = "first" };
            second = new Material(ChiselProjectSettings.DefaultWallMaterial) { name = "second" };
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
            Object.DestroyImmediate(first);
            Object.DestroyImmediate(second);
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
            var root = new GameObject("material mesh test");
            roots.Add(root);
            return root.AddComponent<ChiselModelComponent>();
        }

        static ChiselBoxComponent Box(ChiselModelComponent model, float3 min, float3 max, Material material)
        {
            var go = new GameObject("box");
            go.transform.SetParent(model.transform, false);
            var box = go.AddComponent<ChiselBoxComponent>();
            if (math.any(min > box.Max)) { box.Max = max; box.Min = min; }
            else                         { box.Min = min; box.Max = max; }
            Update();
            SetMaterial(box, material);
            return box;
        }

        static void SetMaterial(ChiselGeneratorComponent generator, Material material)
        {
            foreach (var surface in generator.SurfaceDefinition.surfaces)
                surface.SetMaterial(material);
            generator.OnValidate();
            Update();
        }

        // The renderers of the model that draw something with this material
        static List<ChiselRenderObjects> RenderersOf(ChiselModelComponent model, Material material)
        {
            var found = new List<ChiselRenderObjects>();
            foreach (var renderable in model.generated.renderables)
            {
                if (renderable == null || !renderable.Valid || !renderable.HasGeometry)
                    continue;
                if (renderable.meshRenderer.sharedMaterial == material)
                    found.Add(renderable);
            }
            return found;
        }

        [Test]
        public void TwoMaterials_AreTwoRenderers_OneMaterialEach()
        {
            var model = Model();
            Box(model, new float3(-5, 0, 0), new float3(-4, 1, 1), first);
            Box(model, new float3(4, 0, 0), new float3(5, 1, 1), second);
            var withFirst  = RenderersOf(model, first);
            var withSecond = RenderersOf(model, second);
            Assert.That(withFirst.Count, Is.EqualTo(1), "the first material's renderer");
            Assert.That(withSecond.Count, Is.EqualTo(1), "the second material's renderer");
            Assert.That(withFirst[0].sharedMesh.subMeshCount, Is.EqualTo(1));
            Assert.That(withSecond[0].sharedMesh.subMeshCount, Is.EqualTo(1));
            Assert.That(withFirst[0], Is.Not.SameAs(withSecond[0]));
        }

        [Test]
        public void AnEditToOneMaterial_LeavesTheOtherMaterialsMeshAlone()
        {
            var model = Model();
            var moved = Box(model, new float3(-5, 0, 0), new float3(-4, 1, 1), first);
            Box(model, new float3(4, 0, 0), new float3(5, 1, 1), second);
            var untouched = RenderersOf(model, second)[0];
            var changed   = RenderersOf(model, first)[0];
            untouched.sharedMesh.bounds = kMarker;
            changed.sharedMesh.bounds   = kMarker;

            moved.transform.position += new Vector3(0, 0, 0.5f);
            ChiselNodeHierarchyManager.UpdateTreeNodeTransformation(moved);
            Update();

            Assert.That(RenderersOf(model, second)[0], Is.SameAs(untouched), "the same renderer");
            Assert.That(untouched.sharedMesh.bounds, Is.EqualTo(kMarker), "its mesh was not made again");
            Assert.That(RenderersOf(model, first)[0].sharedMesh.bounds, Is.Not.EqualTo(kMarker), "the edited material's mesh was");
        }

        // A material's surfaces in two 32 m chunks are two meshes (Documentation~/Design/Chunking.md), and an edit in one
        // keeps the other's mesh
        [Test]
        public void AMaterialInTwoChunks_IsTwoMeshes_AndAnEditInOneKeepsTheOther()
        {
            var model = Model();
            var moved = Box(model, new float3(1, 0, 0), new float3(2, 1, 1), first);
            Box(model, new float3(40, 0, 0), new float3(41, 1, 1), first);
            var renderers = RenderersOf(model, first);
            Assert.That(renderers.Count, Is.EqualTo(2), "a mesh per chunk");
            var far = renderers.Find(r => r.sharedMesh.bounds.center.x > 32);
            Assert.That(far, Is.Not.Null, "the far box's chunk");
            far.sharedMesh.bounds = kMarker;

            moved.transform.position += new Vector3(0, 0, 0.5f);
            ChiselNodeHierarchyManager.UpdateTreeNodeTransformation(moved);
            Update();

            Assert.That(RenderersOf(model, first), Does.Contain(far), "the far chunk's renderer");
            Assert.That(far.sharedMesh.bounds, Is.EqualTo(kMarker), "its mesh was not made again");
        }

        // A model that bakes lightmaps, whose renderers' lightmaps may be at most a few centimetres across
        ChiselModelComponent LightmappedModel()
        {
            var model = Model();
            UnityEditor.GameObjectUtility.SetStaticEditorFlags(model.gameObject, UnityEditor.StaticEditorFlags.ContributeGI);
            model.RenderSettings.receiveGI       = ReceiveGI.Lightmaps;
            model.RenderSettings.scaleInLightmap = 1000;
            model.SyncModelSettingsStore();
            Assert.That(model.RenderSettings.lightmapMaxSize, Is.GreaterThan(0), "the model doesn't bake lightmaps: this measures nothing");
            return model;
        }

        // Two boxes in one 32 m cell, in two 8 m cells: one chunk, unless the chunk's lightmap is too large
        [Test]
        public void AChunkWhoseLightmapIsTooLarge_IsSplit_InAModelThatBakesLightmaps()
        {
            var model = LightmappedModel();
            Box(model, new float3(1, 0, 0), new float3(2, 1, 1), first);
            Box(model, new float3(11, 0, 0), new float3(12, 1, 1), first);
            var renderers = RenderersOf(model, first);
            Assert.That(renderers.Count, Is.EqualTo(2), "a mesh per 8 m cell");
            var vertices = 0;
            foreach (var renderer in renderers)
                vertices += renderer.sharedMesh.vertexCount;
            Assert.That(vertices, Is.EqualTo(2 * 24), "both boxes, every face");
        }

        [Test]
        public void AChunk_IsNotSplit_InAModelLitByProbes()
        {
            var model = Model();
            model.RenderSettings.scaleInLightmap = 1000;
            model.SyncModelSettingsStore();
            Assert.That(model.RenderSettings.lightmapMaxSize, Is.EqualTo(0));
            Box(model, new float3(1, 0, 0), new float3(2, 1, 1), first);
            Box(model, new float3(11, 0, 0), new float3(12, 1, 1), first);
            Assert.That(RenderersOf(model, first).Count, Is.EqualTo(1), "one mesh for the 32 m cell");
        }

        // Three boxes of about 6 m^2 of lightmap each: with room for 15, their 32 m cell (18) splits into its 16 m cells (12 and 6)
        [Test]
        public void ASplitStopsAtTheCellsThatFit()
        {
            var model = LightmappedModel();
            var budgetSide = math.sqrt(15.0f);
            model.RenderSettings.scaleInLightmap = ChiselLightmapUVManager.LightmapAtlasSize() / (budgetSide * ChiselLightmapUVManager.LightmapResolution());
            model.SyncModelSettingsStore();
            Box(model, new float3(1, 0, 0), new float3(2, 1, 1), first);
            Box(model, new float3(11, 0, 0), new float3(12, 1, 1), first);
            Box(model, new float3(20, 0, 0), new float3(21, 1, 1), first);
            Assert.That(RenderersOf(model, first).Count, Is.EqualTo(2), "a mesh per 16 m cell");
        }

        // A chunk that fits is not split: the same boxes at the default lightmap scale
        [Test]
        public void AChunkWhoseLightmapFits_IsNotSplit()
        {
            var model = LightmappedModel();
            model.RenderSettings.scaleInLightmap = 1;
            model.SyncModelSettingsStore();
            Box(model, new float3(1, 0, 0), new float3(2, 1, 1), first);
            Box(model, new float3(11, 0, 0), new float3(12, 1, 1), first);
            Assert.That(RenderersOf(model, first).Count, Is.EqualTo(1), "one mesh for the 32 m cell");
        }

        [Test]
        public void AMaterialNoLongerDrawn_LosesItsRenderer()
        {
            var model = Model();
            // both in one 32 m chunk
            Box(model, new float3(1, 0, 0), new float3(2, 1, 1), first);
            var box = Box(model, new float3(9, 0, 0), new float3(10, 1, 1), second);
            SetMaterial(box, first);
            Assert.That(RenderersOf(model, second), Is.Empty, "nothing draws the second material");
            foreach (var renderable in model.generated.renderables)
                Assert.That(renderable.meshRenderer.sharedMaterial, Is.Not.SameAs(second), "no renderer of it is left");
            Assert.That(RenderersOf(model, first).Count, Is.EqualTo(1), "both boxes in the first material's mesh");
        }

        [Test]
        public void AKeptRendererThatWasDeleted_IsMadeAgain()
        {
            var model = Model();
            var moved = Box(model, new float3(-5, 0, 0), new float3(-4, 1, 1), first);
            Box(model, new float3(4, 0, 0), new float3(5, 1, 1), second);
            var lost = RenderersOf(model, second)[0];
            Object.DestroyImmediate(lost.container);

            moved.transform.position += new Vector3(0, 0, 0.5f);
            ChiselNodeHierarchyManager.UpdateTreeNodeTransformation(moved);
            Update();

            var again = RenderersOf(model, second);
            Assert.That(again.Count, Is.EqualTo(1), "the second material is drawn again");
            Assert.That(again[0].container, Is.Not.Null);
        }

        [Test]
        public void AChangeToTheUVsOnly_KeepsTheColliderAndTheLightmapLayout()
        {
            var model = Model();
            var box = Box(model, new float3(-1, 0, -1), new float3(1, 2, 1), first);
            var renderer = RenderersOf(model, first)[0];
            var lightmapHash = renderer.lightmapHashValue;
            Assert.That(lightmapHash, Is.Not.Zero, "the renderer knows its lightmap layout");
            Assert.That(model.generated.colliders.Length, Is.EqualTo(1));
            var collider = model.generated.colliders[0];
            collider.sharedMesh.bounds = kMarker;
            var uvsBefore = renderer.sharedMesh.uv;
            var lightmapBefore = renderer.sharedMesh.uv2;

            foreach (var surface in box.SurfaceDefinition.surfaces)
            {
                var details = surface.surfaceDetails;
                details.UV0.U.w += 0.25f;
                surface.surfaceDetails = details;
            }
            box.OnValidate();
            Update();

            Assert.That(model.generated.colliders[0], Is.SameAs(collider), "the same collider");
            Assert.That(collider.sharedMesh.bounds, Is.EqualTo(kMarker), "its mesh was not made again, nor baked");
            var after = RenderersOf(model, first)[0];
            Assert.That(after.sharedMesh.uv, Is.Not.EqualTo(uvsBefore), "the drawn uvs changed");
            Assert.That(after.lightmapHashValue, Is.EqualTo(lightmapHash), "the lightmap layout did not");
            Assert.That(after.sharedMesh.uv2, Is.EqualTo(lightmapBefore), "nor did the lightmap uvs");
        }

        // The selection owners are stored after an edit, not during it; restoring the selection stores them first
        [Test]
        public void TheSelectionOwners_LeftForLater_AreStoredBeforeTheyAreRestored()
        {
            var model = Model();
            var box = Box(model, new float3(-1, 0, -1), new float3(1, 2, 1), first);
            box.transform.position += new Vector3(0, 0, 0.5f);
            ChiselNodeHierarchyManager.UpdateTreeNodeTransformation(box);
            Update();
            var renderer = RenderersOf(model, first)[0];
            var before = (SelectionDescription[])renderer.GetSelectionIndexDescriptionArray().Clone();
            Assert.That(before, Is.Not.Empty);

            renderer.RestoreSelection();

            Assert.That(renderer.GetSelectionIndexDescriptionArray(), Is.EqualTo(before), "restored from owners stored first");
        }

        [Test]
        public void StoringThePendingSelectionOwners_NamesTheBrushesGenerator()
        {
            var model = Model();
            var box = Box(model, new float3(-1, 0, -1), new float3(1, 2, 1), first);
            ChiselRenderObjects.StorePendingSelectionOwners();
            var renderer = RenderersOf(model, first)[0];
            Assert.That(renderer.selectionOwners, Is.EquivalentTo(new[] { box }));
            Assert.That(renderer.selectionOwnerIndices.Length, Is.EqualTo(renderer.GetSelectionIndexDescriptionArray().Length));
        }

        // A collider is a mesh per physics material and chunk of space, so a move bakes only the chunks it touched
        [Test]
        public void FarApartBrushes_HaveCollidersOfTheirOwn_AndAMoveMakesOnlyItsOwnAgain()
        {
            var model = Model();
            var moved = Box(model, new float3(-51, 0, -1), new float3(-49, 2, 1), first);
            Box(model, new float3(49, 0, -1), new float3(51, 2, 1), first);
            Assert.That(model.generated.colliders.Length, Is.EqualTo(2), "one collider per chunk");
            ChiselColliderObjects ColliderAt(float x)
            {
                foreach (var collider in model.generated.colliders)
                {
                    if (collider.sharedMesh.bounds.center.x * x > 0)
                        return collider;
                }
                return null;
            }
            var untouched = ColliderAt(50);
            Assert.That(untouched, Is.Not.Null);
            untouched.sharedMesh.bounds = kMarker;
            Assert.That(ColliderAt(-50), Is.Not.Null);

            moved.transform.position += new Vector3(0, 0, 0.5f);
            ChiselNodeHierarchyManager.UpdateTreeNodeTransformation(moved);
            Update();

            Assert.That(System.Array.IndexOf(model.generated.colliders, untouched), Is.GreaterThanOrEqualTo(0), "the other chunk's collider is kept");
            Assert.That(untouched.sharedMesh.bounds, Is.EqualTo(kMarker), "and was not made again");
            Assert.That(model.generated.colliders.Length, Is.EqualTo(2));
        }

        static int IndexCount(Mesh mesh)
        {
            var count = 0;
            for (int s = 0; s < mesh.subMeshCount; s++)
                count += (int)mesh.GetIndexCount(s);
            return count;
        }

        static void RefreshVisibility()
        {
            ChiselUnityVisibilityManager.UpdateVisibility(force: true);
            ChiselUnityVisibilityManager.Update();
        }

        // The scene view draws a mesh whose brushes are all shown as it is, and a copy without the hidden ones otherwise
        [Test]
        public void AMeshWithEveryBrushShown_IsDrawnItself_AndOneWithAHiddenBrush_ThroughACopyWithoutIt()
        {
            var model = Model();
            // both in one 32 m chunk, so in one mesh
            Box(model, new float3(1, 0, 0), new float3(2, 1, 1), first);
            var hidden = Box(model, new float3(9, 0, 0), new float3(10, 1, 1), first);
            var visualizing = UnityEditor.StaticOcclusionCullingVisualization.showOcclusionCulling;
            UnityEditor.StaticOcclusionCullingVisualization.showOcclusionCulling = false;
            RefreshVisibility();
            var renderer = RenderersOf(model, first)[0];
            Assert.That(renderer.DrawnMesh, Is.SameAs(renderer.sharedMesh), "every brush shown: the mesh itself");
            Assert.That(renderer.meshRenderer.forceRenderingOff, Is.False, "every brush shown: Unity draws the renderer (and culls it)");
            try
            {
                UnityEditor.SceneVisibilityManager.instance.Hide(hidden.gameObject, false);
                RefreshVisibility();
                Assert.That(renderer.DrawnMesh, Is.SameAs(renderer.partialMesh), "a brush hidden: a copy");
                Assert.That(renderer.meshRenderer.forceRenderingOff, Is.True, "a brush hidden: the copy is drawn instead of the renderer");
                Assert.That(IndexCount(renderer.partialMesh), Is.GreaterThan(0));
                Assert.That(IndexCount(renderer.partialMesh), Is.LessThan(IndexCount(renderer.sharedMesh)), "without the hidden brush");

                // the occlusion culling visualizer shows what occlusion culling sees: the whole mesh
                UnityEditor.StaticOcclusionCullingVisualization.showOcclusionCulling = true;
                RefreshVisibility();
                Assert.That(renderer.meshRenderer.forceRenderingOff, Is.False, "a brush hidden, visualizing occlusion culling: Unity draws the whole mesh");
                UnityEditor.StaticOcclusionCullingVisualization.showOcclusionCulling = false;
                RefreshVisibility();
                Assert.That(renderer.meshRenderer.forceRenderingOff, Is.True, "and the copy again when the visualizer is off");
            }
            finally
            {
                UnityEditor.StaticOcclusionCullingVisualization.showOcclusionCulling = visualizing;
                UnityEditor.SceneVisibilityManager.instance.Show(hidden.gameObject, false);
                RefreshVisibility();
            }
            Assert.That(renderer.DrawnMesh, Is.SameAs(renderer.sharedMesh), "shown again: the mesh itself");
            Assert.That(renderer.meshRenderer.forceRenderingOff, Is.False, "shown again: Unity draws the renderer");
        }

        [Test]
        public void MovingABrush_MakesTheColliderAgain()
        {
            var model = Model();
            var box = Box(model, new float3(-1, 0, -1), new float3(1, 2, 1), first);
            var collider = model.generated.colliders[0];
            collider.sharedMesh.bounds = kMarker;
            box.transform.position += new Vector3(0, 0, 0.5f);
            ChiselNodeHierarchyManager.UpdateTreeNodeTransformation(box);
            Update();
            Assert.That(model.generated.colliders[0].sharedMesh.bounds, Is.Not.EqualTo(kMarker), "the moved collider was made again");
        }
    }
}
