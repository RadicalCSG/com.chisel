using System;
using System.Collections.Generic;
using Chisel.Core;
using NUnit.Framework;
using UnityEngine;

namespace Chisel.Components.Tests
{
    [TestFixture]
    public class ChiselSavedOutputTests
    {
        const int kModelCount = 2;

        // A rebuild recomputes the bounds of every mesh it writes, so bounds this far off only survive when it didn't run
        static readonly Bounds kMarker = new(new Vector3(1000, 1000, 1000), new Vector3(1, 1, 1));

        readonly List<GameObject> roots = new();
        readonly List<ChiselModelComponent> models = new();
        readonly List<ChiselBoxComponent> boxes = new();

        [SetUp]
        public void SetUp()
        {
            if (UnityEngine.Object.FindObjectsByType<ChiselModelComponent>(FindObjectsInactive.Include).Length > 0)
                Assert.Ignore("A Chisel model is loaded; run these in a scene without one");

            for (int i = 0; i < kModelCount; i++)
            {
                var root = new GameObject("Model " + i);
                roots.Add(root);
                models.Add(root.AddComponent<ChiselModelComponent>());
                var boxObject = new GameObject("Box");
                boxObject.transform.SetParent(root.transform, false);
                boxObject.transform.localPosition = new Vector3(i * 4, 0, 0);
                var box = boxObject.AddComponent<ChiselBoxComponent>();
                box.OnValidate();
                boxes.Add(box);
            }
            Update();
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var root in roots)
            {
                if (root)
                    UnityEngine.Object.DestroyImmediate(root);
            }
            roots.Clear();
            models.Clear();
            boxes.Clear();
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

        // What saving the scene does
        void Save()
        {
            ChiselModelManager.Instance.StoreInputHashes(roots[0].scene);
        }

        static ChiselRenderObjects RenderableWithGeometry(ChiselModelComponent model)
        {
            foreach (var renderable in model.generated.renderables)
            {
                if (renderable != null && !renderable.invalid && renderable.sharedMesh.vertexCount > 0)
                    return renderable;
            }
            Assert.Fail($"{model.name} has no geometry");
            return null;
        }

        void MarkSavedMeshes()
        {
            foreach (var model in models)
                RenderableWithGeometry(model).sharedMesh.bounds = kMarker;
        }

        static bool IsMarked(ChiselModelComponent model)
        {
            return RenderableWithGeometry(model).sharedMesh.bounds == kMarker;
        }

        // What opening the scene again leaves: every node registered anew, and the renderables without what the scene
        // doesn't keep, the meshes that are never saved and what only lasts a session
        void Reopen(Action whileClosed = null)
        {
            foreach (var root in roots)
                root.SetActive(false);
            Update();
            whileClosed?.Invoke();
            foreach (var model in models)
            {
                foreach (var renderable in model.generated.renderables)
                    Unload(renderable);
                foreach (var renderable in model.generated.debugVisualizationRenderables)
                    Unload(renderable);
            }
            foreach (var root in roots)
                root.SetActive(true);
            Update();
        }

        static void Unload(ChiselRenderObjects renderable)
        {
            if (renderable == null || renderable.invalid)
                return;
            UnityEngine.Object.DestroyImmediate(renderable.partialMesh);
            UnityEngine.Object.DestroyImmediate(renderable.selectionMesh);
            renderable.partialMesh   = null;
            renderable.selectionMesh = null;
            renderable.triangleBrushes.selectionIndexDescriptions = Array.Empty<SelectionDescription>();
            renderable.triangleBrushes.perTriangleNodeIDLookup    = Array.Empty<CompactNodeID>();
            renderable.triangleBrushes.validTriangleCount         = 0;
        }

        [Test]
        public void AnUnchangedModel_KeepsItsSavedMeshes()
        {
            Save();
            MarkSavedMeshes();
            Reopen();
            foreach (var model in models)
            {
                Assert.That(CompactHierarchyManager.IsTreeUpdateSkipped(model.Node), Is.True, model.name);
                Assert.That(IsMarked(model), Is.True, $"{model.name} was built again");
            }
        }

