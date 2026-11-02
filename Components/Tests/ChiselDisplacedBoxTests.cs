using System.Collections.Generic;
using Chisel.Core;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;

namespace Chisel.Components.Tests
{
    [TestFixture]
    public class ChiselDisplacedBoxTests
    {
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
            {
                if (root)
                    Object.DestroyImmediate(root);
            }
            roots.Clear();
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

        // A box from (0,0,0) to (2,1,2)
        ChiselBoxComponent Box(out ChiselModelComponent model)
        {
            var root = new GameObject("displaced box test");
            roots.Add(root);
            model = root.AddComponent<ChiselModelComponent>();
            var go = new GameObject("box");
            go.transform.SetParent(root.transform, false);
            var box = go.AddComponent<ChiselBoxComponent>();
            box.Min = float3.zero;
            box.Max = new float3(2, 1, 2);
            Update();
            return box;
        }

        // The box's top (surface 0) as a grid of 3 by 3 points, every one at height
        static ChiselSurfacePaint Top(float height)
        {
            var grid = ChiselSurfacePaint.Create(0, ChiselPaintAttribute.Color, ChiselPaintComponents.X, Vector4.zero,
                                                 new Vector3(0, 1, 0), Vector3.right, Vector3.forward, new Vector2Int(3, 3));
            for (int i = 0; i < grid.PointCount; i++)
                grid[i] = new Vector4(height, 0, 0, 0);
            return grid;
        }

        static Bounds DrawnBounds(ChiselModelComponent model)
        {
            var bounds = new Bounds();
            var first = true;
            foreach (var renderable in model.generated.renderables)
            {
                if (renderable == null || renderable.sharedMesh == null || renderable.sharedMesh.vertexCount == 0)
                    continue;
                if (first) bounds = renderable.sharedMesh.bounds;
                else       bounds.Encapsulate(renderable.sharedMesh.bounds);
                first = false;
            }
            Assert.That(first, Is.False, "the model draws nothing");
            return bounds;
        }

        static void AreEqual(Vector3 expected, Vector3 actual, string message)
        {
            Assert.That(Vector3.Distance(expected, actual), Is.LessThan(2e-3f), $"{message}: expected {expected}, got {actual}");
        }

        [Test]
        public void ARaisedTop_MakesTheBoxTaller()
        {
            var box = Box(out var model);
            box.SetSurfaceDisplacement(Top(0.5f));
            Update();
            Assert.That(box.TopTreeNode.Type, Is.EqualTo(CSGNodeType.Branch));
            AreEqual(new Vector3(2, 1.5f, 2), DrawnBounds(model).max, "raised");
            AreEqual(Vector3.zero, DrawnBounds(model).min, "the bottom stays");
        }

        [Test]
        public void AGridReachingPastTheTop_RaisesNothingBeyondTheBox()
        {
            var box = Box(out var model);
            var grid = ChiselSurfacePaint.Create(0, ChiselPaintAttribute.Color, ChiselPaintComponents.X, Vector4.zero,
                                                 new Vector3(-0.5f, 1, -0.5f), Vector3.right, Vector3.forward, new Vector2Int(4, 4));
            for (int i = 0; i < grid.PointCount; i++)
                grid[i] = new Vector4(0.5f, 0, 0, 0);
            box.SetSurfaceDisplacement(grid);
            Update();
            var drawn = DrawnBounds(model);
            AreEqual(Vector3.zero, drawn.min, "nothing beyond the box's sides or below it");
            AreEqual(new Vector3(2, 1.5f, 2), drawn.max, "nothing beyond the box's sides");
        }

        [Test]
        public void ARaisedTop_OfABoxWithAPivot_IsRaisedWhereItIsPainted()
        {
            var box = Box(out var model);
            var pivot = new Vector3(0.5f, 0.25f, 0.75f);
            box.PivotOffset = pivot;
            Update();
            var before = DrawnBounds(model);
            // the grid is in the generator's space, where the brush is moved by the pivot
            var grid = Top(0.5f);
            grid.origin += pivot;
            box.SetSurfaceDisplacement(grid);
            Update();
            var drawn = DrawnBounds(model);
            AreEqual(before.min, drawn.min, "nothing beyond the box's sides or below it");
            AreEqual(before.max + new Vector3(0, 0.5f, 0), drawn.max, "raised");
        }

        [Test]
        public void ARaisedTopAndFront_GrowTheTopsGridOverTheFront()
        {
            var box = Box(out var model);
            box.SetSurfaceDisplacement(Top(0.5f));
            Update();
            // the front (+z, surface 5)
            var front = ChiselSurfacePaint.Create(5, ChiselPaintAttribute.Color, ChiselPaintComponents.X, Vector4.zero,
                                                  new Vector3(0, 0, 2), Vector3.right, Vector3.up, new Vector2Int(3, 2));
            for (int i = 0; i < front.PointCount; i++)
                front[i] = new Vector4(0.25f, 0, 0, 0);
            box.SetSurfaceDisplacement(front);
            Update();
            var top = box.GetSurfaceDisplacement(0);
            var far = top.origin + (top.resolution.x - 1) * top.cellU + (top.resolution.y - 1) * top.cellV;
            Assert.That(Mathf.Max(top.origin.z, far.z), Is.GreaterThanOrEqualTo(2.25f - 1e-4f), "the top's grid reaches over the raised front");
            AreEqual(new Vector3(2, 1.5f, 2.25f), DrawnBounds(model).max, "raised top and front");
        }

