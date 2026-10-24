using System.Collections.Generic;
using Chisel.Core;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;

namespace Chisel.Components.Tests
{
    // Hotspot mapping through the whole pipeline: brushes whose material has a hotspot layout take their uvs from the
    // tile that fits what they draw
    [TestFixture]
    public class ChiselHotspotMappingTests
    {
        const string kFolder = "Assets/__ChiselHotspotMappingTests";

        readonly List<GameObject> roots = new();
        ChiselHotspotLayout   layout;
        Material              material;
        ChiselSurfaceMetadata metadata;

        static HotspotTile Rect(int id, string name, int x, int y, int w, int h, HotspotTileFlags flags = HotspotTileFlags.None)
        {
            return new HotspotTile { id = id, name = name, flags = flags,
                                     points = new[] { new Vector2Int(x, y), new Vector2Int(x + w, y), new Vector2Int(x + w, y + h), new Vector2Int(x, y + h) } };
        }

        [SetUp]
        public void SetUp()
        {
            if (Object.FindObjectsByType<ChiselModelComponent>(FindObjectsInactive.Include).Length > 0)
                Assert.Ignore("A Chisel model is loaded; run these in a scene without one");

            layout = ScriptableObject.CreateInstance<ChiselHotspotLayout>();
            layout.size = new Vector2Int(2048, 2048);
            layout.pixelsPerUnit = 256;
            layout.tiles = new List<HotspotTile>
            {
                Rect(1, "A1", 0, 0, 512, 512),
                Rect(2, "A2", 512, 0, 512, 512),
                Rect(3, "B1", 1024, 0, 1024, 512),
                Rect(4, "C1", 0, 512, 1024, 256),
                Rect(5, "D1", 1024, 512, 256, 256),
                Rect(6, "E1", 1280, 512, 256, 512),
                Rect(7, "G1", 1536, 512, 128, 128),
                new HotspotTile { id = 10, name = "Tr6", points = new[] { new Vector2Int(1792, 1024), new Vector2Int(1792, 1152), new Vector2Int(1536, 1152) } },
                Rect(11, "R1", 1536, 1536, 512, 512, HotspotTileFlags.Radial),
            };

            if (!AssetDatabase.IsValidFolder(kFolder))
                AssetDatabase.CreateFolder("Assets", kFolder.Substring("Assets/".Length));
            material = new Material(ChiselProjectSettings.DefaultWallMaterial);
            AssetDatabase.CreateAsset(material, kFolder + "/hotspot.mat");
            metadata = material.AddMetadataOfType<ChiselSurfaceMetadata>();
            metadata.hotspotLayout = layout;
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
            AssetDatabase.DeleteAsset(kFolder);
            if (layout)
                Object.DestroyImmediate(layout);
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
            var root = new GameObject("hotspot test");
            roots.Add(root);
            return root.AddComponent<ChiselModelComponent>();
        }

        T Add<T>(ChiselModelComponent model, string name) where T : ChiselNodeComponent
        {
            var go = new GameObject(name);
            go.transform.SetParent(model.transform, false);
            return go.AddComponent<T>();
        }

        // Every surface with the hotspot material; settings can change each surface's details
        void UseHotspotMaterial(ChiselGeneratorComponent generator, System.Func<SurfaceDetails, SurfaceDetails> settings = null)
        {
            Update();
            var surfaces = generator.SurfaceDefinition.surfaces;
            Assert.That(surfaces, Is.Not.Null.And.Not.Empty, generator.name + " has no surfaces");
            foreach (var surface in surfaces)
            {
                surface.SetMaterial(material);
                if (settings != null)
                    surface.surfaceDetails = settings(surface.surfaceDetails);
            }
            generator.OnValidate();
            Update();
        }

        ChiselBoxComponent Box(ChiselModelComponent model, float3 min, float3 max, System.Func<SurfaceDetails, SurfaceDetails> settings = null)
        {
            var box = Add<ChiselBoxComponent>(model, "box");
            // a Min beyond the old Max would be put right first
            if (math.any(min > box.Max)) { box.Max = max; box.Min = min; }
            else                         { box.Min = min; box.Max = max; }
            UseHotspotMaterial(box, settings);
            return box;
        }

        // The uv of the drawn vertex at a position on a face with this normal
        static float2 UVAt(ChiselModelComponent model, float3 position, float3 normal)
        {
            foreach (var renderable in model.generated.renderables)
            {
                if (renderable == null || renderable.sharedMesh == null)
                    continue;
                var mesh = renderable.sharedMesh;
                var vertices = mesh.vertices; var normals = mesh.normals; var uvs = mesh.uv;
                for (int i = 0; i < vertices.Length; i++)
                {
                    if (math.distance((float3)vertices[i], position) < 1e-4f && math.dot((float3)normals[i], normal) > 0.999f)
                        return uvs[i];
                }
            }
            Assert.Fail($"no vertex at {position} with normal {normal}");
            return default;
        }

        // One of the uvs drawn at a point (where the border and the interior's repeats meet, each has its own)
        static void AssertHasUV(float2 expected, List<float2> uvs, string what)
        {
            Assert.That(uvs.Exists(uv => math.distance(uv, expected) < 1e-5f), Is.True, $"{what}: expected {expected} among {string.Join(", ", uvs)}");
        }

        static void AreEqual(float2 expected, float2 actual, string what)
        {
            Assert.That(math.distance(expected, actual), Is.LessThan(1e-5f), $"{what}: expected {expected}, got {actual}");
        }

        static readonly float3 kFront = new float3(0, 0, -1);

        [Test]
        public void AWall4x2_TakesThe2To1Tile_UpStaysUp()
        {
            var model = Model();
            Box(model, new float3(-2, 0, -0.125f), new float3(2, 2, 0.125f));
            // B1: pixels 1024..2048 x 0..512 from the top: u 0.5..1, v 0.75..1
            AreEqual(new float2(0.5f, 0.75f), UVAt(model, new float3(-2, 0, -0.125f), kFront), "bottom left");
            AreEqual(new float2(1.0f, 0.75f), UVAt(model, new float3(2, 0, -0.125f), kFront), "bottom right");
            AreEqual(new float2(0.5f, 1.0f),  UVAt(model, new float3(-2, 2, -0.125f), kFront), "top left");
            AreEqual(new float2(1.0f, 1.0f),  UVAt(model, new float3(2, 2, -0.125f), kFront), "top right");
        }

        [Test]
        public void AFloor_TakesTheTallTile_UpAlongPlusZ()
        {
            var model = Model();
            Box(model, new float3(-1, -0.25f, -2), new float3(1, 0, 2));
            // its top, 2 x 4, takes E1 (1:2): pixels 1280..1536 x 512..1024 from the top: u 0.625..0.75, v 0.5..0.75
            var up = new float3(0, 1, 0);
            AreEqual(new float2(0.625f, 0.5f),  UVAt(model, new float3(-1, 0, -2), up), "near left");
            AreEqual(new float2(0.75f, 0.5f),   UVAt(model, new float3(1, 0, -2), up), "near right");
            AreEqual(new float2(0.625f, 0.75f), UVAt(model, new float3(-1, 0, 2), up), "far left");
        }

        [Test]
        public void TheVisiblePart_Decides_AWallHalfSunkIntoABlock()
        {
            var model = Model();
            Box(model, new float3(-2, 0, -0.125f), new float3(2, 4, 0.125f));
            Box(model, new float3(-3, -1, -1), new float3(3, 2, 1));
            // the wall's face is 4 x 4, but only 4 x 2 of it shows: B1, not a square
            AreEqual(new float2(0.5f, 0.75f), UVAt(model, new float3(-2, 2, -0.125f), kFront), "bottom left of what shows");
            AreEqual(new float2(1.0f, 1.0f),  UVAt(model, new float3(2, 4, -0.125f), kFront), "top right");
        }

        [Test]
        public void ATriangularFace_TakesATriangleTile_CornerToCorner()
        {
            var model = Model();
            var brush = Add<ChiselBrushComponent>(model, "right triangle");
            static float4 Plane(float3 n, float3 p) { n = math.normalize(n); return new float4(n, -math.dot(n, p)); }
            // a prism whose front is the right triangle (0,0) (1,0) (1,0.5), its right angle at the bottom right
            brush.definition.SetPlanes(new[] { new float4(0, 0, -1, 0), new float4(0, 0, 1, -0.5f),
                                                Plane(new float3(0, -1, 0), float3.zero), Plane(new float3(1, 0, 0), new float3(1, 0, 0)),
                                                Plane(new float3(-0.5f, 1, 0), float3.zero) });
            brush.OnValidate();
            UseHotspotMaterial(brush);
            // Tr6: (1792, 1024) top right, (1792, 1152) bottom right - the right angle -, (1536, 1152) bottom left, from the top
            AreEqual(new float2(1536f / 2048, 896f / 2048), UVAt(model, new float3(0, 0, 0), kFront), "the sharp corner");
            AreEqual(new float2(1792f / 2048, 896f / 2048), UVAt(model, new float3(1, 0, 0), kFront), "the right angle");
            AreEqual(new float2(1792f / 2048, 1024f / 2048), UVAt(model, new float3(1, 0.5f, 0), kFront), "the top");
        }

