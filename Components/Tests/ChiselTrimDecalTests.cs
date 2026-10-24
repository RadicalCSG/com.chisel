using System.Collections.Generic;
using Chisel.Core;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.TestTools.Utils;

namespace Chisel.Components.Tests
{
    // Trim decals (ChiselTrimDecals): on the edge between two surfaces, the image's lower half on the first, its upper
    // half on the second, each laid flat on its surface
    [TestFixture]
    public class ChiselTrimDecalTests
    {
        readonly List<Object> created = new();
        Material decalMaterial;

        [SetUp]
        public void SetUp()
        {
            if (Object.FindObjectsByType<ChiselModelComponent>(FindObjectsInactive.Include).Length > 0)
                Assert.Ignore("A Chisel model is loaded; run these in a scene without one");
            decalMaterial = new Material(Shader.Find("Standard")) { name = "TrimTestMaterial" };
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
            ChiselDecalComponentTests.DeleteSheets();
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
            var root = new GameObject("trim test");
            created.Add(root);
            return root.AddComponent<ChiselModelComponent>();
        }

        static ChiselBoxComponent Box(ChiselModelComponent model, float3 min, float3 max)
        {
            var go = new GameObject("Box");
            go.transform.SetParent(model.transform, false);
            var box = go.AddComponent<ChiselBoxComponent>();
            if (math.any(min > box.Max)) { box.Max = max; box.Min = min; }
            else                         { box.Min = min; box.Max = max; }
            return box;
        }

        // The surface (of the generator: its polygon's descriptionIndex) of the box whose outward normal is this one
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

        ChiselDecalComponent Trim(ChiselModelComponent model, ChiselBoxComponent a, int surfaceA, ChiselBoxComponent b, int surfaceB,
                                  float length = 1, float height = 1, ChiselTrimFill fill = ChiselTrimFill.Repeat)
        {
            var go = new GameObject("Trim");
            go.transform.SetParent(model.transform, false);
            var decal = go.AddComponent<ChiselDecalComponent>();
            decal.Material    = decalMaterial;
            decal.Shape       = ChiselDecalShape.Trim;
            decal.TrimFill    = fill;
            decal.Size        = new Vector3(length, height, 1);
            decal.Transparent = false;
            decal.SetTargets(new[]
            {
                new ChiselDecalSurfaceTarget { brush = a, surfaceIndex = surfaceA },
                new ChiselDecalSurfaceTarget { brush = b, surfaceIndex = surfaceB }
            });
            Update();
            return decal;
        }

        struct DecalTriangle
        {
            public float3 a, b, c;
            public float2 uvA, uvB, uvC;
            public float3 normal;
            public double area;
        }

        // The triangles drawn with the trim's material
        List<DecalTriangle> DecalTriangles(ChiselModelComponent model)
        {
            var found = new List<DecalTriangle>();
            foreach (var r in model.generated.renderables)
            {
                if (r == null || !r.Valid || r.sharedMesh == null || r.sharedMesh.vertexCount == 0)
                    continue;
                // every renderer that draws (transparent decals cast no shadows)
                if ((r.query & SurfaceDestinationFlags.Renderable) == 0)
                    continue;
                var vertices = r.sharedMesh.vertices;
                var uvs      = r.sharedMesh.uv;
                for (int s = 0; s < r.sharedMesh.subMeshCount; s++)
                {
                    if (r.renderMaterials == null || s >= r.renderMaterials.Length || r.renderMaterials[s] != decalMaterial)
                        continue;
                    var triangles = r.sharedMesh.GetTriangles(s);
                    for (int t = 0; t < triangles.Length; t += 3)
                    {
                        var cross = Vector3.Cross(vertices[triangles[t + 1]] - vertices[triangles[t]], vertices[triangles[t + 2]] - vertices[triangles[t]]);
                        found.Add(new DecalTriangle
                        {
                            a = vertices[triangles[t]], b = vertices[triangles[t + 1]], c = vertices[triangles[t + 2]],
                            uvA = uvs[triangles[t]], uvB = uvs[triangles[t + 1]], uvC = uvs[triangles[t + 2]],
                            normal = cross.normalized,
                            area = cross.magnitude * 0.5
                        });
                    }
                }
            }
            return found;
        }

