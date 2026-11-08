using System.Collections.Generic;
using Chisel.Components;
using Chisel.Core;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;

namespace Chisel.Components.Tests
{
    [TestFixture]
    public sealed class ChiselGeneratorPartsTests
    {
        readonly List<Object> created = new();

        [TearDown]
        public void TearDown()
        {
            for (int i = created.Count - 1; i >= 0; i--)
            {
                if (created[i]) Object.DestroyImmediate(created[i]);
            }
            created.Clear();
        }

        static void UpdateModels()
        {
            for (int i = 0; i < 3; i++)
            {
                ChiselNodeHierarchyManager.Update();
                ChiselModelManager.Instance.UpdateModels();
            }
        }

        ChiselLinearStairsComponent Stairs()
        {
            var model = new GameObject("Model").AddComponent<ChiselModelComponent>();
            created.Add(model.gameObject);
            var stairs = new GameObject("Stairs").AddComponent<ChiselLinearStairsComponent>();
            stairs.transform.SetParent(model.transform, false);
            UpdateModels();
            return stairs;
        }

        // A brush is counted from the end it is nearer to, so the top step stays the top step when steps are added
        [Test]
        public void APart_IsCountedFromTheNearerEnd()
        {
            Assert.That(ChiselGeneratorParts.PlaceOf(new ChiselPartReference { valid = true, index = 1 }, 10), Is.EqualTo(1));
            Assert.That(ChiselGeneratorParts.PlaceOf(new ChiselPartReference { valid = true, index = 0, fromEnd = true }, 10), Is.EqualTo(9));
            Assert.That(ChiselGeneratorParts.PlaceOf(new ChiselPartReference { valid = true, index = 0, fromEnd = true }, 12), Is.EqualTo(11), "the last, however many");
            Assert.That(ChiselGeneratorParts.PlaceOf(new ChiselPartReference { valid = true, index = 5 }, 3), Is.EqualTo(-1), "gone");
            Assert.That(ChiselGeneratorParts.PlaceOf(default, 3), Is.EqualTo(-1));
        }

        // Each brush of a generator made of several is its own part, and the part leads back to it
        [Test]
        public void EveryBrush_OfStairs_IsItsOwnPart()
        {
            var stairs  = Stairs();
            var brushes = new List<CSGTreeBrush>();
            ChiselGeneratorParts.BrushesOf(stairs, brushes);
            Assert.That(brushes.Count, Is.GreaterThan(2), "steps");

            foreach (var brush in brushes)
            {
                var part = ChiselGeneratorParts.PartOf(stairs, brush);
                Assert.That(ChiselGeneratorParts.TryResolve(stairs, part, out var resolved), Is.True);
                Assert.That(resolved, Is.EqualTo(brush));
            }
        }

        // Stairs say what their brushes are: a target on a tread is a tread, counted among the treads
        [Test]
        public void Stairs_NameTheirParts()
        {
            var stairs  = Stairs();
            var brushes = new List<CSGTreeBrush>();
            ChiselGeneratorParts.BrushesOf(stairs, brushes);
            var roles = new HashSet<int>();
            for (int i = 0; i < brushes.Count; i++)
                roles.Add(ChiselGeneratorParts.RoleOf(stairs, i));
            Assert.That(roles, Does.Contain((int)ChiselLinearStairs.Part.Step));
            Assert.That(ChiselGeneratorParts.RoleName(stairs, (int)ChiselLinearStairs.Part.Step), Is.EqualTo("Step"));

            var lastStep = -1;
            for (int i = 0; i < brushes.Count; i++)
            {
                if (ChiselGeneratorParts.RoleOf(stairs, i) == (int)ChiselLinearStairs.Part.Step)
                    lastStep = i;
            }
            var part = ChiselGeneratorParts.PartOf(stairs, brushes[lastStep]);
            Assert.That(part.role, Is.EqualTo((int)ChiselLinearStairs.Part.Step));
            Assert.That(part.fromEnd && part.index == 0, Is.True, "the last step, counted among the steps");
            Assert.That(ChiselGeneratorParts.NameOf(stairs, part), Is.EqualTo("Step 1 from the end"));
            Assert.That(ChiselGeneratorParts.FullNameOf(stairs, part), Is.EqualTo("Stairs > Step 1 from the end"), "with the generator it is of");
        }

        static List<CSGTreeBrush> StepsOf(ChiselLinearStairsComponent stairs)
        {
            var brushes = new List<CSGTreeBrush>();
            ChiselGeneratorParts.BrushesOf(stairs, brushes);
            var steps = new List<CSGTreeBrush>();
            for (int i = 0; i < brushes.Count; i++)
            {
                if (ChiselGeneratorParts.RoleOf(stairs, i) == (int)ChiselLinearStairs.Part.Step)
                    steps.Add(brushes[i]);
            }
            return steps;
        }

