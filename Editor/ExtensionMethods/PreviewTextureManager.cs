using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

namespace Chisel.Editors
{
    public enum PreviewMesh { Sphere, Cube, Plane, Cylinder, Capsule }

    public static class PreviewTextureManager
    {
        const string kPreviewMeshKey = "Chisel.MaterialPreviewMesh";
        static readonly string[] kMeshNames = { "New-Sphere.fbx", "Cube.fbx", "Quad.fbx", "New-Cylinder.fbx", "New-Capsule.fbx" };

        // The shape every material preview shows, remembered across sessions
        public static PreviewMesh Mesh
        {
            get => (PreviewMesh)Mathf.Clamp(EditorPrefs.GetInt(kPreviewMeshKey, 0), 0, kMeshNames.Length - 1);
            set
            {
                if (value == Mesh)
                    return;
                EditorPrefs.SetInt(kPreviewMeshKey, (int)value);
                Clear();
                UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
            }
        }

        static PreviewRenderUtility previewUtility;

        public static void CleanUp()
        {
            if (previewUtility != null)
            {
                previewUtility.Cleanup();
                previewUtility = null;
            }
            Clear();
            s_Requests.Clear();
        }

        public static void Clear()
        {
            foreach (var preview in s_MaterialTextures.Values)
            {
                if (preview.texture)
                    UnityEngine.Object.DestroyImmediate(preview.texture);
            }
            s_MaterialTextures.Clear();
        }

        private static void Init()
        {
            if (previewUtility != null)
                return;
            previewUtility = new PreviewRenderUtility();
            AssemblyReloadEvents.beforeAssemblyReload -= CleanUp;
            AssemblyReloadEvents.beforeAssemblyReload += CleanUp;
        }

        class Styles { public readonly GUIStyle background = new GUIStyle("CurveEditorBackground"); };
        static Styles s_Styles;

        class PreviewRequest
        {
            public Material material;
            public Vector3  direction;
            public Vector3  size;
        }

        struct Preview
        {
            public Texture2D texture;
            public Vector3   direction;
            public Vector2   size;
        }

        readonly static Dictionary<Material, Preview>    s_MaterialTextures   = new Dictionary<Material, Preview>();
        readonly static List<PreviewRequest>             s_Requests           = new List<PreviewRequest>();


        static readonly Mesh[] s_Meshes = new Mesh[kMeshNames.Length];
        static Mesh GetPreviewMesh()
        {
            var index = (int)Mesh;
            if (!s_Meshes[index])
                s_Meshes[index] = Resources.GetBuiltinResource<Mesh>(kMeshNames[index]);
            return s_Meshes[index];
        }

        static Texture GetPreviewTexture(PreviewRenderUtility previewUtility, Vector2 previewDir, Rect region, Material material)
        {
            s_Styles ??= new Styles();

            var mesh        = GetPreviewMesh();
            var camera      = previewUtility.camera;

            var bounds      = mesh.bounds;
            var halfSize    = bounds.extents.magnitude;
            var distance    = 2.5f * halfSize;

            camera.transform.position   = -Vector3.forward * distance;
            camera.transform.rotation   = Quaternion.identity;
            camera.nearClipPlane        = distance - halfSize * 1.1f;
            camera.farClipPlane         = distance + halfSize * 1.1f;
            camera.fieldOfView          = 30.0f;
            camera.orthographicSize     = 1.0f;
            camera.clearFlags           = CameraClearFlags.Nothing;

            previewUtility.lights[0].intensity = 1.0f;
            previewUtility.lights[0].transform.rotation = Quaternion.Euler(40f, 40f, 0);
            previewUtility.lights[1].intensity = 1.0f;

            previewUtility.BeginStaticPreview(region);

            var fog = RenderSettings.fog;
            Unsupported.SetRenderSettingsUseFogNoDirty(false);
            camera.Render();
            Unsupported.SetRenderSettingsUseFogNoDirty(fog);

            // a plane faces the camera: it turns in place and tilts, never showing its back
            var rot = Mesh == PreviewMesh.Plane ? Quaternion.Euler(previewDir.y, 0, 0) * Quaternion.Euler(0, 0, previewDir.x)
                                                : Quaternion.Euler(previewDir.y, 0, 0) * Quaternion.Euler(0, previewDir.x, 0);
            var pos = rot * (-bounds.center);

            previewUtility.DrawMesh(mesh, pos, rot, material, 0);
            previewUtility.Render(true, false);
            return previewUtility.EndStaticPreview();
        }

        // Returns the material's last preview, rendered again on a repaint when its direction or size changed; until
        // then the old one is shown, so dragging the preview around keeps it on screen
        public static Texture2D GetPreviewTexture(Rect region, Vector3 direction, Material material)
        {
            if (!material)
                return null;

            s_MaterialTextures.TryGetValue(material, out var preview);
            if (preview.texture && preview.direction == direction && preview.size == region.size)
                return preview.texture;

            if (Event.current != null && Event.current.type == EventType.Repaint)
            {
                Render(material, direction, region.size);
                s_MaterialTextures.TryGetValue(material, out preview);
                return preview.texture;
            }

            s_Requests.Add(new PreviewRequest() { direction = direction, size = region.size, material = material });
            return preview.texture;
        }

        static void Render(Material material, Vector3 direction, Vector2 size)
        {
            Init();
            var texture = GetPreviewTexture(previewUtility, direction, new Rect(Vector2.zero, size), material) as Texture2D;
            if (!texture)
                return;
            if (s_MaterialTextures.TryGetValue(material, out var old) && old.texture)
                UnityEngine.Object.DestroyImmediate(old.texture);
            s_MaterialTextures[material] = new Preview { texture = texture, direction = direction, size = size };
        }

        public static bool Update()
        {
            Init();

            bool needRepaint = s_Requests.Count > 0;
            if (Event.current.type == EventType.Repaint)
            { 
                // the newest request for a material wins
                for (int i = s_Requests.Count - 1; i >= 0; i--)
                {
                    var request  = s_Requests[i];
                    var material = request.material;
                    if (material)
                    {
                        s_MaterialTextures.TryGetValue(material, out var preview);
                        if (!preview.texture || preview.direction != request.direction || preview.size != (Vector2)request.size)
                            Render(material, request.direction, request.size);
                    }
                }
                s_Requests.Clear();
            }
            return needRepaint;
        }
    }
}
