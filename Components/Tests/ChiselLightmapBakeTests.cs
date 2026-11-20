using System.Collections.Generic;
using Chisel.Core;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using static Chisel.Components.Tests.ChiselBakeScene;

namespace Chisel.Components.Tests
{
    // Chisel's lightmaps with real bakes, each test in a bake scene of its own (ChiselBakeScene): what a bake gives the
    // models' renderers, what edits after it take off and undo gives back, baking again, and saving and opening the scene
    [TestFixture]
    public class ChiselLightmapBakeTests
    {
        const int kTexelsPerUnit = 4;

        readonly List<GameObject> roots = new();
        ChiselBakeScene bakeScene;

        [SetUp]
        public void SetUp()
        {
            if (Object.FindObjectsByType<ChiselModelComponent>(FindObjectsInactive.Include).Length > 0)
                Assert.Ignore("A Chisel model is loaded; run these in a scene without one");
            bakeScene = new ChiselBakeScene(kTexelsPerUnit);
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
            bakeScene?.Dispose();
            bakeScene = null;
        }

        ChiselModelComponent Model(string name, bool lightmapped)
        {
            var root = new GameObject(name);
            roots.Add(root);
            var model = root.AddComponent<ChiselModelComponent>();
            if (lightmapped)
                Lightmapped(model);
            else
            {
                GameObjectUtility.SetStaticEditorFlags(model.gameObject, StaticEditorFlags.ContributeGI);
                model.RenderSettings.receiveGI = ReceiveGI.LightProbes;
                model.SyncModelSettingsStore();
            }
            return model;
        }

        static ChiselBoxComponent Box(ChiselModelComponent model, string name, float3 min, float3 max)
        {
            var go = new GameObject(name);
            go.transform.SetParent(model.transform, false);
            var box = go.AddComponent<ChiselBoxComponent>();
            box.Max = new float3(1e4f);
            box.Min = min;
            box.Max = max;
            UpdateModels();
            return box;
        }

        // A lit scene: a lightmapped model with a box at the origin, a box touching it and a box in another chunk (32 m
        // away), and a model lit by light probes; a lamp over each
        ChiselModelComponent Scene(out ChiselBoxComponent near, out ChiselBoxComponent touching, out ChiselBoxComponent far,
                                   out ChiselModelComponent probeLit)
        {
            var model = Model("lightmapped", lightmapped: true);
            near     = Box(model, "near",     new float3(0, 0, 0),  new float3(2, 2, 2));
            touching = Box(model, "touching", new float3(2, 0, 0),  new float3(3, 1, 1));
            far      = Box(model, "far",      new float3(40, 0, 0), new float3(42, 2, 2));
            probeLit = Model("lit by light probes", lightmapped: false);
            Box(probeLit, "box", new float3(0, 0, 10), new float3(2, 2, 12));
            Lamp(new Vector3(1, 4, 1));
            Lamp(new Vector3(41, 4, 1));
            var probes = new GameObject("probes");
            roots.Add(probes);
            probes.AddComponent<LightProbeGroup>().probePositions = new[]
            {
                new Vector3(-1, 3, 9), new Vector3(3, 3, 9), new Vector3(-1, 3, 13), new Vector3(3, 3, 13),
                new Vector3(-1, -1, 9), new Vector3(3, -1, 9), new Vector3(-1, -1, 13), new Vector3(3, -1, 13)
            };
            return model;
        }

        Light Lamp(Vector3 at)
        {
            var go = new GameObject("lamp");
            roots.Add(go);
            go.transform.position = at;
            var lamp = go.AddComponent<Light>();
            lamp.type             = LightType.Point;
            lamp.range            = 10;
            lamp.intensity        = 3;
            lamp.lightmapBakeType = LightmapBakeType.Baked;
            return lamp;
        }

        static bool IsNear(ChiselRenderObjects renderable) => renderable.sharedMesh.bounds.center.x < 20;