        // A decal's part counted the other way round is the same step now, every one of its targets on that step with it;
        // counted from the end it stays that far from the top when steps are added, and back from the start that far
        // from the bottom
        [Test]
        public void ADecalsPart_CountedTheOtherWay_IsTheSameStep()
        {
            var stairs = Stairs();
            var steps  = StepsOf(stairs);
            Assert.That(steps.Count, Is.GreaterThan(3), "steps");
            var decal = new GameObject("Decal").AddComponent<ChiselDecalComponent>();
            created.Add(decal.gameObject);
            decal.SetTargets(new[] { ChiselGeneratorParts.TargetOf(stairs, steps[1], 0), ChiselGeneratorParts.TargetOf(stairs, steps[1], 1) });
            Assert.That(decal.Targets[0].part.fromEnd, Is.False, "the second step counts from the start");

            decal.CountPartFrom(fromEnd: true);
            foreach (var target in decal.Targets)
            {
                Assert.That(target.part.fromEnd, Is.True, "counted from the end");
                Assert.That(ChiselGeneratorParts.TryResolve(stairs, target.part, out var brush) && brush == steps[1], Is.True, "the same step");
            }

            // more steps: it is as far from the top as it was
            var fromTop = steps.Count - 1 - 1;
            stairs.definition.settings.stepHeight *= 0.5f;
            stairs.OnValidate();
            UpdateModels();
            steps = StepsOf(stairs);
            Assert.That(ChiselGeneratorParts.TryResolve(stairs, decal.Targets[0].part, out var moved) && moved == steps[steps.Count - 1 - fromTop], Is.True,
                        "the step as far from the top");

            decal.CountPartFrom(fromEnd: false);
            Assert.That(decal.Targets[0].part.fromEnd, Is.False, "counted from the start");
            Assert.That(ChiselGeneratorParts.TryResolve(stairs, decal.Targets[0].part, out var back) && back == moved, Is.True, "still that step");
            Assert.That(decal.Targets[1].part, Is.EqualTo(decal.Targets[0].part), "its other target on that step with it");
        }

        T Generator<T>() where T : ChiselGeneratorComponent
        {
            var model = new GameObject("Model").AddComponent<ChiselModelComponent>();
            created.Add(model.gameObject);
            var generator = new GameObject(typeof(T).Name).AddComponent<T>();
            generator.transform.SetParent(model.transform, false);
            UpdateModels();
            return generator;
        }

        static HashSet<int> RolesOf(ChiselGeneratorComponent generator)
        {
            var brushes = new List<CSGTreeBrush>();
            ChiselGeneratorParts.BrushesOf(generator, brushes);
            var roles = new HashSet<int>();
            for (int i = 0; i < brushes.Count; i++)
                roles.Add(ChiselGeneratorParts.RoleOf(generator, i));
            return roles;
        }

        // A step standing in the floor (as stairs' steps reach into the one below): the edge of its front nearest a point
        // just above the floor is where it meets the floor, which is no edge of its own
        [Test]
        public void TheNearestDrawnEdge_IsWhereAFaceMeetsAnotherBrush()
        {
            var model = new GameObject("Model").AddComponent<ChiselModelComponent>();
            created.Add(model.gameObject);
            ChiselBoxComponent Box(float3 min, float3 max)
            {
                var box = new GameObject("Box").AddComponent<ChiselBoxComponent>();
                box.transform.SetParent(model.transform, false);
                box.Max = new float3(1e4f);
                box.Min = min;
                box.Max = max;
                return box;
            }
            Box(new float3(-2, -1, -2), new float3(2, 0, 2));
            var step = Box(new float3(0, -1, -2), new float3(2, 1, 2));
            UpdateModels();

            var brush = (CSGTreeBrush)step.TopTreeNode;
            ref var mesh = ref BrushMeshManager.GetBrushMeshBlob(brush.BrushMesh).Value;
            var front = -1;
            for (int p = 0; p < mesh.localPlanes.Length; p++)
            {
                if (math.dot(math.normalize(mesh.localPlanes[p].xyz), new float3(-1, 0, 0)) > 0.999f)
                    front = p;
            }
            Assert.That(front, Is.GreaterThanOrEqualTo(0));
            var point = new float3(0, 0.05f, 0);

            Assert.That(ChiselTrimDecals.NearestEdge(brush, front, point, out var ownFrom, out var ownTo, out _), Is.True);
            Assert.That(ChiselTrimDecals.NearestDrawnEdge(model, brush, front, point, out var from, out var to), Is.True);
            Assert.That(from.y, Is.EqualTo(0).Within(1e-4f), "on the floor");
            Assert.That(to.y,   Is.EqualTo(0).Within(1e-4f), "on the floor");
            Assert.That(ChiselTrimDecals.OnLine(from, to, ownFrom, ownTo), Is.False, "not an edge of the step's own front");
        }

