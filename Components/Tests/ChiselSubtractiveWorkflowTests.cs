using System;
using System.Collections.Generic;
using Chisel.Core;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;

namespace Chisel.Components.Tests
{
    // A model with the subtractive workflow draws what it would draw without it, seen from the other side: its
    // brushes are the empty space, rooms carved out of solid, seen from inside
    [TestFixture]
#if UNITY_6000_7_OR_NEWER
    [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup] // only a fixed table of generators
#endif
    public class ChiselSubtractiveWorkflowTests
    {
        readonly List<GameObject> roots = new();
        readonly List<UnityEngine.Object> assets = new();

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
            foreach (var asset in assets)
            {
                if (asset)
                    UnityEngine.Object.DestroyImmediate(asset);
            }
            assets.Clear();
        }

        static void Update()
        {
            for (int i = 0; i < 3; i++)
            {
                ChiselNodeHierarchyManager.Update();
                ChiselModelManager.Instance.UpdateModels();
            }
        }

        static readonly Type[] kGenerators =
        {
            typeof(ChiselBoxComponent), typeof(ChiselBrushComponent), typeof(ChiselCapsuleComponent), typeof(ChiselCylinderComponent),
            typeof(ChiselExtrudedShapeComponent), typeof(ChiselHemisphereComponent), typeof(ChiselLinearStairsComponent),
            typeof(ChiselPathedStairsComponent), typeof(ChiselRevolvedShapeComponent), typeof(ChiselSphereComponent),
            typeof(ChiselSpiralStairsComponent), typeof(ChiselStadiumComponent), typeof(ChiselTorusComponent)
        };

        ChiselModelComponent Model()
        {
            var root = new GameObject("subtractive workflow test");
            roots.Add(root);
            return root.AddComponent<ChiselModelComponent>();
        }

        static ChiselGeneratorComponent Add(ChiselModelComponent model, Type type)
        {
            var go = new GameObject(type.Name);
            go.transform.SetParent(model.transform, false);
            var generator = (ChiselGeneratorComponent)go.AddComponent(type);
            Update();
            return generator;
        }

        static ChiselBoxComponent Box(ChiselModelComponent model, float3 min, float3 max, CSGOperationType operation = CSGOperationType.Additive)
        {
            var box = (ChiselBoxComponent)Add(model, typeof(ChiselBoxComponent));
            if (math.any(min > box.Max)) { box.Max = max; box.Min = min; }
            else                         { box.Min = min; box.Max = max; }
            box.Operation = operation;
            Update();
            return box;
        }

        static void SetSubtractiveWorkflow(ChiselModelComponent model, bool on)
        {
            model.renderSettings.subtractiveWorkflow = on;
            model.SyncModelSettingsStore();
            Update();
        }

        struct Corner
        {
            public Vector3Int key;
            public float3     position, normal;
            public float2     uv;
            public float4     tangent;
        }

        struct Triangle
        {
            public Corner a, b, c;
            public float3 WindingNormal => math.cross(b.position - a.position, c.position - a.position);
        }

        static Vector3Int KeyOf(float3 p) => new(Mathf.RoundToInt(p.x * 10000), Mathf.RoundToInt(p.y * 10000), Mathf.RoundToInt(p.z * 10000));

        static bool Less(Vector3Int l, Vector3Int r) => l.x != r.x ? l.x < r.x : l.y != r.y ? l.y < r.y : l.z < r.z;

        // The triangle's corners turned round so the smallest is first, keeping their order: the same for the same
        // triangle wound the same way, another for it wound the other way
        static (Vector3Int, Vector3Int, Vector3Int) WoundKey(Vector3Int a, Vector3Int b, Vector3Int c)
        {
            if (Less(b, a) && !Less(c, b)) return (b, c, a);
            if (Less(c, a) && Less(c, b))  return (c, a, b);
            return (a, b, c);
        }