        [Test]
        public void AModelThatKeptItsMeshes_CanBePicked()
        {
            Save();
            Reopen();
            for (int i = 0; i < kModelCount; i++)
            {
                var renderable   = RenderableWithGeometry(models[i]);
                var lookup       = renderable.triangleBrushes;
                var boxEntityID  = UnityEngine.EntityId.ToULong(boxes[i].GetEntityId());
                Assert.That(lookup.selectionIndexDescriptions, Is.Not.Empty, "no selection ids");
                foreach (var description in lookup.selectionIndexDescriptions)
                    Assert.That(description.entityID, Is.EqualTo(boxEntityID), "a selection id picks something else");
                Assert.That(boxes[i].TopTreeNode.Valid, Is.False,
                            "reading the picking data built the node graph, and a picking render reads it for every "
                            + "model on screen - the skip would be gone at the first mouse move");
            }
        }

        [Test]
        public void PickingASurfaceOnAModelThatKeptItsMeshes_BuildsThatModel()
        {
            Save();
            MarkSavedMeshes();
            Reopen();
            Assume.That(boxes[0].TopTreeNode.Valid, Is.False, "nothing was skipped, so there is no door to go through");

            var renderable  = RenderableWithGeometry(models[0]);
            var description = renderable.DescriptionForPicking(models[0], 0);
            Update();

            Assert.That(boxes[0].TopTreeNode.Valid, Is.True, "the picked model was not built");
            var boxNodeID = CompactHierarchyManager.GetCompactNodeID(boxes[0].TopTreeNode);
            Assert.That(description.brushNodeID, Is.EqualTo(boxNodeID), "the picked surface is on another brush");

            var lookup = RenderableWithGeometry(models[0]).triangleBrushes;
            Assert.That(lookup.validTriangleCount, Is.GreaterThan(0), "no triangles");
            for (int n = 0; n < lookup.validTriangleCount; n++)
                Assert.That(lookup.perTriangleNodeIDLookup[n], Is.EqualTo(boxNodeID), $"triangle {n} is on another brush");

            // The door is per-model on purpose: picking in one model must not build the rest of the map
            Assert.That(boxes[1].TopTreeNode.Valid, Is.False, "picking one model built another one too");
            Assert.That(IsMarked(models[1]), Is.True, "the model nobody picked was built");
        }

        [Test]
        public void TheBrushesOfAModelThatKeptItsMeshes_HaveOutlines()
        {
            Save();
            Reopen();
            var found = new HashSet<CSGTreeBrush>();
            foreach (var box in boxes)
            {
                found.Clear();
                ChiselModelManager.Instance.GetAllTreeBrushes(box, found);
                Assert.That(found, Is.Not.Empty, box.name + " has no brushes to outline");
                foreach (var brush in found)
                    Assert.That(brush.Outline.IsCreated, Is.True, box.name);
            }
        }

        // An incremental update needs what building the whole model leaves behind, so the first one builds all of it
        [Test]
        public void AModelThatKeptItsMeshes_IsBuiltWhenItChanges()
        {
            Save();
            MarkSavedMeshes();
            Reopen();
            boxes[0].transform.localPosition += new Vector3(0, 1, 0);
            ChiselNodeHierarchyManager.UpdateTreeNodeTransformation(boxes[0]);
            Update();
            Assert.That(CompactHierarchyManager.IsTreeUpdateSkipped(models[0].Node), Is.False);
            Assert.That(IsMarked(models[0]), Is.False, "the changed model wasn't built");
            Assert.That(IsMarked(models[1]), Is.True, "the model that didn't change was built too");
        }

        [Test]
        public void AModelThatChangedWhileClosed_IsBuiltWhenItOpens()
        {
            Save();
            MarkSavedMeshes();
            Reopen(whileClosed: () => boxes[0].transform.localPosition += new Vector3(0, 1, 0));
            Assert.That(CompactHierarchyManager.IsTreeUpdateSkipped(models[0].Node), Is.False);
            Assert.That(IsMarked(models[0]), Is.False, "the changed model kept its old meshes");
            Assert.That(IsMarked(models[1]), Is.True);
        }