        // Stairs with treads that overhang: across the top edge of a riser is the underside of the tread above, which
        // can't be seen past the edge, but is found
        [Test]
        public void AboveARiser_TheTreadsUnderside_IsAcrossItsTopEdge()
        {
            var stairs = Stairs();
            stairs.TreadHeight = 0.02f;
            stairs.NosingDepth = 0.05f;
            for (int i = 0; i < 3; i++)
            {
                stairs.UpdateGeneratorNodes();
                UpdateModels();
            }
            var model   = stairs.GetComponentInParent<ChiselModelComponent>();
            var brushes = new List<CSGTreeBrush>();
            ChiselGeneratorParts.BrushesOf(stairs, brushes);
            Assume.That(ChiselGeneratorParts.CountOf(stairs, (int)ChiselLinearStairs.Part.Tread), Is.GreaterThan(0), "stairs with treads");

            // a step in the middle: its riser, and the highest edge of it
            var step = -1;
            for (int b = 0, steps = 0; b < brushes.Count; b++)
            {
                if (ChiselGeneratorParts.RoleOf(stairs, b) == (int)ChiselLinearStairs.Part.Step && steps++ == 2)
                    step = b;
            }
            Assert.That(step, Is.GreaterThanOrEqualTo(0));
            var brush = brushes[step];
            ref var mesh = ref BrushMeshManager.GetBrushMeshBlob(brush.BrushMesh).Value;
            var riser = -1;
            for (int p = 0; p < mesh.polygons.Length; p++)
            {
                if (mesh.polygons[p].descriptionIndex == (int)StairsSurface.RiserFront)
                    riser = p;
            }
            Assert.That(riser, Is.GreaterThanOrEqualTo(0));
            var nodeToTree = brush.NodeToTreeSpaceMatrix;
            float3 from = default, to = default;
            var top = float.NegativeInfinity;
            ref var polygon = ref mesh.polygons[riser];
            for (int e = 0; e < polygon.edgeCount; e++)
            {
                var a = math.transform(nodeToTree, mesh.localVertices[mesh.halfEdges[polygon.firstEdge + e].vertexIndex]);
                var b = math.transform(nodeToTree, mesh.localVertices[mesh.halfEdges[polygon.firstEdge + (e + 1) % polygon.edgeCount].vertexIndex]);
                if (math.abs(a.y - b.y) < 1e-4f && a.y > top)
                {
                    top = a.y; from = a; to = b;
                }
            }

            Assert.That(ChiselTrimDecals.DrawnSurfaceOnEdge(model, stairs, brush, riser, from, to, out var across), Is.True, "a surface across the riser's top");
            Assert.That(across.surfaceIndex, Is.EqualTo((int)StairsSurface.TreadBottom));
        }

        // Smooth stairs with a wall going down on one side: the wall is one band under their slope, with nothing of it
        // facing the front (where it would show at the risers), and no piece in front of it
        [Test]
        public void SmoothStairs_WithASideDown_HaveABandUnderTheirSlope()
        {
            var stairs = Stairs();
            stairs.RiserType = StairsRiserType.Smooth;
            stairs.LeftSide  = StairsSideType.None;
            stairs.RightSide = StairsSideType.Down;
            stairs.SideDepth = 0.125f;
            stairs.definition.settings.sideFrontOffset = 0.2f;
            for (int i = 0; i < 3; i++)
            {
                stairs.UpdateGeneratorNodes();
                UpdateModels();
            }
            var brushes = new List<CSGTreeBrush>();
            ChiselGeneratorParts.BrushesOf(stairs, brushes);
            var sides = 0;
            for (int b = 0; b < brushes.Count; b++)
            {
                var role = ChiselGeneratorParts.RoleOf(stairs, b);
                Assert.That(role, Is.Not.EqualTo((int)ChiselLinearStairs.Part.SideFront), "no piece in front of a band");
                if (role != (int)ChiselLinearStairs.Part.RightSide)
                    continue;
                sides++;
                ref var mesh = ref BrushMeshManager.GetBrushMeshBlob(brushes[b].BrushMesh).Value;
                for (int p = 0; p < mesh.localPlanes.Length; p++)
                    Assert.That(mesh.localPlanes[p].z, Is.LessThan(0.99f), "nothing of the band faces the front");
            }
            Assert.That(sides, Is.EqualTo(1), "one band");
        }

        // On a base, the band under smooth steps ends where the base begins, rather than running on through it
        [Test]
        public void SmoothStairs_OnABase_EndTheirBandAtTheBase()
        {
            var stairs = Stairs();
            stairs.RiserType = StairsRiserType.Smooth;
            stairs.LeftSide  = StairsSideType.None;
            stairs.RightSide = StairsSideType.Down;
            stairs.definition.settings.plateauHeight = 0.3f;
            stairs.definition.settings.stepDepth     = 0.2f;
            stairs.definition.settings.stepHeight    = 0.1f;
            for (int i = 0; i < 3; i++)
            {
                stairs.UpdateGeneratorNodes();
                UpdateModels();
            }
            var brushes = new List<CSGTreeBrush>();
            ChiselGeneratorParts.BrushesOf(stairs, brushes);
            float bandFront = float.NegativeInfinity, baseBack = float.PositiveInfinity;
            for (int b = 0; b < brushes.Count; b++)
            {
                var role = ChiselGeneratorParts.RoleOf(stairs, b);
                if (role != (int)ChiselLinearStairs.Part.RightSide && role != (int)ChiselLinearStairs.Part.Base)
                    continue;
                ref var mesh = ref BrushMeshManager.GetBrushMeshBlob(brushes[b].BrushMesh).Value;
                for (int v = 0; v < mesh.localVertices.Length; v++)
                {
                    if (role == (int)ChiselLinearStairs.Part.Base) baseBack  = math.min(baseBack, mesh.localVertices[v].z);
                    else                                           bandFront = math.max(bandFront, mesh.localVertices[v].z);
                }
            }
            Assert.That(baseBack, Is.LessThan(float.PositiveInfinity), "a base");
            Assert.That(stairs.definition.settings.StepCount, Is.GreaterThan(1), "steps");
            Assert.That(bandFront, Is.LessThanOrEqualTo(baseBack + 1e-4f), "the band stops at the base");
        }

