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
            Assert.That(report.nonManifoldEdgeCount, Is.EqualTo(pinches), $"{what}: edges drawn by more than two triangles");
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

        // The top tread reaches out past the sides only when told to, and by as much
        [TestCase(0f)]
        [TestCase(0.2f)]
        public void LinearStairsTopTread_SticksOutByTheOverhangOnly(float overhang)
        {
            var stairs = Generator<ChiselLinearStairsComponent>(out var model);
            ref var settings = ref stairs.definition.settings;
            settings.bounds           = new MinMaxAABB { Min = new float3(0, 0, 0), Max = new float3(2, 1, 2) };
            settings.leftSide         = StairsSideType.Up;
            settings.rightSide        = StairsSideType.Up;
            settings.topTreadOverhang = overhang;
            stairs.OnValidate();
            Update();
            float minX = float.PositiveInfinity, maxX = float.NegativeInfinity;
            foreach (var renderable in model.generated.renderables)
            {
                if (renderable == null || renderable.sharedMesh == null)
                    continue;
                foreach (var v in renderable.sharedMesh.vertices)
                {
                    minX = math.min(minX, v.x);
                    maxX = math.max(maxX, v.x);
                }
            }
            // with walls on both sides, nothing else reaches past them
            Assert.That(minX, Is.EqualTo(-overhang).Within(1e-4f), "left of the stairs");
            Assert.That(maxX, Is.EqualTo(2 + overhang).Within(1e-4f), "right of the stairs");
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
                    var normal = Vector3.Cross(b - a, c - a).normalized;
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
                                seams++;
                        } else
                            uvAt[key] = uv[t[i + k]];
                    }
                }
            }
            }
            Assert.That(shared, Is.GreaterThan(0), "no front");
            Assert.That(seams, Is.Zero, $"corners on the front with another uv on each side, of {shared}");
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
            // out past the steps there is only the wall: every place along it has its lowest corner on the floor
            var faceDistance = outerApothem + stairs.definition.settings.sideWidth - 0.01f;
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
            foreach (var low in lowestAt.Values)
            {
                if (low > 1e-3f)
                    hanging++;
            }
            Assert.That(hanging, Is.Zero, $"places along the wall that don't reach the floor, of {lowestAt.Count}");
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

        static float Around(Vector3 point) { return new Vector2(point.x, point.z).magnitude; }

        ChiselSpiralStairsComponent WideSpiralStairs(out ChiselModelComponent model)
        {
            var stairs = Generator<ChiselSpiralStairsComponent>(out model);
            stairs.definition.settings.outerDiameter = 3;
            stairs.definition.settings.innerDiameter = 1;
            return stairs;
        }

        // A wall along the outside of spiral stairs stands outside the steps, as thick as set
        [Test]
        public void SpiralStairsWithAnOuterWall_HaveItOutsideTheSteps()
        {
            var stairs = WideSpiralStairs(out var model);
            stairs.definition.settings.outerSide = StairsSideType.Up;
            stairs.definition.settings.sideWidth = 0.2f;
            stairs.OnValidate();
            Update();
            var furthest = 0f;
            foreach (var vertex in DrawnVertices(model, out _))
                furthest = Mathf.Max(furthest, Around(vertex));
            Assert.That(furthest, Is.GreaterThan(1.5f + 0.15f), "nothing outside the steps");
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
    }
}