        static double AreaFacing(List<DecalTriangle> triangles, float3 normal)
        {
            var area = 0.0;
            foreach (var t in triangles)
            {
                if (math.dot(t.normal, normal) > 0.999f)
                    area += t.area;
            }
            return area;
        }

        const double kTolerance = 1e-4;

        // A 2 m box: the trim on the edge between its top and its +x side, 1 m high, so half a metre on each
        [Test]
        public void ATrimOnABoxsEdge_LiesHalfOnEachSurface()
        {
            var model = Model();
            var box = Box(model, new float3(-1, -1, -1), new float3(1, 1, 1));
            Trim(model, box, SurfaceFacing(box, new float3(0, 1, 0)), box, SurfaceFacing(box, new float3(1, 0, 0)));
            var triangles = DecalTriangles(model);
            Assert.That(AreaFacing(triangles, new float3(0, 1, 0)), Is.EqualTo(1.0).Within(kTolerance), "2 m along the edge, half a metre into the top");
            Assert.That(AreaFacing(triangles, new float3(1, 0, 0)), Is.EqualTo(1.0).Within(kTolerance), "and half a metre down the side");
        }

        // The image's v is 0.5 on the edge, 0 half its height into the first surface, 1 half its height into the second
        [Test]
        public void ATrimsImage_RunsOnAcrossTheEdge()
        {
            var model = Model();
            var box = Box(model, new float3(-1, -1, -1), new float3(1, 1, 1));
            Trim(model, box, SurfaceFacing(box, new float3(0, 1, 0)), box, SurfaceFacing(box, new float3(1, 0, 0)));
            foreach (var t in DecalTriangles(model))
            {
                foreach (var (p, uv) in new[] { (t.a, t.uvA), (t.b, t.uvB), (t.c, t.uvC) })
                {
                    // the distance from the edge (y = 1, x = 1), across the surface the point is on
                    var onTop = t.normal.y > 0.5f;
                    var d = onTop ? 1 - p.x : 1 - p.y;
                    var expected = onTop ? 0.5f - d : 0.5f + d;
                    Assert.That(uv.y, Is.EqualTo(expected).Within(1e-4f), $"v at {p} on the {(onTop ? "top" : "side")}");
                }
            }
        }

        // Mirrored: v is 0.5 on the edge and 0 half its height into either surface
        [Test]
        public void AMirroredTrim_TakesTheLowerHalfOnBothSurfaces()
        {
            var model = Model();
            var box = Box(model, new float3(-1, -1, -1), new float3(1, 1, 1));
            var decal = Trim(model, box, SurfaceFacing(box, new float3(0, 1, 0)), box, SurfaceFacing(box, new float3(1, 0, 0)));
            decal.TrimMirror = true;
            Update();
            var triangles = DecalTriangles(model);
            Assert.That(AreaFacing(triangles, new float3(1, 0, 0)), Is.EqualTo(1.0).Within(kTolerance), "still half a metre down the side");
            foreach (var t in triangles)
            {
                foreach (var (p, uv) in new[] { (t.a, t.uvA), (t.b, t.uvB), (t.c, t.uvC) })
                {
                    var d = t.normal.y > 0.5f ? 1 - p.x : 1 - p.y;
                    Assert.That(uv.y, Is.EqualTo(0.5f - d).Within(1e-4f), $"v at {p}");
                }
            }
        }

        // Picking an edge: the nearest edge of the clicked surface, and the surface across it
        [Test]
        public void TheNearestEdge_OfABoxsTop_LeadsToItsSide()
        {
            var model = Model();
            var box = Box(model, new float3(-1, -1, -1), new float3(1, 1, 1));
            var top  = SurfaceFacing(box, new float3(0, 1, 0));
            var side = SurfaceFacing(box, new float3(1, 0, 0));
            var brush = (CSGTreeBrush)box.TopTreeNode;
            var topPolygon = PolygonOf(brush, top);
            Assert.That(ChiselTrimDecals.NearestEdge(brush, topPolygon, new float3(0.9f, 1, 0.2f), out var from, out var to, out var across), Is.True);
            Assert.That(math.min(from.z, to.z), Is.EqualTo(-1).Within(1e-5f));
            Assert.That(math.max(from.z, to.z), Is.EqualTo(1).Within(1e-5f));
            Assert.That(from.x, Is.EqualTo(1).Within(1e-5f));
            Assert.That(from.y, Is.EqualTo(1).Within(1e-5f));
            Assert.That(ChiselTrimDecals.SurfaceOf(brush, across), Is.EqualTo(side), "the side across the edge");
        }