        // Smooth stairs without sides are only their steps: no guard rail pieces at their ends
        [Test]
        public void SmoothStairs_WithoutSides_HaveNoGuardRailSurfaces()
        {
            var stairs = Stairs();
            stairs.RiserType = StairsRiserType.Smooth;
            stairs.LeftSide  = StairsSideType.None;
            stairs.RightSide = StairsSideType.None;
            for (int i = 0; i < 3; i++)
            {
                stairs.UpdateGeneratorNodes();
                UpdateModels();
            }
            var brushes = new List<CSGTreeBrush>();
            ChiselGeneratorParts.BrushesOf(stairs, brushes);
            Assert.That(brushes.Count, Is.GreaterThan(0));
            for (int b = 0; b < brushes.Count; b++)
            {
                ref var mesh = ref BrushMeshManager.GetBrushMeshBlob(brushes[b].BrushMesh).Value;
                for (int p = 0; p < mesh.polygons.Length; p++)
                {
                    var surface = (StairsSurface)mesh.polygons[p].descriptionIndex;
                    Assert.That(surface, Is.Not.EqualTo(StairsSurface.Wall).And.Not.EqualTo(StairsSurface.WallTop).And.Not.EqualTo(StairsSurface.WallBottom),
                                $"no guard rail was asked for: brush {b} of {brushes.Count}, a {ChiselGeneratorParts.RoleName(stairs, ChiselGeneratorParts.RoleOf(stairs, b))}");
                }
            }
        }

        // Spiral stairs tell their steps apart from their column
        [Test]
        public void SpiralStairs_NameTheirParts()
        {
            var roles = RolesOf(Generator<ChiselSpiralStairsComponent>());
            Assert.That(roles.Contains((int)ChiselSpiralStairs.Part.Step) || roles.Contains((int)ChiselSpiralStairs.Part.Tread), Is.True, "steps");            Assert.That(roles.Count, Is.GreaterThan(1));
        }

        ChiselSpiralStairsComponent SpiralWithRail()
        {
            var stairs = Generator<ChiselSpiralStairsComponent>();
            stairs.definition.settings.outerSide = StairsSideType.DownAndUp;
            // few sides and a wide rail: the top of each piece twists, its inside and outside rising alike over different lengths
            stairs.definition.settings.outerSegments = 6;
            stairs.definition.settings.sideWidth     = 0.5f;
            stairs.OnValidate();
            UpdateModels();
            return stairs;
        }

        static float RailInside(ChiselSpiralStairsComponent stairs)
        {
            ref var settings = ref stairs.definition.settings;
            return settings.outerDiameter * 0.5f * math.cos(math.PI / settings.outerSegments) - settings.sideWidth;
        }

        // The shape the steps are trimmed by stands at the rail's inside: inside the rail, its sides would cut the rail's
        // top and sides down their middle
        [Test]
        public void SpiralStairs_TheStepsTrimmer_StopsAtTheRailsInside()
        {
            var stairs  = SpiralWithRail();
            var inside  = RailInside(stairs);
            var origin  = stairs.definition.settings.origin;
            var brushes = new List<CSGTreeBrush>();
            ChiselGeneratorParts.BrushesOf(stairs, brushes);
            var trimmers = 0;
            foreach (var brush in brushes)
            {
                if (brush.Operation != CSGOperationType.Intersecting)
                    continue;
                ref var mesh = ref BrushMeshManager.GetBrushMeshBlob(brush.BrushMesh).Value;
                for (int p = 0; p < mesh.localPlanes.Length; p++)
                {
                    var plane = mesh.localPlanes[p];
                    if (math.abs(plane.y) > 0.1f)
                        continue;
                    // its distance from the middle of the stairs
                    var distance = -(plane.w + math.dot(plane.xyz, (float3)origin));
                    Assert.That(distance, Is.EqualTo(inside).Within(1e-3f), $"side {p} of a trimmer");
                }
                trimmers++;
            }
            Assert.That(trimmers, Is.GreaterThan(0));
        }