        [Test]
        public void AModelSavedWithoutAHash_IsBuiltWhenItOpens()
        {
            MarkSavedMeshes();
            Reopen();
            foreach (var model in models)
                Assert.That(IsMarked(model), Is.False, model.name);
        }

        [Test]
        public void AMoveTheTreeHasNotSeenYet_IsBuiltBeforeTheSaveDescribesIt()
        {
            Save();
            MarkSavedMeshes();
            boxes[0].transform.localPosition += new Vector3(0, 1, 0);
            // What a move in the editor does (ChiselUnityEventsManager): queued, passed on by the next update
            ChiselNodeHierarchyManager.NotifyTransformationChanged(new HashSet<ChiselNodeComponent> { boxes[0] });
            Save();
            Reopen();
            Assert.That(IsMarked(models[0]), Is.False, "the moved box's model kept the meshes of where the box was");
            Assert.That(IsMarked(models[1]), Is.True, "the model that did not change was built too");
        }

        [Test]
        public void ABrushAddedButNotYetRegistered_IsBuiltBeforeTheSaveDescribesIt()
        {
            Save();
            MarkSavedMeshes();
            var added = new GameObject("Added box");   // a child of model 0, destroyed with it in TearDown
            added.transform.SetParent(roots[0].transform, false);
            added.transform.localPosition = new Vector3(0, 2, 0);
            added.AddComponent<ChiselBoxComponent>().OnValidate();   // its registration waits in the queue
            Save();
            Reopen();
            Assert.That(IsMarked(models[0]), Is.False, "the model kept meshes without the box added to it");
            Assert.That(IsMarked(models[1]), Is.True, "the model that did not change was built too");
        }

        [Test]
        public void AModelBuiltOnDemand_HasItsBrushesWhereTheyAre()
        {
            // where the box's brush is, and where the CSG draws it, when the model is built the ordinary way
            var expectedMatrix = ((CSGTreeBrush)boxes[1].TopTreeNode).NodeToTreeSpaceMatrix;
            var expectedCenter = RenderableWithGeometry(models[1]).sharedMesh.bounds.center;
            Assume.That(expectedMatrix.c3.x, Is.EqualTo(4f), "the box is not where the fixture put it");

            Save();
            Reopen();
            Assume.That(boxes[1].TopTreeNode.Valid, Is.False, "nothing was skipped, so nothing is built on demand");

            ChiselModelManager.BuildTheNodesOfNow(models[1]);
            Assert.That(boxes[1].TopTreeNode.Valid, Is.True, "the model was not built");
            Assert.That(((CSGTreeBrush)boxes[1].TopTreeNode).NodeToTreeSpaceMatrix, Is.EqualTo(expectedMatrix),
                        "the box's brush is not where the box is");

            // and what its CSG draws once it runs
            boxes[1].TopTreeNode.SetDirty();
            Update();
            Assert.That(RenderableWithGeometry(models[1]).sharedMesh.bounds.center, Is.EqualTo(expectedCenter),
                        "the model's CSG drew the box somewhere else than the box is");
        }

        // Queries read what the CSG built besides the meshes, so a model that kept its meshes is built first
        [Test]
        public void AQuery_BuildsTheModelsThatKeptTheirMeshes()
        {
            Save();
            Reopen();
            var center = RenderableWithGeometry(models[0]).sharedMesh.bounds.center; // model 0 sits at the origin
            var found  = new List<ChiselIntersection>();
            Assert.That(ChiselSceneQuery.FindFirstWorldIntersection(found, center + new Vector3(0, 10, 0), center - new Vector3(0, 10, 0)),
                        Is.True, "the ray missed the box");
            Assert.That(CompactHierarchyManager.IsTreeUpdateSkipped(models[0].Node), Is.False);
        }