        [Test]
        public void IdenticalWalls_TakeBothIdenticalTiles_ByTheirSeeds()
        {
            var model = Model();
            for (uint i = 0; i < 8; i++)
            {
                var x = i * 3.0f;
                var seed = i + 1;
                Box(model, new float3(x, 0, -0.125f), new float3(x + 2, 2, 0.125f), d => { d.hotspot.seed = seed; return d; });
            }
            var lefts = new HashSet<float>();
            var seen  = "";
            var wrong = false;
            for (uint i = 0; i < 8; i++)
            {
                var low  = UVAt(model, new float3(i * 3.0f, 0, -0.125f), kFront);
                var high = UVAt(model, new float3(i * 3.0f + 2, 2, -0.125f), kFront);
                seen += $" wall {i}: {low} .. {high};";
                wrong |= !(math.abs(low.x) < 1e-5f || math.abs(low.x - 0.25f) < 1e-5f) || math.abs(high.y - 1) > 1e-5f;
                lefts.Add(math.round(low.x * 4));
            }
            Assert.That(wrong, Is.False, "a wall is not on A1 or A2:" + seen);
            Assert.That(lefts.Count, Is.EqualTo(2), "eight seeds took only one of the two identical tiles:" + seen);
        }

        [Test]
        public void APinnedTile_IsKept()
        {
            var model = Model();
            Box(model, new float3(-1, 0, -0.125f), new float3(1, 2, 0.125f), d => { d.hotspot.pinnedTile = 4; return d; });
            // C1: pixels 0..1024 x 512..768 from the top: u 0..0.5, v 0.625..0.75
            AreEqual(new float2(0.0f, 0.625f), UVAt(model, new float3(-1, 0, -0.125f), kFront), "bottom left");
            AreEqual(new float2(0.5f, 0.75f),  UVAt(model, new float3(1, 2, -0.125f), kFront), "top right");
        }

        [Test]
        public void AResizedWall_IsMatchedAgain()
        {
            var model = Model();
            var box = Box(model, new float3(-1, 0, -0.125f), new float3(1, 2, 0.125f), d => { d.hotspot.seed = 1; return d; });
            var square = UVAt(model, new float3(-1, 0, -0.125f), kFront);
            Assert.That(square.y, Is.EqualTo(0.75f).Within(1e-5f), "a 2 x 2 wall takes A1 or A2");
            box.Max = new float3(3, 2, 0.125f);
            Update();
            AreEqual(new float2(0.5f, 0.75f), UVAt(model, new float3(-1, 0, -0.125f), kFront), "4 x 2 now: B1");
            AreEqual(new float2(1.0f, 1.0f),  UVAt(model, new float3(3, 2, -0.125f), kFront), "4 x 2 now: B1");
        }

        [Test]
        public void AWallOnABorderedTile_KeepsTheBorderAtItsPixelWidth()
        {
            var b1 = layout.tiles[2];
            b1.border = 32;
            layout.tiles[2] = b1;
            var model = Model();
            Box(model, new float3(-3, 0, -0.125f), new float3(3, 2, 0.125f));
            // 6 x 2 takes B1 (1024..2048 x 0..512 from the top: u 0.5..1, v 0.75..1); its border is 32 pixels, an eighth of a unit
            AreEqual(new float2(0.5f, 0.75f), UVAt(model, new float3(-3, 0, -0.125f), kFront), "bottom left");
            AreEqual(new float2((1024 + 32) / 2048f, (1536 + 32) / 2048f), UVAt(model, new float3(-3 + 0.125f, 0.125f, -0.125f), kFront), "the inner corner at the bottom left");
            // the interior repeats at the tile's density: 960 pixels (3.75 units) a repeat, the second one cut off 2 units in
            AssertHasUV(new float2((1056 + 512) / 2048f, (2048 - 32) / 2048f), UVsAt(model, new float3(3 - 0.125f, 2 - 0.125f, -0.125f), kFront), "the interior at the inner corner at the top right");
            AreEqual(new float2(1, 1), UVAt(model, new float3(3, 2, -0.125f), kFront), "top right");
            // where the first repeat ends on the bottom border, both repeats
            var seam = UVsAt(model, new float3(-3 + 0.125f + 3.75f, 0.125f, -0.125f), kFront);
            Assert.That(seam.Count, Is.GreaterThanOrEqualTo(2), "a vertex on each side of the seam");
            Assert.That(seam.Exists(uv => math.abs(uv.x - (2048 - 32) / 2048f) < 1e-5f) && seam.Exists(uv => math.abs(uv.x - (1024 + 32) / 2048f) < 1e-5f), Is.True,
                        $"the first repeat's end and the second's start: {string.Join(", ", seam)}");
        }

        void BorderOnB1(int border)
        {
            var b1 = layout.tiles[2];
            b1.border = border;
            layout.tiles[2] = b1;
        }

        // A brush placed (moved, turned, scaled) as the editor tells Chisel of it
        static void Place(ChiselNodeComponent node, Vector3 position, Quaternion rotation, Vector3 scale)
        {
            node.transform.localPosition = position;
            node.transform.localRotation = rotation;
            node.transform.localScale    = scale;
            ChiselNodeHierarchyManager.UpdateTreeNodeTransformation(node);
            Update();
        }

        // Where the drawn vertices along a line of the front are, for a failure's message
        static string XsAt(ChiselModelComponent model, float y)
        {
            var xs = new List<float>();
            foreach (var (position, _) in Drawn(model, kFront))
            {
                if (math.abs(position.y - y) < 1e-4f && !xs.Exists(x => math.abs(x - position.x) < 1e-4f))
                    xs.Add(position.x);
            }
            xs.Sort();
            var ys = new List<float>();
            foreach (var (position, _) in Drawn(model, kFront))
            {
                if (!ys.Exists(other => math.abs(other - position.y) < 1e-4f))
                    ys.Add(position.y);
            }
            ys.Sort();
            return "x " + string.Join(" ", xs) + " (front vertices' y: " + string.Join(" ", ys) + ")";
        }

        static SurfaceDetails InWorld(SurfaceDetails details)
        {
            details.detailFlags |= SurfaceDetailFlags.TextureIsInWorldSpace;
            return details;
        }

        // Every drawn vertex facing this way: its position and uv
        static List<(float3 position, float2 uv)> Drawn(ChiselModelComponent model, float3 normal)
        {
            var drawn = new List<(float3, float2)>();
            foreach (var renderable in model.generated.renderables)
            {
                if (renderable == null || renderable.sharedMesh == null)
                    continue;
                var mesh = renderable.sharedMesh;
                var vertices = mesh.vertices; var normals = mesh.normals; var uvs = mesh.uv;
                for (int i = 0; i < vertices.Length; i++)
                {
                    if (math.dot((float3)normals[i], normal) > 0.999f)
                        drawn.Add((vertices[i], uvs[i]));
                }
            }
            return drawn;
        }

        // A generator's surfaces, each polygon of its brush
        static List<SurfaceReference> SurfacesOf(ChiselGeneratorComponent generator)
        {
            var surfaces = new List<SurfaceReference>();
            var brush = (CSGTreeBrush)generator.TopTreeNode;
            var blob = BrushMeshManager.GetBrushMeshBlob(brush.BrushMesh.BrushMeshID);
            for (int i = 0; i < blob.Value.polygons.Length; i++)
                surfaces.Add(new SurfaceReference(generator, blob.Value.polygons[i].descriptionIndex, brush, i));
            return surfaces;
        }

        // Lock texture to object toggled on every surface, each surface definition once, as the editor does
        static void ToggleLock(ChiselGeneratorComponent generator)
        {
            var done = new HashSet<int>();
            foreach (var surface in SurfacesOf(generator))
            {
                if (!done.Add(surface.descriptionIndex))
                    continue;
                surface.SurfaceDetails = ChiselTextureLock.Toggled(surface, surface.SurfaceDetails);
            }
            generator.OnValidate();
            Update();
        }

        // Every vertex drawn before is drawn with the same uv
        static void AssertSameLook(List<(float3 position, float2 uv)> before, ChiselModelComponent model, float3 normal, string what)
        {
            Assert.That(before.Count, Is.GreaterThan(0), what + ": nothing drawn");
            foreach (var (position, uv) in before)
            {
                var now = UVsAt(model, position, normal);
                Assert.That(now.Exists(other => math.distance(other, uv) < 1e-4f), Is.True, $"{what}: at {position} {uv} became {string.Join(", ", now)}");
            }
        }

        [Test]
        public void ALockedWall_ScaledHalf_LooksAsItDoesUnscaled()
        {
            BorderOnB1(32);
            var model = Model();
            var box = Box(model, new float3(-2, 0, -0.125f), new float3(2, 2, 0.125f));
            Place(box, Vector3.zero, Quaternion.identity, new Vector3(0.5f, 0.5f, 0.5f));
            // the texture scales with the brush: its 32 pixel border is an eighth of the brush's own unit, a sixteenth here
            AreEqual(new float2(0.5f, 0.75f), UVAt(model, new float3(-1, 0, -0.0625f), kFront), "bottom left");
            AreEqual(new float2((1024 + 32) / 2048f, (1536 + 32) / 2048f), UVAt(model, new float3(-1 + 0.0625f, 0.0625f, -0.0625f), kFront), "the inner corner");
            AreEqual(new float2(1, 1), UVAt(model, new float3(1, 1, -0.0625f), kFront), "top right");
        }

        [Test]
        public void AnUnlockedWall_KeepsItsInteriorRepeatsInTheWorld_ALockedOneMovesThem()
        {
            BorderOnB1(32);
            // a 6 x 2 wall takes B1: its interior repeats every 3.75 units (960 pixels); in the world on the world's grid,
            // a seam at x = 0 wherever the wall is
            var world = Model();
            var unlocked = Box(world, new float3(-3, 0, -0.125f), new float3(3, 2, 0.125f), InWorld);
            Assert.That(UVsAt(world, new float3(0, 0.125f, -0.125f), kFront).Count, Is.GreaterThanOrEqualTo(2), "a seam at 0: " + XsAt(world, 0.125f));
            Place(unlocked, new Vector3(1, 0, 0), Quaternion.identity, Vector3.one);
            Assert.That(UVsAt(world, new float3(0, 0.125f, -0.125f), kFront).Count, Is.GreaterThanOrEqualTo(2), "still at 0 when the wall moved: " + XsAt(world, 0.125f));

            // locked, from the wall's inner corner: -3 + 0.125 + 3.75, and along with it
            var own = Model();
            var locked = Box(own, new float3(-3, 0, -0.125f), new float3(3, 2, 0.125f));
            Assert.That(UVsAt(own, new float3(0.875f, 0.125f, -0.125f), kFront).Count, Is.GreaterThanOrEqualTo(2), "a seam at 0.875: " + XsAt(own, 0.125f));
            Place(locked, new Vector3(1, 0, 0), Quaternion.identity, Vector3.one);
            Assert.That(UVsAt(own, new float3(1.875f, 0.125f, -0.125f), kFront).Count, Is.GreaterThanOrEqualTo(2), "moved along to 1.875: " + XsAt(own, 0.125f));
        }

