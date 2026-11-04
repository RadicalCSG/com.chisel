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

        // Every edge drawn by exactly two triangles, and something drawn
        static void AssertClosed(ChiselModelComponent model, string what)
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
            Assert.That(report.nonManifoldEdgeCount, Is.Zero, $"{what}: edges drawn by more than two triangles");
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

        // Filled down, with a rail on the left only: the fill under the steps reaches the right side and stops at the left
        // rail (it was narrowed on the wrong side, so it stuck out under the rail and fell short on the other side)
        [Test]
        public void LinearStairsFilledDown_WithOneRail_FillUpToTheRail()
        {
            var stairs = Generator<ChiselLinearStairsComponent>(out var model);
            ref var settings = ref stairs.definition.settings;
            settings.bounds    = new MinMaxAABB { Min = new float3(0, 0, 0), Max = new float3(2, 1, 2) };
            settings.riserType = StairsRiserType.FillDown;
            settings.rightSide = StairsSideType.None;
            settings.leftSide  = StairsSideType.DownAndUp;
            stairs.OnValidate();
            Update();
            var sideWidth = stairs.definition.settings.sideWidth;
            float minX = float.PositiveInfinity, maxX = float.NegativeInfinity;
            foreach (var renderable in model.generated.renderables)
            {
                if (renderable == null || renderable.sharedMesh == null)
                    continue;
                foreach (var v in renderable.sharedMesh.vertices)
                {
                    if (v.y > 1e-4f)
                        continue;   // the bottom only
                    minX = math.min(minX, v.x);
                    maxX = math.max(maxX, v.x);
                }
            }
            Assert.That(minX, Is.EqualTo(0).Within(1e-4f), "the fill falls short of the right side");
            Assert.That(maxX, Is.EqualTo(2 - sideWidth).Within(1e-4f), "the fill doesn't stop at the left rail");
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