        [Test]
        public void ADisplacedTop_IsSmoothedAcrossItsPieces()
        {
            var box = Box(out var model);
            // a ridge along z at x = 1
            var grid = Top(0);
            for (int j = 0; j < grid.resolution.y; j++)
                grid[1, j] = new Vector4(0.5f, 0, 0, 0);
            box.SetSurfaceDisplacement(grid);
            Update();

            var ridge = 0;
            foreach (var renderable in model.generated.renderables)
            {
                if (renderable == null || renderable.sharedMesh == null)
                    continue;
                var positions = renderable.sharedMesh.vertices;
                var normals   = renderable.sharedMesh.normals;
                for (int i = 0; i < positions.Length; i++)
                {
                    if (Mathf.Abs(positions[i].x - 1) > 1e-3f || Mathf.Abs(positions[i].y - 1.5f) > 1e-3f ||
                        positions[i].z < 0.1f || positions[i].z > 1.9f)
                        continue;
                    ridge++;
                    Assert.That(Vector3.Angle(normals[i], Vector3.up), Is.LessThan(1), $"the ridge's normal at {positions[i]} is {normals[i]}, the slopes' average is up");
                }
            }
            Assert.That(ridge, Is.GreaterThan(0), "no vertices along the ridge");
        }

        // The top as a grid of 5 by 5 points, a cell 0.5 by 0.5, raised by 0.5 at its middle point only
        static ChiselSurfacePaint Bump()
        {
            var grid = ChiselSurfacePaint.Create(0, ChiselPaintAttribute.Color, ChiselPaintComponents.X, Vector4.zero,
                                                 new Vector3(0, 1, 0), Vector3.right * 0.5f, Vector3.forward * 0.5f, new Vector2Int(5, 5));
            grid[2, 2] = new Vector4(0.5f, 0, 0, 0);
            return grid;
        }

        [Test]
        public void ABump_BendsIntoTheFlatSurfaceRoundIt_WithoutASeam()
        {
            var box = Box(out var model);
            box.SetSurfaceDisplacement(Bump());
            Update();
            // a point where the bump meets the flat surface
            var border = new Vector3(0.5f, 1, 1);
            var found = new List<Vector3>();
            foreach (var renderable in model.generated.renderables)
            {
                if (renderable == null || renderable.sharedMesh == null)
                    continue;
                var positions = renderable.sharedMesh.vertices;
                var normals   = renderable.sharedMesh.normals;
                for (int i = 0; i < positions.Length; i++)
                {
                    if (Vector3.Distance(positions[i], border) < 1e-3f && normals[i].y > 0.5f)
                        found.Add(normals[i]);
                }
            }
            Assert.That(found.Count, Is.GreaterThan(1), "the bump's and the flat surface's vertices at the border");
            foreach (var normal in found)
                Assert.That(Vector3.Angle(normal, found[0]), Is.LessThan(0.5f), $"a seam at the border: {found[0]} and {normal}");
            Assert.That(Vector3.Angle(found[0], Vector3.up), Is.GreaterThan(1), "the surface bends at the border");
        }

        // Every face round such a point is flat, so its normal is the flat surface's: the walls between the pieces are
        // left out by the CSG, and must be by the smoothing as well
        // A grid point's normal is made from the slopes to the points on either side of it: next to the bump's peak (0.5
        // up, half a metre away) the slope is 0.5 down over a metre, whichever way the cells round it are cut
        [Test]
        public void ABumpsNormals_AreTheHeightGridsSlopes()
        {
            var box = Box(out var model);
            box.SetSurfaceDisplacement(Bump());
            Update();
            var expected = new Dictionary<Vector3, Vector3>
            {
                { new Vector3(1.5f, 1, 1), new Vector3( 0.5f, 1, 0).normalized },
                { new Vector3(0.5f, 1, 1), new Vector3(-0.5f, 1, 0).normalized },
                { new Vector3(1, 1, 1.5f), new Vector3(0, 1,  0.5f).normalized },
                { new Vector3(1, 1, 0.5f), new Vector3(0, 1, -0.5f).normalized },
                { new Vector3(1, 1.5f, 1), Vector3.up }
            };
            var found = 0;
            foreach (var renderable in model.generated.renderables)
            {
                if (renderable == null || renderable.sharedMesh == null)
                    continue;
                var positions = renderable.sharedMesh.vertices;
                var normals   = renderable.sharedMesh.normals;
                for (int i = 0; i < positions.Length; i++)
                {
                    foreach (var pair in expected)
                    {
                        if (Vector3.Distance(positions[i], pair.Key) > 1e-3f || normals[i].y < 0.5f)
                            continue;
                        found++;
                        Assert.That(Vector3.Angle(normals[i], pair.Value), Is.LessThan(0.5f), $"at {pair.Key}: {normals[i]}, the slopes give {pair.Value}");
                    }
                }
            }
            Assert.That(found, Is.GreaterThanOrEqualTo(expected.Count), "the bump's grid points are drawn");
        }