        // What the selection ids of a model's renderables pick
        static HashSet<ulong> SelectionEntities(ChiselModelComponent model)
        {
            var found = new HashSet<ulong>();
            foreach (var renderable in model.generated.renderables)
            {
                if (renderable == null || renderable.invalid)
                    continue;
                foreach (var description in renderable.triangleBrushes.selectionIndexDescriptions)
                    found.Add(description.entityID);
            }
            return found;
        }

        // A decal's surfaces select the decal, not the generator of the brush they lie on, and the scene keeps only
        // those selection ids apart from the rest
        [Test]
        public void ADecalOnAModelThatKeptItsMeshes_IsStillSelected()
        {
            var decalObject = new GameObject("Decal");
            var material    = new Material(Shader.Find("Hidden/InternalErrorShader"));
            try
            {
                // Looking down onto the top face of the box, which is the unit cube at the model's origin
                decalObject.transform.SetParent(roots[0].transform, false);
                decalObject.transform.localPosition = new Vector3(0.5f, 1.0f, 0.5f);
                decalObject.transform.localRotation = Quaternion.Euler(90, 0, 0);
                var decal = decalObject.AddComponent<ChiselDecalComponent>();
                decal.Material = material;
                decal.Size = new Vector3(0.5f, 0.5f, 0.5f);
                Update();

                var decalEntity = UnityEngine.EntityId.ToULong(decal.GetEntityId());
                Assert.That(SelectionEntities(models[0]), Does.Contain(decalEntity), "the decal drew no surfaces");
                var before = SelectionEntities(models[0]);

                Save();
                Reopen();
                Assert.That(CompactHierarchyManager.IsTreeUpdateSkipped(models[0].Node), Is.True);
                Assert.That(SelectionEntities(models[0]), Is.EquivalentTo(before), "other things are selected now");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(decalObject);
                UnityEngine.Object.DestroyImmediate(material);
                Update();
            }
        }

        [Test]
        public void OnlyTheRenderersOfSurfacesInTheBakedLighting_GoIntoABake()
        {
            foreach (var root in roots)
                UnityEditor.GameObjectUtility.SetStaticEditorFlags(root, UnityEditor.StaticEditorFlags.ContributeGI);
            ChiselNodeHierarchyManager.Rebuild();
            Update();
            AssertOnlyTheRenderersInTheBakeContributeGI("after a build");

            Save();
            MarkSavedMeshes();
            Reopen(whileClosed: () =>
            {
                foreach (var model in models)
                {
                    foreach (var renderable in model.generated.renderables)
                    {
                        if (renderable != null && !renderable.invalid)
                            UnityEditor.GameObjectUtility.SetStaticEditorFlags(renderable.container, UnityEditor.StaticEditorFlags.ContributeGI);
                    }
                }
            });
            foreach (var model in models)
                Assert.That(IsMarked(model), Is.True, $"{model.name} was built again");
            AssertOnlyTheRenderersInTheBakeContributeGI("after the model kept its meshes");
        }

