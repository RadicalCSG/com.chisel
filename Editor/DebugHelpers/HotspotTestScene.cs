using System.Collections.Generic;
using Chisel.Components;
using Chisel.Core;
using Unity.Mathematics;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Chisel.Editors
{
    // Builds Assets/HotspotTest/HotspotTest.unity: every situation hotspot mapping has to handle, one labelled station per
    // row. The textures and layouts are read from that folder.
    static class HotspotTestScene
    {
        const string kFolder   = "Assets/HotspotTest";
        const string kScene    = kFolder + "/HotspotTest.unity";
        const float  kRowSpacing = 14;

        static Material s_Material;
        static ChiselHotspotLayout s_Layout;
        static Transform s_Model;
        static int s_Row;

        [MenuItem("Tools/Chisel/Hotspot Test Scene")]
        public static void Build() { BuildScene(); }

        // The scene built, saved and open; false when it was not
        internal static bool BuildScene()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return false;
            s_Material = CreateMaterial();
            if (s_Material == null)
                return false;

            EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            s_Model = new GameObject("Hotspot Test").AddComponent<ChiselModelComponent>().transform;
            s_Row = 0;

            AspectRatios();
            Sizes();
            Orientation();
            NonRectangularFaces();
            ClippedByCSG();
            LargerThanAnyTile();
            Variation();
            Generators();
            MixedMaterials();
            ScaledTransforms();
            Triangles();
            Connections();
            InsideCorners();

            ChiselNodeHierarchyManager.Update();
            ChiselModelManager.Instance.UpdateModels();
            foreach (var generator in s_Model.GetComponentsInChildren<ChiselGeneratorComponent>())
                AssignMaterial(generator);
            ChiselNodeHierarchyManager.Update();
            ChiselModelManager.Instance.UpdateModels();
            // what is connected by hand, now that its brushes are there
            if (s_Corner)
                ChiselHotspotLinks.Connect(Face(s_Corner, p => -p.z), Face(s_Corner, p => p.x));
            if (s_ShownLeft && s_ShownRight)
                ChiselHotspotLinks.SetBorder(Face(s_ShownLeft, p => -p.z), Face(s_ShownRight, p => -p.z), ChiselHotspotBorderState.Shown);
            if (s_Foot && s_Slab)
                ChiselHotspotLinks.SetBorder(Face(s_Foot, p => -p.z), Face(s_Slab, p => p.y), ChiselHotspotBorderState.Hidden);
            ChiselNodeHierarchyManager.Update();
            ChiselModelManager.Instance.UpdateModels();

            return EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(), kScene);
        }

        // The sheet's material, its trim sheet drawn with it (ChiselSurfaceMetadata.hotspotSheets)
        static Material CreateMaterial()
        {
            var material = CreateSheet("HotspotTest", out var layout);
            var trim     = CreateSheet("HotspotTrim", out _);
            if (material == null || trim == null)
                return null;
            s_Layout = layout;
            var metadata = material.GetMetadataOfType<ChiselSurfaceMetadata>();
            metadata.hotspotSheets = new List<Material> { trim };
            EditorUtility.SetDirty(metadata);
            AssetDatabase.SaveAssets();
            return material;
        }

        // A sheet's material and layout (<name>.png, .mat, .layout.json, .asset in the folder); the layout read again from
        // the json every build
        static Material CreateSheet(string name, out ChiselHotspotLayout layout)
        {
            layout = null;
            var texturePath = $"{kFolder}/{name}.png";
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
            if (texture == null)
            {
                Debug.LogError($"No hotspot test texture at {texturePath}; run SourceAssets~/HotspotTest/make_hotspot_test_texture.py {kFolder}");
                return null;
            }
            var materialPath = $"{kFolder}/{name}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(material, materialPath);
            }
            material.SetTexture("_BaseMap", texture);
            material.SetColor("_BaseColor", Color.white);
            EditorUtility.SetDirty(material);

            var jsonPath = $"{kFolder}/{name}.layout.json";
            var json = AssetDatabase.LoadAssetAtPath<TextAsset>(jsonPath);
            if (json == null)
            {
                Debug.LogError($"No hotspot test layout at {jsonPath}; run SourceAssets~/HotspotTest/make_hotspot_test_texture.py {kFolder}");
                return null;
            }
            var layoutPath = $"{kFolder}/{name}.asset";
            layout = AssetDatabase.LoadAssetAtPath<ChiselHotspotLayout>(layoutPath);
            if (layout == null)
            {
                layout = ScriptableObject.CreateInstance<ChiselHotspotLayout>();
                AssetDatabase.CreateAsset(layout, layoutPath);
            }
            if (!ChiselHotspotLayout.TryReadJson(json.text, layout, out var error))
            {
                Debug.LogError($"{jsonPath}: {error}");
                return null;
            }
            layout.texture = texture;
            EditorUtility.SetDirty(layout);
            var metadata = material.HasMetadataOfType<ChiselSurfaceMetadata>()
                         ? material.GetMetadataOfType<ChiselSurfaceMetadata>()
                         : material.AddMetadataOfType<ChiselSurfaceMetadata>();
            metadata.hotspotLayout = layout;
            EditorUtility.SetDirty(metadata);
            return material;
        }

        static void AssignMaterial(ChiselGeneratorComponent generator)
        {
            var surfaces = generator.SurfaceDefinition?.surfaces;
            if (surfaces == null)
                return;
            // "default N ...": its first N surfaces keep the default material
            var parts = generator.name.Split(' ');
            var keep  = parts.Length > 1 && parts[0] == "default" && int.TryParse(parts[1], out var count) ? count : 0;
            // "pin NAME ...": its front (facing -z) keeps that tile
            var pin   = parts.Length > 1 && parts[0] == "pin" && s_Layout ? s_Layout.tiles.FindIndex(tile => tile.name == parts[1]) : -1;
            var front = pin >= 0 ? Face(generator, p => -p.z) : null;
            for (int i = keep; i < surfaces.Length; i++)
            {
                surfaces[i].RenderMaterial = s_Material;
                if (front != null && i == front.descriptionIndex)
                {
                    var details = surfaces[i].surfaceDetails;
                    details.hotspot.pinnedTile = s_Layout.tiles[pin].id;
                    surfaces[i].surfaceDetails = details;
                }
            }
            generator.OnValidate();
        }

        // A labelled row; its brushes are laid out along +x
        static Transform Station(string title)
        {
            var station = new GameObject(title).transform;
            station.SetParent(s_Model, false);
            station.localPosition = new Vector3(0, 0, -s_Row * kRowSpacing);
            s_Row++;

            var label = new GameObject("label: " + title);
            label.transform.SetParent(station, false);
            label.transform.localPosition = new Vector3(-2, 6, 0);
            var text = label.AddComponent<TextMesh>();
            text.text          = title;
            text.characterSize = 0.25f;
            text.fontSize      = 48;
            text.anchor        = TextAnchor.MiddleRight;
            return station;
        }

        static T Add<T>(Transform parent, string name, Vector3 position, Vector3 euler = default, Vector3? scale = null) where T : ChiselNodeComponent
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            go.transform.localEulerAngles = euler;
            go.transform.localScale = scale ?? Vector3.one;
            return go.AddComponent<T>();
        }

        static ChiselBoxComponent Box(Transform parent, string name, Vector3 position, Vector3 size, Vector3 euler = default, Vector3? scale = null)
        {
            var box = Add<ChiselBoxComponent>(parent, name, position, euler, scale);
            box.Min = new float3(-size.x / 2, 0, -size.z / 2);
            box.Max = new float3(size.x / 2, size.y, size.z / 2);
            return box;
        }

        // A thin wall facing -z, its front face width x height
        static ChiselBoxComponent Wall(Transform parent, string name, ref float x, float width, float height, float gap = 1)
        {
            var wall = Box(parent, name, new Vector3(x + width / 2, 0, 0), new Vector3(width, height, 0.25f));
            x += width + gap;
            return wall;
        }

        static void Subtract(ChiselGeneratorComponent generator) { generator.Operation = CSGOperationType.Subtractive; }

        // Faces whose aspect matches one tile each: 1:1, 2:1, 4:1, 1:2, 1:4, and strips of 16:1 and 64:1
        static void AspectRatios()
        {
            var s = Station("01 aspect ratios (walls facing -z)");
            float x = 0;
            Wall(s, "1:1 2x2", ref x, 2, 2);
            Wall(s, "2:1 4x2", ref x, 4, 2);
            Wall(s, "4:1 8x2", ref x, 8, 2);
            Wall(s, "1:2 2x4", ref x, 2, 4);
            Wall(s, "1:4 1x4", ref x, 1, 4);
            Wall(s, "16:1 16x1", ref x, 16, 1);
            Wall(s, "64:1 32x0.5", ref x, 32, 0.5f);
        }

        // The same aspect at different sizes: smaller faces should take smaller tiles (texel density)
        static void Sizes()
        {
            var s = Station("02 sizes, one aspect (texel density)");
            float x = 0;
            foreach (var size in new[] { 0.25f, 0.5f, 1, 2, 4, 8 })
                Wall(s, $"square {size}", ref x, size, size);
        }

        // Walls in every direction, floor and ceiling faces, slopes and rotated brushes
        static void Orientation()
        {
            var s = Station("03 orientation (walls, floor, ceiling, slopes, rotated)");
            Box(s, "cube 2 (4 walls, top, bottom)", new Vector3(1, 1, 0), new Vector3(2, 2, 2));
            Box(s, "ramp 30 deg", new Vector3(6, 0.5f, 0), new Vector3(2, 0.25f, 4), new Vector3(-30, 0, 0));
            Box(s, "box yawed 45 deg", new Vector3(10, 0, 0), new Vector3(2, 2, 2), new Vector3(0, 45, 0));
            Box(s, "box rolled 30 deg", new Vector3(14, 0.5f, 0), new Vector3(3, 1, 1), new Vector3(0, 0, 30));
            Box(s, "box tumbled (20, 35, 10)", new Vector3(18, 1, 0), new Vector3(2, 1, 1.5f), new Vector3(20, 35, 10));
            Box(s, "upside down 4x1 (yaw 0, roll 180)", new Vector3(23, 2, 0), new Vector3(4, 1, 0.25f), new Vector3(0, 0, 180));
        }

        // Triangles, trapezoids, hexagons, circles (radial), a cone
        static void NonRectangularFaces()
        {
            var s = Station("04 non-rectangular faces (triangles, trapezoids, polygons, circles)");
            var tri = Add<ChiselCylinderComponent>(s, "triangular prism (triangle caps)", new Vector3(1, 0, 0));
            tri.Sides = 3; tri.Diameter = 2; tri.Height = 2;
            var trap = Add<ChiselCylinderComponent>(s, "frustum, 4 sides (trapezoid sides)", new Vector3(5, 0, 0));
            trap.Sides = 4; trap.Type = CylinderShapeType.ConicalFrustum; trap.BottomDiameterX = trap.BottomDiameterZ = 3; trap.TopDiameterX = trap.TopDiameterZ = 1.5f; trap.Height = 2;
            var hex = Add<ChiselCylinderComponent>(s, "hexagonal prism (hexagon caps)", new Vector3(9, 0, 0));
            hex.Sides = 6; hex.Diameter = 2.5f; hex.Height = 1;
            var round = Add<ChiselCylinderComponent>(s, "cylinder, 24 sides (round caps, radial)", new Vector3(13, 0, 0));
            round.Sides = 24; round.Diameter = 3; round.Height = 1.5f;
            var cone = Add<ChiselCylinderComponent>(s, "cone, 16 sides (triangle sides, radial base)", new Vector3(17, 0, 0));
            cone.Sides = 16; cone.Type = CylinderShapeType.Cone; cone.Diameter = 2.5f; cone.Height = 2;
            var disc = Add<ChiselCylinderComponent>(s, "thin disc, 32 sides (radial)", new Vector3(21, 0, 0));
            disc.Sides = 32; disc.Diameter = 3; disc.Height = 0.1f;
        }

        // What is mapped is the visible part of a face after the CSG: notches, holes, pieces, L shapes, sunk parts
        static void ClippedByCSG()
        {
            var s = Station("05 clipped by the CSG (door, window, split, L-shape, sunk)");
            float x = 0;
            Wall(s, "wall with door (U-shaped face)", ref x, 6, 3, 0);
            Subtract(Box(s, "door", new Vector3(3, -0.1f, 0), new Vector3(1.2f, 2.3f, 1)));
            x += 1;
            var window = x;
            Wall(s, "wall with window (face with a hole)", ref x, 6, 3, 0);
            Subtract(Box(s, "window", new Vector3(window + 3, 1, 0), new Vector3(1.5f, 1, 1)));
            x += 1;
            var split = x;
            Wall(s, "wall split in two (two pieces)", ref x, 6, 3, 0);
            Subtract(Box(s, "slot", new Vector3(split + 3, -0.1f, 0), new Vector3(0.5f, 3.2f, 1)));
            x += 1;
            Box(s, "L part A 4x2 top", new Vector3(x + 2, 0, 0), new Vector3(4, 1, 2));
            Box(s, "L part B 2x4 top", new Vector3(x + 1, 0, 1), new Vector3(2, 1, 4));
            x += 6;
            Box(s, "block", new Vector3(x + 2, 0, 0), new Vector3(4, 1, 4));
            Box(s, "pillar sunk into block (shorter visible sides)", new Vector3(x + 2, -0.5f, 0), new Vector3(1, 4, 1));
        }

        // Faces larger than any tile, and long strips that should repeat a strip tile along their length
        static void LargerThanAnyTile()
        {
            var s = Station("06 larger than any tile (repeat), strips (repeat-u / repeat-v)");
            Box(s, "floor 40x3 (top face 40x3)", new Vector3(20, -0.25f, 0), new Vector3(40, 0.25f, 3));
            Box(s, "long wall 40x3", new Vector3(20, 0, 2), new Vector3(40, 3, 0.25f));
            Box(s, "baseboard 20x0.2 (repeat-u)", new Vector3(10, 0, 1.75f), new Vector3(20, 0.2f, 0.25f));
            Box(s, "pillar 0.25x8 (repeat-v)", new Vector3(30, 0, 0), new Vector3(0.25f, 8, 0.25f));
        }

        // Identical faces: deterministic variation across identical tiles (A1/A2, D1/D2, G1..G18)
        static void Variation()
        {
            var s = Station("07 variation (identical crates and panels)");
            for (int i = 0; i < 6; i++)
                Box(s, $"crate 1x1x1 #{i}", new Vector3(i * 1.5f, 0, 0), new Vector3(1, 1, 1));
            float x = 10;
            for (int i = 0; i < 6; i++)
                Wall(s, $"panel 2x1 #{i}", ref x, 2, 1, 0.5f);
        }

        // Generated shapes with many small faces: stairs, spiral stairs, sphere, capsule, torus
        static void Generators()
        {
            var s = Station("08 generators (stairs, spiral stairs, sphere, capsule, torus)");
            Add<ChiselLinearStairsComponent>(s, "linear stairs", new Vector3(0, 0, 0));
            Add<ChiselSpiralStairsComponent>(s, "spiral stairs", new Vector3(8, 0, 0));
            Add<ChiselSphereComponent>(s, "sphere", new Vector3(14, 0, 0));
            Add<ChiselCapsuleComponent>(s, "capsule", new Vector3(18, 0, 0));
            Add<ChiselTorusComponent>(s, "torus", new Vector3(23, 0, 0));
        }

        // Hotspot and ordinary mapping side by side: surfaces keep their own UVs unless their material has a layout
        static void MixedMaterials()
        {
            var s = Station("09 mixed materials (first 3 surfaces default material)");
            Box(s, "default 3 cube 2", new Vector3(1, 0, 0), new Vector3(2, 2, 2));
        }

        // A brush given as planes (outward normal n, n.p + w = 0), in its own space
        static ChiselBrushComponent Planes(Transform parent, string name, Vector3 position, params float4[] planes)
        {
            var brush = Add<ChiselBrushComponent>(parent, name, position);
            brush.definition.SetPlanes(planes);
            brush.OnValidate();
            return brush;
        }

        static float4 Plane(float3 normal, float3 point)
        {
            normal = math.normalize(normal);
            return new float4(normal, -math.dot(normal, point));
        }

        // A prism of depth 0.5 whose front (facing -z) is the triangle a, b, c in the xy plane, counter-clockwise
        static ChiselBrushComponent Prism(Transform parent, string name, Vector3 position, float2 a, float2 b, float2 c)
        {
            float4 Side(float2 from, float2 to) => Plane(new float3(to.y - from.y, from.x - to.x, 0), new float3(from, 0));
            return Planes(parent, name, position,
                          new float4(0, 0, -1, 0), new float4(0, 0, 1, -0.5f), Side(a, b), Side(b, c), Side(c, a));
        }

        // Triangular faces for triangular tiles: right triangles at 1:1, 2:1 and 1:2, mirrored, isosceles, and one the CSG cuts
        static void Triangles()
        {
            var s = Station("11 triangles (right, mirrored, isosceles, cut by the CSG)");
            Prism(s, "right triangle 2:1 (2x1)",          new Vector3(0, 0, 0),  new float2(0, 0), new float2(2, 0), new float2(2, 1));
            Prism(s, "right triangle 1:1 (1x1)",          new Vector3(3, 0, 0),  new float2(0, 0), new float2(1, 0), new float2(1, 1));
            Prism(s, "right triangle 1:2 (1x2)",          new Vector3(5, 0, 0),  new float2(0, 0), new float2(1, 0), new float2(1, 2));
            Prism(s, "right triangle 4x2 (larger)",       new Vector3(7, 0, 0),  new float2(0, 0), new float2(4, 0), new float2(4, 2));
            Prism(s, "mirrored right triangle 2:1",       new Vector3(12, 0, 0), new float2(0, 0), new float2(2, 0), new float2(0, 1));
            Prism(s, "isosceles 2x2 (apex up)",           new Vector3(15, 0, 0), new float2(0, 0), new float2(2, 0), new float2(1, 2));
            Prism(s, "isosceles 2x1 (wide)",              new Vector3(18, 0, 0), new float2(0, 0), new float2(2, 0), new float2(1, 1));
            Prism(s, "upside down isosceles 2x2",         new Vector3(21, 0, 0), new float2(0, 2), new float2(1, 0), new float2(2, 2));
            float x = 24;
            Wall(s, "wall cut diagonally by the CSG (triangle left)", ref x, 3, 3, 0);
            Subtract(Box(s, "diagonal cutter", new Vector3(24 + 1.5f, 1.5f, 0), new Vector3(5, 3, 1), new Vector3(0, 0, 45)));
        }

        static ChiselBoxComponent s_Corner, s_ShownLeft, s_ShownRight, s_Foot, s_Slab;

        // Coplanar surfaces that touch connect by themselves (one tile across them, the border only round them all);
        // round a corner only by hand, and then the tile wraps round it. By hand, the border along the edge two surfaces
        // share can be shown or hidden.
        static void Connections()
        {
            var s = Station("12 connections (touching coplanar walls, offset bricks, a corner connected by hand, a border shown and one hidden by hand)");
            Box(s, "two walls, left", new Vector3(1, 0, 0), new Vector3(2, 2, 0.25f));
            Box(s, "two walls, right", new Vector3(3, 0, 0), new Vector3(2, 2, 0.25f));
            Box(s, "offset bricks, lower", new Vector3(7, 0, 0), new Vector3(4, 1, 0.25f));
            Box(s, "offset bricks, upper", new Vector3(9, 1, 0), new Vector3(4, 1, 0.25f));
            s_Corner = Box(s, "block, front and right side connected by hand", new Vector3(15, 0, 0), new Vector3(2, 2, 2));
            s_ShownLeft  = Box(s, "two walls, the border shown where they meet, left", new Vector3(20, 0, 0), new Vector3(2, 2, 0.25f));
            s_ShownRight = Box(s, "two walls, the border shown where they meet, right", new Vector3(22, 0, 0), new Vector3(2, 2, 0.25f));
            s_Slab = Box(s, "slab", new Vector3(28, 0, 0), new Vector3(5, 0.5f, 2));
            s_Foot = Box(s, "wall on the slab, no border along its foot", new Vector3(28, 0.5f, 0), new Vector3(4, 2, 0.25f));
        }

        // Doorways and windows on tiles with inside corner pieces: B1 four, A2 one turned (the sheet's IC1, IC2)
        static void InsideCorners()
        {
            var s = Station("13 inside corners (B1: four pieces, A2: one turned; doorways, windows)");
            float x = 0;
            foreach (var (tile, width) in new[] { ("B1", 6f), ("A2", 3f) })
            {
                var door = x;
                Wall(s, $"pin {tile} wall with door", ref x, width, 3, 0);
                Subtract(Box(s, "door", new Vector3(door + width / 2, -0.1f, 0), new Vector3(1.2f, 2.3f, 1)));
                x += 1;
                var window = x;
                Wall(s, $"pin {tile} wall with window", ref x, width, 3, 0);
                Subtract(Box(s, "window", new Vector3(window + width / 2, 1, 0), new Vector3(1.5f, 1, 1)));
                x += 1;
            }
        }

        // The face of a generator's first brush whose corners score highest on average
        static SurfaceReference Face(ChiselGeneratorComponent generator, System.Func<float3, float> score)
        {
            var brush = (CSGTreeBrush)generator.TopTreeNode;
            var blob = BrushMeshManager.GetBrushMeshBlob(brush.BrushMesh);
            if (!blob.IsCreated)
                return null;
            ref var mesh = ref blob.Value;
            var best = -1; var bestScore = float.NegativeInfinity;
            for (int p = 0; p < mesh.polygons.Length; p++)
            {
                var sum = 0f;
                for (int e = 0; e < mesh.polygons[p].edgeCount; e++)
                    sum += score(mesh.localVertices[mesh.halfEdges[mesh.polygons[p].firstEdge + e].vertexIndex]);
                sum /= mesh.polygons[p].edgeCount;
                if (sum > bestScore) { bestScore = sum; best = p; }
            }
            return best < 0 ? null : new SurfaceReference(generator, mesh.polygons[best].descriptionIndex, brush, best);
        }

        // World size follows the transform: scaled brushes take tiles by their scaled size
        static void ScaledTransforms()
        {
            var s = Station("10 scaled transforms (the texture scales with a locked brush)");
            Box(s, "cube 1 scaled (2,1,1)", new Vector3(1, 0, 0), new Vector3(1, 1, 1), default, new Vector3(2, 1, 1));
            Box(s, "cube 2 scaled 0.5", new Vector3(5, 0, 0), new Vector3(2, 2, 2), default, new Vector3(0.5f, 0.5f, 0.5f));
            Box(s, "cube 1 unscaled (reference)", new Vector3(8, 0, 0), new Vector3(1, 1, 1));
        }
    }
}
