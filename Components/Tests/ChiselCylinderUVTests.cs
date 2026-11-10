using System.Collections.Generic;
using Chisel.Core;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Chisel.Components.Tests
{
    // A cylinder lays out its uvs (caps centered, sides one after another round it) on top of each surface's own uvs, so
    // changing a surface's uvs, or the cylinder's settings, keeps both
    [TestFixture]
    public class ChiselCylinderUVTests
    {
        readonly List<GameObject> roots = new();
        (Scene scene, bool wasDirty) remembered;

        [SetUp]
        public void SetUp()
        {
            remembered = SceneLeftClean.Remember();
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
            SceneLeftClean.Restore(remembered);
        }

        static void Update()
        {
            for (int i = 0; i < 3; i++)
            {
                ChiselNodeHierarchyManager.Update();
                ChiselModelManager.Instance.UpdateModels();
            }
        }

        ChiselCylinderComponent Cylinder()
        {
            var model = new GameObject("model").AddComponent<ChiselModelComponent>();
            roots.Add(model.gameObject);
            var cylinder = new GameObject("cylinder").AddComponent<ChiselCylinderComponent>();
            cylinder.transform.SetParent(model.transform, false);
            Update();
            return cylinder;
        }

        // The layout a cylinder makes, as it was written into its surfaces before: caps centered, side i offset along u
        static UVMatrix Layout(in ChiselCylinder settings, int descriptionIndex)
        {
            if (descriptionIndex < 2)
                return UVMatrix.Centered;
            var radius     = settings.topDiameterX * 0.5f;
            var angle      = 360.0f / settings.sides;
            var sideLength = (2 * math.sin(math.radians(angle / 2.0f))) * radius;
            var uv0 = UVMatrix.Identity;
            uv0.U.w = ((descriptionIndex - 2) + 0.5f) * sideLength;
            return uv0;
        }

        // The uvs of each built face, by its surface
        static Dictionary<int, UVMatrix> BuiltUVs(ChiselCylinderComponent cylinder)
        {
            var brushes = new List<CSGTreeBrush>();
            ChiselGeneratorParts.BrushesOf(cylinder, brushes);
            Assert.That(brushes.Count, Is.EqualTo(1));
            ref var mesh = ref BrushMeshManager.GetBrushMeshBlob(brushes[0].BrushMesh).Value;
            var uvs = new Dictionary<int, UVMatrix>();
            for (int p = 0; p < mesh.polygons.Length; p++)
                uvs[mesh.polygons[p].descriptionIndex] = mesh.polygons[p].surface.details.UV0;
            return uvs;
        }

        static void AssertSame(UVMatrix actual, UVMatrix expected, string what)
        {
            Assert.That(math.all(math.abs((float4)actual.U - (float4)expected.U) < 1e-5f) && math.all(math.abs((float4)actual.V - (float4)expected.V) < 1e-5f),
                        Is.True, $"{what}: {actual} instead of {expected}");
        }

        [Test]
        public void TheGeneratedMesh_IsLaidOut()
        {
            var cylinder = Cylinder();
            var settings = cylinder.definition.settings;
            using var surfaces = BrushMeshManager.BuildInternalSurfaceArrayBlob(cylinder.SurfaceDefinition, Unity.Collections.Allocator.Temp);
            var mesh = settings.GenerateMesh(surfaces, Unity.Collections.Allocator.Temp);
            try
            {
                Assert.That(mesh.IsCreated, Is.True);
                ref var polygons = ref mesh.Value.polygons;
                for (int p = 0; p < polygons.Length; p++)
                    AssertSame(polygons[p].surface.details.UV0, Layout(settings, polygons[p].descriptionIndex), "surface " + polygons[p].descriptionIndex);
            }
            finally
            {
                mesh.Dispose();
            }
        }

        [Test]
        public void ANewCylinder_IsLaidOutAsBefore()
        {
            var cylinder = Cylinder();
            var settings = cylinder.definition.settings;
            foreach (var pair in BuiltUVs(cylinder))
                AssertSame(pair.Value, Layout(settings, pair.Key), "surface " + pair.Key);
        }

        [Test]
        public void ASurfacesOwnUVs_AreKeptWhenTheCylinderChanges()
        {
            var cylinder = Cylinder();
            var own = UVMatrix.TRS(new Vector2(0.25f, 0), Vector3.up, 0, new Vector2(2, 2));
            cylinder.SurfaceDefinition.surfaces[3].surfaceDetails.UV0 = own;
            cylinder.definition.settings.height = 3;
            cylinder.OnValidate();
            Update();

            Assert.That(cylinder.SurfaceDefinition.surfaces[3].surfaceDetails.UV0, Is.EqualTo(own), "the surface's uvs were reset");
        }

        // A side scaled up keeps the texture running on round the cylinder: its offset is scaled too
        [Test]
        public void AScaledSide_KeepsTheSidesInLine()
        {
            var cylinder = Cylinder();
            var settings = cylinder.definition.settings;
            // a new surface's uvs, scaled up
            var scaled = SurfaceDetails.Default.UV0;
            scaled.U = new Vector4(scaled.U.x * 2, scaled.U.y * 2, scaled.U.z * 2, scaled.U.w);
            scaled.V = new Vector4(scaled.V.x * 2, scaled.V.y * 2, scaled.V.z * 2, scaled.V.w);
            foreach (var surface in cylinder.SurfaceDefinition.surfaces)
                surface.surfaceDetails.UV0 = scaled;
            cylinder.OnValidate();
            Update();

            var built = BuiltUVs(cylinder);
            Assert.That(built[3].U.w, Is.EqualTo(Layout(settings, 3).U.w * 2).Within(1e-5f));
            Assert.That(built[4].U.w - built[3].U.w, Is.EqualTo((Layout(settings, 4).U.w - Layout(settings, 3).U.w) * 2).Within(1e-5f), "the sides don't run on");
            Assert.That(built[3].U.x, Is.EqualTo(2).Within(1e-5f));
        }

        // A cylinder saved before held the layout in its surfaces: it is taken as untouched surfaces, not laid out twice
        [Test]
        public void ACylinderSavedBefore_LooksTheSame()
        {
            var cylinder = Cylinder();
            var settings = cylinder.definition.settings;
            cylinder.definition.settings.surfaceUVVersion = 0;
            for (int i = 0; i < cylinder.SurfaceDefinition.surfaces.Length; i++)
                cylinder.SurfaceDefinition.surfaces[i].surfaceDetails.UV0 = Layout(settings, i);
            cylinder.OnValidate();
            Update();

            Assert.That(cylinder.definition.settings.surfaceUVVersion, Is.EqualTo(ChiselCylinder.kSurfaceUVVersion));
            foreach (var pair in BuiltUVs(cylinder))
                AssertSame(pair.Value, Layout(settings, pair.Key), "surface " + pair.Key);
        }
    }
}