        // The tops of the rail's pieces have one texture running on over them: where corners meet, their uvs are the same
        [Test]
        public void SpiralStairs_TheRailsTop_RunsOnFromPieceToPiece()
        {
            var stairs  = SpiralWithRail();
            var brushes = new List<CSGTreeBrush>();
            ChiselGeneratorParts.BrushesOf(stairs, brushes);
            var uvAt = new Dictionary<int3, float2>();
            var compared = 0;
            for (int b = 0; b < brushes.Count; b++)
            {
                if (!IsOuterRail(ChiselGeneratorParts.RoleOf(stairs, b)))
                    continue;
                ref var mesh = ref BrushMeshManager.GetBrushMeshBlob(brushes[b].BrushMesh).Value;
                for (int p = 0; p < mesh.polygons.Length; p++)
                {
                    var plane = mesh.localPlanes[p];
                    if (plane.y < 0.1f)
                        continue;
                    ref var polygon = ref mesh.polygons[p];
                    var toPlane = MathExtensions.GenerateLocalToPlaneSpaceMatrix(plane);
                    float4 u = polygon.surface.details.UV0.U, v = polygon.surface.details.UV0.V;
                    for (int e = 0; e < polygon.edgeCount; e++)
                    {
                        var vertex = mesh.localVertices[mesh.halfEdges[polygon.firstEdge + e].vertexIndex];
                        var onPlane = math.mul(toPlane, new float4(vertex, 1)).xyz;
                        var uv = new float2(math.dot(u.xyz, onPlane) + u.w, math.dot(v.xyz, onPlane) + v.w);
                        var key = (int3)math.round(vertex * 1000);
                        if (uvAt.TryGetValue(key, out var earlier))
                        {
                            Assert.That(math.distance(uv, earlier), Is.LessThan(1e-3f), $"top corner {vertex}");
                            compared++;
                        } else
                            uvAt[key] = uv;
                    }
                }
            }
            Assert.That(compared, Is.GreaterThan(0), "pieces' tops share corners");
        }

        static bool IsOuterRail(int role) => role == (int)ChiselSpiralStairs.Part.OuterSide;

        // A trim along a top edge of the rail, outer or inner, on every piece: it stays on that edge, not on the level ends
        // between the pieces nor on the diagonal across a piece's top, but the flat piece in front of the rail has it too
        [TestCase(false)]
        [TestCase(true)]
        public void ATrim_OnEveryPieceOfASpiralRail_StaysOnItsEdge(bool inside)
        {
            var stairs  = SpiralWithRail();
            var brushes = new List<CSGTreeBrush>();
            ChiselGeneratorParts.BrushesOf(stairs, brushes);
            var origin = (float3)stairs.definition.settings.origin;
            ChiselDecalSurfaceTarget outer = default, top = default;
            var found = false;
            var facing = inside ? -1 : 1;
            for (int b = 0; b < brushes.Count && !found; b++)
            {
                if (!IsOuterRail(ChiselGeneratorParts.RoleOf(stairs, b)))
                    continue;
                ref var mesh = ref BrushMeshManager.GetBrushMeshBlob(brushes[b].BrushMesh).Value;
                int outerPolygon = -1, topPolygon = -1;
                for (int p = 0; p < mesh.polygons.Length; p++)
                {
                    var plane = mesh.localPlanes[p];
                    var middle = float3.zero;
                    for (int e = 0; e < mesh.polygons[p].edgeCount; e++)
                        middle += mesh.localVertices[mesh.halfEdges[mesh.polygons[p].firstEdge + e].vertexIndex];
                    middle /= mesh.polygons[p].edgeCount;
                    var outwards = math.normalizesafe((middle - origin).xz);
                    if (math.abs(plane.y) < 0.1f && math.dot(plane.xz, outwards) * facing > 0.8f) outerPolygon = p;
                }
                // of the top's faces, the one along that side (the top is two triangles where it twists)
                for (int p = 0; p < mesh.polygons.Length && outerPolygon >= 0; p++)
                {
                    if (mesh.localPlanes[p].y < 0.5f)
                        continue;
                    for (int e = 0; e < mesh.polygons[p].edgeCount; e++)
                    {
                        var twin = mesh.halfEdges[mesh.polygons[p].firstEdge + e].twinIndex;
                        if (twin >= 0 && mesh.halfEdgePolygonIndices[twin] == outerPolygon)
                            topPolygon = p;
                    }
                }
                if (outerPolygon < 0 || topPolygon < 0)
                    continue;
                outer = ChiselGeneratorParts.TargetOfPolygon(stairs, brushes[b], outerPolygon);
                top   = ChiselGeneratorParts.TargetOfPolygon(stairs, brushes[b], topPolygon);
                found = true;
            }
            Assert.That(found, Is.True, "a rail piece with that face");
            var decal = new GameObject("Trim").AddComponent<ChiselDecalComponent>();
            created.Add(decal.gameObject);
            decal.transform.SetParent(stairs.transform.parent, false);
            decal.Shape = ChiselDecalShape.Trim;
            decal.Size  = new Vector3(1, 1, 1);
            decal.SetTargets(new[] { outer, top });
            decal.OnEveryPart = true;
            UpdateModels();

            // how far a piece's top bends away from itself: the corners of one of its top's triangles from the other's plane
            var bend = 0.0f;
            for (int b = 0; b < brushes.Count; b++)
            {
                if (!IsOuterRail(ChiselGeneratorParts.RoleOf(stairs, b)))
                    continue;
                ref var mesh = ref BrushMeshManager.GetBrushMeshBlob(brushes[b].BrushMesh).Value;
                for (int p = 0; p < mesh.localPlanes.Length; p++)
                {
                    var plane = mesh.localPlanes[p];
                    if (plane.y < 0.5f)
                        continue;
                    for (int q = 0; q < mesh.polygons.Length; q++)
                    {
                        if (q == p || mesh.localPlanes[q].y < 0.5f)
                            continue;
                        for (int e = 0; e < mesh.polygons[q].edgeCount; e++)
                        {
                            var vertex = mesh.localVertices[mesh.halfEdges[mesh.polygons[q].firstEdge + e].vertexIndex];
                            bend = math.max(bend, math.abs(math.dot(plane.xyz, vertex) + plane.w));
                        }
                    }
                }
            }
            Assert.That(bend, Is.GreaterThan(0.001f), "a twisted top");

            var halves = new List<ChiselTrimDecals.Half>();
            Assert.That(ChiselTrimDecals.TryDescribe(decal, 1, 1, SurfaceDestinationFlags.Default, out _, halves, out _, out _), Is.True);
            var level = 0;
            var cut   = 0;
            var width = stairs.definition.settings.sideWidth;
            foreach (var half in halves)
            {
                var along = math.normalizesafe(half.decal.decalToTree.c0.xyz);
                if (math.abs(along.y) < 1e-3f)
                    level++;
                // along the rail's side, at one distance from the middle, not across it from its inside to its outside
                var middle = half.decal.decalToTree.c3.xyz;
                var reach  = along * half.decal.settings.size.x * 0.5f;
                var from   = math.length((middle - reach - origin).xz);
                var to     = math.length((middle + reach - origin).xz);
                Assert.That(math.abs(from - to), Is.LessThan(width * 0.5f), $"a piece from {middle - reach} to {middle + reach}");
                // the pieces join: ends cut where one piece's face turns into the next's
                if (math.any(half.decal.settings.endSlant != float2.zero))
                    cut++;
                // and no end so slanted it lies along the edge (faces almost in one plane give no sure line to cut on)
                Assert.That(math.cmax(math.abs(half.decal.settings.endSlant)), Is.LessThanOrEqualTo(4f), "a sane end");
                // deep enough for the top's second triangle, which bends away from the first
                Assert.That(half.decal.settings.size.z * 0.5f, Is.GreaterThanOrEqualTo(bend), "reaches the whole top");
            }
            // pieces along one straight side join square; at the rail's corners they are cut where its faces turn
            Assert.That(cut, Is.GreaterThan(0), "joined at the rail's corners");
            Assert.That(halves.Count, Is.GreaterThan(4), "the other pieces too");
            Assert.That(level, Is.LessThanOrEqualTo(2), "only the flat piece in front is level, not the ends between pieces");
        }