        // Every triangle the model draws, in the model's space; with a material, only those drawn with it (decal) or not
        static List<Triangle> Drawn(ChiselModelComponent model, Material material = null, bool decal = true)
        {
            var triangles = new List<Triangle>();
            foreach (var renderable in model.generated.renderables)
            {
                if (renderable == null || renderable.sharedMesh == null)
                    continue;
                if (material && (renderable.query & SurfaceDestinationFlags.Renderable) == 0)
                    continue;
                var mesh      = renderable.sharedMesh;
                var positions = mesh.vertices;
                var normals   = mesh.normals;
                var uvs       = mesh.uv;
                var tangents  = mesh.tangents;
                Corner CornerOf(int i) => new()
                {
                    key      = KeyOf(positions[i]),
                    position = positions[i],
                    normal   = i < normals.Length ? (float3)normals[i] : float3.zero,
                    uv       = i < uvs.Length ? (float2)uvs[i] : float2.zero,
                    tangent  = i < tangents.Length ? (float4)tangents[i] : float4.zero
                };
                for (int sub = 0; sub < mesh.subMeshCount; sub++)
                {
                    if (material)
                    {
                        var drawnWith = renderable.renderMaterials != null && sub < renderable.renderMaterials.Length &&
                                        renderable.renderMaterials[sub] == material;
                        if (drawnWith != decal)
                            continue;
                    }
                    var indices = mesh.GetTriangles(sub);
                    for (int t = 0; t + 2 < indices.Length; t += 3)
                        triangles.Add(new Triangle { a = CornerOf(indices[t]), b = CornerOf(indices[t + 1]), c = CornerOf(indices[t + 2]) });
                }
            }
            return triangles;
        }

        static Dictionary<(Vector3Int, Vector3Int, Vector3Int), List<Triangle>> ByWinding(List<Triangle> triangles, bool reversed)
        {
            var lookup = new Dictionary<(Vector3Int, Vector3Int, Vector3Int), List<Triangle>>();
            foreach (var triangle in triangles)
            {
                var key = reversed ? WoundKey(triangle.a.key, triangle.c.key, triangle.b.key)
                                   : WoundKey(triangle.a.key, triangle.b.key, triangle.c.key);
                if (!lookup.TryGetValue(key, out var list))
                    lookup[key] = list = new List<Triangle>();
                list.Add(triangle);
            }
            return lookup;
        }

        static Corner CornerAt(Triangle triangle, Vector3Int key)
        {
            if (triangle.a.key == key) return triangle.a;
            if (triangle.b.key == key) return triangle.b;
            return triangle.c;
        }

        // Each drawn triangle the other's turned round: wound the other way, its normals the other way
        static void AssertTurnedRound(List<Triangle> without, List<Triangle> with, string what)
        {
            Assert.That(without.Count, Is.GreaterThan(0), $"{what}: nothing drawn");
            Assert.That(with.Count, Is.EqualTo(without.Count), $"{what}: as many triangles");
            var reversed = ByWinding(without, reversed: true);
            foreach (var triangle in with)
            {
                var key = WoundKey(triangle.a.key, triangle.b.key, triangle.c.key);
                Assert.That(reversed.TryGetValue(key, out var matches) && matches.Count > 0, Is.True,
                            $"{what}: triangle {(Vector3)triangle.a.position} {(Vector3)triangle.b.position} {(Vector3)triangle.c.position} isn't one drawn without the setting, wound the other way");
                var match = matches[matches.Count - 1];
                matches.RemoveAt(matches.Count - 1);
                foreach (var corner in new[] { triangle.a, triangle.b, triangle.c })
                {
                    var other = CornerAt(match, corner.key);
                    Assert.That(math.distance(corner.normal, -other.normal), Is.LessThan(1e-3f),
                                $"{what}: the normal at {(Vector3)corner.position} is {(Vector3)corner.normal}, not {(Vector3)(-other.normal)}");
                }
            }
        }