        [Test]
        public void ABump_LeavesTheFlatSurfaceAwayFromItFlat()
        {
            var box = Box(out var model);
            box.SetSurfaceDisplacement(Bump());
            Update();
            var flat = 0;
            foreach (var renderable in model.generated.renderables)
            {
                if (renderable == null || renderable.sharedMesh == null)
                    continue;
                var positions = renderable.sharedMesh.vertices;
                var normals   = renderable.sharedMesh.normals;
                for (int i = 0; i < positions.Length; i++)
                {
                    var away = Mathf.Max(Mathf.Abs(positions[i].x - 1), Mathf.Abs(positions[i].z - 1));
                    if (Mathf.Abs(positions[i].y - 1) > 1e-3f || away < 0.99f || normals[i].y < 0.5f)
                        continue;
                    flat++;
                    Assert.That(Vector3.Angle(normals[i], Vector3.up), Is.LessThan(0.5f), $"the flat surface at {positions[i]} has normal {normals[i]}");
                }
            }
            Assert.That(flat, Is.GreaterThan(0), "no vertices on the flat surface round the bump");
        }

        // A lowered surface is the floor of what cuts into the box: its normals point up out of it, as the flat top's do
        [Test]
        public void ADent_HasNormalsOutOfTheBox()
        {
            var box = Box(out var model);
            var grid = Bump();
            grid[2, 2] = new Vector4(-0.5f, 0, 0, 0);
            box.SetSurfaceDisplacement(grid);
            Update();
            var floor = 0;
            foreach (var renderable in model.generated.renderables)
            {
                if (renderable == null || renderable.sharedMesh == null)
                    continue;
                var positions = renderable.sharedMesh.vertices;
                var normals   = renderable.sharedMesh.normals;
                for (int i = 0; i < positions.Length; i++)
                {
                    var p = positions[i];
                    if (p.y > 1 - 1e-3f || p.y < 0.4f || p.x < 0.6f || p.x > 1.4f || p.z < 0.6f || p.z > 1.4f)
                        continue;
                    floor++;
                    Assert.That(normals[i].y, Is.GreaterThan(0.5f), $"the dent at {p} has normal {normals[i]}");
                }
            }
            Assert.That(floor, Is.GreaterThan(0), "no vertices in the dent");
        }

        // The bump's pieces reach a corner of the box's top: the rest of the top stays flat
        [Test]
        public void ABumpAtACorner_LeavesTheRestOfTheTopFlat()
        {
            var box = Box(out var model);
            var grid = ChiselSurfacePaint.Create(0, ChiselPaintAttribute.Color, ChiselPaintComponents.X, Vector4.zero,
                                                 new Vector3(0, 1, 0), Vector3.right * 0.5f, Vector3.forward * 0.5f, new Vector2Int(5, 5));
            grid[1, 1] = new Vector4(0.5f, 0, 0, 0);
            box.SetSurfaceDisplacement(grid);
            Update();
            var flat = 0;
            foreach (var renderable in model.generated.renderables)
            {
                if (renderable == null || renderable.sharedMesh == null)
                    continue;
                var positions = renderable.sharedMesh.vertices;
                var normals   = renderable.sharedMesh.normals;
                for (int i = 0; i < positions.Length; i++)
                {
                    var away = Mathf.Max(Mathf.Abs(positions[i].x - 0.5f), Mathf.Abs(positions[i].z - 0.5f));
                    if (Mathf.Abs(positions[i].y - 1) > 1e-3f || away < 0.99f || normals[i].y < 0.5f)
                        continue;
                    flat++;
                    Assert.That(Vector3.Angle(normals[i], Vector3.up), Is.LessThan(0.5f), $"the flat top at {positions[i]} has normal {normals[i]}");
                }
            }
            Assert.That(flat, Is.GreaterThan(0), "no vertices on the flat top away from the bump");
        }

        [Test]
        public void ADisplacedTop_KeepsTheFlatTopsUVs_MovedWithIt()
        {
            var box = Box(out var model);
            box.SetSurfaceDisplacement(Bump());
            Update();
            var plane = MathExtensions.GenerateLocalToPlaneSpaceMatrix(new float4(0, 1, 0, -1));
            var toUV  = math.mul(box.surfaceArray.surfaces[0].surfaceDetails.UV0.ToFloat4x4(), plane);
            var top   = new Vector3(1, 1.5f, 1);
            var tops  = 0;
            foreach (var renderable in model.generated.renderables)
            {
                if (renderable == null || renderable.sharedMesh == null)
                    continue;
                var positions = renderable.sharedMesh.vertices;
                var uvs       = renderable.sharedMesh.uv;
                for (int i = 0; i < positions.Length; i++)
                {
                    if (Vector3.Distance(positions[i], top) > 1e-3f)
                        continue;
                    tops++;
                    var expected = math.mul(toUV, new float4(1, 1, 1, 1)).xy;
                    Assert.That(math.distance(expected, (float2)uvs[i]), Is.LessThan(1e-3f), $"the bump's top has uv {uvs[i]}, the flat top had {expected} there");
                }
            }
            Assert.That(tops, Is.GreaterThan(0), "no vertices at the bump's top");
        }