        [Test]
        public void AnEmptyRenderer_IsNotInTheBake_EvenWhenItsSurfacesWouldBe()
        {
            foreach (var root in roots)
                UnityEditor.GameObjectUtility.SetStaticEditorFlags(root, UnityEditor.StaticEditorFlags.ContributeGI);
            ChiselNodeHierarchyManager.Rebuild();
            Update();

            var model      = models[0];
            var renderable = RenderableWithGeometry(model);
            Assume.That(renderable.HasGeometry, Is.True, "the fixture has to start with a slot that does have geometry");
            Assume.That(renderable.query & SurfaceDestinationFlags.ExcludedFromGlobalIllumination,
                        Is.EqualTo(SurfaceDestinationFlags.None), "and whose surfaces would otherwise be in the bake");
            Assert.That(UnityEditor.GameObjectUtility.GetStaticEditorFlags(renderable.container) & UnityEditor.StaticEditorFlags.ContributeGI,
                        Is.EqualTo(UnityEditor.StaticEditorFlags.ContributeGI), "it should be in the bake while it has geometry");

            // Empty it, exactly as a slot that generated nothing comes out, and let the containers be written again
            var had = renderable.sharedMesh;
            renderable.sharedMesh = new Mesh { name = "empty" };
            try
            {
                Assume.That(renderable.HasGeometry, Is.False, "the slot is empty now");
                model.generated.UpdateContainersWhenModelStateChanged(model);
                // UpdateContainers is driven by the model's state, which an emptied mesh does not change; toggling a
                // flag makes it run, and is what any real edit would do
                UnityEditor.GameObjectUtility.SetStaticEditorFlags(model.gameObject,
                    UnityEditor.StaticEditorFlags.ContributeGI | UnityEditor.StaticEditorFlags.BatchingStatic);
                model.generated.UpdateContainersWhenModelStateChanged(model);

                Assert.That(UnityEditor.GameObjectUtility.GetStaticEditorFlags(renderable.container) & UnityEditor.StaticEditorFlags.ContributeGI,
                            Is.EqualTo(default(UnityEditor.StaticEditorFlags)),
                            "an empty renderer must not be in the bake: Unity would trace rays against an empty mesh");
            }
            finally
            {
                var empty = renderable.sharedMesh;
                renderable.sharedMesh = had;
                if (empty) UnityEngine.Object.DestroyImmediate(empty);
            }
        }

        void AssertOnlyTheRenderersInTheBakeContributeGI(string when)
        {
            foreach (var model in models)
            {
                Assert.That(RenderableWithGeometry(model).query & SurfaceDestinationFlags.ExcludedFromGlobalIllumination,
                            Is.EqualTo(SurfaceDestinationFlags.None), $"a box's surfaces take part in the baked lighting ({when})");
                foreach (var renderable in model.generated.renderables)
                {
                    if (renderable == null || renderable.invalid)
                        continue;
                    var inTheBake     = renderable.HasGeometry &&
                                        (renderable.query & SurfaceDestinationFlags.ExcludedFromGlobalIllumination) == SurfaceDestinationFlags.None;
                    var staticFlags   = UnityEditor.GameObjectUtility.GetStaticEditorFlags(renderable.container);
                    var contributesGI = (staticFlags & UnityEditor.StaticEditorFlags.ContributeGI) == UnityEditor.StaticEditorFlags.ContributeGI;
                    Assert.That(contributesGI, Is.EqualTo(inTheBake), $"{renderable.container.name} of {model.name} {when}");
                }
            }
        }

        [Test]
        public void TheRenderersThatOnlyCastShadows_NeitherOccludeNorAreOccluded()
        {
            const UnityEditor.StaticEditorFlags kOcclusion = UnityEditor.StaticEditorFlags.OccluderStatic |
                                                             UnityEditor.StaticEditorFlags.OccludeeStatic;
            foreach (var root in roots)
                UnityEditor.GameObjectUtility.SetStaticEditorFlags(root, kOcclusion);
            ChiselNodeHierarchyManager.Rebuild();
            Update();

            foreach (var model in models)
            {
                foreach (var renderable in model.generated.renderables)
                {
                    if (renderable == null || renderable.invalid)
                        continue;
                    var isDrawn     = (renderable.query & SurfaceDestinationFlags.Renderable) == SurfaceDestinationFlags.Renderable;
                    var staticFlags = UnityEditor.GameObjectUtility.GetStaticEditorFlags(renderable.container);
                    Assert.That(staticFlags & kOcclusion, Is.EqualTo(isDrawn ? kOcclusion : 0),
                                $"{renderable.container.name} of {model.name}");
                }
            }
        }

        [Test]
        public void AModelMarkedContributeGI_PutsItsRenderersInTheBake_WithoutBuildingAgain()
        {
            Save();
            MarkSavedMeshes();
            foreach (var root in roots)
                UnityEditor.GameObjectUtility.SetStaticEditorFlags(root, UnityEditor.StaticEditorFlags.ContributeGI);
            Update();

            foreach (var model in models)
            {
                Assert.That(IsMarked(model), Is.True, $"{model.name} was built again; the flags should not need it");
                var renderable = RenderableWithGeometry(model);
                var staticFlags = UnityEditor.GameObjectUtility.GetStaticEditorFlags(renderable.container);
                Assert.That(staticFlags & UnityEditor.StaticEditorFlags.ContributeGI,
                            Is.EqualTo(UnityEditor.StaticEditorFlags.ContributeGI), renderable.container.name);
            }
        }

