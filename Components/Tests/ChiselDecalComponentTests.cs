using System.Collections.Generic;
using Chisel.Core;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Chisel.Components.Tests
{
    [TestFixture]
    public class ChiselDecalComponentTests
    {
        readonly List<Object> created = new();
        Material decalMaterial;

        [SetUp]
        public void SetUp()
        {
            if (Object.FindObjectsByType<ChiselModelComponent>(FindObjectsInactive.Include).Length > 0)
                Assert.Ignore("A Chisel model is loaded; run these in a scene without one");
            decalMaterial = new Material(Shader.Find("Standard")) { name = "DecalTestMaterial" };
            created.Add(decalMaterial);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var item in created)
            {
                if (item)
                    Object.DestroyImmediate(item);
            }
            created.Clear();
            Update();
            DeleteSheets();
        }

        const string kSheetFolder = "Assets/DecalSheetTests";
        static readonly List<Object> s_Layouts = new();

        // A material asset (metadata needs one) whose hotspot layout has tile 1 at u 0.5 to 1, v 0 to 0.25
        internal static Material SheetMaterial(Material source)
        {
            var layout = ScriptableObject.CreateInstance<ChiselHotspotLayout>();
            s_Layouts.Add(layout);
            layout.size  = new Vector2Int(4, 4);
            // rows run down from the top: v 0 to 0.25 is the bottom row
            layout.tiles = new List<HotspotTile> { new HotspotTile { id = 1, name = "Trim", points = new[] { new Vector2Int(2, 3), new Vector2Int(4, 3), new Vector2Int(4, 4), new Vector2Int(2, 4) } } };
            if (!AssetDatabase.IsValidFolder(kSheetFolder))
                AssetDatabase.CreateFolder("Assets", kSheetFolder.Substring("Assets/".Length));
            var material = new Material(source);
            AssetDatabase.CreateAsset(material, AssetDatabase.GenerateUniqueAssetPath(kSheetFolder + "/sheet.mat"));
            material.AddMetadataOfType<ChiselSurfaceMetadata>().hotspotLayout = layout;
            return material;
        }

        internal static void DeleteSheets()
        {
            if (AssetDatabase.IsValidFolder(kSheetFolder))
                AssetDatabase.DeleteAsset(kSheetFolder);
            foreach (var layout in s_Layouts)
            {
                if (layout)
                    Object.DestroyImmediate(layout);
            }
            s_Layouts.Clear();
        }

        static void Update()
        {
            for (int i = 0; i < 3; i++)
            {
                ChiselNodeHierarchyManager.Update();
                ChiselModelManager.Instance.UpdateModels();
            }
        }

        ChiselModelComponent Floors(out ChiselBoxComponent a, out ChiselBoxComponent b, out ChiselDecalComponent decal)
        {
            var root = new GameObject("decal test");
            created.Add(root);
            var model = root.AddComponent<ChiselModelComponent>();
            a = Box("A", model.transform, new Vector3(-1, -1, -0.5f));
            b = Box("B", model.transform, new Vector3(0, -1, -0.5f));

            var decalObject = new GameObject("Decal");
            decalObject.transform.SetParent(model.transform, false);
            decalObject.transform.rotation = Quaternion.LookRotation(Vector3.down, Vector3.forward);
            decal = decalObject.AddComponent<ChiselDecalComponent>();
            decal.Material    = decalMaterial;
            decal.Size        = new Vector3(1, 1, 0.5f);
            decal.Transparent = false;
            return model;
        }

        static ChiselBoxComponent Box(string name, Transform parent, Vector3 localPosition)
        {
            var gameObject = new GameObject(name);
            gameObject.transform.SetParent(parent, false);
            gameObject.transform.localPosition = localPosition;
            return gameObject.AddComponent<ChiselBoxComponent>();
        }

        // The area of the upward triangles drawn with the decal's material, and of the others
        (double decal, double other) UpAreas(ChiselModelComponent model)
        {
            double decal = 0, other = 0;
            if (model.generated?.renderables == null)
                return (decal, other);
            foreach (var r in model.generated.renderables)
            {
                if (r == null || !r.Valid || r.sharedMesh == null || r.sharedMesh.vertexCount == 0)
                    continue;
                const SurfaceDestinationFlags kDrawsAndCasts = SurfaceDestinationFlags.RenderShadowReceiveAndCasting;
                if ((r.query & kDrawsAndCasts) != kDrawsAndCasts)
                    continue;
                var vertices = r.sharedMesh.vertices;
                for (int s = 0; s < r.sharedMesh.subMeshCount; s++)
                {
                    var isDecal = r.renderMaterials != null && s < r.renderMaterials.Length && r.renderMaterials[s] == decalMaterial;
                    var triangles = r.sharedMesh.GetTriangles(s);
                    for (int t = 0; t < triangles.Length; t += 3)
                    {
                        var normal = Vector3.Cross(vertices[triangles[t + 1]] - vertices[triangles[t]], vertices[triangles[t + 2]] - vertices[triangles[t]]);
                        if (normal.normalized.y < 0.99f)
                            continue;
                        if (isDecal) decal += normal.magnitude * 0.5;
                        else         other += normal.magnitude * 0.5;
                    }
                }
            }
            return (decal, other);
        }

        static ChiselDecalSurfaceTarget Target(ChiselNodeComponent brush, int surface)
        {
            return new ChiselDecalSurfaceTarget { brush = brush, surfaceIndex = surface };
        }

        const double kTolerance = 1e-4;

        // The decals of each stacking order, as the tree stacks them (lowest first)
        static List<ulong> Stacked(ChiselModelComponent model)
        {
            var data = ChiselTreeLookup.Value[model.Node];
            data.lastJobHandle.Complete();
            var stacked = new List<ulong>();
            foreach (var volume in data.decalVolumes)
                stacked.Add(volume.entityID);
            return stacked;
        }

        static ulong ID(Object item) => EntityId.ToULong(item.GetEntityId());

        // Decals of one order stack in hierarchy order, the last on top, the same every time a scene is loaded
        [Test]
        public void Decals_StackInHierarchyOrder()
        {
            var model = Floors(out _, out _, out var first);
            var secondObject = Object.Instantiate(first.gameObject, model.transform);
            secondObject.name = "Decal 2";
            var second = secondObject.GetComponent<ChiselDecalComponent>();
            Update();
            Assert.That(Stacked(model), Is.EqualTo(new[] { ID(first), ID(second) }));

            secondObject.transform.SetSiblingIndex(first.transform.GetSiblingIndex());
            ChiselDecalManager.HierarchyChanged();
            Update();
            Assert.That(Stacked(model), Is.EqualTo(new[] { ID(second), ID(first) }), "moved up, it is under the other");
        }

        // Transparent decals of other materials, overlapping: one mesh, a sub-mesh per material in hierarchy order, so
        // they draw stacked as the hierarchy has them
        ChiselDecalComponent TransparentDecal(ChiselModelComponent model, string name, Material material, Vector3 position)
        {
            var decalObject = new GameObject(name);
            decalObject.transform.SetParent(model.transform, false);
            decalObject.transform.localPosition = position;
            decalObject.transform.rotation = Quaternion.LookRotation(Vector3.down, Vector3.forward);
            var decal = decalObject.AddComponent<ChiselDecalComponent>();
            decal.Material    = material;
            decal.Size        = new Vector3(1, 1, 0.5f);
            decal.Transparent = true;
            return decal;
        }

        // The materials of each chunk's transparent decals' renderer, in sub-mesh order (the decals here straddle x = 0,
        // so they are in two 32 m cells)
        static void AssertTransparentDecalsDrawn(ChiselModelComponent model, Material[] expected, string message = null)
        {
            var found = 0;
            foreach (var r in model.generated.renderables)
            {
                if (r == null || !r.Valid || r.surfaceParameter != ChiselDecalMeshes.kTransparentDecals)
                    continue;
                Assert.That(r.renderMaterials, Is.EqualTo(expected), message);
                found++;
            }
            Assert.That(found, Is.GreaterThan(0), "the transparent decals have no renderer of their own");
        }

        [Test]
        public void TransparentDecalsOfOtherMaterials_DrawInHierarchyOrder()
        {
            var model = Floors(out _, out _, out var first);
            first.Transparent = true;
            var other = new Material(decalMaterial) { name = "OtherDecalTestMaterial" };
            created.Add(other);
            var second = TransparentDecal(model, "Decal 2", other, new Vector3(0.25f, 0, 0));
            Update();
            AssertTransparentDecalsDrawn(model, new[] { decalMaterial, other }, "lowest first");

            second.transform.SetSiblingIndex(first.transform.GetSiblingIndex());
            ChiselDecalManager.HierarchyChanged();
            Update();
            AssertTransparentDecalsDrawn(model, new[] { other, decalMaterial }, "moved up, it is under the other");
        }

        // A material between two decals of another is drawn between them
        [Test]
        public void TransparentDecals_StackAcrossMaterials()
        {
            var model = Floors(out _, out _, out var first);
            first.Transparent = true;
            var other = new Material(decalMaterial) { name = "OtherDecalTestMaterial" };
            created.Add(other);
            TransparentDecal(model, "Decal 2", other, new Vector3(0.25f, 0, 0));
            TransparentDecal(model, "Decal 3", decalMaterial, new Vector3(-0.25f, 0, 0));
            Update();
            AssertTransparentDecalsDrawn(model, new[] { decalMaterial, other, decalMaterial });
        }

        // The texture coordinates of the triangles drawn with the decal's material
        List<Vector2> DecalUVs(ChiselModelComponent model)
        {
            var found = new List<Vector2>();
            foreach (var r in model.generated.renderables)
            {
                if (r == null || !r.Valid || r.sharedMesh == null || r.sharedMesh.vertexCount == 0 || (r.query & SurfaceDestinationFlags.Renderable) == 0)
                    continue;
                var uvs = r.sharedMesh.uv;
                for (int s = 0; s < r.sharedMesh.subMeshCount; s++)
                {
                    if (r.renderMaterials == null || s >= r.renderMaterials.Length || r.renderMaterials[s] != decalMaterial)
                        continue;
                    foreach (var index in r.sharedMesh.GetTriangles(s))
                        found.Add(uvs[index]);
                }
            }
            return found;
        }

        static Vector2 Range(List<Vector2> uvs, out Vector2 min)
        {
            min = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
            var max = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
            foreach (var uv in uvs)
            {
                min = Vector2.Min(min, uv);
                max = Vector2.Max(max, uv);
            }
            return max - min;
        }

        // The whole texture repeats by wrapping: one decal, its image 2 by 3 times across it
        [Test]
        public void ARepeatedDecal_RepeatsItsImageAcrossTheBox()
        {
            var model = Floors(out _, out _, out var decal);
            decal.Repeat = new Vector2(2, 3);
            Update();
            Assert.That(model.Node.GetDecalCount(), Is.EqualTo(1));
            var range = Range(DecalUVs(model), out _);
            Assert.That(range.x, Is.EqualTo(2).Within(1e-4f));
            Assert.That(range.y, Is.EqualTo(3).Within(1e-4f));
        }

        // An image in a trim sheet can't wrap: a tile per image, every one inside its rect, the last one cut short
        [Test]
        public void ARepeatedSheetImage_IsATilePerImage_InsideItsRect()
        {
            var model = Floors(out _, out _, out var decal);
            decal.Material  = decalMaterial = SheetMaterial(decalMaterial);
            decal.ImageTile = 1;
            Assert.That(decal.ImageRect, Is.EqualTo(new Rect(0.5f, 0, 0.5f, 0.25f)));
            decal.Repeat = new Vector2(2.5f, 1);
            Update();
            Assert.That(model.Node.GetDecalCount(), Is.EqualTo(3), "two whole images and half of one");
            Assert.That(UpAreas(model).decal, Is.EqualTo(1.0).Within(kTolerance), "the whole box still");
            foreach (var uv in DecalUVs(model))
            {
                Assert.That(uv.x, Is.InRange(0.5f - 1e-4f, 1 + 1e-4f), $"u {uv}");
                Assert.That(uv.y, Is.InRange(-1e-4f, 0.25f + 1e-4f), $"v {uv}");
            }
        }

        [Test]
        public void Decal_IsDrawnOnTheFloors()
        {
            var model = Floors(out _, out _, out _);
            Update();
            Assert.That(model.Node.GetDecalCount(), Is.EqualTo(1));
            var (decal, other) = UpAreas(model);
            Assert.That(decal, Is.EqualTo(1.0).Within(kTolerance));
            Assert.That(other, Is.EqualTo(1.0).Within(kTolerance), "the opaque decal replaces a square metre of the two floors");
        }

        // Nothing tells the manager about a moved transform: it has to see it
        [Test]
        public void MovedDecal_MovesOnTheFloors()
        {
            var model = Floors(out _, out _, out var decal);
            Update();
            Assert.That(UpAreas(model).decal, Is.EqualTo(1.0).Within(kTolerance));

            decal.transform.localPosition = new Vector3(1, 0, 0);
            Update();
            Assert.That(UpAreas(model).decal, Is.EqualTo(0.5).Within(kTolerance), "half of it is past the floors' end");
        }

        [Test]
        public void MovedModel_MovesItsFloorsUnderADecalOutsideIt()
        {
            var model = Floors(out _, out _, out var decal);
            decal.transform.SetParent(null, true);
            created.Add(decal.gameObject);
            Update();
            Assert.That(UpAreas(model).decal, Is.EqualTo(1.0).Within(kTolerance));

            model.transform.position = new Vector3(-1, 0, 0);
            Update();
            Assert.That(UpAreas(model).decal, Is.EqualTo(0.5).Within(kTolerance), "the floors moved half out from under it");
        }

        // Rebuild recreates every tree, and a new tree can get the handle the old one had
        [Test]
        public void Rebuild_KeepsTheDecals()
        {
            var model = Floors(out _, out _, out _);
            Update();
            Assert.That(UpAreas(model).decal, Is.EqualTo(1.0).Within(kTolerance));

            ChiselNodeHierarchyManager.Rebuild();
            Update();
            Assert.That(model.Node.GetDecalCount(), Is.EqualTo(1), "the rebuilt tree has the decal again");
            Assert.That(UpAreas(model).decal, Is.EqualTo(1.0).Within(kTolerance));
        }

        [Test]
        public void Targets_LimitTheDecalToTheirSurfaces()
        {
            // A box's top is its surface 0 and its bottom surface 1 (ChiselBoxBrushMeshFactory.BoxSides)
            const int kTop = 0, kBottom = 1;
            var model = Floors(out var a, out var b, out var decal);

            decal.SetTargets(new[] { Target(a, ChiselDecalSurfaceTarget.kAllSurfaces) });
            Update();
            var areas = UpAreas(model);
            Assert.That(areas.decal, Is.EqualTo(0.5).Within(kTolerance), "on A only");
            Assert.That(areas.other, Is.EqualTo(1.5).Within(kTolerance), "B keeps its top");

            decal.SetTargets(new[] { Target(b, kTop) });
            Update();
            Assert.That(UpAreas(model).decal, Is.EqualTo(0.5).Within(kTolerance), "on B's top");

            // the second is a trim's, kept for switching back: a box decal only takes the first
            decal.SetTargets(new[] { Target(b, kTop), Target(a, ChiselDecalSurfaceTarget.kAllSurfaces) });
            Update();
            Assert.That(UpAreas(model).decal, Is.EqualTo(0.5).Within(kTolerance), "on B's top only");

            decal.SetTargets(new[] { Target(b, kBottom) });
            Update();
            areas = UpAreas(model);
            Assert.That(areas.decal, Is.EqualTo(0).Within(kTolerance), "B's bottom is out of reach, and nothing else is a target");
            Assert.That(areas.other, Is.EqualTo(2).Within(kTolerance));

            // A deleted target: nothing is drawn, and once the decal is looked at again it is left out altogether
            decal.SetTargets(new[] { Target(a, ChiselDecalSurfaceTarget.kAllSurfaces) });
            Update();
            Object.DestroyImmediate(a.gameObject);
            Update();
            Assert.That(UpAreas(model).decal, Is.EqualTo(0).Within(kTolerance));
            ChiselDecalManager.SetDirty();
            Update();
            Assert.That(model.Node.GetDecalCount(), Is.EqualTo(0));
            Assert.That(UpAreas(model).other, Is.EqualTo(1).Within(kTolerance), "B's whole top");
        }

        [Test]
        public void ARemovedModel_LeavesNoMeshLoaded_AfterTheUpdateThatFindsItGone()
        {
            ChiselDestroyedModelTests.AssertNoneLoadedAfterAnUnload(DrawAndRemoveFloors());
        }

        // In a method of its own, so that the test refers to nothing of the model when the unload runs
        List<(string what, EntityId id)> DrawAndRemoveFloors()
        {
            var model = Floors(out _, out _, out _);
            Update();
            Assert.That(model.Node.GetDecalCount(), Is.EqualTo(1), "no decal drawn: the decal manager never had the model");
            var meshes = new List<(string what, EntityId id)>();
            ChiselDestroyedModelTests.RecordMeshes(model, meshes);
            Assert.That(meshes.Count, Is.GreaterThan(0), "the model made no meshes: nothing is measured");

            var root = model.gameObject;
            created.Remove(root);
            Object.DestroyImmediate(root);
            // One update: the next one would start by clearing what this one kept
            ChiselNodeHierarchyManager.Update();
            ChiselModelManager.Instance.UpdateModels();
            return meshes;
        }

        [Test]
        public void ARemovedDecal_LeavesItsMaterialUnloadable()
        {
            var material = DrawAndRemoveADecalWithAMaterialOfItsOwn();
            System.GC.Collect();
            System.GC.WaitForPendingFinalizers();
            System.GC.Collect();
            EditorUtility.UnloadUnusedAssetsImmediate(true);
            Assert.That((bool)Resources.EntityIdToObject(material), Is.False, "the removed decal's material is still loaded: something still refers to it");
        }

        // In a method of its own, so that the test refers to nothing of the decal or its material when the unload runs.
        // The material is the test's own and never destroyed: only an unload can take it.
        EntityId DrawAndRemoveADecalWithAMaterialOfItsOwn()
        {
            var material = new Material(Shader.Find("Standard")) { name = "Removed Decal Material" };
            var model = Floors(out _, out _, out var decal);
            decal.Material = material;
            Update();
            Assert.That(model.Node.GetDecalCount(), Is.EqualTo(1), "no decal drawn: the decal manager never had the material");

            var root = model.gameObject;
            created.Remove(root);
            Object.DestroyImmediate(root);
            Update();
            return material.GetEntityId();
        }
    }
}