        // A brush with a slanted side and a pivot, its top painted with a bump, and then a dent next to it, as painted in
        // the editor
        [Test]
        public void ABumpOnAPlaneDefinedBrushWithAPivot_HasNoSeams([Values(false, true)] bool andADent)
        {
            var root = new GameObject("displaced brush test");
            roots.Add(root);
            var model = root.AddComponent<ChiselModelComponent>();
            var go = new GameObject("brush");
            go.transform.SetParent(root.transform, false);
            var brush = go.AddComponent<ChiselBrushComponent>();
            var he = new[] { (1,11),(0,6),(4,21),(5,16),(3,13),(0,22),(1,1),(2,10),(6,19),(2,14),(1,7),(5,0),(3,23),(2,4),(6,9),(7,18),(4,3),(7,20),(6,15),(5,8),(4,17),(0,2),(3,5),(7,12) };
            var mesh = new BrushMesh
            {
                vertices = new float3[] { new(-0.75f,0,0.75f), new(-0.75f,0,-0.75f), new(0.75f,0,-0.75f), new(1.7608f,0,1.1f),
                                          new(-0.75f,1.5f,0.75f), new(-0.75f,1.5f,-0.75f), new(0.75f,1.5f,-0.75f), new(1.7608f,1.5f,1.1f) },
                halfEdges = System.Array.ConvertAll(he, h => new BrushMesh.HalfEdge { vertexIndex = h.Item1, twinIndex = h.Item2 }),
                polygons  = new BrushMesh.Polygon[6],
                planes    = new float4[] { new(-1,0,0,-0.75f), new(0,-1,0,0), new(0,0,-1,-0.75f), new(0.87755454f,0,-0.4794768f,-1.0177736f),
                                           new(0,1,0,-1.5f), new(-0.13806282f,0,0.9904235f,-0.84636486f) }
            };
            for (int p = 0; p < 6; p++)
                mesh.polygons[p] = new BrushMesh.Polygon { firstEdge = p * 4, edgeCount = 4, descriptionIndex = p };
            mesh.UpdateHalfEdgePolygonIndices();
            brush.BrushMesh = mesh;
            brush.PivotOffset = new Vector3(0, -0.75f, 0);
            go.transform.localPosition = new Vector3(2.5f, -0.25f, -36.5f);
            go.transform.localRotation = new Quaternion(0, 0.98094183f, 0, 0.19430204f);
            Update();
            var grid = ChiselSurfacePaint.Create(4, ChiselPaintAttribute.Color, ChiselPaintComponents.X, Vector4.zero,
                                                 new Vector3(-0.75f, 0.75f, 1.1f), new Vector3(0.25f, 0, 0), new Vector3(0, 0, -0.25f), new Vector2Int(12, 9));
            var values = new (int, float)[] { (29,.0299f),(30,.0805f),(31,.0358f),(41,.1f),(42,.1221f),(43,.1055f),(44,.0000928f),
                                              (53,.1229f),(54,.1426f),(55,.1319f),(56,.0000887f),(65,.0446f),(66,.0667f),(67,.0496f),(68,.0000095f),
                                              (77,.00214f),(78,.0224f),(79,.00582f) };
            foreach (var (i, v) in values)
                grid[i] = new Vector4(v, 0, 0, 0);
            if (andADent)
            {
                // rows 2 to 5, points 1 to 4
                var dent = new float[,] { { 0, 0, 0, 0 }, { -0.005f, -0.049f, -0.050f, -0.011f }, { -0.020f, -0.050f, -0.050f, -0.030f }, { -0.001f, -0.043f, -0.046f, -0.005f } };
                for (int j = 0; j < 4; j++)
                    for (int i = 0; i < 4; i++)
                        grid[1 + i, 2 + j] = new Vector4(dent[j, i], 0, 0, 0);
            }
            brush.SetSurfaceDisplacement(grid);
            Update();

            var top = new List<(Vector3 position, Vector3 normal)>();
            var toBrush = go.transform.worldToLocalMatrix * root.transform.localToWorldMatrix;
            var below = new System.Text.StringBuilder();
            foreach (var renderable in model.generated.renderables)
            {
                if (renderable == null || renderable.sharedMesh == null)
                    continue;
                var positions = renderable.sharedMesh.vertices;
                var normals   = renderable.sharedMesh.normals;
                for (int i = 0; i < positions.Length; i++)
                {
                    var local = toBrush.MultiplyPoint3x4(positions[i]);
                    if (normals[i].y > 0.5f)
                        top.Add((local, toBrush.MultiplyVector(normals[i])));
                    if (!andADent && local.y > 0.5f && local.y < 0.749f && below.Length < 2000)
                        below.AppendLine($"below the top: {local}");
                }
            }
            Assert.That(below.Length, Is.Zero, below.ToString());
            var seams = new System.Text.StringBuilder();
            for (int i = 0; i < top.Count; i++)
            {
                for (int j = i + 1; j < top.Count; j++)
                {
                    if (Vector3.Distance(top[i].position, top[j].position) < 1e-4f && Vector3.Angle(top[i].normal, top[j].normal) >= 1 && seams.Length < 3000)
                        seams.AppendLine($"a seam at {top[i].position}: {top[i].normal} and {top[j].normal}");
                }
            }
            Assert.That(seams.Length, Is.Zero, seams.ToString());
        }