        static ChiselRenderObjects Only(ChiselModelComponent model, bool near)
        {
            var found = Renderables(model).FindAll(renderable => IsNear(renderable) == near);
            Assert.That(found.Count, Is.EqualTo(1), $"the model has one renderer {(near ? "near the origin" : "in the far chunk")}");
            return found[0];
        }

        static bool HasLightmap(MeshRenderer renderer) => renderer.lightmapIndex >= 0 && renderer.lightmapIndex < LightmapSettings.lightmaps.Length;

        // Its lightmap uvs inside its part of its lightmap, and as many texels to a unit as the model asks for
        static void AssertFitsItsLightmap(ChiselRenderObjects renderable, float texelsPerUnit)
        {
            var renderer = renderable.meshRenderer;
            Assert.That(HasLightmap(renderer), Is.True, $"{renderable.sharedMesh.name} has a lightmap ({BakeState(renderer)})");
            var lightmap = LightmapSettings.lightmaps[renderer.lightmapIndex].lightmapColor;
            Assert.That(lightmap, Is.Not.Null, $"its lightmap's texture ({BakeState(renderer)})");
            var scaleOffset = renderer.lightmapScaleOffset;
            var mesh      = renderable.sharedMesh;
            var positions = mesh.vertices;
            var uv2       = mesh.uv2;
            Assert.That(uv2.Length, Is.EqualTo(positions.Length), "it has lightmap uvs");
            foreach (var uv in uv2)
            {
                var inLightmap = new Vector2(uv.x * scaleOffset.x + scaleOffset.z, uv.y * scaleOffset.y + scaleOffset.w);
                Assert.That(inLightmap.x >= -1e-4f && inLightmap.x <= 1 + 1e-4f && inLightmap.y >= -1e-4f && inLightmap.y <= 1 + 1e-4f, Is.True,
                            $"{inLightmap} is outside its lightmap");
            }
            // texels across the whole mesh against its area: the charts are laid out at the model's density
            double texels = 0, area = 0;
            var triangles = mesh.triangles;
            for (int t = 0; t < triangles.Length; t += 3)
            {
                int a = triangles[t], b = triangles[t + 1], c = triangles[t + 2];
                area += Vector3.Cross(positions[b] - positions[a], positions[c] - positions[a]).magnitude * 0.5;
                var ab = (uv2[b] - uv2[a]) * new Vector2(scaleOffset.x * lightmap.width, scaleOffset.y * lightmap.height);
                var ac = (uv2[c] - uv2[a]) * new Vector2(scaleOffset.x * lightmap.width, scaleOffset.y * lightmap.height);
                texels += Mathf.Abs(ab.x * ac.y - ab.y * ac.x) * 0.5;
            }
            var density = math.sqrt(texels / area);
            Assert.That(density, Is.EqualTo(texelsPerUnit).Within(texelsPerUnit * 0.25), $"{renderable.sharedMesh.name}: texels to a unit");
        }

        // Every renderer of a lightmapped model gets a lightmap its uvs fit in, at the model's density; a model lit by
        // light probes gets none
        [Test]
        public void ABake_GivesEveryLightmappedRendererALightmapItFits()
        {
            var model = Scene(out _, out _, out _, out var probeLit);
            Bake();
            var renderables = Renderables(model);
            Assert.That(renderables.Count, Is.EqualTo(2), "a renderer in each chunk");
            foreach (var renderable in renderables)
                AssertFitsItsLightmap(renderable, kTexelsPerUnit * model.RenderSettings.scaleInLightmap);
            foreach (var renderable in Renderables(probeLit))
                Assert.That(HasLightmap(renderable.meshRenderer), Is.False, "the model lit by light probes has no lightmap");
        }

        public enum Edit { Move, Resize, Add, Remove, Operation, Material }

