using System.Collections.Generic;
using Chisel.Core;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;

namespace Chisel.Components.Tests
{
    // A texture that isn't square covers a surface with square pixels: its short axis is scaled from its main
    // texture's size, on the uv matrix along the texture's own axes, without changing the matrix (ChiselTextureScale)
    [TestFixture]
    public class ChiselTextureScaleTests
    {
        readonly List<Object> created = new();
        readonly List<GameObject> roots = new();

        [SetUp]
        public void SetUp()
        {
            if (Object.FindObjectsByType<ChiselModelComponent>(FindObjectsInactive.Include).Length > 0)
                Assert.Ignore("A Chisel model is loaded; run these in a scene without one");
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var root in roots)
                if (root) Object.DestroyImmediate(root);
            roots.Clear();
            Update();
            foreach (var item in created)
                if (item) Object.DestroyImmediate(item);
            created.Clear();
            if (UnityEditor.AssetDatabase.IsValidFolder(kFolder))
                UnityEditor.AssetDatabase.DeleteAsset(kFolder);
            ChiselTextureScale.Refresh(null);
        }

        static void Update()
        {
            for (int i = 0; i < 3; i++)
            {
                ChiselNodeHierarchyManager.Update();
                ChiselModelManager.Instance.UpdateModels();
            }
        }

        Texture2D Texture(int width, int height)
        {
            var texture = new Texture2D(width, height);
            created.Add(texture);
            return texture;
        }

        Material MaterialWith(Texture texture)
        {
            var material = new Material(ChiselProjectSettings.DefaultWallMaterial) { mainTexture = texture };
            created.Add(material);
            return material;
        }

        const string kFolder = "Assets/ChiselTextureScaleTests";

        // A material saved as an asset, with Chisel Surface Parameters (only an asset can have them)
        ChiselSurfaceMetadata MaterialAssetWith(Texture texture, out Material material)
        {
            if (!UnityEditor.AssetDatabase.IsValidFolder(kFolder))
                UnityEditor.AssetDatabase.CreateFolder("Assets", kFolder.Substring("Assets/".Length));
            material = new Material(ChiselProjectSettings.DefaultWallMaterial);
            UnityEditor.AssetDatabase.CreateAsset(material, UnityEditor.AssetDatabase.GenerateUniqueAssetPath(kFolder + "/material.mat"));
            var metadata = material.AddMetadataOfType<ChiselSurfaceMetadata>();
            // last: the texture isn't an asset, so the material loses it when it is saved or loaded again
            material.mainTexture = texture;
            return metadata;
        }

        ChiselModelComponent Model()
        {
            var root = new GameObject("texture scale test");
            roots.Add(root);
            return root.AddComponent<ChiselModelComponent>();
        }

        static ChiselBoxComponent Box(ChiselModelComponent model, Material material)
        {
            var go = new GameObject("box");
            go.transform.SetParent(model.transform, false);
            var box = go.AddComponent<ChiselBoxComponent>();
            box.Min = new float3(0, 0, 0);
            box.Max = new float3(1, 1, 1);
            Update();
            foreach (var surface in box.SurfaceDefinition.surfaces)
                surface.SetMaterial(material);
            box.OnValidate();
            Update();
            return box;
        }

        // What the model draws with a material: each vertex's position, normal and uv
        static List<(float3 position, float3 normal, float2 uv)> Drawn(ChiselModelComponent model, Material material)
        {
            var drawn = new List<(float3, float3, float2)>();
            foreach (var renderable in model.generated.renderables)
            {
                if (renderable == null || !renderable.Valid || !renderable.HasGeometry || renderable.meshRenderer.sharedMaterial != material)
                    continue;
                var mesh = renderable.sharedMesh;
                var vertices = mesh.vertices; var normals = mesh.normals; var uvs = mesh.uv;
                for (int i = 0; i < vertices.Length; i++)
                    drawn.Add((vertices[i], normals[i], uvs[i]));
            }
            return drawn;
        }

        // How far the uvs of the face facing up reach, along u and v
        static float2 TopRange(ChiselModelComponent model, Material material)
        {
            float2 min = float.MaxValue, max = float.MinValue;
            foreach (var (_, normal, uv) in Drawn(model, material))
            {
                if (normal.y < 0.999f)
                    continue;
                min = math.min(min, uv);
                max = math.max(max, uv);
            }
            Assert.That(max.x, Is.GreaterThanOrEqualTo(min.x), "the top face is drawn");
            return max - min;
        }

        [Test]
        public void TheShortAxis_IsScaledToTheLongOnes()
        {
            Assert.That(ChiselTextureScale.ScaleOf(2048, 512), Is.EqualTo(new float2(1, 4)));
            Assert.That(ChiselTextureScale.ScaleOf(512, 2048), Is.EqualTo(new float2(4, 1)));
            Assert.That(ChiselTextureScale.ScaleOf(256, 256), Is.EqualTo(new float2(1, 1)));
            Assert.That(ChiselTextureScale.ScaleOf(0, 256), Is.EqualTo(new float2(1, 1)), "no texture size");
        }