        static List<Vector3> DrawnTop(ChiselModelComponent model, Vector3 offset)
        {
            var points = new List<Vector3>();
            foreach (var renderable in model.generated.renderables)
            {
                if (renderable == null || renderable.sharedMesh == null)
                    continue;
                foreach (var p in renderable.sharedMesh.vertices)
                {
                    var q = p - offset;
                    points.Add(new Vector3(Mathf.Round(q.x * 1000) / 1000, Mathf.Round(q.y * 1000) / 1000, Mathf.Round(q.z * 1000) / 1000));
                }
            }
            points.Sort((a, b) => a.x != b.x ? a.x.CompareTo(b.x) : a.y != b.y ? a.y.CompareTo(b.y) : a.z.CompareTo(b.z));
            return points;
        }

        // Dab after dab, as the paint tool does, it comes out as what the same heights make at once
        [Test]
        public void PaintedDabByDab_IsWhatTheHeightsMakeAtOnce()
        {
            var painted = Box(out var paintedModel);
            var heights = ChiselPaintBrush.DisplacementFor(painted, 0);
            var mode = ChiselPaintBrush.kDisplacementMode;
            var dabs = new[] { (ChiselPaintOperation.Add, new Vector3(1, 1, 1)), (ChiselPaintOperation.Add, new Vector3(1.2f, 1, 0.8f)),
                               (ChiselPaintOperation.Subtract, new Vector3(0.7f, 1, 1.1f)), (ChiselPaintOperation.Add, new Vector3(1.5f, 1, 1.4f)),
                               (ChiselPaintOperation.Subtract, new Vector3(1, 1, 1)), (ChiselPaintOperation.Subtract, new Vector3(1, 1, 1)) };
            foreach (var (operation, at) in dabs)
            {
                heights = ChiselPaintBrush.DisplacementFor(painted, 0);
                ChiselPaintBrush.Dab(heights, mode, operation, Vector4.one * 0.3f, at, 0.5f, 0.5f, 0.5f);
                painted.DisplacementChanged();
                Update();
            }

            var root = new GameObject("at once");
            roots.Add(root);
            root.transform.position = new Vector3(10, 0, 0);
            var model = root.AddComponent<ChiselModelComponent>();
            var go = new GameObject("box");
            go.transform.SetParent(root.transform, false);
            var once = go.AddComponent<ChiselBoxComponent>();
            once.Min = float3.zero;
            once.Max = new float3(2, 1, 2);
            Update();
            var copy = painted.GetSurfaceDisplacement(0);
            var grid = ChiselSurfacePaint.Create(0, copy.attribute, copy.components, copy.defaultValue, copy.origin, copy.cellU, copy.cellV, copy.resolution);
            copy.values.CopyTo(grid.values, 0);
            once.SetSurfaceDisplacement(grid);
            Update();

            var a = DrawnTop(paintedModel, Vector3.zero);
            var b = DrawnTop(model, Vector3.zero);
            Assert.That(a.Count, Is.EqualTo(b.Count), $"painted {painted.TopTreeNode.Count} brushes, at once {once.TopTreeNode.Count}");
            for (int i = 0; i < a.Count; i++)
                Assert.That(Vector3.Distance(a[i], b[i]), Is.LessThan(2e-3f), $"vertex {i}: painted {a[i]}, at once {b[i]}");
        }

        // A dab changes the heights round it only: the columns elsewhere keep the brushes they were made as, and only
        // those round it are made again
        [Test]
        public void ADab_MakesOnlyTheColumnsItChanges()
        {
            var box = Box(out var model);
            box.Min = float3.zero;
            box.Max = new float3(4, 1, 4);
            Update();
            var heights = ChiselPaintBrush.DisplacementFor(box, 0);
            ChiselPaintBrush.Dab(heights, ChiselPaintBrush.kDisplacementMode, ChiselPaintOperation.Add, Vector4.one * 0.2f,
                                 new Vector3(1, 1, 1), 1, 1, 0.5f);
            box.DisplacementChanged();
            Update();
            var first = box.ColumnsMade;
            Assert.That(first, Is.GreaterThan(20), "the first dab makes its columns");

            // a small dab at the other corner
            heights = ChiselPaintBrush.DisplacementFor(box, 0);
            ChiselPaintBrush.Dab(heights, ChiselPaintBrush.kDisplacementMode, ChiselPaintOperation.Add, Vector4.one * 0.2f,
                                 new Vector3(3.5f, 1, 3.5f), 0.3f, 1, 0.5f);
            box.DisplacementChanged();
            Update();
            Assert.That(box.ColumnsMade, Is.LessThan(first / 2), $"made {box.ColumnsMade} columns again, the first dab made {first}");
        }

        // Undoing the dabs that displaced a box makes it one brush again, and redoing them displaced
        [Test]
        public void PaintedThenUndone_IsTheBoxAgain()
        {
            var box = Box(out var model);
            Update();
            UnityEditor.Undo.IncrementCurrentGroup();
            for (int i = 0; i < 2; i++)
            {
                UnityEditor.Undo.RecordObject(box, "Chisel Displace");
                var heights = ChiselPaintBrush.DisplacementFor(box, 0);
                ChiselPaintBrush.Dab(heights, ChiselPaintBrush.kDisplacementMode, ChiselPaintOperation.Add, Vector4.one * 0.3f,
                                     new Vector3(1, 1, 1), 0.5f, 0.5f, 0.5f);
                box.DisplacementChanged();
                Update();
            }
            Assert.That(box.IsDisplaced, Is.True);

            UnityEditor.Undo.PerformUndo();
            Update();
            Assert.That(box.IsDisplaced, Is.False);
            Assert.That(box.TopTreeNode.Type, Is.EqualTo(Core.CSGNodeType.Brush), "one brush again");
            Assert.That(box.OwnBrush.Valid, Is.True);

            UnityEditor.Undo.PerformRedo();
            Update();
            Assert.That(box.IsDisplaced, Is.True);
            Assert.That(box.TopTreeNode.Type, Is.EqualTo(Core.CSGNodeType.Branch), "displaced again");
            Assert.That(box.OwnBrush.Valid, Is.True);
        }

