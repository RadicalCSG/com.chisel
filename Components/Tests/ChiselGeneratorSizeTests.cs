using System;
using System.Collections.Generic;
using Chisel.Core;
using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace Chisel.Components.Tests
{
    // What every generator keeps to: a negative size is built the right way out, and a size of 0 is never kept
    [TestFixture]
#if UNITY_6000_7_OR_NEWER
    [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup] // only a fixed table of generators
#endif
    public class ChiselGeneratorSizeTests
    {
        readonly List<GameObject> roots = new();

        [SetUp]
        public void SetUp()
        {
            if (UnityEngine.Object.FindObjectsByType<ChiselModelComponent>(FindObjectsInactive.Include).Length > 0)
                Assert.Ignore("A Chisel model is loaded; run these in a scene without one");
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var root in roots)
            {
                if (root)
                    UnityEngine.Object.DestroyImmediate(root);
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

        T Generator<T>(out ChiselModelComponent model) where T : ChiselGeneratorComponent
        {
            var root = new GameObject("generator size test");
            roots.Add(root);
            model = root.AddComponent<ChiselModelComponent>();
            var go = new GameObject(typeof(T).Name);
            go.transform.SetParent(root.transform, false);
            var generator = go.AddComponent<T>();
            Update();
            return generator;
        }

        // Every edge drawn by exactly two triangles, and something drawn; pinches are edges where two closed parts touch
        static void AssertClosed(ChiselModelComponent model, string what, int pinches = 0)
        {
            var vertices = new List<float3>();
            var indices  = new List<int>();
            foreach (var renderable in model.generated.renderables)
            {
                if (renderable == null || renderable.sharedMesh == null)
                    continue;
                var offset = vertices.Count;
                foreach (var v in renderable.sharedMesh.vertices) vertices.Add(v);
                foreach (var i in renderable.sharedMesh.triangles) indices.Add(offset + i);
            }
            Assert.That(indices.Count, Is.GreaterThan(0), $"{what}: nothing drawn");
            using var vertexArray = new NativeArray<float3>(vertices.ToArray(), Allocator.Temp);
            using var indexArray  = new NativeArray<int>(indices.ToArray(), Allocator.Temp);
            var report = MeshManifoldValidation.Classify(vertexArray, indexArray, 0);
            Assert.That(report.boundaryEdgeCount, Is.Zero, $"{what}: edges drawn by one triangle");
            if (report.nonManifoldEdgeCount != pinches)
            {
                // where they are, by position, as the same corner can be in several meshes
                var uses = new Dictionary<(Vector3Int, Vector3Int), int>();
                Vector3Int Key(float3 p) => new Vector3Int(Mathf.RoundToInt(p.x * 1000), Mathf.RoundToInt(p.y * 1000), Mathf.RoundToInt(p.z * 1000));
                for (int t = 0; t < indices.Count; t += 3)
                {
                    for (int e = 0; e < 3; e++)
                    {
                        var a = Key(vertices[indices[t + e]]);
                        var b = Key(vertices[indices[t + ((e + 1) % 3)]]);
                        var key = (a.x < b.x || (a.x == b.x && (a.y < b.y || (a.y == b.y && a.z < b.z)))) ? (a, b) : (b, a);
                        uses[key] = uses.TryGetValue(key, out var n) ? n + 1 : 1;
                    }
                }
                var where = "";
                foreach (var pair in uses)
                {
                    if (pair.Value > 2)
                        where += $" {(Vector3)pair.Key.Item1 / 1000}-{(Vector3)pair.Key.Item2 / 1000} x{pair.Value}";
                }
                Assert.Fail($"{what}: {report.nonManifoldEdgeCount} edges drawn by more than two triangles:{where}");
            }
            Assert.That(report.degenerateTriangleCount, Is.Zero, $"{what}: triangles without area");
        }

        // Sets a generator's height, and reads it back once validated
        static readonly (Type type, Action<ChiselGeneratorComponent, float> set, Func<ChiselGeneratorComponent, float> get)[] kHeights =
        {
            (typeof(ChiselCylinderComponent),   (g, h) => ((ChiselCylinderComponent)g).definition.settings.height = h,
                                                g      => ((ChiselCylinderComponent)g).definition.settings.height),
            (typeof(ChiselCapsuleComponent),    (g, h) => ((ChiselCapsuleComponent)g).definition.settings.height = h,
                                                g      => ((ChiselCapsuleComponent)g).definition.settings.height),
            (typeof(ChiselSphereComponent),     (g, h) => ((ChiselSphereComponent)g).definition.settings.diameterXYZ.y = h,
                                                g      => ((ChiselSphereComponent)g).definition.settings.diameterXYZ.y),
            (typeof(ChiselHemisphereComponent), (g, h) => ((ChiselHemisphereComponent)g).definition.settings.diameterXYZ.y = h,
                                                g      => ((ChiselHemisphereComponent)g).definition.settings.diameterXYZ.y),
        };

        ChiselGeneratorComponent WithHeight(int which, float height, out ChiselModelComponent model)
        {
            var root = new GameObject("generator size test");
            roots.Add(root);
            model = root.AddComponent<ChiselModelComponent>();
            var go = new GameObject(kHeights[which].type.Name);
            go.transform.SetParent(root.transform, false);
            var generator = (ChiselGeneratorComponent)go.AddComponent(kHeights[which].type);
            Update();
            kHeights[which].set(generator, height);
            generator.OnValidate();
            Update();
            return generator;
        }

        [Test]
        public void ANegativeHeight_IsBuiltTheRightWayOut([Values(0, 1, 2, 3)] int which)
        {
            var generator = WithHeight(which, -1.5f, out var model);
            Assert.That(kHeights[which].get(generator), Is.EqualTo(-1.5f), "the height isn't kept as given");
            AssertClosed(model, kHeights[which].type.Name);
        }

        [Test]
        public void AZeroHeight_IsTheSmallest([Values(0, 1, 2, 3)] int which)
        {
            var generator = WithHeight(which, 0, out var model);
            Assert.That(math.abs(kHeights[which].get(generator)), Is.EqualTo(ChiselGeneratorLimits.kMinSize).Within(1e-7f));
            Assert.That(generator.TopTreeNode.Valid, Is.True, "no brush");
        }

        // Linear stairs of no height are the smallest height there is, and built
        [Test]
        public void LinearStairsOfNoHeight_AreTheSmallest()
        {
            var stairs = Generator<ChiselLinearStairsComponent>(out _);
            var bounds = stairs.definition.settings.bounds;
            bounds.Max.y = bounds.Min.y;
            stairs.definition.settings.bounds = bounds;
            stairs.OnValidate();
            Update();
            var size = stairs.definition.settings.bounds.Max - stairs.definition.settings.bounds.Min;
            Assert.That(math.abs(size.y), Is.EqualTo(ChiselGeneratorLimits.kMinSize).Within(1e-6f));
            Assert.That(stairs.TopTreeNode.Valid, Is.True, "nothing built");
        }

        // Dragged past each other, the stairs' sides stay where they were put (validating moved both of them)
        [Test]
        public void LinearStairsTurnedInsideOut_KeepTheirSides()
        {
            var stairs = Generator<ChiselLinearStairsComponent>(out var model);
            stairs.definition.settings.bounds = new MinMaxAABB { Min = new float3(0, 0, 2), Max = new float3(1.5f, 1, -1) };
            stairs.OnValidate();
            Update();
            Assert.That((Vector3)stairs.definition.settings.bounds.Min, Is.EqualTo(new Vector3(0, 0, 2)), "the near sides moved");
            Assert.That((Vector3)stairs.definition.settings.bounds.Max, Is.EqualTo(new Vector3(1.5f, 1, -1)), "the far sides moved");
            AssertClosed(model, "linear stairs");
        }

        // Filled down, with a rail on the left only: the fill under the steps is the full width, carrying the rail on top of
        // it on the left (the rail is above the steps only, the fill takes the place of the wall below them), and the treads
        // don't reach out past it on the right
        [Test]
        public void LinearStairsFilledDown_WithOneRail_FillTheFullWidth()
        {
            var stairs = Generator<ChiselLinearStairsComponent>(out var model);
            ref var settings = ref stairs.definition.settings;
            settings.bounds    = new MinMaxAABB { Min = new float3(0, 0, 0), Max = new float3(2, 1, 2) };
            settings.riserType = StairsRiserType.FillDown;
            settings.rightSide = StairsSideType.None;
            settings.leftSide  = StairsSideType.DownAndUp;
            stairs.OnValidate();
            Update();
            float minX = float.PositiveInfinity, maxX = float.NegativeInfinity;
            float allMinX = float.PositiveInfinity;
            foreach (var renderable in model.generated.renderables)
            {
                if (renderable == null || renderable.sharedMesh == null)
                    continue;
                foreach (var v in renderable.sharedMesh.vertices)
                {
                    allMinX = math.min(allMinX, v.x);
                    if (v.y > 1e-4f)
                        continue;   // the bottom only
                    minX = math.min(minX, v.x);
                    maxX = math.max(maxX, v.x);
                }
            }
            Assert.That(minX, Is.EqualTo(0).Within(1e-4f), "the fill falls short of the right side");
            Assert.That(maxX, Is.EqualTo(2).Within(1e-4f), "the fill falls short of the left side, under the rail");
            Assert.That(allMinX, Is.EqualTo(0).Within(1e-4f), "the treads reach out past the fill");
        }

        // Every tread's texture starts at its nose: on each tread's top, the uv at the nose's corner is the same
        [Test]
        public void LinearStairsTreads_StartTheirTextureAtTheirNose()
        {
            var stairs = Generator<ChiselLinearStairsComponent>(out var model);
            ref var settings = ref stairs.definition.settings;
            settings.bounds      = new MinMaxAABB { Min = new float3(0, 0, 0), Max = new float3(1, 1, 1.25f) };
            settings.stepHeight  = 0.2f;
            settings.stepDepth   = 0.25f;
            settings.treadHeight = 0.05f;
            settings.nosingDepth = 0.03f;
            stairs.OnValidate();
            Update();
            var starts = new List<Vector2>();
            foreach (var renderable in model.generated.renderables)
            {
                if (renderable == null || renderable.sharedMesh == null)
                    continue;
                var v  = renderable.sharedMesh.vertices;
                var uv = renderable.sharedMesh.uv;
                var t  = renderable.sharedMesh.triangles;
                for (int i = 0; i < t.Length; i += 3)
                {
                    var normal = Vector3.Cross(v[t[i + 1]] - v[t[i]], v[t[i + 2]] - v[t[i]]).normalized;
                    if (normal.y < 0.9f)
                        continue;
                    // the corner at the nose on the low x side: the highest z on this top, the lowest x
                    var best = -1;
                    for (int c = 0; c < 3; c++)
                    {
                        var p = v[t[i + c]];
                        if (best < 0 || p.z > v[t[i + best]].z + 1e-4f ||
                            (Mathf.Abs(p.z - v[t[i + best]].z) <= 1e-4f && p.x < v[t[i + best]].x))
                            best = c;
                    }
                    var corner = v[t[i + best]];
                    if (corner.x > 1e-3f)
                        continue;   // not a triangle with that corner
                    starts.Add(uv[t[i + best]]);
                }
            }
            Assert.That(starts.Count, Is.GreaterThan(1), "no treads");
            foreach (var s in starts)
                Assert.That((s - starts[0]).magnitude, Is.LessThan(1e-3f), "a tread's texture starts somewhere else");
        }

        // The side walls' texture is turned a quarter round: on their faces to the sides u runs along the stairs, not up
        [Test]
        public void LinearStairsSideWalls_HaveTheirTextureTurned()
        {
            var stairs = Generator<ChiselLinearStairsComponent>(out var model);
            ref var settings = ref stairs.definition.settings;
            settings.bounds    = new MinMaxAABB { Min = new float3(0, 0, 0), Max = new float3(2, 1, 2) };
            settings.leftSide  = StairsSideType.DownAndUp;
            settings.rightSide = StairsSideType.DownAndUp;
            stairs.OnValidate();
            Update();
            int faces = 0, alongTheStairs = 0, upsideDown = 0;
            foreach (var renderable in model.generated.renderables)
            {
                if (renderable == null || renderable.sharedMesh == null)
                    continue;
                var v  = renderable.sharedMesh.vertices;
                var uv = renderable.sharedMesh.uv;
                var t  = renderable.sharedMesh.triangles;
                for (int i = 0; i < t.Length; i += 3)
                {
                    Vector3 a = v[t[i]], b = v[t[i + 1]], c = v[t[i + 2]];
                    var normal = Vector3.Cross(b - a, c - a).normalized;
                    // the outsides of the walls, at the stairs' sides
                    if (Mathf.Abs(normal.x) < 0.9f || (Mathf.Abs(a.x) > 1e-3f && Mathf.Abs(a.x - 2) > 1e-3f))
                        continue;
                    // how u changes going up and going along the stairs
                    float z1 = b.z - a.z, y1 = b.y - a.y, z2 = c.z - a.z, y2 = c.y - a.y;
                    var det = z1 * y2 - z2 * y1;
                    if (Mathf.Abs(det) < 1e-6f)
                        continue;
                    float du1 = uv[t[i + 1]].x - uv[t[i]].x, du2 = uv[t[i + 2]].x - uv[t[i]].x;
                    var alongZ = (du1 * y2 - du2 * y1) / det;
                    var alongY = (du2 * z1 - du1 * z2) / det;
                    faces++;
                    if (Mathf.Abs(alongZ) > Mathf.Abs(alongY))
                        alongTheStairs++;
                    // and right side up: v goes up on both sides
                    float dv1 = uv[t[i + 1]].y - uv[t[i]].y, dv2 = uv[t[i + 2]].y - uv[t[i]].y;
                    var vUp = (dv2 * z1 - dv1 * z2) / det;
                    if (vUp <= 0)
                        upsideDown++;
                }
            }
            Assert.That(faces, Is.GreaterThan(0), "no side walls");
            Assert.That(alongTheStairs, Is.EqualTo(faces), $"faces with u still going up, of {faces}");
            Assert.That(upsideDown, Is.Zero, $"faces upside down, of {faces}");
        }

        // The fronts and backs of the steps start their texture at the bottom of each step: on every such face the v at its
        // lowest corner is the same
        [Test]
        // (straight stairs only: on spiral stairs a step's back and the next one's front share a plane, which this can't
        // tell apart)
        public void StairsUprightFaces_StartTheirTextureAtTheBottomOfTheStep([Values(false)] bool spiral)
        {
            ChiselModelComponent model;
            if (spiral)
            {
                var stairs = WideSpiralStairs(out model);
                stairs.definition.settings.innerDiameter = 0;
                stairs.OnValidate();
            } else
            {
                var stairs = Generator<ChiselLinearStairsComponent>(out model);
                stairs.definition.settings.bounds     = new MinMaxAABB { Min = new float3(0, 0, 0), Max = new float3(1, 1, 1.25f) };
                stairs.definition.settings.stepHeight = 0.2f;
                stairs.definition.settings.stepDepth  = 0.25f;
                stairs.OnValidate();
            }
            Update();
            // per face (its plane), the lowest corner and the v there
            var lowestOfFace  = new Dictionary<Vector4, Vector2>();
            var highestOfFace = new Dictionary<Vector4, float>();
            foreach (var renderable in model.generated.renderables)
            {
                if (renderable == null || renderable.sharedMesh == null)
                    continue;
                var v  = renderable.sharedMesh.vertices;
                var uv = renderable.sharedMesh.uv;
                var t  = renderable.sharedMesh.triangles;
                for (int i = 0; i < t.Length; i += 3)
                {
                    Vector3 a = v[t[i]], b = v[t[i + 1]], c = v[t[i + 2]];
                    var normal = Vector3.Cross(b - a, c - a).normalized;
                    if (Mathf.Abs(normal.y) > 0.1f)
                        continue;
                    var middle = (a + b + c) / 3;
                    if (spiral)
                    {
                        // across the stairs: pointing out from the middle
                        var outwards = new Vector2(middle.x, middle.z);
                        if (outwards.sqrMagnitude < 1e-6f || Mathf.Abs(Vector2.Dot(outwards.normalized, new Vector2(normal.x, normal.z))) > 0.1f)
                            continue;
                    } else
                    if (Mathf.Abs(normal.z) < 0.9f)
                        continue;
                    var plane = new Vector4(Mathf.Round(normal.x * 100), Mathf.Round(normal.y * 100), Mathf.Round(normal.z * 100),
                                            Mathf.Round(Vector3.Dot(normal, a) * 1000));
                    for (int k = 0; k < 3; k++)
                    {
                        var p = v[t[i + k]];
                        if (!lowestOfFace.TryGetValue(plane, out var known) || p.y < known.x - 1e-5f)
                            lowestOfFace[plane] = new Vector2(p.y, uv[t[i + k]].y);
                        highestOfFace[plane] = highestOfFace.TryGetValue(plane, out var high) ? Mathf.Max(high, p.y) : p.y;
                    }
                }
            }
            Assert.That(lowestOfFace.Count, Is.GreaterThan(1), "no fronts or backs");
            // the texture starts at the bottom of the step every face is part of (steps of 0.2, a whole number of steps up
            // from the floor, the step its top is in): the v at a face's lowest corner, less how far that is above its
            // step's bottom, is the same
            float first = float.NaN, spread = 0;
            foreach (var face in lowestOfFace)
            {
                var bottom     = face.Value;
                var stepBottom = (Mathf.Ceil(highestOfFace[face.Key] / 0.2f - 1e-3f) - 1) * 0.2f;
                var start      = bottom.y - (bottom.x - stepBottom);
                if (float.IsNaN(first)) first = start;
                spread = Mathf.Max(spread, Mathf.Abs(start - first));
            }
            Assert.That(spread, Is.LessThan(1e-3f), $"faces whose texture starts at another height, of {lowestOfFace.Count}");
        }

        // Filled down, the stairs' back isn't stretched: across it and up it the texture changes as much as anywhere else
        [Test]
        public void LinearStairsFilledDown_HaveAnUnstretchedBack()
        {
            var stairs = Generator<ChiselLinearStairsComponent>(out var model);
            ref var settings = ref stairs.definition.settings;
            settings.bounds      = new MinMaxAABB { Min = new float3(0, 0, 0), Max = new float3(1, 1, 1.25f) };
            settings.riserType   = StairsRiserType.FillDown;
            settings.stepHeight  = 0.2f;
            settings.stepDepth   = 0.2f;
            settings.treadHeight = 0.02f;
            settings.nosingDepth = 0.02f;
            settings.leftSide    = StairsSideType.DownAndUp;
            settings.rightSide   = StairsSideType.DownAndUp;
            settings.sideWidth   = 0.125f;
            settings.sideHeight  = 0.5f;
            stairs.OnValidate();
            Update();
            var faces = 0;
            var stretched = new List<string>();
            foreach (var renderable in model.generated.renderables)
            {
                if (renderable == null || renderable.sharedMesh == null)
                    continue;
                var v  = renderable.sharedMesh.vertices;
                var uv = renderable.sharedMesh.uv;
                var t  = renderable.sharedMesh.triangles;
                for (int i = 0; i < t.Length; i += 3)
                {
                    Vector3 a = v[t[i]], b = v[t[i + 1]], c = v[t[i + 2]];
                    var normal = Vector3.Cross(b - a, c - a).normalized;
                    if (Mathf.Abs(normal.z) < 0.9f || Mathf.Abs(Mathf.Min(a.z, Mathf.Min(b.z, c.z))) > 1e-3f)
                        continue;   // the back, at the far end
                    float x1 = b.x - a.x, y1 = b.y - a.y, x2 = c.x - a.x, y2 = c.y - a.y;
                    var det = x1 * y2 - x2 * y1;
                    if (Mathf.Abs(det) < 1e-6f)
                        continue;
                    Vector2 d1 = uv[t[i + 1]] - uv[t[i]], d2 = uv[t[i + 2]] - uv[t[i]];
                    var alongX = (d1 * y2 - d2 * y1) / det;
                    var alongY = (d2 * x1 - d1 * x2) / det;
                    faces++;
                    if (alongX.magnitude < 0.5f || alongX.magnitude > 2 || alongY.magnitude < 0.5f || alongY.magnitude > 2)
                        stretched.Add($"{alongX} {alongY}");
                }
            }
            Assert.That(faces, Is.GreaterThan(0), "no back");
            Assert.That(stretched, Is.Empty, $"stretched, of {faces}");
        }

        // Filled down, the solid is one shape under the steps plus a piece per step: its back is one face, not a stack of
        // one per step, and it's closed
        [Test]
        public void LinearStairsFilledDown_HaveOneBackFace()
        {
            var stairs = Generator<ChiselLinearStairsComponent>(out var model);
            ref var settings = ref stairs.definition.settings;
            settings.bounds      = new MinMaxAABB { Min = new float3(0, 0, 0), Max = new float3(1, 1, 1.25f) };
            settings.riserType   = StairsRiserType.FillDown;
            settings.stepHeight  = 0.2f;
            settings.stepDepth   = 0.2f;
            stairs.OnValidate();
            Update();
            AssertClosed(model, "filled down stairs");
            var backCorners = new HashSet<Vector3Int>();
            foreach (var renderable in model.generated.renderables)
            {
                if (renderable == null || renderable.sharedMesh == null)
                    continue;
                var v = renderable.sharedMesh.vertices;
                var t = renderable.sharedMesh.triangles;
                for (int i = 0; i < t.Length; i += 3)
                {
                    Vector3 a = v[t[i]], b = v[t[i + 1]], c = v[t[i + 2]];
                    var normal = Vector3.Cross(b - a, c - a).normalized;
                    if (normal.z > -0.9f || Mathf.Abs(a.z) > 1e-3f)
                        continue;   // the back, at the far end, looking away
                    backCorners.Add(Vector3Int.RoundToInt(a * 1000));
                    backCorners.Add(Vector3Int.RoundToInt(b * 1000));
                    backCorners.Add(Vector3Int.RoundToInt(c * 1000));
                }
            }
            Assert.That(backCorners.Count, Is.InRange(4, 6), "the back isn't one face");
        }

        // Steps solid down to the floor have walls down to the floor in one piece, in place of the solid on that side, so
        // the outside of each wall is one face; the stairs stay closed
        [Test]
        public void LinearStairsWallsAboveSolidSteps_AreOnePiece([Values(StairsRiserType.FillDown)] StairsRiserType riser)
        {
            var stairs = Generator<ChiselLinearStairsComponent>(out var model);
            ref var settings = ref stairs.definition.settings;
            settings.bounds     = new MinMaxAABB { Min = new float3(0, 0, 0), Max = new float3(1, 1, 1.25f) };
            settings.riserType  = riser;
            settings.stepHeight = 0.2f;
            settings.stepDepth  = 0.2f;
            settings.leftSide   = StairsSideType.Up;
            settings.rightSide  = StairsSideType.Up;
            stairs.OnValidate();
            Update();
            AssertClosed(model, riser.ToString());
            // the outside of the right wall, at the stairs' side: one sloped face, four corners
            var corners = new HashSet<Vector3Int>();
            foreach (var renderable in model.generated.renderables)
            {
                if (renderable == null || renderable.sharedMesh == null)
                    continue;
                var v = renderable.sharedMesh.vertices;
                var t = renderable.sharedMesh.triangles;
                for (int i = 0; i < t.Length; i += 3)
                {
                    Vector3 a = v[t[i]], b = v[t[i + 1]], c = v[t[i + 2]];
                    var normal = Vector3.Cross(b - a, c - a).normalized;
                    if (normal.x > -0.9f || Mathf.Abs(a.x) > 1e-3f)
                        continue;
                    // above the steps only: the wall's part
                    var top = Mathf.Max(a.y, Mathf.Max(b.y, c.y));
                    corners.Add(Vector3Int.RoundToInt(a * 1000));
                    corners.Add(Vector3Int.RoundToInt(b * 1000));
                    corners.Add(Vector3Int.RoundToInt(c * 1000));
                }
            }
            var stepCount = stairs.definition.settings.StepCount;
            Assert.That(corners.Count, Is.LessThan(stepCount * 2), $"the side is split along the steps, {corners.Count} corners for {stepCount} steps");
        }

        // Filled down with walls, the fronts and back of the walls and of the solid between them run on as one texture:
        // where they meet, facing the same way, the uv is the same on both
        [Test]
        public void LinearStairsFilledDown_HaveTheirFrontAndBackTextureRunOnAcrossTheWalls()
        {
            var stairs = Generator<ChiselLinearStairsComponent>(out var model);
            ref var settings = ref stairs.definition.settings;
            settings.bounds     = new MinMaxAABB { Min = new float3(0, 0, 0), Max = new float3(1, 1, 1.25f) };
            settings.riserType  = StairsRiserType.FillDown;
            settings.stepHeight = 0.2f;
            settings.stepDepth  = 0.2f;
            settings.leftSide   = StairsSideType.DownAndUp;
            settings.rightSide  = StairsSideType.DownAndUp;
            stairs.OnValidate();
            Update();
            var uvAt = new Dictionary<Vector4, Vector2>();
            var seams = 0;
            var shared = 0;
            foreach (var renderable in model.generated.renderables)
            {
                if (renderable == null || renderable.sharedMesh == null)
                    continue;
                var v  = renderable.sharedMesh.vertices;
                var uv = renderable.sharedMesh.uv;
                var t  = renderable.sharedMesh.triangles;
                for (int i = 0; i < t.Length; i += 3)
                {
                    Vector3 a = v[t[i]], b = v[t[i + 1]], c = v[t[i + 2]];
                    var normal = Vector3.Cross(b - a, c - a).normalized;
                    if (Mathf.Abs(normal.z) < 0.9f)
                        continue;   // fronts and back
                    for (int k = 0; k < 3; k++)
                    {
                        var p   = Vector3Int.RoundToInt(v[t[i + k]] * 1000);
                        var key = new Vector4(p.x, p.y, p.z, Mathf.Sign(normal.z));
                        if (uvAt.TryGetValue(key, out var known))
                        {
                            shared++;
                            if ((known - uv[t[i + k]]).magnitude > 1e-3f)
                                seams++;
                        } else
                            uvAt[key] = uv[t[i + k]];
                    }
                }
            }
            Assert.That(shared, Is.GreaterThan(0), "no fronts or back");
            Assert.That(seams, Is.Zero, $"corners with another uv on each side, of {shared}");
        }

        // Thick steps on a base with walls going down: the wall beside the base has its front textured as the base's, so
        // where they meet the uv is the same on both
        [Test]
        public void LinearStairsOnABase_HaveTheirFrontTextureRunOnAcrossTheWalls([Values(StairsRiserType.ThickRiser, StairsRiserType.Smooth)] StairsRiserType riser)
        {
            var stairs = Generator<ChiselLinearStairsComponent>(out var model);
            ref var settings = ref stairs.definition.settings;
            settings.bounds        = new MinMaxAABB { Min = new float3(0, 0, 0), Max = new float3(2, 2, 3) };
            settings.riserType     = riser;
            settings.plateauHeight = 0.6f;
            settings.stepHeight    = 0.2f;
            settings.stepDepth     = 0.2f;
            settings.leftSide      = StairsSideType.DownAndUp;
            settings.rightSide     = StairsSideType.DownAndUp;
            stairs.OnValidate();
            Update();
            var uvAt = new Dictionary<Vector3Int, Vector2>();
            var seams = 0;
            var shared = 0;
            var where = new List<string>();
            foreach (var renderable in model.generated.renderables)
            {
                if (renderable == null || renderable.sharedMesh == null)
                    continue;
                var v  = renderable.sharedMesh.vertices;
                var uv = renderable.sharedMesh.uv;
                var t  = renderable.sharedMesh.triangles;
                for (int i = 0; i < t.Length; i += 3)
                {
                    Vector3 a = v[t[i]], b = v[t[i + 1]], c = v[t[i + 2]];
                    var normal = Vector3.Cross(b - a, c - a).normalized;
                    // the front of the base, below its top: each step's front starts its texture again, as does the wall
                    // above the base, at the steps it's beside
                    if (normal.z < 0.9f || Mathf.Abs(a.z - 3) > 1e-3f)
                        continue;
                    for (int k = 0; k < 3; k++)
                    {
                        if (v[t[i + k]].y > 0.78f - 1e-3f)
                            continue;
                        var key = Vector3Int.RoundToInt(v[t[i + k]] * 1000);
                        if (uvAt.TryGetValue(key, out var known))
                        {
                            shared++;
                            if ((known - uv[t[i + k]]).magnitude > 1e-3f)
                            {
                                seams++;
                                where.Add($"{v[t[i + k]]}: {known} / {uv[t[i + k]]}");
                            }
                        } else
                            uvAt[key] = uv[t[i + k]];
                    }
                }
            }
            Assert.That(shared, Is.GreaterThan(0), "nothing at the front");
            Assert.That(seams, Is.Zero, $"corners with another uv on each side, of {shared}: {string.Join("; ", where)}");
        }

        // Spiral stairs with walls: at the start the front of the first step and at the end the back of the last one run on
        // as one texture with the ends of the walls beside them, the same uv where they meet
        [Test]
        public void SpiralStairsFront_HasItsTextureRunOnAcrossTheWalls()
        {
            var stairs = WideSpiralStairs(out var model);
            stairs.definition.settings.outerSide = StairsSideType.DownAndUp;
            stairs.definition.settings.innerSide = StairsSideType.DownAndUp;
            stairs.OnValidate();
            Update();
            var start = stairs.definition.settings.startAngle * Mathf.Deg2Rad;
            var end   = start + stairs.definition.settings.rotation * Mathf.Deg2Rad;
            var uvAt = new Dictionary<Vector3Int, Vector2>();
            int shared = 0, seams = 0;
            var seamsAt = "";
            var normalAt  = new Dictionary<Vector3Int, Vector3>();
            var cornersAt = new Dictionary<Vector3Int, string>();
            // the front at the start and the back at the end
            foreach (var angle in new[] { start, end })
            {
            var front = new Vector2(Mathf.Sin(angle), Mathf.Cos(angle));
            uvAt.Clear();
            foreach (var renderable in model.generated.renderables)
            {
                if (renderable == null || renderable.sharedMesh == null)
                    continue;
                var v  = renderable.sharedMesh.vertices;
                var uv = renderable.sharedMesh.uv;
                var t  = renderable.sharedMesh.triangles;
                for (int i = 0; i < t.Length; i += 3)
                {
                    Vector3 a = v[t[i]], b = v[t[i + 1]], c = v[t[i + 2]];
                    var cross  = Vector3.Cross(b - a, c - a);
                    // a triangle with no area shows nothing, and has no way it faces
                    if (cross.magnitude < 1e-5f)    // as small as normalized turns to zero
                        continue;
                    var normal = cross.normalized;
                    var middle = (a + b + c) / 3;
                    // on the line out from the middle at the start, facing across it
                    if (Mathf.Abs(normal.y) > 0.1f || Mathf.Abs(Vector2.Dot(new Vector2(normal.x, normal.z), front)) > 0.1f)
                        continue;
                    var along = new Vector2(middle.x, middle.z);
                    if (Mathf.Abs(along.x * front.y - along.y * front.x) > 1e-3f || Vector2.Dot(along, front) < 0)
                        continue;
                    for (int k = 0; k < 3; k++)
                    {
                        var key = Vector3Int.RoundToInt(v[t[i + k]] * 1000);
                        if (uvAt.TryGetValue(key, out var known))
                        {
                            shared++;
                            if ((known - uv[t[i + k]]).magnitude > 1e-3f)
                            {
                                seams++;
                                if (seams <= 4)
                                    seamsAt += $" {(Vector3)v[t[i + k]]:F3}: {known:F3} (normal {normalAt[key]:F2}, triangle {cornersAt[key]}) / {uv[t[i + k]]:F3} (normal {normal:F2}, triangle {a:F3} {b:F3} {c:F3});";
                            }
                        } else
                        {
                            uvAt[key] = uv[t[i + k]];
                            normalAt[key] = normal;
                            cornersAt[key] = $"{a:F3} {b:F3} {c:F3}";
                        }
                    }
                }
            }
            }
            Assert.That(shared, Is.GreaterThan(0), "no front");
            Assert.That(seams, Is.Zero, $"corners on the front with another uv on each side, of {shared}:{seamsAt}");
        }

        // Taller than their steps fit in: the steps keep their depth and start higher, on a base, rather than getting
        // shallower
        [Test]
        public void LinearStairsTallerThanTheirStepsFit_StartHigher()
        {
            var stairs = Generator<ChiselLinearStairsComponent>(out _);
            ref var settings = ref stairs.definition.settings;
            settings.bounds        = new MinMaxAABB { Min = new float3(0, 0, 0), Max = new float3(1, 2, 1) };
            settings.stepHeight    = 0.2f;
            settings.stepDepth     = 0.2f;
            settings.plateauHeight = 0;
            stairs.OnValidate();
            Update();
            Assert.That(stairs.definition.settings.stepDepth, Is.EqualTo(0.2f).Within(1e-5f), "the steps got shallower");
            Assert.That(stairs.definition.settings.StepCount, Is.EqualTo(5), "the steps that fit in a metre");
            Assert.That(stairs.definition.settings.plateauHeight, Is.EqualTo(1).Within(1e-4f), "the base under them");
        }

        // Spiral stairs never turn the other way: a negative rotation is the smallest there is
        [Test]
        public void SpiralStairs_TakeNoNegativeRotation()
        {
            var stairs = Generator<ChiselSpiralStairsComponent>(out _);
            stairs.definition.settings.rotation = -90;
            stairs.OnValidate();
            Update();
            Assert.That(stairs.definition.settings.rotation, Is.EqualTo(ChiselSpiralStairs.kMinRotation));
        }

        // Spiral stairs with nothing under the steps are still built: their treads
        [Test]
        public void SpiralStairsWithNothingUnderTheSteps_AreBuilt()
        {
            var stairs = Generator<ChiselSpiralStairsComponent>(out var model);
            stairs.definition.settings.riserType = StairsRiserType.None;
            stairs.OnValidate();
            Update();
            var triangles = 0;
            foreach (var renderable in model.generated.renderables)
            {
                if (renderable != null && renderable.sharedMesh != null)
                    triangles += renderable.sharedMesh.triangles.Length;
            }
            Assert.That(triangles, Is.GreaterThan(0), "nothing drawn");
        }

        // Spiral stairs with a sloped underside slope under all of each step, out to the edge and in to the middle: none of
        // the pieces it is made of is inside out, which would leave holes in the slope
        [Test]
        public void SpiralStairsWithASlopedUnderside_SlopeAllTheWayIn()
        {
            var stairs = Generator<ChiselSpiralStairsComponent>(out var model);
            stairs.definition.settings.riserType = StairsRiserType.Smooth;
            stairs.OnValidate();
            Update();
            var slopedNearTheMiddle = 0;
            foreach (var renderable in model.generated.renderables)
            {
                if (renderable == null || renderable.sharedMesh == null)
                    continue;
                var v = renderable.sharedMesh.vertices;
                var t = renderable.sharedMesh.triangles;
                for (int i = 0; i < t.Length; i += 3)
                {
                    var a = v[t[i]]; var b = v[t[i + 1]]; var c = v[t[i + 2]];
                    var normal = Vector3.Cross(b - a, c - a).normalized;
                    var middle = (a + b + c) / 3;
                    var radius = new Vector2(middle.x, middle.z).magnitude;
                    if (normal.y < -0.1f && normal.y > -0.9f && radius < 0.35f)
                        slopedNearTheMiddle++;
                }
            }
            Assert.That(slopedNearTheMiddle, Is.GreaterThan(0), "no sloped underside near the middle");
        }

        // The pieces of a sloped underside meet without a seam in their texture: where they share a corner, away from the
        // middle where every angle meets, they have the same uv there
        [Test]
        public void SpiralStairsWithASlopedUnderside_HaveContinuousUVs()
        {
            var stairs = Generator<ChiselSpiralStairsComponent>(out var model);
            stairs.definition.settings.riserType = StairsRiserType.Smooth;
            stairs.OnValidate();
            Update();
            var seen  = new Dictionary<Vector3Int, Vector2>();
            var seams = 0;
            var shared = 0;
            foreach (var renderable in model.generated.renderables)
            {
                if (renderable == null || renderable.sharedMesh == null)
                    continue;
                var v  = renderable.sharedMesh.vertices;
                var uv = renderable.sharedMesh.uv;
                var t  = renderable.sharedMesh.triangles;
                for (int i = 0; i < t.Length; i += 3)
                {
                    var normal = Vector3.Cross(v[t[i + 1]] - v[t[i]], v[t[i + 2]] - v[t[i]]).normalized;
                    if (normal.y > -0.2f || normal.y < -0.95f)
                        continue;   // the slope only
                    for (int c = 0; c < 3; c++)
                    {
                        var p = v[t[i + c]];
                        if (Around(p) < 0.01f)
                            continue;
                        var key = Vector3Int.RoundToInt(p * 1000);
                        if (seen.TryGetValue(key, out var other))
                        {
                            shared++;
                            if ((other - uv[t[i + c]]).magnitude > 1e-3f)
                                seams++;
                        } else
                            seen[key] = uv[t[i + c]];
                    }
                }
            }
            Assert.That(shared, Is.GreaterThan(0), "no shared corners on the slope");
            Assert.That(seams, Is.Zero, $"corners with a different uv on each side, of {shared}");
        }

        // The texture on a side wall isn't mirrored: seen from the side a face faces, u runs to the right
        [Test]
        public void SpiralStairsWalls_AreNotMirrored([Values(StairsSideType.Up, StairsSideType.Down)] StairsSideType side)
        {
            var stairs = WideSpiralStairs(out var model);
            stairs.definition.settings.outerSide = side;
            stairs.definition.settings.innerSide = side;
            stairs.OnValidate();
            Update();
            int checkedFaces = 0, mirrored = 0;
            foreach (var renderable in model.generated.renderables)
            {
                if (renderable == null || renderable.sharedMesh == null)
                    continue;
                var v  = renderable.sharedMesh.vertices;
                var uv = renderable.sharedMesh.uv;
                var t  = renderable.sharedMesh.triangles;
                for (int i = 0; i < t.Length; i += 3)
                {
                    Vector3 a = v[t[i]], b = v[t[i + 1]], c = v[t[i + 2]];
                    var normal = Vector3.Cross(b - a, c - a).normalized;
                    var middle = (a + b + c) / 3;
                    var radial = new Vector3(middle.x, 0, middle.z).normalized;
                    // the faces of the walls away from the steps: outside the outer wall, inside the inner one (the steps'
                    // own ends are mapped as the stairs' surfaces are, not as the walls')
                    if (Mathf.Abs(normal.y) > 0.1f || Mathf.Abs(Vector3.Dot(normal, radial)) < 0.9f)
                        continue;
                    if (Around(middle) < 1.55f && Around(middle) > 0.4f)
                        continue;
                    // to the right seen from in front, in a left handed space
                    var right = Vector3.Cross(normal, Vector3.up);
                    float s1 = Vector3.Dot(b - a, right), h1 = b.y - a.y;
                    float s2 = Vector3.Dot(c - a, right), h2 = c.y - a.y;
                    var det = s1 * h2 - s2 * h1;
                    if (Mathf.Abs(det) < 1e-6f)
                        continue;
                    float du1 = uv[t[i + 1]].x - uv[t[i]].x, du2 = uv[t[i + 2]].x - uv[t[i]].x;
                    var duAlongRight = (du1 * h2 - du2 * h1) / det;
                    checkedFaces++;
                    if (duAlongRight < 0)
                        mirrored++;
                }
            }
            Assert.That(checkedFaces, Is.GreaterThan(0), "no wall faces");
            Assert.That(mirrored, Is.Zero, $"mirrored faces, of {checkedFaces}");
        }

        // Every step's texture starts at the front of its step: on each step's top, u runs along its front, and where the
        // front starts (in the middle) the uv is the same on every step
        [Test]
        public void SpiralStairsTreads_StartTheirTextureAtTheirFront()
        {
            var stairs = WideSpiralStairs(out var model);
            stairs.definition.settings.nosingDepth = 0;
            stairs.definition.settings.nosingWidth = 0;
            stairs.OnValidate();
            Update();
            var faces = 0;
            var middles    = new List<Vector2>();
            var directions = new List<Vector2>();
            foreach (var renderable in model.generated.renderables)
            {
                if (renderable == null || renderable.sharedMesh == null)
                    continue;
                var v  = renderable.sharedMesh.vertices;
                var uv = renderable.sharedMesh.uv;
                var t  = renderable.sharedMesh.triangles;
                for (int i = 0; i < t.Length; i += 3)
                {
                    var normal = Vector3.Cross(v[t[i + 1]] - v[t[i]], v[t[i + 2]] - v[t[i]]).normalized;
                    if (normal.y < 0.9f || v[t[i]].y < 0.01f)
                        continue;   // the tops of the treads
                    // the flat mapping from (x, z) to uv, turned around to find where uv is 0, 0
                    Vector2 p0 = new Vector2(v[t[i]].x, v[t[i]].z), p1 = new Vector2(v[t[i + 1]].x, v[t[i + 1]].z), p2 = new Vector2(v[t[i + 2]].x, v[t[i + 2]].z);
                    Vector2 q0 = uv[t[i]], q1 = uv[t[i + 1]], q2 = uv[t[i + 2]];
                    Vector2 e1 = q1 - q0, e2 = q2 - q0;
                    var det = e1.x * e2.y - e1.y * e2.x;
                    if (Mathf.Abs(det) < 1e-8f)
                        continue;
                    // where a uv lands, from its barycentric weights in uv space
                    Vector2 At(Vector2 q)
                    {
                        var d  = q - q0;
                        var w1 = (d.x * e2.y - d.y * e2.x) / det;
                        var w2 = (d.y * e1.x - d.x * e1.y) / det;
                        return p0 + (p1 - p0) * w1 + (p2 - p0) * w2;
                    }
                    var alongU = (At(Vector2.right) - At(Vector2.zero)).normalized;
                    // the uv at the middle, where every step's front starts
                    Vector2 d1 = p1 - p0, d2 = p2 - p0;
                    var pdet = d1.x * d2.y - d1.y * d2.x;
                    var c  = -p0;
                    var b1 = (c.x * d2.y - c.y * d2.x) / pdet;
                    var b2 = (c.y * d1.x - c.x * d1.y) / pdet;
                    var atMiddle = q0 + e1 * b1 + e2 * b2;
                    faces++;
                    middles.Add(atMiddle);
                    directions.Add(alongU);
                }
            }
            Assert.That(faces, Is.GreaterThan(1), "no treads");
            // every step's texture starts at the same place on it: the same uv where its front starts
            foreach (var m in middles)
                Assert.That((m - middles[0]).magnitude, Is.LessThan(1e-3f), "a step's texture starts somewhere else");
            // and runs along its own front, which turns from step to step
            var turned = false;
            foreach (var d in directions)
                turned |= Vector2.Dot(d, directions[0]) < 0.95f;
            Assert.That(turned, Is.True, "every step has its texture the same way, not along its front");
        }

        // A wall around an open middle isn't split by the steps beside it: its face towards the middle has corners only
        // where the wall's own pieces meet, along its top and bottom
        [Test]
        public void SpiralStairsInnerWall_IsNotSplitByTheSteps([Values(StairsSideType.Up, StairsSideType.Down, StairsSideType.DownAndUp)] StairsSideType side,
                                                               [Values(StairsRiserType.ThickRiser, StairsRiserType.Smooth)] StairsRiserType riser,
                                                               [Values(false, true)] bool fewInnerSides)
        {
            var stairs = WideSpiralStairs(out var model);
            stairs.definition.settings.innerSide = side;
            stairs.definition.settings.riserType = riser;
            if (fewInnerSides)
            {
                // big stairs around a coarse middle: its straight sides are far from a circle at their corners
                ref var big = ref stairs.definition.settings;
                big.outerDiameter = 12;
                big.innerDiameter = 4;
                big.innerSegments = 8;
                big.outerSegments = 32;
                big.height        = 6.2f;
                big.rotation      = 255;
                big.sideWidth     = 0.2f;
                big.treadHeight   = 0.08f;
                big.nosingDepth   = 0.02f;
                big.nosingWidth   = 0.01f;
                big.stepHeight    = 0.2f;
                big.riserDepth    = 0.03f;
                big.origin.y      = 0.6f;
                big.outerSide     = StairsSideType.DownAndUp;
            }
            stairs.OnValidate();
            Update();
            var settings     = stairs.definition.settings;
            var innerApothem = settings.innerDiameter * 0.5f * Mathf.Cos(Mathf.PI / settings.innerSegments);
            // between the wall's face towards the middle and where the steps start, halfway into the wall
            var faceDistance = innerApothem - settings.sideWidth * 0.75f;
            var corners = new HashSet<Vector3Int>();
            var triangles = 0;
            foreach (var renderable in model.generated.renderables)
            {
                if (renderable == null || renderable.sharedMesh == null)
                    continue;
                var v = renderable.sharedMesh.vertices;
                var t = renderable.sharedMesh.triangles;
                for (int i = 0; i < t.Length; i += 3)
                {
                    var middle = (v[t[i]] + v[t[i + 1]] + v[t[i + 2]]) / 3;
                    var normal = Vector3.Cross(v[t[i + 1]] - v[t[i]], v[t[i + 2]] - v[t[i]]).normalized;
                    // facing the middle, at the wall's inside: its plane closer to the middle than where the steps start
                    if (Mathf.Abs(normal.y) > 0.1f)
                        continue;
                    var towardsMiddle = -(middle.x * normal.x + middle.z * normal.z);
                    if (towardsMiddle < innerApothem - settings.sideWidth * 1.25f || towardsMiddle > faceDistance)
                        continue;
                    triangles++;
                    for (int c = 0; c < 3; c++)
                        corners.Add(Vector3Int.RoundToInt(v[t[i + c]] * 1000));
                }
            }
            Assert.That(triangles, Is.GreaterThan(0), "no inner wall");
            // each piece of the wall shows one face here, with about two corners of its own; split by the steps it had half
            // as many again
            var pieces = settings.InnerSideSubMeshCount / 2;
            Assert.That(corners.Count, Is.LessThanOrEqualTo(pieces * 2.5f + 10), $"the wall's face is split ({triangles} triangles, {pieces} pieces)");
        }

        // A wall is one piece along each straight side of its cylinder, not one per step, so each side shows one face
        [Test]
        public void SpiralStairsWalls_HaveOnePiecePerSideOfTheirCylinder()
        {
            var stairs = Generator<ChiselSpiralStairsComponent>(out _);
            ref var settings = ref stairs.definition.settings;
            settings.outerDiameter = 12;
            settings.innerDiameter = 4;
            settings.innerSegments = 8;
            settings.outerSegments = 32;
            settings.height        = 6.2f;
            settings.stepHeight    = 0.2f;
            settings.rotation      = 255;
            settings.innerSide     = StairsSideType.DownAndUp;
            settings.outerSide     = StairsSideType.DownAndUp;
            stairs.OnValidate();
            Update();
            settings = ref stairs.definition.settings;
            // the sides it passes, plus where it starts part way along one and where it reaches the floor
            var innerSides = Mathf.CeilToInt(settings.rotation / (360f / settings.innerSegments)) + 2;
            var outerSides = Mathf.CeilToInt(settings.rotation / (360f / settings.outerSegments)) + 2;
            Assert.That(settings.InnerSideSubMeshCount / 2, Is.LessThanOrEqualTo(innerSides), "inner wall pieces");
            Assert.That(settings.OuterSideSubMeshCount / 2, Is.LessThanOrEqualTo(outerSides), "outer wall pieces");
        }

        // Spiral stairs solid down to the floor have their walls go down to the floor as well, whichever way they're set
        [Test]
        public void SpiralStairsFilledDown_HaveWallsDownToTheFloor([Values(StairsSideType.Up, StairsSideType.Down)] StairsSideType side)
        {
            var stairs = WideSpiralStairs(out var model);
            stairs.definition.settings.riserType = StairsRiserType.FillDown;
            stairs.definition.settings.outerSide = side;
            stairs.OnValidate();
            Update();
            var outerApothem = stairs.definition.settings.outerDiameter * 0.5f * Mathf.Cos(Mathf.PI / stairs.definition.settings.outerSegments);
            // on the outer cylinder there is only the wall: every place along it has its lowest corner on the floor
            var faceDistance = outerApothem - 0.01f;
            var lowestAt = new Dictionary<Vector2Int, float>();
            foreach (var renderable in model.generated.renderables)
            {
                if (renderable == null || renderable.sharedMesh == null)
                    continue;
                var v = renderable.sharedMesh.vertices;
                var t = renderable.sharedMesh.triangles;
                for (int i = 0; i < t.Length; i += 3)
                {
                    Vector3 a = v[t[i]], b = v[t[i + 1]], c = v[t[i + 2]];
                    var normal = Vector3.Cross(b - a, c - a).normalized;
                    // the outer wall's face looking out
                    if (Mathf.Abs(normal.y) > 0.1f || (a.x * normal.x + a.z * normal.z) < faceDistance)
                        continue;
                    for (int k = 0; k < 3; k++)
                    {
                        var p = v[t[i + k]];
                        var key = new Vector2Int(Mathf.RoundToInt(p.x * 1000), Mathf.RoundToInt(p.z * 1000));
                        lowestAt[key] = lowestAt.TryGetValue(key, out var low) ? Mathf.Min(low, p.y) : p.y;
                    }
                }
            }
            Assert.That(lowestAt.Count, Is.GreaterThan(0), "no outer wall");
            var hanging = 0;
            var where   = "";
            foreach (var pair in lowestAt)
            {
                if (pair.Value <= 1e-3f)
                    continue;
                hanging++;
                where += $" ({pair.Key.x / 1000f:F3}, {pair.Value:F3}, {pair.Key.y / 1000f:F3})";
            }
            Assert.That(hanging, Is.Zero, $"places along the wall that don't reach the floor, of {lowestAt.Count}:{where}");
        }

        // A sloped underside is built for each step only: one step too many wrote past its own pieces, over the cylinders
        // and treads after them (leaking what was written there) or past the end when nothing comes after
        [Test]
        public void SpiralStairsWithASlopedUnderside_BuildOnlyTheirOwnSteps()
        {
            var stairs = Generator<ChiselSpiralStairsComponent>(out var model);
            stairs.definition.settings.riserType     = StairsRiserType.Smooth;
            stairs.definition.settings.innerDiameter = 0;
            stairs.definition.settings.nosingDepth   = 0;
            stairs.OnValidate();
            Update();
            // built without writing past its pieces (which threw), and drawn
            var triangles = 0;
            foreach (var renderable in model.generated.renderables)
            {
                if (renderable != null && renderable.sharedMesh != null)
                    triangles += renderable.sharedMesh.triangles.Length;
            }
            Assert.That(triangles, Is.GreaterThan(0), "nothing drawn");
        }

        // Walls that go down beside the steps cover their sides all the way: beside a thick top floor down to its
        // underside, and beside a base down to the floor. The steps are narrowed by the walls, so any side of theirs that
        // faces out at that line is left showing. The wall under the steps only dropped along their slope from the tread
        [Test]
        public void LinearStairsWallsGoingDown_CoverTheTopFloorAndTheBase([Values(StairsRiserType.ThickRiser, StairsRiserType.Smooth)] StairsRiserType riser)
        {
            var stairs = Generator<ChiselLinearStairsComponent>(out var model);
            ref var settings = ref stairs.definition.settings;
            settings.bounds        = new MinMaxAABB { Min = new float3(0, 0, 0), Max = new float3(2, 2, 3) };
            settings.riserType     = riser;
            settings.plateauHeight = 0.6f;
            settings.stepHeight    = 0.2f;
            settings.stepDepth     = 0.2f;
            settings.leftSide      = StairsSideType.DownAndUp;
            settings.rightSide     = StairsSideType.DownAndUp;
            stairs.OnValidate();
            Update();

            var sideWidth = stairs.definition.settings.sideWidth;
            var showing = new List<Vector3>();
            foreach (var renderable in model.generated.renderables)
            {
                if (renderable == null || renderable.sharedMesh == null)
                    continue;
                var v = renderable.sharedMesh.vertices;
                var t = renderable.sharedMesh.triangles;
                for (int i = 0; i < t.Length; i += 3)
                {
                    var a = v[t[i]]; var b = v[t[i + 1]]; var c = v[t[i + 2]];
                    var normal = Vector3.Cross(b - a, c - a).normalized;
                    var middle = (a + b + c) / 3;
                    // the treads' noses reach out past the front, the walls included
                    if (middle.z > 3)
                        continue;
                    if ((normal.x < -0.99f && Mathf.Abs(middle.x - sideWidth) < 1e-3f) ||
                        (normal.x >  0.99f && Mathf.Abs(middle.x - (2 - sideWidth)) < 1e-3f))
                        showing.Add(middle);
                }
            }
            Assert.That(showing, Is.Empty, "sides of the steps the walls don't cover: " + string.Join(" ", showing.ConvertAll(p => $"({p.x:0.##},{p.y:0.##},{p.z:0.##})")));
        }

        // A wall's outline can have two corners in the same place, at the default size: a side between them has no
        // length, and its plane no direction, so the CSG left the whole wall out
        [Test]
        public void LinearStairsWalls_HaveNoPlaneWithoutADirection([Values] StairsRiserType riser,
                                                                   [Values(StairsSideType.Up, StairsSideType.DownAndUp)] StairsSideType side)
        {
            var stairs = Generator<ChiselLinearStairsComponent>(out _);
            stairs.definition.settings.riserType = riser;
            stairs.definition.settings.leftSide  = side;
            stairs.definition.settings.rightSide = side;
            stairs.OnValidate();
            Update();

            var branch = (CSGTreeBranch)stairs.TopTreeNode;
            for (int i = 0; i < branch.Count; i++)
            {
                var blob = BrushMeshManager.GetBrushMeshBlob(((CSGTreeBrush)branch[i]).BrushMesh.BrushMeshID);
                if (!blob.IsCreated)
                    continue;
                ref var mesh = ref blob.Value;
                for (int p = 0; p < mesh.localPlanes.Length; p++)
                {
                    var plane = mesh.localPlanes[p];
                    Assert.That(math.all(math.isfinite(plane)) && math.lengthsq(plane.xyz) > 0.5f, Is.True, $"brush {i} plane {p}: {plane}");
                }
            }
        }

        // Pathed stairs go along their path: on a path that turns a corner both legs are built. The stairs were moved
        // onto the path but not their planes, so every leg was built where the first one was
        [Test]
        public void PathedStairs_FollowAPathThatTurns()
        {
            var stairs = Generator<ChiselPathedStairsComponent>(out var model);
            stairs.definition.shape = new Curve2D(false, new CurveControlPoint2D(-2, -2), new CurveControlPoint2D(2, -2), new CurveControlPoint2D(2, 2));
            stairs.OnValidate();
            Update();

            var vertices = DrawnVertices(model, out _);
            Assert.That(vertices, Is.Not.Empty, "nothing drawn");
            var drawn = new Bounds(vertices[0], Vector3.zero);
            foreach (var vertex in vertices)
                drawn.Encapsulate(vertex);
            // the path is their outer edge: the leg along x from x -2 to 2 at z -2, the one along z from z -2 to 2 at x 2
            Assert.That(drawn.min.x, Is.EqualTo(-2).Within(0.1f), $"drawn {drawn}: not out to where the path starts");
            Assert.That(drawn.max.x, Is.EqualTo( 2).Within(0.1f), $"drawn {drawn}: not out to the path's corner");
            Assert.That(drawn.min.z, Is.EqualTo(-2).Within(0.1f), $"drawn {drawn}: not out to the leg along x");
            Assert.That(drawn.max.z, Is.EqualTo( 2).Within(0.1f), $"drawn {drawn}: not out to where the path ends");
            AssertClosed(model, "pathed stairs");
        }

        // At a corner the two parts of the path meet along its diagonal: nothing of either reaches past it, and they
        // leave no gap. Their ends were cut back by the wrong amounts, which left a sawtooth of gaps
        [Test]
        public void PathedStairs_MeetAtCorners()
        {
            var stairs = Generator<ChiselPathedStairsComponent>(out var model);
            stairs.definition.shape = new Curve2D(false, new CurveControlPoint2D(-2, -2), new CurveControlPoint2D(2, -2), new CurveControlPoint2D(2, 2));
            stairs.OnValidate();
            Update();

            // the corner is at (2, -2) and the stairs go inward from the path, so its diagonal is x + z = 0, from (2, -2)
            // in to (2 - depth, -2 + depth). Both parts are cut along it, so the corners of their steps there are on it,
            // and nothing drawn in the corner's square reaches past it on the wrong side of either part
            var vertices = DrawnVertices(model, out _);
            Assert.That(vertices, Is.Not.Empty, "nothing drawn");
            var depth = (float)(stairs.definition.settings.stairs.BoundsMax.z - stairs.definition.settings.stairs.BoundsMin.z);
            int onDiagonal = 0;
            foreach (var vertex in vertices)
            {
                if (vertex.x < 2 - depth - 0.01f || vertex.z > -2 + depth + 0.01f)
                    continue;
                if (math.abs(vertex.x + vertex.z) < 0.01f)
                    onDiagonal++;
            }
            var steps = Mathf.CeilToInt((float)(stairs.definition.settings.stairs.BoundsMax.y - stairs.definition.settings.stairs.BoundsMin.y) /
                                        stairs.definition.settings.stairs.stepHeight);
            Assert.That(onDiagonal, Is.GreaterThanOrEqualTo(steps * 2), $"{onDiagonal} corners on the diagonal for {steps} steps: the parts don't meet along it");
        }

        // The two parts at a corner are cut by one shared plane, so whatever the risers they join without a seam. Each
        // was cut by a plane made from its own vertices; the two weren't quite the same and both faces were kept
        [Test]
        public void PathedStairs_AreClosedAtCorners_WhateverTheRisers()
        {
            foreach (StairsRiserType riser in Enum.GetValues(typeof(StairsRiserType)))
            {
                var stairs = Generator<ChiselPathedStairsComponent>(out var model);
                stairs.definition.shape = new Curve2D(false, new CurveControlPoint2D(-2, -2), new CurveControlPoint2D(2, -2), new CurveControlPoint2D(2, 2));
                stairs.definition.settings.stairs.riserType = riser;
                stairs.OnValidate();
                Update();
                AssertClosed(model, $"pathed stairs with {riser} risers");
            }
        }

        // A path that isn't square, with stairs deeper than some of its parts are long, and the same path the other
        // way round, so it turns away from the stairs
        [TestCase(false)]
        [TestCase(true)]
        public void PathedStairs_AreClosed_OnAnUnevenPath(bool reversed)
        {
            var points = new[] { new CurveControlPoint2D(2, -18), new CurveControlPoint2D(7, -7), new CurveControlPoint2D(6, 5), new CurveControlPoint2D(-3, 6) };
            if (reversed)
                Array.Reverse(points);
            var stairs = Generator<ChiselPathedStairsComponent>(out var model);
            stairs.definition.shape = new Curve2D(false, points);
            stairs.definition.settings.stairs.bounds = new MinMaxAABB { Min = new float3(-5), Max = new float3(5) };
            stairs.definition.settings.stairs.plateauHeight = 4.6f;
            stairs.OnValidate();
            Update();
            AssertClosed(model, reversed ? "pathed stairs turning away" : "pathed stairs");
        }

        // An open path's ends have their side walls, which rise above the steps. Each end was cut by moving every
        // vertex near it onto its plane, which squashed the walls, a wall's width in from the end, to nothing
        [Test]
        public void PathedStairs_HaveSideWallsAtOpenEnds()
        {
            var stairs = Generator<ChiselPathedStairsComponent>(out var model);
            stairs.definition.shape = new Curve2D(false, new CurveControlPoint2D(-2, -2), new CurveControlPoint2D(2, -2), new CurveControlPoint2D(2, 2));
            ref var settings = ref stairs.definition.settings.stairs;
            settings.leftSide  = StairsSideType.Up;
            settings.rightSide = StairsSideType.Up;
            stairs.OnValidate();
            Update();

            var vertices = DrawnVertices(model, out _);
            Assert.That(vertices, Is.Not.Empty, "nothing drawn");
            var top = float.NegativeInfinity;
            foreach (var vertex in vertices)
                top = Mathf.Max(top, vertex.y);
            var stairsTop = (float)stairs.definition.settings.stairs.BoundsMax.y;
            Assert.That(top, Is.GreaterThan(stairsTop + 0.1f), "no side walls above the steps");
            AssertClosed(model, "pathed stairs with side walls");
        }

        // Where two parts of the path meet there's a side wall of its own, on the plane they share
        [Test]
        public void PathedStairs_HaveSideWallsAtCorners()
        {
            var stairs = Generator<ChiselPathedStairsComponent>(out var model);
            stairs.definition.shape = new Curve2D(false, new CurveControlPoint2D(-2, -2), new CurveControlPoint2D(2, -2), new CurveControlPoint2D(2, 2));
            stairs.definition.settings.cornerSide = StairsSideType.Up;
            stairs.OnValidate();
            Update();

            var vertices = DrawnVertices(model, out _);
            Assert.That(vertices, Is.Not.Empty, "nothing drawn");
            var top = float.NegativeInfinity;
            foreach (var vertex in vertices)
                top = Mathf.Max(top, vertex.y);
            var stairsTop = (float)stairs.definition.settings.stairs.BoundsMax.y;
            Assert.That(top, Is.GreaterThan(stairsTop + 0.1f), "no side wall above the steps at the corner");

            // the corner's plane is x + z = 0: the wall above the steps is on both sides of it, half on each
            float before = 0, after = 0;
            foreach (var vertex in vertices)
            {
                if (vertex.y < stairsTop + 0.1f)
                    continue;
                before = Mathf.Min(before, vertex.x + vertex.z);
                after  = Mathf.Max(after,  vertex.x + vertex.z);
            }
            Assert.That(after + before, Is.EqualTo(0).Within(1e-3f), $"not centred on the corner: from {before} to {after}");
            Assert.That(after, Is.GreaterThan(0.01f), "the wall is only on one side of the corner");

            // one wall square to the plane, not two halves each leaning along their own part
            var planeNormal = new Vector3(1, 0, 1).normalized;
            var sides = 0;
            foreach (var triangle in DrawnTriangles(model))
            {
                var middle = (triangle.a + triangle.b + triangle.c) / 3;
                // within the wall where it's above all the steps, not the steps beside or under it
                if (middle.y < top - (stairs.definition.settings.stairs.sideHeight * 0.5f) || Mathf.Abs(middle.x + middle.z) > 0.15f)
                    continue;
                var cross  = Vector3.Cross(triangle.b - triangle.a, triangle.c - triangle.a);
                // a triangle with all its corners on a line has no side it faces
                if (cross.magnitude < 1e-4f)
                    continue;
                var normal = cross.normalized;
                var facing = Mathf.Abs(Vector3.Dot(normal, planeNormal));
                if (Mathf.Abs(normal.y) > 0.1f || facing < 0.5f)
                    continue;
                sides++;
                Assert.That(facing, Is.GreaterThan(0.999f), $"a side of the wall at {middle} leans along its part, facing {normal}");
            }
            Assert.That(sides, Is.GreaterThan(0), "no sides to the wall at the corner");
            AssertClosed(model, "pathed stairs with a wall at the corner");
        }

        // A curved span is cut into many short parts; only its control points are corners with a wall, not the
        // points inside the curve
        [Test]
        public void PathedStairs_HaveNoCornerWallsInsideACurve()
        {
            float TopOf(StairsSideType cornerSide)
            {
                var stairs = Generator<ChiselPathedStairsComponent>(out var model);
                var start = new CurveControlPoint2D(-2, -2) { tangent2 = new Vector2(-2, 0), constraint2 = ControlPointConstraint.Mirrored };
                var end   = new CurveControlPoint2D( 2,  2) { tangent1 = new Vector2(0, 2),  constraint1 = ControlPointConstraint.Mirrored };
                stairs.definition.shape = new Curve2D(false, start, end);
                stairs.definition.settings.cornerSide = cornerSide;
                stairs.definition.settings.stairs.leftSide  = StairsSideType.None;
                stairs.definition.settings.stairs.rightSide = StairsSideType.None;
                stairs.OnValidate();
                Update();

                var vertices = DrawnVertices(model, out _);
                Assert.That(vertices, Is.Not.Empty, "nothing drawn");
                var top = float.NegativeInfinity;
                foreach (var vertex in vertices)
                    top = Mathf.Max(top, vertex.y);
                AssertClosed(model, $"pathed stairs along a curve, corner side {cornerSide}");
                return top;
            }
            Assert.That(TopOf(StairsSideType.Up), Is.EqualTo(TopOf(StairsSideType.None)).Within(1e-3f), "a corner wall inside the curve");
        }

        // Where the two parts' fronts meet in a point sticking out, that point is cut off under the corner's wall, which
        // is in front of everything within its width
        [TestCase(-6), TestCase(2)]
        public void PathedStairs_AreCutOffUnderTheirCornerWall_WhereTheirFrontsMeetInAPoint(float endZ)
        {
            var stairs = Generator<ChiselPathedStairsComponent>(out var model);
            stairs.definition.shape = new Curve2D(false, new CurveControlPoint2D(-2, -2), new CurveControlPoint2D(2, -2), new CurveControlPoint2D(2, endZ));
            stairs.definition.settings.cornerSide = StairsSideType.Up;
            stairs.OnValidate();
            Update();

            var vertices = DrawnVertices(model, out _);
            Assert.That(vertices, Is.Not.Empty, "nothing drawn");
            // the corner's plane goes through (2, -2) halfway between the two parts; along it, towards the corner of
            // the path is forward
            var planeNormal = (new Vector3(1, 0, 0) + new Vector3(0, 0, Mathf.Sign(endZ + 2))).normalized;
            var along       = Vector3.Cross(Vector3.up, planeNormal);
            var middle      = Vector3.zero;
            foreach (var vertex in vertices)
                middle += vertex;
            middle /= vertices.Count;
            if (Vector3.Dot(new Vector3(2, 0, -2) - middle, along) < 0)
                along = -along;

            var halfWidth = stairs.definition.settings.stairs.sideWidth * 0.5f;
            float inside = float.NegativeInfinity, wallFront = float.NegativeInfinity;
            foreach (var vertex in vertices)
            {
                var fromPlane = Mathf.Abs(Vector3.Dot(vertex - new Vector3(2, 0, -2), planeNormal));
                var forward   = Vector3.Dot(vertex, along);
                if (fromPlane < halfWidth - 1e-3f)
                    inside = Mathf.Max(inside, forward);
                else if (fromPlane < halfWidth + 1e-3f)
                    wallFront = Mathf.Max(wallFront, forward);
            }
            Assert.That(inside, Is.LessThanOrEqualTo(wallFront + 1e-3f), "the steps stick out in front of the wall at the corner");
            AssertClosed(model, "pathed stairs cut off under the wall at a corner");
        }

        // The corner's wall reaches out past the steps' fronts as far as asked, when that's further than the noses
        [Test]
        public void PathedStairs_HaveTheirCornerWallReachOutAsFarAsAsked()
        {
            float FrontWith(float frontOffset)
            {
                var stairs = Generator<ChiselPathedStairsComponent>(out var model);
                stairs.definition.shape = new Curve2D(false, new CurveControlPoint2D(-2, -2), new CurveControlPoint2D(2, -2), new CurveControlPoint2D(2, -6));
                stairs.definition.settings.cornerSide = StairsSideType.Up;
                stairs.definition.settings.stairs.sideFrontOffset = frontOffset;
                stairs.OnValidate();
                Update();
                // the corner's plane is x - z = 4; along it, (-1, 0, -1) is towards the corner of the path
                var planeNormal = new Vector3(1, 0, -1).normalized;
                var along       = new Vector3(-1, 0, -1).normalized;
                var front       = float.NegativeInfinity;
                foreach (var vertex in DrawnVertices(model, out _))
                {
                    var fromPlane = Mathf.Abs(Vector3.Dot(vertex - new Vector3(2, 0, -2), planeNormal));
                    if (fromPlane < (stairs.definition.settings.stairs.sideWidth * 0.5f) + 1e-3f)
                        front = Mathf.Max(front, Vector3.Dot(vertex, along));
                }
                return front;
            }
            var nosesOnly = FrontWith(0);
            var further   = FrontWith(0.5f);
            Assert.That(further - nosesOnly, Is.GreaterThan(0.3f), $"the wall's front went from {nosesOnly} to {further}");
        }

        // Linear stairs' side walls reach out past the steps' fronts as far as asked
        [Test]
        public void LinearStairsSideWalls_ReachOutPastTheStepsAsFarAsAsked()
        {
            float FrontWith(float frontOffset)
            {
                var stairs = Generator<ChiselLinearStairsComponent>(out var model);
                ref var settings = ref stairs.definition.settings;
                settings.bounds          = new MinMaxAABB { Min = new float3(0, 0, 0), Max = new float3(2, 1, 2) };
                settings.leftSide        = StairsSideType.DownAndUp;
                settings.rightSide       = StairsSideType.DownAndUp;
                settings.sideFrontOffset = frontOffset;
                stairs.OnValidate();
                Update();
                var front = float.NegativeInfinity;
                foreach (var vertex in DrawnVertices(model, out _))
                    front = Mathf.Max(front, vertex.z);
                return front;
            }
            // the steps' fronts are at z 2, the treads' noses a little past them
            Assert.That(FrontWith(0.3f), Is.EqualTo(2.3f).Within(1e-3f), "the walls' front isn't that far past the steps'");
        }

        // Spiral stairs' side walls reach out round the stairs past the first step's front as far as asked
        [Test]
        public void SpiralStairsSideWalls_ReachOutPastTheStepsAsFarAsAsked()
        {
            float StartWith(float frontOffset)
            {
                var stairs = WideSpiralStairs(out var model);
                ref var settings = ref stairs.definition.settings;
                settings.rotation        = 90;
                settings.outerSide       = StairsSideType.Up;
                settings.innerSide       = StairsSideType.None;
                settings.sideFrontOffset = frontOffset;
                stairs.OnValidate();
                Update();
                // how far round from the first step's front the outer wall starts, outside the steps
                var start = settings.startAngle * Mathf.Deg2Rad;
                var first = float.PositiveInfinity;
                foreach (var vertex in DrawnVertices(model, out _))
                {
                    if (new Vector2(vertex.x, vertex.z).magnitude < 1.5f + 1e-3f)
                        continue;
                    var angle = Mathf.Atan2(vertex.x, vertex.z) - start;
                    angle = Mathf.Repeat(angle + Mathf.PI, Mathf.PI * 2) - Mathf.PI;
                    first = Mathf.Min(first, angle);
                }
                return first;
            }
            var none    = StartWith(0);
            var further = StartWith(0.3f);
            Assert.That(none, Is.GreaterThan(-1e-3f), "without an offset the wall starts before the first step");
            // 0.3 round the middle of the wall, a little more than 1.5 from the middle
            Assert.That(further, Is.LessThan(-0.1f), $"the wall starts {further} round from the first step");
        }

        // The side walls beside the floor at the top go back to the stairs' back, as far back from the top step as set, or
        // not at all
        [TestCase(StairsTopSideType.Full,   0.0f)]
        [TestCase(StairsTopSideType.Length, 0.7f)]
        [TestCase(StairsTopSideType.None,   float.PositiveInfinity)]
        public void LinearStairsSideWalls_GoBackBesideTheTopFloorAsSet(StairsTopSideType topSide, float back)
        {
            var stairs = Generator<ChiselLinearStairsComponent>(out var model);
            ref var settings = ref stairs.definition.settings;
            // 5 steps of 0.2 deep in front, a top floor 1 deep behind them
            settings.bounds        = new MinMaxAABB { Min = new float3(0, 0, 0), Max = new float3(2, 1, 2) };
            settings.leftSide      = StairsSideType.Up;
            settings.rightSide     = StairsSideType.Up;
            settings.topSide       = topSide;
            settings.topSideLength = 0.3f;
            stairs.OnValidate();
            Update();
            // the wall above the top floor, which is at the stairs' top
            var wallBack = float.PositiveInfinity;
            foreach (var vertex in DrawnVertices(model, out _))
            {
                if (vertex.y > 1.01f)
                    wallBack = Mathf.Min(wallBack, vertex.z);
            }
            if (float.IsPositiveInfinity(back))
                Assert.That(wallBack, Is.GreaterThan(0.99f), "a wall beside the top floor");
            else
                Assert.That(wallBack, Is.EqualTo(back).Within(1e-3f), "the wall beside the top floor goes back the wrong way");
        }

        // Spiral stairs raised above the object's position stand on it: their first step, walls and column reach down to it
        [TestCase(StairsRiserType.ThickRiser)]
        [TestCase(StairsRiserType.ThinRiser)]
        [TestCase(StairsRiserType.Smooth)]
        public void SpiralStairsRaisedAboveTheirPosition_StandOnIt(StairsRiserType riser)
        {
            var stairs = WideSpiralStairs(out var model);
            ref var settings = ref stairs.definition.settings;
            settings.origin    = new float3(0, 0.5f, 0);
            settings.riserType = riser;
            settings.outerSide = StairsSideType.DownAndUp;
            stairs.OnValidate();
            Update();
            float lowest = float.PositiveInfinity, lowestWall = float.PositiveInfinity;
            foreach (var vertex in DrawnVertices(model, out _))
            {
                lowest = Mathf.Min(lowest, vertex.y);
                // the wall on the outer cylinder, past where the steps end
                if (new Vector2(vertex.x, vertex.z).magnitude > 1.5f - 1e-3f)
                    lowestWall = Mathf.Min(lowestWall, vertex.y);
            }
            Assert.That(lowest, Is.EqualTo(0).Within(1e-3f), "the stairs don't reach down to their position");
            Assert.That(lowestWall, Is.EqualTo(0).Within(1e-3f), "the wall beside the first step doesn't reach down to their position");
            AssertClosed(model, "raised spiral stairs");
        }

        // Changing the height of pathed stairs does just what it does to linear stairs
        [Test]
        public void PathedStairsHeight_ChangesAsOnLinearStairs()
        {
            var linear = ChiselLinearStairs.DefaultSettings;
            linear.bounds        = new MinMaxAABB { Min = new float3(-1, 0, 0), Max = new float3(1, 1.4f, 4) };
            linear.stepHeight    = 0.2f;
            linear.stepDepth     = 0.2f;
            linear.plateauHeight = 0.4f;
            var pathed = ChiselPathedStairs.DefaultSettings;
            pathed.stairs = linear;

            foreach (var height in new[] { 1.4f, 1.2f, 1.5f, 0.8f, 1.05f, 2.0f })
            {
                linear.bounds.Max.y = height;
                pathed.stairs.bounds.Max.y = height;
                linear.Validate();
                pathed.Validate();
                Assert.That(pathed.stairs.plateauHeight, Is.EqualTo(linear.plateauHeight).Within(1e-5f), $"the base at {height}");
                Assert.That(pathed.stairs.StepCount, Is.EqualTo(linear.StepCount), $"the steps at {height}");
                Assert.That(pathed.stairs.AbsHeight, Is.EqualTo(linear.AbsHeight).Within(1e-5f), $"the height at {height}");
            }
        }

        // Pathed stairs with a sloped underside get no wall at an open path's start and end they weren't given
        [Test]
        public void PathedStairsWithSmoothRisers_HaveNoWallAtTheirEndsWithoutOne()
        {
            int BrushCount(StairsSideType ends)
            {
                var stairs = Generator<ChiselPathedStairsComponent>(out _);
                stairs.definition.shape = new Curve2D(false, new CurveControlPoint2D(-2, -2), new CurveControlPoint2D(2, -2), new CurveControlPoint2D(2, 2));
                stairs.definition.settings.stairs.riserType = StairsRiserType.Smooth;
                stairs.definition.settings.stairs.leftSide  = ends;
                stairs.definition.settings.stairs.rightSide = ends;
                stairs.OnValidate();
                Update();
                return ((CSGTreeBranch)stairs.TopTreeNode).Count;
            }
            Assert.That(BrushCount(StairsSideType.None), Is.LessThan(BrushCount(StairsSideType.Down)), "a wall at the ends that wasn't asked for");
        }

        // Pathed stairs' treads reach out past an open path's start and end without guard rails, but not at a corner
        [Test]
        public void PathedStairsTreads_ReachOutOnlyAtTheirOpenEnds()
        {
            float Reach(float nosingWidth)
            {
                var stairs = Generator<ChiselPathedStairsComponent>(out var model);
                stairs.definition.shape = new Curve2D(false, new CurveControlPoint2D(-2, -2), new CurveControlPoint2D(2, -2), new CurveControlPoint2D(2, 2));
                stairs.definition.settings.stairs.nosingWidth = nosingWidth;
                stairs.OnValidate();
                Update();
                // the path's start is at x -2: how far past it the stairs reach
                var reach = float.PositiveInfinity;
                foreach (var vertex in DrawnVertices(model, out _))
                    reach = Mathf.Min(reach, vertex.x);
                return -2 - reach;
            }
            Assert.That(Reach(0.1f) - Reach(0), Is.EqualTo(0.1f).Within(1e-3f), "the treads don't reach out past the start");
        }

        // Smooth linear stairs get a wall down on a side without one, which a corner of pathed stairs doesn't need
        [Test]
        public void PathedStairsWithSmoothRisers_HaveNoWallAtCornersWithoutOne()
        {
            int BrushCount(StairsSideType cornerSide)
            {
                var stairs = Generator<ChiselPathedStairsComponent>(out _);
                stairs.definition.shape = new Curve2D(false, new CurveControlPoint2D(-2, -2), new CurveControlPoint2D(2, -2), new CurveControlPoint2D(2, 2));
                stairs.definition.settings.stairs.riserType = StairsRiserType.Smooth;
                stairs.definition.settings.cornerSide = cornerSide;
                stairs.OnValidate();
                Update();
                return ((CSGTreeBranch)stairs.TopTreeNode).Count;
            }
            Assert.That(BrushCount(StairsSideType.None), Is.LessThan(BrushCount(StairsSideType.Down)), "a wall at the corner that wasn't asked for");
        }

        static List<Vector3> DrawnVertices(ChiselModelComponent model, out List<Vector3> triangleMiddles)
        {
            var vertices = new List<Vector3>();
            triangleMiddles = new List<Vector3>();
            foreach (var renderable in model.generated.renderables)
            {
                if (renderable == null || renderable.sharedMesh == null)
                    continue;
                var v = renderable.sharedMesh.vertices;
                var t = renderable.sharedMesh.triangles;
                vertices.AddRange(v);
                for (int i = 0; i < t.Length; i += 3)
                    triangleMiddles.Add((v[t[i]] + v[t[i + 1]] + v[t[i + 2]]) / 3);
            }
            return vertices;
        }

        static List<(Vector3 a, Vector3 b, Vector3 c)> DrawnTriangles(ChiselModelComponent model)
        {
            var triangles = new List<(Vector3 a, Vector3 b, Vector3 c)>();
            foreach (var renderable in model.generated.renderables)
            {
                if (renderable == null || renderable.sharedMesh == null)
                    continue;
                var v = renderable.sharedMesh.vertices;
                var t = renderable.sharedMesh.triangles;
                for (int i = 0; i < t.Length; i += 3)
                    triangles.Add((v[t[i]], v[t[i + 1]], v[t[i + 2]]));
            }
            return triangles;
        }

        static float Around(Vector3 point) { return new Vector2(point.x, point.z).magnitude; }

        ChiselSpiralStairsComponent WideSpiralStairs(out ChiselModelComponent model)
        {
            var stairs = Generator<ChiselSpiralStairsComponent>(out model);
            stairs.definition.settings.outerDiameter = 3;
            stairs.definition.settings.innerDiameter = 1;
            return stairs;
        }

        // Without treads the risers are trimmed by their own cylinder, as wide as the stairs. The treads' cylinders, wider
        // by the nose, were made anyway, in the risers' cylinders' places, which were left without being disposed
        [Test]
        public void SpiralStairsWithoutTreads_TrimTheirRisersWithTheirOwnCylinder()
        {
            var stairs = WideSpiralStairs(out _);
            stairs.definition.settings.nosingDepth = 0;   // no treads
            stairs.definition.settings.nosingWidth = 0.2f;
            stairs.definition.settings.riserType   = StairsRiserType.ThickRiser;
            stairs.OnValidate();
            Update();

            var branch = (CSGTreeBranch)stairs.TopTreeNode;
            var outer  = (CSGTreeBrush)branch[stairs.definition.settings.TreadStart - stairs.definition.settings.CylinderSubMeshCount];
            var blob   = BrushMeshManager.GetBrushMeshBlob(outer.BrushMesh.BrushMeshID);
            Assert.That(blob.IsCreated, Is.True, "no cylinder");
            var bounds = blob.Value.localBounds;
            Assert.That((float)(bounds.Max.x - bounds.Min.x), Is.EqualTo(3).Within(0.1f), "the risers' cylinder isn't as wide as the stairs");
        }

        // A wall along the outside of spiral stairs reaches out to the outer cylinder and no further
        [Test]
        public void SpiralStairsWithAnOuterWall_HaveItOnTheOuterCylinder()
        {
            var stairs = WideSpiralStairs(out var model);
            stairs.definition.settings.outerSide = StairsSideType.Up;
            stairs.definition.settings.sideWidth = 0.2f;
            stairs.OnValidate();
            Update();
            var furthest = 0f;
            foreach (var vertex in DrawnVertices(model, out _))
                furthest = Mathf.Max(furthest, Around(vertex));
            Assert.That(furthest, Is.EqualTo(1.5f).Within(0.01f), "the wall isn't on the outer cylinder");
        }

        // A solid column fills the middle; an open middle has nothing in it
        [Test]
        public void SpiralStairsWithASolidColumn_FillTheMiddle([Values] bool solid)
        {
            var stairs = WideSpiralStairs(out var model);
            stairs.definition.settings.centerType = solid ? SpiralStairsCenterType.Solid : SpiralStairsCenterType.Open;
            stairs.OnValidate();
            Update();
            DrawnVertices(model, out var middles);
            var inTheMiddle = middles.Exists(middle => Around(middle) < 0.3f);
            Assert.That(inTheMiddle, Is.EqualTo(solid));
        }

        // A hollow column has a wall as thick as set around an empty middle
        [Test]
        public void SpiralStairsWithAHollowColumn_HaveAWallAroundAnEmptyMiddle()
        {
            var stairs = WideSpiralStairs(out var model);
            stairs.definition.settings.centerType      = SpiralStairsCenterType.Hollow;
            stairs.definition.settings.columnThickness = 0.1f;
            stairs.OnValidate();
            Update();
            var vertices = DrawnVertices(model, out var middles);
            Assert.That(middles.Exists(middle => Around(middle) < 0.3f), Is.False, "the middle is filled");
            Assert.That(vertices.Exists(vertex => Mathf.Abs(Around(vertex) - 0.4f) < 0.02f), Is.True, "no inside of the wall");
        }

        // Spiral stairs saved before they had a column or sides read those thicknesses as 0: they get the defaults
        [Test]
        public void SpiralStairsSavedWithoutColumnOrSides_GetTheDefaultThicknesses()
        {
            var settings = ChiselSpiralStairs.DefaultSettings;
            settings.columnThickness = 0;
            settings.sideWidth       = 0;
            settings.innerDiameter   = 1;
            settings.Validate();
            Assert.That(settings.columnThickness, Is.EqualTo(ChiselSpiralStairs.DefaultSettings.columnThickness));
            Assert.That(settings.sideWidth,       Is.EqualTo(ChiselSpiralStairs.DefaultSettings.sideWidth));
        }

        // Spiral stairs only use the height of their origin: sideways the steps would leave the cylinders that trim them
        [Test]
        public void SpiralStairs_KeepOnlyTheHeightOfTheirOrigin()
        {
            var settings = ChiselSpiralStairs.DefaultSettings;
            settings.origin = new float3(1, 2, 3);
            settings.Validate();
            Assert.That(settings.origin, Is.EqualTo(new float3(0, 2, 0)));
        }

        // An inner wall stands beside a column in the middle, not inside it
        [Test]
        public void SpiralStairsWithAColumn_HaveTheirInnerWallBesideIt()
        {
            var stairs = WideSpiralStairs(out var model);
            stairs.definition.settings.centerType = SpiralStairsCenterType.Solid;
            stairs.definition.settings.innerSide  = StairsSideType.Up;
            stairs.definition.settings.sideWidth  = 0.2f;
            stairs.OnValidate();
            Update();
            // the wall's top reaches above the steps, between the column (0.5) and 0.5 plus its thickness
            var top = 0f;
            foreach (var vertex in DrawnVertices(model, out _))
            {
                if (Around(vertex) > 0.55f && Around(vertex) < 0.75f)
                    top = Mathf.Max(top, vertex.y);
            }
            Assert.That(top, Is.GreaterThan(stairs.definition.settings.height + 0.1f), "no wall beside the column");
        }

        // Spiral stairs with a column and walls are closed: no gaps, and no triangles without area, where they meet
        [Test]
        public void SpiralStairsWithAColumnAndWalls_AreClosed([Values] SpiralStairsCenterType center)
        {
            var stairs = WideSpiralStairs(out var model);
            stairs.definition.settings.centerType = center;
            stairs.definition.settings.innerSide  = StairsSideType.DownAndUp;
            stairs.definition.settings.outerSide  = StairsSideType.DownAndUp;
            stairs.OnValidate();
            Update();
            AssertClosed(model, center.ToString());
        }

        // An extruded shape whose path ends on its start is pushed out to the smallest length, and built
        [Test]
        public void AnExtrudedShapeOfNoLength_IsTheSmallest()
        {
            var shape = Generator<ChiselExtrudedShapeComponent>(out var model);
            var path  = shape.definition.path;
            var end   = path.segments[1];
            end.position = path.segments[0].position;
            path.segments[1] = end;
            shape.OnValidate();
            Update();
            var length = Vector3.Distance(shape.definition.path.segments[0].position, shape.definition.path.segments[1].position);
            Assert.That(length, Is.EqualTo(ChiselGeneratorLimits.kMinSize).Within(1e-6f));
            AssertClosed(model, "extruded shape");
        }

        // An extruded shape whose outline has no area, its points on one line, is no shape: nothing is built, without errors
        [Test]
        public void AnExtrudedShapeWithoutArea_BuildsNothing()
        {
            var shape = Generator<ChiselExtrudedShapeComponent>(out var model);
            shape.definition.shape = new Curve2D(new CurveControlPoint2D(0, 0), new CurveControlPoint2D(1, 0), new CurveControlPoint2D(2, 0));
            shape.OnValidate();
            Update();
            var drawn = 0;
            foreach (var renderable in model.generated.renderables)
                if (renderable != null && renderable.sharedMesh != null)
                    drawn += renderable.sharedMesh.triangles.Length;
            Assert.That(drawn, Is.Zero, "something was drawn");
        }

        // An extruded shape whose outline crosses itself, like a bow tie, is built as both its halves, each filled, closed
        [Test]
        public void AnExtrudedShapeThatCrossesItself_FillsBothHalves()
        {
            var shape = Generator<ChiselExtrudedShapeComponent>(out var model);
            shape.definition.shape = new Curve2D(new CurveControlPoint2D(0, 0), new CurveControlPoint2D(2, 2),
                                                 new CurveControlPoint2D(2, 0), new CurveControlPoint2D(0, 2));
            shape.OnValidate();
            Update();
            // the halves touch along the one edge through the crossing
            AssertClosed(model, "bow tie", pinches: 1);
            // a corner from each half: the outline's far ends on either side of the crossing
            var vertices = DrawnVertices(model, out _);
            var spread = new Bounds(vertices[0], Vector3.zero);
            foreach (var v in vertices) spread.Encapsulate(v);
            var sizes = new List<float> { spread.size.x, spread.size.y, spread.size.z };
            sizes.Sort();
            Assert.That(sizes[1], Is.EqualTo(2).Within(1e-3f), "a half is missing");
            Assert.That(sizes[2], Is.GreaterThanOrEqualTo(2 - 1e-3f), "a half is missing");
        }

        // Upside down, its rounded ends keep their heights (they were stuck at 0, leaving a cylinder)
        [Test]
        public void ACapsuleUpsideDown_KeepsItsRoundedEnds()
        {
            var capsule = Generator<ChiselCapsuleComponent>(out var model);
            ref var settings = ref capsule.definition.settings;
            settings.height = -2; settings.topHeight = 0.4f; settings.bottomHeight = 0.3f;
            capsule.OnValidate();
            Update();
            Assert.That(capsule.definition.settings.topHeight, Is.EqualTo(0.4f));
            Assert.That(capsule.definition.settings.bottomHeight, Is.EqualTo(0.3f));
            AssertClosed(model, "capsule");
        }

        // Shorter than its rounded ends, the ends shrink; the height stays where it was put, so the end across from the
        // one dragged doesn't move
        [Test]
        public void ACapsuleShorterThanItsEnds_KeepsItsHeight()
        {
            var capsule = Generator<ChiselCapsuleComponent>(out var model);
            ref var settings = ref capsule.definition.settings;
            settings.height = 0.5f; settings.topHeight = 0.5f; settings.bottomHeight = 0.5f;
            capsule.OnValidate();
            Update();
            Assert.That(capsule.definition.settings.height, Is.EqualTo(0.5f), "the height moved");
            Assert.That(capsule.definition.settings.topHeight + capsule.definition.settings.bottomHeight, Is.EqualTo(0.5f).Within(1e-6f));
            AssertClosed(model, "capsule");
        }

        // Each surface its own material, and the normals of the triangles drawn with each, by the surface's place
        static Dictionary<int, List<Vector3>> NormalsBySurface(ChiselGeneratorComponent generator, ChiselModelComponent model, List<Material> materials)
        {
            for (int i = 0; i < generator.SurfaceDefinition.surfaces.Length; i++)
            {
                var material = new Material(ChiselProjectSettings.DefaultWallMaterial) { name = "surface " + i };
                materials.Add(material);
                generator.SurfaceDefinition.surfaces[i].SetMaterial(material);
            }
            generator.OnValidate();
            Update();
            var normals = new Dictionary<int, List<Vector3>>();
            foreach (var renderable in model.generated.renderables)
            {
                if (renderable == null || renderable.sharedMesh == null || renderable.renderMaterials == null)
                    continue;
                var mesh = renderable.sharedMesh;
                var vertices = mesh.vertices;
                for (int sub = 0; sub < mesh.subMeshCount && sub < renderable.renderMaterials.Length; sub++)
                {
                    var surface = materials.IndexOf(renderable.renderMaterials[sub]);
                    if (surface < 0)
                        continue;
                    if (!normals.TryGetValue(surface, out var list))
                        normals[surface] = list = new List<Vector3>();
                    var triangles = mesh.GetTriangles(sub);
                    for (int t = 0; t + 2 < triangles.Length; t += 3)
                    {
                        var a = vertices[triangles[t]]; var b = vertices[triangles[t + 1]]; var c = vertices[triangles[t + 2]];
                        var normal = Vector3.Cross(b - a, c - a);
                        if (normal.sqrMagnitude > 1e-12f)
                            list.Add(normal.normalized);
                    }
                }
            }
            return normals;
        }

        static void AssertFacing(Dictionary<int, List<Vector3>> normals, StairsSurface surface, Func<Vector3, bool> facing, string how)
        {
            Assert.That(normals.ContainsKey((int)surface), Is.True, $"nothing drawn as {surface}");
            foreach (var normal in normals[(int)surface])
                Assert.That(facing(normal), Is.True, $"a {surface} triangle doesn't face {how}: {normal}");
        }

        // Its tops are treads, its fronts risers, each with its own default material (they were numbered by triangle,
        // so the treads had the risers' surface and the tread and riser materials were never used)
        [Test]
        public void LinearStairs_FacesHaveTheirNamedSurfaces()
        {
            var stairs = Generator<ChiselLinearStairsComponent>(out var model);
            Assert.That(stairs.surfaceArray.surfaces[(int)StairsSurface.TreadTop].RenderMaterial, Is.EqualTo(ChiselProjectSettings.DefaultTreadMaterial));
            Assert.That(stairs.surfaceArray.surfaces[(int)StairsSurface.RiserFront].RenderMaterial, Is.EqualTo(ChiselProjectSettings.DefaultRiserMaterial));
            Assert.That(stairs.surfaceArray.surfaces[(int)StairsSurface.StepNose].RenderMaterial.name, Is.EqualTo("StepNose"));
            Assert.That(stairs.surfaceArray.surfaces[(int)StairsSurface.StepSides].RenderMaterial.name, Is.EqualTo("StepSides"));
            var materials = new List<Material>();
            try
            {
                var normals = NormalsBySurface(stairs, model, materials);
                AssertFacing(normals, StairsSurface.TreadTop, n => n.y > 0.9f, "up");
                AssertFacing(normals, StairsSurface.RiserFront, n => Mathf.Abs(n.y) < 0.1f, "sideways");
                AssertFacing(normals, StairsSurface.StepSides, n => Mathf.Abs(n.x) > 0.9f, "to the side");
                AssertFacing(normals, StairsSurface.StepNose, n => n.z > 0.9f, "to the front");
                foreach (var normal in normals[(int)StairsSurface.RiserFront])
                    Assert.That(normal.y, Is.LessThan(0.1f));
            }
            finally { foreach (var m in materials) UnityEngine.Object.DestroyImmediate(m); }
        }

        [Test]
        public void SpiralStairs_FacesHaveTheirNamedSurfaces()
        {
            var stairs = Generator<ChiselSpiralStairsComponent>(out var model);
            Assert.That(stairs.surfaceArray.surfaces[(int)StairsSurface.RiserFront].RenderMaterial, Is.EqualTo(ChiselProjectSettings.DefaultRiserMaterial));
            var materials = new List<Material>();
            try
            {
                var normals = NormalsBySurface(stairs, model, materials);
                AssertFacing(normals, StairsSurface.TreadTop, n => n.y > 0.9f, "up");
                AssertFacing(normals, StairsSurface.RiserFront, n => Mathf.Abs(n.y) < 0.1f, "sideways");
            }
            finally { foreach (var m in materials) UnityEngine.Object.DestroyImmediate(m); }
        }

        // Solid to the floor, the face on the floor is the underside too (it had a surface of its own)
        [TestCase(false)]
        [TestCase(true)]
        public void StairsSolidToTheFloor_UseTheUndersideBelow(bool spiral)
        {
            ChiselGeneratorComponent stairs;
            ChiselModelComponent model;
            if (spiral)
            {
                var s = Generator<ChiselSpiralStairsComponent>(out model);
                s.definition.settings.riserType = StairsRiserType.FillDown;
                stairs = s;
            } else
            {
                var s = Generator<ChiselLinearStairsComponent>(out model);
                s.definition.settings.riserType = StairsRiserType.FillDown;
                stairs = s;
            }
            var materials = new List<Material>();
            try
            {
                var normals = NormalsBySurface(stairs, model, materials);
                AssertFacing(normals, StairsSurface.Underside, n => n.y < -0.9f, "down");
                foreach (var pair in normals)
                {
                    if (pair.Key == (int)StairsSurface.Underside || pair.Key == (int)StairsSurface.WallBottom || pair.Key == (int)StairsSurface.TreadBottom)
                        continue;
                    foreach (var normal in pair.Value)
                        Assert.That(normal.y, Is.GreaterThan(-0.9f), $"{(StairsSurface)pair.Key} faces down");
                }
            }
            finally { foreach (var m in materials) UnityEngine.Object.DestroyImmediate(m); }
        }

        // A typed depth of the flat top moves the back: the front and the steps stay where they are
        [Test]
        public void StairsTopDepth_MovesTheBack()
        {
            var stairs = ChiselLinearStairs.DefaultSettings;
            stairs.bounds     = new MinMaxAABB { Min = new float3(-1, 0, 0), Max = new float3(1, 1, 3) };
            stairs.stepHeight = 0.2f;
            stairs.stepDepth  = 0.2f;
            stairs.SetTopDepth(0.5f);
            Assert.That(stairs.StepDepthOffset, Is.EqualTo(0.5f).Within(1e-4f));
            Assert.That(stairs.bounds.Max.z, Is.EqualTo(3).Within(1e-4f));
            Assert.That(stairs.stepDepth, Is.EqualTo(0.2f).Within(1e-4f));
        }

        // The outer guard rail lies inside the outer cylinder; it was built outwards from it, and treads reached past it
        [Test]
        public void SpiralStairsOuterGuardRail_LiesInsideTheOuterCylinder()
        {
            var stairs = Generator<ChiselSpiralStairsComponent>(out var model);
            stairs.definition.settings.outerSide   = StairsSideType.DownAndUp;
            stairs.definition.settings.sideWidth   = 0.2f;
            stairs.definition.settings.nosingWidth = 0.1f;
            stairs.OnValidate();
            Update();
            var vertices = DrawnVertices(model, out _);
            Assert.That(vertices, Is.Not.Empty);
            var radius = stairs.definition.settings.outerDiameter * 0.5f;
            foreach (var v in vertices)
                Assert.That(new Vector2(v.x, v.z).magnitude, Is.LessThanOrEqualTo(radius + 1e-3f), $"{v} is outside the circle");
        }

        // The outer face of an outer guard rail is whole, one face per piece: steps reaching through it, even where they
        // were trimmed away, split it along their outlines
        [Test]
        public void SpiralStairsOuterGuardRail_IsNotCutByTheSteps()
        {
            var stairs = WideSpiralStairs(out var model);
            ref var settings = ref stairs.definition.settings;
            settings.outerSide = StairsSideType.DownAndUp;
            settings.sideWidth = 0.2f;
            stairs.OnValidate();
            Update();
            var apothem = settings.outerDiameter * 0.5f * Mathf.Cos(Mathf.PI / settings.outerSegments);
            foreach (var triangle in DrawnTriangles(model))
            {
                var normal = Vector3.Cross(triangle.b - triangle.a, triangle.c - triangle.a).normalized;
                if (Mathf.Abs(normal.y) > 0.1f || (triangle.a.x * normal.x + triangle.a.z * normal.z) < apothem - 0.01f)
                    continue;
                foreach (var corner in new[] { triangle.a, triangle.b, triangle.c })
                {
                    var angle = Mathf.Atan2(corner.x, corner.z);
                    var side  = Mathf.PI * 2 / settings.outerSegments;
                    var nearCorner = Mathf.Abs(new Vector2(corner.x, corner.z).magnitude - settings.outerDiameter * 0.5f) < 1e-2f;
                    var atAnEnd    = Mathf.Abs(Mathf.DeltaAngle(angle * Mathf.Rad2Deg, settings.startAngle)) < 0.1f ||
                                     Mathf.Abs(Mathf.DeltaAngle(angle * Mathf.Rad2Deg, settings.startAngle + settings.rotation)) < 0.1f;
                    // the wall has a piece end where it reaches the floor
                    var split      = settings.SideFloorAngle(settings.startAngle * Mathf.Deg2Rad, settings.AnglePerStep * Mathf.Deg2Rad);
                    var atItsSplit = Mathf.Abs(Mathf.DeltaAngle(angle * Mathf.Rad2Deg, split * Mathf.Rad2Deg)) < 0.1f;
                    Assert.That(nearCorner || atAnEnd || atItsSplit, Is.True, $"the rail's outer face is cut at {corner} (a side is {side} wide)");
                }
            }
        }

        // A step's top runs on from its front without a seam, on linear, pathed and spiral stairs
        [TestCase(0, StairsRiserType.ThickRiser)]
        [TestCase(0, StairsRiserType.ThinRiser)]
        [TestCase(1, StairsRiserType.ThickRiser)]
        [TestCase(2, StairsRiserType.ThickRiser)]
        [TestCase(2, StairsRiserType.ThinRiser)]
        public void StairsStepTops_RunOnFromTheirFronts(int kind, StairsRiserType riser)
        {
            ChiselModelComponent model;
            var perStep = 0f;
            if (kind == 0)      Generator<ChiselLinearStairsComponent>(out model).definition.settings.riserType = riser;
            else if (kind == 1)
            {
                // one straight part: where parts meet each has its own uv
                var pathed = Generator<ChiselPathedStairsComponent>(out model);
                pathed.definition.settings.stairs.riserType = riser;
                pathed.definition.shape = new Curve2D(false, new CurveControlPoint2D(-2, -2), new CurveControlPoint2D(2, -2));
            }
            else
            {
                var spiral = Generator<ChiselSpiralStairsComponent>(out model);
                spiral.definition.settings.riserType = riser;
                perStep = spiral.definition.settings.AnglePerStep;
            }
            model.GetComponentInChildren<ChiselGeneratorComponent>().OnValidate();
            Update();
            // looking down the stairs: on straight stairs along z (each part of a path has its own), on spiral ones back around
            bool LooksDownTheStairs(Vector3 normal, Vector3 middle)
            {
                if (kind == 0)
                    return normal.z > 0.9f;
                if (kind == 1)
                    return normal.z < -0.9f;
                var outwards = new Vector2(middle.x, middle.z).normalized;
                if (Mathf.Abs(Vector2.Dot(outwards, new Vector2(normal.x, normal.z))) > 0.1f)
                    return false;
                return Vector2.Dot(new Vector2(normal.x, normal.z), new Vector2(outwards.y, -outwards.x) * Mathf.Sign(perStep)) < 0;
            }

            var tops = new Dictionary<Vector3Int, Vector2>();
            var fronts = new List<(Vector3Int at, Vector2 uv)>();
            foreach (var renderable in model.generated.renderables)
            {
                if (renderable == null || renderable.sharedMesh == null)
                    continue;
                var v  = renderable.sharedMesh.vertices;
                var uv = renderable.sharedMesh.uv;
                var t  = renderable.sharedMesh.triangles;
                for (int i = 0; i < t.Length; i += 3)
                {
                    var normal = Vector3.Cross(v[t[i + 1]] - v[t[i]], v[t[i + 2]] - v[t[i]]).normalized;
                    var middle = (v[t[i]] + v[t[i + 1]] + v[t[i + 2]]) / 3;
                    var highest = Mathf.Max(v[t[i]].y, Mathf.Max(v[t[i + 1]].y, v[t[i + 2]].y));
                    for (int k = 0; k < 3; k++)
                    {
                        var at = Vector3Int.RoundToInt(v[t[i + k]] * 1000);
                        if (normal.y > 0.9f)
                            tops[at] = uv[t[i + k]];
                        else if (Mathf.Abs(normal.y) < 0.1f && LooksDownTheStairs(normal, middle) && v[t[i + k]].y > highest - 1e-4f)
                            fronts.Add((at, uv[t[i + k]]));
                    }
                }
            }
            // a corner on a front's top edge has the top's uv
            int shared = 0, same = 0;
            var samples = "";
            foreach (var front in fronts)
            {
                if (!tops.TryGetValue(front.at, out var uv))
                    continue;
                shared++;
                if ((uv - front.uv).magnitude < 1e-3f)
                    same++;
                else if (shared <= 10)
                    samples += $" {front.at}: top {uv:F3} upright {front.uv:F3};";
            }
            Assert.That(shared, Is.GreaterThan(0), "no tops meet anything upright");
            Assert.That(same, Is.GreaterThanOrEqualTo(shared * 7 / 10), $"only {same} of {shared} corners on a top have the same uv on both faces:{samples}");
        }

        // A capsule that isn't elliptical is round: its depth is its width
        [Test]
        public void CapsuleNotElliptical_IsRound()
        {
            var capsule = ChiselCapsule.DefaultSettings;
            capsule.diameterX = 2;
            capsule.diameterZ = 1;
            capsule.Validate();
            Assert.That(capsule.diameterZ, Is.EqualTo(2));
            capsule.isElliptical = true;
            capsule.diameterZ = 1;
            capsule.Validate();
            Assert.That(capsule.diameterZ, Is.EqualTo(1), "elliptical");
        }

        // Setting a torus's inner diameter sets it; the value given was ignored, so its inner handle did nothing
        [Test]
        public void TorusInnerDiameter_IsSetAsGiven()
        {
            var torus = ChiselTorus.DefaultSettings;
            torus.outerDiameter = 2;
            torus.InnerDiameter = 1.2f;
            Assert.That(torus.InnerDiameter, Is.EqualTo(1.2f).Within(1e-4f));
        }

        // A typed or dragged base height keeps the height and the number of steps, the steps taking up what's left
        [Test]
        public void PathedStairsBaseHeight_ChangesTheStepHeightNotTheirNumber()
        {
            var definition = new ChiselPathedStairsDefinition();
            definition.Reset();
            ref var stairs = ref definition.settings.stairs;
            stairs.bounds        = new MinMaxAABB { Min = new float3(-1, 0, 0), Max = new float3(1, 2, 2) };
            stairs.stepHeight    = 0.2f;
            stairs.plateauHeight = 0;
            var steps = stairs.StepCount;
            definition.SetBaseHeight(1);
            Assert.That(stairs.plateauHeight, Is.EqualTo(1).Within(1e-4f));
            Assert.That(stairs.StepCount, Is.EqualTo(steps));
            Assert.That(stairs.AbsHeight, Is.EqualTo(2).Within(1e-4f));
        }

        // Stairs that start higher up have their base, from the floor to the lowest step, in a surface of its own; it was
        // the lowest step's, down to the floor
        [TestCase(0, StairsRiserType.ThickRiser)]
        [TestCase(0, StairsRiserType.ThinRiser)]
        [TestCase(0, StairsRiserType.Smooth)]
        [TestCase(1, StairsRiserType.ThickRiser)]
        [TestCase(2, StairsRiserType.ThickRiser)]
        [TestCase(2, StairsRiserType.ThinRiser)]
        [TestCase(2, StairsRiserType.Smooth)]
        public void StairsWithABase_HaveItInItsOwnSurface(int kind, StairsRiserType riserType)
        {
            ChiselGeneratorComponent stairs;
            ChiselModelComponent model;
            float baseTop;
            if (kind == 2)
            {
                var s = Generator<ChiselSpiralStairsComponent>(out model);
                s.definition.settings.riserType = riserType;
                s.definition.settings.origin    = new float3(0, 0.5f, 0);
                baseTop = 0.5f;
                stairs = s;
            } else
            {
                ChiselLinearStairs settings;
                if (kind == 0)
                {
                    var linear = Generator<ChiselLinearStairsComponent>(out model);
                    settings = linear.definition.settings;
                    stairs = linear;
                } else
                {
                    var pathed = Generator<ChiselPathedStairsComponent>(out model);
                    pathed.definition.shape = new Curve2D(false, new CurveControlPoint2D(-2, -2), new CurveControlPoint2D(2, -2), new CurveControlPoint2D(2, 2));
                    settings = pathed.definition.settings.stairs;
                    stairs = pathed;
                }
                settings.bounds        = new MinMaxAABB { Min = new float3(-1, 0, 0), Max = new float3(1, 1, 2) };
                settings.stepHeight    = 0.2f;
                settings.stepDepth     = 0.2f;
                settings.plateauHeight = 0.4f;
                settings.riserType     = riserType;
                if (stairs is ChiselLinearStairsComponent l) l.definition.settings = settings;
                else ((ChiselPathedStairsComponent)stairs).definition.settings.stairs = settings;
                baseTop = 0.4f;
            }
            var materials = new List<Material>();
            try
            {
                var triangles = TrianglesBySurface(stairs, model, materials);
                Assert.That(triangles.ContainsKey((int)StairsSurface.Base), Is.True, "no base drawn");
                foreach (var t in triangles[(int)StairsSurface.Base])
                    Assert.That(Mathf.Max(t.a.y, Mathf.Max(t.b.y, t.c.y)), Is.LessThanOrEqualTo(baseTop + 1e-3f), "a base face above the base");
                foreach (var pair in triangles)
                {
                    var surface = (StairsSurface)pair.Key;
                    if (surface == StairsSurface.Base || surface == StairsSurface.Wall || surface == StairsSurface.WallTop || surface == StairsSurface.WallBottom)
                        continue;
                    foreach (var t in pair.Value)
                    {
                        var normal = Vector3.Cross(t.b - t.a, t.c - t.a).normalized;
                        if (Mathf.Abs(normal.y) > 0.1f)
                            continue;
                        Assert.That(Mathf.Max(t.a.y, Mathf.Max(t.b.y, t.c.y)), Is.GreaterThan(baseTop + 1e-3f), $"a {surface} face beside the base");
                    }
                }
            }
            finally { foreach (var m in materials) UnityEngine.Object.DestroyImmediate(m); }
        }

        // the triangles drawn, by the surface they're drawn with, each surface given a material of its own
        Dictionary<int, List<(Vector3 a, Vector3 b, Vector3 c)>> TrianglesBySurface(ChiselGeneratorComponent generator, ChiselModelComponent model, List<Material> materials)
        {
            for (int i = 0; i < generator.SurfaceDefinition.surfaces.Length; i++)
            {
                var material = new Material(ChiselProjectSettings.DefaultWallMaterial) { name = "surface " + i };
                materials.Add(material);
                generator.SurfaceDefinition.surfaces[i].SetMaterial(material);
            }
            generator.OnValidate();
            Update();
            var result = new Dictionary<int, List<(Vector3 a, Vector3 b, Vector3 c)>>();
            foreach (var renderable in model.generated.renderables)
            {
                if (renderable == null || renderable.sharedMesh == null || renderable.renderMaterials == null)
                    continue;
                var mesh = renderable.sharedMesh;
                var vertices = mesh.vertices;
                for (int sub = 0; sub < mesh.subMeshCount && sub < renderable.renderMaterials.Length; sub++)
                {
                    var surface = materials.IndexOf(renderable.renderMaterials[sub]);
                    if (surface < 0)
                        continue;
                    if (!result.TryGetValue(surface, out var list))
                        result[surface] = list = new List<(Vector3 a, Vector3 b, Vector3 c)>();
                    var triangles = mesh.GetTriangles(sub);
                    for (int t = 0; t + 2 < triangles.Length; t += 3)
                        list.Add((vertices[triangles[t]], vertices[triangles[t + 1]], vertices[triangles[t + 2]]));
                }
            }
            return result;
        }

        // The column in the middle has a surface of its own, and the treads' bottoms one apart from the steps' backs
        [Test]
        public void SpiralStairsColumnAndTreadBottoms_HaveTheirOwnSurfaces()
        {
            var stairs = Generator<ChiselSpiralStairsComponent>(out var model);
            stairs.definition.settings.centerType = SpiralStairsCenterType.Solid;
            if (stairs.definition.settings.innerDiameter < 0.2f)
                stairs.definition.settings.innerDiameter = 0.4f;
            Assert.That(stairs.surfaceArray.surfaces[(int)StairsSurface.Column].RenderMaterial.name, Is.EqualTo("Column"));
            Assert.That(stairs.surfaceArray.surfaces[(int)StairsSurface.TreadBottom].RenderMaterial.name, Is.EqualTo("TreadBottom"));
            var materials = new List<Material>();
            try
            {
                var normals = NormalsBySurface(stairs, model, materials);
                Assert.That(normals.ContainsKey((int)StairsSurface.Column), Is.True, "no column drawn");
                AssertFacing(normals, StairsSurface.TreadBottom, n => n.y < -0.9f, "down");
            }
            finally { foreach (var m in materials) UnityEngine.Object.DestroyImmediate(m); }
        }

        // The inspector shows only the surfaces stairs, set up as they are, have faces with: none drawn is left out, and
        // walls and a column they don't have aren't shown
        [TestCase(false, StairsSideType.None)]
        [TestCase(false, StairsSideType.DownAndUp)]
        [TestCase(true, StairsSideType.None)]
        [TestCase(true, StairsSideType.Up)]
        public void Stairs_ShowOnlyTheSurfacesTheyUse(bool spiral, StairsSideType side)
        {
            ChiselGeneratorComponent stairs;
            ChiselModelComponent model;
            if (spiral)
            {
                var s = Generator<ChiselSpiralStairsComponent>(out model);
                s.definition.settings.outerSide = side;
                stairs = s;
            } else
            {
                var s = Generator<ChiselLinearStairsComponent>(out model);
                s.definition.settings.leftSide = side;
                stairs = s;
            }
            var materials = new List<Material>();
            try
            {
                var normals = NormalsBySurface(stairs, model, materials);
                foreach (var surface in normals.Keys)
                    Assert.That(stairs.IsSurfaceUsed(surface), Is.True, $"{(StairsSurface)surface} is drawn but not shown");
                Assert.That(stairs.IsSurfaceUsed((int)StairsSurface.Wall), Is.EqualTo(side != StairsSideType.None), "walls");
                Assert.That(stairs.IsSurfaceUsed((int)StairsSurface.Column), Is.False, "a column without one");
            }
            finally { foreach (var m in materials) UnityEngine.Object.DestroyImmediate(m); }
        }

        // Under a side wall is the wall's bottom, not the steps' underside
        [Test]
        public void LinearStairsSideWall_HasAWallBottom()
        {
            var stairs = Generator<ChiselLinearStairsComponent>(out var model);
            stairs.definition.settings.leftSide = StairsSideType.Down;
            var materials = new List<Material>();
            try
            {
                var normals = NormalsBySurface(stairs, model, materials);
                // a wall below the steps slopes along them underneath
                AssertFacing(normals, StairsSurface.WallBottom, n => n.y < -0.5f, "down");
            }
            finally { foreach (var m in materials) UnityEngine.Object.DestroyImmediate(m); }
        }

        // The way a surface with this name faces, when its generator is not turned; zero when the name says no way
        static Vector3 NamedDirection(string name)
        {
            switch (name)
            {
                case ChiselSurfaceNames.Top:    return Vector3.up;
                case ChiselSurfaceNames.Bottom: return Vector3.down;
                case ChiselSurfaceNames.Left:   return Vector3.left;
                case ChiselSurfaceNames.Right:  return Vector3.right;
                case ChiselSurfaceNames.Front:  return Vector3.forward;   // as the stairs, which go down towards +z
                case ChiselSurfaceNames.Back:   return Vector3.back;
                default:                        return Vector3.zero;
            }
        }

        // A side of an extruded shape is the span from its point to the next: here the first span is by far the longest, the
        // second the shortest. Each side took the surface of the point two further on
        [Test]
        public void ExtrudedShapeSides_AreTheSpansFromTheirPoint()
        {
            var shape = Generator<ChiselExtrudedShapeComponent>(out var model);
            shape.definition.shape = new Curve2D(new CurveControlPoint2D(0, 0), new CurveControlPoint2D(4, 0), new CurveControlPoint2D(4, 1),
                                                 new CurveControlPoint2D(2, 3), new CurveControlPoint2D(0, 1));
            shape.OnValidate();
            Update();
            var materials = new List<Material>();
            try
            {
                NormalsBySurface(shape, model, materials);
                var areas = new float[shape.SurfaceDefinition.surfaces.Length];
                foreach (var renderable in model.generated.renderables)
                {
                    if (renderable == null || renderable.sharedMesh == null || renderable.renderMaterials == null)
                        continue;
                    var vertices = renderable.sharedMesh.vertices;
                    for (int sub = 0; sub < renderable.sharedMesh.subMeshCount && sub < renderable.renderMaterials.Length; sub++)
                    {
                        var surface = materials.IndexOf(renderable.renderMaterials[sub]);
                        if (surface < 0)
                            continue;
                        var triangles = renderable.sharedMesh.GetTriangles(sub);
                        for (int t = 0; t + 2 < triangles.Length; t += 3)
                            areas[surface] += Vector3.Cross(vertices[triangles[t + 1]] - vertices[triangles[t]], vertices[triangles[t + 2]] - vertices[triangles[t]]).magnitude * 0.5f;
                    }
                }
                // sides 1 to 5 are surfaces 2 to 6, spans of 4, 1, 2.8, 2.8 and 1 long
                Assert.That(areas[2], Is.GreaterThan(0), "side 1 isn't drawn");
                Assert.That(areas[2] / areas[3], Is.EqualTo(4).Within(0.01f), "side 1 or 2 is another span");
                Assert.That(areas[4] / areas[3], Is.EqualTo(math.sqrt(8)).Within(0.01f), "side 3 is another span");
                Assert.That(areas[6] / areas[3], Is.EqualTo(1).Within(0.01f), "side 5 is another span");
            }
            finally { foreach (var m in materials) UnityEngine.Object.DestroyImmediate(m); }
        }

        // Every surface a generator names faces the way its name says (a rounded one leans that way), and every surface
        // drawn has a name and is shown. Capsule and hemisphere drew everything with their first surface, a sphere its
        // first faces each with a surface of its own, a torus its tube with three
        [TestCase(typeof(ChiselBoxComponent))]
        [TestCase(typeof(ChiselCylinderComponent))]
        [TestCase(typeof(ChiselCapsuleComponent))]
        [TestCase(typeof(ChiselHemisphereComponent))]
        [TestCase(typeof(ChiselSphereComponent))]
        [TestCase(typeof(ChiselStadiumComponent))]
        [TestCase(typeof(ChiselTorusComponent))]
        [TestCase(typeof(ChiselExtrudedShapeComponent))]
        [TestCase(typeof(ChiselRevolvedShapeComponent))]
        public void NamedSurfaces_FaceTheWayTheirNameSays(Type type)
        {
            var root = new GameObject("named surfaces test");
            roots.Add(root);
            var model = root.AddComponent<ChiselModelComponent>();
            var go = new GameObject(type.Name);
            go.transform.SetParent(root.transform, false);
            var generator = (ChiselGeneratorComponent)go.AddComponent(type);
            Update();
            var materials = new List<Material>();
            try
            {
                var normals = NormalsBySurface(generator, model, materials);
                var named = 0;
                for (int i = 0; i < generator.SurfaceDefinition.surfaces.Length; i++)
                {
                    var name = generator.GetSurfaceName(i);
                    Assert.That(name, Is.Not.Null, $"surface {i} has no name");
                    var direction = NamedDirection(name);
                    if (direction == Vector3.zero)
                        continue;
                    Assert.That(normals.ContainsKey(i), Is.True, $"nothing drawn as {name}");
                    foreach (var normal in normals[i])
                        Assert.That(Vector3.Dot(normal, direction), Is.GreaterThan(0.01f), $"{name} faces {normal}");
                    named++;
                }
                foreach (var surface in normals.Keys)
                    Assert.That(generator.IsSurfaceUsed(surface), Is.True, $"{generator.GetSurfaceName(surface)} is drawn but not shown");
                // a revolved shape has no surface facing one way only; the others have their top and bottom at least
                if (type != typeof(ChiselRevolvedShapeComponent))
                    Assert.That(named, Is.GreaterThanOrEqualTo(2), "too few surfaces with a direction");
            }
            finally { foreach (var m in materials) UnityEngine.Object.DestroyImmediate(m); }
        }
    }
}
