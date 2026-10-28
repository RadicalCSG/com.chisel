using System.Collections;
using System.Collections.Generic;
using Chisel.Components;
using Chisel.Core;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace Chisel.Editors.Tests
{
    // Entering play mode without a domain reload keeps every static, and without a scene reload every scene object too:
    // nothing of one play session may change the next
    public sealed class ChiselPlayModeWithoutDomainReloadTests
    {
        const string kRootName  = "Play mode without domain reload test";
        const string kSessionKey = "ChiselPlayModeWithoutDomainReloadTests.";

        static void Update()
        {
            for (int i = 0; i < 3; i++)
            {
                ChiselNodeHierarchyManager.Update();
                ChiselModelManager.Instance.UpdateModels();
            }
        }

        // What a model draws and collides with, as numbers that change when its output does
        static string Output(ChiselModelComponent model)
        {
            if (!model || model.generated == null)
                return "no output";
            int renderVertices = 0, renderTriangles = 0, colliders = 0, colliderVertices = 0;
            var bounds = new Bounds();
            var any = false;
            foreach (var renderable in model.generated.renderables)
            {
                if (renderable == null || !renderable.Valid || !renderable.sharedMesh)
                    continue;
                var mesh = renderable.sharedMesh;
                renderVertices += mesh.vertexCount;
                for (int s = 0; s < mesh.subMeshCount; s++)
                    renderTriangles += (int)mesh.GetIndexCount(s) / 3;
                if (mesh.vertexCount == 0)
                    continue;
                if (any) bounds.Encapsulate(mesh.bounds); else bounds = mesh.bounds;
                any = true;
            }
            foreach (var collider in model.generated.colliders)
            {
                if (collider == null || !collider.sharedMesh)
                    continue;
                colliders++;
                colliderVertices += collider.sharedMesh.vertexCount;
            }
            return $"{renderVertices} vertices, {renderTriangles} triangles, {colliders} colliders with {colliderVertices} vertices, bounds {bounds.min:F3} to {bounds.max:F3}";
        }

        static ChiselModelComponent Model() => GameObject.Find(kRootName)?.GetComponent<ChiselModelComponent>();

        static T Add<T>(Transform parent, string name, Vector3 position) where T : ChiselGeneratorComponent
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            return go.AddComponent<T>();
        }

        [UnityTest]
        public IEnumerator EnteringPlayModeTwice_WithoutADomainReload_KeepsTheOutput([Values(true, false)] bool sceneReload)
        {
            if (Object.FindObjectsByType<ChiselModelComponent>(FindObjectsInactive.Include).Length > 0)
                Assert.Ignore("A Chisel model is loaded; run this in a scene without one");

            SessionState.SetBool(kSessionKey + "optionsEnabled", EditorSettings.enterPlayModeOptionsEnabled);
            SessionState.SetInt(kSessionKey + "options", (int)EditorSettings.enterPlayModeOptions);

            var root = new GameObject(kRootName);
            root.AddComponent<ChiselModelComponent>();
            Add<ChiselBoxComponent>(root.transform, "Box", Vector3.zero);
            Add<ChiselCylinderComponent>(root.transform, "Cylinder", new Vector3(4, 0, 0));
            Add<ChiselLinearStairsComponent>(root.transform, "Stairs", new Vector3(-4, 0, 0));
            var hole = Add<ChiselBoxComponent>(root.transform, "Hole", new Vector3(0.5f, 0.5f, 0.5f));
            hole.Operation = CSGOperationType.Subtractive;
            Update();
            var before = Output(Model());

            var seen = new List<string>();
            EditorSettings.enterPlayModeOptionsEnabled = true;
            EditorSettings.enterPlayModeOptions = EnterPlayModeOptions.DisableDomainReload | (sceneReload ? EnterPlayModeOptions.None : EnterPlayModeOptions.DisableSceneReload);
            for (int round = 1; round <= 2; round++)
            {
                yield return new EnterPlayMode();
                yield return null;
                Update();
                seen.Add($"round {round}, in play mode: {Output(Model())}");

                // what is changed in play mode is built too
                var box = Model()?.transform.Find("Box")?.GetComponent<ChiselBoxComponent>();
                if (box)
                {
                    box.Max = box.Max + new Unity.Mathematics.float3(0, 1, 0);
                    Update();
                    seen.Add($"round {round}, a box made taller in play mode: {Output(Model())}");
                }

                yield return new ExitPlayMode();
                Update();
                seen.Add($"round {round}, after play mode: {Output(Model())}");
            }

            EditorSettings.enterPlayModeOptionsEnabled = SessionState.GetBool(kSessionKey + "optionsEnabled", false);
            EditorSettings.enterPlayModeOptions = (EnterPlayModeOptions)SessionState.GetInt(kSessionKey + "options", 0);
            var leftover = GameObject.Find(kRootName);
            if (leftover)
                Object.DestroyImmediate(leftover);
            Update();

            Assert.That(before, Does.Not.StartWith("0 vertices"), "the model draws nothing to start with");
            var taller = seen[1];
            Assert.That(seen, Is.EqualTo(new[]
            {
                $"round 1, in play mode: {before}",
                taller,
                $"round 1, after play mode: {before}",
                $"round 2, in play mode: {before}",
                taller.Replace("round 1", "round 2"),
                $"round 2, after play mode: {before}",
            }));
            Assert.That(taller, Does.Not.EndWith(before), "making the box taller in play mode changed nothing");
        }
    }
}