        [Test]
        public void AFloorsPlane_HoldsTheEdgeOfAWallOnIt()
        {
            var model = Model();
            var floor = Box(model, new float3(-4, -1, -4), new float3(4, 0, 4));
            Box(model, new float3(-1, 0, -0.25f), new float3(1, 2, 0.25f));
            var top = SurfaceFacing(floor, new float3(0, 1, 0));
            var brush = (CSGTreeBrush)floor.TopTreeNode;
            var topPolygon = PolygonOf(brush, top);
            Assert.That(ChiselTrimDecals.PlaneHolds(brush, topPolygon, new float3(-1, 0, 0.25f), new float3(1, 0, 0.25f)), Is.True);
            Assert.That(ChiselTrimDecals.PlaneHolds(brush, topPolygon, new float3(-1, 0, 0.25f), new float3(1, 2, 0.25f)), Is.False);
        }

        static int PolygonOf(CSGTreeBrush brush, int surface)
        {
            ref var mesh = ref BrushMeshManager.GetBrushMeshBlob(brush.BrushMesh).Value;
            for (int p = 0; p < mesh.polygons.Length; p++)
            {
                if (mesh.polygons[p].descriptionIndex == surface)
                    return p;
            }
            return -1;
        }

        // Placing by rule: every edge where a surface of the first material meets one of the second
        [Test]
        public void TheEdgesWhereTwoMaterialsMeet_AreFound()
        {
            var model = Model();
            var box = Box(model, new float3(-1, -1, -1), new float3(1, 1, 1));
            var top = SurfaceFacing(box, new float3(0, 1, 0));
            var topMaterial  = new Material(decalMaterial) { name = "Top" };
            var sideMaterial = new Material(decalMaterial) { name = "Side" };
            created.Add(topMaterial);
            created.Add(sideMaterial);
            var surfaces = box.SurfaceDefinition.surfaces;
            for (int i = 0; i < surfaces.Length; i++)
                surfaces[i].SetMaterial(i == top ? topMaterial : sideMaterial);
            box.OnValidate();
            Update();

            var topTarget = new ChiselDecalSurfaceTarget { brush = box, surfaceIndex = top };
            Assert.That(ChiselTrimDecals.MaterialOf(topTarget), Is.EqualTo(UnityEngine.EntityId.ToULong(topMaterial.GetEntityId())));
            var edges = new List<ChiselTrimDecals.Edge>();
            ChiselTrimDecals.EdgesBetween(model, UnityEngine.EntityId.ToULong(topMaterial.GetEntityId()), UnityEngine.EntityId.ToULong(sideMaterial.GetEntityId()), edges);
            Assert.That(edges.Count, Is.EqualTo(4), "the top's four edges");
            foreach (var edge in edges)
            {
                Assert.That(edge.a.surfaceIndex, Is.EqualTo(top), "the first material's surface first");
                Assert.That(edge.b.surfaceIndex, Is.Not.EqualTo(top));
                Assert.That(edge.middle.y, Is.EqualTo(1).Within(1e-5f), "on the top's edge");
            }
            ChiselTrimDecals.EdgesBetween(model, UnityEngine.EntityId.ToULong(sideMaterial.GetEntityId()), UnityEngine.EntityId.ToULong(sideMaterial.GetEntityId()), edges);
            Assert.That(edges.Count, Is.EqualTo(8), "the four vertical edges and the bottom's four (it has the side material too), each once");
        }

