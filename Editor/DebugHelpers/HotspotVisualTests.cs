using System.Collections.Generic;
using System.IO;
using System.Text;
using Chisel.Components;
using Chisel.Core;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;

namespace Chisel.Editors
{
    // The visual inspection suite (automated and visual verification):
    // every case of the hotspot test scene (HotspotTestScene) rendered as it is drawn and in the diagnostic view, into
    // HotspotVisualTests beside Assets. An image is named after its station and case, as the test scene's code names
    // them, so the case can be found from the image; each station also has one image of all its cases together (where
    // cases belong together, as connected surfaces do). Nothing is compared: it is for a person to look at.
    static class HotspotVisualTests
    {
        const string kFolderName = "HotspotVisualTests";
        const int    kWidth      = 960;
        const int    kHeight     = 600;

        // Exactly <project>/HotspotVisualTests, or null
        static string Destination()
        {
            var project = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            var folder  = Path.GetFullPath(Path.Combine(project, kFolderName));
            if (!string.Equals(Path.GetDirectoryName(folder), project, System.StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(Path.GetFileName(folder), kFolderName, System.StringComparison.Ordinal))
                return null;
            return folder;
        }

        [MenuItem("Tools/Chisel/Generate Hotspot Visual Tests")]
        public static void Generate()
        {
            var folder = Destination();
            if (folder == null)
            {
                Debug.LogError($"Hotspot visual tests: {kFolderName} is not beside Assets");
                return;
            }
            if (!HotspotTestScene.BuildScene())
                return;
            // the models of the new scene built before they are drawn
            for (int i = 0; i < 3; i++)
            {
                ChiselNodeHierarchyManager.Update();
                ChiselModelManager.Instance.UpdateModels();
            }
            Directory.CreateDirectory(folder);
            foreach (var file in Directory.GetFiles(folder))
                File.Delete(file);
            foreach (var directory in Directory.GetDirectories(folder))
                Directory.Delete(directory, true);

            var index = new StringBuilder();
            index.Append("Hotspot visual tests: each case of Editor/DebugHelpers/HotspotTestScene.cs, as drawn and in the diagnostic view (distortion)\n\n");
            // in the scene, so the models draw for it; removed again below
            var cameraObject = new GameObject("hotspot visual test camera");
            var texture = new RenderTexture(kWidth, kHeight, 24) { antiAliasing = 4 };
            var image = new Texture2D(kWidth, kHeight, TextureFormat.RGB24, false);
            try
            {
                var camera = cameraObject.AddComponent<Camera>();
                camera.fieldOfView     = 40;
                camera.nearClipPlane   = 0.02f;
                camera.farClipPlane    = 500;
                camera.clearFlags      = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(0.35f, 0.38f, 0.42f);
                camera.targetTexture   = texture;

                var count = 0;
                foreach (var (station, generators) in Stations())
                {
                    if (!Frame(camera, generators))
                        continue;
                    var name = Sanitize(StationNumber(station) + " station");
                    Render(camera, image, Path.Combine(folder, name + ".png"), HotspotDiagnosticMode.Off);
                    Render(camera, image, Path.Combine(folder, name + ".distortion.png"), HotspotDiagnosticMode.Distortion);
                    index.Append(name).Append(".png\t").Append(station).Append(" (all its cases)\n");
                }
                foreach (var (station, generator) in Cases())
                {
                    if (!Frame(camera, new[] { generator }))
                        continue;
                    var name = Sanitize(StationNumber(station) + " " + generator.name);
                    Render(camera, image, Path.Combine(folder, name + ".png"), HotspotDiagnosticMode.Off);
                    var summary = Render(camera, image, Path.Combine(folder, name + ".distortion.png"), HotspotDiagnosticMode.Distortion);
                    if (count == 0)
                        index.Append($"The diagnostic view found {summary.surfaces} surfaces, {summary.triangles} triangles; energy up to {summary.mostEnergy:0.00}\n\n");
                    index.Append(name).Append(".png\t").Append(station).Append(" / ").Append(generator.name).Append('\n');
                    count++;
                }
                File.WriteAllText(Path.Combine(folder, "index.txt"), index.ToString());
                Debug.Log($"Hotspot visual tests: {count} cases in {folder}");
            }
            finally
            {
                ChiselHotspotDiagnosticsView.ClearCaptures();
                Object.DestroyImmediate(image);
                RenderTexture.active = null;
                if (cameraObject.TryGetComponent<Camera>(out var used))
                    used.targetTexture = null;
                Object.DestroyImmediate(texture);
                Object.DestroyImmediate(cameraObject);
            }
        }

        // The generators of each station of the test scene, in its order
        static IEnumerable<(string station, ChiselGeneratorComponent generator)> Cases()
        {
            var model = Object.FindAnyObjectByType<ChiselModelComponent>();
            if (model == null)
                yield break;
            foreach (Transform station in model.transform)
            {
                foreach (Transform child in station)
                {
                    var generator = child.GetComponent<ChiselGeneratorComponent>();
                    // the brushes subtracted from a case are part of it
                    if (generator != null && generator.Operation != CSGOperationType.Subtractive)
                        yield return (station.name, generator);
                }
            }
        }

        // Each station of the test scene with its cases
        static IEnumerable<(string station, List<ChiselGeneratorComponent> generators)> Stations()
        {
            string current = null;
            var generators = new List<ChiselGeneratorComponent>();
            foreach (var (station, generator) in Cases())
            {
                if (current != null && station != current)
                {
                    yield return (current, generators);
                    generators = new List<ChiselGeneratorComponent>();
                }
                current = station;
                generators.Add(generator);
            }
            if (current != null)
                yield return (current, generators);
        }

        static string StationNumber(string station)
        {
            var space = station.IndexOf(' ');
            return space > 0 ? station.Substring(0, space) : station;
        }

        static string Sanitize(string name)
        {
            var builder = new StringBuilder(name.Length);
            foreach (var c in name)
                builder.Append(char.IsLetterOrDigit(c) || c == '.' || c == '-' ? c : (c == ' ' ? ' ' : '_'));
            return builder.ToString().Trim();
        }

        // The camera in front of the cases, a little above and to the left, the whole of them in view
        static bool Frame(Camera camera, IEnumerable<ChiselGeneratorComponent> generators)
        {
            var bounds = new Bounds();
            var any = false;
            var brushes = new HashSet<CSGTreeBrush>();
            foreach (var generator in generators)
            {
                brushes.Clear();
                ChiselModelManager.Instance.GetAllTreeBrushes(generator, brushes);
                var modelMatrix = ChiselNodeHierarchyManager.FindModelTransformMatrixOfTransform(generator.transform);
                foreach (var brush in brushes)
                {
                    if (!brush.Valid)
                        continue;
                    var box = brush.GetBounds(modelMatrix * (Matrix4x4)brush.NodeToTreeSpaceMatrix);
                    if (!math.all(math.isfinite(box.Min)) || !math.all(math.isfinite(box.Max)) || math.any(box.Max < box.Min))
                        continue;
                    var b = new Bounds();
                    b.SetMinMax(box.Min, box.Max);
                    if (!any) bounds = b; else bounds.Encapsulate(b);
                    any = true;
                }
            }
            if (!any)
                return false;
            // as close as keeps every corner of the bounds in view, with a little room
            var direction = new Vector3(-0.35f, 0.45f, -1).normalized;
            camera.transform.position = bounds.center + direction;
            camera.transform.LookAt(bounds.center);
            var tanY = Mathf.Tan(camera.fieldOfView * 0.5f * Mathf.Deg2Rad);
            var tanX = tanY * camera.aspect;
            var right = camera.transform.right; var up = camera.transform.up; var forward = camera.transform.forward;
            var distance = 0.1f;
            for (int corner = 0; corner < 8; corner++)
            {
                var offset = new Vector3((corner & 1) != 0 ? bounds.extents.x : -bounds.extents.x,
                                         (corner & 2) != 0 ? bounds.extents.y : -bounds.extents.y,
                                         (corner & 4) != 0 ? bounds.extents.z : -bounds.extents.z);
                var depth = Vector3.Dot(offset, forward);
                distance = Mathf.Max(distance, Mathf.Abs(Vector3.Dot(offset, right)) / tanX - depth, Mathf.Abs(Vector3.Dot(offset, up)) / tanY - depth);
            }
            camera.transform.position = bounds.center - forward * distance * 1.1f;
            // what stands in front of the cases (the next station, closer to the camera) is clipped away
            var nearest = float.PositiveInfinity;
            for (int corner = 0; corner < 8; corner++)
            {
                var offset = new Vector3((corner & 1) != 0 ? bounds.extents.x : -bounds.extents.x,
                                         (corner & 2) != 0 ? bounds.extents.y : -bounds.extents.y,
                                         (corner & 4) != 0 ? bounds.extents.z : -bounds.extents.z);
                nearest = Mathf.Min(nearest, Vector3.Dot(bounds.center + offset - camera.transform.position, forward));
            }
            camera.nearClipPlane = Mathf.Max(0.02f, nearest * 0.98f);
            return true;
        }

        static ChiselHotspotDiagnosticsView.Summary Render(Camera camera, Texture2D image, string path, HotspotDiagnosticMode mode)
        {
            // the models drawn for this camera as the scene view draws them, whether or not its render reaches the hooks
            foreach (var model in ChiselModelManager.Instance.Models)
            {
                if (model && model.isActiveAndEnabled && model.generated != null)
                    ChiselRenderObjects.OnRenderModel(camera, model, DrawModeFlags.None);
            }
            var summary = ChiselHotspotDiagnosticsView.Capture(camera, mode);
            camera.Render();
            RenderTexture.active = camera.targetTexture;
            image.ReadPixels(new Rect(0, 0, image.width, image.height), 0, 0);
            image.Apply();
            RenderTexture.active = null;
            File.WriteAllBytes(path, image.EncodeToPNG());
            return summary;
        }
    }
}
