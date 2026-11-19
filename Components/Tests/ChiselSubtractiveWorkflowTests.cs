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
            if (paintProfile != null)
            {
                ChiselPaintProfiles.Unregister(paintProfile);
                paintProfile = null;
            }
            if (UnityEditor.AssetDatabase.IsValidFolder(kAssetFolder))
                UnityEditor.AssetDatabase.DeleteAsset(kAssetFolder);
        }

        const string kAssetFolder = "Assets/ChiselSubtractiveWorkflowTests";
        ChiselPaintProfile paintProfile;

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
            public float4     color;
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
                var colors    = mesh.colors;
                Corner CornerOf(int i) => new()
                {
                    key      = KeyOf(positions[i]),
                    position = positions[i],
                    normal   = i < normals.Length ? (float3)normals[i] : float3.zero,
                    uv       = i < uvs.Length ? (float2)uvs[i] : float2.zero,
                    tangent  = i < tangents.Length ? (float4)tangents[i] : float4.zero,
                    color    = i < colors.Length ? (float4)(Vector4)colors[i] : new float4(1)
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

        static readonly string[] kRooms =
        {
            "a box", "a cylinder", "a sphere", "linear stairs", "a cylinder smoothed by the model's angle",
            "a double sided box", "a box with a texture twice as wide as high", "a box with a painted floor", "a box with a displaced floor",
            "a box with a hotspot material", "a box with a trim along the floor's edge"
        };

        // The surface of a box whose plane faces this way
        static int SurfaceFacing(ChiselGeneratorComponent box, float3 normal)
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

        Material AssetMaterial(string name)
        {
            if (!UnityEditor.AssetDatabase.IsValidFolder(kAssetFolder))
                UnityEditor.AssetDatabase.CreateFolder("Assets", kAssetFolder.Substring("Assets/".Length));
            var material = new Material(ChiselProjectSettings.DefaultWallMaterial);
            UnityEditor.AssetDatabase.CreateAsset(material, UnityEditor.AssetDatabase.GenerateUniqueAssetPath($"{kAssetFolder}/{name}.mat"));
            return material;
        }

        static HotspotTile Tile(int id, int x, int y, int w, int h) =>
            new() { id = id, name = $"T{id}", points = new[] { new Vector2Int(x, y), new Vector2Int(x + w, y), new Vector2Int(x + w, y + h), new Vector2Int(x, y + h) } };

        // The surface of a box whose plane faces down: a room's floor
        static int FloorOf(ChiselGeneratorComponent box)
        {
            Update();
            var brush = (CSGTreeBrush)box.TopTreeNode;
            ref var mesh = ref BrushMeshManager.GetBrushMeshBlob(brush.BrushMesh).Value;
            for (int s = 0; s < mesh.polygons.Length; s++)
            {
                if (math.normalize(mesh.localPlanes[s].xyz).y < -0.999f)
                    return mesh.polygons[s].descriptionIndex;
            }
            Assert.Fail("the box has no floor");
            return -1;
        }

        void SetMaterial(ChiselGeneratorComponent generator, Material material)
        {
            foreach (var surface in generator.SurfaceDefinition.surfaces)
                surface.SetMaterial(material);
        }

        // The same room built either way: a brush of a model with the subtractive workflow, or a subtractive brush in a solid
        ChiselModelComponent RoomModel(string room, bool subtractiveWorkflow)
        {
            var model = Model();
            if (subtractiveWorkflow)
                SetSubtractiveWorkflow(model, true);
            else
                Box(model, new float3(-6), new float3(6));
            var type = room switch
            {
                "a cylinder" or "a cylinder smoothed by the model's angle" => typeof(ChiselCylinderComponent),
                "a sphere"      => typeof(ChiselSphereComponent),
                "linear stairs" => typeof(ChiselLinearStairsComponent),
                _               => typeof(ChiselBoxComponent)
            };
            var generator = Add(model, type);
            generator.Operation = subtractiveWorkflow ? CSGOperationType.Additive : CSGOperationType.Subtractive;
            if (generator is ChiselBoxComponent box)
            {
                box.Min = new float3(-1, 0, -1);
                box.Max = new float3(1, 2, 1);
            }
            switch (room)
            {
                case "a cylinder smoothed by the model's angle":
                    ((ChiselCylinderComponent)generator).SmoothingGroup = 0;
                    model.renderSettings.normalSmoothing      = true;
                    model.renderSettings.normalSmoothingAngle = 45;
                    model.SyncModelSettingsStore();
                    break;
                case "a double sided box":
                {
                    var material = AssetMaterial("double sided");
                    material.AddMetadataOfType<ChiselSurfaceMetadata>().outputFlags = SurfaceOutputFlags.DoubleSided;
                    SetMaterial(generator, material);
                    break;
                }
                case "a box with a hotspot material":
                {
                    var layout = ScriptableObject.CreateInstance<ChiselHotspotLayout>();
                    assets.Add(layout);
                    layout.size          = new Vector2Int(1024, 1024);
                    layout.pixelsPerUnit = 256;
                    layout.tiles = new List<HotspotTile> { Tile(1, 0, 0, 512, 512), Tile(2, 512, 0, 512, 256), Tile(3, 0, 512, 1024, 512) };
                    var material = AssetMaterial("hotspot");
                    material.AddMetadataOfType<ChiselSurfaceMetadata>().hotspotLayout = layout;
                    SetMaterial(generator, material);
                    break;
                }
                case "a box with a trim along the floor's edge":
                {
                    var material = new Material(Shader.Find("Standard")) { name = "trim" };
                    assets.Add(material);
                    generator.OnValidate();
                    var go = new GameObject("Trim");
                    go.transform.SetParent(model.transform, false);
                    var trim = go.AddComponent<ChiselDecalComponent>();
                    trim.Material    = material;
                    trim.Shape       = ChiselDecalShape.Trim;
                    trim.Size        = new Vector3(1, 0.5f, 1);
                    trim.Transparent = false;
                    trim.SetTargets(new[]
                    {
                        new ChiselDecalSurfaceTarget { brush = generator, surfaceIndex = SurfaceFacing(generator, new float3(0, -1, 0)) },
                        new ChiselDecalSurfaceTarget { brush = generator, surfaceIndex = SurfaceFacing(generator, new float3(-1, 0, 0)) }
                    });
                    break;
                }
                case "a box with a texture twice as wide as high":
                {
                    var texture = new Texture2D(256, 128);
                    assets.Add(texture);
                    var material = new Material(ChiselProjectSettings.DefaultWallMaterial) { mainTexture = texture };
                    assets.Add(material);
                    ChiselTextureScale.Refresh(null);
                    SetMaterial(generator, material);
                    break;
                }
                case "a box with a painted floor":
                {
                    var material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "paint" };
                    assets.Add(material);
                    if (paintProfile == null)
                    {
                        paintProfile = new ChiselPaintProfile { shader = material.shader };
                        paintProfile.modes.Add(ChiselPaintMode.Color("Color"));
                        ChiselPaintProfiles.Register(paintProfile);
                    }
                    SetMaterial(generator, material);
                    generator.OnValidate();
                    var paint = ChiselSurfacePaint.Create(FloorOf(generator), ChiselPaintAttribute.Color, ChiselPaintComponents.XYZW, Vector4.one,
                                                          new Vector3(-1, 0, -1), Vector3.right, Vector3.forward, new Vector2Int(3, 3));
                    paint[0, 0] = new Vector4(1, 0, 0, 1);
                    paint[2, 0] = new Vector4(0, 1, 0, 1);
                    paint[1, 2] = new Vector4(0, 0, 1, 1);
                    ((ChiselBoxComponent)generator).SetSurfacePaint(paint);
                    break;
                }
                case "a box with a displaced floor":
                {
                    // a bump in its middle
                    var grid = ChiselSurfacePaint.Create(FloorOf(generator), ChiselPaintAttribute.Color, ChiselPaintComponents.X, Vector4.zero,
                                                         new Vector3(-1, 0, -1), Vector3.right, Vector3.forward, new Vector2Int(3, 3));
                    grid[1, 1] = new Vector4(0.3f, 0, 0, 0);
                    ((ChiselBoxComponent)generator).SetSurfaceDisplacement(grid);
                    break;
                }
            }
            generator.OnValidate();
            Update();
            return model;
        }

        // Inside, a room is drawn the same either way: the same triangles, wound the same way, with the same normals,
        // uvs, tangents and colors. Only the solid's outside is drawn besides, carved out of it
        [TestCaseSource(nameof(kRooms))]
        public void ARoom_IsDrawnAsTheSameRoomCarvedOutOfASolid(string room)
        {
            var carved = Drawn(RoomModel(room, subtractiveWorkflow: false));
            var inside = Drawn(RoomModel(room, subtractiveWorkflow: true));
            Assert.That(inside.Count, Is.GreaterThan(0), "the room is drawn");
            Assert.That(carved.Count - inside.Count, Is.EqualTo(12), "the carved room has the solid's six outside faces besides");
            // what is compared is there
            if (room == "a box with a painted floor")
                Assert.That(inside.Exists(t => math.any(t.a.color != new float4(1)) || math.any(t.b.color != new float4(1)) || math.any(t.c.color != new float4(1))),
                            Is.True, "the floor is painted");
            if (room == "a double sided box")
                Assert.That(inside.Count, Is.GreaterThanOrEqualTo(24), "the walls are drawn from behind too");
            if (room == "a box with a hotspot material")
                Assert.That(inside.TrueForAll(t => math.all(t.a.uv >= -1e-4f & t.a.uv <= 1 + 1e-4f)), Is.True, "the walls are mapped by the layout's tiles");
            if (room == "a box with a trim along the floor's edge")
                Assert.That(inside.Count, Is.GreaterThan(12), "the trim is drawn");
            if (room == "a box with a displaced floor")
                Assert.That(inside.Exists(t => math.abs(t.a.position.y) > 0.1f && math.abs(t.a.position.y) < 0.5f), Is.True, "the floor has its bump");
            var lookup = ByWinding(carved, reversed: false);
            foreach (var triangle in inside)
            {
                var key = WoundKey(triangle.a.key, triangle.b.key, triangle.c.key);
                Assert.That(lookup.TryGetValue(key, out var matches) && matches.Count > 0, Is.True,
                            $"{room}: the triangle {(Vector3)triangle.a.position} {(Vector3)triangle.b.position} {(Vector3)triangle.c.position} isn't drawn in the carved room");
                var match = matches[matches.Count - 1];
                matches.RemoveAt(matches.Count - 1);
                foreach (var corner in new[] { triangle.a, triangle.b, triangle.c })
                {
                    var other = CornerAt(match, corner.key);
                    var at = (Vector3)corner.position;
                    Assert.That(math.distance(corner.normal, other.normal), Is.LessThan(1e-3f), $"{room}: the normal at {at}: {(Vector3)corner.normal}, carved {(Vector3)other.normal}");
                    Assert.That(math.distance(corner.uv, other.uv), Is.LessThan(1e-3f), $"{room}: the uv at {at}: {(Vector2)corner.uv}, carved {(Vector2)other.uv}");
                    Assert.That(math.distance(corner.tangent, other.tangent), Is.LessThan(1e-3f), $"{room}: the tangent at {at}: {(Vector4)corner.tangent}, carved {(Vector4)other.tangent}");
                    Assert.That(math.distance(corner.color, other.color), Is.LessThan(1e-3f), $"{room}: the color at {at}: {(Vector4)corner.color}, carved {(Vector4)other.color}");
                }
            }
        }

        // The area of the triangles facing up at height y
        static double AreaFacingUpAt(List<Triangle> triangles, float y)
        {
            double area = 0;
            foreach (var triangle in triangles)
            {
                var normal = triangle.WindingNormal;
                if (math.normalizesafe(normal).y > 0.99f && math.abs(triangle.a.position.y - y) < 1e-3f)
                    area += math.length(normal) * 0.5;
            }
            return area;
        }

        // A shape drawn on a room's floor, made into a brush: pulled out of the floor it is a pillar, pushed into it a pit,
        // in the subtractive workflow as in a room carved out of a solid
        [Test]
        public void AShapeOnARoomsFloor_ExtrudedIntoABrush_IsAPillarOrAPit([Values(0.5f, -0.25f)] float height,
                                                                           [Values(Floor.OfACarvedRoom, Floor.OfASubtractiveWorkflowRoom)] Floor floor)
        {
            var model = FloorModel(floor);
            var boxes = model.GetComponentsInChildren<ChiselBoxComponent>();
            var room  = boxes[boxes.Length - 1];
            var material = new Material(ChiselProjectSettings.DefaultWallMaterial);
            assets.Add(material);
            var square = new[] { new Vector3(1.5f, 0, 1.5f), new Vector3(2.5f, 0, 1.5f), new Vector3(2.5f, 0, 2.5f), new Vector3(1.5f, 0, 2.5f) };
            var decal = ChiselDecalComponent.CreateOnSurface(room, FloorOf(room), material, Vector3.up, square);
            Assert.That(decal, Is.Not.Null);
            var extruded = decal.ExtrudeIntoBrush(height);
            Assert.That(extruded, Is.Not.Null);
            UnityEngine.Object.DestroyImmediate(decal.gameObject);
            Update();

            var drawn = Drawn(model);
            Assert.That(AreaFacingUpAt(drawn, height), Is.EqualTo(1).Within(1e-4), height > 0 ? "the pillar's top" : "the pit's floor");
            Assert.That(AreaFacingUpAt(drawn, 0), Is.EqualTo(16 - 1).Within(1e-4), "the floor round it");
        }

        // A ray from inside a room finds its floor: the room's brush, with the plane facing back up into the room. From below
        // the floor, its back faces left out, it finds nothing of it. Inside one of the floor's triangles, or on the edge
        // between two of them (its diagonal)
        [Test]
        public void ARoomsFloor_IsFoundFromInside_NotFromBehind([Values] Floor floor, [Values] bool onTheDiagonal)
        {
            var model = FloorModel(floor);
            var boxes = model.GetComponentsInChildren<ChiselBoxComponent>();
            var room  = boxes[boxes.Length - 1];

            var at = onTheDiagonal ? new Vector3(2, 0, 2) : new Vector3(1.3f, 0, 2.6f);
            var found = new List<ChiselIntersection>();
            Assert.That(ChiselSceneQuery.FindFirstWorldIntersection(found, at + Vector3.up, at + Vector3.down * 0.5f, ignoreBackfaced: true), Is.True,
                        "the floor is found from inside");
            var nearest = found[0];
            foreach (var intersection in found)
            {
                if (intersection.brushIntersection.surfaceIntersection.distance < nearest.brushIntersection.surfaceIntersection.distance)
                    nearest = intersection;
            }
            Assert.That(nearest.treeNode, Is.EqualTo(room), "the room's brush");
            Assert.That(nearest.worldPlaneIntersection.y, Is.EqualTo(0).Within(1e-4f), "on the floor");
            Assert.That(nearest.worldPlane.normal.y, Is.EqualTo(1).Within(1e-4f), "facing up into the room");

            found.Clear();
            ChiselSceneQuery.FindFirstWorldIntersection(found, at + Vector3.down * 0.5f, at + Vector3.up, ignoreBackfaced: true);
            Assert.That(found.Exists(intersection => intersection.treeNode == room), Is.False, "the floor isn't found from behind");
        }

        // The floor's corners, each with the uv it is drawn with
        static List<(float3 position, float2 uv)> FloorUVs(ChiselModelComponent model)
        {
            var found = new List<(float3, float2)>();
            foreach (var triangle in Drawn(model))
            {
                if (math.normalizesafe(triangle.WindingNormal).y < 0.99f || math.abs(triangle.a.position.y) > 1e-4f)
                    continue;
                foreach (var corner in new[] { triangle.a, triangle.b, triangle.c })
                    found.Add((corner.position, corner.uv));
            }
            return found;
        }

        // The uv the floor is drawn with at a point on it, from a triangle of it: the mapping is the same all over it
        static float2 FloorUVAt(List<Triangle> floor, float3 point)
        {
            var triangle = floor[0];
            Assert.That(UVGradients(triangle, out var gradientU, out var gradientV), Is.True);
            var offset = point - triangle.a.position;
            return triangle.a.uv + new float2(math.dot(gradientU, offset), math.dot(gradientV, offset));
        }

        // The texture moved or turned by the uv tools on a room's floor moves as it is seen, as on the top of a solid: what is
        // drawn at a point afterwards is what was drawn where the move takes it
        [Test]
        public void ATextureMovedOnAFloor_MovesAsItIsSeen([Values] Floor floor, [Values] bool turned)
        {
            var model = FloorModel(floor);
            var boxes = model.GetComponentsInChildren<ChiselBoxComponent>();
            var box   = boxes[boxes.Length - 1];
            var before = Drawn(model).FindAll(t => math.normalizesafe(t.WindingNormal).y > 0.99f && math.abs(t.a.position.y) < 1e-4f);
            Assert.That(before, Is.Not.Empty, "the floor is drawn");

            var brush = (CSGTreeBrush)box.TopTreeNode;
            ref var mesh = ref BrushMeshManager.GetBrushMeshBlob(brush.BrushMesh).Value;
            var surfaceIndex = -1;
            for (int s = 0; s < mesh.localPlanes.Length && surfaceIndex < 0; s++)
            {
                if (math.abs(math.normalize(mesh.localPlanes[s].xyz).y) > 0.999f && math.abs(mesh.localPlanes[s].w) < 1e-4f)
                    surfaceIndex = s;
            }
            Assert.That(surfaceIndex, Is.GreaterThanOrEqualTo(0), "the box has a surface on the floor");
            var surface = new SurfaceReference(box, mesh.polygons[surfaceIndex].descriptionIndex, brush, surfaceIndex);
            var move = turned ? Matrix4x4.Translate(new Vector3(2, 0, 2)) * Matrix4x4.Rotate(Quaternion.Euler(0, 30, 0)) * Matrix4x4.Translate(new Vector3(-2, 0, -2))
                              : Matrix4x4.Translate(new Vector3(0.3f, 0, 0.7f));
            surface.WorldSpaceTransformUV(move, surface.UV0);
            box.OnValidate();
            Update();

            var after = FloorUVs(model);
            Assert.That(after, Is.Not.Empty);
            foreach (var (position, uv) in after)
            {
                var expected = FloorUVAt(before, (float3)move.MultiplyPoint3x4(position));
                Assert.That(math.distance(uv, expected), Is.LessThan(1e-3f), $"at {(Vector3)position}: {(Vector2)uv}, what was drawn where the move takes it {(Vector2)expected}");
            }
        }

        static readonly Vector3 kRoomMiddle = new(2, 1.5f, 2);
        static readonly Vector3[] kDirections = { Vector3.left, Vector3.right, Vector3.down, Vector3.up, Vector3.back, Vector3.forward };

        // From the middle of a room, every way: its wall is hit where it is, its normal facing back into the room
        [Test]
        public void ARoomsWalls_AreHitFromInside([Values(Floor.OfACarvedRoom, Floor.OfASubtractiveWorkflowRoom)] Floor floor)
        {
            FloorModel(floor);
            Physics.SyncTransforms();
            foreach (var direction in kDirections)
            {
                Assert.That(Physics.Raycast(kRoomMiddle, direction, out var hit, 10), Is.True, $"nothing hit going {direction}");
                var expected = Vector3.Dot(direction, Vector3.one) > 0 ? Vector3.Dot(new Vector3(4, 3, 4), direction) : 0;
                Assert.That(Vector3.Dot(hit.point, direction), Is.EqualTo(expected).Within(1e-3f), $"the wall {direction} is hit at {hit.point}");
                Assert.That(Vector3.Dot(hit.normal, -direction), Is.GreaterThan(0.999f), $"the wall {direction} faces {hit.normal}");
            }
        }

        // Something moving inside a room stops at its walls and floor
        [Test]
        public void ACharacterInARoom_StopsAtItsWalls([Values(Floor.OfACarvedRoom, Floor.OfASubtractiveWorkflowRoom)] Floor floor)
        {
            var model = FloorModel(floor);
            var go = new GameObject("character");
            roots.Add(go);
            go.transform.position = kRoomMiddle;
            var character = go.AddComponent<CharacterController>();
            character.radius = 0.25f;
            character.height = 1;
            Physics.SyncTransforms();
            // it keeps its skin width away from what it touches
            var skin = character.skinWidth;
            character.Move(new Vector3(0, -5, 0));
            Assert.That(go.transform.position.y, Is.EqualTo(0.5f + skin).Within(0.02f), "stands on the floor");
            character.Move(new Vector3(10, 0, 0));
            Assert.That(go.transform.position.x, Is.EqualTo(4 - 0.25f - skin).Within(0.02f), "stops at the wall");
            character.Move(new Vector3(0, 0, -10));
            Assert.That(go.transform.position.z, Is.EqualTo(0.25f + skin).Within(0.02f), "stops at the other wall");
        }

        // How bright what the camera sees is, on average (0 to 1)
        static float Brightness(Camera camera)
        {
            const int kSize = 32;
            var target = RenderTexture.GetTemporary(kSize, kSize, 24);
            var image  = new Texture2D(kSize, kSize, TextureFormat.RGBA32, false);
            var active = RenderTexture.active;
            try
            {
                camera.targetTexture = target;
                camera.Render();
                RenderTexture.active = target;
                image.ReadPixels(new Rect(0, 0, kSize, kSize), 0, 0);
                image.Apply();
                var sum = 0f;
                foreach (var color in image.GetPixels())
                    sum += color.grayscale;
                return sum / (kSize * kSize);
            }
            finally
            {
                RenderTexture.active = active;
                camera.targetTexture = null;
                RenderTexture.ReleaseTemporary(target);
                UnityEngine.Object.DestroyImmediate(image);
            }
        }

        // How much brighter a room's wall, floor or ceiling looks, from its middle looking that way, with a lamp in the
        // middle of the room
        float LitByALamp(Vector3 looking) => LitByALamp(kRoomMiddle, kRoomMiddle, looking, LightShadows.None);

        float LitByALamp(Vector3 lampAt, Vector3 cameraAt, Vector3 looking, LightShadows shadows)
        {
            var lampObject = new GameObject("lamp");
            roots.Add(lampObject);
            lampObject.transform.position = lampAt;
            var lamp = lampObject.AddComponent<Light>();
            lamp.type      = LightType.Point;
            lamp.range     = 8;
            lamp.intensity = 3;
            lamp.shadows   = shadows;

            var camera = Looking(roots, cameraAt, looking);
            return InTheDark(() =>
            {
                var lit = Brightness(camera);
                lamp.enabled = false;
                var unlit = Brightness(camera);
                return lit - unlit;
            });
        }

        // Only the lights of the scene: none from the sky or reflections
        static T InTheDark<T>(Func<T> lit)
        {
            var ambientMode         = RenderSettings.ambientMode;
            var ambientLight        = RenderSettings.ambientLight;
            var skybox              = RenderSettings.skybox;
            var reflectionIntensity = RenderSettings.reflectionIntensity;
            try
            {
                RenderSettings.ambientMode         = UnityEngine.Rendering.AmbientMode.Flat;
                RenderSettings.ambientLight        = Color.black;
                RenderSettings.skybox              = null;
                RenderSettings.reflectionIntensity = 0;
                Physics.SyncTransforms();
                return lit();
            }
            finally
            {
                RenderSettings.ambientMode         = ambientMode;
                RenderSettings.ambientLight        = ambientLight;
                RenderSettings.skybox              = skybox;
                RenderSettings.reflectionIntensity = reflectionIntensity;
            }
        }

        static Camera Looking(List<GameObject> owner, Vector3 at, Vector3 looking)
        {
            var cameraObject = new GameObject("camera");
            owner.Add(cameraObject);
            var up = Mathf.Abs(looking.y) > 0.5f ? Vector3.forward : Vector3.up;
            cameraObject.transform.SetPositionAndRotation(at, Quaternion.LookRotation(looking, up));
            var camera = cameraObject.AddComponent<Camera>();
            camera.enabled         = false;
            camera.clearFlags      = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.black;
            camera.nearClipPlane   = 0.05f;
            camera.farClipPlane    = 20;
            camera.fieldOfView     = 60;
            return camera;
        }

        static void Lightmapped(ChiselModelComponent model)
        {
            UnityEditor.GameObjectUtility.SetStaticEditorFlags(model.gameObject, UnityEditor.StaticEditorFlags.ContributeGI);
            model.RenderSettings.receiveGI = ReceiveGI.Lightmaps;
            model.SyncModelSettingsStore();
            Update();
        }

        static MeshRenderer RendererOf(ChiselModelComponent model)
        {
            foreach (var renderable in model.generated.renderables)
            {
                if (renderable != null && renderable.Valid && renderable.HasGeometry && !renderable.debugVisualizationRenderer)
                    return renderable.meshRenderer;
            }
            Assert.Fail("the model has no renderer");
            return null;
        }

        // How bright the light probes are at a point: their constant term, averaged over red, green and blue
        static float ProbeBrightness(Vector3 at)
        {
            LightProbes.GetInterpolatedProbe(at, null, out var probe);
            return (probe[0, 0] + probe[1, 0] + probe[2, 0]) / 3;
        }

        // Bakes the lamp, and how bright the camera sees the baked light alone: with the lamp off, as before a bake a baked
        // light is drawn live
        static float BakedBrightness(Light lamp, Camera camera)
        {
            lamp.enabled = true;
            Assert.That(InTheDark(() => UnityEditor.Lightmapping.Bake()), Is.True, "baking");
            lamp.enabled = false;
            return InTheDark(() => Brightness(camera));
        }

        // A lightmapped room with a lamp baked into its middle and light probes round it: how bright its floor looks, and
        // the probes are in its middle
        (float floor, float probe) Baked(Floor floor, out ChiselModelComponent model, out Camera camera, out Light lamp)
        {
            model = FloorModel(floor);
            Lightmapped(model);
            var lampObject = new GameObject("lamp");
            roots.Add(lampObject);
            lampObject.transform.position = kRoomMiddle;
            lamp = lampObject.AddComponent<Light>();
            lamp.type             = LightType.Point;
            lamp.range            = 8;
            lamp.intensity        = 3;
            lamp.lightmapBakeType = LightmapBakeType.Baked;
            var probes = lampObject.AddComponent<LightProbeGroup>();
            probes.probePositions = new[] { new Vector3(-1, -1, -1), new Vector3(1, -1, -1), new Vector3(-1, -1, 1), new Vector3(1, -1, 1),
                                            new Vector3(-1,  1, -1), new Vector3(1,  1, -1), new Vector3(-1,  1, 1), new Vector3(1,  1, 1) };
            var view = camera = Looking(roots, new Vector3(2, 1, 2), Vector3.down);

            var baked = BakedBrightness(lamp, view);
            Assert.That(RendererOf(model).lightmapIndex, Is.GreaterThanOrEqualTo(0), $"{floor} has a lightmap");
            Assert.That(baked, Is.GreaterThan(0.05f), $"the baked lamp lights the floor of {floor}");
            var probe = ProbeBrightness(kRoomMiddle);
            Assert.That(probe, Is.GreaterThan(0), $"the light probes in {floor} are lit");
            return (baked, probe);
        }

        const string kBakeFolder = "Assets/ChiselBakeTest";

        // A lamp baked into a room lights its floor and its light probes as in the same room carved out of a solid, and
        // after the room is changed, baking again lights it again. Lighting is baked into a saved scene: one of its own,
        // in place of the test runner's, which gets an empty one back
        [Test]
        public void ABakedLamp_LightsARoomAsACarvedOne_AndAgainAfterAnEdit()
        {
            if (!UnityEditor.AssetDatabase.IsValidFolder(kBakeFolder))
                UnityEditor.AssetDatabase.CreateFolder("Assets", kBakeFolder.Substring("Assets/".Length));
            var scene = UnityEditor.SceneManagement.EditorSceneManager.NewScene(UnityEditor.SceneManagement.NewSceneSetup.EmptyScene,
                                                                                UnityEditor.SceneManagement.NewSceneMode.Single);
            Assume.That(UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene, kBakeFolder + "/bake.unity"), Is.True, "the scene is saved");
            try
            {
                BakeTwice();
            }
            finally
            {
                roots.Clear();
                UnityEditor.SceneManagement.EditorSceneManager.NewScene(UnityEditor.SceneManagement.NewSceneSetup.EmptyScene,
                                                                        UnityEditor.SceneManagement.NewSceneMode.Single);
                Update();
                UnityEditor.AssetDatabase.DeleteAsset(kBakeFolder);
            }
        }

        void BakeTwice()
        {
            var hadSettings = UnityEditor.Lightmapping.TryGetLightingSettings(out var previousSettings);
            var settings = new LightingSettings
            {
                bakedGI                = true,
                lightmapResolution     = 4,
                lightmapPadding        = 2,
                lightmapMaxSize        = 256,
                directSampleCount      = 8,
                indirectSampleCount    = 8,
                environmentSampleCount = 8,
                maxBounces             = 1,
                ao                     = false,
                directionalityMode     = LightmapsMode.NonDirectional,
                filteringMode          = LightingSettings.FilterMode.None
            };
            assets.Add(settings);
            try
            {
                UnityEditor.Lightmapping.lightingSettings = settings;
                var carved = Baked(Floor.OfACarvedRoom, out _, out _, out _);
                foreach (var root in roots)
                    UnityEngine.Object.DestroyImmediate(root);
                roots.Clear();
                Update();
                UnityEditor.Lightmapping.Clear();

                var room = Baked(Floor.OfASubtractiveWorkflowRoom, out var model, out var camera, out var lamp);
                Assert.That(room.floor, Is.EqualTo(carved.floor).Within(carved.floor * 0.15f), $"the floor: {room.floor}, in the carved room {carved.floor}");
                Assert.That(room.probe, Is.EqualTo(carved.probe).Within(carved.probe * 0.15f), $"the light probes: {room.probe}, in the carved room {carved.probe}");

                // the room made longer: its lightmap no longer fits, and baking again gives it one that does
                var box = model.GetComponentInChildren<ChiselBoxComponent>();
                box.Max = new float3(5, 3, 4);
                box.OnValidate();
                Update();
                Assert.That(RendererOf(model).lightmapIndex, Is.EqualTo(-1), "the changed room kept a lightmap that doesn't fit it");
                var again = BakedBrightness(lamp, camera);
                Assert.That(RendererOf(model).lightmapIndex, Is.GreaterThanOrEqualTo(0), "the changed room has a lightmap");
                Assert.That(again, Is.EqualTo(room.floor).Within(room.floor * 0.25f), $"the floor after baking again: {again}, before {room.floor}");
            }
            finally
            {
                UnityEditor.Lightmapping.Clear();
                if (hadSettings)
                    UnityEditor.Lightmapping.lightingSettings = previousSettings;
            }
        }

        // A lamp in a room lights its walls, floor and ceiling as it does those of the same room carved out of a solid: they
        // face it, their normals into the room
        [Test]
        public void ALampInARoom_LightsItAsInACarvedRoom()
        {
            var looking = new[] { Vector3.down, Vector3.up, Vector3.left, Vector3.forward };
            var carved  = new float[looking.Length];
            FloorModel(Floor.OfACarvedRoom);
            for (int i = 0; i < looking.Length; i++)
                carved[i] = LitByALamp(looking[i]);
            foreach (var root in roots)
                UnityEngine.Object.DestroyImmediate(root);
            roots.Clear();
            Update();

            FloorModel(Floor.OfASubtractiveWorkflowRoom);
            for (int i = 0; i < looking.Length; i++)
            {
                Assume.That(carved[i], Is.GreaterThan(0.05f), $"the lamp lights the carved room looking {looking[i]}");
                var lit = LitByALamp(looking[i]);
                Assert.That(lit, Is.EqualTo(carved[i]).Within(carved[i] * 0.02f), $"looking {looking[i]}: {lit}, in the carved room {carved[i]}");
            }
        }

        // Every triangle's lightmap coordinates (uv2), keyed by its corners wound the other way, model by model
        static Dictionary<(Vector3Int, Vector3Int, Vector3Int), Vector2[]> LightmapUVs(ChiselModelComponent model, bool reversed)
        {
            var found = new Dictionary<(Vector3Int, Vector3Int, Vector3Int), Vector2[]>();
            foreach (var renderable in model.generated.renderables)
            {
                if (renderable == null || renderable.sharedMesh == null)
                    continue;
                var mesh = renderable.sharedMesh;
                var positions = mesh.vertices;
                var uv2 = mesh.uv2;
                if (uv2.Length != positions.Length)
                    continue;
                var indices = mesh.triangles;
                for (int t = 0; t + 2 < indices.Length; t += 3)
                {
                    int a = indices[t], b = indices[t + 1], c = indices[t + 2];
                    var key = reversed ? WoundKey(KeyOf(positions[a]), KeyOf(positions[c]), KeyOf(positions[b]))
                                       : WoundKey(KeyOf(positions[a]), KeyOf(positions[b]), KeyOf(positions[c]));
                    found[key] = new[] { uv2[a], uv2[b], uv2[c] };
                }
            }
            return found;
        }

        static float Area(Vector2[] corners) => Mathf.Abs((corners[1].x - corners[0].x) * (corners[2].y - corners[0].y) -
                                                          (corners[2].x - corners[0].x) * (corners[1].y - corners[0].y)) * 0.5f;

        // A room's faces get as much of the lightmap as those of the same room carved out of a solid, inside it
        [Test]
        public void ARoomsLightmapUVs_AreAsInACarvedRoom()
        {
            var carved = Model();
            Box(carved, new float3(0, 0, 0), new float3(4, 3, 4));
            var outside = LightmapUVs(carved, reversed: true);
            Assume.That(outside.Count, Is.EqualTo(12), "the box's twelve triangles have lightmap coordinates");

            SetSubtractiveWorkflow(carved, true);
            var inside = LightmapUVs(carved, reversed: false);
            Assert.That(inside.Count, Is.EqualTo(12), "the room's twelve triangles have lightmap coordinates");
            foreach (var pair in inside)
            {
                Assert.That(outside.TryGetValue(pair.Key, out var before), Is.True, "a triangle of the room is the box's turned round");
                foreach (var uv in pair.Value)
                    Assert.That(uv.x >= 0 && uv.x <= 1 && uv.y >= 0 && uv.y <= 1, Is.True, $"{uv} is outside the lightmap");
                Assert.That(Area(pair.Value), Is.EqualTo(Area(before)).Within(Area(before) * 1e-3f), "as much of the lightmap");
            }
        }

        // A pillar between a lamp and the floor shadows it, in the subtractive workflow (a subtractive brush in the room) as
        // in a room carved out of a solid (an additive brush in it)
        [Test]
        public void APillarInARoom_ShadowsTheFloorFromALamp([Values(Floor.OfACarvedRoom, Floor.OfASubtractiveWorkflowRoom)] Floor floor)
        {
            var model = FloorModel(floor);
            Box(model, new float3(2, 0, 1.5f), new float3(2.5f, 3, 2.5f),
                floor == Floor.OfASubtractiveWorkflowRoom ? CSGOperationType.Subtractive : CSGOperationType.Additive);
            var lampAt   = new Vector3(1, 1.5f, 2);
            var cameraAt = new Vector3(3.3f, 1, 2);
            var unshadowed = LitByALamp(lampAt, cameraAt, Vector3.down, LightShadows.None);
            Assume.That(unshadowed, Is.GreaterThan(0.05f), "the lamp lights the floor without shadows");
            var shadowed = LitByALamp(lampAt, cameraAt, Vector3.down, LightShadows.Hard);
            Assert.That(shadowed, Is.LessThan(unshadowed * 0.2f), $"the pillar doesn't shadow the floor: {shadowed} with shadows, {unshadowed} without");
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