        ChiselBoxComponent PlainBox(ChiselModelComponent model, float x, bool inWorld)
        {
            var box = Add<ChiselBoxComponent>(model, "plain");
            box.Max = new float3(2, 2, 0.125f);
            box.Min = new float3(-2, 0, -0.125f);
            Update();
            foreach (var surface in box.SurfaceDefinition.surfaces)
            {
                var details = surface.surfaceDetails;
                details.UV0 = UVMatrix.TRS(new Vector2(0.25f, 0.5f), Vector3.forward, 30, new float2(0.5f, 2));
                surface.surfaceDetails = inWorld ? InWorld(details) : details;
            }
            box.OnValidate();
            Update();
            Place(box, new Vector3(x, 0, 0), Quaternion.identity, Vector3.one);
            return box;
        }

        [Test]
        public void AnUnlockedPlainWall_TakesItsUVsFromTheWorld_ALockedOneFromItself()
        {
            // two walls side by side, the right one's left edge on the left one's right edge (at x = 2)
            var a = Model(); PlainBox(a, 0, true);
            var b = Model(); PlainBox(b, 4, true);
            AreEqual(UVAt(a, new float3(2, 0, -0.125f), kFront), UVAt(b, new float3(2, 0, -0.125f), kFront), "in the world, the same point the same uv");
            var c = Model(); PlainBox(c, 0, false);
            var d = Model(); PlainBox(d, 4, false);
            AreEqual(UVAt(c, new float3(-2, 0, -0.125f), kFront), UVAt(d, new float3(2, 0, -0.125f), kFront), "locked, the same corner of each the same uv");
        }

        [Test]
        public void TogglingTheLock_ChangesNothingOnScreen()
        {
            BorderOnB1(32);
            var model = Model();
            // turned, scaled and moved, its interior repeating
            var hotspot = Box(model, new float3(-2, 0, -0.125f), new float3(6, 2, 0.125f), d => { d.hotspot.rotation = 90; return d; });
            Place(hotspot, new Vector3(0.3f, 0.2f, 0), Quaternion.Euler(0, 0, 20), new Vector3(0.75f, 0.75f, 0.75f));
            var front = math.normalize((float3)(hotspot.transform.localRotation * Vector3.back));
            var before = Drawn(model, front);
            ToggleLock(hotspot);
            Assert.That(ChiselTextureLock.IsInWorldSpace(hotspot.SurfaceDefinition.surfaces[0].surfaceDetails), Is.True);
            AssertSameLook(before, model, front, "hotspot, to the world");
            ToggleLock(hotspot);
            AssertSameLook(before, model, front, "hotspot, back to the object");

            // a plain material's uv matrix
            var plainModel = Model();
            var plain = PlainBox(plainModel, 0.3f, false);
            Place(plain, new Vector3(0.3f, 0.2f, 0), Quaternion.Euler(0, 0, 20), Vector3.one);
            var plainFront = math.normalize((float3)(plain.transform.localRotation * Vector3.back));
            var plainBefore = Drawn(plainModel, plainFront);
            ToggleLock(plain);
            AssertSameLook(plainBefore, plainModel, plainFront, "plain, to the world");
        }

        // Every uv drawn at a position on a face with this normal
        static List<float2> UVsAt(ChiselModelComponent model, float3 position, float3 normal)
        {
            var found = new List<float2>();
            foreach (var renderable in model.generated.renderables)
            {
                if (renderable == null || renderable.sharedMesh == null)
                    continue;
                var mesh = renderable.sharedMesh;
                var vertices = mesh.vertices; var normals = mesh.normals; var uvs = mesh.uv;
                for (int i = 0; i < vertices.Length; i++)
                {
                    if (math.distance((float3)vertices[i], position) < 1e-4f && math.dot((float3)normals[i], normal) > 0.999f)
                        found.Add(uvs[i]);
                }
            }
            return found;
        }

        [Test]
        public void ABaseboard_RepeatsAStripTile_WithASeamBetweenRepeats()
        {
            // 2048 x 128 pixels, repeating across: 8 x 0.5 units
            layout.tiles.Add(Rect(11, "T1", 0, 1536, 2048, 128, HotspotTileFlags.RepeatU));
            var model = Model();
            Box(model, new float3(0, 0, -0.125f), new float3(40, 0.5f, 0.125f));
            // T1 is pixels 1536..1664 from the top: v 0.1875 .. 0.25
            AreEqual(new float2(0, 0.1875f), UVAt(model, new float3(0, 0, -0.125f), kFront), "the start");
            AreEqual(new float2(1, 0.25f),   UVAt(model, new float3(40, 0.5f, -0.125f), kFront), "the end of the fifth repeat");
            var seam = UVsAt(model, new float3(8, 0, -0.125f), kFront);
            Assert.That(seam.Count, Is.EqualTo(2), "the seam between the first two repeats: a vertex for each side");
            Assert.That(seam.Exists(uv => math.distance(uv, new float2(1, 0.1875f)) < 1e-5f), Is.True, $"the end of the first: {string.Join(", ", seam)}");
            Assert.That(seam.Exists(uv => math.distance(uv, new float2(0, 0.1875f)) < 1e-5f), Is.True, $"the start of the second: {string.Join(", ", seam)}");
        }

        [Test]
        public void ABaseboardMovedAlong_StartsPartWayAlongItsTile()
        {
            layout.tiles.Add(Rect(11, "T1", 0, 1536, 2048, 128, HotspotTileFlags.RepeatU));
            var model = Model();
            Box(model, new float3(0, 0, -0.125f), new float3(40, 0.5f, 0.125f), d => { d.hotspot.offset = new float2(0.5f, 0); return d; });
            // T1 is pixels 1536..1664 from the top: v 0.1875 .. 0.25; half a repeat along, the seams are 4 units in
            AreEqual(new float2(0.5f, 0.1875f), UVAt(model, new float3(0, 0, -0.125f), kFront), "the start");
            var seam = UVsAt(model, new float3(4, 0, -0.125f), kFront);
            Assert.That(seam.Exists(uv => math.distance(uv, new float2(1, 0.1875f)) < 1e-5f), Is.True, $"the end of the first: {string.Join(", ", seam)}");
            Assert.That(seam.Exists(uv => math.distance(uv, new float2(0, 0.1875f)) < 1e-5f), Is.True, $"the start of the second: {string.Join(", ", seam)}");
        }

        static int ColliderTriangles(ChiselModelComponent model)
        {
            var count = 0;
            foreach (var collider in model.generated.colliders)
            {
                if (collider != null && collider.sharedMesh != null)
                    count += collider.sharedMesh.triangles.Length / 3;
            }
            return count;
        }

        static int DrawnTriangles(ChiselModelComponent model)
        {
            var count = 0;
            foreach (var renderable in model.generated.renderables)
            {
                if (renderable != null && renderable.sharedMesh != null)
                    count += renderable.sharedMesh.triangles.Length / 3;
            }
            return count;
        }

        [Test]
        public void ABorderedWall_IsCutForDrawing_ItsColliderIsNot()
        {
            var b1 = layout.tiles[2];
            b1.border = 32;
            layout.tiles[2] = b1;
            var model = Model();
            Box(model, new float3(-3, 0, -0.125f), new float3(3, 2, 0.125f));
            // a box: 6 faces of 2 triangles
            Assert.That(ColliderTriangles(model), Is.EqualTo(12), "the collider is cut");
            Assert.That(DrawnTriangles(model), Is.GreaterThan(12), "what is drawn is not cut");
            // cut once along the border: each face's sections are two triangles each, or fewer
            Assert.That(DrawnTriangles(model), Is.LessThanOrEqualTo(6 * 18), "cut more than the border needs");
            AreEqual(new float2((1024 + 32) / 2048f, (1536 + 32) / 2048f), UVAt(model, new float3(-3 + 0.125f, 0.125f, -0.125f), kFront), "the inner corner");
        }

        [Test]
        public void ADoorwayCutByTheCSG_HasABorderAlongItsEdges()
        {
            var b1 = layout.tiles[2];
            b1.border = 32;
            layout.tiles[2] = b1;
            var model = Model();
            Box(model, new float3(-3, 0, -0.125f), new float3(3, 2, 0.125f));
            var door = Box(model, new float3(-0.5f, -0.1f, -1), new float3(0.5f, 1.5f, 1));
            door.Operation = CSGOperationType.Subtractive;
            Update();
            // 6 x 2 takes B1; where the doorway's left edge meets the floor, 0.125 to its left: the tile's bottom right
            // corner piece, the border's whole depth in from the right
            var uvs = UVsAt(model, new float3(-0.5f - 0.125f, 0, -0.125f), kFront);
            Assert.That(uvs.Exists(uv => math.distance(uv, new float2((2048 - 32) / 2048f, 0.75f)) < 1e-5f), Is.True,
                        $"no corner piece of the doorway's border there: {string.Join(", ", uvs)}");
            Assert.That(ColliderTriangles(model), Is.LessThan(DrawnTriangles(model)), "only what is drawn is cut");
        }