        // An image in a trim sheet: a piece for every image along the edge, each inside its rect of the texture, the last
        // one cut short
        [Test]
        public void ATrimSheetImage_IsAPiecePerImage_InsideItsRect()
        {
            var model = Model();
            var box = Box(model, new float3(-1, -1, -1), new float3(1, 1, 1));
            var decal = Trim(model, box, SurfaceFacing(box, new float3(0, 1, 0)), box, SurfaceFacing(box, new float3(1, 0, 0)), length: 0.8f);
            decal.Material  = decalMaterial = ChiselDecalComponentTests.SheetMaterial(decalMaterial);
            decal.ImageTile = 1;
            Update();
            Assert.That(model.Node.GetDecalCount(), Is.EqualTo(6), "2 m in 0.8 m images: three pieces of two halves");
            var triangles = DecalTriangles(model);
            Assert.That(AreaFacing(triangles, new float3(0, 1, 0)), Is.EqualTo(1.0).Within(kTolerance), "all of the edge still");
            foreach (var t in triangles)
            {
                foreach (var (p, uv) in new[] { (t.a, t.uvA), (t.b, t.uvB), (t.c, t.uvC) })
                {
                    Assert.That(uv.x, Is.InRange(0.5f - 1e-4f, 1 + 1e-4f), $"u at {p}");
                    Assert.That(uv.y, Is.InRange(-1e-4f, 0.25f + 1e-4f), $"v at {p}");
                }
            }
            var halves = new List<ChiselTrimDecals.Half>();
            Assert.That(ChiselTrimDecals.TryDescribe(decal, 1, 1, SurfaceDestinationFlags.Default, out _, halves, out _, out _), Is.True);
            Assert.That(halves.Count, Is.EqualTo(6));
            for (int i = 0; i < halves.Count; i++)
            {
                var last = i >= 4;
                Assert.That(halves[i].decal.settings.size.x, Is.EqualTo(last ? 0.4f : 0.8f).Within(1e-5f), $"piece {i / 2}'s length");
                Assert.That(math.abs(halves[i].decal.settings.uvScale.x), Is.EqualTo(last ? 0.25f : 0.5f).Within(1e-5f), $"piece {i / 2} shows {(last ? "half its" : "all of its")} image");
            }
        }

        // Round corners: a chain of edges round the top of a box, mitred where they meet on the top, the image running on
        ChiselDecalComponent TrimRoundTheTop(ChiselModelComponent model, ChiselBoxComponent box)
        {
            var top = SurfaceFacing(box, new float3(0, 1, 0));
            var decal = Trim(model, box, top, box, SurfaceFacing(box, new float3(1, 0, 0)));
            decal.Transparent = true;
            decal.SetTargets(new[]
            {
                new ChiselDecalSurfaceTarget { brush = box, surfaceIndex = top }, new ChiselDecalSurfaceTarget { brush = box, surfaceIndex = SurfaceFacing(box, new float3( 1, 0, 0)) },
                new ChiselDecalSurfaceTarget { brush = box, surfaceIndex = top }, new ChiselDecalSurfaceTarget { brush = box, surfaceIndex = SurfaceFacing(box, new float3( 0, 0, 1)) },
                new ChiselDecalSurfaceTarget { brush = box, surfaceIndex = top }, new ChiselDecalSurfaceTarget { brush = box, surfaceIndex = SurfaceFacing(box, new float3(-1, 0, 0)) },
                new ChiselDecalSurfaceTarget { brush = box, surfaceIndex = top }, new ChiselDecalSurfaceTarget { brush = box, surfaceIndex = SurfaceFacing(box, new float3( 0, 0,-1)) },
            });
            Update();
            return decal;
        }

        // A 2 m box's top, framed half a metre deep: 4 m2 less the 1 m2 middle, each corner once (overlapping square ends
        // would draw 4 m2); each side half a metre down
        [Test]
        public void ATrimRoundCorners_IsMitredWhereItsEdgesMeet()
        {
            var model = Model();
            var box = Box(model, new float3(-1, -1, -1), new float3(1, 1, 1));
            TrimRoundTheTop(model, box);
            var triangles = DecalTriangles(model);
            Assert.That(AreaFacing(triangles, new float3(0, 1, 0)), Is.EqualTo(3.0).Within(kTolerance), "a mitred frame on the top");
            foreach (var side in new[] { new float3(1, 0, 0), new float3(0, 0, 1), new float3(-1, 0, 0), new float3(0, 0, -1) })
                Assert.That(AreaFacing(triangles, side), Is.EqualTo(1.0).Within(kTolerance), $"the side facing {side}");
        }