        // How a triangle's texture lies on it, seen from the side it is drawn on: 1 when its u runs to the right of its v,
        // -1 when mirrored, 0 when its uvs don't span it
        // The directions u and v grow fastest along a triangle; false when it has no area
        static bool UVGradients(Triangle triangle, out float3 gradientU, out float3 gradientV)
        {
            var n  = triangle.WindingNormal;
            var e1 = triangle.b.position - triangle.a.position;
            var e2 = triangle.c.position - triangle.a.position;
            var nn = math.lengthsq(n);
            gradientU = gradientV = float3.zero;
            if (!(nn > 1e-12f))
                return false;
            float3 Gradient(float f0, float f1, float f2) => ((f1 - f0) * math.cross(e2, n) + (f2 - f0) * math.cross(n, e1)) / nn;
            gradientU = Gradient(triangle.a.uv.x, triangle.b.uv.x, triangle.c.uv.x);
            gradientV = Gradient(triangle.a.uv.y, triangle.b.uv.y, triangle.c.uv.y);
            return true;
        }

        static int TextureHandedness(Triangle triangle)
        {
            if (!UVGradients(triangle, out var gradientU, out var gradientV))
                return 0;
            var n  = triangle.WindingNormal;
            var nn = math.lengthsq(n);
            var turn = math.dot(math.cross(gradientU, gradientV), n);
            if (math.abs(turn) < 1e-6f * math.length(gradientU) * math.length(gradientV) * math.sqrt(nn))
                return 0;
            return turn > 0 ? 1 : -1;
        }

        [TestCaseSource(nameof(kGenerators))]
        public void EveryGenerator_IsDrawnTurnedRound(Type type)
        {
            var model = Model();
            Add(model, type);
            var without = Drawn(model);
            SetSubtractiveWorkflow(model, true);
            AssertTurnedRound(without, Drawn(model), type.Name);
        }

        // A texture reads the same way from inside a room as it does from outside a solid: not mirrored
        [TestCaseSource(nameof(kGenerators))]
        public void EveryGenerator_TextureIsNotMirroredFromInside(Type type)
        {
            var model = Model();
            Add(model, type);
            var without = Drawn(model);
            SetSubtractiveWorkflow(model, true);
            var mirrored = MirroredAgainst(Drawn(model), without, out var compared);
            Assert.That(compared, Is.GreaterThan(0), $"{type.Name}: no textured triangles compared");
            Assert.That(mirrored, Is.Zero, $"{type.Name}: {mirrored} of {compared} triangles have their texture mirrored from inside");
        }

        // Without the setting, the walls of a room a subtractive brush carves out of a solid are that brush's faces seen
        // from inside, as a room's are with it: their texture reads as the brush's faces do from outside
        [Test]
        public void ARoomCarvedOutOfASolid_TextureIsNotMirrored()
        {
            var outside = Model();
            Box(outside, new float3(1, 1, 1), new float3(3, 2, 3));

            var carved = Model();
            Box(carved, new float3(0, 0, 0), new float3(4, 3, 4));
            Box(carved, new float3(1, 1, 1), new float3(3, 2, 3), CSGOperationType.Subtractive);
            var mirrored = MirroredAgainst(Drawn(carved), Drawn(outside), out var compared);
            Assert.That(compared, Is.EqualTo(12), "the room's six walls, two triangles each");
            Assert.That(mirrored, Is.Zero, $"{mirrored} of {compared} triangles of the room have their texture mirrored");
        }

        static void SetUV0(ChiselBoxComponent box, UVMatrix uv0)
        {
            foreach (var surface in box.SurfaceDefinition.surfaces)
                surface.surfaceDetails.UV0 = uv0;
            box.OnValidate();
            Update();
        }

