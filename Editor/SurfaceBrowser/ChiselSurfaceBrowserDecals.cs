using System.Collections.Generic;
using Chisel.Components;
using Chisel.Core;
using UnityEditor;
using UnityEngine;

namespace Chisel.Editors
{
    // The decal images of the materials marked as decals, and placing them: one per tile of a decal sheet, or the whole
    // texture for a material without a layout. An image dragged into the scene view is placed where it's dropped
#if UNITY_6000_7_OR_NEWER
    [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
#endif
    [InitializeOnLoad]
    public static class ChiselSurfaceBrowserDecals
    {
        public const string kDragKey = "ChiselDecalImage";

        static ChiselSurfaceBrowserDecals()
        {
            SceneView.duringSceneGui -= OnSceneGUI;
            SceneView.duringSceneGui += OnSceneGUI;
            AssemblyReloadEvents.beforeAssemblyReload -= ClearPreview;
            AssemblyReloadEvents.beforeAssemblyReload += ClearPreview;
            UnityEditor.SceneManagement.EditorSceneManager.sceneSaving -= OnSceneSaving;
            UnityEditor.SceneManagement.EditorSceneManager.sceneSaving += OnSceneSaving;
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
            EditorApplication.delayCall += RemoveStrayPreviews;
        }

        /// <summary>Starts dragging an image; not as a material, so dropping it doesn't paint a surface</summary>
        public static void StartDrag(Image image)
        {
            DragAndDrop.PrepareStartDrag();
            DragAndDrop.objectReferences = System.Array.Empty<Object>();
            DragAndDrop.SetGenericData(kDragKey, image);
            DragAndDrop.StartDrag(image.name);
        }

        // the brush surface under a point of the scene view
        static bool PickSurface(Vector2 mousePosition, out ChiselIntersection hit)
        {
            hit = default;
            var intersections = new List<ChiselIntersection>();
            ChiselSelectionManager.GetOverlappingObjects(mousePosition, intersections);
            foreach (var intersection in intersections)
            {
                if (intersection.treeNode != null && intersection.model != null && intersection.brushIntersection.brush.Valid)
                {
                    hit = intersection;
                    return true;
                }
            }
            return false;
        }

        // While an image is dragged over the scene view: a real decal where it would be placed, made like the placed one
        // so it looks exactly the same. It is hidden, never saved and not in the undo history, and goes again when the
        // drag ends, leaves, is dropped, or anything else comes first (a reload, saving, play mode)
        public const string kPreviewName = "Decal Preview";
        static ChiselDecalComponent s_Preview;

        public static ChiselDecalComponent PreviewDecal => s_Preview;

        /// <summary>The preview decal for an image, made when there is none or it shows another</summary>
        public static ChiselDecalComponent EnsurePreview(Image image)
        {
            if (s_Preview != null && (s_Preview.Material != image.material || s_Preview.ImageTile != image.tile))
                ClearPreview();
            if (s_Preview != null)
                return s_Preview;
            var gameObject = new GameObject(kPreviewName) { hideFlags = HideFlags.HideAndDontSave };
            s_Preview = gameObject.AddComponent<ChiselDecalComponent>();
            s_Preview.Material  = image.material;
            s_Preview.ImageTile = image.tile;
            ChiselDecalManager.HierarchyChanged();
            return s_Preview;
        }

        public static void ClearPreview()
        {
            if (s_Preview != null)
                Object.DestroyImmediate(s_Preview.gameObject);
            s_Preview = null;
            ChiselDecalManager.SetDirty();
            SceneView.RepaintAll();
        }

        // previews left over from anything that went wrong, found by their name and flags
        static void RemoveStrayPreviews()
        {
            foreach (var decal in Resources.FindObjectsOfTypeAll<ChiselDecalComponent>())
            {
                if (decal != null && decal != s_Preview && decal.gameObject.name == kPreviewName &&
                    (decal.gameObject.hideFlags & HideFlags.DontSave) != 0)
                    Object.DestroyImmediate(decal.gameObject);
            }
        }

        // a decal outside any model draws on the models of its scene; a hidden object isn't in one until moved there
        internal static void MoveToScene(ChiselDecalComponent decal, UnityEngine.SceneManagement.Scene scene)
        {
            if (!scene.IsValid() || decal.gameObject.scene == scene)
                return;
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(decal.gameObject, scene);
            ChiselDecalManager.HierarchyChanged();
        }

        static void OnSceneSaving(UnityEngine.SceneManagement.Scene scene, string path) { ClearPreview(); }
        static void OnPlayModeChanged(PlayModeStateChange change) { ClearPreview(); }

        // The preview where a drop at this point would place the decal; hidden where nothing would be placed
        static void UpdatePreview(Image image, ChiselDecalShape shape, bool onSurface, ChiselIntersection hit, Vector2 mousePosition)
        {
            if (shape == ChiselDecalShape.Trim)
            {
                var decal = EnsurePreview(image);
                ConfigureTrim(decal);
                if (!ChiselDecalEditor.PlaceTrimAt(decal, mousePosition, recordUndo: false))
                {
                    ClearPreview();
                    return;
                }
                var target = decal.Targets.Count > 0 ? decal.Targets[0].brush : null;
                if (target != null)
                    MoveToScene(decal, target.gameObject.scene);
            } else if (onSurface)
            {
                var decal = EnsurePreview(image);
                ConfigureBox(decal, image.material, image.tile, hit.worldPlaneIntersection, hit.worldPlane.normal);
                MoveToScene(decal, hit.model.gameObject.scene);
            } else
            {
                ClearPreview();
                return;
            }
            ChiselDecalManager.SetDirty();
        }

        static void OnSceneGUI(SceneView sceneView)
        {
            var evt = Event.current;
            if (evt.type == EventType.DragExited)
            {
                ClearPreview();
                return;
            }
            // a drag that ended without telling us leaves no preview
            if (s_Preview != null && DragAndDrop.GetGenericData(kDragKey) == null)
            {
                ClearPreview();
                return;
            }
            if (evt.type != EventType.DragUpdated && evt.type != EventType.DragPerform)
                return;
            if (DragAndDrop.GetGenericData(kDragKey) is not Image image || image.material == null)
            {
                ClearPreview();
                return;
            }
            var shape = ShapeOf(image.material);
            var onSurface = PickSurface(evt.mousePosition, out var hit);
            DragAndDrop.visualMode = onSurface ? DragAndDropVisualMode.Copy : DragAndDropVisualMode.Rejected;
            if (evt.type == EventType.DragPerform)
            {
                ClearPreview();
                DragAndDrop.AcceptDrag();
                DragAndDrop.SetGenericData(kDragKey, null);
                Drop(image, shape, onSurface, hit, evt.mousePosition);
            } else
            {
                UpdatePreview(image, shape, onSurface, hit, evt.mousePosition);
                sceneView.Repaint();
            }
            evt.Use();
        }

        // a box where it's dropped, a trim on the edge nearest to it; a polygon starts drawing
        static void Drop(Image image, ChiselDecalShape shape, bool onSurface, ChiselIntersection hit, Vector2 mousePosition)
        {
            ChiselSurfaceBrowserPrefs.MarkUsed(image.guid);
            switch (shape)
            {
                case ChiselDecalShape.Trim:
                {
                    var decal = CreateTrim(image.material, image.tile);
                    if (!ChiselDecalEditor.PlaceTrimAt(decal, mousePosition))
                        ChiselDecalEditor.StartPickingEdge(decal, removeIfCancelled: true);
                    else
                        Selection.activeGameObject = decal.gameObject;
                    return;
                }
                default:
                {
                    if (!onSurface)
                        return;
                    var decal = CreateBoxAt(hit.treeNode.transform, image.material, image.tile, hit.worldPlaneIntersection, hit.worldPlane.normal);
                    if (decal == null)
                        return;
                    Undo.RegisterCreatedObjectUndo(decal.gameObject, "Place Decal");
                    Selection.activeGameObject = decal.gameObject;
                    ChiselDecalManager.SetDirty();
                    ChiselSceneMaterialUsage.SetDirty();
                    return;
                }
            }
        }
        public sealed class Image
        {
            public Material material;
            public string   guid;       // the material's
            public int      tile;       // 0: the whole texture
            public string   name;
            public int      index;      // 1 based, of count on its material
            public int      count;

            public string Key => guid + ":" + tile;
        }

        /// <summary>How a decal material is placed: along an edge (Trim) or as a box; a polygon is drawn with a
        /// surface's own material, so it isn't one of these</summary>
        public static ChiselDecalShape ShapeOf(Material material)
        {
            return ChiselDecalComponent.DefaultsOf(material).shape == ChiselDecalShape.Trim ? ChiselDecalShape.Trim : ChiselDecalShape.Box;
        }

        /// <summary>The texture a decal draws: the material's main one, else its first texture</summary>
        public static Texture ImageTextureOf(Material material)
        {
            if (material == null)
                return null;
            if (material.mainTexture != null)
                return material.mainTexture;
            foreach (var name in material.GetTexturePropertyNames())
            {
                var texture = material.GetTexture(name);
                if (texture != null)
                    return texture;
            }
            return null;
        }

        /// <summary>The color a material tints its texture with (URP's base color, or the built-in color), white without one</summary>
        public static Color ColorOf(Material material)
        {
            if (material == null)
                return Color.white;
            if (material.HasColor("_BaseColor")) return material.GetColor("_BaseColor");
            if (material.HasColor("_Color"))     return material.GetColor("_Color");
            return Color.white;
        }

        public static bool IsDecal(Material material)
        {
            var metadata = material != null ? material.GetMetadataOfType<ChiselSurfaceMetadata>() : null;
            return metadata != null && metadata.decalDefaults.isDecal;
        }

        /// <summary>A decal material's images: each tile of its hotspot layout, or its whole texture without one; none for
        /// a material that isn't a decal</summary>
        public static List<Image> ImagesOf(Material material, string guid)
        {
            var images = new List<Image>();
            if (!IsDecal(material))
                return images;
            var layout = material.GetMetadataOfType<ChiselSurfaceMetadata>().hotspotLayout;
            if (layout != null && layout.tiles != null && layout.tiles.Count > 0)
            {
                foreach (var tile in layout.tiles)
                {
                    var name = string.IsNullOrEmpty(tile.name) ? $"{material.name} {images.Count + 1}" : tile.name;
                    images.Add(new Image { material = material, guid = guid, tile = tile.id, name = name });
                }
            } else
                images.Add(new Image { material = material, guid = guid, tile = 0, name = material.name });
            for (int i = 0; i < images.Count; i++)
            {
                images[i].index = i + 1;
                images[i].count = images.Count;
            }
            return images;
        }

        /// <summary>
        /// A box decal of an image on a surface: at the middle of its outline (world space), facing it, a child of
        /// parent, with the material's decal defaults
        /// </summary>
        public static ChiselDecalComponent CreateBox(Transform parent, Material material, int tile, IReadOnlyList<Vector3> outline, Vector3 normal)
        {
            if (material == null || outline == null || outline.Count == 0 || normal.sqrMagnitude < 1e-12f)
                return null;
            var center = Vector3.zero;
            foreach (var point in outline)
                center += point;
            center /= outline.Count;
            return CreateBoxAt(parent, material, tile, center, normal);
        }

        /// <summary>A box decal of an image at a point on a surface, facing it, with the material's decal defaults</summary>
        public static ChiselDecalComponent CreateBoxAt(Transform parent, Material material, int tile, Vector3 center, Vector3 normal)
        {
            if (material == null || normal.sqrMagnitude < 1e-12f)
                return null;
            var gameObject = new GameObject(ChiselDecalComponent.kNodeTypeName);
            var decal = gameObject.AddComponent<ChiselDecalComponent>();
            ConfigureBox(decal, material, tile, center, normal);
            if (parent != null)
                gameObject.transform.SetParent(parent, true);
            return decal;
        }

        /// <summary>A decal made a box decal of an image at a point on a surface, facing it, with the material's decal
        /// defaults; the same for the placed decal and its preview</summary>
        public static void ConfigureBox(ChiselDecalComponent decal, Material material, int tile, Vector3 center, Vector3 normal)
        {
            normal.Normalize();
            var up = Mathf.Abs(Vector3.Dot(normal, Vector3.up)) < 0.99f ? Vector3.up : Vector3.forward;
            // its blue arrow (Z) out of the surface, as one expects of something stuck on it
            decal.transform.SetPositionAndRotation(center, Quaternion.LookRotation(normal, up));
            decal.Material  = material;
            decal.ImageTile = tile;
            var defaults = ChiselDecalComponent.DefaultsOf(material);
            // a box, whatever the material's shape is
            if (defaults.shape != ChiselDecalShape.Box)
                defaults = defaults.WithShape(ChiselDecalShape.Box);
            decal.Shape = ChiselDecalShape.Box;
            var settings = decal.Settings;
            settings.projection  = defaults.projection;
            settings.reach       = defaults.reach;
            settings.transparent = defaults.transparent;
            settings.size        = defaults.SizeFor(ChiselDecalComponent.ImageAspectOf(material, tile), settings.size.z);
            decal.Settings = settings;
        }

        /// <summary>A decal made a trim decal with its material's defaults; the same for the placed decal and its preview</summary>
        public static void ConfigureTrim(ChiselDecalComponent decal)
        {
            if (decal.Shape == ChiselDecalShape.Trim)
                return;
            decal.ApplyMaterialDefaults();
            decal.Shape = ChiselDecalShape.Trim;
        }

        /// <summary>A box decal on each selected surface, undoably</summary>
        public static List<ChiselDecalComponent> PlaceOn(IEnumerable<SurfaceReference> surfaces, Material material, int tile)
        {
            var placed  = new List<ChiselDecalComponent>();
            var outline = new List<Vector3>();
            Undo.SetCurrentGroupName("Place Decals");
            var group = Undo.GetCurrentGroup();
            foreach (var surface in new List<SurfaceReference>(surfaces))
            {
                if (surface?.node == null || !ChiselSurfaceShapes.SurfaceOutline(surface.node, surface.surfaceIndex, outline, out var plane))
                    continue;
                var decal = CreateBox(surface.node.transform, material, tile, outline, plane.normal);
                if (decal == null)
                    continue;
                Undo.RegisterCreatedObjectUndo(decal.gameObject, "Place Decals");
                placed.Add(decal);
            }
            Undo.CollapseUndoOperations(group);
            ChiselDecalManager.SetDirty();
            ChiselSceneMaterialUsage.SetDirty();
            return placed;
        }

        /// <summary>A trim decal of an image, to be put on an edge picked in the scene view</summary>
        public static ChiselDecalComponent CreateTrim(Material material, int tile)
        {
            var gameObject = new GameObject(ChiselDecalComponent.kNodeTypeName);
            var decal = gameObject.AddComponent<ChiselDecalComponent>();
            decal.Material  = material;
            decal.ImageTile = tile;
            ConfigureTrim(decal);
            Undo.RegisterCreatedObjectUndo(gameObject, "Place Trim Decal");
            return decal;
        }
    }

    public sealed partial class ChiselSurfaceBrowserWindow
    {
        // the image of the selected decal material placed next (a tile of its sheet, 0 for the whole texture)
        [SerializeField] int selectedTile;

        Texture2D checker;

        // a transparency checkerboard under an image
        void DrawChecker(Rect rect)
        {
            if (checker == null)
            {
                checker = new Texture2D(2, 2) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Repeat, hideFlags = HideFlags.HideAndDontSave };
                var light = new Color(0.42f, 0.42f, 0.42f);
                var dark  = new Color(0.32f, 0.32f, 0.32f);
                checker.SetPixels(new[] { light, dark, dark, light });
                checker.Apply();
            }
            GUI.DrawTextureWithTexCoords(rect, checker, new Rect(0, 0, rect.width / 16, rect.height / 16));
        }

        void DestroyDecalResources()
        {
            if (checker) DestroyImmediate(checker);
        }

        // a decal's image flat, only its own tile, at its aspect, over a checkerboard, fitted into rect
        void DrawImage(Rect rect, Material material, int tile)
        {
            var texture = ChiselSurfaceBrowserDecals.ImageTextureOf(material);
            var aspect  = ChiselDecalComponent.ImageAspectOf(material, tile);
            var fitted  = rect;
            if (aspect > rect.width / rect.height)
            {
                fitted.height = rect.width / aspect;
                fitted.y += (rect.height - fitted.height) / 2;
            } else
            {
                fitted.width = rect.height * aspect;
                fitted.x += (rect.width - fitted.width) / 2;
            }
            DrawChecker(fitted);
            // tinted by the material's color; a material without a texture is its color
            var tint = ChiselSurfaceBrowserDecals.ColorOf(material);
            if (texture == null)
            {
                EditorGUI.DrawRect(fitted, tint);
                return;
            }
            var prev = GUI.color;
            GUI.color = prev * tint;
            GUI.DrawTextureWithTexCoords(fitted, texture, ChiselDecalComponent.ImageRectOf(material, tile), true);
            GUI.color = prev;
        }

        // The selected decal material's preview, and for a sheet its images, to pick the one to place
        void DecalPreviewGUI(Material material, float contentWidth)
        {
            var images = ChiselSurfaceBrowserDecals.ImagesOf(material, selectedGuid);
            if (images.Count > 0 && !images.Exists(image => image.tile == selectedTile))
                selectedTile = images[0].tile;
            var previewRect = GUILayoutUtility.GetRect(contentWidth, previewHeight, GUILayout.Width(contentWidth), GUILayout.Height(previewHeight));
            if (Event.current.type == EventType.Repaint)
            {
                EditorGUI.DrawRect(previewRect, new Color(0.5f, 0.5f, 0.5f, 1));
                DrawImage(new Rect(previewRect.x + 8, previewRect.y + 8, previewRect.width - 16, previewRect.height - 16), material, selectedTile);
            }
            PreviewResizeGUI();

            if (images.Count <= 1)
                return;
            const float kThumb = 40;
            var columns = Mathf.Max(1, Mathf.FloorToInt(contentWidth / (kThumb + 4)));
            var rows    = Mathf.CeilToInt(images.Count / (float)columns);
            var area    = GUILayoutUtility.GetRect(contentWidth, rows * (kThumb + 4));
            var evt     = Event.current;
            for (int i = 0; i < images.Count; i++)
            {
                var cell = new Rect(area.x + (i % columns) * (kThumb + 4) + 2, area.y + (i / columns) * (kThumb + 4) + 2, kThumb, kThumb);
                if (evt.type == EventType.Repaint)
                {
                    if (images[i].tile == selectedTile)
                        EditorGUI.DrawRect(new Rect(cell.x - 2, cell.y - 2, cell.width + 4, cell.height + 4), new Color(0.24f, 0.48f, 0.9f, 0.8f));
                    DrawImage(cell, material, images[i].tile);
                }
                GUI.Label(cell, new GUIContent(string.Empty, images[i].name));
                if (evt.type == EventType.MouseDown && evt.button == 0 && cell.Contains(evt.mousePosition))
                {
                    selectedTile = images[i].tile;
                    evt.Use();
                }
            }
            var chosen = images.Find(image => image.tile == selectedTile);
            if (chosen != null)
                GUILayout.Label($"{chosen.name}  -  image {chosen.index} of {chosen.count}", EditorStyles.miniLabel);
        }

        void DecalInfoGUI(Material material)
        {
            InfoRow("In scene", $"{ChiselSceneMaterialUsage.DecalsUsing(material)} decals");
            var defaults = ChiselDecalComponent.DefaultsOf(material);
            var shape = ChiselSurfaceBrowserDecals.ShapeOf(material);
            var size  = defaults.SizeFor(ChiselDecalComponent.ImageAspectOf(material, selectedTile), 0);
            InfoRow(shape == ChiselDecalShape.Trim ? "Repeat" : "Size", shape == ChiselDecalShape.Trim ? $"{size.x:0.###} along the edge, {size.y:0.###} across" : $"{size.x:0.###} x {size.y:0.###}");
        }

        static string PlaceActionText(Material material)
        {
            if (ChiselSurfaceBrowserDecals.ShapeOf(material) == ChiselDecalShape.Trim)
                return "Pick edge in Scene view";
            var count = ChiselSurfaceSelectionManager.Selection.Count;
            return $"Place on {count} surface{(count == 1 ? "" : "s")}";
        }

        static bool CanPlace(Material material)
        {
            return ChiselSurfaceBrowserDecals.ShapeOf(material) == ChiselDecalShape.Trim || ChiselSurfaceSelectionManager.Selection.Count > 0;
        }

        ChiselSurfaceBrowserDecals.Image SelectedImage
        {
            get
            {
                var material = Load(Selected);
                if (!ChiselSurfaceBrowserDecals.IsDecal(material))
                    return null;
                var images = ChiselSurfaceBrowserDecals.ImagesOf(material, selectedGuid);
                return images.Find(image => image.tile == selectedTile) ?? (images.Count > 0 ? images[0] : null);
            }
        }

        void PlaceSelectedDecal()
        {
            var image = SelectedImage;
            if (image == null)
                return;
            ChiselSurfaceBrowserPrefs.MarkUsed(image.guid);
            if (ChiselSurfaceBrowserDecals.ShapeOf(image.material) == ChiselDecalShape.Trim)
            {
                ChiselDecalEditor.StartPickingEdge(ChiselSurfaceBrowserDecals.CreateTrim(image.material, image.tile), removeIfCancelled: true);
                return;
            }
            var placed = ChiselSurfaceBrowserDecals.PlaceOn(ChiselSurfaceSelectionManager.Selection, image.material, image.tile);
            if (placed.Count == 0)
                ShowNotification(new GUIContent("Select surfaces to place the decal on"));
        }
    }
}
