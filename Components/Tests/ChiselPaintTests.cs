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

        [SetUp]
        public void SetUp()
        {
            if (Object.FindObjectsByType<ChiselModelComponent>(FindObjectsInactive.Include).Length > 0)
                Assert.Ignore("A Chisel model is loaded; run these in a scene without one");
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
            Assert.That(RenderMesh(model).HasVertexAttribute(VertexAttribute.Color), Is.False, "nothing painted: no colors");

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
            Assert.That(RenderMesh(model).HasVertexAttribute(VertexAttribute.Color), Is.False, "the paint removed");
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
            Assert.That(mesh.HasVertexAttribute(VertexAttribute.Color), Is.False);
            AssertValuesAt(mesh, VertexAttribute.TexCoord3, new Vector3(1, 1, 0), new Vector4(1, 1, 1, 1), "the green corner's y, the default elsewhere");
            AssertValuesAt(mesh, VertexAttribute.TexCoord3, new Vector3(0, 1, 0), new Vector4(1, 0, 1, 1), "the red corner's y");

            // a value changed in place shows at the next update
            paint[0, 0] = new Vector4(0.25f, 0.25f, 0.25f, 0.25f);
            box.PaintChanged();
            Update();
            AssertValuesAt(RenderMesh(model), VertexAttribute.TexCoord3, new Vector3(0, 1, 0), new Vector4(1, 0.25f, 1, 1), "changed");
        }

        // A grid of quarter-metre cells on the top: its 9 points inside the top are vertices, each of them splitting the
        // top's triangles further; the 16 on its outline and corners aren't, so the sides keep their two triangles each
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
            Assert.That(topVertices, Is.EqualTo(13), "the 4 corners and the 9 points inside");
            Assert.That(topTriangles, Is.EqualTo(20));
            Assert.That(sideTriangles, Is.EqualTo(8), "the sides aren't cut");
            AssertValuesAt(mesh, VertexAttribute.Color, new Vector3(0.5f, 1, 0.5f), new Vector4(0, 0, 0, 1), "the middle point's own value");
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