        // How many triangles of one model read their texture the other way round than the same face of another
        static int MirroredAgainst(List<Triangle> triangles, List<Triangle> reference, out int compared)
        {
            var lookup = ByWinding(reference, reversed: true);
            var mirrored = 0;
            compared = 0;
            foreach (var triangle in triangles)
            {
                var key = WoundKey(triangle.a.key, triangle.b.key, triangle.c.key);
                if (!lookup.TryGetValue(key, out var matches) || matches.Count == 0)
                    continue;
                var a = TextureHandedness(matches[0]);
                var b = TextureHandedness(triangle);
                if (a == 0 || b == 0)
                    continue;
                compared++;
                if (a != b)
                    mirrored++;
            }
            return mirrored;
        }

        // A texture mirrored on purpose, by the surface's uv matrix, is mirrored on the walls of a room too: they read as
        // the brush's faces with the same mapping do from outside
        [Test]
        public void ARoomCarvedOutOfASolid_MirroredOnPurpose_IsMirrored([Values] bool subtractiveWorkflow)
        {
            var mirror = new UVMatrix(new float4(-1, 0, 0, 0.25f), new float4(0, 1, 0, 0));
            var outside = Model();
            SetUV0(Box(outside, new float3(1, 1, 1), new float3(3, 2, 3)), mirror);

            var carved = Model();
            if (subtractiveWorkflow)
            {
                SetSubtractiveWorkflow(carved, true);
                SetUV0(Box(carved, new float3(1, 1, 1), new float3(3, 2, 3)), mirror);
            } else
            {
                Box(carved, new float3(0, 0, 0), new float3(4, 3, 4));
                SetUV0(Box(carved, new float3(1, 1, 1), new float3(3, 2, 3), CSGOperationType.Subtractive), mirror);
            }
            var mirrored = MirroredAgainst(Drawn(carved), Drawn(outside), out var compared);
            Assert.That(compared, Is.EqualTo(12), "the room's six walls, two triangles each");
            Assert.That(mirrored, Is.Zero, $"{mirrored} of {compared} triangles of the room don't read as the mirrored box does from outside");
        }

        // A face's tangent runs the way its u does and its bitangent the way its v does, so normal maps light it the
        // right way: on the walls of a room as on the outside of a solid
        [Test]
        public void ARoom_TangentsFollowItsTexture([Values] bool subtractiveWorkflow)
        {
            var model = Model();
            if (subtractiveWorkflow)
            {
                SetSubtractiveWorkflow(model, true);
                Box(model, new float3(1, 1, 1), new float3(3, 2, 3));
            } else
            {
                Box(model, new float3(0, 0, 0), new float3(4, 3, 4));
                Box(model, new float3(1, 1, 1), new float3(3, 2, 3), CSGOperationType.Subtractive);
            }
            var checkedCorners = 0;
            foreach (var triangle in Drawn(model))
            {
                var middle = (triangle.a.position + triangle.b.position + triangle.c.position) / 3;
                if (math.any(middle < 1 - 1e-3f) || math.any(middle > new float3(3, 2, 3) + 1e-3f))
                    continue;   // the outside of the solid
                Assert.That(UVGradients(triangle, out var gradientU, out var gradientV), Is.True);
                foreach (var corner in new[] { triangle.a, triangle.b, triangle.c })
                {
                    var tangent   = corner.tangent.xyz;
                    var bitangent = math.cross(corner.normal, tangent) * corner.tangent.w;
                    Assert.That(math.dot(tangent, gradientU), Is.GreaterThan(0), $"the tangent at {(Vector3)corner.position} of the wall at {(Vector3)middle} runs against u");
                    Assert.That(math.dot(bitangent, gradientV), Is.GreaterThan(0), $"the bitangent at {(Vector3)corner.position} of the wall at {(Vector3)middle} runs against v");
                    checkedCorners++;
                }
            }
            Assert.That(checkedCorners, Is.EqualTo(36), "the room's twelve triangles");
        }

        public enum Floor { OfASolid, OfACarvedRoom, OfASubtractiveWorkflowRoom }