        // ... and taking it away again reaches them too
        [Test]
        public void AModelNoLongerContributingGI_TakesItsRenderersOutOfTheBake()
        {
            foreach (var root in roots)
                UnityEditor.GameObjectUtility.SetStaticEditorFlags(root, UnityEditor.StaticEditorFlags.ContributeGI);
            Update();
            foreach (var root in roots)
                UnityEditor.GameObjectUtility.SetStaticEditorFlags(root, 0);
            Update();

            foreach (var model in models)
            {
                foreach (var renderable in model.generated.renderables)
                {
                    if (renderable == null || renderable.invalid || !renderable.container)
                        continue;
                    var staticFlags = UnityEditor.GameObjectUtility.GetStaticEditorFlags(renderable.container);
                    // Against the enum, not 0: NUnit compares an enum with an int as unequal and then reports
                    // "Expected: 0, But was: 0", which says nothing at all
                    Assert.That(staticFlags & UnityEditor.StaticEditorFlags.ContributeGI,
                                Is.EqualTo(default(UnityEditor.StaticEditorFlags)), renderable.container.name);
                }
            }
        }

        // The model's layer had the same gap, and is the case nobody had noticed
        [Test]
        public void AModelOnANewLayer_MovesItsGeneratedObjects_WithoutBuildingAgain()
        {
            Save();
            MarkSavedMeshes();
            const int kLayer = 5; // UI, which every project has
            foreach (var root in roots)
                root.layer = kLayer;
            Update();

            foreach (var model in models)
            {
                Assert.That(IsMarked(model), Is.True, $"{model.name} was built again; a layer should not need it");
                Assert.That(RenderableWithGeometry(model).container.layer, Is.EqualTo(kLayer));
            }
        }

        // The hash taken from the components has to survive a scene being closed and opened, or it can't be compared
        // with what was stored at save
        [Test]
        public void TheHashTakenFromTheComponents_IsTheSameAfterAReopen()
        {
            Save();
            var stored = new List<Hash128>();
            foreach (var model in models)
            {
                Assert.That(model.generated.componentInputHash.isValid, Is.True, $"{model.name} saved no component hash");
                stored.Add(model.generated.componentInputHash);
            }

            Reopen();
            for (int i = 0; i < models.Count; i++)
            {
                Assert.That(ComponentHash(models[i]), Is.EqualTo(stored[i]), $"{models[i].name}'s components hash differently now");
                Assert.That(CompactHierarchyManager.IsTreeUpdateSkipped(models[i].Node), Is.True, $"{models[i].name} was built");
            }
        }

        // ... and it has to answer the same as the tree's hash about a model that DID change, which is the answer that
        // matters: a hash that misses a change keeps stale geometry with no error
        [Test]
        public void TheHashTakenFromTheComponents_AlsoNoticesAModelThatChangedWhileClosed()
        {
            Save();
            var unchanged = models[1].generated.componentInputHash;
            var changed   = models[0].generated.componentInputHash;

            Reopen(whileClosed: () => boxes[0].transform.localPosition += new Vector3(0, 1, 0));

            Assert.That(ComponentHash(models[0]), Is.Not.EqualTo(changed), "the moved brush didn't reach the component hash");
            Assert.That(CompactHierarchyManager.IsTreeUpdateSkipped(models[0].Node), Is.False, "the changed model kept its meshes");
            Assert.That(ComponentHash(models[1]), Is.EqualTo(unchanged), "the model that didn't change hashes differently");
            Assert.That(CompactHierarchyManager.IsTreeUpdateSkipped(models[1].Node), Is.True, "the model that didn't change was built");
        }