        // A trim on an edge of a face that shares its surface with an earlier face of the same brush (a rail's inside and
        // outside): it is on that face's edge, not on the earlier face, which doesn't meet the other surface
        [Test]
        public void ATrim_OnAFaceSharingItsSurface_IsOnThatFace()
        {
            var stairs  = SpiralWithRail();
            var brushes = new List<CSGTreeBrush>();
            ChiselGeneratorParts.BrushesOf(stairs, brushes);
            for (int b = 0; b < brushes.Count; b++)
            {
                var brush = brushes[b];
                ref var mesh = ref BrushMeshManager.GetBrushMeshBlob(brush.BrushMesh).Value;
                for (int p = 1; p < mesh.polygons.Length; p++)
                {
                    var shared = false;
                    for (int q = 0; q < p && !shared; q++)
                        shared = mesh.polygons[q].descriptionIndex == mesh.polygons[p].descriptionIndex;
                    if (!shared)
                        continue;
                    // an edge of it, and the polygon on the edge's other side
                    var first  = mesh.localVertices[mesh.halfEdges[mesh.polygons[p].firstEdge].vertexIndex];
                    var second = mesh.localVertices[mesh.halfEdges[mesh.polygons[p].firstEdge + 1].vertexIndex];
                    var middle = math.transform(brush.NodeToTreeSpaceMatrix, (first + second) * 0.5f);
                    if (!ChiselTrimDecals.NearestEdge(brush, p, middle, out var from, out var to, out var other, out var edgeIndex) || other < 0)
                        continue;
                    var decal = new GameObject("Trim").AddComponent<ChiselDecalComponent>();
                    created.Add(decal.gameObject);
                    decal.transform.SetParent(stairs.transform.parent, false);
                    decal.Shape = ChiselDecalShape.Trim;
                    decal.Size  = new Vector3(1, 0.1f, 1);
                    decal.SetTargets(new[] { ChiselGeneratorParts.TargetOfPolygon(stairs, brush, p), ChiselGeneratorParts.TargetOfPolygon(stairs, brush, other) });
                    decal.SetTrimEdge(false, edgeIndex);
                    UpdateModels();

                    var halves = new List<ChiselTrimDecals.Half>();
                    Assert.That(ChiselTrimDecals.TryDescribe(decal, 1, 1, SurfaceDestinationFlags.Default, out _, halves, out var edgeFrom, out var edgeTo), Is.True,
                                $"brush {b}, polygon {p}");
                    var same    = math.distance(edgeFrom, from) + math.distance(edgeTo, to);
                    var swapped = math.distance(edgeFrom, to) + math.distance(edgeTo, from);
                    Assert.That(math.min(same, swapped), Is.LessThan(1e-2f), "on the edge it was put on");
                    return;
                }
            }
            Assert.Inconclusive("no brush with two faces of one surface");
        }