        [Test]
        public void ATrimRoundCorners_RunsOnFromEdgeToEdge()
        {
            var model = Model();
            var box = Box(model, new float3(-1, -1, -1), new float3(1, 1, 1));
            TrimRoundTheTop(model, box);
            Assert.That(URange(DecalTriangles(model)), Is.EqualTo(8.0f).Within(1e-3f), "8 m of edges in 1 m images, one after another");
        }

        // A wall on a floor: the trim runs as far as both surfaces reach, the wall's width
        [Test]
        public void ATrimBetweenTwoBrushes_RunsWhereBothReach()
        {
            var model = Model();
            var floor = Box(model, new float3(-4, -1, -4), new float3(4, 0, 4));
            var wall  = Box(model, new float3(-1, 0, -0.25f), new float3(1, 2, 0.25f));
            Trim(model, floor, SurfaceFacing(floor, new float3(0, 1, 0)), wall, SurfaceFacing(wall, new float3(0, 0, 1)));
            var triangles = DecalTriangles(model);
            Assert.That(AreaFacing(triangles, new float3(0, 1, 0)), Is.EqualTo(1.0).Within(kTolerance), "the wall's 2 m along the floor, half a metre into it");
            Assert.That(AreaFacing(triangles, new float3(0, 0, 1)), Is.EqualTo(1.0).Within(kTolerance), "and half a metre up the wall");
        }

        // Its transform stays on its edge (y = 1, x = 1 here), nearest to where it was put
        [Test]
        public void ATrimsTransform_StaysOnItsEdge()
        {
            var model = Model();
            var box = Box(model, new float3(-1, -1, -1), new float3(1, 1, 1));
            var decal = Trim(model, box, SurfaceFacing(box, new float3(0, 1, 0)), box, SurfaceFacing(box, new float3(1, 0, 0)));
            decal.transform.position = new Vector3(5, 3, 0.25f);
            Update();
            Assert.That((Vector3)decal.transform.position, Is.EqualTo(new Vector3(1, 1, 0.25f)).Using(Vector3EqualityComparer.Instance));
            decal.transform.position = new Vector3(0, 0, 9);
            Update();
            Assert.That((Vector3)decal.transform.position, Is.EqualTo(new Vector3(1, 1, 1)).Using(Vector3EqualityComparer.Instance), "the edge's end");
        }

        // A resized brush gets its shape during the update, and the trim follows it in that same update
        [Test]
        public void AResizedBrush_HasItsTrimInTheSameUpdate()
        {
            var model = Model();
            var box = Box(model, new float3(-1, -1, -1), new float3(1, 1, 1));
            Trim(model, box, SurfaceFacing(box, new float3(0, 1, 0)), box, SurfaceFacing(box, new float3(1, 0, 0)));
            box.Max = new float3(1, 1, 3);
            ChiselNodeHierarchyManager.Update();
            ChiselModelManager.Instance.UpdateModels();
            Assert.That(AreaFacing(DecalTriangles(model), new float3(0, 1, 0)), Is.EqualTo(2.0).Within(kTolerance), "4 m along the edge, half a metre into the top");
        }

        // Single: one image at its length, round the decal's position, kept on the edge and its length near the end
        [Test]
        public void ASingleImage_LiesRoundTheDecal()
        {
            var model = Model();
            var box = Box(model, new float3(-1, -1, -1), new float3(1, 1, 1));
            var decal = Trim(model, box, SurfaceFacing(box, new float3(0, 1, 0)), box, SurfaceFacing(box, new float3(1, 0, 0)));
            decal.TrimFill = ChiselTrimFill.Single;
            decal.Size = new Vector3(0.5f, 1, 1);
            decal.transform.position = new Vector3(1, 1, 0.25f);
            Update();
            Assert.That(AreaFacing(DecalTriangles(model), new float3(0, 1, 0)), Is.EqualTo(0.25).Within(kTolerance), "half a metre of the edge, half a metre into the top");
            Assert.That(ZRange(DecalTriangles(model)), Is.EqualTo(new Vector2(0, 0.5f)).Using(Vector2EqualityComparer.Instance));

            decal.transform.position = new Vector3(1, 1, 0.9f);
            Update();
            Assert.That(ZRange(DecalTriangles(model)), Is.EqualTo(new Vector2(0.5f, 1)).Using(Vector2EqualityComparer.Instance), "as long, ending at the edge's end");
        }