        static void Apply(Edit edit, ChiselModelComponent model, ChiselBoxComponent near, ChiselBoxComponent touching)
        {
            Undo.IncrementCurrentGroup();
            switch (edit)
            {
                case Edit.Move:
                    Undo.RecordObject(near.transform, "Move");
                    near.transform.position += new Vector3(0, 0, 0.5f);
                    ChiselNodeHierarchyManager.UpdateTreeNodeTransformation(near);
                    break;
                case Edit.Resize:
                    Undo.RecordObject(near, "Resize");
                    near.Max = new float3(2, 3, 2);
                    near.OnValidate();
                    break;
                case Edit.Add:
                {
                    var go = new GameObject("added");
                    go.transform.SetParent(model.transform, false);
                    Undo.RegisterCreatedObjectUndo(go, "Add");
                    var box = go.AddComponent<ChiselBoxComponent>();
                    box.Max = new float3(1e4f);
                    box.Min = new float3(0, 2, 0);
                    box.Max = new float3(1, 3, 1);
                    break;
                }
                case Edit.Remove:
                    Undo.DestroyObjectImmediate(touching.gameObject);
                    break;
                case Edit.Operation:
                    Undo.RecordObject(touching, "Operation");
                    touching.Operation = CSGOperationType.Subtractive;
                    break;
                case Edit.Material:
                {
                    Undo.RecordObject(near, "Material");
                    foreach (var surface in near.SurfaceDefinition.surfaces)
                        surface.SetMaterial(ChiselProjectSettings.DefaultFloorMaterial);
                    near.OnValidate();
                    break;
                }
            }
            UpdateModels();
        }

        // An edit after a bake takes the lightmap off the renderer it changes, which no longer fits it, and leaves the
        // others theirs; undoing it gives it back, where it was in its lightmap
        [Test]
        public void AnEditAfterABake_TakesOffOnlyTheChangedRenderersLightmap_UndoGivesItBack([Values] Edit edit)
        {
            var model = Scene(out var near, out var touching, out _, out _);
            Bake();
            var nearBefore = Only(model, near: true).meshRenderer;
            var farBefore  = Only(model, near: false).meshRenderer;
            Assert.That(HasLightmap(nearBefore) && HasLightmap(farBefore), Is.True,
                        $"baked, both renderers have a lightmap (near: {BakeState(nearBefore)}; far: {BakeState(farBefore)})");
            var nearLightmap = (nearBefore.lightmapIndex, nearBefore.lightmapScaleOffset);
            var farLightmap  = (farBefore.lightmapIndex, farBefore.lightmapScaleOffset);

            Apply(edit, model, near, touching);
            foreach (var renderable in Renderables(model))
            {
                if (IsNear(renderable))
                    Assert.That(HasLightmap(renderable.meshRenderer), Is.False, $"{edit}: the changed renderer kept a lightmap that doesn't fit it");
            }
            var far = Only(model, near: false).meshRenderer;
            Assert.That((far.lightmapIndex, far.lightmapScaleOffset), Is.EqualTo(farLightmap), $"{edit}: the unchanged renderer keeps its lightmap");

            Undo.PerformUndo();
            UpdateModels();
            var nearAfter = Only(model, near: true).meshRenderer;
            Assert.That((nearAfter.lightmapIndex, nearAfter.lightmapScaleOffset), Is.EqualTo(nearLightmap), $"{edit}: undone, the lightmap is back");
            far = Only(model, near: false).meshRenderer;
            Assert.That((far.lightmapIndex, far.lightmapScaleOffset), Is.EqualTo(farLightmap), $"{edit}: the unchanged renderer still has its lightmap");
        }

