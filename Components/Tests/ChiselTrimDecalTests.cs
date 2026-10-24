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
            // Max far out first, so setting Min never has to move it
            box.Max = new float3(1e4f);
            box.Min = min;
            box.Max = max;
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

        static void Paint(ChiselBoxComponent box, Material material)
        {
            var surfaces = box.SurfaceDefinition.surfaces;
            for (int i = 0; i < surfaces.Length; i++)
                surfaces[i].SetMaterial(material);
            box.OnValidate();
        }

        // A wall standing across the seam of two floor brushes: its trim runs along the wall's whole foot, not only where
        // the one floor brush it names reaches
        [Test]
        public void ATrimBetweenTwoBrushes_RunsAlongTheWholeEdgeOfTheOneThatHasIt()
        {
            var model = Model();
            var left  = Box(model, new float3(-4, -1, -4), new float3(0, 0, 4));
            var right = Box(model, new float3(0, -1, -4), new float3(4, 0, 4));
            var wall  = Box(model, new float3(-1, 0, -1), new float3(1, 2, 1));
            var decal = Trim(model, left, SurfaceFacing(left, new float3(0, 1, 0)), wall, SurfaceFacing(wall, new float3(0, 0, 1)));
            Update();
            var halves = new List<ChiselTrimDecals.Half>();
            Assert.That(ChiselTrimDecals.TryDescribe(decal, 1, 1, SurfaceDestinationFlags.Default, out _, halves, out var from, out var to), Is.True);
            Assert.That(math.min(from.x, to.x), Is.EqualTo(-1).Within(1e-4f), "from the wall's one end");
            Assert.That(math.max(from.x, to.x), Is.EqualTo(1).Within(1e-4f), "to its other end, over the other floor brush");

            // the floor half draws on both floor brushes' tops in front of the wall, the wall half on the wall's front only
            double leftFloor = 0, rightFloor = 0, wallFront = 0, other = 0;
            foreach (var triangle in DecalTriangles(model))
            {
                var middle = (triangle.a + triangle.b + triangle.c) / 3;
                if (triangle.normal.y > 0.9f)      { if (middle.x < 0) leftFloor += triangle.area; else rightFloor += triangle.area; }
                else if (triangle.normal.z > 0.9f) wallFront += triangle.area;
                else                               other += triangle.area;
            }
            Assert.That(leftFloor,  Is.EqualTo(0.5).Within(1e-3), "on the left floor brush");
            Assert.That(rightFloor, Is.EqualTo(0.5).Within(1e-3), "on the right floor brush");
            Assert.That(wallFront,  Is.EqualTo(1.0).Within(1e-3), "on the wall's front");
            Assert.That(other,      Is.EqualTo(0).Within(1e-6), "nowhere else");
        }

        // Seen from its surface, every half's image runs left to right with its top up: not mirrored, whichever way the
        // line the two surfaces meet on happens to run
        [Test]
        public void TrimHalves_AreNotMirrored()
        {
            var model = Model();
            var box   = Box(model, new float3(-6, -1, -1), new float3(-4, 1, 1));
            var floor = Box(model, new float3(-4, -1, -4), new float3(4, 0, 4));
            var wall  = Box(model, new float3(-1, 0, -1), new float3(1, 2, 1));
            var trims = new[]
            {
                Trim(model, box, SurfaceFacing(box, new float3(0, 1, 0)), box, SurfaceFacing(box, new float3(1, 0, 0))),
                Trim(model, box, SurfaceFacing(box, new float3(1, 0, 0)), box, SurfaceFacing(box, new float3(0, 1, 0))),
                Trim(model, floor, SurfaceFacing(floor, new float3(0, 1, 0)), wall, SurfaceFacing(wall, new float3(0, 0, 1))),
                Trim(model, wall, SurfaceFacing(wall, new float3(0, 0, -1)), floor, SurfaceFacing(floor, new float3(0, 1, 0))),
            };
            Update();
            var halves = new List<ChiselTrimDecals.Half>();
            foreach (var trim in trims)
            {
                Assert.That(ChiselTrimDecals.TryDescribe(trim, 1, 1, SurfaceDestinationFlags.Default, out _, halves, out _, out _), Is.True);
                foreach (var half in halves)
                {
                    var toTree = half.decal.decalToTree;
                    var right = math.mul(toTree, new float4(1, 0, 0, 0)).xyz * math.sign(half.decal.settings.uvScale.x);
                    var up    = math.mul(toTree, new float4(0, 1, 0, 0)).xyz * math.sign(half.decal.settings.uvScale.y);
                    var into  = math.mul(toTree, new float4(0, 0, 1, 0)).xyz;
                    Assert.That(math.dot(math.cross(right, up), into), Is.GreaterThan(0), $"{trim.name}: a half is mirrored");
                }
            }
        }

        // Brushes side by side that are nearly flush (one a few millimetres higher, or tilted a fraction of a degree): the
        // trim still lies across their seam
        [TestCase(0.003f, 0f)]
        [TestCase(0f, 0.2f)]
        public void ATrimOnTheSeamOfNearlyFlushBrushes_LiesAcrossIt(float step, float tilt)
        {
            var model = Model();
            var left  = Box(model, new float3(-4, -1, -4), new float3(0, 0, 4));
            var right = Box(model, new float3(-2, -1, -4), new float3(2, step, 4));
            right.transform.localPosition = new Vector3(2, 0, 0);
            right.transform.localRotation = Quaternion.Euler(0, 0, tilt);
            var decal = Trim(model, left, SurfaceFacing(left, new float3(0, 1, 0)), right, SurfaceFacing(right, new float3(0, 1, 0)));
            Update();
            var halves = new List<ChiselTrimDecals.Half>();
            Assert.That(ChiselTrimDecals.TryDescribe(decal, 1, 1, SurfaceDestinationFlags.Default, out _, halves, out var from, out var to), Is.True, "the seam is an edge");
            Assert.That(math.abs(from.x), Is.LessThan(0.01f));
            Assert.That(math.abs(to.z - from.z), Is.EqualTo(8).Within(0.01f), "along the whole seam");
            Assert.That(model.Node.GetDecalCount(), Is.GreaterThan(0), "and it is drawn");
        }

        // A and B flush, C overlapping B by a metre with its top in the same plane: the seam is C's edge on B's top, and the
        // trim lies across it, B's half on the part of B that shows
        [Test]
        public void ATrimOnTheSeamOfOverlappingBrushes_LiesAcrossIt()
        {
            var model = Model();
            Box(model, new float3(-4, -1, -4), new float3(0, 0, 4));
            var b = Box(model, new float3(0, -1, -4), new float3(4, 0, 4));
            var c = Box(model, new float3(3, -1, -4), new float3(7, 0, 4));
            var decal = Trim(model, b, SurfaceFacing(b, new float3(0, 1, 0)), c, SurfaceFacing(c, new float3(0, 1, 0)));
            Update();
            var halves = new List<ChiselTrimDecals.Half>();
            Assert.That(ChiselTrimDecals.TryDescribe(decal, 1, 1, SurfaceDestinationFlags.Default, out _, halves, out var from, out var to), Is.True, "the seam is an edge");
            Assert.That(from.x, Is.EqualTo(3).Within(1e-4f), "on C's edge");
            Assert.That(math.abs(to.z - from.z), Is.EqualTo(8).Within(1e-4f), "along the whole seam");
            for (int h = 0; h < halves.Count; h++)
            {
                var into = math.mul(halves[h].decal.decalToTree, new float4(0, 1, 0, 0)).x;
                Assert.That(h % 2 == 0 ? into < -0.9f : into > 0.9f, Is.True, "B's half towards B, C's into C");
            }
            Assert.That(model.Node.GetDecalCount(), Is.GreaterThan(0), "and it is drawn");
        }

        // A box set into a floor, its top flush with the floor's: two seams between the same two surfaces, and the trim
        // takes the one nearest to where the decal was put (where the edge was picked)
        [TestCase(-3f)]
        [TestCase(-2f)]
        public void OfTwoSeamsBetweenTheSameSurfaces_TheTrimTakesTheOneItWasPutOn(float x)
        {
            var model = Model();
            var floor = Box(model, new float3(-4, -1, -4), new float3(0, 0, 4));
            var inset = Box(model, new float3(-3, -0.5f, -4), new float3(-2, 0, 4));
            var decal = Trim(model, floor, SurfaceFacing(floor, new float3(0, 1, 0)), inset, SurfaceFacing(inset, new float3(0, 1, 0)));
            decal.transform.position = new Vector3(x + 0.1f, 0, 1);
            Update();
            var halves = new List<ChiselTrimDecals.Half>();
            Assert.That(ChiselTrimDecals.TryDescribe(decal, 1, 1, SurfaceDestinationFlags.Default, out _, halves, out var from, out var to), Is.True);
            Assert.That(from.x, Is.EqualTo(x).Within(1e-4f), "on the seam the decal was put on");
            Assert.That(to.x, Is.EqualTo(x).Within(1e-4f));
        }

        // Where two boxes touch, the CSG discards both faces: the edge picker never takes those, the tops are drawn
        [Test]
        public void TheFacesWhereTwoBoxesTouch_AreNotDrawn()
        {
            var model = Model();
            var left  = Box(model, new float3(-4, -1, -4), new float3(0, 0, 4));
            var right = Box(model, new float3(0, -1, -4), new float3(4, 0, 4));
            Update();
            int PolygonFacing(ChiselBoxComponent box, float3 normal)
            {
                ref var mesh = ref BrushMeshManager.GetBrushMeshBlob(((CSGTreeBrush)box.TopTreeNode).BrushMesh).Value;
                for (int p = 0; p < mesh.polygons.Length; p++)
                {
                    if (math.dot(math.normalize(mesh.localPlanes[p].xyz), normal) > 0.999f)
                        return p;
                }
                return -1;
            }
            Assert.That(ChiselTrimDecals.IsDrawn(model, (CSGTreeBrush)left.TopTreeNode,  PolygonFacing(left,  new float3(1, 0, 0))), Is.False, "the left box's face against the right");
            Assert.That(ChiselTrimDecals.IsDrawn(model, (CSGTreeBrush)right.TopTreeNode, PolygonFacing(right, new float3(-1, 0, 0))), Is.False, "the right box's face against the left");
            Assert.That(ChiselTrimDecals.IsDrawn(model, (CSGTreeBrush)left.TopTreeNode,  PolygonFacing(left,  new float3(0, 1, 0))), Is.True, "the left top");
            Assert.That(ChiselTrimDecals.IsDrawn(model, (CSGTreeBrush)right.TopTreeNode, PolygonFacing(right, new float3(-0, 0, 1))), Is.True, "the right box's front");
        }

        // A trim given its edge keeps to it wherever the decal is, and so does one whose decal is dragged along its edge
        // past where another seam between the same surfaces starts
        [Test]
        public void ATrimKeepsToItsEdge_WhereverTheDecalIs()
        {
            var model = Model();
            var floor = Box(model, new float3(-4, -1, -4), new float3(0, 0, 4));
            var inset = Box(model, new float3(-3, -0.5f, -4), new float3(-2, 0, 4));
            var decal = Trim(model, floor, SurfaceFacing(floor, new float3(0, 1, 0)), inset, SurfaceFacing(inset, new float3(0, 1, 0)));

            // the inset top's edge at x = -3, by its number along the outline
            var brush = (CSGTreeBrush)inset.TopTreeNode;
            ref var mesh = ref BrushMeshManager.GetBrushMeshBlob(brush.BrushMesh).Value;
            var top = -1;
            for (int p = 0; p < mesh.polygons.Length; p++)
            {
                if (mesh.polygons[p].descriptionIndex == SurfaceFacing(inset, new float3(0, 1, 0)))
                    top = p;
            }
            var edgeIndex = -1;
            var nodeToTree = brush.NodeToTreeSpaceMatrix;
            for (int e = 0; e < mesh.polygons[top].edgeCount; e++)
            {
                var from = math.transform(nodeToTree, mesh.localVertices[mesh.halfEdges[mesh.polygons[top].firstEdge + e].vertexIndex]);
                var to   = math.transform(nodeToTree, mesh.localVertices[mesh.halfEdges[mesh.polygons[top].firstEdge + (e + 1) % mesh.polygons[top].edgeCount].vertexIndex]);
                if (math.abs(from.x + 3) < 1e-4f && math.abs(to.x + 3) < 1e-4f)
                    edgeIndex = e;
            }
            Assert.That(edgeIndex, Is.GreaterThanOrEqualTo(0));

            decal.SetTrimEdge(true, edgeIndex);
            decal.transform.position = new Vector3(-2, 0, 1);
            Update();
            var halves = new List<ChiselTrimDecals.Half>();
            Assert.That(ChiselTrimDecals.TryDescribe(decal, 1, 1, SurfaceDestinationFlags.Default, out _, halves, out var edgeFrom, out _), Is.True);
            Assert.That(edgeFrom.x, Is.EqualTo(-3).Within(1e-4f), "on the edge it was given, not the seam the decal is nearest");

            // without a given edge, dragged along x = -3 to where the floor's edge at z = 4 meets it, it stays on x = -3
            decal.SetTargets(new[] { new ChiselDecalSurfaceTarget { brush = floor, surfaceIndex = SurfaceFacing(floor, new float3(0, 1, 0)) },
                                     new ChiselDecalSurfaceTarget { brush = inset, surfaceIndex = SurfaceFacing(inset, new float3(0, 1, 0)) } });
            decal.transform.position = new Vector3(-3, 0, 1);
            Assert.That(ChiselTrimDecals.TryDescribe(decal, 1, 1, SurfaceDestinationFlags.Default, out _, halves, out edgeFrom, out _), Is.True);
            Assert.That(edgeFrom.x, Is.EqualTo(-3).Within(1e-4f));
            // at its end, where the shared edge along z = 4 starts too: both run through the decal, and it keeps its line
            decal.transform.position = new Vector3(-3, 0, 4);
            Assert.That(ChiselTrimDecals.TryDescribe(decal, 1, 1, SurfaceDestinationFlags.Default, out _, halves, out edgeFrom, out var edgeTo), Is.True);
            Assert.That(edgeFrom.x, Is.EqualTo(-3).Within(1e-4f), "still on x = -3");
            Assert.That(edgeTo.x, Is.EqualTo(-3).Within(1e-4f));
        }

        // The edge of a box's surface whose two ends both pass the test, by its number along the surface's outline
        static int EdgeOf(ChiselBoxComponent box, float3 normal, System.Func<float3, bool> onIt)
        {
            var brush = (CSGTreeBrush)box.TopTreeNode;
            ref var mesh = ref BrushMeshManager.GetBrushMeshBlob(brush.BrushMesh).Value;
            var nodeToTree = brush.NodeToTreeSpaceMatrix;
            for (int p = 0; p < mesh.polygons.Length; p++)
            {
                if (math.dot(math.normalize(mesh.localPlanes[p].xyz), normal) < 0.999f)
                    continue;
                ref var polygon = ref mesh.polygons[p];
                for (int e = 0; e < polygon.edgeCount; e++)
                {
                    var from = math.transform(nodeToTree, mesh.localVertices[mesh.halfEdges[polygon.firstEdge + e].vertexIndex]);
                    var to   = math.transform(nodeToTree, mesh.localVertices[mesh.halfEdges[polygon.firstEdge + (e + 1) % polygon.edgeCount].vertexIndex]);
                    if (onIt(from) && onIt(to))
                        return e;
                }
            }
            return -1;
        }

        // A chain round the foot of a wall that stands across the seam of two floor brushes, each edge given: the floor
        // band is the 3 x 3 square less the wall's 2 x 2, once (the corners mitred, nothing drawn twice, nothing missing),
        // and the wall band half a metre up all four sides
        [Test]
        public void AChainRoundAWallsFoot_AcrossTwoFloorBrushes_DrawsEachCornerOnce()
        {
            var model = Model();
            var left  = Box(model, new float3(-4, -1, -4), new float3(0, 0, 4));
            var right = Box(model, new float3(0, -1, -4), new float3(4, 0, 4));
            var wall  = Box(model, new float3(-1, 0, -1), new float3(1, 2, 1));
            Update();
            var decal = Trim(model, left, SurfaceFacing(left, new float3(0, 1, 0)), wall, SurfaceFacing(wall, new float3(0, 0, 1)));
            var sides = new[] { new float3(0, 0, 1), new float3(1, 0, 0), new float3(0, 0, -1), new float3(-1, 0, 0) };
            var floors = new[] { left, right, right, left };
            decal.SetTargets(null);
            for (int s = 0; s < sides.Length; s++)
            {
                var side = sides[s];
                var foot = EdgeOf(wall, side, v => math.abs(v.y) < 1e-4f);
                Assert.That(foot, Is.GreaterThanOrEqualTo(0));
                decal.AddTrimEdge(new ChiselDecalSurfaceTarget { brush = floors[s], surfaceIndex = SurfaceFacing(floors[s], new float3(0, 1, 0)) },
                                  new ChiselDecalSurfaceTarget { brush = wall,      surfaceIndex = SurfaceFacing(wall, side) }, true, foot);
            }
            Update();

            double floorArea = 0, wallArea = 0;
            foreach (var triangle in DecalTriangles(model))
            {
                if (triangle.normal.y > 0.9f) floorArea += triangle.area;
                else                          wallArea  += triangle.area;
            }
            Assert.That(floorArea, Is.EqualTo(5).Within(1e-3), "the floor band round the wall, each corner once");
            Assert.That(wallArea,  Is.EqualTo(4).Within(1e-3), "half a metre up each of the wall's four sides");
        }

        // Two floor brushes side by side, their tops in one plane: a trim on the seam lies flat, half on each, along the seam
        [Test]
        public void ATrimOnTheSeamOfTwoFlushBrushes_LiesFlatAcrossIt()
        {
            var model = Model();
            var left  = Box(model, new float3(-4, -1, -4), new float3(0, 0, 4));
            var right = Box(model, new float3(0, -1, -4), new float3(4, 0, 4));
            var decal = Trim(model, left, SurfaceFacing(left, new float3(0, 1, 0)), right, SurfaceFacing(right, new float3(0, 1, 0)));
            Update();
            var halves = new List<ChiselTrimDecals.Half>();
            Assert.That(ChiselTrimDecals.TryDescribe(decal, 1, 1, SurfaceDestinationFlags.Default, out _, halves, out var from, out var to), Is.True, "the seam is an edge");
            Assert.That(from.x, Is.EqualTo(0).Within(1e-4f));
            Assert.That(to.x, Is.EqualTo(0).Within(1e-4f));
            Assert.That(math.abs(to.z - from.z), Is.EqualTo(8).Within(1e-4f), "along the whole seam");
            var across = new List<float>();
            foreach (var half in halves)
                across.Add(math.mul(half.decal.decalToTree, new float4(0, 1, 0, 0)).x);
            Assert.That(across, Has.Some.LessThan(-0.9f).And.Some.GreaterThan(0.9f), "one half into each brush's top, both flat");
            foreach (var half in halves)
                Assert.That(math.abs(math.mul(half.decal.decalToTree, new float4(0, 0, 1, 0)).y), Is.EqualTo(1).Within(1e-4f), "projected straight down");
            Assert.That(model.Node.GetDecalCount(), Is.GreaterThan(0), "and it is drawn");

            // and placing by rule finds the seam between two materials
            var floorA = new Material(decalMaterial) { name = "A" };
            var floorB = new Material(decalMaterial) { name = "B" };
            created.Add(floorA);
            created.Add(floorB);
            Paint(left, floorA);
            Paint(right, floorB);
            Update();
            var edges = new List<ChiselTrimDecals.Edge>();
            ChiselTrimDecals.EdgesBetween(model, UnityEngine.EntityId.ToULong(floorA.GetEntityId()), UnityEngine.EntityId.ToULong(floorB.GetEntityId()), edges);
            Assert.That(edges.Exists(e => e.a.brush == left && e.b.brush == right && math.abs(e.middle.x) < 1e-4f && math.abs(e.middle.y) < 1e-4f),
                        Is.True, "the seam between the two tops");
        }

        // Where two brushes' materials meet: a wall standing on a floor meets it along the four sides of its foot; a wall
        // sunk into the floor, and one that doesn't touch it, don't
        [Test]
        public void TheEdgesWhereTwoBrushesMaterialsMeet_AreFound()
        {
            var model = Model();
            var floor   = Box(model, new float3(-4, -1, -4), new float3(4, 0, 4));
            var wall    = Box(model, new float3(-1, 0, -1), new float3(1, 2, 1));
            var sunk    = Box(model, new float3(2, -0.75f, 2), new float3(3, -0.25f, 3));
            var floating = Box(model, new float3(-3, 1, 2), new float3(-2, 2, 3));
            var floorMaterial = new Material(decalMaterial) { name = "Floor" };
            var wallMaterial  = new Material(decalMaterial) { name = "Wall" };
            created.Add(floorMaterial);
            created.Add(wallMaterial);
            Paint(floor, floorMaterial);
            Paint(wall, wallMaterial);
            Paint(sunk, wallMaterial);
            Paint(floating, wallMaterial);
            Update();

            var edges = new List<ChiselTrimDecals.Edge>();
            ChiselTrimDecals.EdgesBetween(model, UnityEngine.EntityId.ToULong(floorMaterial.GetEntityId()), UnityEngine.EntityId.ToULong(wallMaterial.GetEntityId()), edges);
            Assert.That(edges.Count, Is.EqualTo(4), "the four sides of the wall's foot: " + string.Join("; ", edges.ConvertAll(e => $"{e.a.brush.name}#{e.a.surfaceIndex}/{e.b.brush.name}#{e.b.surfaceIndex} at {e.middle}")));
            var top = SurfaceFacing(floor, new float3(0, 1, 0));
            foreach (var edge in edges)
            {
                Assert.That(edge.a.brush, Is.SameAs(floor), "the first material's surface first");
                Assert.That(edge.a.surfaceIndex, Is.EqualTo(top));
                Assert.That(edge.b.brush, Is.SameAs(wall));
                Assert.That(edge.middle.y, Is.EqualTo(0).Within(1e-5f), "on the floor");
                Assert.That(math.max(math.abs(edge.middle.x), math.abs(edge.middle.z)), Is.EqualTo(1).Within(1e-5f), "along the wall's foot");
            }

            // and a trim on such an edge is drawn
            var decal = Trim(model, floor, top, wall, edges[0].b.surfaceIndex);
            Update();
            Assert.That(model.Node.GetDecalCount(), Is.GreaterThan(0), "the trim between the two brushes is drawn");
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
