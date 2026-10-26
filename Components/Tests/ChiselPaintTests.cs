using System.Collections.Generic;
using Chisel.Core;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

namespace Chisel.Components.Tests
{
    // Surface paint (ChiselSurfacePaint): a grid of values on a surface, written to the vertex attribute it names in the
    // render meshes of its material
    [TestFixture]
    public class ChiselPaintTests
    {
        readonly List<Object> created = new();
        ChiselPaintProfile profile;
        static Material s_Material;

        [SetUp]
        public void SetUp()
        {
            if (Object.FindObjectsByType<ChiselModelComponent>(FindObjectsInactive.Include).Length > 0)
                Assert.Ignore("A Chisel model is loaded; run these in a scene without one");
            // the boxes' material takes paint in the attributes these paint in (its own, so a project's materials don't matter)
            s_Material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "PaintTestMaterial" };
            created.Add(s_Material);
            profile = new ChiselPaintProfile { shader = s_Material.shader };
            profile.modes.Add(ChiselPaintMode.Color("Color"));
            profile.modes.Add(ChiselPaintMode.Value("Value", ChiselPaintAttribute.TexCoord2, ChiselPaintComponents.XYZW));
            profile.modes.Add(ChiselPaintMode.Value("Mask", ChiselPaintAttribute.TexCoord3, ChiselPaintComponents.XYZW));
            ChiselPaintProfiles.Register(profile);
        }