        // The texture Unity calls the material's main one, or the one Unity's own shaders use; none: 1:1
        [Test]
        public void TheMainTexture_IsFound_Else1To1()
        {
            var texture = Texture(256, 64);
            Assert.That(ChiselTextureScale.MainTextureOf(MaterialWith(texture)), Is.SameAs(texture), "the main texture");

            var shader = Shader.Find("Unlit/Texture");
            Assume.That(shader, Is.Not.Null);
            var unlit = new Material(shader);
            created.Add(unlit);
            unlit.SetTexture("_MainTex", texture);
            Assert.That(ChiselTextureScale.MainTextureOf(unlit), Is.SameAs(texture), "_MainTex");
            Assert.That(ChiselTextureScale.Of(unlit), Is.EqualTo(new float2(1, 4)));

            var color = Shader.Find("Unlit/Color");
            Assume.That(color, Is.Not.Null);
            var untextured = new Material(color);
            created.Add(untextured);
            Assert.That(ChiselTextureScale.MainTextureOf(untextured), Is.Null);
            Assert.That(ChiselTextureScale.Of(untextured), Is.EqualTo(new float2(1, 1)));
            Assert.That(ChiselTextureScale.Of(null), Is.EqualTo(new float2(1, 1)));
        }

        // A texture swapped for one of another size is measured again
        [Test]
        public void ASwappedTexture_IsMeasuredAgain()
        {
            var material = MaterialWith(Texture(256, 64));
            Assert.That(ChiselTextureScale.Of(material), Is.EqualTo(new float2(1, 4)));
            material.mainTexture = Texture(64, 64);
            var changed = new List<Material>();
            ChiselTextureScale.Refresh(changed);
            Assert.That(changed, Is.EqualTo(new[] { material }));
            Assert.That(ChiselTextureScale.Of(material), Is.EqualTo(new float2(1, 1)));
            changed.Clear();
            ChiselTextureScale.Refresh(changed);
            Assert.That(changed, Is.Empty, "nothing changed since");
        }

        // A material can set its own uv scale (its Chisel Surface Parameters), used instead of its texture's
        [Test]
        public void AMaterialsOwnUVScale_ReplacesItsTexturesScale()
        {
            var metadata = MaterialAssetWith(Texture(256, 64), out var material);
            Assert.That(ChiselTextureScale.Of(material), Is.EqualTo(new float2(1, 4)), "not set: the texture's");

            metadata.overrideUVScale = true;
            metadata.uvScale = new Vector2(2, 3);
            var changed = new List<Material>();
            ChiselTextureScale.Refresh(changed);
            Assert.That(changed, Is.EqualTo(new[] { material }), "noticed, so the surfaces using it are built again");
            Assert.That(ChiselTextureScale.Of(material), Is.EqualTo(new float2(2, 3)));

            metadata.overrideUVScale = false;
            ChiselTextureScale.Refresh(null);
            Assert.That(ChiselTextureScale.Of(material), Is.EqualTo(new float2(1, 4)), "not set again");
        }

        [Test]
        public void AMaterialsOwnUVScale_IsDrawn()
        {
            var model = Model();
            var metadata = MaterialAssetWith(Texture(256, 64), out var material);
            metadata.overrideUVScale = true;
            metadata.uvScale = new Vector2(2, 0.5f);
            Box(model, material);
            var range = TopRange(model, material);
            Assert.That(range.x, Is.EqualTo(2).Within(1e-4f));
            Assert.That(range.y, Is.EqualTo(0.5f).Within(1e-4f));
        }

        // A scale of zero or less would collapse the texture: it is kept at the smallest scale
        [Test]
        public void AMaterialsOwnUVScale_IsNeverZero()
        {
            var metadata = MaterialAssetWith(Texture(64, 64), out var material);
            metadata.overrideUVScale = true;
            metadata.uvScale = new Vector2(0, -1);
            Assert.That(ChiselTextureScale.Of(material), Is.EqualTo(new float2((float)UVMatrix.kMinScale)));
        }

        // The scale goes on the uvs the matrix gives, along the texture's own axes, its translation too
        [Test]
        public void Scaled_ScalesTheUVs()
        {
            var matrix = new UVMatrix(new float4(0, 1, 0, 0.25f), new float4(-1, 0, 0, 0.5f));
            var scaled = matrix.Scaled(new float2(2, 3));
            Assert.That((float4)scaled.U, Is.EqualTo(new float4(0, 2, 0, 0.5f)));
            Assert.That((float4)scaled.V, Is.EqualTo(new float4(-3, 0, 0, 1.5f)));
        }

