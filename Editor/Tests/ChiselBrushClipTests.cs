using System.Collections.Generic;
using Chisel.Components;
using Chisel.Core;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;

namespace Chisel.Editors.Tests
{
    // The clip tool cuts the selected brushes with a plane in the world: keeping what is behind it, what is in front of
    // it, or splitting each brush into two. A brush that ends up wholly on the removed side goes; the face a cut makes
    // gets the active material.
    [TestFixture]
    public sealed class ChiselBrushClipTests
    {
        const string kFolder = "Assets/__ChiselBrushClipTests";

        readonly List<GameObject> objects = new();
        readonly Dictionary<string, string> savedPrefs = new();
        Material cutMaterial;

        [SetUp]
        public void SetUp()
        {
            foreach (var key in ChiselSurfaceBrowserPrefs.StorageKeys)
                savedPrefs[key] = EditorPrefs.GetString(key, null);
            if (!AssetDatabase.IsValidFolder(kFolder))
                AssetDatabase.CreateFolder("Assets", kFolder.Substring("Assets/".Length));
            cutMaterial = new Material(ChiselProjectSettings.DefaultWallMaterial);
            AssetDatabase.CreateAsset(cutMaterial, kFolder + "/Cut.mat");
            ChiselSurfaceBrowserPrefs.ActiveMaterial = cutMaterial;
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var go in objects)
                if (go) Object.DestroyImmediate(go);
            objects.Clear();
            AssetDatabase.DeleteAsset(kFolder);
            foreach (var (key, value) in savedPrefs)
            {
                if (value == null) EditorPrefs.DeleteKey(key);
                else EditorPrefs.SetString(key, value);
            }
            savedPrefs.Clear();
        }

        // a 2x2x2 box brush, turned a quarter around y and moved, so its own axes aren't the world's
        ChiselBrushComponent Box(Vector3 position)
        {
            BrushMeshFactory.CreateBox(-Vector3.one, Vector3.one, out var box);
            var go = new GameObject("Brush");
            objects.Add(go);
            go.transform.SetPositionAndRotation(position, Quaternion.Euler(0, 90, 0));
            var brush = go.AddComponent<ChiselBrushComponent>();
            brush.definition.BrushOutline = box;
            brush.OnValidate();
            return brush;
        }

        static IEnumerable<Vector3> WorldVertices(ChiselBrushComponent brush)
        {
            var toWorld = brush.GlobalTransformation;
            foreach (var vertex in brush.definition.BrushOutline.vertices)
                yield return toWorld.MultiplyPoint((Vector3)vertex);
        }

        static int CutFace(ChiselBrushComponent brush, Vector3 worldNormal)
        {
            var mesh        = brush.definition.BrushOutline;
            var localNormal = brush.GlobalTransformation.inverse.MultiplyVector(worldNormal).normalized;
            for (int p = 0; p < mesh.polygons.Length; p++)
                if (math.dot(mesh.planes[p].xyz, (float3)localNormal) > 0.9999f) return p;
            return -1;
        }

        void AssertCutFaceHasTheActiveMaterial(ChiselBrushComponent brush, Vector3 worldNormal)
        {
            var face = CutFace(brush, worldNormal);
            Assert.That(face, Is.Not.EqualTo(-1), $"a face facing {worldNormal}");
            var surface = brush.SurfaceDefinition.GetSurface(brush.definition.BrushOutline.polygons[face].descriptionIndex);
            Assert.That(surface.RenderMaterial, Is.EqualTo(cutMaterial), "the cut's material");
        }

        // the world plane x = 5.25, in front where x is larger
        static readonly Plane kPlane = new(Vector3.right, new Vector3(5.25f, 0, 0));

        static ChiselSurface SurfaceFacing(ChiselBrushComponent brush, Vector3 worldNormal)
        {
            var face = CutFace(brush, worldNormal);
            Assert.That(face, Is.Not.EqualTo(-1), $"a face facing {worldNormal}");
            return brush.SurfaceDefinition.GetSurface(brush.definition.BrushOutline.polygons[face].descriptionIndex);
        }