        static Hash128 ComponentHash(ChiselModelComponent model)
        {
            return ChiselModelManager.GetComponentInputHash(model);
        }

        [Test]
        public void AModelWithoutATreeHash_StillKeepsItsMeshes()
        {
            Save();
            models[0].generated.inputHash = default;
            MarkSavedMeshes();

            Reopen();

            Assert.That(CompactHierarchyManager.IsTreeUpdateSkipped(models[0].Node), Is.True, "the tree hash still decides");
            Assert.That(IsMarked(models[0]), Is.True, "the model was built even though its input didn't change");
        }

        [Test]
        public void SavingAgain_KeepsTheHashOfAModelThatKeptItsMeshes()
        {
            Save();
            Reopen();
            Assume.That(models[0].generated.componentInputHash.isValid, Is.True, "the first save stored a hash");

            Save();     // what the next session's save does, to a model that has just kept its meshes
            foreach (var model in models)
                Assert.That(model.generated.componentInputHash.isValid, Is.True, $"{model.name} lost its hash on the second save");

            MarkSavedMeshes();
            Reopen();
            Assert.That(IsMarked(models[0]), Is.True, "built again after a second save, although nothing changed");
        }

        [Test]
        public void AModelThatKeepsItsMeshes_BuildsNoNodesAtAll()
        {
            Save();
            MarkSavedMeshes();
            Reopen();
            Assume.That(IsMarked(models[0]), Is.True, "the model kept its saved meshes");

            Assert.That(boxes[0].TopTreeNode.Valid, Is.False,
                        "the generator's CSG node was built for a model that kept its meshes: stages 1 and 2 ran to "
                        + "answer a question the stored hash had already answered");
        }

        [Test]
        public void AModelThatSkippedItsNodes_ComesBackWholeWhenItChanges()
        {
            Save();
            Reopen();
            Assume.That(boxes[0].TopTreeNode.Valid, Is.False, "nothing was skipped, so there is nothing to recover");
            var expected = RenderableWithGeometry(models[1]).sharedMesh.vertexCount;
            Assume.That(expected, Is.GreaterThan(0), "the fixture has to start with geometry to lose");

            boxes[0].transform.localPosition += new Vector3(0, 1, 0);
            ChiselNodeHierarchyManager.UpdateTreeNodeTransformation(boxes[0]);
            Update();

            Assert.That(boxes[0].TopTreeNode.Valid, Is.True, "the edit never reached the generator, which has no node");
            Assert.That(RenderableWithGeometry(models[0]).sharedMesh.vertexCount, Is.EqualTo(expected),
                        "the model came back with the wrong amount of geometry: its CSG ran on a tree whose children "
                        + "were never built");
        }

        [Test]
        public void AModelAskedToBuild_KeepsItsMeshesUntilItsNodesExist()
        {
            Save();
            MarkSavedMeshes();
            Reopen();
            Assume.That(boxes[0].TopTreeNode.Valid, Is.False, "nothing was skipped, so there is no in-between");
            var expected = RenderableWithGeometry(models[0]).sharedMesh.vertexCount;

            ChiselNodeHierarchyManager.UpdateTreeNodeTransformation(boxes[0]);
            ChiselModelManager.Instance.UpdateModels();      // no hierarchy update: nothing has built the children

            Assert.That(RenderableWithGeometry(models[0]).sharedMesh.vertexCount, Is.EqualTo(expected),
                        "the CSG ran on a tree whose children were never built and emptied the model");

            Update();   // and now the ordinary path gets to it
            Assert.That(boxes[0].TopTreeNode.Valid, Is.True, "the model never got its nodes at all");
            Assert.That(IsMarked(models[0]), Is.False, "the edit was dropped: the model still has its saved meshes");
        }

        // Rebuilding is asking for everything to be built
        [Test]
        public void ARebuild_BuildsEveryModel()
        {
            Save();
            MarkSavedMeshes();
            ChiselNodeHierarchyManager.Rebuild();
            foreach (var model in models)
                Assert.That(IsMarked(model), Is.False, model.name);
        }
    }
}