        // Baking again after an edit gives the changed renderer a lightmap that fits its new mesh, and the lightmap it had
        // before isn't given back when the edit is undone: it belongs to the bake before
        [Test]
        public void BakingAgainAfterAnEdit_FitsTheNewMesh_AndKeepsNothingStale()
        {
            var model = Scene(out var near, out var touching, out _, out _);
            Bake();
            Apply(Edit.Resize, model, near, touching);
            Bake();
            foreach (var renderable in Renderables(model))
                AssertFitsItsLightmap(renderable, kTexelsPerUnit * model.RenderSettings.scaleInLightmap);

            Undo.PerformUndo();
            UpdateModels();
            Assert.That(HasLightmap(Only(model, near: true).meshRenderer), Is.False, "the lightmap of the bake before isn't given back");
        }

        public enum Lighting { NonDirectional, Directional, Shadowmask, Subtractive, LightProbes }

        // A brush hidden, the scene view draws a copy of its renderer without it: lit as Unity lights the renderer, with
        // its lightmap, the lightmap's direction and its shadow mask, or its light probes
        [Test]
        public void ACopyWithoutAHiddenBrush_IsLitAsTheRenderer([Values] Lighting lighting)
        {
            var mixed = lighting == Lighting.Shadowmask || lighting == Lighting.Subtractive;
            bakeScene.settings.directionalityMode = lighting == Lighting.Directional ? LightmapsMode.CombinedDirectional : LightmapsMode.NonDirectional;
            bakeScene.settings.mixedBakeMode      = lighting == Lighting.Subtractive ? MixedLightingMode.Subtractive : MixedLightingMode.Shadowmask;
            var shadowmaskMode = QualitySettings.shadowmaskMode;
            var model  = Model("model", lightmapped: lighting != Lighting.LightProbes);
            // all in one chunk, so all drawn through copies
            Box(model, "floor", new float3(0, 0, 0), new float3(6, 0.5f, 6));
            // to one side, so the floor's middle, where a renderer lit by light probes takes them, is lit
            Box(model, "pillar", new float3(4, 0.5f, 1), new float3(5, 3, 2));
            // out of the camera's sight
            var hidden = Box(model, "hidden", new float3(12, 0, 12), new float3(13, 1, 13));
            var lamp   = Lamp(new Vector3(1, 2.5f, 1));
            lamp.intensity        = 8;
            lamp.shadows          = LightShadows.Hard;
            lamp.lightmapBakeType = mixed ? LightmapBakeType.Mixed : LightmapBakeType.Baked;
            // above the floor: a probe inside a brush sees only its inside
            if (lighting == Lighting.LightProbes)
                Probes(new Vector3(-1, 0.75f, -1), new Vector3(7, 3.5f, 7), 4);
            var camera = Looking(roots, new Vector3(3.5f, 4, 3.5f), Vector3.down);
            try
            {
                QualitySettings.shadowmaskMode = ShadowmaskMode.Shadowmask;
                Bake();
                // a baked lamp only lights through the bake; a mixed one is drawn live as well
                lamp.enabled = mixed;
                var renderables = Renderables(model);
                foreach (var renderable in renderables)
                {
                    var renderer = renderable.meshRenderer;
                    if (lighting == Lighting.LightProbes)
                    {
                        Assert.That(HasLightmap(renderer), Is.False, "lit by light probes, not a lightmap");
                        continue;
                    }
                    Assert.That(HasLightmap(renderer), Is.True, BakeState(renderer));
                    var baked = LightmapSettings.lightmaps[renderer.lightmapIndex];
                    if (lighting == Lighting.Directional)
                        Assert.That(baked.lightmapDir, Is.Not.Null, "the bake has a direction");
                    if (lighting == Lighting.Shadowmask)
                        Assert.That(baked.shadowMask, Is.Not.Null, "the bake has a shadow mask");
                }

                if (lighting == Lighting.LightProbes)
                    Assert.That(ProbeBrightness(new Vector3(3.5f, 1, 3.5f)), Is.GreaterThan(0.01f),
                                $"the light probes are lit ({LightmapSettings.lightProbes?.count ?? 0} probes, renderer probe usage {renderables[0].meshRenderer.lightProbeUsage})");
                var itself = InTheDark(() => Brightness(camera));
                Assert.That(itself, Is.GreaterThan(0.02f), $"the floor is lit ({BakeState(renderables[0].meshRenderer)})");

                UnityEditor.SceneVisibilityManager.instance.Hide(hidden.gameObject, false);
                RefreshVisibility();
                foreach (var renderable in renderables)
                {
                    Assert.That(renderable.DrawnMesh, Is.SameAs(renderable.partialMesh), "drawn through a copy without the hidden brush");
                    Assert.That(renderable.meshRenderer.forceRenderingOff, Is.True, "a brush hidden, the copy is drawn instead of the renderer");
                }
                var copy = InTheDark(() =>
                {
                    ChiselModelManager.Instance.OnRenderModels(camera, DrawModeFlags.None);
                    return Brightness(camera);
                });
                Assert.That(copy, Is.EqualTo(itself).Within(itself * 0.02f), $"{lighting}: the copy is lit otherwise than the renderer");
            }
            finally
            {
                QualitySettings.shadowmaskMode = shadowmaskMode;
                UnityEditor.SceneVisibilityManager.instance.Show(hidden.gameObject, false);
                RefreshVisibility();
            }
        }