        [Test]
        public void ADoorwaysInsideCorners_TakeTheLinkedPieces()
        {
            var b1 = layout.tiles[2];
            b1.border = 32;
            b1.insideCorners = 20;
            layout.tiles[2] = b1;
            // four pieces, 32 pixels each, at the sheet's left (pixels 0..64 x 1024..1088 from the top): their middle at
            // u 32 / 2048, v 992 / 2048
            layout.tiles.Add(Rect(20, "IC1", 0, 1024, 64, 64, HotspotTileFlags.InsideCorners));
            var model = Model();
            Box(model, new float3(-3, 0, -0.125f), new float3(3, 2, 0.125f));
            var door = Box(model, new float3(-0.5f, -0.1f, -1), new float3(0.5f, 1.5f, 1));
            door.Operation = CSGOperationType.Subtractive;
            Update();
            var middle = new float2(32, 992) / 2048f;
            // the doorway's top corners: each one's square lies above it, to the left and to the right
            AssertHasUV(middle, UVsAt(model, new float3(-0.5f, 1.5f, -0.125f), kFront), "the left corner, the pieces' middle");
            AssertHasUV(new float2(0, 1024) / 2048f, UVsAt(model, new float3(-0.625f, 1.625f, -0.125f), kFront), "the top left piece's far corner");
            AssertHasUV(middle, UVsAt(model, new float3(0.5f, 1.5f, -0.125f), kFront), "the right corner, the pieces' middle");
            AssertHasUV(new float2(64, 1024) / 2048f, UVsAt(model, new float3(0.625f, 1.625f, -0.125f), kFront), "the top right piece's far corner");
        }

        [Test]
        public void ATrapezoid_TakesItsTileAsItsBoundsDo()
        {
            var model = Model();
            var brush = Add<ChiselBrushComponent>(model, "trapezoid");
            static float4 Plane(float3 n, float3 p) { n = math.normalize(n); return new float4(n, -math.dot(n, p)); }
            // a prism whose front is the trapezoid (0, 0) (4, 0) (3, 2) (1, 2)
            brush.definition.SetPlanes(new[] { new float4(0, 0, -1, 0), new float4(0, 0, 1, -0.5f),
                                                Plane(new float3(0, -1, 0), float3.zero), Plane(new float3(0, 1, 0), new float3(0, 2, 0)),
                                                Plane(new float3(-2, 1, 0), float3.zero), Plane(new float3(2, 1, 0), new float3(4, 0, 0)) });
            brush.OnValidate();
            UseHotspotMaterial(brush);
            // its bounds are 4 x 2: B1, u 0.5..1, v 0.75..1, undistorted; its slanted sides cut the tile's top corners off
            AreEqual(new float2(0.5f, 0.75f),   UVAt(model, new float3(0, 0, 0), kFront), "bottom left");
            AreEqual(new float2(1.0f, 0.75f),   UVAt(model, new float3(4, 0, 0), kFront), "bottom right");
            AreEqual(new float2(0.875f, 1.0f),  UVAt(model, new float3(3, 2, 0), kFront), "top right");
            AreEqual(new float2(0.625f, 1.0f),  UVAt(model, new float3(1, 2, 0), kFront), "top left");
        }

        [Test]
        public void AWallTurnedAQuarter_TakesTheTallTile_TurnedWithIt()
        {
            var model = Model();
            Box(model, new float3(-2, 0, -0.125f), new float3(2, 2, 0.125f), d => { d.hotspot.rotation = 90; return d; });
            // 4 x 2 turned a quarter is 2 x 4: E1, pixels 1280..1536 x 512..1024 from the top: u 0.625..0.75, v 0.5..0.75;
            // the texture's bottom left is on the wall's bottom right
            AreEqual(new float2(0.625f, 0.5f),  UVAt(model, new float3(2, 0, -0.125f), kFront), "bottom right");
            AreEqual(new float2(0.625f, 0.75f), UVAt(model, new float3(-2, 0, -0.125f), kFront), "bottom left");
            AreEqual(new float2(0.75f, 0.5f),   UVAt(model, new float3(2, 2, -0.125f), kFront), "top right");
        }

        [Test]
        public void AWallOnATurnedBrush_KeepsItsTile_TurnedWithIt()
        {
            var model = Model();
            var box = Box(model, new float3(-2, 0, -0.125f), new float3(2, 2, 0.125f));
            box.transform.localRotation = Quaternion.Euler(0, 0, 90);
            ChiselNodeHierarchyManager.UpdateTreeNodeTransformation(box);
            Update();
            // still B1 (u 0.5..1, v 0.75..1), its bottom now along the wall's left edge: (x, y) went to (-y, x)
            AreEqual(new float2(0.5f, 0.75f), UVAt(model, new float3(0, -2, -0.125f), kFront), "bottom left");
            AreEqual(new float2(1.0f, 0.75f), UVAt(model, new float3(0, 2, -0.125f), kFront), "bottom right");
            AreEqual(new float2(0.5f, 1.0f),  UVAt(model, new float3(-2, -2, -0.125f), kFront), "top left");
        }

        [Test]
        public void AFloorOnATurnedBrush_KeepsItsTile_TurnedWithIt()
        {
            var model = Model();
            var box = Box(model, new float3(-1, -0.25f, -2), new float3(1, 0, 2));
            box.transform.localRotation = Quaternion.Euler(0, 90, 0);
            ChiselNodeHierarchyManager.UpdateTreeNodeTransformation(box);
            Update();
            // still E1 (u 0.625..0.75, v 0.5..0.75): (x, y, z) went to (z, y, -x)
            var up = new float3(0, 1, 0);
            AreEqual(new float2(0.625f, 0.5f),  UVAt(model, new float3(-2, 0, 1), up), "near left");
            AreEqual(new float2(0.75f, 0.5f),   UVAt(model, new float3(-2, 0, -1), up), "near right");
            AreEqual(new float2(0.625f, 0.75f), UVAt(model, new float3(2, 0, 1), up), "far left");
        }

        [Test]
        public void AWallOnAMovedBrush_KeepsItsUVs()
        {
            var model = Model();
            var box = Box(model, new float3(-2, 0, -0.125f), new float3(2, 2, 0.125f));
            box.transform.localPosition = new Vector3(0.3f, 1.7f, 0);
            ChiselNodeHierarchyManager.UpdateTreeNodeTransformation(box);
            Update();
            AreEqual(new float2(0.5f, 0.75f), UVAt(model, new float3(-1.7f, 1.7f, -0.125f), kFront), "bottom left");
            AreEqual(new float2(1.0f, 1.0f),  UVAt(model, new float3(2.3f, 3.7f, -0.125f), kFront), "top right");
        }

        [TestCase(typeof(ChiselSphereComponent))]
        [TestCase(typeof(ChiselCapsuleComponent))]
        public void ThePoleOfARoundGenerator_TakesOneRadialTile(System.Type type)
        {
            var model = Model();
            var go = new GameObject("round");
            go.transform.SetParent(model.transform, false);
            var generator = (ChiselGeneratorComponent)go.AddComponent(type);
            UseHotspotMaterial(generator);
            // the faces round the top pole: every vertex near the top drawn from the radial tile R1, as one circle
            var top = float.NegativeInfinity;
            foreach (var renderable in model.generated.renderables)
                if (renderable != null && renderable.sharedMesh != null)
                    foreach (var v in renderable.sharedMesh.vertices) top = math.max(top, v.y);
            var count = 0;
            foreach (var renderable in model.generated.renderables)
            {
                if (renderable == null || renderable.sharedMesh == null)
                    continue;
                var mesh = renderable.sharedMesh;
                var vertices = mesh.vertices; var normals = mesh.normals; var uvs = mesh.uv;
                for (int i = 0; i < vertices.Length; i++)
                {
                    if (normals[i].y < 0.9f || vertices[i].y < top - 1e-4f)
                        continue;
                    // the pole: the middle of R1's circle
                    Assert.That(math.distance(new float2(0.875f, 0.125f), uvs[i]), Is.LessThan(1e-4f), $"the pole: {uvs[i]}");
                    count++;
                }
            }
            Assert.That(count, Is.GreaterThanOrEqualTo(HotspotMatcher.kRadialCorners), "a vertex at the pole for each face round it");
        }

        [Test]
        public void TwoCoplanarBoxFaces_TakeOneTileAcrossBoth()
        {
            var model = Model();
            Box(model, new float3(-2, 0, -0.125f), new float3(0, 2, 0.125f));
            Box(model, new float3(0, 0, -0.125f), new float3(2, 2, 0.125f));
            // each 2 x 2 front alone would take a square tile; together they are 4 x 2: B1, u 0.5..1, v 0.75..1
            AreEqual(new float2(0.5f, 0.75f),  UVAt(model, new float3(-2, 0, -0.125f), kFront), "bottom left");
            AreEqual(new float2(1.0f, 1.0f),   UVAt(model, new float3(2, 2, -0.125f), kFront), "top right");
            foreach (var uv in UVsAt(model, new float3(0, 0, -0.125f), kFront))
                AreEqual(new float2(0.75f, 0.75f), uv, "where they meet, both");
            foreach (var uv in UVsAt(model, new float3(0, 2, -0.125f), kFront))
                AreEqual(new float2(0.75f, 1.0f), uv, "where they meet at the top, both");
        }

