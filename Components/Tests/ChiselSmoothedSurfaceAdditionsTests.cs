using System.Collections.Generic;
using Chisel.Core;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;

namespace Chisel.Components.Tests
{
    // What decals, paint and hotspot cuts add to a smooth surface doesn't change how it is shaded: every vertex they add
    // has the normal the surface had there, and the vertices that were there keep theirs
    [TestFixture]
    public class ChiselSmoothedSurfaceAdditionsTests
    {
        const string kFolder = "Assets/__ChiselSmoothedSurfaceAdditionsTests";

        readonly List<Object> created = new();
        ChiselPaintProfile profile;

        [SetUp]
        public void SetUp()
        {
            if (Object.FindObjectsByType<ChiselModelComponent>(FindObjectsInactive.Include).Length > 0)
                Assert.Ignore("A Chisel model is loaded; run these in a scene without one");
        }

        [TearDown]
        public void TearDown()
        {
            if (profile != null)
                ChiselPaintProfiles.Unregister(profile);
            profile = null;
            foreach (var item in created)
            {
                if (item)
                    Object.DestroyImmediate(item);
            }
            created.Clear();
            Update();
            if (AssetDatabase.IsValidFolder(kFolder))
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

        // A 16 sided cylinder, 2 across and 2 high, its sides smoothed by their smoothing group, in the given material
        ChiselCylinderComponent SmoothCylinder(out ChiselModelComponent model, Material material = null)
        {
            var root = new GameObject("smoothed surface test");
            created.Add(root);
            model = root.AddComponent<ChiselModelComponent>();
            var go = new GameObject("cylinder");
            go.transform.SetParent(model.transform, false);
            var cylinder = go.AddComponent<ChiselCylinderComponent>();
            cylinder.Sides = 16;
            cylinder.Diameter = 2;
            cylinder.Height = 2;
            cylinder.SmoothingGroup = 1;
            Update();
            if (material != null)
            {
                foreach (var surface in cylinder.SurfaceDefinition.surfaces)
                    surface.SetMaterial(material);
                cylinder.OnValidate();
                Update();
            }
            return cylinder;
        }

        struct Triangle
        {
            public float3 p0, p1, p2;
            public float3 n0, n1, n2;
        }

        // The model's drawn triangles facing sideways, with their corners' normals
        static List<Triangle> SideTriangles(ChiselModelComponent model)
        {
            var triangles = new List<Triangle>();
            foreach (var renderable in model.generated.renderables)
            {
                if (renderable == null || renderable.sharedMesh == null)
                    continue;
                var mesh = renderable.sharedMesh;
                var positions = mesh.vertices; var normals = mesh.normals;
                for (int s = 0; s < mesh.subMeshCount; s++)
                {
                    var indices = mesh.GetTriangles(s);
                    for (int i = 0; i + 2 < indices.Length; i += 3)
                    {
                        var triangle = new Triangle
                        {
                            p0 = positions[indices[i]], p1 = positions[indices[i + 1]], p2 = positions[indices[i + 2]],
                            n0 = normals[indices[i]],   n1 = normals[indices[i + 1]],   n2 = normals[indices[i + 2]]
                        };
                        if (math.abs(math.normalize(math.cross(triangle.p1 - triangle.p0, triangle.p2 - triangle.p0)).y) < 0.5f)
                            triangles.Add(triangle);
                    }
                }
            }
            Assert.That(triangles, Is.Not.Empty, "the model draws no sides");
            return triangles;
        }

        // The normal the plain surface is shaded with at a point of it: interpolated in the triangle the point is in
        static bool ShadedNormal(List<Triangle> plain, float3 point, out float3 normal)
        {
            foreach (var triangle in plain)
            {
                var e0 = triangle.p1 - triangle.p0; var e1 = triangle.p2 - triangle.p0;
                var faceNormal = math.cross(e0, e1);
                var area = math.length(faceNormal);
                if (!(area > 0))
                    continue;
                faceNormal /= area;
                if (math.abs(math.dot(point - triangle.p0, faceNormal)) > 1e-3f)
                    continue;
                var w1 = math.dot(math.cross(point - triangle.p0, e1), faceNormal) / area;
                var w2 = math.dot(math.cross(e0, point - triangle.p0), faceNormal) / area;
                var w0 = 1 - w1 - w2;
                if (w0 < -1e-3f || w1 < -1e-3f || w2 < -1e-3f)
                    continue;
                normal = math.normalize(w0 * triangle.n0 + w1 * triangle.n1 + w2 * triangle.n2);
                return true;
            }
            normal = default;
            return false;
        }

        // Every sideways vertex the model draws now has the normal the plain model was shaded with there; and there are
        // more of them than before, so the addition did cut the surface
        static void AssertShadedAsBefore(List<Triangle> plain, ChiselModelComponent model, string what, bool skipNewSurfaces = false)
        {
            var now = SideTriangles(model);
            Assert.That(now.Count, Is.GreaterThan(plain.Count), $"the {what} added nothing to the sides");
            foreach (var triangle in now)
            {
                // the surfaces of what was added, which weren't there before
                if (skipNewSurfaces && (!ShadedNormal(plain, triangle.p0, out _) || !ShadedNormal(plain, triangle.p1, out _) || !ShadedNormal(plain, triangle.p2, out _)))
                    continue;
                foreach (var (point, normal) in new[] { (triangle.p0, triangle.n0), (triangle.p1, triangle.n1), (triangle.p2, triangle.n2) })
                {
                    Assert.That(ShadedNormal(plain, point, out var before), Is.True, $"the {what} drew a side vertex at {point}, off the plain sides");
                    Assert.That(math.dot(normal, before), Is.GreaterThan(0.99999f), $"the {what} changed the shading at {point}: {normal} where it was {before}");
                }
            }
        }

        // A notch cut out of the top by another brush: what is left of the sides is shaded as it was, the vertices the
        // cut adds inside their faces included
        [Test]
        public void AnotherBrushCuttingTheSurface_DoesntChangeTheShading()
        {
            SmoothCylinder(out var model);
            var plain = SideTriangles(model);

            var go = new GameObject("notch");
            go.transform.SetParent(model.transform, false);
            var notch = go.AddComponent<ChiselBoxComponent>();
            notch.Max = new float3(1.5f, 2.5f, 0.3f);
            notch.Min = new float3(0.5f, 1.5f, -0.3f);
            notch.Operation = CSGOperationType.Subtractive;
            Update();

            AssertShadedAsBefore(plain, model, "cut", skipNewSurfaces: true);
        }

        [Test]
        public void ADecal_DoesntChangeTheShading()
        {
            var cylinder = SmoothCylinder(out var model);
            var plain = SideTriangles(model);

            var decalMaterial = new Material(Shader.Find("Standard")) { name = "SmoothedDecalMaterial" };
            created.Add(decalMaterial);
            var decalObject = new GameObject("Decal");
            decalObject.transform.SetParent(model.transform, false);
            decalObject.transform.localPosition = new Vector3(1.5f, 1, 0);
            decalObject.transform.rotation = Quaternion.LookRotation(Vector3.left, Vector3.up);
            var decal = decalObject.AddComponent<ChiselDecalComponent>();
            decal.Material    = decalMaterial;
            decal.Size        = new Vector3(1, 1, 2);
            decal.Transparent = false;
            Update();

            AssertShadedAsBefore(plain, model, "decal");
        }

        [Test]
        public void Paint_DoesntChangeTheShading()
        {
            var material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "SmoothedPaintMaterial" };
            created.Add(material);
            profile = new ChiselPaintProfile { shader = material.shader };
            profile.modes.Add(ChiselPaintMode.Color("Color"));
            ChiselPaintProfiles.Register(profile);
            var cylinder = SmoothCylinder(out var model, material);
            var plain = SideTriangles(model);

            // the side facing +x (the sides follow the two caps)
            var paint = ChiselPaintBrush.PaintFor(cylinder, 2, profile.modes[0]);
            Assert.That(paint, Is.Not.Null);
            ChiselPaintBrush.Dab(paint, profile.modes[0], ChiselPaintOperation.Fill, new Vector4(1, 0, 0, 1), Vector3.zero, 1, 1, 1);
            cylinder.PaintChanged();
            Update();

            AssertShadedAsBefore(plain, model, "paint");
        }