        // A floor at y = 0 from (0, 0) to (4, 4), facing up: the top of a solid, or the floor of a room 3 high
        ChiselModelComponent FloorModel(Floor floor)
        {
            var model = Model();
            switch (floor)
            {
                case Floor.OfASolid:
                    Box(model, new float3(0, -1, 0), new float3(4, 0, 4));
                    break;
                case Floor.OfACarvedRoom:
                    Box(model, new float3(-1, -1, -1), new float3(5, 4, 5));
                    Box(model, new float3(0, 0, 0), new float3(4, 3, 4), CSGOperationType.Subtractive);
                    break;
                case Floor.OfASubtractiveWorkflowRoom:
                    SetSubtractiveWorkflow(model, true);
                    Box(model, new float3(0, 0, 0), new float3(4, 3, 4));
                    break;
            }
            return model;
        }

        // A 1 x 1 opaque decal above the floor's middle, looking down; an omni-directional one reaches half a metre
        Material Decal(ChiselModelComponent model, ChiselDecalProjection projection)
        {
            var material = new Material(Shader.Find("Standard")) { name = "decal" };
            assets.Add(material);
            var go = new GameObject("decal");
            go.transform.SetParent(model.transform, false);
            go.transform.localPosition = new Vector3(2, 0.2f, 2);
            go.transform.localRotation = Quaternion.LookRotation(Vector3.down, Vector3.forward);
            var decal = go.AddComponent<ChiselDecalComponent>();
            decal.Material    = material;
            decal.Transparent = false;
            var settings = decal.Settings;
            settings.projection = projection;
            decal.Settings = settings;
            decal.Size = new Vector3(1, 1, 1);
            Update();
            return material;
        }

        static double AreaFacingUp(List<Triangle> triangles)
        {
            double area = 0;
            foreach (var triangle in triangles)
            {
                var normal = triangle.WindingNormal;
                if (math.normalizesafe(normal).y > 0.99f)
                    area += math.length(normal) * 0.5;
            }
            return area;
        }

        // How the decal's image lies on the floor: the same handedness on every triangle of it, 0 when they differ
        static int DecalHandedness(List<Triangle> triangles)
        {
            var handedness = 0;
            foreach (var triangle in triangles)
            {
                var h = TextureHandedness(triangle);
                if (h == 0)
                    continue;
                if (handedness == 0)
                    handedness = h;
                else if (h != handedness)
                    return 0;
            }
            return handedness;
        }

        // A decal on the floor of a room, seen from inside, is its whole image, and reads as on the top of a solid
        [Test]
        public void ADecalOnARoomsFloor_IsItsWholeImage_NotMirrored([Values(ChiselDecalProjection.Orthographic, ChiselDecalProjection.Omni)] ChiselDecalProjection projection,
                                                                    [Values(Floor.OfACarvedRoom, Floor.OfASubtractiveWorkflowRoom)] Floor floor)
        {
            var solid = FloorModel(Floor.OfASolid);
            var onSolid = Drawn(solid, Decal(solid, projection));
            var expected = DecalHandedness(onSolid);
            Assume.That(AreaFacingUp(onSolid), Is.EqualTo(1).Within(1e-3), "on the solid");
            Assume.That(expected, Is.Not.Zero, "on the solid");

            var room = FloorModel(floor);
            var inRoom = Drawn(room, Decal(room, projection));
            Assert.That(AreaFacingUp(inRoom), Is.EqualTo(1).Within(1e-3), "the whole image on the floor");
            Assert.That(DecalHandedness(inRoom), Is.EqualTo(expected), "read as on the solid (0: some of it mirrored)");
        }