        static readonly Vector3[] kKeptSides = { Vector3.left, Vector3.up, Vector3.down, Vector3.forward, Vector3.back };

        [Test]
        public void KeepingWhatIsBehind_CutsOffWhatIsInFront()
        {
            var brush = Box(new Vector3(5, 0, 0));
            var surfacesBefore = new Dictionary<Vector3, ChiselSurface>();
            foreach (var side in kKeptSides)
                surfacesBefore[side] = SurfaceFacing(brush, side);

            var left = ChiselBrushClip.Apply(new[] { brush }, kPlane, ChiselClipMode.KeepBehind);

            Assert.That(left, Is.EquivalentTo(new[] { brush }));
            Assert.That(brush.definition.BrushOutline.ValidateShape(out var error), Is.True, error);
            foreach (var vertex in WorldVertices(brush))
                Assert.That(vertex.x, Is.LessThanOrEqualTo(5.25f + 1e-4f), $"{vertex}");
            AssertCutFaceHasTheActiveMaterial(brush, Vector3.right);
            // the faces the cut leaves keep their own surfaces, though their order changed
            foreach (var side in kKeptSides)
                Assert.That(SurfaceFacing(brush, side), Is.SameAs(surfacesBefore[side]), $"the surface of the face facing {side}");
        }

        [Test]
        public void KeepingWhatIsInFront_CutsOffWhatIsBehind()
        {
            var brush = Box(new Vector3(5, 0, 0));

            ChiselBrushClip.Apply(new[] { brush }, kPlane, ChiselClipMode.KeepInFront);

            Assert.That(brush.definition.BrushOutline.ValidateShape(out var error), Is.True, error);
            foreach (var vertex in WorldVertices(brush))
                Assert.That(vertex.x, Is.GreaterThanOrEqualTo(5.25f - 1e-4f), $"{vertex}");
            AssertCutFaceHasTheActiveMaterial(brush, Vector3.left);
        }

        [Test]
        public void Splitting_MakesTwoBrushesThatMeetAtThePlane()
        {
            var brush = Box(new Vector3(5, 0, 0));

            var left = ChiselBrushClip.Apply(new[] { brush }, kPlane, ChiselClipMode.Split);

            Assert.That(left.Count, Is.EqualTo(2));
            Assert.That(left, Does.Contain(brush));
            var other = left[0] == brush ? left[1] : left[0];
            objects.Add(other.gameObject);
            Assert.That(other.transform.parent, Is.EqualTo(brush.transform.parent), "beside the brush");
            Assert.That(other.definition.BrushOutline.ValidateShape(out var error), Is.True, error);
            foreach (var vertex in WorldVertices(brush))
                Assert.That(vertex.x, Is.LessThanOrEqualTo(5.25f + 1e-4f), $"{vertex}");
            foreach (var vertex in WorldVertices(other))
                Assert.That(vertex.x, Is.GreaterThanOrEqualTo(5.25f - 1e-4f), $"{vertex}");
            AssertCutFaceHasTheActiveMaterial(brush, Vector3.right);
            AssertCutFaceHasTheActiveMaterial(other, Vector3.left);
        }

        [TestCase(ChiselClipMode.KeepBehind)]
        [TestCase(ChiselClipMode.Split)]
        public void ABrushWhollyBehindThePlane_IsLeftAsItWas(ChiselClipMode mode)
        {
            var brush  = Box(new Vector3(0, 0, 0));
            var before = new BrushMesh(brush.definition.BrushOutline);

            var left = ChiselBrushClip.Apply(new[] { brush }, kPlane, mode);

            Assert.That(left, Is.EquivalentTo(new[] { brush }));
            Assert.That(brush.definition.BrushOutline.vertices, Is.EqualTo(before.vertices));
        }

        [Test]
        public void ABrushWhollyOnTheSideThatIsRemoved_Goes()
        {
            var brush = Box(new Vector3(10, 0, 0));

            var left = ChiselBrushClip.Apply(new[] { brush }, kPlane, ChiselClipMode.KeepBehind);

            Assert.That(left, Is.Empty);
            Assert.That(brush == null, Is.True, "the brush is destroyed");
        }
    }
}