        [Test]
        public void AHotspotCut_DoesntChangeTheShading()
        {
            // a layout of one tile with a border, so a surface mapped on it is cut along the border
            var layout = ScriptableObject.CreateInstance<ChiselHotspotLayout>();
            created.Add(layout);
            layout.size = new Vector2Int(256, 1024);
            layout.pixelsPerUnit = 256;
            layout.tiles = new List<HotspotTile>
            {
                new HotspotTile { id = 1, name = "Bordered", border = 32,
                                  points = new[] { new Vector2Int(0, 0), new Vector2Int(256, 0), new Vector2Int(256, 1024), new Vector2Int(0, 1024) } }
            };
            if (!AssetDatabase.IsValidFolder(kFolder))
                AssetDatabase.CreateFolder("Assets", kFolder.Substring("Assets/".Length));
            var material = new Material(ChiselProjectSettings.DefaultWallMaterial);
            AssetDatabase.CreateAsset(material, kFolder + "/hotspot.mat");
            var plainMaterial = new Material(ChiselProjectSettings.DefaultWallMaterial);
            AssetDatabase.CreateAsset(plainMaterial, kFolder + "/plain.mat");

            var cylinder = SmoothCylinder(out var model, plainMaterial);
            var plain = SideTriangles(model);

            material.AddMetadataOfType<ChiselSurfaceMetadata>().hotspotLayout = layout;
            foreach (var surface in cylinder.SurfaceDefinition.surfaces)
                surface.SetMaterial(material);
            cylinder.OnValidate();
            Update();

            AssertShadedAsBefore(plain, model, "hotspot cut");
        }
    }
}