        // Where the drawn surfaces leave a hole: every stretch of a triangle's edge no other triangle has. An edge running
        // past the corners of the triangles across it (a T-junction) is split at them first, so only gaps are left.
        static string OpenEdges(ChiselModelComponent model)
        {
            const float kScale = 20000, kOnEdge = 1e-4f;
            Vector3Int Key(Vector3 p) => new Vector3Int(Mathf.RoundToInt(p.x * kScale), Mathf.RoundToInt(p.y * kScale), Mathf.RoundToInt(p.z * kScale));
            var edges  = new List<(Vector3 a, Vector3 b)>();
            var points = new Dictionary<Vector3Int, Vector3>();
            foreach (var renderable in model.generated.renderables)
            {
                if (renderable == null || renderable.sharedMesh == null)
                    continue;
                var mesh = renderable.sharedMesh;
                var v = mesh.vertices;
                for (int s = 0; s < mesh.subMeshCount; s++)
                {
                    var t = mesh.GetTriangles(s);
                    for (int i = 0; i + 2 < t.Length; i += 3)
                    {
                        for (int c = 0; c < 3; c++)
                        {
                            edges.Add((v[t[i + c]], v[t[i + (c + 1) % 3]]));
                            points[Key(v[t[i + c]])] = v[t[i + c]];
                        }
                    }
                }
            }
            // the corners by cell, to find the ones on an edge
            const float kCell = 0.1f;
            Vector3Int CellOf(Vector3 p) => Vector3Int.FloorToInt(p / kCell);
            var cells = new Dictionary<Vector3Int, List<Vector3>>();
            foreach (var p in points.Values)
            {
                if (!cells.TryGetValue(CellOf(p), out var list))
                    cells[CellOf(p)] = list = new List<Vector3>();
                list.Add(p);
            }
            var count = new Dictionary<(Vector3Int, Vector3Int), int>();
            var on = new List<float>();
            foreach (var (a, b) in edges)
            {
                var d = b - a;
                var length = d.magnitude;
                if (length < kOnEdge)
                    continue;
                on.Clear();
                on.Add(0); on.Add(1);
                var min = Vector3Int.FloorToInt(Vector3.Min(a, b) / kCell) - Vector3Int.one;
                var max = Vector3Int.FloorToInt(Vector3.Max(a, b) / kCell) + Vector3Int.one;
                for (int x = min.x; x <= max.x; x++)
                for (int y = min.y; y <= max.y; y++)
                for (int z = min.z; z <= max.z; z++)
                {
                    if (!cells.TryGetValue(new Vector3Int(x, y, z), out var list))
                        continue;
                    foreach (var p in list)
                    {
                        var along = Vector3.Dot(p - a, d) / (length * length);
                        if (along * length <= kOnEdge || (1 - along) * length <= kOnEdge)
                            continue;
                        if (Vector3.Distance(a + d * along, p) < kOnEdge)
                            on.Add(along);
                    }
                }
                on.Sort();
                for (int i = 0; i + 1 < on.Count; i++)
                {
                    var ka = Key(a + d * on[i]); var kb = Key(a + d * on[i + 1]);
                    if (ka == kb) continue;
                    var k = (ka.x != kb.x ? ka.x < kb.x : ka.y != kb.y ? ka.y < kb.y : ka.z < kb.z) ? (ka, kb) : (kb, ka);
                    count.TryGetValue(k, out var n); count[k] = n + 1;
                }
            }
            var text = new System.Text.StringBuilder();
            foreach (var (k, n) in count)
                if (n == 1 && text.Length < 6000)
                    text.AppendLine($"open {((Vector3)k.Item1 / kScale).ToString("F4")} - {((Vector3)k.Item2 / kScale).ToString("F4")}");
            return text.ToString();
        }

        // A dab over the box's edge, raising or lowering it there: the box is still closed, the tops of neighbouring
        // columns meeting a hair apart over a sliver of the wall between them
        [Test]
        public void ADabOverTheEdge_LeavesNoGap([Values(1.8f, 1.95f, 2.0f)] float x, [Values(1, -1)] int sign)
        {
            var box = Box(out var model);
            var heights = ChiselPaintBrush.DisplacementFor(box, 0);
            ChiselPaintBrush.Dab(heights, ChiselPaintBrush.kDisplacementMode, sign > 0 ? ChiselPaintOperation.Add : ChiselPaintOperation.Subtract, Vector4.one * 0.3f, new Vector3(x, 1, 1), 0.6f, 1, 0.5f);
            box.DisplacementChanged();
            Update();
            Assert.That(OpenEdges(model), Is.Empty);
        }