        static Vector2 ZRange(List<DecalTriangle> triangles)
        {
            var min = float.PositiveInfinity;
            var max = float.NegativeInfinity;
            foreach (var t in triangles)
            {
                foreach (var p in new[] { t.a, t.b, t.c })
                {
                    min = math.min(min, p.z);
                    max = math.max(max, p.z);
                }
            }
            return new Vector2(min, max);
        }

        // Nothing tells the decal its surfaces moved: it follows them
        [Test]
        public void AMovedWall_TakesItsTrimAlong()
        {
            var model = Model();
            var floor = Box(model, new float3(-4, -1, -4), new float3(4, 0, 4));
            var wall  = Box(model, new float3(-1, 0, -0.25f), new float3(1, 2, 0.25f));
            Trim(model, floor, SurfaceFacing(floor, new float3(0, 1, 0)), wall, SurfaceFacing(wall, new float3(0, 0, 1)));

            wall.transform.localPosition = new Vector3(0.5f, 0, 0);
            ChiselNodeHierarchyManager.UpdateTreeNodeTransformation(wall);
            Update();
            var minX = float.PositiveInfinity;
            var maxX = float.NegativeInfinity;
            foreach (var t in DecalTriangles(model))
            {
                foreach (var p in new[] { t.a, t.b, t.c })
                {
                    minX = math.min(minX, p.x);
                    maxX = math.max(maxX, p.x);
                }
            }
            Assert.That(minX, Is.EqualTo(-0.5f).Within(1e-4f));
            Assert.That(maxX, Is.EqualTo(1.5f).Within(1e-4f));
        }

        [Test]
        public void ATrimBetweenParallelSurfaces_DrawsNothing()
        {
            var model = Model();
            var box = Box(model, new float3(-1, -1, -1), new float3(1, 1, 1));
            Trim(model, box, SurfaceFacing(box, new float3(0, 1, 0)), box, SurfaceFacing(box, new float3(0, -1, 0)));
            Assert.That(DecalTriangles(model), Is.Empty);
        }

        // Repeat counts the edge's images at their length; Fit rounds that to whole images
        [Test]
        public void TheImage_RepeatsAlongTheEdge()
        {
            var model = Model();
            var box = Box(model, new float3(-1, -1, -1), new float3(1, 1, 1));
            var decal = Trim(model, box, SurfaceFacing(box, new float3(0, 1, 0)), box, SurfaceFacing(box, new float3(1, 0, 0)), length: 0.8f);
            Assert.That(URange(DecalTriangles(model)), Is.EqualTo(2.5f).Within(1e-4f), "2 m in 0.8 m images");
            decal.TrimFill = ChiselTrimFill.Fit;
            Update();
            Assert.That(URange(DecalTriangles(model)), Is.EqualTo(2.0f).Within(1e-4f), "2.5 images round to 2");
            decal.TrimFill = ChiselTrimFill.Stretch;
            Update();
            Assert.That(URange(DecalTriangles(model)), Is.EqualTo(1.0f).Within(1e-4f), "one image over the edge");
        }

        static float URange(List<DecalTriangle> triangles)
        {
            var min = float.PositiveInfinity;
            var max = float.NegativeInfinity;
            foreach (var t in triangles)
            {
                foreach (var uv in new[] { t.uvA, t.uvB, t.uvC })
                {
                    min = math.min(min, uv.x);
                    max = math.max(max, uv.x);
                }
            }
            return max - min;
        }

        // Its second half is drawn with another entity id (one decal per id); picking it picks the decal
        [Test]
        public void TheSecondHalf_BelongsToTheDecal()
        {
            var model = Model();
            var box = Box(model, new float3(-1, -1, -1), new float3(1, 1, 1));
            var decal = Trim(model, box, SurfaceFacing(box, new float3(0, 1, 0)), box, SurfaceFacing(box, new float3(1, 0, 0)));
            var id = UnityEngine.EntityId.ToULong(decal.GetEntityId());
            Assert.That(ChiselTrimDecals.DecalEntityID(id | ChiselTrimDecals.kSecondHalf), Is.EqualTo(id));
            Assert.That(model.Node.GetDecalCount(), Is.EqualTo(2), "two halves");
        }
    }
}