        // A unit square of a 4:1 texture's surface covers a quarter of it along its height, and of a 1:4 one a quarter
        // along its width; a square texture is as before
        [Test]
        public void ANonSquareTexture_IsDrawnWithSquarePixels()
        {
            var model = Model();
            var wide   = MaterialWith(Texture(256, 64));
            var tall   = MaterialWith(Texture(64, 256));
            var square = MaterialWith(Texture(64, 64));
            Box(model, wide);
            var range = TopRange(model, wide);
            Assert.That(range.x, Is.EqualTo(1).Within(1e-4f));
            Assert.That(range.y, Is.EqualTo(4).Within(1e-4f));

            var other = Model();
            Box(other, tall);
            range = TopRange(other, tall);
            Assert.That(range.x, Is.EqualTo(4).Within(1e-4f));
            Assert.That(range.y, Is.EqualTo(1).Within(1e-4f));

            var third = Model();
            Box(third, square);
            range = TopRange(third, square);
            Assert.That(range.x, Is.EqualTo(1).Within(1e-4f));
            Assert.That(range.y, Is.EqualTo(1).Within(1e-4f));
        }

        // Another material on the surface: the scale follows it, the uv matrix stays as it is
        [Test]
        public void SwappingTheMaterial_KeepsTheUVMatrix()
        {
            var model = Model();
            var wide   = MaterialWith(Texture(256, 64));
            var square = MaterialWith(Texture(64, 64));
            var box = Box(model, wide);
            var before = box.SurfaceDefinition.surfaces[0].surfaceDetails.UV0;
            foreach (var surface in box.SurfaceDefinition.surfaces)
                surface.SetMaterial(square);
            box.OnValidate();
            Update();
            Assert.That(box.SurfaceDefinition.surfaces[0].surfaceDetails.UV0, Is.EqualTo(before));
            var range = TopRange(model, square);
            Assert.That(range.y, Is.EqualTo(1).Within(1e-4f));
        }

        // A texture resized on the material maps the surfaces using it again
        [Test]
        public void AResizedTexture_MapsTheSurfacesAgain()
        {
            var model = Model();
            var material = MaterialWith(Texture(256, 64));
            var box = Box(model, material);
            material.mainTexture = Texture(256, 128);
            ChiselTextureScale.Refresh(null);
            box.SetDirty();
            Update();
            Assert.That(TopRange(model, material).y, Is.EqualTo(2).Within(1e-4f));
        }

        // Moving the texture in the surface's plane moves what is drawn by as much, along both axes
        [Test]
        public void MovingTheTexture_MovesItAsItIsSeen()
        {
            var model = Model();
            var material = MaterialWith(Texture(256, 64));
            var box = Box(model, material);
            var surface = new SurfaceReference(box, 0, default, 0);
            var original = surface.UV0;
            var scale = ChiselTextureScale.Of(material);
            var move = Matrix4x4.Translate(new Vector3(0.3f, 0.2f, 0));
            surface.PlaneSpaceTransformUV(move, original);

            var point = new float4(0.1f, 0.7f, 0, 1);
            var drawnAfter  = math.mul(surface.UV0.Scaled(scale).ToFloat4x4(), point).xy;
            var drawnBefore = math.mul(original.Scaled(scale).ToFloat4x4(), math.mul((float4x4)move, point)).xy;
            Assert.That(math.distance(drawnAfter, drawnBefore), Is.LessThan(1e-5f));
        }

        // Locking the texture to the object or the world doesn't change how it looks now
        [Test]
        public void ToggledTextureLock_LooksTheSame()
        {
            var model = Model();
            var material = MaterialWith(Texture(256, 64));
            var box = Box(model, material);
            box.transform.SetPositionAndRotation(new Vector3(2.3f, 0.7f, -1.1f), Quaternion.Euler(0, 30, 0));
            ChiselNodeHierarchyManager.UpdateTreeNodeTransformation(box);
            Update();
            var before = Drawn(model, material);

            var brush = (CSGTreeBrush)box.TopTreeNode;
            var blob = BrushMeshManager.GetBrushMeshBlob(brush.BrushMesh.BrushMeshID);
            var done = new HashSet<int>();
            for (int i = 0; i < blob.Value.polygons.Length; i++)
            {
                var surface = new SurfaceReference(box, blob.Value.polygons[i].descriptionIndex, brush, i);
                if (done.Add(surface.descriptionIndex))
                    surface.SurfaceDetails = ChiselTextureLock.Toggled(surface, surface.SurfaceDetails);
            }
            box.OnValidate();
            Update();

            var after = Drawn(model, material);
            Assert.That(before, Is.Not.Empty);
            foreach (var (position, normal, uv) in before)
            {
                var found = after.Find(v => math.distance(v.position, position) < 1e-4f && math.dot(v.normal, normal) > 0.999f);
                Assert.That(math.distance(found.uv, uv), Is.LessThan(1e-3f), "at " + position);
            }
        }
    }
}