        // The brush of the scene where gaps showed, its top and its slanted side both painted: where the two meet
        [Test]
        public void ATopAndASidePainted_MeetWithoutGaps([Values] bool placedAsInTheScene, [Values] bool asLoaded)
        {
            var root = new GameObject("displaced brush test");
            roots.Add(root);
            var model = root.AddComponent<ChiselModelComponent>();
            var go = new GameObject("brush");
            go.transform.SetParent(root.transform, false);
            var brush = go.AddComponent<ChiselBrushComponent>();
            var he = new[] { (1,11),(0,6),(4,21),(5,16),(3,13),(0,22),(1,1),(2,10),(6,19),(2,14),(1,7),(5,0),(3,23),(2,4),(6,9),(7,18),(4,3),(7,20),(6,15),(5,8),(4,17),(0,2),(3,5),(7,12) };
            var mesh = new BrushMesh
            {
                vertices = new float3[] { new(-0.75f,0,0.7500001f), new(-0.75f,0,-0.7500001f), new(0.75f,0,-0.7500001f), new(1.7608f,0,1.1f),
                                          new(-0.75f,1.5f,0.7500001f), new(-0.75f,1.5f,-0.7500001f), new(0.75f,1.5f,-0.7500001f), new(1.7608f,1.5f,1.1f) },
                halfEdges = System.Array.ConvertAll(he, h => new BrushMesh.HalfEdge { vertexIndex = h.Item1, twinIndex = h.Item2 }),
                polygons  = new BrushMesh.Polygon[6],
                planes    = new float4[] { new(-1,0,0,-0.75f), new(0,-1,0,0), new(0,0,-1,-0.7500001f), new(0.87755454f,0,-0.4794768f,-1.0177736f),
                                           new(0,1,0,-1.5f), new(-0.13806282f,0,0.9904235f,-0.84636486f) }
            };
            for (int p = 0; p < 6; p++)
                mesh.polygons[p] = new BrushMesh.Polygon { firstEdge = p * 4, edgeCount = 4, descriptionIndex = p };
            mesh.UpdateHalfEdgePolygonIndices();
            brush.BrushMesh = mesh;
            brush.PivotOffset = new Vector3(0, -0.75f, 0);
            if (placedAsInTheScene)
            {
                go.transform.localPosition = new Vector3(2.5f, -0.25f, -36.5f);
                go.transform.localRotation = new Quaternion(0, 0.98094183f, 0, 0.19430204f);
            }
            Update();

            var top = ChiselSurfacePaint.Create(4, ChiselPaintAttribute.Color, ChiselPaintComponents.X, Vector4.zero,
                                                new Vector3(-1.000001f, 0.75f, 1.35f), new Vector3(0.25f, 0, 0), new Vector3(0, 0, -0.25f), new Vector2Int(13, 10));
            var topValues = new float[] {
                0f, 0f, 0f, 0.004747027f, -0.0039969683f, 0f, 0.039344184f, 0.116438866f, 0.14516263f, 0.060942702f, 0f, 0f, 0f,
                0f, 0f, 0f, 0.004747027f, -0.0039969683f, 0f, 0.0678989f, 0.11869353f, 0.16324003f, 0.06228424f, 0f, 0f, 0f,
                0f, 0f, 0.044912025f, -0.10547235f, -0.28255486f, -0.18017301f, 0.09231745f, 0.14348304f, -0.028899636f, -0.00082530826f, 0.014256874f, 0f, 0f,
                0f, 0f, 0.14031222f, 0.037851516f, 0.030189645f, -0.2187822f, 0.17236868f, 0.09313612f, -0.10500312f, -0.24004588f, -0.05675938f, 0f, 0f,
                0.0017982334f, 0.0017982334f, 0.19584718f, 0.22195318f, 0.15527588f, 0.06425437f, 0.24236254f, 0.08862278f, -0.005194608f, -0.3201245f, -0.0582996f, 0f, 0f,
                0f, 0f, 0.019158699f, 0.35790694f, 0.30369824f, 0.12548257f, 0.2669736f, 0.26954445f, 0.16025771f, -0.045555227f, 0f, 0f, 0f,
                0f, 0f, -0.0012308806f, -0.0068365126f, 0.02903774f, -0.0015187673f, -0.08897506f, 0.058648326f, 0.09025221f, 0.000009468198f, 0f, 0f, 0f,
                0f, 0f, 0f, 0f, -0.00086853205f, 0.009835089f, 0.03168268f, 0.061383404f, 0.005816713f, 0f, 0f, 0f, 0f,
                0f, 0f, 0f, 0f, 0f, 0f, 0.0077910344f, 0f, 0f, 0f, 0f, 0f, 0f,
                0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f };
            for (int i = 0; i < topValues.Length; i++)
                top[i] = new Vector4(topValues[i], 0, 0, 0);
            var side = ChiselSurfacePaint.Create(5, ChiselPaintAttribute.Color, ChiselPaintComponents.X, Vector4.zero,
                                                 new Vector3(1.7607994f, 0.74899995f, 1.1f), new Vector3(-0.24760588f, 0, -0.034515698f), new Vector3(0, -0.25f, 0), new Vector2Int(12, 7));
            var sideValues = new float[] {
                0f, 0f, 0.00018745066f, 0.1271496f, 0.142625f, 0.14225072f, 0.0048181238f, 0f, 0f, 0f, 0f, 0f, 0f,
                0f, 0f, 0.093378514f, 0.13965455f, 0.1085732f, 0.005631715f, 0f, 0f, 0f, 0f, 0f, 0f, 0f,
                0f, 0.00084564986f, 0.030190367f, 0.015945716f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f,
                0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f,
                0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f,
                0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f,
                0f, 0f, 0f, 0f, 0f, 0f };
            for (int i = 0; i < sideValues.Length; i++)
                side[i] = new Vector4(sideValues[i], 0, 0, 0);
            brush.SetSurfaceDisplacement(top);
            brush.SetSurfaceDisplacement(side);
            if (asLoaded)
            {
                // as the scene has them when it is opened: the grids as they were saved, not grown over what their
                // neighbours reach, which only painting does
                var field = typeof(ChiselGeneratorComponent).GetField("surfaceDisplacement", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                var stored = (List<ChiselSurfacePaint>)field.GetValue(brush);
                stored.Clear();
                stored.Add(top);
                stored.Add(side);
                brush.SetDirty();
            }
            Update();
            Assert.That(OpenEdges(model), Is.Empty);
        }

        // A displaced box whose own brush is gone (as an undo can leave it) is made again, not left broken
        [Test]
        public void ADisplacedBoxThatLostItsOwnBrush_IsMadeAgain()
        {
            var box = Box(out var model);
            box.SetSurfaceDisplacement(Top(0.3f));
            Update();
            box.OwnBrush.Destroy();
            Assert.DoesNotThrow(() => box.SetDirty());
            Update();
            Assert.That(box.OwnBrush.Valid, Is.True);
            // its own brush, the one displaced surface's raised columns, what is cut, what pushes out
            Assert.That(((Core.CSGTreeBranch)box.TopTreeNode).Count, Is.EqualTo(1 + 1 + 2));
        }

        // The faces between a generator's brushes take a copy of their surface that is never output, whatever its material says
        [Test]
        public void AnExcludedSurface_GoesNowhere()
        {
            var surface = Core.ChiselSurface.Create(null);
            Assert.That(surface.DestinationFlags, Is.Not.EqualTo(Core.SurfaceDestinationFlags.None));
            var hash = surface.GetHashCode();
            surface.excluded = true;
            Assert.That(surface.DestinationFlags, Is.EqualTo(Core.SurfaceDestinationFlags.None));
            Assert.That(surface.GetHashCode(), Is.Not.EqualTo(hash), "a brush made with it is made again");
        }

        // The own brush's face of a displaced surface lies under the columns all over: it is never output, so the CSG does
        // no work on it, while its other faces are as before
        [Test]
        public void TheOwnBrushFaceOfADisplacedSurface_GoesNowhere()
        {
            var box = Box(out var model);
            box.SetSurfaceDisplacement(Top(0.3f));
            Update();
            ref var mesh = ref BrushMeshManager.GetBrushMeshBlob(box.OwnBrush.BrushMesh).Value;
            Assert.That(mesh.polygons.Length, Is.EqualTo(6));
            for (int p = 0; p < mesh.polygons.Length; p++)
            {
                var top = math.dot(math.normalize(mesh.localPlanes[p].xyz), new float3(0, 1, 0)) > 0.999f;
                if (top)
                    Assert.That(mesh.polygons[p].surface.destinationFlags, Is.EqualTo(Core.SurfaceDestinationFlags.None), "the displaced face");
                else
                    Assert.That(mesh.polygons[p].surface.destinationFlags, Is.Not.EqualTo(Core.SurfaceDestinationFlags.None), $"face {p}");
            }
        }

        [Test]
        public void ALoweredTop_CutsIntoTheBox()
        {
            var box = Box(out var model);
            box.SetSurfaceDisplacement(Top(-0.4f));
            Update();
            AreEqual(new Vector3(2, 0.6f, 2), DrawnBounds(model).max, "lowered");
        }

        [Test]
        public void ATopLoweredPastTheBottom_PushesTheBottomOut()
        {
            var box = Box(out var model);
            box.SetSurfaceDisplacement(Top(-1.3f));
            Update();
            var drawn = DrawnBounds(model);
            AreEqual(new Vector3(0, -0.3f - ChiselDisplacement.kMinThickness, 0), drawn.min, "pushed out");
            AreEqual(new Vector3(2, -0.3f, 2), drawn.max, "the top is down at its depth");
        }

        [Test]
        public void ADabOfTheDisplacementMode_RaisesTheSurfaceUnderIt()
        {
            var box = Box(out var model);
            var heights = ChiselPaintBrush.DisplacementFor(box, 0);
            Assert.That(heights, Is.Not.Null, "the top gets a grid of heights");
            Assert.That(ChiselPaintBrush.Dab(heights, ChiselPaintBrush.kDisplacementMode, ChiselPaintOperation.Paint, Vector4.one * 0.25f,
                                             new Vector3(1, 1, 1), 0.5f, 1, 1), Is.True);
            box.DisplacementChanged();
            Update();
            AreEqual(new Vector3(2, 1.25f, 2), DrawnBounds(model).max, "raised under the dab");
        }

        [Test]
        public void ATopMadeFlatAgain_IsABrushAgain()
        {
            var box = Box(out var model);
            box.SetSurfaceDisplacement(Top(0.5f));
            Update();
            box.SetSurfaceDisplacement(Top(0));
            Update();
            Assert.That(box.TopTreeNode.Type, Is.EqualTo(CSGNodeType.Brush));
            AreEqual(new Vector3(2, 1, 2), DrawnBounds(model).max, "flat");
        }
    }
}