        // Pathed stairs name the parts of each stretch as linear stairs do, and their corners
        [Test]
        public void PathedStairs_NameTheirParts()
        {
            var stairs = Generator<ChiselPathedStairsComponent>();
            var roles  = RolesOf(stairs);
            Assert.That(roles, Does.Contain((int)ChiselLinearStairs.Part.Step));
            Assert.That(roles, Has.No.Member(-1), "every brush has a part");
            Assert.That(ChiselGeneratorParts.RoleName(stairs, ChiselPathedStairs.kCornerPart), Is.EqualTo("Corner"));
        }

        // A box decal on every step: a copy on each other step, moved as that step is from its own
        [Test]
        public void ABoxDecal_OnEveryStep_IsCopiedOntoTheOthers()
        {
            var stairs  = Stairs();
            var brushes = new List<CSGTreeBrush>();
            ChiselGeneratorParts.BrushesOf(stairs, brushes);
            var target = ChiselGeneratorParts.TargetOf(stairs, brushes[0], (int)StairsSurface.RiserFront);
            var steps  = ChiselGeneratorParts.CountOf(stairs, target.part.role);
            Assert.That(steps, Is.GreaterThan(1));

            var copies = new List<float4x4>();
            ChiselTrimDecals.CopiesOnEveryPart(target, copies);
            Assert.That(copies.Count, Is.EqualTo(0), "only its own step");

            target.part.all = true;
            ChiselTrimDecals.CopiesOnEveryPart(target, copies);
            Assert.That(copies.Count, Is.EqualTo(steps - 1), "every other step");
            var moves = new HashSet<float3>();
            foreach (var copy in copies)
            {
                Assert.That(math.dot(copy.c0.xyz, new float3(1, 0, 0)), Is.EqualTo(1).Within(1e-3f), "straight stairs don't turn");
                Assert.That(math.length(copy.c3.xyz), Is.GreaterThan(1e-3f), "moved onto another step");
                moves.Add(math.round(copy.c3.xyz * 1000));
            }
            Assert.That(moves.Count, Is.EqualTo(copies.Count), "each onto a step of its own");
        }

        // In the model: a box decal on one step is one decal, on every step one on each
        [Test]
        public void ABoxDecal_OnEveryStep_ReachesTheModelOnEachStep()
        {
            var stairs  = Stairs();
            var model   = stairs.GetComponentInParent<ChiselModelComponent>();
            var brushes = new List<CSGTreeBrush>();
            ChiselGeneratorParts.BrushesOf(stairs, brushes);
            var target = ChiselGeneratorParts.TargetOf(stairs, brushes[1], (int)StairsSurface.RiserFront);
            var steps  = ChiselGeneratorParts.CountOf(stairs, target.part.role);
            Assert.That(ChiselTrimDecals.TryCentre(target, out var centre, out var normal), Is.True);

            var material = new Material(Shader.Find("Standard"));
            created.Add(material);
            var go = new GameObject("Decal");
            go.transform.SetParent(stairs.transform, false);
            go.transform.SetPositionAndRotation(model.transform.TransformPoint(centre), Quaternion.LookRotation(model.transform.TransformDirection(normal)));
            var decal = go.AddComponent<ChiselDecalComponent>();
            decal.Material = material;
            decal.Size     = new Vector3(0.1f, 0.1f, 0.2f);
            decal.SetTargets(new[] { target });
            UpdateModels();
            ChiselDecalManager.Update();
            Assert.That(model.Node.GetDecalCount(), Is.EqualTo(1), "its own step");

            decal.OnEveryPart = true;
            UpdateModels();
            ChiselDecalManager.Update();
            Assert.That(model.Node.GetDecalCount(), Is.EqualTo(steps), "every step");
        }