        [Test]
        public void TwoCoplanarBoxFaces_HaveNoBorderWhereTheyMeet()
        {
            var b1 = layout.tiles[2];
            b1.border = 32;
            layout.tiles[2] = b1;
            var model = Model();
            Box(model, new float3(-3, 0, -0.125f), new float3(0, 2, 0.125f));
            Box(model, new float3(0, 0, -0.125f), new float3(3, 2, 0.125f));
            // as one 6 x 2 wall on B1 with its 32 pixel border: the border along the outside only
            AreEqual(new float2((1024 + 32) / 2048f, (1536 + 32) / 2048f), UVAt(model, new float3(-3 + 0.125f, 0.125f, -0.125f), kFront), "the inner corner at the bottom left");
            AssertHasUV(new float2((1056 + 512) / 2048f, (2048 - 32) / 2048f), UVsAt(model, new float3(3 - 0.125f, 2 - 0.125f, -0.125f), kFront), "the interior at the inner corner at the top right");
            // where they meet on the bottom border's inner edge: the border's strip as along one wall (the tile's middle),
            // the interior 2.875 units into its first repeat, the same for both boxes
            var interior = new float2((1056 + 736) / 2048f, (1536 + 32) / 2048f);
            var border   = new float2(0.75f, (1536 + 32) / 2048f);
            var meet     = UVsAt(model, new float3(0, 0.125f, -0.125f), kFront);
            AssertHasUV(interior, meet, "where they meet, the interior");
            AssertHasUV(border, meet, "where they meet, the border");
            foreach (var uv in meet)
                Assert.That(math.distance(uv, interior) < 1e-5f || math.distance(uv, border) < 1e-5f, Is.True, $"where they meet: {uv} is neither");
        }

        [Test]
        public void CoplanarFacesWithDifferentFrames_AreNotConnected()
        {
            var model = Model();
            // in one 32 m chunk, so the left one's corner is found first where they meet
            Box(model, new float3(2, 0, -0.125f), new float3(4, 2, 0.125f));
            Box(model, new float3(4, 0, -0.125f), new float3(6, 2, 0.125f), d => { d.hotspot.rotation = 90; return d; });
            // the left one alone: a square tile from (2, 0) to (4, 2)
            var bottomLeft = UVAt(model, new float3(2, 0, -0.125f), kFront);
            var topRight   = UVAt(model, new float3(4, 2, -0.125f), kFront);
            Assert.That(math.abs((topRight - bottomLeft).x * 2048 - (topRight - bottomLeft).y * 2048), Is.LessThan(1e-2f), $"square: {bottomLeft} .. {topRight}");
        }

        // The face of a generator's first brush toward -z (its corners' middle the furthest toward -z)
        static SurfaceReference FrontOf(ChiselGeneratorComponent generator)
        {
            var brush = (CSGTreeBrush)generator.TopTreeNode;
            var blob = BrushMeshManager.GetBrushMeshBlob(brush.BrushMesh);
            ref var mesh = ref blob.Value;
            var best = -1; var bestZ = float.PositiveInfinity;
            for (int p = 0; p < mesh.polygons.Length; p++)
            {
                var z = 0f;
                for (int e = 0; e < mesh.polygons[p].edgeCount; e++)
                    z += mesh.localVertices[mesh.halfEdges[mesh.polygons[p].firstEdge + e].vertexIndex].z;
                z /= mesh.polygons[p].edgeCount;
                if (z < bestZ) { bestZ = z; best = p; }
            }
            return new SurfaceReference(generator, mesh.polygons[best].descriptionIndex, brush, best);
        }

        [Test]
        public void TwoCoplanarBoxFacesInDifferentFrames_ConnectedByHand_TakeOneTile()
        {
            var model = Model();
            var left  = Box(model, new float3(-2, 0, -0.125f), new float3(0, 2, 0.125f));
            var right = Box(model, new float3(0, 0, -0.125f), new float3(2, 2, 0.125f), d => { d.hotspot.rotation = 90; return d; });
            Assert.That(ChiselHotspotLinks.Connect(FrontOf(left), FrontOf(right)), Is.True, "connected");
            Assert.That(ChiselHotspotLinks.StateOf(FrontOf(left), FrontOf(right)), Is.EqualTo(ChiselHotspotLinkState.Connected));
            // as the diagnostic view finds it
            var links = ChiselHotspotLinks.LinksOf(model);
            Assert.That(links.Count, Is.EqualTo(1), "one link");
            Assert.That(ChiselHotspotLinks.Resolve(links[0], out var brushA, out var brushB), Is.True, "its brushes");
            Assert.That(brushA == (CSGTreeBrush)left.TopTreeNode && brushB == (CSGTreeBrush)right.TopTreeNode, Is.True, "the boxes' brushes");
            Update();
            // 4 x 2 together on the first one's frame: B1, u 0.5..1, v 0.75..1
            AreEqual(new float2(0.5f, 0.75f), UVAt(model, new float3(-2, 0, -0.125f), kFront), "bottom left");
            AreEqual(new float2(1.0f, 1.0f),  UVAt(model, new float3(2, 2, -0.125f), kFront), "top right");
        }

        // Which render buffer the last update left the brush (a rebuilt brush gets a new one), by where it is
        static int RenderBufferOf(ChiselModelComponent model, ChiselGeneratorComponent generator)
        {
            var data = ChiselTreeLookup.Value[model.Node];
            data.lastJobHandle.Complete();
            var brush = (CSGTreeBrush)generator.TopTreeNode;
            Assert.That(data.brushRenderBufferLookup.TryGetValue(CompactHierarchyManager.GetCompactNodeID(brush), out var buffer) && buffer.IsCreated, Is.True, generator.name + " has no render buffer");
            return buffer.GetHashCode();
        }

        [Test]
        public void AnEditedLayout_RebuildsOnlyTheBrushesDrawnOnIt()
        {
            var model = Model();
            var mapped = Box(model, new float3(-2, 0, -0.125f), new float3(2, 2, 0.125f));
            var plain  = Add<ChiselBoxComponent>(model, "plain");
            // Max first: a Min beyond the old Max would be put right
            plain.Max = new float3(8, 2, 0.125f); plain.Min = new float3(6, 0, -0.125f);
            Update();
            var mappedBefore = RenderBufferOf(model, mapped);
            var plainBefore  = RenderBufferOf(model, plain);
            // nothing edited: nothing rebuilt
            layout.NotifyChanged();
            Update();
            Assert.That(RenderBufferOf(model, mapped) == mappedBefore, Is.True, "an unchanged layout rebuilds nothing");
            Assert.That(RenderBufferOf(model, plain) == plainBefore, Is.True, "an unchanged layout rebuilds nothing, the plain brush");
            BorderOnB1(32);
            layout.NotifyChanged();
            Update();
            Assert.That(RenderBufferOf(model, mapped) == mappedBefore, Is.False, "the brush on the layout is mapped again");
            Assert.That(RenderBufferOf(model, plain) == plainBefore, Is.True, "the brush without it is kept");
        }

        // The hotspot group each of a brush's surfaces was mapped with (0: by itself)
        static int[] GroupsOf(ChiselModelComponent model, ChiselGeneratorComponent generator)
        {
            var data = ChiselTreeLookup.Value[model.Node];
            data.lastJobHandle.Complete();
            Assert.That(data.brushRenderBufferLookup.TryGetValue(CompactHierarchyManager.GetCompactNodeID((CSGTreeBrush)generator.TopTreeNode), out var buffer) && buffer.IsCreated, Is.True);
            ref var surfaces = ref buffer.Value.surfaces;
            var groups = new int[surfaces.Length];
            for (int s = 0; s < surfaces.Length; s++)
                groups[s] = surfaces[s].hotspot.group;
            return groups;
        }

        [Test]
        public void ASliver_IsMappedWithItsNeighbour()
        {
            // a sheet 2 mm thick: its four sides are narrower than a texel (256 a unit), its front and back are not (and
            // its sides are wider than the weld collapses, OutputModelWeld.kCollapseLength)
            var model = Model();
            var sheet = Box(model, new float3(-1, 0, -0.001f), new float3(1, 2, 0.001f));
            var groups = GroupsOf(model, sheet);
            Assert.That(groups.Length, Is.EqualTo(6));
            // the sides tie (each shares a 2 unit edge with the front and the back) and go with the first of the two; the
            // other stays by itself: a sliver never joins two surfaces that show a tile
            var alone = 0; var with = new HashSet<int>();
            foreach (var group in groups)
            {
                if (group == 0) alone++;
                else with.Add(group);
            }
            Assert.That(alone, Is.EqualTo(1), "front or back by itself: " + string.Join(", ", groups));
            Assert.That(with.Count, Is.EqualTo(1), "the sides with the other: " + string.Join(", ", groups));

            // a step edge 2 cm wide (5 texels) is no sliver: mapped by itself
            var model2 = Model();
            var slab = Box(model2, new float3(-1, 0, -0.01f), new float3(1, 2, 0.01f));
            foreach (var group in GroupsOf(model2, slab))
                Assert.That(group, Is.Zero, "a 2 cm side is mapped by itself");
        }

        // The materials the model draws its triangles facing this way with, and the uvs of their corners
        static (HashSet<Material> materials, List<Vector2> uvs) Facing(ChiselModelComponent model, Vector3 normal)
        {
            var materials = new HashSet<Material>(); var uvs = new List<Vector2>();
            foreach (var r in model.generated.renderables)
            {
                if (r == null || !r.Valid || r.sharedMesh == null || r.sharedMesh.vertexCount == 0 || r.renderMaterials == null)
                    continue;
                var vertices = r.sharedMesh.vertices; var uv = r.sharedMesh.uv;
                for (int s = 0; s < r.sharedMesh.subMeshCount && s < r.renderMaterials.Length; s++)
                {
                    var triangles = r.sharedMesh.GetTriangles(s);
                    for (int t = 0; t < triangles.Length; t += 3)
                    {
                        var n = Vector3.Cross(vertices[triangles[t + 1]] - vertices[triangles[t]], vertices[triangles[t + 2]] - vertices[triangles[t]]).normalized;
                        if (Vector3.Dot(n, normal) < 0.99f)
                            continue;
                        materials.Add(r.renderMaterials[s]);
                        for (int k = 0; k < 3; k++) uvs.Add(uv[triangles[t + k]]);
                    }
                }
            }
            return (materials, uvs);
        }