        static void RefreshVisibility()
        {
            ChiselUnityVisibilityManager.UpdateVisibility(force: true);
            ChiselUnityVisibilityManager.Update();
        }

        // Light probes on a grid, from one corner to the other
        void Probes(Vector3 from, Vector3 to, int steps)
        {
            var go = new GameObject("probes");
            roots.Add(go);
            var positions = new List<Vector3>();
            for (int x = 0; x <= steps; x++)
                for (int y = 0; y <= steps; y++)
                    for (int z = 0; z <= steps; z++)
                        positions.Add(from + Vector3.Scale(to - from, new Vector3(x, y, z) / steps));
            go.AddComponent<LightProbeGroup>().probePositions = positions.ToArray();
        }

        // Saved and opened again, the models' meshes are built again: they still fit the lightmaps they were baked with
        [Test]
        public void SavedAndOpenedAgain_TheLightmapsStillFit()
        {
            var model = Scene(out _, out _, out _, out _);
            Bake();
            var before = new Dictionary<bool, (int, Vector4, Vector2[])>();
            foreach (var renderable in Renderables(model))
                before[IsNear(renderable)] = (renderable.meshRenderer.lightmapIndex, renderable.meshRenderer.lightmapScaleOffset, renderable.sharedMesh.uv2);

            var scene = EditorSceneManager.GetActiveScene();
            Assert.That(EditorSceneManager.SaveScene(scene), Is.True, "saved");
            roots.Clear();
            scene = EditorSceneManager.OpenScene(scene.path, OpenSceneMode.Single);
            UpdateModels();

            ChiselModelComponent reopened = null;
            foreach (var found in Object.FindObjectsByType<ChiselModelComponent>(FindObjectsInactive.Exclude))
            {
                if (found.RenderSettings.receiveGI == ReceiveGI.Lightmaps)
                    reopened = found;
            }
            Assert.That(reopened, Is.Not.Null, "the model is there again");
            var renderables = Renderables(reopened);
            Assert.That(renderables.Count, Is.EqualTo(before.Count));
            foreach (var renderable in renderables)
            {
                var (index, scaleOffset, uv2) = before[IsNear(renderable)];
                Assert.That(renderable.meshRenderer.lightmapIndex, Is.EqualTo(index), "the same lightmap");
                Assert.That(renderable.meshRenderer.lightmapScaleOffset, Is.EqualTo(scaleOffset), "where it was in it");
                Assert.That(renderable.sharedMesh.uv2, Is.EqualTo(uv2), "the mesh built again has the uvs it was baked with");
                AssertFitsItsLightmap(renderable, kTexelsPerUnit * reopened.RenderSettings.scaleInLightmap);
            }
        }
    }
}