        // A trim on every step runs along the same edge of each
        [Test]
        public void ATrim_OnEveryStep_RunsAlongEachStep()
        {
            var stairs  = Stairs();
            var brushes = new List<CSGTreeBrush>();
            ChiselGeneratorParts.BrushesOf(stairs, brushes);
            var a = ChiselGeneratorParts.TargetOf(stairs, brushes[0], (int)StairsSurface.RiserFront);
            var b = ChiselGeneratorParts.TargetOf(stairs, brushes[0], (int)StairsSurface.StepSides);
            var decal = new GameObject("Trim").AddComponent<ChiselDecalComponent>();
            created.Add(decal.gameObject);
            decal.transform.SetParent(stairs.transform.parent, false);
            decal.Shape = ChiselDecalShape.Trim;
            decal.Size  = new Vector3(1, 0.1f, 1);
            decal.SetTargets(new[] { a, b });
            UpdateModels();

            var halves = new List<ChiselTrimDecals.Half>();
            Assume.That(ChiselTrimDecals.TryDescribe(decal, 1, 1, SurfaceDestinationFlags.Default, out _, halves, out _, out _), Is.True, "a step's front meets its side");
            var one = halves.Count;
            decal.OnEveryPart = true;
            Assert.That(decal.OnEveryPart, Is.True);
            Assert.That(ChiselTrimDecals.TryDescribe(decal, 1, 1, SurfaceDestinationFlags.Default, out _, halves, out _, out _), Is.True);
            Assert.That(halves.Count, Is.GreaterThan(one), "the edge of the other steps too");
        }

        // A trim on a step in the middle, on every step: it is still on its own step's edge, not the first step's
        [Test]
        public void ATrim_OnEveryStep_StaysOnItsOwnStep()
        {
            var stairs  = Stairs();
            var brushes = new List<CSGTreeBrush>();
            ChiselGeneratorParts.BrushesOf(stairs, brushes);
            var a = ChiselGeneratorParts.TargetOf(stairs, brushes[2], (int)StairsSurface.RiserFront);
            var b = ChiselGeneratorParts.TargetOf(stairs, brushes[2], (int)StairsSurface.StepSides);
            var decal = new GameObject("Trim").AddComponent<ChiselDecalComponent>();
            created.Add(decal.gameObject);
            decal.transform.SetParent(stairs.transform.parent, false);
            decal.Shape = ChiselDecalShape.Trim;
            decal.Size  = new Vector3(1, 0.1f, 1);
            decal.SetTargets(new[] { a, b });
            UpdateModels();

            var halves = new List<ChiselTrimDecals.Half>();
            Assume.That(ChiselTrimDecals.TryDescribe(decal, 1, 1, SurfaceDestinationFlags.Default, out _, halves, out var from, out var to), Is.True);
            decal.OnEveryPart = true;
            Assert.That(ChiselTrimDecals.TryDescribe(decal, 1, 1, SurfaceDestinationFlags.Default, out _, halves, out var everyFrom, out var everyTo), Is.True);
            var same    = math.distance(everyFrom, from) + math.distance(everyTo, to);
            var swapped = math.distance(everyFrom, to) + math.distance(everyTo, from);
            Assert.That(math.min(same, swapped), Is.LessThan(1e-3f), "its own edge, either way round");
        }

        // A trim between one step and the next, on every step: each step with the one after it, not with itself
        [Test]
        public void ATrim_BetweenSteps_OnEveryStep_KeepsTheNextStep()
        {
            var targets = new List<ChiselDecalSurfaceTarget>();
            var stairs  = Stairs();
            var count   = ChiselGeneratorParts.CountOf(stairs, (int)ChiselLinearStairs.Part.Step);
            var a = new ChiselDecalSurfaceTarget { brush = stairs, surfaceIndex = 0, part = new ChiselPartReference { valid = true, index = 1, all = true } };
            var b = new ChiselDecalSurfaceTarget { brush = stairs, surfaceIndex = 1, part = new ChiselPartReference { valid = true, index = 2 } };
            var decal = new GameObject("Trim").AddComponent<ChiselDecalComponent>();
            created.Add(decal.gameObject);
            decal.Shape = ChiselDecalShape.Trim;
            decal.SetTargets(new[] { a, b });
            ChiselTrimDecals.PairsOnEveryPart(decal.Targets, targets);
            Assert.That(targets.Count, Is.EqualTo((count - 1) * 2), "every step but the last has a next");
            Assert.That(targets[0].part.index, Is.EqualTo(1), "its own first");
            for (int i = 0; i < targets.Count; i += 2)
                Assert.That(targets[i + 1].part.index, Is.EqualTo(targets[i].part.index + 1), "the next step");
        }

        // A trim on the edge of a step that isn't the first: its targets name that step, not the first with the surface
        [Test]
        public void ATrimsTarget_OnAStep_IsThatStep()
        {
            var stairs  = Stairs();
            var brushes = new List<CSGTreeBrush>();
            ChiselGeneratorParts.BrushesOf(stairs, brushes);
            var step = brushes[brushes.Count / 2];
            var target = ChiselGeneratorParts.TargetOf(stairs, step, (int)StairsSurface.TreadTop);
            Assert.That(target.part.valid, Is.True);
            Assert.That(ChiselGeneratorParts.TryResolve(stairs, target.part, out var resolved) && resolved == step, Is.True);
            Assert.That(resolved, Is.Not.EqualTo(brushes[0]), "not the first step");
        }
    }
}