        [Test]
        public void AnotherSheet_GivesItsTilesAndItsMaterial()
        {
            // a trim sheet: one strip 8 pixels high along the top of a 2048 texture, repeating at its exact size, and turning
            var trimLayout = ScriptableObject.CreateInstance<ChiselHotspotLayout>();
            trimLayout.size = new Vector2Int(2048, 2048);
            trimLayout.pixelsPerUnit = 256;
            trimLayout.tiles = new List<HotspotTile> { Rect(1, "trim", 0, 0, 2048, 8, HotspotTileFlags.RepeatU | HotspotTileFlags.PartialRepeats | HotspotTileFlags.AllowRotation) };
            var trim = new Material(ChiselProjectSettings.DefaultWallMaterial);
            AssetDatabase.CreateAsset(trim, kFolder + "/trim.mat");
            trim.AddMetadataOfType<ChiselSurfaceMetadata>().hotspotLayout = trimLayout;
            metadata.hotspotSheets = new List<Material> { trim };
            try
            {
                // a slab 2 cm thick: its edges take the trim (the trim's material), its front a tile of the sheet's own
                var model = Model();
                Box(model, new float3(-0.5f, 0, -0.01f), new float3(0.5f, 1, 0.01f));
                var top = Facing(model, Vector3.up);
                Assert.That(top.materials, Is.EquivalentTo(new[] { trim }), "the top edge's material");
                foreach (var uv in top.uvs)
                    Assert.That(uv.y, Is.GreaterThanOrEqualTo(1 - 8 / 2048f - 1e-5f), $"the top edge on the strip: {uv}");
                Assert.That(Facing(model, Vector3.right).materials, Is.EquivalentTo(new[] { trim }), "the side edge, the strip turned");
                Assert.That(Facing(model, Vector3.back).materials, Is.EquivalentTo(new[] { material }), "the front keeps the sheet's own");

                // tile 1 of each sheet, pinned: the same id, told apart by the sheet
                var trimPinned = Model();
                Box(trimPinned, new float3(-0.5f, 0, -0.01f), new float3(0.5f, 1, 0.01f), d => { d.hotspot.pinnedTile = 1; d.hotspot.pinnedSheet = 1; return d; });
                Assert.That(Facing(trimPinned, Vector3.back).materials, Is.EquivalentTo(new[] { trim }), "pinned to the trim sheet's tile 1");
                var ownPinned = Model();
                Box(ownPinned, new float3(-0.5f, 0, -0.01f), new float3(0.5f, 1, 0.01f), d => { d.hotspot.pinnedTile = 1; d.hotspot.pinnedSheet = 0; return d; });
                var front = Facing(ownPinned, Vector3.back);
                Assert.That(front.materials, Is.EquivalentTo(new[] { material }), "pinned to the sheet's own tile 1");
                // A1: pixels 0..512 x 0..512 from the top: u 0..0.25, v 0.75..1
                foreach (var uv in front.uvs)
                    Assert.That(uv.x <= 0.25f + 1e-5f && uv.y >= 0.75f - 1e-5f, Is.True, $"on A1: {uv}");
            }
            finally
            {
                metadata.hotspotSheets.Clear();
                Object.DestroyImmediate(trimLayout);
            }
        }

        [Test]
        public void AGroupOnTheLayoutsOwnTile_TakesTheLayoutsOwnMaterialBack()
        {
            // a trim sheet; a baseboard's end cap alone takes a strip of it, but together with the end of the wall it
            // stands against (coplanar, touching: one group) it is mapped on a tile of the sheet's own
            var trimLayout = ScriptableObject.CreateInstance<ChiselHotspotLayout>();
            trimLayout.size = new Vector2Int(1024, 64);
            trimLayout.pixelsPerUnit = 256;
            trimLayout.tiles = new List<HotspotTile> { Rect(1, "TR4", 0, 32, 1024, 32, HotspotTileFlags.RepeatU | HotspotTileFlags.PartialRepeats | HotspotTileFlags.AllowRotation) };
            var trim = new Material(ChiselProjectSettings.DefaultWallMaterial);
            AssetDatabase.CreateAsset(trim, kFolder + "/trim.mat");
            trim.AddMetadataOfType<ChiselSurfaceMetadata>().hotspotLayout = trimLayout;
            metadata.hotspotSheets = new List<Material> { trim };
            try
            {
                var model = Model();
                Box(model, new float3(0, 0, 0.125f), new float3(20, 3, 0.375f));
                Box(model, new float3(0, 0, -0.125f), new float3(20, 0.2f, 0.125f));
                var caps = Facing(model, Vector3.left);
                Assert.That(caps.materials.Count, Is.EqualTo(1), "one group, one material: " + string.Join(", ", caps.materials));
                if (caps.materials.Contains(material))
                {
                    // on the sheet's own tiles: uvs of a 2048 sheet, not squeezed into the trim's 64 pixels
                    var lo = new Vector2(float.MaxValue, float.MaxValue); var hi = -lo;
                    foreach (var uv in caps.uvs) { lo = Vector2.Min(lo, uv); hi = Vector2.Max(hi, uv); }
                    Assert.That((hi - lo).y * 2048, Is.GreaterThan(64), $"the group's uvs: {lo} .. {hi}");
                } else
                {
                    // on the trim: every uv on its strip (the bottom half of the trim's texture)
                    foreach (var uv in caps.uvs)
                        Assert.That(uv.y, Is.LessThanOrEqualTo(0.5f + 1e-5f), $"a trim uv off the strip: {uv}; all (px) " + string.Join(" ", caps.uvs.ConvertAll(u => (u * new Vector2(1024, 64)).ToString("F0"))));
                }
            }
            finally
            {
                metadata.hotspotSheets.Clear();
                Object.DestroyImmediate(trimLayout);
            }
        }

        [Test]
        public void TheSavedOutputsHash_FollowsTheHotspotInputs()
        {
            var model = Model();
            var left  = Box(model, new float3(-2, 0, -0.125f), new float3(0, 2, 0.125f));
            var right = Box(model, new float3(0, 0, -0.125f), new float3(2, 2, 0.125f));
            Update();
            var first = ChiselModelManager.GetComponentInputHash(model);
            var built = ChiselModelManager.GetInputHash(model);
            Assert.That(first.isValid, Is.True);
            // a tile's name is no input
            var a1 = layout.tiles[0]; a1.name = "renamed"; layout.tiles[0] = a1;
            Assert.That(ChiselModelManager.GetComponentInputHash(model), Is.EqualTo(first), "a name");
            // its border is
            BorderOnB1(32);
            var bordered = ChiselModelManager.GetComponentInputHash(model);
            Assert.That(bordered, Is.Not.EqualTo(first), "a tile's border");
            Assert.That(ChiselModelManager.GetInputHash(model), Is.Not.EqualTo(built), "a tile's border, the built tree's hash");
            layout.interiorPadding = 1;
            Assert.That(ChiselModelManager.GetComponentInputHash(model), Is.Not.EqualTo(bordered), "the layout's interior padding");
            // and so are the surfaces connected by hand
            var padded = ChiselModelManager.GetComponentInputHash(model);
            Assert.That(ChiselHotspotLinks.Disconnect(FrontOf(left), FrontOf(right)), Is.True);
            Assert.That(ChiselModelManager.GetComponentInputHash(model), Is.Not.EqualTo(padded), "a link");
        }

        [Test]
        public void TwoCoplanarBoxFaces_DisconnectedByHand_TakeATileEach()
        {
            var model = Model();
            // in one 32 m chunk, so the left one's corner is found first where they meet
            var left  = Box(model, new float3(2, 0, -0.125f), new float3(4, 2, 0.125f));
            var right = Box(model, new float3(4, 0, -0.125f), new float3(6, 2, 0.125f));
            Assert.That(ChiselHotspotLinks.Disconnect(FrontOf(left), FrontOf(right)), Is.True, "disconnected");
            Update();
            // the left one alone: 2 x 2 on a square tile
            var bottomLeft = UVAt(model, new float3(2, 0, -0.125f), kFront);
            var topRight   = UVAt(model, new float3(4, 2, -0.125f), kFront);
            Assert.That(math.abs((topRight - bottomLeft).x - (topRight - bottomLeft).y), Is.LessThan(1e-5f), $"square: {bottomLeft} .. {topRight}");
            // and back to what they do by themselves
            Assert.That(ChiselHotspotLinks.Forget(FrontOf(left), FrontOf(right)), Is.True, "forgotten");
            Update();
            AreEqual(new float2(0.5f, 0.75f), UVAt(model, new float3(2, 0, -0.125f), kFront), "together again: bottom left");
        }

        [Test]
        public void ALayoutEdit_UndoneAndRedone_MapsTheModelsAgain()
        {
            var model = Model();
            Box(model, new float3(-2, 0, -0.125f), new float3(2, 2, 0.125f));
            Update();
            var plain = Drawn(model, kFront).Count;
            Undo.IncrementCurrentGroup();
            Undo.RecordObject(layout, "Edit Hotspot Layout");
            BorderOnB1(32);
            layout.NotifyChanged();
            Undo.IncrementCurrentGroup();
            Update();
            var bordered = Drawn(model, kFront).Count;
            Assert.That(bordered, Is.GreaterThan(plain), "the border cuts the wall's front");
            Undo.PerformUndo();
            Update();
            Assert.That(layout.tiles[2].border, Is.Zero, "the tile is as it was");
            Assert.That(Drawn(model, kFront).Count, Is.EqualTo(plain), "undone: the model is mapped as before");
            Undo.PerformRedo();
            Update();
            Assert.That(Drawn(model, kFront).Count, Is.EqualTo(bordered), "redone: mapped with the border again");
        }