        // An omni-directional decal lies with its image's right along the floor's texture, as the floor is seen
        [Test]
        public void AnOmniDecalOnAFloor_RunsAlongItsTexture([Values] Floor floor)
        {
            var model = FloorModel(floor);
            var material = Decal(model, ChiselDecalProjection.Omni);
            var decalU = float3.zero;
            foreach (var triangle in Drawn(model, material))
            {
                if (UVGradients(triangle, out var gradientU, out _))
                    decalU += math.normalizesafe(gradientU);
            }
            var floorU = float3.zero;
            foreach (var triangle in Drawn(model, material, decal: false))
            {
                if (math.normalizesafe(triangle.WindingNormal).y > 0.99f && math.abs(triangle.a.position.y) < 1e-4f &&
                    UVGradients(triangle, out var gradientU, out _))
                    floorU += math.normalizesafe(gradientU);
            }
            Assert.That(math.lengthsq(decalU), Is.GreaterThan(0), "the decal is drawn");
            Assert.That(math.lengthsq(floorU), Is.GreaterThan(0), "the floor is drawn");
            Assert.That(math.dot(math.normalize(decalU), math.normalize(floorU)), Is.GreaterThan(0.999f),
                        $"the decal's right {(Vector3)math.normalize(decalU)} isn't the floor texture's {(Vector3)math.normalize(floorU)}");
        }

        static readonly string[] kScenes = { "two overlapping rooms", "a room with a pillar", "two rooms side by side", "a room in a room" };

        static void Build(ChiselModelComponent model, string scene)
        {
            switch (scene)
            {
                case "two overlapping rooms":
                    Box(model, new float3(0, 0, 0), new float3(4, 3, 4));
                    Box(model, new float3(2, 0, 2), new float3(6, 2, 6));
                    break;
                case "a room with a pillar":
                    Box(model, new float3(0, 0, 0), new float3(6, 3, 6));
                    Box(model, new float3(2, 0, 2), new float3(3, 3, 3), CSGOperationType.Subtractive);
                    break;
                case "two rooms side by side":
                    Box(model, new float3(0, 0, 0), new float3(4, 3, 4));
                    Box(model, new float3(4, 0, 1), new float3(8, 2, 3));
                    break;
                case "a room in a room":
                    Box(model, new float3(0, 0, 0), new float3(6, 3, 6));
                    Box(model, new float3(1, 1, 1), new float3(2, 2, 2));
                    break;
            }
        }

        [TestCaseSource(nameof(kScenes))]
        public void CarvedRooms_AreDrawnTurnedRound(string scene)
        {
            var model = Model();
            Build(model, scene);
            var without = Drawn(model);
            SetSubtractiveWorkflow(model, true);
            AssertTurnedRound(without, Drawn(model), scene);
        }

        // Every face of a room faces into it, and its normals do too
        [Test]
        public void ARoom_FacesInward()
        {
            var model = Model();
            SetSubtractiveWorkflow(model, true);
            Box(model, new float3(-2, -1, -3), new float3(2, 1, 3));
            var triangles = Drawn(model);
            Assert.That(triangles.Count, Is.EqualTo(12), "a box's six faces, two triangles each");
            foreach (var triangle in triangles)
            {
                var middle = (triangle.a.position + triangle.b.position + triangle.c.position) / 3;
                Assert.That(math.dot(triangle.WindingNormal, -middle), Is.GreaterThan(0), $"the triangle at {(Vector3)middle} faces out");
                foreach (var corner in new[] { triangle.a, triangle.b, triangle.c })
                    Assert.That(math.dot(corner.normal, -middle), Is.GreaterThan(0), $"the normal at {(Vector3)corner.position} of the face at {(Vector3)middle} points out");
            }
        }

        // Turning the setting on and off again draws what was drawn before, and is noticed each time
        [Test]
        public void TurningTheSettingOnAndOff_RebuildsTheModel()
        {
            var model = Model();
            Build(model, "a room with a pillar");
            var before = Drawn(model);
            SetSubtractiveWorkflow(model, true);
            var on = Drawn(model);
            AssertTurnedRound(before, on, "turned on");
            SetSubtractiveWorkflow(model, false);
            AssertTurnedRound(on, Drawn(model), "turned off again");
        }
    }
}