        [TearDown]
        public void TearDown()
        {
            ChiselPaintProfiles.Unregister(profile);
            foreach (var item in created)
            {
                if (item)
                    Object.DestroyImmediate(item);
            }
            created.Clear();
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

        ChiselModelComponent Model()
        {
            var root = new GameObject("paint test");
            created.Add(root);
            return root.AddComponent<ChiselModelComponent>();
        }

        // A unit box with its lowest corner at position
        static ChiselBoxComponent Box(ChiselModelComponent model, Vector3 position)
        {
            var go = new GameObject("Box");
            go.transform.SetParent(model.transform, false);
            go.transform.localPosition = position;
            var box = go.AddComponent<ChiselBoxComponent>();
            box.Max = new float3(1e4f);
            box.Min = float3.zero;
            box.Max = new float3(1);
            foreach (var surface in box.SurfaceDefinition.surfaces)
                surface.SetMaterial(s_Material);
            return box;
        }

        static int SurfaceFacing(ChiselBoxComponent box, float3 normal)
        {
            Update();
            var brush = (CSGTreeBrush)box.TopTreeNode;
            ref var mesh = ref BrushMeshManager.GetBrushMeshBlob(brush.BrushMesh).Value;
            for (int s = 0; s < mesh.polygons.Length; s++)
            {
                if (math.dot(math.normalize(mesh.localPlanes[s].xyz), normal) > 0.999f)
                    return mesh.polygons[s].descriptionIndex;
            }
            Assert.Fail($"the box has no surface facing {normal}");
            return -1;
        }

        // The box's top painted on its corners: (0,0) red, (1,0) green, (0,1) blue, (1,1) black
        static ChiselSurfacePaint PaintTop(ChiselBoxComponent box, ChiselPaintAttribute attribute, ChiselPaintComponents components)
        {
            var paint = ChiselSurfacePaint.Create(SurfaceFacing(box, new float3(0, 1, 0)), attribute, components, Vector4.one,
                                                  new Vector3(0, 1, 0), Vector3.right, Vector3.forward, new Vector2Int(2, 2));
            paint[0, 0] = new Vector4(1, 0, 0, 1);
            paint[1, 0] = new Vector4(0, 1, 0, 1);
            paint[0, 1] = new Vector4(0, 0, 1, 1);
            paint[1, 1] = new Vector4(0, 0, 0, 0);
            box.SetSurfacePaint(paint);
            return paint;
        }

        static Mesh RenderMesh(ChiselModelComponent model)
        {
            foreach (var renderable in model.generated.renderables)
            {
                if (renderable != null && renderable.HasGeometry && (renderable.query & SurfaceDestinationFlags.Renderable) != 0)
                    return renderable.sharedMesh;
            }
            Assert.Fail("the model has no render mesh");
            return null;
        }

        // The attribute's value at every vertex of an upward surface at this position (a corner is the side faces' too)
        static List<Vector4> ValuesAt(Mesh mesh, VertexAttribute attribute, Vector3 position)
        {
            var values = new List<Vector4>();
            if (attribute == VertexAttribute.Color)
            {
                foreach (var color in mesh.colors)
                    values.Add(color);
            } else
                mesh.GetUVs(attribute - VertexAttribute.TexCoord0, values);
            var vertices = mesh.vertices;
            var normals  = mesh.normals;
            var found = new List<Vector4>();
            for (int v = 0; v < vertices.Length; v++)
            {
                if ((vertices[v] - position).sqrMagnitude < 1e-8f && normals[v].y > 0.99f)
                    found.Add(values[v]);
            }
            Assert.That(found, Is.Not.Empty, $"no vertex at {position}");
            return found;
        }

        static void AssertValuesAt(Mesh mesh, VertexAttribute attribute, Vector3 position, Vector4 expected, string message)
        {
            foreach (var value in ValuesAt(mesh, attribute, position))
                Assert.That(Vector4.Distance(value, expected), Is.LessThan(1e-5f), $"{message}: {value} at {position}");
        }

        // The top's corners take the grid's corners; the box's other surfaces, under the same material, the default
        [Test]
        public void PaintedColors_AreTheVertexColorsOfTheirSurface()
        {
            var model = Model();
            var box   = Box(model, new Vector3(2, 0, 3));
            Update();
            AssertValuesAt(RenderMesh(model), VertexAttribute.Color, new Vector3(2, 1, 3), Vector4.one, "nothing painted: the default");

            PaintTop(box, ChiselPaintAttribute.Color, ChiselPaintComponents.XYZW);
            Update();
            var mesh = RenderMesh(model);
            Assert.That(mesh.HasVertexAttribute(VertexAttribute.Color), Is.True);
            AssertValuesAt(mesh, VertexAttribute.Color, new Vector3(2, 1, 3), new Vector4(1, 0, 0, 1), "the top's (0,0)");
            AssertValuesAt(mesh, VertexAttribute.Color, new Vector3(3, 1, 3), new Vector4(0, 1, 0, 1), "the top's (1,0)");
            AssertValuesAt(mesh, VertexAttribute.Color, new Vector3(2, 1, 4), new Vector4(0, 0, 1, 1), "the top's (0,1)");
            var colors = mesh.colors;
            var normals = mesh.normals;
            for (int v = 0; v < colors.Length; v++)
            {
                if (normals[v].y < 0.99f)
                    Assert.That((Vector4)colors[v], Is.EqualTo(Vector4.one), "the other surfaces aren't painted: the default");
            }

            box.RemoveSurfacePaint(SurfaceFacing(box, new float3(0, 1, 0)), ChiselPaintAttribute.Color);
            Update();
            AssertValuesAt(RenderMesh(model), VertexAttribute.Color, new Vector3(2, 1, 3), Vector4.one, "the paint removed: the default");
        }

        // Paint in one component of a texture coordinate leaves the others at the default
        [Test]
        public void PaintInOneComponent_LeavesTheOthersAtTheDefault()
        {
            var model = Model();
            var box   = Box(model, Vector3.zero);
            var paint = PaintTop(box, ChiselPaintAttribute.TexCoord3, ChiselPaintComponents.Y);
            Update();
            var mesh = RenderMesh(model);
            Assert.That(mesh.HasVertexAttribute(VertexAttribute.TexCoord3), Is.True);
            // the other components take the profile's default (the mask mode's 0)
            AssertValuesAt(mesh, VertexAttribute.TexCoord3, new Vector3(1, 1, 0), new Vector4(0, 1, 0, 0), "the green corner's y, the default elsewhere");
            AssertValuesAt(mesh, VertexAttribute.TexCoord3, new Vector3(0, 1, 0), new Vector4(0, 0, 0, 0), "the red corner's y");

            // a value changed in place shows at the next update
            paint[0, 0] = new Vector4(0.25f, 0.25f, 0.25f, 0.25f);
            box.PaintChanged();
            Update();
            AssertValuesAt(RenderMesh(model), VertexAttribute.TexCoord3, new Vector3(0, 1, 0), new Vector4(0, 0.25f, 0, 0), "changed");
        }

        // A grid of quarter-metre cells on the top: all of its 25 points are vertices, the top is the grid's 32 half cells,
        // and the sides, which aren't painted, keep their two triangles each
        [Test]
        public void APaintedSurface_IsSubdividedAtItsGridPointsInsideIt()
        {
            var model = Model();
            var box   = Box(model, Vector3.zero);
            var top   = SurfaceFacing(box, new float3(0, 1, 0));
            var paint = ChiselSurfacePaint.Create(top, ChiselPaintAttribute.Color, ChiselPaintComponents.XYZW, Vector4.one,
                                                  new Vector3(0, 1, 0), Vector3.right * 0.25f, Vector3.forward * 0.25f, new Vector2Int(5, 5));
            paint[2, 2] = new Vector4(0, 0, 0, 1);
            box.SetSurfacePaint(paint);
            Update();

            var mesh     = RenderMesh(model);
            var vertices = mesh.vertices;
            var normals  = mesh.normals;
            var triangles = mesh.triangles;
            int topVertices = 0, topTriangles = 0, sideTriangles = 0;
            for (int v = 0; v < vertices.Length; v++)
            {
                if (normals[v].y > 0.99f)
                    topVertices++;
            }
            for (int t = 0; t < triangles.Length; t += 3)
            {
                var normal = Vector3.Cross(vertices[triangles[t + 1]] - vertices[triangles[t]], vertices[triangles[t + 2]] - vertices[triangles[t]]).normalized;
                if (normal.y > 0.99f) topTriangles++;
                else if (Mathf.Abs(normal.y) < 0.01f) sideTriangles++;
            }
            Assert.That(topVertices, Is.EqualTo(25), "every grid point");
            Assert.That(topTriangles, Is.EqualTo(32));
            Assert.That(sideTriangles, Is.EqualTo(8), "the sides aren't cut");
            AssertValuesAt(mesh, VertexAttribute.Color, new Vector3(0.5f, 1, 0.5f), new Vector4(0, 0, 0, 1), "the middle point's own value");
        }

        // Painted for the first time, a surface gets a grid of its paint spacing; a dab in its middle paints there, and
        // the mesh shows it
        [Test]
        public void TheBrush_GivesASurfaceAGrid_AndPaintsWhereItDabs()
        {
            var model = Model();
            var box   = Box(model, Vector3.zero);
            var top   = SurfaceFacing(box, new float3(0, 1, 0));
            var mode  = ChiselPaintMode.Color("Color");
            var paint = ChiselPaintBrush.PaintFor(box, top, mode);
            Assert.That(paint, Is.Not.Null);
            Assert.That(paint.resolution, Is.EqualTo(new Vector2Int(5, 5)), "a metre in quarters: 5 points along each side");
            Assert.That(paint.cellU.magnitude, Is.EqualTo(0.25f).Within(1e-5f));
            Assert.That(Mathf.Abs(Vector3.Dot(Vector3.Cross(paint.cellU, paint.cellV).normalized, Vector3.up)), Is.EqualTo(1).Within(1e-5f), "in the top's plane");
            Assert.That(box.GetSurfacePaint(top, ChiselPaintAttribute.Color), Is.SameAs(paint));

            Assert.That(ChiselPaintBrush.Dab(paint, mode, ChiselPaintOperation.Paint, new Vector4(1, 0, 0, 1), new Vector3(0.5f, 1, 0.5f), 0.3f, 1, 1), Is.True);
            box.PaintChanged();
            Update();
            var mesh = RenderMesh(model);
            AssertValuesAt(mesh, VertexAttribute.Color, new Vector3(0.5f, 1, 0.5f), new Vector4(1, 0, 0, 1), "the middle");
            AssertValuesAt(mesh, VertexAttribute.Color, new Vector3(0.25f, 1, 0.25f), Vector4.one, "out of reach (0.35 away)");
        }

        // Two modes in different components of one attribute share its paint: painting the second keeps the first
        [Test]
        public void ModesOfOneAttribute_ShareItsPaint()
        {
            var model  = Model();
            var box    = Box(model, Vector3.zero);
            var top    = SurfaceFacing(box, new float3(0, 1, 0));
            var height = ChiselPaintMode.Value("Height", ChiselPaintAttribute.TexCoord2, ChiselPaintComponents.X, 0, 0, 1);
            var wet    = ChiselPaintMode.Value("Wetness", ChiselPaintAttribute.TexCoord2, ChiselPaintComponents.Y, 0, 0, 1);
            var paint  = ChiselPaintBrush.PaintFor(box, top, height);
            ChiselPaintBrush.Dab(paint, height, ChiselPaintOperation.Add, Vector4.one * 0.5f, new Vector3(0.5f, 1, 0.5f), 2, 1, 1);
            Assert.That(ChiselPaintBrush.PaintFor(box, top, wet), Is.SameAs(paint));
            ChiselPaintBrush.Dab(paint, wet, ChiselPaintOperation.Paint, Vector4.one * 0.75f, new Vector3(0.5f, 1, 0.5f), 2, 1, 1);
            Assert.That(paint.components, Is.EqualTo(ChiselPaintComponents.X | ChiselPaintComponents.Y));
            Assert.That(paint[2, 2].x, Is.EqualTo(0.5f).Within(1e-5f), "the height stays");
            Assert.That(paint[2, 2].y, Is.EqualTo(0.75f).Within(1e-5f));

            ChiselPaintBrush.Dab(paint, height, ChiselPaintOperation.Add, Vector4.one, new Vector3(0.5f, 1, 0.5f), 2, 1, 1);
            Assert.That(paint[2, 2].x, Is.EqualTo(1).Within(1e-5f), "clamped to the mode's range");
            ChiselPaintBrush.Dab(paint, wet, ChiselPaintOperation.Erase, Vector4.zero, new Vector3(0.5f, 1, 0.5f), 2, 1, 1);
            Assert.That(paint[2, 2], Is.EqualTo(new Vector4(1, 0, 0, 0)), "only the wetness erased");
        }

        // Smoothing evens out a spike
        [Test]
        public void Smoothing_EvensOutASpike()
        {
            var model = Model();
            var box   = Box(model, Vector3.zero);
            var top   = SurfaceFacing(box, new float3(0, 1, 0));
            var mode  = ChiselPaintMode.Value("Value", ChiselPaintAttribute.TexCoord2, ChiselPaintComponents.X);
            var paint = ChiselPaintBrush.PaintFor(box, top, mode);
            paint[2, 2] = new Vector4(1, 0, 0, 0);
            ChiselPaintBrush.Dab(paint, mode, ChiselPaintOperation.Smooth, Vector4.zero, new Vector3(0.5f, 1, 0.5f), 2, 1, 1);
            Assert.That(paint[2, 2].x, Is.EqualTo(1f / 9).Within(1e-5f), "the average of it and its 8 neighbours");
            Assert.That(paint[1, 1].x, Is.EqualTo(1f / 9).Within(1e-5f));
        }

        // Three layer weights in one group: painting one takes from the other two, in their proportion, so they always
        // sum to one
        [Test]
        public void TheWeightsOfAGroup_AlwaysSumToOne()
        {
            var model   = Model();
            var box     = Box(model, Vector3.zero);
            var top     = SurfaceFacing(box, new float3(0, 1, 0));
            var profile = new ChiselPaintProfile();
            profile.modes.Add(ChiselPaintMode.Value("Rock", ChiselPaintAttribute.TexCoord4, ChiselPaintComponents.X, 1));
            profile.modes.Add(ChiselPaintMode.Value("Moss", ChiselPaintAttribute.TexCoord4, ChiselPaintComponents.Y, 0));
            profile.modes.Add(ChiselPaintMode.Value("Sand", ChiselPaintAttribute.TexCoord4, ChiselPaintComponents.Z, 0));
            for (int i = 0; i < 3; i++)
            {
                var weighted = profile.modes[i];
                weighted.group = 1;
                profile.modes[i] = weighted;
            }
            Assert.That(profile.GroupOf(1), Is.EqualTo(ChiselPaintComponents.X | ChiselPaintComponents.Z));

            var paint = ChiselPaintBrush.PaintFor(box, top, profile.modes[0]);
            ChiselPaintBrush.PaintFor(box, top, profile.modes[1]);
            ChiselPaintBrush.PaintFor(box, top, profile.modes[2]);
            // the weights are x, y and z; w is no mode's
            Assert.That((Vector3)paint[2, 2], Is.EqualTo(new Vector3(1, 0, 0)), "all rock");

            ChiselPaintBrush.Fill(paint, profile.modes[1], Vector4.one * 0.5f, profile.GroupOf(1));
            Assert.That(Vector3.Distance(paint[2, 2], new Vector3(0.5f, 0.5f, 0)), Is.LessThan(1e-5f), "half moss: the rock gives it");
            ChiselPaintBrush.Dab(paint, profile.modes[2], ChiselPaintOperation.Paint, Vector4.one * 0.5f, new Vector3(0.5f, 1, 0.5f), 0.1f, 1, 1, profile.GroupOf(2));
            Assert.That(Vector3.Distance(paint[2, 2], new Vector3(0.25f, 0.25f, 0.5f)), Is.LessThan(1e-5f), "half sand: rock and moss give it alike");
        }

        // Sampling takes the paint between grid points
        [Test]
        public void Sampling_TakesThePaintBetweenGridPoints()
        {
            var model = Model();
            var box   = Box(model, Vector3.zero);
            var top   = SurfaceFacing(box, new float3(0, 1, 0));
            var mode  = ChiselPaintMode.Color("Color");
            box.SurfaceDefinition.surfaces[top].paintSpacing = 1;
            var paint = ChiselPaintBrush.PaintFor(box, top, mode);
            paint[0] = new Vector4(0, 0, 0, 1); paint[1] = new Vector4(1, 0, 0, 1); paint[2] = new Vector4(0, 1, 0, 1); paint[3] = new Vector4(1, 1, 0, 1);
            var middle = paint.Sample(paint.origin + (paint.cellU + paint.cellV) * 0.5f + Vector3.up * 3);
            Assert.That(Vector4.Distance(middle, new Vector4(0.5f, 0.5f, 0, 1)), Is.LessThan(1e-5f), "between the four, the grid projected along its normal");
        }

        // Paint under a shader that doesn't take it (its profile gone, or another material) is kept, but neither drawn nor
        // subdividing the surface; it comes back with a shader that does
        [Test]
        public void PaintUnderAShaderWithoutAProfile_IsKeptButNotDrawn()
        {
            var model = Model();
            var box   = Box(model, Vector3.zero);
            PaintTop(box, ChiselPaintAttribute.Color, ChiselPaintComponents.XYZW);
            Update();
            Assert.That(RenderMesh(model).HasVertexAttribute(VertexAttribute.Color), Is.True);

            ChiselPaintProfiles.Unregister(profile);
            Update();
            Assert.That(RenderMesh(model).HasVertexAttribute(VertexAttribute.Color), Is.False, "no profile: not drawn");
            Assert.That(box.SurfacePaint.Count, Is.EqualTo(1), "but kept");

            ChiselPaintProfiles.Register(profile);
            Update();
            Assert.That(RenderMesh(model).HasVertexAttribute(VertexAttribute.Color), Is.True, "drawn again");
        }

        // A surface's paint is on a grid of its paint spacing; changing the spacing resamples the paint onto a new grid
        [Test]
        public void ChangingThePaintSpacing_ResamplesThePaint()
        {
            var model = Model();
            var box   = Box(model, Vector3.zero);
            var top   = SurfaceFacing(box, new float3(0, 1, 0));
            var paint = ChiselPaintBrush.PaintFor(box, top, ChiselPaintMode.Color("Color"));
            Assert.That(paint.resolution, Is.EqualTo(new Vector2Int(5, 5)), "the default spacing, 25 cm");
            Assert.That(ChiselPaintBrush.Respace(box), Is.False, "nothing changed");

            var old = PaintTop(box, ChiselPaintAttribute.Color, ChiselPaintComponents.XYZW);
            ChiselPaintBrush.Respace(box);
            Assert.That(box.GetSurfacePaint(top, ChiselPaintAttribute.Color), Is.SameAs(old), "a grid of unknown spacing takes the surface's");

            box.SurfaceDefinition.surfaces[top].paintSpacing = 0.5f;
            Assert.That(ChiselPaintBrush.Respace(box), Is.True);
            var respaced = box.GetSurfacePaint(top, ChiselPaintAttribute.Color);
            Assert.That(respaced, Is.Not.SameAs(old));
            Assert.That(respaced.resolution, Is.EqualTo(new Vector2Int(3, 3)));
            Assert.That(respaced.spacing, Is.EqualTo(0.5f));
            for (int j = 0; j < 3; j++)
            {
                for (int i = 0; i < 3; i++)
                {
                    var point = respaced.origin + i * respaced.cellU + j * respaced.cellV;
                    Assert.That(Vector4.Distance(respaced[i, j], old.Sample(point)), Is.LessThan(1e-5f), $"point {i}, {j}");
                }
            }
            Assert.That(box.SurfacePaint.Count, Is.EqualTo(1));
        }

        // A fill sets the whole surface, wherever it is clicked and whatever the brush's radius
        [Test]
        public void AFill_SetsTheWholeSurface()
        {
            var model = Model();
            var box   = Box(model, Vector3.zero);
            var top   = SurfaceFacing(box, new float3(0, 1, 0));
            var mode  = ChiselPaintMode.Color("Color");
            var paint = ChiselPaintBrush.PaintFor(box, top, mode);
            Assert.That(ChiselPaintBrush.Dab(paint, mode, ChiselPaintOperation.Fill, new Vector4(0, 1, 0, 1), new Vector3(0.1f, 1, 0.1f), 0.01f, 0.1f, 0), Is.True);
            for (int p = 0; p < paint.PointCount; p++)
                Assert.That(paint[p], Is.EqualTo(new Vector4(0, 1, 0, 1)));
        }

        // A surface under a shader that takes paint has its attributes before it is painted, at their modes' defaults
        [Test]
        public void AnUnpaintedSurface_HasItsShadersAttributesAtTheirDefaults()
        {
            var model = Model();
            Box(model, Vector3.zero);
            Update();
            var mesh = RenderMesh(model);
            Assert.That(mesh.HasVertexAttribute(VertexAttribute.Color), Is.True);
            Assert.That(mesh.HasVertexAttribute(VertexAttribute.TexCoord2), Is.True);
            Assert.That(mesh.HasVertexAttribute(VertexAttribute.TexCoord3), Is.True);
            AssertValuesAt(mesh, VertexAttribute.Color, new Vector3(0, 1, 0), new Vector4(1, 1, 1, 1), "the color mode's default");
            AssertValuesAt(mesh, VertexAttribute.TexCoord2, new Vector3(0, 1, 0), Vector4.zero, "the value mode's default");
        }

        // A disabled generator's paint leaves its tree, and comes back with it
        [Test]
        public void ADisabledGenerator_TakesItsPaintWithIt()
        {
            var model = Model();
            var box   = Box(model, Vector3.zero);
            var top   = SurfaceFacing(box, new float3(0, 1, 0));
            PaintTop(box, ChiselPaintAttribute.Color, ChiselPaintComponents.XYZW);
            Update();
            var entityID = EntityId.ToULong(box.GetEntityId());
            bool InTree() => ChiselPaintLookup.Of(ChiselTreeLookup.Value[model.Node]).TryGetRange(entityID, top, out _);
            Assert.That(InTree(), Is.True);

            box.enabled = false;
            Update();
            Assert.That(InTree(), Is.False, "disabled");

            box.enabled = true;
            Update();
            Assert.That(InTree(), Is.True, "enabled again");
        }

        // A grid whose values didn't load is made again when the surface is painted
        [Test]
        public void AGridWithoutItsValues_IsMadeAgainWhenPainted()
        {
            var model = Model();
            var box   = Box(model, Vector3.zero);
            var top   = SurfaceFacing(box, new float3(0, 1, 0));
            var mode  = ChiselPaintMode.Color("Color");
            var broken = ChiselPaintBrush.PaintFor(box, top, mode);
            broken.values = System.Array.Empty<float>();
            Assert.That(broken.IsValid, Is.False);

            var paint = ChiselPaintBrush.PaintFor(box, top, mode);
            Assert.That(paint, Is.Not.SameAs(broken));
            Assert.That(paint.IsValid, Is.True);
            Assert.That(box.GetSurfacePaint(top, ChiselPaintAttribute.Color), Is.SameAs(paint));
        }

        // A scene keeps its saved meshes only while their paint is the same: the paint and what the shaders take of it are
        // in the hash that decides it
        [Test]
        public void TheSavedMeshesInputHash_HasThePaintAndWhatTheShaderTakes()
        {
            var model = Model();
            var box   = Box(model, Vector3.zero);
            Update();
            var unpainted = ChiselModelManager.GetComponentInputHash(model);

            var paint = PaintTop(box, ChiselPaintAttribute.Color, ChiselPaintComponents.XYZW);
            Update();
            var painted = ChiselModelManager.GetComponentInputHash(model);
            Assert.That(painted, Is.Not.EqualTo(unpainted), "painted");

            paint[0, 0] = new Vector4(0.5f, 0.5f, 0.5f, 1);
            Assert.That(ChiselModelManager.GetComponentInputHash(model), Is.Not.EqualTo(painted), "a value changed");
            var repainted = ChiselModelManager.GetComponentInputHash(model);

            ChiselPaintProfiles.Unregister(profile);
            Assert.That(ChiselModelManager.GetComponentInputHash(model), Is.Not.EqualTo(repainted), "the shader takes no paint");
            ChiselPaintProfiles.Register(profile);
            Assert.That(ChiselModelManager.GetComponentInputHash(model), Is.EqualTo(repainted), "and takes it again");
        }

        // The buffer the box's brush was last built into
        static Unity.Entities.BlobAssetReference<ChiselBrushRenderBuffer> BuiltBuffer(ChiselModelComponent model, ChiselBoxComponent box)
        {
            var entityID = EntityId.ToULong(box.GetEntityId());
            var cache = ChiselTreeLookup.Value[model.Node].brushRenderBufferCache;
            for (int b = 0; b < cache.Length; b++)
            {
                if (cache[b].IsCreated && cache[b].Value.entityID == entityID)
                    return cache[b];
            }
            Assert.Fail("the box's brush has no buffer");
            return default;
        }

        // New values on the same grid are copied into the mesh without building the brush again; a new grid builds it
        [Test]
        public void NewValues_AreDrawnWithoutBuildingTheBrush()
        {
            var model = Model();
            var box   = Box(model, Vector3.zero);
            var paint = PaintTop(box, ChiselPaintAttribute.Color, ChiselPaintComponents.XYZW);
            Update();
            var built = BuiltBuffer(model, box);

            paint[0, 0] = new Vector4(0, 1, 1, 1);
            box.PaintChanged();
            Update();
            AssertValuesAt(RenderMesh(model), VertexAttribute.Color, new Vector3(0, 1, 0), new Vector4(0, 1, 1, 1), "the new value");
            Assert.That(BuiltBuffer(model, box), Is.EqualTo(built), "the brush was built again for new values");

            box.SetSurfacePaint(ChiselSurfacePaint.Create(paint.surfaceIndex, paint.attribute, paint.components, Vector4.one,
                                                          paint.origin, paint.cellU * 0.5f, paint.cellV * 0.5f, new Vector2Int(3, 3)));
            Update();
            Assert.That(BuiltBuffer(model, box), Is.Not.EqualTo(built), "a new grid didn't build the brush");
        }

        // The grid moves with its generator
        [Test]
        public void Paint_MovesWithItsGenerator()
        {
            var model = Model();
            var box   = Box(model, Vector3.zero);
            PaintTop(box, ChiselPaintAttribute.Color, ChiselPaintComponents.XYZW);
            Update();
            box.transform.localPosition = new Vector3(5, 0, 0);
            ChiselNodeHierarchyManager.UpdateTreeNodeTransformation(box);
            Update();
            AssertValuesAt(RenderMesh(model), VertexAttribute.Color, new Vector3(5, 1, 0), new Vector4(1, 0, 0, 1), "moved with it");
        }
    }
}