        // The lightmap uv of the drawn vertex at a position on a face with this normal
        static Vector2 LightmapUVAt(ChiselModelComponent model, float3 position, float3 normal)
        {
            foreach (var renderable in model.generated.renderables)
            {
                if (renderable == null || renderable.sharedMesh == null)
                    continue;
                var mesh = renderable.sharedMesh;
                var vertices = mesh.vertices; var normals = mesh.normals; var uvs = mesh.uv2;
                for (int i = 0; i < vertices.Length && i < uvs.Length; i++)
                {
                    if (math.distance((float3)vertices[i], position) < 1e-4f && math.dot((float3)normals[i], normal) > 0.999f)
                        return uvs[i];
                }
            }
            Assert.Fail($"no vertex at {position} with normal {normal}");
            return default;
        }

        [Test]
        public void ABorderOnTheTile_CutsTheWall_ButNotItsColliderOrItsLightmap()
        {
            var model = Model();
            Box(model, new float3(-2, 0, -0.125f), new float3(2, 2, 0.125f));
            Update();
            var collider = model.generated.colliders[0];
            var marker = new Bounds(new Vector3(1000, 1000, 1000), Vector3.one);
            collider.sharedMesh.bounds = marker;
            var drawn = Drawn(model, kFront).Count;
            var corners = new[] { new float3(-2, 0, -0.125f), new float3(2, 0, -0.125f), new float3(2, 2, -0.125f), new float3(-2, 2, -0.125f) };
            var lightmapBefore = new Vector2[corners.Length];
            for (int c = 0; c < corners.Length; c++)
                lightmapBefore[c] = LightmapUVAt(model, corners[c], kFront);
            var hashes = new Dictionary<ChiselRenderObjects, uint>();
            foreach (var renderable in model.generated.renderables)
                hashes[renderable] = renderable.lightmapHashValue;

            BorderOnB1(32);
            layout.NotifyChanged();
            Update();

            Assert.That(Drawn(model, kFront).Count, Is.GreaterThan(drawn), "the border cuts what is drawn");
            Assert.That(model.generated.colliders[0], Is.SameAs(collider), "the same collider");
            Assert.That(collider.sharedMesh.bounds, Is.EqualTo(marker), "its mesh was not made again, nor baked");
            foreach (var renderable in model.generated.renderables)
            {
                if (hashes.TryGetValue(renderable, out var hash))
                    Assert.That(renderable.lightmapHashValue, Is.EqualTo(hash), renderable.container.name + ": the lightmap layout");
            }
            for (int c = 0; c < corners.Length; c++)
                Assert.That(LightmapUVAt(model, corners[c], kFront), Is.EqualTo(lightmapBefore[c]), $"the lightmap uv at {corners[c]}");
        }

        [Test]
        public void ALinkThatIsGone_MapsItsSurfacesAgain()
        {
            // an undo takes the link away without anything marking its surfaces
            var model = Model();
            var left  = Box(model, new float3(-2, 0, -0.125f), new float3(0, 2, 0.125f));
            var right = Box(model, new float3(0, 0, -0.125f), new float3(2, 2, 0.125f));
            Assert.That(ChiselHotspotLinks.Disconnect(FrontOf(left), FrontOf(right)), Is.True, "disconnected");
            Update();
            var apart = UVAt(model, new float3(-2, 0, -0.125f), kFront);
            Assert.That(math.abs(apart.x - 0.5f) + math.abs(apart.y - 0.75f), Is.GreaterThan(1e-3f), "apart, they are mapped one each");
            model.hotspotLinks.Clear();
            Update();
            AreEqual(new float2(0.5f, 0.75f), UVAt(model, new float3(-2, 0, -0.125f), kFront), "together again without a mark on the surfaces");
        }

        // The face of a generator's first brush toward +x
        static SurfaceReference RightOf(ChiselGeneratorComponent generator)
        {
            var brush = (CSGTreeBrush)generator.TopTreeNode;
            var blob = BrushMeshManager.GetBrushMeshBlob(brush.BrushMesh);
            ref var mesh = ref blob.Value;
            var best = -1; var bestX = float.NegativeInfinity;
            for (int p = 0; p < mesh.polygons.Length; p++)
            {
                var x = 0f;
                for (int e = 0; e < mesh.polygons[p].edgeCount; e++)
                    x += mesh.localVertices[mesh.halfEdges[mesh.polygons[p].firstEdge + e].vertexIndex].x;
                x /= mesh.polygons[p].edgeCount;
                if (x > bestX) { bestX = x; best = p; }
            }
            return new SurfaceReference(generator, mesh.polygons[best].descriptionIndex, brush, best);
        }

        [Test]
        public void ABoxsFrontAndSide_ConnectedByHand_WrapOneTileRoundItsCorner()
        {
            var model = Model();
            var box = Box(model, new float3(-2, 0, -0.125f), new float3(0, 2, 0.125f));
            Assert.That(ChiselHotspotLinks.Connect(FrontOf(box), RightOf(box)), Is.True, "connected");
            Update();
            // where they meet: the same uv on both faces, so the texture runs on round the corner
            foreach (var at in new[] { new float3(0, 0, -0.125f), new float3(0, 2, -0.125f) })
                AreEqual(UVAt(model, at, kFront), UVAt(model, at, new float3(1, 0, 0)), $"at {at}");
            // and the side goes on past where the front ends
            var frontLeft = UVAt(model, new float3(-2, 0, -0.125f), kFront);
            var corner    = UVAt(model, new float3(0, 0, -0.125f), kFront);
            var sideEnd   = UVAt(model, new float3(0, 0, 0.125f), new float3(1, 0, 0));
            Assert.That(math.dot(corner - frontLeft, sideEnd - corner), Is.GreaterThan(0), $"{frontLeft} -> {corner} -> {sideEnd}");
        }

        // The face of a generator's first brush toward +y
        static SurfaceReference TopOf(ChiselGeneratorComponent generator)
        {
            var brush = (CSGTreeBrush)generator.TopTreeNode;
            var blob = BrushMeshManager.GetBrushMeshBlob(brush.BrushMesh);
            ref var mesh = ref blob.Value;
            var best = -1; var bestY = float.NegativeInfinity;
            for (int p = 0; p < mesh.polygons.Length; p++)
            {
                var y = 0f;
                for (int e = 0; e < mesh.polygons[p].edgeCount; e++)
                    y += mesh.localVertices[mesh.halfEdges[mesh.polygons[p].firstEdge + e].vertexIndex].y;
                y /= mesh.polygons[p].edgeCount;
                if (y > bestY) { bestY = y; best = p; }
            }
            return new SurfaceReference(generator, mesh.polygons[best].descriptionIndex, brush, best);
        }

        [Test]
        public void TwoCoplanarBoxFaces_WithTheBorderShownByHand_HaveABorderWhereTheyMeet()
        {
            var b1 = layout.tiles[2];
            b1.border = 32;
            layout.tiles[2] = b1;
            var model = Model();
            var left  = Box(model, new float3(-3, 0, -0.125f), new float3(0, 2, 0.125f));
            var right = Box(model, new float3(0, 0, -0.125f), new float3(3, 2, 0.125f));
            Assert.That(ChiselHotspotLinks.SetBorder(FrontOf(left), FrontOf(right), ChiselHotspotBorderState.Shown), Is.True, "shown");
            Assert.That(ChiselHotspotLinks.BorderOf(FrontOf(left), FrontOf(right)), Is.EqualTo(ChiselHotspotBorderState.Shown));
            Assert.That(ChiselHotspotLinks.StateOf(FrontOf(left), FrontOf(right)), Is.EqualTo(ChiselHotspotLinkState.Automatic), "still connected by themselves");
            Update();
            // still one 6 x 2 wall on B1, but where they meet each has a side of the tile: the left one the tile's right
            // side, the right one its left side
            var meet = UVsAt(model, new float3(0, 0.125f, -0.125f), kFront);
            Assert.That(meet.Exists(uv => math.abs(uv.x - 1) < 1e-5f), Is.True, $"the tile's right side among {string.Join(", ", meet)}");
            Assert.That(meet.Exists(uv => math.abs(uv.x - 0.5f) < 1e-5f), Is.True, $"the tile's left side among {string.Join(", ", meet)}");
            AreEqual(new float2((1024 + 32) / 2048f, (1536 + 32) / 2048f), UVAt(model, new float3(-3 + 0.125f, 0.125f, -0.125f), kFront), "the inner corner at the bottom left, as before");
            // and back
            Assert.That(ChiselHotspotLinks.SetBorder(FrontOf(left), FrontOf(right), ChiselHotspotBorderState.Automatic), Is.True, "automatic");
            Assert.That(model.hotspotLinks, Is.Empty, "nothing left to keep");
            Update();
            var after = UVsAt(model, new float3(0, 0.125f, -0.125f), kFront);
            Assert.That(after.Count, Is.GreaterThan(0));
            foreach (var uv in after)
                Assert.That(math.abs(uv.x - 1) > 1e-3f && math.abs(uv.x - 0.5f) > 1e-3f, Is.True, $"no border where they meet: {uv}");
        }

        [Test]
        public void AWallOnAFloor_WithTheBorderAlongItsFootHiddenByHand_HasItsSidesRunDownToIt()
        {
            var b1 = layout.tiles[2];
            b1.border = 32;
            layout.tiles[2] = b1;
            var model = Model();
            var wall  = Box(model, new float3(-2, 0, -0.125f), new float3(2, 2, 0.125f));
            var floor = Box(model, new float3(-4, -1, -2), new float3(4, 0, 2));
            // by itself the wall's bottom left corner is the tile's
            AreEqual(new float2(0.5f, 0.75f), UVAt(model, new float3(-2, 0, -0.125f), kFront), "with its border");
            Assert.That(ChiselHotspotLinks.SetBorder(FrontOf(wall), TopOf(floor), ChiselHotspotBorderState.Hidden), Is.True, "hidden");
            Update();
            // without it the left side's strip runs down to the floor, and the tile's inside starts there
            var foot = UVsAt(model, new float3(-2, 0, -0.125f), kFront);
            Assert.That(foot.Count, Is.GreaterThan(0));
            foreach (var uv in foot)
                AreEqual(new float2(0.5f, (1536 + 32) / 2048f), uv, "the left strip at the foot");
            // the top keeps its border
            AreEqual(new float2(0.5f, 1), UVAt(model, new float3(-2, 2, -0.125f), kFront), "the top left corner");
        }

        // The edge of a surface's brush polygon whose two corners are the lowest
        static int BottomEdgeOf(SurfaceReference surface)
        {
            var best = -1; var bestY = float.PositiveInfinity;
            for (int edge = 0; ChiselHotspotLinks.EdgeOf(surface.brush, surface.surfaceIndex, edge, out var from, out var to); edge++)
            {
                var y = from.y + to.y;
                if (y < bestY) { bestY = y; best = edge; }
            }
            return best;
        }

        [Test]
        public void AWall_WithTheBorderAlongItsFootHiddenByHand_HasItsSidesRunDownToIt()
        {
            var b1 = layout.tiles[2];
            b1.border = 32;
            layout.tiles[2] = b1;
            var model = Model();
            var wall  = Box(model, new float3(-2, 0, -0.125f), new float3(2, 2, 0.125f));
            AreEqual(new float2(0.5f, 0.75f), UVAt(model, new float3(-2, 0, -0.125f), kFront), "with its border");
            var front = FrontOf(wall);
            var foot  = BottomEdgeOf(front);
            Assert.That(ChiselHotspotLinks.SetEdgeBorder(front, foot, ChiselHotspotBorderState.Hidden), Is.True, "hidden");
            Assert.That(ChiselHotspotLinks.EdgeBorderOf(front, foot), Is.EqualTo(ChiselHotspotBorderState.Hidden));
            Update();
            foreach (var uv in UVsAt(model, new float3(-2, 0, -0.125f), kFront))
                AreEqual(new float2(0.5f, (1536 + 32) / 2048f), uv, "the left strip at the foot");
            AreEqual(new float2(0.5f, 1), UVAt(model, new float3(-2, 2, -0.125f), kFront), "the top left corner");
            // and back
            Assert.That(ChiselHotspotLinks.SetEdgeBorder(front, foot, ChiselHotspotBorderState.Automatic), Is.True, "automatic");
            Assert.That(model.hotspotLinks, Is.Empty);
            Update();
            AreEqual(new float2(0.5f, 0.75f), UVAt(model, new float3(-2, 0, -0.125f), kFront), "its border again");
        }

        // How many drawn triangles face this way
        static int TrianglesFacing(ChiselModelComponent model, float3 normal)
        {
            var count = 0;
            foreach (var renderable in model.generated.renderables)
            {
                if (renderable == null || renderable.sharedMesh == null)
                    continue;
                var mesh = renderable.sharedMesh;
                var normals = mesh.normals;
                for (int sub = 0; sub < mesh.subMeshCount; sub++)
                {
                    var triangles = mesh.GetTriangles(sub);
                    for (int i = 0; i + 2 < triangles.Length; i += 3)
                        if (math.dot((float3)normals[triangles[i]], normal) > 0.999f) count++;
                }
            }
            return count;
        }

        [Test]
        public void AWallWhoseCutWouldDrawMoreThanItsBudget_IsDrawnUncut()
        {
            var b1 = layout.tiles[2];
            b1.border = 32;
            layout.tiles[2] = b1;
            var model = Model();
            Box(model, new float3(-2, 0, -0.125f), new float3(2, 2, 0.125f));
            var cut = TrianglesFacing(model, kFront);
            Assert.That(cut, Is.GreaterThan(2), "cut along its border");
            // a budget it cannot keep, with or without its interior's repeats
            layout.maxTriangles = 2;
            layout.NotifyChanged();
            Update();
            Assert.That(TrianglesFacing(model, kFront), Is.EqualTo(2), "drawn uncut");
            layout.maxTriangles = 0;
            layout.NotifyChanged();
            Update();
            Assert.That(TrianglesFacing(model, kFront), Is.EqualTo(cut), "cut again with the project's budget");
        }

        [Test]
        public void OffsetCoplanarBoxFaces_TouchingAlongPartOfAnEdge_AreConnected()
        {
            var model = Model();
            Box(model, new float3(-2, 0, -0.125f), new float3(2, 1, 0.125f));
            Box(model, new float3(0, 1, -0.125f), new float3(4, 2, 0.125f));
            // where the one's corner lies on the other's edge both are drawn with one uv
            foreach (var at in new[] { new float3(0, 1, -0.125f), new float3(2, 1, -0.125f) })
            {
                var uvs = UVsAt(model, at, kFront);
                Assert.That(uvs.Count, Is.GreaterThanOrEqualTo(2), $"vertices at {at}");
                foreach (var uv in uvs)
                    AreEqual(uvs[0], uv, $"at {at}");
            }
        }

        [Test]
        public void AMirroredWall_TakesItsTileMirrored()
        {
            var model = Model();
            Box(model, new float3(-2, 0, -0.125f), new float3(2, 2, 0.125f), d => { d.hotspot.mirror = true; return d; });
            // B1: u 0.5..1, v 0.75..1, its left on the wall's right
            AreEqual(new float2(0.5f, 0.75f), UVAt(model, new float3(2, 0, -0.125f), kFront), "bottom right");
            AreEqual(new float2(1.0f, 0.75f), UVAt(model, new float3(-2, 0, -0.125f), kFront), "bottom left");
            AreEqual(new float2(1.0f, 1.0f),  UVAt(model, new float3(-2, 2, -0.125f), kFront), "top left");
        }

        [Test]
        public void ADenserWall_HasANarrowerBorder()
        {
            var b1 = layout.tiles[2];
            b1.border = 32;
            layout.tiles[2] = b1;
            var model = Model();
            Box(model, new float3(-1.5f, 0, -0.125f), new float3(1.5f, 1, 0.125f), d => { d.hotspot.density = 2; return d; });
            // 3 x 1 at twice the tile's density looks 6 x 2: B1, its 32 pixel border a sixteenth of a unit
            AreEqual(new float2(0.5f, 0.75f), UVAt(model, new float3(-1.5f, 0, -0.125f), kFront), "bottom left");
            AreEqual(new float2((1024 + 32) / 2048f, (1536 + 32) / 2048f), UVAt(model, new float3(-1.5f + 0.0625f, 0.0625f, -0.125f), kFront), "the inner corner at the bottom left");
            AreEqual(new float2(1, 1), UVAt(model, new float3(1.5f, 1, -0.125f), kFront), "top right");
        }

        [Test]
        public void RemovingTheHotspotMaterial_PutsBackTheSurfacesOwnUVs()
        {
            var model = Model();
            var box = Add<ChiselBoxComponent>(model, "box");
            box.Max = new float3(2, 2, 0.125f);
            box.Min = new float3(-2, 0, -0.125f);
            Update();
            // a uv matrix of its own on every surface: turned, scaled and moved
            var plain = new List<Material>();
            foreach (var surface in box.SurfaceDefinition.surfaces)
            {
                plain.Add(surface.RenderMaterial);
                var details = surface.surfaceDetails;
                details.UV0 = UVMatrix.TRS(new Vector2(0.25f, 0.5f), Vector3.forward, 30, new float2(0.5f, 2));
                surface.surfaceDetails = details;
            }
            box.OnValidate();
            Update();
            var corners = new[] { new float3(-2, 0, -0.125f), new float3(2, 0, -0.125f), new float3(-2, 2, -0.125f), new float3(2, 2, -0.125f) };
            var own = new float2[corners.Length];
            for (int i = 0; i < corners.Length; i++)
                own[i] = UVAt(model, corners[i], kFront);
            var matrices = new List<UVMatrix>();
            foreach (var surface in box.SurfaceDefinition.surfaces)
                matrices.Add(surface.surfaceDetails.UV0);

            UseHotspotMaterial(box);
            AreEqual(new float2(0.5f, 0.75f), UVAt(model, corners[0], kFront), "on B1");

            var surfaces = box.SurfaceDefinition.surfaces;
            for (int s = 0; s < surfaces.Length; s++)
                surfaces[s].SetMaterial(plain[s]);
            box.OnValidate();
            Update();
            for (int s = 0; s < surfaces.Length; s++)
                Assert.That(surfaces[s].surfaceDetails.UV0, Is.EqualTo(matrices[s]), $"surface {s}'s uv matrix changed");
            for (int i = 0; i < corners.Length; i++)
                AreEqual(own[i], UVAt(model, corners[i], kFront), $"corner {corners[i]}");
        }

        [Test]
        public void AMaterialWithoutALayout_KeepsItsOwnUVs()
        {
            metadata.hotspotLayout = null;
            var model = Model();
            Box(model, new float3(-2, 0, -0.125f), new float3(2, 2, 0.125f));
            var corners = new[] { UVAt(model, new float3(-2, 0, -0.125f), kFront), UVAt(model, new float3(2, 0, -0.125f), kFront),
                                  UVAt(model, new float3(-2, 2, -0.125f), kFront), UVAt(model, new float3(2, 2, -0.125f), kFront) };
            var min = math.min(math.min(corners[0], corners[1]), math.min(corners[2], corners[3]));
            var max = math.max(math.max(corners[0], corners[1]), math.max(corners[2], corners[3]));
            // the material's own planar uvs: one per unit, so a 4 x 2 wall spans 4 one way
            Assert.That(math.cmax(max - min), Is.GreaterThan(1), $"the wall's uvs fit in one tile: {string.Join(", ", corners)}");
        }
    }
}
